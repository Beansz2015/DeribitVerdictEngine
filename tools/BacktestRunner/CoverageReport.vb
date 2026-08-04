' tools/BacktestRunner/CoverageReport.vb
' The `coverage` verb (docs/trade-store-coverage-report-proposal.md, BUILD-AUTHORIZED
' 2026-08-03 — see docs/trade-store-coverage-report-implementer-brief.md).
'
' Reports capture health for the raw-trade store: six classes per weekday UTC hour
' (captured / defect / expected-missing / not-capturing / unknown-scope /
' out-of-scope-weekend — docs/j-b-scoping-ruling-2026-08-02.md +
' docs/weekday-scope-ruling-2026-08-03.md), plus S4 candle/funding completeness and an
' optional S0 venue diff. Tools-only, read-only, no settings keys, no version bump.
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

    Public Property CandleHave As New Dictionary(Of Integer, Integer)
    Public Property CandleExpected As New Dictionary(Of Integer, Integer)
    Public Property FundingHave As Integer
    Public Property FundingExpected As Integer

    Public Property VenueRan As Boolean = False
    Public Property VenueMissingTrades As New List(Of TradeRecord)
    Public Property VenueCoveredFromUtc As DateTime?
    Public Property VenueCoveredToUtc As DateTime?

    Public Function CountByClass(cls As HourClass) As Integer
        ' .Where(...).Count() rather than .Count(predicate) — List(Of T)'s own zero-arg
        ' Count PROPERTY shadows the Enumerable.Count(predicate) extension method, which VB
        ' resolves as an indexer on the property's Integer return type instead (BC32016).
        Return Hours.Where(Function(h) h.Classification = cls).Count()
    End Function
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
        Dim hourEndMs As Long = hourStartMs + HourMs - 1
        If upIntervals IsNot Nothing Then
            For Each iv In upIntervals
                If hourEndMs >= iv.FirstUtcMs AndAlso hourStartMs <= iv.LastUtcMs Then
                    Return ("up", iv.InstanceId)
                End If
            Next
        End If

        Dim prevIv As UpInterval = Nothing
        Dim nextIv As UpInterval = Nothing
        If upIntervals IsNot Nothing Then
            For Each iv In upIntervals
                If iv.LastUtcMs < hourStartMs Then
                    If prevIv Is Nothing OrElse iv.LastUtcMs > prevIv.LastUtcMs Then prevIv = iv
                ElseIf iv.FirstUtcMs > hourEndMs Then
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
    ''' the seam is still measured correctly.</summary>
    Public Shared Function AccumulateHourStats(storeDir As String, fromUtc As DateTime, toUtc As DateTime) _
            As Dictionary(Of Long, HourStoreStats)
        Dim byHour As New Dictionary(Of Long, HourStoreStats)
        Dim prevTs As Long = Long.MinValue
        Dim havePrev As Boolean = False

        For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
            Dim path As String = TradeStoreWriter.TradeFileFor(storeDir, m.Year, m.Month)
            Dim rows = TradeStoreWriter.ReadTradeFile(path)
            If rows.Count = 0 Then Continue For
            Dim seen As New HashSet(Of String)
            Dim deduped As New List(Of TradeRecord)
            For Each r In rows
                If seen.Add(TradeStoreWriter.FormatRow(r)) Then deduped.Add(r)
            Next
            deduped.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))

            For Each t In deduped
                Dim hourStartMs As Long = (t.Timestamp \ HourMs) * HourMs
                Dim stats As HourStoreStats = Nothing
                If Not byHour.TryGetValue(hourStartMs, stats) Then
                    stats = New HourStoreStats()
                    byHour(hourStartMs) = stats
                End If
                stats.RowCount += 1
                If havePrev Then
                    Dim gap As Long = t.Timestamp - prevTs
                    If gap > stats.LongestGapMs Then stats.LongestGapMs = gap
                End If
                prevTs = t.Timestamp
                havePrev = True
            Next
        Next
        Return byHour
    End Function

    ' ── Per-hour classification ───────────────────────────────────────────────────────

    ''' <summary>The six-class per-hour verdict. Positive store evidence (clean rows) always
    ''' wins as Captured, regardless of how ambiguous the uptime read is. See the file-header
    ''' note for the ExpectedMissing / S1-skipped design decisions.</summary>
    Public Shared Function ClassifyHour(hourStartUtc As DateTime,
                                        markers As List(Of CaptureMarkerLog.MarkerRecord),
                                        upIntervals As List(Of UpInterval),
                                        s1Skipped As Boolean,
                                        hourStats As HourStoreStats,
                                        gapMs As Long) As HourResult
        Dim result As New HourResult With {.HourUtc = hourStartUtc}

        If hourStartUtc.DayOfWeek = DayOfWeek.Saturday OrElse hourStartUtc.DayOfWeek = DayOfWeek.Sunday Then
            result.Classification = HourClass.OutOfScopeWeekend
            Return result
        End If

        Dim hourStartMs As Long =
            New DateTimeOffset(DateTime.SpecifyKind(hourStartUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()

        Dim scope = ResolveScope(hourStartMs, markers)
        If scope.Kind = "unknown" Then
            result.Classification = HourClass.UnknownScope
            Return result
        End If
        If scope.Kind = "off" Then
            result.Classification = HourClass.NotCapturing
            result.InstanceId = scope.InstanceId
            Return result
        End If

        Dim stats As HourStoreStats = If(hourStats, New HourStoreStats())
        Dim storeClean As Boolean = stats.RowCount > 0 AndAlso stats.LongestGapMs <= gapMs
        If storeClean Then
            result.Classification = HourClass.Captured
            Return result
        End If

        If s1Skipped Then
            result.Classification = HourClass.Defect
            result.Reason = If(stats.RowCount = 0, "empty(S1 skipped)",
                               "gap-breach(S1 skipped," & stats.LongestGapMs & "ms)")
            Return result
        End If

        Dim up = ClassifyUptime(hourStartMs, upIntervals)
        result.InstanceId = up.InstanceId
        If up.Kind = "before-first" Then
            result.Classification = HourClass.ExpectedMissing
            Return result
        End If

        result.Classification = HourClass.Defect
        result.Reason = If(up.Kind <> "up", "ambiguous-uptime(" & up.Kind & ")",
                           If(stats.RowCount = 0, "empty", "gap-breach(" & stats.LongestGapMs & "ms)"))
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

    ''' <summary>Trades present at the venue but absent from the store — the diff IS the
    ''' loss, enumerated exactly, never estimated. Identity = the full formatted row (the
    ''' same equality TradeStoreWriter's own read-time dedup uses). Pure — no HTTP — so
    ''' fixtures exercise it with a stubbed venue list (A49j).</summary>
    Public Shared Function ComputeVenueDiff(storeTrades As List(Of TradeRecord),
                                            venueTrades As List(Of TradeRecord)) As List(Of TradeRecord)
        Dim storeKeys As New HashSet(Of String)
        If storeTrades IsNot Nothing Then
            For Each t In storeTrades
                storeKeys.Add(TradeStoreWriter.FormatRow(t))
            Next
        End If
        Dim missing As New List(Of TradeRecord)
        If venueTrades IsNot Nothing Then
            For Each t In venueTrades
                If Not storeKeys.Contains(TradeStoreWriter.FormatRow(t)) Then missing.Add(t)
            Next
        End If
        Return missing
    End Function

    ''' <summary>CLI-side wiring for S0 — reuses HistoricalStore.FetchTradesByTimeAsync (§10:
    ''' no second HTTP path), never writes to the store. Nothing ⇒ the fetch failed; the
    ''' caller reports "not run", distinct from an empty (zero-missing) result.</summary>
    Public Shared Async Function RunVenueDiffAsync(storeDir As String, windowStartMs As Long, windowEndMs As Long) _
            As Task(Of List(Of TradeRecord))
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

    ''' <summary>Read-only: builds the full six-class hourly walk + S4 completeness. S0 is
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

        Dim hourStats = AccumulateHourStats(storeDir, walkFromUtc, walkToUtc)

        Dim cursor As DateTime = New DateTime(walkFromUtc.Year, walkFromUtc.Month, walkFromUtc.Day,
                                              walkFromUtc.Hour, 0, 0, DateTimeKind.Utc)
        While cursor < walkToUtc
            Dim hourStartMs As Long = New DateTimeOffset(cursor).ToUnixTimeMilliseconds()
            Dim stats As HourStoreStats = Nothing
            hourStats.TryGetValue(hourStartMs, stats)
            Dim hr = ClassifyHour(cursor, markers, upIntervals, result.S1Skipped, stats, opts.GapMs)
            result.Hours.Add(hr)
            If stats IsNot Nothing Then
                If stats.LongestGapMs > result.ObservedLongestGapMs Then result.ObservedLongestGapMs = stats.LongestGapMs
                If stats.LongestGapMs > opts.GapMs Then result.GapBreachHours += 1
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

        If result.VenueRan Then
            sb.AppendLine(String.Format("  venue diff (S0)     {0} missing trade(s) in [{1:yyyy-MM-dd HH:mm}, {2:yyyy-MM-dd HH:mm}] UTC",
                                        result.VenueMissingTrades.Count, result.VenueCoveredFromUtc, result.VenueCoveredToUtc))
        Else
            sb.AppendLine("  venue diff (S0)     not run — pass --verify-venue")
        End If

        Dim defectCount As Integer = result.CountByClass(HourClass.Defect)
        Dim fundingBad As Boolean = result.FundingExpected > 0 AndAlso result.FundingHave < result.FundingExpected
        If defectCount = 0 AndAlso candleOk AndAlso Not fundingBad Then
            sb.AppendLine("  VERDICT: clean — no capture defects, candles + funding complete")
        Else
            sb.AppendLine(String.Format("  VERDICT: {0} defect hour(s){1}", defectCount,
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
