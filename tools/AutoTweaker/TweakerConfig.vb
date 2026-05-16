' tools/AutoTweaker/TweakerConfig.vb
' POCO for tweaker_config.json.
' Read fresh on each AutoTweaker run — no caching.
' Host-agnostic: no System.Windows.Forms references.
'
' settings-snapshot-history-proposal.md additions:
'   - MaxKeysPerProposal — previously hardcoded to 3 in SettingsDiffApplier
'   - SnapshotStreakX / StreakWeight / StreakLengthClamp — composite-score knobs
'   - SnapshotsDir / ManifestPath — derived paths for the snapshot history
'
' auto-tweaker-fixed-window-proposal.md (v29):
'   - WindowMode — "fixed" | "sliding", default "fixed".
'   - MinTierEligibleRows is now Integer? (nullable). Null triggers auto-compute
'     as max(15, ceil(WindowSize × 0.5)). Resolve via EffectiveMinTier(windowSize).
'   - CooldownRows retained for backward-compat; treated as no-op when mode=fixed.

Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

Public Class TweakerConfig

    Public Const WindowModeFixed   As String = "fixed"
    Public Const WindowModeSliding As String = "sliding"

    <JsonPropertyName("version")>
    Public Property Version As Integer = 1

    <JsonPropertyName("window_mode")>
    Public Property WindowMode As String = WindowModeFixed

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

    ' Nullable: null → auto-compute via EffectiveMinTier(windowSize).
    <JsonPropertyName("min_tier_eligible_rows")>
    Public Property MinTierEligibleRows As Integer? = Nothing

    <JsonPropertyName("max_keys_per_proposal")>
    Public Property MaxKeysPerProposal As Integer = 3

    <JsonPropertyName("snapshot_streak_x")>
    Public Property SnapshotStreakX As Integer = 3

    <JsonPropertyName("streak_weight")>
    Public Property StreakWeight As Double = 1.5

    <JsonPropertyName("streak_length_clamp")>
    Public Property StreakLengthClamp As Integer = 20

    <JsonPropertyName("csv_path")>
    Public Property CsvPath As String = "bin/Debug/net8.0-windows/analysis_log.csv"

    <JsonPropertyName("settings_path")>
    Public Property SettingsPath As String = "settings.json"

    <JsonPropertyName("state_path")>
    Public Property StatePath As String = "tools/AutoTweaker/state.json"

    <JsonPropertyName("snapshots_dir")>
    Public Property SnapshotsDir As String = "settings_snapshots"

    <JsonPropertyName("manifest_path")>
    Public Property ManifestPath As String = "settings_snapshots/manifest.csv"

    <JsonPropertyName("dry_run_output_dir")>
    Public Property DryRunOutputDir As String = "tools/AutoTweaker/dry_run_payloads/"

    <JsonPropertyName("anthropic_model_alias")>
    Public Property AnthropicModelAlias As String = "latest-opus"

    ' Compute the active MinTier threshold for the supplied WindowSize.
    ' User-specified value (non-null) wins. Otherwise: max(15, ceil(size × 0.5)).
    Public Function EffectiveMinTier(windowSize As Integer) As Integer
        If MinTierEligibleRows.HasValue Then Return MinTierEligibleRows.Value
        Return ComputeDefaultMinTier(windowSize)
    End Function

    Public Shared Function ComputeDefaultMinTier(windowSize As Integer) As Integer
        Dim half As Integer = CInt(Math.Ceiling(windowSize * 0.5))
        Return Math.Max(15, half)
    End Function

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
