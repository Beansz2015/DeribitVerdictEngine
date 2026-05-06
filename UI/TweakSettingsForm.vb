' UI/TweakSettingsForm.vb
' Non-modal WinForms dialog for the Auto-Tweaker settings + status.
' Opened via MainForm "Tweak Settings" link. Non-modal (Q24 confirmed).
'
' Status polling:
'   - Subscribes to MainForm.AnalysisCompleted event (fires after every RunAnalysisAsync).
'   - 30-second fallback Timer when the form is open.
'   - Cheap: just reads state.json + counts CSV rows.
'
' Run Tweaker Now:
'   - Process.Start(AutoTweaker.exe) asynchronously.
'   - Button disabled when status != Ready.
'
' Save:
'   - Validates textbox inputs, writes tweaker_config.json, shows MessageBox confirmation.

Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Text.Json
Imports System.Windows.Forms

Public Class TweakSettingsForm
    Inherits Form

    ' ── Controls ────────────────────────────────────────────────────────────
    Private WithEvents chkAutoCommit     As CheckBox
    Private WithEvents chkDryRun         As CheckBox
    Private            txtWindowSize     As TextBox
    Private            txtFailThreshold  As TextBox
    Private            txtCooldownRows   As TextBox
    Private            lblConfigPath     As Label
    Private            lblCsvPath        As Label
    Private            lblStatePath      As Label
    Private            lblTweakerStatus  As Label
    Private WithEvents btnRunNow         As Button
    Private WithEvents btnSave           As Button
    Private            lblLastSummary    As Label
    Private WithEvents _pollTimer        As Timer

    ' ── Paths (resolved once at construction) ───────────────────────────────
    Private ReadOnly _repoRoot      As String
    Private ReadOnly _configPath    As String
    Private ReadOnly _statePath     As String
    Private ReadOnly _csvPath       As String
    Private ReadOnly _tweakerExe    As String
    Private ReadOnly _mainForm      As MainForm

    Public Sub New(owner As MainForm)
        InitializeComponent()
        _mainForm = owner

        ' Resolve paths from the running executable's location.
        ' Application.StartupPath = bin/Debug/net8.0-windows/ — go 3 levels up to repo root.
        _repoRoot   = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", ".."))
        _configPath = Path.Combine(_repoRoot, "tools", "AutoTweaker", "tweaker_config.json")
        _statePath  = Path.Combine(_repoRoot, "tools", "AutoTweaker", "state.json")
        _csvPath    = Path.Combine(Application.StartupPath, "analysis_log.csv")
        _tweakerExe = Path.Combine(_repoRoot, "tools", "AutoTweaker", "bin", "Debug", "net8.0", "AutoTweaker.exe")

        ' Populate path labels
        lblConfigPath.Text = _configPath
        lblCsvPath.Text    = _csvPath
        lblStatePath.Text  = _statePath

        ' Load existing config into controls
        LoadConfigIntoControls()

        ' Subscribe to MainForm.AnalysisCompleted
        AddHandler _mainForm.AnalysisCompleted, AddressOf OnAnalysisCompleted

        ' 30-second polling timer
        _pollTimer          = New Timer() With {.Interval = 30_000, .Enabled = True}
        AddHandler _pollTimer.Tick, AddressOf OnPollTick

        UpdateStatusLabel()
    End Sub

    ' ── Status polling ────────────────────────────────────────────────────────
    Private Sub OnAnalysisCompleted(sender As Object, e As EventArgs)
        ' Called on the UI thread via MainForm.RaiseEvent
        UpdateStatusLabel()
    End Sub

    Private Sub OnPollTick(sender As Object, e As EventArgs)
        UpdateStatusLabel()
    End Sub

    Private Sub UpdateStatusLabel()
        Dim cfg   = TweakerConfig.Load(_configPath)
        Dim state = TweakerState.Load(_statePath)

        ' 1. Cooldown check
        Dim currentRowCount As Integer = CountCsvRows()
        Dim rowsSinceLast As Integer = currentRowCount - state.LastRunCsvRowCount
        If state.LastRunCsvRowCount > 0 AndAlso rowsSinceLast < cfg.CooldownRows Then
            Dim remaining = cfg.CooldownRows - rowsSinceLast
            SetStatus(String.Format("Cooldown: {0} rows remaining", remaining), Color.Orange)
            btnRunNow.Enabled = False
            UpdateSummaryLabel(state)
            Return
        End If

        ' 2. Session-aligned window check (quick CSV line count vs window size)
        If currentRowCount < cfg.WindowSizeVerdicts Then
            SetStatus(String.Format("Waiting for session-aligned window: {0}/{1} rows",
                                    currentRowCount, cfg.WindowSizeVerdicts), Color.Orange)
            btnRunNow.Enabled = False
            UpdateSummaryLabel(state)
            Return
        End If

        ' 3. Tier-eligible rows check — mirrors AutoTweakerCore step 4.
        '    Count directional verdicts in the last window_size_verdicts CSV rows.
        Dim tierEligible As Integer = CountTierEligibleInWindow(cfg.WindowSizeVerdicts)
        If tierEligible < cfg.MinTierEligibleRows Then
            SetStatus(String.Format("Insufficient tier-eligible rows: {0}/{1}",
                                    tierEligible, cfg.MinTierEligibleRows), Color.Orange)
            btnRunNow.Enabled = False
            UpdateSummaryLabel(state)
            Return
        End If

        SetStatus("Ready", Color.FromArgb(80, 220, 120))
        btnRunNow.Enabled = True
        UpdateSummaryLabel(state)
    End Sub

    Private Sub SetStatus(text As String, colour As Color)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() SetStatus(text, colour))
            Return
        End If
        lblTweakerStatus.Text      = text
        lblTweakerStatus.ForeColor = colour
    End Sub

    Private Sub UpdateSummaryLabel(state As TweakerState)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() UpdateSummaryLabel(state))
            Return
        End If
        If String.IsNullOrEmpty(state.LastRunAtIso) Then
            lblLastSummary.Text = "(No prior runs)"
            Return
        End If
        lblLastSummary.Text = String.Format("{0}  [{1}]{2}{3}",
            state.LastRunAtIso,
            state.LastRunOutcome,
            If(String.IsNullOrEmpty(state.LastProposalSummary), "", vbCrLf & state.LastProposalSummary),
            If(String.IsNullOrEmpty(state.LastErrorMessage),    "", vbCrLf & "Error: " & state.LastErrorMessage))
    End Sub

    Private Function CountCsvRows() As Integer
        Try
            If Not File.Exists(_csvPath) Then Return 0
            Dim lines = File.ReadAllLines(_csvPath)
            Return Math.Max(0, lines.Length - 1)  ' subtract header
        Catch
            Return 0
        End Try
    End Function

    ' Count rows in the last windowSize data rows of the CSV whose Verdict column
    ' is a directional tier (STRONG LONG / LONG / SHORT / STRONG SHORT).
    ' Mirrors the tier-eligible filter in AutoTweakerCore step 4.
    Private Function CountTierEligibleInWindow(windowSize As Integer) As Integer
        Try
            If Not File.Exists(_csvPath) Then Return 0
            Dim lines As String() = File.ReadAllLines(_csvPath)
            If lines.Length < 2 Then Return 0  ' header only or empty

            ' Locate "Verdict" column from header
            Dim headers As String() = lines(0).Split(","c)
            Dim verdictIdx As Integer = -1
            For i As Integer = 0 To headers.Length - 1
                If headers(i).Trim().Equals("Verdict", StringComparison.OrdinalIgnoreCase) Then
                    verdictIdx = i : Exit For
                End If
            Next
            If verdictIdx < 0 Then Return 0

            ' Scan last windowSize data lines (lines(1) onward)
            Dim firstLine As Integer = lines.Length - windowSize
            If firstLine < 1 Then firstLine = 1
            Dim count As Integer = 0
            For i As Integer = firstLine To lines.Length - 1
                Dim parts As String() = lines(i).Split(","c)
                If parts.Length <= verdictIdx Then Continue For
                Dim v As String = parts(verdictIdx).Trim().ToUpper()
                If v = "STRONG LONG" OrElse v = "LONG" OrElse
                   v = "STRONG SHORT" OrElse v = "SHORT" Then
                    count += 1
                End If
            Next
            Return count
        Catch
            Return 0
        End Try
    End Function

    ' ── Button handlers ───────────────────────────────────────────────────────
    Private Sub btnRunNow_Click(sender As Object, e As EventArgs) Handles btnRunNow.Click
        btnRunNow.Enabled         = False
        lblTweakerStatus.Text     = "Running..."
        lblTweakerStatus.ForeColor = Color.Yellow

        Dim psi As New ProcessStartInfo() With {
            .FileName        = _tweakerExe,
            .Arguments       = "--config """ & _configPath & """",
            .UseShellExecute = False,
            .CreateNoWindow  = True
        }

        Try
            Dim proc = Process.Start(psi)
            If proc IsNot Nothing Then
                proc.EnableRaisingEvents = True
                AddHandler proc.Exited, Sub(s, ev)
                    Me.Invoke(Sub()
                        UpdateStatusLabel()
                    End Sub)
                End Sub
            End If
        Catch ex As Exception
            MessageBox.Show("Failed to start AutoTweaker: " & ex.Message,
                            "Tweak Settings", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatusLabel()
        End Try
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        ' Validate inputs
        Dim windowSize As Integer
        Dim failThreshold As Double
        Dim cooldown As Integer

        If Not Integer.TryParse(txtWindowSize.Text, windowSize) OrElse windowSize < 10 Then
            MessageBox.Show("Window size must be an integer >= 10.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not Double.TryParse(txtFailThreshold.Text,
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               failThreshold) OrElse
               failThreshold < 1 OrElse failThreshold > 99 Then
            MessageBox.Show("Failure threshold must be between 1 and 99.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not Integer.TryParse(txtCooldownRows.Text, cooldown) OrElse cooldown < 1 Then
            MessageBox.Show("Cooldown rows must be a positive integer.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Read existing config to preserve other fields, then update
        Dim cfg = TweakerConfig.Load(_configPath)
        cfg.WindowSizeVerdicts       = windowSize
        cfg.FailureRateThresholdPct  = failThreshold
        cfg.CooldownRows             = cooldown
        cfg.AutoCommitEnabled        = chkAutoCommit.Checked
        cfg.DryRunEnabled            = chkDryRun.Checked
        cfg.CsvPath                  = _csvPath
        cfg.SettingsPath             = Path.Combine(_repoRoot, "settings.json")
        cfg.StatePath                = _statePath

        Try
            TweakerConfig.Save(_configPath, cfg)
            MessageBox.Show("tweaker_config.json saved successfully.", "Tweak Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Save failed: " & ex.Message, "Tweak Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ── Config loading ────────────────────────────────────────────────────────
    Private Sub LoadConfigIntoControls()
        Dim cfg = TweakerConfig.Load(_configPath)
        chkAutoCommit.Checked   = cfg.AutoCommitEnabled
        chkDryRun.Checked       = cfg.DryRunEnabled
        txtWindowSize.Text      = cfg.WindowSizeVerdicts.ToString()
        txtFailThreshold.Text   = cfg.FailureRateThresholdPct.ToString("F0")
        txtCooldownRows.Text    = cfg.CooldownRows.ToString()
    End Sub

    ' ── Form close — unsubscribe handlers ────────────────────────────────────
    Protected Overrides Sub OnFormClosed(e As FormClosedEventArgs)
        RemoveHandler _mainForm.AnalysisCompleted, AddressOf OnAnalysisCompleted
        _pollTimer.Stop()
        _pollTimer.Dispose()
        MyBase.OnFormClosed(e)
    End Sub

    ' ── InitializeComponent — minimal Designer stub ───────────────────────────
    Private Sub InitializeComponent()
        Me.SuspendLayout()

        Me.Text          = "Tweak Settings"
        Me.Size          = New Size(720, 460)
        Me.MinimumSize   = New Size(600, 420)
        Me.BackColor     = Color.FromArgb(30, 30, 30)
        Me.ForeColor     = Color.FromArgb(200, 200, 200)
        Me.Font          = New Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.StartPosition = FormStartPosition.Manual
        Me.Location      = New Point(100, 100)

        Const LBL_X As Integer = 12
        Const CTL_X As Integer = 200
        Const W_W   As Integer = 480
        Dim   y     As Integer = 12

        ' ── Auto-commit ──────────────────────────────────────────────────────
        chkAutoCommit           = New CheckBox()
        chkAutoCommit.Text      = "Auto-commit on apply (default: off)"
        chkAutoCommit.Location  = New Point(LBL_X, y)
        chkAutoCommit.Size      = New Size(350, 20)
        chkAutoCommit.ForeColor = Color.FromArgb(200, 200, 200)
        Me.Controls.Add(chkAutoCommit)
        y += 28

        ' ── Dry-run ──────────────────────────────────────────────────────────
        chkDryRun           = New CheckBox()
        chkDryRun.Text      = "Dry-run mode — write API payload to file instead of calling (default: on)"
        chkDryRun.Location  = New Point(LBL_X, y)
        chkDryRun.Size      = New Size(500, 20)
        chkDryRun.ForeColor = Color.FromArgb(200, 200, 200)
        Me.Controls.Add(chkDryRun)
        y += 32

        ' ── Window size ──────────────────────────────────────────────────────
        AddRow("Window size (verdicts, default 120):", LBL_X, CTL_X, y, 60, txtWindowSize)
        y += 28

        ' ── Failure threshold ────────────────────────────────────────────────
        AddRow("Failure rate threshold % (default 40):", LBL_X, CTL_X, y, 60, txtFailThreshold)
        y += 28

        ' ── Cooldown rows ────────────────────────────────────────────────────
        AddRow("Cooldown rows (default 10):", LBL_X, CTL_X, y, 60, txtCooldownRows)
        y += 36

        ' ── Path labels ──────────────────────────────────────────────────────
        AddPathRow("Config path:", LBL_X, y, W_W, lblConfigPath)   : y += 22
        AddPathRow("CSV path:",    LBL_X, y, W_W, lblCsvPath)       : y += 22
        AddPathRow("State path:",  LBL_X, y, W_W, lblStatePath)     : y += 32

        ' ── Status ───────────────────────────────────────────────────────────
        Dim statusLabel As New Label() With {
            .Text     = "Status:",
            .Location = New Point(LBL_X, y),
            .Size     = New Size(100, 20),
            .ForeColor = Color.FromArgb(160, 160, 160)
        }
        Me.Controls.Add(statusLabel)

        lblTweakerStatus           = New Label()
        lblTweakerStatus.Text      = "Initialising..."
        lblTweakerStatus.Location  = New Point(CTL_X, y)
        lblTweakerStatus.Size      = New Size(300, 20)
        lblTweakerStatus.Font      = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        Me.Controls.Add(lblTweakerStatus)
        y += 28

        ' ── Run Now ──────────────────────────────────────────────────────────
        btnRunNow           = New Button()
        btnRunNow.Text      = "Run Tweaker Now"
        btnRunNow.Location  = New Point(LBL_X, y)
        btnRunNow.Size      = New Size(150, 28)
        btnRunNow.Enabled   = False
        btnRunNow.BackColor = Color.FromArgb(60, 80, 60)
        btnRunNow.ForeColor = Color.FromArgb(200, 200, 200)
        btnRunNow.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnRunNow)

        ' ── Save ─────────────────────────────────────────────────────────────
        btnSave           = New Button()
        btnSave.Text      = "Save Config"
        btnSave.Location  = New Point(175, y)
        btnSave.Size      = New Size(100, 28)
        btnSave.BackColor = Color.FromArgb(50, 70, 90)
        btnSave.ForeColor = Color.FromArgb(200, 200, 200)
        btnSave.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnSave)
        y += 36

        ' ── Last summary ─────────────────────────────────────────────────────
        Dim summaryLabel As New Label() With {
            .Text     = "Last run:",
            .Location = New Point(LBL_X, y),
            .Size     = New Size(100, 20),
            .ForeColor = Color.FromArgb(160, 160, 160)
        }
        Me.Controls.Add(summaryLabel)

        lblLastSummary           = New Label()
        lblLastSummary.Text      = "(No prior runs)"
        lblLastSummary.Location  = New Point(LBL_X, y + 20)
        lblLastSummary.Size      = New Size(W_W, 60)
        lblLastSummary.ForeColor = Color.FromArgb(160, 160, 160)
        lblLastSummary.AutoSize  = False
        Me.Controls.Add(lblLastSummary)

        Me.ResumeLayout(False)
    End Sub

    Private Sub AddRow(labelText As String, lx As Integer, cx As Integer, y As Integer,
                       ctlW As Integer, ByRef ctl As TextBox)
        Dim lbl As New Label() With {
            .Text      = labelText,
            .Location  = New Point(lx, y + 2),
            .Size      = New Size(cx - lx - 4, 20),
            .ForeColor = Color.FromArgb(160, 160, 160)
        }
        Me.Controls.Add(lbl)

        ctl           = New TextBox()
        ctl.Location  = New Point(cx, y)
        ctl.Size      = New Size(ctlW, 22)
        ctl.BackColor = Color.FromArgb(45, 45, 45)
        ctl.ForeColor = Color.FromArgb(200, 200, 200)
        ctl.BorderStyle = BorderStyle.FixedSingle
        Me.Controls.Add(ctl)
    End Sub

    Private Sub AddPathRow(labelText As String, lx As Integer, y As Integer,
                           maxW As Integer, ByRef lbl As Label)
        Dim titleLbl As New Label() With {
            .Text      = labelText,
            .Location  = New Point(lx, y),
            .Size      = New Size(100, 20),
            .ForeColor = Color.FromArgb(120, 120, 120)
        }
        Me.Controls.Add(titleLbl)

        lbl           = New Label()
        lbl.Location  = New Point(lx + 105, y)
        lbl.Size      = New Size(maxW - 105, 20)
        lbl.ForeColor = Color.FromArgb(140, 160, 140)
        lbl.AutoEllipsis = True
        Me.Controls.Add(lbl)
    End Sub

End Class
