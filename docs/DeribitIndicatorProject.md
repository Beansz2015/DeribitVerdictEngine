# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-11 | Current version: v0.49 (Commit 5 complete)**

This document is the authoritative handover for any new AI conversation continuing this project.
It takes precedence over `indicator-spec.md` wherever the two conflict.

---

## 1. Project Purpose

A Windows Forms (VB.NET / .NET 8) desktop application that connects to the Deribit REST API,
calculates a set of technical indicators on live BTC-PERPETUAL data, scores them via a
weighted multi-tier engine, and emits a verdict (STRONG LONG / LONG / WEAK LONG / NO TRADE /
WEAK SHORT / SHORT / STRONG SHORT) with ATR-based entry/stop/target levels.

---

## 2. Repository

- **GitHub:** https://github.com/Beansz2015/DeribitVerdictEngine
- **Branch:** `master`
- **Solution file:** `DeribitVerdictEngine.sln`
- **Target framework:** .NET 8, Windows Forms

---

## 3. File Inventory & Current Versions

### Root files

| File | Notes |
|---|---|
| `DeribitClient.vb` | All Deribit REST calls incl. 15m candles, recentTrades |
| `DynamicNorms.vb` | ATR/Vol/VWAP norm computation |
| `AnalysisLogger.vb` | CSV logging + CalibrationReport |
| `OiSnapshot.vb` | OI ring-buffer helper |
| `AutoRunTimer.vb` | IAutoRunTimer interface + WinFormsAutoRunTimer impl |
| `Program.vb` | Entry point |
| `SettingsLoader.vb` | JSON deserialisation, SettingsLoader.Current singleton |
| `settings.json` | v6 — All tunable parameters (see Section 6) |
| `MainForm.Designer.vb` | Auto-generated WinForms designer file (do not edit manually) |
| `MainForm.resx` | Form resources |

### Core/ — ScoringEngine + IndicatorEngine partial classes

| File | Notes |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult (incl. AdjustedLongTarget, AdjustedShortTarget, TargetCapReason), PositionState, SignalCategory, ScoreState |
| `Core/ScoringEngine_Helpers.vb` | RegimeMaxScore, Threshold, TierFloor, AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus |
| `Core/ScoringEngine_Calculate.vb` | MaxScore const + full Calculate() pipeline **(Commit 5 — T2-C Donchian NONE mid-channel note added to breakdown)** |
| `Core/IndicatorResults.vb` | IndicatorResults struct — all indicator output fields |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcEMAList, CalcRSI, CalcRSISeries, CalcRSIDivergence (pivotWing + lookbackBars params), CalcROCSeries, CalcVolumeSMA |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze (flatThreshold param) |
| `Core/Indicators_OrderFlow.vb` | CalcOFI (dominance thresholds + bookDepth wired to cfg), CalcLiquidations **(dominanceRatio param — T3-D)**, CalcCVD, CalcMicroCVD, CalcTFI |
| `Core/Indicators_Structure.vb` | CalcDonchian, CalcOBV, CalcVPFRLite (exp decay, numBuckets param), CalcMTFGate |

### Core/Settings/

| File | Notes |
|---|---|
| `Core/Settings/EngineSettings.vb` | **v0.37** — All scoring/indicator tuning fields. Requires `cfg.Indicators.VPFR.NumBuckets`, `cfg.Indicators.RSI.PivotWing`, `cfg.Indicators.RSI.LookbackBars`, `cfg.Indicators.TTM.FlatThreshold`, `cfg.Indicators.Liquidations.DominanceRatio` to be present (added Commit 5). |

### UI/ — MainForm partial classes

| File | Version | Notes |
|---|---|---|
| `UI/MainForm_Layout.vb` | v0.47 | Constants, DllImport/RECT, New(), ResizeControls(), SetOutputMargins(), OnFormHandleCreated(), CentreNudText(); shared fields: colour palette, _oiHistory, auto-run state vars, MTF TTL cache fields, `_prevRegime` |
| `UI/MainForm_AutoRun.vb` | v0.47 | InitAutoRunControls(), btnStartStop_Click, StartAutoRun(), StopAutoRun(), RunAutoAnalysis(), OnCountdownTick(), UpdateCountdownLabel() |
| `UI/MainForm_Analysis.vb` | **Commit 5** | RunAnalysisAsync() — T3-A VPFR numBuckets, T3-B RSI pivotWing/lookbackBars, T3-C TTM flatThreshold, T3-D Liq dominanceRatio all wired from cfg at call sites |
| `UI/MainForm_Render.vb` | v0.46 | RenderOutput(), AppendRtf(), AR(), SectionHeader(), Divider(), BuildCalibrationReport(), Flag(), UpdateLogInfo() |

