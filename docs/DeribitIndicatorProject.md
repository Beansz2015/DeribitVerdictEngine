# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-10 | Current version: v0.49**

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
| `Core/ScoringEngine_Calculate.vb` | MaxScore const + full Calculate() pipeline **(v0.49 — all thresholds read from cfg)** |
| `Core/IndicatorResults.vb` | IndicatorResults struct — all indicator output fields |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcEMAList, CalcRSI, CalcRSISeries, CalcRSIDivergence, CalcROCSeries, CalcVolumeSMA |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze |
| `Core/Indicators_OrderFlow.vb` | CalcOFI **(OFI dominance thresholds wired to cfg, v0.49)**, CalcCVD, CalcMicroCVD, CalcTFI, CalcLiquidations |
| `Core/Indicators_Structure.vb` | CalcDonchian, CalcOBV, CalcVPFRLite (exp decay v0.47), CalcMTFGate |

### Core/Settings/

| File | Notes |
|---|---|
| `Core/Settings/EngineSettings.vb` | **v0.37** — Added `BbwSqueezePenalty`, `LiqStandardPenalty`, `LiqLargePenalty`, `FundingHighPenalty`, `FundingHighBoost`, `FundingLowPenalty`, `AtrTargetMultiplier`, `AtrStopMultiplier` to `ScoringSettings` (v0.49 P11/P12). TfiSettings + MicroCvdSettings from v0.48. |

### UI/ — MainForm partial classes

| File | Version | Notes |
|---|---|---|
| `UI/MainForm_Layout.vb` | v0.47 | Constants, DllImport/RECT, New(), ResizeControls(), SetOutputMargins(), OnFormHandleCreated(), CentreNudText(); shared fields: colour palette, _oiHistory, auto-run state vars, MTF TTL cache fields |
| `UI/MainForm_AutoRun.vb` | v0.47 | InitAutoRunControls(), btnStartStop_Click, StartAutoRun(), StopAutoRun(), RunAutoAnalysis(), OnCountdownTick(), UpdateCountdownLabel() |
| `UI/MainForm_Analysis.vb` | v0.48 | btnAnalyze_Click, RunAnalysisAsync() — MTF TTL re-fetch; Donchian quartile call-site; independent TFI/MicroCVD window sizes |
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
  │    ├─ CalcATR, CalcROCSeries, CalcRSI, CalcRSIDivergence, CalcVolumeSMA
  │    ├─ CalcDMI, CalcVWAP (dual-session from cfg), CalcVWAPBands
  │    ├─ CalcBBW, CalcTTMSqueeze
  │    ├─ CalcEMA (1m ribbon 9/21/50 + 5m EMA200)
  │    ├─ CalcOFI (dominance thresholds from cfg, v0.49)
  │    ├─ CalcLiquidations, CalcCVD (3-seg weighted slope), CalcMicroCVD
  │    ├─ CalcTFI (tfiWindowSize from cfg, default 30)
  │    ├─ CalcMTFGate (15m DMI/ADX + EMA confluence gate)
  │    ├─ CalcDonchian → DonchianSignal (LONG/SHORT/LONG_PARTIAL/SHORT_PARTIAL)
  │    ├─ CalcOBV
  │    ├─ CalcVPFRLite (exp-decay weighted volume profile)
  │    └─ DynamicNorms.Compute
  ├─ ScoringEngine.Calculate(r, posState, norms, cfg)  →  VerdictResult
  │    ├─ MTF veto: forces NO TRADE if MTFGatePass = False
  │    ├─ RSI zones read from cfg.Indicators.RSI (Overbought/Oversold/Partial*) [P8]
  │    ├─ ADX threshold read from cfg.Indicators.ADX.TrendThreshold [P9]
  │    ├─ VWAP warmup read from cfg.Indicators.VWAP.WarmupCandles [P9]
  │    ├─ RSI divergence penalty trigger from cfg.Indicators.RSI.DivPenaltyHigh/Low [P8]
  │    ├─ ROC partial dead-band from cfg.Indicators.ROC.PartialThreshold [P10]
  │    ├─ ATR target/stop multipliers from cfg.Scoring.AtrTargetMultiplier/AtrStopMultiplier [P11]
  │    ├─ BBW squeeze penalty from cfg.Scoring.BbwSqueezePenalty [P12]
  │    ├─ Liq standard/large penalty from cfg.Scoring.LiqStandardPenalty/LiqLargePenalty [P12]
  │    ├─ Funding step deltas from cfg.Scoring.FundingHighPenalty/HighBoost/LowPenalty [P12]
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
| ROC(9) | CalcROCSeries | Lookback from cfg. Partial dead-band from `cfg.Indicators.ROC.PartialThreshold` (default 0.1, v0.49). |
| RSI(9) | CalcRSI | Full zones: `cfg.Indicators.RSI.Overbought` (60) / `Oversold` (40). Partial zones: `PartialOverbought` (50) / `PartialOversold` (50). **All now read from cfg (v0.49 P8).** |
| RSI Divergence | CalcRSIDivergence | −1 long when BEARISH + RSI > `cfg.Indicators.RSI.DivPenaltyRsiHigh` (65); −1 short when BULLISH + RSI < `cfg.Indicators.RSI.DivPenaltyRsiLow` (35). **Wired to cfg v0.49 P8.** |
| DMI/ADX | CalcDMI | 5m candles. ADX threshold reads `cfg.Indicators.ADX.TrendThreshold` in scoring (was hardcoded 25, **fixed v0.49 P9**). |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms. Mid-tier directional partial via cross-confirm (v0.47). |

