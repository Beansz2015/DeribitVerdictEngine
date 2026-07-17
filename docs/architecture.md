# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-07-07 | App version: settings.json v51 — B4b placed-geometry structural-first levels (ONE arbitration seam `SignalEmitter.ComputeSideLevels`; Step 5b delegates; FOUR parity surfaces: snapshot ↔ cards ↔ `verdict_signal.json` ↔ CSV `Placed*`) on top of v50 #5 aggressor-velocity + retune bundle (CSV v0.8), v49 signal-bridge emitter, v48 OFI dominance re-baseline, v42 WebSocket cutover, v31 correctness pass. `settings.json` line 1 is the version source of truth.**

> **Trade-stream contract (v31).** `DeribitClient.GetRecentTradesAsync` returns trades in **chronological ascending** order (oldest first, most recent last) — the HTTP request keeps `sorting=desc` to guarantee the latest trades, and the parsed list is reversed before return. Window-consuming indicators (TFI, MicroCVD) take their window from the **end** of the list via `IndicatorEngine.LastN`; `Take(n)` on a trade list selects the OLDEST n and is a bug. CalcCVD's positional thirds (early/mid/late) are chronologically truthful under this contract.

> **Auto-tweaker windowing (v29).** `tools/AutoTweaker/AutoTweakerCore.RunAsync` reads `TweakerConfig.WindowMode`. In `fixed` mode (default), a "round" is a disjoint slice `allRows[LastEvaluatedRowIndex .. +WindowSize-1]`; the index advances by exactly `WindowSize` after every completed terminal branch (including the new `SKIPPED_INSUFFICIENT_TIER` / `SKIPPED_SESSION_BOUNDARY` outcomes — which do **not** tick the BELOW_THRESHOLD streak). `cooldown_rows` is a no-op in fixed mode. MinTier resolves through `TweakerConfig.EffectiveMinTier(windowSize)` — null in JSON auto-scales as `max(15, ceil(WindowSize × 0.5))`. Sliding mode is retained behind the `Else` arm for legacy comparison and is documented as deprecated.

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
├── IMarketDataSource.vb                [WS-P1] Transport contract mirroring DeribitClient's 5
│                                       live call shapes. DORMANT until P2 routes RunAnalysisAsync
│                                       through it by network.transport. Host-agnostic.
├── RestMarketDataSource.vb             [WS-P1] Pass-through to DeribitClient (the fallback path).
├── WsMarketDataSource.vb               [WS-P1] Serves the 5 shapes from MarketState; staleness-
│                                       gated (book/trades/ticker); candles defer to IsFresh.
├── MarketState.vb                      [WS-P1] Thread-safe snapshot store (1 SyncLock, copy-on-
│                                       read): 4 candle series 1/3/5/15, 5000 ascending trade
│                                       ring, top-10 ladder, ticker (funding_8h/OI/mark).
├── DeribitWsFeed.vb                    [WS-P1] One public ClientWebSocket — REST-seed →
│                                       set_heartbeat → subscribe 1/3/5/15 chart + trades +
│                                       ticker + depth-limited book → receive loop → backoff
│                                       reconnect. Answers heartbeat test_request. DORMANT
│                                       (only the standalone soak constructs it). Host-agnostic.
├── ShadowParityComparer.vb             [WS-P2] When network.shadow_parity=true, compares the
│                                       WS source vs the authoritative REST results each run and
│                                       logs a field diff to ws_parity_log.txt + console — NEVER
│                                       the CSV/scoring. The proposal §7 acceptance instrument.
│                                       Host-agnostic. (RunAnalysisAsync routes the 8 live fetches
│                                       through IMarketDataSource via ResolveSource() since P2.)
├── MtfRefreshPolicy.vb                 [WS-P3] Pure host-agnostic predicate — whether to (re)fetch
│                                       15m this run. transport="ws" → always (15m is in-memory; the
│                                       60s TTL only spares the REST HTTP call, so it's moot);
│                                       "rest" → the original TTL gate, byte-identical. §4 15m-TTL
│                                       collapse; harness-tested A16d/e.
├── DynamicNorms.vb                     Live adaptive thresholds (ATR scale, vol, VWAP dev);
│                                       now also applies session-aware volume multipliers
├── AnalysisLogger.vb                   CSV run logger + CalibrationReport
├── AutoRunTimer.vb                     IAutoRunTimer interface + WinFormsAutoRunTimer impl
├── OiSnapshot.vb                       OI ring-buffer snapshot struct
├── settings.json                       All tunable parameters (version: see line 1 of the file; no recompile needed)
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
│   │                                   FileSystemWatcher hot-reload; Save(... bumpVersion)
│   │                                   — operational/UI saves pass False (v36 §10a)
│   │
│   ├── ExecutionResolution.vb          [v36] Host-agnostic session-conditional execution
│   │                                   resolver. MatchSessionBucket (shared by
│   │                                   DynamicNorms.ApplySessionVolume + the display
│   │                                   ResolveSessionLabel), ResolveResolution,
│   │                                   ResolveRocMagnitude / ResolveRocSlopeDelta, +
│   │                                   ResolveRocMagnitudeForHour (v40 (B) re-baseline —
│   │                                   per-session 3-min ROC magnitude ASIA 0.17 / LONDON
│   │                                   0.11; slope shared in resolution_profiles).
│   │
│   ├── ProcessIdentity.vb              [Signal Bridge v1] Shared process-identity
│   │                                   primitive: instance_id GUID per process start +
│   │                                   signal_id ticked once per completed run (skips
│   │                                   included) in RunAnalysisAsync, BEFORE the CSV
│   │                                   write. Consumed by SignalEmitter now and by the
│   │                                   CSV InstanceId/SignalId attribution columns at
│   │                                   the #5 v0.8 rotation. Host-agnostic.
│   ├── SignalEmitter.vb                [Signal Bridge v1] verdict_signal.json emitter
│   │                                   (signal-bridge-v1-proposal.md §3 — schema v1
│   │                                   FROZEN 2026-07-03). Pure BuildOk/BuildSkipped map
│   │                                   the SAME VerdictResult/IndicatorResults fields
│   │                                   the snapshot renders (incl. sub-tick cap-noise
│   │                                   suppression) — the THIRD parity surface. Pinned
│   │                                   DeriveDirection (NONE on all NO TRADE*) +
│   │                                   DeriveWsHealth (OK/DEGRADED/DOWN/REST). Atomic
│   │                                   TryWrite (tmp + File.Replace, create-dir,
│   │                                   never-throws). Host-agnostic; harness A22.
│   │                                   [v51 B4b] ComputeSideLevels IS the structural-
│   │                                   first arbitration (target ladder swing→HVN→
│   │                                   POC→session-resolved ATR fallback, bound 3.5×;
│   │                                   stop min(structural, 1.6×ATR) ≥ 4-tick floor;
│   │                                   labels SWING_STOP/STOP_CLAMPED/FALLBACK_ATR,
│   │                                   reason "PLACED @ p (LABEL)"). Consumed by
│   │                                   Step 5b + snapshot + card + payload + CSV
│   │                                   Placed* — FOUR parity surfaces, one seam.
│   │                                   enabled:false ⇒ v50 legacy geometry verbatim.
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
│   │                                   Step 5b [v51 B4b]: placed-level arbitration —
│   │                                   with structural_levels.enabled DELEGATES to
│   │                                   SignalEmitter.ComputeSideLevels (structural-
│   │                                   first target ladder + DG1 stop) and copies onto
│   │                                   Adjusted*/TargetCapReason*; enabled:false =
│   │                                   the legacy 3-tier closest-wins cap verbatim.
│   │                                   Step 5c (v35 min-move gate) evaluates the
│   │                                   PLACED target. + VerdictContext tag.
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
│   │                                   CalcTFI, CalcLiquidations,
│   │                                   CalcFundingMomentum + AppendFundingSample
│   │                                   (v53 time-anchored window + ring eviction)
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
│   │                                   _fundingHistory (List(Of (UtcMs, Rate)) — v53
│   │                                   timestamped ring, age-evicted at 30 min, no count cap);
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
│   │                                   AppendFundingSample(_fundingHistory, nowTs, rate);
│   │                                   calls CalcFundingMomentum → r.FundingMomentum;
│   │                                   appends OFI to _ofiHistory;
│   │                                   calls CalcOFIMomentum → r.OFIMomentum;
│   │                                   computes SpreadBps from order book;
│   │                                   calls CalcSwingPivots (5m + 15m);
│   │                                   computes SwingTarget/Stop bookkeeping
│   ├── MainForm_PlaintextSnapshot.vb   [P5b] BuildPlaintextSnapshot() — the engine's
│   │                                   ONLY text renderer (replaced the deleted
│   │                                   MainForm_Render_Header.vb /
│   │                                   MainForm_Render_Sections.vb): verdict header
│   │                                   block (VERDICT / CONTEXT / SCORE / TIME /
│   │                                   LAST TRANSACTED PRICE / HOLD \ EXIT /
│   │                                   ATR ENTRY LEVELS / structural rows / KELLY
│   │                                   SIZING) + all indicator sections + signal
│   │                                   breakdown. Feeds the output dump; its inline
│   │                                   CalcKellySizing call (the sole surviving
│   │                                   invocation) populates v.Kelly* BEFORE the
│   │                                   card binds.
│   ├── MainForm_Render_Cards.vb        [P5b] card-based UI render — BindCard*
│   │                                   bindings (score, verdict, last price, ATR
│   │                                   levels, structural, breakdown, OI×CVD, MTF,
│   │                                   Kelly, …). The card is the SECOND rendered
│   │                                   surface; the display-string parity rule holds
│   │                                   it in lockstep with the plaintext snapshot.
│   ├── MainForm_SignalBridge.vb        [Signal Bridge v1] Thin WinForms glue: the two
│   │                                   RunAnalysisAsync emission call sites (success →
│   │                                   full payload AFTER snapshot + card binds; skip →
│   │                                   reduced SKIPPED payload), both try/catch-hardened
│   │                                   (never throw into the run), gated on
│   │                                   signal_bridge.enabled. Owns the ARM AUTOTRADE
│   │                                   checkbox state (runtime-only, default OFF every
│   │                                   start, never persisted — dual-arm interlock D7;
│   │                                   emitted as engine.autotrade_armed, emission
│   │                                   unconditional on arming).
│   ├── OutputDumpSettingsForm.vb       Non-modal dialog: Enabled toggle, max-runs
│   │                                   textbox, file path + size, Clear + Save + Close.
│   │                                   Save routes through SettingsLoader.Save.
│   ├── WhatIfLauncherForm.vb           [offline-whatif-replay W7] Non-modal launcher —
│   │                                   whitelisted-knob grid (value-or-sweep), constraint
│   │                                   field, span, Run. Writes overlay JSON + Process.Start's
│   │                                   tools/WhatIfRunner, opens the report in AnalysisReportForm.
│   │                                   A launcher only — zero replay logic, no tools-project ref.
│   └── MainForm_Calibration.vb         BuildCalibrationReport() + calibration link
│                                       handlers (UpdateLogInfo lives in
│                                       MainForm_Layout.vb).
│
├── analysis/                          Host-agnostic offline analysis (Bundle 1).
│                                       NO System.Windows.Forms references except
│                                       AnalysisReportForm (thin viewer).
│                                       AnalysisRunner, ForwardReturnJoiner,
│                                       FailureRateMatrix, FundingMomentumDiagnostic,
│                                       OutlierAudit, MarkdownReportWriter,
│                                       AnalysisReport, AnalysisConstants.
│                                       Reusable from future Linux CLI port.
│                                       Report is segmented per (session ×
│                                       resolution) — AnalysisRunner partitions rows
│                                       into NY×1 / LONDON×3 / ASIA×3 populations and
│                                       runs FailureRateMatrix.Compute (byte-unchanged)
│                                       once each; MarkdownReportWriter renders
│                                       tier-major (offline-analysis-report-audit-proposal.md).
│
├── tools/
│   ├── AutoTweaker/                    Host-agnostic console app (Bundle 2).
│   │                                   AutoTweaker.vbproj — separate .NET 8 project.
│   │                                   Zero WinForms references. Runs unmodified
│   │                                   on Linux via `dotnet AutoTweaker.dll`.
│   │                                   AutoTweakerProgram, AutoTweakerCore,
│   │                                   PromptBuilder, ClaudeApiClient,
│   │                                   SettingsDiffApplier, TweakerConfig, TweakerState.
│   └── WhatIfRunner/                   Offline What-If replay runner (analysis-only;
│                                       zero scoring impact, never writes settings.json).
│                                       WhatIfRunner.vbproj — separate .NET 8, zero WinForms.
│                                       Links the SHIPPED SignalEmitter.ComputeSideLevels +
│                                       FailureRateMatrix (one seam, no copies): applies a
│                                       whitelisted settings overlay, re-derives placed levels
│                                       + verdict tier per logged CSV row, re-walks 1m-OHLC
│                                       outcomes, prints baseline-vs-overlay + EV-in-ATR grid
│                                       ranking with split-half validation. WhatIfOverlay,
│                                       WhatIfSettings, WhatIfReplay, WhatIfReport, WhatIfProgram.
│                                       docs/offline-whatif-replay-proposal.md.
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
        └─► DeribitClient.GetRecentTradesAsync(500)      → recentTrades   (List or Nothing,
                                                            chronological ASCENDING — see contract note above)
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
        ├─ IndicatorEngine.AppendFundingSample(_fundingHistory, nowTs, fundingRate)
        │                         [v53] every run, no dedup; evict age > 30 min
        ├─ r.FundingMomentum    = CalcFundingMomentum(_fundingHistory, nowTs, cfg)
        │                         → RISING / FALLING / FLAT (delta vs the newest
        │                           sample ≥ momentum_window_minutes old; no
        │                           anchor ⇒ FLAT)
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
        ├─ r.MTFGatePassLong/Short/Details = CalcMTFGate(candles15m)  [cached; refreshed by TTL]
        │                         direction-independent per-side flags; the final
        │                         display reason is composed at scoring Step 4b
        │                         (VerdictResult.MTFGateReason / MTFGateBlocked)
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
                    │          (penalty arms cover [0, mid) and [mid, high) —
                    │           the grace-bar ADX < 20 case gets the full penalty)
                    ├─ Step 4b: MTF gate veto (direction-aware) — dominant side
                    │          determined once from effective scores (tie → NONE);
                    │          the matching per-side flag is consulted; BLOCK
                    │          forces NO TRADE; final reason composed here
                    ├─ Step 5:  Dominant-side tier walk → verdict string
                    │          (only the dominant side's tiers are checked;
                    │           ties and below-weak dominants → NO TRADE)
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
                    └─ Step 5b [v51 B4b]: placed-level arbitration.
                               structural_levels.enabled (default) → delegates to
                               SignalEmitter.ComputeSideLevels: target ladder
                               swing → nearest HVN → POC (HVN-gated) → session-
                               resolved ATR fallback (first tier with
                               0 < dist ≤ 3.5×ATR places — structure wins even
                               when FARTHER than the ATR level); stop =
                               min(structural swing stop, 1.6×ATR) ≥ 4-tick floor
                               (SWING_STOP / STOP_CLAMPED / FALLBACK_ATR).
                               Outputs copied onto AdjustedLongTarget /
                               AdjustedShortTarget + TargetCapReason*
                               ("PLACED @ p (LABEL)"); Step 5c (the v35 min-move
                               gate) evaluates the PLACED target.
                               enabled:false → the legacy 3-tier closest-wins cap,
                               byte-identical v50 (the rollback).

                    (Kelly sizing is NOT invoked here. CalcKellySizing() is called
                     inline from BuildPlaintextSnapshot() — the sole surviving
                     invocation, which must run BEFORE the card binds so
                     BindCardKelly reads populated v.Kelly* fields.
                     See docs/kelly-criterion-proposal.md.)
                    │
                    ▼
        VerdictResult  v
                    │
                    ▼
        [P5b render — two surfaces held in lockstep by the display-string parity rule]

        UI/MainForm_PlaintextSnapshot.vb :: BuildPlaintextSnapshot(v, r, norms, cfg, …)
                    │  (the ONLY text renderer; runs FIRST — its inline
                    │   CalcKellySizing call populates v.Kelly*)
                    ├─ Verdict header + CONTEXT + score
                    ├─ HOLD \ EXIT guidance (suppressed when posState = None)
                    ├─ ATR entry / stop / target block
                    │          3-tier-capped target with tier label
                    ├─ Long + Short structural rows (swing pivot R:R)
                    ├─ KELLY SIZING block
                    │          Contracts / USD risk / [LEV CAPPED] tag.
                    │          EST mode only; suppressed when KellyF ≤ 0
                    ├─ DYNAMIC NORMS / REGIME / CORE SIGNALS / VWAP /
                    │  BBW/TTM / EMA RIBBON / MARKET STRUCTURE /
                    │  OI / ORDER FLOW / LIQUIDATIONS / MTF GATE / FUNDING
                    └─ Signal breakdown table
                    │  (string feeds the output dump)
                    ▼
        UI/MainForm_Render_Cards.vb :: BindCard*(…)
                    │  (BindCardScore / Verdict / LastPrice / AtrLevels /
                    │   Structural×2 / SignalBreakdown / OiCvdCross / MTF /
                    │   Kelly / … — every snapshot line has a card binding)
                    ▼
        AnalysisLogger.LogRun(r, verdict) → analysis_log.csv
        (in code LogRun runs just BEFORE the snapshot build — shown last here
         for readability; the CSV row does not depend on either render surface)
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
| Funding momentum as adjunct (Step 3b) | Absolute funding rate alone misses the *direction of crowding*. A rate already at +0.03% but falling is less dangerous than one at +0.02% and rising fast. Step 3b amplifies or softens the Step 3 penalty based on momentum direction, reading the momentum state off the time-anchored window held in `_fundingHistory`. Display-only impact on the funding UI row; scoring impact is bounded by the amplify/soften cfg values. |
| Funding momentum window anchored in **time**, not sample count (v53) | The original window was 3 funding *changes*. On the WS feed funding changes on ~96.5% of runs, so the window's wall-clock span was ≈ 3 × run cadence — the same funding path produced different states at different cadences. At the collector's on-close cadence this stopped being a corner case and became the operating mode: 60s NY runs gave FLAT 52.1% / Step-3b engagement 27.6% (bands: 60–70% / 15–25%), and on 180s Asia/London runs a *single* 3-min step (p50 6.5e-7) already exceeded the whole-window threshold — Step 3b moved scores on 95.8% of London rows, making res-3 `FundingMomentum` uninformative and violating the adjunct invariant by arithmetic. No per-cadence threshold could fix it: the backstop timer, feed gaps and session hand-offs all move the effective cadence *within* a session. The anchored window means "funding moved more than T over ≥ W minutes" — identical at every cadence. **Anchor = the newest sample ≥ W old, not the oldest in the ring**: the oldest would re-import cadence dependence through the ring's span. W=5 min is the knee (≥ 1 full bar at both execution resolutions, ≥ 2 samples at every cadence the engine has run, front edge of the 2–15 min hold horizon); 5-min anchored deltas run *smaller* than the old ~90s count-window deltas (p50 3.0e-8 vs 8.0e-8) because the funding premium oscillates at short horizons and partially cancels, so the anchor reads sustained drift rather than wiggle. See `docs/funding-momentum-time-anchored-window-proposal.md`. |
| _fundingHistory age-evicted at 30 min, no count cap (v53) | Eviction horizon = the audit's segment-reset horizon; ≤ 60 entries at the fastest cadence the engine has run, so the ring stays small without a count cap. The retired `FundingHistoryMax=10` cap is not merely unnecessary but actively wrong under age-anchoring: at a 30s cadence 10 samples span only 5 minutes, so the cap would evict the very samples the W=5min anchor needs and pin the state at FLAT. The pre-v53 `[S9]` append-on-change dedup went the same way — it existed because identical samples filled a *count*-indexed ring and forced FLAT; an age-anchored ring wants them, since "funding hasn't moved in W minutes" genuinely *is* FLAT. |
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

---

## Display Behaviour Clarifications

Notes on rendering behaviours that have surfaced in audits as potentially-buggy but are intentional. Documented here so future audits (and future Claude conversations) don't re-flag them.

| Behaviour | Status | Rationale |
|---|---|---|
| `HOLD \ EXIT:` row absent from rendered output and output dump when no position is held | By design | `BuildPlaintextSnapshot` (`UI/MainForm_PlaintextSnapshot.vb:136`) guards on `If v.HoldStatus <> "N/A -- no open position" Then` (the card binding mirrors the same sentinel check). `CalcHoldStatus` returns the `"N/A -- no open position"` sentinel when `posState.IsNone`. The whole hold-guidance block (label + value) is suppressed in that case — there's no position to guide. The rendered label is `HOLD \ EXIT` and only when a position has been declared via the radio buttons. |
| POC tier 3 of the target cap never fires in practice | By design + geometry | The Step 5b cap arbitration considers POC only when `hvnAbove` / `hvnBelow` is True (VPFR signal flags the engine is in HVN proximity). When the gate is open, POC must additionally be closer to entry than both the swing target AND the nearest HVN. The combined conditions are narrow enough that POC almost never wins by geometry alone. The branch is reachable, not dead code. POC tier is a **refinement** of the HVN tier, not a general fallback for the "no swing, no HVN" case. |
| `STRONG LONG` / `STRONG SHORT` co-existing with `STRUCTURALLY_WEAK` / `MOMENTUM_FADING` context tags | Intentional | `VerdictContext` is display-only (Step 5b post). It surfaces structural caveats the score didn't fold in. A STRONG-tier score with a warning tag legitimately means "score qualifies for the strong tier, but the structural picture is thin / momentum is fading." This is informative, not contradictory — the trader should read the context tag before sizing. Suppressing warnings on STRONG verdicts would remove the very signal the tag was added to surface. |
| MTF Reason rendered in three formats (`MTF PASS [DIR]`, `MTF BLOCK [DIR vs TREND]`, `MTF state: TREND \| details`) | By design | Three scenarios, three formats. Since v31 the string is composed at scoring Step 4b against the **dominant side** and stored on `VerdictResult.MTFGateReason` — every consumer (MTF card, plaintext snapshot, CSV, breakdown row) renders that one string. `MTF PASS [DIR]` when a directional verdict is in play and the gate clears; `MTF BLOCK [DIR vs TREND]` when it fails; `MTF state: TREND \| details` when no directional verdict is in play. The leading-keyword inconsistency is a deliberate signal of the no-direction case — unifying would lose that distinction. |
| `MTF BLOCK [...]` reason string still composed when `mtf_gate.enabled: false` while no block occurs | By design (config-edge display quirk, v47 N3) | With `mtf_gate.enabled: false` (non-default), a failing gate still composes its reason as `MTF BLOCK [DIR vs TREND]` — Step 4b (`ScoringEngine_Calculate_Verdict.vb` ~:110) doesn't consult `Enabled` when formatting, only when deciding whether to veto. So the display can read BLOCK while the verdict proceeds. Unreachable at current config (`enabled: true`), and the auto-tweaker can never create the state (`mtf_gate.enabled` is in `DisabledGatedPaths`). The three MTF reason formats are locked by design — do not change the code. |
| `[B]` / `[T]` mode indicator absent from rendered output dump (pre-v30) | Was a spec gap (fixed v30) | `AnalysisOutputDump.Append` originally captured only `txtOutput.Text` (the RTF content). The perf-strip is separate WinForms `Label` controls outside the RTF, so the mode indicator and the six rate labels weren't captured. v30's display-polish pass adds a `PERF STRIP` header line to each dump block. Dumps from pre-v30 won't have this line. |

