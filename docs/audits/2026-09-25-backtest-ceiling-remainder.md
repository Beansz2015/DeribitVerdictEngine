# Audit: backtest / ceiling-audit remainder (tree at `6e74181`)

**Date:** written 2026-09-28 (UTC), filed under the 2026-09-25 audit series · **Tree:** `6e74181` in a
worktree, nothing newer · **Posture:** hostile review, no code changed · **Proofs:**
[`proofs/backtest-ceiling-remainder/`](proofs/backtest-ceiling-remainder/README.md)

## 0. Scope, and what was not read

**In-scope files and ranges not read in full: none.** Every in-scope item was read in full at
`6e74181`: all of `tools/BacktestRunner/CoverageReport.vb` (2,052 lines, including the parts the
first audit had read, because the trace needs them); `tools/CeilingAudit/FeatureMatrix.vb`,
`L2Logistic.vb`, `AuditReport.vb` and `CeilingAuditProgram.vb`, all four in full;
`tools/CeilingAudit/CsvFeatureBuilder.vb` in full; all of `tools/WhatIfRunner/WhatIfReport.vb`;
and all five files in `tools/WhatIfRunner/overlays/`.

**Out-of-scope context read only in part, so claims that lean on them are bounded by the
excerpt:** `tools/CeilingAudit/AuditMetrics.vb` (read in full, but it is the first audit's
territory); `tools/BacktestRunner/BacktestProgram.vb` (lines 300–434 plus the argument
parser); `tools/WhatIfRunner/WhatIfProgram.vb` (lines 100–262); `WhatIfOverlay.vb` (the
whitelist and `ExpandSweep`); `WhatIfSettings.vb` and `WhatIfReplay.vb` (grep excerpts);
`analysis/FailureRateMatrix.vb` (`CanonicalTier` and the placed-barrier fallbacks);
`Core/VenueStatusLog.vb` (lines 1–160); `Core/WsHealthLog.vb`; `Core/CaptureMarkerLog.vb`;
`Core/TradeStoreWriter.vb` (the format, parse and dedup code); `Core/StoreFiles.vb` (the grid and
count helpers); `Core/SignalEmitter.vb` (the buffer and POC paths); `AnalysisLogger.vb` (header,
rotation and `LogRun`); `UI/MainForm_Analysis.vb` (lines 60–215 and 740–800).

**Not read at all:**
- The first audit, `docs/audits/2026-09-24-backtest-whatif-ceilingaudit.md`. It is absent from the
  tree at `6e74181` and from this branch. F1–F12 are known here only from the one-line summaries in
  the brief, so possible overlaps are flagged in §3 rather than silently dropped.
- The CLAUDE.md session-start documents (`DeribitIndicatorProject.md`, `architecture.md`,
  `trader-profile.md`, `trader-tick-queue.md`). This is a code audit of tools. No finding relies
  on those documents.

## 1. Scratchpad

```
--- LOGIC TRACE ---
```

**Case.** Wednesday 2026-09-16, hour 10:00–11:00 UTC, BTC-PERPETUAL. Price falls 1.5 % linearly
across the hour. Trade capture is out from 10:07 to 10:52. Hour 10 UTC is in the LONDON bucket
(`session_volume` LONDON 8–12), so live rows are at exec resolution 3, with runs at each 3-minute
close under `trigger_mode: on_close`. That puts them in the **LONDON×3** CeilingAudit population
(indicative), not NY×1 (decisive). The outage has three possible causes, and they diverge:

**(a) WS socket down, process alive** (fixture S-A). `ws_fallback_to_rest: true`, so each run
falls back to REST and still writes an `analysis_log.csv` row. `ws_health.log` gets
`DOWN 10:07` / `OK 10:52`.
- *Evidence parse.* Analysis rows are `CaptureCapable` (`CoverageReport.vb:424-426`), so one
  up-interval covers the whole hour.
