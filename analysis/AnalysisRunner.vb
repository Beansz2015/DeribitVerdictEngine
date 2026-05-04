' analysis/AnalysisRunner.vb
' Host-agnostic entry point for the offline analysis pipeline.
' Called from MainForm via lnkAnalysisReport click handler.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq

Public Class AnalysisRunner

    ' Run the full analysis pipeline against the given v0.4 CSV file.
    ' Writes markdown + summary CSV to outputDir and populates report.MarkdownText.
    Public Shared Function Run(csvPath As String,
                               outputDir As String,
                               cfg As EngineSettings) As AnalysisReport
        Dim report As New AnalysisReport()

        ' Build session-start hour set from cfg
        Dim sessionStarts As New List(Of Integer)()
        If cfg.SessionVolume IsNot Nothing AndAlso cfg.SessionVolume.Sessions IsNot Nothing Then
            For Each s In cfg.SessionVolume.Sessions
                sessionStarts.Add(s.StartHour)
            Next
        End If

        ' Load and join forward returns
        Dim rows As List(Of CsvRow) = ForwardReturnJoiner.Load(csvPath, sessionStarts)
        report.TotalRows = rows.Count

        ' Count excluded rows (any window invalid — approximate: count rows with no valid window)
        report.ExcludedRows = rows.Where(
            Function(r) Not AnalysisConstants.HoldWindowsMinutes.Any(
                Function(w) r.WindowValid.ContainsKey(w) AndAlso r.WindowValid(w))).Count()

        ' Verdict counts
        For Each row In rows
            Dim v As String = If(row.Verdict, "UNKNOWN")
            If Not report.VerdictCounts.ContainsKey(v) Then report.VerdictCounts(v) = 0
            report.VerdictCounts(v) += 1
        Next

        ' Failure-rate matrix
        report.FailureCells = FailureRateMatrix.Compute(rows)

        ' VerdictContext cross-tab: for each context tag, compute failure rate at recommended window
        Dim recCells = report.FailureCells.Where(Function(c) c.IsRecommended).ToList()
        For Each ctx In {"CONFIRMED", "FLOW_UNCONFIRMED", "MOMENTUM_FADING", "STRUCTURALLY_WEAK"}
            Dim ctxRows = rows.Where(Function(r) String.Equals(r.VerdictContext, ctx,
                                                               StringComparison.OrdinalIgnoreCase) AndAlso
                                              r.Verdict <> "" AndAlso
                                              r.Verdict.ToUpper() <> "NO TRADE" AndAlso
                                              Not r.Verdict.ToUpper().StartsWith("WEAK")).ToList()
            If ctxRows.Count = 0 Then Continue For

            ' Use the recommended cell for the matching tier if available, else first STRONG window
            Dim cell As New FailureCellResult() With {.VerdictTier = ctx}
            Dim w As Integer = 10 ' default window if no recommended cell
            Dim thr As Double = 0.5

            Dim recCell = recCells.FirstOrDefault()
            If recCell IsNot Nothing Then w = recCell.WindowMin : thr = recCell.AtrThreshold

            Dim n As Integer = 0, f As Integer = 0
            For Each row In ctxRows
                Dim tierStr As String = If(row.Verdict.ToUpper().Contains("LONG"), "LONG", "SHORT")
                If Not row.WindowValid.ContainsKey(w) OrElse Not row.WindowValid(w) Then Continue For
                Dim priceDiff = row.ForwardPrice(w) - row.Price
                Dim isFailure As Boolean = If(tierStr = "LONG",
                                              priceDiff < -thr * row.ATR,
                                              priceDiff > +thr * row.ATR)
                n += 1
                If isFailure Then f += 1
            Next
            cell.SampleSize  = n
            cell.Failures    = f
            cell.FailureRate = If(n > 0, CDbl(f) / n, 0)
            If n > 0 Then FailureRateMatrix.WilsonCI(f, n, cell.CiLow, cell.CiHigh)
            report.ContextOutcomes(ctx) = cell
        Next

        ' Funding momentum diagnostic
        report.FundingDiagnostic = FundingMomentumDiagnostic.Compute(rows)

        ' OFI outlier audit
        report.OfiAudit = OutlierAudit.ComputeOfi(rows)

        ' OI×CVD asymmetry audit
        report.OiCvdAudit = OutlierAudit.ComputeOiCvdAsymmetry(rows)

        ' Render and write
        MarkdownReportWriter.Write(report, outputDir)

        Return report
    End Function

End Class
