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
            End Select
            i += 1
        End While

        If fromUtc = DateTime.MinValue OrElse toUtc = DateTime.MinValue Then
            Console.Error.WriteLine("[BacktestRunner] --from and --to are required (yyyy-MM-dd, UTC).")
            PrintUsage()
            Return 1
        End If
        If toUtc <= fromUtc Then
            Console.Error.WriteLine("[BacktestRunner] --to must be strictly greater than --from.")
            Return 1
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

                Dim summary = ReplayLoop.Run(cfg, fromUtc, toUtc, outPath)

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
                    Console.WriteLine("[BacktestRunner] Replay {0:yyyy-MM-dd HH:mm} → {1:yyyy-MM-dd HH:mm} UTC into " & synCsv)
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
                                "[--settings <path>] [--out <path>]")
        Console.Error.WriteLine("  BacktestRunner validate --from yyyy-MM-dd[Thh:mm] --to yyyy-MM-dd[Thh:mm] " &
                                "--live <path> [--live2 <path>] [--replay <syntheticCsv>] " &
                                "[--report <mdOut>] [--settings <path>]")
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
