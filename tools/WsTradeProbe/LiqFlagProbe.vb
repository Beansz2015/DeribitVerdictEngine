' ─────────────────────────────────────────────────────────────────────────────────────────
' LiqFlagProbe — the MEASUREMENT for the liquidation-flag defect (D-4).
'
' Context: docs/engine-fix-build-spec-2026-09-21.md §4.1, and its brief
'          docs/engine-fix-session-b1-probe-brief.md. Session B1.
'
' THE QUESTION. DeribitWsFeed.vb:488 already parses `liquidation`, defaulting to "none",
' and finding L-1 measured LiqSignal NONE on 51,107 of 51,107 stored rows. The same trades
' held a second time from REST carry `T` or `M` under the same trade_id — 91 such pairs.
' So the PARSE is not the defect. Something upstream of it is.
'
' THREE ARMS, one run:
'   Arm 1  Dump the RAW JSON TEXT of every streamed trade object — never a parsed struct.
'          A parsed struct has already thrown away the field NAMES, which is the whole
'          question. Also tallies every property name seen, per channel, so the answer is
'          readable without grepping the dump.
'   Arm 2  Poll REST public/get_last_trades_by_instrument over the same window. For every
'          REST trade whose `liquidation` is not `none`, find the streamed object with the
'          same trade_id and emit BOTH raw objects side by side.
'          ⭐ This is the arm that answers D-4. Arms 1 and 3 alone can only report "no
'          liquidation seen yet", which is indistinguishable from "none happened".
'   Arm 3  Subscribe trades.BTC-PERPETUAL.raw in parallel and record whether ITS objects
'          carry the field — i.e. whether the 100ms aggregation is what drops it.
'          ⛔ MEASURED 2026-09-21 (UTC): the venue REFUSES this channel to an unauthorized
'          client — error 13778 `raw_subscriptions_not_available_for_unauthorized`. This
'          probe may not authenticate, so arm 3 AS SPECIFIED CANNOT RUN. It is still
'          subscribed every run, so the refusal is re-measured rather than assumed.
'   Arm 3b trades.BTC-PERPETUAL.agg2 — the substitute, and it is open to an unauthorized
'          client. A different aggregation of the same tape. It cannot prove what the
'          unaggregated feed carries, but it does separate "aggregation drops the field"
'          from "no trades channel ever carries it".
'
' ⛔ EACH CHANNEL IS SUBSCRIBED IN ITS OWN REQUEST. A single request naming several channels
' is rejected WHOLE when any one of them is refused, so arm 3 silently killed arms 1 and 2
' on the first smoke run — 90 seconds of live market, zero trades, no error printed. Every
' control frame is now logged and the run summary states each channel's subscription state.
'
' ABSENCE IS NOT DECLARED EAGERLY. A REST-flagged trade missing from the index is held
' PENDING until the stream has demonstrably moved past it (a streamed trade stamped later
' than the REST trade, plus a wall-clock grace). A pending window that spans a WebSocket
' reconnect is reported as UNTRUSTED rather than as a missing trade.
'
' SAFETY. Standalone by construction, exactly as WsTradeProbeProgram is: links nothing from
' the app, reads no settings, opens no authenticated call, places no order, and writes only
' new files in the working directory. It cannot touch collector state, analysis_log.csv or
' the trade store. Public channels and public REST only.
' ⛔ Dev machine only. Never the collector box.
' ─────────────────────────────────────────────────────────────────────────────────────────
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Net.Http
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Namespace Global.DeribitVerdictEngine

    ''' <summary>One streamed trade object, kept as the RAW TEXT the venue sent.</summary>
    Friend Class StreamRec
        Public Channel As String = ""
        Public RecvUtc As DateTime
        Public VenueTs As Long
        Public Raw As String = ""
        Public ReconnectGen As Integer
    End Class

    ''' <summary>A REST trade flagged as a liquidation, waiting for its streamed twin.</summary>
    Friend Class PendingLiq
        Public TradeId As String = ""
        Public VenueTs As Long
        Public RestRaw As String = ""
        Public RestFlag As String = ""
        Public FirstSeenUtc As DateTime
        Public ReconnectGenAtPend As Integer
    End Class

    Public Module LiqFlagProbe

        Private Const WsUrl As String = "wss://www.deribit.com/ws/api/v2"
        Private Const RestUrl As String = "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument"
        Private Const Instrument As String = "BTC-PERPETUAL"
        Private Const Chan100ms As String = "trades.BTC-PERPETUAL.100ms"
        Private Const ChanRaw As String = "trades.BTC-PERPETUAL.raw"
        ' Arm 3b — the SUBSTITUTE for a refused arm 3. `.raw` needs authentication (error
        ' 13778, measured 2026-09-21 UTC) and this probe is forbidden to authenticate, so
        ' `.raw` can never answer "is the 100ms aggregation what drops the field". `agg2` is
        ' a DIFFERENT aggregation of the same tape and is open to an unauthorized client.
        ' It does not replace `.raw`, but it separates "aggregation drops it" from "the venue
        ' never sends it on a trades channel" — which is the decision D-4 actually turns on.
        Private Const ChanAgg2 As String = "trades.BTC-PERPETUAL.agg2"
        Private Const HeartbeatSec As Integer = 30

        ' Index cap. BTC-PERPETUAL runs well under 200k trades/day per channel; 400k entries
        ' covers both channels for far longer than any REST poll needs to reach back.
        Private Const IndexCap As Integer = 400000
        ' A REST-flagged trade is only called MISSING once the stream is stamped this far
        ' past it AND this much wall clock has elapsed. Absence declared early is a lie.
        Private Const StreamAheadMs As Long = 5000
        Private Const PendGraceSec As Integer = 120
        Private Const RawDumpRotateBytes As Long = 64L * 1024L * 1024L
        ' The status loop ticks often enough to notice a STOP file quickly, but only prints
        ' every StatusEveryTicks-th tick, so an unattended multi-day log stays readable.
        Private Const StatusTickSec As Integer = 30
        Private Const StatusEveryTicks As Integer = 10
        Private Const StopFileName As String = "STOP"

        Private ReadOnly _gate As New Object()

        ' key = channel & "|" & trade_id  → the raw streamed object
        Private ReadOnly _index As New Dictionary(Of String, StreamRec)()
        Private ReadOnly _order As New Queue(Of String)()

        ' arm 1 / arm 3: property-name tallies and `liquidation` value tallies, per channel
        Private ReadOnly _fieldTally As New Dictionary(Of String, Dictionary(Of String, Long))()
        Private ReadOnly _liqValueTally As New Dictionary(Of String, Dictionary(Of String, Long))()
        Private ReadOnly _tradeCount As New Dictionary(Of String, Long)()

        ' subscribe request id → channel, and the venue's per-channel verdict on it
        Private ReadOnly _subIds As New Dictionary(Of Integer, String)()
        Private ReadOnly _chanStatus As New Dictionary(Of String, String)()

        Private ReadOnly _pending As New Dictionary(Of String, PendingLiq)()
        Private ReadOnly _resolved As New HashSet(Of String)()

        Private _maxVenueTs100ms As Long = 0
        Private _batchIndex As Long = 0
        Private _restPolls As Long = 0
        Private _restTradesSeen As Long = 0
        Private _restFlagged As Long = 0
        Private _pairFound As Long = 0
        Private _pairMissing As Long = 0
        Private _pairUntrusted As Long = 0
        Private _reconnectGen As Integer = 0
        Private _controlSeen As Long = 0
        Private _nextId As Integer = 1000

        Private _rawDump As StreamWriter = Nothing
        Private _rawDumpBytes As Long = 0
        Private _rawDumpSeq As Integer = 0
        Private _pairOut As StreamWriter = Nothing
        Private _runStamp As String = ""
        Private _startUtc As DateTime

        ''' <summary>
        ''' Entry point. args(0) = duration seconds, 0 or absent meaning run until stopped.
        ''' args(1) = REST poll interval in seconds (default 10).
        ''' </summary>
        Public Function Run(args As String()) As Integer
            Dim seconds As Integer = 0          ' 0 = unlimited; this probe is meant to be left running
            Dim restPollSec As Integer = 10
            If args IsNot Nothing AndAlso args.Length > 0 Then
                Dim p As Integer
                If Integer.TryParse(args(0), NumberStyles.Integer, CultureInfo.InvariantCulture, p) AndAlso p >= 0 Then seconds = p
            End If
            If args IsNot Nothing AndAlso args.Length > 1 Then
                Dim p As Integer
                If Integer.TryParse(args(1), NumberStyles.Integer, CultureInfo.InvariantCulture, p) AndAlso p > 0 Then restPollSec = p
            End If
            Try
                Return RunAsync(seconds, restPollSec).GetAwaiter().GetResult()
            Catch ex As Exception
                Console.Error.WriteLine("FATAL: " & ex.Message)
                Return 2
            Finally
                CloseWriters()
            End Try
        End Function

        Private Async Function RunAsync(seconds As Integer, restPollSec As Integer) As Task(Of Integer)
            _startUtc = DateTime.UtcNow
            _runStamp = _startUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)

            Console.WriteLine("LiqFlagProbe — liquidation-flag measurement (engine-fix build spec section 4.1, session B1)")
            Console.WriteLine("  instrument : " & Instrument)
            Console.WriteLine("  arm 1      : " & Chan100ms & "   (raw JSON dumped verbatim)")
            Console.WriteLine("  arm 3      : " & ChanRaw & "   (needs auth - expect REFUSED 13778)")
            Console.WriteLine("  arm 3b     : " & ChanAgg2 & "   (the unauthorized substitute)")
            Console.WriteLine("  arm 2      : REST public/get_last_trades_by_instrument every " & restPollSec & "s")
            Console.WriteLine("  duration   : " & If(seconds > 0, seconds & "s", "UNLIMITED — stop with Ctrl+C"))
            Console.WriteLine("  started    : " & _startUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & " UTC")
            Console.WriteLine("  NOTE: public feeds only, no auth, no orders. Writes only new files here.")
            Console.WriteLine()

            OpenWriters()

            Dim cts As CancellationTokenSource = MakeCts(seconds)
            Try
                AddHandler Console.CancelKeyPress,
                    Sub(sender As Object, e As ConsoleCancelEventArgs)
                        e.Cancel = True
                        Console.WriteLine()
                        Console.WriteLine("Ctrl+C — finishing and writing the summary…")
                        Try
                            cts.Cancel()
                        Catch
                        End Try
                    End Sub

                Dim wsTask As Task = WsSupervisorAsync(cts.Token)
                Dim restTask As Task = RestPollLoopAsync(restPollSec, cts.Token)
                Dim statusTask As Task = StatusLoopAsync(cts, cts.Token)
                Try
                    Await Task.WhenAll(wsTask, restTask, statusTask)
                Catch ex As OperationCanceledException
                    ' the duration elapsed, or Ctrl+C — the normal exit
                Catch ex As Exception
                    Console.Error.WriteLine("loop error: " & ex.Message)
                End Try
            Finally
                cts.Dispose()
            End Try

            Report()
            CloseWriters()
            Return 0
        End Function

        ''' <summary>A CancellationTokenSource that never fires on its own when seconds is 0.</summary>
        Private Function MakeCts(seconds As Integer) As CancellationTokenSource
            If seconds > 0 Then Return New CancellationTokenSource(TimeSpan.FromSeconds(seconds))
            Return New CancellationTokenSource()
        End Function

        ' ── WebSocket: supervisor + receive ──────────────────────────────────────────────
        ' A multi-day run WILL be disconnected. Reconnect, and remember that it happened —
        ' a pending window spanning a reconnect cannot honestly report a missing trade.
        Private Async Function WsSupervisorAsync(ct As CancellationToken) As Task
            Dim backoff As Integer = 2
            While Not ct.IsCancellationRequested
                Dim failure As String = Nothing          ' VB forbids Await inside Catch
                Try
                    Using ws As New ClientWebSocket()
                        ' ⛔ Match DeribitWsFeed: WPAD proxy auto-detect hangs the connect
                        ' before any socket opens (584c616). That commit missed this file.
                        ws.Options.Proxy = Nothing
                        Await ws.ConnectAsync(New Uri(WsUrl), ct)
                        SyncLock _gate
                            _reconnectGen += 1
                        End SyncLock
                        Console.WriteLine("[" & NowUtc() & "] ws connected (generation " & _reconnectGen & ")")
                        Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() &
                                             ",""method"":""public/set_heartbeat"",""params"":{""interval"":" &
                                             HeartbeatSec & "}}", ct)
                        ' ⛔ ONE SUBSCRIBE PER CHANNEL, and this is not a style choice.
                        ' Measured 2026-09-21 (UTC) on the first smoke run: a single request
                        ' naming both channels is rejected WHOLE with error 13778
                        ' `raw_subscriptions_not_available_for_unauthorized`, so arm 3 takes
                        ' arms 1 and 2 down with it and the run collects nothing in silence.
                        ' Separate requests let the 100ms channel survive arm 3's refusal.
                        For Each chan As String In New String() {Chan100ms, ChanRaw, ChanAgg2}
                            Dim subId As Integer = NextId()
                            SyncLock _gate
                                _subIds(subId) = chan
                            End SyncLock
                            Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & subId &
                                                 ",""method"":""public/subscribe"",""params"":{""channels"":[""" &
                                                 chan & """]}}", ct)
                        Next
                        Console.WriteLine("[" & NowUtc() & "] subscribe sent for all three channels; collecting…")
                        backoff = 2
                        Await ReceiveLoopAsync(ws, ct)
                    End Using
                Catch ex As OperationCanceledException
                    Return
                Catch ex As Exception
                    failure = ex.Message
                End Try
                If ct.IsCancellationRequested Then Return
                Console.Error.WriteLine("[" & NowUtc() & "] ws dropped" & If(failure Is Nothing, "", ": " & failure) &
                                        " — reconnecting in " & backoff & "s")
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(backoff), ct)
                Catch ex As OperationCanceledException
                    Return
                End Try
                backoff = Math.Min(backoff * 2, 60)
            End While
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
                ' A response, not a notification. ⛔ Log it. A silently rejected subscribe
                ' looks exactly like a quiet market, and this probe is meant to be left
                ' unattended for days — the first smoke run collected 0 trades for want of
                ' this line.
                LogControl(msg)
                Return
            End If
            Dim m As String = If(methodEl.GetString(), "")

            ' Miss the test_request and Deribit drops the connection. The app's own feed
            ' calls this out; a multi-day run cannot afford it.
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
            Dim chan As String = "?"
            If prm.TryGetProperty("channel", chanEl) Then chan = If(chanEl.GetString(), "?")
            Dim data As JsonElement = Nothing
            If Not prm.TryGetProperty("data", data) Then Return
            If data.ValueKind <> JsonValueKind.Array Then Return

            Dim recvUtc As DateTime = DateTime.UtcNow
            SyncLock _gate
                _batchIndex += 1
                For Each t As JsonElement In data.EnumerateArray()
                    If t.ValueKind <> JsonValueKind.Object Then Continue For
                    ' ⛔ ARM 1's WHOLE POINT: the RAW TEXT, never a re-serialised parsed struct.
                    Dim raw As String = t.GetRawText()
                    Dim tradeId As String = RawString(t, "trade_id")
                    Dim venueTs As Long = RawLong(t, "timestamp")

                    TallyFields(chan, t)
                    Dim cnt As Long = 0
                    _tradeCount.TryGetValue(chan, cnt)
                    _tradeCount(chan) = cnt + 1

                    If chan = Chan100ms AndAlso venueTs > _maxVenueTs100ms Then _maxVenueTs100ms = venueTs

                    If tradeId.Length > 0 Then
                        Dim key As String = chan & "|" & tradeId
                        If Not _index.ContainsKey(key) Then
                            Dim rec As New StreamRec() With {
                                .Channel = chan, .RecvUtc = recvUtc, .VenueTs = venueTs,
                                .Raw = raw, .ReconnectGen = _reconnectGen}
                            _index(key) = rec
                            _order.Enqueue(key)
                            While _order.Count > IndexCap
                                Dim old As String = _order.Dequeue()
                                _index.Remove(old)
                            End While
                        End If
                    End If

                    WriteRawDump(recvUtc, chan, raw)
                Next
                ' A streamed trade may have arrived for something already pending.
                ResolvePending_Locked()
            End SyncLock
        End Function

        ' ── ARM 2: REST poll and pairing ─────────────────────────────────────────────────
        Private Async Function RestPollLoopAsync(everySec As Integer, ct As CancellationToken) As Task
            ' ⛔ UseProxy = False for the same WPAD reason: HttpClient.Timeout does not bound
            ' proxy resolution, which happens before the request starts (584c616).
            Using http As New HttpClient(New HttpClientHandler() With {.UseProxy = False})
                http.Timeout = TimeSpan.FromSeconds(20)
                While Not ct.IsCancellationRequested
                    Dim body As String = Nothing                ' VB forbids Await inside Catch
                    Try
                        Dim url As String = RestUrl & "?instrument_name=" & Instrument & "&count=1000&sorting=desc"
                        body = Await http.GetStringAsync(url, ct)
                    Catch ex As OperationCanceledException
                        Return
                    Catch ex As Exception
                        Console.Error.WriteLine("[" & NowUtc() & "] rest error: " & ex.Message)
                    End Try
                    If body IsNot Nothing Then IngestRest(body)
                    Try
                        Await Task.Delay(TimeSpan.FromSeconds(everySec), ct)
                    Catch ex As OperationCanceledException
                        Return
                    End Try
                End While
            End Using
        End Function

        Private Sub IngestRest(body As String)
            Dim root As JsonElement
            Try
                Using doc As JsonDocument = JsonDocument.Parse(body)
                    root = doc.RootElement.Clone()
                End Using
            Catch
                Return
            End Try
            Dim resEl As JsonElement = Nothing
            If Not root.TryGetProperty("result", resEl) Then Return
            ' The documented shape is {"result":{"trades":[…],"has_more":bool}}; tolerate a bare array too.
            Dim arr As JsonElement = resEl
            If resEl.ValueKind = JsonValueKind.Object Then
                Dim tr As JsonElement = Nothing
                If Not resEl.TryGetProperty("trades", tr) Then Return
                arr = tr
            End If
            If arr.ValueKind <> JsonValueKind.Array Then Return

            SyncLock _gate
                _restPolls += 1
                For Each t As JsonElement In arr.EnumerateArray()
                    If t.ValueKind <> JsonValueKind.Object Then Continue For
                    _restTradesSeen += 1
                    Dim tradeId As String = RawString(t, "trade_id")
                    If tradeId.Length = 0 Then Continue For
                    Dim flag As String = RawString(t, "liquidation")
                    ' Flagged = the property is present, non-empty, and not "none".
                    If flag.Length = 0 OrElse String.Equals(flag, "none", StringComparison.OrdinalIgnoreCase) Then Continue For
                    If _resolved.Contains(tradeId) OrElse _pending.ContainsKey(tradeId) Then Continue For
                    _restFlagged += 1
                    Dim p As New PendingLiq() With {
                        .TradeId = tradeId,
                        .VenueTs = RawLong(t, "timestamp"),
                        .RestRaw = t.GetRawText(),
                        .RestFlag = flag,
                        .FirstSeenUtc = DateTime.UtcNow,
                        .ReconnectGenAtPend = _reconnectGen}
                    _pending(tradeId) = p
                    Console.WriteLine("[" & NowUtc() & "] REST liquidation trade_id=" & tradeId &
                                      " liquidation=" & flag & " — looking for its streamed twin")
                Next
                ResolvePending_Locked()
            End SyncLock
        End Sub

        ''' <summary>
        ''' Pair or expire pending REST-flagged trades. Caller holds _gate.
        ''' ⛔ A missing trade is only declared once the stream has demonstrably moved PAST it.
        ''' </summary>
        Private Sub ResolvePending_Locked()
            If _pending.Count = 0 Then Return
            Dim done As New List(Of String)()
            For Each kv In _pending
                Dim p As PendingLiq = kv.Value
                Dim s100 As StreamRec = Nothing
                Dim sRaw As StreamRec = Nothing
                Dim sAgg As StreamRec = Nothing
                _index.TryGetValue(Chan100ms & "|" & p.TradeId, s100)
                _index.TryGetValue(ChanRaw & "|" & p.TradeId, sRaw)
                _index.TryGetValue(ChanAgg2 & "|" & p.TradeId, sAgg)

                If s100 IsNot Nothing OrElse sRaw IsNot Nothing OrElse sAgg IsNot Nothing Then
                    EmitPair(p, s100, sRaw, sAgg, "PAIRED")
                    _pairFound += 1
                    done.Add(kv.Key)
                    Continue For
                End If

                Dim streamPast As Boolean = (_maxVenueTs100ms > 0 AndAlso _maxVenueTs100ms > p.VenueTs + StreamAheadMs)
                Dim graceOver As Boolean = ((DateTime.UtcNow - p.FirstSeenUtc).TotalSeconds >= PendGraceSec)
                If streamPast AndAlso graceOver Then
                    If _reconnectGen <> p.ReconnectGenAtPend Then
                        ' The socket dropped while this trade was pending. Absence here says
                        ' nothing about the channel — report it as untrusted, not as missing.
                        EmitPair(p, Nothing, Nothing, Nothing, "UNTRUSTED_RECONNECT")
                        _pairUntrusted += 1
                    Else
                        EmitPair(p, Nothing, Nothing, Nothing, "NO_STREAMED_TRADE")
                        _pairMissing += 1
                    End If
                    done.Add(kv.Key)
                End If
            Next
            For Each k In done
                _pending.Remove(k)
                _resolved.Add(k)
            Next
        End Sub

        Private Sub EmitPair(p As PendingLiq, s100 As StreamRec, sRaw As StreamRec, sAgg As StreamRec, verdict As String)
            Dim sb As New StringBuilder()
            sb.Append("{""emitted_utc"":""").Append(NowUtc()).Append(""",")
            sb.Append("""verdict"":""").Append(verdict).Append(""",")
            sb.Append("""trade_id"":""").Append(JsonStr(p.TradeId)).Append(""",")
            sb.Append("""rest_liquidation"":""").Append(JsonStr(p.RestFlag)).Append(""",")
            sb.Append("""venue_ts"":").Append(p.VenueTs.ToString(CultureInfo.InvariantCulture)).Append(",")
            sb.Append("""stream_100ms_has_liquidation_field"":").Append(HasLiqField(s100)).Append(",")
            sb.Append("""stream_raw_has_liquidation_field"":").Append(HasLiqField(sRaw)).Append(",")
            sb.Append("""stream_agg2_has_liquidation_field"":").Append(HasLiqField(sAgg)).Append(",")
            sb.Append("""rest_raw"":").Append(p.RestRaw).Append(",")
            sb.Append("""stream_100ms_raw"":").Append(If(s100 Is Nothing, "null", s100.Raw)).Append(",")
            sb.Append("""stream_raw_chan_raw"":").Append(If(sRaw Is Nothing, "null", sRaw.Raw)).Append(",")
            sb.Append("""stream_agg2_raw"":").Append(If(sAgg Is Nothing, "null", sAgg.Raw))
            sb.Append("}")
            If _pairOut IsNot Nothing Then
                _pairOut.WriteLine(sb.ToString())
                _pairOut.Flush()
            End If

            Console.WriteLine()
            Console.WriteLine("====== ARM 2 PAIRING — " & verdict & " ======")
            Console.WriteLine("  trade_id         : " & p.TradeId)
            Console.WriteLine("  REST liquidation : " & p.RestFlag)
            Console.WriteLine("  REST raw         : " & p.RestRaw)
            Console.WriteLine("  100ms raw        : " & If(s100 Is Nothing, "<NO STREAMED MESSAGE CARRIED THIS trade_id>", s100.Raw))
            Console.WriteLine("  raw-chan raw     : " & If(sRaw Is Nothing, "<NO STREAMED MESSAGE CARRIED THIS trade_id>", sRaw.Raw))
            Console.WriteLine("  agg2 raw         : " & If(sAgg Is Nothing, "<NO STREAMED MESSAGE CARRIED THIS trade_id>", sAgg.Raw))
            Console.WriteLine("===========================================")
            Console.WriteLine()
        End Sub

        ''' <summary>
        ''' Print a control-frame response (subscribe acknowledgement, error, test reply).
        ''' The first few always print; after that only errors, so a multi-day run stays quiet.
        ''' </summary>
        Private Sub LogControl(msg As String)
            Dim isError As Boolean = msg.Contains("""error""")

            ' Attribute a subscribe acknowledgement or refusal to ITS channel, so the run
            ' summary can state per channel whether the subscription was ever accepted.
            Dim chan As String = Nothing
            Try
                Using d As JsonDocument = JsonDocument.Parse(msg)
                    Dim idEl As JsonElement = Nothing
                    If d.RootElement.TryGetProperty("id", idEl) AndAlso idEl.ValueKind = JsonValueKind.Number Then
                        Dim id As Integer = 0
                        If idEl.TryGetInt32(id) Then
                            SyncLock _gate
                                _subIds.TryGetValue(id, chan)
                            End SyncLock
                        End If
                    End If
                End Using
            Catch
            End Try
            ' Print the venue's own line BEFORE the attribution, or an unattended log reads
            ' as though each verdict belongs to the message above it.
            Dim quiet As Boolean
            SyncLock _gate
                _controlSeen += 1
                quiet = (_controlSeen > 8 AndAlso Not isError)
            End SyncLock
            If Not quiet Then
                Console.WriteLine("[" & NowUtc() & "] ws control: " & If(msg.Length > 900, msg.Substring(0, 900) & "…", msg))
            End If

            If chan IsNot Nothing Then
                Dim state As String = If(isError, "REFUSED", "ACCEPTED")
                SyncLock _gate
                    _chanStatus(chan) = state & " — " & msg
                End SyncLock
                Console.WriteLine("[" & NowUtc() & "]   ^ subscribe " & state & " for " & chan)
            End If
        End Sub

        Private Function HasLiqField(s As StreamRec) As String
            If s Is Nothing Then Return "null"
            Try
                Using d As JsonDocument = JsonDocument.Parse(s.Raw)
                    Dim v As JsonElement = Nothing
                    If d.RootElement.TryGetProperty("liquidation", v) Then Return "true"
                End Using
            Catch
            End Try
            Return "false"
        End Function

        ' ── arm 1 / arm 3 tallies ────────────────────────────────────────────────────────
        Private Sub TallyFields(chan As String, t As JsonElement)
            Dim names As Dictionary(Of String, Long) = Nothing
            If Not _fieldTally.TryGetValue(chan, names) Then
                names = New Dictionary(Of String, Long)()
                _fieldTally(chan) = names
            End If
            For Each pr As JsonProperty In t.EnumerateObject()
                Dim c As Long = 0
                names.TryGetValue(pr.Name, c)
                names(pr.Name) = c + 1
                If pr.Name = "liquidation" Then
                    Dim vals As Dictionary(Of String, Long) = Nothing
                    If Not _liqValueTally.TryGetValue(chan, vals) Then
                        vals = New Dictionary(Of String, Long)()
                        _liqValueTally(chan) = vals
                    End If
                    Dim v As String = If(pr.Value.ValueKind = JsonValueKind.String, If(pr.Value.GetString(), ""), pr.Value.GetRawText())
                    Dim c2 As Long = 0
                    vals.TryGetValue(v, c2)
                    vals(v) = c2 + 1
                End If
            Next
        End Sub

        ' ── status + report ──────────────────────────────────────────────────────────────
        Private Async Function StatusLoopAsync(stopper As CancellationTokenSource, ct As CancellationToken) As Task
            Dim tick As Integer = 0
            While Not ct.IsCancellationRequested
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(StatusTickSec), ct)
                Catch ex As OperationCanceledException
                    Return
                End Try

                ' ⭐ A detached, windowless run cannot be sent Ctrl+C, and killing the process
                ' skips Report() — the run summary would be lost even though the data files
                ' survive. Dropping a file named STOP in the working directory ends the run
                ' the same way Ctrl+C does, so the summary is always printed.
                Try
                    If File.Exists(StopFileName) Then
                        Console.WriteLine("[" & NowUtc() & "] STOP file seen — finishing and writing the summary…")
                        stopper.Cancel()
                        Return
                    End If
                Catch
                End Try

                tick += 1
                If tick Mod StatusEveryTicks <> 0 Then Continue While
                SyncLock _gate
                    If _rawDump IsNot Nothing Then _rawDump.Flush()
                    Dim c1 As Long = 0
                    _tradeCount.TryGetValue(Chan100ms, c1)
                    Dim c2 As Long = 0
                    _tradeCount.TryGetValue(ChanRaw, c2)
                    Console.WriteLine("[" & NowUtc() & "] up " &
                        CInt((DateTime.UtcNow - _startUtc).TotalMinutes) & "m | 100ms " & c1 &
                        " | raw " & c2 & " | rest polls " & _restPolls &
                        " | flagged " & _restFlagged & " | paired " & _pairFound &
                        " | missing " & _pairMissing & " | untrusted " & _pairUntrusted &
                        " | pending " & _pending.Count)
                End SyncLock
            End While
        End Function

        Private Sub Report()
            SyncLock _gate
                Console.WriteLine()
                Console.WriteLine("== RUN SUMMARY ==")
                Console.WriteLine("  window UTC : " & _startUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) &
                                  "  ->  " & NowUtc())
                Console.WriteLine("  ws batches : " & _batchIndex & "   reconnects: " & Math.Max(0, _reconnectGen - 1))
                Console.WriteLine()
                Console.WriteLine("== ARM 1 / ARM 3 / ARM 3b — field names actually delivered, per channel ==")
                For Each chan In New String() {Chan100ms, ChanRaw, ChanAgg2}
                    Dim n As Long = 0
                    _tradeCount.TryGetValue(chan, n)
                    Console.WriteLine("  " & chan & "  (" & n & " trade objects)")
                    ' ⛔ Without this line a REFUSED channel reads exactly like a quiet one,
                    ' and "the field never arrived" would be reported for a channel the venue
                    ' never let us open. Measured 2026-09-21: `.raw` is refused unauthorized.
                    Dim status As String = Nothing
                    If _chanStatus.TryGetValue(chan, status) Then
                        Console.WriteLine("      subscription: " & status)
                    Else
                        Console.WriteLine("      subscription: NO ACKNOWLEDGEMENT SEEN — treat this channel as unmeasured")
                    End If
                    Dim names As Dictionary(Of String, Long) = Nothing
                    If _fieldTally.TryGetValue(chan, names) Then
                        Dim keys As New List(Of String)(names.Keys)
                        keys.Sort(StringComparer.Ordinal)
                        For Each k In keys
                            Console.WriteLine("      " & k.PadRight(24) & " " & names(k))
                        Next
                    Else
                        Console.WriteLine("      <no trade objects seen on this channel>")
                    End If
                    Dim vals As Dictionary(Of String, Long) = Nothing
                    If _liqValueTally.TryGetValue(chan, vals) Then
                        Console.WriteLine("      `liquidation` values:")
                        For Each kv In vals
                            Console.WriteLine("        """ & kv.Key & """ : " & kv.Value)
                        Next
                    Else
                        Console.WriteLine("      `liquidation` : FIELD NEVER PRESENT on this channel")
                    End If
                Next
                Console.WriteLine()
                Console.WriteLine("== ARM 2 — REST pairing ==")
                Console.WriteLine("  rest polls          : " & _restPolls)
                Console.WriteLine("  rest trades scanned : " & _restTradesSeen)
                Console.WriteLine("  rest FLAGGED trades : " & _restFlagged)
                Console.WriteLine("  paired with stream  : " & _pairFound)
                Console.WriteLine("  no streamed trade   : " & _pairMissing)
                Console.WriteLine("  untrusted (reconn.) : " & _pairUntrusted)
                Console.WriteLine("  still pending       : " & _pending.Count)
                If _restFlagged = 0 Then
                    Console.WriteLine("  => INCONCLUSIVE. No liquidation passed in this window. Keep running.")
                End If
                Console.WriteLine()
                Console.WriteLine("  raw dump    -> " & RawDumpPath())
                Console.WriteLine("  pairings    -> " & Path.GetFullPath("liq_probe_pairings_" & _runStamp & ".jsonl"))
            End SyncLock
        End Sub

        ' ── file plumbing ────────────────────────────────────────────────────────────────
        Private Sub OpenWriters()
            OpenRawDump()
            _pairOut = New StreamWriter("liq_probe_pairings_" & _runStamp & ".jsonl", True, New UTF8Encoding(False))
            _pairOut.AutoFlush = True
        End Sub

        Private Sub OpenRawDump()
            If _rawDump IsNot Nothing Then
                _rawDump.Flush()
                _rawDump.Dispose()
            End If
            _rawDumpSeq += 1
            _rawDumpBytes = 0
            _rawDump = New StreamWriter(RawDumpPath(), True, New UTF8Encoding(False))
        End Sub

        Private Function RawDumpPath() As String
            Return Path.GetFullPath("liq_probe_raw_" & _runStamp & "_" &
                                    _rawDumpSeq.ToString("00", CultureInfo.InvariantCulture) & ".jsonl")
        End Function

        ''' <summary>Arm 1's output: the venue's own bytes, one JSON object per line. Caller holds _gate.</summary>
        Private Sub WriteRawDump(recvUtc As DateTime, chan As String, raw As String)
            If _rawDump Is Nothing Then Return
            Dim line As String = "{""recv_utc"":""" & recvUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) &
                                 """,""channel"":""" & chan & """,""raw"":" & raw & "}"
            _rawDump.WriteLine(line)
            _rawDumpBytes += line.Length + 2
            If _rawDumpBytes >= RawDumpRotateBytes Then OpenRawDump()
        End Sub

        Private Sub CloseWriters()
            Try
                If _rawDump IsNot Nothing Then
                    _rawDump.Flush()
                    _rawDump.Dispose()
                    _rawDump = Nothing
                End If
            Catch
            End Try
            Try
                If _pairOut IsNot Nothing Then
                    _pairOut.Flush()
                    _pairOut.Dispose()
                    _pairOut = Nothing
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

        Private Function NowUtc() As String
            Return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        End Function

        Private Function JsonStr(s As String) As String
            If s Is Nothing Then Return ""
            Return s.Replace("\", "\\").Replace("""", "\""")
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
