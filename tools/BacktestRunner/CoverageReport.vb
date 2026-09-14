' tools/BacktestRunner/CoverageReport.vb
' The `coverage` verb (docs/trade-store-coverage-report-proposal.md, BUILD-AUTHORIZED
' 2026-08-03 — see docs/trade-store-coverage-report-implementer-brief.md).
'
' Reports capture health for the raw-trade store: nine classes per weekday UTC hour
' (captured / defect / trailing-edge / expected-missing / startup-window / not-capturing /
' unknown-scope / out-of-scope-weekend / out-of-scope-declared —
' docs/j-b-scoping-ruling-2026-08-02.md +
' docs/weekday-scope-ruling-2026-08-03.md + docs/coverage-trailing-edge-f1-proposal.md
' §4b), plus S4 candle/funding completeness and an optional S0 venue diff. Tools-only,
' read-only, no settings keys, no version bump.
'
' ── trailing-edge (F1, docs/coverage-trailing-edge-f1-proposal.md) ──
' AccumulateHourStats charges a gap to the hour containing the trade that ENDS it, so an
' hour with trades early and silence to its own end read Captured — the silence was real
' but attributed to the FOLLOWING hour. HourClass.TrailingEdge (D-5(c)) now reports that
' hour on its own terms: rows present, internally clean, but silent from the last trade to
' the observed end. "Observed end" is bounded (D-4(c)) by MIN(the span's own end, the
' evidence boundary, the store's own last in-range trade) so a run with no fresher evidence
' — most manual invocations — does not flag its own final hour just because the tape
' stopped there.
'
' ── Two decisions worth flagging explicitly to the reviewing seat (see the Session 1
'    spec-back) ──
'
' (1) "expected-missing" (S1's uptime join) fires ONLY for hours strictly before the very
'     first evidenced process life — a clean, unambiguous "capture had not started yet"
'     read. Every OTHER form of "no uptime evidence" (a gap between two different process
'     lives, or the open trailing window after the last evidence) is deliberately treated
'     as ambiguous and defaults to Defect, per J-B's residual-ambiguity clause — UNLESS the
'     store itself shows clean captured data for that hour, which always wins as Captured
'     regardless of how ambiguous the uptime read is (positive store evidence outranks an
'     absent/ambiguous uptime signal).
' (2) S1 is entirely skipped (§3 "degrades gracefully") only when BOTH ws_health.log is
'     absent AND analysis_log.csv carries no rows in range — not merely when ws_health.log
'     alone is missing. The proposal's own §2 revision makes analysis_log.csv the PRIMARY
'     uptime record precisely so a missing ws_health.log alone must not blind the report;
'     skipping S1 outright on that condition alone would contradict the primacy the spec
'     just established for the CSV. When skipped, every capturing-scoped hour falls back to
'     a store-only judgment (S2/S3 alone): clean ⇒ Captured, otherwise ⇒ Defect — never
'     ExpectedMissing, since there is no positive evidence to justify calling it a clean
'     "down" period.
'
' Host-agnostic (tools-only). No live HTTP except the optional --verify-venue path. Since
' V-1 (2026-09-14) that path has its OWN page fetch (FetchVenuePageJsonAsync), injected
' behind FetchVenueWindowAsync so fixtures never need a live call — HistoricalStore is linked
' into the engine binary and could not be touched by a no-engine-change build.
'
' Fixtures: A49a–l in verify/ordercheck (A49m — Part B's weekday+liveness pairing — is
' Session 2, per the implementer brief's session split).

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks

Public Enum HourClass
    Captured
    Defect
    ' [F1, D-5.1] Sits between Defect and Captured in the combine precedence at
    ' ClassifyHour's worst-of resolution — a split hour with one clean span and one
    ' trailing-edge span must report TrailingEdge, never Captured. Ordinal position here is
    ' inert (every HourClass reference in the tree is by name — CountByClass, ToString() —
    ' never by ordinal), so the insertion point is free to match that precedence.
    TrailingEdge
    ExpectedMissing
    ' [C-2] The span is inside an up-interval but before the first capture-capable evidence
    ' for that interval — the process was alive (socket not yet established) so it could not
    ' yet capture trades. Sits adjacent to ExpectedMissing in the combine's worst-of chain:
    ' neither class may mask a real Defect elsewhere in the hour, and neither asserts that
    ' capture was possible. ⛔ Ordinal position is inert — the combine checks by name, not
    ' by ordinal; do not rely on declaration order for precedence.
    StartupWindow
    NotCapturing
    UnknownScope
    OutOfScopeWeekend
    ' [C-3a] The hour falls inside a declared intentional-downtime window
    ' (declared_schedule.txt). Checked in ClassifyHour ahead of every uptime/store test,
    ' mirroring OutOfScopeWeekend. ClassifySpan never emits this class — it is a
    ' ClassifyHour gate only. Ordinal position is inert.
    OutOfScopeDeclared
    ' [C-3b] The hour falls inside a venue-outage window: a venue_status.log error line
    ' opened it and the next VENUE_OK (or end of the read range) closes it. ONLY when the
    ' opening code is in CoverageReport.VenueSideCodes — our-fault and unknown codes leave
    ' the hour as Defect (B-1/B-2 guard). Checked in ClassifyHour after OutOfScopeDeclared
    ' and before every uptime/store test. ClassifySpan never emits this class. Ordinal inert.
    OutOfScopeVenue
End Enum

''' <summary>[C-3a] One declared intentional-downtime window from declared_schedule.txt.
''' An hour whose UTC start falls within [StartMs, EndMs) is classified OutOfScopeDeclared
''' ahead of every uptime/store test in ClassifyHour.</summary>
Public Structure DeclaredWindow
    Public StartMs As Long
    ''' <summary>Exclusive: an hour at StartMs is in-window; one at EndMs is not.</summary>
    Public EndMs As Long
    Public Sub New(startMs As Long, endMs As Long)
        Me.StartMs = startMs
        Me.EndMs = endMs
    End Sub
End Structure

''' <summary>[C-3b] One venue-outage window derived from venue_status.log.
''' A venue-error line opens the window; the next VENUE_OK (or the end of the read range)
''' closes it. An hour whose UTC start falls within [StartMs, EndMs) is a candidate for
''' OutOfScopeVenue — but ONLY when IsVenueSide is True (opening code is in
''' CoverageReport.VenueSideCodes). A non-venue-side code leaves the hour as Defect,
''' with Code carried in the Reason string so it is visible rather than lost (B-1/B-2).</summary>
Public Structure VenueWindow
    ''' <summary>UTC ms of the venue-error line that opened this window (inclusive).</summary>
    Public StartMs As Long
    ''' <summary>UTC ms of the VENUE_OK line that closed this window (exclusive).
    ''' Set to the end of the read range when the window was not closed before the range ended.</summary>
    Public EndMs As Long
    ''' <summary>State that opened the window (e.g. "VENUE_503", "VENUE_RPC_11051").
    ''' Carried in the Defect Reason when IsVenueSide is False so our-fault codes are visible.</summary>
    Public Code As String
    ''' <summary>True only when Code is in CoverageReport.VenueSideCodes.
    ''' False for our-fault and unknown codes — those leave the hour as Defect (B-1/B-2 guard).</summary>
    Public IsVenueSide As Boolean
    Public Sub New(startMs As Long, endMs As Long, code As String, isVenueSide As Boolean)
        Me.StartMs = startMs
        Me.EndMs = endMs
        Me.Code = code
        Me.IsVenueSide = isVenueSide
    End Sub
End Structure

Public Class HourResult
    Public Property HourUtc As DateTime
    Public Property Classification As HourClass
    ''' <summary>Diagnostic detail — defect sub-reason, or the instance id(s) bracketing an
    ''' ambiguous gap. Never consumed for classification, display/markdown only.</summary>
    Public Property Reason As String = ""
    Public Property InstanceId As String = ""
    ''' <summary>[coverage-trailing-split-span-spec.md, RULED 2026-09-03] The DECIDING span's
    ''' own trailing gap — bounded by that span's own end (the hour's end when the hour was
    ''' not split), never by the whole hour. Nothing when Classification is not TrailingEdge,
    ''' or when the deciding span's HourStoreStats never set LastTsMs (D-2/D-3(c) guard).
    ''' Structural, not rendered — BuildResult maxes over it into
    ''' CoverageResult.ObservedLongestTrailingMs; BuildConsoleSummary/BuildMarkdown never read
    ''' it directly (T3, confirmed 2026-09-03: neither enumerates HourResult generically).</summary>
    Public Property TrailingMsForHour As Long?
End Class

Public Class CoverageOptions
    Public Property FromUtc As DateTime
    Public Property ToUtc As DateTime
    ''' <summary>D4 — 300,000 ms, confirmed (not provisional) per weekday-scope-ruling-2026
    ''' -08-03.md: derived at 1.85× the observed 2m42s max on an already-weekday sample.</summary>
    Public Property GapMs As Long = 300000L
    Public Property Strict As Boolean = False
    Public Property VerifyVenue As Boolean = False
End Class

''' <summary>[C-2] Whether evidence proves capture capability or mere liveness.
''' A DOWN ws_health line proves the app was alive to write it (WsHealthLog.LogStart fires
''' before the socket connects) but NOT that it was capturing (socket disconnected, feed
''' inactive). OK/DEGRADED/REST lines prove the feed was active. Analysis log rows are
''' capture-capable by construction — a completed analysis run implies capture.</summary>
Public Enum EvidenceKind
    ''' <summary>App was alive — enough to anchor FirstUtcMs. Socket was NOT connected.</summary>
    Liveness
    ''' <summary>App was actively capturing — anchors both FirstUtcMs and CaptureCapableFromMs.</summary>
    CaptureCapable
End Enum

Public Structure EvidencePoint
    Public UtcMs As Long
    Public InstanceId As String
    ''' <summary>[C-2] Whether this point proves capture capability or only liveness.</summary>
    Public Kind As EvidenceKind
    Public Sub New(utcMs As Long, instanceId As String, kind As EvidenceKind)
        Me.UtcMs = utcMs
        Me.InstanceId = If(instanceId, "")
        Me.Kind = kind
    End Sub
End Structure

''' <summary>One process life's evidenced span [FirstUtcMs, LastUtcMs] — deliberately NOT
''' extended past LastUtcMs even when that line isn't a "DOWN" state (A49a: "a process that
''' ends without a DOWN line"). IsTrailing marks the chronologically LAST interval; the
''' region past its LastUtcMs is the open "trailing window", resolved separately.</summary>
Public Class UpInterval
    Public Property InstanceId As String = ""
    Public Property FirstUtcMs As Long
    Public Property LastUtcMs As Long
    Public Property IsTrailing As Boolean = False
    ''' <summary>[C-2] Earliest evidence that the process was actively capturing (socket up /
    ''' analysis log row). Long.MaxValue when no capture-capable evidence was seen for this
    ''' instance — a span whose end is before this value and which overlaps this interval is
    ''' a startup window (connect phase), not a defect. Guard: only fire when this is not
    ''' Long.MaxValue, i.e. capture-capable evidence actually exists for this interval.</summary>
    Public Property CaptureCapableFromMs As Long = Long.MaxValue
End Class

Public Class HourStoreStats
    Public Property RowCount As Integer
    Public Property LongestGapMs As Long
    ''' <summary>[F1, D-2/D-3(c)] The last trade timestamp seen in this bucket. Nullable BY
    ''' RULING — a default of 0 reads as a 1970-01-01 trailing gap on every hand-built fixture
    ''' that never sets it (coverage-trailing-edge-f1-proposal.md §4a.4). Nothing ⇒ the
    ''' trailing-edge check is skipped, never evaluated against a phantom epoch.</summary>
    Public Property LastTsMs As Long?
    ''' <summary>[C-1] Count of rows in this bucket that carry no trade_seq. Non-zero forces
    ''' ClassifySpan to fall back wholly to the time tolerance — a gap across a legacy row is
    ''' uninterpretable (D-2(a)). Zero with Seqs non-empty means every row has a sequence.</summary>
    Public Property RowsWithoutSeq As Integer = 0
    ''' <summary>[C-1] trade_seq values seen in this bucket, for the contiguity check in
    ''' ClassifySpan. Populated during AccumulateHourStats / AccumulateSplitSpanStats.
    ''' Sorted on demand by ClassifySpan (accumulation walks in timestamp order, which only
    ''' correlates with sequence order). Empty when no rows carry trade_seq.</summary>
    Public Property Seqs As New List(Of Long)()
End Class

