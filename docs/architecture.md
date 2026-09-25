# DeribitVerdictEngine — Architecture Reference
**This header deliberately carries NO version number.** It used to, and it went **eleven versions stale** (it read "App version: settings.json v54" while the tree was on v65) — a seat reading top-down inherited that as fact. **For the live version, read the tracked repo-root `settings.json` line 2**; for what each version changed, read `DeribitIndicatorProject.md` §15 (most recent five) and [`history-archive.md`](history-archive.md) §E (everything older).

> **Generalised from the same lesson, 2026-08-02:** a doc header must not carry a number that lives somewhere else. This is the doc-side form of the engine-side rule that display strings carry no measured values or dates — both fail the same way, silently and in the place a reader trusts most. Structural facts belong here; *current* facts belong at their source, with a pointer.

> **Trimmed 2026-09-14 (UTC).** Superseded and history text in this file moved verbatim to [`architecture-archive.md`](architecture-archive.md) §A. Every moved block leaves a pointer carrying its `trim-2026-09-14-NN` id. Ledger: [`doc-trim-log.md`](doc-trim-log.md). Pre-trim original: git tag `doc-trim-2026-09-14-pre`; byte copy under `docs/archive/doc-trim-2026-09-14/originals/`.

> **Second trim pass 2026-09-14 (UTC):** per-file build narratives in the Directory Layout moved verbatim to [`architecture-archive.md`](architecture-archive.md) §B, ids `trim-2026-09-14b-NN`, same ledger. Pre-pass original: git tag `doc-trim-2026-09-14b-pre`.

> **Trade-stream contract (v31).** `DeribitClient.GetRecentTradesAsync` returns trades in **chronological ascending** order (oldest first, most recent last) — the HTTP request keeps `sorting=desc` to guarantee the latest trades, and the parsed list is reversed before return. Window-consuming indicators (TFI, MicroCVD) take their window from the **end** of the list via `IndicatorEngine.LastN`; `Take(n)` on a trade list selects the OLDEST n and is a bug. CalcCVD's positional thirds (early/mid/late) are chronologically truthful under this contract.

> **Auto-tweaker windowing (v29).** `tools/AutoTweaker/AutoTweakerCore.RunAsync` reads `TweakerConfig.WindowMode`. In `fixed` mode (default), a "round" is a disjoint slice `allRows[LastEvaluatedRowIndex .. +WindowSize-1]`; the index advances by exactly `WindowSize` after every completed terminal branch (including the new `SKIPPED_INSUFFICIENT_TIER` / `SKIPPED_SESSION_BOUNDARY` outcomes — which do **not** tick the BELOW_THRESHOLD streak). `cooldown_rows` is a no-op in fixed mode. MinTier resolves through `TweakerConfig.EffectiveMinTier(windowSize)` — null in JSON auto-scales as `max(15, ceil(WindowSize × 0.5))`. Sliding mode is retained behind the `Else` arm for legacy comparison and is documented as deprecated.

This document describes the full codebase structure, data flow, and design rationale.
Update whenever files are added, moved, or significantly changed.

---

## Directory Layout