### Docs

| File | Notes |
|---|---|
| `docs/DeribitIndicatorProject.md` | This handover document |
| `docs/architecture.md` | Full codebase structure map |
| `docs/trader-profile.md` | User trading profile |
| `docs/bbw-scoring-proposal.md` | Historical proposal |
| `docs/bbw-scoring-response.md` | Historical response |
| `docs/dual-scoring-fix-proposal.md` | Historical proposal |
| `docs/dual-scoring-fix-response.md` | Historical response |

---

## 4. Architecture Summary

See `docs/architecture.md` for the full annotated structure map.

```
UI/MainForm_Analysis.vb  →  RunAnalysisAsync()
  ├─ MTF TTL check: re-fetch candles15m only if _mtfLastFetchTime > 60s ago
  ├─ DeribitClient        fetches candles1m(250), candles5m(210),
  │                       candles15m(70) [TTL-gated], funding, bookSummary,
  │                       orderBook(depth10), recentTrades(100)
  ├─ IndicatorEngine      fills IndicatorResults (r)
  │    ├─ CalcATR, CalcROCSeries, CalcRSI
  │    ├─ CalcRSIDivergence (pivotWing + lookbackBars from cfg — T3-B)
  │    ├─ CalcVolumeSMA, CalcDMI
  │    ├─ CalcVWAP (dual-session from cfg), CalcVWAPBands
  │    ├─ CalcBBW, CalcTTMSqueeze (flatThreshold from cfg — T3-C)
  │    ├─ CalcEMA (1m ribbon 9/21/50 + 5m EMA200)
  │    ├─ CalcOFI (dominance thresholds + bookDepth from cfg)
  │    ├─ CalcLiquidations (dominanceRatio from cfg — T3-D)
  │    ├─ CalcCVD (3-seg weighted slope), CalcMicroCVD, CalcTFI
  │    ├─ CalcMTFGate (15m DMI/ADX + EMA confluence gate)
  │    ├─ CalcDonchian → DonchianSignal (LONG/SHORT/LONG_PARTIAL/SHORT_PARTIAL/NONE)
  │    ├─ CalcOBV
  │    ├─ CalcVPFRLite (exp-decay, numBuckets from cfg — T3-A)
  │    └─ DynamicNorms.Compute
  ├─ ScoringEngine.Calculate(r, posState, norms, cfg)  →  VerdictResult
  │    ├─ MTF veto: forces NO TRADE if MTFGatePass = False
  │    ├─ RSI zones read from cfg.Indicators.RSI (Overbought/Oversold/Partial*) [P8]
  │    ├─ ADX threshold read from cfg.Indicators.ADX.TrendThreshold [P9]
  │    ├─ VWAP warmup read from cfg.Indicators.VWAP.WarmupCandles [P9]
  │    ├─ RSI divergence penalty trigger from cfg.Indicators.RSI.DivPenaltyHigh/Low
  │    ├─ ROC partial dead-band from cfg.Indicators.ROC.PartialThreshold
  │    ├─ ATR target/stop multipliers from cfg.Scoring.AtrTargetMultiplier/AtrStopMultiplier
  │    ├─ BBW squeeze penalty from cfg.Scoring.BbwSqueezePenalty
  │    ├─ Liq standard/large penalty from cfg.Scoring.LiqStandardPenalty/LiqLargePenalty
  │    ├─ Funding step deltas from cfg.Scoring.FundingHighPenalty/HighBoost/LowPenalty
  │    ├─ Donchian NONE mid-channel note in breakdown (T2-C)
  │    ├─ Donchian quartile partial upgrade
  │    ├─ Volume mid-tier directional partial upgrade
  │    ├─ OBV upgrade blocked on adverse divergence
  │    └─ VPFR HVN cap: sets AdjustedLongTarget / AdjustedShortTarget
  ├─ AnalysisLogger.LogRun
  └─ UI/MainForm_Render.vb  →  RenderOutput()  →  txtOutput + lblVerdict
```

### Last Transacted Price
Fetched from `recentTrades(0).Price` (Deribit returns newest-first). Displayed above the
ATR Entry Levels block. **Not** used as the ATR entry price — that remains `candles1m.Last().Close`.

