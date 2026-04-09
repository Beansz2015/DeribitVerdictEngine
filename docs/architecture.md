# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-04-09 | App version: v0.45**

This document describes the full codebase structure, data flow, and responsibility of every file.
It is generated from the actual state of the repository and should be updated whenever files are
added, moved, or significantly changed.

---

## Directory Layout

```
DeribitVerdictEngine/
├── Program.vb                          Entry point — Application.Run(New MainForm)
├── MainForm.Designer.vb                Auto-generated WinForms designer (do not edit)
├── MainForm.resx                       Form resource file
│
├── DeribitClient.vb                    REST API layer — all Deribit HTTP calls
├── DynamicNorms.vb                     Live adaptive thresholds (ATR scale, vol, VWAP dev)
├── AnalysisLogger.vb                   CSV run logger + CalibrationReport
├── AutoRunTimer.vb                     IAutoRunTimer interface + WinFormsAutoRunTimer impl
├── OiSnapshot.vb                       OI ring-buffer snapshot struct
├── SettingsLoader.vb                   JSON loader — SettingsLoader.Current singleton
├── settings.json                       All tunable parameters (no recompile needed)
│
├── Core/
│   ├── Settings/
│   │   └── EngineSettings.vb           Strongly-typed POCO for settings.json (v0.33)
│   │
│   ├── ScoringEngine_Types.vb          Enums + result types: SignalBreakdownItem,
│   │                                   VerdictResult, PositionState, SignalCategory, ScoreState
│   ├── ScoringEngine_Helpers.vb        Pure functions: RegimeMaxScore, Threshold, TierFloor,
│   │                                   AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus
│   ├── ScoringEngine_Calculate.vb      Main Calculate() pipeline — assembles verdict
│   │
│   └── Indicators/
│       ├── IndicatorEngine_Types.vb    IndicatorResults, Candle, BookEntry, TradeEntry
│       ├── IndicatorEngine_Core.vb     CalcATR, CalcRSI, CalcROCSeries, CalcEMA,
│       │                               CalcVolumeSMA, CalcDMI
│       ├── IndicatorEngine_VWAP.vb     CalcVWAP, CalcVWAPBands
│       ├── IndicatorEngine_OrderFlow.vb CalcOFI, CalcCVD, CalcLiquidations
│       └── IndicatorEngine_Structure.vb CalcDonchian, CalcOBV, CalcRSIDivergence,
│                                        CalcBBW, CalcTTMSqueeze, CalcMTFGate, CalcVPFRLite
│
├── UI/
│   ├── MainForm_Layout.vb              Constants, DllImport/RECT, constructor (New()),
│   │                                   ResizeControls(), SetOutputMargins(),
│   │                                   OnFormHandleCreated(), CentreNudText();
│   │                                   shared fields: C_* colour palette, _oiHistory,
│   │                                   _autoRunTimer, _countdownTimer, CHAR_PLAY/STOP
│   ├── MainForm_AutoRun.vb             Auto-run timer: InitAutoRunControls(),
│   │                                   btnStartStop_Click, StartAutoRun(), StopAutoRun(),
│   │                                   RunAutoAnalysis(), OnCountdownTick(),
│   │                                   UpdateCountdownLabel()
│   ├── MainForm_Analysis.vb            btnAnalyze_Click, RunAnalysisAsync() —
│   │                                   fetches data, calls all indicators + scoring engine,
│   │                                   logs result, calls RenderOutput
│   └── MainForm_Render.vb              RenderOutput(), AppendRtf(), AR(), SectionHeader(),
│                                       Divider(), BuildCalibrationReport(), Flag(),
│                                       UpdateLogInfo(), lnkResetLog_LinkClicked,
│                                       lnkCalibCheck_LinkClicked
│
└── docs/
    ├── DeribitIndicatorProject.md      Authoritative handover document (read first)
    ├── architecture.md                 This file
    ├── trader-profile.md               User trading profile
    ├── bbw-scoring-proposal.md         Historical
    ├── bbw-scoring-response.md         Historical
    ├── dual-scoring-fix-proposal.md    Historical
    └── dual-scoring-fix-response.md    Historical
```

---

## Data Flow — Single Analysis Run