- *Store.* Rows 10:00–10:07 and 10:52–11:00. `trade_seq` jumps by 540.
- *Classification.* No marker inside the hour, so the whole-hour path runs. Every row carries a
  sequence, so `ClassifySpan` takes the sequence arm (`:783-795`). The jump sits inside the hour,
  so `storeClean = False`. The uptime read is `up`, and it is not a startup window, so the hour is
  **Defect** `gap-breach(2705000ms)`.
- *Exit.* `--strict` exits **1**. **Correct.** If the in-app gap repair backfilled the hole
  first, the hour reads Captured, and that is also correct.

**(b) The process dies at 10:07 and a new instance starts at 10:52** (fixture S-B). The new
process writes a `capture_marker.log` line at 10:52, so `ClassifyHour` splits the hour.
- *Span [10:00, 10:52).* Rows run to 10:07 and their sequences are contiguous, so the span is
  clean and reads **TrailingEdge** (45 minutes).
- *Span [10:52, 11:00).* The 45-minute gap is charged here, but the sequence arm ignores
  `LongestGapMs`. The span's own sequences are contiguous, so it reads **Captured**.
- *Hour.* **TrailingEdge.** The VERDICT line reads `0 defect hour(s) + 1 trailing-edge hour(s)`.
  The same console prints `seq gaps (local) *** 546 TRADE(S) MISSING ***`, but neither the
  VERDICT nor the exit code reads that count. `--strict` exits **0** (R-2, R-8).

**(c) The flush load draws one `VENUE_503` at 10:06, just before (b)** (fixture S-D1). The window
opens at 10:06. Process A is dead, and process B never saw a venue error, so B's `_lastState` is
`Nothing` and it never writes `VENUE_OK`. The window stays open to `--to`.
- Hour 10 itself is not excused, because its start (10:00) is before the window opens.
- **Every later hour is `OutOfScopeVenue`.** A real 20-minute capture defect at 12:10 disappears.
  The hour is dropped from the markdown table and `--strict` exits **0** (R-1).

**(d) Variant: the hole straddles the hour boundary, 09:56–10:50** (fixture S-C). Hour 9 trails
by 4 minutes, which is under the 5-minute gap, so it reads Captured. Hour 10's sequences are
contiguous within the hour, so it also reads **Captured**. The report says **`VERDICT: clean`**
and exits 0 while it prints 648 missing trades (R-2).

**CeilingAudit.** It never reads the coverage result, the trade store, `ws_health.log` or
`venue_status.log`. Coverage gates nothing here: the only input is `analysis_log.csv` plus 1m OHLC
fetched from Deribit.
- **Rows in.** In (a), every row is written (REST fallback) and survives `LoadAndBuild`: weekday,
  directional, and B's 60 s–180 s cadence clears the 45 s burst floor. In (b), there are no rows
  from 10:07 to 10:52, about 15 LONDON rows at 3-minute cadence. A process killed mid-write leaves
  a truncated 10:07 row, which is **admitted** with `HasPlaced = True`, placed levels of 0 and
  every field past the cut empty or NaN. `AttachLabels` then labels it through the swing/ATR
  fallback geometry (P-CA4, R-9).
- **Labels.** The forward bars come from Deribit OHLC, not our capture, so the 10:00–10:06 rows'
  45-minute windows (resolution 3 → 45 min) cover the real flush and are labelled on the true path.
- **Features carried.** In (a), everything comes from REST and is complete. *Not verified:*
  whether the WS-fed `MarketState` accumulators (aggressor velocity, OFI momentum) go stale while
  runs fall back to REST. AggrVel is un-armed in LONDON×3, but armed and in X for NY×1. In (b), the
  post-restart rows from 10:53 carry cold-start state; I did not verify which indicators need
  warm-up. Neither is flagged. Missing numerics become NaN, then the train median plus a
  `_MISSING` column, and cold zeros (for example `SpreadBps` = 0.0) enter as real values.
