' Core/CaptureMarkerLog.vb
' [C1 / D7] Per-process capture-scope marker (docs/trade-store-coverage-report-proposal.md D7,
' pinned by docs/j-b-scoping-ruling-2026-08-02.md). The coverage report must never infer
' whether a box was capturing from the CURRENT settings.json (a single later flip would
' misread all of history) and never from an expected-uptime baseline (the same statistical
' mistake proposal §0 exists to reject — a box that stops capturing permanently would
' converge to its own baseline and read healthy). It needs a POSITIVE RECORD instead; this
' file is that record.
'
' One line, written ONCE per process at start — unconditional, mirroring WsHealthLog.LogStart
' rather than WsHealthLog.LogTransition. There is no "marker changed" concept: a process reads
' its settings once at startup, and that single reading is the fact worth recording.
'
'   utc | enabled | store_dir | instance_id
'
' The caller MUST pass the MERGED value (SettingsLoader.Current, post settings.local.json
' overlay) — never the tracked base file. A base-file read would see trade_store.enabled:true
' on a box whose overlay turned it off and report every local up-hour as a defect, which is
' the exact false alarm the ruling exists to prevent.
'
' Contract mirrors WsHealthLog / AlertsSidecar (Core/AlertsTracker.vb): host-agnostic,
' exe-relative path, append-only, NEVER THROWS. No settings keys, no version bump — this
' reads existing trade_store.* fields, it does not add any.
'
' Fixtures: A49k (not-capturing), A49l (unknown-scope) in verify/ordercheck.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO

Public NotInheritable Class CaptureMarkerLog

    Private Const FileName As String = "capture_marker.log"

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Unconditional per-process start line. Never throws.</summary>
    Public Shared Sub LogStart(enabled As Boolean, storeDir As String, instanceId As String)
        Try
            Dim path As String = GetPath()
            Dim dir As String = System.IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Dim utc As String = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
            ' '|' is the field separator, so any occurrence in a path is neutralised — the
            ' same discipline MTFGateReason uses on ',' in AnalysisLogger.
            Dim safeDir As String = If(storeDir, "").Replace("|", "/")
            Dim safeIid As String = If(instanceId, "")
            Dim line As String = String.Format(CultureInfo.InvariantCulture,
                                               "{0} | {1} | {2} | {3}" & vbLf,
                                               utc, enabled.ToString(CultureInfo.InvariantCulture), safeDir, safeIid)
            File.AppendAllText(path, line)
        Catch ex As Exception
            Console.WriteLine("[CaptureMarkerLog] append failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>One parsed marker record — one process life's declared capture scope.</summary>
    Public Class MarkerRecord
        Public Property UtcMs As Long
        Public Property Enabled As Boolean
        Public Property StoreDir As String = ""
        Public Property InstanceId As String = ""
    End Class

    ''' <summary>Parse the marker file. Missing/unreadable file ⇒ empty list, never throws.
    ''' A malformed individual line is skipped rather than failing the whole read — the same
    ''' per-line tolerance TradeStoreWriter.ReadTradeFile uses.</summary>
    Public Shared Function ParseFile(path As String) As List(Of MarkerRecord)
        Dim result As New List(Of MarkerRecord)
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return result
        Try
            For Each line In File.ReadAllLines(path)
                Dim rec As MarkerRecord = Nothing
                If TryParseLine(line, rec) Then result.Add(rec)
            Next
        Catch ex As Exception
            Console.Error.WriteLine("[CaptureMarkerLog] ParseFile failed: " & ex.Message)
        End Try
        ' Chronological — downstream scope resolution assumes this ordering.
        result.Sort(Function(a, b) a.UtcMs.CompareTo(b.UtcMs))
        Return result
    End Function

    Private Shared Function TryParseLine(line As String, ByRef rec As MarkerRecord) As Boolean
        If String.IsNullOrWhiteSpace(line) Then Return False
        Dim parts = line.Split({" | "}, StringSplitOptions.None)
        If parts.Length < 4 Then Return False
        Dim utc As DateTime
        If Not DateTime.TryParse(parts(0), CultureInfo.InvariantCulture,
                                 DateTimeStyles.AssumeUniversal Or DateTimeStyles.AdjustToUniversal, utc) Then
            Return False
        End If
        Dim enabled As Boolean
        If Not Boolean.TryParse(parts(1).Trim(), enabled) Then Return False
        rec = New MarkerRecord With {
            .UtcMs = New DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            .Enabled = enabled,
            .StoreDir = parts(2).Trim(),
            .InstanceId = parts(3).Trim()
        }
        Return True
    End Function

End Class
