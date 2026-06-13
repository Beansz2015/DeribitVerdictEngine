' analysis/AnalysisRunner.vb
' Host-agnostic entry point for the offline analysis pipeline.
' Called from MainForm via lnkAnalysisReport click handler.
'
' v2 pipeline (failure-definition-v2-proposal.md):
'   1. Load CSV rows via ForwardWindowJoiner (no forward-price joining).
'   2. Fetch 1m OHLC from Deribit for the full row time range.
'   3. Populate row.ForwardBars(W) from OHLC map.
'   4. Compute failure-rate matrix via barrier-hit logic.
'   5. Compute VerdictContext cross-tab via same barrier-hit logic.
'   6. Run diagnostics (funding, OFI, OI×CVD) — unchanged.
'   7. Render and write markdown + summary CSV.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Threading.Tasks

Public Class AnalysisRunner

    ' Run the full analysis pipeline against the given v0.4 CSV file.
    ' Writes markdown + summary CSV to outputDir and populates report fields.
    ' Returns a report with MarkdownText = error banner if the OHLC fetch fails.
    Public Shared Async Function Run(csvPath As String,
                                     outputDir As String,
                                     cfg As EngineSettings) As Task(Of AnalysisReport)
        Dim report As New AnalysisReport()

        ' ── 1. Load CSV rows ────────────────────────────────────────────────────────
        Dim rows As List(Of CsvRow) = ForwardWindowJoiner.Load(csvPath)
        report.TotalRows = rows.Count

        ' Verdict counts (over all rows regardless of forward-data availability).
        For Each row In rows
            Dim v As String = If(row.Verdict, "UNKNOWN")
            If Not report.VerdictCounts.ContainsKey(v) Then report.VerdictCounts(v) = 0
            report.VerdictCounts(v) += 1
        Next

        If rows.Count = 0 Then
            report.MarkdownText = "Analysis report: no rows in CSV."
            Return report
        End If

        ' ── 2. Compute OHLC fetch range ──────────────────────────────────────────────
        Dim validRows = rows.Where(Function(r) r.Timestamp > DateTime.MinValue).ToList()
        If validRows.Count = 0 Then
            report.MarkdownText = "Analysis report: no valid timestamps in CSV."
            Return report
        End If
        ' Fetch from the earliest row timestamp to max + 16 min to cover
        ' all eligible bars (bars closing at T+15 open at T+14, plus buffer).
        Dim startUtc As DateTime = validRows.Min(Function(r) r.Timestamp)
        Dim endUtc   As DateTime = validRows.Max(Function(r) r.Timestamp).AddMinutes(16)

        ' ── 3. Deribit OHLC bulk fetch ───────────────────────────────────────────────
        Dim ohlcMap As Dictionary(Of DateTime, OhlcBar) =
            Await DeribitOhlcFetcher.FetchOhlcRange(startUtc, endUtc)

        If ohlcMap Is Nothing Then
            report.MarkdownText =
                "Forward-data fetch failed — report cannot be regenerated until Deribit is reachable."
            MarkdownReportWriter.WriteErrorBanner(report, outputDir)
            Return report
        End If

        ' ── 4. Populate ForwardBars ──────────────────────────────────────────────────
        ForwardWindowJoiner.PopulateForwardBars(rows, ohlcMap)

        ' Count rows excluded from ALL windows (engine was off / large Deribit gap).
        report.ExcludedRows = rows.Where(
            Function(r) Not AnalysisConstants.HoldWindowsMinutes.Any(
                Function(w) r.ForwardBars.ContainsKey(w) AndAlso r.ForwardBars(w).Count > 0)).Count()

        ' ── 5. Failure-rate matrix ───────────────────────────────────────────────────
        Dim atrEx As Integer = 0, structStop As Integer = 0, atrFb As Integer = 0
        Dim belowMin As Integer = 0
        ' v35 de-confound: pass the live shared floor + engine target multiplier so the
        ' matrix floors the favourable barrier and EXCLUDES gate-killed rows.
        report.FailureCells = FailureRateMatrix.Compute(rows, atrEx, structStop, atrFb, belowMin,
                                                        cfg.Scoring.MinTradeableMovePct,
                                                        cfg.Scoring.AtrTargetMultiplier)
        report.AtrInvalidExcluded = atrEx
        report.StructuralStopRows = structStop
        report.AtrFallbackRows    = atrFb
        report.BelowMinMoveExcluded = belowMin

        ' ── 6. VerdictContext cross-tab ──────────────────────────────────────────────
        ' Use the recommended cell for each tier (picks the most stable window/threshold).
        ' Failure classification uses v2 barrier-hit logic (same as FailureRateMatrix).
        Dim recCells = report.FailureCells.Where(Function(c) c.IsRecommended).ToList()
        ' "ALIGNED" added 2026-05-17 (audit cleanup pass): post-v30 NO TRADE rows
        ' carry VerdictContext="ALIGNED". Currently masked by the inner "NO TRADE"
        ' filter so the row renders as n=0, but the addition removes enum/filter
        ' divergence if a future change writes ALIGNED on directional verdicts.
        For Each ctx In {"CONFIRMED", "ALIGNED", "FLOW_UNCONFIRMED", "MOMENTUM_FADING", "STRUCTURALLY_WEAK"}
            Dim ctxRows = rows.Where(Function(r)
                Return String.Equals(r.VerdictContext, ctx, StringComparison.OrdinalIgnoreCase) AndAlso
                       r.ATR > 0 AndAlso
                       r.Verdict <> "" AndAlso
                       r.Verdict.ToUpper() <> "NO TRADE" AndAlso
                       Not r.Verdict.ToUpper().StartsWith("WEAK")
            End Function).ToList()
            If ctxRows.Count = 0 Then Continue For

            Dim cell As New FailureCellResult() With {.VerdictTier = ctx}
            Dim w   As Integer = 10
            Dim thr As Double  = 0.5
            Dim recCell = recCells.FirstOrDefault()
            If recCell IsNot Nothing Then w = recCell.WindowMin : thr = recCell.AtrThreshold

            Dim n As Integer = 0, f As Integer = 0
            For Each row In ctxRows
                Dim bars As List(Of OhlcBar) = Nothing
                If Not row.ForwardBars.TryGetValue(w, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
                    Continue For
                End If
                Dim tierStr  As String  = If(row.Verdict.ToUpper().Contains("LONG"), "LONG", "SHORT")
                Dim isLong   As Boolean = (tierStr = "LONG")
                Dim entry    As Double  = row.Price
                Dim atr      As Double  = row.ATR
                Dim favBar   As Double  = If(isLong, entry + thr * atr, entry - thr * atr)
                Dim advBar   As Double
                If isLong Then
                    advBar = If(row.SwingStopLong > 0, row.SwingStopLong,
                                entry - AnalysisConstants.AdverseFallbackAtrMultiplier * atr)
                Else
                    advBar = If(row.SwingStopShort > 0, row.SwingStopShort,
                                entry + AnalysisConstants.AdverseFallbackAtrMultiplier * atr)
                End If
                Dim outcome    As String  = FailureRateMatrix.WalkBars(bars, favBar, advBar, isLong)
                Dim isFailure  As Boolean = (outcome <> "SUCCESS")
                n += 1
                If isFailure Then f += 1
            Next
            cell.SampleSize  = n
            cell.Failures    = f
            cell.FailureRate = If(n > 0, CDbl(f) / n, 0)
            If n > 0 Then FailureRateMatrix.WilsonCI(f, n, cell.CiLow, cell.CiHigh)
            report.ContextOutcomes(ctx) = cell
        Next

        ' ── 7. Diagnostics ──────────────────────────────────────────────────────────
        report.FundingDiagnostic = FundingMomentumDiagnostic.Compute(rows)
        report.OfiAudit          = OutlierAudit.ComputeOfi(rows)
        report.OiCvdAudit        = OutlierAudit.ComputeOiCvdAsymmetry(rows)

        ' ── 8. Render and write ─────────────────────────────────────────────────────
        MarkdownReportWriter.Write(report, outputDir)

        Return report
    End Function

End Class
