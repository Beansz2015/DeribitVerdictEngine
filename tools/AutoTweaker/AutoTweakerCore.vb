' tools/AutoTweaker/AutoTweakerCore.vb
' Implements the 7-step auto-tweaker pipeline (spec section 3).
' Returns an exit code: 0 = clean run, 1 = error, 2 = ineligible.
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Threading.Tasks

Public Class AutoTweakerCore

    Public Shared Async Function RunAsync(config As TweakerConfig,
                                           state As TweakerState,
                                           statePath As String) As Task(Of Integer)
        ' ── 1. Cooldown check ────────────────────────────────────────────────
        Dim csvLines As String()
        Try
            csvLines = File.ReadAllLines(config.CsvPath)
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Cannot read CSV: " & ex.Message)
            state.LastRunOutcome     = "ERROR"
            state.LastErrorMessage   = "CSV read failed: " & ex.Message
            state.LastRunAtIso       = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End Try

        Dim currentRowCount As Integer = Math.Max(0, csvLines.Length - 1)  ' subtract header
        Dim rowsSinceLast    As Integer = currentRowCount - state.LastRunCsvRowCount

        If state.LastRunCsvRowCount > 0 AndAlso rowsSinceLast < config.CooldownRows Then
            Console.WriteLine(String.Format("[AutoTweaker] INELIGIBLE — cooldown: {0}/{1} rows.",
                                            rowsSinceLast, config.CooldownRows))
            state.LastRunOutcome = "INELIGIBLE"
            state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 2
        End If

        ' ── 2. Load settings and session boundaries ──────────────────────────
        Dim settingsJson As String
        Dim sessionStartHours As New List(Of Integer)()
        Try
            settingsJson = File.ReadAllText(config.SettingsPath)
            Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            Dim settings = JsonSerializer.Deserialize(Of EngineSettings)(settingsJson, opts)
            If settings?.SessionVolume?.Sessions IsNot Nothing Then
                For Each s In settings.SessionVolume.Sessions
                    sessionStartHours.Add(s.StartHour)
                Next
            End If
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Cannot read settings.json: " & ex.Message)
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "settings.json read failed: " & ex.Message
            state.LastRunAtIso     = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End Try
        If sessionStartHours.Count = 0 Then
            sessionStartHours.Add(0)   ' default: midnight UTC
            sessionStartHours.Add(13)  ' default: NY open UTC
        End If

        ' ── 3. Build session-aligned window ──────────────────────────────────
        Dim allRows = ForwardReturnJoiner.Load(config.CsvPath, sessionStartHours)
        If allRows.Count < config.WindowSizeVerdicts Then
            Console.WriteLine(String.Format("[AutoTweaker] INELIGIBLE — only {0} rows, need {1}.",
                                            allRows.Count, config.WindowSizeVerdicts))
            state.LastRunOutcome = "INELIGIBLE"
            state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 2
        End If

        ' Walk back from end of allRows to find window_size_verdicts rows in same session
        Dim windowRows As New List(Of CsvRow)()
        Dim sessionSet As New HashSet(Of Integer)(sessionStartHours)
        Dim endIdx As Integer = allRows.Count - 1

        For i As Integer = endIdx To Math.Max(0, endIdx - config.WindowSizeVerdicts * 2) Step -1
            If windowRows.Count >= config.WindowSizeVerdicts Then Exit For
            If windowRows.Count > 0 Then
                ' Check no session boundary between allRows(i) and the most-recently-added row
                Dim newerRow = windowRows(windowRows.Count - 1)
                If CrossesSessionBoundary(allRows(i).Timestamp, newerRow.Timestamp, sessionSet) Then
                    Console.WriteLine(String.Format(
                        "[AutoTweaker] INELIGIBLE — session boundary at row {0}. Window has {1}/{2} rows.",
                        i, windowRows.Count, config.WindowSizeVerdicts))
                    state.LastRunOutcome = "INELIGIBLE"
                    state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                    TweakerState.Save(statePath, state)
                    Return 2
                End If
            End If
            windowRows.Add(allRows(i))
        Next

        If windowRows.Count < config.WindowSizeVerdicts Then
            Console.WriteLine(String.Format("[AutoTweaker] INELIGIBLE — session-aligned window only {0}/{1} rows.",
                                            windowRows.Count, config.WindowSizeVerdicts))
            state.LastRunOutcome = "INELIGIBLE"
            state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 2
        End If

        ' windowRows was built newest-first; reverse for chronological order
        windowRows.Reverse()

        ' ── 4. Count tier-eligible rows ───────────────────────────────────────
        Dim tierEligible As Integer = windowRows.Where(Function(r)
            Dim v = r.Verdict.Trim().ToUpper()
            Return v = "STRONG LONG" OrElse v = "LONG" OrElse
                   v = "STRONG SHORT" OrElse v = "SHORT"
        End Function).Count()

        If tierEligible < config.MinTierEligibleRows Then
            Console.WriteLine(String.Format(
                "[AutoTweaker] INELIGIBLE — only {0}/{1} tier-eligible rows.",
                tierEligible, config.MinTierEligibleRows))
            state.LastRunOutcome = "INELIGIBLE"
            state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 2
        End If

        ' ── 5. Compute failure-rate matrix and pick recommended cells ─────────
        Dim failureCells = FailureRateMatrix.Compute(windowRows)
        Dim recommended  = failureCells.Where(Function(c) c.IsRecommended).ToList()

        ' Append picked cells to history and write to picked_cell_history.csv
        Dim pickedCsvPath = Path.Combine(
            If(String.IsNullOrEmpty(Path.GetDirectoryName(config.CsvPath)),
               ".", Path.GetDirectoryName(config.CsvPath)),
            "picked_cell_history.csv")
        For Each cell In recommended
            state.PickedCellHistory.Add(New PickedCellEntry With {
                .Ts           = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                .Tier         = cell.VerdictTier,
                .WindowMin    = cell.WindowMin,
                .AtrThreshold = cell.AtrThreshold
            })
            FailureRateMatrix.AppendPickedCell(pickedCsvPath, cell.VerdictTier,
                                               cell.WindowMin, cell.AtrThreshold)
        Next

        ' ── 6. Compute aggregate failure rate at recommended cells ─────────────
        Dim totalN As Integer = 0
        Dim totalF As Integer = 0
        For Each cell In recommended
            totalN += cell.SampleSize
            totalF += cell.Failures
        Next

        Dim aggregateRate As Double = If(totalN > 0, CDbl(totalF) / totalN * 100, 0)

        If aggregateRate < config.FailureRateThresholdPct Then
            Console.WriteLine(String.Format(
                "[AutoTweaker] BELOW_THRESHOLD — aggregate failure rate {0:F1}% < {1:F1}% threshold. No tweak needed.",
                aggregateRate, config.FailureRateThresholdPct))
            state.LastRunOutcome      = "BELOW_THRESHOLD"
            state.LastRunAtIso        = DateTime.UtcNow.ToString("o")
            state.LastRunCsvRowCount  = currentRowCount
            TweakerState.Save(statePath, state)
            Return 0
        End If

        Console.WriteLine(String.Format(
            "[AutoTweaker] Failure rate {0:F1}% >= {1:F1}% threshold — building prompt...",
            aggregateRate, config.FailureRateThresholdPct))

        ' Determine session name for trigger line
        Dim sessionName As String = "unknown session"
        Try
            Dim hour = windowRows.Last().Timestamp.Hour
            Dim opts2 As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            Dim settings2 = JsonSerializer.Deserialize(Of EngineSettings)(settingsJson, opts2)
            If settings2?.SessionVolume?.Sessions IsNot Nothing Then
                For Each s In settings2.SessionVolume.Sessions
                    If hour >= s.StartHour AndAlso hour < s.EndHour Then
                        sessionName = s.Name : Exit For
                    End If
                Next
            End If
        Catch
        End Try

        Dim trigger As String = String.Format(
            "aggregate failure rate {0:F1}% > threshold {1:F1}% over window {2} rows ({3} session)",
            aggregateRate, config.FailureRateThresholdPct, config.WindowSizeVerdicts, sessionName)

        ' ── 7. Build prompt ───────────────────────────────────────────────────
        Dim promptResult = PromptBuilder.Build(
            settingsJson, windowRows, failureCells, state.PickedCellHistory, trigger)
        Dim systemMsg As String = promptResult.SystemMsg
        Dim userMsg   As String = promptResult.UserMsg

        ' ── 8. Branch: dry-run or live ────────────────────────────────────────
        If config.DryRunEnabled Then
            Dim dryPath = ClaudeApiClient.WriteDryRunFile(
                systemMsg, userMsg, "claude-opus-latest", trigger, config.DryRunOutputDir)
            Console.WriteLine("[AutoTweaker] DRY_RUN_WRITTEN → " & dryPath)
            state.LastRunOutcome     = "DRY_RUN_WRITTEN"
            state.LastRunAtIso       = DateTime.UtcNow.ToString("o")
            state.LastRunCsvRowCount = currentRowCount
            state.LastProposalSummary = "Dry-run file: " & dryPath
            TweakerState.Save(statePath, state)
            Return 0
        End If

        ' Live API call
        Dim apiKey As String = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        If String.IsNullOrEmpty(apiKey) Then
            Console.Error.WriteLine("[AutoTweaker] ERROR — ANTHROPIC_API_KEY env var not set.")
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "ANTHROPIC_API_KEY not set."
            state.LastRunAtIso     = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End If

        Dim model As String
        Try
            model = Await ClaudeApiClient.ResolveLatestOpusModelAsync(apiKey)
        Catch ex As Exception
            model = "claude-opus-latest"
        End Try
        Console.WriteLine("[AutoTweaker] Using model: " & model)

        Dim responseText As String
        Try
            responseText = Await ClaudeApiClient.CallAsync(apiKey, model, systemMsg, userMsg)
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] API call failed: " & ex.Message)
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "API call failed: " & ex.Message
            state.LastRunAtIso     = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End Try

        ' ── 9. Parse and validate diff ────────────────────────────────────────
        Dim parseResult = SettingsDiffApplier.ParseDiff(responseText)
        Dim diffItems   = parseResult.Items
        Dim reasoning   = parseResult.Reasoning
        If diffItems.Count = 0 Then
            Console.WriteLine("[AutoTweaker] Claude returned empty diff — no changes needed.")
            state.LastRunOutcome     = "BELOW_THRESHOLD"
            state.LastProposalSummary = "Claude returned empty diff: " & reasoning
            state.LastRunAtIso        = DateTime.UtcNow.ToString("o")
            state.LastRunCsvRowCount  = currentRowCount
            TweakerState.Save(statePath, state)
            Return 0
        End If

        Dim validation = SettingsDiffApplier.Validate(diffItems, settingsJson)
        If Not validation.IsValid Then
            Console.Error.WriteLine("[AutoTweaker] Diff rejected: " & validation.ErrorReason)
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "Diff rejected: " & validation.ErrorReason
            state.LastRunAtIso     = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End If

        ' ── 10. Branch: auto-commit or propose ───────────────────────────────
        Dim diffSummary As String = String.Join("; ",
            diffItems.Select(Function(d) String.Format("{0}: {1} → {2}",
                                                        d.Path,
                                                        d.OldValue.GetRawText(),
                                                        d.NewValue.GetRawText())))

        If config.AutoCommitEnabled Then
            Try
                Dim newVer = SettingsDiffApplier.Apply(diffItems, config.SettingsPath, reasoning)
                Console.WriteLine(String.Format("[AutoTweaker] APPLIED settings.json → v{0}", newVer))
                state.LastRunOutcome     = "APPLIED"
                state.LastProposalSummary = diffSummary
            Catch ex As Exception
                Console.Error.WriteLine("[AutoTweaker] Apply failed: " & ex.Message)
                state.LastRunOutcome   = "ERROR"
                state.LastErrorMessage = "Apply failed: " & ex.Message
                state.LastRunAtIso     = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 1
            End Try
        Else
            ' Write proposed diff to file
            Dim proposedDir = "tools/AutoTweaker/proposed_diffs/"
            Directory.CreateDirectory(proposedDir)
            Dim ts        As String = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss")
            Dim diffPath  As String = Path.Combine(proposedDir, ts & ".json")
            File.WriteAllText(diffPath,
                System.Text.Json.JsonSerializer.Serialize(
                    New With {.reasoning = reasoning, .diff = diffItems},
                    New JsonSerializerOptions With {.WriteIndented = True}))
            Console.WriteLine("[AutoTweaker] PROPOSED → " & diffPath)
            state.LastRunOutcome      = "PROPOSED"
            state.LastPendingDiffPath = diffPath
            state.LastProposalSummary = diffSummary
        End If

        state.LastRunAtIso       = DateTime.UtcNow.ToString("o")
        state.LastRunCsvRowCount = currentRowCount
        TweakerState.Save(statePath, state)
        Return 0
    End Function

    ' Check if any session-start hour falls inside the interval (t1, t2).
    Private Shared Function CrossesSessionBoundary(t1 As DateTime, t2 As DateTime,
                                                    sessionStarts As HashSet(Of Integer)) As Boolean
        If t1 = DateTime.MinValue OrElse t2 = DateTime.MinValue Then Return True
        Dim h1 = t1.Hour
        Dim h2 = t2.Hour
        If h1 = h2 Then Return False
        Dim h = h1
        Do While h <> h2
            h = (h + 1) Mod 24
            If sessionStarts.Contains(h) Then Return True
        Loop
        Return False
    End Function

End Class
