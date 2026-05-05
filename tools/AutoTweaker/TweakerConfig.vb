' tools/AutoTweaker/TweakerConfig.vb
' POCO for tweaker_config.json.
' Read fresh on each AutoTweaker run — no caching.
' Host-agnostic: no System.Windows.Forms references.

Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

Public Class TweakerConfig

    <JsonPropertyName("version")>
    Public Property Version As Integer = 1

    <JsonPropertyName("auto_commit_enabled")>
    Public Property AutoCommitEnabled As Boolean = False

    <JsonPropertyName("dry_run_enabled")>
    Public Property DryRunEnabled As Boolean = True

    <JsonPropertyName("window_size_verdicts")>
    Public Property WindowSizeVerdicts As Integer = 120

    <JsonPropertyName("failure_rate_threshold_pct")>
    Public Property FailureRateThresholdPct As Double = 40.0

    <JsonPropertyName("cooldown_rows")>
    Public Property CooldownRows As Integer = 10

    <JsonPropertyName("min_tier_eligible_rows")>
    Public Property MinTierEligibleRows As Integer = 60

    <JsonPropertyName("csv_path")>
    Public Property CsvPath As String = "bin/Debug/net8.0-windows/analysis_log.csv"

    <JsonPropertyName("settings_path")>
    Public Property SettingsPath As String = "settings.json"

    <JsonPropertyName("state_path")>
    Public Property StatePath As String = "tools/AutoTweaker/state.json"

    <JsonPropertyName("dry_run_output_dir")>
    Public Property DryRunOutputDir As String = "tools/AutoTweaker/dry_run_payloads/"

    <JsonPropertyName("anthropic_model_alias")>
    Public Property AnthropicModelAlias As String = "latest-opus"

    ' Load from disk, or return defaults if the file doesn't exist.
    Public Shared Function Load(path As String) As TweakerConfig
        If Not File.Exists(path) Then Return New TweakerConfig()
        Try
            Dim json As String = File.ReadAllText(path)
            Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            Dim cfg = JsonSerializer.Deserialize(Of TweakerConfig)(json, opts)
            Return If(cfg, New TweakerConfig())
        Catch
            Return New TweakerConfig()
        End Try
    End Function

    ' Write to disk with indented JSON.
    Public Shared Sub Save(filePath As String, cfg As TweakerConfig)
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        Dim dir = IO.Path.GetDirectoryName(filePath)
        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
        IO.File.WriteAllText(filePath, JsonSerializer.Serialize(cfg, opts))
    End Sub

End Class