- **Is the fit or the AUC comparison biased by the outage?** Only slightly, through admitted rows,
  and not in the paired comparison itself:
  - Baseline and challenger are scored on identical rows, so dropping the flush core removes the
    same rows from both AUCs.
  - It does make the sample missing-not-at-random: the highest-volatility rows of a flush are the
    ones that vanish, and hour 10's (date, hour) bootstrap block shrinks from about 20 rows to
    about 5.
  - The admitted truncated row and the cold post-restart rows are a few rows per crash, which is
    small.
- **What does bias the comparison, by construction and on every run, is R-4.** The challenger's
  design matrix has no side. Every directional state is one-hot BULLISH/BEARISH across pooled
  LONG and SHORT rows. With a symmetric side mix those columns carry no information, so the
  challenger loses to the pipeline and the one-sided verdict rule (R-5) prints **CEILING
  DECLARED**. On synthetic data where real headroom exists (P-CA1), the shipped encoding scores
  AUC 0.443 against a baseline of 0.681, verdict CEILING DECLARED. The same rows side-aligned score
  0.813, verdict B1 PRIZE MEASURED, CI [+0.058, +0.216]. That effect is two orders of magnitude
  larger than anything the outage does.

## 2. Findings, most severe first

Nothing here reaches S0 or S1. These tools place no orders; their output feeds settings
decisions. "Proven" means a run I did, with output in the proofs folder. "Read" means read from
code only.

### R-1 · S2 · PROVEN — a venue window opened before a restart is never closed, so one `VENUE_503` excuses every later hour

- **LOCATION:** `tools/BacktestRunner/CoverageReport.vb:491-512` (`ParseVenueWindows`: windows
  close only on `VENUE_OK` or the next `VENUE_*` line, `instance_id` is ignored, and an
  unterminated window runs to `rangeEndMs`); `:1767` (`rangeEndMs` = the requested `--to`, not
  the evidence boundary). Root cause in the writer: `Core/VenueStatusLog.vb:67-78`
  (`RecordOkIfRecovery` is a no-op when `_lastState Is Nothing`, which is always true in a fresh
  process).
- **DOWNSTREAM IMPACT:** Hours inside the window skip every uptime and store test
  (`ClassifyHour` `:902-913`), are omitted from the markdown "Non-captured hours" table
  (`:2042-2045`), and never count toward `--strict` (`BacktestProgram.vb:425`). A copy-back
  certified this way feeds replay and calibration with unmarked holes.
- **FAILURE SCENARIO:** S-D0 vs S-D1. Both have the same store: a restart at 10:52 and a real WS
  hole at 12:10–12:30. Adding the single line `2026-09-16T10:06:00.000Z | VENUE_503 | <A>` moves
  12:00 from **Defect** to **OutOfScopeVenue** and moves `--strict` from **1** to **0**. The hour
  vanishes from the markdown table.
- **ANALYTICAL CRITIQUE:** The venue log is a per-process transition log with no start line
  (documented at `VenueStatusLog.vb:24-26`), and the parser treats it as a global state machine.
  The trigger is the most correlated pair in this system: venue 5xx under flush load, and a
  restart or crash during the same incident. The B-1/B-2 guard exists so that our faults never
  excuse themselves, and this path lets a two-second venue error excuse days of our faults. A
  window needs to close at the opening instance's last evidence, or at the next process start.

### R-2 · S2 · PROVEN — outages split by an hour boundary or a restart marker read Captured or TrailingEdge, and the one counter that sees them is excluded from the VERDICT and the exit code

- **LOCATION:** `CoverageReport.vb:2011-2025` (the clean gate reads Defect, TrailingEdge,
  candles, funding and venue, but **not** `SequenceGaps.MissingCount`); `BacktestProgram.vb:423-426`
  (`--strict`). The mechanism sits in the ranges the first audit read: `:783-795` (an
  all-sequenced span is judged on contiguity **within the span** and ignores `LongestGapMs`) and
  `:672` / `:723` (sequences are bucketed per hour or per span, so the missing numbers between one
  bucket's last sequence and the next bucket's first are never checked).
