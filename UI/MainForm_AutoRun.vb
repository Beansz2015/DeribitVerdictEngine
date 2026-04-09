' UI/MainForm_AutoRun.vb
' Partial class: auto-run timer, Start/Stop logic, countdown tick.

Imports System.Threading
Imports System.Windows.Forms

Partial Public Class MainForm

    ' -----------------------------------------------------------------------
    ' Initialise controls from saved settings and wire the Start/Stop button
    ' -----------------------------------------------------------------------
    Private Sub InitAutoRunControls()
        _autoRunTimer = New WinFormsAutoRunTimer(Me)
        Dim cfg As EngineSettings = SettingsLoader.Current
        nudMinutes.Value = Math.Max(0, Math.Min(60, cfg.AutoRun.IntervalMinutes))
        nudSeconds.Value = Math.Max(0, Math.Min(59, cfg.AutoRun.IntervalSeconds))
        rbSingle.Checked = True
        ' Always cold-start in stopped state -- user must click Start each session.
        btnStartStop.Text      = CHAR_PLAY
        btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
        UpdateCountdownLabel("Auto-run: OFF")
    End Sub

    Private Sub btnStartStop_Click(sender As Object, e As EventArgs) Handles btnStartStop.Click
        If _autoRunTimer.IsRunning Then
            StopAutoRun()
        Else
            StartAutoRun()
        End If
    End Sub

    Private Sub StartAutoRun()
        Dim mins As Integer = CInt(nudMinutes.Value)
        Dim secs As Integer = CInt(nudSeconds.Value)
        _intervalMs = (mins * 60 + secs) * 1000
        If _intervalMs < 10_000 Then
            MessageBox.Show("Minimum interval is 10 seconds.", "Auto-Run",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.AutoRun.Enabled         = True
        cfg.AutoRun.IntervalMinutes = mins
        cfg.AutoRun.IntervalSeconds = secs
        SettingsLoader.Save(cfg, "auto_run enabled via UI")

        _countdownSecs         = _intervalMs \ 1000
        btnStartStop.Text      = CHAR_STOP
        btnStartStop.BackColor = Color.FromArgb(160, 40, 40)
        nudMinutes.Enabled     = False
        nudSeconds.Enabled     = False

        _countdownTimer = New Threading.Timer(AddressOf OnCountdownTick, Nothing, 1000, 1000)

        If rbSingle.Checked Then
            CType(_autoRunTimer, WinFormsAutoRunTimer).StartOnce(_intervalMs, AddressOf RunAutoAnalysis)
        Else
            _autoRunTimer.Start(_intervalMs, AddressOf RunAutoAnalysis)
        End If
    End Sub

    Private Sub StopAutoRun()
        _autoRunTimer.Stop()
        If _countdownTimer IsNot Nothing Then
            _countdownTimer.Dispose()
            _countdownTimer = Nothing
        End If
        btnStartStop.Text      = CHAR_PLAY
        btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
        nudMinutes.Enabled     = True
        nudSeconds.Enabled     = True
        UpdateCountdownLabel("Auto-run: OFF")
        Dim cfg As EngineSettings = SettingsLoader.Current
        cfg.AutoRun.Enabled = False
        SettingsLoader.Save(cfg, "auto_run disabled via UI")
    End Sub

    Private Sub RunAutoAnalysis()
        If Not btnAnalyze.Enabled Then Return
        _countdownSecs = _intervalMs \ 1000
        If rbSingle.Checked Then
            btnStartStop.Text      = CHAR_PLAY
            btnStartStop.BackColor = Color.FromArgb(0, 140, 60)
            nudMinutes.Enabled     = True
            nudSeconds.Enabled     = True
        End If
        btnAnalyze_Click(Me, EventArgs.Empty)
    End Sub

    Private Sub OnCountdownTick(state As Object)
        If Me.IsDisposed OrElse Not Me.IsHandleCreated Then Return
        Try
            Me.Invoke(Sub()
                          If Not _autoRunTimer.IsRunning Then Return
                          _countdownSecs -= 1
                          If _countdownSecs < 0 Then _countdownSecs = _intervalMs \ 1000
                          Dim m As Integer = _countdownSecs \ 60
                          Dim s As Integer = _countdownSecs Mod 60
                          UpdateCountdownLabel(String.Format("Next run in: {0}:{1:D2}  [{2}]",
                                                             m, s,
                                                             If(rbRepeat.Checked, "REPEAT", "SINGLE")))
                      End Sub)
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub UpdateCountdownLabel(text As String)
        lblCountdown.Text = text
    End Sub

End Class
