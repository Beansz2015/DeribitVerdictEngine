' analysis/DeribitOhlcFetcher.vb
' Bulk-fetches 1m OHLC bars from Deribit for a UTC time range.
' Returns a dictionary keyed by bar CloseTime (UTC, minute-aligned).
' Used by AnalysisRunner (offline report) and AutoTweakerCore.
'
' Self-contained: owns its HttpClient, does not depend on DeribitClient or
' SettingsLoader. Single attempt — no retry (the caller decides how to handle
' Nothing on failure). Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text.Json
Imports System.Threading.Tasks

Public Class DeribitOhlcFetcher

    Private Shared ReadOnly _http As New HttpClient() With {.Timeout = TimeSpan.FromSeconds(30)}
    Private Const BaseUrl As String = "https://www.deribit.com/api/v2"

    ' Bulk-fetch 1m OHLC bars for [startUtc, endUtc]. Returns Nothing on any
    ' network or parse failure. Dictionary key = bar CloseTime (openTime + 1 min).
    ' Caller checks for Nothing and aborts gracefully.
    Public Shared Async Function FetchOhlcRange(
            startUtc As DateTime,
            endUtc   As DateTime) As Task(Of Dictionary(Of DateTime, OhlcBar))

        Try
            Dim startMs As Long = New DateTimeOffset(startUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()
            Dim endMs   As Long = New DateTimeOffset(endUtc,   TimeSpan.Zero).ToUnixTimeMilliseconds()

            Dim url As String = BaseUrl & "/public/get_tradingview_chart_data" &
                                "?instrument_name=BTC-PERPETUAL&resolution=1" &
                                "&start_timestamp=" & startMs &
                                "&end_timestamp=" & endMs

            Dim json As String = Await _http.GetStringAsync(url)
            Dim doc  As JsonDocument = JsonDocument.Parse(json)
            Dim result As JsonElement = doc.RootElement.GetProperty("result")

            Dim ticks  As JsonElement = result.GetProperty("ticks")
            Dim opens  As JsonElement = result.GetProperty("open")
            Dim highs  As JsonElement = result.GetProperty("high")
            Dim lows   As JsonElement = result.GetProperty("low")
            Dim closes As JsonElement = result.GetProperty("close")

            Dim map As New Dictionary(Of DateTime, OhlcBar)()
            For i As Integer = 0 To ticks.GetArrayLength() - 1
                ' ticks[i] is bar-open epoch ms. CloseTime = openTime + 1 minute.
                Dim openUtc   As DateTime = DateTimeOffset.FromUnixTimeMilliseconds(
                                                ticks(i).GetInt64()).UtcDateTime
                Dim closeTime As DateTime = openUtc.AddMinutes(1)
                map(closeTime) = New OhlcBar() With {
                    .CloseTime = closeTime,
                    .Open      = opens(i).GetDouble(),
                    .High      = highs(i).GetDouble(),
                    .Low       = lows(i).GetDouble(),
                    .Close     = closes(i).GetDouble()
                }
            Next

            Return map

        Catch ex As Exception
            Console.WriteLine("[DeribitOhlcFetcher] Fetch failed: " & ex.Message)
            Return Nothing
        End Try

    End Function

End Class
