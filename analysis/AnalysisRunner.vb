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
        ' Fetch from the earliest row timestamp to max + (largest hold window + 1) min
        ' to cover all eligible bars. The hold windows are resolution-scaled, so a book
        ' containing 3-min rows needs forward OHLC out to T+45 (not T+15) — derive the
        ' span from the resolution-scaled windows actually present in the book
        ' (three-min-hold-window-recalibration-proposal.md §4).
        Dim startUtc As DateTime = validRows.Min(Function(r) r.Timestamp)
        Dim maxWindowMin As Integer = validRows.
            Select(Function(r) AnalysisConstants.HoldWindowsForResolution(r.ExecResolution).Max()).
            DefaultIfEmpty(15).Max()
        Dim endUtc   As DateTime = validRows.Max(Function(r) r.Timestamp).AddMinutes(maxWindowMin + 1)

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
        Dim popBarrier  As New Dictionary(Of String, String)()
        For Each row In rows
            Dim bucket As SessionBucketSettings = ExecutionResolution.MatchSessionBucket(cfg, row.Timestamp.Hour)
            Dim sessionName As String = If(bucket IsNot Nothing, bucket.Name, "UNKNOWN")
            ' [D6] Split placed-barrier rows from legacy (pre-v0.8) rows so the two adverse
            ' bases are never mixed in one cell (no silent mixing). Placed populations keep
            ' the "NY|1" key unchanged; legacy rows get a "|LEGACY_YARDSTICK" suffix + label.
            ' The live corpus is all-v0.8, so the legacy split is empty in practice.
            Dim barrierLabel As String = If(row.HasPlaced, "PLACED", "LEGACY_YARDSTICK")
            Dim popKey      As String = sessionName & "|" & row.ExecResolution.ToString() &
                                        If(row.HasPlaced, "", "|LEGACY_YARDSTICK")
            If Not popRowsMap.ContainsKey(popKey) Then
                popRowsMap(popKey) = New List(Of CsvRow)()
                popSession(popKey) = sessionName
                popRes(popKey)     = row.ExecResolution
                popBarrier(popKey) = barrierLabel
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
                .BarrierLabel  = popBarrier(popKey),
                .RowCount      = popRows.Count
            }

            ' Rows excluded from ALL windows (engine was off / large Deribit gap).
            ' Use THIS population's resolution-scaled windows so a 3-min population's
            ' exclusion test checks {15,30,45}, matching its ForwardBars keys.
            Dim popWindows As Integer() = AnalysisConstants.HoldWindowsForResolution(popRes(popKey))
            pr.ExcludedRows = popRows.Where(
                Function(r) Not popWindows.Any(
                    Function(w) r.ForwardBars.ContainsKey(w) AndAlso r.ForwardBars(w).Count > 0)).Count()

            ' ── 5a. Failure-rate matrix (this population only) ────────────────────────
            ' v35 de-confound: pass the live shared floor + engine target multiplier so the
            ' matrix floors the favourable barrier and EXCLUDES gate-killed rows.
            ' Pass the population's resolution so each matrix uses its own hold windows
            ' (NY×1 → {5,10,15}; ASIA/LONDON×3 → {15,30,45}) — three-min-hold-window-recal.
            Dim atrEx As Integer = 0, structStop As Integer = 0, atrFb As Integer = 0
            Dim placedTgt As Integer = 0, legacyFav As Integer = 0
            Dim belowMin As Integer = 0
            ' [D6 + placed-target migration] The main matrix runs in this population's mode:
            ' PLACED populations score placed target vs placed stop — the geometry the engine
            ' emitted and the autotrader executes; LEGACY_YARDSTICK populations keep the raw
            ' swing / ATR formula on BOTH sides. structStop counts placed-stop rows and
            ' placedTgt placed-target rows for a PLACED pop.
            Dim popMode As AdverseBarrierMode = If(pr.BarrierLabel = "LEGACY_YARDSTICK",
                                                   AdverseBarrierMode.Legacy, AdverseBarrierMode.Placed)
            pr.FailureCells = FailureRateMatrix.Compute(popRows, atrEx, structStop, atrFb,
                                                        placedTgt, legacyFav, belowMin,
                                                        cfg.Scoring.TradeCosts.EffectiveMinMovePct,
                                                        cfg.Scoring.AtrTargetMultiplier,
                                                        popRes(popKey), popMode)
            pr.AtrInvalidExcluded    = atrEx
            pr.StructuralStopRows    = structStop
            pr.AtrFallbackRows       = atrFb
            pr.PlacedTargetRows      = placedTgt
            pr.LegacyFavourableRows  = legacyFav
            pr.BelowMinMoveExcluded  = belowMin

            ' [D6] D4 before/after: re-walk the SAME rows under the LEGACY barrier formula
            ' (raw swing adverse + engine-target favourable) so the report can show the
            ' placed-vs-legacy failure-rate delta — the continuity bridge. Throwaway
            ' counters; the diagnostics above are the main pass.
            Dim lAtrEx As Integer = 0, lStruct As Integer = 0, lAtrFb As Integer = 0
            Dim lPlaced As Integer = 0, lLegacyFav As Integer = 0, lBelow As Integer = 0
            pr.LegacyFailureCells = FailureRateMatrix.Compute(popRows, lAtrEx, lStruct, lAtrFb,
                                                              lPlaced, lLegacyFav, lBelow,
                                                              cfg.Scoring.TradeCosts.EffectiveMinMovePct,
                                                              cfg.Scoring.AtrTargetMultiplier,
                                                              popRes(popKey), AdverseBarrierMode.Legacy)

            ' ── 5b. VerdictContext cross-tab (this population only) ───────────────────
            pr.ContextOutcomes = ComputeContextOutcomes(popRows, pr.FailureCells, cfg)
            ' [D7 spin-off 2 — smalls-2026-07-22 item 2] NO-TRADE lean-tag counts,
            ' rendered as the §6 (b) sub-table. Lean rows have NO barrier — counts only.
            pr.LeanContextCounts = ComputeLeanContextCounts(popRows)

            ' ── 5b2. Band ladder (E5, diagnostic — includes untraded WEAK) ────────────
            ' Three rows (STRONG/MEDIUM/WEAK), pooled LONG+SHORT, at THIS population's
            ' resolution horizon (res-1 → 15m; res-3 → 45m). Same placed-vs-placed
            ' eval as the matrix; WEAK enters ONLY here (§3c). Diagnostic; not read
            ' by the tweaker (PromptBuilder is oblivious to this list).
            pr.BandLadder = BandLadder.Compute(popRows, cfg)

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
                pr.MoveFloorUsd = cfg.Scoring.TradeCosts.EffectiveMinMovePct * Percentile(dirPrices, 0.50)
            End If

            report.Populations.Add(pr)
        Next

        ' ── 6. Pooled band ladder (E5, across all populations) ────────────────────────
        ' Each row walks at its own resolution horizon (Compute reads row.ExecResolution),
        ' so pooling rows across NY×1 and Asia/London×3 is coherent — every row uses the
        ' horizon that matches its own ForwardBars.
        report.PooledBandLadder = BandLadder.Compute(rows, cfg)

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
    ' population's recommended cell's HOLD WINDOW as the horizon. Barrier geometry is
    ' the row's own placed target/stop (placed-target migration) — the recommended cell
    ' no longer carries a threshold to borrow. Failure classification uses v2 barrier-hit
    ' logic (same as FailureRateMatrix). cfg supplies the legacy fallback multipliers for
    ' any pre-v0.8 row.
    Private Shared Function ComputeContextOutcomes(popRows As List(Of CsvRow),
                                                   failureCells As List(Of FailureCellResult),
                                                   cfg As EngineSettings) _
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
            Dim w As Integer = 10
            If recCell IsNot Nothing Then w = recCell.WindowMin

            Dim n As Integer = 0, f As Integer = 0
            For Each row In ctxRows
                Dim bars As List(Of OhlcBar) = Nothing
                If Not row.ForwardBars.TryGetValue(w, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
                    Continue For
                End If
                Dim isLong   As Boolean = row.Verdict.ToUpper().Contains("LONG")
                Dim entry    As Double  = row.Price
                Dim atr      As Double  = row.ATR
                ' Placed target vs placed stop when the row carries them, else the legacy
                ' formula on both sides — the same routing the main matrix uses.
                Dim ctxStruct As Integer = 0, ctxFb As Integer = 0
                Dim ctxPlaced As Integer = 0, ctxLegacyFav As Integer = 0
                Dim favBar    As Double  = FailureRateMatrix.ResolveFavourableBarrier(
                    row, isLong, entry, atr, AdverseBarrierMode.Placed,
                    cfg.Scoring.AtrTargetMultiplier, cfg.Scoring.TradeCosts.EffectiveMinMovePct,
                    ctxPlaced, ctxLegacyFav)
                Dim advBar    As Double  = FailureRateMatrix.ResolveAdverseBarrier(
                    row, isLong, entry, atr, AdverseBarrierMode.Placed, ctxStruct, ctxFb)
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

    ' [D7 spin-off 2] Per-tag row counts on NO-TRADE rows in this population — the
    ' §6 (b) sub-table. Every NO-TRADE row is counted (incl. "NO TRADE [WEAK …]"),
    ' bucketed by its VerdictContext; empty context → "(untagged)". No barrier, no
    ' outcome (NO-TRADE runs log EXCLUDED_NO_PREDICTION), so counts only — the
    ' whole point of splitting §6 is that lean-drift and committed outcomes are NOT
    ' comparable (see d7-confirmed-reread-2026-07-22.md §8).
    Private Shared Function ComputeLeanContextCounts(popRows As List(Of CsvRow)) _
                                                     As Dictionary(Of String, Integer)
        Dim counts As New Dictionary(Of String, Integer)()
        For Each row In popRows
            Dim v As String = If(row.Verdict, "").Trim().ToUpper()
            If Not v.StartsWith("NO TRADE") Then Continue For
            Dim tag As String = If(row.VerdictContext, "").Trim()
            If tag.Length = 0 Then tag = "(untagged)"
            Dim n As Integer = 0
            counts.TryGetValue(tag, n)
            counts(tag) = n + 1
        Next
        Return counts
    End Function

End Class
