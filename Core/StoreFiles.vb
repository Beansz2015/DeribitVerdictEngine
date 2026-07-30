' Core/StoreFiles.vb
' [2026-07-31] Network-free file layer for the candle and funding halves of the backtest
' store, extracted so the store's write invariant can actually be tested.
'
' Why it exists: **this code destroyed a month of data and had no test covering it.**
' `BackfillCandleMonthAsync` wrote a whole MONTH file from a partial SEGMENT fetch with
' `append:=False`, and a resolution-blind coverage heuristic made it refetch every non-1m
' month on every run. Together they replaced June 2026 at 3m/5m/15m with 20 hours.
'
' The obstacle to testing was never the link surface — `verify/ordercheck` has linked
' `HistoricalStore.vb` since the A43 family, HttpClient and all. The obstacle is that the
' damage happened inside an `Async` function that calls `DeribitClient.GetCandlesAsync`, so
' no fixture could reach the write without a live fetch or dependency injection. But the two
' decisions that broke are pure functions of (what is stored, what was fetched) — the merge
' and the coverage count — so lifting them out makes the invariant directly exercisable.
' That is the whole point of this file.
'
' (The `Core/TradeStoreWriter.vb` split at v64 is a sibling but not a precedent: that one
' genuinely was about keeping an HttpClient off the app's feed path.)
'
' THE INVARIANT this file exists to hold:
'
'     Stored rows ALWAYS survive a write. The result is the UNION of stored and fetched.
'     A partial, truncated or failed fetch can therefore never destroy anything — the
'     worst outcome is that it adds nothing.
'
' Everything else here — the coverage-by-count check — is an optimisation on top of that
' invariant, not a substitute for it. If the two ever conflict, the invariant wins.
'
' Host-agnostic: no WinForms, no HttpClient, no settings. Fixtures A51a–e.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO

