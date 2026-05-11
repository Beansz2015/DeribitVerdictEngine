# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-05-11 | App version: settings.json v25 — output-dump feature on top of Bundle 3 (d1 trend structure + d2 volume-weighted pivots), Bundle 1 (csv-expansion-v0.4 + analysis script), and Bundle 2 (auto-tweaker)**

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
├── AnalysisOutputDump.vb               Append-only markdown dump helper (host-agnostic);
│                                       Append(), Clear(), CountRuns(), TrimToMaxRuns();
│                                       rolling-trim after each write; never throws.
├── DeribitClient.vb                    REST API layer — all Deribit HTTP calls;
│                                       ExecuteWithRetry wrapper: retry-once on 5xx/timeout,
│                                       return Nothing on hard failure. GetFundingRateAsync →
│                                       Double?; GetBookSummaryAsync → nullable value tuple.
├── DynamicNorms.vb                     Live adaptive thresholds (ATR scale, vol, VWAP dev);
│                                       now also applies session-aware volume multipliers
├── AnalysisLogger.vb                   CSV run logger + CalibrationReport
├── AutoRunTimer.vb                     IAutoRunTimer interface + WinFormsAutoRunTimer impl
├── OiSnapshot.vb                       OI ring-buffer snapshot struct
├── settings.json                       All tunable parameters v25 (no recompile needed)
│
├── Core/
│   ├── Settings/
│   │   ├── EngineSettings.vb           Strongly-typed POCO for settings.json
│   │   │                               Includes KellySettings + FundingSettings +
│   │   │                               OiCvdSettings + SessionVolumeSettings +
│   │   │                               RegimeWeightSettings + SwingSettings +
│   │   │                               RegimeMaxScoreSettings + TierFloorSettings +
│   │   │                               ContextTagThresholds blocks
│   │   └── SettingsLoader.vb           JSON loader — SettingsLoader.Current singleton;
│   │                                   FileSystemWatcher hot-reload
│   │
│   ├── ScoringEngine_Types.vb          Enums + result types: SignalBreakdownItem,
│   │                                   VerdictResult (incl. AdjustedLongTarget,
│   │                                   AdjustedShortTarget, TargetCapReason,
│   │                                   VerdictContext, Kelly fields),
│   │                                   PositionState, SignalCategory, ScoreState
│   ├── ScoringEngine_Helpers.vb        Pure functions: RegimeMaxScore (reads cfg
│   │                                   scoring.regime_max_score), Threshold,
│   │                                   TierFloor (reads cfg scoring.tier_floor),
│   │                                   AddFull, HasCrossConfirm, BuildNote,
│   │                                   CalcHoldStatus (Layer 1 microstructure,
│   │                                   Layer 1.5 structural-break exit,
│   │                                   Layer 2 OBV div, Layer 3 RSI/ROC)
│   ├── ScoringEngine_Calculate_Scoring.vb
│   │                                   AppendLean(), CalcVerdictContext()
│   │                                   (incl. swing structural-target check);
│   │                                   RunScoringPipeline() — Steps 2/Pass 2/
│   │                                   Pass 2b/Pass 2c/3/3b: signal scoring,
│   │                                   partial upgrades, OI×CVD cross-confirm,
│   │                                   regime alignment, funding modifiers,
│   │                                   all breakdown note rows.
│   ├── ScoringEngine_Calculate_Verdict.vb
│   │                                   Calculate() entry point — assembles verdict;
│   │                                   Step 4: regime veto / TRANSITIONAL ADX penalty;
│   │                                   Step 4b: MTF gate veto;
│   │                                   Step 5: threshold comparison → verdict string;
│   │                                   Step 5b: 3-tier target cap (swing target →
│   │                                   nearest HVN → POC) + VerdictContext tag.
│   ├── ScoringEngine_Kelly.vb          CalcKellySizing() — display-only Kelly Criterion
│   │                                   sizing. Called from MainForm_Render after ATR levels,
│   │                                   not from ScoringEngine.Calculate(). Zero scoring impact.
│   │
│   ├── IndicatorResults.vb             IndicatorResults struct — all indicator output fields
│   │                                   incl. FundingMomentum, SpreadBps, OFIMomentum,
│   │                                   VPFR-v2 fields (VPFRNearestHvnAbove/Below,
│   │                                   VPFRVAH, VPFRVAL), swing pivot fields
│   │                                   (LastSwingHigh/Low5m/15m, SwingTargetLong/Short,
│   │                                   SwingStopLong/Short)
│   ├── Indicators_Momentum.vb          CalcDMI, CalcATR, CalcEMA, CalcRSI,
│   │                                   CalcRSISeries, CalcRSIDivergence, CalcROCSeries,
│   │                                   CalcVolumeSMA
│   ├── Indicators_Volatility.vb        CalcVWAP (dual-session auto-anchor),
│   │                                   CalcVWAPBands,
│   │                                   CalcBBW (seriesWindowMultiplier + squeezePercentile
│   │                                   Optional params, wired from cfg in v17),
│   │                                   CalcTTMSqueeze (smaPeriod + linRegPeriod Optional
│   │                                   params, wired from cfg in v17)
│   ├── Indicators_OrderFlow.vb         CalcOFI (bookDepth param, dynamic descending weights),
│   │                                   CalcOFIMomentum (RISING/FALLING/FLAT),
│   │                                   CalcCVD (lateSegmentWeight + earlySegmentWeight from
│   │                                   cfg), CalcMicroCVD (dynamic accelThreshold),
│   │                                   CalcTFI, CalcLiquidations, CalcFundingMomentum
│   └── Indicators_Structure.vb         CalcDonchian (quartilePct from cfg),
│                                       CalcOBV,
│                                       CalcVPFRLite v2 (VAH/VAL + nearest HVN/LVN,
│                                       exponential decay weighting),
│                                       CalcSwingPivots (5m + 15m confirmed pivot scan),
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
│   │                                   _prevRegime (regime hysteresis);
│   │                                   _fundingHistory (List(Of Double), FundingHistoryMax=10);
│   │                                   _ofiHistory (List(Of Double), OFIHistoryMax=10)
│   ├── MainForm_AutoRun.vb             Auto-run timer: InitAutoRunControls(),
│   │                                   btnStartStop_Click, StartAutoRun(), StopAutoRun(),
│   │                                   RunAutoAnalysis(), OnCountdownTick(),
│   │                                   UpdateCountdownLabel()
│   ├── MainForm_Analysis.vb            btnAnalyze_Click, RunAnalysisAsync() —
│   │                                   fetches data, calls all indicators + scoring engine,
│   │                                   logs result, calls RenderOutput;
│   │                                   MTF TTL refresh; Donchian quartile signal;
│   │                                   regime hysteresis logic; OFI BookDepth wiring;
│   │                                   appends fundingRate to _fundingHistory;
│   │                                   calls CalcFundingMomentum → r.FundingMomentum;
│   │                                   appends OFI to _ofiHistory;
│   │                                   calls CalcOFIMomentum → r.OFIMomentum;
│   │                                   computes SpreadBps from order book;
│   │                                   calls CalcSwingPivots (5m + 15m);
│   │                                   computes SwingTarget/Stop bookkeeping
│   ├── MainForm_Render_Header.vb       RTF helpers: AppendRtf(), AR(), SectionHeader(),
│   │                                   Divider();
│   │                                   log/calibration helpers: UpdateLogInfo(),
│   │                                   BuildCalibrationReport(), Flag(),
│   │                                   lnkResetLog_LinkClicked, lnkCalibCheck_LinkClicked;
│   │                                   RenderOutputHeader() — top render block:
│   │                                   VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME /
│   │                                   LAST TRANSACTED PRICE / HOLD STATUS /
│   │                                   ATR ENTRY LEVELS /
│   │                                   LONG+SHORT STRUCTURAL ROWS (swing pivot R:R) /
│   │                                   KELLY SIZING.
│   ├── OutputDumpSettingsForm.vb       Non-modal dialog: Enabled toggle, max-runs
│   │                                   textbox, file path + size, Clear + Save + Close.
│   │                                   Save routes through SettingsLoader.Save.
│   └── MainForm_Render_Sections.vb     RenderOutput() entry point;
│                                       all indicator sections: DYNAMIC NORMS, REGIME,
│                                       CORE SIGNALS, VWAP, BBW/TTM, EMA RIBBON,
│                                       MARKET STRUCTURE, OPEN INTEREST, ORDER FLOW,
│                                       LIQUIDATIONS, MTF GATE, FUNDING;
│                                       SIGNAL BREAKDOWN table;
│                                       verdict label (lblVerdict) colour update.
│                                       Split from MainForm_Render.vb.
│
├── analysis/                          Host-agnostic offline analysis (Bundle 1).
│                                       NO System.Windows.Forms references except
│                                       AnalysisReportForm (thin viewer).
│                                       AnalysisRunner, ForwardReturnJoiner,
│                                       FailureRateMatrix, FundingMomentumDiagnostic,
│                                       OutlierAudit, MarkdownReportWriter,
│                                       AnalysisReport, AnalysisConstants.
│                                       Reusable from future Linux CLI port.
│
├── tools/
│   └── AutoTweaker/                    Host-agnostic console app (Bundle 2).
│                                       AutoTweaker.csproj — separate .NET 8 project.
│                                       Zero WinForms references. Runs unmodified
│                                       on Linux via `dotnet AutoTweaker.dll`.
│                                       AutoTweakerProgram, AutoTweakerCore,
│                                       PromptBuilder, ClaudeApiClient,
│                                       SettingsDiffApplier, TweakerConfig, TweakerState.
│
└── docs/
    ├── DeribitIndicatorProject.md      Authoritative handover document (read first)
    ├── architecture.md                 This file
    ├── trader-profile.md               Trader style, preferences, collaboration rules
    ├── verdict-context-tag-proposal.md Spec: Verdict Sub-Context Tag — IMPLEMENTED
    ├── kelly-criterion-proposal.md     Spec: Kelly Criterion sizing — IMPLEMENTED
    ├── bid-ask-spread-proposal.md      Spec: Bid-ask spread signal — IMPLEMENTED
    ├── ofi-momentum-proposal.md        Spec: OFI Momentum — IMPLEMENTED
    ├── dynamic-microcvd-accel-proposal.md  Spec: Dynamic MicroCVD — IMPLEMENTED
    ├── vpfr-lite-v2-proposal.md        Spec: VPFR-lite v2 — IMPLEMENTED
    ├── swing-pivot-proposal.md         Spec: Swing pivot detection — IMPLEMENTED
    ├── settings-exposure-pass-proposal.md  Spec: Settings exposure pass — IMPLEMENTED
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
        ├─► DeribitClient.GetCandlesAsync("1", 250)      → candles1m      (List or Nothing)
        ├─► DeribitClient.GetCandlesAsync("5", 210)      → candles5m      (List or Nothing)
        ├─► DeribitClient.GetCandlesAsync("15", 70)      → candles15m     [cached/TTL]
        ├─► DeribitClient.GetFundingRateAsync()          → fundingRate    (Double? or Nothing)
        ├─► DeribitClient.GetBookSummaryAsync()          → bookSummary    (tuple? or Nothing)
        ├─► DeribitClient.GetOrderBookAsync(10)          → orderBook      (snapshot or Nothing)
        └─► DeribitClient.GetRecentTradesAsync(100)      → recentTrades   (List or Nothing)
                    │  (all fetched in parallel via Task.WhenAll;
                    │   15m only included when cache is stale;
                    │   each call wrapped in ExecuteWithRetry — retry-once on 5xx/timeout,
                    │   return Nothing on hard failure; 15m cache preserved on fetch failure)
                    │
                    │  Resilience check after Task.WhenAll:
                    │  if any required result is Nothing → render ANALYSIS SKIPPED,
                    │  increment _skipCount, return (no scoring, no CSV row).
                    │  15m failure alone does not skip — stale cache kept for MTF gate.
                    ▼
        IndicatorResults  r  (filled field by field)
        ├─ r.CurrentPrice       = candles1m.Last().Close
        ├─ r.ATR                = CalcATR(candles1m, 7)
        ├─ DynamicNorms.Compute → norms (ATRScaleFactor, VolThresholds, VWAPDevThreshold)
        │                         → ApplySessionVolume(cfg.SessionVolume, utcHour)
        │                         → session-adjusted VolHighThreshold / VolMidThreshold
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
        ├─ _fundingHistory.Add(fundingRate); trim to FundingHistoryMax
        ├─ r.FundingMomentum    = CalcFundingMomentum(_fundingHistory, cfg)
        │                         → RISING / FALLING / FLAT
        ├─ r.OI_Current/Changes = bookSummary.OI + _oiHistory ring buffer
        ├─ r.SpreadBps          = (orderBook.BestAsk - orderBook.BestBid) / mid × 10000
        ├─ r.OFI* / OFISignal   = CalcOFI(orderBook, bookDepth:=cfg.Indicators.OFI.BookDepth)
        ├─ _ofiHistory.Add(r.OFISignal); trim to OFIHistoryMax
        ├─ r.OFIMomentum        = CalcOFIMomentum(_ofiHistory, cfg) → RISING/FALLING/FLAT
        ├─ r.Liq* / LiqSignal   = CalcLiquidations(recentTrades, dominanceRatio from cfg)
        ├─ r.CVD* / CVDSlope    = CalcCVD(recentTrades, candles1m,
        │                         lateSegmentWeight:=cfg.Indicators.CVD.LateSegmentWeight,
        │                         earlySegmentWeight:=cfg.Indicators.CVD.EarlySegmentWeight)
        ├─ r.MicroCVD*          = CalcMicroCVD(recentTrades)
        │                         [dynamic accelThreshold: max(staticFloor, windowUsd × pct)]
        ├─ r.TFI* / TFISignal   = CalcTFI(recentTrades)
        ├─ r.MTFGatePass/Reason = CalcMTFGate(candles15m)  [cached; refreshed by TTL]
        ├─ r.DonchianSignal     = CalcDonchian(candles1m,
        │                         quartilePct:=cfg.Indicators.Donchian.QuartilePct)
        ├─ r.OBVTrend/Div       = CalcOBV
        ├─ r.VPFR* / Signal     = CalcVPFRLite v2 (exp decay; VAH/VAL; nearest HVN/LVN;
        │                         numBuckets from cfg)
        ├─ r.LastSwingHigh/Low5m = CalcSwingPivots(candles5m,
        │                          pivotWing:=cfg.Indicators.Swing.PivotWing5m,
        │                          lookbackBars:=cfg.Indicators.Swing.LookbackBars5m)
        ├─ r.LastSwingHigh/Low15m = CalcSwingPivots(candles15m, ...)  [optional context]
        └─ r.SwingTarget/StopLong/Short = direction-aware bookkeeping (inline in Analysis)
                    │
                    ▼
        ScoringEngine.Calculate(r, posState, norms, cfg)
        [ScoringEngine_Calculate_Verdict.vb → calls RunScoringPipeline in _Scoring.vb]
                    │
                    ├─ Step 1:  Regime classification → MaxScore.
                    │          Base 19/18/15; with RegimeWeights.Enabled (default)
                    │          20/19/15 (base + Trending/RangeBound AlignmentBonus).
                    ├─ Step 2:  Score each signal → ScoreState
                    │          All thresholds from cfg. Scoring highlights:
                    │          ─ SpreadBps WIDE penalty (cfg.Indicators.Spread.*)
                    │          ─ OFIMomentum RISING/FALLING modifier on OFI level score
                    │          ─ BBW squeeze penalty (cfg.Scoring.BbwSqueezePenalty)
                    │          ─ Liquidation penalty/boost by size + dominance
                    │          ─ OBV adverse divergence blocks cross-category upgrade
                    ├─ Pass 2:  Partial upgrade on cross-category confirmation
                    ├─ Pass 2b: OI × CVD cross-confirm
                    │          If cfg.Indicators.OiCvd.Enabled and OI direction/sign
                    │          confirms CVD bullish/bearish direction, apply
                    │          cfg.Indicators.OiCvd.UpgradeBonus (capped at regimeMax).
                    │          If full OI directly conflicts with CVD, apply
                    │          cfg.Indicators.OiCvd.ConflictPenalty.
                    │          Upgraded partial OI signals (COVERING/CAPITULATION)
                    │          can confirm, but partial conflict is not penalised.
                    │          Result is appended to the OI Delta breakdown note.
                    ├─ Pass 2c: Regime alignment gate
                    │          Suppressed in TRANSITIONAL or when LongScore=ShortScore.
                    │          TRENDING: EMA ribbon + ROC (threshold-gated by
                    │          MagnitudeThreshold) + CVD slope+sign.
                    │          RANGE_BOUND: VWAP dev (suppressed in warmup) +
                    │          RSI(9) vs cfg.Indicators.RSI.Pass2cMidline + Donchian(20).
                    │          All active signals aligned → +AlignmentBonus on dominant
                    │          side (capped at regimeMax). All conflict → -ConflictPenalty.
                    │          Reads cfg.RegimeWeights.{Trending|RangeBound}.
                    │          {AlignmentBonus,ConflictPenalty}. ls/ss snapshot taken
                    │          AFTER this pass, before funding modifiers.
                    ├─ Step 3:  Baseline funding-rate modifier (penalty/boost from cfg)
                    ├─ Step 3b: Funding-momentum modifier
                    │          If FundingMomentum=RISING and funding already crowded
                    │          → amplify penalty by cfg.Indicators.Funding.MomentumAmplify
                    │          If FundingMomentum=FALLING and funding crowded
                    │          → soften penalty by cfg.Indicators.Funding.MomentumSoften
                    │          Controlled by cfg.Indicators.Funding.MomentumEnabled.
                    │          Zero scoring impact when momentum = FLAT or disabled.
                    ├─ Step 4:  Regime veto + TRANSITIONAL ADX penalty
                    ├─ Step 4b: MTF gate veto → forces NO TRADE on BLOCK
                    ├─ Step 5:  Threshold comparison → verdict string
                    ├─ Step 5 (post): CalcVerdictContext → VerdictResult.VerdictContext
                    │          (FLOW_UNCONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK /
                    │          CONFIRMED). Structural check fires STRUCTURALLY_WEAK when
                    │          swing data exists but no clean target+stop pair.
                    │          All thresholds from cfg.Scoring.ContextTagThresholds.
                    │          See docs/verdict-context-tag-proposal.md.
                    ├─ Step 5 (post): CalcHoldStatus → hold/exit/flip guidance.
                    │          Layer 1: 2+ adverse microstructure signals → EXIT
                    │          Layer 1.5: structural break (swing low/high breach) → EXIT
                    │          Layer 2: OBV divergence → EXIT
                    │          Layer 3: RSI divergence / single signal / RSI+ROC
                    └─ Step 5b: 3-tier target cap:
                               Tier 1 (highest priority): swing target
                               Tier 2: nearest HVN above/below
                               Tier 3 (fallback): POC
                               Winner = closest cap to entry. Sets AdjustedLongTarget /
                               AdjustedShortTarget and TargetCapReason label.

                    (Kelly sizing is NOT invoked here. CalcKellySizing() is called from
                     MainForm_Render_Header.RenderOutputHeader() after ATR entry levels
                     are rendered. See docs/kelly-criterion-proposal.md.)
                    │
                    ▼
        VerdictResult  v
                    │
                    ▼
        MainForm_Render_Sections.vb :: RenderOutput(v, r)
        [calls RenderOutputHeader() from MainForm_Render_Header.vb for top block]
                    │
                    ├─ Verdict header + score + breakdown  [_Header]
                    ├─ CONTEXT: line (always shown)        [_Header]
                    │          CONFIRMED → C_GOOD (green)
                    │          FLOW_UNCONFIRMED / MOMENTUM_FADING /
                    │          STRUCTURALLY_WEAK → amber/red/dim as appropriate
                    ├─ Hold/position guidance              [_Header]
                    ├─ ATR entry / stop / target block     [_Header]
                    │          3-tier-capped target in amber bold with tier label
                    ├─ Long + Short structural rows        [_Header]
                    │          Swing pivot stop/entry/target + R:R display
                    │          (cyan full pair; dim when only one side available)
                    ├─ KELLY SIZING block                  [_Header]
                    │          Contracts / USD risk / [CAPPED] tag when applicable.
                    │          Advisory label always rendered below header:
                    │          "Advisory (ATR-basis) — R:R uses ATR multiples,
                    │           not structural targets. Treat as directional bias
                    │           indicator only."
                    │          EST mode only — CAL mode removed pending backtesting.
                    │          Suppressed when KellyF = 0
                    ├─ DYNAMIC NORMS / REGIME / CORE SIGNALS / VWAP /
                    │  BBW/TTM / EMA RIBBON / MARKET STRUCTURE /
                    │  OI / ORDER FLOW / LIQUIDATIONS /    [_Sections]
                    │  MTF GATE / FUNDING sections
                    ├─ FUNDING section                     [_Sections]
                    │          Row 1: rate value + bias label
                    │          Row 2: Momentum → RISING / FALLING / FLAT
                    │                 + enabled/amplify/soften config values
                    └─ Signal breakdown table + lblVerdict colour update  [_Sections]
                    │
                    ▼
        AnalysisLogger.LogRun(r, verdict) → analysis_log.csv
