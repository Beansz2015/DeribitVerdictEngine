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
| `Core/ScoringEngine_Helpers.vb` | `RegimeMaxScore` (cfg `scoring.regime_max_score`), `Threshold`, `TierFloor` (cfg `scoring.tier_floor`), `AddFull`, `HasCrossConfirm`, `BuildNote`, `CalcHoldStatus` (Layer 1 microstructure / **Layer 1.5 structural-break exit** / Layer 2 OBV divergence / Layer 3 RSI/ROC). |
| `Core/ScoringEngine_Calculate_Scoring.vb` | `AppendLean()`, `CalcVerdictContext()` (returns ALIGNED on NO TRADE per v30 F11; FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED / ALIGNED), `RunScoringPipeline()` Steps 2 / Pass 2 / Pass 2b / Pass 2c / 3 / 3b. |
| `Core/ScoringEngine_Calculate_Verdict.vb` | `Calculate()` entry point. Step 4 regime veto + TRANSITIONAL ADX penalty. Step 4b MTF gate veto. Step 5 verdict. **Step 5b 3-tier target cap** (swing → nearest HVN → POC). |
| `Core/ScoringEngine_Kelly.vb` | `CalcKellySizing()` — display-only, called from `MainForm_Render_Header` not from `Calculate()`. Zero scoring impact. |
| `Core/IndicatorResults.vb` | IndicatorResults struct. All indicator output fields incl. `FundingMomentum`, `SpreadBps`, `OFIMomentum`, VPFR-v2 fields, swing pivot fields, `TrendStructure5m`, `BestPivotByVolume5m`. |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcRSI, CalcRSISeries, **CalcRSIDivergence** (v21 semantic rewrite), CalcROCSeries, CalcVolumeSMA. |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze. |
| `Core/Indicators_OrderFlow.vb` | CalcOFI, CalcOFIMomentum, CalcLiquidations, CalcCVD, CalcMicroCVD (dynamic accelThreshold), CalcTFI, CalcFundingMomentum. |
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
| Funding Momentum | CalcFundingMomentum | `cfg.Indicators.Funding.MomentumEnabled/Window/Threshold/Amplify/Soften`. |
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

| Verdict | Meaning |
|---|---|
| STRONG LONG | High-confidence long |
| LONG | Standard long |
| WEAK LONG | Low-confidence long |
| NO TRADE | Insufficient signal or MTF block |
| WEAK SHORT | Low-confidence short |
| SHORT | Standard short |
| STRONG SHORT | High-confidence short |

`VerdictContext` tag (always rendered as a CONTEXT: line):
- **CONFIRMED** — directional call with cross-category support (only for directional verdicts)
- **ALIGNED** — sub-threshold bias has cross-category support (NO TRADE only, v30)
- **FLOW_UNCONFIRMED** — score qualifies but order-flow indicators contradict
- **MOMENTUM_FADING** — score qualifies but momentum is decaying
- **STRUCTURALLY_WEAK** — swing data exists but no clean target+stop pair

---

## 6. settings.json — operational pointer

**Source of truth:** `settings.json` itself + its inline `change_log` array.

Current version: **v31**. Top-level blocks:
- `indicators` (per-indicator parameter blocks)
- `session_volume` (UTC bucket multipliers)
- `mtf_gate` (15m gate configuration)
- `auto_run` (auto-run timer)
- `scoring` (verdict thresholds, regime max scores, tier floors, context tag thresholds, hold thresholds)
- `kelly` (display-only sizing block)
- `regime_gates` (TRANSITIONAL ADX penalties)
- `regime_weights` (Pass 2c alignment bonus/penalty)
- `network` (HttpClient timeout, retry config)
- `performance_display` (live perf strip + OHLC gap-fill + metric mode)
- `analysis_logging` (output dump)

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
- **Step 5b (3-tier target cap):** Priority swing target → nearest HVN → POC. Winner = closest cap to entry. Sets `AdjustedLongTarget`/`AdjustedShortTarget` and `TargetCapReasonLong`/`TargetCapReasonShort` (split B1 2026-05-12). POC tier is HVN-gated; rarely fires in practice (see `architecture.md` *Display Behaviour Clarifications*).
- **Step 5b (VerdictContext):** FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / CONFIRMED. **NO TRADE special case (v30):** CONFIRMED relabels to ALIGNED. Decay ratios + count thresholds from `cfg.Scoring.ContextTagThresholds.*`.
- **Step 6 (CalcHoldStatus — layered exit):** Layer 1 microstructure (2+ adverse → fast EXIT) → Layer 1.5 structural break (prior swing breached) → Layer 2 OBV divergence → Layer 3 RSI divergence / single adverse / RSI+ROC structural. Only renders when `posState ≠ None`.
- **Step 7:** ATR target/stop from `cfg.Scoring.AtrTargetMultiplier`/`AtrStopMultiplier`. **Structural rows** rendered in UI alongside (cyan when both target+stop exist, dim when partial). v30 `FormatRR` uses `< 0.1` literal for sub-1dp ratios.
- **CalcKellySizing():** called from `RenderOutputHeader` after ATR levels. Display-only, zero scoring impact.

