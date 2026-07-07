# History Archive

Historical content moved out of `DeribitIndicatorProject.md` on 2026-05-17 to keep the operational handover under the Read-tool token cap. **Operational reference lives in `DeribitIndicatorProject.md`.** This file is for retrospective lookups only — pre-v30 settings change rationale, full version history, completed bundles, and resolved parked observations.

For settings change details post-v22, read the `change_log` array inside `settings.json` directly. It's the source of truth.

---

## A. Pre-v22 settings.json Migration Notes

### v21 (RSI divergence + ROC split, 2026-04-30)
`CalcRSIDivergence` semantically rewritten: BEARISH fires when current price is AT OR ABOVE prior pivot (canonical higher-high pattern), prior pivot's RSI must have been ≥ `DivergenceOverboughtThreshold` (65) to qualify as exhaustion, most-recent confirmed pivot is used rather than highest in lookback. Mirror fix for BULLISH. `DivergenceRsiDelta` raised 2.0 → 5.0. Expected NONE rate rises from ~20% to ~80–90%. `ROC.slope_sensitivity` split into `slope_delta_threshold` (0.05, for RISING/FALLING/FLAT bar-to-bar classification) and `magnitude_threshold` (0.1, for partial ROC scoring and Pass 2c ROC-active check). Old key removed.

### v20 (OI threshold recalibration, 2026-04-30)
Post-v19 499-row dataset showed `OISignal` still 100% NEUTRAL: the effective threshold (0.003 × 100 = 0.3%) was above the observed 15m OI peak of ~0.23%. `indicators.OI.change_threshold_pct` lowered 0.003 → 0.002 (effective 0.2%). Quiet-session noise ~0.01–0.015% → 10:1 signal/noise separation.

### v19 (calibration tuning pass, 2026-04-30)
Six classifiers stuck on a single value across 618 rows due to threshold mismatches vs observed BTC-PERPETUAL scale. Recalibrated: `scoring.funding_high_positive/negative` ±3 bp → ±0.3 bp; `scoring.funding_low_positive/negative` ±0.5 bp → ±0.05 bp; `indicators.funding.momentum_threshold` 10 bp → 0.5 bp; `indicators.OI.change_threshold_pct` 1.0% → 0.3%; `indicators.ROC.slope_sensitivity` 0.1 → 0.05. `GetRecentTradesAsync(100)` → `GetRecentTradesAsync(500)` to widen liquidation detection window from ~60s to ~5min.

### v18 (API resilience pass, 2026-04-30)
`DeribitClient` wraps all five `GetXxxAsync` methods with `ExecuteWithRetry`: retry-once with 1s backoff on transient failures (5xx, timeout, network drop); return `Nothing` on hard failure. `GetFundingRateAsync` return type `Double` → `Double?`; `GetBookSummaryAsync` value tuple → nullable value tuple. `RunAnalysisAsync` validates all required fetches after `Task.WhenAll`; if any are `Nothing`, renders `ANALYSIS SKIPPED: <reason>`, increments `_skipCount`, returns without scoring or writing a CSV row. 15m cache preserved on fetch failure — stale data kept for MTF gate.

### v17 (settings-exposure pass, 2026-04-29)
All new keys default to exactly the previously-hardcoded values — no behavioural change. Keys added: `rsi.pass2c_midline`, `bbw.series_window_multiplier`, `bbw.squeeze_percentile`, `donchian.quartile_pct`, `cvd.late_segment_weight`, `cvd.early_segment_weight`, `ttm.sma_period`, `ttm.lin_reg_period`, `scoring.regime_max_score`, `scoring.tier_floor`, `scoring.context_tag_thresholds`. Closes the auto-tweaking audit prerequisite.

### v16 (swing pivot spec, 2026-04-29)
Added `indicators.swing` block: `pivot_wing_5m`, `lookback_bars_5m`, `pivot_wing_15m`, `lookback_bars_15m`.

