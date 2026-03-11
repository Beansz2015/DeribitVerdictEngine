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

    Private Enum SignalCategory
        Momentum
        Volume
        Structure
        Microstructure
    End Enum

    Private Class ScoreState
        Public Property FullLongCategories As New HashSet(Of SignalCategory)
        Public Property FullShortCategories As New HashSet(Of SignalCategory)
        Public Property LongScore As Integer
        Public Property ShortScore As Integer
    End Class

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState) As VerdictResult
        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim state As New ScoreState()

        ' ── Step 2: Weighted Signal Scoring ──────────────────────────────────
        ' Pass 1 = full signals only. Partials are tracked and upgraded only if
        ' a full signal from a DIFFERENT category exists in the same direction.

        ' CORE (max 5 each)
        ' ROC (Momentum)
        Dim rocLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        Dim rocPartialLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope <> "RISING"
        Dim rocPartialShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope <> "FALLING"
        If Math.Abs(r.ROC) <= 0.1 Then
            rocPartialLong = False : rocPartialShort = False
        End If
        AddFull(state, rocLong, rocShort, SignalCategory.Momentum)

        ' RSI (Momentum)
        Dim rsiLong As Boolean = r.RSI > 60
        Dim rsiShort As Boolean = r.RSI < 40
        Dim rsiPartialLong As Boolean = r.RSI > 50 AndAlso r.RSI <= 60
        Dim rsiPartialShort As Boolean = r.RSI < 50 AndAlso r.RSI >= 40
        AddFull(state, rsiLong, rsiShort, SignalCategory.Momentum)

        ' DMI +DI vs -DI (Structure)
        Dim dmiLong As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        AddFull(state, dmiLong, dmiShort, SignalCategory.Structure)

        ' ADX strength (Structure)
        Dim adxLong As Boolean = r.ADX > 25 AndAlso dmiLong
        Dim adxShort As Boolean = r.ADX > 25 AndAlso dmiShort
        AddFull(state, adxLong, adxShort, SignalCategory.Structure)

        ' Volume spike (Volume)
        Dim volSpike As Boolean = r.VolumeRatio >= 3.0
        Dim volPartial As Boolean = r.VolumeRatio >= 2.0 AndAlso r.VolumeRatio < 3.0
        If volSpike Then
            state.LongScore += 1
            state.ShortScore += 1
            state.FullLongCategories.Add(SignalCategory.Volume)
            state.FullShortCategories.Add(SignalCategory.Volume)
        End If

        ' TIER 1 (max 5 each)
        ' VWAP (Microstructure)
        Dim vwapLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= 1.5
        Dim vwapShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= 1.5
        Dim vwapPartialLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > 1.5
        Dim vwapPartialShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > 1.5
        AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure)

        ' BBW (Microstructure)
        Dim bbwFull As Boolean = r.SqueezeStatus = "NONE"
        Dim bbwPartial As Boolean = r.SqueezeStatus = "RELEASING"
        If bbwFull Then
            state.LongScore += 1
            state.ShortScore += 1
            state.FullLongCategories.Add(SignalCategory.Microstructure)
            state.FullShortCategories.Add(SignalCategory.Microstructure)
        End If

        ' EMA Ribbon (Structure)
        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        AddFull(state, emaBull, emaBear, SignalCategory.Structure)

        ' Funding rate (Microstructure)
        Dim fundOkLong As Boolean = r.FundingRate <= 0.0005
        Dim fundOkShort As Boolean = r.FundingRate >= -0.0005
        AddFull(state, fundOkLong, fundOkShort, SignalCategory.Microstructure)

        ' OI signal (Microstructure)
        Dim oiLong As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort As Boolean = r.OISignal = "NEW SHORTS"
        Dim oiPartialLong As Boolean = r.OISignal = "COVERING"
        Dim oiPartialShort As Boolean = r.OISignal = "CAPITULATION"
        AddFull(state, oiLong, oiShort, SignalCategory.Microstructure)

        ' TIER 2 (max 3 each)
        ' Order flow imbalance (Microstructure)
        Dim ofiBuy As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure)

        ' Liquidation (Microstructure)
        Dim noLongLiq As Boolean = r.LiqSignal <> "LONG LIQS"
        Dim noShortLiq As Boolean = r.LiqSignal <> "SHORT LIQS"
        AddFull(state, noLongLiq, noShortLiq, SignalCategory.Microstructure)

        ' 5m EMA200 (Structure)
        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        AddFull(state, ema200Bull, ema200Bear, SignalCategory.Structure)

        ' TIER 3 (max 2 each)
        ' Donchian breakout (Structure)
        Dim donchLong As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort As Boolean = r.DonchianSignal = "SHORT"
        AddFull(state, donchLong, donchShort, SignalCategory.Structure)

        ' OBV (Volume)
        Dim obvLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "NONE"
        Dim obvShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "NONE"
        Dim obvPartialLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "BEARISH"
        Dim obvPartialShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "BULLISH"
        AddFull(state, obvLong, obvShort, SignalCategory.Volume)

        ' Pass 2 = upgrade partials only if confirmed by a full signal from a
        ' DIFFERENT category in the same direction.
        Dim rocLongUpgraded As Boolean = rocPartialLong AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Momentum)
        Dim rocShortUpgraded As Boolean = rocPartialShort AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Momentum)
        If rocLongUpgraded Then state.LongScore += 1
        If rocShortUpgraded Then state.ShortScore += 1

        Dim rsiLongUpgraded As Boolean = rsiPartialLong AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Momentum)
        Dim rsiShortUpgraded As Boolean = rsiPartialShort AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Momentum)
        If rsiLongUpgraded Then state.LongScore += 1
        If rsiShortUpgraded Then state.ShortScore += 1

        Dim volLongUpgraded As Boolean = volPartial AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Volume)
        Dim volShortUpgraded As Boolean = volPartial AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Volume)
        If volLongUpgraded Then state.LongScore += 1
        If volShortUpgraded Then state.ShortScore += 1

        Dim vwapLongUpgraded As Boolean = vwapPartialLong AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Microstructure)
        Dim vwapShortUpgraded As Boolean = vwapPartialShort AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Microstructure)
        If vwapLongUpgraded Then state.LongScore += 1
        If vwapShortUpgraded Then state.ShortScore += 1

        Dim bbwLongUpgraded As Boolean = bbwPartial AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Microstructure)
        Dim bbwShortUpgraded As Boolean = bbwPartial AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Microstructure)
        If bbwLongUpgraded Then state.LongScore += 1
        If bbwShortUpgraded Then state.ShortScore += 1

        Dim oiLongUpgraded As Boolean = oiPartialLong AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += 1
        If oiShortUpgraded Then state.ShortScore += 1

        Dim obvLongUpgraded As Boolean = obvPartialLong AndAlso HasCrossCategoryConfirmation(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso HasCrossCategoryConfirmation(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += 1
        If obvShortUpgraded Then state.ShortScore += 1

        ' Breakdown rendering after final state known
        breakdown.Add(("ROC(9)", rocLong OrElse rocLongUpgraded, rocShort OrElse rocShortUpgraded,
                       BuildNote($"{r.ROC:F3} | Slope: {r.ROCSlope}", rocPartialLong AndAlso Not rocLongUpgraded, rocPartialShort AndAlso Not rocShortUpgraded, rocLongUpgraded, rocShortUpgraded)))

        breakdown.Add(("RSI(9)", rsiLong OrElse rsiLongUpgraded, rsiShort OrElse rsiShortUpgraded,
                       BuildNote($"{r.RSI:F1}", rsiPartialLong AndAlso Not rsiLongUpgraded, rsiPartialShort AndAlso Not rsiShortUpgraded, rsiLongUpgraded, rsiShortUpgraded)))

        breakdown.Add(("DMI ±DI", dmiLong, dmiShort, $"+DI:{r.PlusDI:F1} -DI:{r.MinusDI:F1}"))
        breakdown.Add(("ADX>25", adxLong, adxShort, $"{r.ADX:F1}"))

        breakdown.Add(("Volume >=3xSMA", volSpike OrElse volLongUpgraded, volSpike OrElse volShortUpgraded,
                       BuildNote(r.VolumeRatio.ToString("F2") & "x", volPartial AndAlso Not volLongUpgraded, volPartial AndAlso Not volShortUpgraded, volLongUpgraded, volShortUpgraded)))

        breakdown.Add(("VWAP", vwapLong OrElse vwapLongUpgraded, vwapShort OrElse vwapShortUpgraded,
                       BuildNote($"VWAP:{r.VWAP:F1} Dev:{r.VWAPDevPct:F2}%", vwapPartialLong AndAlso Not vwapLongUpgraded, vwapPartialShort AndAlso Not vwapShortUpgraded, vwapLongUpgraded, vwapShortUpgraded)))

        breakdown.Add(("BBW Squeeze", bbwFull OrElse bbwLongUpgraded, bbwFull OrElse bbwShortUpgraded,
                       BuildNote($"{r.BBW:F3} | {r.SqueezeStatus}", bbwPartial AndAlso Not bbwLongUpgraded, bbwPartial AndAlso Not bbwShortUpgraded, bbwLongUpgraded, bbwShortUpgraded)))

        breakdown.Add(("EMA 9/21/50", emaBull, emaBear, $"9:{r.EMA9:F0} 21:{r.EMA21:F0} 50:{r.EMA50:F0} | {r.EMAAlignment}"))
        breakdown.Add(("Funding OK", fundOkLong, fundOkShort, $"{r.FundingRate * 100:F4}% | {r.FundingBias}"))

        breakdown.Add(("OI Δ", oiLong OrElse oiLongUpgraded, oiShort OrElse oiShortUpgraded,
                       BuildNote($"15m:{r.OIChange15m:F2}% 60m:{r.OIChange60m:F2}% | {r.OISignal}", oiPartialLong AndAlso Not oiLongUpgraded, oiPartialShort AndAlso Not oiShortUpgraded, oiLongUpgraded, oiShortUpgraded)))

        breakdown.Add(("OFI", ofiBuy, ofiSell, $"Ratio:{r.OFIRatio:F2} | {r.OFISignal}"))
        breakdown.Add(("No Adverse Liq", noLongLiq, noShortLiq, $"L:{r.LiqLongSize:F0} S:{r.LiqShortSize:F0} | {r.LiqSignal}"))
        breakdown.Add(("5m EMA(200)", ema200Bull, ema200Bear, $"{r.EMA200_5m:F0} | {r.PriceVsEMA200}"))
        breakdown.Add(("Donchian(20)", donchLong, donchShort, $"U:{r.DonchianUpper:F0} L:{r.DonchianLower:F0} | {r.DonchianSignal}"))

        breakdown.Add(("OBV", obvLong OrElse obvLongUpgraded, obvShort OrElse obvShortUpgraded,
                       BuildNote($"Trend:{r.OBVTrend} Div:{r.OBVDivergence}", obvPartialLong AndAlso Not obvLongUpgraded, obvPartialShort AndAlso Not obvShortUpgraded, obvLongUpgraded, obvShortUpgraded)))

        ' ── Step 3: Funding Rate Confidence Modifier ──────────────────────────
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

        ' ── Step 4: Regime Veto / Override ───────────────────────────────────
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

        ' ── Step 5: Generate Verdict ──────────────────────────────────────────
        res.LongScore = ls
        res.ShortScore = ss

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

        ' ── Step 6: Hold / Exit Assessment ───────────────────────────────────
        res.HoldStatus = CalcHoldStatus(r, posState)
        Return res
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

    Private Shared Function HasCrossCategoryConfirmation(cats As HashSet(Of SignalCategory), ownCat As SignalCategory) As Boolean
        Return cats.Any(Function(c) c <> ownCat)
    End Function

    Private Shared Function BuildNote(baseNote As String,
                                      partialLong As Boolean,
                                      partialShort As Boolean,
                                      upgradedLong As Boolean,
                                      upgradedShort As Boolean) As String
        If upgradedLong Then Return baseNote & " | PARTIAL->UPGRADED LONG"
        If upgradedShort Then Return baseNote & " | PARTIAL->UPGRADED SHORT"
        If partialLong Then Return baseNote & " | PARTIAL LONG"
        If partialShort Then Return baseNote & " | PARTIAL SHORT"
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
