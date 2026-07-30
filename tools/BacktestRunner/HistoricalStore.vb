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

    Public Shared Function TradeFileFor(year As Integer, month As Integer) As String
        Return Path.Combine(StoreDir, String.Format("trades_{0:D4}-{1:D2}.csv", year, month))
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
    ''' </summary>
    Public Shared Async Function BackfillTradeMonthAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime) As Task(Of Integer)
        EnsureStoreDir()
        Dim path As String = TradeFileFor(year, month)
        Dim resumeMs As Long = -1
        Dim isNewFile As Boolean = Not File.Exists(path)
        If Not isNewFile Then
            resumeMs = LastTradeTimestamp(path)
        End If

        Dim cursorMs As Long
        If resumeMs > 0 Then
            cursorMs = resumeMs + 1
        Else
            cursorMs = New DateTimeOffset(segStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        End If
        Dim endMs As Long = New DateTimeOffset(segEndExcl, TimeSpan.Zero).ToUnixTimeMilliseconds() - 1
        If cursorMs > endMs Then Return CountDataRows(path)

        Dim total As Integer = 0
        Dim page As Integer = 0
        Using sw As New StreamWriter(path, append:=Not isNewFile)
            If isNewFile Then sw.WriteLine("Timestamp,Price,Amount,Direction,Liquidation")

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

                For Each t In trades
                    sw.WriteLine(String.Format(CultureInfo.InvariantCulture,
                        "{0},{1:F2},{2:F2},{3},{4}",
                        t.Timestamp, t.Price, t.Amount, t.Direction, t.Liquidation))
                Next
                total += trades.Count

                Dim newestMs As Long = trades(trades.Count - 1).Timestamp
                If newestMs <= cursorMs Then
                    ' No forward progress — Deribit returned <=1000 trades all with the same ms.
                    ' Nudge past to avoid infinite loop.
                    cursorMs = newestMs + 1
                Else
                    cursorMs = newestMs + 1
                End If
                page += 1

                If trades.Count < TradesPerPage Then
                    ' Fewer than a full page ⇒ no more trades in the window.
                    Exit Do
                End If

                Await Task.Delay(PoliteDelayMs)
            Loop
        End Using

        Console.WriteLine(String.Format(
            "[HistoricalStore] Trades {0:D4}-{1:D2}: appended {2} rows across {3} page(s)",
            year, month, total, page))
        Return total
    End Function

    Private Shared Function LastTradeTimestamp(path As String) As Long
        Try
            Using sr As New StreamReader(path)
                sr.ReadLine()   ' header
                Dim prev As String = Nothing
                Dim line As String
                Do
                    line = sr.ReadLine()
                    If line Is Nothing Then Exit Do
                    prev = line
                Loop
                If prev Is Nothing Then Return -1
                Dim comma As Integer = prev.IndexOf(","c)
                If comma <= 0 Then Return -1
                Dim n As Long
                If Long.TryParse(prev.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, n) Then Return n
            End Using
        Catch
        End Try
        Return -1
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
    ''' ascending (the F1 contract). Duplicates deduped by (ts, price, amount, direction).</summary>
    Public Shared Function LoadTradeRange(warmupStartUtc As DateTime, toUtc As DateTime) As List(Of TradeRecord)
        Dim all As New List(Of TradeRecord)()
        Dim seen As New HashSet(Of String)()
        For Each m In EnumerateMonths(warmupStartUtc, toUtc)
            Dim path As String = TradeFileFor(m.Year, m.Month)
            If Not File.Exists(path) Then Continue For
            Try
                Using sr As New StreamReader(path)
                    sr.ReadLine()   ' header
                    Dim line As String
                    Do
                        line = sr.ReadLine()
                        If line Is Nothing Then Exit Do
                        Dim parts = line.Split(","c)
                        If parts.Length < 5 Then Continue Do
                        Dim key = line
                        If Not seen.Add(key) Then Continue Do
                        Dim rec As New TradeRecord()
                        rec.Timestamp   = Long.Parse(parts(0), CultureInfo.InvariantCulture)
                        rec.Price       = Double.Parse(parts(1), CultureInfo.InvariantCulture)
                        rec.Amount      = Double.Parse(parts(2), CultureInfo.InvariantCulture)
                        rec.Direction   = parts(3)
                        rec.Liquidation = parts(4)
                        all.Add(rec)
                    Loop
                End Using
            Catch ex As Exception
                Console.Error.WriteLine("[HistoricalStore] LoadTradeRange failed: " & ex.Message)
            End Try
        Next
        all.Sort(Function(a, b) a.Timestamp.CompareTo(b.Timestamp))
        Return all
    End Function

    ' ── Funding backfill ──────────────────────────────────────────────────────────────
    ' The sample type is the TOP-LEVEL BacktestFundingSample (declared in ReplayLoop.vb) —
    ' keeps HistoricalStore's HttpClient off the harness's link surface.

    ''' <summary>Fetch and store one calendar-month of funding rate history. Fetch-once:
    ''' existing file left alone. The funding endpoint returns the full range in one call
    ''' (~720 hourly samples/month) so pagination is unnecessary.</summary>
    Public Shared Async Function BackfillFundingMonthAsync(
            year As Integer, month As Integer,
            segStart As DateTime, segEndExcl As DateTime) As Task(Of Integer)
        EnsureStoreDir()
        Dim path As String = FundingFileFor(year, month)
        If File.Exists(path) Then Return CountDataRows(path)

        Dim startMs As Long = New DateTimeOffset(segStart, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs   As Long = New DateTimeOffset(segEndExcl.AddMilliseconds(-1), TimeSpan.Zero).ToUnixTimeMilliseconds()

        Dim samples As List(Of BacktestFundingSample) = Await FetchFundingHistoryAsync(startMs, endMs)
        If samples Is Nothing Then
            Console.Error.WriteLine(String.Format(
                "[HistoricalStore] Funding fetch failed for {0:D4}-{1:D2}", year, month))
            Return 0
        End If

        samples.Sort(Function(a, b) a.TsMs.CompareTo(b.TsMs))
        Using sw As New StreamWriter(path, append:=False)
            sw.WriteLine("Timestamp,Rate")
            For Each s In samples
                sw.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0},{1:F10}", s.TsMs, s.Rate))
            Next
        End Using
        Return samples.Count
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
            Dim path As String = FundingFileFor(m.Year, m.Month)
            If Not File.Exists(path) Then Continue For
            Try
                Using sr As New StreamReader(path)
                    sr.ReadLine()   ' header
                    Dim line As String
                    Do
                        line = sr.ReadLine()
                        If line Is Nothing Then Exit Do
                        Dim parts = line.Split(","c)
                        If parts.Length < 2 Then Continue Do
                        Dim s As New BacktestFundingSample()
                        s.TsMs = Long.Parse(parts(0), CultureInfo.InvariantCulture)
                        s.Rate = Double.Parse(parts(1), CultureInfo.InvariantCulture)
                        all.Add(s)
                    Loop
                End Using
            Catch ex As Exception
                Console.Error.WriteLine("[HistoricalStore] LoadFundingRange failed: " & ex.Message)
            End Try
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
