' UI/MainForm_AutoRun.vb
' Partial class: Auto-run timer controls, start/stop, countdown tick.
'
' [P4 #2] Two trigger modes (docs/on-close-analysis-mode-proposal.md):
'   - interval (default): the fixed-interval _autoRunTimer — byte-identical to v43.
'   - on_close: a ~1s bar-close watcher (StartOnCloseWatcher) fires RunAutoAnalysis the instant the
'     execution-resolution bar rolls, with the configured interval as a feed-stall BACKSTOP. WS-mode
'     feature — falls back to interval mode when no live MarketState feed is present (§4.4).
' The run itself (RunAutoAnalysis → btnAnalyze_Click → RunAnalysisAsync) is UNCHANGED in both modes.

Imports System.Drawing
Imports System.Threading
Imports System.Windows.Forms

Partial Public Class MainForm

    Private Sub InitAutoRunControls()
        _autoRunTimer = New WinFormsAutoRunTimer(Me)
        Dim cfg As EngineSettings = SettingsLoader.Current
        nudMinutes.Value = Math.Max(0, Math.Min(60, cfg.AutoRun.IntervalMinutes))
        nudSeconds.Value = Math.Max(0, Math.Min(59, cfg.AutoRun.IntervalSeconds))
        rbSingle.Checked = True

        ' [P4 #2] Restore the saved trigger mode onto the INTERVAL/ON-CLOSE radios (built in
        ' ReparentHeaderStripControls, before settings were loaded). Setting .Checked here fires
        ' OnTriggerModeChanged, which no-ops the Save because cfg already matches.
        Dim onCloseSel As Boolean =
            String.Equals(cfg.AutoRun.TriggerMode, "on_close", StringComparison.OrdinalIgnoreCase)
        If rbModeInterval IsNot Nothing AndAlso rbModeOnClose IsNot Nothing Then
            rbModeOnClose.Checked  = onCloseSel
            rbModeInterval.Checked = Not onCloseSel
        End If
        UpdateTriggerModeUi()

        ' Always initialise the CONTROLS to the stopped visual state here, regardless of
        ' auto_run.start_engaged — the constructor may immediately call StartAutoRun() right
        ' after this returns (MainForm_Layout.vb, [v68]), which flips them again. Keeping the
        ' rest state here (rather than branching on start_engaged inside this sub) means
        ' StartAutoRun() is the ONLY place that puts the UI into the running state, on start
        ' or on a manual click alike — one code path, not two.
        btnStartStop.Text      = CHAR_PLAY
        btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
        UpdateCountdownLabel("Auto-run: OFF")
    End Sub

    Private Sub btnStartStop_Click(sender As Object, e As EventArgs) Handles btnStartStop.Click
        If AutoRunEngaged() Then
            StopAutoRun()
        Else
            StartAutoRun()
        End If
    End Sub

    ''' <summary>
    ''' silentOnInvalid: [v68 FIX 4, docs/collector-ops-tooling-spec-back.md §5] the
    ''' auto-start-on-load caller (MainForm_Layout.vb) passes True — an unattended box with a
    ''' hand-edited invalid interval must never block on a MessageBox nobody is present to
    ''' dismiss (the same session-0 hazard docs/collector-ops-tooling-proposal.md §2.1 warns
    ''' about, reopened in session 2 by this build). The manual Start-button click path
    ''' (btnStartStop_Click) passes the default False and keeps the dialog — a human is
    ''' present there to see it.
    ''' </summary>
    Private Sub StartAutoRun(Optional silentOnInvalid As Boolean = False)
        Dim mins As Integer = CInt(nudMinutes.Value)
        Dim secs As Integer = CInt(nudSeconds.Value)
        _intervalMs = (mins * 60 + secs) * 1000
        If _intervalMs < 10_000 Then
            If silentOnInvalid Then
                Console.WriteLine("[AutoRun] start_engaged: interval below the 10-second minimum — not engaging, no dialog (unattended start)")
            Else
                MessageBox.Show(Me, "Minimum interval is 10 seconds.", "Auto-Run",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
            Return
        End If

        ' D4 (S-4): persist the interval ONLY when it actually changed. The
        ' interval is the one AutoRun value with a reader (InitAutoRunControls
        ' restores it next session). A plain start no longer bumps version /
        ' litters change_log — only a genuine interval edit writes.
        ' [v36 §10a] bumpVersion:=False — an operational interval change must not
        ' churn the feature version or the change_log (it's not a scoring/feature edit).
        Dim cfg As EngineSettings = SettingsLoader.Current
        If cfg.AutoRun.IntervalMinutes <> mins OrElse cfg.AutoRun.IntervalSeconds <> secs Then
            cfg.AutoRun.IntervalMinutes = mins
            cfg.AutoRun.IntervalSeconds = secs
            SettingsLoader.Save(cfg, "auto_run interval changed via UI", bumpVersion:=False)
        End If

        ' [P4 #2] On-close is a WS-mode feature: it reads the streaming MarketState. With no feed
        ' (transport=rest / feed Nothing) it falls back to interval mode for this session (§4.4),
        ' surfaced as a status note. The toggle stays selected; it simply has no effect until a feed exists.
        Dim onClose As Boolean = OnCloseModeActive()
        _onCloseFellBackToInterval =
            String.Equals(cfg.AutoRun.TriggerMode, "on_close", StringComparison.OrdinalIgnoreCase) AndAlso Not onClose

        _countdownSecs = _intervalMs \ 1000
        btnStartStop.Text      = CHAR_STOP
        btnStartStop.BackColor = Color.FromArgb(160, 40, 40)
        SetAutoRunInputsEnabled(False)

        _countdownTimer = New Threading.Timer(AddressOf OnCountdownTick, Nothing, 1000, 1000)

        If onClose Then
            ' Bar-close watcher in place of the interval timer; honours SINGLE/REPEAT in RunAutoAnalysis.
            StartOnCloseWatcher()
        ElseIf rbSingle.Checked Then
            CType(_autoRunTimer, WinFormsAutoRunTimer).StartOnce(_intervalMs, AddressOf RunAutoAnalysis)
        Else
            _autoRunTimer.Start(_intervalMs, AddressOf RunAutoAnalysis)
        End If
        ' [P4 #1] The exit-guard tick is NOT tied to auto-run (D6) — it runs from form load and
        ' self-gates on posState + feed health, so a declared position is watched even when auto-run
        ' is paused.
    End Sub

    Private Sub StopAutoRun()
        _autoRunTimer.Stop()
        StopOnCloseWatcher()
        _onCloseFellBackToInterval = False
        If _countdownTimer IsNot Nothing Then
            _countdownTimer.Dispose()
            _countdownTimer = Nothing
        End If
        btnStartStop.Text      = CHAR_PLAY
        btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
        SetAutoRunInputsEnabled(True)
        UpdateCountdownLabel("Auto-run: OFF")
        ' D4 (S-4): no Save on stop — Enabled is never read and persisting it
        ' bought nothing but settings.json churn.
    End Sub

    Private Sub RunAutoAnalysis()
        If Not btnAnalyze.Enabled Then Return
        _countdownSecs = _intervalMs \ 1000
        If rbSingle.Checked Then
            ' SINGLE: reset to the Start state after this one fire. Interval SINGLE self-stops via
            ' StartOnce; on-close SINGLE must dispose its watcher here.
            If _onCloseActive Then StopOnCloseWatcher()
            btnStartStop.Text      = CHAR_PLAY
            btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
            SetAutoRunInputsEnabled(True)
        End If
        btnAnalyze_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub OnCountdownTick(state As Object)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
        Try
            Me.Invoke(Sub()
                          If Not AutoRunEngaged() Then Return
                          If _onCloseActive Then
                              UpdateCountdownLabel(BuildOnCloseCountdownText())
                          Else
                              _countdownSecs -= 1
                              If _countdownSecs < 0 Then _countdownSecs = _intervalMs \ 1000
                              Dim m As Integer = _countdownSecs \ 60
                              Dim s As Integer = _countdownSecs Mod 60
                              Dim txt As String = String.Format("Next run in: {0}:{1:D2}  [{2}]",
                                                                m, s,
                                                                If(rbRepeat.Checked, "REPEAT", "SINGLE"))
                              ' On-close requested but no feed → ran interval; tell the trader why (§4.4).
                              If _onCloseFellBackToInterval Then txt &= "  [on-close: WS only]"
                              UpdateCountdownLabel(txt)
                          End If
                      End Sub)
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub UpdateCountdownLabel(text As String)
        lblCountdown.Text = text
    End Sub

    ' =======================================================================
    ' [P4 #2] On-close analysis mode — bar-close watcher + helpers
    ' =======================================================================

    ''' <summary>True when an auto-run is engaged in EITHER mode. In on-close mode the interval
    ''' _autoRunTimer is not running, so callers that gate on "is auto-run on" must OR the two.</summary>
    Private Function AutoRunEngaged() As Boolean
        Return _autoRunTimer.IsRunning OrElse _onCloseActive
    End Function

    ''' <summary>On-close is selected AND a live WS MarketState feed exists to read bar rolls from.
    ''' transport=rest or an unconstructed feed → False (caller falls back to interval mode, §4.4).</summary>
    Private Function OnCloseModeActive() As Boolean
        Dim cfg As EngineSettings = SettingsLoader.Current
        If Not String.Equals(cfg.AutoRun.TriggerMode, "on_close", StringComparison.OrdinalIgnoreCase) Then Return False
        If Not String.Equals(cfg.Network.Transport, "ws", StringComparison.OrdinalIgnoreCase) Then Return False
        Return _marketState IsNot Nothing AndAlso _wsFeed IsNot Nothing
    End Function

    Private Sub SetAutoRunInputsEnabled(enabled As Boolean)
        nudMinutes.Enabled = enabled
        nudSeconds.Enabled = enabled
        ' Lock the trigger mode while a run is engaged (mirrors the interval NUDs — the active
        ' mode is fixed until Stop, same as the interval value).
        If rbModeInterval IsNot Nothing Then rbModeInterval.Enabled = enabled
        If rbModeOnClose IsNot Nothing Then rbModeOnClose.Enabled = enabled
    End Sub

    Private Sub StartOnCloseWatcher()
        StopOnCloseWatcher()   ' idempotent: dispose any prior watcher first
        _onCloseActive       = True
        _onCloseLastSeenOpen = BarCloseDetector.Unseen
        _onCloseLastRes      = ExecutionResolution.ResolveResolution(SettingsLoader.Current, DateTime.UtcNow.Hour)
        ' Backstop baseline: the first backstop fire is one interval from start if no roll is seen.
        _onCloseLastFireUtc  = DateTime.UtcNow
        _onCloseWatcher      = New Threading.Timer(AddressOf OnCloseWatcherTick, Nothing, 1000, 1000)
    End Sub

    Private Sub StopOnCloseWatcher()
        _onCloseActive = False
        If _onCloseWatcher IsNot Nothing Then
            _onCloseWatcher.Dispose()
            _onCloseWatcher = Nothing
        End If
        _onCloseLastSeenOpen = BarCloseDetector.Unseen
        _onCloseLastRes      = 0
    End Sub

    ''' <summary>The effective on-close feed-stall backstop (ms): the NUD interval floored to one
    ''' bar + 1 minute, so a backstop shorter than the exec resolution can't pre-empt the bar-close
    ''' trigger — a 1m NUD on 3m bars would otherwise fire every minute. The backstop is a stall
    ''' net, never the cadence (the bar close is the primary trigger).</summary>
    Private Function EffectiveBackstopMs(execRes As Integer) As Double
        Return Math.Max(_intervalMs, (Math.Max(execRes, 1) + 1) * 60000.0)
    End Function

    ' Runs on a threadpool thread (Threading.Timer). MarketState reads are lock-guarded, so the
    ' detection is safe off-UI-thread; the fire is marshalled onto the UI thread (RunAutoAnalysis
    ' touches controls + kicks off the async run, exactly like the WinFormsAutoRunTimer path).
    Private Sub OnCloseWatcherTick(state As Object)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
        If Not _onCloseActive Then Return
        Try
            Dim cfg As EngineSettings = SettingsLoader.Current
            ' Re-resolve each tick so a session boundary (e.g. London→NY 13:00 UTC, 3→1) is honoured
            ' live: on a resolution change, re-adopt the new resolution's forming bar (no immediate
            ' fire), then its next roll fires normally (§4.2 / §7).
            Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, DateTime.UtcNow.Hour)
            If execRes <> _onCloseLastRes Then
                _onCloseLastRes      = execRes
                _onCloseLastSeenOpen = BarCloseDetector.Unseen
            End If

            Dim roll = BarCloseDetector.DetectBarRoll(_marketState, execRes, _onCloseLastSeenOpen)
            _onCloseLastSeenOpen = roll.FormingOpen

            Dim nowUtc As DateTime = DateTime.UtcNow
            Dim fire As Boolean = roll.Fired
            ' Backstop: never go silent if the WS-fed series stops rolling (feed stall). A real roll
            ' resets _onCloseLastFireUtc, so the backstop only fires after a full silent interval.
            ' EffectiveBackstopMs floors it to one bar + 1 min so a too-short NUD can't pre-empt the
            ' bar-close trigger (the stall net must never become the cadence).
            If Not fire AndAlso (nowUtc - _onCloseLastFireUtc).TotalMilliseconds >= EffectiveBackstopMs(execRes) Then
                fire = True
            End If

            If fire Then
                _onCloseLastFireUtc = nowUtc   ' set on EVERY fire → double-fire guard for the backstop
                Me.Invoke(Sub() RunAutoAnalysis())
            End If
        Catch ex As ObjectDisposedException
        Catch
            ' Advisory trigger — never surface an exception into the tick.
        End Try
    End Sub

    ' "Next close: M:SS" — seconds to the next exec-resolution bar boundary (ceil(now/execRes) − now),
    ' on the UTC minute grid. Informative only; reuses lblCountdown (§4.5).
    Private Function BuildOnCloseCountdownText() As String
        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim execRes As Integer = ExecutionResolution.ResolveResolution(cfg, DateTime.UtcNow.Hour)
        If execRes < 1 Then execRes = 1
        Dim periodSec As Integer = execRes * 60
        Dim secsIntoDay As Double = DateTime.UtcNow.TimeOfDay.TotalSeconds
        Dim secsLeft As Integer = CInt(Math.Ceiling(periodSec - (secsIntoDay Mod periodSec)))
        If secsLeft <= 0 Then secsLeft = periodSec
        If secsLeft > periodSec Then secsLeft = periodSec
        Dim m As Integer = secsLeft \ 60
        Dim s As Integer = secsLeft Mod 60
        ' Surface the effective backstop when the NUD was floored above the bar, so the trader sees
        ' the real stall-net value rather than a too-short NUD (e.g. a 1m NUD on 3m bars -> 4m).
        Dim eff As Double = EffectiveBackstopMs(execRes)
        Dim bkstp As String = If(eff > _intervalMs, String.Format(" - backstop {0}m", CInt(eff / 60000)), "")
        Return String.Format("Next close: {0}:{1:D2}  [{2} {3}m{4}]",
                             m, s, If(rbRepeat.Checked, "REPEAT", "SINGLE"), execRes, bkstp)
    End Function

    ' =======================================================================
    ' [P4 #2] Trigger-mode toggle (INTERVAL | ON-CLOSE) — persist + relabel
    ' =======================================================================

    Private Sub OnTriggerModeChanged(sender As Object, e As EventArgs)
        Dim cfg As EngineSettings = SettingsLoader.Current
        Dim wantOnClose As Boolean = (rbModeOnClose IsNot Nothing AndAlso rbModeOnClose.Checked)
        Dim newMode As String = If(wantOnClose, "on_close", "interval")
        If Not String.Equals(cfg.AutoRun.TriggerMode, newMode, StringComparison.OrdinalIgnoreCase) Then
            cfg.AutoRun.TriggerMode = newMode
            ' [v36 §10a] operational/UI toggle — bumpVersion:=False (no version/change_log churn).
            SettingsLoader.Save(cfg, "auto_run trigger_mode changed via UI", bumpVersion:=False)
        End If
        UpdateTriggerModeUi()
    End Sub

    ' Relabel the interval cluster: in on-close mode the NUD is the feed-stall BACKSTOP, not the
    ' primary cadence (§5 / §10). The NUDs stay editable — they set the backstop ceiling.
    Private Sub UpdateTriggerModeUi()
        If lblAutoRun Is Nothing Then Return
        Dim onClose As Boolean = (rbModeOnClose IsNot Nothing AndAlso rbModeOnClose.Checked)
        lblAutoRun.Text = If(onClose, "BACKSTOP", "AUTO EVERY")
    End Sub

End Class
