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

''' <summary>
''' [P4 #1 realtime-exit-guard] Shared fast-exit primitives — the SINGLE source of truth for
''' "what counts as an adverse microstructure signal" during a hold. Consumed by BOTH
''' <see cref="ScoringEngine.CalcHoldStatus"/> (Layers 1 / 1.5 / 3) and ExitGuardEvaluator, so the
''' full-run hold logic and the realtime exit-guard overlay can never drift on the adverse
''' definitions. Pure data; no logic. (docs/realtime-exit-guard-proposal.md §4.3.)
'''
''' AdverseSignals carries the TERSE CalcHoldStatus fragments (e.g. "BEAR_ACCEL", "OFI:SELL") in
''' [micro, ofi, tfi, cvd] order, already filtered — so CalcHoldStatus's Layer-1 string stays
''' byte-identical via String.Join. The four booleans let the exit guard build its own readable
''' strip labels from the same source without re-deriving the adverse definitions.
''' </summary>
Public NotInheritable Class FastExitPrimitives
    Public Property AdverseCount    As Integer
    Public Property MicroAdverse    As Boolean
    Public Property OfiAdverse      As Boolean
    Public Property TfiAdverse      As Boolean
    Public Property CvdAdverse      As Boolean
    Public Property AdverseSignals  As String() = Array.Empty(Of String)()
    Public Property StructuralBreak As Boolean
    Public Property BreakLevel      As Double
End Class

Partial Public Class ScoringEngine

    ' Regime-specific max achievable scores.
    ' +2 vs previous values: TFI (Microstructure) + MicroCVD (Microstructure).
    ' cfg param: when cfg.RegimeWeights.Enabled, adds AlignmentBonus headroom for TRENDING/RANGE_BOUND
    ' so the Pass 2c bonus fits within the ceiling and verdict % thresholds auto-adjust.
    Public Shared Function RegimeMaxScore(regime As String, cfg As EngineSettings) As Integer
        Dim baseMax As Integer
        Select Case regime
            Case "TRENDING_UP", "TRENDING_DOWN" : baseMax = cfg.Scoring.RegimeMaxScore.Trending
            Case "RANGE_BOUND"                  : baseMax = cfg.Scoring.RegimeMaxScore.RangeBound
            Case Else                           : baseMax = cfg.Scoring.RegimeMaxScore.Transitional
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

    Private Shared Function TierFloor(rawScore As Integer, cfg As EngineSettings) As Integer
        Dim tf = cfg.Scoring.TierFloor
        If rawScore >= tf.HighThreshold Then Return tf.HighFloor
        If rawScore >= tf.MedThreshold  Then Return tf.MedFloor
        If rawScore >= tf.LowThreshold  Then Return tf.LowFloor
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

    ''' <summary>
    ''' [P4 #1 realtime-exit-guard] Computes the shared fast-exit primitives from an
    ''' IndicatorResults snapshot for the given side — the single source of truth for the
    ''' adverse-signal definitions + structural-break test used by BOTH CalcHoldStatus (Layers
    ''' 1 / 1.5 / 3) and ExitGuardEvaluator. Pure; reads only the streaming-driven fields
    ''' (MicroCVDSignal / OFISignal / TFISignal / CVDSlope / CVDValue) plus r.CurrentPrice and the
    ''' carried 5m swing levels for the structural break. (docs/realtime-exit-guard-proposal.md §4.2–§4.3.)
    ''' </summary>
    Friend Shared Function ComputeFastExitPrimitives(r As IndicatorResults, posState As PositionState) As FastExitPrimitives
        Dim p As New FastExitPrimitives()
        Select Case posState
            Case PositionState.InLong
                p.MicroAdverse = (r.MicroCVDSignal = "BEAR_ACCEL" OrElse r.MicroCVDSignal = "BEAR_DECEL")
                p.OfiAdverse   = (r.OFISignal = "SELL DOMINANT")
                p.TfiAdverse   = (r.TFISignal = "SELL PRESSURE")
                p.CvdAdverse   = (r.CVDSlope = "FALLING" AndAlso r.CVDValue < 0)
                p.AdverseSignals = New List(Of String) From {
                    If(p.MicroAdverse, r.MicroCVDSignal, Nothing),
                    If(p.OfiAdverse,   "OFI:SELL",       Nothing),
                    If(p.TfiAdverse,   "TFI:SELL",       Nothing),
                    If(p.CvdAdverse,   "CVD:FALLING",    Nothing)
                }.Where(Function(s) s IsNot Nothing).ToArray()
                p.StructuralBreak = (r.LastSwingLow5m > 0 AndAlso r.CurrentPrice <= r.LastSwingLow5m)
                p.BreakLevel = r.LastSwingLow5m
            Case PositionState.InShort
                p.MicroAdverse = (r.MicroCVDSignal = "BULL_ACCEL" OrElse r.MicroCVDSignal = "BULL_DECEL")
                p.OfiAdverse   = (r.OFISignal = "BUY DOMINANT")
                p.TfiAdverse   = (r.TFISignal = "BUY PRESSURE")
                p.CvdAdverse   = (r.CVDSlope = "RISING" AndAlso r.CVDValue > 0)
                p.AdverseSignals = New List(Of String) From {
                    If(p.MicroAdverse, r.MicroCVDSignal, Nothing),
                    If(p.OfiAdverse,   "OFI:BUY",        Nothing),
                    If(p.TfiAdverse,   "TFI:BUY",        Nothing),
                    If(p.CvdAdverse,   "CVD:RISING",     Nothing)
                }.Where(Function(s) s IsNot Nothing).ToArray()
                p.StructuralBreak = (r.LastSwingHigh5m > 0 AndAlso r.CurrentPrice >= r.LastSwingHigh5m)
                p.BreakLevel = r.LastSwingHigh5m
        End Select
        p.AdverseCount = (If(p.MicroAdverse, 1, 0)) + (If(p.OfiAdverse, 1, 0)) +
                         (If(p.TfiAdverse, 1, 0)) + (If(p.CvdAdverse, 1, 0))
        Return p
    End Function

    ' [P1] v0.48: CalcHoldStatus microstructure fast-exit layer.
    ' Previous version used only ROC, OBV divergence, RSI divergence, and RSI level.
    ' During a 2-15 minute hold, OFI, TFI, MicroCVD, and CVD react faster than RSI/ROC
    ' and are the correct tools to assess whether momentum is intact or deteriorating.
    '
    ' [T1-D]: All RSI/ROC threshold literals replaced with cfg.Scoring.Hold* fields.
    '
    ' Priority order for hold evaluation (highest precedence first) — [v47 N2] list
    ' corrected to match the code (A17g pins this order as canonical):
    '   1.   Microstructure fast exit: adverse MicroCVD signal + confirming OFI or TFI
    '        -- two independent microstructure signals both adverse = immediate exit.
    '   1.5  Structural break exit: price closed through the prior swing level.
    '   2.   Momentum-break exit: ROC crosses zero against the position (checked
    '        BEFORE OBV divergence).
    '   3.   OBV divergence exit.
    '   4.   RSI divergence evaluate.
    '   5.   Microstructure soft warning: single adverse microstructure signal alone.
    '   6.   ROC/RSI-based structural assessment (take-profit / hold / evaluate / exit).
    Private Shared Function CalcHoldStatus(r As IndicatorResults, posState As PositionState,
                                            cfg As EngineSettings) As String
        Select Case posState
            Case PositionState.InLong
                ' [P4 #1 realtime-exit-guard] Layers 1 / 1.5 / 3 read the SHARED primitive
                ' (ComputeFastExitPrimitives) the exit guard also consumes — one definition of
                ' "adverse," no drift. Output byte-identical to the prior inline computation.
                Dim p = ComputeFastExitPrimitives(r, posState)
                ' Layer 1: two adverse microstructure signals = fast exit
                If p.AdverseCount >= 2 Then
                    Return "EXIT -- microstructure deterioration (" & String.Join("+", p.AdverseSignals) & ")"
                End If
                ' Layer 1.5: structural break exit -- price closed through prior swing low
                If p.StructuralBreak Then
                    Return String.Format("EXIT -- structural break (closed at/below swing low {0:F1})", p.BreakLevel)
                End If
                ' Layer 2: structural divergence exits
                If r.ROC < 0 Then Return "EXIT -- momentum break (ROC crossed below 0)"
                If r.OBVDivergence = "BEARISH" Then Return "EXIT -- OBV bearish divergence"
                If r.RSIDivergence = "BEARISH" Then Return "EVALUATE -- RSI bearish divergence, watch for reversal"
                ' Layer 3: single adverse microstructure = soft warning
                If p.MicroAdverse Then Return "EVALUATE -- " & r.MicroCVDSignal & " signal, confirm with price action"
                ' Layer 4: RSI/ROC structural assessment -- [T1-D] thresholds from cfg
                If r.ROC > cfg.Scoring.HoldRocTakeProfitLong Then Return "TAKE PROFIT -- extreme momentum, tighten stops"
                If r.RSI > cfg.Scoring.HoldRsiHoldLong       Then Return "HOLD -- momentum intact"
                If r.RSI >= cfg.Scoring.HoldRsiEvaluateLong  Then Return "EVALUATE -- momentum weakening, consider scaling out"
                Return "EXIT -- retracement too deep (RSI < " & cfg.Scoring.HoldRsiEvaluateLong.ToString("F0") & ")"

            Case PositionState.InShort
                ' [P4 #1 realtime-exit-guard] Shared primitive (mirror of the long side).
                Dim p = ComputeFastExitPrimitives(r, posState)
                ' Layer 1: two adverse microstructure signals = fast exit
                If p.AdverseCount >= 2 Then
                    Return "EXIT -- microstructure deterioration (" & String.Join("+", p.AdverseSignals) & ")"
                End If
                ' Layer 1.5: structural break exit -- price closed through prior swing high
                If p.StructuralBreak Then
                    Return String.Format("EXIT -- structural break (closed at/above swing high {0:F1})", p.BreakLevel)
                End If
                ' Layer 2: structural divergence exits
                If r.ROC > 0 Then Return "EXIT -- momentum break (ROC crossed above 0)"
                If r.OBVDivergence = "BULLISH" Then Return "EXIT -- OBV bullish divergence"
                If r.RSIDivergence = "BULLISH" Then Return "EVALUATE -- RSI bullish divergence, watch for reversal"
                ' Layer 3: single adverse microstructure = soft warning
                If p.MicroAdverse Then Return "EVALUATE -- " & r.MicroCVDSignal & " signal, confirm with price action"
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
