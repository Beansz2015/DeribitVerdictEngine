# Seat handover — 2026-09-07 (UTC)

**Read after** CLAUDE.md's session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md)** — superseded for STATE. Its collector detail and its §5 lessons still bind.

**Settings: v68**, unchanged all session. ⛔ **Run `git status -sb` — never inherit a push state from this line.** At close it was **ahead 5** (the trader pushed mid-session, so the first four of the day are already remote).

⚠⚠ **ALL DATES HERE ARE UTC. The workstation is GMT+8.** ⛔⛔ **THE CLOCK TRAP FIRED THREE TIMES IN THIS ONE SESSION, and the third time it was in a claim made TO THE TRADER: "the Kelly read is now inside its window — UTC crossed into 09-08 while we worked." It had not.** At close, local read **2026-09-08 03:32** while UTC was **2026-09-07 19:32, Monday**. **Run `date -u`. Every time. Knowing about this trap does not prevent it.**

---

## 0. ⭐ FIRST TASK — the Kelly read, and it is now dated to a TIME OF DAY, not just a day

| Pick | Item | When | Model / effort |
|---|---|---|---|
| ⭐ **1st** | **The Kelly trigger read** + the W6-4 re-run that bundles with it | ⛔ **LATE in the Tuesday 2026-09-08 UTC day. NOT at 00:01** — see §1 | Sonnet, medium |
| **2nd** | `WD-TIDY` — converge the last two inline weekday predicates | No clock. Feasibility already verified | Sonnet, low |
| **3rd** | `S-4` eval-cache backfill — key on identity, fix the loop | No clock | Sonnet, medium |

**Nothing is owed by the trader. Nothing is blocked.**

---

## 1. ⛔ THE KELLY READ — a progress sample was taken, and it sharpened the ETA

⭐⭐ **MEASURED 2026-09-07 18:49 UTC, deliberately ONE DAY EARLY and labelled as a PROGRESS SAMPLE, on the trader's call, to test whether the projected rate was real:**

| Source | weekday STRONG |
|---|---:|
| `analysis_log.csv.v0.7.bak` (closed, cross-validated) | **337** |
| `analysis_log.csv` (live) | **62** |
| **TOTAL** | **399** against **≥406 — shortfall 7** |

⛔ **THAT 399 IS NOT THE TRIGGER READ AND MUST NEVER BE QUOTED AS ONE.**

⭐ **The rate is REAL.** The 09-06 projection said *"Mon 09-07 ends ≈ 400"*; it read **399 with ~5 h of the UTC day still to run**. Live-file weekdays: 09-01 = 4 (partial, post-rotation) · 09-02 = 14 · 09-03 = 21 · 09-04 = 10 · 09-07 = 13 so far.

⭐⭐ **THE ETA IS NOW SHARPER THAN "TUESDAY": a shortfall of 7 against ~13–14/weekday crosses 406 PARTWAY THROUGH Tuesday 2026-09-08 UTC — late morning to early afternoon UTC.** ⛔ **So read LATE in that UTC day. A 00:01 read still returns short, and would burn the bundled W6-4 freeze on a below-trigger span.**

### 1.1 ⭐ How to run it — and the calibration step is not optional

**The read is one count plus a constant:** `337 + (weekday STRONG in the live analysis_log.csv)`. One SSM command against `i-0d6c133058876273e` (region `eu-west-2`), read-only, no copy-back.

⛔ **CALIBRATE THE METHOD BEFORE TRUSTING IT.** The 337 baseline was measured by another seat; counting differently makes the sum meaningless. **Run the identical query against `AWS-copybacks/aws-copyback-2026-09-06/analysis_log_aws.csv` first — it MUST return exactly 49**, with 09-02 = 14 and 09-03 = 21. Only then run it on the box.

**Two traps, both checked rather than assumed this session:**
- ⚠ **Exact-match the two verdict strings.** All ten distinct `Verdict` values were enumerated: there is **no** `NO TRADE [STRONG …]` form today, so a `/STRONG/` substring would not over-count — **but exact-match anyway**, so the count cannot silently shift if one ever appears.
- ⚠ **`ParseExact` with `InvariantCulture`**, never `[datetime]::Parse`, which reads the box locale.

A working script is at `scratchpad/kelly-progress-read.ps1` (session-local, not committed) — it mirrors `tools/ops/collector.ps1`'s process resolution and BOM-less payload plumbing.

---

## 2. The collector

Healthy at the 18:49 UTC read: **one** process, `C:\DeribitEngine\analysis_log.csv`, **5,692 rows**, span `2026-09-01 15:50:01 → 2026-09-07 18:48:01`. No redeploy this session; **settings v68 throughout.**

⚠ **STILL UNCHECKED, carried from the 09-06 handover: the 2026-09-05 DEGRADED event ran 32 minutes and nobody has verified whether it cost tape.** Verify from `trade_seq` completeness, **not** from the repair log.

---

## 3. What shipped — 17 commits, and `settings.json` is in NONE of them

