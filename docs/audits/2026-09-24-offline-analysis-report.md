# Audit — offline analysis report (2026-09-24)

**Scope:** `analysis/AnalysisRunner.vb`, `analysis/MarkdownReportWriter.vb`, `analysis/AnalysisReport.vb`,
`analysis/OutlierAudit.vb`, `analysis/FundingMomentumDiagnostic.vb`, `analysis/DeribitOhlcFetcher.vb`,
`analysis/AnalysisConstants.vb`, `analysis/AnalysisReportForm.vb`. Audited at commit `6e74181`.

**Coverage:** all eight files in scope were read in full. No file was left uncovered.
Also read for the trace (not in scope, not audited in their own right): `analysis/FailureRateMatrix.vb`,
`analysis/ForwardWindowJoiner.vb`, parts of `analysis/BandLadder.vb`, `AnalysisLogger.vb` (row write),
`Core/ScoringEngine_Calculate_Verdict.vb` (verdict strings), `Core/Settings/EngineSettings.vb` (multiplier
defaults), `settings.json` (v68).

**Limits of this audit, stated up front:**
- No .NET SDK in the audit container, so no VB was compiled or run.
- No `analysis_log.csv` in the tree. The trace runs on a **synthetic** flush day through a Python replica
  of the pipeline — see [`proofs/offline-analysis-report/`](proofs/offline-analysis-report/). Its numbers
  prove mechanisms, not live-book figures.
- The session-start read of `docs/DeribitIndicatorProject.md` was skipped; targeted greps only.
- No repo code was changed. Every fix below moves a rendered value, which is a reserved class.

---

--- LOGIC TRACE ---

**Caveats first.** There's no .NET SDK in this container and no CSV book in the tree, so I couldn't run the VB. I wrote a Python replica of the pipeline, following the code branch by branch, and fed it a synthetic flush day. The script is at `proofs/offline-analysis-report/trace.py`. It covers `PopulateForwardBars`, `FailureRateMatrix.Compute`, `WalkBars`, `WilsonCI`, `ComputeContextOutcomes` and `ComputeOiCvdAsymmetry`. Treat its numbers as proofs of mechanism, not as figures from your real book. I also skipped the 29K-token session-start doc and only grepped the parts I needed.

**Setup.** 100 rows, one per minute from 13:00:07 UTC on a Wednesday (NY, res=1). Each row is logged at `DateTime.UtcNow` (`AnalysisLogger.vb:237`), and `Price` is the close of the still-forming bar (`MainForm_Analysis.vb:219`). ATR is 45 and entry is about 60,000. A 3-bar cascade at minutes 40–42 is followed by a V bounce. Placed levels are target 1.75×ATR and stop 1.6×ATR (the shipped v68 multipliers). The verdict mix came out as 38 `NO TRADE`, 22 `NO TRADE [WEAK SHORT]`, 16 `WEAK LONG`, 7 STRONG LONG, 3 LONG, 6 STRONG SHORT and 8 SHORT.

**The pipeline, step by step:**

1. **Load and classify.** `ClassifyLoadedRows` keeps all 100 rows because they're weekday rows with parseable timestamps. `VerdictCounts` is keyed on the raw verdict string.
2. **Fetch.** The range runs from min(ts) to max(ts) + 16 minutes. Bars are keyed on open time + 1 minute.
3. **Join.** `rowMin` is the timestamp floored to the minute. A 5-minute window takes the bars closing at T+3 to T+5, so the first eligible bar opens at T+2:00. The logged entry was priced at about T+0:07. Nothing re-prices the entry.
4. **Population.** The session bucket comes from today's cfg applied to `Timestamp.Hour`. The `PLACED` label is set once per file from the header.
5. **Matrix.** Only the 24 STRONG/MEDIUM rows count. The exclusion test is |target − entry| < 0.0008 × entry, which is 48 USD, and a 78.75 USD target passes. Each row is walked in every window.

| tier | W | n | success | EV after fees (bps) |
|---|---|---|---|---|
| MEDIUM_SHORT | 5 / 10 / 15 | 8 | 38% / 75% / 88% | +2.9 / +7.4 / +8.7 |
| STRONG_LONG | 5 / 10 / 15 | 7 | 43% / 43% / 43% | −0.2 / −3.2 / −3.7 |
| STRONG_SHORT | all | 6 | 33% | −8.0 |

