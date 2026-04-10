# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-04-10 | App version: Commit 4 (T1-B/T2-A/T2-B)**

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
│   │                                   VerdictResult (incl. AdjustedLongTarget,
│   │                                   AdjustedShortTarget, TargetCapReason),
│   │                                   PositionState, SignalCategory, ScoreState
│   ├── ScoringEngine_Helpers.vb        Pure functions: RegimeMaxScore, Threshold, TierFloor,
│   │                                   AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus
│   ├── ScoringEngine_Calculate.vb      Main Calculate() pipeline — assembles verdict;
│   │                                   includes VPFR HVN cap logic;
│   │                                   v0.47: P2 RSI divergence penalty, P4 Donchian
│   │                                   quartile partial upgrade, P5 volMid directional
│   │                                   partial, P6 OBV adverse divergence gate;
│   │                                   T2-A: MicroCVD FLAT stall penalty — when signal=FLAT
│   │                                   and price/CVD direction conflict, applies
│   │                                   DecelPenalty to opposing side with STALL breakdown note
│   │
│   ├── IndicatorResults.vb             IndicatorResults struct — all indicator output fields
│   ├── Indicators_Momentum.vb          CalcDMI, CalcATR, CalcEMA, CalcRSI,
│   │                                   CalcRSISeries, CalcRSIDivergence, CalcROCSeries,
│   │                                   CalcVolumeSMA
│   ├── Indicators_Volatility.vb        CalcVWAP (dual-session auto-anchor),
│   │                                   CalcVWAPBands, CalcBBW, CalcTTMSqueeze
│   ├── Indicators_OrderFlow.vb         CalcOFI (T2-B: Optional bookDepth param, dynamic
│   │                                   descending weights, replaces hardcoded Take(3)),
│   │                                   CalcCVD (v0.47: 3-segment weighted slope),
│   │                                   CalcMicroCVD, CalcTFI, CalcLiquidations
│   └── Indicators_Structure.vb         CalcDonchian, CalcOBV,
│                                       CalcVPFRLite (v0.47: exponential decay weighting),
│                                       CalcMTFGate
│
├── UI/
│   ├── MainForm_Layout.vb              Constants, DllImport/RECT, constructor (New()),
│   │                                   ResizeControls(), SetOutputMargins(),
│   │                                   OnFormHandleCreated(), CentreNudText();
│   │                                   shared fields: C_* colour palette, _oiHistory,
│   │                                   _autoRunTimer, _countdownTimer, CHAR_PLAY/STOP;
│   │                                   v0.47: MTF TTL cache fields — _mtfCandles15m,
│   │                                   _mtfLastFetchTime, MTF_TTL_SECONDS (const=60);
│   │                                   T1-B: _prevRegime As String = "" (regime hysteresis)
│   ├── MainForm_AutoRun.vb             Auto-run timer: InitAutoRunControls(),
│   │                                   btnStartStop_Click, StartAutoRun(), StopAutoRun(),
│   │                                   RunAutoAnalysis(), OnCountdownTick(),
│   │                                   UpdateCountdownLabel()
│   ├── MainForm_Analysis.vb            btnAnalyze_Click, RunAnalysisAsync() —
│   │                                   fetches data, calls all indicators + scoring engine,
│   │                                   logs result, calls RenderOutput;
│   │                                   v0.47: P1 MTF TTL refresh (15m candles cached,
│   │                                   re-fetched only when >60s stale);
│   │                                   P4 Donchian quartile signal set here;
│   │                                   T1-B: rawRegime computed, then grace period holds
│   │                                   _prevRegime when rawRegime=RANGE_BOUND and previous
│   │                                   was TRENDING_* or TRANSITIONAL; _prevRegime updated
│   │                                   at end of pipeline;
│   │                                   T2-B: CalcOFI call passes bookDepth:=cfg.Indicators.OFI.BookDepth
│   └── MainForm_Render.vb              RenderOutput(), AppendRtf(), AR(), SectionHeader(),
│                                       Divider(), BuildCalibrationReport(), Flag(),
│                                       UpdateLogInfo(), lnkResetLog_LinkClicked,
│                                       lnkCalibCheck_LinkClicked.
│                                       ATR block: shows HVN-capped target in amber bold
│                                       when v.AdjustedLongTarget or AdjustedShortTarget > 0;
│                                       raw target shown dimmed for reference.
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
        │  [P1 v0.47] MTF TTL check: if _mtfCandles15m is stale (> MTF_TTL_SECONDS),
        │  fetch 15m candles and update _mtfLastFetchTime. Otherwise reuse cached list.
        │
        ├──► DeribitClient.GetCandlesAsync("1", 250)      → List(Of Candle)  candles1m
        ├──► DeribitClient.GetCandlesAsync("5", 210)      → List(Of Candle)  candles5m
        ├──► DeribitClient.GetCandlesAsync("15", 70)      → List(Of Candle)  candles15m  [cached/TTL]
        ├──► DeribitClient.GetFundingRateAsync()          → Double           fundingRate
        ├──► DeribitClient.GetBookSummaryAsync()          → BookSummary      bookSummary
        ├──► DeribitClient.GetOrderBookAsync(10)          → OrderBook        orderBook
        └──► DeribitClient.GetRecentTradesAsync(100)      → List(Of Trade)   recentTrades
                    │  (all fetched in parallel via Task.WhenAll;
                    │   15m only included in WhenAll when cache is stale)
                    ▼
        IndicatorResults  r  (filled field by field)
        ├─ r.CurrentPrice       = candles1m.Last().Close
        ├─ r.ATR                = IndicatorEngine.CalcATR(candles1m, 7)
        ├─ DynamicNorms.Compute → norms (ATRScaleFactor, VolThresholds, VWAPDevThreshold)
        ├─ r.ROC / r.ROCSlope   = CalcROCSeries
        ├─ r.RSI                = CalcRSI
        ├─ r.RSIDivergence      = CalcRSIDivergence
        ├─ r.Volume* / Ratio    = CalcVolumeSMA
        ├─ r.ADX/PlusDI/MinusDI = CalcDMI(candles5m)  → rawRegime → r.Regime
        │                         [T1-B] rawRegime=RANGE_BOUND + _prevRegime=TRENDING/TRANSITIONAL
        │                         → hold _prevRegime for 1 bar; update _prevRegime at end of run
        ├─ r.VWAP / VWAPDevPct  = CalcVWAP  (dual-session: 00:00 UTC or 13:30 UTC)
        ├─ r.VWAPSigma1/2       = CalcVWAPBands
        ├─ r.BBW / SqueezeStatus = CalcBBW
        ├─ r.TTMHistogram/Dir   = CalcTTMSqueeze
        ├─ r.EMA9/21/50 + Align = CalcEMA(candles1m)
        ├─ r.EMA200_5m          = CalcEMA(candles5m, 200)
        ├─ r.FundingRate/Bias   = fundingRate
        ├─ r.OI_Current/Changes = bookSummary.OI + _oiHistory ring buffer
        ├─ r.OFI* / OFISignal   = CalcOFI(orderBook, bookDepth:=cfg.Indicators.OFI.BookDepth)
        │                         [T2-B] depth is now cfg-driven; weights are dynamic descending
        ├─ r.Liq* / LiqSignal   = CalcLiquidations(recentTrades)
        ├─ r.CVD* / CVDSlope    = CalcCVD(recentTrades, candles1m)   [v0.47: 3-seg weighted slope]
        ├─ r.MicroCVD*          = CalcMicroCVD(recentTrades)  [3-segment; BULL/BEAR_ACCEL/DECEL/FLAT]
        ├─ r.TFI* / TFISignal   = CalcTFI(recentTrades)  [BUY/SELL PRESSURE]
        ├─ r.MTFGatePass/Reason = CalcMTFGate(candles15m)   [cached; refreshed by TTL]
        ├─ r.DonchianSignal     = CalcDonchian + quartile logic  [v0.47: LONG/SHORT/LONG_PARTIAL/SHORT_PARTIAL]
        ├─ r.OBVTrend/Div       = CalcOBV
        └─ r.VPFR* / Signal     = CalcVPFRLite   [v0.47: exponential decay weighting]
                    │
                    ▼
        ScoringEngine.Calculate(r, posState, norms, cfg)
                    │
                    ├─ Step 1: Regime classification → MaxScore (19 TRENDING / 18 RANGE / 15 TRANSITIONAL)
                    ├─ Step 2: Score each signal → ScoreState (LongPts, ShortPts)
                    │         v0.47 additions:
                    │           P2: RSI divergence penalty (-1 long if BEARISH+RSI>65;
                    │               -1 short if BULLISH+RSI<35)
                    │           P5: volMidLong/volMidShort flags set here (direction-confirmed)
                    │         T2-A: MicroCVD FLAT stall penalty — FLAT + price>VWAP + CVD<=0
                    │               → penalise long; FLAT + price<VWAP + CVD>=0 → penalise short;
                    │               magnitude = cfg.Indicators.MicroCVD.DecelPenalty;
                    │               breakdown note annotated with STALL flag
                    ├─ Pass 2: Upgrade cross-category partials
                    │         v0.47 additions:
                    │           P4: Donchian LONG_PARTIAL/SHORT_PARTIAL quartile upgrade
                    │           P5: volMid directional upgrade via cross-category confirm
                    │           P6: OBV partial upgrade blocked when adverse divergence present
                    ├─ Step 3: Funding modifier
                    ├─ Step 4: Regime veto / TRANSITIONAL penalty
                    ├─ Step 4b: MTF gate veto → NO TRADE if blocked
                    ├─ Step 4c: VPFR HVN cap → sets AdjustedLongTarget / AdjustedShortTarget
                    └─ Step 5: Threshold comparison → Verdict + Confidence
                    │  Step 6: CalcHoldStatus
                    ▼
        VerdictResult  v
                    │
                    ├──► AnalysisLogger.LogRun(r, v)   → analysis_log.csv
                    └──► MainForm_Render.RenderOutput()  → txtOutput (RTF colour) + lblVerdict
                              └─ ATR block: if v.AdjustedLongTarget > 0 or v.AdjustedShortTarget > 0,
                                          show raw target dimmed + capped target in amber bold
