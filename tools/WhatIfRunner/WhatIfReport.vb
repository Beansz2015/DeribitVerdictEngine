' tools/WhatIfRunner/WhatIfReport.vb
' Renders the What-If markdown report: binding guard-rail banner, EV-in-ATR grid ranking
' with split-half validation, the population-shift line, and the per-(session × resolution ×
' tier) baseline-vs-overlay failure matrix. docs/offline-whatif-replay-proposal.md §3b/§4.
'
' The failure matrix per population is the SHIPPED FailureRateMatrix.Compute run twice
' (baseline live cfg rows vs overlay/winner cfg rows) on identical input rows — so the
' baseline column reproduces the standing failure matrix for the span (§7 acceptance).
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports System.Text

''' <summary>Mean EV in ATR units with a normal-approx 95% CI (mean ± 1.96·SE).</summary>
Public Class WhatIfEvStat
    Public Property N      As Integer
    Public Property Mean   As Double
    Public Property CiLow  As Double
    Public Property CiHigh As Double

    Public Shared Function Of_(samples As IEnumerable(Of Double)) As WhatIfEvStat
        Dim xs = samples.ToList()
        Dim s As New WhatIfEvStat With {.N = xs.Count}
        If xs.Count = 0 Then Return s
        s.Mean = xs.Average()
        If xs.Count < 2 Then
            s.CiLow = s.Mean : s.CiHigh = s.Mean
            Return s
        End If
        Dim variance = xs.Sum(Function(x) (x - s.Mean) * (x - s.Mean)) / (xs.Count - 1)
        Dim se = Math.Sqrt(variance / xs.Count)
        s.CiLow = s.Mean - 1.96 * se
        s.CiHigh = s.Mean + 1.96 * se
        Return s
    End Function
End Class

''' <summary>One expanded grid cell + its replayed EV statistics (full / selection-half /
''' holdout-half). Divergent when the holdout mean drops below the selection-half CI (§3b).</summary>
Public Class WhatIfGridCell
    Public Property Index                As Integer
    Public Property Cell                 As Dictionary(Of String, Double)
    Public Property EvalWindowBars       As Integer
    Public Property EvSamples            As List(Of WhatIfEvSample)
    Public Property DirectionalCount     As Integer
    Public Property BelowMinMoveExcluded As Integer
    Public Property EvFull               As WhatIfEvStat
    Public Property EvSel                As WhatIfEvStat
    Public Property EvHold               As WhatIfEvStat
    Public Property Divergent            As Boolean
End Class

''' <summary>Everything the writer needs. Assembled by WhatIfProgram.</summary>
Public Class WhatIfReportModel
    Public Property Stamp             As String
    Public Property OverlayPath       As String
    Public Property CsvPath           As String
    Public Property SpanFrom          As DateTime
    Public Property SpanTo            As DateTime
    Public Property TotalRows         As Integer          ' eligible rows replayed (post poc-exclusion)
    Public Property PocExcluded       As Integer
    Public Property GridCellCount     As Integer
    Public Property SweptKnobs        As List(Of String)
    Public Property OverlaySummary    As String           ' human summary of the grid knob value-sets
    Public Property OverfitCounter    As Integer          ' overlays/cells run against this span (incl. this run)
    Public Property Cells             As List(Of WhatIfGridCell)
    Public Property WinnerIndex       As Integer
    Public Property LiveCfg           As EngineSettings
    Public Property WinnerCfg         As EngineSettings
    Public Property BaselineRows      As List(Of CsvRow)  ' replayed under live cfg
    Public Property WinnerRows        As List(Of CsvRow)  ' replayed under winner cfg
    Public Property BaselineBelowMin  As Integer
    Public Property WinnerBelowMin    As Integer
    Public Property SettingsVersion   As Integer
End Class

Public Class WhatIfReport

    Private Const MinCellN As Integer = 30   ' AnalysisConstants.MinSamplesPerCell mirror
    Private Const MaxRankingRows As Integer = 50   ' cap the ranking table (winner is always rank 1)

    Public Shared Function Build(m As WhatIfReportModel) As String
        Dim sb As New StringBuilder()
        Dim single_ As Boolean = m.GridCellCount = 1
        Dim winner = m.Cells(m.WinnerIndex)

        sb.AppendLine("# What-If Replay Report — " & m.Stamp)
        sb.AppendLine()
        sb.AppendLine(String.Format("**Overlay:** `{0}`  ·  **Book span:** {1:yyyy-MM-dd HH:mm} → {2:yyyy-MM-dd HH:mm} UTC",
                                    m.OverlayPath, m.SpanFrom, m.SpanTo))
        sb.AppendLine(String.Format("**Source:** `{0}`  ·  **Settings baseline:** v{1}", m.CsvPath, m.SettingsVersion))
        sb.AppendLine(String.Format("**Eligible rows:** {0}  ·  **POC-tier rows excluded (unlogged VPFR):** {1}",
                                    m.TotalRows, m.PocExcluded))
        sb.AppendLine(String.Format("**Grid:** {0} cell(s){1}", m.GridCellCount,
                                    If(m.SweptKnobs.Count > 0, "  ·  **swept:** " & String.Join(", ", m.SweptKnobs), "")))
        sb.AppendLine()

        AppendGuardrails(sb, m)

        If Not single_ Then AppendGridRanking(sb, m)

        AppendPopulationShift(sb, m)
        AppendFailureMatrix(sb, m, winner)

        sb.AppendLine("---")
        sb.AppendLine("_This runner never writes settings.json (§4 guard-rail 1). A what-if result " &
                      "feeds a spec proposal; no live change ships without the normal spec-first + " &
                      "trader-tick + own-watch discipline._")
        Return sb.ToString()
    End Function

    ' -- §4 binding guard-rails ------------------------------------------------------------
    Private Shared Sub AppendGuardrails(sb As StringBuilder, m As WhatIfReportModel)
        Dim expectedFalse As Double = m.OverfitCounter * 0.05
        sb.AppendLine("## ⚠ Guard-rails (binding)")
        sb.AppendLine()
        sb.AppendLine("1. **Motivates, never is.** This result feeds a *spec proposal* — it is not a change. " &
                      "The runner never writes `settings.json`.")
        sb.AppendLine(String.Format(
            "2. **Overfit.** ~{0} overlay/grid-cell evaluation(s) have been run against this book span " &
            "(counter). Trying many knobs on one book *will* find phantom winners: at a 95% bar you expect " &
            "≈ **{1:F1}** false winners from noise alone. Treat single-cell wins on a swept grid with suspicion; " &
            "trust the split-half holdout, not the selection half.", m.OverfitCounter, expectedFalse))
        sb.AppendLine("3. **Touch-based.** Barriers are mid-price wick touches on 1m OHLC — **no fills, no slippage, " &
                      "no queue position**. Real execution is worse. (W6-6 closes that loop.)")
        sb.AppendLine("4. **Registered use cases:** W6-1 LONDON `stop_max` 2.0/2.2, and LONDON STRONG-only selectivity — " &
                      "the two named candidates decide on evidence from this instrument.")
        sb.AppendLine()
    End Sub

    ' -- §3b EV-in-ATR grid ranking + split-half validation --------------------------------
    Private Shared Sub AppendGridRanking(sb As StringBuilder, m As WhatIfReportModel)
        sb.AppendLine("## Grid ranking — per-trade EV in ATR units")
        sb.AppendLine()
        sb.AppendLine("_Ranking objective is EV/trade in ATR units, never win-rate (§3b): target-touch → +targetDist, " &
                      "stop/ambiguous → −stopDist, window-expiry → mark-to-window-end. Winner selected on the selection " &
                      "half (alternating session-days); holdout is the unseen half. **DIVERGENT** = holdout mean below the " &
                      "selection-half CI._")
        sb.AppendLine()
        Dim ranked = m.Cells.OrderByDescending(Function(c) c.EvSel.Mean).ToList()
        ' Show only the top cells — a full grid of up to 3,000 rows is unreadable, and the
        ' winner is always rank 1 (it is selected by the same max-selection-EV key). The
        ' overfit banner already states the full cell count that was evaluated.
        Dim shown = ranked.Take(MaxRankingRows).ToList()
        If ranked.Count > MaxRankingRows Then
            sb.AppendLine(String.Format("_Showing the top {0} of {1} cells (ranked by selection-half EV)._",
                                        MaxRankingRows, ranked.Count))
            sb.AppendLine()
        End If
        sb.AppendLine("| rank | cell | n | EV full | EV (sel) | EV (holdout) | flag |")
        sb.AppendLine("|---|---|---:|---:|---:|---:|---|")

        Dim rank As Integer = 1
        For Each c In shown
            Dim flag As String = ""
            If c.Index = m.WinnerIndex Then flag = "◆ winner"
            If c.Divergent Then flag = (flag & " ⚠ DIVERGENT").Trim()
            If c.EvFull.N < MinCellN Then flag = (flag & " n<30").Trim()
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
                "| {0} | {1} | {2} | {3:F3} | {4} | {5} | {6} |",
                rank, CellLabel(c.Cell), c.EvFull.N,
                c.EvFull.Mean, EvCell(c.EvSel), EvCell(c.EvHold), flag))
            rank += 1
        Next
        sb.AppendLine()
        Dim w = m.Cells(m.WinnerIndex)
        sb.AppendLine("**Winner (cell " & CellLabel(w.Cell) & "):** full effective overlay below; every knob not " &
                      "listed inherits the live setting.")
        sb.AppendLine()
        sb.AppendLine("```")
        sb.AppendLine(FullOverlayBlock(w, m.LiveCfg))
        sb.AppendLine("```")
        sb.AppendLine()
    End Sub

    ' -- §4 population-shift line ----------------------------------------------------------
    Private Shared Sub AppendPopulationShift(sb As StringBuilder, m As WhatIfReportModel)
        sb.AppendLine("## Population shift (directional count: baseline → overlay)")
        sb.AppendLine()
        Dim basePops = Segment(m.BaselineRows, m.LiveCfg)
        Dim ovlPops = Segment(m.WinnerRows, m.WinnerCfg)
        Dim keys = basePops.Keys.Union(ovlPops.Keys).OrderBy(Function(k) PopRank(k)).ToList()
        For Each k In keys
            Dim baseDir = If(basePops.ContainsKey(k), basePops(k).Where(Function(r) FailureRateMatrix.CanonicalTier(r.Verdict) <> "").Count(), 0)
            Dim ovlDir = If(ovlPops.ContainsKey(k), ovlPops(k).Where(Function(r) FailureRateMatrix.CanonicalTier(r.Verdict) <> "").Count(), 0)
            Dim arrow As String = If(ovlDir = baseDir, "=", If(ovlDir > baseDir, "▲", "▼"))
            sb.AppendLine(String.Format("- **{0}**: {1} → {2} directional {3}", k, baseDir, ovlDir, arrow))
        Next
        sb.AppendLine()
        sb.AppendLine(String.Format("BELOW_MIN excluded (min-move gate flipped a tradeable tier): baseline {0} / overlay {1}.",
                                    m.BaselineBelowMin, m.WinnerBelowMin))
        sb.AppendLine()
    End Sub

    ' -- §4 per-population baseline-vs-overlay failure matrix -------------------------------
    Private Shared Sub AppendFailureMatrix(sb As StringBuilder, m As WhatIfReportModel, winner As WhatIfGridCell)
        sb.AppendLine("## Baseline vs overlay — failure matrix (winner cell)")
        sb.AppendLine()
        sb.AppendLine("_Same rows, both columns. `SUCC%` = favourable barrier before adverse stop (Wilson 95% CI); " &
                      "`ADV%` = adverse-first; `EXP%` = window-expired. Cells n<30 flagged †._")
        sb.AppendLine()

        Dim basePops = Segment(m.BaselineRows, m.LiveCfg)
        Dim ovlPops = Segment(m.WinnerRows, m.WinnerCfg)
        Dim keys = basePops.Keys.Union(ovlPops.Keys).OrderBy(Function(k) PopRank(k)).ToList()

        For Each k In keys
            Dim baseRows = If(basePops.ContainsKey(k), basePops(k), New List(Of CsvRow)())
            Dim ovlRows = If(ovlPops.ContainsKey(k), ovlPops(k), New List(Of CsvRow)())
            Dim res As Integer = If(baseRows.Count > 0, baseRows(0).ExecResolution, If(ovlRows.Count > 0, ovlRows(0).ExecResolution, 1))

            ' Throwaway barrier-diagnostic counters — this report shows outcome rates only.
            Dim d1 As Integer = 0, d2 As Integer = 0, d3 As Integer = 0
            Dim d4 As Integer = 0, d5 As Integer = 0, d6 As Integer = 0
            Dim baseCells = FailureRateMatrix.Compute(baseRows, d1, d2, d3, d4, d5, d6,
                                                      m.LiveCfg.Scoring.MinTradeableMovePct,
                                                      m.LiveCfg.Scoring.AtrTargetMultiplier, res, AdverseBarrierMode.Placed)
            d1 = 0 : d2 = 0 : d3 = 0 : d4 = 0 : d5 = 0 : d6 = 0
            Dim ovlCells = FailureRateMatrix.Compute(ovlRows, d1, d2, d3, d4, d5, d6,
                                                     m.WinnerCfg.Scoring.MinTradeableMovePct,
                                                     m.WinnerCfg.Scoring.AtrTargetMultiplier, res, AdverseBarrierMode.Placed)

            Dim ovlMap = ovlCells.ToDictionary(Function(c) CellKey(c), Function(c) c)
            Dim baseDir = baseRows.Where(Function(r) FailureRateMatrix.CanonicalTier(r.Verdict) <> "").Count()
            Dim ovlDir = ovlRows.Where(Function(r) FailureRateMatrix.CanonicalTier(r.Verdict) <> "").Count()

            sb.AppendLine(String.Format("### {0}  (directional rows — baseline {1} / overlay {2})", k, baseDir, ovlDir))
            sb.AppendLine()
            ' [placed-target migration] The per-cell "k" column is gone — the matrix cell space
            ' is (tier × window) on placed geometry. Threshold sweeping lives in this runner's
            ' own overlay grid, not in the matrix.
            sb.AppendLine("| tier | win | n(b/o) | SUCC% base | SUCC% ovl | ADV% b/o | EXP% b/o |")
            sb.AppendLine("|---|---:|---|---|---|---|---|")

            Dim any As Boolean = False
            For Each bc In baseCells
                Dim oc As FailureCellResult = Nothing
                ovlMap.TryGetValue(CellKey(bc), oc)
                If bc.SampleSize = 0 AndAlso (oc Is Nothing OrElse oc.SampleSize = 0) Then Continue For
                any = True
                Dim flagB As String = If(bc.SampleSize < MinCellN, "†", "")
                Dim flagO As String = If(oc IsNot Nothing AndAlso oc.SampleSize < MinCellN, "†", "")
                sb.AppendLine(String.Format(CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2}{6}/{3}{7} | {4} | {5} | {8} | {9} |",
                    bc.VerdictTier, bc.WindowMin,
                    bc.SampleSize, If(oc IsNot Nothing, oc.SampleSize, 0),
                    RateCI(bc.Successes, bc.SampleSize),
                    If(oc IsNot Nothing, RateCI(oc.Successes, oc.SampleSize), "—"),
                    flagB, flagO,
                    PairPct(bc.AdverseHitFails, bc.SampleSize, If(oc IsNot Nothing, oc.AdverseHitFails, 0), If(oc IsNot Nothing, oc.SampleSize, 0)),
                    PairPct(bc.WindowExpiryFails, bc.SampleSize, If(oc IsNot Nothing, oc.WindowExpiryFails, 0), If(oc IsNot Nothing, oc.SampleSize, 0))))
            Next
            If Not any Then sb.AppendLine("| _(no directional rows with forward data)_ | | | | | | |")
            sb.AppendLine()
        Next
    End Sub

    ' -- helpers ---------------------------------------------------------------------------

    Private Shared Function EvCell(s As WhatIfEvStat) As String
        If s.N = 0 Then Return "—"
        Return String.Format(CultureInfo.InvariantCulture, "{0:F3} [{1:F3},{2:F3}] n={3}", s.Mean, s.CiLow, s.CiHigh, s.N)
    End Function

    Private Shared Function RateCI(count As Integer, n As Integer) As String
        If n = 0 Then Return "—"
        Dim lo As Double, hi As Double
        FailureRateMatrix.WilsonCI(count, n, lo, hi)
        Return String.Format(CultureInfo.InvariantCulture, "{0:F1}% [{1:F0}–{2:F0}]",
                             100.0 * count / n, 100.0 * lo, 100.0 * hi)
    End Function

    Private Shared Function PairPct(cb As Integer, nb As Integer, co As Integer, no As Integer) As String
        Dim b As String = If(nb > 0, String.Format(CultureInfo.InvariantCulture, "{0:F0}%", 100.0 * cb / nb), "—")
        Dim o As String = If(no > 0, String.Format(CultureInfo.InvariantCulture, "{0:F0}%", 100.0 * co / no), "—")
        Return b & "/" & o
    End Function

    Private Shared Function CellKey(c As FailureCellResult) As String
        Return String.Format(CultureInfo.InvariantCulture, "{0}|{1}", c.VerdictTier, c.WindowMin)
    End Function

    ' Compact one-line label of a cell's grid knobs (short names).
    Private Shared Function CellLabel(cell As Dictionary(Of String, Double)) As String
        If cell.Count = 0 Then Return "(live baseline)"
        Return String.Join(", ", cell.OrderBy(Function(kv) kv.Key).Select(
            Function(kv) ShortName(kv.Key) & "=" & kv.Value.ToString("0.####", CultureInfo.InvariantCulture)))
    End Function

    ' The full effective overlay block (§3b: every report row is a full combination). Lists the
    ' cell's grid knobs; all other whitelisted knobs inherit the live settings value.
    Private Shared Function FullOverlayBlock(c As WhatIfGridCell, live As EngineSettings) As String
        Dim sb As New StringBuilder()
        For Each kv In c.Cell.OrderBy(Function(x) x.Key)
            sb.AppendLine(String.Format(CultureInfo.InvariantCulture, "{0} = {1}   (pinned/swept)", kv.Key, kv.Value))
        Next
        sb.AppendLine("eval_window(bars) = " & c.EvalWindowBars)
        sb.Append("(all other whitelisted knobs = live settings v" & live.Version & ")")
        Return sb.ToString()
    End Function

    Private Shared Function ShortName(path As String) As String
        Dim i = path.LastIndexOf("."c)
        Return If(i >= 0, path.Substring(i + 1), path)
    End Function

    ' Segment replayed rows into (session × resolution) populations, using the shipped
    ' bucket matcher. Baseline and overlay rows map identically (session buckets aren't
    ' whitelisted), so populations align 1:1.
    Private Shared Function Segment(rows As List(Of CsvRow), cfg As EngineSettings) As Dictionary(Of String, List(Of CsvRow))
        Dim map As New Dictionary(Of String, List(Of CsvRow))()
        For Each row In rows
            Dim b = ExecutionResolution.MatchSessionBucket(cfg, row.Timestamp.Hour)
            Dim name As String = If(b IsNot Nothing, b.Name, "UNKNOWN")
            Dim key As String = name & "×" & row.ExecResolution
            If Not map.ContainsKey(key) Then map(key) = New List(Of CsvRow)()
            map(key).Add(row)
        Next
        Return map
    End Function

    Private Shared Function PopRank(key As String) As Integer
        If key.StartsWith("NY") Then Return 0
        If key.StartsWith("LONDON") Then Return 1
        If key.StartsWith("ASIA") Then Return 2
        Return 9
    End Function
End Class
