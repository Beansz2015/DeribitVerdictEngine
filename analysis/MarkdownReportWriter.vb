' analysis/MarkdownReportWriter.vb
' Renders an AnalysisReport to a markdown string and writes it to disk.
' Also writes the summary CSV (failure-rate matrix) consumed by the auto-tweaker.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Text

Public Class MarkdownReportWriter

    Public Shared Sub Write(report As AnalysisReport, outputDir As String)
        Dim ts As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
        Dim mdPath  As String = Path.Combine(outputDir, "analysis_report_" & ts & ".md")
        Dim csvPath As String = Path.Combine(outputDir, "analysis_summary_" & ts & ".csv")

        Dim md As String = BuildMarkdown(report, ts)
        File.WriteAllText(mdPath, md)
        report.MarkdownText     = md
        report.MarkdownFilePath = mdPath

        BuildSummaryCsv(report.FailureCells, csvPath)
        report.SummaryCsvPath = csvPath
    End Sub

    Private Shared Function BuildMarkdown(r As AnalysisReport, ts As String) As String
        Dim sb As New StringBuilder()

        sb.AppendLine("# Analysis Report — " & ts)
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 1: Summary
        sb.AppendLine("## 1. Summary")
        sb.AppendLine()
        sb.AppendLine(String.Format("- Rows in CSV: **{0}**  |  Excluded (incomplete window or session boundary): **{1}**",
                                    r.TotalRows, r.ExcludedRows))
        If r.VerdictCounts.Count > 0 Then
            sb.Append("- Verdict counts: ")
            Dim parts As New List(Of String)()
            For Each kvp In r.VerdictCounts
                parts.Add(String.Format("{0}={1}", kvp.Key, kvp.Value))
            Next
            sb.AppendLine(String.Join("  |  ", parts))
        End If
        sb.AppendLine()
        ' Headline failure rates
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim best = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
            If best IsNot Nothing AndAlso best.SampleSize >= AnalysisConstants.MinSamplesPerCell Then
                sb.AppendLine(String.Format("- **{0}** best cell: {1}m window / {2:F1}× ATR  →  {3:P1} failure (n={4})",
                                            tier, best.WindowMin, best.AtrThreshold,
                                            best.FailureRate, best.SampleSize))
            Else
                sb.AppendLine(String.Format("- **{0}**: insufficient sample (< {1} rows in any cell)",
                                            tier, AnalysisConstants.MinSamplesPerCell))
            End If
        Next
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 2: Failure-Rate Matrix
        sb.AppendLine("## 2. Failure-Rate Matrix")
        sb.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            sb.AppendLine("### " & tier)
            sb.AppendLine()
            ' Header row
            Dim thrs = If(tier.StartsWith("STRONG"),
                          AnalysisConstants.StrongAtrThresholds,
                          AnalysisConstants.MediumAtrThresholds)
            Dim hdr As New StringBuilder("| Window |")
            For Each thr In thrs
                hdr.Append(String.Format(" {0:F1}× ATR |", thr))
            Next
            sb.AppendLine(hdr.ToString())
            Dim sep As New StringBuilder("|--------|")
            For Each thr In thrs
                sep.Append("------------|")
            Next
            sb.AppendLine(sep.ToString())
            For Each w In AnalysisConstants.HoldWindowsMinutes
                Dim row As New StringBuilder(String.Format("| {0,4}m  |", w))
                For Each thr In thrs
                    Dim cell = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso
                                                              c.WindowMin = w AndAlso
                                                              c.AtrThreshold = thr).FirstOrDefault()
                    If cell Is Nothing OrElse cell.SampleSize = 0 Then
                        row.Append(" n/a        |")
                    Else
                        Dim tag As String = If(cell.IsRecommended, "★ ", "  ")
                        row.Append(String.Format(" {4}{0:P0} n={1} [{2:P0}-{3:P0}] |",
                                                 cell.FailureRate, cell.SampleSize,
                                                 cell.CiLow, cell.CiHigh, tag))
                    End If
                Next
                sb.AppendLine(row.ToString())
            Next
            sb.AppendLine()
        Next

        ' ------------------------------------------------------------------ Section 3: Recommended cells
        sb.AppendLine("## 3. Recommended (window, threshold) per tier")
        sb.AppendLine()
        sb.AppendLine("★ = lowest CI width with n ≥ " & AnalysisConstants.MinSamplesPerCell)
        sb.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim best = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
            If best IsNot Nothing Then
                sb.AppendLine(String.Format("- **{0}**: {1}m window, {2:F1}× ATR  →  {3:P1} failure  CI [{4:P0}–{5:P0}]  n={6}",
                                            tier, best.WindowMin, best.AtrThreshold,
                                            best.FailureRate, best.CiLow, best.CiHigh, best.SampleSize))
            Else
                sb.AppendLine(String.Format("- **{0}**: no stable cell yet (need ≥ {1} rows per cell)",
                                            tier, AnalysisConstants.MinSamplesPerCell))
            End If
        Next
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 4: VerdictContext × Outcome
        sb.AppendLine("## 4. Verdict Context Tag × Outcome")
        sb.AppendLine()
        If r.ContextOutcomes.Count = 0 Then
            sb.AppendLine("_Insufficient data to compute per-context failure rates._")
        Else
            For Each kvp In r.ContextOutcomes
                Dim c = kvp.Value
                If c.SampleSize > 0 Then
                    sb.AppendLine(String.Format("- **{0}**: {1:P1} failure  n={2}  CI [{3:P0}–{4:P0}]",
                                                kvp.Key, c.FailureRate, c.SampleSize, c.CiLow, c.CiHigh))
                Else
                    sb.AppendLine("- **" & kvp.Key & "**: insufficient sample")
                End If
            Next
        End If
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 5: Funding Momentum Diagnostic
        sb.AppendLine("## 5. Funding Momentum Diagnostic")
        sb.AppendLine()
        Dim fd = r.FundingDiagnostic
        If fd IsNot Nothing Then
            sb.AppendLine(String.Format("- Total rows: {0}  |  Non-zero FundingDelta: {1}",
                                        fd.TotalRows, fd.NonZeroRows))
            If fd.AbsValues.Count > 0 Then
                sb.AppendLine(String.Format("- |FundingDelta| percentiles (bp):  " &
                                            "p50={0:F4}  p75={1:F4}  p90={2:F4}  p95={3:F4}",
                                            fd.Pct50 * 10000, fd.Pct75 * 10000,
                                            fd.Pct90 * 10000, fd.Pct95 * 10000))
                sb.AppendLine(String.Format("- Implied threshold for ~30% non-FLAT: {0:F4} bp",
                                            fd.ImpliedThreshold30Pct * 10000))
            End If
            sb.AppendLine("- **Recommendation:** " & fd.Recommendation)
        Else
            sb.AppendLine("_No FundingDelta data available (v0.3 schema rows only?)._")
        End If
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 6: OFI Outlier Audit
        sb.AppendLine("## 6. OFI Outlier Audit")
        sb.AppendLine()
        Dim oa = r.OfiAudit
        If oa IsNot Nothing Then
            sb.AppendLine(String.Format("- Rows with OFIRatio > 100: **{0}**  |  > 1000: **{1}**",
                                        oa.RowsAbove100, oa.RowsAbove1000))
            If oa.Top10.Count > 0 Then
                sb.AppendLine()
                sb.AppendLine("| Timestamp           | OFIRatio | BidVol | AskVol |")
                sb.AppendLine("|---------------------|----------|--------|--------|")
                For Each row In oa.Top10
                    sb.AppendLine(String.Format("| {0} | {1,8:F1} | {2,6:F0} | {3,6:F0} |",
                                                row.Timestamp, row.OfiRatio, row.OfiBidVol, row.OfiAskVol))
                Next
            End If
            sb.AppendLine()
            sb.AppendLine("**Recommendation:** " & oa.Recommendation)
        Else
            sb.AppendLine("_No OFI data available._")
        End If
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 7: OI x CVD Asymmetry
        sb.AppendLine("## 7. OI×CVD Asymmetry Audit")
        sb.AppendLine()
        Dim oc = r.OiCvdAudit
        If oc IsNot Nothing Then
            sb.AppendLine(String.Format("- Confirmed LONG: **{0}**  |  Confirmed SHORT: **{1}**",
                                        oc.TotalConfirmedLong, oc.TotalConfirmedShort))
            sb.AppendLine()
            If oc.ByRegime.Count > 0 Then
                sb.AppendLine("By Regime:")
                sb.AppendLine()
                sb.AppendLine("| Regime | Conf. LONG | Conf. SHORT |")
                sb.AppendLine("|--------|------------|-------------|")
                For Each kvp In oc.ByRegime
                    sb.AppendLine(String.Format("| {0,-20} | {1,10} | {2,11} |",
                                                kvp.Key, kvp.Value.LongCount, kvp.Value.ShortCount))
                Next
                sb.AppendLine()
            End If
            If oc.ByFundingBias.Count > 0 Then
                sb.AppendLine("By Funding Bias:")
                sb.AppendLine()
                sb.AppendLine("| Funding Bias         | Conf. LONG | Conf. SHORT |")
                sb.AppendLine("|----------------------|------------|-------------|")
                For Each kvp In oc.ByFundingBias
                    sb.AppendLine(String.Format("| {0,-20} | {1,10} | {2,11} |",
                                                kvp.Key, kvp.Value.LongCount, kvp.Value.ShortCount))
                Next
                sb.AppendLine()
            End If
            sb.AppendLine(String.Format("**Verdict:** {0}", oc.Verdict))
        Else
            sb.AppendLine("_No OI×CVD data available._")
        End If
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 8: Hold Window Stats
        sb.AppendLine("## 8. Hold Window Selection Stats")
        sb.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim best = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
            If best IsNot Nothing Then
                sb.AppendLine(String.Format("- **{0}**: recommended hold window = **{1}m**  (threshold {2:F1}× ATR)",
                                            tier, best.WindowMin, best.AtrThreshold))
            Else
                sb.AppendLine(String.Format("- **{0}**: no recommendation yet", tier))
            End If
        Next
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 9: Pending data
        sb.AppendLine("## 9. Pending data (n < " & AnalysisConstants.MinSamplesPerCell & ")")
        sb.AppendLine()
        Dim pending = r.FailureCells.Where(Function(c) c.SampleSize < AnalysisConstants.MinSamplesPerCell).ToList()
        If pending.Count = 0 Then
            sb.AppendLine("All cells have sufficient sample sizes.")
        Else
            For Each cell In pending
                sb.AppendLine(String.Format("- {0} / {1}m / {2:F1}× ATR:  n={3}  (need {4})",
                                            cell.VerdictTier, cell.WindowMin, cell.AtrThreshold,
                                            cell.SampleSize, AnalysisConstants.MinSamplesPerCell))
            Next
        End If
        sb.AppendLine()

        Return sb.ToString()
    End Function

    Private Shared Sub BuildSummaryCsv(cells As List(Of FailureCellResult), path As String)
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine("VerdictTier,WindowMin,AtrThreshold,FailureRate,SampleSize,CiLow,CiHigh,IsRecommended")
                For Each c In cells
                    sw.WriteLine(String.Join(",",
                        c.VerdictTier,
                        c.WindowMin.ToString(),
                        c.AtrThreshold.ToString("F2"),
                        c.FailureRate.ToString("F6"),
                        c.SampleSize.ToString(),
                        c.CiLow.ToString("F6"),
                        c.CiHigh.ToString("F6"),
                        c.IsRecommended.ToString()))
                Next
            End Using
        Catch
        End Try
    End Sub

End Class