For full annotated `Calculate()` pipeline detail, see `docs/architecture.md`.

---

## 8. ATR Entry / Stop / Target Display

- **Entry price** = `candles1m.Last().Close`.
- **Last transacted price** = `recentTrades.Last().Price` — displayed above ATR block, not used as entry. (Trade lists are chronological ascending since the v31 correctness pass; the most recent trade is the LAST element.)
- Long: Stop = price − ATR × scale × `AtrStopMultiplier`; Target = price + ATR × scale × `AtrTargetMultiplier`.
- Short: mirrored. Default R:R = 1:1.7 (1.2 stop / 2.0 target).
- **3-tier cap:** if `AdjustedLongTarget > 0` (or Short), raw target shown dimmed and capped target shown in amber bold with reason label (e.g. `CAPPED @ 95200.0 (SWING_HIGH_5M)`). **v30 sub-tick suppression:** when `|raw − adjusted| < max(0.5, ATR × 0.02)`, the amber label is hidden; target renders as a normal value (CSV `TargetCapReason` still populated for analytics).
- **Multipliers read from cfg** — labels and R:R display are dynamic, not hardcoded.
- **Structural rows** below ATR block: `Long structural: Stop X | Entry X | Target X  R:R 1:N  (risk X / rwd X)` in cyan when both target+stop exist; dim with per-side missing-data note when only one side (v30 F12 wording). Mirror for short.
- **Kelly Sizing block** rendered after ATR levels. Half-Kelly, 5% hard cap, $1,000 account, $10 contract face. Advisory label notes R:R is ATR-basis (not structural). EST mode only — CAL mode will return when backtesting module ships empirical per-tier win rates. Suppressed when KellyF = 0. v30 plural fix: `1 contract` / `N contracts`.
- **Funding display** (FUNDING section): rate row + momentum row. v30 negative-zero clamp at both display sites.

---

## 9. Open Position Guidance (CalcHoldStatus)

Priority order: (1) 2+ adverse microstructure signals → fast EXIT → (1.5) structural break exit (price closed at/below prior swing low for long; at/above prior swing high for short) → (2) OBV divergence exit → (3) RSI divergence evaluate → (4) single adverse microstructure warning → (5) RSI/ROC structural assessment.

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

Currently-open items pending live-data review (≥50–100 fresh rows under the new v27-v30 metrics):

