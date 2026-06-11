' Core/ScoringEngine_Kelly.vb
' ScoringEngine partial: Kelly sizing helper (display-only).
' No scoring impact. Computes recommended risk fraction / contracts for the
' currently dominant verdict side using ATR stop distance and configured
' account/contract assumptions.

Partial Public Class ScoringEngine

    ' -----------------------------------------------------------------------
    ' Kelly sizing (display-only)
    ' -----------------------------------------------------------------------
    ' Inputs:
    '   v               = VerdictResult to populate
    '   stopDistanceUsd = ATR-derived stop distance in price points (always > 0)
    '   entryPriceUsd   = current price (denominator for inverse-contract risk)
    '   cfg             = Engine settings (kelly block)
    '
    ' Outputs written into v:
    '   KellyF, KellyFHalf, KellyFApplied, KellyPWin, KellyPMode,
    '   KellyCapped, KellyContracts, KellyRiskUsd, KellyLevCapped
    '
    ' Notes:
    ' - Uses EST mode only for now (pre-calibration).
    ' - Silent / zeroed if no directional edge or invalid stop distance/price.
    ' - Deribit BTC-PERPETUAL is an INVERSE contract: the USD loss on a
    '   $face contract over a $Δ price move is face × Δ / price, not face × Δ
    '   (D1/H4 fix — the old form was off by the entry price, ~1e4×, so contracts
    '   were always 0). At correct sizing the $ risk cap is rarely binding, so a
    '   leverage cap (kelly.max_leverage) is applied and surfaced via KellyLevCapped.
    Public Shared Sub CalcKellySizing(v As VerdictResult,
                                      stopDistanceUsd As Double,
                                      entryPriceUsd As Double,
                                      cfg As EngineSettings)

        ' Reset all outputs first so suppression is deterministic.
        v.KellyF = 0.0
        v.KellyFHalf = 0.0
        v.KellyFApplied = 0.0
        v.KellyPWin = 0.0
        v.KellyPMode = ""
        v.KellyCapped = False
        v.KellyContracts = 0
        v.KellyRiskUsd = 0.0
        v.KellyLevCapped = False

        If v Is Nothing OrElse cfg Is Nothing Then Exit Sub
        If stopDistanceUsd <= 0 Then Exit Sub
        If entryPriceUsd <= 0 Then Exit Sub

        ' Empty verdict → no directional edge to size. (The old guard also tested
        ' "NEUTRAL"/"WAIT" — verdict strings retired ~20 versions ago; dead, removed.
        ' v30 intentionally renders the lean Kelly on NO TRADE, so NO TRADE is NOT
        ' suppressed here.)
        Dim verdict As String = If(v.Verdict, "").Trim().ToUpperInvariant()
        If verdict = "" Then Exit Sub

        ' ---------------------------------------------------------------
        ' Step 1: Estimate p(win) from confidence tier (EST mode only).
        ' Mapping per approved spec:
        '   HIGH   -> 0.45 + 0.20 = 0.65
        '   MEDIUM -> 0.45 + 0.10 = 0.55
        '   LOW    -> 0.45 + 0.00 = 0.45
        ' ---------------------------------------------------------------
        Dim p As Double = cfg.Kelly.EstProbFloor
        Select Case If(v.Confidence, "").Trim().ToUpperInvariant()
            Case "HIGH"   : p += cfg.Kelly.EstProbScale
            Case "MEDIUM" : p += (cfg.Kelly.EstProbScale / 2.0)
            Case Else      : p += 0.0
        End Select

        Dim q As Double = 1.0 - p

        ' ---------------------------------------------------------------
        ' Step 2: Determine payoff ratio b using fixed ATR target/stop ratio.
        ' From current spec/settings: targetMult / stopMult.
        ' ---------------------------------------------------------------
        If cfg.Scoring.AtrStopMultiplier <= 0 Then Exit Sub
        Dim b As Double = cfg.Scoring.AtrTargetMultiplier / cfg.Scoring.AtrStopMultiplier
        If b <= 0 Then Exit Sub

        ' Kelly fraction: f* = (b*p - q) / b
        Dim fStar As Double = ((b * p) - q) / b
        v.KellyF = fStar
        v.KellyPWin = p
        v.KellyPMode = "EST"

        ' No edge -> silent block
        If fStar <= 0 Then Exit Sub

        ' Half-Kelly and hard cap
        Dim fHalf As Double = If(cfg.Kelly.UseHalfKelly, fStar / 2.0, fStar)
        Dim fApplied As Double = Math.Min(fHalf, cfg.Kelly.MaxRiskFraction)

        v.KellyFHalf = fHalf
        v.KellyFApplied = fApplied
        v.KellyCapped = (fHalf > cfg.Kelly.MaxRiskFraction)
        v.KellyRiskUsd = cfg.Kelly.AccountSizeUsd * fApplied

        ' ---------------------------------------------------------------
        ' Step 3: Contract sizing (inverse contract)
        ' Risk per contract = face × stopDistance / entryPrice  (USD).
        ' Final contracts = min(risk-derived, leverage-derived); whole only.
        ' ---------------------------------------------------------------
        Dim riskPerContractUsd As Double = cfg.Kelly.ContractFaceUsd * stopDistanceUsd / entryPriceUsd
        If riskPerContractUsd <= 0 Then Exit Sub

        Dim contractsByRisk As Integer = CInt(Math.Floor(v.KellyRiskUsd / riskPerContractUsd))
        If contractsByRisk < 0 Then contractsByRisk = 0

        ' Leverage cap: max contracts before notional exceeds account × max_leverage.
        Dim maxContractsByLeverage As Integer = 0
        If cfg.Kelly.ContractFaceUsd > 0 Then
            maxContractsByLeverage = CInt(Math.Floor(
                cfg.Kelly.AccountSizeUsd * cfg.Kelly.MaxLeverage / cfg.Kelly.ContractFaceUsd))
        End If

        If maxContractsByLeverage > 0 AndAlso maxContractsByLeverage < contractsByRisk Then
            v.KellyContracts = maxContractsByLeverage
            v.KellyLevCapped = True
        Else
            v.KellyContracts = contractsByRisk
        End If
    End Sub

End Class
