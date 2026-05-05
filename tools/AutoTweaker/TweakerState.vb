' tools/AutoTweaker/TweakerState.vb
' Persistent state across AutoTweaker runs (state.json, gitignored).
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

Public Class PickedCellEntry
    <JsonPropertyName("ts")>
    Public Property Ts As String = ""

    <JsonPropertyName("tier")>
    Public Property Tier As String = ""

    <JsonPropertyName("window_min")>
    Public Property WindowMin As Integer = 0

    <JsonPropertyName("atr_threshold")>
    Public Property AtrThreshold As Double = 0.0
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
        Dim opts As New JsonSerializerOptions With {.WriteIndented = True}
        Dim dir = IO.Path.GetDirectoryName(path)
        If Not String.IsNullOrEmpty(dir) Then IO.Directory.CreateDirectory(dir)
        File.WriteAllText(path, JsonSerializer.Serialize(state, opts))
    End Sub

End Class
