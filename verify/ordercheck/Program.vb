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
    '   floor = MinTradeableMovePct(0.0008) × 62000 = 49.6
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

        ' A13d — editability: lower the floor to 0.0003 (18.6); A13a's 22.75-pt target
        ' now clears → the shared key drives the gate (hot-reloadable in-app).
        Dim cfgLow = BuildA8Cfg(fundingBoost:=0)
        cfgLow.Scoring.MinTradeableMovePct = 0.0003
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

            Check("A15e re-seed on filter change (ASIA|3 → NY|1: index→3, INELIGIBLE, nothing evaluated)",
                  rc = 2 AndAlso st.LastEvaluatedRowIndex = 3 AndAlso
                  st.PopulationFilterKey = "NY|1" AndAlso st.LastRunOutcome = "INELIGIBLE",
                  String.Format("rc={0} idx={1} key={2} outcome={3}",
                                rc, st.LastEvaluatedRowIndex, st.PopulationFilterKey, st.LastRunOutcome))
        Finally
            Try : System.IO.Directory.Delete(dir, True) : Catch : End Try
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
        Dim rMin = SettingsDiffApplier.Validate(OneDiff("scoring.min_tradeable_move_pct", "0.0008", "0.0010"), s, 3)
        Dim rCtl = SettingsDiffApplier.Validate(OneDiff("indicators.OBV.trend_gate", "10", "12"), s, 3)

        Check("A15f Validate rejects resolution_profiles.* / kelly.* / min_tradeable_move_pct, passes OBV.trend_gate",
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
        ' [v52 wire-in] NY now carries an EXPLICIT burst_ratio_threshold 4.5 (the §5.2 value,
        ' also the S2a scoping key); LONDON/ASIA still inherit the 2.5 default until their own pass.
        Check("A23f per-session resolution (NY norm 60 / thr 4.5; LONDON/ASIA inherit 120/2.5; explicit override wins)",
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
        ' (1) S2a scoping: a LONDON-hour run (res-3, no explicit burst_ratio_threshold)
        '     leaves the modifier inert even with a same-side BURST_SELL present.
        '     (LONDON fallback ×2.0 clears the min-move floor, so the SHORT stands and
        '     EffectiveShortScore reflects the plain cascade — no gate confound.)
        Dim vLondon = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 10),
                                              PositionState.None, BuildA8Norms(), cfg)
        ' (2) scoring_enabled:false → inert at NY too (the hot rollback).
        Dim cfgOff = BuildBurstCfg()
        cfgOff.Indicators.AggressorVelocity.ScoringEnabled = False
        Dim vOff = ScoringEngine.Calculate(BuildBurstIndicators("BURST_SELL", 15),
                                           PositionState.None, BuildA8Norms(), cfgOff)
        ' Baseline NORMAL at NY (modifier eligible but tape calm) for the identity anchor.
        Dim vNorm = ScoringEngine.Calculate(BuildBurstIndicators("NORMAL", 15),
                                            PositionState.None, BuildA8Norms(), cfg)

        Check("A28c S2a scoping + disable inert (LONDON burst ss=11; scoring_enabled:false ss=11 == NORMAL)",
              vLondon.EffectiveShortScore = 11 AndAlso
              vOff.EffectiveShortScore = 11 AndAlso
              vNorm.EffectiveShortScore = 11,
              String.Format("london={0} off={1} norm={2} (all must be 11 — modifier inert)",
                            vLondon.EffectiveShortScore, vOff.EffectiveShortScore, vNorm.EffectiveShortScore))
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
    ' A31 — book absorption at structural levels (P4 #6 build sub-version).
    ' docs/book-absorption-proposal.md §4/§9 + docs/book-absorption-implementer-brief.md.
    ' Exercises the REAL LevelAbsorptionTracker + ClassifyAbsorption +
    ' ResolveAbsorptionMinAggrUsd. Tick $0.5 ⇒ POCO defaults: proximity $6,
    ' band $2, break tolerance $1, window 10s, absorb_ratio 3.0, floor 25000,
    ' max_pull_frac 0.5, min_aggr_usd 150000. The feed-side folds + the WS-only
    ' run-path gate stay OUT (live-socket/WinForms boundary, the A23 precedent) —
    ' REST-inertness holds by construction: nothing folds the tracker off the WS
    ' feed, and the cold tracker reads NONE/null (A31e/A31f pin that surface).
    ' =======================================================================

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
        tr.SetLevels(100010.0, 0, 0, 0)

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
        tr.SetLevels(100011.0, 0, 0, 0)
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
        tr.SetLevels(100010.0, 0, 0, 0)

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
        tr.SetLevels(100010.0, 0, 0, 0)

        tr.FoldBook(AbsBandBook(), t0, ab)
        For i As Integer = 0 To 6
            tr.FoldTrade(100008.0, 30000.0, isBuy:=True, tsMs:=t0 + 10 + i, cfg:=ab)  ' 210000 pressing, no fills
        Next
        tr.FoldBook(AbsBandBook(level10Size:=40000.0), t0 + 100, ab)   ' −60000, no fills → pull
        tr.FoldBook(AbsBandBook(), t0 + 200, ab)                       ' +60000 repost → post
        tr.FoldBook(AbsBandBook(level10Size:=40000.0), t0 + 300, ab)   ' −60000 again → pull

        Dim s = tr.Snapshot(t0 + 350, ab)
        Dim read = IndicatorEngine.ClassifyAbsorption(s, ab.Defaults.MinAggrUsd, ab.AbsorbRatio, ab.MaxPullFrac)

        Check("A31c churn (pullLB 120000 / postLB 60000 → pullFrac 2.0 > 0.5 → D8 veto NONE; ratio 3.5 would have fired)",
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
        tr.SetLevels(100010.0, 0, 0, 0)

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
        tr.SetLevels(100010.0, 0, 0, 0)
        tr.FoldBook(AbsBandBook(), t0 + 100, ab)
        Dim sActive = tr.Snapshot(t0 + 100, ab)
        tr.Reset()
        Dim sReset = tr.Snapshot(t0 + 200, ab)
        tr.FoldBook(AbsBandBook(), t0 + 300, ab)      ' levels cleared by Reset ⇒ still idle
        Dim sNoLevels = tr.Snapshot(t0 + 300, ab)
        tr.SetLevels(100010.0, 0, 0, 0)
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
        Check("A31g session min_aggr_usd (NY override 250000 / LONDON inherits 150000 / unstamped 150000)",
              mNy = 250000.0 AndAlso mLon = 150000.0 AndAlso mUnset = 150000.0,
              String.Format(CultureInfo.InvariantCulture, "ny={0} lon={1} unset={2}", mNy, mLon, mUnset))

        ' HC23 fences: the two switches exact-match rejected; default./sessions. prefixes
        ' rejected; the flat params stay proposable.
        Dim s As String = "{""version"":54,""indicators"":{""absorption"":{""enabled"":true," &
                          """scoring_enabled"":false,""proximity_ticks"":12,""band_ticks"":4," &
                          """window_sec"":10,""break_tol_ticks"":2,""absorb_ratio"":3.0," &
                          """depletion_floor_usd"":25000,""max_pull_frac"":0.5,""penalty"":1," &
                          """default"":{""min_aggr_usd"":150000},""sessions"":{""NY"":{}}}}}"
        Dim rEnabled = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.enabled", "true", "false"), s, 3)
        Dim rScoring = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.scoring_enabled", "false", "true"), s, 3)
        Dim rDefault = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.default.min_aggr_usd", "150000", "100000"), s, 3)
        Dim rSession = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.sessions.NY.min_aggr_usd", "150000", "250000"), s, 3)
        Dim rProx = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.proximity_ticks", "12", "10"), s, 3)
        Dim rRatio = SettingsDiffApplier.Validate(OneDiff("indicators.absorption.absorb_ratio", "3.0", "3.5"), s, 3)
        Check("A31g HC23 fences (enabled/scoring_enabled + default./sessions. rejected; proximity_ticks + absorb_ratio tunable)",
              Not rEnabled.IsValid AndAlso rEnabled.ErrorReason.Contains("HARD CONSTRAINT 23") AndAlso
              Not rScoring.IsValid AndAlso rScoring.ErrorReason.Contains("HARD CONSTRAINT 23") AndAlso
              Not rDefault.IsValid AndAlso Not rSession.IsValid AndAlso
              rProx.IsValid AndAlso rRatio.IsValid,
              String.Format("enabled={0}'{1}' scoring={2} default={3} session={4} prox={5} ratio={6}",
                            rEnabled.IsValid, rEnabled.ErrorReason, rScoring.IsValid,
                            rDefault.IsValid, rSession.IsValid, rProx.IsValid, rRatio.IsValid))
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
                                               cfg.Scoring.MinTradeableMovePct,
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

    ' -- A39e: informational Absorption* + un-armed AggrVel provably absent from the
    ' -- decision-model design matrix. Build a schema and check that no column name
    ' -- carries an informational feature name; also check the ScoredCategoricalNames
    ' -- and ScoredNumericNames don't list them.
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
        Dim banned As String() = {"Absorption", "AggrVel"}
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
            If nm.IndexOf("Absorption", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               nm.IndexOf("AggrVel", StringComparison.OrdinalIgnoreCase) >= 0 Then
                clean = False
                offender = "cat:" & nm
            End If
        Next
        For Each nm In schema.ScoredNumericNames
            If nm.IndexOf("Absorption", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               nm.IndexOf("AggrVel", StringComparison.OrdinalIgnoreCase) >= 0 Then
                clean = False
                offender = "num:" & nm
            End If
        Next

        ' Also verify the transformed X has no non-zero columns at banned positions
        ' — the schema drives everything, so column-name absence is the primary proof;
        ' this is belt-and-braces.
        Dim X = FeatureMatrix.Transform(schema, bundles)
        Dim xClean As Boolean = X.GetLength(1) = schema.Columns.Count

        Check("A39e informational Absorption/AggrVel-un-armed extras absent from decision matrix",
              clean AndAlso xClean,
              String.Format("clean={0} offender={1} cols={2}", clean, offender, schema.Columns.Count))
    End Sub

    ' Standard-normal sample via Box-Muller (uniform → N(0,1)).
    Private Function GaussianStd(rng As Random) As Double
        Dim u1 As Double = 1.0 - rng.NextDouble()
        Dim u2 As Double = 1.0 - rng.NextDouble()
        Return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)
    End Function

End Module
