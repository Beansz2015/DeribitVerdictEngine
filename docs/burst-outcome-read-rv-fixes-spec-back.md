# Burst outcome read tools — review fixes `RV-1`, `RV-2`, `RV-3`, `RV-5`, `RV-6`: spec-back (2026-10-02 UTC)

**Reviewer: Opus 5.5, high.**
- **Why that tier:** every change sits on the outcome path of a pre-registered read (run 1, after 2026-11-25). The review must confirm two things a lower tier tends to wave through: (1) no outcome column is opened before the new gates, and (2) each new selftest case can go red. Both are checkable from the handles below, but a wrong "looks fine" here contaminates run 1 for good.
- **Where a reviewer slips:** running the full mode on real data "to see the gates work". It is forbidden. The gates are proved on synthetic data (`H-1`) and on counts-only (`H-3`, `H-4`).
- **Move to a second seat** if `H-1` fails, or if `H-2` / `H-2b` show any line other than `Run at`.

**Work list:** [`docs/burst-outcome-read-tools-review-2026-10-02.md`](burst-outcome-read-tools-review-2026-10-02.md) §3 (the review; `RV-n` = "review finding n", its IDs). **Spec:** [`docs/burst-outcome-read-spec.md`](burst-outcome-read-spec.md). **Start commit:** `5192b2b`. **Tools commit:** `08ca2ed`. **Docs commit:** this file and the `docs/burst-outcome-read-spec.md` §6.1 run-1 procedure box.

⛔ **No real outcome was printed, aggregated, sorted, plotted or read.** Real-data runs: `SwingFallbackRead` default mode and `diagexport` for parity (diff and MD5 only); `diagexport` console tails; the power count; `--counts-only`. `MainMissingBars` (now a coverage column, `RV-1`) was read on real data through counts-only and one scratch count of its non-zero rows by Timestamp. The full mode never ran on real data. The full-mode refusal was checked on real data only in the form that exits at argument parsing (`H-5`).

---

## 0. What changed

