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

    ' ── [v64] Trade-store streaming capture (in-app-trade-store-capture-proposal.md §1.1)
    ' The raw stream is already in hand, so appending it to the store is a WRITE, not a
    ' fetch — which is what removes Deribit's ~24 h public-trades retention deadline for
    ' everything captured while the app is up. Buffered (60k trades/day would otherwise mean
    ' 60k file opens) and flushed on the D2 dual trigger: every flush_seconds OR every
    ' flush_trade_count, whichever comes first. Rebuilt when store_dir hot-reloads.
    Private _tradeStore As TradeStoreWriter
    Private _tradeStoreDir As String = Nothing
    Private _flushTimer As Threading.Timer
    Private _flushPeriodSec As Integer = -1
    Private ReadOnly _tradeStoreLock As New Object()

    ' ── Health surface (P2) — written on the background supervisor/receive task, read on
    ' the analysis thread for the per-run fallback gate + the WS-health status line. Plain
    ' fields: torn reads of a bool/int/DateTime are harmless for a per-run gate / display
    ' (MarketState carries the real locked state). No Control.Invoke — CLI-port aligned.
    Private _connected As Boolean = False
    Private _reconnectCount As Integer = 0
    Private _lastFrameUtc As DateTime = DateTime.MinValue
    Private _coolingDown As Boolean = False
    Private _currentBackoffMs As Integer = 1000

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
        StartFlushTimer()
        _runTask = Task.Run(Function() RunLoopAsync(_cts.Token))
        Return Task.CompletedTask
    End Function

    ''' <summary>Signal the loop to stop. The background task unwinds and disposes the socket.
    ''' [v64] The buffered trade tail is flushed here so a clean shutdown never drops captured
    ''' tape; a kill still costs at most flush_seconds, which gap repair recovers.</summary>
    Public Sub [Stop]()
        _cts?.Cancel()
        Try
            _flushTimer?.Dispose()
        Catch
        End Try
        _flushTimer = Nothing
        FlushTradeStore()
    End Sub

    ' ── [v64] Trade-store capture plumbing ──────────────────────────────────────────────

    ' D2 TIME trigger. A real timer rather than an elapsed-time check inside ApplyTrades:
    ' the case the time trigger exists for is a quiet hour, which is exactly when no batch
    ' arrives to run such a check. Period is re-read on every tick so flush_seconds
    ' hot-reloads like the rest of the block.
    Private Sub StartFlushTimer()
        If _flushTimer IsNot Nothing Then Return
        _flushPeriodSec = Math.Max(1, SettingsLoader.Current.TradeStore.FlushSeconds)
        _flushTimer = New Threading.Timer(AddressOf OnFlushTick, Nothing,
                                          _flushPeriodSec * 1000, _flushPeriodSec * 1000)
    End Sub

    Private Sub OnFlushTick(state As Object)
        Try
            FlushTradeStore()
            Dim want As Integer = Math.Max(1, SettingsLoader.Current.TradeStore.FlushSeconds)
            If want <> _flushPeriodSec AndAlso _flushTimer IsNot Nothing Then
                _flushPeriodSec = want
                _flushTimer.Change(want * 1000, want * 1000)
            End If
        Catch ex As Exception
            ' Never let a capture problem escape onto a timer thread and kill the process.
            Log("trade-store flush tick error: " & ex.Message)
        End Try
    End Sub

    Private Sub FlushTradeStore()
        Try
            Dim w As TradeStoreWriter
            SyncLock _tradeStoreLock
                w = _tradeStore
            End SyncLock
            w?.Flush()
        Catch ex As Exception
            Log("trade-store flush error: " & ex.Message)
        End Try
    End Sub

    ' Resolve the writer for the configured store dir, rebuilding if store_dir hot-reloaded.
    ' Nothing when capture is disabled — the caller early-outs and the feed does no extra work.
    Private Function ResolveTradeStore(ts As TradeStoreSettings) As TradeStoreWriter
        If Not TradeStoreWriter.ShouldCapture(ts) Then Return Nothing
        Dim dir As String = TradeStoreWriter.ResolveStoreDir(ts.StoreDir)
        SyncLock _tradeStoreLock
            If _tradeStore Is Nothing OrElse Not String.Equals(dir, _tradeStoreDir, StringComparison.OrdinalIgnoreCase) Then
                ' Dir changed under us — commit whatever the old writer holds before swapping.
                _tradeStore?.Flush()
                _tradeStore = New TradeStoreWriter(dir)
                _tradeStoreDir = dir
            End If
            Return _tradeStore
        End SyncLock
    End Function

    ''' <summary>[C1 Session 2 / Part B] Live capture-health snapshot for the TAPE STORE
    ''' status element — a read plus a label over state TradeStoreWriter already tracks, per
    ''' the proposal's own framing. Enabled=False (everything else default) when capture is
    ''' off or no writer has been constructed yet (no trade folded this process life) — the
    ''' UI host treats that as "nothing to show", the same as a REST-only transport hides the
    ''' WS-health segment. Never throws.</summary>
    Public Function GetTradeStoreStatus() As TradeStoreStatus
        Dim snap As New TradeStoreStatus()
        Try
            Dim ts = SettingsLoader.Current.TradeStore
            snap.Enabled = TradeStoreWriter.ShouldCapture(ts)
            If Not snap.Enabled Then Return snap
            Dim w As TradeStoreWriter
            SyncLock _tradeStoreLock
                w = _tradeStore
            End SyncLock
            snap.FlushSeconds = Math.Max(1, ts.FlushSeconds)
            If w Is Nothing Then Return snap
            snap.RowsThisProcess = w.TotalRowsWritten
            If w.LastFlushUtc.HasValue Then
                snap.SecondsSinceFlush = (DateTime.UtcNow - w.LastFlushUtc.Value).TotalSeconds
            End If
        Catch ex As Exception
            Log("GetTradeStoreStatus error: " & ex.Message)
        End Try
        Return snap
    End Function

    ' ── Connect / reconnect supervisor ──────────────────────────────────────────────────
    Private Async Function RunLoopAsync(ct As CancellationToken) As Task
        Dim backoffMs As Integer = 1000
        Dim reconnects As New Queue(Of DateTime)()

        While Not ct.IsCancellationRequested
            ' Did THIS cycle actually establish a connection? Only a connect-then-drop counts
            ' as a "flap" for the storm guard — still-down retries during one outage do not.
            Dim connectedThisCycle As Boolean = False
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
                    _currentBackoffMs = backoffMs
                    _connected = True
                    connectedThisCycle = True
                    Await ReceiveLoopAsync(ws, ct)
                End Using
            Catch ex As OperationCanceledException
                Exit While
            Catch ex As Exception
                Log("connection error: " & ex.Message)
            Finally
                _connected = False
            End Try

            If ct.IsCancellationRequested Then Exit While

            ' Storm guard: >5 FLAPS / 10 min → hold the feed down for the cooldown. Only a
            ' connect-then-drop (connectedThisCycle) counts as a flap; a still-down retry loop
            ' during a single outage does NOT — otherwise exponential backoff racks up ~6
            ' attempts in the first 60s and a brief blip trips the cooldown, leaving the feed
            ' down ~5 min even after the network returns. With this, a single outage (however
            ' long) recovers within one ≤60s backoff cycle; only genuine flapping cools down.
            Dim nowUtc As DateTime = DateTime.UtcNow
            _reconnectCount += 1   ' status counter — every attempt (display only)
            If connectedThisCycle Then
                reconnects.Enqueue(nowUtc)
                While reconnects.Count > 0 AndAlso (nowUtc - reconnects.Peek()).TotalMinutes > 10
                    reconnects.Dequeue()
                End While
            End If

            Dim delayMs As Integer
            If reconnects.Count > 5 Then
                Log("reconnect storm (" & reconnects.Count & "/10min) — cooling down " & _cooldownSec & "s")
                delayMs = _cooldownSec * 1000
                _currentBackoffMs = delayMs
                _coolingDown = True
                reconnects.Clear()
            Else
                delayMs = backoffMs
                _currentBackoffMs = backoffMs
                Log("reconnecting in " & (backoffMs \ 1000) & "s")
                backoffMs = Math.Min(backoffMs * 2, 60000)
            End If

            Try
                Await Task.Delay(delayMs, ct)
            Catch ex As OperationCanceledException
                Exit While
            End Try
            _coolingDown = False
        End While
        _connected = False
        Log("feed stopped")
    End Function

    ' ── REST seeding (startup + every reconnect) ────────────────────────────────────────
    ' WS streams forward only, so the deep history is seeded from REST so the first reads
    ' are complete windows. Seed-then-subscribe leaves a tiny boundary gap (trades in the
    ' seed→subscribe window); shadow parity in P2 is the real proof.
    Private Async Function SeedAsync(ct As CancellationToken) As Task
        Dim nowUtc As DateTime = DateTime.UtcNow
        ' [P4 #4] Fresh connection → clear the OFI accumulator so a stale pre-disconnect
        ' average can't bleed across the gap; the warmup fallback re-arms (proposal §4.1/§8).
        ' Runs before SubscribeAsync, so no book-update fold for this connection precedes it.
        _state.ResetOfiAccumulator()
        ' [P4 #5] Same discipline for the aggressor-velocity accumulator — reset on every
        ' (re)connect so pre-disconnect flow can't bleed across the gap; the cold-start
        ' suppression re-arms. Seed trades are deliberately NOT folded (only live prints
        ' carry the burst signal; the warmup gate covers the cold window).
        _state.ResetAggressorVelocity()
        ' [P4 #6] Same discipline for the absorption tracker — a pre-gap episode must
        ' never survive a reconnect (its band trajectory spans the gap and is a lie).
        ' Carried levels also clear; they re-carry at the next completed full run and
        ' the tracker re-arms on the next approach.
        _state.ResetAbsorption()
        ' [#7 + #8 v59] Same discipline for the alerts tracker (cascade + approach) —
        ' a pre-gap CASCADE edge cannot persist across a feed gap; a pre-gap approach
        ' episode's price context is gone. The per-process first-liq-seen flag is
        ' PRESERVED across reconnects within the same process (that persistence lives
        ' in the sidecar file, not tracker memory — H4 amended).
        _state.ResetAlerts()
        ' [v64] Same discipline for the trade-store buffer, with one difference: the pending
        ' buffer is FLUSHED rather than discarded (a reconnect must not silently drop captured
        ' tape), and only then is the monotonic guard un-seeded so it re-reads the on-disk
        ' high-water mark. That is what makes the REST re-seed window below idempotent against
        ' whatever is already stored.
        Try
            SyncLock _tradeStoreLock
                _tradeStore?.ResetBufferState()
            End SyncLock
        Catch ex As Exception
            Log("trade-store reset error: " & ex.Message)
        End Try
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
            _lastFrameUtc = DateTime.UtcNow   ' liveness stamp for the WS-health status line
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
    ' [P4 #5] Each streamed trade also folds into the aggressor-velocity accumulator
    ' (the trade analogue of FoldOfiAverage in ApplyBook). Config is read once per
    ' notification batch; SettingsLoader.Current each call honours hot-reload of
    ' enabled / fast_window_sec / the per-session norm (avg-window keys are read-time
    ' resolved). When the feature is off the feed does no extra work.
    Private Sub ApplyTrades(data As JsonElement, nowUtc As DateTime)
        If data.ValueKind <> JsonValueKind.Array Then Return
        Dim cfg = SettingsLoader.Current
        Dim av = cfg.Indicators.AggressorVelocity
        Dim foldAggr As Boolean = av IsNot Nothing AndAlso av.Enabled
        Dim tauNorm As Double = 0.0
        If foldAggr Then
            ' The trade's own exchange stamp is the fold dt basis; the SESSION is resolved
            ' from the receive hour (a boundary-straddling batch shifts the norm tau one
            ' trade early/late — immaterial at a 60/120s horizon).
            tauNorm = ExecutionResolution.ResolveAggrVelNormWindow(cfg, nowUtc.Hour)
        End If
        ' [P4 #6] Absorption trade fold (the trade half of the dual fold). Config read
        ' once per notification batch; SettingsLoader.Current honours hot-reload of
        ' enabled + the flat tracker params. Off ⇒ no extra work in the feed.
        Dim ab = cfg.Indicators.Absorption
        Dim foldAbs As Boolean = ab IsNot Nothing AndAlso ab.Enabled
        ' [#7 + #8 v59] Alerts fold — the same per-batch cfg read pattern. Enabled ⇒
        ' the tracker maintains the liq window (H2) + level-approach episodes (H3);
        ' first-liq-seen writes into the sidecar (H4 amended) inside the tracker.
        Dim al = cfg.Alerts
        Dim foldAlerts As Boolean = al IsNot Nothing AndAlso al.Enabled
        Dim alInstance As String = If(foldAlerts, ProcessIdentity.InstanceId, "")
        ' [v64] Trade-store capture — the same per-batch cfg read pattern. The stream is
        ' already in hand, so this is a buffered WRITE, not a fetch. Disabled ⇒ Nothing here
        ' and the fold below is inert, byte-identical to pre-build (A48f).
        Dim ts = cfg.TradeStore
        Dim store As TradeStoreWriter = ResolveTradeStore(ts)
        For Each t As JsonElement In data.EnumerateArray()
            Dim rec As New TradeRecord()
            rec.Price = t.GetProperty("price").GetDouble()
            rec.Amount = t.GetProperty("amount").GetDouble()
            rec.Direction = t.GetProperty("direction").GetString()
            rec.Timestamp = t.GetProperty("timestamp").GetInt64()
            Dim liqEl As JsonElement = Nothing
            rec.Liquidation = If(t.TryGetProperty("liquidation", liqEl), liqEl.GetString(), "none")
            ' [trade identity] The WS half of the capture path. Same two shared readers the REST
            ' backfill uses — one seam, so the two feeds cannot disagree about these fields'
            ' shape (§0 trap 3). Confirmed live at the §1 gate: a trade seen on BOTH feeds
            ' carried the same trade_id and the same trade_seq.
            rec.TradeId = TradeRecord.ReadTradeId(t)
            rec.TradeSeq = TradeRecord.ReadTradeSeq(t)
            _state.AppendTrade(rec, nowUtc)
            If foldAggr Then
                _state.FoldAggressorVelocity(rec.Amount, rec.Direction = "buy", rec.Timestamp,
                                             av.FastWindowSec, tauNorm)
            End If
            If foldAbs Then
                _state.FoldAbsorptionTrade(rec.Price, rec.Amount, rec.Direction = "buy",
                                           rec.Timestamp, ab)
            End If
            If foldAlerts Then
                _state.FoldAlertsTrade(rec.Price, rec.Amount, rec.Direction = "buy",
                                       rec.Liquidation IsNot Nothing AndAlso rec.Liquidation <> "none",
                                       rec.Timestamp, al, alInstance)
            End If
            ' [v64] Buffer for the store. The writer's monotonic guard drops anything at or
            ' before the newest committed timestamp, which is what makes the reconnect
            ' re-seed idempotent — SeedAsync re-seeds the ring from REST on every (re)connect,
            ' so the same trades WILL arrive twice (A48b).
            If store IsNot Nothing Then store.Buffer(rec)
        Next
        ' D2 COUNT trigger — checked once per batch, after the fold. The timer covers the
        ' quiet-hour case this cannot.
        If store IsNot Nothing AndAlso store.PendingCount >= Math.Max(1, ts.FlushTradeCount) Then
            store.Flush()
        End If
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
        FoldOfiAverage(snap, nowUtc)
        FoldAbsorptionBook(snap, nowUtc)
    End Sub

    ' [P4 #6] Fold this book update into the absorption tracker (the book half of the
    ' dual fold — the analogue of FoldOfiAverage). Receive time is the fold stamp (book
    ' updates carry no exchange stamp — same basis as the OFI fold). Reading
    ' SettingsLoader.Current each fold honours hot-reload of enabled + the tracker
    ' params; when the feature is off the feed does no extra work.
    Private Sub FoldAbsorptionBook(snap As OrderBookSnapshot, nowUtc As DateTime)
        Dim ab = SettingsLoader.Current.Indicators.Absorption
        If ab Is Nothing OrElse Not ab.Enabled Then Return
        Dim tsMs As Long = New DateTimeOffset(nowUtc).ToUnixTimeMilliseconds()
        _state.FoldAbsorptionBook(snap, tsMs, ab)
    End Sub

    ' [P4 #4] Fold this book update's top-book imbalance into the time-averaged OFI accumulator
    ' (docs/time-averaged-ofi-proposal.md §4.1). Uses the SAME weighted-imbalance math as the
    ' snapshot CalcOFI (IndicatorEngine.ComputeOfiImbalance) so the averaged OFIRatio is a
    ' cleaner version of the same quantity. tau = avg_window_sec; the receive time is the fold
    ' stamp (dt basis). Reading SettingsLoader.Current each fold honours hot-reload of
    ' averaging_enabled / avg_window_sec (avg_window_sec is on the tweaker surface). When
    ' averaging is off the feed does no extra work; the run path stays on snapshot OFI.
    Private Sub FoldOfiAverage(snap As OrderBookSnapshot, nowUtc As DateTime)
        Dim ofi = SettingsLoader.Current.Indicators.OFI
        If Not ofi.AveragingEnabled Then Return
        Dim bidVol, askVol, ratio As Double
        If Not IndicatorEngine.ComputeOfiImbalance(snap, ofi.BookDepth, bidVol, askVol, ratio) Then Return
        Dim tsMs As Long = New DateTimeOffset(nowUtc).ToUnixTimeMilliseconds()
        _state.FoldOfi(bidVol, askVol, ratio, tsMs, ofi.AvgWindowSec)
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

    ' ── Health surface (P2: per-run fallback gate + WS-health status line) ───────────────

    ''' <summary>The per-run health gate (handoff §2.2). Degraded when not connected, in
    ''' the reconnect-storm cooldown, or ALL primary streams (book/trades/ticker) are stale
    ''' past ws_stale_after_sec. When ws_fallback_to_rest is on and this is true at run time,
    ''' RunAnalysisAsync serves that run from REST and surfaces DEGRADED. A single transiently
    ''' stale stream on an otherwise-live connection is NOT degraded — the WsMarketDataSource
    ''' staleness gate returns Nothing for that one shape and the existing skip-gate handles
    ''' it like a REST failure (no row lost).</summary>
    Public Function IsDegraded() As Boolean
        If Not _connected Then Return True
        If _coolingDown Then Return True
        Dim staleSec As Integer = SettingsLoader.Current.Network.WsStaleAfterSec
        Dim nowUtc As DateTime = DateTime.UtcNow
        Dim bookStale As Boolean = (nowUtc - _state.BookLastUpdate).TotalSeconds > staleSec
        Dim tradesStale As Boolean = (nowUtc - _state.TradesLastUpdate).TotalSeconds > staleSec
        Dim tickerStale As Boolean = (nowUtc - _state.TickerLastUpdate).TotalSeconds > staleSec
        Return bookStale AndAlso tradesStale AndAlso tickerStale
    End Function

    ''' <summary>True while the receive loop is running on a subscribed connection.</summary>
    Public ReadOnly Property IsConnected As Boolean
        Get
            Return _connected
        End Get
    End Property

    ''' <summary>Total reconnect attempts since StartAsync (the "R reconnects" status counter).</summary>
    Public ReadOnly Property ReconnectCount As Integer
        Get
            Return _reconnectCount
        End Get
    End Property

    ''' <summary>UTC of the last received frame (receive-loop liveness).</summary>
    Public ReadOnly Property LastFrameUtc As DateTime
        Get
            Return _lastFrameUtc
        End Get
    End Property

    ''' <summary>True during the reconnect-storm cooldown hold.</summary>
    Public ReadOnly Property IsCoolingDown As Boolean
        Get
            Return _coolingDown
        End Get
    End Property

    ''' <summary>Current reconnect delay in seconds (the "Xs backoff" status value).</summary>
    Public ReadOnly Property CurrentBackoffSec As Integer
        Get
            Return _currentBackoffMs \ 1000
        End Get
    End Property

    Private Shared Sub Log(msg As String)
        Console.WriteLine("[WS] " & msg)
    End Sub

End Class

''' <summary>[C1 Session 2 / Part B] Snapshot returned by DeribitWsFeed.GetTradeStoreStatus.
''' All fields safe-default (never a fake reading) — Enabled=False when capture is off or no
''' writer has been constructed yet.</summary>
Public Class TradeStoreStatus
    Public Property Enabled As Boolean = False
    ''' <summary>Nothing = this writer instance has never successfully flushed yet.</summary>
    Public Property SecondsSinceFlush As Double? = Nothing
    Public Property RowsThisProcess As Long = 0
    Public Property FlushSeconds As Integer = 30
End Class
