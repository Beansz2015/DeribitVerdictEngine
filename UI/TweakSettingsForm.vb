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
    Private WithEvents chkAutoCommit         As CheckBox
    Private WithEvents chkDryRun             As CheckBox
    Private WithEvents txtWindowSize         As TextBox
    Private            txtFailThreshold      As TextBox
    Private            txtCooldownRows       As TextBox
    Private WithEvents txtMinTierRows        As TextBox
    Private            txtSnapshotStreakX    As TextBox
    Private            txtMaxKeysPerProposal As TextBox
    Private            txtStreakWeight       As TextBox

    ' True while the MinTier textbox is showing the computed default. Cleared when the
    ' user manually edits the value, so changes to WindowSize stop auto-updating it.
    Private _minTierIsAuto As Boolean = True
    Private _suppressMinTierEdit As Boolean = False
    ' Held as a FIELD, not a local: WinForms controls keep no back-reference to the ToolTip
    ' that serves them, so a locally-scoped one is collectible and the hover silently dies
    ' (the c508d93 / _minNetMoveTip pattern; fee-aware-min-move-spec-back.md §2.9).
    Private            _minTierTip           As ToolTip
    Private            lblActiveSnapshot     As Label
    Private WithEvents btnShowRoundStats     As Button
    Private WithEvents btnOpenSnapshotsDir   As Button
    Private            lblConfigPath         As Label
    Private            lblCsvPath            As Label
    Private            lblStatePath          As Label
    Private            lblTweakerStatus      As Label
    Private WithEvents btnRunNow             As Button
    Private WithEvents btnSave               As Button
    Private            lblLastSummary        As Label
    Private WithEvents _pollTimer            As Timer
    Private            _roundStatsForm       As RoundStatsForm

    ' ── Paths (resolved once at construction) ───────────────────────────────
    Private ReadOnly _repoRoot      As String
    Private ReadOnly _configPath    As String
    Private ReadOnly _statePath     As String
    Private ReadOnly _csvPath       As String
    Private ReadOnly _tweakerExe    As String
    Private ReadOnly _snapshotsDir  As String
    Private ReadOnly _manifestPath  As String
    Private ReadOnly _mainForm      As MainForm

    Public Sub New(owner As MainForm)
        InitializeComponent()
        _mainForm = owner

        ' Resolve paths from the running executable's location.
        ' Application.StartupPath = bin/Debug/net8.0-windows/ — go 3 levels up to repo root.
        _repoRoot     = Path.GetFullPath(Path.Combine(Application.StartupPath, "..", "..", ".."))
        _configPath   = Path.Combine(_repoRoot, "tools", "AutoTweaker", "tweaker_config.json")
        _statePath    = Path.Combine(_repoRoot, "tools", "AutoTweaker", "state.json")
        _csvPath      = Path.Combine(Application.StartupPath, "analysis_log.csv")
        _tweakerExe   = Path.Combine(_repoRoot, "tools", "AutoTweaker", "bin", "Debug", "net8.0", "AutoTweaker.exe")
        _snapshotsDir = Path.Combine(_repoRoot, "settings_snapshots")
        _manifestPath = Path.Combine(_snapshotsDir, "manifest.csv")

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
        Dim isFixedMode As Boolean = String.Equals(cfg.WindowMode,
            TweakerConfig.WindowModeFixed, StringComparison.OrdinalIgnoreCase)
        Dim minTierThreshold As Integer = cfg.EffectiveMinTier(cfg.WindowSizeVerdicts)
        Dim currentRowCount As Integer = CountCsvRows()

        If isFixedMode Then
            ' [v36 Phase-2a] Status mirrors the population filter the core applies:
            ' count only the (session × resolution) population, and surface a pending
            ' re-seed (filter change OR a CSV shrink such as the v0.6->v0.7 migration)
            ' instead of a negative count. Mirrors AutoTweakerCore re-seed-on-change +
            ' shrink guard; LastEvaluatedRowIndex indexes the FILTERED sequence.
            Dim pop = cfg.PopulationFilter
            Dim popRowCount As Integer = If(pop Is Nothing, currentRowCount, CountPopulationRows(pop))
            Dim popKey As String = If(pop Is Nothing, "none",
                String.Format("{0}|{1}", pop.Session,
                    If(pop.ExecutionResolution.HasValue, pop.ExecutionResolution.Value.ToString(), "")))
            Dim popLabel As String = If(pop Is Nothing, "all rows",
                String.Format("{0}×{1}m", If(pop.Session, "any"),
                    If(pop.ExecutionResolution.HasValue, pop.ExecutionResolution.Value.ToString(), "any")))

            ' First-run path — index is uninitialised; first run seeds it.
            If state.LastEvaluatedRowIndex < 0 Then
                SetStatus(String.Format("Awaiting first-run seed (rows so far: {0})", popRowCount),
                          Color.Orange)
                btnRunNow.Enabled = True
                UpdateSummaryLabel(state)
                Return
            End If

            ' Re-seed pending: a population-filter change or a CSV shrink (e.g. the
            ' v0.6->v0.7 migration rotating the book) leaves LastEvaluatedRowIndex
            ' stale; the next tweaker run re-seeds it. Mirror that here so the status
            ' shows the pending re-seed instead of a negative accumulation.
            If pop IsNot Nothing AndAlso
               (state.PopulationFilterKey <> popKey OrElse state.LastEvaluatedRowIndex > popRowCount) Then
                SetStatus(String.Format(
                    "Re-seed pending (book rotated or population filter changed) — next run re-seeds. {0} rows: {1}",
                    popLabel, popRowCount), Color.Orange)
                btnRunNow.Enabled = True
                UpdateSummaryLabel(state)
                Return
            End If

            Dim accumulated As Integer = popRowCount - state.LastEvaluatedRowIndex
            If accumulated < cfg.WindowSizeVerdicts Then
                Dim startRow As Integer = state.LastEvaluatedRowIndex + 1
                Dim endRow   As Integer = state.LastEvaluatedRowIndex + cfg.WindowSizeVerdicts
                SetStatus(String.Format("Next round ({4}): {0}/{1} rows accumulating (population rows {2}..{3})",
                                        accumulated, cfg.WindowSizeVerdicts, startRow, endRow, popLabel),
                          Color.Orange)
                btnRunNow.Enabled = False
                UpdateSummaryLabel(state)
                Return
            End If

            ' MinTier preview against the prospective window.
            Dim tierEligible As Integer = CountTierEligibleInWindow(cfg.WindowSizeVerdicts)
            If tierEligible < minTierThreshold Then
                SetStatus(String.Format("Window full but tier-eligible {0}/{1} — round will SKIP",
                                        tierEligible, minTierThreshold), Color.Orange)
                btnRunNow.Enabled = True
                UpdateSummaryLabel(state)
                Return
            End If

            SetStatus("Ready", Color.FromArgb(80, 220, 120))
            btnRunNow.Enabled = True
            UpdateSummaryLabel(state)
            Return
        End If

        ' ── Sliding (legacy) mode status flow ────────────────────────────────
        Dim rowsSinceLast As Integer = currentRowCount - state.LastRunCsvRowCount
        If state.LastRunCsvRowCount > 0 AndAlso rowsSinceLast < cfg.CooldownRows Then
            Dim remaining = cfg.CooldownRows - rowsSinceLast
            SetStatus(String.Format("Cooldown: {0} rows remaining", remaining), Color.Orange)
            btnRunNow.Enabled = False
            UpdateSummaryLabel(state)
            Return
        End If

        Dim sessionRows As Integer = CountCurrentSessionRows(cfg.WindowSizeVerdicts * 2)
        If sessionRows < cfg.WindowSizeVerdicts Then
            SetStatus(String.Format("Waiting for session-aligned window: {0}/{1} rows",
                                    sessionRows, cfg.WindowSizeVerdicts), Color.Orange)
            btnRunNow.Enabled = False
            UpdateSummaryLabel(state)
            Return
        End If

        Dim tierEligibleS As Integer = CountTierEligibleInWindow(cfg.WindowSizeVerdicts)
        If tierEligibleS < minTierThreshold Then
            SetStatus(String.Format("Insufficient tier-eligible rows: {0}/{1}",
                                    tierEligibleS, minTierThreshold), Color.Orange)
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

        ' Active snapshot + streak counter
        Dim cfg = TweakerConfig.Load(_configPath)
        Dim snapshotText As String
        If String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then
            snapshotText = String.Format("Streak: {0}/{1}  Active snapshot: none",
                                          state.CurrentBelowThresholdStreak,
                                          cfg.SnapshotStreakX)
        Else
            snapshotText = String.Format("Streak: {0}/{1}  Active snapshot: {2}",
                                          state.CurrentBelowThresholdStreak,
                                          cfg.SnapshotStreakX,
                                          state.ActiveSnapshotFilename)
        End If
        lblActiveSnapshot.Text = snapshotText

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

    ' [v36 Phase-2a] Count CSV data rows in the given (session × resolution)
    ' population — the form-side mirror of AutoTweakerCore.MatchesPopulation so the
    ' status line reflects the population the tweaker actually evaluates, not raw
    ' rows. Resolution from the ExecResolution column (v0.7; absent/legacy ⇒ 1);
    ' session from the shared engine bucket (ExecutionResolution.MatchSessionBucket,
    ' inclusive <=). Falls back to the raw count on any parse trouble.
    Private Function CountPopulationRows(pop As PopulationFilter) As Integer
        If pop Is Nothing Then Return CountCsvRows()
        Try
            If Not File.Exists(_csvPath) Then Return 0
            Dim lines As String() = File.ReadAllLines(_csvPath)
            If lines.Length < 2 Then Return 0

            Dim headers As String() = lines(0).Split(","c)
            Dim tsIdx As Integer = -1, resIdx As Integer = -1
            For i As Integer = 0 To headers.Length - 1
                Dim h As String = headers(i).Trim()
                If h.Equals("Timestamp", StringComparison.OrdinalIgnoreCase) Then tsIdx = i
                If h.Equals("ExecResolution", StringComparison.OrdinalIgnoreCase) Then resIdx = i
            Next

            Dim needSession As Boolean = Not String.IsNullOrEmpty(pop.Session)
            Dim settings = SettingsLoader.Current
            ' Can't derive session without timestamps or settings → fall back to raw.
            If needSession AndAlso (tsIdx < 0 OrElse settings Is Nothing) Then Return CountCsvRows()

            Dim count As Integer = 0
            For i As Integer = 1 To lines.Length - 1
                Dim parts As String() = lines(i).Split(","c)

                ' Resolution (default 1 when the column is absent/unparseable — legacy v0.6).
                Dim execRes As Integer = 1
                If resIdx >= 0 AndAlso parts.Length > resIdx Then
                    Dim rv As Integer
                    If Integer.TryParse(parts(resIdx).Trim(), rv) Then execRes = rv
                End If
                If pop.ExecutionResolution.HasValue AndAlso execRes <> pop.ExecutionResolution.Value Then Continue For

                ' Session via the shared engine bucket (only when the filter pins a session).
                If needSession Then
                    If parts.Length <= tsIdx Then Continue For
                    Dim ts As DateTime
                    If Not DateTime.TryParse(parts(tsIdx).Trim(), Nothing,
                            Globalization.DateTimeStyles.AssumeUniversal Or
                            Globalization.DateTimeStyles.AdjustToUniversal, ts) Then Continue For
                    Dim b = ExecutionResolution.MatchSessionBucket(settings, ts.Hour)
                    If b Is Nothing OrElse
                       Not String.Equals(b.Name, pop.Session, StringComparison.OrdinalIgnoreCase) Then Continue For
                End If

                count += 1
            Next
            Return count
        Catch
            Return CountCsvRows()
        End Try
    End Function

    ' Walk back from the end of the CSV and count consecutive rows that fall within
    ' the current UTC session (i.e. no session-start hour is crossed).
    ' maxScan caps how many lines we read to keep the poll cheap.
    ' Mirrors AutoTweakerCore step 2 session logic.
    Private Function CountCurrentSessionRows(maxScan As Integer) As Integer
        Try
            If Not File.Exists(_csvPath) Then Return 0
            Dim lines As String() = File.ReadAllLines(_csvPath)
            If lines.Length < 2 Then Return 0

            ' Load session start hours from settings.json
            Dim sessionStarts As New HashSet(Of Integer)()
            Try
                Dim settingsPath As String = Path.Combine(_repoRoot, "settings.json")
                If File.Exists(settingsPath) Then
                    Dim doc = JsonDocument.Parse(File.ReadAllText(settingsPath))
                    Dim svEl As JsonElement
                    If doc.RootElement.TryGetProperty("session_volume", svEl) Then
                        Dim sessArr As JsonElement
                        If svEl.TryGetProperty("sessions", sessArr) Then
                            For Each s In sessArr.EnumerateArray()
                                Dim hEl As JsonElement
                                If s.TryGetProperty("start_hour", hEl) Then
                                    sessionStarts.Add(hEl.GetInt32())
                                End If
                            Next
                        End If
                    End If
                End If
            Catch
            End Try
            If sessionStarts.Count = 0 Then
                sessionStarts.Add(0)   ' default: midnight UTC
                sessionStarts.Add(13)  ' default: NY open UTC
            End If

            ' Find Timestamp column index
            Dim headers As String() = lines(0).Split(","c)
            Dim tsIdx As Integer = 0  ' default first column
            For i As Integer = 0 To headers.Length - 1
                If headers(i).Trim().Equals("Timestamp", StringComparison.OrdinalIgnoreCase) Then
                    tsIdx = i : Exit For
                End If
            Next

            ' Walk backwards, counting rows until a session boundary is crossed
            Dim prevTs As DateTime = DateTime.MinValue
            Dim count As Integer = 0
            Dim scanStart As Integer = Math.Max(1, lines.Length - maxScan)
            For i As Integer = lines.Length - 1 To scanStart Step -1
                Dim parts As String() = lines(i).Split(","c)
                If parts.Length <= tsIdx Then Continue For
                Dim ts As DateTime
                If Not DateTime.TryParse(parts(tsIdx).Trim(),
                                         Nothing,
                                         Globalization.DateTimeStyles.AssumeUniversal Or
                                         Globalization.DateTimeStyles.AdjustToUniversal,
                                         ts) Then Continue For

                If prevTs = DateTime.MinValue Then
                    ' First (most-recent) row — anchor the session
                    prevTs = ts
                    count += 1
                    Continue For
                End If

                If FormCrossesSessionBoundary(ts, prevTs, sessionStarts) Then
                    Exit For   ' hit a session boundary; stop counting
                End If

                prevTs = ts
                count += 1
            Next
            Return count
        Catch
            Return 0
        End Try
    End Function

    ' Returns True if any session-start hour falls inside the half-open interval
    ' (earlier, later] of the two timestamps — date-aware.
    ' Identical logic to AutoTweakerCore.CrossesSessionBoundary.
    Private Shared Function FormCrossesSessionBoundary(t1 As DateTime, t2 As DateTime,
                                                        sessionStarts As HashSet(Of Integer)) As Boolean
        If t1 = DateTime.MinValue OrElse t2 = DateTime.MinValue Then Return True
        Dim earlier As DateTime = If(t1 <= t2, t1, t2)
        Dim later   As DateTime = If(t1 <= t2, t2, t1)
        ' A span of a full day or more crosses every session-start hour.
        If (later - earlier).TotalHours >= 24.0 Then Return True
        ' Walk each top-of-hour boundary in (earlier, later] and test its hour.
        Dim boundary As DateTime = earlier.Date.AddHours(earlier.Hour + 1)
        Do While boundary <= later
            If sessionStarts.Contains(boundary.Hour) Then Return True
            boundary = boundary.AddHours(1)
        Loop
        Return False
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
            MessageBox.Show(Me, "Failed to start AutoTweaker: " & ex.Message,
                            "Tweak Settings", MessageBoxButtons.OK, MessageBoxIcon.Error)
            UpdateStatusLabel()
        End Try
    End Sub

    Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
        ' Validate inputs
        Dim windowSize As Integer
        Dim failThreshold As Double
        Dim cooldown As Integer
        Dim snapshotStreakX As Integer
        Dim maxKeysPerProposal As Integer
        Dim streakWeight As Double

        If Not Integer.TryParse(txtWindowSize.Text, windowSize) OrElse windowSize < 10 Then
            MessageBox.Show(Me, "Window size must be an integer >= 10.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not Double.TryParse(txtFailThreshold.Text,
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               failThreshold) OrElse
               failThreshold < 1 OrElse failThreshold > 99 Then
            MessageBox.Show(Me, "Failure threshold must be between 1 and 99.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not Integer.TryParse(txtCooldownRows.Text, cooldown) OrElse cooldown < 1 Then
            MessageBox.Show(Me, "Cooldown rows must be a positive integer.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' ── MinTier validation (auto-tweaker-fixed-window-proposal §4) ───────
        Dim minTier As Integer = 0
        Dim minTierIsBlank As Boolean = String.IsNullOrWhiteSpace(txtMinTierRows.Text)
        If Not minTierIsBlank Then
            If Not Integer.TryParse(txtMinTierRows.Text, minTier) OrElse minTier < 0 Then
                MessageBox.Show(Me, "Min tier-eligible rows must be a non-negative integer (or blank for auto).",
                                "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            If minTier < 5 Then
                MessageBox.Show(Me, "Min ≥ 5 required for any statistical meaning.", "Invalid input",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            If minTier > windowSize Then
                MessageBox.Show(Me, "Cannot exceed Window Size — gate would be unreachable.", "Invalid input",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            If minTier > CInt(Math.Floor(windowSize * 0.7)) Then
                Dim res = MessageBox.Show(Me,
                    "MinTier exceeds 70% of WindowSize. Many rounds may be skipped if NO_TRADE density is high. Proceed?",
                    "MinTier high",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
                If res <> DialogResult.OK Then Return
            End If
        End If

        If Not Integer.TryParse(txtSnapshotStreakX.Text, snapshotStreakX) OrElse snapshotStreakX < 1 Then
            MessageBox.Show(Me, "Snapshot streak X must be a positive integer.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not Integer.TryParse(txtMaxKeysPerProposal.Text, maxKeysPerProposal) OrElse maxKeysPerProposal < 1 Then
            MessageBox.Show(Me, "Max keys per tweak proposal must be a positive integer.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If Not Double.TryParse(txtStreakWeight.Text,
                               System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture,
                               streakWeight) OrElse streakWeight < 0 Then
            MessageBox.Show(Me, "Streak weight must be a non-negative number.", "Invalid input",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' Read existing config to preserve other fields, then update
        Dim cfg = TweakerConfig.Load(_configPath)
        cfg.WindowSizeVerdicts       = windowSize
        cfg.FailureRateThresholdPct  = failThreshold
        cfg.CooldownRows             = cooldown
        ' Blank textbox OR value matching the auto-tracking flag → store null so the
        ' formula recomputes against future WindowSize changes. Otherwise honour the
        ' explicit user value.
        If minTierIsBlank OrElse _minTierIsAuto Then
            cfg.MinTierEligibleRows  = Nothing
        Else
            cfg.MinTierEligibleRows  = minTier
        End If
        cfg.AutoCommitEnabled        = chkAutoCommit.Checked
        cfg.DryRunEnabled            = chkDryRun.Checked
        cfg.SnapshotStreakX          = snapshotStreakX
        cfg.MaxKeysPerProposal       = maxKeysPerProposal
        cfg.StreakWeight             = streakWeight
        cfg.CsvPath                  = _csvPath
        cfg.SettingsPath             = Path.Combine(_repoRoot, "settings.json")
        cfg.StatePath                = _statePath
        cfg.SnapshotsDir             = _snapshotsDir
        cfg.ManifestPath             = _manifestPath

        Try
            TweakerConfig.Save(_configPath, cfg)
            MessageBox.Show(Me, "tweaker_config.json saved successfully.", "Tweak Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(Me, "Save failed: " & ex.Message, "Tweak Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnShowRoundStats_Click(sender As Object, e As EventArgs) Handles btnShowRoundStats.Click
        If _roundStatsForm Is Nothing OrElse _roundStatsForm.IsDisposed Then
            _roundStatsForm = New RoundStatsForm(_configPath, _statePath, _csvPath,
                                                  _snapshotsDir, _manifestPath)
            MainForm.PositionOnParentScreen(_roundStatsForm, Me)
            _roundStatsForm.Show(Me)
        Else
            If _roundStatsForm.WindowState = FormWindowState.Minimized Then
                _roundStatsForm.WindowState = FormWindowState.Normal
            End If
            _roundStatsForm.BringToFront()
            _roundStatsForm.Activate()
        End If
    End Sub

    Private Sub btnOpenSnapshotsDir_Click(sender As Object, e As EventArgs) Handles btnOpenSnapshotsDir.Click
        Try
            If Not Directory.Exists(_snapshotsDir) Then Directory.CreateDirectory(_snapshotsDir)
            Process.Start(New ProcessStartInfo() With {
                .FileName        = _snapshotsDir,
                .UseShellExecute = True
            })
        Catch ex As Exception
            MessageBox.Show(Me, "Could not open snapshots directory: " & ex.Message, "Tweak Settings",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ── Config loading ────────────────────────────────────────────────────────
    Private Sub LoadConfigIntoControls()
        Dim cfg = TweakerConfig.Load(_configPath)
        chkAutoCommit.Checked        = cfg.AutoCommitEnabled
        chkDryRun.Checked            = cfg.DryRunEnabled
        txtWindowSize.Text           = cfg.WindowSizeVerdicts.ToString()
        txtFailThreshold.Text        = cfg.FailureRateThresholdPct.ToString("F0")
        txtCooldownRows.Text         = cfg.CooldownRows.ToString()
        txtSnapshotStreakX.Text      = cfg.SnapshotStreakX.ToString()
        txtMaxKeysPerProposal.Text   = cfg.MaxKeysPerProposal.ToString()
        txtStreakWeight.Text         = cfg.StreakWeight.ToString("F2",
            System.Globalization.CultureInfo.InvariantCulture)

        ' MinTier: null in JSON → auto-track WindowSize via formula. Otherwise the
        ' user has fixed it explicitly, so honour the stored value and disable tracking.
        _suppressMinTierEdit = True
        _minTierIsAuto = Not cfg.MinTierEligibleRows.HasValue
        txtMinTierRows.Text = cfg.EffectiveMinTier(cfg.WindowSizeVerdicts).ToString()
        _suppressMinTierEdit = False
    End Sub

    Private Sub txtWindowSize_TextChanged(sender As Object, e As EventArgs) Handles txtWindowSize.TextChanged
        If Not _minTierIsAuto Then Return
        Dim ws As Integer
        If Not Integer.TryParse(txtWindowSize.Text, ws) OrElse ws < 1 Then Return
        _suppressMinTierEdit = True
        txtMinTierRows.Text = TweakerConfig.ComputeDefaultMinTier(ws).ToString()
        _suppressMinTierEdit = False
    End Sub

    Private Sub txtMinTierRows_TextChanged(sender As Object, e As EventArgs) Handles txtMinTierRows.TextChanged
        If _suppressMinTierEdit Then Return
        _minTierIsAuto = False
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
        Me.Size          = New Size(720, 640)
        Me.MinimumSize   = New Size(600, 600)
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
        y += 28

        ' ── Min tier-eligible rows ───────────────────────────────────────────
        AddRow("Min tier-eligible rows:", LBL_X, CTL_X, y, 60, txtMinTierRows)
        _minTierTip = New ToolTip()
        _minTierTip.SetToolTip(txtMinTierRows,
            "Minimum STRONG/MEDIUM directional rows that must exist within a window for " &
            "the round to evaluate. Rounds with fewer are skipped (don't tick the streak). " &
            "Must be ≤ Window Size — the tier-eligible count is a subset of the window's " &
            "rows, so a larger value can never be reached. Default scales with Window Size " &
            "as max(15, ceil(WindowSize × 0.5)).")
        y += 32

        ' ── Snapshot history section ─────────────────────────────────────────
        Dim snapHeader As New Label() With {
            .Text      = "Snapshot history",
            .Location  = New Point(LBL_X, y),
            .Size      = New Size(300, 18),
            .Font      = New Font("Segoe UI", 9.0!, FontStyle.Bold),
            .ForeColor = Color.FromArgb(180, 180, 180)
        }
        Me.Controls.Add(snapHeader)
        y += 22

        AddRow("Snapshot streak X (default 3):", LBL_X, CTL_X, y, 60, txtSnapshotStreakX)
        y += 28

        AddRow("Max keys per tweak proposal (default 3):", LBL_X, CTL_X, y, 60, txtMaxKeysPerProposal)
        y += 28

        AddRow("Streak weight (default 1.5):", LBL_X, CTL_X, y, 60, txtStreakWeight)
        y += 28

        ' Active snapshot read-only label
        Dim activeHdr As New Label() With {
            .Text      = "Active snapshot:",
            .Location  = New Point(LBL_X, y + 2),
            .Size      = New Size(CTL_X - LBL_X - 4, 20),
            .ForeColor = Color.FromArgb(160, 160, 160)
        }
        Me.Controls.Add(activeHdr)

        lblActiveSnapshot           = New Label()
        lblActiveSnapshot.Location  = New Point(CTL_X, y + 2)
        lblActiveSnapshot.Size      = New Size(W_W - CTL_X + LBL_X, 20)
        lblActiveSnapshot.ForeColor = Color.FromArgb(140, 160, 140)
        lblActiveSnapshot.AutoEllipsis = True
        lblActiveSnapshot.Text      = "(loading…)"
        Me.Controls.Add(lblActiveSnapshot)
        y += 26

        ' Round stats + open dir buttons
        btnShowRoundStats           = New Button()
        btnShowRoundStats.Text      = "Show Round Stats"
        btnShowRoundStats.Location  = New Point(LBL_X, y)
        btnShowRoundStats.Size      = New Size(140, 26)
        btnShowRoundStats.BackColor = Color.FromArgb(50, 70, 90)
        btnShowRoundStats.ForeColor = Color.FromArgb(220, 220, 220)
        btnShowRoundStats.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnShowRoundStats)

        btnOpenSnapshotsDir           = New Button()
        btnOpenSnapshotsDir.Text      = "Open Snapshots Folder"
        btnOpenSnapshotsDir.Location  = New Point(LBL_X + 150, y)
        btnOpenSnapshotsDir.Size      = New Size(160, 26)
        btnOpenSnapshotsDir.BackColor = Color.FromArgb(50, 70, 90)
        btnOpenSnapshotsDir.ForeColor = Color.FromArgb(220, 220, 220)
        btnOpenSnapshotsDir.FlatStyle = FlatStyle.Flat
        Me.Controls.Add(btnOpenSnapshotsDir)
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
