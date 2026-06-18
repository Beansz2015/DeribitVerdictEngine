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

        ' ── 5. Partition rows by (session × resolution) ──────────────────────────────
        ' Offline twin of the Phase-2a tweaker filter: the matrix is resolution-blind,
        ' so a pooled cell blends 1-min NY rows with 3-min Asia/London rows (different ATR
        ' scales, provisional-by-design 3-min ROC thresholds). Segment here; the matrix
        ' engine (FailureRateMatrix.Compute) is byte-unchanged and runs once per population.
        ' Session is DERIVED from the timestamp via the shared engine bucket (inclusive);
        ' resolution is the logged authoritative ExecResolution stamp (never re-derived).
        Dim popOrder    As New List(Of String)()
        Dim popRowsMap  As New Dictionary(Of String, List(Of CsvRow))()
        Dim popSession  As New Dictionary(Of String, String)()
        Dim popRes      As New Dictionary(Of String, Integer)()
        For Each row In rows
            Dim bucket As SessionBucketSettings = ExecutionResolution.MatchSessionBucket(cfg, row.Timestamp.Hour)
            Dim sessionName As String = If(bucket IsNot Nothing, bucket.Name, "UNKNOWN")
            Dim popKey      As String = sessionName & "|" & row.ExecResolution.ToString()
            If Not popRowsMap.ContainsKey(popKey) Then
                popRowsMap(popKey) = New List(Of CsvRow)()
                popSession(popKey) = sessionName
                popRes(popKey)     = row.ExecResolution
                popOrder.Add(popKey)
            End If
            popRowsMap(popKey).Add(row)
        Next
        ' Highest-data-first display order: NY×1, LONDON×3, ASIA×3, then UNKNOWN/phantom.
        popOrder = popOrder.OrderBy(Function(k) PopulationRank(popSession(k))).
                            ThenBy(Function(k) k).ToList()

        For Each popKey In popOrder
            Dim popRows As List(Of CsvRow) = popRowsMap(popKey)
            Dim pr As New PopulationReport() With {
                .PopulationKey = popKey,
                .SessionName   = popSession(popKey),
                .Resolution    = popRes(popKey),
                .RowCount      = popRows.Count
            }

            ' Rows excluded from ALL windows (engine was off / large Deribit gap).
            pr.ExcludedRows = popRows.Where(
                Function(r) Not AnalysisConstants.HoldWindowsMinutes.Any(
                    Function(w) r.ForwardBars.ContainsKey(w) AndAlso r.ForwardBars(w).Count > 0)).Count()

            ' ── 5a. Failure-rate matrix (this population only) ────────────────────────
            ' v35 de-confound: pass the live shared floor + engine target multiplier so the
            ' matrix floors the favourable barrier and EXCLUDES gate-killed rows.
            Dim atrEx As Integer = 0, structStop As Integer = 0, atrFb As Integer = 0
            Dim belowMin As Integer = 0
            pr.FailureCells = FailureRateMatrix.Compute(popRows, atrEx, structStop, atrFb, belowMin,
                                                        cfg.Scoring.MinTradeableMovePct,
                                                        cfg.Scoring.AtrTargetMultiplier)
            pr.AtrInvalidExcluded   = atrEx
            pr.StructuralStopRows   = structStop
            pr.AtrFallbackRows      = atrFb
            pr.BelowMinMoveExcluded = belowMin

            ' ── 5b. VerdictContext cross-tab (this population only) ───────────────────
            pr.ContextOutcomes = ComputeContextOutcomes(popRows, pr.FailureCells)

            ' ── 5c. ATR caption stats (proposal §2.4 req 3) ───────────────────────────
            ' Directional rows = the rows that feed the tier matrices (tier-classified,
            ' ATR > 0). Their ATR p25/p50/p75 + the $ move-floor caption each sub-table.
            Dim dirRows = popRows.Where(Function(r) r.ATR > 0 AndAlso IsDirectionalVerdict(r.Verdict)).ToList()
            pr.DirAtrN = dirRows.Count
            If dirRows.Count > 0 Then
                Dim dirAtrs As List(Of Double) = dirRows.Select(Function(r) r.ATR).ToList()
                dirAtrs.Sort()
                pr.DirAtrP25 = Percentile(dirAtrs, 0.25)
                pr.DirAtrP50 = Percentile(dirAtrs, 0.50)
                pr.DirAtrP75 = Percentile(dirAtrs, 0.75)
                ' Representative price = median entry of this population's directional rows.
                Dim dirPrices As List(Of Double) = dirRows.Select(Function(r) r.Price).ToList()
                dirPrices.Sort()
                pr.MoveFloorUsd = cfg.Scoring.MinTradeableMovePct * Percentile(dirPrices, 0.50)
            End If

            report.Populations.Add(pr)
        Next

        ' ── 7. Diagnostics (GLOBAL — proposal D2: not segmented) ──────────────────────
        report.FundingDiagnostic = FundingMomentumDiagnostic.Compute(rows, cfg)
        report.OfiAudit          = OutlierAudit.ComputeOfi(rows)
        report.OiCvdAudit        = OutlierAudit.ComputeOiCvdAsymmetry(rows)

        ' ── 8. Render and write ─────────────────────────────────────────────────────
        MarkdownReportWriter.Write(report, outputDir)

        Return report
    End Function

    ' Display priority for the population ordering (highest-data-first, then phantom).
    Private Shared Function PopulationRank(session As String) As Integer
        Select Case session
            Case "NY"     : Return 0
            Case "LONDON" : Return 1
            Case "ASIA"   : Return 2
            Case Else     : Return 9   ' UNKNOWN / phantom populations last
        End Select
    End Function

    ' Directional = a tier-eligible verdict (STRONG/MEDIUM LONG/SHORT). Mirrors
    ' FailureRateMatrix.ToTier's positive set without coupling to that private member.
    Private Shared Function IsDirectionalVerdict(verdict As String) As Boolean
        If verdict Is Nothing Then Return False
        Select Case verdict.Trim().ToUpper()
            Case "STRONG LONG", "LONG", "STRONG SHORT", "SHORT" : Return True
            Case Else : Return False
        End Select
    End Function

    ' Linear-interpolated percentile over a pre-sorted ascending list.
    Private Shared Function Percentile(sorted As List(Of Double), p As Double) As Double
        If sorted Is Nothing OrElse sorted.Count = 0 Then Return 0
        Dim idx  As Double  = p * (sorted.Count - 1)
        Dim lo   As Integer = CInt(Math.Floor(idx))
        Dim hi   As Integer = Math.Min(lo + 1, sorted.Count - 1)
        Dim frac As Double  = idx - lo
        Return sorted(lo) * (1 - frac) + sorted(hi) * frac
    End Function

    ' VerdictContext × outcome cross-tab for ONE population's rows, using that
    ' population's recommended cell (window/threshold) as the barrier geometry.
    ' Failure classification uses v2 barrier-hit logic (same as FailureRateMatrix).
    Private Shared Function ComputeContextOutcomes(popRows As List(Of CsvRow),
                                                   failureCells As List(Of FailureCellResult)) _
                                                   As Dictionary(Of String, FailureCellResult)
        Dim outcomes As New Dictionary(Of String, FailureCellResult)()
        Dim recCell = failureCells.Where(Function(c) c.IsRecommended).FirstOrDefault()
        ' "ALIGNED" added 2026-05-17 (audit cleanup pass): post-v30 NO TRADE rows
        ' carry VerdictContext="ALIGNED". Currently masked by the inner "NO TRADE"
        ' filter so the row renders as n=0, but the addition removes enum/filter
        ' divergence if a future change writes ALIGNED on directional verdicts.
        For Each ctx In {"CONFIRMED", "ALIGNED", "FLOW_UNCONFIRMED", "MOMENTUM_FADING", "STRUCTURALLY_WEAK"}
            Dim ctxRows = popRows.Where(Function(r)
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
            If recCell IsNot Nothing Then w = recCell.WindowMin : thr = recCell.AtrThreshold

            Dim n As Integer = 0, f As Integer = 0
            For Each row In ctxRows
                Dim bars As List(Of OhlcBar) = Nothing
                If Not row.ForwardBars.TryGetValue(w, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
                    Continue For
                End If
                Dim isLong   As Boolean = row.Verdict.ToUpper().Contains("LONG")
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
                Dim outcome   As String  = FailureRateMatrix.WalkBars(bars, favBar, advBar, isLong)
                n += 1
                If outcome <> "SUCCESS" Then f += 1
            Next
            cell.SampleSize  = n
            cell.Failures    = f
            cell.FailureRate = If(n > 0, CDbl(f) / n, 0)
            If n > 0 Then FailureRateMatrix.WilsonCI(f, n, cell.CiLow, cell.CiHigh)
            outcomes(ctx) = cell
        Next
        Return outcomes
    End Function

End Class
