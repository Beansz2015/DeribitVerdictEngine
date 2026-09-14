# Architecture Archive

Historical and superseded text moved out of [`architecture.md`](architecture.md). Not current state.

---

## A. Trimmed from `architecture.md` (2026-09-14)

Moved verbatim by the 2026-09-14 context trim. Each block sits between `begin`/`end` markers and is byte-identical (LF form) to the stated line range at git tag `doc-trim-2026-09-14-pre`. Ledger and hashes: [`doc-trim-log.md`](doc-trim-log.md). Re-check with `tools/checks/doc-trim-verify.ps1`. **History only - not current state.**

<a id="trim-2026-09-14-52"></a>
### trim-2026-09-14-52 - Directory Layout - the April-era docs/ subtree listing

Source: `docs/architecture.md` lines 361-376 at git tag `doc-trim-2026-09-14-pre` - 1193 B - SHA-256 `fa9fae9120b69cdebcce66cf3c76e0152e3004df9b80ef6d96237765d6318f4a`.

<!-- trim-2026-09-14-52 begin -->
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
<!-- trim-2026-09-14-52 end -->

<a id="trim-2026-09-14-53"></a>
### trim-2026-09-14-53 - Settings Data Flow - the v30 session-bucket values quoted in the diagram

Source: `docs/architecture.md` lines 624-625 at git tag `doc-trim-2026-09-14-pre` - 145 B - SHA-256 `469e7fb60e5861f000bb7c0fd6584fc61b63f7f9b5f7f2f305b3666f9d64a893`.

<!-- trim-2026-09-14-53 begin -->
    │         Current settings.json populates ASIA (00–07, 0.80/0.85),
    │         LONDON (08–12, 1.00/1.00), NY (13–23, 1.15/1.10).
<!-- trim-2026-09-14-53 end -->

<a id="trim-2026-09-14-54"></a>
### trim-2026-09-14-54 - Design Decisions row - MainForm_Render split into _Header + _Sections (files deleted in P5b)

Source: `docs/architecture.md` lines 682-682 at git tag `doc-trim-2026-09-14-pre` - 241 B - SHA-256 `fbb745a6181c0cb32f5b9bdcfdf25adba21e21acf49ab96470b5fa49638617ae`.

<!-- trim-2026-09-14-54 begin -->
| MainForm_Render split into _Header + _Sections | MainForm_Render.vb exceeded 28 KB. RTF helpers + top render block (verdict/ATR/Kelly) in _Header.vb; RenderOutput() entry point + all indicator sections + breakdown table in _Sections.vb. |
<!-- trim-2026-09-14-54 end -->

<a id="trim-2026-09-14-55"></a>
### trim-2026-09-14-55 - Design Decisions row - v15 cleanup pass (historical audit)

Source: `docs/architecture.md` lines 684-684 at git tag `doc-trim-2026-09-14-pre` - 665 B - SHA-256 `f90afd2951b48f1542978de60ca50fe6f1a96fb47538fde820fb992d72a480f5`.

