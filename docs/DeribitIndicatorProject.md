# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-06-11 | Current version: settings.json v31 — engine correctness pass (F1–F9 + CSV v0.5 + data reset) on top of v30 (display polish), v29 (auto-tweaker fixed window), v28 (target-hit metric toggle).**

Operational reference for any AI conversation continuing this project. Historical content — pre-v27 settings change rationale, full version history back to v0.33, completed spec bundles, resolved parked observations — lives in `docs/history-archive.md`.

**Session start checklist:**
1. Read this file + `docs/architecture.md`.
2. Load the `crypto-trading-context` skill (it carries trader profile + writing style — don't separately read `docs/trader-profile.md`; the skill loads it).
3. Do NOT read individual `.vb` files at session start — only open them when a specific edit is required.

---

## 1. Project Purpose

Windows Forms (VB.NET / .NET 8) desktop app. Polls Deribit REST for BTC-PERPETUAL, computes technical indicators on 1m/5m/15m candles, scores them through a multi-tier pipeline, emits a verdict (STRONG LONG / LONG / WEAK LONG / NO TRADE / WEAK SHORT / SHORT / STRONG SHORT) with ATR-based entry/stop/target levels.

**Current state (v30, 2026-05-17):** scoring pipeline stable, calibration complete on six initial spec bundles, four major v27-v30 features shipped in May 2026, fresh data accumulation under new metrics in progress. The engine is approaching its accuracy ceiling for a single-instrument 1m scalping system using REST polling — see §13 *Accuracy Ceiling Note* and §16.4 for the WebSocket migration discussion.

Recent major shipments (full detail in §15):
- **UI reskin P3** (2026-05-21): Custom controls library — 14 controls in `UI/Controls/` (`RoundedCardPanel`, `ScoreArcGauge`, `VolumeHistogramMini`, `MiniMeter`, `FlatButton`, `AnalysisReportButton`, plus 8 composition controls: `Pill`, `ChipNumeric`, `LinkRow`, `SectionGroup`, `ContextBadge`, `MtfRow`, `RegimeAnchorWarn`, `OiCvdBadge`). Shared `Helpers/PaintHelpers.vb` (`RoundedRect` / `DrawGlow` / `ArcPath`). Not wired in yet — P4 connects them to `MainForm`. App appearance unchanged from P2. `SegmentedToggle` and `Pass2cBadge` retired in the 2026-05-24 P3 maintenance pass (see §15).
- **UI reskin P2** (2026-05-20): Visual repaint — palette hex values swapped to design system (`#0D1117` base, 7-tier verdict colour ramp, amber CTA on ANALYZE). `ACC_HEADER` token retired; section headers now neutral grey, CAPPED labels stay amber. Layout unchanged.
- **UI reskin P1** (2026-05-19): Theme infrastructure — Geist Mono bundled as embedded resource, palette token layer (`UI/Theme/Theme.vb`) replacing the inline `C_*` constants. Zero visual change. Prepares for P2 visual repaint.
- **v30** (2026-05-17): Display polish pass — output dump captures perf-strip, R:R rendering, CAPPED suppression, NO TRADE relabelled CONFIRMED→ALIGNED, 5 other display fixes
- **v29** (2026-05-17): Auto-tweaker fixed-window mode + MinTier statistical-floor rework
- **v28** (2026-05-17): Target-hit metric toggle on perf strip + eval cache schema v1→v2
- **v27** (2026-05-15): OHLC cache gap-backfill on `InitialiseAsync`

Earlier feature waves (Spec #1-#6 in 2026-04-29, Bundles 1-3 in 2026-05-05..2026-05-12, v22 funding calibration, v21 RSI/ROC algorithm fixes, v20 OI threshold recalibration, v19 calibration tuning, v18 API resilience, v17 settings exposure) — see `docs/history-archive.md`.

---

## 2. Repository

- **GitHub:** https://github.com/Beansz2015/DeribitVerdictEngine
- **Branch:** `master`
- **Solution file:** `DeribitVerdictEngine.sln`
- **Target framework:** .NET 8, Windows Forms
- **Working tree:** `C:\Dev\DeribitVerdictEngine` (root). Visual Studio solution points here. All code edits target root, not any `.claude\worktrees\` subdirectory.

---

## 3. File Inventory

### Root

| File | Purpose |
|---|---|
| `DeribitClient.vb` | All Deribit REST calls incl. 15m candles, recentTrades. `ExecuteWithRetry` wraps every public `GetXxxAsync`. |
| `DynamicNorms.vb` | ATR/Vol/VWAP norm computation; session-aware volume threshold adjustment via `ApplySessionVolume`. |
| `AnalysisLogger.vb` | CSV logging + CalibrationReport. v0.4 schema rotates old logs on header mismatch. |
| `AnalysisOutputDump.vb` | Persistent markdown record of full rendered analysis text per run (v25; v30 adds optional `perfStripLine` param). |
| `LivePerformanceTracker.vb` | Live perf-strip eval cache + window aggregator. `InitialiseAsync` Step 1.5 gap-fills OHLC (v27). v2 eval schema with `TargetEverHit` (v28). |
| `OhlcCache.vb` | Rolling 7-day 1m OHLC cache. Load / Append / WriteAll / RollingTrim / NewestBarTime. |
| `OiSnapshot.vb` | OI ring-buffer helper. |
| `AutoRunTimer.vb` | `IAutoRunTimer` interface + `WinFormsAutoRunTimer` impl. Linux CLI variant planned. |
| `Program.vb` | Entry point. |
| `settings.json` | v30 — all tunable parameters. See §6 for the operational pointer; `change_log` array inside the file is the source of truth for version history. |
| `MainForm.Designer.vb` | Auto-generated WinForms designer (do not edit). |
| `MainForm.resx` | Form resources. |

### Core/

| File | Purpose |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult (incl. `AdjustedLongTarget`/`AdjustedShortTarget`, `TargetCapReasonLong/Short` split — B1 fix 2026-05-12, `VerdictContext`, Kelly fields, `Timestamp`, `OiCvdOutcome`), PositionState, SignalCategory, ScoreState. |
| `Core/ScoringEngine_Helpers.vb` | `RegimeMaxScore` (cfg `scoring.regime_max_score`), `Threshold`, `TierFloor` (cfg `scoring.tier_floor`), `AddFull`, `HasCrossConfirm`, `BuildNote`, `CalcHoldStatus` (Layer 1 microstructure / **Layer 1.5 structural-break exit** / Layer 2 momentum break (ROC crosses 0) then OBV divergence / Layer 3 RSI/ROC). |
| `Core/ScoringEngine_Calculate_Scoring.vb` | `AppendLean()`, `CalcVerdictContext()` (returns ALIGNED on NO TRADE per v30 F11; FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED / ALIGNED), `RunScoringPipeline()` Steps 2 / Pass 2 / Pass 2b / Pass 2c / 3 / 3b. |
| `Core/ScoringEngine_Calculate_Verdict.vb` | `Calculate()` entry point. Step 4 regime veto + TRANSITIONAL ADX penalty. Step 4b MTF gate veto. Step 5 verdict. **Step 5b 3-tier target cap** (swing → nearest HVN → POC). |
| `Core/ScoringEngine_Kelly.vb` | `CalcKellySizing()` — display-only, called from `MainForm_Render_Header` not from `Calculate()`. Zero scoring impact. |
| `Core/IndicatorResults.vb` | IndicatorResults struct. All indicator output fields incl. `FundingMomentum`, `SpreadBps`, `OFIMomentum`, VPFR-v2 fields, swing pivot fields, `TrendStructure5m`, `BestPivotByVolume5m`. |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcRSI, CalcRSISeries, **CalcRSIDivergence** (v21 semantic rewrite), CalcROCSeries, CalcVolumeSMA. |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze. |
| `Core/Indicators_OrderFlow.vb` | CalcOFI, CalcOFIMomentum, CalcLiquidations, CalcCVD, CalcMicroCVD (dynamic accelThreshold), CalcTFI, **CalcFundingMomentum + AppendFundingSample** (v53 time-anchored window + 30-min ring eviction). |
| `Core/Indicators_Structure.vb` | CalcDonchian, CalcOBV, CalcVPFRLite v2 (VAH/VAL + nearest HVN/LVN), **CalcSwingPivots** (5m primary + 15m context), **ClassifyTrendStructure** (HH/HL/LH/LL D1), CalcMTFGate. |
| `Core/Settings/EngineSettings.vb` | POCO contract for `settings.json`. All sub-settings classes. |
| `Core/Settings/SettingsLoader.vb` | JSON deserialisation. `SettingsLoader.Current` singleton. `FileSystemWatcher` hot-reload. `Save(cfg, changeNote)` writes back with auto change_log entry. |

### UI/

| File | Purpose |
|---|---|
| `UI/MainForm_Layout.vb` | Shared fields, constructor, status-bar cascade, NUD centring. Owns `_fundingHistory`, `_ofiHistory`, six perf-strip labels + `lblPerfMode` (`[B]`/`[T]` indicator), `_metricMode`, `_perfContextMenu`. |
| `UI/MainForm_AutoRun.vb` | Auto-run timer lifecycle. |
| `UI/MainForm_Analysis.vb` | `RunAnalysisAsync()` — full data fetch + indicator + scoring pipeline. Raises `AnalysisCompleted` event. |
| `UI/MainForm_Render_Header.vb` | RTF helpers, CalibrationReport/log helpers, `RenderOutputHeader()`: VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME / LAST PRICE / HOLD\EXIT (gated on `posState ≠ None`) / ATR levels / Long+Short structural rows / KELLY block. v30 `FormatRR` helper, `capNoiseFloor` for sub-tick CAPPED suppression. |
| `UI/MainForm_Render_Sections.vb` | `RenderOutput()` entry point, all indicator sections, FUNDING section (v30 negative-zero clamp), signal breakdown table, verdict label update, perf-strip line composition for output dump. |
| `UI/OutputDumpSettingsForm.vb` | Non-modal dialog for output dump settings (Enabled toggle, max-runs, clear/save). |
| `UI/TweakSettingsForm.vb` | Non-modal Auto-Tweaker settings dialog. v29 adds MinTier textbox + auto-track-WindowSize logic. |
| `UI/RoundStatsForm.vb` | Non-modal RichTextBox showing per-tier accuracy via `RoundStatsBuilder`. |
| `UI/AnalysisReportForm.vb` | Non-modal viewer for offline analysis markdown report. |

### Docs (current, operational)

| File | Purpose |
|---|---|
| `docs/DeribitIndicatorProject.md` | This handover. |
| `docs/architecture.md` | Codebase structure, data flow, design decisions. Plus display-behaviour clarifications. |
| `docs/history-archive.md` | Pre-v27 settings notes, full version history v0.33 onward, completed spec bundle list, resolved parked observations. |
| `docs/UserManual.md` | End-user documentation (what every row in the rendered output means). |
| `docs/TraderGuide.md` | Trading-side documentation (how to read verdicts, when to act). |

**Active spec docs** (currently shipped; preserved for reference):
- `display-polish-pass-proposal.md` ✅ v30
- `auto-tweaker-fixed-window-proposal.md` ✅ v29
- `target-hit-metric-proposal.md` ✅ v28
- `ohlc-gap-backfill-proposal.md` ✅ v27
- `live-performance-display-proposal.md` ✅ v26
- `output-dump-proposal.md` ✅ v25
- `settings-snapshot-history-proposal.md` ✅ 2026-05-12
- `failure-definition-v2-proposal.md` ✅ 2026-05-07
- `auto-tweaker-pipeline-proposal.md` ✅ Bundle 2 (extended by snapshot-history + fixed-window specs above)
- `d1-trend-structure-proposal.md` ✅ Bundle 3 / v24
- `d2-volume-weighted-pivots-proposal.md` ✅ Bundle 3 / v24
- `csv-expansion-v0.4-proposal.md` ✅ Bundle 1 / v23
- `analysis-script-proposal.md` ✅ Bundle 1 / v23
- `verdict-context-tag-proposal.md` ✅ 2026-04-14
- `kelly-criterion-proposal.md` ✅ 2026-04-14
- `bid-ask-spread-proposal.md` / `ofi-momentum-proposal.md` / `dynamic-microcvd-accel-proposal.md` / `vpfr-lite-v2-proposal.md` / `swing-pivot-proposal.md` / `settings-exposure-pass-proposal.md` ✅ all 2026-04-29
- `api-resilience-pass-proposal.md` ✅ v18
- `v19-calibration-tuning-pass-proposal.md` ✅ v19
- `v20-rsi-roc-algorithm-fixes-proposal.md` ✅ v21
- `v22-funding-calibration-pass-proposal.md` ✅ v22
- `adaptive-regime-weights-proposal.md` ✅ Pass 2c
- `post-websocket-post-calibration-backlog.md` — deferred items (D3/D4 etc.)

**Stubbed / blocked:**
- `b1-per-indicator-regime-weights-proposal.md` — STUB, blocked on Bundle 1 empirical hit-rate output

**Historical / superseded:** `bbw-scoring-*`, `dual-scoring-fix-*`, `failure-definition-proposal.md` (v1, superseded by v2). All catalogued in `history-archive.md` §F.

For the full annotated directory tree and data flow diagram, see `docs/architecture.md`.

---

## 4. Indicator Signal Map

### Core Signals (always scored)
| Indicator | Method | Config keys |
|---|---|---|
| ROC(9) | CalcROCSeries | `cfg.Indicators.ROC.SlopeDeltaThreshold` (0.05) — bar-to-bar delta for RISING/FALLING/FLAT. `cfg.Indicators.ROC.MagnitudeThreshold` (0.1) — gates partial scoring + Pass 2c ROC-active check. Split in v21. |
| RSI(9) | CalcRSI | `Overbought` (60) / `Oversold` (40) / `PartialOverbought` (55) / `PartialOversold` (45). |
| RSI Divergence | CalcRSIDivergence | v21 semantic rewrite: BEARISH fires when current price ≥ prior pivot AND prior pivot RSI ≥ `DivergenceOverboughtThreshold` (65). Mirror for BULLISH. Most-recent confirmed pivot used. `DivergenceRsiDelta` 5.0. |
| DMI/ADX | CalcDMI | 5m candles. `cfg.Indicators.ADX.TrendThreshold` (25). |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms, session-adjusted via `session_volume`. |

### Tier 1
| Indicator | Method | Config keys |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session. `cfg.Indicators.VWAP.WarmupCandles` (15). |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED when price between bands. |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | `cfg.Scoring.BbwSqueezePenalty` (2); `cfg.Indicators.TTM.FlatThreshold` (0.5). |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) regime anchor. |
| Funding Rate | GetFundingRateAsync | Step 3 baseline funding modifier from cfg thresholds. |
| Funding Momentum | CalcFundingMomentum | `cfg.Indicators.Funding.MomentumEnabled/WindowMinutes/Threshold/Amplify/Soften`. **v53: time-anchored** — delta vs the newest sample ≥ `momentum_window_minutes` (5) old, not a 3-*change* count window. Cadence-independent by construction. |
| OI Change | OiSnapshot | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL. |
| OI × CVD Cross-Confirm | Pass 2b | `cfg.Indicators.OiCvd.Enabled/UpgradeBonus/ConflictPenalty`. |
| Trend Structure (D1) | ClassifyTrendStructure | HH/HL/LH/LL classification on 5m. Pass 2c `StructureBonus` capped at regimeMax. |

### Tier 2
| Indicator | Method | Config keys |
|---|---|---|
| Bid-Ask Spread | order book | `cfg.Indicators.Spread.WidePenaltyThresholdBps`. WIDE penalty in Step 2. |
| OFI | CalcOFI | `cfg.Indicators.OFI.BookDepth` (5); `BuyDominantRatio` (2.0) / `SellDominantRatio` (0.5). |
| OFI Momentum | CalcOFIMomentum | RISING/FALLING/FLAT modifier. Ring buffer `_ofiHistory` (max=10). |
| Liquidations | CalcLiquidations | `cfg.Indicators.Liquidations.DominanceRatio` (2.0). |
| CVD | CalcCVD | 3-segment slope (late ×2 / early ×1 from cfg). `SlopeMinUsd` (12000). |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL + FLAT stall penalty. Dynamic accel threshold (`totalWindowUsd × pct` with static floor). |
| TFI | CalcTFI | BUY/SELL PRESSURE. Window=30, threshold=0.15. |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW regime anchor. |

### Tier 3
| Indicator | Method | Config keys |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT + quartile partial (`cfg.Indicators.Donchian.QuartilePct`=0.25). |
| OBV | CalcOBV | Trend + divergence gate. Adverse divergence blocks cross-category upgrade. |
| VPFR-lite v2 | CalcVPFRLite | POC proximity; VAH/VAL; `VPFRNearestHvnAbove/Below` for 3-tier target cap. Exp decay (0.985). |
| Swing Pivots | CalcSwingPivots | 5m primary (wing=3, lookback=30) + 15m context (wing=2, lookback=20). Direction-aware bookkeeping. |
| Volume-Weighted Pivots (D2) | extension of CalcSwingPivots | `BestPivotByVolume5m`, `BestPivotVolumeRatio5m`, `BestPivotIsHigh5m`. Display-only v1. v2 cap promotion parked as P1. |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK; forces NO TRADE on BLOCK. TTL cache 60s. 1-bar regime hysteresis. Three Reason formats by design (see `architecture.md`). |

---

## 5. Verdict Levels

| Verdict (displayed) | Stored / wire string | Meaning |
|---|---|---|
| STRONG LONG   | STRONG LONG  | High-confidence long |
| MEDIUM LONG   | LONG         | Standard long |
| WEAK LONG     | WEAK LONG    | Low-confidence long |
| NO TRADE      | NO TRADE     | Insufficient signal or MTF block |
| WEAK SHORT    | WEAK SHORT   | Low-confidence short |
| MEDIUM SHORT  | SHORT        | Standard short |
| STRONG SHORT  | STRONG SHORT | High-confidence short |

**Display ↔ stored mapping (v55, 2026-07-21).** The plaintext snapshot's `VERDICT:` line and the verdict card render the middle band as `MEDIUM LONG` / `MEDIUM SHORT` so the on-screen ladder reads STRONG / MEDIUM / WEAK explicitly. The **stored/wire** strings stay bare `LONG` / `SHORT` for the middle band — the CSV `Verdict` column, the bridge payload `verdict` field, the eval cache, and every string-matching site (`AnalysisLogger`, `SignalEmitter.DeriveDirection`, `LivePerformanceTracker`, `FailureRateMatrix.CanonicalTier`, `AutoTweakerCore`, etc.) are UNCHANGED. The frozen bridge contract routes actionability through `direction` + `confidence`; neither is touched. Divergence is deliberate on the two render surfaces only (precedented by the cap-reason rich string vs the CSV bucket).

`VerdictContext` tag (always rendered as a CONTEXT: line):
- **CONFIRMED** — directional call with cross-category support (only for directional verdicts)
- **ALIGNED** — sub-threshold bias has cross-category support (NO TRADE only, v30)
- **FLOW_UNCONFIRMED** — score qualifies but order-flow indicators contradict
- **MOMENTUM_FADING** — score qualifies but momentum is decaying
- **STRUCTURALLY_WEAK** — swing data exists but no clean target+stop pair

---

## 6. settings.json — operational pointer

**Source of truth:** `settings.json` itself + its inline `change_log` array.

Current version: **v64**. Top-level blocks:
- `indicators` (per-indicator parameter blocks; incl. `aggressor_velocity` — v50 #5 build, three-tier tweaker surface, HARD CONSTRAINT 19; `OFI.momentum_*` fenced off the tweaker surface — v50 retune R1, HARD CONSTRAINT 20)
- `session_volume` (UTC bucket multipliers + per-session `execution_resolution` — v36; + per-session `roc_magnitude_threshold` — v40 Asia/London (B) re-baseline)
- `resolution_profiles` (per-resolution ROC threshold overrides keyed by "1"/"3"/"5" — v36; 3-min `roc_slope_delta_threshold` re-baselined 0.105→0.06 in v40)
- `mtf_gate` (15m gate configuration)
- `auto_run` (auto-run timer + `trigger_mode` — `interval` | `on_close` bar-close firing, v44; off the auto-tweaker surface)
- `scoring` (verdict thresholds, regime max scores, tier floors, context tag thresholds, hold thresholds — the `hold_*` keys are off the auto-tweaker surface, HARD CONSTRAINT 17 — v47)
- `kelly` (display-only sizing block)
- `regime_gates` (TRANSITIONAL ADX penalties)
- `regime_weights` (Pass 2c alignment bonus/penalty)
- `network` (HttpClient timeout, retry config)
- `performance_display` (live perf strip + OHLC gap-fill + metric mode)
- `analysis_logging` (output dump)
- `exit_guard` (realtime exit-guard overlay — display/alert only, off the auto-tweaker surface — v43)
- `live_strip` (live microstructure TAPE strip — display/awareness only, NOT a verdict, off the auto-tweaker surface — v45)
- `signal_bridge` (verdict_signal.json emission to the order app — transport plumbing, off the auto-tweaker surface, HARD CONSTRAINT 18 — v49)
- `alerts` (#7 liq-cascade alarm + #8 level-approach alerts — display/alert only, off the auto-tweaker surface, HARD CONSTRAINT 25 — v59)
- `trade_store` (raw-trade capture to the backtest store — streaming capture + gap repair; **no rendered surface at all**, off the auto-tweaker surface, HARD CONSTRAINT 27 — v64)

**Workflow when adding new config keys:**
1. Add the corresponding POCO field in `Core/Settings/EngineSettings.vb` with `<JsonPropertyName(...)>` attribute and a sensible default.
2. Bump the top-level `version` integer in `settings.json`.
3. Append a new entry to the `change_log` array (newest first), referencing the spec doc.
4. Add a one-line entry to §15 *Recent Changes* below.

Pre-v22 settings change rationale and earlier audit-trail commentary lives in `docs/history-archive.md` §A. Settings change history beyond the last five versions is in the `change_log` array within `settings.json` itself.

---

## 7. ScoringEngine — Key Behaviours

- **MaxScore:** base values from `cfg.Scoring.RegimeMaxScore.*` — TRENDING (19), RANGE_BOUND (18), TRANSITIONAL (15). With `RegimeWeights.Enabled` (default), TRENDING → 20 and RANGE_BOUND → 19 (base + AlignmentBonus). TRANSITIONAL unchanged.
- **Verdict thresholds:** `Math.Ceiling(regimeMax * pct)`.
- **Step 2:** Score signals into ScoreState. All thresholds from cfg. Includes bid-ask spread WIDE penalty and OFI momentum modifier.
- **Pass 2:** Upgrade partials on cross-category confirmation; OBV upgrade blocked on adverse divergence.
- **Pass 2b:** OI × CVD cross-confirm gate. Full OI + matching CVD → `UpgradeBonus`. Full OI conflict with CVD → `ConflictPenalty`. Partial OI confirms upgrade-eligible; partial conflict non-penalising.
- **Pass 2c:** Regime alignment gate. Suppressed in TRANSITIONAL and on zero-net scores. TRENDING checks EMA ribbon + ROC (active when |ROC|≥`MagnitudeThreshold`) + CVD slope+sign. RANGE_BOUND checks VWAP dev (warmup-gated) + RSI vs `Pass2cMidline` (50) + Donchian. All aligned → `+AlignmentBonus` (capped at regimeMax). All conflict → `-ConflictPenalty`. D1 trend structure contributes `+StructureBonus` when structure agrees with dominant side.
- **Step 3:** Baseline funding-rate modifier.
- **Step 3b:** Funding-momentum modifier (soften crowding when momentum falling; amplify when momentum rising into crowding).
- **Step 4:** Regime veto + TRANSITIONAL ADX penalty.
- **Step 4b:** MTF gate veto → forces NO TRADE.
- **Step 5:** Threshold comparison → verdict.
- **Step 5b (placed-level arbitration — v51 B4b structural-first):** delegates to the ONE shared seam `SignalEmitter.ComputeSideLevels`. Target ladder swing → nearest HVN → POC (HVN-gated) → ATR fallback, priority-with-looseness-bound (`structural_levels.target_max_atr_mult`); stop = min(structural swing stop, `stop_max_atr_mult`×ATR) per DG1. Copies the placed outputs onto `AdjustedLongTarget`/`AdjustedShortTarget` + `TargetCapReasonLong`/`TargetCapReasonShort` (split B1 2026-05-12); **Step 5c (v35 min-move gate) evaluates the PLACED target.** With `structural_levels.enabled:false`: the legacy 3-tier closest-wins CAP (winner = closest cap to entry), byte-identical to v50.
- **Step 5b (VerdictContext):** FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED. **NO TRADE special case (v30):** CONFIRMED relabels to ALIGNED. Decay ratios + count thresholds from `cfg.Scoring.ContextTagThresholds.*`.
- **Step 6 (CalcHoldStatus — layered exit):** Layer 1 microstructure (2+ adverse → fast EXIT) → Layer 1.5 structural break (prior swing breached) → Layer 2 momentum break (ROC crosses 0) then OBV divergence → Layer 3 RSI divergence / single adverse / RSI+ROC structural. Only renders when `posState ≠ None`.
- **Step 7:** placed levels rendered from `ComputeSideLevels` (fallback multipliers `cfg.Scoring.AtrTargetMultiplier`/`AtrStopMultiplier` 1.75/1.6 + `structural_levels` bounds/sessions — see §8). **Structural rows** rendered in UI alongside (cyan when both target+stop exist, dim when partial). v30 `FormatRR` uses `< 0.1` literal for sub-1dp ratios.
- **CalcKellySizing():** called from `RenderOutputHeader` after ATR levels. Display-only, zero scoring impact.

For full annotated `Calculate()` pipeline detail, see `docs/architecture.md`.

---

## 8. ATR Entry / Stop / Target Display

- **Entry price** = `candles1m.Last().Close`.
- **Last transacted price** = `recentTrades.Last().Price` — displayed above ATR block, not used as entry. (Trade lists are chronological ascending since the v31 correctness pass; the most recent trade is the LAST element.)
- **[v51 B4b] Placed levels are STRUCTURAL-FIRST** — every consumer (snapshot, card, bridge payload, CSV `Placed*`) reads the ONE shared arbitration `SignalEmitter.ComputeSideLevels`. Target ladder: swing → nearest HVN → POC (HVN-gated) → ATR fallback (first tier with 0 < dist ≤ `structural_levels.target_max_atr_mult`×ATR places; structure wins even when farther than the ATR level). Stop (DG1): min(structural swing stop, `stop_max_atr_mult`×ATR) — labels `SWING_STOP` / `STOP_CLAMPED` / `FALLBACK_ATR` on the rendered rows. `structural_levels.enabled:false` reverts byte-identically to the legacy geometry below.
- Fallback/legacy: Long Stop = price − ATR × `AtrStopMultiplier` (1.6); Target = price + ATR × `AtrTargetMultiplier` (1.75 global; LONDON 2.0 / ASIA 1.25 via `structural_levels.sessions`). Short mirrored. Fallback R:R ≈ 1:1.1.
- **Structural placement display:** fallback target shown ahead of an arrow to the placed value with reason label (e.g. `PLACED @ 95200.0 (SWING_HIGH_5M)`; legacy path renders `CAPPED @ …`). **v30 sub-tick suppression:** when `|fallback − placed| < max(0.5, ATR × 0.02)`, the label is hidden; target renders as a normal value (CSV `TargetCapReason` still populated for analytics).
- **Multipliers read from cfg** — labels and R:R display are dynamic, not hardcoded.
- **Structural rows** below ATR block: `Long structural: Stop X | Entry X | Target X  R:R 1:N  (risk X / rwd X)` in cyan when both target+stop exist; dim with per-side missing-data note when only one side (v30 F12 wording). Mirror for short.
- **Kelly Sizing block** rendered after ATR levels. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face. Advisory label notes R:R is ATR-basis (not structural). EST mode only — CAL mode will return when backtesting module ships empirical per-tier win rates. Suppressed when KellyF = 0. v30 plural fix: `1 contract` / `N contracts`.
- **Funding display** (FUNDING section): rate row + momentum row. v30 negative-zero clamp at both display sites.

---

## 9. Open Position Guidance (CalcHoldStatus)

Priority order (v47 N2 — corrected to match the code, pinned by harness A17g): (1) 2+ adverse microstructure signals → fast EXIT → (1.5) structural break exit (price closed at/below prior swing low for long; at/above prior swing high for short) → (2) momentum-break exit (ROC crosses zero against the position) → (3) OBV divergence exit → (4) RSI divergence evaluate → (5) single adverse microstructure warning → (6) RSI/ROC structural assessment.

All RSI/ROC thresholds read from cfg (`HoldRoc*`, `HoldRsi*`).

**Render gate:** the `HOLD \ EXIT:` line and the CalcHoldStatus output render in UI only when a position has been declared via the position radio buttons (`posState ≠ None`). When no position is held, the entire hold-guidance block is suppressed. See `architecture.md` *Display Behaviour Clarifications*.

---

## 10. CSV Logging & Auto-Run

- `AnalysisLogger.LogRun(r, verdict)` → `analysis_log.csv` in exe directory. v0.4 schema (87 columns).
- `CalibrationReport` summarises recent directional accuracy.
- Auto-run timer driven by `MainForm_AutoRun.vb`; interval configurable from UI (min 10s).
- `VerdictContext` column may carry `ALIGNED` on post-v30 NO TRADE rows.
- OI × CVD Pass 2b outcome surfaced in `OiCvdOutcome` CSV column (Bundle 1).

---

## 11. DynamicNorms

`DynamicNorms.Compute(candles1m, r.ATR)` computes per-run:
- `ATRScaleFactor` — current ATR vs reference; scales stop/target distances.
- `VolHighThreshold` / `VolMidThreshold` — regime-adjusted volume thresholds.
- `VWAPDevThreshold` — dynamic VWAP deviation threshold (clamped from settings).
- `ApplySessionVolume()` — session-aware post-adjustment that applies ASIA / LONDON / NY bucket multipliers from `SessionVolumeSettings`.

---

## 12. WATCHING / Calibration Backlog

Currently-open items pending live-data review. **Roadmap absorption (2026-07-02):** the Low-priority threshold-sweep rows below (funding momentum, session multipliers, OI×CVD, TFI, TTM, VPFR buckets, liq ratio, ContextTag, Kelly, swing wing) are collectively absorbed by the **roadmap W1 signal-health audit** (`docs/roadmap.md`) — they get data-driven verdicts there rather than one-by-one passes. Rows with a specific re-home are annotated in place. Resolved rows move to `history-archive.md` §G.

| Item | Description | Priority |
|---|---|---|
| **v52 aggressor-velocity wire-in post-ship watch (S5.2)** | On post-v52 NY×1 rows, first 2 weekday sessions: (1) **NY burst fire rate** stays in the **8–12%** band (`AggrVelSignal ∈ {BURST_BUY,BURST_SELL}` ÷ non-empty AggrVel rows at `ExecResolution=1`; the 07-13 derivation put T=4.5 at 9.9%, the 07-13-evening out-of-sample partial at 8.9%); (2) **same-side share** P(TFI same side \| BURST) **≥85%** (derivation 89.7%); (3) **TFI-modifier engagement** ≈**5–10%** of NY directional votes (~35 upgrades + ~2 softens/NY session-day — the burst suffix appears on ~7% of TFI breakdown rows; net Microstructure fire count unchanged by construction, it's a modifier not a vote). Trigger: fire rate outside 8–12% or same-side <85% across 2 consecutive weekday sessions ⇒ re-run the §5.2 derivation (T re-fit). **Res-3 (Asia/London)** stay display-only until ~150 fires/session accumulate → their own §5.2 pass sets `sessions.{LONDON,ASIA}.burst_ratio_threshold` (which ALSO arms the S2a scoping there). **✅ THAT CLAUSE IS NOW SPENT — LONDON was armed at v60 (2026-07-23) and ASIA at v65 (2026-08-02, D3), so NO res-3 session remains display-only.** The ASIA arming carries its own multi-day watch in [`trader-tick-queue.md`](trader-tick-queue.md) §4. Recipe: `aggressor-velocity-s52-derivation-2026-07-13.md` §7 + Appendix. | Medium (watch) |
| **Funding momentum — time-anchored window post-ship watch** (v53, SHIPPED 2026-07-15) | **The build landed** (`funding-momentum-time-anchored-window-proposal.md`, D1–D5 ticked; spec-back `funding-window-spec-back.md`) — timestamped ring, delta vs the newest sample **≥5 min** back, **T=2e-7**, 30-min eviction, cold-start/post-gap FLAT. Bundled at the **v52 boundary** on trader sign-off (not its own, superseding D4 — the v52 wire-in went live the same evening with negligible rows between; v52+v53 share ONE dataset boundary). **THE WATCH — re-run the retune §5 check PER-RESOLUTION** on post-v53 rows: FLAT **60–70%** + Step-3b engagement **15–25%**, and **both res-1 AND res-3 must sit in-band** — that dual-resolution pass is the success criterion the count window could never meet (it read FLAT 52%/engaged 28% on on-close NY-60s and FLAT 0%/engaged 96% on 3-min sessions, where a single 3-min step p50 6.5e-7 exceeded the whole 2e-7 window threshold — arithmetic, not sampling). Expect engagement **above** band on hot-funding weeks and **below** on quiet ones (10–34% across the two fit segments) — by design; once anchored, above-band engagement during a genuine crowding build is honest signal, not the cadence artifact R2 chased. Trigger for a T re-fit: a **regime-independent** miss (both resolutions out of band in the same direction across 2 weekday sessions), not a single hot week. **Pre-v53 rows are not comparable** — res-3 `FundingMomentum` was uninformative on the old window. | Medium (watch) |
| **Session volume multipliers** | Review ASIA/LONDON/NY `vol_high_mult` / `vol_mid_mult`. **🔒 PARKED 2026-07-31 behind the D3 forming-bar ruling (JOB 2 decision D-C).** The lever is inert live and the mechanism is confirmed in code, not inferred: `DynamicNorms.vb:36-40` computes the threshold over the most recent 100 **completed** bars — the in-progress bar is excluded by explicit comment — while `MainForm_Analysis.vb:237` sets the ratio's numerator to `candlesExec.Last().Volume`, i.e. **that same excluded bar**; and `CalcVolumeSMA` (`Indicators_Momentum.vb:290`) includes it in the 9-bar denominator too, compounding the same direction. Result: the volume vote fires on **0.69% of NY** runs and **2.66% at ExecRes 3** — quote the 2.66% for the queued LONDON/NY passes, not the NY figure, so the argument is not overstated against them. Tuning a multiplier against a threshold the input does not reach is tuning a dial on nothing, and a closed-bar-derived multiplier would describe the D3 closed-bar arm rather than the live engine. **Rider:** `auto_run.trigger_mode` is not a CSV column, so the book cannot record which trigger mode scored a row — it rides the next natural rotation alongside the J-E effective-source stamp, and **neither forces one**. **Also flagged:** v58's *direction* stands, but its stated *mechanism* (the notch "suppressing trades") can account for at most a fraction of a pp through the volume channel — do not re-apply that reasoning to LONDON/NY without this context. | Blocked (D3) |
| TFI threshold | BLOCKED — the W1 audit (2026-07-03, F11) found TFI is **not logged at all** (no CSV column), so the sweep has no data. `TFIValue`/`TFISignal` columns ride #5's v0.7→v0.8 rotation (retune spec C1, APPROVED); becomes measurable at the next audit re-run. | Blocked (data) |
| **v51 placed-geometry post-ship watch (B4b)** | On post-v51 rows, first weekday sessions: (1) realized structural-target reach-rate vs the fallback's; (2) **STOP_CLAMPED frequency** (expected to bind on MOST structural-stop rows at v1 fixed sizing — the un-clamp waits on consumer sizing-by-stop-distance, derivation §6b); (3) BELOW_MIN_MOVE rate (projected +4–6pp NY, ~0 elsewhere); (4) **LONDON structural-target inversion (F3/DG4)**: in-bound structural reach was 33% vs fallback 61% (n=227) — trigger: still <45% after ≥3 more LONDON session-days ⇒ London session override or bound tightening. Recipes: `placed-geometry-spec-back.md` §5 (`TargetCapReason` bucket = structural placement; `PlacedStop*` at exactly ±1.6×ATR with `SwingStop*` present = clamped). **📖 READ 2026-07-31 at the W6-1 ruling** ([`w6-1-london-ruling-2026-07-31.md`](w6-1-london-ruling-2026-07-31.md) §3), pooled AWS-preferred weekday post-07-08: **(2) STOP_CLAMPED = NY 99.6% / LONDON 95.5% / ASIA 96.0%**, stop-distance/ATR ratio p50 **and** p90 both exactly 1.60 — binds harder than the 92% on file, so stops are de facto ATR stops and the live question moves to **L9 un-clamp** (gated on L3). **(3) BELOW_MIN_MOVE = NY 23.35% / LONDON 15.16% / ASIA 18.28%** (absolute rates; the §12 projection was a *delta* against a pre-v51 baseline this book cannot reconstruct — recorded as the new baseline). ⚠ **(1) and (4) are NOT RUNNABLE — the F3 trigger cannot currently be evaluated.** Both need outcomes segmented by **cap bucket**, and nothing produces that any more: the 2026-07-21 placed-target migration retired the geometry axis (`FailureRateMatrix` is now `tier × window`), the what-if runner reads `TargetCapReason` only to exclude `poc` (`WhatIfProgram.vb:91-92`), and `CeilingAudit` carries it only as an AUC info-categorical. **The F3 watch outlived its instrument** — either add cap-bucket segmentation to an offline surface or retire the trigger explicitly; do not leave it live and unevaluable. CSV-only companion: LONDON directional placement is structural **41.0%** (swing 33.6 + hvn 7.4) vs fallback 59.0%. | Medium (watch — F3 arm blocked on tooling) |
| **TTM flatThreshold** | ~~Default 0.5; review FLAT vs RISING/FALLING against 1m candle range distribution.~~ **DERIVED 2026-07-31 — the row asked for the wrong quantity.** The knob gates the 7-bar drift of `close − SMA20` (`Indicators_Volatility.vb:149-150,173-175` — a raw USD difference, no normalisation), **not** the candle range; that substitution is why the inherited ×2.1 proxy is wrong (measured 3m/1m ratio **1.45**). **Finding: 0.5 is ~100× too small** — median \|delta\| **52.5 / 76.5** on 1m/3m, and 0.5 sits below the **1st percentile** of both, so the FLAT band is inert (FLAT fires 0.69% / 0.61% live). Reproduced on two independent instruments (6-month store replay + 8,452 live rows). ⚠ **Do NOT inherit the recommended 25.0/40.0 ladder** — [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) §2 shows its anchor is a mis-identified quantity: `AWARD%` is **not** the vote, because the TTM award block is nested under `Case "RELEASING","NONE"` (`ScoringEngine_Calculate_Scoring.vb:254-277`) and awards nothing on `ACTIVE` rows (29.2%/24.5%). The real vote rate is already **43.4% / 46.3%**, so the ladder would push it to ~34–36%, the wrong way. Re-derive against the post-gate vote rate; take the 3m value off the measured 1.45, not the 40÷25 grid quotient. **⚠ live scoring change ⇒ own spec + dataset boundary** (D-A, direction-only). | Medium (re-derive the anchor) |
| VPFR numBuckets | Default 50; review POC resolution on quiet sessions. | Low |
| Liq dominanceRatio | Default 2.0; review false signals; consider raising/lowering after live observations. | Low |
| ContextTag thresholds | Review FLOW_UNCONFIRMED hit rate after 50+ trades. | Low |
| **Kelly `est_prob_floor`/`scale` + the EST advisory promise** | Default 0.45 / 0.20 ⇒ p(win) HIGH 0.65 / MEDIUM 0.55 / LOW 0.45. **MEASURED 2026-08-01 (F1 §9): STRONG 46.8% · MEDIUM 42.2% — 13–18pp below the assumption, and both under the 47.76% Kelly breakeven at `b`=1.0938.** Trader decision 2026-08-02: **keep EST, wait for separation, and say so on screen** — the block now renders *"p(win) is ASSUMED from the confidence tier — Actual numbers after next book doubling."* ⚠ **That line is a forward PROMISE, so this row is the watch that keeps it true.** **Trigger: ≥406 pooled weekday STRONG** (double the 201 at the read) on the AWS-preferred deduped book — **ETA ~2026-08-30** at the measured two-box rate of **12.4 STRONG/weekday**. **Bundle with the W6-4 re-run**, which lands in the same window on its own basis. **On trigger:** re-run the §9 band ladder, re-work the f\* table at the then-current `b`; if the ladder separates AND STRONG clears breakeven ⇒ CAL becomes arguable and the advisory retires itself (the mode tag is data-driven off `KellyPMode`); if not ⇒ **the line must be re-worded or the block suppressed — it must not silently promise another doubling.** Evidence: `f1-tier-ladder-read-2026-08-01.md` · decision: `kelly-est-honesty-decision-2026-08-02.md`. | **Medium (watch — dated trigger)** |
| **v28 target-hit vs barrier-hit gap** | Post-v28 data should show ~30-50pp gap between target-hit and barrier-hit rates. If gap is small, direction calls themselves are bad; if large, stops are too tight. Probe on 2026-05-15 showed +35pp on 67-row sample — needs validation on larger sample. | Medium |
| **First auto-tweaker live fire (supervised)** | The fixed-mode tweaker has never fired live. All original gates are long since met (v35 de-confound ✅, Phase-2a NY×1 population filter ✅ + replay-validated, `window_size_verdicts` 75 interim ✅); the remaining gate is **data**: a real >40%-failure NY×1 window must appear in the live book. When it does: supervised **dry-run first** (`dry_run_enabled: true`, `auto_commit_enabled: false`), validate snapshot/apply/revert/streak, diff reviewed before any apply. Roadmap W5. *(Status text refreshed 2026-07-02 — the old "gated on v35" wording predated v35 shipping.)* | Medium (data-gated) |
| **Auto-tweaker tier-eligibility floor vs the v35 book (MinTier mismatch)** | v29's `min_tier_eligible_rows` default auto-scales as `max(15, ceil(WindowSize×0.5))`, assuming ~50% of window rows are tier-eligible (STRONG/standard LONG/SHORT only — WEAK and NO TRADE are excluded, `AutoTweakerCore.vb` lines 214-218). The conservative post-v35 engine delivers far less: NY measured **~23% actionable-directional** (128/551 NY rows, 2026-06-15; remaining 37% WEAK + 30% NO TRADE both excluded). So the shipped 30-row window / MinTier 15 can **never** clear the floor on the live book — a 30-row window averages ~7 eligible vs the 15 needed, so every block trips `SKIPPED_INSUFFICIENT_TIER` and burns the rows without firing. **Interim (2026-06-15):** bumped `window_size_verdicts` 30→75 in `tweaker_config.json` for the supervised first fire (keeps MinTier 15 statistically meaningful at ~17 eligible/window). **Proper fix:** recalibrate the window/MinTier pair against the real post-v35 directional rate, per session (NY ~23%; Asia/London likely lower → may need session-specific windowing — dovetails with the session+resolution filtering the tweaker needs before tuning beyond NY/1-min anyway). | Medium |
| **v30 ALIGNED frequency** | Post-v30, NO TRADE rows that previously rendered CONFIRMED now render ALIGNED. Watch the CSV's `VerdictContext` distribution. | Low |
| **v36 Phase-2 threshold carry-forward (deferred 3-min scaling)** | Two refinements deferred from v36 Phase 1 (coordinator spec-back review 2026-06-15) — fold into the Phase-2 per-resolution re-baseline. **(1) Secondary candle-magnitude keys held at 1-min:** `TTM.flat_threshold` + the CVD/RSI `divergence_price_gate`s are candle-magnitude (same class as the two ROC keys that scale ×2.1) but were kept at 1-min for a minimal Phase-1 seed. Un-scaled on 3-min they make divergence/squeeze fire **more eagerly** — conservative (aligned with low-FP tolerance) but risks **over-suppression** (missing valid setups). Phase 2: scale ≈2.1× or re-measure, and check the Asia/London suppression rate. **(2) DynamicNorms baselines span 3× wall-clock on 3-min:** keep-count is correct for Path B, but the 100-bar volume / 50-bar VWAP-dev baselines = 5h / 2.5h at 3-min, so the 5h **LONDON** session never gets a pure-session volume baseline (always pulls adjacent-session character; on 1-min it's pure mid-to-late session). Bounded by `session_volume` + self-scaling so not a Phase-1 blocker — but Phase 2 should check whether LONDON's adaptive volume / VWAP-dev thresholds want a session-scoped baseline window. Source: `session-timeframe-resolution-implementer-handoff.md` §1/§4 + spec-back §2. | Medium |
| **Auto-tweaker resolution-awareness (Phase-2 precondition)** | The tweaker's fixed-window slicer walks disjoint **chronological** row slices and is **resolution-blind** (compounds the existing session-blind gap). Before it tunes on any post-v36 data it MUST filter the failure-rate matrix + CSV rows by `(session × resolution)` so it never pools 3-min Asia/London with 1-min NY. The `ExecResolution` CSV stamp (v0.7) makes this possible; `execution_resolution` is already on HARD CONSTRAINT 11's exclusion list (it can never *propose* a resolution change). **Hard ordering:** the v35 NY/1-min supervised first-fire dry-run must consume the clean 1-min history **before** v36 ships 3-min rows (once 3-min rows interleave, no later window isolates a pure-1-min population). Spec the `(session × resolution)` filter as a Phase-2 item before any un-gated fire on mixed data. **Phase-2a addressed (2026-06-17, `auto-tweaker-session-resolution-filter-implementer-handoff.md`):** load-time `(session × resolution)` population filter in `AutoTweakerCore` (consumes `CsvRow.ExecResolution`, derives session via the shared `MatchSessionBucket`; initial population NY×1), state re-seed on filter change (`TweakerState.PopulationFilterKey`), and a **code-level** off-surface reject in `SettingsDiffApplier.Validate` (`kelly.*` / `resolution_profiles.*` prefixes + `scoring.min_tradeable_move_pct` exact — hardens the previously prompt-only HARD CONSTRAINT 11). Harness A15a–A15g + A1–A14 green. **Carry-forward (Phase-2b):** per-population `LastEvaluatedRowIndex`/round-robin, per-population `WindowSize`/`MinTier`/threshold, picked-cell/round-history segregation by population, the schema-home decision for session-specific tuned values, and the wholesale-revert-vs-manual-`resolution_profiles["3"]` interaction (a revert restores `resolution_profiles` from the snapshot — supervised/`auto_commit`-gated, flagged not fixed). The **manual `resolution_profiles["3"]` re-baseline** (the Asia/London accuracy fix) is a separate data-gated settings pass, NOT the tweaker. Source: hand-off §6.1/§6.3 + spec-back §4. | Medium (Phase-2a done; Phase-2b open) |
| **v48 OFI per-session fire-rate watch (spec §4a)** | Global pair 1.60/0.625 shipped 2026-07-03 with LONDON's per-session fit at +11.7% vs pooled (n=200 — within percentile-fit sampling error) and ASIA unfittable (geo n=69; zero reference-period Asia rows). After **≥2 further weekday session-days** on the shipped pair: recompute per-population BUY/SELL/combined dominance rates at 1.60/0.625 (recipe in `v48-ofi-dominance-rebaseline-spec-back.md` §4). **Trigger:** any population's combined dominance rate outside **[0.6×, 1.5×] of the fitted 63.2% target across 2 consecutive weekday sessions.** Response ladder: (a) verdict-impact check (OFI is 1 vote of ~20) → (b) pooled retune incl. the new data → (c) per-session bucket overrides (last resort — new nullable `session_volume.sessions[].ofi_*` keys, hand-tuned/off-tweaker by construction, own small spec). The W1 signal-health audit reports these rates automatically on every re-run. | Medium (watch) |
| **WS 3-min closed-bar volume undercount (cutover watch)** | 12h soak (2026-06-23) found WS `chart.trades` 3-min bars undercount REST volume by ~2.5% (OHLC exact; systematic, always ws-low; 8 bars). DECISION (`websocket-migration-p3-cutover-spec.md` §7): accept via a relative-volume tolerance on `ShadowParityComparer` — immaterial to scoring in normal flow. **Watch during volume spikes** (breakout-confirm, vol > 3× SMA-9): check whether the undercount could pull a WS reading below the 3× gate when REST clears it — the one place 2.5% could flip a signal. Live only post-cutover / under `shadow_parity`; escalate to fixing the WS bar aggregation (P3 §7 option b) if it trips. | Medium |

Earlier ✅ Done items moved to `history-archive.md`.

---

## 13. Future Upgrades

> **Sequencing authority: `docs/roadmap.md`** (2026-07-02) — the cross-project strategic roadmap (workstreams, execution order, the DeribitOrderPlacementApp signal bridge, Linux port). §13/§16 remain the engine-local backlog detail; when they disagree with the roadmap on *order*, the roadmap wins.

Ranked by expected accuracy / reliability gain. Items marked 🔍 require a spec decision before coding begins.

### High-Impact (deferred until post-WebSocket or post-calibration)

| Item | Description | Status |
|---|---|---|
| WebSocket migration | Real-time order book + trade stream vs REST snapshot polling. Was the single highest-impact non-indicator upgrade; gated the Section-A microstructure class. | ✅ **SHIPPED** — P1 v38 → P2 v39 → cutover **v42 (2026-06-24, live on WS)**. The unlocked items it gated are now sequenced in `docs/roadmap.md` W1/W2 (spread revival, #5 aggressor velocity, #6 absorption, A4 liq×OFI). |

### Moderate-Impact (post-calibration spec work)

| Item | Description | Status |
|---|---|---|
| RSI divergence on 5m candles | 1m + 5m divergence simultaneously = stronger penalty. Requires `CalcRSIDivergence` on `candles5m` + combined gate. | 🔍 Deferred — see `post-websocket-post-calibration-backlog.md` D3. |
| Donchian × BBW state cross-reference | Wide-channel vs tight-channel breakout differentiation. | 🔍 Deferred — see `post-websocket-post-calibration-backlog.md` D4. |
| B1 per-indicator regime weights | Full per-indicator weighting scheme. STUB blocked on Bundle 1 empirical hit-rate output. | 🔍 STUB. |

### Auto-tweaker calibration

| Item | Description | Status |
|---|---|---|
| Auto-tweaker first-fire validation | Once `LastEvaluatedRowIndex` clears WindowSize threshold and a round fires, verify streak / snapshot / revert paths end-to-end on real data. | Pending data accumulation. |
| Auto-tweaker tuning | Once 50–100 rounds have completed, recalibrate `failure_rate_threshold_pct` (default 40), `min_tier_eligible_rows` formula, `streak_weight` (1.5). | Pending data. |

### Accuracy Ceiling Note

*(Premise refreshed 2026-07-02 — the original note predated the WS cutover.)* The REST-polling latency floor is **gone** (v42: 100ms-fresh book/trades/ticker, on-close bar triggering). The binding constraints on further accuracy are now the three risk thresholds themselves, unchanged and more relevant than ever:

1. **Overfit risk** — the number of tunable parameters continues to rise; session-bucket multipliers and per-tier thresholds should be validated against forward runs, not a tiny historical slice.
2. **Signal redundancy** — OFI + TFI + CVD + MicroCVD already cover order flow from four angles; the roadmap W1 **signal-health audit** is the standing instrument for this (fire rates, pairwise agreement, conditional outcomes → keep/retune/retire with evidence), and any new flow signal (#5/#6) must be specced against it.
3. **Interpretability** — adjustments should remain easy to reason about from DynamicNorms and settings. If traders cannot quickly tell which bucket is active and why a threshold moved, the engine becomes harder to trust — doubly so now that the O2 signal bridge lets an autotrader act on the output.

---

## 14. Backlog

*(cleared — all spec bundles 1–3 + v27-v30 features shipped; remaining items are calibration review or future spec work tracked in §12 and §13)*

---

## 15. Recent Changes

Most recent five settings.json versions. Full history (v0.33 through v26) lives in `docs/history-archive.md` §E.

| Version | Date | Summary |
|---|---|---|
| **v65 · ASIA aggressor-velocity arming** (D3) | 2026-08-02 | **`indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` = 5.5, previously absent — ASIA inherited the exploratory default 2.5 for classifier/TAPE-display/CSV while the SCORING modifier stayed inert there. ⚠ LIVE SCORING CHANGE AND A DATASET BOUNDARY:** rows after this build carry the burst-modified TFI vote on ASIA; NY (4.5) and LONDON (5.5) unchanged. Trader-ticked 2026-08-02. Evidence + D-table: `asia-burst-threshold-derivation-2026-08-01.md` §5. **Sequencing:** the 2026-08-01 D-cluster ruling puts D3 **alone and first**, ahead of the D1+D2 bundle — it *arms* rather than retunes, and bundling it with D2 (`OBV trend_gate` 18→23) would confound the one session whose watch justifies it, both pushing the ASIA upgrade path the same way. **Mechanism — no change to the scoring path:** threshold *presence* is what arms a session (`ExecutionResolution.HasExplicitAggrVelBurstThreshold`, the wire-in D1 auto-arm), so setting the key arms ASIA through the shipped v52 seam in `ScoringEngine_Calculate_Scoring.vb` Step 2. **The POCO default in `EngineSettings.vb` moves in lockstep (v60 LONDON precedent)** — the harness builds every cfg from `New EngineSettings()`, so a JSON-only edit would leave app and harness pinning different behaviour. **Data gate met comfortably:** 323 fires at the exploratory default against the ~150/session bar the 07-13 NY pass deferred res-3 on; n=1,489 ASIA rows over 14 session-days, pooled AWS-preferred book, weekday-only, frozen. **Why 5.5:** fire rate **9.7 %** against the ~10 % design point and inside the 8–12 % band (the live default 2.5 fires on **21.7 %** — one bar in five, the always-on failure mode the audit retired non-directional padding for); ASIA p90 **5.35** → nearest grid value, identical construction to NY (4.51→4.5) and LONDON (5.69→5.5); same-side TFI **91.0 %** over the ≥85 % bar. **Decision-of-record:** no ASIA-vs-LONDON split — the res-3 burst distribution is **one** distribution (every percentile within ±9 % on n≈1,489 each), `burstRatio` being scale-free by construction; ASIA takes LONDON's value, arrived at independently, which is itself the evidence the method transferred. Kept as **two identical per-session entries, not promoted to `default`** — the auto-arm keys on session-threshold presence, so `default` would silently arm every session including any future one. **⚠ Honest caveat, recorded not buried:** the W6-4 informational column is the **only** outcome-linked read on this knob and is **neutral at best** — `AggrVelBurstRatio` AUC **0.5179** (n=217), `AggrVelNet` 0.4654. It does not refute arming (NY/LONDON were armed distributionally too, LONDON's watch passed; a ±1 modifier on ~10 % of rows would not show strongly on ~220 test rows) but the distributional case must not be led with alone, and it sits inside the **three-instrument finding** (engine score AUC 0.5407, tier bands do not separate, structural placement 0.4683/0.5000/0.3221) — no further scoring spend without weighing it. **Contra arm effectively dead on res-3** at every candidate T including LONDON's shipped 5.5 (ASIA 0.21–0.43 contra/day, LONDON 0.16–0.42), so criterion (d) cannot discriminate: on 3-minute sessions this modifier is in practice **upgrade-only** — not a defect, but the §4.5 warning half must not be claimed as operative on res-3. **Coupling caveat binds:** 5.5 is derived *for* `fast_window_sec` 5 / `norm_window_sec` 120. **Engagement preview** (computed from the derivation §1, not separately measured): ~10.4 fires/day, ~9.5 same-side upgrades, ~0.29 contra, on ~106 AggrVel rows/day. **Post-ship watch:** fire rate ≈9.7 % and same-side ≥85 % over a **multi-day band, never one session-day** — ASIA's per-day rate spans **4.5–13.8 %** at row density ~106/day, so NY's ±2pp band does not transfer. **Tweaker: no new fence, HC28 stays free** — `indicators.aggressor_velocity.sessions.` is already prefix-rejected under **HC19**, re-verified. **Display-string parity: no new obligation** — the burst suffix appends to the existing TFI breakdown note row and the card binds the same `SignalBreakdownItem.Note`; arming ASIA only makes an already-rendered suffix *reachable* there. **Reversibility:** remove the ASIA key from JSON **and** POCO ⇒ inert again, byte-identical to v64, pinned by A28c's constructed un-armed arm; `scoring_enabled:false` remains the whole-feature hot rollback. **Fixtures: A28c RE-PINNED** — ASIA *was* the un-armed exemplar and all three shipped sessions are now armed, so no session remains to demonstrate S2a scoping; the un-armed arm is now **constructed** (ASIA's threshold cleared on a cfg copy), pinning the mechanism rather than an incidental session. A23f comment corrected (stale since v60). **New A52a** pins the JSON→arming contract in three arms — present ⇒ armed @5.5, absent ⇒ inert @2.5, and a **JSON↔POCO drift guard**. Acceptance: solution + AutoTweaker + WhatIfRunner + BacktestRunner + CeilingAudit + OrderCheck build **0/0** Release; harness **ALL PASS** incl. A28c/A23f/A52a. **Rider travelling with this tick** (queue §3 — it could not travel alone): the v64 reversibility wording corrected below. Local commit — trader tests + pushes. |
| **v64 · in-app trade-store capture** (D1–D5) | 2026-07-31 | **Streaming raw-trade capture off the WS trades stream + an in-app gap-repair backfill. Settings v63 → v64 (one new top-level `trade_store` block); ZERO scoring impact, NO rendered surface, NOT a dataset boundary.** Spec: `in-app-trade-store-capture-proposal.md` (APPROVED 2026-07-31, D1–D5 ticked; **D1 ruled AGAINST the recommendation**). **Why it is urgent rather than nice-to-have:** Deribit's public trades endpoint serves **≈24 h** and refuses older windows (measured, `backtest-synthesizer-proposal.md` §7.3) while candles have no such cap — trades are the exception, and the one input the backtester cannot synthesise around. Trade-derived signals (CVD, MicroCVD, TFI, aggressor velocity, liquidations) can only be **re-derived under different settings** from the raw ticks; `analysis_log.csv` stores their *outputs*, which keeps the answer and discards the question (§1.3 — also records the scale reason: ~382 CSV rows/day vs ~60,594 trades/day, ~160 raw trades per row). With Tardis declined on cost (~$350–700/mo subscription, trader 2026-07-31), append-forward is the only path, so **every day without capture is permanently unobtainable flow**. The previously-planned external Scheduled Task had three failure modes that are each **silent and unrecoverable past 24 h**; the app is already up 24/7, already watched daily, and **already receives every trade** — so this is a *write*, not a fetch. **Mechanism — two deliberately redundant halves (§1), neither sufficient alone:** (1) **streaming (primary)** — `DeribitWsFeed.ApplyTrades` buffers each streamed trade into `trades_YYYY-MM.csv` in the **existing store format**, so `HistoricalStore.LoadTradeRange` reads it with no reader change; buffered not per-trade (60k trades/day would be 60k file opens); a **monotonic last-written guard** makes reconnect re-seed idempotent (`SeedAsync` re-seeds from REST on every (re)connect, so the same trades WILL arrive twice); **never throws** (the `SignalEmitter.TryWrite` / `liq_events.log` discipline). (2) **gap repair (secondary)** — the one thing streaming cannot do: it is complete while the app runs and recovers **nothing** from downtime, so a low-frequency in-app timer runs the existing trade backfill over a 20 h lookback (under the ~24 h venue cap). Not collapsible: streaming without repair loses every restart; repair without streaming reinstates the 24-hour deadline the build exists to remove. **Refactor (§2):** `HistoricalStore.vb` sits in `tools/BacktestRunner/` (outside the root glob) and owns a live `HttpClient`, so moving it wholesale would push networking into the feed path *and* the fixture project. Split instead — new host-agnostic **network-free `Core/TradeStoreWriter.vb`** owns file naming, monthly rollover, buffered append + monotonic guard, the row format, the row **parse**, `LastTradeTimestamp` and the resume-cursor decision; `HistoricalStore` keeps the network backfill and now delegates **both** its trade append and `LoadTradeRange`'s per-file read to the writer, so writer and reader cannot drift (the `ComputeSideLevels` "one seam, no copies" move). `BacktestFundingSample` hoisted out of `ReplayLoop.vb` into its own file so the root project links `HistoricalStore` for repair without dragging the replay pipeline in. New root-level host-agnostic `TradeStoreGapRepair.vb` drives the repair timer; `MainForm` starts/stops it beside the WS feed, **independent of transport** (`transport="rest"` ⇒ no stream, repair alone carries it). **D-table:** **D1 AWS-ONLY — ruled against the recommendation on a better ground.** The recommendation was dual-box redundancy; the trader's ground is that **the end goal is the app on AWS and not on the local box at all**, so dual capture builds for a topology the project is leaving — redundancy across a box you intend to retire is migration debt, not redundancy. Consequences the build honours (§7.1): repair is now the **only** recovery mechanism, so it **fires once on start** and then on the interval rather than waiting a full 6 h (a restart is precisely when a gap exists — a build requirement, not a nicety); and the store lives on AWS, so any trades-consuming study takes a store copy-back alongside the CSV copy-back. **D2** both flush triggers (30 s **or** 500 trades, whichever first — time alone loses a burst on a crash, count alone can sit unflushed through a quiet Asia hour). **D3** `store_dir` resolved against the **exe directory**, never the process cwd (the cwd is not guaranteed and a cwd-relative store would silently scatter files); the repo's own `backtest_data\` stays the local analysis store, populated by copy-back. **D4** 6 h cadence / 20 h lookback. **D5** the app owns repair. **Tweaker — HARD CONSTRAINT 27:** `trade_store.` PREFIX reject in `SettingsDiffApplier` + PromptBuilder rule 27 — data-capture plumbing has no failure-rate linkage, same class as `alerts.` (HC25) / `exit_guard.` / `live_strip.` / `signal_bridge.`. Prefix-safe: no other top-level `trade_store.` keys exist. **Display-string parity: NO OBLIGATION** — there is no rendered surface at all (no snapshot line, no card binding, no CSV column, no bridge field), stated explicitly per the hard rule. **Reversibility:** `enabled:false` ⇒ the fold early-outs and nothing is written, byte-identical to pre-build. *(Precision, corrected by the v65 rider: A48f proves the **gate** — that `enabled:false` performs zero writes with the shipped defaults pinned — while the fold's **inertness beyond the gate** is reasoned, not harness-proven. The original "harness-proven" wording overstated it.)* **Fixtures A48a–h:** round-trip vs the shipped reader (header + 5-col F2 rows + liq flag); monotonic guard (identical replay writes once, a fresh writer re-seeds from disk, newer trades still land); month rollover (straddling batch splits, header on create only); gap-repair overlap is a no-op (covered window ⇒ no fetch, gap resumes at last+1, retention clamp, read-time dedup); unwritable path never throws (blocked dir + locked file, fold keeps running); `enabled:false` ⇒ zero writes with the shipped defaults pinned; HC27 rejects all seven keys while a sibling `scoring.*` tunable passes and prompt rule 27 is present; store path exe-relative and stable across two working directories. Acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck build **0/0** Release; A1–A47b unregressed + A48a–h; verify-gate `prepush` GATE PASSED. **Rider for the trader:** `aws-collector-deploy-checklist.md` §3's daily ~30 s RDP glance now carries real data risk — post-build it is the only thing between an unnoticed app death and permanently lost tape, so add the store's newest-file mtime to that glance. **Out of scope (§9):** order-book capture, backfilling the existing pre-capture gap (nothing recovers it — the declined Tardis question), store compaction/retention (~2.4 MB/day, ~900 MB/year), and any change to `analysis_log.csv`. Local commit — trader tests + pushes. |
| **v63 · D2-v2 what-if candidate mode** (D1–D6) | 2026-07-30 | **Engine seam extension in `SignalEmitter.ComputeSideLevels` — ONE new POCO key `scoring.structural_levels.use_best_pivot_candidate` (Boolean, default `false`) makes the volume-weighted best pivot (`r.BestPivotByVolume5m`) a testable TARGET candidate. Settings v62 → v63; ALL DEFAULTS BYTE-IDENTICAL to v62, ZERO behaviour change at build, NOT a dataset boundary** (the v56 arbitration-modes precedent). Spec: `d2v2-whatif-candidate-mode-proposal.md` (APPROVED 2026-07-29, D1–D6 ticked all-as-recommended); spec-back: `d2v2-whatif-candidate-mode-spec-back.md`. **Trigger:** the backlog map's D2-v2 row — BestPivot promotion is "NOT what-if-testable today (`ComputeSideLevels` never reads `BestPivot*`)" — and the Aug-1 geometry session that wants to test it against the ~2,151 post-07-08 directional rows carrying a non-empty `BestPivotByVolume5m`. Context: D2 volume-weighted pivots shipped display-only at v24; P1 (§16.6) parks live promotion behind evidence this build produces. **Mechanism (one seam):** when the flag is true and `r.BestPivotByVolume5m > 0`, the pivot joins the TARGET candidate set inside `ComputeSideLevels`. **Side by PRICE-vs-entry** (D3, one rule live+replay share — CSV logs price + ratio but not `IsHigh`; a LOW pivot sitting above entry still marks a defended level). **Ladder mode (`target_arbitration_mode=0`)** ⇒ inserted as the **FIRST tier ABOVE swing** (D2, P1's "4th cap tier above swing" verbatim). **NEAREST mode (=1)** ⇒ competes on distance like any candidate (no priority, min-distance rule alone). Same looseness bound (`target_max_atr_mult × ATR`) as every other tier; absent/zero pivot ⇒ candidate simply absent (counted, not guessed — POC-tier precedent). **STOP side untouched** (D2 was always a target idea; DG1 stops stay). VolumeRatio NOT consulted at this build (D5 YAGNI — revisit at P1 promotion spec if ratio-dependence shows). **Surfaces:** label `BEST_PIVOT_5M` joins the existing set and renders through the same `PLACED @ p (LABEL)` composition — snapshot / card / payload / CSV inherit it through the one seam. No new lines, no format change anywhere. At default false four-surface parity preserved by construction (v56 A36a pattern). **Replay:** `analysis/ForwardWindowJoiner.CsvRow` gains `BestPivotByVolume5m` (header-name parsed, guarded — absent column keeps default 0); `tools/WhatIfRunner/WhatIfReplay.BuildIndicator` feeds it into `r`; rows with the column empty have no candidate. **Tweaker fence — HARD CONSTRAINT 24:** `scoring.structural_levels.use_best_pivot_candidate` joins the exact-match reject set alongside `target_arbitration_mode` / `stop_arbitration_mode` / `target_buffer_pct` / `stop_buffer_pct` (same hand-ruled-geometry class, HC11 sub-class — a shape/candidate-set choice, not a failure-rate threshold). `PromptBuilder` rule 24 text extended. The flat `structural_levels` numerics stay tunable (HC21 unchanged). **What-if:** `WhatIfOverlay.Whitelist` accepts the dotted path; `WhatIfSettings.ApplyKnob` / `ReadKnob` gained mirror cases (boolean-as-int — any non-zero ⇒ true, the v56 int-mode precedent — booleans sweep as `{0,1}`); `UI/WhatIfLauncherForm.vb` row `Use best-pivot candidate` with `0:1:1` sweep syntax. **Fixtures:** new **A42a–d** in `verify/ordercheck` (default-false byte-identity across the A26 case set + through the REAL `Calculate()` with a pivot supplied that WOULD win under the flag; ladder-first pick beating a closer swing + NEAREST distance pick beating pivot with closer swing + short mirror + STOP side untouched; looseness bound rejects a too-far pivot (falls through to swing) + wrong-side pivot rejected + absent/zero pivot ≡ default; HC24 rejects the key + whitelist accepts it + `{0,1}` sweep round-trips through `WhatIfSettings.BuildCellSettings` and reproduces `ComputeSideLevels` — the A36f linked-seam pattern). Acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0** Release; A1–A41d unregressed + A42a–d; verify-gate `prepush` GATE PASSED. **Live promotion** is a LATER separate ⚠ D-table (D6) gated on the Aug-1 geometry replay evidence produced by this instrument. Local commit — trader tests + pushes. |
| **v62 · fee-aware min-move floor** (D1–D6) | 2026-07-27 | **The minimum-tradeable-move floor is now COMPOSED from an execution-cost model instead of a flat literal — defaults byte-identical to v61, so ZERO behaviour change at ship and NOT a dataset boundary** (settings v61 → v62; the v56 arbitration-modes precedent). Spec: `fee-aware-min-move-proposal.md` (APPROVED 2026-07-27, D1–D6 ticked all-as-recommended); spec-back: `fee-aware-min-move-spec-back.md`; order-app relay: `fee-aware-order-app-relay-2026-07-27.md`. **Trigger:** the Deribit fee change effective 2026-08-01 (maker 1.5 bps / taker 3.5 bps, trader-supplied) invalidates the derivation basis of the v35 floor, which was sized "to clear slippage" under zero-maker-fee execution — a re-derivation, not a re-opened settled decision. **Mechanism:** `scoring.min_tradeable_move_pct` RETIRED (JSON key + POCO property removed; applier-unresolvable so C-6 rejects it naturally — deliberately NOT added to `RejectedPathFragments`, the v47-F1 lesson, v53/v61 precedent). New block `scoring.trade_costs` `{maker_fee_bps 1.5, taker_fee_bps 3.5, round_trip_style "maker_maker", min_net_move_pct 0.0005}`, resolved by ONE shared resolver `TradeCostSettings.EffectiveMinMovePct = RoundTripFeePct(style) + MinNetMovePct`, where `RoundTripFeePct` sums **both legs** (maker_maker = 2 × 1.5 bps; maker_taker = maker + taker; taker_taker = 2 × 3.5 bps; unrecognised style falls back to maker_maker). Fees are proportional to notional, so cost and move share the unit "% of price" and the engine never needs trade size; ATR enters nowhere. **Byte-identity at defaults:** 0.0003 + 0.0005 = **0.0008** = the v61 floor exactly — verdicts, eval floors and BELOW_MIN_MOVE rates unchanged at ship. The Aug-1 fee reality is expressed thereafter by turning the min-net knob, which the v35 hot-reload + eval floor-change re-walk machinery already absorbs attributably. **D1:** `maker_maker` is correct rather than optimistic — the floor gates the TARGET side (the profit path: maker entry + maker TP in the trader's flow); taker occurs on emergency SL repositioning and rare manual exits, the LOSS path this floor does not price. **Call sites** all routed through the one resolver: Step 5c, `LivePerformanceTracker` ×4 (incl. `_floorPctInEffect`, now the composed value — same number at defaults, so no re-walk at ship), `AnalysisRunner` ×4, `BandLadder`, CeilingAudit `CsvFeatureBuilder`, `AutoTweakerCore`, `WhatIfReplay`, `WhatIfReport` ×2. **UI:** new editable row **`MIN NET MOVE % (after fees)`** in the SETTINGS & TOOLS card with a derived read-out of the composed floor (v35's change_log claimed UI-editability but no control existed — grep-verified 2026-07-27); commits on Enter/blur via `SettingsLoader.Save(bumpVersion:=False)` (operational save, v36 §10a) → hot-reload next run; re-read after each run so a file-side fee edit surfaces. A live status element ⇒ **display-parity exempt** (the EXIT GUARD strip precedent). Fees + style stay settings-file-only. **Surfaces:** no snapshot/card/payload/CSV line changes; `BELOW_MIN_MOVE` renders identically (frozen policy-targetable token). **Tweaker — HARD CONSTRAINT 26:** `scoring.trade_costs.` PREFIX reject in `SettingsDiffApplier` + PromptBuilder rule 26; the retired key's exact-match reject removed with the key. **What-if:** the old key leaves the whitelist/launcher grid, `scoring.trade_costs.min_net_move_pct` joins (launcher row relabelled `Min net move % (after fees)`); fee/style keys NOT sweepable — the floor sweep IS the min-net sweep at fixed fees. **Fixtures:** new A40a–e (resolver composition across all three styles + default == 0.0008 to 1e-12 + derived props never serialise; defaults byte-identical through the REAL `Calculate()` against a cfg carrying the retired flat-key semantics, incl. a BELOW_MIN_MOVE case; knob turn moves the gate + the composed delta clears the eval re-walk epsilon; HC26 fence + retired-key C-6 rejection + what-if whitelist split; min_net overlay round-trips through `WhatIfSettings.BuildCellSettings`). A13d re-pointed to the new knob; A15f's exemplar re-pinned to the `trade_costs.` prefix. Acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0** Release; A1–A39e unregressed; verify-gate `prepush` GATE PASSED. **Out of scope (named follow-ups, proposal §6):** eval net-EV rider (per-row fee drag in ATR = `round_trip_fee_pct × entry_price / ATR`, unconditional across all three outcome arms; slot = post-Aug-1 with the EV-dispersion column), optional display net-R:R rider, and the order-app relay (EV-aware chase budget + maker→taker SL-emergency delta; the bridge payload does NOT carry fees — no contract change). Local commit — trader tests + pushes. |
| **§7.5 VWAP session anchor** (settings-untouched) | 2026-07-30 | `GetSessionCandles`/`CalcVWAP`/`CalcVWAPBands` gain `Optional nowUtc As DateTime? = Nothing` — `Nothing` ⇒ `UtcNow`, live byte-identical (A45a pins default-path identity + the §8.6 wall-clock-fallback defect as a regression trap); offline replay passes the bar close. The sole engine edit of the pre-Aug-1 batch (`ff1d34c`, `[no-engine-change]` token); zero behaviour change, no settings bump — the W6-4 settings-untouched precedent. Spec: `backtest-synthesizer-proposal.md` §7.5; evidence: `backtest-overlap-validation-2026-07-30.md` §8.6/§10. |
| **v61 · absorption geometry rescale** (V1–V4) | 2026-07-23 | **Absorption geometry retired the three tick-scaled keys for ATR-fractions — display/CSV-only surface (`scoring_enabled` stays false), ZERO scoring impact, NOT a dataset boundary** (settings v60 → v61; the v54 CSV Absorption* columns keep their names + positions, values start populating rather than staying empty). Spec: `absorption-geometry-rescale-proposal.md` (APPROVED 2026-07-23; V1–V4 all ticked); evidence: `absorption-engagement-derivation-2026-07-23.md` + the map ruling (0% engagement because the tick-scaled geometry — band 4t=$2, proximity 12t=$6 vs ATR≈$44 — measures a shell almost nothing prints in; all three anchors bind at 100%; loosest re-anchor caps at 1.6% vs the 3–8% design band). Spec-back: `absorption-geometry-rescale-spec-back.md`. **Mechanism:** three retired keys (`indicators.absorption.proximity_ticks / band_ticks / break_tol_ticks`) become applier-unresolvable (v53 pattern — deliberately NOT added to `RejectedPathFragments`, the v47-F1 snapshot-poisoning lesson); three new ATR-fraction keys populate at defaults **0.30 / 0.10 / 0.05** (≈$13.2 / $4.4 / $2.2 at ATR 44 — the design point of the 07-23 derivation). Resolved to absolute dollars **once per run** at the `SetAbsorptionLevels` carry site in `MainForm_Analysis.vb` (execution-resolution ATR by construction, since `r.ATR` carries the exec-resolution value); the `LevelAbsorptionTracker` keeps working in absolute dollars internally — `_proximityUsd` / `_bandUsd` / `_breakTolUsd` become tracker state refreshed at `SetLevels` alongside the four candidate-level fields; `FoldBook` / `FoldTrade` no longer read cfg tick fields (no `SignalEmitter.TickSize` multiplication anywhere in the tracker). Warmup: `r.ATR = 0` collapses the resolved distances to zero, the proximity gate cannot open, tracker stays IDLE by construction. **Floor rescale (all provisional against the widened shell):** `depletion_floor_usd` 25000 → 5000; `default.min_aggr_usd` 150000 → 20000; `absorb_ratio` 3.0 → 1.5; `max_pull_frac` 0.5 → 0.75 (the 07-23 derivation showed 0.5 vetoing 50–83% at tiny volumes where the lower-bound ratio is noise). **D8 conservation / visibility mask UNCHANGED** — the formulas follow whatever band is passed in, so widening the band flows through by construction (proposal §1). **Tweaker:** HC23 unchanged in *shape* (exact-match reject on `enabled`/`scoring_enabled`; prefix reject on `default.`/`sessions.`); comment text in `SettingsDiffApplier` + rule 23 in `PromptBuilder` updated to name the new `proximity_atr_frac`/`band_atr_frac`/`break_tol_atr_frac` tunables. **CSV:** header unchanged (v0.8 columns keep names + positions); Absorption* row VALUES start populating (the 07-23 derivation projects ~1–3 flagged rows per NY session-day at these anchors, ~0.5 per LONDON, ~0 per ASIA). **Retro-filter:** NOT POSSIBLE (proposal §2) — the tracker folds ~100 ms book snapshots + per-trade stream at fold time; neither is persisted, and episodes that never opened left no record. Re-collection under honest geometry is unavoidable (~1–1.5 weekday-weeks → §5 re-derivation → activation gates, ~mid-Aug). **POCO defaults ride this commit** (v33/v34 precedent): `AbsorptionSettings.ProximityAtrFrac/BandAtrFrac/BreakTolAtrFrac = 0.30/0.10/0.05`, `AbsorbRatio = 1.5`, `DepletionFloorUsd = 5000`, `MaxPullFrac = 0.75`, `AbsorptionDefaults.MinAggrUsd = 20000`; the three tick property/JsonPropertyName pairs REMOVED from the POCO. **Display-string parity:** strip-only surface (the ABS-tag TAPE-strip line), **no card/snapshot obligation** — the ABS tag renders the same shape from the same fields (v54 D6 precedent under the display-string parity rule). Fixtures: A31a–g re-pinned to fraction-resolved dollars via the extended `SetLevels(...,proximityUsd,bandUsd,breakTolUsd)` signature (fixed 6/2/1 USD to preserve the previous v54 test geometry byte-identical against unchanged books); A31g fence-test JSON literal + proposable check updated to the new key names. **New A31h — two-ATR scale invariance (V3):** same book-geometry logic replayed at ATR=44 (prox $13.2 / band $4.4 / brk $2.2) and ATR=88 (2×), verifying the tracker's resolution arithmetic gives identical classification. Acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0** Release; A1–A39e unregressed + new A31h; verify-gate `prepush` GATE PASSED. **Post-ship watch (§12 addendum):** NY Absorption engagement rate on directional rows over the first weekday-week (target 3–8% band; if <1% after 5 weekday sessions → re-derive with looser default; if >12% → raise `absorb_ratio`); LONDON + ASIA on their own longer clocks per the derivation §4 note. Local commit — trader tests + pushes; coordinator review follows. |
| **W6-4 ceiling-audit tool (settings-untouched)** | 2026-07-23 | **Standalone offline measurement instrument (`tools/CeilingAudit/`)** — the K1–K6-ticked spec from `w6-4-ceiling-audit-method-proposal.md`. **Tool-only build; zero engine changes; no settings.json bump (no engine keys added).** Reads a pooled `analysis_log.csv` (local + AWS-collector rows concatenated externally, repeated headers tolerated), filters to v0.8 weekday directional rows (STRONG+MEDIUM+WEAK), excludes burst `InstanceId` prefix `8706ebae` + any instance whose median inter-row gap < 45s (computed, not hardcoded), partitions into NY×1 / LONDON×3 / ASIA×3 via the engine's own `ExecutionResolution.MatchSessionBucket`. Label = placed-vs-placed SUCCESS at the row's tracker horizon (res-1 → 15m, res-3 → 45m) via the SHIPPED `FailureRateMatrix.ResolveFavourableBarrier` / `ResolveAdverseBarrier` / `WalkBars` — same eval as offline matrix / D4 / What-If (one truth). Features per §2 amendment (scored-inputs only): one-hot categorical signal states + named numerics (ATR / VolumeRatio / ADX / VWAPDevPct / SpreadBps / OFIRatio) + regime + session-hour, standardized on train ONLY. AggrVel joins the SCORED set only on populations whose bucket carries an explicit `burst_ratio_threshold` (today NY×1 = armed; LON/ASIA = un-armed → AggrVel demoted to an informational side-column). Absorption* stays informational on ALL populations (`scoring_enabled:false` at v54). **Baseline** = `dominant_effective / MaxScore` as single ranking feature. **Challenger** = hand-rolled L2 logistic (batch GD, deterministic zero-init, λ chosen via internal walk-forward WITHIN the train block). Chronological train/test split (default ≥7-day test); ΔAUC (challenger − baseline) CI via block bootstrap over **session-hour** blocks (never straddling boundaries — A39d). Markdown report `ceiling_audit_report_<stamp>.md` prints the §4 three-way verdict line (`CEILING DECLARED` / `B1 PRIZE MEASURED` / `INCONCLUSIVE` at ±0.03 margin). Joins the verify-gate build set (F10 lesson). Acceptance: solution + AutoTweaker + WhatIfRunner + **CeilingAudit** + OrderCheck build **0/0** Release; **NEW A39a–e** (loss monotone + direction on separable synth / label-shuffle AUC≈0.5 leakage canary / chronological split respected / block bootstrap boundary discipline / informational extras provably absent from decision matrix); all A1–A38b unregressed. verify-gate `prepush` GATE PASSED (version-bump WARN inherited from prior unpushed commit, per D6 precedent). Spec: `w6-4-ceiling-audit-method-proposal.md`; spec-back: `w6-4-ceiling-audit-spec-back.md`. Local commit — trader tests + pushes; RUN awaits the ~early-Aug pooled-book data gate. |

Older entries are **not** deleted — **v59 down to v27** live verbatim in [`history-archive.md`](history-archive.md) §E, and v26 back to v0.33 below them. **Keep this table at five settings versions**; when it grows past that, move the oldest down. It reached 56 rows and 69 % of this file before the 2026-08-02 trim, which is why the rule is restated here rather than only in the header.

---

## 16. Future Direction — Auto-Tweaking & Dual Interface

> **Sequencing authority: `docs/roadmap.md`** — adds a third strategic objective alongside the CLI port (16.2): the **signal bridge to DeribitOrderPlacementApp** (verdict/score/direction/ATR feeding its autotrade function; human display stays the primary output, the machine contract is additive).

Longer-arc plans. **All items here are KIV** while the team is in the live-data accumulation phase post-v30. Recorded so architectural prerequisites stay visible and groundwork can be laid opportunistically.

### 16.1 Auto-Tweaking via Frontier-LLM API

**Status:** ✅ Shipped as `tools/AutoTweaker/` (Bundle 2, 2026-05-06; extended by `settings-snapshot-history-proposal.md` 2026-05-12 and `auto-tweaker-fixed-window-proposal.md` 2026-05-17 / v29). Not yet fired in live mode — pending data accumulation under v29 fixed-window semantics.

When the engine's analysis fails to predict outcomes at a defined rate, the engine sends `settings.json` (plus a window of the recent `analysis_log.csv`) to a frontier-LLM API. The model returns a tweaked `settings.json` which replaces the local file. This is the long-arc replacement for manual calibration sweeps — automated, data-driven, runs unattended.

**Trigger** (v29 fixed-window): per-round failure rate within a disjoint WindowSize-row window. If failure rate > `failure_rate_threshold_pct` (default 40), a tweak round fires. Snapshot system tracks 3-in-a-row below-threshold streaks for revert candidates.

**Audit prerequisite:** every scoring-affecting parameter reachable through `settings.json`. ✅ Closed by `settings-exposure-pass-proposal.md` (v17).

**Display-only fields are NOT exposed for tweaking.** The audit explicitly excludes display formatting, colour palettes, calibration-report thresholds, MTF cache TTL. The tweaking surface is scoring decisions only.

### 16.2 Dual Interface — CLI (Linux) + WinForm (Windows)

**Purpose.** Two host targets sharing the same engine code:

- **CLI (Linux server, headless)** — runs on a remote host, drives the auto-tweaking loop unattended.
- **WinForm (Windows desktop)** — the existing manual-usage interface.

**Architecture status — partly satisfied.** Most of the codebase is already host-agnostic: scoring engine, indicators, settings, `DeribitClient`, `DynamicNorms`, `AnalysisLogger`, `OiSnapshot`, `AnalysisOutputDump`, `LivePerformanceTracker`, `OhlcCache`, the entire `tools/AutoTweaker/` and `analysis/` subtrees.

**Still WinForms-coupled:**
- Output rendering (`MainForm_Render_*.vb`) is RTF-based. CLI host needs a parallel renderer (ANSI plaintext or structured JSON).
- State plumbing (`_oiHistory`, `_fundingHistory`, `_ofiHistory`, MTF cache, `_prevRegime`, `_metricMode`) on `MainForm`. CLI host needs an equivalent state container.
- Auto-run scheduling. `WinFormsAutoRunTimer` uses `Control.Invoke`; CLI variant uses `System.Threading.Timer` callbacks (interface already defined).

**Constraint:** all new code in `analysis/` and `tools/` MUST be host-agnostic. Form-side viewers are allowed but must be thin wrappers around host-agnostic core classes. The auto-tweaker console app already builds with zero WinForms references.

### 16.3 KIV Prerequisites

Before 16.2 (CLI port) should be specced and scheduled:

1. **All four v27-v30 features stable in production.** ✅ Long since stable (statuses refreshed 2026-07-02).
2. **Auto-tweaker proven on live data.** ⏳ Still open — the first supervised fire remains data-gated on a real >40%-failure NY×1 window (§12 row; roadmap W5). The only prerequisite still outstanding.
3. **CalibrationReport READY threshold met under new metrics.** ✅ Met at v34 (975 rows, 3 regimes ≥50, 3 session days).
4. **WebSocket migration decision.** ✅ Decided and SHIPPED (v38–v42, cutover 2026-06-24).

The port's own groundwork is now scheduled independently of prerequisite 2: the roadmap W4 **run-state extraction spec** (host-agnostic run-context + headless runner skeleton) is behaviour-neutral and de-risks the port without waiting on the tweaker.

### 16.4 Long-Arc Architectural Ceiling

*(Refreshed 2026-07-02.)* The ceiling this section described — **WebSocket migration** — SHIPPED (v38–v42, cutover 2026-06-24). The microstructure class it gated (backlog Section A: real-time spread, aggressor velocity, order-book absorption, liquidation × OFI flip, VPFR shape) is unblocked and sequenced in `docs/roadmap.md` W1/W2. The next architectural ceiling, if one emerges, is the full-depth **incremental order-book channel** (public, change_id semantics) — deliberately deferred behind #6-v1 proving absorption on snapshots (roadmap W4); authenticated/raw feeds were evaluated and ruled out (W4).

### 16.5 Active Spec Bundle Status

Bundles 1–3 (foundation / auto-tweaker / structural refinements) all shipped 2026-05-05 → 2026-05-06. Subsequent v27–v30 features layered on top. See `history-archive.md` §C for the original bundle plan and §B for the spec inventory.

**Deferred bundles — dispositions as of 2026-07-02** (none are "done"; none need a bundle anymore):
- **Bundle 4** (B4 threshold sweeps) — **dissolved into the roadmap**: the sweep items are absorbed by W1 (signal-health audit + v48 OFI re-baseline + spread revival + reach-target calibration) and, longer-term, the auto-tweaker's per-round tuning. No separate bundle will be scheduled.
- **Bundle 5** (multi-session VPFR naked-POC / anchored VWAP — C1/C2) — still deferred, unchanged gates (multi-session state plumbing + demonstrated appetite; backlog Section C). Not on the roadmap.
- **Bundle 6** (Smart OBV / MFI replacement — D5/D6) — still deferred, unchanged triggers (observed OBV/RSI divergence false positives; backlog Section D). Not on the roadmap.

*(The old closing line — "Section A and the WebSocket migration remain priority-ordered after Bundles 4–6" — is obsolete: WebSocket shipped v42 and Section A now leads via roadmap W2.)*

### 16.6 Parked Observations (Watch For)

Items not currently scheduled but with concrete promotion conditions.

**P1. Promote BestPivotByVolume to cap arbitration (D2 v2).**
*Condition:* CalibrationReport's `BEST VOLUME PIVOT DISTRIBUTION` shows "best is also most-recent" rate < 50% AND auto-tweaker output shows volume-weighted pivots correlate with target-hit rate. Both required.
*Action:* re-spec `d2-volume-weighted-pivots-v2-proposal.md`. Promote to 4th cap tier above swing.

**P2. Funding momentum threshold v23+ tuning.**
*Condition:* offline analysis `FundingMomentumDiagnostic` shows FundingDelta percentiles such that a threshold below 1 bp would meaningfully change the RISING/FALLING/FLAT distribution.
*Action:* simple settings-only follow-up pass. If percentiles show 1 bp is genuinely above all observed deltas at REST cadence, accept as polling-cadence ceiling.

**P3.** RESOLVED 2026-05-08 — OI×CVD asymmetry (`priceUp` was biased by 1bp against `MarkPrice`). See `history-archive.md` §D.

**P4. STRONG/MEDIUM tier collapse in failure-rate matrix.**
*Condition:* after 1000+ tier-eligible rows, both STRONG and MEDIUM matrices pick (window, threshold) combinations within 1 cell of each other.
*Action:* revise `failure-definition-v2-proposal.md` to a single tier-agnostic matrix.

**P5. Liquidation count window.**
*Condition:* CalibrationReport still shows 0 liquidation events 1000+ rows after Bundle 1 ships.
*Action:* small spec re-introducing `cfg.Indicators.Liquidations.TradeCount` removed in v15.

**P6. STRUCTURAL_RR_LOW context tag.**
*Condition:* a directional verdict fires with the verdict-direction structural R:R below a threshold (default candidate: 1:1). Currently the engine only fires `STRUCTURALLY_WEAK` when no clean target+stop pair can be placed at all.
*Action:* spec `structural-rr-low-context-tag-proposal.md`. New VerdictContext value `STRUCTURAL_RR_LOW` (display-only). Threshold reads from `cfg.Scoring.ContextTagThresholds`.

**P7.** RESOLVED 2026-05-13 — Live per-analysis success/fail display shipped as v26. See `history-archive.md` §D.

**P8. Live performance display — WEAK tier filtering.**
*Condition:* after ~1 week of live data, if `WEAK LONG`/`WEAK SHORT` inclusion produces visibly different headline rates vs STRONG+MEDIUM-only filter AND trader observes the WEAK-included rate is misleading.
*Action:* small spec changing `LivePerformanceTracker`'s eligibility filter. Optionally expose as `performance_display.tier_filter` (`all_directional` | `actionable_only`).

**P9. Auto-tweaker SKIPPED_SESSION_BOUNDARY waste.**
*Condition:* v29 fixed-mode advances `LastEvaluatedRowIndex` by full WindowSize on session-boundary skip, losing up to `WindowSize-1` rows. If RoundHistory shows lots of SKIPPED_SESSION_BOUNDARY after a week of running, the boundary-aware skip could be smartened to advance only up to the boundary itself.
*Action:* small follow-up patch in `AutoTweakerCore`.

**P10. POC tier 3 of target cap never fires.**
*Condition:* if 1000+ runs with `CAPPED @` events show 0 POC selections AND the `hvnAbove`/`hvnBelow` gate on POC is the bottleneck (rather than POC just being geometrically dominated by HVN). Investigation 2026-05-17 showed code path is reachable but conditions are narrow.
*Action:* consider removing the HVN gate so POC fires as a true "no swing + no HVN" fallback. Re-spec if pursued.

**P11. ATR-band recalibration for the current price regime.** RESOLVED 2026-06-17 (settings v37): `static_ref` 115->38 + trader-profile §5 bands recalibrated to the live regime (1-min 20/55, 3-min ~42/115; resolution-dependent since v36). Original context below.
*Condition:* trader-profile §5 ATR bands (Low<80 / Normal 80–150 / High>150) were calibrated for BTC ~$80k–$100k (Q1 2026). BTC is now ~$62k with ATR running 13–68 — everything reads "Low" by the old bands. Surfaced by the 2026-06-13 ATR-confound finding. **Low impact:** the engine's live ATR reference (`DynamicNorms.ComputeATRRef`) is a recent-average, self-calibrating; only the cold-start fallback `indicators.ATR.static_ref = 115` (set as the old Normal-band midpoint, v14) and the profile's reference bands are stale. Mostly profile-doc + fallback-anchor housekeeping, not a scoring knob.
*Action:* update profile §5 bands + `static_ref` to the current regime; ~10-min settings/doc edit. Backlogged 2026-06-14.

**P12. Reduced size in TRANSITIONAL / low-vol (sizing-advisory).**
*Condition:* trader-profile §6 says "Transitional = reduced size, extra caution." The engine honors the caution via the Step-4 ADX-proximity *score* penalty (fewer/weaker verdicts) but applies no *size* haircut — a transitional trade that passes is Kelly-sized like a clean trend. Display-only (Kelly is advisory). **Design tension to resolve first:** competes with the profile's vol-normalization (`Base × AvgATR/CurrATR` → low ATR = *bigger* size); a transitional/low-vol caution multiplier would layer *on top* and the interaction must be specified (which signal wins when). Backlogged 2026-06-14.
*Action:* if transitional trades that pass still size too aggressively in practice, spec a regime/vol caution multiplier on the Kelly advisory. Display-only; low priority.

**P13. Document the tweaker-tunable vs hand-tuned settings split in the User Manual.**
*Condition:* `settings.json` keys fall into three de-facto ownership tiers that today are only encoded developer-facing (in `PromptBuilder` HARD CONSTRAINTs 11–16 + `SettingsDiffApplier` rejects), never documented for the trader: **(1) auto-tweaker-tunable** failure-rate levers (verdict thresholds, `OFI.avg_window_sec` + dominance ratios, etc.); **(2) hand-tuned re-baseline overrides** — the per-session / per-resolution keys (`session_volume.sessions[].roc_magnitude_threshold`, `resolution_profiles.*`, and the future `aggressor_velocity.sessions[].*`), set by manual firing-rate-match, never auto-tuned; **(3) hand-toggle feature switches** (`OFI.averaging_enabled`, `exit_guard.*`, `network.*`, `aggressor_velocity.enabled`/`scoring_enabled`). Raised 2026-07-01 during the P4 #5 spec (trader asked which knobs the tweaker owns).
*Action:* add a UserManual section/table listing each `settings.json` block's keys by tier (tweaker-tunable / hand-tuned re-baseline / hand-toggle switch), sourced from the `PromptBuilder` HC 11–16 + `SettingsDiffApplier` reject lists. Doc-only, ~30-min pass. Backlogged 2026-07-01.

**P14. Auto-tweaker Phase-2b — per-population auto-tuning (workstream C).** Draft spec exists: `auto-tweaker-phase2b-per-population-autotuning-proposal.md` (DRAFT, living). Lifts the tweaker from **one** designated population (NY×1, Phase-2a) to **many** `(session × resolution)` populations — each with its own evaluated-row cursor / window / MinTier / picked-cell history and its own tunable home, so an Asia/London tune lands in `resolution_profiles` and never overwrites the global keys NY depends on. Not part of the P4/WebSocket upgrade catalogue — this is the *auto-tweaker* arc's workstream (C), which is why it doesn't appear in the P4 list.
*Condition:* build only if the manual (B) Asia/London re-baseline cadence proves heavy enough to be worth automating. Blocked on three gates (spec §1): (A) population filter shipped ✅; ≥50 weekday-3-min rows per session (Asia/London separately); (B) the manual `resolution_profiles["3"]` re-baseline done + the §3 schema-home decision signed off. **Lowest-priority in the v36 arc — may never be built.**
*Action:* finalize the `[FILL IN]` sections of the draft with the accumulated-data findings; trader signs off §3. Pointer added here 2026-07-01 so (C) is visible from the main doc, not just its own file.

### 16.7 Portability Constraint Reaffirmed

The Linux CLI port (16.2) is the long-term target. All new code under `analysis/` and `tools/` MUST be host-agnostic — no WinForms references, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers are allowed but must be thin wrappers around host-agnostic core classes.

Enforced in `CLAUDE.md` Collaboration Rules and is a hard PR-review check.

The port itself happens **after** auto-tweaker proves on live data (16.3) AND analysis accuracy reaches a plateau. WebSocket migration is independent — may or may not happen before the port.