| Finding (review) | Change | File |
|---|---|---|
| `RV-1` a (partial cache week) | `LoadBarsAsync`: a cached week whose last bar is before the week close AND before now is re-fetched. Its coverage-log source reads `Deribit (cache was partial, last bar MM-dd HH:mm; re-fetched)`. A complete cached week is reused exactly as before | `tools/ops/SwingFallbackRead/SwingFallbackRead.vb` |
| `RV-1` b (guard) | `MainMissingBars` moved out of `FORBIDDEN_IN_COUNTS` into a new `COVERAGE_COLS`, read with the signal columns before any outcome column. Both modes print the count of population rows with `MainMissingBars` > 0 per session × arm A/B/C/O. Full mode STOPs if any is > 0 or not parseable | `tools/ops/burst_outcome_read.py` |
| `RV-1` c (procedure) | Run 1 uses a fresh, empty `--cache` folder | `docs/burst-outcome-read-spec.md` §6.1 |
| `RV-2` (pins and gates) | `--run 1` pins Timestamp < 2026-11-25 00:00:00, seed 20260928, 10,000 resamples, and refuses `--cut-before`, `--from`, `--seed`, `--resamples`. `--run 2` needs `--from` ≥ the run-1 cut. Full mode refuses to start without `--run` (in `main()` and again in `run()`). Eight named gates print in both modes; full mode STOPs (exit 2, failed names listed, output file still written) before `run_full` on any FAIL | `tools/ops/burst_outcome_read.py` |
| `RV-2` (procedure) | Run 1 runs from a worktree at the reviewed tools commit | `docs/burst-outcome-read-spec.md` §6.1 |
| `RV-3` (selftest) | New cases: Holm and `TB-D5` label units (the review's `H-7`); run-pin argument units; one gate variant per gate; a `const` case (no noise) that pins the `BO-H1` point, the `BO-H2` point with `S_add` dilution, and a seam/POC sign disagreement | `tools/ops/burst_outcome_read.py` |
| `RV-5` (display) | `Holm passed` column (yes/no) in the `BO-H1` and `BO-H2` tables. No label wording changed | `tools/ops/burst_outcome_read.py` |
| `RV-6` (hygiene) | `st_build` docstring now says NDAYS (80); `arm_rw` replaces `rw_run`'s B − A entry in `ALL_LABELS` with the A − B one; one-line comment at `boxLog` naming last-wins vs merge first-wins | both files |

The pinned values match the spec: cut 2026-11-25 00:00:00 UTC (`docs/burst-outcome-read-spec.md` §6.1 box), seed 20260928 (decision `TB-D11`, "tools build decision 11", ruled 2026-09-29), 10,000 resamples (`docs/burst-outcome-read-spec.md` §5 "CI" row), settings v69 (`docs/burst-outcome-read-spec.md` §1 and §5 "Outcome" row). No difference found.

---

## 1. Ranked verification handles

`H-n` = a reader can run it. `E-n` = build-time evidence from a scratch instrument; the edits are listed so a reader can repeat them. All from the repo root at the tools commit.

| Rank | Handle | What it shows | Result |
|---|---|---|---|
| 1 | `H-1` | `python tools/ops/burst_outcome_read.py --selftest` | ✅ `SELFTEST PASSED (0 failed)`, 168 PASS lines, 7 sections |
| 2 | `E-1` | 11 mutants of the script, each must FAIL the selftest | ✅ 11 of 11 red, including all 5 of the review's survivors |
| 3 | `H-2` | Default mode, old binary (`5192b2b`) vs new, on `aws_fetch/20260924-084613`, complete cache weeks | ✅ only `Run at` differs |
| 4 | `H-2b` | `diagexport`, old vs new binary, same fetch and cache | ✅ CSVs byte-identical, MD5 `219dac4a…` = the review's `H-1b` |
| 5 | `H-3` | Power count vs counts-only `--run 1` on `aws_fetch/20260928-121255`, export from a fresh empty cache | ✅ tables identical; coverage 0; all 8 gates PASS |
| 6 | `H-4` | The `RV-1` scenario, measured: `diagexport` on `aws_fetch/20261002-121123` with a copy of the recipe cache, old vs new binary, then counts-only | ✅ old: 631 population rows with missing bars, gate FAIL; new: 0, gate PASS |
| 7 | `H-5` | Full mode without `--run`, and `--run 1 --cut-before …`, on real paths | ✅ both refused at argument parsing, exit 2 |
| 8 | `H-6` | Build, `verify-gate.ps1`, diff stat | ✅ 0 warnings / 0 errors; `GATE PASSED`; 2 files in the tools commit |

### `H-1` — selftest (10,000 resamples)

Run on the committed script (working-file MD5 `f36a5c48a6ac9c93b1e92bef7d76c954` = `git show 08ca2ed:tools/ops/burst_outcome_read.py | md5sum`). Tail and per-case lines (actual):

```
==== SELFTEST case: Holm step-down and the TB-D5 label (deterministic draws) ====
==== SELFTEST case: run pins (argument resolution) ====
==== SELFTEST case: pre-outcome gates (one broken check per variant) ====
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
==== SELFTEST case: const (no noise; B = 0; A = +2 before the POC edge, -10 from it to the seam, +40 from the seam) ====
      ASIA BO-H1 FULL d = +8.3 [+4.3, +12.6]  Holm CI [+3.5, +13.3]  label: CONFIRMED (d > 0) SEAM-SENSITIVE
      LONDON BO-H1 FULL d = +8.3 [+4.2, +12.6]  Holm CI [+3.8, +13.2]  label: CONFIRMED (d > 0) SEAM-SENSITIVE
      NY BO-H1 FULL d = +5.2 [+1.5, +9.2]  Holm CI [+1.5, +9.2]  label: CONFIRMED (d > 0) SEAM-SENSITIVE
SELFTEST PASSED (0 failed)
real	8m27.706s
PASS=168 FAIL=0
```

New assertion lines, selected (actual, from `--selftest --resamples 500` runs during the build; these lines do not depend on the resample count, and the const lines are identical in the 10,000 run):

```
PASS  Holm unit: boot p = 0.01 / 0.03 / 0.04 (got 0.0100 / 0.0300 / 0.0400)
PASS  Holm unit: alpha_i = 0.05/3, 0.05/2, 0.05 by rank
PASS  Holm unit: ASIA (p 0.01 <= 0.0167) passes Holm
PASS  Holm unit: LONDON (p 0.03 > 0.025) is stopped
PASS  Holm unit: NY is NOT passed although its own p 0.04 <= 0.05 - the step-down stopped at LONDON
PASS  Holm unit: LONDON and NY reach the TB-D5 label 'NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0)' (...)
PASS  Holm unit: a NOT READABLE session enters with p = 1, ranks last, is not passed
PASS  gate missing_bars: full mode STOPs naming exactly [missing_bars] before any outcome column (edited 1 rows; got ['missing_bars'])
PASS  gate missing_bars: counts-only prints FAIL missing_bars and does not stop (got ['missing_bars'])
PASS  gate arm_diff: full mode STOPs naming exactly [arm_diff] before any outcome column (edited 1 rows; got ['arm_diff'])
PASS  gate extra: full mode STOPs naming exactly [extra] before any outcome column (edited 1 rows; got ['extra'])
PASS  gate settings_version: full mode STOPs naming exactly [settings_version] before any outcome column (edited 0 rows; got ['settings_version'])
PASS  gate settings_version: --run 2 STOPs while run 2 has no pinned version (got ['settings_version'])
PASS  ASIA const: pre-POC-only d > 0, pooled d > 0, pre-seam-only d < 0 (pre-POC 2.0, pooled +8.35, pre-seam -2.2)
PASS  ASIA const label carries SEAM-SENSITIVE and not EDGE-SENSITIVE (got 'CONFIRMED (d > 0) SEAM-SENSITIVE')
PASS  ASIA const BO-H2 FULL point = sum(A EV) / (nA + nS_add) over fifths 4-5 = +6.9583 (got +6.9583; A only would be +8.3500)
PASS  NY const BO-H2 FULL point = sum(A EV) / (nA + nS_add) over fifths 4-5 = +4.3542 (got +4.3542; A only would be +5.2250)
```

Every gate (`missing_bars`, `extra`, `arm_diff`, `session_mismatch`, `signal_mismatch`, `verdict_differs`, `side_tier_inconsistent`, `settings_version`) has one variant that breaks only it, on one NY arm-A row; each asserts the STOP names exactly that gate. Arm counts per session are now 400 / 800 / 80 / 80 / 80 (A / B / C / O / `S_add`): NDAYS 80, not the build packet's 60.

- The three old cases print BO-H1 lines identical to the review's `H-3` paste at `9d764b2` (for example `ASIA BO-H1 FULL d = +5.5 [+4.4, +6.6]  Holm CI [+4.1, +6.9]`). So the statistics did not move.
- ⚠ The gate variants and Holm units ran before the bootstrap cases; the gate variants each STOP before any bootstrap, so they add seconds, not minutes.

### `E-1` — mutants (scratch copies outside the repo, `--selftest --resamples 500`)

Each mutant is the committed script (MD5 `f36a5c48…`, recorded before the run) with one exact-string edit, asserted to match once. Instrument: a scratch `mutate.py` in the session scratchpad, not committed. Actual output:

```
c0_negation_dropped: SELFTEST FAILED (18 failed)  FAIL lines=18  exit=1
m4_holm_no_multiplicity: SELFTEST FAILED (5 failed)  FAIL lines=5  exit=1
    FAIL  Holm unit: alpha_i = 0.05/3, 0.05/2, 0.05 by rank
    FAIL  Holm unit: LONDON (p 0.03 > 0.025) is stopped
m5_tbd5_label_removed: SELFTEST FAILED (1 failed)  FAIL lines=1  exit=1
    FAIL  Holm unit: LONDON and NY reach the TB-D5 label '...' (got 'CONFIRMED (d > 0)' / 'CONFIRMED (d > 0)')
m6_bo_h2_drops_s_add: SELFTEST FAILED (3 failed)  FAIL lines=3  exit=1
    FAIL  ASIA const BO-H2 FULL point = sum(A EV) / (nA + nS_add) over fifths 4-5 = +6.9583 (got +8.3500; A only would be +8.3500)
m7_seam_sens_uses_poc_filter: SELFTEST FAILED (6 failed)  FAIL lines=6  exit=1
    FAIL  ASIA const: pre-POC-only d > 0, pooled d > 0, pre-seam-only d < 0 (pre-POC 2.0, pooled +8.35, pre-seam 2.0)
    FAIL  ASIA const label carries SEAM-SENSITIVE and not EDGE-SENSITIVE (got 'CONFIRMED (d > 0)')
m8_holm_reject_ignores_holm: SELFTEST FAILED (4 failed)  FAIL lines=4  exit=1
    FAIL  Holm unit: LONDON (p 0.03 > 0.025) is stopped
    FAIL  Holm unit: NY is NOT passed although its own p 0.04 <= 0.05 - the step-down stopped at LONDON
m9_missing_bars_gate_removed: SELFTEST FAILED (2 failed)  FAIL lines=2  exit=1
    FAIL  gate missing_bars: full mode STOPs naming exactly [missing_bars] before any outcome column (edited 1 rows; got None)
m10_full_mode_ignores_failed_gates: SELFTEST FAILED (9 failed)  FAIL lines=9  exit=1
    FAIL  gate missing_bars: full mode STOPs naming exactly [missing_bars] before any outcome column (edited 1 rows; got None)
m11_run2_from_before_cut_allowed: SELFTEST FAILED (1 failed)  FAIL lines=1  exit=1
    FAIL  run pins: refused: --run 2 --from before the run-1 cut (None)
m13_run2_version_gate_open: SELFTEST FAILED (1 failed)  FAIL lines=1  exit=1
    FAIL  gate settings_version: --run 2 STOPs while run 2 has no pinned version (got None)
m12_all_labels_fix_removed: SELFTEST FAILED (11 failed)  FAIL lines=11  exit=1
    FAIL  ASIA ALL_LABELS entry for BO-H1 matches the A - B result
```

| Mutant | Edit | Covers |
|---|---|---|
| `c0` (control) | `arm_rw`: `neg` keeps `rw_run`'s sign | the harness can go red |
| `m4` | `holm`: `a_i = 0.05` at every rank | review mutant, survived before |
| `m5` | `finalize_label`: the `TB-D5` clause becomes `if False:` | review mutant, survived before |
| `m6` | `BO-H2` `in_c`: A rows only, `S_add` dropped | review mutant, survived before |
| `m7` | `BO-H1` pre-seam run filters on `poc == "pre"` | review mutant, survived before |
| `m8` | `holm`: `x["reject"] = True` | review mutant, survived before |
| `m9` | `missing_bars` gate always PASS | new `RV-1` guard |
| `m10` | full mode ignores failed gates (`if failed:` → `if False:`) | new `RV-2` STOP |
| `m11` | `--run 2 --from` before the run-1 cut allowed | new `RV-2` pin |
| `m12` | the `ALL_LABELS` fix removed | `RV-6` |
| `m13` | `settings_version` passes when no version is pinned (run 2) | new `RV-2` gate |

### `H-2` — default-mode parity, complete cache weeks

Old binary: `git archive 5192b2b` extracted into the scratchpad, built with `-o <S>/old-bin`. New binary: the rebuilt project. Each run had its own copy of the `backtest_data/burst-outcome-read` weeks 2026-07-06 → 2026-09-21 (all complete: last bar = Saturday 00:00) plus its funding file. Command: `dotnet <dll> --root . --fetch aws_fetch/20260924-084613 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --cache <copy> --out <file>`. Diff only (actual):

```
3c3
< - Run at (UTC): 2026-10-02 13:05:52
---
> - Run at (UTC): 2026-10-02 13:05:55
diff exit=1
   507 .../h1-old.md
   507 .../h1-new.md
```

⚠ **Do not repeat the review's `H-1` recipe with the `backtest_data/q1d-tier-geometry` cache.** Its week 2026-09-21 ends at `2026-09-24 17:53`, so the new binary re-fetches it and the diff will show the change. That is the fix working, not a parity break.

### `H-2b` — `diagexport` parity (actual)

```
- Population rows: 10520. Rows without a logged row (must be 0): 0.
- Split date 2026-08-17 00:00 UTC: H1 = 29 trading days, H2 = 29 (the stability read's rule).
- Population rows: 10520. Rows without a logged row (must be 0): 0.
- Split date 2026-08-17 00:00 UTC: H1 = 29 trading days, H2 = 29 (the stability read's rule).
219dac4a5ecbb065554f4e4d61f0421d *.../dx-old.csv
219dac4a5ecbb065554f4e4d61f0421d *.../dx-new.csv
```

### `H-3` — power count vs counts-only, fresh empty cache

Export: `dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport --fetch aws_fetch/20260928-121255 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --cache <empty folder> --out <folder>/diagnosis-rows-20260928.csv`. Console tail (actual):

```
- Population rows: 11001. Rows without a logged row (must be 0): 0.
- Split date 2026-08-18 00:00 UTC: H1 = 30 trading days, H2 = 30 (the stability read's rule).
3fc4f716012481cb7132e9583cde5d18 *<S>/cache-fresh-0928/diagnosis-rows-20260928.csv
3fc4f716012481cb7132e9583cde5d18 *backtest_data/burst-outcome-read/diagnosis-rows-20260928.csv
```

The fresh-cache export is byte-identical to the build's export (and to the review's `H-2` MD5). Then `powershell -NoProfile -File tools/ops/burst-outcome-power-count.ps1 -FetchFolder aws_fetch\20260928-121255 -RunDate 2026-11-02` and `python tools/ops/burst_outcome_read.py --counts-only --run 1 --export <that csv> --fetch aws_fetch/20260928-121255 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --out <S>/counts-0928.md`. Table comparison by a scratch script that cuts each 19-line table (actual):

