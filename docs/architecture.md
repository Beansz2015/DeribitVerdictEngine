# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-04-11 | App version: Commit 5**

This document describes the full codebase structure, data flow, and design rationale.
Update whenever files are added, moved, or significantly changed.

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
│   │   └── EngineSettings.vb           Strongly-typed POCO for settings.json (v0.37)
│   │
│   ├── ScoringEngine_Types.vb          Enums + result types: SignalBreakdownItem,
│   │                                   VerdictResult (incl. AdjustedLongTarget,
│   │                                   AdjustedShortTarget, TargetCapReason),
│   │                                   PositionState, SignalCategory, ScoreState
│   ├── ScoringEngine_Helpers.vb        Pure functions: RegimeMaxScore, Threshold, TierFloor,
│   │                                   AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus
│   ├── ScoringEngine_Calculate.vb      Main Calculate() pipeline — assembles verdict;
│   │                                   VPFR HVN cap logic; all scoring steps (see Data Flow)
│   │
│   ├── IndicatorResults.vb             IndicatorResults struct — all indicator output fields
│   ├── Indicators_Momentum.vb          CalcDMI, CalcATR, CalcEMA, CalcRSI,
│   │                                   CalcRSISeries, CalcRSIDivergence, CalcROCSeries,
│   │                                   CalcVolumeSMA
│   ├── Indicators_Volatility.vb        CalcVWAP (dual-session auto-anchor),
│   │                                   CalcVWAPBands, CalcBBW, CalcTTMSqueeze
│   ├── Indicators_OrderFlow.vb         CalcOFI (bookDepth param, dynamic descending weights),
│   │                                   CalcCVD (3-segment weighted slope),
│   │                                   CalcMicroCVD, CalcTFI, CalcLiquidations
│   └── Indicators_Structure.vb         CalcDonchian, CalcOBV,
│                                       CalcVPFRLite (exponential decay weighting),
│                                       CalcMTFGate
│
├── UI/
│   ├── MainForm_Layout.vb              Constants, DllImport/RECT, constructor (New()),
│   │                                   ResizeControls(), SetOutputMargins(),
│   │                                   OnFormHandleCreated(), CentreNudText();
│   │                                   shared fields: C_* colour palette, _oiHistory,
│   │                                   _autoRunTimer, _countdownTimer, CHAR_PLAY/STOP;
│   │                                   MTF TTL: _mtfCandles15m, _mtfLastFetchTime,
│   │                                   MTF_TTL_SECONDS (const=60);
│   │                                   _prevRegime (regime hysteresis)
│   ├── MainForm_AutoRun.vb             Auto-run timer: InitAutoRunControls(),
│   │                                   btnStartStop_Click, StartAutoRun(), StopAutoRun(),
│   │                                   RunAutoAnalysis(), OnCountdownTick(),
│   │                                   UpdateCountdownLabel()
│   ├── MainForm_Analysis.vb            btnAnalyze_Click, RunAnalysisAsync() —
│   │                                   fetches data, calls all indicators + scoring engine,
│   │                                   logs result, calls RenderOutput;
│   │                                   MTF TTL refresh; Donchian quartile signal;
│   │                                   regime hysteresis logic; OFI BookDepth wiring
│   └── MainForm_Render.vb              RenderOutput(), AppendRtf(), AR(), SectionHeader(),
│                                       Divider(), BuildCalibrationReport(), Flag(),
│                                       UpdateLogInfo(), lnkResetLog_LinkClicked,
│                                       lnkCalibCheck_LinkClicked.
│                                       ATR block: HVN-capped target in amber bold
│                                       when AdjustedLongTarget or AdjustedShortTarget > 0
│
└── docs/
    ├── DeribitIndicatorProject.md      Authoritative handover document (read first)
    ├── architecture.md                 This file
    ├── trader-profile.md               Trader style, preferences, collaboration rules
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
        │  MTF TTL check: if _mtfCandles15m is stale (> MTF_TTL_SECONDS),
        │  fetch 15m candles and update _mtfLastFetchTime. Otherwise reuse cached list.
        │
        ├──► DeribitClient.GetCandlesAsync("1", 250)      → candles1m
        ├──► DeribitClient.GetCandlesAsync("5", 210)      → candles5m
        ├──► DeribitClient.GetCandlesAsync("15", 70)      → candles15m  [cached/TTL]
        ├──► DeribitClient.GetFundingRateAsync()          → fundingRate
        ├──► DeribitClient.GetBookSummaryAsync()          → bookSummary
        ├──► DeribitClient.GetOrderBookAsync(10)          → orderBook
        └──► DeribitClient.GetRecentTradesAsync(100)      → recentTrades
                    │  (all fetched in parallel via Task.WhenAll;
                    │   15m only included when cache is stale)
                    ▼
        IndicatorResults  r  (filled field by field)
        ├─ r.CurrentPrice       = candles1m.Last().Close
        ├─ r.ATR                = CalcATR(candles1m, 7)
        ├─ DynamicNorms.Compute → norms (ATRScaleFactor, VolThresholds, VWAPDevThreshold)
        ├─ r.ROC / r.ROCSlope   = CalcROCSeries
        ├─ r.RSI                = CalcRSI
        ├─ r.RSIDivergence      = CalcRSIDivergence (pivotWing + lookbackBars from cfg)
        ├─ r.Volume* / Ratio    = CalcVolumeSMA
        ├─ r.ADX/PlusDI/MinusDI = CalcDMI(candles5m) → rawRegime → r.Regime
        │                         [T1-B] RANGE_BOUND following TRENDING/TRANSITIONAL
        │                         → hold _prevRegime for 1 bar; update at end of run
        ├─ r.VWAP / VWAPDevPct  = CalcVWAP (dual-session: 00:00 UTC or 13:30 UTC)
        ├─ r.VWAPSigma1/2       = CalcVWAPBands
        ├─ r.BBW / SqueezeStatus = CalcBBW
        ├─ r.TTMHistogram/Dir   = CalcTTMSqueeze (flatThreshold from cfg)
        ├─ r.EMA9/21/50 + Align = CalcEMA(candles1m)
        ├─ r.EMA200_5m          = CalcEMA(candles5m, 200)
        ├─ r.FundingRate/Bias   = fundingRate
        ├─ r.OI_Current/Changes = bookSummary.OI + _oiHistory ring buffer
        ├─ r.OFI* / OFISignal   = CalcOFI(orderBook, bookDepth:=cfg.Indicators.OFI.BookDepth)
        ├─ r.Liq* / LiqSignal   = CalcLiquidations(recentTrades, dominanceRatio from cfg)
        ├─ r.CVD* / CVDSlope    = CalcCVD(recentTrades, candles1m)  [3-seg weighted slope]
        ├─ r.MicroCVD*          = CalcMicroCVD(recentTrades)  [BULL/BEAR_ACCEL/DECEL/FLAT]
        ├─ r.TFI* / TFISignal   = CalcTFI(recentTrades)
        ├─ r.MTFGatePass/Reason = CalcMTFGate(candles15m)  [cached; refreshed by TTL]
        ├─ r.DonchianSignal     = CalcDonchian + quartile logic
        ├─ r.OBVTrend/Div       = CalcOBV
        └─ r.VPFR* / Signal     = CalcVPFRLite (exp decay; numBuckets from cfg)
                    │
                    ▼
        ScoringEngine.Calculate(r, posState, norms, cfg)
                    │
                    ├─ Step 1: Regime classification → MaxScore (19/18/15)
                    ├─ Step 2: Score each signal → ScoreState
                    │         All thresholds from cfg. Scoring highlights:
                    │           RSI full/partial zones + divergence penalty (−1 at RSI>65/σ5)
                    │           ADX trend gate; VWAP warmup guard
                    │           ROC partial dead-band; OFI dominance thresholds
                    │           BBW squeeze penalty; Liq standard/large penalty
                    │           Funding step deltas
                    │           MicroCVD FLAT stall penalty (T2-A): FLAT + price/CVD
                    │           contradiction → DecelPenalty on opposing side + STALL note
                    │           Donchian NONE → mid-channel note in breakdown (T2-C)
                    ├─ Pass 2: Upgrade cross-category partials
                    │           Donchian LONG_PARTIAL/SHORT_PARTIAL quartile upgrade
                    │           volMid directional upgrade via cross-category confirm
                    │           OBV upgrade blocked when adverse divergence present
                    ├─ Step 3: Funding modifier
                    ├─ Step 4: Regime veto / TRANSITIONAL ADX penalty
                    ├─ Step 4b: MTF gate veto → NO TRADE if blocked
                    ├─ Step 4c: VPFR HVN cap → AdjustedLongTarget / AdjustedShortTarget
                    ├─ Step 5: Threshold comparison → Verdict + Confidence
                    ├─ Step 6: CalcHoldStatus
                    └─ Step 7: ATR target/stop
                    │
                    ▼
        VerdictResult  v
                    │
                    ├──► AnalysisLogger.LogRun(r, v)   → analysis_log.csv
                    └──► MainForm_Render.RenderOutput() → txtOutput (RTF) + lblVerdict
                              └─ if v.AdjustedLongTarget > 0 or v.AdjustedShortTarget > 0:
                                         raw target dimmed + capped target in amber bold