6. **Fees.** The report computes none. I added them to the replica:
   - Target path: +|T−e|/e − 3 bps (maker/maker).
   - Stop or ambiguous bar: −|S−e|/e − 5 bps.
   - Window expiry: mark-to-close − 5 bps.
   - At ATR 45 the target is 13.1 bps and the stop is 12.0 bps. Gross break-even success is 12/25.1 = **47.8%**. Net of fees it's 17/27.1 = **62.7%**. For a structural target sitting right at the 8 bps floor it's 17/22 = **77%**.
   - Inverse-contract convexity is second order at these distances, and I ignored it.
7. **Correlation.** The 6 STRONG_SHORT rows are **one** episode: consecutive minutes after the cascade, all walking the same V bounce. MEDIUM_SHORT's 8 rows are 4 runs.
8. **Stale entries.** In 7 of the 24 directional rows, the first eligible bar **opens already through the target**, so they're booked SUCCESS at an entry that no longer existed. 4 open already through the stop. The mean entry drift over those 2 minutes was +35 USD favourable, with a range of −170 to +443.
9. **Context table.** All 22 `NO TRADE [WEAK SHORT]` rows pass the filter at `AnalysisRunner.vb:278-283` and are walked as shorts. That's EVAL-1, confirmed.

---

### F1: CRITICAL: the report has no fees and no EV anywhere
**LOCATION:** `MarkdownReportWriter.vb` (all sections plus the summary CSV) and `FailureRateMatrix.Compute`. Grep `analysis/` for bps, fee or EV and you get only the move-floor exclusion.

**DOWNSTREAM IMPACT:** You decide settings and whether to trade from a success rate whose break-even is never shown. The rate is also blind to payoff, because the placed target is structural and varies per row.

**FAILURE SCENARIO:** A cell shows **58%** success with a CI floor of 50%, and it reads as an edge. At ATR 45 with 1.75/1.6 geometry the net break-even is 62.7%, so the cell loses about 1.6 bps per trade. Rows with targets near the 8 bps floor need 77%. The same success rate can be +EV in one book and −EV in the next, depending on the target-distance mix.

**CRITIQUE:** Success rate is the wrong statistic for a barrier strategy with asymmetric, per-row payoffs. EV has to be computed per row, as realized distance minus path-specific fees (3 bps on the target path, 5 on the stop path), then aggregated with a CI. Window expiry needs a mark-to-close P&L, not a flat "failure": in the trace, STRONG_LONG's success is flat at 43% across windows while its EV falls from −0.2 to −3.7 bps. That change only shows up through the expiry and adverse split.

One more thing, outside this file set but feeding its exclusion test: `EffectiveMinMovePct` is built from the **maker_maker** round trip (3 bps). The floor never prices the 5 bps loss path.

### F2: CRITICAL: the unit of analysis is a minute row, not a trade, so Wilson CIs and n ≥ 30 are fiction
**LOCATION:** `FailureRateMatrix.Compute:246-304`, `WilsonCI:362`, `AnalysisConstants.MinSamplesPerCell = 30`. There is no dedupe on `SignalId` or on position state anywhere in `analysis/`.

**DOWNSTREAM IMPACT:** Every minute a verdict persists adds another "independent" trial over the same forward bars. The autotrader wouldn't open a second position while in the first, so the matrix counts signals that were never executable. The ★ picks, the n ≥ 30 stability gate and the auto-tweaker's 60-row trigger all inherit this.

**FAILURE SCENARIO:** In the trace, 6 STRONG_SHORT rows are one episode. A 30-minute trend with a sticky STRONG verdict fills a whole "stable" cell with n=30 and a ±17 pp CI, while the true n is 1. Pooled books make it worse. Your own EVAL-1 sizing script (`medium-tier-bug-hunt-spec-back.md:465`) dedupes the pooled book on timestamp (`if x[0] in seen`), which shows the concatenated book carries duplicate rows. `ForwardWindowJoiner.Load` doesn't dedupe, so those rows count twice.

**CRITIQUE:** Collapse to episodes. Take the first row of each run of the same tier, or better, replay position occupancy with the order app's rules. Then report the number of episodes next to the number of rows.

### F3: HIGH: the logged entry is used as the fill, a touch counts as a target fill, and the window starts two minutes later
**LOCATION:** `ComputeContextOutcomes:298` and `FailureRateMatrix.Compute:257` (entry = `row.Price`); `ForwardWindowJoiner.PopulateForwardBars:274`; `WalkBars:165` (`>=` / `<=`).

