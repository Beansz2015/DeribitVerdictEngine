' Core/ScoringEngine_Types.vb
' All data types used by the scoring engine.
' No logic -- data containers and enums only.

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
    ''' <summary>Regime-aware maximum achievable score. 19=TRENDING, 18=RANGE_BOUND, 15=TRANSITIONAL.</summary>
    Public Property MaxScore As Integer
    Public Property Verdict As String
    Public Property Confidence As String
    Public Property HoldStatus As String
    Public Property SignalBreakdown As New List(Of SignalBreakdownItem)

    ' VPFR-aware target adjustment
    ' Non-zero when an HVN wall falls between entry and the raw ATR target.
    ' Zero means no cap was applied -- use the raw ATR target as normal.
    Public Property AdjustedLongTarget  As Double  ' capped long target ($), 0 = no cap
    Public Property AdjustedShortTarget As Double  ' capped short target ($), 0 = no cap
    Public Property TargetCapReason     As String  ' e.g. "HVN_CAPPED @ 72480 (POC wall)" or ""

    ''' <summary>
    ''' Post-scoring diagnostic context for weak/ambiguous verdicts.
    ''' Values: FLOW_UNCONFIRMED | MOMENTUM_FADING | STRUCTURALLY_WEAK | CONFIRMED
    ''' CONFIRMED is not displayed -- absence of CONTEXT: line in output means all tiers aligned.
    ''' Set by CalcVerdictContext() in ScoringEngine_Calculate Step 5b.
    ''' </summary>
    Public Property VerdictContext As String = "CONFIRMED"
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