### Tier 1
| Indicator | Method | Notes |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session. Warmup guard reads `cfg.Indicators.VWAP.WarmupCandles` in scoring (was hardcoded 15, **fixed v0.49 P9**). |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED logic when price between bands. |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | BBW squeeze penalty reads `cfg.Scoring.BbwSqueezePenalty` (default 1, **v0.49 P12**). |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) as regime anchor. |
| Funding Rate | GetFundingRateAsync | Step 3 deltas: `FundingHighPenalty`=2, `FundingHighBoost`=1, `FundingLowPenalty`=1. **All from cfg (v0.49 P12).** |
| OI Change | OiSnapshot ring buffer | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL. |

### Tier 2
| Indicator | Method | Notes |
|---|---|---|
| OFI | CalcOFI | Top-3 depth levels, bid/ask imbalance. Dominance thresholds read `cfg.Indicators.OFI.BuyDominantRatio` / `SellDominantRatio` (**wired v0.49 P10**, was hardcoded 1.2/0.833). |
| Liquidations | CalcLiquidations | Penalty magnitudes: `cfg.Scoring.LiqStandardPenalty` (1) and `LiqLargePenalty` (2) (**v0.49 P12**). |
| CVD | CalcCVD | 3-segment weighted slope (late×2 − early×1). −1 penalty on divergence. |
| MicroCVD | CalcMicroCVD | BULL/BEAR_ACCEL/DECEL; sign-aware penalty −1 opposing. Window=50 via cfg. |
| TFI | CalcTFI | BUY/SELL PRESSURE. Window=30 via cfg. |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below. |

