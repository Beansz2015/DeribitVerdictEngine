' tools/AutoTweaker/AutoTweakerProgram.vb
' Entry point for the AutoTweaker console application.
'
' Usage:
'   AutoTweaker.exe                              — normal run (reads tweaker_config.json)
'   AutoTweaker.exe --config <path>              — explicit config path
'   AutoTweaker.exe --apply-manual <diff.json>   — apply a manually-obtained diff
'
' Exit codes:
'   0 — clean run (no action needed, or action taken)
'   1 — error (API failure, settings parse error, invalid diff)
'   2 — ineligible (cooldown / session not aligned / insufficient tiers)
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.IO

Public Class AutoTweakerProgram

    Public Shared Function Main(args As String()) As Integer
        Try
            Return RunAsync(args).GetAwaiter().GetResult()
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Fatal error: " & ex.Message)
            Return 1
        End Try
    End Function

    Private Shared Async Function RunAsync(args As String()) As System.Threading.Tasks.Task(Of Integer)
        ' ── Set working directory to repo root ────────────────────────────────
        ' Walk up from the exe directory until we find DeribitVerdictEngine.sln.
        ' This is robust against Debug/Release/RID variations in the output path.
        Try
            Dim dir As String = AppDomain.CurrentDomain.BaseDirectory
            Dim found As Boolean = False
            For level As Integer = 1 To 8
                dir = Path.GetFullPath(Path.Combine(dir, ".."))
                If File.Exists(Path.Combine(dir, "DeribitVerdictEngine.sln")) Then
                    Directory.SetCurrentDirectory(dir)
                    Console.WriteLine("[AutoTweaker] Working directory: " & dir)
                    found = True
                    Exit For
                End If
            Next
            If Not found Then
                Console.Error.WriteLine("[AutoTweaker] Warning: could not locate repo root (DeribitVerdictEngine.sln not found).")
            End If
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Warning: could not set working directory: " & ex.Message)
        End Try

        ' ── Parse arguments ───────────────────────────────────────────────────
        Dim configPath    As String = "tweaker_config.json"
        Dim manualDiffPath As String = Nothing

        Dim i As Integer = 0
        Do While i < args.Length
            Select Case args(i).ToLower()
                Case "--config"
                    i += 1
                    If i < args.Length Then configPath = args(i)
                Case "--apply-manual"
                    i += 1
                    If i < args.Length Then manualDiffPath = args(i)
            End Select
            i += 1
        Loop

        ' ── Manual-apply path ─────────────────────────────────────────────────
        If Not String.IsNullOrEmpty(manualDiffPath) Then
            Return ApplyManual(manualDiffPath, configPath)
        End If

        ' ── Normal run ────────────────────────────────────────────────────────
        Dim config = TweakerConfig.Load(configPath)
        Dim state  = TweakerState.Load(config.StatePath)

        Console.WriteLine(String.Format("[AutoTweaker] Starting run. DryRun={0}, AutoCommit={1}",
                                        config.DryRunEnabled, config.AutoCommitEnabled))

        Dim exitCode = Await AutoTweakerCore.RunAsync(config, state, config.StatePath)
        Console.WriteLine("[AutoTweaker] Done. Exit code: " & exitCode)
        Return exitCode
    End Function

    ' Apply a diff from a manually-obtained JSON file (e.g. from dry-run workflow).
    Private Shared Function ApplyManual(diffPath As String, configPath As String) As Integer
        If Not File.Exists(diffPath) Then
            Console.Error.WriteLine("[AutoTweaker] Diff file not found: " & diffPath)
            Return 1
        End If

        Dim config = TweakerConfig.Load(configPath)

        Dim responseText As String
        Try
            responseText = File.ReadAllText(diffPath)
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Cannot read diff file: " & ex.Message)
            Return 1
        End Try

        Dim settingsJson As String
        Try
            settingsJson = File.ReadAllText(config.SettingsPath)
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Cannot read settings.json: " & ex.Message)
            Return 1
        End Try

        Dim parseResult = SettingsDiffApplier.ParseDiff(responseText)
        Dim diffItems   = parseResult.Items
        Dim reasoning   = parseResult.Reasoning
        If diffItems.Count = 0 Then
            Console.Error.WriteLine("[AutoTweaker] No diff items found in: " & diffPath)
            Return 1
        End If

        Dim validation = SettingsDiffApplier.Validate(diffItems, settingsJson)
        If Not validation.IsValid Then
            Console.Error.WriteLine("[AutoTweaker] Diff rejected: " & validation.ErrorReason)
            Return 1
        End If

        Try
            Dim newVer = SettingsDiffApplier.Apply(diffItems, config.SettingsPath, reasoning)
            Console.WriteLine(String.Format("[AutoTweaker] Manual apply succeeded → settings.json v{0}", newVer))

            ' Update state
            Dim state = TweakerState.Load(config.StatePath)
            state.LastRunOutcome      = "APPLIED"
            state.LastRunAtIso        = DateTime.UtcNow.ToString("o")
            state.LastProposalSummary = "Manual apply from: " & diffPath
            TweakerState.Save(config.StatePath, state)
            Return 0
        Catch ex As Exception
            Console.Error.WriteLine("[AutoTweaker] Apply failed: " & ex.Message)
            Return 1
        End Try
    End Function

End Class