<!-- trim-2026-09-14-55 begin -->
| v15 cleanup pass | Source of truth audit. Removed dead fields (`OI_Prev15m` / `OI_Prev60m` / `ATRAvg20d`), three unused `DynamicNorms.StaticVol*` properties, an entire `Ema200Settings` class, 13 silently-ignored config properties, the dead `ScoringEngine.MaxScore` const, the unused `SettingsLoader.Reload()`. Aligned remaining default values with v14 calibration (so an absent `settings.json` doesn't seed stale defaults). Two display-only colour bugs fixed in `MainForm_Render_Sections` (BBW status compared against `"SQUEEZE"` instead of `"ACTIVE"`; TTM direction compared against `"UP"` / `"DOWN"` instead of `"RISING"` / `"FALLING"`). Zero scoring impact. |
<!-- trim-2026-09-14-55 end -->

<a id="trim-2026-09-14-56"></a>
### trim-2026-09-14-56 - Design Decisions row - Settings exposure pass (spec 6), whose method-default pattern A54a later removed

Source: `docs/architecture.md` lines 693-693 at git tag `doc-trim-2026-09-14-pre` - 557 B - SHA-256 `995b30a06b2da312c4863dd03aa8a9856587fe6511548fba52348af6a269c735`.

<!-- trim-2026-09-14-56 begin -->
| Settings exposure pass (spec #6) | Exposing 19 formerly-hardcoded literals to settings.json completes the auto-tweaking audit prerequisite (Section 16.3 item 2). All defaults are exactly the previously-hardcoded values — zero behaviour change. The new Optional params on CalcBBW, CalcTTMSqueeze, CalcCVD, CalcDonchian match the existing pattern (cfg value passed at call site; default value in method signature for caller convenience). RegimeMaxScore() and TierFloor() now take cfg and read from POCO fields rather than returning hardcoded constants. |
<!-- trim-2026-09-14-56 end -->

---

## B. Trimmed from `architecture.md` (2026-09-14, second pass)

Moved verbatim by the second 2026-09-14 trim pass. Each block sits between `begin`/`end` markers and is byte-identical (LF form) to the stated line range at git tag `doc-trim-2026-09-14b-pre`. Ledger and hashes: [`doc-trim-log.md`](doc-trim-log.md). Re-check with `tools/checks/doc-trim-verify.ps1`. **History only - not current state.**

<a id="trim-2026-09-14b-29"></a>
### trim-2026-09-14b-29 - Directory Layout - IMarketDataSource.vb entry (said DORMANT until P2)

Source: `docs/architecture.md` lines 32-34 at git tag `doc-trim-2026-09-14b-pre` - 292 B - SHA-256 `d589f08628f26cf45b80c9ed1cb0dbfbf48dbca5a17f0ab23f797ff188272b5e`.

<!-- trim-2026-09-14b-29 begin -->
├── IMarketDataSource.vb                [WS-P1] Transport contract mirroring DeribitClient's 5
│                                       live call shapes. DORMANT until P2 routes RunAnalysisAsync
│                                       through it by network.transport. Host-agnostic.
<!-- trim-2026-09-14b-29 end -->

<a id="trim-2026-09-14b-30"></a>
### trim-2026-09-14b-30 - Directory Layout - MarketState.vb entry

Source: `docs/architecture.md` lines 38-43 at git tag `doc-trim-2026-09-14b-pre` - 566 B - SHA-256 `043655a87705cac966efc64307dfd57a0d408b3f38e00de7ae61bf1bde8021b4`.

<!-- trim-2026-09-14b-30 begin -->
├── MarketState.vb                      [WS-P1] Thread-safe snapshot store (1 SyncLock, copy-on-
│                                       read): 4 candle series 1/3/5/15, 5000 ascending trade
│                                       ring, top-10 ladder, ticker (funding_8h/OI/mark). Also
│                                       owns the OFI + aggressor-velocity accumulators and
│                                       [v54 #6] the dual-fed LevelAbsorptionTracker (folds,
│                                       reads, resets all under the same lock).
<!-- trim-2026-09-14b-30 end -->

<a id="trim-2026-09-14b-31"></a>
### trim-2026-09-14b-31 - Directory Layout - DeribitWsFeed.vb entry (said DORMANT)

Source: `docs/architecture.md` lines 44-56 at git tag `doc-trim-2026-09-14b-pre` - 1215 B - SHA-256 `b604496c164e760ecbaf2d7b2320f53c49e76b1ce1de69bc7d626eafbbba3fa0`.

<!-- trim-2026-09-14b-31 begin -->
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
<!-- trim-2026-09-14b-31 end -->

<a id="trim-2026-09-14b-32"></a>
### trim-2026-09-14b-32 - Directory Layout - ShadowParityComparer.vb entry

Source: `docs/architecture.md` lines 57-62 at git tag `doc-trim-2026-09-14b-pre` - 599 B - SHA-256 `8ee903cb120af09094f47860d12073b5b4feaed552d60fdaacbf0af4293e6a56`.

<!-- trim-2026-09-14b-32 begin -->
├── ShadowParityComparer.vb             [WS-P2] When network.shadow_parity=true, compares the
│                                       WS source vs the authoritative REST results each run and
│                                       logs a field diff to ws_parity_log.txt + console — NEVER
│                                       the CSV/scoring. The proposal §7 acceptance instrument.
│                                       Host-agnostic. (RunAnalysisAsync routes the 8 live fetches
│                                       through IMarketDataSource via ResolveSource() since P2.)
<!-- trim-2026-09-14b-32 end -->

<a id="trim-2026-09-14b-33"></a>
### trim-2026-09-14b-33 - Directory Layout - TradeStoreGapRepair.vb entry

Source: `docs/architecture.md` lines 63-75 at git tag `doc-trim-2026-09-14b-pre` - 1195 B - SHA-256 `3e923e8ee839033922a6db6ac23700d4e84e5d3ab9b08947feb96a1c3f61ac31`.

<!-- trim-2026-09-14b-33 begin -->
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
<!-- trim-2026-09-14b-33 end -->

<a id="trim-2026-09-14b-34"></a>
### trim-2026-09-14b-34 - Directory Layout - MtfRefreshPolicy.vb entry

Source: `docs/architecture.md` lines 76-80 at git tag `doc-trim-2026-09-14b-pre` - 489 B - SHA-256 `fee214e10c080587af72fd5c22e18ffd2c71d13e4c4d44db264440d76235de83`.

<!-- trim-2026-09-14b-34 begin -->
├── MtfRefreshPolicy.vb                 [WS-P3] Pure host-agnostic predicate — whether to (re)fetch
│                                       15m this run. transport="ws" → always (15m is in-memory; the
│                                       60s TTL only spares the REST HTTP call, so it's moot);
│                                       "rest" → the original TTL gate, byte-identical. §4 15m-TTL
│                                       collapse; harness-tested A16d/e.
<!-- trim-2026-09-14b-34 end -->

<a id="trim-2026-09-14b-35"></a>
### trim-2026-09-14b-35 - Directory Layout - settings.json entry (said version on line 1)

Source: `docs/architecture.md` lines 86-86 at git tag `doc-trim-2026-09-14b-pre` - 124 B - SHA-256 `f907549bf637f579ae92e461b3629902ec8e86c8c19a96252cccb7cc4b4d3e05`.

<!-- trim-2026-09-14b-35 begin -->
├── settings.json                       All tunable parameters (version: see line 1 of the file; no recompile needed)
<!-- trim-2026-09-14b-35 end -->

<a id="trim-2026-09-14b-36"></a>
### trim-2026-09-14b-36 - Directory Layout - EngineSettings.vb entry

Source: `docs/architecture.md` lines 90-95 at git tag `doc-trim-2026-09-14b-pre` - 513 B - SHA-256 `7e9a4385fd66cb881684e87e742b5422bbd04ac75c35e3fe95be53e7828c5db6`.

<!-- trim-2026-09-14b-36 begin -->
│   │   ├── EngineSettings.vb           Strongly-typed POCO for settings.json
│   │   │                               Includes KellySettings + FundingSettings +
│   │   │                               OiCvdSettings + SessionVolumeSettings +
│   │   │                               RegimeWeightSettings + SwingSettings +
│   │   │                               RegimeMaxScoreSettings + TierFloorSettings +
│   │   │                               ContextTagThresholds blocks
<!-- trim-2026-09-14b-36 end -->

<a id="trim-2026-09-14b-37"></a>
### trim-2026-09-14b-37 - Directory Layout - SettingsLoader.vb entry (overlay narrative)

Source: `docs/architecture.md` lines 96-115 at git tag `doc-trim-2026-09-14b-pre` - 1793 B - SHA-256 `c2fc6f8d01a425279473ba88d700cb0ffe9d5ea3ed5e43d9ad9820a0c246ccf5`.

<!-- trim-2026-09-14b-37 begin -->
│   │   └── SettingsLoader.vb           JSON loader — SettingsLoader.Current singleton;
│   │                                   FileSystemWatcher hot-reload; Save(... bumpVersion)
│   │                                   — operational/UI saves pass False (v36 §10a).
│   │                                   [settings.local.json overlay 2026-08-02] Deep
│   │                                   per-key merge of a gitignored per-box overlay
│   │                                   over the tracked base (AWS captures the tape,
│   │                                   the local box does not — same binary, same
│   │                                   settings.json). Allow-list BY CONSTRUCTION:
│   │                                   trade_store / signal_bridge / live_strip /
│   │                                   exit_guard / performance_display /
│   │                                   analysis_logging whole + four network keys;
│   │                                   everything else rejected + logged, non-fatal.
│   │                                   Save() writes the BASE, never the merge — an
│   │                                   override can never be promoted into the shared
│   │                                   file and from there onto AWS. Absent overlay ⇒
│   │                                   byte-identical to the pre-overlay engine.
│   │                                   Second watcher (incl. Created/Deleted) so
│   │                                   dropping the overlay in and deleting it both
│   │                                   take effect live. OverlayActive drives the
│   │                                   title-bar "+local" marker. Fixtures A50a–j.
<!-- trim-2026-09-14b-37 end -->

<a id="trim-2026-09-14b-38"></a>
### trim-2026-09-14b-38 - Directory Layout - ExecutionResolution.vb entry

Source: `docs/architecture.md` lines 117-124 at git tag `doc-trim-2026-09-14b-pre` - 726 B - SHA-256 `af417b3a1a54fed68482b8b4cf1f669091870c93ea1f792f8b5e956895a841bf`.

<!-- trim-2026-09-14b-38 begin -->
│   ├── ExecutionResolution.vb          [v36] Host-agnostic session-conditional execution
│   │                                   resolver. MatchSessionBucket (shared by
│   │                                   DynamicNorms.ApplySessionVolume + the display
│   │                                   ResolveSessionLabel), ResolveResolution,
│   │                                   ResolveRocMagnitude / ResolveRocSlopeDelta, +
│   │                                   ResolveRocMagnitudeForHour (v40 (B) re-baseline —
│   │                                   per-session 3-min ROC magnitude ASIA 0.17 / LONDON
│   │                                   0.11; slope shared in resolution_profiles).
<!-- trim-2026-09-14b-38 end -->

<a id="trim-2026-09-14b-39"></a>
### trim-2026-09-14b-39 - Directory Layout - ProcessIdentity.vb entry (said columns land at the v0.8 rotation)

Source: `docs/architecture.md` lines 126-132 at git tag `doc-trim-2026-09-14b-pre` - 628 B - SHA-256 `27bea2ba8a2c96103028f9506c6f4da2542282b1976c0bbc0d6fe31c44f3628a`.

<!-- trim-2026-09-14b-39 begin -->
│   ├── ProcessIdentity.vb              [Signal Bridge v1] Shared process-identity
│   │                                   primitive: instance_id GUID per process start +
│   │                                   signal_id ticked once per completed run (skips
│   │                                   included) in RunAnalysisAsync, BEFORE the CSV
│   │                                   write. Consumed by SignalEmitter now and by the
│   │                                   CSV InstanceId/SignalId attribution columns at
│   │                                   the #5 v0.8 rotation. Host-agnostic.
<!-- trim-2026-09-14b-39 end -->

<a id="trim-2026-09-14b-40"></a>
### trim-2026-09-14b-40 - Directory Layout - SignalEmitter.vb entry (File.Replace guard narrative, broken tree prefixes)

Source: `docs/architecture.md` lines 133-154 at git tag `doc-trim-2026-09-14b-pre` - 1973 B - SHA-256 `eefeb5dfe9327b2bdee54a877d61a721e37d7e97f44066f27bb057c80712670f`.

<!-- trim-2026-09-14b-40 begin -->
│   ├── SignalEmitter.vb                [Signal Bridge v1] verdict_signal.json emitter
│   │                                   (signal-bridge-v1-proposal.md §3 — schema v1
│   │                                   FROZEN 2026-07-03). Pure BuildOk/BuildSkipped map
│   │                                   the SAME VerdictResult/IndicatorResults fields
│   │                                   the snapshot renders (incl. sub-tick cap-noise
│   │                                   suppression) — the THIRD parity surface. Pinned
│   │                                   DeriveDirection (NONE on all NO TRADE*) +
│   │                                   DeriveWsHealth (OK/DEGRADED/DOWN/REST). Atomic
│   │                                   TryWrite (temp + atomic replace — the
│                                   File.Exists guard is load-bearing: File.Replace
│                                   THROWS when the destination is absent, so the
│                                   first write falls back to File.Move; create-dir,
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
<!-- trim-2026-09-14b-40 end -->

<a id="trim-2026-09-14b-41"></a>
### trim-2026-09-14b-41 - Directory Layout - ScoringEngine_Types.vb entry

Source: `docs/architecture.md` lines 155-159 at git tag `doc-trim-2026-09-14b-pre` - 419 B - SHA-256 `d2ea5decccb315f8a8e2aa298cbc11e649b2d6c3b8dee8452d9fb833786f3d83`.

<!-- trim-2026-09-14b-41 begin -->
│   ├── ScoringEngine_Types.vb          Enums + result types: SignalBreakdownItem,
│   │                                   VerdictResult (incl. AdjustedLongTarget,
│   │                                   AdjustedShortTarget, TargetCapReason,
│   │                                   VerdictContext, Kelly fields),
│   │                                   PositionState, SignalCategory, ScoreState
<!-- trim-2026-09-14b-41 end -->

<a id="trim-2026-09-14b-42"></a>
### trim-2026-09-14b-42 - Directory Layout - ScoringEngine_Helpers.vb entry

Source: `docs/architecture.md` lines 160-166 at git tag `doc-trim-2026-09-14b-pre` - 578 B - SHA-256 `94086bb75584937276e748406052ba10f4810e008f60e0b043a64c47fd069800`.

<!-- trim-2026-09-14b-42 begin -->
│   ├── ScoringEngine_Helpers.vb        Pure functions: RegimeMaxScore (reads cfg
│   │                                   scoring.regime_max_score), Threshold,
│   │                                   TierFloor (reads cfg scoring.tier_floor),
│   │                                   AddFull, HasCrossConfirm, BuildNote,
│   │                                   CalcHoldStatus (Layer 1 microstructure,
│   │                                   Layer 1.5 structural-break exit,
│   │                                   Layer 2 OBV div, Layer 3 RSI/ROC)
<!-- trim-2026-09-14b-42 end -->

<a id="trim-2026-09-14b-43"></a>
### trim-2026-09-14b-43 - Directory Layout - ScoringEngine_Calculate_Scoring.vb entry

Source: `docs/architecture.md` lines 167-174 at git tag `doc-trim-2026-09-14b-pre` - 615 B - SHA-256 `2abe90336cb8ab3782aa749e0ed05a7c9e69ecd08f055cb591442698afb83916`.

<!-- trim-2026-09-14b-43 begin -->
│   ├── ScoringEngine_Calculate_Scoring.vb
│   │                                   AppendLean(), CalcVerdictContext()
│   │                                   (incl. swing structural-target check);
│   │                                   RunScoringPipeline() — Steps 2/Pass 2/
│   │                                   Pass 2b/Pass 2c/3/3b: signal scoring,
│   │                                   partial upgrades, OI×CVD cross-confirm,
│   │                                   regime alignment, funding modifiers,
│   │                                   all breakdown note rows.
<!-- trim-2026-09-14b-43 end -->

<a id="trim-2026-09-14b-44"></a>
### trim-2026-09-14b-44 - Directory Layout - ScoringEngine_Calculate_Verdict.vb entry

Source: `docs/architecture.md` lines 175-187 at git tag `doc-trim-2026-09-14b-pre` - 1100 B - SHA-256 `64513e7f8fe1c2af5e1d7e89fba85285b69f7e0980df706162a4962e2478c5dd`.

<!-- trim-2026-09-14b-44 begin -->
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
<!-- trim-2026-09-14b-44 end -->

<a id="trim-2026-09-14b-45"></a>
### trim-2026-09-14b-45 - Directory Layout - ScoringEngine_Kelly.vb entry (said called from MainForm_Render)

Source: `docs/architecture.md` lines 188-190 at git tag `doc-trim-2026-09-14b-pre` - 298 B - SHA-256 `988243f3db1a05e5ebeb7b61f91eb38119185c7a4738016fb56eef781e0132a1`.

<!-- trim-2026-09-14b-45 begin -->
│   ├── ScoringEngine_Kelly.vb          CalcKellySizing() — display-only Kelly Criterion
│   │                                   sizing. Called from MainForm_Render after ATR levels,
│   │                                   not from ScoringEngine.Calculate(). Zero scoring impact.
<!-- trim-2026-09-14b-45 end -->

<a id="trim-2026-09-14b-46"></a>
### trim-2026-09-14b-46 - Directory Layout - IndicatorResults.vb entry

Source: `docs/architecture.md` lines 192-197 at git tag `doc-trim-2026-09-14b-pre` - 522 B - SHA-256 `dc7db4a2832878eff5f549dcc35a171883da83d2426921f9c312554340921e07`.

<!-- trim-2026-09-14b-46 begin -->
│   ├── IndicatorResults.vb             IndicatorResults struct — all indicator output fields
│   │                                   incl. FundingMomentum, SpreadBps, OFIMomentum,
│   │                                   VPFR-v2 fields (VPFRNearestHvnAbove/Below,
│   │                                   VPFRVAH, VPFRVAL), swing pivot fields
│   │                                   (LastSwingHigh/Low5m/15m, SwingTargetLong/Short,
│   │                                   SwingStopLong/Short)
<!-- trim-2026-09-14b-46 end -->

<a id="trim-2026-09-14b-47"></a>
### trim-2026-09-14b-47 - Directory Layout - Indicators_Volatility.vb entry (said Optional params)

Source: `docs/architecture.md` lines 201-206 at git tag `doc-trim-2026-09-14b-pre` - 494 B - SHA-256 `9ae9d1a475132130c45cfe33a78eaf580e8a5d3f5e73dc7e40d2f10aa81463ad`.

<!-- trim-2026-09-14b-47 begin -->
│   ├── Indicators_Volatility.vb        CalcVWAP (dual-session auto-anchor),
│   │                                   CalcVWAPBands,
│   │                                   CalcBBW (seriesWindowMultiplier + squeezePercentile
│   │                                   Optional params, wired from cfg in v17),
│   │                                   CalcTTMSqueeze (smaPeriod + linRegPeriod Optional
│   │                                   params, wired from cfg in v17)
<!-- trim-2026-09-14b-47 end -->

<a id="trim-2026-09-14b-48"></a>
### trim-2026-09-14b-48 - Directory Layout - Indicators_OrderFlow.vb entry

Source: `docs/architecture.md` lines 207-215 at git tag `doc-trim-2026-09-14b-pre` - 790 B - SHA-256 `d6e06c9e0bbadbf0f6b4f667e25889c2eb808cdede691e83f255722463704646`.

<!-- trim-2026-09-14b-48 begin -->
│   ├── Indicators_OrderFlow.vb         CalcOFI (bookDepth param, dynamic descending weights),
│   │                                   CalcOFIMomentum (RISING/FALLING/FLAT),
│   │                                   CalcCVD (lateSegmentWeight + earlySegmentWeight from
│   │                                   cfg), CalcMicroCVD (dynamic accelThreshold),
│   │                                   CalcTFI, CalcLiquidations,
│   │                                   CalcFundingMomentum + AppendFundingSample
│   │                                   (v53 time-anchored window + ring eviction),
│   │                                   ClassifyAbsorption (v54 #6 — pure classifier
│   │                                   over the LevelAbsorptionTracker read)
<!-- trim-2026-09-14b-48 end -->

<a id="trim-2026-09-14b-49"></a>
### trim-2026-09-14b-49 - Directory Layout - TradeStoreWriter.vb entry (monotonic-guard narrative)

Source: `docs/architecture.md` lines 216-229 at git tag `doc-trim-2026-09-14b-pre` - 1308 B - SHA-256 `6e0ff79059cde3524fa30de414e016a714babee68992c448545b11e7b66a0015`.

<!-- trim-2026-09-14b-49 begin -->
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
<!-- trim-2026-09-14b-49 end -->

<a id="trim-2026-09-14b-50"></a>
### trim-2026-09-14b-50 - Directory Layout - LevelAbsorptionTracker.vb entry

Source: `docs/architecture.md` lines 230-243 at git tag `doc-trim-2026-09-14b-pre` - 1221 B - SHA-256 `148d73f6f48358fec5440d4514aa91b7b7f83b215b79afa7fd072a64f40d8b7c`.

<!-- trim-2026-09-14b-50 begin -->
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
<!-- trim-2026-09-14b-50 end -->

<a id="trim-2026-09-14b-51"></a>
### trim-2026-09-14b-51 - Directory Layout - MainForm_Layout.vb entry

Source: `docs/architecture.md` lines 252-262 at git tag `doc-trim-2026-09-14b-pre` - 966 B - SHA-256 `e74236356a6c707920bab5f5ac72ccdbf8b46a386b252f5d83f734d533fa3708`.

<!-- trim-2026-09-14b-51 begin -->
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
<!-- trim-2026-09-14b-51 end -->

<a id="trim-2026-09-14b-52"></a>
### trim-2026-09-14b-52 - Directory Layout - MainForm_Analysis.vb entry (said calls RenderOutput)

Source: `docs/architecture.md` lines 267-278 at git tag `doc-trim-2026-09-14b-pre` - 1026 B - SHA-256 `6849b5c81a836d9fc6be38a155e08cfce1838789dc0424135855fd5ff8aad557`.

<!-- trim-2026-09-14b-52 begin -->
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
<!-- trim-2026-09-14b-52 end -->

<a id="trim-2026-09-14b-53"></a>
### trim-2026-09-14b-53 - Directory Layout - MainForm_PlaintextSnapshot.vb entry

Source: `docs/architecture.md` lines 279-290 at git tag `doc-trim-2026-09-14b-pre` - 999 B - SHA-256 `922e5b828be2d75a5096729a91346f46072fa0224929edbf32eeb4adbaba1e1e`.

<!-- trim-2026-09-14b-53 begin -->
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
<!-- trim-2026-09-14b-53 end -->

<a id="trim-2026-09-14b-54"></a>
### trim-2026-09-14b-54 - Directory Layout - MainForm_Render_Cards.vb entry

Source: `docs/architecture.md` lines 291-296 at git tag `doc-trim-2026-09-14b-pre` - 531 B - SHA-256 `e39af876f39de2d9c10b767d7ebf1f652891fb5be9613d1d2007a2196ab62758`.

<!-- trim-2026-09-14b-54 begin -->
│   ├── MainForm_Render_Cards.vb        [P5b] card-based UI render — BindCard*
│   │                                   bindings (score, verdict, last price, ATR
│   │                                   levels, structural, breakdown, OI×CVD, MTF,
│   │                                   Kelly, …). The card is the SECOND rendered
│   │                                   surface; the display-string parity rule holds
│   │                                   it in lockstep with the plaintext snapshot.
<!-- trim-2026-09-14b-54 end -->

<a id="trim-2026-09-14b-55"></a>
### trim-2026-09-14b-55 - Directory Layout - MainForm_SignalBridge.vb entry

Source: `docs/architecture.md` lines 297-306 at git tag `doc-trim-2026-09-14b-pre` - 893 B - SHA-256 `a0f6325e797f71e1822fe12b811ad92a6ee61cba855ccc25d5d5d9f77c81ff60`.

<!-- trim-2026-09-14b-55 begin -->
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
<!-- trim-2026-09-14b-55 end -->

<a id="trim-2026-09-14b-56"></a>
### trim-2026-09-14b-56 - Directory Layout - WhatIfLauncherForm.vb entry

Source: `docs/architecture.md` lines 310-314 at git tag `doc-trim-2026-09-14b-pre` - 502 B - SHA-256 `72a6a2f9ba4930fe28f44db06eeb93f5d3a83579c5f0ccb85a62b1d06c2c11a4`.

<!-- trim-2026-09-14b-56 begin -->
│   ├── WhatIfLauncherForm.vb           [offline-whatif-replay W7] Non-modal launcher —
│   │                                   whitelisted-knob grid (value-or-sweep), constraint
│   │                                   field, span, Run. Writes overlay JSON + Process.Start's
│   │                                   tools/WhatIfRunner, opens the report in AnalysisReportForm.
│   │                                   A launcher only — zero replay logic, no tools-project ref.
<!-- trim-2026-09-14b-56 end -->

<a id="trim-2026-09-14b-57"></a>
### trim-2026-09-14b-57 - Directory Layout - analysis/ entry (placed-target migration narrative)

Source: `docs/architecture.md` lines 319-341 at git tag `doc-trim-2026-09-14b-pre` - 1937 B - SHA-256 `b029c1692135bdf233803d5be4367e67b763351ab0d8ca973508e753fca77745`.

<!-- trim-2026-09-14b-57 begin -->
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
<!-- trim-2026-09-14b-57 end -->

<a id="trim-2026-09-14b-58"></a>
### trim-2026-09-14b-58 - Directory Layout - tools/AutoTweaker/ entry

Source: `docs/architecture.md` lines 344-350 at git tag `doc-trim-2026-09-14b-pre` - 598 B - SHA-256 `815104d4c6f27acd7a98572d002516808f2744da2e6489889e68c42b40302a36`.

<!-- trim-2026-09-14b-58 begin -->
│   ├── AutoTweaker/                    Host-agnostic console app (Bundle 2).
│   │                                   AutoTweaker.vbproj — separate .NET 8 project.
│   │                                   Zero WinForms references. Runs unmodified
│   │                                   on Linux via `dotnet AutoTweaker.dll`.
│   │                                   AutoTweakerProgram, AutoTweakerCore,
│   │                                   PromptBuilder, ClaudeApiClient,
│   │                                   SettingsDiffApplier, TweakerConfig, TweakerState.
<!-- trim-2026-09-14b-58 end -->

<a id="trim-2026-09-14b-59"></a>
### trim-2026-09-14b-59 - Directory Layout - tools/WhatIfRunner/ entry

Source: `docs/architecture.md` lines 351-361 at git tag `doc-trim-2026-09-14b-pre` - 1034 B - SHA-256 `1f6dcfdd40c115d9fc00a6e4427f37b5ae61498f5be0357bd3599347dcff416b`.

<!-- trim-2026-09-14b-59 begin -->
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
<!-- trim-2026-09-14b-59 end -->