### Tier 3
| Indicator | Method | Notes |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT + quartile partial (v0.47). Partial upgrades via cross-confirm. |
| OBV | CalcOBV | Trend + divergence gate from cfg. Adverse divergence blocks cross-category upgrade (v0.47). |
| VPFR-lite | CalcVPFRLite | POC proximity; HVN wall triggers target cap. Exp decay weighting (v0.47). |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK; forces NO TRADE on BLOCK. TTL cache 60s (v0.47). |

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
                     div_penalty_rsi_high (65), div_penalty_rsi_low (35),   ← NEW v6
                     divergencePriceGate, divergenceRsiDelta }
    roc:           { period, seriesLookback,
                     partial_threshold (0.1) }                               ← NEW v6
    adx:           { trendThreshold (25) }                     ← now used in scoring too
    vwap:          { devThresholdPct, session1/2 times,
                     warmupCandles (15) }                      ← now used in scoring too
    ofi:           { bookDepth, buyDominantRatio (1.2),        ← NOW wired into CalcOFI
                     sellDominantRatio (0.833) }
    obv:           { trendGate, divergenceGate }
    liquidations:  { largeLiqSize }
    cvd:           { slopeMinUsd, slopePctOfValue,
                     divergencePriceGate, tradeLookback }
    tfi:           { window_size (30), threshold (0.15) }
    microCvd:      { window_size (50), accel_threshold (5000) }
  scoring:
    verdictStrongPct / verdictMedPct / verdictWeakPct
    fundingHighPositive / fundingLowPositive
    fundingHighNegative / fundingLowNegative
    bbw_squeeze_penalty (1)                                    ← NEW v6
    liq_standard_penalty (1)                                   ← NEW v6
    liq_large_penalty (2)                                      ← NEW v6
    funding_high_penalty (2)                                   ← NEW v6
    funding_high_boost (1)                                     ← NEW v6
    funding_low_penalty (1)                                    ← NEW v6
    atr_target_multiplier (3.0)                                ← NEW v6
    atr_stop_multiplier (1.5)                                  ← NEW v6
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
- **All signal thresholds now read from `cfg`** — no hardcoded magic numbers remain in scoring logic as of v0.49.
- **Step 2:** Score each signal into a `ScoreState` (long pts / short pts)
  - RSI full zones: `cfg.Indicators.RSI.Overbought` / `Oversold` [P8]
  - RSI partial zones: `cfg.Indicators.RSI.PartialOverbought` / `PartialOversold` [P8]
  - RSI divergence penalty: fires when RSI > `DivPenaltyRsiHigh` (65) or < `DivPenaltyRsiLow` (35) [P8]
  - ADX trend gate: `cfg.Indicators.ADX.TrendThreshold` [P9]
  - VWAP warmup: `cfg.Indicators.VWAP.WarmupCandles` [P9]
  - ROC partial dead-band: `cfg.Indicators.ROC.PartialThreshold` [P10]
  - OFI dominance thresholds wired into `CalcOFI` call [P10]
  - BBW squeeze penalty: `cfg.Scoring.BbwSqueezePenalty` [P12]
  - Liquidation penalty: `cfg.Scoring.LiqStandardPenalty` / `LiqLargePenalty` [P12]
- **Pass 2:** Upgrade partials when cross-category confirmation exists
- **Step 3:** Funding modifier: `cfg.Scoring.FundingHighPenalty` (−2/+1), `FundingLowPenalty` (−1) [P12]
- **Step 4:** Regime veto or TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate veto → NO TRADE
- **Step 5:** Verdict thresholds
- **Step 6:** CalcHoldStatus
- **Step 7:** ATR target = `ATR × scale × cfg.Scoring.AtrTargetMultiplier` (default 3.0); stop = `× AtrStopMultiplier` (default 1.5) [P11]

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
|---|---|
| TFI threshold tuning | After TFI window (30) runs live, evaluate whether threshold=0.15 needs lowering to 0.10 for BTC-PERPETUAL tick size. | Low |
| MicroCVD accelThreshold calibration | Default 5000 USD. May need dynamic scaling vs ATR or volumeSMA on quiet sessions. | Low |
| AtrTargetMultiplier live calibration | Now externalised (default 3.0). Review against logged R:R outcomes after 50+ trades. | Low |
| OFI ratio live calibration | BuyDominantRatio=1.2 / SellDominantRatio=0.833 now hot-reloadable. Review against OFI hit rate in CalibrationReport. | Low |

---

## 15. Backlog

*(cleared — all P1–P12 shipped as of v0.49)*

---

## 16. Version History

| Version | Key Changes |
|---|---|
| v0.49 | **[P8]** RSI full/partial zones and divergence penalty triggers wired to cfg. **[P9]** ADX threshold and VWAP warmup wired to cfg in scoring (were reading cfg in analysis but hardcoded 25/15 in scoring). **[P10]** ROC partial dead-band `partial_threshold` added to settings; OFI dominance thresholds wired into `CalcOFI` call. **[P11]** `AtrTargetMultiplier` (3.0) and `AtrStopMultiplier` (1.5) added to `ScoringSettings`; Step 7 reads from cfg. **[P12]** `BbwSqueezePenalty`, `LiqStandardPenalty`, `LiqLargePenalty`, `FundingHighPenalty`, `FundingHighBoost`, `FundingLowPenalty` added to `ScoringSettings`; scoring logic reads all from cfg. `EngineSettings.vb` → v0.37. `settings.json` → v6. |
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
