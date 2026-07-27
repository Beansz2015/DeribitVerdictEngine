' analysis/FailureRateMatrix.vb
' Computes per-tier x window failure rates with 95% Wilson CI.
' Verdict tiers: STRONG_LONG, STRONG_SHORT, MEDIUM_LONG, MEDIUM_SHORT.
' NO_TRADE and WEAK_* are excluded from the denominator.
' Rows where ATR <= 0 are excluded entirely (degenerate barriers).
'
' Failure definition v2 (failure-definition-v2-proposal.md):
'   Barrier-hit with adverse stop. Walk 1m OHLC bars in chronological order:
'     favHit AND advHit in same bar → FAILURE (conservative ambiguous-bar rule)
'     favHit first → SUCCESS
'     advHit first → FAILURE
'   Window expires without any hit → FAILURE
'
' [placed-target migration 2026-07-21, offline-matrix-placed-target-proposal.md]
'   Both barriers are now the PLACED geometry the engine emitted for the row:
'     Adverse    — PlacedStop{Long,Short}   (migrated at D6)
'     Favourable — PlacedTarget{Long,Short} (this migration)
'   Pre-v0.8 rows carry neither and keep the legacy formula on BOTH sides, labelled
'   LEGACY_YARDSTICK. The cell space loses the ATR-threshold axis entirely: one
'   placed-geometry cell per (tier × window). The retired grid was degenerate — at
'   ATR≈44 every column sat below the min-move floor and collapsed onto one barrier.
'   The window axis survives: the hold-horizon question is geometry-independent.
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Math