- **DOWNSTREAM IMPACT:** An hour with 50+ minutes of missing tape can pass as Captured. `BacktestRunner`
  replay has no coverage gate of its own (first audit, F5), so an operator using `--strict` as the
  gate lets it through.
- **FAILURE SCENARIO:**
  - S-C: a 54-minute hole from 09:56 to 10:50. Hours 9 and 10 both read Captured,
    **`VERDICT: clean`**, exit 0, while the same output prints `*** 648 TRADE(S) MISSING ***`.
  - S-B: a 45-minute crash hole. The hour reads TrailingEdge, exit 0, with 546 missing.
  - Any leading gap of any length in an hour whose previous hour trails by less than 300 s is
    invisible to the hourly classes.
- **ANALYTICAL CRITIQUE:** The F1 trailing-edge design depends on the gap being charged to the
  hour where it ends (header comment, `:14-21`). The C-1 sequence arm then throws that charge
  away, because contiguity inside a bucket says nothing about the bucket's edges. The global walk
  (`AccumulateSequenceGaps`) is the correct instrument and already runs; it just gates nothing.
  ⚠ *Possible overlap with F5 ("hour-start coverage misclassification"). I could not read F5.
  If F5 is this leading-gap mechanism, keep only the VERDICT/exit half.*

### R-3 · S2 · PROVEN — the S1-skip gate tests the file's line count, so a header-only or out-of-range `analysis_log.csv` turns every Defect into ExpectedMissing

- **LOCATION:** `CoverageReport.vb:1792` (`analysisLines.Length = 0`, while the message at `:1794`
  claims "no rows in range"); the effect lands at `:596-597` (no up-intervals, so
  `before-first`, so ExpectedMissing).
- **DOWNSTREAM IMPACT:** The VERDICT reads **clean** and `--strict` exits 0, because ExpectedMissing
  counts for neither.
- **FAILURE SCENARIO:** S-E0 vs S-E1. Both have the same store, a 45-minute hole and no
  `ws_health.log`. Without an `analysis_log.csv` the hour is **Defect** and the exit is 1. Adding
  a file that holds only its header makes the hour **ExpectedMissing**, `VERDICT: clean`, exit 0.
  `AnalysisLogger.EnsureLogFile` (`AnalysisLogger.vb:194-205`) produces exactly that file on
  every schema rotation. The same laundering hits any hour older than the oldest evidence line
  when `ws_health.log` is absent or starts later, which is what happens when you run coverage
  over a range older than a rotated log.
- **ANALYTICAL CRITIQUE:** The file header (`:26-28`) sells `before-first` as the "clean,
  unambiguous" case. It isn't: the hourly walk starts at the store's first trade
  (`ResolveCaptureBeginsUtc`), so the store itself proves capture had started before any hour it
  walks. `before-first` should never outrank that positive evidence.

### R-4 · S2 · PROVEN — the challenger's design matrix is side-blind, so every directional feature is uninformative by construction and the audit declares a ceiling that is not there

- **LOCATION:** `tools/CeilingAudit/FeatureMatrix.vb:46-155` (`FitSchema`) and `:159-195`
  (`Transform`): no column encodes the row's side, and no directional state is oriented to it.
  The input is `CsvFeatureBuilder.vb:322-331` (raw BULLISH/BEARISH strings). It is consumed at
  `CeilingAuditProgram.vb:250-262`.
- **DOWNSTREAM IMPACT:** The §4 decision (`AuditReport.vb:210-213`) is "combination spend stops;
  W6-5/B1 + D3-D6 close as 'no measured headroom'". That is a standing research ruling built on a
  model that cannot express the question.
