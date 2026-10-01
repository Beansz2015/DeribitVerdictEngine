' tools/BacktestRunner/HistoryStore.vb
' Stage 2 of docs/history-data-store-spec.md: the dev-machine history store. Trades for
' BTC-PERPETUAL from https://history.deribit.com, paged by trade_seq, written in the box
' store's monthly layout with the HDS-1 (b) columns appended.
'
' Host-agnostic (no WinForms) and NETWORK-FREE: the one HTTP seam is an injected
' Func(Of String, Task(Of String)) (HistoryHostHttp.GetJsonAsync in production, a fake host in
' the A94 fixtures). Reuses TradeStoreWriter's file naming, FormatRow and TryParseRow, and
' HistoricalStore.ParseTradesPage for the trade fields, so this file adds no second parse of
' the seven shared columns.
'
' The four rules this file exists to keep (spec §0, §3):
'   1. Page by trade_seq, NEVER by timestamp. The host widens a seq range to whole
'      milliseconds, so pages overlap: de-duplicate on trade_seq. A page with has_more is
'      split and re-requested, so no millisecond group is truncated (§3.2).
'   2. Never write a trade younger than SettleMarginMs at fetch time: the liquidation flag
'      arrives late (§3.4).
'   3. A day is the unit of work. Its month file is rewritten atomically (tmp, flush, re-read,
'      rename) and only THEN is the day checkpointed (§3.3, audit row E2). A kill at any point
'      leaves either the old file or the new one, and a re-run replaces the day's rows rather
'      than appending them, so a resume can neither duplicate nor lose a trade.
'   4. A row that does not parse is never skipped silently (audit row E1): the merge refuses
'      to rewrite a month file that holds one, and the status check counts them.
'
' Fixtures: A94a-A94k in verify/ordercheck/Program.vb.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks

''' <summary>A transient host failure: transport error, timeout, non-2xx status, or a body
''' without a `result`. Retried with backoff (spec §3.5). Fakes throw it to simulate errors.</summary>
Public Class HistoryHostException
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>The retry budget for one request is spent. The run STOPS here (never skips the
''' range); a re-run resumes from the checkpoint.</summary>
Public Class HistoryTransportException
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>The host answered, but the answer cannot be stored as a complete day (an
''' unsplittable page, no anchor trade, two different copies of one seq). The day is marked
''' FAILED and not written; the run continues with the next day.</summary>
Public Class HistoryDataException
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>The store on disk is not safe to rewrite (foreign header, unparseable row, rows
''' out of day order, a re-read that does not match). The run stops; nothing is renamed.</summary>
Public Class HistoryStoreException
    Inherits Exception
    Public Sub New(message As String)
        MyBase.New(message)
    End Sub
End Class

''' <summary>The four HDS-1 (b) fields. Nothing = absent on the host (2020-era trades carry
''' no `contracts`); written as an empty column.</summary>
Public Structure HistoryExtras
    Public MarkPrice As Double?
    Public IndexPrice As Double?
    Public TickDirection As Integer?
    Public Contracts As Double?
End Structure

''' <summary>One history-host trade: the shared seven-column record plus the HDS-1 extras.</summary>
Public NotInheritable Class HistoryTrade
    Public Property Rec As TradeRecord
    Public Property Extras As HistoryExtras
End Class

''' <summary>One checkpoint row: the outcome of the last fetch of one UTC day.</summary>
Public NotInheritable Class HistoryDayEntry
    Public Property Day As DateTime
    Public Property Status As String = ""
    Public Property Rows As Long
    Public Property FirstSeq As Long = TradeRecord.AbsentSeq
    Public Property LastSeq As Long = TradeRecord.AbsentSeq
    Public Property MissingSeqs As Long
    Public Property LiqFlagged As Long
    Public Property Requests As Long
    Public Property Retries As Long
    Public Property HasMoreSplits As Long
    Public Property TsInversions As Long
    Public Property PrecisionLoss As Long
    Public Property FetchedAtUtc As String = ""
    Public Property Detail As String = ""
End Class

''' <summary>A fetched day before it is written.</summary>
Public NotInheritable Class HistoryDayResult
    Public Property Entry As New HistoryDayEntry()
    Public Property Trades As New List(Of HistoryTrade)()
    Public Property Seconds As Double
End Class

''' <summary>What a backfill or top-up run did. ExitCode follows the CLI contract.</summary>
Public NotInheritable Class HistoryRunResult
    Public Property Planned As Integer
    Public Property AlreadyComplete As Integer
    Public Property Unsettled As Integer
    Public Property DaysOk As Integer
    Public Property DaysGap As Integer
    Public Property DaysFailed As Integer
    Public Property Trades As Long
    Public Property Requests As Long
    Public Property Retries As Long
    Public Property Seconds As Double
    Public Property StoppedReason As String = ""
    Public Property ExitCode As Integer
    Public Property RemainingNotDone As Integer
End Class

