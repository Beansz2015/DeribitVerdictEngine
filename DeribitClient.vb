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
        _http.Timeout = TimeSpan.FromSeconds(SettingsLoader.Current.Network.RequestTimeoutSeconds)
    End Sub

    ' ── Retry helper ─────────────────────────────────────────────────────────────────────────
    ' Executes an async HTTP fetch with bounded retry on transient failures.
    ' Returns the parsed result or Nothing if all retries exhausted.
    ' Transient: HTTP 5xx, TaskCanceledException (timeout), HttpRequestException without status.
    ' Hard failures (no retry): HTTP 4xx, JSON parse errors, anything else.
    '
    ' Note: Await cannot be used inside a Catch block in VB.NET. The retry delay is
    ' signalled via needsDelay and awaited after the Try/Catch structure.
    Private Shared Async Function ExecuteWithRetry(Of T)(
            fetcher As Func(Of Task(Of T)),
            callerName As String) As Task(Of T)

        Dim cfg = SettingsLoader.Current.Network
        Dim attempts As Integer = 1 + Math.Max(0, cfg.RetryCount)

        For i As Integer = 1 To attempts
            Dim needsDelay As Boolean = False

            Try
                Return Await fetcher()
            Catch ex As HttpRequestException
                ' 4xx are hard failures -- don't retry. Caller likely has a bug.
                If ex.StatusCode.HasValue AndAlso
                   CInt(ex.StatusCode.Value) >= 400 AndAlso
                   CInt(ex.StatusCode.Value) < 500 Then
                    Console.WriteLine(String.Format("[{0}] Hard HTTP failure: {1}", callerName, ex.Message))
                ElseIf i < attempts Then
                    ' 5xx / network -- transient, retry if we have one left
                    Console.WriteLine(String.Format("[{0}] Transient failure (attempt {1}/{2}): {3}",
                                                    callerName, i, attempts, ex.Message))
                    needsDelay = True
                Else
                    Console.WriteLine(String.Format("[{0}] Retry exhausted: {1}", callerName, ex.Message))
                End If
            Catch ex As TaskCanceledException
                ' Treat timeout same as 5xx -- retry once
                If i < attempts Then
                    Console.WriteLine(String.Format("[{0}] Timeout (attempt {1}/{2})",
                                                    callerName, i, attempts))
                    needsDelay = True
                Else
                    Console.WriteLine(String.Format("[{0}] Timeout retry exhausted", callerName))
                End If
            Catch ex As Exception
                ' JSON parse, etc -- hard failure, no retry
                Console.WriteLine(String.Format("[{0}] Hard failure: {1}", callerName, ex.Message))
            End Try

            If needsDelay Then
                Await Task.Delay(cfg.RetryBackoffMs)
            Else
                Return Nothing
            End If
        Next

        Return Nothing
    End Function

    ' ── Candle data ───────────────────────────────────────────────────────────────────────────
    ' resolution: "1" = 1-minute, "5" = 5-minute, "15" = 15-minute
    ' count: number of candles to fetch
    ' Deribit get_tradingview_chart_data returns:
    '   volume = BTC (base currency) volume per candle
    '   cost   = USD (quote currency) volume per candle
    Public Shared Async Function GetCandlesAsync(
            resolution As String,
            count As Integer) As Task(Of List(Of Candle))

        Return Await ExecuteWithRetry(Of List(Of Candle))(
            Async Function() As Task(Of List(Of Candle))
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
                Dim costs As JsonElement = Nothing
                Dim hasCost As Boolean = result.TryGetProperty("cost", costs)

                Dim candles As New List(Of Candle)
                For i As Integer = 0 To ticks.GetArrayLength() - 1
                    Dim c As New Candle()
                    c.Timestamp = ticks(i).GetInt64()
                    c.Open = opens(i).GetDouble()
                    c.High = highs(i).GetDouble()
                    c.Low = lows(i).GetDouble()
                    c.Close = closes(i).GetDouble()
                    c.Volume = volumes(i).GetDouble()          ' BTC -- used for scoring
                    If hasCost Then
                        c.VolumeUSD = costs(i).GetDouble()     ' USD -- used for display
                    Else
                        c.VolumeUSD = c.Volume * c.Close       ' fallback: approximate
                    End If
                    candles.Add(c)
                Next
                Return candles
            End Function,
            "GetCandlesAsync")
    End Function

    ' ── Funding rate ───────────────────────────────────────────────────────────────────────
    ' Returns the projected 8-hour funding rate from ticker.funding_8h.
    ' e.g. 0.00001 = 0.001%/8h (typical BTC-PERPETUAL range: +/-0.01% to +/-0.1%)
    '
    ' NOTE: Do NOT use current_funding here. current_funding is the intraperiod
    ' 1-hour accrual that resets to ~0 after each 8h settlement and accumulates
    ' toward the next settlement. Its value is time-of-period dependent and will
    ' read near-zero for most of each cycle, making Step 3 funding modifier inert.
    ' funding_8h is the time-invariant projected settlement rate -- directly
    ' comparable across all run times regardless of where we are in the 8h cycle.
    Public Shared Async Function GetFundingRateAsync() As Task(Of Double?)
        Return Await ExecuteWithRetry(Of Double?)(
            Async Function() As Task(Of Double?)
                Dim tickerUrl As String = BaseUrl & "/public/ticker?instrument_name=BTC-PERPETUAL"
                Dim json As String = Await _http.GetStringAsync(tickerUrl)
                Dim doc As JsonDocument = JsonDocument.Parse(json)
                Dim result As JsonElement = doc.RootElement.GetProperty("result")
                Dim fundingEl As JsonElement = Nothing
                If result.TryGetProperty("funding_8h", fundingEl) Then
                    Return CType(fundingEl.GetDouble(), Double?)
                End If
                Return CType(0.0, Double?)
            End Function,
            "GetFundingRateAsync")
    End Function

    ' ── Open Interest snapshot ─────────────────────────────────────────────────────────────
    ' Returns (open_interest, mark_price)
    Public Shared Async Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double)?)
        Return Await ExecuteWithRetry(Of (OI As Double, MarkPrice As Double)?)(
            Async Function() As Task(Of (OI As Double, MarkPrice As Double)?)
                Dim url As String = BaseUrl & "/public/get_book_summary_by_instrument" &
                                    "?instrument_name=BTC-PERPETUAL"
                Dim json As String = Await _http.GetStringAsync(url)
                Dim doc As JsonDocument = JsonDocument.Parse(json)
                Dim result As JsonElement = doc.RootElement.GetProperty("result")
                Dim item As JsonElement = result(0)
                Dim oi As Double = item.GetProperty("open_interest").GetDouble()
                Dim mp As Double = item.GetProperty("mark_price").GetDouble()
                Return CType((oi, mp), (OI As Double, MarkPrice As Double)?)
            End Function,
            "GetBookSummaryAsync")
    End Function

    ' ── L2 Order book snapshot (for OFI) ─────────────────────────────────────────────
    Public Shared Async Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot)
        Return Await ExecuteWithRetry(Of OrderBookSnapshot)(
            Async Function() As Task(Of OrderBookSnapshot)
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
            End Function,
            "GetOrderBookAsync")
    End Function

    ' ── Recent trades (for liquidation detection) ──────────────────────────────────────────
    Public Shared Async Function GetRecentTradesAsync(count As Integer) As Task(Of List(Of TradeRecord))
        Return Await ExecuteWithRetry(Of List(Of TradeRecord))(
            Async Function() As Task(Of List(Of TradeRecord))
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
                    Dim liqEl As JsonElement = Nothing
                    If t.TryGetProperty("liquidation", liqEl) Then
                        rec.Liquidation = liqEl.GetString()
                    Else
                        rec.Liquidation = "none"
                    End If
                    list.Add(rec)
                Next
                Return list
            End Function,
            "GetRecentTradesAsync")
    End Function
End Class

' ── Data transfer objects ─────────────────────────────────────────────────────────────────────────────

Public Class Candle
    Public Property Timestamp As Long
    Public Property [Open] As Double
    Public Property High As Double
    Public Property Low As Double
    Public Property Close As Double
    Public Property Volume As Double      ' BTC volume -- used for all indicator scoring
    Public Property VolumeUSD As Double   ' USD volume -- display only
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