- **FAILURE SCENARIO:** P-CA1, on the same 3,120 synthetic rows with the same labels and real
  headroom (one signal carries 15× the weight the baseline gives it):

  | Encoding | Challenger AUC | Baseline AUC | ΔAUC CI | Verdict |
  |---|---|---|---|---|
  | Shipped | 0.443 | 0.681 | [−0.346, −0.131] | **CEILING DECLARED** |
  | Side-aligned | 0.813 | 0.681 | [+0.058, +0.216] | **B1 PRIZE MEASURED** |

  The shipped fit gives the dominant signal a coefficient of +0.013.
- **ANALYTICAL CRITIQUE:** With LONG and SHORT pooled, P(success | BULLISH) mixes aligned-LONG
  with counter-SHORT, and under a symmetric side mix it equals P(success | BEARISH). A linear model
  cannot recover the XOR. Whatever the challenger learns comes from non-directional columns
  (ADX, ATR, hour, regime). The baseline is side-aware by construction, because dominant-effective
  is the vote count for the verdict's side. The comparison is rigged toward the pipeline.

### R-5 · S2 · PROVEN — the verdict rule is one-sided: a challenger that is significantly worse, or a CI that straddles zero, declares the ceiling

- **LOCATION:** `AuditReport.vb:227-232` (`CiHigh < margin` gives CEILING DECLARED), which
  contradicts the header at `:4-8` ("INCONCLUSIVE when CI straddles ±0.03").
  `TestSpansSessions` is computed and printed (`:135`) but never gates the verdict.
- **DOWNSTREAM IMPACT:** Any defect that degrades the challenger converts directly into CEILING
  DECLARED: R-4, the under-convergence in R-10, or a feature-matrix bug. It reads as a clean
  negative result.
- **FAILURE SCENARIO:** P-CA2. CIs of [−0.20, −0.10], [−0.05, +0.02] and [+0.01, +0.029] all
  print **CEILING DECLARED**. The first says the challenger is broken, and the second says nothing.
- **ANALYTICAL CRITIQUE:** "The combiner is worse than the pipeline" is not evidence that no
  combiner could be better. An upper bound under zero should flag the challenger as defective,
  not close the workstream.

### R-6 · S2 · PROVEN — the What-If report prints no EV for a single-cell overlay, and never prints the live baseline's EV

- **LOCATION:** `tools/WhatIfRunner/WhatIfReport.vb:94,110` (the ranking section, the only EV
  output, is skipped when `GridCellCount = 1`); `:64-85` (`WhatIfReportModel` has no
  baseline-EV field, although `WhatIfProgram.vb:135` computes `baselineRun` with EV samples).
- **DOWNSTREAM IMPACT:** A pinned candidate, such as "stop_max 2.0" as a registered use case,
  shows only SUCC%/ADV%/EXP% rates. §3b forbids ranking on win rate (`:150`), and win rate is all
  that remains. For swept grids, the winner is never compared with "do nothing": the live value
  shows up, if at all, as an unlabelled row.
- **FAILURE SCENARIO:** P-WI2. A one-cell overlay renders the guard-rails, the population shift and
  the failure matrix, and not one EV number.
- **ANALYTICAL CRITIQUE:** The report's objective function vanishes in exactly the mode used to
  check a single proposed change.

### R-7 · S2 · READ — the "W6-1 LONDON stop" overlay sweeps a global knob and ranks EV pooled across all sessions

- **LOCATION:** `tools/WhatIfRunner/overlays/w61-london-stop-grid.json` (a global
  `scoring.structural_levels.stop_max_atr_mult`); `WhatIfReport.vb:136-137` (the registered use
  case "W6-1 LONDON stop_max 2.0/2.2"); `:159` (ranking on pooled `EvSel`). The whitelist has no
  per-session stop key (`WhatIfOverlay.vb:66-92`), and the README calls this file "the W6-1 LONDON
  stop candidate".
