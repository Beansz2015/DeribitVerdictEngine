' tools/BacktestRunner/BacktestProgram.vb
' CLI entry point for the historical replay backtest synthesizer
' (docs/backtest-synthesizer-proposal.md, APPROVED 2026-07-30).
'
' Usage:
'   BacktestRunner fetch    --from yyyy-MM-dd --to yyyy-MM-dd
'   BacktestRunner replay   --from yyyy-MM-dd --to yyyy-MM-dd
'                            [--settings <settings.json>] [--out <path>]
'   BacktestRunner validate --from yyyy-MM-dd[Thh:mm] --to yyyy-MM-dd[Thh:mm]
'                            --live  <liveCsvPath>
'                            [--live2 <secondCsvPath>]
'                            [--settings <settings.json>] [--replay <existingSyntheticCsv>]
'                            [--report <markdownOut>]
'   BacktestRunner report   --csv <analysisLogCsv> [--settings <settings.json>]
'   BacktestRunner coverage --from yyyy-MM-dd --to yyyy-MM-dd
'                            [--gap-ms <ms>] [--out <path>] [--strict] [--verify-venue]
'
' The `coverage` verb reports raw-trade capture health (docs/trade-store-coverage-report
' -proposal.md): six classes per weekday UTC hour, S4 candle/funding completeness, and an
' optional S0 venue diff (--verify-venue, network). Read-only — never fetches (except S0)
' and never writes to the store. Reads analysis_log.csv / ws_health.log / capture_marker.log
' beside the store (CWD-relative, i.e. the repo root BacktestProgram already sets CWD to) if
' present, degrading gracefully when absent. Exit 1 with --strict when any DEFECT hour
' exists; without the flag, always exits 0 (interactive-safe).
'
' The `report` verb runs the SHIPPED analysis/AnalysisRunner pipeline over an
' arbitrary CSV file and writes the standard markdown report + summary CSV BESIDE
' the input. It exists so a pooled book (local + AWS-collector rows concatenated
' externally) can be reported on without going through the in-app status-bar link,
' which is hardwired to the engine's own working-directory analysis_log.csv. Zero
' changes to that in-app path; the analysis layer is host-agnostic by design and
' forward-bar OHLC comes from its existing DeribitOhlcFetcher path.
'
' Exit codes: 0 = success, 1 = bad args / fetch failed / no data.

Imports System.Globalization
Imports System.IO
Imports System.Threading.Tasks

