' Core/VenueStatusLog.vb
' Venue-status sidecar (instrument spec: docs/venue-status-instrument-spec.md).
' Records when Deribit itself returns a response declaring it is unavailable:
'   Shape A — any 5xx (venue answered with a server error)
'   Shape B — HTTP 200 carrying a JSON-RPC error object
'             e.g. {"error":{"code":11051,"message":"system_maintenance"}}
' NEVER writes for Shape C (no response at all — timeout, DNS failure, TCP refusal).
' That boundary is the instrument's whole purpose: ws_health.log records the WS feed
' going down but cannot distinguish our box from the venue. This log records a positive
' response from the venue itself.
'
' Contract mirrors WsHealthLog (Core/WsHealthLog.vb): never throws, path =
' AppDomain BaseDirectory + "venue_status.log", one line per event:
'   utc | state | instance_id
' where state is "VENUE_<http-status-code>", e.g. "VENUE_503" or "VENUE_200".
'
' Transition-only: N consecutive failures write ONE line. No start line (unlike
' WsHealthLog the venue state is not meaningful at process start — only a real
' venue response warrants a record). Host-agnostic (no WinForms). No settings
' keys, no version bump. Display/observation only — ZERO scoring impact.

Imports System.IO
Imports System.Globalization
Imports System.Text.Json

Public NotInheritable Class VenueStatusLog

    Private Const FileName As String = "venue_status.log"

    ' Last-logged state. Nothing means no line has been written this process.
    Private Shared _lastState As String = Nothing
    Private Shared ReadOnly _lock As New Object()

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Transition-only append. Same state twice in a row ⇒ ZERO lines.
    ''' First call in a process (before any prior LogTransition) is treated as a
    ''' transition and writes one line.</summary>
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
    ''' the sidecar file). Harness A74 uses this between sub-cases.</summary>
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

    ' ── Policy ────────────────────────────────────────────────────────────────────────────────

    ''' <summary>Returns True when this response warrants a venue-status log entry.
    ''' Shape A: any 5xx — venue answered with a server error.
    ''' Shape B: HTTP 200 whose body carries a JSON-RPC error object.
    ''' Shape C (no response — statusCode = Nothing) always returns False.
    ''' That is the V-1 guard: an unanswered request is indistinguishable from our
    ''' own box being broken; logging it would let our defects scope themselves out.
    ''' 4xx also returns False — those are client errors, not venue declarations.</summary>
    Friend Shared Function ShouldRecord(statusCode As Integer?, body As String) As Boolean
        If Not statusCode.HasValue Then Return False   ' V-1: no response at all
        Dim code As Integer = statusCode.Value
        If code >= 500 Then Return True               ' Shape A: 5xx
        If code = 200 Then Return IsRpcError(body)    ' Shape B: 200 + JSON-RPC error
        Return False                                   ' 4xx, 3xx, 1xx — not a venue declaration
    End Function

    ''' <summary>Returns True when the response body contains a JSON-RPC error object
    ''' at the root level (e.g. {"error":{"code":11051,"message":"system_maintenance"}}).
    ''' Never throws — a malformed body is treated as non-error.</summary>
    Friend Shared Function IsRpcError(body As String) As Boolean
        If String.IsNullOrEmpty(body) Then Return False
        Try
            Using doc As JsonDocument = JsonDocument.Parse(body)
                Dim dummy As JsonElement = Nothing
                Return doc.RootElement.TryGetProperty("error", dummy)
            End Using
        Catch
            Return False
        End Try
    End Function

    ' -- private ───────────────────────────────────────────────────────────────────────────────

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
            Console.WriteLine("[VenueStatusLog] append failed: " & ex.Message)
            Return False
        End Try
    End Function

End Class
