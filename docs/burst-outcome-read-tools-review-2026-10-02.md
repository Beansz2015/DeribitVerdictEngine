# Burst outcome read — tools build (session 1): second review (2026-10-02 UTC)

**Reviewed:** the tools build for [`docs/burst-outcome-read-spec.md`](burst-outcome-read-spec.md) (the burst outcome read spec). Packet: [`docs/burst-outcome-read-tools-spec-back.md`](burst-outcome-read-tools-spec-back.md). Tools commit `8943324`, built from `b5b4a7d`.
**Reviewed at:** `HEAD` = `9d764b2`. Three later commits changed `tools/ops/burst_outcome_read.py` after the build, all before any outcome was seen: `15a0234` (the first review: covered-strata readability), `98ed7cc` (breakevens in the Σ forms), `4de2a3b` (the daylight-saving seam stratum). Run 1 will use `HEAD`'s script, so the code review is of `HEAD`. The packet's handles were re-run at `HEAD`, and `H-3` also at `8943324`.
**Seat:** Opus 5.5. Effort: **high**. The packet's escalation trigger did not fire (`H-2` reproduced; I agree with `TB-D5` and `TB-D6`). I moved up because finding `RV-1` below can silently corrupt run 1.

⛔ **No real outcome was printed, aggregated, sorted, plotted or read.** Real-data runs: the power count, the counts-only mode, `diagexport` (console lines `Population rows` and `Split date` only, plus file MD5s), and the `H-1` default-mode diff (diff only). Every outcome-path run used synthetic data (`--selftest`, mutants, a unit check).

`RV-n` = "review finding n", new IDs for this document.

> ⚠ **Orchestrator note (2026-10-02): this second review was not needed as a gate.** The build was already ACCEPTED on 2026-09-29 in `15a0234`, and `TB-D5` was already ruled (b) there ([`burst-outcome-read-tools-decisions-for-orchestrator.md`](burst-outcome-read-tools-decisions-for-orchestrator.md)). The queue row still read "review owed", and the dispatching seat did not check the tree first. **This review's reads on `TB-D5` and `TB-D6` agree with those rulings, so nothing changes there.** Its new findings `RV-1`–`RV-4` stand on their own and are not in the first review.

---

## 1. Verdict: ACCEPT WITH FIXES

- **The build does what the packet says.** Every re-run handle reproduces. The arm tables match the power count line for line (9,091 rows, 0 drops). The `diagexport` output is byte-identical to the build seat's export.
- **The statistics are correct as far as I can test them.** The copied block matches `tools/ops/medium_tier_diagnosis.py` except the three marked lines. A deterministic unit check shows that Holm and the `TB-D5` label (the "NOT CONFIRMED" label) behave as specified.
- **Fix before run 1: `RV-1`.** The candle cache named in the script's run recipe holds a partial week. Reused, it gives every later row of that week no bars, so the row scores −fee. Nothing reports or stops this.
- **Fix before run 1: `RV-2` and `RV-3`.** Full mode opens outcomes even when its reconciliation checks fail, and its window, seed and settings version are not pinned. The selftest cannot fail on Holm, on the `TB-D5` label, on the `BO-H2` arm set or on the seam filter (5 surviving mutants).
- **Before run 2: `RV-4`.** The script has no session filter, so a per-session run 2 prints the other sessions' outcomes early. It also has no notion of the daylight-saving-aware session hours (audit row `F5`).

---

## 2. Handles

`H-n` = a reader can run it. `E-n` = build-time evidence, not re-runnable as written. All from the repo root. My scratch files are in my session scratchpad (shown as `<S>`), never in the repo.

| Rank | Handle | What it shows | Result |
|---|---|---|---|
| 1 | `H-2` | Export regenerated; power count section 1 = counts-only tables 2a and 2b | ✅ Identical, 0 drops |
| 2 | `H-7` (new) | Deterministic unit check of `holm()` and `finalize_label()` | ✅ Holm step-down and `TB-D5` label correct |
| 3 | `E-2` (new) | Mutation runs of `--selftest` | ⚠ 1 control red, **5 mutants survive** (`RV-3`) |
| 4 | `H-3` | `--selftest` at `HEAD` (3 cases) and at `8943324` (2 cases) | ✅ Both pass |
| 5 | `H-1` | Default mode, old binary vs new binary, no-rotated-book fetch | ✅ Only `Run at` differs |
| 6 | `H-1b` (new) | `diagexport`, old vs new binary, same fetch | ✅ CSVs byte-identical (MD5) |
| 7 | `H-6` | Counts-only split at 2026-09-01 | ✅ Adds to 9,091 and 11,001 |
| 8 | `H-4` | Project rebuild; `verify-gate.ps1` | ✅ 0/0; `GATE PASSED` |
| 9 | `H-5` | `git diff --stat b5b4a7d 8943324 -- tools` | ✅ Matches; ⚠ docs commit holds 3 files, not 2 |

