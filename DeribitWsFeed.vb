' DeribitWsFeed.vb
' WebSocket migration P1 (foundation, additive-only — docs/websocket-migration-p1-implementer-handoff.md).
'
' One public ClientWebSocket to Deribit (public channels only, no auth): connect →
' REST-seed full windows → set_heartbeat → subscribe → receive loop → exponential-backoff
' reconnect with a storm guard. Populates a MarketState. Host-agnostic: no WinForms, no
' Control.Invoke, no MainForm — all diagnostics go to Console.WriteLine.
'
' Dormant in P1: nothing in the live app calls StartAsync()/Stop(); only the §9
' standalone soak does. The live verdict path is untouched (pure REST). Reads settings
' once at start — a transport change needs a restart (hot-swap is out of scope).
'
' API-drift note: channel names and payload field names were verified against the
' current Deribit API docs at implementation (2026-06-19). See the P1 spec-back.
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading

Public NotInheritable Class DeribitWsFeed

    Private Const Instrument As String = "BTC-PERPETUAL"

    ' Subscriptions: 1/3/5/15 chart (v36 added 3-min for Asia/London exec), trades, ticker,
    ' depth-limited book (snapshot semantics — no change-application/checksum logic).
    Private Shared ReadOnly Channels As String() = {
        "book." & Instrument & ".none.10.100ms",
        "trades." & Instrument & ".100ms",
        "ticker." & Instrument & ".100ms",
        "chart.trades." & Instrument & ".1",
        "chart.trades." & Instrument & ".3",
        "chart.trades." & Instrument & ".5",
        "chart.trades." & Instrument & ".15"
    }

    ' Candle series to REST-seed on startup + every reconnect (resolution → seed count).
    Private Shared ReadOnly SeedResolutions As String() = {"1", "3", "5", "15"}

    Private ReadOnly _state As MarketState
    Private ReadOnly _wsUrl As String
    Private ReadOnly _heartbeatSec As Integer
    Private ReadOnly _cooldownSec As Integer
    Private ReadOnly _sendLock As New SemaphoreSlim(1, 1)

    Private _cts As CancellationTokenSource
    Private _runTask As Task
    Private _id As Integer = 0

    Public Sub New(state As MarketState)
        Me.New(state, Nothing)
    End Sub

    ''' <summary>Settings come from the network block by default; pass an override only for
    ''' the standalone soak.</summary>
    Public Sub New(state As MarketState, overrideWsUrl As String,
                   Optional heartbeatSec As Integer = -1, Optional cooldownSec As Integer = -1)
        Dim net = SettingsLoader.Current.Network
        _state = state
        _wsUrl = If(String.IsNullOrWhiteSpace(overrideWsUrl), net.WsUrl, overrideWsUrl)
        _heartbeatSec = If(heartbeatSec > 0, heartbeatSec, net.WsHeartbeatSec)
        _cooldownSec = If(cooldownSec > 0, cooldownSec, net.WsCooldownSec)
    End Sub

    ''' <summary>Launch the connect/receive/reconnect loop on a background task. Idempotent.</summary>
    Public Function StartAsync() As Task
        If _runTask IsNot Nothing Then Return Task.CompletedTask
        _cts = New CancellationTokenSource()
        _runTask = Task.Run(Function() RunLoopAsync(_cts.Token))
        Return Task.CompletedTask
    End Function

    ''' <summary>Signal the loop to stop. The background task unwinds and disposes the socket.</summary>
    Public Sub [Stop]()
        _cts?.Cancel()
    End Sub

    ' ── Connect / reconnect supervisor ──────────────────────────────────────────────────
    Private Async Function RunLoopAsync(ct As CancellationToken) As Task
        Dim backoffMs As Integer = 1000
        Dim reconnects As New Queue(Of DateTime)()

        While Not ct.IsCancellationRequested
            Try
                Using ws As New ClientWebSocket()
                    Log("connecting to " & _wsUrl)
                    Await ws.ConnectAsync(New Uri(_wsUrl), ct)
                    Log("connected; seeding via REST…")
                    Await SeedAsync(ct)
                    Await SetHeartbeatAsync(ws, ct)
                    Await SubscribeAsync(ws, ct)
                    Log("subscribed to " & Channels.Length & " channels")
                    backoffMs = 1000   ' healthy connection → reset backoff
                    Await ReceiveLoopAsync(ws, ct)
                End Using
            Catch ex As OperationCanceledException
                Exit While
            Catch ex As Exception
                Log("connection error: " & ex.Message)
            End Try

            If ct.IsCancellationRequested Then Exit While

            ' Storm guard: >5 reconnects / 10 min → hold the feed down for the cooldown.
            Dim nowUtc As DateTime = DateTime.UtcNow
            reconnects.Enqueue(nowUtc)
            While reconnects.Count > 0 AndAlso (nowUtc - reconnects.Peek()).TotalMinutes > 10
                reconnects.Dequeue()
            End While

            Dim delayMs As Integer
            If reconnects.Count > 5 Then
                Log("reconnect storm (" & reconnects.Count & "/10min) — cooling down " & _cooldownSec & "s")
                delayMs = _cooldownSec * 1000
                reconnects.Clear()
            Else
                delayMs = backoffMs
                Log("reconnecting in " & (backoffMs \ 1000) & "s")
                backoffMs = Math.Min(backoffMs * 2, 60000)
            End If

            Try
                Await Task.Delay(delayMs, ct)
            Catch ex As OperationCanceledException
                Exit While
            End Try
        End While
        Log("feed stopped")
    End Function

    ' ── REST seeding (startup + every reconnect) ────────────────────────────────────────
    ' WS streams forward only, so the deep history is seeded from REST so the first reads
    ' are complete windows. Seed-then-subscribe leaves a tiny boundary gap (trades in the
    ' seed→subscribe window); shadow parity in P2 is the real proof.
    Private Async Function SeedAsync(ct As CancellationToken) As Task
        Dim nowUtc As DateTime = DateTime.UtcNow
        For Each res As String In SeedResolutions
            ct.ThrowIfCancellationRequested()
            Dim cap As Integer = 250
            MarketState.Caps.TryGetValue(res, cap)
            Dim candles = Await DeribitClient.GetCandlesAsync(res, cap)
            If candles IsNot Nothing Then _state.SeedCandles(res, candles, nowUtc)
        Next
        ct.ThrowIfCancellationRequested()
        Dim trades = Await DeribitClient.GetRecentTradesAsync(500)
        If trades IsNot Nothing Then _state.SeedTrades(trades, nowUtc)
    End Function

    ' ── Outbound JSON-RPC ───────────────────────────────────────────────────────────────
    Private Function NextId() As Integer
        Return Interlocked.Increment(_id)
    End Function

    Private Async Function SendJsonAsync(ws As ClientWebSocket, payload As String, ct As CancellationToken) As Task
        Dim bytes() As Byte = Encoding.UTF8.GetBytes(payload)
        Await _sendLock.WaitAsync(ct)
        Try
            Await ws.SendAsync(New ArraySegment(Of Byte)(bytes), WebSocketMessageType.Text, True, ct)
        Finally
            _sendLock.Release()
        End Try
    End Function

    Private Async Function SetHeartbeatAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
        Dim payload As String =
            "{""jsonrpc"":""2.0"",""id"":" & NextId() &
            ",""method"":""public/set_heartbeat"",""params"":{""interval"":" & _heartbeatSec & "}}"
        Await SendJsonAsync(ws, payload, ct)
    End Function

    Private Async Function SubscribeAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
        Dim sb As New StringBuilder()
        For i As Integer = 0 To Channels.Length - 1
            If i > 0 Then sb.Append(",")
            sb.Append("""").Append(Channels(i)).Append("""")
        Next
        Dim payload As String =
            "{""jsonrpc"":""2.0"",""id"":" & NextId() &
            ",""method"":""public/subscribe"",""params"":{""channels"":[" & sb.ToString() & "]}}"
        Await SendJsonAsync(ws, payload, ct)
    End Function

    ' Reply to a heartbeat test_request — the #1 foot-gun: miss it and Deribit drops us.
    Private Async Function SendTestAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
        Dim payload As String =
            "{""jsonrpc"":""2.0"",""id"":" & NextId() & ",""method"":""public/test"",""params"":{}}"
        Await SendJsonAsync(ws, payload, ct)
        Log("heartbeat test_request → public/test")
    End Function

    ' ── Receive loop ────────────────────────────────────────────────────────────────────
    Private Async Function ReceiveLoopAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
        Dim buffer(16383) As Byte
        Dim sb As New StringBuilder()
        While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
            sb.Clear()
            Dim result As WebSocketReceiveResult
            Do
                result = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), ct)
                If result.MessageType = WebSocketMessageType.Close Then
                    Log("server close frame; will reconnect")
                    Return
                End If
                sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count))
            Loop While Not result.EndOfMessage
            Await HandleMessageAsync(ws, sb.ToString(), ct)
        End While
    End Function

    ' ── Frame routing ───────────────────────────────────────────────────────────────────
    Private Async Function HandleMessageAsync(ws As ClientWebSocket, json As String, ct As CancellationToken) As Task
        Dim methodName As String = Nothing
        Dim isTestRequest As Boolean = False
        Dim channel As String = Nothing

        Try
            Using doc As JsonDocument = JsonDocument.Parse(json)
                Dim root As JsonElement = doc.RootElement
                Dim methodEl As JsonElement = Nothing
                If Not root.TryGetProperty("method", methodEl) Then Return   ' RPC response/error — ignore
                methodName = methodEl.GetString()

                Dim params As JsonElement = Nothing
                If Not root.TryGetProperty("params", params) Then Return

                If methodName = "heartbeat" Then
                    Dim typeEl As JsonElement = Nothing
                    If params.TryGetProperty("type", typeEl) AndAlso typeEl.GetString() = "test_request" Then
                        isTestRequest = True
                    End If
                ElseIf methodName = "subscription" Then
                    Dim chEl As JsonElement = Nothing
                    If params.TryGetProperty("channel", chEl) Then channel = chEl.GetString()
                    Dim data As JsonElement = Nothing
                    If channel IsNot Nothing AndAlso params.TryGetProperty("data", data) Then
                        RouteSubscription(channel, data)
                    End If
                End If
            End Using
        Catch ex As Exception
            Log("parse error: " & ex.Message)
            Return
        End Try

        If isTestRequest Then Await SendTestAsync(ws, ct)
    End Function

    Private Sub RouteSubscription(channel As String, data As JsonElement)
        Dim nowUtc As DateTime = DateTime.UtcNow
        If channel.StartsWith("ticker.") Then
            ApplyTicker(data, nowUtc)
        ElseIf channel.StartsWith("trades.") Then
            ApplyTrades(data, nowUtc)
        ElseIf channel.StartsWith("book.") Then
            ApplyBook(data, nowUtc)
        ElseIf channel.StartsWith("chart.trades.") Then
            Dim res As String = channel.Substring(channel.LastIndexOf("."c) + 1)
            ApplyChart(res, data, nowUtc)
        End If
    End Sub

    ' funding_8h (NOT current_funding — parity with DeribitClient.GetFundingRateAsync), OI, mark, index.
    Private Sub ApplyTicker(data As JsonElement, nowUtc As DateTime)
        Dim funding As Double? = Nothing
        Dim el As JsonElement = Nothing
        If data.TryGetProperty("funding_8h", el) Then funding = el.GetDouble()
        Dim oi As Double = If(data.TryGetProperty("open_interest", el), el.GetDouble(), 0.0)
        Dim mark As Double = If(data.TryGetProperty("mark_price", el), el.GetDouble(), 0.0)
        Dim index As Double = If(data.TryGetProperty("index_price", el), el.GetDouble(), 0.0)
        _state.UpdateTicker(funding, oi, mark, index, nowUtc)
    End Sub

    ' data is an array of trade objects (mapped 1:1 to DeribitClient.GetRecentTradesAsync).
    Private Sub ApplyTrades(data As JsonElement, nowUtc As DateTime)
        If data.ValueKind <> JsonValueKind.Array Then Return
        For Each t As JsonElement In data.EnumerateArray()
            Dim rec As New TradeRecord()
            rec.Price = t.GetProperty("price").GetDouble()
            rec.Amount = t.GetProperty("amount").GetDouble()
            rec.Direction = t.GetProperty("direction").GetString()
            rec.Timestamp = t.GetProperty("timestamp").GetInt64()
            Dim liqEl As JsonElement = Nothing
            rec.Liquidation = If(t.TryGetProperty("liquidation", liqEl), liqEl.GetString(), "none")
            _state.AppendTrade(rec, nowUtc)
        Next
    End Sub

    ' Depth-limited book: bids/asks are [price, amount] pairs (same shape as REST get_order_book).
    Private Sub ApplyBook(data As JsonElement, nowUtc As DateTime)
        Dim snap As New OrderBookSnapshot()
        Dim bids As JsonElement = Nothing
        Dim asks As JsonElement = Nothing
        If data.TryGetProperty("bids", bids) AndAlso bids.ValueKind = JsonValueKind.Array Then
            For Each lvl As JsonElement In bids.EnumerateArray()
                snap.Bids.Add((lvl(0).GetDouble(), lvl(1).GetDouble()))
            Next
        End If
        If data.TryGetProperty("asks", asks) AndAlso asks.ValueKind = JsonValueKind.Array Then
            For Each lvl As JsonElement In asks.EnumerateArray()
                snap.Asks.Add((lvl(0).GetDouble(), lvl(1).GetDouble()))
            Next
        End If
        _state.UpdateBook(snap, nowUtc)
    End Sub

    ' chart.trades OHLCV: tick/open/high/low/close/volume/cost. Map cost → VolumeUSD exactly
    ' as DeribitClient maps the REST get_tradingview_chart_data response (fallback volume*close).
    Private Sub ApplyChart(resolution As String, data As JsonElement, nowUtc As DateTime)
        Dim c As New Candle()
        c.Timestamp = data.GetProperty("tick").GetInt64()
        c.Open = data.GetProperty("open").GetDouble()
        c.High = data.GetProperty("high").GetDouble()
        c.Low = data.GetProperty("low").GetDouble()
        c.Close = data.GetProperty("close").GetDouble()
        c.Volume = data.GetProperty("volume").GetDouble()
        Dim costEl As JsonElement = Nothing
        c.VolumeUSD = If(data.TryGetProperty("cost", costEl), costEl.GetDouble(), c.Volume * c.Close)
        _state.ApplyChartTick(resolution, c, nowUtc)
    End Sub

    Private Shared Sub Log(msg As String)
        Console.WriteLine("[WS] " & msg)
    End Sub
End Class
