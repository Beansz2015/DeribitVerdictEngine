' Core/ScoringEngine_Helpers.vb
' ScoringEngine partial: utility and helper methods.
' Covers: RegimeMaxScore, Threshold, TierFloor, AddFull,
'         HasCrossConfirm, BuildNote, CalcHoldStatus.
'
' [P1] v0.48: CalcHoldStatus microstructure fast-exit layer added.
' [T1-D]: CalcHoldStatus exit thresholds from cfg.
'   Was: ROC > 0.6 / ROC < -0.6 (take profit) and RSI > 60 / RSI < 40 (hold)
'        all hardcoded literals.
'   Now: cfg.Scoring.HoldRocTakeProfitLong/Short and HoldRsiHoldLong/Short/
'        HoldRsiEvaluateLong/Short. Defaults preserve prior behaviour exactly.
'   Signature change: CalcHoldStatus(r, posState, cfg) -- cfg added as 3rd param.
'   All three call sites in ScoringEngine_Calculate.vb updated accordingly.

Partial Public Class ScoringEngine

    ' Regime-specific max achievable scores.
    ' +2 vs previous values: TFI (Microstructure) + MicroCVD (Microstructure).
    ' cfg param: when cfg.RegimeWeights.Enabled, adds AlignmentBonus headroom for TRENDING/RANGE_BOUND
    ' so the Pass 2c bonus fits within the ceiling and verdict % thresholds auto-adjust.
    Public Shared Function RegimeMaxScore(regime As String, cfg As EngineSettings) As Integer
        Dim baseMax As Integer
        Select Case regime
            Case "TRENDING_UP", "TRENDING_DOWN" : baseMax = 19
            Case "RANGE_BOUND"                  : baseMax = 18
            Case Else                           : baseMax = 15
        End Select
        If Not cfg.RegimeWeights.Enabled Then Return baseMax
        Select Case regime
            Case "TRENDING_UP", "TRENDING_DOWN" : Return baseMax + cfg.RegimeWeights.Trending.AlignmentBonus
            Case "RANGE_BOUND"                  : Return baseMax + cfg.RegimeWeights.RangeBound.AlignmentBonus
            Case Else                           : Return baseMax
        End Select
    End Function

    ''' <summary>
    ''' Returns the integer threshold at or above which a score qualifies for the given tier.
    ''' Replaces the former ThresholdStrong / ThresholdMed / ThresholdWeak trio (identical bodies).
    ''' </summary>
    Private Shared Function Threshold(maxScore As Integer, pct As Double) As Integer
        Return CInt(Math.Ceiling(maxScore * pct))
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

    ' [P1] v0.48: CalcHoldStatus microstructure fast-exit layer.
    ' Previous version used only ROC, OBV divergence, RSI divergence, and RSI level.
    ' During a 2-15 minute hold, OFI, TFI, MicroCVD, and CVD react faster than RSI/ROC
    ' and are the correct tools to assess whether momentum is intact or deteriorating.
    '
    ' [T1-D]: All RSI/ROC threshold literals replaced with cfg.Scoring.Hold* fields.
    '
    ' Priority order for hold evaluation (highest precedence first):
    '   1. Microstructure fast exit: adverse MicroCVD signal + confirming OFI or TFI
    '      -- two independent microstructure signals both adverse = immediate exit.
    '   2. OBV divergence exit (unchanged from prior version).
    '   3. RSI divergence evaluate (unchanged).
    '   4. Microstructure soft warning: single adverse microstructure signal alone.
    '   5. ROC/RSI-based structural exit (unchanged from prior version).
    Private Shared Function CalcHoldStatus(r As IndicatorResults, posState As PositionState,
                                            cfg As EngineSettings) As String
        Select Case posState
            Case PositionState.InLong
                ' Layer 1: two adverse microstructure signals = fast exit
                Dim microAdverse As Boolean = (r.MicroCVDSignal = "BEAR_ACCEL" OrElse r.MicroCVDSignal = "BEAR_DECEL")
                Dim ofiAdverse   As Boolean = (r.OFISignal = "SELL DOMINANT")
                Dim tfiAdverse   As Boolean = (r.TFISignal = "SELL PRESSURE")
                Dim cvdAdverse   As Boolean = (r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0)
                Dim adverseCount As Integer = (If(microAdverse, 1, 0)) + (If(ofiAdverse, 1, 0)) +
                                              (If(tfiAdverse, 1, 0)) + (If(cvdAdverse, 1, 0))
                If adverseCount >= 2 Then
                    Return "EXIT -- microstructure deterioration (" &
                           String.Join("+", New List(Of String) From {
                               If(microAdverse, r.MicroCVDSignal, Nothing),
                               If(ofiAdverse,   "OFI:SELL",       Nothing),
                               If(tfiAdverse,   "TFI:SELL",       Nothing),
                               If(cvdAdverse,   "CVD:FALLING",    Nothing)
                           }.Where(Function(s) s IsNot Nothing)) & ")"
                End If
                ' Layer 2: structural divergence exits
                If r.ROC < 0 Then Return "EXIT -- momentum break (ROC crossed below 0)"
                If r.OBVDivergence = "BEARISH" Then Return "EXIT -- OBV bearish divergence"
                If r.RSIDivergence = "BEARISH" Then Return "EVALUATE -- RSI bearish divergence, watch for reversal"
                ' Layer 3: single adverse microstructure = soft warning
                If microAdverse Then Return "EVALUATE -- " & r.MicroCVDSignal & " signal, confirm with price action"
                ' Layer 4: RSI/ROC structural assessment -- [T1-D] thresholds from cfg
                If r.ROC > cfg.Scoring.HoldRocTakeProfitLong Then Return "TAKE PROFIT -- extreme momentum, tighten stops"
                If r.RSI > cfg.Scoring.HoldRsiHoldLong       Then Return "HOLD -- momentum intact"
                If r.RSI >= cfg.Scoring.HoldRsiEvaluateLong  Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI < " & cfg.Scoring.HoldRsiEvaluateLong.ToString("F0") & ")"

            Case PositionState.InShort
                ' Layer 1: two adverse microstructure signals = fast exit
                Dim microAdverse As Boolean = (r.MicroCVDSignal = "BULL_ACCEL" OrElse r.MicroCVDSignal = "BULL_DECEL")
                Dim ofiAdverse   As Boolean = (r.OFISignal = "BUY DOMINANT")
                Dim tfiAdverse   As Boolean = (r.TFISignal = "BUY PRESSURE")
                Dim cvdAdverse   As Boolean = (r.CVDSlope = "RISING" AndAlso r.CVDValue > 0)
                Dim adverseCount As Integer = (If(microAdverse, 1, 0)) + (If(ofiAdverse, 1, 0)) +
                                              (If(tfiAdverse, 1, 0)) + (If(cvdAdverse, 1, 0))
                If adverseCount >= 2 Then
                    Return "EXIT -- microstructure deterioration (" &
                           String.Join("+", New List(Of String) From {
                               If(microAdverse, r.MicroCVDSignal, Nothing),
                               If(ofiAdverse,   "OFI:BUY",        Nothing),
                               If(tfiAdverse,   "TFI:BUY",        Nothing),
                               If(cvdAdverse,   "CVD:RISING",     Nothing)
                           }.Where(Function(s) s IsNot Nothing)) & ")"
                End If
                ' Layer 2: structural divergence exits
                If r.ROC > 0 Then Return "EXIT -- momentum break (ROC crossed above 0)"
                If r.OBVDivergence = "BULLISH" Then Return "EXIT -- OBV bullish divergence"
                If r.RSIDivergence = "BULLISH" Then Return "EVALUATE -- RSI bullish divergence, watch for reversal"
                ' Layer 3: single adverse microstructure = soft warning
                If microAdverse Then Return "EVALUATE -- " & r.MicroCVDSignal & " signal, confirm with price action"
                ' Layer 4: RSI/ROC structural assessment -- [T1-D] thresholds from cfg
                If r.ROC < cfg.Scoring.HoldRocTakeProfitShort Then Return "TAKE PROFIT -- extreme bearish momentum, tighten stops"
                If r.RSI < cfg.Scoring.HoldRsiHoldShort        Then Return "HOLD -- bearish momentum intact"
                If r.RSI <= cfg.Scoring.HoldRsiEvaluateShort   Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI > " & cfg.Scoring.HoldRsiEvaluateShort.ToString("F0") & ")"

            Case Else
                Return "N/A -- no open position"
        End Select
    End Function

End Class