| Item | Description | Priority |
|---|---|---|
| Funding momentum thresholds | Review `momentum_threshold=0.0001`, `momentum_window=3`, soften/amplify values after 50+ live runs; check whether BTC funding changes are too sticky for 3-sample lookback. | Low |
| Session volume multipliers | Review ASIA/LONDON/NY `vol_high_mult` / `vol_mid_mult` after 50+ live runs; verify reduced false positives in thin hours without underweighting genuine expansion. | Low |
| OI × CVD gate tuning | Review `upgrade_bonus=1` / `conflict_penalty=1` after 50+ live runs; confirm full-signal conflict is strong enough and upgraded partial OI confirmations improve hit rate. | Low |
| TFI threshold | Evaluate 0.15 vs 0.10 for BTC-PERPETUAL tick size after live data. | Low |
| AtrTargetMultiplier | Currently 2.0; review against logged R:R after 50+ trades. | Low |
| OFI ratio | Buy/Sell dominant 2.0/0.5; review hit rate in CalibrationReport. | Low |
| TTM flatThreshold | Default 0.5; review FLAT vs RISING/FALLING against 1m candle range distribution. | Low |
| VPFR numBuckets | Default 50; review POC resolution on quiet sessions. | Low |
| Liq dominanceRatio | Default 2.0; review false signals; consider raising/lowering after live observations. | Low |
| ContextTag thresholds | Review FLOW_UNCONFIRMED hit rate after 50+ trades. | Low |
| Kelly est_prob_floor/scale | Default 0.45 / 0.20 — review against actual win rates once CalibrationReport reaches READY. | Low |
| Bid-ask spread threshold | `wide_penalty_threshold_bps` default — review after 50+ runs for BTC-PERPETUAL normal spread distribution. | Low |
| Swing pivot wing/lookback | `pivot_wing_5m=3` / `lookback_bars_5m=30` — review false-positive rate; consider widening to 4–5 on low-volatility sessions. | Low |
| **v28 target-hit vs barrier-hit gap** | Post-v28 data should show ~30-50pp gap between target-hit and barrier-hit rates. If gap is small, direction calls themselves are bad; if large, stops are too tight. Probe on 2026-05-15 showed +35pp on 67-row sample — needs validation on larger sample. | Medium |
| **v29 first auto-tweaker fire** | The fixed-mode auto-tweaker hasn't fired yet (LastEvaluatedRowIndex was just seeded). First-fire is a single-shot event — watch for it carefully, verify streak/snapshot/revert paths work end-to-end. | Medium |
| **v30 ALIGNED frequency** | Post-v30, NO TRADE rows that previously rendered CONFIRMED now render ALIGNED. Watch the CSV's `VerdictContext` distribution. | Low |
| **Post-correctness-pass re-baseline (incl. OBV trend_gate seed)** | **ACTIVE — the correctness pass shipped 2026-06-11 (v31) and the clean-data reset is in effect.** `indicators.OBV.trend_gate = 10.0` is a **seeded guess, not a calibrated value** (the OBV normalisation basis changed from first-bar volume to mean per-bar volume) — it must be re-baselined together with CVD slope_min_usd, MicroCVD accel thresholds, session-volume multipliers, volume clamps, and Donchian quartile_pct (full table: proposal §8). **Trigger: ≥300 clean v0.5 CSV rows spanning ≥2 sessions. Any session reading this after the trigger is met should remind the trader to run the re-baseline review.** First-100-rows sanity checks: verdict tier + NO TRADE distribution per regime, MTF BLOCK rate per side. | Medium |

Earlier ✅ Done items moved to `history-archive.md`.

---

## 13. Future Upgrades

Ranked by expected accuracy / reliability gain. Items marked 🔍 require a spec decision before coding begins.

### High-Impact (deferred until post-WebSocket or post-calibration)

| Item | Description | Status |
|---|---|---|
| WebSocket migration | Real-time order book + trade stream vs REST snapshot polling. The single highest-impact non-indicator upgrade. Gates a class of microstructure improvements (real-time spread, aggressor velocity, order-book absorption, liquidation × OFI flip detection, fine-grained VPFR profile-shape work). | 🔍 KIV — fix when (a) indicator backlog exhausted and latency floor becomes the next bottleneck, OR (b) a post-WebSocket item becomes priority. See §16.4. |

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

The engine is approaching its natural accuracy limit for a **single-instrument 1m scalping system using REST polling**. By the time a signal is computed, the 1m candle is closed and partially acted on by faster participants. The three risk thresholds to watch before adding more upgrades:

1. **Overfit risk** — the number of tunable parameters continues to rise; session-bucket multipliers and per-tier thresholds should be validated against forward runs, not a tiny historical slice.
2. **Signal redundancy** — OFI + TFI + CVD + MicroCVD already cover order flow from four angles.
3. **Interpretability** — adjustments should remain easy to reason about from DynamicNorms and settings. If traders cannot quickly tell which bucket is active and why a threshold moved, the engine becomes harder to trust.

The highest-impact non-code upgrade at this stage is the **WebSocket migration**, which would remove the fundamental latency constraint.

---

## 14. Backlog

*(cleared — all spec bundles 1–3 + v27-v30 features shipped; remaining items are calibration review or future spec work tracked in §12 and §13)*

---

## 15. Recent Changes

Most recent five settings.json versions. Full history (v0.33 through v26) lives in `docs/history-archive.md` §E.

