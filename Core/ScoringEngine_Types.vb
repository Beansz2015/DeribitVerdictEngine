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
    Public Property AdjustedLongTarget   As Double  ' capped long target ($), 0 = no cap
    Public Property AdjustedShortTarget  As Double  ' capped short target ($), 0 = no cap
    Public Property TargetCapReasonLong  As String  ' e.g. "CAPPED @ 81382.6 (NEAREST_HVN_ABOVE)" or ""
    Public Property TargetCapReasonShort As String  ' e.g. "CAPPED @ 81344.8 (NEAREST_HVN_BELOW)" or ""

    ''' <summary>
    ''' Post-scoring diagnostic context for weak/ambiguous verdicts.
    ''' Values: FLOW_UNCONFIRMED | MOMENTUM_FADING | STRUCTURALLY_WEAK | CONFIRMED
    ''' CONFIRMED is not displayed -- absence of CONTEXT: line in output means all tiers aligned.
    ''' Set by CalcVerdictContext() in ScoringEngine_Calculate Step 5b.
    ''' </summary>
    Public Property VerdictContext As String = "CONFIRMED"

    ''' <summary>
    ''' Pass 2b OI x CVD cross-confirm gate outcome for this run.
    ''' Values: "NONE" / "CONFIRMED_LONG" / "CONFIRMED_SHORT" / "CONFLICT_LONG" / "CONFLICT_SHORT".
    ''' "NONE" when the gate is disabled, OI did not fire a level signal, or no qualifying
    ''' alignment/conflict was detected.
    ''' Set by RunScoringPipeline at Pass 2b. Display impact already surfaced in the OI Delta
    ''' breakdown note; this field makes the outcome CSV-loggable for calibration analysis.
    ''' </summary>
    Public Property OiCvdOutcome As String = "NONE"

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
    ''' <summary>Probability estimation mode. Always "EST" — CAL mode will be reinstated after the backtesting module is built.</summary>
    Public Property KellyPMode    As String  = ""
    ''' <summary>True when MaxRiskFraction cap was applied (f_half > MaxRiskFraction).</summary>
    Public Property KellyCapped   As Boolean = False
    ''' <summary>Recommended whole contracts. 0 = less than 1 contract (stop too wide).</summary>
    Public Property KellyContracts As Integer = 0
    ''' <summary>Dollar risk amount = AccountSizeUsd * KellyFApplied.</summary>
    Public Property KellyRiskUsd  As Double  = 0.0

    ''' <summary>Analysis run timestamp. Set in RunAnalysisAsync; used for TIME: line and dump header.</summary>
    Public Property Timestamp As DateTime = DateTime.MinValue

    ''' <summary>
    ''' Final composed MTF gate reason, set at Step 4b against the dominant side.
    ''' Three locked formats: "MTF PASS [LONG] &lt;details&gt;" /
    ''' "MTF BLOCK [LONG vs BEAR] &lt;details&gt;" (mirror for SHORT) /
    ''' "MTF state: &lt;TREND&gt; | &lt;details&gt;" when no directional verdict is in play.
    ''' Every consumer (MTF card, plaintext snapshot, CSV, breakdown row) renders
    ''' this exact string.
    ''' </summary>
    Public Property MTFGateReason As String = ""
    ''' <summary>True when Step 4b enforced the MTF hard veto (verdict forced to NO TRADE).</summary>
    Public Property MTFGateBlocked As Boolean = False
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
