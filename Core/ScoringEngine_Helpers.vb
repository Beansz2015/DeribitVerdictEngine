' Core/ScoringEngine_Helpers.vb
' ScoringEngine partial: utility and helper methods.
' Covers: RegimeMaxScore, Threshold, TierFloor, AddFull,
'         HasCrossConfirm, BuildNote, CalcHoldStatus.

Partial Public Class ScoringEngine

    ' Regime-specific max achievable scores
    ' +2 vs previous values: TFI (Microstructure) + MicroCVD (Microstructure)
    Public Shared Function RegimeMaxScore(regime As String) As Integer
        Select Case regime
            Case "TRENDING_UP", "TRENDING_DOWN" : Return 19
            Case "RANGE_BOUND"                  : Return 18
            Case Else                           : Return 15   ' TRANSITIONAL
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
