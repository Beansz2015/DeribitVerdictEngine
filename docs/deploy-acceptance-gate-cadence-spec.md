# Deploy acceptance gate — require CADENCE, not one row

**Status:** spec, not built. Handed to the trader 2026-08-22.
**Scope:** `tools/ops/collector.ps1`, function `Wait-DeployGate` only. **No engine code.**
**Companion:** the engine-side defect this exposed is already fixed — `rbRepeat.Checked = True` in `UI/MainForm_Layout.vb`, recorded in `docs/DeribitIndicatorProject.md` Section 15 under the v68 row.

---

## 0. Model + effort — READ THIS FIRST

**Model: Opus. Effort: high.**

**Why that tier.** The edit is perhaps 15 lines of PowerShell and the judgment is already done — this spec names the pass condition, the deadline and the traps. The tier is not for the typing. It is for the fact that **this exact function certified a dead collector as healthy on 2026-08-22**, and that three separate reviews of `collector.ps1` — two of them by seats that had just written warnings about this class of defect — passed code that failed on first execution. A cheap tier here re-runs that experiment.

**Where the implementer will specifically slip:**

1. **Reading `on_close` cadence as the NUD interval.** It is not. In `on_close` mode the cadence is the **execution resolution**, which is **ASIA=3 / LONDON=3 / NY=1 minutes** (`settings.json`, `session_volume.sessions[].execution_resolution`, trader-signed v36). A deploy run during ASIA needs up to ~7 minutes to produce two rows. **The existing 5-minute deadline would turn a healthy box into a false failure — and a false failure triggers a rollback, which is destructive theatre on a box that was fine.** This is the single most likely way to make things worse than they are now.
2. **Counting rows with `Get-Content $csv` unbounded.** The current code reads the entire CSV every poll — 22 MB on production, over SSM, every 20 seconds. It works today only because it uses `$l[-1]`. Counting rows after the restart tempts a full parse. Use `-Tail`.
3. **Believing the new gate proves the collector will keep running.** It does not, and the spec must not claim it does. Two rows prove the loop **fired more than once**. That is precisely enough to defeat the observed single-shot failure and no more. Write that down rather than overclaiming — overclaiming is what put us here.

**Escalation trigger:** if the gate fails on a box you can independently show is collecting (rows landing in `analysis_log.csv` while the gate reports failure), **stop**. Do not tune the tolerance until it passes. That symptom means the row-parsing or the clock comparison is wrong, and a tolerance nudge would hide it.

---

## 1. What is wrong

`Wait-DeployGate` (`tools/ops/collector.ps1:783`) passes when **one** row lands with a timestamp later than the restart:

```powershell
$rowOk = ($gv['GATE_LAST_ROW_NEWER'] -eq 'True')
```

`docs/collector-ops-tooling-proposal.md` §2.6 defines acceptance that way deliberately, and the reasoning was sound: one row exercises the launch, the session, auto-run engagement, the WS connect, the seed and the write path. **The gap is that it also passes when auto-run fires once and stops.**

**Measured, t2.micro collector, 2026-08-22.** The v68 deploy went green. The gate passed on poll 5 with `newerRow=True`. The box then wrote **no further analysis row for 175 minutes** while its WS tape kept capturing normally. Every analysis artifact — `analysis_log.csv`, `analysis_output_dump.md`, `analysis_eval_cache.csv`, `ohlc_1m_cache.csv`, `ws_health.log` — stopped at the same instant, 11:12:03–11:12:04Z.

**The gate did not merely miss the defect. It reported the opposite**, and `docs/seat-handover-2026-08-22.md` §7 recorded "Part A is proven in production conditions" on the strength of it.

⭐ **This is the project's own §5.3 lesson turned on the checker itself:** *a marker you print is not a property you checked.* The gate printed acceptance. What it had checked was one row.

---

## 2. The fix — required properties

| # | Property | Why |
|---|---|---|
| P1 | **≥ 2 rows** with timestamp later than the restart | one row is exactly what the single-shot failure produces |
| P2 | **span between first and last such row ≥ 45 s** | belt-and-braces: proves two independent fires, not a pathological double-write in one run |
| P3 | **deadline derived from the worst-case cadence, not fixed at 5 min** | ASIA/LONDON run 3-minute bars; two rows can take ~7 min |
| P4 | the poll line **reports row count and span** | an operator must see the evidence, not a verdict |
| P5 | session > 0 and settings-version checks **unchanged** | they were never the problem |

**What this does NOT prove, and the spec must say so in the code comment:** that the collector will still be running in an hour. It proves the loop fired more than once. That defeats the observed failure mode exactly, and nothing beyond it.

---

## 3. The patch

**3.1 Deadline** — `tools/ops/collector.ps1:791`

```powershell
# was: $deadline = (Get-Date).AddMinutes(5)
# 12 min, derived not guessed: worst-case cadence is the 3-minute ASIA/LONDON execution
# resolution (settings.json session_volume.sessions[].execution_resolution, v36). Two rows
# need up to 3 min to the first bar roll + ~1 min analysis + 3 min to the second, and the
# on_close feed-stall backstop is max(interval, (execRes+1)*60s) = 4 min. 12 gives headroom
# without being open-ended. The SUCCESS path is unaffected -- it returns on the first
# passing poll; only a genuine failure waits longer, and a genuine failure means STOP anyway.
$deadline = (Get-Date).AddMinutes(12)
```

**3.2 Remote row counting** — replaces the `$csv` block at `tools/ops/collector.ps1:798-807`

