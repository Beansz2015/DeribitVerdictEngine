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

    ''' <summary>
    ''' [D-9 (b), docs/engine-fix-build-spec-2026-09-21.md §5.2] Step 3b's OWN signed effect on
    ''' each side's score: the ACTUAL delta Step 3b applied (after its floor at 0 and its cap
    ''' at the regime max), not the configured soften / amplify value. Negative = a crowding
    ''' penalty, positive = a de-crowding soften, 0 = no effect (disabled, no arm fired, or
    ''' the clamp absorbed it). Step 3b moves at most one side per run.
    ''' Set by RunScoringPipeline at Step 3b. Display-only: the card and the snapshot read it
    ''' (through FundingStep3bDisplay) instead of parsing the breakdown note's text. Not logged.
    ''' </summary>
    Public Property FundingStep3bLongPoints As Integer
    Public Property FundingStep3bShortPoints As Integer

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

    ' ---------------------------------------------------------------------------
    ' Kelly one-class + placed-payoff-book fields (v69, docs/kelly-one-class-
    ' placed-payoff-spec.md §3.3 + the K-1 (g) ruling). Display-only, no scoring
    ' impact. All default to their reset state: no side / book not read.
    ' ---------------------------------------------------------------------------

    ''' <summary>True when a Kelly side exists (verdict side, or the lean side on
    ''' "NO TRADE [WEAK LONG]"/"[WEAK SHORT]"). False on plain NO TRADE and "[TIE]" —
    ''' the render gate (KO-4): the Kelly block shows iff this is True, replacing the
    ''' pre-v69 KellyPWin &gt; 0 gate that finding F-1 named as wrong.</summary>
    Public Property KellyHasSide As Boolean = False
    ''' <summary>Payoff ratio b actually used in f* — the book's (bucket, or session-pool
    ''' on fallback) pooled net payoff Σ(target-fee)/Σ(stop+fee). NOT the live row's own
    ''' placed R:R (that stays on the ATR ENTRY LEVELS rows) — see F-2.</summary>
    Public Property KellyB As Double = 0.0
    ''' <summary>Breakeven win rate at KellyB: 1 / (1 + KellyB).</summary>
    Public Property KellyBreakevenP As Double = 0.0
    ''' <summary>Row count backing KellyPWin/KellyB — the bucket's N, or the session
    ''' pool's N when KellyBucketFallback is True, or the session pool's N in the
    ''' "book below the floor" state (where it is also cfg.Kelly.MinBookRows-short).</summary>
    Public Property KellyBookN As Integer = 0
    ''' <summary>Session bucket name (ASIA/LONDON/NY) the book was pooled over (KO-2 (a):
    ''' the current run's session, not all sessions).</summary>
    Public Property KellyBookSession As String = ""
    ''' <summary>True when the whole session book meets cfg.Kelly.MinBookRows. False ⇒
    ''' the "book below the floor" render state (KO-4/§3.4): no p/b/f* computed.</summary>
    Public Property KellyBookSufficient As Boolean = False
    ''' <summary>Earliest weekday, in-population eval-cache row timestamp (UTC) in the
    ''' session book — renders as "since YYYY-MM-DD" on the book row.</summary>
    Public Property KellyBookSpanStartUtc As DateTime = DateTime.MinValue
    ''' <summary>1-based tercile index (1..3) the live row's own placed net payoff falls
    ''' into, by K-1 (g). 0 when no buckets exist (book below the floor, or no side).</summary>
    Public Property KellyBucketIndex As Integer = 0
    ''' <summary>Lower bound of KellyBucketIndex's b_row range, as measured on the current book.</summary>
    Public Property KellyBucketLo As Double = 0.0
    ''' <summary>Upper bound of KellyBucketIndex's b_row range, as measured on the current book.</summary>
    Public Property KellyBucketHi As Double = 0.0
    ''' <summary>True when KellyBucketIndex's own row count is below cfg.Kelly.MinBookRows,
    ''' so KellyPWin/KellyB/KellyBookN fell back to the session-pooled values (K-1 (g) point 4).
    ''' The basis line must say so on screen when True.</summary>
    Public Property KellyBucketFallback As Boolean = False

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

' ---------------------------------------------------------------------------
' Kelly book (v69, docs/kelly-one-class-placed-payoff-spec.md §3.1/§3.2 + the
' K-1 (g) ruling). Plain data containers, deliberately homed HERE rather than
' on LivePerformanceTracker (where the fold that BUILDS them, ComputeKellyBook,
' lives) — Core/ScoringEngine_Kelly.vb's CalcKellySizing takes a KellyBook
' parameter, and every project that links the ScoringEngine partial class
' (e.g. tools/BacktestRunner.vbproj) must be able to compile that signature
' without also pulling in LivePerformanceTracker.vb's OhlcCache dependency,
' which BacktestRunner has no other reason to reference.
' ---------------------------------------------------------------------------

''' <summary>One geometry tercile of the session book — rows grouped by their own
''' placed net payoff b_row = (target-fee)/(stop+fee). K-1 (g): the live row's p and b
''' come from whichever bucket its own b_row falls into, not the whole-session pool.</summary>
Public Class KellyBucket
    ''' <summary>Lower bound of this bucket's b_row range, as measured on the book.</summary>
    Public Property LoB As Double = 0.0
    ''' <summary>Upper bound of this bucket's b_row range, as measured on the book.</summary>
    Public Property HiB As Double = 0.0
    Public Property N As Integer = 0
    Public Property Successes As Integer = 0
    ''' <summary>Successes / N. 0 when N = 0.</summary>
    Public Property P As Double = 0.0
    ''' <summary>Pooled net payoff over this bucket: Σ(targetᵢ-feeᵢ) / Σ(stopᵢ+feeᵢ).</summary>
    Public Property NetPayoff As Double = 0.0
    ''' <summary>True when N meets cfg.Kelly.MinBookRows. False ⇒ a row landing in this
    ''' bucket falls back to the session-pooled P/NetPayoff (K-1 (g) point 4).</summary>
    Public Property Sufficient As Boolean = False
End Class

''' <summary>The session-scoped population CalcKellySizing reads p and b from (KO-1 (a):
''' the live eval cache; KO-2 (a): the current run's session only). N/Successes/P/NetPayoff
''' are the WHOLE-SESSION pool — the session-pooled fallback values (K-1 (g) option (e))
''' and the "book below the floor" state test. Buckets are the 3 terciles of the pool's
''' own b_row (K-1 (g)); empty when the session pool itself is below the floor. Built by
''' LivePerformanceTracker.ComputeKellyBook (a pure fold over its EvalCacheEntry list).</summary>
Public Class KellyBook
    Public Property Session As String = ""
    Public Property N As Integer = 0
    Public Property Successes As Integer = 0
    ''' <summary>Session-pooled success rate. The K-1 (g) fallback (option (e)) value.</summary>
    Public Property P As Double = 0.0
    ''' <summary>Session-pooled net payoff Σ(target-fee)/Σ(stop+fee). The K-1 (g) fallback value.</summary>
    Public Property NetPayoff As Double = 0.0
    ''' <summary>Earliest weekday, in-population row timestamp (UTC) in the pool.
    ''' DateTime.MinValue when N = 0.</summary>
    Public Property SpanStartUtc As DateTime = DateTime.MinValue
    ''' <summary>True when N meets cfg.Kelly.MinBookRows — gates the "book below the
    ''' floor" render state (KO-4/§3.4). False ⇒ Buckets is empty; nothing else in this
    ''' book is meaningful to render.</summary>
    Public Property Sufficient As Boolean = False
    ''' <summary>3 terciles, ascending by b_row range. Empty when Not Sufficient.</summary>
    Public Property Buckets As New List(Of KellyBucket)
End Class
