' ─────────────────────────────────────────────────────────────────────────────────────────
' RawChannelProbe — does the AUTHENTICATED raw trades channel carry `liquidation` on FIRST
' DELIVERY? (HH-2, ruled 2026-09-28; docs/history-host-and-raw-channel-read-2026-09-28.md §3.)
'
' THE QUESTION. If `trades.BTC-PERPETUAL.raw` carries the liquidation flag when a trade is
' first delivered, the engine has a real-time flag it could score on. The public feeds and the
' main host's REST do not: the flag was observed arriving ~60 min AFTER the trade
' (docs/liquidation-probe-run-2026-09-21.md §0000). Second question: does the raw trade object
' carry ANY field the history host lacks?
'
' HOW.
'   * Authenticates (public/auth, client_credentials) on the WebSocket BEFORE subscribing,
'     refreshes at 80 % of expires_in, and re-authenticates on every reconnect.
'   * Subscribes 100ms, agg2 and raw — each in its OWN request (one refusal must not kill the rest).
'   * Records, per trade_id and per channel, the FIRST delivery: receive time, venue timestamp,
'     trade_seq, whether a `liquidation` property was present and its value, and a "shape" id
'     (the property-name list) — never the whole raw text, except for a delivery that carried a flag.
'   * DELAYED JUDGE: every 5 min, judges the venue-time slice that is now >= 90 min old. It pages
'     the history host by trade_seq (never by timestamp — the 2026-09-14 gap-repair lesson),
'     de-duplicates on trade_seq, splits on has_more, and asks for every history-flagged trade:
'     did each channel's first delivery carry the flag? A flag that arrives ~60 min late is
'     therefore visible; the old REST arm cannot see it and is NOT run here.
'   * After the collection window ends the probe stops collecting and keeps judging ("drain")
'     until the last slice is 90 min old, so the final hours are not censored.
'
' ⛔ SECRET HANDLING. The key is read ONLY from the environment variables
' DERIBIT_RO_CLIENT_ID / DERIBIT_RO_CLIENT_SECRET. Nothing here writes, logs or dumps the
' secret, the access_token or the refresh_token:
'   - the auth/refresh REQUEST is built and sent, never logged;
'   - the auth/refresh RESPONSE is handled before any generic logging, and only expires_in or an
'     error code + message is printed;
'   - any other control frame that mentions a credential field is withheld, not printed;
'   - the raw dump is per-trade objects only, and the history HttpClient carries no credentials.
' The refresh token lives in one module field for the life of the process and is never persisted.
' ⛔ Dev machine ONLY. Never copy this build or the key to the collector box.
'
' Host-agnostic: no WinForms, no app coupling. The one Windows-only call (keep-awake) is
' guarded and optional.
' ─────────────────────────────────────────────────────────────────────────────────────────
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Namespace Global.DeribitVerdictEngine

    ''' <summary>What one channel's FIRST delivery of one trade looked like.</summary>
    Friend Structure ChanSlot
        Public Delivered As Boolean
        Public LiqPresent As Boolean          ' a `liquidation` property was on the object
        Public LiqFlagged As Boolean          ' ... and its value is not "none"
        Public LaterFlagSeen As Boolean       ' a LATER delivery carried a flag the first one lacked
        Public RecvTicks As Long
        Public DelayMs As Integer             ' receive clock minus venue timestamp (clock skew included)
        Public LiqValue As String             ' Nothing unless the property was present
        Public Shape As Integer               ' index into ChanStats.Shapes; -1 = shape list full
        Public RawIfFlagged As String         ' first-delivery raw text, kept ONLY when it carried a flag
    End Structure

    ''' <summary>One trade_id in the first-delivery index: one slot per channel.</summary>
    Friend Class FirstRec
        Public TradeId As String = ""
        Public VenueTs As Long
        Public Seq As Long
        Public FirstAnyTicks As Long
        Public Slots As ChanSlot() = New ChanSlot(2) {}
    End Class

    ''' <summary>A trade as the history host returned it (only what the judge needs).</summary>
    Friend Class HistTrade
        Public Seq As Long
        Public TradeId As String = ""
        Public Ts As Long
        Public Liq As String = ""
        Public Raw As String = ""             ' kept only for flagged trades
    End Class

    Friend Class ChanStats
        Public Name As String = ""
        Public Label As String = ""
        Public SubState As String = "NOT SUBSCRIBED"
        Public Objects As Long
        Public Delivered As Long              ' distinct trade_ids, first deliveries
        Public LiqFieldPresent As Long        ' first deliveries with a `liquidation` property at all
        Public LiqFlaggedAtDelivery As Long   ' first deliveries whose value was not "none"
        Public DelaySumMs As Long
        Public DelayN As Long
        Public DelayMaxMs As Long
        Public Carried As Long                ' history-flagged trades whose FIRST delivery carried the flag
        Public NotCarried As Long             ' history-flagged, delivered, first delivery had no flag
        Public NeverDelivered As Long         ' history-flagged, this channel never delivered it
        Public CarriedButHistNone As Long     ' first delivery flagged, history says none
        Public LaterFlag As Long              ' flag arrived on a later delivery of the same trade
        Public Fields As New Dictionary(Of String, Long)()
        Public LiqValues As New Dictionary(Of String, Long)()
        Public ShapeIds As New Dictionary(Of String, Integer)()
        Public Shapes As New List(Of String)()
        Public ShapeN As New List(Of Long)()
    End Class

    Public Module RawChannelProbe

        Private Const WsUrl As String = "wss://www.deribit.com/ws/api/v2"
        Private Const HistUrl As String = "https://history.deribit.com/api/v2/public/get_last_trades_by_instrument"
        Private Const Instrument As String = "BTC-PERPETUAL"
        Private Const EnvId As String = "DERIBIT_RO_CLIENT_ID"
        Private Const EnvSecret As String = "DERIBIT_RO_CLIENT_SECRET"
        Private Const HeartbeatSec As Integer = 30

        Private ReadOnly ChanNames As String() = {
            "trades.BTC-PERPETUAL.100ms", "trades.BTC-PERPETUAL.agg2", "trades.BTC-PERPETUAL.raw"}
        Private ReadOnly ChanLabels As String() = {"100ms", "agg2", "raw"}
        Private Const RawIdx As Integer = 2

        ' Index cap in BYTES. Measured per-entry cost is not known before the run, so the estimate
        ' is deliberately generous (rec object + three 56 B slots + key string + dictionary entry +
        ' queue entry). 128 MB / 400 B = 320k entries; 150 min of BTC-PERPETUAL is ~15k trades on
        ' an ordinary day, ~100k in a violent one. The real private working set is in every status
        ' line, so the estimate is checked against the process, not trusted.
        Private Const EntryBytesEstimate As Long = 400L
        Private Const IndexCapBytes As Long = 128L * 1024L * 1024L
        ' A rec older than this that was never judged is dropped and counted.
        Private Const ExpireMin As Integer = 200

        ' History paging. Step 800 keeps a page under count=1000 even after the host widens the
        ' range to whole milliseconds; a page that still reports has_more is split in halves.
        Private Const HistStepSeq As Long = 800L
        Private Const HistPauseMs As Integer = 150
        Private Const MinSliceMs As Long = 60000L
        Private Const JudgeEverySec As Integer = 300
        Private Const DrainPollSec As Integer = 60
        Private Const OutagePadMs As Long = 5000L

        Private Const StatusTickSec As Integer = 30
        Private Const StatusEveryTicks As Integer = 10
        Private Const RefreshFraction As Double = 0.8R

        Private Const ExitNoCreds As Integer = 3
        Private Const ExitAuthFailed As Integer = 4
        Private Const ExitRawRefused As Integer = 5

        Private ReadOnly _gate As New Object()
        Private ReadOnly _ch As ChanStats() = MakeStats()
        Private ReadOnly _index As New Dictionary(Of String, FirstRec)()
        Private ReadOnly _order As New Queue(Of FirstRec)()
        Private ReadOnly _histFields As New Dictionary(Of String, Long)()
        Private ReadOnly _outages As New List(Of Long())()
        Private ReadOnly _subIds As New Dictionary(Of Integer, Integer)()

        ' credentials: module fields, read from the environment once, never written anywhere
        Private _clientId As String = ""
        Private _clientSecret As String = ""
        Private _refreshToken As String = ""

        Private _authId As Integer = 0
        Private _refreshId As Integer = 0
        Private _authOk As Boolean = False
        Private _refreshDueUtc As DateTime = DateTime.MaxValue
        Private _refreshSentUtc As DateTime = DateTime.MinValue
        Private _authFailures As Integer = 0
        Private _authOks As Long = 0
        Private _refreshOks As Long = 0
        Private _subsAcceptedThisConn As Integer = 0
        Private _rawTriedPrivate As Boolean = False
        Private _fatal As Integer = 0
        Private _collectCts As CancellationTokenSource = Nothing

        Private _reconnectGen As Integer = 0
        Private _disconnectedAtMs As Long = 0
        Private _nextId As Integer = 1000
        Private _batches As Long = 0

        Private _judgedUpToTs As Long = 0
        Private _collectEndMs As Long = 0
        Private _judgeMinAgeMin As Integer = 90
        Private _refreshCapSec As Integer = 0
        Private _judgeRuns As Long = 0
        Private _judgeEmptySlices As Long = 0
        Private _judgeFetchFails As Long = 0
        Private _histTradesJudged As Long = 0
        Private _histFlagged As Long = 0
        Private _histFlaggedCensored As Long = 0
        Private _histFlaggedNoIndex As Long = 0
        Private _recsNotInHistory As Long = 0
        Private _expiredUnjudged As Long = 0
        Private _evictedForCap As Long = 0
        Private _histRequests As Long = 0
        Private _histErrors As Long = 0
        Private _histSplits As Long = 0

        Private _flagOut As StreamWriter = Nothing
        Private _sampleOut As StreamWriter = Nothing
        Private ReadOnly _samples As Integer() = {0, 0, 0}
        Private ReadOnly _flagSamples As Integer() = {0, 0, 0}
        Private _runStamp As String = ""
        Private _startUtc As DateTime

        Private Declare Function SetThreadExecutionState Lib "kernel32.dll" (esFlags As UInteger) As UInteger

        Private Function MakeStats() As ChanStats()
            Dim a(2) As ChanStats
            For i As Integer = 0 To 2
                a(i) = New ChanStats() With {.Name = ChanNames(i), .Label = ChanLabels(i)}
            Next
            Return a
        End Function

        ''' <summary>
        ''' args(0) = collection seconds (0 = until STOP file / Ctrl+C).
        ''' args(1) = judge minimum age in minutes (default 90; lower it ONLY for a smoke run).
        ''' </summary>
        Public Function Run(args As String()) As Integer
            Dim seconds As Integer = 0
            If args IsNot Nothing AndAlso args.Length > 0 Then
                Dim p As Integer
                If Integer.TryParse(args(0), NumberStyles.Integer, CultureInfo.InvariantCulture, p) AndAlso p >= 0 Then seconds = p
            End If
            If args IsNot Nothing AndAlso args.Length > 1 Then
                Dim p As Integer
                If Integer.TryParse(args(1), NumberStyles.Integer, CultureInfo.InvariantCulture, p) AndAlso p > 0 Then _judgeMinAgeMin = p
            End If

            ' args(2): TEST ONLY. Caps the refresh interval (seconds) so a smoke run can exercise the
            ' refresh path. The venue's tokens last a year (measured 2026-09-29: expires_in=31536000),
            ' so 80 % of that never fires inside a run. Leave it off for the real run.
            If args IsNot Nothing AndAlso args.Length > 2 Then
                Dim p As Integer
                If Integer.TryParse(args(2), NumberStyles.Integer, CultureInfo.InvariantCulture, p) AndAlso p > 0 Then _refreshCapSec = p
            End If

            _clientId = If(Environment.GetEnvironmentVariable(EnvId), "")
            _clientSecret = If(Environment.GetEnvironmentVariable(EnvSecret), "")
            If _clientId.Length = 0 OrElse _clientSecret.Length = 0 Then
                Console.Error.WriteLine("STOP: " & EnvId & " and " & EnvSecret & " must both be set. Nothing was sent.")
                Return ExitNoCreds
            End If
            ' Lengths only. The values are never printed.
            Console.WriteLine("credentials present: id length " & _clientId.Length & ", secret length " & _clientSecret.Length &
                              " (values never printed)")

            KeepAwake()
            Try
                Return RunAsync(seconds).GetAwaiter().GetResult()
            Catch ex As Exception
                Console.Error.WriteLine("FATAL: " & ex.GetType().Name & ": " & ex.Message)
                Return 2
            Finally
                CloseWriters()
            End Try
        End Function

        ''' <summary>Ask Windows not to sleep while this process runs. Optional; dies with the process.</summary>
        Private Sub KeepAwake()
            Try
                If OperatingSystem.IsWindows() Then
                    ' ES_CONTINUOUS | ES_SYSTEM_REQUIRED. Per-thread: the main thread blocks in Run() for the whole run.
                    SetThreadExecutionState(&H80000001UI)
                    Console.WriteLine("keep-awake requested (ES_SYSTEM_REQUIRED); it ends with this process")
                End If
            Catch ex As Exception
                Console.Error.WriteLine("keep-awake unavailable: " & ex.Message)
            End Try
        End Sub

        Private Async Function RunAsync(seconds As Integer) As Task(Of Integer)
            _startUtc = DateTime.UtcNow
            _runStamp = _startUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)

            Console.WriteLine("RawChannelProbe — HH-2: does the authenticated raw channel carry `liquidation` at first delivery?")
            Console.WriteLine("  instrument   : " & Instrument)
            For i As Integer = 0 To 2
                Console.WriteLine("  arm " & (i + 1) & "        : " & ChanNames(i))
            Next
            Console.WriteLine("  judge        : history host by trade_seq, slice age >= " & _judgeMinAgeMin & " min, every " & JudgeEverySec & " s")
            If _refreshCapSec > 0 Then Console.WriteLine("  ⚠ TEST ONLY: refresh interval capped at " & _refreshCapSec & " s")
            Console.WriteLine("  REST arm     : DISABLED for this run (it cannot see a flag that arrives ~60 min late)")
            Console.WriteLine("  collection   : " & If(seconds > 0, seconds & " s", "UNLIMITED - stop with a STOP file or Ctrl+C") &
                              "; then a drain until the last slice is judged (STOPNOW file skips the drain)")
            Console.WriteLine("  started      : " & _startUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & " UTC")
            Console.WriteLine()

            OpenWriters()

            _collectCts = If(seconds > 0, New CancellationTokenSource(TimeSpan.FromSeconds(seconds)), New CancellationTokenSource())
            Dim runCts As New CancellationTokenSource()
            Dim collectDone As Boolean = False
            AddHandler Console.CancelKeyPress,
                Sub(sender As Object, e As ConsoleCancelEventArgs)
                    e.Cancel = True
                    Console.WriteLine()
                    Try
                        If Not collectDone Then
                            Console.WriteLine("Ctrl+C — ending collection; the drain will finish the judging (Ctrl+C again to skip it)")
                            _collectCts.Cancel()
                        Else
                            Console.WriteLine("Ctrl+C — skipping the drain")
                            runCts.Cancel()
                        End If
                    Catch
                    End Try
                End Sub

            Dim wsTask As Task = WsSupervisorAsync(_collectCts.Token)
            Dim statusTask As Task = StatusLoopAsync(_collectCts, runCts, runCts.Token)
            Dim judgeTask As Task = JudgeLoopAsync(runCts.Token)

            Try
                Await wsTask
            Catch ex As OperationCanceledException
            Catch ex As Exception
                Console.Error.WriteLine("ws supervisor error: " & ex.GetType().Name & ": " & ex.Message)
            End Try
            collectDone = True
            SyncLock _gate
                _collectEndMs = UtcMs()
            End SyncLock
            Console.WriteLine("[" & NowUtc() & "] collection ended" &
                              If(_fatal <> 0, " (FATAL stop, code " & _fatal & ")", "") & "; draining the judge…")

            Try
                Await judgeTask
            Catch ex As Exception
                Console.Error.WriteLine("judge task error: " & ex.GetType().Name & ": " & ex.Message)
            End Try
            runCts.Cancel()
            Try
                Await statusTask
            Catch ex As Exception
            End Try

            Report()
            CloseWriters()
            runCts.Dispose()
            _collectCts.Dispose()
            Return _fatal
        End Function

        ' ── WebSocket: supervisor, auth, receive ─────────────────────────────────────────
        Private Async Function WsSupervisorAsync(ct As CancellationToken) As Task
            Dim backoff As Integer = 2
            While Not ct.IsCancellationRequested AndAlso _fatal = 0
                Dim failure As String = Nothing          ' VB forbids Await inside Catch
                Try
                    Using ws As New ClientWebSocket()
                        ' ⛔ WPAD proxy auto-detect hangs the connect before any socket opens (584c616).
                        ws.Options.Proxy = Nothing
                        Await ws.ConnectAsync(New Uri(WsUrl), ct)
                        SyncLock _gate
                            _reconnectGen += 1
                            _subsAcceptedThisConn = 0
                            _rawTriedPrivate = False
                            _subIds.Clear()
                        End SyncLock
                        _authOk = False
                        _refreshDueUtc = DateTime.MaxValue
                        _refreshId = 0
                        For i As Integer = 0 To 2
                            _ch(i).SubState = "NOT SUBSCRIBED"
                        Next
                        Console.WriteLine("[" & NowUtc() & "] ws connected (generation " & _reconnectGen & "); authenticating before any subscribe")
                        _authId = NextId()
                        Await SendAsync(ws, AuthRequest(_authId, "client_credentials"), ct)
                        Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() &
                                            ",""method"":""public/set_heartbeat"",""params"":{""interval"":" &
                                            HeartbeatSec & "}}", ct)
                        backoff = 2
                        Await ReceiveLoopAsync(ws, ct)
                    End Using
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Return
                Catch ex As Exception
                    failure = ex.GetType().Name & ": " & ex.Message
                End Try
                If ct.IsCancellationRequested OrElse _fatal <> 0 Then Return
                SyncLock _gate
                    If _disconnectedAtMs = 0 Then _disconnectedAtMs = UtcMs()
                End SyncLock
                Console.Error.WriteLine("[" & NowUtc() & "] ws dropped" & If(failure Is Nothing, "", ": " & failure) &
                                        " — reconnecting in " & backoff & "s (will re-authenticate)")
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(backoff), ct)
                Catch ex As OperationCanceledException
                    Return
                End Try
                backoff = Math.Min(backoff * 2, 60)
            End While
        End Function

        ''' <summary>Builds an auth request. The returned text carries the secret: send it, NEVER log it.</summary>
        Private Function AuthRequest(id As Integer, grant As String) As String
            Dim sb As New StringBuilder()
            sb.Append("{""jsonrpc"":""2.0"",""id"":").Append(id).Append(",""method"":""public/auth"",""params"":{")
            sb.Append("""grant_type"":").Append(Q(grant))
            If grant = "client_credentials" Then
                sb.Append(",""client_id"":").Append(Q(_clientId))
                sb.Append(",""client_secret"":").Append(Q(_clientSecret))
            Else
                sb.Append(",""refresh_token"":").Append(Q(_refreshToken))
            End If
            sb.Append("}}")
            Return sb.ToString()
        End Function

        Private Async Function ReceiveLoopAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
            Dim buffer(65535) As Byte
            Dim sb As New StringBuilder()
            While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
                Dim res As WebSocketReceiveResult = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), ct)
                If res.MessageType = WebSocketMessageType.Close Then Exit While
                sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count))
                If Not res.EndOfMessage Then Continue While
                Dim msg As String = sb.ToString()
                sb.Clear()
                Await HandleWsAsync(ws, msg, ct)

                ' Token refresh at 80 % of expires_in. Heartbeats arrive every HeartbeatSec, so this runs
                ' at least that often.
                If _authOk Then
                    If _refreshId = 0 AndAlso DateTime.UtcNow >= _refreshDueUtc Then
                        _refreshId = NextId()
                        _refreshSentUtc = DateTime.UtcNow
                        Await SendAsync(ws, AuthRequest(_refreshId, "refresh_token"), ct)
                    ElseIf _refreshId <> 0 AndAlso (DateTime.UtcNow - _refreshSentUtc).TotalSeconds > 60 Then
                        Throw New InvalidOperationException("refresh got no answer in 60 s")
                    End If
                End If
            End While
        End Function

        Private Async Function HandleWsAsync(ws As ClientWebSocket, msg As String, ct As CancellationToken) As Task
            Dim root As JsonElement
            Try
                Using doc As JsonDocument = JsonDocument.Parse(msg)
                    root = doc.RootElement.Clone()
                End Using
            Catch
                Return
            End Try

            Dim methodEl As JsonElement = Nothing
            If Not root.TryGetProperty("method", methodEl) Then
                Await HandleResponseAsync(ws, root, msg, ct)
                Return
            End If
            Dim m As String = If(methodEl.GetString(), "")

            If m = "heartbeat" Then
                Dim p As JsonElement = Nothing
                If root.TryGetProperty("params", p) Then
                    Dim t As JsonElement = Nothing
                    If p.TryGetProperty("type", t) AndAlso t.GetString() = "test_request" Then
                        Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() &
                                            ",""method"":""public/test"",""params"":{}}", ct)
                    End If
                End If
                Return
            End If

            If m <> "subscription" Then Return
            Dim prm As JsonElement = Nothing
            If Not root.TryGetProperty("params", prm) Then Return
            Dim chanEl As JsonElement = Nothing
            Dim chanName As String = "?"
            If prm.TryGetProperty("channel", chanEl) Then chanName = If(chanEl.GetString(), "?")
            Dim ci As Integer = ChanIndexOf(chanName)
            If ci < 0 Then Return
            Dim data As JsonElement = Nothing
            If Not prm.TryGetProperty("data", data) Then Return
            If data.ValueKind <> JsonValueKind.Array Then Return

            Dim recvUtc As DateTime = DateTime.UtcNow
            SyncLock _gate
                _batches += 1
                For Each t As JsonElement In data.EnumerateArray()
                    If t.ValueKind = JsonValueKind.Object Then IngestTrade_Locked(ci, t, recvUtc)
                Next
                EvictForCap_Locked()
            End SyncLock
        End Function

        ''' <summary>Auth, refresh and subscribe responses. Credential-bearing frames NEVER reach a log.</summary>
        Private Async Function HandleResponseAsync(ws As ClientWebSocket, root As JsonElement, msg As String, ct As CancellationToken) As Task
            Dim id As Integer = -1
            Dim idEl As JsonElement = Nothing
            If root.TryGetProperty("id", idEl) AndAlso idEl.ValueKind = JsonValueKind.Number Then
                If Not idEl.TryGetInt32(id) Then id = -1
            End If

            ' 1. auth / refresh — handled FIRST and only ever summarised.
            If id <> -1 AndAlso (id = _authId OrElse id = _refreshId) Then
                Dim initial As Boolean = (id = _authId)
                Dim what As String = If(initial, "auth", "refresh")
                Dim errEl As JsonElement = Nothing
                If root.TryGetProperty("error", errEl) Then
                    Console.Error.WriteLine("[" & NowUtc() & "] " & what & " FAILED: " & ErrText(errEl))
                    If initial Then
                        _authFailures += 1
                        If _authFailures >= 3 Then
                            _fatal = ExitAuthFailed
                            Console.Error.WriteLine("[" & NowUtc() & "] STOP: 3 consecutive auth failures — the key is wrong, revoked or the venue is refusing it. Check the key; do not retry blindly.")
                            Try
                                _collectCts.Cancel()
                            Catch
                            End Try
                        End If
                    End If
                    Throw New InvalidOperationException(what & " failed")   ' reconnect and re-authenticate
                End If
                Dim resEl As JsonElement = Nothing
                Dim expiresIn As Integer = 0
                Dim rt As String = ""
                If root.TryGetProperty("result", resEl) AndAlso resEl.ValueKind = JsonValueKind.Object Then
                    Dim v As JsonElement = Nothing
                    If resEl.TryGetProperty("expires_in", v) AndAlso v.ValueKind = JsonValueKind.Number Then
                        If Not v.TryGetInt32(expiresIn) Then expiresIn = 0
                    End If
                    If resEl.TryGetProperty("refresh_token", v) AndAlso v.ValueKind = JsonValueKind.String Then rt = If(v.GetString(), "")
                End If
                If expiresIn <= 0 OrElse rt.Length = 0 Then
                    Console.Error.WriteLine("[" & NowUtc() & "] " & what & " FAILED: the response carried no usable expires_in / refresh grant")
                    Throw New InvalidOperationException(what & " response unusable")
                End If
                _refreshToken = rt
                Dim refreshInSec As Double = expiresIn * RefreshFraction
                If _refreshCapSec > 0 Then refreshInSec = Math.Min(refreshInSec, _refreshCapSec)
                _refreshDueUtc = DateTime.UtcNow.AddSeconds(refreshInSec)
                _refreshId = 0
                If initial Then
                    _authOk = True
                    _authFailures = 0
                    _authOks += 1
                    Console.WriteLine("[" & NowUtc() & "] auth OK, expires_in=" & expiresIn)
                    ' Authenticated: NOW subscribe, one request per channel.
                    For i As Integer = 0 To 2
                        Await SendSubscribeAsync(ws, i, "public/subscribe", ct)
                    Next
                Else
                    _refreshOks += 1
                    Console.WriteLine("[" & NowUtc() & "] refresh OK, expires_in=" & expiresIn)
                End If
                Return
            End If

            ' 2. subscribe responses
            Dim ci As Integer = -1
            SyncLock _gate
                If id <> -1 AndAlso Not _subIds.TryGetValue(id, ci) Then ci = -1
            End SyncLock
            If ci >= 0 Then
                Dim errEl As JsonElement = Nothing
                If root.TryGetProperty("error", errEl) Then
                    Dim et As String = ErrText(errEl)
                    _ch(ci).SubState = "REFUSED — " & et
                    Console.Error.WriteLine("[" & NowUtc() & "] subscribe REFUSED for " & ChanNames(ci) & ": " & et)
                    If ci = RawIdx Then
                        If Not _rawTriedPrivate Then
                            _rawTriedPrivate = True
                            Console.Error.WriteLine("[" & NowUtc() & "] raw refused on public/subscribe; retrying ONCE on private/subscribe")
                            Await SendSubscribeAsync(ws, RawIdx, "private/subscribe", ct)
                        Else
                            _fatal = ExitRawRefused
                            Console.Error.WriteLine("[" & NowUtc() & "] ################################################################")
                            Console.Error.WriteLine("[" & NowUtc() & "] RAW ARM STOPPED: the venue refuses " & ChanNames(RawIdx) & " even after auth. Last error: " & et)
                            Console.Error.WriteLine("[" & NowUtc() & "] The run cannot answer its question; stopping instead of collecting the other two arms in silence.")
                            Console.Error.WriteLine("[" & NowUtc() & "] ################################################################")
                            Try
                                _collectCts.Cancel()
                            Catch
                            End Try
                        End If
                    End If
                Else
                    _ch(ci).SubState = "ACCEPTED"
                    Console.WriteLine("[" & NowUtc() & "] subscribe ACCEPTED for " & ChanNames(ci))
                    SyncLock _gate
                        _subsAcceptedThisConn += 1
                        If _subsAcceptedThisConn = 3 Then AllSubscribed_Locked()
                    End SyncLock
                End If
                Return
            End If

            ' 3. anything else: log it, but NEVER a frame that mentions a credential field.
            If msg.Contains("access_token") OrElse msg.Contains("refresh_token") OrElse msg.Contains("client_secret") Then
                Console.WriteLine("[" & NowUtc() & "] ws control: <frame withheld — it names a credential field>")
            ElseIf msg.Contains("""error""") Then
                Console.Error.WriteLine("[" & NowUtc() & "] ws control: " & If(msg.Length > 600, msg.Substring(0, 600) & "…", msg))
            End If
        End Function

        Private Async Function SendSubscribeAsync(ws As ClientWebSocket, ci As Integer, method As String, ct As CancellationToken) As Task
            Dim subId As Integer = NextId()
            SyncLock _gate
                _subIds(subId) = ci
            End SyncLock
            Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & subId & ",""method"":""" & method &
                                """,""params"":{""channels"":[""" & ChanNames(ci) & """]}}", ct)
        End Function

        ''' <summary>All three subscriptions are live on this connection. Caller holds _gate.</summary>
        Private Sub AllSubscribed_Locked()
            Dim nowMs As Long = UtcMs()
            Console.WriteLine("[" & NowUtc() & "] all three channels subscribed; collecting…")
            If _disconnectedAtMs > 0 Then
                _outages.Add(New Long() {_disconnectedAtMs, nowMs})
                Console.WriteLine("[" & NowUtc() & "] outage recorded: " & FromMs(_disconnectedAtMs).ToString("HH:mm:ss", CultureInfo.InvariantCulture) &
                                  " -> " & FromMs(nowMs).ToString("HH:mm:ss", CultureInfo.InvariantCulture) & " UTC (history-flagged trades inside it are CENSORED, not misses)")
                _disconnectedAtMs = 0
            End If
            ' The judge starts 30 s after the first time every channel was live, so the subscribe
            ' skew cannot read as a "never delivered" trade.
            If _judgedUpToTs = 0 Then _judgedUpToTs = nowMs + 30000L
        End Sub

        ' ── ingest ───────────────────────────────────────────────────────────────────────
        Private Sub IngestTrade_Locked(ci As Integer, t As JsonElement, recvUtc As DateTime)
            Dim st As ChanStats = _ch(ci)
            st.Objects += 1
            Dim tradeId As String = RawString(t, "trade_id")
            Dim venueTs As Long = RawLong(t, "timestamp")
            Dim seq As Long = RawLong(t, "trade_seq")

            Dim liqPresent As Boolean = False
            Dim liqVal As String = Nothing
            Dim lv As JsonElement = Nothing
            If t.TryGetProperty("liquidation", lv) Then
                liqPresent = True
                liqVal = If(lv.ValueKind = JsonValueKind.String, If(lv.GetString(), ""), lv.GetRawText())
            End If
            Dim flagged As Boolean = liqPresent AndAlso Not IsNone(liqVal)
            Dim shape As Integer = TallyFields(st, t)

            Dim delay As Long = 0
            If venueTs > 0 Then delay = (recvUtc.Ticks - FromMs(venueTs).Ticks) \ TimeSpan.TicksPerMillisecond

            If tradeId.Length = 0 Then Return
            Dim rec As FirstRec = Nothing
            If Not _index.TryGetValue(tradeId, rec) Then
                rec = New FirstRec() With {.TradeId = tradeId, .VenueTs = venueTs, .Seq = seq, .FirstAnyTicks = recvUtc.Ticks}
                _index(tradeId) = rec
                _order.Enqueue(rec)
            End If

            If Not rec.Slots(ci).Delivered Then
                rec.Slots(ci).Delivered = True
                rec.Slots(ci).RecvTicks = recvUtc.Ticks
                rec.Slots(ci).DelayMs = CInt(Math.Max(Math.Min(delay, Integer.MaxValue), Integer.MinValue))
                rec.Slots(ci).LiqPresent = liqPresent
                rec.Slots(ci).LiqFlagged = flagged
                rec.Slots(ci).LiqValue = If(liqVal Is Nothing, Nothing, String.Intern(liqVal))
                rec.Slots(ci).Shape = shape
                If flagged Then rec.Slots(ci).RawIfFlagged = t.GetRawText()
                st.Delivered += 1
                If liqPresent Then st.LiqFieldPresent += 1
                If flagged Then st.LiqFlaggedAtDelivery += 1
                st.DelaySumMs += delay
                st.DelayN += 1
                If delay > st.DelayMaxMs Then st.DelayMaxMs = delay
                WriteSample_Locked(ci, recvUtc, t, flagged)
            ElseIf flagged AndAlso Not rec.Slots(ci).LiqFlagged AndAlso Not rec.Slots(ci).LaterFlagSeen Then
                rec.Slots(ci).LaterFlagSeen = True
                st.LaterFlag += 1
                Console.WriteLine("[" & NowUtc() & "] " & st.Label & ": a LATER delivery of trade_id=" & tradeId &
                                  " carried liquidation=" & liqVal & " (the first delivery did not)")
            End If
        End Sub

        Private Function TallyFields(st As ChanStats, t As JsonElement) As Integer
            Dim sb As New StringBuilder()
            For Each pr As JsonProperty In t.EnumerateObject()
                Dim c As Long = 0
                st.Fields.TryGetValue(pr.Name, c)
                st.Fields(pr.Name) = c + 1
                If sb.Length > 0 Then sb.Append(","c)
                sb.Append(pr.Name)
                If pr.Name = "liquidation" Then
                    Dim v As String = If(pr.Value.ValueKind = JsonValueKind.String, If(pr.Value.GetString(), ""), pr.Value.GetRawText())
                    Dim c2 As Long = 0
                    st.LiqValues.TryGetValue(v, c2)
                    st.LiqValues(v) = c2 + 1
                End If
            Next
            Dim sig As String = sb.ToString()
            Dim id As Integer = 0
            If st.ShapeIds.TryGetValue(sig, id) Then
                st.ShapeN(id) += 1
                Return id
            End If
            If st.Shapes.Count >= 64 Then Return -1
            id = st.Shapes.Count
            st.Shapes.Add(sig)
            st.ShapeN.Add(1)
            st.ShapeIds(sig) = id
            Return id
        End Function

        ''' <summary>The first few objects per channel, and the first few flagged ones, verbatim — for reading the field list by eye.</summary>
        Private Sub WriteSample_Locked(ci As Integer, recvUtc As DateTime, t As JsonElement, flagged As Boolean)
            If _sampleOut Is Nothing Then Return
            Dim kind As String = Nothing
            If flagged AndAlso _flagSamples(ci) < 5 Then
                _flagSamples(ci) += 1
                kind = "flagged"
            ElseIf _samples(ci) < 5 Then
                _samples(ci) += 1
                kind = "first"
            End If
            If kind Is Nothing Then Return
            _sampleOut.WriteLine("{""recv_utc"":""" & recvUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) &
                                 """,""channel"":""" & ChanLabels(ci) & """,""kind"":""" & kind & """,""raw"":" & t.GetRawText() & "}")
            _sampleOut.Flush()
        End Sub

        Private Sub EvictForCap_Locked()
            While _order.Count > 0 AndAlso _order.Count * EntryBytesEstimate > IndexCapBytes
                Dim h As FirstRec = _order.Dequeue()
                _index.Remove(h.TradeId)
                _evictedForCap += 1
                If _evictedForCap = 1 Then
                    Console.Error.WriteLine("[" & NowUtc() & "] INDEX CAP REACHED — unjudged first-delivery entries are now being dropped; " &
                                            "the judge will read those trades as never delivered. The result is degraded from here.")
                End If
            End While
        End Sub

        ''' <summary>Drop judged entries (venue time before the judged watermark) and expired ones. Caller holds _gate.</summary>
        Private Sub EvictJudged_Locked()
            Dim nowTicks As Long = DateTime.UtcNow.Ticks
            Dim expireTicks As Long = ExpireMin * TimeSpan.TicksPerMinute
            While _order.Count > 0
                Dim h As FirstRec = _order.Peek()
                Dim judged As Boolean = (_judgedUpToTs > 0 AndAlso h.VenueTs < _judgedUpToTs)
                Dim expired As Boolean = (nowTicks - h.FirstAnyTicks) > expireTicks
                If Not judged AndAlso Not expired Then Exit While
                _order.Dequeue()
                _index.Remove(h.TradeId)
                If expired AndAlso Not judged Then _expiredUnjudged += 1
            End While
        End Sub

        ' ── the delayed judge ────────────────────────────────────────────────────────────
        Private Async Function JudgeLoopAsync(runCt As CancellationToken) As Task
            ' The history host is public. This client carries NO credentials.
            Using http As New HttpClient(New HttpClientHandler() With {.UseProxy = False})
                http.Timeout = TimeSpan.FromSeconds(30)
                Dim nextRun As DateTime = DateTime.UtcNow.AddSeconds(JudgeEverySec)
                While Not runCt.IsCancellationRequested
                    Try
                        Await Task.Delay(TimeSpan.FromSeconds(10), runCt)
                    Catch ex As OperationCanceledException
                        Return
                    End Try
                    Dim draining As Boolean
                    SyncLock _gate
                        draining = (_collectEndMs > 0)
                    End SyncLock
                    If DateTime.UtcNow < nextRun Then Continue While
                    nextRun = DateTime.UtcNow.AddSeconds(If(draining, DrainPollSec, JudgeEverySec))
                    Dim done As Boolean = False
                    Try
                        done = Await JudgeOnceAsync(http, runCt)
                    Catch ex As OperationCanceledException When runCt.IsCancellationRequested
                        Return
                    Catch ex As Exception
                        Console.Error.WriteLine("[" & NowUtc() & "] judge error (" & ex.GetType().Name & "): " & ex.Message)
                    End Try
                    If done Then Return
                End While
            End Using
        End Function

        ''' <summary>Judges one venue-time slice. Returns True when the drain has nothing left to judge.</summary>
        Private Async Function JudgeOnceAsync(http As HttpClient, ct As CancellationToken) As Task(Of Boolean)
            Dim nowMs As Long = UtcMs()
            Dim lo As Long
            Dim hi As Long
            Dim collectEnd As Long
            SyncLock _gate
                lo = _judgedUpToTs
                collectEnd = _collectEndMs
            End SyncLock
            If lo = 0 Then Return (collectEnd > 0)         ' never fully subscribed
            hi = nowMs - CLng(_judgeMinAgeMin) * 60000L
            If collectEnd > 0 Then hi = Math.Min(hi, collectEnd)
            If collectEnd > 0 AndAlso lo >= collectEnd Then Return True
            If hi <= lo Then Return False
            If hi - lo < MinSliceMs AndAlso Not (collectEnd > 0 AndAlso hi = collectEnd) Then Return False

            If nowMs - lo > 150L * 60000L Then
                Console.Error.WriteLine("[" & NowUtc() & "] judge is lagging: the oldest unjudged trade is " & ((nowMs - lo) \ 60000L) &
                                        " min old (window 90–150 min; entries expire at " & ExpireMin & " min)")
            End If

            ' 1. the seq span of what the channels delivered in this slice
            Dim minSeq As Long = Long.MaxValue
            Dim maxSeq As Long = 0
            Dim nRecs As Integer = 0
            SyncLock _gate
                For Each r As FirstRec In _order
                    If r.VenueTs >= lo AndAlso r.VenueTs < hi AndAlso r.Seq > 0 Then
                        nRecs += 1
                        If r.Seq < minSeq Then minSeq = r.Seq
                        If r.Seq > maxSeq Then maxSeq = r.Seq
                    End If
                Next
            End SyncLock
            If nRecs = 0 Then
                SyncLock _gate
                    _judgedUpToTs = hi
                    _judgeEmptySlices += 1
                    EvictJudged_Locked()
                End SyncLock
                Console.WriteLine("[" & NowUtc() & "] judge slice " & FromMs(lo).ToString("HH:mm:ss", CultureInfo.InvariantCulture) & "–" &
                                  FromMs(hi).ToString("HH:mm:ss", CultureInfo.InvariantCulture) & " UTC: no delivered trades to anchor a seq span — slice CENSORED")
                Return (collectEnd > 0 AndAlso hi >= collectEnd)
            End If

            ' 2. the history host, by trade_seq
            Dim hist As Dictionary(Of Long, HistTrade) = Await FetchSeqRangeAsync(http, minSeq, maxSeq, ct)
            If hist Is Nothing Then
                _judgeFetchFails += 1
                Console.Error.WriteLine("[" & NowUtc() & "] judge: history fetch failed for the slice; it will be retried")
                Return False
            End If

            ' 3. compare
            Dim sliceJudged As Integer = 0
            Dim sliceFlagged As Integer = 0
            Dim carried As Integer() = {0, 0, 0}
            SyncLock _gate
                _judgeRuns += 1
                Dim judgeNowUtc As String = NowUtc()
                For Each h As HistTrade In hist.Values
                    If h.Ts < lo OrElse h.Ts >= hi Then Continue For
                    sliceJudged += 1
                    _histTradesJudged += 1
                    Dim rec As FirstRec = Nothing
                    _index.TryGetValue(h.TradeId, rec)
                    Dim histFlag As Boolean = Not IsNone(h.Liq)

                    If histFlag Then
                        sliceFlagged += 1
                        _histFlagged += 1
                        Dim kind As String
                        If rec Is Nothing Then
                            If InOutage_Locked(h.Ts) Then
                                _histFlaggedCensored += 1
                                kind = "history_flagged_in_outage"
                            Else
                                _histFlaggedNoIndex += 1
                                For i As Integer = 0 To 2
                                    _ch(i).NeverDelivered += 1
                                Next
                                kind = "history_flagged_no_channel_delivered"
                            End If
                        Else
                            kind = "history_flagged"
                            For i As Integer = 0 To 2
                                If Not rec.Slots(i).Delivered Then
                                    _ch(i).NeverDelivered += 1
                                ElseIf rec.Slots(i).LiqFlagged Then
                                    _ch(i).Carried += 1
                                    carried(i) += 1
                                Else
                                    _ch(i).NotCarried += 1
                                End If
                            Next
                        End If
                        WriteFlagDetail_Locked(kind, h, rec, judgeNowUtc)
                    ElseIf rec IsNot Nothing Then
                        For i As Integer = 0 To 2
                            If rec.Slots(i).LiqFlagged Then
                                _ch(i).CarriedButHistNone += 1
                                WriteFlagDetail_Locked("delivered_flag_history_none", h, rec, judgeNowUtc)
                            End If
                        Next
                    End If
                Next
                ' delivered trades the history host does not know (should be 0)
                For Each r As FirstRec In _order
                    If r.VenueTs >= lo AndAlso r.VenueTs < hi AndAlso r.Seq > 0 AndAlso Not hist.ContainsKey(r.Seq) Then
                        _recsNotInHistory += 1
                        If _recsNotInHistory <= 5 Then
                            Console.Error.WriteLine("[" & NowUtc() & "] delivered trade_id=" & r.TradeId & " seq=" & r.Seq & " is NOT on the history host")
                        End If
                    End If
                Next
                _judgedUpToTs = hi
                EvictJudged_Locked()
            End SyncLock
            Console.WriteLine("[" & NowUtc() & "] judged slice " & FromMs(lo).ToString("HH:mm:ss", CultureInfo.InvariantCulture) & "–" &
                              FromMs(hi).ToString("HH:mm:ss", CultureInfo.InvariantCulture) & " UTC: history trades " & sliceJudged &
                              ", delivered recs " & nRecs & ", history-flagged " & sliceFlagged &
                              " (carried at delivery: raw " & carried(RawIdx) & ", 100ms " & carried(0) & ", agg2 " & carried(1) & ")")
            Return (collectEnd > 0 AndAlso hi >= collectEnd)
        End Function

        ''' <summary>Pages the history host by trade_seq. The host widens a range to whole ms, so pages overlap: de-duplicate on trade_seq.</summary>
        Private Async Function FetchSeqRangeAsync(http As HttpClient, lo As Long, hi As Long, ct As CancellationToken) As Task(Of Dictionary(Of Long, HistTrade))
            Dim result As New Dictionary(Of Long, HistTrade)()
            Dim s As Long = lo
            While s <= hi
                Dim e As Long = Math.Min(s + HistStepSeq - 1, hi)
                Dim stack As New Stack(Of Long())()
                stack.Push(New Long() {s, e})
                While stack.Count > 0
                    Dim rg As Long() = stack.Pop()
                    Dim url As String = HistUrl & "?instrument_name=" & Instrument &
                                        "&start_seq=" & rg(0).ToString(CultureInfo.InvariantCulture) &
                                        "&end_seq=" & rg(1).ToString(CultureInfo.InvariantCulture) &
                                        "&count=1000&sorting=asc"
                    Dim body As String = Await HistGetAsync(http, url, ct)
                    If body Is Nothing Then Return Nothing
                    Dim trades As New List(Of HistTrade)()
                    Dim hasMore As Boolean = False
                    Try
                        Using doc As JsonDocument = JsonDocument.Parse(body)
                            Dim resEl As JsonElement = Nothing
                            If Not doc.RootElement.TryGetProperty("result", resEl) Then Return Nothing
                            Dim hm As JsonElement = Nothing
                            If resEl.TryGetProperty("has_more", hm) AndAlso hm.ValueKind = JsonValueKind.True Then hasMore = True
                            Dim arr As JsonElement = Nothing
                            If Not resEl.TryGetProperty("trades", arr) OrElse arr.ValueKind <> JsonValueKind.Array Then Return Nothing
                            For Each t As JsonElement In arr.EnumerateArray()
                                If t.ValueKind <> JsonValueKind.Object Then Continue For
                                Dim ht As New HistTrade() With {
                                    .Seq = RawLong(t, "trade_seq"), .TradeId = RawString(t, "trade_id"),
                                    .Ts = RawLong(t, "timestamp")}
                                Dim lv As JsonElement = Nothing
                                If t.TryGetProperty("liquidation", lv) Then
                                    ht.Liq = If(lv.ValueKind = JsonValueKind.String, If(lv.GetString(), ""), lv.GetRawText())
                                End If
                                If Not IsNone(ht.Liq) Then ht.Raw = t.GetRawText()
                                SyncLock _gate
                                    For Each pr As JsonProperty In t.EnumerateObject()
                                        Dim c As Long = 0
                                        _histFields.TryGetValue(pr.Name, c)
                                        _histFields(pr.Name) = c + 1
                                    Next
                                End SyncLock
                                trades.Add(ht)
                            Next
                        End Using
                    Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                        Console.Error.WriteLine("[" & NowUtc() & "] history parse error: " & ex.Message)
                        Return Nothing
                    End Try
                    If hasMore AndAlso rg(1) > rg(0) Then
                        _histSplits += 1
                        Dim mid As Long = (rg(0) + rg(1)) \ 2L
                        stack.Push(New Long() {rg(0), mid})
                        stack.Push(New Long() {mid + 1L, rg(1)})
                        Continue While
                    End If
                    For Each ht As HistTrade In trades
                        result(ht.Seq) = ht                       ' de-duplicate on trade_seq
                    Next
                    Await Task.Delay(HistPauseMs, ct)
                End While
                s = e + 1L
            End While
            Return result
        End Function

        Private Async Function HistGetAsync(http As HttpClient, url As String, ct As CancellationToken) As Task(Of String)
            For attempt As Integer = 1 To 5
                Dim body As String = Nothing
                Dim err As String = Nothing
                _histRequests += 1
                Try
                    body = Await http.GetStringAsync(url, ct)
                Catch ex As OperationCanceledException When ct.IsCancellationRequested
                    Throw
                Catch ex As Exception
                    ' an HttpClient timeout is also an OperationCanceledException — retry it, do not end
                    err = ex.GetType().Name & ": " & ex.Message
                End Try
                If body IsNot Nothing Then Return body
                _histErrors += 1
                Console.Error.WriteLine("[" & NowUtc() & "] history request error (" & err & "); retry " & attempt)
                Await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct)
            Next
            Return Nothing
        End Function

        Private Function InOutage_Locked(ts As Long) As Boolean
            For Each o As Long() In _outages
                If ts >= o(0) - OutagePadMs AndAlso ts <= o(1) + OutagePadMs Then Return True
            Next
            Return False
        End Function

        Private Sub WriteFlagDetail_Locked(kind As String, h As HistTrade, rec As FirstRec, judgeNowUtc As String)
            If _flagOut Is Nothing Then Return
            Dim sb As New StringBuilder()
            sb.Append("{""kind"":").Append(Q(kind))
            sb.Append(",""judged_utc"":").Append(Q(judgeNowUtc))
            sb.Append(",""trade_id"":").Append(Q(h.TradeId))
            sb.Append(",""trade_seq"":").Append(h.Seq.ToString(CultureInfo.InvariantCulture))
            sb.Append(",""venue_ts"":").Append(h.Ts.ToString(CultureInfo.InvariantCulture))
            sb.Append(",""venue_utc"":").Append(Q(FromMs(h.Ts).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)))
            sb.Append(",""age_at_judge_min"":").Append(((UtcMs() - h.Ts) \ 60000L).ToString(CultureInfo.InvariantCulture))
            sb.Append(",""history_liquidation"":").Append(Q(h.Liq))
            sb.Append(",""history_raw"":").Append(If(h.Raw.Length > 0, h.Raw, "null"))
            sb.Append(",""channels"":{")
            For i As Integer = 0 To 2
                If i > 0 Then sb.Append(","c)
                sb.Append(Q(ChanLabels(i))).Append(":")
                If rec Is Nothing OrElse Not rec.Slots(i).Delivered Then
                    sb.Append("{""delivered"":false}")
                Else
                    Dim sl As ChanSlot = rec.Slots(i)
                    sb.Append("{""delivered"":true,""recv_utc"":").Append(Q(New DateTime(sl.RecvTicks, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)))
                    sb.Append(",""delivery_delay_ms"":").Append(sl.DelayMs.ToString(CultureInfo.InvariantCulture))
                    sb.Append(",""liquidation_present"":").Append(If(sl.LiqPresent, "true", "false"))
                    sb.Append(",""liquidation_value"":").Append(If(sl.LiqValue Is Nothing, "null", Q(sl.LiqValue)))
                    sb.Append(",""carried_flag_at_delivery"":").Append(If(sl.LiqFlagged, "true", "false"))
                    sb.Append(",""later_delivery_flagged"":").Append(If(sl.LaterFlagSeen, "true", "false"))
                    sb.Append(",""fields"":").Append(Q(If(sl.Shape >= 0 AndAlso sl.Shape < _ch(i).Shapes.Count, _ch(i).Shapes(sl.Shape), "?")))
                    sb.Append(",""raw_at_delivery"":").Append(If(sl.RawIfFlagged Is Nothing, "null", sl.RawIfFlagged))
                    sb.Append("}")
                End If
            Next
            sb.Append("}}")
            _flagOut.WriteLine(sb.ToString())
            _flagOut.Flush()
        End Sub

        ' ── status + report ──────────────────────────────────────────────────────────────
        Private Async Function StatusLoopAsync(collectCts As CancellationTokenSource, runCts As CancellationTokenSource, runCt As CancellationToken) As Task
            Dim tick As Integer = 0
            While Not runCt.IsCancellationRequested
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(StatusTickSec), runCt)
                Catch ex As OperationCanceledException
                    Return
                End Try
                ' A detached run has no console to Ctrl+C: drop a file in the run folder.
                Try
                    If File.Exists("STOPNOW") Then
                        Console.WriteLine("[" & NowUtc() & "] STOPNOW file seen — skipping the drain")
                        collectCts.Cancel()
                        runCts.Cancel()
                        Return
                    End If
                    If File.Exists("STOP") AndAlso Not collectCts.IsCancellationRequested Then
                        Console.WriteLine("[" & NowUtc() & "] STOP file seen — ending collection; the drain finishes the judging")
                        collectCts.Cancel()
                    End If
                Catch
                End Try
                tick += 1
                SyncLock _gate
                    EvictJudged_Locked()
                    If _flagOut IsNot Nothing Then _flagOut.Flush()
                End SyncLock
                If tick Mod StatusEveryTicks <> 0 Then Continue While
                PrintStatus()
            End While
        End Function

        Private Sub PrintStatus()
            SyncLock _gate
                Dim sb As New StringBuilder()
                sb.Append("[").Append(NowUtc()).Append("] up ").Append(CInt((DateTime.UtcNow - _startUtc).TotalMinutes)).Append("m")
                sb.Append(" | delivered 100ms ").Append(_ch(0).Delivered).Append(" agg2 ").Append(_ch(1).Delivered).Append(" raw ").Append(_ch(2).Delivered)
                sb.Append(" | liq@delivery 100ms ").Append(_ch(0).LiqFlaggedAtDelivery).Append(" agg2 ").Append(_ch(1).LiqFlaggedAtDelivery).Append(" raw ").Append(_ch(2).LiqFlaggedAtDelivery)
                sb.Append(" | judged ").Append(_histTradesJudged).Append(" histFlagged ").Append(_histFlagged)
                sb.Append(" | carried raw ").Append(_ch(2).Carried).Append(" 100ms ").Append(_ch(0).Carried).Append(" agg2 ").Append(_ch(1).Carried)
                sb.Append(" | neverDelivered raw ").Append(_ch(2).NeverDelivered)
                sb.Append(" | idx ").Append(_index.Count).Append(" (~").Append((_index.Count * EntryBytesEstimate) \ (1024L * 1024L)).Append(" MB est)")
                sb.Append(" priv ").Append(CLng(Process.GetCurrentProcess().PrivateMemorySize64 \ (1024L * 1024L))).Append(" MB")
                sb.Append(" | auth ").Append(_authOks).Append(" refresh ").Append(_refreshOks).Append(" reconn ").Append(Math.Max(0, _reconnectGen - 1))
                sb.Append(" | hist req ").Append(_histRequests).Append(" err ").Append(_histErrors)
                Console.WriteLine(sb.ToString())
            End SyncLock
        End Sub

        Private Sub Report()
            SyncLock _gate
                Console.WriteLine()
                Console.WriteLine("== RUN SUMMARY — RawChannelProbe (HH-2) ==")
                Console.WriteLine("  window UTC        : " & _startUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & "  ->  " & NowUtc())
                If _collectEndMs > 0 Then
                    Console.WriteLine("  collection ended  : " & FromMs(_collectEndMs).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & " UTC")
                End If
                If _judgedUpToTs > 0 Then
                    Console.WriteLine("  judged up to      : " & FromMs(_judgedUpToTs).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & " UTC" &
                                      If(_collectEndMs > 0 AndAlso _judgedUpToTs < _collectEndMs, "   ⚠ trades after this were NOT judged (censored tail)", ""))
                End If
                Console.WriteLine("  ws batches        : " & _batches & "   reconnects: " & Math.Max(0, _reconnectGen - 1) & "   outages recorded: " & _outages.Count)
                Console.WriteLine("  auth OK / refresh : " & _authOks & " / " & _refreshOks)
                Console.WriteLine("  fatal code        : " & _fatal & If(_fatal = 0, "", "   (3 = no credentials, 4 = auth failed, 5 = raw refused after auth)"))
                Console.WriteLine()
                Console.WriteLine("== PER CHANNEL — delivery ==")
                For i As Integer = 0 To 2
                    Dim st As ChanStats = _ch(i)
                    Console.WriteLine("  " & st.Name)
                    Console.WriteLine("      subscription                 : " & st.SubState)
                    Console.WriteLine("      trade objects / distinct ids : " & st.Objects & " / " & st.Delivered)
                    Console.WriteLine("      `liquidation` property present at first delivery : " & st.LiqFieldPresent)
                    Console.WriteLine("      flagged (value != none) at first delivery        : " & st.LiqFlaggedAtDelivery)
                    Console.WriteLine("      receive delay ms (recv - venue ts; includes dev-machine clock skew): mean " &
                                      If(st.DelayN > 0, (st.DelaySumMs / st.DelayN).ToString("F0", CultureInfo.InvariantCulture), "n/a") & "  max " & st.DelayMaxMs)
                Next
                Console.WriteLine()
                Console.WriteLine("== JUDGE (history host, by trade_seq) ==")
                Console.WriteLine("  judge runs                         : " & _judgeRuns & "   empty slices: " & _judgeEmptySlices & "   fetch failures: " & _judgeFetchFails)
                Console.WriteLine("  history requests / errors / splits : " & _histRequests & " / " & _histErrors & " / " & _histSplits)
                Console.WriteLine("  history trades judged              : " & _histTradesJudged)
                Console.WriteLine("  delivered trades absent on history : " & _recsNotInHistory & "   (expected 0)")
                Console.WriteLine("  unjudged entries expired / cap-evicted : " & _expiredUnjudged & " / " & _evictedForCap & "   (expected 0 / 0)")
                Console.WriteLine("  HISTORY-FLAGGED trades judged      : " & _histFlagged)
                Console.WriteLine("     of which inside a ws outage (censored)       : " & _histFlaggedCensored)
                Console.WriteLine("     of which no channel delivered it (not in outage): " & _histFlaggedNoIndex)
                Console.WriteLine()
                Console.WriteLine("  per channel, over the history-flagged trades:")
                Console.WriteLine("      channel   carried-at-delivery   delivered-without-flag   never-delivered   later-delivery-flagged   flagged-but-history-none")
                For i As Integer = 0 To 2
                    Dim st As ChanStats = _ch(i)
                    Console.WriteLine("      " & st.Label.PadRight(8) & "  " & st.Carried.ToString().PadLeft(18) & "   " & st.NotCarried.ToString().PadLeft(22) &
                                      "   " & st.NeverDelivered.ToString().PadLeft(15) & "   " & st.LaterFlag.ToString().PadLeft(22) & "   " & st.CarriedButHistNone.ToString().PadLeft(24))
                Next
                Console.WriteLine()
                If _histFlagged = 0 Then
                    Console.WriteLine("  => RESULT: INCONCLUSIVE. No history-flagged trade was judged in this window. Extend the run; do not conclude.")
                Else
                    Dim r As ChanStats = _ch(RawIdx)
                    Console.WriteLine("  => RESULT: raw carried the flag at first delivery on " & r.Carried & " of " & _histFlagged &
                                      " history-flagged trades (" & r.NotCarried & " delivered without it, " & r.NeverDelivered & " never delivered, " &
                                      _histFlaggedCensored & " censored by an outage).")
                    If _histFlagged < 5 Then Console.WriteLine("     ⚠ n = " & _histFlagged & ": liquidations cluster; this is a small sample.")
                End If
                Console.WriteLine()
                Console.WriteLine("== PROPERTY-NAME TALLY, per channel (every key ever seen; count = objects carrying it) ==")
                For i As Integer = 0 To 2
                    Dim st As ChanStats = _ch(i)
                    Console.WriteLine("  " & st.Name & "  (" & st.Objects & " objects)")
                    Dim keys As New List(Of String)(st.Fields.Keys)
                    keys.Sort(StringComparer.Ordinal)
                    For Each k In keys
                        Console.WriteLine("      " & k.PadRight(24) & " " & st.Fields(k))
                    Next
                    If keys.Count = 0 Then Console.WriteLine("      <no trade objects seen>")
                    If st.LiqValues.Count = 0 Then
                        Console.WriteLine("      `liquidation` : FIELD NEVER PRESENT on this channel")
                    Else
                        For Each kv In st.LiqValues
                            Console.WriteLine("      `liquidation` value """ & kv.Key & """ : " & kv.Value)
                        Next
                    End If
                    Console.WriteLine("      distinct property lists (shapes): " & st.Shapes.Count)
                    For s As Integer = 0 To st.Shapes.Count - 1
                        Console.WriteLine("        [" & st.ShapeN(s) & "x] " & st.Shapes(s))
                    Next
                Next
                Console.WriteLine("  history host (all judged trades)")
                Dim hkeys As New List(Of String)(_histFields.Keys)
                hkeys.Sort(StringComparer.Ordinal)
                For Each k In hkeys
                    Console.WriteLine("      " & k.PadRight(24) & " " & _histFields(k))
                Next
                Console.WriteLine()
                Console.WriteLine("== FIELD COMPARISON — property names on a channel that the history host never returned ==")
                For i As Integer = 0 To 2
                    Dim extra As New List(Of String)()
                    For Each k In _ch(i).Fields.Keys
                        If Not _histFields.ContainsKey(k) Then extra.Add(k)
                    Next
                    extra.Sort(StringComparer.Ordinal)
                    Dim missing As New List(Of String)()
                    For Each k In _histFields.Keys
                        If Not _ch(i).Fields.ContainsKey(k) Then missing.Add(k)
                    Next
                    missing.Sort(StringComparer.Ordinal)
                    Console.WriteLine("  " & ChanLabels(i).PadRight(6) & " only on channel: " & If(extra.Count = 0, "<none>", String.Join(", ", extra)) &
                                      "   |   only on history host: " & If(missing.Count = 0, "<none>", String.Join(", ", missing)))
                Next
                Console.WriteLine()
                Console.WriteLine("  per-flagged-trade detail -> " & Path.GetFullPath("rawliq_flagged_" & _runStamp & ".jsonl"))
                Console.WriteLine("  first-object samples     -> " & Path.GetFullPath("rawliq_samples_" & _runStamp & ".jsonl"))
                Console.WriteLine("  ⚠ Not judged: trades flagged by history but inside the first/last seconds of a slice edge are not searched;")
                Console.WriteLine("    a flag that arrives later than the judge's minimum age (" & _judgeMinAgeMin & " min) is not seen.")
            End SyncLock
        End Sub

        ' ── file plumbing ────────────────────────────────────────────────────────────────
        Private Sub OpenWriters()
            _flagOut = New StreamWriter("rawliq_flagged_" & _runStamp & ".jsonl", True, New UTF8Encoding(False))
            _sampleOut = New StreamWriter("rawliq_samples_" & _runStamp & ".jsonl", True, New UTF8Encoding(False))
        End Sub

        Private Sub CloseWriters()
            Try
                If _flagOut IsNot Nothing Then
                    _flagOut.Flush()
                    _flagOut.Dispose()
                    _flagOut = Nothing
                End If
            Catch
            End Try
            Try
                If _sampleOut IsNot Nothing Then
                    _sampleOut.Flush()
                    _sampleOut.Dispose()
                    _sampleOut = Nothing
                End If
            Catch
            End Try
        End Sub

        ' ── small helpers ────────────────────────────────────────────────────────────────
        Private Async Function SendAsync(ws As ClientWebSocket, json As String, ct As CancellationToken) As Task
            Dim b As Byte() = Encoding.UTF8.GetBytes(json)
            Await ws.SendAsync(New ArraySegment(Of Byte)(b), WebSocketMessageType.Text, True, ct)
        End Function

        Private Function NextId() As Integer
            _nextId += 1
            Return _nextId
        End Function

        Private Function ChanIndexOf(name As String) As Integer
            For i As Integer = 0 To 2
                If String.Equals(ChanNames(i), name, StringComparison.Ordinal) Then Return i
            Next
            Return -1
        End Function

        Private Function UtcMs() As Long
            Return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        End Function

        Private Function FromMs(ms As Long) As DateTime
            Return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime
        End Function

        Private Function NowUtc() As String
            Return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        End Function

        Private Function IsNone(s As String) As Boolean
            Return String.IsNullOrEmpty(s) OrElse String.Equals(s, "none", StringComparison.OrdinalIgnoreCase)
        End Function

        ''' <summary>A JSON string literal, quotes included.</summary>
        Private Function Q(s As String) As String
            Return """" & JsonEncodedText.Encode(If(s, "")).ToString() & """"
        End Function

        ''' <summary>The venue's error code and message only. Nothing else from the frame.</summary>
        Private Function ErrText(errEl As JsonElement) As String
            Dim code As String = "?"
            Dim message As String = "?"
            If errEl.ValueKind = JsonValueKind.Object Then
                Dim v As JsonElement = Nothing
                If errEl.TryGetProperty("code", v) Then code = v.GetRawText()
                If errEl.TryGetProperty("message", v) AndAlso v.ValueKind = JsonValueKind.String Then message = If(v.GetString(), "?")
            End If
            Return "code " & code & " " & message
        End Function

        Private Function RawString(e As JsonElement, name As String) As String
            Dim v As JsonElement = Nothing
            If Not e.TryGetProperty(name, v) Then Return ""
            If v.ValueKind = JsonValueKind.String Then Return If(v.GetString(), "")
            If v.ValueKind = JsonValueKind.Number Then Return v.GetRawText()
            Return ""
        End Function

        Private Function RawLong(e As JsonElement, name As String) As Long
            Dim v As JsonElement = Nothing
            If Not e.TryGetProperty(name, v) Then Return 0
            If v.ValueKind = JsonValueKind.Number Then
                Dim n As Long
                If v.TryGetInt64(n) Then Return n
            ElseIf v.ValueKind = JsonValueKind.String Then
                Dim n As Long
                If Long.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, n) Then Return n
            End If
            Return 0
        End Function

    End Module
End Namespace