```powershell
"`$csv = Join-Path `$dir 'analysis_log.csv'",
"if (Test-Path `$csv) {",
"  `$restart = [datetime]::Parse('$restartIso')",
# -Tail 50, NOT a full read: the production book is 22 MB and this runs every 20 s over SSM.
# 50 rows is ~25 min of 1-min cadence -- far more than the 12-minute window can consume.
"  `$after = @()",
"  foreach (`$ln in @(Get-Content `$csv -Tail 50)) {",
"    try { `$dt = [datetime]::Parse(`$ln.Split(',')[0]); if (`$dt -gt `$restart) { `$after += `$dt } } catch { }",
"  }",
"  'GATE_ROWS_AFTER=' + `$after.Count",
"  if (`$after.Count -ge 2) { 'GATE_SPAN_SEC=' + [math]::Round((`$after[-1] - `$after[0]).TotalSeconds, 0) }",
"  else { 'GATE_SPAN_SEC=0' }",
"} else { 'GATE_ROWS_AFTER=0'; 'GATE_SPAN_SEC=0' }"
```

The header line and any malformed row throw inside `[datetime]::Parse` and are skipped by the `catch` — that is the intent, not an accident. **Do not "improve" this into a regex match on the timestamp shape**; the parse *is* the validation.

**3.3 Local evaluation** — replaces the `$rowOk` line at `tools/ops/collector.ps1:813`

```powershell
$rowsAfter = 0; [void][int]::TryParse($gv['GATE_ROWS_AFTER'], [ref]$rowsAfter)
$spanSec   = 0; [void][int]::TryParse($gv['GATE_SPAN_SEC'],   [ref]$spanSec)
# CADENCE, not existence. One row newer than the restart is exactly what a single-shot
# auto-run produces -- measured on the t2.micro 2026-08-22, where this gate passed on one
# row and the box then wrote nothing for 175 minutes. Two rows >=45 s apart prove the loop
# fired MORE THAN ONCE. It does not prove the box will still be collecting in an hour.
$rowOk = ($rowsAfter -ge 2 -and $spanSec -ge 45)
```

**3.4 Poll line** — `tools/ops/collector.ps1:814`

```powershell
Info "poll: PID=$($gv['GATE_PID']) session=$($gv['GATE_SESSION']) rowsAfterRestart=$rowsAfter spanSec=$spanSec settings=[$($gv['GATE_SETTINGS_VERSION'])]"
```

---

## 4. Blast radius — one caller you must not forget

`Invoke-Rollback` also calls `Wait-DeployGate` (`tools/ops/collector.ps1:645`, FIX 9). It inherits this change automatically, and that is **correct but worth stating**: rolling back to v66 or v67 restores a binary with no `auto_run.start_engaged`, so the app returns running-but-stopped and produces **zero** rows. The gate will fail, which is exactly FIX 9's purpose — escalate rather than report `OK`. The only change is that the escalation now takes up to 12 minutes instead of 5.

⛔ **The rollback path has still never run on a green deploy.** `docs/seat-handover-2026-08-22.md` §7 records that FIX 9 and FIX 10 are proven only from the two *failing* attempts, and FIX 10's `robocopy /MIR` restore has never executed at all. This change does not alter that, and must not be described as if it did.

---

## 5. Verification — run it, do not parse it

⭐ **Five for five: FIX 1, 6, 7, 8 and 10 were all found by executing `collector.ps1`, and none was reachable by reading it** (`docs/seat-handover-2026-08-22.md` §7). Treat "it parses" and "I traced it" as worthless here.

| # | Check | Passes when |
|---|---|---|
| V1 | Run `deploy` against the test box with the engine fix in place | gate passes, and the poll line shows `rowsAfterRestart=2` or more with a plausible `spanSec` (≈60 in NY, ≈180 in ASIA/LONDON) |
| V2 | **The negative case, and it is the one that matters.** Check out the engine *without* the `rbRepeat` fix, deploy it, and watch | gate **fails** and escalates. **If it passes, this whole change is worthless and you have learned that before shipping it, which is the point.** |
| V3 | Confirm the run was not in NY | if `spanSec` ≈ 60 every time, you have only tested the 1-minute path and trap 1 is untested. Note it plainly rather than claiming coverage |
| V4 | `status` on the box ~30 min after a passing deploy | rows still accumulating — the thing the gate deliberately does not prove |

⚠ **V2 is not optional.** A gate that has only ever been observed passing is in exactly the state this gate was in on 2026-08-22.

---

## 6. D-table — trader

| # | Decision | My read |
|---|---|---|
| D-1 | Pass condition = **≥2 rows AND span ≥45 s**, or is 2 rows alone enough? | **Keep the span check.** It costs nothing and closes a double-write nobody has ruled out |
| D-2 | Deadline **12 min fixed**, or read `execution_resolution` off the box and derive per-session? | **Fixed 12.** Deriving it means parsing the session table and the current UTC hour inside an SSM payload for a number that can only be 1 or 3 — complexity that buys one saved minute on the failure path |
| D-3 | Should the gate also assert the **tape** is advancing? | **No, and deliberately.** The tape is independent of auto-run and was never at risk (`docs/seat-handover-2026-08-22.md` §0.3b). Folding it in would let a healthy tape mask a dead book — the precise confusion this gate exists to prevent |
| D-4 | Fixture for `Wait-DeployGate`? | **No.** `collector.ps1` is outside every `.vbproj` and has no harness. Its evidence is V1–V4, run live. Saying so beats inventing coverage that does not exist |
