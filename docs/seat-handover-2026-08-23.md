# Seat handover — 2026-08-23

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This document is the STATE read.** Prior handover: [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) — **superseded for STATE, its rulings still bind.** Its §0 first task (the collector migration) is **DONE**.

**Settings: v68, pushed.** Master at `e4e298d` and pushed; the four-site fix is `30ca04d`, verified live by §0.5. Verify with `git status -sb` — never inherit a push state.

⚠ **DATES IN THIS DOCUMENT ARE UTC.** The machine is GMT+8, so the whole cutover reads 2026-08-22 in UTC and spans local midnight into 2026-08-23. **Check now-in-UTC before claiming any collection gap** — this project has made two false gap claims from that confusion, one formally withdrawn.

---

## 0. FIRST TASK — the four-site single-instance defect class in `collector.ps1`

> ✅ **UPDATE — the FIX IS WRITTEN, REVIEWED AND PUSHED (`30ca04d`).** All four sites are guarded by a shared `$ResolveSingleProcCmd`, `Stop-RemoteApp` is extracted, and `Invoke-Rollback` now stops before restoring and refuses to restore if the stop cannot be confirmed. **Proven by execution:** the guard run against real processes at 0/1/2 instances, array concatenation proven to flatten, the 11-case gate harness re-run with no regression, parse clean, and a live `-DryRun` resolving one PID and one path.
>
> ✅✅ **§0.4 IS NOW DONE TOO — RUN 2026-08-24, PASSED. THIS WHOLE SECTION IS CLOSED.** The failing deploy was executed end to end against the retired box: the gate failed, **the rollback stopped the app before restoring** (`OK app stopped (before restore)` — the line that did not exist on 08-22), **`robocopy /MIR` executed successfully for the first time ever**, and **exactly one process remained**. Evidence in §0.5.

✅ **The deploy tooling's recovery path is now proven by execution, not merely at the unit level.** §0.1–§0.3 record what the defect was; §0.5 records the verification.

**Model: Opus. Effort: high.** *(Recorded as the recommendation that was made and followed; the work is now complete.)* Not for the diff — it is a guard repeated at four call sites. It is because this file has now produced **seven** defects found only by executing it, and because the fix has to be *verified against a deliberately failing deploy*, which is the one test nobody has ever run to completion. A cheap tier will patch the loud site and leave the three silent ones.

### 0.1 What is wrong

**One assumption — "exactly one `DeribitVerdictEngine` process exists" — is unguarded at four sites.** `Get-Process` returns an *array*; every site treats it as scalar.

| Site | Line | Failure |
|---|---|---|
| `Invoke-Fetch` dir resolution | `collector.ps1:262` | `$dir = Split-Path $p.Path` yields an ARRAY → **silent** bogus remote dir on the load-bearing copy-back |
| `Invoke-Deploy` pre-flight | `collector.ps1:431` | same, plus `REMOTE_PID` / `REMOTE_SESSION` render as `"2484 2920"` / `"2 2"` |
| `Start-RemoteApp` | `collector.ps1:782` | reports `LAUNCH_SESSION=2 2` and calls it a success |
| `Wait-DeployGate` | `collector.ps1:824-825` | `[int]"2 2"` **throws every poll**; `$sessionOk` never assigns, so the gate can never pass |

⭐ **Only the gate site is loud. The other three fail silently by producing a wrong string** — which is worse, and is why a fix that only chases the stack trace is a bad fix.

### 0.2 How the box got two processes — the actual root cause

**`Invoke-Rollback` restores WITHOUT stopping the app first.** Measured live 2026-08-22 (see §3.2):

```
FAIL RESTORE ITSELF DID NOT CONFIRM ... The process cannot access the file
'C:\DeribitEngine\DeribitVerdictEngine.exe' because it is being used by another process.
```

Deploy step 8 restarts the app; the gate then fails; `Invoke-Rollback` tries to overwrite the binary it just relaunched. The restore aborts on the lock, `Start-RemoteApp` runs anyway, and the box ends with **two** collectors writing the same book and tape.