Public NotInheritable Class HistoryStore

    ' ── Ruled constants (CLAUDE.md: a ruled constant is Public so fixtures read it) ──────

    ''' <summary>[spec §3.4] Only trades older than this at fetch time are stored. The
    ''' liquidation flag was seen arriving ~60 min late (n = 1); 24 h is a wide margin and costs
    ''' nothing, because the store serves research, not live scoring. A whole day is fetched
    ''' only when its END is at least this old.</summary>
    Public Const SettleMarginMs As Long = 24L * 60L * 60L * 1000L

    ''' <summary>[spec §3.5] Pause after every request. The measured 2026-09-28 pace: ~1.1
    ''' req/s, 0 errors in 451 requests. Sequential only; no parallelism (rate limits not read).</summary>
    Public Const PacingDelayMs As Integer = 150

    ''' <summary>[spec §3.5] Retries after the first attempt, then the run stops.</summary>
    Public Const MaxRetries As Integer = 5

    ''' <summary>[spec §3.5] First backoff; each further retry doubles it (2, 4, 8, 16, 32 s).</summary>
    Public Const FirstBackoffMs As Integer = 2000

    ''' <summary>Seq span asked for per page. The host caps a page at
    ''' <see cref="HistoricalStore.TradesPerPage"/> (1000) and widens the range to whole
    ''' milliseconds, so 800 leaves room for the boundary groups (the validated
    ''' tools/ops/history_host_validate.py value: 0 splits in 451 requests).</summary>
    Public Const PageSeqSpan As Long = 800L

    ''' <summary>The low anchor is the first trade at or after (day start − this). Starting a
    ''' minute early makes the day's first trade independent of whether the time endpoint's
    ''' start bound is inclusive or exclusive (the funding endpoint's is exclusive).</summary>
    Public Const AnchorOvershootMs As Long = 60000L

    ''' <summary>How far past the day end the high anchor looks for the next trade.</summary>
    Public Const AnchorSearchMs As Long = 24L * 60L * 60L * 1000L

    Public Const DayMs As Long = 24L * 60L * 60L * 1000L

    Public Const InstrumentName As String = "BTC-PERPETUAL"

    ''' <summary>The HDS-1 (b) columns 8-11, appended after the box store's seven.</summary>
    Public Const ExtraHeader As String = "MarkPrice,IndexPrice,TickDirection,Contracts"

    ''' <summary>The dev store's header: the box header plus the four HDS-1 columns. Every
    ''' existing reader skips line 1 and parses with TryParseRow, which tolerates longer rows.</summary>
    Public Const HeaderLine As String = TradeStoreWriter.HeaderLine & "," & ExtraHeader

    Public Const ColumnCount As Integer = 11

    Public Const CheckpointFileName As String = "history_checkpoint.csv"
    Public Const CheckpointHeader As String =
        "Day,Status,Rows,FirstSeq,LastSeq,MissingSeqs,LiqFlagged,Requests,Retries,HasMoreSplits,TsInversions,PrecisionLoss,FetchedAtUtc,Detail"
    Public Const LogFileName As String = "history_backfill.log"
    Public Const LockFileName As String = "history.lock"
    Public Const TmpSuffix As String = ".tmp"

    Public Const StatusOk As String = "OK"
    Public Const StatusGap As String = "GAP"
    Public Const StatusFailed As String = "FAILED"

    ' Exit codes (the CLI contract; the runbook's loop keys on them).
    Public Const ExitOk As Integer = 0
    Public Const ExitBadArgsOrStore As Integer = 1
    Public Const ExitTransportStop As Integer = 2
    Public Const ExitCompareFail As Integer = 3
    Public Const ExitDaysIncomplete As Integer = 4

    ' ── Instance state ──────────────────────────────────────────────────────────────────

    Private ReadOnly _storeDir As String
    Private ReadOnly _getJson As Func(Of String, Task(Of String))
    Private ReadOnly _delay As Func(Of Integer, Task)
    Private ReadOnly _clockMs As Func(Of Long)
    Private ReadOnly _stageHook As Action(Of String)
    Private ReadOnly _log As Action(Of String)

    ' Per-day counters, reset by FetchDayAsync.
    Private _requests, _retries, _splits, _gapRepages, _conflicts As Long
    Private _conflictDetail As String = ""

    ''' <param name="getJson">Path-and-query after the API base (e.g.
    ''' "get_last_trades_by_instrument?instrument_name=..."), returns the body. Throws
    ''' <see cref="HistoryHostException"/> on a transient failure.</param>
    ''' <param name="delay">Sleep for n ms. Injected so fixtures record the pacing and backoff
    ''' instead of waiting.</param>
    ''' <param name="clockUtcMs">Now, in Unix ms. Injected so the settle margin is testable.</param>
    ''' <param name="stageHook">Called with "after-tmp", "after-rename", "after-checkpoint".
    ''' Fixtures throw from it to simulate a kill between the write steps.</param>
    Public Sub New(storeDir As String,
                   getJson As Func(Of String, Task(Of String)),
                   Optional delay As Func(Of Integer, Task) = Nothing,
                   Optional clockUtcMs As Func(Of Long) = Nothing,
                   Optional stageHook As Action(Of String) = Nothing,
                   Optional log As Action(Of String) = Nothing)
        _storeDir = storeDir
        _getJson = getJson
        _delay = If(delay, Function(ms As Integer) Task.Delay(ms))
        _clockMs = If(clockUtcMs, Function() DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        _stageHook = If(stageHook, Sub(s As String)
                                   End Sub)
        _log = If(log, Sub(s As String) Console.WriteLine(s))
    End Sub

    Public ReadOnly Property StoreDir As String
        Get
            Return _storeDir
        End Get
    End Property

    ' ── Day arithmetic ──────────────────────────────────────────────────────────────────

    Public Shared Function DayStartMs(day As DateTime) As Long
        Return New DateTimeOffset(New DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
    End Function

    Public Shared Function DayOfMs(ms As Long) As DateTime
        Return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.Date
    End Function

    Private Shared Function DayFloorMs(ms As Long) As Long
        Dim d As Long = ms \ DayMs
        If ms < 0 AndAlso ms Mod DayMs <> 0 Then d -= 1
        Return d * DayMs
    End Function

    Public Shared Function DayKey(day As DateTime) As String
        Return day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>The default store folder: C:\DeribitData\history on Windows (spec §3.1),
    ''' ~/DeribitData/history elsewhere (the cloud backfill host).</summary>
    Public Shared Function DefaultStoreDir() As String
        If OperatingSystem.IsWindows() Then Return "C:\DeribitData\history"
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DeribitData", "history")
    End Function

    ' ── Row format (HDS-1 (b)) ──────────────────────────────────────────────────────────

    ''' <summary>One dev-store row: TradeStoreWriter.FormatRow's seven columns, unchanged, then
    ''' mark_price, index_price, tick_direction, contracts. An absent field is an empty column.</summary>
    Public Shared Function FormatHistoryRow(t As HistoryTrade) As String
        Dim x As HistoryExtras = t.Extras
        Return TradeStoreWriter.FormatRow(t.Rec) & "," &
               FormatOptional(x.MarkPrice) & "," &
               FormatOptional(x.IndexPrice) & "," &
               If(x.TickDirection.HasValue, x.TickDirection.Value.ToString(CultureInfo.InvariantCulture), "") & "," &
               FormatOptional(x.Contracts)
    End Function

    Private Shared Function FormatOptional(v As Double?) As String
        If Not v.HasValue Then Return ""
        Return v.Value.ToString("R", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>Parse one dev-store row. False on anything that is not exactly eleven columns
    ''' with the shared seven parsing through TradeStoreWriter.TryParseRow, a trade_seq, and
    ''' well-formed extras. Stricter than the shared parse on purpose: the dev store is written
    ''' only by this file, so any other shape is corruption.</summary>
    Public Shared Function TryParseHistoryRow(line As String, ByRef rec As TradeRecord, ByRef extras As HistoryExtras) As Boolean
        If String.IsNullOrEmpty(line) Then Return False
        Dim parts As String() = line.Split(","c)
        If parts.Length <> ColumnCount Then Return False
        Dim r As New TradeRecord()
        If Not TradeStoreWriter.TryParseRow(line, r) Then Return False
        If Not r.HasSeq OrElse Not r.HasIdentity Then Return False
        Dim x As New HistoryExtras()
        If Not TryParseOptionalDouble(parts(7), x.MarkPrice) Then Return False
        If Not TryParseOptionalDouble(parts(8), x.IndexPrice) Then Return False
        If parts(9).Length > 0 Then
            Dim td As Integer
            If Not Integer.TryParse(parts(9), NumberStyles.Integer, CultureInfo.InvariantCulture, td) Then Return False
            x.TickDirection = td
        End If
        If Not TryParseOptionalDouble(parts(10), x.Contracts) Then Return False
        rec = r
        extras = x
        Return True
    End Function

    Private Shared Function TryParseOptionalDouble(s As String, ByRef v As Double?) As Boolean
        If s.Length = 0 Then
            v = Nothing
            Return True
        End If
        Dim d As Double
        If Not Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, d) Then Return False
        v = d
        Return True
    End Function

    ' ── Host requests ───────────────────────────────────────────────────────────────────

    Public Shared Function SeqQuery(startSeq As Long, endSeq As Long) As String
        Return "get_last_trades_by_instrument?instrument_name=" & InstrumentName &
               "&start_seq=" & startSeq.ToString(CultureInfo.InvariantCulture) &
               "&end_seq=" & endSeq.ToString(CultureInfo.InvariantCulture) &
               "&count=" & HistoricalStore.TradesPerPage.ToString(CultureInfo.InvariantCulture) &
               "&sorting=asc"
    End Function

    Public Shared Function TimeQuery(startMs As Long, endMs As Long) As String
        Return "get_last_trades_by_instrument_and_time?instrument_name=" & InstrumentName &
               "&start_timestamp=" & startMs.ToString(CultureInfo.InvariantCulture) &
               "&end_timestamp=" & endMs.ToString(CultureInfo.InvariantCulture) &
               "&count=1&sorting=asc"
    End Function

    ''' <summary>One page: the shared seven-field parse (HistoricalStore.ParseTradesPage) plus
    ''' the HDS-1 extras keyed by trade_seq.</summary>
    Friend NotInheritable Class HostPage
        Public Property Trades As New List(Of HistoryTrade)()
        Public Property HasMore As Boolean
    End Class

    ''' <summary>Parse one get_last_trades_* body. Nothing when the body is not a usable result
    ''' (a JSON-RPC error, a missing trades array, a trade without trade_seq or trade_id).</summary>
    Friend Shared Function ParseHostPage(json As String) As HostPage
        Dim shared7 As HistoricalStore.TradePage = HistoricalStore.ParseTradesPage(json)
        If shared7 Is Nothing Then Return Nothing
        Dim extras As New Dictionary(Of Long, HistoryExtras)()
        Try
            Using doc As JsonDocument = JsonDocument.Parse(json)
                For Each t As JsonElement In doc.RootElement.GetProperty("result").GetProperty("trades").EnumerateArray()
                    Dim seq As Long = TradeRecord.ReadTradeSeq(t)
                    If seq < 0 Then Return Nothing
                    Dim x As New HistoryExtras()
                    x.MarkPrice = ReadOptionalDouble(t, "mark_price")
                    x.IndexPrice = ReadOptionalDouble(t, "index_price")
                    Dim td As Double? = ReadOptionalDouble(t, "tick_direction")
                    If td.HasValue Then x.TickDirection = CInt(td.Value)
                    x.Contracts = ReadOptionalDouble(t, "contracts")
                    extras(seq) = x
                Next
            End Using
        Catch
            Return Nothing
        End Try
        Dim page As New HostPage()
        ' has_more absent: treat a full page as truncated (the conservative reading).
        page.HasMore = If(shared7.HasMore.HasValue, shared7.HasMore.Value,
                          shared7.Trades.Count >= HistoricalStore.TradesPerPage)
        For Each r As TradeRecord In shared7.Trades
            If Not r.HasSeq OrElse Not r.HasIdentity Then Return Nothing
            Dim x As HistoryExtras = Nothing
            If Not extras.TryGetValue(r.TradeSeq, x) Then Return Nothing
            page.Trades.Add(New HistoryTrade With {.Rec = r, .Extras = x})
        Next
        Return page
    End Function

    Private Shared Function ReadOptionalDouble(t As JsonElement, name As String) As Double?
        Dim el As JsonElement = Nothing
        If Not t.TryGetProperty(name, el) Then Return Nothing
        If el.ValueKind <> JsonValueKind.Number Then Return Nothing
        Return el.GetDouble()
    End Function

    ''' <summary>One request with the spec §3.5 discipline: pause PacingDelayMs after every
    ''' answer; on a <see cref="HistoryHostException"/> or an unusable body, back off
    ''' FirstBackoffMs × 2^k and retry, up to MaxRetries; then throw
    ''' <see cref="HistoryTransportException"/>. Any other exception propagates untouched.</summary>
    Private Async Function RequestPageAsync(query As String) As Task(Of HostPage)
        Dim lastError As String = ""
        For attempt As Integer = 0 To MaxRetries
            _requests += 1
            Dim page As HostPage = Nothing
            Try
                Dim body As String = Await _getJson(query)
                page = ParseHostPage(body)
                If page Is Nothing Then
                    lastError = "unusable body: " & Shorten(body)
                End If
            Catch ex As HistoryHostException
                lastError = ex.Message
            End Try
            If page IsNot Nothing Then
                Await _delay(PacingDelayMs)
                Return page
            End If
            If attempt = MaxRetries Then Exit For
            _retries += 1
            Dim backoff As Integer = FirstBackoffMs << attempt
            _log(String.Format(CultureInfo.InvariantCulture, "  request error ({0}); retry {1}/{2} in {3} ms",
                               lastError, attempt + 1, MaxRetries, backoff))
            Await _delay(backoff)
        Next
        Throw New HistoryTransportException("gave up after " & (MaxRetries + 1) & " attempts on " & query & " — last error: " & lastError)
    End Function

    Private Shared Function Shorten(s As String) As String
        If s Is Nothing Then Return "(null)"
        Dim one As String = s.Replace(vbCr, " ").Replace(vbLf, " ")
        Return If(one.Length > 200, one.Substring(0, 200) & "...", one)
    End Function

    ''' <summary>The seq of the first trade at or after startMs (and at or before endMs), or
    ''' AbsentSeq when the window holds none.</summary>
    Private Async Function AnchorSeqAsync(startMs As Long, endMs As Long) As Task(Of Long)
        Dim page As HostPage = Await RequestPageAsync(TimeQuery(startMs, endMs))
        If page.Trades.Count = 0 Then Return TradeRecord.AbsentSeq
        Return page.Trades(0).Rec.TradeSeq
    End Function

    ''' <summary>Fetch every trade in [lo, hi] into <paramref name="into"/>, keyed on trade_seq.
    ''' Pages of PageSeqSpan seqs; a page with has_more is split in halves and re-requested; a
    ''' single-seq page with has_more cannot be split and fails the day. A seq seen twice with
    ''' different content is counted as a conflict.</summary>
    Private Async Function PageRangeAsync(lo As Long, hi As Long, into As Dictionary(Of Long, HistoryTrade)) As Task
        Dim s As Long = lo
        While s <= hi
            Dim e As Long = Math.Min(s + PageSeqSpan - 1L, hi)
            Dim stack As New Stack(Of (A As Long, B As Long))()
            stack.Push((s, e))
            While stack.Count > 0
                Dim rng = stack.Pop()
                Dim page As HostPage = Await RequestPageAsync(SeqQuery(rng.A, rng.B))
                If page.HasMore Then
                    If rng.B <= rng.A Then
                        Throw New HistoryDataException(String.Format(CultureInfo.InvariantCulture,
                            "page at seq {0} reports has_more and cannot be split (one millisecond holds more than {1} trades)",
                            rng.A, HistoricalStore.TradesPerPage))
                    End If
                    _splits += 1
                    Dim m As Long = (rng.A + rng.B) \ 2L
                    stack.Push((m + 1L, rng.B))
                    stack.Push((rng.A, m))
                    Continue While
                End If
                For Each t As HistoryTrade In page.Trades
                    Dim prev As HistoryTrade = Nothing
                    If into.TryGetValue(t.Rec.TradeSeq, prev) Then
                        If FormatHistoryRow(prev) <> FormatHistoryRow(t) Then
                            _conflicts += 1
                            If _conflictDetail.Length = 0 Then _conflictDetail = "seq " & t.Rec.TradeSeq
                        End If
                    Else
                        into(t.Rec.TradeSeq) = t
                    End If
                Next
            End While
            s = e + 1L
        End While
    End Function

    Private Shared Function MissingRuns(lo As Long, hi As Long, have As Dictionary(Of Long, HistoryTrade)) As List(Of (A As Long, B As Long))
        Dim runs As New List(Of (A As Long, B As Long))()
        Dim runStart As Long = -1
        For q As Long = lo To hi
            If have.ContainsKey(q) Then
                If runStart >= 0 Then runs.Add((runStart, q - 1L)) : runStart = -1
            ElseIf runStart < 0 Then
                runStart = q
            End If
        Next
        If runStart >= 0 Then runs.Add((runStart, hi))
        Return runs
    End Function

    ''' <summary>
    ''' Fetch one UTC day. Does not write. The day's trades are those with timestamp in
    ''' [d0, d1); the seq range [lo, hi] that is paged runs from the first trade at or after
    ''' d0 − AnchorOvershootMs to the first trade at or after d1, so it covers the day whatever
    ''' order the host returns inside a millisecond, and its contiguity is checked end to end.
    ''' Missing seqs are re-requested once; any still missing make the day GAP (written, not
    ''' complete). A data failure makes it FAILED (not written).
    ''' </summary>
    ''' <param name="cutoffMs">Now − SettleMarginMs at the start of the run. A day ending after it
    ''' is refused (FAILED) — the planner never offers one; this is the second guard.</param>
    Public Async Function FetchDayAsync(day As DateTime, cutoffMs As Long) As Task(Of HistoryDayResult)
        _requests = 0 : _retries = 0 : _splits = 0 : _gapRepages = 0 : _conflicts = 0 : _conflictDetail = ""
        Dim sw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        Dim res As New HistoryDayResult()
        Dim en As HistoryDayEntry = res.Entry
        en.Day = day.Date
        Dim d0 As Long = DayStartMs(day)
        Dim d1 As Long = d0 + DayMs
        Try
            If d1 > cutoffMs Then
                Throw New HistoryDataException(String.Format(CultureInfo.InvariantCulture,
                    "day ends {0} ms after the settle cutoff (now − {1} ms); not fetched", d1 - cutoffMs, SettleMarginMs))
            End If
            Dim lo As Long = Await AnchorSeqAsync(d0 - AnchorOvershootMs, d1 - 1L)
            If lo < 0 Then Throw New HistoryDataException("no trade found in the day")
            Dim hi As Long = Await AnchorSeqAsync(d1, d1 + AnchorSearchMs - 1L)
            If hi < 0 Then Throw New HistoryDataException("no trade found after the day end")
            If hi < lo Then Throw New HistoryDataException("high anchor below low anchor")

            Dim got As New Dictionary(Of Long, HistoryTrade)()
            Await PageRangeAsync(lo, hi, got)
            Dim runs = MissingRuns(lo, hi, got)
            If runs.Count > 0 Then
                For Each r In runs
                    _gapRepages += 1
                    Await PageRangeAsync(r.A, r.B, got)
                Next
                runs = MissingRuns(lo, hi, got)
            End If
            If _conflicts > 0 Then
                Throw New HistoryDataException(_conflicts & " seq(s) served twice with different content, first " & _conflictDetail)
            End If

            Dim dayTrades As New List(Of HistoryTrade)()
            For Each t As HistoryTrade In got.Values
                If t.Rec.Timestamp >= d0 AndAlso t.Rec.Timestamp < d1 Then dayTrades.Add(t)
            Next
            dayTrades.Sort(Function(a, b) a.Rec.TradeSeq.CompareTo(b.Rec.TradeSeq))
            If dayTrades.Count = 0 Then Throw New HistoryDataException("no trade inside the day after paging")

            Dim missing As Long = 0
            For Each r In runs
                missing += r.B - r.A + 1L
            Next
            For i As Integer = 0 To dayTrades.Count - 1
                Dim t As TradeRecord = dayTrades(i).Rec
                If t.Timestamp >= cutoffMs Then
                    Throw New HistoryDataException("a trade at or after the settle cutoff reached the write set")
                End If
                If i > 0 AndAlso t.Timestamp < dayTrades(i - 1).Rec.Timestamp Then en.TsInversions += 1
                If Not String.Equals(t.Liquidation, "none", StringComparison.Ordinal) AndAlso
                   Not String.IsNullOrEmpty(t.Liquidation) Then en.LiqFlagged += 1
                If LosesPrecision(t.Price) OrElse LosesPrecision(t.Amount) Then en.PrecisionLoss += 1
            Next
            en.Rows = dayTrades.Count
            en.FirstSeq = dayTrades(0).Rec.TradeSeq
            en.LastSeq = dayTrades(dayTrades.Count - 1).Rec.TradeSeq
            en.MissingSeqs = missing
            If missing = 0 Then
                en.Status = StatusOk
            Else
                en.Status = StatusGap
                en.Detail = runs.Count & " missing run(s), first " & runs(0).A & "-" & runs(0).B
            End If
            res.Trades = dayTrades
        Catch ex As HistoryDataException
            en.Status = StatusFailed
            en.Detail = ex.Message
            res.Trades = New List(Of HistoryTrade)()
        End Try
        en.Requests = _requests
        en.Retries = _retries
        en.HasMoreSplits = _splits
        en.FetchedAtUtc = DateTimeOffset.FromUnixTimeMilliseconds(_clockMs()).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
        If _gapRepages > 0 AndAlso en.Status = StatusOk Then en.Detail = _gapRepages & " missing run(s) recovered on re-request"
        res.Seconds = sw.Elapsed.TotalSeconds
        Return res
    End Function

    ''' <summary>True when FormatRow's two-decimal format would change the value.</summary>
    Private Shared Function LosesPrecision(v As Double) As Boolean
        Dim x As Double = v * 100.0
        Return Math.Abs(x - Math.Round(x)) > 0.000001
    End Function

    ' ── Planning ────────────────────────────────────────────────────────────────────────

    ''' <summary>
    ''' The days a run will fetch: every day in [fromDay, toDayExcl) whose end is at or before
    ''' cutoffMs and whose checkpoint status is not OK. Never-fetched days come first, NEWEST
    ''' FIRST (ruling HDS-2); GAP and FAILED days follow, newest first, so a chunked run
    ''' (--max-days) does not spend the head of every chunk re-trying the same bad day.
    ''' </summary>
    Public Shared Function PlanDays(fromDay As DateTime, toDayExcl As DateTime, cutoffMs As Long,
                                    checkpoint As Dictionary(Of String, HistoryDayEntry),
                                    ByRef alreadyComplete As Integer, ByRef unsettled As Integer) As List(Of DateTime)
        alreadyComplete = 0
        unsettled = 0
        Dim plan As New List(Of DateTime)()
        Dim retry As New List(Of DateTime)()
        Dim d As DateTime = toDayExcl.Date.AddDays(-1)
        While d >= fromDay.Date
            If DayStartMs(d) + DayMs > cutoffMs Then
                unsettled += 1
            Else
                Dim en As HistoryDayEntry = Nothing
                If checkpoint IsNot Nothing Then checkpoint.TryGetValue(DayKey(d), en)
                If en Is Nothing Then
                    plan.Add(d)
                ElseIf en.Status = StatusOk Then
                    alreadyComplete += 1
                Else
                    retry.Add(d)
                End If
            End If
            d = d.AddDays(-1)
        End While
        plan.AddRange(retry)
        Return plan
    End Function

    ''' <summary>The newest day whose end is at or before now − SettleMarginMs.</summary>
    Public Shared Function LastSettledDay(nowMs As Long) As DateTime
        Return DayOfMs(nowMs - SettleMarginMs - DayMs)
    End Function

    ' ── Runs ────────────────────────────────────────────────────────────────────────────

    ''' <summary>Backfill [fromDay, toDayExcl), newest day first, resumable. Holds the store
    ''' lock for the whole run.</summary>
    Public Async Function RunAsync(fromDay As DateTime, toDayExcl As DateTime, Optional maxDays As Integer = 0) As Task(Of HistoryRunResult)
        Dim result As New HistoryRunResult()
        Dim runSw As Diagnostics.Stopwatch = Diagnostics.Stopwatch.StartNew()
        Directory.CreateDirectory(_storeDir)
        Dim lockStream As FileStream = Nothing
        Try
            lockStream = New FileStream(Path.Combine(_storeDir, LockFileName), FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                        FileShare.None, 1, FileOptions.DeleteOnClose)
        Catch ex As IOException
            result.StoppedReason = "store lock held by another run (" & Path.Combine(_storeDir, LockFileName) & ")"
            result.ExitCode = ExitBadArgsOrStore
            Return result
        End Try
        Using lockStream
            DeleteStaleTmps()
            Dim cp As Dictionary(Of String, HistoryDayEntry)
            Try
                cp = LoadCheckpoint(_storeDir)
            Catch ex As HistoryStoreException
                result.StoppedReason = ex.Message
                result.ExitCode = ExitBadArgsOrStore
                Return result
            End Try
            Dim cutoffMs As Long = _clockMs() - SettleMarginMs
            Dim ac, uns As Integer
            Dim plan As List(Of DateTime) = PlanDays(fromDay, toDayExcl, cutoffMs, cp, ac, uns)
            result.AlreadyComplete = ac
            result.Unsettled = uns
            result.Planned = plan.Count
            _log(String.Format(CultureInfo.InvariantCulture,
                 "[history] store {0} | range {1} .. {2} (to exclusive) | to fetch {3} | already complete {4} | not settled yet {5} | cutoff {6:yyyy-MM-dd HH:mm}Z",
                 _storeDir, DayKey(fromDay), DayKey(toDayExcl), plan.Count, ac, uns,
                 DateTimeOffset.FromUnixTimeMilliseconds(cutoffMs).UtcDateTime))
            If plan.Count = 0 Then
                result.ExitCode = ExitOk
                result.Seconds = runSw.Elapsed.TotalSeconds
                Return result
            End If
            AppendLog("RUN start range=" & DayKey(fromDay) & ".." & DayKey(toDayExcl) & " planned=" & plan.Count)

            Dim done As Integer = 0
            For Each day As DateTime In plan
                If maxDays > 0 AndAlso done >= maxDays Then Exit For
                Dim dr As HistoryDayResult
                Try
                    dr = Await FetchDayAsync(day, cutoffMs)
                Catch ex As HistoryTransportException
                    result.Requests += _requests
                    result.Retries += _retries
                    result.StoppedReason = "transport: " & ex.Message
                    result.ExitCode = ExitTransportStop
                    AppendLog("DAY " & DayKey(day) & " STOPPED " & ex.Message)
                    _log("[history] STOP " & DayKey(day) & ": " & ex.Message)
                    Exit For
                End Try
                result.Requests += dr.Entry.Requests
                result.Retries += dr.Entry.Retries
                If dr.Entry.Status <> StatusFailed Then
                    Try
                        MergeDayIntoMonth(_storeDir, day, dr.Trades, _stageHook)
                    Catch ex As Exception When TypeOf ex Is HistoryStoreException OrElse TypeOf ex Is IOException OrElse
                                               TypeOf ex Is UnauthorizedAccessException
                        result.StoppedReason = "store write: " & ex.Message
                        result.ExitCode = ExitBadArgsOrStore
                        AppendLog("DAY " & DayKey(day) & " STORE_ERROR " & ex.Message)
                        _log("[history] STOP " & DayKey(day) & ": store write failed: " & ex.Message)
                        Exit For
                    End Try
                End If
                cp(DayKey(day)) = dr.Entry
                SaveCheckpoint(_storeDir, cp)
                _stageHook("after-checkpoint")
                done += 1
                Select Case dr.Entry.Status
                    Case StatusOk : result.DaysOk += 1
                    Case StatusGap : result.DaysGap += 1
                    Case Else : result.DaysFailed += 1
                End Select
                result.Trades += dr.Entry.Rows
                Dim line As String = DayLine(dr)
                AppendLog(line)
                _log(line)
            Next
            result.RemainingNotDone = plan.Count - done
            result.Seconds = runSw.Elapsed.TotalSeconds
            If result.ExitCode = ExitOk Then
                If result.DaysGap + result.DaysFailed > 0 Then
                    result.ExitCode = ExitDaysIncomplete
                    result.StoppedReason = "walked the planned days; " & result.DaysGap & " GAP and " & result.DaysFailed & " FAILED (re-run retries them)"
                End If
            End If
            AppendLog(String.Format(CultureInfo.InvariantCulture,
                      "RUN end exit={0} ok={1} gap={2} failed={3} remaining={4} trades={5} requests={6} retries={7} secs={8:F0} {9}",
                      result.ExitCode, result.DaysOk, result.DaysGap, result.DaysFailed, result.RemainingNotDone,
                      result.Trades, result.Requests, result.Retries, result.Seconds, result.StoppedReason))
        End Using
        Return result
    End Function

    Private Shared Function DayLine(dr As HistoryDayResult) As String
        Dim e As HistoryDayEntry = dr.Entry
        Dim rate As Double = If(dr.Seconds > 0, e.Rows / dr.Seconds, 0)
        Return String.Format(CultureInfo.InvariantCulture,
            "DAY {0} {1} rows={2} seq={3}..{4} missing={5} liq={6} req={7} retries={8} splits={9} ts_inversions={10} precision_loss={11} secs={12:F1} rate={13:F0}/s{14}",
            DayKey(e.Day), e.Status, e.Rows, e.FirstSeq, e.LastSeq, e.MissingSeqs, e.LiqFlagged, e.Requests, e.Retries,
            e.HasMoreSplits, e.TsInversions, e.PrecisionLoss, dr.Seconds, rate,
            If(String.IsNullOrEmpty(e.Detail), "", " detail=" & e.Detail))
    End Function

    Private Sub AppendLog(line As String)
        Try
            File.AppendAllText(Path.Combine(_storeDir, LogFileName),
                               DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) & " " & line & vbLf)
        Catch ex As Exception
            _log("[history] could not write the run log: " & ex.Message)
        End Try
    End Sub

    Private Sub DeleteStaleTmps()
        For Each f As String In Directory.GetFiles(_storeDir, "*" & TmpSuffix)
            Try
                File.Delete(f)
            Catch
            End Try
        Next
    End Sub

    ' ── The month-file merge (spec §3.3, audit rows E1 and E2) ──────────────────────────

    ''' <summary>
    ''' Replace one day's rows in its month file, atomically. The existing file is streamed:
    ''' rows before the day are copied, the day's old rows are DROPPED, the new rows go in, rows
    ''' after the day are copied. Written to a tmp file, flushed to disk, re-read and counted,
    ''' and only then renamed over the month file. A re-run of a day therefore replaces it, never
    ''' appends to it. Throws <see cref="HistoryStoreException"/> (nothing renamed) on a foreign
    ''' header, an unparseable row, rows out of day order, or a re-read that does not match.
    ''' </summary>
    Public Shared Sub MergeDayIntoMonth(storeDir As String, day As DateTime, dayRows As List(Of HistoryTrade),
                                        Optional stageHook As Action(Of String) = Nothing)
        Dim target As String = TradeStoreWriter.TradeFileFor(storeDir, day.Year, day.Month)
        Dim tmp As String = target & TmpSuffix
        Dim d0 As Long = DayStartMs(day)
        Dim d1 As Long = d0 + DayMs
        Dim kept As Long = 0
        Dim renamed As Boolean = False
        Try
            Using fs As New FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16)
                Using w As New StreamWriter(fs, New UTF8Encoding(False), 1 << 16)
                    w.NewLine = vbLf
                    w.WriteLine(HeaderLine)
                    Dim inserted As Boolean = False
                    If File.Exists(target) Then
                        Using sr As New StreamReader(target, New UTF8Encoding(False))
                            Dim header As String = sr.ReadLine()
                            If header IsNot Nothing AndAlso header <> HeaderLine Then
                                Throw New HistoryStoreException("foreign header in " & target & " (not a history store file): " & header)
                            End If
                            Dim lineNo As Long = 1
                            Dim prevDay As Long = Long.MinValue
                            Do
                                Dim line As String = sr.ReadLine()
                                If line Is Nothing Then Exit Do
                                lineNo += 1
                                Dim rec As TradeRecord = Nothing
                                Dim x As HistoryExtras = Nothing
                                If Not TryParseHistoryRow(line, rec, x) Then
                                    Throw New HistoryStoreException("unparseable row at line " & lineNo & " of " & target & " — refusing to rewrite (audit row E1)")
                                End If
                                Dim rowDay As Long = DayFloorMs(rec.Timestamp)
                                If rowDay < prevDay Then
                                    Throw New HistoryStoreException("rows out of day order at line " & lineNo & " of " & target)
                                End If
                                prevDay = rowDay
                                If rec.Timestamp < d0 Then
                                    w.WriteLine(line)
                                    kept += 1
                                ElseIf rec.Timestamp < d1 Then
                                    ' The day's previous rows: replaced, never kept beside the new ones.
                                Else
                                    If Not inserted Then
                                        WriteRows(w, dayRows)
                                        inserted = True
                                    End If
                                    w.WriteLine(line)
                                    kept += 1
                                End If
                            Loop
                        End Using
                    End If
                    If Not inserted Then WriteRows(w, dayRows)
                    w.Flush()
                    fs.Flush(True)
                End Using
            End Using

            ' E2: count only what is on disk. Re-read the flushed tmp and check it.
            Dim total As Long = 0
            Dim inDay As Long = 0
            Using sr As New StreamReader(tmp, New UTF8Encoding(False))
                If sr.ReadLine() <> HeaderLine Then Throw New HistoryStoreException("re-read: header mismatch in " & tmp)
                Do
                    Dim line As String = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim rec As TradeRecord = Nothing
                    Dim x As HistoryExtras = Nothing
                    If Not TryParseHistoryRow(line, rec, x) Then Throw New HistoryStoreException("re-read: unparseable row in " & tmp)
                    total += 1
                    If rec.Timestamp >= d0 AndAlso rec.Timestamp < d1 Then inDay += 1
                Loop
            End Using
            If inDay <> dayRows.Count OrElse total <> kept + dayRows.Count Then
                Throw New HistoryStoreException(String.Format(CultureInfo.InvariantCulture,
                    "re-read mismatch in {0}: day rows {1} (expected {2}), total {3} (expected {4})",
                    tmp, inDay, dayRows.Count, total, kept + dayRows.Count))
            End If
            If stageHook IsNot Nothing Then stageHook("after-tmp")
            File.Move(tmp, target, True)
            renamed = True
            If stageHook IsNot Nothing Then stageHook("after-rename")
        Finally
            If Not renamed Then
                Try
                    If File.Exists(tmp) Then File.Delete(tmp)
                Catch
                End Try
            End If
        End Try
    End Sub

    Private Shared Sub WriteRows(w As StreamWriter, rows As List(Of HistoryTrade))
        For Each t As HistoryTrade In rows
            w.WriteLine(FormatHistoryRow(t))
        Next
    End Sub

    ' ── Checkpoint ──────────────────────────────────────────────────────────────────────

    ''' <summary>Read the checkpoint. Empty when absent. Throws on a foreign header; a row that
    ''' does not parse is ignored (its day is simply fetched again).</summary>
    Public Shared Function LoadCheckpoint(storeDir As String) As Dictionary(Of String, HistoryDayEntry)
        Dim cp As New Dictionary(Of String, HistoryDayEntry)(StringComparer.Ordinal)
        Dim p As String = Path.Combine(storeDir, CheckpointFileName)
        If Not File.Exists(p) Then Return cp
        Dim lines As String() = File.ReadAllLines(p)
        If lines.Length = 0 Then Return cp
        If lines(0) <> CheckpointHeader Then Throw New HistoryStoreException("foreign checkpoint header in " & p)
        For i As Integer = 1 To lines.Length - 1
            Dim en As HistoryDayEntry = ParseCheckpointRow(lines(i))
            If en IsNot Nothing Then cp(DayKey(en.Day)) = en
        Next
        Return cp
    End Function

    Private Shared Function ParseCheckpointRow(line As String) As HistoryDayEntry
        Dim p As String() = line.Split(","c)
        If p.Length <> 14 Then Return Nothing
        Dim d As DateTime
        If Not DateTime.TryParseExact(p(0), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                      DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, d) Then Return Nothing
        ' Columns 2..11 are the ten counters, in CheckpointHeader order.
        Dim n(9) As Long
        For k As Integer = 0 To 9
            If Not Long.TryParse(p(k + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, n(k)) Then Return Nothing
        Next
        Return New HistoryDayEntry With {
            .Day = d.Date, .Status = p(1), .Rows = n(0), .FirstSeq = n(1), .LastSeq = n(2), .MissingSeqs = n(3),
            .LiqFlagged = n(4), .Requests = n(5), .Retries = n(6), .HasMoreSplits = n(7), .TsInversions = n(8),
            .PrecisionLoss = n(9), .FetchedAtUtc = p(12), .Detail = p(13)}
    End Function

    Private Shared Function FormatCheckpointRow(e As HistoryDayEntry) As String
        Dim detail As String = If(e.Detail, "").Replace(",", ";").Replace(vbCr, " ").Replace(vbLf, " ")
        Return String.Join(",", {DayKey(e.Day), e.Status,
            e.Rows.ToString(CultureInfo.InvariantCulture), e.FirstSeq.ToString(CultureInfo.InvariantCulture),
            e.LastSeq.ToString(CultureInfo.InvariantCulture), e.MissingSeqs.ToString(CultureInfo.InvariantCulture),
            e.LiqFlagged.ToString(CultureInfo.InvariantCulture), e.Requests.ToString(CultureInfo.InvariantCulture),
            e.Retries.ToString(CultureInfo.InvariantCulture), e.HasMoreSplits.ToString(CultureInfo.InvariantCulture),
            e.TsInversions.ToString(CultureInfo.InvariantCulture), e.PrecisionLoss.ToString(CultureInfo.InvariantCulture),
            e.FetchedAtUtc, detail})
    End Function

    ''' <summary>Rewrite the whole checkpoint atomically (tmp, flush, rename), days ascending.</summary>
    Public Shared Sub SaveCheckpoint(storeDir As String, cp As Dictionary(Of String, HistoryDayEntry))
        Dim p As String = Path.Combine(storeDir, CheckpointFileName)
        Dim tmp As String = p & TmpSuffix
        Using fs As New FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
            Using w As New StreamWriter(fs, New UTF8Encoding(False))
                w.NewLine = vbLf
                w.WriteLine(CheckpointHeader)
                For Each k As String In cp.Keys.OrderBy(Function(s) s, StringComparer.Ordinal)
                    w.WriteLine(FormatCheckpointRow(cp(k)))
                Next
                w.Flush()
                fs.Flush(True)
            End Using
        End Using
        File.Move(tmp, p, True)
    End Sub

End Class
