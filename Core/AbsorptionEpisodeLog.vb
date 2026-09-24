' Core/AbsorptionEpisodeLog.vb
' [D-6d Stage 1] absorption_episodes.log — the absorption counting-gap sidecar
' (docs/d6d-episode-continuity-spec.md §4.3; build spec
' docs/absorption-d2-stage1-rotation-build-spec.md §4.3).
'
' ONE LINE PER COMPLETED RUN that read the absorption tracker (WS source, absorption
' enabled), written right after that run's analysis_log.csv row. Never one line per
' close: at a ~2 s median episode age a per-close log would run to ~100k lines/day.
'
' Contract mirrors VenueStatusLog / WsHealthLog: never throws, host-agnostic (no
' WinForms), append-only at AppDomain BaseDirectory + "absorption_episodes.log".
' No settings keys, no version bump. Observation only — ZERO scoring or display impact.
'
' Line format (v1), fields separated by " | ":
'   <utc ISO-8601 ms Z> | v1 | iid=<InstanceId> | sid=<SignalId> | interval_sec=<s>
'     | ABOVE <side fields> | BELOW <side fields>
' interval_sec = seconds since the previous line's drain (EMPTY on the first line of a
' process: the drain window then starts at process start, whose instant the tracker
' does not hold). <side fields> are space-separated key=value pairs:
'   active=0|1  level=<watched level, empty while idle>  last_level=<level last watched,
'   0 = never>  press_usd=<PressSum at the read — the CSV row's AggrUsd source when this
'   side is primary>  episode_sec=<age at the read, empty while idle>
'   shadow_usd=<flow dropped while idle that the live predicate would have counted
'   against last_level>  shadow_n=<prints>  shadow_breaks=<idle intervals ended by a
'   break-class print>
'   then one <Reason>=<count>/<discarded_usd>/<life_sec>/<shadow_usd> per
'   AbsorptionCloseReason member, in enum order.
'   press_accrued_usd=<live press accrued in THIS drain interval> sits after press_usd.
' ⭐ The counting gap per side is shadow_usd / (press_accrued_usd + shadow_usd): both
' terms are interval flows, so lines pool by simple summation. ⚠ press_usd and
' discarded_usd are EPISODE sums (an episode spanning two drains appears in both);
' never sum them across lines. The gap is comparable to the 2026-08-19 replay's 31 %
' only after the BreakThrough bucket is examined (its shadow is flow after a
' legitimate break).
' (iid, sid) is the analysis_log.csv attribution key, so the join to the row is exact.

Imports System.IO
Imports System.Globalization
Imports System.Text

Public NotInheritable Class AbsorptionEpisodeLog

    Private Const FileName As String = "absorption_episodes.log"

    Private Shared ReadOnly _lock As New Object()

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Append this run's line to the default sidecar path. Never throws;
    ''' returns False when the write failed (and says so on the console).</summary>
    Friend Shared Function LogRun(instrument As AbsorptionInstrumentRead,
                                  instanceId As String, signalId As Long) As Boolean
        Return TryAppend(GetPath(), FormatLine(instrument, instanceId, signalId, DateTime.UtcNow))
    End Function

    ''' <summary>The v1 line (no trailing newline). Pure — fixtures assert on it
    ''' directly. A Nothing instrument formats as empty sides rather than throwing.</summary>
    Friend Shared Function FormatLine(instrument As AbsorptionInstrumentRead,
                                      instanceId As String, signalId As Long,
                                      utcNow As DateTime) As String
        Dim inv As CultureInfo = CultureInfo.InvariantCulture
        Dim sb As New StringBuilder()
        sb.Append(utcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", inv))
        sb.Append(" | v1 | iid=").Append(If(instanceId, ""))
        sb.Append(" | sid=").Append(signalId.ToString(inv))
        sb.Append(" | interval_sec=")
        If instrument IsNot Nothing AndAlso instrument.IntervalSec.HasValue Then
            sb.Append(instrument.IntervalSec.Value.ToString("F3", inv))
        End If
        sb.Append(" | ABOVE ").Append(FormatSide(If(instrument Is Nothing, Nothing, instrument.Above)))
        sb.Append(" | BELOW ").Append(FormatSide(If(instrument Is Nothing, Nothing, instrument.Below)))
        Return sb.ToString()
    End Function

    Private Shared Function FormatSide(s As AbsorptionSideInstrument) As String
        Dim inv As CultureInfo = CultureInfo.InvariantCulture
        Dim sb As New StringBuilder()
        If s Is Nothing Then Return "missing=1"
        sb.Append("active=").Append(If(s.Active, "1", "0"))
        sb.Append(" level=").Append(If(s.Active, s.LevelPrice.ToString("F2", inv), ""))
        sb.Append(" last_level=").Append(s.LastLevelPrice.ToString("F2", inv))
        sb.Append(" press_usd=").Append(s.PressSum.ToString("F0", inv))
        sb.Append(" press_accrued_usd=").Append(s.PressAccruedUsd.ToString("F0", inv))
        sb.Append(" episode_sec=").Append(If(s.EpisodeSec.HasValue, s.EpisodeSec.Value.ToString("F3", inv), ""))
        sb.Append(" shadow_usd=").Append(s.ShadowPressUsd.ToString("F0", inv))
        sb.Append(" shadow_n=").Append(s.ShadowPressCount.ToString(inv))
        sb.Append(" shadow_breaks=").Append(s.ShadowBreaks.ToString(inv))
        For i As Integer = 0 To LevelAbsorptionTracker.CloseReasonCount - 1
            Dim t As AbsorptionReasonTally = If(s.Tally IsNot Nothing AndAlso i < s.Tally.Length,
                                                s.Tally(i), New AbsorptionReasonTally())
            sb.Append(" "c).Append(CType(i, AbsorptionCloseReason).ToString()).Append("="c)
            sb.Append(t.Count.ToString(inv)).Append("/"c)
            sb.Append(t.DiscardedUsd.ToString("F0", inv)).Append("/"c)
            sb.Append(t.LifeSec.ToString("F3", inv)).Append("/"c)
            sb.Append(t.ShadowUsd.ToString("F0", inv))
        Next
        Return sb.ToString()
    End Function

    ''' <summary>Append one line. Never throws — a locked or unwritable path returns
    ''' False (fixture A88f).</summary>
    Friend Shared Function TryAppend(path As String, line As String) As Boolean
        SyncLock _lock
            Try
                Dim dir As String = System.IO.Path.GetDirectoryName(path)
                If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
                File.AppendAllText(path, If(line, "") & vbLf)
                Return True
            Catch ex As Exception
                Console.WriteLine("[AbsorptionEpisodeLog] append failed: " & ex.Message)
                Return False
            End Try
        End SyncLock
    End Function

End Class