```
lines: power 19 2a 19 2b 19 2c 19
2a == fresh power count section 1: True
2b == fresh power count section 1: True
2c rows with any non-zero count: 0
```

Counts-only lines (actual):

```
- Data window: Timestamp < 2026-11-25 00:00:00 
- Run: --run 1; seed 20260928; resamples 10000.
- Export rows inside the window: 11001. Excluded, by reason:
  - ASIA before arming 2026-08-01 19:02:31: 329
  - AggrVelBurstRatio empty: 54
  - not in the fetch books (pre-collector or pooled-only row): 1527
- Analysis population (directional, valid levels, armed, ratio present, in the fetch books): 9091 rows, first 2026-07-22 16:31:01, last 2026-09-28 12:12:01.
- Coverage (MainMissingBars > 0 = the main-window walk lacks at least one 1-minute bar; a coverage column, not an outcome). Population rows, by session and arm: ASIA A 0 B 0 C 0 O 0  LONDON A 0 B 0 C 0 O 0  NY A 0 B 0 C 0 O 0. Total 0; MainMissingBars not parseable: 0.
- Pre-outcome gates (full mode STOPs on any FAIL before an outcome column is opened; counts-only prints them and goes on):
  - PASS missing_bars: population rows with MainMissingBars > 0 or not parseable = 0 (must be 0)
  - PASS extra: analysis rows not in the power-count population = 0 (must be 0)
  - PASS arm_diff: rows in both with a different arm, shadow arm or fifth = 0 (must be 0)
  - PASS session_mismatch: export Session differs from the hour's session = 0 (must be 0)
  - PASS signal_mismatch: export AggrVelSignal differs from the fetch book = 0 (must be 0)
  - PASS verdict_differs: export rows dropped as 'verdict differs between export and fetch book' = 0 (must be 0)
  - PASS side_tier_inconsistent: export rows dropped as 'Side/Tier inconsistent with Verdict' = 0 (must be 0)
  - PASS settings_version: settings.json version 69, pinned 69 (run 1)
COUNTS ONLY: exiting before any outcome column is opened.
```