| Item | Commits |
|---|---|
| **`S2-2`** — `CalcSpread` → `CalcSpreadBps` + `ClassifySpread`, proposal → ruling → build → review | `368c17a` · `f6a2c08` · `57b55f9` · `9e418e0` · `4d82e3a` |
| **`R-1`/`R-2` remediation** + its review | `de4f4aa` · `55369a6` · `eb05a4b` |
| **`R-2` residual** — `ApplySpread` extraction | `1ad7d6d` |
| **`D3-RESIDUAL`** — the strip stops printing a fabricated `0.0 bps` | `4ab0b25` |
| **`OPS-1`** — `fetch` collects the rotated `.bak` | `124e154` |
| **Item 6** — `S-1` re-checked, direction ruled | `30b0c3f` |
| **`F2` + `F3`** | `e082844` |
| **Loose-end sweep** — 11 stale rows, `G12` manuals + PDFs | `0767245` |
| **Kelly sample + weekday scoping** | `7f96a1c` |
| **Weekday filters, surfaces 2 and 3** | `817a88c` · `f0a47c7` |

**Harness 328 → 337.** `GATE PASSED` throughout. **The spread seam is CLOSED — no further follow-ups on it.**

---

## 4. ⚠ What is OPEN

- **`WD-TIDY`** — three copies of the Sat/Sun predicate remain; converge the two inline ones onto `ForwardWindowJoiner.IsWeekdayRow`. **Feasibility verified: both projects already link it.** ⛔ Keep `MatchesWeekday`'s signature (7 harness refs) and delegate only its body.
- **Item 6** — ruled **(a) as the direction, NOT NOW** by the trader. Split `TradeRecord` into `Core/TradeRecord.vb`; schedule it behind anything with a live consumer.
- **`S-4`** eval-cache · coverage-report scoping (two rows) · `ws_health.log` under-reporting · intentional-downtime scoping · atomic writes · absorption (D-2 read ~2026-09-15) · fills-import.
- **`R-2`'s other half** — `UI/MainForm_Analysis.vb` is structurally uncoverable by the harness; accepted explicitly, not an oversight.
- ⛔ **FIVE RIDERS that cannot travel alone** — the stale `.bak` rotation name, `.bak` in the pooled-concat rule, the `TriggerMode` column, the effective-source stamp (`J-E`), the `SettingsVersion` column. **They attach to the next `analysis_log.csv` header rotation, and the standing rule is NEVER FORCE ONE.**

**Next free fixture family: `A68`.** Next free hard constraint: **HC29**.

---

## 5. ⚠ The lessons — and the count is again the finding

### 5.1 ⛔⛔ ELEVEN queue rows described work that was already done

A 2026-08-01 sweep found 4 of 13. **This one found 11**, including a cell still reading *"LIVE, ongoing, and time-critical"* about a defect fixed on 2026-08-11. ⚠ **A stale row in [`trader-tick-queue.md`](trader-tick-queue.md) is worse than a stale spec header, because its own §0 designates it THE STATE READ.** **Verify every row against the tree before offering it as work.**

### 5.2 ⭐⭐ A handle whose INSTRUMENT does not survive the build is EVIDENCE, not a handle

`S2-2`'s packet led with *"if you only run one, run `H-1`"* — and `H-1` was a parity MD5 from a scratchpad **never committed**. Run honestly, reported honestly, and **no reviewer can ever execute it**. **Now a standing rule in `CLAUDE.md`, beside its two sibling handle rules.** ⭐ The replacement was already in the tree: `A65a` + `A65b` + "the render files are absent from the diff".

### 5.3 ⛔ Writing documentation against the code is an AUDIT

`G12` caught two facts that would otherwise have shipped wrong into a user manual: `SettingsLoader.Save` **drops** `changeNote` when `bumpVersion:=False`, and `HourClass` has **seven** members against `BacktestProgram.vb:20`'s claimed six. **No fixture, gate or reflection walk was ever going to catch that count.**

### 5.4 ⭐ The seam you reject teaches more than the one you take

Surface 3 rejected **two** plausible shared seams — `FailureRateMatrix.Compute` and `ForwardWindowJoiner.Load` — each on measurement (five callers; `A46a` asserts an exact row count). **Filtering inside a shared primitive silently changes its contract.** The surviving pattern is LOAD, THEN FILTER at each consumer.

### 5.5 ⚠ Traps that cost real time this session

- ⛔ **`git checkout -- <file>` to undo a mutation ALSO reverts uncommitted FEATURE work in that same file.** It silently deleted `ApplySpread`. **Restore mutations by FILE COPY when the file carries uncommitted work.**
- ⛔ **A build that FAILS leaves the harness running the STALE binary** — a run then "passes" against code that was never compiled. **Assert `Build succeeded` before believing any harness result.**
- ⛔ **`python` is NOT installed on this box.** A `python -c` mutation silently did nothing and the harness reported four PASS lines against unmutated code. Use the editor.
- ⚠ **A VB comment inside a collection initializer breaks implicit line continuation.** Put it above the block.
- ⚠ **`DateTime.MinValue.DayOfWeek` is MONDAY.** Guard it FIRST in any weekday check.
- ⚠ **`AggregateRange` silently ignores an unrecognised `EvalOutcome`** — counted in `TotalRange`, in neither numerator. `"FAILURE"` is not a real outcome; the real ones are `SUCCESS` / `ADVERSE_HIT` / `AMBIGUOUS` / `WINDOW_EXPIRED`.

### 5.6 ⭐ Two agents ran concurrently on disjoint files, and one corrected the orchestrator three times

Both were briefed to run **no git write command**; the orchestrator committed. **The sweep agent disagreed with its own briefing three times and verified right every time** — `DayOfWeek` is in seven files not five, the downtime deploy is 2026-08-13 not 08-14, and commit `1b2adbe`'s **subject** says 13 where its packet measured 17. ⭐ **That last one generalises: a commit subject can be stale against its own spec-back. Do not read counts off `git log`.**
