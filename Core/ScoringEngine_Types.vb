' Core/ScoringEngine_Types.vb
' All data types used by the scoring engine.
' No logic -- data containers and enums only.

' Replaces anonymous tuple in List(Of (...)) which confuses the VB.NET parser
Public Class SignalBreakdownItem
    Public Property Label As String
    Public Property LongHit As Boolean
    Public Property ShortHit As Boolean
    Public Property Note As String
    ''' <summary>Actual signed contribution to v.LongScore from this emission.
    ''' Positive when this row added to Long, negative on penalties. The sum
    ''' across all items equals v.LongScore (raw, through Step 3b). Captured
    ''' from the before/after state delta at the emission site, so caps and
    ''' floors are respected automatically. (Spec C — SC/TOTAL parity.)</summary>
    Public Property LongPoints As Integer
    ''' <summary>Actual signed contribution to v.ShortScore from this emission.
    ''' Positive when this row added to Short, negative on penalties. The sum
    ''' across all items equals v.ShortScore (raw, through Step 3b).</summary>
    Public Property ShortPoints As Integer

    ' Original 4-arg constructor preserved — new points default to 0. Used by the
    ' informational MTF Gate rows in _Verdict.vb (vetoes, not scoring contributors).
    Public Sub New(lbl As String, lng As Boolean, sht As Boolean, nt As String)
        Label = lbl : LongHit = lng : ShortHit = sht : Note = nt
    End Sub
    ' 6-arg constructor for emission sites that carry an actual scoring delta.
    Public Sub New(lbl As String, lng As Boolean, sht As Boolean, nt As String,
                   lngPts As Integer, shtPts As Integer)
        Label = lbl : LongHit = lng : ShortHit = sht : Note = nt
        LongPoints = lngPts : ShortPoints = shtPts
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
    ''' <summary>True when the kelly.max_leverage cap (not the $ risk cap) set KellyContracts.</summary>
    Public Property KellyLevCapped As Boolean = False

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

    ''' <summary>
    ''' Display-only ledger guard flag (Spec C). True when the signed
    ''' SignalBreakdown points do NOT sum to LongScore/ShortScore — i.e. a
    ''' scoring contribution was mis-attributed (the #1 banned pattern,
    ''' double-counting, would trip this). Set by CheckLedger() before every
    ''' Return in Calculate(). Surfaced via console, the status-bar LOG line,
    ''' and the output-dump block. Never in production output when quiet; no
    ''' CSV column. Zero scoring impact.
    ''' </summary>
    Public Property LedgerMismatch As Boolean = False

    ''' <summary>
    ''' [F12 / E3a — 2026-07-21] Render the verdict for display: the middle band
    ''' is drawn as "MEDIUM LONG" / "MEDIUM SHORT" so the on-screen ladder reads
    ''' STRONG / MEDIUM / WEAK explicitly. The stored/wire string stays bare
    ''' LONG / SHORT — CSV Verdict, payload verdict, eval cache, and every
    ''' string-matching site are untouched (parity rule deliberately diverged on
    ''' the two render surfaces, precedented by the cap-reason rich string vs
    ''' CSV bucket; spec §3 revised same-day to DISPLAY-RENDERING only). Both
    ''' render sites — BuildPlaintextSnapshot and BindCardVerdict — MUST route
    ''' through this helper so the mapping stays in one place; adding a third
    ''' render surface means calling this here too.
    ''' </summary>
    Public Shared Function FormatVerdictForDisplay(stored As String) As String
        If String.IsNullOrEmpty(stored) Then Return stored
        Dim s As String = stored.Trim()
        If s = "LONG"  Then Return "MEDIUM LONG"
        If s = "SHORT" Then Return "MEDIUM SHORT"
        Return stored
    End Function
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
