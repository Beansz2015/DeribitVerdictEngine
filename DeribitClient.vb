' DeribitClient.vb
' Handles all REST calls to Deribit public API.
' No authentication required -- all endpoints are public.

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
    ' count: number of candles to fetch
    ' Deribit get_tradingview_chart_data returns:
    '   volume = BTC (base currency) volume per candle
    '   cost   = USD (quote currency) volume per candle
    Public Shared Async Function GetCandlesAsync(
            resolution As String,
            count As Integer) As Task(Of List(Of Candle))

        Dim resMin As Integer = Integer.Parse(resolution)
        Dim endTs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        Dim startTs As Long = endTs - CLng(count) * resMin * 60 * 1000L

        Dim url As String = BaseUrl & "/public/get_tradingview_chart_data" &
                            "?instrument_name=BTC-PERPETUAL" &
                            "&resolution=" & resolution &
                            "&start_timestamp=" & startTs &
                            "&end_timestamp=" & endTs

        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")

        ' Parallel arrays returned by Deribit
        Dim ticks As JsonElement = result.GetProperty("ticks")
        Dim opens As JsonElement = result.GetProperty("open")
        Dim highs As JsonElement = result.GetProperty("high")
        Dim lows As JsonElement = result.GetProperty("low")
        Dim closes As JsonElement = result.GetProperty("close")
        Dim volumes As JsonElement = result.GetProperty("volume")  ' BTC volume

        ' cost = USD volume; may not always be present on all resolutions
        Dim hasCost As Boolean = result.TryGetProperty("cost", Nothing)
        Dim costs As JsonElement = Nothing
        If hasCost Then
            costs = result.GetProperty("cost")
        End If

        Dim candles As New List(Of Candle)
        For i As Integer = 0 To ticks.GetArrayLength() - 1
            Dim c As New Candle()
            c.Timestamp = ticks(i).GetInt64()
            c.Open = opens(i).GetDouble()
            c.High = highs(i).GetDouble()
            c.Low = lows(i).GetDouble()
            c.Close = closes(i).GetDouble()
            c.Volume = volumes(i).GetDouble()         ' BTC -- used for scoring
            If hasCost Then
                c.VolumUSD = costs(i).GetDouble()     ' USD -- used for display
            Else
                c.VolumUSD = c.Volume * c.Close       ' fallback: approximate
            End If
            candles.Add(c)
        Next
        Return candles
    End Function

    ' ── Funding rate ─────────────────────────────────────────────────────────
    ' Returns current 8-hour funding rate as a decimal (e.g. 0.0001 = 0.01%)
    Public Shared Async Function GetFundingRateAsync() As Task(Of Double)
        Dim tickerUrl As String = BaseUrl & "/public/ticker?instrument_name=BTC-PERPETUAL"
        Dim json As String = Await _http.GetStringAsync(tickerUrl)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")
        If result.TryGetProperty("current_funding", Nothing) Then
            Return result.GetProperty("current_funding").GetDouble()
        End If
        Return 0.0
    End Function

    ' ── Open Interest snapshot ────────────────────────────────────────────────
    ' Returns (open_interest, mark_price)
    Public Shared Async Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double))
        Dim url As String = BaseUrl & "/public/get_book_summary_by_instrument" &
                            "?instrument_name=BTC-PERPETUAL"
        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim result As JsonElement = doc.RootElement.GetProperty("result")
        Dim item As JsonElement = result(0)
        Dim oi As Double = item.GetProperty("open_interest").GetDouble()
        Dim mp As Double = item.GetProperty("mark_price").GetDouble()
        Return (oi, mp)
    End Function

    ' ── L2 Order book snapshot (for OFI) ─────────────────────────────────────
    Public Shared Async Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot)
        Dim url As String = BaseUrl & "/public/get_order_book" &
                            "?instrument_name=BTC-PERPETUAL&depth=" & depth
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
        Dim url As String = BaseUrl & "/public/get_last_trades_by_instrument" &
                            "?instrument_name=BTC-PERPETUAL&count=" & count & "&sorting=desc"
        Dim json As String = Await _http.GetStringAsync(url)
        Dim doc As JsonDocument = JsonDocument.Parse(json)
        Dim trades As JsonElement = doc.RootElement.GetProperty("result").GetProperty("trades")

        Dim list As New List(Of TradeRecord)
        For Each t As JsonElement In trades.EnumerateArray()
            Dim rec As New TradeRecord()
            rec.Price = t.GetProperty("price").GetDouble()
            rec.Amount = t.GetProperty("amount").GetDouble()
            rec.Direction = t.GetProperty("direction").GetString()
            rec.Timestamp = t.GetProperty("timestamp").GetInt64()
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
    Public Property Volume As Double      ' BTC volume -- used for all indicator scoring
    Public Property VolumUSD As Double    ' USD volume -- display only
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
