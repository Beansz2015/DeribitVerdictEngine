' Core/WsFeedLog.vb
' Durable sidecar for the WebSocket feed's own narrative — `ws_feed.log` beside the CSV.
'
' ⛔ WHY IT EXISTS. DeribitWsFeed.Log wrote every feed event to Console.WriteLine, and the
' collector runs as a WinForms app launched into an interactive session by a throwaway
' scheduled task. NOTHING CAPTURES THAT CONSOLE. On 2026-09-21 the feed was down for hours
' and the reason was structurally unreadable: ws_health.log records the STATE transition
' (OK/DEGRADED/DOWN/REST) but never the CAUSE, and "connection error: <msg>" — the one line
' that would have answered it — went to a stream nobody owns. This closes that gap.
'
' Contract mirrors WsHealthLog and AlertsSidecar: never throws, path = AppDomain
' BaseDirectory + "ws_feed.log", one line per event:
'   utc | instance_id | message
' and when a repeat was suppressed:
'   utc | instance_id | message | repeated 37x since 2026-09-21T10:55:54.123Z
'
' ⭐ RATE LIMIT, NOT TRANSITION-ONLY, and the difference matters. WsHealthLog can be
' transition-only because its alphabet is four states. This alphabet is free text, and a
' reconnect cycle emits THREE different messages in rotation ("connecting…", "connection
' error: …", "reconnecting in Ns"), so a consecutive-duplicate filter would never fire and
' the file would grow unbounded through an outage. Instead each DISTINCT message is allowed
' at most one line per RepeatWindowMinutes, carrying the count of what it suppressed.
'
' ⛔ It is deliberately NOT "collapse and emit only when the message changes". That version
' is silent for exactly as long as the problem persists — the silent-hole class this repo
' already rejects. A repeating error re-emits on the window, forever, with its count.
'
' Host-agnostic (no WinForms). No settings keys, no version bump. Observation only — ZERO
' scoring impact, no CSV column, no rendered surface. Fixtures A85a-A85e.

Imports System.IO
Imports System.Globalization
Imports System.Collections.Generic

Public NotInheritable Class WsFeedLog

    Private Const FileName As String = "ws_feed.log"

    ''' <summary>A given distinct message writes at most one line per this many minutes.
    ''' Public so the fixture reads the production number instead of restating it
    ''' (CLAUDE.md, RULED 2026-08-11: a value ruled into a constant goes Public Const).</summary>
    Public Const RepeatWindowMinutes As Double = 5.0

    ''' <summary>Cap on distinct tracked messages. A feed error that embeds varying detail
    ''' (a port, a byte count) mints a new key every time, so the map itself needs a bound —
    ''' the unbounded-collection defect this repo has already paid for once in _evalCache.
    ''' On overflow the map is cleared and a line SAYS SO: bounded, never silent.</summary>
    Public Const MaxTrackedMessages As Integer = 200

    Private Structure Seen
        Public LastWrittenUtc As DateTime
        Public SuppressedSince As DateTime
        Public SuppressedCount As Integer
    End Structure

    Private Shared ReadOnly _lock As New Object()
    Private Shared ReadOnly _seen As New Dictionary(Of String, Seen)(StringComparer.Ordinal)

    Private Sub New()
    End Sub

    Public Shared Function GetPath() As String
        Return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName)
    End Function

    ''' <summary>Record a feed event. Never throws. Returns True when a line was actually
    ''' written, False when the message was inside its repeat window and only counted —
    ''' the return exists so a fixture can assert the suppression without reading the file.</summary>
    Public Shared Function Write(message As String) As Boolean
        Return WriteAt(message, DateTime.UtcNow)
    End Function

    ''' <summary>Clock-injected form. Production calls <see cref="Write"/>; the fixture drives
    ''' this so the window can be crossed without sleeping.</summary>
    Friend Shared Function WriteAt(message As String, nowUtc As DateTime) As Boolean
        If message Is Nothing Then message = ""
        Try
            SyncLock _lock
                Dim overflowed As Boolean = False
                If _seen.Count >= MaxTrackedMessages AndAlso Not _seen.ContainsKey(message) Then
                    _seen.Clear()
                    overflowed = True
                End If

                Dim s As Seen = Nothing
                If _seen.TryGetValue(message, s) Then
                    If (nowUtc - s.LastWrittenUtc).TotalMinutes < RepeatWindowMinutes Then
                        ' Inside the window: count it, write nothing.
                        If s.SuppressedCount = 0 Then s.SuppressedSince = nowUtc
                        s.SuppressedCount += 1
                        _seen(message) = s
                        Return False
                    End If
                End If

                If overflowed Then
                    TryAppend(nowUtc, String.Format(CultureInfo.InvariantCulture,
                        "ws_feed.log tracked-message map hit {0} entries and was cleared — repeat suppression restarts",
                        MaxTrackedMessages))
                End If

                Dim suffix As String = ""
                If s.SuppressedCount > 0 Then
                    suffix = String.Format(CultureInfo.InvariantCulture,
                                           " | repeated {0}x since {1:yyyy-MM-ddTHH:mm:ss.fffZ}",
                                           s.SuppressedCount, s.SuppressedSince)
                End If

                TryAppend(nowUtc, message & suffix)
                _seen(message) = New Seen With {.LastWrittenUtc = nowUtc,
                                                .SuppressedSince = Nothing,
                                                .SuppressedCount = 0}
                Return True
            End SyncLock
        Catch
            ' Observation only. A logging failure must never reach the feed's run loop.
            Return False
        End Try
    End Function

    ''' <summary>Test-only — clear the in-process repeat state. Does NOT touch the file.</summary>
    Friend Shared Sub ResetForTests()
        SyncLock _lock
            _seen.Clear()
        End SyncLock
    End Sub

    ''' <summary>Test-only — number of distinct messages currently tracked.</summary>
    Friend Shared ReadOnly Property TrackedCount As Integer
        Get
            SyncLock _lock
                Return _seen.Count
            End SyncLock
        End Get
    End Property

    Private Shared Sub TryAppend(nowUtc As DateTime, line As String)
        Try
            Dim id As String = ""
            Try
                id = If(ProcessIdentity.InstanceId, "")
            Catch
            End Try
            File.AppendAllText(GetPath(),
                String.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-ddTHH:mm:ss.fffZ} | {1} | {2}",
                              nowUtc, id, line) & Environment.NewLine)
        Catch
            ' Swallow — same contract as WsHealthLog.
        End Try
    End Sub

End Class
