' UI/MainForm_TapeStoreStatus.vb
' Live TAPE STORE status strip (C1 Session 2 / Part B,
' docs/trade-store-coverage-report-proposal.md §4) — thin WinForms host.
'
' A System.Windows.Forms.Timer (UI-thread) ticks every trade_store.flush_seconds — no new
' settings key: checking at the flush interval matches the feature's own subject matter, and
' the brief's own guidance is that Part B costs no version bump if it derives its cadence and
' thresholds from the existing key. Started ONCE at form load (the constructor) and disposed
' on form close — independent of auto-run, the exit-guard timer, and the live microstructure
' strip. Each tick self-gates: hidden when trade_store.enabled is false (nothing to report on
' a non-capturing box — e.g. the local box under D1's AWS-only ruling). Reads
' DeribitWsFeed.GetTradeStoreStatus() — a read plus a label over state TradeStoreWriter
' already tracks — and colours via the shared, host-agnostic
' TradeStoreWriter.ClassifyTapeStoreTier (amber past 3× flush_seconds since the last
' successful flush, red past 10×; that function takes no date/day-of-week input at all, which
' is what keeps Part B unconditional on weekends per weekday-scope-ruling-2026-08-03.md — an
' element with nothing to suppress a Saturday/Sunday reading with, since it never asks).
'
' DISPLAY/AWARENESS ONLY — no scoring, no CSV, no card/snapshot binding (the v62 MIN NET
' MOVE % / EXIT GUARD strip precedent — a live status element is display-parity exempt).

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    ' Created/parented in MainForm_Layout.BuildCardGridLayout (its own AutoSize row in the
    ' SETTINGS & TOOLS outer TLP, between MIN NET MOVE % and TOOLS).
    Friend lblTapeStoreStatus As Label

    Private _tapeStoreTimer As System.Windows.Forms.Timer

    ' -----------------------------------------------------------------------
    ' Lifecycle — StartTapeStoreStatus from the constructor (form load); StopTapeStoreStatus
    ' on form close. Idempotent: a re-start disposes the prior timer first.
    ' -----------------------------------------------------------------------
    Private Sub StartTapeStoreStatus()
        StopTapeStoreStatus()

        _tapeStoreTimer = New System.Windows.Forms.Timer() With {
            .Interval = TapeStoreIntervalMs(SettingsLoader.Current)
        }
        AddHandler _tapeStoreTimer.Tick, AddressOf OnTapeStoreTick
        _tapeStoreTimer.Start()

        ' Paint once immediately so the strip reflects the current state from form load.
        OnTapeStoreTick(Nothing, EventArgs.Empty)
    End Sub

    Private Sub StopTapeStoreStatus()
        If _tapeStoreTimer IsNot Nothing Then
            _tapeStoreTimer.Stop()
            RemoveHandler _tapeStoreTimer.Tick, AddressOf OnTapeStoreTick
            _tapeStoreTimer.Dispose()
            _tapeStoreTimer = Nothing
        End If
        SetTapeStoreStrip(Nothing, Nothing, visible:=False)
    End Sub

    Private Shared Function TapeStoreIntervalMs(cfg As EngineSettings) As Integer
        ' No new settings key — reuses trade_store.flush_seconds. Floor at 2s so a mis-set
        ' 0/1 can't busy-spin the UI thread; cap at 30s so a large flush_seconds still
        ' refreshes the glance often enough to be worth having.
        Return Math.Max(2, Math.Min(30, cfg.TradeStore.FlushSeconds)) * 1000
    End Function

    ' -----------------------------------------------------------------------
    ' Tick — runs on the UI thread, so the feed's plain read (GetTradeStoreStatus) and the
    ' label update are safe without Control.Invoke.
    ' -----------------------------------------------------------------------
    Private Sub OnTapeStoreTick(sender As Object, e As EventArgs)
        If IsDisposed OrElse lblTapeStoreStatus Is Nothing Then Return
        Dim cfg As EngineSettings = SettingsLoader.Current

        Dim desiredMs As Integer = TapeStoreIntervalMs(cfg)
        If _tapeStoreTimer IsNot Nothing AndAlso _tapeStoreTimer.Interval <> desiredMs Then
            _tapeStoreTimer.Interval = desiredMs
        End If

        If _wsFeed Is Nothing Then
            SetTapeStoreStrip(Nothing, Nothing, visible:=False)
            Return
        End If

        Dim status = _wsFeed.GetTradeStoreStatus()
        If Not status.Enabled Then
            SetTapeStoreStrip(Nothing, Nothing, visible:=False)
            Return
        End If

        Dim tier As String = TradeStoreWriter.ClassifyTapeStoreTier(status.SecondsSinceFlush, status.FlushSeconds)
        Dim colour As Color
        Select Case tier
            Case "RED"
                colour = Theme.ACC_CASCADE
            Case "AMBER"
                colour = Theme.ACC_WARN
            Case Else   ' NORMAL / UNKNOWN (cold start, not a fault)
                colour = Theme.FG_TERTIARY
        End Select

        SetTapeStoreStrip(ComposeTapeStoreStrip(status), colour, visible:=True)
    End Sub

    ' "TAPE STORE: 12s · 47.3k rows" (proposal §4, verbatim shape). "no flush yet" before the
    ' first successful commit this writer instance's life — a cold start, not a fault.
    Private Shared Function ComposeTapeStoreStrip(status As TradeStoreStatus) As String
        Dim rowsText As String = FormatTapeStoreRows(status.RowsThisProcess)
        If Not status.SecondsSinceFlush.HasValue Then
            Return String.Format("TAPE STORE: no flush yet · {0} rows", rowsText)
        End If
        Return String.Format("TAPE STORE: {0}s · {1} rows",
                             CInt(Math.Round(status.SecondsSinceFlush.Value)), rowsText)
    End Function

    Private Shared Function FormatTapeStoreRows(n As Long) As String
        If n >= 1000L Then Return (n / 1000.0).ToString("0.0") & "k"
        Return n.ToString()
    End Function

    ' Sets text/colour and toggles visibility only on CHANGE (no flicker, the #3 TAPE-strip
    ' precedent). SyncSettingsCardHeight is called only on an actual visibility flip, so the
    ' card grows/shrinks exactly once per transition rather than every tick.
    Private Sub SetTapeStoreStrip(body As String, colour As Color?, visible As Boolean)
        If lblTapeStoreStatus Is Nothing Then Return
        If Not visible OrElse body Is Nothing Then
            If lblTapeStoreStatus.Visible Then
                lblTapeStoreStatus.Visible = False
                SyncSettingsCardHeight()
            End If
            Return
        End If
        If lblTapeStoreStatus.Text <> body Then lblTapeStoreStatus.Text = body
        If colour.HasValue AndAlso Not lblTapeStoreStatus.ForeColor.Equals(colour.Value) Then
            lblTapeStoreStatus.ForeColor = colour.Value
        End If
        If Not lblTapeStoreStatus.Visible Then
            lblTapeStoreStatus.Visible = True
            SyncSettingsCardHeight()
        End If
    End Sub

End Class