### VPFR HVN Target Cap
When VPFR-lite detects a High-Volume Node (HVN) wall between the entry price and the raw
ATR target, `ScoringEngine.Calculate` sets `v.AdjustedLongTarget` or `v.AdjustedShortTarget`
to the POC price and `v.TargetCapReason` to a descriptive string.
`RenderOutput` shows the raw target in dim and the capped target in **amber bold** with the reason.
When no cap fires, the display is identical to the pre-cap behaviour.

---

## 5. Indicator Signal Map

### Core Signals (always scored)
| Indicator | Method | Notes |
|---|---|---|
| ROC(9) | CalcROCSeries | Lookback from cfg. Partial dead-band from `cfg.Indicators.ROC.PartialThreshold` (default 0.1). |
| RSI(9) | CalcRSI | Full zones: `cfg.Indicators.RSI.Overbought` (60) / `Oversold` (40). Partial zones: `PartialOverbought` (50) / `PartialOversold` (50). |
| RSI Divergence | CalcRSIDivergence | −1 long when BEARISH + RSI > `DivPenaltyRsiHigh` (65); −1 short when BULLISH + RSI < `DivPenaltyRsiLow` (35). `PivotWing` and `LookbackBars` now passed from cfg **(T3-B)**. |
| DMI/ADX | CalcDMI | 5m candles. ADX threshold reads `cfg.Indicators.ADX.TrendThreshold`. |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms. Mid-tier directional partial via cross-confirm. |

### Tier 1
| Indicator | Method | Notes |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session. Warmup guard reads `cfg.Indicators.VWAP.WarmupCandles`. |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED logic when price between bands. |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | BBW squeeze penalty from cfg. TTM `flatThreshold` now passed from `cfg.Indicators.TTM.FlatThreshold` **(T3-C)**. |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) as regime anchor. |
| Funding Rate | GetFundingRateAsync | Step 3 deltas from cfg. |
| OI Change | OiSnapshot ring buffer | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL. |

### Tier 2
| Indicator | Method | Notes |
|---|---|---|
| OFI | CalcOFI | Configurable depth via `cfg.Indicators.OFI.BookDepth`. Dominance thresholds from cfg. |
| Liquidations | CalcLiquidations | `dominanceRatio` now passed from `cfg.Indicators.Liquidations.DominanceRatio` **(T3-D)**. Default 1.0 preserves prior behaviour. Penalty magnitudes from cfg. |
| CVD | CalcCVD | 3-segment weighted slope (late×2 − early×1). −1 penalty on divergence. |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL + FLAT stall penalty (T2-A). Window=50 via cfg. |
| TFI | CalcTFI | BUY/SELL PRESSURE. Window=30 via cfg. |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below. |

### Tier 3
| Indicator | Method | Notes |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT + quartile partial + NONE mid-channel note **(T2-C)**. |
| OBV | CalcOBV | Trend + divergence gate from cfg. Adverse divergence blocks cross-category upgrade. |
| VPFR-lite | CalcVPFRLite | POC proximity; HVN wall triggers target cap. Exp decay weighting. `numBuckets` now passed from `cfg.Indicators.VPFR.NumBuckets` **(T3-A)**. |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK; forces NO TRADE on BLOCK. TTL cache 60s. Regime hysteresis 1-bar grace period **(T1-B)**. |

---

## 6. settings.json Structure

All scoring and indicator gate parameters are externalised here.
`SettingsLoader.Initialise()` is called in `MainForm.New()` and loads
`settings.json` from the exe directory. `SettingsLoader.Current` returns
the singleton `EngineSettings` instance. Current file version: **v6**.

Key sections:
```
settings.json
  indicators:
    rsi:           { period, overbought (60), oversold (40),
                     partial_overbought (50), partial_oversold (50),
                     div_penalty_rsi_high (65), div_penalty_rsi_low (35),
                     divergencePriceGate, divergenceRsiDelta,
                     pivot_wing (2),           ← wired T3-B
                     lookback_bars (20) }      ← wired T3-B
    roc:           { period, seriesLookback, partial_threshold (0.1) }
    adx:           { trendThreshold (25), rangeThreshold (20) }
    vwap:          { devThresholdPct, session1/2 times, warmupCandles (15) }
    ofi:           { bookDepth (3), buyDominantRatio (1.2),
                     sellDominantRatio (0.833) }
    obv:           { trendGate, divergenceGate }
    liquidations:  { largeLiqSize,
                     dominance_ratio (1.0) }   ← wired T3-D
    vpfr:          { num_buckets (50) }        ← wired T3-A
    ttm:           { flat_threshold (0.5) }    ← wired T3-C
    cvd:           { slopeMinUsd, slopePctOfValue,
                     divergencePriceGate, tradeLookback }
    tfi:           { window_size (30), threshold (0.15) }
    microCvd:      { window_size (50), accel_threshold (5000) }
  scoring:
    verdictStrongPct / verdictMedPct / verdictWeakPct
    fundingHighPositive / fundingLowPositive
    fundingHighNegative / fundingLowNegative
    bbw_squeeze_penalty (1)
    liq_standard_penalty (1)
    liq_large_penalty (2)
    funding_high_penalty (2)
    funding_high_boost (1)
    funding_low_penalty (1)
    atr_target_multiplier (3.0)
    atr_stop_multiplier (1.5)
  mtfGate:
    enabled, dmiPeriod, requiredConfirms, candleCount
  regimeGates:
    transitionalAdxPenaltyLow / Mid / High
    transitionalPenaltyLow / Mid
```

