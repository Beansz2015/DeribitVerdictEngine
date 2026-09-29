# Burst outcome read — tools build (session 1): spec-back

**Reviewer: Opus 5.5, medium.**
- **Why that tier:** the handles below run in minutes and need no judgment. The judgment is in the decisions queued in section 2 of this document, especially `TB-D5` (a label the spec did not write). They are small and are listed with my read.
- **Move up to high** if `H-2` does not reproduce, or if you disagree with `TB-D5` or `TB-D6`. Both change how run 1 reports, and run 1 must not adapt after it sees outcomes.

**Spec:** [`docs/burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §0, §5, §5.2, §6.1, §6.2. **Built at:** start commit `b5b4a7d`. **Tools commit:** `8943324`. **Docs commit:** this file and the queue line.

⛔ **No real outcome was printed, aggregated, sorted, plotted or looked at.** The only real-data runs were the three the brief allowed:
1. `SwingFallbackRead` default mode, old and new binary, for the `H-1` parity diff. I diffed the two output files and printed only the diff. I did not open either file.
2. `SwingFallbackRead --mode diagexport` on `aws_fetch/20260928-121255`, to produce the export. Its console output is the population count and the file path only.
3. `burst_outcome_read.py --counts-only` on that export, three times (no window, `--cut-before 2026-09-01`, `--from 2026-09-01`). That mode reads the export through a column whitelist with no outcome column.

The outcome path ran on synthetic data only (`--selftest`).

---

## 1. Ranked verification handles

All handles run from the repo root at the tools commit. **If you run only one, run `H-2`**: it covers the loader, the join, the exclusions and every arm rule in one table.

| Handle | Command | Expected (pasted below) |
|---|---|---|
| `H-2` | Run the power count, then the counts-only mode (commands below); compare the power count's section 1 with tables 2a and 2b | All three are identical, line for line. The drops count is 0 |
| `H-3` | `python tools/ops/burst_outcome_read.py --selftest` | `SELFTEST PASSED (0 failed)`, both cases |
| `H-1` | Build the start commit and the tools commit; run default mode on `aws_fetch/20260924-084613` with each; `diff` | Only the `Run at (UTC)` line differs |
| `H-4` | `dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release -t:Rebuild`; `powershell -NoProfile -File tools/checks/verify-gate.ps1` | 0 warnings, 0 errors; gate verdict pasted |
| `H-5` | `git diff --stat b5b4a7d 8943324 -- tools`, then `git show --stat` on the docs commit | The two tools and the new script; then this file and the queue line only (another seat's commits are interleaved, see below) |
| `H-6` | The same counts-only command with `--cut-before 2026-09-01T00:00:00Z`, then with `--from 2026-09-01T00:00:00Z` | The populations add to 9,091 and the export rows add to 11,001 |
| `E-1` | Mutation runs of the selftest (scratchpad copies, not committed) | Each of three mutations makes the selftest FAIL |

### `H-2` — arm counts equal the power count

Commands:

```bash
powershell -NoProfile -File tools/ops/burst-outcome-power-count.ps1 -FetchFolder aws_fetch\20260928-121255 -RunDate 2026-11-02
```

```bash
python tools/ops/burst_outcome_read.py --counts-only --export backtest_data/burst-outcome-read/diagnosis-rows-20260928.csv --fetch aws_fetch/20260928-121255 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
```

The export comes from (gitignored, so regenerate it):

```bash
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport --fetch aws_fetch/20260928-121255 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --cache backtest_data/burst-outcome-read --out backtest_data/burst-outcome-read/diagnosis-rows-20260928.csv
```

Export console tail (actual):

```
- Population rows: 11001. Rows without a logged row (must be 0): 0.
- Split date 2026-08-18 00:00 UTC: H1 = 30 trading days, H2 = 30 (the stability read's rule).
```

Power count, section 1 (actual, this seat's run; it also equals the block pasted in `docs/burst-outcome-read-spec.md` §2.2):

```
session fifth    A (S/M/W)        B (S/M/W)          C    O  Oup | A_keep A_drop S_add B_lean | crossed bandMismatch
ASIA    1    13 (1/4/8)       59 (1/12/46)          0   23    3 |     10      3     0      0 |       6     0
ASIA    2    18 (0/7/11)      139 (5/31/103)        0   99    7 |     18      0     5      0 |       6     0
ASIA    3    18 (2/7/9)       220 (12/73/135)       1  156    4 |     18      0    13      1 |       6     0
ASIA    4    18 (5/6/7)       234 (14/76/144)       1  230    4 |     18      0    19      0 |       6     0
ASIA    5    12 (3/4/5)       291 (28/104/159)      0  321    5 |     12      0    21      0 |       5     0
ASIA    all  79 (11/28/40)    943 (60/296/587)      2  829   23 |     76      3    58      1 |      29     0
LONDON  1    19 (2/7/10)      95 (7/30/58)          0   58    3 |     14      5     0      0 |       8     0
LONDON  2    32 (6/18/8)      200 (31/66/103)       0  165   11 |     31      1     0      1 |      16     0
LONDON  3    9 (0/5/4)        169 (13/58/98)        1  150    6 |      9      0    11      0 |       1     0
LONDON  4    14 (3/5/6)       166 (20/61/85)        0  180    4 |     14      0    14      1 |       3     0
LONDON  5    3 (0/2/1)        215 (28/85/102)       1  209    2 |      3      0    20      0 |       0     0
LONDON  all  77 (11/37/29)    845 (99/300/446)      2  762   26 |     71      6    45      2 |      28     0
NY      1    6 (0/1/5)        12 (0/4/8)            0   16    1 |      2      4     0      0 |       1     0
NY      2    35 (2/14/19)     211 (12/61/138)       0  149   14 |     35      0     1      0 |       9     0
NY      3    67 (13/28/26)    624 (50/193/381)      1  528   19 |     67      0    35      0 |      23     0
NY      4    41 (3/21/17)     840 (67/302/471)      0  737   20 |     41      0    64      0 |      15     0
NY      5    20 (3/4/13)      1175 (136/420/619)    0 1090   15 |     20      0   114      1 |       2     0
NY      all  169 (21/68/80)   2862 (265/980/1617)    1 2520   69 |    165      4   214      1 |      50     0
```

`burst_outcome_read.py --counts-only`, table 2b (actual; the analysis population):

```
session fifth    A (S/M/W)        B (S/M/W)          C    O  Oup | A_keep A_drop S_add B_lean | crossed bandMismatch
ASIA    1    13 (1/4/8)       59 (1/12/46)          0   23    3 |     10      3     0      0 |       6     0
ASIA    2    18 (0/7/11)      139 (5/31/103)        0   99    7 |     18      0     5      0 |       6     0
ASIA    3    18 (2/7/9)       220 (12/73/135)       1  156    4 |     18      0    13      1 |       6     0
ASIA    4    18 (5/6/7)       234 (14/76/144)       1  230    4 |     18      0    19      0 |       6     0
ASIA    5    12 (3/4/5)       291 (28/104/159)      0  321    5 |     12      0    21      0 |       5     0
ASIA    all  79 (11/28/40)    943 (60/296/587)      2  829   23 |     76      3    58      1 |      29     0
LONDON  1    19 (2/7/10)      95 (7/30/58)          0   58    3 |     14      5     0      0 |       8     0
LONDON  2    32 (6/18/8)      200 (31/66/103)       0  165   11 |     31      1     0      1 |      16     0
LONDON  3    9 (0/5/4)        169 (13/58/98)        1  150    6 |      9      0    11      0 |       1     0
LONDON  4    14 (3/5/6)       166 (20/61/85)        0  180    4 |     14      0    14      1 |       3     0
LONDON  5    3 (0/2/1)        215 (28/85/102)       1  209    2 |      3      0    20      0 |       0     0
LONDON  all  77 (11/37/29)    845 (99/300/446)      2  762   26 |     71      6    45      2 |      28     0
NY      1    6 (0/1/5)        12 (0/4/8)            0   16    1 |      2      4     0      0 |       1     0
NY      2    35 (2/14/19)     211 (12/61/138)       0  149   14 |     35      0     1      0 |       9     0
NY      3    67 (13/28/26)    624 (50/193/381)      1  528   19 |     67      0    35      0 |      23     0
NY      4    41 (3/21/17)     840 (67/302/471)      0  737   20 |     41      0    64      0 |      15     0
NY      5    20 (3/4/13)      1175 (136/420/619)    0 1090   15 |     20      0   114      1 |       2     0
NY      all  169 (21/68/80)   2862 (265/980/1617)    1 2520   69 |    165      4   214      1 |      50     0
```

Population and exclusion lines of the same run (actual):

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
```

**Arithmetic identity:** 11,001 export rows − 329 − 54 − 1,527 = 9,091 = the power count's population. **Export drops of power-count rows: 0**, so the escalation trigger (a difference larger than the counted drops) cannot fire. Table 2a (the power-count rule re-implemented in Python on the same books) is also identical to the power count, so the Python arm rule reproduces the PowerShell one.

Comparison method: a scratchpad script (not committed) extracted the three 19-line blocks, stripped `\r`, and compared them as lists. Actual output:

```
lines: spec 19 power(fresh) 19 2a 19 2b 19 2c 19
fresh power count section 1 == spec pasted block: True
2a (reference) == fresh power count section 1: True
2b (analysis)  == fresh power count section 1: True
2c (dropped) non-zero rows: 0
```

A reader can repeat it with any diff tool: the counts-only run prints tables 2a, 2b and 2c in the power count's own format for that reason.

### `H-3` — selftest (actual output, tail)

10,000 resamples, 2 min 12 s wall time. The arm-count and exclusion PASS lines are identical in both cases and are cut here after the first case:

```
==== SELFTEST case: effect (A = +5 bps, B = 0, noise sd 10) ====
PASS  counts-only runs on an export that has no outcome column, and returns no outcome
PASS  ASIA arm counts {'A': 300, 'B': 600, 'C': 60, 'O': 60, 'S_add': 60} == constructed {'A': 300, 'B': 600, 'C': 60, 'O': 60, 'S_add': 60}
PASS  LONDON arm counts {'A': 300, 'B': 600, 'C': 60, 'O': 60, 'S_add': 60} == constructed {'A': 300, 'B': 600, 'C': 60, 'O': 60, 'S_add': 60}
PASS  NY arm counts {'A': 300, 'B': 600, 'C': 60, 'O': 60, 'S_add': 60} == constructed {'A': 300, 'B': 600, 'C': 60, 'O': 60, 'S_add': 60}
PASS  2 export rows missing from the books are dropped and counted
PASS  180 ratio-empty rows excluded and counted
PASS  3 ASIA pre-arming rows excluded and counted, in the export and in the power-count reference
PASS  analysis rows are a subset of the power-count rows with identical arms
PASS  1 book row missing from the export is counted as an export drop (got 1)
      ASIA BO-H1 FULL d = +4.9 [+3.6, +6.2]  Holm CI [+3.4, +6.4]  label: CONFIRMED (d > 0)
PASS  ASIA BO-H1 A - B CI excludes 0 and the point is near +5
PASS  ASIA BO-H1 label is CONFIRMED (d > 0)
      LONDON BO-H1 FULL d = +4.7 [+3.4, +6.1]  Holm CI [+3.2, +6.3]  label: CONFIRMED (d > 0)
PASS  LONDON BO-H1 A - B CI excludes 0 and the point is near +5
PASS  LONDON BO-H1 label is CONFIRMED (d > 0)
      NY BO-H1 FULL d = +4.4 [+3.0, +5.8]  Holm CI [+3.0, +5.8]  label: CONFIRMED (d > 0)
PASS  NY BO-H1 A - B CI excludes 0 and the point is near +5
PASS  NY BO-H1 label is CONFIRMED (d > 0)
PASS  BO-H2 table printed

==== SELFTEST case: null (B rows carry their A row's EV) ====
...
      ASIA BO-H1 FULL d = -0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
PASS  ASIA BO-H1 A - B is exactly 0 in every resample
PASS  ASIA BO-H1 label is NO DIFFERENCE SHOWN
      LONDON BO-H1 FULL d = -0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
PASS  LONDON BO-H1 A - B is exactly 0 in every resample
PASS  LONDON BO-H1 label is NO DIFFERENCE SHOWN
      NY BO-H1 FULL d = +0.0 [-0.0, +0.0]  Holm CI [-0.0, +0.0]  label: NO DIFFERENCE SHOWN
PASS  NY BO-H1 A - B is exactly 0 in every resample
PASS  NY BO-H1 label is NO DIFFERENCE SHOWN
PASS  BO-H2 table printed

SELFTEST PASSED (0 failed)
```

Why the null case is exact rather than "probably not significant": every synthetic B row copies the EV of an A row in the same stratum and day. So in every resample the re-weighted B mean equals the A mean, and the CI is [0, 0] with noise present. A null built from independent noise would fail about 1 time in 10 at a fixed seed, and a failing null would say nothing about the code.

⚠ The selftest asserts on BO-H1 only. BO-H2 and BO-H3 run through the same `arm_rw` and `boot`, and the test checks only that their tables print, not their values.

### `H-1` — `SwingFallbackRead` default-mode parity

No default-mode output for `aws_fetch/20260924-084613` is committed (see decision `TB-D1`). So I built the start commit `b5b4a7d` and the tools commit into separate folders. I ran both on that fetch with the same candle cache (copied from `backtest_data/q1d-tier-geometry`), and diffed the two report files. Actual output (scratchpad paths shortened to `...`):

```
3c3
< - Run at (UTC): 2026-09-29 14:00:10
---
> - Run at (UTC): 2026-09-29 14:01:05
diff exit=1
   507 .../h1-old.md
   507 .../h1-new.md
```

To reproduce: `git worktree add ../dve-b5b4a7d b5b4a7d`, then `dotnet build <worktree>/tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release -o <dir>`, and run both DLLs with `--root . --fetch aws_fetch/20260924-084613 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --cache <a copy of the q1d candle cache> --out <file>`.

### `H-4` — build and gate

`dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release -t:Rebuild` (actual tail):

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

`powershell -NoProfile -File tools/checks/verify-gate.ps1`, run after the tools commit `8943324` (actual tail):

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

⚠ The gate ran in its default mode. It does not build `SwingFallbackRead.vbproj` and does not run the Python script, so it covers only "no engine change" here. The project build above is the build evidence.

### `H-5` — diff stat

This build's own changes: `tools/ops/SwingFallbackRead/SwingFallbackRead.vb`, `tools/ops/SwingFallbackRead/MediumTierDiagnosisExport.vb`, `tools/ops/burst_outcome_read.py` (new), `docs/burst-outcome-read-tools-spec-back.md` (new) and `docs/trader-tick-queue.md` (one row), and nothing else.

What I ran and saw (actual). Before the tools commit, `git status --short` printed only the three tool files:

```
 M tools/ops/SwingFallbackRead/MediumTierDiagnosisExport.vb
 M tools/ops/SwingFallbackRead/SwingFallbackRead.vb
?? tools/ops/burst_outcome_read.py
```

The tracked VB part of that change, `git diff --stat` before the commit:

```
 .../SwingFallbackRead/MediumTierDiagnosisExport.vb |  5 ++++-
 tools/ops/SwingFallbackRead/SwingFallbackRead.vb   | 25 ++++++++++++++++------
 2 files changed, 23 insertions(+), 7 deletions(-)
```

⚠ **Another seat committed three docs-only commits while this build ran** (`43d9545`, `b4755e2`, `3d9376b`; files `docs/history-host-and-raw-channel-read-2026-09-28.md`, `docs/trader-tick-queue.md`, `docs/history-data-store-spec.md`). So a plain `git diff --stat b5b4a7d` also lists their files. Scope the handle to this build's commits instead:

```bash
git diff --stat b5b4a7d 8943324 -- tools
```

Actual output:

```
 .../SwingFallbackRead/MediumTierDiagnosisExport.vb |    5 +-
 tools/ops/SwingFallbackRead/SwingFallbackRead.vb   |   25 +-
 tools/ops/burst_outcome_read.py                    | 1015 ++++++++++++++++++++
 3 files changed, 1038 insertions(+), 7 deletions(-)
```

The docs commit holds this file and one row of `docs/trader-tick-queue.md` (`git diff --stat` before that commit: `docs/trader-tick-queue.md | 2 +-`). My row edit sits on top of the other seat's `b4755e2` change to the same file; the working-tree diff showed only my row.

### `H-6` — the window flags split the data

Actual lines (`--cut-before 2026-09-01T00:00:00Z`, then `--from 2026-09-01T00:00:00Z`):

```
- Export rows inside the window: 6699.  ... Analysis population ...: 4813 rows, first 2026-07-22 16:31:01, last 2026-08-31 23:44:02.
- Export rows inside the window: 4302.  ... Analysis population ...: 4278 rows, first 2026-09-01 00:21:08, last 2026-09-28 12:12:01.
```

4,813 + 4,278 = 9,091, and 6,699 + 4,302 = 11,001. Session-pooled arm A: ASIA 44 + 35 = 79, LONDON 61 + 16 = 77, NY 120 + 49 = 169, which equals the unwindowed table.

### `E-1` — mutations (build-time evidence; the mutated copies are in the scratchpad, not committed)

The fixture must be able to fail. Each mutation was applied to a copy of the script, and the copy's selftest was run at 500 resamples:

| Mutation | What it breaks | Selftest |
|---|---|---|
| `m1`: the rotated-book glob returns nothing | Brief trap: the rotated book is not read | FAIL: LONDON and NY arm counts 200 vs 300; export rows "not in books" ≠ 2 |
| `m2`: `arm_rw` does not negate `rw_run`'s B − A | Sign of every A − B | FAIL: BO-H1 CI and label, all sessions |
| `m3`: `S_add` takes `AggrVelNet` against X | Shadow arm definition | FAIL: `S_add` 0 vs 60 |

A first version of the selftest carried a hollow check: the ASIA arm count was compared with itself, and no synthetic ASIA row fell before arming. I found it while reading the mutation output. Fixed before the commit: three synthetic ASIA rows now sit on 2026-07-31, and the check asserts they are excluded in the export and in the reference.

---

## 2. Decisions

One line each: decision · options · pick · why. `TB-D` = "tools build decision", new IDs for this document.

| ID | Decision | Pick and why |
|---|---|---|
| `TB-D1` | `H-1` parity target: none of the committed outputs is a default-mode run on `aws_fetch/20260924-084613`. The one committed default output (`docs/swing-vs-fallback-target-read-2026-09-15-output.md`) is fetch `20260913-153704` at settings v68 | **Old binary vs new binary on the named fetch.** Step 3 of the three-step test: a byte match against the v68 doc is mechanically impossible at v69 (its settings line differs). The binary diff is the stronger check anyway |
| `TB-D2` | Merge order after adding the rotated books | **pooled → rotated books (by rotation stamp) → live, first seen wins.** Same order as the box books' time order. It only matters for a Timestamp in two books; today the export has 0 verdict conflicts with the books |
| `TB-D3` | `--mode diagexport` reads raw columns from pooled + live only | **Add the rotated books to its raw loader too.** Without it, every rotated-only row is "without a logged row" and the export exits 3 |
| `TB-D4` | How to get a Holm-adjusted CI from a percentile bootstrap | **Bootstrap two-sided p = 2 × min tail share; Holm step-down on p; the adjusted CI is the percentile CI of the FULL draws at level 1 − α_i.** A result passes Holm only if the step-down has not stopped AND that CI excludes 0. Needs the draws, so the copied `boot` has ONE added line (`DRAWS.append(draws)`), marked in the source |
| `TB-D6` | Holm family sizes | **BO-H1: m = 3 always; a NOT READABLE session enters with p = 1** (`docs/burst-outcome-read-spec.md` §4: "Three tests"; also the power count's worst-case α/3). **BO-H2: m = number of readable sessions** (§4: "Holm across the sessions that are readable") |
| `TB-D7` | Re-using `rw_run` verbatim for A − B | **B takes rw_run's `MEDIUM` slot (re-weighted), A takes its `WEAK` slot; rw_run returns B_rw − A; `arm_rw` negates the point and swaps the CI ends, then re-runs the copied `label`.** No re-derivation of the statistic. Mutation `m2` proves the negation is load-bearing |
| `TB-D8` | Halves | **Re-computed on the analysis population after the window and every exclusion** (first ⌊D/2⌋ UTC trading days, the export's own rule). The export's `Half` column is computed over its whole population, including 1,527 rows the read drops and rows outside a run's window, so it would split run 1 and run 2 wrongly |
| `TB-D9` | Which labels can gain "EDGE-SENSITIVE" | **Every label except NOT READABLE and NO DIFFERENCE SHOWN**; the test is the sign of the pre-edge FULL point vs the full FULL point. Run 2 has no pre-edge rows and prints "n/a" |
| `TB-D10` | Where the arm fields come from | **TFISignal, AggrVelBurstRatio, AggrVelNet, AggrVelSignal from the fetch books; side, band, ATR and scores from the export.** An export row whose verdict differs from its book row is dropped and counted (0 today) |
| `TB-D11` | Seed | **20260928** (the spec's date), fixed; the selftest uses the same default of 10,000 resamples |
| `TB-D12` | Section 3.1 "explain" columns | **Raw, not re-weighted:** n, success %, gross breakeven = mean of S/(T+S), net breakeven = mean of (S+fee)/(T+S), net edge = success − net breakeven, net EV per trade with a day-bootstrap CI. Descriptive only; net EV decides (`docs/burst-outcome-read-spec.md` §5) |
| `TB-D13` | BO-H3 content | **A − B re-weighted for low / mid / high and for each fifth; `S_add` − (B − `S_add`); crossed A − uncrossed A (raw); A − B for era E1 vs E2–E4** (`docs/burst-outcome-read-spec.md` §6.2 asks for the E1 split). CIs only, no label |

### Queued for the reviewer, with my read

| ID | Decision | Options | My read (hypothesis) |
|---|---|---|---|
| `TB-D5` | What label does a result get when both halves are significant with the same sign, but the full-sample Holm-adjusted CI includes 0? `docs/burst-outcome-read-spec.md` §5 adds the Holm clause to CONFIRMED but does not say what follows when only that clause fails | (a) Literal first-match-wins. The next rule, "H1 FINDING, H2 SAME SIGN NOT SIGNIFICANT", matches, because the copied code checks only that H2 is readable and has the same sign. Its name then says H2 is not significant when it is. (b) A new label: **"NOT CONFIRMED: HALVES AGREE, HOLM-ADJUSTED FULL CI INCLUDES 0 (d > 0 / d < 0)"** | **(b), built.** It is the more truthful option, and (a) prints a false statement. It adds a label to a pre-registered set, so please rule on it before run 1. Reverting to (a) is one `if` in `finalize_label` |

`TB-D5` and `TB-D4` share a root: both exist only because the spec's CONFIRMED rule carries a Holm clause the copied `label` never had.

---

## 3. Feedback on the spec

**What worked.**
- `docs/burst-outcome-read-spec.md` §0 named the exact trap (the concurrent-instance rule drops the rotated book's instances). The build added the book to the box-log set, and mutation `m1` shows how it fails without it.
- Pinning `H-2` to the power count's section 1 made the acceptance check an exact table match, not a judgement.

**Assumptions that broke or were narrower than the words.**
- `docs/burst-outcome-read-spec.md` §5.2 says default mode "reproduces its committed output". No committed default output exists for `aws_fetch/20260924-084613` (`TB-D1`).
- The export drops **no** power-count rows today. The spec expected some ("minus rows the export drops"). The 1,527 rows the export has beyond the power count (pre-collector rows from 2026-07-06, and pooled-only rows) are dropped by the join as "not in the fetch books". So the two populations are identical, not nested.
- `docs/burst-outcome-read-spec.md` §5 "Readable: n ≥ 100 in every group" is applied by the copied `rw_run` to **all** rows of each arm, including A rows in strata with no B row. The read prints how many A rows each test drops for that reason, so a reader can see the gap. I did not change it (copy verbatim).
- `docs/burst-outcome-read-spec.md` §5 label: the CONFIRMED Holm clause has no stated fall-through (`TB-D5`).

**Riders that follow from BUILD 1 but are out of this brief's scope.**
- `--mode census`, `--mode rescore`, `--mode pocgate` and `--mode liqflag` still load raw columns from pooled + live only. On a fetch that has a rotated book, their populations now include rotated-only rows, and their raw look-ups for those rows will miss. On a fetch without a rotated book they are unchanged: the rotated list is empty, so the merge and the box-log set are the same as before, which `H-1` shows for the default mode. **Do not run those four modes on a rotated-book fetch** until their loaders read the rotated books too.

---

## 4. What I did not verify

- **Any real outcome.** By design. The default (full) mode has never run on real data.
- **The four other `SwingFallbackRead` modes** on a rotated-book fetch (see section 3). Not run.
- **`H-1` for `--mode diagexport` on a fetch without a rotated book.** Only default mode was diffed. The diagexport change is the same list threaded through, and it is empty on such a fetch. Checked by reading, not run.
- **Python `float()` vs .NET `Double.TryParse(..., Float)` on odd inputs** (for example `nan`, `inf`). `H-2` shows the two agree on every row of this fetch. They could differ on a future book that holds such a value.
- **`TB-D12` against `docs/DeribitIndicatorProject.md` §5a wording.** I used the breakeven forms the spec paraphrases (§5 of `docs/burst-outcome-read-spec.md`). I did not re-read `docs/DeribitIndicatorProject.md` §5a (the brief said to skip the session-start reads).
- **The full-mode run time on real data.** Measured only on the selftest: about 3,000 population rows per case over 60 days, both cases in 2 min 12 s at 10,000 resamples. The real run has about 9,000 rows over about 50 trading days in these books, and more strata. I expect minutes, not hours. Not measured.
- **The solution build.** `DeribitVerdictEngine.sln` does not reference `SwingFallbackRead.vbproj`, so only the project was built.
