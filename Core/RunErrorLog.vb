' Core/RunErrorLog.vb
' [C-8 / CH-1..CH-3, docs/collector-halt-fixes-spec.md] The run-error sidecar `run_errors.log`
' beside the CSV. Before this file, an exception in an auto-run raised a modal MessageBox that
' stopped the collector until a human clicked OK (adversarial audit row B2), and nothing on
' disk said why. Now every failure is written here, and the loop carries on.
'
' EVERY failure is one line (CH-3), not transition-only like ws_health.log: a failure that
' repeats every run is a count worth seeing. Worst case about one line per minute.
'
' Line: utc | origin | instance_id | trigger | exception type | message | top frame
'   origin: RUN (btnAnalyze_Click) · UI_THREAD (Application.ThreadException) ·
'           APPDOMAIN (the process is dying) · CONFIG (a settings value rejected at use)
' A pipe, CR or LF inside any field is replaced, so every line splits into exactly seven
' fields. The message and the frame are capped (public constants — fixtures read them).
'
' Contract mirrors WsHealthLog / VenueStatusLog: host-agnostic (no WinForms), never throws,
' path = AppDomain BaseDirectory + "run_errors.log". No settings keys, no bump. Zero scoring
' impact. Fixture: A93a.

Imports System.IO
Imports System.Globalization

Public NotInheritable Class RunErrorLog

    Public Const FileName As String = "run_errors.log"
    Public Const OriginRun As String = "RUN"
    Public Const OriginUiThread As String = "UI_THREAD"
    Public Const OriginAppDomain As String = "APPDOMAIN"
    Public Const OriginConfig As String = "CONFIG"
    ''' <summary>Cap on the message field, in characters, after sanitising.</summary>
    Public Const MaxMessageChars As Integer = 300
    ''' <summary>Cap on the top-frame field, in characters, after sanitising.</summary>
    Public Const MaxFrameChars As Integer = 200

    Private Shared ReadOnly _lock As New Object()

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Append one failure line. Never throws; returns False when the append failed.</summary>
    Public Shared Function Log(origin As String, ex As Exception, trigger As String,
                               instanceId As String) As Boolean
        Return LogTo(GetPath(), origin, ex, trigger, instanceId, DateTime.UtcNow)
    End Function

    ''' <summary>Append a failure described by text alone (CONFIG rejections carry no exception).
    ''' Never throws.</summary>
    Public Shared Function LogText(origin As String, typeName As String, message As String,
                                   instanceId As String) As Boolean
        Dim line As String = ComposeLine(DateTime.UtcNow, origin, instanceId, "", typeName, message, "")
        Return TryAppend(GetPath(), line)
    End Function

    ''' <summary>Path- and clock-injectable form, for fixtures. Never throws.</summary>
    Friend Shared Function LogTo(path As String, origin As String, ex As Exception, trigger As String,
                                 instanceId As String, utc As DateTime) As Boolean
        Dim typeName As String = ""
        Dim message As String = ""
        Dim frame As String = ""
        Try
            If ex IsNot Nothing Then
                typeName = ex.GetType().FullName
                message = ex.Message
                frame = TopFrame(ex)
            End If
        Catch
            ' A hostile exception object (a throwing Message override) must not stop the log.
        End Try
        Return TryAppend(path, ComposeLine(utc, origin, instanceId, trigger, typeName, message, frame))
    End Function

    ''' <summary>The seven-field line, sanitised and capped. Pure.</summary>
    Friend Shared Function ComposeLine(utc As DateTime, origin As String, instanceId As String,
                                       trigger As String, typeName As String, message As String,
                                       frame As String) As String
        Return String.Join(" | ", {
            utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture),
            Clean(origin, Integer.MaxValue),
            Clean(instanceId, Integer.MaxValue),
            Clean(trigger, Integer.MaxValue),
            Clean(typeName, Integer.MaxValue),
            Clean(message, MaxMessageChars),
            Clean(frame, MaxFrameChars)})
    End Function

    ''' <summary>Replace the field separator and line breaks, trim, then cap. Pure.</summary>
    Friend Shared Function Clean(s As String, maxChars As Integer) As String
        If String.IsNullOrEmpty(s) Then Return ""
        Dim t As String = s.Replace("|", "/").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        If t.Length > maxChars Then t = t.Substring(0, maxChars)
        Return t
    End Function

    ' The first "at ..." line of the innermost exception's stack trace: where it was thrown.
    Private Shared Function TopFrame(ex As Exception) As String
        Dim inner As Exception = ex
        While inner.InnerException IsNot Nothing
            inner = inner.InnerException
        End While
        Dim st As String = inner.StackTrace
        If String.IsNullOrEmpty(st) Then Return ""
        Dim lines = st.Split({vbCrLf, vbLf}, StringSplitOptions.RemoveEmptyEntries)
        Return If(lines.Length > 0, lines(0).Trim(), "")
    End Function

    Private Shared Function TryAppend(path As String, line As String) As Boolean
        Try
            SyncLock _lock
                Dim dir As String = System.IO.Path.GetDirectoryName(path)
                If Not String.IsNullOrEmpty(dir) Then Directory.CreateDirectory(dir)
                File.AppendAllText(path, line & vbLf)
            End SyncLock
            Return True
        Catch ex As Exception
            Console.WriteLine("[RunErrorLog] append failed: " & ex.Message)
            Return False
        End Try
    End Function

End Class
