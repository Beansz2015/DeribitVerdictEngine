' TradeStoreGapRepair.vb
' [v64 in-app trade-store capture §1.2] The SECONDARY capture mechanism — the one thing
' streaming cannot do.
'
' Streaming capture (DeribitWsFeed.ApplyTrades) is complete while the app runs and recovers
' NOTHING from downtime. This is the mirror image: it backfills up to the venue's ~24 h
' public-trades retention after a crash, reboot or deploy. The two are deliberately NOT
' collapsible — streaming without repair loses every restart; repair without streaming
' reinstates the 24-hour deadline the whole build exists to remove.
'
' ⚠ BOTH HALVES OF THAT SENTENCE WERE FALSIFIED, and the corrections are recorded here rather
' than left in the specs, because this comment is what a reader trusts:
'
'   • "complete while the app runs" — FALSE until 2026-08-11. The write guard keyed on a
'     millisecond and silently discarded 49.2 % of the tape (see TradeStoreWriter's own note).
'   • "recovers ... from downtime" — TRUE ONLY IF THE APP RESTARTS. A pass resumed from the
'     file's LAST WRITTEN ROW, which streaming makes current again within seconds of
'     reconnecting, so an app that RIDES THROUGH an outage lost the hole permanently. Measured:
'     a 60.3-minute hole on 2026-08-11 survived two scheduled passes and aged out of retention.
'     Fixed by passing repairHoles:=True below — the pass now fetches the trade_seq-bracketed
'     holes BEHIND the tail as well as the tail itself
'     (docs/trade-store-downtime-repair-proposal.md Part A).
'
' D1 ruled AWS-ONLY, which makes this the ONLY recovery mechanism there is: with no second
' box, an AWS outage means the stream stops and nothing else is capturing. So per §7.1 the
' first pass fires ONCE ON START rather than waiting a full interval — a restart is precisely
' when a gap exists. That is a build requirement, not a nicety.
'
' Overlap is a no-op BY CONSTRUCTION: HistoricalStore's trade backfill resumes from the last
' on-disk timestamp, which after streaming is seconds old, so a repay pass over already-
' captured ground fetches nothing (A48d).
'
' Host-agnostic — no WinForms, no Control.Invoke, no MainForm. Runs on a plain
' System.Threading.Timer so the Linux CLI port drives it unchanged. NEVER THROWS.
'
' Runs independently of transport: with network.transport = "rest" there is no WS stream and
' therefore no streaming capture, and repair alone carries the store (§5).

Imports System.Threading
Imports System.Threading.Tasks

Public NotInheritable Class TradeStoreGapRepair

    Private _timer As Timer
    ' 0 = idle, 1 = a pass is in flight. A 20 h backfill after a long outage can outlast the
    ' interval, and two concurrent passes on one file would both resume from the same cursor.
    Private _running As Integer = 0
    Private _stopped As Boolean = False

    ''' <summary>Total rows appended by repair passes this process. Observational.</summary>
    Public ReadOnly Property TotalRowsRepaired As Integer
        Get
            Return _totalRows
        End Get
    End Property
    Private _totalRows As Integer = 0

    ''' <summary>
    ''' Start the repair schedule: one pass IMMEDIATELY (§7.1), then every
    ''' `gap_repair_interval_hours`. Idempotent. Does nothing when capture or repair is
    ''' disabled — but note the config is re-checked on every tick, so a hot-reload that
    ''' turns repair on takes effect at the next interval without a restart.
    ''' </summary>
    Public Sub Start()
        If _timer IsNot Nothing OrElse _stopped Then Return
        Dim ts = SettingsLoader.Current.TradeStore
        If Not TradeStoreWriter.ShouldGapRepair(ts) Then Return
        Dim periodMs As Long = CLng(Math.Max(0.25, ts.GapRepairIntervalHours) * 3600.0 * 1000.0)
        ' dueTime 0 ⇒ fire once on start.
        _timer = New Timer(AddressOf OnTick, Nothing, 0, periodMs)
    End Sub

    ''' <summary>Stop the schedule. Any in-flight pass finishes on its own thread.</summary>
    Public Sub [Stop]()
        _stopped = True
        Try
            _timer?.Dispose()
        Catch
        End Try
        _timer = Nothing
    End Sub

    Private Sub OnTick(state As Object)
        If Interlocked.CompareExchange(_running, 1, 0) <> 0 Then
            Console.WriteLine("[TradeStoreGapRepair] previous pass still running — skipping this tick")
            Return
        End If
        Task.Run(Async Function()
                     Try
                         Dim n As Integer = Await RepairOnceAsync(SettingsLoader.Current)
                         Interlocked.Add(_totalRows, n)
                     Catch ex As Exception
                         Console.Error.WriteLine("[TradeStoreGapRepair] pass failed: " & ex.Message)
                     Finally
                         Interlocked.Exchange(_running, 0)
                     End Try
                 End Function)
    End Sub

    ''' <summary>
    ''' One repair pass over the last `gap_repair_lookback_hours`. Returns rows appended
    ''' (0 when disabled, when the store is already current, or on any failure). Never throws.
    '''
    ''' The lookback bounds how far back the resume cursor may reach: Deribit refuses windows
    ''' past its ~24 h retention, so after a long outage an unclamped resume would ask for a
    ''' refused window and recover nothing — including the last 20 h that ARE still served.
    ''' That clamp is why `clampToSegStart:=True` is passed here and nowhere else.
    ''' </summary>
    Public Shared Async Function RepairOnceAsync(cfg As EngineSettings) As Task(Of Integer)
        Try
            If cfg Is Nothing Then Return 0
            Dim ts = cfg.TradeStore
            If Not TradeStoreWriter.ShouldGapRepair(ts) Then Return 0

            Dim storeDir As String = TradeStoreWriter.ResolveStoreDir(ts.StoreDir)
            Dim toUtc As DateTime = DateTime.UtcNow
            Dim fromUtc As DateTime = toUtc.AddHours(-Math.Max(0.5, ts.GapRepairLookbackHours))

            Dim total As Integer = 0
            For Each m In HistoricalStore.EnumerateMonths(fromUtc, toUtc)
                ' [downtime repair Part A, D-1/D-5] repairHoles:=True is what makes this pass
                ' able to heal an outage the app RODE THROUGH. Without it the fetch resumes from
                ' the file's last written row, which streaming makes current again within
                ' seconds of reconnecting — so every hole behind it reads as already covered and
                ' is never fetched. This is the ONLY caller that opts in; the historical backfill
                ' keeps the default False and is byte-identical.
                total += Await HistoricalStore.BackfillTradeMonthAsync(
                             m.Year, m.Month, m.StartUtc, m.EndUtcExcl,
                             storeDir:=storeDir, clampToSegStart:=True, repairHoles:=True)
            Next
            Console.WriteLine(String.Format(
                "[TradeStoreGapRepair] pass complete — {0} row(s) appended to {1}", total, storeDir))
            Return total
        Catch ex As Exception
            Console.Error.WriteLine("[TradeStoreGapRepair] RepairOnceAsync error: " & ex.Message)
            Return 0
        End Try
    End Function

End Class