- **DOWNSTREAM IMPACT:** The LONDON decision reads an EV dominated by whichever session has the
  most rows. The only LONDON-specific numbers are SUCC% rates.
- **FAILURE SCENARIO:** A stop_max of 2.2 that improves LONDON×3 EV and costs NY×1 EV, where NY has
  the larger row count, ranks below live. The reverse also happens: an NY-driven gain "wins" a
  LONDON-only proposal.
- **ANALYTICAL CRITIQUE:** The instrument cannot represent the registered change. Per the
  guard-rail at `:136`, it can only run a different change, and the report does not say so. The
  same applies to "LONDON STRONG-only selectivity", since `verdict_strong_pct` is global too.

### R-8 · S3 · PROVEN — `--strict` exits 0 on a report whose own VERDICT is not clean

- **LOCATION:** `BacktestProgram.vb:423-426` (it counts only Defect and the venue verdict) vs
  `CoverageReport.vb:2017` (the clean gate also counts TrailingEdge, candles and funding).
- **DOWNSTREAM IMPACT:** Scripted consumers (the `venue-check.ps1` pattern) see success while a
  human reads `VERDICT: 0 defect hour(s) + 1 trailing-edge hour(s)` or `*** INCOMPLETE ***`
  candles.
- **FAILURE SCENARIO:** S-B: the VERDICT is not clean and the exit is 0.
- **ANALYTICAL CRITIQUE:** Two definitions of "bad" in one command. The machine-read one is the
  weaker.

### R-9 · S3 · PROVEN — short rows are admitted as placed-schema rows with zero placed levels and labelled on fallback geometry

- **LOCATION:** `CsvFeatureBuilder.vb:284` (`row.HasPlaced = hasPlaced`, taken from the **header**,
  not the row); `:467-475` (`TryD` returns 0.0 for a missing field). This defeats the
  `NonV08Excluded` filter at `:195`. `ForwardWindowJoiner.vb:239` repeats the pattern for the
  What-If and report loaders; that file is out of scope, and I noted it only.
- **DOWNSTREAM IMPACT:** A crash-truncated row (trace case b), or a pre-placed-schema block
  concatenated under a v0.8+ header, enters the fit. Its label comes from the swing-else-ATR
  fallback barriers, not the geometry the bridge would have traded, and its features past the cut
  are NaN, imputed to the median with a `_MISSING` flag.
- **FAILURE SCENARIO:** P-CA4. A 66-of-116-field row gives `eligible rows=2`, `NonV08Excluded=0`,
  `HasPlaced=True`, `PlacedTargetShort=0` and `label=1`.
- **ANALYTICAL CRITIQUE:** The repeated-header skip (`:141`) shows the tool expects pooled books,
  but it never re-reads a later header's column set. The cheap guard is a per-row field count.

### R-10 · S3 · PROVEN — the logistic fit is iteration-limited, and the report labels the regulariser as if it were binding

- **LOCATION:** `tools/CeilingAudit/L2Logistic.vb:70-74,83-122` (a fixed 500 epochs with a
  decaying lr and no convergence test); `AuditReport.vb:137` (it prints λ as "selected").
- **DOWNSTREAM IMPACT:** The challenger's test AUC is biased downward, toward CEILING DECLARED
  (through R-5).
- **FAILURE SCENARIO:** P-CA3, on the side-aligned fit: test AUC is 0.8128 at 500 epochs and
  0.8360 at 20,000. The gap of 0.023 is about ¾ of the 0.03 margin. The loss trace is monotone,
  so A39a holds; stability is not the problem.
- **ANALYTICAL CRITIQUE:** The effective regularisation is the epoch budget. Tuning λ over a
  near-flat grid (`AuditMetrics.TuneLambda`, same 500 epochs) is then mostly noise. The magnitude
  here is synthetic; the direction is structural.

### R-11 · S3 · READ — rows dropped at labelling are invisible in the report

