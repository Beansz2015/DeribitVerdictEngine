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
Imports System.Text.Json
Imports System.Threading

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
        ' v35 min-tradeable-move gate: give the cascade a tradeable ATR (raw target
        ' 50×2.0=100 > floor 0.0008×100000=80) so the Step 5c gate doesn't veto the
        ' directional verdict this fixture asserts. Mirrors how this fixture disables
        ' MTF/Pass2b/2c to isolate the dominant-side cascade. (A12 already sets 50.)
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
    ' norms.ATRScaleFactor. price=100000, ATR=50, targetMult=2.0 (default),
    ' norms.ATRScaleFactor=2.0 (deliberately ≠ 1 to expose the old bug). The
    ' linear raw long target = 100000 + 50×2.0 = 100100; the old quadratic form
    ' would have been 100000 + 50×2.0×2.0 = 100200. A Tier-1 swing-high cap fires
    ' only when SwingTargetLong < rawLongTarget, so the cap boundary pins the raw
    ' target exactly:
    '   swing 100099 (< 100100) → caps → AdjustedLongTarget = 100099
    '   swing 100101 (> 100100) → no cap → AdjustedLongTarget = 0
    ' Under the old quadratic geometry the boundary would sit at 100200 and BOTH
    ' would cap; the bracket proves the scale factor is absent.
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
        Dim cfg = BuildA8Cfg(fundingBoost:=0)   ' SHORT dominant; floor = POCO default 0.0008

        ' A13a — low ATR: raw short target 13×2.0=26 < floor 49.6 → gate fires.
        Dim vLow = ScoringEngine.Calculate(BuildGateIndicators(atr:=13, price:=62000),
                                           PositionState.None, BuildA8Norms(), cfg)
        Check("A13a low-ATR veto (target 26 < floor 49.6 → NO TRADE)",
              vLow.Verdict = "NO TRADE" AndAlso vLow.VerdictContext = "BELOW_MIN_MOVE",
              String.Format("expected NO TRADE / BELOW_MIN_MOVE, got '{0}' / {1}", vLow.Verdict, vLow.VerdictContext))

        ' A13b — tradeable ATR: raw short target 30×2.0=60 > floor 49.6 → stands.
        Dim vOk = ScoringEngine.Calculate(BuildGateIndicators(atr:=30, price:=62000),
                                          PositionState.None, BuildA8Norms(), cfg)
        Check("A13b tradeable-ATR (target 60 > floor 49.6 → directional stands)",
              Not vOk.Verdict.StartsWith("NO TRADE") AndAlso vOk.VerdictContext <> "BELOW_MIN_MOVE",
              String.Format("expected directional SHORT, got '{0}' / {1}", vOk.Verdict, vOk.VerdictContext))

        ' A13c — near-swing cap (validates the EFFECTIVE-target choice A): high ATR
        ' so the raw target (100×2.0=200) clears the floor, but a swing cap pulls
        ' the short target to 30 points from entry (< floor) → gate still fires.
        Dim rNear = BuildGateIndicators(atr:=100, price:=62000)
        rNear.SwingTargetShort = 61970          ' 30 below entry; > rawShortTarget 61800 → caps
        Dim vNear = ScoringEngine.Calculate(rNear, PositionState.None, BuildA8Norms(), cfg)
        Check("A13c near-swing cap veto (capped target 30 < floor 49.6 → NO TRADE)",
              vNear.Verdict = "NO TRADE" AndAlso vNear.VerdictContext = "BELOW_MIN_MOVE" AndAlso
              Math.Abs(vNear.AdjustedShortTarget - 61970) < 0.001,
              String.Format("expected NO TRADE / BELOW_MIN_MOVE / cap 61970, got '{0}' / {1} / cap {2:F1}",
                            vNear.Verdict, vNear.VerdictContext, vNear.AdjustedShortTarget))

        ' A13d — editability: lower the floor to 0.0004 (24.8); A13a's 26-pt target
        ' now clears → the shared key drives the gate (hot-reloadable in-app).
        Dim cfgLow = BuildA8Cfg(fundingBoost:=0)
        cfgLow.Scoring.MinTradeableMovePct = 0.0004
        Dim vEdit = ScoringEngine.Calculate(BuildGateIndicators(atr:=13, price:=62000),
                                            PositionState.None, BuildA8Norms(), cfgLow)
        Check("A13d editability (floor 24.8 < target 26 → directional stands)",
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
    Private Sub A14c_AtrOn3MinGateFlip()
        Dim atr1m As Double = IndicatorEngine.CalcATR(FlatRangeCandles(30, 62000, 13), 7)
        Dim atr3m As Double = IndicatorEngine.CalcATR(FlatRangeCandles(30, 62000, 27), 7)

        Dim cfg = BuildA8Cfg(fundingBoost:=0)
        Dim vLow = ScoringEngine.Calculate(BuildGateIndicators(atr1m, 62000),
                                           PositionState.None, BuildA8Norms(), cfg)
        Dim vHigh = ScoringEngine.Calculate(BuildGateIndicators(atr3m, 62000),
                                            PositionState.None, BuildA8Norms(), cfg)

        Check("A14c ATR on 3-min clears the gate the 1-min ATR can't",
              Math.Abs(atr1m - 13) < 1.0 AndAlso Math.Abs(atr3m - 27) < 1.0 AndAlso
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

    ' -- A15g: the new guard does not over-match session_volume keys -----------
    Private Sub A15g_ValidatePassesNormalSessionVolumeKey()
        Dim s As String = "{""version"":1,""session_volume"":{""enabled"":true,""sessions"":[{""name"":""NY"",""high_multiplier"":1.15}]}}"

        ' A resolvable, non-guarded session_volume key passes Validate cleanly.
        Dim rEn = SettingsDiffApplier.Validate(OneDiff("session_volume.enabled", "true", "false"), s, 3)
        ' An array-path session_volume multiplier is rejected only as UNRESOLVED
        ' (NavigatePath can't traverse the sessions array) — NOT by the new
        ' HARD CONSTRAINT 11 guard. Confirms no over-match onto session_volume.
        Dim rArr = SettingsDiffApplier.Validate(OneDiff("session_volume.sessions.0.high_multiplier", "1.15", "1.2"), s, 3)

        Check("A15g over-match guard (session_volume.enabled passes; array multiplier not guard-rejected)",
              rEn.IsValid AndAlso (Not rArr.IsValid) AndAlso
              Not rArr.ErrorReason.Contains("HARD CONSTRAINT 11"),
              String.Format("enabled={0} arrValid={1} arrReason='{2}'",
                            rEn.IsValid, rArr.IsValid, rArr.ErrorReason))
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
        ' POCO defaults carry the live multipliers: stop ×1.2 / target ×2.0,
        ' trigger_mode "interval". Version pinned so the pass-through is visible.
        Dim cfg As New EngineSettings()
        cfg.Version = 49
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

    ' -- A22a: full payload serialises field-by-field; three target-cap cases --
    Private Sub A22a_PayloadFieldByField()
        ' Case 1 — uncapped (Adjusted*Target = 0). ATR distances: stop 49.56 / target 82.6.
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
              eng.GetProperty("settings_version").GetInt32() = 49 AndAlso
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
        Check("A22a uncapped levels (linear ATR distances, capped=false, cap_reason=null, raw=target)",
              Math.Abs(JNum(lng, "entry") - 59012.5) < 0.0001 AndAlso
              Math.Abs(JNum(lng, "stop") - (59012.5 - 49.56)) < 0.0001 AndAlso
              Math.Abs(JNum(lng, "target") - (59012.5 + 82.6)) < 0.0001 AndAlso
              Not lng.GetProperty("target_capped").GetBoolean() AndAlso
              lng.GetProperty("cap_reason").ValueKind = JsonValueKind.Null AndAlso
              Math.Abs(JNum(lng, "raw_target") - JNum(lng, "target")) < 0.0001 AndAlso
              Math.Abs(JNum(sht, "stop") - (59012.5 + 49.56)) < 0.0001 AndAlso
              Math.Abs(JNum(sht, "target") - (59012.5 - 82.6)) < 0.0001 AndAlso
              Not sht.GetProperty("target_capped").GetBoolean(),
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

        ' Case 2 — genuinely capped long target (adjustment 35.1 >= noise floor 0.826).
        Dim vCap = BuildBridgeVerdict()
        vCap.AdjustedLongTarget = 59060.0
        vCap.TargetCapReasonLong = "CAPPED @ 59060.0 (SWING_HIGH_5M)"
        Dim lngCap = BridgeOkJson(vCap, BuildBridgeIndicators()).RootElement _
                         .GetProperty("levels").GetProperty("long")
        Check("A22a capped long (target=adjusted, capped=true, reason verbatim, raw preserved)",
              Math.Abs(JNum(lngCap, "target") - 59060.0) < 0.0001 AndAlso
              lngCap.GetProperty("target_capped").GetBoolean() AndAlso
              lngCap.GetProperty("cap_reason").GetString() = "CAPPED @ 59060.0 (SWING_HIGH_5M)" AndAlso
              Math.Abs(JNum(lngCap, "raw_target") - (59012.5 + 82.6)) < 0.0001,
              "capped long mismatch: " & lngCap.GetRawText())

        ' Case 3 — sub-tick cap-noise suppression (adjustment 0.3 < floor
        ' max(0.5, ATR×0.02)=0.826): the display renders UNCAPPED, so the file
        ' reports target_capped=false with the adjusted value (§3 field sourcing).
        Dim vNoise = BuildBridgeVerdict()
        vNoise.AdjustedLongTarget = 59094.8
        vNoise.TargetCapReasonLong = "CAPPED @ 59094.8 (POC)"
        Dim lngNoise = BridgeOkJson(vNoise, BuildBridgeIndicators()).RootElement _
                           .GetProperty("levels").GetProperty("long")
        Check("A22a cap-noise-suppressed long (target=adjusted, capped=FALSE, cap_reason null)",
              Math.Abs(JNum(lngNoise, "target") - 59094.8) < 0.0001 AndAlso
              Not lngNoise.GetProperty("target_capped").GetBoolean() AndAlso
              lngNoise.GetProperty("cap_reason").ValueKind = JsonValueKind.Null AndAlso
              Math.Abs(JNum(lngNoise, "raw_target") - (59012.5 + 82.6)) < 0.0001,
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
        Check("A23f per-session resolution (NY norm 60; LONDON/ASIA inherit 120/2.5; explicit override wins)",
              nyNorm = 60.0 AndAlso londonNorm = 120.0 AndAlso asiaNorm = 120.0 AndAlso
              nyThr = 2.5 AndAlso asiaThr = 3.1,
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
    ' three cap cases A22a pins (uncapped / capped / cap-noise-suppressed). The
    ' CSV Placed* columns and the payload levels block both read ComputeSideLevels,
    ' so this pin holds the parity for both surfaces.
    Private Sub A24a_PlacedLevelsEqualPayloadLevels()
        Dim cfg = BuildBridgeCfg()
        Dim r = BuildBridgeIndicators()

        Dim ok As Boolean = True
        Dim detail As String = ""
        ' (adjustedLong, reasonLong) per case: uncapped / capped / noise-suppressed.
        Dim cases = New List(Of (Name As String, Adj As Double, Reason As String)) From {
            ("uncapped", 0.0, Nothing),
            ("capped", 59060.0, "CAPPED @ 59060.0 (SWING_HIGH_5M)"),
            ("noise", 59095.0, "CAPPED @ 59095.0 (SWING_HIGH_5M)")}   ' |raw−adj| = 0.1 < floor
        For Each c In cases
            Dim v = BuildBridgeVerdict()
            v.AdjustedLongTarget = c.Adj
            v.TargetCapReasonLong = c.Reason
            Dim lvLong = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=True)
            Dim lvShort = SignalEmitter.ComputeSideLevels(v, r, cfg, isLong:=False)
            Dim levels = BridgeOkJson(v, r).RootElement.GetProperty("levels")
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
        ' Sanity-pin the capped case's absolute values (raw target 59095.1, adjusted wins).
        Dim vPin = BuildBridgeVerdict()
        vPin.AdjustedLongTarget = 59060.0
        vPin.TargetCapReasonLong = "CAPPED @ 59060.0 (SWING_HIGH_5M)"
        Dim pin = SignalEmitter.ComputeSideLevels(vPin, r, cfg, isLong:=True)
        If Not (pin.Capped AndAlso pin.Target = 59060.0 AndAlso
                Math.Abs(pin.StopPx - (59012.5 - 41.3 * 1.2)) < 0.0001) Then
            ok = False
            detail &= String.Format(CultureInfo.InvariantCulture,
                                    "pin mismatch: capped={0} target={1} stop={2}; ",
                                    pin.Capped, pin.Target, pin.StopPx)
        End If
        Check("A24a Placed* levels ≡ payload levels (shared ComputeSideLevels, 3 cap cases + pin)", ok, detail)
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

End Module