| Version | Date | Summary |
|---|---|---|
| **post-v31** | 2026-06-11 | CSV schema v0.5 → v0.6 (no settings.json bump). Columns 89-93 added: MicroCVDEarly/Mid/Late (net USD deltas — negative values valid), MicroCVDMomentum, MicroCVDSignal. MicroCVD was never logged to the CSV, so the accel-threshold re-baseline (§12 WATCHING) would have had no CSV data to sweep — added immediately post-reset while the v0.5 file held only ~5 rows (rotated to `analysis_log.csv.v0.5.bak`; rotation bak-name literals updated v0.4 → v0.5). Verified live: 93/93 header/row alignment, chronological E/M/L values in the first logged row. All `analysis_log.csv` readers are header-name-based since F9, so the appended columns are transparent to them. |
| **v31** | 2026-06-11 | **Engine correctness pass** — one bundled fix event at a single reset boundary. Spec: `engine-correctness-pass-proposal.md`; audit: `fable5-audit-report.md`. Five sequential commits, all behaviour-changing fixes before the data reset. **F1** (C1/C2/C3, CRITICAL): `GetRecentTradesAsync` returns chronological ASCENDING (request stays `sorting=desc`; list reversed before return — preserves intra-ms ordering); TFI + MicroCVD windows take from the END via new `LastN` helper; CVD slope and MicroCVD accel/decel polarity become chronologically truthful (hold/exit cues inherit the fix); last-transacted price reads the last element. **F2** (H1): DynamicNorms volume (100) + VWAP-dev (50) baselines sample the most recent completed bars, mirroring `ComputeATRRef`. **F3** (H2): direction-aware MTF gate — `CalcMTFGate` emits per-side flags (`MTFGatePassLong/Short`) + direction-free details; the 1m pre-scoring proposal is deleted; Step 4b consults the dominant side's flag and composes the final reason (three locked formats) onto `VerdictResult.MTFGateReason`/`MTFGateBlocked`, which every consumer renders; regime-veto early returns now append the MTF breakdown row. **F4** (H3): Step 5 walks only the dominant side's tiers; ties carry no direction → NO TRADE; `AppendLean` long-bias tie removed, explicit `[TIE]` tag. **F5** (M1): OBV change normalised by mean per-bar volume (units: average-bar-volumes of drift); `indicators.OBV.trend_gate` 0.001 → **10.0 (seeded, WATCHING)**. **F6** (M2): Donchian channel spans the prior `period` bars (current bar excluded) — full breakouts genuinely reachable. **F7** (S-5): Step 3 funding boost capped at regimeMax. **F8** (S-6): TRANSITIONAL ADX penalty first arm covers [0, mid) — grace-bar ADX < 20 now gets the full penalty. **F9** (M7): `LivePerformanceTracker` CSV backfill parses by header name (was fixed indices — required by the schema shift). **CSV v0.4 → v0.5**: gate columns now `MTFGatePassLong`, `MTFGatePassShort`, `MTFGateReason` (final composed string); same-name columns `CVDSlope`/`MicroCVD*`/`VolumeRatio`/`OBV*`/`DonchianSignal` change semantics — the header is the schema marker. Acceptance harness `verify/ordercheck/` recreated glob-safely (root `.vbproj` `Compile Remove verify/**`); fixtures A1–A9 all pass. **Data reset executed** (pre-authorised 2026-06-10): `analysis_log.csv`, `analysis_eval_cache.csv`, tweaker `state.json`, picked-cell history, `settings_snapshots/`, output dump archived to `data-archive/pre-orderfix-20260611/` (gitignored); `ohlc_1m_cache.csv` kept (raw input, uncontaminated). Auto-tweaker stays held until ≥1 full clean window + supervised first fire. Expect on day one: hold/exit cues track the tape, wrong-side WEAKs gone, MTF blocks redistribute both ways, more NO TRADE on provisional thresholds — different ≠ regression; judge against the tape. |
| **post-v30** | 2026-06-10 | Tier C dataset-protection bundle (no settings.json bump; zero scoring impact; no happy-path behaviour change). Kickoff: `engine-tier-c-dataset-protection-kickoff.md`; implementation report: `engine-tier-c-dataset-protection-spec-back.md`. Seven commits `a86532a`…`57de688`: (C-1) atomic tmp+`File.Replace` writes for settings.json in `SettingsLoader.Save` and both `SettingsDiffApplier` writers. (C-2) settings parse failure surfaced in the LOG status line via `SettingsLoader.LastLoadError` — self-healing on a later clean hot-reload; console line kept for the future CLI host. (C-3) POCO defaults re-aligned to live v30: funding bands ±0.00008/±0.00001, funding momentum_threshold 0.00001, OI change_threshold_pct 0.002, plus session-volume default buckets populated so the silent-defaults path keeps session scaling. (C-4) culture-invariant numeric formatting in the `AnalysisLogger` 87-column row build and `FailureRateMatrix.AppendPickedCell` (Linux-port protection; byte-identical output on dot-decimal hosts). (C-5) auto-tweaker re-seeds `LastEvaluatedRowIndex` with a logged warning when the CSV shrinks below it (was: permanent INELIGIBLE). (C-6) tweak diffs with unresolvable settings paths rejected at `Validate`; `Apply` never creates keys. (C-7) date-aware session-boundary check in `AutoTweakerCore` + its `TweakSettingsForm` mirror (cross-day same-hour and ≥24h spans now detected). Side effect: the gitignored `verify/ordercheck/` audit harness was deleted to clear a solution build break (root `.vbproj` `**/*.vb` glob swept the nested harness project); the engine correctness pass recreates it glob-safely. |
| **post-v30** | 2026-05-24 | P3 maintenance pass (no settings.json bump). Spec: `p3-maintenance-pass-proposal.md`. Four cleanup items bundled in one commit. (1) `UI/Controls/AnalysisReportButton.vb` swaps `Char.ConvertFromUtf32(&H1F4CA)` (📊, supplementary plane) for `"▤"` (U+25A4) — Geist Mono has no glyph for the emoji and GDI+ `Label.DrawString` doesn't invoke Segoe UI Emoji fallback, so the icon was rendering as tofu in the P4e SETTINGS & TOOLS CTA. Fallback if `▤` reads thin at 12pt bold: `≡` (U+2261). (2) `UI/Controls/Pass2cBadge.vb` deleted — design-stale 3-state enum; SIGNAL BREAKDOWN footer uses inline `MakeFooterAggregate` with the correct 4-state shape (SUPPRESSED / ALIGNED↑ / ALIGNED↓ / CONFLICT). (3) `UI/Controls/SegmentedToggle.vb` deleted — superseded by P4e's `Pill`-mirrors-radio pattern (radios stay source of truth). UI reskin handover §4 row added so future spec authors don't propose reintroducing it. (4) Dead `_analyzeButton As FlatButton` field in `MainForm_Layout.vb` removed (P4a scaffold; handover §4 locked `btnAnalyze` as Designer Button). UI reskin handover doc promoted from worktree to `docs/ui-reskin-handover-2026-05-22.md` so it's discoverable from project root. |
| **post-v30** | 2026-05-18 | `MainForm_Render_Header.vb` REGIME ANCHOR caution line. Fires only on STRONG verdicts when price is displaced from `r.EMA200_5m` by > 3.0× ATR in the opposite direction (STRONG LONG with price ≥ 3×ATR below the anchor → "fighting intermediate bear"; mirror for STRONG SHORT). Display-only — zero scoring impact. Rendered between CONFIDENCE and SCORE in the verdict header. Hardcoded threshold; lift to `cfg.Scoring.RegimeAnchorAtrThreshold` if tuning proves needed. Labelled INTERMEDIATE not MACRO because 5m EMA(200) is ~3.3 hours of data — true macro context (daily timeframe) needs a separate spec for Deribit daily candle fetch. |
| **post-v30** | 2026-05-18 | `analysis/FailureRateMatrix.vb` dual recommendation per tier — `IsRecommended` (lowest CI width, auto-tweaker view) preserved; new `IsMostProfitable` flag (lowest failure rate with n ≥ MinSamplesPerCell, trader view). `MarkdownReportWriter` renders both: ★ for IsRecommended, ◆ for IsMostProfitable, ★◆ when same cell. §1 headline, §2 matrix, §3 (with new legend), §8 hold-window stats, and CSV summary all dual-view. Surfaced because the original picker minimised CI width, which favours extreme p values — for high-failure tiers (e.g. STRONG_LONG 69% recommended cell) the "recommended" cell was the worst-performing one, confusing for human readers. Auto-tweaker continues to read `IsRecommended` (correct for the most-precise-estimate consumer). |
| **post-v30** | 2026-05-18 | `analysis/DeribitOhlcFetcher.vb` chunked fetch — closes STRONG_SHORT cell-eligibility starvation. Surfaced in 2026-05-17 Analysis Report: STRONG_SHORT showed n=0 in every failure-matrix cell despite 47 STRONG SHORT verdicts in CSV. Root cause: `FetchOhlcRange` made a single `DeribitClient.GetCandlesAsync` call. Deribit caps responses at ~5000 bars per call, so CSV spans >5000 minutes lost the oldest portion of OHLC. All 47 STRONG SHORTs clustered in the first 2 hours of CSV (2026-05-13 13:56–16:11 UTC+8) — all silently excluded from the matrix. Fix: loop in `CHUNK_MINUTES=5000` segments with `MAX_CHUNKS=20` safety cap, mid-range failure aborts the whole fetch (avoids silently-partial OHLC maps). Same pattern as `LivePerformanceTracker.FetchGapChunked` (v27). |
| **post-v30** | 2026-05-17 | Audit cleanup pass (no settings.json bump). Spec: `audit-cleanup-pass-proposal.md`. Four small fixes from the 2026-05-17 OHLC + calibration-tooling audits. (1) `TweakerState.Save` now uses atomic write-to-tmp + `File.Replace` so mid-write crashes can't wipe accumulated state. (2) `OhlcCache.WriteAll` and `RollingTrim` same pattern. (3) `BuildCalibrationReport.contextCounts` adds `"ALIGNED"` key — post-v30 NO TRADE rows were silently dropped from CONTEXT DISTRIBUTION counts. (4) `analysis/AnalysisRunner.vb` line 88 cross-tab adds `"ALIGNED"` to the enum list for consistency (currently renders n=0 due to NO TRADE filter, but removes future enum/filter divergence). No behaviour change at happy path. |
| **v30** | 2026-05-17 | Display polish pass. 8 fixes bundled in one commit. Spec: `display-polish-pass-proposal.md`. Output dump captures perf-strip values via new optional `perfStripLine` param on `AnalysisOutputDump.Append`. R:R rendering uses `< 0.1` literal for sub-1dp ratios via new `FormatRR` helper. Sub-noise CAPPED labels suppressed when `|raw − adjusted| < max(0.5, ATR × 0.02)`. Negative-zero funding clamped (`|r| < 1e-8` → 0.0) at both display sites. OI Delta breakdown row 2dp → 3dp. Kelly `1 contract` / `N contracts` pluralisation. `CalcVerdictContext` returns `ALIGNED` (new VerdictContext value) on NO TRADE instead of CONFIRMED. Per-side missing-target / missing-stop wording in structural rows. No new config keys; thresholds hardcoded. |
| **v29** | 2026-05-17 | Auto-tweaker switches from sliding to fixed (non-overlapping) windows. Spec: `auto-tweaker-fixed-window-proposal.md`. New `TweakerConfig.WindowMode` (`"fixed"` default, `"sliding"` retained as deprecated). `MinTierEligibleRows` becomes nullable — null auto-scales as `max(15, ceil(WindowSize × 0.5))`. `cooldown_rows` retained but no-op when `window_mode=fixed`. `TweakerState.LastEvaluatedRowIndex` (default −1; seeded to `currentRowCount` on first v29 run so historical sliding-era CSV data stays in the file but isn't re-evaluated). `RoundHistoryCap` raised 50 → 1000. New `SKIPPED_INSUFFICIENT_TIER` outcome (advances row index but doesn't tick streak). New `SKIPPED_SESSION_BOUNDARY` outcome (same semantics). Tweak Settings dialog exposes MinTier with save-time validation: reject <5, reject >WindowSize, warning at >WindowSize × 0.7. |
| **v28** | 2026-05-17 | Target-hit metric toggle on perf strip. Spec: `target-hit-metric-proposal.md`. **Eval cache schema v1 → v2** with new `TargetEverHit` column. `IsV1Schema` detection by absence of `"TargetEverHit"` in header; one-time `MigrateV1ToV2` backfill on first load. New `FailureRateMatrix.TargetHitWalk(bars, favBar, isLong)`. `EvalCacheEntry.TargetEverHit As Boolean?`. `WindowAggregate` gains `TargetHitCount` and dual `BarrierRatePct` / `TargetRatePct`. `MainForm_Layout` gains `lblPerfMode` (`[B]`/`[T]`), `_metricMode`, `_perfContextMenu`. Left-click on perf label toggles mode ephemerally; right-click context menu persists via `SettingsLoader.Save`. Tooltip second line shows the other metric. New `performance_display.metric_mode` (`"barrier"` default). |
| **v27** | 2026-05-15 | OHLC cache gap-backfill on `InitialiseAsync`. Spec: `ohlc-gap-backfill-proposal.md`. New **Step 1.5** between trailing-gap fetch and load-eval-cache: scans `_ohlcLookup` for interior gaps within the 7-day window via new private shared helpers `FindGaps` / `TruncateToMinute` / `FetchGapChunked`. Throttled by `max_gap_fill_calls` safety cap; chunked by `max_gap_fill_minutes`. Idempotent (filters by `Not _ohlcLookup.ContainsKey(b.CloseTime)`). New optional `statusCallback As Action(Of String)` on `InitialiseAsync`. Three post-impl fixes followed: UTC parse in OhlcCache, file-order canonicalisation after gap-fill, `NewestBarTime` scans all rows instead of trusting file order. `PerformanceDisplaySettings` gains `GapBackfillEnabled`, `MaxGapFillCalls`, `MaxGapFillMinutes`. |
| **v26** | 2026-05-13 | Live performance display strip. Spec: `live-performance-display-proposal.md`. Activates P7. New host-agnostic files `OhlcCache.vb` + `LivePerformanceTracker.vb`. New sidecar caches `analysis_eval_cache.csv` + `ohlc_1m_cache.csv` (gitignored). Six perf-strip labels in `MainForm_Layout`, updated on every `RunAnalysisAsync`. Most-recent-block session semantics with straddle-aware NY (tail/head/between). New `performance_display` block (4 keys). |

