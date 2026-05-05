' analysis/FailureRateMatrix.vb
' Computes per-tier x window x ATR-threshold failure rates with 95% Wilson CI.
' Verdict tiers: STRONG_LONG, STRONG_SHORT, MEDIUM_LONG, MEDIUM_SHORT.
' NO_TRADE and WEAK_* are excluded from the denominator.
'
' Failure definition (from failure-definition-proposal.md):
'   LONG: price[N+W] - price[N] < -threshold * ATR[N]
'   SHORT: price[N+W] - price[N] > +threshold * ATR[N]
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Math

Public Class FailureRateMatrix

    ' Maps engine verdict strings → canonical tier names
    Private Shared Function ToTier(verdict As String) As String
        Select Case verdict.Trim().ToUpper()
            Case "STRONG LONG"  : Return "STRONG_LONG"
            Case "LONG"         : Return "MEDIUM_LONG"
            Case "STRONG SHORT" : Return "STRONG_SHORT"
            Case "SHORT"        : Return "MEDIUM_SHORT"
            Case Else           : Return ""   ' WEAK / NO TRADE — excluded
        End Select
    End Function

    Private Shared Function ThresholdsFor(tier As String) As Double()
        If tier.StartsWith("STRONG") Then Return AnalysisConstants.StrongAtrThresholds
        Return AnalysisConstants.MediumAtrThresholds
    End Function

    Private Shared Function IsLong(tier As String) As Boolean
        Return tier.EndsWith("LONG")
    End Function

    Public Shared Function Compute(rows As List(Of CsvRow)) As List(Of FailureCellResult)
        ' Accumulate counts in a nested dict: tier -> window -> threshold -> (n, failures)
        Dim counts As New Dictionary(Of String, Dictionary(Of Integer, Dictionary(Of Double, (N As Integer, F As Integer))))()

        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            counts(tier) = New Dictionary(Of Integer, Dictionary(Of Double, (Integer, Integer)))()
            For Each w In AnalysisConstants.HoldWindowsMinutes
                counts(tier)(w) = New Dictionary(Of Double, (Integer, Integer))()
                For Each thr In ThresholdsFor(tier)
                    counts(tier)(w)(thr) = (0, 0)
                Next
            Next
        Next

        For Each row In rows
            Dim tier As String = ToTier(row.Verdict)
            If tier = "" Then Continue For
            For Each w In AnalysisConstants.HoldWindowsMinutes
                If Not row.WindowValid.ContainsKey(w) OrElse Not row.WindowValid(w) Then Continue For
                Dim fwdPrice As Double = row.ForwardPrice(w)
                Dim priceDiff As Double = fwdPrice - row.Price
                For Each thr In ThresholdsFor(tier)
                    Dim atrBar As Double = thr * row.ATR
                    Dim failed As Boolean
                    If IsLong(tier) Then
                        failed = priceDiff < -atrBar
                    Else
                        failed = priceDiff > +atrBar
                    End If
                    Dim cur = counts(tier)(w)(thr)
                    counts(tier)(w)(thr) = (cur.N + 1, cur.F + If(failed, 1, 0))
                Next
            Next
        Next

        ' Build result list and mark recommended cell per tier
        Dim results As New List(Of FailureCellResult)()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim bestCiWidth As Double = Double.MaxValue
            Dim bestIdx As Integer = -1
            Dim tierResults As New List(Of FailureCellResult)()
            For Each w In AnalysisConstants.HoldWindowsMinutes
                For Each thr In ThresholdsFor(tier)
                    Dim c = counts(tier)(w)(thr)
                    Dim cell As New FailureCellResult() With {
                        .VerdictTier  = tier,
                        .WindowMin    = w,
                        .AtrThreshold = thr,
                        .SampleSize   = c.N,
                        .Failures     = c.F
                    }
                    If c.N > 0 Then
                        cell.FailureRate = CDbl(c.F) / c.N
                        WilsonCI(c.F, c.N, cell.CiLow, cell.CiHigh)
                        cell.CiWidth = cell.CiHigh - cell.CiLow
                    End If
                    tierResults.Add(cell)
                    If c.N >= AnalysisConstants.MinSamplesPerCell AndAlso cell.CiWidth < bestCiWidth Then
                        bestCiWidth = cell.CiWidth
                        bestIdx = tierResults.Count - 1
                    End If
                Next
            Next
            ' Break ties by sample size then smaller window (already ordered small→large above)
            If bestIdx >= 0 Then tierResults(bestIdx).IsRecommended = True
            results.AddRange(tierResults)
        Next

        Return results
    End Function

    ' 95% Wilson score confidence interval (zsq = 1.96^2 = 3.8416)
    Public Shared Sub WilsonCI(failures As Integer, n As Integer,
                                ByRef ciLow As Double, ByRef ciHigh As Double)
        Const Zsq As Double = 3.8416
        If n = 0 Then ciLow = 0 : ciHigh = 1 : Return
        Dim p As Double = CDbl(failures) / n
        Dim denom As Double = 1.0 + Zsq / n
        Dim centre As Double = (p + Zsq / (2 * n)) / denom
        Dim margin As Double = Sqrt(p * (1 - p) / n + Zsq / (4.0 * n * n)) * 1.96 / denom
        ciLow  = Max(0.0, centre - margin)
        ciHigh = Min(1.0, centre + margin)
    End Sub

    ' Append one picked-cell entry to analysis/picked_cell_history.csv.
    ' Called once per auto-tweaker run after the recommended cell is selected.
    ' Creates the file with header if it doesn't exist yet.
    Public Shared Sub AppendPickedCell(csvPath As String, tier As String,
                                       windowMin As Integer, atrThreshold As Double)
        Try
            Dim writeHeader As Boolean = Not IO.File.Exists(csvPath)
            Using sw As New IO.StreamWriter(csvPath, append:=True)
                If writeHeader Then
                    sw.WriteLine("Timestamp,Tier,WindowMin,AtrThreshold")
                End If
                sw.WriteLine(String.Join(",",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    tier,
                    windowMin.ToString(),
                    atrThreshold.ToString("F2")))
            End Using
        Catch
            ' Best-effort write — do not abort the auto-tweaker run on CSV I/O failure.
        End Try
    End Sub

End Class