### `H-4` — the `RV-1` scenario, measured

Two copies of `backtest_data/burst-outcome-read` (the cache the old run recipe named; its week 2026-09-28 ends at `2026-09-29 14:03`). `diagexport` on `aws_fetch/20261002-121123`, old binary on one copy, new binary on the other, then counts-only `--run 1` on each export. Actual:

```
old week 09-28: first 2026-09-28 00:01 last 2026-09-29 14:03 lines 2284
- Population rows: 11892. Rows without a logged row (must be 0): 0.
new week 09-28: first 2026-09-28 00:01 last 2026-10-02 13:08 lines 6549
- Population rows: 11892. Rows without a logged row (must be 0): 0.

== old
- Analysis population (...): 9971 rows, first 2026-07-22 16:31:01, last 2026-10-02 11:39:05.
- Coverage (...): ASIA A 3 B 74 C 0 O 86  LONDON A 2 B 56 C 0 O 38  NY A 3 B 185 C 0 O 184. Total 631; MainMissingBars not parseable: 0.
  - FAIL missing_bars: population rows with MainMissingBars > 0 or not parseable = 631 (must be 0)
== new
- Coverage (...): ASIA A 0 B 0 C 0 O 0  LONDON A 0 B 0 C 0 O 0  NY A 0 B 0 C 0 O 0. Total 0; MainMissingBars not parseable: 0.
```

