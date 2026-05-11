' UI/OutputDumpSettingsForm.vb
' Non-modal WinForms dialog for Output Dump settings.
' Opened via gear-icon link in the MainForm status bar.
' Lets the user toggle output_dump_enabled, set output_dump_max_runs,
' view the dump file path + size, clear the dump file, and save settings.
' Save routes through SettingsLoader.Save so the version bump happens via the existing path.

Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Public Class OutputDumpSettingsForm
    Inherits Form

    ' ── Controls ────────────────────────────────────────────────────────────
    Private WithEvents chkEnabled  As CheckBox
    Private            txtMaxRuns  As TextBox
    Private            lblFilePath As Label
    Private            lblFileSize As Label
    Private WithEvents btnClear    As Button
    Private WithEvents btnSave     As Button
    Private WithEvents btnClose    As Button

    ' ── State ───────────────────────────────────────────────────────────────
    Private ReadOnly _dumpPath As String

    Public Sub New(dumpPath As String)
        InitializeComponent()
        _dumpPath = dumpPath
        lblFilePath.Text = _dumpPath
        RefreshFileSize()
        LoadSettings()
    End Sub

    ' ── Settings load ────────────────────────────────────────────────────────
    Private Sub LoadSettings()
        Dim cfg = SettingsLoader.Current
        chkEnabled.Checked = cfg.AnalysisLogging.OutputDumpEnabled
        txtMaxRuns.Text    = cfg.AnalysisLogging.OutputDumpMaxRuns.ToString()
    End Sub

    ' ── File size refresh ────────────────────────────────────────────────────
    Private Sub RefreshFileSize()
        Try
            If File.Exists(_dumpPath) Then
                Dim fi As New FileInfo(_dumpPath)
                Dim kb As Double = fi.Length / 1024.0
                Dim runs As Integer = AnalysisOutputDump.CountRuns(_dumpPath)
                lblFileSize.Text = String.Format("{0:F1} KB  |  Runs: {1}", kb, runs)
            Else
                lblFileSize.Text = "0 KB  |  Runs: 0"
            End If
        Catch
            lblFileSize.Text = "(unavailable)"
        End Try
    End Sub

    ' ── Button handlers ─────────────────────────────────────────────────────
    Private Sub btnClear_Click(sender As Object, e As EventArgs) Handles btnClear.Click
        Dim result = MessageBox.Show(
            "Clear analysis_output_dump.md? This cannot be undone.",
            "Clear Output Dump",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning)
        If result = DialogResult.Yes Then
            AnalysisOutputDump.Clear(_dumpPath)
            RefreshFileSize()
        End If
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        Dim maxRuns As Integer
        If Not Integer.TryParse(txtMaxRuns.Text, maxRuns) OrElse maxRuns < 0 Then
            MessageBox.Show("Keep last N runs must be a non-negative integer (0 = unlimited).",
                            "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim cfg = SettingsLoader.Current
        cfg.AnalysisLogging.OutputDumpEnabled = chkEnabled.Checked
        cfg.AnalysisLogging.OutputDumpMaxRuns = maxRuns

        Try
            SettingsLoader.Save(cfg, String.Format("output-dump: enabled={0}, max_runs={1}",
                                                    chkEnabled.Checked, maxRuns))
            RefreshFileSize()
            MessageBox.Show("Output dump settings saved.", "Output Dump Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("Save failed: " & ex.Message, "Output Dump Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    ' ── InitializeComponent ───────────────────────────────────────────────────
    Private Sub InitializeComponent()
        Me.SuspendLayout()

        Me.Text            = "Output Dump Settings"
        Me.ClientSize      = New Size(480, 260)
        Me.MinimumSize     = New Size(480, 260)
        Me.BackColor       = Color.FromArgb(30, 30, 30)
        Me.ForeColor       = Color.FromArgb(200, 200, 200)
        Me.Font            = New Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.StartPosition   = FormStartPosition.Manual
        Me.Location        = New Point(120, 120)
        Me.MaximizeBox     = False

        Const LBL_X As Integer = 12
        Const CTL_X As Integer = 220
        Dim y As Integer = 14

        ' ── Enabled ──────────────────────────────────────────────────────────
        chkEnabled          = New CheckBox()
        chkEnabled.Text     = "Enabled"
        chkEnabled.Location = New Point(LBL_X, y)
        chkEnabled.Size     = New Size(200, 20)
        chkEnabled.ForeColor = Color.FromArgb(200, 200, 200)
        Me.Controls.Add(chkEnabled)
        y += 34

        ' ── Max runs ─────────────────────────────────────────────────────────
        Dim lblMaxRuns As New Label() With {
            .Text      = "Keep last N runs (0 = unlimited):",
            .Location  = New Point(LBL_X, y + 2),
            .Size      = New Size(CTL_X - LBL_X - 4, 20),
            .ForeColor = Color.FromArgb(160, 160, 160)
        }
        Me.Controls.Add(lblMaxRuns)

        txtMaxRuns             = New TextBox()
        txtMaxRuns.Location    = New Point(CTL_X, y)
        txtMaxRuns.Size        = New Size(80, 22)
        txtMaxRuns.BackColor   = Color.FromArgb(45, 45, 45)
        txtMaxRuns.ForeColor   = Color.FromArgb(200, 200, 200)
        txtMaxRuns.BorderStyle = BorderStyle.FixedSingle
        Me.Controls.Add(txtMaxRuns)
        y += 34

        ' ── File path ────────────────────────────────────────────────────────
        Dim lblFilePathTitle As New Label() With {
            .Text      = "Dump file:",
            .Location  = New Point(LBL_X, y),
            .Size      = New Size(80, 20),
            .ForeColor = Color.FromArgb(120, 120, 120)
        }
        Me.Controls.Add(lblFilePathTitle)

        lblFilePath              = New Label()
        lblFilePath.Location     = New Point(LBL_X + 85, y)
        lblFilePath.Size         = New Size(370, 36)
        lblFilePath.ForeColor    = Color.FromArgb(140, 160, 140)
        lblFilePath.AutoSize     = False
        lblFilePath.AutoEllipsis = True
        Me.Controls.Add(lblFilePath)
        y += 46

        ' ── File size ────────────────────────────────────────────────────────
        Dim lblFileSizeTitle As New Label() With {
            .Text      = "File size:",
            .Location  = New Point(LBL_X, y),
            .Size      = New Size(80, 20),
            .ForeColor = Color.FromArgb(120, 120, 120)
        }
        Me.Controls.Add(lblFileSizeTitle)

        lblFileSize           = New Label()
        lblFileSize.Location  = New Point(LBL_X + 85, y)
        lblFileSize.Size      = New Size(300, 20)
        lblFileSize.ForeColor = Color.FromArgb(160, 160, 160)
        lblFileSize.AutoSize  = False
        Me.Controls.Add(lblFileSize)
        y += 42

        ' ── Buttons ──────────────────────────────────────────────────────────
        btnClear           = New Button()
        btnClear.Text      = "Clear Output Dump"
        btnClear.Location  = New Point(LBL_X, y)
        btnClear.Size      = New Size(140, 28)
        btnClear.BackColor = Color.FromArgb(80, 40, 40)
        btnClear.ForeColor = Color.FromArgb(200, 200, 200)
        btnClear.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnClear)

        btnSave           = New Button()
        btnSave.Text      = "Save"
        btnSave.Location  = New Point(LBL_X + 155, y)
        btnSave.Size      = New Size(80, 28)
        btnSave.BackColor = Color.FromArgb(50, 70, 90)
        btnSave.ForeColor = Color.FromArgb(200, 200, 200)
        btnSave.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnSave)

        btnClose           = New Button()
        btnClose.Text      = "Close"
        btnClose.Location  = New Point(LBL_X + 245, y)
        btnClose.Size      = New Size(80, 28)
        btnClose.BackColor = Color.FromArgb(50, 50, 50)
        btnClose.ForeColor = Color.FromArgb(200, 200, 200)
        btnClose.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnClose)

        Me.ResumeLayout(False)
    End Sub

End Class
