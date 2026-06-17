' Core/ScoringEngine_Calculate_Verdict.vb
' ScoringEngine partial: Calculate() entry point, Steps 4/4b regime/MTF veto,
' Step 5 verdict generation, ATR target cap.
' Split from ScoringEngine_Calculate.vb for maintainability.
'
' Depends on:
'   ScoringEngine_Calculate_Scoring.vb  (RunScoringPipeline, CalcVerdictContext, AppendLean)
'   ScoringEngine_Helpers.vb            (Threshold, TierFloor, RegimeMaxScore, CalcHoldStatus)
'   ScoringEngine_Types.vb              (VerdictResult, ScoreState, PositionState)

Partial Public Class ScoringEngine

    Public Shared Function Calculate(r As IndicatorResults, posState As PositionState,
                                     norms As DynamicNorms,
                                     cfg As EngineSettings) As VerdictResult
        Dim res As New VerdictResult()
        Dim state As New ScoreState()

        Dim regimeMax As Integer = RegimeMaxScore(r.Regime, cfg)
        res.MaxScore = regimeMax

        ' -- Steps 2 / Pass 2 / Steps 3/3b -----------------------------------
        Dim ls As Integer = 0
        Dim ss As Integer = 0
        RunScoringPipeline(r, norms, cfg, res, state, ls, ss)

        ' -- Step 4: Regime Veto / Override -----------------------------------
        Dim effectiveLS As Integer = ls
        Dim effectiveSS As Integer = ss
        Dim adxPenalty  As Integer = 0
        Dim tWeakEarly  As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)

        Select Case r.Regime
            Case "TRENDING_UP"
                If ss > ls Then
                    res.Verdict = AppendLean("NO TRADE", ls, ss, tWeakEarly)
                    res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    ' Regime veto pre-empts the MTF check — keep the breakdown
                    ' row present on this path too (no-direction format).
                    res.MTFGateReason = "MTF state: " & r.MTF15mTrend & " | " & r.MTFGateDetails
                    res.SignalBreakdown.Add(New SignalBreakdownItem("MTF Gate (15m)", False, False, res.MTFGateReason, 0, 0))
                    res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
                    res.HoldStatus = CalcHoldStatus(r, posState, cfg)
                    CheckLedger(res)
                    Return res
                End If
            Case "TRENDING_DOWN"
                If ls > ss Then
                    res.Verdict = AppendLean("NO TRADE", ls, ss, tWeakEarly)
                    res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.MTFGateReason = "MTF state: " & r.MTF15mTrend & " | " & r.MTFGateDetails
                    res.SignalBreakdown.Add(New SignalBreakdownItem("MTF Gate (15m)", False, False, res.MTFGateReason, 0, 0))
                    res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
                    res.HoldStatus = CalcHoldStatus(r, posState, cfg)
                    CheckLedger(res)
                    Return res
                End If
            Case "TRANSITIONAL"
                ' ADX-proximity scale: the further below the trend threshold,
                ' the heavier the penalty. The first arm covers everything
                ' below the mid boundary — ADX under 20 (reachable on the
                ' regime-hysteresis grace bar) is the WEAKEST reading and must
                ' get the full penalty, not fall through to zero.
                Dim penMid As Double = cfg.RegimeGates.TransitionalAdxPenaltyMid
                If r.ADX < penMid Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyLow
                ElseIf r.ADX < cfg.RegimeGates.TransitionalAdxPenaltyHigh Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyMid
                End If
                effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls, cfg))
                effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss, cfg))
        End Select

        ' -- Dominant side (shared by Step 4b and Step 5) ----------------------
        ' Determined once from the effective scores. A tie carries no
        ' directional information → dominant NONE → NO TRADE at Step 5.
        Dim dominant As String = "NONE"
        If effectiveLS > effectiveSS Then
            dominant = "LONG"
        ElseIf effectiveSS > effectiveLS Then
            dominant = "SHORT"
        End If

        Dim tWeakCheck    As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)
        Dim dominantScore As Integer = If(dominant = "LONG", effectiveLS, If(dominant = "SHORT", effectiveSS, 0))
        Dim directional   As Boolean = dominant <> "NONE" AndAlso dominantScore >= tWeakCheck

        ' -- Step 4b: MTF Gate Veto (direction-aware) ---------------------------
        ' Hard-veto invariant: the 15m trend must align with the VERDICT
        ' direction. The per-side flags from CalcMTFGate are consulted against
        ' the dominant side. The final reason is composed here — the single
        ' string every consumer (MTF card, snapshot, CSV, breakdown row) renders.
        Dim gatePassDominant As Boolean = True
        If dominant = "LONG" Then
            gatePassDominant = r.MTFGatePassLong
        ElseIf dominant = "SHORT" Then
            gatePassDominant = r.MTFGatePassShort
        End If

        Dim gateFails  As Boolean = directional AndAlso Not gatePassDominant
        Dim mtfBlocked As Boolean = cfg.MTFGate.Enabled AndAlso gateFails

        If directional Then
            If gateFails Then
                res.MTFGateReason = String.Format("MTF BLOCK [{0} vs {1}] {2}", dominant, r.MTF15mTrend, r.MTFGateDetails)
            Else
                res.MTFGateReason = String.Format("MTF PASS [{0}] {1}", dominant, r.MTFGateDetails)
            End If
        Else
            res.MTFGateReason = "MTF state: " & r.MTF15mTrend & " | " & r.MTFGateDetails
        End If
        res.MTFGateBlocked = mtfBlocked

        res.SignalBreakdown.Add(New SignalBreakdownItem("MTF Gate (15m)",
            directional AndAlso dominant = "LONG" AndAlso gatePassDominant,
            directional AndAlso dominant = "SHORT" AndAlso gatePassDominant,
            res.MTFGateReason, 0, 0))

        If mtfBlocked Then
            res.Verdict = AppendLean("NO TRADE", effectiveLS, effectiveSS, tWeakCheck)
            res.Confidence = "N/A"
            res.LongScore = ls : res.ShortScore = ss
            res.EffectiveLongScore = effectiveLS : res.EffectiveShortScore = effectiveSS
            res.RegimePenalty = adxPenalty
            res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
            res.HoldStatus = CalcHoldStatus(r, posState, cfg)
            CheckLedger(res)
            Return res
        End If

        ' -- Step 5: Generate Verdict (dominant side only) ----------------------
        ' Only the dominant side's tiers are walked; the dominated side cannot
        ' produce a verdict even if it clears a tier the dominant side misses.
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty

        Dim tStrong As Integer = Threshold(regimeMax, cfg.Scoring.VerdictStrongPct)
        Dim tMed    As Integer = Threshold(regimeMax, cfg.Scoring.VerdictMedPct)
        Dim tWeak   As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)

        Select Case dominant
            Case "LONG"
                If effectiveLS >= tStrong Then
                    res.Verdict = "STRONG LONG"  : res.Confidence = "HIGH"
                ElseIf effectiveLS >= tMed Then
                    res.Verdict = "LONG"          : res.Confidence = "MEDIUM"
                ElseIf effectiveLS >= tWeak Then
                    res.Verdict = "WEAK LONG"     : res.Confidence = "LOW"
                Else
                    res.Verdict = AppendLean("NO TRADE", ls, ss, tWeak)
                    res.Confidence = "N/A"
                End If
            Case "SHORT"
                If effectiveSS >= tStrong Then
                    res.Verdict = "STRONG SHORT"  : res.Confidence = "HIGH"
                ElseIf effectiveSS >= tMed Then
                    res.Verdict = "SHORT"         : res.Confidence = "MEDIUM"
                ElseIf effectiveSS >= tWeak Then
                    res.Verdict = "WEAK SHORT"    : res.Confidence = "LOW"
                Else
                    res.Verdict = AppendLean("NO TRADE", ls, ss, tWeak)
                    res.Confidence = "N/A"
                End If
            Case Else
                res.Verdict = AppendLean("NO TRADE", ls, ss, tWeak)
                res.Confidence = "N/A"
        End Select

        res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
        res.HoldStatus = CalcHoldStatus(r, posState, cfg)

        ' -- Step 5b: ATR Target Cap (VPFR HVN) --------------------------------
        ' D2 (S-1): levels are LINEAR in ATR — distance = ATR × multiplier. The
        ' old quadratic form (× norms.ATRScaleFactor) was dropped so the cap base
        ' matches the linear display and the eval pipeline's raw-ATR barriers.
        Dim atrTarget As Double = r.ATR * cfg.Scoring.AtrTargetMultiplier
        Dim rawLongTarget  As Double = r.CurrentPrice + atrTarget
        Dim rawShortTarget As Double = r.CurrentPrice - atrTarget

        res.AdjustedLongTarget   = 0
        res.AdjustedShortTarget  = 0
        res.TargetCapReasonLong  = ""
        res.TargetCapReasonShort = ""

        Dim hvnAbove As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR")
        Dim hvnBelow As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL")

        ' 3-tier cap arbitration (long): swing target → nearest HVN → POC.
        ' Fires when any qualifier is closer than the raw ATR target.
        ' Winner = closest to entry (minimum value above current price).
        Dim capLongTarget As Double = 0
        Dim capLongLabel  As String = ""

        ' Tier 1: swing target -- highest priority
        If r.SwingTargetLong > 0 AndAlso r.SwingTargetLong < rawLongTarget Then
            capLongTarget = r.SwingTargetLong
            capLongLabel  = "SWING_HIGH_5M"
        End If

        ' Tier 2: nearest HVN above (VPFR-lite v2)
        If r.VPFRNearestHvnAbove > 0 AndAlso r.VPFRNearestHvnAbove > r.CurrentPrice AndAlso
           r.VPFRNearestHvnAbove < rawLongTarget AndAlso
           (capLongTarget = 0 OrElse r.VPFRNearestHvnAbove < capLongTarget) Then
            capLongTarget = r.VPFRNearestHvnAbove
            capLongLabel  = "NEAREST_HVN_ABOVE"
        End If

        ' Tier 3: POC fallback
        If hvnAbove AndAlso r.VPFRPoc > r.CurrentPrice AndAlso r.VPFRPoc < rawLongTarget AndAlso
           (capLongTarget = 0 OrElse r.VPFRPoc < capLongTarget) Then
            capLongTarget = r.VPFRPoc
            capLongLabel  = "POC"
        End If

        If capLongTarget > 0 Then
            res.AdjustedLongTarget  = capLongTarget
            res.TargetCapReasonLong = String.Format("CAPPED @ {0:F1} ({1})", capLongTarget, capLongLabel)
        End If

        ' 3-tier cap arbitration (short): swing target → nearest HVN → POC.
        ' Winner = closest to entry (maximum value below current price).
        Dim capShortTarget As Double = 0
        Dim capShortLabel  As String = ""

        ' Tier 1: swing target -- highest priority
        If r.SwingTargetShort > 0 AndAlso r.SwingTargetShort > rawShortTarget Then
            capShortTarget = r.SwingTargetShort
            capShortLabel  = "SWING_LOW_5M"
        End If

        ' Tier 2: nearest HVN below (VPFR-lite v2)
        If r.VPFRNearestHvnBelow > 0 AndAlso r.VPFRNearestHvnBelow < r.CurrentPrice AndAlso
           r.VPFRNearestHvnBelow > rawShortTarget AndAlso
           (capShortTarget = 0 OrElse r.VPFRNearestHvnBelow > capShortTarget) Then
            capShortTarget = r.VPFRNearestHvnBelow
            capShortLabel  = "NEAREST_HVN_BELOW"
        End If

        ' Tier 3: POC fallback
        If hvnBelow AndAlso r.VPFRPoc < r.CurrentPrice AndAlso r.VPFRPoc > rawShortTarget AndAlso
           (capShortTarget = 0 OrElse r.VPFRPoc > capShortTarget) Then
            capShortTarget = r.VPFRPoc
            capShortLabel  = "POC"
        End If

        If capShortTarget > 0 Then
            res.AdjustedShortTarget   = capShortTarget
            res.TargetCapReasonShort  = String.Format("CAPPED @ {0:F1} ({1})", capShortTarget, capShortLabel)
        End If

        ' -- Step 5c: Minimum-Tradeable-Move Gate (v35) -------------------------
        ' A directional verdict whose realistic take-profit can't clear the
        ' minimum tradeable move (cfg.Scoring.MinTradeableMovePct of entry — sized
        ' to clear slippage) is overridden to NO TRADE. The check uses the
        ' EFFECTIVE (post-cap) target so it catches BOTH causes: a small raw ATR
        ' target (low vol) AND a near structural swing that the Step 5b cap pulled
        ' below the floor. Scores, breakdown and the computed levels are preserved
        ' for display — only the verdict flips, and BELOW_MIN_MOVE records why.
        ' Mirrors the MTF veto: compute everything, then override, then return.
        ' Shared floor with the eval-metric de-confound; off the auto-tweaker
        ' surface (trader risk preference, never auto-tuned). See
        ' docs/min-tradeable-move-gate-proposal.md.
        If dominant <> "NONE" AndAlso res.Verdict IsNot Nothing AndAlso
           Not res.Verdict.StartsWith("NO TRADE") Then
            Dim floorDist As Double = cfg.Scoring.MinTradeableMovePct * r.CurrentPrice
            Dim effTarget As Double = If(dominant = "LONG",
                                         If(res.AdjustedLongTarget > 0, res.AdjustedLongTarget, rawLongTarget),
                                         If(res.AdjustedShortTarget > 0, res.AdjustedShortTarget, rawShortTarget))
            Dim effDist As Double = Math.Abs(effTarget - r.CurrentPrice)
            If floorDist > 0 AndAlso effDist < floorDist Then
                res.Verdict = "NO TRADE"
                res.Confidence = "N/A"
                res.VerdictContext = "BELOW_MIN_MOVE"
            End If
        End If

        CheckLedger(res)
        Return res
    End Function

    ' -- Permanent SC/TOTAL ledger guard (Spec C, S5 amendment) --------------
    ' The signed SignalBreakdown points must sum to the raw scores (through
    ' Step 3b — NOT the Step-4 effective scores). A mismatch means a scoring
    ' contribution was mis-attributed at an emission site — the checked property
    ' that makes a silent double-count (the trader profile's #1 banned pattern)
    ' impossible to ship. Runs in all builds before every Return in Calculate();
    ' host-agnostic (survives the CLI port). Cost: one integer sum over ~25 items.
    Private Shared Sub CheckLedger(res As VerdictResult)
        Dim sumLng As Integer = 0, sumSht As Integer = 0
        For Each it In res.SignalBreakdown
            If it Is Nothing Then Continue For
            sumLng += it.LongPoints
            sumSht += it.ShortPoints
        Next
        If sumLng <> res.LongScore OrElse sumSht <> res.ShortScore Then
            res.LedgerMismatch = True
            Console.WriteLine(String.Format(
                "[LEDGER_MISMATCH] ΣLong={0} LongScore={1} | ΣShort={2} ShortScore={3} (verdict={4})",
                sumLng, res.LongScore, sumSht, res.ShortScore, res.Verdict))
        End If
    End Sub

End Class
