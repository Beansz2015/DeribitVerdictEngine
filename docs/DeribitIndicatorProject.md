# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-10 | Current version: v0.48**

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
| `settings.json` | All tunable parameters (see Section 6) |
| `MainForm.Designer.vb` | Auto-generated WinForms designer file (do not edit manually) |
| `MainForm.resx` | Form resources |

### Core/ — ScoringEngine + IndicatorEngine partial classes

| File | Notes |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult (incl. AdjustedLongTarget, AdjustedShortTarget, TargetCapReason), PositionState, SignalCategory, ScoreState |
| `Core/ScoringEngine_Helpers.vb` | RegimeMaxScore, Threshold, TierFloor, AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus |
| `Core/ScoringEngine_Calculate.vb` | MaxScore const + full Calculate() pipeline (v0.47) |
| `Core/IndicatorResults.vb` | IndicatorResults struct — all indicator output fields |
| `Core/Indicators_Momentum.vb` | CalcDMI, CalcATR, CalcEMA, CalcEMAList, CalcRSI, CalcRSISeries, CalcRSIDivergence, CalcROCSeries, CalcVolumeSMA |
| `Core/Indicators_Volatility.vb` | CalcVWAP (dual-session), CalcVWAPBands, CalcBBW, CalcTTMSqueeze |
| `Core/Indicators_OrderFlow.vb` | CalcOFI, CalcCVD (3-segment weighted slope v0.47), CalcMicroCVD, **CalcTFI (dedicated tfiWindowSize=30, v0.48)**, CalcLiquidations |
| `Core/Indicators_Structure.vb` | CalcDonchian, CalcOBV, CalcVPFRLite (exp decay v0.47), CalcMTFGate |

### Core/Settings/

| File | Notes |
|---|---|
| `Core/Settings/EngineSettings.vb` | v0.36 — Strongly-typed POCO for settings.json; **TfiSettings (WindowSize=30, Threshold=0.15) and MicroCvdSettings (WindowSize=50, AccelThreshold=5000) added (v0.48)** |

### UI/ — MainForm partial classes

| File | Version | Notes |
|---|---|---|
| `UI/MainForm_Layout.vb` | v0.47 | Constants, DllImport/RECT, New(), ResizeControls(), SetOutputMargins(), OnFormHandleCreated(), CentreNudText(); shared fields: colour palette, _oiHistory, auto-run state vars, MTF TTL cache fields (_mtfCandles15m, _mtfLastFetchTime, MTF_TTL_SECONDS=60) |
| `UI/MainForm_AutoRun.vb` | v0.47 | InitAutoRunControls(), btnStartStop_Click, StartAutoRun(), StopAutoRun(), RunAutoAnalysis(), OnCountdownTick(), UpdateCountdownLabel() |
| `UI/MainForm_Analysis.vb` | v0.48 | btnAnalyze_Click, RunAnalysisAsync() — MTF TTL re-fetch logic; Donchian quartile call-site; **CalcTFI and CalcMicroCVD call sites now pass independent window sizes from cfg (v0.48)** |
| `UI/MainForm_Render.vb` | v0.46 | RenderOutput(), AppendRtf(), AR(), SectionHeader(), Divider(), BuildCalibrationReport(), Flag(), UpdateLogInfo(), lnkResetLog_LinkClicked, lnkCalibCheck_LinkClicked |

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
  │    ├─ CalcOFI (top-3 weighted bid/ask imbalance)
  │    ├─ CalcLiquidations, CalcCVD (3-seg weighted slope), CalcMicroCVD
  │    ├─ CalcTFI (tfiWindowSize from cfg.Indicators.TFI.WindowSize, default 30)
  │    ├─ CalcMTFGate (15m DMI/ADX + EMA confluence gate)
  │    ├─ CalcDonchian → DonchianSignal (LONG/SHORT/LONG_PARTIAL/SHORT_PARTIAL)
  │    ├─ CalcOBV
  │    ├─ CalcVPFRLite (exp-decay weighted volume profile)
  │    └─ DynamicNorms.Compute
  ├─ ScoringEngine.Calculate(r, posState, norms, cfg)  →  VerdictResult
  │    ├─ MTF veto: forces NO TRADE if MTFGatePass = False
  │    ├─ RSI divergence penalty: −1 long (BEARISH+RSI>65) / −1 short (BULLISH+RSI<35)
  │    ├─ Donchian quartile partial upgrade (LONG_PARTIAL/SHORT_PARTIAL)
  │    ├─ Volume mid-tier directional partial upgrade
  │    ├─ OBV upgrade blocked on adverse divergence
  │    └─ VPFR HVN cap: sets AdjustedLongTarget / AdjustedShortTarget when POC blocks raw target
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
| ROC(9) | CalcROCSeries | Lookback from cfg.Indicators.ROC.SeriesLookback |
| RSI(9) | CalcRSI | Period from cfg.Indicators.RSI.Period |
| RSI Divergence | CalcRSIDivergence | **Now scored (v0.47).** −1 long when BEARISH + RSI > 65; −1 short when BULLISH + RSI < 35. Price gate + RSI delta gate from settings. |
| DMI/ADX | CalcDMI | 5m candles, period from settings |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms. Mid-tier (volMid) now scores partial when direction confirms (v0.47). |

