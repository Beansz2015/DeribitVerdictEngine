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

    ' Deribit's public/get_tradingview_chart_data caps responses at ~5000 bars
    ' per call. Single-call fetches for ranges larger than this silently return
    ' only the latest 5000 bars, leaving the oldest portion of the requested
    ' range with no data. Use 5000 as the chunk size; loop to cover the full
    ' requested range.
    Private Const CHUNK_MINUTES As Integer = 5000

    ' Safety cap on calls per FetchOhlcRange invocation. 20 chunks × 5000 minutes
    ' = ~70 days, well beyond any realistic offline-analysis CSV span. Prevents
    ' runaway loops if startUtc/endUtc are accidentally inverted or wildly far
    ' apart.
    Private Const MAX_CHUNKS As Integer = 20

    ' Bulk-fetch 1m OHLC bars for [startUtc, endUtc]. Returns Nothing on hard
    ' failure (Deribit maintenance, network exhaustion, parse error in any
    ' chunk). Caller checks for Nothing and aborts gracefully.
    '
    ' Chunks the range into ≤ CHUNK_MINUTES segments to avoid Deribit's per-call
    ' bar cap. Mid-range chunks that fail abort the whole fetch (caller treats
    ' as Nothing) to avoid silently partial OHLC maps that produce misleading
    ' matrix numbers downstream.
    '
    ' Dictionary key = bar CloseTime, computed as openTime + 1 minute since
    ' Deribit returns bar open timestamps. Used to align with the engine's
    ' verdict timestamps (which are minute-end / candle-close aligned).
    '
    ' Bug fixed 2026-05-18: previously made a single GetCandlesAsync call which
    ' Deribit truncates to the latest ~5000 bars. CSV spans >5000 minutes (>~3.5
    ' days at 1m resolution) lost the oldest portion of their OHLC, silently
    ' starving the failure-rate matrix of any verdicts that happened in the head
    ' of the CSV. Surfaced as "STRONG_SHORT n=0 in every cell" in the 2026-05-17
    ' Analysis Report despite 47 STRONG SHORT verdicts in CSV (P11).
    Public Shared Async Function FetchOhlcRange(
            startUtc As DateTime,
            endUtc   As DateTime) As Task(Of Dictionary(Of DateTime, OhlcBar))

        If endUtc <= startUtc Then Return New Dictionary(Of DateTime, OhlcBar)()

        Dim map As New Dictionary(Of DateTime, OhlcBar)()
        Dim cursor As DateTime = startUtc
        Dim chunkIdx As Integer = 0
        Dim totalBars As Integer = 0

        While cursor < endUtc
            If chunkIdx >= MAX_CHUNKS Then
                Console.WriteLine(String.Format(
                    "[DeribitOhlcFetcher] Chunk cap hit ({0}) — aborting fetch. Range too large for offline analysis.",
                    MAX_CHUNKS))
                Return Nothing
            End If

            Dim chunkEnd As DateTime = cursor.AddMinutes(CHUNK_MINUTES - 1)
            If chunkEnd > endUtc Then chunkEnd = endUtc

            Dim startMs As Long = New DateTimeOffset(cursor,   TimeSpan.Zero).ToUnixTimeMilliseconds()
            Dim endMs   As Long = New DateTimeOffset(chunkEnd, TimeSpan.Zero).ToUnixTimeMilliseconds()

            Dim candles As List(Of Candle) =
                Await DeribitClient.GetCandlesAsync("1", startMs, endMs)
            If candles Is Nothing Then
                Console.WriteLine(String.Format(
                    "[DeribitOhlcFetcher] Chunk {0} fetch failed ({1:yyyy-MM-ddTHH:mmZ} → {2:yyyy-MM-ddTHH:mmZ}). Aborting.",
                    chunkIdx + 1, cursor, chunkEnd))
                Return Nothing
            End If

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
            totalBars += candles.Count
            chunkIdx += 1

            cursor = chunkEnd.AddMinutes(1)
        End While

        Console.WriteLine(String.Format(
            "[DeribitOhlcFetcher] Fetched {0} bars across {1} chunk(s) for {2:yyyy-MM-ddTHH:mmZ} → {3:yyyy-MM-ddTHH:mmZ}.",
            totalBars, chunkIdx, startUtc, endUtc))
        Return map
    End Function

End Class