```

---

## Partial Class Strategy

VB.NET `Partial Class` allows a single class to be split across multiple `.vb` files.
All partial files compile into the **same class** — they share fields, methods, and event
wiring. `Imports` statements are **not** shared; each partial file must declare its own.

### MainForm partials

| File | Owns | Depends on |
|---|---|---|
| `MainForm_Layout.vb` | All shared fields (`C_*`, `_oiHistory`, `_autoRunTimer`, `_mtfCandles15m`, `_mtfLastFetchTime`, MTF_TTL_SECONDS const, `_prevRegime`), constructor, layout/resize | `System.Drawing`, `System.Runtime.InteropServices`, `System.Windows.Forms` |
| `MainForm_AutoRun.vb` | Auto-run timer lifecycle | `System.Drawing` (Color.FromArgb), `System.Threading`, `System.Windows.Forms` |
| `MainForm_Analysis.vb` | Full analysis pipeline; MTF TTL cache check and conditional fetch; Donchian quartile signal; T1-B regime hysteresis; T2-B OFI BookDepth wiring | `System.Drawing` (Color.Gray/OrangeRed), `System.Windows.Forms` |
| `MainForm_Render.vb` | All output rendering and log helpers; ATR HVN cap display | `System.Drawing`, `System.IO`, `System.Windows.Forms` |

### ScoringEngine partials

| File | Owns |
|---|---|
| `ScoringEngine_Types.vb` | All enums and result/state types (incl. VerdictResult HVN cap fields) |
| `ScoringEngine_Helpers.vb` | All pure helper functions; no UI dependency |
| `ScoringEngine_Calculate.vb` | MaxScore const + the full Calculate() method incl. HVN cap logic, P2/P4/P5/P6 scoring changes, T2-A MicroCVD FLAT stall penalty |

### IndicatorEngine partials

| File | Owns |
|---|---|
| `IndicatorResults.vb` | IndicatorResults struct — all output fields |
| `Indicators_Momentum.vb` | DMI, ATR, EMA, RSI (incl. series + divergence), ROC, VolumeSMA |
| `Indicators_Volatility.vb` | VWAP (dual-session), VWAPBands, BBW, TTMSqueeze |
| `Indicators_OrderFlow.vb` | OFI (T2-B: bookDepth param + dynamic weights), CVD (3-segment weighted slope), MicroCVD, TFI, Liquidations |
| `Indicators_Structure.vb` | Donchian, OBV, VPFRLite (exponential decay), MTFGate |

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| ATR Entry = candle close, not last trade price | Candle close is stable and reproducible; last trade price can be stale or noisy. Last trade is shown separately for reference. |
| All thresholds in settings.json | Zero-recompile tuning during live calibration sessions. |
| DynamicNorms computed per run | Adapts to current volatility regime automatically. |
| OI ring buffer in MainForm | OI history must persist across runs within the same session; CSV log does not store it. |
| MTF gate is veto-only | Prevents score inflation from a weak gate — it can only block, never add points. |
| VPFR HVN cap in VerdictResult | Keeps cap logic inside the scoring engine (not render); render is purely presentational. Cap fields are zero/empty when no wall is detected so existing render paths are untouched. |
| Partial class split | Keeps individual files under ~200 lines; each file has a single clear responsibility; avoids merge conflicts when multiple features are developed in parallel. |
| MTF 15m candle TTL cache (v0.47) | 15m candles change slowly; re-fetching every 1m auto-run cycle wastes a Deribit API call and adds ~80ms latency. Cache is reused when younger than MTF_TTL_SECONDS (60). Invalidated automatically on next cycle past TTL. Fields live in Layout.vb (shared state) and are read/written only in MainForm_Analysis.vb. |
| CVD 3-segment weighted slope (v0.47) | Half-split (count/2) was vulnerable to a single large trade early in the window flipping the slope. Weighting late segment 2x (formula: lateDelta*2 - earlyDelta*1) anchors slope signal to recency without discarding early context. |
| Donchian quartile partial signal (v0.47) | Pure breakout-only fired ~0% of the time on 1m charts (price rarely closes exactly at the channel edge). Upper/lower 25% quartile gives a meaningful partial signal that fires ~15-20% of bars while still requiring cross-category confirmation before scoring. |
| RSI divergence penalty gated at RSI>65 / <35 (v0.47) | Applying the penalty only in overbought/oversold territory avoids false negatives in mid-range; divergence near RSI 50 is noise. |
| OBV partial upgrade divergence gate (v0.47) | Upgrading OBV trend when an adverse divergence is present contradicts a known negative signal. The gate blocks the upgrade but does not add a penalty — it simply withholds a point rather than double-counting. |
| VPFR exponential decay (v0.47) | Uniform weighting anchors POC to early-session high-volume events that are no longer relevant intraday. Decay base 0.985 gives ~22% weight reduction per 15 bars, making POC track current structure without being reactive to single-bar noise. |
| Regime ADX hysteresis — 1-bar grace (T1-B) | 1m scalping produces whipsaw TRENDING→RANGING→TRENDING flips during momentum consolidations. A single-bar grace period holds the previous regime when a raw RANGE_BOUND reading follows a TRENDING or TRANSITIONAL regime, absorbing single-candle ADX dips without changing the veto/penalty path. _prevRegime field in Layout.vb; grace logic in MainForm_Analysis.vb. |
| MicroCVD FLAT stall penalty (T2-A) | FLAT during a trending session (price above VWAP but CVD non-positive, or below VWAP but CVD non-negative) indicates momentum stall rather than neutrality. Reuses DecelPenalty magnitude to avoid a proliferation of config fields; STALL annotated in breakdown so the operator can distinguish it from a genuine DECEL signal. |
| OFI BookDepth configurable (T2-B) | Hardcoded Take(3) with weights {3,2,1} was insensitive to order book depth configuration. Dynamic descending weight array built at runtime from cfg.Indicators.OFI.BookDepth allows widening to 5-10 levels on deep books without code changes. Default remains 3. |
