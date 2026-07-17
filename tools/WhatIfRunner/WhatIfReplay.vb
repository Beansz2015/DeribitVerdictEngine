' tools/WhatIfRunner/WhatIfReplay.vb
' The replay core: CsvRow → minimal IndicatorResults adapter → the ACTUAL shipped
' SignalEmitter.ComputeSideLevels for placed levels, plus a faithful re-derivation of the
' Step 4→5 verdict from the LOGGED effective scores under the overlay's thresholds.
' docs/offline-whatif-replay-proposal.md §3 ("one seam, no copies").
'
' Nothing here rebuilds Step-2/Pass-2x scores (raw scores are replayed as logged, never
' rebuilt — §2). The placed-level arbitration IS production (ComputeSideLevels); the verdict
' tier walk mirrors ScoringEngine_Calculate_Verdict Step 4→5 using logged inputs:
'   - regime veto      from row.Regime + raw row.LongScore/ShortScore,
'   - dominant + tier   from row.EffectiveLong/ShortScore vs Math.Ceiling(row.MaxScore × pct),
'   - MTF veto          from the logged per-side MTFGatePass flags (mtf_gate.enabled is live),
'   - min-move gate     on the REPLAYED placed target (better than the CSV's ATR approximation).
' POC tier: VPFRPoc/VPFRSignal are unlogged, so the replay ladder is swing → HVN → fallback,
' and rows whose LIVE placement was poc (TargetCapReason bucket) are excluded upstream (§3 W3).
'
' Host-agnostic: no System.Windows.Forms references.

Imports System.Collections.Generic
Imports System.Linq

''' <summary>One directional replayed trade's EV sample (ATR units), tagged for
''' per-population aggregation and split-half date partitioning.</summary>
Public Class WhatIfEvSample
    Public Property Timestamp   As DateTime
    Public Property SessionName As String
    Public Property Resolution  As Integer
    Public Property Tier        As String
    Public Property EvAtr       As Double
End Class

