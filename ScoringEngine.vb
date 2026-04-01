' ScoringEngine.vb  v0.25
' Implements the 6-step verdict engine from the specification.
' Input: IndicatorResults + DynamicNorms + position state. Output: VerdictResult.
'
' v0.25 — Regime-gated scoring weights
'   TRENDING (UP/DOWN) : momentum signals x2  (ROC, RSI, ADX, EMA Ribbon)
'   RANGE_BOUND        : microstructure signals x2  (VWAP, OI Delta, OFI)
'   TRANSITIONAL       : all signals x1 (unchanged), ADX penalty retained
'
'   Regime-specific MaxScore:
'     TRENDING    : 17  (4 momentum sigs x2 = +4; rest 9 = 13 base; +4 = 17)
'     RANGE_BOUND : 16  (3 micro sigs x2 = +3; rest 10 = 13 base; +3 = 16)
'     TRANSITIONAL: 13  (unchanged)
'
'   Verdict thresholds (absolute, not % of max):
'     STRONG >= 13  |  MEDIUM >= 10  |  WEAK >= 7
'
' v0.24: SettingsLoader wired in MainForm.
' v0.23: Volume/VWAPDev thresholds from DynamicNorms.

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
    Public Property MaxScore As Integer          ' regime-specific ceiling, for display
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

    ' Base score ceiling when no regime weighting applies (TRANSITIONAL)
    Public Const BaseMaxScore As Integer = 13

    ' Legacy flat accessor -- use VerdictResult.MaxScore for regime-aware ceiling
    Public Shared ReadOnly Property MaxScore As Integer
        Get
            Return BaseMaxScore
        End Get
    End Property

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState,
                                     norms As DynamicNorms) As VerdictResult
        Dim res As New VerdictResult()
        Dim breakdown = res.SignalBreakdown
        Dim state As New ScoreState()

        ' Determine weight multipliers based on regime
        Dim wMomentum As Integer = 1
        Dim wMicro    As Integer = 1

        Select Case r.Regime
            Case "TRENDING_UP", "TRENDING_DOWN"
                wMomentum = 2
            Case "RANGE_BOUND"
                wMicro = 2
        End Select

        ' Regime-specific max score for display
        Dim regimeMax As Integer
        Select Case r.Regime
            Case "TRENDING_UP", "TRENDING_DOWN" : regimeMax = 17
            Case "RANGE_BOUND"                  : regimeMax = 16
            Case Else                           : regimeMax = BaseMaxScore
        End Select
        res.MaxScore = regimeMax

        ' ---- Step 2: Weighted Signal Scoring --------------------------------

        ' ROC (Momentum) -- x2 TRENDING
        Dim rocLong As Boolean = r.ROC > 0 AndAlso r.ROCSlope = "RISING"
        Dim rocShort As Boolean = r.ROC < 0 AndAlso r.ROCSlope = "FALLING"
        Dim rocPartialLong As Boolean = r.ROC > 0.1 AndAlso r.ROCSlope <> "RISING"
        Dim rocPartialShort As Boolean = r.ROC < -0.1 AndAlso r.ROCSlope <> "FALLING"
        AddFull(state, rocLong, rocShort, SignalCategory.Momentum, wMomentum)

        ' RSI (Momentum) -- x2 TRENDING
        Dim rsiLong As Boolean = r.RSI > 60
        Dim rsiShort As Boolean = r.RSI < 40
        Dim rsiPartialLong As Boolean = r.RSI > 50 AndAlso r.RSI <= 60
        Dim rsiPartialShort As Boolean = r.RSI < 50 AndAlso r.RSI >= 40
        AddFull(state, rsiLong, rsiShort, SignalCategory.Momentum, wMomentum)

        ' DMI (MarketStructure) -- x1 always
        Dim dmiLong As Boolean = r.PlusDI > r.MinusDI
        Dim dmiShort As Boolean = r.MinusDI > r.PlusDI
        AddFull(state, dmiLong, dmiShort, SignalCategory.MarketStructure)

        ' ADX (Momentum) -- x2 TRENDING
        Dim adxLong As Boolean = r.ADX > 25 AndAlso dmiLong
        Dim adxShort As Boolean = r.ADX > 25 AndAlso dmiShort
        AddFull(state, adxLong, adxShort, SignalCategory.Momentum, wMomentum)

        ' Volume (Volume) -- x1 always
        Dim volHigh As Double = norms.VolHighThreshold
        Dim volMid As Double = norms.VolMidThreshold
        Dim volLong As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC > 0 AndAlso r.CurrentPrice > r.VWAP
        Dim volShort As Boolean = r.VolumeRatio >= volHigh AndAlso r.ROC < 0 AndAlso r.CurrentPrice < r.VWAP
        Dim volPartial As Boolean = r.VolumeRatio >= volMid AndAlso r.VolumeRatio < volHigh
        AddFull(state, volLong, volShort, SignalCategory.Volume)

        ' VWAP (Microstructure) -- x2 RANGE_BOUND
        Dim vwapDev As Double = norms.VWAPDevThreshold
        Dim vwapLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= vwapDev
        Dim vwapShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) <= vwapDev
        Dim vwapPartialLong As Boolean = r.CurrentPrice > r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > vwapDev
        Dim vwapPartialShort As Boolean = r.CurrentPrice < r.VWAP AndAlso Math.Abs(r.VWAPDevPct) > vwapDev
        AddFull(state, vwapLong, vwapShort, SignalCategory.Microstructure, wMicro)

        ' BBW Squeeze -- x1 always (volatility regime, not direction weight)
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

        ' EMA Ribbon (Momentum) -- x2 TRENDING
        Dim emaBull As Boolean = r.EMAAlignment = "BULL"
        Dim emaBear As Boolean = r.EMAAlignment = "BEAR"
        AddFull(state, emaBull, emaBear, SignalCategory.Momentum, wMomentum)

        ' OI Delta (Microstructure) -- x2 RANGE_BOUND
        Dim oiLong As Boolean = r.OISignal = "NEW LONGS"
        Dim oiShort As Boolean = r.OISignal = "NEW SHORTS"
        Dim oiPartialLong As Boolean = r.OISignal = "COVERING"
        Dim oiPartialShort As Boolean = r.OISignal = "CAPITULATION"
        AddFull(state, oiLong, oiShort, SignalCategory.Microstructure, wMicro)

        ' OFI (Microstructure) -- x2 RANGE_BOUND
        Dim ofiBuy As Boolean = r.OFISignal = "BUY DOMINANT"
        Dim ofiSell As Boolean = r.OFISignal = "SELL DOMINANT"
        AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure, wMicro)

        ' Liquidations -- penalty-only, unchanged
        Dim liqLongPenalty As Integer = 0
        Dim liqShortPenalty As Integer = 0
        If r.LiqSignal = "LONG LIQS" Then
            liqLongPenalty = If(r.LiqLongSize > 200, 2, 1)
            state.LongScore = Math.Max(0, state.LongScore - liqLongPenalty)
        ElseIf r.LiqSignal = "SHORT LIQS" Then
            liqShortPenalty = If(r.LiqShortSize > 200, 2, 1)
            state.ShortScore = Math.Max(0, state.ShortScore - liqShortPenalty)
        End If

        ' 5m EMA200 (MarketStructure) -- x1 always (macro filter)
        Dim ema200Bull As Boolean = r.CurrentPrice > r.EMA200_5m AndAlso r.EMA200_5m > 0
        Dim ema200Bear As Boolean = r.CurrentPrice < r.EMA200_5m AndAlso r.EMA200_5m > 0
        AddFull(state, ema200Bull, ema200Bear, SignalCategory.MarketStructure)

        ' Donchian (MarketStructure) -- x1 always
        Dim donchLong As Boolean = r.DonchianSignal = "LONG"
        Dim donchShort As Boolean = r.DonchianSignal = "SHORT"
        AddFull(state, donchLong, donchShort, SignalCategory.MarketStructure)

        ' OBV (Volume) -- x1 always
        Dim obvLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "NONE"
        Dim obvShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "NONE"
        Dim obvPartialLong As Boolean = r.OBVTrend = "RISING" AndAlso r.OBVDivergence = "BEARISH"
        Dim obvPartialShort As Boolean = r.OBVTrend = "FALLING" AndAlso r.OBVDivergence = "BULLISH"
        AddFull(state, obvLong, obvShort, SignalCategory.Volume)

        ' ---- Pass 2: Partial upgrades (always +1 regardless of regime weight)
        Dim rocLongUpgraded As Boolean = rocPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rocShortUpgraded As Boolean = rocPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rocLongUpgraded Then state.LongScore += 1
        If rocShortUpgraded Then state.ShortScore += 1

        Dim rsiLongUpgraded As Boolean = rsiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Momentum)
        Dim rsiShortUpgraded As Boolean = rsiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Momentum)
        If rsiLongUpgraded Then state.LongScore += 1
        If rsiShortUpgraded Then state.ShortScore += 1

        Dim vwapLongUpgraded As Boolean = vwapPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim vwapShortUpgraded As Boolean = vwapPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If vwapLongUpgraded Then state.LongScore += 1
        If vwapShortUpgraded Then state.ShortScore += 1

        Dim oiLongUpgraded As Boolean = oiPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Microstructure)
        Dim oiShortUpgraded As Boolean = oiPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Microstructure)
        If oiLongUpgraded Then state.LongScore += 1
        If oiShortUpgraded Then state.ShortScore += 1

        Dim obvLongUpgraded As Boolean = obvPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.Volume)
        Dim obvShortUpgraded As Boolean = obvPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.Volume)
        If obvLongUpgraded Then state.LongScore += 1
        If obvShortUpgraded Then state.ShortScore += 1

        ' Breakdown labels annotated with [x2] where weight is doubled
        Dim normMode As String = If(norms.IsLive, "LIVE", "STATIC")
        Dim wMTag As String = If(wMomentum = 2, " [x2]", "")
        Dim wUTag As String = If(wMicro = 2, " [x2]", "")

        breakdown.Add(New SignalBreakdownItem("ROC(9)" & wMTag, rocLong OrElse rocLongUpgraded, rocShort OrElse rocShortUpgraded,
            BuildNote(String.Format("{0:F3} | Slope: {1}", r.ROC, r.ROCSlope),
                      rocPartialLong AndAlso Not rocLongUpgraded, rocPartialShort AndAlso Not rocShortUpgraded,
                      rocLongUpgraded, rocShortUpgraded)))

        Dim rsiNote As String = String.Format("{0:F1}", r.RSI)
        If r.RSIDivergence <> "NONE" Then rsiNote &= String.Format(" | DIV:{0}", r.RSIDivergence)
        breakdown.Add(New SignalBreakdownItem("RSI(9)" & wMTag, rsiLong OrElse rsiLongUpgraded, rsiShort OrElse rsiShortUpgraded,
            BuildNote(rsiNote,
                      rsiPartialLong AndAlso Not rsiLongUpgraded, rsiPartialShort AndAlso Not rsiShortUpgraded,
                      rsiLongUpgraded, rsiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("DMI +/-DI", dmiLong, dmiShort,
            String.Format("+DI:{0:F1} -DI:{1:F1}", r.PlusDI, r.MinusDI)))

        breakdown.Add(New SignalBreakdownItem("ADX>25" & wMTag, adxLong, adxShort,
            String.Format("{0:F1}", r.ADX)))

        breakdown.Add(New SignalBreakdownItem("Volume", volLong, volShort,
            BuildNote(String.Format("{0:F2}x | thr H:{1:F2}x M:{2:F2}x [{3}]",
                                   r.VolumeRatio, volHigh, volMid, normMode),
                      volPartial, volPartial, False, False)))

        breakdown.Add(New SignalBreakdownItem("VWAP" & wUTag, vwapLong OrElse vwapLongUpgraded, vwapShort OrElse vwapShortUpgraded,
            BuildNote(String.Format("Dev:{0:F2}% | thr ±{1:F2}% [{2}]", r.VWAPDevPct, vwapDev, normMode),
                      vwapPartialLong AndAlso Not vwapLongUpgraded, vwapPartialShort AndAlso Not vwapShortUpgraded,
                      vwapLongUpgraded, vwapShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("BBW Squeeze", bbwLongHit, bbwShortHit, bbwNote))

        breakdown.Add(New SignalBreakdownItem("EMA 9/21/50" & wMTag, emaBull, emaBear,
            String.Format("9:{0:F0} 21:{1:F0} 50:{2:F0} | {3}", r.EMA9, r.EMA21, r.EMA50, r.EMAAlignment)))

        breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False,
            String.Format("{0:F4}% | {1}", r.FundingRate * 100, r.FundingBias)))

        breakdown.Add(New SignalBreakdownItem("OI Delta" & wUTag, oiLong OrElse oiLongUpgraded, oiShort OrElse oiShortUpgraded,
            BuildNote(String.Format("15m:{0:F2}% 60m:{1:F2}% | {2}", r.OIChange15m, r.OIChange60m, r.OISignal),
                      oiPartialLong AndAlso Not oiLongUpgraded, oiPartialShort AndAlso Not oiShortUpgraded,
                      oiLongUpgraded, oiShortUpgraded)))

        breakdown.Add(New SignalBreakdownItem("OFI" & wUTag, ofiBuy, ofiSell,
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

        ' ---- Step 3: Funding Rate Confidence Modifier -----------------------
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

        ' ---- Step 4: Regime Veto / Override ---------------------------------
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
                If r.ADX >= 20.0 AndAlso r.ADX < 22.5 Then
                    adxPenalty = 2
                ElseIf r.ADX >= 22.5 AndAlso r.ADX < 25.0 Then
                    adxPenalty = 1
                End If
                effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls))
                effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss))
        End Select

        ' ---- Step 5: Generate Verdict ---------------------------------------
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty

        Dim maxL As Integer = effectiveLS
        Dim maxS As Integer = effectiveSS
        Dim leading As Integer = Math.Max(maxL, maxS)
        Dim leadIsLong As Boolean = maxL >= maxS

        If leading >= 13 Then
            res.Verdict = If(leadIsLong, "STRONG LONG", "STRONG SHORT")
            res.Confidence = "HIGH"
        ElseIf leading >= 10 Then
            res.Verdict = If(leadIsLong, "LONG", "SHORT")
            res.Confidence = "MEDIUM"
        ElseIf leading >= 7 Then
            res.Verdict = If(leadIsLong, "WEAK LONG", "WEAK SHORT")
            res.Confidence = "LOW"
        Else
            res.Verdict = "NO TRADE" : res.Confidence = "N/A"
        End If

        ' ---- Step 6: Hold / Exit Assessment ---------------------------------
        res.HoldStatus = CalcHoldStatus(r, posState)
        Return res
    End Function

    Private Shared Function TierFloor(rawScore As Integer) As Integer
        If rawScore >= 13 Then Return 10
        If rawScore >= 10 Then Return 7
        If rawScore >= 7 Then Return 4
        Return 0
    End Function

    Private Shared Sub AddFull(state As ScoreState,
                               fullLong As Boolean, fullShort As Boolean,
                               cat As SignalCategory,
                               Optional weight As Integer = 1)
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