**DOWNSTREAM IMPACT:** Moves in the T+0 to T+2 blind spot are credited or debited at the stale entry. On flush days, when momentum signals fire late, this books "successes" whose entry price had already gone.

**FAILURE SCENARIO:** In the trace, 7 of 24 rows open through their target on the first eligible bar and score SUCCESS with no fill possible. There are two more problems:
- A limit take-profit that price only touches usually doesn't fill (queue priority). The levels aren't tick-rounded, so a touch test against an unrounded level such as 60,123.37 can be a fraction of a tick off wherever the order app actually rounds.
- The OHLC comes from `get_tradingview_chart_data`, which is last-trade data. If the order app triggers stops on mark or index price, last-trade cascade wicks overstate both stop and target hits. I did not verify which trigger price the order app uses.

**CRITIQUE:** Model the fill. Either enter at the first eligible bar's open and shift both barriers by the same offset, or drop rows that are already through a barrier and count them. Require the target to trade through by at least 1 tick. Use the stop's trigger series for the adverse walk.

### F4: HIGH: ◆ "highest success rate" is mechanically the longest window, so §4 and §7 are noise
**LOCATION:** `FailureRateMatrix.Compute:347` and `MarkdownReportWriter.AppendRecommended` / `AppendHoldWindow`.

**DOWNSTREAM IMPACT:** You're shown a "recommended hold window" that is a tautology.

**FAILURE SCENARIO:** The walk is prefix-monotone. A success at 5m is still a success at 15m, an adverse hit stays an adverse hit, and expiries can only turn into successes. With the same rows, success can only rise with W. Ties break to the shortest window because the comparison is a strict `<`. The trace shows MEDIUM_SHORT at 38% → 75% → 88%. ◆ can only point elsewhere when data gaps change n.

★ ("lowest CI width") favours extreme p, and the section admits it can pick the worst cell. Neither pick measures what a hold window costs, which is expiry P&L plus fees. Only EV per window can answer that.

### F5: HIGH: forward windows at the end of the book are truncated but scored as complete
**LOCATION:** `AnalysisRunner.vb:73`, `DeribitOhlcFetcher.FetchOhlcRange`, `PopulateForwardBars:274-279`, and the `ExcludedRows` / `Compute:288` checks, which only reject an **empty** bar list.

**DOWNSTREAM IMPACT:** If the report runs on a live book, every row within W minutes of "now" walks a partial window, which can include the still-forming 1m bar. Those rows fall through to WINDOW_EXPIRED and count as failures. The same happens anywhere Deribit leaves a bar out of a window: a missing bar that held the stop hit turns an adverse hit into a later success.

**FAILURE SCENARIO:** You run the report 5 minutes after a STRONG signal. That row's 10m and 15m cells get 2 bars instead of 7 and 12. The most recent rows are exactly the ones you're reading for regime change.

**CRITIQUE:** Require `bars.Count = W − 2` (exact-count completeness) and count incomplete windows as a separate exclusion.

### F6: HIGH: the §6(a) context table is wrong in three ways beyond EVAL-1
**LOCATION:** `AnalysisRunner.ComputeContextOutcomes:272-313`.

1. **EVAL-1 (known, confirmed).** `<> "NO TRADE"` is an exact match, so `NO TRADE [WEAK SHORT]` gets in and is walked as a short by `Contains("LONG")`. In the trace that was 22 non-trades. The comment at `:273-276` claims the filter masks NO TRADE rows, which is false for every lean-tagged verdict.
2. **Blacks out 3-min populations.** When no tier has a recommended cell, `w` falls back to 10. A res-3 row's `ForwardBars` keys are {15, 30, 45} (the trace prints `False`), so every directional row is skipped. The whole population renders "insufficient sample", which looks like thin data rather than a bug.
3. **One window for everything.** `recCell` is the **first** recommended cell in tier order, usually STRONG_LONG's. That window is applied to LONG and SHORT, and STRONG and MEDIUM, pooled together. There's also no v35 below-min-move exclusion, so the denominator differs from the §2 matrix it's meant to be read against.

### F7: MEDIUM: stale constants still drive the barrier fallback
**LOCATION:** `AnalysisConstants.vb:370`: `AdverseFallbackAtrMultiplier = 1.2`, documented as "matches cfg default (1.2)". The shipped value and the POCO default are **1.6** (`EngineSettings.vb:851`). `EngineTargetAtrMultiplier = 2.0` is documented as "mirror of default 2.0", but the POCO default is **1.75**.

