# DeribitVerdictEngine — Project Handover Document
**Last updated: 2026-04-09 | Current version: v0.45**

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

### Root files (single-class, unchanged by refactor)

| File | Version | Notes |
|---|---|---|
| `DeribitClient.vb` | current | All Deribit REST calls incl. 15m candles, recentTrades |
| `DynamicNorms.vb` | current | ATR/Vol/VWAP norm computation |
| `AnalysisLogger.vb` | current | CSV logging + CalibrationReport |
| `OiSnapshot.vb` | current | OI ring-buffer helper |
| `AutoRunTimer.vb` | current | IAutoRunTimer interface + WinFormsAutoRunTimer impl |
| `Program.vb` | current | Entry point |
| `settings.json` | current | All tunable parameters (see Section 6) |
| `MainForm.Designer.vb` | current | Auto-generated WinForms designer file (do not edit manually) |
| `MainForm.resx` | current | Form resources |

### Core/ — ScoringEngine partial classes

| File | Notes |
|---|---|
| `Core/ScoringEngine_Types.vb` | SignalBreakdownItem, VerdictResult, PositionState, SignalCategory, ScoreState |
| `Core/ScoringEngine_Helpers.vb` | RegimeMaxScore, Threshold, TierFloor, AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus |
| `Core/ScoringEngine_Calculate.vb` | MaxScore const + full Calculate() pipeline |

### Core/Settings/ — settings POCO

| File | Version | Notes |
|---|---|---|
| `Core/Settings/EngineSettings.vb` | v0.33 | Strongly-typed POCO for settings.json incl. MTFGateSettings |
| `SettingsLoader.vb` | current | JSON deserialisation, SettingsLoader.Current singleton |

### Core/Indicators/ — IndicatorEngine partial classes

| File | Notes |
|---|---|
| `Core/Indicators/IndicatorEngine_Types.vb` | IndicatorResults, Candle, BookEntry, TradeEntry, OiSnapshot |
| `Core/Indicators/IndicatorEngine_Core.vb` | CalcATR, CalcRSI, CalcROCSeries, CalcEMA, CalcVolumeSMA, CalcDMI |
| `Core/Indicators/IndicatorEngine_VWAP.vb` | CalcVWAP, CalcVWAPBands |
| `Core/Indicators/IndicatorEngine_OrderFlow.vb` | CalcOFI, CalcCVD, CalcLiquidations |
| `Core/Indicators/IndicatorEngine_Structure.vb` | CalcDonchian, CalcOBV, CalcRSIDivergence, CalcBBW, CalcTTMSqueeze, CalcMTFGate, CalcVPFRLite |

### UI/ — MainForm partial classes

| File | Version | Notes |
|---|---|---|
| `UI/MainForm_Layout.vb` | v0.45 | Constants, DllImport/RECT, New(), ResizeControls(), SetOutputMargins(), OnFormHandleCreated(), CentreNudText(); shared fields: colour palette, _oiHistory, auto-run state vars |
| `UI/MainForm_AutoRun.vb` | v0.45 | InitAutoRunControls(), btnStartStop_Click, StartAutoRun(), StopAutoRun(), RunAutoAnalysis(), OnCountdownTick(), UpdateCountdownLabel() |
| `UI/MainForm_Analysis.vb` | v0.45 | btnAnalyze_Click, RunAnalysisAsync() |
| `UI/MainForm_Render.vb` | v0.45 | RenderOutput(), AppendRtf(), AR(), SectionHeader(), Divider(), BuildCalibrationReport(), Flag(), UpdateLogInfo(), lnkResetLog_LinkClicked, lnkCalibCheck_LinkClicked |

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
  ├─ DeribitClient        fetches candles1m(250), candles5m(210), candles15m(70),
  │                       funding, bookSummary, orderBook(depth10), recentTrades(100)
  ├─ IndicatorEngine      fills IndicatorResults (r)
  │    ├─ CalcATR, CalcROCSeries, CalcRSI, CalcRSIDivergence, CalcVolumeSMA
  │    ├─ CalcDMI, CalcVWAP (session params from cfg), CalcVWAPBands
  │    ├─ CalcBBW, CalcTTMSqueeze
  │    ├─ CalcEMA (1m ribbon 9/21/50 + 5m EMA200)
  │    ├─ CalcOFI (top-3 weighted bid/ask imbalance)
  │    ├─ CalcLiquidations, CalcCVD
  │    ├─ CalcMTFGate (15m DMI/ADX + EMA confluence gate)
  │    ├─ CalcDonchian, CalcOBV
  │    ├─ CalcVPFRLite (volume-profile POC + HVN signal)
  │    └─ DynamicNorms.Compute
  ├─ ScoringEngine.Calculate(r, posState, norms, cfg)  →  VerdictResult
  │    └─ MTF veto: forces NO TRADE if MTFGatePass = False
  ├─ AnalysisLogger.LogRun
  └─ UI/MainForm_Render.vb  →  RenderOutput()  →  txtOutput + lblVerdict