- **LOCATION:** `CeilingAuditProgram.vb:202` sets `NRowsTotal`; `AuditReport.vb:28` declares it;
  nothing renders it. The table shows only `NRowsLabelled` (`:100-107`).
- **DOWNSTREAM IMPACT:** `AttachLabels` drops rows for ATR ≤ 0, no forward bars, or the min-move
  gate evaluated at **today's** `EffectiveMinMovePct`. A population that loses half its rows to a
  fee-floor change shows only a smaller N.
- **FAILURE SCENARIO:** A book spanning the 2026-08-01 fee change re-gates pre-change rows with the
  post-change floor. The report cannot show how many rows that removed.
- **ANALYTICAL CRITIQUE:** The §1 table accounts for every load-stage exclusion, then stops
  accounting at the stage most sensitive to settings drift.

### R-12 · S3 · READ — the coefficient table ranks incomparable numbers, and §4 scopes W6-5 by that ranking

- **LOCATION:** `AuditReport.vb:170-185` (sort by |coef|, top 30) and `:216` ("scoped to the
  top-|coef| features"); `FeatureMatrix.vb:140-148` (the dropped reference level is the
  ordinal-first string).
- **DOWNSTREAM IMPACT:** One-hot coefficients are relative to an alphabetically chosen reference;
  numeric coefficients are per SD; `_MISSING` columns are collinear across fields that go missing
  together. Change the reference level and the ranking changes while the predictions do not.
- **FAILURE SCENARIO:** Renaming a level from "BEARISH" to "SHORT_BIAS" moves which level is
  dropped, and so moves which feature tops the W6-5 scope list.
- **ANALYTICAL CRITIQUE:** |coef| is not feature importance here. A drop-column or permutation
  ΔAUC would be.

### R-13 · S3 · READ — the informational Absorption/AggrVel table ignores the row's side, and the comments claim otherwise

- **LOCATION:** `CeilingAuditProgram.vb:300-315` (ABOVE is +1 and BELOW is −1 on LONG and SHORT
  rows alike; the comment says "|sign|-adjusted"); `:336-351` (NaN is replaced with 0.0; the
  comment says "median-imputed"; `AbsorptionLevel` is a raw price); `:355-366` (BURST_BUY is +1
  regardless of side).
- **DOWNSTREAM IMPACT:** This table is the only evidence the audit gives on whether to arm
  Absorption (`scoring_enabled:false`) or AggrVel outside NY. With pooled sides, a real aligned
  effect cancels towards AUC 0.5: the R-4 mechanism in miniature.
- **FAILURE SCENARIO:** Absorption below price predicts LONG success and absorption above predicts
  SHORT success, symmetrically. The table prints about 0.50.
- **ANALYTICAL CRITIQUE:** It is labelled "not in §4 decision", but it is still the decision input
  for a later arming change.

### R-14 · S3 · PROVEN — the What-If ranking table uses a different key from the winner rule, so a zero-trade cell can sit at rank 1

- **LOCATION:** `WhatIfReport.vb:159` (orders by `EvSel.Mean`, where N = 0 gives 0.0, with no
  tie-break) vs `WhatIfProgram.vb:165-166` (N = 0 gives −∞, then `EvFull`); the comment at `:90`
  and `:160-162` says "winner is always rank 1".
- **DOWNSTREAM IMPACT:** In net-of-fee EV, where most cells are negative, a cell that traded
  nothing on the selection half heads the table. The ◆ winner sits lower. With more than 50 such
  cells the winner can drop off the table entirely (`MaxRankingRows`).
- **FAILURE SCENARIO:** P-WI1. Rank 1 is `stop_max_atr_mult=2.2` with `EV (sel) —`, `n<30`; the
  ◆ winner is at rank 2.
- **ANALYTICAL CRITIQUE:** This sits next to F3 (no minimum n) but is distinct: the program handles
  N = 0 and the renderer does not.