A scratch count over the export (`Timestamp` and `MainMissingBars` only): old export, 638 rows with `MainMissingBars` > 0, first `2026-09-29 14:03:01`, last `2026-10-02 11:39:05`; new export, 0. **This is the review's failure scenario, now measured:** every row after the partial week's last bar had missing bars, 8 of them arm A, and before this fix nothing reported it.

### `H-5` — full mode refuses unpinned runs (real paths, exits at argument parsing)

```
burst_outcome_read.py: error: full mode runs only as --run 1, or --run 2 --from <ts> (docs/burst-outcome-read-spec.md section 6.1)
exit=2
burst_outcome_read.py: error: --run 1 pins the window (Timestamp < 2026-11-25 00:00:00); do not pass --cut-before or --from
```

### `H-6` — build, gate, diff stat

`dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release -t:Rebuild` (actual tail):

```
    0 Warning(s)
    0 Error(s)
```

`powershell -NoProfile -File tools/checks/verify-gate.ps1`, run after the tools commit `08ca2ed` (actual tail):

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
```

`git show --stat 08ca2ed` (actual tail):

```
 tools/ops/SwingFallbackRead/SwingFallbackRead.vb |  20 +-
 tools/ops/burst_outcome_read.py                  | 438 ++++++++++++++++++++---
 2 files changed, 396 insertions(+), 62 deletions(-)
