' ScoringEngine.vb
' Implements the 6-step verdict engine from the specification.
' Input: IndicatorResults + position state. Output: VerdictResult.

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

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState) As VerdictResult
        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim state As New ScoreState()

        ' -- Step 2: Weighted Signal Scoring ----------------------------------
        ' Pass 1: score full signals, track categories.
        ' Pass 2: upgrade partials only if a full signal from a DIFFERENT
        '         category exists in the same direction.

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

        ' Volume (Volume) -- direction-neutral
        Dim volSpike As Boolean = r.VolumeRatio >= 3.0
        Dim volPartial As Boolean = r.VolumeRatio >= 2.0 AndAlso r.VolumeRatio < 3.0
        If volSpike Then
            state.LongScore += 1
            state.ShortScore += 1
            state.FullLongCategories.Add(SignalCategory.Volume)
            state.FullShortCategories.Add(SignalCategory.Volume)
        End If

        ' TIER 1
        ' VWAP (Microstructure)
        Dim vwapLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= 1.5
        Dim vwapShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= 1.5
        Dim vwapPartialLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > 1.5
        Dim vwapPartialShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > 1.5
        AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure)

        ' BBW (Microstructure) -- direction-neutral
        Dim bbwFull As Boolean = r.SqueezeStatus = "NONE"
        Dim bbwPartial As Boolean = r.SqueezeStatus = "RELEASING"
        If bbwFull Then
            state.LongScore += 1
            state.ShortScore += 1
            state.FullLongCategories.Add(SignalCategory.Microstructure)
            state.FullShortCategories.Add(SignalCategory.Microstructure)
        End If

        ' EMA Ribbon (MarketStructure)
        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        AddFull(state, emaBull, emaBear, SignalCategory.MarketStructure)

        ' Funding (Microstructure)
        Dim fundOkLong As Boolean = r.FundingRate <= 0.0005
        Dim fundOkShort As Boolean = r.FundingRate >= -0.0005
        AddFull(state, fundOkLong, fundOkShort, SignalCategory.Microstructure)

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

        ' Liquidations (Microstructure)
        Dim noLongLiq As Boolean = r.LiqSignal <> "LONG LIQS"
        Dim noShortLiq As Boolean = r.LiqSignal <> "SHORT LIQS"
        AddFull(state, noLongLiq, noShortLiq, SignalCategory.Microstructure)

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

        Dim volLongUpgraded As Boolean = volPartial AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim volShortUpgraded As Boolean = volPartial AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If volLongUpgraded Then state.LongScore += 1
        If volShortUpgraded Then state.ShortScore += 1

        Dim vwapLongUpgraded As Boolean = vwapPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim vwapShortUpgraded As Boolean = vwapPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If vwapLongUpgraded Then state.LongScore += 1
        If vwapShortUpgraded Then state.ShortScore += 1

        Dim bbwLongUpgraded As Boolean = bbwPartial AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim bbwShortUpgraded As Boolean = bbwPartial AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If bbwLongUpgraded Then state.LongScore += 1
        If bbwShortUpgraded Then state.ShortScore += 1

        Dim oiLongUpgraded As Boolean = oiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += 1
        If oiShortUpgraded Then state.ShortScore += 1

        Dim obvLongUpgraded As Boolean = obvPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += 1
        If obvShortUpgraded Then state.ShortScore += 1

        ' Breakdown (rendered after all scores finalised)
        breakdown.Add(New SignalBreakdownItem("ROC(9)", rocLong OrElse rocLongUpgraded, rocShort OrElse rocShortUpgraded,
            BuildNote(String.Format("{0:F3} | Slope: {1}", r.ROC, r.ROCSlope),
                      rocPartialLong AndAlso Not rocLongUpgraded, rocPartialShort AndAlso Not rocShortUpgraded,
                      rocLongUpgraded, rocShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("RSI(9)", rsiLong OrElse rsiLongUpgraded, rsiShort OrElse rsiShortUpgraded,
            BuildNote(String.Format("{0:F1}", r.RSI),
                      rsiPartialLong AndAlso Not rsiLongUpgraded, rsiPartialShort AndAlso Not rsiShortUpgraded,
                      rsiLongUpgraded, rsiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("DMI +/-DI", dmiLong, dmiShort,
            String.Format("+DI:{0:F1} -DI:{1:F1}", r.PlusDI, r.MinusDI)))

        breakdown.Add(New SignalBreakdownItem("ADX>25", adxLong, adxShort,
            String.Format("{0:F1}", r.ADX)))

        breakdown.Add(New SignalBreakdownItem("Volume >=3xSMA", volSpike OrElse volLongUpgraded, volSpike OrElse volShortUpgraded,
            BuildNote(r.VolumeRatio.ToString("F2") & "x",
                      volPartial AndAlso Not volLongUpgraded, volPartial AndAlso Not volShortUpgraded,
                      volLongUpgraded, volShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("VWAP", vwapLong OrElse vwapLongUpgraded, vwapShort OrElse vwapShortUpgraded,
            BuildNote(String.Format("VWAP:{0:F1} Dev:{1:F2}%", r.VWAP, r.VWAPDevPct),
                      vwapPartialLong AndAlso Not vwapLongUpgraded, vwapPartialShort AndAlso Not vwapShortUpgraded,
                      vwapLongUpgraded, vwapShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("BBW Squeeze", bbwFull OrElse bbwLongUpgraded, bbwFull OrElse bbwShortUpgraded,
            BuildNote(String.Format("{0:F3} | {1}", r.BBW, r.SqueezeStatus),
                      bbwPartial AndAlso Not bbwLongUpgraded, bbwPartial AndAlso Not bbwShortUpgraded,
                      bbwLongUpgraded, bbwShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("EMA 9/21/50", emaBull, emaBear,
            String.Format("9:{0:F0} 21:{1:F0} 50:{2:F0} | {3}", r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment)))

        breakdown.Add(New SignalBreakdownItem("Funding OK", fundOkLong, fundOkShort,
            String.Format("{0:F4}% | {1}", r.FundingRate * 100, r.FundingBias)))

        breakdown.Add(New SignalBreakdownItem("OI Delta", oiLong OrElse oiLongUpgraded, oiShort OrElse oiShortUpgraded,
            BuildNote(String.Format("15m:{0:F2}% 60m:{1:F2}% | {2}", r.OIChange15m, r.OIChange60m, r.OISignal),
                      oiPartialLong AndAlso Not oiLongUpgraded, oiPartialShort AndAlso Not oiShortUpgraded,
                      oiLongUpgraded, oiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("OFI", ofiBuy, ofiSell,
            String.Format("Ratio:{0:F2} | {1}", r.OFIRatio, r.OFISignal)))

        breakdown.Add(New SignalBreakdownItem("No Adverse Liq", noLongLiq, noShortLiq,
            String.Format("L:{0:F0} S:{1:F0} | {2}", r.LiqLongSize, r.LiqShortSize, r.LiqSignal)))

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
        If fr > 0.001 Then
            ls -= 2 : ss += 1
        ElseIf fr > 0.0005 Then
            ls -= 1
        ElseIf fr < -0.001 Then
            ss -= 2 : ls += 1
        ElseIf fr < -0.0005 Then
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
                ' ADX-proximity penalty: deeper in TRANSITIONAL = higher penalty
                If r.ADX >= 20.0 AndAlso r.ADX < 22.5 Then
                    adxPenalty = 2
                ElseIf r.ADX >= 22.5 AndAlso r.ADX < 25.0 Then
                    adxPenalty = 1
                End If

                ' Apply penalty, capped so score cannot drop below floor of current tier
                effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls))
                effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss))
        End Select

        ' -- Step 5: Generate Verdict -----------------------------------------
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty

        If effectiveLS >= 12 Then
            res.Verdict = "STRONG LONG" : res.Confidence = "HIGH"
        ElseIf effectiveLS >= 9 Then
            res.Verdict = "LONG" : res.Confidence = "MEDIUM"
        ElseIf effectiveLS >= 6 Then
            res.Verdict = "WEAK LONG" : res.Confidence = "LOW"
        ElseIf effectiveSS >= 12 Then
            res.Verdict = "STRONG SHORT" : res.Confidence = "HIGH"
        ElseIf effectiveSS >= 9 Then
            res.Verdict = "SHORT" : res.Confidence = "MEDIUM"
        ElseIf effectiveSS >= 6 Then
            res.Verdict = "WEAK SHORT" : res.Confidence = "LOW"
        Else
            res.Verdict = "NO TRADE" : res.Confidence = "N/A"
        End If

        ' -- Step 6: Hold / Exit Assessment -----------------------------------
        res.HoldStatus = CalcHoldStatus(r, posState)
        Return res
    End Function

    ' Returns the bottom boundary of the tier that rawScore currently sits in.
    ' Prevents a penalty from skipping an entire tier in one step.
    Private Shared Function TierFloor(rawScore As Integer) As Integer
        If rawScore >= 12 Then Return 9   ' STRONG tier floor
        If rawScore >= 9 Then Return 6    ' MEDIUM tier floor
        If rawScore >= 6 Then Return 3    ' WEAK tier floor (caps at NO TRADE entry)
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
                If r.OBVDivergence = "BEARISH" Then Return "EXIT -- RSI/OBV bearish divergence"
                If r.ROC > 0.6 Then Return "TAKE PROFIT -- extreme momentum, tighten stops"
                If r.RSI > 60 Then Return "HOLD -- momentum intact"
                If r.RSI >= 40 Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI < 40)"
            Case PositionState.InShort
                If r.ROC > 0 Then Return "EXIT -- momentum break (ROC crossed above 0)"
                If r.OBVDivergence = "BULLISH" Then Return "EXIT -- OBV bullish divergence"
                If r.ROC < -0.6 Then Return "TAKE PROFIT -- extreme bearish momentum, tighten stops"
                If r.RSI < 40 Then Return "HOLD -- bearish momentum intact"
                If r.RSI <= 60 Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI > 60)"
            Case Else
                Return "N/A -- no open position"
        End Select
    End Function

End Class
