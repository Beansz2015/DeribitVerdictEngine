# DeribitVerdictEngine — Architecture Reference
**Last updated: 2026-07-21 (offline matrix placed-target migration — offline-analysis semantics only, no settings bump) | App version: settings.json v54 — #6 book-absorption build (level-scoped dual-fed episode tracker, display/CSV only, `scoring_enabled:false`) on top of v53 funding time-anchored window, v52 #5 TFI-modifier wire-in, v51 B4b placed-geometry structural-first levels (ONE arbitration seam `SignalEmitter.ComputeSideLevels`; Step 5b delegates; FOUR parity surfaces: snapshot ↔ cards ↔ `verdict_signal.json` ↔ CSV `Placed*`), v50 #5 aggressor-velocity + retune bundle (CSV v0.8), v49 signal-bridge emitter, v42 WebSocket cutover. `settings.json` line 1 is the version source of truth.**

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
│                                       ring, top-10 ladder, ticker (funding_8h/OI/mark). Also
│                                       owns the OFI + aggressor-velocity accumulators and
│                                       [v54 #6] the dual-fed LevelAbsorptionTracker (folds,
│                                       reads, resets all under the same lock).
├── DeribitWsFeed.vb                    [WS-P1] One public ClientWebSocket — REST-seed →
│                                       [v64] ApplyTrades also buffers every streamed trade
│                                       into the trade store via TradeStoreWriter (a WRITE,
│                                       not a fetch — the stream is already in hand); D2 dual
│                                       flush trigger (count checked per batch, a
│                                       System.Threading.Timer covering the quiet-hour case
│                                       a per-batch check cannot); SeedAsync FLUSHES then
│                                       un-seeds the monotonic guard so the REST re-seed is
│                                       idempotent; Stop() flushes the tail.
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
├── TradeStoreGapRepair.vb              [v64] In-app trade-store gap repair (§1.2 / D5) — the
│                                       SECONDARY capture mechanism and, under D1's AWS-only
│                                       ruling, the ONLY recovery mechanism. Plain
│                                       System.Threading.Timer: one pass IMMEDIATELY on start
│                                       (§7.1 — a restart is precisely when a gap exists),
│                                       then every gap_repair_interval_hours over a
│                                       gap_repair_lookback_hours window. Calls
│                                       HistoricalStore.BackfillTradeMonthAsync with the
│                                       exe-resolved store dir and clampToSegStart:=True (the
│                                       clamp keeps the fetch inside Deribit's ~24h trade
│                                       retention). Started INDEPENDENTLY of transport —
│                                       transport="rest" has no stream, so repair alone
│                                       carries the store. Host-agnostic, never throws.
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
│   │                                   (v53 time-anchored window + ring eviction),
│   │                                   ClassifyAbsorption (v54 #6 — pure classifier
│   │                                   over the LevelAbsorptionTracker read)
│   ├── TradeStoreWriter.vb             [v64] The ONE trade-store seam — host-agnostic and
│   │                                   deliberately NETWORK-FREE (that split is why the
│   │                                   app's feed path and the fixture project never link
│   │                                   HistoricalStore's HttpClient). Owns file naming,
│   │                                   monthly rollover, buffered append + monotonic guard,
│   │                                   the row FORMAT and the row PARSE, LastTradeTimestamp,
│   │                                   ResolveStoreDir (exe-relative, D3/A48h) and
│   │                                   ResolveResumeCursorMs (the by-construction overlap
│   │                                   no-op, A48d). Three consumers: DeribitWsFeed's
│   │                                   streaming capture, HistoricalStore's network backfill,
│   │                                   and LoadTradeRange's per-file read — so writer and
│   │                                   reader cannot drift. One process-wide append lock
│   │                                   (streaming + repair append to the same file). Never
│   │                                   throws. Fixtures A48a–h.
│   ├── LevelAbsorptionTracker.vb       [P4 #6 v54] Level-scoped absorption episode
│   │                                   tracker (book-absorption-proposal.md §4) —
│   │                                   the first DUAL-FED tracker: owned by
│   │                                   MarketState under its ONE lock, folded from
│   │                                   BOTH the ~100ms book snapshots (proximity
│   │                                   gate on the nearest CARRIED level, band-size
│   │                                   trajectory, D8 ΔSize=Posts−Pulls−Fills
│   │                                   conservation w/ visibility mask) AND the
│   │                                   trades stream (rolling pressing USD, band
│   │                                   fills, break-through test). absorbRatio =
│   │                                   pressing USD per USD net band depletion;
│   │                                   pullFrac = provable pulls / provable posts
│   │                                   (spoof veto). Reset on SeedAsync. Display/
│   │                                   CSV only at the build (scoring_enabled:false).
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
│                                       runs FailureRateMatrix.Compute once each;
│                                       MarkdownReportWriter renders tier-major
│                                       (offline-analysis-report-audit-proposal.md).
│                                       [placed-target migration 2026-07-21] BOTH eval
│                                       barriers are the row's logged placed geometry:
│                                       adverse = PlacedStop* (D6), favourable =
│                                       PlacedTarget* (ResolveFavourableBarrier, the
│                                       mirror of ResolveAdverseBarrier). The per-tier
│                                       ATR grid retired, so the cell space is
│                                       (tier × window) — one placed-geometry cell.
│                                       Pre-v0.8 rows keep the legacy formula on both
│                                       sides in a LEGACY_YARDSTICK population.
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
        ├─ r.Absorption*        = [v54 #6, WS-live only] MarketState.GetAbsorption →
        │                         IndicatorEngine.ClassifyAbsorption (session-resolved
        │                         min_aggr_usd via ExecutionResolution) → ABSORB_ABOVE /
        │                         ABSORB_BELOW / NONE + episode numerics (pullFrac logs
        │                         even on D8-vetoed episodes). REST/fallback/cold/no
        │                         episode ⇒ NONE + nulls. Display/CSV only at the build.
        │                         Carried levels re-set post-run from this run's swing/HVN
        │                         fields (the strip's carry) via SetAbsorptionLevels.
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
| Offline eval barriers = the placed geometry, both sides (2026-07-21) | D6 moved the adverse barrier onto the logged `PlacedStop*` but left the favourable side on a synthetic per-tier ATR grid, making the offline matrix the lone hybrid while the live tracker, the D4 re-walk and the what-if runner all measured placed-vs-placed. The grid was also degenerate by then: anchored at ATR≈115, it put every multiple below the min-move floor at ATR≈44, so all threshold columns collapsed onto one floored barrier and differed only in label. Moving the favourable side onto `PlacedTarget*` makes the whole eval stack measure one thing — what the engine emitted and the autotrader executes — and removes an axis that had stopped carrying information. The **window** dimension survives because the hold-horizon question is geometry-independent. The placed target is used **unfloored**: the live Step 5c gate already evaluated that exact price, so re-flooring it offline would re-create the collapse; the floor still binds on the pre-v0.8 legacy fallback, which nothing upstream vetted. Same reasoning makes the v35 min-move EXCLUDE test exact on v0.8+ rows (`\|PlacedTarget − entry\|`) — the `engineTargetMult × ATR` approximation existed only because the CSV lacked the value, and using it on placed rows would drop precisely the low-ATR rows the migration makes readable. Threshold sweeping is not lost, it moves to the what-if runner, which does it with EV and a split-half holdout instead of a standing two-column report axis. `offline-matrix-placed-target-proposal.md`. |
| Level absorption as a dual-fed, level-scoped episode tracker (v54 #6) | Absorption is the only signal reading the *interaction* of flow with resting liquidity, and needs both streams at native cadence: the trades stream says how hard the flow hits the level band, the ~100 ms book snapshots say whether the band's resting size dies or replenishes. Folding both into one `LevelAbsorptionTracker` under MarketState's single lock (the OFI/AggrVel fold discipline) keeps the two feeds consistent without a second synchronisation primitive. Episodes are proximity-gated on the nearest CARRIED level (the strip's candidate set — carried, never recomputed, so the tracker adds no level machinery) and reset on break-through/leave/re-map, so a stale ABSORB can never outlive the structure that justified it. The D8 conservation accounting (`ΔSize = Posts − Pulls − Fills`, masked to the band portion visible in both consecutive top-10 snapshots) turns unfakeable fills into a hard lower bound on pulled-without-filled volume — the spoof signature — without incremental-book plumbing; where sub-interval flicker evades it, the signal degrades to the pre-D8 baseline, never below. Build is display/CSV only; activation is evidence-gated twice (independence AND an outcome gradient) per the proposal §5. |
| Best-pivot as a testable TARGET candidate, not a live level (v63) | The volume-weighted best pivot has shipped display-only since v24 (D2); P1 parks live promotion behind evidence that pivot placement earns its slot alongside swing/HVN/POC. Until this build the evidence could not be produced because `ComputeSideLevels` did not read `BestPivot*` — a what-if study run under the flag would have measured the geometry it was *not* trying to test. `scoring.structural_levels.use_best_pivot_candidate` (Boolean, default `false`) inserts the pivot into the target candidate set with the same rules every other tier follows: side by **price-vs-entry** (D3, the one rule live and replay can share — CSV logs the price and volume-ratio but not `IsHigh`; a LOW pivot above entry is still a defended level), same looseness bound (`target_max_atr_mult × ATR`), zero pivot ⇒ candidate absent (counted, not guessed — the POC-tier precedent). Ladder mode makes it the FIRST tier above swing (P1 verbatim); NEAREST mode drops it into the distance race with no priority. STOP side untouched — D2 was always a target idea, DG1 stays. At default `false` every surface (snapshot, card, payload, CSV) is byte-identical, so this build is a what-if instrument and NOT a dataset boundary; live-enable is a later ⚠ D-table with the P1 promotion conditions. `docs/d2v2-whatif-candidate-mode-proposal.md`. |
| Min-move floor composed from a fee model, not a flat literal (v62) | The v35 floor `scoring.min_tradeable_move_pct = 0.0008` was sized "to clear slippage" under **zero-maker-fee** execution. Deribit's 2026-08-01 schedule (maker 1.5 bps / taker 3.5 bps) removes that basis, so the number had to be re-derived — and a single literal cannot say *which* half moved when it changes. The floor is now `EffectiveMinMovePct = round_trip_fee_pct(style) + min_net_move_pct`: the first term is a **venue fact** (edited when Deribit edits it), the second a **trader risk preference** (UI-adjustable, hot-reloadable). Deribit perp fees are proportional to notional, so cost and move share the unit "% of price" and the engine never needs to know trade size; ATR enters nowhere, because fees don't scale with volatility. `maker_maker` is the right default rather than an optimistic one: the floor gates the **target** side — the profit path, which is maker entry + maker TP in this trader's flow. Taker only appears on emergency stop repositioning and rare manual exits, i.e. the **loss** path, which this floor deliberately does not price (loss-side cost belongs to sizing/EV analytics and the order app). At defaults the composition reproduces 0.0008 exactly, so the restructure ships behaviour-neutral and is not a dataset boundary; a later knob turn is an ordinary live floor change the v35 eval machinery already re-walks attributably. One shared resolver (`TradeCostSettings.EffectiveMinMovePct`) serves the live gate, the eval cache, the offline matrix, the ceiling audit and the what-if replay, so measurement and behaviour cannot drift. `docs/fee-aware-min-move-proposal.md`. |
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
| Trade capture in the app, as two redundant mechanisms (v64) | Deribit's public trades endpoint serves ≈24 h and refuses older windows; candles have no such cap. Trades are therefore the one input the backtester cannot synthesise around, and trade-derived signals (CVD, MicroCVD, TFI, aggressor velocity, liquidations) can only be **re-derived under different settings** from raw ticks — `analysis_log.csv` stores their outputs, which keeps the answer and discards the question, so it cannot substitute. With a vendor feed declined on cost, append-forward is the only path, which makes its reliability the whole question. The app is the right host because it is already up 24/7, already watched daily, and **already receives every trade** — appending the stream is a write, not a fetch, so capture needs no API call and has no 24-hour deadline. The external-scheduled-task alternative had three failure modes that were each silent and unrecoverable past 24 h. Streaming and gap repair are deliberately NOT collapsed into one mechanism: streaming is complete while the app runs and recovers nothing from downtime; repair recovers downtime but, alone, reinstates the 24-hour deadline. The monotonic last-written guard is what makes them compose — `SeedAsync` re-seeds the trade ring from REST on every (re)connect, so duplicates are guaranteed by design, and the guard plus the reader's whole-row dedup make overlap a no-op at both write and read time. `HistoricalStore` was split rather than moved because it owns a live `HttpClient`: the format/rollover/guard half (`Core/TradeStoreWriter.vb`) links everywhere including the fixture project, the network half stays in `tools/BacktestRunner/` and is linked by the app for repair only. Store path resolves against the **exe directory**, never the cwd — the app's cwd is not guaranteed and a cwd-relative store would silently scatter capture files. `docs/in-app-trade-store-capture-proposal.md`. |
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

