' ─────────────────────────────────────────────────────────────────────────────────────────
' RawBookProbe — does the 100 ms book feed mis-measure the D8 pull accounting?
' Spec: docs/raw-book-absorption-test-spec.md. Read-only research: no settings write, no
' scoring, no collector contact.
'
' THREE ARMS, one process, one authenticated WebSocket, one fold thread, one lock. Every arm
' is a separate instance of the SHIPPED Core/LevelAbsorptionTracker (linked, not copied),
' fed the SAME carried levels at the SAME instant:
'   A  "shipped"   book.BTC-PERPETUAL.none.10.100ms  + trades.BTC-PERPETUAL.100ms  (the engine's pair)
'   B  "raw"       top-10 of the book rebuilt from book.BTC-PERPETUAL.raw + trades.BTC-PERPETUAL.raw
'   C  "raw-book"  top-10 of the rebuilt raw book    + trades.BTC-PERPETUAL.100ms  (diagnostic:
'                  isolates the trade-feed granularity from the book-feed granularity)
'
' WHY THE RAW BOOK IS REBUILT. `book.BTC-PERPETUAL.none.10.raw` does not exist: the venue
' answers its subscribe with an EMPTY result list and sends nothing (measured 2026-10-05). The
' raw book is incremental only (`book.BTC-PERPETUAL.raw`, snapshot + changes chained by
' prev_change_id), so the probe keeps the full ladder and hands the tracker its top 10 levels
' after every change — the same shape the engine's grouped feed delivers. Reconstruction is
' CHECKED, not trusted: every 100 ms snapshot carries a change_id, and its top 10 must equal the
' rebuilt top 10 at that change_id (the book-agreement counter in every status line).
'
' LEVELS. Every minute the probe fetches REST candles and calls the engine's own
' CalcSwingPivots / CalcVPFRLite / CalcATR with the tracked settings.json, then resolves
' proximity / band / break tolerance as ATR × the absorption fractions — the carry in
' UI/MainForm_Analysis.vb (the SetAbsorptionLevels call site). That carry is three
' multiplications; it is restated here because UI/ cannot be linked. The fold is not restated.
'
' OUTPUT (all in --out): episodes_*.csv (one row per closed episode per arm), samples_*.csv
' (all three arms read at the same instant every --sample-sec), levels_*.csv, events_*.log,
' and a status line on stdout every minute. Files rotate at 64 MB; total output is capped.
'
' ⛔ SECRET HANDLING. The key comes ONLY from DERIBIT_RO_CLIENT_ID / DERIBIT_RO_CLIENT_SECRET,
' either in the process environment or in a KEY=VALUE file named by --env-file. The values, the
' auth request and the auth response are never printed or written; only their lengths are.
' Tokens last a year (measured 2026-09-29), so no refresh is attempted inside a run.
' ─────────────────────────────────────────────────────────────────────────────────────────
Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Net.WebSockets
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks

