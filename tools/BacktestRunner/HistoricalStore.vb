' tools/BacktestRunner/HistoricalStore.vb
' Fetch-once historical data store for the backtest synthesizer (docs/backtest-synthesizer
' -proposal.md §3). Stores 1m/3m/5m/15m candles, raw trades (with the liq flag), and funding
' rate history under backtest_data/, keyed monthly per resolution / stream. Re-runs skip
' complete months; a partial trade month resumes from the last-stored timestamp.
'
' Endpoints:
'   Candles  — public/get_tradingview_chart_data     (via DeribitClient.GetCandlesAsync range,
'                                                     inherits ExecuteWithRetry)
'   Trades   — public/get_last_trades_by_instrument_and_time  (local HTTP; not in DeribitClient)
'   Funding  — public/get_funding_rate_history       (local HTTP; not in DeribitClient)
'
' Host-agnostic (no WinForms). Polite: 200ms delay between paginated calls; retry-once on
' 5xx / timeout for the two local-HTTP endpoints (the ExecuteWithRetry discipline, implemented
' here to keep DeribitClient untouched per the task's HARD CONSTRAINT).
'
' Store paths (relative to CWD, which BacktestProgram sets to the repo root):
'   backtest_data/candles_1m_YYYY-MM.csv    ts,open,high,low,close,volume,cost
'   backtest_data/candles_3m_YYYY-MM.csv    same
'   backtest_data/candles_5m_YYYY-MM.csv    same
'   backtest_data/candles_15m_YYYY-MM.csv   same
'   backtest_data/trades_YYYY-MM.csv        ts,price,amount,direction,liquidation
'   backtest_data/funding_YYYY-MM.csv       ts,rate
'   backtest_data/.state.json               resumable cursors (per-stream last-ts)

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading.Tasks