> Each compacted entry below ends with the id of its archived original entry (`[trim-2026-09-14b-NN]`, full text in [`architecture-archive.md`](architecture-archive.md) §B). Entries marked `(added 2026-09-14)` were missing from this map; each was checked against the tree that day. This map lists the main files; for anything else, check the tree.

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
├── IMarketDataSource.vb                Transport contract for the live data calls.
│                                       RestMarketDataSource and WsMarketDataSource implement it;
│                                       RunAnalysisAsync picks one per run through ResolveSource()
│                                       (network.transport). Host-agnostic. [trim-2026-09-14b-29]
├── RestMarketDataSource.vb             [WS-P1] Pass-through to DeribitClient (the fallback path).
├── WsMarketDataSource.vb               [WS-P1] Serves the 5 shapes from MarketState; staleness-
│                                       gated (book/trades/ticker); candles defer to IsFresh.
├── MarketState.vb                      Thread-safe snapshot store fed by the WS feed: candle
│                                       series, trade ring, top-10 book, ticker. Owns the OFI and
│                                       aggressor-velocity accumulators and the
│                                       LevelAbsorptionTracker, all under one lock. [trim-2026-09-14b-30]
├── DeribitWsFeed.vb                    The live WebSocket feed (primary transport since the v42
│                                       cutover): REST seed, chart/trades/ticker/book
│                                       subscriptions, receive loop, backoff reconnect, heartbeat
│                                       replies. Buffers every streamed trade into the trade store
│                                       via TradeStoreWriter. Host-agnostic. [trim-2026-09-14b-31]
├── ShadowParityComparer.vb             With network.shadow_parity=true, diffs the WS source
│                                       against REST each run into ws_parity_log.txt. Never
│                                       touches the CSV or scoring. Host-agnostic. [trim-2026-09-14b-32]
├── TradeStoreGapRepair.vb              In-app trade-store gap repair: one pass at start, then
│                                       every gap_repair_interval_hours over
│                                       gap_repair_lookback_hours, filling trade_seq holes through
│                                       HistoricalStore. Runs on either transport. Host-agnostic,
│                                       never throws. [trim-2026-09-14b-33]
├── MtfRefreshPolicy.vb                 Decides whether to re-fetch 15m candles this run: always
│                                       on ws, the 60 s TTL gate on rest. Fixtures A16d/e. [trim-2026-09-14b-34]
├── DynamicNorms.vb                     Live adaptive thresholds (ATR scale, vol, VWAP dev);
│                                       now also applies session-aware volume multipliers
├── AnalysisLogger.vb                   CSV run logger + CalibrationReport
├── AutoRunTimer.vb                     IAutoRunTimer interface + WinFormsAutoRunTimer impl
├── OiSnapshot.vb                       OI ring-buffer snapshot struct
├── LivePerformanceTracker.vb           Live perf strip: the eval cache (analysis_eval_cache.csv)
│                                       and window aggregates; resolves PENDING rows as windows
│                                       complete. Host-agnostic. (added 2026-09-14)
├── OhlcCache.vb                        Rolling 7-day on-disk 1m OHLC cache. Host-agnostic. (added
│                                       2026-09-14)
├── LiveMicrostructureEvaluator.vb      Live TAPE strip evaluator on a MarketState snapshot;
│                                       display only, never a verdict. (added 2026-09-14)
├── ExitGuardEvaluator.vb               Realtime exit guard: fast microstructure exit checks on a
│                                       live snapshot; display and alert only. (added 2026-09-14)
├── settings.json                       All tunable parameters; live version on line 2; hot-
│                                       reloaded, no recompile. [trim-2026-09-14b-35]
│
├── Core/
│   ├── Settings/
│   │   ├── EngineSettings.vb           Strongly-typed POCO contract for settings.json, one class
│   │   │                               per settings block. JSON-to-POCO drift guard: fixtures
│   │   │                               A62a-A62g. [trim-2026-09-14b-36]
│   │   └── SettingsLoader.vb           Loader: SettingsLoader.Current singleton,
│   │                                   FileSystemWatcher hot-reload, Save(... bumpVersion).
│   │                                   Merges the gitignored per-box settings.local.json over the
│   │                                   tracked base through an allow-list
│   │                                   (AdmittedBlocks/AdmittedKeys); Save() writes the base,
│   │                                   never the merge. Fixtures A50a-j. [trim-2026-09-14b-37]
│   │
│   ├── ExecutionResolution.vb          Session-conditional execution resolver:
│   │                                   MatchSessionBucket, ResolveResolution, per-session ROC
│   │                                   magnitude and slope resolution. Host-agnostic. [trim-2026-09-14b-38]
│   │
│   ├── ProcessIdentity.vb              instance_id GUID per process start and signal_id per
│   │                                   completed run; used by SignalEmitter and the CSV
│   │                                   InstanceId/SignalId columns. Host-agnostic. [trim-2026-09-14b-39]
│   ├── SignalEmitter.vb                verdict_signal.json emitter for the order app (schema v1):
│   │                                   BuildOk/BuildSkipped, DeriveDirection, DeriveWsHealth,
│   │                                   atomic TryWrite. ComputeSideLevels is the one structural-
│   │                                   first placed-level seam for Step 5b, snapshot, card,
│   │                                   payload and CSV. Host-agnostic; fixtures A22. [trim-2026-09-14b-40]
│   ├── ScoringEngine_Types.vb          Result types and enums: VerdictResult,
│   │                                   SignalBreakdownItem, PositionState, SignalCategory,
│   │                                   ScoreState. [trim-2026-09-14b-41]
│   ├── ScoringEngine_Helpers.vb        Pure helpers: RegimeMaxScore, Threshold, TierFloor,
│   │                                   AddFull, HasCrossConfirm, BuildNote, CalcHoldStatus
│   │                                   (layered exit guidance). [trim-2026-09-14b-42]
│   ├── ScoringEngine_Calculate_Scoring.vb
│   │                                   RunScoringPipeline (Step 2, Pass 2/2b/2c, Steps 3/3b),
│   │                                   CalcVerdictContext, breakdown notes. [trim-2026-09-14b-43]
│   ├── ScoringEngine_Calculate_Verdict.vb
│   │                                   Calculate() entry: Step 4 regime veto, Step 4b MTF veto,
│   │                                   Step 5 verdict, Step 5b placed levels via
│   │                                   SignalEmitter.ComputeSideLevels, Step 5c min-move gate,
│   │                                   VerdictContext. [trim-2026-09-14b-44]
│   ├── ScoringEngine_Kelly.vb          CalcKellySizing(): display-only Kelly sizing, called
│   │                                   inline from BuildPlaintextSnapshot. Zero scoring impact.
│   │                                   [trim-2026-09-14b-45]
│   │
│   ├── IndicatorResults.vb             IndicatorResults: every indicator output field one run
│   │                                   produces. [trim-2026-09-14b-46]
│   ├── Indicators_Momentum.vb          CalcDMI, CalcATR, CalcEMA, CalcRSI,
│   │                                   CalcRSISeries, CalcRSIDivergence, CalcROCSeries,
│   │                                   CalcVolumeSMA
│   ├── Indicators_Volatility.vb        CalcVWAP (dual-session anchor), CalcVWAPBands, CalcBBW,
│   │                                   CalcTTMSqueeze; parameters passed from cfg by name. [trim-2026-09-14b-47]
│   ├── Indicators_OrderFlow.vb         CalcOFI, CalcOFIMomentum, CalcCVD, CalcMicroCVD, CalcTFI,
│   │                                   CalcLiquidations, CalcFundingMomentum +
│   │                                   AppendFundingSample (time-anchored), ClassifyAbsorption,
│   │                                   CalcSpreadBps + ClassifySpread + ApplySpread. [trim-2026-09-14b-48]
│   ├── TradeStoreWriter.vb             The one network-free trade-store seam: file naming,
│   │                                   monthly rollover, buffered append behind the identity-
│   │                                   keyed write guard, row format and parse, DedupTrades,
│   │                                   ResolveRepairWindowsMs. Used by DeribitWsFeed,
│   │                                   HistoricalStore and LoadTradeRange. Never throws. [trim-2026-09-14b-49]
│   ├── LevelAbsorptionTracker.vb       Level-scoped absorption episode tracker fed by both book
│   │                                   snapshots and trades under MarketState's lock; produces
│   │                                   absorbRatio and pullFrac. Display/CSV only
│   │                                   (scoring_enabled false). [trim-2026-09-14b-50]
│   ├── AggressorVelocityAccumulator.vb
│   │                                   Time-decayed taker USD sums at a fast and a norm horizon
│   │                                   (aggressor velocity). (added 2026-09-14)
│   ├── AlertsTracker.vb                Liquidation-cascade alarm and level-approach alerts;
│   │                                   display and alert only. (added 2026-09-14)
│   ├── BarCloseDetector.vb             Detects an execution-resolution bar close for on_close
│   │                                   firing. (added 2026-09-14)
│   ├── CaptureMarkerLog.vb             Per-process capture-scope marker read by the coverage
│   │                                   report. (added 2026-09-14)
│   ├── OfiAccumulator.vb               Time-averaged OFI, fed by each streaming book update.
│   │                                   (added 2026-09-14)
│   ├── StoreFiles.vb                   Network-free file layer for the candle and funding halves
│   │                                   of the backtest store. (added 2026-09-14)
│   ├── VenueStatusLog.vb               Transition-only venue_status.log: venue 5xx and JSON-RPC
│   │                                   errors, VENUE_OK on recovery. (added 2026-09-14)
│   ├── WsHealthLog.vb                  Transition-only ws_health.log of the OK/DEGRADED/DOWN/REST
│   │                                   state. (added 2026-09-14)
│   └── Indicators_Structure.vb         CalcDonchian (quartilePct from cfg),
│                                       CalcOBV,
│                                       CalcVPFRLite v2 (VAH/VAL + nearest HVN/LVN,
│                                       exponential decay weighting),
│                                       CalcSwingPivots (5m + 15m confirmed pivot scan),
│                                       CalcMTFGate
│
├── UI/
│   ├── MainForm_Layout.vb              Constructor and layout; shared form fields and run-state
│   │                                   history (_oiHistory, _fundingHistory, _ofiHistory, 15m MTF
│   │                                   cache, _prevRegime); status bar and perf strip. [trim-2026-09-14b-51]
│   ├── MainForm_AutoRun.vb             Auto-run timer: InitAutoRunControls(),
│   │                                   btnStartStop_Click, StartAutoRun(), StopAutoRun(),
│   │                                   RunAutoAnalysis(), OnCountdownTick(),
│   │                                   UpdateCountdownLabel()
│   ├── MainForm_Analysis.vb            RunAnalysisAsync(): fetch through ResolveSource(),
│   │                                   indicators, scoring, CSV log, renders; funding/OFI history
│   │                                   and swing bookkeeping. [trim-2026-09-14b-52]
│   ├── MainForm_PlaintextSnapshot.vb   BuildPlaintextSnapshot(): the only text renderer (header,
│   │                                   levels, Kelly, indicator sections, breakdown). Feeds the
│   │                                   output dump; runs CalcKellySizing before the card binds.
│   │                                   [trim-2026-09-14b-53]
│   ├── MainForm_Render_Cards.vb        BindCard* card bindings, the second rendered surface; the
│   │                                   display-string parity rule keeps it in step with the
│   │                                   snapshot. [trim-2026-09-14b-54]
│   ├── MainForm_SignalBridge.vb        Signal-bridge glue: success and skip emission call sites
│   │                                   (never throw into the run); runtime-only ARM AUTOTRADE
│   │                                   state (default OFF, never persisted). [trim-2026-09-14b-55]
│   ├── OutputDumpSettingsForm.vb       Non-modal dialog: Enabled toggle, max-runs
│   │                                   textbox, file path + size, Clear + Save + Close.
│   │                                   Save routes through SettingsLoader.Save.
│   ├── WhatIfLauncherForm.vb           Launcher for tools/WhatIfRunner: knob grid, span, Run;
│   │                                   opens the report. No replay logic. [trim-2026-09-14b-56]
│   ├── MainForm_ExitGuard.vb           Exit-guard host timer. (added 2026-09-14)
│   ├── MainForm_LiveStrip.vb           Live TAPE strip host timer. (added 2026-09-14)
│   ├── MainForm_TapeStoreStatus.vb     TAPE STORE status strip host timer. (added 2026-09-14)
│   ├── RoundStatsForm.vb               Last five auto-tweaker rounds with per-tier accuracy.
│   │                                   (added 2026-09-14)
│   ├── TweakSettingsForm.vb            Auto-tweaker settings and status dialog. (added
│   │                                   2026-09-14)
│   ├── Controls/                       Custom WinForms controls (cards, gauges, pills, meters).
│   │                                   (added 2026-09-14)
│   ├── Theme/                          Theme.vb palette tokens. (added 2026-09-14)
│   └── MainForm_Calibration.vb         BuildCalibrationReport() + calibration link
│                                       handlers (UpdateLogInfo lives in
│                                       MainForm_Layout.vb).
│
├── analysis/                           Host-agnostic offline analysis (only AnalysisReportForm, a
│                                       thin viewer, uses WinForms): AnalysisRunner,
│                                       ForwardWindowJoiner, FailureRateMatrix (tier x window on
│                                       placed geometry), BandLadder, FundingMomentumDiagnostic,
│                                       OutlierAudit, MarkdownReportWriter, DeribitOhlcFetcher.
│                                       The report is segmented by session x resolution; pre-v0.8
│                                       rows form the LEGACY_YARDSTICK population. [trim-2026-09-14b-57]
│
├── tools/
│   ├── AutoTweaker/                    Auto-tweaker console app (own .vbproj, zero WinForms, runs
│   │                                   on Linux): AutoTweakerCore, PromptBuilder,
│   │                                   ClaudeApiClient, SettingsDiffApplier, ConditionsExtractor,
│   │                                   TweakerConfig, TweakerState. [trim-2026-09-14b-58]
│   ├── BacktestRunner/                 Backtest CLI (own .vbproj): fetch, replay, validate,
│   │                                   report, coverage; HistoricalStore, CoverageReport. (added
│   │                                   2026-09-14)
│   ├── CeilingAudit/                   W6-4 offline ceiling-audit runner (own .vbproj). (added
│   │                                   2026-09-14)
│   ├── WsTradeProbe/                   Standalone trade-feed delivery probe; writes no collector
│   │                                   files. (added 2026-09-14)
│   ├── checks/                         verify-gate.ps1 (pre-push, CI, Stop hook), rotation-
│   │                                   riders.ps1, doc-trim-verify.ps1, hook installers. (added
│   │                                   2026-09-14)
│   ├── ops/                            collector.ps1 (SSM status, fetch, deploy), kelly-trigger-
│   │                                   read.ps1, absorption-episode-age-read.ps1. (added
│   │                                   2026-09-14)
│   ├── *.ps1                           UI automation for the running MainForm (screenshot, click,
│   │                                   inspect, resize) and build-manual-pdfs.ps1. (added
│   │                                   2026-09-14)
│   └── WhatIfRunner/                   Offline what-if replay (own .vbproj, zero WinForms, never
│                                       writes settings.json): whitelisted overlay, placed levels
│                                       re-derived through the shipped ComputeSideLevels, outcomes
│                                       re-walked, EV ranked with split-half validation. [trim-2026-09-14b-59]
│
├── verify/ordercheck/                  Fixture harness (OrderCheck.vbproj) linking the shipped
│                                       sources; run by verify-gate.ps1. (added 2026-09-14)
│
└── docs/                               ~380 docs. Session start reads
                                        DeribitIndicatorProject.md, architecture.md,
                                        trader-profile.md, trader-tick-queue.md and the
                                        current seat handover. Archives:
                                        history-archive.md, trader-tick-queue-archive.md,
                                        architecture-archive.md. Trim ledger:
                                        doc-trim-log.md (trim-2026-09-14-52 holds the old
                                        April-era docs listing).
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
                    │          [v69] p/b from the live eval cache (BOOK mode), not a
                    │          confidence tier; shown whenever a Kelly side exists —
                    │          [NO EDGE] at f* ≤ 0, a below-floor state on a thin book
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