### v15 cleanup (2026-04-27)
Removed silently-ignored keys: `scoring.long_threshold` / `short_threshold` / `strong_long_threshold` / `strong_short_threshold` / `medium_long_threshold` / `medium_short_threshold` (superseded by `verdict_*_pct` since v0.30); `scoring.weights` block (`ScoringWeights` class deleted long ago); `scoring.transitional_penalty_enabled` (TRANSITIONAL ADX penalty applies unconditionally); `regime_gates.suppress_long_in_trending_down` / `suppress_short_in_trending_up` (Step 4 regime veto applies unconditionally); `indicators.EMA200` block (`CalcEMA(candles5m, 200)` is hardcoded); `indicators.OBV.lookback`, `indicators.CVD.trade_lookback`, `indicators.VWAP.dev_threshold_pct`, `indicators.VWAP.session1_start_hour` / `session1_start_minute`, `indicators.BBW.releasing_roc_threshold`, `indicators.Liquidations.long_liq_threshold` / `short_liq_threshold`, `indicators.ATR.ref_period`; `mtf_gate.ema_period_fast` / `ema_period_slow` (`CalcMTFGate` hardcodes EMA 9/21/50). Display-only colour bugs fixed in `MainForm_Render_Sections`. Zero scoring impact.

---

## B. Specs Shipped Before v27 (2026-04-29 through 2026-05-13)

