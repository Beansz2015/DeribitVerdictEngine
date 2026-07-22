' Core/WsHealthLog.vb
' WS-health per-run state persistence (roadmap W4 row — closes the "feed health is
' inferred" caveat). Transition-only append-only sidecar `ws_health.log` beside the
' CSV. The same OK/DEGRADED/DOWN/REST enum SignalEmitter.DeriveWsHealth derives (§8
' D8); logged exactly once per state transition plus one line at process start —
' the file stays tiny, no rotation.
'
' Contract mirrors AlertsSidecar (Core/AlertsTracker.vb): never throws, path =
' AppDomain BaseDirectory + "ws_health.log", one line per event:
'   utc | state | instance_id
'
' Host-agnostic (no WinForms). No settings keys, no bump. Display/observation only —
' ZERO scoring impact. Fixture: A38 (transition-only logic — same state twice ⇒ one
' line).

Imports System.IO
Imports System.Globalization

Public NotInheritable Class WsHealthLog

    Private Const FileName As String = "ws_health.log"

    ' Last-logged state. Nothing means we haven't written anything in this process
    ' yet; the first LogStart / LogTransition call will write regardless.
    Private Shared _lastState As String = Nothing
    Private Shared ReadOnly _lock As New Object()

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Process-start line — unconditional (writes even if it matches the
    ''' pre-existing sidecar's last line; the file is per-process observational, not
    ''' a state store across restarts). Sets the transition baseline for this
    ''' process.</summary>
    Public Shared Sub LogStart(state As String, instanceId As String)
        SyncLock _lock
            _lastState = state
            TryAppend(state, instanceId)
        End SyncLock
    End Sub

    ''' <summary>Transition-only append. Same state twice in a row ⇒ ZERO lines.
    ''' First call in a process (i.e. before LogStart) is treated as a transition
    ''' and writes.</summary>
    Public Shared Sub LogTransition(state As String, instanceId As String)
        SyncLock _lock
            If _lastState IsNot Nothing AndAlso String.Equals(state, _lastState, StringComparison.Ordinal) Then
                Return
            End If
            _lastState = state
            TryAppend(state, instanceId)
        End SyncLock
    End Sub

    ''' <summary>Test-only — reset the in-process transition baseline (does NOT touch
    ''' the sidecar file). Harness A38 uses this between sub-cases.</summary>
    Public Shared Sub ResetForTest()
        SyncLock _lock
            _lastState = Nothing
        End SyncLock
    End Sub

    ''' <summary>Test-only — peek the last-logged state (Nothing if never logged in
    ''' this process).</summary>
    Public Shared Function LastLoggedState() As String
        SyncLock _lock
            Return _lastState
        End SyncLock
    End Function

    ' -- private ---------------------------------------------------------------

    Private Shared Function TryAppend(state As String, instanceId As String) As Boolean
        Try
            Dim path As String = GetPath()
            Dim dir As String = System.IO.Path.GetDirectoryName(path)
            If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
            Dim utc As String = DateTime.UtcNow.ToString(
                                    "yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)
            Dim safeState As String = If(state, "")
            Dim safeIid As String = If(instanceId, "")
            Dim line As String = String.Format(CultureInfo.InvariantCulture,
                                               "{0} | {1} | {2}" & vbLf,
                                               utc, safeState, safeIid)
            File.AppendAllText(path, line)
            Return True
        Catch ex As Exception
            Console.WriteLine("[WsHealthLog] append failed: " & ex.Message)
            Return False
        End Try
    End Function

End Class