Public Class BacktestProgram

    Public Shared Function Main(args As String()) As Integer
        Try
            Return RunAsync(args).GetAwaiter().GetResult()
        Catch ex As Exception
            Console.Error.WriteLine("[BacktestRunner] Fatal: " & ex.Message)
            Console.Error.WriteLine(ex.StackTrace)
            Return 1
        End Try
    End Function

    Private Shared Async Function RunAsync(args As String()) As Task(Of Integer)
        SetWorkingDirectoryToRepoRoot()

        If args.Length = 0 Then
            PrintUsage()
            Return 1
        End If

        Dim cmd As String = args(0).ToLowerInvariant()
        Dim fromUtc As DateTime = DateTime.MinValue
        Dim toUtc   As DateTime = DateTime.MinValue
        Dim settingsPath As String = "settings.json"
        Dim outPath As String = ""
        Dim livePath As String = ""
        Dim livePath2 As String = ""
        Dim replayPath As String = ""
        Dim reportPath As String = ""
        Dim csvPath    As String = ""
        Dim useFormingStub As Boolean = True
        Dim gapMs As Long = 300000L
        Dim strict As Boolean = False
        Dim verifyVenue As Boolean = False

        Dim i As Integer = 1
        While i < args.Length
            Select Case args(i).ToLowerInvariant()
                Case "--from"
                    i += 1
                    If i < args.Length Then fromUtc = ParseDate(args(i))
                Case "--to"
                    i += 1
                    If i < args.Length Then toUtc = ParseDate(args(i))
                Case "--settings"
                    i += 1
                    If i < args.Length Then settingsPath = args(i)
                Case "--out"
                    i += 1
                    If i < args.Length Then outPath = args(i)
                Case "--live"
                    i += 1
                    If i < args.Length Then livePath = args(i)
                Case "--live2"
                    i += 1
                    If i < args.Length Then livePath2 = args(i)
                Case "--replay"
                    i += 1
                    If i < args.Length Then replayPath = args(i)
                Case "--report"
                    i += 1
                    If i < args.Length Then reportPath = args(i)
                Case "--csv"
                    i += 1
                    If i < args.Length Then csvPath = args(i)
                Case "--closed-bars"
                    ' D3 evidence lane: closed bars only, no §7.1 forming stub.
                    useFormingStub = False
                Case "--gap-ms"
                    i += 1
                    If i < args.Length Then Long.TryParse(args(i), gapMs)
                Case "--strict"
                    strict = True
                Case "--verify-venue"
                    verifyVenue = True
            End Select
            i += 1
        End While

        ' `report` derives its own range from the CSV's own row timestamps, so --from/--to
        ' are meaningless there. Every other verb requires them.
        If cmd <> "report" Then
            If fromUtc = DateTime.MinValue OrElse toUtc = DateTime.MinValue Then
                Console.Error.WriteLine("[BacktestRunner] --from and --to are required (yyyy-MM-dd, UTC).")
                PrintUsage()
                Return 1
            End If
            If toUtc <= fromUtc Then
                Console.Error.WriteLine("[BacktestRunner] --to must be strictly greater than --from.")
                Return 1
            End If
        End If

        Select Case cmd
            Case "fetch"
                Console.WriteLine(String.Format("[BacktestRunner] Fetching {0:yyyy-MM-dd} → {1:yyyy-MM-dd} UTC ...",
                                                fromUtc, toUtc))
                Await HistoricalStore.BackfillAllAsync(fromUtc, toUtc)
                Console.WriteLine("[BacktestRunner] Fetch complete. Store: " & Path.GetFullPath(HistoricalStore.StoreDir))
                Return 0

            Case "replay"
                SettingsLoader.Initialise(settingsPath)
                Dim cfg = SettingsLoader.Current
                If String.IsNullOrEmpty(outPath) Then
                    Dim stamp As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                    outPath = "backtest_log_" & stamp & ".csv"
                End If

                Console.WriteLine(String.Format("[BacktestRunner] Replay {0:yyyy-MM-dd HH:mm} → {1:yyyy-MM-dd HH:mm} UTC",
                                                fromUtc, toUtc))
                Console.WriteLine("[BacktestRunner] Settings: " & Path.GetFullPath(settingsPath) &
                                  " (version " & cfg.Version & ")")
                Console.WriteLine("[BacktestRunner] Output:   " & Path.GetFullPath(outPath))
                Console.WriteLine("[BacktestRunner] Bar mode: " &
                                  If(useFormingStub, "forming stub (§7.1 live mirror)", "CLOSED BARS ONLY (D3 A/B arm)"))

                Dim summary = ReplayLoop.Run(cfg, fromUtc, toUtc, outPath, useFormingStub)

                Console.WriteLine("")
                Console.WriteLine("[BacktestRunner] === Replay summary ===")
                Console.WriteLine("[BacktestRunner] InstanceId: " & summary.InstanceId)
                Console.WriteLine("[BacktestRunner] Rows written: " & summary.RowsWritten)
                Console.WriteLine("[BacktestRunner] Rows per session:")
                For Each kv In summary.RowsPerSession.OrderBy(Function(k) k.Key)
                    Console.WriteLine(String.Format("[BacktestRunner]   {0,-10} {1}", kv.Key, kv.Value))
                Next
                Console.WriteLine("[BacktestRunner] Rows per verdict:")
                For Each kv In summary.RowsPerVerdict.OrderBy(Function(k) k.Key)
                    Console.WriteLine(String.Format("[BacktestRunner]   {0,-20} {1}", kv.Key, kv.Value))
                Next
                Console.WriteLine("[BacktestRunner] First 3 sample rows:")
                For Each s In summary.SampleRows
                    Console.WriteLine("[BacktestRunner]   " & s)
                Next
                Return If(summary.RowsWritten > 0, 0, 1)

            Case "validate"
                SettingsLoader.Initialise(settingsPath)
                Dim cfg = SettingsLoader.Current
                If String.IsNullOrEmpty(livePath) Then
                    Console.Error.WriteLine("[BacktestRunner] validate requires --live <liveCsvPath>.")
                    PrintUsage()
                    Return 1
                End If

                ' If no --replay provided, run the replay into a temp CSV first.
                Dim synCsv As String = replayPath
                If String.IsNullOrEmpty(synCsv) Then
                    Dim stampV As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
                    synCsv = "backtest_log_validate_" & stampV & ".csv"
                    Console.WriteLine("[BacktestRunner] Replay into " & synCsv)
                    Console.WriteLine(String.Format(
                        "[BacktestRunner] Replay {0:yyyy-MM-dd HH:mm} → {1:yyyy-MM-dd HH:mm} UTC (settings v{2})",
                        fromUtc, toUtc, cfg.Version))
                    Dim sumV = ReplayLoop.Run(cfg, fromUtc, toUtc, synCsv)
                    Console.WriteLine("[BacktestRunner] Replay wrote " & sumV.RowsWritten &
                                      " rows (InstanceId " & sumV.InstanceId & ")")
                Else
                    Console.WriteLine("[BacktestRunner] Reusing synthetic CSV: " & Path.GetFullPath(synCsv))
                End If

                Console.WriteLine("[BacktestRunner] Validating overlap ...")
                Console.WriteLine("[BacktestRunner]   live1   = " & Path.GetFullPath(livePath))
                If Not String.IsNullOrEmpty(livePath2) Then
                    Console.WriteLine("[BacktestRunner]   live2   = " & Path.GetFullPath(livePath2))
                End If

                Dim rep = OverlapValidator.Validate(cfg, synCsv, livePath, livePath2, fromUtc, toUtc)

                ' Console summary: keep it terse. Full markdown goes to --report or stdout.
                Console.WriteLine("")
                Console.WriteLine("[Validate] Joined pairs: " & rep.JoinedPairs)
                Console.WriteLine(String.Format(
                    "[Validate] Verdict agreement: {0}/{1} = {2:P2}",
                    rep.VerdictMatchOverall, rep.VerdictComparedOverall,
                    If(rep.VerdictComparedOverall > 0,
                       CDbl(rep.VerdictMatchOverall) / rep.VerdictComparedOverall, 0.0)))
                Console.WriteLine(String.Format(
                    "[Validate] Tier    agreement: {0}/{1} = {2:P2}",
                    rep.TierMatchOverall, rep.TierComparedOverall,
                    If(rep.TierComparedOverall > 0,
                       CDbl(rep.TierMatchOverall) / rep.TierComparedOverall, 0.0)))
                Dim md = OverlapValidator.BuildMarkdown(rep, cfg)
                If Not String.IsNullOrEmpty(reportPath) Then
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath)))
                    File.WriteAllText(reportPath, md)
                    Console.WriteLine("[Validate] Report: " & Path.GetFullPath(reportPath))
                Else
                    Console.WriteLine("")
                    Console.WriteLine(md)
                End If
                Return If(rep.JoinedPairs > 0, 0, 1)

            Case "report"
                SettingsLoader.Initialise(settingsPath)
                Dim cfg = SettingsLoader.Current
                If String.IsNullOrEmpty(csvPath) Then
                    Console.Error.WriteLine("[BacktestRunner] report requires --csv <analysisLogCsv>.")
                    PrintUsage()
                    Return 1
                End If
                Dim csvFull As String = Path.GetFullPath(csvPath)
                If Not File.Exists(csvFull) Then
                    Console.Error.WriteLine("[BacktestRunner] CSV not found: " & csvFull)
                    Return 1
                End If

                ' Report lands BESIDE the input, never in the repo root — a pooled snapshot
                ' usually lives in a scratch directory and its report belongs with it.
                Dim outDir As String = Path.GetDirectoryName(csvFull)
                Console.WriteLine("[BacktestRunner] Report over: " & csvFull)
                Console.WriteLine("[BacktestRunner] Settings:    " & Path.GetFullPath(settingsPath) &
                                  " (version " & cfg.Version & ")")
                Console.WriteLine("[BacktestRunner] Output dir:  " & outDir)
                Console.WriteLine("[BacktestRunner] Fetching forward-bar OHLC ...")

                Dim rpt As AnalysisReport = Await AnalysisRunner.Run(csvFull, outDir, cfg)

                Console.WriteLine("")
                Console.WriteLine("[Report] Rows loaded: " & rpt.TotalRows)
                If rpt.Populations.Count = 0 Then
                    Console.WriteLine("[Report] No populations — check the CSV schema / row timestamps.")
                Else
                    Console.WriteLine("[Report] Populations:")
                    For Each pop In rpt.Populations
                        Console.WriteLine(String.Format("[Report]   {0,-24} rows={1} excluded={2}",
                                                        pop.PopulationKey, pop.RowCount, pop.ExcludedRows))
                    Next
                End If
                If String.IsNullOrEmpty(rpt.MarkdownFilePath) Then
                    ' AnalysisRunner writes an error banner and leaves the path empty when the
                    ' forward-OHLC fetch fails — surface that as a non-zero exit, not a silent pass.
                    Console.Error.WriteLine("[Report] No markdown written: " & If(rpt.MarkdownText, "(no detail)"))
                    Return 1
                End If
                Console.WriteLine("[Report] Markdown:   " & rpt.MarkdownFilePath)
                If Not String.IsNullOrEmpty(rpt.SummaryCsvPath) Then
                    Console.WriteLine("[Report] Summary CSV: " & rpt.SummaryCsvPath)
                End If
                Return If(rpt.TotalRows > 0, 0, 1)

            Case "coverage"
                Dim opts As New CoverageOptions With {
                    .FromUtc = fromUtc, .ToUtc = toUtc, .GapMs = gapMs,
                    .Strict = strict, .VerifyVenue = verifyVenue
                }
                Dim storeDir As String = HistoricalStore.StoreDir
                Dim repoRoot As String = Directory.GetCurrentDirectory()
                Dim analysisLogPath As String = Path.Combine(repoRoot, "analysis_log.csv")
                Dim wsHealthPath As String = Path.Combine(repoRoot, "ws_health.log")
                Dim markerPath As String = Path.Combine(repoRoot, "capture_marker.log")

                Console.WriteLine(String.Format("[BacktestRunner] Coverage {0:yyyy-MM-dd} → {1:yyyy-MM-dd} UTC (gap-ms={2})",
                                                fromUtc, toUtc, gapMs))
                Dim covResult = CoverageReport.BuildResult(opts, storeDir, analysisLogPath, wsHealthPath, markerPath)

                If verifyVenue Then
                    Dim windowStartMs As Long = New DateTimeOffset(toUtc.AddHours(-24), TimeSpan.Zero).ToUnixTimeMilliseconds()
                    Dim windowEndMs As Long = New DateTimeOffset(toUtc, TimeSpan.Zero).ToUnixTimeMilliseconds()
                    Dim venueMissing = Await CoverageReport.RunVenueDiffAsync(storeDir, windowStartMs, windowEndMs)
                    If venueMissing IsNot Nothing Then
                        covResult.VenueRan = True
                        covResult.VenueMissingTrades = venueMissing
                        covResult.VenueCoveredFromUtc = toUtc.AddHours(-24)
                        covResult.VenueCoveredToUtc = toUtc
                    Else
                        Console.Error.WriteLine("[BacktestRunner] --verify-venue fetch failed — S0 not run.")
                    End If
                End If

                Console.WriteLine("")
                Console.Write(CoverageReport.BuildConsoleSummary(covResult))
                If Not String.IsNullOrEmpty(outPath) Then
                    Dim md As String = CoverageReport.BuildMarkdown(covResult)
                    Dim outDirCov As String = Path.GetDirectoryName(Path.GetFullPath(outPath))
                    If Not String.IsNullOrEmpty(outDirCov) Then Directory.CreateDirectory(outDirCov)
                    File.WriteAllText(outPath, md)
                    Console.WriteLine("[BacktestRunner] Markdown: " & Path.GetFullPath(outPath))
                End If

                If strict AndAlso covResult.CountByClass(HourClass.Defect) > 0 Then Return 1
                Return 0

            Case Else
                Console.Error.WriteLine("[BacktestRunner] Unknown subcommand: " & cmd)
                PrintUsage()
                Return 1
        End Select
    End Function

    Private Shared Sub PrintUsage()
        Console.Error.WriteLine("Usage:")
        Console.Error.WriteLine("  BacktestRunner fetch    --from yyyy-MM-dd --to yyyy-MM-dd")
        Console.Error.WriteLine("  BacktestRunner replay   --from yyyy-MM-dd --to yyyy-MM-dd " &
                                "[--settings <path>] [--out <path>] [--closed-bars]")
        Console.Error.WriteLine("  BacktestRunner validate --from yyyy-MM-dd[Thh:mm] --to yyyy-MM-dd[Thh:mm] " &
                                "--live <path> [--live2 <path>] [--replay <syntheticCsv>] " &
                                "[--report <mdOut>] [--settings <path>]")
        Console.Error.WriteLine("  BacktestRunner report   --csv <analysisLogCsv> [--settings <path>]")
        Console.Error.WriteLine("  BacktestRunner coverage --from yyyy-MM-dd --to yyyy-MM-dd " &
                                "[--gap-ms <ms>] [--out <path>] [--strict] [--verify-venue]")
    End Sub

    Private Shared Function ParseDate(s As String) As DateTime
        Dim d As DateTime
        If DateTime.TryParse(s, CultureInfo.InvariantCulture,
                             DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, d) Then
            Return d
        End If
        Return DateTime.MinValue
    End Function

    Private Shared Sub SetWorkingDirectoryToRepoRoot()
        Try
            Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
            For level As Integer = 1 To 8
                dir = Path.GetFullPath(Path.Combine(dir, ".."))
                If File.Exists(Path.Combine(dir, "DeribitVerdictEngine.sln")) Then
                    Directory.SetCurrentDirectory(dir)
                    Return
                End If
            Next
        Catch
        End Try
    End Sub

End Class
