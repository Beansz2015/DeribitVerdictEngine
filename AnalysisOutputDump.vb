' AnalysisOutputDump.vb
' Appends full rendered analysis text to analysis_output_dump.md after each run.
' Host-agnostic — no System.Windows.Forms references.
'
' Rolling-trim: after each append, if block count > maxRuns, oldest blocks are
' dropped until count == maxRuns. maxRuns = 0 means unlimited (no trim).
' Write failures are swallowed — analysis runs must never abort on dump failure.

Imports System.IO

Public Class AnalysisOutputDump

    ''' <summary>
    ''' Append a run block to the dump file. Best-effort; never throws.
    ''' Rolling-trim applied after appending if maxRuns > 0 and count exceeds cap.
    ''' </summary>
    Public Shared Sub Append(timestamp As DateTime, renderedText As String,
                              dumpPath As String, enabled As Boolean,
                              maxRuns As Integer,
                              Optional perfStripLine As String = Nothing)
        If Not enabled Then Return
        Try
            Using sw As New StreamWriter(dumpPath, append:=True)
                sw.WriteLine("## Run " & timestamp.ToString("yyyy-MM-dd HH:mm:ss") & " " & GetTzSuffix())
                If Not String.IsNullOrEmpty(perfStripLine) Then
                    sw.WriteLine(perfStripLine)
                End If
                sw.WriteLine(renderedText.TrimEnd())
                sw.WriteLine()
                sw.WriteLine("---")
                sw.WriteLine()
            End Using

            If maxRuns > 0 Then
                TrimToMaxRuns(dumpPath, maxRuns)
            End If
        Catch ex As Exception
            Console.WriteLine("[AnalysisOutputDump] write failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Truncate the dump file to empty.</summary>
    Public Shared Sub Clear(dumpPath As String)
        Try
            File.WriteAllText(dumpPath, "")
        Catch ex As Exception
            Console.WriteLine("[AnalysisOutputDump] clear failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>Count "## Run " header lines in the file (= run blocks).</summary>
    Public Shared Function CountRuns(dumpPath As String) As Integer
        If Not File.Exists(dumpPath) Then Return 0
        Dim count As Integer = 0
        Try
            For Each line In File.ReadLines(dumpPath)
                If line.StartsWith("## Run ") Then count += 1
            Next
        Catch
        End Try
        Return count
    End Function

    ''' <summary>Trim oldest run blocks until block count == maxRuns.</summary>
    Private Shared Sub TrimToMaxRuns(dumpPath As String, maxRuns As Integer)
        Dim lines As String() = File.ReadAllLines(dumpPath)
        Dim runStartIdx As New List(Of Integer)()
        For i As Integer = 0 To lines.Length - 1
            If lines(i).StartsWith("## Run ") Then runStartIdx.Add(i)
        Next
        If runStartIdx.Count <= maxRuns Then Return

        Dim keepFromIdx As Integer = runStartIdx(runStartIdx.Count - maxRuns)
        Dim trimmed As String() = lines.Skip(keepFromIdx).ToArray()
        File.WriteAllLines(dumpPath, trimmed)
    End Sub

    Private Shared Function GetTzSuffix() As String
        Dim offset As TimeSpan = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now)
        Dim sign As String = If(offset >= TimeSpan.Zero, "+", "-")
        Dim hours As Integer = CInt(Math.Floor(Math.Abs(offset.TotalHours)))
        Return "UTC" & sign & hours.ToString()
    End Function

End Class
