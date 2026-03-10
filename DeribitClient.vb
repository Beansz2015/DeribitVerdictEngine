' DeribitClient.vb
' Handles all REST calls to Deribit public API.
' No authentication required — all endpoints are public.

Imports System.Net.Http
Imports System.Text.Json

Public Class DeribitClient
    Private Shared ReadOnly _http As New HttpClient()
    Private Const BaseUrl As String = "https://www.deribit.com/api/v2"

    Shared Sub New()
        _http.DefaultRequestHeaders.Add("User-Agent", "DeribitScalpVerdictApp/1.0")
        _http.Timeout = TimeSpan.FromSeconds(10)
    End Sub

    ' ── Candle data ──────────────────────────────────────────────────────────
    ' resolution: "1" = 1-minute, "5" = 5-minute
    ' count: number of candles to fetch (spec max = 200 for 5m EMA200)
    Public Shared Async Function GetCandlesAsync(
            resolution As String,
            count As Integer) As Task(Of List(Of Candle))

        ' Deribit requires start_timestamp and end_timestamp in ms.
        ' We fetch [now - count*resolution minutes, now].
        Dim resMin As Integer = Integer.Parse(resolution)
        Dim endTs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        Dim startTs As Long = endTs - CLng(count) * resMin * 60 * 1000L

        Dim url As String = $"{BaseUrl}/public/get_tradingview_chart_data" &
                            $"?instrument_name=BTC-PERPETUAL" &
                            $"&resolution={resolution}" &
                            $"&start_timestamp={startTs}" &
                            $"&end_timestamp={endTs}"

        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")

        ' Deribit returns parallel arrays: ticks, open, high, low, close, volume, status
        Dim ticks As JsonElement = result.GetProperty("ticks")
        Dim opens As JsonElement = result.GetProperty("open")
        Dim highs As JsonElement = result.GetProperty("high")
        Dim lows As JsonElement = result.GetProperty("low")
        Dim closes As JsonElement = result.GetProperty("close")
        Dim volumes As JsonElement = result.GetProperty("volume")

        Dim candles As New List(Of Candle)
        For i As Integer = 0 To ticks.GetArrayLength() - 1
            candles.Add(New Candle With {
                .Timestamp = ticks(i).GetInt64(),
                .Open = opens(i).GetDouble(),
                .High = highs(i).GetDouble(),
                .Low = lows(i).GetDouble(),
                .Close = closes(i).GetDouble(),
                .Volume = volumes(i).GetDouble()
            })
        Next
        Return candles
    End Function

    ' ── Funding rate ─────────────────────────────────────────────────────────
    ' Returns current 8-hour funding rate as a decimal (e.g. 0.0001 = 0.01%)
    Public Shared Async Function GetFundingRateAsync() As Task(Of Double)
        Dim url As String = $"{BaseUrl}/public/get_funding_rate_value" &
                            $"?instrument_name=BTC-PERPETUAL" &
                            $"&start_timestamp=0&end_timestamp=0"

        ' Simpler: use ticker which includes current_funding
        Dim tickerUrl As String = $"{BaseUrl}/public/ticker?instrument_name=BTC-PERPETUAL"
        Dim json As String = Await _http.GetStringAsync(tickerUrl)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")
        ' current_funding is per-8h rate expressed as decimal
        If result.TryGetProperty("current_funding", Nothing) Then
            Return result.GetProperty("current_funding").GetDouble()
        End If
        Return 0.0
    End Function

    ' ── Open Interest snapshot ────────────────────────────────────────────────
    ' Returns (open_interest_usd, mark_price)
    Public Shared Async Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double))
        Dim url As String = $"{BaseUrl}/public/get_book_summary_by_instrument" &
                            $"?instrument_name=BTC-PERPETUAL"
        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")
        Dim item As JsonElement = result(0)
        Dim oi As Double = item.GetProperty("open_interest").GetDouble()
        Dim mp As Double = item.GetProperty("mark_price").GetDouble()
        Return (oi, mp)
    End Function

    ' ── L2 Order book snapshot (for OFI approximation) ───────────────────────
    ' Returns top 5 bids and asks as (price, size) tuples
    Public Shared Async Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot)
        Dim url As String = $"{BaseUrl}/public/get_order_book" &
                            $"?instrument_name=BTC-PERPETUAL&depth={depth}"
        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")

        Dim snap As New OrderBookSnapshot()
        Dim bids As JsonElement = result.GetProperty("bids")
        Dim asks As JsonElement = result.GetProperty("asks")

        For i As Integer = 0 To Math.Min(depth, bids.GetArrayLength()) - 1
            snap.Bids.Add((bids(i)(0).GetDouble(), bids(i)(1).GetDouble()))
        Next
        For i As Integer = 0 To Math.Min(depth, asks.GetArrayLength()) - 1
            snap.Asks.Add((asks(i)(0).GetDouble(), asks(i)(1).GetDouble()))
        Next
        Return snap
    End Function

    ' ── Recent trades (for liquidation detection) ────────────────────────────
    Public Shared Async Function GetRecentTradesAsync(count As Integer) As Task(Of List(Of TradeRecord))
        Dim url As String = $"{BaseUrl}/public/get_last_trades_by_instrument" &
                            $"?instrument_name=BTC-PERPETUAL&count={count}&sorting=desc"
        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim trades As JsonElement = doc.RootElement.GetProperty("result").GetProperty("trades")

        Dim list As New List(Of TradeRecord)
        For Each t As JsonElement In trades.EnumerateArray()
            Dim rec As New TradeRecord With {
                .Price = t.GetProperty("price").GetDouble(),
                .Amount = t.GetProperty("amount").GetDouble(),
                .Direction = t.GetProperty("direction").GetString(),
                .Timestamp = t.GetProperty("timestamp").GetInt64()
            }
            ' liquidation field: "none", "M" (maker liq), "T" (taker liq)
            If t.TryGetProperty("liquidation", Nothing) Then
                rec.Liquidation = t.GetProperty("liquidation").GetString()
            Else
                rec.Liquidation = "none"
            End If
            list.Add(rec)
        Next
        Return list
    End Function
End Class

' ── Data transfer objects ─────────────────────────────────────────────────────

Public Class Candle
    Public Property Timestamp As Long
    Public Property [Open] As Double
    Public Property High As Double
    Public Property Low As Double
    Public Property Close As Double
    Public Property Volume As Double
End Class

Public Class OrderBookSnapshot
    Public Property Bids As New List(Of (Price As Double, Size As Double))
    Public Property Asks As New List(Of (Price As Double, Size As Double))
End Class

Public Class TradeRecord
    Public Property Price As Double
    Public Property Amount As Double
    Public Property Direction As String
    Public Property Liquidation As String
    Public Property Timestamp As Long
End Class