''' <summary>Everything one grid cell (or the baseline) produces over the eligible rows:
''' the replayed CsvRows (fed straight into the shipped FailureRateMatrix per population),
''' the directional EV samples, and the exclusion tallies.</summary>
Public Class WhatIfCellRun
    Public Property ReplayedRows         As New List(Of CsvRow)()
    Public Property EvSamples            As New List(Of WhatIfEvSample)()
    Public Property DirectionalCount     As Integer   ' STRONG/MEDIUM LONG/SHORT (matrix denominator)
    Public Property BelowMinMoveExcluded As Integer   ' tradeable-by-score rows flipped by the min-move gate
End Class

Public Class WhatIfReplay

    ''' <summary>Adapter: the minimal IndicatorResults ComputeSideLevels reads. VPFRPoc/Signal
    ''' are left at the no-POC values (unlogged), so the target ladder is swing → HVN → fallback.
    ''' SessionUtcHour drives the structural_levels fallback-target session override.</summary>
    Public Shared Function BuildIndicator(row As CsvRow) As IndicatorResults
        Dim r As New IndicatorResults()
        r.CurrentPrice = row.Price
        r.ATR = row.ATR
        r.Regime = row.Regime
        r.ExecResolution = row.ExecResolution
        r.SessionUtcHour = row.Timestamp.Hour
        r.SwingTargetLong = row.SwingTargetLong
        r.SwingTargetShort = row.SwingTargetShort
        r.SwingStopLong = row.SwingStopLong
        r.SwingStopShort = row.SwingStopShort
        r.VPFRNearestHvnAbove = row.VpfrNearestHvnAbove
        r.VPFRNearestHvnBelow = row.VpfrNearestHvnBelow
        r.VPFRPoc = 0                 ' unlogged — keeps the POC tier closed (replay ladder = swing→HVN→fallback)
        r.VPFRSignal = "NEUTRAL"      ' unlogged — pocGated stays False in ComputeSideLevels
        Return r
    End Function

    ''' <summary>The dominant side from the LOGGED effective scores, after the regime veto.
    ''' Mirrors Step 4 pre-empt returns: a TRENDING_UP short (ss&gt;ls) / TRENDING_DOWN long
    ''' (ls&gt;ss) is vetoed to NONE (no directional verdict). The veto reads RAW scores, exactly
    ''' as the engine does.</summary>
    Public Shared Function DominantSide(row As CsvRow) As String
        Select Case row.Regime
            Case "TRENDING_UP"
                If row.ShortScore > row.LongScore Then Return "NONE"
            Case "TRENDING_DOWN"
                If row.LongScore > row.ShortScore Then Return "NONE"
        End Select
        If row.EffectiveLongScore > row.EffectiveShortScore Then Return "LONG"
        If row.EffectiveShortScore > row.EffectiveLongScore Then Return "SHORT"
        Return "NONE"
    End Function

    ' Ceiling(maxScore × pct) — the shipped Threshold() formula (ScoringEngine_Helpers).
    Private Shared Function Threshold(maxScore As Integer, pct As Double) As Integer
        Return CInt(Math.Ceiling(maxScore * pct))
    End Function

    ''' <summary>Re-derive the canonical verdict for a row under cfg, given the dominant side's
    ''' PLACED target (for the min-move gate). Returns "STRONG LONG" / "LONG" / "WEAK LONG" /
    ''' mirror SHORT / "NO TRADE" — the strings FailureRateMatrix.ToTier keys on. tierBeforeMinMove
    ''' is the STRONG/MEDIUM/WEAK tier the score+MTF alone produced (before the min-move flip),
    ''' so the caller can tally min-move exclusions honestly.</summary>
    Public Shared Function DeriveVerdict(row As CsvRow, cfg As EngineSettings,
                                         dominant As String, placedDomTarget As Double,
                                         ByRef tierBeforeMinMove As String) As String
        tierBeforeMinMove = ""
        Dim regimeMax As Integer = row.MaxScore
        Dim tStrong As Integer = Threshold(regimeMax, cfg.Scoring.VerdictStrongPct)
        Dim tMed As Integer = Threshold(regimeMax, cfg.Scoring.VerdictMedPct)
        Dim tWeak As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)

        Dim effLS As Integer = row.EffectiveLongScore
        Dim effSS As Integer = row.EffectiveShortScore
        Dim domScore As Integer = If(dominant = "LONG", effLS, If(dominant = "SHORT", effSS, 0))
        Dim directional As Boolean = dominant <> "NONE" AndAlso domScore >= tWeak

        ' Step 4b — MTF hard veto (direction-aware). mtf_gate.enabled is live (not whitelisted).
        Dim gatePassDominant As Boolean = True
        If dominant = "LONG" Then
            gatePassDominant = row.MtfGatePassLong
        ElseIf dominant = "SHORT" Then
            gatePassDominant = row.MtfGatePassShort
        End If
        If cfg.MTFGate.Enabled AndAlso directional AndAlso Not gatePassDominant Then
            Return "NO TRADE"
        End If

        ' Step 5 — dominant-side tier walk.
        Dim verdict As String = "NO TRADE"
        If dominant = "LONG" Then
            If effLS >= tStrong Then
                verdict = "STRONG LONG"
            ElseIf effLS >= tMed Then
                verdict = "LONG"
            ElseIf effLS >= tWeak Then
                verdict = "WEAK LONG"
            End If
        ElseIf dominant = "SHORT" Then
            If effSS >= tStrong Then
                verdict = "STRONG SHORT"
            ElseIf effSS >= tMed Then
                verdict = "SHORT"
            ElseIf effSS >= tWeak Then
                verdict = "WEAK SHORT"
            End If
        End If
        tierBeforeMinMove = verdict

        ' Step 5c — min-move gate on the PLACED target (the arbitration output, not the ATR approx).
        If dominant <> "NONE" AndAlso Not verdict.StartsWith("NO TRADE") Then
            Dim floorDist As Double = cfg.Scoring.MinTradeableMovePct * row.Price
            Dim effDist As Double = Math.Abs(placedDomTarget - row.Price)
            If floorDist > 0 AndAlso effDist < floorDist Then
                Return "NO TRADE"
            End If
        End If
        Return verdict
    End Function

    ''' <summary>Replay every eligible row under cfg: recompute placed levels via the shipped
    ''' arbitration, re-derive the verdict, and emit a cloned CsvRow (verdict + Placed* replaced)
    ''' ready for FailureRateMatrix, plus the directional EV samples. evalWindowBars is the
    ''' bar-count budget (5/10/15); the actual window key = evalWindowBars × row resolution.</summary>
    Public Shared Function RunCell(rows As List(Of CsvRow), cfg As EngineSettings,
                                   evalWindowBars As Integer,
                                   Optional keepRows As Boolean = True) As WhatIfCellRun
        ' keepRows=False for the grid-ranking pass (thousands of rows × up to 1,000 cells):
        ' the replayed CsvRows are only needed for the winner + baseline tier matrices, so
        ' ranking cells retain just the EV samples + tallies.
        Dim run As New WhatIfCellRun()
        Dim vDummy As New VerdictResult()   ' structural path ignores v; a placeholder is safe

        For Each row In rows
            Dim r = BuildIndicator(row)
            Dim placedLong = SignalEmitter.ComputeSideLevels(vDummy, r, cfg, isLong:=True)
            Dim placedShort = SignalEmitter.ComputeSideLevels(vDummy, r, cfg, isLong:=False)

            Dim dominant As String = DominantSide(row)
            Dim placedDomTarget As Double = If(dominant = "LONG", placedLong.Target,
                                               If(dominant = "SHORT", placedShort.Target, 0))
            Dim tierBefore As String = ""
            Dim verdict As String = DeriveVerdict(row, cfg, dominant, placedDomTarget, tierBefore)

            ' Clone the row with the replayed verdict + placed geometry. FailureRateMatrix reads
            ' Verdict / Price / ATR / PlacedStop{Long,Short} (HasPlaced) / ForwardBars / ExecResolution.
            Dim rep As New CsvRow() With {
                .Index = row.Index,
                .Timestamp = row.Timestamp,
                .Price = row.Price,
                .ATR = row.ATR,
                .Regime = row.Regime,
                .ExecResolution = row.ExecResolution,
                .Verdict = verdict,
                .SwingStopLong = row.SwingStopLong,
                .SwingStopShort = row.SwingStopShort,
                .PlacedTargetLong = placedLong.Target,
                .PlacedStopLong = placedLong.StopPx,
                .PlacedTargetShort = placedShort.Target,
                .PlacedStopShort = placedShort.StopPx,
                .HasPlaced = True,
                .ForwardBars = row.ForwardBars
            }
            If keepRows Then run.ReplayedRows.Add(rep)

            ' Min-move exclusion tally: a STRONG/MEDIUM tier the min-move gate flipped to NO TRADE.
            Dim tradeableTier As Boolean = (tierBefore = "STRONG LONG" OrElse tierBefore = "LONG" OrElse
                                            tierBefore = "STRONG SHORT" OrElse tierBefore = "SHORT")
            If tradeableTier AndAlso verdict = "NO TRADE" Then
                run.BelowMinMoveExcluded += 1
            End If

            ' Directional (STRONG/MEDIUM) + EV sample.
            Dim tierNow As String = FailureRateMatrix.CanonicalTier(verdict)
            If tierNow <> "" Then
                run.DirectionalCount += 1
                Dim ev As Double? = ComputeEvAtr(rep, dominant, placedDomTarget,
                                                 If(dominant = "LONG", placedLong.StopPx, placedShort.StopPx),
                                                 evalWindowBars)
                If ev.HasValue Then
                    run.EvSamples.Add(New WhatIfEvSample With {
                        .Timestamp = row.Timestamp,
                        .SessionName = SessionFor(cfg, row.Timestamp.Hour),
                        .Resolution = row.ExecResolution,
                        .Tier = tierNow,
                        .EvAtr = ev.Value})
                End If
            End If
        Next
        Return run
    End Function

    ' Per-trade EV in ATR units (§3b ranking objective). Reuses the shipped WalkBars for the
    ' outcome classification, then maps: target-touch → +targetDist; stop/ambiguous → −stopDist;
    ' window-expiry → mark-to-window-end. All distances normalised by ATR. Nothing = no window data.
    Private Shared Function ComputeEvAtr(rep As CsvRow, dominant As String,
                                         target As Double, stopPx As Double,
                                         evalWindowBars As Integer) As Double?
        If rep.ATR <= 0 Then Return Nothing
        Dim isLong As Boolean = (dominant = "LONG")
        Dim windowKey As Integer = evalWindowBars * If(rep.ExecResolution <= 0, 1, rep.ExecResolution)
        Dim bars As List(Of OhlcBar) = Nothing
        If Not rep.ForwardBars.TryGetValue(windowKey, bars) OrElse bars Is Nothing OrElse bars.Count = 0 Then
            Return Nothing
        End If

        Dim entry As Double = rep.Price
        Dim targetDistAtr As Double = Math.Abs(target - entry) / rep.ATR
        Dim stopDistAtr As Double = Math.Abs(entry - stopPx) / rep.ATR

        Select Case FailureRateMatrix.WalkBars(bars, target, stopPx, isLong)
            Case "SUCCESS" : Return targetDistAtr
            Case "ADVERSE_HIT", "AMBIGUOUS" : Return -stopDistAtr
            Case Else       ' WINDOW_EXPIRED → mark-to-window-end
                Dim endClose As Double = bars.Last().Close
                Return (If(isLong, endClose - entry, entry - endClose)) / rep.ATR
        End Select
    End Function

    Private Shared Function SessionFor(cfg As EngineSettings, utcHour As Integer) As String
        Dim b = ExecutionResolution.MatchSessionBucket(cfg, utcHour)
        Return If(b IsNot Nothing, b.Name, "UNKNOWN")
    End Function
End Class
