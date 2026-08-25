' verify/ordercheck/Program.vb
' Acceptance fixtures for the engine correctness pass
' (docs/engine-correctness-pass-proposal.md §11, tests A1–A9).
'
' The .vbproj links the REAL shipped sources, so these fixtures run exactly the
' code the app ships. All trade-list fixtures are CHRONOLOGICAL ASCENDING
' (oldest first) — the contract GetRecentTradesAsync guarantees post-F1. Truth
' labels are chronological ground truth, not pre-fix outputs.
'
' Run: dotnet run --project verify/ordercheck
' Exit code 0 = all pass, 1 = failures.

Imports System
Imports System.Globalization
Imports System.IO
Imports System.Text.Json
Imports System.Threading
' RootNamespace=OrderCheck applies to files declared without an explicit top-level namespace;
' the CeilingAudit fixture code lives under OrderCheck.CeilingAudit as a result. Alias so the
' fixtures below can write `L2Logistic` / `AuditMetrics` unqualified.
Imports OrderCheck.CeilingAudit

Module Program

    Private _failures As Integer = 0

    Sub Main()
        Console.WriteLine("OrderCheck — engine correctness pass acceptance fixtures")
        Console.WriteLine()

        A1_CvdSlopeRising()
        A2_MicroCvdBullAccel()
        A3_MicroCvdWindowFromEnd()
        A4_TfiWindowFromEnd()
        A5_NormsRecentWindow()
        A6_ObvNormalisation()
        A7_DonchianPriorWindow()
        A8_DominantSideCascade()
        A9_MtfPerSideFlags()
        A10_KellyInverseLeverage()
        A11_CandleFreshness()
        A12_LinearLevels()
        A13_MinTradeableMoveGate()
        A14a_ResolutionSelection()
        A14b_RocOverrideResolves()
        A14c_AtrOn3MinGateFlip()
        A14d_NyByteIdentical()
        A14e_PerRowStamp()
        A14f_ResolutionFilteredAggregation()
        A14g_FreshnessHonoursResolution()
        A14h_RegimeMtfUnchanged()
        A14i_ResolutionSurvivesSessionVolumeDisabled()
        A14j_PerSessionRocMagnitude()

        ' v36 Phase-2a — auto-tweaker (session × resolution) population filter.
        A15a_FilterExcludesNonMatching()
        A15b_ResolutionHomogeneity()
        A15c_LegacyRowDefaultsRes1()
        A15d_SessionDerivationEqualsEngineBucket()
        A15e_ReseedOnFilterChange()
        A15f_ValidateRejectsOffSurfaceKeys()
        A15g_ValidatePassesNormalSessionVolumeKey()

        ' WS-P2 — auto-tweaker network.* hardening (HARD CONSTRAINT 12).
        A15h_ValidateRejectsNetworkKeys()

        ' A59 — AutoTweaker weekday-only row filter, docs/autotweaker-weekday-filter-proposal.md.
        A59a_WeekdayRowsSurviveWeekendExcluded()
        A59b_ReseedOnWeekdayKeyChange()
        A59c_WeekendGapTripsSessionBoundary()
        A59d_ConditionsExtractionExcludesWeekendRows()
        A59e_UnparseableTimestampExcludedNotAdmittedAsMonday()

        ' WS-P3 — cutover: §3 trades connection-health gate + §4 15m-refresh policy.
        A16a_TradesServedWhenConnectedButQuiet()
        A16b_TradesWithheldWhenConnectionDown()
        A16c_LegacyAgeGateWithoutHealthCheck()
        A16d_Mtf15mWsReadsEveryRun()
        A16e_Mtf15mRestRetainsTtl()

        ' P4 #1 — realtime exit guard: shared primitive, evaluator, CalcHoldStatus byte-identical.
        A17a_PrimitiveTwoAdverseLong()
        A17b_PrimitiveStructuralBreakLong()
        A17c_PrimitiveSingleAdverseLong()
        A17d_PrimitiveClearLong()
        A17e_PrimitiveTwoAdverseShort()
        A17f_EvaluatorEndToEnd()
        A17g_CalcHoldStatusByteIdentical()
        A17h_EvaluatorSingleAdverseIsClear()

        ' P4 #2 — on-close analysis mode: bar-roll detection + backstop arithmetic.
        A18a_NoRollSameOpen()
        A18b_RollFiresOnce()
        A18c_MultiBarGapSingleFire()
        A18d_ResolutionSwitchCleanFirstRoll()
        A18e_BackstopArithmetic()

        ' P4 #3 — live microstructure strip: evaluator (TFI / spread / imbalance, nearest-level
        ' bracketing, tape-speed window + lull, empty-buffer blanks).
        A19a_TfiSpreadImbalance()
        A19b_NearestLevelsBracketPrice()
        A19c_TapeSpeedWindow()
        A19d_TapeSpeedLull()
        A19e_EmptyBufferBlanks()

        ' P4 #4 — time-averaged OFI: CalcOFI refactor byte-identical, accumulator EMA
        ' (steady-state + time-aware step), warmup gate, reset re-arm, tweaker surface.
        A20a_CalcOfiRefactorEquivalence()
        A20b_CalcOfiEdgeCasesUnchanged()
        A20c_AccumulatorSteadyState()
        A20d_AccumulatorTimeAwareStep()
        A20e_WarmupGate()
        A20f_ResetReArmsWarmup()
        A20g_TweakerRejectsAveragingFlag()
        A20h_TweakerAcceptsAvgWindow()
        A20i_GeometricSymmetryConvergence()

        ' v47 audit fixes — F1 dead-key removal closes the tweaker no-op path;
        ' D4 fences the scoring.hold_ prefix (HARD CONSTRAINT 17).
        A21a_TweakerRejectsRemovedDeadKey()
        A21b_TweakerRejectsHoldPrefix()

        ' Signal Bridge v1 — emitter payload (schema v1 FROZEN 2026-07-03):
        ' field-by-field serialisation incl. the three target-cap cases, SKIPPED
        ' shape, NO TRADE* leans ⇒ direction NONE, enum pins, ARM flag + process
        ' identity, invariant-culture pins, tweaker fence (HARD CONSTRAINT 18).
        A22a_PayloadFieldByField()
        A22b_SkippedPayloadShape()
        A22c_NoTradeLeansDirectionNone()
        A22d_EnumPins()
        A22e_ArmedFlagAndIdentity()
        A22f_InvariantCultureSerialization()
        A22g_TweakerRejectsSignalBridge()

        ' P4 #5 — aggressor velocity (build sub-version): accumulator decay/rate math,
        ' two-horizon burst detection, cold-start suppression, reset re-arm,
        ' ClassifyAggressorBurst edges, per-session norm/threshold resolution,
        ' three-tier tweaker surface (HARD CONSTRAINT 19).
        A23a_AggrVelSteadyRate()
        A23b_AggrVelBurstDetection()
        A23c_AggrVelColdStartSuppression()
        A23d_AggrVelResetReArms()
        A23e_ClassifyAggressorBurstEdges()
        A23f_AggrVelSessionResolution()
        A23g_AggrVelTweakerSurface()

        ' CSV v0.8 rotation — the shared placed-level arbitration
        ' (SignalEmitter.ComputeSideLevels) IS the payload's levels source, so the
        ' Placed* CSV columns equal the bridge levels by construction; pin it.
        A24a_PlacedLevelsEqualPayloadLevels()

        ' v50 retune cargo (signal-health-retune-proposal.md §5): R1 OFIMomentum
        ' modifier retirement is byte-identical to the no-modifier path + the flag
        ' still gates; the momentum_ prefix is fenced (HARD CONSTRAINT 20) while
        ' the OFI siblings stay on the surface.
        A25a_OfiMomentumRetireByteIdentical()
        A25b_TweakerRejectsOfiMomentumPrefix()

        ' B4b placed-geometry structural-first levels (v51 — placed-geometry
        ' proposal §10 acceptance): target ladder tier selection (incl. the
        ' farther-than-ATR structural win), too-loose → next tier → fallback,
        ' stop SWING/CLAMPED/floored/fallback (DG1 min-shape), min-move gate on
        ' PLACED values through the real Calculate(), session fallback-multiplier
        ' resolution, enabled:false byte-identical rollback, HC21 tweaker fences.
        A26a_StructuralTargetPlacesEvenWhenFarther()
        A26b_TargetLadderTooLooseWalksTiers()
        A26c_StopShapes()
        A26d_MinMoveGateReadsPlacedTarget()
        A26e_SessionFallbackMultiplier()
        A26f_DisabledIsByteIdenticalLegacy()
        A26g_TweakerStructuralLevelsSurface()

        ' D6 eval-barrier migration onto placed levels (d6-eval-placed-stop-migration
        ' -proposal.md): (a) the live tracker's barriers ≡ ComputeSideLevels across
        ' capped/fallback/noise-suppressed cases; (b) offline Placed*-vs-legacy adverse
        ' routing; (c) the D4 before/after report renders both barrier bases; (d) the
        ' eval-cache v4→v5 rotate-and-rebuild path.
        A27a_TrackerBarriersEqualComputeSideLevels()
        A27b_OfflineAdverseRoutingPlacedVsLegacy()
        A27c_D4ReportRendersBothPopulations()
        A27d_EvalCacheV5RotationRebuild()

        ' P4 #5 wire-in (v52) — aggressor-velocity TFI-modifier scoring (proposal §4.5):
        ' upgrade/soften/no-op through the real Calculate(), regimeMax cap, S2a session
        ' scoping (res-3 inert) + scoring_enabled:false byte-identical, HC22 tweaker fence.
        A28a_TfiBurstModifierUpgradeSoftenNoop()
        A28b_TfiBurstUpgradeCapsAtRegimeMax()
        A28c_ScopingAndDisableInert()
        A28d_Hc22SessionVolumeEnabledFence()

        ' Funding momentum TIME-ANCHORED window (v53 — funding-momentum-time-anchored
        ' -window-proposal.md §7 acceptance): anchored RISING/FALLING/FLAT, the newest-≥W
        ' anchor rule (NOT oldest-in-window), cold-start + post-gap FLAT, 30-min eviction
        ' with no count cap, and the cadence-invariance pin — the same funding path sampled
        ' at 30s vs 180s yielding the same states at the same instants, which is the whole
        ' point of the change.
        A29a_AnchoredClassification()
        A29b_AnchorIsNewestAtLeastW()
        A29c_ColdStartAndPostGapFlat()
        A29d_ThirtyMinuteEviction()
        A29e_CadenceInvariance()

        ' offline-whatif-replay (proposal §5 acceptance): the CsvRow → IndicatorResults
        ' adapter feeds the SHIPPED SignalEmitter.ComputeSideLevels identically (no copies);
        ' the overlay whitelist rejects off-list keys loudly; the verdict re-derivation shifts
        ' the directional population under a threshold overlay; the POC ladder tier is closed
        ' in replay (VPFRPoc/VPFRSignal are unlogged).
        A30a_AdapterReproducesComputeSideLevels()
        A30b_WhitelistRejectAccept()
        A30c_ThresholdReplayPopulationShift()
        A30d_PocTierClosedInReplay()

        ' P4 #6 — book absorption at structural levels (build sub-version; docs/
        ' book-absorption-proposal.md §9): episode lifecycle (open / leave-proximity
        ' close / level re-map reset), absorbRatio vs an analytic case, D8 conservation
        ' bounds + pullFrac veto vs sitting defender, break-through instant-NONE +
        ' re-arm discipline, reset/cold/degenerate never-throw, reserved-CSV-column
        ' population (empty ⇒ values, no rotation), session min_aggr_usd resolution,
        ' HC23 three-tier tweaker surface.
        A31a_AbsorptionEpisodeLifecycle()
        A31b_AbsorbRatioAnalyticCase()
        A31c_D8ConservationAndPullFracVeto()
        A31d_BreakThroughAndReArm()
        A31e_ResetColdDegenerate()
        A31f_CsvReservedColumnsPopulate()
        A31g_SessionResolutionAndHc23Fences()
        A31h_TwoAtrScaleInvariance()

        ' Offline matrix placed-target migration (docs/offline-matrix-placed-target
        ' -proposal.md §5 acceptance): the favourable barrier routes to the logged
        ' PlacedTarget* (v0.8 rows) vs the legacy formula (pre-v0.8), the tweaker picks on
        ' (window) alone while still parsing pre-migration history, the before/after grid
        ' renders one column, and the 2026-07-17 floored-grid collapse cannot recur.
        A32a_PlacedFavourableRouting()
        A32b_TweakerWindowOnlyPickAndHistoryParse()
        A32c_D4SingleColumnRender()
        A32d_FlooredGridImpossibility()

        ' F4 eval-cache no-data outcome (docs/eval-no-data-outcome-proposal.md
        ' §4 acceptance): empty bar-lists produce NO_DATA (not WINDOW_EXPIRED);
        ' aggregation excludes NO_DATA from numerator+denominator while TotalRange
        ' still counts it; the v5→v6 sweep reclassifies uncovered WINDOW_EXPIRED
        ' rows to NO_DATA and preserves rows with fresh OHLC coverage.
        A33a_EmptyBarsProducesNoData()
        A33b_AggregationExcludesNoData()
        A33c_V6SweepReclassifiesUncoveredPreservesCovered()

        ' F2/F3/F12 eval display-semantics pass (docs/eval-display-semantics-proposal.md
        ' §6 acceptance): E1 success flip at the render boundary (MarkdownReportWriter
        ' + PromptBuilder) with internal FailureCellResult truth unchanged and the
        ' auto-tweaker's trigger comparison intact under the flipped render; E2a
        ' WEAK exclusion at display time (matrix population = strip population); E3a
        ' middle-band display rendering as "MEDIUM LONG" / "MEDIUM SHORT" while every
        ' stored/wire string stays bare LONG / SHORT (the revision's load-bearing
        ' invariant — CSV, payload, eval cache, string-matching sites all untouched).
        A34a_SuccessRenderFlipMatrixAndCsv()
        A34b_TweakerTriggerUnchangedUnderSuccessRender()
        A34c_WeakExcludedFromStripAggregate()
        A34d_BandDisplayHelperPrefix()
        A34e_StoredFormPinsUnchanged()
        ' [D7 spin-off 2 — smalls-2026-07-22 item 2] §6 render split into (a) DIRECTIONAL
        ' + (b) NO-TRADE LEAN sub-tables with the not-comparable caption.
        A34f_ContextOutcomesSplitAndCaption()

        ' [E5 — v55 addendum, eval-display-semantics-proposal.md §3c] Band-ladder
        ' diagnostic section: renders all three bands with correct counts/success%/CI;
        ' WEAK classifier excludes NO TRADE (and NO TRADE [WEAK LONG] lean forms);
        ' matrix cell space stays 12 (tier × window) with no WEAK tier; PromptBuilder
        ' output contains no ladder section / no WEAK band row.
        A35a_BandLadderRendersAllThreeBands()
        A35b_BandClassifierExcludesNoTradeAndLeanForms()
        A35c_MatrixCellSpaceUnchangedNoWeakTier()
        A35d_PromptBuilderOmitsLadderAndWeak()

        ' [geometry-arbitration-modes v56 — docs/geometry-arbitration-modes-proposal.md §3]
        ' Defaults byte-identical (the load-bearing pin); NEAREST target picks min-distance
        ' incl. the fallback beating a farther swing; WIDEST stop picks max + respects the
        ' 4-tick floor; signed buffers move each side the right direction and the min-move
        ' gate reads buffered prices; whitelist accepts the 4 keys, HC24 fence rejects them,
        ' a sibling numeric still passes; mode-1 overlay replays through the What-If adapter
        ' (the A30a linked-seam pattern).
        A36a_DefaultsByteIdenticalToV51B4b()
        A36b_NearestTargetPicksMinDistance()
        A36c_WidestStopPicksMaxAndRespectsFloor()
        A36d_SignedBuffersMoveAndMinMoveGateReadsBuffered()
        A36e_WhitelistAndHc24Fence()
        A36f_ModeOneOverlayRoundTripsThroughWhatIf()

        ' [#7 + #8 v59 — docs/liq-cascade-level-alerts-proposal.md §4 H5] Alerts tracker:
        ' cascade window math (>= min in window, dominant side, edge-fire once), episode
        ' re-arm on leave-proximity + level re-map close, FIRST_SEEN + CASCADE sidecar
        ' append shape (never-throws), disabled/degenerate no-op, HC25 fence + siblings.
        A37a_CascadeWindowMathAndEdgeFireOnce()
        A37b_LevelApproachEpisodeReArm()
        A37c_SidecarAppendShape()
        A37d_ResetAndDisabledInert()
        A37e_Hc25AlertsFence()

        ' [ws_health.log W4 row — A38] Transition-only WsHealthLog sidecar: same
        ' state twice ⇒ one line; process-start line always writes; format shape
        ' matches the AlertsSidecar contract (utc | state | instance_id).
        A38a_TransitionOnly()
        A38b_StartLineAndFormat()

        ' [W6-4 ceiling audit — A39, docs/w6-4-ceiling-audit-method-proposal.md §5]
        ' Hand-rolled L2 logistic + walk-forward + block bootstrap + feature matrix
        ' fixtures: (a) monotone loss + direction recovery on a separable set,
        ' (b) AUC≈0.5 on label-shuffled data (leakage canary),
        ' (c) chronological split respected (no test row precedes any train row),
        ' (d) bootstrap blocks never straddle a session-hour boundary,
        ' (e) informational Absorption/AggrVel-un-armed extras absent from X.
        A39a_LogisticLossMonotoneAndDirection()
        A39b_LabelShuffledAucIsHalf()
        A39c_ChronologicalSplitRespected()
        A39d_BlockBootstrapNeverStraddlesHourBoundary()
        A39e_InformationalExtrasAbsentFromDecisionMatrix()

        ' [v62 fee-aware min-move floor — A40, docs/fee-aware-min-move-proposal.md §5]
        ' Resolver composition across all three round-trip styles (default composes to
        ' 0.0008 EXACTLY); defaults byte-identical through the REAL Calculate() against a
        ' cfg carrying the retired v35 flat-floor semantics; a knob turn moves the gate and
        ' the composed delta clears the eval re-walk epsilon; HC26 prefix fence + the
        ' retired key's natural C-6 rejection + the what-if whitelist split; and the
        ' min_net overlay round-tripping through WhatIfSettings.BuildCellSettings.
        A40a_ResolverComposition()
        A40b_DefaultsByteIdenticalToV61Floor()
        A40c_KnobChangeMovesTheGate()
        A40d_Hc26FenceAndWhatIfWhitelist()
        A40e_MinNetOverlayRoundTripsThroughWhatIf()

        ' [§6.1 rider — eval net-EV, docs/fee-aware-min-move-proposal.md §6.1]
        ' Analysis-only, no scoring impact, no settings keys. Pins the fee-drag arithmetic
        ' inside WhatIfReplay.ComputeEvAtr across all three outcome arms + the fees-zero
        ' regression identity + the report's net-of-fees label + σ dispersion column.
        A41a_NetEvSubtractsFeeDragOnSuccessAndStopArms()
        A41b_WindowExpiredArmAlsoPaysFeeDrag()
        A41c_FeesZeroCfgIsByteIdenticalToGross()
        A41d_ReportCarriesNetOfFeesLabelAndDispersionColumn()

        ' [v63 D2-v2 what-if candidate mode — A42, docs/d2v2-whatif-candidate-mode-proposal.md §4]
        ' New POCO knob scoring.structural_levels.use_best_pivot_candidate (default false).
        ' Ladder mode ⇒ inserted as the FIRST tier above swing; NEAREST mode ⇒ competes on
        ' distance. Same looseness bound as every tier; STOP side untouched. Label BEST_PIVOT_5M.
        A42a_DefaultsByteIdenticalToV56()
        A42b_BestPivotEntersLadderFirstAndNearestDistancePick()
        A42c_LoosenessBoundAndAbsentPivotEquivalence()
        A42d_Hc24FenceAndWhatIfWhitelistAndRoundTrip()

        ' [Backtest synthesizer CORE — A43, docs/backtest-synthesizer-proposal.md §4]
        ' Slicing exactness (window size + at-or-before-close boundary) · trade window
        ' ascending + last-500-from-end · sequential-state replay (funding ring +
        ' regime hysteresis vs a hand-walked case) · muted-vote inertness (OFI / spread
        ' / OI / absorption contribute zero) · header byte-parity with AnalysisLogger
        ' (reflection) + provenance stamping (BACKTEST- prefix + monotonic SignalId).
        A43a_SliceCandlesAtOrBefore()
        A43b_SliceTradesAscendingAndLastN()
        A43c_SequentialStateReplay()
        A43d_MutedVoteInertness()
        A43e_HeaderParityAndProvenance()

        ' [Backtest §7.1 forming-bar stub — A43f, post-validation amendment 2026-07-30]
        ' Mirror live's forming-bar convention: every candle series's last bar in the
        ' assembled slice is a stub built from real trades in [closeMs, closeMs + 2s]
        ' (zero-trade fallback = prev close + 0 volume). Pins the three moving parts:
        ' trades-in-window OHLCV compaction · zero-trade fallback shape · stub is the
        ' LAST bar of the slice (Timestamp advanced past the last real bar).
        A43f_FormingStubConstruction()

        ' [Backtest overlap validation — A44, docs/backtest-synthesizer-proposal.md §4]
        ' The validate CLI verb joins synthetic ⇔ live rows by execution-resolution bar.
        ' A44 pins the OverlapValidator.FloorToBucket contract: floor(ts, execRes-min grid)
        ' collapses everything within the same bar to a single key so the join is
        ' well-defined even when live and synthetic timestamps differ by seconds
        ' (live-collection latency vs synthetic exact-close alignment).
        A44a_FloorToBucketSameGrid()

        ' [VWAP session-anchor parameterization — A45, docs/backtest-synthesizer-proposal.md §7.5]
        ' GetSessionCandles gained Optional nowUtc As DateTime? = Nothing. Nothing => UtcNow,
        ' which is the LIVE path and must stay byte-identical to the pre-parameterisation
        ' behaviour; replay passes the bar close. A45a pins default-path identity, the
        ' historical-anchor semantics on both sides of the session-2 cutoff, and the
        ' whole-list fallback the un-parameterised call produced under replay (§8.6).
        A45a_VwapSessionAnchorParameterization()

        ' [Pooled-file report runner — A46, pre-aug1-opus-batch-2026-07-31.md item B]
        ' The `report` verb runs the SHIPPED analysis/AnalysisRunner pipeline over an
        ' arbitrary (pooled) CSV. A46a drives that chain from a real CSV file on disk
        ' through the REAL Load → PopulateForwardBars → FailureRateMatrix.Compute →
        ' BandLadder.Compute → MarkdownReportWriter path and asserts the produced
        ' document carries the §2 matrix and §9 band-ladder sections with the fixture's
        ' own numbers. The forward-OHLC map is supplied synthetically, so the fixture is
        ' offline; the network hop it stands in for is the pre-existing shared
        ' DeribitOhlcFetcher path, unchanged by this lane.
        A46a_PooledCsvReportSections()

        ' [D3 closed-bar A/B arm — A47, pre-aug1-batch-spec-back.md §7]
        ' ReplayLoop.Run gained Optional useFormingStub As Boolean = True. The False arm
        ' slices N fully-closed bars and appends no stub, so the window LENGTH matches the
        ' stub arm and the terminal bar is the only variable. A47a pins that equal-length
        ' property and that the closed arm's terminal bar is a REAL bar, not a stub.
        A47a_ClosedBarArmHoldsWindowLength()
        A47b_StubVolumeIsCommensurateWithCandleVolume()

        ' [v64 in-app trade-store capture — A48, docs/in-app-trade-store-capture-proposal.md §6]
        ' Streaming capture writes the raw tape into the backtest store from
        ' DeribitWsFeed.ApplyTrades; a gap-repair backfill heals downtime. Core/
        ' TradeStoreWriter.vb is the ONE seam the streaming writer, the network backfill and
        ' LoadTradeRange's per-file parse all route through — these fixtures pin that seam.
        A48a_AppendedRowsRoundTripThroughShippedReader()
        A48b_MonotonicGuardDropsReplayedBatch()
        A48c_MonthRolloverSplitsAndHeadersOnCreateOnly()
        A48d_GapRepairOverlapIsNoOp()
        A48e_UnwritablePathNeverThrows()
        A48f_DisabledMeansZeroWrites()
        A48g_Hc27FencesTradeStoreKeys()
        A48h_StoreDirIsExeRelativeNotCwdRelative()

        ' [A49 — trade-store coverage report `coverage` verb, Session 1,
        ' docs/trade-store-coverage-report-proposal.md + docs/trade-store-coverage-report
        ' -implementer-brief.md + docs/j-b-scoping-ruling-2026-08-02.md +
        ' docs/weekday-scope-ruling-2026-08-03.md] Six-class per-hour classification +
        ' S0-S4 signals. A49m (Part B pairing) is Session 2.
        A49a_UptimeParseAcrossTwoProcessLives()
        A49b_S1JoinUpButUncapturedVsAppDown()
        A49c_DegradedRestCountsAsUpNotConflated()
        A49d_CaptureEraSelfBounding()
        A49e_S3ThresholdReportsExactMaxAndBreachesOnlyAbove()
        A49f_S4CandleCompletenessShortByK()
        A49g_AbsentWsHealthSkipsS1S2ToS4StillRun()
        A49h_StrictExitDecisionDefectVsExpectedMissing()
        A49i_PrimarySupplementPrecedenceAndAmbiguousDefaultsToDefect()
        A49j_S0VenueDiffEnumeratesExactly()
        A49k_NotCapturingInversionTrap()
        A49l_UnknownScopeWithNoMarkerAtAll()

        ' [C1 Session 2 / Part B — trade-store-coverage-report-implementer-brief.md §0]
        A49m_WeekdayScopeVsPartBUnconditionalLiveness()
        ' [C1 Session 2 review finding, 2026-08-05 — dead capture path vs genuine cold start]
        A49n_DeadCapturePathEscalatesPastGraceWindow()

        ' [SH-1 — split-hour fix, docs/coverage-split-hour-implementer-brief.md, ruled
        ' trader-tick-queue.md §0a 2026-08-12. Origin: c1-session1-review-2026-08-04.md F2 —
        ' called "SH-1" throughout, never "F2" (collides with three other things; brief §0).]
        A49o_SplitHourFlipOnMidHourCleanOnHalfNotLaundered()
        A49p_SplitHourFlipOnMidHourSilentOnHalfIsDefect()
        A49q_SplitHourFlipOffMidHourDefectInOnHalfNotLaundered()
        A49r_SplitHourMarkerExactlyAtHourStartUnchanged()
        A49s_SplitHourMarkerAtFiftyNineUsesAbsoluteThresholdNotScaled()
        A49t_SplitHourTwoMarkersThreeSpansAllClassified()
        A49u_SplitHourGapStraddlingMarkerAttributedToEndingSpan()
        A49w_SplitHourFirstEverMarkerMidHourResidualPrefersUnknownScope()
        A49v_BuildResultOneRowPerHourAcrossASplitDay()

        ' [A51 — candle/funding store write invariant, docs/store-integrity-check-2026-07-31
        ' -post-fix.md] The candle backfill destroyed June 2026 at 3m/5m/15m by writing a
        ' whole MONTH file from a partial SEGMENT fetch, behind a resolution-blind coverage
        ' check that refetched every non-1m month on every run. Both decisions now live in
        ' Core/StoreFiles.vb as pure functions, and these pin them. Test debt closed.
        A51a_MergePreservesExistingRowsOnPartialFetch()
        A51b_CoverageCountIsResolutionAware()
        A51c_EmptyOrFailedFetchNeverDestroys()
        A51d_FundingMergeClipsOverreachButKeepsStored()
        A51e_CandleRoundTripThroughShippedParse()

        ' [A52 — v65 / D3 ASIA aggressor-velocity arming,
        ' docs/asia-burst-threshold-derivation-2026-08-01.md §5] D3 arms a session by the
        ' PRESENCE of its burst_ratio_threshold and ships no code of its own, so the whole
        ' change rests on the JSON→POCO→HasExplicit path. A23g pinned the tweaker fence on
        ' that path and A28c pins the scoring behaviour off POCO defaults; neither pinned
        ' the arming contract itself.
        A52a_AsiaArmingJsonContract()

        ' [A53 — trade identity in the store schema,
        ' docs/trade-store-trade-identity-proposal.md §6] The store row carried no trade
        ' identity, so its dedup and S0's venue diff both matched on five fields that
        ' genuinely distinct trades can share. A53c and A53e are the two that matter:
        ' both guard failure modes that produce a FULL STORE and a GREEN HARNESS.
        A53a_IdentityRoundTripsThroughStore()
        A53b_LegacyRowParsesWithIdentityAbsent()
        A53c_EmptyIdentityDoesNotCollapseLegacyRows()
        A53d_EqualIdentityDedupsToOne()
        A53e_DistinctIdentitySameLegacyFieldsSurvivesAsTwo()
        A53f_MixedShapeFileDedupsUnderBothBranches()
        A53g_VenueDiffSeparatesIdentityAndFallbackMatches()
        A53h_SequenceGapDetection()

        ' [A55 — the trade-store WRITE guard, keyed on identity,
        ' docs/trade-store-write-guard-identity-proposal.md §5] The identity build fixed the
        ' READ path and left the WRITE guard keyed on a millisecond, which discarded 49.2 % of
        ' the live tape — measured off the wire, not inferred (§1a). ⚠ Every fixture here puts
        ' two DISTINCT trades on ONE millisecond, because that is the single thing the eight
        ' A48 fixtures never did. All seven were confirmed to FAIL against the shipped guard.
        A55a_SameMillisecondSiblingsBothSurvive()
        A55b_ReconnectReplayWritesOnce()
        A55c_MixedIdentifiedAndLegacyBatch()
        A55d_IdentitylessRowsDifferingOnlyInAmountAllSurvive()
        A55e_DuplicateOlderThanWindowIsAdmitted()
        A55f_RestartSeedsWindowFromFileTail()
        A55g_UnwritableStoreNeverThrowsAndStillGuards()

        ' [A56 — hole-derived repair windows,
        ' docs/trade-store-downtime-repair-proposal.md §5] Gap repair resumed from the file's
        ' LAST WRITTEN ROW, which streaming makes current again within seconds of reconnecting,
        ' so an app that RODE THROUGH an outage lost the hole permanently — measured at 60.3
        ' minutes on 2026-08-11, now past retention. ⚠ A56a is the mutation proof: revert
        ' ResolveRepairWindowsMs to return only the tail window and it MUST fail. The other five
        ' are the no-phantom half of the property, which one-sided reasoning passes without.
        A56a_SeqBracketedHoleIsReturned()
        A56b_CoveredStoreReturnsTailOnlyAndA48dHolds()
        A56c_OutOfOrderStoreProducesNoPhantomHoles()
        A56d_AbsentSeqRowsProduceNoPhantomHoles()
        A56e_HoleReachingPastSegStartIsClamped()
        A56f_HoleCountIsCappedKeepingTheLargest()
        A56g_TruncationCutIsTimeContiguousNotFileOrder()

        ' [A57 — thin-trade-window skip gate, docs/thin-trade-window-skip-gate-proposal.md §6]
        ' Every guard on the trade path tested Count = 0; nothing tested for a THIN list. A57c
        ' is the mutation proof: revert ScoringEngine.MinTradesForScoring to a hardcoded 50 and
        ' it MUST fail — that is the entire point of the fixture family. [Follow-up to 613cf1e]
        ' A57b DELETED — mutated MinTradesForScoring to drop the MicroCVD term (Max(TFI,1)=30):
        ' A57a/c/d all failed, A57b passed. It asserted only that "<" is strict; it cannot fail
        ' for any implementation. A57c already pins the boundary (fires79/passes80) at a
        ' non-default window, strictly stronger. Numbering kept — do not renumber A57c/A57d,
        ' the spec and the spec-back packet reference them by name. A57e is new (F3): the
        ' exit-guard's own thin-but-nonzero path had no assertion until now.
        A57a_DerivesToFiftyAtDefaultsAndReasonStringFormat()
        A57c_NonDefaultMicroCvdWindowMovesTheDerivedMinimum()
        A57d_OverrideAndTweakerFence()
        A57e_ExitGuardClearOnThinAdverseBuffer()

        ' [A58 — auto-run on start, docs/collector-ops-tooling-proposal.md §1] A58a/b are
        ' host-agnostic; A58c joins the A50 group below (it calls SettingsLoader.Initialise).
        A58a_AbsentKeyDefaultsFalseOnOldSettingsFile()
        A58b_StartEngagedRoundTripsTrueThroughJson()

        ' [settings.local.json overlay — A50, docs/settings-local-overlay-proposal.md §5 with
        ' the corrections in docs/overlay-whitelist-reaudit-2026-07-31.md]
        ' DELIBERATELY LAST in the run order: these are the only fixtures that call
        ' SettingsLoader.Initialise, so they mutate the process-wide SettingsLoader.Current
        ' singleton. Running them after everything else keeps that blast radius at zero.
        A50a_AbsentOverlayIsByteIdentical()
        A50b_DeepMergeFlipsOneKeyOnly()
        A50c_SaveWritesTheBaseNotTheMerge()
        A50d_WhitelistRejectsScoringIndicatorsVersionMtfGateAlerts()
        A50e_MalformedOverlayIsIgnored()
        A50f_HotReloadReMergesAndDeleteReverts()
        A50g_ArraysReplaceWholesale()
        A50h_ScoringSurfacePinThroughRealCalculate()
        A50i_NetworkSplitIsKeyGranular()
        A50j_WhitelistIntersectUiWriteback()
        A50k_AdmittedButAbsentKeyIsWarnedAndDoesNotActivate()
        A58c_OverlayRoutesStartEngagedAndTweakerFenceStillRejectsIt()

        Console.WriteLine()
        If _failures = 0 Then
            Console.WriteLine("ALL PASS")
        Else
            Console.WriteLine(_failures & " FAILURE(S)")
            Environment.ExitCode = 1
        End If
    End Sub

    ' -- Fixture helpers ------------------------------------------------------

    Private Sub Check(name As String, cond As Boolean, detail As String)
        If cond Then
            Console.WriteLine("PASS  " & name)
        Else
            _failures += 1
            Console.WriteLine("FAIL  " & name & " — " & detail)
        End If
    End Sub

    Private Function Trade(direction As String, amount As Double, ts As Long) As TradeRecord
        Return New TradeRecord With {
            .Price = 100000, .Amount = amount, .Direction = direction,
            .Liquidation = "none", .Timestamp = ts}
    End Function

    ''' <summary>Two flat candles — keeps the CVD divergence path quiet (price change below gate).</summary>
    Private Function FlatCandles() As List(Of Candle)
        Return New List(Of Candle) From {
            New Candle With {.Open = 100000, .High = 100001, .Low = 99999, .Close = 100000, .Volume = 10},
            New Candle With {.Open = 100000, .High = 100001, .Low = 99999, .Close = 100000, .Volume = 10}}
    End Function

    ' -- A1: CVD slope — old sells, recent buys → RISING ----------------------
    ' Ascending list of 90 trades: oldest third all sells, middle third net-flat,
    ' newest third all buys. Chronological truth: flow shifted from selling to
    ' buying → slope RISING. (Pre-fix, the desc input made "early" the newest
    ' trades, inverting this.)
    Private Sub A1_CvdSlopeRising()
        Dim trades As New List(Of TradeRecord)
        Dim ts As Long = 1
        For i As Integer = 1 To 30
            trades.Add(Trade("sell", 20000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 15
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
            trades.Add(Trade("sell", 1000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 30
            trades.Add(Trade("buy", 20000, ts)) : ts += 1
        Next

        Dim cvdValue As Double, cvdSlope As String = "", cvdDiv As String = ""
        IndicatorEngine.CalcCVD(trades, FlatCandles(), cvdValue, cvdSlope, cvdDiv)

        Check("A1 CVD slope (old sells → recent buys)",
              cvdSlope = "RISING",
              "expected RISING, got " & cvdSlope)
    End Sub

    ' -- A2: MicroCVD polarity — accelerating bull burst in the tail ----------
    ' 60 ascending buys with sharply growing size toward the end. Window = last
    ' 50; within it, late third USD flow far exceeds early third → chronological
    ' truth is BULL_ACCEL. (Pre-fix the early/late labels were inverted →
    ' BULL_DECEL.)
    Private Sub A2_MicroCvdBullAccel()
        Dim trades As New List(Of TradeRecord)
        Dim ts As Long = 1
        For i As Integer = 1 To 10                      ' outside the 50-trade window
            trades.Add(Trade("buy", 500, ts)) : ts += 1
        Next
        For i As Integer = 1 To 16                      ' window early third
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 16                      ' window mid third
            trades.Add(Trade("buy", 2000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 18                      ' window late third — the burst
            trades.Add(Trade("buy", 5000, ts)) : ts += 1
        Next

        Dim e As Double, m As Double, l As Double
        Dim momentum As String = "", signal As String = ""
        IndicatorEngine.CalcMicroCVD(trades, e, m, l, momentum, signal)

        Check("A2 MicroCVD polarity (bull burst in tail)",
              signal = "BULL_ACCEL",
              String.Format("expected BULL_ACCEL, got {0} (E={1:F0} M={2:F0} L={3:F0})", signal, e, m, l))
    End Sub

    ' -- A3: MicroCVD window selection — oldest trades excluded ---------------
    ' 60 ascending trades, first 10 are huge sells. LastN(50) must exclude them:
    ' the window is the 50 newest (all small buys), so microEarly is exactly the
    ' first 16 buys of the window and net delta is positive.
    Private Sub A3_MicroCvdWindowFromEnd()
        Dim trades As New List(Of TradeRecord)
        Dim ts As Long = 1
        For i As Integer = 1 To 10
            trades.Add(Trade("sell", 1000000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 50
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
        Next

        Dim e As Double, m As Double, l As Double
        Dim momentum As String = "", signal As String = ""
        IndicatorEngine.CalcMicroCVD(trades, e, m, l, momentum, signal)

        Check("A3 MicroCVD window (huge old sells excluded)",
              e = 16000 AndAlso (e + m + l) = 50000,
              String.Format("expected E=16000 net=50000, got E={0:F0} net={1:F0} signal={2}", e, e + m + l, signal))
    End Sub

    ' -- A4: TFI window selection — newest trades only -------------------------
    ' 60 ascending trades: first 30 sells, last 30 buys. The 30-trade TFI window
    ' must be the newest 30 (all buys) → BUY PRESSURE with tfiValue = +1.
    ' (Pre-fix, Take(30) on a desc list happened to be correct; on the ascending
    ' contract Take(30) would select the 30 sells → SELL PRESSURE.)
    Private Sub A4_TfiWindowFromEnd()
        Dim trades As New List(Of TradeRecord)
        Dim ts As Long = 1
        For i As Integer = 1 To 30
            trades.Add(Trade("sell", 1000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 30
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
        Next

        Dim tfiValue As Double, tfiSignal As String = ""
        IndicatorEngine.CalcTFI(trades, tfiValue, tfiSignal)

        Check("A4 TFI window (first 30 sells excluded)",
              tfiSignal = "BUY PRESSURE" AndAlso Math.Abs(tfiValue - 1.0) < 0.000001,
              String.Format("expected BUY PRESSURE / +1.0, got {0} / {1:F4}", tfiSignal, tfiValue))
    End Sub

    ' -- A5: DynamicNorms volume baseline samples the RECENT window ------------
    ' 250 ascending candles: oldest 100 have volume 10, newest 150 volume 1000.
    ' The baseline must describe current conditions → VolMean = 1000 (last 100
    ' completed bars, in-progress final bar excluded). Pre-fix, Take(100)
    ' sampled the OLDEST 100 → VolMean = 10.
    Private Sub A5_NormsRecentWindow()
        Dim candles As New List(Of Candle)
        For i As Integer = 0 To 249
            Dim vol As Double = If(i < 100, 10, 1000)
            candles.Add(New Candle With {
                .Open = 100000, .High = 100050, .Low = 99950, .Close = 100000,
                .Volume = vol, .Timestamp = i})
        Next

        Dim n As DynamicNorms = DynamicNorms.Compute(candles, currentATR:=50)

        Check("A5 DynamicNorms volume baseline (recent window)",
              Math.Abs(n.VolMean - 1000) < 0.001,
              String.Format("expected VolMean=1000, got {0:F1}", n.VolMean))
    End Sub

    ' -- A6: OBV normalisation — no first-bar dead state ------------------------
    ' Two identical 50-bar rises (volume 10/bar), differing only in whether the
    ' FIRST close pair is equal. Pre-fix, the equal first pair made
    ' obvValues(0) = 0 → obvChange forced 0 → OBV FLAT-dead for the run.
    ' Post-fix (mean-volume normalisation) both classify identically.
    Private Function ObvRiseCandles(firstPairEqual As Boolean) As List(Of Candle)
        Dim candles As New List(Of Candle)
        Dim close As Double = 100000
        For i As Integer = 0 To 49
            If i > 0 AndAlso Not (i = 1 AndAlso firstPairEqual) Then close += 10
            candles.Add(New Candle With {
                .Timestamp = i, .Open = close - 5, .High = close + 5,
                .Low = close - 10, .Close = close, .Volume = 10})
        Next
        Return candles
    End Function

    Private Sub A6_ObvNormalisation()
        Dim trendA As String = "", divA As String = ""
        Dim trendB As String = "", divB As String = ""
        IndicatorEngine.CalcOBV(ObvRiseCandles(firstPairEqual:=True), trendA, divA, trendGate:=10.0)
        IndicatorEngine.CalcOBV(ObvRiseCandles(firstPairEqual:=False), trendB, divB, trendGate:=10.0)

        Check("A6 OBV normalisation (first-pair-equal not dead)",
              trendA = "RISING" AndAlso trendA = trendB,
              String.Format("expected RISING both ways, got equal-pair={0} distinct-pair={1}", trendA, trendB))
    End Sub

    ' -- A7: Donchian prior-bar channel ----------------------------------------
    ' 25 candles: bars 0–23 cap at high 105; the current bar closes at 110.
    ' The 20-bar channel must span the PRIOR bars (4..23) → upper = 105, so the
    ' call-site full-LONG check (close ≥ upper) genuinely fires. Pre-fix the
    ' window included the current bar → upper = 112 → full signal unreachable.
    Private Sub A7_DonchianPriorWindow()
        Dim candles As New List(Of Candle)
        For i As Integer = 0 To 23
            candles.Add(New Candle With {
                .Timestamp = i, .Open = 100, .High = 105, .Low = 95, .Close = 100, .Volume = 10})
        Next
        candles.Add(New Candle With {
            .Timestamp = 24, .Open = 100, .High = 112, .Low = 99, .Close = 110, .Volume = 10})

        Dim upper As Double, lower As Double
        IndicatorEngine.CalcDonchian(candles, 20, upper, lower)

        Check("A7 Donchian prior-window channel",
              upper = 105 AndAlso lower = 95 AndAlso candles.Last().Close >= upper,
              String.Format("expected upper=105 lower=95 (close 110 breaks out), got upper={0:F0} lower={1:F0}", upper, lower))
    End Sub

    ' -- A8: Step 5 dominant-side cascade --------------------------------------
    ' Drives the real Calculate() with contrived RANGE_BOUND inputs.
    ' POCO-default tiers at regimeMax 18 (RegimeWeights disabled):
    ' strong 13 / med 10 / weak 7.
    ' The fixture produces exactly 11 short votes and 4 long votes; a Step 3
    ' funding boost (penalty zeroed) lifts the long side to the target:
    '   boost 3 → ls 7 / ss 11 → must emit SHORT (MEDIUM), not WEAK LONG
    '   boost 7 → ls 11 / ss 11 → tie carries no direction → NO TRADE [TIE]

    Private Function BuildA8Cfg(fundingBoost As Integer) As EngineSettings
        Dim cfg As New EngineSettings()
        cfg.RegimeWeights.Enabled = False              ' suppress Pass 2c
        cfg.MTFGate.Enabled = False                    ' isolate the cascade from the gate (A9 covers it)
        cfg.Indicators.OiCvd.Enabled = False           ' suppress Pass 2b
        cfg.Indicators.OFI.MomentumEnabled = False     ' suppress OFI momentum modifier
        cfg.Indicators.Funding.MomentumEnabled = False ' suppress Step 3b
        cfg.Scoring.FundingHighPenalty = 0             ' boost-only Step 3 arm
        cfg.Scoring.FundingHighBoost = fundingBoost
        Return cfg
    End Function

    Private Function BuildA8Indicators() As IndicatorResults
        Dim r As New IndicatorResults()
        r.Regime = "RANGE_BOUND"
        r.CurrentPrice = 100000
        ' v35 min-tradeable-move gate: give the cascade a tradeable ATR (placed
        ' fallback target 50×1.75=87.5 > floor 0.0008×100000=80; was 50×2.0=100
        ' pre-B4b) so the Step 5c gate doesn't veto the directional verdict this
        ' fixture asserts. Mirrors how this fixture disables MTF/Pass2b/2c to
        ' isolate the dominant-side cascade. (A12 already sets 50.)
        r.ATR = 50

        ' 11 short votes: ROC, RSI, DMI, Volume, VWAP, EMA ribbon, OI, OFI,
        ' CVD, TFI, MicroCVD.
        r.ROC = -0.5 : r.ROCSlope = "FALLING"
        r.RSI = 30 : r.RSIDivergence = "NONE"
        r.PlusDI = 10 : r.MinusDI = 25 : r.ADX = 15        ' DMI short; ADX below trend threshold
        r.VolumeRatio = 5                                   ' ≥ VolHighThreshold (3) below
        r.VWAP = 100100 : r.VWAPSigma1Lower = 99000 : r.VWAPSigma1Upper = 101000
        r.VWAPSigma2Lower = 98000 : r.VWAPSigma2Upper = 102000
        r.VWAPSessionCandles = 30                           ' past warmup
        r.EMAAlignment = "BEAR"
        r.OISignal = "NEW SHORTS"
        r.OFISignal = "SELL DOMINANT" : r.OFIMomentum = "FLAT"
        r.CVDSlope = "FALLING" : r.CVDValue = -50000 : r.CVDDivergence = "NONE"
        r.TFISignal = "SELL PRESSURE"
        r.MicroCVDSignal = "BEAR_ACCEL" : r.MicroCVDMomentum = "ACCELERATING"

        ' 4 long votes: BBW/TTM building, Donchian full LONG, OBV rising,
        ' price above the 5m EMA(200) anchor.
        r.SqueezeStatus = "NONE" : r.TTMSignal = "BULL_BUILDING" : r.TTMDirection = "RISING"
        r.DonchianSignal = "LONG"
        r.OBVTrend = "RISING" : r.OBVDivergence = "NONE"
        r.EMA200_5m = 90000

        ' Step 3 trigger: heavy negative funding → boost-only long lift.
        r.FundingRate = -0.0002 : r.FundingBias = "SHORTS HEAVILY CROWDED"
        r.FundingMomentum = "FLAT"

        ' Quiet everything else.
        r.SpreadStatus = "NORMAL"
        r.LiqSignal = "NONE"
        r.VPFRSignal = "NEUTRAL" : r.VPFRValueAreaSignal = "INSIDE_VA"
        r.MTF15mTrend = "FLAT" : r.MTFGatePassLong = True : r.MTFGatePassShort = True
        r.MTFGateDetails = "fixture"
        Return r
    End Function

    Private Function BuildA8Norms() As DynamicNorms
        Return New DynamicNorms With {
            .VolHighThreshold = 3, .VolMidThreshold = 1.5,
            .VolMean = 100, .VolStdDev = 50,
            .VWAPDevThreshold = 1, .ATRScaleFactor = 1, .ATRRef = 50, .IsLive = True}
    End Function

    Private Sub A8_DominantSideCascade()
        Dim v = ScoringEngine.Calculate(BuildA8Indicators(), PositionState.None,
                                        BuildA8Norms(), BuildA8Cfg(fundingBoost:=3))
        Check("A8 dominant-side cascade (ls7/ss11 → SHORT)",
              v.Verdict = "SHORT" AndAlso v.EffectiveLongScore = 7 AndAlso v.EffectiveShortScore = 11,
              String.Format("expected SHORT with eff 7/11, got '{0}' with eff {1}/{2}",
                            v.Verdict, v.EffectiveLongScore, v.EffectiveShortScore))

        Dim vTie = ScoringEngine.Calculate(BuildA8Indicators(), PositionState.None,
                                           BuildA8Norms(), BuildA8Cfg(fundingBoost:=7))
        Check("A8 tie (11/11 above weak → NO TRADE [TIE])",
              vTie.Verdict = "NO TRADE [TIE]" AndAlso vTie.EffectiveLongScore = 11 AndAlso vTie.EffectiveShortScore = 11,
              String.Format("expected NO TRADE [TIE] with eff 11/11, got '{0}' with eff {1}/{2}",
                            vTie.Verdict, vTie.EffectiveLongScore, vTie.EffectiveShortScore))
    End Sub

    ' -- A9: MTF per-side flags on a 15m BEAR fixture ---------------------------
    ' 70 steadily falling 15m candles → DMI bearish, ADX strong, EMA stack
    ' bearish → mtfTrend BEAR → long blocked, short passes.
    Private Sub A9_MtfPerSideFlags()
        Dim candles As New List(Of Candle)
        Dim p As Double = 110000
        For i As Integer = 0 To 69
            Dim c As New Candle With {
                .Timestamp = i, .Open = p, .High = p + 20,
                .Close = p - 100, .Low = p - 120, .Volume = 10}
            candles.Add(c)
            p = c.Close
        Next

        Dim trend As String = "", emaAlign As String = "", details As String = ""
        Dim adx As Double
        Dim passLong As Boolean, passShort As Boolean
        IndicatorEngine.CalcMTFGate(candles, trend, adx, emaAlign,
                                    passLong, passShort, details)

        Check("A9 MTF per-side flags (15m BEAR)",
              trend = "BEAR" AndAlso passLong = False AndAlso passShort = True,
              String.Format("expected BEAR/blockL/passS, got {0}/passL={1}/passS={2} ({3})",
                            trend, passLong, passShort, details))
    End Sub

    ' -- A10: Kelly inverse-contract sizing + leverage cap (D1/H4) --------------
    ' STRONG LONG / HIGH / stop distance 60 / entry 62,900 / POCO defaults.
    ' p=0.65, q=0.35, b=2.0/1.2=1.6667 → f*=0.44, half=0.22, applied=min(0.22,0.05)=0.05
    '   → KellyRiskUsd = 1000 × 0.05 = $50.
    ' riskPerContract = face×stop/entry = 10×60/62900 ≈ 0.009539 USD
    '   → risk-derived = floor(50 / 0.009539) = 5241 contracts.
    ' leverage cap = floor(account × maxLev / face) = floor(1000×5.0/10) = 500.
    ' min(5241, 500) = 500, leverage-bound → notional $5,000, 5.0× lev, LEV CAPPED.
    Private Sub A10_KellyInverseLeverage()
        Dim cfg As New EngineSettings()
        Dim v As New VerdictResult With {.Verdict = "STRONG LONG", .Confidence = "HIGH"}
        ScoringEngine.CalcKellySizing(v, stopDistanceUsd:=60, entryPriceUsd:=62900, cfg:=cfg)

        Check("A10 Kelly leverage-capped (inverse contract)",
              v.KellyContracts = 500 AndAlso v.KellyLevCapped = True AndAlso
              Math.Abs(v.KellyRiskUsd - 50.0) < 0.001,
              String.Format("expected 500 contracts / LEV CAPPED / risk $50, got {0} contracts / levCapped={1} / risk ${2:F2}",
                            v.KellyContracts, v.KellyLevCapped, v.KellyRiskUsd))
    End Sub

    ' -- A11: candle freshness guard (D5/S-6) ----------------------------------
    ' Last bar 30s old (within the 2×1min threshold) → fresh; 3min old (3× the
    ' 1m resolution, beyond 2×) → stale; empty list → not fresh.
    Private Function CandleAt(msEpoch As Long) As List(Of Candle)
        Return New List(Of Candle) From {
            New Candle With {.Open = 100000, .High = 100001, .Low = 99999,
                             .Close = 100000, .Volume = 10, .Timestamp = msEpoch}}
    End Function

    Private Sub A11_CandleFreshness()
        Dim nowDto As DateTimeOffset = DateTimeOffset.UtcNow
        Dim nowUtc As DateTime = nowDto.UtcDateTime
        Dim nowMs As Long = nowDto.ToUnixTimeMilliseconds()
        Dim freshMs As Long = nowMs - 30L * 1000          ' 30s old
        Dim staleMs As Long = nowMs - 3L * 60 * 1000      ' 3 minutes old = 3× the 1m resolution

        Check("A11 freshness (30s-old 1m bar → fresh)",
              IndicatorEngine.IsFresh(CandleAt(freshMs), 1, nowUtc) = True,
              "expected fresh for a 30s-old 1m bar")

        Check("A11 staleness (3min-old 1m bar → stale)",
              IndicatorEngine.IsFresh(CandleAt(staleMs), 1, nowUtc) = False,
              "expected stale for a 3min-old 1m bar (2× threshold = 2min)")

        Check("A11 empty list → not fresh",
              IndicatorEngine.IsFresh(New List(Of Candle)(), 1, nowUtc) = False,
              "expected not-fresh for an empty candle list")
    End Sub

    ' -- A12: linear ATR levels — Step 5b raw target carries no scale factor ----
    ' D2 (S-1): the Step 5b cap base is price + ATR × targetMult, with NO
    ' norms.ATRScaleFactor. price=100000, ATR=50, targetMult=2.0 (pinned),
    ' norms.ATRScaleFactor=2.0 (deliberately ≠ 1 to expose the old bug). The
    ' linear raw long target = 100000 + 50×2.0 = 100100; the old quadratic form
    ' would have been 100000 + 50×2.0×2.0 = 100200. A Tier-1 swing-high cap fires
    ' only when SwingTargetLong < rawLongTarget, so the cap boundary pins the raw
    ' target exactly:
    '   swing 100099 (< 100100) → caps → AdjustedLongTarget = 100099
    '   swing 100101 (> 100100) → no cap → AdjustedLongTarget = 0
    ' Under the old quadratic geometry the boundary would sit at 100200 and BOTH
    ' would cap; the bracket proves the scale factor is absent.
    ' [B4b] This fixture pins the LEGACY closest-wins cap, so it runs with
    ' structural_levels.enabled=false + the pre-B4b multipliers pinned explicitly —
    ' doubling as part of the enabled:false rollback (byte-identical) proof. The
    ' structural-first behaviour (swing 100101 would PLACE, not stay uncapped) is
    ' pinned by A26a.
    Private Function BuildA12Indicators(swingTargetLong As Double) As IndicatorResults
        Dim r = BuildA8Indicators()
        r.ATR = 50
        r.CurrentPrice = 100000
        r.SwingTargetLong = swingTargetLong
        Return r
    End Function

    Private Function BuildA12Norms() As DynamicNorms
        Dim n = BuildA8Norms()
        n.ATRScaleFactor = 2.0                 ' ≠ 1 — a no-op under linear geometry
        Return n
    End Function

    Private Sub A12_LinearLevels()
        Dim cfg = BuildA8Cfg(fundingBoost:=0)
        cfg.Scoring.StructuralLevels.Enabled = False   ' legacy-geometry pin (B4b rollback path)
        cfg.Scoring.AtrTargetMultiplier = 2.0          ' pre-B4b multipliers, pinned
        cfg.Scoring.AtrStopMultiplier = 1.2

        Dim vIn = ScoringEngine.Calculate(BuildA12Indicators(100099), PositionState.None,
                                          BuildA12Norms(), cfg)
        Check("A12 linear levels (swing 100099 inside linear target → capped)",
              Math.Abs(vIn.AdjustedLongTarget - 100099) < 0.001,
              String.Format("expected AdjustedLongTarget=100099 (raw target 100100), got {0:F1}",
                            vIn.AdjustedLongTarget))

        Dim vOut = ScoringEngine.Calculate(BuildA12Indicators(100101), PositionState.None,
                                           BuildA12Norms(), cfg)
        Check("A12 linear levels (swing 100101 beyond linear target → uncapped)",
              vOut.AdjustedLongTarget = 0,
              String.Format("expected AdjustedLongTarget=0 (raw target 100100, scale absent), got {0:F1} — old quadratic geometry would cap here",
                            vOut.AdjustedLongTarget))
    End Sub

    ' -- A13: minimum-tradeable-move gate (v35) --------------------------------
    ' Reuses the A8 RANGE_BOUND fixture (11 short / 4 long → SHORT MEDIUM at
    ' regimeMax 18 with RegimeWeights/MTF/OiCvd off) but re-anchors the
    ' price-relative reference levels around a configurable entry price so the
    ' SHORT-dominant structure is preserved at $62k. The gate (Step 5c, end of
    ' Calculate()) fires purely on (directional verdict, dominant side, ATR,
    ' price, EFFECTIVE post-cap target). At the POCO-default floor:
    '   [v62] floor = TradeCosts.EffectiveMinMovePct × 62000
    '               = (2 × 1.5bps + min_net 0.0005) × 62000 = 0.0008 × 62000 = 49.6
    '   — the composed default reproduces the retired flat v35 floor exactly, which is
    '   why every A13 number below is unchanged.
    Private Function BuildGateIndicators(atr As Double, price As Double) As IndicatorResults
        Dim r = BuildA8Indicators()
        r.CurrentPrice = price
        r.ATR = atr
        ' Re-anchor around the new price (A8 placed these around 100k). Preserves
        ' price < VWAP (short vote), inside-σ1, and price > 5m EMA(200) (the lone
        ' long vote) so the cascade still resolves SHORT MEDIUM.
        r.VWAP = price + 100
        r.VWAPSigma1Lower = price - 1000 : r.VWAPSigma1Upper = price + 1000
        r.VWAPSigma2Lower = price - 2000 : r.VWAPSigma2Upper = price + 2000
        r.EMA200_5m = price - 10000
        Return r
    End Function

    Private Sub A13_MinTradeableMoveGate()
        ' [B4b] Runs on the live geometry (structural-first enabled, fallback ×1.75 —
        ' the fixtures carry no swing/HVN levels except A13c, so the placed target is
        ' the ATR fallback). The gate now evaluates the PLACED target from Step 5b.
        Dim cfg = BuildA8Cfg(fundingBoost:=0)   ' SHORT dominant; floor = POCO default 0.0008

        ' A13a — low ATR: fallback short target 13×1.75=22.75 < floor 49.6 → gate fires.
        Dim vLow = ScoringEngine.Calculate(BuildGateIndicators(atr:=13, price:=62000),
                                           PositionState.None, BuildA8Norms(), cfg)
        Check("A13a low-ATR veto (placed target 22.75 < floor 49.6 → NO TRADE)",
              vLow.Verdict = "NO TRADE" AndAlso vLow.VerdictContext = "BELOW_MIN_MOVE",
              String.Format("expected NO TRADE / BELOW_MIN_MOVE, got '{0}' / {1}", vLow.Verdict, vLow.VerdictContext))

        ' A13b — tradeable ATR: fallback short target 30×1.75=52.5 > floor 49.6 → stands.
        Dim vOk = ScoringEngine.Calculate(BuildGateIndicators(atr:=30, price:=62000),
                                          PositionState.None, BuildA8Norms(), cfg)
        Check("A13b tradeable-ATR (placed target 52.5 > floor 49.6 → directional stands)",
              Not vOk.Verdict.StartsWith("NO TRADE") AndAlso vOk.VerdictContext <> "BELOW_MIN_MOVE",
              String.Format("expected directional SHORT, got '{0}' / {1}", vOk.Verdict, vOk.VerdictContext))

        ' A13c — near structural target (validates the PLACED-target choice): high ATR
        ' so the fallback (100×1.75=175) clears the floor, but the arbitration places
        ' the swing target 30 points from entry (within the 3.5×ATR bound) → gate fires.
        Dim rNear = BuildGateIndicators(atr:=100, price:=62000)
        rNear.SwingTargetShort = 61970          ' 30 below entry; dist ≤ 350 bound → placed
        Dim vNear = ScoringEngine.Calculate(rNear, PositionState.None, BuildA8Norms(), cfg)
        Check("A13c near-swing placed veto (placed target 30 < floor 49.6 → NO TRADE)",
              vNear.Verdict = "NO TRADE" AndAlso vNear.VerdictContext = "BELOW_MIN_MOVE" AndAlso
              Math.Abs(vNear.AdjustedShortTarget - 61970) < 0.001,
              String.Format("expected NO TRADE / BELOW_MIN_MOVE / placed 61970, got '{0}' / {1} / placed {2:F1}",
                            vNear.Verdict, vNear.VerdictContext, vNear.AdjustedShortTarget))

        ' A13d — editability: [v62] turn the trader-owned half of the composition to zero,
        ' which collapses the floor onto the pure round-trip fee 0.0003 (18.6) — the same
        ' number this fixture has always pinned. A13a's 22.75-pt target now clears → the
        ' shared resolver drives the gate (hot-reloadable in-app via the SETTINGS & TOOLS row).
        Dim cfgLow = BuildA8Cfg(fundingBoost:=0)
        cfgLow.Scoring.TradeCosts.MinNetMovePct = 0.0
        Dim vEdit = ScoringEngine.Calculate(BuildGateIndicators(atr:=13, price:=62000),
                                            PositionState.None, BuildA8Norms(), cfgLow)
        Check("A13d editability (floor 18.6 < target 22.75 → directional stands)",
              Not vEdit.Verdict.StartsWith("NO TRADE") AndAlso vEdit.VerdictContext <> "BELOW_MIN_MOVE",
              String.Format("expected directional SHORT at lowered floor, got '{0}' / {1}", vEdit.Verdict, vEdit.VerdictContext))
    End Sub

    ' =======================================================================
    ' A14 — session-conditional execution timeframe (v36 Phase 1)
    ' docs/session-timeframe-resolution-implementer-handoff.md §8.
    ' =======================================================================

    ''' <summary>cfg with ASIA/LONDON = 3-min, NY = 1-min, and the "3" ROC profile.</summary>
    Private Function BuildResolutionCfg() As EngineSettings
        Dim cfg As New EngineSettings()
        cfg.SessionVolume.Enabled = True
        cfg.SessionVolume.Sessions = New List(Of SessionBucketSettings) From {
            New SessionBucketSettings With {.Name = "ASIA",   .StartHour = 0,  .EndHour = 7,  .ExecutionResolution = 3},
            New SessionBucketSettings With {.Name = "LONDON", .StartHour = 8,  .EndHour = 12, .ExecutionResolution = 3},
            New SessionBucketSettings With {.Name = "NY",     .StartHour = 13, .EndHour = 23, .ExecutionResolution = 1}}
        cfg.ResolutionProfiles = New Dictionary(Of String, ResolutionProfile) From {
            {"1", New ResolutionProfile()},
            {"3", New ResolutionProfile With {.RocMagnitudeThreshold = 0.21, .RocSlopeDeltaThreshold = 0.105}}}
        Return cfg
    End Function

    ''' <summary>Candles whose per-bar true range = rangePts (flat closes), so ATR(7) ≈ rangePts.</summary>
    Private Function FlatRangeCandles(count As Integer, center As Double, rangePts As Double) As List(Of Candle)
        Dim cs As New List(Of Candle)
        For i As Integer = 0 To count - 1
            cs.Add(New Candle With {
                .Timestamp = i, .Open = center, .Close = center,
                .High = center + rangePts / 2, .Low = center - rangePts / 2, .Volume = 10})
        Next
        Return cs
    End Function

    ' -- A14a: resolution selection on the engine bucket (incl. hour-7 ASIA inclusive) --
    Private Sub A14a_ResolutionSelection()
        Dim cfg = BuildResolutionCfg()
        Check("A14a resolution selection (ASIA hr3/hr7→3, LONDON hr10→3, NY hr13/hr23→1)",
              ExecutionResolution.ResolveResolution(cfg, 3) = 3 AndAlso
              ExecutionResolution.ResolveResolution(cfg, 7) = 3 AndAlso
              ExecutionResolution.ResolveResolution(cfg, 10) = 3 AndAlso
              ExecutionResolution.ResolveResolution(cfg, 13) = 1 AndAlso
              ExecutionResolution.ResolveResolution(cfg, 23) = 1,
              String.Format("got hr3={0} hr7={1} hr10={2} hr13={3} hr23={4}",
                            ExecutionResolution.ResolveResolution(cfg, 3),
                            ExecutionResolution.ResolveResolution(cfg, 7),
                            ExecutionResolution.ResolveResolution(cfg, 10),
                            ExecutionResolution.ResolveResolution(cfg, 13),
                            ExecutionResolution.ResolveResolution(cfg, 23)))
    End Sub

    ' -- A14b: ROC override resolves through the profile map -------------------
    Private Sub A14b_RocOverrideResolves()
        Dim cfg = BuildResolutionCfg()
        Check("A14b ROC override (mag 3→0.21 / 1→0.1; slope 3→0.105 / 1→0.05)",
              Math.Abs(ExecutionResolution.ResolveRocMagnitude(cfg, 3) - 0.21) < 0.0000001 AndAlso
              Math.Abs(ExecutionResolution.ResolveRocMagnitude(cfg, 1) - 0.1) < 0.0000001 AndAlso
              Math.Abs(ExecutionResolution.ResolveRocSlopeDelta(cfg, 3) - 0.105) < 0.0000001 AndAlso
              Math.Abs(ExecutionResolution.ResolveRocSlopeDelta(cfg, 1) - 0.05) < 0.0000001,
              String.Format("got mag3={0} mag1={1} slope3={2} slope1={3}",
                            ExecutionResolution.ResolveRocMagnitude(cfg, 3),
                            ExecutionResolution.ResolveRocMagnitude(cfg, 1),
                            ExecutionResolution.ResolveRocSlopeDelta(cfg, 3),
                            ExecutionResolution.ResolveRocSlopeDelta(cfg, 1)))
    End Sub

    ' -- A14c: ATR computed on 3-min candles clears the gate 1-min ATR can't ---
    ' [B4b refit] The placed fallback target is now ATR×1.75 (was ×2.0), so the
    ' 3-min bar range moves 27 → 32 to stay above the gate floor (32×1.75 = 56 >
    ' 49.6; the old 27×1.75 = 47.25 would gate). The point under test is unchanged:
    ' the SAME session at 3-min resolution carries a tradeable ATR the 1-min
    ' chart can't (13×1.75 = 22.75 < 49.6).
    Private Sub A14c_AtrOn3MinGateFlip()
        Dim atr1m As Double = IndicatorEngine.CalcATR(FlatRangeCandles(30, 62000, 13), 7)
        Dim atr3m As Double = IndicatorEngine.CalcATR(FlatRangeCandles(30, 62000, 32), 7)

        Dim cfg = BuildA8Cfg(fundingBoost:=0)
        Dim vLow = ScoringEngine.Calculate(BuildGateIndicators(atr1m, 62000),
                                           PositionState.None, BuildA8Norms(), cfg)
        Dim vHigh = ScoringEngine.Calculate(BuildGateIndicators(atr3m, 62000),
                                            PositionState.None, BuildA8Norms(), cfg)

        Check("A14c ATR on 3-min clears the gate the 1-min ATR can't",
              Math.Abs(atr1m - 13) < 1.0 AndAlso Math.Abs(atr3m - 32) < 1.0 AndAlso
              vLow.Verdict = "NO TRADE" AndAlso vLow.VerdictContext = "BELOW_MIN_MOVE" AndAlso
              Not vHigh.Verdict.StartsWith("NO TRADE"),
              String.Format("atr1m={0:F1}(→{1}) atr3m={2:F1}(→{3})", atr1m, vLow.Verdict, atr3m, vHigh.Verdict))
    End Sub

    ' -- A14d: NY byte-identical guard (res=1 ⇒ ROC overrides == globals) ------
    Private Sub A14d_NyByteIdentical()
        Dim cfg = BuildResolutionCfg()
        Check("A14d NY byte-identical guard (res=1; ROC overrides == globals)",
              ExecutionResolution.ResolveResolution(cfg, 15) = 1 AndAlso
              ExecutionResolution.ResolveRocMagnitude(cfg, 1) = cfg.Indicators.ROC.MagnitudeThreshold AndAlso
              ExecutionResolution.ResolveRocSlopeDelta(cfg, 1) = cfg.Indicators.ROC.SlopeDeltaThreshold,
              "expected res=1 + ROC overrides equal to the global ROC thresholds at NY")
    End Sub

    ' -- A14j: per-session ROC magnitude override (B re-baseline 2026-06-20) ---
    Private Sub A14j_PerSessionRocMagnitude()
        Dim cfg = BuildResolutionCfg()
        ' Per-session magnitude overrides on the buckets (ASIA 0.17 / LONDON 0.11; NY none).
        cfg.SessionVolume.Sessions(0).RocMagnitudeThreshold = 0.17   ' ASIA  hr 0-7 (v41 Monday recal)
        cfg.SessionVolume.Sessions(1).RocMagnitudeThreshold = 0.11   ' LONDON hr 8-12
        ' NY (index 2) left Nothing → ResolveRocMagnitudeForHour falls back to base.
        Dim asia   As Double = ExecutionResolution.ResolveRocMagnitudeForHour(cfg, 6)
        Dim london As Double = ExecutionResolution.ResolveRocMagnitudeForHour(cfg, 10)
        Dim ny     As Double = ExecutionResolution.ResolveRocMagnitudeForHour(cfg, 15)
        Check("A14j per-session ROC magnitude (ASIA 6→0.17 / LONDON 10→0.11 / NY 15→base)",
              Math.Abs(asia - 0.17) < 0.0000001 AndAlso
              Math.Abs(london - 0.11) < 0.0000001 AndAlso
              Math.Abs(ny - cfg.Indicators.ROC.MagnitudeThreshold) < 0.0000001,
              String.Format("got asia={0} london={1} ny={2} (base={3})",
                            asia, london, ny, cfg.Indicators.ROC.MagnitudeThreshold))
    End Sub

    ' -- A14e: per-row stamp value (ASIA→3, NY→1) + legacy-safe defaults -------
    Private Sub A14e_PerRowStamp()
        Dim cfg = BuildResolutionCfg()
        Dim freshR As New IndicatorResults()
        Dim freshE As New LivePerformanceTracker.EvalCacheEntry()
        Check("A14e per-row stamp (ASIA→3, NY→1; container defaults 1)",
              ExecutionResolution.ResolveResolution(cfg, 3) = 3 AndAlso
              ExecutionResolution.ResolveResolution(cfg, 15) = 1 AndAlso
              freshR.ExecResolution = 1 AndAlso freshE.ExecResolution = 1,
              String.Format("got asia={0} ny={1} rDefault={2} eDefault={3}",
                            ExecutionResolution.ResolveResolution(cfg, 3),
                            ExecutionResolution.ResolveResolution(cfg, 15),
                            freshR.ExecResolution, freshE.ExecResolution))
    End Sub

    Private Function EvalRow(tsHour As Integer, outcome As String, res As Integer) As LivePerformanceTracker.EvalCacheEntry
        Return New LivePerformanceTracker.EvalCacheEntry With {
            .Timestamp = New DateTime(2026, 1, 1, tsHour, 0, 0, DateTimeKind.Utc),
            .Verdict = "LONG", .EvalOutcome = outcome, .ExecResolution = res}
    End Function

    ' -- A14f: one session window, two resolutions → two sub-populations -------
    Private Sub A14f_ResolutionFilteredAggregation()
        Dim entries As New List(Of LivePerformanceTracker.EvalCacheEntry) From {
            EvalRow(1, "SUCCESS", 3), EvalRow(2, "SUCCESS", 3), EvalRow(3, "ADVERSE_HIT", 3),
            EvalRow(4, "SUCCESS", 1), EvalRow(5, "ADVERSE_HIT", 1)}
        Dim lo As DateTime = New DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        Dim hi As DateTime = New DateTime(2026, 1, 1, 23, 0, 0, DateTimeKind.Utc)

        Dim a3 = LivePerformanceTracker.AggregateRange(entries, lo, hi, 3)
        Dim a1 = LivePerformanceTracker.AggregateRange(entries, lo, hi, 1)
        Dim aAll = LivePerformanceTracker.AggregateRange(entries, lo, hi, 0)

        Check("A14f resolution-filtered aggregation (3-min 2/1, 1-min 1/1, never blended)",
              a3.SuccessCount = 2 AndAlso a3.FailureCount = 1 AndAlso a3.TotalRange = 3 AndAlso
              a1.SuccessCount = 1 AndAlso a1.FailureCount = 1 AndAlso a1.TotalRange = 2 AndAlso
              aAll.SuccessCount = 3 AndAlso aAll.FailureCount = 2 AndAlso aAll.TotalRange = 5,
              String.Format("res3={0}/{1}/{2} res1={3}/{4}/{5} all={6}/{7}/{8}",
                            a3.SuccessCount, a3.FailureCount, a3.TotalRange,
                            a1.SuccessCount, a1.FailureCount, a1.TotalRange,
                            aAll.SuccessCount, aAll.FailureCount, aAll.TotalRange))
    End Sub

    ' -- A14g: freshness honours the execution resolution ---------------------
    Private Sub A14g_FreshnessHonoursResolution()
        Dim nowDto As DateTimeOffset = DateTimeOffset.UtcNow
        Dim nowUtc As DateTime = nowDto.UtcDateTime
        Dim nowMs As Long = nowDto.ToUnixTimeMilliseconds()
        Dim fiveMinMs  As Long = nowMs - 5L * 60 * 1000
        Dim sevenMinMs As Long = nowMs - 7L * 60 * 1000
        Dim threeMinMs As Long = nowMs - 3L * 60 * 1000

        Check("A14g freshness honours resolution (3m 5-min fresh / 7-min stale; 1m 3-min stale)",
              IndicatorEngine.IsFresh(CandleAt(fiveMinMs), 3, nowUtc) = True AndAlso
              IndicatorEngine.IsFresh(CandleAt(sevenMinMs), 3, nowUtc) = False AndAlso
              IndicatorEngine.IsFresh(CandleAt(threeMinMs), 1, nowUtc) = False,
              "expected 3m 5-min fresh / 7-min stale, and 1m 3-min stale (unchanged)")
    End Sub

    ' -- A14h: 5m regime + 15m MTF resolution-independent (unchanged) ----------
    Private Sub A14h_RegimeMtfUnchanged()
        Dim candles As New List(Of Candle)
        Dim p As Double = 110000
        For i As Integer = 0 To 69
            Dim c As New Candle With {
                .Timestamp = i, .Open = p, .High = p + 20,
                .Close = p - 100, .Low = p - 120, .Volume = 10}
            candles.Add(c)
            p = c.Close
        Next
        Dim trend As String = "", emaAlign As String = "", details As String = ""
        Dim adx As Double
        Dim passLong As Boolean, passShort As Boolean
        IndicatorEngine.CalcMTFGate(candles, trend, adx, emaAlign, passLong, passShort, details)

        Check("A14h regime/MTF unchanged (15m gate resolution-independent)",
              trend = "BEAR" AndAlso passLong = False AndAlso passShort = True,
              String.Format("expected BEAR/blockL/passS, got {0}/passL={1}/passS={2}", trend, passLong, passShort))
    End Sub

    ' -- A14i: resolution selection independent of session_volume.enabled ------
    Private Sub A14i_ResolutionSurvivesSessionVolumeDisabled()
        Dim cfg = BuildResolutionCfg()
        cfg.SessionVolume.Enabled = False
        Check("A14i resolution survives session_volume disabled (ASIA still 3)",
              ExecutionResolution.ResolveResolution(cfg, 3) = 3,
              String.Format("expected 3 with session_volume disabled, got {0}",
                            ExecutionResolution.ResolveResolution(cfg, 3)))
    End Sub

    ' =======================================================================
    ' A15 — auto-tweaker (session × resolution) population filter (v36 Phase-2a)
    ' docs/auto-tweaker-session-resolution-filter-implementer-handoff.md §7.
    ' =======================================================================

    ''' <summary>A CsvRow at the given UTC hour with the given execution resolution.</summary>
    Private Function PopRow(hour As Integer, res As Integer) As CsvRow
        Return New CsvRow With {
            .Timestamp = New DateTime(2026, 1, 1, hour, 0, 0, DateTimeKind.Utc),
            .Verdict = "LONG", .ExecResolution = res}
    End Function

    ''' <summary>Interleaved NY(res 1, hours 13-23) + ASIA(res 3, hours 0-7) rows.</summary>
    Private Function InterleavedPopRows() As List(Of CsvRow)
        Return New List(Of CsvRow) From {
            PopRow(13, 1), PopRow(3, 3), PopRow(20, 1),
            PopRow(7, 3), PopRow(23, 1), PopRow(0, 3)}
    End Function

    ' -- A15a: filter keeps only the NY res-1 rows -----------------------------
    Private Sub A15a_FilterExcludesNonMatching()
        Dim settings = BuildResolutionCfg()
        Dim pop As New PopulationFilter With {.Session = "NY", .ExecutionResolution = 1}
        Dim kept = InterleavedPopRows().
            Where(Function(r) AutoTweakerCore.MatchesPopulation(r, pop, settings)).ToList()

        Check("A15a filter excludes non-matching (NY×1 keeps 3, no res-3 survives)",
              kept.Count = 3 AndAlso Not kept.Any(Function(r) r.ExecResolution = 3),
              String.Format("kept {0} rows; res-3 survivors={1}",
                            kept.Count, kept.Where(Function(r) r.ExecResolution = 3).Count))
    End Sub

    ' -- A15b: every surviving row is resolution-homogeneous (res 1) -----------
    Private Sub A15b_ResolutionHomogeneity()
        Dim settings = BuildResolutionCfg()
        Dim pop As New PopulationFilter With {.Session = "NY", .ExecutionResolution = 1}
        Dim kept = InterleavedPopRows().
            Where(Function(r) AutoTweakerCore.MatchesPopulation(r, pop, settings)).ToList()

        Check("A15b resolution homogeneity (all NY×1 survivors have ExecResolution=1)",
              kept.Count > 0 AndAlso kept.All(Function(r) r.ExecResolution = 1),
              "expected every surviving row to have ExecResolution=1")
    End Sub

    ' -- A15c: a legacy v0.6 row (no ExecResolution column) defaults to res 1 --
    Private Sub A15c_LegacyRowDefaultsRes1()
        Dim path As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ordercheck_a15c_" & Guid.NewGuid().ToString("N") & ".csv")
        Try
            ' v0.6 header — no ExecResolution column. NY-hour timestamp (15:00 UTC).
            System.IO.File.WriteAllText(path,
                "Timestamp,Price,Verdict" & vbCrLf &
                "2026-01-01 15:00:00,100000,LONG" & vbCrLf)
            Dim rows = ForwardWindowJoiner.Load(path)
            Dim settings = BuildResolutionCfg()
            Dim pop As New PopulationFilter With {.Session = "NY", .ExecutionResolution = 1}

            Check("A15c legacy v0.6 row defaults ExecResolution=1 and passes NY×1",
                  rows.Count = 1 AndAlso rows(0).ExecResolution = 1 AndAlso
                  AutoTweakerCore.MatchesPopulation(rows(0), pop, settings),
                  String.Format("rows={0} res={1}", rows.Count,
                                If(rows.Count > 0, rows(0).ExecResolution, -1)))
        Finally
            Try : System.IO.File.Delete(path) : Catch : End Try
        End Try
    End Sub

    ' -- A15d: session derivation == engine bucket (guards the hour-7 off-by-one) --
    Private Sub A15d_SessionDerivationEqualsEngineBucket()
        Dim cfg = BuildResolutionCfg()
        Dim h7 = ExecutionResolution.MatchSessionBucket(cfg, 7)?.Name
        Dim h12 = ExecutionResolution.MatchSessionBucket(cfg, 12)?.Name
        Dim h13 = ExecutionResolution.MatchSessionBucket(cfg, 13)?.Name

        Check("A15d session derivation (hr7→ASIA, hr12→LONDON, hr13→NY)",
              h7 = "ASIA" AndAlso h12 = "LONDON" AndAlso h13 = "NY",
              String.Format("got hr7={0} hr12={1} hr13={2}", h7, h12, h13))
    End Sub

    ' -- A15e: re-seed on filter change (ASIA|3 → NY×1) ------------------------
    ' Drives the real RunAsync: with a stale PopulationFilterKey, the key-mismatch
    ' path re-seeds LastEvaluatedRowIndex to filtered.Count and returns INELIGIBLE
    ' before any windowing/fetch. Throwaway temp config+state+CSV+settings.
    Private Sub A15e_ReseedOnFilterChange()
        Dim dir As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ordercheck_a15e_" & Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory(dir)
        Try
            Dim csvPath   As String = System.IO.Path.Combine(dir, "analysis_log.csv")
            Dim setPath   As String = System.IO.Path.Combine(dir, "settings.json")
            Dim statePath As String = System.IO.Path.Combine(dir, "state.json")

            ' 3 NY res-1 rows + 2 ASIA res-3 rows → filtered (NY×1).Count = 3.
            System.IO.File.WriteAllText(csvPath,
                "Timestamp,Price,Verdict,ExecResolution" & vbCrLf &
                "2026-01-01 14:00:00,100000,LONG,1" & vbCrLf &
                "2026-01-01 03:00:00,100000,LONG,3" & vbCrLf &
                "2026-01-01 15:00:00,100000,LONG,1" & vbCrLf &
                "2026-01-01 04:00:00,100000,LONG,3" & vbCrLf &
                "2026-01-01 16:00:00,100000,LONG,1" & vbCrLf)

            System.IO.File.WriteAllText(setPath,
                "{""version"":1,""session_volume"":{""sessions"":[" &
                "{""name"":""ASIA"",""start_hour"":0,""end_hour"":7,""execution_resolution"":3}," &
                "{""name"":""LONDON"",""start_hour"":8,""end_hour"":12,""execution_resolution"":3}," &
                "{""name"":""NY"",""start_hour"":13,""end_hour"":23,""execution_resolution"":1}]}}")

            Dim cfg As New TweakerConfig With {
                .WindowMode = TweakerConfig.WindowModeFixed,
                .CsvPath = csvPath, .SettingsPath = setPath, .StatePath = statePath,
                .DryRunEnabled = True,
                .PopulationFilter = New PopulationFilter With {.Session = "NY", .ExecutionResolution = 1}}
            Dim st As New TweakerState With {
                .PopulationFilterKey = "ASIA|3", .LastEvaluatedRowIndex = 999}

            Dim rc As Integer = AutoTweakerCore.RunAsync(cfg, st, statePath).GetAwaiter().GetResult()

            ' Key now carries the "|WD" weekday-filter term (docs/autotweaker-weekday-filter
            ' -proposal.md D-1); all 5 rows are Thursday 2026-01-01, so the weekday filter
            ' does not change filtered.Count=3, only the key string.
            Check("A15e re-seed on filter change (ASIA|3 → NY|1|WD: index→3, INELIGIBLE, nothing evaluated)",
                  rc = 2 AndAlso st.LastEvaluatedRowIndex = 3 AndAlso
                  st.PopulationFilterKey = "NY|1|WD" AndAlso st.LastRunOutcome = "INELIGIBLE",
                  String.Format("rc={0} idx={1} key={2} outcome={3}",
                                rc, st.LastEvaluatedRowIndex, st.PopulationFilterKey, st.LastRunOutcome))
        Finally
            Try : System.IO.Directory.Delete(dir, True) : Catch : End Try
        End Try
    End Sub

    ' =======================================================================
    ' A59 — AutoTweaker weekday-only row filter
    ' docs/autotweaker-weekday-filter-proposal.md §6.
    ' Reference week: 2026-01-01=Thu, 01-02=Fri, 01-03=Sat, 01-04=Sun, 01-05=Mon.
    ' =======================================================================

    ' -- A59a: weekday rows survive, Saturday and Sunday rows are excluded -----
    Private Sub A59a_WeekdayRowsSurviveWeekendExcluded()
        Dim rows As New List(Of CsvRow) From {
            New CsvRow With {.Timestamp = New DateTime(2026, 1, 1, 12, 0, 0)}, ' Thu
            New CsvRow With {.Timestamp = New DateTime(2026, 1, 2, 12, 0, 0)}, ' Fri
            New CsvRow With {.Timestamp = New DateTime(2026, 1, 3, 12, 0, 0)}, ' Sat
            New CsvRow With {.Timestamp = New DateTime(2026, 1, 4, 12, 0, 0)}, ' Sun
            New CsvRow With {.Timestamp = New DateTime(2026, 1, 5, 12, 0, 0)}} ' Mon
        Dim kept = rows.Where(Function(r) AutoTweakerCore.MatchesWeekday(r)).ToList()

        Check("A59a weekday rows survive, Sat/Sun excluded (5 rows -> 3 kept)",
              kept.Count = 3 AndAlso
              Not kept.Any(Function(r) r.Timestamp.DayOfWeek = DayOfWeek.Saturday OrElse
                                        r.Timestamp.DayOfWeek = DayOfWeek.Sunday),
              String.Format("kept={0}/{1}", kept.Count, rows.Count))
    End Sub

    ' -- A59b: the "|WD" key term triggers exactly one re-seed off the LIVE key --
    ' Pre-seeds population_filter_key="NY|1" — the value shipped before this change —
    ' so the fixture proves the deploy-day re-seed fires, not just a fresh-config one.
    Private Sub A59b_ReseedOnWeekdayKeyChange()
        Dim dir As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ordercheck_a59b_" & Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory(dir)
        Try
            Dim csvPath   As String = System.IO.Path.Combine(dir, "analysis_log.csv")
            Dim setPath   As String = System.IO.Path.Combine(dir, "settings.json")
            Dim statePath As String = System.IO.Path.Combine(dir, "state.json")

            ' 5 NY res-1 rows, one per day Thu..Mon — filtered (NY×1, weekday) drops
            ' Sat/Sun, so filtered.Count = 3.
            System.IO.File.WriteAllText(csvPath,
                "Timestamp,Price,Verdict,ExecResolution" & vbCrLf &
                "2026-01-01 14:00:00,100000,LONG,1" & vbCrLf &
                "2026-01-02 15:00:00,100000,LONG,1" & vbCrLf &
                "2026-01-03 16:00:00,100000,LONG,1" & vbCrLf &
                "2026-01-04 17:00:00,100000,LONG,1" & vbCrLf &
                "2026-01-05 18:00:00,100000,LONG,1" & vbCrLf)

            System.IO.File.WriteAllText(setPath,
                "{""version"":1,""session_volume"":{""sessions"":[" &
                "{""name"":""ASIA"",""start_hour"":0,""end_hour"":7,""execution_resolution"":3}," &
                "{""name"":""LONDON"",""start_hour"":8,""end_hour"":12,""execution_resolution"":3}," &
                "{""name"":""NY"",""start_hour"":13,""end_hour"":23,""execution_resolution"":1}]}}")

            Dim cfg As New TweakerConfig With {
                .WindowMode = TweakerConfig.WindowModeFixed,
                .CsvPath = csvPath, .SettingsPath = setPath, .StatePath = statePath,
                .DryRunEnabled = True,
                .PopulationFilter = New PopulationFilter With {.Session = "NY", .ExecutionResolution = 1}}
            Dim st As New TweakerState With {
                .PopulationFilterKey = "NY|1", .LastEvaluatedRowIndex = 999}

            Dim rc As Integer = AutoTweakerCore.RunAsync(cfg, st, statePath).GetAwaiter().GetResult()

            Check("A59b re-seed on weekday-key change (NY|1 -> NY|1|WD: index->3, INELIGIBLE, nothing evaluated)",
                  rc = 2 AndAlso st.LastEvaluatedRowIndex = 3 AndAlso
                  st.PopulationFilterKey = "NY|1|WD" AndAlso st.LastRunOutcome = "INELIGIBLE",
                  String.Format("rc={0} idx={1} key={2} outcome={3}",
                                rc, st.LastEvaluatedRowIndex, st.PopulationFilterKey, st.LastRunOutcome))
        Finally
            Try : System.IO.Directory.Delete(dir, True) : Catch : End Try
        End Try
    End Sub

    ' -- A59c: pins the D-2 burn — a window spanning a weekend after filtering ---
    ' emits SKIPPED_SESSION_BOUNDARY. No Thursday row: the CSV starts on Friday so
    ' filtered[0..1] is exactly [Fri, Mon] under the fix. Asserting on the absolute
    ' WindowStartRow/WindowEndRow (not just the outcome string) makes this
    ' mutation-sensitive to the weekday guard: if MatchesWeekday were reverted,
    ' filtered[0..1] would be [Fri, Sat] instead and WindowEndRow would read 1, not 3.
    Private Sub A59c_WeekendGapTripsSessionBoundary()
        Dim dir As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ordercheck_a59c_" & Guid.NewGuid().ToString("N"))
        System.IO.Directory.CreateDirectory(dir)
        Try
            Dim csvPath   As String = System.IO.Path.Combine(dir, "analysis_log.csv")
            Dim setPath   As String = System.IO.Path.Combine(dir, "settings.json")
            Dim statePath As String = System.IO.Path.Combine(dir, "state.json")

            System.IO.File.WriteAllText(csvPath,
                "Timestamp,Price,Verdict,ExecResolution" & vbCrLf &
                "2026-01-02 14:00:00,100000,LONG,1" & vbCrLf &  ' Fri — CsvRow.Index=0
                "2026-01-03 14:00:00,100000,LONG,1" & vbCrLf &  ' Sat — excluded, Index=1
                "2026-01-04 14:00:00,100000,LONG,1" & vbCrLf &  ' Sun — excluded, Index=2
                "2026-01-05 14:00:00,100000,LONG,1" & vbCrLf)   ' Mon — Index=3

            System.IO.File.WriteAllText(setPath,
                "{""version"":1,""session_volume"":{""sessions"":[" &
                "{""name"":""ASIA"",""start_hour"":0,""end_hour"":7,""execution_resolution"":3}," &
                "{""name"":""LONDON"",""start_hour"":8,""end_hour"":12,""execution_resolution"":3}," &
                "{""name"":""NY"",""start_hour"":13,""end_hour"":23,""execution_resolution"":1}]}}")

            Dim cfg As New TweakerConfig With {
                .WindowMode = TweakerConfig.WindowModeFixed,
                .WindowSizeVerdicts = 2,
                .CsvPath = csvPath, .SettingsPath = setPath, .StatePath = statePath,
                .DryRunEnabled = True,
                .PopulationFilter = New PopulationFilter With {.Session = "NY", .ExecutionResolution = 1}}
            ' Key already matches post-fix — this run must reach the boundary check,
            ' not the re-seed gate.
            Dim st As New TweakerState With {
                .PopulationFilterKey = "NY|1|WD", .LastEvaluatedRowIndex = 0}

            Dim rc As Integer = AutoTweakerCore.RunAsync(cfg, st, statePath).GetAwaiter().GetResult()
            Dim lastRound = st.RoundHistory.LastOrDefault()

            Check("A59c weekend gap (Fri idx0 -> Mon idx3, filtered-adjacent) trips SKIPPED_SESSION_BOUNDARY",
                  rc = 2 AndAlso st.LastRunOutcome = "SKIPPED_SESSION_BOUNDARY" AndAlso
                  lastRound IsNot Nothing AndAlso
                  lastRound.Outcome = "SKIPPED_SESSION_BOUNDARY" AndAlso
                  lastRound.WindowStartRow = 0 AndAlso lastRound.WindowEndRow = 3,
                  String.Format("rc={0} outcome={1} start={2} end={3}",
                                rc, st.LastRunOutcome,
                                If(lastRound Is Nothing, -1, lastRound.WindowStartRow),
                                If(lastRound Is Nothing, -1, lastRound.WindowEndRow)))
        Finally
            Try : System.IO.Directory.Delete(dir, True) : Catch : End Try
        End Try
    End Sub

    ' -- A59d: ConditionsExtractor sees no weekend row (D-3) --------------------
    ' A round span (WindowStartRow=0..WindowEndRow=3) that contains weekend lines.
    ' Without the fix, all 4 rows are counted (2 UP + 2 RB -> "UP:50|...|RB:50|...");
    ' with it, only the 2 weekday rows are counted (both UP -> "UP:100|...").
    Private Sub A59d_ConditionsExtractionExcludesWeekendRows()
        Dim path As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ordercheck_a59d_" & Guid.NewGuid().ToString("N") & ".csv")
        Try
            System.IO.File.WriteAllText(path,
                "Timestamp,Price,Regime,Verdict" & vbCrLf &
                "2026-01-02 14:00:00,100,TRENDING_UP,LONG" & vbCrLf &   ' Fri — kept
                "2026-01-03 14:00:00,101,RANGE_BOUND,LONG" & vbCrLf &   ' Sat — must be excluded
                "2026-01-04 14:00:00,102,RANGE_BOUND,LONG" & vbCrLf &   ' Sun — must be excluded
                "2026-01-05 14:00:00,103,TRENDING_UP,LONG" & vbCrLf)    ' Mon — kept

            Dim round As New RoundSummary With {.WindowStartRow = 0, .WindowEndRow = 3}
            Dim cv = ConditionsExtractor.Extract(path, New List(Of RoundSummary) From {round}, 5.0, 15.0)

            Check("A59d ConditionsExtractor excludes weekend rows from the regime mix (UP:100, not UP:50/RB:50)",
                  cv.RegimeMix = "UP:100|DN:0|RB:0|TR:0",
                  String.Format("RegimeMix='{0}'", cv.RegimeMix))
        Finally
            Try : System.IO.File.Delete(path) : Catch : End Try
        End Try
    End Sub

    ' -- A59e: the MinValue trap — an unparseable timestamp is EXCLUDED, --------
    ' not admitted as Monday (DateTime.MinValue.DayOfWeek = Monday).
    Private Sub A59e_UnparseableTimestampExcludedNotAdmittedAsMonday()
        Dim path As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ordercheck_a59e_" & Guid.NewGuid().ToString("N") & ".csv")
        Try
            System.IO.File.WriteAllText(path,
                "Timestamp,Price,Verdict" & vbCrLf &
                "2026-01-05 14:00:00,100000,LONG" & vbCrLf &   ' well-formed Monday
                "2026-01-05T14:00:00,100000,LONG" & vbCrLf)    ' ISO 'T' separator — TryParseExact rejects
            Dim rows = ForwardWindowJoiner.Load(path)

            Check("A59e malformed timestamp parses to MinValue and is excluded, not admitted as Monday",
                  rows.Count = 2 AndAlso
                  rows(1).Timestamp = DateTime.MinValue AndAlso
                  AutoTweakerCore.MatchesWeekday(rows(0)) AndAlso
                  Not AutoTweakerCore.MatchesWeekday(rows(1)),
                  String.Format("rows={0} row1Ts={1:o} row0Match={2} row1Match={3}",
                                rows.Count,
                                If(rows.Count > 1, rows(1).Timestamp, DateTime.MinValue),
                                If(rows.Count > 0, AutoTweakerCore.MatchesWeekday(rows(0)), False),
                                If(rows.Count > 1, AutoTweakerCore.MatchesWeekday(rows(1)), False)))
        Finally
            Try : System.IO.File.Delete(path) : Catch : End Try
        End Try
    End Sub

    ''' <summary>Parse a single-key TWEAK diff into DiffItems via the real ParseDiff.</summary>
    Private Function OneDiff(path As String, oldV As String, newV As String) As List(Of DiffItem)
        Dim resp As String =
            "{""action"":""tweak"",""reasoning"":""t"",""diff"":[{""path"":""" & path &
            """,""old_value"":" & oldV & ",""new_value"":" & newV & ",""justification"":""j""}]}"
        Return SettingsDiffApplier.ParseDiff(resp).Items
    End Function

    ' -- A15f: Validate rejects off-surface keys, passes a normal tunable ------
    Private Sub A15f_ValidateRejectsOffSurfaceKeys()
        ' settings tree carrying the control key (indicators.OBV.trend_gate=10).
        Dim s As String = "{""version"":1,""indicators"":{""OBV"":{""trend_gate"":10}}}"

        Dim rRes = SettingsDiffApplier.Validate(OneDiff("resolution_profiles.3.roc_magnitude_threshold", "0.21", "0.25"), s, 3)
        Dim rKel = SettingsDiffApplier.Validate(OneDiff("kelly.max_leverage", "5.0", "6.0"), s, 3)
        ' [v62] The exemplar re-pinned from the retired exact-match key
        ' scoring.min_tradeable_move_pct to its successor prefix scoring.trade_costs.
        ' (HARD CONSTRAINT 26). A40d covers the whole block + the retired key.
        Dim rMin = SettingsDiffApplier.Validate(OneDiff("scoring.trade_costs.min_net_move_pct", "0.0005", "0.0010"), s, 3)
        Dim rCtl = SettingsDiffApplier.Validate(OneDiff("indicators.OBV.trend_gate", "10", "12"), s, 3)

        Check("A15f Validate rejects resolution_profiles.* / kelly.* / scoring.trade_costs.*, passes OBV.trend_gate",
              Not rRes.IsValid AndAlso Not rKel.IsValid AndAlso Not rMin.IsValid AndAlso rCtl.IsValid,
              String.Format("res={0} kel={1} min={2} ctl={3}",
                            rRes.IsValid, rKel.IsValid, rMin.IsValid, rCtl.IsValid))
    End Sub

    ' -- A15g: HC11 prefix guard does not over-match session_volume; enabled is HC22-fenced --
    ' [v52 S5 rider] session_volume.enabled is now an exact-match HARD CONSTRAINT 22 reject
    ' (this fixture originally documented it PASSING — the unfenced switch the S5 rider closes).
    ' The array-path multiplier is still rejected only as UNRESOLVED — NOT by the HC11 prefix
    ' guard — which remains the over-match proof this fixture was written for.
    Private Sub A15g_ValidatePassesNormalSessionVolumeKey()
        Dim s As String = "{""version"":1,""session_volume"":{""enabled"":true,""sessions"":[{""name"":""NY"",""high_multiplier"":1.15}]}}"

        ' session_volume.enabled → exact-match HARD CONSTRAINT 22 reject (S5 rider).
        Dim rEn = SettingsDiffApplier.Validate(OneDiff("session_volume.enabled", "true", "false"), s, 3)
        ' An array-path session_volume multiplier is rejected only as UNRESOLVED
        ' (NavigatePath can't traverse the sessions array) — NOT by the
        ' HARD CONSTRAINT 11 guard. Confirms no over-match onto session_volume.
        Dim rArr = SettingsDiffApplier.Validate(OneDiff("session_volume.sessions.0.high_multiplier", "1.15", "1.2"), s, 3)

        Check("A15g session_volume.enabled HC22-fenced; array multiplier UNRESOLVED (not HC11-guarded)",
              Not rEn.IsValid AndAlso rEn.ErrorReason.Contains("HARD CONSTRAINT 22") AndAlso
              (Not rArr.IsValid) AndAlso Not rArr.ErrorReason.Contains("HARD CONSTRAINT 11"),
              String.Format("enValid={0} enReason='{1}' arrValid={2} arrReason='{3}'",
                            rEn.IsValid, rEn.ErrorReason, rArr.IsValid, rArr.ErrorReason))
    End Sub

    ' -- A15h: Validate rejects network.* (HARD CONSTRAINT 12), passes a scoring key --
    ' WS-P2 §6. The whole network block (REST timeout/retry + the WS/transport/shadow_parity
    ' keys) is transport plumbing with no failure-rate linkage; SettingsDiffApplier must
    ' reject any 'network.' diff via the prefix guard while a legitimate tunable still passes.
    Private Sub A15h_ValidateRejectsNetworkKeys()
        Dim s As String = "{""version"":1,""network"":{""transport"":""rest"",""ws_url"":""wss://x""}," &
                          """indicators"":{""OBV"":{""trend_gate"":10}}}"

        Dim rTr  = SettingsDiffApplier.Validate(OneDiff("network.transport", """rest""", """ws"""), s, 3)
        Dim rUrl = SettingsDiffApplier.Validate(OneDiff("network.ws_url", """wss://x""", """wss://y"""), s, 3)
        Dim rCtl = SettingsDiffApplier.Validate(OneDiff("indicators.OBV.trend_gate", "10", "12"), s, 3)

        Check("A15h Validate rejects network.transport / network.ws_url (HARD CONSTRAINT 12), passes OBV.trend_gate",
              (Not rTr.IsValid) AndAlso (Not rUrl.IsValid) AndAlso rCtl.IsValid AndAlso
              rTr.ErrorReason.Contains("HARD CONSTRAINT") AndAlso rUrl.ErrorReason.Contains("HARD CONSTRAINT"),
              String.Format("tr={0} url={1} ctl={2} trReason='{3}'",
                            rTr.IsValid, rUrl.IsValid, rCtl.IsValid, rTr.ErrorReason))
    End Sub

    ' =======================================================================
    ' A16 — WebSocket migration P3 cutover
    ' docs/websocket-migration-p3-cutover-spec.md §6.
    '   (a) §3 trades connection-health gate: served when connected-but-quiet,
    '       withheld when the connection is down (+ the legacy no-delegate age-gate).
    '   (b) §4 15m-refresh policy: WS reads every run; REST retains the TTL.
    ' The stub-testable WsMarketDataSource + MarketState run as the real shipped
    ' code (OrderCheck.vbproj links them); the feed + live path are validated by
    ' the live shadow-parity gate, not here.
    ' =======================================================================

    ''' <summary>A WS source over a freshly-seeded MarketState. healthCheck stubs the feed's
    ''' connection-health; staleAfterSec is set tight so the age-gate WOULD trip if consulted,
    ''' isolating the connection-health gate from the age-gate.</summary>
    Private Function SeededWsSource(healthCheck As Func(Of Boolean),
                                    tradesAgeSeconds As Double) As WsMarketDataSource
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord) From {
            Trade("buy", 1000, 1), Trade("sell", 1000, 2), Trade("buy", 1000, 3)}
        ' Stamp TradesLastUpdate `tradesAgeSeconds` in the past.
        state.SeedTrades(trades, DateTime.UtcNow.AddSeconds(-tradesAgeSeconds))
        Return New WsMarketDataSource(state, healthCheck, staleAfterSec:=10)
    End Function

    ' -- A16a: trades served when connected-but-quiet --------------------------
    ' Old buffer (300s, past the 10s age-gate) but a healthy connection → the
    ' connection-health gate (P3 §3) returns the complete-but-quiet buffer rather
    ' than mistaking "no new trades" for "stream broken". This is the case that
    ' reset the live parity streak before the §3 fix.
    Private Sub A16a_TradesServedWhenConnectedButQuiet()
        Dim src = SeededWsSource(Function() True, tradesAgeSeconds:=300)
        Dim got = src.GetRecentTradesAsync(500).GetAwaiter().GetResult()
        Check("A16a trades served when connected-but-quiet (300s-old buffer, healthCheck=True → returned)",
              got IsNot Nothing AndAlso got.Count = 3,
              String.Format("expected 3 trades back despite the stale age, got {0}",
                            If(got Is Nothing, "Nothing", got.Count.ToString())))
    End Sub

    ' -- A16b: trades withheld when the connection is down ----------------------
    ' Even a FRESH buffer (0s old, well inside the age-gate) is withheld when the
    ' connection is unhealthy → the whole-run REST fallback (IsDegraded upstream)
    ' takes over. Proves connection-health, not age, governs the WS trades stream.
    Private Sub A16b_TradesWithheldWhenConnectionDown()
        Dim src = SeededWsSource(Function() False, tradesAgeSeconds:=0)
        Dim got = src.GetRecentTradesAsync(500).GetAwaiter().GetResult()
        Check("A16b trades withheld when connection down (fresh buffer, healthCheck=False → Nothing)",
              got Is Nothing,
              String.Format("expected Nothing, got {0}",
                            If(got Is Nothing, "Nothing", got.Count.ToString())))
    End Sub

    ' -- A16c: legacy age-gate when no health delegate is wired -----------------
    ' Without a healthCheck (Nothing) the trades getter falls back to the original
    ' last-trade-age gate, byte-identical to the pre-§3 path: old → Nothing, fresh
    ' → served. Guards the legacy/test path the §3 fix deliberately preserved.
    Private Sub A16c_LegacyAgeGateWithoutHealthCheck()
        Dim srcStale = SeededWsSource(healthCheck:=Nothing, tradesAgeSeconds:=300)
        Dim gotStale = srcStale.GetRecentTradesAsync(500).GetAwaiter().GetResult()
        Dim srcFresh = SeededWsSource(healthCheck:=Nothing, tradesAgeSeconds:=0)
        Dim gotFresh = srcFresh.GetRecentTradesAsync(500).GetAwaiter().GetResult()
        Check("A16c legacy age-gate without healthCheck (300s→Nothing, fresh→served)",
              gotStale Is Nothing AndAlso gotFresh IsNot Nothing AndAlso gotFresh.Count = 3,
              String.Format("expected stale=Nothing fresh=3, got stale={0} fresh={1}",
                            If(gotStale Is Nothing, "Nothing", "served"),
                            If(gotFresh Is Nothing, "Nothing", gotFresh.Count.ToString())))
    End Sub

    ' -- A16d: WS-path 15m reads every run -------------------------------------
    ' transport="ws" ⇒ ShouldRefresh is always True, even with a 0s-old present
    ' cache — the §4 collapse (15m is in-memory, the TTL buys nothing). Case-
    ' insensitive on the transport string.
    Private Sub A16d_Mtf15mWsReadsEveryRun()
        Check("A16d WS-path 15m reads every run (fresh present cache still refreshes; case-insensitive)",
              MtfRefreshPolicy.ShouldRefresh("ws", haveCached:=True, secondsSinceLastFetch:=0, ttlSeconds:=60) = True AndAlso
              MtfRefreshPolicy.ShouldRefresh("WS", haveCached:=True, secondsSinceLastFetch:=1, ttlSeconds:=60) = True,
              "expected ShouldRefresh=True on the WS path even with a 0s-old cache")
    End Sub

    ' -- A16e: REST-path 15m retains the TTL (byte-identical-at-rest proof) -----
    ' transport="rest" ⇒ the original TTL gate exactly: a fresh cache (<TTL) skips
    ' the fetch; an at/over-TTL cache refreshes; an absent cache refreshes. This is
    ' the predicate's REST arm proving the §4 change is WS-path-only.
    Private Sub A16e_Mtf15mRestRetainsTtl()
        Check("A16e REST-path 15m retains TTL (<TTL skips; >=TTL refreshes; no-cache refreshes)",
              MtfRefreshPolicy.ShouldRefresh("rest", haveCached:=True, secondsSinceLastFetch:=30, ttlSeconds:=60) = False AndAlso
              MtfRefreshPolicy.ShouldRefresh("rest", haveCached:=True, secondsSinceLastFetch:=60, ttlSeconds:=60) = True AndAlso
              MtfRefreshPolicy.ShouldRefresh("rest", haveCached:=True, secondsSinceLastFetch:=90, ttlSeconds:=60) = True AndAlso
              MtfRefreshPolicy.ShouldRefresh("rest", haveCached:=False, secondsSinceLastFetch:=0, ttlSeconds:=60) = True,
              "expected REST TTL semantics: <TTL skips, >=TTL refreshes, no-cache refreshes")
    End Sub

    ' =======================================================================
    ' A17 — realtime exit guard (P4 #1)
    ' docs/realtime-exit-guard-proposal.md §8.
    ' Covers the SHARED ScoringEngine.ComputeFastExitPrimitives, the host-agnostic
    ' ExitGuardEvaluator (end-to-end over a MarketState), and the CalcHoldStatus
    ' byte-identical refactor (asserted through the public Calculate() with InLong).
    ' =======================================================================

    ''' <summary>A bare IndicatorResults carrying only the streaming-driven fields the
    ''' shared primitive reads, for the given adverse profile.</summary>
    Private Function ExitGuardR(micro As String, ofi As String, tfi As String,
                                cvdSlope As String, cvdValue As Double,
                                price As Double, swingLow As Double, swingHigh As Double) As IndicatorResults
        Return New IndicatorResults With {
            .MicroCVDSignal = micro, .OFISignal = ofi, .TFISignal = tfi,
            .CVDSlope = cvdSlope, .CVDValue = cvdValue,
            .CurrentPrice = price, .LastSwingLow5m = swingLow, .LastSwingHigh5m = swingHigh}
    End Function

    ' -- A17a: 2 adverse on a long → AdverseCount 2, no structural break --------
    ' TFI SELL + CVD FALLING(<0) are adverse for a long; MicroCVD FLAT / OFI BALANCED
    ' are not. Count = 2 → the fast-exit branch. AdverseSignals carries the terse
    ' CalcHoldStatus fragments in [micro, ofi, tfi, cvd] order.
    Private Sub A17a_PrimitiveTwoAdverseLong()
        Dim r = ExitGuardR("FLAT", "BALANCED", "SELL PRESSURE", "FALLING", -50000, 100000, 0, 0)
        Dim p = ScoringEngine.ComputeFastExitPrimitives(r, PositionState.InLong)
        Check("A17a primitive 2-adverse long (TFI+CVD → count 2, no break)",
              p.AdverseCount = 2 AndAlso Not p.StructuralBreak AndAlso
              p.TfiAdverse AndAlso p.CvdAdverse AndAlso Not p.MicroAdverse AndAlso Not p.OfiAdverse AndAlso
              p.AdverseSignals.Length = 2 AndAlso String.Join("+", p.AdverseSignals) = "TFI:SELL+CVD:FALLING",
              String.Format("count={0} break={1} signals={2}", p.AdverseCount, p.StructuralBreak,
                            String.Join("+", p.AdverseSignals)))
    End Sub

    ' -- A17b: structural break on a long (price <= swing low) -----------------
    ' Single adverse (TFI) but a confirmed break of the carried 5m swing low → the
    ' structural-break flag fires independently of the adverse count.
    Private Sub A17b_PrimitiveStructuralBreakLong()
        Dim r = ExitGuardR("FLAT", "BALANCED", "SELL PRESSURE", "FLAT", 0, 64200, 64210, 0)
        Dim p = ScoringEngine.ComputeFastExitPrimitives(r, PositionState.InLong)
        Check("A17b primitive structural break long (price 64200 <= swing low 64210)",
              p.StructuralBreak AndAlso Math.Abs(p.BreakLevel - 64210) < 0.001 AndAlso p.AdverseCount = 1,
              String.Format("break={0} level={1:F1} count={2}", p.StructuralBreak, p.BreakLevel, p.AdverseCount))
    End Sub

    ' -- A17c: a single adverse signal on a long → count 1, no break -----------
    Private Sub A17c_PrimitiveSingleAdverseLong()
        Dim r = ExitGuardR("BEAR_ACCEL", "BALANCED", "NEUTRAL", "FLAT", 0, 100000, 0, 0)
        Dim p = ScoringEngine.ComputeFastExitPrimitives(r, PositionState.InLong)
        Check("A17c primitive single-adverse long (MicroCVD only → count 1)",
              p.AdverseCount = 1 AndAlso p.MicroAdverse AndAlso Not p.StructuralBreak,
              String.Format("count={0} micro={1} break={2}", p.AdverseCount, p.MicroAdverse, p.StructuralBreak))
    End Sub

    ' -- A17d: nothing adverse on a long → count 0, no break -------------------
    Private Sub A17d_PrimitiveClearLong()
        Dim r = ExitGuardR("BULL_ACCEL", "BUY DOMINANT", "BUY PRESSURE", "RISING", 50000, 100000, 90000, 0)
        Dim p = ScoringEngine.ComputeFastExitPrimitives(r, PositionState.InLong)
        Check("A17d primitive clear long (bullish flow → count 0, no break)",
              p.AdverseCount = 0 AndAlso Not p.StructuralBreak AndAlso p.AdverseSignals.Length = 0,
              String.Format("count={0} break={1} signals={2}", p.AdverseCount, p.StructuralBreak, p.AdverseSignals.Length))
    End Sub

    ' -- A17e: mirror — 2 adverse on a short ----------------------------------
    ' For a short, BUY-side flow is adverse: OFI BUY + TFI BUY → count 2.
    Private Sub A17e_PrimitiveTwoAdverseShort()
        Dim r = ExitGuardR("FLAT", "BUY DOMINANT", "BUY PRESSURE", "FLAT", 0, 100000, 0, 0)
        Dim p = ScoringEngine.ComputeFastExitPrimitives(r, PositionState.InShort)
        Check("A17e primitive 2-adverse short (OFI+TFI buy → count 2)",
              p.AdverseCount = 2 AndAlso p.OfiAdverse AndAlso p.TfiAdverse AndAlso
              String.Join("+", p.AdverseSignals) = "OFI:BUY+TFI:BUY",
              String.Format("count={0} signals={1}", p.AdverseCount, String.Join("+", p.AdverseSignals)))
    End Sub

    ' -- A17f: ExitGuardEvaluator end-to-end over a live MarketState -----------
    ' A buffer of heavy recent sells drives TFI SELL PRESSURE + CVD FALLING(<0) → 2
    ' adverse for a long → Exit. An empty MarketState → Clear (never a false EXIT, §7).
    Private Sub A17f_EvaluatorEndToEnd()
        Dim heavy As New MarketState()
        Dim trades As New List(Of TradeRecord)
        For i As Integer = 1 To 120
            trades.Add(Trade("sell", 20000, i))
        Next
        heavy.SeedTrades(trades, DateTime.UtcNow)
        Dim cfg As New EngineSettings()
        Dim res = ExitGuardEvaluator.Evaluate(heavy, PositionState.InLong, 0, 0, cfg)

        Dim empty As New MarketState()
        Dim resEmpty = ExitGuardEvaluator.Evaluate(empty, PositionState.InLong, 0, 0, cfg)

        Check("A17f evaluator end-to-end (heavy sells → Exit; empty buffer → Clear)",
              res.Kind = ExitGuardKind.[Exit] AndAlso res.AdverseCount >= 2 AndAlso
              resEmpty.Kind = ExitGuardKind.Clear,
              String.Format("heavy={0}/cnt{1} empty={2}", res.Kind, res.AdverseCount, resEmpty.Kind))
    End Sub

    ' -- A17g: CalcHoldStatus byte-identical after the primitive extraction -----
    ' Asserted through the public Calculate() (Step 6 sets res.HoldStatus) with a
    ' declared LONG position. The A8 indicators carry all four adverse signals
    ' (MicroCVD BEAR_ACCEL / OFI SELL / TFI SELL / CVD FALLING<0), so Layer 1 fires
    ' and the exact terse string must match the pre-refactor output.
    Private Sub A17g_CalcHoldStatusByteIdentical()
        Dim v = ScoringEngine.Calculate(BuildA8Indicators(), PositionState.InLong,
                                        BuildA8Norms(), BuildA8Cfg(fundingBoost:=0))
        Const expected As String = "EXIT -- microstructure deterioration (BEAR_ACCEL+OFI:SELL+TFI:SELL+CVD:FALLING)"
        Check("A17g CalcHoldStatus byte-identical (Layer 1 exact string via Calculate/InLong)",
              v.HoldStatus = expected,
              "expected '" & expected & "', got '" & v.HoldStatus & "'")
    End Sub

    ' -- A17h: D3 ruling — a SINGLE adverse signal maps to Clear, not a Warn tier --------------
    ' 450 buys then 50 sells (no book → OFI BALANCED): the last-30 TFI window is all sells →
    ' SELL PRESSURE (the lone adverse), while the 500-trade CVD stays net-positive (value > 0 →
    ' not adverse) and the last-50 MicroCVD window is all uniform sells → FLAT (not adverse). So
    ' AdverseCount == 1, and the evaluator must return Clear (the Warn tier is retired).
    Private Sub A17h_EvaluatorSingleAdverseIsClear()
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord)
        Dim ts As Long = 1
        For i As Integer = 1 To 450
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 50
            trades.Add(Trade("sell", 1000, ts)) : ts += 1
        Next
        state.SeedTrades(trades, DateTime.UtcNow)
        Dim cfg As New EngineSettings()
        Dim res = ExitGuardEvaluator.Evaluate(state, PositionState.InLong, 0, 0, cfg)

        Check("A17h evaluator single-adverse → Clear (D3: Warn tier dropped)",
              res.Kind = ExitGuardKind.Clear AndAlso res.AdverseCount = 1,
              String.Format("expected Clear/cnt1, got {0}/cnt{1}", res.Kind, res.AdverseCount))
    End Sub

    ' =======================================================================
    ' A18 — On-close analysis mode (P4 #2)
    ' docs/on-close-analysis-mode-proposal.md §8.
    '   BarCloseDetector.DetectBarRoll runs as the REAL shipped code (OrderCheck.vbproj
    '   links Core/BarCloseDetector.vb + MarketState.vb). The WinForms watcher Threading.Timer
    '   + the marshal-to-RunAutoAnalysis wiring are host glue (validated live), as with A17's timer.
    '   Candle.Timestamp is epoch-ms (Long) — the detector compares forming-bar OPEN-times.
    ' =======================================================================

    Private Const OneMinMs As Long = 60_000   ' 1-min in epoch-ms

    Private Function BarCandle(tsMs As Long, close As Double) As Candle
        Return New Candle With {.Timestamp = tsMs, .Open = close, .High = close + 1,
                                .Low = close - 1, .Close = close, .Volume = 10}
    End Function

    ' -- A18a: same forming-bar open-time → no fire ---------------------------
    ' First look adopts the forming open WITHOUT firing (no run on start, mirroring the
    ' interval timer); a second look at the unchanged open-time stays quiet.
    Private Sub A18a_NoRollSameOpen()
        Dim state As New MarketState()
        Dim t0 As Long = 1_700_000_000_000L
        state.ApplyChartTick("1", BarCandle(t0, 100), DateTime.UtcNow)
        Dim first = BarCloseDetector.DetectBarRoll(state, 1, BarCloseDetector.Unseen)
        Dim second = BarCloseDetector.DetectBarRoll(state, 1, first.FormingOpen)
        Check("A18a no roll on unchanged forming open (first adopts no-fire, second no-fire)",
              first.Fired = False AndAlso first.FormingOpen = t0 AndAlso
              second.Fired = False AndAlso second.FormingOpen = t0,
              String.Format("first=({0},{1}) second=({2},{3})",
                            first.Fired, first.FormingOpen, second.Fired, second.FormingOpen))
    End Sub

    ' -- A18b: forming bar advanced one interval → fire exactly once ----------
    Private Sub A18b_RollFiresOnce()
        Dim state As New MarketState()
        Dim t0 As Long = 1_700_000_000_000L
        state.ApplyChartTick("1", BarCandle(t0, 100), DateTime.UtcNow)
        Dim seen = BarCloseDetector.DetectBarRoll(state, 1, BarCloseDetector.Unseen)   ' adopt t0
        state.ApplyChartTick("1", BarCandle(t0 + OneMinMs, 101), DateTime.UtcNow)      ' bar rolls
        Dim rolled = BarCloseDetector.DetectBarRoll(state, 1, seen.FormingOpen)
        Dim afterRoll = BarCloseDetector.DetectBarRoll(state, 1, rolled.FormingOpen)
        Check("A18b roll fires once on +1 interval, then quiesces",
              rolled.Fired = True AndAlso rolled.FormingOpen = t0 + OneMinMs AndAlso afterRoll.Fired = False,
              String.Format("rolled=({0},{1}) afterRoll.Fired={2}", rolled.Fired, rolled.FormingOpen, afterRoll.Fired))
    End Sub

    ' -- A18c: multi-bar reconnect gap → single catch-up fire (no burst) ------
    Private Sub A18c_MultiBarGapSingleFire()
        Dim state As New MarketState()
        Dim t0 As Long = 1_700_000_000_000L
        state.ApplyChartTick("1", BarCandle(t0, 100), DateTime.UtcNow)
        Dim seen = BarCloseDetector.DetectBarRoll(state, 1, BarCloseDetector.Unseen)   ' adopt t0
        ' Feed gap: five bars elapsed; the forming open jumps to t0 + 5 min.
        state.ApplyChartTick("1", BarCandle(t0 + 5 * OneMinMs, 105), DateTime.UtcNow)
        Dim rolled = BarCloseDetector.DetectBarRoll(state, 1, seen.FormingOpen)
        Dim afterRoll = BarCloseDetector.DetectBarRoll(state, 1, rolled.FormingOpen)
        Check("A18c multi-bar gap fires exactly once (catch-up, not burst), adopts newest open",
              rolled.Fired = True AndAlso rolled.FormingOpen = t0 + 5 * OneMinMs AndAlso afterRoll.Fired = False,
              String.Format("rolled=({0},{1}) afterRoll.Fired={2}", rolled.Fired, rolled.FormingOpen, afterRoll.Fired))
    End Sub

    ' -- A18d: resolution switch → first roll on the new resolution fires -----
    ' Each resolution is tracked by its OWN series. On a session boundary the host resets the
    ' last-seen open to Unseen (re-adopt → no immediate fire); the new resolution's next roll
    ' fires normally. Proves the per-resolution independence DetectBarRoll relies on.
    Private Sub A18d_ResolutionSwitchCleanFirstRoll()
        Dim state As New MarketState()
        Dim t0 As Long = 1_700_000_000_000L
        state.ApplyChartTick("1", BarCandle(t0, 100), DateTime.UtcNow)   ' NY 1-min series
        state.ApplyChartTick("3", BarCandle(t0, 100), DateTime.UtcNow)   ' Asia/London 3-min series
        Dim ny = BarCloseDetector.DetectBarRoll(state, 1, BarCloseDetector.Unseen)        ' tracking 1-min
        ' Session flips to 3-min: host resets last-seen → re-adopt the 3-min forming bar (no fire).
        Dim asiaAdopt = BarCloseDetector.DetectBarRoll(state, 3, BarCloseDetector.Unseen)
        state.ApplyChartTick("3", BarCandle(t0 + 3 * OneMinMs, 103), DateTime.UtcNow)     ' first 3-min roll
        Dim asiaRoll = BarCloseDetector.DetectBarRoll(state, 3, asiaAdopt.FormingOpen)
        Check("A18d resolution switch: 3-min re-adopts no-fire, first new-resolution roll fires",
              asiaAdopt.Fired = False AndAlso asiaAdopt.FormingOpen = t0 AndAlso
              asiaRoll.Fired = True AndAlso asiaRoll.FormingOpen = t0 + 3 * OneMinMs,
              String.Format("ny.FormingOpen={0} adopt=({1},{2}) roll=({3},{4})",
                            ny.FormingOpen, asiaAdopt.Fired, asiaAdopt.FormingOpen, asiaRoll.Fired, asiaRoll.FormingOpen))
    End Sub

    ' -- A18e: interval backstop arithmetic (now − lastFire ≥ interval) -------
    ' The watcher fires a backstop run when no roll has been seen for a full interval, so the
    ' engine never goes silent on a WS feed stall. Pure ms-delta check (host uses DateTime deltas).
    Private Sub A18e_BackstopArithmetic()
        Dim intervalMs As Long = 30_000
        Dim lastFire As Long = 1_000_000
        Dim justBefore As Long = lastFire + intervalMs - 1     ' 29.999s later → no backstop
        Dim atCeiling As Long = lastFire + intervalMs          ' exactly interval later → backstop fires
        Check("A18e backstop fires at (now − lastFire ≥ interval), not before",
              (justBefore - lastFire >= intervalMs) = False AndAlso
              (atCeiling - lastFire >= intervalMs) = True,
              String.Format("justBefore Δ={0} atCeiling Δ={1} interval={2}",
                            justBefore - lastFire, atCeiling - lastFire, intervalMs))
    End Sub

    ' =======================================================================
    ' A19 — LIVE microstructure strip (P4 #3)
    ' docs/live-microstructure-strip-proposal.md §8. The evaluator is display/awareness only — it never
    ' calls Calculate, never writes the CSV. These fixtures assert it reuses the engine's pure fns
    ' correctly (TFI/spread/imbalance), brackets the live price between the nearest carried levels, scans
    ' the tape-speed window (recent counted / older excluded / lull → 0), and degrades empty → blanks.
    ' =======================================================================

    ' Build a top-of-book snapshot with descending bid prices and ascending ask prices.
    Private Function MakeBook(bestBid As Double, bestAsk As Double,
                              bidSize As Double, askSize As Double) As OrderBookSnapshot
        Dim book As New OrderBookSnapshot()
        For i As Integer = 0 To 4
            book.Bids.Add((bestBid - i, bidSize))
            book.Asks.Add((bestAsk + i, askSize))
        Next
        Return book
    End Function

    ' -- A19a: TFI / spread / imbalance computed via the reused pure fns -------
    ' 30 sells then 30 buys → the last-30 TFI window is all buys → BUY PRESSURE. Book bestBid 99990 /
    ' bestAsk 100010 (mid 100000) → spread = 20/100000 × 10000 = 2.0 bps. Bids 10× the ask size →
    ' OFI ratio > 1 → imbalance side "bid".
    Private Sub A19a_TfiSpreadImbalance()
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord)
        Dim ts As Long = 1
        For i As Integer = 1 To 30
            trades.Add(Trade("sell", 1000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 30
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
        Next
        state.SeedTrades(trades, DateTime.UtcNow)
        state.UpdateBook(MakeBook(99990, 100010, 10, 1), DateTime.UtcNow)

        Dim cfg As New EngineSettings()
        Dim snap = LiveMicrostructureEvaluator.Evaluate(state, Nothing, cfg)

        Check("A19a TFI/spread/imbalance (BUY PRESSURE, 2.0 bps, bid-heavy)",
              snap.HasTfi AndAlso snap.TfiSignal = "BUY PRESSURE" AndAlso
              snap.HasSpread AndAlso Math.Abs(snap.SpreadBps - 2.0) < 0.01 AndAlso
              snap.HasImbalance AndAlso snap.ImbalanceSide = "bid" AndAlso snap.ImbalanceRatio > 1.0,
              String.Format("tfi={0} spread={1:F3} side={2} ratio={3:F2}",
                            snap.TfiSignal, snap.SpreadBps, snap.ImbalanceSide, snap.ImbalanceRatio))
    End Sub

    ' -- A19b: nearest carried levels bracket the live price -------------------
    ' Tail price 100000 (Trade helper sets every price to 100000). Carried levels: two above
    ' (SH 100050 / HVN↑ 100020) and two below (SL 99970 / HVN↓ 99990). Nearest-wins → Above = HVN↑
    ' (100020, +20), Below = HVN↓ (99990, −10).
    Private Sub A19b_NearestLevelsBracketPrice()
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord)
        For i As Integer = 1 To 10
            trades.Add(Trade("buy", 1000, i))
        Next
        state.SeedTrades(trades, DateTime.UtcNow)

        Dim lastRun As New IndicatorResults() With {
            .LastSwingHigh5m = 100050, .VPFRNearestHvnAbove = 100020,
            .LastSwingLow5m = 99970, .VPFRNearestHvnBelow = 99990}

        Dim cfg As New EngineSettings()
        Dim snap = LiveMicrostructureEvaluator.Evaluate(state, lastRun, cfg)

        Check("A19b nearest levels bracket price (above=HVN↑ +20, below=HVN↓ −10)",
              snap.HasPrice AndAlso
              snap.Above.Has AndAlso snap.Above.Label = "HVN↑" AndAlso snap.Above.Price = 100020 AndAlso snap.Above.Delta = 20 AndAlso
              snap.Below.Has AndAlso snap.Below.Label = "HVN↓" AndAlso snap.Below.Price = 99990 AndAlso snap.Below.Delta = -10,
              String.Format("above=({0},{1},{2}) below=({3},{4},{5})",
                            snap.Above.Label, snap.Above.Price, snap.Above.Delta,
                            snap.Below.Label, snap.Below.Price, snap.Below.Delta))
    End Sub

    ' -- A19c: tape-speed window — recent counted, older excluded --------------
    ' Fixed now = 100000 ms, window 10 s → cutoff 90000. Five old trades (ts ≤ 84000) excluded; five
    ' recent trades (ts > 90000, 20000 USD each) counted → 5/10 = 0.5 tr/s, 100000/10 = 10000 USD/s.
    Private Sub A19c_TapeSpeedWindow()
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord)
        For i As Integer = 0 To 4
            trades.Add(Trade("buy", 999999, 80000 + i * 1000))   ' old — outside the 10s window
        Next
        For Each ts As Long In New Long() {91000, 93000, 95000, 97000, 99000}
            trades.Add(Trade("buy", 20000, ts))                  ' recent — inside the window
        Next
        state.SeedTrades(trades, DateTime.UtcNow)

        Dim cfg As New EngineSettings()
        Dim snap = LiveMicrostructureEvaluator.Evaluate(state, Nothing, cfg, nowUtcMs:=100000)

        Check("A19c tape-speed window (5 recent counted, 5 old excluded → 0.5 tr/s, $10000/s)",
              Math.Abs(snap.TradesPerSec - 0.5) < 0.000001 AndAlso
              Math.Abs(snap.UsdPerSec - 10000.0) < 0.001,
              String.Format("tr/s={0:F3} usd/s={1:F1}", snap.TradesPerSec, snap.UsdPerSec))
    End Sub

    ' -- A19d: tape-speed lull → 0 --------------------------------------------
    ' All trades older than the window (ts ≤ 50000, now 100000) → empty window → 0 tr/s, 0 USD/s.
    ' Price still resolves (tail trade) — only the speed reads ~0.
    Private Sub A19d_TapeSpeedLull()
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord)
        For i As Integer = 0 To 9
            trades.Add(Trade("buy", 50000, 40000 + i * 1000))
        Next
        state.SeedTrades(trades, DateTime.UtcNow)

        Dim cfg As New EngineSettings()
        Dim snap = LiveMicrostructureEvaluator.Evaluate(state, Nothing, cfg, nowUtcMs:=100000)

        Check("A19d tape-speed lull (all trades older than window → 0)",
              snap.HasPrice AndAlso snap.TradesPerSec = 0 AndAlso snap.UsdPerSec = 0,
              String.Format("hasPrice={0} tr/s={1:F3} usd/s={2:F1}", snap.HasPrice, snap.TradesPerSec, snap.UsdPerSec))
    End Sub

    ' -- A19e: empty buffer → safe blanks, no throw ---------------------------
    ' Empty MarketState + no carried levels: every field blank (Has* = False), tape speed 0, no throw.
    Private Sub A19e_EmptyBufferBlanks()
        Dim empty As New MarketState()
        Dim cfg As New EngineSettings()
        Dim snap = LiveMicrostructureEvaluator.Evaluate(empty, Nothing, cfg)

        Check("A19e empty buffer → blanks (no price/levels/TFI/spread/imbalance), no throw",
              Not snap.HasPrice AndAlso Not snap.Above.Has AndAlso Not snap.Below.Has AndAlso
              Not snap.HasTfi AndAlso Not snap.HasSpread AndAlso Not snap.HasImbalance AndAlso
              snap.TradesPerSec = 0,
              String.Format("price={0} tfi={1} spread={2} imb={3}",
                            snap.HasPrice, snap.HasTfi, snap.HasSpread, snap.HasImbalance))
    End Sub

    ' ====================================================================
    ' P4 #4 — time-averaged OFI (docs/time-averaged-ofi-proposal.md)
    ' ====================================================================

    ' -- A20a: CalcOFI refactor is byte-identical + equals the shared helpers ---
    ' Book bestBid 99990 / bestAsk 100010, bid size 10 vs ask size 1, depth 5 (weights
    ' {5,4,3,2,1} sum 15) → bidVol 150, askVol 15, ratio 10.0 → BUY DOMINANT (>2.0). CalcOFI
    ' must produce exactly those values (byte-identical to the pre-extraction math) AND match
    ' ComputeOfiImbalance + ClassifyOfiRatio (the helpers the accumulator also folds through).
    Private Sub A20a_CalcOfiRefactorEquivalence()
        Dim book = MakeBook(99990, 100010, 10, 1)

        Dim cRatio, cBid, cAsk As Double, cSig As String = Nothing
        IndicatorEngine.CalcOFI(book, cRatio, cSig, cBid, cAsk,
                                buyDominantRatio:=2.0, sellDominantRatio:=0.5, bookDepth:=5)

        Dim hBid, hAsk, hRatio As Double
        Dim ok = IndicatorEngine.ComputeOfiImbalance(book, 5, hBid, hAsk, hRatio)
        Dim hSig = IndicatorEngine.ClassifyOfiRatio(hRatio, 2.0, 0.5)

        Check("A20a CalcOFI byte-identical (150/15/10.0/BUY DOMINANT) + equals shared helpers",
              ok AndAlso
              Math.Abs(cRatio - 10.0) < 1.0E-9 AndAlso Math.Abs(cBid - 150.0) < 1.0E-9 AndAlso
              Math.Abs(cAsk - 15.0) < 1.0E-9 AndAlso cSig = "BUY DOMINANT" AndAlso
              cRatio = hRatio AndAlso cBid = hBid AndAlso cAsk = hAsk AndAlso cSig = hSig,
              String.Format("ratio={0:F3} bid={1:F1} ask={2:F1} sig={3} | h:{4:F3}/{5:F1}/{6:F1}/{7}",
                            cRatio, cBid, cAsk, cSig, hRatio, hBid, hAsk, hSig))
    End Sub

    ' -- A20b: CalcOFI edge cases unchanged (Nothing book / zero-total book) ----
    ' Nothing → ratio 1.0, BALANCED, bid=ask=0; ComputeOfiImbalance returns False.
    ' Zero-size book → total 0 → same defaults, helper returns False (no fold).
    Private Sub A20b_CalcOfiEdgeCasesUnchanged()
        Dim nRatio, nBid, nAsk As Double, nSig As String = Nothing
        IndicatorEngine.CalcOFI(Nothing, nRatio, nSig, nBid, nAsk,
                                buyDominantRatio:=2.0, sellDominantRatio:=0.5, bookDepth:=5)
        Dim xBid, xAsk, xRatio As Double
        Dim nOk = IndicatorEngine.ComputeOfiImbalance(Nothing, 5, xBid, xAsk, xRatio)

        Dim zeroBook = MakeBook(99990, 100010, 0, 0)
        Dim zRatio, zBid, zAsk As Double, zSig As String = Nothing
        IndicatorEngine.CalcOFI(zeroBook, zRatio, zSig, zBid, zAsk,
                                buyDominantRatio:=2.0, sellDominantRatio:=0.5, bookDepth:=5)
        Dim zOkBid, zOkAsk, zOkRatio As Double
        Dim zOk = IndicatorEngine.ComputeOfiImbalance(zeroBook, 5, zOkBid, zOkAsk, zOkRatio)

        Check("A20b CalcOFI edge cases (Nothing + zero-total → 1.0/BALANCED/0/0, helper False)",
              nRatio = 1.0 AndAlso nSig = "BALANCED" AndAlso nBid = 0 AndAlso nAsk = 0 AndAlso Not nOk AndAlso
              zRatio = 1.0 AndAlso zSig = "BALANCED" AndAlso zBid = 0 AndAlso zAsk = 0 AndAlso Not zOk,
              String.Format("nothing={0}/{1} helperOk={2} | zero={3}/{4} helperOk={5}",
                            nRatio, nSig, nOk, zRatio, zSig, zOk))
    End Sub

    ' -- A20c: accumulator steady state — constant ratio averages to itself -----
    ' 13 folds of ratio 2.0 (bid 2 / ask 1) at 1s steps, tau 10 → EMA stays 2.0 (alpha·0 each
    ' fold) and warmup arms (coverage 12s ≥ 10, 13 folds ≥ the min).
    Private Sub A20c_AccumulatorSteadyState()
        Dim acc As New OfiAccumulator()
        For i As Integer = 0 To 12
            acc.Fold(2.0, 1.0, 2.0, CLng(i) * 1000L, 10.0)
        Next
        Dim snap = acc.Snapshot(10.0)
        Check("A20c accumulator steady state (constant 2.0 → avg 2.0, warmup armed)",
              Math.Abs(snap.Ratio - 2.0) < 1.0E-9 AndAlso Math.Abs(snap.BidVol - 2.0) < 1.0E-9 AndAlso
              Math.Abs(snap.AskVol - 1.0) < 1.0E-9 AndAlso snap.HasWarmup AndAlso snap.UpdateCount = 13,
              String.Format("ratio={0:F6} bid={1:F3} ask={2:F3} warm={3} n={4}",
                            snap.Ratio, snap.BidVol, snap.AskVol, snap.HasWarmup, snap.UpdateCount))
    End Sub

    ' -- A20d: accumulator time-aware GEOMETRIC EMA — one dt=tau step ----------
    ' Seed ratio 1.0 at t=0 (lnSeed=0), then ratio 2.0 at t=10s with tau=10 → dt=tau →
    ' alpha = 1-e^-1 = 0.63212 → emaLn = 0.63212·ln(2) = 0.43817 → Ratio = exp(0.43817) =
    ' 1.5500 (geometric mean). Proves both the alpha = 1-exp(-dt/tau) time-aware formula
    ' AND the geometric (log-ratio) construction (arithmetic would give 1.6321 — see
    ' docs/ofi-geometric-construction-spec.md).
    Private Sub A20d_AccumulatorTimeAwareStep()
        Dim acc As New OfiAccumulator()
        acc.Fold(1.0, 1.0, 1.0, 0L, 10.0)
        acc.Fold(2.0, 1.0, 2.0, 10000L, 10.0)
        Dim snap = acc.Snapshot(0.0)
        Dim expected As Double = Math.Exp((1.0 - Math.Exp(-1.0)) * Math.Log(2.0))
        Check("A20d accumulator time-aware geometric step (dt=tau → EMA 1.5500)",
              Math.Abs(snap.Ratio - expected) < 0.001,
              String.Format("ratio={0:F6} expected={1:F6}", snap.Ratio, expected))
    End Sub

    ' -- A20i: geometric symmetry — alternating 2.0/0.5 converges to ~1.0 ------
    ' Alternating ratio 2.0/0.5 at equal 1s dt steps settles into a symmetric two-cycle in
    ' log space around 0 (i.e. Ratio oscillates evenly either side of 1.0) — the
    ' multiplicatively-symmetric midpoint; an arithmetic mean of the same series would drift
    ' to ~1.25. tau=8 keeps the steady-state oscillation amplitude inside the assert window
    ' (a smaller tau/dt ratio widens the oscillation, since each fold's alpha weight is
    ' larger relative to the alternation period). Locks in the AM/GM fix that motivated the
    ' switch (NY DIAG test, docs/ofi-geometric-construction-spec.md).
    Private Sub A20i_GeometricSymmetryConvergence()
        Dim acc As New OfiAccumulator()
        Dim tau As Double = 8.0
        For i As Integer = 0 To 399
            Dim r As Double = If(i Mod 2 = 0, 2.0, 0.5)
            acc.Fold(1.0, 1.0, r, CLng(i) * 1000L, tau)
        Next
        Dim snap = acc.Snapshot(0.0)
        Check("A20i geometric symmetry (alternating 2.0/0.5 → Ratio in [0.95, 1.05])",
              snap.Ratio >= 0.95 AndAlso snap.Ratio <= 1.05,
              String.Format("ratio={0:F6}", snap.Ratio))
    End Sub

    ' -- A20e: warmup gate — under-window False, full-window True --------------
    ' 6 folds over 5s (< window 10) → not warmed (snapshot fallback). 11 folds over 10s → warmed.
    Private Sub A20e_WarmupGate()
        Dim under As New OfiAccumulator()
        For i As Integer = 0 To 5
            under.Fold(1.5, 1.0, 1.5, CLng(i) * 1000L, 10.0)
        Next

        Dim over As New OfiAccumulator()
        For i As Integer = 0 To 10
            over.Fold(1.5, 1.0, 1.5, CLng(i) * 1000L, 10.0)
        Next

        Check("A20e warmup gate (5s coverage → not warm; 10s coverage → warm)",
              Not under.Snapshot(10.0).HasWarmup AndAlso over.Snapshot(10.0).HasWarmup,
              String.Format("underCov={0:F1} underWarm={1} overCov={2:F1} overWarm={3}",
                            under.CoverageSeconds, under.Snapshot(10.0).HasWarmup,
                            over.CoverageSeconds, over.Snapshot(10.0).HasWarmup))
    End Sub

    ' -- A20f: Reset re-arms the warmup fallback (reconnect semantics) ----------
    ' A warmed accumulator, after Reset(), reports not-warmed + zeroed state — so a fresh
    ' connection re-collects a full window before the average is used again.
    Private Sub A20f_ResetReArmsWarmup()
        Dim acc As New OfiAccumulator()
        For i As Integer = 0 To 10
            acc.Fold(1.5, 1.0, 1.5, CLng(i) * 1000L, 10.0)
        Next
        Dim warmedBefore As Boolean = acc.Snapshot(10.0).HasWarmup
        acc.Reset()
        Dim afterSnap = acc.Snapshot(10.0)
        Check("A20f Reset re-arms warmup (warm → reset → not warm, n=0, coverage=0)",
              warmedBefore AndAlso Not afterSnap.HasWarmup AndAlso
              afterSnap.UpdateCount = 0 AndAlso acc.CoverageSeconds = 0.0,
              String.Format("before={0} afterWarm={1} n={2} cov={3:F1}",
                            warmedBefore, afterSnap.HasWarmup, afterSnap.UpdateCount, acc.CoverageSeconds))
    End Sub

    ' -- A20g: tweaker rejects the OFI averaging feature flag (HARD CONSTRAINT 16) --
    Private Sub A20g_TweakerRejectsAveragingFlag()
        Dim s As String = "{""version"":1,""indicators"":{""OFI"":{""averaging_enabled"":true,""avg_window_sec"":10}}}"
        Dim r = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.averaging_enabled", "true", "false"), s, 3)
        Check("A20g Validate rejects indicators.OFI.averaging_enabled (HARD CONSTRAINT 16)",
              Not r.IsValid AndAlso r.ErrorReason.Contains("HARD CONSTRAINT 16"),
              String.Format("valid={0} reason='{1}'", r.IsValid, r.ErrorReason))
    End Sub

    ' -- A20h: tweaker ACCEPTS avg_window_sec (it stays on the surface) ---------
    Private Sub A20h_TweakerAcceptsAvgWindow()
        Dim s As String = "{""version"":1,""indicators"":{""OFI"":{""averaging_enabled"":true,""avg_window_sec"":10}}}"
        Dim r = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.avg_window_sec", "10", "12"), s, 3)
        Check("A20h Validate accepts indicators.OFI.avg_window_sec (on the tweaker surface)",
              r.IsValid,
              String.Format("valid={0} reason='{1}'", r.IsValid, r.ErrorReason))
    End Sub

    ' -- A21a: v47 F1 — removed dead key is unresolvable; sibling stays tunable -
    ' Settings tree mirrors the post-v47 regime_gates block (transitional_adx_penalty_low
    ' deleted). A diff against the removed key must reject as an unresolvable path
    ' (the C-6 no-key-creation rule); the live sibling stays proposable.
    Private Sub A21a_TweakerRejectsRemovedDeadKey()
        Dim s As String = "{""version"":47,""regime_gates"":{""transitional_adx_penalty_mid"":22.5,""transitional_adx_penalty_high"":25.0,""transitional_penalty_low"":2,""transitional_penalty_mid"":1}}"
        Dim rDead = SettingsDiffApplier.Validate(OneDiff("regime_gates.transitional_adx_penalty_low", "20.0", "15.0"), s, 3)
        Dim rLive = SettingsDiffApplier.Validate(OneDiff("regime_gates.transitional_penalty_mid", "1", "2"), s, 3)
        Check("A21a Validate rejects removed regime_gates.transitional_adx_penalty_low (unresolvable) + accepts live sibling",
              Not rDead.IsValid AndAlso rDead.ErrorReason.Contains("does not resolve") AndAlso rLive.IsValid,
              String.Format("dead: valid={0} reason='{1}' | sibling: valid={2} reason='{3}'",
                            rDead.IsValid, rDead.ErrorReason, rLive.IsValid, rLive.ErrorReason))
    End Sub

    ' -- A21b: v47 D4 — scoring.hold_ prefix fenced (HC17); sibling scoring.* tunable -
    Private Sub A21b_TweakerRejectsHoldPrefix()
        Dim s As String = "{""version"":47,""scoring"":{""verdict_med_pct"":0.53,""hold_rsi_hold_long"":60}}"
        Dim rHold = SettingsDiffApplier.Validate(OneDiff("scoring.hold_rsi_hold_long", "60", "55"), s, 3)
        Dim rSib  = SettingsDiffApplier.Validate(OneDiff("scoring.verdict_med_pct", "0.53", "0.55"), s, 3)
        Check("A21b Validate rejects scoring.hold_rsi_hold_long (HARD CONSTRAINT 17 fence) + accepts scoring.verdict_med_pct",
              Not rHold.IsValid AndAlso rHold.ErrorReason.Contains("off-tweaker-surface") AndAlso rSib.IsValid,
              String.Format("hold: valid={0} reason='{1}' | sibling: valid={2} reason='{3}'",
                            rHold.IsValid, rHold.ErrorReason, rSib.IsValid, rSib.ErrorReason))
    End Sub

    ' =======================================================================
    ' A22 — Signal Bridge v1 emitter (docs/signal-bridge-v1-proposal.md §4,
    ' schema v1 FROZEN 2026-07-03). Pure Build/Serialize against fixed
    ' fixtures; every assertion runs on the PARSED JSON (JsonDocument) so the
    ' serialization pins (§9 item 6: numbers as numbers, invariant culture,
    ' ISO-8601 Z) are exercised, not just the in-memory object.
    ' =======================================================================

    Private Function BuildBridgeCfg() As EngineSettings
        ' POCO defaults carry the live geometry (B4b): structural_levels enabled,
        ' fallback stop ×1.6 / target ×1.75, target bound 3.5×ATR, stop bound 1.6×ATR.
        ' Version pinned so the pass-through is visible. trigger_mode set EXPLICITLY
        ' (the A22a pin tests pass-THROUGH, not the POCO default — which moved to
        ' "on_close" at v57 stomp-proofing and must be free to drift without
        ' re-pinning this fixture).
        Dim cfg As New EngineSettings()
        cfg.Version = 51
        cfg.AutoRun.TriggerMode = "interval"
        Return cfg
    End Function

    Private Function BuildBridgeVerdict() As VerdictResult
        Return New VerdictResult With {
            .Verdict = "STRONG SHORT", .Confidence = "HIGH", .VerdictContext = "CONFIRMED",
            .LongScore = 4, .ShortScore = 13,
            .EffectiveLongScore = 4, .EffectiveShortScore = 13, .MaxScore = 20,
            .MTFGateBlocked = False, .HoldStatus = "N/A -- no open position",
            .KellyContracts = 2, .KellyRiskUsd = 32.5, .KellyLevCapped = False}
    End Function

    Private Function BuildBridgeIndicators() As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = 59012.5
        r.ATR = 41.3
        r.ExecResolution = 1
        r.SwingTargetLong = 59095.0 : r.SwingStopLong = 58860.0
        r.SwingTargetShort = 58860.0 : r.SwingStopShort = 59095.0
        Return r
    End Function

    Private Function BridgeOkJson(v As VerdictResult, r As IndicatorResults,
                                  Optional armed As Boolean = False) As JsonDocument
        Dim payload = SignalEmitter.BuildOk(v, r, BuildBridgeCfg(),
                                            "fixture-instance", 7, armed,
                                            "OK", False,
                                            New DateTime(2026, 7, 3, 14, 31, 2, DateTimeKind.Utc))
        Return JsonDocument.Parse(SignalEmitter.Serialize(payload))
    End Function

    Private Function JNum(el As JsonElement, name As String) As Double
        Return el.GetProperty(name).GetDouble()
    End Function

    ' -- A22a: full payload serialises field-by-field; three target-placement cases --
    ' [B4b re-pin] The levels come from the structural-first arbitration (r-driven —
    ' the manual v.Adjusted* injection the pre-B4b cases used is ignored on this path):
    '   Case 1 (default fixture): LONG swing target 59095 (dist 82.5 ≤ 3.5×ATR=144.55)
    '     PLACES (capped=true, reason "PLACED @ …"); SHORT swing target 58860
    '     (dist 152.5 > bound) falls back to entry − 1.75×ATR (capped=false). BOTH swing
    '     stops (dist 152.5 / 82.5 > 1.6×ATR=66.08) CLAMP to entry ∓ 1.6×ATR.
    Private Sub A22a_PayloadFieldByField()
        Dim root = BridgeOkJson(BuildBridgeVerdict(), BuildBridgeIndicators()).RootElement

        Check("A22a head (schema_version/signal_id/generated_at_utc/instrument/signal_state/skip_reason)",
              root.GetProperty("schema_version").GetInt32() = 1 AndAlso
              root.GetProperty("signal_id").GetInt64() = 7 AndAlso
              root.GetProperty("generated_at_utc").GetString() = "2026-07-03T14:31:02Z" AndAlso
              root.GetProperty("instrument").GetString() = "BTC-PERPETUAL" AndAlso
              root.GetProperty("signal_state").GetString() = "OK" AndAlso
              root.GetProperty("skip_reason").ValueKind = JsonValueKind.Null,
              "head fields mismatch: " & root.GetRawText())

        Dim eng = root.GetProperty("engine")
        Check("A22a engine block (app/settings_version/instance_id/autotrade_armed)",
              eng.GetProperty("app").GetString() = "DeribitVerdictEngine" AndAlso
              eng.GetProperty("settings_version").GetInt32() = 51 AndAlso
              eng.GetProperty("instance_id").GetString() = "fixture-instance" AndAlso
              eng.GetProperty("autotrade_armed").GetBoolean() = False,
              "engine block mismatch: " & eng.GetRawText())

        Dim sc = root.GetProperty("scores")
        Check("A22a verdict/confidence/direction/context/mtf/scores",
              root.GetProperty("verdict").GetString() = "STRONG SHORT" AndAlso
              root.GetProperty("confidence").GetString() = "HIGH" AndAlso
              root.GetProperty("direction").GetString() = "SHORT" AndAlso
              root.GetProperty("verdict_context").GetString() = "CONFIRMED" AndAlso
              Not root.GetProperty("mtf_blocked").GetBoolean() AndAlso
              sc.GetProperty("long").GetInt32() = 4 AndAlso sc.GetProperty("short").GetInt32() = 13 AndAlso
              sc.GetProperty("eff_long").GetInt32() = 4 AndAlso sc.GetProperty("eff_short").GetInt32() = 13 AndAlso
              sc.GetProperty("max").GetInt32() = 20,
              "verdict/scores mismatch: " & root.GetRawText())

        Check("A22a price/exec_resolution_min/trigger_mode/atr",
              Math.Abs(JNum(root, "price") - 59012.5) < 0.0001 AndAlso
              root.GetProperty("exec_resolution_min").GetInt32() = 1 AndAlso
              root.GetProperty("trigger_mode").GetString() = "interval" AndAlso
              Math.Abs(JNum(root, "atr") - 41.3) < 0.0001,
              "price/atr block mismatch")

        Dim lng = root.GetProperty("levels").GetProperty("long")
        Dim sht = root.GetProperty("levels").GetProperty("short")
        Check("A22a structural-first levels (long swing PLACED + clamp; short fallback + clamp)",
              Math.Abs(JNum(lng, "entry") - 59012.5) < 0.0001 AndAlso
              Math.Abs(JNum(lng, "stop") - (59012.5 - 41.3 * 1.6)) < 0.0001 AndAlso
              Math.Abs(JNum(lng, "target") - 59095.0) < 0.0001 AndAlso
              lng.GetProperty("target_capped").GetBoolean() AndAlso
              lng.GetProperty("cap_reason").GetString() = "PLACED @ 59095.0 (SWING_HIGH_5M)" AndAlso
              Math.Abs(JNum(lng, "raw_target") - (59012.5 + 41.3 * 1.75)) < 0.0001 AndAlso
              Math.Abs(JNum(sht, "stop") - (59012.5 + 41.3 * 1.6)) < 0.0001 AndAlso
              Math.Abs(JNum(sht, "target") - (59012.5 - 41.3 * 1.75)) < 0.0001 AndAlso
              Not sht.GetProperty("target_capped").GetBoolean() AndAlso
              sht.GetProperty("cap_reason").ValueKind = JsonValueKind.Null AndAlso
              Math.Abs(JNum(sht, "raw_target") - JNum(sht, "target")) < 0.0001,
              String.Format("levels mismatch: long={0} short={1}", lng.GetRawText(), sht.GetRawText()))

        Dim st = root.GetProperty("structural")
        Check("A22a structural verbatim (zeros = unset semantics ride the raw values)",
              Math.Abs(JNum(st, "swing_target_long") - 59095.0) < 0.0001 AndAlso
              Math.Abs(JNum(st, "swing_stop_long") - 58860.0) < 0.0001 AndAlso
              Math.Abs(JNum(st, "swing_target_short") - 58860.0) < 0.0001 AndAlso
              Math.Abs(JNum(st, "swing_stop_short") - 59095.0) < 0.0001,
              "structural mismatch: " & st.GetRawText())

        Dim kel = root.GetProperty("kelly")
        Check("A22a hold_status null (no-position sentinel) + kelly + health",
              root.GetProperty("hold_status").ValueKind = JsonValueKind.Null AndAlso
              kel.GetProperty("contracts").GetInt32() = 2 AndAlso
              Math.Abs(JNum(kel, "risk_usd") - 32.5) < 0.0001 AndAlso
              Not kel.GetProperty("lev_capped").GetBoolean() AndAlso
              root.GetProperty("health").GetProperty("ws").GetString() = "OK" AndAlso
              Not root.GetProperty("health").GetProperty("degraded_this_run").GetBoolean() AndAlso
              Not root.GetProperty("health").GetProperty("ledger_mismatch").GetBoolean(),
              "hold/kelly/health mismatch: " & root.GetRawText())

        ' Case 2 — no structure at all (fresh r): both sides fall back to the ATR
        ' geometry — uncapped targets, cap_reason null, stops at the fallback distance.
        Dim rBare As New IndicatorResults()
        rBare.CurrentPrice = 59012.5
        rBare.ATR = 41.3
        rBare.ExecResolution = 1
        Dim lvlBare = BridgeOkJson(BuildBridgeVerdict(), rBare).RootElement.GetProperty("levels")
        Dim lngBare = lvlBare.GetProperty("long")
        Dim shtBare = lvlBare.GetProperty("short")
        Check("A22a fallback levels when no structure (capped=false, cap_reason=null, raw=target)",
              Math.Abs(JNum(lngBare, "stop") - (59012.5 - 41.3 * 1.6)) < 0.0001 AndAlso
              Math.Abs(JNum(lngBare, "target") - (59012.5 + 41.3 * 1.75)) < 0.0001 AndAlso
              Not lngBare.GetProperty("target_capped").GetBoolean() AndAlso
              lngBare.GetProperty("cap_reason").ValueKind = JsonValueKind.Null AndAlso
              Math.Abs(JNum(lngBare, "raw_target") - JNum(lngBare, "target")) < 0.0001 AndAlso
              Math.Abs(JNum(shtBare, "target") - (59012.5 - 41.3 * 1.75)) < 0.0001 AndAlso
              Not shtBare.GetProperty("target_capped").GetBoolean(),
              String.Format("fallback levels mismatch: long={0} short={1}", lngBare.GetRawText(), shtBare.GetRawText()))

        ' Case 3 — sub-tick noise suppression survives the arbitration: a structural
        ' swing target 0.225 from the fallback price (< floor max(0.5, ATR×0.02)=0.826)
        ' PLACES the value but reports target_capped=false / cap_reason null (§3 field
        ' sourcing — the display renders it uncapped too).
        Dim rNoise = BuildBridgeIndicators()
        rNoise.SwingTargetLong = 59085.0          ' fallback = 59012.5 + 72.275 = 59084.775
        Dim lngNoise = BridgeOkJson(BuildBridgeVerdict(), rNoise).RootElement _
                           .GetProperty("levels").GetProperty("long")
        Check("A22a cap-noise-suppressed long (target=placed swing, capped=FALSE, cap_reason null)",
              Math.Abs(JNum(lngNoise, "target") - 59085.0) < 0.0001 AndAlso
              Not lngNoise.GetProperty("target_capped").GetBoolean() AndAlso
              lngNoise.GetProperty("cap_reason").ValueKind = JsonValueKind.Null AndAlso
              Math.Abs(JNum(lngNoise, "raw_target") - (59012.5 + 41.3 * 1.75)) < 0.0001,
              "noise-suppressed long mismatch: " & lngNoise.GetRawText())

        ' Held position: hold_status rides verbatim (informational free string).
        Dim vHold = BuildBridgeVerdict()
        vHold.HoldStatus = "HOLD -- momentum intact"
        Check("A22a hold_status verbatim when a position is declared",
              BridgeOkJson(vHold, BuildBridgeIndicators()).RootElement _
                  .GetProperty("hold_status").GetString() = "HOLD -- momentum intact",
              "hold_status pass-through failed")
    End Sub

    ' -- A22b: SKIPPED payload — reduced shape, no verdict/levels fields -------
    Private Sub A22b_SkippedPayloadShape()
        Dim payload = SignalEmitter.BuildSkipped("1m candles unavailable", BuildBridgeCfg(),
                                                 "fixture-instance", 8, False,
                                                 "REST", False,
                                                 New DateTime(2026, 7, 3, 14, 32, 0, DateTimeKind.Utc))
        Dim root = JsonDocument.Parse(SignalEmitter.Serialize(payload)).RootElement
        Dim unused As JsonElement
        Check("A22b SKIPPED shape (state/reason/engine/health present; verdict/levels/price absent)",
              root.GetProperty("signal_state").GetString() = "SKIPPED" AndAlso
              root.GetProperty("skip_reason").GetString() = "1m candles unavailable" AndAlso
              root.GetProperty("signal_id").GetInt64() = 8 AndAlso
              root.GetProperty("engine").GetProperty("instance_id").GetString() = "fixture-instance" AndAlso
              root.GetProperty("health").GetProperty("ws").GetString() = "REST" AndAlso
              Not root.TryGetProperty("verdict", unused) AndAlso
              Not root.TryGetProperty("levels", unused) AndAlso
              Not root.TryGetProperty("price", unused) AndAlso
              Not root.TryGetProperty("kelly", unused),
              "SKIPPED shape mismatch: " & root.GetRawText())
    End Sub

    ' -- A22c: every NO TRADE* lean fixture ⇒ direction NONE; WEAK carries it --
    Private Sub A22c_NoTradeLeansDirectionNone()
        ' The AppendLean output space: bare + the three lean tags (§8 D2 — leans
        ' live in `verdict` for logging, never actionable).
        Dim noTrades As String() = {"NO TRADE", "NO TRADE [WEAK LONG]",
                                    "NO TRADE [WEAK SHORT]", "NO TRADE [TIE]"}
        Dim allNone As Boolean = True
        Dim detail As String = ""
        For Each nt In noTrades
            Dim d = SignalEmitter.DeriveDirection(nt)
            If d <> "NONE" Then allNone = False : detail &= String.Format("'{0}'→{1} ", nt, d)
        Next
        Check("A22c all NO TRADE* verdicts (incl. lean tags) ⇒ direction NONE", allNone, detail)

        Check("A22c directional verdicts carry direction (WEAK included — tier gate is consumer-side)",
              SignalEmitter.DeriveDirection("WEAK LONG") = "LONG" AndAlso
              SignalEmitter.DeriveDirection("WEAK SHORT") = "SHORT" AndAlso
              SignalEmitter.DeriveDirection("LONG") = "LONG" AndAlso
              SignalEmitter.DeriveDirection("STRONG LONG") = "LONG" AndAlso
              SignalEmitter.DeriveDirection("SHORT") = "SHORT" AndAlso
              SignalEmitter.DeriveDirection("STRONG SHORT") = "SHORT",
              "directional derivation mismatch")

        ' End-to-end: the full payload keeps the lean text in `verdict` while
        ' direction reads NONE.
        Dim vLean = BuildBridgeVerdict()
        vLean.Verdict = "NO TRADE [WEAK LONG]"
        vLean.Confidence = "N/A"
        Dim root = BridgeOkJson(vLean, BuildBridgeIndicators()).RootElement
        Check("A22c payload: verdict keeps the lean text, direction NONE, confidence N/A",
              root.GetProperty("verdict").GetString() = "NO TRADE [WEAK LONG]" AndAlso
              root.GetProperty("direction").GetString() = "NONE" AndAlso
              root.GetProperty("confidence").GetString() = "N/A",
              "lean payload mismatch: " & root.GetRawText())
    End Sub

    ' -- A22d: enum pins — health.ws all four states + precedence --------------
    Private Sub A22d_EnumPins()
        Check("A22d DeriveWsHealth pins (REST wins at transport=rest; DEGRADED > DOWN > OK on ws)",
              SignalEmitter.DeriveWsHealth(False, True, True, True) = "REST" AndAlso
              SignalEmitter.DeriveWsHealth(False, False, False, False) = "REST" AndAlso
              SignalEmitter.DeriveWsHealth(True, True, True, True) = "DEGRADED" AndAlso
              SignalEmitter.DeriveWsHealth(True, False, False, False) = "DOWN" AndAlso
              SignalEmitter.DeriveWsHealth(True, False, True, False) = "DOWN" AndAlso
              SignalEmitter.DeriveWsHealth(True, False, True, True) = "OK",
              "ws-health derivation mismatch")

        ' signal_state is pinned by construction (BuildOk/BuildSkipped are the
        ' only producers); confidence rides VerdictResult verbatim — assert the
        ' pass-through for the four pinned values.
        Dim ok As Boolean = True
        Dim detail As String = ""
        For Each conf In {"HIGH", "MEDIUM", "LOW", "N/A"}
            Dim v = BuildBridgeVerdict()
            v.Confidence = conf
            Dim got = BridgeOkJson(v, BuildBridgeIndicators()).RootElement _
                          .GetProperty("confidence").GetString()
            If got <> conf Then ok = False : detail &= String.Format("{0}→{1} ", conf, got)
        Next
        Check("A22d confidence pin pass-through (HIGH/MEDIUM/LOW/N-A)", ok, detail)
    End Sub

    ' -- A22e: ARM flag rides the payload; process identity stable + monotonic -
    Private Sub A22e_ArmedFlagAndIdentity()
        Dim armedTrue = BridgeOkJson(BuildBridgeVerdict(), BuildBridgeIndicators(), armed:=True) _
                            .RootElement.GetProperty("engine").GetProperty("autotrade_armed").GetBoolean()
        Dim armedFalse = BridgeOkJson(BuildBridgeVerdict(), BuildBridgeIndicators(), armed:=False) _
                             .RootElement.GetProperty("engine").GetProperty("autotrade_armed").GetBoolean()
        Check("A22e autotrade_armed reflects the toggle in every payload", armedTrue AndAlso Not armedFalse,
              String.Format("armed:=True→{0}, armed:=False→{1}", armedTrue, armedFalse))

        Dim id1 As String = ProcessIdentity.InstanceId
        Dim id2 As String = ProcessIdentity.InstanceId
        Dim s1 As Long = ProcessIdentity.NextSignalId()
        Dim s2 As Long = ProcessIdentity.NextSignalId()
        Check("A22e instance_id stable across reads; signal_id strictly monotonic",
              id1 = id2 AndAlso id1.Length > 0 AndAlso s2 = s1 + 1 AndAlso
              ProcessIdentity.CurrentSignalId = s2,
              String.Format("id1={0} id2={1} s1={2} s2={3} current={4}",
                            id1, id2, s1, s2, ProcessIdentity.CurrentSignalId))
    End Sub

    ' -- A22f: serialization pins survive a hostile thread culture -------------
    Private Sub A22f_InvariantCultureSerialization()
        Dim savedCulture = Thread.CurrentThread.CurrentCulture
        Dim savedUi = Thread.CurrentThread.CurrentUICulture
        Try
            Thread.CurrentThread.CurrentCulture = New CultureInfo("de-DE")
            Thread.CurrentThread.CurrentUICulture = New CultureInfo("de-DE")
            Dim json As String = SignalEmitter.Serialize(
                SignalEmitter.BuildOk(BuildBridgeVerdict(), BuildBridgeIndicators(), BuildBridgeCfg(),
                                      "fixture-instance", 7, False, "OK", False,
                                      New DateTime(2026, 7, 3, 14, 31, 2, DateTimeKind.Utc)))
            Dim root = JsonDocument.Parse(json).RootElement
            Check("A22f invariant culture under de-DE (dot decimals, numbers as JSON numbers, ISO-8601 Z)",
                  json.Contains("59012.5") AndAlso Not json.Contains("59012,5") AndAlso
                  root.GetProperty("price").ValueKind = JsonValueKind.Number AndAlso
                  root.GetProperty("atr").ValueKind = JsonValueKind.Number AndAlso
                  root.GetProperty("generated_at_utc").GetString() = "2026-07-03T14:31:02Z",
                  "culture-sensitive serialization detected")
        Finally
            Thread.CurrentThread.CurrentCulture = savedCulture
            Thread.CurrentThread.CurrentUICulture = savedUi
        End Try
    End Sub

    ' -- A22g: HARD CONSTRAINT 18 — signal_bridge.* fenced; sibling tunable ----
    Private Sub A22g_TweakerRejectsSignalBridge()
        Dim s As String = "{""version"":49,""scoring"":{""verdict_med_pct"":0.53}," &
                          """signal_bridge"":{""enabled"":false,""output_path"":""C:\\Dev\\DeribitBridge\\verdict_signal.json""}}"
        Dim rBridge = SettingsDiffApplier.Validate(OneDiff("signal_bridge.enabled", "false", "true"), s, 3)
        Dim rSib = SettingsDiffApplier.Validate(OneDiff("scoring.verdict_med_pct", "0.53", "0.55"), s, 3)
        Check("A22g Validate rejects signal_bridge.enabled (HARD CONSTRAINT 18 fence) + accepts scoring.verdict_med_pct",
              Not rBridge.IsValid AndAlso rBridge.ErrorReason.Contains("off-tweaker-surface") AndAlso rSib.IsValid,
              String.Format("bridge: valid={0} reason='{1}' | sibling: valid={2} reason='{3}'",
                            rBridge.IsValid, rBridge.ErrorReason, rSib.IsValid, rSib.ErrorReason))
    End Sub

    ' =======================================================================
    ' A23 — P4 #5 aggressor velocity (docs/aggressor-velocity-proposal.md §9,
    ' build sub-version). Deterministic folds against the host-agnostic
    ' accumulator; classification via the shipped pure fn; session resolution
    ' via ExecutionResolution; tweaker surface via SettingsDiffApplier.
    ' =======================================================================

    ' -- A23a: steady tape → rate ≈ the analytic fixed point --------------------
    ' 100 USD buys at exactly 1 trade/sec, tauFast=5 / tauNorm=120. Fast-horizon
    ' fixed point A* = a/(1-e^(-dt/tau)) = 551.67 → grossFast = A*/tau = 110.33
    ' USD/s (the ~10% discrete-arrival bias over the true 100/s is inherent to the
    ' EMA-sum construction and calibrated through by §5). Norm horizon at 400 s is
    ' 96.4% converged → grossNorm ≈ 96.8. All-buy tape → lean ≈ +1.
    Private Sub A23a_AggrVelSteadyRate()
        Dim acc As New AggressorVelocityAccumulator()
        Dim ts As Long = 1_000_000
        For i As Integer = 1 To 400
            acc.Fold(100.0, isBuy:=True, tsMs:=ts, tauFastSec:=5.0, tauNormSec:=120.0)
            ts += 1000
        Next
        Dim s = acc.Snapshot(grossFloorUsdPerSec:=50.0, minCoverageSec:=120.0)
        Check("A23a steady tape rate math (grossFast≈110, grossNorm≈97, lean≈+1, warm)",
              s.HasWarmup AndAlso
              s.GrossFastUsdPerSec > 109.0 AndAlso s.GrossFastUsdPerSec < 112.0 AndAlso
              s.GrossNormUsdPerSec > 95.0 AndAlso s.GrossNormUsdPerSec < 99.0 AndAlso
              s.BurstRatio > 1.05 AndAlso s.BurstRatio < 1.25 AndAlso
              s.Lean > 0.99,
              String.Format(CultureInfo.InvariantCulture,
                            "warm={0} fast={1:F2} norm={2:F2} ratio={3:F3} lean={4:F3}",
                            s.HasWarmup, s.GrossFastUsdPerSec, s.GrossNormUsdPerSec, s.BurstRatio, s.Lean))
    End Sub

    ' -- A23b: two-horizon burst — balanced baseline, then a one-sided firehose -
    ' 600 s of balanced 50-USD buy/sell alternation at 2 trades/sec (≈100 USD/s both
    ' horizons, lean ≈ 0), then 20 × 1000 USD BUY prints at 100 ms spacing (a 2 s
    ' 10k USD/s buy burst). The fast horizon jumps far above the slow norm →
    ' burstRatio ≫ 2.5 with a strong positive lean → BURST_BUY.
    Private Sub A23b_AggrVelBurstDetection()
        Dim acc As New AggressorVelocityAccumulator()
        Dim ts As Long = 1_000_000
        Dim buy As Boolean = True
        For i As Integer = 1 To 1200                       ' 600 s balanced baseline
            acc.Fold(50.0, buy, ts, 5.0, 120.0)
            buy = Not buy
            ts += 500
        Next
        Dim preBurst = acc.Snapshot(50.0, 120.0)
        For i As Integer = 1 To 20                          ' the burst
            acc.Fold(1000.0, isBuy:=True, tsMs:=ts, tauFastSec:=5.0, tauNormSec:=120.0)
            ts += 100
        Next
        Dim s = acc.Snapshot(50.0, 120.0)
        Dim sig As String = IndicatorEngine.ClassifyAggressorBurst(s.BurstRatio, s.Lean, 2.5, 0.2)
        Dim preSig As String = IndicatorEngine.ClassifyAggressorBurst(preBurst.BurstRatio, preBurst.Lean, 2.5, 0.2)
        Check("A23b burst detection (balanced baseline NORMAL → one-sided burst BURST_BUY)",
              preSig = "NORMAL" AndAlso Math.Abs(preBurst.Lean) < 0.2 AndAlso
              s.HasWarmup AndAlso s.BurstRatio > 2.5 AndAlso s.Lean > 0.2 AndAlso sig = "BURST_BUY",
              String.Format(CultureInfo.InvariantCulture,
                            "pre: sig={0} lean={1:F3} | post: ratio={2:F2} lean={3:F3} sig={4}",
                            preSig, preBurst.Lean, s.BurstRatio, s.Lean, sig))
    End Sub

    ' -- A23c: cold-start suppression — no warmup before a full norm window -----
    Private Sub A23c_AggrVelColdStartSuppression()
        Dim acc As New AggressorVelocityAccumulator()
        Dim ts As Long = 1_000_000
        For i As Integer = 1 To 3                           ' 3 trades over 10 s
            acc.Fold(100.0, True, ts, 5.0, 120.0) : ts += 5000
        Next
        Dim few = acc.Snapshot(50.0, 120.0)
        For i As Integer = 1 To 10                          ' more trades, still < 120 s coverage
            acc.Fold(100.0, True, ts, 5.0, 120.0) : ts += 5000
        Next
        Dim short120 = acc.Snapshot(50.0, 120.0)            ' coverage 60 s < 120
        For i As Integer = 1 To 16                          ' push coverage past 120 s
            acc.Fold(100.0, True, ts, 5.0, 120.0) : ts += 5000
        Next
        Dim warm = acc.Snapshot(50.0, 120.0)
        Check("A23c cold-start suppression (3 trades → cold; 60s coverage → cold; ≥120s → warm)",
              Not few.HasWarmup AndAlso Not short120.HasWarmup AndAlso warm.HasWarmup,
              String.Format("few={0} short={1} (cov {2:F0}s) warm={3} (cov {4:F0}s)",
                            few.HasWarmup, short120.HasWarmup, short120.CoverageSec,
                            warm.HasWarmup, warm.CoverageSec))
    End Sub

    ' -- A23d: Reset re-arms the cold-start suppression (reconnect semantics) ---
    Private Sub A23d_AggrVelResetReArms()
        Dim acc As New AggressorVelocityAccumulator()
        Dim ts As Long = 1_000_000
        For i As Integer = 1 To 200
            acc.Fold(100.0, True, ts, 5.0, 120.0) : ts += 1000
        Next
        Dim warm As Boolean = acc.Snapshot(50.0, 120.0).HasWarmup
        acc.Reset()
        Dim s = acc.Snapshot(50.0, 120.0)
        Check("A23d Reset re-arms warmup (warm → reset → cold, n=0, coverage=0)",
              warm AndAlso Not s.HasWarmup AndAlso s.TradeCount = 0 AndAlso s.CoverageSec = 0.0,
              String.Format("preWarm={0} postWarm={1} n={2} cov={3:F0}",
                            warm, s.HasWarmup, s.TradeCount, s.CoverageSec))
    End Sub

    ' -- A23e: ClassifyAggressorBurst threshold / lean-floor edges ---------------
    Private Sub A23e_ClassifyAggressorBurstEdges()
        Dim buyBurst      = IndicatorEngine.ClassifyAggressorBurst(3.0, 0.5, 2.5, 0.2)
        Dim sellBurst     = IndicatorEngine.ClassifyAggressorBurst(3.0, -0.5, 2.5, 0.2)
        Dim balancedHose  = IndicatorEngine.ClassifyAggressorBurst(4.0, 0.1, 2.5, 0.2)   ' §2.1 guard
        Dim oneSideTrickle = IndicatorEngine.ClassifyAggressorBurst(1.5, 0.9, 2.5, 0.2)  ' quiet tape
        Dim atThreshold   = IndicatorEngine.ClassifyAggressorBurst(2.5, 0.2, 2.5, 0.2)   ' >= fires
        Dim justUnder     = IndicatorEngine.ClassifyAggressorBurst(2.4999, 1.0, 2.5, 0.2)
        Check("A23e ClassifyAggressorBurst edges (buy/sell fire; balanced firehose + trickle NORMAL; >= boundary)",
              buyBurst = "BURST_BUY" AndAlso sellBurst = "BURST_SELL" AndAlso
              balancedHose = "NORMAL" AndAlso oneSideTrickle = "NORMAL" AndAlso
              atThreshold = "BURST_BUY" AndAlso justUnder = "NORMAL",
              String.Format("buy={0} sell={1} hose={2} trickle={3} at={4} under={5}",
                            buyBurst, sellBurst, balancedHose, oneSideTrickle, atThreshold, justUnder))
    End Sub

    ' -- A23f: per-session norm/threshold resolution (v40 override pattern) -----
    ' POCO defaults: NY bucket (hour 13-23) carries norm_window_sec 60; LONDON/ASIA
    ' inherit the shared default 120 / 2.5. An explicit per-session threshold
    ' override resolves ahead of the default.
    Private Sub A23f_AggrVelSessionResolution()
        Dim cfg As New EngineSettings()
        Dim nyNorm     = ExecutionResolution.ResolveAggrVelNormWindow(cfg, 14)
        Dim londonNorm = ExecutionResolution.ResolveAggrVelNormWindow(cfg, 9)
        Dim asiaNorm   = ExecutionResolution.ResolveAggrVelNormWindow(cfg, 3)
        Dim nyThr      = ExecutionResolution.ResolveAggrVelBurstThreshold(cfg, 14)
        cfg.Indicators.AggressorVelocity.Sessions("ASIA").BurstRatioThreshold = 3.1
        Dim asiaThr    = ExecutionResolution.ResolveAggrVelBurstThreshold(cfg, 3)
        ' [v52 wire-in] NY carries an EXPLICIT burst_ratio_threshold 4.5 (the §5.2 value, also
        ' the S2a scoping key). [comment corrected v65] The old text claimed "LONDON/ASIA still
        ' inherit the 2.5 default" — stale since v60 armed LONDON at 5.5, and doubly so since
        ' v65/D3 armed ASIA at 5.5. What res-3 still inherits is the NORM WINDOW (120), which is
        ' what this fixture asserts; the threshold arm here sets ASIA explicitly to 3.1, so it
        ' demonstrates override-wins over ASIA's shipped 5.5 rather than over the 2.5 default.
        Check("A23f per-session resolution (NY norm 60 / thr 4.5; LONDON/ASIA inherit norm 120; explicit override wins)",
              nyNorm = 60.0 AndAlso londonNorm = 120.0 AndAlso asiaNorm = 120.0 AndAlso
              nyThr = 4.5 AndAlso asiaThr = 3.1,
              String.Format(CultureInfo.InvariantCulture,
                            "nyNorm={0} lonNorm={1} asiaNorm={2} nyThr={3} asiaThr={4}",
                            nyNorm, londonNorm, asiaNorm, nyThr, asiaThr))
    End Sub

    ' -- A23g: three-tier tweaker surface (HARD CONSTRAINT 19) -------------------
    ' Switches exact-match rejected; default./sessions. prefix rejected (hand-tuned
    ' tier); the flat params stay proposable.
    Private Sub A23g_AggrVelTweakerSurface()
        Dim s As String = "{""version"":49,""indicators"":{""aggressor_velocity"":{" &
                          """enabled"":true,""scoring_enabled"":false,""fast_window_sec"":5," &
                          """direction_lean_floor"":0.2,""gross_floor_usd_per_sec"":50," &
                          """upgrade_bonus"":1,""contra_penalty"":1," &
                          """default"":{""norm_window_sec"":120,""burst_ratio_threshold"":2.5}," &
                          """sessions"":{""NY"":{""norm_window_sec"":60},""LONDON"":{},""ASIA"":{}}}}}"
        Dim rEnabled = SettingsDiffApplier.Validate(OneDiff("indicators.aggressor_velocity.enabled", "true", "false"), s, 3)
        Dim rScoring = SettingsDiffApplier.Validate(OneDiff("indicators.aggressor_velocity.scoring_enabled", "false", "true"), s, 3)
        Dim rSession = SettingsDiffApplier.Validate(OneDiff("indicators.aggressor_velocity.sessions.NY.norm_window_sec", "60", "45"), s, 3)
        Dim rDefault = SettingsDiffApplier.Validate(OneDiff("indicators.aggressor_velocity.default.burst_ratio_threshold", "2.5", "2.0"), s, 3)
        Dim rFast    = SettingsDiffApplier.Validate(OneDiff("indicators.aggressor_velocity.fast_window_sec", "5", "7"), s, 3)
        Dim rBonus   = SettingsDiffApplier.Validate(OneDiff("indicators.aggressor_velocity.upgrade_bonus", "1", "2"), s, 3)
        Check("A23g aggressor_velocity three-tier surface (switches + default./sessions. fenced; flat params tunable)",
              Not rEnabled.IsValid AndAlso rEnabled.ErrorReason.Contains("HARD CONSTRAINT 19") AndAlso
              Not rScoring.IsValid AndAlso rScoring.ErrorReason.Contains("HARD CONSTRAINT 19") AndAlso
              Not rSession.IsValid AndAlso rSession.ErrorReason.Contains("off-tweaker-surface") AndAlso
              Not rDefault.IsValid AndAlso rDefault.ErrorReason.Contains("off-tweaker-surface") AndAlso
              rFast.IsValid AndAlso rBonus.IsValid,
              String.Format("enabled={0}'{1}' scoring={2} session={3} default={4} fast={5}'{6}' bonus={7}",
                            rEnabled.IsValid, rEnabled.ErrorReason, rScoring.IsValid,
                            rSession.IsValid, rDefault.IsValid, rFast.IsValid, rFast.ErrorReason, rBonus.IsValid))
    End Sub

    ' =======================================================================
    ' A24 — CSV v0.8 Placed* ≡ payload levels (one shared arbitration)
    ' =======================================================================

    ' -- A24a: ComputeSideLevels output = the emitted payload levels, across the
    ' three placement cases A22a pins (structural-placed / no-structure fallback /
    ' cap-noise-suppressed — B4b: the cases are r-driven, the arbitration ignores
    ' v.Adjusted* on the structural-first path). The CSV Placed* columns and the
    ' payload levels block both read ComputeSideLevels, so this pin holds the
    ' parity for both surfaces.
    Private Sub A24a_PlacedLevelsEqualPayloadLevels()
        Dim cfg = BuildBridgeCfg()

        ' Case fixtures: (name, r) — placed / bare-fallback / noise-suppressed.
        Dim rPlaced = BuildBridgeIndicators()
        Dim rBare As New IndicatorResults()
        rBare.CurrentPrice = 59012.5 : rBare.ATR = 41.3 : rBare.ExecResolution = 1
        Dim rNoise = BuildBridgeIndicators()
        rNoise.SwingTargetLong = 59085.0
        Dim cases = New List(Of (Name As String, R As IndicatorResults)) From {
            ("placed", rPlaced), ("fallback", rBare), ("noise", rNoise)}

        Dim ok As Boolean = True
        Dim detail As String = ""
        For Each c In cases
            Dim v = BuildBridgeVerdict()
            Dim lvLong = SignalEmitter.ComputeSideLevels(v, c.R, cfg, isLong:=True)
            Dim lvShort = SignalEmitter.ComputeSideLevels(v, c.R, cfg, isLong:=False)
            Dim levels = BridgeOkJson(v, c.R).RootElement.GetProperty("levels")
            Dim pl = levels.GetProperty("long")
            Dim ps = levels.GetProperty("short")
            If JNum(pl, "target") <> lvLong.Target OrElse JNum(pl, "stop") <> lvLong.StopPx OrElse
               JNum(pl, "raw_target") <> lvLong.RawTarget OrElse
               pl.GetProperty("target_capped").GetBoolean() <> lvLong.Capped OrElse
               JNum(ps, "target") <> lvShort.Target OrElse JNum(ps, "stop") <> lvShort.StopPx Then
                ok = False
                detail &= c.Name & " diverged; "
            End If
        Next

        ' Sanity-pin the placed case's absolute values: swing target 59095 placed
        ' (farther than the 1.75×ATR fallback — the B4b behavioural delta), swing
        ' stop 152.5 away → DG1 clamp at entry − 1.6×ATR, labeled STOP_CLAMPED.
        Dim pin = SignalEmitter.ComputeSideLevels(BuildBridgeVerdict(), rPlaced, cfg, isLong:=True)
        If Not (pin.Capped AndAlso pin.Target = 59095.0 AndAlso
                pin.Reason = "PLACED @ 59095.0 (SWING_HIGH_5M)" AndAlso
                pin.TargetReason = "SWING_HIGH_5M" AndAlso
                pin.StopReason = "STOP_CLAMPED" AndAlso
                Math.Abs(pin.StopPx - (59012.5 - 41.3 * 1.6)) < 0.0001) Then
            ok = False
            detail &= String.Format(CultureInfo.InvariantCulture,
                                    "pin mismatch: capped={0} target={1} reason='{2}' stopReason={3} stop={4}; ",
                                    pin.Capped, pin.Target, pin.Reason, pin.StopReason, pin.StopPx)
        End If
        Check("A24a Placed* levels ≡ payload levels (shared ComputeSideLevels, 3 placement cases + pin)", ok, detail)
    End Sub

    ' =======================================================================
    ' A25 — v50 retune cargo (signal-health-retune-proposal.md §2 R1 + §5)
    ' =======================================================================

    ' -- A25a: R1 — momentum_enabled=false leaves the OFI level award byte-identical
    ' to the no-modifier path. Reuses the A8 harness: OFI SELL DOMINANT fixture with
    '   (a) enabled=False + OFIMomentum FALLING (a would-be +1[S] confirm)
    '   (b) enabled=True  + OFIMomentum FLAT   (modifier inert by state)
    ' → identical scores; note renders MOM:state with NO modifier suffix.
    '   (c) enabled=True  + OFIMomentum FALLING → +1 short — the flag still gates.
    Private Function OfiNote(v As VerdictResult) As String
        For Each item In v.SignalBreakdown
            If item.Label = "OFI" Then Return item.Note
        Next
        Return "(no OFI row)"
    End Function

    Private Sub A25a_OfiMomentumRetireByteIdentical()
        Dim rA = BuildA8Indicators() : rA.OFIMomentum = "FALLING"
        Dim cfgA = BuildA8Cfg(fundingBoost:=3)            ' MomentumEnabled=False already
        Dim vA = ScoringEngine.Calculate(rA, PositionState.None, BuildA8Norms(), cfgA)

        Dim rB = BuildA8Indicators()                       ' OFIMomentum FLAT
        Dim cfgB = BuildA8Cfg(fundingBoost:=3)
        cfgB.Indicators.OFI.MomentumEnabled = True
        Dim vB = ScoringEngine.Calculate(rB, PositionState.None, BuildA8Norms(), cfgB)

        Check("A25a R1 retire — disabled modifier ≡ no-modifier path (scores + verdict), MOM:state kept, no suffix",
              vA.Verdict = vB.Verdict AndAlso
              vA.LongScore = vB.LongScore AndAlso vA.ShortScore = vB.ShortScore AndAlso
              vA.EffectiveLongScore = vB.EffectiveLongScore AndAlso
              vA.EffectiveShortScore = vB.EffectiveShortScore AndAlso
              OfiNote(vA).Contains("MOM:FALLING") AndAlso
              Not OfiNote(vA).Contains("confirmed") AndAlso Not OfiNote(vA).Contains("suppressed"),
              String.Format("A: '{0}' {1}/{2} note='{3}' | B: '{4}' {5}/{6}",
                            vA.Verdict, vA.EffectiveLongScore, vA.EffectiveShortScore, OfiNote(vA),
                            vB.Verdict, vB.EffectiveLongScore, vB.EffectiveShortScore))

        Dim rC = BuildA8Indicators() : rC.OFIMomentum = "FALLING"
        Dim cfgC = BuildA8Cfg(fundingBoost:=3)
        cfgC.Indicators.OFI.MomentumEnabled = True
        Dim vC = ScoringEngine.Calculate(rC, PositionState.None, BuildA8Norms(), cfgC)
        Check("A25a flag still gates (enabled + FALLING on SELL DOMINANT → +1[S] confirm)",
              vC.ShortScore = vA.ShortScore + 1 AndAlso OfiNote(vC).Contains("confirmed"),
              String.Format("expected short {0}, got {1}; note='{2}'",
                            vA.ShortScore + 1, vC.ShortScore, OfiNote(vC)))
    End Sub

    ' -- A25b: HC20 — indicators.OFI.momentum_ prefix fenced; siblings tunable ---
    Private Sub A25b_TweakerRejectsOfiMomentumPrefix()
        Dim s As String = "{""version"":49,""indicators"":{""OFI"":{""book_depth"":5," &
                          """buy_dominant_ratio"":1.60,""sell_dominant_ratio"":0.625," &
                          """momentum_enabled"":false,""momentum_window"":3," &
                          """momentum_threshold"":0.15,""momentum_bonus"":1," &
                          """averaging_enabled"":true,""avg_window_sec"":10}}}"
        Dim rBonus = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.momentum_bonus", "1", "2"), s, 3)
        Dim rThr   = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.momentum_threshold", "0.15", "0.10"), s, 3)
        Dim rDepth = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.book_depth", "5", "4"), s, 3)
        Dim rDom   = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.buy_dominant_ratio", "1.60", "1.5"), s, 3)
        Dim rWin   = SettingsDiffApplier.Validate(OneDiff("indicators.OFI.avg_window_sec", "10", "12"), s, 3)
        Check("A25b Validate rejects indicators.OFI.momentum_* (HC20 fence) + accepts book_depth/dominance/avg_window_sec",
              Not rBonus.IsValid AndAlso rBonus.ErrorReason.Contains("off-tweaker-surface") AndAlso
              Not rThr.IsValid AndAlso
              rDepth.IsValid AndAlso rDom.IsValid AndAlso rWin.IsValid,
              String.Format("bonus={0}'{1}' thr={2} depth={3} dom={4} win={5}",
                            rBonus.IsValid, rBonus.ErrorReason, rThr.IsValid,
                            rDepth.IsValid, rDom.IsValid, rWin.IsValid))
    End Sub

    ' =======================================================================
    ' A26 — B4b placed-geometry structural-first levels (v51).
    ' docs/placed-geometry-structural-first-proposal.md §3/§10, values per
    ' docs/placed-geometry-derivation-2026-07-06.md §4 (DG1–DG5).
    ' Arbitration fixtures run the shipped SignalEmitter.ComputeSideLevels
    ' directly (POCO-default cfg: enabled, fallback 1.75/1.6, bound 3.5×ATR,
    ' stop bound 1.6×ATR, floor 4 ticks = $2). Gate fixtures run the real
    ' ScoringEngine.Calculate(). ATR 40 / entry 62000 ⇒ fallback target dist 70,
    ' target bound 140, stop bound/fallback dist 64, stop floor 2.0.
    ' =======================================================================

    Private Function BuildPgCfg() As EngineSettings
        Return New EngineSettings()          ' POCO defaults = shipped v51 values
    End Function

    Private Function BuildPgIndicators() As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = 62000.0
        r.ATR = 40.0
        Return r
    End Function

    ' The arbitration ignores v on the structural-first path — a bare result is fine.
    Private Function PgLevels(r As IndicatorResults, isLong As Boolean) As SideLevels
        Return SignalEmitter.ComputeSideLevels(New VerdictResult(), r, BuildPgCfg(), isLong)
    End Function

    ' -- A26a: structural target PLACES even when FARTHER than the ATR fallback --
    ' The key behavioural delta vs the legacy closest-wins cap (A12 pins that path):
    ' swing dist 100 > fallback dist 70, but ≤ the 140 bound → structure wins.
    Private Sub A26a_StructuralTargetPlacesEvenWhenFarther()
        Dim rL = BuildPgIndicators() : rL.SwingTargetLong = 62100.0
        Dim lvL = PgLevels(rL, isLong:=True)
        Check("A26a long swing target farther than fallback still PLACES (62100, dist 100 ≤ bound 140)",
              lvL.Target = 62100.0 AndAlso lvL.Capped AndAlso
              lvL.Reason = "PLACED @ 62100.0 (SWING_HIGH_5M)" AndAlso
              lvL.TargetReason = "SWING_HIGH_5M" AndAlso
              Math.Abs(lvL.RawTarget - 62070.0) < 0.0001,
              String.Format(CultureInfo.InvariantCulture,
                            "target={0} capped={1} reason='{2}' raw={3}",
                            lvL.Target, lvL.Capped, lvL.Reason, lvL.RawTarget))

        Dim rS = BuildPgIndicators() : rS.SwingTargetShort = 61900.0
        Dim lvS = PgLevels(rS, isLong:=False)
        Check("A26a short mirror (swing 61900 places, label SWING_LOW_5M, raw 61930)",
              lvS.Target = 61900.0 AndAlso lvS.Capped AndAlso
              lvS.Reason = "PLACED @ 61900.0 (SWING_LOW_5M)" AndAlso
              lvS.TargetReason = "SWING_LOW_5M" AndAlso
              Math.Abs(lvS.RawTarget - 61930.0) < 0.0001,
              String.Format(CultureInfo.InvariantCulture,
                            "target={0} capped={1} reason='{2}' raw={3}",
                            lvS.Target, lvS.Capped, lvS.Reason, lvS.RawTarget))
    End Sub

    ' -- A26b: too-loose structural target walks the ladder → HVN → POC → fallback --
    Private Sub A26b_TargetLadderTooLooseWalksTiers()
        ' Swing too loose (150 > 140) → the HVN tier places (120 ≤ 140).
        Dim rHvn = BuildPgIndicators()
        rHvn.SwingTargetLong = 62150.0
        rHvn.VPFRNearestHvnAbove = 62120.0
        Dim lvHvn = PgLevels(rHvn, isLong:=True)
        Check("A26b swing too loose → NEAREST_HVN_ABOVE places (62120)",
              lvHvn.Target = 62120.0 AndAlso lvHvn.Capped AndAlso
              lvHvn.TargetReason = "NEAREST_HVN_ABOVE" AndAlso
              lvHvn.Reason = "PLACED @ 62120.0 (NEAREST_HVN_ABOVE)",
              String.Format(CultureInfo.InvariantCulture, "target={0} reason='{1}'", lvHvn.Target, lvHvn.Reason))

        ' Swing + HVN too loose, POC gated open (NEAR_HVN_RESIST) → POC places.
        Dim rPoc = BuildPgIndicators()
        rPoc.SwingTargetLong = 62150.0
        rPoc.VPFRNearestHvnAbove = 62200.0
        rPoc.VPFRSignal = "NEAR_HVN_RESIST"
        rPoc.VPFRPoc = 62050.0
        Dim lvPoc = PgLevels(rPoc, isLong:=True)
        Check("A26b swing+HVN too loose → HVN-gated POC places (62050)",
              lvPoc.Target = 62050.0 AndAlso lvPoc.Capped AndAlso lvPoc.TargetReason = "POC",
              String.Format(CultureInfo.InvariantCulture, "target={0} reason='{1}'", lvPoc.Target, lvPoc.Reason))

        ' No tier survives (POC gate closed) → ATR fallback, uncapped, labeled.
        Dim rFb = BuildPgIndicators()
        rFb.SwingTargetLong = 62150.0
        rFb.VPFRNearestHvnAbove = 62200.0
        rFb.VPFRPoc = 62050.0                      ' present but NOT gated (VPFRSignal neutral)
        Dim lvFb = PgLevels(rFb, isLong:=True)
        Check("A26b no tier survives → FALLBACK_ATR (62070, capped=false, reason=Nothing)",
              lvFb.Target = 62070.0 AndAlso Not lvFb.Capped AndAlso lvFb.Reason Is Nothing AndAlso
              lvFb.TargetReason = "FALLBACK_ATR",
              String.Format(CultureInfo.InvariantCulture, "target={0} capped={1} label={2}",
                            lvFb.Target, lvFb.Capped, lvFb.TargetReason))
    End Sub

    ' -- A26c: DG1 stop shapes — min(structural, 1.6×ATR), floor-guarded ----------
    Private Sub A26c_StopShapes()
        ' Structural stop within [floor, bound] → SWING_STOP at the swing price.
        Dim rSwing = BuildPgIndicators() : rSwing.SwingStopLong = 61950.0   ' dist 50 ∈ [2, 64]
        Dim lvSwing = PgLevels(rSwing, isLong:=True)
        Check("A26c structural stop within bound → SWING_STOP (61950)",
              lvSwing.StopPx = 61950.0 AndAlso lvSwing.StopReason = "SWING_STOP",
              String.Format(CultureInfo.InvariantCulture, "stop={0} label={1}", lvSwing.StopPx, lvSwing.StopReason))

        ' Structural stop wider than the bound → D3 clamp at entry − 1.6×ATR.
        Dim rClamp = BuildPgIndicators() : rClamp.SwingStopLong = 61900.0   ' dist 100 > 64
        Dim lvClamp = PgLevels(rClamp, isLong:=True)
        Check("A26c structural stop too loose → STOP_CLAMPED (61936 = entry − 64)",
              Math.Abs(lvClamp.StopPx - 61936.0) < 0.0001 AndAlso lvClamp.StopReason = "STOP_CLAMPED",
              String.Format(CultureInfo.InvariantCulture, "stop={0} label={1}", lvClamp.StopPx, lvClamp.StopReason))

        ' No structural stop → FALLBACK_ATR at the same distance (stop_max = fallback).
        Dim lvNone = PgLevels(BuildPgIndicators(), isLong:=True)
        Check("A26c no structural stop → FALLBACK_ATR (61936)",
              Math.Abs(lvNone.StopPx - 61936.0) < 0.0001 AndAlso lvNone.StopReason = "FALLBACK_ATR",
              String.Format(CultureInfo.InvariantCulture, "stop={0} label={1}", lvNone.StopPx, lvNone.StopReason))

        ' Structural stop under the 4-tick ($2) floor → FALLBACK_ATR, not the swing.
        Dim rFloor = BuildPgIndicators() : rFloor.SwingStopLong = 61999.0   ' dist 1.0 < 2.0
        Dim lvFloor = PgLevels(rFloor, isLong:=True)
        Check("A26c sub-floor structural stop → FALLBACK_ATR (61936, not 61999)",
              Math.Abs(lvFloor.StopPx - 61936.0) < 0.0001 AndAlso lvFloor.StopReason = "FALLBACK_ATR",
              String.Format(CultureInfo.InvariantCulture, "stop={0} label={1}", lvFloor.StopPx, lvFloor.StopReason))

        ' Short mirror: structural stop above entry within bound → SWING_STOP.
        Dim rShort = BuildPgIndicators() : rShort.SwingStopShort = 62050.0  ' dist 50
        Dim lvShort = PgLevels(rShort, isLong:=False)
        Check("A26c short mirror structural stop → SWING_STOP (62050)",
              lvShort.StopPx = 62050.0 AndAlso lvShort.StopReason = "SWING_STOP",
              String.Format(CultureInfo.InvariantCulture, "stop={0} label={1}", lvShort.StopPx, lvShort.StopReason))
    End Sub

    ' -- A26d: the v35 min-move gate evaluates the PLACED target (real Calculate) --
    ' ATR 20 ⇒ fallback dist 35 < floor 49.6 (would gate), but a swing target at
    ' dist 60 (≤ bound 70) PLACES → the directional verdict STANDS. Same r with
    ' structural levels disabled gates — the difference is placed geometry, and
    ' Step 5b's copy-out (Adjusted*/TargetCapReason*) carries the placed values.
    Private Sub A26d_MinMoveGateReadsPlacedTarget()
        Dim cfg = BuildA8Cfg(fundingBoost:=0)              ' SHORT dominant; floor 0.0008
        Dim rGate = BuildGateIndicators(atr:=20, price:=62000)
        rGate.SwingTargetShort = 61940.0                   ' dist 60 ≤ 3.5×20=70 → places
        Dim vStand = ScoringEngine.Calculate(rGate, PositionState.None, BuildA8Norms(), cfg)
        Check("A26d placed structural target 60 > floor 49.6 → directional stands (fallback 35 would gate)",
              Not vStand.Verdict.StartsWith("NO TRADE") AndAlso
              vStand.VerdictContext <> "BELOW_MIN_MOVE" AndAlso
              Math.Abs(vStand.AdjustedShortTarget - 61940.0) < 0.001 AndAlso
              vStand.TargetCapReasonShort = "PLACED @ 61940.0 (SWING_LOW_5M)",
              String.Format("verdict='{0}' ctx={1} adj={2:F1} reason='{3}'",
                            vStand.Verdict, vStand.VerdictContext,
                            vStand.AdjustedShortTarget, vStand.TargetCapReasonShort))

        Dim cfgOff = BuildA8Cfg(fundingBoost:=0)
        cfgOff.Scoring.StructuralLevels.Enabled = False
        Dim rGate2 = BuildGateIndicators(atr:=20, price:=62000)
        rGate2.SwingTargetShort = 61940.0                  ' legacy cap can't fire (beyond raw 61965)
        Dim vGated = ScoringEngine.Calculate(rGate2, PositionState.None, BuildA8Norms(), cfgOff)
        Check("A26d same r, structural disabled → legacy effective target 35 < floor → NO TRADE",
              vGated.Verdict = "NO TRADE" AndAlso vGated.VerdictContext = "BELOW_MIN_MOVE",
              String.Format("verdict='{0}' ctx={1}", vGated.Verdict, vGated.VerdictContext))
    End Sub

    ' -- A26e: DG3 session fallback-target multiplier resolution -------------------
    Private Sub A26e_SessionFallbackMultiplier()
        Dim cfg = BuildPgCfg()                             ' POCO: LONDON 2.0 / ASIA 1.25 / NY inherit
        Dim mLon = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, 10)
        Dim mAsia = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, 3)
        Dim mNy = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, 15)
        Dim mUnset = ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, -1)
        Dim cfgOff = BuildPgCfg()
        cfgOff.Scoring.StructuralLevels.Enabled = False
        Dim mOff = ExecutionResolution.ResolveFallbackTargetMultiplier(cfgOff, 10)
        Check("A26e session multiplier (LONDON 2.0 / ASIA 1.25 / NY 1.75 / unstamped 1.75 / disabled 1.75)",
              mLon = 2.0 AndAlso mAsia = 1.25 AndAlso mNy = 1.75 AndAlso
              mUnset = 1.75 AndAlso mOff = 1.75,
              String.Format(CultureInfo.InvariantCulture,
                            "lon={0} asia={1} ny={2} unset={3} off={4}", mLon, mAsia, mNy, mUnset, mOff))

        ' End-to-end: an ASIA-stamped run's fallback target uses 1.25×ATR.
        Dim rAsia = BuildPgIndicators()
        rAsia.SessionUtcHour = 3
        Dim lvAsia = PgLevels(rAsia, isLong:=True)
        Check("A26e ASIA-stamped fallback target = entry + 1.25×ATR (62050)",
              Math.Abs(lvAsia.Target - 62050.0) < 0.0001 AndAlso lvAsia.TargetReason = "FALLBACK_ATR",
              String.Format(CultureInfo.InvariantCulture, "target={0} label={1}", lvAsia.Target, lvAsia.TargetReason))
    End Sub

    ' -- A26f: enabled:false ⇒ byte-identical v50 legacy geometry ------------------
    ' Replicates the pre-B4b A22a/A24a level semantics exactly (pure 1.2×ATR stop,
    ' v.Adjusted*-driven target with the v30 noise suppression, no source labels).
    ' A12 pins the Calculate()-side legacy cap under the same flag.
    Private Sub A26f_DisabledIsByteIdenticalLegacy()
        Dim cfg As New EngineSettings()
        cfg.Scoring.StructuralLevels.Enabled = False
        cfg.Scoring.AtrTargetMultiplier = 2.0              ' the v50 values
        cfg.Scoring.AtrStopMultiplier = 1.2
        Dim r = BuildBridgeIndicators()                    ' swing data present — must be IGNORED

        Dim vPlain = BuildBridgeVerdict()
        Dim lvPlain = SignalEmitter.ComputeSideLevels(vPlain, r, cfg, isLong:=True)
        Dim lvPlainS = SignalEmitter.ComputeSideLevels(vPlain, r, cfg, isLong:=False)
        Dim okPlain As Boolean =
            Math.Abs(lvPlain.StopPx - (59012.5 - 41.3 * 1.2)) < 0.0001 AndAlso
            Math.Abs(lvPlain.Target - (59012.5 + 41.3 * 2.0)) < 0.0001 AndAlso
            Not lvPlain.Capped AndAlso lvPlain.Reason Is Nothing AndAlso
            lvPlain.StopReason Is Nothing AndAlso lvPlain.TargetReason Is Nothing AndAlso
            Math.Abs(lvPlainS.StopPx - (59012.5 + 41.3 * 1.2)) < 0.0001 AndAlso
            lvPlainS.StopReason Is Nothing

        Dim vCap = BuildBridgeVerdict()
        vCap.AdjustedLongTarget = 59060.0
        vCap.TargetCapReasonLong = "CAPPED @ 59060.0 (SWING_HIGH_5M)"
        Dim lvCap = SignalEmitter.ComputeSideLevels(vCap, r, cfg, isLong:=True)
        Dim okCap As Boolean =
            lvCap.Target = 59060.0 AndAlso lvCap.Capped AndAlso
            lvCap.Reason = "CAPPED @ 59060.0 (SWING_HIGH_5M)" AndAlso
            Math.Abs(lvCap.RawTarget - (59012.5 + 41.3 * 2.0)) < 0.0001

        Dim vNoise = BuildBridgeVerdict()
        vNoise.AdjustedLongTarget = 59095.0                ' raw 59095.1 → |0.1| < floor 0.826
        vNoise.TargetCapReasonLong = "CAPPED @ 59095.0 (SWING_HIGH_5M)"
        Dim lvNoise = SignalEmitter.ComputeSideLevels(vNoise, r, cfg, isLong:=True)
        Dim okNoise As Boolean =
            lvNoise.Target = 59095.0 AndAlso Not lvNoise.Capped AndAlso lvNoise.Reason Is Nothing

        Check("A26f enabled:false byte-identical legacy trio (uncapped / capped / noise-suppressed, no labels)",
              okPlain AndAlso okCap AndAlso okNoise,
              String.Format("plain={0} cap={1} noise={2}", okPlain, okCap, okNoise))
    End Sub

    ' -- A26g: HC21 — structural_levels three-tier tweaker surface ------------------
    Private Sub A26g_TweakerStructuralLevelsSurface()
        Dim s As String = "{""version"":51,""scoring"":{""atr_target_multiplier"":1.75," &
                          """atr_stop_multiplier"":1.6,""structural_levels"":{""enabled"":true," &
                          """target_max_atr_mult"":3.5,""stop_max_atr_mult"":1.6," &
                          """stop_min_floor_ticks"":4,""stop_too_loose_mode"":""clamp""," &
                          """sessions"":{""LONDON"":{""fallback_target_atr_mult"":2.0}}}}}"
        Dim rEnabled = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.enabled", "true", "false"), s, 3)
        Dim rMode = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.stop_too_loose_mode", """clamp""", """skip"""), s, 3)
        Dim rSession = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.sessions.LONDON.fallback_target_atr_mult", "2.0", "1.5"), s, 3)
        Dim rBound = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.target_max_atr_mult", "3.5", "3.0"), s, 3)
        Dim rStopMax = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.stop_max_atr_mult", "1.6", "1.8"), s, 3)
        Dim rFloor = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.stop_min_floor_ticks", "4", "6"), s, 3)
        Dim rFallback = SettingsDiffApplier.Validate(OneDiff("scoring.atr_target_multiplier", "1.75", "1.9"), s, 3)
        Check("A26g structural_levels three-tier surface (switches + sessions. fenced; flat numerics + fallback mult tunable)",
              Not rEnabled.IsValid AndAlso rEnabled.ErrorReason.Contains("HARD CONSTRAINT 21") AndAlso
              Not rMode.IsValid AndAlso
              Not rSession.IsValid AndAlso rSession.ErrorReason.Contains("off-tweaker-surface") AndAlso
              rBound.IsValid AndAlso rStopMax.IsValid AndAlso rFloor.IsValid AndAlso rFallback.IsValid,
              String.Format("enabled={0}'{1}' mode={2} session={3}'{4}' bound={5} stopMax={6} floor={7} fallback={8}",
                            rEnabled.IsValid, rEnabled.ErrorReason, rMode.IsValid,
                            rSession.IsValid, rSession.ErrorReason,
                            rBound.IsValid, rStopMax.IsValid, rFloor.IsValid, rFallback.IsValid))
    End Sub

    ' =======================================================================
    ' A27 — D6 eval-barrier migration onto placed levels.
    ' docs/d6-eval-placed-stop-migration-proposal.md (APPROVED 2026-07-14).
    ' The eval adverse barrier moves from the raw ~9×ATR swing stop onto the
    ' placed stop the engine emits and the autotrader executes, so stop-outs
    ' become recordable. Live tracker sources barriers from ComputeSideLevels;
    ' offline sources them from the logged Placed* columns.
    ' =======================================================================

    Private Function BuildD6Indicators() As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = 62000.0
        r.ATR = 40.0
        r.SessionUtcHour = 15          ' NY → fallback target multiplier = base 1.75
        Return r
    End Function

    ' -- A27a: live tracker barriers ≡ ComputeSideLevels (3 arbitration cases) -----
    ' fallback target dist 70 (=1.75×40), target bound 140 (=3.5×40), stop dist 64
    ' (=1.6×40), sub-tick noise floor max(0.5, 0.8)=0.8; min-move floor 49.6.
    Private Sub A27a_TrackerBarriersEqualComputeSideLevels()
        Dim cfg As New EngineSettings()                  ' POCO defaults = shipped v51
        Dim vLong As New VerdictResult With {.Verdict = "LONG"}
        Dim nowUtc As DateTime = DateTime.UtcNow

        ' Case 1 — fallback (no structural levels): FavBar = ATR-fallback target 62070,
        ' AdvBar = fallback stop 61936. The tracker stores exactly ComputeSideLevels'.
        Dim rFb = BuildD6Indicators()
        Dim eFb = LivePerformanceTracker.BuildLiveEntry(vLong, rFb, cfg, nowUtc)
        Dim expFb = SignalEmitter.ComputeSideLevels(vLong, rFb, cfg, isLong:=True)
        Check("A27a tracker barriers = ComputeSideLevels (fallback)",
              eFb.EvalOutcome = "PENDING" AndAlso
              eFb.FavBar = expFb.Target AndAlso eFb.AdvBar = expFb.StopPx AndAlso
              expFb.TargetReason = "FALLBACK_ATR" AndAlso
              Math.Abs(eFb.FavBar - 62070.0) < 0.0001 AndAlso Math.Abs(eFb.AdvBar - 61936.0) < 0.0001,
              String.Format(CultureInfo.InvariantCulture, "fav={0} adv={1} tReason={2} outcome={3}",
                            eFb.FavBar, eFb.AdvBar, expFb.TargetReason, eFb.EvalOutcome))

        ' Case 2 — structural placed: swing target 62100 (dist 100 ≤ 140) + swing stop
        ' 61950 (dist 50 ∈ [floor, 64] → SWING_STOP). Barriers track the placed levels.
        Dim rSt = BuildD6Indicators()
        rSt.SwingTargetLong = 62100.0
        rSt.SwingStopLong = 61950.0
        Dim eSt = LivePerformanceTracker.BuildLiveEntry(vLong, rSt, cfg, nowUtc)
        Dim expSt = SignalEmitter.ComputeSideLevels(vLong, rSt, cfg, isLong:=True)
        Check("A27a tracker barriers = ComputeSideLevels (structural placed)",
              eSt.FavBar = expSt.Target AndAlso eSt.AdvBar = expSt.StopPx AndAlso
              eSt.FavBar = 62100.0 AndAlso eSt.AdvBar = 61950.0 AndAlso
              expSt.TargetReason = "SWING_HIGH_5M" AndAlso expSt.StopReason = "SWING_STOP",
              String.Format(CultureInfo.InvariantCulture, "fav={0} adv={1} t={2} s={3}",
                            eSt.FavBar, eSt.AdvBar, expSt.TargetReason, expSt.StopReason))

        ' Case 3 — noise-suppressed: structural target 62070.3 within the 0.8 sub-tick
        ' floor of the fallback 62070 → Capped=false, but Target is still the structural
        ' price; the tracker stores that exact placed value (not the raw fallback).
        Dim rNs = BuildD6Indicators()
        rNs.SwingTargetLong = 62070.3
        Dim eNs = LivePerformanceTracker.BuildLiveEntry(vLong, rNs, cfg, nowUtc)
        Dim expNs = SignalEmitter.ComputeSideLevels(vLong, rNs, cfg, isLong:=True)
        Check("A27a tracker barriers = ComputeSideLevels (noise-suppressed structural)",
              eNs.FavBar = expNs.Target AndAlso eNs.AdvBar = expNs.StopPx AndAlso
              Not expNs.Capped AndAlso Math.Abs(eNs.FavBar - 62070.3) < 0.0001,
              String.Format(CultureInfo.InvariantCulture, "fav={0} capped={1}", eNs.FavBar, expNs.Capped))
    End Sub

    ' -- A27b: offline adverse-barrier routing (Placed* present vs legacy) ---------
    Private Sub A27b_OfflineAdverseRoutingPlacedVsLegacy()
        Dim s As Integer = 0, f As Integer = 0
        ' Placed row: Placed mode uses the logged placed stop 99000, NOT the swing 95000.
        Dim rowP As New CsvRow With {.HasPlaced = True, .PlacedStopLong = 99000.0, .SwingStopLong = 95000.0}
        Dim advP = FailureRateMatrix.ResolveAdverseBarrier(rowP, True, 100000.0, 50.0, AdverseBarrierMode.Placed, s, f)
        ' Legacy row (no Placed*): falls back to the raw swing 95000.
        Dim rowL As New CsvRow With {.HasPlaced = False, .SwingStopLong = 95000.0}
        Dim advL = FailureRateMatrix.ResolveAdverseBarrier(rowL, True, 100000.0, 50.0, AdverseBarrierMode.Placed, s, f)
        ' Legacy MODE forces the raw swing even on a placed row (the D4 "before" walk).
        Dim advForce = FailureRateMatrix.ResolveAdverseBarrier(rowP, True, 100000.0, 50.0, AdverseBarrierMode.Legacy, s, f)
        ' No swing, no placed → ATR fallback (100000 − 1.2×50 = 99940).
        Dim rowN As New CsvRow With {.HasPlaced = False, .SwingStopLong = 0.0}
        Dim advN = FailureRateMatrix.ResolveAdverseBarrier(rowN, True, 100000.0, 50.0, AdverseBarrierMode.Placed, s, f)
        Check("A27b offline adverse routing (placed / legacy-fallback / forced-legacy / ATR)",
              advP = 99000.0 AndAlso advL = 95000.0 AndAlso advForce = 95000.0 AndAlso
              Math.Abs(advN - 99940.0) < 0.0001,
              String.Format(CultureInfo.InvariantCulture, "placed={0} legacyRow={1} forced={2} atr={3}",
                            advP, advL, advForce, advN))
    End Sub

    ' -- A27c: D4 before/after report renders both barrier bases -------------------
    Private Sub A27c_D4ReportRendersBothPopulations()
        Dim rep As New AnalysisReport()
        Dim pop As New PopulationReport With {
            .PopulationKey = "NY|1", .SessionName = "NY", .Resolution = 1,
            .BarrierLabel = "PLACED", .RowCount = 40}
        ' Placed (after) 60% failure vs legacy (before) 20% failure on the same cell.
        ' [placed-target migration] Cells are keyed (tier × window) only — no threshold.
        pop.FailureCells.Add(New FailureCellResult With {
            .VerdictTier = "STRONG_LONG", .WindowMin = 5,
            .SampleSize = 40, .Failures = 24, .FailureRate = 0.6})
        pop.LegacyFailureCells.Add(New FailureCellResult With {
            .VerdictTier = "STRONG_LONG", .WindowMin = 5,
            .SampleSize = 40, .Failures = 8, .FailureRate = 0.2})
        rep.Populations.Add(pop)

        Dim md As String = MarkdownReportWriter.BuildD4Section(rep)
        ' [E1 v55, 2026-07-21] Report render text flipped from FAILURE to SUCCESS
        ' rates. The same before/after cell that read "20% → 60% (+40%)" pre-flip
        ' now reads "80% → 40% (−40%)" — same rows, opposite side of the axis.
        Check("A27c D4 report renders before→after (both barrier bases, success orientation)",
              md.Contains("Placed-Geometry Migration") AndAlso md.Contains("STRONG_LONG") AndAlso
              md.Contains("→") AndAlso md.Contains("-40%") AndAlso md.Contains("n=40"),
              "expected the migration section with a before→after cell (-40% success delta, n=40); got:" & vbLf & md)
    End Sub

    ' -- A27d: eval-cache v4→v5 rotate-and-rebuild path ----------------------------
    Private Sub A27d_EvalCacheV5RotationRebuild()
        Dim tmp As String = Path.Combine(Path.GetTempPath(), "d6_eval_" & Guid.NewGuid().ToString("N") & ".csv")
        Dim bak As String = tmp & ".v4.bak"
        Try
            ' A v4 cache is pre-v5 → rotation fires; the original is moved to .v4.bak so
            ' the cold-start backfill rebuilds it fresh on placed barriers.
            File.WriteAllLines(tmp, New String() {
                "# schema=v4 (min-tradeable-move floor; exec resolution)",
                "Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome,TargetEverHit,ExecResolution",
                "2026-07-10T00:00:00.0000000Z,LONG,62000,62070,61936,PENDING,,1"})
            Dim wasPreV5 As Boolean = LivePerformanceTracker.IsPreV5Schema(tmp)
            LivePerformanceTracker.RotatePreV5Cache(tmp)
            Dim rotated As Boolean = (Not File.Exists(tmp)) AndAlso File.Exists(bak)

            ' A v5 cache is current → no rotation (rebuild does not re-fire on restart).
            File.WriteAllLines(tmp, New String() {
                "# schema=v5 (placed-level barriers; min-tradeable-move floor; exec resolution) floor_pct=0.0008",
                "Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome,TargetEverHit,ExecResolution",
                "2026-07-10T00:00:00.0000000Z,LONG,62000,62070,61936,PENDING,,1"})
            Dim v5Current As Boolean = Not LivePerformanceTracker.IsPreV5Schema(tmp)

            Check("A27d eval-cache v4→v5 rotate-and-rebuild",
                  wasPreV5 AndAlso rotated AndAlso v5Current,
                  String.Format("wasPreV5={0} rotated(orig-gone + .v4.bak)={1} v5Current={2}",
                                wasPreV5, rotated, v5Current))
        Finally
            Try : File.Delete(tmp) : Catch : End Try
            Try : File.Delete(bak) : Catch : End Try
        End Try
    End Sub

    ' =======================================================================
    ' A28 — aggressor-velocity TFI-modifier scoring wire-in (v52).
    ' docs/aggressor-velocity-proposal.md §4.5 + docs/aggr-vel-wirein-implementer-brief.md.
    ' Reuses the A8 RANGE_BOUND cascade (11 short / 4 long, TFI SELL PRESSURE, regimeMax 18
    ' with RegimeWeights/MTF/Pass2b off). r.ATR=50 + no swing short target ⇒ the placed
    ' fallback short target clears the min-move floor at NY (1.75×) / LONDON (2.0×), so the
    ' directional SHORT verdict stands and EffectiveShortScore isolates the modifier's ±1.
    ' =======================================================================

    ''' <summary>A8 cascade cfg with the aggressor-velocity modifier armed (scoring on).</summary>
    Private Function BuildBurstCfg(Optional upgradeBonus As Integer = 1,
                                   Optional contraPenalty As Integer = 1) As EngineSettings
        Dim cfg = BuildA8Cfg(fundingBoost:=0)   ' RangeBound cascade; Pass2b/2c/MTF/OFImom/Step3b off
        cfg.Indicators.AggressorVelocity.ScoringEnabled = True   ' explicit (POCO default is also True)
        cfg.Indicators.AggressorVelocity.UpgradeBonus  = upgradeBonus
        cfg.Indicators.AggressorVelocity.ContraPenalty = contraPenalty
        Return cfg
    End Function

    ''' <summary>A8 SELL-dominant indicators + a burst signal + a session UTC hour (the S2a key).</summary>
    Private Function BuildBurstIndicators(aggrVelSignal As String, utcHour As Integer) As IndicatorResults
        Dim r = BuildA8Indicators()   ' 11 short / 4 long; TFI SELL PRESSURE
        r.AggrVelSignal  = aggrVelSignal
        r.SessionUtcHour = utcHour     ' NY 13-23 has an explicit burst_ratio_threshold; res-3 does not
        Return r
    End Function

    ' -- A28a: upgrade (same-side) / soften (contra) / no-op (NORMAL) through Calculate() --
    Private Sub A28a_TfiBurstModifierUpgradeSoftenNoop()
        Dim cfg = BuildBurstCfg()   ' bonus 1 / penalty 1
        ' NORMAL tape → modifier no-op → the plain SELL cascade ss = 11.
        Dim vNorm = ScoringEngine.Calculate(BuildBurstIndicators("NORMAL", 15),
                                            PositionState.None, BuildA8Norms(), cfg)
        ' TFI SELL + BURST_SELL (same side) → +1[S] → ss = 12.
        Dim vUp = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 15),
                                          PositionState.None, BuildA8Norms(), cfg)
        ' TFI SELL + BURST_BUY (contra) → −1[S] soften → ss = 10.
        Dim vDown = ScoringEngine.Calculate(BuildBurstIndicators("BURST_BUY", 15),
                                            PositionState.None, BuildA8Norms(), cfg)

        Check("A28a TFI burst modifier (NORMAL ss=11 / same-side +1 ss=12 / contra −1 ss=10; all SHORT)",
              vNorm.EffectiveShortScore = 11 AndAlso vNorm.Verdict = "SHORT" AndAlso
              vUp.EffectiveShortScore = 12 AndAlso vUp.Verdict = "SHORT" AndAlso
              vDown.EffectiveShortScore = 10 AndAlso vDown.Verdict = "SHORT",
              String.Format("normal={0}/{1} up={2}/{3} down={4}/{5}",
                            vNorm.EffectiveShortScore, vNorm.Verdict,
                            vUp.EffectiveShortScore, vUp.Verdict,
                            vDown.EffectiveShortScore, vDown.Verdict))
    End Sub

    ' -- A28b: same-side upgrade caps at regimeMax (Math.Min site) ------------------
    Private Sub A28b_TfiBurstUpgradeCapsAtRegimeMax()
        Dim cfg = BuildBurstCfg(upgradeBonus:=20)   ' absurd bonus to force the cap
        ' Neutralise the post-TFI MicroCVD short vote so EffectiveShortScore isolates the
        ' capped Step-2 value: short = 10 at the TFI site, +20 → min(30, regimeMax 18) = 18.
        Dim r = BuildBurstIndicators("BURST_SELL", 15)
        r.MicroCVDSignal = "NEUTRAL"
        Dim v = ScoringEngine.Calculate(r, PositionState.None, BuildA8Norms(), cfg)
        Check("A28b burst upgrade caps at regimeMax (short 10 +20 → 18, not 30)",
              v.EffectiveShortScore = 18,
              String.Format("expected EffectiveShortScore=18 (capped at regimeMax), got {0}",
                            v.EffectiveShortScore))
    End Sub

    ' -- A28c: S2a session scoping + scoring_enabled:false byte-identical -----------
    Private Sub A28c_ScopingAndDisableInert()
        Dim cfg = BuildBurstCfg()   ' scoring on, bonus/penalty 1
        ' (1) S2a scoping: the modifier fires ONLY for a session carrying an EXPLICIT
        '     burst_ratio_threshold — absence is what keeps it inert.
        '     [v65 re-pin] D3 armed ASIA (sessions.ASIA.burst_ratio_threshold = 5.5,
        '     trader-ticked 2026-08-02), so ALL THREE shipped sessions are now armed and
        '     NO session is left that can serve as the un-armed exemplar. The un-armed arm
        '     is therefore CONSTRUCTED — ASIA's threshold cleared on a cfg copy — which is
        '     strictly better coverage: it pins the MECHANISM (absence ⇒ inert) rather than
        '     an incidental session, and it is the D3 rollback's only cover.
        '     [v60 history] LONDON was armed 2026-07-23, which is when the un-armed
        '     exemplar last moved (NY→ASIA). It has now run out of sessions to move to.
        Dim cfgUnarmed = BuildBurstCfg()
        cfgUnarmed.Indicators.AggressorVelocity.Sessions("ASIA").BurstRatioThreshold = Nothing
        Dim vAsiaUnarmed = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 3),
                                                   PositionState.None, BuildA8Norms(), cfgUnarmed)
        ' (2) all three shipped sessions ARMED at their own thresholds → +1 same-side.
        Dim vAsiaArmed = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 3),
                                                 PositionState.None, BuildA8Norms(), cfg)
        Dim vLondonArmed = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 10),
                                                   PositionState.None, BuildA8Norms(), cfg)
        ' (3) scoring_enabled:false → inert at NY too (the hot rollback).
        Dim cfgOff = BuildBurstCfg()
        cfgOff.Indicators.AggressorVelocity.ScoringEnabled = False
        Dim vOff = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 15),
                                           PositionState.None, BuildA8Norms(), cfgOff)
        ' Baseline NORMAL at NY (modifier eligible but tape calm) for the identity anchor.
        Dim vNorm = ScoringEngine.Calculate(BuildBurstIndicators("NORMAL", 15),
                                            PositionState.None, BuildA8Norms(), cfg)

        Check("A28c S2a scoping + disable inert (constructed un-armed ss=11; ASIA ARMED ss=12 [v65]; LONDON ss=12; off ss=11 == NORMAL)",
              vAsiaUnarmed.EffectiveShortScore = 11 AndAlso
              vAsiaArmed.EffectiveShortScore = 12 AndAlso
              vLondonArmed.EffectiveShortScore = 12 AndAlso
              vOff.EffectiveShortScore = 11 AndAlso
              vNorm.EffectiveShortScore = 11,
              String.Format("asiaUnarmed={0} asiaArmed={1} londonArmed={2} off={3} norm={4} (unarmed/off/norm=11, armed=12)",
                            vAsiaUnarmed.EffectiveShortScore, vAsiaArmed.EffectiveShortScore,
                            vLondonArmed.EffectiveShortScore, vOff.EffectiveShortScore,
                            vNorm.EffectiveShortScore))
    End Sub

    ' -- A28d: S5 rider — HC22 exact-match fences session_volume.enabled -----------
    Private Sub A28d_Hc22SessionVolumeEnabledFence()
        Dim s As String = "{""version"":52,""session_volume"":{""enabled"":true}," &
                          """indicators"":{""OBV"":{""trend_gate"":10}}}"
        ' The feature switch is exact-match rejected with HARD CONSTRAINT 22.
        Dim rEn = SettingsDiffApplier.Validate(OneDiff("session_volume.enabled", "true", "false"), s, 3)
        ' A sibling tunable (unrelated resolvable key) still passes — proves exact-match, no over-reach.
        Dim rSib = SettingsDiffApplier.Validate(OneDiff("indicators.OBV.trend_gate", "10", "12"), s, 3)
        Check("A28d HC22 fence (session_volume.enabled rejected; sibling OBV.trend_gate accepted)",
              Not rEn.IsValid AndAlso rEn.ErrorReason.Contains("HARD CONSTRAINT 22") AndAlso
              rSib.IsValid,
              String.Format("enValid={0} enReason='{1}' sibValid={2}",
                            rEn.IsValid, rEn.ErrorReason, rSib.IsValid))
    End Sub

    ' =======================================================================
    ' A29 — funding momentum TIME-ANCHORED window (v53).
    ' Spec: docs/funding-momentum-time-anchored-window-proposal.md §3/§7.
    ' POCO-default cfg = the shipped v53 values (W = 5 min, T = 2e-7).
    ' All rings are built at synthetic wall-clock offsets from T0; the shipped
    ' AppendFundingSample does the appending + eviction, so the fixtures pin the
    ' real path rather than a harness copy of it.
    ' =======================================================================

    Private ReadOnly FundT0 As Long = 1_760_000_000_000L      ' arbitrary fixed epoch — no wall-clock dependence

    Private Function FundCfg() As EngineSettings
        Return New EngineSettings()          ' W=5min, T=2e-7
    End Function

    Private Function MinsMs(m As Double) As Long
        Return CLng(m * 60_000.0)
    End Function

    ''' <summary>Ring built directly (no eviction) from (ageMinutes, rate) pairs, oldest first.</summary>
    Private Function FundRing(ParamArray samples As (AgeMin As Double, Rate As Double)()) _
            As List(Of (UtcMs As Long, Rate As Double))
        Dim ring As New List(Of (UtcMs As Long, Rate As Double))
        For Each s In samples
            ring.Add((FundT0 - MinsMs(s.AgeMin), s.Rate))
        Next
        Return ring
    End Function

    ' -- A29a: anchored classification — RISING / FALLING / FLAT ---------------
    ' Anchor is the 6-min-old sample (age ≥ W=5). Deltas are chosen either side of T=2e-7.
    Private Sub A29a_AnchoredClassification()
        Dim cfg = FundCfg()

        ' +5e-7 over the anchor → RISING.
        Dim up   = FundRing((6.0, 0.0000010), (0.0, 0.0000015))
        ' −5e-7 → FALLING.
        Dim down = FundRing((6.0, 0.0000015), (0.0, 0.0000010))
        ' +1e-7 < T → FLAT (moved, but not enough).
        Dim flat = FundRing((6.0, 0.0000010), (0.0, 0.0000011))

        Check("A29a anchored classification (+5e-7 RISING / −5e-7 FALLING / +1e-7 below T → FLAT)",
              IndicatorEngine.CalcFundingMomentum(up,   FundT0, cfg) = "RISING"  AndAlso
              IndicatorEngine.CalcFundingMomentum(down, FundT0, cfg) = "FALLING" AndAlso
              IndicatorEngine.CalcFundingMomentum(flat, FundT0, cfg) = "FLAT",
              String.Format("up={0} down={1} flat={2}",
                            IndicatorEngine.CalcFundingMomentum(up,   FundT0, cfg),
                            IndicatorEngine.CalcFundingMomentum(down, FundT0, cfg),
                            IndicatorEngine.CalcFundingMomentum(flat, FundT0, cfg)))

        ' Threshold is strict (> T, not ≥ T): exactly T → FLAT.
        Dim exact = FundRing((6.0, 0.0000010), (0.0, 0.0000010 + 0.0000002))
        Check("A29a threshold is strict (delta exactly T → FLAT)",
              IndicatorEngine.CalcFundingMomentum(exact, FundT0, cfg) = "FLAT",
              String.Format("got {0}", IndicatorEngine.CalcFundingMomentum(exact, FundT0, cfg)))
    End Sub

    ' -- A29b: the anchor is the NEWEST sample ≥ W old, not the oldest in the ring --
    ' THE tier-selection pin. Ring spans 20 min. Newest-≥5min anchor = the 5-min sample
    ' (rate 1.0e-6) → delta +1e-7 → FLAT. Oldest-in-ring (20-min, rate 5.0e-7) would give
    ' delta +6e-7 → RISING. Picking the oldest is what re-imports cadence dependence
    ' (the ring span grows with cadence), so this must read FLAT.
    Private Sub A29b_AnchorIsNewestAtLeastW()
        Dim cfg = FundCfg()
        Dim ring = FundRing((20.0, 0.0000005),      ' oldest — the wrong anchor
                            (12.0, 0.0000007),
                            (5.0,  0.0000010),      ' newest ≥ W — the right anchor
                            (2.0,  0.0000010),      ' younger than W — ineligible
                            (0.0,  0.0000011))      ' current
        Dim got = IndicatorEngine.CalcFundingMomentum(ring, FundT0, cfg)
        Check("A29b anchor = newest ≥W (5-min sample → FLAT; oldest-in-ring would read RISING)",
              got = "FLAT",
              String.Format("got {0} — RISING means the anchor walked to the oldest sample", got))

        ' Mirror: make the newest-≥W anchor the one that DOES clear T, and prove the
        ' younger-than-W samples are never selected (they'd read FLAT).
        Dim ring2 = FundRing((20.0, 0.0000010),
                             (5.5,  0.0000010),     ' newest ≥ W
                             (1.0,  0.0000016),     ' younger than W — ineligible
                             (0.0,  0.0000016))
        Check("A29b younger-than-W samples ineligible (anchor 5.5-min → RISING, not FLAT)",
              IndicatorEngine.CalcFundingMomentum(ring2, FundT0, cfg) = "RISING",
              String.Format("got {0}", IndicatorEngine.CalcFundingMomentum(ring2, FundT0, cfg)))
    End Sub

    ' -- A29c: cold start + post-gap → FLAT -----------------------------------
    Private Sub A29c_ColdStartAndPostGapFlat()
        Dim cfg = FundCfg()

        ' Cold start: ring exists but nothing is old enough to anchor, despite a big move.
        Dim cold = FundRing((2.0, 0.0000010), (1.0, 0.0000030), (0.0, 0.0000050))
        Check("A29c cold start (no sample ≥W old → FLAT even on a +4e-6 move)",
              IndicatorEngine.CalcFundingMomentum(cold, FundT0, cfg) = "FLAT",
              String.Format("got {0}", IndicatorEngine.CalcFundingMomentum(cold, FundT0, cfg)))

        ' Degenerate rings.
        Check("A29c empty / single-sample / Nothing rings → FLAT",
              IndicatorEngine.CalcFundingMomentum(FundRing(), FundT0, cfg) = "FLAT" AndAlso
              IndicatorEngine.CalcFundingMomentum(FundRing((9.0, 0.0000010)), FundT0, cfg) = "FLAT" AndAlso
              IndicatorEngine.CalcFundingMomentum(Nothing, FundT0, cfg) = "FLAT",
              "one of the degenerate rings did not return FLAT")

        ' Post-gap: the engine was down ~40 min. The first run back appends through the
        ' shipped path — eviction clears the whole pre-gap ring, so there is no anchor → FLAT.
        Dim gapped = FundRing((41.0, 0.0000010), (40.0, 0.0000010))
        IndicatorEngine.AppendFundingSample(gapped, FundT0, 0.0000090)
        Check("A29c post-gap (40-min outage → pre-gap ring evicted, no anchor → FLAT, count=1)",
              gapped.Count = 1 AndAlso
              IndicatorEngine.CalcFundingMomentum(gapped, FundT0, cfg) = "FLAT",
              String.Format("count={0} state={1}", gapped.Count,
                            IndicatorEngine.CalcFundingMomentum(gapped, FundT0, cfg)))
    End Sub

    ' -- A29d: 30-min eviction (and no count cap) ------------------------------
    Private Sub A29d_ThirtyMinuteEviction()
        ' Boundary: > maxAge evicts, exactly maxAge survives.
        Dim ring = FundRing((31.0, 0.0000010),      ' older than 30 min → evicted
                            (30.0, 0.0000011),      ' exactly 30 min → KEPT (strict >)
                            (10.0, 0.0000012))
        IndicatorEngine.AppendFundingSample(ring, FundT0, 0.0000013)
        Dim ages = ring.Select(Function(s) (FundT0 - s.UtcMs) / 60_000.0).ToList()
        Check("A29d eviction at 30 min (31-min dropped, exactly-30 kept, count 4→3)",
              ring.Count = 3 AndAlso
              Not ages.Any(Function(a) a > 30.0) AndAlso
              ages.Contains(30.0),
              String.Format("count={0} ages=[{1}]", ring.Count, String.Join(", ", ages)))

        ' No count cap: 120 appends at a 15s cadence (30 min of history) all survive on age
        ' alone. The retired FundingHistoryMax=10 would have left a 2.5-min span here — too
        ' short to ever anchor a W=5min window, i.e. permanently FLAT.
        Dim dense As New List(Of (UtcMs As Long, Rate As Double))
        For i As Integer = 0 To 119
            IndicatorEngine.AppendFundingSample(dense, FundT0 - MinsMs(29.75) + CLng(i * 15_000L), 0.0000010)
        Next
        Dim spanMin As Double = (dense(dense.Count - 1).UtcMs - dense(0).UtcMs) / 60_000.0
        Check("A29d no count cap (120 samples @15s all retained by age; span 29.75 min > W)",
              dense.Count = 120 AndAlso Math.Abs(spanMin - 29.75) < 0.01,
              String.Format("count={0} spanMin={1}", dense.Count, spanMin))
    End Sub

    ' -- A29e: CADENCE INVARIANCE — the fixture that pins the whole point -------
    ' One funding path, defined as a continuous function of wall-clock time, sampled at
    ' 30s and at 180s. At every instant BOTH cadences have a sample, the anchored state
    ' must agree. The retired count window could not do this: its span was 3 × cadence,
    ' so the 180s ring looked 6× further back than the 30s ring at the same instant.
    Private Function FundingPathAt(tMs As Long) As Double
        ' Piecewise: flat at 1.0e-6 for 15 min, then a steady crowding build of +2e-7/min.
        ' Rates chosen so every probe's delta sits far from T=2e-7 — the invariance claim is
        ' about STATE agreement, and a probe parked on the threshold would decide by float noise.
        Dim minsIn As Double = (tMs - FundT0) / 60_000.0
        If minsIn <= 15.0 Then Return 0.0000010
        Return 0.0000010 + (minsIn - 15.0) * 0.0000002
    End Function

    ''' <summary>Replays the path through the shipped append+classify path at a given cadence.</summary>
    Private Function ReplayCadence(cadenceSec As Integer, probesMs As List(Of Long)) As List(Of String)
        Dim cfg   = FundCfg()
        Dim ring  As New List(Of (UtcMs As Long, Rate As Double))
        Dim state As New Dictionary(Of Long, String)
        Dim stepMs As Long = cadenceSec * 1000L
        Dim endMs  As Long = probesMs.Max()

        Dim t As Long = FundT0
        While t <= endMs
            IndicatorEngine.AppendFundingSample(ring, t, FundingPathAt(t))
            If probesMs.Contains(t) Then
                state(t) = IndicatorEngine.CalcFundingMomentum(ring, t, cfg)
            End If
            t += stepMs
        End While
        Return probesMs.Select(Function(p) If(state.ContainsKey(p), state(p), "(no sample)")).ToList()
    End Function

    Private Sub A29e_CadenceInvariance()
        ' Probe instants, all on the 180s grid so BOTH cadences land a sample there:
        '  6 min → inside the flat stretch → FLAT
        ' 12 min → still flat (build starts at 15) → FLAT
        ' 21 min → mid-build → RISING
        ' 30 min → still building → RISING
        ' Note 21/30 are the interesting ones: the two cadences pick DIFFERENT anchors
        ' (16 vs 15 min, 25 vs 24 min) and so compute DIFFERENT deltas (1.0e-6 vs 1.2e-6) —
        ' and still agree on the state. That is the invariance claim: same states, not same
        ' deltas. The count window agreed on neither.
        Dim probes As New List(Of Long) From {
            FundT0 + MinsMs(6.0), FundT0 + MinsMs(12.0), FundT0 + MinsMs(21.0), FundT0 + MinsMs(30.0)}

        Dim fast = ReplayCadence(30, probes)      ' 30s cadence
        Dim slow = ReplayCadence(180, probes)     ' 180s on-close cadence (res-3)

        Dim agree As Boolean = fast.SequenceEqual(slow)
        Check("A29e CADENCE INVARIANCE (30s vs 180s over one funding path → identical states at identical instants)",
              agree AndAlso Not fast.Contains("(no sample)"),
              String.Format("30s=[{0}] 180s=[{1}]", String.Join(",", fast), String.Join(",", slow)))

        ' Pin the states themselves, so "invariant" can't pass by being uniformly wrong.
        Check("A29e states are the expected path reading (FLAT, FLAT, RISING, RISING)",
              fast.SequenceEqual(New String() {"FLAT", "FLAT", "RISING", "RISING"}),
              String.Format("got [{0}]", String.Join(",", fast)))

        ' The counter-example that motivated the spec: on this SAME path, a 3-change count
        ' window spans 90s at the 30s cadence but 9 min at the 180s cadence — at 21 min the
        ' 30s ring would read a +3e-7 delta and the 180s ring +1.8e-6, off the same path at
        ' the same instant. Documented rather than asserted: the count-window code is gone,
        ' so there is nothing left to run it against.
    End Sub

    ' =======================================================================
    ' A30 — offline What-If replay runner (docs/offline-whatif-replay-proposal.md §5)
    ' =======================================================================

    ' A directional RANGE_BOUND row with a swing target + HVN on each side, at NY hour 14.
    Private Function BuildWhatIfRow() As CsvRow
        Return New CsvRow() With {
            .Timestamp = New DateTime(2026, 7, 10, 14, 0, 0, DateTimeKind.Utc),
            .Price = 62000, .ATR = 40, .Regime = "RANGE_BOUND", .ExecResolution = 1,
            .MaxScore = 18, .LongScore = 12, .ShortScore = 4,
            .EffectiveLongScore = 12, .EffectiveShortScore = 4,
            .MtfGatePassLong = True, .MtfGatePassShort = True,
            .SwingTargetLong = 62070, .SwingStopLong = 61950,
            .SwingTargetShort = 61930, .SwingStopShort = 62050,
            .VpfrNearestHvnAbove = 62090, .VpfrNearestHvnBelow = 61910,
            .HasPlaced = True}
    End Function

    ' -- A30a: the adapter feeds ComputeSideLevels identically (the "no copies" guarantee) --
    ' The replayed Placed* on a row must equal a DIRECT SignalEmitter.ComputeSideLevels call
    ' on the same adapter output — proving the replay arbitration IS production, so the
    ' empty-overlay baseline column reproduces the standing failure matrix (§7).
    Private Sub A30a_AdapterReproducesComputeSideLevels()
        Dim cfg As New EngineSettings()   ' structural_levels enabled by default
        Dim row = BuildWhatIfRow()
        Dim r = WhatIfReplay.BuildIndicator(row)
        Dim directLong = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cfg, isLong:=True)
        Dim directShort = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cfg, isLong:=False)

        Dim run = WhatIfReplay.RunCell(New List(Of CsvRow) From {row}, cfg, 15, keepRows:=True)
        Dim rep = run.ReplayedRows(0)

        Check("A30a adapter placements ≡ ComputeSideLevels (no copies)",
              Math.Abs(rep.PlacedTargetLong - directLong.Target) < 0.001 AndAlso
              Math.Abs(rep.PlacedStopLong - directLong.StopPx) < 0.001 AndAlso
              Math.Abs(rep.PlacedTargetShort - directShort.Target) < 0.001 AndAlso
              Math.Abs(rep.PlacedStopShort - directShort.StopPx) < 0.001,
              String.Format("replay L(t{0:F1}/s{1:F1}) S(t{2:F1}/s{3:F1}) vs direct L(t{4:F1}/s{5:F1}) S(t{6:F1}/s{7:F1})",
                            rep.PlacedTargetLong, rep.PlacedStopLong, rep.PlacedTargetShort, rep.PlacedStopShort,
                            directLong.Target, directLong.StopPx, directShort.Target, directShort.StopPx))
    End Sub

    ' -- A30b: whitelist rejects off-list keys loudly + accepts a listed knob ----------------
    Private Sub A30b_WhitelistRejectAccept()
        Dim threw As Boolean = False
        Try
            WhatIfOverlay.Parse("{""indicators"":{""OFI"":{""book_depth"":7}}}")
        Catch ex As WhatIfOverlayError
            threw = True
        End Try
        Dim ok As WhatIfOverlay = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""stop_max_atr_mult"":2.0}}}")

        Check("A30b whitelist rejects off-list (indicators.OFI.book_depth) + accepts listed (stop_max_atr_mult)",
              threw AndAlso ok.Knobs.Count = 1 AndAlso
              ok.Knobs(0).Path = "scoring.structural_levels.stop_max_atr_mult" AndAlso
              Math.Abs(ok.Knobs(0).Values(0) - 2.0) < 0.000001,
              String.Format("threw={0}, knobs={1}, path={2}", threw, ok.Knobs.Count,
                            If(ok.Knobs.Count > 0, ok.Knobs(0).Path, "(none)")))
    End Sub

    ' -- A30c: threshold overlay shifts the directional population --------------------------
    ' regimeMax 18: live med_pct 0.53 → tMed=ceil(9.54)=10; weak_pct 0.35 → tWeak=ceil(6.3)=7.
    ' effLS=9 ⇒ WEAK LONG at live (9<10, 9≥7). Lower med_pct to 0.45 → tMed=ceil(8.1)=9 ⇒ 9≥9
    ' ⇒ LONG (MEDIUM, directional). The min-move gate does not fire (ATR fallback 40×1.75=70 >
    ' floor 0.0008×62000=49.6), so the flip is purely the threshold re-derivation.
    Private Sub A30c_ThresholdReplayPopulationShift()
        Dim row = BuildWhatIfRow()
        row.EffectiveLongScore = 9 : row.EffectiveShortScore = 2
        row.LongScore = 9 : row.ShortScore = 2
        row.SwingTargetLong = 0 : row.VpfrNearestHvnAbove = 0   ' force the ATR fallback target

        Dim dom As String = WhatIfReplay.DominantSide(row)
        Dim cfgBase As New EngineSettings()
        Dim cfgLow As New EngineSettings()
        cfgLow.Scoring.VerdictMedPct = 0.45

        Dim placedBase = SignalEmitter.ComputeSideLevels(New VerdictResult(), WhatIfReplay.BuildIndicator(row), cfgBase, True).Target
        Dim placedLow = SignalEmitter.ComputeSideLevels(New VerdictResult(), WhatIfReplay.BuildIndicator(row), cfgLow, True).Target
        Dim tB As String = "", tL As String = ""
        Dim vBase = WhatIfReplay.DeriveVerdict(row, cfgBase, dom, placedBase, tB)
        Dim vLow = WhatIfReplay.DeriveVerdict(row, cfgLow, dom, placedLow, tL)

        Check("A30c threshold overlay shifts population (WEAK LONG → LONG on med_pct 0.53→0.45)",
              dom = "LONG" AndAlso vBase = "WEAK LONG" AndAlso vLow = "LONG",
              String.Format("dom={0} base='{1}' low='{2}'", dom, vBase, vLow))
    End Sub

    ' -- A30d: the POC ladder tier is closed in replay (VPFRPoc/VPFRSignal are unlogged) -----
    Private Sub A30d_PocTierClosedInReplay()
        Dim cfg As New EngineSettings()
        Dim row = BuildWhatIfRow()
        row.SwingTargetLong = 0 : row.VpfrNearestHvnAbove = 0   ' no swing, no HVN → only ATR fallback left
        Dim r = WhatIfReplay.BuildIndicator(row)
        Dim lv = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cfg, isLong:=True)

        Check("A30d replay ladder = swing→HVN→fallback (POC closed: VPFR unlogged → adapter zeroes it)",
              lv.TargetReason = "FALLBACK_ATR" AndAlso r.VPFRPoc = 0 AndAlso r.VPFRSignal = "NEUTRAL",
              String.Format("targetReason={0} poc={1} sig={2}", lv.TargetReason, r.VPFRPoc, r.VPFRSignal))
    End Sub

    ' =======================================================================
    ' A31 — book absorption at structural levels (P4 #6 build sub-version, v54;
    ' [v61] geometry rescale onto ATR-fractions — docs/absorption-geometry-rescale-
    ' proposal.md §1). Docs: book-absorption-proposal.md §4/§9 + book-absorption-
    ' implementer-brief.md; rescale spec-back: docs/absorption-geometry-rescale-
    ' spec-back.md. Exercises the REAL LevelAbsorptionTracker + ClassifyAbsorption +
    ' ResolveAbsorptionMinAggrUsd. [v61] The three tick keys retired; SetLevels now
    ' carries resolved dollar distances (the carry-site pattern). Fixtures re-pin the
    ' PREVIOUS test book geometry byte-identical by passing 6/2/1 USD explicitly
    ' (== the retired v54 defaults 12t/4t/2t at TickSize $0.5). POCO defaults [v61]:
    ' proximity_atr_frac 0.30, band_atr_frac 0.10, break_tol_atr_frac 0.05, window
    ' 10s, absorb_ratio 1.5, depletion_floor_usd 5000, max_pull_frac 0.75, default
    ' min_aggr_usd 20000. The feed-side folds + the WS-only run-path gate stay OUT
    ' (live-socket/WinForms boundary, the A23 precedent) — REST-inertness holds by
    ' construction: nothing folds the tracker off the WS feed, and the cold tracker
    ' reads NONE/null (A31e/A31f pin that surface).
    ' =======================================================================

    ' Historic v54 test geometry (12t / 4t / 2t at TickSize $0.5) as absolute dollars —
    ' passed to SetLevels so A31a-g exercise the same ladder/book geometry as pre-v61.
    Private Const AbsProxUsd As Double = 6.0
    Private Const AbsBandUsd As Double = 2.0
    Private Const AbsBreakTolUsd As Double = 1.0

    ''' <summary>A 10-level ladder: asks ascending from askStart (step $0.5, default
    ''' size 5000, overridable per price), bids descending just below. Watched-level
    ''' fixtures put the level at 100010 with asks from 100008 (touch $2 inside the
    ''' $6 proximity gate; band [100010, 100012] visible).</summary>
    Private Function AbsBook(askStart As Double,
                             Optional askSizes As Dictionary(Of Double, Double) = Nothing) As OrderBookSnapshot
        Dim snap As New OrderBookSnapshot()
        For i As Integer = 0 To 9
            Dim p As Double = askStart + i * 0.5
            Dim sz As Double = 5000.0
            If askSizes IsNot Nothing AndAlso askSizes.ContainsKey(p) Then sz = askSizes(p)
            snap.Asks.Add((p, sz))
            snap.Bids.Add((askStart - 0.5 - i * 0.5, 5000.0))
        Next
        Return snap
    End Function

    ''' <summary>The standard pressed-level book: band [100010, 100012] holds
    ''' 100000+50000+30000+20000+10000 = 210000 USD. level10Size overrides the size
    ''' resting exactly at the level (the depletion knob).</summary>
    Private Function AbsBandBook(Optional level10Size As Double = 100000.0) As OrderBookSnapshot
        Return AbsBook(100008.0, New Dictionary(Of Double, Double) From {
            {100010.0, level10Size}, {100010.5, 50000.0}, {100011.0, 30000.0},
            {100011.5, 20000.0}, {100012.0, 10000.0}})
    End Function

    ' -- A31a: episode lifecycle — open / leave-proximity close / level re-map reset --
    Private Sub A31a_AbsorptionEpisodeLifecycle()
        Dim ab As New AbsorptionSettings()
        Dim tr As New LevelAbsorptionTracker()
        Dim t0 As Long = 1700000000000L
        tr.SetLevels(100010.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)

        tr.FoldBook(AbsBandBook(), t0, ab)
        Dim sOpen = tr.Snapshot(t0, ab)

        ' Price drops away — level leaves the visible ladder / proximity ⇒ episode closes.
        tr.FoldBook(AbsBook(100000.0), t0 + 100, ab)
        Dim sAway = tr.Snapshot(t0 + 100, ab)

        ' Re-approach ⇒ a fresh episode re-opens on the same level.
        tr.FoldBook(AbsBandBook(), t0 + 200, ab)
        Dim sBack = tr.Snapshot(t0 + 200, ab)

        Check("A31a lifecycle (approach→ACTIVE @100010 / leave-proximity→IDLE / re-approach→ACTIVE)",
              sOpen.Above.Active AndAlso sOpen.Above.LevelPrice = 100010.0 AndAlso
              Not sOpen.Below.Active AndAlso
              Not sAway.Above.Active AndAlso
              sBack.Above.Active AndAlso sBack.Above.LevelPrice = 100010.0,
              String.Format("open={0}@{1} away={2} back={3}@{4}",
                            sOpen.Above.Active, sOpen.Above.LevelPrice,
                            sAway.Above.Active, sBack.Above.Active, sBack.Above.LevelPrice))

        ' Mid-episode carried-level re-map (the §4.1 no-cross-level-bleed rule): the side
        ' re-binds to the NEW nearest level as a fresh episode, never carrying the old one.
        tr.SetLevels(100011.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)
        tr.FoldBook(AbsBandBook(), t0 + 300, ab)
        Dim sRemap = tr.Snapshot(t0 + 300, ab)
        Check("A31a level re-map mid-episode resets onto the new level (100010→100011, fresh episode)",
              sRemap.Above.Active AndAlso sRemap.Above.LevelPrice = 100011.0 AndAlso
              sRemap.Above.AggrUsd = 0.0,
              String.Format("active={0} level={1} aggr={2}",
                            sRemap.Above.Active, sRemap.Above.LevelPrice, sRemap.Above.AggrUsd))
    End Sub

    ' -- A31b: absorbRatio vs an analytic case (sitting defender fires) ------------
    ' sizeStart 210000; buys 90000 INTO the band (fills) + 120000 pressing below it;
    ' the band re-reads 150000 (net −60000: 90000 filled, 30000 provably reposted).
    ' aggr = 210000 ≥ min 150000; depletion = 60000 ≥ floor 25000 ⇒ ratio = 3.5 ≥ 3.0;
    ' conservation: ΔSize −60000 + fills 90000 = +30000 ⇒ postLB 30000, pullLB 0 ⇒
    ' pullFrac = 0 ≤ 0.5 ⇒ ABSORB_ABOVE — the wall is eating flow and being re-fed
    ' by a real (filled, not painted) defender.
    Private Sub A31b_AbsorbRatioAnalyticCase()
        Dim ab As New AbsorptionSettings()
        Dim tr As New LevelAbsorptionTracker()
        Dim t0 As Long = 1700000000000L
        tr.SetLevels(100010.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)

        tr.FoldBook(AbsBandBook(), t0, ab)
        tr.FoldTrade(100010.0, 90000.0, isBuy:=True, tsMs:=t0 + 50, cfg:=ab)    ' press + band fill
        tr.FoldTrade(100008.5, 120000.0, isBuy:=True, tsMs:=t0 + 60, cfg:=ab)   ' press only
        tr.FoldBook(AbsBandBook(level10Size:=40000.0), t0 + 100, ab)

        Dim s = tr.Snapshot(t0 + 150, ab)
        Dim read = IndicatorEngine.ClassifyAbsorption(s, ab.Defaults.MinAggrUsd, ab.AbsorbRatio, ab.MaxPullFrac)

        Check("A31b analytic case (aggr 210000 / depletion 60000 → ratio 3.5; pullFrac 0 → ABSORB_ABOVE)",
              s.Above.Active AndAlso
              Math.Abs(s.Above.AggrUsd - 210000.0) < 0.001 AndAlso
              Math.Abs(s.Above.AbsorbRatio - 3.5) < 0.0001 AndAlso
              Math.Abs(s.Above.PullFrac - 0.0) < 0.0001 AndAlso
              read.Signal = "ABSORB_ABOVE" AndAlso read.HasEpisode AndAlso
              read.LevelPrice = 100010.0 AndAlso Math.Abs(read.AbsorbRatio - 3.5) < 0.0001,
              String.Format(CultureInfo.InvariantCulture,
                            "active={0} aggr={1} ratio={2} pullFrac={3} signal={4}",
                            s.Above.Active, s.Above.AggrUsd, s.Above.AbsorbRatio,
                            s.Above.PullFrac, read.Signal))

        ' Threshold edges: an aggr below the session min, or a ratio below absorb_ratio,
        ' stays NONE (the same episode read classified against stricter thresholds).
        Dim readMin = IndicatorEngine.ClassifyAbsorption(s, 250000.0, ab.AbsorbRatio, ab.MaxPullFrac)
        Dim readRatio = IndicatorEngine.ClassifyAbsorption(s, ab.Defaults.MinAggrUsd, 4.0, ab.MaxPullFrac)
        Check("A31b threshold edges (aggr < min → NONE; ratio < absorb_ratio → NONE; episode numerics still surfaced)",
              readMin.Signal = "NONE" AndAlso readMin.HasEpisode AndAlso
              readRatio.Signal = "NONE" AndAlso readRatio.HasEpisode,
              String.Format("min={0}/{1} ratio={2}/{3}",
                            readMin.Signal, readMin.HasEpisode, readRatio.Signal, readRatio.HasEpisode))
    End Sub

    ' -- A31c: D8 conservation bounds + pullFrac veto (painted defense → NONE) -----
    ' The same pressing flow (210000, all below the band — no fills), but the band
    ' cycles 210000→150000→210000→150000 with ZERO fills: every drop is a provable
    ' pull (pullLB 120000), every recovery a provable post (postLB 60000).
    ' ratio = 3.5 would fire — but pullFrac = 2.0 > 0.5 ⇒ D8 veto ⇒ NONE, with the
    ' vetoed episode's pullFrac still surfaced (the CSV logs it — W4 evidence).
    Private Sub A31c_D8ConservationAndPullFracVeto()
        Dim ab As New AbsorptionSettings()
        Dim tr As New LevelAbsorptionTracker()
        Dim t0 As Long = 1700000000000L
        tr.SetLevels(100010.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)

        tr.FoldBook(AbsBandBook(), t0, ab)
        For i As Integer = 0 To 6
            tr.FoldTrade(100008.0, 30000.0, isBuy:=True, tsMs:=t0 + 10 + i, cfg:=ab)  ' 210000 pressing, no fills
        Next
        tr.FoldBook(AbsBandBook(level10Size:=40000.0), t0 + 100, ab)   ' −60000, no fills → pull
        tr.FoldBook(AbsBandBook(), t0 + 200, ab)                       ' +60000 repost → post
        tr.FoldBook(AbsBandBook(level10Size:=40000.0), t0 + 300, ab)   ' −60000 again → pull

        Dim s = tr.Snapshot(t0 + 350, ab)
        Dim read = IndicatorEngine.ClassifyAbsorption(s, ab.Defaults.MinAggrUsd, ab.AbsorbRatio, ab.MaxPullFrac)

        Check("A31c churn (pullLB 120000 / postLB 60000 → pullFrac 2.0 > 0.75 → D8 veto NONE; ratio 3.5 would have fired)",
              s.Above.Active AndAlso
              Math.Abs(s.Above.AbsorbRatio - 3.5) < 0.0001 AndAlso
              Math.Abs(s.Above.PullFrac - 2.0) < 0.0001 AndAlso
              read.Signal = "NONE" AndAlso read.HasEpisode AndAlso
              Math.Abs(read.PullFrac - 2.0) < 0.0001,
              String.Format(CultureInfo.InvariantCulture, "ratio={0} pullFrac={1} signal={2} hasEp={3}",
                            s.Above.AbsorbRatio, s.Above.PullFrac, read.Signal, read.HasEpisode))
    End Sub

    ' -- A31d: break-through → instant NONE; re-arm only after leaving proximity ---
    Private Sub A31d_BreakThroughAndReArm()
        Dim ab As New AbsorptionSettings()
        Dim tr As New LevelAbsorptionTracker()
        Dim t0 As Long = 1700000000000L
        tr.SetLevels(100010.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)

        tr.FoldBook(AbsBandBook(), t0, ab)
        Dim sOpen = tr.Snapshot(t0, ab)

        ' A print beyond level + break_tol ($1) ⇒ the level gave way — cleared INSTANTLY.
        tr.FoldTrade(100011.5, 10000.0, isBuy:=True, tsMs:=t0 + 50, cfg:=ab)
        Dim sBroken = tr.Snapshot(t0 + 50, ab)

        ' Still parked in proximity at the broken level ⇒ stays idle (no instant re-open).
        tr.FoldBook(AbsBandBook(), t0 + 100, ab)
        Dim sParked = tr.Snapshot(t0 + 100, ab)

        ' Leave proximity, then re-approach ⇒ the side re-arms with a fresh episode.
        tr.FoldBook(AbsBook(100000.0), t0 + 200, ab)
        tr.FoldBook(AbsBandBook(), t0 + 300, ab)
        Dim sRearm = tr.Snapshot(t0 + 300, ab)

        Check("A31d break-through (open → print 100011.5 > 100011 → instant NONE → parked idle → leave+re-approach re-arms)",
              sOpen.Above.Active AndAlso
              Not sBroken.Above.Active AndAlso
              Not sParked.Above.Active AndAlso
              sRearm.Above.Active AndAlso sRearm.Above.LevelPrice = 100010.0,
              String.Format("open={0} broken={1} parked={2} rearm={3}",
                            sOpen.Above.Active, sBroken.Above.Active,
                            sParked.Above.Active, sRearm.Above.Active))
    End Sub

    ' -- A31e: reset re-arm + cold/degenerate inputs never throw -------------------
    Private Sub A31e_ResetColdDegenerate()
        Dim ab As New AbsorptionSettings()
        Dim tr As New LevelAbsorptionTracker()
        Dim t0 As Long = 1700000000000L

        ' Cold tracker: both sides idle; classify → NONE with no episode.
        Dim sCold = tr.Snapshot(t0, ab)
        Dim readCold = IndicatorEngine.ClassifyAbsorption(sCold, ab.Defaults.MinAggrUsd, ab.AbsorbRatio, ab.MaxPullFrac)

        ' Degenerate inputs: Nothing / empty book, Nothing cfg, zero-priced trades —
        ' none may throw (the feed folds run on every frame).
        Dim threw As Boolean = False
        Try
            tr.FoldBook(Nothing, t0, ab)
            tr.FoldBook(New OrderBookSnapshot(), t0, ab)                    ' empty ladder
            tr.FoldBook(AbsBandBook(), t0, Nothing)                        ' Nothing cfg
            tr.FoldTrade(0.0, 1000.0, True, t0, ab)                        ' zero price
            tr.FoldTrade(100010.0, 0.0, True, t0, ab)                      ' zero amount
            tr.FoldTrade(100010.0, 1000.0, True, t0, Nothing)              ' Nothing cfg
            Dim ignored = tr.Snapshot(t0, Nothing)                         ' Nothing cfg read
        Catch
            threw = True
        End Try

        ' SeedAsync discipline: an ACTIVE episode + carried levels reset cold; after
        ' levels re-carry, the next approach re-arms a fresh episode.
        tr.SetLevels(100010.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)
        tr.FoldBook(AbsBandBook(), t0 + 100, ab)
        Dim sActive = tr.Snapshot(t0 + 100, ab)
        tr.Reset()
        Dim sReset = tr.Snapshot(t0 + 200, ab)
        tr.FoldBook(AbsBandBook(), t0 + 300, ab)      ' levels cleared by Reset ⇒ still idle
        Dim sNoLevels = tr.Snapshot(t0 + 300, ab)
        tr.SetLevels(100010.0, 0, 0, 0, AbsProxUsd, AbsBandUsd, AbsBreakTolUsd)
        tr.FoldBook(AbsBandBook(), t0 + 400, ab)
        Dim sRearmed = tr.Snapshot(t0 + 400, ab)

        Check("A31e cold NONE / degenerate never throws / reset clears + levels re-carry re-arms",
              Not sCold.Above.Active AndAlso Not sCold.Below.Active AndAlso
              readCold.Signal = "NONE" AndAlso Not readCold.HasEpisode AndAlso
              Not threw AndAlso
              sActive.Above.Active AndAlso Not sReset.Above.Active AndAlso
              Not sNoLevels.Above.Active AndAlso sRearmed.Above.Active,
              String.Format("cold={0}/{1} threw={2} active={3} reset={4} noLevels={5} rearmed={6}",
                            readCold.Signal, readCold.HasEpisode, threw, sActive.Above.Active,
                            sReset.Above.Active, sNoLevels.Above.Active, sRearmed.Above.Active))
    End Sub

    ' -- A31f: the 5 reserved v0.8 CSV columns populate — rotation-free ------------
    ' The header is UNCHANGED (reserved at the #5 rotation per D4), so EnsureLogFile
    ' must NOT rotate; an episode-active row carries values, a NONE row carries the
    ' empty numerics (the same shape a REST/fallback run logs — §4.3 null-never-guess).
    Private Sub A31f_CsvReservedColumnsPopulate()
        Dim logPath As String = AnalysisLogger.GetLogPath()
        Dim dir As String = Path.GetDirectoryName(logPath)
        Try
            If File.Exists(logPath) Then File.Delete(logPath)
            For Each bak In Directory.GetFiles(dir, "analysis_log.csv*.bak")
                File.Delete(bak)
            Next

            Dim cfg As New EngineSettings()
            Dim v As New VerdictResult With {.Verdict = "NO TRADE", .Confidence = "N/A"}

            Dim rEpisode As New IndicatorResults()
            rEpisode.CurrentPrice = 62000.0 : rEpisode.ATR = 40.0
            rEpisode.AbsorptionSignal = "ABSORB_ABOVE"
            rEpisode.AbsorptionLevel = 100010.0
            rEpisode.AbsorptionRatio = 3.5
            rEpisode.AbsorptionAggrUsd = 210000.0
            rEpisode.AbsorptionPullFrac = 0.0
            AnalysisLogger.LogRun(rEpisode, v, cfg)

            Dim rRest As New IndicatorResults()          ' the REST/no-episode shape
            rRest.CurrentPrice = 62000.0 : rRest.ATR = 40.0
            AnalysisLogger.LogRun(rRest, v, cfg)

            Dim lines() As String = File.ReadAllLines(logPath)
            Dim header() As String = lines(0).Split(","c)
            Dim idx As Integer = Array.IndexOf(header, "AbsorptionSignal")
            Dim row1() As String = lines(1).Split(","c)
            Dim row2() As String = lines(2).Split(","c)
            Dim noBak As Boolean = Directory.GetFiles(dir, "analysis_log.csv*.bak").Length = 0

            Check("A31f reserved columns populate (episode row values / NONE row empties; header unrotated, no .bak)",
                  lines.Length = 3 AndAlso idx >= 0 AndAlso noBak AndAlso
                  header(idx + 4) = "AbsorptionPullFrac" AndAlso
                  row1.Length = header.Length AndAlso row2.Length = header.Length AndAlso
                  row1(idx) = "ABSORB_ABOVE" AndAlso row1(idx + 1) = "100010.00" AndAlso
                  row1(idx + 2) = "3.50" AndAlso row1(idx + 3) = "210000" AndAlso
                  row1(idx + 4) = "0.0000" AndAlso
                  row2(idx) = "NONE" AndAlso row2(idx + 1) = "" AndAlso row2(idx + 2) = "" AndAlso
                  row2(idx + 3) = "" AndAlso row2(idx + 4) = "",
                  String.Format("lines={0} idx={1} noBak={2} row1=[{3}] row2=[{4}]",
                                lines.Length, idx, noBak,
                                If(idx >= 0 AndAlso row1.Length > idx + 4,
                                   String.Join("|", row1, idx, 5), "(short)"),
                                If(idx >= 0 AndAlso row2.Length > idx + 4,
                                   String.Join("|", row2, idx, 5), "(short)")))
        Finally
            Try : File.Delete(logPath) : Catch : End Try
        End Try
    End Sub

    ' -- A31g: session min_aggr_usd resolution + HC23 three-tier tweaker surface ---
    Private Sub A31g_SessionResolutionAndHc23Fences()
        ' Resolver: session override → shared default (the v40 nullable-override chain).
        Dim cfg As New EngineSettings()
        cfg.Indicators.Absorption.Sessions("NY").MinAggrUsd = 250000.0
        Dim mNy As Double = ExecutionResolution.ResolveAbsorptionMinAggrUsd(cfg, 15)
        Dim mLon As Double = ExecutionResolution.ResolveAbsorptionMinAggrUsd(cfg, 10)
        Dim mUnset As Double = ExecutionResolution.ResolveAbsorptionMinAggrUsd(cfg, -1)
        Check("A31g session min_aggr_usd (NY override 250000 / LONDON inherits v61 default 20000 / unstamped 20000)",
              mNy = 250000.0 AndAlso mLon = 20000.0 AndAlso mUnset = 20000.0,
              String.Format(CultureInfo.InvariantCulture, "ny={0} lon={1} unset={2}", mNy, mLon, mUnset))

        ' HC23 fences: the two switches exact-match rejected; default./sessions. prefixes
        ' rejected; the flat params stay proposable. [v61] JSON literal + proposable check
        ' updated to the new *_atr_frac key names (retired tick keys resolve-fail as
        ' UNRESOLVED, which C-6 rejects — not a HARD CONSTRAINT reject).
        Dim s As String = "{""version"":61,""indicators"":{""absorption"":{""enabled"":true," &
                          """scoring_enabled"":false,""proximity_atr_frac"":0.30,""band_atr_frac"":0.10," &
                          """window_sec"":10,""break_tol_atr_frac"":0.05,""absorb_ratio"":1.5," &
                          """depletion_floor_usd"":5000,""max_pull_frac"":0.75,""penalty"":1," &
                          """default"":{""min_aggr_usd"":20000},""sessions"":{""NY"":{}}}}}"
        Dim rEnabled = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.enabled", "true", "false"), s, 3)
        Dim rScoring = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.scoring_enabled", "false", "true"), s, 3)
        Dim rDefault = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.default.min_aggr_usd", "20000", "15000"), s, 3)
        Dim rSession = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.sessions.NY.min_aggr_usd", "20000", "35000"), s, 3)
        Dim rProx = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.proximity_atr_frac", "0.30", "0.28"), s, 3)
        Dim rRatio = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.absorb_ratio", "1.5", "1.7"), s, 3)
        Check("A31g HC23 fences (enabled/scoring_enabled + default./sessions. rejected; proximity_atr_frac + absorb_ratio tunable)",
              Not rEnabled.IsValid AndAlso rEnabled.ErrorReason.Contains("HARD CONSTRAINT 23") AndAlso
              Not rScoring.IsValid AndAlso rScoring.ErrorReason.Contains("HARD CONSTRAINT 23") AndAlso
              Not rDefault.IsValid AndAlso Not rSession.IsValid AndAlso
              rProx.IsValid AndAlso rRatio.IsValid,
              String.Format("enabled={0}'{1}' scoring={2} default={3} session={4} prox={5} ratio={6}",
                            rEnabled.IsValid, rEnabled.ErrorReason, rScoring.IsValid,
                            rDefault.IsValid, rSession.IsValid, rProx.IsValid, rRatio.IsValid))
    End Sub

    ' -- A31h: two-ATR scale invariance (V3 pin — docs/absorption-geometry-rescale-
    ' proposal.md V3). At ATR=44 with defaults (0.30/0.10/0.05) the carry site
    ' resolves prox=$13.2 / band=$4.4 / break-tol=$2.2. At ATR=88 (double) the
    ' resolution gives prox=$26.4 / band=$8.8 / break-tol=$4.4 — twice the dollar
    ' distances at twice the ATR. Verifies the arithmetic AND runs the tracker
    ' against a 2×-scaled book at each ATR to confirm identical classification —
    ' the "tracker internals stay absolute dollars, only the config→dollars
    ' conversion moves" invariant holds across scale.
    Private Sub A31h_TwoAtrScaleInvariance()
        Dim ab As New AbsorptionSettings()
        Const proxFrac As Double = 0.30
        Const bandFrac As Double = 0.10
        Const brkFrac As Double = 0.05
        Const atrLo As Double = 44.0
        Const atrHi As Double = 88.0
        Dim proxLo As Double = atrLo * proxFrac    ' 13.2
        Dim bandLo As Double = atrLo * bandFrac    ' 4.4
        Dim brkLo As Double  = atrLo * brkFrac     ' 2.2
        Dim proxHi As Double = atrHi * proxFrac    ' 26.4
        Dim bandHi As Double = atrHi * bandFrac    ' 8.8
        Dim brkHi As Double  = atrHi * brkFrac     ' 4.4

        Dim frac2xOk As Boolean =
            Math.Abs(proxLo - 13.2) < 1.0E-9 AndAlso Math.Abs(bandLo - 4.4) < 1.0E-9 AndAlso
            Math.Abs(brkLo - 2.2) < 1.0E-9 AndAlso
            Math.Abs(proxHi - 2.0 * proxLo) < 1.0E-9 AndAlso
            Math.Abs(bandHi - 2.0 * bandLo) < 1.0E-9 AndAlso
            Math.Abs(brkHi - 2.0 * brkLo) < 1.0E-9

        ' Book #1: level 100010, touch $2 inside prox 13.2 (best ask 100008), band
        ' [100010, 100010+bandLo≈100014.4]. Bands span ~4.4 USD; asks fill the
        ' visible ladder every $0.5 tick from 100008 upward, with size 5000 default.
        ' Sum inside band [100010, 100014.4]: prices 100010/100010.5/100011/100011.5/
        ' 100012/100012.5/100013/100013.5/100014 = 9 asks × 5000 = 45000 sizeStart.
        Dim tr1 As New LevelAbsorptionTracker()
        tr1.SetLevels(100010.0, 0, 0, 0, proxLo, bandLo, brkLo)
        tr1.FoldBook(AbsBook(100008.0), 1000L, ab)
        Dim s1Open = tr1.Snapshot(1000L, ab)

        ' Book #2: SAME logical geometry at 2×. Level 200020, touch 4 inside prox
        ' 26.4 (best ask 200016), band [200020, 200020+bandHi≈200028.8].
        Dim tr2 As New LevelAbsorptionTracker()
        tr2.SetLevels(200020.0, 0, 0, 0, proxHi, bandHi, brkHi)
        tr2.FoldBook(AbsBook(200016.0), 2000L, ab)
        Dim s2Open = tr2.Snapshot(2000L, ab)

        ' Both must open ACTIVE — the arithmetic-doubled resolution behaves identically.
        Dim scaleOk As Boolean =
            s1Open.Above.Active AndAlso s2Open.Above.Active AndAlso
            s1Open.Above.LevelPrice = 100010.0 AndAlso
            s2Open.Above.LevelPrice = 200020.0

        ' Break-through arithmetic scales too: a print at level+brk_lo+ε on tr1 breaks
        ' the level; the equivalent print at level+brk_hi+ε on tr2 also breaks. Anything
        ' inside break_tol stays active.
        tr1.FoldTrade(100010.0 + brkLo + 0.5, 5000.0, True, 1001L, ab)   ' > brk_lo → break
        tr2.FoldTrade(200020.0 + brkHi + 0.5, 5000.0, True, 2001L, ab)   ' > brk_hi → break
        Dim s1Brk = tr1.Snapshot(1001L, ab)
        Dim s2Brk = tr2.Snapshot(2001L, ab)
        Dim breakScales As Boolean = Not s1Brk.Above.Active AndAlso Not s2Brk.Above.Active

        Check("A31h two-ATR scale invariance (V3): ATR-fraction resolution doubles cleanly; tracker fires + breaks identically at 1× and 2×",
              frac2xOk AndAlso scaleOk AndAlso breakScales,
              String.Format(CultureInfo.InvariantCulture,
                            "frac2xOk={0} scaleOk={1} breakScales={2} proxLo={3} proxHi={4} s1Open={5}@{6} s2Open={7}@{8}",
                            frac2xOk, scaleOk, breakScales, proxLo, proxHi,
                            s1Open.Above.Active, s1Open.Above.LevelPrice,
                            s2Open.Above.Active, s2Open.Above.LevelPrice))
    End Sub

    ' =======================================================================
    ' A32 — offline matrix placed-target migration (docs/offline-matrix-placed-target
    ' -proposal.md). The favourable barrier joins the adverse on placed geometry; the
    ' per-tier ATR grid retires and the cell space collapses to (tier × window).
    ' =======================================================================

    ' -- A32a: favourable-barrier routing (placed target vs legacy formula) --------
    Private Sub A32a_PlacedFavourableRouting()
        Dim p As Integer = 0, l As Integer = 0
        Const Entry As Double = 100000.0
        Const FloorPct As Double = 0.0008      ' floor distance = 80
        Const TgtMult As Double = 2.0

        ' Placed row, LONG. The placed target sits 30 away — INSIDE the 80 floor — and must
        ' still be returned verbatim. Flooring it would push low-ATR rows back onto a shared
        ' floor price, which is exactly the collapse this migration removes.
        Dim rowP As New CsvRow With {.HasPlaced = True,
                                     .PlacedTargetLong = 100030.0, .PlacedTargetShort = 99940.0}
        Dim favP = FailureRateMatrix.ResolveFavourableBarrier(rowP, True, Entry, 50.0,
                                                              AdverseBarrierMode.Placed, TgtMult, FloorPct, p, l)
        ' Short side reads the short column.
        Dim favPs = FailureRateMatrix.ResolveFavourableBarrier(rowP, False, Entry, 50.0,
                                                               AdverseBarrierMode.Placed, TgtMult, FloorPct, p, l)
        ' Legacy row (pre-v0.8): engineTargetMult × ATR = 100, above the 80 floor.
        Dim rowL As New CsvRow With {.HasPlaced = False}
        Dim favL = FailureRateMatrix.ResolveFavourableBarrier(rowL, True, Entry, 50.0,
                                                              AdverseBarrierMode.Placed, TgtMult, FloorPct, p, l)
        ' Legacy row at low ATR: 2.0 × 20 = 40 < 80 → the floor binds (legacy side only).
        Dim favLf = FailureRateMatrix.ResolveFavourableBarrier(rowL, True, Entry, 20.0,
                                                               AdverseBarrierMode.Placed, TgtMult, FloorPct, p, l)
        ' Legacy MODE forces the legacy formula even on a placed row (the D4 "before" walk).
        Dim favForce = FailureRateMatrix.ResolveFavourableBarrier(rowP, True, Entry, 50.0,
                                                                  AdverseBarrierMode.Legacy, TgtMult, FloorPct, p, l)
        Check("A32a favourable routing (placed unfloored / short column / legacy / legacy-floored / forced-legacy)",
              favP = 100030.0 AndAlso favPs = 99940.0 AndAlso
              favL = 100100.0 AndAlso favLf = 100080.0 AndAlso favForce = 100100.0,
              String.Format(CultureInfo.InvariantCulture,
                            "placed={0} placedShort={1} legacy={2} legacyFloored={3} forced={4}",
                            favP, favPs, favL, favLf, favForce))

        ' Routing counters: 2 placed reads, 3 legacy reads across the five calls above.
        Check("A32a favourable routing counters (placed 2 / legacy 3)",
              p = 2 AndAlso l = 3,
              String.Format("placedTargetRows={0} legacyFavourableRows={1}", p, l))

        ' The gate distance the EXCLUDE test uses: exact on a placed row, approximated on legacy.
        Dim gP = FailureRateMatrix.GateTargetDistance(rowP, True, Entry, 50.0, AdverseBarrierMode.Placed, TgtMult)
        Dim gL = FailureRateMatrix.GateTargetDistance(rowL, True, Entry, 50.0, AdverseBarrierMode.Placed, TgtMult)
        Check("A32a gate distance (placed exact 30 / legacy approx 100)",
              Math.Abs(gP - 30.0) < 0.0001 AndAlso Math.Abs(gL - 100.0) < 0.0001,
              String.Format(CultureInfo.InvariantCulture, "placed={0} legacy={1}", gP, gL))
    End Sub

    ' -- A32b: tweaker picks on (window) alone; pre-migration history still parses ---
    Private Sub A32b_TweakerWindowOnlyPickAndHistoryParse()
        Dim tmp As String = Path.Combine(Path.GetTempPath(), "a32_state_" & Guid.NewGuid().ToString("N") & ".json")
        Try
            ' A state.json written BEFORE the migration: the picked-cell entry carries
            ' atr_threshold. It must survive Load with the value intact (no rotation).
            File.WriteAllText(tmp,
                "{""picked_cell_history"":[{""ts"":""2026-07-01 00:00:00"",""tier"":""STRONG_LONG""," &
                """window_min"":10,""atr_threshold"":0.5}]}")
            Dim loaded = TweakerState.Load(tmp)
            Dim oldParsed As Boolean = loaded.PickedCellHistory.Count = 1 AndAlso
                                       loaded.PickedCellHistory(0).AtrThreshold.HasValue AndAlso
                                       Math.Abs(loaded.PickedCellHistory(0).AtrThreshold.Value - 0.5) < 0.0001 AndAlso
                                       loaded.PickedCellHistory(0).WindowMin = 10

            ' A post-migration pick records (tier, window) only — no threshold key at all.
            loaded.PickedCellHistory.Add(New PickedCellEntry With {
                .Ts = "2026-07-21 00:00:00", .Tier = "STRONG_LONG", .WindowMin = 15})
            TweakerState.Save(tmp, loaded)
            Dim json As String = File.ReadAllText(tmp)

            ' The old entry keeps its key; the new one omits it. One occurrence total.
            Dim occurrences As Integer =
                json.Split(New String() {"atr_threshold"}, StringSplitOptions.None).Length - 1
            Dim reloaded = TweakerState.Load(tmp)
            Dim roundTrip As Boolean = reloaded.PickedCellHistory.Count = 2 AndAlso
                                       reloaded.PickedCellHistory(0).AtrThreshold.HasValue AndAlso
                                       Not reloaded.PickedCellHistory(1).AtrThreshold.HasValue AndAlso
                                       reloaded.PickedCellHistory(1).WindowMin = 15

            Check("A32b picked-cell history parse-tolerant (old row keeps threshold, new row omits it)",
                  oldParsed AndAlso occurrences = 1 AndAlso roundTrip,
                  String.Format("oldParsed={0} atr_threshold occurrences={1} roundTrip={2}" & vbLf & "{3}",
                                oldParsed, occurrences, roundTrip, json))
        Finally
            Try : File.Delete(tmp) : Catch : End Try
        End Try

        ' The pick space itself: every cell Compute emits is keyed (tier × window) with no
        ' duplicates, so a "recommended cell" can only differ from another by its window.
        Dim rows As New List(Of CsvRow)()
        Dim d1 As Integer, d2 As Integer, d3 As Integer, d4 As Integer, d5 As Integer, d6 As Integer
        Dim cells = FailureRateMatrix.Compute(rows, d1, d2, d3, d4, d5, d6)
        Dim keys As New HashSet(Of String)()
        Dim dupes As Boolean = False
        For Each c In cells
            If Not keys.Add(c.VerdictTier & "|" & c.WindowMin.ToString()) Then dupes = True
        Next
        Check("A32b tweaker pick-space is (tier × window) only — 12 cells, no duplicates",
              cells.Count = 12 AndAlso keys.Count = 12 AndAlso Not dupes,
              String.Format("cells={0} distinctKeys={1} dupes={2}", cells.Count, keys.Count, dupes))
    End Sub

    ' -- A32c: before/after grid renders ONE placed-geometry column -----------------
    Private Sub A32c_D4SingleColumnRender()
        Dim rep As New AnalysisReport()
        Dim pop As New PopulationReport With {
            .PopulationKey = "NY|1", .SessionName = "NY", .Resolution = 1,
            .BarrierLabel = "PLACED", .RowCount = 40}
        pop.FailureCells.Add(New FailureCellResult With {
            .VerdictTier = "STRONG_LONG", .WindowMin = 5,
            .SampleSize = 40, .Failures = 24, .FailureRate = 0.6})
        pop.LegacyFailureCells.Add(New FailureCellResult With {
            .VerdictTier = "STRONG_LONG", .WindowMin = 5,
            .SampleSize = 40, .Failures = 8, .FailureRate = 0.2})
        rep.Populations.Add(pop)

        Dim md As String = MarkdownReportWriter.BuildD4Section(rep)
        ' Header is Window + exactly ONE data column, with no ATR-multiple caption. (The
        ' section's prose still says "~1.6×ATR clamp", so the ATR check is scoped to the
        ' GRID, not the whole section — the retired axis was a column, not a sentence.)
        Const HdrLine As String = "| Window | Placed geometry (before→after Δ) |"
        Dim hdrOk As Boolean = md.Contains(HdrLine)
        Dim hdrHasNoThresholdCaption As Boolean = Not HdrLine.Contains("ATR")
        ' The 5m data row: "|    5m  | 20% → 60% (+40%) n=40 |".
        Dim dataRow As String = ""
        For Each line In md.Split(New String() {vbLf, vbCrLf}, StringSplitOptions.None)
            If line.TrimStart().StartsWith("|") AndAlso line.Contains("→") AndAlso line.Contains("n=40") Then
                dataRow = line.Trim()
            End If
        Next
        ' Window + one data column = 3 pipes. The retired 2-threshold grid rendered 4.
        Dim pipeCount As Integer = dataRow.Split("|"c).Length - 1
        Dim rowHasNoAtr As Boolean = Not dataRow.Contains("ATR")
        ' [E1 v55, 2026-07-21] Delta re-sign after the failure→success flip: the
        ' 20% failure → 60% failure cell now reads 80% success → 40% success (−40%).
        Check("A32c before/after grid renders a single placed-geometry column (success orientation)",
              hdrOk AndAlso hdrHasNoThresholdCaption AndAlso rowHasNoAtr AndAlso
              pipeCount = 3 AndAlso dataRow.Contains("-40%"),
              String.Format("hdrOk={0} hdrNoAtr={1} rowNoAtr={2} pipes={3} row='{4}'",
                            hdrOk, hdrHasNoThresholdCaption, rowHasNoAtr, pipeCount, dataRow))
    End Sub

    ' -- A32d: the 2026-07-17 floored-grid collapse cannot recur --------------------
    ' The bug: at ATR≈44 the whole per-tier grid ({0.5,0.8}) sat below the $51 min-move
    ' floor, so every column resolved to the SAME floored barrier and reported identical
    ' numbers by construction. On placed geometry the barrier is the row's own emitted
    ' target, so two low-ATR rows with different targets must produce different outcomes.
    Private Sub A32d_FlooredGridImpossibility()
        Const Entry As Double = 100000.0
        Const Atr As Double = 30.0             ' floor distance = 0.0008 × 100000 = 80 > 2.0 × 30
        Dim bars As New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = DateTime.UtcNow, .Open = Entry, .High = 100200.0,
                              .Low = 99950.0, .Close = 100150.0}}

        ' Near target (120 away) — reachable by the 100200 wick. Far target (400) — not.
        ' Both stops at 99900, below the 99950 low, so neither row stops out.
        Dim near As New CsvRow With {
            .Verdict = "STRONG LONG", .Price = Entry, .ATR = Atr, .HasPlaced = True,
            .PlacedTargetLong = 100120.0, .PlacedStopLong = 99900.0}
        Dim far As New CsvRow With {
            .Verdict = "STRONG LONG", .Price = Entry, .ATR = Atr, .HasPlaced = True,
            .PlacedTargetLong = 100400.0, .PlacedStopLong = 99900.0}
        For Each w In {5, 10, 15}
            near.ForwardBars(w) = bars
            far.ForwardBars(w) = bars
        Next

        Dim atrEx As Integer, structStop As Integer, atrFb As Integer
        Dim placedTgt As Integer, legacyFav As Integer, belowMin As Integer
        Dim cells = FailureRateMatrix.Compute(New List(Of CsvRow) From {near, far},
                                              atrEx, structStop, atrFb, placedTgt, legacyFav, belowMin)

        Dim cell = cells.Where(Function(c) c.VerdictTier = "STRONG_LONG" AndAlso c.WindowMin = 5).FirstOrDefault()
        Dim distinct As Boolean = cell IsNot Nothing AndAlso cell.SampleSize = 2 AndAlso
                                  cell.Successes = 1 AndAlso cell.Failures = 1 AndAlso
                                  cell.WindowExpiryFails = 1
        Check("A32d low-ATR rows produce DISTINCT outcomes (no floored-grid collapse)",
              distinct,
              If(cell Is Nothing, "no STRONG_LONG/5m cell",
                 String.Format("n={0} succ={1} fail={2} expiry={3} adverse={4}",
                               cell.SampleSize, cell.Successes, cell.Failures,
                               cell.WindowExpiryFails, cell.AdverseHitFails)))

        ' Both rows survive the min-move EXCLUDE on their EXACT placed distances (120 / 400
        ' ≥ 80). The retired approximation would have read 2.0 × 30 = 60 < 80 and dropped
        ' BOTH — silently deleting exactly the low-ATR rows this migration makes readable.
        Check("A32d exact gate keeps low-ATR placed rows the ATR approximation would have dropped",
              belowMin = 0 AndAlso placedTgt = 2 AndAlso legacyFav = 0,
              String.Format("belowMinMove={0} placedTargetRows={1} legacyFavourableRows={2}",
                            belowMin, placedTgt, legacyFav))
    End Sub

    ' =======================================================================
    ' A33 — F4 eval-cache no-data outcome (docs/eval-no-data-outcome-proposal.md).
    ' The empty-bar branch of LivePerformanceTracker.EvaluateEntry used to record
    ' WINDOW_EXPIRED (a failure); the offline matrix already excluded the same
    ' condition from its denominator. That asymmetry biased every live rate
    ' downward and invisibly (2026-07-03 NY = 22/22 fabricated expiries — the
    ' backfill day with no OHLC coverage for that slice).
    ' =======================================================================

    ' -- A33a: EvaluateEntry returns NO_DATA when the bar list is empty --------------
    Private Sub A33a_EmptyBarsProducesNoData()
        Dim entry As New LivePerformanceTracker.EvalCacheEntry With {
            .Timestamp     = New DateTime(2026, 7, 3, 20, 0, 0, DateTimeKind.Utc),
            .Verdict       = "STRONG LONG",
            .EntryPrice    = 100000.0,
            .FavBar        = 100200.0,
            .AdvBar        = 99900.0,
            .EvalOutcome   = "PENDING",
            .ExecResolution = 1}
        Dim nowUtc As DateTime = entry.Timestamp.AddMinutes(20)
        ' Empty lookup ⇒ empty bar list. Post-F4: NO_DATA (was WINDOW_EXPIRED).
        Dim empty As New Dictionary(Of DateTime, OhlcBar)()
        Dim evEmpty = LivePerformanceTracker.EvaluateEntry(entry, entry.Timestamp, nowUtc, empty)
        ' A single covering bar (CloseTime > ts+2 AND ≤ ts+15) with a target-hitting
        ' wick should still resolve normally — SUCCESS/targetHit=True.
        Dim covered As New Dictionary(Of DateTime, OhlcBar)()
        Dim bar As New OhlcBar With {.CloseTime = entry.Timestamp.AddMinutes(5),
                                     .Open = 100000.0, .High = 100300.0,
                                     .Low = 99950.0, .Close = 100250.0}
        covered(bar.CloseTime) = bar
        Dim evCovered = LivePerformanceTracker.EvaluateEntry(entry, entry.Timestamp, nowUtc, covered)
        ' Degenerate barrier keeps WINDOW_EXPIRED (proposal §1 — early-outs untouched).
        Dim degen As New LivePerformanceTracker.EvalCacheEntry With {
            .Timestamp     = entry.Timestamp,
            .Verdict       = "STRONG LONG",
            .EntryPrice    = 100000.0,
            .FavBar        = 0.0,          ' degenerate
            .AdvBar        = 99900.0,
            .EvalOutcome   = "PENDING",
            .ExecResolution = 1}
        Dim evDegen = LivePerformanceTracker.EvaluateEntry(degen, degen.Timestamp, nowUtc, covered)
        Check("A33a empty-bars ⇒ NO_DATA (covered still resolves; degenerate still WINDOW_EXPIRED)",
              evEmpty.outcome = "NO_DATA" AndAlso evEmpty.targetHit Is Nothing AndAlso
              evCovered.outcome = "SUCCESS" AndAlso evCovered.targetHit.HasValue AndAlso evCovered.targetHit.Value AndAlso
              evDegen.outcome = "WINDOW_EXPIRED",
              String.Format("empty={0}/{1} covered={2}/{3} degenerate={4}",
                            evEmpty.outcome, evEmpty.targetHit,
                            evCovered.outcome, evCovered.targetHit, evDegen.outcome))
    End Sub

    ' -- A33b: AggregateRange excludes NO_DATA from num+denom; TotalRange counts it --
    Private Sub A33b_AggregationExcludesNoData()
        Dim baseTs As DateTime = New DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc)
        Dim entries As New List(Of LivePerformanceTracker.EvalCacheEntry) From {
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(0), .Verdict = "STRONG LONG",
                .EvalOutcome = "SUCCESS", .TargetEverHit = True, .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(1), .Verdict = "STRONG LONG",
                .EvalOutcome = "WINDOW_EXPIRED", .TargetEverHit = False, .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(2), .Verdict = "STRONG LONG",
                .EvalOutcome = "NO_DATA", .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(3), .Verdict = "STRONG LONG",
                .EvalOutcome = "NO_DATA", .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(4), .Verdict = "STRONG LONG",
                .EvalOutcome = "PENDING", .ExecResolution = 1}}
        Dim agg = LivePerformanceTracker.AggregateRange(entries,
                                                        baseTs.AddMinutes(-1),
                                                        baseTs.AddMinutes(10), 0)
        ' 1 success, 1 failure, 2 NO_DATA excluded from num+denom, 1 PENDING excluded.
        ' TotalRange = 5 (every row in range, whatever the outcome).
        ' BarrierRatePct denominator = SuccessCount + FailureCount = 2 → 50%.
        Check("A33b NO_DATA excluded from success/failure counts; TotalRange counts it",
              agg.SuccessCount = 1 AndAlso agg.FailureCount = 1 AndAlso
              agg.TotalRange = 5 AndAlso Math.Abs(agg.BarrierRatePct - 50.0) < 0.0001,
              String.Format("succ={0} fail={1} total={2} rate={3}",
                            agg.SuccessCount, agg.FailureCount, agg.TotalRange, agg.BarrierRatePct))
    End Sub

    ' -- A33c: v5→v6 sweep reclassifies uncovered WE rows; preserves covered outcomes ---
    ' =======================================================================
    ' A34 — F2/F3/F12 eval display-semantics (docs/eval-display-semantics-proposal.md).
    ' E1 render flip (report + prompt) with internal truth + tweaker trigger unchanged;
    ' E2a WEAK exclusion at display time with a WEAK-only tooltip aggregate; E3a band
    ' display rendering (middle band as "MEDIUM LONG" / "MEDIUM SHORT") while every
    ' stored/wire string stays bare LONG / SHORT (the load-bearing invariant).
    ' =======================================================================

    ' -- A34a: MarkdownReportWriter renders SUCCESS + summary CSV column renamed ------
    Private Sub A34a_SuccessRenderFlipMatrixAndCsv()
        Dim rep As New AnalysisReport()
        Dim pop As New PopulationReport With {
            .PopulationKey = "NY|1", .SessionName = "NY", .Resolution = 1,
            .BarrierLabel = "PLACED", .RowCount = 40}
        ' Same cell as A32c: 24 failures / 40 = 60% failure ⇒ 40% success. Wilson
        ' CI [.44, .74] on the failure side ⇒ [.26, .56] on the success side.
        Dim ciLow As Double, ciHigh As Double
        FailureRateMatrix.WilsonCI(24, 40, ciLow, ciHigh)
        pop.FailureCells.Add(New FailureCellResult With {
            .VerdictTier = "STRONG_LONG", .WindowMin = 5,
            .SampleSize = 40, .Failures = 24, .FailureRate = 0.6,
            .CiLow = ciLow, .CiHigh = ciHigh})
        rep.Populations.Add(pop)

        Dim md As String = MarkdownReportWriter.BuildD4Section(rep)
        ' Section 3's blurb reads "success rates" and D4 renders 80%→40% for the
        ' before/after (before defaults to after when no legacy cell exists).
        Dim d4Flipped As Boolean = md.Contains("success") AndAlso md.Contains("40%")

        ' Full report — section 2 heading changed AND the ★◆ cell renders as success.
        Dim full As String = MarkdownReportWriter.BuildFullMarkdownForHarness(rep)
        Dim matrixHeading  As Boolean = full.Contains("## 2. Success-Rate Matrix")
        Dim legendPresent  As Boolean = full.Contains("MEDIUM_x") AndAlso full.Contains("STRONG_x") AndAlso
                                        full.Contains("WEAK") AndAlso full.Contains("excluded")
        Dim orientNote     As Boolean = full.Contains("Success orientation")

        ' Summary CSV column name changed AND value flipped.
        Dim csvPath As String = Path.Combine(Path.GetTempPath(), "a34a_" & Guid.NewGuid().ToString("N") & ".csv")
        Try
            MarkdownReportWriter.BuildSummaryCsvForHarness(rep, csvPath)
            Dim csv = File.ReadAllText(csvPath)
            Dim headerOk As Boolean = csv.Contains(",SuccessRate,") AndAlso Not csv.Contains(",FailureRate,")
            ' Success value = 1 - 0.6 = 0.4 = "0.400000".
            Dim valueOk As Boolean = csv.Contains(",0.400000,")

            Check("A34a report + CSV render as SUCCESS (matrix heading, D4 blurb, legend, CSV column+value)",
                  matrixHeading AndAlso d4Flipped AndAlso legendPresent AndAlso orientNote AndAlso
                  headerOk AndAlso valueOk,
                  String.Format("matrixHdr={0} d4Flip={1} legend={2} orient={3} csvHdr={4} csvVal={5}",
                                matrixHeading, d4Flipped, legendPresent, orientNote, headerOk, valueOk))
        Finally
            Try : File.Delete(csvPath) : Catch : End Try
        End Try
    End Sub

    ' -- A34b: the auto-tweaker's BELOW_THRESHOLD trigger comparison is UNCHANGED ------
    ' The load-bearing invariant of the display-only spec: the internal
    ' FailureCellResult.Failures/FailureRate stays as-is, so
    ' `aggregateRatePct < FailureRateThresholdPct` still decides BELOW_THRESHOLD the
    ' same way it did pre-flip. Same three cells that would have read below the
    ' threshold pre-flip must still read below the threshold post-flip.
    Private Sub A34b_TweakerTriggerUnchangedUnderSuccessRender()
        ' Simulate the tweaker's aggregation: sum Failures / sum SampleSize across
        ' recommended cells, compare to FailureRateThresholdPct=40.
        Const Threshold As Double = 40.0
        Dim cells As New List(Of FailureCellResult) From {
            New FailureCellResult With {.SampleSize = 50, .Failures = 10, .FailureRate = 0.2},
            New FailureCellResult With {.SampleSize = 30, .Failures =  9, .FailureRate = 0.3},
            New FailureCellResult With {.SampleSize = 20, .Failures =  6, .FailureRate = 0.3}}
        Dim totalN As Integer = 0, totalF As Integer = 0
        For Each c In cells
            totalN += c.SampleSize
            totalF += c.Failures
        Next
        Dim aggregateRatePct As Double = CDbl(totalF) / totalN * 100.0
        Dim isBelowThreshold As Boolean = aggregateRatePct < Threshold

        ' Under the render flip, the same three cells would DISPLAY as 80%/70%/70% success
        ' rates — visually way above 40. The trigger reads FAILURE and stays BELOW_THRESHOLD.
        Check("A34b tweaker trigger stays failure-oriented under success render (25% < 40 ⇒ BELOW_THRESHOLD)",
              isBelowThreshold AndAlso Math.Abs(aggregateRatePct - 25.0) < 0.0001,
              String.Format("aggregateRatePct={0:F2}%, threshold={1:F2}%, below={2}",
                            aggregateRatePct, Threshold, isBelowThreshold))
    End Sub

    ' -- A34c: WEAK excluded from Success/Failure counts; WeakBarrierRatePct tooltip ---
    ' Storage is UNCHANGED — the entries retain their WEAK verdict strings. The
    ' exclusion happens at display time in AggregateRange keyed off the verdict band.
    Private Sub A34c_WeakExcludedFromStripAggregate()
        Dim baseTs As DateTime = New DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc)
        ' Rows in the list (indices 0-4):
        '   0: STRONG LONG SUCCESS      — counted in SuccessCount
        '   1: LONG (medium) FAILURE    — counted in FailureCount
        '   2: WEAK LONG SUCCESS        — counted in WeakSuccessCount only (E2a)
        '   3: WEAK SHORT SUCCESS       — counted in WeakSuccessCount only
        '   4: WEAK LONG ADVERSE_HIT    — counted in WeakFailureCount only
        ' Expected: Success/Failure = 1/1 → 50%; WeakBarrierRatePct = 2/3 → 66.7%.
        Dim entries As New List(Of LivePerformanceTracker.EvalCacheEntry) From {
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(0), .Verdict = "STRONG LONG",
                .EvalOutcome = "SUCCESS", .TargetEverHit = True, .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(1), .Verdict = "LONG",
                .EvalOutcome = "WINDOW_EXPIRED", .TargetEverHit = False, .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(2), .Verdict = "WEAK LONG",
                .EvalOutcome = "SUCCESS", .TargetEverHit = True, .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(3), .Verdict = "WEAK SHORT",
                .EvalOutcome = "SUCCESS", .TargetEverHit = True, .ExecResolution = 1},
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs.AddMinutes(4), .Verdict = "WEAK LONG",
                .EvalOutcome = "ADVERSE_HIT", .TargetEverHit = False, .ExecResolution = 1}}
        Dim agg = LivePerformanceTracker.AggregateRange(entries,
                                                        baseTs.AddMinutes(-1),
                                                        baseTs.AddMinutes(10), 0)
        ' Strip rate = STRONG + MEDIUM only: 1/1 → 50%. WEAK aggregate = 2/3 → 66.7%.
        ' TotalRange still counts every row (tooltip's "predictions evaluated" line).
        Check("A34c WEAK excluded from Success/Failure counts; WeakBarrierRatePct tracks the WEAK aggregate",
              agg.SuccessCount = 1 AndAlso agg.FailureCount = 1 AndAlso
              agg.WeakSuccessCount = 2 AndAlso agg.WeakFailureCount = 1 AndAlso
              agg.TotalRange = 5 AndAlso
              Math.Abs(agg.BarrierRatePct - 50.0) < 0.0001 AndAlso
              Math.Abs(agg.WeakBarrierRatePct - (200.0 / 3.0)) < 0.01,
              String.Format("succ={0} fail={1} weakSucc={2} weakFail={3} total={4} rate={5:F2} weakRate={6:F2}",
                            agg.SuccessCount, agg.FailureCount,
                            agg.WeakSuccessCount, agg.WeakFailureCount,
                            agg.TotalRange, agg.BarrierRatePct, agg.WeakBarrierRatePct))
    End Sub

    ' -- A34d: middle band renders "MEDIUM LONG" / "MEDIUM SHORT" via the display helper ---
    ' The helper is the ONE seam both render surfaces (snapshot + card) route through.
    ' STRONG/WEAK/NO TRADE unchanged; stored/wire strings are never touched by rendering.
    Private Sub A34d_BandDisplayHelperPrefix()
        Dim mLong  As String = VerdictResult.FormatVerdictForDisplay("LONG")
        Dim mShort As String = VerdictResult.FormatVerdictForDisplay("SHORT")
        Dim strong As String = VerdictResult.FormatVerdictForDisplay("STRONG LONG")
        Dim weakS  As String = VerdictResult.FormatVerdictForDisplay("WEAK SHORT")
        Dim noTrd  As String = VerdictResult.FormatVerdictForDisplay("NO TRADE")
        Dim noTrdW As String = VerdictResult.FormatVerdictForDisplay("NO TRADE [WEAK LONG]")
        Dim empty  As String = VerdictResult.FormatVerdictForDisplay("")

        Check("A34d display helper renders MEDIUM prefix on bare LONG/SHORT; leaves STRONG/WEAK/NO TRADE untouched",
              mLong = "MEDIUM LONG" AndAlso mShort = "MEDIUM SHORT" AndAlso
              strong = "STRONG LONG" AndAlso weakS = "WEAK SHORT" AndAlso
              noTrd = "NO TRADE" AndAlso noTrdW = "NO TRADE [WEAK LONG]" AndAlso
              empty = "",
              String.Format("mLong='{0}' mShort='{1}' strong='{2}' weakS='{3}' noTrd='{4}' noTrdW='{5}' empty='{6}'",
                            mLong, mShort, strong, weakS, noTrd, noTrdW, empty))
    End Sub

    ' -- A34e: STORED-FORM PINS — the revision's load-bearing invariant --------------
    ' The card + snapshot render MEDIUM LONG / MEDIUM SHORT, but every OTHER surface
    ' (CSV Verdict column, payload verdict field, eval cache Verdict) still carries
    ' bare LONG / SHORT for the middle band. This fixture asserts the NON-change: a
    ' VerdictResult built with .Verdict = "LONG" flows through the payload builder
    ' unchanged (SignalEmitter reads v.Verdict verbatim into the payload's `verdict`
    ' key), and the eval-cache classifier IsWeakVerdict / IsLongVerdict still route
    ' bare LONG correctly (LivePerformanceTracker doesn't second-guess the string).
    ' Regressing this in either direction breaks the frozen bridge contract.
    Private Sub A34e_StoredFormPinsUnchanged()
        Dim v As New VerdictResult With {
            .Verdict = "LONG",
            .LongScore = 10, .ShortScore = 2, .MaxScore = 19,
            .EffectiveLongScore = 10, .EffectiveShortScore = 2,
            .Confidence = "MEDIUM", .VerdictContext = "CONFIRMED",
            .HoldStatus = "N/A -- no open position", .Timestamp = DateTime.UtcNow,
            .MTFGateReason = "MTF PASS [LONG] test"}

        ' Payload — the bridge reads v.Verdict verbatim into the "verdict" key (§4
        ' R1); no display transform touches v.Verdict along the way. DeriveDirection
        ' still routes "LONG" → "LONG" (side only, unchanged) — the two payload keys
        ' that consumers gate on. A22 fixtures pin the full serialisation shape
        ' separately; here we assert only the string identity in memory.
        Dim directionFromStored As String = SignalEmitter.DeriveDirection(v.Verdict)
        Dim storedVerdictBare  As Boolean = v.Verdict = "LONG"

        ' Eval cache: an entry stored as "LONG" carries "LONG" — IsLongVerdict TRUE,
        ' IsWeakVerdict FALSE. AggregateRange puts it in Success/Failure (not Weak*).
        Dim baseTs As DateTime = New DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc)
        Dim entries As New List(Of LivePerformanceTracker.EvalCacheEntry) From {
            New LivePerformanceTracker.EvalCacheEntry With {
                .Timestamp = baseTs, .Verdict = "LONG",
                .EvalOutcome = "SUCCESS", .TargetEverHit = True, .ExecResolution = 1}}
        Dim agg = LivePerformanceTracker.AggregateRange(entries,
                                                        baseTs.AddMinutes(-1),
                                                        baseTs.AddMinutes(10), 0)
        Dim routedToStrongMedium As Boolean = agg.SuccessCount = 1 AndAlso agg.WeakSuccessCount = 0

        ' Display side: the same stored "LONG" ⇒ "MEDIUM LONG" (surfaces routes through
        ' the helper — divergence lives HERE, not on the storage side).
        Dim displayed As String = VerdictResult.FormatVerdictForDisplay(v.Verdict)

        Check("A34e stored-form pins — CSV/payload/eval-cache carry bare LONG; display renders MEDIUM LONG",
              storedVerdictBare AndAlso directionFromStored = "LONG" AndAlso
              routedToStrongMedium AndAlso displayed = "MEDIUM LONG",
              String.Format("stored='{0}' direction='{1}' routedSM={2} displayed='{3}'",
                            v.Verdict, directionFromStored, routedToStrongMedium, displayed))
    End Sub

    Private Sub A33c_V6SweepReclassifiesUncoveredPreservesCovered()
        Dim tmp As String = Path.Combine(Path.GetTempPath(), "f4_eval_" & Guid.NewGuid().ToString("N") & ".csv")
        Try
            ' A v5 file (has "placed-level" marker but LACKS "no-data outcome") is pre-v6.
            File.WriteAllLines(tmp, New String() {
                "# schema=v5 (placed-level barriers; min-tradeable-move floor; exec resolution) floor_pct=0.0008",
                "Timestamp,Verdict,EntryPrice,FavBar,AdvBar,EvalOutcome,TargetEverHit,ExecResolution"})
            Dim wasPreV6 As Boolean = LivePerformanceTracker.IsPreV6Schema(tmp)

            ' Two WINDOW_EXPIRED rows with valid barriers, both LONG. The "uncovered" row's
            ' timestamp sits in an era with no OHLC bars in the lookup → sweep re-stamps as
            ' NO_DATA. The "covered" row has bars in T+3..T+15 that touch the favourable
            ' barrier → sweep re-stamps as SUCCESS. A third row is degenerate (FavBar=0) and
            ' should be left as WINDOW_EXPIRED (proposal §1 — early-outs untouched).
            Dim tsUncov As DateTime = New DateTime(2026, 7, 3, 20, 0, 0, DateTimeKind.Utc)
            Dim tsCov   As DateTime = New DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc)
            Dim tsDegen As DateTime = New DateTime(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc)
            Dim entries As New List(Of LivePerformanceTracker.EvalCacheEntry) From {
                New LivePerformanceTracker.EvalCacheEntry With {
                    .Timestamp = tsUncov, .Verdict = "STRONG LONG", .EntryPrice = 100000.0,
                    .FavBar = 100200.0, .AdvBar = 99900.0,
                    .EvalOutcome = "WINDOW_EXPIRED", .TargetEverHit = False, .ExecResolution = 1},
                New LivePerformanceTracker.EvalCacheEntry With {
                    .Timestamp = tsCov, .Verdict = "STRONG LONG", .EntryPrice = 100000.0,
                    .FavBar = 100200.0, .AdvBar = 99900.0,
                    .EvalOutcome = "WINDOW_EXPIRED", .TargetEverHit = False, .ExecResolution = 1},
                New LivePerformanceTracker.EvalCacheEntry With {
                    .Timestamp = tsDegen, .Verdict = "STRONG LONG", .EntryPrice = 100000.0,
                    .FavBar = 0.0, .AdvBar = 99900.0,
                    .EvalOutcome = "WINDOW_EXPIRED", .TargetEverHit = Nothing, .ExecResolution = 1}}
            Dim lookup As New Dictionary(Of DateTime, OhlcBar)()
            ' Only the "covered" window gets bars (CloseTime in ts+2..ts+15 exclusive/inclusive).
            Dim bar As New OhlcBar With {.CloseTime = tsCov.AddMinutes(5),
                                         .Open = 100000.0, .High = 100300.0,
                                         .Low = 99950.0, .Close = 100250.0}
            lookup(bar.CloseTime) = bar

            LivePerformanceTracker.ReclassifyWindowExpiredForNoData(entries, lookup, tsCov.AddMinutes(30))

            Check("A33c v5→v6 sweep: uncovered → NO_DATA / covered → SUCCESS / degenerate preserved",
                  wasPreV6 AndAlso
                  entries(0).EvalOutcome = "NO_DATA" AndAlso Not entries(0).TargetEverHit.HasValue AndAlso
                  entries(1).EvalOutcome = "SUCCESS" AndAlso entries(1).TargetEverHit.HasValue AndAlso entries(1).TargetEverHit.Value AndAlso
                  entries(2).EvalOutcome = "WINDOW_EXPIRED",
                  String.Format("preV6={0} uncov={1}/{2} cov={3}/{4} degen={5}",
                                wasPreV6, entries(0).EvalOutcome, entries(0).TargetEverHit,
                                entries(1).EvalOutcome, entries(1).TargetEverHit, entries(2).EvalOutcome))
        Finally
            Try : File.Delete(tmp) : Catch : End Try
        End Try
    End Sub

    ' ======================================================================
    ' A35 — E5 band-ladder diagnostic section.
    ' Spec: eval-display-semantics-proposal.md §3c (TICKED 2026-07-21);
    ' spec-back: eval-display-semantics-spec-back.md E5 addendum.
    ' ======================================================================

    ' -- A35a: report §9 renders all three bands with correct counts/success%/CI ---
    ' Build a PopulationReport whose BandLadder carries pre-computed rows for STRONG /
    ' MEDIUM / WEAK, plus a pooled ladder on the report, then assert the full markdown
    ' render (via BuildFullMarkdownForHarness) carries the section heading + the three
    ' band rows + the diagnostic + F1 footnote + WEAK-never-trades disclosure.
    Private Sub A35a_BandLadderRendersAllThreeBands()
        Dim rep As New AnalysisReport()
        Dim pop As New PopulationReport With {
            .PopulationKey = "NY|1", .SessionName = "NY", .Resolution = 1,
            .BarrierLabel = "PLACED", .RowCount = 60}

        ' STRONG 24/40 = 60% failure ⇒ 40% success (Wilson).
        Dim sCiLow As Double, sCiHigh As Double
        FailureRateMatrix.WilsonCI(24, 40, sCiLow, sCiHigh)
        ' MEDIUM 3/10 = 30% failure ⇒ 70% success.
        Dim mCiLow As Double, mCiHigh As Double
        FailureRateMatrix.WilsonCI(3, 10, mCiLow, mCiHigh)
        ' WEAK 5/10 = 50% failure ⇒ 50% success.
        Dim wCiLow As Double, wCiHigh As Double
        FailureRateMatrix.WilsonCI(5, 10, wCiLow, wCiHigh)

        pop.BandLadder = New List(Of BandLadderRow) From {
            New BandLadderRow With {.Band = "STRONG", .SampleSize = 40, .Failures = 24,
                                    .FailureRate = 0.6, .CiLow = sCiLow, .CiHigh = sCiHigh},
            New BandLadderRow With {.Band = "MEDIUM", .SampleSize = 10, .Failures = 3,
                                    .FailureRate = 0.3, .CiLow = mCiLow, .CiHigh = mCiHigh},
            New BandLadderRow With {.Band = "WEAK",   .SampleSize = 10, .Failures = 5,
                                    .FailureRate = 0.5, .CiLow = wCiLow, .CiHigh = wCiHigh}}
        rep.Populations.Add(pop)

        ' Pooled ladder — an identical shape so the assert is single-target.
        rep.PooledBandLadder = New List(Of BandLadderRow) From {
            New BandLadderRow With {.Band = "STRONG", .SampleSize = 40, .Failures = 24,
                                    .FailureRate = 0.6, .CiLow = sCiLow, .CiHigh = sCiHigh},
            New BandLadderRow With {.Band = "MEDIUM", .SampleSize = 10, .Failures = 3,
                                    .FailureRate = 0.3, .CiLow = mCiLow, .CiHigh = mCiHigh},
            New BandLadderRow With {.Band = "WEAK",   .SampleSize = 10, .Failures = 5,
                                    .FailureRate = 0.5, .CiLow = wCiLow, .CiHigh = wCiHigh}}

        Dim full As String = MarkdownReportWriter.BuildFullMarkdownForHarness(rep)

        Dim heading      As Boolean = full.Contains("## 9. Band ladder (diagnostic — includes untraded WEAK)")
        Dim strongRow    As Boolean = full.Contains("| STRONG |") AndAlso full.Contains("40.0%")
        Dim mediumRow    As Boolean = full.Contains("| MEDIUM |") AndAlso full.Contains("70.0%")
        Dim weakRow      As Boolean = full.Contains("| WEAK   |") AndAlso full.Contains("50.0%")
        Dim diagnostic   As Boolean = full.Contains("Diagnostic only") AndAlso full.Contains("WEAK never trades")
        Dim f1Footnote   As Boolean = full.Contains("§8 F1") OrElse full.Contains("F1")
        Dim pooledBlock  As Boolean = full.Contains("POOLED")
        Dim popHorizon   As Boolean = full.Contains("horizon 15m")  ' res-1 → 15m
        ' Global Diagnostics renumbered §9 → §10 by the ladder insertion.
        Dim diagRenumber As Boolean = full.Contains("## 10. Global Diagnostics") AndAlso
                                      full.Contains("### 10.1") AndAlso Not full.Contains("## 9. Global Diagnostics")

        Check("A35a §9 band-ladder renders STRONG/MEDIUM/WEAK success/n/CI + pooled + diagnostic + F1 footnote",
              heading AndAlso strongRow AndAlso mediumRow AndAlso weakRow AndAlso
              diagnostic AndAlso f1Footnote AndAlso pooledBlock AndAlso popHorizon AndAlso
              diagRenumber,
              String.Format("head={0} s={1} m={2} w={3} diag={4} f1={5} pooled={6} horizon={7} renum={8}",
                            heading, strongRow, mediumRow, weakRow, diagnostic,
                            f1Footnote, pooledBlock, popHorizon, diagRenumber))
    End Sub

    ' -- A35b: WEAK classifier excludes NO TRADE strings (WEAK ≠ NO TRADE — §3c) ---
    ' The load-bearing distinction: FailureRateMatrix.CanonicalTier maps WEAK LONG AND
    ' NO TRADE [WEAK LONG] alike to "" (both excluded from the tradeable matrix). The
    ' new BandLadder.CanonicalBand MUST distinguish them: WEAK LONG / WEAK SHORT → "WEAK",
    ' every NO TRADE variant → "" (refused signals never count as WEAK, no matter how
    ' the lean bracket is worded).
    Private Sub A35b_BandClassifierExcludesNoTradeAndLeanForms()
        ' Positive side: the four in-band tier strings collapse to the three ladder rungs.
        Dim strongL As String = BandLadder.CanonicalBand("STRONG LONG")
        Dim strongS As String = BandLadder.CanonicalBand("STRONG SHORT")
        Dim medL    As String = BandLadder.CanonicalBand("LONG")
        Dim medS    As String = BandLadder.CanonicalBand("SHORT")
        Dim weakL   As String = BandLadder.CanonicalBand("WEAK LONG")
        Dim weakS   As String = BandLadder.CanonicalBand("WEAK SHORT")

        ' Exclusion side: every NO TRADE form MUST return "". This includes the lean
        ' forms the WEAK-shape bracket embeds ("NO TRADE [WEAK LONG]") — the refused-
        ' signal record is NOT a WEAK data point, no matter the annotation.
        Dim noTrade    As String = BandLadder.CanonicalBand("NO TRADE")
        Dim noTradeWL  As String = BandLadder.CanonicalBand("NO TRADE [WEAK LONG]")
        Dim noTradeWS  As String = BandLadder.CanonicalBand("NO TRADE [WEAK SHORT]")
        Dim noTradeL   As String = BandLadder.CanonicalBand("NO TRADE [LONG]")
        Dim noTradeS   As String = BandLadder.CanonicalBand("NO TRADE [SHORT]")
        Dim empty      As String = BandLadder.CanonicalBand("")
        Dim nul        As String = BandLadder.CanonicalBand(Nothing)
        Dim garbage    As String = BandLadder.CanonicalBand("UNKNOWN")

        ' Cross-check against CanonicalTier: the two classifiers AGREE on the exclusion
        ' set for NO TRADE strings (both return ""), but DIVERGE on WEAK — CanonicalTier
        ' excludes WEAK (returns ""), BandLadder partitions it (returns "WEAK"). This
        ' pin is the mechanical guarantee against a future consumer accidentally
        ' cross-wiring the two.
        Dim tierWL       As String = FailureRateMatrix.CanonicalTier("WEAK LONG")
        Dim tierNoTradeW As String = FailureRateMatrix.CanonicalTier("NO TRADE [WEAK LONG]")

        Check("A35b band classifier: WEAK LONG/SHORT → WEAK; every NO TRADE form → """" (WEAK ≠ NO TRADE)",
              strongL = "STRONG" AndAlso strongS = "STRONG" AndAlso
              medL = "MEDIUM" AndAlso medS = "MEDIUM" AndAlso
              weakL = "WEAK" AndAlso weakS = "WEAK" AndAlso
              noTrade = "" AndAlso noTradeWL = "" AndAlso noTradeWS = "" AndAlso
              noTradeL = "" AndAlso noTradeS = "" AndAlso
              empty = "" AndAlso nul = "" AndAlso garbage = "" AndAlso
              tierWL = "" AndAlso tierNoTradeW = "",
              String.Format("sL={0} sS={1} mL={2} mS={3} wL={4} wS={5} " &
                            "NT={6} NT[WL]={7} NT[WS]={8} NT[L]={9} NT[S]={10} " &
                            "empty={11} nul={12} garb={13} tierWL={14} tierNTWL={15}",
                            strongL, strongS, medL, medS, weakL, weakS,
                            noTrade, noTradeWL, noTradeWS, noTradeL, noTradeS,
                            empty, nul, garbage, tierWL, tierNoTradeW))
    End Sub

    ' -- A35c: matrix cell space stays (tier × window) — no WEAK tier -------------
    ' Run FailureRateMatrix.Compute on a synthetic row set that MIXES the four tier
    ' strings AND both WEAK strings; assert exactly 12 cells at res=1 (4 tiers × 3
    ' windows) with no WEAK tier present. This pins §3c's F3 lesson: the tweaker-
    ' facing matrix population is UNCHANGED by the ladder — WEAK enters §9 alone.
    Private Sub A35c_MatrixCellSpaceUnchangedNoWeakTier()
        Dim cfg As New EngineSettings()
        Dim baseTs As DateTime = New DateTime(2026, 7, 20, 15, 0, 0, DateTimeKind.Utc)

        ' One row per tier + a WEAK row for each side. ATR/price arranged so the
        ' de-confound gate passes (2×20 vs 100 000 × 0.0008 = 80 → 40 clears 80? no —
        ' floor = 80, target dist = 40, would REJECT). Bump ATR so 2×ATR clears:
        ' ATR = 60 ⇒ target dist = 120 > 80 floor. Placed target below unfloored, so
        ' rows without HasPlaced use engineTarget × ATR = 2 × 60 = 120.
        Dim rows As New List(Of CsvRow)()
        Dim verdicts As String() = {"STRONG LONG", "STRONG SHORT", "LONG", "SHORT",
                                    "WEAK LONG", "WEAK SHORT"}
        For i As Integer = 0 To verdicts.Length - 1
            Dim r As New CsvRow With {
                .Index = i, .Timestamp = baseTs.AddMinutes(i),
                .Price = 100000.0, .ATR = 60.0, .Verdict = verdicts(i),
                .ExecResolution = 1, .HasPlaced = False}
            ' Populate ForwardBars for the res-1 window set {5,10,15}. Bars keep price
            ' flat so no barrier fires — outcomes = WINDOW_EXPIRED = failure. We only
            ' need the CELL to be present (n > 0); the outcome doesn't matter for A35c.
            For Each w In {5, 10, 15}
                Dim bars As New List(Of OhlcBar)()
                For b As Integer = 3 To w
                    bars.Add(New OhlcBar With {
                        .CloseTime = r.Timestamp.AddMinutes(b),
                        .Open = 100000.0, .High = 100005.0,
                        .Low = 99995.0, .Close = 100000.0})
                Next
                r.ForwardBars(w) = bars
            Next
            rows.Add(r)
        Next

        Dim atrEx, structStop, atrFb, placedTgt, legacyFav, belowMin As Integer
        Dim cells = FailureRateMatrix.Compute(rows, atrEx, structStop, atrFb,
                                               placedTgt, legacyFav, belowMin,
                                               cfg.Scoring.TradeCosts.EffectiveMinMovePct,
                                               cfg.Scoring.AtrTargetMultiplier,
                                               1, AdverseBarrierMode.Placed)

        ' 4 tiers × 3 windows = 12 cells. No WEAK tier — the two WEAK rows fell out
        ' at CanonicalTier ("" — excluded from the matrix, admitted only to §9's ladder).
        Dim distinctKeys = cells.Select(Function(c) c.VerdictTier & "|" & c.WindowMin.ToString()).Distinct().Count()
        Dim tierSet      = cells.Select(Function(c) c.VerdictTier).Distinct().OrderBy(Function(t) t).ToList()
        Dim expectedTiers = New List(Of String) From
            {"MEDIUM_LONG", "MEDIUM_SHORT", "STRONG_LONG", "STRONG_SHORT"}
        Dim tiersMatch    As Boolean = tierSet.SequenceEqual(expectedTiers)
        Dim noWeakTier    As Boolean = Not cells.Any(Function(c) c.VerdictTier.Contains("WEAK"))
        Dim allCellsHaveRows As Boolean = cells.All(Function(c) c.SampleSize = 1)

        Check("A35c matrix cell space stays (tier × window): 12 cells, no WEAK tier, WEAK rows excluded upstream",
              cells.Count = 12 AndAlso distinctKeys = 12 AndAlso tiersMatch AndAlso
              noWeakTier AndAlso allCellsHaveRows,
              String.Format("cellCount={0} distinct={1} tiersMatch={2} noWeak={3} allN=1?={4} tiers=[{5}]",
                            cells.Count, distinctKeys, tiersMatch, noWeakTier,
                            allCellsHaveRows, String.Join(",", tierSet)))
    End Sub

    ' -- A35d: PromptBuilder output carries NO ladder section / NO WEAK band row ---
    ' The tweaker-facing surface stays tradeable-population only (§3c). Assert on the
    ' full user message: the "Band ladder" heading is absent AND no line contains a
    ' WEAK band row shape. Also assert the existing matrix heading is still there
    ' (parity — this fixture should not accidentally hide the whole prompt).
    Private Sub A35d_PromptBuilderOmitsLadderAndWeak()
        ' Minimal inputs: an empty matrix + one CSV row are enough — PromptBuilder
        ' iterates the four tiers unconditionally and renders "n/a" for empty cells.
        Dim cells As New List(Of FailureCellResult)()
        Dim rows  As New List(Of CsvRow) From {
            New CsvRow With {.Timestamp = New DateTime(2026, 7, 20, 15, 0, 0, DateTimeKind.Utc),
                             .Price = 100000.0, .ATR = 44.0, .Verdict = "STRONG LONG",
                             .Regime = "TRENDING_UP", .FundingBias = "BULL_MILD",
                             .VerdictContext = "CONFIRMED", .OiCvdOutcome = "CONFIRMED"}}
        Dim history As New List(Of PickedCellEntry)()

        Dim result = PromptBuilder.Build(
            settingsJson:="{""version"": 55}",
            csvRows:=rows,
            failureCells:=cells,
            pickedCellHistory:=history,
            trigger:="test trigger",
            manifestActiveRows:="",
            conditions:=Nothing,
            maxKeysPerProposal:=3)

        Dim userMsg As String = result.UserMsg

        ' The load-bearing negatives.
        Dim noLadderHeading As Boolean = Not userMsg.Contains("Band ladder") AndAlso
                                         Not userMsg.Contains("## 9. Band ladder")
        Dim noWeakBandRow   As Boolean = Not userMsg.Contains("| WEAK   |") AndAlso
                                         Not userMsg.Contains("| WEAK |") AndAlso
                                         Not userMsg.Contains("WEAK LONG") AndAlso
                                         Not userMsg.Contains("WEAK SHORT") AndAlso
                                         Not userMsg.Contains("STRONG/MEDIUM/WEAK")
        ' The matrix headings for the tradeable population are still there (this
        ' fixture must not accidentally hide the whole prompt).
        Dim matrixStillThere As Boolean = userMsg.Contains("## Success-Rate Matrix") AndAlso
                                          userMsg.Contains("### STRONG_LONG") AndAlso
                                          userMsg.Contains("### MEDIUM_LONG")

        Check("A35d PromptBuilder omits ladder section + any WEAK band row; matrix tiers still rendered",
              noLadderHeading AndAlso noWeakBandRow AndAlso matrixStillThere,
              String.Format("noLadder={0} noWeak={1} matrixPresent={2} len={3}",
                            noLadderHeading, noWeakBandRow, matrixStillThere, userMsg.Length))
    End Sub

    ' =======================================================================
    ' A36 — geometry arbitration modes + signed buffers (v56).
    ' docs/geometry-arbitration-modes-proposal.md §3 acceptance.
    ' All defaults (target/stop mode 0, buffer 0.0) are BYTE-IDENTICAL to v51 B4b —
    ' A36a is the load-bearing pin: the exact A26 case set replayed with defaults
    ' produces identical SideLevels. Mode 1 / buffer ≠ 0 fixtures exercise the new
    ' branches. Same POCO cfg (entry 62000, ATR 40 ⇒ fallback 62070, target bound
    ' 140, stop bound/fallback dist 64, stop floor $2) as A26.
    ' =======================================================================

    Private Function A36Cfg() As EngineSettings
        Return New EngineSettings()          ' POCO defaults = shipped v56 (mode 0, buffer 0)
    End Function

    Private Function A36Indicators() As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = 62000.0
        r.ATR = 40.0
        Return r
    End Function

    ' -- A36a: defaults byte-identical to v51 B4b -- THE load-bearing fixture -------
    ' Runs the A26 case set through ComputeSideLevels with an explicit A36 cfg (POCO
    ' defaults = mode 0/0, buffer 0/0) and pins equality with the shipped placements.
    ' A26a-f are the standing pin for v51 B4b; A36a asserts the v56 default path
    ' produces the SAME outputs — the "zero live impact at this build" invariant.
    Private Sub A36a_DefaultsByteIdenticalToV51B4b()
        Dim cfg = A36Cfg()

        ' Case 1 — swing target farther than fallback still places (A26a long).
        Dim rSwing = A36Indicators() : rSwing.SwingTargetLong = 62100.0
        Dim lvSwing = SignalEmitter.ComputeSideLevels(New VerdictResult(), rSwing, cfg, isLong:=True)
        Dim ok1 As Boolean = lvSwing.Target = 62100.0 AndAlso lvSwing.Capped AndAlso
                             lvSwing.Reason = "PLACED @ 62100.0 (SWING_HIGH_5M)" AndAlso
                             lvSwing.TargetReason = "SWING_HIGH_5M" AndAlso
                             Math.Abs(lvSwing.RawTarget - 62070.0) < 0.0001

        ' Case 2 — swing too loose → HVN places (A26b tier walk).
        Dim rHvn = A36Indicators()
        rHvn.SwingTargetLong = 62150.0 : rHvn.VPFRNearestHvnAbove = 62120.0
        Dim lvHvn = SignalEmitter.ComputeSideLevels(New VerdictResult(), rHvn, cfg, isLong:=True)
        Dim ok2 As Boolean = lvHvn.Target = 62120.0 AndAlso lvHvn.TargetReason = "NEAREST_HVN_ABOVE" AndAlso
                             lvHvn.Reason = "PLACED @ 62120.0 (NEAREST_HVN_ABOVE)"

        ' Case 3 — no tier survives → FALLBACK_ATR, Reason Nothing (A26b fallback).
        Dim rFb = A36Indicators()
        rFb.SwingTargetLong = 62150.0 : rFb.VPFRNearestHvnAbove = 62200.0 : rFb.VPFRPoc = 62050.0
        Dim lvFb = SignalEmitter.ComputeSideLevels(New VerdictResult(), rFb, cfg, isLong:=True)
        Dim ok3 As Boolean = lvFb.Target = 62070.0 AndAlso Not lvFb.Capped AndAlso lvFb.Reason Is Nothing AndAlso
                             lvFb.TargetReason = "FALLBACK_ATR"

        ' Case 4 — structural stop within bound → SWING_STOP (A26c).
        Dim rStop = A36Indicators() : rStop.SwingStopLong = 61950.0
        Dim lvStop = SignalEmitter.ComputeSideLevels(New VerdictResult(), rStop, cfg, isLong:=True)
        Dim ok4 As Boolean = lvStop.StopPx = 61950.0 AndAlso lvStop.StopReason = "SWING_STOP"

        ' Case 5 — structural stop too loose → STOP_CLAMPED (A26c clamp).
        Dim rClamp = A36Indicators() : rClamp.SwingStopLong = 61900.0
        Dim lvClamp = SignalEmitter.ComputeSideLevels(New VerdictResult(), rClamp, cfg, isLong:=True)
        Dim ok5 As Boolean = Math.Abs(lvClamp.StopPx - 61936.0) < 0.0001 AndAlso lvClamp.StopReason = "STOP_CLAMPED"

        ' Case 6 — no structural stop → FALLBACK_ATR (A26c).
        Dim lvNone = SignalEmitter.ComputeSideLevels(New VerdictResult(), A36Indicators(), cfg, isLong:=True)
        Dim ok6 As Boolean = Math.Abs(lvNone.StopPx - 61936.0) < 0.0001 AndAlso lvNone.StopReason = "FALLBACK_ATR"

        Check("A36a defaults (mode 0/0, buffer 0/0) byte-identical to v51 B4b across the A26 case set",
              ok1 AndAlso ok2 AndAlso ok3 AndAlso ok4 AndAlso ok5 AndAlso ok6,
              String.Format("swing={0} hvn={1} fallback={2} sSwing={3} sClamp={4} sFb={5}",
                            ok1, ok2, ok3, ok4, ok5, ok6))
    End Sub

    ' -- A36b: NEAREST target mode picks the minimum-distance qualifying candidate --
    ' Includes the load-bearing case where the ATR fallback (dist 70) beats a FARTHER
    ' qualifying swing (dist 100 ≤ bound 140). Mode 0 would place the swing; mode 1
    ' places the fallback. Fallback wins ⇒ TargetReason=FALLBACK_ATR, Reason=Nothing.
    Private Sub A36b_NearestTargetPicksMinDistance()
        Dim cfg = A36Cfg()
        cfg.Scoring.StructuralLevels.TargetArbitrationMode = 1

        ' Fallback beats a farther-out swing (the A36a Case 1 inputs).
        Dim rFar = A36Indicators() : rFar.SwingTargetLong = 62100.0    ' swing dist 100 > fallback dist 70
        Dim lvFar = SignalEmitter.ComputeSideLevels(New VerdictResult(), rFar, cfg, isLong:=True)
        Dim okFallbackWins As Boolean = lvFar.Target = 62070.0 AndAlso Not lvFar.Capped AndAlso
                                        lvFar.Reason Is Nothing AndAlso
                                        lvFar.TargetReason = "FALLBACK_ATR" AndAlso
                                        Math.Abs(lvFar.RawTarget - 62070.0) < 0.0001

        ' Swing closer than fallback ⇒ swing still wins.
        Dim rClose = A36Indicators() : rClose.SwingTargetLong = 62050.0   ' swing dist 50 < fallback dist 70
        Dim lvClose = SignalEmitter.ComputeSideLevels(New VerdictResult(), rClose, cfg, isLong:=True)
        Dim okSwingWinsWhenCloser As Boolean = lvClose.Target = 62050.0 AndAlso lvClose.Capped AndAlso
                                               lvClose.TargetReason = "SWING_HIGH_5M" AndAlso
                                               lvClose.Reason = "PLACED @ 62050.0 (SWING_HIGH_5M)"

        ' Multiple structural candidates: HVN (dist 40) < swing (dist 50) < fallback (dist 70).
        Dim rMulti = A36Indicators()
        rMulti.SwingTargetLong = 62050.0
        rMulti.VPFRNearestHvnAbove = 62040.0
        Dim lvMulti = SignalEmitter.ComputeSideLevels(New VerdictResult(), rMulti, cfg, isLong:=True)
        Dim okHvnWins As Boolean = lvMulti.Target = 62040.0 AndAlso
                                   lvMulti.TargetReason = "NEAREST_HVN_ABOVE"

        ' Short mirror — fallback (dist 70) beats a farther swing (dist 100).
        Dim rShort = A36Indicators() : rShort.SwingTargetShort = 61900.0
        Dim lvShort = SignalEmitter.ComputeSideLevels(New VerdictResult(), rShort, cfg, isLong:=False)
        Dim okShort As Boolean = Math.Abs(lvShort.Target - 61930.0) < 0.0001 AndAlso
                                 lvShort.TargetReason = "FALLBACK_ATR" AndAlso Not lvShort.Capped

        Check("A36b NEAREST target picks min-distance (fallback beats farther swing; closer swing wins; HVN wins over swing+fallback; short mirror)",
              okFallbackWins AndAlso okSwingWinsWhenCloser AndAlso okHvnWins AndAlso okShort,
              String.Format("fbWins={0} swingWinsClose={1} hvnWins={2} short={3}",
                            okFallbackWins, okSwingWinsWhenCloser, okHvnWins, okShort))
    End Sub

    ' -- A36c: WIDEST stop picks max(structural, stop_max×ATR), respects 4-tick floor --
    ' Mode 0 would clamp a wider swing to stop_max; mode 1 keeps the wider swing.
    ' Mode 1 with a tighter-than-bound swing loses to the ATR bound (FALLBACK_ATR).
    Private Sub A36c_WidestStopPicksMaxAndRespectsFloor()
        Dim cfg = A36Cfg()
        cfg.Scoring.StructuralLevels.StopArbitrationMode = 1

        ' Wider swing wins over the ATR bound (mode 0 would STOP_CLAMPED at 61936).
        Dim rWide = A36Indicators() : rWide.SwingStopLong = 61900.0   ' dist 100 > bound 64
        Dim lvWide = SignalEmitter.ComputeSideLevels(New VerdictResult(), rWide, cfg, isLong:=True)
        Dim okWider As Boolean = lvWide.StopPx = 61900.0 AndAlso lvWide.StopReason = "SWING_STOP"

        ' Tighter swing loses to the ATR bound → FALLBACK_ATR at entry − stop_max×ATR.
        Dim rTight = A36Indicators() : rTight.SwingStopLong = 61950.0  ' dist 50 < bound 64
        Dim lvTight = SignalEmitter.ComputeSideLevels(New VerdictResult(), rTight, cfg, isLong:=True)
        Dim okTighterLoses As Boolean = Math.Abs(lvTight.StopPx - 61936.0) < 0.0001 AndAlso
                                        lvTight.StopReason = "FALLBACK_ATR"

        ' No structural stop → FALLBACK_ATR at entry − stop_max×ATR.
        Dim lvNone = SignalEmitter.ComputeSideLevels(New VerdictResult(), A36Indicators(), cfg, isLong:=True)
        Dim okNoStruct As Boolean = Math.Abs(lvNone.StopPx - 61936.0) < 0.0001 AndAlso
                                    lvNone.StopReason = "FALLBACK_ATR"

        ' Floor respected under a punishing negative stop buffer: ATR bound 64, buffer −99%
        ' would give dist 0.64 → snaps to floor $2 (61998).
        Dim cfgFloor = A36Cfg()
        cfgFloor.Scoring.StructuralLevels.StopArbitrationMode = 1
        cfgFloor.Scoring.StructuralLevels.StopBufferPct = -99.0
        Dim lvFloor = SignalEmitter.ComputeSideLevels(New VerdictResult(), A36Indicators(), cfgFloor, isLong:=True)
        Dim okFloor As Boolean = Math.Abs(lvFloor.StopPx - 61998.0) < 0.0001

        ' Short mirror: wider swing (dist 100) beats the ATR bound (64) → keeps 62100.
        Dim rShort = A36Indicators() : rShort.SwingStopShort = 62100.0
        Dim lvShort = SignalEmitter.ComputeSideLevels(New VerdictResult(), rShort, cfg, isLong:=False)
        Dim okShort As Boolean = lvShort.StopPx = 62100.0 AndAlso lvShort.StopReason = "SWING_STOP"

        Check("A36c WIDEST stop picks max + respects the 4-tick floor (wider swing wins; tighter loses to ATR bound; floor snaps buffered stop; short mirror)",
              okWider AndAlso okTighterLoses AndAlso okNoStruct AndAlso okFloor AndAlso okShort,
              String.Format("wider={0} tighterLoses={1} noStruct={2} floor={3} short={4} px={5}",
                            okWider, okTighterLoses, okNoStruct, okFloor, okShort, lvFloor.StopPx))
    End Sub

    ' -- A36d: signed buffers move the right direction; min-move gate reads buffered --
    ' The load-bearing check: a negative target buffer must be able to gate a verdict to
    ' BELOW_MIN_MOVE through the REAL Calculate() (Step 5c reads placed via Step 5b's
    ' AdjustedLongTarget/TargetCapReasonLong, which SignalEmitter.ComputeSideLevels
    ' populates with the buffered price when Capped fires).
    Private Sub A36d_SignedBuffersMoveAndMinMoveGateReadsBuffered()
        ' (a) Target buffer signs: entry 62000, placed 62100.
        '     +10% → placed' = 62000 + 100×1.10 = 62110  (farther from entry).
        '     −5%  → placed' = 62000 + 100×0.95 = 62095  (closer to entry).
        Dim cfgPos = A36Cfg() : cfgPos.Scoring.StructuralLevels.TargetBufferPct = 10.0
        Dim rL = A36Indicators() : rL.SwingTargetLong = 62100.0
        Dim lvPos = SignalEmitter.ComputeSideLevels(New VerdictResult(), rL, cfgPos, isLong:=True)
        Dim cfgNeg = A36Cfg() : cfgNeg.Scoring.StructuralLevels.TargetBufferPct = -5.0
        Dim lvNeg = SignalEmitter.ComputeSideLevels(New VerdictResult(), rL, cfgNeg, isLong:=True)
        Dim okTgt As Boolean = Math.Abs(lvPos.Target - 62110.0) < 0.0001 AndAlso
                               Math.Abs(lvNeg.Target - 62095.0) < 0.0001 AndAlso
                               lvPos.Reason.Contains("BUF +10%") AndAlso
                               lvNeg.Reason.Contains("BUF -5%")

        ' (b) Stop buffer signs: entry 62000, placed 61950 (dist 50).
        '     +10% → placed' = 62000 − 50×1.10 = 61945 (wider stop, farther from entry).
        '     −20% → placed' = 62000 − 50×0.80 = 61960 (tighter stop).
        Dim cfgSp = A36Cfg() : cfgSp.Scoring.StructuralLevels.StopBufferPct = 10.0
        Dim rS = A36Indicators() : rS.SwingStopLong = 61950.0
        Dim lvSp = SignalEmitter.ComputeSideLevels(New VerdictResult(), rS, cfgSp, isLong:=True)
        Dim cfgSn = A36Cfg() : cfgSn.Scoring.StructuralLevels.StopBufferPct = -20.0
        Dim lvSn = SignalEmitter.ComputeSideLevels(New VerdictResult(), rS, cfgSn, isLong:=True)
        Dim okStp As Boolean = Math.Abs(lvSp.StopPx - 61945.0) < 0.0001 AndAlso
                               Math.Abs(lvSn.StopPx - 61960.0) < 0.0001

        ' (c) Min-move gate reads buffered target (real Calculate). ATR 20 ⇒ fallback dist
        ' 35 (raw 62035); swing 62060 (dist 60) places under bound 70. Without a buffer the
        ' gate stands (60 > floor 49.6). With −40% target buffer: placed' = 62000 + 60×0.60
        ' = 62036 → dist 36 < 49.6 ⇒ BELOW_MIN_MOVE. This proves the buffered price flows
        ' through Step 5b onto Adjusted* and the gate reads it.
        Dim cfgStandGate = BuildA8Cfg(fundingBoost:=0)
        Dim rGate1 = BuildGateIndicators(atr:=20, price:=62000)
        rGate1.SwingTargetShort = 61940.0                  ' dist 60 ≤ bound 70 → places
        Dim vStand = ScoringEngine.Calculate(rGate1, PositionState.None, BuildA8Norms(), cfgStandGate)

        Dim cfgGateBuf = BuildA8Cfg(fundingBoost:=0)
        cfgGateBuf.Scoring.StructuralLevels.TargetBufferPct = -40.0
        Dim rGate2 = BuildGateIndicators(atr:=20, price:=62000)
        rGate2.SwingTargetShort = 61940.0
        Dim vGated = ScoringEngine.Calculate(rGate2, PositionState.None, BuildA8Norms(), cfgGateBuf)
        Dim okGate As Boolean = Not vStand.Verdict.StartsWith("NO TRADE") AndAlso
                                vStand.VerdictContext <> "BELOW_MIN_MOVE" AndAlso
                                vGated.Verdict = "NO TRADE" AndAlso
                                vGated.VerdictContext = "BELOW_MIN_MOVE"

        Check("A36d signed buffers move each side + Step 5c min-move gate reads BUFFERED target",
              okTgt AndAlso okStp AndAlso okGate,
              String.Format("tgt={0} stp={1} gate=(stand='{2}' ctx={3} adj={4:F1}; gated='{5}' ctx={6} adj={7:F1})",
                            okTgt, okStp,
                            vStand.Verdict, vStand.VerdictContext, vStand.AdjustedShortTarget,
                            vGated.Verdict, vGated.VerdictContext, vGated.AdjustedShortTarget))
    End Sub

    ' -- A36e: WhatIfOverlay whitelist + HC24 tweaker fence -----------------------
    Private Sub A36e_WhitelistAndHc24Fence()
        ' Whitelist: the 4 new keys parse cleanly; a sibling flat numeric already listed
        ' still parses.
        Dim okMode = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""target_arbitration_mode"":1}}}")
        Dim okBuf  = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""stop_buffer_pct"":10.0}}}")
        Dim okSib  = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""target_max_atr_mult"":3.0}}}")
        ' Numeric sweep 0→1 step 1 on an int-coded mode expands to two cells.
        Dim okSweep = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""target_arbitration_mode"":{""sweep"":{""from"":0,""to"":1,""step"":1}}}}}")
        Dim okWl As Boolean = okMode.Knobs.Count = 1 AndAlso
                              okMode.Knobs(0).Path = "scoring.structural_levels.target_arbitration_mode" AndAlso
                              okBuf.Knobs.Count = 1 AndAlso
                              okBuf.Knobs(0).Path = "scoring.structural_levels.stop_buffer_pct" AndAlso
                              okSib.Knobs.Count = 1 AndAlso
                              okSweep.Knobs.Count = 1 AndAlso okSweep.Knobs(0).IsSweep AndAlso
                              okSweep.Knobs(0).Values.Count = 2

        ' HC24 fence: SettingsDiffApplier exact-match rejects the 4 keys; the sibling
        ' target_max_atr_mult (HC21 flat surface) still passes.
        Dim s As String = "{""version"":56,""scoring"":{""atr_target_multiplier"":1.75," &
                          """structural_levels"":{""enabled"":true,""target_max_atr_mult"":3.5," &
                          """stop_max_atr_mult"":1.6,""stop_min_floor_ticks"":4," &
                          """stop_too_loose_mode"":""clamp""," &
                          """target_arbitration_mode"":0,""stop_arbitration_mode"":0," &
                          """target_buffer_pct"":0.0,""stop_buffer_pct"":0.0}}}"
        Dim rTMode = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.target_arbitration_mode", "0", "1"), s, 3)
        Dim rSMode = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.stop_arbitration_mode", "0", "1"), s, 3)
        Dim rTBuf  = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.target_buffer_pct", "0.0", "-5.0"), s, 3)
        Dim rSBuf  = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.stop_buffer_pct", "0.0", "10.0"), s, 3)
        Dim rBound = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.target_max_atr_mult", "3.5", "3.0"), s, 3)
        Dim okFence As Boolean =
            Not rTMode.IsValid AndAlso rTMode.ErrorReason.Contains("HARD CONSTRAINT 24") AndAlso
            Not rSMode.IsValid AndAlso rSMode.ErrorReason.Contains("HARD CONSTRAINT 24") AndAlso
            Not rTBuf.IsValid  AndAlso rTBuf.ErrorReason.Contains("HARD CONSTRAINT 24") AndAlso
            Not rSBuf.IsValid  AndAlso rSBuf.ErrorReason.Contains("HARD CONSTRAINT 24") AndAlso
            rBound.IsValid

        Check("A36e whitelist accepts 4 new keys (incl. int-mode sweep) + HC24 exact-match fence rejects them; sibling flat numeric still passes",
              okWl AndAlso okFence,
              String.Format("wl={0} fence(tMode={1},sMode={2},tBuf={3},sBuf={4},bound={5})",
                            okWl, rTMode.IsValid, rSMode.IsValid, rTBuf.IsValid, rSBuf.IsValid, rBound.IsValid))
    End Sub

    ' -- A36f: a mode-1 overlay replays through the What-If adapter identically ------
    ' The A30a linked-seam pattern extended: WhatIfSettings.BuildCellSettings applies the
    ' overlay onto a cloned cfg, and the adapter fed through the shipped
    ' SignalEmitter.ComputeSideLevels produces the SAME SideLevels a direct call under a
    ' hand-mutated cfg would. Proves the overlay path actually reaches the new POCO fields.
    Private Sub A36f_ModeOneOverlayRoundTripsThroughWhatIf()
        ' Overlay: flip target mode to NEAREST + apply a +5% target buffer + +10% stop buffer.
        Dim overlay As New Dictionary(Of String, Double) From {
            {"scoring.structural_levels.target_arbitration_mode", 1},
            {"scoring.structural_levels.target_buffer_pct", 5.0},
            {"scoring.structural_levels.stop_buffer_pct", 10.0}
        }

        ' A settings.json fragment carrying the four new fields at their defaults —
        ' WhatIfSettings deserialises this fresh per cell, then applies the overlay.
        Dim tmp As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                                    "wifsettings-a36f-" & Guid.NewGuid().ToString("N") & ".json")
        Try
            System.IO.File.WriteAllText(tmp,
                "{""version"":56," &
                """scoring"":{""atr_target_multiplier"":1.75,""atr_stop_multiplier"":1.6," &
                """structural_levels"":{""enabled"":true,""target_max_atr_mult"":3.5," &
                """stop_max_atr_mult"":1.6,""stop_min_floor_ticks"":4," &
                """stop_too_loose_mode"":""clamp""," &
                """target_arbitration_mode"":0,""stop_arbitration_mode"":0," &
                """target_buffer_pct"":0.0,""stop_buffer_pct"":0.0}}}")

            Dim wis As New WhatIfSettings(tmp)
            Dim cellCfg = wis.BuildCellSettings(overlay)

            ' The mutation actually landed on the POCO fields.
            Dim okApply As Boolean =
                cellCfg.Scoring.StructuralLevels.TargetArbitrationMode = 1 AndAlso
                cellCfg.Scoring.StructuralLevels.StopArbitrationMode = 0 AndAlso
                Math.Abs(cellCfg.Scoring.StructuralLevels.TargetBufferPct - 5.0) < 0.0001 AndAlso
                Math.Abs(cellCfg.Scoring.StructuralLevels.StopBufferPct - 10.0) < 0.0001

            ' The adapter's placed levels ≡ a direct ComputeSideLevels call under the same cfg.
            Dim row = BuildWhatIfRow()
            Dim r = WhatIfReplay.BuildIndicator(row)
            Dim direct = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cellCfg, isLong:=True)
            Dim run = WhatIfReplay.RunCell(New List(Of CsvRow) From {row}, cellCfg, 15, keepRows:=True)
            Dim rep = run.ReplayedRows(0)
            Dim okAdapter As Boolean =
                Math.Abs(rep.PlacedTargetLong - direct.Target) < 0.001 AndAlso
                Math.Abs(rep.PlacedStopLong - direct.StopPx) < 0.001

            Check("A36f mode-1 overlay round-trips through WhatIfSettings.BuildCellSettings and reproduces ComputeSideLevels via the WhatIfReplay adapter",
                  okApply AndAlso okAdapter,
                  String.Format(CultureInfo.InvariantCulture,
                                "apply={0} adapter={1} directT={2:F3} repT={3:F3} directS={4:F3} repS={5:F3}",
                                okApply, okAdapter,
                                direct.Target, rep.PlacedTargetLong, direct.StopPx, rep.PlacedStopLong))
        Finally
            Try : System.IO.File.Delete(tmp) : Catch : End Try
        End Try
    End Sub

    ' =======================================================================
    ' A37 — #7 liq-cascade alarm + #8 level-approach alerts (docs/liq-cascade-
    ' level-alerts-proposal.md §4 H5). Deterministic folds against the host-
    ' agnostic AlertsTracker; sidecar utility exercised via the shipped
    ' AlertsSidecar.TryAppend in a scratch bin dir; tweaker surface via
    ' SettingsDiffApplier. The strip render + audible cue stay OUT — same
    ' live-socket / WinForms boundary as A16-A31.
    ' =======================================================================

    ' A helper: build a fresh AlertsSettings at proposal defaults (matches settings.json).
    Private Function AlertsCfgDefault() As AlertsSettings
        Return New AlertsSettings() ' the POCO defaults ARE the shipped v59 anchors
    End Function

    ' -- A37a: cascade window — 3 in 10s across sides, edge-fires ONCE, dominant side ---
    Private Sub A37a_CascadeWindowMathAndEdgeFireOnce()
        Dim tr As New AlertsTracker()
        Dim cfg = AlertsCfgDefault()
        Dim iid As String = "test-instance-a37a"
        Dim ts As Long = 1_700_000_000_000L   ' arbitrary anchor (ms since epoch, ~2023-11)

        ' Two non-liq trades — never enter the window at all.
        tr.FoldTrade(60000.0, 5000.0, True, isLiq:=False, tsMs:=ts, cfg:=cfg, instanceId:=iid)
        tr.FoldTrade(60005.0, 6000.0, True, isLiq:=False, tsMs:=ts + 100, cfg:=cfg, instanceId:=iid)
        Dim s0 = tr.Snapshot(ts + 200, cfg)
        Dim step0Ok As Boolean = s0.CascadeSignal = "NONE" AndAlso s0.CascadeCount = 0

        ' Sidecar existence would let A37a's FIRST_SEEN counter go silent (the sidecar
        ' sentinel = per-process-lifetime unlock). Clear it first so this fixture reads
        ' a clean "first-ever" event.
        Dim sidecarPath As String = AlertsSidecar.GetPath()
        Try
            If File.Exists(sidecarPath) Then File.Delete(sidecarPath)
        Catch
        End Try

        ' Two liq-flagged BUYS in quick succession — count 2 < min 3 ⇒ still NONE, but
        ' the FIRST_SEEN event fires on the FIRST of the two (drained by s1).
        tr.FoldTrade(60010.0, 250000.0, True, isLiq:=True, tsMs:=ts + 1000, cfg:=cfg, instanceId:=iid)
        tr.FoldTrade(60015.0, 300000.0, True, isLiq:=True, tsMs:=ts + 2000, cfg:=cfg, instanceId:=iid)
        Dim s1 = tr.Snapshot(ts + 2100, cfg)
        Dim firstSeenFired As Integer = 0
        For Each ev In s1.PendingEvents
            If ev.Kind = "FIRST_SEEN" Then firstSeenFired += 1
        Next
        Dim step1Ok As Boolean = s1.CascadeSignal = "NONE" AndAlso s1.CascadeCount = 2 AndAlso
                                 firstSeenFired = 1

        ' Third liq-flagged trade — one SELL — pushes the count over the threshold.
        ' Dominant side is BUY ($550k) > SELL ($200k) ⇒ CASCADE_ABOVE. Pending events
        ' now include one CASCADE (edge-fire) only (FIRST_SEEN was drained by s1).
        tr.FoldTrade(60020.0, 200000.0, False, isLiq:=True, tsMs:=ts + 3000, cfg:=cfg, instanceId:=iid)
        Dim s2 = tr.Snapshot(ts + 3100, cfg)
        Dim cascadeFired As Integer = 0
        For Each ev In s2.PendingEvents
            If ev.Kind = "CASCADE" Then cascadeFired += 1
        Next
        Dim step2Ok As Boolean = s2.CascadeSignal = "CASCADE_ABOVE" AndAlso
                                 s2.CascadeCount = 3 AndAlso
                                 s2.CascadeBuyCount = 2 AndAlso s2.CascadeSellCount = 1 AndAlso
                                 cascadeFired = 1

        ' A fourth liq trade — still above threshold — must NOT double-fire the CASCADE event.
        tr.FoldTrade(60021.0, 100000.0, True, isLiq:=True, tsMs:=ts + 4000, cfg:=cfg, instanceId:=iid)
        Dim s3 = tr.Snapshot(ts + 4100, cfg)
        Dim cascadeFired2 As Integer = 0
        For Each ev In s3.PendingEvents
            If ev.Kind = "CASCADE" Then cascadeFired2 += 1
        Next
        Dim step3Ok As Boolean = s3.CascadeSignal = "CASCADE_ABOVE" AndAlso cascadeFired2 = 0

        ' Advance past the window ⇒ all pruned; re-arm.
        Dim s4 = tr.Snapshot(ts + 20000, cfg)
        Dim step4Ok As Boolean = s4.CascadeSignal = "NONE" AndAlso s4.CascadeCount = 0

        Check("A37a cascade window math + edge-fires once + dominant side + re-arm",
              step0Ok AndAlso step1Ok AndAlso step2Ok AndAlso step3Ok AndAlso step4Ok,
              String.Format("s0(sig={0} n={1}) s1(sig={2} n={3} firstFired={4}) s2(sig={5} n={6} b={7} s={8} cascFired={9}) s3(sig={10} cascFired2={11}) s4(sig={12} n={13})",
                            s0.CascadeSignal, s0.CascadeCount,
                            s1.CascadeSignal, s1.CascadeCount, firstSeenFired,
                            s2.CascadeSignal, s2.CascadeCount, s2.CascadeBuyCount, s2.CascadeSellCount,
                            cascadeFired,
                            s3.CascadeSignal, cascadeFired2,
                            s4.CascadeSignal, s4.CascadeCount))
        Try
            If File.Exists(sidecarPath) Then File.Delete(sidecarPath)
        Catch
        End Try
    End Sub

    ' -- A37b: level-approach episode — enter within N ticks, re-arm on leave; re-map close ---
    Private Sub A37b_LevelApproachEpisodeReArm()
        Dim tr As New AlertsTracker()
        Dim cfg = AlertsCfgDefault()
        Dim iid As String = "test-instance-a37b"
        Dim ts As Long = 1_700_000_000_000L
        ' SignalEmitter.TickSize = 0.5 — the 12-tick anchor = $6.
        ' Carried levels: swingHigh5m 60100, swingLow5m 59900 (HVNs + 15m swings zero).
        tr.SetLevels(60100.0, 59900.0, 0.0, 0.0, 0.0, 0.0)

        ' Price 60050 — 100 ticks below 60100 (100 * $0.5 = $50), NOT within 12 ticks ⇒ inactive.
        tr.FoldTrade(60050.0, 5000.0, True, isLiq:=False, tsMs:=ts, cfg:=cfg, instanceId:=iid)
        Dim sA = tr.Snapshot(ts + 100, cfg)
        Dim aInactive As Boolean = Not sA.ApproachAboveActive AndAlso Not sA.ApproachBelowActive

        ' Move to 60094 — 12 ticks below (12 * 0.5 = 6, matches level_ticks:12) ⇒ ABOVE approach fires.
        tr.FoldTrade(60094.0, 5000.0, True, isLiq:=False, tsMs:=ts + 200, cfg:=cfg, instanceId:=iid)
        Dim sB = tr.Snapshot(ts + 300, cfg)
        Dim bAboveActive As Boolean = sB.ApproachAboveActive AndAlso
                                       Math.Abs(sB.ApproachAboveLevel - 60100.0) < 0.01 AndAlso
                                       Not sB.ApproachBelowActive

        ' Leave the band — move down to 60070 (60 ticks below) — the ABOVE episode should re-arm.
        tr.FoldTrade(60070.0, 5000.0, True, isLiq:=False, tsMs:=ts + 400, cfg:=cfg, instanceId:=iid)
        Dim sC = tr.Snapshot(ts + 500, cfg)
        Dim cReArmed As Boolean = Not sC.ApproachAboveActive

        ' Re-enter — new episode.
        tr.FoldTrade(60095.0, 5000.0, True, isLiq:=False, tsMs:=ts + 600, cfg:=cfg, instanceId:=iid)
        Dim sD = tr.Snapshot(ts + 700, cfg)
        Dim dReEnter As Boolean = sD.ApproachAboveActive AndAlso
                                   Math.Abs(sD.ApproachAboveLevel - 60100.0) < 0.01

        ' Re-map the carried level (swingHigh5m moves to 60200) — mid-episode close (no
        ' cross-level bleed — the #6 discipline).
        tr.SetLevels(60200.0, 59900.0, 0.0, 0.0, 0.0, 0.0)
        Dim sE = tr.Snapshot(ts + 800, cfg)
        Dim eReMapClosed As Boolean = Not sE.ApproachAboveActive

        ' v59 follow-up: 15m swings extend the candidate set. Carry a 15m swingHigh AT
        ' 60150 with the 5m swingHigh farther away (60300) — the ABOVE approach must
        ' select the CLOSER 15m level (60150), not the 5m 60300. Price 60148 sits 4
        ' ticks below 60150 ⇒ within band ⇒ fires against the 15m pivot.
        Dim tr2 As New AlertsTracker()
        tr2.SetLevels(60300.0, 59700.0, 0.0, 0.0, 60150.0, 59850.0)
        tr2.FoldTrade(60148.0, 5000.0, True, isLiq:=False, tsMs:=ts + 900, cfg:=cfg, instanceId:=iid)
        Dim sF = tr2.Snapshot(ts + 1000, cfg)
        Dim f15mSelected As Boolean = sF.ApproachAboveActive AndAlso
                                       Math.Abs(sF.ApproachAboveLevel - 60150.0) < 0.01

        Check("A37b level-approach — enter within 12 ticks, re-arm on leave, close on level re-map, 15m in candidate set",
              aInactive AndAlso bAboveActive AndAlso cReArmed AndAlso dReEnter AndAlso eReMapClosed AndAlso f15mSelected,
              String.Format("inactive={0} enter={1} reArm={2} reEnter={3} reMap={4} 15mSelected={5} (b.level={6} d.level={7} f.level={8})",
                            aInactive, bAboveActive, cReArmed, dReEnter, eReMapClosed, f15mSelected,
                            sB.ApproachAboveLevel, sD.ApproachAboveLevel, sF.ApproachAboveLevel))
    End Sub

    ' -- A37c: sidecar append shape (utc | kind | side | usd | instance_id), never-throws ---
    Private Sub A37c_SidecarAppendShape()
        ' Redirect the sidecar path to a temp file — AlertsSidecar.GetPath uses
        ' AppDomain.CurrentDomain.BaseDirectory + "liq_events.log", so we clean up
        ' any residual file in the harness bin dir instead of relocating.
        Dim path As String = AlertsSidecar.GetPath()
        Dim didThrow As Boolean = False
        Try
            If File.Exists(path) Then File.Delete(path)

            Dim ev1 As New AlertEvent With {
                .Kind = "FIRST_SEEN", .Side = "SELL", .UsdAmount = 250000.0,
                .UtcMs = 1_700_000_000_000L, .InstanceId = "iid-A37c"}
            Dim ok1 As Boolean = AlertsSidecar.TryAppend(ev1)

            Dim ev2 As New AlertEvent With {
                .Kind = "CASCADE", .Side = "BUY", .UsdAmount = 1234567.0,
                .UtcMs = 1_700_000_010_000L, .InstanceId = "iid-A37c"}
            Dim ok2 As Boolean = AlertsSidecar.TryAppend(ev2)

            Dim lines() As String = File.ReadAllLines(path)
            Dim row1 As String = If(lines.Length > 0, lines(0), "")
            Dim row2 As String = If(lines.Length > 1, lines(1), "")

            ' Shape: "<utc> | <kind> | <side> | <usd> | <instance_id>". Split on " | " to
            ' recover the fields.
            Dim p1() As String = row1.Split(New String() {" | "}, StringSplitOptions.None)
            Dim p2() As String = row2.Split(New String() {" | "}, StringSplitOptions.None)
            Dim shape1Ok As Boolean = p1.Length = 5 AndAlso p1(1) = "FIRST_SEEN" AndAlso
                                       p1(2) = "SELL" AndAlso p1(3) = "250000" AndAlso p1(4) = "iid-A37c" AndAlso
                                       p1(0).EndsWith("Z")
            Dim shape2Ok As Boolean = p2.Length = 5 AndAlso p2(1) = "CASCADE" AndAlso
                                       p2(2) = "BUY" AndAlso p2(3) = "1234567" AndAlso p2(4) = "iid-A37c"

            ' Never-throws on a null event.
            Dim ok3 As Boolean = Not AlertsSidecar.TryAppend(Nothing)

            Check("A37c sidecar append shape (utc | kind | side | usd | instance_id, append-only, null-safe)",
                  ok1 AndAlso ok2 AndAlso shape1Ok AndAlso shape2Ok AndAlso ok3 AndAlso Not didThrow,
                  String.Format("ok1={0} ok2={1} shape1={2} shape2={3} ok3(null)={4} row1='{5}' row2='{6}'",
                                ok1, ok2, shape1Ok, shape2Ok, ok3, row1, row2))
        Catch ex As Exception
            didThrow = True
            Check("A37c sidecar append shape", False, "threw: " & ex.Message)
        Finally
            Try
                If File.Exists(path) Then File.Delete(path)
            Catch
            End Try
        End Try
    End Sub

    ' -- A37d: Reset clears state; disabled cfg makes Fold + Snapshot a no-op ---
    Private Sub A37d_ResetAndDisabledInert()
        Dim tr As New AlertsTracker()
        Dim cfg = AlertsCfgDefault()
        Dim iid As String = "test-instance-a37d"
        Dim ts As Long = 1_700_000_000_000L
        Dim sidecarPath As String = AlertsSidecar.GetPath()
        Try
            If File.Exists(sidecarPath) Then File.Delete(sidecarPath)

            ' Fire three liq-flagged trades to build a cascade — then Reset — then check.
            tr.FoldTrade(60000.0, 100000.0, True, True, ts + 100, cfg, iid)
            tr.FoldTrade(60000.0, 100000.0, True, True, ts + 200, cfg, iid)
            tr.FoldTrade(60000.0, 100000.0, True, True, ts + 300, cfg, iid)
            Dim sPre = tr.Snapshot(ts + 400, cfg)
            Dim preFired As Boolean = sPre.CascadeSignal = "CASCADE_ABOVE"

            tr.Reset()
            Dim sPost = tr.Snapshot(ts + 500, cfg)
            Dim resetOk As Boolean = sPost.CascadeSignal = "NONE" AndAlso sPost.CascadeCount = 0 AndAlso
                                     sPost.PendingEvents.Count = 0

            ' Disabled cfg — Fold and Snapshot are no-ops (the byte-identical rollback path).
            Dim cfgOff As New AlertsSettings With {.Enabled = False}
            tr.FoldTrade(60000.0, 100000.0, True, True, ts + 600, cfgOff, iid)
            tr.FoldTrade(60000.0, 100000.0, True, True, ts + 700, cfgOff, iid)
            tr.FoldTrade(60000.0, 100000.0, True, True, ts + 800, cfgOff, iid)
            Dim sOff = tr.Snapshot(ts + 900, cfgOff)
            Dim disabledInert As Boolean = sOff.CascadeSignal = "NONE" AndAlso sOff.CascadeCount = 0

            Check("A37d Reset clears cascade state; disabled cfg makes Fold+Snapshot inert",
                  preFired AndAlso resetOk AndAlso disabledInert,
                  String.Format("preFired={0} resetOk={1} disabledInert={2} sPre.count={3} sPost.count={4} sOff.count={5}",
                                preFired, resetOk, disabledInert, sPre.CascadeCount, sPost.CascadeCount, sOff.CascadeCount))
        Finally
            Try
                If File.Exists(sidecarPath) Then File.Delete(sidecarPath)
            Catch
            End Try
        End Try
    End Sub

    ' -- A37e: HC25 fence rejects alerts.* keys; sibling scoring key still passes -----
    Private Sub A37e_Hc25AlertsFence()
        Dim s As String = "{""version"":59,""scoring"":{""verdict_med_pct"":0.53}," &
                          """alerts"":{""enabled"":true,""cascade_min_trades"":3," &
                          """cascade_window_sec"":10,""level_ticks"":12,""sound_enabled"":false}}"
        Dim rEnabled = SettingsDiffApplier.Validate(OneDiff("alerts.enabled", "true", "false"), s, 3)
        Dim rMin = SettingsDiffApplier.Validate(OneDiff("alerts.cascade_min_trades", "3", "5"), s, 3)
        Dim rWin = SettingsDiffApplier.Validate(OneDiff("alerts.cascade_window_sec", "10", "20"), s, 3)
        Dim rTicks = SettingsDiffApplier.Validate(OneDiff("alerts.level_ticks", "12", "24"), s, 3)
        Dim rSound = SettingsDiffApplier.Validate(OneDiff("alerts.sound_enabled", "false", "true"), s, 3)
        Dim rSib = SettingsDiffApplier.Validate(OneDiff("scoring.verdict_med_pct", "0.53", "0.55"), s, 3)

        Check("A37e HC25 fence rejects all 5 alerts.* keys; sibling scoring.verdict_med_pct still passes",
              Not rEnabled.IsValid AndAlso rEnabled.ErrorReason.Contains("off-tweaker-surface") AndAlso
              Not rMin.IsValid AndAlso Not rWin.IsValid AndAlso
              Not rTicks.IsValid AndAlso Not rSound.IsValid AndAlso
              rSib.IsValid,
              String.Format("enabled={0} min={1} win={2} ticks={3} sound={4} sib={5} reason='{6}'",
                            rEnabled.IsValid, rMin.IsValid, rWin.IsValid,
                            rTicks.IsValid, rSound.IsValid, rSib.IsValid, rEnabled.ErrorReason))
    End Sub

    ' -- A34f: §6 renders TWO sub-tables (a directional + b lean) + caption --------
    ' The D7 spin-off-2 render segmentation: same population, one directional
    ' context outcome + one lean context count; verify both sub-tables and the
    ' not-comparable caption appear in the rendered markdown.
    Private Sub A34f_ContextOutcomesSplitAndCaption()
        Dim rep As New AnalysisReport()
        Dim pop As New PopulationReport With {
            .PopulationKey = "NY|1", .SessionName = "NY", .Resolution = 1,
            .BarrierLabel = "PLACED", .RowCount = 100}
        ' Add a directional context cell (CONFIRMED, 20/50 success ⇒ 40%).
        Dim ciLow As Double, ciHigh As Double
        FailureRateMatrix.WilsonCI(30, 50, ciLow, ciHigh)
        pop.ContextOutcomes.Add("CONFIRMED", New FailureCellResult With {
            .VerdictTier = "CONFIRMED", .SampleSize = 50, .Failures = 30,
            .FailureRate = 0.6, .CiLow = ciLow, .CiHigh = ciHigh})
        ' Add lean-tag counts on NO-TRADE rows.
        pop.LeanContextCounts.Add("ALIGNED", 42)
        pop.LeanContextCounts.Add("(untagged)", 3)
        rep.Populations.Add(pop)

        Dim md As String = MarkdownReportWriter.BuildFullMarkdownForHarness(rep)

        ' Caption present + names D7 dates.
        Dim caption As Boolean = md.Contains("NOT comparable") AndAlso
                                 md.Contains("2026-06-24") AndAlso md.Contains("2026-07-21")
        ' Both sub-table headers present.
        Dim dirHdr As Boolean = md.Contains("(a) DIRECTIONAL verdicts")
        Dim leanHdr As Boolean = md.Contains("(b) NO-TRADE LEAN rows")
        ' Directional row rendered (CONFIRMED n=50).
        Dim dirRow As Boolean = md.Contains("**CONFIRMED**") AndAlso md.Contains("n=50")
        ' Lean rows rendered as counts, ordered by n descending (ALIGNED 42 before untagged 3).
        Dim leanAligned As Boolean = md.Contains("**ALIGNED**: n=42")
        Dim leanUntagged As Boolean = md.Contains("**(untagged)**: n=3")
        Dim iA As Integer = md.IndexOf("**ALIGNED**")
        Dim iU As Integer = md.IndexOf("**(untagged)**")
        Dim orderedDesc As Boolean = iA > 0 AndAlso iU > 0 AndAlso iA < iU

        Check("A34f §6 splits into (a) DIRECTIONAL + (b) NO-TRADE LEAN sub-tables with the D7 not-comparable caption",
              caption AndAlso dirHdr AndAlso leanHdr AndAlso dirRow AndAlso
              leanAligned AndAlso leanUntagged AndAlso orderedDesc,
              String.Format("caption={0} dirHdr={1} leanHdr={2} dirRow={3} leanA={4} leanU={5} order={6}",
                            caption, dirHdr, leanHdr, dirRow, leanAligned, leanUntagged, orderedDesc))
    End Sub

    ' ============================================================================
    ' A38 — WsHealthLog transition-only sidecar (ws_health.log W4 row)
    ' ============================================================================

    ' -- A38a: same state twice ⇒ ONE line; a change writes the second line -------
    Private Sub A38a_TransitionOnly()
        Dim path As String = WsHealthLog.GetPath()
        Try
            If File.Exists(path) Then File.Delete(path)
            WsHealthLog.ResetForTest()

            ' First transition call (no prior baseline) ⇒ writes.
            WsHealthLog.LogTransition("REST", "iid-A38a")
            Dim afterFirst As Integer = If(File.Exists(path), File.ReadAllLines(path).Length, 0)

            ' Same state again — MUST NOT append.
            WsHealthLog.LogTransition("REST", "iid-A38a")
            WsHealthLog.LogTransition("REST", "iid-A38a")
            Dim afterRepeat As Integer = If(File.Exists(path), File.ReadAllLines(path).Length, 0)

            ' A real transition — writes one more line.
            WsHealthLog.LogTransition("DEGRADED", "iid-A38a")
            Dim afterFlip As Integer = If(File.Exists(path), File.ReadAllLines(path).Length, 0)

            ' Another flip — writes; a further repeat of DEGRADED does NOT.
            WsHealthLog.LogTransition("OK", "iid-A38a")
            WsHealthLog.LogTransition("OK", "iid-A38a")
            Dim afterAllTicks As Integer = If(File.Exists(path), File.ReadAllLines(path).Length, 0)

            Dim ok As Boolean = afterFirst = 1 AndAlso afterRepeat = 1 AndAlso
                                 afterFlip = 2 AndAlso afterAllTicks = 3

            Check("A38a transition-only: same state twice → one line; each real transition adds one line",
                  ok,
                  String.Format("first={0} repeat={1} flip={2} allTicks={3}",
                                afterFirst, afterRepeat, afterFlip, afterAllTicks))
        Finally
            Try
                If File.Exists(path) Then File.Delete(path)
            Catch
            End Try
            WsHealthLog.ResetForTest()
        End Try
    End Sub

    ' -- A38b: LogStart writes unconditionally + shape "utc | state | iid" --------
    Private Sub A38b_StartLineAndFormat()
        Dim path As String = WsHealthLog.GetPath()
        Try
            If File.Exists(path) Then File.Delete(path)
            WsHealthLog.ResetForTest()

            ' LogStart ALWAYS writes (even if same state).
            WsHealthLog.LogStart("DOWN", "iid-A38b")
            WsHealthLog.LogStart("DOWN", "iid-A38b")   ' still writes — start is unconditional
            Dim lines() As String = File.ReadAllLines(path)
            Dim startsOk As Boolean = lines.Length = 2

            ' Shape of the first line: "<utc> | <state> | <iid>" (split on " | ").
            Dim parts() As String = lines(0).Split(New String() {" | "}, StringSplitOptions.None)
            Dim shapeOk As Boolean = parts.Length = 3 AndAlso
                                     parts(0).EndsWith("Z") AndAlso
                                     parts(1) = "DOWN" AndAlso
                                     parts(2) = "iid-A38b"

            ' After a LogStart, the last-logged baseline is set, so a matching LogTransition
            ' must be a no-op (proves the two entrypoints share one baseline).
            WsHealthLog.LogTransition("DOWN", "iid-A38b")
            Dim afterMatch As Integer = File.ReadAllLines(path).Length
            Dim baselineOk As Boolean = afterMatch = 2

            Check("A38b LogStart unconditional + shape (utc | state | iid) + shared transition baseline",
                  startsOk AndAlso shapeOk AndAlso baselineOk,
                  String.Format("starts={0} shape={1} baseline={2} row0='{3}'",
                                startsOk, shapeOk, baselineOk, If(lines.Length > 0, lines(0), "")))
        Finally
            Try
                If File.Exists(path) Then File.Delete(path)
            Catch
            End Try
            WsHealthLog.ResetForTest()
        End Try
    End Sub

    ' ============================================================================
    ' A39 — W6-4 ceiling audit (docs/w6-4-ceiling-audit-method-proposal.md §5)
    ' The instrument that gates W6-5/B1 + D3-D6 + W6-7 Tier-C spend. These fixtures
    ' exercise the standalone stats machinery against synthetic data — no CSV, no
    ' OHLC — so they run in the offline harness with no external dependency.
    ' ============================================================================

    ' -- A39a: loss decreases monotonically on a linearly-separable set; the fitter
    ' -- recovers the separating direction (weight on the informative dimension
    ' -- dominates the noise dimensions). Pins the L2Logistic contract used for
    ' -- the challenger model.
    Private Sub A39a_LogisticLossMonotoneAndDirection()
        ' 200 rows, 3 features: dim 0 is perfectly separable (positive class ~ N(+2,1),
        ' negative ~ N(-2,1)); dims 1-2 are pure noise. Deterministic seed.
        Dim rng As New Random(1)
        Dim n As Integer = 200
        Dim d As Integer = 3
        Dim X(n - 1, d - 1) As Double
        Dim y(n - 1) As Integer
        For i = 0 To n - 1
            y(i) = If(i < n \ 2, 0, 1)
            X(i, 0) = If(y(i) = 1, 2.0, -2.0) + GaussianStd(rng)
            X(i, 1) = GaussianStd(rng)
            X(i, 2) = GaussianStd(rng)
        Next
        Dim m = L2Logistic.Fit(X, y, lambda:=0.1, lr:=0.5, epochs:=200)

        ' Monotonicity: allow small floating-point wobble (< 1e-6) per step. Overall loss
        ' at last epoch must be materially below first epoch.
        Dim strictlyMonoUpTo As Integer = 0
        For i = 1 To m.LossTrace.Count - 1
            If m.LossTrace(i) > m.LossTrace(i - 1) + 0.000001 Then Exit For
            strictlyMonoUpTo = i
        Next
        Dim lossFirst As Double = m.LossTrace(0)
        Dim lossLast As Double = m.LossTrace(m.LossTrace.Count - 1)
        Dim monotoneOk As Boolean = strictlyMonoUpTo = m.LossTrace.Count - 1
        Dim descentOk As Boolean = lossLast < lossFirst * 0.5

        ' Direction: |w0| must dominate |w1|, |w2|; and w0 > 0 (positive class has +ve x0).
        Dim directionOk As Boolean = m.Weights(0) > 0 AndAlso
                                     Math.Abs(m.Weights(0)) > 2.0 * Math.Abs(m.Weights(1)) AndAlso
                                     Math.Abs(m.Weights(0)) > 2.0 * Math.Abs(m.Weights(2))

        Check("A39a logistic loss monotone + separating direction recovered",
              monotoneOk AndAlso descentOk AndAlso directionOk,
              String.Format("mono={0} first={1:F4} last={2:F4} w=[{3:F3},{4:F3},{5:F3}] b={6:F3}",
                            monotoneOk, lossFirst, lossLast, m.Weights(0), m.Weights(1), m.Weights(2), m.Bias))
    End Sub

    ' -- A39b: leakage canary. Shuffle the labels randomly; the AUC of the trained
    ' -- model on a held-out slice must be near 0.5. A deterministic run may sit a
    ' -- bit off centre on small n; we accept |auc - 0.5| < 0.15.
    Private Sub A39b_LabelShuffledAucIsHalf()
        Dim rng As New Random(2)
        Dim n As Integer = 400
        Dim d As Integer = 5
        Dim X(n - 1, d - 1) As Double
        Dim y(n - 1) As Integer
        For i = 0 To n - 1
            For j = 0 To d - 1
                X(i, j) = GaussianStd(rng)
            Next
            y(i) = rng.Next(0, 2)     ' pure noise labels
        Next
        Dim trainN As Integer = n \ 2
        Dim Xtr(trainN - 1, d - 1) As Double
        Dim ytr(trainN - 1) As Integer
        Dim Xte(n - trainN - 1, d - 1) As Double
        Dim yte(n - trainN - 1) As Integer
        For i = 0 To trainN - 1
            For j = 0 To d - 1
                Xtr(i, j) = X(i, j)
            Next
            ytr(i) = y(i)
        Next
        For i = trainN To n - 1
            For j = 0 To d - 1
                Xte(i - trainN, j) = X(i, j)
            Next
            yte(i - trainN) = y(i)
        Next
        Dim m = L2Logistic.Fit(Xtr, ytr, lambda:=1.0, epochs:=200)
        Dim scores = m.PredictAll(Xte)
        Dim auc As Double = AuditMetrics.Auc(scores, yte)
        Dim ok As Boolean = Math.Abs(auc - 0.5) < 0.15

        Check("A39b label-shuffled AUC ≈ 0.5 (leakage canary)", ok,
              String.Format("auc={0:F4}", auc))
    End Sub

    ' -- A39c: chronological split respected — the LATEST train timestamp must be
    ' -- STRICTLY earlier than (or equal to) the earliest test timestamp; the split
    ' -- covers ≥ minTestDays * 24 hours and touches ≥ 3 distinct hours (§3
    ' -- validity discipline flag).
    Private Sub A39c_ChronologicalSplitRespected()
        Dim bundles As New List(Of FeatureBundle)()
        Dim ts As New List(Of DateTime)()
        Dim start As New DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        ' 21 days of hourly rows — 24×21 = 504 rows, all sessions covered.
        For h = 0 To 21 * 24 - 1
            ts.Add(start.AddHours(h))
            bundles.Add(New FeatureBundle With {.RowIndex = h})
        Next
        Dim split = AuditMetrics.MakeChronologicalSplit(bundles, ts, minTestDays:=7)

        Dim maxTrainTs As DateTime = split.TrainIdx.Select(Function(i) ts(i)).Max()
        Dim minTestTs As DateTime = split.TestIdx.Select(Function(i) ts(i)).Min()
        Dim chronoOk As Boolean = maxTrainTs <= minTestTs
        Dim spanOk As Boolean = (split.TestEndUtc - split.TestStartUtc).TotalDays >= 6.5
        Dim hourCoverOk As Boolean = split.TestSpansSessions AndAlso
                                     split.TestIdx.Select(Function(i) ts(i).Hour).Distinct().Count() >= 3

        Check("A39c chronological split — no test row precedes any train row",
              chronoOk AndAlso spanOk AndAlso hourCoverOk,
              String.Format("chrono={0} span={1} hours={2} maxTrain={3:yyyy-MM-dd HH:mm} minTest={4:yyyy-MM-dd HH:mm}",
                            chronoOk, spanOk, hourCoverOk, maxTrainTs, minTestTs))
    End Sub

    ' -- A39d: block bootstrap blocks never straddle a session-hour boundary — every
    ' -- row assigned to the same block MUST share (UTC date, UTC hour). The block
    ' -- assignment is the mechanism that guarantees this at resample time.
    Private Sub A39d_BlockBootstrapNeverStraddlesHourBoundary()
        Dim ts As New List(Of DateTime)()
        Dim start As New DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        For m = 0 To 999
            ts.Add(start.AddMinutes(m))    ' 1000 min ≈ 17 hours across 2 days
        Next
        Dim blocks = AuditMetrics.AssignBlocks(ts)

        ' Group rows by assigned block, assert every group's dates+hours are identical.
        Dim byBlock As New Dictionary(Of Integer, List(Of DateTime))()
        For i = 0 To ts.Count - 1
            Dim k As Integer = blocks(i)
            Dim lst As List(Of DateTime) = Nothing
            If Not byBlock.TryGetValue(k, lst) Then
                lst = New List(Of DateTime)()
                byBlock(k) = lst
            End If
            lst.Add(ts(i))
        Next
        Dim clean As Boolean = True
        Dim offender As String = ""
        For Each kv In byBlock
            Dim first = kv.Value(0)
            For Each t In kv.Value
                If t.Date <> first.Date OrElse t.Hour <> first.Hour Then
                    clean = False
                    offender = String.Format("block {0}: {1:yyyy-MM-dd HH} vs {2:yyyy-MM-dd HH}",
                                             kv.Key, first, t)
                    Exit For
                End If
            Next
            If Not clean Then Exit For
        Next

        ' Also assert the block COUNT equals the distinct (date, hour) count.
        Dim distinctHours As Integer = ts.Select(Function(t) t.Date.ToString("yyyyMMdd") & t.Hour.ToString()).
                                          Distinct().Count()
        Dim countOk As Boolean = byBlock.Count = distinctHours

        Check("A39d block-bootstrap blocks never straddle a session-hour boundary",
              clean AndAlso countOk,
              String.Format("clean={0} blocks={1} distinctHours={2} offender={3}",
                            clean, byBlock.Count, distinctHours, offender))
    End Sub

    ' -- A39e: informational Absorption*, un-armed AggrVel, AND TargetCapReason provably
    ' -- absent from the decision-model design matrix. TargetCapReason is Step-5b OUTPUT
    ' -- (placed-geometry bucket the arbitration emitted, not a scoring input) — coordinator-
    ' -- demoted 2026-07-23 to prevent geometry-difficulty leakage into the challenger's ΔAUC.
    ' -- The Real CsvFeatureBuilder is the shipped writer of TargetCapReason into InfoCategoricals;
    ' -- this fixture builds bundles the way the loader does (scored dictionary carries the
    ' -- 23-name post-correction list; info dictionary carries TargetCapReason) so the schema
    ' -- fit sees the same shape as production.
    Private Sub A39e_InformationalExtrasAbsentFromDecisionMatrix()
        Dim bundles As New List(Of FeatureBundle)()
        For i = 0 To 49
            Dim fb As New FeatureBundle()
            fb.SessionHour = i Mod 24
            fb.Regime = "TRENDING_UP"
            fb.ScoredCategoricals("ROCSlope") = If(i Mod 2 = 0, "RISING", "FALLING")
            fb.ScoredNumerics("ATR") = 40.0 + i
            ' Populate informational fields — the audit MUST NOT let these into X.
            fb.InfoCategoricals("AbsorptionSignal") = "ABSORB_ABOVE"
            fb.InfoCategoricals("TargetCapReason") = If(i Mod 3 = 0, "swing", If(i Mod 3 = 1, "hvn", "none"))
            fb.InfoNumerics("AbsorptionRatio") = 1.5
            fb.InfoNumerics("AbsorptionAggrUsd") = 250000
            fb.InfoNumerics("AbsorptionPullFrac") = 0.7
            fb.InfoNumerics("AbsorptionLevel") = 65000
            ' Populate AggrVel; test with IncludeAggrVel = False (un-armed population).
            fb.AggrVelSignal = "BURST_BUY"
            fb.AggrVelBurstRatio = 3.0
            fb.AggrVelNet = 12345.0
            bundles.Add(fb)
        Next

        Dim schema = FeatureMatrix.FitSchema(bundles, includeAggrVel:=False)
        Dim banned As String() = {"Absorption", "AggrVel", "TargetCapReason"}
        Dim clean As Boolean = True
        Dim offender As String = ""
        For Each col In schema.Columns
            For Each b In banned
                If col.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    clean = False
                    offender = col
                    Exit For
                End If
            Next
            If Not clean Then Exit For
        Next
        For Each nm In schema.ScoredCategoricalNames
            For Each b In banned
                If nm.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    clean = False
                    offender = "cat:" & nm
                    Exit For
                End If
            Next
            If Not clean Then Exit For
        Next
        For Each nm In schema.ScoredNumericNames
            For Each b In banned
                If nm.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0 Then
                    clean = False
                    offender = "num:" & nm
                    Exit For
                End If
            Next
            If Not clean Then Exit For
        Next

        ' Also verify the transformed X has no non-zero columns at banned positions
        ' — the schema drives everything, so column-name absence is the primary proof;
        ' this is belt-and-braces.
        Dim X = FeatureMatrix.Transform(schema, bundles)
        Dim xClean As Boolean = X.GetLength(1) = schema.Columns.Count

        Check("A39e informational Absorption / AggrVel-un-armed / TargetCapReason absent from decision matrix",
              clean AndAlso xClean,
              String.Format("clean={0} offender={1} cols={2}", clean, offender, schema.Columns.Count))
    End Sub

    ' Standard-normal sample via Box-Muller (uniform → N(0,1)).
    Private Function GaussianStd(rng As Random) As Double
        Dim u1 As Double = 1.0 - rng.NextDouble()
        Dim u2 As Double = 1.0 - rng.NextDouble()
        Return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
    End Function

    ' =======================================================================
    ' A40 — fee-aware min-move floor (v62).
    ' docs/fee-aware-min-move-proposal.md §5 acceptance.
    '
    ' The flat scoring.min_tradeable_move_pct key is retired; the floor is COMPOSED by
    ' one shared resolver, TradeCostSettings.EffectiveMinMovePct = round-trip fee for the
    ' configured style + the trader's minimum acceptable NET move. At the shipped defaults
    ' (maker 1.5 bps, maker_maker, min_net 0.0005) that is 0.0003 + 0.0005 = 0.0008 — the
    ' retired v35 floor EXACTLY, which is why this build is not a dataset boundary and why
    ' every A13 number is unchanged. A40b is the load-bearing pin for that claim.
    ' =======================================================================

    ' -- A40a: resolver composition across all three styles + non-default bps ------
    Private Sub A40a_ResolverComposition()
        ' Default: maker_maker at 1.5/3.5 bps + min_net 0.0005 ⇒ 0.0008 EXACTLY.
        ' Asserted to 1e-12: the whole no-dataset-boundary argument rests on this being
        ' the v35 literal, not merely close to it.
        Dim def As New TradeCostSettings()
        Dim okDefault As Boolean = Math.Abs(def.RoundTripFeePct - 0.0003) < 0.000000000001 AndAlso
                                   Math.Abs(def.EffectiveMinMovePct - 0.0008) < 0.000000000001

        ' maker_taker: 1.5 + 3.5 = 5 bps ⇒ 0.0005; + min_net ⇒ 0.0010.
        Dim mt As New TradeCostSettings() With {.RoundTripStyle = "maker_taker"}
        Dim okMakerTaker As Boolean = Math.Abs(mt.RoundTripFeePct - 0.0005) < 0.000000000001 AndAlso
                                      Math.Abs(mt.EffectiveMinMovePct - 0.0010) < 0.000000000001

        ' taker_taker: 2 × 3.5 = 7 bps ⇒ 0.0007; + min_net ⇒ 0.0012.
        Dim tt As New TradeCostSettings() With {.RoundTripStyle = "taker_taker"}
        Dim okTakerTaker As Boolean = Math.Abs(tt.RoundTripFeePct - 0.0007) < 0.000000000001 AndAlso
                                      Math.Abs(tt.EffectiveMinMovePct - 0.0012) < 0.000000000001

        ' Non-default bps track through: maker 2.0 / taker 4.0.
        Dim hi As New TradeCostSettings() With {.MakerFeeBps = 2.0, .TakerFeeBps = 4.0,
                                                .RoundTripStyle = "maker_taker", .MinNetMovePct = 0.001}
        Dim okBps As Boolean = Math.Abs(hi.RoundTripFeePct - 0.0006) < 0.000000000001 AndAlso
                               Math.Abs(hi.EffectiveMinMovePct - 0.0016) < 0.000000000001

        ' Style parsing is case/whitespace tolerant, and an unrecognised style falls back
        ' to maker_maker rather than to zero cost (fail-safe = the conservative floor).
        Dim loud As New TradeCostSettings() With {.RoundTripStyle = "  TAKER_TAKER "}
        Dim junk As New TradeCostSettings() With {.RoundTripStyle = "carrier_pigeon"}
        Dim nul  As New TradeCostSettings() With {.RoundTripStyle = Nothing}
        Dim okFallback As Boolean = Math.Abs(loud.RoundTripFeePct - 0.0007) < 0.000000000001 AndAlso
                                    Math.Abs(junk.RoundTripFeePct - 0.0003) < 0.000000000001 AndAlso
                                    Math.Abs(nul.RoundTripFeePct - 0.0003) < 0.000000000001

        ' The two DERIVED properties must never round-trip into settings.json — the UI's
        ' operational save serialises the whole POCO, and the tweaker prompt inlines the
        ' whole file, so a leaked computed key would become a phantom tunable.
        Dim json As String = System.Text.Json.JsonSerializer.Serialize(
            New EngineSettings(),
            New System.Text.Json.JsonSerializerOptions With {.WriteIndented = True})
        Dim okSerialise As Boolean =
            json.Contains("""trade_costs""") AndAlso
            json.Contains("""min_net_move_pct""") AndAlso
            json.Contains("""round_trip_style""") AndAlso
            Not json.Contains("EffectiveMinMovePct") AndAlso
            Not json.Contains("RoundTripFeePct")

        Check("A40a resolver composition (maker_maker default == 0.0008 exactly; maker_taker/taker_taker; bps track; unrecognised style ⇒ maker_maker; derived props never serialise)",
              okDefault AndAlso okMakerTaker AndAlso okTakerTaker AndAlso okBps AndAlso
              okFallback AndAlso okSerialise,
              String.Format(CultureInfo.InvariantCulture,
                            "def={0} ({1:R}) mt={2} tt={3} bps={4} fallback={5} serialise={6}",
                            okDefault, def.EffectiveMinMovePct, okMakerTaker, okTakerTaker,
                            okBps, okFallback, okSerialise))
    End Sub

    ' -- A40b: defaults byte-identical to the v61 flat floor -- THE load-bearing pin ---
    ' The v61 cfg is reconstructed honestly rather than circularly: zero fees + min_net
    ' 0.0008 IS the retired semantics (a flat 0.0008 floor with no fee model). The v62
    ' default cfg reaches the same floor by a different composition (0.0003 + 0.0005), so
    ' identical engine output across the A13 gate case set — including a BELOW_MIN_MOVE
    ' case — proves the restructure is behaviour-neutral at ship.
    Private Sub A40b_DefaultsByteIdenticalToV61Floor()
        Dim cfgV62 = BuildA8Cfg(fundingBoost:=0)     ' POCO defaults ⇒ composed 0.0008

        Dim cfgV61 = BuildA8Cfg(fundingBoost:=0)     ' the retired flat-key semantics
        cfgV61.Scoring.TradeCosts.MakerFeeBps = 0.0
        cfgV61.Scoring.TradeCosts.TakerFeeBps = 0.0
        cfgV61.Scoring.TradeCosts.MinNetMovePct = 0.0008

        Dim sameFloor As Boolean =
            Math.Abs(cfgV61.Scoring.TradeCosts.EffectiveMinMovePct -
                     cfgV62.Scoring.TradeCosts.EffectiveMinMovePct) < 0.000000000001

        ' Case set: A13a (gate fires on a small fallback target — the BELOW_MIN_MOVE case),
        ' A13b (clears the floor), A13c (gate fires on a near structural placement),
        ' plus a high-ATR control well clear of the floor.
        Dim allMatch As Boolean = True
        Dim firstDiff As String = ""
        Dim sawBelowMin As Boolean = False

        For Each c In New(Name As String, Atr As Double, Swing As Double)() {
                ("A13a low-ATR", 13.0, 0.0),
                ("A13b tradeable-ATR", 30.0, 0.0),
                ("A13c near-swing", 100.0, 61970.0),
                ("control high-ATR", 100.0, 0.0)}

            Dim r61 = BuildGateIndicators(atr:=c.Atr, price:=62000)
            Dim r62 = BuildGateIndicators(atr:=c.Atr, price:=62000)
            If c.Swing > 0 Then
                r61.SwingTargetShort = c.Swing
                r62.SwingTargetShort = c.Swing
            End If

            Dim v61 = ScoringEngine.Calculate(r61, PositionState.None, BuildA8Norms(), cfgV61)
            Dim v62 = ScoringEngine.Calculate(r62, PositionState.None, BuildA8Norms(), cfgV62)

            If v62.VerdictContext = "BELOW_MIN_MOVE" Then sawBelowMin = True

            Dim match As Boolean =
                v61.Verdict = v62.Verdict AndAlso
                v61.Confidence = v62.Confidence AndAlso
                v61.VerdictContext = v62.VerdictContext AndAlso
                Math.Abs(v61.AdjustedLongTarget - v62.AdjustedLongTarget) < 0.000001 AndAlso
                Math.Abs(v61.AdjustedShortTarget - v62.AdjustedShortTarget) < 0.000001 AndAlso
                v61.TargetCapReasonLong = v62.TargetCapReasonLong AndAlso
                v61.TargetCapReasonShort = v62.TargetCapReasonShort
            If Not match Then
                allMatch = False
                If firstDiff = "" Then
                    firstDiff = String.Format(CultureInfo.InvariantCulture,
                        "{0}: v61 '{1}'/{2}/tgt {3:F2} vs v62 '{4}'/{5}/tgt {6:F2}",
                        c.Name, v61.Verdict, v61.VerdictContext, v61.AdjustedShortTarget,
                        v62.Verdict, v62.VerdictContext, v62.AdjustedShortTarget)
                End If
            End If
        Next

        Check("A40b v62 defaults byte-identical to the retired v61 flat 0.0008 floor through the REAL Calculate() (incl. a BELOW_MIN_MOVE case)",
              sameFloor AndAlso allMatch AndAlso sawBelowMin,
              String.Format("sameFloor={0} allMatch={1} sawBelowMin={2} firstDiff='{3}'",
                            sameFloor, allMatch, sawBelowMin, firstDiff))
    End Sub

    ' -- A40c: turning the knob moves the gate (and clears the eval re-walk epsilon) ---
    ' A13b's placed short target sits 52.5 from entry and clears the default floor 49.6.
    ' min_net 0.0005 → 0.0010 lifts the composed floor to (0.0003 + 0.0010) × 62000 = 80.6,
    ' so the same marginal directional flips to NO TRADE / BELOW_MIN_MOVE.
    '
    ' The eval side: LivePerformanceTracker stores the COMPOSED value as its
    ' _floorPctInEffect and re-walks when it differs from the cache's stored floor by more
    ' than 1e-7. Driving that re-walk end-to-end needs InitialiseAsync + a live OHLC fetch
    ' (the same live-network boundary that keeps A16–A31 stubbed), so what is pinned here
    ' is the input the trigger consumes: the composed delta produced by a knob turn is
    ' non-zero and comfortably exceeds that epsilon.
    Private Sub A40c_KnobChangeMovesTheGate()
        Dim cfgBase = BuildA8Cfg(fundingBoost:=0)
        Dim vBase = ScoringEngine.Calculate(BuildGateIndicators(atr:=30, price:=62000),
                                            PositionState.None, BuildA8Norms(), cfgBase)
        Dim okBaseStands As Boolean = Not vBase.Verdict.StartsWith("NO TRADE") AndAlso
                                      vBase.VerdictContext <> "BELOW_MIN_MOVE"

        Dim cfgRaised = BuildA8Cfg(fundingBoost:=0)
        cfgRaised.Scoring.TradeCosts.MinNetMovePct = 0.0010
        Dim vRaised = ScoringEngine.Calculate(BuildGateIndicators(atr:=30, price:=62000),
                                              PositionState.None, BuildA8Norms(), cfgRaised)
        Dim okRaisedVetoes As Boolean = vRaised.Verdict = "NO TRADE" AndAlso
                                        vRaised.VerdictContext = "BELOW_MIN_MOVE"

        ' The composed floor moved by exactly the knob delta, and clears the tracker's
        ' 1e-7 re-walk epsilon.
        Dim delta As Double = cfgRaised.Scoring.TradeCosts.EffectiveMinMovePct -
                              cfgBase.Scoring.TradeCosts.EffectiveMinMovePct
        Dim okRewalkTrigger As Boolean = Math.Abs(delta - 0.0005) < 0.000000000001 AndAlso
                                         Math.Abs(delta) > 0.0000001

        Check("A40c min_net 0.0005→0.0010 flips a marginal directional to NO TRADE / BELOW_MIN_MOVE; composed delta clears the eval re-walk epsilon",
              okBaseStands AndAlso okRaisedVetoes AndAlso okRewalkTrigger,
              String.Format(CultureInfo.InvariantCulture,
                            "base='{0}'/{1} raised='{2}'/{3} delta={4:R}",
                            vBase.Verdict, vBase.VerdictContext,
                            vRaised.Verdict, vRaised.VerdictContext, delta))
    End Sub

    ' -- A40d: HC26 prefix fence + retired key unresolvable + what-if whitelist split --
    Private Sub A40d_Hc26FenceAndWhatIfWhitelist()
        ' A settings tree at v62 shape: the trade_costs block present, the retired flat key
        ' ABSENT (so it can only fail the C-6 resolve check), plus a sibling scoring tunable.
        Dim s As String = "{""version"":62,""scoring"":{""verdict_med_pct"":0.53," &
                          """trade_costs"":{""maker_fee_bps"":1.5,""taker_fee_bps"":3.5," &
                          """round_trip_style"":""maker_maker"",""min_net_move_pct"":0.0005}}}"

        Dim rMaker = SettingsDiffApplier.Validate(OneDiff("scoring.trade_costs.maker_fee_bps", "1.5", "2.0"), s, 3)
        Dim rTaker = SettingsDiffApplier.Validate(OneDiff("scoring.trade_costs.taker_fee_bps", "3.5", "4.0"), s, 3)
        Dim rStyle = SettingsDiffApplier.Validate(OneDiff("scoring.trade_costs.round_trip_style", """maker_maker""", """taker_taker"""), s, 3)
        Dim rNet   = SettingsDiffApplier.Validate(OneDiff("scoring.trade_costs.min_net_move_pct", "0.0005", "0.0010"), s, 3)
        Dim okAllFour As Boolean = Not rMaker.IsValid AndAlso Not rTaker.IsValid AndAlso
                                   Not rStyle.IsValid AndAlso Not rNet.IsValid

        ' The retired key is applier-UNRESOLVABLE (C-6), NOT fragment-banned — the v47-F1
        ' snapshot-poisoning lesson. Assert both halves: rejected, and rejected for the
        ' resolve reason rather than by a fragment/prefix guard.
        Dim rRetired = SettingsDiffApplier.Validate(OneDiff("scoring.min_tradeable_move_pct", "0.0008", "0.0010"), s, 3)
        Dim okRetired As Boolean = Not rRetired.IsValid AndAlso
                                   rRetired.ErrorReason.Contains("does not resolve") AndAlso
                                   Not rRetired.ErrorReason.Contains("banned fragment")

        ' Prefix-safety: a sibling scoring.* tunable is untouched by the fence.
        Dim rSib = SettingsDiffApplier.Validate(OneDiff("scoring.verdict_med_pct", "0.53", "0.55"), s, 3)

        ' What-if surface: min_net_move_pct is sweepable, the fee/style keys are not, and
        ' the retired path no longer parses.
        Dim wlNet As Boolean = WhatIfOverlay.Whitelist.Contains("scoring.trade_costs.min_net_move_pct")
        Dim wlFee As Boolean = WhatIfOverlay.Whitelist.Contains("scoring.trade_costs.maker_fee_bps")
        Dim wlOld As Boolean = WhatIfOverlay.Whitelist.Contains("scoring.min_tradeable_move_pct")

        Dim parsedNet As Boolean = False
        Try
            Dim ov = WhatIfOverlay.Parse("{""scoring"":{""trade_costs"":{""min_net_move_pct"":0.0007}}}")
            parsedNet = ov.Knobs.Count = 1 AndAlso
                        ov.Knobs(0).Path = "scoring.trade_costs.min_net_move_pct"
        Catch
        End Try

        Dim rejectedFee As Boolean = False
        Try
            WhatIfOverlay.Parse("{""scoring"":{""trade_costs"":{""maker_fee_bps"":2.0}}}")
        Catch ex As WhatIfOverlayError
            rejectedFee = True
        End Try

        Dim okWhatIf As Boolean = wlNet AndAlso Not wlFee AndAlso Not wlOld AndAlso
                                  parsedNet AndAlso rejectedFee

        Check("A40d HC26 rejects all four trade_costs keys; retired key unresolvable (not fragment-banned); sibling scoring tunable passes; what-if takes min_net only",
              okAllFour AndAlso okRetired AndAlso rSib.IsValid AndAlso okWhatIf,
              String.Format("fence(mk={0},tk={1},st={2},net={3}) retired={4}/'{5}' sib={6} whatif(wlNet={7},wlFee={8},wlOld={9},parse={10},rejFee={11})",
                            rMaker.IsValid, rTaker.IsValid, rStyle.IsValid, rNet.IsValid,
                            rRetired.IsValid, rRetired.ErrorReason, rSib.IsValid,
                            wlNet, wlFee, wlOld, parsedNet, rejectedFee))
    End Sub

    ' -- A40e: a min_net overlay round-trips through the What-If settings seam ---------
    ' The A36f linked-seam pattern: WhatIfSettings.BuildCellSettings must actually reach the
    ' new nested POCO field, and the resolver must recompose off it — otherwise the runner
    ' would sweep a knob the replay's Step 5c never reads.
    Private Sub A40e_MinNetOverlayRoundTripsThroughWhatIf()
        Dim overlay As New Dictionary(Of String, Double) From {
            {"scoring.trade_costs.min_net_move_pct", 0.0010}
        }

        Dim tmp As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                                    "wifsettings-a40e-" & Guid.NewGuid().ToString("N") & ".json")
        Try
            System.IO.File.WriteAllText(tmp,
                "{""version"":62," &
                """scoring"":{""atr_target_multiplier"":1.75,""atr_stop_multiplier"":1.6," &
                """trade_costs"":{""maker_fee_bps"":1.5,""taker_fee_bps"":3.5," &
                """round_trip_style"":""maker_maker"",""min_net_move_pct"":0.0005}}}")

            Dim wis As New WhatIfSettings(tmp)

            ' The live read (used for constraint resolution + the report's pinned-vs-inherited
            ' marking) sees the file value, not the POCO default.
            Dim okLive As Boolean = Math.Abs(wis.LiveValueOf("scoring.trade_costs.min_net_move_pct") - 0.0005) < 0.000000000001

            Dim cellCfg = wis.BuildCellSettings(overlay)
            Dim okApply As Boolean =
                Math.Abs(cellCfg.Scoring.TradeCosts.MinNetMovePct - 0.0010) < 0.000000000001 AndAlso
                Math.Abs(cellCfg.Scoring.TradeCosts.MakerFeeBps - 1.5) < 0.000000000001

            ' The resolver recomposes off the overlaid value — 0.0003 + 0.0010 = 0.0013 —
            ' which is what the replay's Step 5c mirror multiplies by the row price.
            Dim okCompose As Boolean =
                Math.Abs(cellCfg.Scoring.TradeCosts.EffectiveMinMovePct - 0.0013) < 0.000000000001

            Check("A40e min_net overlay round-trips through WhatIfSettings.BuildCellSettings and recomposes the floor (0.0003 + 0.0010 = 0.0013)",
                  okLive AndAlso okApply AndAlso okCompose,
                  String.Format(CultureInfo.InvariantCulture,
                                "live={0} apply={1} compose={2} floor={3:R}",
                                okLive, okApply, okCompose, cellCfg.Scoring.TradeCosts.EffectiveMinMovePct))
        Finally
            Try : System.IO.File.Delete(tmp) : Catch : End Try
        End Try
    End Sub

    ' =======================================================================
    ' A41 — eval net-EV rider (docs/fee-aware-min-move-proposal.md §6.1).
    '
    ' Analysis-only. WhatIfReplay.ComputeEvAtr now subtracts a per-row round-trip fee drag
    ' (round_trip_fee_pct × entry_price / ATR) from EVERY outcome arm — SUCCESS, ADVERSE_HIT/
    ' AMBIGUOUS, and WINDOW_EXPIRED mark-to-end. Every resolved trade pays the round trip:
    ' the drag never depends on whether the target was hit. WhatIfReport labels the ranking
    ' as "net of fees" and adds a dispersion column (σ = population std of the per-trade
    ' EvAtr samples) beside the EV mean.
    '
    ' ComputeEvAtr is Private Shared; the harness reaches it through the same seam A30 uses
    ' — WhatIfReplay.RunCell drives the replay end-to-end and emits WhatIfEvSample.EvAtr —
    ' so these fixtures pin the observable output of the shipped call graph, no visibility
    ' changes required.
    '
    ' The maker→taker emergency loss-arm delta (§6.1 optional toggle) is NOT built and no
    ' fixture pins its absence — recorded as a deliberate deviation in the spec-back addendum.
    ' =======================================================================

    ' -- A41a: fee drag subtracted from SUCCESS + ADVERSE_HIT arms ------
    ' Two single-row replays under default cfg (maker/maker 1.5 bps ⇒ RoundTripFeePct 0.0003).
    ' At price 62000 / ATR 40 the drag is 0.0003 × 62000 / 40 = 0.465 ATR units. Bar geometry
    ' is designed to force each arm deterministically via FailureRateMatrix.WalkBars; the
    ' expected net EV is measured against whatever placed levels the arbitration emits (read
    ' back off the ReplayedRow), so the fixture never re-derives ComputeSideLevels.
    Private Sub A41a_NetEvSubtractsFeeDragOnSuccessAndStopArms()
        Dim cfg As New EngineSettings()   ' default trade_costs ⇒ 1.5 bps maker/maker
        Dim feeDragAtr As Double = cfg.Scoring.TradeCosts.RoundTripFeePct * 62000.0 / 40.0

        ' SUCCESS: first bar's High clears the placed target while Low sits comfortably above
        ' the placed stop (SwingStopLong 61950 ⇒ Low 61980 is safe). WalkBars ⇒ SUCCESS.
        Dim rowSucc = BuildWhatIfRow()
        rowSucc.ForwardBars(15) = New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = rowSucc.Timestamp.AddMinutes(1),
                              .Open = 62000, .High = 63000, .Low = 61980, .Close = 62500}}
        Dim runSucc = WhatIfReplay.RunCell(New List(Of CsvRow) From {rowSucc}, cfg, 15, keepRows:=True)
        Dim repSucc = runSucc.ReplayedRows(0)
        Dim grossSucc As Double = Math.Abs(repSucc.PlacedTargetLong - rowSucc.Price) / rowSucc.ATR
        Dim netSucc As Double = runSucc.EvSamples(0).EvAtr
        Dim okSucc As Boolean = Math.Abs(netSucc - (grossSucc - feeDragAtr)) < 0.000000000001

        ' ADVERSE_HIT: first bar's Low hits the placed stop while High stays below the placed
        ' target (SwingTargetLong 62070 ⇒ High 62050 is safe). WalkBars ⇒ ADVERSE_HIT.
        Dim rowAdv = BuildWhatIfRow()
        rowAdv.ForwardBars(15) = New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = rowAdv.Timestamp.AddMinutes(1),
                              .Open = 62000, .High = 62050, .Low = 61800, .Close = 61850}}
        Dim runAdv = WhatIfReplay.RunCell(New List(Of CsvRow) From {rowAdv}, cfg, 15, keepRows:=True)
        Dim repAdv = runAdv.ReplayedRows(0)
        Dim grossAdv As Double = -Math.Abs(rowAdv.Price - repAdv.PlacedStopLong) / rowAdv.ATR
        Dim netAdv As Double = runAdv.EvSamples(0).EvAtr
        Dim okAdv As Boolean = Math.Abs(netAdv - (grossAdv - feeDragAtr)) < 0.000000000001

        Check("A41a per-row fee drag subtracted on SUCCESS + ADVERSE_HIT arms (net = gross − round_trip_fee_pct × price / ATR)",
              okSucc AndAlso okAdv,
              String.Format(CultureInfo.InvariantCulture,
                            "drag={0:R}  succ: gross={1:R} net={2:R} Δ={3:R}  adv: gross={4:R} net={5:R} Δ={6:R}",
                            feeDragAtr, grossSucc, netSucc, netSucc - (grossSucc - feeDragAtr),
                            grossAdv, netAdv, netAdv - (grossAdv - feeDragAtr)))
    End Sub

    ' -- A41b: fee drag on WINDOW_EXPIRED arm (unconditional) ------
    ' Two bars that stay strictly inside both barriers ⇒ WalkBars returns WINDOW_EXPIRED. The
    ' mark-to-end reference is the last bar's Close, and the drag applies unchanged: a
    ' horizon-close pays the same round trip as a stop-out or a target touch.
    Private Sub A41b_WindowExpiredArmAlsoPaysFeeDrag()
        Dim cfg As New EngineSettings()
        Dim feeDragAtr As Double = cfg.Scoring.TradeCosts.RoundTripFeePct * 62000.0 / 40.0

        Dim rowExp = BuildWhatIfRow()
        Dim endClose As Double = 62030
        rowExp.ForwardBars(15) = New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = rowExp.Timestamp.AddMinutes(1),
                              .Open = 62000, .High = 62050, .Low = 61980, .Close = 62020},
            New OhlcBar With {.CloseTime = rowExp.Timestamp.AddMinutes(2),
                              .Open = 62020, .High = 62055, .Low = 61985, .Close = endClose}}
        Dim runExp = WhatIfReplay.RunCell(New List(Of CsvRow) From {rowExp}, cfg, 15, keepRows:=True)
        Dim grossExp As Double = (endClose - rowExp.Price) / rowExp.ATR
        Dim netExp As Double = runExp.EvSamples(0).EvAtr
        Dim okExp As Boolean = Math.Abs(netExp - (grossExp - feeDragAtr)) < 0.000000000001

        Check("A41b WINDOW_EXPIRED arm also pays the round-trip fee drag (drag is unconditional across all three outcome arms)",
              okExp,
              String.Format(CultureInfo.InvariantCulture,
                            "gross={0:R} net={1:R} drag={2:R} Δ={3:R}",
                            grossExp, netExp, feeDragAtr, netExp - (grossExp - feeDragAtr)))
    End Sub

    ' -- A41c: fees-zero cfg ⇒ net ≡ gross (the regression identity) ------
    ' A cfg with maker_fee_bps = taker_fee_bps = 0 has RoundTripFeePct = 0, so the drag term
    ' collapses to zero across every arm. That IS the pre-rider (gross) semantics. Byte
    ' identity to 1e-12 across SUCCESS + ADVERSE_HIT + WINDOW_EXPIRED proves the rider is a
    ' pure add-on: turn off fees, the rider disappears — no accidental drift into other arms.
    Private Sub A41c_FeesZeroCfgIsByteIdenticalToGross()
        Dim cfg As New EngineSettings()
        cfg.Scoring.TradeCosts.MakerFeeBps = 0.0
        cfg.Scoring.TradeCosts.TakerFeeBps = 0.0
        ' min_net stays at its default (0.0005) — the composed floor drops to 0.0005, still
        ' well below the ~1.1%-of-price target distance, so directional gating is unchanged.

        ' SUCCESS
        Dim rowSucc = BuildWhatIfRow()
        rowSucc.ForwardBars(15) = New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = rowSucc.Timestamp.AddMinutes(1),
                              .Open = 62000, .High = 63000, .Low = 61980, .Close = 62500}}
        Dim runSucc = WhatIfReplay.RunCell(New List(Of CsvRow) From {rowSucc}, cfg, 15, keepRows:=True)
        Dim repSucc = runSucc.ReplayedRows(0)
        Dim grossSucc As Double = Math.Abs(repSucc.PlacedTargetLong - rowSucc.Price) / rowSucc.ATR
        Dim okSucc As Boolean = Math.Abs(runSucc.EvSamples(0).EvAtr - grossSucc) < 0.000000000001

        ' ADVERSE_HIT
        Dim rowAdv = BuildWhatIfRow()
        rowAdv.ForwardBars(15) = New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = rowAdv.Timestamp.AddMinutes(1),
                              .Open = 62000, .High = 62050, .Low = 61800, .Close = 61850}}
        Dim runAdv = WhatIfReplay.RunCell(New List(Of CsvRow) From {rowAdv}, cfg, 15, keepRows:=True)
        Dim repAdv = runAdv.ReplayedRows(0)
        Dim grossAdv As Double = -Math.Abs(rowAdv.Price - repAdv.PlacedStopLong) / rowAdv.ATR
        Dim okAdv As Boolean = Math.Abs(runAdv.EvSamples(0).EvAtr - grossAdv) < 0.000000000001

        ' WINDOW_EXPIRED
        Dim rowExp = BuildWhatIfRow()
        Dim endClose As Double = 62030
        rowExp.ForwardBars(15) = New List(Of OhlcBar) From {
            New OhlcBar With {.CloseTime = rowExp.Timestamp.AddMinutes(1),
                              .Open = 62000, .High = 62050, .Low = 61980, .Close = 62020},
            New OhlcBar With {.CloseTime = rowExp.Timestamp.AddMinutes(2),
                              .Open = 62020, .High = 62055, .Low = 61985, .Close = endClose}}
        Dim runExp = WhatIfReplay.RunCell(New List(Of CsvRow) From {rowExp}, cfg, 15, keepRows:=True)
        Dim grossExp As Double = (endClose - rowExp.Price) / rowExp.ATR
        Dim okExp As Boolean = Math.Abs(runExp.EvSamples(0).EvAtr - grossExp) < 0.000000000001

        Check("A41c fees-zero cfg (maker=taker=0) ⇒ net EV byte-identical to gross across SUCCESS + ADVERSE_HIT + WINDOW_EXPIRED (regression identity)",
              okSucc AndAlso okAdv AndAlso okExp,
              String.Format(CultureInfo.InvariantCulture,
                            "succ Δ={0:R} adv Δ={1:R} exp Δ={2:R}",
                            runSucc.EvSamples(0).EvAtr - grossSucc,
                            runAdv.EvSamples(0).EvAtr - grossAdv,
                            runExp.EvSamples(0).EvAtr - grossExp))
    End Sub

    ' -- A41d: report ranking table carries net-of-fees label + σ dispersion column ------
    ' StdPop pinned against a known two-sample set (values +1, −1 ⇒ mean 0, population std 1).
    ' The rendered ranking table (multi-cell path — AppendGridRanking is skipped when
    ' GridCellCount == 1) is asserted to disclose net-of-fees orientation, the not-comparable
    ' note, and the new σ column header — all three are E1-style rendered-semantics
    ' disclosures the caller must be able to see at a glance.
    Private Sub A41d_ReportCarriesNetOfFeesLabelAndDispersionColumn()
        ' Population std of {+1, −1} = sqrt(((1-0)^2 + (-1-0)^2)/2) = 1.0 exactly.
        Dim samples As New List(Of WhatIfEvSample) From {
            New WhatIfEvSample With {.Timestamp = New DateTime(2026, 7, 10, 14, 0, 0, DateTimeKind.Utc),
                                     .SessionName = "NY", .Resolution = 1, .Tier = "LONG", .EvAtr = 1.0},
            New WhatIfEvSample With {.Timestamp = New DateTime(2026, 7, 11, 14, 0, 0, DateTimeKind.Utc),
                                     .SessionName = "NY", .Resolution = 1, .Tier = "LONG", .EvAtr = -1.0}}
        Dim statFull = WhatIfEvStat.Of_(samples.Select(Function(s) s.EvAtr))
        Dim okStdPop As Boolean = Math.Abs(statFull.StdPop - 1.0) < 0.000000000001

        Dim cfg As New EngineSettings()
        Dim cell0 As New WhatIfGridCell With {.Index = 0, .Cell = New Dictionary(Of String, Double)(),
                                              .EvalWindowBars = 15, .EvSamples = samples,
                                              .DirectionalCount = 2, .BelowMinMoveExcluded = 0,
                                              .EvFull = statFull, .EvSel = statFull, .EvHold = statFull,
                                              .Divergent = False}
        Dim cell1 As New WhatIfGridCell With {.Index = 1,
                                              .Cell = New Dictionary(Of String, Double) From {
                                                {"scoring.trade_costs.min_net_move_pct", 0.0007}},
                                              .EvalWindowBars = 15, .EvSamples = samples,
                                              .DirectionalCount = 2, .BelowMinMoveExcluded = 0,
                                              .EvFull = statFull, .EvSel = statFull, .EvHold = statFull,
                                              .Divergent = False}
        Dim model As New WhatIfReportModel With {
            .Stamp = "20260729_120000", .OverlayPath = "test.json", .CsvPath = "analysis_log.csv",
            .SpanFrom = New DateTime(2026, 7, 10), .SpanTo = New DateTime(2026, 7, 11),
            .TotalRows = 2, .PocExcluded = 0, .GridCellCount = 2,
            .SweptKnobs = New List(Of String) From {"scoring.trade_costs.min_net_move_pct"},
            .OverlaySummary = "", .OverfitCounter = 1,
            .Cells = New List(Of WhatIfGridCell) From {cell0, cell1},
            .WinnerIndex = 0, .LiveCfg = cfg, .WinnerCfg = cfg,
            .BaselineRows = New List(Of CsvRow)(), .WinnerRows = New List(Of CsvRow)(),
            .BaselineBelowMin = 0, .WinnerBelowMin = 0, .SettingsVersion = 62}
        Dim md As String = WhatIfReport.Build(model)

        Dim okTitle As Boolean = md.Contains("net of fees")
        Dim okSigmaHeader As Boolean = md.Contains("| σ |")
        Dim okNotComparable As Boolean = md.Contains("not comparable")

        Check("A41d report ranking carries the net-of-fees label + σ dispersion column + not-comparable note (§6.1 rider)",
              okStdPop AndAlso okTitle AndAlso okSigmaHeader AndAlso okNotComparable,
              String.Format(CultureInfo.InvariantCulture,
                            "stdPop={0} ({1:R}) title={2} sigma={3} notCmp={4}",
                            okStdPop, statFull.StdPop, okTitle, okSigmaHeader, okNotComparable))
    End Sub

    ' =======================================================================
    ' A42 — D2-v2 what-if candidate mode (v63). New POCO key:
    ' scoring.structural_levels.use_best_pivot_candidate (Boolean, default False).
    ' When true, r.BestPivotByVolume5m joins the TARGET candidate set inside
    ' SignalEmitter.ComputeSideLevels — side by PRICE-vs-entry (D3); ladder mode
    ' ⇒ FIRST tier above swing (D2, P1 verbatim); NEAREST mode ⇒ competes on
    ' distance; same looseness bound (target_max_atr_mult) as every tier; absent
    ' pivot ⇒ candidate absent (counted, not guessed). STOP side untouched.
    ' Label BEST_PIVOT_5M. Same fixture cfg (entry 62000, ATR 40 ⇒ fallback 62070,
    ' target bound 140, stop bound/fallback dist 64) as A36.
    ' =======================================================================

    Private Function A42Cfg() As EngineSettings
        Return New EngineSettings()          ' POCO defaults = shipped v63 (flag off)
    End Function

    Private Function A42Indicators() As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = 62000.0
        r.ATR = 40.0
        Return r
    End Function

    ' -- A42a: defaults byte-identical to v56 across the A26 case set --------------
    ' The load-bearing pin: at UseBestPivotCandidate=False the pivot must be a
    ' complete no-op even when r.BestPivotByVolume5m is populated with a value
    ' that WOULD win under the flag. Replays the A36a case set through the REAL
    ' arbitration and additionally asserts a "pivot supplied but flag off" case.
    Private Sub A42a_DefaultsByteIdenticalToV56()
        Dim cfg = A42Cfg()

        ' Case 1 — swing target farther than fallback still places (A26a long).
        Dim rSwing = A42Indicators()
        rSwing.SwingTargetLong = 62100.0
        rSwing.BestPivotByVolume5m = 62050.0    ' populated but ignored (flag off)
        Dim lvSwing = SignalEmitter.ComputeSideLevels(New VerdictResult(), rSwing, cfg, isLong:=True)
        Dim ok1 As Boolean = lvSwing.Target = 62100.0 AndAlso lvSwing.Capped AndAlso
                             lvSwing.Reason = "PLACED @ 62100.0 (SWING_HIGH_5M)" AndAlso
                             lvSwing.TargetReason = "SWING_HIGH_5M"

        ' Case 2 — swing too loose → HVN places (A26b tier walk).
        Dim rHvn = A42Indicators()
        rHvn.SwingTargetLong = 62150.0 : rHvn.VPFRNearestHvnAbove = 62120.0
        rHvn.BestPivotByVolume5m = 62030.0      ' populated but ignored
        Dim lvHvn = SignalEmitter.ComputeSideLevels(New VerdictResult(), rHvn, cfg, isLong:=True)
        Dim ok2 As Boolean = lvHvn.Target = 62120.0 AndAlso lvHvn.TargetReason = "NEAREST_HVN_ABOVE"

        ' Case 3 — no tier survives → FALLBACK_ATR (A26b fallback).
        Dim rFb = A42Indicators()
        rFb.SwingTargetLong = 62150.0 : rFb.VPFRNearestHvnAbove = 62200.0 : rFb.VPFRPoc = 62050.0
        rFb.BestPivotByVolume5m = 62060.0       ' populated but ignored
        Dim lvFb = SignalEmitter.ComputeSideLevels(New VerdictResult(), rFb, cfg, isLong:=True)
        Dim ok3 As Boolean = lvFb.Target = 62070.0 AndAlso Not lvFb.Capped AndAlso
                             lvFb.Reason Is Nothing AndAlso lvFb.TargetReason = "FALLBACK_ATR"

        ' Case 4 — structural stop within bound → SWING_STOP (A26c). STOP side must
        ' be entirely unaffected by the D2-v2 flag or a pivot value.
        Dim rStop = A42Indicators() : rStop.SwingStopLong = 61950.0
        rStop.BestPivotByVolume5m = 61960.0     ' populated but STOP side untouched
        Dim lvStop = SignalEmitter.ComputeSideLevels(New VerdictResult(), rStop, cfg, isLong:=True)
        Dim ok4 As Boolean = lvStop.StopPx = 61950.0 AndAlso lvStop.StopReason = "SWING_STOP"

        ' Case 5 — structural stop too loose → STOP_CLAMPED (A26c clamp).
        Dim rClamp = A42Indicators() : rClamp.SwingStopLong = 61900.0
        Dim lvClamp = SignalEmitter.ComputeSideLevels(New VerdictResult(), rClamp, cfg, isLong:=True)
        Dim ok5 As Boolean = Math.Abs(lvClamp.StopPx - 61936.0) < 0.0001 AndAlso
                             lvClamp.StopReason = "STOP_CLAMPED"

        ' Case 6 — no structural stop → FALLBACK_ATR (A26c).
        Dim lvNone = SignalEmitter.ComputeSideLevels(New VerdictResult(), A42Indicators(), cfg, isLong:=True)
        Dim ok6 As Boolean = Math.Abs(lvNone.StopPx - 61936.0) < 0.0001 AndAlso
                             lvNone.StopReason = "FALLBACK_ATR"

        ' Case 7 — pin the "pivot supplied, flag off ⇒ candidate absent" invariant
        ' through the REAL Calculate() (byte-identity to a run with pivot=0). The
        ' pivot at 62050 would be the closest qualifying candidate under NEAREST +
        ' the ladder-first pick under the flag; here it must NOT alter the output.
        Dim cfgReal = BuildA8Cfg(fundingBoost:=0)
        Dim rReal0 = BuildGateIndicators(atr:=20, price:=62000)
        rReal0.SwingTargetShort = 61940.0
        Dim vNoPivot = ScoringEngine.Calculate(rReal0, PositionState.None, BuildA8Norms(), cfgReal)
        Dim rReal1 = BuildGateIndicators(atr:=20, price:=62000)
        rReal1.SwingTargetShort = 61940.0
        rReal1.BestPivotByVolume5m = 61960.0
        Dim vPivotSupplied = ScoringEngine.Calculate(rReal1, PositionState.None, BuildA8Norms(), cfgReal)
        Dim ok7 As Boolean = vNoPivot.Verdict = vPivotSupplied.Verdict AndAlso
                             Math.Abs(vNoPivot.AdjustedShortTarget - vPivotSupplied.AdjustedShortTarget) < 0.0001 AndAlso
                             vNoPivot.TargetCapReasonShort = vPivotSupplied.TargetCapReasonShort

        Check("A42a defaults (use_best_pivot_candidate=false) byte-identical to v56 across the A26 case set + real-Calculate with pivot supplied",
              ok1 AndAlso ok2 AndAlso ok3 AndAlso ok4 AndAlso ok5 AndAlso ok6 AndAlso ok7,
              String.Format("swing={0} hvn={1} fallback={2} sSwing={3} sClamp={4} sFb={5} realIdentity={6}",
                            ok1, ok2, ok3, ok4, ok5, ok6, ok7))
    End Sub

    ' -- A42b: pivot enters the ladder FIRST + NEAREST distance pick + short mirror --
    ' Ladder mode: even a swing CLOSER to entry than the pivot loses — the pivot is
    ' the FIRST tier above swing (D2, P1 verbatim). NEAREST mode: pivot competes on
    ' distance like any candidate; a closer swing still wins.
    Private Sub A42b_BestPivotEntersLadderFirstAndNearestDistancePick()
        Dim cfg = A42Cfg()
        cfg.Scoring.StructuralLevels.UseBestPivotCandidate = True

        ' (a) LADDER mode, pivot 62100 (dist 100) beats a CLOSER qualifying swing at
        ' 62050 (dist 50) — the priority rule is candidate-set order, not distance.
        Dim rLad = A42Indicators()
        rLad.SwingTargetLong = 62050.0
        rLad.BestPivotByVolume5m = 62100.0
        Dim lvLad = SignalEmitter.ComputeSideLevels(New VerdictResult(), rLad, cfg, isLong:=True)
        Dim okLadder As Boolean = lvLad.Target = 62100.0 AndAlso lvLad.Capped AndAlso
                                  lvLad.TargetReason = "BEST_PIVOT_5M" AndAlso
                                  lvLad.Reason = "PLACED @ 62100.0 (BEST_PIVOT_5M)"

        ' (b) LADDER short mirror: pivot BELOW entry ⇒ short-target candidate. Pivot
        ' 61900 (dist 100) beats a closer swing at 61950 (dist 50).
        Dim rLadS = A42Indicators()
        rLadS.SwingTargetShort = 61950.0
        rLadS.BestPivotByVolume5m = 61900.0
        Dim lvLadS = SignalEmitter.ComputeSideLevels(New VerdictResult(), rLadS, cfg, isLong:=False)
        Dim okLadderShort As Boolean = lvLadS.Target = 61900.0 AndAlso lvLadS.Capped AndAlso
                                       lvLadS.TargetReason = "BEST_PIVOT_5M" AndAlso
                                       lvLadS.Reason = "PLACED @ 61900.0 (BEST_PIVOT_5M)"

        ' (c) NEAREST mode: pivot competes on distance. A CLOSER swing (dist 40)
        ' wins over a FARTHER pivot (dist 100).
        Dim cfgN = A42Cfg()
        cfgN.Scoring.StructuralLevels.UseBestPivotCandidate = True
        cfgN.Scoring.StructuralLevels.TargetArbitrationMode = 1
        Dim rN = A42Indicators()
        rN.SwingTargetLong = 62040.0
        rN.BestPivotByVolume5m = 62100.0
        Dim lvN = SignalEmitter.ComputeSideLevels(New VerdictResult(), rN, cfgN, isLong:=True)
        Dim okNearestSwing As Boolean = lvN.Target = 62040.0 AndAlso lvN.TargetReason = "SWING_HIGH_5M"

        ' (d) NEAREST mode: pivot CLOSER than every other candidate wins.
        Dim rN2 = A42Indicators()
        rN2.SwingTargetLong = 62100.0                   ' dist 100
        rN2.VPFRNearestHvnAbove = 62060.0               ' dist 60
        rN2.BestPivotByVolume5m = 62030.0               ' dist 30 (closest)
        Dim lvN2 = SignalEmitter.ComputeSideLevels(New VerdictResult(), rN2, cfgN, isLong:=True)
        Dim okNearestPivot As Boolean = lvN2.Target = 62030.0 AndAlso
                                        lvN2.TargetReason = "BEST_PIVOT_5M"

        ' (e) LADDER STOP untouched: a pivot that would qualify as a stop-relevant
        ' level must NOT alter the stop arbitration (which reads Swing*Stop only).
        Dim rStop = A42Indicators()
        rStop.SwingStopLong = 61950.0
        rStop.BestPivotByVolume5m = 62100.0
        Dim lvStop = SignalEmitter.ComputeSideLevels(New VerdictResult(), rStop, cfg, isLong:=True)
        Dim okStop As Boolean = lvStop.StopPx = 61950.0 AndAlso lvStop.StopReason = "SWING_STOP"

        Check("A42b pivot enters ladder FIRST (beats closer swing) + NEAREST picks min-distance + short mirror + STOP untouched",
              okLadder AndAlso okLadderShort AndAlso okNearestSwing AndAlso okNearestPivot AndAlso okStop,
              String.Format("ladder={0} ladShort={1} nearSwing={2} nearPivot={3} stop={4}",
                            okLadder, okLadderShort, okNearestSwing, okNearestPivot, okStop))
    End Sub

    ' -- A42c: looseness bound rejects a too-far pivot + absent/zero pivot ≡ default --
    ' Same looseness bound (target_max_atr_mult × ATR = 3.5 × 40 = 140) as every tier.
    ' A pivot beyond the bound is rejected; the ladder walks on to swing.
    Private Sub A42c_LoosenessBoundAndAbsentPivotEquivalence()
        Dim cfg = A42Cfg()
        cfg.Scoring.StructuralLevels.UseBestPivotCandidate = True

        ' (a) LADDER: pivot at dist 150 > bound 140 ⇒ rejected; swing (dist 50) places.
        Dim rTooFar = A42Indicators()
        rTooFar.SwingTargetLong = 62050.0
        rTooFar.BestPivotByVolume5m = 62150.0
        Dim lvTooFar = SignalEmitter.ComputeSideLevels(New VerdictResult(), rTooFar, cfg, isLong:=True)
        Dim okTooFar As Boolean = lvTooFar.Target = 62050.0 AndAlso
                                  lvTooFar.TargetReason = "SWING_HIGH_5M"

        ' (b) LADDER: pivot on the WRONG side of entry (below entry for long) ⇒
        ' rejected (side is price-vs-entry, D3); swing places.
        Dim rWrong = A42Indicators()
        rWrong.SwingTargetLong = 62050.0
        rWrong.BestPivotByVolume5m = 61950.0
        Dim lvWrong = SignalEmitter.ComputeSideLevels(New VerdictResult(), rWrong, cfg, isLong:=True)
        Dim okWrongSide As Boolean = lvWrong.Target = 62050.0 AndAlso
                                     lvWrong.TargetReason = "SWING_HIGH_5M"

        ' (c) Absent/zero pivot ⇒ candidate absent, output ≡ default (flag off).
        Dim rAbs = A42Indicators()
        rAbs.SwingTargetLong = 62100.0
        rAbs.BestPivotByVolume5m = 0.0
        Dim lvOn = SignalEmitter.ComputeSideLevels(New VerdictResult(), rAbs, cfg, isLong:=True)
        Dim cfgOff = A42Cfg()
        Dim rAbsOff = A42Indicators()
        rAbsOff.SwingTargetLong = 62100.0
        Dim lvOff = SignalEmitter.ComputeSideLevels(New VerdictResult(), rAbsOff, cfgOff, isLong:=True)
        Dim okAbs As Boolean = lvOn.Target = lvOff.Target AndAlso
                               lvOn.TargetReason = lvOff.TargetReason AndAlso
                               lvOn.Reason = lvOff.Reason AndAlso
                               lvOn.Capped = lvOff.Capped

        Check("A42c looseness bound rejects too-far pivot (falls through to swing) + wrong-side rejected + absent/zero pivot ≡ default",
              okTooFar AndAlso okWrongSide AndAlso okAbs,
              String.Format("tooFar={0} wrongSide={1} absentEqDefault={2}", okTooFar, okWrongSide, okAbs))
    End Sub

    ' -- A42d: HC24 tweaker fence + what-if whitelist + {0,1} sweep round-trip -----
    ' HC24 exact-match rejects the new key; the flat sibling target_max_atr_mult
    ' (HC21) still passes. WhatIfOverlay whitelists the key + parses a {0,1} sweep
    ' (v56 int-mode precedent) into two cells. WhatIfSettings.BuildCellSettings
    ' applies the overlay onto a cloned cfg, and the WhatIfReplay adapter fed
    ' through the shipped ComputeSideLevels reproduces the direct-call SideLevels.
    Private Sub A42d_Hc24FenceAndWhatIfWhitelistAndRoundTrip()
        ' Whitelist accepts the new key; a scalar 1 and a {0,1} sweep both parse.
        Dim okFlag = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""use_best_pivot_candidate"":1}}}")
        Dim okSweep = WhatIfOverlay.Parse("{""scoring"":{""structural_levels"":{""use_best_pivot_candidate"":{""sweep"":{""from"":0,""to"":1,""step"":1}}}}}")
        Dim okWl As Boolean = okFlag.Knobs.Count = 1 AndAlso
                              okFlag.Knobs(0).Path = "scoring.structural_levels.use_best_pivot_candidate" AndAlso
                              okSweep.Knobs.Count = 1 AndAlso okSweep.Knobs(0).IsSweep AndAlso
                              okSweep.Knobs(0).Values.Count = 2

        ' HC24 fence: SettingsDiffApplier exact-match rejects the key; sibling flat
        ' numeric (target_max_atr_mult, HC21 surface) still passes.
        Dim s As String = "{""version"":63,""scoring"":{""atr_target_multiplier"":1.75," &
                          """structural_levels"":{""enabled"":true,""target_max_atr_mult"":3.5," &
                          """stop_max_atr_mult"":1.6,""stop_min_floor_ticks"":4," &
                          """stop_too_loose_mode"":""clamp""," &
                          """target_arbitration_mode"":0,""stop_arbitration_mode"":0," &
                          """target_buffer_pct"":0.0,""stop_buffer_pct"":0.0," &
                          """use_best_pivot_candidate"":false}}}"
        Dim rFlag = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.use_best_pivot_candidate", "false", "true"), s, 3)
        Dim rBound = SettingsDiffApplier.Validate(OneDiff("scoring.structural_levels.target_max_atr_mult", "3.5", "3.0"), s, 3)
        Dim okFence As Boolean = Not rFlag.IsValid AndAlso
                                 rFlag.ErrorReason.Contains("HARD CONSTRAINT 24") AndAlso
                                 rBound.IsValid

        ' Round-trip: an overlay {flag=1} through WhatIfSettings.BuildCellSettings
        ' must reach the POCO field, and the WhatIfReplay adapter must reproduce a
        ' direct SignalEmitter.ComputeSideLevels call under the same cfg (A36f pattern).
        Dim overlay As New Dictionary(Of String, Double) From {
            {"scoring.structural_levels.use_best_pivot_candidate", 1.0}
        }

        Dim tmp As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                                    "wifsettings-a42d-" & Guid.NewGuid().ToString("N") & ".json")
        Dim okApply As Boolean = False
        Dim okAdapter As Boolean = False
        Try
            System.IO.File.WriteAllText(tmp,
                "{""version"":63," &
                """scoring"":{""atr_target_multiplier"":1.75,""atr_stop_multiplier"":1.6," &
                """structural_levels"":{""enabled"":true,""target_max_atr_mult"":3.5," &
                """stop_max_atr_mult"":1.6,""stop_min_floor_ticks"":4," &
                """stop_too_loose_mode"":""clamp""," &
                """target_arbitration_mode"":0,""stop_arbitration_mode"":0," &
                """target_buffer_pct"":0.0,""stop_buffer_pct"":0.0," &
                """use_best_pivot_candidate"":false}}}")

            Dim wis As New WhatIfSettings(tmp)
            Dim cellCfg = wis.BuildCellSettings(overlay)
            okApply = cellCfg.Scoring.StructuralLevels.UseBestPivotCandidate = True

            ' Row: swing at 62070 (dist 70 — the fallback distance, in-bound) and a
            ' pivot at 62100 (dist 100 — also in-bound). Under the flag, ladder mode
            ' places the pivot FIRST — a distinct outcome from the flag-off path
            ' (which would place the swing).
            Dim row = BuildWhatIfRow()   ' SwingTargetLong = 62070
            row.BestPivotByVolume5m = 62100.0

            Dim r = WhatIfReplay.BuildIndicator(row)
            Dim direct = SignalEmitter.ComputeSideLevels(New VerdictResult(), r, cellCfg, isLong:=True)
            Dim run = WhatIfReplay.RunCell(New List(Of CsvRow) From {row}, cellCfg, 15, keepRows:=True)
            Dim rep = run.ReplayedRows(0)
            okAdapter = Math.Abs(rep.PlacedTargetLong - direct.Target) < 0.001 AndAlso
                        Math.Abs(rep.PlacedStopLong - direct.StopPx) < 0.001 AndAlso
                        direct.TargetReason = "BEST_PIVOT_5M" AndAlso
                        Math.Abs(direct.Target - 62100.0) < 0.001
        Finally
            Try : System.IO.File.Delete(tmp) : Catch : End Try
        End Try

        Check("A42d HC24 fence rejects the key + whitelist accepts it + {0,1} sweep + BuildCellSettings round-trip reproduces ComputeSideLevels",
              okWl AndAlso okFence AndAlso okApply AndAlso okAdapter,
              String.Format("wl={0} fence(flag={1}, bound={2}) apply={3} adapter={4}",
                            okWl, rFlag.IsValid, rBound.IsValid, okApply, okAdapter))
    End Sub

    ' == A43: Backtest synthesizer CORE (docs/backtest-synthesizer-proposal.md §4) ==

    ''' <summary>Build a small candle list starting at openTsMs, cadence resMin (in minutes),
    ''' n candles. Each candle closes at open + resMin*60000; the fixture asserts the slice
    ''' obeys that boundary exactly.</summary>
    Private Function BacktestCandleSeries(openTsMs As Long, resMin As Integer, n As Integer) As List(Of Candle)
        Dim result As New List(Of Candle)()
        Dim stepMs As Long = CLng(resMin) * 60000L
        For i As Integer = 0 To n - 1
            result.Add(New Candle With {
                .Timestamp = openTsMs + i * stepMs,
                .Open = 100000 + i, .High = 100000 + i + 5, .Low = 100000 + i - 5,
                .Close = 100000 + i, .Volume = 10})
        Next
        Return result
    End Function

    ' -- A43a: slice candles at-or-before-close boundary ----------------------
    ' 50 1-min candles starting at t=0. At closeMs = 30*60_000 we want candles whose
    ' close time (openTs + 60_000) is <= closeMs — that's opens 0 .. 29*60_000 = the
    ' first 30 candles. Requesting n=250 (the live 1m window) yields the whole 30
    ' because that's all that has closed. The LAST candle in the slice is candle 29
    ' (open t=29 min, close t=30 min) — the bar that JUST closed at closeMs.
    Private Sub A43a_SliceCandlesAtOrBefore()
        Dim series = BacktestCandleSeries(0, 1, 50)
        Dim closeMs As Long = 30L * 60L * 1000L

        Dim s250 = ReplayLoop.SliceCandlesAtOrBefore(series, 1, closeMs, 250)
        Dim s10  = ReplayLoop.SliceCandlesAtOrBefore(series, 1, closeMs, 10)

        Dim okAll = (s250.Count = 30) AndAlso (s250.Last().Timestamp = 29L * 60L * 1000L) AndAlso (s250.First().Timestamp = 0L)
        Dim okLast10 = (s10.Count = 10) AndAlso (s10.Last().Timestamp = 29L * 60L * 1000L) AndAlso (s10.First().Timestamp = 20L * 60L * 1000L)

        ' At exactly closeMs = 29*60_000 the bar with open 29 is NOT yet closed
        ' (it would close at t=30 min). Only 29 candles qualify.
        Dim onGrid = ReplayLoop.SliceCandlesAtOrBefore(series, 1, 29L * 60L * 1000L, 250)
        Dim okBoundary = (onGrid.Count = 29) AndAlso (onGrid.Last().Timestamp = 28L * 60L * 1000L)

        Check("A43a slice candles at-or-before-close (boundary + last-N-from-end)",
              okAll AndAlso okLast10 AndAlso okBoundary,
              String.Format("okAll={0} last10={1} boundary={2} (s250.Count={3} lastTs={4})",
                            okAll, okLast10, okBoundary, s250.Count,
                            If(s250.Count > 0, s250.Last().Timestamp, -1L)))
    End Sub

    ' -- A43b: trades ascending + LastN-from-end ------------------------------
    ' 60 ascending trades: oldest 30 sells, newest 30 buys. Slicing last-500 at a
    ' closeMs after all trades yields all 60 in ascending order. Then TFI (which
    ' uses LastN internally at window=30) must classify BUY PRESSURE — proving the
    ' slice keeps the newest trades at the END, not the beginning (Take(30) on the
    ' slice would score the SELLS and produce SELL PRESSURE — the classic F1 bug).
    Private Sub A43b_SliceTradesAscendingAndLastN()
        Dim trades As New List(Of TradeRecord)()
        Dim ts As Long = 1
        For i As Integer = 1 To 30
            trades.Add(Trade("sell", 1000, ts)) : ts += 1
        Next
        For i As Integer = 1 To 30
            trades.Add(Trade("buy", 1000, ts)) : ts += 1
        Next

        ' All trades qualify at a closeMs past the newest.
        Dim slice500 = ReplayLoop.SliceTradesAtOrBefore(trades, 100, 500)
        Dim okAll  = slice500.Count = 60 AndAlso slice500(0).Direction = "sell" AndAlso slice500.Last().Direction = "buy"

        ' Slicing to n=40 keeps the LAST 40 (all 30 buys + the newest 10 sells).
        Dim slice40 = ReplayLoop.SliceTradesAtOrBefore(trades, 100, 40)
        Dim okKeepEnd = slice40.Count = 40 AndAlso slice40.Last().Direction = "buy"

        ' TFI over the WHOLE slice (windowSize>=count) must read BUY PRESSURE because
        ' the newest half are buys — the ascending-order + LastN-from-end contract.
        Dim tfiVal As Double, tfiSig As String = ""
        IndicatorEngine.CalcTFI(slice500, tfiVal, tfiSig, tfiWindowSize:=30)
        Dim okTfi = (tfiSig = "BUY PRESSURE") AndAlso (Math.Abs(tfiVal - 1.0) < 0.000001)

        ' Boundary: closeMs = 40 admits the first 40 trades (ts 1..40); newest is a sell.
        Dim slice40Boundary = ReplayLoop.SliceTradesAtOrBefore(trades, 40, 500)
        Dim okBoundary = slice40Boundary.Count = 40 AndAlso slice40Boundary.Last().Timestamp = 40L

        Check("A43b trades ascending + LastN-from-end + at-or-before-close",
              okAll AndAlso okKeepEnd AndAlso okTfi AndAlso okBoundary,
              String.Format("okAll={0} keepEnd={1} tfi={2} boundary={3}",
                            okAll, okKeepEnd, okTfi, okBoundary))
    End Sub

    ' -- A43c: sequential-state replay (funding ring + regime hysteresis) ------
    ' Two independent state pins:
    '   (i)  Funding ring: append samples at t=0, t=W, t=2W minutes (W = the cfg's
    '        MomentumWindowMinutes). The v53 window is "newest sample ≥W old" — after
    '        the 2nd append the anchor is the t=0 sample; after the 3rd it becomes t=W.
    '        A rate that jumps by > MomentumThreshold between anchor and current fires
    '        RISING; a flat trajectory keeps FLAT.
    '   (ii) Regime hysteresis: prev TRENDING_UP → raw RANGE_BOUND holds the trending
    '        label for one bar (the fixture-pinned live rule); a following raw
    '        RANGE_BOUND then flips (prev is now RANGE_BOUND, no hold applied).
    Private Sub A43c_SequentialStateReplay()
        ' (i) Funding ring
        Dim cfg As New EngineSettings()
        Dim winMs As Long = CLng(cfg.Indicators.Funding.MomentumWindowMinutes * 60_000.0)
        Dim thr   As Double = cfg.Indicators.Funding.MomentumThreshold

        Dim history As New List(Of (UtcMs As Long, Rate As Double))()
        IndicatorEngine.AppendFundingSample(history, 0L, 0.00001)
        Dim s1 = IndicatorEngine.CalcFundingMomentum(history, 0L, cfg)   ' cold start ⇒ FLAT

        IndicatorEngine.AppendFundingSample(history, winMs, 0.00001)     ' identical rate at t=W
        Dim s2 = IndicatorEngine.CalcFundingMomentum(history, winMs, cfg) ' anchor=t=0, delta=0 ⇒ FLAT

        ' Bump by > 2*threshold at t=2W; anchor is now t=W (newest with age ≥ W); delta > threshold ⇒ RISING.
        Dim bumpedRate As Double = 0.00001 + 2.0 * thr
        IndicatorEngine.AppendFundingSample(history, 2L * winMs, bumpedRate)
        Dim s3 = IndicatorEngine.CalcFundingMomentum(history, 2L * winMs, cfg)

        Dim okFunding = (s1 = "FLAT") AndAlso (s2 = "FLAT") AndAlso (s3 = "RISING")

        ' (ii) Regime hysteresis — the live rule (MainForm_Analysis) reproduced pure.
        Dim prev As String = ""
        Dim seq() As String = {"TRENDING_UP", "RANGE_BOUND", "RANGE_BOUND", "TRENDING_DOWN"}
        Dim effective(seq.Length - 1) As String
        For i As Integer = 0 To seq.Length - 1
            Dim raw = seq(i)
            Dim prevWasTrending As Boolean = (prev = "TRENDING_UP" OrElse prev = "TRENDING_DOWN" OrElse prev = "TRANSITIONAL")
            effective(i) = If(raw = "RANGE_BOUND" AndAlso prevWasTrending, prev, raw)
            prev = raw   ' hysteresis updates on the RAW regime — the fixture-pinned rule
        Next
        Dim okHyst = effective(0) = "TRENDING_UP" AndAlso
                     effective(1) = "TRENDING_UP" AndAlso   ' held for 1 bar
                     effective(2) = "RANGE_BOUND" AndAlso   ' 2nd RB flips (prev raw was RB, no hold)
                     effective(3) = "TRENDING_DOWN"

        Check("A43c sequential state (funding ring anchors + regime hysteresis)",
              okFunding AndAlso okHyst,
              String.Format("funding=[{0}/{1}/{2}] hyst=[{3},{4},{5},{6}]",
                            s1, s2, s3, effective(0), effective(1), effective(2), effective(3)))
    End Sub

    ' -- A43d: muted-vote inertness -------------------------------------------
    ' Drive Calculate() with the A8 dominant-side cascade fixture (isolates the vote
    ' math from MTF/Pass 2b/2c). Run TWICE: once with the backtest muted-signal
    ' defaults set (OFI BALANCED / FLAT, OFISignal="BALANCED", spread 0 / NORMAL,
    ' OI NEUTRAL, absorption NONE); once with those same fields wiped to POCO
    ' defaults. The verdict + effective scores must be IDENTICAL — the muted signals
    ' contribute zero, which is the whole D2 contract.
    Private Sub A43d_MutedVoteInertness()
        Dim cfg = BuildA8Cfg(fundingBoost:=3)
        Dim norms = BuildA8Norms()

        ' (i) Backtest-muted version — the values ReplayLoop sets explicitly.
        Dim rMuted = BuildA8Indicators()
        rMuted.OFIRatio    = 1.0
        rMuted.OFIBidVol   = 0
        rMuted.OFIAskVol   = 0
        rMuted.OFISignal   = "BALANCED"
        rMuted.OFIMomentum = "FLAT"
        rMuted.SpreadBps    = 0
        rMuted.SpreadStatus = "NORMAL"
        rMuted.OI_Current  = 0
        rMuted.OIChange15m = 0
        rMuted.OIChange60m = 0
        rMuted.OISignal    = "NEUTRAL"
        rMuted.AbsorptionSignal   = "NONE"
        rMuted.AbsorptionLevel    = Nothing
        rMuted.AbsorptionRatio    = Nothing
        rMuted.AbsorptionAggrUsd  = Nothing
        rMuted.AbsorptionPullFrac = Nothing

        ' (ii) Bare "unavailable" version — leave the same fields at POCO defaults.
        ' A8's own indicator builder sets OFISignal etc. to values that DO vote.
        ' Wipe them so we're comparing "muted-explicit" against "empty/unset".
        Dim rBare = BuildA8Indicators()
        rBare.OFIRatio    = 0
        rBare.OFIBidVol   = 0
        rBare.OFIAskVol   = 0
        rBare.OFISignal   = ""
        rBare.OFIMomentum = ""
        rBare.SpreadBps    = 0
        rBare.SpreadStatus = ""
        rBare.OI_Current  = 0
        rBare.OIChange15m = 0
        rBare.OIChange60m = 0
        rBare.OISignal    = ""
        rBare.AbsorptionSignal   = ""
        rBare.AbsorptionLevel    = Nothing
        rBare.AbsorptionRatio    = Nothing
        rBare.AbsorptionAggrUsd  = Nothing
        rBare.AbsorptionPullFrac = Nothing

        Dim vMuted = ScoringEngine.Calculate(rMuted, PositionState.None, norms, cfg)
        Dim vBare  = ScoringEngine.Calculate(rBare,  PositionState.None, norms, cfg)

        Dim ok = vMuted.Verdict = vBare.Verdict AndAlso
                 vMuted.EffectiveLongScore  = vBare.EffectiveLongScore AndAlso
                 vMuted.EffectiveShortScore = vBare.EffectiveShortScore

        Check("A43d muted-vote inertness (OFI/spread/OI/absorption contribute zero)",
              ok,
              String.Format("muted='{0}' eff {1}/{2} vs bare='{3}' eff {4}/{5}",
                            vMuted.Verdict, vMuted.EffectiveLongScore, vMuted.EffectiveShortScore,
                            vBare.Verdict, vBare.EffectiveLongScore, vBare.EffectiveShortScore))
    End Sub

    ' -- A43e: header byte-parity + provenance stamping ------------------------
    ' The local BacktestRowWriter reproduces AnalysisLogger's v0.8 row format because
    ' AnalysisLogger's Header is Private and its path is hardcoded — the task's
    ' HARD CONSTRAINT forbids modifying that file. Byte-level equality via reflection
    ' catches any header drift on either side; the provenance sub-check verifies the
    ' "BACKTEST-" InstanceId prefix and the monotonic SignalId per writer instance.
    Private Sub A43e_HeaderParityAndProvenance()
        ' -- header equality --
        Dim fld = GetType(AnalysisLogger).GetField("Header",
            Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Static)
        Dim liveHeader As String = If(fld Is Nothing, "", CStr(fld.GetValue(Nothing)))
        Dim okHeader = liveHeader = BacktestRowWriter.Header

        ' -- provenance: write two rows and re-read to inspect prefix + monotonic id --
        Dim tmp As String = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "backtest_row_writer_a43e_" & Guid.NewGuid().ToString("N") & ".csv")

        Dim okProv As Boolean = False
        Dim detail As String = ""
        Try
            Dim writer As New BacktestRowWriter(tmp)
            Dim v As New VerdictResult() With {.Verdict = "NO TRADE", .Confidence = "LOW", .MaxScore = 18}
            Dim r = BuildA8Indicators()   ' any populated fixture will do; header shape doesn't depend on values
            r.ATR = 50
            Dim cfg = BuildA8Cfg(fundingBoost:=0)
            writer.WriteRow(r, v, cfg, New DateTime(2026, 7, 30, 12, 0, 0, DateTimeKind.Utc))
            writer.WriteRow(r, v, cfg, New DateTime(2026, 7, 30, 12, 1, 0, DateTimeKind.Utc))

            Dim lines = System.IO.File.ReadAllLines(tmp)
            If lines.Length <> 3 Then
                detail = "expected 3 lines (header + 2 rows), got " & lines.Length
            Else
                Dim r1 = lines(1).Split(","c)
                Dim r2 = lines(2).Split(","c)
                Dim iid1 = r1(r1.Length - 2)
                Dim iid2 = r2(r2.Length - 2)
                Dim sid1 = Integer.Parse(r1(r1.Length - 1))
                Dim sid2 = Integer.Parse(r2(r2.Length - 1))
                okProv = iid1.StartsWith("BACKTEST-") AndAlso iid1 = iid2 AndAlso sid2 = sid1 + 1
                detail = String.Format("iid1='{0}' iid2='{1}' sid1={2} sid2={3}", iid1, iid2, sid1, sid2)
            End If
        Finally
            Try : System.IO.File.Delete(tmp) : Catch : End Try
        End Try

        Check("A43e header byte-parity + provenance (BACKTEST- prefix + monotonic SignalId)",
              okHeader AndAlso okProv,
              String.Format("header={0} prov={1} ({2})", okHeader, okProv, detail))
    End Sub

    ' A44a: OverlapValidator.FloorToBucket collapses live and synthetic timestamps that
    ' land in the same execution-resolution bar into a single join key. Rows drifting
    ' by seconds within the same 3-min ASIA/LONDON bar or 1-min NY bar MUST hash to the
    ' same bucket; rows crossing the grid boundary MUST land in different buckets.
    Private Sub A44a_FloorToBucketSameGrid()
        ' 3-min grid: 12:03:00 UTC.
        Dim mid3   As Long = New DateTimeOffset(2026, 7, 30, 12, 3, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim late3  As Long = New DateTimeOffset(2026, 7, 30, 12, 5, 59, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim next3  As Long = New DateTimeOffset(2026, 7, 30, 12, 6, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim b1 = OverlapValidator.FloorToBucket(mid3, 3)
        Dim b2 = OverlapValidator.FloorToBucket(late3, 3)
        Dim b3 = OverlapValidator.FloorToBucket(next3, 3)
        Dim ok3 As Boolean = (b1 = b2) AndAlso (b3 = b1 + 3L * 60L * 1000L)

        ' 1-min grid: two timestamps 30 s apart on same minute bucket must collapse.
        Dim mid1   As Long = New DateTimeOffset(2026, 7, 30, 14, 27, 0,  TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim late1  As Long = New DateTimeOffset(2026, 7, 30, 14, 27, 59, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim next1  As Long = New DateTimeOffset(2026, 7, 30, 14, 28, 0,  TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim c1 = OverlapValidator.FloorToBucket(mid1, 1)
        Dim c2 = OverlapValidator.FloorToBucket(late1, 1)
        Dim c3 = OverlapValidator.FloorToBucket(next1, 1)
        Dim ok1 As Boolean = (c1 = c2) AndAlso (c3 = c1 + 60L * 1000L)

        ' Guard: execRes<=0 must fall through to 1-min grid, not divide by zero.
        Dim zGuard = OverlapValidator.FloorToBucket(mid1, 0)
        Dim okZero As Boolean = (zGuard = c1)

        Check("A44a FloorToBucket collapses same-bar timestamps + advances one bucket at the boundary + execRes<=0 guard",
              ok3 AndAlso ok1 AndAlso okZero,
              String.Format("ok3={0} ok1={1} okZero={2}", ok3, ok1, okZero))
    End Sub

    ' A43f: §7.1 forming-bar stub construction — the trader's post-validation amendment.
    ' Three sub-checks pinned in a single fixture:
    '   (i)   trades-in-window OHLCV compaction — 4 real trades in [closeMs, closeMs+2s]
    '         yield Open=first, Close=last, High=max, Low=min, Volume=Σ amount,
    '         VolumeUSD=Σ (amount × price); Timestamp = closeMs.
    '   (ii)  zero-trade fallback — no trades in the window ⇒ {O=H=L=C = prevClose, V=0}.
    '   (iii) stub-is-last-bar in the sliced series — after AppendFormingStub the slice's
    '         last element IS the stub (identity check by Timestamp = closeMs advancing
    '         past the last real bar), and total count = 1-less-slice + 1 = live-count.
    Private Sub A43f_FormingStubConstruction()
        Const closeMs As Long = 1785321600000L   ' 2026-07-29 18:00:00 UTC — a valid 1m/3m boundary
        Dim endMs As Long = closeMs + ReplayLoop.FormingStubDeltaMs
        Const prevClose As Double = 64000.0

        ' -- (i) trades-in-window OHLCV --
        Dim inWindow As New List(Of TradeRecord) From {
            New TradeRecord With {.Price = 64010, .Amount = 0.5, .Direction = "buy",  .Liquidation = "none", .Timestamp = closeMs + 100L},
            New TradeRecord With {.Price = 64050, .Amount = 1.0, .Direction = "buy",  .Liquidation = "none", .Timestamp = closeMs + 800L},
            New TradeRecord With {.Price = 63990, .Amount = 0.25,.Direction = "sell", .Liquidation = "none", .Timestamp = closeMs + 1400L},
            New TradeRecord With {.Price = 64025, .Amount = 0.75,.Direction = "buy",  .Liquidation = "none", .Timestamp = closeMs + 1900L}}
        ' UNITS (corrected 2026-07-31): TradeRecord.Amount is USD notional; Candle.Volume
        ' is BASE (BTC) and Candle.VolumeUSD is the USD cost. So Volume = Σ(amount/price)
        ' and VolumeUSD = Σ amount. The original expectations here encoded the inverted
        ' convention and are why the ~64,000x stub-volume error survived to the D3 A/B.
        Dim stub = ReplayLoop.BuildFormingStub(prevClose, inWindow, closeMs)
        Dim expVol As Double = 0.5 / 64010 + 1.0 / 64050 + 0.25 / 63990 + 0.75 / 64025
        Dim expVolUsd As Double = 0.5 + 1.0 + 0.25 + 0.75
        Dim okTrades = (stub.Timestamp = closeMs) AndAlso
                       (Math.Abs(stub.Open  - 64010.0) < 1e-9) AndAlso
                       (Math.Abs(stub.Close - 64025.0) < 1e-9) AndAlso
                       (Math.Abs(stub.High  - 64050.0) < 1e-9) AndAlso
                       (Math.Abs(stub.Low   - 63990.0) < 1e-9) AndAlso
                       (Math.Abs(stub.Volume - expVol) < 1e-9) AndAlso
                       (Math.Abs(stub.VolumeUSD - expVolUsd) < 1e-6)

        ' -- (ii) zero-trade fallback --
        Dim stubEmpty = ReplayLoop.BuildFormingStub(prevClose, New List(Of TradeRecord)(), closeMs)
        Dim okEmpty = (stubEmpty.Timestamp = closeMs) AndAlso
                      (stubEmpty.Open = prevClose) AndAlso (stubEmpty.High = prevClose) AndAlso
                      (stubEmpty.Low = prevClose) AndAlso (stubEmpty.Close = prevClose) AndAlso
                      (stubEmpty.Volume = 0) AndAlso (stubEmpty.VolumeUSD = 0)

        ' -- (iii) stub is the last bar of the slice; TradesInStubWindow selects only
        '         the [closeMs, closeMs+2s] set — trades outside are excluded.
        Dim allTrades As New List(Of TradeRecord) From {
            New TradeRecord With {.Price = 63000, .Amount = 1.0, .Direction = "buy",  .Liquidation = "none", .Timestamp = closeMs - 5000L},
            New TradeRecord With {.Price = 63500, .Amount = 1.0, .Direction = "sell", .Liquidation = "none", .Timestamp = closeMs - 1L}}
        allTrades.AddRange(inWindow)
        allTrades.Add(New TradeRecord With {.Price = 65000, .Amount = 1.0, .Direction = "buy",  .Liquidation = "none", .Timestamp = endMs + 1L})

        Dim windowTrades = ReplayLoop.TradesInStubWindow(allTrades, closeMs)
        Dim okWindow = (windowTrades.Count = 4)   ' the 4 inWindow trades, not the 2 pre or 1 post

        Dim slice As New List(Of Candle) From {
            New Candle With {.Timestamp = closeMs - 3L * 60L * 1000L, .Open = 63700, .High = 63750, .Low = 63650, .Close = 63700, .Volume = 5},
            New Candle With {.Timestamp = closeMs - 2L * 60L * 1000L, .Open = 63700, .High = 63780, .Low = 63690, .Close = 63750, .Volume = 6},
            New Candle With {.Timestamp = closeMs - 1L * 60L * 1000L, .Open = 63750, .High = 64000, .Low = 63700, .Close = prevClose, .Volume = 8}}
        Dim originalCount As Integer = slice.Count

        ReplayLoop.AppendFormingStub(slice, windowTrades, closeMs)
        Dim okAppendCount = (slice.Count = originalCount + 1)
        Dim last = slice(slice.Count - 1)
        Dim okAppendLast  = (last.Timestamp = closeMs) AndAlso
                            (last.Timestamp > slice(slice.Count - 2).Timestamp) AndAlso
                            (Math.Abs(last.Close - 64025.0) < 1e-9) AndAlso
                            (Math.Abs(last.Volume - expVol) < 1e-9)

        ' Empty-slice no-op guard: appending to an empty slice must not throw and must
        ' leave the slice empty (there is no prevClose to fall back to).
        Dim emptySlice As New List(Of Candle)()
        ReplayLoop.AppendFormingStub(emptySlice, windowTrades, closeMs)
        Dim okEmptyNoop = (emptySlice.Count = 0)

        Check("A43f forming-bar stub (§7.1) — OHLCV compaction · zero-trade fallback · stub-is-last-bar",
              okTrades AndAlso okEmpty AndAlso okWindow AndAlso okAppendCount AndAlso okAppendLast AndAlso okEmptyNoop,
              String.Format("trades={0} empty={1} window={2}(cnt={3}) appCount={4} appLast={5} emptyNoop={6}",
                            okTrades, okEmpty, okWindow, windowTrades.Count,
                            okAppendCount, okAppendLast, okEmptyNoop))
    End Sub

    ' A45a: VWAP session-anchor parameterization (§7.5). Four sub-checks:
    '
    '   (i)   DEFAULT-PATH IDENTITY — the live contract. Omitting nowUtc must equal passing
    '         DateTime.UtcNow explicitly, for BOTH CalcVWAP and CalcVWAPBands. The candle set
    '         is placed a day either side of the probe date so the answer is invariant to the
    '         13:30 cutoff AND to a midnight rollover landing between the two calls — the
    '         identity is therefore clock-race-free while still being NON-TRIVIAL (the
    '         day-before candle is genuinely filtered out, so this is not the fallback tie).
    '   (ii)  HISTORICAL ANCHOR, POST-CUTOFF — nowUtc = 2026-07-15 15:00Z anchors at 13:30Z
    '         that day, selecting only the two post-cutoff candles: VWAP = (300+400)/2 = 350.
    '   (iii) HISTORICAL ANCHOR, PRE-CUTOFF — nowUtc = 2026-07-15 12:00Z anchors at 00:00Z,
    '         selecting all four: VWAP = (100+200+300+400)/4 = 250. Sigma differs too
    '         (50 over the 2-candle window vs sqrt(12500) over the 4-candle one), so the
    '         band path is pinned to the same anchor, not just the mean.
    '   (iv)  THE §8.6 DEFECT — the un-parameterised (default) call on that same historical
    '         set anchors to the real clock, finds zero in-session candles and falls back to
    '         the WHOLE list. So default == the pre-cutoff answer (250) and != the correct
    '         post-cutoff answer (350). This is why replay had to pass the bar close.
    Private Sub A45a_VwapSessionAnchorParameterization()
        Const s2Hour As Integer = 13
        Const s2Min  As Integer = 30

        ' -- (i) default ≡ explicit-UtcNow, non-trivially --
        Dim probe As DateTime = DateTime.UtcNow
        Dim before As Long = New DateTimeOffset(probe.Date.AddDays(-1).AddHours(12), TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim afterA As Long = New DateTimeOffset(probe.Date.AddDays(1).AddHours(2), TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim afterB As Long = New DateTimeOffset(probe.Date.AddDays(1).AddHours(4), TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim liveSet As New List(Of Candle) From {
            New Candle With {.Timestamp = before, .Open = 1000, .High = 1000, .Low = 1000, .Close = 1000, .Volume = 1},
            New Candle With {.Timestamp = afterA, .Open = 2000, .High = 2000, .Low = 2000, .Close = 2000, .Volume = 1},
            New Candle With {.Timestamp = afterB, .Open = 3000, .High = 3000, .Low = 3000, .Close = 3000, .Volume = 1}}

        Dim cntDefault As Integer = 0, cntExplicit As Integer = 0
        Dim vwapDefault As Double = IndicatorEngine.CalcVWAP(liveSet, cntDefault, s2Hour, s2Min)
        Dim vwapExplicit As Double = IndicatorEngine.CalcVWAP(liveSet, cntExplicit, s2Hour, s2Min, probe)
        Dim okLiveIdentity As Boolean = (Math.Abs(vwapDefault - vwapExplicit) < 1e-9) AndAlso
                                        (cntDefault = cntExplicit) AndAlso
                                        (cntDefault = 2) AndAlso
                                        (Math.Abs(vwapDefault - 2500.0) < 1e-9)

        Dim d1U As Double, d1L As Double, d2U As Double, d2L As Double
        Dim e1U As Double, e1L As Double, e2U As Double, e2L As Double
        IndicatorEngine.CalcVWAPBands(liveSet, vwapDefault, d1U, d1L, d2U, d2L, s2Hour, s2Min)
        IndicatorEngine.CalcVWAPBands(liveSet, vwapDefault, e1U, e1L, e2U, e2L, s2Hour, s2Min, probe)
        Dim okBandIdentity As Boolean = (Math.Abs(d1U - e1U) < 1e-9) AndAlso (Math.Abs(d1L - e1L) < 1e-9) AndAlso
                                        (Math.Abs(d2U - e2U) < 1e-9) AndAlso (Math.Abs(d2L - e2L) < 1e-9) AndAlso
                                        (Math.Abs(d1U - 3000.0) < 1e-9)   ' vwap 2500 + sigma 500

        ' -- historical set: 2026-07-15, two candles either side of the 13:30Z cutoff --
        Dim hist As New List(Of Candle) From {
            New Candle With {.Timestamp = HistMs(10, 0), .Open = 100, .High = 100, .Low = 100, .Close = 100, .Volume = 1},
            New Candle With {.Timestamp = HistMs(12, 0), .Open = 200, .High = 200, .Low = 200, .Close = 200, .Volume = 1},
            New Candle With {.Timestamp = HistMs(14, 0), .Open = 300, .High = 300, .Low = 300, .Close = 300, .Volume = 1},
            New Candle With {.Timestamp = HistMs(16, 0), .Open = 400, .High = 400, .Low = 400, .Close = 400, .Volume = 1}}

        ' -- (ii) post-cutoff anchor: 15:00Z ⇒ session starts 13:30Z ⇒ last two candles --
        Dim cntPost As Integer = 0
        Dim vwapPost As Double = IndicatorEngine.CalcVWAP(hist, cntPost, s2Hour, s2Min,
                                                          New DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc))
        Dim p1U As Double, p1L As Double, p2U As Double, p2L As Double
        IndicatorEngine.CalcVWAPBands(hist, vwapPost, p1U, p1L, p2U, p2L, s2Hour, s2Min,
                                      New DateTime(2026, 7, 15, 15, 0, 0, DateTimeKind.Utc))
        Dim okPost As Boolean = (cntPost = 2) AndAlso (Math.Abs(vwapPost - 350.0) < 1e-9) AndAlso
                                (Math.Abs(p1U - 400.0) < 1e-9) AndAlso (Math.Abs(p1L - 300.0) < 1e-9) AndAlso
                                (Math.Abs(p2U - 450.0) < 1e-9) AndAlso (Math.Abs(p2L - 250.0) < 1e-9)

        ' -- (iii) pre-cutoff anchor: 12:00Z ⇒ session starts 00:00Z ⇒ all four candles --
        Dim cntPre As Integer = 0
        Dim vwapPre As Double = IndicatorEngine.CalcVWAP(hist, cntPre, s2Hour, s2Min,
                                                         New DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc))
        Dim q1U As Double, q1L As Double, q2U As Double, q2L As Double
        IndicatorEngine.CalcVWAPBands(hist, vwapPre, q1U, q1L, q2U, q2L, s2Hour, s2Min,
                                      New DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc))
        Dim expSigmaPre As Double = Math.Sqrt(12500.0)
        Dim okPre As Boolean = (cntPre = 4) AndAlso (Math.Abs(vwapPre - 250.0) < 1e-9) AndAlso
                               (Math.Abs(q1U - (250.0 + expSigmaPre)) < 1e-9) AndAlso
                               (Math.Abs(q1L - (250.0 - expSigmaPre)) < 1e-9)

        ' -- (iv) the §8.6 defect: default anchors to the real clock ⇒ whole-list fallback --
        Dim cntWall As Integer = 0
        Dim vwapWall As Double = IndicatorEngine.CalcVWAP(hist, cntWall, s2Hour, s2Min)
        Dim okDefect As Boolean = (cntWall = 4) AndAlso
                                  (Math.Abs(vwapWall - 250.0) < 1e-9) AndAlso
                                  (Math.Abs(vwapWall - vwapPost) > 1.0)

        Check("A45a VWAP session anchor (§7.5) — default ≡ UtcNow (non-trivial) · post/pre-cutoff historical anchor · §8.6 wall-clock fallback",
              okLiveIdentity AndAlso okBandIdentity AndAlso okPost AndAlso okPre AndAlso okDefect,
              String.Format("liveIdentity={0}(def={1}/exp={2} cnt={3}/{4}) bandIdentity={5} post={6}(cnt={7} vwap={8}) pre={9}(cnt={10} vwap={11}) defect={12}(cnt={13} vwap={14})",
                            okLiveIdentity, vwapDefault, vwapExplicit, cntDefault, cntExplicit,
                            okBandIdentity, okPost, cntPost, vwapPost,
                            okPre, cntPre, vwapPre, okDefect, cntWall, vwapWall))
    End Sub

    ' A47a: the D3 closed-bar A/B arm holds the window length constant.
    '
    ' The whole point of the A/B is that ONE thing differs between arms. The stub arm
    ' slices (N−1) closed bars and appends a 2-second stub; the closed arm slices N closed
    ' bars and appends nothing. Both must therefore yield N bars, and the closed arm's
    ' terminal bar must be a REAL closed bar (a genuine store candle with real volume),
    ' not a stub. If the reserve arithmetic ever drifts, the A/B silently becomes a
    ' two-variable comparison — window length AND terminal-bar kind — and its result
    ' would be uninterpretable. This fixture is what stops that.
    Private Sub A47a_ClosedBarArmHoldsWindowLength()
        Const closeMs As Long = 1785321600000L    ' 2026-07-29 18:00:00 UTC, a 1m/3m boundary
        Const barMs   As Long = 60000L
        Const want    As Integer = 10             ' stand-in for Candles1mCount

        ' 12 real closed 1m bars ending at closeMs (the last one closes exactly at closeMs).
        Dim store As New List(Of Candle)
        For k = 12 To 1 Step -1
            store.Add(New Candle With {.Timestamp = closeMs - k * barMs,
                                       .Open = 64000 + k, .High = 64010 + k,
                                       .Low = 63990 + k, .Close = 64005 + k,
                                       .Volume = 5 + k})
        Next

        ' Trades inside [closeMs, closeMs+2s] — what the stub arm compacts.
        Dim stubTrades As New List(Of TradeRecord) From {
            New TradeRecord With {.Price = 64100, .Amount = 0.4, .Direction = "buy",
                                  .Liquidation = "none", .Timestamp = closeMs + 250L}}

        ' -- stub arm: (N−1) closed + 1 stub --
        Dim stubArm = ReplayLoop.SliceCandlesAtOrBefore(store, 1, closeMs, want - 1)
        Dim closedTail = stubArm(stubArm.Count - 1)          ' last REAL bar before the stub
        ReplayLoop.AppendFormingStub(stubArm, stubTrades, closeMs)

        ' -- closed arm: N closed, no stub --
        Dim closedArm = ReplayLoop.SliceCandlesAtOrBefore(store, 1, closeMs, want)

        ' (a) equal window length — the controlled-comparison property
        Dim okLen As Boolean = (stubArm.Count = want) AndAlso (closedArm.Count = want)

        ' (b) the closed arm's terminal bar is a REAL store bar (carries real volume and
        '     its timestamp is a closed-bar open, strictly before closeMs)
        Dim cTail = closedArm(closedArm.Count - 1)
        Dim okReal As Boolean = (cTail.Timestamp < closeMs) AndAlso
                                (cTail.Timestamp + barMs <= closeMs) AndAlso
                                (cTail.Volume > 0) AndAlso
                                (cTail.Timestamp = closedTail.Timestamp) AndAlso
                                (Math.Abs(cTail.Volume - closedTail.Volume) < 1e-9)

        ' (c) the stub arm's terminal bar is the STUB (timestamp = closeMs, trade-built).
        '     Volume is BTC — $0.4 notional at $64,100 — see A47b for the unit pin.
        Dim sTail = stubArm(stubArm.Count - 1)
        Dim okStub As Boolean = (sTail.Timestamp = closeMs) AndAlso
                                (Math.Abs(sTail.Volume - (0.4 / 64100.0)) < 1e-12) AndAlso
                                (Math.Abs(sTail.VolumeUSD - 0.4) < 1e-9) AndAlso
                                (Math.Abs(sTail.Close - 64100.0) < 1e-9)

        ' (d) The arms share their RECENT history exactly, offset by one: holding total
        '     length at N means the closed arm reaches one bar FURTHER BACK than the stub
        '     arm. So stubArm(k) must equal closedArm(k+1) across the whole overlap.
        '     Consequence, pinned here so it is never rediscovered as a surprise: the two
        '     arms differ in exactly TWO places — the terminal bar (the thing under test)
        '     and one extra OLD bar in the closed arm. For tail-window indicators
        '     (VolumeRatio's SMA-9, ATR-7) the old bar falls outside the window and the
        '     comparison is clean; for full-series indicators (VWAP session window, OBV
        '     meanVol, BBW percentile series) it is a ~1/N perturbation that rides along.
        Dim okOverlap As Boolean = True
        For k = 0 To want - 2
            If stubArm(k).Timestamp <> closedArm(k + 1).Timestamp OrElse
               Math.Abs(stubArm(k).Volume - closedArm(k + 1).Volume) > 1e-9 Then
                okOverlap = False : Exit For
            End If
        Next
        ' and the closed arm's extra bar really is older than anything the stub arm holds
        Dim okOlder As Boolean = (closedArm(0).Timestamp < stubArm(0).Timestamp)

        ' (e) the closed arm carries MORE terminal volume than the stub arm — the effect
        '     the A/B exists to measure (a full bar vs ~2 seconds of it).
        Dim okVolGap As Boolean = (cTail.Volume > sTail.Volume)

        Check("A47a closed-bar arm — equal window length · real terminal bar · stub arm keeps its stub · overlap identical (offset 1) · terminal volume gap",
              okLen AndAlso okReal AndAlso okStub AndAlso okOverlap AndAlso okOlder AndAlso okVolGap,
              String.Format("len={0}({1}/{2}) real={3} stub={4} overlap={5} older={6} volGap={7} (closed {8} vs stub {9})",
                            okLen, stubArm.Count, closedArm.Count, okReal, okStub, okOverlap,
                            okOlder, okVolGap, cTail.Volume, sTail.Volume))
    End Sub

    ' A47b: the forming stub's volume must be COMMENSURATE with the candle series it is
    ' appended to. This is the pin that was missing.
    '
    ' A43f verified the stub's internal arithmetic (Σ over the window) against hand-computed
    ' sums — and passed, while the stub was writing USD notional into a BTC-denominated
    ' Candle.Volume. An internal-consistency check cannot catch a unit error; only a
    ' cross-series check can. Real store scale: 1m candle Volume ≈ 2.4 BTC, trade Amount
    ' ≈ $2,909, price ≈ $64k. A 2-second slice of a 60-second bar must therefore carry a
    ' SMALL FRACTION of that bar's volume — never a multiple of it.
    Private Sub A47b_StubVolumeIsCommensurateWithCandleVolume()
        Const px      As Double = 64000.0
        Const closeMs As Long   = 1785321600000L
        Const realBarVolBtc As Double = 2.4        ' store-typical 1m volume, BTC

        ' ~2 s of tape: three trades totalling $9,000 notional.
        Dim wnd As New List(Of TradeRecord) From {
            New TradeRecord With {.Price = px, .Amount = 4000, .Direction = "buy",  .Liquidation = "none", .Timestamp = closeMs + 100L},
            New TradeRecord With {.Price = px, .Amount = 3000, .Direction = "sell", .Liquidation = "none", .Timestamp = closeMs + 900L},
            New TradeRecord With {.Price = px, .Amount = 2000, .Direction = "buy",  .Liquidation = "none", .Timestamp = closeMs + 1700L}}

        Dim stub = ReplayLoop.BuildFormingStub(px, wnd, closeMs)

        ' (a) Volume is BTC: $9,000 / $64,000 = 0.140625 BTC.
        Dim okBtc As Boolean = Math.Abs(stub.Volume - (9000.0 / px)) < 1e-12

        ' (b) VolumeUSD is the raw USD notional.
        Dim okUsd As Boolean = Math.Abs(stub.VolumeUSD - 9000.0) < 1e-9

        ' (c) the two agree through price — the self-consistency that makes them the same
        '     pair of units the chart endpoint delivers (volume + cost).
        Dim okPair As Boolean = Math.Abs(stub.VolumeUSD - stub.Volume * px) < 1e-6

        ' (d) THE PIN THAT CATCHES THE BUG: a 2-second stub must be a small fraction of a
        '     real one-minute bar, never a multiple. Pre-fix this read 9000 vs 2.4 — a
        '     3,750x OVERSHOOT — which drove VolumeRatio to a degenerate 0-or-~9 split and
        '     dominated the volume-weighted VWAP on every row that had any tape.
        Dim okScale As Boolean = (stub.Volume < realBarVolBtc) AndAlso
                                 (stub.Volume / realBarVolBtc < 0.25)

        ' (e) zero-trade fallback still carries no volume in either unit.
        Dim empty = ReplayLoop.BuildFormingStub(px, New List(Of TradeRecord)(), closeMs)
        Dim okEmpty As Boolean = (empty.Volume = 0) AndAlso (empty.VolumeUSD = 0)

        Check("A47b stub volume commensurate with the candle series — BTC in .Volume · USD in .VolumeUSD · pair agrees through price · 2s stub << 1m bar",
              okBtc AndAlso okUsd AndAlso okPair AndAlso okScale AndAlso okEmpty,
              String.Format("btc={0}({1:F6}) usd={2}({3:F2}) pair={4} scale={5}(frac {6:F4}) empty={7}",
                            okBtc, stub.Volume, okUsd, stub.VolumeUSD, okPair,
                            okScale, stub.Volume / realBarVolBtc, okEmpty))
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A48 — in-app trade-store capture (docs/in-app-trade-store-capture-proposal.md §6)
    '
    ' Everything below runs against Core/TradeStoreWriter.vb, which is NETWORK-FREE by
    ' design — that is the point of the §2 split, which keeps an HttpClient off the app's
    ' feed path. These fixtures pin the shared seam rather than the HTTP wrapper around it.
    '
    ' [CORRECTION 2026-07-31] An earlier version of this comment, and the v64 spec-back §2.1,
    ' claimed HistoricalStore is "deliberately NOT linked into this project". That is false —
    ' OrderCheck.vbproj:146 has linked it since the A43 family. The real constraint is that
    ' the store's backfill entry points are Async and call the network, so a fixture cannot
    ' drive them; that is why A51 tests the extracted pure layer (Core/StoreFiles.vb) instead.
    ' ═══════════════════════════════════════════════════════════════════════════════════

    ' Scratch store dir under the system temp root; each fixture gets its own.
    Private Function A48TempStore(tag As String) As String
        Dim dir As String = Path.Combine(Path.GetTempPath(),
                                         "ordercheck_a48_" & tag & "_" & Guid.NewGuid().ToString("N").Substring(0, 8))
        Directory.CreateDirectory(dir)
        Return dir
    End Function

    Private Sub A48Cleanup(dir As String)
        Try
            If Directory.Exists(dir) Then Directory.Delete(dir, recursive:=True)
        Catch
        End Try
    End Sub

    ' A 2026-07-20 12:00 UTC base, well inside a single month.
    Private Function A48Ms(offsetMs As Long) As Long
        Return New DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() + offsetMs
    End Function

    Private Function A48Trade(tsMs As Long, px As Double, amt As Double,
                              dir As String, Optional liq As String = "none") As TradeRecord
        Return New TradeRecord With {
            .Timestamp = tsMs, .Price = px, .Amount = amt, .Direction = dir, .Liquidation = liq}
    End Function

    ' Buffer every trade and return how many the write guard ACCEPTED. An explicit loop
    ' rather than Count(predicate): the predicate has a side effect, so relying on LINQ to
    ' evaluate it exactly once per element would be the wrong kind of clever.
    Private Function A48BufferAll(w As TradeStoreWriter, batch As List(Of TradeRecord)) As Integer
        Dim n As Integer = 0
        For Each t In batch
            If w.Buffer(t) Then n += 1
        Next
        Return n
    End Function

    ' -- A48a: appended rows are byte-compatible with the shipped reader -------------------
    ' Write through the streaming path (Buffer → Flush) and read back through
    ' TradeStoreWriter.ReadTradeFile — which is exactly the per-file parse
    ' HistoricalStore.LoadTradeRange delegates to since v64, so a clean round-trip here IS
    ' compatibility with the shipped reader. Also pins the on-disk shape: the shipped header
    ' verbatim, 5 comma-separated columns, F2 price/amount, and the liq flag preserved
    ' (liquidation rows are the whole reason the store carries a fifth column).
    Private Sub A48a_AppendedRowsRoundTripThroughShippedReader()
        Dim dir As String = A48TempStore("a")
        Try
            Dim w As New TradeStoreWriter(dir)
            Dim src As New List(Of TradeRecord) From {
                A48Trade(A48Ms(0), 64000.5, 1250.0, "buy"),
                A48Trade(A48Ms(250), 64001.25, 730.5, "sell"),
                A48Trade(A48Ms(900), 63999.0, 40000.0, "sell", "T")}
            For Each t In src
                w.Buffer(t)
            Next
            Dim flushed As Integer = w.Flush()

            Dim path As String = TradeStoreWriter.TradeFileFor(dir, 2026, 7)
            Dim lines = File.ReadAllLines(path)
            ' [trade identity, 2026-08-08] The on-disk shape moved from 5 columns to 7. These
            ' three assertions are UPDATED, not relaxed: the header is the seven-column one, and
            ' every row still has a fixed, known column count.
            Dim headerOk As Boolean = lines.Length = 4 AndAlso
                                      lines(0) = "Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq"
            Dim shapeOk As Boolean = lines.Skip(1).All(Function(l) l.Split(","c).Length = 7)
            ' F2 formatting on price + amount, invariant culture (no comma decimal separator).
            ' These synthetic trades carry NO identity, so both new columns are EMPTY — and the
            ' first five fields are byte-identical to what every pre-identity binary wrote, which
            ' is the backward-compatibility claim in D5 stated as a literal.
            Dim legacyPrefix As String = A48Ms(0) & ",64000.50,1250.00,buy,none"
            Dim fmtOk As Boolean = lines(1) = legacyPrefix & ",," AndAlso
                                   TradeStoreWriter.LegacyRowKey(src(0)) = legacyPrefix

            Dim back = TradeStoreWriter.ReadTradeFile(path)
            Dim countOk As Boolean = back.Count = src.Count
            Dim valuesOk As Boolean = countOk
            If countOk Then
                For i As Integer = 0 To src.Count - 1
                    If back(i).Timestamp <> src(i).Timestamp OrElse
                       Math.Abs(back(i).Price - src(i).Price) > 0.005 OrElse
                       Math.Abs(back(i).Amount - src(i).Amount) > 0.005 OrElse
                       back(i).Direction <> src(i).Direction OrElse
                       back(i).Liquidation <> src(i).Liquidation Then
                        valuesOk = False
                    End If
                Next
            End If

            Check("A48a store round-trip — 7-col header + F2 rows + liq flag + empty identity survive write→read",
                  flushed = 3 AndAlso headerOk AndAlso shapeOk AndAlso fmtOk AndAlso valuesOk,
                  String.Format("flushed={0} header={1} shape={2} fmt={3}('{4}') count={5} values={6}",
                                flushed, headerOk, shapeOk, fmtOk, If(lines.Length > 1, lines(1), "<none>"),
                                countOk, valuesOk))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A48b: write guard — replaying an identical batch twice writes once -----------------
    ' This is what makes reconnect re-seed idempotent: SeedAsync re-seeds the trade ring from
    ' REST on every (re)connect, so the same trades WILL arrive twice. Three sub-cases:
    '   (1) an identical replay in the same writer is fully dropped,
    '   (2) a fresh writer over the SAME dir re-seeds its guard FROM DISK (the restart case),
    '   (3) genuinely newer trades in a replayed batch still get through (the guard must not
    '       be a batch-level "seen this before").
    '
    ' ⚠ These trades are one SECOND apart, and that is why this fixture — named "monotonic
    ' guard" until 2026-08-11 — passed for ten days while the guard discarded half the tape.
    ' It never presented two trades on the same millisecond, so the defect it was closest to
    ' was never in its input. A55a-g are the fixtures that actually reach it. This one keeps
    ' its original job: the guard's REASON TO EXIST must not regress.
    Private Sub A48b_MonotonicGuardDropsReplayedBatch()
        Dim dir As String = A48TempStore("b")
        Try
            Dim batch As New List(Of TradeRecord) From {
                A48Trade(A48Ms(0), 64000, 100, "buy"),
                A48Trade(A48Ms(100), 64001, 200, "sell"),
                A48Trade(A48Ms(200), 64002, 300, "buy")}

            Dim w As New TradeStoreWriter(dir)
            Dim accepted1 As Integer = A48BufferAll(w, batch)
            w.Flush()
            Dim accepted2 As Integer = A48BufferAll(w, batch)   ' identical replay
            w.Flush()
            Dim path As String = TradeStoreWriter.TradeFileFor(dir, 2026, 7)
            Dim afterReplay As Integer = TradeStoreWriter.ReadTradeFile(path).Count

            ' (2) restart: a brand-new writer seeds its guard from the on-disk high-water mark.
            Dim w2 As New TradeStoreWriter(dir)
            Dim accepted3 As Integer = A48BufferAll(w2, batch)
            w2.Flush()
            Dim afterRestart As Integer = TradeStoreWriter.ReadTradeFile(path).Count

            ' (3) a mixed batch: two already-seen + one newer ⇒ only the newer lands.
            Dim mixed As New List(Of TradeRecord) From {
                A48Trade(A48Ms(100), 64001, 200, "sell"),
                A48Trade(A48Ms(200), 64002, 300, "buy"),
                A48Trade(A48Ms(350), 64003, 400, "sell")}
            Dim accepted4 As Integer = A48BufferAll(w2, mixed)
            w2.Flush()
            Dim final = TradeStoreWriter.ReadTradeFile(path)

            Check("A48b write guard — identical replay writes once · fresh writer re-seeds from disk · newer trades still land",
                  accepted1 = 3 AndAlso accepted2 = 0 AndAlso afterReplay = 3 AndAlso
                  accepted3 = 0 AndAlso afterRestart = 3 AndAlso
                  accepted4 = 1 AndAlso final.Count = 4 AndAlso final(3).Timestamp = A48Ms(350),
                  String.Format("acc1={0} acc2={1} afterReplay={2} acc3={3} afterRestart={4} acc4={5} final={6}",
                                accepted1, accepted2, afterReplay, accepted3, afterRestart,
                                accepted4, final.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A48c: month rollover — batch straddling the boundary lands in two files -----------
    ' Header written ONLY on create: the July file already exists and must NOT gain a second
    ' header line when the straddling batch appends to it, while the freshly-created August
    ' file must get one.
    Private Sub A48c_MonthRolloverSplitsAndHeadersOnCreateOnly()
        Dim dir As String = A48TempStore("c")
        Try
            Dim julEnd As Long = New DateTimeOffset(2026, 7, 31, 23, 59, 59, TimeSpan.Zero).ToUnixTimeMilliseconds()
            Dim augStart As Long = New DateTimeOffset(2026, 8, 1, 0, 0, 1, TimeSpan.Zero).ToUnixTimeMilliseconds()

            Dim w As New TradeStoreWriter(dir)
            ' Pass 1 — July only, so the July file pre-exists when the straddle arrives.
            w.Buffer(A48Trade(julEnd - 5000, 64000, 100, "buy"))
            w.Flush()

            ' Pass 2 — one batch straddling the boundary.
            w.Buffer(A48Trade(julEnd, 64010, 110, "sell"))
            w.Buffer(A48Trade(augStart, 64020, 120, "buy"))
            w.Buffer(A48Trade(augStart + 1000, 64030, 130, "sell"))
            Dim written As Integer = w.Flush()

            Dim julPath As String = TradeStoreWriter.TradeFileFor(dir, 2026, 7)
            Dim augPath As String = TradeStoreWriter.TradeFileFor(dir, 2026, 8)
            Dim julLines = File.ReadAllLines(julPath)
            Dim augLines = File.ReadAllLines(augPath)

            Dim namesOk As Boolean = Path.GetFileName(julPath) = "trades_2026-07.csv" AndAlso
                                     Path.GetFileName(augPath) = "trades_2026-08.csv"
            ' July: 1 header + 2 rows, and exactly ONE header line in the whole file.
            Dim julOk As Boolean = julLines.Length = 3 AndAlso
                                   julLines.Count(Function(l) l.StartsWith("Timestamp,")) = 1
            ' August: created by this batch ⇒ header + 2 rows.
            Dim augOk As Boolean = augLines.Length = 3 AndAlso
                                   augLines(0) = TradeStoreWriter.HeaderLine

            Check("A48c month rollover — straddling batch splits across two files, header on create only",
                  written = 3 AndAlso namesOk AndAlso julOk AndAlso augOk,
                  String.Format("written={0} names={1} julLines={2}(hdrs={3}) augLines={4}",
                                written, namesOk, julLines.Length,
                                julLines.Count(Function(l) l.StartsWith("Timestamp,")), augLines.Length))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A48d: gap-repair overlap is a no-op ----------------------------------------------
    ' The proposal's claim is "overlap is a no-op BY CONSTRUCTION" (§1.2) because the backfill
    ' resumes from the last on-disk timestamp. That decision lives on the shared seam
    ' (TradeStoreWriter.ResolveResumeCursorMs), which HistoricalStore.BackfillTradeMonthAsync
    ' calls verbatim — so this exercises the REAL decision without a live HTTP call.
    '
    '   (1) stream a window, then repair the SAME window ⇒ cursor -1 (nothing to fetch),
    '   (2) a gap: stream, then repair a window extending past it ⇒ cursor resumes at
    '       last+1, i.e. it fetches only the gap and not the captured ground,
    '   (3) the clamp: after an outage longer than the lookback, the cursor is pulled forward
    '       to segStart so the fetch stays inside Deribit's ~24 h retention instead of asking
    '       for a refused window,
    '   (4) belt-and-braces at READ time — if an overlap ever did double-write, the reader
    '       dedups on the whole row, so LoadTradeRange still yields no duplicates.
    Private Sub A48d_GapRepairOverlapIsNoOp()
        Dim dir As String = A48TempStore("d")
        Try
            Dim w As New TradeStoreWriter(dir)
            For i As Integer = 0 To 4
                w.Buffer(A48Trade(A48Ms(i * 1000L), 64000 + i, 100, "buy"))
            Next
            w.Flush()
            Dim path As String = TradeStoreWriter.TradeFileFor(dir, 2026, 7)
            Dim lastTs As Long = TradeStoreWriter.LastTradeTimestamp(path)

            ' (1) repair the exact captured window ⇒ already covered.
            Dim c1 As Long = TradeStoreWriter.ResolveResumeCursorMs(path, A48Ms(0), lastTs, clampToSegStart:=True)
            ' (2) repair a window running 60s past the captured tail ⇒ resume at last+1.
            Dim c2 As Long = TradeStoreWriter.ResolveResumeCursorMs(path, A48Ms(0), A48Ms(60000), clampToSegStart:=True)
            ' (3) outage longer than the lookback: segStart is AFTER the stale on-disk tail,
            '     so the clamp pulls the cursor forward to segStart.
            Dim segStartLate As Long = A48Ms(30L * 3600L * 1000L)     ' 30 h after the captured tail
            Dim c3 As Long = TradeStoreWriter.ResolveResumeCursorMs(path, segStartLate, segStartLate + 3600000L,
                                                                    clampToSegStart:=True)
            ' ...and the historical backfill (clamp off) still fills the whole hole.
            Dim c3NoClamp As Long = TradeStoreWriter.ResolveResumeCursorMs(path, segStartLate, segStartLate + 3600000L,
                                                                           clampToSegStart:=False)

            ' (4) [DR-3] the SAME covered window as case (1), driven through the real entry
            ' point rather than the cursor helper it depends on: BackfillTradeMonthAsync must
            ' report 0 rows appended when there is nothing to fetch. Pre-fix it returns
            ' CountDataRows(path) — the whole file's row count (5 here) — which is why every
            ' healthy gap-repair pass over a multi-hundred-thousand-row month logged that
            ' figure as "rows appended". windows.Count = 0 is reached without any network call
            ' (cursor -1 short-circuits before the fetch loop), so this is safe to drive live.
            Dim segStart5 As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(A48Ms(0)).UtcDateTime
            Dim segEndExcl5 As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(lastTs + 1L).UtcDateTime
            Dim n5 As Integer = HistoricalStore.BackfillTradeMonthAsync(
                2026, 7, segStart5, segEndExcl5, storeDir:=dir, clampToSegStart:=True).GetAwaiter().GetResult()

            ' (5) force a double-write of the same rows, then read back.
            Dim dupRows = TradeStoreWriter.ReadTradeFile(path)
            TradeStoreWriter.AppendRows(dir, dupRows)
            Dim raw = File.ReadAllLines(path).Length - 1
            ' [trade identity] Routes through the production dedup contract rather than
            ' restating it. These rows carry no identity, so the §3.4 fallback arm decides —
            ' which is what makes the overlap no-op survive the schema change unchanged.
            Dim deduped As Integer = TradeStoreWriter.DedupTrades(TradeStoreWriter.ReadTradeFile(path)).Count

            Check("A48d gap-repair overlap is a no-op — covered window ⇒ no fetch · gap resumes at last+1 · retention clamp · read-time dedup · DR-3 reports 0 rows appended",
                  c1 = -1 AndAlso c2 = lastTs + 1 AndAlso c3 = segStartLate AndAlso
                  c3NoClamp = lastTs + 1 AndAlso raw = 10 AndAlso deduped = 5 AndAlso n5 = 0,
                  String.Format("c1={0} c2={1}(want {2}) c3={3}(want {4}) c3NoClamp={5} rawRows={6} deduped={7} n5={8}(want 0)",
                                c1, c2, lastTs + 1, c3, segStartLate, c3NoClamp, raw, deduped, n5))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A48e: unwritable path never throws and never blocks the fold ----------------------
    ' The SignalEmitter.TryWrite / liq_events.log discipline: losing capture must never kill
    ' the feed. Two unwritable shapes — a store "directory" that is actually a FILE (so
    ' CreateDirectory fails), and a target file locked open by another handle (so the append
    ' fails). Both must return 0 and leave the caller running.
    Private Sub A48e_UnwritablePathNeverThrows()
        Dim root As String = A48TempStore("e")
        Dim threw As String = ""
        Dim wroteBlocked As Integer = -1
        Dim wroteLocked As Integer = -1
        Dim keptBuffering As Boolean = False
        Try
            ' (1) store dir path occupied by a file.
            Dim asFile As String = Path.Combine(root, "not_a_dir")
            File.WriteAllText(asFile, "x")
            Dim w As New TradeStoreWriter(asFile)
            w.Buffer(A48Trade(A48Ms(0), 64000, 100, "buy"))
            Try
                wroteBlocked = w.Flush()
            Catch ex As Exception
                threw = "flush-blocked: " & ex.Message
            End Try
            ' The fold keeps working afterwards — a capture failure is not a poison pill.
            keptBuffering = w.Buffer(A48Trade(A48Ms(1000), 64001, 200, "sell"))

            ' (2) target file locked open by another handle.
            Dim lockedDir As String = Path.Combine(root, "locked")
            Directory.CreateDirectory(lockedDir)
            Dim lockedPath As String = TradeStoreWriter.TradeFileFor(lockedDir, 2026, 7)
            File.WriteAllText(lockedPath, "Timestamp,Price,Amount,Direction,Liquidation" & vbLf)
            Using hold As New FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)
                Try
                    wroteLocked = TradeStoreWriter.AppendRows(
                        lockedDir, New List(Of TradeRecord) From {A48Trade(A48Ms(0), 64000, 100, "buy")})
                Catch ex As Exception
                    threw &= " | append-locked: " & ex.Message
                End Try
            End Using

            Check("A48e unwritable store never throws — blocked dir + locked file both return 0, fold keeps running",
                  threw = "" AndAlso wroteBlocked = 0 AndAlso wroteLocked = 0 AndAlso keptBuffering,
                  String.Format("threw='{0}' blocked={1} locked={2} keptBuffering={3}",
                                threw, wroteBlocked, wroteLocked, keptBuffering))
        Finally
            A48Cleanup(root)
        End Try
    End Sub

    ' -- A48f: enabled:false ⇒ zero writes, fold inert -------------------------------------
    ' The reversibility claim (§4).
    '
    ' [F1 fix] This calls the PRODUCTION gates — TradeStoreWriter.ShouldCapture /
    ' ShouldGapRepair, the same functions DeribitWsFeed.ResolveTradeStore and
    ' TradeStoreGapRepair call. It previously re-stated the predicate inline and asserted the
    ' copy was false, which would have kept passing if the real gate lost its `Not ts.Enabled`
    ' arm — the A43f shape (internal consistency of a mirror proves nothing about the thing it
    ' mirrors). The gate decision is now genuinely harness-proven.
    '
    ' What remains REASONED rather than proven: that a closed gate makes the ApplyTrades fold
    ' inert. `ApplyTrades` is private on DeribitWsFeed, which owns a ClientWebSocket the
    ' harness deliberately does not link (the A22/A37 boundary). The fixture proves the gate
    ' answers false and that no file appears; it does not drive the feed.
    Private Sub A48f_DisabledMeansZeroWrites()
        Dim dir As String = A48TempStore("f")
        Try
            ' The shipped POCO defaults — what an absent settings.json block resolves to.
            Dim def As New TradeStoreSettings()
            Dim defaultsOk As Boolean = def.Enabled AndAlso def.StoreDir = "backtest_data" AndAlso
                                        def.FlushSeconds = 30 AndAlso def.FlushTradeCount = 500 AndAlso
                                        def.GapRepairEnabled AndAlso
                                        Math.Abs(def.GapRepairIntervalHours - 6.0) < 1e-9 AndAlso
                                        Math.Abs(def.GapRepairLookbackHours - 20.0) < 1e-9

            ' Disabled ⇒ the SHIPPED capture gate answers false, so ResolveTradeStore builds no
            ' writer and nothing reaches the disk. Nothing/enabled arms both pinned.
            Dim off As New TradeStoreSettings() With {.Enabled = False, .StoreDir = dir}
            Dim gateOpen As Boolean = TradeStoreWriter.ShouldCapture(off)
            Dim gateNothing As Boolean = TradeStoreWriter.ShouldCapture(Nothing)
            Dim gateOn As Boolean = TradeStoreWriter.ShouldCapture(
                New TradeStoreSettings() With {.Enabled = True, .StoreDir = dir})
            Dim filesBefore As Integer = Directory.GetFiles(dir).Length
            If gateOpen Then
                Dim w As New TradeStoreWriter(dir)
                w.Buffer(A48Trade(A48Ms(0), 64000, 100, "buy"))
                w.Flush()
            End If
            Dim filesAfter As Integer = Directory.GetFiles(dir).Length

            ' Gap repair honours BOTH switches independently — the SHIPPED gate, all four arms.
            Dim rOff As Boolean = TradeStoreWriter.ShouldGapRepair(
                New TradeStoreSettings() With {.Enabled = False, .GapRepairEnabled = True, .StoreDir = dir})
            Dim rRepairOff As Boolean = TradeStoreWriter.ShouldGapRepair(
                New TradeStoreSettings() With {.Enabled = True, .GapRepairEnabled = False, .StoreDir = dir})
            Dim rBothOn As Boolean = TradeStoreWriter.ShouldGapRepair(
                New TradeStoreSettings() With {.Enabled = True, .GapRepairEnabled = True, .StoreDir = dir})
            Dim rNothing As Boolean = TradeStoreWriter.ShouldGapRepair(Nothing)

            Check("A48f shipped capture/repair gates — enabled:false ⇒ no writer, no files; both repair switches independent; defaults pinned",
                  defaultsOk AndAlso Not gateOpen AndAlso Not gateNothing AndAlso gateOn AndAlso
                  filesBefore = 0 AndAlso filesAfter = 0 AndAlso
                  Not rOff AndAlso Not rRepairOff AndAlso rBothOn AndAlso Not rNothing,
                  String.Format("defaults={0} capture(off={1} nothing={2} on={3}) files={4}/{5} repair(off={6} repairOff={7} bothOn={8} nothing={9})",
                                defaultsOk, gateOpen, gateNothing, gateOn, filesBefore, filesAfter,
                                rOff, rRepairOff, rBothOn, rNothing))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A48g: HARD CONSTRAINT 27 fences every trade_store.* key ---------------------------
    ' Data-capture plumbing has no failure-rate linkage — the same class as alerts.* (HC25),
    ' exit_guard.*, live_strip.*, signal_bridge.*. Prefix-safe: a sibling scoring.* tunable
    ' must still pass, which is what makes this a fence and not a blanket.
    Private Sub A48g_Hc27FencesTradeStoreKeys()
        Dim s As String = "{""version"":64,""scoring"":{""bbw_squeeze_penalty"":2}," &
                          """trade_store"":{""enabled"":true,""store_dir"":""backtest_data""," &
                          """flush_seconds"":30,""flush_trade_count"":500,""gap_repair_enabled"":true," &
                          """gap_repair_interval_hours"":6,""gap_repair_lookback_hours"":20}}"

        Dim keys As String() = {"trade_store.enabled", "trade_store.store_dir",
                                "trade_store.flush_seconds", "trade_store.flush_trade_count",
                                "trade_store.gap_repair_enabled", "trade_store.gap_repair_interval_hours",
                                "trade_store.gap_repair_lookback_hours"}
        Dim allRejected As Boolean = True
        Dim allCiteHc As Boolean = True
        For Each k In keys
            Dim r = SettingsDiffApplier.Validate(OneDiff(k, "30", "60"), s, 3)
            If r.IsValid Then allRejected = False
            If Not r.ErrorReason.Contains("HARD CONSTRAINT") Then allCiteHc = False
        Next

        ' Sibling scoring.* tunable still proposable — the prefix must not over-match.
        Dim rCtl = SettingsDiffApplier.Validate(OneDiff("scoring.bbw_squeeze_penalty", "2", "3"), s, 3)
        ' And the prompt tells the model the same thing the applier enforces — a fence the
        ' code rejects but the prompt never mentions burns a round on a doomed proposal.
        Dim built = PromptBuilder.Build(
            settingsJson:=s,
            csvRows:=New List(Of CsvRow)(),
            failureCells:=New List(Of FailureCellResult)(),
            pickedCellHistory:=New List(Of PickedCellEntry)(),
            trigger:="a48g",
            manifestActiveRows:="",
            conditions:=Nothing,
            maxKeysPerProposal:=3)
        Dim promptOk As Boolean = built.SystemMsg.Contains("'trade_store.*'") AndAlso
                                  built.SystemMsg.Contains("27. Never propose")

        Check("A48g HC27 rejects all seven trade_store.* keys, sibling scoring tunable passes, prompt rule 27 present",
              allRejected AndAlso allCiteHc AndAlso rCtl.IsValid AndAlso promptOk,
              String.Format("allRejected={0} citeHc={1} ctlValid={2} promptOk={3}",
                            allRejected, allCiteHc, rCtl.IsValid, promptOk))
    End Sub

    ' -- A48h: store_dir resolves EXE-relative, never CWD-relative -------------------------
    ' D3's whole point. The app's working directory is not guaranteed (a shortcut, a service
    ' host and a debugger all set it differently), so a cwd-relative store would silently
    ' scatter capture files. The pin: resolve the same configured value from two different
    ' working directories and get the same absolute path — which must sit under the exe dir.
    Private Sub A48h_StoreDirIsExeRelativeNotCwdRelative()
        Dim originalCwd As String = Directory.GetCurrentDirectory()
        Dim scratch As String = A48TempStore("h")
        Try
            Dim baseDir As String = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory)

            Dim r1 As String = TradeStoreWriter.ResolveStoreDir("backtest_data")
            Directory.SetCurrentDirectory(scratch)
            Dim r2 As String = TradeStoreWriter.ResolveStoreDir("backtest_data")
            Dim cwdMoved As String = Path.GetFullPath(Directory.GetCurrentDirectory())
            Directory.SetCurrentDirectory(originalCwd)

            Dim stableOk As Boolean = String.Equals(r1, r2, StringComparison.OrdinalIgnoreCase)
            Dim underExeOk As Boolean = r1.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase)
            ' The cwd genuinely moved during the test, so stability is a real result.
            Dim cwdActuallyMoved As Boolean =
                Not String.Equals(cwdMoved.TrimEnd(Path.DirectorySeparatorChar),
                                  Path.GetFullPath(originalCwd).TrimEnd(Path.DirectorySeparatorChar),
                                  StringComparison.OrdinalIgnoreCase)
            Dim notCwdOk As Boolean = Not r2.StartsWith(cwdMoved, StringComparison.OrdinalIgnoreCase)
            ' Absolute configured paths pass through untouched.
            Dim absIn As String = Path.Combine(scratch, "explicit_store")
            Dim absOk As Boolean = String.Equals(TradeStoreWriter.ResolveStoreDir(absIn),
                                                 Path.GetFullPath(absIn), StringComparison.OrdinalIgnoreCase)
            ' Empty ⇒ the shipped default name, still exe-anchored.
            Dim emptyOk As Boolean = String.Equals(TradeStoreWriter.ResolveStoreDir(""), r1,
                                                   StringComparison.OrdinalIgnoreCase)

            Check("A48h store_dir is exe-relative — same path from two working directories, never under cwd; absolute passes through",
                  stableOk AndAlso underExeOk AndAlso cwdActuallyMoved AndAlso notCwdOk AndAlso absOk AndAlso emptyOk,
                  String.Format("stable={0} underExe={1} cwdMoved={2} notCwd={3} abs={4} empty={5} r1='{6}'",
                                stableOk, underExeOk, cwdActuallyMoved, notCwdOk, absOk, emptyOk, r1))
        Finally
            Try
                Directory.SetCurrentDirectory(originalCwd)
            Catch
            End Try
            A48Cleanup(scratch)
        End Try
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A49 — trade-store coverage report `coverage` verb (Session 1)
    ' docs/trade-store-coverage-report-proposal.md, docs/trade-store-coverage-report
    ' -implementer-brief.md, docs/j-b-scoping-ruling-2026-08-02.md,
    ' docs/weekday-scope-ruling-2026-08-03.md.
    '
    ' A49a-j are the proposal's own §6 arms; A49k/A49l are the two the rulings add
    ' (A49m — the weekday+Part B pairing — is Session 2, per the implementer brief split).
    ' ═══════════════════════════════════════════════════════════════════════════════════

    Private Function A49TempStore(tag As String) As String
        Dim dir As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                         "ordercheck_a49_" & tag & "_" & Guid.NewGuid().ToString("N").Substring(0, 8))
        Directory.CreateDirectory(dir)
        Return dir
    End Function

    Private Sub A49Cleanup(dir As String)
        Try
            If Directory.Exists(dir) Then Directory.Delete(dir, recursive:=True)
        Catch
        End Try
    End Sub

    ' A Monday, so the six-day walk in most fixtures stays inside weekday scope without
    ' needing to reason about the weekend carve-out (that is A49m's job, Session 2).
    Private Function A49Monday() As DateTime
        Return New DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc)
    End Function

    Private Function A49Ms(dt As DateTime) As Long
        Return New DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
    End Function

    Private Function A49WsLine(dt As DateTime, state As String, iid As String) As String
        Return dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) & " | " & state & " | " & iid
    End Function

    Private Function A49MarkerLine(dt As DateTime, enabled As Boolean, storeDir As String, iid As String) As String
        Return dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) & " | " &
               enabled.ToString(CultureInfo.InvariantCulture) & " | " & storeDir & " | " & iid
    End Function

    Private Function A49CsvRow(dt As DateTime, iid As String) As String
        ' Only Timestamp (col 0) and InstanceId matter to ParseAnalysisLogEvidence, which
        ' resolves both by header name — pad the rest with a matching column count isn't
        ' required since the parser only reads the two indices it finds by name.
        Return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & "," & iid
    End Function

    Private Function A49Trade(tsMs As Long, px As Double) As TradeRecord
        Return New TradeRecord With {.Timestamp = tsMs, .Price = px, .Amount = 100.0, .Direction = "buy", .Liquidation = "none"}
    End Function

    ' -- A49a: uptime parse across two process lives -------------------------------------
    Private Sub A49a_UptimeParseAcrossTwoProcessLives()
        Dim day = A49Monday()
        Dim lines As New List(Of String) From {
            A49WsLine(day.AddHours(0), "DOWN", "iid-1"),
            A49WsLine(day.AddHours(0).AddMinutes(5), "OK", "iid-1"),
            A49WsLine(day.AddHours(2), "DEGRADED", "iid-1"),   ' ends WITHOUT a DOWN line
            A49WsLine(day.AddHours(10), "DOWN", "iid-2"),      ' restart — new instance
            A49WsLine(day.AddHours(10).AddMinutes(30), "OK", "iid-2")   ' iid-2 is the open-ended TRAILING life — no further lines
        }
        Dim evidence = CoverageReport.ParseWsHealthEvidence(lines)
        Dim intervals = CoverageReport.BuildUpIntervals(evidence)

        Dim iv1 = intervals.FirstOrDefault(Function(iv) iv.InstanceId = "iid-1")
        Dim iv2 = intervals.FirstOrDefault(Function(iv) iv.InstanceId = "iid-2")
        Dim ok As Boolean = intervals.Count = 2 AndAlso
                            iv1 IsNot Nothing AndAlso Not iv1.IsTrailing AndAlso
                            iv1.FirstUtcMs = A49Ms(day.AddHours(0)) AndAlso
                            iv1.LastUtcMs = A49Ms(day.AddHours(2)) AndAlso
                            iv2 IsNot Nothing AndAlso iv2.IsTrailing AndAlso
                            iv2.FirstUtcMs = A49Ms(day.AddHours(10)) AndAlso
                            iv2.LastUtcMs = A49Ms(day.AddHours(10).AddMinutes(30))

        Check("A49a uptime parse — two process lives resolve to correct up-intervals; " &
              "iid-1 closes on its last DEGRADED line (no DOWN needed); iid-2 is the open trailing interval",
              ok, String.Format("count={0} iv1.trailing={1} iv2.trailing={2}",
                                intervals.Count, If(iv1 Is Nothing, "?", iv1.IsTrailing.ToString()),
                                If(iv2 Is Nothing, "?", iv2.IsTrailing.ToString())))
    End Sub

    ' -- A49b: the S1 join — up-but-uncaptured (defect) vs app-down (expected-missing) ----
    Private Sub A49b_S1JoinUpButUncapturedVsAppDown()
        Dim day = A49Monday()
        Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(day.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"}
        }
        ' One up-interval, 04:00 → 07:30. Hours before it (app-down / before-first) must
        ' read expected-missing; hour 04 (up, but the store is silent) must read defect;
        ' hours 05-07 (up, store clean) must read captured.
        Dim upIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(day.AddHours(4)),
                                 .LastUtcMs = A49Ms(day.AddHours(7).AddMinutes(30)), .IsTrailing = True}
        }
        Dim cleanStats As New HourStoreStats With {.RowCount = 10, .LongestGapMs = 1000}

        Dim h00 = CoverageReport.ClassifyHour(day.AddHours(0), markers, upIntervals, False, Nothing, 300000L)
        Dim h03 = CoverageReport.ClassifyHour(day.AddHours(3), markers, upIntervals, False, Nothing, 300000L)
        Dim h04 = CoverageReport.ClassifyHour(day.AddHours(4), markers, upIntervals, False, Nothing, 300000L)
        Dim h05 = CoverageReport.ClassifyHour(day.AddHours(5), markers, upIntervals, False, cleanStats, 300000L)
        Dim h07 = CoverageReport.ClassifyHour(day.AddHours(7), markers, upIntervals, False, cleanStats, 300000L)

        Dim ok As Boolean = h00.Classification = HourClass.ExpectedMissing AndAlso
                           h03.Classification = HourClass.ExpectedMissing AndAlso
                           h04.Classification = HourClass.Defect AndAlso
                           h05.Classification = HourClass.Captured AndAlso
                           h07.Classification = HourClass.Captured

        Check("A49b S1 join — before the app ever started ⇒ expected-missing (never defect); " &
              "up but the store is silent ⇒ defect; up with a clean store ⇒ captured",
              ok, String.Format("h00={0} h03={1} h04={2} h05={3} h07={4}",
                                h00.Classification, h03.Classification, h04.Classification,
                                h05.Classification, h07.Classification))
    End Sub

    ' -- A49c: DEGRADED/REST evidence counts as "up", never conflated with down or defect --
    Private Sub A49c_DegradedRestCountsAsUpNotConflated()
        Dim day = A49Monday()
        ' ONLY DEGRADED/REST lines for this instance — no OK, no DOWN — bracketing hour 05.
        Dim lines As New List(Of String) From {
            A49WsLine(day.AddHours(2), "DEGRADED", "iid-1"),
            A49WsLine(day.AddHours(5), "REST", "iid-1"),
            A49WsLine(day.AddHours(8), "DEGRADED", "iid-1")
        }
        Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(day.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"}
        }
        Dim upIntervals = CoverageReport.BuildUpIntervals(CoverageReport.ParseWsHealthEvidence(lines))
        Dim cleanStats As New HourStoreStats With {.RowCount = 5, .LongestGapMs = 1000}
        Dim emptyStats As New HourStoreStats With {.RowCount = 0, .LongestGapMs = 0}

        ' DEGRADED/REST evidence ⇒ "up": a clean store there reads captured (not accidentally
        ' expected-missing/unknown), and a SILENT store there reads defect (not silently let
        ' off as expected-missing) — DEGRADED is trusted evidence, not a "down" token.
        Dim hClean = CoverageReport.ClassifyHour(day.AddHours(5), markers, upIntervals, False, cleanStats, 300000L)
        Dim hSilent = CoverageReport.ClassifyHour(day.AddHours(5), markers, upIntervals, False, emptyStats, 300000L)

        Dim ok As Boolean = hClean.Classification = HourClass.Captured AndAlso
                           hSilent.Classification = HourClass.Defect

        Check("A49c DEGRADED/REST lines count as 'up' evidence exactly like OK — clean store ⇒ captured, silent store ⇒ defect, never expected-missing",
              ok, String.Format("clean={0} silent={1}", hClean.Classification, hSilent.Classification))
    End Sub

    ' -- A49d: capture-era self-bounding ---------------------------------------------------
    Private Sub A49d_CaptureEraSelfBounding()
        Dim dir As String = A49TempStore("d")
        Try
            Dim monthStart As New DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            ' First trade lands on day 5 — days 1-4 are outside capture.
            Dim firstTradeUtc As New DateTime(2026, 7, 5, 9, 0, 0, DateTimeKind.Utc)
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(firstTradeUtc), 64000),
                A49Trade(A49Ms(firstTradeUtc.AddHours(1)), 64010)
            })

            Dim fromUtc = monthStart
            Dim toUtc = New DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc)
            Dim begins = CoverageReport.ResolveCaptureBeginsUtc(dir, fromUtc, toUtc)

            Dim opts As New CoverageOptions With {.FromUtc = fromUtc, .ToUtc = toUtc, .GapMs = 300000L}
            Dim result = CoverageReport.BuildResult(opts, dir, "", "", "")

            Dim earliestWalked = If(result.Hours.Count > 0, result.Hours.Min(Function(h) h.HourUtc), DateTime.MaxValue)

            Dim ok As Boolean = begins.HasValue AndAlso begins.Value = firstTradeUtc AndAlso
                               result.PreCaptureDaysExcluded = 4 AndAlso
                               earliestWalked >= New DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc)

            Check("A49d capture-era self-bounding — reports from the store's first trade, not --from; pre-capture days counted, not walked as gaps",
                  ok, String.Format("begins={0} preDays={1} earliestWalked={2}",
                                    If(begins.HasValue, begins.Value.ToString("u"), "<none>"),
                                    result.PreCaptureDaysExcluded, earliestWalked.ToString("u")))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49e: S3 threshold — exact max, breach only strictly above --------------------------
    Private Sub A49e_S3ThresholdReportsExactMaxAndBreachesOnlyAbove()
        Dim dir As String = A49TempStore("e")
        Try
            Dim hourStart As New DateTime(2026, 7, 20, 5, 0, 0, DateTimeKind.Utc)   ' Monday
            Dim gapMs As Long = 300000L
            ' Exactly AT the threshold ⇒ not a breach.
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(1)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(1)) + gapMs, 64010)
            })
            Dim statsAtThreshold = CoverageReport.AccumulateHourStats(dir, hourStart, hourStart.AddHours(1))
            Dim s1 As HourStoreStats = Nothing
            statsAtThreshold.TryGetValue(A49Ms(hourStart), s1)

            A49Cleanup(dir)
            Directory.CreateDirectory(dir)
            ' One ms OVER the threshold ⇒ a breach, and the max is reported exactly.
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(1)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(1)) + gapMs + 1, 64010)
            })
            Dim statsOverThreshold = CoverageReport.AccumulateHourStats(dir, hourStart, hourStart.AddHours(1))
            Dim s2 As HourStoreStats = Nothing
            statsOverThreshold.TryGetValue(A49Ms(hourStart), s2)

            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(hourStart.AddHours(-1)),
                                     .LastUtcMs = A49Ms(hourStart.AddHours(2)), .IsTrailing = True}
            }
            Dim clsAt = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, s1, gapMs)
            Dim clsOver = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, s2, gapMs)

            Dim ok As Boolean = s1 IsNot Nothing AndAlso s1.LongestGapMs = gapMs AndAlso
                               s2 IsNot Nothing AndAlso s2.LongestGapMs = gapMs + 1 AndAlso
                               clsAt.Classification = HourClass.Captured AndAlso
                               clsOver.Classification = HourClass.Defect

            Check("A49e S3 threshold — observed max reported exactly; breach fires strictly above the threshold, not at it",
                  ok, String.Format("gapAt={0} clsAt={1} gapOver={2} clsOver={3}",
                                    If(s1 Is Nothing, -1, s1.LongestGapMs), clsAt.Classification,
                                    If(s2 Is Nothing, -1, s2.LongestGapMs), clsOver.Classification))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49f: S4 candle completeness — a month short by k bars reports exactly k missing ---
    Private Sub A49f_S4CandleCompletenessShortByK()
        Dim dir As String = A49TempStore("f")
        Try
            Dim fromUtc As New DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
            Dim toUtc As New DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc)   ' one hour ⇒ 60 expected 1m bars
            Dim path As String = System.IO.Path.Combine(dir, "candles_1m_2026-07.csv")
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(StoreFiles.CandleHeader)
                ' Write only 55 of the 60 expected minutes — short by k=5.
                For i As Integer = 0 To 54
                    Dim ts As Long = A49Ms(fromUtc.AddMinutes(i))
                    sw.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F6},{6:F2}",
                                               ts, 64000.0, 64010.0, 63990.0, 64005.0, 1.0, 64000.0))
                Next
            End Using

            Dim result = CoverageReport.ComputeCandleCompleteness(dir, fromUtc, toUtc)
            Dim have As Integer = result(1).Have
            Dim expected As Integer = result(1).Expected
            Dim missing As Integer = expected - have

            Check("A49f S4 candle completeness — a 1m month file short by k=5 bars reports exactly 5 missing against the deterministic grid",
                  expected = 60 AndAlso have = 55 AndAlso missing = 5,
                  String.Format("expected={0} have={1} missing={2}", expected, have, missing))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49g: absent ws_health.log ⇒ S1 skipped (when analysis_log.csv ALSO carries nothing
    ' in range), stated reason, S2-S4 still run -------------------------------------------
    Private Sub A49g_AbsentWsHealthSkipsS1S2ToS4StillRun()
        Dim dir As String = A49TempStore("g")
        Try
            Dim day = A49Monday()
            ' A clean hour of trades — proves S2/S3/S4 still run with S1 unavailable.
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(day.AddHours(5).AddMinutes(1)), 64000),
                A49Trade(A49Ms(day.AddHours(5).AddMinutes(2)), 64010)
            })
            Dim markerPath As String = System.IO.Path.Combine(dir, "capture_marker.log")
            File.WriteAllText(markerPath, A49MarkerLine(day.AddHours(-1), True, dir, "iid-1") & vbLf)

            Dim missingWsHealthPath As String = System.IO.Path.Combine(dir, "ws_health.log")     ' never created
            Dim missingAnalysisLogPath As String = System.IO.Path.Combine(dir, "analysis_log.csv") ' never created

            Dim opts As New CoverageOptions With {.FromUtc = day, .ToUtc = day.AddHours(6), .GapMs = 300000L}
            Dim result = CoverageReport.BuildResult(opts, dir, missingAnalysisLogPath, missingWsHealthPath, markerPath)

            Dim h05 = result.Hours.FirstOrDefault(Function(h) h.HourUtc = day.AddHours(5))

            Check("A49g absent ws_health.log + no analysis_log rows ⇒ S1 skipped with a stated reason; S2-S4 still run (a clean hour still reads captured)",
                  result.S1Skipped AndAlso Not String.IsNullOrEmpty(result.S1SkipReason) AndAlso
                  h05 IsNot Nothing AndAlso h05.Classification = HourClass.Captured,
                  String.Format("s1Skipped={0} reason='{1}' h05={2}",
                                result.S1Skipped, result.S1SkipReason, If(h05 Is Nothing, "<none>", h05.Classification.ToString())))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49h: --strict exit decision — defect ⇒ 1, expected-missing-only ⇒ 0 ----------------
    ' BacktestProgram.vb carries its own Main and stays OUT of this harness (the same
    ' boundary as every other verb) — this pins the DECISION the CLI's `Case "coverage"`
    ' block makes (opts.Strict AndAlso CountByClass(Defect) > 0), not the literal process
    ' exit code.
    Private Sub A49h_StrictExitDecisionDefectVsExpectedMissing()
        Dim day = A49Monday()
        Dim markersOn As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(day.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"}
        }
        Dim upIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(day.AddHours(4)),
                                 .LastUtcMs = A49Ms(day.AddHours(6)), .IsTrailing = True}
        }
        ' Hour 4 is up but silent ⇒ defect.
        Dim hDefect = CoverageReport.ClassifyHour(day.AddHours(4), markersOn, upIntervals, False, Nothing, 300000L)
        ' Hour 0 is before the app ever started ⇒ expected-missing only.
        Dim hExpected = CoverageReport.ClassifyHour(day.AddHours(0), markersOn, upIntervals, False, Nothing, 300000L)

        Dim exitWithDefect As Integer = If(True AndAlso hDefect.Classification = HourClass.Defect, 1, 0)
        Dim exitExpectedOnlyStrict As Integer = If(True AndAlso hExpected.Classification = HourClass.Defect, 1, 0)
        Dim exitExpectedOnlyNoStrict As Integer = If(False AndAlso hExpected.Classification = HourClass.Defect, 1, 0)

        Check("A49h --strict decision — a defect hour ⇒ exit 1; an expected-missing-only report ⇒ exit 0 under --strict; default (no --strict) always 0",
              exitWithDefect = 1 AndAlso exitExpectedOnlyStrict = 0 AndAlso exitExpectedOnlyNoStrict = 0,
              String.Format("defect={0} expectedStrict={1} expectedNoStrict={2}",
                            exitWithDefect, exitExpectedOnlyStrict, exitExpectedOnlyNoStrict))
    End Sub

    ' -- A49i: S1 primary/supplement precedence + ambiguous residual defaults to defect -----
    Private Sub A49i_PrimarySupplementPrecedenceAndAmbiguousDefaultsToDefect()
        Dim day = A49Monday()

        ' Part 1 — analysis_log.csv rows alone (ws_health.log entirely silent across the
        ' hour) still resolve "up".
        Dim csvLines As New List(Of String) From {
            "Timestamp,InstanceId",
            A49CsvRow(day.AddHours(5).AddMinutes(1), "iid-1"),
            A49CsvRow(day.AddHours(5).AddMinutes(2), "iid-1")
        }
        Dim evidence = CoverageReport.ParseAnalysisLogEvidence(csvLines)   ' zero ws_health lines merged
        Dim upIntervals = CoverageReport.BuildUpIntervals(evidence)
        Dim up = CoverageReport.ClassifyUptime(A49Ms(day.AddHours(5)), upIntervals)

        ' Part 2 — an hour with NEITHER analysis_log NOR ws_health evidence, sitting in a
        ' cross-GUID gap between two known instances, resolves DEFECT and not
        ' expected-missing (the inversion trap this arm exists to catch).
        Dim crossGuidIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(day.AddHours(2)), .LastUtcMs = A49Ms(day.AddHours(3))},
            New UpInterval With {.InstanceId = "iid-2", .FirstUtcMs = A49Ms(day.AddHours(9)), .LastUtcMs = A49Ms(day.AddHours(10)), .IsTrailing = True}
        }
        Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(day.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"}
        }
        ' Hour 5 sits strictly between the two intervals — no store data (silent).
        Dim gapHour = CoverageReport.ClassifyHour(day.AddHours(5), markers, crossGuidIntervals, False, Nothing, 300000L)

        Dim ok As Boolean = up.Kind = "up" AndAlso up.InstanceId = "iid-1" AndAlso
                           gapHour.Classification = HourClass.Defect

        Check("A49i S1 precedence — analysis_log.csv rows resolve UP even with ws_health.log silent; " &
              "a cross-GUID gap with no evidence at all resolves DEFECT, never expected-missing",
              ok, String.Format("upKind={0} upIid={1} gapHour={2}", up.Kind, up.InstanceId, gapHour.Classification))
    End Sub

    ' -- A49j: S0 venue diff — exact enumeration, pure, no live HTTP -------------------------
    Private Sub A49j_S0VenueDiffEnumeratesExactly()
        Dim baseTs As Long = A49Ms(A49Monday().AddHours(5))
        Dim storeTrades As New List(Of TradeRecord) From {
            A49Trade(baseTs, 64000),
            A49Trade(baseTs + 1000, 64010)
        }
        ' Venue has one trade the store also has, plus TWO the store is missing.
        Dim venueTrades As New List(Of TradeRecord) From {
            A49Trade(baseTs, 64000),
            A49Trade(baseTs + 2000, 64020),
            A49Trade(baseTs + 3000, 64030)
        }
        ' [trade identity] ComputeVenueDiff now returns a VenueDiffResult so the two match
        ' populations can be reported apart (D4). These A49 trades carry NO identity, so this
        ' fixture exercises the FALLBACK arm — which is exactly what the S0 diff falls back to
        ' on every pre-identity store row, and therefore still the case worth pinning here.
        Dim diff = CoverageReport.ComputeVenueDiff(storeTrades, venueTrades)
        Dim missing = diff.MissingTrades
        Dim missingOk As Boolean = missing.Count = 2 AndAlso
                                  missing.Any(Function(t) t.Timestamp = baseTs + 2000) AndAlso
                                  missing.Any(Function(t) t.Timestamp = baseTs + 3000) AndAlso
                                  diff.FallbackMatched = 1 AndAlso diff.IdentityMatched = 0

        ' An identical set reports zero missing.
        Dim identicalMissing = CoverageReport.ComputeVenueDiff(storeTrades, storeTrades).MissingTrades
        Dim identicalOk As Boolean = identicalMissing.Count = 0

        ' S0's absence never turns a defect into "clean" — the per-hour six-class result is
        ' computed independently of whether S0 ran at all (CoverageResult.VenueRan defaults
        ' False and nothing in ClassifyHour reads it), which is the "not covered by S0 rather
        ' than clean" guarantee: hours outside S0's window keep whatever S1-S3 already said.
        Dim freshResult As New CoverageResult()
        Dim s0NeverOverridesClassification As Boolean = Not freshResult.VenueRan

        Check("A49j S0 venue diff — missing trades enumerated exactly (2 of 3), identical sets report zero, S0 never overrides the six-class result",
              missingOk AndAlso identicalOk AndAlso s0NeverOverridesClassification,
              String.Format("missingCount={0} identicalCount={1} venueRanDefault={2}",
                            missing.Count, identicalMissing.Count, freshResult.VenueRan))
    End Sub

    ' -- A49k: not-capturing — the inversion trap, in A49i's shape ---------------------------
    Private Sub A49k_NotCapturingInversionTrap()
        Dim day = A49Monday()
        ' The SAME silence (no up-interval evidence, no store rows) under two different
        ' marker scopes — this is the trap: get the marker join backwards and BOTH read the
        ' same, either always-defect (false-alarm storm) or always-clean (silently absolving
        ' a real capturing box).
        Dim markersOff As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(day.AddHours(-1)), .Enabled = False, .InstanceId = "iid-1"}
        }
        Dim markersOn As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(day.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"}
        }
        Dim upIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(day.AddHours(0)),
                                 .LastUtcMs = A49Ms(day.AddHours(10)), .IsTrailing = True}
        }
        ' Store is silent throughout (zero rows every hour) despite the app being "up".
        Dim hOff = CoverageReport.ClassifyHour(day.AddHours(5), markersOff, upIntervals, False, Nothing, 300000L)
        Dim hOn = CoverageReport.ClassifyHour(day.AddHours(5), markersOn, upIntervals, False, Nothing, 300000L)

        Check("A49k not-capturing inversion trap — marker OFF over a silence ⇒ not-capturing (zero defects); " &
              "the IDENTICAL silence with marker ON ⇒ defect",
              hOff.Classification = HourClass.NotCapturing AndAlso hOn.Classification = HourClass.Defect,
              String.Format("off={0} on={1}", hOff.Classification, hOn.Classification))
    End Sub

    ' -- A49l: unknown-scope — no marker record at all ---------------------------------------
    Private Sub A49l_UnknownScopeWithNoMarkerAtAll()
        Dim day = A49Monday()
        Dim noMarkers As New List(Of CaptureMarkerLog.MarkerRecord)   ' empty — no marker file / no records
        Dim upIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(day.AddHours(0)),
                                 .LastUtcMs = A49Ms(day.AddHours(10)), .IsTrailing = True}
        }
        Dim cleanStats As New HourStoreStats With {.RowCount = 10, .LongestGapMs = 1000}

        ' Distinct from BOTH not-capturing (no marker ≠ marker-says-off) and expected-missing
        ' (a clean store during "up" evidence would otherwise read captured) — scope
        ' resolution runs BEFORE the store is even consulted, so it wins unconditionally.
        Dim hSilent = CoverageReport.ClassifyHour(day.AddHours(5), noMarkers, upIntervals, False, Nothing, 300000L)
        Dim hClean = CoverageReport.ClassifyHour(day.AddHours(5), noMarkers, upIntervals, False, cleanStats, 300000L)

        Check("A49l unknown-scope — no marker record at all classifies unknown-scope regardless of store state, distinct from not-capturing and expected-missing",
              hSilent.Classification = HourClass.UnknownScope AndAlso hClean.Classification = HourClass.UnknownScope,
              String.Format("silent={0} clean={1}", hSilent.Classification, hClean.Classification))
    End Sub

    ' -- A49m: weekday scope — Part A never flags a weekend defect, Part B stays unconditional
    ' (C1 Session 2 / Part B pairing) ------------------------------------------------------
    Private Sub A49m_WeekdayScopeVsPartBUnconditionalLiveness()
        Dim saturday As New DateTime(2026, 7, 25, 5, 0, 0, DateTimeKind.Utc)   ' A49Monday() minus 2 days
        Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(saturday.AddDays(-7)), .Enabled = True, .InstanceId = "iid-1"}
        }
        Dim upIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(saturday.AddDays(-7)),
                                 .LastUtcMs = A49Ms(saturday.AddDays(7)), .IsTrailing = True}
        }
        ' Part A — the Saturday hour is silent (zero store rows, the same silence that would
        ' flag DEFECT on a weekday) but must classify out-of-scope-weekend, never defect.
        Dim weekendHour = CoverageReport.ClassifyHour(saturday, markers, upIntervals, False, Nothing, 300000L)

        ' Part B — the SAME silence, expressed as seconds-since-flush, evaluated by the
        ' liveness tier classifier. It takes no date/day-of-week input at all, so there is
        ' nothing in it that COULD suppress a weekend reading — it reports RED exactly as it
        ' would on a weekday. That absence-of-a-check IS the "stays unconditional on
        ' weekends" guarantee (weekday-scope-ruling-2026-08-03.md §3 — Part B is the one
        ' place the ruling deliberately does not apply).
        Dim flushSeconds As Integer = 30
        Dim longSilenceSec As Double = 15.0 * flushSeconds   ' well past the 10× red threshold
        Dim tier As String = TradeStoreWriter.ClassifyTapeStoreTier(longSilenceSec, 0.0, flushSeconds)

        Check("A49m weekday scope — a Saturday hour classifies out-of-scope-weekend and never defect, while Part B's liveness tier still reports RED on the identical silence",
              weekendHour.Classification = HourClass.OutOfScopeWeekend AndAlso tier = "RED",
              String.Format("weekendHour={0} (dayOfWeek={1}) tapeTier={2}",
                            weekendHour.Classification, saturday.DayOfWeek, tier))
    End Sub

    ' -- A49n: a dead-but-never-throwing capture path must not hide behind UNKNOWN forever
    ' (C1 Session 2 review finding, 2026-08-05) -------------------------------------------
    ' A48e already pins that an unwritable store (blocked dir, locked file, full disk) NEVER
    ' THROWS — AppendRows logs and returns 0 forever, so `written > 0` is never true and
    ' secondsSinceFlush stays Nothing permanently. Before this fix, ClassifyTapeStoreTier read
    ' that as "UNKNOWN" (cold start, neutral colour) indefinitely — exactly the failure this
    ' element exists to catch, rendered as benign, on the only box that captures.
    Private Sub A49n_DeadCapturePathEscalatesPastGraceWindow()
        Dim flushSeconds As Integer = 30

        ' Genuine cold start — no flush yet, but well inside the grace window.
        Dim coldStart As String = TradeStoreWriter.ClassifyTapeStoreTier(Nothing, 5.0, flushSeconds)
        ' Past the amber horizon, still no flush ever — a dead path, not a slow one.
        Dim deadAmber As String = TradeStoreWriter.ClassifyTapeStoreTier(Nothing, 3.0 * flushSeconds, flushSeconds)
        ' Well past the red horizon, still no flush ever.
        Dim deadRed As String = TradeStoreWriter.ClassifyTapeStoreTier(Nothing, 20.0 * flushSeconds, flushSeconds)
        ' A stale-but-once-working flush uses the SAME thresholds, unaffected by the new arm.
        Dim staleFlush As String = TradeStoreWriter.ClassifyTapeStoreTier(11.0 * flushSeconds, 999.0, flushSeconds)
        ' A healthy, recent flush stays NORMAL regardless of how long the process has run.
        Dim healthy As String = TradeStoreWriter.ClassifyTapeStoreTier(2.0, 999.0, flushSeconds)

        Check("A49n dead capture path escalates UNKNOWN → AMBER → RED on the SAME 3×/10× thresholds when no flush ever lands; a genuine cold start stays UNKNOWN inside the grace window; a stale/healthy flush is unaffected",
              coldStart = "UNKNOWN" AndAlso deadAmber = "AMBER" AndAlso deadRed = "RED" AndAlso
              staleFlush = "RED" AndAlso healthy = "NORMAL",
              String.Format("coldStart={0} deadAmber={1} deadRed={2} staleFlush={3} healthy={4}",
                            coldStart, deadAmber, deadRed, staleFlush, healthy))
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' SH-1 — split the coverage hour at a capture-state marker
    ' (docs/coverage-split-hour-implementer-brief.md). Pre-fix, ResolveScope's whole-hour
    ' <= pick governs the ENTIRE hour a marker lands inside, so a flip mid-hour reads the
    ' hour by whichever scope happened to apply at :00 — a box that starts capturing at
    ' :30 reads not-capturing for the whole hour and a defect in its ON half is never
    ' examined. A49o/A49p are the mutation proof (§7 handle 1): both MUST fail if this
    ' fix is reverted.
    ' ═══════════════════════════════════════════════════════════════════════════════════

    ' -- A49o: flip ON at :30, clean store in the ON half — not laundered into not-capturing
    ' by the OFF marker that governed the hour's start. MUTATION PROOF: pre-fix this reads
    ' NotCapturing (ResolveScope picks the OFF marker for the whole hour and the store is
    ' never consulted) -----------------------------------------------------------------------
    Private Sub A49o_SplitHourFlipOnMidHourCleanOnHalfNotLaundered()
        Dim dir As String = A49TempStore("o")
        Try
            Dim day = A49Monday()
            Dim hourStart = day.AddHours(5)
            Dim flipMs As Long = A49Ms(hourStart.AddMinutes(30))
            Dim hourStartMs As Long = A49Ms(hourStart)

            ' Pre-flip: OFF (capture had not started on this box yet). Flip ON at :30.
            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = False, .InstanceId = "iid-1"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = flipMs, .Enabled = True, .InstanceId = "iid-2"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-2", .FirstUtcMs = flipMs,
                                     .LastUtcMs = A49Ms(hourStart.AddHours(2)), .IsTrailing = True}
            }
            ' Clean, tight trades in the ON half only (05:31-05:33, well under the 300,000ms
            ' threshold).
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(31)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(32)), 64010),
                A49Trade(A49Ms(hourStart.AddMinutes(33)), 64020)
            })

            Dim spanBounds As New Dictionary(Of Long, List(Of Long)) From {
                {hourStartMs, New List(Of Long) From {hourStartMs, flipMs}}
            }
            Dim spanStats = CoverageReport.AccumulateSplitSpanStats(dir, hourStart, hourStart.AddHours(1), spanBounds)

            Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L, spanStats)

            Check("A49o split hour — flip ON at :30 with a clean ON-half store reads CAPTURED, not laundered into not-capturing by the OFF marker that governed the hour's start (mutation proof — pre-fix reads NotCapturing)",
                  hr.Classification = HourClass.Captured,
                  String.Format("classification={0} reason='{1}'", hr.Classification, hr.Reason))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49p: flip ON at :30, SILENT store in the ON half — reads DEFECT. MUTATION PROOF:
    ' pre-fix this also reads NotCapturing, which is the opposite of the stated preference
    ' (a false defect is the cheaper error) -------------------------------------------------
    Private Sub A49p_SplitHourFlipOnMidHourSilentOnHalfIsDefect()
        Dim dir As String = A49TempStore("p")
        Try
            Dim day = A49Monday()
            Dim hourStart = day.AddHours(5)
            Dim flipMs As Long = A49Ms(hourStart.AddMinutes(30))
            Dim hourStartMs As Long = A49Ms(hourStart)

            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = False, .InstanceId = "iid-1"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = flipMs, .Enabled = True, .InstanceId = "iid-2"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-2", .FirstUtcMs = flipMs,
                                     .LastUtcMs = A49Ms(hourStart.AddHours(2)), .IsTrailing = True}
            }
            ' No trades at all — the store is silent throughout, including the ON half.
            Dim spanBounds As New Dictionary(Of Long, List(Of Long)) From {
                {hourStartMs, New List(Of Long) From {hourStartMs, flipMs}}
            }
            Dim spanStats = CoverageReport.AccumulateSplitSpanStats(dir, hourStart, hourStart.AddHours(1), spanBounds)

            Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L, spanStats)

            Check("A49p split hour — flip ON at :30 with a SILENT ON-half store reads DEFECT (up evidence present, zero rows) — the second mutation-proof case",
                  hr.Classification = HourClass.Defect,
                  String.Format("classification={0} reason='{1}'", hr.Classification, hr.Reason))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49q: flip OFF at :30, defect entirely inside the ON (first) half — not laundered by
    ' the later off marker governing the rest of the hour --------------------------------
    Private Sub A49q_SplitHourFlipOffMidHourDefectInOnHalfNotLaundered()
        Dim dir As String = A49TempStore("q")
        Try
            Dim day = A49Monday()
            Dim hourStart = day.AddHours(5)
            Dim flipMs As Long = A49Ms(hourStart.AddMinutes(30))
            Dim hourStartMs As Long = A49Ms(hourStart)

            ' Pre-flip: ON. Flip OFF at :30.
            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = flipMs, .Enabled = False, .InstanceId = "iid-1"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(hourStart.AddHours(-1)),
                                     .LastUtcMs = flipMs, .IsTrailing = True}
            }
            ' One trade at :05, then silence past the 300,000ms threshold before the flip at
            ' :30 — a gap-breach entirely inside the ON half.
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(5)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(25)), 64010)   ' 20-minute gap > 5-minute threshold
            })

            Dim spanBounds As New Dictionary(Of Long, List(Of Long)) From {
                {hourStartMs, New List(Of Long) From {hourStartMs, flipMs}}
            }
            Dim spanStats = CoverageReport.AccumulateSplitSpanStats(dir, hourStart, hourStart.AddHours(1), spanBounds)

            Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L, spanStats)

            Check("A49q split hour — flip OFF at :30 with a gap-breach entirely inside the ON (first) half still reads DEFECT, not laundered by the later off marker governing the rest of the hour",
                  hr.Classification = HourClass.Defect,
                  String.Format("classification={0} reason='{1}'", hr.Classification, hr.Reason))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49r: a marker landing EXACTLY at hourStartMs is ResolveScope's business (<=), not a
    ' split — no split-detail Reason, single-scope path unchanged ---------------------------
    Private Sub A49r_SplitHourMarkerExactlyAtHourStartUnchanged()
        Dim day = A49Monday()
        Dim hourStart = day.AddHours(5)
        Dim atStart As Long = A49Ms(hourStart)

        Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = False, .InstanceId = "iid-1"},
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = atStart, .Enabled = True, .InstanceId = "iid-2"}
        }
        Dim upIntervals As New List(Of UpInterval) From {
            New UpInterval With {.InstanceId = "iid-2", .FirstUtcMs = atStart,
                                 .LastUtcMs = A49Ms(hourStart.AddHours(2)), .IsTrailing = True}
        }
        Dim cleanStats As New HourStoreStats With {.RowCount = 3, .LongestGapMs = 60000}

        Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, cleanStats, 300000L)

        Check("A49r a marker landing EXACTLY at hourStartMs is ResolveScope's business (<=), not a split — no split-detail Reason, classification via the ordinary single-scope path",
              hr.Classification = HourClass.Captured AndAlso String.IsNullOrEmpty(hr.Reason),
              String.Format("classification={0} reason='{1}'", hr.Classification, hr.Reason))
    End Sub

    ' -- A49s: a marker at :59 still splits (the last span is ~60s wide); a 50s gap inside it
    ' (83% of the span's own width) must NOT breach the absolute 300,000ms threshold — gapMs
    ' is not scaled to the sub-span length ---------------------------------------------------
    Private Sub A49s_SplitHourMarkerAtFiftyNineUsesAbsoluteThresholdNotScaled()
        Dim dir As String = A49TempStore("s")
        Try
            Dim day = A49Monday()
            Dim hourStart = day.AddHours(5)
            Dim flipMs As Long = A49Ms(hourStart.AddMinutes(59))
            Dim hourStartMs As Long = A49Ms(hourStart)

            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = False, .InstanceId = "iid-1"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = flipMs, .Enabled = True, .InstanceId = "iid-2"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-2", .FirstUtcMs = flipMs,
                                     .LastUtcMs = A49Ms(hourStart.AddHours(2)), .IsTrailing = True}
            }
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(59).AddSeconds(5)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(59).AddSeconds(55)), 64010)
            })

            Dim spanBounds As New Dictionary(Of Long, List(Of Long)) From {
                {hourStartMs, New List(Of Long) From {hourStartMs, flipMs}}
            }
            Dim spanStats = CoverageReport.AccumulateSplitSpanStats(dir, hourStart, hourStart.AddHours(1), spanBounds)
            Dim s59Stats As HourStoreStats = Nothing
            spanStats.TryGetValue(flipMs, s59Stats)

            Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L, spanStats)

            Check("A49s split hour — a marker at :59 still splits; a 50s gap inside the ~60s-wide last span (83% of the span's own width) does NOT breach the absolute 300,000ms threshold — not scaled to the sub-span length",
                  hr.Classification = HourClass.Captured AndAlso
                  s59Stats IsNot Nothing AndAlso s59Stats.LongestGapMs = 50000L,
                  String.Format("classification={0} reason='{1}' span59Gap={2}",
                                hr.Classification, hr.Reason, If(s59Stats Is Nothing, -1, s59Stats.LongestGapMs)))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49t: two in-hour markers split an hour into THREE spans, all independently
    ' classified — off / captured / off, worst-of combine reads CAPTURED -------------------
    Private Sub A49t_SplitHourTwoMarkersThreeSpansAllClassified()
        Dim dir As String = A49TempStore("t")
        Try
            Dim day = A49Monday()
            Dim hourStart = day.AddHours(5)
            Dim hourStartMs As Long = A49Ms(hourStart)
            Dim onMs As Long = A49Ms(hourStart.AddMinutes(20))
            Dim offMs As Long = A49Ms(hourStart.AddMinutes(40))

            ' OFF (:00-:20) → ON (:20-:40) → OFF (:40-:59).
            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = False, .InstanceId = "iid-1"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = onMs, .Enabled = True, .InstanceId = "iid-2"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = offMs, .Enabled = False, .InstanceId = "iid-2"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-2", .FirstUtcMs = onMs, .LastUtcMs = offMs, .IsTrailing = True}
            }
            ' Clean, tight trades inside the ON middle span only.
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(25)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(30)), 64010)
            })

            Dim spanBounds As New Dictionary(Of Long, List(Of Long)) From {
                {hourStartMs, New List(Of Long) From {hourStartMs, onMs, offMs}}
            }
            Dim spanStats = CoverageReport.AccumulateSplitSpanStats(dir, hourStart, hourStart.AddHours(1), spanBounds)

            Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L, spanStats)
            Dim spanCount As Integer = hr.Reason.Split("|"c).Length

            Check("A49t split hour — two in-hour markers split it into THREE spans, all independently classified (off/captured/off); worst-of combine reads CAPTURED, D-2",
                  hr.Classification = HourClass.Captured AndAlso spanCount = 3,
                  String.Format("classification={0} spanCount={1} reason='{2}'", hr.Classification, spanCount, hr.Reason))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49u: a gap straddling the marker (starts in span0, ends in span1) is attributed to
    ' span1 — the span containing the ENDING trade, the same convention AccumulateHourStats
    ' already uses at the hour level. Slip 2: prevTs must carry across the split, never reset
    ' to the span's own first row --------------------------------------------------------
    Private Sub A49u_SplitHourGapStraddlingMarkerAttributedToEndingSpan()
        Dim dir As String = A49TempStore("u")
        Try
            Dim day = A49Monday()
            Dim hourStart = day.AddHours(5)
            Dim hourStartMs As Long = A49Ms(hourStart)
            Dim restartMs As Long = A49Ms(hourStart.AddMinutes(30))

            ' A restart (same scope, ON, on both sides) — isolates the stats-attribution
            ' question from the scope-combination logic A49o/A49q already cover.
            Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = A49Ms(hourStart.AddHours(-1)), .Enabled = True, .InstanceId = "iid-1"},
                New CaptureMarkerLog.MarkerRecord With {.UtcMs = restartMs, .Enabled = True, .InstanceId = "iid-2"}
            }
            Dim upIntervals As New List(Of UpInterval) From {
                New UpInterval With {.InstanceId = "iid-1", .FirstUtcMs = A49Ms(hourStart.AddHours(-1)),
                                     .LastUtcMs = A49Ms(hourStart.AddHours(2)), .IsTrailing = True}
            }
            ' One trade at :25 (span0), then silence across the restart until :32 (span1) — a
            ' 7-minute (420,000ms) gap that STARTS before the marker and ENDS after it.
            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(hourStart.AddMinutes(25)), 64000),
                A49Trade(A49Ms(hourStart.AddMinutes(32)), 64010)
            })

            Dim spanBounds As New Dictionary(Of Long, List(Of Long)) From {
                {hourStartMs, New List(Of Long) From {hourStartMs, restartMs}}
            }
            Dim spanStats = CoverageReport.AccumulateSplitSpanStats(dir, hourStart, hourStart.AddHours(1), spanBounds)
            Dim span0 As HourStoreStats = Nothing
            Dim span1 As HourStoreStats = Nothing
            spanStats.TryGetValue(hourStartMs, span0)
            spanStats.TryGetValue(restartMs, span1)

            Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L, spanStats)

            Dim ok As Boolean = span0 IsNot Nothing AndAlso span0.RowCount = 1 AndAlso span0.LongestGapMs = 0 AndAlso
                               span1 IsNot Nothing AndAlso span1.RowCount = 1 AndAlso span1.LongestGapMs = 420000L AndAlso
                               hr.Classification = HourClass.Defect

            Check("A49u split hour — a gap straddling the marker (starts in span0, ends in span1) is attributed to span1, the span containing the ending trade; span0's own first row carries no phantom gap; the straddling gap alone flips the hour to DEFECT",
                  ok, String.Format("span0={{rows={0},gap={1}}} span1={{rows={2},gap={3}}} classification={4}",
                                    If(span0 Is Nothing, -1, span0.RowCount), If(span0 Is Nothing, -1, span0.LongestGapMs),
                                    If(span1 Is Nothing, -1, span1.RowCount), If(span1 Is Nothing, -1, span1.LongestGapMs),
                                    hr.Classification))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' -- A49w: D-3 [RULED 2026-08-13, docs/coverage-split-hour-implementer-brief.md §5a] — the
    ' residual combine (no Defect, no Captured present) must prefer UnknownScope over
    ' NotCapturing, not the reverse. A first-ever marker landing mid-hour splits an
    ' UnknownScope span (nothing applies before it) from a NotCapturing span (its own off
    ' record) — laundering that into NotCapturing is the SH-1 defect in miniature, and it
    ' silently reverses ClassifySpan's own unknown-before-off precedence. Not contrived: this
    ' is exactly the shape capture_marker.log's first-ever write produces on a box brought up
    ' with the capture overlay (ec487909…, 2026-08-07 16:02, AWS) --------------------------
    Private Sub A49w_SplitHourFirstEverMarkerMidHourResidualPrefersUnknownScope()
        Dim day = A49Monday()
        Dim hourStart = day.AddHours(5)
        Dim firstMarkerMs As Long = A49Ms(hourStart.AddMinutes(30))

        ' The ONLY marker that exists at all lands mid-hour: span0 [hourStart, marker) has no
        ' applicable marker (UnknownScope); span1 [marker, hourEnd] is governed by this
        ' first-ever marker, recorded OFF (NotCapturing).
        Dim markers As New List(Of CaptureMarkerLog.MarkerRecord) From {
            New CaptureMarkerLog.MarkerRecord With {.UtcMs = firstMarkerMs, .Enabled = False, .InstanceId = "iid-1"}
        }
        Dim upIntervals As New List(Of UpInterval)

        Dim hr = CoverageReport.ClassifyHour(hourStart, markers, upIntervals, False, Nothing, 300000L)

        Check("A49w split hour D-3 — a first-ever marker landing mid-hour splits UnknownScope (before it) from NotCapturing (its own off record); the residual combine reads UnknownScope, never laundered into NotCapturing",
              hr.Classification = HourClass.UnknownScope,
              String.Format("classification={0} reason='{1}'", hr.Classification, hr.Reason))
    End Sub

    ' -- A49v: BuildResult end-to-end — a day containing a split hour still returns exactly
    ' 24 HourResult rows (D-1, §7 handle 5) -------------------------------------------------
    Private Sub A49v_BuildResultOneRowPerHourAcrossASplitDay()
        Dim dir As String = A49TempStore("v")
        Try
            Dim day = A49Monday()
            Dim splitAt = day.AddHours(10).AddMinutes(15)

            TradeStoreWriter.AppendRows(dir, New List(Of TradeRecord) From {
                A49Trade(A49Ms(day.AddMinutes(5)), 64000)
            })

            Dim markerPath As String = System.IO.Path.Combine(dir, "capture_marker.log")
            File.WriteAllText(markerPath,
                A49MarkerLine(day.AddHours(-1), True, dir, "iid-1") & vbLf &
                A49MarkerLine(splitAt, False, dir, "iid-1") & vbLf)

            Dim missingAnalysisLogPath As String = System.IO.Path.Combine(dir, "analysis_log.csv")   ' never created
            Dim missingWsHealthPath As String = System.IO.Path.Combine(dir, "ws_health.log")         ' never created

            Dim opts As New CoverageOptions With {.FromUtc = day, .ToUtc = day.AddDays(1), .GapMs = 300000L}
            Dim result = CoverageReport.BuildResult(opts, dir, missingAnalysisLogPath, missingWsHealthPath, markerPath)

            Dim splitHour = result.Hours.FirstOrDefault(Function(h) h.HourUtc = day.AddHours(10))

            Check("A49v BuildResult end-to-end — a day containing a split hour still returns exactly 24 HourResult rows (D-1 preserved, §7 handle 5); the split hour carries split detail in Reason",
                  result.Hours.Count = 24 AndAlso splitHour IsNot Nothing AndAlso splitHour.Reason.StartsWith("split@"),
                  String.Format("hoursCount={0} splitHourReason='{1}'", result.Hours.Count, If(splitHour Is Nothing, "<none>", splitHour.Reason)))
        Finally
            A49Cleanup(dir)
        End Try
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A51 — candle/funding store write invariant (Core/StoreFiles.vb)
    '
    ' THE INVARIANT: stored rows ALWAYS survive a write; the result is the UNION of stored
    ' and fetched. A partial, truncated or failed fetch can never destroy anything.
    '
    ' This is the guard the June wipe did not have. `BackfillCandleMonthAsync` used
    ' `append:=False` over a whole month file from a segment fetch — and `BackfillAllAsync`
    ' starts 20 h before `fromUtc`, so `fetch --from 2026-07-01` handed June a 20-hour
    ' segment. 14,400 3m bars became 400. A51a is the direct regression trap for that.
    ' ═══════════════════════════════════════════════════════════════════════════════════

    Private Function A51TempDir(tag As String) As String
        Dim dir As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                         "ordercheck_a51_" & tag & "_" & Guid.NewGuid().ToString("N").Substring(0, 8))
        Directory.CreateDirectory(dir)
        Return dir
    End Function

    ' A month of bars at `intervalMin`, starting at `startMs`, count `n`. Close encodes the
    ' index so provenance is checkable after a merge.
    Private Function A51Bars(startMs As Long, intervalMin As Integer, n As Integer,
                             Optional closeBase As Double = 60000.0) As List(Of Candle)
        Dim outp As New List(Of Candle)()
        For i As Integer = 0 To n - 1
            outp.Add(New Candle With {
                .Timestamp = startMs + CLng(i) * intervalMin * 60000L,
                .Open = closeBase + i, .High = closeBase + i + 5, .Low = closeBase + i - 5,
                .Close = closeBase + i, .Volume = 1.5, .VolumeUSD = (closeBase + i) * 1.5})
        Next
        Return outp
    End Function

    ' -- A51a: a partial-segment fetch must NOT destroy the rest of the month --------------
    ' The June wipe, reproduced in miniature and asserted against. A full 3m "month" is
    ' stored; then a fetch returns only the LAST 20 hours of it (exactly the shape
    ' BackfillAllAsync's 20 h warmup produces). Pre-fix that replaced the file. Post-fix the
    ' union must be complete and the early bars must still carry their original values.
    Private Sub A51a_MergePreservesExistingRowsOnPartialFetch()
        Dim dir As String = A51TempDir("a")
        Try
            Dim monthStart As Long = New DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
            Const barsPerMonth As Integer = 14400          ' 30 days of 3m bars
            Const tailBars As Integer = 400                ' the 20-hour survivor window
            Dim csvPath As String = System.IO.Path.Combine(dir, "candles_3m_2026-06.csv")

            ' Store the whole month.
            StoreFiles.MergeAndWriteCandles(csvPath, Nothing, A51Bars(monthStart, 3, barsPerMonth))
            Dim storedFull As Integer = StoreFiles.LoadCandleFile(csvPath).Count

            ' A fetch that returns ONLY the trailing 20 hours, with different values so an
            ' overwrite would be detectable even at equal row counts.
            Dim tailStart As Long = monthStart + CLng(barsPerMonth - tailBars) * 3L * 60000L
            Dim partialFetch = A51Bars(tailStart, 3, tailBars, closeBase:=99000.0)

            Dim total As Integer = StoreFiles.MergeAndWriteCandles(csvPath, StoreFiles.LoadCandleFile(csvPath), partialFetch)
            Dim after = StoreFiles.LoadCandleFile(csvPath)

            Dim countOk As Boolean = (storedFull = barsPerMonth) AndAlso
                                     (total = barsPerMonth) AndAlso (after.Count = barsPerMonth)
            ' The first bar must still be the ORIGINAL value — this is the assertion that
            ' fails pre-fix, where the file would hold 400 rows starting at tailStart.
            Dim firstOk As Boolean = after.Count > 0 AndAlso
                                     after(0).Timestamp = monthStart AndAlso
                                     Math.Abs(after(0).Close - 60000.0) < 1e-9
            ' The overlapping tail took the fetched values (fetch wins on collision).
            Dim tailOk As Boolean = Math.Abs(after(after.Count - 1).Close - (99000.0 + tailBars - 1)) < 1e-9
            ' Chronological + no duplicates.
            Dim orderOk As Boolean = True
            For i As Integer = 1 To after.Count - 1
                If after(i).Timestamp <= after(i - 1).Timestamp Then orderOk = False
            Next

            Check("A51a partial-segment fetch preserves the month — union complete · early bars keep original values · tail refreshed · ordered (the June-wipe trap)",
                  countOk AndAlso firstOk AndAlso tailOk AndAlso orderOk,
                  String.Format("storedFull={0} total={1} after={2} first={3} tail={4} order={5}",
                                storedFull, total, after.Count, firstOk, tailOk, orderOk))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A51b: coverage counting is resolution-aware ---------------------------------------
    ' The retired MonthFileCovers required the last bar within a FIXED 2 minutes of the
    ' segment end. A month's last bar is 23:59 at 1m but 23:57 / 23:55 / 23:45 at 3m/5m/15m,
    ' so only 1m could ever pass — every other resolution refetched forever. The grid count
    ' must call a complete month complete at ALL four resolutions.
    Private Sub A51b_CoverageCountIsResolutionAware()
        Dim monthStart As Long = New DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim monthEndIncl As Long = New DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() - 1

        Dim allOk As Boolean = True
        Dim detail As String = ""
        For Each res As Integer In New Integer() {1, 3, 5, 15}
            Dim iv As Long = CLng(res) * 60000L
            Dim expected As Integer = StoreFiles.ExpectedGridPoints(monthStart, monthEndIncl, iv)
            Dim want As Integer = CInt(30L * 24L * 60L \ res)          ' June = 30 days
            Dim bars = A51Bars(monthStart, res, expected)
            Dim have As Integer = StoreFiles.CountCandlesInRange(bars, monthStart, monthEndIncl)
            ' A complete month must satisfy the check — this is the arm that failed before.
            If expected <> want OrElse have < expected Then allOk = False
            detail &= String.Format("{0}m(exp={1} want={2} have={3}) ", res, expected, want, have)
        Next

        ' And a genuinely short month must NOT satisfy it (the check still has teeth).
        Dim shortBars = A51Bars(monthStart, 3, 400)
        Dim shortExpected As Integer = StoreFiles.ExpectedGridPoints(monthStart, monthEndIncl, 3L * 60000L)
        Dim shortHave As Integer = StoreFiles.CountCandlesInRange(shortBars, monthStart, monthEndIncl)
        Dim teethOk As Boolean = shortHave < shortExpected

        Check("A51b coverage count is resolution-aware — complete month passes at 1/3/5/15m, a 400-bar month still fails",
              allOk AndAlso teethOk,
              detail & String.Format("| short have={0} exp={1} teeth={2}", shortHave, shortExpected, teethOk))
    End Sub

    ' -- A51c: an empty or failed fetch never destroys -------------------------------------
    ' The worst case a fetch failure may produce is "adds nothing". Three shapes: Nothing,
    ' an empty list, and a fetch that duplicates what is already there.
    Private Sub A51c_EmptyOrFailedFetchNeverDestroys()
        Dim dir As String = A51TempDir("c")
        Try
            Dim start As Long = New DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
            Dim csvPath As String = System.IO.Path.Combine(dir, "candles_5m_2026-06.csv")
            Dim seed = A51Bars(start, 5, 500)
            StoreFiles.MergeAndWriteCandles(csvPath, Nothing, seed)
            Dim baseline = StoreFiles.LoadCandleFile(csvPath)

            Dim nNothing As Integer = StoreFiles.MergeAndWriteCandles(csvPath, StoreFiles.LoadCandleFile(csvPath), Nothing)
            Dim afterNothing = StoreFiles.LoadCandleFile(csvPath)
            Dim nEmpty As Integer = StoreFiles.MergeAndWriteCandles(csvPath, StoreFiles.LoadCandleFile(csvPath), New List(Of Candle)())
            Dim afterEmpty = StoreFiles.LoadCandleFile(csvPath)
            Dim nDup As Integer = StoreFiles.MergeAndWriteCandles(csvPath, StoreFiles.LoadCandleFile(csvPath), seed)
            Dim afterDup = StoreFiles.LoadCandleFile(csvPath)

            Dim ok As Boolean = baseline.Count = 500 AndAlso
                                nNothing = 500 AndAlso afterNothing.Count = 500 AndAlso
                                nEmpty = 500 AndAlso afterEmpty.Count = 500 AndAlso
                                nDup = 500 AndAlso afterDup.Count = 500 AndAlso
                                Math.Abs(afterDup(0).Close - baseline(0).Close) < 1e-9

            Check("A51c empty / Nothing / duplicate fetch never destroys — row count and values stable across all three",
                  ok,
                  String.Format("base={0} nothing={1}/{2} empty={3}/{4} dup={5}/{6}",
                                baseline.Count, nNothing, afterNothing.Count,
                                nEmpty, afterEmpty.Count, nDup, afterDup.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A51d: funding merge clips the deliberate over-reach, keeps everything stored -------
    ' The funding fetch reaches one interval EARLIER than the segment because Deribit's
    ' start_timestamp is exclusive (verified live: from exactly T the first sample back is
    ' T+1h; from T−1ms it is T). That over-reach must not leak the previous month's sample
    ' into this month's file — but stored rows are never clipped.
    Private Sub A51d_FundingMergeClipsOverreachButKeepsStored()
        Dim dir As String = A51TempDir("d")
        Try
            Const hour As Long = 3600000L
            Dim monthStart As Long = New DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
            Dim endIncl As Long = monthStart + 5L * hour
            Dim csvPath As String = System.IO.Path.Combine(dir, "funding_2026-06.csv")

            ' Stored: hours 1..3 (the 00:00 sample is the one the exclusive-start bug lost).
            Dim stored As New List(Of BacktestFundingSample) From {
                New BacktestFundingSample With {.TsMs = monthStart + 1 * hour, .Rate = 0.0000011},
                New BacktestFundingSample With {.TsMs = monthStart + 2 * hour, .Rate = 0.0000022},
                New BacktestFundingSample With {.TsMs = monthStart + 3 * hour, .Rate = 0.0000033}}
            StoreFiles.MergeAndWriteFunding(csvPath, Nothing, stored, monthStart, endIncl)

            ' Fetched with the over-reach: includes the PREVIOUS month's 23:00 sample, the
            ' boundary 00:00 sample, and hours 4..5.
            Dim fetched As New List(Of BacktestFundingSample) From {
                New BacktestFundingSample With {.TsMs = monthStart - 1 * hour, .Rate = 0.0000999},
                New BacktestFundingSample With {.TsMs = monthStart, .Rate = 0.0000001},
                New BacktestFundingSample With {.TsMs = monthStart + 4 * hour, .Rate = 0.0000044},
                New BacktestFundingSample With {.TsMs = monthStart + 5 * hour, .Rate = 0.0000055}}

            Dim total As Integer = StoreFiles.MergeAndWriteFunding(
                csvPath, StoreFiles.LoadFundingFile(csvPath), fetched, monthStart, endIncl)
            Dim after = StoreFiles.LoadFundingFile(csvPath)

            Dim clipOk As Boolean = Not after.Any(Function(s) s.TsMs < monthStart)
            Dim boundaryOk As Boolean = after.Any(Function(s) s.TsMs = monthStart)      ' the recovered sample
            Dim keptOk As Boolean = after.Any(Function(s) s.TsMs = monthStart + 2 * hour)
            Dim countOk As Boolean = (total = 6) AndAlso (after.Count = 6)               ' hours 0..5
            Dim expOk As Boolean = StoreFiles.ExpectedGridPoints(monthStart, endIncl, hour) = 6

            Check("A51d funding merge — previous-month over-reach clipped · boundary 00:00 sample recovered · stored rows kept · grid expectation 6",
                  clipOk AndAlso boundaryOk AndAlso keptOk AndAlso countOk AndAlso expOk,
                  String.Format("clip={0} boundary={1} kept={2} total={3} after={4} exp={5}",
                                clipOk, boundaryOk, keptOk, total, after.Count, expOk))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A51e: written candles round-trip through the shipped parse ------------------------
    ' Same discipline as A48a on the trade side: the writer and the reader are one seam, so
    ' a round-trip proves the on-disk format rather than asserting it.
    Private Sub A51e_CandleRoundTripThroughShippedParse()
        Dim dir As String = A51TempDir("e")
        Try
            Dim start As Long = New DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
            Dim csvPath As String = System.IO.Path.Combine(dir, "candles_15m_2026-06.csv")
            Dim src = A51Bars(start, 15, 12)
            StoreFiles.MergeAndWriteCandles(csvPath, Nothing, src)

            Dim lines = File.ReadAllLines(csvPath)
            Dim headerOk As Boolean = lines.Length = 13 AndAlso lines(0) = "Timestamp,Open,High,Low,Close,Volume,Cost"
            Dim colsOk As Boolean = lines.Skip(1).All(Function(l) l.Split(","c).Length = 7)

            Dim back = StoreFiles.LoadCandleFile(csvPath)
            Dim valuesOk As Boolean = back.Count = src.Count
            If valuesOk Then
                For i As Integer = 0 To src.Count - 1
                    If back(i).Timestamp <> src(i).Timestamp OrElse
                       Math.Abs(back(i).Close - src(i).Close) > 0.005 OrElse
                       Math.Abs(back(i).Volume - src(i).Volume) > 5e-7 OrElse
                       Math.Abs(back(i).VolumeUSD - src(i).VolumeUSD) > 0.005 Then valuesOk = False
                Next
            End If

            Check("A51e candle round-trip — shipped header + 7 columns + OHLCV survive write→read",
                  headerOk AndAlso colsOk AndAlso valuesOk,
                  String.Format("header={0} cols={1} values={2} n={3}", headerOk, colsOk, valuesOk, back.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ''' <summary>A52 settings shape — the aggressor_velocity block with a substitutable ASIA body.</summary>
    Private Function A52Json(asiaBody As String) As String
        Return "{""version"":65,""indicators"":{""aggressor_velocity"":{" &
               """enabled"":true,""scoring_enabled"":true," &
               """default"":{""norm_window_sec"":120,""burst_ratio_threshold"":2.5}," &
               """sessions"":{""NY"":{""norm_window_sec"":60,""burst_ratio_threshold"":4.5}," &
               """LONDON"":{""burst_ratio_threshold"":5.5},""ASIA"":" & asiaBody & "}}}}"
    End Function

    ' -- A52a: the JSON contract for the ASIA arming key (v65 / D3) -----------------
    ' Three arms, because D3's correctness has three separable failure modes:
    '   (1) present ⇒ ARMED at 5.5 — the change itself;
    '   (2) absent  ⇒ INERT at the 2.5 default — the rollback, and the mechanism that
    '       A28c can no longer borrow a real session to demonstrate;
    '   (3) the shipped POCO agrees with the shipped JSON. This one is the drift guard:
    '       the harness builds every cfg from New EngineSettings(), so if settings.json
    '       and EngineSettings.vb ever disagree about ASIA, the app and the harness pin
    '       DIFFERENT behaviour and A28c still passes. v60 established that lockstep for
    '       LONDON; v65 owes it for ASIA.
    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A53 — trade identity in the store schema
    ' (docs/trade-store-trade-identity-proposal.md §6)
    '
    ' The defect being fixed is a case where WRONG LOOKS RIGHT: five-field rows silently
    ' merged distinct trades, and every surface downstream reported a full, clean store.
    ' Two of the fixtures below (A53c, A53e) exist because the OBVIOUS implementation of
    ' this fix reproduces the same defect at greater scale inside its own repair.
    '
    ' ⚠ A53c and A53e were written FROM THE SPEC TEXT (§3.4 and the §0 trap list), before
    ' reading TradeStoreWriter.DedupTrades, precisely because the implementer writes both the
    ' code and the test and a misunderstanding of the contract would otherwise propagate
    ' straight into its own check.
    ' ═══════════════════════════════════════════════════════════════════════════════════

    Private Function A53Ms(offsetMs As Long) As Long
        Return New DateTimeOffset(2026, 8, 8, 9, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() + offsetMs
    End Function

    Private Function A53Trade(tsMs As Long, px As Double, amt As Double, dir As String,
                              Optional liq As String = "none",
                              Optional id As String = Nothing,
                              Optional seq As Long = TradeStoreWriter.AbsentSeq) As TradeRecord
        Return New TradeRecord With {
            .Timestamp = tsMs, .Price = px, .Amount = amt, .Direction = dir, .Liquidation = liq,
            .TradeId = id, .TradeSeq = seq}
    End Function

    ' -- A53a: a seven-field row writes and parses back with both identity fields intact ----
    ' Through the real streaming path (Buffer → Flush) and the real reader, so this is the
    ' on-disk contract, not a formatter unit test. Values are the ones observed at the §1
    ' verification gate on 2026-08-08 — a real trade_id (a STRING) and a real trade_seq.
    Private Sub A53a_IdentityRoundTripsThroughStore()
        Dim dir As String = A48TempStore("53a")
        Try
            Dim w As New TradeStoreWriter(dir)
            Dim src As New List(Of TradeRecord) From {
                A53Trade(A53Ms(0), 64670.0, 3050.0, "sell", "none", "439922712", 295960045L),
                A53Trade(A53Ms(100), 64670.0, 10.0, "sell", "none", "439922713", 295960046L),
                A53Trade(A53Ms(200), 64665.0, 30000.0, "sell", "T", "439922717", 295960050L)}
            For Each t In src
                w.Buffer(t)
            Next
            Dim flushed As Integer = w.Flush()

            Dim path As String = TradeStoreWriter.TradeFileFor(dir, 2026, 8)
            Dim lines = File.ReadAllLines(path)
            Dim headerOk As Boolean = lines(0) = TradeStoreWriter.HeaderLine
            Dim rowOk As Boolean = lines(1) = A53Ms(0) & ",64670.00,3050.00,sell,none,439922712,295960045"

            Dim back = TradeStoreWriter.ReadTradeFile(path)
            Dim ok As Boolean = back.Count = 3
            If ok Then
                For i As Integer = 0 To 2
                    If back(i).TradeId <> src(i).TradeId OrElse
                       back(i).TradeSeq <> src(i).TradeSeq OrElse
                       Not back(i).HasIdentity OrElse Not back(i).HasSeq OrElse
                       back(i).Liquidation <> src(i).Liquidation Then ok = False
                Next
            End If

            Check("A53a identity round-trip — 7-field row writes and parses back with trade_id (string) + trade_seq intact",
                  flushed = 3 AndAlso headerOk AndAlso rowOk AndAlso ok,
                  String.Format("flushed={0} header={1} row={2} ('{3}') values={4}",
                                flushed, headerOk, rowOk, If(lines.Length > 1, lines(1), "<none>"), ok))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A53b: a five-field legacy row still parses, and reports identity ABSENT ------------
    ' Absent is NOT empty and NOT a value. This is the migration claim in D5 stated as a test:
    ' every month-file written by every prior binary must keep reading, with no rotation and no
    ' rewrite. Also pins that a SEVEN-column row with empty identity columns reads as absent
    ' too — an empty column is not an identity, and treating it as one is the A53c collapse.
    Private Sub A53b_LegacyRowParsesWithIdentityAbsent()
        Dim dir As String = A48TempStore("53b")
        Try
            Dim path As String = TradeStoreWriter.TradeFileFor(dir, 2026, 8)
            Directory.CreateDirectory(dir)
            ' A genuine pre-identity file: the five-column header and five-column rows.
            File.WriteAllLines(path, {
                TradeStoreWriter.LegacyHeaderLine,
                A53Ms(0) & ",64000.00,100.00,buy,none",
                A53Ms(50) & ",64001.00,200.00,sell,none",
                A53Ms(90) & ",64002.00,300.00,buy,none,,"})   ' 7 cols, identity columns EMPTY

            Dim back = TradeStoreWriter.ReadTradeFile(path)
            Dim parsedAll As Boolean = back.Count = 3
            Dim absentOk As Boolean = parsedAll
            If parsedAll Then
                For Each r In back
                    If r.HasIdentity OrElse r.HasSeq Then absentOk = False
                    If r.TradeId IsNot Nothing Then absentOk = False           ' absent, not ""
                    If r.TradeSeq <> TradeStoreWriter.AbsentSeq Then absentOk = False
                Next
            End If
            ' The legacy values themselves survived intact.
            Dim valuesOk As Boolean = parsedAll AndAlso
                                      back(0).Timestamp = A53Ms(0) AndAlso back(0).Direction = "buy" AndAlso
                                      Math.Abs(back(2).Amount - 300.0) < 0.005

            Check("A53b legacy row — 5-col file still parses, identity reports ABSENT (Nothing/-1) not empty; 7-col empty columns also absent",
                  parsedAll AndAlso absentOk AndAlso valuesOk,
                  String.Format("count={0} absent={1} values={2}", back.Count, absentOk, valuesOk))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A53c: ⚠ THE EMPTY-IDENTITY COLLAPSE ----------------------------------------------
    ' Written from docs/trade-store-trade-identity-proposal.md §3.4 before reading the
    ' implementation. The contract sentence this encodes:
    '
    '   "Never key on an absent or empty identity. A missing identity is not a value and
    '    must not join a group."
    '
    ' If dedup keys on trade_id and a legacy row has none, every legacy row keys on the SAME
    ' empty string and the whole file collapses to ONE row — the original defect, reproduced
    ' at greater scale by the code written to fix it. Ten rows differing only in amount are
    ' ten distinct trades and must survive as ten.
    Private Sub A53c_EmptyIdentityDoesNotCollapseLegacyRows()
        Dim rows As New List(Of TradeRecord)
        For i As Integer = 1 To 10
            ' Identical in EVERY field except amount. No identity on any of them.
            rows.Add(A53Trade(A53Ms(0), 64000.0, 100.0 * i, "buy"))
        Next

        Dim deduped = TradeStoreWriter.DedupTrades(rows)

        ' The failure mode stated explicitly, so the fixture names what it is guarding against:
        ' keying these on TradeId would produce exactly 1.
        Dim collapsedTo As Integer = rows.Select(Function(r) If(r.TradeId, "")).Distinct().Count()

        Check("A53c ⚠ empty-identity collapse — 10 identity-less rows differing only in amount survive dedup as 10, not 1",
              deduped.Count = 10 AndAlso collapsedTo = 1,
              String.Format("deduped={0} (want 10) · naive-identity-key would give {1}", deduped.Count, collapsedTo))
    End Sub

    ' -- A53d: two rows with equal trade_id and differing other fields dedup to one ---------
    ' The positive half of the identity contract: identity WINS over content. A gap-repair page
    ' and a streamed print of the same trade can differ in formatting or in the liquidation
    ' flag; if the ids agree it is one trade.
    Private Sub A53d_EqualIdentityDedupsToOne()
        Dim rows As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000.0, 100.0, "buy", "none", "439922700", 295960001L),
            A53Trade(A53Ms(5), 64000.5, 250.0, "sell", "T", "439922700", 295960001L)}

        Dim deduped = TradeStoreWriter.DedupTrades(rows)
        ' First occurrence wins, and it is the one kept.
        Dim keptFirst As Boolean = deduped.Count = 1 AndAlso deduped(0).Timestamp = A53Ms(0)

        Check("A53d equal trade_id — same identity, different content, dedups to one (identity beats content)",
              deduped.Count = 1 AndAlso keptFirst,
              String.Format("deduped={0} keptFirst={1}", deduped.Count, keptFirst))
    End Sub

    ' -- A53e: ⚠ THE SILENT NO-OP ---------------------------------------------------------
    ' Written from docs/trade-store-trade-identity-proposal.md §0 trap 2 before reading the
    ' implementation. TryParseRow accepts `parts.Length < 5`, so it already TOLERATED extra
    ' columns and IGNORED them. A writer that emits trade_id against a reader that never reads
    ' it produces a full store, a green harness and no behaviour change whatsoever.
    '
    ' This fixture fails if the reader ignores the new column. The two rows are identical in
    ' all five legacy fields and differ ONLY in trade_id — so under the old five-field dedup
    ' they collapse to one, and under a correct identity dedup they stay two.
    '
    ' ⚠ These are not synthetic. Both were returned by Deribit on 2026-08-08 at the §1 gate,
    ' in the FIRST three trades fetched: same millisecond, same price, same amount, same
    ' direction, distinct ids. The defect is this common.
    Private Sub A53e_DistinctIdentitySameLegacyFieldsSurvivesAsTwo()
        Dim shared5 As Long = 1786122637808L
        Dim rows As New List(Of TradeRecord) From {
            A53Trade(shared5, 64730.0, 10.0, "sell", "none", "439922656", 295960018L),
            A53Trade(shared5, 64730.0, 10.0, "sell", "none", "439922657", 295960019L)}

        ' Proof the two rows really are indistinguishable on the legacy five fields — without
        ' this, the fixture could pass for the wrong reason (rows that were never colliding).
        Dim legacyIdentical As Boolean =
            TradeStoreWriter.LegacyRowKey(rows(0)) = TradeStoreWriter.LegacyRowKey(rows(1))

        Dim deduped = TradeStoreWriter.DedupTrades(rows)

        Check("A53e ⚠ silent no-op — 2 real trades identical in all 5 legacy fields but with different trade_id survive as 2",
              legacyIdentical AndAlso deduped.Count = 2,
              String.Format("legacyKeysIdentical={0} deduped={1} (want 2 — 1 means the reader ignores TradeId)",
                            legacyIdentical, deduped.Count))
    End Sub

    ' -- A53f: mixed-shape file — legacy and identified rows in ONE file --------------------
    ' §5 calls this the NORMAL case, not an edge case: after the AWS redeploy every current
    ' month-file holds five-column rows written before it and seven-column rows written after.
    ' Both dedup branches must work in the same pass, and the result must not depend on the
    ' order the rows happen to sit in the file.
    Private Sub A53f_MixedShapeFileDedupsUnderBothBranches()
        Dim dir As String = A48TempStore("53f")
        Try
            Dim path As String = TradeStoreWriter.TradeFileFor(dir, 2026, 8)
            Directory.CreateDirectory(dir)
            File.WriteAllLines(path, {
                TradeStoreWriter.LegacyHeaderLine,
                A53Ms(0) & ",64000.00,100.00,buy,none",                       ' legacy A
                A53Ms(10) & ",64001.00,200.00,sell,none",                     ' legacy B
                A53Ms(0) & ",64000.00,100.00,buy,none",                       ' legacy A again → drop
                A53Ms(20) & ",64002.00,300.00,buy,none,439922801,295960101",  ' identified C
                A53Ms(20) & ",64002.00,300.00,buy,none,439922802,295960102",  ' identified D — same 5 fields as C, keep
                A53Ms(20) & ",64002.00,300.00,buy,none,439922801,295960101"}) ' C again → drop

            Dim deduped = TradeStoreWriter.DedupTrades(TradeStoreWriter.ReadTradeFile(path))
            Dim identified As Integer = deduped.Where(Function(r) r.HasIdentity).Count()
            Dim legacy As Integer = deduped.Where(Function(r) Not r.HasIdentity).Count()

            ' Order-independence: the same file read back-to-front must dedup to the same count.
            Dim reversed = TradeStoreWriter.ReadTradeFile(path)
            reversed.Reverse()
            Dim dedupedRev = TradeStoreWriter.DedupTrades(reversed)

            Check("A53f mixed-shape file — legacy + identified rows dedup under both branches (2 legacy + 2 identified), order-independently",
                  deduped.Count = 4 AndAlso identified = 2 AndAlso legacy = 2 AndAlso dedupedRev.Count = 4,
                  String.Format("deduped={0} identified={1} legacy={2} reversed={3}",
                                deduped.Count, identified, legacy, dedupedRev.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A53g: venue diff reports the two match populations SEPARATELY ---------------------
    ' D4. A single blended "matched" number would hide exactly the ambiguity this build exists
    ' to remove: a fallback match is a five-field coincidence, an identity match is proof. The
    ' store here is deliberately MIXED, which is what every real store will be after the
    ' redeploy, so both arms are exercised in one call.
    Private Sub A53g_VenueDiffSeparatesIdentityAndFallbackMatches()
        Dim storeTrades As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000.0, 100.0, "buy", "none", "id-1", 1L),   ' identified
            A53Trade(A53Ms(10), 64001.0, 200.0, "sell")}                     ' legacy, no identity

        Dim venueTrades As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000.0, 100.0, "buy", "none", "id-1", 1L),   ' → identity match
            A53Trade(A53Ms(10), 64001.0, 200.0, "sell", "none", "id-2", 2L), ' → fallback match
            A53Trade(A53Ms(20), 64002.0, 300.0, "buy", "none", "id-3", 3L)}  ' → missing

        Dim d = CoverageReport.ComputeVenueDiff(storeTrades, venueTrades)

        Check("A53g venue diff — identity-matched and fallback-matched counted and reported SEPARATELY, never blended",
              d.IdentityMatched = 1 AndAlso d.FallbackMatched = 1 AndAlso
              d.MissingTrades.Count = 1 AndAlso d.MissingTrades(0).TradeId = "id-3" AndAlso
              d.StoreIdentified = 1 AndAlso d.StoreLegacyOnly = 1,
              String.Format("identity={0} fallback={1} missing={2} storeIdentified={3} storeLegacy={4}",
                            d.IdentityMatched, d.FallbackMatched, d.MissingTrades.Count,
                            d.StoreIdentified, d.StoreLegacyOnly))
    End Sub

    ' -- A53h: trade_seq gap detection -----------------------------------------------------
    ' The §3.3 property: a store holding 100, 101, 103 is PROVABLY missing 102, with no venue
    ' call, no network and no exposure to Deribit's ~24 h trade retention. This is what retires
    ' the clock that made a daily S0 job urgent.
    '
    ' Also pins the false-clean trap: a store with NO sequences at all finds zero gaps, which is
    ' "nothing to check", not "nothing wrong". If that reported OK the metric would be worse
    ' than useless on exactly the legacy data it will meet first.
    '
    ' ⚠ Whether Deribit ever RESETS trade_seq was not verified at the §1 gate, so a backwards
    ' step is reported as a discontinuity and deliberately NOT counted as loss.
    Private Sub A53h_SequenceGapDetection()
        Dim withGap As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000, 10, "buy", "none", "id-a", 100L),
            A53Trade(A53Ms(10), 64001, 10, "buy", "none", "id-b", 101L),
            A53Trade(A53Ms(20), 64002, 10, "buy", "none", "id-c", 103L)}
        Dim g = CoverageReport.ComputeSequenceGaps(withGap)
        Dim gapOk As Boolean = g.MissingCount = 1 AndAlso g.GapRuns = 1 AndAlso
                               g.LongestGap = 1 AndAlso g.RowsWithSeq = 3 AndAlso
                               g.RowsWithoutSeq = 0 AndAlso g.Checkable

        ' Contiguous ⇒ no gaps.
        Dim contiguous As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000, 10, "buy", "none", "id-a", 100L),
            A53Trade(A53Ms(10), 64001, 10, "buy", "none", "id-b", 101L),
            A53Trade(A53Ms(20), 64002, 10, "buy", "none", "id-c", 102L)}
        Dim c = CoverageReport.ComputeSequenceGaps(contiguous)
        Dim cleanOk As Boolean = c.MissingCount = 0 AndAlso c.GapRuns = 0 AndAlso c.Checkable

        ' ⚠ The false-clean trap: legacy rows carry no sequence, so there is nothing to check.
        ' Zero gaps here must NOT read as a clean store.
        Dim legacyOnly As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000, 10, "buy"),
            A53Trade(A53Ms(10), 64001, 10, "buy")}
        Dim l = CoverageReport.ComputeSequenceGaps(legacyOnly)
        Dim notCheckableOk As Boolean = l.MissingCount = 0 AndAlso Not l.Checkable AndAlso
                                        l.RowsWithoutSeq = 2 AndAlso l.RowsWithSeq = 0

        ' Scattered loss — the shape S3's longest-gap metric cannot see. Three separate holes.
        Dim scattered As New List(Of TradeRecord) From {
            A53Trade(A53Ms(0), 64000, 10, "buy", "none", "id-a", 200L),
            A53Trade(A53Ms(10), 64001, 10, "buy", "none", "id-b", 202L),
            A53Trade(A53Ms(20), 64002, 10, "buy", "none", "id-c", 204L),
            A53Trade(A53Ms(30), 64003, 10, "buy", "none", "id-d", 206L)}
        Dim s = CoverageReport.ComputeSequenceGaps(scattered)
        Dim scatteredOk As Boolean = s.MissingCount = 3 AndAlso s.GapRuns = 3 AndAlso s.LongestGap = 1

        Check("A53h trade_seq gap detection — 100/101/103 reports 102 missing · contiguous clean · " &
              "no-sequence store reports NOT CHECKABLE not clean · scattered loss counted as 3 runs",
              gapOk AndAlso cleanOk AndAlso notCheckableOk AndAlso scatteredOk,
              String.Format("gap(missing={0},runs={1}) clean={2} notCheckable={3} scattered(missing={4},runs={5})",
                            g.MissingCount, g.GapRuns, cleanOk, notCheckableOk, s.MissingCount, s.GapRuns))
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A55 — the trade-store WRITE guard, keyed on identity
    ' (docs/trade-store-write-guard-identity-proposal.md §5)
    '
    ' ⚠ WHAT THESE EXIST FOR, and it is a lesson about fixtures rather than about code. The
    ' shipped guard was `If t.Timestamp <= _lastTs Then Return False` — a millisecond used as
    ' an identity. It discarded 49.2 % of the live tape for ten days, inside a path covered by
    ' EIGHT fixtures, one of them called "monotonic guard". None of them noticed, for one
    ' reason: every A48 fixture builds its trades `A48Ms(i * 1000L)`, one SECOND apart, so no
    ' two distinct trades were ever put on the same millisecond.
    '
    ' ⚠ A fixture that does not put two distinct trades on ONE millisecond does not test this.
    ' Every fixture below therefore does, INCLUDING the three whose stated job is only to stop
    ' an old property regressing (A55b, A55f, A55g) — a regression guard that passes on the
    ' unfixed code proves nothing about the fix, so each was given a sibling pair until it
    ' failed. All seven were confirmed to FAIL against the shipped `<=` guard (§5 mutation).
    ' ═══════════════════════════════════════════════════════════════════════════════════

    ' -- A55a: ⭐ THE DEFECT — two trades on one millisecond both reach disk -----------------
    ' ⚠ REAL Deribit data, reused from A53e: two trades in the FIRST THREE fetched at the
    ' identity build's §1 gate. Same millisecond, same price, same amount, same direction,
    ' distinct trade_id. A53e aimed this pair at the READ path's dedup contract and passed; the
    ' WRITE guard was never shown it, which is exactly how the defect survived that build.
    Private Sub A55a_SameMillisecondSiblingsBothSurvive()
        Dim dir As String = A48TempStore("55a")
        Try
            Dim sharedMs As Long = 1786122637808L
            Dim a = A53Trade(sharedMs, 64730.0, 10.0, "sell", "none", "439922656", 295960018L)
            Dim b = A53Trade(sharedMs, 64730.0, 10.0, "sell", "none", "439922657", 295960019L)

            ' Proof the two really are indistinguishable on the five legacy fields — without it
            ' the fixture could pass for the wrong reason, on rows that never collided.
            Dim legacyIdentical As Boolean =
                TradeStoreWriter.LegacyRowKey(a) = TradeStoreWriter.LegacyRowKey(b)

            Dim w As New TradeStoreWriter(dir)
            Dim accepted As Integer = A48BufferAll(w, New List(Of TradeRecord) From {a, b})
            Dim flushed As Integer = w.Flush()

            ' Month derived from the real timestamp rather than hand-written, so the fixture
            ' cannot quietly read a different file than the writer wrote.
            Dim utc As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(sharedMs).UtcDateTime
            Dim path As String = TradeStoreWriter.TradeFileFor(dir, utc.Year, utc.Month)
            Dim back = TradeStoreWriter.ReadTradeFile(path)
            Dim bothOnDisk As Boolean = back.Count = 2 AndAlso
                                        back.Any(Function(r) r.TradeId = "439922656") AndAlso
                                        back.Any(Function(r) r.TradeId = "439922657")

            Check("A55a ⭐ same-millisecond siblings — 2 REAL trades on one ms with distinct trade_id BOTH reach disk",
                  legacyIdentical AndAlso accepted = 2 AndAlso flushed = 2 AndAlso bothOnDisk,
                  String.Format("legacyKeysIdentical={0} accepted={1} (want 2 — 1 IS the shipped defect) flushed={2} onDisk={3}",
                                legacyIdentical, accepted, flushed, back.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A55b: reconnect re-seed replays a batch ⇒ written ONCE ----------------------------
    ' The guard's REASON TO EXIST. SeedAsync re-seeds the ring from REST on every (re)connect
    ' and the WS may replay on re-subscribe, so duplicates genuinely arrive. A48b's property
    ' must not regress — loosening the guard until replays get through would trade one silent
    ' defect for another.
    '
    ' ⚠ The batch deliberately carries a same-millisecond sibling pair (…801/…802). Without it
    ' this fixture passes on the shipped code and proves nothing, which is A48b's exact failure.
    Private Sub A55b_ReconnectReplayWritesOnce()
        Dim dir As String = A48TempStore("55b")
        Try
            Dim batch As New List(Of TradeRecord) From {
                A53Trade(A53Ms(0), 64000.0, 100.0, "buy", "none", "439922800", 295960200L),
                A53Trade(A53Ms(500), 64002.0, 300.0, "buy", "none", "439922801", 295960201L),
                A53Trade(A53Ms(500), 64002.0, 300.0, "buy", "none", "439922802", 295960202L)}

            Dim w As New TradeStoreWriter(dir)
            Dim accepted1 As Integer = A48BufferAll(w, batch)
            w.Flush()
            ' (1) identical replay with no reconnect — the WS re-subscribe case.
            Dim accepted2 As Integer = A48BufferAll(w, batch)
            w.Flush()

            ' (2) the REAL reconnect path: ResetBufferState flushes, clears the window and
            '     un-seeds, so the window is rebuilt from the on-disk tail (D-4).
            w.ResetBufferState()
            Dim accepted3 As Integer = A48BufferAll(w, batch)
            w.Flush()

            ' (3) a genuinely new trade still lands after all that.
            Dim accepted4 As Integer = If(w.Buffer(A53Trade(A53Ms(900), 64003.0, 400.0, "sell",
                                                            "none", "439922803", 295960203L)), 1, 0)
            w.Flush()

            Dim back = TradeStoreWriter.ReadTradeFile(TradeStoreWriter.TradeFileFor(dir, 2026, 8))

            Check("A55b reconnect re-seed — a batch holding same-ms siblings replays 3× and is written ONCE; new trades still land",
                  accepted1 = 3 AndAlso accepted2 = 0 AndAlso accepted3 = 0 AndAlso
                  accepted4 = 1 AndAlso back.Count = 4,
                  String.Format("acc1={0}(want 3) acc2={1} acc3={2}(post-ResetBufferState) acc4={3} onDisk={4}(want 4)",
                                accepted1, accepted2, accepted3, accepted4, back.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A55c: a MIXED batch — some rows identified, some not ------------------------------
    ' §5's fallback arm. After the AWS redeploy this is the normal case, not an edge case: a
    ' month file holds pre-identity rows and identified rows, and a re-seeded REST window can
    ' hand the writer either shape. Neither duplicates nor drops are allowed in one pass.
    Private Sub A55c_MixedIdentifiedAndLegacyBatch()
        Dim dir As String = A48TempStore("55c")
        Try
            ' Rows 1-2: IDENTIFIED trades on one ms, identical in all five legacy fields, so
            '           separable only by trade_id.
            ' Rows 3-4: IDENTITY-LESS trades on one (later) ms, separable only by amount.
            Dim batch As New List(Of TradeRecord) From {
                A53Trade(A53Ms(500), 64002.0, 300.0, "buy", "none", "439922900", 295960300L),
                A53Trade(A53Ms(500), 64002.0, 300.0, "buy", "none", "439922901", 295960301L),
                A53Trade(A53Ms(600), 64003.0, 100.0, "sell"),
                A53Trade(A53Ms(600), 64003.0, 200.0, "sell")}

            Dim w As New TradeStoreWriter(dir)
            Dim accepted As Integer = A48BufferAll(w, batch)
            w.Flush()
            ' No drops above; no duplicates below.
            Dim replayed As Integer = A48BufferAll(w, batch)
            w.Flush()

            Dim back = TradeStoreWriter.ReadTradeFile(TradeStoreWriter.TradeFileFor(dir, 2026, 8))
            Dim identified As Integer = back.Where(Function(r) r.HasIdentity).Count()
            Dim legacy As Integer = back.Where(Function(r) Not r.HasIdentity).Count()

            Check("A55c mixed batch — 2 identified siblings + 2 identity-less rows all land, and a replay of the same batch adds none",
                  accepted = 4 AndAlso replayed = 0 AndAlso back.Count = 4 AndAlso
                  identified = 2 AndAlso legacy = 2,
                  String.Format("accepted={0}(want 4) replayed={1} onDisk={2} identified={3} legacy={4}",
                                accepted, replayed, back.Count, identified, legacy))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A55d: ⚠ THE EMPTY-IDENTITY COLLAPSE, at the WRITE path ----------------------------
    ' A53c's property, one path further upstream. If the guard keys identity-less rows on ""
    ' they all collapse into a single group and nine of ten real trades never reach disk — the
    ' original defect reproduced at greater scale inside its own fix (§0 trap 2).
    '
    ' Ten rows identical in every field EXCEPT amount, all on one millisecond. That is not a
    ' contrived shape: it is what a book-sweep looks like once the identity columns are absent.
    Private Sub A55d_IdentitylessRowsDifferingOnlyInAmountAllSurvive()
        Dim dir As String = A48TempStore("55d")
        Try
            Dim w As New TradeStoreWriter(dir)
            Dim rows As New List(Of TradeRecord)
            For i As Integer = 1 To 10
                rows.Add(A53Trade(A53Ms(0), 64000.0, 100.0 * i, "buy"))
            Next
            Dim accepted As Integer = A48BufferAll(w, rows)
            w.Flush()

            ' The pairing that stops this passing on a guard that admits EVERYTHING: an EXACT
            ' duplicate of one of them — same five fields, still no identity — must be dropped.
            Dim exactDupRejected As Boolean = Not w.Buffer(A53Trade(A53Ms(0), 64000.0, 300.0, "buy"))
            w.Flush()

            Dim back = TradeStoreWriter.ReadTradeFile(TradeStoreWriter.TradeFileFor(dir, 2026, 8))

            Check("A55d ⚠ empty-identity collapse at the WRITE path — 10 identity-less rows on ONE ms differing only in amount all reach disk; an exact duplicate still drops",
                  accepted = 10 AndAlso back.Count = 10 AndAlso exactDupRejected,
                  String.Format("accepted={0}(want 10 — 1 means the guard keyed on the ms, and a collapse to 1 means it keyed on empty identity) onDisk={1} exactDupRejected={2}",
                                accepted, back.Count, exactDupRejected))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A55e: ⚠ THE BIAS — a duplicate older than the window is ADMITTED, not dropped ------
    ' §3.1 stated as a test. The costs are asymmetric: a duplicate on disk is harmless (the read
    ' path dedups it, and A48d already writes duplicates deliberately), while a dropped trade is
    ' unrecoverable past Deribit's ~24 h retention. So once a trade has aged out of the window
    ' the guard no longer KNOWS it was seen, and it must admit.
    '
    ' ⚠ If a future edit makes this fixture assert the opposite, the guard has the bias backwards
    ' and the defect is back. No Flush here on purpose — the window advances on Buffer (§3.4), so
    ' the boundary is reachable without writing 20,000 rows to disk.
    Private Sub A55e_DuplicateOlderThanWindowIsAdmitted()
        Dim dir As String = A48TempStore("55e")
        Try
            Dim w As New TradeStoreWriter(dir)
            ' Identified, so this exercises the identity arm and not the legacy fallback.
            Dim oldest = A53Trade(A53Ms(0), 64000.0, 10.0, "buy", "none", "id-oldest", 500000L)
            Dim acceptedOldest As Boolean = w.Buffer(oldest)

            ' Fill exactly to the cap. Reads the PRODUCTION constant rather than restating
            ' 20000 — the F1 lesson: a fixture that restates the number still passes when the
            ' number changes underneath it.
            Dim cap As Integer = TradeStoreWriter.RecentWindowCapacity
            For i As Integer = 1 To cap
                w.Buffer(A53Trade(A53Ms(i), 64000.0 + i, 10.0, "buy", "none", "id-fill-" & i, 500000L + i))
            Next
            Dim windowFull As Boolean = w.RecentWindowCount = cap

            ' `oldest` has now been evicted ⇒ ADMIT.
            Dim readmitted As Boolean = w.Buffer(oldest)

            ' The pairing: a trade still INSIDE the window is still rejected, so this cannot
            ' pass on a guard that has simply stopped guarding.
            Dim insideStillRejected As Boolean =
                Not w.Buffer(A53Trade(A53Ms(cap), 64000.0 + cap, 10.0, "buy", "none",
                                      "id-fill-" & cap, 500000L + cap))

            Check("A55e ⚠ window bias — a duplicate aged OUT of the window is ADMITTED (a dup on disk is harmless, a drop is not); one still inside is rejected",
                  acceptedOldest AndAlso windowFull AndAlso readmitted AndAlso insideStillRejected,
                  String.Format("acceptedOldest={0} windowCount={1}(want {2}) readmitted={3}(want True) insideStillRejected={4}",
                                acceptedOldest, w.RecentWindowCount, cap, readmitted, insideStillRejected))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A55f: restart over a populated file seeds the window from the tail ------------------
    ' D-4(a). A fresh writer over an existing store must not re-write the rows it already holds,
    ' and must still accept a NEW sibling of the row at the tail — which is the case the shipped
    ' guard got wrong at every restart, not merely in a batch.
    Private Sub A55f_RestartSeedsWindowFromFileTail()
        Dim dir As String = A48TempStore("55f")
        Try
            Dim w1 As New TradeStoreWriter(dir)
            Dim seed As New List(Of TradeRecord) From {
                A53Trade(A53Ms(0), 64000.0, 100.0, "buy", "none", "439922950", 295960350L),
                A53Trade(A53Ms(100), 64001.0, 200.0, "sell", "none", "439922951", 295960351L),
                A53Trade(A53Ms(100), 64001.0, 200.0, "sell", "none", "439922952", 295960352L)}
            Dim accepted1 As Integer = A48BufferAll(w1, seed)
            w1.Flush()

            ' The restart.
            Dim w2 As New TradeStoreWriter(dir)
            ' (1) a re-delivered trade already on disk is rejected — the point of D-4.
            Dim dupRejected As Boolean = Not w2.Buffer(seed(1))
            ' (2) ⚠ a NEW sibling on the same millisecond as the on-disk tail must still land.
            Dim newSibling = A53Trade(A53Ms(100), 64001.0, 200.0, "sell", "none", "439922953", 295960353L)
            Dim siblingAccepted As Boolean = w2.Buffer(newSibling)
            w2.Flush()

            Dim back = TradeStoreWriter.ReadTradeFile(TradeStoreWriter.TradeFileFor(dir, 2026, 8))

            Check("A55f restart over a populated store — window seeds from the file tail: re-delivered rows drop, a NEW same-ms sibling of the tail still lands",
                  accepted1 = 3 AndAlso dupRejected AndAlso siblingAccepted AndAlso back.Count = 4,
                  String.Format("acc1={0}(want 3) dupRejected={1} siblingAccepted={2} onDisk={3}(want 4)",
                                accepted1, dupRejected, siblingAccepted, back.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A55g: unwritable store — never throws, and the guard still guards -------------------
    ' A48e's property must not regress: losing capture must never kill the feed or a run. The
    ' guard now holds state that a failed flush does not clear, so the no-throw path is worth
    ' re-pinning at the same time as the sibling behaviour.
    Private Sub A55g_UnwritableStoreNeverThrowsAndStillGuards()
        Dim root As String = A48TempStore("55g")
        Dim threw As String = ""
        Dim accepted As Integer = -1
        Dim wrote As Integer = -1
        Dim keptBuffering As Boolean = False
        Dim dupRejected As Boolean = False
        Try
            ' Store "directory" path occupied by a FILE, so CreateDirectory fails on flush.
            Dim asFile As String = Path.Combine(root, "not_a_dir")
            File.WriteAllText(asFile, "x")
            Dim w As New TradeStoreWriter(asFile)

            Dim siblings As New List(Of TradeRecord) From {
                A53Trade(A53Ms(500), 64002.0, 300.0, "buy", "none", "439922970", 295960370L),
                A53Trade(A53Ms(500), 64002.0, 300.0, "buy", "none", "439922971", 295960371L)}
            Try
                accepted = A48BufferAll(w, siblings)
                wrote = w.Flush()
                ' The fold keeps working afterwards — a capture failure is not a poison pill.
                keptBuffering = w.Buffer(A53Trade(A53Ms(900), 64003.0, 400.0, "sell",
                                                  "none", "439922972", 295960372L))
                ' ...and the guard is still a guard with no disk underneath it.
                dupRejected = Not w.Buffer(siblings(0))
            Catch ex As Exception
                threw = ex.Message
            End Try

            Check("A55g unwritable store — same-ms siblings both accepted, flush returns 0, never throws, fold keeps running, guard still rejects a dup",
                  threw = "" AndAlso accepted = 2 AndAlso wrote = 0 AndAlso keptBuffering AndAlso dupRejected,
                  String.Format("threw='{0}' accepted={1}(want 2) wrote={2} keptBuffering={3} dupRejected={4}",
                                threw, accepted, wrote, keptBuffering, dupRejected))
        Finally
            A48Cleanup(root)
        End Try
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A56 — hole-derived repair windows (TradeStoreWriter.ResolveRepairWindowsMs)
    ' docs/trade-store-downtime-repair-proposal.md §5
    '
    ' ⚠ WHAT THESE EXIST FOR. Gap repair seeded its fetch cursor from the file's LAST WRITTEN
    ' ROW. After a venue outage the app reconnects on its own, streaming makes that row current
    ' again within seconds, and every hole BEHIND it reads as "already covered" and is never
    ' fetched. Measured: 60.3 minutes of tape lost on 2026-08-11 (08:59:56 → 10:00:12 UTC),
    ' across two scheduled repair passes, now past Deribit's ~24 h retention.
    '
    ' ⚠ THE CORRECTNESS PROPERTY IS TWO-SIDED and one-sided reasoning passes its own tests:
    ' no MISSED holes (A56a) AND no PHANTOM holes (A56c, A56d, A56e). A phantom costs a REST
    ' fetch and — because the MaxHolesPerPass cap ranks by missing-sequence count — a single
    ' AbsentSeq-derived phantom would score ~296 million and evict every real hole from the pass.
    '
    ' ⚠ FIXTURE-LITERAL PROVENANCE (hard rule, 2026-08-11), declared once for the family:
    ' every timestamp and sequence number below is a CONSTRUCTED INPUT and asserts MECHANISM,
    ' so a literal is correct. The one exception is A56f, which asserts SHIPPED BEHAVIOUR and
    ' therefore READS TradeStoreWriter.MaxHolesPerPass instead of restating it. (MinHoleMs was
    ' the other production constant read here — REMOVED, DR-1, 2026-08-13: the width floor it
    ' gated is gone, not replaced.)

    ' A 2026-08-11 08:00 UTC base — the morning of the measured outage. Well inside one month.
    Private Function A56Ms(offsetMs As Long) As Long
        Return New DateTimeOffset(2026, 8, 11, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds() + offsetMs
    End Function

    ' Write rows through the PRODUCTION append path, so file order is arrival order and the
    ' out-of-order case in A56c can be built the way the store actually produces it.
    Private Sub A56Write(dir As String, rows As List(Of TradeRecord))
        TradeStoreWriter.AppendRows(dir, rows)
    End Sub

    Private Function A56Path(dir As String) As String
        Return TradeStoreWriter.TradeFileFor(dir, 2026, 8)
    End Function

    ' -- A56a: ⭐ THE DEFECT — a trade_seq-bracketed hole is returned ----------------------
    ' ⚠ THIS IS THE MUTATION PROOF. Revert ResolveRepairWindowsMs to return only the trailing
    ' window and this fixture MUST fail. If it still passes, the test is not testing what it
    ' claims and the build stops (proposal §0 escalation trigger).
    '
    ' Shape is the measured 2026-08-11 outage: three rows, a 60-minute silence, then two more.
    ' The venue kept trading through it, so trade_seq jumps 1002 → 1500 and PROVES the loss
    ' locally — no venue call, no time threshold.
    Private Sub A56a_SeqBracketedHoleIsReturned()
        Dim dir As String = A48TempStore("56a")
        Try
            A56Write(dir, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy", "none", "id-1", 1000L),
                A53Trade(A56Ms(30000), 64001, 10, "buy", "none", "id-2", 1001L),
                A53Trade(A56Ms(60000), 64002, 10, "buy", "none", "id-3", 1002L),
                A53Trade(A56Ms(3660000), 64100, 10, "sell", "none", "id-4", 1500L),
                A53Trade(A56Ms(3690000), 64101, 10, "sell", "none", "id-5", 1501L)})

            Dim segStart As Long = A56Ms(0)
            Dim segEnd As Long = A56Ms(7200000)
            Dim w = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(dir), segStart, segEnd,
                                                            clampToSegStart:=True)

            ' The hole window excludes both bracketing rows' own timestamps — those are held.
            Dim holeOk As Boolean = w.Count = 2 AndAlso
                                    w(0).StartMs = A56Ms(60000) + 1 AndAlso
                                    w(0).EndInclMs = A56Ms(3660000) - 1
            ' ...and the trailing window is still there, unchanged, AFTER it.
            Dim tailOk As Boolean = w.Count = 2 AndAlso
                                    w(1).StartMs = A56Ms(3690000) + 1 AndAlso
                                    w(1).EndInclMs = segEnd

            Check("A56a ⭐ the defect — a 60-min trade_seq-bracketed hole BEHIND the tail is returned as its own fetch window, tail last",
                  holeOk AndAlso tailOk,
                  String.Format("windows={0}(want 2) first=[{1},{2}] wantFirst=[{3},{4}] last=[{5},{6}] wantLast=[{7},{8}]",
                                w.Count,
                                If(w.Count > 0, w(0).StartMs, -1), If(w.Count > 0, w(0).EndInclMs, -1),
                                A56Ms(60000) + 1, A56Ms(3660000) - 1,
                                If(w.Count > 1, w(1).StartMs, -1), If(w.Count > 1, w(1).EndInclMs, -1),
                                A56Ms(3690000) + 1, segEnd))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A56b: a covered store returns the tail only, and A48d's property does not regress ---
    ' The invariant that makes this change safe: the LAST window is the trailing window and,
    ' for an in-order store, its start is EXACTLY today's ResolveResumeCursorMs result; an empty
    ' list is exactly today's -1. Asserted by CALLING the shipped function and comparing, not by
    ' restating what it returns — a restatement would pass even if the two drifted apart.
    '
    ' ⚠ Part 4, REPURPOSED (DR-1, docs/downtime-repair-followups-implementer-briefs.md §1) — the
    ' MinHoleMs width floor is GONE, so this no longer pins "a sub-threshold gap is not fetched".
    ' The property that survives the removal: a real sequence gap is UNFETCHABLE only when it
    ' inverts after clamping — the two bracketing rows one millisecond apart, so the venue
    ' window would be empty — and that drop is LOGGED. Any FETCHABLE gap, however narrow, is now
    ' returned as its own window; asserted with a gap well under the old 2,000 ms floor so the
    ' fixture would have failed against the pre-DR-1 code.
    Private Sub A56b_CoveredStoreReturnsTailOnlyAndA48dHolds()
        Dim dir As String = A48TempStore("56b")
        Try
            Dim rows As New List(Of TradeRecord)()
            For i As Integer = 0 To 10
                rows.Add(A53Trade(A56Ms(i * 30000L), 64000 + i, 10, "buy", "none",
                                  "id-" & i, 2000L + i))      ' contiguous: no holes at all
            Next
            A56Write(dir, rows)
            Dim path As String = A56Path(dir)
            Dim lastTs As Long = A56Ms(10 * 30000L)

            ' (1) window extending past the tail ⇒ tail window only, and it agrees with the
            '     shipped cursor.
            Dim segEndFar As Long = A56Ms(600000)
            Dim wFar = TradeStoreWriter.ResolveRepairWindowsMs(path, A56Ms(0), segEndFar, True)
            Dim cursorFar As Long = TradeStoreWriter.ResolveResumeCursorMs(path, A56Ms(0), segEndFar, True)
            Dim farOk As Boolean = wFar.Count = 1 AndAlso wFar(0).StartMs = cursorFar AndAlso
                                   wFar(0).EndInclMs = segEndFar AndAlso cursorFar = lastTs + 1

            ' (2) already-covered window ⇒ EMPTY list, which is exactly today's -1.
            Dim wCovered = TradeStoreWriter.ResolveRepairWindowsMs(path, A56Ms(0), lastTs, True)
            Dim cursorCovered As Long = TradeStoreWriter.ResolveResumeCursorMs(path, A56Ms(0), lastTs, True)
            Dim coveredOk As Boolean = wCovered.Count = 0 AndAlso cursorCovered = -1

            ' (3) empty store ⇒ the whole window, matching today's segStart fallback.
            Dim emptyDir As String = A48TempStore("56b2")
            Dim wEmpty = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(emptyDir), A56Ms(0), segEndFar, True)
            Dim emptyOk As Boolean = wEmpty.Count = 1 AndAlso wEmpty(0).StartMs = A56Ms(0) AndAlso
                                     wEmpty(0).EndInclMs = segEndFar
            A48Cleanup(emptyDir)

            ' (4) NO WIDTH FLOOR — a real sequence gap well under the old 2,000 ms MinHoleMs
            '     value IS returned as its own fetch window, ahead of the tail. Would FAIL
            '     pre-DR-1 (old code returned the tail alone, count=1).
            Dim tinyDir As String = A48TempStore("56b3")
            Dim tinyGap As Long = 3L   ' 3 ms — comfortably fetchable, comfortably sub-2000ms
            A56Write(tinyDir, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy", "none", "t-1", 9000L),
                A53Trade(A56Ms(tinyGap), 64001, 10, "buy", "none", "t-2", 9005L)})
            Dim wTiny = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(tinyDir), A56Ms(0),
                                                                A56Ms(600000), True)
            Dim tinyOk As Boolean = wTiny.Count = 2 AndAlso
                                    wTiny(0).StartMs = A56Ms(0) + 1 AndAlso
                                    wTiny(0).EndInclMs = A56Ms(tinyGap) - 1 AndAlso
                                    wTiny(1).StartMs = A56Ms(tinyGap) + 1
            A48Cleanup(tinyDir)

            ' (5) UNFETCHABLE — two rows one millisecond apart carrying a real sequence gap
            '     invert after clamping (their hole window is [ts+1, ts]) and are dropped, not
            '     returned. This is the one drop DR-1 keeps, and the only one left to keep.
            Dim unfetchDir As String = A48TempStore("56b4")
            A56Write(unfetchDir, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy", "none", "u-1", 9100L),
                A53Trade(A56Ms(1), 64001, 10, "buy", "none", "u-2", 9105L)})
            Dim wUnfetch = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(unfetchDir), A56Ms(0),
                                                                   A56Ms(600000), True)
            ' Only the tail window survives — the 1-ms-wide hole itself is unfetchable.
            Dim unfetchOk As Boolean = wUnfetch.Count = 1 AndAlso
                                       wUnfetch(0).StartMs = A56Ms(1) + 1
            A48Cleanup(unfetchDir)

            Check("A56b covered store ⇒ tail window only · already-covered ⇒ empty list (today's -1) · empty store ⇒ whole window · sub-old-floor gap IS fetched · 1ms-apart gap is unfetchable and dropped",
                  farOk AndAlso coveredOk AndAlso emptyOk AndAlso tinyOk AndAlso unfetchOk,
                  String.Format("far={0}(n={1} start={2} cursor={3}) covered={4}(n={5} cursor={6}) empty={7} tiny={8}(n={9}) unfetch={10}(n={11})",
                                farOk, wFar.Count, If(wFar.Count > 0, wFar(0).StartMs, -1), cursorFar,
                                coveredOk, wCovered.Count, cursorCovered, emptyOk, tinyOk, wTiny.Count,
                                unfetchOk, wUnfetch.Count))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A56c: ⚠ TRAP 1 — an out-of-order store produces ZERO phantom holes -----------------
    ' The store is NOT sorted, and LastTradeTimestamp's own summary records it: repair appends
    ' its pages AFTER whatever streaming has already written. Built here exactly that way — two
    ' AppendRows calls, the second carrying OLDER timestamps — so the file is genuinely out of
    ' order rather than notionally so.
    '
    ' Walked in append order this reports a phantom hole at the block boundary and each phantom
    ' costs a REST fetch. Walked sorted, the sequences are contiguous and there is no hole.
    Private Sub A56c_OutOfOrderStoreProducesNoPhantomHoles()
        Dim dir As String = A48TempStore("56c")
        Try
            ' Streaming wrote these first, and it already had a hole in the middle.
            A56Write(dir, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy", "none", "s-1", 1000L),
                A53Trade(A56Ms(120000), 64004, 10, "buy", "none", "s-2", 1004L),
                A53Trade(A56Ms(150000), 64005, 10, "buy", "none", "s-3", 1005L)})
            ' A repair pass then filled it — appended LATER, timestamped EARLIER.
            A56Write(dir, New List(Of TradeRecord) From {
                A53Trade(A56Ms(30000), 64001, 10, "buy", "none", "r-1", 1001L),
                A53Trade(A56Ms(60000), 64002, 10, "buy", "none", "r-2", 1002L),
                A53Trade(A56Ms(90000), 64003, 10, "buy", "none", "r-3", 1003L)})

            Dim segEnd As Long = A56Ms(600000)
            Dim w = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(dir), A56Ms(0), segEnd, True)

            ' Exactly one window — the tail — and it resumes past the MAXIMUM timestamp, not
            ' past the last line (which is A56Ms(90000), the repair block's end).
            Dim ok As Boolean = w.Count = 1 AndAlso w(0).StartMs = A56Ms(150000) + 1 AndAlso
                                w(0).EndInclMs = segEnd
            ' Confirm the file really IS out of order, so a future refactor that silently starts
            ' writing sorted does not turn this fixture into a tautology.
            Dim lastLine As Long = TradeStoreWriter.LastTradeTimestamp(A56Path(dir))
            Dim genuinelyUnsorted As Boolean = lastLine = A56Ms(90000)

            Check("A56c ⚠ out-of-order store — repair block appended after newer streaming rows ⇒ ZERO phantom holes, tail resumes past the MAX ts not the last line",
                  ok AndAlso genuinelyUnsorted,
                  String.Format("windows={0}(want 1) start={1}(want {2}) lastLine={3}(want {4}, proves unsorted)",
                                w.Count, If(w.Count > 0, w(0).StartMs, -1), A56Ms(150000) + 1,
                                lastLine, A56Ms(90000)))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A56d: ⚠ TRAP 2 — AbsentSeq rows produce ZERO phantom holes ------------------------
    ' TradeRecord.AbsentSeq is -1 and every pre-2026-08-10 row carries it. Feeding it into the
    ' gap arithmetic makes a legacy→identified boundary look like a hole ~296 million wide, and
    ' that count would win the MaxHolesPerPass ranking outright and evict every real hole.
    '
    ' ⚠ TWO SHAPES, and the second is the one the trap is actually about. The proposal's fixture
    ' text named only the ERA BOUNDARY (part 1) — but that shape has seq-carrying rows on ONE
    ' side only, so it cannot produce a phantom under any reading and does not test the
    ' decision. Part 2 INTERLEAVES legacy rows BETWEEN two identified rows, which is what the
    ' timestamp sort produces from a mixed-era store, and it is the shape that separates
    ' "skip the row" (phantom) from "break the walk" (correct).
    Private Sub A56d_AbsentSeqRowsProduceNoPhantomHoles()
        Dim dirEra As String = A48TempStore("56d1")
        Dim dirMix As String = A48TempStore("56d2")
        Try
            ' Part 1 — the era boundary: legacy block, then identified block.
            A56Write(dirEra, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy"),
                A53Trade(A56Ms(30000), 64001, 10, "buy"),
                A53Trade(A56Ms(60000), 64002, 10, "buy"),
                A53Trade(A56Ms(90000), 64003, 10, "buy", "none", "e-1", 2000L),
                A53Trade(A56Ms(120000), 64004, 10, "buy", "none", "e-2", 2001L),
                A53Trade(A56Ms(150000), 64005, 10, "buy", "none", "e-3", 2002L)})
            Dim segEnd As Long = A56Ms(600000)
            Dim wEra = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(dirEra), A56Ms(0), segEnd, True)
            Dim eraOk As Boolean = wEra.Count = 1 AndAlso wEra(0).StartMs = A56Ms(150000) + 1

            ' Part 2 — ⚠ INTERLEAVED. The legacy rows COVER the ground between seq 3000 and
            ' 3500, so there is no hole. Skipping past them reports 499 missing sequences and a
            ' 90-second phantom window over ground the store already holds.
            A56Write(dirMix, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy", "none", "m-1", 3000L),
                A53Trade(A56Ms(30000), 64001, 10, "buy"),
                A53Trade(A56Ms(60000), 64002, 10, "buy"),
                A53Trade(A56Ms(90000), 64003, 10, "buy"),
                A53Trade(A56Ms(120000), 64004, 10, "buy", "none", "m-2", 3500L)})
            Dim wMix = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(dirMix), A56Ms(0), segEnd, True)
            Dim mixOk As Boolean = wMix.Count = 1 AndAlso wMix(0).StartMs = A56Ms(120000) + 1

            ' The sentinel itself, read rather than restated — if AbsentSeq ever stopped being
            ' negative, HasSeq and the walk's `Seq < 0` test would part company silently.
            Dim legacyRow = A53Trade(A56Ms(0), 64000, 10, "buy")
            Dim sentinelOk As Boolean = Not legacyRow.HasSeq AndAlso
                                        legacyRow.TradeSeq = TradeStoreWriter.AbsentSeq AndAlso
                                        TradeStoreWriter.AbsentSeq < 0

            Check("A56d ⚠ AbsentSeq — legacy rows at an era boundary AND interleaved between two identified rows both yield ZERO phantom holes; sentinel is negative",
                  eraOk AndAlso mixOk AndAlso sentinelOk,
                  String.Format("era={0}(n={1}) interleaved={2}(n={3} start={4} want {5}) sentinel={6}",
                                eraOk, wEra.Count, mixOk, wMix.Count,
                                If(wMix.Count > 0, wMix(0).StartMs, -1), A56Ms(120000) + 1, sentinelOk))
        Finally
            A48Cleanup(dirEra)
            A48Cleanup(dirMix)
        End Try
    End Sub

    ' -- A56e: ⚠ TRAP 3 — a hole reaching back past segStartMs is clamped ------------------
    ' Deribit refuses trade windows past its ~24 h retention. An unclamped hole asks for a
    ' window the venue will not serve, gets nothing, and is re-detected on every pass forever —
    ' the pre-fix era holds 7,471 gap runs, so the cost of getting this wrong is not theoretical.
    ' ⚠ The clamp goes on EACH HOLE, not only on the pass's outer window.
    '
    ' Also pins the bracket read: the row BELOW segStartMs is what makes a straddling hole
    ' visible at all. Drop it and this window is silently never fetched.
    Private Sub A56e_HoleReachingPastSegStartIsClamped()
        Dim dir As String = A48TempStore("56e")
        Try
            A56Write(dir, New List(Of TradeRecord) From {
                A53Trade(A56Ms(0), 64000, 10, "buy", "none", "b-1", 5000L),
                A53Trade(A56Ms(600000), 64100, 10, "sell", "none", "b-2", 5900L),
                A53Trade(A56Ms(660000), 64101, 10, "sell", "none", "b-3", 5901L)})

            Dim segStart As Long = A56Ms(300000)      ' AFTER the bracketing row, INSIDE the hole
            Dim segEnd As Long = A56Ms(900000)
            Dim w = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(dir), segStart, segEnd, True)

            Dim clampedOk As Boolean = w.Count = 2 AndAlso
                                       w(0).StartMs = segStart AndAlso
                                       w(0).EndInclMs = A56Ms(600000) - 1
            Dim tailOk As Boolean = w.Count = 2 AndAlso w(1).StartMs = A56Ms(660000) + 1 AndAlso
                                    w(1).EndInclMs = segEnd
            ' Nothing returned may reach before segStart — that is the whole point of the clamp.
            Dim noneBeforeStart As Boolean = True
            For Each r In w
                If r.StartMs < segStart Then noneBeforeStart = False
            Next

            Check("A56e ⚠ retention clamp — a hole straddling segStart is clamped to it (never a refused window), the pre-segStart bracket row still makes it visible",
                  clampedOk AndAlso tailOk AndAlso noneBeforeStart,
                  String.Format("windows={0}(want 2) first=[{1},{2}] want=[{3},{4}] noneBeforeStart={5}",
                                w.Count, If(w.Count > 0, w(0).StartMs, -1), If(w.Count > 0, w(0).EndInclMs, -1),
                                segStart, A56Ms(600000) - 1, noneBeforeStart))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A56f: the MaxHolesPerPass cap keeps the LARGEST holes -----------------------------
    ' ⚠ THE ONE FIXTURE IN THIS FAMILY THAT ASSERTS SHIPPED BEHAVIOUR, so it READS
    ' TradeStoreWriter.MaxHolesPerPass and never restates 32 (fixture-literal provenance rule,
    ' and the 2026-08-11 Public-Const ruling that exists so a fixture can do exactly this).
    '
    ' Holes are built with strictly increasing missing-sequence counts, so which ones survive
    ' the cap is unambiguous: the N smallest must be gone and the survivors must come back in
    ' CHRONOLOGICAL order with the tail still last.
    Private Sub A56f_HoleCountIsCappedKeepingTheLargest()
        Dim dir As String = A48TempStore("56f")
        Try
            Dim overBy As Integer = 5
            Dim holeCount As Integer = TradeStoreWriter.MaxHolesPerPass + overBy
            Dim rows As New List(Of TradeRecord)()
            Dim seq As Long = 100000L
            rows.Add(A53Trade(A56Ms(0), 64000, 10, "buy", "none", "h-0", seq))
            For j As Integer = 1 To holeCount
                ' Hole j is missing exactly j sequences, and spans 10 s.
                seq += 1L + CLng(j)
                rows.Add(A53Trade(A56Ms(CLng(j) * 10000L), 64000 + j, 10, "buy", "none", "h-" & j, seq))
            Next
            A56Write(dir, rows)

            Dim segEnd As Long = A56Ms(CLng(holeCount + 10) * 10000L)
            Dim w = TradeStoreWriter.ResolveRepairWindowsMs(A56Path(dir), A56Ms(0), segEnd, True)

            Dim countOk As Boolean = w.Count = TradeStoreWriter.MaxHolesPerPass + 1   ' + the tail
            ' The smallest surviving hole is the one whose missing count is (holeCount - cap + 1).
            Dim lowestKept As Integer = holeCount - TradeStoreWriter.MaxHolesPerPass + 1
            Dim firstOk As Boolean = w.Count > 0 AndAlso
                                     w(0).StartMs = A56Ms(CLng(lowestKept - 1) * 10000L) + 1
            Dim tailOk As Boolean = w.Count > 0 AndAlso
                                    w(w.Count - 1).StartMs = A56Ms(CLng(holeCount) * 10000L) + 1 AndAlso
                                    w(w.Count - 1).EndInclMs = segEnd
            ' Chronological, despite the cap having ranked them by size.
            Dim orderedOk As Boolean = True
            For i As Integer = 1 To w.Count - 1
                If w(i).StartMs <= w(i - 1).StartMs Then orderedOk = False
            Next
            ' ⚠ DR-1 (docs/downtime-repair-followups-implementer-briefs.md §1): this fixture
            ' USED to also assert `w(0).WidthMs >= MinHoleMs` as a sanity check that the CAP,
            ' not the FLOOR, produced the count — MinHoleMs is gone, so there is no floor left
            ' to rule out. `countOk` above already asserts the count against
            ' TradeStoreWriter.MaxHolesPerPass directly, which is the property this stood in for.

            Check("A56f MaxHolesPerPass cap — " & holeCount & " holes capped at the production constant, the smallest dropped, survivors chronological, tail last",
                  countOk AndAlso firstOk AndAlso tailOk AndAlso orderedOk,
                  String.Format("windows={0}(want {1}) firstStart={2}(want {3}) ordered={4} tailOk={5}",
                                w.Count, TradeStoreWriter.MaxHolesPerPass + 1,
                                If(w.Count > 0, w(0).StartMs, -1),
                                A56Ms(CLng(lowestKept - 1) * 10000L) + 1, orderedOk, tailOk))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' -- A56g: DR-2 — a truncation cut is TIME-contiguous, not a FILE-position slice ----------
    ' docs/downtime-repair-followups-implementer-briefs.md §2. `ScanForRepair`'s truncation used
    ' to drop the oldest MaxScanRows\10 rows by FILE POSITION. On an out-of-order store — repair
    ' pages append after streaming, so file order and time order differ — that scatters holes
    ' across the retained set's interior, and the walk in ResolveRepairWindowsMs reports each one
    ' as a PHANTOM hole with a large missing-sequence count, which then wins the MaxHolesPerPass
    ' ranking and evicts every real hole.
    '
    ' Construction: 16 rows, fully sequence-contiguous (seq 0..15, zero real holes) when sorted
    ' by time, but written out of order — an "S" block (seq 0,2,4,...,14, ascending timestamps)
    ' written first, then an "R" block (seq 1,3,5,...,15, interleaved timestamps) appended after,
    ' exactly the streaming-then-repair shape A56c uses. Driven through the Friend overload with
    ' a cap of 10 so the truncation path fires without 500,000 rows.
    '
    ' ⚠ THIS IS THE MUTATION PROOF. Hand-traced against the pre-fix file-order RemoveRange: the
    ' same 16 rows produce FIVE phantom holes (seq boundaries 2, 4, 6, 8, 10 — the S-block rows
    ' the file-order cut discarded while every R-block row, always appended at the list's end,
    ' survived every cut). The fix retains seq 6..15 — perfectly contiguous, zero phantoms.
    Private Sub A56g_TruncationCutIsTimeContiguousNotFileOrder()
        Dim dir As String = A48TempStore("56g")
        Try
            Dim sRows As New List(Of TradeRecord)()
            For k As Integer = 0 To 7
                sRows.Add(A53Trade(A56Ms(CLng(k * 2) * 10000L), 64000 + k, 10, "buy", "none",
                                   "s-" & k, CLng(k * 2)))
            Next
            A56Write(dir, sRows)   ' streaming wrote these first

            Dim rRows As New List(Of TradeRecord)()
            For k As Integer = 0 To 7
                rRows.Add(A53Trade(A56Ms(CLng(k * 2 + 1) * 10000L), 64100 + k, 10, "sell", "none",
                                   "r-" & k, CLng(k * 2 + 1)))
            Next
            A56Write(dir, rRows)   ' a repair pass filled the gaps, appended LATER, timestamped EARLIER

            ' Confirm the file really IS out of order (A56c's own discipline) — otherwise a
            ' future refactor that starts writing sorted would turn this into a tautology.
            Dim allRows = TradeStoreWriter.ReadTradeFile(A56Path(dir))
            Dim genuinelyUnsorted As Boolean = False
            For i As Integer = 1 To allRows.Count - 1
                If allRows(i).Timestamp < allRows(i - 1).Timestamp Then genuinelyUnsorted = True
            Next

            Dim smallCap As Integer = 10
            Dim truncated As Boolean = False
            Dim scanned = TradeStoreWriter.ScanForRepair(A56Path(dir), A56Ms(0), smallCap, truncated)

            scanned.Sort(Function(a, b)
                             Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
                             If c <> 0 Then Return c
                             Return a.Seq.CompareTo(b.Seq)
                         End Function)

            Dim noPhantom As Boolean = True
            For i As Integer = 1 To scanned.Count - 1
                If scanned(i).Seq - scanned(i - 1).Seq <> 1L Then noPhantom = False
            Next

            Dim countOk As Boolean = scanned.Count = smallCap
            Dim rangeOk As Boolean = scanned.Count > 0 AndAlso
                                     scanned(0).Seq = 6L AndAlso
                                     scanned(scanned.Count - 1).Seq = 15L

            Check("A56g DR-2 ⚠ truncation cut is TIME-contiguous, not file-order — an out-of-order store's cap truncation produces ZERO phantom holes and reports truncated",
                  truncated AndAlso genuinelyUnsorted AndAlso countOk AndAlso rangeOk AndAlso noPhantom,
                  String.Format("truncated={0} unsorted={1} count={2}(want {3}) firstSeq={4}(want 6) lastSeq={5}(want 15) noPhantom={6}",
                                truncated, genuinelyUnsorted, scanned.Count, smallCap,
                                If(scanned.Count > 0, scanned(0).Seq, -1L),
                                If(scanned.Count > 0, scanned(scanned.Count - 1).Seq, -1L), noPhantom))
        Finally
            A48Cleanup(dir)
        End Try
    End Sub

    ' =======================================================================
    ' A57 — thin-trade-window skip gate (docs/thin-trade-window-skip-gate-proposal.md §6).
    ' ScoringEngine.MinTradesForScoring is the host-agnostic seam the live skip gate
    ' (UI/MainForm_Analysis.vb) and the exit guard (ExitGuardEvaluator.vb, D-4) both
    ' call — the UI event handler itself sits outside this harness's WinForms boundary
    ' (same class as the A16-A31 boundary notes), so these fixtures pin the derived-
    ' minimum function and its tweaker fence directly.
    ' =======================================================================

    ' -- A57a: derives to 50 at shipped defaults (Max(TFI 30, MicroCVD 50)); the reason
    ' string the live gate builds carries both numbers --------------------------------
    Private Sub A57a_DerivesToFiftyAtDefaultsAndReasonStringFormat()
        Dim cfg As New EngineSettings()
        Dim minTrades As Integer = ScoringEngine.MinTradesForScoring(cfg)
        Dim count As Integer = 10
        ' [F2 follow-up to 613cf1e] Assert against the REAL production function, not a copy
        ' of its format — the copy left A57a green even if ThinTradesSkipReason's own text
        ' changed, because the live call site never ran under the WinForms boundary.
        Dim reason As String = ScoringEngine.ThinTradesSkipReason(count, minTrades)
        Check("A57a MinTradesForScoring derives to 50 at shipped defaults (Max(TFI 30, MicroCVD 50)) + ScoringEngine.ThinTradesSkipReason carries both numbers",
              minTrades = 50 AndAlso reason = "recent trades thin (10<50)",
              String.Format("minTrades={0} reason='{1}'", minTrades, reason))
    End Sub

    ' -- A57c: ⚠ THE HARDCODE-TRAP CATCHER — a non-default MicroCVD window moves the
    ' derived minimum. A build that hardcoded "< 50" passes A57a and A57b and FAILS this
    ' one. Mutation-proved: reverting MinTradesForScoring to Return 50 makes this fail. --
    Private Sub A57c_NonDefaultMicroCvdWindowMovesTheDerivedMinimum()
        Dim cfg As New EngineSettings()
        cfg.Indicators.MicroCVD.WindowSize = 80        ' non-default; TFI stays 30
        Dim minTrades As Integer = ScoringEngine.MinTradesForScoring(cfg)
        Dim fires79 As Boolean = (79 < minTrades)
        Dim passes80 As Boolean = Not (80 < minTrades)
        Check("A57c ⚠ non-default MicroCVD.WindowSize=80 moves the derived minimum to 80 (fires at 79, passes at 80) — catches the hardcoded-50 trap",
              minTrades = 80 AndAlso fires79 AndAlso passes80,
              String.Format("minTrades={0} fires79={1} passes80={2}", minTrades, fires79, passes80))
    End Sub

    ' -- A57d: the override — 0 = derived (byte-identical), a positive value takes
    ' precedence, and HC28 rejects the override key while a sibling scoring.* key passes --
    Private Sub A57d_OverrideAndTweakerFence()
        Dim cfgZero As New EngineSettings()
        cfgZero.Scoring.MinTradesForScoringOverride = 0
        Dim derivedViaZero As Integer = ScoringEngine.MinTradesForScoring(cfgZero)

        Dim cfgOverride As New EngineSettings()
        cfgOverride.Scoring.MinTradesForScoringOverride = 100
        Dim viaOverride As Integer = ScoringEngine.MinTradesForScoring(cfgOverride)

        Dim s As String = "{""version"":67,""scoring"":{""verdict_med_pct"":0.53,""min_trades_for_scoring_override"":0}}"
        Dim rOverride = SettingsDiffApplier.Validate(OneDiff("scoring.min_trades_for_scoring_override", "0", "80"), s, 3)
        Dim rSibling = SettingsDiffApplier.Validate(OneDiff("scoring.verdict_med_pct", "0.53", "0.55"), s, 3)

        Check("A57d override: 0 == derived (50); positive value (100) takes precedence; HC28 rejects the override key while sibling scoring.verdict_med_pct still passes",
              derivedViaZero = 50 AndAlso viaOverride = 100 AndAlso
              Not rOverride.IsValid AndAlso rOverride.ErrorReason.Contains("HARD CONSTRAINT 28") AndAlso rSibling.IsValid,
              String.Format("derivedViaZero={0} viaOverride={1} overrideValid={2} overrideReason='{3}' siblingValid={4}",
                            derivedViaZero, viaOverride, rOverride.IsValid, rOverride.ErrorReason, rSibling.IsValid))
    End Sub

    ' -- A57e: ⚠ D-4 pinned — the exit guard goes SILENT on a thin, heavily-adverse
    ' buffer while holding an open position. §4 of the spec-back undersold this: the
    ' guard previously went silent only on an EMPTY buffer (effectively never); it now
    ' goes silent for up to ~36s after a seed failure while a position is open. The
    ' same 40-trade heavy-sell pattern is evaluated TWICE — once with the derived
    ' minimum bypassed (MinTradesForScoringOverride=1) to prove it WOULD read Exit /
    ' AdverseCount>=2 if evaluated, and once at the shipped default (50) to prove the
    ' guard instead returns Clear. Without the bypass comparison this fixture could not
    ' tell "correctly gated" from "coincidentally not adverse". -----------------------
    Private Sub A57e_ExitGuardClearOnThinAdverseBuffer()
        Dim state As New MarketState()
        Dim trades As New List(Of TradeRecord)
        For i As Integer = 1 To 40                     ' 40 < the shipped derived minimum (50)
            trades.Add(Trade("sell", 20000, i))
        Next
        state.SeedTrades(trades, DateTime.UtcNow)

        Dim cfgBypassed As New EngineSettings()
        cfgBypassed.Scoring.MinTradesForScoringOverride = 1
        Dim resBypassed = ExitGuardEvaluator.Evaluate(state, PositionState.InLong, 0, 0, cfgBypassed)

        Dim cfgDefault As New EngineSettings()
        Dim resDefault = ExitGuardEvaluator.Evaluate(state, PositionState.InLong, 0, 0, cfgDefault)

        Check("A57e ⚠ D-4: 40-trade heavy-sell buffer (< derived min 50) evaluates Clear at shipped defaults, despite the SAME buffer reading Exit/AdverseCount>=2 once the gate is bypassed",
              resBypassed.Kind = ExitGuardKind.[Exit] AndAlso resBypassed.AdverseCount >= 2 AndAlso
              resDefault.Kind = ExitGuardKind.Clear,
              String.Format("bypassed={0}/cnt{1} default={2}", resBypassed.Kind, resBypassed.AdverseCount, resDefault.Kind))
    End Sub

    ' =======================================================================
    ' A58 — auto-run on start (docs/collector-ops-tooling-proposal.md §1). A58a/b are
    ' host-agnostic settings-load fixtures; A58c joins the A50 overlay group further down
    ' (it is one of the fixtures that calls SettingsLoader.Initialise). The UI wiring itself
    ' (MainForm_Layout.vb's `If cfg.AutoRun.StartEngaged Then StartAutoRun()`) sits outside
    ' this harness's WinForms boundary, same class as every A16-A57 boundary note.
    ' ⚠ §1.3's safety property (auto-start cannot arm autotrade) is NOT fixture-tested and
    ' cannot be from here — OrderCheck.vbproj does not Compile Include any MainForm_*.vb file,
    ' so a reflection check over AutoRunSettings (as an earlier draft of A58b did) proves only
    ' that the SETTINGS side carries no arm field; it says nothing about chkArmAutotrade or
    ' _autotradeArmed, which is where the property actually lives. That earlier draft read as
    ' coverage it did not provide (review finding, docs/collector-ops-tooling-spec-back.md
    ' FIX 3) and has been removed. The property is verified by READING
    ' UI/MainForm_Layout.vb (chkArmAutotrade's construction takes no settings-backed initial
    ' state) and UI/MainForm_SignalBridge.vb (_autotradeArmed is assigned only from the
    ' checkbox's own Checked property) — recorded there, not asserted here.
    ' =======================================================================

    ' -- A58a: a pre-v68 settings.json with no start_engaged key deserialises to False -------
    Private Sub A58a_AbsentKeyDefaultsFalseOnOldSettingsFile()
        Dim json As String = "{""version"":67,""auto_run"":{""interval_minutes"":1,""interval_seconds"":0,""trigger_mode"":""on_close""}}"
        Dim cfg = JsonSerializer.Deserialize(Of EngineSettings)(
            json, New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
        Check("A58a a pre-v68 settings.json with no start_engaged key deserialises AutoRun.StartEngaged to False — byte-identical on every box that has not been re-deployed",
              Not cfg.AutoRun.StartEngaged,
              String.Format("startEngaged={0}", cfg.AutoRun.StartEngaged))
    End Sub

    ' -- A58b: start_engaged:true round-trips True through JSON deserialisation. This is a
    ' JSON-contract check ONLY — it does not touch and cannot prove the §1.3 safety property;
    ' see the group comment above.
    Private Sub A58b_StartEngagedRoundTripsTrueThroughJson()
        Dim json As String = "{""version"":67,""auto_run"":{""start_engaged"":true,""interval_minutes"":2,""interval_seconds"":30,""trigger_mode"":""interval""}}"
        Dim cfg = JsonSerializer.Deserialize(Of EngineSettings)(
            json, New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})

        Check("A58b start_engaged:true round-trips True through JSON deserialisation (JSON-contract check only — see the A58 group comment for why §1.3's safety property is not, and cannot be, fixture-tested here)",
              cfg.AutoRun.StartEngaged AndAlso cfg.AutoRun.IntervalMinutes = 2,
              String.Format("startEngaged={0} intervalMinutes={1}", cfg.AutoRun.StartEngaged, cfg.AutoRun.IntervalMinutes))
    End Sub

    Private Sub A52a_AsiaArmingJsonContract()
        Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
        Dim armed   = JsonSerializer.Deserialize(Of EngineSettings)(
                          A52Json("{""burst_ratio_threshold"":5.5}"), opts)
        Dim unarmed = JsonSerializer.Deserialize(Of EngineSettings)(A52Json("{}"), opts)

        Const ASIA_HOUR As Integer = 3      ' ASIA bucket is UTC 0–7
        Dim armedHas   As Boolean = ExecutionResolution.HasExplicitAggrVelBurstThreshold(armed, ASIA_HOUR)
        Dim armedThr   As Double = ExecutionResolution.ResolveAggrVelBurstThreshold(armed, ASIA_HOUR)
        Dim unarmedHas As Boolean = ExecutionResolution.HasExplicitAggrVelBurstThreshold(unarmed, ASIA_HOUR)
        Dim unarmedThr As Double = ExecutionResolution.ResolveAggrVelBurstThreshold(unarmed, ASIA_HOUR)

        Dim poco As New EngineSettings()
        Dim pocoHas As Boolean = ExecutionResolution.HasExplicitAggrVelBurstThreshold(poco, ASIA_HOUR)
        Dim pocoThr As Double = ExecutionResolution.ResolveAggrVelBurstThreshold(poco, ASIA_HOUR)

        Check("A52a ASIA arming JSON contract (present ⇒ armed @5.5 · absent ⇒ inert @2.5 · shipped POCO mirrors shipped JSON)",
              armedHas AndAlso Math.Abs(armedThr - 5.5) < 0.000001 AndAlso
              Not unarmedHas AndAlso Math.Abs(unarmedThr - 2.5) < 0.000001 AndAlso
              pocoHas AndAlso Math.Abs(pocoThr - 5.5) < 0.000001,
              String.Format(CultureInfo.InvariantCulture,
                            "armed={0}/{1} unarmed={2}/{3} poco={4}/{5} (want True/5.5 False/2.5 True/5.5)",
                            armedHas, armedThr, unarmedHas, unarmedThr, pocoHas, pocoThr))
    End Sub

    ' Unix-ms for a 2026-07-15 UTC wall time — the A45a historical session fixture date.
    Private Function HistMs(hour As Integer, minute As Integer) As Long
        Return New DateTimeOffset(2026, 7, 15, hour, minute, 0, TimeSpan.Zero).ToUnixTimeMilliseconds()
    End Function

    ' A46a: the pooled-file report path. Writes a small v0.8-shaped CSV to a temp file,
    ' then runs the SHIPPED chain the `report` verb runs:
    '
    '   ForwardWindowJoiner.Load  →  PopulateForwardBars  →  FailureRateMatrix.Compute
    '                             →  BandLadder.Compute   →  MarkdownReportWriter
    '
    ' Asserts (a) the CSV genuinely round-trips (row count + placed-schema detection via
    ' HasPlaced, which is what routes the barriers), (b) the rendered document carries
    ' `## 2. Success-Rate Matrix` with a real STRONG_LONG cell, and (c) it carries
    ' `## 9. Band ladder` with all three bands at the fixture's own success rates.
    '
    ' Six rows, 14:00–15:40 UTC on 2026-07-28 (NY hours ⇒ population NY|1, ExecResolution 1):
    ' two per band, one hitting the placed target and one hitting the placed stop. So every
    ' band reads exactly 50 % success, and STRONG_LONG / MEDIUM_LONG each carry n=2 in the
    ' matrix while WEAK appears ONLY in the ladder (the matrix has no WEAK tier — A35c).
    Private Sub A46a_PooledCsvReportSections()
        Const entryPx As Double = 100000.0
        Const atr     As Double = 40.0
        Const tgtLong As Double = 100200.0    ' dist 200 > floor 80 (0.0008 × 100000)
        Const stpLong As Double = 99900.0

        ' The bands in CSV/wire form — bare LONG is the MEDIUM band (v55 stored-form pin).
        Dim verdicts As String() = {"STRONG LONG", "STRONG LONG", "LONG", "LONG", "WEAK LONG", "WEAK LONG"}
        Dim wins     As Boolean() = {True, False, True, False, True, False}

        Dim csvPath As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                             "a46a_pooled_" & Guid.NewGuid().ToString("N") & ".csv")
        Dim ohlc As New Dictionary(Of DateTime, OhlcBar)()
        Dim rowTimes As New List(Of DateTime)()

        Try
            Dim sb As New System.Text.StringBuilder()
            ' Header-name indexed loader ⇒ a minimal column set is legitimate input.
            ' All four Placed* columns must be present together or HasPlaced stays False.
            sb.AppendLine("Timestamp,Price,Verdict,ATR,Regime,VerdictContext,ExecResolution," &
                          "PlacedTargetLong,PlacedStopLong,PlacedTargetShort,PlacedStopShort")
            For k = 0 To verdicts.Length - 1
                Dim ts As DateTime = New DateTime(2026, 7, 28, 14, 0, 0, DateTimeKind.Utc).AddMinutes(k * 20)
                rowTimes.Add(ts)
                sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
                    "{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},TRENDING_UP,CONFIRMED,1,{4},{5},{6},{7}",
                    ts, entryPx, verdicts(k), atr, tgtLong, stpLong, 99800.0, 100100.0))

                ' Forward bars at +3/+5/+10/+15 (the eligible T+3..T+W band for res-1).
                ' A winning row's +5 bar wicks through the placed target without touching
                ' the stop; a losing row's +5 bar does the mirror.
                For Each off In {3, 5, 10, 15}
                    Dim closeTime As DateTime = ts.AddMinutes(off)
                    Dim hi As Double = entryPx + 50
                    Dim lo As Double = entryPx - 50
                    If off = 5 Then
                        If wins(k) Then
                            hi = tgtLong + 50 : lo = entryPx - 50      ' target first
                        Else
                            hi = entryPx + 50 : lo = stpLong - 50      ' stop first
                        End If
                    End If
                    ohlc(closeTime) = New OhlcBar With {.CloseTime = closeTime, .Open = entryPx,
                                                        .High = hi, .Low = lo, .Close = entryPx}
                Next
            Next
            File.WriteAllText(csvPath, sb.ToString())

            ' -- the shipped chain --
            Dim cfg As New EngineSettings()
            Dim rows As List(Of CsvRow) = ForwardWindowJoiner.Load(csvPath)
            Dim okLoad As Boolean = (rows.Count = verdicts.Length) AndAlso
                                    rows.All(Function(x) x.HasPlaced) AndAlso
                                    rows.All(Function(x) x.ExecResolution = 1) AndAlso
                                    (rows(0).Timestamp = rowTimes(0))

            ForwardWindowJoiner.PopulateForwardBars(rows, ohlc)
            Dim okBars As Boolean = rows.All(Function(x) x.ForwardBars.ContainsKey(15) AndAlso
                                                         x.ForwardBars(15).Count > 0)

            Dim atrInv As Integer, structStop As Integer, atrFb As Integer
            Dim placedTgt As Integer, legacyFav As Integer, belowMin As Integer
            Dim cells = FailureRateMatrix.Compute(rows, atrInv, structStop, atrFb,
                                                  placedTgt, legacyFav, belowMin,
                                                  cfg.Scoring.TradeCosts.EffectiveMinMovePct,
                                                  cfg.Scoring.AtrTargetMultiplier, 1)
            Dim ladder = BandLadder.Compute(rows, cfg)

            Dim rep As New AnalysisReport() With {.TotalRows = rows.Count}
            rep.Populations.Add(New PopulationReport With {
                .PopulationKey = "NY|1", .SessionName = "NY", .Resolution = 1,
                .BarrierLabel = "PLACED", .RowCount = rows.Count,
                .FailureCells = cells, .BandLadder = ladder})
            rep.PooledBandLadder = ladder

            Dim md As String = MarkdownReportWriter.BuildFullMarkdownForHarness(rep)

            ' §2 — heading, the STRONG_LONG sub-table, and a real cell (n=2, 50 %).
            Dim okMatrixHead As Boolean = md.Contains("## 2. Success-Rate Matrix") AndAlso
                                          md.Contains("### STRONG_LONG") AndAlso
                                          md.Contains("| Window | Placed geometry |")
            Dim okMatrixCell As Boolean = md.Contains("n=2")

            ' §9 — heading and all three bands. Two rows per band, one win each ⇒ 50 %.
            Dim okLadderHead As Boolean = md.Contains("## 9. Band ladder")
            Dim okLadderRows As Boolean = ladder.Count = 3 AndAlso
                                          ladder.All(Function(b) b.SampleSize = 2) AndAlso
                                          ladder.All(Function(b) Math.Abs(b.FailureRate - 0.5) < 1e-9) AndAlso
                                          md.Contains("| STRONG |") AndAlso
                                          md.Contains("| MEDIUM |") AndAlso
                                          md.Contains("| WEAK   |")

            ' WEAK is ladder-only: the matrix tier list has no WEAK tier (A35c pin).
            Dim okWeakNotInMatrix As Boolean = Not cells.Any(Function(c) c.VerdictTier.Contains("WEAK"))

            Check("A46a pooled CSV → report carries §2 matrix + §9 band ladder (real Load/matrix/ladder/writer chain)",
                  okLoad AndAlso okBars AndAlso okMatrixHead AndAlso okMatrixCell AndAlso
                  okLadderHead AndAlso okLadderRows AndAlso okWeakNotInMatrix,
                  String.Format("load={0}(n={1}) bars={2} matrixHead={3} matrixCell={4} ladderHead={5} ladderRows={6}(n={7}) weakOutOfMatrix={8}",
                                okLoad, rows.Count, okBars, okMatrixHead, okMatrixCell,
                                okLadderHead, okLadderRows, ladder.Count, okWeakNotInMatrix))
        Finally
            Try : File.Delete(csvPath) : Catch : End Try
        End Try
    End Sub

    ' ═══════════════════════════════════════════════════════════════════════════════════
    ' A50 — settings.local.json per-box overlay (Core/Settings/SettingsLoader.vb)
    '
    ' Two boxes, one binary, one tracked settings.json. AWS captures the raw tape; the
    ' local box does not. The overlay is how that divergence gets expressed without a
    ' hand-edit that every PreserveNewest build silently undoes.
    '
    ' THE REGRESSION TRAP IS A50c: Save() must write the BASE document, never the merge.
    ' Inverting it promotes a local-only override into the shared tracked file and from
    ' there onto AWS on the next xcopy — for trade_store.enabled that is permanent, silent
    ' tape loss. A50j is its sibling: the whitelist ∩ UI-writeback interaction, which A50c
    ' does not cover (re-audit F4).
    ' ═══════════════════════════════════════════════════════════════════════════════════

    Private Function A50TempDir(tag As String) As String
        Dim dir As String = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                 "ordercheck_a50_" & tag & "_" & Guid.NewGuid().ToString("N").Substring(0, 8))
        Directory.CreateDirectory(dir)
        Return dir
    End Function

    Private Sub A50Cleanup(dir As String)
        Try : Directory.Delete(dir, True) : Catch : End Try
    End Sub

    ''' <summary>
    ''' A full settings tree, off the shipped POCO with a handful of non-default values so
    ''' the round-trip assertions are not trivially satisfied by defaults.
    ''' </summary>
    Private Function A50BaseSettings() As EngineSettings
        Dim s As New EngineSettings()
        s.Version = 64
        s.LastModified = "2026-08-01T00:00:00Z"
        s.ModifiedBy = "A50 fixture base"
        s.ChangeLog.Add("v64 [2026-08-01T00:00:00Z]: fixture seed")
        s.TradeStore.Enabled = True
        s.TradeStore.StoreDir = "backtest_data"
        s.TradeStore.FlushSeconds = 30
        s.LiveStrip.Enabled = True
        s.LiveStrip.RefreshSec = 2
        s.AnalysisLogging.OutputDumpMaxRuns = 3000
        s.Network.Transport = "ws"
        s.Network.RequestTimeoutSeconds = 15
        s.AutoRun.TriggerMode = "on_close"
        s.AutoRun.IntervalSeconds = 0
        s.MTFGate.Enabled = True
        s.Alerts.Enabled = True
        s.Scoring.BbwSqueezePenalty = 2
        s.Scoring.RegimeMaxScore.Trending = 19
        s.Indicators.RSI.Pass2cMidline = 50.0
        Return s
    End Function

    Private Function A50Json(s As EngineSettings) As String
        Return JsonSerializer.Serialize(s, New JsonSerializerOptions With {.WriteIndented = True})
    End Function

    Private Function A50Read(path As String) As EngineSettings
        Return JsonSerializer.Deserialize(Of EngineSettings)(
            File.ReadAllText(path), New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True})
    End Function

    ''' <summary>Write base (+ optional overlay) into a fresh dir and Initialise the loader on it.</summary>
    Private Function A50Init(dir As String, baseJson As String, overlayJson As String) As String
        Dim basePath As String = System.IO.Path.Combine(dir, "settings.json")
        File.WriteAllText(basePath, baseJson)
        If overlayJson IsNot Nothing Then
            File.WriteAllText(System.IO.Path.Combine(dir, SettingsLoader.LocalOverlayFileName), overlayJson)
        End If
        SettingsLoader.Initialise(basePath)
        Return basePath
    End Function

    ' -- A50a: absent overlay ⇒ byte-identical to the pre-overlay engine -------------------
    ' The merge branch must be skipped entirely, not merely produce the same answer. Pinned
    ' two ways: the loaded tree re-serialises to the base text exactly, AND it matches a
    ' plain JsonSerializer.Deserialize of the same text — which is literally what the loader
    ' did before this build.
    Private Sub A50a_AbsentOverlayIsByteIdentical()
        Dim dir As String = A50TempDir("a")
        Try
            Dim baseJson As String = A50Json(A50BaseSettings())
            A50Init(dir, baseJson, Nothing)

            Dim loadedJson As String = A50Json(SettingsLoader.Current)
            Dim plain As String = A50Json(JsonSerializer.Deserialize(Of EngineSettings)(
                baseJson, New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}))

            Check("A50a absent overlay — loaded tree byte-identical to the base text and to a plain deserialise; overlay inactive",
                  loadedJson = baseJson AndAlso loadedJson = plain AndAlso
                  Not SettingsLoader.OverlayActive AndAlso SettingsLoader.OverlayAppliedKeys.Count = 0,
                  String.Format("roundTrip={0} vsPlain={1} active={2} applied={3}",
                                loadedJson = baseJson, loadedJson = plain,
                                SettingsLoader.OverlayActive, SettingsLoader.OverlayAppliedKeys.Count))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50b: deep per-key merge, not block replacement ------------------------------------
    ' {"trade_store":{"enabled":false}} must flip exactly that key. The control is the base
    ' tree with the same single field changed in code — equal serialisations prove nothing
    ' else moved, in trade_store or anywhere else.
    Private Sub A50b_DeepMergeFlipsOneKeyOnly()
        Dim dir As String = A50TempDir("b")
        Try
            Dim baseCfg = A50BaseSettings()
            A50Init(dir, A50Json(baseCfg), "{""trade_store"":{""enabled"":false}}")

            Dim expected = A50BaseSettings()
            expected.TradeStore.Enabled = False

            Dim mergedJson As String = A50Json(SettingsLoader.Current)
            Dim appliedOk As Boolean = SettingsLoader.OverlayAppliedKeys.Count = 1 AndAlso
                                       SettingsLoader.OverlayAppliedKeys(0) = "trade_store.enabled"

            Check("A50b deep merge — trade_store.enabled flips, every sibling and every other block survives; +local marker on",
                  mergedJson = A50Json(expected) AndAlso
                  SettingsLoader.Current.TradeStore.StoreDir = "backtest_data" AndAlso
                  SettingsLoader.Current.TradeStore.FlushSeconds = 30 AndAlso
                  SettingsLoader.OverlayActive AndAlso appliedOk,
                  String.Format("treeEqual={0} storeDir='{1}' flush={2} active={3} applied=[{4}]",
                                mergedJson = A50Json(expected), SettingsLoader.Current.TradeStore.StoreDir,
                                SettingsLoader.Current.TradeStore.FlushSeconds, SettingsLoader.OverlayActive,
                                String.Join(",", SettingsLoader.OverlayAppliedKeys)))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50c: Save writes the BASE, not the merge — THE REGRESSION TRAP -------------------
    ' An unrelated UI edit (the auto_run interval path) is saved while trade_store.enabled is
    ' overlaid to false. The tracked file must come back with enabled STILL TRUE — the AWS
    ' value — and the edit preserved. Inverting this is how a local "don't capture" silently
    ' becomes AWS's.
    Private Sub A50c_SaveWritesTheBaseNotTheMerge()
        Dim dir As String = A50TempDir("c")
        Try
            Dim basePath As String = A50Init(dir, A50Json(A50BaseSettings()),
                                             "{""trade_store"":{""enabled"":false}}")

            Dim cfg = SettingsLoader.Current
            Dim mergedBeforeSave As Boolean = Not cfg.TradeStore.Enabled       ' overlay in force
            cfg.AutoRun.IntervalSeconds = 77                                    ' the unrelated edit
            SettingsLoader.Save(cfg, "A50c unrelated operational edit", bumpVersion:=False)

            Dim onDisk = A50Read(basePath)

            Check("A50c Save writes the BASE — tracked trade_store.enabled stays TRUE, the unrelated edit persists, the overlay keeps winning in memory",
                  mergedBeforeSave AndAlso
                  onDisk.TradeStore.Enabled AndAlso onDisk.AutoRun.IntervalSeconds = 77 AndAlso
                  onDisk.Version = 64 AndAlso
                  Not SettingsLoader.Current.TradeStore.Enabled AndAlso
                  SettingsLoader.Current.AutoRun.IntervalSeconds = 77,
                  String.Format("mergedBefore={0} diskEnabled={1} diskInterval={2} diskVersion={3} curEnabled={4} curInterval={5}",
                                mergedBeforeSave, onDisk.TradeStore.Enabled, onDisk.AutoRun.IntervalSeconds,
                                onDisk.Version, SettingsLoader.Current.TradeStore.Enabled,
                                SettingsLoader.Current.AutoRun.IntervalSeconds))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50d: the whitelist is an ALLOW-LIST — five rejects, base values survive ----------
    ' scoring.* / indicators.* / version were the spec's original three. mtf_gate.* and
    ' alerts.* are the re-audit's additions and the load-bearing ones: mtf_gate is the HARD
    ' VETO and was named nowhere in the spec's 16-of-17 enumeration; alerts gates
    ' liq_events.log, the sole A4 gate instrument. A reject-list implementation passes the
    ' first three and ships the other two.
    Private Sub A50d_WhitelistRejectsScoringIndicatorsVersionMtfGateAlerts()
        Dim dir As String = A50TempDir("d")
        Try
            Dim overlay As String =
                "{""version"":999," &
                """scoring"":{""bbw_squeeze_penalty"":99}," &
                """indicators"":{""RSI"":{""pass2c_midline"":99.0}}," &
                """mtf_gate"":{""enabled"":false}," &
                """alerts"":{""enabled"":false}," &
                """trade_store"":{""enabled"":false}}"
            A50Init(dir, A50Json(A50BaseSettings()), overlay)

            Dim c = SettingsLoader.Current
            Dim rej = SettingsLoader.OverlayRejectedKeys
            Dim wanted As String() = {"version", "scoring.bbw_squeeze_penalty",
                                      "indicators.RSI.pass2c_midline", "mtf_gate.enabled", "alerts.enabled"}
            Dim allRejected As Boolean = True
            For Each w In wanted
                If Not rej.Contains(w) Then allRejected = False
            Next

            Check("A50d whitelist rejects version / scoring.* / indicators.* / mtf_gate.* / alerts.* with base values intact; the admitted key still applies and startup succeeds",
                  allRejected AndAlso rej.Count = wanted.Length AndAlso
                  c.Version = 64 AndAlso c.Scoring.BbwSqueezePenalty = 2 AndAlso
                  Math.Abs(c.Indicators.RSI.Pass2cMidline - 50.0) < 1e-9 AndAlso
                  c.MTFGate.Enabled AndAlso c.Alerts.Enabled AndAlso
                  Not c.TradeStore.Enabled AndAlso SettingsLoader.OverlayActive,
                  String.Format("rejected=[{0}] version={1} bbw={2} midline={3} mtf={4} alerts={5} tradeStore={6}",
                                String.Join(",", rej), c.Version, c.Scoring.BbwSqueezePenalty,
                                c.Indicators.RSI.Pass2cMidline, c.MTFGate.Enabled, c.Alerts.Enabled,
                                c.TradeStore.Enabled))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50e: malformed overlay ⇒ logged, ignored, app starts ------------------------------
    ' D3 chose ignore-and-log over fatal: a box that will not boot loses more data than a box
    ' running shared settings, and on AWS "will not boot" means silent tape loss.
    Private Sub A50e_MalformedOverlayIsIgnored()
        Dim dir As String = A50TempDir("e")
        Try
            Dim baseJson As String = A50Json(A50BaseSettings())
            A50Init(dir, baseJson, "{ this is not json ")

            Check("A50e malformed overlay — ignored, base settings load intact, overlay inactive, no throw",
                  A50Json(SettingsLoader.Current) = baseJson AndAlso
                  Not SettingsLoader.OverlayActive AndAlso
                  SettingsLoader.Current.TradeStore.Enabled,
                  String.Format("treeEqual={0} active={1} tradeStore={2}",
                                A50Json(SettingsLoader.Current) = baseJson,
                                SettingsLoader.OverlayActive, SettingsLoader.Current.TradeStore.Enabled))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50f: hot reload — the overlay appearing and disappearing both take effect --------
    ' §1.1. Editing the overlay must not require a restart; DELETING it must revert, which is
    ' the direction that matters (dotnet clean removes bin/, and losing the overlay silently
    ' switches local capture back ON). Drives the REAL FileSystemWatcher, so it polls.
    Private Sub A50f_HotReloadReMergesAndDeleteReverts()
        Dim dir As String = A50TempDir("f")
        Try
            A50Init(dir, A50Json(A50BaseSettings()), Nothing)
            Dim startsOff As Boolean = Not SettingsLoader.OverlayActive AndAlso SettingsLoader.Current.TradeStore.Enabled

            Dim overlayPath As String = System.IO.Path.Combine(dir, SettingsLoader.LocalOverlayFileName)
            File.WriteAllText(overlayPath, "{""trade_store"":{""enabled"":false}}")
            Dim mergedOnCreate As Boolean = A50WaitFor(Function() SettingsLoader.OverlayActive AndAlso
                                                                  Not SettingsLoader.Current.TradeStore.Enabled)

            File.Delete(overlayPath)
            Dim revertedOnDelete As Boolean = A50WaitFor(Function() Not SettingsLoader.OverlayActive AndAlso
                                                                     SettingsLoader.Current.TradeStore.Enabled)

            Check("A50f hot reload — dropping the overlay in re-merges without a restart; deleting it reverts to base",
                  startsOff AndAlso mergedOnCreate AndAlso revertedOnDelete,
                  String.Format("startsOff={0} mergedOnCreate={1} revertedOnDelete={2}",
                                startsOff, mergedOnCreate, revertedOnDelete))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ''' <summary>Poll a condition for up to ~10 s — the watcher handler sleeps 200 ms before reloading.</summary>
    Private Function A50WaitFor(cond As Func(Of Boolean)) As Boolean
        For i As Integer = 1 To 100
            If cond() Then Return True
            Thread.Sleep(100)
        Next
        Return cond()
    End Function

    ' -- A50g: arrays are leaves — replaced wholesale, never element-merged -----------------
    ' No admitted block carries an array today, so the pin is on the WALK rather than on a
    ' POCO field: an array counts as exactly ONE path on both sides of the whitelist. If the
    ' merge ever descended into arrays these would come back as per-element paths, which is
    ' the partial-element surprise §1 rules out.
    '
    ' The probe array is absent from the base, so this fixture also trips the F1 warning —
    ' deliberate and harmless. trade_store.enabled IS in the base, so the overlay still
    ' activates; A50k is where the absent-key rule itself is pinned.
    Private Sub A50g_ArraysReplaceWholesale()
        Dim dir As String = A50TempDir("g")
        Try
            Dim baseCfg = A50BaseSettings()
            A50Init(dir, A50Json(baseCfg),
                    "{""trade_store"":{""enabled"":false,""probe_list"":[9,9]},""change_log"":[""nope""]}")

            Dim applied = SettingsLoader.OverlayAppliedKeys
            Dim rej = SettingsLoader.OverlayRejectedKeys
            Dim appliedOk As Boolean = applied.Count = 2 AndAlso
                                       applied.Contains("trade_store.enabled") AndAlso
                                       applied.Contains("trade_store.probe_list")
            Dim rejOk As Boolean = rej.Count = 1 AndAlso rej(0) = "change_log"
            Dim c = SettingsLoader.Current

            Check("A50g arrays are single leaves — one applied path for an admitted array, one rejected path for change_log; siblings and the base array survive",
                  appliedOk AndAlso rejOk AndAlso Not c.TradeStore.Enabled AndAlso
                  c.TradeStore.StoreDir = "backtest_data" AndAlso
                  c.ChangeLog.Count = 1 AndAlso c.ChangeLog(0).Contains("fixture seed"),
                  String.Format("applied=[{0}] rejected=[{1}] tradeStore={2} storeDir='{3}' changeLog={4}",
                                String.Join(",", applied), String.Join(",", rej),
                                c.TradeStore.Enabled, c.TradeStore.StoreDir, c.ChangeLog.Count))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50h: the scoring-surface pin, through the REAL Calculate() ------------------------
    ' A50d proves the key is ignored at parse. The header claims "Scoring impact: NONE by
    ' construction", and that claim deserves a pin at the surface it is about (the A42a/A36a
    ' pattern). The third arm is what makes the first two mean anything: the SAME overlay
    ' values, applied directly to the POCO, DO move the verdict — so equality is a property
    ' of the whitelist, not of values that were never potent.
    Private Sub A50h_ScoringSurfacePinThroughRealCalculate()
        Dim dirA As String = A50TempDir("h1")
        Dim dirB As String = A50TempDir("h2")
        Try
            Dim baseJson As String = A50Json(A50BaseSettings())
            Dim jopt As New JsonSerializerOptions With {.WriteIndented = True}

            A50Init(dirA, baseJson, Nothing)
            Dim vBase = ScoringEngine.Calculate(BuildA8Indicators(), PositionState.None,
                                                BuildA8Norms(), SettingsLoader.Current)
            Dim jsonBase As String = JsonSerializer.Serialize(vBase, jopt)

            Dim overlay As String =
                "{""scoring"":{""regime_max_score"":{""trending"":5,""range_bound"":5,""transitional"":5}," &
                """verdict_strong_pct"":0.01}," &
                """indicators"":{""RSI"":{""pass2c_midline"":99.0}}}"
            A50Init(dirB, baseJson, overlay)
            Dim vOverlaid = ScoringEngine.Calculate(BuildA8Indicators(), PositionState.None,
                                                    BuildA8Norms(), SettingsLoader.Current)
            Dim jsonOverlaid As String = JsonSerializer.Serialize(vOverlaid, jopt)

            ' Potency arm — the same values applied to the POCO directly.
            Dim potent = A50BaseSettings()
            potent.Scoring.RegimeMaxScore.Trending = 5
            potent.Scoring.RegimeMaxScore.RangeBound = 5
            potent.Scoring.RegimeMaxScore.Transitional = 5
            potent.Scoring.VerdictStrongPct = 0.01
            Dim vPotent = ScoringEngine.Calculate(BuildA8Indicators(), PositionState.None,
                                                  BuildA8Norms(), potent)
            Dim potencyOk As Boolean = JsonSerializer.Serialize(vPotent, jopt) <> jsonBase

            Check("A50h scoring-surface pin — a scoring.*/indicators.* overlay leaves the verdict byte-identical through the real Calculate(), and the same values DO move it when applied directly",
                  jsonBase = jsonOverlaid AndAlso potencyOk,
                  String.Format("identical={0} potent={1} baseMax={2} overlaidMax={3} potentMax={4}",
                                jsonBase = jsonOverlaid, potencyOk, vBase.MaxScore,
                                vOverlaid.MaxScore, vPotent.MaxScore))
        Finally
            A50Cleanup(dirA)
            A50Cleanup(dirB)
        End Try
    End Sub

    ' -- A50i: network. is split PER KEY, not per block -------------------------------------
    ' A block-granular reading silently undoes §2.2. transport selects the data source (three
    ' run-path signals gate on `src Is _wsSource`), and auto_run cadence moves scoring — both
    ' must fail while a sibling in the same block passes.
    Private Sub A50i_NetworkSplitIsKeyGranular()
        Dim dir As String = A50TempDir("i")
        Try
            A50Init(dir, A50Json(A50BaseSettings()),
                    "{""network"":{""transport"":""rest"",""request_timeout_seconds"":45}," &
                    """auto_run"":{""trigger_mode"":""interval""}}")

            Dim c = SettingsLoader.Current
            Dim rej = SettingsLoader.OverlayRejectedKeys
            Dim applied = SettingsLoader.OverlayAppliedKeys

            Check("A50i network split is key-granular — request_timeout_seconds admitted, transport and auto_run.trigger_mode rejected, base values survive",
                  c.Network.RequestTimeoutSeconds = 45 AndAlso c.Network.Transport = "ws" AndAlso
                  c.AutoRun.TriggerMode = "on_close" AndAlso
                  applied.Count = 1 AndAlso applied(0) = "network.request_timeout_seconds" AndAlso
                  rej.Count = 2 AndAlso rej.Contains("network.transport") AndAlso
                  rej.Contains("auto_run.trigger_mode"),
                  String.Format("timeout={0} transport='{1}' trigger='{2}' applied=[{3}] rejected=[{4}]",
                                c.Network.RequestTimeoutSeconds, c.Network.Transport, c.AutoRun.TriggerMode,
                                String.Join(",", applied), String.Join(",", rej)))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50j: whitelist ∩ UI-writeback (re-audit F4) ---------------------------------------
    ' Three admitted blocks are live-UI-writable — live_strip.enabled, performance_display.
    ' metric_mode, analysis_logging.output_dump_*. On an overlaid box these become one-way
    ' mirrors: the click writes the shared tracked file (the AWS xcopy source) while the
    ' overlay keeps winning locally, so the checkbox visibly snaps back within ~2 s.
    '
    ' That is the ACCEPTED behaviour; what must never happen is the OVERLAY's own value
    ' reaching the tracked file. Arm 1 is the click; arm 2 is the unrelated save, which is
    ' where a naive implementation quietly promotes the override.
    Private Sub A50j_WhitelistIntersectUiWriteback()
        Dim dir As String = A50TempDir("j")
        Try
            Dim basePath As String = A50Init(dir, A50Json(A50BaseSettings()),
                                             "{""live_strip"":{""enabled"":false}}")

            ' Arm 1 — the TAPE checkbox click on an overlaid key (MainForm_LiveStrip.vb:341-343).
            Dim cfg = SettingsLoader.Current
            Dim overlayWinsBefore As Boolean = Not cfg.LiveStrip.Enabled
            cfg.LiveStrip.Enabled = True
            SettingsLoader.Save(cfg, "live_strip enabled toggled via UI", bumpVersion:=False)
            Dim afterClick = A50Read(basePath)
            Dim clickWroteBase As Boolean = afterClick.LiveStrip.Enabled          ' the click reached the base
            Dim snapsBack As Boolean = Not SettingsLoader.Current.LiveStrip.Enabled ' overlay still wins

            ' Arm 2 — an UNRELATED save must not promote the overlay's false into the base.
            Dim cfg2 = SettingsLoader.Current
            cfg2.AnalysisLogging.OutputDumpMaxRuns = 1234
            SettingsLoader.Save(cfg2, "A50j unrelated save", bumpVersion:=False)
            Dim afterUnrelated = A50Read(basePath)
            Dim noPromotion As Boolean = afterUnrelated.LiveStrip.Enabled
            Dim editKept As Boolean = afterUnrelated.AnalysisLogging.OutputDumpMaxRuns = 1234

            Check("A50j whitelist ∩ UI-writeback — a click on an overlaid key writes the BASE and snaps back; an unrelated save never promotes the overlay value",
                  overlayWinsBefore AndAlso clickWroteBase AndAlso snapsBack AndAlso
                  noPromotion AndAlso editKept AndAlso
                  Not SettingsLoader.Current.LiveStrip.Enabled,
                  String.Format("before={0} clickWroteBase={1} snapsBack={2} noPromotion={3} editKept={4}",
                                overlayWinsBefore, clickWroteBase, snapsBack, noPromotion, editKept))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

    ' -- A50k: an admitted-but-absent key is warned and does not activate the overlay -------
    ' [F1, review 2026-08-02] IsAdmitted is a path-prefix match with no POCO validation, so
    ' {"trade_store":{"enabledd":false}} is ADMITTED and merged — and changes nothing,
    ' because no POCO field matches. Before the fix it counted as an override, rendered
    ' "+local" and logged success while local capture kept running: the exact F6 failure this
    ' feature exists to prevent, on the one key it was built for. Same family as D-D — case
    ' variants failed loudly, typos failed silently. Now both fail loudly.
    '
    ' Arm 1 pairs the typo with a REAL admitted key: the real one still applies, the marker
    ' still shows, and the typo is reported separately — so the fix cannot be satisfied by
    ' simply deactivating any overlay that contains an absent key.
    ' Arm 2 is the typo alone: no marker, base untouched, startup fine.
    Private Sub A50k_AdmittedButAbsentKeyIsWarnedAndDoesNotActivate()
        Dim dirA As String = A50TempDir("k1")
        Dim dirB As String = A50TempDir("k2")
        Try
            ' Arm 1 — typo alongside a real admitted key.
            A50Init(dirA, A50Json(A50BaseSettings()),
                    "{""trade_store"":{""enabledd"":false},""live_strip"":{""enabled"":false}}")
            Dim cA = SettingsLoader.Current
            Dim mixedOk As Boolean =
                SettingsLoader.OverlayUnknownKeys.Count = 1 AndAlso
                SettingsLoader.OverlayUnknownKeys(0) = "trade_store.enabledd" AndAlso
                cA.TradeStore.Enabled AndAlso                      ' the typo changed NOTHING
                Not cA.LiveStrip.Enabled AndAlso                   ' the real key still applies
                SettingsLoader.OverlayActive                       ' …and still earns "+local"
            ' Snapshot arm 1's diagnostics before arm 2 replaces the singleton's state.
            Dim unknownA As String = String.Join(",", SettingsLoader.OverlayUnknownKeys)
            Dim activeA As Boolean = SettingsLoader.OverlayActive

            ' Arm 2 — the typo on its own. This is the F6 shape: capture must stay ON and the
            ' title bar must NOT claim an overlay is doing something.
            A50Init(dirB, A50Json(A50BaseSettings()), "{""trade_store"":{""enabledd"":false}}")
            Dim cB = SettingsLoader.Current
            Dim aloneOk As Boolean =
                SettingsLoader.OverlayUnknownKeys.Count = 1 AndAlso
                SettingsLoader.OverlayUnknownKeys(0) = "trade_store.enabledd" AndAlso
                Not SettingsLoader.OverlayActive AndAlso           ' no false "+local"
                cB.TradeStore.Enabled AndAlso                      ' base untouched — capture ON
                cB.TradeStore.StoreDir = "backtest_data" AndAlso
                cB.Version = 64                                    ' startup succeeded, tree intact

            Check("A50k admitted-but-absent key — warned, excluded from +local, base untouched; a real sibling key still applies and still activates",
                  mixedOk AndAlso aloneOk,
                  String.Format("mixed={0} alone={1} armA(unknown=[{2}] tradeStore={3} liveStrip={4} active={5}) armB(unknown=[{6}] tradeStore={7} active={8})",
                                mixedOk, aloneOk, unknownA, cA.TradeStore.Enabled, cA.LiveStrip.Enabled, activeA,
                                String.Join(",", SettingsLoader.OverlayUnknownKeys),
                                cB.TradeStore.Enabled, SettingsLoader.OverlayActive))
        Finally
            A50Cleanup(dirA)
            A50Cleanup(dirB)
        End Try
    End Sub

    ' -- A58c: overlay routes auto_run.start_engaged key-granular, and the tweaker fence
    ' still rejects it (docs/collector-ops-tooling-proposal.md §1.4) --------------------------
    ' Base ships true (collectors, hands-off after a scripted deploy); the dev box opts out
    ' via settings.local.json — the SAME overlay mechanism as trade_store.enabled (A50b). Pins
    ' BOTH directions of the routing PLUS that admitting the key individually does not open a
    ' hole on the tweaker surface: auto_run. stays whole-block fenced (HARD CONSTRAINT 14
    ' label; the shared prefix-reject message text is "HARD CONSTRAINT 11/12" — see A57d's
    ' HC28 fixture for the contrasting exact-match-reject message shape).
    Private Sub A58c_OverlayRoutesStartEngagedAndTweakerFenceStillRejectsIt()
        Dim dir As String = A50TempDir("58c")
        Try
            Dim baseCfg = A50BaseSettings()
            baseCfg.AutoRun.StartEngaged = True
            A50Init(dir, A50Json(baseCfg), "{""auto_run"":{""start_engaged"":false}}")

            Dim c = SettingsLoader.Current
            Dim applied = SettingsLoader.OverlayAppliedKeys

            Dim s As String = "{""version"":67,""auto_run"":{""start_engaged"":false}}"
            Dim rTweak = SettingsDiffApplier.Validate(OneDiff("auto_run.start_engaged", "false", "true"), s, 3)

            Check("A58c overlay admits auto_run.start_engaged key-granular (base true -> overlaid false), and the tweaker fence still rejects it (auto_run. stays whole-block fenced)",
                  Not c.AutoRun.StartEngaged AndAlso
                  applied.Count = 1 AndAlso applied(0) = "auto_run.start_engaged" AndAlso
                  Not rTweak.IsValid AndAlso rTweak.ErrorReason.Contains("HARD CONSTRAINT 11/12"),
                  String.Format("mergedStartEngaged={0} applied=[{1}] tweakerValid={2} tweakerReason='{3}'",
                                c.AutoRun.StartEngaged, String.Join(",", applied), rTweak.IsValid, rTweak.ErrorReason))
        Finally
            A50Cleanup(dir)
        End Try
    End Sub

End Module
