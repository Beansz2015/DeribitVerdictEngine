' analysis/ForwardReturnJoiner.vb
' Loads a v0.4 analysis_log.csv, parses each row, and attaches forward-return
' values (T+5 / T+10 / T+15) by index-based lookup (60s cadence assumption).
' Rows where the forward window is incomplete or crosses a UTC session boundary
' are excluded from the returned enriched set.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Globalization

Public Class CsvRow
    Public Property Index       As Integer
    Public Property Timestamp   As DateTime
    Public Property Price       As Double
    Public Property Verdict     As String
    Public Property ATR         As Double
    Public Property Regime      As String
    Public Property FundingBias As String
    Public Property VerdictContext As String
    Public Property OiCvdOutcome   As String
    Public Property OfiRatio       As Double
    Public Property OfiBidVol      As Double
    Public Property OfiAskVol      As Double
    Public Property FundingDelta   As Double
    ' Forward prices keyed by window (5, 10, 15)
    Public Property ForwardPrice   As New Dictionary(Of Integer, Double)()
    ' Whether this row is usable for each window
    Public Property WindowValid    As New Dictionary(Of Integer, Boolean)()
End Class

Public Class ForwardReturnJoiner

    Public Shared Function Load(csvPath As String,
                                sessionStarts As IEnumerable(Of Integer)) As List(Of CsvRow)
        Dim lines As String() = File.ReadAllLines(csvPath)
        If lines.Length <= 1 Then Return New List(Of CsvRow)()

        Dim header = lines(0).Split(","c)
        Dim colIdx As New Dictionary(Of String, Integer)(StringComparer.OrdinalIgnoreCase)
        For i = 0 To header.Length - 1
            colIdx(header(i).Trim()) = i
        Next

        Dim raw As New List(Of CsvRow)()
        For i = 1 To lines.Length - 1
            Dim parts = lines(i).Split(","c)
            If parts.Length < header.Length Then Continue For
            Dim row As New CsvRow() With {.Index = i - 1}
            If colIdx.ContainsKey("Timestamp") Then
                DateTime.TryParseExact(parts(colIdx("Timestamp")).Trim(),
                                       "yyyy-MM-dd HH:mm:ss",
                                       CultureInfo.InvariantCulture,
                                       DateTimeStyles.None,
                                       row.Timestamp)
            End If
            TryParseD(parts, colIdx, "Price",        row.Price)
            row.Verdict     = GetStr(parts, colIdx, "Verdict")
            TryParseD(parts, colIdx, "ATR",           row.ATR)
            row.Regime      = GetStr(parts, colIdx, "Regime")
            row.FundingBias = GetStr(parts, colIdx, "FundingBias")
            row.VerdictContext = GetStr(parts, colIdx, "VerdictContext")
            row.OiCvdOutcome   = GetStr(parts, colIdx, "OiCvdOutcome")
            TryParseD(parts, colIdx, "OFIRatio",     row.OfiRatio)
            TryParseD(parts, colIdx, "OFIBidVol",    row.OfiBidVol)
            TryParseD(parts, colIdx, "OFIAskVol",    row.OfiAskVol)
            TryParseD(parts, colIdx, "FundingDelta", row.FundingDelta)
            raw.Add(row)
        Next

        ' Attach forward prices and mark window validity
        Dim sessionStartSet As New HashSet(Of Integer)(sessionStarts)
        For Each w In AnalysisConstants.HoldWindowsMinutes
            For Each row In raw
                Dim fwdIdx As Integer = row.Index + w
                If fwdIdx >= raw.Count Then
                    row.WindowValid(w) = False
                    Continue For
                End If
                Dim fwdRow = raw(fwdIdx)
                If CrossesSessionBoundary(row.Timestamp, fwdRow.Timestamp, sessionStartSet) Then
                    row.WindowValid(w) = False
                    Continue For
                End If
                row.ForwardPrice(w) = fwdRow.Price
                row.WindowValid(w) = True
            Next
        Next

        Return raw
    End Function

    ' Returns True if any session-start hour falls strictly inside (t1, t2).
    Private Shared Function CrossesSessionBoundary(t1 As DateTime, t2 As DateTime,
                                                   sessionStarts As HashSet(Of Integer)) As Boolean
        If t1 = DateTime.MinValue OrElse t2 = DateTime.MinValue Then Return True
        Dim h1 As Integer = t1.Hour
        Dim h2 As Integer = t2.Hour
        If h1 = h2 Then Return False
        ' Walk hour range from h1 to h2 (may wrap around midnight)
        Dim h As Integer = h1
        Do While h <> h2
            h = (h + 1) Mod 24
            If sessionStarts.Contains(h) Then Return True
        Loop
        Return False
    End Function

    Private Shared Sub TryParseD(parts As String(), colIdx As Dictionary(Of String, Integer),
                                  key As String, ByRef result As Double)
        Dim idx As Integer
        If Not colIdx.TryGetValue(key, idx) Then Return
        Double.TryParse(parts(idx).Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, result)
    End Sub

    Private Shared Function GetStr(parts As String(), colIdx As Dictionary(Of String, Integer),
                                   key As String) As String
        Dim idx As Integer
        If Not colIdx.TryGetValue(key, idx) Then Return ""
        Return parts(idx).Trim()
    End Function

End Class
