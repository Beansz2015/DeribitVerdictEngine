' ScoringEngine.vb  v0.24
' Implements the 6-step verdict engine from the specification.
' Input: IndicatorResults + DynamicNorms + position state. Output: VerdictResult.
' v0.24: All hardcoded thresholds replaced with SettingsLoader.Current references.

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

    ' Max achievable score after removing non-directional padding points
    Public Const MaxScore As Integer = 13

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState,
                                     norms As DynamicNorms) As VerdictResult
        Dim cfg = SettingsLoader.Current
        Dim sc  = cfg.Scoring
        Dim ind = cfg.Indicators
        Dim rg  = cfg.RegimeGates

        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim state As New ScoreState()

        ' -- Step 2: Weighted Signal Scoring ----------------------------------

        ' CORE
        ' ROC (Momentum)
        Dim rocSlope As Double = ind.ROC.SlopeSensitivity
        Dim rocLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        Dim rocPartialLong As Boolean = r.ROC > rocSlope AndAlso r.ROCSlope <> "RISING"
        Dim rocPartialShort As Boolean = r.ROC < -rocSlope AndAlso r.ROCSlope <> "FALLING"
        AddFull(state, rocLong, rocShort, SignalCategory.Momentum, sc.Weights.ROC)

        ' RSI (Momentum)
        Dim rsiLong As Boolean = r.RSI > ind.RSI.Overbought
        Dim rsiShort As Boolean = r.RSI < ind.RSI.Oversold
        Dim rsiPartialLong As Boolean = r.RSI > ind.RSI.PartialOverbought AndAlso r.RSI <= ind.RSI.Overbought
        Dim rsiPartialShort As Boolean = r.RSI < ind.RSI.PartialOversold AndAlso r.RSI >= ind.RSI.Oversold
        AddFull(state, rsiLong, rsiShort, SignalCategory.Momentum, sc.Weights.RSI)

        ' DMI (MarketStructure)
        Dim dmiLong As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        AddFull(state, dmiLong, dmiShort, SignalCategory.MarketStructure, sc.Weights.DMI)

        ' ADX (MarketStructure)
        Dim adxLong As Boolean = r.ADX > ind.ADX.TrendThreshold AndAlso dmiLong
        Dim adxShort As Boolean = r.ADX > ind.ADX.TrendThreshold AndAlso dmiShort
        AddFull(state, adxLong, adxShort, SignalCategory.MarketStructure, sc.Weights.ADX)

        ' Volume (Volume) -- thresholds from DynamicNorms
        Dim volHigh As Double = norms.VolHighThreshold
        Dim volMid As Double = norms.VolMidThreshold
        Dim volLong As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC > 0 AndAlso r.CurrentPrice > r.VWAP
        Dim volShort As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC < 0 AndAlso r.CurrentPrice < r.VWAP
        Dim volPartial As Boolean = r.VolumeRatio >= volMid AndAlso r.VolumeRatio < volHigh
        AddFull(state, volLong, volShort, SignalCategory.Volume, sc.Weights.Volume)

        ' TIER 1
        ' VWAP (Microstructure) -- deviation boundary from DynamicNorms
        Dim vwapDev As Double = norms.VWAPDevThreshold
        Dim vwapLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= vwapDev
        Dim vwapShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= vwapDev
        Dim vwapPartialLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > vwapDev
        Dim vwapPartialShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > vwapDev
        AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure, sc.Weights.VWAP)

        ' BBW Squeeze-State Scoring
        Dim bbwLongHit As Boolean = False
        Dim bbwShortHit As Boolean = False
        Dim bbwNote As String
        Dim bbwRocThr As Double = ind.BBW.ReleasingRocThreshold

        Select Case r.SqueezeStatus
            Case "ACTIVE"
                state.LongScore  = Math.Max(0, state.LongScore  - sc.Weights.BBW)
                state.ShortScore = Math.Max(0, state.ShortScore - sc.Weights.BBW)
                bbwNote = String.Format("{0:F3} | ACTIVE -- penalty -{1} both sides", r.BBW, sc.Weights.BBW)
            Case "RELEASING"
                If r.ROC > bbwRocThr Then
                    state.LongScore += sc.Weights.BBW
                    state.FullLongCategories.Add(SignalCategory.Microstructure)
                    bbwLongHit = True
                    bbwNote = String.Format("{0:F3} | RELEASING -- breakout [L] (ROC {1:F3})", r.BBW, r.ROC)
                ElseIf r.ROC < -bbwRocThr Then
                    state.ShortScore += sc.Weights.BBW
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
        AddFull(state, emaBull, emaBear, SignalCategory.MarketStructure, sc.Weights.EMA)

        ' OI (Microstructure)
        Dim oiLong As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort As Boolean = r.OISignal = "NEW SHORTS"
        Dim oiPartialLong As Boolean = r.OISignal = "COVERING"
        Dim oiPartialShort As Boolean = r.OISignal = "CAPITULATION"
        AddFull(state, oiLong, oiShort, SignalCategory.Microstructure, sc.Weights.OI)

        ' TIER 2
        ' OFI (Microstructure)
        Dim ofiBuy As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure, sc.Weights.OFI)

        ' Liquidations -- penalty-only, scaled by size
        Dim liqLongPenalty As Integer = 0
        Dim liqShortPenalty As Integer = 0
        Dim liqCfg = ind.Liquidations
        If r.LiqSignal = "LONG LIQS" Then
            liqLongPenalty = If(r.LiqLongSize > liqCfg.LargeLiqSize, sc.Weights.LiqPenalty + 1, sc.Weights.LiqPenalty)
            state.LongScore = Math.Max(0, state.LongScore - liqLongPenalty)
        ElseIf r.LiqSignal = "SHORT LIQS" Then
            liqShortPenalty = If(r.LiqShortSize > liqCfg.LargeLiqSize, sc.Weights.LiqPenalty + 1, sc.Weights.LiqPenalty)
            state.ShortScore = Math.Max(0, state.ShortScore - liqShortPenalty)
        End If

        ' 5m EMA200 (MarketStructure)
        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        AddFull(state, ema200Bull, ema200Bear, SignalCategory.MarketStructure, sc.Weights.EMA200)

        ' TIER 3
        ' Donchian (MarketStructure)
        Dim donchLong As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort As Boolean = r.DonchianSignal = "SHORT"
        AddFull(state, donchLong, donchShort, SignalCategory.MarketStructure, sc.Weights.Donchian)

        ' OBV (Volume)
        Dim obvLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "NONE"
        Dim obvShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "NONE"
        Dim obvPartialLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "BEARISH"
        Dim obvPartialShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "BULLISH"
        AddFull(state, obvLong, obvShort, SignalCategory.Volume, sc.Weights.OBV)

        ' Pass 2: upgrade partials with cross-category full confirmation
        Dim rocLongUpgraded As Boolean = rocPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rocShortUpgraded As Boolean = rocPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rocLongUpgraded Then state.LongScore += sc.Weights.ROC
        If rocShortUpgraded Then state.ShortScore += sc.Weights.ROC

        Dim rsiLongUpgraded As Boolean = rsiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rsiShortUpgraded As Boolean = rsiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rsiLongUpgraded Then state.LongScore += sc.Weights.RSI
        If rsiShortUpgraded Then state.ShortScore += sc.Weights.RSI

        Dim vwapLongUpgraded As Boolean = vwapPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim vwapShortUpgraded As Boolean = vwapPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If vwapLongUpgraded Then state.LongScore += sc.Weights.VWAP
        If vwapShortUpgraded Then state.ShortScore += sc.Weights.VWAP

        Dim oiLongUpgraded As Boolean = oiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += sc.Weights.OI
        If oiShortUpgraded Then state.ShortScore += sc.Weights.OI

        Dim obvLongUpgraded As Boolean = obvPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += sc.Weights.OBV
        If obvShortUpgraded Then state.ShortScore += sc.Weights.OBV

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
            BuildNote(String.Format("Dev:{0:F2}% | thr ±{1:F2}% [{2}]", r.VWAPDevPct, vwapDev, normMode),
                      vwapPartialLong AndAlso Not vwapLongUpgraded, vwapPartialShort AndAlso Not vwapShortUpgraded,
                      vwapLongUpgraded, vwapShortUpgraded)))

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
        If fr > sc.FundingHighPositive Then
            ls -= 2 : ss += 1
        ElseIf fr > sc.FundingLowPositive Then
            ls -= 1
        ElseIf fr < sc.FundingHighNegative Then
            ss -= 2 : ls += 1
        ElseIf fr < sc.FundingLowNegative Then
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
                If rg.SuppressShortInTrendingUp AndAlso ss > ls Then
                    res.Verdict = "NO TRADE" : res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRENDING_DOWN"
                If rg.SuppressLongInTrendingDown AndAlso ls > ss Then
                    res.Verdict = "NO TRADE" : res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRANSITIONAL"
                If sc.TransitionalPenaltyEnabled Then
                    If r.ADX >= rg.TransitionalAdxPenaltyLow AndAlso r.ADX < rg.TransitionalAdxPenaltyMid Then
                        adxPenalty = rg.TransitionalPenaltyLow
                    ElseIf r.ADX >= rg.TransitionalAdxPenaltyMid AndAlso r.ADX < rg.TransitionalAdxPenaltyHigh Then
                        adxPenalty = rg.TransitionalPenaltyMid
                    End If
                    effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls))
                    effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss))
                End If
        End Select

        ' -- Step 5: Generate Verdict -----------------------------------------
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty

        If effectiveLS >= sc.StrongLongThreshold Then
            res.Verdict = "STRONG LONG" : res.Confidence = "HIGH"
        ElseIf effectiveLS >= sc.MediumLongThreshold Then
            res.Verdict = "LONG" : res.Confidence = "MEDIUM"
        ElseIf effectiveLS >= sc.LongThreshold Then
            res.Verdict = "WEAK LONG" : res.Confidence = "LOW"
        ElseIf effectiveSS >= sc.StrongShortThreshold Then
            res.Verdict = "STRONG SHORT" : res.Confidence = "HIGH"
        ElseIf effectiveSS >= sc.MediumShortThreshold Then
            res.Verdict = "SHORT" : res.Confidence = "MEDIUM"
        ElseIf effectiveSS >= sc.ShortThreshold Then
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

    Private Shared Sub AddFull(state As ScoreState, fullLong As Boolean, fullShort As Boolean,
                               cat As SignalCategory, weight As Integer)
        If fullLong Then
            state.LongScore += weight
            state.FullLongCategories.Add(cat)
        End If
        If fullShort Then
            state.ShortScore += weight
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
        Dim ind = SettingsLoader.Current.Indicators
        Select Case posState
            Case PositionState.InLong
                If r.ROC < 0 Then Return "EXIT -- momentum break (ROC crossed below 0)"
                If r.OBVDivergence = "BEARISH" Then Return "EXIT -- OBV bearish divergence"
                If r.RSIDivergence = "BEARISH" Then Return "EVALUATE -- RSI bearish divergence, watch for reversal"
                If r.ROC > 0.6 Then Return "TAKE PROFIT -- extreme momentum, tighten stops"
                If r.RSI > ind.RSI.Overbought Then Return "HOLD -- momentum intact"
                If r.RSI >= ind.RSI.Oversold Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI < " & ind.RSI.Oversold.ToString("F0") & ")"
            Case PositionState.InShort
                If r.ROC > 0 Then Return "EXIT -- momentum break (ROC crossed above 0)"
                If r.OBVDivergence = "BULLISH" Then Return "EXIT -- OBV bullish divergence"
                If r.RSIDivergence = "BULLISH" Then Return "EVALUATE -- RSI bullish divergence, watch for reversal"
                If r.ROC < -0.6 Then Return "TAKE PROFIT -- extreme bearish momentum, tighten stops"
                If r.RSI < ind.RSI.Oversold Then Return "HOLD -- bearish momentum intact"
                If r.RSI <= ind.RSI.Overbought Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI > " & ind.RSI.Overbought.ToString("F0") & ")"
            Case Else
                Return "N/A -- no open position"
        End Select
    End Function

End Class
