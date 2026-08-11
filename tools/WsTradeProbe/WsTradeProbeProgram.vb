' ─────────────────────────────────────────────────────────────────────────────────────────
' WsTradeProbe — the DELIVERY GATE for the same-millisecond trade drop.
'
' Context: docs/trade-store-same-millisecond-drop-2026-08-11.md
'   Core/TradeStoreWriter.vb:149 reads `If t.Timestamp <= _lastTs Then Return False`, so any
'   trade sharing a millisecond with the previously buffered one is discarded at write time.
'   Two instruments measured ~50% of the tape missing. What neither can answer from stored
'   data is whether the WS channel DELIVERED the dropped trades.
'
'   That fork decides the whole fix, which is why it runs first and alone — the same
'   discipline the trade-identity build used for its own §1 gate.
'
' THREE ANSWERS, all from one run:
'   G1  Does the feed deliver several trades at one millisecond?
'       max trades-per-timestamp = 1  ⇒  the feed never sends siblings; the guard is
'                                        INNOCENT and a guard fix changes nothing.
'       max > 1                       ⇒  the feed does send them; the guard is discarding
'                                        real trades.
'   G2  Is the DELIVERED trade_seq stream contiguous?
'       contiguous ⇒ the feed is complete, so the guard is the SOLE cause of store loss.
'       gaps       ⇒ the feed is itself incomplete and a guard fix is necessary but not
'                    sufficient. This is the finding that would change the spec most.
'   G3  Replaying the SHIPPED guard over the delivered stream, how much would it drop?
'       If G1 and G2 are as theorised, this should land near the ~50% both stored-data
'       instruments measured. Three independent routes agreeing is the acceptance bar.
'
' SAFETY. Standalone by construction: links nothing from the app, reads no settings, and
' writes exactly one new CSV in the working directory. It cannot touch collector state, so
' it is safe to run beside a live collector. Public channels only — no auth, no orders.
' ─────────────────────────────────────────────────────────────────────────────────────────
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Namespace Global.DeribitVerdictEngine

    Friend Structure ProbeTrade
        Public RecvMs As Long          ' our receive clock — distinguishes venue stamp from arrival
        Public BatchIndex As Long      ' which notification carried it (see the batch note in G1)
        Public Timestamp As Long       ' the venue's millisecond stamp — the guard's key
        Public Price As Double
        Public Amount As Double
        Public Direction As String
        Public TradeId As String
        Public TradeSeq As Long
    End Structure

    Public Module WsTradeProbeProgram

        Private Const WsUrl As String = "wss://www.deribit.com/ws/api/v2"
        Private Const Channel As String = "trades.BTC-PERPETUAL.100ms"
        Private Const HeartbeatSec As Integer = 30

        Private ReadOnly _trades As New List(Of ProbeTrade)()
        Private _batchIndex As Long = 0
        Private _nextId As Integer = 1

        Public Function Main(args As String()) As Integer
            Dim seconds As Integer = 300
            If args IsNot Nothing AndAlso args.Length > 0 Then
                Dim parsed As Integer
                If Integer.TryParse(args(0), parsed) AndAlso parsed > 0 Then seconds = parsed
            End If
            Try
                Return RunAsync(seconds).GetAwaiter().GetResult()
            Catch ex As Exception
                Console.Error.WriteLine("FATAL: " & ex.Message)
                Return 2
            End Try
        End Function

        Private Async Function RunAsync(seconds As Integer) As Task(Of Integer)
            Console.WriteLine("WsTradeProbe — delivery gate for the same-millisecond drop")
            Console.WriteLine("  channel : " & Channel)
            Console.WriteLine("  duration: " & seconds & "s")
            Console.WriteLine("  NOTE: this reads a public feed and writes ONE csv here. It touches no collector state.")
            Console.WriteLine()

            Using cts As New CancellationTokenSource(TimeSpan.FromSeconds(seconds))
                Try
                    Using ws As New ClientWebSocket()
                        ' Connect with a short backoff. Deribit's edge can answer 503 to
                        ' everything (REST and WS alike) from a given network — measured
                        ' 2026-08-11 from the local box, 0.26s responses, with and without a
                        ' User-Agent. That is the venue's edge, not a client defect: the app's
                        ' own DeribitWsFeed uses an identical bare ClientWebSocket. Retry a
                        ' few times so an unattended run is not wasted, then give up honestly.
                        Dim connected As Boolean = False
                        For attempt As Integer = 1 To 4
                            Dim failure As String = Nothing      ' VB forbids Await inside Catch
                            Try
                                Await ws.ConnectAsync(New Uri(WsUrl), cts.Token)
                                connected = True
                            Catch ex As OperationCanceledException
                                Throw
                            Catch ex As Exception
                                failure = ex.Message
                            End Try
                            If connected Then Exit For
                            Console.WriteLine("  connect attempt " & attempt & " failed: " & failure)
                            If attempt = 4 Then Exit For
                            Await Task.Delay(TimeSpan.FromSeconds(5 * attempt), cts.Token)
                        Next
                        If Not connected Then
                            Console.Error.WriteLine("could not connect after 4 attempts — see the header note; try the AWS box.")
                            Return 1
                        End If
                        Console.WriteLine("connected")
                        Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() &
                                             ",""method"":""public/set_heartbeat"",""params"":{""interval"":" &
                                             HeartbeatSec & "}}", cts.Token)
                        Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() &
                                             ",""method"":""public/subscribe"",""params"":{""channels"":[""" &
                                             Channel & """]}}", cts.Token)
                        Console.WriteLine("subscribed; collecting…")
                        Await ReceiveLoopAsync(ws, cts.Token)
                    End Using
                Catch ex As OperationCanceledException
                    ' the duration elapsed — the normal exit
                Catch ex As Exception
                    Console.Error.WriteLine("socket error: " & ex.Message)
                End Try
            End Using

            Console.WriteLine()
            If _trades.Count = 0 Then
                Console.Error.WriteLine("NO TRADES CAPTURED — inconclusive. Re-run in an active session.")
                Return 1
            End If
            WriteCsv()
            Report()
            Return 0
        End Function

        ' ── receive ──────────────────────────────────────────────────────────────────────
        Private Async Function ReceiveLoopAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
            Dim buffer(16383) As Byte
            Dim sb As New StringBuilder()
            While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested
                Dim res As WebSocketReceiveResult = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), ct)
                If res.MessageType = WebSocketMessageType.Close Then Exit While
                sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count))
                If Not res.EndOfMessage Then Continue While
                Dim msg As String = sb.ToString()
                sb.Clear()
                Await HandleAsync(ws, msg, ct)
            End While
        End Function

        Private Async Function HandleAsync(ws As ClientWebSocket, msg As String, ct As CancellationToken) As Task
            Dim root As JsonElement
            Try
                Using doc As JsonDocument = JsonDocument.Parse(msg)
                    root = doc.RootElement.Clone()
                End Using
            Catch
                Return
            End Try

            Dim methodEl As JsonElement = Nothing
            If Not root.TryGetProperty("method", methodEl) Then Return
            Dim m As String = If(methodEl.GetString(), "")

            ' The #1 foot-gun the app's own feed calls out: miss the test_request and Deribit drops us.
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
            Dim data As JsonElement = Nothing
            If Not prm.TryGetProperty("data", data) Then Return
            If data.ValueKind <> JsonValueKind.Array Then Return

            _batchIndex += 1
            Dim recvMs As Long = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            For Each t As JsonElement In data.EnumerateArray()
                Dim r As New ProbeTrade()
                r.RecvMs = recvMs
                r.BatchIndex = _batchIndex
                r.Timestamp = ReadLong(t, "timestamp")
                r.Price = ReadDouble(t, "price")
                r.Amount = ReadDouble(t, "amount")
                r.Direction = ReadString(t, "direction")
                r.TradeId = ReadString(t, "trade_id")
                r.TradeSeq = ReadLong(t, "trade_seq")
                _trades.Add(r)
            Next
            If _batchIndex Mod 200 = 0 Then
                Console.WriteLine("  … " & _batchIndex & " batches, " & _trades.Count & " trades")
            End If
        End Function

        ' ── report ───────────────────────────────────────────────────────────────────────
        Private Sub Report()
            ' G1 — same-millisecond delivery.
            Dim perTs As New Dictionary(Of Long, Integer)()
            For Each t In _trades
                Dim c As Integer = 0
                perTs.TryGetValue(t.Timestamp, c)
                perTs(t.Timestamp) = c + 1
            Next
            Dim hist As New SortedDictionary(Of Integer, Integer)()
            Dim maxPer As Integer = 0
            For Each kv In perTs
                Dim c As Integer = 0
                hist.TryGetValue(kv.Value, c)
                hist(kv.Value) = c + 1
                If kv.Value > maxPer Then maxPer = kv.Value
            Next

            Console.WriteLine("══ G1 — does the feed deliver several trades at one millisecond? ══")
            Console.WriteLine("  trades delivered      : " & _trades.Count)
            Console.WriteLine("  distinct timestamps   : " & perTs.Count)
            Console.WriteLine("  max trades per stamp  : " & maxPer)
            Console.WriteLine("  trades-per-timestamp distribution:")
            For Each kv In hist
                Console.WriteLine("     " & kv.Key.ToString().PadLeft(3) & " trade(s) : " & kv.Value & " timestamp(s)")
            Next
            If maxPer <= 1 Then
                Console.WriteLine("  ⇒ VERDICT: the feed NEVER delivers siblings. The guard is INNOCENT.")
                Console.WriteLine("             A guard fix would change nothing — re-open the capture strategy.")
            Else
                Console.WriteLine("  ⇒ VERDICT: the feed DOES deliver siblings. The guard is discarding real trades.")
            End If

            ' G2 — is the DELIVERED sequence contiguous?
            Console.WriteLine()
            Console.WriteLine("══ G2 — is the DELIVERED trade_seq stream contiguous? ══")
            Dim seqs As New List(Of Long)()
            For Each t In _trades
                If t.TradeSeq > 0 Then seqs.Add(t.TradeSeq)
            Next
            If seqs.Count = 0 Then
                Console.WriteLine("  no trade_seq on the wire — INCONCLUSIVE (and itself a finding).")
            Else
                seqs.Sort()
                Dim uniq As New List(Of Long)()
                For Each s In seqs
                    If uniq.Count = 0 OrElse uniq(uniq.Count - 1) <> s Then uniq.Add(s)
                Next
                Dim span As Long = uniq(uniq.Count - 1) - uniq(0) + 1
                Dim missing As Long = span - uniq.Count
                Dim runs As Integer = 0
                For i As Integer = 1 To uniq.Count - 1
                    If uniq(i) <> uniq(i - 1) + 1 Then runs += 1
                Next
                Console.WriteLine("  seq range     : " & uniq(0) & " … " & uniq(uniq.Count - 1))
                Console.WriteLine("  span          : " & span)
                Console.WriteLine("  delivered     : " & uniq.Count & "  (duplicates on the wire: " & (seqs.Count - uniq.Count) & ")")
                Console.WriteLine("  MISSING       : " & missing & "  across " & runs & " gap run(s)")
                If missing = 0 Then
                    Console.WriteLine("  ⇒ VERDICT: the feed is COMPLETE. The guard is the SOLE cause of store loss.")
                Else
                    Console.WriteLine("  ⇒ VERDICT: the FEED ITSELF is incomplete (" &
                                      (missing * 100.0R / span).ToString("F1", CultureInfo.InvariantCulture) &
                                      "% absent). A guard fix is necessary but NOT sufficient.")
                End If
            End If

            ' G3 — replay the shipped guard over what was actually delivered.
            Console.WriteLine()
            Console.WriteLine("══ G3 — replaying the SHIPPED guard (TradeStoreWriter.vb:149) ══")
            Dim lastTs As Long = -1
            Dim accepted As Integer = 0, dropped As Integer = 0
            For Each t In _trades                     ' arrival order, exactly as Buffer() sees them
                If t.Timestamp <= lastTs Then
                    dropped += 1
                Else
                    accepted += 1
                    lastTs = t.Timestamp
                End If
            Next
            Dim pct As Double = If(_trades.Count > 0, dropped * 100.0R / _trades.Count, 0.0R)
            Console.WriteLine("  delivered : " & _trades.Count)
            Console.WriteLine("  accepted  : " & accepted)
            Console.WriteLine("  DROPPED   : " & dropped & "  (" & pct.ToString("F1", CultureInfo.InvariantCulture) & "%)")
            Console.WriteLine("  ⇒ compare against the ~50% both stored-data instruments measured.")
            Console.WriteLine("     Three independent routes agreeing is the acceptance bar.")
        End Sub

        Private Sub WriteCsv()
            Dim stamp As String = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            ' NB: not named `path` — VB is case-insensitive, so a local `path` shadows System.IO.Path.
            Dim outFile As String = "ws_trade_probe_" & stamp & ".csv"
            Using sw As New StreamWriter(outFile, False, New UTF8Encoding(False))
                sw.WriteLine("RecvMs,BatchIndex,Timestamp,Price,Amount,Direction,TradeId,TradeSeq")
                For Each t In _trades
                    sw.WriteLine(String.Join(",",
                        t.RecvMs.ToString(CultureInfo.InvariantCulture),
                        t.BatchIndex.ToString(CultureInfo.InvariantCulture),
                        t.Timestamp.ToString(CultureInfo.InvariantCulture),
                        t.Price.ToString("F2", CultureInfo.InvariantCulture),
                        t.Amount.ToString("F2", CultureInfo.InvariantCulture),
                        t.Direction,
                        t.TradeId,
                        t.TradeSeq.ToString(CultureInfo.InvariantCulture)))
                Next
            End Using
            Console.WriteLine("raw capture → " & Path.GetFullPath(outFile))
            Console.WriteLine()
        End Sub

        ' ── plumbing ─────────────────────────────────────────────────────────────────────
        Private Async Function SendAsync(ws As ClientWebSocket, json As String, ct As CancellationToken) As Task
            Dim b As Byte() = Encoding.UTF8.GetBytes(json)
            Await ws.SendAsync(New ArraySegment(Of Byte)(b), WebSocketMessageType.Text, True, ct)
        End Function

        Private Function NextId() As Integer
            _nextId += 1
            Return _nextId
        End Function

        Private Function ReadLong(e As JsonElement, name As String) As Long
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

        Private Function ReadDouble(e As JsonElement, name As String) As Double
            Dim v As JsonElement = Nothing
            If Not e.TryGetProperty(name, v) Then Return 0.0R
            If v.ValueKind = JsonValueKind.Number Then Return v.GetDouble()
            Return 0.0R
        End Function

        Private Function ReadString(e As JsonElement, name As String) As String
            Dim v As JsonElement = Nothing
            If Not e.TryGetProperty(name, v) Then Return ""
            If v.ValueKind = JsonValueKind.String Then Return If(v.GetString(), "")
            If v.ValueKind = JsonValueKind.Number Then Return v.GetRawText()
            Return ""
        End Function

    End Module
End Namespace
