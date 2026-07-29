' UI/WhatIfLauncherForm.vb
' Non-modal launcher for the offline What-If replay runner (proposal W7).
' Opened via the MainForm "What-If Replay" link in SETTINGS & TOOLS.
'
' A LAUNCHER ONLY — all logic lives in the host-agnostic runner (tools/WhatIfRunner).
' This form:
'   1. lets the trader fill a whitelisted-knob grid (each row: blank=inherit live,
'      a single number=pinned, or "from:to:step"=swept — the runner's one semantic),
'   2. writes the overlay JSON to a temp file,
'   3. Process.Start's WhatIfRunner.exe over the live analysis_log.csv + span,
'   4. opens the produced whatif_report_*.md in the existing AnalysisReportForm viewer.
' The TweakSettingsForm precedent, exactly. The runner is the source of truth: it
' re-validates the overlay against the §2 whitelist and rejects anything off-list loudly.
'
' Zero scoring impact; the runner never writes settings.json.

Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Windows.Forms

Public Class WhatIfLauncherForm
    Inherits Form

    ' Whitelisted knobs (label → dotted path → example hint). Mirrors the runner's §2
    ' whitelist for convenience; the runner remains the authority and rejects any drift.
    ' Each Example is a grey placeholder showing a realistic PIN value (the live v53
    ' setting) and a realistic SWEEP around it — the "value or from:to:step" cue in situ.
    ' Each row carries the placeholder Example (grey hint) and its Sweep default — the
    ' sweep string the "Prefill default sweep ranges" checkbox drops into the field.
    Private Shared ReadOnly Knobs As (Label As String, Path As String, Example As String, Sweep As String)() = {
        ("ATR target mult", "scoring.atr_target_multiplier", "1.75  or  1.5:2.5:0.25", "1.5:2.5:0.25"),
        ("ATR stop mult", "scoring.atr_stop_multiplier", "1.6  or  1.4:2.2:0.2", "1.4:2.2:0.2"),
        ("Verdict STRONG %", "scoring.verdict_strong_pct", "0.70  or  0.65:0.80:0.05", "0.65:0.80:0.05"),
        ("Verdict MED %", "scoring.verdict_med_pct", "0.53  or  0.45:0.60:0.05", "0.45:0.60:0.05"),
        ("Verdict WEAK %", "scoring.verdict_weak_pct", "0.35  or  0.30:0.45:0.05", "0.30:0.45:0.05"),
        ("Min net move % (after fees)", "scoring.trade_costs.min_net_move_pct", "0.0005  or  0.0003:0.0009:0.0002", "0.0003:0.0009:0.0002"),
        ("Target max ×ATR", "scoring.structural_levels.target_max_atr_mult", "3.5  or  3.0:4.0:0.5", "3.0:4.0:0.5"),
        ("Stop max ×ATR", "scoring.structural_levels.stop_max_atr_mult", "2.0  or  1.6:2.4:0.2", "1.6:2.4:0.2"),
        ("Stop min floor ticks", "scoring.structural_levels.stop_min_floor_ticks", "4  or  4:8:2", "4:8:2"),
        ("NY fallback ×ATR", "scoring.structural_levels.sessions.NY.fallback_target_atr_mult", "1.75  or  1.5:2.25:0.25", "1.5:2.25:0.25"),
        ("LONDON fallback ×ATR", "scoring.structural_levels.sessions.LONDON.fallback_target_atr_mult", "2.0  or  1.75:2.5:0.25", "1.75:2.5:0.25"),
        ("ASIA fallback ×ATR", "scoring.structural_levels.sessions.ASIA.fallback_target_atr_mult", "1.25  or  1.0:1.5:0.25", "1.0:1.5:0.25"),
        ("Use best-pivot candidate", "scoring.structural_levels.use_best_pivot_candidate", "0  or  0:1:1", "0:1:1"),
        ("Eval window (bars 5/10/15)", "eval_window", "15  or  5:15:5", "5:15:5")
    }

    Private ReadOnly _fields As New Dictionary(Of String, TextBox)()
    Private WithEvents chkSweepDefaults As CheckBox
    Private WithEvents txtFrom As TextBox
    Private WithEvents txtTo As TextBox
    Private WithEvents txtConstraints As TextBox
    Private WithEvents btnRun As Button
    Private WithEvents btnOpenLast As Button
    Private lblStatus As Label

    Private ReadOnly _repoRoot As String
    Private ReadOnly _runnerExe As String
    Private ReadOnly _csvPath As String
    Private ReadOnly _settingsPath As String

    Public Sub New()
        _repoRoot = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", ".."))
        _runnerExe = ResolveRunnerExe(_repoRoot)
        _csvPath = Path.Combine(Application.StartupPath, "analysis_log.csv")
        _settingsPath = Path.Combine(_repoRoot, "settings.json")
        InitializeComponent()
    End Sub

    ' [2026-07-29] Prefer the NEWEST built runner across Release/Debug. The launcher
    ' originally hardcoded bin\Debug, but implementation lanes build Release-only while
    ' the Debug collector runs (the 07-17 stomp rule), so the Debug exe goes stale and
    ' rejects newly-whitelisted knobs (first hit: use_best_pivot_candidate, v63).
    ' Neither exists ⇒ return the Debug path so the existing "not built" message fires.
    Private Shared Function ResolveRunnerExe(repoRoot As String) As String
        Dim releaseExe As String = Path.Combine(repoRoot, "tools", "WhatIfRunner", "bin", "Release", "net8.0", "WhatIfRunner.exe")
        Dim debugExe As String = Path.Combine(repoRoot, "tools", "WhatIfRunner", "bin", "Debug", "net8.0", "WhatIfRunner.exe")
        Dim best As String = debugExe
        Dim bestTime As Date = Date.MinValue
        For Each c As String In {releaseExe, debugExe}
            If File.Exists(c) AndAlso File.GetLastWriteTimeUtc(c) > bestTime Then
                bestTime = File.GetLastWriteTimeUtc(c)
                best = c
            End If
        Next
        Return best
    End Function

    ' -- Run ------------------------------------------------------------------------------
    Private Sub btnRun_Click(sender As Object, e As EventArgs) Handles btnRun.Click
        If Not File.Exists(_runnerExe) Then
            SetStatus("WhatIfRunner.exe not built. Build tools/WhatIfRunner first.", Color.Orange)
            Return
        End If

        Dim overlayJson As String
        Try
            overlayJson = BuildOverlayJson()
        Catch ex As Exception
            SetStatus("Overlay build error: " & ex.Message, Color.Orange)
            Return
        End Try

        Dim overlayPath As String = Path.Combine(Path.GetTempPath(),
            "whatif_overlay_" & DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") & ".json")
        Try
            File.WriteAllText(overlayPath, overlayJson)
        Catch ex As Exception
            SetStatus("Could not write overlay: " & ex.Message, Color.Orange)
            Return
        End Try

        Dim args As New System.Text.StringBuilder()
        args.Append("""" & overlayPath & """")
        If Not String.IsNullOrWhiteSpace(txtFrom.Text) Then args.Append(" --from " & txtFrom.Text.Trim())
        If Not String.IsNullOrWhiteSpace(txtTo.Text) Then args.Append(" --to " & txtTo.Text.Trim())
        args.Append(" --csv """ & _csvPath & """")
        args.Append(" --settings """ & _settingsPath & """")
        args.Append(" --out """ & _repoRoot & """")

        btnRun.Enabled = False
        SetStatus("Running replay (fetching OHLC)…", Color.Yellow)

        Dim psi As New ProcessStartInfo() With {
            .FileName = _runnerExe,
            .Arguments = args.ToString(),
            .WorkingDirectory = _repoRoot,
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True
        }
        Try
            Dim proc = Process.Start(psi)
            If proc Is Nothing Then
                SetStatus("Failed to start runner.", Color.Orange)
                btnRun.Enabled = True
                Return
            End If
            proc.EnableRaisingEvents = True
            AddHandler proc.Exited, Sub(s, ev)
                Dim code = proc.ExitCode
                Dim errText = proc.StandardError.ReadToEnd()
                Me.BeginInvoke(Sub()
                    btnRun.Enabled = True
                    If code = 0 Then
                        SetStatus("Done. Opening report…", Color.FromArgb(80, 220, 120))
                        OpenLatestReport()
                    Else
                        SetStatus("Runner exit " & code & ": " & FirstLine(errText), Color.Orange)
                    End If
                End Sub)
            End Sub
        Catch ex As Exception
            SetStatus("Start failed: " & ex.Message, Color.Orange)
            btnRun.Enabled = True
        End Try
    End Sub

    Private Sub btnOpenLast_Click(sender As Object, e As EventArgs) Handles btnOpenLast.Click
        OpenLatestReport()
    End Sub

    ' Checked → drop each knob's default sweep range into its field; unchecked → clear them
    ' back to blank (inherit live). The user then edits down to the knob(s) they actually want.
    Private Sub chkSweepDefaults_CheckedChanged(sender As Object, e As EventArgs) Handles chkSweepDefaults.CheckedChanged
        For Each k In Knobs
            _fields(k.Path).Text = If(chkSweepDefaults.Checked, k.Sweep, "")
        Next
    End Sub

    Private Sub OpenLatestReport()
        Try
            Dim files = Directory.GetFiles(_repoRoot, "whatif_report_*.md")
            If files.Length = 0 Then
                SetStatus("No whatif_report_*.md found in repo root yet.", Color.Orange)
                Return
            End If
            Dim latest = files.OrderByDescending(Function(f) File.GetLastWriteTimeUtc(f)).First()
            Dim md = File.ReadAllText(latest)
            Dim frm As New AnalysisReportForm(md, latest)
            MainForm.PositionOnParentScreen(frm, Me)
            frm.Show(Me)
        Catch ex As Exception
            SetStatus("Could not open report: " & ex.Message, Color.Orange)
        End Try
    End Sub

    ' Build the overlay JSON from the knob grid. Empty = inherit; "a:b:c" = sweep; else pinned.
    Private Function BuildOverlayJson() As String
        Dim root As New JsonObject()
        For Each kv In _fields
            Dim text = kv.Value.Text.Trim()
            If String.IsNullOrEmpty(text) Then Continue For
            Dim node As JsonNode
            If text.Contains(":") Then
                Dim parts = text.Split(":"c)
                If parts.Length <> 3 Then Throw New Exception("Sweep for '" & kv.Key & "' must be from:to:step.")
                node = New JsonObject From {
                    {"sweep", New JsonObject From {
                        {"from", ParseNum(parts(0), kv.Key)},
                        {"to", ParseNum(parts(1), kv.Key)},
                        {"step", ParseNum(parts(2), kv.Key)}}}}
            Else
                node = JsonValue.Create(ParseNum(text, kv.Key))
            End If
            SetPath(root, kv.Key, node)
        Next

        Dim constraints = txtConstraints.Text.Trim()
        If Not String.IsNullOrEmpty(constraints) Then
            ' Spliced verbatim as the constraints array — the runner validates it.
            Dim arr = JsonNode.Parse(constraints)
            root("constraints") = arr
        End If

        Return root.ToJsonString(New JsonSerializerOptions With {.WriteIndented = True})
    End Function

    Private Shared Function ParseNum(s As String, label As String) As Double
        Dim v As Double
        If Not Double.TryParse(s.Trim(), Globalization.NumberStyles.Float,
                               Globalization.CultureInfo.InvariantCulture, v) Then
            Throw New Exception("'" & s & "' (" & label & ") is not a number.")
        End If
        Return v
    End Function

    Private Shared Sub SetPath(root As JsonObject, dottedPath As String, value As JsonNode)
        Dim parts = dottedPath.Split("."c)
        Dim cur = root
        For i = 0 To parts.Length - 2
            Dim child = TryCast(cur(parts(i)), JsonObject)
            If child Is Nothing Then
                child = New JsonObject()
                cur(parts(i)) = child
            End If
            cur = child
        Next
        cur(parts(parts.Length - 1)) = value
    End Sub

    Private Shared Function FirstLine(s As String) As String
        If String.IsNullOrEmpty(s) Then Return "(no detail)"
        Dim nl = s.IndexOfAny(New Char() {ChrW(10), ChrW(13)})
        Return If(nl > 0, s.Substring(0, nl), s).Trim()
    End Function

    Private Sub SetStatus(text As String, colour As Color)
        If Me.InvokeRequired Then
            Me.Invoke(Sub() SetStatus(text, colour))
            Return
        End If
        lblStatus.Text = text
        lblStatus.ForeColor = colour
    End Sub

    ' -- InitializeComponent (code-defined, TweakSettingsForm dark theme) ------------------
    Private Sub InitializeComponent()
        Me.SuspendLayout()
        Me.Text = "What-If Replay"
        Me.Size = New Size(560, 640)
        Me.MinimumSize = New Size(480, 480)
        Me.BackColor = Color.FromArgb(30, 30, 30)
        Me.ForeColor = Color.FromArgb(200, 200, 200)
        Me.Font = New Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = FormBorderStyle.Sizable
        Me.StartPosition = FormStartPosition.Manual
        Me.Location = New Point(120, 120)
        Me.AutoScroll = True

        Const LBL_X As Integer = 12
        Const CTL_X As Integer = 220
        Const CTL_W As Integer = 300
        Dim y As Integer = 12

        AddHeader("Overlay knobs (blank = inherit live · single value = pin · from:to:step = sweep)", LBL_X, y) : y += 22

        chkSweepDefaults = New CheckBox() With {
            .Text = "Prefill default sweep ranges (then edit down — all knobs at once exceeds the grid cap)",
            .Location = New Point(LBL_X, y), .Size = New Size(CTL_W + CTL_X - LBL_X, 20),
            .BackColor = Color.Transparent, .ForeColor = Color.FromArgb(170, 170, 170)}
        Me.Controls.Add(chkSweepDefaults)
        y += 26

        For Each k In Knobs
            Dim tb As TextBox = Nothing
            AddRow(k.Label & ":", LBL_X, CTL_X, y, CTL_W, tb)
            tb.PlaceholderText = k.Example
            _fields(k.Path) = tb
            y += 28
        Next
        y += 6

        AddHeader("Constraints (optional raw JSON array — see docs)", LBL_X, y) : y += 24
        txtConstraints = MakeTextBox(LBL_X, y, CTL_W + CTL_X - LBL_X, 44)
        txtConstraints.Multiline = True
        ' Realistic ratio-constraint example (keep the diagonal stops coupled 1:1).
        txtConstraints.PlaceholderText =
            "[{""ratio"":{""of"":[""scoring.structural_levels.stop_max_atr_mult"",""scoring.atr_stop_multiplier""],""min"":1.0,""max"":1.0}}]"
        Me.Controls.Add(txtConstraints)
        y += 52

        AddRow("From (yyyy-MM-dd):", LBL_X, CTL_X, y, 140, txtFrom)
        txtFrom.PlaceholderText = "2026-07-10"
        y += 28
        AddRow("To (yyyy-MM-dd):", LBL_X, CTL_X, y, 140, txtTo)
        txtTo.PlaceholderText = "2026-07-16"
        y += 34

        btnRun = New Button() With {
            .Text = "Run Replay", .Location = New Point(LBL_X, y), .Size = New Size(140, 30),
            .BackColor = Color.FromArgb(60, 80, 60), .ForeColor = Color.FromArgb(210, 210, 210),
            .FlatStyle = FlatStyle.Flat, .TabStop = True}
        Me.Controls.Add(btnRun)

        btnOpenLast = New Button() With {
            .Text = "Open Last Report", .Location = New Point(LBL_X + 150, y), .Size = New Size(150, 30),
            .BackColor = Color.FromArgb(50, 70, 90), .ForeColor = Color.FromArgb(210, 210, 210),
            .FlatStyle = FlatStyle.Flat, .TabStop = True}
        Me.Controls.Add(btnOpenLast)
        y += 38

        lblStatus = New Label() With {
            .Text = "Ready. Reports write to the repo root; the runner never touches settings.json.",
            .Location = New Point(LBL_X, y), .Size = New Size(CTL_W + CTL_X - LBL_X, 40),
            .ForeColor = Color.FromArgb(160, 160, 160), .AutoSize = False}
        Me.Controls.Add(lblStatus)

        Me.ResumeLayout(False)
    End Sub

    Private Sub AddHeader(text As String, x As Integer, y As Integer)
        Me.Controls.Add(New Label() With {
            .Text = text, .Location = New Point(x, y), .Size = New Size(500, 18),
            .Font = New Font("Segoe UI", 9.0!, FontStyle.Bold), .ForeColor = Color.FromArgb(180, 180, 180)})
    End Sub

    Private Sub AddRow(labelText As String, lx As Integer, cx As Integer, y As Integer,
                       ctlW As Integer, ByRef ctl As TextBox)
        Me.Controls.Add(New Label() With {
            .Text = labelText, .Location = New Point(lx, y + 2), .Size = New Size(cx - lx - 4, 20),
            .ForeColor = Color.FromArgb(160, 160, 160)})
        ctl = MakeTextBox(cx, y, ctlW, 22)
        Me.Controls.Add(ctl)
    End Sub

    Private Function MakeTextBox(x As Integer, y As Integer, w As Integer, h As Integer) As TextBox
        Return New TextBox() With {
            .Location = New Point(x, y), .Size = New Size(w, h),
            .BackColor = Color.FromArgb(45, 45, 45), .ForeColor = Color.FromArgb(210, 210, 210),
            .BorderStyle = BorderStyle.FixedSingle}
    End Function
End Class
