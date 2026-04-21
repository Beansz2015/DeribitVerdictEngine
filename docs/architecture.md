# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-04-21 | App version: adaptive-regime-weights (Pass 2c) shipped / settings v13**

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
├── DynamicNorms.vb                     Live adaptive thresholds (ATR scale, vol, VWAP dev);
│                                       now also applies session-aware volume multipliers
├── AnalysisLogger.vb                   CSV run logger + CalibrationReport
├── AutoRunTimer.vb                     IAutoRunTimer interface + WinFormsAutoRunTimer impl
├── OiSnapshot.vb                       OI ring-buffer snapshot struct
├── SettingsLoader.vb                   JSON loader — SettingsLoader.Current singleton
├── settings.json                       All tunable parameters v13 (no recompile needed)
│
├── Core/
│   ├── Settings/
│   │   └── EngineSettings.vb           Strongly-typed POCO for settings.json
│   │                                   Includes KellySettings + FundingSettings +
│   │                                   OiCvdSettings + SessionVolumeSettings +
│   │                                   RegimeWeightSettings blocks
│   │
│   ├── ScoringEngine_Types.vb          Enums + result types: SignalBreakdownItem,
│   │                                   VerdictResult (incl. AdjustedLongTarget,
│   │                                   AdjustedShortTarget, TargetCapReason,
│   │                                   VerdictContext, Kelly fields),
│   │                                   PositionState, SignalCategory, ScoreState
│   ├── ScoringEngine_Helpers.vb        Pure functions: RegimeMaxScore, Threshold, TierFloor,
│   │                                   AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus
│   ├── ScoringEngine_Calculate_Scoring.vb
│   │                                   AppendLean(), CalcVerdictContext();
│   │                                   RunScoringPipeline() — Steps 2/Pass 2/Pass 2b/3/3b:
│   │                                   signal scoring, partial upgrades, OI×CVD cross-confirm,
│   │                                   funding modifiers, all breakdown note rows.
│   │                                   Split from ScoringEngine_Calculate.vb.
│   ├── ScoringEngine_Calculate_Verdict.vb
│   │                                   Calculate() entry point — assembles verdict;
│   │                                   Step 4: regime veto / TRANSITIONAL ADX penalty;
│   │                                   Step 4b: MTF gate veto;
│   │                                   Step 5: threshold comparison → verdict string;
│   │                                   Step 5b: ATR target cap (VPFR HVN).
│   │                                   Split from ScoringEngine_Calculate.vb.
│   ├── ScoringEngine_Kelly.vb          CalcKellySizing() — display-only Kelly Criterion
│   │                                   sizing. Called from MainForm_Render after ATR levels,
│   │                                   not from ScoringEngine.Calculate(). Zero scoring impact.
│   │
│   ├── IndicatorResults.vb             IndicatorResults struct — all indicator output fields
│   │                                   incl. FundingMomentum (RISING/FALLING/FLAT)
│   ├── Indicators_Momentum.vb          CalcDMI, CalcATR, CalcEMA, CalcRSI,
│   │                                   CalcRSISeries, CalcRSIDivergence, CalcROCSeries,
│   │                                   CalcVolumeSMA
│   ├── Indicators_Volatility.vb        CalcVWAP (dual-session auto-anchor),
│   │                                   CalcVWAPBands, CalcBBW, CalcTTMSqueeze
│   ├── Indicators_OrderFlow.vb         CalcOFI (bookDepth param, dynamic descending weights),
│   │                                   CalcCVD (3-segment weighted slope),
│   │                                   CalcMicroCVD, CalcTFI, CalcLiquidations,
│   │                                   CalcFundingMomentum
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
│   │                                   _prevRegime (regime hysteresis);
│   │                                   _fundingHistory (List(Of Double)),
│   │                                   FundingHistoryMax (const=10)
│   ├── MainForm_AutoRun.vb             Auto-run timer: InitAutoRunControls(),
│   │                                   btnStartStop_Click, StartAutoRun(), StopAutoRun(),
│   │                                   RunAutoAnalysis(), OnCountdownTick(),
│   │                                   UpdateCountdownLabel()
│   ├── MainForm_Analysis.vb            btnAnalyze_Click, RunAnalysisAsync() —
│   │                                   fetches data, calls all indicators + scoring engine,
│   │                                   logs result, calls RenderOutput;
│   │                                   MTF TTL refresh; Donchian quartile signal;
│   │                                   regime hysteresis logic; OFI BookDepth wiring;
│   │                                   appends fundingRate to _fundingHistory,
│   │                                   calls CalcFundingMomentum → r.FundingMomentum
│   ├── MainForm_Render_Header.vb       RTF helpers: AppendRtf(), AR(), SectionHeader(),
│   │                                   Divider();
│   │                                   log/calibration helpers: UpdateLogInfo(),
│   │                                   BuildCalibrationReport(), Flag(),
│   │                                   lnkResetLog_LinkClicked, lnkCalibCheck_LinkClicked;
│   │                                   RenderOutputHeader() — top render block:
│   │                                   VERDICT / CONTEXT / CONFIDENCE / SCORE / TIME /
│   │                                   LAST TRANSACTED PRICE / HOLD STATUS /
│   │                                   ATR ENTRY LEVELS / KELLY SIZING.
│   │                                   Split from MainForm_Render.vb.
│   └── MainForm_Render_Sections.vb     RenderOutput() entry point;
│                                       all indicator sections: DYNAMIC NORMS, REGIME,
│                                       CORE SIGNALS, VWAP, BBW/TTM, EMA RIBBON,
│                                       MARKET STRUCTURE, OPEN INTEREST, ORDER FLOW,
│                                       LIQUIDATIONS, MTF GATE, FUNDING;
│                                       SIGNAL BREAKDOWN table;
│                                       verdict label (lblVerdict) colour update.
│                                       Split from MainForm_Render.vb.
│
└── docs/
    ├── DeribitIndicatorProject.md      Authoritative handover document (read first)
    ├── architecture.md                 This file
    ├── trader-profile.md               Trader style, preferences, collaboration rules
    ├── verdict-context-tag-proposal.md Spec: Verdict Sub-Context Tag — IMPLEMENTED
    ├── kelly-criterion-proposal.md     Spec: Kelly Criterion sizing — IMPLEMENTED
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
        ├─► DeribitClient.GetCandlesAsync("1", 250)      → candles1m
        ├─► DeribitClient.GetCandlesAsync("5", 210)      → candles5m
        ├─► DeribitClient.GetCandlesAsync("15", 70)      → candles15m  [cached/TTL]
        ├─► DeribitClient.GetFundingRateAsync()          → fundingRate
        ├─► DeribitClient.GetBookSummaryAsync()          → bookSummary
        ├─► DeribitClient.GetOrderBookAsync(10)          → orderBook
        └─► DeribitClient.GetRecentTradesAsync(100)      → recentTrades
                    │  (all fetched in parallel via Task.WhenAll;
                    │   15m only included when cache is stale)
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
        [ScoringEngine_Calculate_Verdict.vb → calls RunScoringPipeline in _Scoring.vb]
                    │
                    ├─ Step 1:  Regime classification → MaxScore.
                    │          Base 19/18/15; with RegimeWeights.Enabled (default)
                    │          20/19/15 (base + Trending/RangeBound AlignmentBonus).
                    ├─ Step 2:  Score each signal → ScoreState
                    │          All thresholds from cfg. Scoring highlights:
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
                    │          SlopeSensitivity) + CVD slope+sign.
                    │          RANGE_BOUND: VWAP dev (suppressed in warmup) +
                    │          RSI(9) vs 50 + Donchian(20).
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
                    │          CONFIRMED). Zero scoring impact.
                    │          See docs/verdict-context-tag-proposal.md.
                    ├─ Step 5 (post): CalcHoldStatus → hold/exit/flip guidance.
                    └─ Step 5b: VPFR HVN cap → AdjustedLongTarget / AdjustedShortTarget.
                               Runs last in ScoringEngine.Calculate().

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
                    │          HVN-capped target in amber bold when adjusted target > 0
                    ├─ KELLY SIZING block                  [_Header]
                    │          Contracts / USD risk / [EST] or [CAL] or [CAPPED] tag
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
settings.json (v13)
    │
    ▼
