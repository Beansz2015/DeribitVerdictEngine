' ScoringEngine.vb
' Implements the 6-step verdict engine from the specification.
' Input: IndicatorResults + position state. Output: VerdictResult.

Public Class VerdictResult
    Public Property LongScore As Integer
    Public Property ShortScore As Integer
    Public Property Verdict As String
    Public Property Confidence As String
    Public Property HoldStatus As String
    Public Property SignalBreakdown As New List(Of (Label As String, LongHit As Boolean, ShortHit As Boolean, Note As String))
End Class

Public Enum PositionState
    None
    InLong
    InShort
End Enum

Public Class ScoringEngine

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState) As VerdictResult
        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim ls As Integer = 0
        Dim ss As Integer = 0

        ' CORE signals (max 5 each)

        ' ROC
        Dim rocLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        If rocLong Then ls += 1
        If rocShort Then ss += 1
        breakdown.Add(("ROC(9)", rocLong, rocShort, r.ROC.ToString("F3") & " | Slope: " & r.ROCSlope))

        ' RSI
        Dim rsiLong As Boolean = r.RSI > 50
        Dim rsiShort As Boolean = r.RSI < 50
        If rsiLong Then ls += 1
        If rsiShort Then ss += 1
        breakdown.Add(("RSI(9)", rsiLong, rsiShort, r.RSI.ToString("F1")))

        ' DMI +DI vs -DI
        Dim dmiLong As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        If dmiLong Then ls += 1
        If dmiShort Then ss += 1
        breakdown.Add(("DMI +/-DI", dmiLong, dmiShort, "+DI:" & r.PlusDI.ToString("F1") & " -DI:" & r.MinusDI.ToString("F1")))

        ' ADX strength
        Dim adxStrong As Boolean = r.ADX > 25
        If adxStrong AndAlso dmiLong Then ls += 1
        If adxStrong AndAlso dmiShort Then ss += 1
        breakdown.Add(("ADX>25", adxStrong AndAlso dmiLong, adxStrong AndAlso dmiShort, r.ADX.ToString("F1")))

        ' Volume spike (only 3x qualifies for score per spec)
        Dim volSpike As Boolean = r.VolumeRatio >= 3.0
        If volSpike Then
            ls += 1
            ss += 1
        End If
        breakdown.Add(("Volume >=3xSMA", volSpike, volSpike, r.VolumeRatio.ToString("F2") & "x"))

        ' TIER 1 (max 5 each)

        ' VWAP
        Dim vwapLong As Boolean = r.CurrentPrice > r.VWAP
        Dim vwapShort As Boolean = r.CurrentPrice < r.VWAP
        If vwapLong Then ls += 1
        If vwapShort Then ss += 1
        breakdown.Add(("VWAP", vwapLong, vwapShort, "VWAP:" & r.VWAP.ToString("F1") & " Dev:" & r.VWAPDevPct.ToString("F2") & "%"))

        ' BBW squeeze (no active squeeze = ok for both directions)
        Dim bbwOk As Boolean = r.SqueezeStatus <> "ACTIVE"
        If bbwOk Then ls += 1 : ss += 1
        breakdown.Add(("BBW Squeeze", bbwOk, bbwOk, r.BBW.ToString("F3") & " | " & r.SqueezeStatus))

        ' EMA Ribbon
        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        If emaBull Then ls += 1
        If emaBear Then ss += 1
        breakdown.Add(("EMA 9/21/50", emaBull, emaBear, "9:" & r.EMA9.ToString("F0") & " 21:" & r.EMA21.ToString("F0") & " 50:" & r.EMA50.ToString("F0") & " | " & r.EMAAlignment))

        ' Funding rate
        Dim fundOkLong As Boolean = r.FundingRate <= 0.0005
        Dim fundOkShort As Boolean = r.FundingRate >= -0.0005
        If fundOkLong Then ls += 1
        If fundOkShort Then ss += 1
        breakdown.Add(("Funding OK", fundOkLong, fundOkShort, (r.FundingRate * 100).ToString("F4") & "% | " & r.FundingBias))

        ' OI signal
        Dim oiLong As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort As Boolean = r.OISignal = "NEW SHORTS"
        If oiLong Then ls += 1
        If oiShort Then ss += 1
        breakdown.Add(("OI Delta", oiLong, oiShort, "15m:" & r.OIChange15m.ToString("F2") & "% 60m:" & r.OIChange60m.ToString("F2") & "% | " & r.OISignal))

        ' TIER 2 (max 3 each)

        ' Order flow imbalance
        Dim ofiBuy As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        If ofiBuy Then ls += 1
        If ofiSell Then ss += 1
        breakdown.Add(("OFI", ofiBuy, ofiSell, "Ratio:" & r.OFIRatio.ToString("F2") & " | " & r.OFISignal))

        ' Liquidations
        Dim noLongLiq As Boolean = r.LiqSignal <> "LONG LIQS"
        Dim noShortLiq As Boolean = r.LiqSignal <> "SHORT LIQS"
        If noLongLiq Then ls += 1
        If noShortLiq Then ss += 1
        breakdown.Add(("No Adverse Liq", noLongLiq, noShortLiq, "L:" & r.LiqLongSize.ToString("F0") & " S:" & r.LiqShortSize.ToString("F0") & " | " & r.LiqSignal))

        ' 5m EMA200
        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        If ema200Bull Then ls += 1
        If ema200Bear Then ss += 1
        breakdown.Add(("5m EMA(200)", ema200Bull, ema200Bear, r.EMA200_5m.ToString("F0") & " | " & r.PriceVsEMA200))

        ' TIER 3 (max 2 each)

        ' Donchian breakout
        Dim donchLong As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort As Boolean = r.DonchianSignal = "SHORT"
        If donchLong Then ls += 1
        If donchShort Then ss += 1
        breakdown.Add(("Donchian(20)", donchLong, donchShort, "U:" & r.DonchianUpper.ToString("F0") & " L:" & r.DonchianLower.ToString("F0") & " | " & r.DonchianSignal))

        ' OBV
        Dim obvLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence <> "BEARISH"
        Dim obvShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence <> "BULLISH"
        If obvLong Then ls += 1
        If obvShort Then ss += 1
        breakdown.Add(("OBV", obvLong, obvShort, "Trend:" & r.OBVTrend & " Div:" & r.OBVDivergence))

        ' Step 3: Funding Rate Confidence Modifier
        Dim fr As Double = r.FundingRate
        If fr > 0.001 Then
            ls -= 2
            ss += 1
        ElseIf fr > 0.0005 Then
            ls -= 1
        ElseIf fr < -0.001 Then
            ss -= 2
            ls += 1
        ElseIf fr < -0.0005 Then
            ss -= 1
        End If

        ls = Math.Max(0, ls)
        ss = Math.Max(0, ss)

        ' Step 4: Regime Veto / Override
        Dim effectiveLS As Integer = ls
        Dim effectiveSS As Integer = ss

        Select Case r.Regime
            Case "TRENDING_UP"
                If ss > ls Then
                    res.Verdict = "NO TRADE"
                    res.Confidence = "N/A"
                    res.LongScore = ls
                    res.ShortScore = ss
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRENDING_DOWN"
                If ls > ss Then
                    res.Verdict = "NO TRADE"
                    res.Confidence = "N/A"
                    res.LongScore = ls
                    res.ShortScore = ss
                    res.HoldStatus = CalcHoldStatus(r, posState)
                    Return res
                End If
            Case "TRANSITIONAL"
                effectiveLS = Math.Max(0, effectiveLS - 2)
                effectiveSS = Math.Max(0, effectiveSS - 2)
        End Select

        ' Step 5: Generate Verdict
        res.LongScore = ls
        res.ShortScore = ss

        If effectiveLS >= 12 Then
            res.Verdict = "STRONG LONG"
            res.Confidence = "HIGH"
        ElseIf effectiveLS >= 9 Then
            res.Verdict = "LONG"
            res.Confidence = "MEDIUM"
        ElseIf effectiveLS >= 6 Then
            res.Verdict = "WEAK LONG"
            res.Confidence = "LOW"
        ElseIf effectiveSS >= 12 Then
            res.Verdict = "STRONG SHORT"
            res.Confidence = "HIGH"
        ElseIf effectiveSS >= 9 Then
            res.Verdict = "SHORT"
            res.Confidence = "MEDIUM"
        ElseIf effectiveSS >= 6 Then
            res.Verdict = "WEAK SHORT"
            res.Confidence = "LOW"
        Else
            res.Verdict = "NO TRADE"
            res.Confidence = "N/A"
        End If

        ' Step 6: Hold / Exit Assessment
        res.HoldStatus = CalcHoldStatus(r, posState)

        Return res
    End Function

    Private Shared Function CalcHoldStatus(r As IndicatorResults, posState As PositionState) As String
        Select Case posState
            Case PositionState.InLong
                If r.ROC < 0 Then Return "EXIT - momentum break (ROC crossed below 0)"
                If r.OBVDivergence = "BEARISH" Then Return "EXIT - OBV bearish divergence"
                If r.ROC > 0.6 Then Return "TAKE PROFIT - extreme momentum, tighten stops"
                If r.RSI > 60 Then Return "HOLD - momentum intact"
                If r.RSI >= 40 Then Return "EVALUATE - momentum weakening, consider scaling out"
                Return "EXIT - retracement too deep (RSI < 40)"

            Case PositionState.InShort
                If r.ROC > 0 Then Return "EXIT - momentum break (ROC crossed above 0)"
                If r.OBVDivergence = "BULLISH" Then Return "EXIT - OBV bullish divergence"
                If r.ROC < -0.6 Then Return "TAKE PROFIT - extreme bearish momentum, tighten stops"
                If r.RSI < 40 Then Return "HOLD - bearish momentum intact"
                If r.RSI <= 60 Then Return "EVALUATE - momentum weakening, consider scaling out"
                Return "EXIT - retracement too deep (RSI > 60)"

            Case Else
                Return "N/A - no open position"
        End Select
    End Function

End Class