```

### Last Transacted Price
Fetched from `recentTrades(0).Price` (Deribit returns newest-first). Displayed above the
ATR Entry Levels block. **Not** used as the ATR entry price — that remains `candles1m.Last().Close`.

---

## 5. Indicator Signal Map

### Core Signals (always scored)
| Indicator | Method | Notes |
|---|---|---|
| ROC(9) | CalcROCSeries | Lookback from cfg.Indicators.ROC.SeriesLookback |
| RSI(9) | CalcRSI | Period from cfg.Indicators.RSI.Period |
| RSI Divergence | CalcRSIDivergence | Price gate + RSI delta gate from settings |
| DMI/ADX | CalcDMI | 5m candles, period from settings |
| Volume | CalcVolumeSMA | SMA-9; H/M thresholds from DynamicNorms |

### Tier 1
| Indicator | Method | Notes |
|---|---|---|
| VWAP Dev | CalcVWAP | Session boundary times from cfg.Indicators.VWAP; warmup guard |
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
| CVD | CalcCVD | Net delta + slope + divergence; −1 penalty on divergence |
| 5m EMA(200) | CalcEMA(candles5m,200) | ABOVE/BELOW; short signal if price below |

### Tier 3
| Indicator | Method | Notes |
|---|---|---|
| Donchian(20) | CalcDonchian | LONG/SHORT/NONE breakout |
| OBV | CalcOBV | Trend gate + divergence gate from cfg |
| VPFR-lite | CalcVPFRLite | Volume-profile POC; HVN/LVN proximity signal |

### Multi-Timeframe Gate
| Indicator | Method | Notes |
|---|---|---|
| MTF Gate (15m) | CalcMTFGate | 15m DMI/ADX + EMA alignment; PASS/BLOCK only — not a score contributor; forces NO TRADE on BLOCK |

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

- **MaxScore** = 17 (TRENDING), 16 (RANGE_BOUND), 13 (TRANSITIONAL)
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
- CVD divergence penalty: −1 before liquidation penalty
- Liquidation penalty: −1 standard, −2 large
- OBV scoring (v0.32): RISING/FALLING trend + non-adverse divergence = full score;
  adverse divergence = partial-upgrade only

---

## 8. ATR Entry / Stop / Target Display

- ATR value (from CalcATR on 1m candles) and current ATR scale factor (vs DynamicNorms ref ATR)
- **Entry price** = `r.CurrentPrice` = `candles1m.Last().Close` (latest 1m candle close)
- **Last transacted price** = `recentTrades(0).Price` — shown separately above ATR block
- Long: Stop = price − (ATR × scale × 1.5), Target = price + (ATR × scale × 3.0)
- Short: mirrored
- R:R always 1:2

---

## 9. DynamicNorms

Computed from the last 250 × 1m candles on each analysis run.
Provides live adaptive thresholds for Volume and VWAP deviation,
and an ATR scale factor. Falls back to static defaults if insufficient data.

---

## 10. AnalysisLogger

Appends one CSV row per `Analyze Now` click to `analysis_log.csv` (next to exe).
Calibration Readiness Report checks:
- ≥ 300 total rows
- ≥ 3 sessions (days)
- ≥ 3 regimes with ≥ 50 rows each
- ≥ 2 liquidation events

---

## 11. Documentation Requirements (for AI continuations)

When continuing this project in a new conversation, the AI **must** follow these rules:

### a. Always read this file first
Fetch `docs/DeribitIndicatorProject.md` from GitHub at the start of every session.
This file overrides `indicator-spec.md` on all points of conflict.

### b. Update this file at the end of every session
After any code change is pushed to GitHub, update this handover doc with:
- Bumped version number in the header
- Updated version in the File Inventory table (Section 3)
- A summary of what changed and why in a new entry at the bottom (Section 13)

### c. Condition tracking
For any feature that requires a live condition to be observed before it can be
called complete, add it to Section 14 (Pending Observations) with status `WATCHING`.
Mark it `COMPLETE` once confirmed.

### d. GitHub push method
If `push_files` fails, fall back to `create_or_update_file` one file at a time.
Always read the current file SHA before updating (use `get_file_contents`).

### e. Version numbering
The app version is shown in `UI/MainForm_Layout.vb` → `Me.Text = "Deribit Verdict Engine vX.XX"`.
Partial-class files within the same logical unit share the same version.
- `UI/MainForm_*.vb` files: all carry the app version (currently v0.45)
- `Core/ScoringEngine_*.vb` files: independent minor version (currently v0.32 base, no new changes)
- `Core/Indicators/IndicatorEngine_*.vb` files: independent minor version
- `Core/Settings/EngineSettings.vb`: currently v0.33
- When any file changes, bump only that logical unit's version

---

## 12. Known Gaps / Not Yet Built

- Automated backtesting harness (manual log review only for now)
- Alert/notification system (no audio or push alerts)
- Multi-instrument support (BTC-PERPETUAL only)
- Settings UI (edit settings.json manually)

---

## 13. Change Log

| Version | Date | Changes |
|---|---|---|
| v0.01–v0.20 | (prior sessions) | Initial build: core indicators, DMI, VWAP, EMA ribbon, OI, OFI, Donchian, BBW, calibration log |
| v0.21–v0.25 | (prior sessions) | DynamicNorms, dual-score engine, regime veto, partial upgrade logic, MaxScore regime-aware |
| v0.26 | (prior sessions) | OBV + RSI divergence + CVD added |
| v0.27 (ScoringEngine) | 2026-04-06 | All verdict/penalty thresholds now read from EngineSettings |
| v0.28 (MainForm, Indicators) | 2026-04-06 | OFI call site updated for top-3 weighted bid/ask imbalance |
| v0.29 (MainForm) | 2026-04-06 | CalcCVD call site added; SettingsLoader.Initialise() in constructor |
| v0.30 (MainForm, EngineSettings, Indicators) | 2026-04-06 | Settings-driven gate params; CvdSettings class |
| v0.31 (MainForm, Indicators) | 2026-04-06 | CalcVWAP ByRef sessionCandleCount; CalcVWAPBands σ1/σ2 |
| v0.32 (MainForm, Indicators, EngineSettings, settings.json) | 2026-04-07 | VWAP session boundary times and warmup to settings.json |
| v0.33 (EngineSettings) | 2026-04-08 | MTFGateSettings class added |
| v0.34–v0.35 (MainForm, Indicators) | 2026-04-08 | CalcMTFGate implemented; 15m candles fetched |
| v0.36 (MainForm, Indicators) | 2026-04-08 | Build fixes: MTFGateSettings property names; ADX threshold mapping |
| v0.32 (ScoringEngine) | 2026-04-08 | MTF gate veto block; OBV scoring fix |
| v0.37 (MainForm) | 2026-04-08 | VolumeUSD field rename (was VolumeUSD_approx) |
| v0.38 (MainForm) | 2026-04-08 | Auto-run feature: interval NUD, Single/Repeat radio, Start/Stop button, countdown label |
| v0.38a (MainForm) | 2026-04-08 | SettingsLoader.Save signature fix |
| v0.39 (MainForm) | 2026-04-08 | 6 UI bug fixes (ChrW, Panel grouping, Single default, spacing, alignment) |
| v0.40 (MainForm) | 2026-04-09 | InitAutoRunControls forced False on load; AR_Y/TXT_Y equal-gap; RenderOutput rewritten to use AppendRtf colour-coded output |
| v0.41 (MainForm) | 2026-04-09 | NUD digit vertical centering via EM_SETRECT / SendMessage on inner TextBox handle |
| v0.42 (MainForm) | 2026-04-09 | Last transacted price line (recentTrades(0).Price) displayed above ATR Entry Levels; TIME line changed to UTC+8 |
| v0.43 (MainForm, Indicators) | 2026-04-09 | CalcVPFRLite implemented and wired into RunAnalysisAsync; VPFR-lite scoring active |
| v0.44 (MainForm) | 2026-04-09 | Fix CalcVPFRLite call: unpack ByRef params instead of passing IndicatorResults directly |
| v0.45 (MainForm) | 2026-04-09 | Rename OnHandleCreated → OnFormHandleCreated (suppress BC40003 shadow warning) |
| v0.45 (refactor) | 2026-04-09 | **Full partial-class refactor:** ScoringEngine.vb split into Core/ScoringEngine_Types/_Helpers/_Calculate; MainForm.vb split into UI/MainForm_Layout/_AutoRun/_Analysis/_Render; root monolithic files deleted; all partials compile clean |

---

## 14. Pending Observations / Calibration Watchlist

| Feature | Condition to observe | Status |
|---|---|---|
| CVD divergence penalty | See a live BEARISH or BULLISH divergence and confirm −1 penalty appears in scoring breakdown | WATCHING |
| Transitional ADX penalty | Run during a TRANSITIONAL regime and confirm tiered penalty applied correctly | WATCHING |
| Calibration log saturation | Accumulate 300+ rows across 3+ sessions to trigger READY FOR RECALIBRATION | WATCHING |
| MTF weak-15m pass | Review whether PASS should require stricter confirmation when 15m ADX is below trend-strength threshold | WATCHING |
| VPFR-lite signal | Observe live NEAR_HVN_SUPPORT / NEAR_HVN_RESIST / IN_LVN_BULL / IN_LVN_BEAR signals and confirm scoring and display correct | WATCHING |