### Tier 1
| Indicator | Method | Notes |
|---|---|---|
| VWAP Dev | CalcVWAP | Dual-session: anchor = 00:00 UTC if before session2 boundary, else 13:30 UTC (both times from settings). Warmup guard active. |
| VWAP σ Bands | CalcVWAPBands | σ1/σ2 bands; PARTIAL→UPGRADED logic when price between bands |
| BBW / TTM Squeeze | CalcBBW + CalcTTMSqueeze | BBW status + TTM histogram direction and signal |
| EMA Ribbon | CalcEMA | 9/21/50 on 1m → BULL/BEAR/MIXED; 5m EMA(200) as regime anchor |
| Funding Rate | GetFundingRateAsync | Info-only; modifies long/short scores in Step 3 |
| OI Change | OiSnapshot ring buffer | 15m + 60m delta → NEW LONGS/SHORTS/COVERING/CAPITULATION/NEUTRAL |

### Tier 2
| Indicator | Method | Notes |
|---|---|---|
| OFI | CalcOFI | Top-3 depth levels, bid/ask imbalance, weights 3/2/1 |
| Liquidations | CalcLiquidations | Penalty-only; large threshold from cfg |
| CVD | CalcCVD | Net delta + **3-segment weighted slope** (late×2 − early×1, v0.47) + divergence; −1 penalty on divergence |
| MicroCVD | CalcMicroCVD | 3-segment (early/mid/late) CVD; BULL/BEAR_ACCEL/DECEL; sign-aware penalty −1 when opposing verdict direction. **Window=50 (cfg.Indicators.MicroCVD.WindowSize, v0.48 — independent of TFI).** |
| TFI | CalcTFI | Trade Flow Imbalance; BUY/SELL PRESSURE signal; scored independently. **Window=30 (cfg.Indicators.TFI.WindowSize, v0.48 — separate from MicroCVD window).** |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below |

### Tier 3
| Indicator | Method | Notes |
|---|---|---|
| Donchian(20) | CalcDonchian | Full LONG/SHORT breakout + **upper/lower quartile partial** (LONG_PARTIAL/SHORT_PARTIAL, v0.47). Partial upgrades via cross-category confirm. |
| OBV | CalcOBV | Trend gate + divergence gate from cfg; adverse divergence suppresses full upgrade to partial only; **cross-category upgrade now also blocked on adverse divergence (v0.47)**. |
| VPFR-lite | CalcVPFRLite | Volume-profile POC; HVN/LVN proximity signal; HVN wall triggers target cap in RenderOutput. **Exponential decay weighting (decayBase=0.985, v0.47)** makes POC track intraday shifts. |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK only — not a score contributor; forces NO TRADE on BLOCK. **TTL cache implemented v0.47: 15m candles re-fetched only when cache > 60s stale.** |

---

## 6. settings.json Structure

All scoring and indicator gate parameters are externalised here.
`SettingsLoader.Initialise()` is called in `MainForm.New()` and loads
`settings.json` from the exe directory. `SettingsLoader.Current` returns
the singleton `EngineSettings` instance.

Key sections:
```
settings.json
  indicators:
    rsi:           { period, divergencePriceGate, divergenceRsiDelta }
    roc:           { period, seriesLookback }
    adx:           { trendThreshold }                               ← used by MTF gate for ADX min
    vwap:          { devThresholdPct,
                     session1StartHour, session1StartMinute,
                     session2StartHour, session2StartMinute,
                     warmupCandles }
    obv:           { trendGate, divergenceGate }
    liquidations:  { largeLiqSize }
    cvd:           { slopeMinUsd, slopePctOfValue,
                     divergencePriceGate, tradeLookback }
    tfi:           { window_size (default 30), threshold (default 0.15) }   ← NEW v0.48
    microCvd:      { window_size (default 50), accel_threshold (default 5000) }  ← NEW v0.48
  mtfGate:
    enabled        -- bool; if false the gate is bypassed
    dmiPeriod      -- DMI/ADX period for 15m calc
    requiredConfirms -- number of bull/bear confirms required to pass
    candleCount    -- 15m candle lookback
  scoring:
    verdictStrongPct / verdictMedPct / verdictWeakPct
    fundingHighPositive / fundingLowPositive
    fundingHighNegative / fundingLowNegative
  regimeGates:
    transitionalAdxPenaltyLow / Mid / High
    transitionalPenaltyLow / Mid
```

---

## 7. ScoringEngine Logic Summary

- **MaxScore** = 19 (TRENDING), 18 (RANGE_BOUND), 15 (TRANSITIONAL)
- Verdict thresholds: `Math.Ceiling(regimeMax * pct)` using `verdictStrong/Med/WeakPct`
- **Weighted scoring:** each signal category contributes a defined max to the long or short
  score pool. Partial scores are awarded when only one side of a cross-category confirm
  exists; full score on alignment.
