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
                    res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
                    res.HoldStatus = CalcHoldStatus(r, posState, cfg)
                    Return res
                End If
            Case "TRENDING_DOWN"
                If ls > ss Then
                    res.Verdict = AppendLean("NO TRADE", ls, ss, tWeakEarly)
                    res.Confidence = "N/A"
                    res.LongScore = ls : res.ShortScore = ss
                    res.EffectiveLongScore = ls : res.EffectiveShortScore = ss
                    res.RegimePenalty = 0
                    res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
                    res.HoldStatus = CalcHoldStatus(r, posState, cfg)
                    Return res
                End If
            Case "TRANSITIONAL"
                Dim penLow As Double = cfg.RegimeGates.TransitionalAdxPenaltyLow
                Dim penMid As Double = cfg.RegimeGates.TransitionalAdxPenaltyMid
                If r.ADX >= penLow AndAlso r.ADX < penMid Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyLow
                ElseIf r.ADX >= penMid AndAlso r.ADX < cfg.RegimeGates.TransitionalAdxPenaltyHigh Then
                    adxPenalty = cfg.RegimeGates.TransitionalPenaltyMid
                End If
                effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls, cfg))
                effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss, cfg))
        End Select

        ' -- Step 4b: MTF Gate Veto -------------------------------------------
        Dim tWeakCheck  As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)
        Dim proposedDir As String = "NONE"
        If effectiveLS >= tWeakCheck AndAlso effectiveLS >= effectiveSS Then
            proposedDir = "LONG"
        ElseIf effectiveSS >= tWeakCheck AndAlso effectiveSS > effectiveLS Then
            proposedDir = "SHORT"
        End If

        Dim mtfBlocked As Boolean = False
        If cfg.MTFGate.Enabled AndAlso proposedDir <> "NONE" AndAlso Not r.MTFGatePass Then
            mtfBlocked = True
        End If

        res.SignalBreakdown.Add(New SignalBreakdownItem("MTF Gate (15m)",
            r.MTFGatePass AndAlso proposedDir = "LONG",
            r.MTFGatePass AndAlso proposedDir = "SHORT",
            r.MTFGateReason))

        If mtfBlocked Then
            res.Verdict = AppendLean("NO TRADE", effectiveLS, effectiveSS, tWeakCheck)
            res.Confidence = "N/A"
            res.LongScore = ls : res.ShortScore = ss
            res.EffectiveLongScore = effectiveLS : res.EffectiveShortScore = effectiveSS
            res.RegimePenalty = adxPenalty
            res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
            res.HoldStatus = CalcHoldStatus(r, posState, cfg)
            Return res
        End If

        ' -- Step 5: Generate Verdict -----------------------------------------
        res.LongScore = ls
        res.ShortScore = ss
        res.EffectiveLongScore = effectiveLS
        res.EffectiveShortScore = effectiveSS
        res.RegimePenalty = adxPenalty

        Dim tStrong As Integer = Threshold(regimeMax, cfg.Scoring.VerdictStrongPct)
        Dim tMed    As Integer = Threshold(regimeMax, cfg.Scoring.VerdictMedPct)
        Dim tWeak   As Integer = Threshold(regimeMax, cfg.Scoring.VerdictWeakPct)

        If effectiveLS >= tStrong Then
            res.Verdict = "STRONG LONG"  : res.Confidence = "HIGH"
        ElseIf effectiveLS >= tMed Then
            res.Verdict = "LONG"          : res.Confidence = "MEDIUM"
        ElseIf effectiveLS >= tWeak Then
            res.Verdict = "WEAK LONG"     : res.Confidence = "LOW"
        ElseIf effectiveSS >= tStrong Then
            res.Verdict = "STRONG SHORT"  : res.Confidence = "HIGH"
        ElseIf effectiveSS >= tMed Then
            res.Verdict = "SHORT"         : res.Confidence = "MEDIUM"
        ElseIf effectiveSS >= tWeak Then
            res.Verdict = "WEAK SHORT"    : res.Confidence = "LOW"
        Else
            res.Verdict = AppendLean("NO TRADE", ls, ss, tWeak)
            res.Confidence = "N/A"
        End If

        res.VerdictContext = CalcVerdictContext(res, r, state, cfg)
        res.HoldStatus = CalcHoldStatus(r, posState, cfg)

        ' -- Step 5b: ATR Target Cap (VPFR HVN) --------------------------------
        Dim atrTarget As Double = r.ATR * norms.ATRScaleFactor * cfg.Scoring.AtrTargetMultiplier
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

        Return res
    End Function

End Class