```

⚠ Other seats committed while this ran (`1984dd9`, `ccadf7c`: `CLAUDE.md` and docs only). A plain `git diff --stat 5192b2b HEAD` lists their files too; scope to `-- tools` or use `git show --stat 08ca2ed`. ⚠ The gate does not build `SwingFallbackRead.vbproj` or run the Python selftest; the build line above and `H-1` are the evidence for those.

---

## 2. Decisions I made (auto-proceeded, one line each)

All are tools-only and undone by one revert. None touches scoring, `settings.json`, a rendered engine value, the collector or a CSV header of `analysis_log.csv`.

| # | Decision | Options | Pick | Why |
|---|---|---|---|---|
| 1 | Re-fetch condition for a cached week | (a) last bar < week close AND < now · (b) also re-fetch weeks with internal gaps | **(a)**, the brief's rule | Internal gaps are genuine venue gaps; a re-fetch returns the same. The `missing_bars` gate catches them on population rows. Cost: a week whose final bar Deribit never had is re-fetched every run (same data, slower) |
| 2 | What a gate failure does in full mode | (a) STOP before `run_full`, exit 2, failed names listed, output file written · (b) STOP without writing | **(a)** | Records more: the counts and the STOP line land in the output file |
| 3 | Gate failures reported | first failure · all failures | **All**, in one STOP | Records more |
| 4 | `MainMissingBars` not parseable | ignore · count as a failure | **Failure** | A blank coverage value cannot prove coverage |
| 5 | Run-2 settings version | (a) not gated · (b) gate FAILS until a version is pinned | **(b)** | Step 1 of the three-step test (`CLAUDE.md`, the auto-proceed ruling): (b) guarantees more. Run 2 cannot run before the `RV-4` rider anyway (its session filter). `--run 2 --from` is still accepted, as the brief asks; only its full read STOPs. No run-1 effect |
| 6 | `--run 2` window | free · `--from` required and ≥ 2026-11-25, no `--cut-before` | **Required, ≥ the cut, no upper cut** | Keeps the runs disjoint (`docs/burst-outcome-read-spec.md` §6.1 box: "pre-registered and disjoint"). The spec gives run 2 no end. `RV-4` may revisit |
| 7 | Where the pins live | `main()` only · `main()` plus a re-check in `run()` | **Both** | A caller that bypasses `main()` (the selftest, a future script) still cannot start an unpinned full read |
| 8 | Selftest settings | real `settings.json` · a copy with `version` forced to the pin | **Copy** | The selftest tests code; the real version is checked by the real run's gate. Otherwise a future version bump fails the selftest for a non-code reason. The gate variant sets version 70 to prove the gate |
| 9 | `C24MissingBars` | move with `MainMissingBars` · stay forbidden | **Stay forbidden** | The read uses only the main window |
| 10 | Counts-only with `--run 1` | refuse · accept and apply the pins | **Accept** | Gives the run-1 seat a dry run on the exact window, with every gate printed |

---

## 3. Queued for the trader (not decided)

> ✅ **RULED 2026-10-02 (trader):** `RVF-1` = **(a)** keep the STOP · `RVF-2` = **(b)** add both gates (a small tools follow-up, not yet built). ✅ **`RVF-3` = (c) RULED 2026-10-02 (trader)** — the gate stays on outcome-relevant bars; a full-window coverage column is added as information only (counts-only prints it per session × arm; never a gate). Built together with the `RVF-2` gates in one small follow-up, held until the A4 session-1 agent finishes. History: harness 6 (decision-bias tripwire) flagged the orchestrator's (a) read as `gives_up_for_economy`, 5 of 5 samples ([`harness-runs/decision-bias-20261002T1520Z-jev.json`](harness-runs/decision-bias-20261002T1520Z-jev.json)). The orchestrator's proposed option (c): keep the gate on outcome-relevant bars (a), and add a full-window coverage column as information only, printed in counts-only and never a gate.

| ID (this doc) | Question | Options | My read (a hypothesis) |
|---|---|---|---|
| `RVF-1` | A genuine venue gap inside a population row's main window now STOPs run 1. Today: 0 such rows on `aws_fetch/20260928-121255` (fresh cache) and on `aws_fetch/20261002-121123` (re-fetched week). A Deribit candle gap after today could still land one | (a) STOP and ask, as built · (b) pre-register now: drop rows with `MainMissingBars` > 0, count them per arm, and go on | **(a)**. A STOP happens before any outcome is opened, so ruling it then does not contaminate run 1. (b) is cheaper at run time but adds a rule to a pre-registered read without a case in hand. ⚠ If the trader prefers no run-1 stall, (b) must be written into `docs/burst-outcome-read-spec.md` §5 before 2026-11-25 |
| `RVF-2` | Two drop reasons are not gated: "not directional (the export should hold none)" and "in the export, dropped for no recorded reason" | (a) leave · (b) add both as gates | **(b)**. Both should be 0 and both can only stop, never relabel. Not built: the brief listed the gates exactly |
| `RVF-3` | `MainMissingBars` is not a pure coverage count: the walk stops at resolution, so a gap after an early resolution is not counted | (a) accept, as built · (b) add a full-window coverage column to the export | **(a)**. The gate needs it to be 0 everywhere, so it can only reveal "all zero" or a count that stops the run. (b) changes `MediumTierDiagnosisExport.vb` output columns; out of scope |

Riders carried, unchanged: `RV-4` (review finding 4: `--session` filter and the daylight-saving session hours, audit row `F5`) must now also pin run 2's settings version (decision 5 above).

---

## 4. Feedback on the review and the spec

- **The review's `RV-1` scenario was exact.** Measured in `H-4`: the first row with missing bars is `2026-09-29 14:03:01`, one minute after the cache's last bar.
- **The review's `H-1` recipe now breaks by design.** It copied the `backtest_data/q1d-tier-geometry` cache, whose last week is partial. A parity check must use complete weeks (`H-2` above).
- **The review's mutant `m7` needed a new data shape, not a new assertion.** In every old case each pre-POC row is also pre-seam. The `const` case puts A negative only between the POC edge and the seam, so the two filters give +2 and −2.2. Same lesson as the memory "a fixture's shape must admit the failure".
- **Holm and `TB-D5` are now tested at the unit level**, not through the bootstrap. Bootstrap p values in the effect case are tiny, so no end-to-end case can tell α_i from 0.05.
- **`docs/burst-outcome-read-spec.md` §6.1 run-2 rows** say nothing about which settings version run 2 reads. That gap is now visible as a gate FAIL, not a silent pass.

---

## 5. What I did not verify

- **Any real outcome, and the full mode on real data.** By design.
- **The worktree procedure** (`docs/burst-outcome-read-spec.md` §6.1). Written, not exercised.
- **A re-fetch of a past week whose last bar Deribit lacks.** Decision 1's cost is reasoned from the code, not observed.
- **`LoadBarsAsync` behaviour when the re-fetch fails.** It throws "candle fetch failed … STOP", as before for a missing week. Not exercised.
- **The other `SwingFallbackRead` modes.** `census` and `stability` call `LoadBarsAsync` (they branch after it, `SwingFallbackRead.vb` "---- walks"), so they also re-fetch partial weeks now; on a complete cache the code path is the one `H-2` covers. `liqflag`, `pocgate` and `rescore` return before `LoadBarsAsync` and are untouched. Read from the code; none was run.
- **The solution build.** Only `SwingFallbackRead.vbproj` was built; the gate builds its own harness.
- **Python `float()` vs .NET parsing on `nan`/`inf`.** Carried from the build packet. Not checked.