Public NotInheritable Class StoreFiles

    Public Const CandleHeader  As String = "Timestamp,Open,High,Low,Close,Volume,Cost"
    Public Const FundingHeader As String = "Timestamp,Rate"

    Private Sub New()
    End Sub

    ' ── Grid arithmetic ───────────────────────────────────────────────────────────────

    ''' <summary>
    ''' How many grid points sit in [startMs, endMsIncl] at the given interval — the
    ''' deterministic expectation the coverage checks compare a stored count against.
    '''
    ''' This replaced the `MonthFileCovers` heuristic, which compared the file's last
    ''' timestamp against a FIXED 2-minute tolerance. A month's last bar is 23:59 at 1m but
    ''' 23:57 / 23:55 / 23:45 at 3m / 5m / 15m, so that check could only ever pass at 1m —
    ''' every other resolution refetched unconditionally, on every run. Counting against the
    ''' grid is resolution-aware by construction.
    ''' </summary>
    Public Shared Function ExpectedGridPoints(startMs As Long, endMsIncl As Long, intervalMs As Long) As Integer
        If intervalMs <= 0 OrElse endMsIncl < startMs Then Return 0
        Dim firstPoint As Long = ((startMs + intervalMs - 1) \ intervalMs) * intervalMs
        If firstPoint > endMsIncl Then Return 0
        Return CInt((endMsIncl - firstPoint) \ intervalMs) + 1
    End Function

    ''' <summary>Count candles whose timestamp falls in [startMs, endMsIncl].</summary>
    Public Shared Function CountCandlesInRange(rows As List(Of Candle), startMs As Long, endMsIncl As Long) As Integer
        Dim n As Integer = 0
        If rows Is Nothing Then Return 0
        For Each c In rows
            If c.Timestamp >= startMs AndAlso c.Timestamp <= endMsIncl Then n += 1
        Next
        Return n
    End Function

    ''' <summary>Count funding samples whose timestamp falls in [startMs, endMsIncl].</summary>
    Public Shared Function CountFundingInRange(rows As List(Of BacktestFundingSample), startMs As Long, endMsIncl As Long) As Integer
        Dim n As Integer = 0
        If rows Is Nothing Then Return 0
        For Each s In rows
            If s.TsMs >= startMs AndAlso s.TsMs <= endMsIncl Then n += 1
        Next
        Return n
    End Function

    ' ── Candle file layer ─────────────────────────────────────────────────────────────

    ''' <summary>Parse one candle file. Empty list when absent/unreadable; never throws.</summary>
    Public Shared Function LoadCandleFile(path As String) As List(Of Candle)
        Dim result As New List(Of Candle)
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return result
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
            Console.Error.WriteLine("[StoreFiles] LoadCandleFile failed: " & ex.Message)
        End Try
        Return result
    End Function

    ''' <summary>
    ''' Write the UNION of what is stored and what was fetched, timestamp-ordered, fetched
    ''' winning on collision. **This is the function whose absence cost a month of data.**
    ''' Returns the row count written; 0 and the file untouched on any I/O failure.
    ''' </summary>
    Public Shared Function MergeAndWriteCandles(path As String,
                                                 existing As List(Of Candle),
                                                 fetched As IEnumerable(Of Candle)) As Integer
        Dim map As New SortedDictionary(Of Long, Candle)()
        If existing IsNot Nothing Then
            For Each c In existing
                map(c.Timestamp) = c
            Next
        End If
        If fetched IsNot Nothing Then
            For Each c In fetched
                map(c.Timestamp) = c
            Next
        End If
        If map.Count = 0 Then Return 0
        Try
            Dim dir As String = System.IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(CandleHeader)
                For Each c In map.Values
                    sw.WriteLine(String.Format(CultureInfo.InvariantCulture,
                        "{0},{1:F2},{2:F2},{3:F2},{4:F2},{5:F6},{6:F2}",
                        c.Timestamp, c.Open, c.High, c.Low, c.Close, c.Volume, c.VolumeUSD))
                Next
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("[StoreFiles] MergeAndWriteCandles failed for '" & path & "': " & ex.Message)
            Return 0
        End Try
        Return map.Count
    End Function

    ' ── Funding file layer ────────────────────────────────────────────────────────────

    ''' <summary>Parse one funding month file. Empty list when absent/unreadable; never throws.</summary>
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
            Console.Error.WriteLine("[StoreFiles] LoadFundingFile failed: " & ex.Message)
        End Try
        Return all
    End Function

    ''' <summary>
    ''' Same union invariant for funding, with fetched samples clipped to
    ''' [keepFromMs, keepToMs]. The clip exists because the funding fetch deliberately
    ''' reaches one interval EARLIER than the segment — `start_timestamp` is exclusive on
    ''' Deribit (verified live: a request from exactly T returns T+1h first, from T−1ms
    ''' returns T), which silently dropped every month's 00:00 sample. Stored rows are never
    ''' clipped; only the deliberate over-reach is discarded.
    ''' </summary>
    Public Shared Function MergeAndWriteFunding(path As String,
                                                 existing As List(Of BacktestFundingSample),
                                                 fetched As IEnumerable(Of BacktestFundingSample),
                                                 keepFromMs As Long, keepToMs As Long) As Integer
        Dim map As New SortedDictionary(Of Long, Double)()
        If existing IsNot Nothing Then
            For Each s In existing
                map(s.TsMs) = s.Rate
            Next
        End If
        If fetched IsNot Nothing Then
            For Each s In fetched
                If s.TsMs < keepFromMs OrElse s.TsMs > keepToMs Then Continue For
                map(s.TsMs) = s.Rate
            Next
        End If
        If map.Count = 0 Then Return 0
        Try
            Dim dir As String = System.IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(FundingHeader)
                For Each kv In map
                    sw.WriteLine(String.Format(CultureInfo.InvariantCulture, "{0},{1:F10}", kv.Key, kv.Value))
                Next
            End Using
        Catch ex As Exception
            Console.Error.WriteLine("[StoreFiles] MergeAndWriteFunding failed for '" & path & "': " & ex.Message)
            Return 0
        End Try
        Return map.Count
    End Function

End Class
