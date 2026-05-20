' UI/RoundStatsForm.vb
' Non-modal WinForms dialog showing the last 5 auto-tweaker rounds with
' per-verdict-tier accuracy breakdown (settings-snapshot-history-proposal.md §3k).
'
' Render path:
'   1. Read tweaker_config.json + state.json
'   2. Call RoundStatsBuilder.BuildAsync (host-agnostic, async — fetches OHLC)
'   3. Display in a single RichTextBox
'
' Thin wrapper — all data computation lives in tools/AutoTweaker.

Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

Public Class RoundStatsForm
    Inherits Form

    Private WithEvents btnRefresh As Button
    Private WithEvents btnClose   As Button
    Private            txtReport  As RichTextBox

    Private ReadOnly _configPath   As String
    Private ReadOnly _statePath    As String
    Private ReadOnly _csvPath      As String
    Private ReadOnly _snapshotsDir As String
    Private ReadOnly _manifestPath As String

    Public Sub New(configPath As String, statePath As String, csvPath As String,
                    snapshotsDir As String, manifestPath As String)
        _configPath   = configPath
        _statePath    = statePath
        _csvPath      = csvPath
        _snapshotsDir = snapshotsDir
        _manifestPath = manifestPath
        InitializeComponent()
        AddHandler Me.Shown, AddressOf OnShownRefresh
    End Sub

    Private Async Sub OnShownRefresh(sender As Object, e As EventArgs)
        Await RefreshAsync()
    End Sub

    Private Async Function RefreshAsync() As Threading.Tasks.Task
        If Me.InvokeRequired Then
            Me.Invoke(Sub() btnRefresh.Enabled = False)
        Else
            btnRefresh.Enabled = False
        End If

        txtReport.Text = "Loading round stats..."
        Try
            Dim cfg   = TweakerConfig.Load(_configPath)
            Dim state = TweakerState.Load(_statePath)
            Dim text  = Await RoundStatsBuilder.BuildAsync(state, _csvPath, _snapshotsDir,
                                                            _manifestPath, cfg, 5)
            If Me.InvokeRequired Then
                Me.Invoke(Sub() txtReport.Text = text)
            Else
                txtReport.Text = text
            End If
        Catch ex As Exception
            txtReport.Text = "Round stats render failed: " & ex.Message
        Finally
            If Me.InvokeRequired Then
                Me.Invoke(Sub() btnRefresh.Enabled = True)
            Else
                btnRefresh.Enabled = True
            End If
        End Try
    End Function

    Private Async Sub btnRefresh_Click(sender As Object, e As EventArgs) Handles btnRefresh.Click
        Await RefreshAsync()
    End Sub

    Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
        Me.Close()
    End Sub

    Private Sub InitializeComponent()
        Me.SuspendLayout()

        Me.Text            = "Round Statistics"
        Me.Size            = New Size(760, 560)
        Me.MinimumSize     = New Size(620, 420)
        Me.BackColor       = Color.FromArgb(30, 30, 30)
        Me.ForeColor       = Color.FromArgb(200, 200, 200)
        Me.Font            = New Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.StartPosition   = FormStartPosition.Manual
        Me.Location        = New Point(150, 150)

        txtReport               = New RichTextBox()
        txtReport.Location      = New Point(8, 8)
        txtReport.Size          = New Size(728, 480)
        txtReport.Anchor        = AnchorStyles.Top Or AnchorStyles.Left Or
                                  AnchorStyles.Right Or AnchorStyles.Bottom
        txtReport.BackColor     = Color.FromArgb(20, 20, 20)
        txtReport.ForeColor     = Color.FromArgb(220, 220, 220)
        txtReport.Font          = Theme.FontMono(9.0F)
        txtReport.ReadOnly      = True
        txtReport.BorderStyle   = BorderStyle.FixedSingle
        txtReport.WordWrap      = False
        txtReport.ScrollBars    = RichTextBoxScrollBars.Both
        Me.Controls.Add(txtReport)

        btnRefresh           = New Button()
        btnRefresh.Text      = "Refresh"
        btnRefresh.Size      = New Size(100, 28)
        btnRefresh.Location  = New Point(8, 496)
        btnRefresh.Anchor    = AnchorStyles.Bottom Or AnchorStyles.Left
        btnRefresh.BackColor = Color.FromArgb(50, 70, 90)
        btnRefresh.ForeColor = Color.FromArgb(220, 220, 220)
        btnRefresh.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnRefresh)

        btnClose           = New Button()
        btnClose.Text      = "Close"
        btnClose.Size      = New Size(100, 28)
        btnClose.Location  = New Point(636, 496)
        btnClose.Anchor    = AnchorStyles.Bottom Or AnchorStyles.Right
        btnClose.BackColor = Color.FromArgb(70, 60, 60)
        btnClose.ForeColor = Color.FromArgb(220, 220, 220)
        btnClose.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnClose)

        Me.ResumeLayout(False)
    End Sub

End Class