Namespace Global.DeribitVerdictEngine

    ''' <summary>One side's open episode as the observer sees it.</summary>
    Friend NotInheritable Class EpisodeWatch
        Public Active As Boolean
        Public Level As Double
        Public OpenMs As Long
        Public LastRead As AbsorptionSideRead
        Public BookFolds As Integer
    End Class

    ''' <summary>A closed episode, kept in memory for the end summary only (the CSV is the record).</summary>
    Friend Structure EpisodeRec
        Public Arm As Integer
        Public Side As Integer
        Public Gen As Integer
        Public Level As Double
        Public OpenMs As Long
        Public CloseMs As Long
        Public Sec As Double
        Public BookFolds As Integer
        Public PullLB As Double
        Public PostLB As Double
        Public PullFrac As Double
        Public ProbeReset As Boolean
    End Structure

    Friend NotInheritable Class Arm
        Public ReadOnly Index As Integer
        Public ReadOnly Name As String
        Public ReadOnly Tracker As New LevelAbsorptionTracker()
        Public ReadOnly Watch As EpisodeWatch() = {New EpisodeWatch(), New EpisodeWatch()}
        Public BookFolds As Long
        Public TradeFolds As Long
        Public Episodes As Long
        Public Sub New(index As Integer, name As String)
            Me.Index = index
            Me.Name = name
        End Sub
    End Class

    Public Module RawBookProbeProgram

        Private Const WsUrl As String = "wss://www.deribit.com/ws/api/v2"
        Private Const EnvId As String = "DERIBIT_RO_CLIENT_ID"
        Private Const EnvSecret As String = "DERIBIT_RO_CLIENT_SECRET"
        Private Const HeartbeatSec As Integer = 30

        Private Const ChBook100 As String = "book.BTC-PERPETUAL.none.10.100ms"
        Private Const ChBookRaw As String = "book.BTC-PERPETUAL.raw"
        Private Const ChTrades100 As String = "trades.BTC-PERPETUAL.100ms"
        Private Const ChTradesRaw As String = "trades.BTC-PERPETUAL.raw"
        Private ReadOnly Channels As String() = {ChBook100, ChBookRaw, ChTrades100, ChTradesRaw}

        Private Const ArmA As Integer = 0, ArmB As Integer = 1, ArmC As Integer = 2
        Private ReadOnly SideNames As String() = {"ABOVE", "BELOW"}
        Private ReadOnly ReasonNames As String() = {"DegenerateLadder", "LevelRemap", "ProximityShut",
                                                    "LadderSpanLost", "BreakThrough", "Reset", "TouchCrossed"}

        ' ── Caps (bytes or counts; every one is printed at start) ───────────────────────
        Private Const MaxMessageBytes As Integer = 16 * 1024 * 1024   ' one WS message; a raw snapshot is ~100 KB
        Private Const RotateBytes As Long = 64L * 1024 * 1024          ' per output file
        Private Const RingCap As Integer = 8192                        ' raw change_id -> top-10 hash
        Private Const PendingCap As Integer = 512                      ' 100 ms snapshots awaiting their raw change
        Private Const EpisodeMemCap As Integer = 300000                ' in-memory summary records (~80 B each)
        Private _outputCapBytes As Long = 2L * 1024 * 1024 * 1024      ' --output-cap-mb
        Private _heapStopBytes As Long = 160L * 1024 * 1024            ' --heap-stop-mb

        ' ── Run arguments ────────────────────────────────────────────────────────────────
        Private _seconds As Integer = 0
        Private _outDir As String = "."
        Private _envFile As String = Nothing
        Private _settingsPath As String = Nothing
        Private _sampleSec As Integer = 1
        Private _clientId As String = ""
        Private _clientSecret As String = ""

        ' ── State; every field below is touched only under _gate ────────────────────────
        Private ReadOnly _gate As New Object()
        Private ReadOnly _arms As Arm() = {New Arm(ArmA, "A"), New Arm(ArmB, "B"), New Arm(ArmC, "C")}
        Private _absCfg As AbsorptionSettings = Nothing
        Private _gen As Integer = 0                    ' bumps on every probe reset (connect, raw gap)
        Private _levelsSet As Boolean = False
        Private _lvl As Double() = New Double(6) {}    ' swingH, swingL, hvnA, hvnB, prox, band, brk
        Private _levelsAtMs As Long = 0

        Private ReadOnly _rawBids As New SortedDictionary(Of Double, Double)(Comparer(Of Double).Create(Function(a, b) b.CompareTo(a)))
        Private ReadOnly _rawAsks As New SortedDictionary(Of Double, Double)()
        Private _rawValid As Boolean = False
        Private _rawLastChange As Long = 0
        Private ReadOnly _ring As New Dictionary(Of Long, Integer)()
        Private ReadOnly _ringOrder As New Queue(Of Long)()
        Private ReadOnly _ringIds As New List(Of Long)()
        Private ReadOnly _pending As New List(Of (ChangeId As Long, Hash As Integer))()
        Private _agreeMatch As Long, _agreeMismatch As Long, _agreeUnresolved As Long, _agreeDropped As Long
        Private _agreePriorMatch As Long, _agreePriorMismatch As Long

        Private ReadOnly _msgs As Long() = {0, 0, 0, 0}
        Private ReadOnly _subOk As Boolean() = {False, False, False, False}
        Private ReadOnly _subIds As New Dictionary(Of Integer, Integer)()
        Private _rawGaps As Long, _reconnects As Long, _levelFails As Long, _levelOks As Long
        Private ReadOnly _episodes As New List(Of EpisodeRec)()
        Private _episodesDropped As Long

        Private _fatal As Integer = 0
        Private _authId As Integer = 0
        Private _authFailures As Integer = 0
        Private _nextId As Integer = 1
        Private _runStamp As String = ""
        Private _startUtc As DateTime
        Private _collectCts As CancellationTokenSource = Nothing

        ' ── Writers ──────────────────────────────────────────────────────────────────────
        Private ReadOnly _writers As New Dictionary(Of String, StreamWriter)()
        Private ReadOnly _writerPart As New Dictionary(Of String, Integer)()
        Private _bytesClosed As Long = 0

        Private Const ExitBadArgs As Integer = 1
        Private Const ExitAuthFailed As Integer = 3
        Private Const ExitRefused As Integer = 4
        Private Const ExitHeapCap As Integer = 5
        Private Const ExitOutputCap As Integer = 6

        Private Declare Function SetThreadExecutionState Lib "kernel32.dll" (esFlags As UInteger) As UInteger

        Public Function Main(args As String()) As Integer
            If Not ParseArgs(args) Then
                Console.Error.WriteLine("usage: RawBookProbe [--seconds N (0 = until STOP)] [--out DIR] [--env-file PATH] " &
                                        "[--settings PATH] [--sample-sec N] [--heap-stop-mb N] [--output-cap-mb N]")
                Return ExitBadArgs
            End If
            If Not LoadCredentials() Then Return ExitBadArgs

            Directory.CreateDirectory(_outDir)
            If String.IsNullOrEmpty(_settingsPath) Then _settingsPath = Path.Combine(AppContext.BaseDirectory, "settings.json")
            If Not File.Exists(_settingsPath) Then
                Console.Error.WriteLine("STOP: settings file not found: " & _settingsPath & " (the probe never writes a default one)")
                Return ExitBadArgs
            End If
            SettingsLoader.Initialise(_settingsPath)
            _absCfg = SettingsLoader.Current.Indicators.Absorption

            KeepAwake()
            Try
                Return RunAsync().GetAwaiter().GetResult()
            Catch ex As Exception
                Console.Error.WriteLine("FATAL: " & ex.GetType().Name & ": " & ex.Message)
                Return 2
            Finally
                CloseWriters()
            End Try
        End Function

        Private Function ParseArgs(args As String()) As Boolean
            Dim i As Integer = 0
            While i < args.Length
                Dim a As String = args(i)
                Dim v As String = If(i + 1 < args.Length, args(i + 1), Nothing)
                Select Case a
                    Case "--seconds" : If Not Integer.TryParse(v, _seconds) OrElse _seconds < 0 Then Return False
                    Case "--out" : If v Is Nothing Then Return False Else _outDir = v
                    Case "--env-file" : If v Is Nothing Then Return False Else _envFile = v
                    Case "--settings" : If v Is Nothing Then Return False Else _settingsPath = v
                    Case "--sample-sec" : If Not Integer.TryParse(v, _sampleSec) OrElse _sampleSec < 1 Then Return False
                    Case "--heap-stop-mb"
                        Dim mb As Integer
                        If Not Integer.TryParse(v, mb) OrElse mb < 32 Then Return False
                        _heapStopBytes = CLng(mb) * 1024 * 1024
                    Case "--output-cap-mb"
                        Dim mb As Integer
                        If Not Integer.TryParse(v, mb) OrElse mb < 16 Then Return False
                        _outputCapBytes = CLng(mb) * 1024 * 1024
                    Case Else
                        Return False
                End Select
                i += 2
            End While
            Return True
        End Function

        ''' <summary>Reads the key from --env-file (KEY=VALUE lines) or the environment. Prints lengths only.</summary>
        Private Function LoadCredentials() As Boolean
            If Not String.IsNullOrEmpty(_envFile) Then
                If Not File.Exists(_envFile) Then
                    Console.Error.WriteLine("STOP: --env-file not found: " & _envFile)
                    Return False
                End If
                If Not OperatingSystem.IsWindows() Then
                    Dim mode As UnixFileMode = File.GetUnixFileMode(_envFile)
                    If (mode And (UnixFileMode.GroupRead Or UnixFileMode.OtherRead Or UnixFileMode.GroupWrite Or UnixFileMode.OtherWrite)) <> 0 Then
                        Console.Error.WriteLine("WARNING: " & _envFile & " is readable by group or other; it should be mode 600")
                    End If
                End If
                For Each line As String In File.ReadAllLines(_envFile)
                    Dim t As String = line.Trim()
                    If t.Length = 0 OrElse t.StartsWith("#") Then Continue For
                    If t.StartsWith("export ") Then t = t.Substring(7).Trim()
                    Dim eq As Integer = t.IndexOf("="c)
                    If eq <= 0 Then Continue For
                    Dim k As String = t.Substring(0, eq).Trim()
                    Dim v As String = t.Substring(eq + 1).Trim().Trim(""""c, "'"c)
                    If k = EnvId Then _clientId = v
                    If k = EnvSecret Then _clientSecret = v
                Next
            Else
                _clientId = If(Environment.GetEnvironmentVariable(EnvId), "")
                _clientSecret = If(Environment.GetEnvironmentVariable(EnvSecret), "")
            End If
            If _clientId.Length = 0 OrElse _clientSecret.Length = 0 Then
                Console.Error.WriteLine("STOP: " & EnvId & " and " & EnvSecret & " must both be set" &
                                        If(String.IsNullOrEmpty(_envFile), " in the environment", " in " & _envFile) & ". Nothing was sent.")
                Return False
            End If
            Console.WriteLine("credentials present: id length " & _clientId.Length & ", secret length " & _clientSecret.Length &
                              " (values never printed; source " & If(String.IsNullOrEmpty(_envFile), "environment", "env file") & ")")
            Return True
        End Function

        Private Sub KeepAwake()
            Try
                If OperatingSystem.IsWindows() Then SetThreadExecutionState(&H80000001UI)
            Catch
            End Try
        End Sub

        Private Async Function RunAsync() As Task(Of Integer)
            _startUtc = DateTime.UtcNow
            _runStamp = _startUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            Dim cfg As EngineSettings = SettingsLoader.Current
            Console.WriteLine("RawBookProbe — raw vs 100 ms book feed, D8 pull accounting (docs/raw-book-absorption-test-spec.md)")
            Console.WriteLine("  settings     : " & _settingsPath & " version " & cfg.Version)
            Console.WriteLine("  absorption   : enabled=" & _absCfg.Enabled & " prox=" & Fmt(_absCfg.ProximityAtrFrac) & " band=" & Fmt(_absCfg.BandAtrFrac) &
                              " brk=" & Fmt(_absCfg.BreakTolAtrFrac) & " floor=" & Fmt(_absCfg.DepletionFloorUsd) & " max_pull_frac=" & Fmt(_absCfg.MaxPullFrac))
            Console.WriteLine("  arm A        : " & ChBook100 & " + " & ChTrades100 & "  (the engine's pair)")
            Console.WriteLine("  arm B        : top-10 of rebuilt " & ChBookRaw & " + " & ChTradesRaw)
            Console.WriteLine("  arm C        : top-10 of rebuilt " & ChBookRaw & " + " & ChTrades100 & "  (diagnostic)")
            Console.WriteLine("  run          : " & If(_seconds > 0, _seconds & " s", "until a STOP file") & "; STOP file = " & Path.Combine(_outDir, "STOP"))
            Console.WriteLine("  caps         : heap stop " & (_heapStopBytes \ (1024 * 1024)) & " MB | output " & (_outputCapBytes \ (1024 * 1024)) &
                              " MB | rotate " & (RotateBytes \ (1024 * 1024)) & " MB/file | msg " & (MaxMessageBytes \ (1024 * 1024)) & " MB | ring " & RingCap &
                              " | pending " & PendingCap & " | episodes in memory " & EpisodeMemCap)
            Console.WriteLine("  started      : " & _startUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & " UTC, stamp " & _runStamp)
            Console.WriteLine()

            OpenWriters()
            WriteEvent("start settings_version=" & cfg.Version & " floor=" & Fmt(_absCfg.DepletionFloorUsd) & " max_pull_frac=" & Fmt(_absCfg.MaxPullFrac))

            _collectCts = If(_seconds > 0, New CancellationTokenSource(TimeSpan.FromSeconds(_seconds)), New CancellationTokenSource())
            AddHandler Console.CancelKeyPress,
                Sub(sender As Object, e As ConsoleCancelEventArgs)
                    e.Cancel = True
                    Try
                        _collectCts.Cancel()
                    Catch
                    End Try
                End Sub

            Dim ct As CancellationToken = _collectCts.Token
            Dim wsTask As Task = WsSupervisorAsync(ct)
            Dim levelTask As Task = LevelLoopAsync(ct)
            Dim tickTask As Task = TickLoopAsync(ct)
            Try
                Await Task.WhenAll(wsTask, levelTask, tickTask)
            Catch ex As OperationCanceledException
            Catch ex As Exception
                Console.Error.WriteLine("task error: " & ex.GetType().Name & ": " & ex.Message)
            End Try

            SyncLock _gate
                FlushOpenEpisodes_Locked(UtcMs(), "PROBE_END")
                WriteEvent("end fatal=" & _fatal)
            End SyncLock
            PrintStatus()
            Report()
            CloseWriters()
            Return _fatal
        End Function

        ' ── WebSocket ────────────────────────────────────────────────────────────────────
        Private Async Function WsSupervisorAsync(ct As CancellationToken) As Task
            Dim backoff As Integer = 2
            While Not ct.IsCancellationRequested AndAlso _fatal = 0
                Dim failure As String = Nothing
                Try
                    Using ws As New ClientWebSocket()
                        ws.Options.Proxy = Nothing   ' WPAD auto-detect hangs a connect (584c616)
                        Await ws.ConnectAsync(New Uri(WsUrl), ct)
                        SyncLock _gate
                            _subIds.Clear()
                            For i As Integer = 0 To Channels.Length - 1
                                _subOk(i) = False
                            Next
                            ProbeReset_Locked(UtcMs(), "CONNECT")
                        End SyncLock
                        Console.WriteLine("[" & NowUtc() & "] ws connected; authenticating before any subscribe")
                        _authId = NextId()
                        Await SendAsync(ws, AuthRequest(_authId), ct)
                        Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() &
                                            ",""method"":""public/set_heartbeat"",""params"":{""interval"":" & HeartbeatSec & "}}", ct)
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
                    _reconnects += 1
                    WriteEvent("ws_drop " & If(failure, "closed"))
                End SyncLock
                Console.Error.WriteLine("[" & NowUtc() & "] ws dropped" & If(failure Is Nothing, "", ": " & failure) & " — reconnecting in " & backoff & "s")
                Try
                    Await Task.Delay(TimeSpan.FromSeconds(backoff), ct)
                Catch ex As OperationCanceledException
                    Return
                End Try
                backoff = Math.Min(backoff * 2, 60)
            End While
        End Function

        ''' <summary>The returned text carries the secret: send it, NEVER log it.</summary>
        Private Function AuthRequest(id As Integer) As String
            Return "{""jsonrpc"":""2.0"",""id"":" & id & ",""method"":""public/auth"",""params"":{""grant_type"":""client_credentials""," &
                   """client_id"":" & JsonSerializer.Serialize(_clientId) & ",""client_secret"":" & JsonSerializer.Serialize(_clientSecret) & "}}"
        End Function

        Private Async Function ReceiveLoopAsync(ws As ClientWebSocket, ct As CancellationToken) As Task
            Dim buffer(65535) As Byte
            Dim ms As New MemoryStream()
            While ws.State = WebSocketState.Open AndAlso Not ct.IsCancellationRequested AndAlso _fatal = 0
                Dim res As WebSocketReceiveResult = Await ws.ReceiveAsync(New ArraySegment(Of Byte)(buffer), ct)
                If res.MessageType = WebSocketMessageType.Close Then Exit While
                ms.Write(buffer, 0, res.Count)
                If ms.Length > MaxMessageBytes Then Throw New InvalidOperationException("one WS message exceeded " & MaxMessageBytes & " bytes")
                If Not res.EndOfMessage Then Continue While
                Dim recvMs As Long = UtcMs()
                Dim root As JsonElement
                Try
                    Using doc As JsonDocument = JsonDocument.Parse(New ReadOnlyMemory(Of Byte)(ms.GetBuffer(), 0, CInt(ms.Length)))
                        root = doc.RootElement.Clone()
                    End Using
                Catch
                    ms.SetLength(0)
                    Continue While
                End Try
                ms.SetLength(0)
                Await HandleAsync(ws, root, recvMs, ct)
            End While
        End Function

        Private Async Function HandleAsync(ws As ClientWebSocket, root As JsonElement, recvMs As Long, ct As CancellationToken) As Task
            Dim methodEl As JsonElement = Nothing
            If Not root.TryGetProperty("method", methodEl) Then
                Await HandleResponseAsync(ws, root, ct)
                Return
            End If
            Dim m As String = If(methodEl.GetString(), "")
            If m = "heartbeat" Then
                Dim p As JsonElement = Nothing, t As JsonElement = Nothing
                If root.TryGetProperty("params", p) AndAlso p.TryGetProperty("type", t) AndAlso t.GetString() = "test_request" Then
                    Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() & ",""method"":""public/test"",""params"":{}}", ct)
                End If
                Return
            End If
            If m <> "subscription" Then Return
            Dim prm As JsonElement = Nothing, chEl As JsonElement = Nothing, data As JsonElement = Nothing
            If Not root.TryGetProperty("params", prm) OrElse Not prm.TryGetProperty("channel", chEl) OrElse
               Not prm.TryGetProperty("data", data) Then Return
            Dim ch As String = If(chEl.GetString(), "")
            Dim needResub As Boolean = False
            SyncLock _gate
                Select Case ch
                    Case ChBook100
                        _msgs(0) += 1
                        OnBook100_Locked(data, recvMs)
                    Case ChBookRaw
                        _msgs(1) += 1
                        needResub = Not OnBookRaw_Locked(data, recvMs)
                    Case ChTrades100
                        _msgs(2) += 1
                        OnTrades_Locked(data, recvMs, {_arms(ArmA), _arms(ArmC)})
                    Case ChTradesRaw
                        _msgs(3) += 1
                        OnTrades_Locked(data, recvMs, {_arms(ArmB)})
                End Select
            End SyncLock
            If needResub Then
                ' A broken change chain: drop the raw subscription and take a fresh snapshot.
                Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & NextId() & ",""method"":""public/unsubscribe"",""params"":{""channels"":[""" & ChBookRaw & """]}}", ct)
                Await SendSubscribeAsync(ws, 1, ct)
            End If
        End Function

        ''' <summary>Auth and subscribe responses. The auth frame is summarised, never printed.</summary>
        Private Async Function HandleResponseAsync(ws As ClientWebSocket, root As JsonElement, ct As CancellationToken) As Task
            Dim id As Integer = -1
            Dim idEl As JsonElement = Nothing
            If root.TryGetProperty("id", idEl) AndAlso idEl.ValueKind = JsonValueKind.Number Then
                If Not idEl.TryGetInt32(id) Then id = -1
            End If
            Dim errEl As JsonElement = Nothing
            Dim hasErr As Boolean = root.TryGetProperty("error", errEl)

            If id <> -1 AndAlso id = _authId Then
                If hasErr Then
                    _authFailures += 1
                    Console.Error.WriteLine("[" & NowUtc() & "] auth FAILED: " & ErrText(errEl))
                    If _authFailures >= 3 Then
                        _fatal = ExitAuthFailed
                        Console.Error.WriteLine("[" & NowUtc() & "] STOP: 3 auth failures — check the key; do not retry blindly")
                        Try
                            _collectCts.Cancel()
                        Catch
                        End Try
                    End If
                    Throw New InvalidOperationException("auth failed")
                End If
                _authFailures = 0
                Dim expiresIn As Integer = 0
                Dim resEl As JsonElement = Nothing, v As JsonElement = Nothing
                If root.TryGetProperty("result", resEl) AndAlso resEl.ValueKind = JsonValueKind.Object AndAlso
                   resEl.TryGetProperty("expires_in", v) AndAlso v.ValueKind = JsonValueKind.Number Then
                    If Not v.TryGetInt32(expiresIn) Then expiresIn = 0
                End If
                Console.WriteLine("[" & NowUtc() & "] auth OK, expires_in=" & expiresIn)
                For i As Integer = 0 To Channels.Length - 1
                    Await SendSubscribeAsync(ws, i, ct)
                Next
                Return
            End If

            Dim ci As Integer = -1
            SyncLock _gate
                If id <> -1 AndAlso Not _subIds.TryGetValue(id, ci) Then ci = -1
            End SyncLock
            If ci < 0 Then
                If hasErr Then Console.Error.WriteLine("[" & NowUtc() & "] ws error frame: " & ErrText(errEl))
                Return
            End If
            ' ⚠ A subscribe to a channel that does not exist returns result = [] with NO error
            ' (measured 2026-10-05 on book.BTC-PERPETUAL.none.10.raw). Accept only an echo.
            Dim accepted As Boolean = False
            Dim resArr As JsonElement = Nothing
            If Not hasErr AndAlso root.TryGetProperty("result", resArr) AndAlso resArr.ValueKind = JsonValueKind.Array Then
                For Each e As JsonElement In resArr.EnumerateArray()
                    If e.ValueKind = JsonValueKind.String AndAlso e.GetString() = Channels(ci) Then accepted = True
                Next
            End If
            If accepted Then
                SyncLock _gate
                    _subOk(ci) = True
                    WriteEvent("subscribed " & Channels(ci))
                End SyncLock
                Console.WriteLine("[" & NowUtc() & "] subscribe ACCEPTED for " & Channels(ci))
            Else
                _fatal = ExitRefused
                Console.Error.WriteLine("[" & NowUtc() & "] STOP: subscribe NOT accepted for " & Channels(ci) & ": " &
                                        If(hasErr, ErrText(errEl), "result did not echo the channel") & ". The arms cannot be compared without it.")
                Try
                    _collectCts.Cancel()
                Catch
                End Try
            End If
        End Function

        Private Async Function SendSubscribeAsync(ws As ClientWebSocket, ci As Integer, ct As CancellationToken) As Task
            Dim subId As Integer = NextId()
            SyncLock _gate
                _subIds(subId) = ci
            End SyncLock
            Await SendAsync(ws, "{""jsonrpc"":""2.0"",""id"":" & subId & ",""method"":""public/subscribe"",""params"":{""channels"":[""" & Channels(ci) & """]}}", ct)
        End Function

        ' ── Feed handlers (caller holds _gate) ───────────────────────────────────────────

        ''' <summary>Arm A's book: the engine's grouped top-10 snapshot, parsed as DeribitWsFeed.ApplyBook does.</summary>
        Private Sub OnBook100_Locked(data As JsonElement, recvMs As Long)
            Dim snap As New OrderBookSnapshot()
            Dim bids As JsonElement = Nothing, asks As JsonElement = Nothing
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
            ' Reconstruction check against the rebuilt raw book at the same change_id.
            Dim cid As JsonElement = Nothing
            If data.TryGetProperty("change_id", cid) AndAlso cid.ValueKind = JsonValueKind.Number Then
                CheckAgreement_Locked(cid.GetInt64(), BookHash(snap))
            End If
            FoldBook_Locked(_arms(ArmA), snap, recvMs)
        End Sub

        ''' <summary>The raw book: apply one snapshot or change; fold the top 10 into arms B and C.
        ''' Returns False when the change chain broke and a re-subscribe is needed.</summary>
        Private Function OnBookRaw_Locked(data As JsonElement, recvMs As Long) As Boolean
            Dim typeEl As JsonElement = Nothing, cidEl As JsonElement = Nothing, prevEl As JsonElement = Nothing
            Dim typ As String = If(data.TryGetProperty("type", typeEl), typeEl.GetString(), "")
            If Not data.TryGetProperty("change_id", cidEl) Then Return True
            Dim cid As Long = cidEl.GetInt64()
            If typ = "snapshot" Then
                _rawBids.Clear() : _rawAsks.Clear()
                ApplyRawSide(data, "bids", _rawBids)
                ApplyRawSide(data, "asks", _rawAsks)
                _rawValid = True
                _rawLastChange = cid
                WriteEvent("raw_snapshot change_id=" & cid & " bids=" & _rawBids.Count & " asks=" & _rawAsks.Count)
            Else
                If Not _rawValid Then Return True               ' waiting for the snapshot
                Dim prev As Long = If(data.TryGetProperty("prev_change_id", prevEl), prevEl.GetInt64(), -1L)
                If prev <> _rawLastChange Then
                    _rawGaps += 1
                    _rawValid = False
                    WriteEvent("raw_gap prev_change_id=" & prev & " expected=" & _rawLastChange & " — resubscribing; all arms reset")
                    ProbeReset_Locked(recvMs, "RAW_GAP")
                    Return False
                End If
                ApplyRawSide(data, "bids", _rawBids)
                ApplyRawSide(data, "asks", _rawAsks)
                _rawLastChange = cid
            End If

            Dim snap As OrderBookSnapshot = RawTop10()
            RecordRing_Locked(cid, BookHash(snap))
            FoldBook_Locked(_arms(ArmB), snap, recvMs)
            FoldBook_Locked(_arms(ArmC), snap, recvMs)
            Return True
        End Function

        Private Sub ApplyRawSide(data As JsonElement, name As String, book As SortedDictionary(Of Double, Double))
            Dim arr As JsonElement = Nothing
            If Not data.TryGetProperty(name, arr) OrElse arr.ValueKind <> JsonValueKind.Array Then Return
            For Each e As JsonElement In arr.EnumerateArray()
                Dim action As String = e(0).GetString()
                Dim price As Double = e(1).GetDouble()
                Dim amount As Double = e(2).GetDouble()
                If action = "delete" OrElse amount <= 0 Then
                    book.Remove(price)
                Else
                    book(price) = amount
                End If
            Next
        End Sub

        Private Function RawTop10() As OrderBookSnapshot
            Dim s As New OrderBookSnapshot()
            For Each kv In _rawBids
                s.Bids.Add((kv.Key, kv.Value))
                If s.Bids.Count >= 10 Then Exit For
            Next
            For Each kv In _rawAsks
                s.Asks.Add((kv.Key, kv.Value))
                If s.Asks.Count >= 10 Then Exit For
            Next
            Return s
        End Function

        Private Function BookHash(s As OrderBookSnapshot) As Integer
            Dim h As New HashCode()
            For Each l In s.Bids
                h.Add(l.Price) : h.Add(l.Size)
            Next
            h.Add(-1.0)
            For Each l In s.Asks
                h.Add(l.Price) : h.Add(l.Size)
            Next
            Return h.ToHashCode()
        End Function

        Private Sub RecordRing_Locked(cid As Long, hash As Integer)
            _ring(cid) = hash
            _ringOrder.Enqueue(cid)
            _ringIds.Add(cid)                  ' change_ids rise, so this list stays sorted
            While _ringOrder.Count > RingCap
                _ring.Remove(_ringOrder.Dequeue())
                _ringIds.RemoveAt(0)
            End While
            ' Resolve 100 ms snapshots that were waiting for this change.
            If _pending.Count > 0 Then
                For i As Integer = _pending.Count - 1 To 0 Step -1
                    Dim p = _pending(i)
                    If p.ChangeId <= cid Then
                        Resolve_Locked(p.ChangeId, p.Hash)
                        _pending.RemoveAt(i)
                    End If
                Next
            End If
        End Sub

        ''' <summary>Compare a 100 ms snapshot with the rebuilt raw top 10 at its change_id. A grouped
        ''' snapshot can name a change_id that no raw notification carries (the raw channel may
        ''' batch); then the raw state at the latest change_id BEFORE it is the same book, and the
        ''' comparison is counted separately as "prior".</summary>
        Private Sub Resolve_Locked(cid As Long, hash As Integer)
            Dim rh As Integer
            If _ring.TryGetValue(cid, rh) Then
                If rh = hash Then _agreeMatch += 1 Else _agreeMismatch += 1
                Return
            End If
            Dim idx As Integer = _ringIds.BinarySearch(cid)
            Dim prior As Integer = If(idx >= 0, idx, (Not idx) - 1)
            If prior >= 0 Then
                If _ring(_ringIds(prior)) = hash Then _agreePriorMatch += 1 Else _agreePriorMismatch += 1
            Else
                _agreeUnresolved += 1
            End If
        End Sub

        Private Sub CheckAgreement_Locked(cid As Long, hash As Integer)
            If Not _rawValid Then
                _agreeUnresolved += 1
                Return
            End If
            If _ring.ContainsKey(cid) OrElse cid < _rawLastChange Then
                Resolve_Locked(cid, hash)
            ElseIf cid > _rawLastChange Then
                If _pending.Count >= PendingCap Then
                    _agreeDropped += 1
                Else
                    _pending.Add((cid, hash))
                End If
            Else
                _agreeUnresolved += 1
            End If
        End Sub

        ''' <summary>Trades fold exactly as DeribitWsFeed.ApplyTrades feeds the tracker: price, USD
        ''' amount, buy flag, the venue's millisecond stamp.</summary>
        Private Sub OnTrades_Locked(data As JsonElement, recvMs As Long, arms As Arm())
            If data.ValueKind <> JsonValueKind.Array OrElse Not _levelsSet Then Return
            For Each t As JsonElement In data.EnumerateArray()
                Dim price As Double = t.GetProperty("price").GetDouble()
                Dim amount As Double = t.GetProperty("amount").GetDouble()
                Dim isBuy As Boolean = t.GetProperty("direction").GetString() = "buy"
                Dim ts As Long = t.GetProperty("timestamp").GetInt64()
                For Each a As Arm In arms
                    a.Tracker.FoldTrade(price, amount, isBuy, ts, _absCfg)
                    a.TradeFolds += 1
                    Observe_Locked(a, recvMs, False)
                Next
            Next
        End Sub

        Private Sub FoldBook_Locked(a As Arm, snap As OrderBookSnapshot, recvMs As Long)
            If Not _levelsSet Then Return
            a.Tracker.FoldBook(snap, recvMs, _absCfg)
            a.BookFolds += 1
            Observe_Locked(a, recvMs, True)
        End Sub

        ' ── Episode observer (caller holds _gate) ────────────────────────────────────────

        ''' <summary>Reads the tracker after a fold and turns active→idle (or a level change) into a
        ''' closed-episode row. The row carries the LAST read taken while the episode was active:
        ''' the most-accumulated value any run-time read of that episode could have seen.</summary>
        Private Sub Observe_Locked(a As Arm, nowMs As Long, isBook As Boolean)
            Dim snap As AbsorptionSnapshot = a.Tracker.Snapshot(nowMs, _absCfg)
            Dim ended0 As Boolean = ObserveSide_Locked(a, 0, snap.Above, nowMs, isBook)
            Dim ended1 As Boolean = ObserveSide_Locked(a, 1, snap.Below, nowMs, isBook)
            If ended0 OrElse ended1 Then
                ' Drain the close-reason tallies; closes only tally on an ACTIVE side, so the
                ' sides that just ended are exactly the ones with counts here.
                Dim ins As AbsorptionInstrumentRead = a.Tracker.TakeInstrument(nowMs)
                If ended0 Then EmitPending_Locked(a, 0, ReasonOf(ins.Above))
                If ended1 Then EmitPending_Locked(a, 1, ReasonOf(ins.Below))
            End If
            ' Open new episodes after emitting the closed ones.
            StartIfActive_Locked(a, 0, snap.Above, nowMs)
            StartIfActive_Locked(a, 1, snap.Below, nowMs)
        End Sub

        Private ReadOnly _endedRead As EpisodeWatch() = {New EpisodeWatch(), New EpisodeWatch()}
        Private ReadOnly _endedMs As Long() = {0, 0}

        Private Function ObserveSide_Locked(a As Arm, side As Integer, rd As AbsorptionSideRead, nowMs As Long, isBook As Boolean) As Boolean
            Dim w As EpisodeWatch = a.Watch(side)
            If Not w.Active Then Return False
            Dim openNow As Long = nowMs - CLng(Math.Round(rd.EpisodeSec * 1000.0))
            Dim ended As Boolean = Not rd.Active OrElse rd.LevelPrice <> w.Level OrElse Math.Abs(openNow - w.OpenMs) > 2
            If ended Then
                ' Park the closed episode; EmitPending writes it once the reason is known.
                Dim p As EpisodeWatch = _endedRead(side)
                p.Active = True : p.Level = w.Level : p.OpenMs = w.OpenMs : p.LastRead = w.LastRead : p.BookFolds = w.BookFolds
                _endedMs(side) = nowMs
                w.Active = False
                Return True
            End If
            w.LastRead = rd
            If isBook Then w.BookFolds += 1
            Return False
        End Function

        Private Sub StartIfActive_Locked(a As Arm, side As Integer, rd As AbsorptionSideRead, nowMs As Long)
            Dim w As EpisodeWatch = a.Watch(side)
            If w.Active OrElse Not rd.Active Then Return
            w.Active = True
            w.Level = rd.LevelPrice
            w.OpenMs = nowMs - CLng(Math.Round(rd.EpisodeSec * 1000.0))
            w.LastRead = rd
            w.BookFolds = 0
        End Sub

        Private Function ReasonOf(s As AbsorptionSideInstrument) As String
            If s Is Nothing OrElse s.Tally Is Nothing Then Return "UNKNOWN"
            Dim found As String = Nothing
            For i As Integer = 0 To s.Tally.Length - 1
                If s.Tally(i).Count > 0 Then
                    If found IsNot Nothing Then Return "MULTI"
                    found = If(i < 7, ReasonNames(i), "R" & i)
                End If
            Next
            Return If(found, "UNKNOWN")
        End Function

        Private Sub EmitPending_Locked(a As Arm, side As Integer, reason As String)
            Dim p As EpisodeWatch = _endedRead(side)
            If Not p.Active Then Return
            p.Active = False
            WriteEpisode_Locked(a, side, p, _endedMs(side), reason)
        End Sub

        Private Sub WriteEpisode_Locked(a As Arm, side As Integer, w As EpisodeWatch, closeMs As Long, reason As String)
            Dim r As AbsorptionSideRead = w.LastRead
            a.Episodes += 1
            Dim probeReset As Boolean = reason.StartsWith("PROBE_")
            WriteLine("episodes", "arm,side,gen,level,open_ms,close_ms,reason,episode_sec,book_folds,aggr_usd,pull_lb,post_lb,pull_frac,size_start,size_min,absorb_ratio",
                      a.Name & "," & SideNames(side) & "," & _gen & "," & Fmt(w.Level) & "," & w.OpenMs & "," & closeMs & "," & reason & "," &
                      Fmt(r.EpisodeSec) & "," & w.BookFolds & "," & Fmt(r.AggrUsd) & "," & Fmt(r.PullLB) & "," & Fmt(r.PostLB) & "," &
                      Fmt(r.PullFrac) & "," & Fmt(r.SizeStart) & "," & Fmt(r.SizeMin) & "," & Fmt(r.AbsorbRatio))
            If _episodes.Count < EpisodeMemCap Then
                _episodes.Add(New EpisodeRec With {.Arm = a.Index, .Side = side, .Gen = _gen, .Level = w.Level, .OpenMs = w.OpenMs,
                    .CloseMs = closeMs, .Sec = r.EpisodeSec, .BookFolds = w.BookFolds, .PullLB = r.PullLB, .PostLB = r.PostLB,
                    .PullFrac = r.PullFrac, .ProbeReset = probeReset})
            Else
                _episodesDropped += 1
            End If
        End Sub

        ''' <summary>Close every open episode with a PROBE_* reason (excluded from the read).</summary>
        Private Sub FlushOpenEpisodes_Locked(nowMs As Long, reason As String)
            For Each a As Arm In _arms
                For side As Integer = 0 To 1
                    Dim w As EpisodeWatch = a.Watch(side)
                    If w.Active Then
                        WriteEpisode_Locked(a, side, w, nowMs, reason)
                        w.Active = False
                    End If
                Next
            Next
        End Sub

        ''' <summary>Reset every arm together (the engine resets its tracker on every reconnect),
        ''' then re-apply the last carried levels so the arms stay in lock-step.</summary>
        Private Sub ProbeReset_Locked(nowMs As Long, why As String)
            FlushOpenEpisodes_Locked(nowMs, "PROBE_" & why)
            _gen += 1
            For Each a As Arm In _arms
                a.Tracker.Reset()
                a.Tracker.TakeInstrument(nowMs)    ' drain the Reset tallies
                If _levelsSet Then a.Tracker.SetLevels(_lvl(0), _lvl(1), _lvl(2), _lvl(3), _lvl(4), _lvl(5), _lvl(6))
            Next
            _pending.Clear()
            WriteEvent("probe_reset " & why & " gen=" & _gen)
        End Sub

        ' ── Level carry (every minute, just after the bar close) ────────────────────────
        Private Async Function LevelLoopAsync(ct As CancellationToken) As Task
            While Not ct.IsCancellationRequested AndAlso _fatal = 0
                Try
                    Await RefreshLevelsAsync()
                Catch ex As Exception
                    SyncLock _gate
                        _levelFails += 1
                        WriteEvent("levels_fail " & ex.GetType().Name & ": " & ex.Message)
                    End SyncLock
                End Try
                Dim now As DateTime = DateTime.UtcNow
                Dim nextAt As DateTime = New DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc).AddMinutes(1).AddSeconds(2)
                Try
                    Await Task.Delay(nextAt - now, ct)
                Catch ex As OperationCanceledException
                    Return
                End Try
            End While
        End Function

        ''' <summary>The engine's own level functions on REST candles, with the run path's arguments
        ''' (UI/MainForm_Analysis.vb: CalcATR, CalcSwingPivots on 5m, CalcVPFRLite on the
        ''' execution-resolution stack, then ATR × the absorption fractions).</summary>
        Private Async Function RefreshLevelsAsync() As Task
            Dim cfg As EngineSettings = SettingsLoader.Current
            Dim nowUtc As DateTime = DateTime.UtcNow
            Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, nowUtc.Hour)
            Dim c1 As List(Of Candle) = Await DeribitClient.GetCandlesAsync("1", 250)
            Dim c5 As List(Of Candle) = Await DeribitClient.GetCandlesAsync("5", 210)
            Dim cExec As List(Of Candle) = If(execRes = 1, c1, Await DeribitClient.GetCandlesAsync(execRes.ToString(), 250))
            If c1 Is Nothing OrElse c1.Count < 50 OrElse c5 Is Nothing OrElse c5.Count < 30 OrElse cExec Is Nothing OrElse cExec.Count < 50 Then
                Throw New InvalidOperationException("candles unavailable (kept the previous levels)")
            End If
            If Not IndicatorEngine.IsFresh(cExec, execRes, nowUtc) OrElse Not IndicatorEngine.IsFresh(c5, 5, nowUtc) Then
                Throw New InvalidOperationException("candles stale (kept the previous levels)")
            End If
            Dim price As Double = cExec.Last().Close
            Dim atr As Double = IndicatorEngine.CalcATR(cExec, cfg.Indicators.ATR.Period)
            Dim sh As Double = 0, sl As Double = 0
            IndicatorEngine.CalcSwingPivots(c5, sh, sl, pivotWing:=cfg.Indicators.Swing.PivotWing5m, lookbackBars:=cfg.Indicators.Swing.LookbackBars5m)
            Dim poc As Double = 0, hvnNearPoc As Boolean = False, sig As String = "NEUTRAL"
            Dim vah As Double = 0, val As Double = 0, vaSig As String = ""
            Dim hvnA As Double = 0, hvnB As Double = 0, lvnA As Double = 0, lvnB As Double = 0
            Dim bVols() As Double = Array.Empty(Of Double)(), bLow As Double = 0, bSize As Double = 0
            IndicatorEngine.CalcVPFRLite(cExec, price, poc, hvnNearPoc, sig, vah, val, vaSig, hvnA, hvnB, lvnA, lvnB,
                                         bVols, bLow, bSize,
                                         numBuckets:=cfg.Indicators.VPFR.NumBuckets,
                                         hvnVolPct:=cfg.Indicators.VPFR.HvnVolPct,
                                         lvnVolPct:=cfg.Indicators.VPFR.LvnVolPct,
                                         hvnProximityPct:=cfg.Indicators.VPFR.HvnProximityPct,
                                         decayBase:=cfg.Indicators.VPFR.DecayBase,
                                         valueAreaPct:=cfg.Indicators.VPFR.ValueAreaPct)
            Dim ab As AbsorptionSettings = cfg.Indicators.Absorption
            Dim atrForAbs As Double = If(atr > 0.0, atr, 0.0)
            Dim prox As Double = atrForAbs * ab.ProximityAtrFrac
            Dim band As Double = atrForAbs * ab.BandAtrFrac
            Dim brk As Double = atrForAbs * ab.BreakTolAtrFrac
            SyncLock _gate
                _absCfg = ab
                _lvl = {sh, sl, hvnA, hvnB, prox, band, brk}
                For Each a As Arm In _arms
                    a.Tracker.SetLevels(sh, sl, hvnA, hvnB, prox, band, brk)
                Next
                _levelsSet = True
                _levelOks += 1
                _levelsAtMs = UtcMs()
                WriteLine("levels", "t_ms,exec_res,price,atr,swing_high_5m,swing_low_5m,hvn_above,hvn_below,prox_usd,band_usd,brk_usd",
                          _levelsAtMs & "," & execRes & "," & Fmt(price) & "," & Fmt(atr) & "," & Fmt(sh) & "," & Fmt(sl) & "," &
                          Fmt(hvnA) & "," & Fmt(hvnB) & "," & Fmt(prox) & "," & Fmt(band) & "," & Fmt(brk))
            End SyncLock
        End Function

        ' ── Paired reads, flush, status, STOP ────────────────────────────────────────────
        Private Async Function TickLoopAsync(ct As CancellationToken) As Task
            Dim lastStatus As DateTime = DateTime.UtcNow
            Dim lastSample As DateTime = DateTime.MinValue
            While Not ct.IsCancellationRequested AndAlso _fatal = 0
                Try
                    Await Task.Delay(1000, ct)
                Catch ex As OperationCanceledException
                    Exit While
                End Try
                Dim now As DateTime = DateTime.UtcNow
                SyncLock _gate
                    If (now - lastSample).TotalSeconds >= _sampleSec - 0.05 Then
                        lastSample = now
                        WriteSamples_Locked(UtcMs())
                    End If
                    For Each w As StreamWriter In _writers.Values
                        w.Flush()
                    Next
                    If TotalBytes_Locked() > _outputCapBytes Then
                        _fatal = ExitOutputCap
                        Console.Error.WriteLine("[" & NowUtc() & "] STOP: output cap reached")
                    End If
                End SyncLock
                Dim heap As Long = GC.GetGCMemoryInfo().HeapSizeBytes
                If heap > _heapStopBytes Then
                    _fatal = ExitHeapCap
                    Console.Error.WriteLine("[" & NowUtc() & "] STOP: GC heap " & (heap \ 1048576) & " MB exceeds the " & (_heapStopBytes \ 1048576) & " MB stop")
                End If
                If File.Exists(Path.Combine(_outDir, "STOP")) Then
                    Console.WriteLine("[" & NowUtc() & "] STOP file seen — ending")
                    Exit While
                End If
                If (now - lastStatus).TotalSeconds >= 60 Then
                    lastStatus = now
                    PrintStatus()
                End If
            End While
            Try
                _collectCts.Cancel()
            Catch
            End Try
        End Function

        ''' <summary>All three arms read at ONE instant — the shape the engine logs (a read at run time).</summary>
        Private Sub WriteSamples_Locked(nowMs As Long)
            If Not _levelsSet Then Return
            Dim reads(2) As AbsorptionSnapshot
            For i As Integer = 0 To 2
                reads(i) = _arms(i).Tracker.Snapshot(nowMs, _absCfg)
            Next
            For side As Integer = 0 To 1
                Dim any As Boolean = False
                For i As Integer = 0 To 2
                    If SideOf(reads(i), side).Active Then any = True
                Next
                If Not any Then Continue For
                Dim sb As New StringBuilder()
                sb.Append(nowMs).Append(","c).Append(SideNames(side)).Append(","c).Append(_gen)
                For i As Integer = 0 To 2
                    Dim r As AbsorptionSideRead = SideOf(reads(i), side)
                    sb.Append(","c).Append(If(r.Active, "1", "0")).Append(","c).Append(Fmt(r.LevelPrice)).Append(","c).Append(Fmt(r.EpisodeSec))
                    sb.Append(","c).Append(Fmt(r.AggrUsd)).Append(","c).Append(Fmt(r.PullLB)).Append(","c).Append(Fmt(r.PostLB)).Append(","c).Append(Fmt(r.PullFrac))
                Next
                WriteLine("samples", "t_ms,side,gen," &
                          "a_active,a_level,a_sec,a_aggr,a_pull_lb,a_post_lb,a_pull_frac," &
                          "b_active,b_level,b_sec,b_aggr,b_pull_lb,b_post_lb,b_pull_frac," &
                          "c_active,c_level,c_sec,c_aggr,c_pull_lb,c_post_lb,c_pull_frac", sb.ToString())
            Next
        End Sub

        Private Function SideOf(s As AbsorptionSnapshot, side As Integer) As AbsorptionSideRead
            Return If(side = 0, s.Above, s.Below)
        End Function

        Private Sub PrintStatus()
            Dim line As String
            SyncLock _gate
                Dim p As Process = Process.GetCurrentProcess()
                Dim open As New StringBuilder()
                For Each a As Arm In _arms
                    open.Append(a.Name).Append("=").Append(a.Episodes).Append("/").Append(If(a.Watch(0).Active, "1", "0")).Append(If(a.Watch(1).Active, "1", "0")).Append(" ")
                Next
                line = "[" & NowUtc() & "] up " & CInt((DateTime.UtcNow - _startUtc).TotalMinutes) & "m" &
                       " | msgs b100=" & _msgs(0) & " braw=" & _msgs(1) & " t100=" & _msgs(2) & " traw=" & _msgs(3) &
                       " | agree " & _agreeMatch & "/" & _agreeMismatch & " prior " & _agreePriorMatch & "/" & _agreePriorMismatch & " unres " & _agreeUnresolved & " drop " & _agreeDropped &
                       " | gaps " & _rawGaps & " reconn " & _reconnects & " gen " & _gen &
                       " | levels ok " & _levelOks & " fail " & _levelFails & " age " & If(_levelsAtMs > 0, CStr((UtcMs() - _levelsAtMs) \ 1000) & "s", "-") &
                       " | episodes(n/open) " & open.ToString().Trim() &
                       " | pairs A-B " & PairCountText_Locked() &
                       " | mem ws " & (p.WorkingSet64 \ 1048576) & "MB priv " & (p.PrivateMemorySize64 \ 1048576) & "MB heap " &
                       (GC.GetGCMemoryInfo().HeapSizeBytes \ 1048576) & "MB | out " & (TotalBytes_Locked() \ 1024) & "KB"
            End SyncLock
            Console.WriteLine(line)
            Try
                File.WriteAllText(Path.Combine(_outDir, "status_latest.txt"), line & Environment.NewLine)
            Catch
            End Try
        End Sub

        ''' <summary>Live count toward the pre-stated minimum: aligned A-B pairs, and those with arm A
        ''' pullFrac ≤ 1 (the minimum applies to the second number).</summary>
        Private Function PairCountText_Locked() As String
            Dim prs = AlignedPairs_Locked(ArmB)
            Return prs.Count & " (A<=1: " & prs.Where(Function(p) p.A.PullFrac <= 1.0).Count() & ")"
        End Function

        ' ── End summary (convenience only; the read re-derives everything from the CSVs) ──
        Private Sub Report()
            Dim floorUsd As Double = _absCfg.DepletionFloorUsd
            Dim maxPf As Double = _absCfg.MaxPullFrac
            Dim sb As New StringBuilder()
            sb.AppendLine()
            sb.AppendLine("==== RawBookProbe summary (" & _runStamp & ") — CONVENIENCE ONLY; the read re-derives from episodes_*.csv ====")
            sb.AppendLine("book agreement (100 ms top-10 vs rebuilt raw top-10 at the same change_id): match " & _agreeMatch & " prior-match " & _agreePriorMatch & " prior-mismatch " & _agreePriorMismatch &
                          " mismatch " & _agreeMismatch & " unresolved " & _agreeUnresolved & " dropped " & _agreeDropped)
            sb.AppendLine("raw gaps " & _rawGaps & " | reconnects " & _reconnects & " | level refresh ok " & _levelOks & " fail " & _levelFails &
                          " | episodes dropped from memory " & _episodesDropped)
            Dim inf = _episodes.Where(Function(e) IsInformative(e)).ToList()
            For i As Integer = 0 To 2
                Dim ai As Integer = i
                Dim xs = inf.Where(Function(e) e.Arm = ai).ToList()
                Dim all = _episodes.Where(Function(e) e.Arm = ai AndAlso Not e.ProbeReset).Count()
                sb.Append("arm " & _arms(i).Name & ": episodes " & all & ", informative " & xs.Count)
                If xs.Count > 0 Then
                    sb.Append(" | median pullLB " & Fmt(Median(xs.Select(Function(e) e.PullLB))) &
                              " postLB " & Fmt(Median(xs.Select(Function(e) e.PostLB))) &
                              " pullFrac " & Fmt(Median(xs.Select(Function(e) e.PullFrac))) &
                              " | pullFrac>" & Fmt(maxPf) & " " & Pct(xs.Where(Function(e) e.PullFrac > maxPf).Count(), xs.Count) &
                              " | exactly 1.000 " & Pct(xs.Where(Function(e) Math.Abs(e.PullFrac - 1.0) < 0.00005).Count(), xs.Count) &
                              " | postLB<floor " & Pct(xs.Where(Function(e) e.PostLB < floorUsd).Count(), xs.Count))
                End If
                sb.AppendLine()
            Next
            For Each other As Integer In {ArmB, ArmC}
                Dim aligned As Integer = 0, up As Integer = 0, down As Integer = 0, same As Integer = 0, passToVeto As Integer = 0, vetoToPass As Integer = 0
                Dim dPull As New List(Of Double)(), dPost As New List(Of Double)()
                For Each pr In AlignedPairs_Locked(other)
                    Dim ea As EpisodeRec = pr.A, eb As EpisodeRec = pr.B
                    aligned += 1
                    dPull.Add(eb.PullLB - ea.PullLB) : dPost.Add(eb.PostLB - ea.PostLB)
                    If ea.PullFrac <= 1.0 Then
                        If eb.PullFrac > ea.PullFrac + 0.000001 Then
                            up += 1
                        ElseIf eb.PullFrac < ea.PullFrac - 0.000001 Then
                            down += 1
                        Else
                            same += 1
                        End If
                    End If
                    If ea.PullFrac <= maxPf AndAlso eb.PullFrac > maxPf Then passToVeto += 1
                    If ea.PullFrac > maxPf AndAlso eb.PullFrac <= maxPf Then vetoToPass += 1
                Next
                sb.Append("A vs " & _arms(other).Name & ": aligned pairs " & aligned & " (the only overlapping episode, open and close within 500 ms, same level)")
                If aligned > 0 Then
                    sb.Append(" | pullFrac_A<=1: " & _arms(other).Name & " higher " & up & " lower " & down & " equal " & same &
                              " | veto flips pass->veto " & passToVeto & " veto->pass " & vetoToPass &
                              " | median dPullLB " & Fmt(Median(dPull)) & " dPostLB " & Fmt(Median(dPost)))
                End If
                sb.AppendLine()
            Next
            Dim text As String = sb.ToString()
            Console.Write(text)
            Try
                File.WriteAllText(Path.Combine(_outDir, "summary_" & _runStamp & ".txt"), text)
            Catch
            End Try
        End Sub

        ''' <summary>Arm A informative episode (raw-book-absorption-test-spec.md §5): not cut by a
        ''' probe reset, at least 1 s old at its last read, at least 2 book folds after open.</summary>
        Private Function IsInformative(e As EpisodeRec) As Boolean
            Return Not e.ProbeReset AndAlso e.Sec >= 1.0 AndAlso e.BookFolds >= 2
        End Function

        ''' <summary>Aligned pairs (raw-book-absorption-test-spec.md §5): an informative arm A episode
        ''' and the ONLY episode of the other arm with the same side, gen and level that overlaps
        ''' its span, provided that episode opens and closes within 500 ms of it.</summary>
        Private Function AlignedPairs_Locked(other As Integer) As List(Of (A As EpisodeRec, B As EpisodeRec))
            Dim res As New List(Of (A As EpisodeRec, B As EpisodeRec))()
            Dim byKey = _episodes.Where(Function(e) e.Arm = other AndAlso Not e.ProbeReset).ToLookup(Function(e) (e.Side, e.Gen, e.Level))
            For Each ea In _episodes.Where(Function(e) e.Arm = ArmA AndAlso IsInformative(e))
                Dim ov = byKey((ea.Side, ea.Gen, ea.Level)).Where(Function(e) e.OpenMs < ea.CloseMs AndAlso e.CloseMs > ea.OpenMs).ToList()
                If ov.Count <> 1 Then Continue For
                Dim eb As EpisodeRec = ov(0)
                If Math.Abs(eb.OpenMs - ea.OpenMs) > 500 OrElse Math.Abs(eb.CloseMs - ea.CloseMs) > 500 Then Continue For
                res.Add((ea, eb))
            Next
            Return res
        End Function

        Private Function Median(xs As IEnumerable(Of Double)) As Double
            Dim a = xs.OrderBy(Function(x) x).ToArray()
            If a.Length = 0 Then Return Double.NaN
            Return If(a.Length Mod 2 = 1, a(a.Length \ 2), (a(a.Length \ 2 - 1) + a(a.Length \ 2)) / 2.0)
        End Function

        Private Function Pct(n As Integer, d As Integer) As String
            If d = 0 Then Return "-"
            Return (100.0 * n / d).ToString("0.0", CultureInfo.InvariantCulture) & "% (" & n & ")"
        End Function

        ' ── Writers (caller holds _gate, except Open/Close) ─────────────────────────────
        Private Sub OpenWriters()
            ' Opened lazily per kind by WriteLine; events opens now so the run is visible at once.
            WriteEvent("writers open, out=" & Path.GetFullPath(_outDir))
        End Sub

        Private Sub WriteLine(kind As String, header As String, line As String)
            Dim w As StreamWriter = Nothing
            If _writers.TryGetValue(kind, w) AndAlso w.BaseStream.Length > RotateBytes Then
                _bytesClosed += w.BaseStream.Length
                w.Dispose()
                _writers.Remove(kind)
                w = Nothing
            End If
            If w Is Nothing Then
                Dim part As Integer = 0
                _writerPart.TryGetValue(kind, part)
                part += 1
                _writerPart(kind) = part
                Dim ext As String = If(kind = "events", ".log", ".csv")
                Dim fn As String = Path.Combine(_outDir, kind & "_" & _runStamp & If(part > 1, "_part" & part, "") & ext)
                w = New StreamWriter(New FileStream(fn, FileMode.Append, FileAccess.Write, FileShare.Read), New UTF8Encoding(False))
                _writers(kind) = w
                If header IsNot Nothing Then w.WriteLine(header)
            End If
            w.WriteLine(line)
        End Sub

        Private Sub WriteEvent(text As String)
            WriteLine("events", Nothing, DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture) & " " & text)
        End Sub

        Private Function TotalBytes_Locked() As Long
            Dim t As Long = _bytesClosed
            For Each w As StreamWriter In _writers.Values
                t += w.BaseStream.Length
            Next
            Return t
        End Function

        Private Sub CloseWriters()
            SyncLock _gate
                For Each w As StreamWriter In _writers.Values
                    Try
                        w.Flush()
                        w.Dispose()
                    Catch
                    End Try
                Next
                _writers.Clear()
            End SyncLock
        End Sub

        ' ── Helpers ──────────────────────────────────────────────────────────────────────
        Private Async Function SendAsync(ws As ClientWebSocket, json As String, ct As CancellationToken) As Task
            Dim b As Byte() = Encoding.UTF8.GetBytes(json)
            Await ws.SendAsync(New ArraySegment(Of Byte)(b), WebSocketMessageType.Text, True, ct)
        End Function

        Private Function NextId() As Integer
            Return Interlocked.Increment(_nextId)
        End Function

        Private Function UtcMs() As Long
            Return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        End Function

        Private Function NowUtc() As String
            Return DateTime.UtcNow.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
        End Function

        Private Function Fmt(d As Double) As String
            If Double.IsNaN(d) Then Return "NaN"
            Return d.ToString("0.######", CultureInfo.InvariantCulture)
        End Function

        Private Function ErrText(errEl As JsonElement) As String
            Dim code As String = "", msg As String = ""
            Dim v As JsonElement = Nothing
            If errEl.ValueKind = JsonValueKind.Object Then
                If errEl.TryGetProperty("code", v) Then code = v.ToString()
                If errEl.TryGetProperty("message", v) Then msg = v.ToString()
            End If
            Return "code " & code & " " & msg
        End Function

    End Module

End Namespace