Public Class HistoricalStore

    Public Const StoreDir As String = "backtest_data"
    Public Const InstrumentName As String = "BTC-PERPETUAL"

    ' Polite delay between paginated requests (trades + funding). Deribit's public
    ' endpoints have generous rate limits; 200 ms leaves plenty of headroom and keeps a
    ' months-long backfill from ever tripping them.
    Private Const PoliteDelayMs As Integer = 200

    ' Trades: max 1000 per call is Deribit's documented cap for get_last_trades_*.
    Private Const TradesPerPage As Integer = 1000

    ' Guard against runaway loops on inverted ranges / bad cursors.
    Private Const MaxTradePages As Integer = 200000

    Private Shared ReadOnly _http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}

    Shared Sub New()
        _http.DefaultRequestHeaders.Add("User-Agent", "DeribitBacktestRunner/1.0")
    End Sub

    ' ── Path helpers ──────────────────────────────────────────────────────────────────

    Public Shared Function CandleFileFor(resolution As Integer, year As Integer, month As Integer) As String
        Return Path.Combine(StoreDir, String.Format("candles_{0}m_{1:D4}-{2:D2}.csv", resolution, year, month))
    End Function

    ' [v64] Trade-file naming lives in TradeStoreWriter — the ONE seam shared with the
    ' streaming capture in DeribitWsFeed.ApplyTrades and with the reader below, so the
    ' three cannot drift (in-app-trade-store-capture-proposal.md §2).
    Public Shared Function TradeFileFor(year As Integer, month As Integer) As String
        Return TradeStoreWriter.TradeFileFor(StoreDir, year, month)
    End Function

    Public Shared Function FundingFileFor(year As Integer, month As Integer) As String
        Return Path.Combine(StoreDir, String.Format("funding_{0:D4}-{1:D2}.csv", year, month))
    End Function

    Private Shared Sub EnsureStoreDir()
        Directory.CreateDirectory(StoreDir)
    End Sub

    ' Iterate the (year, month, monthStartUtc, monthEndExclusiveUtc) tuples that cover
    ' [fromUtc, toUtc]. Boundaries snap to first-of-month; a partial trailing month is
    ' capped at toUtc.
    Public Shared Iterator Function EnumerateMonths(fromUtc As DateTime, toUtc As DateTime) _
            As IEnumerable(Of (Year As Integer, Month As Integer, StartUtc As DateTime, EndUtcExcl As DateTime))
        Dim cur As New DateTime(fromUtc.Year, fromUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
        Dim endCap As DateTime = toUtc
        While cur < endCap
            Dim nxt As DateTime = cur.AddMonths(1)
            Dim segStart As DateTime = If(cur < fromUtc, fromUtc, cur)
            Dim segEnd   As DateTime = If(nxt > endCap, endCap, nxt)
            Yield (cur.Year, cur.Month, segStart, segEnd)
            cur = nxt
        End While
    End Function

    ' ── Candle backfill ───────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Fetch (if needed) and store one calendar month of candles at the given resolution.
    ''' If the month file already exists AND covers the entire requested [segStart, segEnd),
    ''' it is left alone (fetch-once). Otherwise the file is rewritten in full for the
    ''' segment window — candles are cheap (~44k rows/month at 1m) and this avoids
    ''' complex merge logic. Returns the row count written.
    ''' </summary>
    Public Shared Async Function BackfillCandleMonthAsync(
            resolution As Integer, year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime) As Task(Of Integer)
        EnsureStoreDir()
        Dim path As String = CandleFileFor(resolution, year, month)
        If File.Exists(path) AndAlso MonthFileCovers(path, segStart, segEndExcl) Then
            Return CountDataRows(path)
        End If

        Dim startMs As Long = New DateTimeOffset(segStart,   TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs   As Long = New DateTimeOffset(segEndExcl.AddMilliseconds(-1), TimeSpan.Zero).ToUnixTimeMilliseconds()

        ' Deribit's get_tradingview_chart_data caps each response at ~5001 ticks. For a
        ' multi-day 1m fetch that under-reports silently (only the trailing window
        ' comes back). Chunk into ~4000-candle segments per call — safely under the cap
        ' at every resolution — and stitch the results (dedup by Timestamp).
        Dim chunkMs As Long = CLng(resolution) * 60L * 1000L * 4000L
        Dim collected As New SortedDictionary(Of Long, Candle)()
        Dim cursor As Long = startMs
        While cursor <= endMs
            Dim chunkEnd As Long = Math.Min(endMs, cursor + chunkMs - 1)
            Dim chunk As List(Of Candle) = Await DeribitClient.GetCandlesAsync(resolution.ToString(), cursor, chunkEnd)
            If chunk Is Nothing Then
                Console.Error.WriteLine(String.Format(
                    "[HistoricalStore] Candle fetch failed for {0}m {1:D4}-{2:D2} @ {3}",
                    resolution, year, month, cursor))
                Exit While
            End If
            For Each c In chunk
                collected(c.Timestamp) = c
            Next
            cursor = chunkEnd + 1
            If chunk.Count > 0 Then
                Await Task.Delay(PoliteDelayMs)
            End If
        End While

        If collected.Count = 0 Then Return 0
        Using sw As New StreamWriter(path, append:=False)
            sw.WriteLine("Timestamp,Open,High,Low,Close,Volume,Cost")
            For Each c In collected.Values
                sw.WriteLine(String.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F6},{6:F2}",
                    c.Timestamp, c.Open, c.High, c.Low, c.Close, c.Volume, c.VolumeUSD))
            Next
        End Using
        Return collected.Count
    End Function

    Private Shared Function MonthFileCovers(path As String, segStart As DateTime, segEndExcl As DateTime) As Boolean
        ' Sanity: if the file spans at least segStart's day to segEnd's day - 1, treat as
        ' covering. Cheap heuristic; candle files are line-append order so first/last are
        ' at the top/bottom.
        Try
            Dim first As Long = -1
            Dim last  As Long = -1
            Using sr As New StreamReader(path)
                Dim header = sr.ReadLine()
                Dim line As String = sr.ReadLine()
                If line IsNot Nothing Then
                    first = ParseFirstColLong(line)
                    Dim prev As String = line
                    Do
                        Dim nxt = sr.ReadLine()
                        If nxt Is Nothing Then Exit Do
                        prev = nxt
                    Loop
                    last = ParseFirstColLong(prev)
                End If
            End Using
            If first < 0 OrElse last < 0 Then Return False
            Dim firstUtc As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(first).UtcDateTime
            Dim lastUtc  As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(last).UtcDateTime
            Return firstUtc <= segStart.AddMinutes(1) AndAlso lastUtc >= segEndExcl.AddMinutes(-2)
        Catch
            Return False
        End Try
    End Function

    Private Shared Function ParseFirstColLong(line As String) As Long
        Dim comma As Integer = line.IndexOf(","c)
        If comma <= 0 Then Return -1
        Dim n As Long
        If Long.TryParse(line.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, n) Then Return n
        Return -1
    End Function

    Private Shared Function CountDataRows(path As String) As Integer
        Dim n As Integer = 0
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                While sr.ReadLine() IsNot Nothing
                    n += 1
                End While
            End Using
        Catch
        End Try
        Return n
    End Function

    ''' <summary>Load one candle-month file into memory. Empty list on any error.</summary>
    Public Shared Function LoadCandleMonth(resolution As Integer, year As Integer, month As Integer) As List(Of Candle)
        Dim result As New List(Of Candle)
        Dim path As String = CandleFileFor(resolution, year, month)
        If Not File.Exists(path) Then Return result
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim parts = line.Split(","c)
                    If parts.Length < 7 Then Continue Do
                    Dim c As New Candle()
                    c.Timestamp = Long.Parse(parts(0), CultureInfo.InvariantCulture)
                    c.Open      = Double.Parse(parts(1), CultureInfo.InvariantCulture)
                    c.High      = Double.Parse(parts(2), CultureInfo.InvariantCulture)
                    c.Low       = Double.Parse(parts(3), CultureInfo.InvariantCulture)
                    c.Close     = Double.Parse(parts(4), CultureInfo.InvariantCulture)
                    c.Volume    = Double.Parse(parts(5), CultureInfo.InvariantCulture)
                    c.VolumeUSD = Double.Parse(parts(6), CultureInfo.InvariantCulture)
                    result.Add(c)
                Loop
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("[HistoricalStore] LoadCandleMonth failed: " & ex.Message)
        End Try
        Return result
    End Function

    ''' <summary>Load the union of candle months covering [fromUtc, toUtc] and preceding
    ''' warm-up (needed for the 250-bar window at the leftmost bar-close). Chronological
    ''' ascending. Duplicate timestamps deduped keeping the last (newer file wins on the
    ''' seam).</summary>
    Public Shared Function LoadCandleRange(resolution As Integer,
                                            warmupStartUtc As DateTime,
                                            toUtc As DateTime) As List(Of Candle)
        Dim all As New List(Of Candle)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            all.AddRange(LoadCandleMonth(resolution, m.Year, m.Month))
        Next
        ' Dedup by Timestamp, keep the last-encountered.
        Dim map As New SortedDictionary(Of Long, Candle)()
        For Each c In all
            map(c.Timestamp) = c
        Next
        Return map.Values.ToList()
    End Function

    ' ── Trades backfill ───────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Fetch trades for one calendar-month segment [segStart, segEndExcl), appending
    ''' to the month file. Resumable: if the month file already exists, we resume from
    ''' the last recorded timestamp + 1 ms. Rows are written in ascending order (the
    ''' F1 contract).
    '''
    ''' [v64] Rows are committed through TradeStoreWriter.AppendRows — the SAME seam the
    ''' streaming capture writes through — so format, header-on-create and monthly rollover
    ''' cannot drift between the two producers, and the process-wide append lock keeps a
    ''' backfill page from interleaving with a streaming flush.
    ''' </summary>
    ''' <param name="storeDir">Nothing ⇒ the CWD-relative repo store (BacktestProgram sets
    ''' CWD to the repo root). The in-app gap repair passes the EXE-resolved dir (D3).</param>
    ''' <param name="clampToSegStart">[v64 gap repair] True ⇒ never reach further back than
    ''' segStart even when the on-disk resume point is older. Deribit's public trades endpoint
    ''' refuses windows past its ~24 h retention, so after a long outage an unclamped resume
    ''' cursor would ask for a refused window and recover NOTHING — including the last 20 h
    ''' that are still served. False (the default) preserves the historical-backfill
    ''' behaviour exactly: resume from disk and fill any hole between there and segEnd.</param>
    Public Shared Async Function BackfillTradeMonthAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime,
            Optional storeDir As String = Nothing,
            Optional clampToSegStart As Boolean = False) As Task(Of Integer)
        Dim dir As String = If(String.IsNullOrWhiteSpace(storeDir), StoreDir, storeDir)
        Directory.CreateDirectory(dir)
        Dim path As String = TradeStoreWriter.TradeFileFor(dir, year, month)
        Dim segStartMs As Long = New DateTimeOffset(segStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs As Long = New DateTimeOffset(segEndExcl, TimeSpan.Zero).ToUnixTimeMilliseconds() - 1

        ' Resume decision lives on the shared seam (A48d exercises this exact call).
        Dim cursorMs As Long = TradeStoreWriter.ResolveResumeCursorMs(path, segStartMs, endMs, clampToSegStart)
        If cursorMs < 0 Then Return CountDataRows(path)

        Dim total As Integer = 0
        Dim page As Integer = 0

        Do
            If page >= MaxTradePages Then
                Console.Error.WriteLine("[HistoricalStore] Trade page cap hit — aborting month " &
                                        String.Format("{0:D4}-{1:D2}", year, month))
                Exit Do
            End If

            Dim trades As List(Of TradeRecord) =
                Await FetchTradesByTimeAsync(cursorMs, endMs, TradesPerPage)
            If trades Is Nothing Then
                Console.Error.WriteLine("[HistoricalStore] Trade fetch failed at cursor " & cursorMs)
                Exit Do
            End If
            If trades.Count = 0 Then Exit Do

            total += TradeStoreWriter.AppendRows(dir, trades)

            Dim newestMs As Long = trades(trades.Count - 1).Timestamp
            ' newestMs <= cursorMs means Deribit returned a full page all stamped the same
            ' ms; the +1 nudge is what stops that from looping forever.
            cursorMs = newestMs + 1
            page += 1

            If trades.Count < TradesPerPage Then
                ' Fewer than a full page ⇒ no more trades in the window.
                Exit Do
            End If

            Await Task.Delay(PoliteDelayMs)
        Loop

        Console.WriteLine(String.Format(
            "[HistoricalStore] Trades {0:D4}-{1:D2}: appended {2} rows across {3} page(s)",
            year, month, total, page))
        Return total
    End Function

    ''' <summary>One paginated call to get_last_trades_by_instrument_and_time. Ascending
    ''' order guaranteed by sorting=asc. Local retry-once on transient failures (the
    ''' ExecuteWithRetry discipline reproduced here so DeribitClient can stay untouched).</summary>
    Public Shared Async Function FetchTradesByTimeAsync(startMs As Long, endMs As Long, count As Integer) _
            As Task(Of List(Of TradeRecord))
        Dim url As String = "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time" &
                            "?instrument_name=" & InstrumentName &
                            "&start_timestamp=" & startMs &
                            "&end_timestamp=" & endMs &
                            "&count=" & count &
                            "&sorting=asc"
        For attempt As Integer = 1 To 2
            Dim needsRetry As Boolean = False
            Try
                Dim json = Await _http.GetStringAsync(url)
                Dim doc  = JsonDocument.Parse(json)
                Dim result = doc.RootElement.GetProperty("result")
                Dim tradesEl = result.GetProperty("trades")
                Dim list As New List(Of TradeRecord)()
                For Each t In tradesEl.EnumerateArray()
                    Dim rec As New TradeRecord()
                    rec.Price     = t.GetProperty("price").GetDouble()
                    rec.Amount    = t.GetProperty("amount").GetDouble()
                    rec.Direction = t.GetProperty("direction").GetString()
                    rec.Timestamp = t.GetProperty("timestamp").GetInt64()
                    Dim liqEl As JsonElement = Nothing
                    rec.Liquidation = If(t.TryGetProperty("liquidation", liqEl), liqEl.GetString(), "none")
                    list.Add(rec)
                Next
                Return list
            Catch ex As HttpRequestException
                If attempt < 2 AndAlso ex.StatusCode.HasValue AndAlso CInt(ex.StatusCode.Value) >= 500 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Trades HTTP failure: " & ex.Message)
                    Return Nothing
                End If
            Catch ex As TaskCanceledException
                If attempt < 2 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Trades timeout")
                    Return Nothing
                End If
            Catch ex As Exception
                Console.Error.WriteLine("[HistoricalStore] Trades error: " & ex.Message)
                Return Nothing
            End Try
            If needsRetry Then Await Task.Delay(500)
        Next
        Return Nothing
    End Function

    ''' <summary>Load the union of trade months for [warmupStartUtc, toUtc], chronological
    ''' ascending (the F1 contract). Duplicates deduped on the whole row, which is what makes
    ''' a gap-repair pass overlapping streamed data harmless at read time (A48d).
    '''
    ''' [v64] The per-file parse delegates to TradeStoreWriter.ReadTradeFile — the same seam
    ''' that formats the rows — so reader and writer cannot drift. Sorting here (rather than
    ''' assuming file order) is what tolerates a backfill page landing after a streaming
    ''' flush of newer trades.</summary>
    Public Shared Function LoadTradeRange(warmupStartUtc As DateTime, toUtc As DateTime) As List(Of TradeRecord)
        Dim all As New List(Of TradeRecord)()
        Dim seen As New HashSet(Of String)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            For Each rec In TradeStoreWriter.ReadTradeFile(TradeFileFor(m.Year, m.Month))
                If Not seen.Add(TradeStoreWriter.FormatRow(rec)) Then Continue For
                all.Add(rec)
            Next
        Next
        all.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))
        Return all
    End Function

    ' ── Funding backfill ──────────────────────────────────────────────────────────────
    ' The sample type is the TOP-LEVEL BacktestFundingSample (declared in ReplayLoop.vb) —
    ' keeps HistoricalStore's HttpClient off the harness's link surface.

    ' Funding samples land on the hour, every hour (verified: 0 off-hour samples across
    ' Feb–Jun 2026, and 3,637 of 3,643 intervals exactly 60 min).
    Private Const FundingIntervalMs As Long = 3600000L

    ''' <summary>
    ''' Fetch and store one calendar-month of funding rate history. The endpoint returns the
    ''' full range in one call (~720 hourly samples/month), so pagination is unnecessary.
    '''
    ''' [2026-07-31 fix] Two defects lived here and both are closed:
    '''
    ''' (1) **Exclusive start.** `start_timestamp` is EXCLUSIVE — verified against the live
    '''     endpoint: a request from exactly 2026-06-01T00:00:00.000Z returns 01:00 first,
    '''     the same request minus 1 ms returns 00:00. Since each month's window began at the
    '''     boundary instant, **every month silently lost its 00:00 sample** — 5 of 5 internal
    '''     seams in the store (Feb 671/672, Mar 743/744, Apr 719/720, May 743/744,
    '''     Jun 719/720: exactly one missing each, always the boundary). Fixed by fetching one
    '''     interval early and filtering the result back to the segment, which is correct
    '''     under either inclusive or exclusive semantics.
    '''
    ''' (2) **Fetch-once with no coverage check.** The guard was `If File.Exists(path)`, so a
    '''     partial month file was frozen PERMANENTLY. That is what produced the 28.2-day hole
    '''     (2026-06-30 23:00 → 2026-07-29 05:00 UTC): a narrow early fetch on 07-30 created
    '''     `funding_2026-07.csv` with 30 samples, and the 6-month fetch the next day skipped
    '''     the month entirely on File.Exists. The candle path never had this bug because it
    '''     checks `MonthFileCovers`; funding had no equivalent. Fixed by comparing the stored
    '''     in-range count against the expected hourly count, and MERGING on refetch rather
    '''     than rewriting — so repeated runs accumulate coverage instead of churning it.
    '''
    ''' Cost of the coverage check: a month the VENUE genuinely cannot fill stays short and
    ''' costs one redundant call per run. At one call per month that is not worth extra state
    ''' to avoid.
    ''' </summary>
    Public Shared Async Function BackfillFundingMonthAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime) As Task(Of Integer)
        EnsureStoreDir()
        Dim path As String = FundingFileFor(year, month)

        Dim segStartMs As Long = New DateTimeOffset(segStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs      As Long = New DateTimeOffset(segEndExcl.AddMilliseconds(-1), TimeSpan.Zero).ToUnixTimeMilliseconds()

        ' Cap the window at NOW. The current month's segment runs to the month end, so without
        ' this the expectation counts hours that have not happened yet and the month can never
        ' satisfy the coverage check — a redundant fetch every run, forever. (Observed: the
        ' repair fetch left July at 716/720, the four "missing" samples being 20:00–23:00 on a
        ' day that had not reached 20:00.) Nothing is lost: the future cannot be fetched.
        Dim nowMs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        If endMs > nowMs Then endMs = nowMs

        ' Coverage check (defect 2). Count what is already stored INSIDE the requested
        ' segment — an out-of-range straggler must not make a short month look complete.
        Dim existing As List(Of BacktestFundingSample) = LoadFundingFile(path)
        Dim haveInRange As Integer = 0
        For Each s In existing
            If s.TsMs >= segStartMs AndAlso s.TsMs <= endMs Then haveInRange += 1
        Next
        Dim expected As Integer = ExpectedFundingSamples(segStartMs, endMs)
        If haveInRange >= expected Then Return existing.Count

        ' Fetch one interval early (defect 1) and filter back to the segment below.
        Dim samples As List(Of BacktestFundingSample) =
            Await FetchFundingHistoryAsync(segStartMs - FundingIntervalMs, endMs)
        If samples Is Nothing Then
            Console.Error.WriteLine(String.Format(
                "[HistoricalStore] Funding fetch failed for {0:D4}-{1:D2} — keeping {2} stored sample(s)",
                year, month, existing.Count))
            Return existing.Count
        End If

        ' Merge: stored rows survive, fetched rows fill the holes. Dedup by timestamp,
        ' newest fetch wins on a collision.
        Dim map As New SortedDictionary(Of Long, Double)()
        For Each s In existing
            map(s.TsMs) = s.Rate
        Next
        Dim added As Integer = 0
        For Each s In samples
            If s.TsMs < segStartMs OrElse s.TsMs > endMs Then Continue For   ' the margin, discarded
            If Not map.ContainsKey(s.TsMs) Then added += 1
            map(s.TsMs) = s.Rate
        Next

        Using sw As New StreamWriter(path, append:=False)
            sw.WriteLine("Timestamp,Rate")
            For Each kv In map
                sw.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0},{1:F10}", kv.Key, kv.Value))
            Next
        End Using
        Console.WriteLine(String.Format(
            "[HistoricalStore] Funding {0:D4}-{1:D2}: {2} stored (+{3} new, expected {4} in range)",
            year, month, map.Count, added, expected))
        Return map.Count
    End Function

    ''' <summary>How many hourly samples SHOULD sit in [startMs, endMsIncl] — the number of
    ''' hour boundaries in the window. Pure; the coverage check above is the only caller.</summary>
    Public Shared Function ExpectedFundingSamples(startMs As Long, endMsIncl As Long) As Integer
        If endMsIncl < startMs Then Return 0
        ' First hour boundary at or after startMs.
        Dim firstHour As Long = ((startMs + FundingIntervalMs - 1) \ FundingIntervalMs) * FundingIntervalMs
        If firstHour > endMsIncl Then Return 0
        Return CInt((endMsIncl - firstHour) \ FundingIntervalMs) + 1
    End Function

    ''' <summary>Parse one funding month file. Empty list when absent/unreadable; never throws.
    ''' The single parse both LoadFundingRange and the coverage check route through.</summary>
    Public Shared Function LoadFundingFile(path As String) As List(Of BacktestFundingSample)
        Dim all As New List(Of BacktestFundingSample)()
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return all
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    Dim parts = line.Split(","c)
                    If parts.Length < 2 Then Continue Do
                    Dim ts As Long
                    Dim rate As Double
                    If Not Long.TryParse(parts(0), NumberStyles.Integer, CultureInfo.InvariantCulture, ts) Then Continue Do
                    If Not Double.TryParse(parts(1), NumberStyles.Float, CultureInfo.InvariantCulture, rate) Then Continue Do
                    all.Add(New BacktestFundingSample() With {.TsMs = ts, .Rate = rate})
                Loop
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("[HistoricalStore] LoadFundingFile failed: " & ex.Message)
        End Try
        Return all
    End Function

    Public Shared Async Function FetchFundingHistoryAsync(startMs As Long, endMs As Long) As Task(Of List(Of BacktestFundingSample))
        Dim url As String = "https://www.deribit.com/api/v2/public/get_funding_rate_history" &
                            "?instrument_name=" & InstrumentName &
                            "&start_timestamp=" & startMs &
                            "&end_timestamp=" & endMs
        For attempt As Integer = 1 To 2
            Dim needsRetry As Boolean = False
            Try
                Dim json = Await _http.GetStringAsync(url)
                Dim doc  = JsonDocument.Parse(json)
                Dim result = doc.RootElement.GetProperty("result")
                Dim list As New List(Of BacktestFundingSample)()
                For Each row In result.EnumerateArray()
                    Dim s As New BacktestFundingSample()
                    s.TsMs = row.GetProperty("timestamp").GetInt64()
                    ' Prefer interest_8h (the projected 8h rate; matches DeribitClient.GetFundingRateAsync's
                    ' funding_8h source). Fall back to interest_1h*8 if 8h absent.
                    Dim rateEl As JsonElement = Nothing
                    If row.TryGetProperty("interest_8h", rateEl) Then
                        s.Rate = rateEl.GetDouble()
                    ElseIf row.TryGetProperty("interest_1h", rateEl) Then
                        s.Rate = rateEl.GetDouble() * 8.0
                    Else
                        s.Rate = 0.0
                    End If
                    list.Add(s)
                Next
                Return list
            Catch ex As HttpRequestException
                If attempt < 2 AndAlso ex.StatusCode.HasValue AndAlso CInt(ex.StatusCode.Value) >= 500 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Funding HTTP failure: " & ex.Message)
                    Return Nothing
                End If
            Catch ex As TaskCanceledException
                If attempt < 2 Then
                    needsRetry = True
                Else
                    Console.Error.WriteLine("[HistoricalStore] Funding timeout")
                    Return Nothing
                End If
            Catch ex As Exception
                Console.Error.WriteLine("[HistoricalStore] Funding error: " & ex.Message)
                Return Nothing
            End Try
            If needsRetry Then Await Task.Delay(500)
        Next
        Return Nothing
    End Function

    Public Shared Function LoadFundingRange(warmupStartUtc As DateTime, toUtc As DateTime) As List(Of BacktestFundingSample)
        Dim all As New List(Of BacktestFundingSample)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            all.AddRange(LoadFundingFile(FundingFileFor(m.Year, m.Month)))
        Next
        all.Sort(Function(a, b) a.TsMs.CompareTo(b.TsMs))
        Return all
    End Function

    ' ── Top-level fetch orchestration ─────────────────────────────────────────────────

    ''' <summary>Fetch (fetch-once) all data for [fromUtc, toUtc] plus a warm-up prefix.
    ''' Warm-up is 3 hours: enough for 250×1m, 210×5m and 70×15m windows at the leftmost
    ''' bar-close (max window = 70×15 = 1050 min = 17.5 h; the trade window is 500 trades
    ''' — a few minutes at most on BTC-PERPETUAL). Actually 20 h to be safe.</summary>
    Public Shared Async Function BackfillAllAsync(fromUtc As DateTime, toUtc As DateTime) As Task
        Dim warmupStart As DateTime = fromUtc.AddHours(-20)
        EnsureStoreDir()

        For Each res In New Integer() {1, 3, 5, 15}
            For Each m In EnumerateMonths(warmupStart, toUtc)
                Dim n As Integer = Await BackfillCandleMonthAsync(res, m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
                Console.WriteLine(String.Format("[HistoricalStore] Candles {0}m {1:D4}-{2:D2}: {3} rows on disk",
                                                res, m.Year, m.Month, n))
                Await Task.Delay(PoliteDelayMs)
            Next
        Next

        For Each m In EnumerateMonths(warmupStart, toUtc)
            Await BackfillFundingMonthAsync(m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
            Await Task.Delay(PoliteDelayMs)
        Next

        For Each m In EnumerateMonths(warmupStart, toUtc)
            Await BackfillTradeMonthAsync(m.Year, m.Month, m.StartUtc, m.EndUtcExcl)
        Next
    End Function

End Class
