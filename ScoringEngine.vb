' ScoringEngine.vb  v0.28
' Implements the 6-step verdict engine from the specification.
' Input: IndicatorResults + DynamicNorms + position state. Output: VerdictResult.
' v0.23: Volume and VWAPDev thresholds now driven by DynamicNorms instead of static constants.
' v0.25: VerdictResult now carries MaxScore (regime-aware: 17 TRENDING / 16 RANGE_BOUND / 13 TRANSITIONAL).
'        Verdict thresholds scaled proportionally per regime.
'        ScoringEngine.MaxScore const kept for legacy reference (= 17, the theoretical ceiling).
' v0.26: CVD signal block added after OFI, before Liquidations.
'        CVD divergence penalty applied before liquidation penalty.
' v0.27: ThresholdStrong/Med/Weak now read from EngineSettings (settings.json) instead of hardcoded pcts.
'        Calculate() signature updated: accepts EngineSettings as third parameter.
' v0.28: VWAP scoring block rewritten to use adaptive sigma bands.
'        Full signal: price between VWAP and sigma1 band (tight mean-reversion zone).
'        Partial signal: price between sigma1 and sigma2 (extended zone).
'        No signal: price beyond sigma2 (overextended -- noise territory).
'        Warmup guard: VWAP scoring skipped entirely if VWAPSessionCandles < 15.

' Replaces anonymous tuple in List(Of (...)) which confuses the VB.NET parser
Public Class SignalBreakdownItem
    Public Property Label As String
    Public Property LongHit As Boolean
    Public Property ShortHit As Boolean
    Public Property Note As String
    Public Sub New(lbl As String, lng As Boolean, sht As Boolean, nt As String)
        Label = lbl : LongHit = lng : ShortHit = sht : Note = nt
    End Sub
End Class

