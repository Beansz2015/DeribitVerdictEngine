' analysis/MarkdownReportWriter.vb
' Renders an AnalysisReport to a markdown string and writes it to disk.
' Also writes the summary CSV (failure-rate matrix) consumed by the auto-tweaker.
'
' v2 (failure-definition-v2-proposal.md): updated Section 1 summary, interpretation
' hint at top, Section 4a Barrier-Hit Decomposition.
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

    ' Write a minimal error-banner report when the OHLC fetch fails.
    ' The caller (AnalysisRunner) has already set report.MarkdownText.
    Public Shared Sub WriteErrorBanner(report As AnalysisReport, outputDir As String)
        Try
            Dim ts      As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
            Dim mdPath  As String = Path.Combine(outputDir, "analysis_report_" & ts & ".md")
            File.WriteAllText(mdPath, report.MarkdownText)
            report.MarkdownFilePath = mdPath
        Catch
        End Try
    End Sub

    Private Shared Function BuildMarkdown(r As AnalysisReport, ts As String) As String
        Dim sb As New StringBuilder()

        sb.AppendLine("# Analysis Report — " & ts)
        sb.AppendLine()

        ' Interpretation hint (static — describes v2 barrier-hit semantics).
        sb.AppendLine("> **Failure model: barrier-hit with adverse stop (v2)**")
        sb.AppendLine("> - **SUCCESS** = price wicked through favourable barrier (entry ± multiplier × ATR)")
        sb.AppendLine(">   within the hold window, before any adverse hit.")
        sb.AppendLine("> - **FAILURE** = adverse barrier (structural stop or 1.2×ATR fallback) hit first,")
        sb.AppendLine(">   OR window expired without favourable hit.")
        sb.AppendLine("> - Same-bar and next-bar after verdict are excluded (too quick to execute).")
        sb.AppendLine("> - Ambiguous bars (both barriers touched in same 1m candle) count as failure.")
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 1: Summary
        sb.AppendLine("## 1. Summary")
        sb.AppendLine()
        sb.AppendLine(String.Format("- Rows in CSV: **{0}**  |  Excluded (no OHLC bars for any window): **{1}**",
                                    r.TotalRows, r.ExcludedRows))
        sb.AppendLine(String.Format("- ATR-invalid rows excluded: **{0}**", r.AtrInvalidExcluded))
        sb.AppendLine(String.Format("- Below-min-tradeable-move rows excluded (v35 gate-killed; engine target < floor): **{0}**", r.BelowMinMoveExcluded))
        sb.AppendLine(String.Format("- Adverse barrier source: structural stop **{0}** rows  /  ATR fallback **{1}** rows  /  row excluded **{2}** rows",
                                    r.StructuralStopRows, r.AtrFallbackRows, r.AtrInvalidExcluded))
        sb.AppendLine("- Forward data source: **Deribit OHLC bulk fetch** (replaces v1 CSV-close ±30s lookup)")
        sb.AppendLine()
        If r.VerdictCounts.Count > 0 Then
            sb.Append("- Verdict counts: ")
            Dim parts As New List(Of String)()
            For Each kvp In r.VerdictCounts
                parts.Add(String.Format("{0}={1}", kvp.Key, kvp.Value))
            Next
            sb.AppendLine(String.Join("  |  ", parts))
        End If
        sb.AppendLine()
        ' Headline failure rates per tier. Dual recommendation:
        '   ★ most-precise estimate (lowest CI width — auto-tweaker view)
        '   ◆ lowest failure rate (trader view — best actual trade outcome)
        ' Same cell can satisfy both; in that case rendered as ★◆.
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim precise = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
            Dim profit  = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsMostProfitable).FirstOrDefault()
            If precise Is Nothing OrElse precise.SampleSize < AnalysisConstants.MinSamplesPerCell Then
                sb.AppendLine(String.Format("- **{0}**: insufficient sample (< {1} rows in any cell)",
                                            tier, AnalysisConstants.MinSamplesPerCell))
            ElseIf profit IsNot Nothing AndAlso
                   (precise.WindowMin <> profit.WindowMin OrElse precise.AtrThreshold <> profit.AtrThreshold) Then
                sb.AppendLine(String.Format("- **{0}**: ★ {1}m / {2:F1}× ATR = {3:P0} (n={4})  |  ◆ {5}m / {6:F1}× ATR = {7:P0} (n={8})",
                                            tier,
                                            precise.WindowMin, precise.AtrThreshold, precise.FailureRate, precise.SampleSize,
                                            profit.WindowMin,  profit.AtrThreshold,  profit.FailureRate,  profit.SampleSize))
            Else
                sb.AppendLine(String.Format("- **{0}**: ★◆ {1}m / {2:F1}× ATR = {3:P1} failure (n={4})",
                                            tier, precise.WindowMin, precise.AtrThreshold,
                                            precise.FailureRate, precise.SampleSize))
            End If
        Next
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 2: Failure-Rate Matrix
        sb.AppendLine("## 2. Failure-Rate Matrix")
        sb.AppendLine()
        sb.AppendLine("_Failure = adverse barrier hit first OR window expired without favourable hit._")
        sb.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            sb.AppendLine("### " & tier)
            sb.AppendLine()
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
                        Dim tag As String
                        If cell.IsRecommended AndAlso cell.IsMostProfitable Then
                            tag = "★◆"
                        ElseIf cell.IsRecommended Then
                            tag = "★ "
                        ElseIf cell.IsMostProfitable Then
                            tag = "◆ "
                        Else
                            tag = "  "
                        End If
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
        sb.AppendLine("Two views per tier, both require n ≥ " & AnalysisConstants.MinSamplesPerCell & ":")
        sb.AppendLine("- ★ **Most-precise estimate** — lowest CI width. Used by the auto-tweaker to decide if the current settings are failing. Wilson CI narrows at extreme p, so this can pick the WORST cell when failure rates are high — that's working as designed.")
        sb.AppendLine("- ◆ **Lowest failure rate** — best actual trade outcome. The cell to look at when deciding whether the verdict is worth taking discretionarily.")
        sb.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim precise = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
            Dim profit  = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsMostProfitable).FirstOrDefault()
            If precise Is Nothing Then
                sb.AppendLine(String.Format("- **{0}**: no stable cell yet (need ≥ {1} rows per cell)",
                                            tier, AnalysisConstants.MinSamplesPerCell))
            ElseIf profit IsNot Nothing AndAlso
                   (precise.WindowMin <> profit.WindowMin OrElse precise.AtrThreshold <> profit.AtrThreshold) Then
                sb.AppendLine(String.Format("- **{0}**:", tier))
                sb.AppendLine(String.Format("    - ★ {0}m, {1:F1}× ATR  →  {2:P1} failure  CI [{3:P0}–{4:P0}]  n={5}",
                                            precise.WindowMin, precise.AtrThreshold,
                                            precise.FailureRate, precise.CiLow, precise.CiHigh, precise.SampleSize))
                sb.AppendLine(String.Format("    - ◆ {0}m, {1:F1}× ATR  →  {2:P1} failure  CI [{3:P0}–{4:P0}]  n={5}",
                                            profit.WindowMin, profit.AtrThreshold,
                                            profit.FailureRate, profit.CiLow, profit.CiHigh, profit.SampleSize))
            Else
                sb.AppendLine(String.Format("- **{0}** (both views agree): ★◆ {1}m, {2:F1}× ATR  →  {3:P1} failure  CI [{4:P0}–{5:P0}]  n={6}",
                                            tier, precise.WindowMin, precise.AtrThreshold,
                                            precise.FailureRate, precise.CiLow, precise.CiHigh, precise.SampleSize))
            End If
        Next
        sb.AppendLine()

        ' ------------------------------------------------------------------ Section 4a: Barrier-Hit Decomposition
        sb.AppendLine("## 4a. Barrier-Hit Decomposition")
        sb.AppendLine()
        sb.AppendLine("How failures occurred within each cell (counts, not %). Ambiguous = both barriers in same 1m bar (counts as failure).")
        sb.AppendLine()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            sb.AppendLine("### " & tier)
            sb.AppendLine()
            Dim thrs = If(tier.StartsWith("STRONG"),
                          AnalysisConstants.StrongAtrThresholds,
                          AnalysisConstants.MediumAtrThresholds)
            sb.AppendLine("| Window | ATR× | n | Success | Adverse Hit | Window Expiry | Ambiguous |")
            sb.AppendLine("|--------|------|---|---------|-------------|---------------|-----------|")
            For Each w In AnalysisConstants.HoldWindowsMinutes
                For Each thr In thrs
                    Dim cell = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso
                                                              c.WindowMin = w AndAlso
                                                              c.AtrThreshold = thr).FirstOrDefault()
                    If cell Is Nothing OrElse cell.SampleSize = 0 Then
                        sb.AppendLine(String.Format("| {0,4}m | {1:F1}× | — | — | — | — | — |", w, thr))
                    Else
                        sb.AppendLine(String.Format("| {0,4}m | {1:F1}× | {2} | {3} | {4} | {5} | {6} |",
                                                     w, thr,
                                                     cell.SampleSize,
                                                     cell.Successes,
                                                     cell.AdverseHitFails,
                                                     cell.WindowExpiryFails,
                                                     cell.AmbiguousFails))
                    End If
                Next
            Next
            sb.AppendLine()
        Next

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
            Dim precise = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
            Dim profit  = r.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsMostProfitable).FirstOrDefault()
            If precise Is Nothing Then
                sb.AppendLine(String.Format("- **{0}**: no recommendation yet", tier))
            ElseIf profit IsNot Nothing AndAlso precise.WindowMin <> profit.WindowMin Then
                sb.AppendLine(String.Format("- **{0}**: ★ hold = **{1}m** ({2:F1}× ATR)  |  ◆ hold = **{3}m** ({4:F1}× ATR)",
                                            tier, precise.WindowMin, precise.AtrThreshold,
                                            profit.WindowMin, profit.AtrThreshold))
            Else
                sb.AppendLine(String.Format("- **{0}**: ★◆ hold = **{1}m** (threshold {2:F1}× ATR)",
                                            tier, precise.WindowMin, precise.AtrThreshold))
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
                sw.WriteLine("VerdictTier,WindowMin,AtrThreshold,FailureRate,SampleSize,CiLow,CiHigh,IsRecommended,IsMostProfitable")
                For Each c In cells
                    sw.WriteLine(String.Join(",",
                        c.VerdictTier,
                        c.WindowMin.ToString(),
                        c.AtrThreshold.ToString("F2"),
                        c.FailureRate.ToString("F6"),
                        c.SampleSize.ToString(),
                        c.CiLow.ToString("F6"),
                        c.CiHigh.ToString("F6"),
                        c.IsRecommended.ToString(),
                        c.IsMostProfitable.ToString()))
                Next
            End Using
        Catch
        End Try
    End Sub

End Class