Public Class CoverageResult
    Public Property FromUtc As DateTime
    Public Property ToUtc As DateTime
    Public Property CaptureBeginsUtc As DateTime?
    Public Property PreCaptureDaysExcluded As Integer
    Public Property PostBoundaryHoursExcluded As Integer
    Public Property Hours As New List(Of HourResult)
    Public Property S1Skipped As Boolean = False
    Public Property S1SkipReason As String = ""
    Public Property GapMs As Long
    Public Property ObservedLongestGapMs As Long
    Public Property GapBreachHours As Integer

    ''' <summary>[F1, D-6(c)] Reported BESIDE ObservedLongestGapMs/GapBreachHours, never folded
    ''' into them — D-6's re-ruling found folding is a no-op on the gap metric (the straddling
    ''' gap that produces a trailing edge is always ≥ the trailing edge itself and is already
    ''' counted there) and a silent double-count on the hour metric (the hour the gap ENDS in
    ''' already counts the breach). See coverage-trailing-edge-f1-proposal.md §4a.3.</summary>
    Public Property TrailingEdgeHours As Integer
    Public Property ObservedLongestTrailingMs As Long

    Public Property CandleHave As New Dictionary(Of Integer, Integer)
    Public Property CandleExpected As New Dictionary(Of Integer, Integer)
    Public Property FundingHave As Integer
    Public Property FundingExpected As Integer

    Public Property VenueRan As Boolean = False
    Public Property VenueDiff As VenueDiffResult = Nothing
    Public Property VenueCoveredFromUtc As DateTime?
    Public Property VenueCoveredToUtc As DateTime?
    ''' <summary>[V-4] The paginated check's verdict and counts. Nothing when --verify-venue was
    ''' not passed.</summary>
    Public Property VenueCheck As CoverageReport.VenueCheckResult = Nothing

    ''' <summary>[trade identity §3.3] Sequence-gap read over the same window the hourly walk
    ''' covers. Nothing when not computed.</summary>
    Public Property SequenceGaps As SequenceGapResult = Nothing

    ''' <summary>Trades the venue reported and the store does not hold. Convenience passthrough
    ''' so existing readers keep working; the two MATCH populations live on
    ''' <see cref="VenueDiff"/> and are deliberately not summed into anything.</summary>
    Public ReadOnly Property VenueMissingTrades As List(Of TradeRecord)
        Get
            If VenueDiff Is Nothing Then Return New List(Of TradeRecord)
            Return VenueDiff.MissingTrades
        End Get
    End Property

    Public Function CountByClass(cls As HourClass) As Integer
        ' .Where(...).Count() rather than .Count(predicate) — List(Of T)'s own zero-arg
        ' Count PROPERTY shadows the Enumerable.Count(predicate) extension method, which VB
        ' resolves as an indexer on the property's Integer return type instead (BC32016).
        Return Hours.Where(Function(h) h.Classification = cls).Count()
    End Function
End Class

''' <summary>
''' [trade identity / D4] The S0 venue diff, reported as TWO match populations plus the misses.
''' They are never summed. An identity match is exact; a fallback match is a five-field
''' coincidence that MIGHT be the same trade. Blending them into one "matched" number would
''' restore precisely the ambiguity this build removes.
''' </summary>
Public Class VenueDiffResult
    ''' <summary>Venue trades the store does not hold under either matching arm.</summary>
    Public Property MissingTrades As New List(Of TradeRecord)

    ''' <summary>Venue trades matched EXACTLY — both sides carried a trade_id and they agreed.</summary>
    Public Property IdentityMatched As Integer = 0

    ''' <summary>Venue trades matched only on the five legacy fields, because one side had no
    ''' identity. Ambiguous by construction — this count is a CEILING on real matches.</summary>
    Public Property FallbackMatched As Integer = 0

    ''' <summary>Store rows in the window that carry an identity.</summary>
    Public Property StoreIdentified As Integer = 0

    ''' <summary>Store rows in the window written before identity shipped. While this is
    ''' non-zero the fallback arm is load-bearing and the diff cannot be read as exact.</summary>
    Public Property StoreLegacyOnly As Integer = 0

    Public ReadOnly Property TotalMatched As Integer
        Get
            ' Exposed for a row count only. Do NOT render this as a quality figure — the two
            ' populations carry different evidential weight and the report prints them apart.
            Return IdentityMatched + FallbackMatched
        End Get
    End Property
End Class

''' <summary>
''' [trade identity / §3.3] Local completeness from trade_seq alone — no network, no venue call,
''' and no exposure to Deribit's ~24 h trade retention.
''' </summary>
Public Class SequenceGapResult
    Public Property RowsWithSeq As Integer = 0

    ''' <summary>⚠ Rows carrying NO sequence. Non-zero means the walk below is partial. A store
    ''' of pure legacy rows reports zero gaps because there is nothing to check, which is not
    ''' the same as being complete.</summary>
    Public Property RowsWithoutSeq As Integer = 0

    Public Property FirstSeq As Long = -1
    Public Property LastSeq As Long = -1

    ''' <summary>Sequence numbers provably absent between FirstSeq and LastSeq.</summary>
    Public Property MissingCount As Long = 0

    ''' <summary>Contiguous runs of absence. Many small runs = scattered loss, which is exactly
    ''' what the S3 longest-gap metric cannot see.</summary>
    Public Property GapRuns As Integer = 0

    Public Property LongestGap As Long = 0

    ''' <summary>Repeated sequence numbers — a duplicate that survived dedup.</summary>
    Public Property DuplicateSeqs As Integer = 0

    ''' <summary>Backwards steps. Reported, never counted as loss: whether Deribit ever resets
    ''' trade_seq was NOT verified, so a negative step means "cannot interpret".</summary>
    Public Property Discontinuities As Integer = 0

    Public ReadOnly Property Checkable As Boolean
        Get
            Return RowsWithSeq > 1
        End Get
    End Property
End Class

Public NotInheritable Class CoverageReport

    Private Sub New()
    End Sub

    Public Const HourMs As Long = 3600000L

    ' [C-3b, D-5] Venue-side error codes that cause ClassifyHour to emit OutOfScopeVenue.
    ' Public per the standing ruling: a Private const forces fixtures to restate the literal.
    ' NOT a settings.json key (D-5 rejects it) — this is an empirically-observed set, not
    ' operational configuration. An unrecognised code fails safe toward Defect (B-2).
    ' ⚠ 11051 is carried from the 2026-08-11 observation, NOT verified against Deribit docs.
    ' The design tolerates that: an unknown code fails safe toward Defect (never a false excuse).
    Public Shared ReadOnly VenueSideCodes As New HashSet(Of String)(StringComparer.Ordinal) From {
        "VENUE_500", "VENUE_502", "VENUE_503", "VENUE_504",
        "VENUE_RPC_11051"}

    ' ── Evidence parsing ──────────────────────────────────────────────────────────────

    ''' <summary>Parse ws_health.log lines ("utc | state | instance_id") into evidence
    ''' points. For interval MEMBERSHIP (FirstUtcMs), the STATE value is deliberately
    ''' IGNORED — even a DOWN line proves the app was alive to write it (WsHealthLog.LogStart
    ''' fires before the socket connects), so every line is equally valid "app was up at this
    ''' instant" evidence regardless of state. For CAPTURE CAPABILITY (CaptureCapableFromMs),
    ''' the state IS consulted: OK/DEGRADED/REST carry EvidenceKind.CaptureCapable (the feed
    ''' was active); DOWN carries EvidenceKind.Liveness only (socket disconnected, not
    ''' capturing). Malformed lines are skipped, never throws.</summary>
    Public Shared Function ParseWsHealthEvidence(lines As IEnumerable(Of String)) As List(Of EvidencePoint)
        Dim result As New List(Of EvidencePoint)
        If lines Is Nothing Then Return result
        For Each line In lines
            If String.IsNullOrWhiteSpace(line) Then Continue For
            Dim parts = line.Split({" | "}, StringSplitOptions.None)
            If parts.Length < 3 Then Continue For
            Dim utc As DateTime
            If Not DateTime.TryParse(parts(0), CultureInfo.InvariantCulture,
                                     DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, utc) Then
                Continue For
            End If
            ' DOWN means the socket is disconnected — the process was alive to write the line
            ' (liveness only), but was not capturing. Every other state (OK/DEGRADED/REST)
            ' means the feed was active and the process was capture-capable.
            Dim kind As EvidenceKind = If(parts(1).Trim() = "DOWN",
                                          EvidenceKind.Liveness, EvidenceKind.CaptureCapable)
            result.Add(New EvidencePoint(
                New DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                parts(2).Trim(), kind))
        Next
        Return result
    End Function

    ''' <summary>Parse analysis_log.csv rows into evidence points — S1's PRIMARY record
    ''' (~60s heartbeat, InstanceId-attributed). Column indices resolve from the file's OWN
    ''' header rather than a hardcoded position, so a future header rotation cannot silently
    ''' misattribute a column. First line is always treated as header.</summary>
    Public Shared Function ParseAnalysisLogEvidence(lines As IEnumerable(Of String)) As List(Of EvidencePoint)
        Dim result As New List(Of EvidencePoint)
        If lines Is Nothing Then Return result
        Dim tsIdx As Integer = -1
        Dim iidIdx As Integer = -1
        Dim headerSeen As Boolean = False
        For Each line In lines
            If String.IsNullOrWhiteSpace(line) Then Continue For
            If Not headerSeen Then
                headerSeen = True
                Dim cols = line.Split(","c)
                tsIdx = Array.IndexOf(cols, "Timestamp")
                iidIdx = Array.IndexOf(cols, "InstanceId")
                Continue For
            End If
            If tsIdx < 0 OrElse iidIdx < 0 Then Continue For
            Dim parts = line.Split(","c)
            If parts.Length <= Math.Max(tsIdx, iidIdx) Then Continue For
            Dim ts As DateTime
            If Not DateTime.TryParse(parts(tsIdx), CultureInfo.InvariantCulture,
                                     DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, ts) Then
                Continue For
            End If
            ' Analysis log rows are capture-capable by construction — a completed analysis
            ' run implies the collection feed was active at that timestamp.
            result.Add(New EvidencePoint(
                New DateTimeOffset(DateTime.SpecifyKind(ts, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                parts(iidIdx), EvidenceKind.CaptureCapable))
        Next
        Return result
    End Function

    ''' <summary>[C-3a] Parse a declared_schedule.txt file into DeclaredWindow instances.
    ''' Format: comment lines begin with '#'; data lines are "YYYY-MM-DD HH:MM | YYYY-MM-DD HH:MM"
    ''' (start inclusive, end exclusive, both UTC). Malformed lines are silently skipped.
    ''' Returns an empty list when path is Nothing/empty or the file does not exist.</summary>
    Public Shared Function ParseDeclaredSchedule(path As String) As List(Of DeclaredWindow)
        Dim result As New List(Of DeclaredWindow)
        If String.IsNullOrEmpty(path) OrElse Not File.Exists(path) Then Return result
        For Each line In File.ReadAllLines(path)
            Dim trimmed As String = line.Trim()
            If String.IsNullOrEmpty(trimmed) OrElse trimmed.StartsWith("#") Then Continue For
            Dim sep As Integer = trimmed.IndexOf("|"c)
            If sep < 0 Then Continue For
            Dim startStr As String = trimmed.Substring(0, sep).Trim()
            Dim endStr As String = trimmed.Substring(sep + 1).Trim()
            Dim startDt, endDt As DateTime
            If Not DateTime.TryParseExact(startStr, "yyyy-MM-dd HH:mm",
                                          CultureInfo.InvariantCulture,
                                          DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal,
                                          startDt) Then Continue For
            If Not DateTime.TryParseExact(endStr, "yyyy-MM-dd HH:mm",
                                          CultureInfo.InvariantCulture,
                                          DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal,
                                          endDt) Then Continue For
            If endDt <= startDt Then Continue For
            result.Add(New DeclaredWindow(
                New DateTimeOffset(DateTime.SpecifyKind(startDt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                New DateTimeOffset(DateTime.SpecifyKind(endDt, DateTimeKind.Utc)).ToUnixTimeMilliseconds()))
        Next
        Return result
    End Function

    ''' <summary>[C-3b] Parse venue_status.log lines ("utc | state | instance_id") into
    ''' VenueWindow instances. A venue-error state line opens a window; VENUE_OK closes it.
    ''' An unterminated window (no VENUE_OK before the end of the read range) closes at
    ''' rangeEndMs (A77d). Non-VENUE_* lines and malformed lines are silently skipped.
    ''' Consecutive error lines close the previous window and open a new one.
    ''' Returns an empty list when lines is Nothing or empty — no change to output (AC-6).</summary>
    Public Shared Function ParseVenueWindows(lines As IEnumerable(Of String),
                                              rangeEndMs As Long) As List(Of VenueWindow)
        Dim result As New List(Of VenueWindow)
        If lines Is Nothing Then Return result

        Dim openStart As Long = 0
        Dim openCode As String = Nothing
        Dim openVenueSide As Boolean = False
        Dim inWindow As Boolean = False

        For Each line In lines
            If String.IsNullOrWhiteSpace(line) Then Continue For
            Dim parts() As String = line.Split(New String() {" | "}, StringSplitOptions.None)
            If parts.Length < 2 Then Continue For

            Dim dt As DateTimeOffset
            If Not DateTimeOffset.TryParseExact(parts(0).Trim(),
                                                "yyyy-MM-ddTHH:mm:ss.fffZ",
                                                CultureInfo.InvariantCulture,
                                                DateTimeStyles.AssumeUniversal, dt) Then Continue For
            Dim utcMs As Long = dt.ToUnixTimeMilliseconds()
            Dim state As String = parts(1).Trim()

            If state = "VENUE_OK" Then
                If inWindow Then
                    result.Add(New VenueWindow(openStart, utcMs, openCode, openVenueSide))
                    inWindow = False
                    openCode = Nothing
                End If
            ElseIf state.StartsWith("VENUE_", StringComparison.Ordinal) Then
                ' A new error line: close any previous open window, then open a new one.
                If inWindow Then
                    result.Add(New VenueWindow(openStart, utcMs, openCode, openVenueSide))
                End If
                openStart = utcMs
                openCode = state
                openVenueSide = VenueSideCodes.Contains(state)
                inWindow = True
            End If
        Next

        ' Unterminated window: close at the end of the read range (A77d).
        If inWindow Then
            result.Add(New VenueWindow(openStart, rangeEndMs, openCode, openVenueSide))
        End If

        Return result
    End Function

    ''' <summary>Merge evidence from every source into per-instance up-intervals (A49a). Each
    ''' interval is bounded to exactly its evidenced span [First, Last] — never extended past
    ''' its last line regardless of that line's state. The chronologically LAST interval is
    ''' flagged IsTrailing.</summary>
    Public Shared Function BuildUpIntervals(evidence As IEnumerable(Of EvidencePoint)) As List(Of UpInterval)
        Dim result As New List(Of UpInterval)
        If evidence Is Nothing Then Return result
        Dim byInstance As New Dictionary(Of String, UpInterval)
        For Each e In evidence
            Dim key As String = If(e.InstanceId, "")
            If String.IsNullOrEmpty(key) Then Continue For
            Dim iv As UpInterval = Nothing
            If Not byInstance.TryGetValue(key, iv) Then
                iv = New UpInterval With {
                    .InstanceId = key, .FirstUtcMs = e.UtcMs, .LastUtcMs = e.UtcMs,
                    .CaptureCapableFromMs = If(e.Kind = EvidenceKind.CaptureCapable, e.UtcMs, Long.MaxValue)}
                byInstance(key) = iv
                result.Add(iv)
            Else
                If e.UtcMs < iv.FirstUtcMs Then iv.FirstUtcMs = e.UtcMs
                If e.UtcMs > iv.LastUtcMs Then iv.LastUtcMs = e.UtcMs
                ' [C-2] Track the earliest capture-capable evidence for this interval.
                If e.Kind = EvidenceKind.CaptureCapable AndAlso e.UtcMs < iv.CaptureCapableFromMs Then
                    iv.CaptureCapableFromMs = e.UtcMs
                End If
            End If
        Next
        result.Sort(Function(a, b) a.FirstUtcMs.CompareTo(b.FirstUtcMs))
        If result.Count > 0 Then result(result.Count - 1).IsTrailing = True
        Return result
    End Function

    ''' <summary>Classify an hour-start timestamp against the up-interval list.
    ''' "up" = the [hourStart, hourEnd] window OVERLAPS some instance's evidenced span —
    '''   NOT merely "the hour-start instant falls inside it". Evidence is scattered
    '''   throughout an hour (a ~60s heartbeat lands a few minutes past the hour boundary,
    '''   never exactly on it), so checking containment of the hour-START INSTANT ALONE
    '''   would miss every hour whose evidence doesn't happen to straddle :00 (found by
    '''   A49i during Session 1 — see the fixture's own comment).
    ''' "before-first" = no earlier instance exists at all — capture had not started yet
    '''   (clean, unambiguous).
    ''' "trailing" = past the chronologically LAST instance's last evidence (the walk itself
    '''   stops at the boundary — see ResolveBoundaryUtc — so this is always the short
    '''   genuine residual, never an unbounded stretch).
    ''' "cross-guid" = between two DIFFERENT instances' evidence — a restart happened
    '''   somewhere inside; we cannot tell a graceful restart from an unnoticed crash.
    ''' trailing and cross-guid are BOTH ambiguous and share the same downstream default.</summary>
    Public Shared Function ClassifyUptime(hourStartMs As Long, upIntervals As List(Of UpInterval)) _
            As (Kind As String, InstanceId As String)
        Return ClassifyUptimeSpan(hourStartMs, hourStartMs + HourMs - 1, upIntervals)
    End Function

    ''' <summary>[SH-1] Same walk as <see cref="ClassifyUptime"/>, generalised to an arbitrary
    ''' [spanStartMs, spanEndMsInclusive] span rather than a fixed whole hour — the piece a
    ''' split sub-span needs to resolve its own uptime read. ClassifyUptime is now the
    ''' whole-hour special case of this.</summary>
    Public Shared Function ClassifyUptimeSpan(spanStartMs As Long, spanEndMsInclusive As Long,
                                              upIntervals As List(Of UpInterval)) _
            As (Kind As String, InstanceId As String)
        If upIntervals IsNot Nothing Then
            For Each iv In upIntervals
                If spanEndMsInclusive >= iv.FirstUtcMs AndAlso spanStartMs <= iv.LastUtcMs Then
                    Return ("up", iv.InstanceId)
                End If
            Next
        End If

        Dim prevIv As UpInterval = Nothing
        Dim nextIv As UpInterval = Nothing
        If upIntervals IsNot Nothing Then
            For Each iv In upIntervals
                If iv.LastUtcMs < spanStartMs Then
                    If prevIv Is Nothing OrElse iv.LastUtcMs > prevIv.LastUtcMs Then prevIv = iv
                ElseIf iv.FirstUtcMs > spanEndMsInclusive Then
                    If nextIv Is Nothing OrElse iv.FirstUtcMs < nextIv.FirstUtcMs Then nextIv = iv
                End If
            Next
        End If

        If prevIv Is Nothing Then
            Return ("before-first", If(nextIv IsNot Nothing, nextIv.InstanceId, ""))
        End If
        If nextIv Is Nothing Then
            Return ("trailing", prevIv.InstanceId)
        End If
        Return ("cross-guid", prevIv.InstanceId & "→" & nextIv.InstanceId)
    End Function

    ' ── D7 marker scope join ──────────────────────────────────────────────────────────

    ''' <summary>Resolve capture scope for an hour from D7's marker records (chronological).
    ''' The record with the greatest UtcMs ≤ hourStartMs governs — a process reads its
    ''' settings once at start, so that reading scopes everything until the NEXT recorded
    ''' process start. No applicable record ⇒ "unknown" (pre-marker history, or a copy-back
    ''' that dropped the marker file).</summary>
    Public Shared Function ResolveScope(hourStartMs As Long, markers As List(Of CaptureMarkerLog.MarkerRecord)) _
            As (Kind As String, InstanceId As String)
        If markers Is Nothing OrElse markers.Count = 0 Then Return ("unknown", "")
        Dim applicable As CaptureMarkerLog.MarkerRecord = Nothing
        For Each m In markers
            If m.UtcMs <= hourStartMs Then
                If applicable Is Nothing OrElse m.UtcMs > applicable.UtcMs Then applicable = m
            End If
        Next
        If applicable Is Nothing Then Return ("unknown", "")
        Return (If(applicable.Enabled, "on", "off"), applicable.InstanceId)
    End Function

    ' ── S2 / S3 — per-hour store stats ────────────────────────────────────────────────

    ''' <summary>Stream the store's trade files month by month — never materialising the
    ''' whole multi-month range at once (§10: HistoricalStore.LoadTradeRange is the wrong
    ''' tool for a 6-month window) — and fold into per-UTC-hour counters. Each month is
    ''' individually whole-row-deduped + sorted (bounded to one month in memory at a time),
    ''' carrying the previous trade's timestamp across month boundaries so a gap spanning
    ''' the seam is still measured correctly.
    '''
    ''' [F1, D-4(c)] Also returns StoreEndMs — the last trade timestamp actually WITHIN
    ''' [fromUtc, toUtc), Nothing if none. This is deliberately NOT the same as the largest
    ''' LastTsMs across the returned ByHour dictionary: EnumerateMonths reads WHOLE month
    ''' files, so ByHour is a superset of the requested range (§4a.1/§8) — a trade sitting
    ''' past toUtc in the same file must never leak into this value, or it silently
    ''' un-exempts the true last hour of the walk (coverage-trailing-edge-f1-proposal.md
    ''' §4a.1, fixture F1-e).</summary>
    Public Shared Function AccumulateHourStats(storeDir As String, fromUtc As DateTime, toUtc As DateTime) _
            As (ByHour As Dictionary(Of Long, HourStoreStats), StoreEndMs As Long?)
        Dim byHour As New Dictionary(Of Long, HourStoreStats)
        Dim prevTs As Long = Long.MinValue
        Dim havePrev As Boolean = False
        Dim fromMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        Dim toMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        Dim storeEndMs As Long? = Nothing

        For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
            Dim path As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            Dim rows = TradeStoreWriter.ReadTradeFile(path)
            If rows.Count = 0 Then Continue For
            ' [trade identity] Was whole-row equality on the five legacy fields, which merged
            ' distinct trades that shared them and so UNDER-counted every hour they fell in.
            ' Now the one §3.4 contract, shared with LoadTradeRange.
            Dim deduped = TradeStoreWriter.DedupTrades(rows)
            deduped.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))

            For Each t In deduped
                Dim hourStartMs As Long = (t.Timestamp \ HourMs) * HourMs
                Dim stats As HourStoreStats = Nothing
                If Not byHour.TryGetValue(hourStartMs, stats) Then
                    stats = New HourStoreStats()
                    byHour(hourStartMs) = stats
                End If
                stats.RowCount += 1
                stats.LastTsMs = t.Timestamp
                ' [C-1] Track sequences for per-hour contiguity check in ClassifySpan.
                If t.HasSeq Then stats.Seqs.Add(t.TradeSeq) Else stats.RowsWithoutSeq += 1
                If havePrev Then
                    Dim gap As Long = t.Timestamp - prevTs
                    If gap > stats.LongestGapMs Then stats.LongestGapMs = gap
                End If
                prevTs = t.Timestamp
                havePrev = True
                If t.Timestamp >= fromMs AndAlso t.Timestamp < toMs Then storeEndMs = t.Timestamp
            Next
        Next
        Return (byHour, storeEndMs)
    End Function

    ''' <summary>[SH-1 §4.2 route (b)] A second, targeted-by-caller pass for hours a D7 marker
    ''' splits — rare, deploy/toggle only. <see cref="AccumulateHourStats"/>'s hot path stays
    ''' whole-hour and untouched; this walks the SAME [fromUtc, toUtc) range once more, keyed
    ''' by `spanBoundsByHour` (hourStartMs → each span's start ms, ascending, first entry
    ''' always the hour start itself). `prevTs` is carried CONTINUOUSLY across the whole walk —
    ''' never reset at a span or hour boundary — so a gap that starts before a span's own first
    ''' row (slip 2: straddling the marker, or straddling the hour before it) is still
    ''' attributed to the span it lands in, exactly like the whole-hour accumulator's own
    ''' cross-boundary carry.</summary>
    Public Shared Function AccumulateSplitSpanStats(storeDir As String, fromUtc As DateTime, toUtc As DateTime,
                                                     spanBoundsByHour As Dictionary(Of Long, List(Of Long))) _
            As Dictionary(Of Long, HourStoreStats)
        Dim bySpan As New Dictionary(Of Long, HourStoreStats)
        If spanBoundsByHour Is Nothing OrElse spanBoundsByHour.Count = 0 Then Return bySpan

        Dim prevTs As Long = Long.MinValue
        Dim havePrev As Boolean = False

        For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
            Dim path As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            Dim rows = TradeStoreWriter.ReadTradeFile(path)
            If rows.Count = 0 Then Continue For
            Dim deduped = TradeStoreWriter.DedupTrades(rows)
            deduped.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))

            For Each t In deduped
                Dim hourStartMs As Long = (t.Timestamp \ HourMs) * HourMs
                Dim bounds As List(Of Long) = Nothing
                If spanBoundsByHour.TryGetValue(hourStartMs, bounds) Then
                    Dim spanStartMs As Long = SpanStartFor(t.Timestamp, bounds)
                    Dim stats As HourStoreStats = Nothing
                    If Not bySpan.TryGetValue(spanStartMs, stats) Then
                        stats = New HourStoreStats()
                        bySpan(spanStartMs) = stats
                    End If
                    stats.RowCount += 1
                    stats.LastTsMs = t.Timestamp
                    ' [C-1] Track sequences for per-span contiguity check in ClassifySpan.
                    If t.HasSeq Then stats.Seqs.Add(t.TradeSeq) Else stats.RowsWithoutSeq += 1
                    If havePrev Then
                        Dim gap As Long = t.Timestamp - prevTs
                        If gap > stats.LongestGapMs Then stats.LongestGapMs = gap
                    End If
                End If
                prevTs = t.Timestamp
                havePrev = True
            Next
        Next
        Return bySpan
    End Function

    ''' <summary>The last entry in `boundsAscending` that is ≤ tsMs — which span a trade's
    ''' timestamp falls into. `boundsAscending(0)` is always the hour start, so this always
    ''' resolves to some entry.</summary>
    Private Shared Function SpanStartFor(tsMs As Long, boundsAscending As List(Of Long)) As Long
        Dim result As Long = boundsAscending(0)
        For Each b In boundsAscending
            If b <= tsMs Then result = b Else Exit For
        Next
        Return result
    End Function

    ' ── Per-hour classification ───────────────────────────────────────────────────────

    ''' <summary>One sub-span's classify — the same checks <see cref="ClassifyHour"/> ran
    ''' inline pre-SH-1, parameterised on an already-resolved scope and an already-scoped
    ''' stats/uptime read so both the whole-hour path and the split path share one
    ''' implementation.
    '''
    ''' [F1, D-1/D-4(c)] `boundMs` is the caller-resolved MIN of the evidence boundary and the
    ''' store's own last in-range trade (BuildResult computes it once; a direct caller — e.g.
    ''' a fixture bypassing BuildResult — may pass Long.MaxValue for "unconstrained"). The
    ''' observed end of this span is Min(spanEndMsInclusive, boundMs); a span that is
    ''' otherwise clean but silent from its last trade to that observed end past `gapMs`
    ''' reports TrailingEdge rather than Captured — the D-3(c) nullable LastTsMs is the guard,
    ''' so a hand-built HourStoreStats that never sets it is never evaluated.</summary>
    Private Shared Function ClassifySpan(spanStartMs As Long, spanEndMsInclusive As Long,
                                         scopeKind As String, scopeInstanceId As String,
                                         upIntervals As List(Of UpInterval),
                                         s1Skipped As Boolean,
                                         spanStats As HourStoreStats,
                                         gapMs As Long,
                                         boundMs As Long) _
            As (Classification As HourClass, InstanceId As String, Reason As String, TrailingMs As Long?)
        If scopeKind = "unknown" Then
            Return (HourClass.UnknownScope, "", "", Nothing)
        End If
        If scopeKind = "off" Then
            Return (HourClass.NotCapturing, scopeInstanceId, "", Nothing)
        End If

        Dim stats As HourStoreStats = If(spanStats, New HourStoreStats())

        ' [C-1] storeClean: prefer the sequence signal when ALL rows carry trade_seq (D-1/D-2).
        ' If ANY row lacks a sequence, fall back wholly to the time tolerance — a gap across a
        ' legacy row is uninterpretable; evaluating only the sequenced subset would compute a
        ' number that means nothing (D-2(a): MECHANISM argument, not simplicity).
        Dim storeClean As Boolean
        If stats.RowsWithoutSeq = 0 AndAlso stats.Seqs.Count > 0 Then
            ' Every row has trade_seq — check contiguity directly. A contiguous span is
            ' Captured regardless of LongestGapMs (the single confirmed false defect: a 302 s
            ' quiet period with zero missing sequences on 2026-08-13 21:00).
            Dim sortedSeqs = stats.Seqs.OrderBy(Function(s) s).ToList()
            Dim seqContiguous As Boolean = True
            For i As Integer = 1 To sortedSeqs.Count - 1
                If sortedSeqs(i) - sortedSeqs(i - 1) > 1 Then
                    seqContiguous = False
                    Exit For
                End If
            Next
            storeClean = seqContiguous  ' RowCount > 0 is implied by Seqs.Count > 0
        Else
            ' Mixed or legacy span: fall back to the time tolerance.
            storeClean = stats.RowCount > 0 AndAlso stats.LongestGapMs <= gapMs
        End If

        If storeClean Then
            Dim observedEndMs As Long = Math.Min(spanEndMsInclusive, boundMs)
            If stats.LastTsMs.HasValue AndAlso observedEndMs > stats.LastTsMs.Value Then
                Dim trailingMs As Long = observedEndMs - stats.LastTsMs.Value
                If trailingMs > gapMs Then
                    Return (HourClass.TrailingEdge, "", "trailing-edge(" & trailingMs & "ms)", trailingMs)
                End If
            End If
            Return (HourClass.Captured, "", "", Nothing)
        End If

        If s1Skipped Then
            Dim skipReason As String = If(stats.RowCount = 0, "empty(S1 skipped)",
                                          "gap-breach(S1 skipped," & stats.LongestGapMs & "ms)")
            Return (HourClass.Defect, "", skipReason, Nothing)
        End If

        Dim up = ClassifyUptimeSpan(spanStartMs, spanEndMsInclusive, upIntervals)
        If up.Kind = "before-first" Then
            Return (HourClass.ExpectedMissing, up.InstanceId, "", Nothing)
        End If

        ' [C-2] Startup window: span is inside an up-interval but ends before the first
        ' capture-capable evidence in that interval — the process was alive (a DOWN line anchors
        ' the interval's start) but the feed had not yet established. Not a defect: absence was
        ' expected during the connect phase. Guard: CaptureCapableFromMs < Long.MaxValue ensures
        ' we fire only when capture-capable evidence actually exists for this interval.
        If up.Kind = "up" AndAlso upIntervals IsNot Nothing Then
            Dim matchIv = upIntervals.FirstOrDefault(Function(iv) iv.InstanceId = up.InstanceId)
            If matchIv IsNot Nothing AndAlso
               matchIv.CaptureCapableFromMs < Long.MaxValue AndAlso
               spanEndMsInclusive < matchIv.CaptureCapableFromMs Then
                Return (HourClass.StartupWindow, up.InstanceId, "startup-window", Nothing)
            End If
        End If

        Dim reason As String = If(up.Kind <> "up", "ambiguous-uptime(" & up.Kind & ")",
                                  If(stats.RowCount = 0, "empty", "gap-breach(" & stats.LongestGapMs & "ms)"))
        Return (HourClass.Defect, up.InstanceId, reason, Nothing)
    End Function

    ''' <summary>[SH-1] The nine-class per-hour verdict. Positive store evidence (clean rows)
    ''' always wins as Captured, regardless of how ambiguous the uptime read is. See the
    ''' file-header note for the ExpectedMissing / S1-skipped design decisions.
    '''
    ''' An hour containing a D7 marker strictly inside it (hourStartMs &lt; UtcMs ≤ hourEndMs —
    ''' a marker landing exactly ON hourStartMs was already handled by ResolveScope's `≤`) is
    ''' SPLIT at every such marker and each part classified against the scope that governed it.
    ''' Ruling (docs/coverage-split-hour-implementer-brief.md §2): the hour is DEFECT if EITHER
    ''' part is DEFECT. [F1, D-5.1] TrailingEdge sits directly below Defect and above Captured —
    ''' a split hour with one clean span and one trailing-edge span reports TrailingEdge, never
    ''' Captured (the SH-1 defect reproduced in miniature if this precedence is wrong). D-2:
    ''' when no part is Defect or TrailingEdge but the parts disagree, Captured wins — positive
    ''' store evidence outranks an ambiguous/absent uptime or scope read, same precedence the
    ''' whole-hour path already used. Output stays ONE ROW PER HOUR (D-1); the split detail goes
    ''' in Reason only.
    ''' `spanStats` carries the route-(b) targeted pass's per-span stats (see
    ''' AccumulateSplitSpanStats) — Nothing/absent is fine for an hour that turns out not to be
    ''' split. `observedBoundMs` [F1, D-4(c)] is the caller-resolved trailing-edge bound —
    ''' BuildResult computes MIN(the evidence boundary, the store's own last in-range trade) once
    ''' and threads it here; the default Long.MaxValue ("unconstrained") is what every pre-F1
    ''' direct caller (fixtures that never set HourStoreStats.LastTsMs) keeps running under
    ''' unchanged, since the trailing check is gated on that field being set at all.</summary>
    Public Shared Function ClassifyHour(hourStartUtc As DateTime,
                                        markers As List(Of CaptureMarkerLog.MarkerRecord),
                                        upIntervals As List(Of UpInterval),
                                        s1Skipped As Boolean,
                                        hourStats As HourStoreStats,
                                        gapMs As Long,
                                        Optional spanStats As Dictionary(Of Long, HourStoreStats) = Nothing,
                                        Optional observedBoundMs As Long = Long.MaxValue,
                                        Optional declaredWindows As List(Of DeclaredWindow) = Nothing,
                                        Optional venueWindows As List(Of VenueWindow) = Nothing) As HourResult
        Dim result As New HourResult With {.HourUtc = hourStartUtc}

        ' hourStartMs is computed before the weekend/declared checks so both gates can reuse it.
        Dim hourStartMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(hourStartUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()

        If hourStartUtc.DayOfWeek = DayOfWeek.Saturday OrElse hourStartUtc.DayOfWeek = DayOfWeek.Sunday Then
            result.Classification = HourClass.OutOfScopeWeekend
            Return result
        End If

        ' [C-3a] Declared intentional-downtime window: checked after weekend (weekend is more
        ' fundamental) but before every uptime/store test. ClassifySpan never emits this class.
        If declaredWindows IsNot Nothing Then
            For Each w In declaredWindows
                If hourStartMs >= w.StartMs AndAlso hourStartMs < w.EndMs Then
                    result.Classification = HourClass.OutOfScopeDeclared
                    Return result
                End If
            Next
        End If

        ' [C-3b] Venue-outage window: checked after OutOfScopeDeclared, before every
        ' uptime/store test. ClassifySpan never emits OutOfScopeVenue (B-4).
        ' ⛔ B-1/B-2 guard: ONLY venue-side codes scope out. Our-fault and unknown codes
        ' leave the hour for normal classification; their code is collected here and appended
        ' to the Reason string so it is visible rather than lost.
        Dim nonVenueCode As String = Nothing
        If venueWindows IsNot Nothing Then
            For Each vw In venueWindows
                If hourStartMs >= vw.StartMs AndAlso hourStartMs < vw.EndMs Then
                    If vw.IsVenueSide Then
                        result.Classification = HourClass.OutOfScopeVenue
                        Return result
                    Else
                        nonVenueCode = vw.Code   ' annotate Defect reason below (B-1/B-2)
                        Exit For
                    End If
                End If
            Next
        End If

        Dim hourEndMs As Long = hourStartMs + HourMs - 1

        Dim splitMarkers = If(markers, New List(Of CaptureMarkerLog.MarkerRecord)).
            Where(Function(m) m.UtcMs > hourStartMs AndAlso m.UtcMs <= hourEndMs).
            OrderBy(Function(m) m.UtcMs).ToList()

        If splitMarkers.Count = 0 Then
            Dim scope = ResolveScope(hourStartMs, markers)
            Dim only = ClassifySpan(hourStartMs, hourEndMs, scope.Kind, scope.InstanceId,
                                    upIntervals, s1Skipped, hourStats, gapMs, observedBoundMs)
            result.Classification = only.Classification
            result.InstanceId = only.InstanceId
            result.Reason = only.Reason
            result.TrailingMsForHour = only.TrailingMs
            ' [C-3b] Annotate non-venue-side code so it is visible in the Reason (B-1/B-2).
            If nonVenueCode IsNot Nothing Then
                result.Reason = If(String.IsNullOrEmpty(result.Reason),
                                   nonVenueCode,
                                   result.Reason & " [" & nonVenueCode & "]")
            End If
            Return result
        End If

        ' Split hour (rare — a deploy or a capture toggle landed inside it). Build N+1 spans:
        ' [hourStart, marker1) governed by the pre-flip scope, then [markerK, markerK+1) each
        ' governed by markerK's own Enabled, the last running to hourEnd.
        Dim boundaries As New List(Of Long) From {hourStartMs}
        boundaries.AddRange(splitMarkers.Select(Function(m) m.UtcMs))
        Dim preFlipScope = ResolveScope(hourStartMs, markers)

        Dim spans As New List(Of (Classification As HourClass, InstanceId As String, Reason As String, SpanStartMs As Long, TrailingMs As Long?))
        For i As Integer = 0 To boundaries.Count - 1
            Dim spanStartMs As Long = boundaries(i)
            Dim spanEndMsIncl As Long = If(i + 1 < boundaries.Count, boundaries(i + 1) - 1, hourEndMs)
            Dim scopeKind As String
            Dim scopeIid As String
            If i = 0 Then
                scopeKind = preFlipScope.Kind
                scopeIid = preFlipScope.InstanceId
            Else
                Dim marker = splitMarkers(i - 1)
                scopeKind = If(marker.Enabled, "on", "off")
                scopeIid = marker.InstanceId
            End If
            Dim spanStat As HourStoreStats = Nothing
            If spanStats IsNot Nothing Then spanStats.TryGetValue(spanStartMs, spanStat)
            Dim cls = ClassifySpan(spanStartMs, spanEndMsIncl, scopeKind, scopeIid,
                                   upIntervals, s1Skipped, spanStat, gapMs, observedBoundMs)
            spans.Add((cls.Classification, cls.InstanceId, cls.Reason, spanStartMs, cls.TrailingMs))
        Next

        ' Worst-of, per the ruling: any Defect ⇒ Defect. [F1, D-5.1] Else any TrailingEdge ⇒
        ' TrailingEdge — a span silent to the observed edge outranks a sibling span that
        ' happens to be clean, exactly as a Defect span would; placing this check BELOW
        ' Captured would launder a genuine trailing-edge span into Captured, the SH-1 defect
        ' reproduced in miniature (fixture F1-d). D-2: else Captured wins on a disagreement.
        ' [D-3, RULED 2026-08-13 — docs/coverage-split-hour-implementer-brief.md §5a] The
        ' residual (no Defect, no TrailingEdge, no Captured) orders UnknownScope >
        ' ExpectedMissing > NotCapturing: bottom-placing UnknownScope would launder an
        ' uncharacterisable span into a confident label (the SH-1 defect in miniature), and it
        ' would silently reverse ClassifySpan's own precedence, which already checks unknown
        ' BEFORE off on the single-scope path. ExpectedMissing > NotCapturing stands —
        ' NotCapturing asserts a deliberate off-state that a span with no such record cannot
        ' honestly claim.
        ' [C-2, D-4] StartupWindow sits adjacent to ExpectedMissing — both are "absence we
        ' expected" — and neither may mask a real Defect elsewhere in the hour. ⛔ It must NOT
        ' outrank Defect, TrailingEdge, Captured, or UnknownScope. Ordinal position is inert.
        Dim finalCls As HourClass
        If spans.Any(Function(s) s.Classification = HourClass.Defect) Then
            finalCls = HourClass.Defect
        ElseIf spans.Any(Function(s) s.Classification = HourClass.TrailingEdge) Then
            finalCls = HourClass.TrailingEdge
        ElseIf spans.Any(Function(s) s.Classification = HourClass.Captured) Then
            finalCls = HourClass.Captured
        ElseIf spans.Any(Function(s) s.Classification = HourClass.UnknownScope) Then
            finalCls = HourClass.UnknownScope
        ElseIf spans.Any(Function(s) s.Classification = HourClass.ExpectedMissing) Then
            finalCls = HourClass.ExpectedMissing
        ElseIf spans.Any(Function(s) s.Classification = HourClass.StartupWindow) Then
            finalCls = HourClass.StartupWindow
        Else
            ' [coverage-trailing-split-span-spec.md R8, RULED 2026-09-03] Reached only because
            ' ClassifySpan never emits OutOfScopeWeekend or OutOfScopeDeclared — the eighth and
            ' ninth HourClasses, both handled before this function's span logic. If that ever
            ' changes, this Else and the combine's exhaustiveness must be revisited together.
            finalCls = HourClass.NotCapturing
        End If

        ' [R8, AMENDED at review 2026-09-03] Membership check FIRST, then First() —
        ' deliberately NOT FirstOrDefault plus a Classification comparison. A default value
        ' tuple carries Classification = CType(0, HourClass), so comparing it against finalCls
        ' is an ORDINAL read — and this enum's own comment (see HourClass above) states that
        ' ordinal position is INERT and the insertion point is FREE. Reordering so that
        ' NotCapturing sat at ordinal 0 would have silently DISARMED this guard on exactly the
        ' case it exists to catch, via an edit the enum explicitly authorises. This form
        ' depends on no ordinal, and First() provably cannot throw after the check.
        If Not spans.Any(Function(s) s.Classification = finalCls) Then
            Throw New InvalidOperationException(
                "ClassifyHour: the combine selected finalCls=" & finalCls.ToString() &
                " but no span in this split hour carries that classification. " &
                "See the Else branch's comment above — this should be unreachable.")
        End If
        Dim winner = spans.First(Function(s) s.Classification = finalCls)
        result.Classification = finalCls
        result.InstanceId = winner.InstanceId
        ' [coverage-trailing-split-span-spec.md R7, handback 2026-09-04] MAX over every span
        ' matching finalCls, not just the first — an hour can carry TWO TrailingEdge spans and
        ' ObservedLongestTrailingMs is itself a maximum, so `winner.TrailingMs` alone could
        ' silently report the SMALLER of the two. Restricted to finalCls (not all spans) so the
        ' Nothing-unless-TrailingEdge invariant survives for free: when finalCls isn't
        ' TrailingEdge, every matching span's TrailingMs is Nothing (ClassifySpan only ever
        ' pairs a non-Nothing TrailingMs with a TrailingEdge return), and Max() over an
        ' all-Nothing sequence is Nothing.
        result.TrailingMsForHour = spans.Where(Function(s) s.Classification = finalCls).
                                         Select(Function(s) s.TrailingMs).Max()

        Dim markerTimes As New List(Of String)
        For Each m In splitMarkers
            markerTimes.Add(DateTimeOffset.FromUnixTimeMilliseconds(m.UtcMs).UtcDateTime.ToString("HH:mm"))
        Next
        Dim spanParts As New List(Of String)
        For Each s In spans
            Dim spanTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(s.SpanStartMs).UtcDateTime
            Dim reasonSuffix As String = If(String.IsNullOrEmpty(s.Reason), "", "(" & s.Reason & ")")
            spanParts.Add("[" & spanTimeUtc.ToString("HH:mm") & "] " & s.Classification.ToString() & reasonSuffix)
        Next
        result.Reason = "split@" & String.Join(",", markerTimes) & " :: " & String.Join(" | ", spanParts)
        ' [C-3b] Annotate non-venue-side code in the split-hour reason too (B-1/B-2).
        If nonVenueCode IsNot Nothing Then
            result.Reason = result.Reason & " [" & nonVenueCode & "]"
        End If
        Return result
    End Function

    ' ── Capture-era self-bounding + trailing boundary ─────────────────────────────────

    ''' <summary>The store's first-ever trade timestamp within [fromUtc, toUtc] — coverage
    ''' is reported from here, not from --from (§3). Nothing ⇒ the store has no data in
    ''' range at all.</summary>
    Public Shared Function ResolveCaptureBeginsUtc(storeDir As String, fromUtc As DateTime, toUtc As DateTime) _
            As DateTime?
        For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
            Dim path As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            Dim rows = TradeStoreWriter.ReadTradeFile(path)
            If rows.Count > 0 Then
                Dim firstTs As Long = rows.Min(Function(r) r.Timestamp)
                Return DateTimeOffset.FromUnixTimeMilliseconds(firstTs).UtcDateTime
            End If
        Next
        Return Nothing
    End Function

    ''' <summary>The far edge of the "trusted" window — bounded by the newest evidence found
    ''' across S1's sources, never by DateTime.UtcNow. Re-running this report later against
    ''' the SAME stale copy-back must not grow the trailing window every day it goes
    ''' un-refreshed (both rulings' "every AWS copy-back reads as a fresh death" warning).
    ''' Capped at --to so a report never manufactures hours past what was actually
    ''' requested.</summary>
    Public Shared Function ResolveBoundaryUtc(evidence As List(Of EvidencePoint), toUtc As DateTime) As DateTime
        If evidence Is Nothing OrElse evidence.Count = 0 Then Return toUtc
        Dim newest As Long = evidence.Max(Function(e) e.UtcMs)
        Dim newestUtc As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(newest).UtcDateTime
        Return If(newestUtc < toUtc, newestUtc, toUtc)
    End Function

    ' ── S4 — candle / funding completeness ────────────────────────────────────────────

    Private Shared Function CoverageCandleFileFor(storeDir As String, resolution As Integer,
                                                  year As Integer, month As Integer) As String
        Return Path.Combine(storeDir, String.Format(CultureInfo.InvariantCulture,
                            "candles_{0}m_{1:D4}-{2:D2}.csv", resolution, year, month))
    End Function

    Private Shared Function CoverageFundingFileFor(storeDir As String, year As Integer, month As Integer) As String
        Return Path.Combine(storeDir, String.Format(CultureInfo.InvariantCulture,
                            "funding_{0:D4}-{1:D2}.csv", year, month))
    End Function

    Public Shared Function ComputeCandleCompleteness(storeDir As String, fromUtc As DateTime, toUtc As DateTime) _
            As Dictionary(Of Integer, (Have As Integer, Expected As Integer))
        Dim result As New Dictionary(Of Integer, (Have As Integer, Expected As Integer))
        For Each res In New Integer() {1, 3, 5, 15}
            Dim have As Integer = 0
            Dim expected As Integer = 0
            Dim intervalMs As Long = CLng(res) * 60000L
            For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
                Dim path As String = CoverageCandleFileFor(storeDir, res, m.Year, m.Month)
                Dim rows = StoreFiles.LoadCandleFile(path)
                Dim segStartMs As Long =
                    New DateTimeOffset(DateTime.SpecifyKind(m.StartUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                Dim segEndMs As Long =
                    New DateTimeOffset(DateTime.SpecifyKind(m.EndUtcExcl, DateTimeKind.Utc)).ToUnixTimeMilliseconds() - 1
                have += StoreFiles.CountCandlesInRange(rows, segStartMs, segEndMs)
                expected += StoreFiles.ExpectedGridPoints(segStartMs, segEndMs, intervalMs)
            Next
            result(res) = (have, expected)
        Next
        Return result
    End Function

    Public Shared Function ComputeFundingCompleteness(storeDir As String, fromUtc As DateTime, toUtc As DateTime) _
            As (Have As Integer, Expected As Integer)
        Dim have As Integer = 0
        Dim expected As Integer = 0
        For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
            Dim path As String = CoverageFundingFileFor(storeDir, m.Year, m.Month)
            Dim rows = StoreFiles.LoadFundingFile(path)
            Dim segStartMs As Long =
                New DateTimeOffset(DateTime.SpecifyKind(m.StartUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
            Dim segEndMs As Long =
                New DateTimeOffset(DateTime.SpecifyKind(m.EndUtcExcl, DateTimeKind.Utc)).ToUnixTimeMilliseconds() - 1
            have += StoreFiles.CountFundingInRange(rows, segStartMs, segEndMs)
            expected += HistoricalStore.ExpectedFundingSamples(segStartMs, segEndMs)
        Next
        Return (have, expected)
    End Function

    ' ── S0 — venue diff (optional, --verify-venue) ────────────────────────────────────

    ''' <summary>
    ''' Trades present at the venue but absent from the store — the diff IS the loss,
    ''' enumerated exactly, never estimated. Pure — no HTTP — so fixtures exercise it with a
    ''' stubbed venue list (A49j, A53g).
    '''
    ''' [trade identity / D4] Matching is now two-population, not one:
    '''   • IDENTITY-MATCHED — both sides carry a trade_id and the ids are equal. Exact.
    '''   • FALLBACK-MATCHED — either side lacks an identity, so the five legacy fields decide.
    '''     Ambiguous by construction: distinct trades can share all five.
    ''' The counts are reported SEPARATELY and never blended. A single number would hide exactly
    ''' the ambiguity this build exists to remove — it would let a store of legacy rows report a
    ''' confident match rate that is really a five-field coincidence rate.
    ''' </summary>
    Public Shared Function ComputeVenueDiff(storeTrades As List(Of TradeRecord),
                                            venueTrades As List(Of TradeRecord)) As VenueDiffResult
        Dim result As New VenueDiffResult()

        Dim storeIds As New HashSet(Of String)(StringComparer.Ordinal)
        Dim storeLegacy As New HashSet(Of String)(StringComparer.Ordinal)
        If storeTrades IsNot Nothing Then
            For Each t In storeTrades
                If t.HasIdentity Then storeIds.Add(t.TradeId)
                ' EVERY store row contributes a legacy key, identified or not — the fallback arm
                ' must be able to match a venue trade against an identified store row whose id
                ' the venue happens not to carry.
                storeLegacy.Add(TradeStoreWriter.LegacyRowKey(t))
                If t.HasIdentity Then result.StoreIdentified += 1 Else result.StoreLegacyOnly += 1
            Next
        End If

        If venueTrades IsNot Nothing Then
            For Each t In venueTrades
                If t.HasIdentity AndAlso storeIds.Contains(t.TradeId) Then
                    result.IdentityMatched += 1
                ElseIf storeLegacy.Contains(TradeStoreWriter.LegacyRowKey(t)) Then
                    result.FallbackMatched += 1
                Else
                    result.MissingTrades.Add(t)
                End If
            Next
        End If

        Return result
    End Function

    ' ── Sequence-gap detection (§3.3) ─────────────────────────────────────────────────
    ' The property that makes trade_seq worth taking, and the reason it outranks trade_id in
    ' §3.3: a per-instrument MONOTONIC sequence makes completeness a LOCAL computation. A store
    ' holding 100, 101, 103 is provably missing 102 — with no venue call, no network, and no
    ' exposure to Deribit's ~24 h trade retention. A month-old file can be checked at any time.
    '
    ' ⚠ It supplements S0, it does not replace it (D6). A sequence proves CONTINUITY; only the
    ' venue diff proves the stored rows AGREE WITH THE VENUE on content.

    ''' <summary>
    ''' Walk a trade list in sequence order and report the holes.
    '''
    ''' ⚠ Rows WITHOUT a sequence are counted and reported, never silently skipped. A store of
    ''' pure legacy rows has no sequences at all, so a naive walk finds zero gaps and reads as
    ''' PERFECT — the worst possible failure for a completeness instrument. RowsWithoutSeq is
    ''' what stops a reader mistaking "nothing to check" for "nothing wrong".
    '''
    ''' A negative step (a sequence reset, or two feeds interleaved) is reported as
    ''' <see cref="SequenceGapResult.Discontinuities"/> rather than counted as loss: we did not
    ''' verify that Deribit never resets trade_seq, so a backwards step is "cannot interpret",
    ''' not "missing trades".
    ''' </summary>
    Public Shared Function ComputeSequenceGaps(trades As IEnumerable(Of TradeRecord)) As SequenceGapResult
        Dim seqs As New List(Of Long)()
        Dim withoutSeq As Integer = 0
        If trades IsNot Nothing Then
            For Each t In trades
                If t.HasSeq Then seqs.Add(t.TradeSeq) Else withoutSeq += 1
            Next
        End If
        Return FoldSequenceGaps(seqs, withoutSeq)
    End Function

    ''' <summary>Stream the store month by month and fold the sequence walk — never
    ''' materialising the whole multi-month range at once (the §10 constraint
    ''' AccumulateHourStats already obeys). Only the sequence NUMBERS are carried across
    ''' months, not the trade records.</summary>
    Public Shared Function AccumulateSequenceGaps(storeDir As String, fromUtc As DateTime, toUtc As DateTime) _
            As SequenceGapResult
        Dim seqs As New List(Of Long)()
        Dim withoutSeq As Integer = 0
        Dim fromMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        Dim toMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()

        For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
            Dim rows = TradeStoreWriter.ReadTradeFile(TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month))
            If rows.Count = 0 Then Continue For
            For Each t In TradeStoreWriter.DedupTrades(rows)
                If t.Timestamp < fromMs OrElse t.Timestamp >= toMs Then Continue For
                If t.HasSeq Then seqs.Add(t.TradeSeq) Else withoutSeq += 1
            Next
        Next
        Return FoldSequenceGaps(seqs, withoutSeq)
    End Function

    Private Shared Function FoldSequenceGaps(seqs As List(Of Long), withoutSeq As Integer) As SequenceGapResult
        Dim r As New SequenceGapResult()
        r.RowsWithoutSeq = withoutSeq
        If seqs Is Nothing OrElse seqs.Count = 0 Then Return r
        r.RowsWithSeq = seqs.Count

        seqs.Sort()
        r.FirstSeq = seqs(0)
        r.LastSeq = seqs(seqs.Count - 1)

        Dim prev As Long = seqs(0)
        For i As Integer = 1 To seqs.Count - 1
            Dim cur As Long = seqs(i)
            Dim delta As Long = cur - prev
            If delta = 0 Then
                r.DuplicateSeqs += 1
            ElseIf delta < 0 Then
                r.Discontinuities += 1
            ElseIf delta > 1 Then
                r.GapRuns += 1
                r.MissingCount += (delta - 1)
                If delta - 1 > r.LongestGap Then r.LongestGap = delta - 1
            End If
            prev = cur
        Next
        Return r
    End Function

    ' ── S0 venue check — paginated fetch, windowed store read, verdict ─────────────────
    ' [V-1 / V-4, docs/venue-check-schedule-plan.md §2; ruling docs/venue-check-plan-review-
    ' 2026-09-14.md] The previous RunVenueDiffAsync made ONE call with count=1000. Measured
    ' 2026-09-14: a 24 h window came back as exactly 1,000 trades and the report printed
    ' "0 missing" for the whole day. Two venue facts, both verified live 2026-09-14, shape the
    ' loop below: `start_timestamp` is INCLUSIVE for this endpoint, and a window past Deribit's
    ' ~24 h retention returns `trades: []` with `has_more: false` — so an EMPTY or SHORT venue
    ' list is not evidence of a complete store. VerdictVenueShort exists for exactly that.
    '
    ' ⚠ A second HTTP path, deliberately. HistoricalStore.vb is linked into the ENGINE binary
    ' (DeribitVerdictEngine.vbproj, for TradeStoreGapRepair) and this build is ruled
    ' no-engine-change. Trade fields parse as HistoricalStore parses them, through the same
    ' shared TradeRecord.ReadTradeId / ReadTradeSeq readers.

    Public Const VerdictClean As String = "CLEAN"
    Public Const VerdictLoss As String = "LOSS"
    Public Const VerdictInexact As String = "INEXACT"
    Public Const VerdictVenueShort As String = "VENUE_SHORT"
    Public Const VerdictNotRun As String = "NOT_RUN"

    ''' <summary>Deribit's documented cap for get_last_trades_*.</summary>
    Public Const VenuePageSize As Integer = 1000
    ''' <summary>Runaway guard. ~25 pages cover a normal 13 h window.</summary>
    Public Const VenueMaxPages As Integer = 5000

    Public Class VenuePage
        Public Property Trades As New List(Of TradeRecord)
        ''' <summary>Deribit's `has_more`. Nothing when the response omitted it.</summary>
        Public Property HasMore As Boolean?
    End Class

    Public Class VenueFetchResult
        ''' <summary>True only when every page was fetched and parsed. A partial list is never
        ''' diffed: it would under-report loss and read as clean.</summary>
        Public Property Ok As Boolean
        Public Property FailReason As String = ""
        ''' <summary>De-duplicated, inside [start, end], ascending by (timestamp, trade_seq).</summary>
        Public Property Trades As New List(Of TradeRecord)
        Public Property Pages As Integer
        ''' <summary>Every response body exactly as received, for --venue-dump. Kept on failure
        ''' too: the venue's history is gone after ~24 h, so these are the only copy.</summary>
        Public Property RawPages As New List(Of (StartMs As Long, EndMs As Long, Json As String))
    End Class

    Public Class VenueCheckResult
        Public Property WindowStartMs As Long
        Public Property WindowEndMs As Long
        Public Property Verdict As String = VerdictNotRun
        Public Property Reason As String = ""
        ''' <summary>Nothing when the check did not run — never a zero-filled diff.</summary>
        Public Property Diff As VenueDiffResult
        Public Property VenueTrades As Integer
        Public Property StoreTrades As Integer
        ''' <summary>Store rows in the window that fall before the venue's first trade or after its
        ''' last. Non-zero means the venue list does not cover the window (retention, or a fetch
        ''' that ended early), whatever the missing count says.</summary>
        Public Property StoreOutsideVenueSpan As Integer
        Public Property Pages As Integer
        Public Property VenueFirstTs As Long?
        Public Property VenueLastTs As Long?
    End Class

    ''' <summary>Parse one get_last_trades_by_instrument_and_time response. Nothing on any
    ''' malformed body.</summary>
    Public Shared Function ParseVenueTradesPage(json As String) As VenuePage
        If String.IsNullOrEmpty(json) Then Return Nothing
        Try
            Using doc = System.Text.Json.JsonDocument.Parse(json)
                Dim result = doc.RootElement.GetProperty("result")
                Dim page As New VenuePage()
                Dim hm As System.Text.Json.JsonElement = Nothing
                If result.TryGetProperty("has_more", hm) AndAlso
                   (hm.ValueKind = System.Text.Json.JsonValueKind.True OrElse
                    hm.ValueKind = System.Text.Json.JsonValueKind.False) Then
                    page.HasMore = hm.GetBoolean()
                End If
                For Each t In result.GetProperty("trades").EnumerateArray()
                    Dim rec As New TradeRecord()
                    rec.Price = t.GetProperty("price").GetDouble()
                    rec.Amount = t.GetProperty("amount").GetDouble()
                    rec.Direction = t.GetProperty("direction").GetString()
                    rec.Timestamp = t.GetProperty("timestamp").GetInt64()
                    Dim liqEl As System.Text.Json.JsonElement = Nothing
                    rec.Liquidation = If(t.TryGetProperty("liquidation", liqEl), liqEl.GetString(), "none")
                    rec.TradeId = TradeRecord.ReadTradeId(t)
                    rec.TradeSeq = TradeRecord.ReadTradeSeq(t)
                    page.Trades.Add(rec)
                Next
                Return page
            End Using
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Fetch every venue trade in [startMs, endMs]. <paramref name="fetchPage"/> returns one
    ''' response body, or Nothing on failure — injected so fixtures A78b run with no network.
    '''
    ''' ⛔ The cursor restarts AT the newest millisecond of the previous page, never one past it.
    ''' HistoricalStore.BackfillTradeMonthAsync uses `newestMs + 1`, which skips any trade that
    ''' shares the last page's final millisecond but did not fit on it. The re-fetched overlap is
    ''' removed by trade_id. A full page that never leaves one millisecond cannot advance and
    ''' fails loudly rather than looping or silently dropping trades.
    ''' </summary>
    Public Shared Async Function FetchVenueWindowAsync(startMs As Long, endMs As Long,
            fetchPage As Func(Of Long, Long, Integer, Task(Of String)),
            Optional pageDelayMs As Integer = 200) As Task(Of VenueFetchResult)
        Dim r As New VenueFetchResult()
        Dim seen As New HashSet(Of String)(StringComparer.Ordinal)
        Dim cursor As Long = startMs
        Do
            If r.Pages >= VenueMaxPages Then
                r.FailReason = "page cap of " & VenueMaxPages & " hit at cursor " & cursor
                Return r
            End If
            Dim json As String = Await fetchPage(cursor, endMs, VenuePageSize)
            If json Is Nothing Then
                r.FailReason = "fetch failed at cursor " & cursor & " after " & r.Pages & " page(s)"
                Return r
            End If
            r.RawPages.Add((cursor, endMs, json))
            Dim page = ParseVenueTradesPage(json)
            If page Is Nothing Then
                r.FailReason = "unparseable response at cursor " & cursor
                Return r
            End If
            r.Pages += 1

            Dim newest As Long = cursor
            For Each t In page.Trades
                If t.Timestamp > newest Then newest = t.Timestamp
                If t.Timestamp < startMs OrElse t.Timestamp > endMs Then Continue For
                Dim key As String = If(t.HasIdentity, "ID|" & t.TradeId, "LK|" & TradeStoreWriter.LegacyRowKey(t))
                If seen.Add(key) Then r.Trades.Add(t)
            Next

            If page.Trades.Count = 0 Then Exit Do
            ' Prefer the venue's own flag; fall back to "a full page means there may be more".
            Dim more As Boolean = If(page.HasMore.HasValue, page.HasMore.Value, page.Trades.Count >= VenuePageSize)
            If Not more Then Exit Do
            If newest <= cursor Then
                r.FailReason = "stall: a full page inside one millisecond at cursor " & cursor
                Return r
            End If
            cursor = newest
            If pageDelayMs > 0 Then Await Task.Delay(pageDelayMs)
        Loop

        r.Trades.Sort(Function(a, b) If(a.Timestamp <> b.Timestamp,
                                        a.Timestamp.CompareTo(b.Timestamp),
                                        a.TradeSeq.CompareTo(b.TradeSeq)))
        r.Ok = True
        Return r
    End Function

    Private Shared ReadOnly _venueHttp As System.Net.Http.HttpClient = CreateVenueHttp()

    Private Shared Function CreateVenueHttp() As System.Net.Http.HttpClient
        Dim c As New System.Net.Http.HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}
        c.DefaultRequestHeaders.UserAgent.ParseAdd("BacktestRunner-coverage/1.0")
        Return c
    End Function

    ''' <summary>The production page source for <see cref="FetchVenueWindowAsync"/>. Retries once
    ''' on a 5xx or a timeout (HistoricalStore's discipline); Nothing on any other failure.</summary>
    Public Shared Async Function FetchVenuePageJsonAsync(startMs As Long, endMs As Long, count As Integer) _
            As Task(Of String)
        Dim url As String = "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time" &
                            "?instrument_name=" & HistoricalStore.InstrumentName &
                            "&start_timestamp=" & startMs &
                            "&end_timestamp=" & endMs &
                            "&count=" & count &
                            "&sorting=asc"
        For attempt As Integer = 1 To 2
            Try
                Return Await _venueHttp.GetStringAsync(url)
            Catch ex As System.Net.Http.HttpRequestException When attempt < 2 AndAlso
                    ex.StatusCode.HasValue AndAlso CInt(ex.StatusCode.Value) >= 500
                ' fall through to the retry delay
            Catch ex As TaskCanceledException When attempt < 2
                ' fall through to the retry delay
            Catch ex As Exception
                Console.Error.WriteLine("[CoverageReport] venue page fetch failed: " & ex.Message)
                Return Nothing
            End Try
            Await Task.Delay(500)
        Next
        Return Nothing
    End Function

    ''' <summary>
    ''' Store rows in [startMs, endMs], streamed month by month — never a whole month held in
    ''' memory. <paramref name="ok"/> is False on ANY read exception: the shipped
    ''' TradeStoreWriter.ReadTradeFile swallows those and returns a short list, which here would
    ''' report false loss or a false clean.
    '''
    ''' ⛔ Opens with FileShare.ReadWrite. The live writer (TradeStoreWriter.AppendRows) holds its
    ''' StreamWriter with share Read; a plain StreamReader both fails against an open writer AND,
    ''' while it holds the file, makes the writer's own open fail — and the writer drops that
    ''' batch (B-3, docs/venue-check-schedule-plan.md). Tools-side only: the Core readers are
    ''' unchanged (V-3 is held).
    ''' </summary>
    Public Shared Function ReadStoreWindow(storeDir As String, startMs As Long, endMs As Long,
                                           ByRef ok As Boolean, ByRef failReason As String) As List(Of TradeRecord)
        Dim rows As New List(Of TradeRecord)
        ok = True
        failReason = ""
        Dim startUtc = DateTimeOffset.FromUnixTimeMilliseconds(startMs).UtcDateTime
        Dim endUtc = DateTimeOffset.FromUnixTimeMilliseconds(endMs).UtcDateTime
        For Each m In HistoricalStore.EnumerateMonths(startUtc, endUtc.AddMilliseconds(1))
            Dim monthPath As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            If Not File.Exists(monthPath) Then Continue For
            Try
                Using fs As New FileStream(monthPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                      sr As New StreamReader(fs)
                    sr.ReadLine()   ' header
                    Do
                        Dim line As String = sr.ReadLine()
                        If line Is Nothing Then Exit Do
                        Dim rec As New TradeRecord()
                        If TradeStoreWriter.TryParseRow(line, rec) AndAlso
                           rec.Timestamp >= startMs AndAlso rec.Timestamp <= endMs Then rows.Add(rec)
                    Loop
                End Using
            Catch ex As Exception
                ok = False
                failReason = "STORE_READ " & Path.GetFileName(monthPath) & ": " & ex.Message
                Return rows
            End Try
        Next
        Return rows
    End Function

    ''' <summary>
    ''' THE verdict mapping, most severe first. Pure, so fixture A78c pins every arm.
    ''' NOT_RUN — the fetch or the store read failed, or neither side holds a trade.
    ''' VENUE_SHORT — store rows lie outside the venue's span: the venue list does not cover the
    '''   window, so a zero missing count proves nothing. Outranks LOSS; the line keeps the count.
    ''' LOSS — venue trades absent from the store. INEXACT — store rows without trade_id, so
    ''' fallback matching is ambiguous. CLEAN — none of the above.
    ''' </summary>
    Public Shared Function ComputeVenueVerdict(fetchOk As Boolean, fetchFailReason As String,
                                               storeReadOk As Boolean, storeFailReason As String,
                                               venueTrades As Integer, storeTrades As Integer,
                                               storeOutsideVenueSpan As Integer, missing As Integer,
                                               storeLegacyOnly As Integer) As (Verdict As String, Reason As String)
        If Not fetchOk Then Return (VerdictNotRun, "VENUE_FETCH " & fetchFailReason)
        If Not storeReadOk Then Return (VerdictNotRun, storeFailReason)
        If venueTrades = 0 AndAlso storeTrades = 0 Then
            Return (VerdictNotRun, "NO_TRADES neither the venue nor the store holds a trade in the window")
        End If
        If storeOutsideVenueSpan > 0 Then
            Return (VerdictVenueShort, String.Format(CultureInfo.InvariantCulture,
                "venue history does not cover the window: {0} store row(s) outside the venue span; {1} missing inside it",
                storeOutsideVenueSpan, missing))
        End If
        If missing > 0 Then
            Return (VerdictLoss, String.Format(CultureInfo.InvariantCulture,
                "{0} venue trade(s) absent from the store", missing))
        End If
        If storeLegacyOnly > 0 Then
            Return (VerdictInexact, String.Format(CultureInfo.InvariantCulture,
                "{0} store row(s) carry no trade_id; fallback matching is ambiguous", storeLegacyOnly))
        End If
        Return (VerdictClean, "")
    End Function

    ''' <summary>--strict exit codes for a venue verdict. 1 stays "bad args / DEFECT hours".</summary>
    Public Shared Function VenueExitCode(verdict As String) As Integer
        Select Case verdict
            Case VerdictClean : Return 0
            Case VerdictLoss : Return 3
            Case VerdictNotRun : Return 4
            Case VerdictInexact : Return 5
            Case VerdictVenueShort : Return 6
            Case Else : Return 4
        End Select
    End Function

    Public Shared Function BuildVenueCheck(storeDir As String, startMs As Long, endMs As Long,
                                           fetch As VenueFetchResult) As VenueCheckResult
        Dim c As New VenueCheckResult With {.WindowStartMs = startMs, .WindowEndMs = endMs}
        If fetch Is Nothing OrElse Not fetch.Ok Then
            c.Pages = If(fetch Is Nothing, 0, fetch.Pages)
            Dim nr = ComputeVenueVerdict(False, If(fetch Is Nothing, "no fetch result", fetch.FailReason),
                                         True, "", 0, 0, 0, 0, 0)
            c.Verdict = nr.Verdict
            c.Reason = nr.Reason
            Return c
        End If

        c.Pages = fetch.Pages
        c.VenueTrades = fetch.Trades.Count
        If c.VenueTrades > 0 Then
            c.VenueFirstTs = fetch.Trades.Min(Function(t) t.Timestamp)
            c.VenueLastTs = fetch.Trades.Max(Function(t) t.Timestamp)
        End If

        Dim readOk As Boolean
        Dim readWhy As String = ""
        Dim store = ReadStoreWindow(storeDir, startMs, endMs, readOk, readWhy)
        If Not readOk Then
            Dim sr = ComputeVenueVerdict(True, "", False, readWhy, c.VenueTrades, 0, 0, 0, 0)
            c.Verdict = sr.Verdict
            c.Reason = sr.Reason
            Return c
        End If

        c.StoreTrades = store.Count
        Dim firstTs As Long? = c.VenueFirstTs
        Dim lastTs As Long? = c.VenueLastTs
        c.StoreOutsideVenueSpan = store.Where(Function(t) Not firstTs.HasValue OrElse
                                                  t.Timestamp < firstTs.Value OrElse
                                                  t.Timestamp > lastTs.Value).Count()
        c.Diff = ComputeVenueDiff(store, fetch.Trades)
        Dim v = ComputeVenueVerdict(True, "", True, "", c.VenueTrades, c.StoreTrades,
                                    c.StoreOutsideVenueSpan, c.Diff.MissingTrades.Count, c.Diff.StoreLegacyOnly)
        c.Verdict = v.Verdict
        c.Reason = v.Reason
        Return c
    End Function

    ''' <summary>"true" only when the store's trade_seq walk is checkable, gap-free, fully
    ''' sequenced and never steps backwards; "false" when it found missing sequence numbers;
    ''' "unknown" otherwise. The column the option-A sample review turns on: LOSS with
    ''' seq_contiguous=true disproves the gap-free-counter assumption.</summary>
    Public Shared Function SeqContiguousLabel(sg As SequenceGapResult) As String
        If sg Is Nothing Then Return "unknown"
        If sg.MissingCount > 0 Then Return "false"
        If sg.Checkable AndAlso sg.RowsWithoutSeq = 0 AndAlso sg.Discontinuities = 0 Then Return "true"
        Return "unknown"
    End Function

    ''' <summary>The build's commit, from AssemblyInformationalVersion ("1.0.0+&lt;sha&gt;[-dirty]",
    ''' stamped by the SDK plus BacktestRunner.vbproj's MarkToolCommitDirty target).</summary>
    Public Shared Function ResolveToolCommit() As String
        Try
            Dim asm = System.Reflection.Assembly.GetEntryAssembly()
            If asm Is Nothing Then Return "unknown"
            Dim attr = CType(Attribute.GetCustomAttribute(asm, GetType(System.Reflection.AssemblyInformationalVersionAttribute)),
                             System.Reflection.AssemblyInformationalVersionAttribute)
            Return ToolCommitFromInformationalVersion(If(attr Is Nothing, Nothing, attr.InformationalVersion))
        Catch
            Return "unknown"
        End Try
    End Function

    Public Shared Function ToolCommitFromInformationalVersion(v As String) As String
        If String.IsNullOrEmpty(v) Then Return "unknown"
        Dim plus As Integer = v.IndexOf("+"c)
        If plus < 0 OrElse plus = v.Length - 1 Then Return "unknown"
        Dim rev As String = v.Substring(plus + 1)
        Dim suffix As String = ""
        Dim dash As Integer = rev.IndexOf("-"c)
        If dash >= 0 Then
            suffix = rev.Substring(dash)
            rev = rev.Substring(0, dash)
        End If
        If rev.Length > 12 Then rev = rev.Substring(0, 12)
        Return rev & suffix
    End Function

    Private Shared Function IsoMs(ms As Long?) As String
        If Not ms.HasValue Then Return "none"
        Return DateTimeOffset.FromUnixTimeMilliseconds(ms.Value).UtcDateTime.ToString(
            "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>The one machine-readable line tools/ops/venue-check.ps1 records. Key=value,
    ''' space-separated, `reason` last and quoted. Diff counts read "na" when the check did not
    ''' run — never 0, which would read as a measured zero.</summary>
    Public Shared Function BuildVenueCheckLine(c As VenueCheckResult, seqContiguous As String,
                                               toolCommit As String, dumpState As String) As String
        Dim d = c.Diff
        Dim na As Func(Of Integer, String) = Function(n) If(d Is Nothing, "na", n.ToString(CultureInfo.InvariantCulture))
        Dim reason As String = If(c.Reason, "").Replace("""", "'").Replace(vbCr, " ").Replace(vbLf, " ")
        Return String.Format(CultureInfo.InvariantCulture,
            "VENUE_CHECK verdict={0} from={1} to={2} venue={3} identity={4} fallback={5} missing={6} legacy={7} " &
            "store={8} store_outside_venue_span={9} pages={10} first={11} last={12} seq_contiguous={13} " &
            "tool_commit={14} dump={15} reason=""{16}""",
            c.Verdict, IsoMs(c.WindowStartMs), IsoMs(c.WindowEndMs), c.VenueTrades,
            na(If(d Is Nothing, 0, d.IdentityMatched)), na(If(d Is Nothing, 0, d.FallbackMatched)),
            na(If(d Is Nothing, 0, d.MissingTrades.Count)), na(If(d Is Nothing, 0, d.StoreLegacyOnly)),
            c.StoreTrades, c.StoreOutsideVenueSpan, c.Pages, IsoMs(c.VenueFirstTs), IsoMs(c.VenueLastTs),
            If(seqContiguous, "unknown"), If(String.IsNullOrEmpty(toolCommit), "unknown", toolCommit),
            If(String.IsNullOrEmpty(dumpState), "off", dumpState), reason)
    End Function

    ''' <summary>--venue-dump: every response body fetched for the window, gzipped JSON. Bodies
    ''' are embedded as JSON STRINGS (`response_text`), so a non-JSON error body is kept verbatim
    ''' and the file always parses.</summary>
    Public Shared Sub WriteVenueDump(dumpPath As String, fetch As VenueFetchResult,
                                     startMs As Long, endMs As Long, toolCommit As String)
        Dim dir As String = Path.GetDirectoryName(Path.GetFullPath(dumpPath))
        If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
        Dim js As Func(Of String, String) = Function(s) System.Text.Json.JsonSerializer.Serialize(If(s, ""))
        Using fs As New FileStream(dumpPath, FileMode.Create, FileAccess.Write),
              gz As New System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionLevel.Optimal),
              sw As New StreamWriter(gz, New Text.UTF8Encoding(False))
            sw.Write(String.Format(CultureInfo.InvariantCulture,
                "{{""window_start_ms"":{0},""window_end_ms"":{1},""tool_commit"":{2},""fetch_ok"":{3},""fail_reason"":{4},""pages"":[",
                startMs, endMs, js(toolCommit), If(fetch IsNot Nothing AndAlso fetch.Ok, "true", "false"),
                js(If(fetch Is Nothing, "no fetch result", fetch.FailReason))))
            If fetch IsNot Nothing Then
                For i As Integer = 0 To fetch.RawPages.Count - 1
                    Dim p = fetch.RawPages(i)
                    sw.Write(String.Format(CultureInfo.InvariantCulture,
                        "{0}{{""start_ms"":{1},""end_ms"":{2},""response_text"":{3}}}",
                        If(i > 0, ",", ""), p.StartMs, p.EndMs, js(p.Json)))
                Next
            End If
            sw.Write("]}")
        End Using
    End Sub

    ' ── Top-level orchestration ───────────────────────────────────────────────────────

    Private Shared Function SafeReadAllLines(path As String) As String()
        Try
            If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return Array.Empty(Of String)()
            Return File.ReadAllLines(path)
        Catch
            Return Array.Empty(Of String)()
        End Try
    End Function

    ''' <summary>[venue-log wiring, 2026-09-14] The evidence paths the `coverage` CLI reads,
    ''' resolved in ONE place. Before this, BacktestProgram resolved five paths inline and never
    ''' resolved venue_status.log at all, so BuildResult's optional venueLogPath stayed "" and
    ''' OutOfScopeVenue could never fire from the CLI (docs/venue-check-plan-review-2026-09-14.md
    ''' §2). Extracted so the harness can exercise the CLI's own resolution and forwarding
    ''' instead of calling BuildResult with a hand-built path — which is the exact shape that let
    ''' the omission through (the C-3b fixtures pass the path directly).</summary>
    Public Class CoveragePaths
        Public Property StoreDir As String = ""
        Public Property AnalysisLog As String = ""
        Public Property WsHealth As String = ""
        Public Property Marker As String = ""
        Public Property Schedule As String = ""
        Public Property VenueLog As String = ""
    End Class

    ''' <summary>Pure path arithmetic — no existence checks (the CLI reports a missing
    ''' --evidence-dir / --store-dir itself). Defaults sit beside the store under
    ''' <paramref name="repoRoot"/>; --evidence-dir moves all six to a copy-back's
    ''' aws_fetch/&lt;stamp&gt;/ layout; --store-dir then overrides the store alone.</summary>
    Public Shared Function ResolveCoveragePaths(repoRoot As String, evidenceDir As String,
                                                storeDirOverride As String) As CoveragePaths
        Dim baseDir As String = If(String.IsNullOrEmpty(evidenceDir), repoRoot, evidenceDir)
        Dim p As New CoveragePaths With {
            .StoreDir = If(String.IsNullOrEmpty(evidenceDir), HistoricalStore.StoreDir,
                           Path.Combine(evidenceDir, HistoricalStore.StoreDir)),
            .AnalysisLog = Path.Combine(baseDir, "analysis_log.csv"),
            .WsHealth = Path.Combine(baseDir, "ws_health.log"),
            .Marker = Path.Combine(baseDir, "capture_marker.log"),
            .Schedule = Path.Combine(baseDir, "declared_schedule.txt"),
            .VenueLog = Path.Combine(baseDir, "venue_status.log")   ' = VenueStatusLog.FileName (Private in Core; not widened, engine-binary file)
        }
        If Not String.IsNullOrEmpty(storeDirOverride) Then p.StoreDir = storeDirOverride
        Return p
    End Function

    ''' <summary>The CLI's entry point: forwards EVERY resolved path. Fixture A78a runs through
    ''' this overload, so dropping a path here fails the harness.</summary>
    Public Shared Function BuildResult(opts As CoverageOptions, paths As CoveragePaths) As CoverageResult
        Return BuildResult(opts, paths.StoreDir, paths.AnalysisLog, paths.WsHealth, paths.Marker,
                           paths.Schedule, paths.VenueLog)
    End Function

    ''' <summary>Read-only: builds the full seven-class hourly walk + S4 completeness. S0 is
    ''' NOT run here (it needs live HTTP) — the CLI wires RunVenueDiffAsync's result onto the
    ''' returned CoverageResult separately when --verify-venue is passed.</summary>
    Public Shared Function BuildResult(opts As CoverageOptions, storeDir As String,
                                       analysisLogPath As String, wsHealthPath As String,
                                       markerPath As String,
                                       Optional schedulePath As String = "",
                                       Optional venueLogPath As String = "") As CoverageResult
        Dim result As New CoverageResult With {.FromUtc = opts.FromUtc, .ToUtc = opts.ToUtc, .GapMs = opts.GapMs}
        Dim declaredWindows As List(Of DeclaredWindow) = ParseDeclaredSchedule(schedulePath)
        Dim toUtcMs As Long = New DateTimeOffset(
            DateTime.SpecifyKind(opts.ToUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        Dim venueWindows As List(Of VenueWindow) = ParseVenueWindows(SafeReadAllLines(venueLogPath), toUtcMs)

        Dim captureBegins = ResolveCaptureBeginsUtc(storeDir, opts.FromUtc, opts.ToUtc)
        result.CaptureBeginsUtc = captureBegins

        Dim walkFromUtc As DateTime = opts.FromUtc
        If captureBegins.HasValue Then
            If captureBegins.Value > walkFromUtc Then
                result.PreCaptureDaysExcluded = CInt(Math.Floor((captureBegins.Value - walkFromUtc).TotalDays))
                walkFromUtc = New DateTime(captureBegins.Value.Year, captureBegins.Value.Month, captureBegins.Value.Day,
                                           captureBegins.Value.Hour, 0, 0, DateTimeKind.Utc)
            End If
        Else
            ' No trades anywhere in range — nothing to walk hourly; S4 still runs below.
            walkFromUtc = opts.ToUtc
        End If

        Dim analysisLines = SafeReadAllLines(analysisLogPath)
        Dim wsLines = SafeReadAllLines(wsHealthPath)
        Dim wsHealthExists As Boolean = File.Exists(wsHealthPath)

        Dim evidence As New List(Of EvidencePoint)
        evidence.AddRange(ParseAnalysisLogEvidence(analysisLines))
        evidence.AddRange(ParseWsHealthEvidence(wsLines))

        If Not wsHealthExists AndAlso analysisLines.Length = 0 Then
            result.S1Skipped = True
            result.S1SkipReason = "ws_health.log absent and analysis_log.csv has no rows in range — " &
                                  "S1 skipped, judging by store presence alone; S2-S4 still run"
        End If

        Dim upIntervals = BuildUpIntervals(evidence)
        Dim boundaryUtc = ResolveBoundaryUtc(evidence, opts.ToUtc)
        Dim markers = CaptureMarkerLog.ParseFile(markerPath)

        Dim walkToUtc As DateTime = If(boundaryUtc < opts.ToUtc, boundaryUtc, opts.ToUtc)
        If walkToUtc > walkFromUtc AndAlso opts.ToUtc > walkToUtc Then
            result.PostBoundaryHoursExcluded = CInt(Math.Ceiling((opts.ToUtc - walkToUtc).TotalHours))
        End If

        Dim hourStatsResult = AccumulateHourStats(storeDir, walkFromUtc, walkToUtc)
        Dim hourStats = hourStatsResult.ByHour

        ' [F1, D-4(c)] The trailing-edge observation bound — MIN of the evidence/request
        ' boundary and the store's own last in-range trade, resolved ONCE here and threaded
        ' into every ClassifyHour call below. StoreEndMs is already filtered to
        ' [walkFromUtc, walkToUtc), so it is always ≤ walkToUtcMs when present; the Min() is
        ' kept explicit to match the ruled formula literally rather than rely on that fact.
        Dim walkToUtcMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(walkToUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        Dim observedBoundMs As Long = If(hourStatsResult.StoreEndMs.HasValue,
                                         Math.Min(walkToUtcMs, hourStatsResult.StoreEndMs.Value), walkToUtcMs)

        ' [SH-1 §4.2] Find hours a marker splits (strictly inside — a marker landing exactly on
        ' hourStartMs was already ResolveScope's business) and run route (b)'s targeted second
        ' pass ONLY when at least one exists. Split hours are deploy/toggle-rare, so this stays
        ' a no-op cost on every normal run.
        Dim spanBoundsByHour As New Dictionary(Of Long, List(Of Long))
        If markers IsNot Nothing Then
            For Each mk In markers
                Dim hMs As Long = (mk.UtcMs \ HourMs) * HourMs
                If mk.UtcMs > hMs Then
                    Dim bounds As List(Of Long) = Nothing
                    If Not spanBoundsByHour.TryGetValue(hMs, bounds) Then
                        bounds = New List(Of Long) From {hMs}
                        spanBoundsByHour(hMs) = bounds
                    End If
                    bounds.Add(mk.UtcMs)
                End If
            Next
            For Each kv In spanBoundsByHour
                kv.Value.Sort()
            Next
        End If
        Dim splitSpanStats As Dictionary(Of Long, HourStoreStats) = Nothing
        If spanBoundsByHour.Count > 0 Then
            splitSpanStats = AccumulateSplitSpanStats(storeDir, walkFromUtc, walkToUtc, spanBoundsByHour)
        End If

        ' [§3.3] Local completeness over the SAME window the hourly walk covers. Unlike S0 this
        ' needs no network and is not bounded by Deribit's ~24 h trade retention, so it stays
        ' readable on a month-old file. It SUPPLEMENTS S0 (D6) — a sequence proves continuity,
        ' only the venue diff proves the rows agree with the venue on content.
        result.SequenceGaps = AccumulateSequenceGaps(storeDir, walkFromUtc, walkToUtc)

        Dim cursor As DateTime = New DateTime(walkFromUtc.Year, walkFromUtc.Month, walkFromUtc.Day,
                                              walkFromUtc.Hour, 0, 0, DateTimeKind.Utc)
        While cursor < walkToUtc
            Dim hourStartMs As Long = New DateTimeOffset(cursor).ToUnixTimeMilliseconds()
            Dim stats As HourStoreStats = Nothing
            hourStats.TryGetValue(hourStartMs, stats)
            Dim hr = ClassifyHour(cursor, markers, upIntervals, result.S1Skipped, stats, opts.GapMs,
                                  splitSpanStats, observedBoundMs, declaredWindows, venueWindows)
            result.Hours.Add(hr)
            If stats IsNot Nothing Then
                If stats.LongestGapMs > result.ObservedLongestGapMs Then result.ObservedLongestGapMs = stats.LongestGapMs
                If stats.LongestGapMs > opts.GapMs Then result.GapBreachHours += 1
            End If
            ' [F1, D-6(c)] Reported beside — never folded into — ObservedLongestGapMs/
            ' GapBreachHours (§4a.3). [coverage-trailing-split-span-spec.md, RULED 2026-09-03]
            ' Unlike those two siblings — which measure a real whole-hour quantity even when
            ' imprecise — this one is read PER-SPAN: a whole-hour figure could report a gap
            ' measured to the HOUR end even when the deciding span (the one ClassifyHour
            ' actually classified TrailingEdge on) ended earlier, inventing a trailing value no
            ' span ever had. ClassifyHour already selects the deciding span and returns its own
            ' span-bounded figure on HourResult.TrailingMsForHour; this only maxes over it.
            If hr.Classification = HourClass.TrailingEdge Then
                result.TrailingEdgeHours += 1
                If hr.TrailingMsForHour.HasValue AndAlso hr.TrailingMsForHour.Value > result.ObservedLongestTrailingMs Then
                    result.ObservedLongestTrailingMs = hr.TrailingMsForHour.Value
                End If
            End If
            cursor = cursor.AddHours(1)
        End While

        Dim candleStats = ComputeCandleCompleteness(storeDir, opts.FromUtc, opts.ToUtc)
        For Each kv In candleStats
            result.CandleHave(kv.Key) = kv.Value.Have
            result.CandleExpected(kv.Key) = kv.Value.Expected
        Next
        Dim funding = ComputeFundingCompleteness(storeDir, opts.FromUtc, opts.ToUtc)
        result.FundingHave = funding.Have
        result.FundingExpected = funding.Expected

        Return result
    End Function

    ' ── Rendering ──────────────────────────────────────────────────────────────────────

    Public Shared Function BuildConsoleSummary(result As CoverageResult) As String
        Dim sb As New Text.StringBuilder()
        sb.AppendLine(String.Format("TRADE STORE COVERAGE  {0:yyyy-MM-dd} → {1:yyyy-MM-dd}",
                                    result.FromUtc, result.ToUtc))
        If result.CaptureBeginsUtc.HasValue Then
            sb.AppendLine(String.Format("  capture begins      {0:yyyy-MM-dd HH:mm} UTC  ({1} earlier day(s) outside capture)",
                                        result.CaptureBeginsUtc.Value, result.PreCaptureDaysExcluded))
        Else
            sb.AppendLine("  capture begins      NO DATA — store has no trades in range")
        End If
        If result.PostBoundaryHoursExcluded > 0 Then
            sb.AppendLine(String.Format("  boundary            {0} hour(s) past the last available evidence, not yet reported",
                                        result.PostBoundaryHoursExcluded))
        End If
        sb.AppendLine(String.Format("  captured hours      {0}", result.CountByClass(HourClass.Captured)))
        sb.AppendLine(String.Format("  DEFECT              {0}   ← capture defects", result.CountByClass(HourClass.Defect)))
        sb.AppendLine(String.Format("  trailing-edge        {0}   ← silence to the observed edge, not a gap between trades", result.CountByClass(HourClass.TrailingEdge)))
        sb.AppendLine(String.Format("  expected-missing     {0}", result.CountByClass(HourClass.ExpectedMissing)))
        sb.AppendLine(String.Format("  startup-window       {0}", result.CountByClass(HourClass.StartupWindow)))
        sb.AppendLine(String.Format("  not-capturing        {0}", result.CountByClass(HourClass.NotCapturing)))
        sb.AppendLine(String.Format("  unknown-scope        {0}", result.CountByClass(HourClass.UnknownScope)))
        sb.AppendLine(String.Format("  out-of-scope-weekend   {0}", result.CountByClass(HourClass.OutOfScopeWeekend)))
        sb.AppendLine(String.Format("  out-of-scope-declared  {0}", result.CountByClass(HourClass.OutOfScopeDeclared)))
        sb.AppendLine(String.Format("  out-of-scope-venue     {0}", result.CountByClass(HourClass.OutOfScopeVenue)))
        If result.S1Skipped Then
            sb.AppendLine("  S1 (uptime)         SKIPPED — " & result.S1SkipReason)
        End If
        sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
            "  longest gap         {0:F1}s  (threshold {1:F1}s — {2} breach(es))",
            result.ObservedLongestGapMs / 1000.0, result.GapMs / 1000.0, result.GapBreachHours))
        ' [F1, D-6(c)] Own pair, reported beside — never folded into — the gap counters above.
        sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
            "  longest trailing    {0:F1}s  ({1} trailing-edge hour(s))",
            result.ObservedLongestTrailingMs / 1000.0, result.TrailingEdgeHours))

        Dim resList = {1, 3, 5, 15}
        Dim candleOk As Boolean = True
        Dim candleDetail As New List(Of String)
        For Each res In resList
            Dim have As Integer = 0, expected As Integer = 0
            result.CandleHave.TryGetValue(res, have)
            result.CandleExpected.TryGetValue(res, expected)
            If have < expected Then candleOk = False
            candleDetail.Add(res & "m " & have & "/" & expected)
        Next
        sb.AppendLine("  candles 1m/3m/5m/15m " & If(candleOk, "complete at all four resolutions   OK",
                                                     "*** INCOMPLETE *** (" & String.Join(", ", candleDetail) & ")"))

        If result.FundingExpected > 0 AndAlso result.FundingHave < result.FundingExpected Then
            Dim missing As Integer = result.FundingExpected - result.FundingHave
            Dim pct As Double = 100.0 * missing / result.FundingExpected
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "  funding             {0} / {1} samples   *** {2} MISSING ({3:F1}%) ***",
                result.FundingHave, result.FundingExpected, missing, pct))
        Else
            sb.AppendLine(String.Format("  funding             {0} / {1} samples   OK",
                                        result.FundingHave, result.FundingExpected))
        End If

        If result.VenueRan AndAlso result.VenueDiff IsNot Nothing Then
            Dim vd = result.VenueDiff
            sb.AppendLine(String.Format("  venue diff (S0)     {0} missing trade(s) in [{1:yyyy-MM-dd HH:mm}, {2:yyyy-MM-dd HH:mm}] UTC",
                                        vd.MissingTrades.Count, result.VenueCoveredFromUtc, result.VenueCoveredToUtc))
            ' [D4] The two match populations, side by side and never summed. A high fallback
            ' count is not reassurance — it says the store rows in this window predate identity,
            ' so the match is a five-field coincidence rate, not a trade-for-trade agreement.
            sb.AppendLine(String.Format("                      matched: {0} by identity (exact) · {1} by legacy five-field fallback (ambiguous)",
                                        vd.IdentityMatched, vd.FallbackMatched))
            sb.AppendLine(String.Format("                      store rows in window: {0} identified · {1} pre-identity{2}",
                                        vd.StoreIdentified, vd.StoreLegacyOnly,
                                        If(vd.StoreLegacyOnly > 0, "   *** diff is NOT exact while this is non-zero ***", "")))
        ElseIf result.VenueRan Then
            sb.AppendLine("  venue diff (S0)     ran but returned no result")
        Else
            sb.AppendLine("  venue diff (S0)     not run — pass --verify-venue")
        End If
        If result.VenueCheck IsNot Nothing Then
            Dim vc = result.VenueCheck
            sb.AppendLine(String.Format("  venue check         {0}{1}", vc.Verdict,
                                        If(String.IsNullOrEmpty(vc.Reason), "", " — " & vc.Reason)))
            sb.AppendLine(String.Format("                      venue trades {0} across {1} page(s), span {2} → {3}; store rows {4}, {5} outside the venue span",
                                        vc.VenueTrades, vc.Pages, IsoMs(vc.VenueFirstTs), IsoMs(vc.VenueLastTs),
                                        vc.StoreTrades, vc.StoreOutsideVenueSpan))
        End If

        ' [§3.3] Local completeness from trade_seq. Costs no network and is not bound by
        ' Deribit's ~24 h retention, so unlike S0 it stays readable on a month-old file.
        If result.SequenceGaps IsNot Nothing Then
            Dim sg = result.SequenceGaps
            If Not sg.Checkable Then
                sb.AppendLine(String.Format("  seq gaps (local)    NOT CHECKABLE — {0} row(s) carry no trade_seq, {1} do",
                                            sg.RowsWithoutSeq, sg.RowsWithSeq))
            Else
                Dim partialNote As String = If(sg.RowsWithoutSeq > 0,
                    String.Format("   *** PARTIAL: {0} row(s) carry no trade_seq and were not checked ***", sg.RowsWithoutSeq), "")
                If sg.MissingCount = 0 Then
                    sb.AppendLine(String.Format("  seq gaps (local)    none across {0} sequenced row(s)   OK{1}",
                                                sg.RowsWithSeq, partialNote))
                Else
                    sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
                        "  seq gaps (local)    *** {0} TRADE(S) MISSING *** in {1} run(s), longest {2}, across {3} sequenced row(s){4}",
                        sg.MissingCount, sg.GapRuns, sg.LongestGap, sg.RowsWithSeq, partialNote))
                End If
                If sg.Discontinuities > 0 Then
                    sb.AppendLine(String.Format("                      {0} backwards step(s) — sequence reset or interleaved feeds; NOT counted as loss",
                                                sg.Discontinuities))
                End If
                If sg.DuplicateSeqs > 0 Then
                    sb.AppendLine(String.Format("                      {0} repeated sequence number(s) survived dedup", sg.DuplicateSeqs))
                End If
            End If
        End If

        ' [F1, D-5.3] TrailingEdgeCount joins the "clean" gate — a report carrying
        ' trailing-edge hours and zero Defect hours must not print `clean` (fixture F1-f).
        Dim defectCount As Integer = result.CountByClass(HourClass.Defect)
        Dim trailingEdgeCount As Integer = result.CountByClass(HourClass.TrailingEdge)
        Dim fundingBad As Boolean = result.FundingExpected > 0 AndAlso result.FundingHave < result.FundingExpected
        ' [V-4] A venue verdict other than CLEAN keeps the report off `clean` — before this the
        ' line read "0 defect hour(s)" beside 1,000 missing venue trades (measured 2026-09-14).
        Dim venueBad As Boolean = result.VenueCheck IsNot Nothing AndAlso result.VenueCheck.Verdict <> VerdictClean
        If defectCount = 0 AndAlso trailingEdgeCount = 0 AndAlso candleOk AndAlso Not fundingBad AndAlso Not venueBad Then
            sb.AppendLine("  VERDICT: clean — no capture defects, candles + funding complete")
        Else
            Dim trailingNote As String = If(trailingEdgeCount > 0,
                String.Format(" + {0} trailing-edge hour(s)", trailingEdgeCount), "")
            sb.AppendLine(String.Format("  VERDICT: {0} defect hour(s){1}{2}{3}", defectCount, trailingNote,
                                        If(Not candleOk OrElse fundingBad, " + store gaps above", ""),
                                        If(venueBad, " + venue " & result.VenueCheck.Verdict, "")))
        End If
        Return sb.ToString()
    End Function

    Public Shared Function BuildMarkdown(result As CoverageResult) As String
        Dim sb As New Text.StringBuilder()
        sb.AppendLine("# Trade Store Coverage Report")
        sb.AppendLine()
        sb.AppendLine("```")
        sb.Append(BuildConsoleSummary(result))
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("## Non-captured hours")
        sb.AppendLine()
        sb.AppendLine("| Hour (UTC) | Class | Instance | Reason |")
        sb.AppendLine("|---|---|---|---|")
        For Each h In result.Hours
            If h.Classification = HourClass.Captured OrElse
               h.Classification = HourClass.OutOfScopeWeekend OrElse
               h.Classification = HourClass.OutOfScopeDeclared OrElse
               h.Classification = HourClass.OutOfScopeVenue Then Continue For
            sb.AppendLine(String.Format("| {0:yyyy-MM-dd HH:00} | {1} | {2} | {3} |",
                                        h.HourUtc, h.Classification.ToString(), h.InstanceId, h.Reason))
        Next
        Return sb.ToString()
    End Function

End Class