**DOWNSTREAM IMPACT:** `ResolveAdverseBarrier:96/103` uses the 1.2 constant directly, not cfg. Any row in a placed population with neither a placed stop nor a swing stop, and every legacy row, gets a stop 25% tighter than the engine's. That inflates adverse hits in exactly the rows the fallback counters tell you are "mixed". `AutoTweakerCore.vb:75-76` also reads both constants as defaults; that's outside this file set and I didn't trace it.

This breaks the repo's own rule against magic numbers and the fixture-literal-provenance class of rule.

### F8: MEDIUM: the D4 before/after table invents zero deltas and compares different row sets
**LOCATION:** `MarkdownReportWriter.AppendD4Grid:349-357`.

**FAILURE SCENARIO:** If `before` has n=0, `beforeRate` falls back to `after.FailureRate`, and the table prints `x% → x% (0%)`: a measured-looking "no change" that was never measured.

Separately, Legacy mode's `GateTargetDistance` uses 1.75×ATR where Placed mode uses the placed distance. The two walks therefore exclude **different rows**, and only the "after" n is printed, so the Δ mixes a population change with a geometry change.

### F9: MEDIUM: the OI×CVD asymmetry audit can return ASYMMETRIC_ALGORITHM with no evidence
**LOCATION:** `OutlierAudit.vb:95-107`.

**FAILURE SCENARIO:** This one is proven in the replica. When no regime has at least 10 signals, `regimeMax=0` and `regimeMin=1`, so the spread is −1. The check `< 0.2` passes and the verdict is **ASYMMETRIC_ALGORITHM**, with no stratification done at all. A single qualifying regime also gives a spread of 0, which again reads as "asymmetric algorithm". On top of that, the counts are minute rows, not events (same problem as F2).

### F10: MEDIUM: the concatenated-book loader drops an embedded header instead of re-mapping to it
**LOCATION:** `ForwardWindowJoiner.Load:172-198, 239`.

**FAILURE SCENARIO:** You concatenate a `.bak` from before a header rotation with the current file. The second header is skipped, and every later row is parsed with the **first** file's column indices. Placed levels, ATR and `ExecResolution` silently misalign, and none of the "not a live defect" reasoning in the comment covers this.

`HasPlaced` is also set per file from the first header. If the pre-v0.8 file comes first, the whole book is labelled `LEGACY_YARDSTICK`. If the v0.8 file comes first, pre-v0.8 rows are marked placed and read placed columns that aren't there.

### F11: MEDIUM: the funding diagnostic's recommendation depends on a number it never computes
**LOCATION:** `FundingMomentumDiagnostic.vb:179, 210-214`.

The "implied 30% threshold" is the 70th percentile of **non-zero** rows only, not of all rows. If 60% of rows are exactly 0, it implies a 12% firing rate, not 30%. The recommendation says "if the non-FLAT rate is below 5%…", but the non-FLAT rate at the live threshold is never computed or printed.

Rows are also weighted by run cadence, so heavily autocorrelated consecutive samples dominate the percentiles. The `0.00001` fallback when cfg is Nothing is a magic number.

### F12: LOW: a 70-day cap is reported as a network failure
**LOCATION:** `DeribitOhlcFetcher.vb:24`, where `MAX_CHUNKS = 20` covers about 69 days, and `AnalysisRunner.vb:81`.

A longer book returns Nothing, and the report says "report cannot be regenerated until Deribit is reachable". That sends you after the wrong cause.

### F13: LOW: the summary CSV depends on locale and fails silently
**LOCATION:** `MarkdownReportWriter.BuildSummaryCsv:741-746`.

It uses `ToString("F6")` without `InvariantCulture`, the same bug `FailureRateMatrix.Inv` was written to fix. On a comma-decimal locale every rate splits across two columns. The catch-all swallows write failures, but `SummaryCsvPath` is still set to a file that may not exist.

**Not a finding:** `AnalysisReportForm.vb` is a pure display shell with nothing wrong in it.

---

If I had to rank what to fix: F1, F2 and F3 together mean the headline number can't answer "is this signal worth trading after fees". Until per-episode, fee-inclusive EV with a modelled fill exists, the matrix tells you mostly about how persistent the verdicts are and how long the windows are. Every fix here moves a rendered value, which your rules reserve, so I changed no repo files. Nothing was committed. *(True when the audit was reported. This file and the proof directory were committed afterwards on request; no code file was touched.)*
