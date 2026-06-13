' tools/AutoTweaker/AutoTweakerCore.vb
' Implements the 7-step auto-tweaker pipeline (spec section 3).
' Returns an exit code: 0 = clean run, 1 = error, 2 = ineligible.
'
' v2 (failure-definition-v2-proposal.md):
'   - ForwardWindowJoiner replaces ForwardReturnJoiner (no forward-price joining).
'   - OHLC fetch via DeribitOhlcFetcher runs AFTER eligibility checks pass.
'   - ForwardBars populated from OHLC map before FailureRateMatrix.Compute.
'
' settings-snapshot-history-proposal.md additions:
'   - Round-summary capture for every evaluable round (BELOW_THRESHOLD / APPLIED /
'     PROPOSED / DRY_RUN_WRITTEN).
'   - Streak tracking with SnapshotManager Create / AccumulateConditions / Finalise.
'   - REVERT action support — when Claude returns action="revert", apply via
'     SettingsDiffApplier.ApplyRevert (bypasses key-cap but still validates content).
'   - Manifest + current conditions vector handed to PromptBuilder.
'
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

        Dim currentRowCount As Integer = Math.Max(0, csvLines.Length - 1)
        Dim isFixedMode As Boolean = String.Equals(config.WindowMode,
            TweakerConfig.WindowModeFixed, StringComparison.OrdinalIgnoreCase)

        If isFixedMode Then
            ' cooldown_rows is a no-op in fixed mode — natural cooldown is
            ' "wait for the next disjoint batch to fill".
            Console.WriteLine("[AutoTweaker] INFO — window_mode=fixed; cooldown_rows ignored.")
        Else
            Dim rowsSinceLast As Integer = currentRowCount - state.LastRunCsvRowCount
            If state.LastRunCsvRowCount > 0 AndAlso rowsSinceLast < config.CooldownRows Then
                Console.WriteLine(String.Format("[AutoTweaker] INELIGIBLE — cooldown: {0}/{1} rows.",
                                                rowsSinceLast, config.CooldownRows))
                state.LastRunOutcome = "INELIGIBLE"
                state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 2
            End If
        End If

        ' ── 2. Load settings and session boundaries ──────────────────────────
        Dim settingsJson As String
        Dim sessionStartHours As New List(Of Integer)()
        Dim tightThresholdBps As Double = 0.0
        Dim wideThresholdBps  As Double = 0.0
        ' v35 de-confound: live shared min-tradeable-move floor + engine target
        ' multiplier, read from settings.json (fall back to the AnalysisConstants
        ' mirror if absent). Passed to FailureRateMatrix.Compute so the tweaker
        ' optimises the gate-filtered book. min_tradeable_move_pct is OFF the tweaker's
        ' tunable surface (PromptBuilder HARD CONSTRAINT 11) — read-only here.
        Dim minTradeableMovePct As Double = AnalysisConstants.FavBarAbsFloorPct
        Dim atrTargetMult       As Double = AnalysisConstants.EngineTargetAtrMultiplier
        Try
            settingsJson = File.ReadAllText(config.SettingsPath)
            Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            Dim settings = JsonSerializer.Deserialize(Of EngineSettings)(settingsJson, opts)
            If settings?.SessionVolume?.Sessions IsNot Nothing Then
                For Each s In settings.SessionVolume.Sessions
                    sessionStartHours.Add(s.StartHour)
                Next
            End If
            If settings?.Indicators?.Spread IsNot Nothing Then
                tightThresholdBps = settings.Indicators.Spread.TightThresholdBps
                wideThresholdBps  = settings.Indicators.Spread.WideThresholdBps
            End If
            If settings?.Scoring IsNot Nothing Then
                minTradeableMovePct = settings.Scoring.MinTradeableMovePct
                atrTargetMult       = settings.Scoring.AtrTargetMultiplier
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
            sessionStartHours.Add(0)
            sessionStartHours.Add(13)
        End If

        ' ── 3. Build window ──────────────────────────────────────────────────
        Dim allRows = ForwardWindowJoiner.Load(config.CsvPath)
        Dim sessionSet As New HashSet(Of Integer)(sessionStartHours)
        Dim windowRows As New List(Of CsvRow)()
        Dim windowStartRow As Integer = 0
        Dim windowEndRow   As Integer = 0
        Dim minTierThreshold As Integer = config.EffectiveMinTier(config.WindowSizeVerdicts)

        If isFixedMode Then
            ' Fixed (non-overlapping) windows — disjoint round semantics.
            ' First-run init: LastEvaluatedRowIndex == -1 ⇒ preserve historical CSV
            ' rows by setting the index to currentRowCount so the next round only
            ' covers data accumulated after v29 ships.
            If state.LastEvaluatedRowIndex < 0 Then
                Console.WriteLine(String.Format(
                    "[AutoTweaker] INFO — first v29 run — LastEvaluatedRowIndex initialised to currentRowCount={0}. " &
                    "Historical rows preserved but not re-evaluated.", currentRowCount))
                state.LastEvaluatedRowIndex = currentRowCount
                state.LastRunOutcome = "INELIGIBLE"
                state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 2
            End If

            ' CSV shrink guard: LastEvaluatedRowIndex is an absolute row index, but
            ' the CSV can shrink below it — schema-mismatch rotation (has happened
            ' twice), the UI Reset Log link, or a deliberate data reset. Without
            ' this guard, currentRowCount < LastEvaluatedRowIndex makes the
            ' window-full check below compute a negative new-row count, which is
            ' always < WindowSize, so every run exits INELIGIBLE forever and the
            ' first-fire silently never happens. Re-seed to the new row count so
            ' evaluation resumes on the fresh data. This is a reset, not a round —
            ' do NOT tick the BELOW_THRESHOLD streak and write no RoundSummary.
            If currentRowCount < state.LastEvaluatedRowIndex Then
                Console.WriteLine(String.Format(
                    "[AutoTweaker] WARNING — CSV shrank below LastEvaluatedRowIndex " &
                    "(rows={0} < index={1}); re-seeding index to {0}. Likely a log " &
                    "rotation, Reset Log, or data reset. Streak not ticked.",
                    currentRowCount, state.LastEvaluatedRowIndex))
                state.LastEvaluatedRowIndex = currentRowCount
                state.LastRunOutcome     = "INELIGIBLE"
                state.LastRunAtIso       = DateTime.UtcNow.ToString("o")
                state.LastRunCsvRowCount = currentRowCount
                TweakerState.Save(statePath, state)
                Return 2
            End If

            If currentRowCount - state.LastEvaluatedRowIndex < config.WindowSizeVerdicts Then
                Console.WriteLine(String.Format(
                    "[AutoTweaker] INELIGIBLE — fixed-mode window not full: {0}/{1} new rows since row {2}.",
                    currentRowCount - state.LastEvaluatedRowIndex,
                    config.WindowSizeVerdicts,
                    state.LastEvaluatedRowIndex))
                state.LastRunOutcome = "INELIGIBLE"
                state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 2
            End If

            ' Slice rows [LastEvaluatedRowIndex .. LastEvaluatedRowIndex + WindowSize - 1].
            Dim startIdx As Integer = state.LastEvaluatedRowIndex
            Dim endIdxFx As Integer = startIdx + config.WindowSizeVerdicts - 1
            If endIdxFx >= allRows.Count Then endIdxFx = allRows.Count - 1
            For idx As Integer = startIdx To endIdxFx
                windowRows.Add(allRows(idx))
            Next

            If windowRows.Count < config.WindowSizeVerdicts Then
                Console.WriteLine(String.Format(
                    "[AutoTweaker] INELIGIBLE — fixed-mode window only {0}/{1} rows after slice.",
                    windowRows.Count, config.WindowSizeVerdicts))
                state.LastRunOutcome = "INELIGIBLE"
                state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 2
            End If

            windowStartRow = windowRows.First().Index
            windowEndRow   = windowRows.Last().Index

            ' Session-boundary check within the disjoint window.
            For i As Integer = 1 To windowRows.Count - 1
                If CrossesSessionBoundary(windowRows(i - 1).Timestamp,
                                            windowRows(i).Timestamp, sessionSet) Then
                    Console.WriteLine(String.Format(
                        "[AutoTweaker] SKIPPED_SESSION_BOUNDARY — fixed-mode round rows {0}..{1} crosses session at row {2}.",
                        windowStartRow, windowEndRow, windowRows(i).Index))
                    Dim skipIso As String = DateTime.UtcNow.ToString("o")
                    Dim skipSummary As New RoundSummary() With {
                        .RoundIso                = skipIso,
                        .Outcome                 = "SKIPPED_SESSION_BOUNDARY",
                        .WindowStartRow          = windowStartRow,
                        .WindowEndRow            = windowEndRow,
                        .AggregateFailureRatePct = 0.0,
                        .PickedCellsJson         = "{}"
                    }
                    state.LastEvaluatedRowIndex += config.WindowSizeVerdicts
                    state.LastRunOutcome        = "SKIPPED_SESSION_BOUNDARY"
                    state.LastRunAtIso          = skipIso
                    state.LastRunCsvRowCount    = currentRowCount
                    ' Streak counter does NOT tick — skipped rounds are not failures.
                    state.RoundHistory.Add(skipSummary)
                    TweakerState.Save(statePath, state)
                    Return 2
                End If
            Next

            ' ── 4. Tier-eligible check (fixed mode) ─────────────────────────────
            Dim tierEligibleFx As Integer = windowRows.Where(Function(r)
                Dim v = r.Verdict.Trim().ToUpper()
                Return v = "STRONG LONG" OrElse v = "LONG" OrElse
                       v = "STRONG SHORT" OrElse v = "SHORT"
            End Function).Count()

            If tierEligibleFx < minTierThreshold Then
                Console.WriteLine(String.Format(
                    "[AutoTweaker] SKIPPED_INSUFFICIENT_TIER — only {0}/{1} tier-eligible rows in window rows {2}..{3}.",
                    tierEligibleFx, minTierThreshold, windowStartRow, windowEndRow))
                Dim skipIso As String = DateTime.UtcNow.ToString("o")
                Dim skipSummary As New RoundSummary() With {
                    .RoundIso                = skipIso,
                    .Outcome                 = "SKIPPED_INSUFFICIENT_TIER",
                    .WindowStartRow          = windowStartRow,
                    .WindowEndRow            = windowEndRow,
                    .AggregateFailureRatePct = 0.0,
                    .PickedCellsJson         = "{}"
                }
                state.LastEvaluatedRowIndex += config.WindowSizeVerdicts
                state.LastRunOutcome        = "SKIPPED_INSUFFICIENT_TIER"
                state.LastRunAtIso          = skipIso
                state.LastRunCsvRowCount    = currentRowCount
                ' Streak counter does NOT tick — skipped rounds are not failures.
                state.RoundHistory.Add(skipSummary)
                TweakerState.Save(statePath, state)
                Return 2
            End If
        Else
            ' Sliding (legacy) — session-aligned reverse walk. Deprecated; retained
            ' for backward-compat experimentation.
            If allRows.Count < config.WindowSizeVerdicts Then
                Console.WriteLine(String.Format("[AutoTweaker] INELIGIBLE — only {0} rows, need {1}.",
                                                allRows.Count, config.WindowSizeVerdicts))
                state.LastRunOutcome = "INELIGIBLE"
                state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 2
            End If

            Dim endIdx As Integer = allRows.Count - 1

            For i As Integer = endIdx To Math.Max(0, endIdx - config.WindowSizeVerdicts * 2) Step -1
                If windowRows.Count >= config.WindowSizeVerdicts Then Exit For
                If windowRows.Count > 0 Then
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

            windowRows.Reverse()

            windowStartRow = windowRows.First().Index
            windowEndRow   = windowRows.Last().Index

            ' ── 4. Count tier-eligible rows (sliding) ───────────────────────────
            Dim tierEligible As Integer = windowRows.Where(Function(r)
                Dim v = r.Verdict.Trim().ToUpper()
                Return v = "STRONG LONG" OrElse v = "LONG" OrElse
                       v = "STRONG SHORT" OrElse v = "SHORT"
            End Function).Count()

            If tierEligible < minTierThreshold Then
                Console.WriteLine(String.Format(
                    "[AutoTweaker] INELIGIBLE — only {0}/{1} tier-eligible rows.",
                    tierEligible, minTierThreshold))
                state.LastRunOutcome = "INELIGIBLE"
                state.LastRunAtIso   = DateTime.UtcNow.ToString("o")
                TweakerState.Save(statePath, state)
                Return 2
            End If
        End If

        ' ── 5. Deribit OHLC bulk fetch ────────────────────────────────────────
        Dim validWindowRows = windowRows.Where(Function(r) r.Timestamp > DateTime.MinValue).ToList()
        If validWindowRows.Count = 0 Then
            Console.Error.WriteLine("[AutoTweaker] ERROR — no valid timestamps in window rows.")
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "No valid row timestamps."
            state.LastRunAtIso     = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End If
        Dim fetchStart As DateTime = validWindowRows.Min(Function(r) r.Timestamp)
        Dim fetchEnd   As DateTime = validWindowRows.Max(Function(r) r.Timestamp).AddMinutes(16)

        Console.WriteLine("[AutoTweaker] Fetching Deribit OHLC for window rows…")
        Dim ohlcMap = Await DeribitOhlcFetcher.FetchOhlcRange(fetchStart, fetchEnd)
        If ohlcMap Is Nothing Then
            Console.Error.WriteLine("[AutoTweaker] ERROR — Deribit OHLC fetch failed.")
            state.LastRunOutcome      = "ERROR"
            state.LastErrorMessage    = "Deribit OHLC fetch failed — auto-tweaker cannot evaluate without forward price data."
            state.LastProposalSummary = "Deribit OHLC fetch failed — auto-tweaker cannot evaluate without forward price data."
            state.LastRunAtIso        = DateTime.UtcNow.ToString("o")
            TweakerState.Save(statePath, state)
            Return 1
        End If

        ForwardWindowJoiner.PopulateForwardBars(windowRows, ohlcMap)

        ' ── 6. Compute failure-rate matrix and pick recommended cells ─────────
        Dim atrEx As Integer = 0, structStop As Integer = 0, atrFb As Integer = 0
        Dim belowMin As Integer = 0
        ' v35 de-confound: floor the favourable barrier + EXCLUDE gate-killed rows so the
        ' tweaker optimises only the book the engine will actually trade.
        Dim failureCells = FailureRateMatrix.Compute(windowRows, atrEx, structStop, atrFb, belowMin,
                                                     minTradeableMovePct, atrTargetMult)
        If belowMin > 0 Then
            Console.WriteLine(String.Format(
                "[AutoTweaker] {0} window row(s) EXCLUDED below min-tradeable-move floor (gate-killed; not failures).", belowMin))
        End If
        Dim recommended  = failureCells.Where(Function(c) c.IsRecommended).ToList()

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
            FailureRateMatrix.AppendPickedCell(pickedCsvPath,
                cell.VerdictTier, cell.WindowMin, cell.AtrThreshold,
                cell.FailureRate, cell.SampleSize, cell.CiLow, cell.CiHigh)
        Next

        ' ── 7. Aggregate failure rate at recommended cells ─────────────────────
        Dim totalN As Integer = 0
        Dim totalF As Integer = 0
        For Each cell In recommended
            totalN += cell.SampleSize
            totalF += cell.Failures
        Next
        Dim aggregateRatePct As Double = If(totalN > 0, CDbl(totalF) / totalN * 100, 0)

        ' Build a compact picked-cells JSON for round history persistence.
        Dim pickedCellsJson As String = BuildPickedCellsJson(recommended)

        ' Round summary skeleton — populated per branch below.
        Dim roundIso As String = DateTime.UtcNow.ToString("o")
        Dim summary As New RoundSummary() With {
            .RoundIso                = roundIso,
            .WindowStartRow          = windowStartRow,
            .WindowEndRow            = windowEndRow,
            .AggregateFailureRatePct = aggregateRatePct,
            .PickedCellsJson         = pickedCellsJson
        }

        ' ── BELOW_THRESHOLD branch ─────────────────────────────────────────────
        If aggregateRatePct < config.FailureRateThresholdPct Then
            Console.WriteLine(String.Format(
                "[AutoTweaker] BELOW_THRESHOLD — aggregate failure rate {0:F1}% < {1:F1}% threshold. No tweak needed.",
                aggregateRatePct, config.FailureRateThresholdPct))
            summary.Outcome = "BELOW_THRESHOLD"
            state.LastRunOutcome      = "BELOW_THRESHOLD"
            state.LastRunAtIso        = roundIso
            state.LastRunCsvRowCount  = currentRowCount
            state.LastSuccessfulRoundIso = roundIso
            If isFixedMode Then state.LastEvaluatedRowIndex += config.WindowSizeVerdicts

            ' Streak tracking + snapshot management
            state.CurrentBelowThresholdStreak += 1
            HandleStreakAdvance(state, config)
            state.RoundHistory.Add(summary)
            TweakerState.Save(statePath, state)
            Return 0
        End If

        Console.WriteLine(String.Format(
            "[AutoTweaker] Failure rate {0:F1}% >= {1:F1}% threshold — building prompt...",
            aggregateRatePct, config.FailureRateThresholdPct))

        ' Determine session name for trigger line.
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
            aggregateRatePct, config.FailureRateThresholdPct, config.WindowSizeVerdicts, sessionName)

        ' ── Build conditions vector + manifest snippet for this round ─────────
        Dim singleRound As New List(Of RoundSummary) From {summary}
        Dim conditions = ConditionsExtractor.Extract(
            config.CsvPath, singleRound, tightThresholdBps, wideThresholdBps)
        Dim manifestActiveRows As String = SnapshotManager.GetActiveRowsCsv(config.ManifestPath)

        ' ── 8. Build prompt ───────────────────────────────────────────────────
        Dim promptResult = PromptBuilder.Build(
            settingsJson, windowRows, failureCells, state.PickedCellHistory, trigger,
            manifestActiveRows, conditions, config.MaxKeysPerProposal)
        Dim systemMsg As String = promptResult.SystemMsg
        Dim userMsg   As String = promptResult.UserMsg

        ' ── 9. Branch: dry-run or live ────────────────────────────────────────
        If config.DryRunEnabled Then
            Dim dryPath = ClaudeApiClient.WriteDryRunFile(
                systemMsg, userMsg, "claude-opus-latest", trigger, config.DryRunOutputDir)
            Console.WriteLine("[AutoTweaker] DRY_RUN_WRITTEN → " & dryPath)

            summary.Outcome     = "DRY_RUN_WRITTEN"
            summary.DiffSummary = "Dry-run payload: " & Path.GetFileName(dryPath)
            state.LastRunOutcome      = "DRY_RUN_WRITTEN"
            state.LastRunAtIso        = roundIso
            state.LastRunCsvRowCount  = currentRowCount
            state.LastProposalSummary = "Dry-run file: " & dryPath
            If isFixedMode Then state.LastEvaluatedRowIndex += config.WindowSizeVerdicts
            HandleStreakInterrupt(state, config, conditions, singleRound)
            state.RoundHistory.Add(summary)
            TweakerState.Save(statePath, state)
            Return 0
        End If

        ' Live API call.
        Dim apiKey As String = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
        If String.IsNullOrEmpty(apiKey) Then
            Console.Error.WriteLine("[AutoTweaker] ERROR — ANTHROPIC_API_KEY env var not set.")
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "ANTHROPIC_API_KEY not set."
            state.LastRunAtIso     = roundIso
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
            state.LastRunAtIso     = roundIso
            TweakerState.Save(statePath, state)
            Return 1
        End Try

        ' ── 10. Parse response ────────────────────────────────────────────────
        Dim parseResult = SettingsDiffApplier.ParseDiff(responseText)
        Dim diffItems   = parseResult.Items
        Dim reasoning   = parseResult.Reasoning
        Dim action      = parseResult.Action
        Dim revertTarget = parseResult.RevertTarget
        summary.Reasoning = reasoning

        ' ── 11a. REVERT branch ───────────────────────────────────────────────
        If action = "revert" AndAlso Not String.IsNullOrEmpty(revertTarget) Then
            Dim snapshotPath As String = Path.Combine(config.SnapshotsDir, revertTarget)
            Dim manifestRows = SnapshotManager.LoadAll(config.ManifestPath)
            Dim targetRow = manifestRows.FirstOrDefault(
                Function(r) r.Filename = revertTarget AndAlso r.Status = "ACTIVE")
            If targetRow Is Nothing OrElse Not File.Exists(snapshotPath) Then
                Dim reason As String = String.Format(
                    "Revert target '{0}' not found in ACTIVE manifest or file missing.", revertTarget)
                Console.Error.WriteLine("[AutoTweaker] " & reason)
                state.LastRunOutcome   = "ERROR"
                state.LastErrorMessage = reason
                state.LastRunAtIso     = roundIso
                TweakerState.Save(statePath, state)
                Return 1
            End If

            If config.AutoCommitEnabled Then
                Try
                    Dim newVer = SettingsDiffApplier.ApplyRevert(snapshotPath, config.SettingsPath, reasoning)
                    Console.WriteLine(String.Format(
                        "[AutoTweaker] APPLIED revert → settings.json v{0} (from {1})", newVer, revertTarget))
                    summary.Outcome     = "APPLIED"
                    summary.DiffSummary = "REVERT to " & revertTarget
                    state.LastRunOutcome      = "APPLIED"
                    state.LastProposalSummary = "Reverted to " & revertTarget
                Catch ex As Exception
                    Console.Error.WriteLine("[AutoTweaker] Revert failed: " & ex.Message)
                    state.LastRunOutcome   = "ERROR"
                    state.LastErrorMessage = "Revert failed: " & ex.Message
                    state.LastRunAtIso     = roundIso
                    TweakerState.Save(statePath, state)
                    Return 1
                End Try
            Else
                Dim proposedDir = "tools/AutoTweaker/proposed_diffs/"
                Directory.CreateDirectory(proposedDir)
                Dim ts        As String = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss")
                Dim diffPath  As String = Path.Combine(proposedDir, ts & "_revert.json")
                File.WriteAllText(diffPath,
                    JsonSerializer.Serialize(
                        New With {.action = "revert",
                                  .revert_target = revertTarget,
                                  .reasoning = reasoning},
                        New JsonSerializerOptions With {.WriteIndented = True}))
                Console.WriteLine("[AutoTweaker] PROPOSED revert → " & diffPath)
                summary.Outcome     = "PROPOSED"
                summary.DiffSummary = "REVERT proposal to " & revertTarget
                state.LastRunOutcome      = "PROPOSED"
                state.LastPendingDiffPath = diffPath
                state.LastProposalSummary = "Revert proposal: " & revertTarget
            End If

            state.LastRunAtIso       = roundIso
            state.LastRunCsvRowCount = currentRowCount
            If isFixedMode Then state.LastEvaluatedRowIndex += config.WindowSizeVerdicts
            HandleStreakInterrupt(state, config, conditions, singleRound)
            state.RoundHistory.Add(summary)
            TweakerState.Save(statePath, state)
            Return 0
        End If

        ' ── 11b. TWEAK branch ────────────────────────────────────────────────
        If diffItems.Count = 0 Then
            Console.WriteLine("[AutoTweaker] Claude returned empty diff — no changes needed.")
            summary.Outcome     = "BELOW_THRESHOLD"
            summary.DiffSummary = "Empty diff from Claude"
            state.LastRunOutcome      = "BELOW_THRESHOLD"
            state.LastProposalSummary = "Claude returned empty diff: " & reasoning
            state.LastRunAtIso        = roundIso
            state.LastRunCsvRowCount  = currentRowCount
            state.LastSuccessfulRoundIso = roundIso
            If isFixedMode Then state.LastEvaluatedRowIndex += config.WindowSizeVerdicts

            ' Treat as a successful round for streak purposes — accuracy is fine.
            state.CurrentBelowThresholdStreak += 1
            HandleStreakAdvance(state, config)
            state.RoundHistory.Add(summary)
            TweakerState.Save(statePath, state)
            Return 0
        End If

        Dim validation = SettingsDiffApplier.Validate(diffItems, settingsJson, config.MaxKeysPerProposal)
        If Not validation.IsValid Then
            Console.Error.WriteLine("[AutoTweaker] Diff rejected: " & validation.ErrorReason)
            state.LastRunOutcome   = "ERROR"
            state.LastErrorMessage = "Diff rejected: " & validation.ErrorReason
            state.LastRunAtIso     = roundIso
            TweakerState.Save(statePath, state)
            Return 1
        End If

        ' ── 12. Apply or propose ─────────────────────────────────────────────
        Dim diffSummary As String = String.Join("; ",
            diffItems.Select(Function(d) String.Format("{0}: {1} → {2}",
                                                        d.Path,
                                                        d.OldValue.GetRawText(),
                                                        d.NewValue.GetRawText())))

        If config.AutoCommitEnabled Then
            Try
                Dim newVer = SettingsDiffApplier.Apply(diffItems, config.SettingsPath, reasoning)
                Console.WriteLine(String.Format("[AutoTweaker] APPLIED settings.json → v{0}", newVer))
                summary.Outcome     = "APPLIED"
                summary.DiffSummary = diffSummary
                state.LastRunOutcome      = "APPLIED"
                state.LastProposalSummary = diffSummary
            Catch ex As Exception
                Console.Error.WriteLine("[AutoTweaker] Apply failed: " & ex.Message)
                state.LastRunOutcome   = "ERROR"
                state.LastErrorMessage = "Apply failed: " & ex.Message
                state.LastRunAtIso     = roundIso
                TweakerState.Save(statePath, state)
                Return 1
            End Try
        Else
            Dim proposedDir = "tools/AutoTweaker/proposed_diffs/"
            Directory.CreateDirectory(proposedDir)
            Dim ts        As String = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss")
            Dim diffPath  As String = Path.Combine(proposedDir, ts & ".json")
            File.WriteAllText(diffPath,
                JsonSerializer.Serialize(
                    New With {.reasoning = reasoning, .diff = diffItems},
                    New JsonSerializerOptions With {.WriteIndented = True}))
            Console.WriteLine("[AutoTweaker] PROPOSED → " & diffPath)
            summary.Outcome     = "PROPOSED"
            summary.DiffSummary = diffSummary
            state.LastRunOutcome      = "PROPOSED"
            state.LastPendingDiffPath = diffPath
            state.LastProposalSummary = diffSummary
        End If

        state.LastRunAtIso       = roundIso
        state.LastRunCsvRowCount = currentRowCount
        If isFixedMode Then state.LastEvaluatedRowIndex += config.WindowSizeVerdicts
        HandleStreakInterrupt(state, config, conditions, singleRound)
        state.RoundHistory.Add(summary)
        TweakerState.Save(statePath, state)
        Return 0
    End Function

    ' Increment-side snapshot management — fires when this round was BELOW_THRESHOLD.
    Private Shared Sub HandleStreakAdvance(state As TweakerState, config As TweakerConfig)
        Dim streakRounds As List(Of RoundSummary) =
            SuccessfulRoundsForCurrentStreak(state)

        If state.CurrentBelowThresholdStreak = config.SnapshotStreakX AndAlso
           String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then
            ' Create the snapshot. The just-appended round is added AFTER this routine,
            ' so synthesise a streak that includes this round.
            Dim synthetic As New List(Of RoundSummary)(streakRounds)
            ' The latest BELOW_THRESHOLD summary hasn't been added yet — caller adds it
            ' after we return. We still pass the count = StreakX rounds. Use a virtual
            ' round that matches the spec: streak length = X.
            Dim virtualSummary As New RoundSummary() With {
                .RoundIso = state.LastSuccessfulRoundIso,
                .Outcome  = "BELOW_THRESHOLD",
                .WindowStartRow = 0,
                .WindowEndRow   = 0,
                .AggregateFailureRatePct = 0.0,
                .PickedCellsJson = ""
            }
            ' Conditions extraction uses the WindowStart/End rows from prior rounds.
            ' For the just-completed (not-yet-appended) round we don't have rows here;
            ' extraction will simply skip rows for that one. The Finalise / Accumulate
            ' path re-runs extraction once the round IS in history, which corrects this.
            Dim conditions = ConditionsExtractor.Extract(
                config.CsvPath, streakRounds, 0.0, 0.0)
            SnapshotManager.Create(config.SettingsPath,
                                    config.SnapshotsDir,
                                    config.ManifestPath,
                                    state,
                                    streakRounds,
                                    conditions)
        ElseIf state.CurrentBelowThresholdStreak > config.SnapshotStreakX AndAlso
                Not String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then
            ' Streak still growing past X — refresh conditions across the full streak.
            Dim conditions = ConditionsExtractor.Extract(
                config.CsvPath, streakRounds, 0.0, 0.0)
            SnapshotManager.AccumulateConditions(config.ManifestPath, state,
                                                  streakRounds, conditions)
        End If
    End Sub

    ' Change-triggering outcome — finalise any active snapshot, reset the streak.
    Private Shared Sub HandleStreakInterrupt(state As TweakerState,
                                              config As TweakerConfig,
                                              currentRoundConditions As ConditionsVector,
                                              singleRound As List(Of RoundSummary))
        If Not String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then
            Dim streakRounds = SuccessfulRoundsForCurrentStreak(state)
            ' Re-compute conditions across the full successful-streak history (NOT this round).
            Dim conditions = ConditionsExtractor.Extract(
                config.CsvPath, streakRounds, 0.0, 0.0)
            Dim finalisedIso As String = If(streakRounds.Count > 0,
                streakRounds.Last().RoundIso, state.LastSuccessfulRoundIso)
            SnapshotManager.Finalise(config.ManifestPath,
                                      config.SnapshotsDir,
                                      state,
                                      streakRounds,
                                      conditions,
                                      finalisedIso,
                                      config.StreakWeight,
                                      config.StreakLengthClamp)
        End If
        state.CurrentBelowThresholdStreak = 0
    End Sub

    ' Recent BELOW_THRESHOLD rounds in the active streak — bounded by the
    ' current streak counter (capped to RoundHistory length).
    Private Shared Function SuccessfulRoundsForCurrentStreak(state As TweakerState) As List(Of RoundSummary)
        Dim n As Integer = Math.Min(state.CurrentBelowThresholdStreak, state.RoundHistory.Count)
        If n <= 0 Then Return New List(Of RoundSummary)()
        Return state.RoundHistory.
            Skip(state.RoundHistory.Count - n).
            Where(Function(r) r.Outcome = "BELOW_THRESHOLD").
            ToList()
    End Function

    Private Shared Function BuildPickedCellsJson(cells As List(Of FailureCellResult)) As String
        If cells Is Nothing OrElse cells.Count = 0 Then Return "{}"
        Dim obj As New Dictionary(Of String, Object)()
        For Each c In cells
            obj(c.VerdictTier) = New With {
                .window = c.WindowMin,
                .thr    = c.AtrThreshold,
                .n      = c.SampleSize,
                .fails  = c.Failures
            }
        Next
        Return JsonSerializer.Serialize(obj)
    End Function

    ' Returns True if any session-start hour falls inside the half-open interval
    ' (earlier, later] of the two timestamps — date-aware: a span of a full day
    ' or more always crosses, and the hour walk advances real DateTimes so two
    ' timestamps in the same clock-hour on different days are no longer treated as
    ' boundary-clean. Keep identical to TweakSettingsForm.FormCrossesSessionBoundary.
    Private Shared Function CrossesSessionBoundary(t1 As DateTime, t2 As DateTime,
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

End Class