''' <summary>
''' [D6] Which adverse (stop) barrier the failure walk scores against.
'''   Placed — the logged placed stop (PlacedStop{Long,Short}) when the row carries it,
'''            else the legacy swing-else-ATR fallback. The migrated default: outcomes
'''            score against the geometry the autotrader executes.
'''   Legacy — always the raw 5m swing stop, else the ATR-multiple fallback. The pre-D6
'''            yardstick (median ~9×ATR away). Used for the D4 before/after re-walk.
''' d6-eval-placed-stop-migration-proposal.md.
''' </summary>
Public Enum AdverseBarrierMode
    Placed
    Legacy
End Enum

Public Class FailureRateMatrix

    ' Maps engine verdict strings → canonical tier names.
    Private Shared Function ToTier(verdict As String) As String
        Return CanonicalTier(verdict)
    End Function

    ''' <summary>Public tier classifier — the SAME mapping ToTier uses, exposed so the
    ''' What-If replay tags its re-derived verdicts against the identical denominator
    ''' definition (STRONG/MEDIUM LONG/SHORT feed the matrix; WEAK / NO TRADE excluded).
    ''' Returns "" for excluded verdicts. Null-safe.</summary>
    Public Shared Function CanonicalTier(verdict As String) As String
        If verdict Is Nothing Then Return ""
        Select Case verdict.Trim().ToUpper()
            Case "STRONG LONG"  : Return "STRONG_LONG"
            Case "LONG"         : Return "MEDIUM_LONG"
            Case "STRONG SHORT" : Return "STRONG_SHORT"
            Case "SHORT"        : Return "MEDIUM_SHORT"
            Case Else           : Return ""   ' WEAK / NO TRADE — excluded
        End Select
    End Function

    Private Shared Function IsLong(tier As String) As Boolean
        Return tier.EndsWith("LONG")
    End Function

    ' [D6] Resolve the adverse (stop) barrier for one directional row under the chosen mode.
    ' Placed mode uses the logged placed stop (PlacedStop{Long,Short}) when the row carries
    ' it — the geometry the autotrader executes — else the legacy swing-else-ATR fallback.
    ' Legacy mode forces the raw 5m swing stop on every row (the pre-D6 yardstick), so the
    ' D4 report can re-walk the same rows both ways. structuralStopRows counts rows whose
    ' adverse came from a real stop level (placed or swing); atrFallbackRows counts the
    ' ATR-multiple fallback. Public for harness A27b (Placed-vs-legacy routing).
    Public Shared Function ResolveAdverseBarrier(row As CsvRow, isLong As Boolean,
                                                 entry As Double, atr As Double,
                                                 mode As AdverseBarrierMode,
                                                 ByRef structuralStopRows As Integer,
                                                 ByRef atrFallbackRows As Integer) As Double
        If mode = AdverseBarrierMode.Placed AndAlso row.HasPlaced Then
            Dim placedStop As Double = If(isLong, row.PlacedStopLong, row.PlacedStopShort)
            If placedStop > 0 Then
                structuralStopRows += 1
                Return placedStop
            End If
        End If
        ' Legacy yardstick: raw swing stop, else ATR-multiple fallback.
        If isLong Then
            If row.SwingStopLong > 0 Then
                structuralStopRows += 1
                Return row.SwingStopLong
            End If
            atrFallbackRows += 1
            Return entry - AnalysisConstants.AdverseFallbackAtrMultiplier * atr
        Else
            If row.SwingStopShort > 0 Then
                structuralStopRows += 1
                Return row.SwingStopShort
            End If
            atrFallbackRows += 1
            Return entry + AnalysisConstants.AdverseFallbackAtrMultiplier * atr
        End If
    End Function

    ' [placed-target migration] Resolve the FAVOURABLE (target) barrier for one directional
    ' row — the mirror of ResolveAdverseBarrier, and the change this migration exists for.
    ' Placed mode uses the logged placed target (PlacedTarget{Long,Short}) when the row
    ' carries a non-zero one: that price IS the barrier, returned UNFLOORED. The live
    ' Step 5c min-tradeable-move gate already evaluated it, so re-flooring it here would
    ' push low-ATR rows back onto a shared floor price — the exact degeneracy the retired
    ' ATR grid suffered. Legacy (pre-v0.8) rows fall back to the engine's own take-profit
    ' geometry, engineTargetMult × ATR, floored at floorPct × entry as before.
    ' placedTargetRows / legacyFavourableRows track which side fired, so a PLACED
    ' population that still contains fallback rows is visible rather than silently mixed.
    ' Public for harness A32a (placed-vs-legacy favourable routing).
    Public Shared Function ResolveFavourableBarrier(row As CsvRow, isLong As Boolean,
                                                    entry As Double, atr As Double,
                                                    mode As AdverseBarrierMode,
                                                    engineTargetMult As Double,
                                                    floorPct As Double,
                                                    ByRef placedTargetRows As Integer,
                                                    ByRef legacyFavourableRows As Integer) As Double
        If mode = AdverseBarrierMode.Placed AndAlso row.HasPlaced Then
            Dim placedTarget As Double = If(isLong, row.PlacedTargetLong, row.PlacedTargetShort)
            If placedTarget > 0 Then
                placedTargetRows += 1
                Return placedTarget
            End If
        End If
        legacyFavourableRows += 1
        Dim favDist As Double = Max(engineTargetMult * atr, floorPct * entry)
        Return If(isLong, entry + favDist, entry - favDist)
    End Function

    ' [placed-target migration] The target distance the LIVE min-move gate would have
    ' evaluated for this row — the input to the v35 de-confound EXCLUDE test.
    ' v0.8+ rows are tested EXACTLY against their logged placed target; the
    ' engineTargetMult × ATR approximation survives only for pre-v0.8 rows, where it was
    ' always a stand-in for a value the CSV did not carry (eval-metric-deconfound §3).
    ' Using the approximation on a placed row would drop low-ATR rows whose structural
    ' target the live gate actually passed — i.e. exactly the rows this migration makes
    ' readable. Public for harness A32d (floored-grid impossibility).
    Public Shared Function GateTargetDistance(row As CsvRow, isLong As Boolean,
                                              entry As Double, atr As Double,
                                              mode As AdverseBarrierMode,
                                              engineTargetMult As Double) As Double
        If mode = AdverseBarrierMode.Placed AndAlso row.HasPlaced Then
            Dim placedTarget As Double = If(isLong, row.PlacedTargetLong, row.PlacedTargetShort)
            If placedTarget > 0 Then Return Abs(placedTarget - entry)
        End If
        Return engineTargetMult * atr
    End Function

    ' Walk eligible bars chronologically and classify outcome.
    ' Returns "SUCCESS", "ADVERSE_HIT", "AMBIGUOUS", or "WINDOW_EXPIRED".
    ' AMBIGUOUS (both barriers touched in same bar) → treated as FAILURE by caller
    ' per spec §2a conservative-bias rule.
    Public Shared Function WalkBars(bars As List(Of OhlcBar),
                                    favBar As Double,
                                    advBar As Double,
                                    isLong As Boolean) As String
        For Each bar In bars
            Dim favHit As Boolean = If(isLong, bar.High >= favBar, bar.Low <= favBar)
            Dim advHit As Boolean = If(isLong, bar.Low <= advBar,  bar.High >= advBar)

            If favHit AndAlso advHit Then Return "AMBIGUOUS"   ' conservative = failure
            If favHit Then Return "SUCCESS"
            If advHit Then Return "ADVERSE_HIT"
        Next
        Return "WINDOW_EXPIRED"
    End Function

    ' Walk eligible bars and return True iff the favourable barrier was touched
    ' at any point within the window, regardless of whether the adverse barrier
    ' hit first. Used by the target-hit metric — decouples "direction was right"
    ' from "stop placement survived noise".
    Public Shared Function TargetHitWalk(bars   As List(Of OhlcBar),
                                         favBar As Double,
                                         isLong As Boolean) As Boolean
        For Each b In bars
            If isLong Then
                If b.High >= favBar Then Return True
            Else
                If b.Low <= favBar Then Return True
            End If
        Next
        Return False
    End Function

    ' Compute the full failure-rate matrix — one placed-geometry cell per (tier × window).
    ' ByRef counters are informational (each counted once per row, not per window):
    '   atrInvalidExcluded    — rows excluded because ATR <= 0
    '   structuralStopRows / atrFallbackRows       — adverse barrier: real stop vs ATR-multiple
    '   placedTargetRows   / legacyFavourableRows  — favourable barrier: placed target vs
    '                          the legacy engineTargetMult × ATR fallback. The mirror of the
    '                          adverse pair, so a PLACED population still carrying fallback
    '                          favourable rows is visible rather than silently mixed.
    ' v35 de-confound (eval-metric-deconfound-proposal.md): rows the live min-tradeable-move
    ' gate would NO-TRADE are EXCLUDED from the denominator rather than counted as failures.
    ' Post-migration that test is EXACT on v0.8+ rows (|placed target − entry| vs the floor)
    ' and keeps the engineTargetMult × ATR approximation only for pre-v0.8 rows — see
    ' GateTargetDistance. floorPct / engineTargetMult default to the AnalysisConstants POCO
    ' mirrors; call sites pass the live cfg.Scoring.TradeCosts.EffectiveMinMovePct (v62: the
    ' composed fee + min-net floor) / cfg.Scoring.AtrTargetMultiplier.
    ' resolution scales the hold windows (three-min-hold-window-recalibration-proposal.md):
    ' res=1 → {5,10,15}, res=3 → {15,30,45}. Defaults to 1 so the NY×1-filtered
    ' auto-tweaker call (which omits this arg) keeps its window set; the offline report
    ' passes the population's resolution per (session × resolution) segment.
    Public Shared Function Compute(rows As List(Of CsvRow),
                                   ByRef atrInvalidExcluded As Integer,
                                   ByRef structuralStopRows  As Integer,
                                   ByRef atrFallbackRows     As Integer,
                                   ByRef placedTargetRows    As Integer,
                                   ByRef legacyFavourableRows As Integer,
                                   ByRef belowMinMoveExcluded As Integer,
                                   Optional floorPct As Double = AnalysisConstants.FavBarAbsFloorPct,
                                   Optional engineTargetMult As Double = AnalysisConstants.EngineTargetAtrMultiplier,
                                   Optional resolution As Integer = 1,
                                   Optional adverseMode As AdverseBarrierMode = AdverseBarrierMode.Placed) As List(Of FailureCellResult)

        atrInvalidExcluded  = 0
        structuralStopRows  = 0
        atrFallbackRows     = 0
        placedTargetRows    = 0
        legacyFavourableRows = 0
        belowMinMoveExcluded = 0

        Dim windows As Integer() = AnalysisConstants.HoldWindowsForResolution(resolution)

        ' counts(tier)(window) = (N, Failures, Successes, AdverseHits, Expiries, Ambiguous)
        ' [placed-target migration] The threshold dimension is gone — the favourable barrier
        ' is a row property (the placed target), not a per-cell sweep knob.
        Dim counts As New Dictionary(Of String, Dictionary(Of Integer,
            (N As Integer, F As Integer, Suc As Integer, Adv As Integer, Exp As Integer, Amb As Integer)))()

        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            counts(tier) = New Dictionary(Of Integer,
                (Integer, Integer, Integer, Integer, Integer, Integer))()
            For Each w In windows
                counts(tier)(w) = (0, 0, 0, 0, 0, 0)
            Next
        Next

        For Each row In rows
            ' Exclude rows with degenerate ATR (barriers would collapse to entry price).
            If row.ATR <= 0 Then
                atrInvalidExcluded += 1
                Continue For
            End If

            Dim tier As String = ToTier(row.Verdict)
            If tier = "" Then Continue For

            Dim isLongRow As Boolean = IsLong(tier)
            Dim entry     As Double  = row.Price
            Dim atr       As Double  = row.ATR

            ' [v35 de-confound] EXCLUDE gate-killed rows: a directional trade whose engine
            ' target can't clear the min-tradeable-move floor is one the live v35 gate
            ' would NO-TRADE — remove it from the denominator (not a prediction failure).
            ' The distance tested is the row's own placed target when logged, else the
            ' pre-v0.8 engineTargetMult × ATR approximation (GateTargetDistance).
            Dim floorDist As Double = floorPct * entry
            If GateTargetDistance(row, isLongRow, entry, atr, adverseMode, engineTargetMult) < floorDist Then
                belowMinMoveExcluded += 1
                Continue For
            End If

            ' [D6] Adverse barrier: the placed stop when the row carries it (Placed mode —
            ' the geometry the autotrader executes), else the legacy swing-else-ATR
            ' yardstick. Legacy mode forces the raw swing on every row (the D4 "before"
            ' walk). structuralStopRows / atrFallbackRows track which fallback fired.
            Dim advBar As Double = ResolveAdverseBarrier(row, isLongRow, entry, atr, adverseMode,
                                                         structuralStopRows, atrFallbackRows)

            ' [placed-target migration] Favourable barrier: the placed target the engine
            ' emitted for this row (unfloored), else the legacy floored ATR fallback.
            ' Both barriers are now row properties, so they resolve ONCE per row — the
            ' window loop below no longer re-derives geometry, it only varies the horizon.
            Dim favBar As Double = ResolveFavourableBarrier(row, isLongRow, entry, atr, adverseMode,
                                                            engineTargetMult, floorPct,
                                                            placedTargetRows, legacyFavourableRows)

            For Each w In windows
                Dim bars As List(Of OhlcBar) = Nothing
                If Not row.ForwardBars.TryGetValue(w, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
                    Continue For   ' no data for this window — exclude from denominator
                End If

                Dim outcome As String = WalkBars(bars, favBar, advBar, isLongRow)
                Dim failed  As Boolean = (outcome <> "SUCCESS")

                Dim cur = counts(tier)(w)
                Dim newSuc = cur.Suc + If(outcome = "SUCCESS",        1, 0)
                Dim newAdv = cur.Adv + If(outcome = "ADVERSE_HIT",    1, 0)
                Dim newExp = cur.Exp + If(outcome = "WINDOW_EXPIRED", 1, 0)
                Dim newAmb = cur.Amb + If(outcome = "AMBIGUOUS",      1, 0)
                counts(tier)(w) = (cur.N + 1,
                                   cur.F + If(failed, 1, 0),
                                   newSuc, newAdv, newExp, newAmb)
            Next
        Next

        ' Build result list and mark recommended cell per tier.
        ' Two flags per tier:
        '   IsRecommended    — lowest CI width (most-precise estimate; auto-tweaker view)
        '                      Picks cells with extreme p more often because Wilson CI is narrower
        '                      at p near 0/1. Good for "how trustworthy is this number" but can pick
        '                      the WORST-performing cell when failure rates are high.
        '   IsMostProfitable — lowest failure rate (trader view)
        '                      Picks the cell with the best actual trade outcome. The cell a human
        '                      should look at when deciding whether the verdict is worth taking.
        ' Both require n >= MinSamplesPerCell. They CAN point at the same cell (common when failure
        ' rate is near the extremes); the markdown writer renders ★◆ for that case.
        Dim results As New List(Of FailureCellResult)()
        For Each tier In {"STRONG_LONG", "STRONG_SHORT", "MEDIUM_LONG", "MEDIUM_SHORT"}
            Dim bestCiWidth As Double  = Double.MaxValue
            Dim bestCiIdx   As Integer = -1
            Dim bestFailRate As Double  = Double.MaxValue
            Dim bestFailIdx  As Integer = -1
            Dim tierResults As New List(Of FailureCellResult)()
            For Each w In windows
                Dim c = counts(tier)(w)
                Dim cell As New FailureCellResult() With {
                    .VerdictTier        = tier,
                    .WindowMin          = w,
                    .SampleSize         = c.N,
                    .Failures           = c.F,
                    .Successes          = c.Suc,
                    .AdverseHitFails    = c.Adv,
                    .WindowExpiryFails  = c.Exp,
                    .AmbiguousFails     = c.Amb
                }
                If c.N > 0 Then
                    cell.FailureRate = CDbl(c.F) / c.N
                    WilsonCI(c.F, c.N, cell.CiLow, cell.CiHigh)
                    cell.CiWidth = cell.CiHigh - cell.CiLow
                End If
                tierResults.Add(cell)
                If c.N >= AnalysisConstants.MinSamplesPerCell Then
                    If cell.CiWidth < bestCiWidth Then
                        bestCiWidth = cell.CiWidth
                        bestCiIdx = tierResults.Count - 1
                    End If
                    If cell.FailureRate < bestFailRate Then
                        bestFailRate = cell.FailureRate
                        bestFailIdx = tierResults.Count - 1
                    End If
                End If
            Next
            If bestCiIdx >= 0 Then tierResults(bestCiIdx).IsRecommended = True
            If bestFailIdx >= 0 Then tierResults(bestFailIdx).IsMostProfitable = True
            results.AddRange(tierResults)
        Next

        Return results
    End Function

    ' 95% Wilson score confidence interval (zsq = 1.96^2 = 3.8416).
    Public Shared Sub WilsonCI(failures As Integer, n As Integer,
                                ByRef ciLow As Double, ByRef ciHigh As Double)
        Const Zsq As Double = 3.8416
        If n = 0 Then ciLow = 0 : ciHigh = 1 : Return
        Dim p      As Double = CDbl(failures) / n
        Dim denom  As Double = 1.0 + Zsq / n
        Dim centre As Double = (p + Zsq / (2 * n)) / denom
        Dim margin As Double = Sqrt(p * (1 - p) / n + Zsq / (4.0 * n * n)) * 1.96 / denom
        ciLow  = Max(0.0, centre - margin)
        ciHigh = Min(1.0, centre + margin)
    End Sub

    ' Append one picked-cell entry to analysis/picked_cell_history.csv.
    ' v2 schema: first line starts with "# schema=v2 (barrier-hit with adverse stop)".
    ' If an existing file does NOT start with that marker it is a v1 file —
    ' rotate it to .v1.bak before writing. Idempotent on repeated calls.
    '
    ' [placed-target migration, M3] The pick space is (window) only — there is no
    ' threshold to record. The file is NOT rotated: the AtrThreshold COLUMN is kept in
    ' place and written EMPTY on new rows, so the file stays one consistent CSV shape
    ' and a reader sees at a glance which rows predate the migration (numeric threshold)
    ' and which are placed-geometry picks (blank). The schema marker still begins
    ' "# schema=v2", so RotateV1HistoryIfNeeded leaves existing v2 files alone.
    Public Shared Sub AppendPickedCell(csvPath As String,
                                       tier As String, windowMin As Integer,
                                       failureRate As Double, sampleSize As Integer,
                                       ciLow As Double, ciHigh As Double)
        Try
            RotateV1HistoryIfNeeded(csvPath)
            Dim writeHeader As Boolean = Not IO.File.Exists(csvPath)
            Using sw As New IO.StreamWriter(csvPath, append:=True)
                If writeHeader Then
                    sw.WriteLine("# schema=v2 (barrier-hit with adverse stop; placed-target favourable " &
                                 "since 2026-07-21 — AtrThreshold blank on placed-geometry rows)")
                    sw.WriteLine("Timestamp,Tier,WindowMin,AtrThreshold,FailureRate,SampleSize,CiLow,CiHigh")
                End If
                sw.WriteLine(String.Join(",",
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    tier,
                    windowMin.ToString(),
                    "",                       ' AtrThreshold — retired; column held for shape
                    Inv(failureRate, "F6"),
                    sampleSize.ToString(),
                    Inv(ciLow, "F6"),
                    Inv(ciHigh, "F6")))
            End Using
        Catch
            ' Best-effort write — do not abort the auto-tweaker run on CSV I/O failure.
        End Try
    End Sub

    ' Format a numeric field with InvariantCulture so a comma-decimal host locale
    ' can't split a value across CSV columns. Mirrors AnalysisLogger.Inv; byte-
    ' identical to the prior culture-sensitive ToString on a dot-decimal host.
    Private Shared Function Inv(value As Double, fmt As String) As String
        Return value.ToString(fmt, CultureInfo.InvariantCulture)
    End Function

    ' Rename an existing v1 picked-cell history file to .v1.bak.
    ' Detection: first line of the file does NOT start with "# schema=v2".
    Private Shared Sub RotateV1HistoryIfNeeded(csvPath As String)
        If Not IO.File.Exists(csvPath) Then Return
        Dim firstLine As String = ""
        Try
            Using sr As New IO.StreamReader(csvPath)
                firstLine = sr.ReadLine()
            End Using
        Catch
            Return
        End Try
        If firstLine IsNot Nothing AndAlso firstLine.StartsWith("# schema=v2") Then Return
        ' File is v1 — rename.
        Dim bakPath As String = csvPath & ".v1.bak"
        If IO.File.Exists(bakPath) Then
            Dim ts As String = DateTime.UtcNow.ToString("yyyyMMddHHmmss")
            bakPath = csvPath & ".v1." & ts & ".bak"
        End If
        Try
            IO.File.Move(csvPath, bakPath)
            Console.WriteLine("[FailureRateMatrix] Rotated v1 picked-cell history → " & bakPath)
        Catch ex As Exception
            Console.WriteLine("[FailureRateMatrix] Could not rotate v1 history: " & ex.Message)
        End Try
    End Sub

End Class