⚠ **Why three prior reviews missed it:** attempts 1–2 (recorded in [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §0.3/§0.3b) rolled back from a **pre-restart** failure — a hash mismatch, when the app was still stopped from step 4. The gate-timeout path is the **post-restart** rollback, and it is the only one that leaves the app running. **The two paths differ in exactly the property that matters, and only one had ever been exercised.**

✅ **CLOSED BY §0.5 (2026-08-24).** ~~FIX 10's `robocopy /MIR` restore STILL has never successfully executed. It aborted on the lock before doing any work.~~ It has now executed successfully, and the restored binaries hash-match the 2026-08-22 archive. [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §7's gap is finally closed rather than moved.

### 0.3 The fix, in order

1. **Extract the stop-and-confirm block** from `Invoke-Deploy` step 4 (`collector.ps1:477-485`) into a `Stop-RemoteApp` function, and **call it from `Invoke-Rollback` before `Restore-DeployBackup`.** That is the root-cause fix; the rest are defence in depth.
2. **`Start-RemoteApp` must refuse to launch when an instance already exists.** Its callers reach it only after a confirmed stop, so a live process means an earlier step failed — launching a second is never the right answer.
3. **Guard all four sites on COUNT.** Report the process count explicitly, and treat `count <> 1` as a *named* failure ("2 instances running") rather than casting an array to `[int]` or concatenating it into a path.
4. **Sweep, do not spot-fix.** `grep -n "Get-Process DeribitVerdictEngine" tools/ops/collector.ps1` returns six hits across four functions. This project has burned this exact lesson before — FIX 2 / FIX 7 / FIX 8 were three instances of one class, and the sweep that followed found a fourth.

### 0.4 ⚠ How to verify it, and why the obvious way is wrong

**Testing a rollback fix requires a deploy that FAILS.** Do not do that against the new production box.

⭐ **Use the retired old box as the test target.** It is now ideal, and for a non-obvious reason: **it runs v66, which has no `auto_run.start_engaged`, so it comes up running-but-idle and will NEITHER collect NOR fork the book.** Its data is archived and snapshotted, so it is fully disposable.

1. `aws ec2 start-instances --region eu-west-2 --instance-ids i-08c740e22d507667d`
2. Deploy a knowingly-bad build to it (the V2 recipe in §3.3 below) → the gate must fail → **watch the rollback actually stop the app, restore, and relaunch ONE instance.**
3. Confirm exactly one process afterwards, and that `robocopy /MIR` genuinely ran.
4. Stop the instance again.

✅ **The step-8 watch is COMPLETE (§1.4), so the deploy embargo that deferred this has lifted.** The fix itself is written, reviewed and pushed; only this verification is owed.

### 0.5 ✅ §0.4 EXECUTED 2026-08-24 — PASSED, and the recovery path is finally proven

**Run against the retired box `i-08c740e22d507667d`, negative build `B8BE290F…` (the `rbRepeat` fix backed out on a throwaway branch, since deleted). Exit code 2, which is the PASS condition — a green deploy would have invalidated the test.**

```
=== 4. stop the app ===
OK    app stopped (pre-deploy)
=== 9. acceptance gate ===
FAIL  gate did not pass within 12 minutes
OK    app stopped (before restore)          <-- THE FIX
=== restore from _deploy_backup ===
OK    restored from backup -- all six items verified present
OK    relaunched (LAUNCH_SESSION=2)          <-- single value, not "2 2"
```

**Compare to the same code path on 2026-08-22:** `FAIL RESTORE ITSELF DID NOT CONFIRM … being used by another process`, then `LAUNCH_SESSION=2 2` and 28 consecutive cast exceptions.

| Claim | Evidence |
|---|---|
| Rollback stops before restoring | `OK app stopped (before restore)` — **no file lock** |
| `robocopy /MIR` executes | `OK restored from backup -- all six items verified present` — **first ever successful execution** |
| Exactly one process | **`procs=1` on every poll of BOTH gates**; zero cast errors, zero `2 2`, zero `procs=2` |

⭐ **An independent check that was not planned:** the restored `exe` hashes to `C934DD7D38B2DD15…` and the `dll` to `0FF3553647A2180A…`, and **both match the `_manual_backup` entries in the 28-file archive manifest taken 2026-08-22.** The restore was byte-correct, not merely "six items present."

**Box afterwards:** one process, settings back to **v66**, `fonts` flat (no FIX 10 nesting), backup intact at 6 items. Instance stopped again.

⚠ **ONE HONEST NUANCE — this run did NOT reproduce the one-row signature.** Its gate failed at `rowsAfterRestart=0`, not `1`; the box had been down ~41 h, so gap repair probably consumed the window. **§0.4 proves the ROLLBACK path; V2 on 2026-08-22 proves the ONE-ROW discrimination (28 polls stuck at `rows=1`).** Together they cover both. **Neither run covers both on its own** — do not cite this one as evidence the gate discriminates a single-shot start.

⚠ **A prerequisite worth recording for anyone repeating this:** a rebooted box has NO logged-on session, and the §2.1 launch mechanism needs one. `schtasks /run` returns **SUCCESS** and produces **zero processes** — measured. Someone must RDP in once and DISCONNECT (not sign out) before the app can be launched. `Start-RemoteApp` asserts the process count afterwards, so it catches the false success rather than reporting it.

---

## 1. The collector migration — DONE

**The t2.micro `i-0d6c133058876273e` IS production.** The old t3.small `i-08c740e22d507667d` is stopped and retired.

### 1.1 What was executed, with evidence

Procedure: [`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §5.4, in order, not reordered.

| Step | Evidence |
|---|---|
| 1 — fetch + verify | Ran twice. Final pre-stop fetch at **15:36:26Z**, cutting the delta to 67 s |
| 2 — stop old production | **15:37:33Z**, asserted `POST_COUNT=0` |
| 3 — capture the final delta | 6/6 items **hash-for-hash**; delta was exactly 1 book row, 1 eval-cache row, 3,796 B of tape |
| 4 — stop new box, delete its book + store | Host-guarded, delete asserted item by item |
| 5 — copy production's book + store across | Hash-verified **on the box** against production-computed SHA256s |
| 6 — verify the copy | Met and exceeded — hashes, not just sizes |
| 7 — start, confirm append + cadence | **Append proven**, gate passed, tape healed |
| **8 — watch a full day** | ✅ **PASSED** — see §1.4 |

**Carried state:** book 24,721 rows (first row `2026-07-22 16:24:54`), eval cache 44,910 rows, store **65,088,339 B across 2 files**, both sidecars.

### 1.2 ⭐ The tape gap healed completely — measured, not assumed

Production stopped 15:37:33Z; the new box started 16:02:54Z. **A 25-minute hole.** Gap repair fired once on start (`gap_repair_lookback_hours` is 20) and the result was:

```
WINDOW_ROWS=1586   15:25:01Z -> 16:04:48Z
MAX_TIME_GAP_SEC=69.1 at 15:38:38Z
SEQ_MIN=296958647 SEQ_MAX=296960232 DISTINCT=1586 SPAN=1586 MISSING=0
```

**Zero trades missing across the cutover.**

⚠ **The byte count nearly produced a false alarm and `trade_seq` settled it.** The store grew only ~54 KB where the morning's measured rate (~168 B/s) predicted ~270 KB. The market was simply quieter — 1,586 trades over ~40 min is ~0.66/s. **A volume- or time-based detector gives false comfort or false alarm here; `trade_seq` gives the answer.** That is D-2's whole point, now demonstrated twice on live data.

### 1.4 ✅ Step 8 — the full-day watch PASSED

Measured 2026-08-23 14:31Z, **22 h 27 m** after the new box went live at 2026-08-22 16:02:54Z. The remaining 92 minutes could not have added information — nothing was trending toward a threshold.

| Check | Result |
|---|---|
| Rows | 24,721 -> **25,550** = +829, i.e. **36.9/hour** (old production historical: 33.3) |
| Cadence | gap now-lastrow **0.3 min** |
| Stability | **no restart** — app uptime 22 h 27 m |
| `ws_health` | **zero DEGRADED, zero DOWN** since the 16:02 startup |
| History | first row `2026-07-22 16:24:54` intact — it is appending, not a fresh book |
| CPU credits | **144.0 pinned** across 20 h, one dip to 143.58 |
| Memory | ~150–160 MB baseline available; **`pagesOUT/s` 0 across 15 consecutive samples** |
| Eval cache | 3.83 -> 4.1 MB in 22.5 h = **0.29 MB/day**, matching the ~0.33 MB/day predicted in [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §4 |

⚠⚠ **ONE READING NEARLY PRODUCED A FALSE ALARM, AND IT IS THE LESSON OF THIS SECTION.** An `ssm-mem.json` read at 14:31Z showed `app PVT 90 MB` and **`pagesOUT/s avg 726.8 / max 5,108` sustained over 30 s** — against `0.0` on the same probe pre-cutover. That looked like a genuine regression and I had the alarm half-written. **A third reading four minutes later showed `71.1 MB` and `pagesOUT/s 0 across 15/15 samples.** App private across three readings ran **74.3 -> 90 -> 71.1 MB — not monotonic.** The 90 was a transient of undetermined cause; I did not invent one. ⭐ **[`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §5.1 records the prior seat making this exact call three times in one day. Two points is not a trend, and one point is not either.**

⭐ **The one genuinely monotonic series is Windows Defender:** `MsMpEng` 298.8 -> 305.5 -> **322.8 MB** across three readings ~21 h apart — now **31 % of the box RAM** and larger than the collector, `dwm`, `explorer` and `SearchUI` combined. **If memory ever needs relief, that is the target**, but it is a security-posture decision on a box holding trading data.

### 1.3 ⚠ A real ordering defect in the procedure document

[`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §5.4 step 3 says *"re-`fetch` to capture the final rows written between step 1 and step 2."* **`fetch` cannot run with the app stopped** — `Invoke-Fetch` resolves the install dir from `Get-Process` and exits 1 (`collector.ps1:261`). **Step 2 makes step 3 impossible as written.**

**Worked around, not fixed:** a fresh `fetch` was taken immediately *before* the stop, and the post-stop delta was captured by a hand-run SSM snapshot. With the app stopped the files are static, so that capture is exact and race-free — safer than the running case FIX 7 exists to handle. **If §5.4 is ever run again, either patch `fetch` to accept an `-InstallDir` override or write the hand-run step into the document.**

---

## 2. ⛔ The v68 defect — the thing that would have ruined the migration

**v68's auto-run-on-start collected EXACTLY ONE analysis row per process start, then went silent for the life of the process.** Measured on the test box: one row at 11:12:03Z, then **175 minutes** of nothing while the WS tape kept capturing normally.

**Mechanism.** `InitAutoRunControls` sets `rbSingle.Checked = True` unconditionally (`UI/MainForm_AutoRun.vb:22`), and **SINGLE poisons every branch of `StartAutoRun`'s mode selection, not only the interval one.** In `on_close` mode the bar-close watcher starts normally and `RunAutoAnalysis` then disposes it on its own first fire (`UI/MainForm_AutoRun.vb:136`) — which also removes the feed-stall backstop, because the backstop lives inside the tick that no longer runs. Only the final `Else` branch repeats, and it needs `rbSingle` unchecked.

⭐ **Why nobody saw it:** a human clicking Start picks REPEAT by hand. **Auto-start had no such step, so the defect existed only on the capability v68 added.**

**Fix:** `rbRepeat.Checked = True` immediately before the `StartAutoRun` call (`UI/MainForm_Layout.vb:445`). Commit `9e9fe33`. No settings key, no version bump. Recorded inside the existing v68 row of [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) Section 15 — **a defect in that item, not a new version.**

⚠⚠ **A first diagnosis was WRONG and is worth knowing.** I attributed it to `StartAutoRun` racing the WS feed construction. It does not: `InitMarketDataSources()` runs at `UI/MainForm_Layout.vb:409`, **before** `StartAutoRun()` at 445, so the feed exists and on-close activates normally. **The wrong diagnosis produced a plausible fix that would have changed nothing.** Reading the call site beat reading the file top-to-bottom.

---

## 3. The acceptance gate was blind to it — fixed and PROVEN

### 3.1 The gap

[`collector-ops-tooling-proposal.md`](collector-ops-tooling-proposal.md) §2.6 defined acceptance as **one** new CSV row within 5 minutes. **A single-shot start satisfies that exactly.** The gate passed the v68 deploy and [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §7 recorded *"Part A is proven in production conditions"* on the strength of it. **The gate did not merely miss the defect — it reported the opposite.**

**Now:** ≥2 rows, ≥45 s apart, 12-minute deadline. Spec: [`deploy-acceptance-gate-cadence-spec.md`](deploy-acceptance-gate-cadence-spec.md). Packet: [`deploy-acceptance-gate-cadence-spec-back.md`](deploy-acceptance-gate-cadence-spec-back.md). Commit `9eb3329`.

⚠ **The deadline is the load-bearing half.** ASIA and LONDON run 3-minute `execution_resolution`, so two rows can legitimately take ~7 minutes. **Raising the row count while keeping 5 minutes would false-fail healthy boxes and trigger a rollback on a box that was fine.**

### 3.2 V2 — the negative case, PASSED

Deployed a build **without** the fix. 28 polls across the full 12 minutes, every one `rowsAfterRestart=1 spanSec=0`. **Gate correctly failed, exit 2.**

### 3.3 V1 — the positive case, PASSED

```
poll: rowsAfterRestart=0 spanSec=0
poll: rowsAfterRestart=0 spanSec=0
poll: rowsAfterRestart=1 spanSec=0     <-- the OLD gate would have PASSED here
poll: rowsAfterRestart=1 spanSec=0
poll: rowsAfterRestart=1 spanSec=0
poll: rowsAfterRestart=2 spanSec=61    <-- the NEW gate passes here
```

⭐⭐ **A controlled experiment: same box, same tooling, same gate, one line of engine code as the only variable. The old one-row gate would have passed BOTH.**

**To reproduce the negative build:** on a throwaway branch, `git checkout 9e9fe33~1 -- UI/MainForm_Layout.vb`, commit, push (pre-flight rejects a dirty or unpushed tree), `dotnet build -c Release`. Delete the branch afterwards.

---

## 4. State — verified, not inherited

| Thing | State |
|---|---|
| **New production** `i-0d6c133058876273e` | t2.micro, Server 2019, `C:\DeribitEngine`, **v68 + the rbRepeat fix** (exe built 2026-08-22 15:27Z), collecting at ~0.6 min gap |
| **Old production** `i-08c740e22d507667d` | **`stopped`** 16:43:34Z. Volume `vol-03c79bb3716c94809` attached and intact |
| **Termination protection** | ✅ **`DisableApiTermination = True`** — set because `DeleteOnTermination` is `true`, so one mistaken terminate would have destroyed the volume with no confirmation step |
| **Snapshot** | ✅ **`snap-0a17195c58e850cbd`**, `completed` 100%, 30 GB, standard tier |
| **Local archive** | `C:\Dev\collector-cutover-2026-08-22\production-full-archive\` — **all 28 files, hash-verified**, 112,328,075 B |
| **Local cutover copies** | same parent dir: the 14:03 fetch, the 15:36 pre-stop fetch, and the hash-verified `final\` set |

⭐ **Four independent layers stand behind the irreplaceable tape:** the running new box, the stopped old volume, the snapshot, and three verified local copies.

**Snapshot tier:** standard, deliberately. Archive tier is ~4× cheaper per GB but carries a 90-day minimum and a **24–72 h restore**, useless as a backstop during a watch period. **`modify-snapshot-tier` after step 8 passes** if the saving is wanted.

⚠ **The archive holds things the migration deliberately did NOT carry** — `analysis_output_dump.md` (16.8 MB), production's **v66 `settings.json`**, the July `analysis_report_*` / `analysis_summary_*` pairs, `ohlc_1m_cache.csv`, `tools\ws_trade_probe_20260811-144742.csv` from the `trade_seq` investigation, and `_manual_backup\`. None irreplaceable; all preserved.

---

## 5. Lessons, each earned by an error made this session

### 5.1 ⚠ A background task's reported exit code is the LAST command's, not the one that matters

Bit twice in one session. The V2 deploy notification said **"exit code 0"** while the output held `V1_DEPLOY_EXIT=2`. The snapshot waiter notification said **"exit code 0"** while the output held `WAIT_EXIT=255 — Max attempts exceeded`. **Both times the real answer was inside the output.** A trailing `echo` swallows the status of everything before it. **Read the output, never the summary line.**

### 5.2 ⛔ A checker that reports success it never performed — THREE instances, all mine, all in one session

| Checker | Claimed | Actually |
|---|---|---|
| The v68 acceptance gate | box healthy | one row, then 175 min of silence |
| My gate-review harness | `ALL CASES PASSED` | **9 of 10 cases never ran** — `R` is the built-in alias for `Invoke-History` and outranks a function |
| My archive verifier | `ARCHIVE VERIFIED — all 0 files` | a path substitution failed; the loop ran zero times |

⭐ **The fix that works is the same every time: assert the check RAN.** Both harnesses now throw when the item count is zero or short of expected. **"It didn't complain" is not "it passed."**

### 5.3 A ready explanation for a surprising number is a warning sign

The tape grew 54 KB where I predicted 270 KB. The tidy explanation would have been "gap repair failed." **Measuring `trade_seq` instead returned `MISSING=0`** — the market was quiet. [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §5.1 records the same trap catching the prior seat four times in one day.

### 5.4 Execution finds what parsing structurally cannot — now SEVEN for seven

FIX 1, 6, 7, 8, 10, plus this session's rollback-lock defect and the two-instance defect. **Every one was found by running `collector.ps1`. None was reachable by reading it, and several survived multiple careful reviews.** Do not accept "it parses" or "I traced it" for anything in that file.

---

## 6. What is NOT done

- ✅ **Step 8 — DONE and PASSED**, 22 h 27 m, see §1.4. That also discharges V4 of [`deploy-acceptance-gate-cadence-spec.md`](deploy-acceptance-gate-cadence-spec.md) §5.
- ✅ **§0.4 — DONE and PASSED 2026-08-24.** The rollback path has now completed successfully end to end, `robocopy /MIR` has executed for the first time, and the restored binaries hash-match the archive. Evidence in §0.5.
- ✅ **Nothing on the deploy tooling is outstanding.** Both remaining items below are LATENT and need trader rulings, not builds.
- ⚠ **The book's `Timestamp` column is LOCAL time, not UTC** — §7 below. Dormant while every host is UTC; **a decision, not a cleanup**; and the CLI port is what makes it live.
- ⚠⚠ **`_evalCache` — still no spec, and it now has a CLOCK.** Confirmed UNBOUNDED from source (`LivePerformanceTracker.vb:118`; no `Remove`/`Clear`/`Take` of any kind). **The code did not change — the box did.** [`seat-handover-2026-08-22.md`](seat-handover-2026-08-22.md) §4's "years of headroom" was measured against ~496 MB free on a 2 GB t3.small; the collector now runs a 1 GB t2.micro with ~150 MB free. File growth **measured at 0.29 MB/day**; the in-memory rate is **not** measured, so the runway is **roughly 8–17 months** on a 1×–2× multiplier, not "years". ⛔ **The §4 landmine is unchanged and now dated: `WriteEvalCache` rewrites the whole file with `append:=False` from five call sites, so trimming the list TRUNCATES `analysis_eval_cache.csv`. Decoupling the list from the file is the work, and it comes first.** **Do not "just trim it."**
- **Other open trader decisions, untouched:** the absorption D-table ([`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 — attack `pullFrac` first) and the CLI-port reversal not yet written into [`roadmap.md`](roadmap.md).
- **Cosmetic:** `tools/checks/verify-gate.ps1:144` prints *"no engine-path change"* on commits that change engine behaviour. Its engine prefixes are `Core/`, `DynamicNorms.vb`, `analysis/` — `UI/` is excluded, correctly, because the check is a *settings-version-bump* nudge and a new key must touch `Core/Settings/EngineSettings.vb`. **The logic is sound; only the message overclaims.** Reword if that file is ever touched.

---

## 7. ⚠ LATENT DEFECT — the book's `Timestamp` column is LOCAL time, not UTC

**Found 2026-08-23**, not by a test, but because the hostel app's seat asked whether co-locating on the collector box carried a timezone risk. **Their app's bug was elsewhere; the question found ours.**

### 7.1 What it is

```
UI/MainForm_Analysis.vb:613     verdict.Timestamp = DateTime.Now
```

`verdict.Timestamp` becomes the **`Timestamp` column of `analysis_log.csv`** — the first field of every row in a book running back to 2026-07-22.

**It is `DateTime.Now`. Local time.** It has only ever looked correct because **every collector host this project has ever run has been UTC** — both the retired t3.small and the current t2.micro (`TZ_ID=UTC`, verified 2026-08-22). On a UTC host `DateTime.Now` and `DateTime.UtcNow` carry the same clock value, so the defect is invisible.

### 7.2 ⚠ It is also latently self-inconsistent, today

The engine does **not** use local time everywhere. Session bucketing resolves on UTC:

```
ExecutionResolution.ResolveResolution(cfg, DateTime.UtcNow.Hour)
```

**So a row's stamped hour and the session it was scored under come from two different clocks.** They agree only because the host is UTC. On any other host they would diverge by the offset, and a later reader inferring the session from the CSV timestamp would get the wrong session — silently, with no error anywhere.

### 7.3 Where it becomes real

- ⛔ **Changing the box timezone** would shift every new row by the offset, against a month of existing rows. That desynchronises the eval cache (keyed to analysis rows), the Kelly dated trigger, and any pooled date-range read across the boundary. **This is the concrete reason the collector host must stay UTC** — a harder reason than the one recorded during the hostel co-location exchange, which was about an email header.
- ⛔ **The Linux CLI port** (`DeribitIndicatorProject.md` Section 16.2). A headless Linux host's local time is not guaranteed to be UTC, and on Linux .NET *does* honour the `TZ` environment variable. **The port would make this live.**

### 7.4 ⚠ The fix is NOT a trivial swap — it needs a ruling

⭐ **On a UTC host, `DateTime.Now` → `DateTime.UtcNow` is byte-identical in the rendered CSV**, because the clock value is the same and the write formats to `yyyy-MM-dd HH:mm:ss`. **That makes it cheap to do NOW and expensive to do after a non-UTC host exists** — once one does, the change becomes a visible discontinuity rather than a no-op.

**But it is a decision, not a cleanup, for two reasons:**

1. **`DateTime.Kind` changes** from `Local` to `Utc`. Any consumer that converts, compares against a `Local` value, or round-trips through a typed parse would see a difference the rendered string does not show. **`LivePerformanceTracker`, the eval cache walk, and `AnalysisLogger` all need checking before this moves** — the rendered-output equivalence is not by itself sufficient evidence.
2. **It changes what is written to the book**, which is a data-model decision and the trader's to make, not an implementer's.

**The other five `DateTime.Now` sites are NOT affected** and should not be swept up in a fix: `MainForm_Analysis.vb:713` and `MainForm_Render_Cards.vb:503` are elapsed-time calculations that are self-consistent by construction; `MainForm_PlaintextSnapshot.vb:80` and `MainForm_Render_Cards.vb:1043` are display-only; `AnalysisOutputDump.vb:79` explicitly computes `TimeZoneInfo.Local.GetUtcOffset(...)` and is deliberately timezone-aware. **Only line 613 reaches persisted data.**

### 7.5 Standing constraint this establishes

⛔ **Collector hosts run UTC.** Until line 613 is ruled on, that is a hard constraint rather than a convention, and it should be checked on any new box before it collects a single row. `TZ_ID` is one line of a status probe.

⚠ **Note for a Windows host specifically:** the `TZ` environment variable is **inert** on Windows .NET — verified 2026-08-23 by setting `TZ=America/New_York` on a Singapore-zoned machine and observing `TimeZoneInfo.Local` unchanged. On Windows the timezone comes from the registry. **Do not attempt to pin a host's timezone with `TZ`; it will silently do nothing.** On Linux the same variable *is* honoured, which is precisely why the port makes this defect live.

### 7.6 ⭐ The checklist item this produced — a taxonomy, RANKED, not a blanket ban

**Two local-time defects were found in two independent codebases in one exchange, neither by a test, both invisible because the host timezone happened to match the assumption.** That is a category, not a coincidence. The hostel app's seat proposed carrying it into the migration checklist as a class to grep for; this is the form it settled into.

**The grep:**

```
DateTime.Now · DateTime.Today · DateTimeOffset.Now · Date.Now
```

⚠ **Include `DateTimeOffset.Now`.** My first sweep omitted it and I only checked after the other seat swept for it in their code. We have none — but that was luck confirmed late, not diligence.

**Then classify every hit. The categories are NOT equal, and the order is the point:**

| # | Category | Severity | Action |
|---|---|---|---|
| **1** | **PERSISTS a value** — written to a file, a store, a cache that outlives the process | ⛔ **corrupts a dataset** | **grep this first; treat every hit as a defect until proven otherwise** |
| **2** | **TRANSMITS a value** — an email, an API payload, a signal to another process | ⚠ **confuses a person** | real, but recoverable |
| 3 | **Elapsed-time arithmetic** — `now - storedNow`, cache TTLs | ✅ legitimate | same clock at both ends, self-consistent by construction |
| 4 | **Display only** | ✅ legitimate | renders in host-local time, which is usually what a human wants |
| 5 | String literals — `"Today"` as a label | ✅ false positive | ignore |

⭐⭐ **The asymmetry between 1 and 2 is the useful part, and it is evidenced rather than asserted.** Both codebases were swept independently against this taxonomy:

| | This repo | The hostel app |
|---|---:|---:|
| Persists | **1 defect** — `MainForm_Analysis.vb:613` | 0 |
| Transmits | 0 — the signal bridge already uses `DateTime.UtcNow` | **2 defects** |
| Elapsed / display / literals | 5, all legitimate | 11, all legitimate |
| **Total calls** | **6** | **13** |

**The taxonomy ranked severity correctly in code neither author wrote.** Ours is the persisted one and is the worse defect; theirs are transmitted and are the milder kind; and our transmit path was already clean without anyone having planned it that way.

⛔ **Why a blanket ban on `DateTime.Now` is the wrong rule:** it flags all 19 calls across both codebases and buries the 3 that matter. **5 of our 6 sites are correct as written** and a sweep that "fixed" them would be pure churn — `AnalysisOutputDump.vb:79` in particular *deliberately* computes `TimeZoneInfo.Local.GetUtcOffset(...)` and must not be touched.

**Apply this at:** any host migration, any new collector box, and — most importantly — **the Linux CLI port**, where `TZ` becomes live and the assumption that has protected us stops holding.
