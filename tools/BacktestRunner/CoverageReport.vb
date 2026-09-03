' tools/BacktestRunner/CoverageReport.vb
' The `coverage` verb (docs/trade-store-coverage-report-proposal.md, BUILD-AUTHORIZED
' 2026-08-03 — see docs/trade-store-coverage-report-implementer-brief.md).
'
' Reports capture health for the raw-trade store: seven classes per weekday UTC hour
' (captured / defect / trailing-edge / expected-missing / not-capturing / unknown-scope /
' out-of-scope-weekend — docs/j-b-scoping-ruling-2026-08-02.md +
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
' Host-agnostic (tools-only). No live HTTP except the optional --verify-venue path, which
' reuses HistoricalStore.FetchTradesByTimeAsync (§10 — no second HTTP path) and is injected
' behind a pure diff function so fixtures never need a live call.
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
    NotCapturing
    UnknownScope
    OutOfScopeWeekend
End Enum

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

Public Structure EvidencePoint
    Public UtcMs As Long
    Public InstanceId As String
    Public Sub New(utcMs As Long, instanceId As String)
        Me.UtcMs = utcMs
        Me.InstanceId = If(instanceId, "")
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
End Class

Public Class HourStoreStats
    Public Property RowCount As Integer
    Public Property LongestGapMs As Long
    ''' <summary>[F1, D-2/D-3(c)] The last trade timestamp seen in this bucket. Nullable BY
    ''' RULING — a default of 0 reads as a 1970-01-01 trailing gap on every hand-built fixture
    ''' that never sets it (coverage-trailing-edge-f1-proposal.md §4a.4). Nothing ⇒ the
    ''' trailing-edge check is skipped, never evaluated against a phantom epoch.</summary>
    Public Property LastTsMs As Long?
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

    ' ── Evidence parsing ──────────────────────────────────────────────────────────────

    ''' <summary>Parse ws_health.log lines ("utc | state | instance_id") into evidence
    ''' points. The STATE value is deliberately IGNORED — even a DOWN line proves the app
    ''' was alive to write it (WsHealthLog.LogStart fires before the socket connects), so
    ''' every line is equally valid "app was up at this instant" evidence regardless of
    ''' state. Malformed lines are skipped, never throws.</summary>
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
            result.Add(New EvidencePoint(
                New DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                parts(2).Trim()))
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
            result.Add(New EvidencePoint(
                New DateTimeOffset(DateTime.SpecifyKind(ts, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
                parts(iidIdx)))
        Next
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
                iv = New UpInterval With {.InstanceId = key, .FirstUtcMs = e.UtcMs, .LastUtcMs = e.UtcMs}
                byInstance(key) = iv
                result.Add(iv)
            Else
                If e.UtcMs < iv.FirstUtcMs Then iv.FirstUtcMs = e.UtcMs
                If e.UtcMs > iv.LastUtcMs Then iv.LastUtcMs = e.UtcMs
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
        Dim storeClean As Boolean = stats.RowCount > 0 AndAlso stats.LongestGapMs <= gapMs
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

        Dim reason As String = If(up.Kind <> "up", "ambiguous-uptime(" & up.Kind & ")",
                                  If(stats.RowCount = 0, "empty", "gap-breach(" & stats.LongestGapMs & "ms)"))
        Return (HourClass.Defect, up.InstanceId, reason, Nothing)
    End Function

    ''' <summary>[SH-1] The seven-class per-hour verdict. Positive store evidence (clean rows)
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
                                        Optional observedBoundMs As Long = Long.MaxValue) As HourResult
        Dim result As New HourResult With {.HourUtc = hourStartUtc}

        If hourStartUtc.DayOfWeek = DayOfWeek.Saturday OrElse hourStartUtc.DayOfWeek = DayOfWeek.Sunday Then
            result.Classification = HourClass.OutOfScopeWeekend
            Return result
        End If

        Dim hourStartMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(hourStartUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
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
        Else
            ' [coverage-trailing-split-span-spec.md R8, RULED 2026-09-03] Reached only because
            ' ClassifySpan never emits OutOfScopeWeekend — the seventh HourClass, handled by no
            ' branch here. If that ever changes, this Else and the combine's exhaustiveness must
            ' be revisited together.
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

    ''' <summary>CLI-side wiring for S0 — reuses HistoricalStore.FetchTradesByTimeAsync (§10:
    ''' no second HTTP path), never writes to the store. Nothing ⇒ the fetch failed; the
    ''' caller reports "not run", distinct from an empty (zero-missing) result.</summary>
    Public Shared Async Function RunVenueDiffAsync(storeDir As String, windowStartMs As Long, windowEndMs As Long) _
            As Task(Of VenueDiffResult)
        Dim venueTrades = Await HistoricalStore.FetchTradesByTimeAsync(windowStartMs, windowEndMs, 1000)
        If venueTrades Is Nothing Then Return Nothing

        Dim storeTrades As New List(Of TradeRecord)
        Dim startUtc = DateTimeOffset.FromUnixTimeMilliseconds(windowStartMs).UtcDateTime
        Dim endUtc = DateTimeOffset.FromUnixTimeMilliseconds(windowEndMs).UtcDateTime
        For Each m In HistoricalStore.EnumerateMonths(startUtc, endUtc.AddMilliseconds(1))
            Dim rows = TradeStoreWriter.ReadTradeFile(TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month))
            For Each r In rows
                If r.Timestamp >= windowStartMs AndAlso r.Timestamp <= windowEndMs Then storeTrades.Add(r)
            Next
        Next
        Return ComputeVenueDiff(storeTrades, venueTrades)
    End Function

    ' ── Top-level orchestration ───────────────────────────────────────────────────────

    Private Shared Function SafeReadAllLines(path As String) As String()
        Try
            If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return Array.Empty(Of String)()
            Return File.ReadAllLines(path)
        Catch
            Return Array.Empty(Of String)()
        End Try
    End Function

    ''' <summary>Read-only: builds the full seven-class hourly walk + S4 completeness. S0 is
    ''' NOT run here (it needs live HTTP) — the CLI wires RunVenueDiffAsync's result onto the
    ''' returned CoverageResult separately when --verify-venue is passed.</summary>
    Public Shared Function BuildResult(opts As CoverageOptions, storeDir As String,
                                       analysisLogPath As String, wsHealthPath As String,
                                       markerPath As String) As CoverageResult
        Dim result As New CoverageResult With {.FromUtc = opts.FromUtc, .ToUtc = opts.ToUtc, .GapMs = opts.GapMs}

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
                                  splitSpanStats, observedBoundMs)
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
        sb.AppendLine(String.Format("  not-capturing        {0}", result.CountByClass(HourClass.NotCapturing)))
        sb.AppendLine(String.Format("  unknown-scope        {0}", result.CountByClass(HourClass.UnknownScope)))
        sb.AppendLine(String.Format("  out-of-scope-weekend {0}", result.CountByClass(HourClass.OutOfScopeWeekend)))
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
        If defectCount = 0 AndAlso trailingEdgeCount = 0 AndAlso candleOk AndAlso Not fundingBad Then
            sb.AppendLine("  VERDICT: clean — no capture defects, candles + funding complete")
        Else
            Dim trailingNote As String = If(trailingEdgeCount > 0,
                String.Format(" + {0} trailing-edge hour(s)", trailingEdgeCount), "")
            sb.AppendLine(String.Format("  VERDICT: {0} defect hour(s){1}{2}", defectCount, trailingNote,
                                        If(Not candleOk OrElse fundingBad, " + store gaps above", "")))
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
            If h.Classification = HourClass.Captured OrElse h.Classification = HourClass.OutOfScopeWeekend Then Continue For
            sb.AppendLine(String.Format("| {0:yyyy-MM-dd HH:00} | {1} | {2} | {3} |",
                                        h.HourUtc, h.Classification.ToString(), h.InstanceId, h.Reason))
        Next
        Return sb.ToString()
    End Function

End Class