---

## 7. ScoringEngine Logic Summary

- **MaxScore** = 19 (TRENDING), 18 (RANGE_BOUND), 15 (TRANSITIONAL)
- Verdict thresholds: `Math.Ceiling(regimeMax * pct)` using `verdictStrong/Med/WeakPct`
- **All signal thresholds now read from `cfg`** — no hardcoded magic numbers remain.
- **Step 2:** Score each signal into a `ScoreState` (long pts / short pts)
  - RSI full/partial zones, divergence penalty — all from cfg
  - ADX trend gate: `cfg.Indicators.ADX.TrendThreshold`
  - VWAP warmup: `cfg.Indicators.VWAP.WarmupCandles`
  - ROC partial dead-band: `cfg.Indicators.ROC.PartialThreshold`
  - OFI dominance thresholds wired into `CalcOFI` call
  - BBW squeeze penalty: `cfg.Scoring.BbwSqueezePenalty`
  - Liquidation penalty: `cfg.Scoring.LiqStandardPenalty` / `LiqLargePenalty`
  - **Donchian NONE**: mid-channel note annotated in breakdown **(T2-C)**
- **Pass 2:** Upgrade partials when cross-category confirmation exists
- **Step 3:** Funding modifier from cfg
- **Step 4:** Regime veto or TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate veto → NO TRADE
- **Step 5:** Verdict thresholds
- **Step 6:** CalcHoldStatus
- **Step 7:** ATR target/stop from cfg multipliers

---

## 8. ATR Entry / Stop / Target Display

- ATR value (from CalcATR on 1m candles) and current ATR scale factor (vs DynamicNorms ref ATR)
- **Entry price** = `r.CurrentPrice` = `candles1m.Last().Close`
- **Last transacted price** = `recentTrades(0).Price` — shown separately above ATR block
- Long: Stop = price − (ATR × scale × `AtrStopMultiplier`), Target = price + (ATR × scale × `AtrTargetMultiplier`)
- Short: mirrored
- R:R always 1:2 at default multipliers (1.5 stop / 3.0 target)
- **HVN cap:** if `v.AdjustedLongTarget > 0` (or Short), render dims raw target and shows POC-capped target in amber bold.

---

## 9. Verdict Levels

| Verdict | Meaning |
|---|---|
| STRONG LONG | High-confidence long |
| LONG | Standard long |
| WEAK LONG | Low-confidence long |
| NO TRADE | Insufficient signal or MTF block |
| WEAK SHORT | Low-confidence short |
| SHORT | Standard short |
| STRONG SHORT | High-confidence short |

---

## 10. Open Position Guidance (CalcHoldStatus)

When `posState = InLong` or `InShort`, `CalcHoldStatus` computes a hold/exit/flip guidance
field appended to the breakdown. Priority: (1) 2+ adverse microstructure signals → fast EXIT;
(2) OBV divergence exit; (3) RSI divergence evaluate; (4) single adverse microstructure warning;
(5) RSI/ROC structural assessment.

---

## 11. CSV Logging

`AnalysisLogger.LogRun(r, verdict)` appends one row per run to `analysis_log.csv` in the
exe directory. `CalibrationReport` summarises recent directional accuracy.

---

## 12. Auto-Run

Timer-driven auto-analysis triggered every N seconds/minutes via `MainForm_AutoRun.vb`.
Interval configurable from the UI. Minimum 10 seconds enforced. `AutoRunSettings` in
`settings.json` can pre-set the interval and enable flag.

---

## 13. DynamicNorms

