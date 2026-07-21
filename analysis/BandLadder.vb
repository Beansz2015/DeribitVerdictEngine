' analysis/BandLadder.vb
' [E5 — v55 addendum, 2026-07-21, eval-display-semantics-proposal.md §3c]
'
' The band-ladder diagnostic: STRONG / MEDIUM / WEAK success rates for the report's
' §10 section — the F1 re-read instrument (offline-matrix-placed-target-spec-back.md
' §8 F1: re-read the tier ladder at n≥150 STRONG). WEAK enters ONLY this section;
' the tradeable-population surfaces (matrix, D4, recommended, decomposition, context,
' hold-window, pending, summary CSV, auto-tweaker) keep the STRONG+MEDIUM population.
'
' Why WEAK is here at all (from §3c): the bridge's default tier gate refuses WEAK
' and the strip excludes it since v55 — but F1 evidence has WEAK currently
' out-performing MEDIUM (not significant, but information), and the F1 gate re-read
' has to run off THIS report because live surfaces don't show WEAK anywhere.
'
' Distinct-from-NO-TRADE contract (§3c hard requirement): CanonicalBand returns
' "WEAK" for "WEAK LONG"/"WEAK SHORT" ONLY. NO TRADE strings — bare "NO TRADE"
' and lean forms like "NO TRADE [WEAK LONG]" — return "" (excluded), the same
' way FailureRateMatrix.CanonicalTier maps them to "". The two classifiers agree
' on the exclusion, differ on the WEAK partition.
'
' Bands POOL LONG+SHORT (band-level rows, not per-side — this is a ladder read,
' not a direction read).
'
' Barriers + eval semantics IDENTICAL to the matrix: ResolveFavourableBarrier /
' ResolveAdverseBarrier in Placed mode, v35 de-confound EXCLUDE, ATR-invalid drop,
' bar-count parity via the row's own resolution horizon (max window; res-1 → 15m,
' res-3 → 45m — F1 method). Same eval, different population.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic

Public Class BandLadder

    ''' <summary>
    ''' [E5] Band classifier — the tier→band collapse used by the ladder.
    '''   "STRONG LONG" / "STRONG SHORT" → "STRONG"
    '''   "LONG"        / "SHORT"        → "MEDIUM"  (bare stored form; MEDIUM only)
    '''   "WEAK LONG"   / "WEAK SHORT"   → "WEAK"
    '''   everything else (empty, whitespace, "NO TRADE", "NO TRADE [WEAK LONG]",
    '''   "NO TRADE [LONG]", any lean/annotation form) → ""  (excluded)
    ''' The contract that WEAK ≠ NO TRADE is load-bearing (§3c) — the fixture A35b
    ''' pins the NO TRADE lean forms to "" so a future rename of these strings cannot
    ''' silently start counting refused signals as WEAK.
    ''' </summary>
    Public Shared Function CanonicalBand(verdict As String) As String
        If verdict Is Nothing Then Return ""
        Select Case verdict.Trim().ToUpperInvariant()
            Case "STRONG LONG", "STRONG SHORT" : Return "STRONG"
            Case "LONG", "SHORT"               : Return "MEDIUM"
            Case "WEAK LONG", "WEAK SHORT"     : Return "WEAK"
            Case Else                          : Return ""
        End Select
    End Function

    ''' <summary>
    ''' Compute the three-row ladder (STRONG / MEDIUM / WEAK) over a set of rows.
    ''' Each row walks the placed-vs-placed barriers at its own resolution's max
    ''' hold window (res-1 → 15m; res-3 → 45m — the F1 horizon), so a per-population
    ''' call (homogeneous resolution) and a pooled call (mixed resolutions) share
    ''' this same method — every row uses the horizon that matches its ForwardBars.
    '''
    ''' Exclusions match FailureRateMatrix.Compute exactly:
    '''   - ATR &lt;= 0 rows dropped
    '''   - v35 de-confound: gate-killed rows (|placed target − entry| &lt; floor) dropped
    '''   - rows with no bars at the horizon dropped from the denominator
    ''' Same eval semantics; different population set.
    '''
    ''' Always returns three rows in ladder order (STRONG, MEDIUM, WEAK), even when
    ''' a band has n=0 — the report renders "—" for empty rows rather than hiding
    ''' the ladder rung. This is what makes a "no MEDIUM rows this session" honest.
    ''' </summary>
    Public Shared Function Compute(rows As List(Of CsvRow),
                                   cfg As EngineSettings) As List(Of BandLadderRow)

        ' Initialize the three bands in ladder order.
        Dim bandOrder As String() = {"STRONG", "MEDIUM", "WEAK"}
        Dim counts As New Dictionary(Of String, (N As Integer, F As Integer))()
        For Each b In bandOrder
            counts(b) = (0, 0)
        Next

        Dim floorPct         As Double = cfg.Scoring.MinTradeableMovePct
        Dim engineTargetMult As Double = cfg.Scoring.AtrTargetMultiplier

        For Each row In rows
            If row.ATR <= 0 Then Continue For
            Dim band As String = CanonicalBand(row.Verdict)
            If band = "" Then Continue For

            Dim isLongRow As Boolean = row.Verdict.Trim().ToUpperInvariant().Contains("LONG")
            Dim entry     As Double  = row.Price
            Dim atr       As Double  = row.ATR

            ' v35 de-confound EXCLUDE — same test the matrix uses, mode = Placed
            ' (the row's own placed target on v0.8+ rows, engineTargetMult × ATR on
            ' the pre-v0.8 legacy fallback).
            Dim floorDist As Double = floorPct * entry
            If FailureRateMatrix.GateTargetDistance(row, isLongRow, entry, atr,
                                                    AdverseBarrierMode.Placed,
                                                    engineTargetMult) < floorDist Then
                Continue For
            End If

            ' Placed target / placed stop (throwaway routing counters — the ladder
            ' doesn't track the placed-vs-fallback split; the matrix does).
            Dim structStop As Integer = 0, atrFb As Integer = 0
            Dim placedTgt  As Integer = 0, legacyFav As Integer = 0
            Dim advBar As Double = FailureRateMatrix.ResolveAdverseBarrier(
                row, isLongRow, entry, atr, AdverseBarrierMode.Placed,
                structStop, atrFb)
            Dim favBar As Double = FailureRateMatrix.ResolveFavourableBarrier(
                row, isLongRow, entry, atr, AdverseBarrierMode.Placed,
                engineTargetMult, floorPct, placedTgt, legacyFav)

            ' Horizon = this row's max hold window (res-1 → 15m; res-3 → 45m). Every
            ' row walks the horizon matching its own resolution's ForwardBars entry.
            Dim horizonMin As Integer = AnalysisConstants.HoldWindowsForResolution(row.ExecResolution).Max()
            Dim bars As List(Of OhlcBar) = Nothing
            If Not row.ForwardBars.TryGetValue(horizonMin, bars) OrElse
               bars Is Nothing OrElse bars.Count = 0 Then
                Continue For
            End If

            Dim outcome As String = FailureRateMatrix.WalkBars(bars, favBar, advBar, isLongRow)
            Dim failed  As Boolean = (outcome <> "SUCCESS")
            Dim cur = counts(band)
            counts(band) = (cur.N + 1, cur.F + If(failed, 1, 0))
        Next

        Dim results As New List(Of BandLadderRow)()
        For Each b In bandOrder
            Dim c = counts(b)
            Dim ladderRow As New BandLadderRow With {
                .Band       = b,
                .SampleSize = c.N,
                .Failures   = c.F}
            If c.N > 0 Then
                ladderRow.FailureRate = CDbl(c.F) / c.N
                FailureRateMatrix.WilsonCI(c.F, c.N, ladderRow.CiLow, ladderRow.CiHigh)
            End If
            results.Add(ladderRow)
        Next
        Return results
    End Function

End Class

''' <summary>
''' [E5] One row of the band-ladder diagnostic. Storage stays failure-oriented like
''' FailureCellResult; the render surface flips to SUCCESS at the boundary via the
''' same SuccessPct / SuccessCiLow / SuccessCiHigh helpers (E1 v55 orientation rule).
''' </summary>
Public Class BandLadderRow
    Public Property Band        As String   ' "STRONG" | "MEDIUM" | "WEAK"
    Public Property SampleSize  As Integer
    Public Property Failures    As Integer
    Public Property FailureRate As Double
    Public Property CiLow       As Double
    Public Property CiHigh      As Double
End Class