```

---

## Settings Data Flow

```
settings.json (v17)
    │
    ▼
SettingsLoader.Initialise()
    │
    ▼
SettingsLoader.Current As EngineSettings
    │
    ├─ Indicators.*        → indicator thresholds / windows (incl. new Swing, Spread,
    │                         and per-indicator Optional params lifted in v17)
    ├─ Scoring.*           → verdict %, penalties, ATR multipliers;
    │                         now also: RegimeMaxScore.{Trending/RangeBound/Transitional},
    │                         TierFloor.{High/Med/Low Threshold+Floor},
    │                         ContextTagThresholds.{MomentumFadingDecayRatio,
    │                           MomentumFadingCountMin, StructurallyWeakStructMin,
    │                           StructurallyWeakFlowMin}
    ├─ Kelly.*             → sizing display controls
    ├─ FundingSettings     → Step 3b momentum behaviour
    ├─ OiCvdSettings       → Pass 2b OI×CVD confirm/conflict behaviour
    │     ├─ enabled
    │     ├─ upgrade_bonus
    │     └─ conflict_penalty
    ├─ SessionVolumeSettings
    │      ├─ enabled
    │      └─ sessions[]  — ordered list of UTC buckets, each:
    │           { name, start_hour, end_hour,
    │             high_multiplier, mid_multiplier }
    │         Current settings.json populates ASIA (00–07, 0.80/0.85),
    │         LONDON (08–12, 1.00/1.00), NY (13–23, 1.15/1.10).
    │         First bucket whose [start_hour..end_hour] contains UTC hour wins;
    │         no implicit fallback — if no bucket matches, thresholds stay
    │         at their DynamicNorms-computed values.
    ├─ RegimeWeightSettings
    │      ├─ enabled
    │      ├─ trending    { alignment_bonus, conflict_penalty }
    │      └─ range_bound { alignment_bonus, conflict_penalty }
    ├─ SwingSettings       → pivot_wing_5m, lookback_bars_5m,
    │                        pivot_wing_15m, lookback_bars_15m
    └─ NetworkSettings     → request_timeout_seconds (HttpClient.Timeout, set once at
                             DeribitClient static ctor), retry_count, retry_backoff_ms
                    │
                    ▼