*(⚠ This diagram's first node read `settings.json (v17)` until 2026-08-24, against a tree on v68 — **fifty-one versions stale, inside the file whose own header carries the rule against exactly this.** A version number buried in an ASCII diagram is the same defect as one in a header; it just hides better. The block list below is structural and stays.)*

```
settings.json          ← version lives in the file, line 2. Never quoted here
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
    │         Values: read settings.json session_volume.sessions. This
    │         diagram carried v30 values that went stale (trim-2026-09-14-53).
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
| ~~REST polling (not WebSocket)~~ — ⚠ **SUPERSEDED, quoted not deleted** | **Original rationale, still the reason the REST path exists:** *"Simpler implementation; adequate for 1m candle resolution. WebSocket is the highest-impact next upgrade."* ⛔ **The second sentence went stale on 2026-06-24 and was still standing on 2026-08-24 — two months describing a shipped migration as the next one, in the row a reader hits first.** WebSocket SHIPPED (P1 v38 → P2 v39 → **cutover v42, 2026-06-24**); `network.transport` is `"ws"` on the live boxes. **REST is now the FALLBACK path, not the primary** — `RestMarketDataSource` behind `IMarketDataSource`, selected by `ResolveSource()`, and the resilience row below still governs it. The next architectural ceiling, if one emerges, is the full-depth **incremental order-book channel** — see `DeribitIndicatorProject.md` §16.4, which is authoritative for that and is where the claim belongs. |
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
| ~~MainForm_Render split into _Header + _Sections~~ - SUPERSEDED | Both files were deleted in P5b. `UI/MainForm_PlaintextSnapshot.vb` is now the only text renderer and `UI/MainForm_Render_Cards.vb` is the card surface. Original row: [`architecture-archive.md` §A, `trim-2026-09-14-54`](architecture-archive.md#trim-2026-09-14-54) |
| GetSessionCandles helper extracted (v15) | `CalcVWAP` and `CalcVWAPBands` both anchored on the session-2 cutoff and re-derived the session window independently. The boundary calculation is now a single private helper in `Indicators_Volatility.vb`; both callers route through it. |
| v15 cleanup pass (historical) | A source-of-truth audit that removed dead fields, unused properties and silently-ignored config keys, with zero scoring impact. Original row: [`architecture-archive.md` §A, `trim-2026-09-14-55`](architecture-archive.md#trim-2026-09-14-55) |
| Bid-ask spread as entry-side gate (spec #1) | The order book is already fetched. `SpreadBps` is a near-zero-cost microstructure signal: a WIDE spread during an apparent directional signal often indicates a flush in progress, not a clean breakout. Penalises entries only — no direction signal, no scoring during trending spread expansion. |
| OFI Momentum mirrors FundingMomentum pattern (spec #2) | OFI level measures current order-flow imbalance; OFI momentum measures whether that imbalance is accelerating or decelerating. A level signal being amplified by rising momentum is structurally stronger than a level signal that is fading. Ring buffer pattern and RISING/FALLING/FLAT enum match the already-shipped FundingMomentum design so the engine is internally consistent. |
| Dynamic MicroCVD accelThreshold (spec #3) | Static threshold (10000 USD) was too high a bar on quiet sessions (total window USD flow might be only 2× the threshold) and too low on high-volume sessions (normal flow exceeds it continuously). Dynamic `max(static, windowUsd × pct)` self-scales: the static floor prevents it from being trivially crossed on micro-flow sessions; the multiplier raises the ceiling proportionally with actual session activity. |
| VPFR-lite v2 VAH/VAL + nearest HVN/LVN (spec #4) | Raw POC alone misses the case where price is between two significant nodes. VAH/VAL define the value area (70% of volume) and give a profile-based context for VWAP extensions. `VPFRNearestHvnAbove` / `VPFRNearestHvnBelow` provide the closest resistance/support wall which is more actionable than the POC for target capping — price is more likely to stall at the nearest wall than to reach the POC when structure intervenes. Required by the 3-tier cap in spec #5. |
| Swing pivot detection — scan and bookkeeping (spec #5) | Confirmed swing pivot requires ALL N bars left and right to be strictly lower/higher than the pivot bar — avoids equal-high false positives that create spurious structure. Walking backward from scanEnd to find the most recent pivot (rather than scanning forward) means the engine always surfaces the freshest actionable level. Direction-aware bookkeeping (SwingTargetLong/Short, SwingStopLong/Short) computed inline in Analysis so CalcSwingPivots stays a pure function. |
| 3-tier Step 5b target cap (spec #5) | The prior 2-tier cap (HVN wall → POC) ignored swing structure. A confirmed 5m swing high is typically closer to entry and more immediately relevant than a VPFR HVN. The 3-tier priority (swing > nearest HVN > POC) reflects structural significance: swing targets are the first price memory the market has established, HVN walls are the second, and POC is the broadest fallback. Winner = closest to entry ensures the cap is conservative: the tightest available cap is used, not the loosest. |
| Layer 1.5 structural-break exit in CalcHoldStatus (spec #5) | A confirmed break through the prior swing low (long) or swing high (short) is a discrete structural event — it invalidates the original entry premise faster than gradual RSI/OBV divergence. Sits between Layer 1 (fast microstructure count) and Layer 2 (OBV divergence) to maintain the priority ordering: structural breaks are evaluated only when microstructure hasn't already triggered, but before the slower divergence signals can fire. |
| CalcVerdictContext structural-target first check (spec #5) | When swing data exists (LastSwingHigh5m or LastSwingLow5m is non-zero), the engine has committed to a structural view. If neither target nor stop can be placed for the current direction, flagging STRUCTURALLY_WEAK is more informative than CONFIRMED even if the score is high — it means the structural picture is ambiguous or the trade doesn't have a defined structural R:R. The graceful-degradation path (check fires only when at least one swing level exists) prevents false STRUCTURALLY_WEAK signals when candle history is too short for pivot detection. |
| Settings exposure pass (spec #6) | Exposed 19 formerly-hardcoded literals to `settings.json` with zero behaviour change, completing the auto-tweaking audit prerequisite. ⚠ **Its "default value in the method signature for caller convenience" pattern is SUPERSEDED:** A54a session 2 (2026-09-05) deleted 44 method `Optional` defaults, so callers now pass cfg values by name. Original row: [`architecture-archive.md` §A, `trim-2026-09-14-56`](architecture-archive.md#trim-2026-09-14-56) |
| Trade capture in the app, as two redundant mechanisms (v64) | Deribit's public trades endpoint serves ≈24 h and refuses older windows; candles have no such cap. Trades are therefore the one input the backtester cannot synthesise around, and trade-derived signals (CVD, MicroCVD, TFI, aggressor velocity, liquidations) can only be **re-derived under different settings** from raw ticks — `analysis_log.csv` stores their outputs, which keeps the answer and discards the question, so it cannot substitute. With a vendor feed declined on cost, append-forward is the only path, which makes its reliability the whole question. The app is the right host because it is already up 24/7, already watched daily, and **already receives every trade** — appending the stream is a write, not a fetch, so capture needs no API call and has no 24-hour deadline. The external-scheduled-task alternative had three failure modes that were each silent and unrecoverable past 24 h. Streaming and gap repair are deliberately NOT collapsed into one mechanism: streaming is complete while the app runs and recovers nothing from downtime; repair recovers downtime but, alone, reinstates the 24-hour deadline. The monotonic last-written guard is what makes them compose — `SeedAsync` re-seeds the trade ring from REST on every (re)connect, so duplicates are guaranteed by design, and the guard plus the reader's whole-row dedup make overlap a no-op at both write and read time. `HistoricalStore` was split rather than moved because it owns a live `HttpClient`: the format/rollover/guard half (`Core/TradeStoreWriter.vb`) links everywhere including the fixture project, the network half stays in `tools/BacktestRunner/` and is linked by the app for repair only. Store path resolves against the **exe directory**, never the cwd — the app's cwd is not guaranteed and a cwd-relative store would silently scatter capture files. `docs/in-app-trade-store-capture-proposal.md`. |
| Linux CLI port as long-term target (2026-05-05) | The WinForms app remains the active surface but a future headless Linux service is on the roadmap (`DeribitIndicatorProject.md` §16.2). All code in `analysis/` and `tools/` must therefore be host-agnostic — no `System.Windows.Forms`, no `Control.Invoke`, no `MainForm` coupling. Form-side viewers like `AnalysisReportForm` and `TweakSettingsForm` are thin wrappers over host-agnostic core. The auto-tweaker console app builds as a separate .NET 8 project with **zero WinForms references** so it runs unmodified under `dotnet AutoTweaker.dll` on Linux. Port itself happens after auto-tweaker ships AND analysis accuracy plateaus. WebSocket migration is independent of the port. |

---

## Display Behaviour Clarifications

Notes on rendering behaviours that have surfaced in audits as potentially-buggy but are intentional. Documented here so future audits (and future Claude conversations) don't re-flag them.

| Behaviour | Status | Rationale |
|---|---|---|
| `HOLD \ EXIT:` row absent from rendered output and output dump when no position is held | By design | `BuildPlaintextSnapshot` (`UI/MainForm_PlaintextSnapshot.vb:136`) guards on `If v.HoldStatus <> "N/A -- no open position" Then` (the card binding mirrors the same sentinel check). `CalcHoldStatus` returns the `"N/A -- no open position"` sentinel when `posState.IsNone`. The whole hold-guidance block (label + value) is suppressed in that case — there's no position to guide. The rendered label is `HOLD \ EXIT` and only when a position has been declared via the radio buttons. |
| POC tier 3 of the target cap never fires in practice | ⛔ **NOT by design: a DEFECT, found 2026-09-16.** The POC-tier gate reads the VPFR labels inverted. `CalcVPFRLite` emits `NEAR_HVN_RESIST` when price is at or above the POC, and the long POC tier opens on exactly that label (`Core/SignalEmitter.vb` ~330-332; legacy twin `ScoringEngine_Calculate_Verdict.vb` ~223-224). So it opens only when the POC cannot be a long target: 0 POC placements in 38,665 verified rows. Evidence: [`medium-tier-bug-hunt-2026-09-16.md`](../docs/medium-tier-bug-hunt-2026-09-16.md). Fix option is trader ruling `D-1` in [`medium-tier-bug-hunt-spec-back.md`](../docs/medium-tier-bug-hunt-spec-back.md). The original "by design + geometry" explanation follows, superseded | The Step 5b cap arbitration considers POC only when `hvnAbove` / `hvnBelow` is True (VPFR signal flags the engine is in HVN proximity). When the gate is open, POC must additionally be closer to entry than both the swing target AND the nearest HVN. The combined conditions are narrow enough that POC almost never wins by geometry alone. The branch is reachable, not dead code. POC tier is a **refinement** of the HVN tier, not a general fallback for the "no swing, no HVN" case. |
| `STRONG LONG` / `STRONG SHORT` co-existing with `STRUCTURALLY_WEAK` / `MOMENTUM_FADING` context tags | Intentional | `VerdictContext` is display-only (Step 5b post). It surfaces structural caveats the score didn't fold in. A STRONG-tier score with a warning tag legitimately means "score qualifies for the strong tier, but the structural picture is thin / momentum is fading." This is informative, not contradictory — the trader should read the context tag before sizing. Suppressing warnings on STRONG verdicts would remove the very signal the tag was added to surface. |
| MTF Reason rendered in three formats (`MTF PASS [DIR]`, `MTF BLOCK [DIR vs TREND]`, `MTF state: TREND \| details`) | By design | Three scenarios, three formats. Since v31 the string is composed at scoring Step 4b against the **dominant side** and stored on `VerdictResult.MTFGateReason` — every consumer (MTF card, plaintext snapshot, CSV, breakdown row) renders that one string. `MTF PASS [DIR]` when a directional verdict is in play and the gate clears; `MTF BLOCK [DIR vs TREND]` when it fails; `MTF state: TREND \| details` when no directional verdict is in play. The leading-keyword inconsistency is a deliberate signal of the no-direction case — unifying would lose that distinction. |
| `MTF BLOCK [...]` reason string still composed when `mtf_gate.enabled: false` while no block occurs | By design (config-edge display quirk, v47 N3) | With `mtf_gate.enabled: false` (non-default), a failing gate still composes its reason as `MTF BLOCK [DIR vs TREND]` — Step 4b (`ScoringEngine_Calculate_Verdict.vb` ~:110) doesn't consult `Enabled` when formatting, only when deciding whether to veto. So the display can read BLOCK while the verdict proceeds. Unreachable at current config (`enabled: true`), and the auto-tweaker can never create the state (`mtf_gate.enabled` is in `DisabledGatedPaths`). The three MTF reason formats are locked by design — do not change the code. |
| `[B]` / `[T]` mode indicator absent from rendered output dump (pre-v30) | Was a spec gap (fixed v30) | `AnalysisOutputDump.Append` originally captured only `txtOutput.Text` (the RTF content). The perf-strip is separate WinForms `Label` controls outside the RTF, so the mode indicator and the six rate labels weren't captured. v30's display-polish pass adds a `PERF STRIP` header line to each dump block. Dumps from pre-v30 won't have this line. |

