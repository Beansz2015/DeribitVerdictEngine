' tools/AutoTweaker/SnapshotManager.vb
' Manages settings snapshots created when the auto-tweaker accumulates
' settings-snapshot-history-proposal.md §3c–§3h:
'   - Create() — copies settings.json to settings_snapshots/ and appends
'     an ACTIVE manifest row when the success streak hits StreakX.
'   - AccumulateConditions() — re-extracts the conditions vector across the
'     full streak window and updates the manifest row in place.
'   - Finalise() — when a change-triggering outcome interrupts the streak,
'     populates FinalisedIso (last successful round's timestamp) and
'     StreakLength, then runs bucket-rotation (§3h).
'
' Manifest schema is in settings-snapshot-history-proposal.md §3a.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

Public Class SnapshotManager

    Public Const ManifestHeader As String =
        "Filename,CreatedIso,FinalisedIso,StreakLength,AvgFailureRatePct," &
        "RegimeMix,AtrScaleAvg,AtrScaleMin,AtrScaleMax,FundingMin,FundingMax," &
        "NetPriceMovePct,VolumeRatioAvg,VerdictTierMix," &
        "VWAPDevAvg,VWAPDevMin,VWAPDevMax,SpreadRegimeMix,OFIImbalanceMix," &
        "ConditionBucket,Status,RotationReason"

    ' One parsed manifest row (used by rotation logic).
    Public Class ManifestRow
        Public Property Filename            As String = ""
        Public Property CreatedIso          As String = ""
        Public Property FinalisedIso        As String = ""
        Public Property StreakLength        As Integer = 0
        Public Property AvgFailureRatePct   As Double = 0.0
        Public Property RegimeMix           As String = ""
        Public Property AtrScaleAvg         As Double = 0.0
        Public Property AtrScaleMin         As Double = 0.0
        Public Property AtrScaleMax         As Double = 0.0
        Public Property FundingMin          As Double = 0.0
        Public Property FundingMax          As Double = 0.0
        Public Property NetPriceMovePct     As Double = 0.0
        Public Property VolumeRatioAvg      As Double = 0.0
        Public Property VerdictTierMix      As String = ""
        Public Property VWAPDevAvg          As Double = 0.0
        Public Property VWAPDevMin          As Double = 0.0
        Public Property VWAPDevMax          As Double = 0.0
        Public Property SpreadRegimeMix     As String = ""
        Public Property OFIImbalanceMix     As String = ""
        Public Property ConditionBucket     As String = ""
        Public Property Status              As String = ""   ' ACTIVE | ROTATED
        Public Property RotationReason      As String = ""
    End Class

    ' ── Public API ─────────────────────────────────────────────────────────────

    ' Called when the streak first hits StreakX and there's no active snapshot.
    Public Shared Sub Create(settingsPath As String,
                              snapshotsDir As String,
                              manifestPath As String,
                              state As TweakerState,
                              streakRounds As List(Of RoundSummary),
                              conditions As ConditionsVector)
        Directory.CreateDirectory(snapshotsDir)

        Dim ts       As String = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
        Dim filename As String = "settings_snapshot_" & ts & ".json"
        Dim destPath As String = Path.Combine(snapshotsDir, filename)

        Try
            File.Copy(settingsPath, destPath, overwrite:=True)
        Catch ex As Exception
            Console.Error.WriteLine("[SnapshotManager] Failed to copy settings to snapshot: " & ex.Message)
            Return
        End Try

        Dim row As New ManifestRow() With {
            .Filename          = filename,
            .CreatedIso        = DateTime.UtcNow.ToString("o"),
            .FinalisedIso      = "",
            .StreakLength      = streakRounds.Count,
            .AvgFailureRatePct = conditions.AvgFailureRatePct,
            .RegimeMix         = conditions.RegimeMix,
            .AtrScaleAvg       = conditions.AtrScaleAvg,
            .AtrScaleMin       = conditions.AtrScaleMin,
            .AtrScaleMax       = conditions.AtrScaleMax,
            .FundingMin        = conditions.FundingMin,
            .FundingMax        = conditions.FundingMax,
            .NetPriceMovePct   = conditions.NetPriceMovePct,
            .VolumeRatioAvg    = conditions.VolumeRatioAvg,
            .VerdictTierMix    = conditions.VerdictTierMix,
            .VWAPDevAvg        = conditions.VWAPDevAvg,
            .VWAPDevMin        = conditions.VWAPDevMin,
            .VWAPDevMax        = conditions.VWAPDevMax,
            .SpreadRegimeMix   = conditions.SpreadRegimeMix,
            .OFIImbalanceMix   = conditions.OFIImbalanceMix,
            .ConditionBucket   = conditions.ConditionBucket,
            .Status            = "ACTIVE",
            .RotationReason    = ""
        }

        AppendRow(manifestPath, row)

        state.ActiveSnapshotFilename   = filename
        state.ActiveSnapshotCreatedIso = row.CreatedIso

        Console.WriteLine(String.Format(
            "[SnapshotManager] Created snapshot {0} after streak hit X={1}",
            filename, streakRounds.Count))
    End Sub

    ' Re-extract conditions and write back to the ACTIVE row.
    Public Shared Sub AccumulateConditions(manifestPath As String,
                                            state As TweakerState,
                                            streakRounds As List(Of RoundSummary),
                                            conditions As ConditionsVector)
        If String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then Return
        Dim rows = LoadAll(manifestPath)
        Dim target = rows.FirstOrDefault(Function(r) r.Filename = state.ActiveSnapshotFilename)
        If target Is Nothing Then Return

        target.StreakLength      = streakRounds.Count
        target.AvgFailureRatePct = conditions.AvgFailureRatePct
        target.RegimeMix         = conditions.RegimeMix
        target.AtrScaleAvg       = conditions.AtrScaleAvg
        target.AtrScaleMin       = conditions.AtrScaleMin
        target.AtrScaleMax       = conditions.AtrScaleMax
        target.FundingMin        = conditions.FundingMin
        target.FundingMax        = conditions.FundingMax
        target.NetPriceMovePct   = conditions.NetPriceMovePct
        target.VolumeRatioAvg    = conditions.VolumeRatioAvg
        target.VerdictTierMix    = conditions.VerdictTierMix
        target.VWAPDevAvg        = conditions.VWAPDevAvg
        target.VWAPDevMin        = conditions.VWAPDevMin
        target.VWAPDevMax        = conditions.VWAPDevMax
        target.SpreadRegimeMix   = conditions.SpreadRegimeMix
        target.OFIImbalanceMix   = conditions.OFIImbalanceMix
        target.ConditionBucket   = conditions.ConditionBucket

        WriteAll(manifestPath, rows)
    End Sub

    ' Finalise an active snapshot when a change-triggering outcome lands.
    ' finalisedIso = timestamp of the LAST SUCCESSFUL round of the streak (not
    ' the current interrupting round). Per spec §3g and the critical constraint.
    Public Shared Sub Finalise(manifestPath As String,
                                snapshotsDir As String,
                                state As TweakerState,
                                streakRounds As List(Of RoundSummary),
                                conditions As ConditionsVector,
                                finalisedIso As String,
                                streakWeight As Double,
                                streakLengthClamp As Integer)
        If String.IsNullOrEmpty(state.ActiveSnapshotFilename) Then Return
        Dim rows = LoadAll(manifestPath)
        Dim target = rows.FirstOrDefault(Function(r) r.Filename = state.ActiveSnapshotFilename)
        If target Is Nothing Then Return

        target.FinalisedIso      = finalisedIso
        target.StreakLength      = If(streakRounds Is Nothing, target.StreakLength, streakRounds.Count)
        target.AvgFailureRatePct = conditions.AvgFailureRatePct
        target.RegimeMix         = conditions.RegimeMix
        target.AtrScaleAvg       = conditions.AtrScaleAvg
        target.AtrScaleMin       = conditions.AtrScaleMin
        target.AtrScaleMax       = conditions.AtrScaleMax
        target.FundingMin        = conditions.FundingMin
        target.FundingMax        = conditions.FundingMax
        target.NetPriceMovePct   = conditions.NetPriceMovePct
        target.VolumeRatioAvg    = conditions.VolumeRatioAvg
        target.VerdictTierMix    = conditions.VerdictTierMix
        target.VWAPDevAvg        = conditions.VWAPDevAvg
        target.VWAPDevMin        = conditions.VWAPDevMin
        target.VWAPDevMax        = conditions.VWAPDevMax
        target.SpreadRegimeMix   = conditions.SpreadRegimeMix
        target.OFIImbalanceMix   = conditions.OFIImbalanceMix
        target.ConditionBucket   = conditions.ConditionBucket

        ' ── Bucket rotation check (§3h) ────────────────────────────────────────
        Dim existing = rows.FirstOrDefault(
            Function(r) r.Status = "ACTIVE" AndAlso
                        r.Filename <> target.Filename AndAlso
                        r.ConditionBucket = target.ConditionBucket)

        If existing IsNot Nothing Then
            Dim scoreNew As Double = CompositeScorer.Score(
                target.AvgFailureRatePct, target.StreakLength,
                streakWeight, streakLengthClamp)
            Dim scoreOld As Double = CompositeScorer.Score(
                existing.AvgFailureRatePct, existing.StreakLength,
                streakWeight, streakLengthClamp)

            If scoreNew > scoreOld Then
                existing.Status         = "ROTATED"
                existing.RotationReason = "superseded by " & target.Filename
                DeleteSnapshotFile(snapshotsDir, existing.Filename)
                Console.WriteLine(String.Format(
                    "[SnapshotManager] Rotated existing {0} (score {1:F2} <= new {2:F2}) in bucket {3}",
                    existing.Filename, scoreOld, scoreNew, existing.ConditionBucket))
            Else
                target.Status         = "ROTATED"
                target.RotationReason = String.Format(CultureInfo.InvariantCulture,
                    "score {0:F2} <= existing {1:F2}", scoreNew, scoreOld)
                DeleteSnapshotFile(snapshotsDir, target.Filename)
                Console.WriteLine(String.Format(
                    "[SnapshotManager] New snapshot {0} immediately rotated (score {1:F2} <= existing {2:F2})",
                    target.Filename, scoreNew, scoreOld))
            End If
        End If

        WriteAll(manifestPath, rows)

        Console.WriteLine(String.Format(
            "[SnapshotManager] Finalised snapshot {0} at streak length {1}",
            state.ActiveSnapshotFilename, target.StreakLength))

        state.ActiveSnapshotFilename   = ""
        state.ActiveSnapshotCreatedIso = ""
    End Sub

    ' Returns all ACTIVE manifest rows formatted as a CSV string (header + rows).
    ' Used by PromptBuilder when including manifest in API payloads.
    Public Shared Function GetActiveRowsCsv(manifestPath As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine(ManifestHeader)
        For Each row In LoadAll(manifestPath).Where(Function(r) r.Status = "ACTIVE")
            sb.AppendLine(SerialiseRow(row))
        Next
        Return sb.ToString()
    End Function

    ' Load all manifest rows. Returns empty list if file is missing.
    Public Shared Function LoadAll(manifestPath As String) As List(Of ManifestRow)
        Dim result As New List(Of ManifestRow)()
        If Not File.Exists(manifestPath) Then Return result

        Dim lines As String() = File.ReadAllLines(manifestPath)
        If lines.Length < 2 Then Return result

        For i As Integer = 1 To lines.Length - 1
            Dim row = ParseRow(lines(i))
            If row IsNot Nothing Then result.Add(row)
        Next
        Return result
    End Function

    ' ── Internal helpers ───────────────────────────────────────────────────────

    Private Shared Sub AppendRow(manifestPath As String, row As ManifestRow)
        Dim dir = Path.GetDirectoryName(manifestPath)
        If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
        Dim writeHeader As Boolean = Not File.Exists(manifestPath)
        Using sw As New StreamWriter(manifestPath, append:=True)
            If writeHeader Then sw.WriteLine(ManifestHeader)
            sw.WriteLine(SerialiseRow(row))
        End Using
    End Sub

    Private Shared Sub WriteAll(manifestPath As String, rows As List(Of ManifestRow))
        Dim dir = Path.GetDirectoryName(manifestPath)
        If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
        Using sw As New StreamWriter(manifestPath, append:=False)
            sw.WriteLine(ManifestHeader)
            For Each r In rows
                sw.WriteLine(SerialiseRow(r))
            Next
        End Using
    End Sub

    Private Shared Function SerialiseRow(r As ManifestRow) As String
        Return String.Join(",",
            CsvSafe(r.Filename),
            CsvSafe(r.CreatedIso),
            CsvSafe(r.FinalisedIso),
            r.StreakLength.ToString(CultureInfo.InvariantCulture),
            r.AvgFailureRatePct.ToString("F2", CultureInfo.InvariantCulture),
            CsvSafe(r.RegimeMix),
            r.AtrScaleAvg.ToString("F4", CultureInfo.InvariantCulture),
            r.AtrScaleMin.ToString("F4", CultureInfo.InvariantCulture),
            r.AtrScaleMax.ToString("F4", CultureInfo.InvariantCulture),
            r.FundingMin.ToString("F8", CultureInfo.InvariantCulture),
            r.FundingMax.ToString("F8", CultureInfo.InvariantCulture),
            r.NetPriceMovePct.ToString("F4", CultureInfo.InvariantCulture),
            r.VolumeRatioAvg.ToString("F4", CultureInfo.InvariantCulture),
            CsvSafe(r.VerdictTierMix),
            r.VWAPDevAvg.ToString("F4", CultureInfo.InvariantCulture),
            r.VWAPDevMin.ToString("F4", CultureInfo.InvariantCulture),
            r.VWAPDevMax.ToString("F4", CultureInfo.InvariantCulture),
            CsvSafe(r.SpreadRegimeMix),
            CsvSafe(r.OFIImbalanceMix),
            CsvSafe(r.ConditionBucket),
            CsvSafe(r.Status),
            CsvSafe(r.RotationReason))
    End Function

    Private Shared Function ParseRow(line As String) As ManifestRow
        Try
            Dim parts = SplitCsvLine(line)
            If parts.Count < 22 Then Return Nothing
            Dim r As New ManifestRow() With {
                .Filename          = parts(0),
                .CreatedIso        = parts(1),
                .FinalisedIso      = parts(2),
                .StreakLength      = ParseI(parts(3)),
                .AvgFailureRatePct = ParseD(parts(4)),
                .RegimeMix         = parts(5),
                .AtrScaleAvg       = ParseD(parts(6)),
                .AtrScaleMin       = ParseD(parts(7)),
                .AtrScaleMax       = ParseD(parts(8)),
                .FundingMin        = ParseD(parts(9)),
                .FundingMax        = ParseD(parts(10)),
                .NetPriceMovePct   = ParseD(parts(11)),
                .VolumeRatioAvg    = ParseD(parts(12)),
                .VerdictTierMix    = parts(13),
                .VWAPDevAvg        = ParseD(parts(14)),
                .VWAPDevMin        = ParseD(parts(15)),
                .VWAPDevMax        = ParseD(parts(16)),
                .SpreadRegimeMix   = parts(17),
                .OFIImbalanceMix   = parts(18),
                .ConditionBucket   = parts(19),
                .Status            = parts(20),
                .RotationReason    = parts(21)
            }
            Return r
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function ParseI(s As String) As Integer
        Dim v As Integer
        Integer.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, v)
        Return v
    End Function

    Private Shared Function ParseD(s As String) As Double
        Dim v As Double
        Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, v)
        Return v
    End Function

    Private Shared Function CsvSafe(s As String) As String
        If s Is Nothing Then Return ""
        If s.IndexOfAny(New Char() {","c, """"c, ControlChars.Lf, ControlChars.Cr}) >= 0 Then
            Return """" & s.Replace("""", """""") & """"
        End If
        Return s
    End Function

    ' Minimal RFC-4180-style splitter (handles quoted fields with embedded commas).
    Private Shared Function SplitCsvLine(line As String) As List(Of String)
        Dim parts As New List(Of String)()
        Dim sb As New StringBuilder()
        Dim inQuotes As Boolean = False
        Dim i As Integer = 0
        Do While i < line.Length
            Dim c As Char = line(i)
            If inQuotes Then
                If c = """"c Then
                    If i + 1 < line.Length AndAlso line(i + 1) = """"c Then
                        sb.Append(""""c)
                        i += 1
                    Else
                        inQuotes = False
                    End If
                Else
                    sb.Append(c)
                End If
            Else
                If c = ","c Then
                    parts.Add(sb.ToString())
                    sb.Clear()
                ElseIf c = """"c Then
                    inQuotes = True
                Else
                    sb.Append(c)
                End If
            End If
            i += 1
        Loop
        parts.Add(sb.ToString())
        Return parts
    End Function

    Private Shared Sub DeleteSnapshotFile(snapshotsDir As String, filename As String)
        Try
            Dim fullPath As String = Path.Combine(snapshotsDir, filename)
            If File.Exists(fullPath) Then File.Delete(fullPath)
        Catch ex As Exception
            Console.WriteLine("[SnapshotManager] Could not delete rotated snapshot file: " & ex.Message)
        End Try
    End Sub

End Class