For full version history including v0.33 onward, see `docs/history-archive.md` §E.

---

## 16. Future Direction — Auto-Tweaking & Dual Interface

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

1. **All four v27-v30 features stable in production.** ⏳ In progress — fresh data accumulation under new metrics ongoing as of 2026-05-17.
2. **Auto-tweaker proven on live data.** ⏳ First fixed-window round hasn't fired yet (LastEvaluatedRowIndex just seeded). Need a few rounds + at least one successful tweak before considering the loop validated.
3. **CalibrationReport READY threshold met under new metrics.** Currently met under v0.4 schema; new target-hit metric needs ~1 week of fresh data.
4. **WebSocket migration decision.** Independent of port; may or may not happen first.

### 16.4 Long-Arc Architectural Ceiling

`docs/post-websocket-post-calibration-backlog.md` documents the architectural ceiling: **WebSocket migration** is the single highest-impact non-indicator upgrade, gating an entire class of microstructure improvements (real-time spread, aggressor velocity, order-book absorption, liquidation × OFI flip detection, fine-grained VPFR profile-shape work).

Develop assuming WebSocket arrives eventually: avoid hardcoded REST-cadence assumptions in scoring logic. Score thresholds should remain meaningful at higher poll rates. They currently are.

### 16.5 Active Spec Bundle Status

Bundles 1–3 (foundation / auto-tweaker / structural refinements) all shipped 2026-05-05 → 2026-05-06. Subsequent v27–v30 features layered on top. See `history-archive.md` §C for the original bundle plan and §B for the spec inventory.

**Deferred bundles** (not currently scheduled):
- **Bundle 4** — small refinements per B4 items.
- **Bundle 5** — multi-session VPFR / anchored VWAP (C1/C2 items).
- **Bundle 6** — Smart OBV / MFI replacement.

Section A (post-WebSocket items) and the WebSocket migration itself remain priority-ordered after Bundles 4–6 unless WebSocket becomes the binding constraint sooner.

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

### 16.7 Portability Constraint Reaffirmed

The Linux CLI port (16.2) is the long-term target. All new code under `analysis/` and `tools/` MUST be host-agnostic — no WinForms references, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers are allowed but must be thin wrappers around host-agnostic core classes.

Enforced in `CLAUDE.md` Collaboration Rules and is a hard PR-review check.

The port itself happens **after** auto-tweaker proves on live data (16.3) AND analysis accuracy reaches a plateau. WebSocket migration is independent — may or may not happen before the port.