```

---

## Partial Class Strategy

VB.NET `Partial Class` splits a single class across multiple `.vb` files. All partials compile
into the same class — shared fields, methods, and event wiring. `Imports` are **not** shared;
each file must declare its own.

### MainForm partials

| File | Owns | Depends on |
|---|---|---|
| `MainForm_Layout.vb` | All shared fields, constructor, layout/resize | `System.Drawing`, `System.Runtime.InteropServices`, `System.Windows.Forms` |
| `MainForm_AutoRun.vb` | Auto-run timer lifecycle | `System.Drawing`, `System.Threading`, `System.Windows.Forms` |
| `MainForm_Analysis.vb` | Full analysis pipeline; MTF TTL; Donchian quartile; regime hysteresis; OFI BookDepth | `System.Drawing`, `System.Windows.Forms` |
| `MainForm_Render.vb` | All output rendering and log helpers; ATR HVN cap display | `System.Drawing`, `System.IO`, `System.Windows.Forms` |

### ScoringEngine partials

| File | Owns |
|---|---|
| `ScoringEngine_Types.vb` | All enums and result/state types |
| `ScoringEngine_Helpers.vb` | All pure helper functions; no UI dependency |
| `ScoringEngine_Calculate.vb` | MaxScore const + full Calculate() method |

### IndicatorEngine partials

| File | Owns |
|---|---|
| `IndicatorResults.vb` | IndicatorResults struct — all output fields |
| `Indicators_Momentum.vb` | DMI, ATR, EMA, RSI (series + divergence), ROC, VolumeSMA |
| `Indicators_Volatility.vb` | VWAP (dual-session), VWAPBands, BBW, TTMSqueeze |
| `Indicators_OrderFlow.vb` | OFI (bookDepth + dynamic weights), CVD (3-seg slope), MicroCVD, TFI, Liquidations |
| `Indicators_Structure.vb` | Donchian, OBV, VPFRLite (exp decay), MTFGate |

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| ATR entry = candle close, not last trade price | Candle close is stable and reproducible; last trade can be stale or noisy. Last trade shown separately for reference. |
| All thresholds in settings.json | Zero-recompile tuning during live calibration sessions. |
| DynamicNorms computed per run | Adapts to current volatility regime automatically. |
| OI ring buffer in MainForm | OI history must persist across runs within a session; CSV log does not store it. |
| MTF gate is veto-only | Prevents score inflation — can only block, never add points. |
| VPFR HVN cap in VerdictResult | Cap logic inside scoring engine, not render. Cap fields are zero/empty when no wall detected. |
| Partial class split | Keeps files under ~200 lines; single responsibility per file; avoids merge conflicts on parallel features. |
| MTF 15m candle TTL cache | 15m candles change slowly; re-fetching every 1m auto-run wastes ~80ms. Cache reused when < MTF_TTL_SECONDS (60). Fields in Layout.vb; read/written only in Analysis.vb. |
| CVD 3-segment weighted slope | Half-split was vulnerable to a single large early trade flipping the slope. Late segment ×2 (lateDelta*2 − earlyDelta*1) anchors slope to recency without discarding early context. |
| Donchian quartile partial signal | Pure breakout-only fired ~0% of the time on 1m (price rarely closes exactly at channel edge). Upper/lower 25% quartile fires ~15–20% of bars while still requiring cross-category confirmation. |
| RSI divergence penalty gated at RSI>65 / <35 | Penalty only in overbought/oversold territory; divergence near RSI 50 is noise. |
| OBV partial upgrade divergence gate | Upgrading OBV trend on adverse divergence contradicts a known negative signal. Gate withholds the point rather than double-counting. |
| VPFR exponential decay | Uniform weighting anchors POC to early-session events no longer relevant intraday. Decay base 0.985 gives ~22% weight reduction per 15 bars. |
| Regime ADX hysteresis — 1-bar grace | 1m scalping produces whipsaw TRENDING→RANGING flips during consolidations. Single-bar grace absorbs ADX dips without triggering regime veto path. _prevRegime in Layout.vb; grace logic in Analysis.vb. |
| MicroCVD FLAT stall penalty | FLAT during trending session (price above VWAP, CVD non-positive) indicates stall, not neutrality. Reuses DecelPenalty to avoid new config field; STALL annotated in breakdown. |
| OFI BookDepth configurable | Hardcoded Take(3) with static weights was insensitive to book depth. Dynamic descending weight array from cfg.Indicators.OFI.BookDepth allows widening to 5–10 levels without code changes. |