### `H-2` — export, power count, counts-only

Export regenerated into `<S>` with a copy of the candle cache (the packet's command, `--cache` and `--out` pointed at `<S>`). Console lines (actual):

```
- Rotated box logs (in the box-log set and the merge): C:\Dev\DeribitVerdictEngine\aws_fetch\20260928-121255\analysis_log.csv.116col-83564b1b.20260924_184606.bak
- Population rows: 11001. Rows without a logged row (must be 0): 0.
- Split date 2026-08-18 00:00 UTC: H1 = 30 trading days, H2 = 30 (the stability read's rule).
```

```
3fc4f716012481cb7132e9583cde5d18 *<S>/diagnosis-rows-20260928.csv
3fc4f716012481cb7132e9583cde5d18 *backtest_data/burst-outcome-read/diagnosis-rows-20260928.csv
```

Power count (`-FetchFolder aws_fetch\20260928-121255 -RunDate 2026-11-02`), `\r` stripped, diffed against `docs/burst-outcome-read-spec.md` §2.2 lines 54–177: `diff-whole-exit=0` (the whole block, not only section 1).

Counts-only (the packet's command, export from `<S>`, `--out` to `<S>`). Comparison by a scratch script that cuts each 19-line table (actual):

```
lines: power 19 2a 19 2b 19 2c 19
2a == fresh power count section 1: True
2b == fresh power count section 1: True
2c rows with any non-zero count: 0
```

Population lines (actual):

```
- Export rows inside the window: 11001. Excluded, by reason:
  - ASIA before arming 2026-08-01 19:02:31: 329
  - AggrVelBurstRatio empty: 54
  - not in the fetch books (pre-collector or pooled-only row): 1527
- Analysis population (directional, valid levels, armed, ratio present, in the fetch books): 9091 rows, first 2026-07-22 16:31:01, last 2026-09-28 12:12:01.
- Checks: export Session differs from the hour's session: 0. Export AggrVelSignal differs from the fetch book: 0. Band mismatch on A rows: 0.
- Power-count reference population from the same books (tools/ops/burst-outcome-power-count.ps1 rule): weekday session rows=41933 population=41725; excluded NO TRADE=26332 lean forms=5973 ASIA before arming=329 invalid placed levels=0; directional valid armed rows=9091.
- Power-count rows NOT in the analysis population: 0, by reason:
- Analysis rows NOT in the power-count population (must be 0): 0. Rows in both with a different arm, shadow arm or fifth (must be 0): 0.
COUNTS ONLY: exiting before any outcome column is opened.
```

### `H-7` — Holm and the `TB-D5` label, deterministic

Synthetic draws, evenly spaced, so each p is known exactly. Save as a scratch file and run with `python`:

```python
import sys; sys.path.insert(0, r"C:/Dev/DeribitVerdictEngine/tools/ops")
import burst_outcome_read as b
def draws(center, n=10000, spread=10.0):
    return sorted(center - spread + 2*spread*i/(n-1) for i in range(n))
def res(pt): return {"FULL": (pt, 0, 0, [200, 200])}
mk = lambda c, lab: {"draws": draws(c), "label": lab, "res": res(c)}
R = {"ASIA": mk(9.9, "CONFIRMED (d > 0)"), "LONDON": mk(9.7, "CONFIRMED (d > 0)"), "NY": mk(9.6, "CONFIRMED (d > 0)")}
b.holm(R, 3)
for s, x in R.items():
    print(s, "p=%.4f alpha=%.4f hci=[%+.2f,%+.2f] reject=%s final=%s" % (x["p"], x["alpha"], x["hci"][0], x["hci"][1], x["reject"], b.finalize_label(x, None, None)))
R2 = {"ASIA": mk(9.9, "CONFIRMED (d > 0)"), "LONDON": mk(9.0, "NOT READABLE"), "NY": mk(9.9, "CONFIRMED (d > 0)")}
b.holm(R2, 3)
for s, x in R2.items():
    print("R2", s, "p=%.4f alpha=%.4f reject=%s final=%s" % (x["p"], x["alpha"], x["reject"], b.finalize_label(x, None, None)))
```

Actual output:

```
ASIA p=0.0100 alpha=0.0167 hci=[+0.07,+19.73] reject=True final=CONFIRMED (d > 0)
LONDON p=0.0300 alpha=0.0250 hci=[-0.05,+19.45] reject=False final=NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0)
NY p=0.0400 alpha=0.0500 hci=[+0.10,+19.10] reject=False final=NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0)
R2 ASIA p=0.0100 alpha=0.0167 reject=True final=CONFIRMED (d > 0)
R2 LONDON p=1.0000 alpha=0.0500 reject=False final=NOT READABLE
R2 NY p=0.0100 alpha=0.0250 reject=True final=CONFIRMED (d > 0)
```

- NY's own CI excludes 0 at 0.05, but the step-down stopped at LONDON. NY is correctly not rejected. That is Holm, not per-test thresholds.
- In `R2`, the NOT READABLE session enters with p = 1 and sorts last. The readable sessions get 0.05/3 and 0.05/2. That is `TB-D6` as built.

### `E-2` — mutation runs (the instrument is scratch; the edits are listed so a reader can repeat them)

Each mutant is a copy of `HEAD`'s script with one exact-string edit (asserted to match once), run with `--selftest --resamples 500`. Actual output:

```
c0_negation_dropped: SELFTEST FAILED (9 failed)  FAIL lines=9
m4_holm_no_multiplicity: SELFTEST PASSED (0 failed)  FAIL lines=0
m5_tbd5_label_removed: SELFTEST PASSED (0 failed)  FAIL lines=0
m6_bo_h2_drops_s_add: SELFTEST PASSED (0 failed)  FAIL lines=0
m7_seam_sens_uses_poc_filter: SELFTEST PASSED (0 failed)  FAIL lines=0
m8_holm_reject_ignores_holm: SELFTEST PASSED (0 failed)  FAIL lines=0
```

| Mutant | Edit | Expected | Got |
|---|---|---|---|
| `c0` (control) | `arm_rw`: `neg` keeps rw_run's sign (the packet's `m2`) | FAIL | FAIL ✅ — the harness can go red |
| `m4` | `holm`: `a_i = 0.05` for every rank (no multiplicity) | FAIL | **PASS** |
| `m5` | `finalize_label`: the `TB-D5` clause becomes `if False:` | FAIL | **PASS** |
| `m6` | `BO-H2` `in_c`: A rows only, `S_add` dropped | FAIL | **PASS** |
| `m7` | `BO-H1` pre-seam run filters on `poc == "pre"` instead of `seam == "pre"` | FAIL | **PASS** |
| `m8` | `holm`: `ok = True` (every result rejects) | FAIL | **PASS** |

### `H-3` — selftest

At `HEAD` (3 cases, 10,000 resamples), tail (actual): `SELFTEST PASSED (0 failed)`, 79 PASS lines, 0 FAIL lines, `real 6m27.018s`. Per-case BO-H1 lines (actual):

```
==== SELFTEST case: effect (A = +5 bps, B = 0, noise sd 10) ====
      ASIA BO-H1 FULL d = +5.5 [+4.4, +6.6]  Holm CI [+4.1, +6.9]  label: CONFIRMED (d > 0)
      LONDON BO-H1 FULL d = +4.8 [+3.7, +6.0]  Holm CI [+3.5, +6.1]  label: CONFIRMED (d > 0)
      NY BO-H1 FULL d = +5.0 [+3.8, +6.2]  Holm CI [+3.8, +6.2]  label: CONFIRMED (d > 0)
==== SELFTEST case: null (B rows carry their A row's EV) ====
      ASIA BO-H1 FULL d = -0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
      LONDON BO-H1 FULL d = +0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
      NY BO-H1 FULL d = +0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
==== SELFTEST case: seam flip (A = -4 bps before the session seam, +40 from it, B = 0) ====
      ASIA BO-H1 FULL d = +7.5 [+3.1, +12.0]  Holm CI [+2.3, +13.0]  label: H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) EDGE-SENSITIVE SEAM-SENSITIVE
      LONDON BO-H1 FULL d = +6.8 [+2.6, +11.3]  Holm CI [+1.9, +12.1]  label: H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) EDGE-SENSITIVE SEAM-SENSITIVE
      NY BO-H1 FULL d = +4.3 [+0.3, +8.5]  Holm CI [+0.3, +8.5]  label: H1 FINDING, H2 CONTRADICTS OR NOT READABLE (d < 0) EDGE-SENSITIVE SEAM-SENSITIVE
SELFTEST PASSED (0 failed)
```

At `8943324` (`git show 8943324:tools/ops/burst_outcome_read.py` into `<S>`, run with `--root C:/Dev/DeribitVerdictEngine --selftest`): see the line under `H-3b` at the end of this section.

⚠ The packet's `H-3` paste shows 2 cases and 2 min 12 s. That is the `8943324` script. `HEAD` has 3 cases and takes about 6.5 min.

### `H-1` — default-mode parity (no rotated book)

Start-commit tree extracted with `git archive b5b4a7d | tar -x` into `<S>` (no worktree created), built with `-o <S>/old-bin`. New binary = `HEAD`'s rebuilt one. Each run had its own copy of the `backtest_data/q1d-tier-geometry` candle cache. Diff only (actual):

```
3c3
< - Run at (UTC): 2026-10-02 12:19:42
---
> - Run at (UTC): 2026-10-02 12:19:57
diff exit=1
   507 .../h1-old.md
   507 .../h1-new.md
```

### `H-1b` — `diagexport` parity (no rotated book), new

Same two binaries, `--mode diagexport` on `aws_fetch/20260924-084613`. Console lines and MD5 only (actual):

```
- Population rows: 10520. Rows without a logged row (must be 0): 0.
- Split date 2026-08-17 00:00 UTC: H1 = 29 trading days, H2 = 29 (the stability read's rule).
- Population rows: 10520. Rows without a logged row (must be 0): 0.
- Split date 2026-08-17 00:00 UTC: H1 = 29 trading days, H2 = 29 (the stability read's rule).
219dac4a5ecbb065554f4e4d61f0421d *.../dx-old.csv
219dac4a5ecbb065554f4e4d61f0421d *.../dx-new.csv
```

This closes the packet's "not verified" item on `diagexport` for a fetch without a rotated book.

### `H-6` — window split (actual)

```
== --cut-before 2026-09-01T00:00:00Z exit=0
- Export rows inside the window: 6699. Excluded, by reason:
- Analysis population (...): 4813 rows, first 2026-07-22 16:31:01, last 2026-08-31 23:44:02.
ASIA    all  44 (5/16/23)     368 (19/109/240) ...
LONDON  all  61 (10/29/22)    490 (59/171/260) ...
NY      all  120 (16/47/57)   1546 (163/550/833) ...
== --from 2026-09-01T00:00:00Z exit=0
- Export rows inside the window: 4302. Excluded, by reason:
- Analysis population (...): 4278 rows, first 2026-09-01 00:21:08, last 2026-09-28 12:12:01.
ASIA    all  35 (6/12/17)     575 (41/187/347) ...
LONDON  all  16 (1/8/7)       355 (40/129/186) ...
NY      all  49 (5/21/23)     1316 (102/430/784) ...
```

4,813 + 4,278 = 9,091. 6,699 + 4,302 = 11,001. Arm A: 44 + 35 = 79, 61 + 16 = 77, 120 + 49 = 169.

### `H-4` — build and gate (actual tails)

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

```
OK    harness ALL PASS
=== display-parity ===
OK    no snapshot/card drift detected
=== version-bump ===
OK    no engine-path change
=== rotation-riders ===
OK    AnalysisLogger.vb not in the changed set - no header rotation possible
=== result ===
GATE PASSED
exit=0
```

### `H-5` — diff stat (actual)

```
 .../SwingFallbackRead/MediumTierDiagnosisExport.vb |    5 +-
 tools/ops/SwingFallbackRead/SwingFallbackRead.vb   |   25 +-
 tools/ops/burst_outcome_read.py                    | 1015 ++++++++++++++++++++
 3 files changed, 1038 insertions(+), 7 deletions(-)
```

The docs commit `55afe41` holds **three** files, not "this file and the queue line only": `docs/burst-outcome-read-tools-decisions-for-orchestrator.md` (+73), `docs/burst-outcome-read-tools-spec-back.md` (+341), `docs/trader-tick-queue.md` (1 row). Harmless; the packet's handle text is wrong.

Since the build (`git diff --stat 8943324 HEAD`): `burst_outcome_read.py` +81/−25 over `15a0234`, `98ed7cc`, `4de2a3b`; `SwingFallbackRead.vbproj` +1 (`Core/RunErrorLog.vb`, from `bcb8e56`, the collector-halt fix). No `.vb` source of `SwingFallbackRead` changed after `8943324`.

### `H-3b` — selftest at `8943324`

Actual: `SELFTEST PASSED (0 failed)`, 32 PASS lines, 0 FAIL lines, `real 2m12.429s`. The BO-H1 lines are byte-identical to the packet's `H-3` paste:

```
      ASIA BO-H1 FULL d = +4.9 [+3.6, +6.2]  Holm CI [+3.4, +6.4]  label: CONFIRMED (d > 0)
      LONDON BO-H1 FULL d = +4.7 [+3.4, +6.1]  Holm CI [+3.2, +6.3]  label: CONFIRMED (d > 0)
      NY BO-H1 FULL d = +4.4 [+3.0, +5.8]  Holm CI [+3.0, +5.8]  label: CONFIRMED (d > 0)
      ASIA BO-H1 FULL d = -0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
      LONDON BO-H1 FULL d = -0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
      NY BO-H1 FULL d = +0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
```

---

## 3. Findings, most severe first

| ID | Severity | Where | One line |
|---|---|---|---|
| `RV-1` | **High** | `tools/ops/SwingFallbackRead/SwingFallbackRead.vb:953`, `:809`, `:813-815`, `:826`; `tools/ops/burst_outcome_read.py:96-97`, `:645-651` | A partial cached candle week is reused silently. Rows past its last bar score −fee. Nothing counts or stops it |
| `RV-2` | **Medium-high** | `tools/ops/burst_outcome_read.py:830-834`, `:1041-1046`, `:296-306` | Full mode opens outcomes even if reconciliation fails. Window, seed, resamples and settings version are free |
| `RV-3` | **Medium** | `tools/ops/burst_outcome_read.py:1011-1028` | The selftest cannot fail on Holm, the `TB-D5` label, the `BO-H2` arm set or the seam filter |
| `RV-4` | **Medium (before run 2)** | `tools/ops/burst_outcome_read.py:687-707`, `:313-317` | No session filter for the per-session run 2; no handling of daylight-saving-aware session hours |
| `RV-5` | Low | `tools/ops/burst_outcome_read.py:625-626`, `:716-726` | The `TB-D5` label contains the word CONFIRMED; the table does not print the Holm reject flag |
| `RV-6` | Low | `tools/ops/burst_outcome_read.py:846-850`, `:264-265`; `tools/ops/SwingFallbackRead/SwingFallbackRead.vb:273-276` | Stale docstring, a sign-inverted unused `ALL_LABELS`, and `boxLog` last-wins against merge first-wins |

### `RV-1` — partial candle week reused; missing bars score −fee, silently (High)

- **Mechanism.** `LoadBarsAsync` reads a cached week file whenever it exists (`SwingFallbackRead.vb:953`). It never checks that the file reaches the week's close. `Walk` skips a missing bar (`:809`). With no bar at all, `MarkBps` stays 0 (`:813-815`), so `Result` returns 0 − fee (`:826`).
- **Not reported in this path.** `diagexport` returns before the coverage log `covLog` is printed (`SwingFallbackRead.vb:367-368`, `:406`). The export has a `MainMissingBars` column. `burst_outcome_read.py` never reads it: it is in `FORBIDDEN_IN_COUNTS` (`:97`), and `run_full` reads only `MainNetEv`, `MainOutcome`, `TBps`, `SBps` (`:647-651`).
- **The trap is armed today.** The script's own run recipe (`burst_outcome_read.py:57`) and the packet use `--cache backtest_data/burst-outcome-read`. In that folder, `ohlc_1m_BTC-PERPETUAL_week_2026-09-28.csv` runs from `2026-09-28 00:01` to **`2026-09-29 14:03`** (2,284 lines; checked by reading its first and last timestamp). The same hazard was named for the q1d read (`docs/q1d-tier-geometry-read-2026-09-24.md` line 211, "Use a separate `--cache` for the newest fetch").
- **Failure scenario.** The run-1 seat follows the recipe after 2026-11-25. Every directional row from 2026-09-29 14:04 to the 2026-10-02 close gets no bars. Each scores outcome 0 and −3 bps, in both arms. A − B shrinks toward 0 for that slice. Success % and net edge in `burst-outcome-read-spec.md` §5 terms fall. No line of either output says so. A seat that notices only after reading outcomes faces exactly the adapt-or-not choice the pre-registration forbids.
- **Fix (tools-only, before run 1).** Two parts, both pre-outcome:
  1. Procedure: run 1 uses a **fresh, empty** `--cache` folder. Write it into `docs/burst-outcome-read-spec.md` §6.1.
  2. Guard: in full mode, read `MainMissingBars` **first**, before `MainNetEv`. Print the count of population rows with `MainMissingBars > 0`, per session and arm. **STOP** if any is non-zero, unless a pre-registered rule says otherwise. `MainMissingBars` is a coverage column, not an outcome. Move it out of `FORBIDDEN_IN_COUNTS` so counts-only can print it too.
  3. Optional: `LoadBarsAsync` re-fetches a cached week whose last bar is before the week close and before now.
- **Read on the cheaper option.** "Just tell the seat to use a fresh cache" is the cheaper option. It is procedure-only and lives in a doc. Per the repo's prior (docs rot, code survives), I recommend the code guard as well.

### `RV-2` — full mode has no pre-outcome hard gates and no run pin (Medium-high)

- **Mechanism.** `run()` prints every reconciliation check, then calls `run_full` unconditionally (`burst_outcome_read.py:830-834`). Nothing stops on:
  - analysis rows not in the power-count population (`extra`), or a different arm (`arm_diff`);
  - export `Session` differing from the hour's session, or `AggrVelSignal` differing from the book;
  - rows dropped for "verdict differs" or "Side/Tier inconsistent";
  - a settings version other than v69 (only the fee style is checked, `:304-305`).
- `--cut-before`, `--from`, `--seed` and `--resamples` are free arguments (`:1041-1046`). A full run with no window is allowed.
- **Failure scenario 1.** Before run 1, a header rotation or a settings change (for example the daylight-saving-aware session hours, audit row `F5`, if its code lands before the run-1 seat starts) moves rows between sessions. `arm_diff` goes non-zero. The full read still prints all labels. The seat must then decide whether the drift matters **after** seeing outcomes.
- **Failure scenario 2.** The seat omits `--cut-before 2026-11-25T00:00:00Z`. The output prints outcomes from rows after the cut. Those rows belong to neither run (`docs/burst-outcome-read-spec.md` §6.1, the 2026-10-01 amendment), and they are now seen.
- **Fix (tools-only, before run 1).**
  - Add `--run 1`, which pins `cut_before = 2026-11-25 00:00:00`, the seed 20260928 and 10,000 resamples. Refuse full mode without `--run 1` or an explicit `--run 2 --from <first row after the F5 deploy>`.
  - Before `run_full`, STOP unless `extra == 0`, `arm_diff == 0`, session and signal mismatches are 0, and the `settings.json` version equals the pinned one. Each STOP names the check. Counts-only keeps printing them.
  - Record in `docs/burst-outcome-read-spec.md` §6.1 that run 1 runs from a worktree at the reviewed tools commit.

### `RV-3` — the selftest shape cannot fail on the decisive label logic (Medium)

- **Evidence.** `E-2`: the control mutant goes red, and five mutants survive (Holm multiplicity removed; Holm reject forced; `TB-D5` label removed; `BO-H2` arm set changed; seam sensitivity filtered on the POC era).
- **Why each survives.**
  - The effect case's p values are tiny, so any α passes. The null case never reaches CONFIRMED. No case reaches the "NOT CONFIRMED" label.
  - The selftest checks only that the `BO-H2` table prints (`:1028`), not its values. The packet says so (`docs/burst-outcome-read-tools-spec-back.md` §1, `H-3` note).
  - In the seam-flip case, every pre-POC row is also pre-seam. Both filters give a negative point, so the seam filter and the POC filter cannot be told apart.
- **What I checked instead.** `H-7` shows `holm()` and `finalize_label()` are correct today. I read `in_c`, `out_c` and the seam filter, and they match `docs/burst-outcome-read-spec.md` §4 and §6.2. So this is a guard gap, not a known bug.
- **Fix (tools-only, before run 1).**
  - Add the `H-7` cases to `--selftest` as deterministic assertions: one session rejects, the next is stopped, and a NOT READABLE session gets p = 1.
  - Add a `BO-H2` value case: give `S_add` rows a known EV, and assert the `BO-H2` point.
  - Add a seam case where pre-POC and pre-seam disagree in sign. Example: A is negative only between the POC edge (2026-09-24 18:46:06) and the seam, so pre-POC rows have a positive A − B and pre-seam rows a negative one.
  - Then re-run `m4` to `m8` and see each go red.

### `RV-4` — run 2 per session, and the session-hours change (Medium; must land before run 2, not run 1)

- **Mechanism.** The full read always prints all three sessions (`:687-707`). Session assignment uses fixed hours from `settings.json` (`Ctx.session_of`, `:313-317`). `EDGES`, `SHADOW` and `SEAM` are per fixed session.
- **Failure scenario.** Run 2 is now per session (`docs/burst-outcome-read-spec.md` §6.1, amended 2026-09-29). The NY seat runs it about 2027-01-07. The output also prints ASIA and LONDON outcome tables on run-2 rows. ASIA's run 2 (about 2027-02-24) then reads data whose early part has already been seen. Its blindness is gone.
- Also, the daylight-saving-aware session hours (`F5`) ship before run 2 and move rows between sessions. The fixed-hour `session_of` check and the export's session will then disagree in a way this code cannot describe.
- **Fix.** Before the first run 2: add `--session S`, which computes and prints outcome tables for that session only. Make the session rule follow the `F5` spec. Re-run the selftest. Queue it as a rider. No run-1 effect.

### `RV-5` — label text and Holm visibility (Low)

- The `TB-D5` label starts "NOT CONFIRMED: …" (`:626`). A reader or script that searches for "CONFIRMED" matches it. The decision table in `docs/burst-outcome-read-spec.md` §7 keys on "CONFIRMED". The selftest uses `startswith`, so it is safe. A human skim is not.
- The direction text of CONFIRMED comes from the H1 point (copied `label`, `:233`). The `TB-D5` label takes it from the FULL point (`:626`). They can differ only in an odd case. Harmless, but undocumented.
- The `BO-H1` and `BO-H2` tables print the boot p, the Holm α and the Holm CI, but not `reject` (`:716-726`). For a result that is not CONFIRMED, a reader cannot see whether the step-down stopped earlier.
- **Fix.** Print a `Holm passed` column. Leave the label wording to the trader. It is now pre-registered (ruled 2026-09-29), so changing it is a spec amendment, and it must happen before run 1 if at all.

### `RV-6` — small hygiene (Low)

- `st_build`'s docstring says "60 weekdays" (`:846`); `NDAYS = 80` (`:842`).
- The copied `rw_run` appends its label to `ALL_LABELS` with B − A's sign (`:264-265`). `arm_rw` negates the result but not that entry. `ALL_LABELS` is never printed today. If a later edit prints it, every direction is inverted.
- `SwingFallbackRead.vb:273-276` builds `boxLog` last-wins (live over rotated over v0.7), while the merge is first-wins. `boxLog` feeds only the default mode's eval-cache join diagnostic. `H-1` shows no effect on a fetch without a rotated book. On a fetch with one, a Timestamp in two books would differ between the two structures. Count: not measured.

### Checked and found sound

| Check | How | Result |
|---|---|---|
| Copied statistics block vs `tools/ops/medium_tier_diagnosis.py` at `3288d32` | `diff -w` of the two blocks | Only the two marked `DRAWS.append` lines and the marked covered-strata readability lines differ |
| Breakevens against `docs/DeribitIndicatorProject.md` §5a rule 1 | Read `:677-681` and §5a | Σ forms, as fixed by `98ed7cc`. Net edge = success − net breakeven ✅ |
| Arms, shadow arms, exclusions vs `docs/burst-outcome-read-spec.md` §5 | Read `arm_fields`, `analysis_population` | Match. `H-2` proves parity with the power count's PowerShell rule |
| Strata = fifth × band × POC era × seam era | Read `strat_key` (`:572-573`) | Matches `docs/burst-outcome-read-spec.md` §5 and §6.2 |
| Seam times | Calendar: Europe leaves summer time 2026-10-25 01:00 UTC; the US 2026-11-01 06:00 UTC | `SEAM` matches |
| Halves on the analysis population (`TB-D8`, the halves decision) | Read `:652-656` | First ⌊D/2⌋ days after window and exclusions ✅ |
| `FULL` draws picked for Holm | Read `arm_rw` `:587-589` and `rw_run`'s halves order | `DRAWS[n0]` is the FULL call ✅ |
| Rotated-book order, Python vs VB | Read `book_names` (`:335-337`) and `SwingFallbackRead.vb:208-209` | Both sort on the rotation stamp ✅ |
| Counts-only never opens an outcome column | Read `read_export` whitelist and the asserts (`:99`, `:469`) | ✅ |

---

## 4. My read on the `TB-D*` decisions

`TB-D` = "tools build decision", defined in `docs/burst-outcome-read-tools-spec-back.md` §2. The orchestrator already ruled them on 2026-09-29 (`docs/burst-outcome-read-tools-decisions-for-orchestrator.md` §5). This is a second opinion.

| ID | Decision | My read | Why |
|---|---|---|---|
| `TB-D5` | Label when both halves are significant and agree, but the Holm-adjusted FULL CI includes 0 | ✅ **Agree with (b)**, the new label | (a) prints "H2 SAME SIGN NOT SIGNIFICANT" when H2 is significant: a false statement. (b) is the truthful, self-describing option. `H-7` shows it fires exactly when Holm stops. Riders in `RV-5`: print the Holm flag; consider wording without the word CONFIRMED |
| `TB-D6` | Holm family sizes | ✅ **Agree.** `BO-H1` (the primary A − B test): m = 3 always, NOT READABLE at p = 1. `BO-H2` (the option (c) test): m = readable sessions | Both follow the spec's words (`docs/burst-outcome-read-spec.md` §4). Readability is set by counts in covered strata, never by outcomes, so choosing the `BO-H2` family by readability cannot adapt to results. `BO-H1` m = 3 is the more conservative reading and matches the power count's α/3. ⚠ A uniform m = 3 for `BO-H2` would be more conservative still, but it contradicts the spec's text, so I do not recommend it. This is not a cheaper-option pick: no option records more |
| `TB-D1` | `H-1` target = old vs new binary | Agree | A byte match against a v68 document is impossible at v69. `H-1b` extends it to `diagexport` |
| `TB-D2` | Merge order pooled → rotated → live, first wins | Agree | Time order. Verdict conflicts are counted and dropped (0 today) |
| `TB-D3` | `diagexport` raw loader reads rotated books | Agree | Without it the export exits 3 |
| `TB-D4` | Holm mechanics on the bootstrap | Agree | `H-7` confirms step-down and adjusted CI. Requiring both p ≤ α_i and an excluding CI is redundant but harmless |
| `TB-D7` | Reuse `rw_run`, negate | Agree | Control mutant `c0` shows the negation is load-bearing |
| `TB-D8` | Halves on the analysis population | Agree | The export's `Half` would split runs wrongly |
| `TB-D9` | EDGE-SENSITIVE scope | Agree | Spec says "labelled result"; NOT READABLE and NO DIFFERENCE SHOWN carry no direction |
| `TB-D10` | Arm fields from the books | Agree | The export lacks TFI, ratio and net |
| `TB-D11` | Seed 20260928 | Agree, with `RV-2`: pin it in code for run 1 |
| `TB-D12` | Explain columns raw | Agree; the Σ fix in `98ed7cc` is correct |
| `TB-D13` | `BO-H3` content | Agree | Descriptive; matches `docs/burst-outcome-read-spec.md` §4 and §6.2 |

---

## 5. Observations on the spec (not build findings; for the trader before run 1)

- **Rows, not episodes.** `docs/DeribitIndicatorProject.md` §5a rule 3a (added 2026-10-01) says consecutive rows of one signal are one episode, about 12.6 NY rows per episode (adversarial audit row `C4`). The burst read's readability floor of 100 counts rows. The day bootstrap keeps the CIs honest. But "n ≥ 100" overstates the information in dense arms, mostly B. Not a reason to change run 1. Worth one line in the run-1 read.
- **Band is a post-burst stratum.** A rows sit in the band after the +1. About one A row in three is `crossed` (`docs/burst-outcome-read-spec.md` §2.3). `BO-H1` therefore asks "does an upgraded row trade like a non-burst row of the band it landed in?". That is a fair question, but it is not "does the burst add EV over the same pre-burst score". `BO-H3`'s crossed − uncrossed row is the only window on it.

---

## 6. What I did not verify

- **Any real outcome.** By design.
- **How many rows of today's export have `MainMissingBars > 0`.** Reading that column on real data is outside the runs this review was allowed. `RV-1`'s scenario is from the code and the cache file's last timestamp, not from a measured count.
- **That `AggrVelSignal` holds only `NORMAL`, `BURST_BUY`, `BURST_SELL` on rows with a ratio.** Python and PowerShell both map any other value to arm B. `H-2` proves they agree, not that the value set is clean.
- **The other `SwingFallbackRead` modes on a rotated-book fetch.** Already a queued rider (`docs/trader-tick-queue.md`, the `census`/`rescore`/`pocgate`/`liqflag` row). Not run.
- **Full-mode run time on real data.** Not run.
- **Python `float()` against .NET parsing on `nan`/`inf`.** Carried from the packet. Not checked.
- **The `boxLog` divergence count in `RV-6`.** Not measured.
- **The solution build.** Only the project and the gate's harness were built.
