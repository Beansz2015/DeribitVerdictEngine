' analysis/DeribitOhlcFetcher.vb
' Bulk-fetches 1m OHLC bars from Deribit for a UTC time range.
' Returns a dictionary keyed by bar CloseTime (UTC, minute-aligned).
' Used by AnalysisRunner (offline report) and AutoTweakerCore.
'
' Thin wrapper over DeribitClient.GetCandlesAsync(range) — inherits the
' v18 ExecuteWithRetry resilience layer (retry-once on transient 5xx /
' timeout, return Nothing on hard failure). No duplicate URL or JSON
' parsing here — that lives in DeribitClient as the single source of
' the API contract.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Threading.Tasks

Public Class DeribitOhlcFetcher

    ' Bulk-fetch 1m OHLC bars for [startUtc, endUtc]. Returns Nothing on hard
    ' failure (Deribit maintenance, network exhaustion, parse error). Caller
    ' checks for Nothing and aborts gracefully.
    '
    ' Dictionary key = bar CloseTime, computed as openTime + 1 minute since
    ' Deribit returns bar open timestamps. Used to align with the engine's
    ' verdict timestamps (which are minute-end / candle-close aligned).
    Public Shared Async Function FetchOhlcRange(
            startUtc As DateTime,
            endUtc   As DateTime) As Task(Of Dictionary(Of DateTime, OhlcBar))

        Dim startMs As Long = New DateTimeOffset(startUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()
        Dim endMs   As Long = New DateTimeOffset(endUtc,   TimeSpan.Zero).ToUnixTimeMilliseconds()

        Dim candles As List(Of Candle) =
            Await DeribitClient.GetCandlesAsync("1", startMs, endMs)

        If candles Is Nothing Then
            Console.WriteLine("[DeribitOhlcFetcher] Fetch failed (DeribitClient returned Nothing after retry).")
            Return Nothing
        End If

        Dim map As New Dictionary(Of DateTime, OhlcBar)()
        For Each c In candles
            Dim openUtc   As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(c.Timestamp).UtcDateTime
            Dim closeTime As DateTime = openUtc.AddMinutes(1)
            map(closeTime) = New OhlcBar() With {
                .CloseTime = closeTime,
                .Open      = c.Open,
                .High      = c.High,
                .Low       = c.Low,
                .Close     = c.Close
            }
        Next
        Return map
    End Function

End Class