- **Step 2:** Score each signal into a `ScoreState` (long pts / short pts)
- **Pass 2:** Upgrade partials when cross-category confirmation exists
- **Step 3:** Funding modifier adjusts L/S scores
- **Step 4:** Regime veto (TRENDING blocks counter-trend) or TRANSITIONAL ADX penalty
- **Step 4b:** MTF gate check — if `cfg.MTFGate.Enabled` and `r.MTFGatePass = False`,
  proposed direction is vetoed → verdict forced to NO TRADE; gate reason added to breakdown
- **Step 5:** Compare effectiveLS/SS to thresholds → Verdict + Confidence
- **Step 6:** CalcHoldStatus for open position guidance
- RSI divergence penalty (v0.47): −1 long when BEARISH + RSI > 65; −1 short when BULLISH + RSI < 35
- CVD divergence penalty: −1 before liquidation penalty
- MicroCVD sign-aware penalty: −1 applied to the **opposing** direction when BULL/BEAR_DECEL detected
- TFI: independent full score contributor (BUY/SELL PRESSURE). **Window now 30 via cfg (v0.48).**
- Liquidation penalty: −1 standard, −2 large
- OBV scoring: RISING/FALLING trend + non-adverse divergence = full score; adverse divergence = partial-upgrade only; **cross-category upgrade also blocked on adverse divergence (v0.47)**
- Volume mid-tier (v0.47): directional partial (volMidLong/volMidShort) upgrades via Volume cross-confirm
- Donchian quartile (v0.47): LONG_PARTIAL/SHORT_PARTIAL upgrades via MarketStructure cross-confirm

---

## 8. ATR Entry / Stop / Target Display

- ATR value (from CalcATR on 1m candles) and current ATR scale factor (vs DynamicNorms ref ATR)
- **Entry price** = `r.CurrentPrice` = `candles1m.Last().Close` (latest 1m candle close)
- **Last transacted price** = `recentTrades(0).Price` — shown separately above ATR block
- Long: Stop = price − (ATR × scale × 1.5), Target = price + (ATR × scale × 3.0)
- Short: mirrored
- R:R always 1:2
- **HVN cap:** if `v.AdjustedLongTarget > 0` (or Short), the render dims the raw target and
  shows the POC-capped target in amber bold with the `TargetCapReason` string.

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
field appended to the breakdown. Logic checks current verdict alignment with open position.

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
| TFI threshold tuning | After TFI window (30) runs live, evaluate whether threshold=0.15 filters noise correctly or needs lowering to 0.10 for BTC-PERPETUAL tick size. | Low |
| MicroCVD accelThreshold calibration | Default 5000 USD. May need dynamic scaling vs ATR or volumeSMA to avoid false DECEL on quiet sessions. | Low |

---

## 15. Backlog

*(cleared — all P1–P4 items shipped as of v0.48)*

---

## 16. Version History

| Version | Key Changes |
|---|---|
| v0.48 | **[P4]** TFI window separated from MicroCVD. CalcTFI param renamed `windowSize`→`tfiWindowSize` (default 30). CalcMicroCVD param renamed `windowSize`→`microWindowSize` (default 50). `TfiSettings` and `MicroCvdSettings` added to `EngineSettings` (v0.36). Call sites in `RunAnalysisAsync` now pass `cfg.Indicators.TFI.WindowSize` and `cfg.Indicators.MicroCVD.WindowSize` independently. |
| v0.47 | [P1] MTF TTL cache (60s); [P2] RSI divergence scoring penalty; [P3] CVD 3-segment weighted slope; [P4] Donchian quartile partial signal; [P5] Volume mid-tier directional partial; [P6] OBV cross-category upgrade blocked on adverse divergence; [P7] VPFR exponential decay |
| v0.46 | RenderOutput refactor; VPFR HVN target cap display; last transacted price block |
| v0.45 | MicroCVD sign-aware penalty; CVD divergence penalty fix |
| v0.44 | VPFR-lite HVN cap in ScoringEngine; AdjustedLongTarget/ShortTarget added to VerdictResult |
| v0.43 | CalcVPFRLite added; ScoringEngine POC proximity scoring |
| v0.42 | OBV adverse divergence gate; cross-category upgrade logic |
| v0.41 | Donchian quartile signal scaffolding (pre-scoring) |
| v0.40 | DynamicNorms volume thresholds; volMid partial scoring |
| v0.39 | Dual-session VWAP; VWAP warmup guard |
| v0.38 | MicroCVD 3-segment; BULL/BEAR_ACCEL/DECEL signals |
| v0.37 | CalcRSIDivergence added (info-only prior to v0.47) |
| v0.36 (settings) | AutoRunSettings added to EngineSettings |
| v0.35 | Auto-run timer UI + AutoRunTimer interface |
| v0.34 | MTFGate RSI fields removed; DMI/ADX/EMA 2-of-3 gate finalised |
| v0.33 | MTFGateSettings + CalcMTFGate + 15m TTL fetch |
| v0.32 | VWAP session timing in settings |
| v0.31 | CVDSettings in EngineSettings |
| v0.30 | RSI divergence gate params; OBV gate params; ScoringWeights |
