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

    ' ---------------------------------------------------------------------------
    ' Kelly sizing outputs
    ' Populated by CalcKellySizing() in MainForm_Render -- display-only, no scoring impact.
    ' All fields at default (0 / "") = Kelly block suppressed (no edge or not computed).
    ' ---------------------------------------------------------------------------

    ''' <summary>Raw Kelly fraction f* = (b*p - q) / b. May be negative (no edge).</summary>
    Public Property KellyF        As Double  = 0.0
    ''' <summary>Half-Kelly fraction (f* / 2). Zero if f* <= 0.</summary>
    Public Property KellyFHalf    As Double  = 0.0
    ''' <summary>Applied fraction after hard cap (Min(f_half, MaxRiskFraction)). Zero if f* <= 0.</summary>
    Public Property KellyFApplied As Double  = 0.0
    ''' <summary>Win probability p used in the Kelly formula.</summary>
    Public Property KellyPWin     As Double  = 0.0
    ''' <summary>Probability estimation mode: "EST" (pre-calibration) or "CAL" (post-calibration).</summary>
    Public Property KellyPMode    As String  = ""
    ''' <summary>True when MaxRiskFraction cap was applied (f_half > MaxRiskFraction).</summary>
    Public Property KellyCapped   As Boolean = False
    ''' <summary>Recommended whole contracts. 0 = less than 1 contract (stop too wide).</summary>
    Public Property KellyContracts As Integer = 0
    ''' <summary>Dollar risk amount = AccountSizeUsd * KellyFApplied.</summary>
    Public Property KellyRiskUsd  As Double  = 0.0
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