All six "spec bundle" specs (#1–#6) implemented 2026-04-29:

| Spec | Scope |
|---|---|
| `docs/bid-ask-spread-proposal.md` | SpreadBps + WIDE-spread entry-side penalty |
| `docs/ofi-momentum-proposal.md` | OFIMomentum (RISING/FALLING/FLAT) modifier on OFI level signal |
| `docs/dynamic-microcvd-accel-proposal.md` | Self-scaling MicroCVD acceleration threshold |
| `docs/vpfr-lite-v2-proposal.md` | VAH/VAL + nearest HVN/LVN walls; 3-tier Step 5b cap arbitration |
| `docs/swing-pivot-proposal.md` | 5m + 15m swing structure; structural ATR display rows; Layer 1.5 exit; 3-tier cap; sharper STRUCTURALLY_WEAK |
| `docs/settings-exposure-pass-proposal.md` | 19 hardcoded scoring literals → `settings.json` |

Earlier shipped:

| Spec | Scope |
|---|---|
| `docs/verdict-context-tag-proposal.md` | Verdict Sub-Context Tag — IMPLEMENTED 2026-04-14 |
| `docs/kelly-criterion-proposal.md` | Kelly Criterion sizing — IMPLEMENTED 2026-04-14 |
| `docs/adaptive-regime-weights-proposal.md` | Pass 2c regime-alignment gate — IMPLEMENTED 2026-04-21 |
| `docs/api-resilience-pass-proposal.md` | Retry + skip-on-failure — IMPLEMENTED 2026-04-30 |
| `docs/v19-calibration-tuning-pass-proposal.md` | Funding/OI/ROC threshold recalibration — IMPLEMENTED 2026-04-30 |
| `docs/v20-rsi-roc-algorithm-fixes-proposal.md` | RSI divergence + ROC split — IMPLEMENTED 2026-04-30 |
| `docs/v22-funding-calibration-pass-proposal.md` | Regime-aware funding bands — IMPLEMENTED 2026-05-01 |

---

## C. Active Spec Bundle Plan (2026-05-05) — historical reference

CalibrationReport reached threshold-with-caveats on 2026-05-05 (2460 rows, 4 regimes covered; 0 liquidation events accepted as rare-event blocker). User authorised proceeding with the full backlog under explicit priority order:

**Bundle 1 (foundation) — shipped 2026-05-05.**
- `csv-expansion-v0.4-proposal.md` — adds 18 columns at positions 69–86 (SpreadBps, OFIMomentum, FundingDelta, VPFR-v2 fields, swing fields, TargetCapReason, BestPivotByVolume reservations). Bumps schema, rotates log on header mismatch.
- `analysis-script-proposal.md` — VB.NET host-agnostic offline analyser at `analysis/`. Forward-return joiner, failure-rate matrix, funding-momentum diagnostic, OFI outlier audit, OI×CVD asymmetry audit. Reachable from MainForm via `lnkAnalysisReport`.

**Bundle 3 (structural refinements) — shipped 2026-05-06.**
- `d1-trend-structure-proposal.md` — HH/HL/LH/LL classification, Pass 2c integration via separate `StructureBonus` (default 1).
- `d2-volume-weighted-pivots-proposal.md` — display-only volume-weighted pivot ranking. v2 cap arbitration parked as observation P1.
- `b1-per-indicator-regime-weights-proposal.md` — STUB, blocked on Bundle 1 output for empirical hit rates.

**Bundle 2 (auto-tweaker) — shipped 2026-05-06.**
- `failure-definition-proposal.md` — ATR-based forward-return failure, 3 windows × 3 thresholds, Wilson-CI cell-stability picker. **Superseded** by `failure-definition-v2-proposal.md` (2026-05-07): barrier-hit with adverse stop, OHLC walk via Deribit.
- `auto-tweaker-pipeline-proposal.md` — VB.NET console app at `tools/AutoTweaker/`, Linux-portable, dry-run mode + manual-apply path. Later extended by `settings-snapshot-history-proposal.md` (2026-05-12) and `auto-tweaker-fixed-window-proposal.md` (2026-05-17).

Bundles 4 (small refinements per B4 item), 5 (multi-session VPFR / anchored VWAP, C1/C2), and 6 (Smart OBV / MFI replacement) remain deferred. Section A (post-WebSocket) and the WebSocket migration itself remain post-Bundle-2 in priority.

---

## D. Resolved Parked Observations

### P3. OI×CVD asymmetry — RESOLVED 2026-05-08
**Diagnosis:** asymmetry was upstream of Pass 2b. `MainForm_Analysis.vb`'s `priceUp` computation compared `r.CurrentPrice > bookSummary.Value.MarkPrice * 0.9999` — but `MarkPrice` is the current snapshot of mid + smoothing, tracking the last-traded within ~1bp at any moment. The `* 0.9999` factor introduced a 1bp bias on the threshold, so `priceUp` was True almost always. The classification branches `NEW SHORTS` (OI rose + price fell) and `CAPITULATION` (OI fell + price fell) almost never fired, starving Pass 2b's CONFIRMED_SHORT path.
**Fix:** commit `e2ecb95` 2026-05-08. `priceUp` now compares `r.CurrentPrice` against `candles1m(count - 16).Close` — the actual close 15 minutes ago, matching the window of `OIChange15m`. Pass 2b downstream logic was already symmetric.

### P7. Live per-analysis success/fail display — RESOLVED 2026-05-13
Trader confirmed need 2026-05-12 — settings-snapshot work showed that round-level stats (every ~10–15 min) are insufficient for real-time trading-style adaptation.
**Resolution:** implemented per `docs/live-performance-display-proposal.md`. Six-window strip (Cur.Wk / 3d / Cur.Day / Asia / London / NY) in `MainForm`, updated on every `RunAnalysisAsync`. Session most-recent-block semantics. Reuses `FailureRateMatrix.WalkBars` verbatim. Two gitignored sidecar caches. Settings block `performance_display` added to v26.

---

## E. Full Version History (pre-v27)

| Version | Date | Key Changes |
|---|---|---|
| **v26 — live-performance-display** | 2026-05-13 | Activates P7. New host-agnostic files `OhlcCache.vb` + `LivePerformanceTracker.vb`. New sidecar caches `analysis_eval_cache.csv` + `ohlc_1m_cache.csv` (both gitignored). Six perf-strip labels in `MainForm_Layout`, updated on every `RunAnalysisAsync`. Most-recent-block session semantics with straddle-aware NY. Settings block `performance_display` (4 keys). |
| **v25 — output-dump** | 2026-05-11 | New `AnalysisOutputDump.vb` (host-agnostic). New `UI/OutputDumpSettingsForm.vb`. Status-bar links `lnkOutputDump` + `lnkOutputDumpSettings`. `RenderOutput` hooks `Append(v.Timestamp, txtOutput.Text, ...)` after breakdown table. `VerdictResult.Timestamp` added (replaces hardcoded +8 offset). New settings block `analysis_logging.output_dump_enabled` + `output_dump_max_runs`. Rolling-trim to last N runs. |
| **v24 — Bundle 3 (d1 + d2)** | 2026-05-06 | D1: HH/HL/LH/LL trend structure. `ClassifyTrendStructure()` in `Indicators_Structure.vb`. `TrendStructure` enum. Pass 2c `StructureBonus` (default 1). `indicators.trend_structure` block. CSV column 87 `TrendStructure5m`. D2: volume-weighted pivots display-only. `BestPivotByVolume5m`, `BestPivotVolumeRatio5m`, `BestPivotIsHigh5m`. CSV columns 85–86. v2 cap-arbitration promotion parked as P1. B1 stub left untouched. |
| **v23 — Bundle 1 (csv-expansion + analysis script)** | 2026-05-05 | CSV schema bump v0.3 → v0.4. 18 new columns at positions 69–86. `AnalysisLogger.EnsureLogFile()` rotates on header mismatch. New `analysis/` folder with 9 host-agnostic VB.NET classes + `AnalysisReportForm.vb` (the only file in the folder with WinForms ref). |
| **Bundle 2 (auto-tweaker)** | 2026-05-06 | No settings.json change. New `tools/AutoTweaker/AutoTweaker.vbproj` (.NET 8 console, Linux-portable). 7 classes: Program, Core, PromptBuilder, ClaudeApiClient, SettingsDiffApplier, TweakerConfig, TweakerState. Latest-Opus model resolution via `/v1/models`. ANTHROPIC_API_KEY from env. Dry-run + `--apply-manual` modes. Hard rejection list + 3-key scope cap. New `UI/TweakSettingsForm.vb` (non-modal). |
| **failure-definition-v2** | 2026-05-07 | No settings.json change. ForwardReturnJoiner → ForwardWindowJoiner. OHLC bulk fetch via new `analysis/DeribitOhlcFetcher.vb`. SUCCESS / ADVERSE_HIT / WINDOW_EXPIRED / AMBIGUOUS outcomes via barrier-hit walk. STRONG/MEDIUM thresholds swapped to preserve "STRONG=harder bar". |
| **v22 — funding calibration** | 2026-05-01 | Regime-aware funding band recalibration based on Deribit's 1m/7d/8h charts: low ±0.0001 kept / high ±0.0008. Funding momentum threshold 1 bp. Acknowledges 60s polling cadence ceiling. |
| **2026-05-12 output-dump-bug-audit** | 2026-05-12 | No settings.json change. **B1:** `TargetCapReason` field split into `TargetCapReasonLong` / `TargetCapReasonShort` (was overwritten by short-side block when both fired). **B2:** `CONTEXT:` line guard fixed — was hiding CONFIRMED verdicts. |
| **2026-05-12 settings-snapshot-history** | 2026-05-12 | No settings.json version change (knobs in `tweaker_config.json`). New host-agnostic classes in `tools/AutoTweaker/`: `CompositeScorer`, `ConditionsExtractor`, `SnapshotManager`, `RoundStatsBuilder`. TweakerState gains `CurrentBelowThresholdStreak`, `ActiveSnapshotFilename`, `LastSuccessfulRoundIso`, `RoundHistory` (50-cap). TweakerConfig gains `MaxKeysPerProposal`, `SnapshotStreakX`, `StreakWeight`, `StreakLengthClamp`. SettingsDiffApplier gains `ApplyRevert`. New `UI/RoundStatsForm.vb`. |
| **2026-04-29 specs #1–#4** | 2026-04-29 | Spec #1 bid-ask spread: `SpreadBps`, WIDE-spread penalty, `indicators.spread`. Spec #2 OFI momentum: `OFIMomentum`, `CalcOFIMomentum`, `_ofiHistory`, `ofi.momentum_*`. Spec #3 dynamic MicroCVD: `accel_pct_of_window`. Spec #4 VPFR-lite v2: VAH/VAL + nearest HVN/LVN. |
| **2026-04-22 calibration pass** | 2026-04-22 | v14. Value-only tuning: OFI dom ratios, RSI partial bands, funding high bands, CVD slope min, Volume clamp mins, ATR static ref, MicroCVD accel threshold, Kelly account placeholder. Funding-history dedup. |
| **2026-04-22 defect fixes Batch 1** | 2026-04-22 | M1: MTF-gate JSON key binding repaired (`candle_lookback`, `adx_period`, `min_of`, new `adx_min`). M2: Kelly advisory label. M3: CAL-mode dead code removed. |
| **2026-04-21 adaptive-regime-weights** | 2026-04-21 | Pass 2c regime-alignment gate. `RegimeWeightSettings`. TRENDING ceiling 19→20, RANGE_BOUND 18→19 by default. |
| **2026-04-20 funding-momentum shipped** | 2026-04-20 | `FundingMomentum`, `CalcFundingMomentum`, `_fundingHistory`/`FundingHistoryMax`, Step 3b modifier, settings v10 with `indicators.funding` block. |
| **2026-04-20 refactor split** | 2026-04-20 | `Core/ScoringEngine_Calculate.vb` → `_Scoring.vb` + `_Verdict.vb`. `UI/MainForm_Render.vb` → `_Header.vb` + `_Sections.vb`. |
| **2026-04-14 Verdict Context + Kelly + CONTEXT line** | 2026-04-14 | Step 5b `CalcVerdictContext()`. `CONTEXT:` line always rendered (CONFIRMED in green). `CalcKellySizing()` block. Kelly fields on `VerdictResult`. `KELLY SIZING` rendered after ATR. CAL mode removed (EST only). |
| **2026-04-13 ATR multiplier fix** | 2026-04-13 | Stop/target multipliers read from cfg (no hardcoded 1.5/3.0). atr_stop_multiplier 1.2, atr_target_multiplier 2.0. |
| **Commit 5** | — | T2-C Donchian NONE mid-channel note. T3-A VPFR numBuckets cfg. T3-B RSI pivotWing + lookbackBars cfg. T3-C TTM flatThreshold cfg. T3-D Liq dominanceRatio cfg. |
| **Commit 4** | — | T1-B Regime ADX hysteresis 1-bar grace. T2-A MicroCVD FLAT stall penalty. T2-B OFI BookDepth injectable. |
| **v0.49** | — | P8 RSI zones/div penalty cfg. P9 ADX threshold + VWAP warmup scoring. P10 ROC dead-band + OFI dominance cfg. P11 ATR multipliers externalised. P12 BBW/Liq/Funding penalties externalised. settings.json v6. |
| **v0.48** | — | P4 TFI window separated from MicroCVD. TfiSettings + MicroCvdSettings (EngineSettings v0.36). |
| **v0.47** | — | P1 MTF TTL cache. P2 RSI div penalty. P3 CVD 3-seg slope. P4 Donchian quartile. P5 volMid partial. P6 OBV div block. P7 VPFR exp decay. |
| **v0.46** | — | RenderOutput refactor; VPFR HVN target cap display; last transacted price block. |
| **v0.45** | — | MicroCVD sign-aware penalty; CVD divergence penalty fix. |
| **v0.44** | — | VPFR-lite HVN cap in ScoringEngine; AdjustedLongTarget/ShortTarget. |
| **v0.43** | — | CalcVPFRLite added; POC proximity scoring. |
| **v0.42** | — | OBV adverse divergence gate; cross-category upgrade logic. |
| **v0.41** | — | Donchian quartile signal scaffolding. |
| **v0.40** | — | DynamicNorms volume thresholds; volMid partial scoring. |
| **v0.39** | — | Dual-session VWAP; warmup guard. |
| **v0.38** | — | MicroCVD 3-segment; BULL/BEAR_ACCEL/DECEL. |
| **v0.37** | — | CalcRSIDivergence added. |
| **v0.36** | — | AutoRunSettings added. |
| **v0.35** | — | Auto-run timer UI. |
| **v0.34** | — | MTFGate RSI fields removed. |
| **v0.33** | — | MTFGateSettings + CalcMTFGate + 15m TTL fetch. |

---

## F. Historical / Superseded Spec Docs

The following spec files in `/docs` are kept for historical reference. They do NOT reflect current engine state — the corresponding feature has been re-specced or superseded:

- `bbw-scoring-proposal.md` / `bbw-scoring-response.md` — original BBW scoring design, pre-v0.41
- `dual-scoring-fix-proposal.md` / `dual-scoring-fix-response.md` — legacy dual-scoring approach, pre-v0.30
- `failure-definition-proposal.md` — original ATR forward-return failure definition. **Superseded** by `failure-definition-v2-proposal.md` (2026-05-07 barrier-hit semantics).

For all currently-shipped specs, see `docs/DeribitIndicatorProject.md` §3 Docs table.

---

## G. Resolved WATCHING / Calibration Items (moved from §12, 2026-07-02 housekeeping)

- **Post-correctness-pass re-baseline (OBV/CVD/MicroCVD/session-volume seed)** — RESOLVED by v33 (partial, NY-only) + v34 (full, CalibrationReport READY at 975 rows). OBV `trend_gate` 10→18, CVD `slope_pct_of_value` 0.01→0.05→0.10, MicroCVD `accel_threshold_dynamic_pct` 0.03→0.30, `session_volume` ASIA 0.80/0.85→1.10/1.05; volume clamps + Donchian quartile re-confirmed. Specs: `clean-data-rebaseline-v33-proposal.md` / `-v34-proposal.md`. (Residual weekday-ASIA re-verify stayed in §12.)
- **v36 Phase-2 ROC re-baseline (the 2.1× proxy)** — RESOLVED by v40 + v41 (workstream B, 2026-06-20/22): firing-rate-matched to NY-1m on the weekday 3-min book. Magnitude went per-session (`session_volume.sessions[].roc_magnitude_threshold` — ASIA 0.17, LONDON 0.11; `resolution_profiles["3"]` keeps 0.21 as fallback); slope stayed shared (`resolution_profiles["3"].roc_slope_delta_threshold` 0.105→0.06). Spec: `asia-london-roc-rebaseline-proposal.md`.
- **3-min hold-window recalibration (offline matrix + eval horizon)** — IMPLEMENTED + VALIDATED + CLOSED 2026-06-24: resolution-scaled windows `{5,10,15}×execRes` → 3-min `{15,30,45}` (`3d473a1`), `LivePerformanceTracker` horizon scaled via `EvalHorizonMinutes(res)` (`1825fad`); plateau confirmed on the 5,275-row post-soak book (failure flattens at 30m; 45m adds zero new successes). Specs: `three-min-hold-window-recalibration-proposal.md` / `-spec-back.md`.
- **OI × CVD gate tuning** — RESOLVED-KEEP 2026-07-03 (W1 signal-health audit R5), archived on the v50 retune ship: CONFLICT 38.9% vs CONFIRMED 55.6% SUCC (n=18/27), gradient in the designed direction at both ends; engages on 2.9% of runs. No change to `oi_cvd` settings. Evidence: `signal-health-audit-2026-07-03.md`.
- **Bid-ask spread threshold (revival question)** — RESOLVED-KEEP 2026-07-03 (W1 audit R3/F4), archived on the v50 retune ship: the revival premise is refuted — one-tick book on both transports (WS p50–p95 0.08–0.09 bps, p99 0.59; >5 bps on 0.1% of runs); 5.0 kept as an inert tail-guard; **A1 spread-momentum REJECTED with evidence** (no variance to ride). Evidence: `signal-health-audit-2026-07-03.md`.