DynamicNorms.Compute(...)
    │
    └─ ApplySessionVolume() adjusts VolHighThreshold / VolMidThreshold
       by active UTC session bucket before scoring consumes volume signals

RunScoringPipeline(...)
    │
    ├─ Pass 2b reads cfg.Indicators.OiCvd to decide whether OI/CVD alignment
    │  earns a bonus or full-signal conflict earns a penalty
    └─ Step 5b reads cfg.Scoring.ContextTagThresholds for VerdictContext decay
       ratios / count thresholds, and CalcHoldStatus / 3-tier cap read
       cfg.Scoring.{RegimeMaxScore, TierFloor} for all formerly-hardcoded values
```

---

## Design Decisions

| Decision | Rationale |
|---|---|
| REST polling (not WebSocket) | Simpler implementation; adequate for 1m candle resolution. WebSocket is the highest-impact next upgrade. |
| API resilience — retry + skip (v18) | Transient Deribit/Cloudflare failures (HTTP 525, timeout) observed during AFK auto-run. `ExecuteWithRetry` in `DeribitClient`: retry-once on 5xx/timeout, return `Nothing` on hard failure. `RunAnalysisAsync` skip-on-any-failure rather than degraded-mode — cleaner calibration CSV, simpler code. 15m failure does not skip: stale MTF cache data is better than no data (15m candles change slowly). Retry-once vs exponential backoff: single retry catches the most common transient flakes without risking overlap with the next auto-run cycle. Both layers preserve the same `GetXxxAsync` call-site contract so WebSocket migration can replace the implementation without changing call sites. |
| 15m MTF cached with TTL | 15m candles change slowly; re-fetching every 10s run wastes API quota. TTL=60s balances freshness vs. rate limits. |
| Dual-session VWAP anchor | BTC perpetual has meaningful session breaks at 00:00 UTC and 13:30 UTC. Single-anchor VWAP deviates badly after Asian/EU handoff. |
| VerdictContext (Step 5b) | WEAK verdicts have three distinct structural causes that require different discretionary responses. Context tag surfaces the cause without changing the score. Zero new data fetches; reads already-computed state only. |
| Kelly sizing display-only | Sizing advisory only — no position management integration. Suppressed when no edge (KellyF ≤ 0). An inline advisory label immediately under the header reminds the reader that the R:R used is ATR-basis (not structural), so the block is a directional-bias sanity check rather than a trade-sizing prescription. EST mode only; CAL mode will be reinstated once a backtesting module supplies empirical per-tier win rates. |
| Exponential decay in VPFR | Recent price levels are more relevant than historical. Linear decay overstates old HVNs; exponential decay (base=0.985) self-tunes to recent session structure. |
| CVD 3-segment slope | Late segment weighted ×2 vs early ×1. Captures momentum *direction change* mid-window (deceleration signal), not just net delta. |
| OFI descending weight array | Levels deeper in the book are less actionable. Dynamic descending weights (injectable depth) reduce noise from thin deep levels. |
| Regime hysteresis (1 bar) | Prevents regime flip-flop on RANGE_BOUND→TRENDING boundary. Single bar of grace avoids scoring discontinuity on noisy ADX crossings. |
| Settings externalised to JSON | All thresholds tunable without recompile. SettingsLoader.Current singleton; EngineSettings is the POCO contract. |
| MicroCVD can be negative | `MicroCVDEarly` and `MicroCVDLate` are net USD deltas over their sub-windows. Negative values are valid and intentional — they indicate net sell pressure in that segment. |
| Funding momentum as adjunct (Step 3b) | Absolute funding rate alone misses the *direction of crowding*. A rate already at +0.03% but falling is less dangerous than one at +0.02% and rising fast. Step 3b amplifies or softens the Step 3 penalty based on momentum direction, using a short rolling window (default 3 samples) held in `_fundingHistory`. Display-only impact on the funding UI row; scoring impact is bounded by the amplify/soften cfg values. |
| _fundingHistory capped at FundingHistoryMax | Funding rate changes are slow relative to 1m candles. A window of 10 samples is sufficient to detect sustained crowding direction without accumulating stale history across sessions. |
| Session-aware volume norms in DynamicNorms | BTC volume has strong time-of-day seasonality. A single global `VolHighThreshold` / `VolMidThreshold` misclassifies quiet Asian-session participation as expansion and underweights genuine London/NY burst volume. Applying UTC session multipliers at the DynamicNorms layer preserves existing scoring logic while adapting thresholds to expected liquidity. |
| OI × CVD as Pass 2b adjunct | OI and CVD together say more than either alone: rising/opening interest confirmed by supportive CVD is stronger than standalone OI, while a full OI build that directly opposes CVD often reflects weaker participation quality. Implementing this as a post-upgrade Pass 2b preserves existing indicator methods and lets the confirm/conflict effect be tuned independently via `OiCvdSettings`. Partial OI signals can be confirmed, but only full OI conflict is penalised, reducing false negatives on covering/capitulation transitions. |
| Pass 2c regime alignment gate | Static per-indicator weights over-reward weak signals that disagree with the active regime. Pass 2c rewards runs where the regime-key signals are fully aligned with the dominant side and penalises full conflict, while staying suppressed in TRANSITIONAL and on zero-net scores. RegimeMaxScore() adds the alignment bonus to the ceiling when enabled so verdict % thresholds auto-adjust and the bonus cannot carry the score past saturation. |
| ScoringEngine split into _Scoring + _Verdict | ScoringEngine_Calculate.vb exceeded 35 KB. Split into RunScoringPipeline (Steps 2–3b + breakdown notes) in _Scoring.vb and Calculate() entry point (Steps 4–5b) in _Verdict.vb. CalcVerdictContext kept in _Scoring.vb as it is called from multiple early-return paths in _Verdict.vb. |
| MainForm_Render split into _Header + _Sections | MainForm_Render.vb exceeded 28 KB. RTF helpers + top render block (verdict/ATR/Kelly) in _Header.vb; RenderOutput() entry point + all indicator sections + breakdown table in _Sections.vb. |
| GetSessionCandles helper extracted (v15) | `CalcVWAP` and `CalcVWAPBands` both anchored on the session-2 cutoff and re-derived the session window independently. The boundary calculation is now a single private helper in `Indicators_Volatility.vb`; both callers route through it. |
| v15 cleanup pass | Source of truth audit. Removed dead fields (`OI_Prev15m` / `OI_Prev60m` / `ATRAvg20d`), three unused `DynamicNorms.StaticVol*` properties, an entire `Ema200Settings` class, 13 silently-ignored config properties, the dead `ScoringEngine.MaxScore` const, the unused `SettingsLoader.Reload()`. Aligned remaining default values with v14 calibration (so an absent `settings.json` doesn't seed stale defaults). Two display-only colour bugs fixed in `MainForm_Render_Sections` (BBW status compared against `"SQUEEZE"` instead of `"ACTIVE"`; TTM direction compared against `"UP"` / `"DOWN"` instead of `"RISING"` / `"FALLING"`). Zero scoring impact. |
| Bid-ask spread as entry-side gate (spec #1) | The order book is already fetched. `SpreadBps` is a near-zero-cost microstructure signal: a WIDE spread during an apparent directional signal often indicates a flush in progress, not a clean breakout. Penalises entries only — no direction signal, no scoring during trending spread expansion. |
| OFI Momentum mirrors FundingMomentum pattern (spec #2) | OFI level measures current order-flow imbalance; OFI momentum measures whether that imbalance is accelerating or decelerating. A level signal being amplified by rising momentum is structurally stronger than a level signal that is fading. Ring buffer pattern and RISING/FALLING/FLAT enum match the already-shipped FundingMomentum design so the engine is internally consistent. |
| Dynamic MicroCVD accelThreshold (spec #3) | Static threshold (10000 USD) was too high a bar on quiet sessions (total window USD flow might be only 2× the threshold) and too low on high-volume sessions (normal flow exceeds it continuously). Dynamic `max(static, windowUsd × pct)` self-scales: the static floor prevents it from being trivially crossed on micro-flow sessions; the multiplier raises the ceiling proportionally with actual session activity. |
| VPFR-lite v2 VAH/VAL + nearest HVN/LVN (spec #4) | Raw POC alone misses the case where price is between two significant nodes. VAH/VAL define the value area (70% of volume) and give a profile-based context for VWAP extensions. `VPFRNearestHvnAbove` / `VPFRNearestHvnBelow` provide the closest resistance/support wall which is more actionable than the POC for target capping — price is more likely to stall at the nearest wall than to reach the POC when structure intervenes. Required by the 3-tier cap in spec #5. |
| Swing pivot detection — scan and bookkeeping (spec #5) | Confirmed swing pivot requires ALL N bars left and right to be strictly lower/higher than the pivot bar — avoids equal-high false positives that create spurious structure. Walking backward from scanEnd to find the most recent pivot (rather than scanning forward) means the engine always surfaces the freshest actionable level. Direction-aware bookkeeping (SwingTargetLong/Short, SwingStopLong/Short) computed inline in Analysis so CalcSwingPivots stays a pure function. |
| 3-tier Step 5b target cap (spec #5) | The prior 2-tier cap (HVN wall → POC) ignored swing structure. A confirmed 5m swing high is typically closer to entry and more immediately relevant than a VPFR HVN. The 3-tier priority (swing > nearest HVN > POC) reflects structural significance: swing targets are the first price memory the market has established, HVN walls are the second, and POC is the broadest fallback. Winner = closest to entry ensures the cap is conservative: the tightest available cap is used, not the loosest. |
| Layer 1.5 structural-break exit in CalcHoldStatus (spec #5) | A confirmed break through the prior swing low (long) or swing high (short) is a discrete structural event — it invalidates the original entry premise faster than gradual RSI/OBV divergence. Sits between Layer 1 (fast microstructure count) and Layer 2 (OBV divergence) to maintain the priority ordering: structural breaks are evaluated only when microstructure hasn't already triggered, but before the slower divergence signals can fire. |
| CalcVerdictContext structural-target first check (spec #5) | When swing data exists (LastSwingHigh5m or LastSwingLow5m is non-zero), the engine has committed to a structural view. If neither target nor stop can be placed for the current direction, flagging STRUCTURALLY_WEAK is more informative than CONFIRMED even if the score is high — it means the structural picture is ambiguous or the trade doesn't have a defined structural R:R. The graceful-degradation path (check fires only when at least one swing level exists) prevents false STRUCTURALLY_WEAK signals when candle history is too short for pivot detection. |
| Settings exposure pass (spec #6) | Exposing 19 formerly-hardcoded literals to settings.json completes the auto-tweaking audit prerequisite (Section 16.3 item 2). All defaults are exactly the previously-hardcoded values — zero behaviour change. The new Optional params on CalcBBW, CalcTTMSqueeze, CalcCVD, CalcDonchian match the existing pattern (cfg value passed at call site; default value in method signature for caller convenience). RegimeMaxScore() and TierFloor() now take cfg and read from POCO fields rather than returning hardcoded constants. |
| Linux CLI port as long-term target (2026-05-05) | The WinForms app remains the active surface but a future headless Linux service is on the roadmap (`DeribitIndicatorProject.md` §16.2). All code in `analysis/` and `tools/` must therefore be host-agnostic — no `System.Windows.Forms`, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers like `AnalysisReportForm` and `TweakSettingsForm` are thin wrappers over host-agnostic core. The auto-tweaker console app builds as a separate .NET 8 project with **zero WinForms references** so it runs unmodified under `dotnet AutoTweaker.dll` on Linux. Port itself happens after auto-tweaker ships AND analysis accuracy plateaus. WebSocket migration is independent of the port. |
