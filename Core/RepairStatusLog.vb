' Core/RepairStatusLog.vb
' Repair-outcome sidecar (GR-4 (b), docs/gap-repair-same-ms-page-skip-spec.md §4.4).
'
' Why it exists. Every repair log line used to go to Console only, and nothing in the tree
' captures Console output (no Console.SetOut / SetError anywhere). So on AWS a repair pass that
' failed, a hole the venue no longer served, and a repair timer that had stopped firing were all
' invisible — the 2026-08-17 start-up repair lost 70 trades and no record of any pass survived.
'
' Contract mirrors VenueStatusLog (Core/VenueStatusLog.vb): never throws, host-agnostic, no
' settings keys, no version bump, ZERO scoring impact. Path = AppDomain BaseDirectory +
' "repair_status.log", beside ws_health.log and venue_status.log, and fetched by
' tools/ops/collector.ps1 ($FetchFiles).
'
' ⚠ FOUR fields, not three:   utc | state | instance_id | detail
' ws_health.log and venue_status.log carry three. Do not point their parser at this file.
' `detail` is space-separated key=value, with `reason` (when present) always last. A pipe
' character inside any field is replaced, so every line splits into exactly four fields.
'
' What is written — outcome-only, never per page:
'   • one WINDOW line for every window whose state is not TAIL_OK or TAIL_EMPTY;
'   • one PASS line for every pass, after its windows — ⚠ INCLUDING A CLEAN PASS. A clean pass
'     that wrote nothing would be indistinguishable from a repair timer that has died.
' ~4 pass lines a day; window lines only on real holes. No rotation.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Text