Public Class VerdictResult
    Public Property LongScore As Integer
    Public Property ShortScore As Integer
    Public Property EffectiveLongScore As Integer
    Public Property EffectiveShortScore As Integer
    Public Property RegimePenalty As Integer
    ''' <summary>Regime-aware maximum achievable score. 17=TRENDING, 16=RANGE_BOUND, 13=TRANSITIONAL.</summary>
    Public Property MaxScore As Integer
    Public Property Verdict As String
    Public Property Confidence As String
    Public Property HoldStatus As String
    Public Property SignalBreakdown As New List(Of SignalBreakdownItem)
End Class

Public Enum PositionState
    None
    InLong
    InShort
End Enum

Public Enum SignalCategory
    Momentum
    Volume
    MarketStructure
    Microstructure
End Enum

Public Class ScoreState
    Public Property FullLongCategories As New HashSet(Of SignalCategory)
    Public Property FullShortCategories As New HashSet(Of SignalCategory)
    Public Property LongScore As Integer
    Public Property ShortScore As Integer
End Class

Public Class ScoringEngine

    ' Theoretical max score (TRENDING regime, all signals firing). Legacy reference.
    Public Const MaxScore As Integer = 17

    ' Regime-specific max achievable scores
    ' TRENDING:     17  (regime bonus +4 available)
    ' RANGE_BOUND:  16  (no ADX bonus, one partial upgrade less likely)
    ' TRANSITIONAL: 13  (ADX penalty applied)
    Public Shared Function RegimeMaxScore(regime As String) As Integer
        Select Case regime
            Case "TRENDING_UP", "TRENDING_DOWN" : Return 17
            Case "RANGE_BOUND"                  : Return 16
            Case Else                           : Return 13   ' TRANSITIONAL
        End Select
    End Function

    ' Verdict thresholds derived from settings.json percentages (VerdictStrongPct etc.)
    ' Rounded up so thresholds are always whole numbers achievable in practice.
    Private Shared Function ThresholdStrong(maxScore As Integer, pct As Double) As Integer
        Return CInt(Math.Ceiling(maxScore * pct))
    End Function
    Private Shared Function ThresholdMed(maxScore As Integer, pct As Double) As Integer
        Return CInt(Math.Ceiling(maxScore * pct))
    End Function
    Private Shared Function ThresholdWeak(maxScore As Integer, pct As Double) As Integer
        Return CInt(Math.Ceiling(maxScore * pct))
    End Function

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState,
                                     norms As DynamicNorms,
                                     cfg As EngineSettings) As VerdictResult
        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim state As New ScoreState()

        ' Determine regime-aware MaxScore up front so VerdictResult always carries it
        Dim regimeMax As Integer = RegimeMaxScore(r.Regime)
        res.MaxScore = regimeMax

        ' -- Step 2: Weighted Signal Scoring ----------------------------------

        ' CORE
        ' ROC (Momentum)
        Dim rocLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        Dim rocPartialLong As Boolean = r.ROC > 0.1 AndAlso r.ROCSlope <> "RISING"
        Dim rocPartialShort As Boolean = r.ROC < -0.1 AndAlso r.ROCSlope <> "FALLING"
        AddFull(state, rocLong, rocShort, SignalCategory.Momentum)

        ' RSI (Momentum)
        Dim rsiLong As Boolean = r.RSI > 60
        Dim rsiShort As Boolean = r.RSI < 40
        Dim rsiPartialLong As Boolean = r.RSI > 50 AndAlso r.RSI <= 60
        Dim rsiPartialShort As Boolean = r.RSI < 50 AndAlso r.RSI >= 40
        AddFull(state, rsiLong, rsiShort, SignalCategory.Momentum)

        ' DMI (MarketStructure)
        Dim dmiLong As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        AddFull(state, dmiLong, dmiShort, SignalCategory.MarketStructure)

        ' ADX (MarketStructure)
        Dim adxLong As Boolean = r.ADX > 25 AndAlso dmiLong
        Dim adxShort As Boolean = r.ADX > 25 AndAlso dmiShort
        AddFull(state, adxLong, adxShort, SignalCategory.MarketStructure)

        ' Volume (Volume) -- thresholds from DynamicNorms
        Dim volHigh As Double = norms.VolHighThreshold
        Dim volMid As Double = norms.VolMidThreshold
        Dim volLong As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC > 0 AndAlso r.CurrentPrice > r.VWAP
        Dim volShort As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC < 0 AndAlso r.CurrentPrice < r.VWAP
        Dim volPartial As Boolean = r.VolumeRatio >= volMid AndAlso r.VolumeRatio < volHigh
        AddFull(state, volLong, volShort, SignalCategory.Volume)

        ' TIER 1
        ' VWAP (Microstructure) -- adaptive sigma bands
        ' Warmup guard: skip entirely if session has fewer than 15 candles
        Dim vwapLong As Boolean = False
        Dim vwapShort As Boolean = False
        Dim vwapPartialLong As Boolean = False
        Dim vwapPartialShort As Boolean = False
        Dim vwapNote As String
        Dim vwapWarmup As Boolean = r.VWAPSessionCandles < 15

        If vwapWarmup Then
            vwapNote = String.Format("WARMUP ({0}/15 candles) -- signal suppressed", r.VWAPSessionCandles)
        Else
            Dim price As Double = r.CurrentPrice
            ' Full signal: price is between VWAP and sigma1 (mean-reversion confirmation zone)
            vwapLong  = price > r.VWAP AndAlso price <= r.VWAPSigma1Upper
            vwapShort = price < r.VWAP AndAlso price >= r.VWAPSigma1Lower
            ' Partial signal: price is between sigma1 and sigma2 (extended but not overextended)
            vwapPartialLong  = price > r.VWAPSigma1Upper AndAlso price <= r.VWAPSigma2Upper
            vwapPartialShort = price < r.VWAPSigma1Lower AndAlso price >= r.VWAPSigma2Lower
            ' Beyond sigma2: no signal (overextended, mean-reversion risk too high)
            AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure)
            vwapNote = String.Format(
                "Price:{0:F0} VWAP:{1:F0} | σ1:[{2:F0},{3:F0}] σ2:[{4:F0},{5:F0}] | {6}candles",
                price, r.VWAP,
                r.VWAPSigma1Lower, r.VWAPSigma1Upper,
                r.VWAPSigma2Lower, r.VWAPSigma2Upper,
                r.VWAPSessionCandles)
        End If

        ' BBW Squeeze-State Scoring
        Dim bbwLongHit As Boolean = False
        Dim bbwShortHit As Boolean = False
        Dim bbwNote As String

        Select Case r.SqueezeStatus
            Case "ACTIVE"
                state.LongScore  = Math.Max(0, state.LongScore  - 1)
                state.ShortScore = Math.Max(0, state.ShortScore - 1)
                bbwNote = String.Format("{0:F3} | ACTIVE -- penalty -1 both sides", r.BBW)
            Case "RELEASING"
                If r.ROC > 0.1 Then
                    state.LongScore += 1
                    state.FullLongCategories.Add(SignalCategory.Microstructure)
                    bbwLongHit = True
                    bbwNote = String.Format("{0:F3} | RELEASING -- breakout [L] (ROC {1:F3})", r.BBW, r.ROC)
                ElseIf r.ROC < -0.1 Then
                    state.ShortScore += 1
                    state.FullShortCategories.Add(SignalCategory.Microstructure)
                    bbwShortHit = True
                    bbwNote = String.Format("{0:F3} | RELEASING -- breakout [S] (ROC {1:F3})", r.BBW, r.ROC)
                Else
                    bbwNote = String.Format("{0:F3} | RELEASING -- ROC chop ({1:F3}), no award", r.BBW, r.ROC)
                End If
            Case Else
                bbwNote = String.Format("{0:F3} | NONE", r.BBW)
        End Select

        ' EMA Ribbon (MarketStructure)
        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        AddFull(state, emaBull, emaBear, SignalCategory.MarketStructure)

        ' OI (Microstructure)
        Dim oiLong As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort As Boolean = r.OISignal = "NEW SHORTS"
        Dim oiPartialLong As Boolean = r.OISignal = "COVERING"
        Dim oiPartialShort As Boolean = r.OISignal = "CAPITULATION"
        AddFull(state, oiLong, oiShort, SignalCategory.Microstructure)

        ' TIER 2
        ' OFI (Microstructure)
        Dim ofiBuy As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure)

        ' CVD (Microstructure)
        ' Full signal: slope aligned with net direction
        ' Divergence penalty: -1 if CVD contradicts price direction
        Dim cvdLong  As Boolean = r.CVDSlope = "RISING"  AndAlso r.CVDValue > 0
        Dim cvdShort As Boolean = r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0
        AddFull(state, cvdLong, cvdShort, SignalCategory.Microstructure)
        If r.CVDDivergence = "BEARISH" Then state.LongScore  = Math.Max(0, state.LongScore  - 1)
        If r.CVDDivergence = "BULLISH" Then state.ShortScore = Math.Max(0, state.ShortScore - 1)

        ' Liquidations -- penalty-only, scaled by size
        Dim liqLongPenalty As Integer = 0
        Dim liqShortPenalty As Integer = 0
        If r.LiqSignal = "LONG LIQS" Then
            liqLongPenalty = If(r.LiqLongSize > cfg.Indicators.Liquidations.LargeLiqSize, 2, 1)
            state.LongScore = Math.Max(0, state.LongScore - liqLongPenalty)
        ElseIf r.LiqSignal = "SHORT LIQS" Then
            liqShortPenalty = If(r.LiqShortSize > cfg.Indicators.Liquidations.LargeLiqSize, 2, 1)
            state.ShortScore = Math.Max(0, state.ShortScore - liqShortPenalty)
        End If

        ' 5m EMA200 (MarketStructure)
        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        AddFull(state, ema200Bull, ema200Bear, SignalCategory.MarketStructure)

        ' TIER 3
        ' Donchian (MarketStructure)
        Dim donchLong As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort As Boolean = r.DonchianSignal = "SHORT"
        AddFull(state, donchLong, donchShort, SignalCategory.MarketStructure)

        ' OBV (Volume)
        Dim obvLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "NONE"
        Dim obvShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "NONE"
        Dim obvPartialLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "BEARISH"
        Dim obvPartialShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "BULLISH"
        AddFull(state, obvLong, obvShort, SignalCategory.Volume)

        ' Pass 2: upgrade partials with cross-category full confirmation
        Dim rocLongUpgraded As Boolean = rocPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rocShortUpgraded As Boolean = rocPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rocLongUpgraded Then state.LongScore += 1
        If rocShortUpgraded Then state.ShortScore += 1

        Dim rsiLongUpgraded As Boolean = rsiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rsiShortUpgraded As Boolean = rsiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rsiLongUpgraded Then state.LongScore += 1
        If rsiShortUpgraded Then state.ShortScore += 1

        ' VWAP partial upgrades (only if not in warmup)
        Dim vwapLongUpgraded As Boolean = False
        Dim vwapShortUpgraded As Boolean = False
        If Not vwapWarmup Then
            vwapLongUpgraded  = vwapPartialLong  AndAlso HasCrossConfirm(state.FullLongCategories,  SignalCategory.Microstructure)
            vwapShortUpgraded = vwapPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
            If vwapLongUpgraded  Then state.LongScore  += 1
            If vwapShortUpgraded Then state.ShortScore += 1
        End If

        Dim oiLongUpgraded As Boolean = oiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += 1
        If oiShortUpgraded Then state.ShortScore += 1

        Dim obvLongUpgraded As Boolean = obvPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += 1
        If obvShortUpgraded Then state.ShortScore += 1

        ' Breakdown notes
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC")

        breakdown.Add(New SignalBreakdownItem("ROC(9)", rocLong OrElse rocLongUpgraded, rocShort OrElse rocShortUpgraded,
            BuildNote(String.Format("{0:F3} | Slope: {1}", r.ROC, r.ROCSlope),
                      rocPartialLong AndAlso Not rocLongUpgraded, rocPartialShort AndAlso Not rocShortUpgraded,
                      rocLongUpgraded, rocShortUpgraded)))

        Dim rsiNote As String = String.Format("{0:F1}", r.RSI)
        If r.RSIDivergence <> "NONE" Then rsiNote &= String.Format(" | DIV:{0}", r.RSIDivergence)
        breakdown.Add(New SignalBreakdownItem("RSI(9)", rsiLong OrElse rsiLongUpgraded, rsiShort OrElse rsiShortUpgraded,
            BuildNote(rsiNote,
                      rsiPartialLong AndAlso Not rsiLongUpgraded, rsiPartialShort AndAlso Not rsiShortUpgraded,
                      rsiLongUpgraded, rsiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("DMI +/-DI", dmiLong, dmiShort,
            String.Format("+DI:{0:F1} -DI:{1:F1}", r.PlusDI, r.MinusDI)))

        breakdown.Add(New SignalBreakdownItem("ADX>25", adxLong, adxShort,
            String.Format("{0:F1}", r.ADX)))

        breakdown.Add(New SignalBreakdownItem("Volume", volLong, volShort,
            BuildNote(String.Format("{0:F2}x | thr H:{1:F2}x M:{2:F2}x [{3}]",
                                   r.VolumeRatio, volHigh, volMid, normMode),
                      volPartial, volPartial, False, False)))

        breakdown.Add(New SignalBreakdownItem("VWAP", vwapLong OrElse vwapLongUpgraded, vwapShort OrElse vwapShortUpgraded,
            If(vwapWarmup, vwapNote,
               BuildNote(vwapNote,
                         vwapPartialLong AndAlso Not vwapLongUpgraded,
                         vwapPartialShort AndAlso Not vwapShortUpgraded,
                         vwapLongUpgraded, vwapShortUpgraded))))

        breakdown.Add(New SignalBreakdownItem("BBW Squeeze", bbwLongHit, bbwShortHit, bbwNote))

        breakdown.Add(New SignalBreakdownItem("EMA 9/21/50", emaBull, emaBear,
            String.Format("9:{0:F0} 21:{1:F0} 50:{2:F0} | {3}", r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment)))

        breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False,
            String.Format("{0:F4}% | {1}", r.FundingRate * 100, r.FundingBias)))

        breakdown.Add(New SignalBreakdownItem("OI Delta", oiLong OrElse oiLongUpgraded, oiShort OrElse oiShortUpgraded,
            BuildNote(String.Format("15m:{0:F2}% 60m:{1:F2}% | {2}", r.OIChange15m, r.OIChange60m, r.OISignal),
                      oiPartialLong AndAlso Not oiLongUpgraded, oiPartialShort AndAlso Not oiShortUpgraded,
                      oiLongUpgraded, oiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("OFI", ofiBuy, ofiSell,
            String.Format("Ratio:{0:F2} | {1}", r.OFIRatio, r.OFISignal)))

        ' CVD breakdown note: show divergence penalty flag if active
        Dim cvdNote As String = String.Format("Net:{0:F0} | Slope:{1} | Div:{2}", r.CVDValue, r.CVDSlope, r.CVDDivergence)
        If r.CVDDivergence <> "NONE" Then cvdNote &= " | PENALTY -1"
        breakdown.Add(New SignalBreakdownItem("CVD", cvdLong, cvdShort, cvdNote))

        Dim liqNote As String = String.Format("L:{0:F0} S:{1:F0} | {2}", r.LiqLongSize, r.LiqShortSize, r.LiqSignal)
        If liqLongPenalty > 0 Then liqNote &= String.Format(" | PENALTY -{0} [L]", liqLongPenalty)
        If liqShortPenalty > 0 Then liqNote &= String.Format(" | PENALTY -{0} [S]", liqShortPenalty)
        breakdown.Add(New SignalBreakdownItem("Liq Penalty", liqLongPenalty > 0, liqShortPenalty > 0, liqNote))

        breakdown.Add(New SignalBreakdownItem("5m EMA(200)", ema200Bull, ema200Bear,
            String.Format("{0:F0} | {1}", r.EMA200_5m, r.PriceVsEMA200)))

        breakdown.Add(New SignalBreakdownItem("Donchian(20)", donchLong, donchShort,
            String.Format("U:{0:F0} L:{1:F0} | {2}", r.DonchianUpper, r.DonchianLower, r.DonchianSignal)))

        breakdown.Add(New SignalBreakdownItem("OBV", obvLong OrElse obvLongUpgraded, obvShort OrElse obvShortUpgraded,
            BuildNote(String.Format("Trend:{0} Div:{1}", r.OBVTrend, r.OBVDivergence),
                      obvPartialLong AndAlso Not obvLongUpgraded, obvPartialShort AndAlso Not obvShortUpgraded,
                      obvLongUpgraded, obvShortUpgraded)))

        ' -- Step 3: Funding Rate Confidence Modifier -------------------------
        Dim ls As Integer = state.LongScore
        Dim ss As Integer = state.ShortScore
        Dim fr As Double = r.FundingRate
        If fr > cfg.Scoring.FundingHighPositive Then
            ls -= 2 : ss += 1
        ElseIf fr > cfg.Scoring.FundingLowPositive Then
            ls -= 1
        ElseIf fr < cfg.Scoring.FundingHighNegative Then
            ss -= 2 : ls += 1
        ElseIf fr < cfg.Scoring.FundingLowNegative Then
            ss -= 1
        End If
        ls = Math.Max(0, ls)
        ss = Math.Max(0, ss)

        ' -- Step 4: Regime Veto / Override -----------------------------------
        Dim effectiveLS As Integer = ls
        Dim effectiveSS As Integer = ss
        Dim adxPenalty As Integer = 0

        Select Case r.Regime
            Case "TRENDING_UP"
                If ss > ls Then
                    res.Verdict = "NO TRADE" : res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRENDING_DOWN"
                If ls > ss Then
                    res.Verdict = "NO TRADE" : res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRANSITIONAL"
                Dim penLow  As Double = cfg.RegimeGates.TransitionalAdxPenaltyLow
                Dim penMid  As Double = cfg.RegimeGates.TransitionalAdxPenaltyMid
                If r.ADX >= penLow AndAlso r.ADX < penMid Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyLow
                ElseIf r.ADX >= penMid AndAlso r.ADX < cfg.RegimeGates.TransitionalAdxPenaltyHigh Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyMid
                End If
                effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls))
                effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss))
        End Select

        ' -- Step 5: Generate Verdict (proportional thresholds from settings) -
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty
        ' res.MaxScore already set above

        Dim tStrong As Integer = ThresholdStrong(regimeMax, cfg.Scoring.VerdictStrongPct)
        Dim tMed    As Integer = ThresholdMed(regimeMax,    cfg.Scoring.VerdictMedPct)
        Dim tWeak   As Integer = ThresholdWeak(regimeMax,   cfg.Scoring.VerdictWeakPct)

        If effectiveLS >= tStrong Then
            res.Verdict = "STRONG LONG" : res.Confidence = "HIGH"
        ElseIf effectiveLS >= tMed Then
            res.Verdict = "LONG" : res.Confidence = "MEDIUM"
        ElseIf effectiveLS >= tWeak Then
            res.Verdict = "WEAK LONG" : res.Confidence = "LOW"
        ElseIf effectiveSS >= tStrong Then
            res.Verdict = "STRONG SHORT" : res.Confidence = "HIGH"
        ElseIf effectiveSS >= tMed Then
            res.Verdict = "SHORT" : res.Confidence = "MEDIUM"
        ElseIf effectiveSS >= tWeak Then
            res.Verdict = "WEAK SHORT" : res.Confidence = "LOW"
        Else
            res.Verdict = "NO TRADE" : res.Confidence = "N/A"
        End If

        ' -- Step 6: Hold / Exit Assessment -----------------------------------
        res.HoldStatus = CalcHoldStatus(r, posState)
        Return res
    End Function

    Private Shared Function TierFloor(rawScore As Integer) As Integer
        If rawScore >= 12 Then Return 9
        If rawScore >= 9 Then Return 6
        If rawScore >= 6 Then Return 3
        Return 0
    End Function

    Private Shared Sub AddFull(state As ScoreState, fullLong As Boolean, fullShort As Boolean, cat As SignalCategory)
        If fullLong Then
            state.LongScore += 1
            state.FullLongCategories.Add(cat)
        End If
        If fullShort Then
            state.ShortScore += 1
            state.FullShortCategories.Add(cat)
        End If
    End Sub

    Private Shared Function HasCrossConfirm(cats As HashSet(Of SignalCategory), ownCat As SignalCategory) As Boolean
        For Each c In cats
            If c <> ownCat Then Return True
        Next
        Return False
    End Function

    Private Shared Function BuildNote(baseNote As String,
                                      partialLong As Boolean,
                                      partialShort As Boolean,
                                      upgradedLong As Boolean,
                                      upgradedShort As Boolean) As String
        If upgradedLong Then Return baseNote & " | PARTIAL->UPGRADED [L]"
        If upgradedShort Then Return baseNote & " | PARTIAL->UPGRADED [S]"
        If partialLong Then Return baseNote & " | PARTIAL [L*]"
        If partialShort Then Return baseNote & " | PARTIAL [S*]"
        Return baseNote
    End Function

    Private Shared Function CalcHoldStatus(r As IndicatorResults, posState As PositionState) As String
        Select Case posState
            Case PositionState.InLong
                If r.ROC < 0 Then Return "EXIT -- momentum break (ROC crossed below 0)"
                If r.OBVDivergence = "BEARISH" Then Return "EXIT -- OBV bearish divergence"
                If r.RSIDivergence = "BEARISH" Then Return "EVALUATE -- RSI bearish divergence, watch for reversal"
                If r.ROC > 0.6 Then Return "TAKE PROFIT -- extreme momentum, tighten stops"
                If r.RSI > 60 Then Return "HOLD -- momentum intact"
                If r.RSI >= 40 Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI < 40)"
            Case PositionState.InShort
                If r.ROC > 0 Then Return "EXIT -- momentum break (ROC crossed above 0)"
                If r.OBVDivergence = "BULLISH" Then Return "EXIT -- OBV bullish divergence"
                If r.RSIDivergence = "BULLISH" Then Return "EVALUATE -- RSI bullish divergence, watch for reversal"
                If r.ROC < -0.6 Then Return "TAKE PROFIT -- extreme bearish momentum, tighten stops"
                If r.RSI < 40 Then Return "HOLD -- bearish momentum intact"
                If r.RSI <= 60 Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI > 60)"
            Case Else
                Return "N/A -- no open position"
        End Select
    End Function

End Class