### R-15 · S3 · READ — the geometry overlays sweep arbitration mode on a population filtered by the live ladder's POC outcome

- **LOCATION:** `overlays/geometry-study-36cell.json` and `geometry-4cell-mode-x-pivot.json`
  (`target_arbitration_mode` 0/1); the exclusion is keyed on the logged live `TargetCapReason =
  "poc"` (`WhatIfProgram.vb:102-103`); the replay sets `VPFRPoc = 0` (`WhatIfReplay.vb:60-61`); and
  `WhatIfReport.vb:102` prints "POC-tier rows excluded (unlogged VPFR)".
- **DOWNSTREAM IMPACT:** Under mode 1 (nearest candidate), rows where the live mode-0 ladder picked
  swing or HVN but POC was nearer are kept and replayed without POC. The mode-1 cell's placed
  target is not what live mode 1 would place.
- **FAILURE SCENARIO:** POC sits 0.6×ATR from entry and the swing target 1.1×ATR. Live mode 0 logs
  "swing", so the row is kept. Replayed mode 1 picks HVN or swing, never the POC that live mode 1
  would take.
- **ANALYTICAL CRITIQUE:** The report line reads as though POC were removed from the question.
  It was removed only from mode 0.

### R-16 · S4 · READ, except the ΔAUC nit (proven) — nits

- `WhatIfReport.vb:134`: "mid-price wick touches". The OHLC comes from
  `get_tradingview_chart_data`, so it is trade-price bars (wicks include single liquidation
  prints), not mid.
- `:124,129-133`: "≈ counter × 0.05 false winners at a 95 % bar". The winner is an argmax with no
  95 % test, and the CI is two-sided.
- `overlays/README.md:28`: "Both were run" describes four files.
- `AuditReport.vb:87-88`: prefix-burst rows are counted in both the "prefix" and the "burst-cadence
  (median gap < 45 s)" lines (`CsvFeatureBuilder.vb:178-192`).
- `AuditReport.vb:113,165`: the printed ΔAUC is the bootstrap mean, not test AUC(chal) −
  AUC(base). P-CA1 prints −0.2380 against −0.2378. Small, but the table's own two AUCs do not
  subtract to the printed Δ.
- `CeilingAuditProgram.vb:200`: decisiveness is keyed on the display string `"NY×1"`.
- `CeilingAuditProgram.vb:266-277`: Success@K is computed with baseline ties broken by chronological
  index. `BaselineScore` has at most about 20 distinct values.

## 3. Possible overlap with F1–F12 (the first audit, unread)

- **R-2** may share its mechanism with **F5** ("hour-start coverage misclassification"). The
  VERDICT and exit-code omission of `SequenceGaps` is independent of that.
- **R-14** borders **F3** (no minimum n in winner selection). The defect reported here is the
  renderer's key mismatch, not the selection rule.
- **R-11**'s re-gating at current settings may overlap **F4** ("one fee model for all outcomes") if F4
  covered CeilingAudit's `AttachLabels` as well as the What-If walk.
- The R-9 pattern in `ForwardWindowJoiner.vb:239` may be part of **F12**'s smaller items.

## 4. Not verified

- Whether the WS-fed `MarketState` accumulators (AggrVel, OFI momentum, absorption tracker) go
  stale while runs fall back to REST, and which indicators need warm-up after a restart. This is
  the trace's feature-quality question for cases (a) and (b).
- Whether a real collector book contains truncated rows (R-9). The mechanism is proven; its
  frequency is not.
- The size of R-4 and R-10 on the real book. Both proofs use synthetic data. The direction of R-4
  holds for any side-symmetric mix; the magnitude does not transfer.
- Whether the in-app gap repair would have filled the holes in S-B or S-C before a coverage run.
  If it does, those hours are genuinely Captured, and R-2 narrows to holes the repair could not
  serve (past the roughly 24 h retention, or `HOLE_NOT_SERVED`).
