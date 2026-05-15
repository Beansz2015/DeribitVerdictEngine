' OhlcCache.vb
' Rolling 7-day on-disk cache for 1m OHLC bars.
' Host-agnostic: no System.Windows.Forms references.
'
' Schema: CSV with header comment "# schema=v1 (1m ohlc cache)"
' Columns: CloseTime,Open,High,Low,Close,Volume (Volume = 0 placeholder)
'
' Rolling cap: 10,080 bars (7 days). RollingTrim fires only when explicitly
' called — the in-memory slack check lives in LivePerformanceTracker.
' Write failures are swallowed; never throws.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq

Public Class OhlcCache

    Private Const SCHEMA_COMMENT As String = "# schema=v1 (1m ohlc cache)"
    Private Const COL_HEADER     As String = "CloseTime,Open,High,Low,Close,Volume"

    ''' <summary>7-day rolling cap in bars (7 × 24 × 60).</summary>
    Public Const MAX_BARS As Integer = 10_080

    ' -----------------------------------------------------------------------
    ' Public interface
    ' -----------------------------------------------------------------------

    ''' <summary>
    ''' Load the OHLC cache from disk into a Dictionary keyed by CloseTime (UTC).
    ''' Returns an empty dictionary if the file is missing, unreadable, or malformed.
    ''' Never throws.
    ''' </summary>
    Public Shared Function Load(path As String) As Dictionary(Of DateTime, OhlcBar)
        Dim result As New Dictionary(Of DateTime, OhlcBar)()
        If Not File.Exists(path) Then Return result
        Try
            For Each line As String In File.ReadLines(path)
                If line.StartsWith("#") OrElse line.StartsWith("CloseTime") OrElse
                   String.IsNullOrWhiteSpace(line) Then Continue For
                Dim bar As OhlcBar = ParseLine(line)
                If bar IsNot Nothing Then result(bar.CloseTime) = bar
            Next
        Catch ex As Exception
            Console.WriteLine("[OhlcCache] Load failed: " & ex.Message)
        End Try
        Return result
    End Function

    ''' <summary>
    ''' Append the given bars to the on-disk cache.
    ''' Caller is responsible for pre-filtering to only bars newer than the
    ''' current cache. Bars are written in CloseTime order.
    ''' Creates the file with schema header if it does not already exist.
    ''' Never throws.
    ''' </summary>
    Public Shared Sub Append(path As String, bars As IEnumerable(Of OhlcBar))
        Try
            Dim ordered = bars.OrderBy(Function(b) b.CloseTime).ToList()
            If ordered.Count = 0 Then Return
            Dim needsHeader As Boolean = Not File.Exists(path) OrElse
                                         New FileInfo(path).Length = 0
            Using sw As New StreamWriter(path, append:=True)
                If needsHeader Then
                    sw.WriteLine(SCHEMA_COMMENT)
                    sw.WriteLine(COL_HEADER)
                End If
                For Each bar In ordered
                    sw.WriteLine(FormatBar(bar))
                Next
            End Using
        Catch ex As Exception
            Console.WriteLine("[OhlcCache] Append failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Overwrite the entire cache file with the given bars (used after a full
    ''' 7-day fetch). Bars are written in CloseTime order.
    ''' Never throws.
    ''' </summary>
    Public Shared Sub WriteAll(path As String, bars As IEnumerable(Of OhlcBar))
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine(SCHEMA_COMMENT)
                sw.WriteLine(COL_HEADER)
                For Each bar In bars.OrderBy(Function(b) b.CloseTime)
                    sw.WriteLine(FormatBar(bar))
                Next
            End Using
        Catch ex As Exception
            Console.WriteLine("[OhlcCache] WriteAll failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Trim the on-disk file so only the most recent maxBars data rows remain.
    ''' No-op if the file already has ≤ maxBars rows.
    ''' Never throws.
    ''' </summary>
    Public Shared Sub RollingTrim(path As String, maxBars As Integer)
        Try
            If Not File.Exists(path) Then Return
            Dim allLines = File.ReadAllLines(path)
            Dim header As New List(Of String)()
            Dim data   As New List(Of String)()
            For Each line In allLines
                If line.StartsWith("#") OrElse line.StartsWith("CloseTime") Then
                    header.Add(line)
                ElseIf Not String.IsNullOrWhiteSpace(line) Then
                    data.Add(line)
                End If
            Next
            If data.Count <= maxBars Then Return
            Dim kept = data.Skip(data.Count - maxBars).ToList()
            Dim result As New List(Of String)()
            result.AddRange(header)
            result.AddRange(kept)
            File.WriteAllLines(path, result)
        Catch ex As Exception
            Console.WriteLine("[OhlcCache] RollingTrim failed: " & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' Return the CloseTime (UTC) of the newest bar in the file.
    ''' Returns Nothing if the file is missing or has no data rows.
    ''' Never throws.
    ''' </summary>
    Public Shared Function NewestBarTime(path As String) As DateTime?
        If Not File.Exists(path) Then Return Nothing
        Try
            Dim lastData As String = Nothing
            For Each line As String In File.ReadLines(path)
                If Not line.StartsWith("#") AndAlso
                   Not line.StartsWith("CloseTime") AndAlso
                   Not String.IsNullOrWhiteSpace(line) Then
                    lastData = line
                End If
            Next
            If lastData Is Nothing Then Return Nothing
            Dim parts = lastData.Split(","c)
            If parts.Length < 1 Then Return Nothing
            ' AdjustToUniversal honours the Z suffix and returns Kind=Utc;
            ' AssumeUniversal treats unsuffixed strings as UTC (defensive).
            ' Without these flags, DateTime.Parse converted Z-suffixed strings
            ' to local time and SpecifyKind(Utc) re-labelled the shifted value
            ' as UTC, leaving every loaded key offset by the local UTC offset.
            ' Bug fixed 2026-05-15 in lockstep with the same fix on the eval
            ' cache parsers (commit 4caa0bc).
            Return DateTime.Parse(
                parts(0).Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal Or DateTimeStyles.AssumeUniversal)
        Catch ex As Exception
            Console.WriteLine("[OhlcCache] NewestBarTime failed: " & ex.Message)
            Return Nothing
        End Try
    End Function

    ' -----------------------------------------------------------------------
    ' Private helpers
    ' -----------------------------------------------------------------------

    Private Shared Function ParseLine(line As String) As OhlcBar
        Try
            Dim p = line.Split(","c)
            If p.Length < 5 Then Return Nothing
            Dim bar As New OhlcBar()
            ' See NewestBarTime for rationale on the parse flags.
            bar.CloseTime = DateTime.Parse(
                p(0).Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal Or DateTimeStyles.AssumeUniversal)
            bar.Open  = Double.Parse(p(1), CultureInfo.InvariantCulture)
            bar.High  = Double.Parse(p(2), CultureInfo.InvariantCulture)
            bar.Low   = Double.Parse(p(3), CultureInfo.InvariantCulture)
            bar.Close = Double.Parse(p(4), CultureInfo.InvariantCulture)
            Return bar
        Catch
            Return Nothing
        End Try
    End Function

    Private Shared Function FormatBar(bar As OhlcBar) As String
        ' Volume placeholder 0 — field reserved for future use.
        Return String.Format(CultureInfo.InvariantCulture,
            "{0:o},{1:F2},{2:F2},{3:F2},{4:F2},0",
            bar.CloseTime, bar.Open, bar.High, bar.Low, bar.Close)
    End Function

End Class