```
[User clicks Analyze Now]
        │
        ▼
MainForm_Analysis.vb :: RunAnalysisAsync()
        │
        ├──► DeribitClient.GetCandlesAsync("1", 250)      → List(Of Candle)  candles1m
        ├──► DeribitClient.GetCandlesAsync("5", 210)      → List(Of Candle)  candles5m
        ├──► DeribitClient.GetCandlesAsync("15", 70)      → List(Of Candle)  candles15m
        ├──► DeribitClient.GetFundingRateAsync()          → Double           fundingRate
        ├──► DeribitClient.GetBookSummaryAsync()          → BookSummary      bookSummary
        ├──► DeribitClient.GetOrderBookAsync(10)          → OrderBook        orderBook
        └──► DeribitClient.GetRecentTradesAsync(100)      → List(Of Trade)   recentTrades
                    │  (all fetched in parallel via Task.WhenAll)
                    ▼
        IndicatorResults  r  (filled field by field)
        ├─ r.CurrentPrice       = candles1m.Last().Close
        ├─ r.ATR                = IndicatorEngine.CalcATR(candles1m, 7)
        ├─ r.ATRAvg20d          = CalcATR(candles5m, 60) * √5
        ├─ DynamicNorms.Compute → norms (ATRScaleFactor, VolThresholds, VWAPDevThreshold)
        ├─ r.ROC / r.ROCSlope   = CalcROCSeries
        ├─ r.RSI                = CalcRSI
        ├─ r.Volume* / Ratio    = CalcVolumeSMA
        ├─ r.ADX/PlusDI/MinusDI = CalcDMI(candles5m)  → r.Regime
        ├─ r.VWAP / VWAPDevPct  = CalcVWAP
        ├─ r.VWAPSigma1/2       = CalcVWAPBands
        ├─ r.BBW / SqueezeStatus = CalcBBW
        ├─ r.TTMHistogram/Dir   = CalcTTMSqueeze
        ├─ r.EMA9/21/50 + Align = CalcEMA(candles1m)
        ├─ r.EMA200_5m          = CalcEMA(candles5m, 200)
        ├─ r.FundingRate/Bias   = fundingRate
        ├─ r.OI_Current/Changes = bookSummary.OI + _oiHistory ring buffer
        ├─ r.OFI* / OFISignal   = CalcOFI(orderBook)
        ├─ r.Liq* / LiqSignal   = CalcLiquidations(recentTrades)
        ├─ r.CVD* / CVDSlope    = CalcCVD(recentTrades, candles1m)
        ├─ r.MTFGatePass/Reason = CalcMTFGate(candles15m)
        ├─ r.Donchian* / Signal = CalcDonchian
        ├─ r.OBVTrend/Div       = CalcOBV
        ├─ r.RSIDivergence       = CalcRSIDivergence
        └─ r.VPFR* / Signal     = CalcVPFRLite
                    │
                    ▼
        ScoringEngine.Calculate(r, posState, norms, cfg)
                    │
                    ├─ Step 1: Regime classification → MaxScore
                    ├─ Step 2: Score each signal → ScoreState (LongPts, ShortPts)
                    ├─ Pass 2: Upgrade cross-category partials
                    ├─ Step 3: Funding modifier
                    ├─ Step 4: Regime veto / TRANSITIONAL penalty
                    ├─ Step 4b: MTF gate veto → NO TRADE if blocked
                    ├─ Step 5: Threshold comparison → Verdict + Confidence
                    └─ Step 6: CalcHoldStatus
                    │
                    ▼
        VerdictResult  v
                    │
                    ├──► AnalysisLogger.LogRun(r, v)   → analysis_log.csv
                    └──► MainForm_Render.RenderOutput()  → txtOutput (RTF colour) + lblVerdict
```

---

## Partial Class Strategy

VB.NET `Partial Class` allows a single class to be split across multiple `.vb` files.
All partial files compile into the **same class** — they share fields, methods, and event
wiring. `Imports` statements are **not** shared; each partial file must declare its own.

### MainForm partials

| File | Owns | Depends on |
|---|---|---|
| `MainForm_Layout.vb` | All shared fields (`C_*`, `_oiHistory`, `_autoRunTimer`, etc.), constructor, layout/resize | `System.Drawing`, `System.Runtime.InteropServices`, `System.Windows.Forms` |
| `MainForm_AutoRun.vb` | Auto-run timer lifecycle | `System.Drawing` (Color.FromArgb), `System.Threading`, `System.Windows.Forms` |
| `MainForm_Analysis.vb` | Full analysis pipeline | `System.Drawing` (Color.Gray/OrangeRed), `System.Windows.Forms` |
| `MainForm_Render.vb` | All output rendering and log helpers | `System.Drawing`, `System.IO`, `System.Windows.Forms` |

### ScoringEngine partials

| File | Owns |
|---|---|
| `ScoringEngine_Types.vb` | All enums and result/state types |
| `ScoringEngine_Helpers.vb` | All pure helper functions; no UI dependency |
| `ScoringEngine_Calculate.vb` | MaxScore const + the full Calculate() method |

### IndicatorEngine partials

| File | Owns |
|---|---|
| `IndicatorEngine_Types.vb` | IndicatorResults and all data transfer types |
| `IndicatorEngine_Core.vb` | Price/volume/momentum indicators |
| `IndicatorEngine_VWAP.vb` | All VWAP variants |
| `IndicatorEngine_OrderFlow.vb` | OFI, CVD, Liquidations |
| `IndicatorEngine_Structure.vb` | Market structure, squeeze, MTF gate, VPFR |

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| ATR Entry = candle close, not last trade price | Candle close is stable and reproducible; last trade price can be stale or noisy. Last trade is shown separately for reference. |
| All thresholds in settings.json | Zero-recompile tuning during live calibration sessions. |
| DynamicNorms computed per run | Adapts to current volatility regime automatically. |
| OI ring buffer in MainForm | OI history must persist across runs within the same session; CSV log does not store it. |
| MTF gate is veto-only | Prevents score inflation from a weak gate — it can only block, never add points. |
| Partial class split | Keeps individual files under ~200 lines; each file has a single clear responsibility; avoids merge conflicts when multiple features are developed in parallel. |