Public NotInheritable Class RepairStatusLog

    Private Const FileName As String = "repair_status.log"

    Public Const PassClean As String = "PASS_CLEAN"
    Public Const PassLoss As String = "PASS_LOSS"
    Public Const PassFailed As String = "PASS_FAILED"

    Private Shared ReadOnly _lock As New Object()

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Write one pass's window lines and its pass line to <see cref="GetPath"/>.
    ''' Never throws.</summary>
    ''' <param name="exceptionReason">Non-empty when the pass itself threw; the pass is then
    ''' PASS_FAILED whatever its windows said.</param>
    Public Shared Sub WritePass(outcomes As IList(Of TradeStoreWriter.RepairWindowOutcome),
                                lookbackHours As Double, instanceId As String,
                                Optional exceptionReason As String = Nothing)
        Try
            AppendTo(GetPath(), ComposePassLines(outcomes, lookbackHours, instanceId, DateTime.UtcNow, exceptionReason))
        Catch ex As Exception
            Console.WriteLine("[RepairStatusLog] write failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>The lines one pass writes, in order: window lines, then the pass line. Pure —
    ''' A79f drives it directly.</summary>
    Friend Shared Function ComposePassLines(outcomes As IList(Of TradeStoreWriter.RepairWindowOutcome),
                                            lookbackHours As Double, instanceId As String, utc As DateTime,
                                            Optional exceptionReason As String = Nothing) As List(Of String)
        Dim lines As New List(Of String)()
        Dim windows As Integer = 0, holes As Integer = 0, failed As Integer = 0, pages As Integer = 0
        Dim committed As Long = 0, notServed As Long = 0

        If outcomes IsNot Nothing Then
            For Each o In outcomes
                If o Is Nothing Then Continue For
                windows += 1
                If o.Window.Kind = TradeStoreWriter.RepairWindowKind.Hole Then holes += 1
                If o.IsFailure Then failed += 1
                pages += o.Pages
                committed += o.Committed
                notServed += o.NotServed
                If o.State <> TradeStoreWriter.RepairWindowOutcome.TailOk AndAlso
                   o.State <> TradeStoreWriter.RepairWindowOutcome.TailEmpty Then
                    lines.Add(FormatLine(utc, o.State, instanceId, WindowDetail(o)))
                End If
            Next
        End If

        Dim hasException As Boolean = Not String.IsNullOrEmpty(exceptionReason)
        Dim state As String
        If hasException OrElse failed > 0 Then
            state = PassFailed
        ElseIf notServed > 0 Then
            state = PassLoss
        Else
            state = PassClean
        End If

        Dim sb As New StringBuilder()
        sb.AppendFormat(CultureInfo.InvariantCulture,
                        "windows={0} holes={1} committed={2} not_served={3} failed={4} pages={5} lookback_h={6}",
                        windows, holes, committed, notServed, failed, pages, lookbackHours)
        If hasException Then sb.Append(" reason=").Append(exceptionReason)
        lines.Add(FormatLine(utc, state, instanceId, sb.ToString()))
        Return lines
    End Function

    ''' <summary>One line, exactly four pipe-separated fields.</summary>
    Friend Shared Function FormatLine(utc As DateTime, state As String, instanceId As String, detail As String) As String
        Return String.Format(CultureInfo.InvariantCulture, "{0} | {1} | {2} | {3}",
                             utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
                             Clean(state), Clean(instanceId), Clean(detail))
    End Function

    ''' <summary>Append lines to <paramref name="path"/>. False on any failure — never throws.</summary>
    Friend Shared Function AppendTo(path As String, lines As IList(Of String)) As Boolean
        If lines Is Nothing OrElse lines.Count = 0 Then Return True
        SyncLock _lock
            Try
                Dim dir As String = System.IO.Path.GetDirectoryName(path)
                If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
                Dim sb As New StringBuilder()
                For Each l In lines
                    sb.Append(l).Append(vbLf)
                Next
                File.AppendAllText(path, sb.ToString())
                Return True
            Catch ex As Exception
                Console.WriteLine("[RepairStatusLog] append failed: " & ex.Message)
                Return False
            End Try
        End SyncLock
    End Function

    ' ── private ───────────────────────────────────────────────────────────────────────

    Private Shared Function WindowDetail(o As TradeStoreWriter.RepairWindowOutcome) As String
        Dim w = o.Window
        Dim sb As New StringBuilder()
        sb.Append("file=").Append(If(String.IsNullOrEmpty(o.FileName), "unknown", o.FileName))
        Select Case w.Kind
            Case TradeStoreWriter.RepairWindowKind.Hole
                sb.AppendFormat(CultureInfo.InvariantCulture, " kind=hole seq={0}..{1}", w.FirstSeq, w.LastSeq)
            Case TradeStoreWriter.RepairWindowKind.Tail
                sb.AppendFormat(CultureInfo.InvariantCulture, " kind=tail seq={0}..open", w.FirstSeq)
            Case Else
                sb.Append(" kind=anchored_tail seq=")
                sb.Append(If(o.StartSeq >= 0, o.StartSeq.ToString(CultureInfo.InvariantCulture), "none")).Append("..open")
                sb.Append(" anchor=").Append(Iso(w.AnchorMs))
        End Select
        sb.AppendFormat(CultureInfo.InvariantCulture,
                        " committed={0} not_served_before={1} not_served_inside={2} not_served_after={3} rejected={4} pages={5}",
                        o.Committed, o.NotServedBefore, o.NotServedInside, o.NotServedAfter, o.Rejected, o.Pages)
        If w.Kind = TradeStoreWriter.RepairWindowKind.Hole Then
            sb.Append(" span=").Append(Iso(w.LeftTsMs)).Append("..").Append(Iso(w.RightTsMs))
        End If
        If Not String.IsNullOrEmpty(o.Reason) Then sb.Append(" reason=").Append(o.Reason)
        Return sb.ToString()
    End Function

    Private Shared Function Iso(ms As Long) As String
        Try
            Return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString(
                       "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
        Catch
            Return ms.ToString(CultureInfo.InvariantCulture)
        End Try
    End Function

    ' A pipe would add a field; a line break would add a line. Neither may reach the file.
    Private Shared Function Clean(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("|", "/").Replace(vbCr, " ").Replace(vbLf, " ")
    End Function

End Class
