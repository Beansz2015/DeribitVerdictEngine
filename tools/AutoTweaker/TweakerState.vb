' tools/AutoTweaker/TweakerState.vb
' Persistent state across AutoTweaker runs (state.json, gitignored).
' Host-agnostic: no System.Windows.Forms references.
'
' settings-snapshot-history-proposal.md additions:
'   - CurrentBelowThresholdStreak / ActiveSnapshotFilename / ActiveSnapshotCreatedIso
'     track the running BELOW_THRESHOLD streak and an associated ACTIVE snapshot file.
'   - RoundHistory captures the last 50 evaluable rounds for round-stats display and
'     conditions extraction.

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text.Json
Imports System.Text.Json.Serialization

Public Class PickedCellEntry
    <JsonPropertyName("ts")>
    Public Property Ts As String = ""

    <JsonPropertyName("tier")>
    Public Property Tier As String = ""

    <JsonPropertyName("window_min")>
    Public Property WindowMin As Integer = 0

    ' [placed-target migration, M3] The pick space collapsed to (window); new entries
    ' carry no threshold. Nullable + WhenWritingNull so rows written before the migration
    ' still deserialise with their value intact while new rows omit the key entirely —
    ' parse-tolerant in both directions, no state.json rotation.
    <JsonPropertyName("atr_threshold")>
    <JsonIgnore(Condition:=JsonIgnoreCondition.WhenWritingNull)>
    Public Property AtrThreshold As Double? = Nothing
End Class

' One evaluable round summary — written after every BELOW_THRESHOLD / APPLIED /
' PROPOSED / DRY_RUN_WRITTEN outcome. INELIGIBLE / ERROR do NOT produce a summary.
Public Class RoundSummary

    <JsonPropertyName("round_iso")>
    Public Property RoundIso As String = ""

    <JsonPropertyName("outcome")>
    Public Property Outcome As String = ""

    <JsonPropertyName("window_start_row")>
    Public Property WindowStartRow As Integer = 0

    <JsonPropertyName("window_end_row")>
    Public Property WindowEndRow As Integer = 0

    <JsonPropertyName("aggregate_failure_rate_pct")>
    Public Property AggregateFailureRatePct As Double = 0.0

    ' Compact JSON of picked cells: {"STRONG_LONG":{"window":10,"thr":0.5,"n":42,"fails":12}, ...}
    <JsonPropertyName("picked_cells_json")>
    Public Property PickedCellsJson As String = ""

    ' Populated for APPLIED / PROPOSED / revert rounds — short human-readable summary
    ' of the diff or revert that fired.
    <JsonPropertyName("diff_summary")>
    Public Property DiffSummary As String = ""

    ' Populated for APPLIED / PROPOSED / revert rounds — Claude reasoning excerpt.
    <JsonPropertyName("reasoning")>
    Public Property Reasoning As String = ""

End Class

Public Class TweakerState

    <JsonPropertyName("last_run_at_iso")>
    Public Property LastRunAtIso As String = ""

    <JsonPropertyName("last_run_csv_row_count")>
    Public Property LastRunCsvRowCount As Integer = 0

    ' "PROPOSED" | "APPLIED" | "DRY_RUN_WRITTEN" | "BELOW_THRESHOLD" | "INELIGIBLE" | "ERROR"
    <JsonPropertyName("last_run_outcome")>
    Public Property LastRunOutcome As String = ""

    <JsonPropertyName("last_proposal_summary")>
    Public Property LastProposalSummary As String = ""

    <JsonPropertyName("last_pending_diff_path")>
    Public Property LastPendingDiffPath As String = ""

    <JsonPropertyName("last_error_message")>
    Public Property LastErrorMessage As String = ""

    <JsonPropertyName("picked_cell_history")>
    Public Property PickedCellHistory As New List(Of PickedCellEntry)()

    ' ── snapshot history additions ─────────────────────────────────────────
    <JsonPropertyName("current_below_threshold_streak")>
    Public Property CurrentBelowThresholdStreak As Integer = 0

    <JsonPropertyName("active_snapshot_filename")>
    Public Property ActiveSnapshotFilename As String = ""

    <JsonPropertyName("active_snapshot_created_iso")>
    Public Property ActiveSnapshotCreatedIso As String = ""

    ' Last BELOW_THRESHOLD round timestamp — used to populate FinalisedIso when an
    ' active snapshot is finalised by a change-triggering outcome.
    <JsonPropertyName("last_successful_round_iso")>
    Public Property LastSuccessfulRoundIso As String = ""

    ' Fixed-window mode (v29): highest 1-based "row count consumed" already
    ' evaluated by a completed (or skipped) round. -1 = uninitialised; on first
    ' v29 run it is set to currentRowCount so historical sliding-era data is
    ' preserved in the CSV but not re-evaluated under fixed mode.
    <JsonPropertyName("last_evaluated_row_index")>
    Public Property LastEvaluatedRowIndex As Integer = -1

    ' v36 Phase-2a — the (session × resolution) population this state's
    ' LastEvaluatedRowIndex was advanced against. Under a population filter the
    ' index counts the FILTERED sequence, so a key change (first introduction, or
    ' the trader switching populations) must re-seed the index to filtered.Count.
    ' "" = never filtered; "none" = filter explicitly absent.
    <JsonPropertyName("population_filter_key")>
    Public Property PopulationFilterKey As String = ""

    ' Capped at RoundHistoryCap rounds; older entries dropped on Save.
    <JsonPropertyName("round_history")>
    Public Property RoundHistory As New List(Of RoundSummary)()

    Public Const RoundHistoryCap As Integer = 1000

    Public Shared Function Load(path As String) As TweakerState
        If Not File.Exists(path) Then Return New TweakerState()
        Try
            Dim json As String = File.ReadAllText(path)
            Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
            Dim s = JsonSerializer.Deserialize(Of TweakerState)(json, opts)
            Return If(s, New TweakerState())
        Catch
            Return New TweakerState()
        End Try
    End Function

    Public Shared Sub Save(path As String, state As TweakerState)
        ' Cap round history before persisting.
        If state.RoundHistory IsNot Nothing AndAlso state.RoundHistory.Count > RoundHistoryCap Then
            state.RoundHistory = state.RoundHistory.
                Skip(state.RoundHistory.Count - RoundHistoryCap).ToList()
        End If

        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        Dim dir = IO.Path.GetDirectoryName(path)
        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)

        ' Atomic write: persist to .tmp, then rename. NTFS rename is atomic at the
        ' filesystem level — a mid-write crash either leaves the original file
        ' intact (rename never happened) or the new file in place (rename completed).
        ' Avoids the failure mode where File.WriteAllText is killed mid-write and
        ' leaves a partial state.json that triggers a defaults reset on Load.
        Dim tmpPath As String = path & ".tmp"
        Try
            File.WriteAllText(tmpPath, JsonSerializer.Serialize(state, opts))
            If File.Exists(path) Then
                File.Replace(tmpPath, path, Nothing)
            Else
                File.Move(tmpPath, path)
            End If
        Catch
            Try : File.Delete(tmpPath) : Catch : End Try
            Throw
        End Try
    End Sub

End Class