`DynamicNorms.Compute(candles1m, r.ATR)` calculates:
- `ATRScaleFactor`: current ATR vs reference → scales stop/target distances
- `VolHighThreshold` / `VolMidThreshold`: regime-adjusted volume thresholds
- `VWAPDevThreshold`: dynamic VWAP deviation threshold (clamped from settings)

---

## 14. WATCHING / Future Work

| Item | Description | Priority |
|---|---|---|
| TFI threshold tuning | After TFI window (30) runs live, evaluate whether threshold=0.15 needs lowering to 0.10 for BTC-PERPETUAL tick size. | Low |
| MicroCVD accelThreshold calibration | Default 5000 USD. May need dynamic scaling vs ATR or volumeSMA on quiet sessions. | Low |
| AtrTargetMultiplier live calibration | Now externalised (default 3.0). Review against logged R:R outcomes after 50+ trades. | Low |
| OFI ratio live calibration | BuyDominantRatio=1.2 / SellDominantRatio=0.833 now hot-reloadable. Review against OFI hit rate in CalibrationReport. | Low |
| TTM flatThreshold calibration | Now wired from cfg (default 0.5). Review FLAT vs RISING/FALLING classification against 1m candle range distribution. | Low |
| VPFR numBuckets calibration | Now wired from cfg (default 50). Higher values increase POC resolution at cost of sparse buckets on quiet sessions. | Low |
| Liq dominanceRatio calibration | Now wired from cfg (default 1.0 = equal-or-greater). Review false LONG/SHORT LIQS signals at default; consider raising to 1.2–1.5. | Low |

---

## 15. Backlog

*(cleared — all Commit 1–5 items shipped)*

---

## 16. Version History

| Version | Key Changes |
|---|---|
| **Commit 5** | **[T2-C]** Donchian NONE mid-channel note added to scoring breakdown. **[T3-A]** CalcVPFRLite `numBuckets` wired from `cfg.Indicators.VPFR.NumBuckets`. **[T3-B]** CalcRSIDivergence `pivotWing` + `lookbackBars` wired from cfg. **[T3-C]** CalcTTMSqueeze `flatThreshold` wired from `cfg.Indicators.TTM.FlatThreshold`. **[T3-D]** CalcLiquidations `dominanceRatio` param added; wired from `cfg.Indicators.Liquidations.DominanceRatio`. |
| **Commit 4** | **[T1-B]** Regime ADX hysteresis — 1-bar grace period before RANGE_BOUND flip from TRENDING/TRANSITIONAL (`_prevRegime` field). **[T2-A]** MicroCVD FLAT stall penalty (price/CVD contradiction). **[T2-B]** OFI BookDepth injectable via cfg; dynamic descending weight array in CalcOFI. |
| v0.49 | [P8] RSI zones/div penalty wired to cfg. [P9] ADX threshold + VWAP warmup wired in scoring. [P10] ROC partial dead-band; OFI dominance thresholds wired. [P11] ATR multipliers externalised. [P12] BBW/Liq/Funding penalty magnitudes externalised. EngineSettings v0.37. settings.json v6. |
| v0.48 | [P4] TFI window separated from MicroCVD. TfiSettings + MicroCvdSettings added to EngineSettings (v0.36). |
| v0.47 | [P1] MTF TTL cache; [P2] RSI div penalty; [P3] CVD 3-seg slope; [P4] Donchian quartile; [P5] volMid partial; [P6] OBV div block; [P7] VPFR exp decay |
| v0.46 | RenderOutput refactor; VPFR HVN target cap display; last transacted price block |
| v0.45 | MicroCVD sign-aware penalty; CVD divergence penalty fix |
| v0.44 | VPFR-lite HVN cap in ScoringEngine; AdjustedLongTarget/ShortTarget |
| v0.43 | CalcVPFRLite added; ScoringEngine POC proximity scoring |
| v0.42 | OBV adverse divergence gate; cross-category upgrade logic |
| v0.41 | Donchian quartile signal scaffolding |
| v0.40 | DynamicNorms volume thresholds; volMid partial scoring |
| v0.39 | Dual-session VWAP; warmup guard |
| v0.38 | MicroCVD 3-segment; BULL/BEAR_ACCEL/DECEL |
| v0.37 | CalcRSIDivergence added |
| v0.36 (settings) | AutoRunSettings added |
| v0.35 | Auto-run timer UI |
| v0.34 | MTFGate RSI fields removed |
| v0.33 | MTFGateSettings + CalcMTFGate + 15m TTL fetch |
| v0.32 | VWAP session timing in settings |
| v0.31 | CVDSettings in EngineSettings |
| v0.30 | RSI div gates; OBV gates; ScoringWeights |
