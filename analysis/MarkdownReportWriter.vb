' analysis/MarkdownReportWriter.vb
' Renders an AnalysisReport to a markdown string and writes it to disk.
' Also writes the summary CSV (flat failure-rate matrix, one row per
' population × tier × window × threshold).
'
' v2 (failure-definition-v2-proposal.md): updated Section 1 summary, interpretation
' hint at top, Section 4a Barrier-Hit Decomposition.
'
' Resolution-segmentation fix (offline-analysis-report-audit-proposal.md):
' TIER-MAJOR layout — outer loop is verdict tier, inner loop is population
' (NY×1 / LONDON×3 / ASIA×3). Every matrix / barrier / context section is rendered
' PER (session × resolution); the §5/§6/§7 diagnostics stay global (D2). The summary
' CSV gains a leading Population column. NOTE: the auto-tweaker computes its own
' matrix from FailureRateMatrix.Compute over its filtered rows — it does NOT read
' this summary CSV, so the added column is a pure artifact (verified, proposal §2.3).
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Text

Public Class MarkdownReportWriter

    Private Shared ReadOnly Tiers As String() =
        {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}

    Public Shared Sub Write(report As AnalysisReport, outputDir As String)
        Dim ts As String = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
        Dim mdPath  As String = Path.Combine(outputDir, "analysis_report_" & ts & ".md")
        Dim csvPath As String = Path.Combine(outputDir, "analysis_summary_" & ts & ".csv")

        Dim md As String = BuildMarkdown(report, ts)
        File.WriteAllText(mdPath, md)
        report.MarkdownText     = md
        report.MarkdownFilePath = mdPath

        BuildSummaryCsv(report, csvPath)
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
        sb.AppendLine(">")
        sb.AppendLine("> **Segmented by (session × resolution):** every matrix / barrier / context section")
        sb.AppendLine("> below is computed PER population (NY×1, LONDON×3, ASIA×3) — never pooled across")
        sb.AppendLine("> execution regimes, which have different ATR scales and (Asia/London) provisional")
        sb.AppendLine("> 3-min ROC thresholds. ★◆ are picked WITHIN each (tier × session) sub-table.")
        sb.AppendLine()

        AppendGlobalSummary(sb, r)
        AppendFailureMatrix(sb, r)
        AppendRecommended(sb, r)
        AppendDecomposition(sb, r)
        AppendContextOutcomes(sb, r)
        AppendHoldWindow(sb, r)
        AppendPending(sb, r)
        AppendGlobalDiagnostics(sb, r)

        Return sb.ToString()
    End Function

    ' ------------------------------------------------------------------ Global Summary
    Private Shared Sub AppendGlobalSummary(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## Global Summary")
        sb.AppendLine()
        sb.AppendLine(String.Format("- Rows in CSV: **{0}**", r.TotalRows))
        sb.AppendLine("- Forward data source: **Deribit OHLC bulk fetch** (replaces v1 CSV-close ±30s lookup)")
        If r.VerdictCounts.Count > 0 Then
            Dim parts As New List(Of String)()
            For Each kvp In r.VerdictCounts
                parts.Add(String.Format("{0}={1}", kvp.Key, kvp.Value))
            Next
            sb.AppendLine("- Verdict counts (global): " & String.Join("  |  ", parts))
        End If
        If r.Populations.Count > 0 Then
            Dim popParts As New List(Of String)()
            For Each pop In r.Populations
                popParts.Add(String.Format("{0}×{1} n={2}", pop.SessionName, pop.Resolution, pop.RowCount))
            Next
            sb.AppendLine("- Populations detected:  " & String.Join("  |  ", popParts))
        End If
        sb.AppendLine()
        ' Per-population barrier diagnostics. below-min-move=0 is expected for an
        ' all-post-v35 book (the live gate already NO-TRADE'd sub-floor directional
        ' signals before logging) — see audit §4.1, not a regression.
        sb.AppendLine("Per-population barrier diagnostics (below-min-move=0 is expected for an all-post-v35 book — audit §4.1):")
        sb.AppendLine()
        sb.AppendLine("| Population | Rows | No-OHLC excl. | ATR-invalid | Below-min-move | Adverse: structural / fallback |")
        sb.AppendLine("|-----------|------|---------------|-------------|----------------|--------------------------------|")
        For Each pop In r.Populations
            sb.AppendLine(String.Format("| {0}×{1} | {2} | {3} | {4} | {5} | {6} / {7} |",
                                        pop.SessionName, pop.Resolution, pop.RowCount,
                                        pop.ExcludedRows, pop.AtrInvalidExcluded, pop.BelowMinMoveExcluded,
                                        pop.StructuralStopRows, pop.AtrFallbackRows))
        Next
        sb.AppendLine()
    End Sub

    ' ------------------------------------------------------------------ Section 2: Failure-Rate Matrix
    Private Shared Sub AppendFailureMatrix(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 2. Failure-Rate Matrix")
        sb.AppendLine()
        sb.AppendLine("_Failure = adverse barrier hit first OR window expired without favourable hit. " &
                      "Segmented per (tier × session); ★◆ picked WITHIN each sub-table._")
        sb.AppendLine()
        For Each tier In Tiers
            sb.AppendLine("### " & tier)
            sb.AppendLine()
            For Each pop In r.Populations
                sb.AppendLine(SubTableHeader(pop, tier))
                sb.AppendLine()
                If TierHasRows(pop.FailureCells, tier) Then
                    AppendMatrixGrid(sb, pop.FailureCells, tier)
                    sb.AppendLine()
                End If
            Next
        Next
    End Sub

    Private Shared Sub AppendMatrixGrid(sb As StringBuilder, cells As List(Of FailureCellResult), tier As String)
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
                Dim cell = cells.Where(Function(c) c.VerdictTier = tier AndAlso
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
    End Sub

    ' ------------------------------------------------------------------ Section 3: Recommended cells
    Private Shared Sub AppendRecommended(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 3. Recommended (window, threshold) — per (tier × session)")
        sb.AppendLine()
        sb.AppendLine("Two views per tier, both require n ≥ " & AnalysisConstants.MinSamplesPerCell & ":")
        sb.AppendLine("- ★ **Most-precise estimate** — lowest CI width. Used by the auto-tweaker to decide if the current settings are failing. Wilson CI narrows at extreme p, so this can pick the WORST cell when failure rates are high — that's working as designed.")
        sb.AppendLine("- ◆ **Lowest failure rate** — best actual trade outcome. The cell to look at when deciding whether the verdict is worth taking discretionarily.")
        sb.AppendLine()
        For Each tier In Tiers
            sb.AppendLine("### " & tier)
            For Each pop In r.Populations
                sb.AppendLine("- **" & PopLabel(pop) & "**: " & RecommendedText(pop.FailureCells, tier))
            Next
            sb.AppendLine()
        Next
    End Sub

    Private Shared Function RecommendedText(cells As List(Of FailureCellResult), tier As String) As String
        Dim precise = cells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
        Dim profit  = cells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsMostProfitable).FirstOrDefault()
        If precise Is Nothing Then
            Return String.Format("no stable cell yet (need ≥ {0} rows per cell)", AnalysisConstants.MinSamplesPerCell)
        ElseIf profit IsNot Nothing AndAlso
               (precise.WindowMin <> profit.WindowMin OrElse precise.AtrThreshold <> profit.AtrThreshold) Then
            Return String.Format("★ {0}m, {1:F1}× ATR → {2:P1} failure CI [{3:P0}–{4:P0}] n={5}  |  ◆ {6}m, {7:F1}× ATR → {8:P1} failure CI [{9:P0}–{10:P0}] n={11}",
                                 precise.WindowMin, precise.AtrThreshold, precise.FailureRate, precise.CiLow, precise.CiHigh, precise.SampleSize,
                                 profit.WindowMin, profit.AtrThreshold, profit.FailureRate, profit.CiLow, profit.CiHigh, profit.SampleSize)
        Else
            Return String.Format("★◆ {0}m, {1:F1}× ATR → {2:P1} failure CI [{3:P0}–{4:P0}] n={5}",
                                 precise.WindowMin, precise.AtrThreshold, precise.FailureRate, precise.CiLow, precise.CiHigh, precise.SampleSize)
        End If
    End Function

    ' ------------------------------------------------------------------ Section 4a: Barrier-Hit Decomposition
    Private Shared Sub AppendDecomposition(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 4a. Barrier-Hit Decomposition — per (tier × session)")
        sb.AppendLine()
        sb.AppendLine("How failures occurred within each cell (counts, not %). Ambiguous = both barriers in same 1m bar (counts as failure).")
        sb.AppendLine()
        For Each tier In Tiers
            sb.AppendLine("### " & tier)
            sb.AppendLine()
            For Each pop In r.Populations
                If Not TierHasRows(pop.FailureCells, tier) Then
                    sb.AppendLine(String.Format("#### {0} · {1}-min · (no {2} rows yet)", pop.SessionName, pop.Resolution, tier))
                    sb.AppendLine()
                    Continue For
                End If
                sb.AppendLine(String.Format("#### {0} · {1}-min", pop.SessionName, pop.Resolution))
                sb.AppendLine()
                Dim thrs = If(tier.StartsWith("STRONG"),
                              AnalysisConstants.StrongAtrThresholds,
                              AnalysisConstants.MediumAtrThresholds)
                sb.AppendLine("| Window | ATR× | n | Success | Adverse Hit | Window Expiry | Ambiguous |")
                sb.AppendLine("|--------|------|---|---------|-------------|---------------|-----------|")
                For Each w In AnalysisConstants.HoldWindowsMinutes
                    For Each thr In thrs
                        Dim cell = pop.FailureCells.Where(Function(c) c.VerdictTier = tier AndAlso
                                                                      c.WindowMin = w AndAlso
                                                                      c.AtrThreshold = thr).FirstOrDefault()
                        If cell Is Nothing OrElse cell.SampleSize = 0 Then
                            sb.AppendLine(String.Format("| {0,4}m | {1:F1}× | — | — | — | — | — |", w, thr))
                        Else
                            sb.AppendLine(String.Format("| {0,4}m | {1:F1}× | {2} | {3} | {4} | {5} | {6} |",
                                                         w, thr,
                                                         cell.SampleSize, cell.Successes,
                                                         cell.AdverseHitFails, cell.WindowExpiryFails, cell.AmbiguousFails))
                        End If
                    Next
                Next
                sb.AppendLine()
            Next
        Next
    End Sub

    ' ------------------------------------------------------------------ Section 4: VerdictContext × Outcome
    Private Shared Sub AppendContextOutcomes(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 4. Verdict Context Tag × Outcome — per session")
        sb.AppendLine()
        sb.AppendLine("_Barrier-based, so segmented per session; each uses that session's recommended cell geometry._")
        sb.AppendLine()
        For Each pop In r.Populations
            sb.AppendLine("### " & PopLabel(pop))
            If pop.ContextOutcomes.Count = 0 Then
                sb.AppendLine("_Insufficient data to compute per-context failure rates._")
            Else
                For Each kvp In pop.ContextOutcomes
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
        Next
    End Sub

    ' ------------------------------------------------------------------ Section 8: Hold Window Stats
    Private Shared Sub AppendHoldWindow(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 8. Hold Window Selection Stats — per (tier × session)")
        sb.AppendLine()
        For Each tier In Tiers
            sb.AppendLine("### " & tier)
            For Each pop In r.Populations
                sb.AppendLine("- **" & PopLabel(pop) & "**: " & HoldWindowText(pop.FailureCells, tier))
            Next
            sb.AppendLine()
        Next
    End Sub

    Private Shared Function HoldWindowText(cells As List(Of FailureCellResult), tier As String) As String
        Dim precise = cells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsRecommended).FirstOrDefault()
        Dim profit  = cells.Where(Function(c) c.VerdictTier = tier AndAlso c.IsMostProfitable).FirstOrDefault()
        If precise Is Nothing Then
            Return "no recommendation yet"
        ElseIf profit IsNot Nothing AndAlso precise.WindowMin <> profit.WindowMin Then
            Return String.Format("★ hold = **{0}m** ({1:F1}× ATR)  |  ◆ hold = **{2}m** ({3:F1}× ATR)",
                                 precise.WindowMin, precise.AtrThreshold, profit.WindowMin, profit.AtrThreshold)
        Else
            Return String.Format("★◆ hold = **{0}m** (threshold {1:F1}× ATR)", precise.WindowMin, precise.AtrThreshold)
        End If
    End Function

    ' ------------------------------------------------------------------ Section 9: Pending data
    Private Shared Sub AppendPending(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 9. Pending data (n < " & AnalysisConstants.MinSamplesPerCell & ") — per (tier × session)")
        sb.AppendLine()
        Dim anyPending As Boolean = False
        For Each pop In r.Populations
            Dim pending = pop.FailureCells.Where(Function(c) c.SampleSize < AnalysisConstants.MinSamplesPerCell).ToList()
            If pending.Count = 0 Then Continue For
            anyPending = True
            sb.AppendLine("### " & PopLabel(pop))
            For Each cell In pending
                sb.AppendLine(String.Format("- {0} / {1}m / {2:F1}× ATR:  n={3}  (need {4})",
                                            cell.VerdictTier, cell.WindowMin, cell.AtrThreshold,
                                            cell.SampleSize, AnalysisConstants.MinSamplesPerCell))
            Next
            sb.AppendLine()
        Next
        If Not anyPending Then
            sb.AppendLine("All cells have sufficient sample sizes.")
            sb.AppendLine()
        End If
    End Sub

    ' ------------------------------------------------------------------ Global Diagnostics (§5/§6/§7)
    Private Shared Sub AppendGlobalDiagnostics(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## Global Diagnostics")
        sb.AppendLine()
        sb.AppendLine("_Not segmented (proposal D2): book-wide, resolution-independent._")
        sb.AppendLine()

        ' § 5 Funding Momentum Diagnostic
        sb.AppendLine("### 5. Funding Momentum Diagnostic")
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

        ' § 6 OFI Outlier Audit
        sb.AppendLine("### 6. OFI Outlier Audit")
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

        ' § 7 OI×CVD Asymmetry Audit
        sb.AppendLine("### 7. OI×CVD Asymmetry Audit")
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
    End Sub

    ' ------------------------------------------------------------------ shared render helpers
    Private Shared Function PopLabel(pop As PopulationReport) As String
        Return pop.SessionName & "×" & pop.Resolution.ToString()
    End Function

    Private Shared Function TierHasRows(cells As List(Of FailureCellResult), tier As String) As Boolean
        Return cells.Any(Function(c) c.VerdictTier = tier AndAlso c.SampleSize > 0)
    End Function

    ' Per-session sub-table header: resolution label + directional-row ATR caption +
    ' $ move-floor (proposal §2.4 req 2/3), or a "(no rows yet)" line when this tier
    ' has no rows in this population.
    Private Shared Function SubTableHeader(pop As PopulationReport, tier As String) As String
        Dim res As String = pop.Resolution.ToString() & "-min"
        If Not TierHasRows(pop.FailureCells, tier) Then
            Return String.Format("#### {0} · {1} · (no {2} rows yet)", pop.SessionName, res, tier)
        End If
        Return String.Format("#### {0} · {1} · ATR p50={2:F0} (p25–p75 {3:F0}–{4:F0}) · move-floor ${5:F0}",
                             pop.SessionName, res, pop.DirAtrP50, pop.DirAtrP25, pop.DirAtrP75, pop.MoveFloorUsd)
    End Function

    Private Shared Sub BuildSummaryCsv(report As AnalysisReport, path As String)
        Try
            Using sw As New StreamWriter(path, append:=False)
                sw.WriteLine("Population,VerdictTier,WindowMin,AtrThreshold,FailureRate,SampleSize,CiLow,CiHigh,IsRecommended,IsMostProfitable")
                For Each pop In report.Populations
                    For Each c In pop.FailureCells
                        sw.WriteLine(String.Join(",",
                            pop.PopulationKey,
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
                Next
            End Using
        Catch
        End Try
    End Sub

End Class