SettingsLoader.Initialise()
    │
    ▼
SettingsLoader.Current As EngineSettings
    │
    ├─ Indicators.*        → indicator thresholds / windows
    ├─ Scoring.*           → verdict %, penalties, ATR multipliers
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
    └─ RegimeWeightSettings
           ├─ enabled
           ├─ trending    { alignment_bonus, conflict_penalty }
           └─ range_bound { alignment_bonus, conflict_penalty }
                    │
                    ▼
DynamicNorms.Compute(...)
    │
    └─ ApplySessionVolume() adjusts VolHighThreshold / VolMidThreshold
       by active UTC session bucket before scoring consumes volume signals

RunScoringPipeline(...)
    │
    └─ Pass 2b reads cfg.Indicators.OiCvd to decide whether OI/CVD alignment
       earns a bonus or full-signal conflict earns a penalty
```

---

## Design Decisions

| Decision | Rationale |
|---|---|
| REST polling (not WebSocket) | Simpler implementation; adequate for 1m candle resolution. WebSocket is the highest-impact next upgrade. |
| 15m MTF cached with TTL | 15m candles change slowly; re-fetching every 10s run wastes API quota. TTL=60s balances freshness vs. rate limits. |
| Dual-session VWAP anchor | BTC perpetual has meaningful session breaks at 00:00 UTC and 13:30 UTC. Single-anchor VWAP deviates badly after Asian/EU handoff. |
| VerdictContext (Step 5b) | WEAK verdicts have three distinct structural causes that require different discretionary responses. Context tag surfaces the cause without changing the score. Zero new data fetches; reads already-computed state only. |
| Kelly sizing display-only | Sizing advisory only — no position management integration. Suppressed when no edge (KellyF ≤ 0). [EST] / [CAL] modes enforce honesty about calibration state. |
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
