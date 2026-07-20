' analysis/MarkdownReportWriter.vb
' Renders an AnalysisReport to a markdown string and writes it to disk.
' Also writes the summary CSV (flat failure-rate matrix, one row per
' population × tier × window).
'
' v2 (failure-definition-v2-proposal.md): updated Section 1 summary, interpretation
' hint at top, Barrier-Hit Decomposition section.
'
' [placed-target migration 2026-07-21, offline-matrix-placed-target-proposal.md]
' The ATR-threshold column is gone from every grid — one placed-geometry column per
' (tier × session × window). Same pass: sections renumbered sequentially (the old
' 2 / D6 / 3 / 4a / 4 / 8 / 9 ordering had drifted out of order) and the funding
' diagnostic's canned recommendation refreshed off its two-eras-stale REST framing.
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

        ' Matrix re-base note (M5) — dated, change_log-style, so a reader comparing this
        ' report against one generated before 2026-07-21 knows the yardstick moved.
        sb.AppendLine("> **Matrix re-based 2026-07-21: placed-vs-placed.** The favourable barrier is now the")
        sb.AppendLine("> logged **PlacedTarget** (it was a synthetic per-tier ATR grid), joining the adverse")
        sb.AppendLine("> side which moved to **PlacedStop** at D6. The offline matrix therefore measures the")
        sb.AppendLine("> same thing as the live tracker, the D4 re-walk and the what-if runner — the geometry")
        sb.AppendLine("> the engine actually emitted. The retired grid ({0.5,0.8} STRONG / {0.3,0.5} MEDIUM)")
        sb.AppendLine("> was anchored at ATR≈115 and had gone degenerate: at ATR≈44 every column sat below")
        sb.AppendLine("> the min-move floor and collapsed onto one barrier. Threshold sweeping now belongs")
        sb.AppendLine("> to the what-if runner, which does it with EV and a split-half holdout.")
        sb.AppendLine("> **Failure rates are not comparable across the re-base** — targets moved from a")
        sb.AppendLine("> fixed fraction of ATR to real structural placements.")
        sb.AppendLine()

        ' Interpretation hint (static — v2 barrier-hit semantics on placed geometry).
        sb.AppendLine("> **Failure model: barrier-hit, placed target vs placed stop (v2)**")
        sb.AppendLine("> - **SUCCESS** = price wicked through the placed target within the hold window,")
        sb.AppendLine(">   before any adverse hit.")
        sb.AppendLine("> - **FAILURE** = placed stop hit first, OR window expired without a target hit.")
        sb.AppendLine("> - **Both barriers are the logged placed levels** — `PlacedTarget{Long,Short}` /")
        sb.AppendLine(">   `PlacedStop{Long,Short}`, what `SignalEmitter.ComputeSideLevels` emitted for that")
        sb.AppendLine(">   row. Pre-v0.8 rows carry neither, keep the legacy swing-else-ATR formula on both")
        sb.AppendLine(">   sides, and are labelled **[LEGACY_YARDSTICK]**. The stop side moved from the raw")
        sb.AppendLine(">   ~9×ATR swing to the executed 1.6×ATR clamp, so failure rates rise materially vs")
        sb.AppendLine(">   the pre-D6 book — see the **before/after** section.")
        sb.AppendLine("> - The placed target is used **unfloored**: the live min-move gate already vetted")
        sb.AppendLine(">   that exact price, so flooring it here would re-create the collapse above.")
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
        AppendD4Comparison(sb, r)
        AppendRecommended(sb, r)
        AppendDecomposition(sb, r)
        AppendContextOutcomes(sb, r)
        AppendHoldWindow(sb, r)
        AppendPending(sb, r)
        AppendGlobalDiagnostics(sb, r)

        Return sb.ToString()
    End Function

    ' ------------------------------------------------------------------ Section 1: Global Summary
    Private Shared Sub AppendGlobalSummary(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 1. Global Summary")
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
                popParts.Add(String.Format("{0} n={1}", PopLabel(pop), pop.RowCount))
            Next
            sb.AppendLine("- Populations detected:  " & String.Join("  |  ", popParts))
        End If
        sb.AppendLine()
        ' Per-population barrier diagnostics. below-min-move=0 is expected for an
        ' all-post-v35 book (the live gate already NO-TRADE'd sub-floor directional
        ' signals before logging) — see audit §4.1, not a regression.
        sb.AppendLine("Per-population barrier diagnostics (below-min-move=0 is expected for an all-post-v35 book — audit §4.1).")
        sb.AppendLine("For PLACED populations, the barrier columns count rows scored on the logged placed level")
        sb.AppendLine("(``PlacedStop*`` / ``PlacedTarget*``) vs the legacy fallback path — a non-zero fallback count")
        sb.AppendLine("inside a PLACED population means that side's level was absent on some rows, not silent mixing.")
        sb.AppendLine()
        sb.AppendLine("| Population | Rows | No-OHLC excl. | ATR-invalid | Below-min-move | Adverse: placed / fallback | Favourable: placed / fallback |")
        sb.AppendLine("|-----------|------|---------------|-------------|----------------|----------------------------|-------------------------------|")
        For Each pop In r.Populations
            sb.AppendLine(String.Format("| {0} | {1} | {2} | {3} | {4} | {5} / {6} | {7} / {8} |",
                                        PopLabel(pop), pop.RowCount,
                                        pop.ExcludedRows, pop.AtrInvalidExcluded, pop.BelowMinMoveExcluded,
                                        pop.StructuralStopRows, pop.AtrFallbackRows,
                                        pop.PlacedTargetRows, pop.LegacyFavourableRows))
        Next
        sb.AppendLine()
    End Sub

    ' ------------------------------------------------------------------ Section 2: Failure-Rate Matrix
    Private Shared Sub AppendFailureMatrix(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 2. Failure-Rate Matrix")
        sb.AppendLine()
        sb.AppendLine("_Failure = placed stop hit first OR window expired without a placed-target hit. " &
                      "Segmented per (tier × session); ★◆ picked WITHIN each sub-table._")
        sb.AppendLine()
        sb.AppendLine("_One placed-geometry column: the barriers are the row's own emitted levels, so the " &
                      "only free dimension left is the hold horizon._")
        sb.AppendLine()
        sb.AppendLine("_Hold windows are resolution-scaled for bar-count parity: NY×1 = 5/10/15m, " &
                      "3-min Asia/London = 15/30/45m (= 5/10/15 three-minute bars)._")
        sb.AppendLine()
        For Each tier In Tiers
            sb.AppendLine("### " & tier)
            sb.AppendLine()
            For Each pop In r.Populations
                sb.AppendLine(SubTableHeader(pop, tier))
                sb.AppendLine()
                If TierHasRows(pop.FailureCells, tier) Then
                    AppendMatrixGrid(sb, pop.FailureCells, tier, pop.Resolution)
                    sb.AppendLine()
                End If
            Next
        Next
    End Sub

    ' resolution scales the Window-column rows: NY×1 shows 5/10/15m, the 3-min tables
    ' show 15/30/45m (three-min-hold-window-recalibration-proposal.md §4).
    ' One placed-geometry column since the placed-target migration.
    Private Shared Sub AppendMatrixGrid(sb As StringBuilder, cells As List(Of FailureCellResult), tier As String, resolution As Integer)
        sb.AppendLine("| Window | Placed geometry |")
        sb.AppendLine("|--------|-----------------|")
        For Each w In AnalysisConstants.HoldWindowsForResolution(resolution)
            Dim row As New StringBuilder(String.Format("| {0,4}m  |", w))
            Dim cell = FindCell(cells, tier, w)
            If cell Is Nothing OrElse cell.SampleSize = 0 Then
                row.Append(" n/a             |")
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
            sb.AppendLine(row.ToString())
        Next
    End Sub

    ' ------------------------------------------------------------------ Section 3: Placed-Geometry Migration before/after
    ' [D6/D4] Public wrapper so the harness (A27c/A32c) can assert the section renders both
    ' the legacy and the placed failure numbers for the same rows.
    Public Shared Function BuildD4Section(r As AnalysisReport) As String
        Dim sb As New StringBuilder()
        AppendD4Comparison(sb, r)
        Return sb.ToString()
    End Function

    ' The D4 continuity bridge: the SAME rows re-walked under the legacy barrier formula
    ' (before) and the placed levels (after), per session × resolution × tier.
    ' One before→after column since the placed-target migration.
    Private Shared Sub AppendD4Comparison(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 3. Placed-Geometry Migration — before/after failure rates")
        sb.AppendLine()
        sb.AppendLine("_The SAME rows walked twice. **before** = the legacy formula on both sides — raw-swing " &
                      "adverse (median ~9×ATR, essentially unreachable intrabar, so 'failure' collapsed to " &
                      "window-expiry) against an engine-target favourable; **after** = the placed levels the " &
                      "engine emits and the autotrader executes (~1.6×ATR stop clamp, structural target). " &
                      "Each cell shows `before% → after% (Δ)` at the population's own hold windows. Rising " &
                      "failure rates are the honest re-base — stop-outs become recordable._")
        sb.AppendLine()
        For Each tier In Tiers
            sb.AppendLine("### " & tier)
            sb.AppendLine()
            For Each pop In r.Populations
                Dim hasRows As Boolean = TierHasRows(pop.FailureCells, tier) OrElse
                                         TierHasRows(pop.LegacyFailureCells, tier)
                If Not hasRows Then
                    sb.AppendLine(String.Format("#### {0} · {1}-min · (no {2} rows yet)",
                                                PopLabel(pop), pop.Resolution, tier))
                    sb.AppendLine()
                    Continue For
                End If
                sb.AppendLine(String.Format("#### {0} · {1}-min", PopLabel(pop), pop.Resolution))
                sb.AppendLine()
                AppendD4Grid(sb, pop, tier)
                sb.AppendLine()
            Next
        Next
    End Sub

    ' before% → after% (Δ) per window, mirroring the main matrix grid shape.
    Private Shared Sub AppendD4Grid(sb As StringBuilder, pop As PopulationReport, tier As String)
        sb.AppendLine("| Window | Placed geometry (before→after Δ) |")
        sb.AppendLine("|--------|----------------------------------|")
        For Each w In AnalysisConstants.HoldWindowsForResolution(pop.Resolution)
            Dim rowSb As New StringBuilder(String.Format("| {0,4}m  |", w))
            Dim before = FindCell(pop.LegacyFailureCells, tier, w)
            Dim after  = FindCell(pop.FailureCells, tier, w)
            If after Is Nothing OrElse after.SampleSize = 0 Then
                rowSb.Append(" n/a                              |")
            Else
                Dim beforeRate As Double = If(before IsNot Nothing AndAlso before.SampleSize > 0,
                                              before.FailureRate, after.FailureRate)
                Dim delta As Double = after.FailureRate - beforeRate
                rowSb.Append(String.Format(" {0:P0} → {1:P0} ({2:+0%;-0%;0%}) n={3} |",
                                           beforeRate, after.FailureRate, delta, after.SampleSize))
            End If
            sb.AppendLine(rowSb.ToString())
        Next
    End Sub

    Private Shared Function FindCell(cells As List(Of FailureCellResult),
                                     tier As String, w As Integer) As FailureCellResult
        If cells Is Nothing Then Return Nothing
        Return cells.Where(Function(c) c.VerdictTier = tier AndAlso
                                       c.WindowMin = w).FirstOrDefault()
    End Function

    ' ------------------------------------------------------------------ Section 4: Recommended cells
    Private Shared Sub AppendRecommended(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 4. Recommended hold window — per (tier × session)")
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
        ElseIf profit IsNot Nothing AndAlso precise.WindowMin <> profit.WindowMin Then
            Return String.Format("★ {0}m → {1:P1} failure CI [{2:P0}–{3:P0}] n={4}  |  ◆ {5}m → {6:P1} failure CI [{7:P0}–{8:P0}] n={9}",
                                 precise.WindowMin, precise.FailureRate, precise.CiLow, precise.CiHigh, precise.SampleSize,
                                 profit.WindowMin, profit.FailureRate, profit.CiLow, profit.CiHigh, profit.SampleSize)
        Else
            Return String.Format("★◆ {0}m → {1:P1} failure CI [{2:P0}–{3:P0}] n={4}",
                                 precise.WindowMin, precise.FailureRate, precise.CiLow, precise.CiHigh, precise.SampleSize)
        End If
    End Function

    ' ------------------------------------------------------------------ Section 5: Barrier-Hit Decomposition
    Private Shared Sub AppendDecomposition(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 5. Barrier-Hit Decomposition — per (tier × session)")
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
                sb.AppendLine("| Window | n | Success | Adverse Hit | Window Expiry | Ambiguous |")
                sb.AppendLine("|--------|---|---------|-------------|---------------|-----------|")
                For Each w In AnalysisConstants.HoldWindowsForResolution(pop.Resolution)
                    Dim cell = FindCell(pop.FailureCells, tier, w)
                    If cell Is Nothing OrElse cell.SampleSize = 0 Then
                        sb.AppendLine(String.Format("| {0,4}m | — | — | — | — | — |", w))
                    Else
                        sb.AppendLine(String.Format("| {0,4}m | {1} | {2} | {3} | {4} | {5} |",
                                                     w, cell.SampleSize, cell.Successes,
                                                     cell.AdverseHitFails, cell.WindowExpiryFails, cell.AmbiguousFails))
                    End If
                Next
                sb.AppendLine()
            Next
        Next
    End Sub

    ' ------------------------------------------------------------------ Section 6: VerdictContext × Outcome
    Private Shared Sub AppendContextOutcomes(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 6. Verdict Context Tag × Outcome — per session")
        sb.AppendLine()
        sb.AppendLine("_Barrier-based, so segmented per session; each row uses its own placed geometry at " &
                      "that session's recommended hold window._")
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

    ' ------------------------------------------------------------------ Section 7: Hold Window Stats
    Private Shared Sub AppendHoldWindow(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 7. Hold Window Selection Stats — per (tier × session)")
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
            Return String.Format("★ hold = **{0}m**  |  ◆ hold = **{1}m**",
                                 precise.WindowMin, profit.WindowMin)
        Else
            Return String.Format("★◆ hold = **{0}m**", precise.WindowMin)
        End If
    End Function

    ' ------------------------------------------------------------------ Section 8: Pending data
    Private Shared Sub AppendPending(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 8. Pending data (n < " & AnalysisConstants.MinSamplesPerCell & ") — per (tier × session)")
        sb.AppendLine()
        Dim anyPending As Boolean = False
        For Each pop In r.Populations
            Dim pending = pop.FailureCells.Where(Function(c) c.SampleSize < AnalysisConstants.MinSamplesPerCell).ToList()
            If pending.Count = 0 Then Continue For
            anyPending = True
            sb.AppendLine("### " & PopLabel(pop))
            For Each cell In pending
                sb.AppendLine(String.Format("- {0} / {1}m:  n={2}  (need {3})",
                                            cell.VerdictTier, cell.WindowMin,
                                            cell.SampleSize, AnalysisConstants.MinSamplesPerCell))
            Next
            sb.AppendLine()
        Next
        If Not anyPending Then
            sb.AppendLine("All cells have sufficient sample sizes.")
            sb.AppendLine()
        End If
    End Sub

    ' ------------------------------------------------------------------ Section 9: Global Diagnostics
    Private Shared Sub AppendGlobalDiagnostics(sb As StringBuilder, r As AnalysisReport)
        sb.AppendLine("## 9. Global Diagnostics")
        sb.AppendLine()
        sb.AppendLine("_Not segmented (proposal D2): book-wide, resolution-independent._")
        sb.AppendLine()

        ' § 9.1 Funding Momentum Diagnostic
        sb.AppendLine("### 9.1 Funding Momentum Diagnostic")
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

        ' § 9.2 OFI Outlier Audit
        sb.AppendLine("### 9.2 OFI Outlier Audit")
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

        ' § 9.3 OI×CVD Asymmetry Audit
        sb.AppendLine("### 9.3 OI×CVD Asymmetry Audit")
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
        Dim base As String = pop.SessionName & "×" & pop.Resolution.ToString()
        ' [D6] Tag legacy-adverse populations so the placed and legacy barrier bases
        ' are never read as one number (no silent mixing).
        If pop.BarrierLabel = "LEGACY_YARDSTICK" Then Return base & " [LEGACY_YARDSTICK]"
        Return base
    End Function

    Private Shared Function TierHasRows(cells As List(Of FailureCellResult), tier As String) As Boolean
        Return cells.Any(Function(c) c.VerdictTier = tier AndAlso c.SampleSize > 0)
    End Function

    ' Per-session sub-table header: resolution label + directional-row ATR caption +
    ' $ move-floor (proposal §2.4 req 2/3), or a "(no rows yet)" line when this tier
    ' has no rows in this population.
    Private Shared Function SubTableHeader(pop As PopulationReport, tier As String) As String
        Dim res As String = pop.Resolution.ToString() & "-min"
        Dim sess As String = If(pop.BarrierLabel = "LEGACY_YARDSTICK",
                                pop.SessionName & " [LEGACY_YARDSTICK]", pop.SessionName)
        If Not TierHasRows(pop.FailureCells, tier) Then
            Return String.Format("#### {0} · {1} · (no {2} rows yet)", sess, res, tier)
        End If
        Return String.Format("#### {0} · {1} · ATR p50={2:F0} (p25–p75 {3:F0}–{4:F0}) · move-floor ${5:F0}",
                             sess, res, pop.DirAtrP50, pop.DirAtrP25, pop.DirAtrP75, pop.MoveFloorUsd)
    End Function

    Private Shared Sub BuildSummaryCsv(report As AnalysisReport, path As String)
        Try
            Using sw As New StreamWriter(path, append:=False)
                ' [placed-target migration] AtrThreshold column dropped — one placed-geometry
                ' cell per (population × tier × window). Nothing reads this CSV programmatically
                ' (the auto-tweaker computes its own matrix), so the column simply goes.
                sw.WriteLine("Population,VerdictTier,WindowMin,FailureRate,SampleSize,CiLow,CiHigh,IsRecommended,IsMostProfitable")
                For Each pop In report.Populations
                    For Each c In pop.FailureCells
                        sw.WriteLine(String.Join(",",
                            pop.PopulationKey,
                            c.VerdictTier,
                            c.WindowMin.ToString(),
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
