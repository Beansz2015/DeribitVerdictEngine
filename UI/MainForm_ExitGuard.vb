' UI/MainForm_ExitGuard.vb
' Realtime Exit Guard (P4 #1, docs/realtime-exit-guard-proposal.md) — thin WinForms host.
'
' A System.Windows.Forms.Timer (UI-thread) ticks every exit_guard.interval_sec, started/stopped
' with the auto-run lifecycle (MainForm_AutoRun.vb). Each tick self-gates (§4.1): it does work only
' when exit_guard is enabled, a position is declared, and the WS feed is healthy + fresh. It then
' calls the host-agnostic ExitGuardEvaluator against the live MarketState and renders the EXIT GUARD
' strip in the LOG cascade (built in MainForm_Layout.ReparentSettingsToolsControls), with a debounce
' before latching EXIT + the audible alarm. DISPLAY/ALERT ONLY — no scoring, no CSV, no orders.

Imports System.Drawing
Imports System.Windows.Forms

Partial Public Class MainForm

    ' Strip + tooltip are created/parented in MainForm_Layout (they need the grpLog SectionGroup).
    Friend lblExitGuard As Label
    Private _exitGuardTip As ToolTip

    Private _exitGuardTimer       As System.Windows.Forms.Timer
    Private _exitGuardConsecExit  As Integer = 0   ' consecutive EXIT-condition ticks (debounce build-up)
    Private _exitGuardConsecClear As Integer = 0   ' consecutive non-EXIT ticks (auto-clear / re-arm)
    Private _exitGuardLatched     As Boolean = False

    ' -----------------------------------------------------------------------
    ' Lifecycle — called from StartAutoRun / StopAutoRun (and OnFormClosing).
    ' Idempotent: a re-start (e.g. single-mode re-arm) disposes the prior timer first.
    ' -----------------------------------------------------------------------
    Private Sub StartExitGuard()
        Dim cfg As EngineSettings = SettingsLoader.Current
        If Not cfg.ExitGuard.Enabled Then Return

        StopExitGuard()   ' dispose any prior timer + clear latch/strip

        _exitGuardTimer = New System.Windows.Forms.Timer() With {
            .Interval = ExitGuardIntervalMs(cfg)
        }
        AddHandler _exitGuardTimer.Tick, AddressOf OnExitGuardTick
        _exitGuardTimer.Start()
    End Sub

    Private Sub StopExitGuard()
        If _exitGuardTimer IsNot Nothing Then
            _exitGuardTimer.Stop()
            RemoveHandler _exitGuardTimer.Tick, AddressOf OnExitGuardTick
            _exitGuardTimer.Dispose()
            _exitGuardTimer = Nothing
        End If
        _exitGuardConsecExit  = 0
        _exitGuardConsecClear = 0
        _exitGuardLatched     = False
        If lblExitGuard IsNot Nothing Then lblExitGuard.Visible = False
    End Sub

    Private Shared Function ExitGuardIntervalMs(cfg As EngineSettings) As Integer
        ' Spec range 2–5s; floor at 1s so a mis-set 0 can't busy-spin.
        Return Math.Max(1, cfg.ExitGuard.IntervalSec) * 1000
    End Function

    ' -----------------------------------------------------------------------
    ' Tick — runs on the UI thread (System.Windows.Forms.Timer), so MarketState
    ' reads (lock-guarded), the feed's plain health fields, the position radios,
    ' and the label update are all safe without Control.Invoke.
    ' -----------------------------------------------------------------------
    Private Sub OnExitGuardTick(sender As Object, e As EventArgs)
        If IsDisposed Then Return
        Dim cfg As EngineSettings = SettingsLoader.Current

        ' Hot-reload: enabled flips off → hide + no-op (timer keeps running so it re-enables
        ' instantly); interval edit re-applies live.
        If Not cfg.ExitGuard.Enabled Then
            SetExitGuardStrip(Nothing, Nothing, Nothing, visible:=False)
            Return
        End If
        Dim desiredMs As Integer = ExitGuardIntervalMs(cfg)
        If _exitGuardTimer IsNot Nothing AndAlso _exitGuardTimer.Interval <> desiredMs Then
            _exitGuardTimer.Interval = desiredMs
        End If

        ' (2) Dormant when flat — hidden, zero work, latch reset.
        Dim posState As PositionState = PositionState.None
        If rbLong.Checked Then posState = PositionState.InLong
        If rbShort.Checked Then posState = PositionState.InShort
        If posState = PositionState.None Then
            ResetExitGuardLatch()
            SetExitGuardStrip(Nothing, Nothing, Nothing, visible:=False)
            Return
        End If

        ' WS-mode feature by nature: no feed (transport=rest, no shadow) → "WS only".
        If _wsFeed Is Nothing OrElse _marketState Is Nothing Then
            ResetExitGuardLatch()
            SetExitGuardStrip("WS only", Theme.FG_QUATERNARY, Nothing, visible:=True)
            Return
        End If

        ' (3) Feed-health gate — never evaluate (and never fire) on a stale/down buffer.
        Dim pausedReason As String = ExitGuardPausedReason(cfg)
        If pausedReason IsNot Nothing Then
            ResetExitGuardLatch()
            SetExitGuardStrip("paused (" & pausedReason & ")", Theme.FG_QUATERNARY, Nothing, visible:=True)
            Return
        End If

        ' Carried 5m swing levels from the last full run (slow-moving 5m pivots — §4.2).
        Dim swingLow  As Double = If(_lastSuccessfulIndicators IsNot Nothing, _lastSuccessfulIndicators.LastSwingLow5m, 0)
        Dim swingHigh As Double = If(_lastSuccessfulIndicators IsNot Nothing, _lastSuccessfulIndicators.LastSwingHigh5m, 0)

        Dim res As ExitGuardResult = ExitGuardEvaluator.Evaluate(_marketState, posState, swingLow, swingHigh, cfg)

        ' Debounce + latch (§4.6): EXIT must hold debounce_evals consecutive ticks before the strip
        ' latches and the alarm fires; auto-clears + re-arms after the condition resolves for the
        ' same count. The sound fires once on the latch transition, not every tick.
        Dim debounce As Integer = Math.Max(1, cfg.ExitGuard.DebounceEvals)
        If res.Kind = ExitGuardKind.[Exit] Then
            _exitGuardConsecExit += 1
            _exitGuardConsecClear = 0
            If Not _exitGuardLatched AndAlso _exitGuardConsecExit >= debounce Then
                _exitGuardLatched = True
                If cfg.ExitGuard.SoundEnabled Then PlayExitAlarm()
            End If
        Else
            _exitGuardConsecClear += 1
            _exitGuardConsecExit = 0
            If _exitGuardLatched AndAlso _exitGuardConsecClear >= debounce Then
                _exitGuardLatched = False
            End If
        End If

        RenderExitGuard(res, debounce)
    End Sub

    ' Returns Nothing when the feed is healthy + fresh enough to evaluate, else a short reason.
    Private Function ExitGuardPausedReason(cfg As EngineSettings) As String
        If Not _wsFeed.IsConnected Then Return "feed down"
        If _wsFeed.IsCoolingDown Then Return "feed cooling down"
        Dim staleAfter As Integer = cfg.Network.WsStaleAfterSec
        Dim nowUtc As DateTime = DateTime.UtcNow
        Dim tradesStale As Boolean = (nowUtc - _marketState.TradesLastUpdate).TotalSeconds > staleAfter
        Dim bookStale   As Boolean = (nowUtc - _marketState.BookLastUpdate).TotalSeconds > staleAfter
        If tradesStale OrElse bookStale Then Return "feed stale"
        Return Nothing
    End Function

    Private Sub ResetExitGuardLatch()
        _exitGuardConsecExit  = 0
        _exitGuardConsecClear = 0
        _exitGuardLatched     = False
    End Sub

    ' Compose the inline strip text + colour from the evaluation + latch state. Full detail
    ' (the adverse-signal list / break level) rides the tooltip so the line stays glanceable.
    Private Sub RenderExitGuard(res As ExitGuardResult, debounce As Integer)
        Dim inlineText As String
        Dim colour As Color
        Dim tip As String = res.Reason

        If _exitGuardLatched Then
            inlineText = "⚠ EXIT — " & If(res.StructuralBreak, "structural break", res.AdverseCount & " adverse")
            colour = Theme.ACC_SHORT
        ElseIf res.Kind = ExitGuardKind.[Exit] Then
            ' Condition present but not yet confirmed — building toward the latch.
            inlineText = "⚠ EXIT? confirming " & _exitGuardConsecExit & "/" & debounce
            colour = Theme.ACC_WARN
        ElseIf res.Kind = ExitGuardKind.Warn Then
            inlineText = res.AdverseCount & " adverse"
            colour = Theme.ACC_WARN
        Else
            inlineText = "clear"
            colour = Theme.FG_QUATERNARY
            tip = Nothing
        End If

        SetExitGuardStrip(inlineText, colour, tip, visible:=True)
    End Sub

    Private Sub SetExitGuardStrip(inlineText As String, colour As Color?, tip As String, visible As Boolean)
        If lblExitGuard Is Nothing Then Return
        If Not visible Then
            lblExitGuard.Visible = False
            Return
        End If
        lblExitGuard.Text = "EXIT GUARD: " & inlineText
        If colour.HasValue Then lblExitGuard.ForeColor = colour.Value
        lblExitGuard.Visible = True
        If _exitGuardTip IsNot Nothing Then
            _exitGuardTip.SetToolTip(lblExitGuard, If(String.IsNullOrEmpty(tip), "", tip))
        End If
    End Sub

    Private Shared Sub PlayExitAlarm()
        Try
            System.Media.SystemSounds.Exclamation.Play()
        Catch
            ' A missing audio device must never disrupt the run — the visual EXIT latch stands alone.
        End Try
    End Sub

End Class
