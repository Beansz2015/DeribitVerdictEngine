# Spec: Kelly Criterion Position Sizing Display
**Proposed:** 2026-04-14
**Status:** AWAITING REVIEW
**Target files:** `Core/ScoringEngine_Types.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Render.vb`, `settings.json`

---

## 1. Problem Statement

The engine currently emits entry/stop/target levels and an R:R ratio but provides no guidance
on **how much** to trade. A trader with a $1,000 account seeing `R:R 1:1.7` has no systematic
basis for sizing the position — they are forced to guess or use a fixed arbitrary amount.

Kelly Criterion provides a mathematically grounded position sizing fraction based on edge
(win probability) and reward/risk ratio. Integrating it as a **display-only advisory** block
below the ATR entry levels gives the trader a per-run sizing recommendation without changing
any scoring, verdicts, or stop/target calculations.

---

## 2. Kelly Formula

Standard Kelly fraction:

    f* = (b*p - q) / b

Where:
- `b` = net odds = AtrTargetMultiplier / AtrStopMultiplier (R:R ratio, e.g. 2.0/1.2 = 1.667)
- `p` = estimated win probability (see Section 3)
- `q` = 1 - p
- `f*` = fraction of account to risk on this trade

Half-Kelly (recommended default):

    f_half = f* / 2

Hard cap (safety floor regardless of Kelly output):

    f_applied = Min(f_half, cfg.Kelly.MaxRiskFraction)   ' default 0.05 = 5%

If `f*` <= 0, Kelly signals no edge — display `NO EDGE` and suppress contract recommendation.

---

## 3. Win Probability Estimation (p)

Two modes, selected automatically based on CalibrationReport status:

### Mode A: ESTIMATED (pre-calibration)

Used when `CalibrationReport.Status != READY` (fewer than the required trade samples).
Derives `p` from the score ratio, scaled to a conservative probability band:

    scoreRatio = effectiveLongScore / maxScore       ' e.g. 11/19 = 0.579
    p_raw      = 0.45 + (scoreRatio * 0.20)          ' maps [0,1] -> [0.45, 0.65]
    p          = Clamp(p_raw, 0.45, 0.65)

Rationale for the band:
- Floor 0.45: below 45% the system has no positive edge; Kelly will return <= 0 at typical R:R
- Ceiling 0.65: prevents overconfidence before real calibration data exists
- Linear scaling within the band preserves score differentiation without over-stating confidence

Label displayed in UI: `p=0.553 [EST]`

### Mode B: CALIBRATED (post-calibration)

Used when `CalibrationReport.Status = READY`. Reads per-tier historical win rate from
`CalibrationReport.WinRateByVerdict` — a dictionary populated from `analysis_log.csv`:

    p = CalibrationReport.WinRateByVerdict(v.Verdict)  ' e.g. LONG -> 0.57

Label displayed in UI: `p=0.570 [CAL]`

Fallback: if the specific verdict tier has fewer than `cfg.Kelly.MinCalibrationSamples` (default 30)
recorded trades, fall back to Mode A for that tier and label as `[EST]`.

---

## 4. Contract Size Calculation

Deribit BTC-PERPETUAL contract size = **$10 USD per contract** (index-denominated).

    stopDistanceUsd = Abs(entryPrice - stopPrice)              ' ATR-derived stop distance in USD
    riskDollars     = accountSize * f_applied                  ' e.g. $1000 * 0.043 = $43
    contracts       = Floor(riskDollars / stopDistanceUsd * entryPrice / 10)

Note: `stopDistanceUsd` is in price points. Converting to USD per contract:
    dollarRiskPerContract = stopDistanceUsd / entryPrice * 10  ' contract value in USD at entry
    contracts             = Floor(riskDollars / dollarRiskPerContract)

Minimum display: 1 contract. If Kelly outputs 0 contracts (risk budget too small for 1 contract
at current stop distance), display `< 1 contract — skip or reduce stop` in amber.

---

## 5. Worked Example

Inputs:
- Account size: $1,000
- Entry: 70867.5 | Stop: 70796.5 | Target: 71009.5
- AtrStopMultiplier: 1.2 | AtrTargetMultiplier: 2.0 → b = 2.0/1.2 = 1.667
- Score: 11/19, Verdict: LONG, Mode A
- scoreRatio = 11/19 = 0.579
- p = 0.45 + (0.579 * 0.20) = 0.566
- q = 0.434
- f* = (1.667 * 0.566 - 0.434) / 1.667 = (0.943 - 0.434) / 1.667 = 0.305
- f_half = 0.153
- f_applied = Min(0.153, 0.05) = 0.05  ← hard capped
- riskDollars = $1,000 * 0.05 = $50
- stopDistanceUsd = 70867.5 - 70796.5 = 71.0 price points
- dollarRiskPerContract = 71.0 / 70867.5 * 10 = $0.01002 per contract
- contracts = Floor($50 / $0.01002) = Floor(4,990) = 4,990

Wait — this is wrong. BTC-PERPETUAL contracts are $10 face each, so:
    dollarRiskPerContract = (stopDistance / entryPrice) * contractFaceValue
                          = (71.0 / 70867.5) * 10 = $0.01002 per contract

That gives ~4,990 contracts for $50 risk — which is correct for Deribit's $10 contract sizing
(4,990 contracts * $0.01002/contract = $50.00 risk). This is an unusually large number because
the contract face value is only $10. Display as integer with comma formatting: `4,990 contracts`.

For clarity, also display the USD risk amount alongside: `Risk: $50.00 (4,990 contracts)`.

---

## 6. Display Format

Append a `KELLY SIZING` block immediately after the ATR entry levels block in `RenderOutput`.
Only render when verdict is not NO TRADE.

```
KELLY SIZING  (Half-Kelly | cap 5.0% | acct $1,000)
  p-win:  0.566 [EST]  |  f*: 30.5%  |  f-half: 15.3%  |  applied: 5.0% [CAPPED]
  Long:   Risk $50.00  |  Contracts: 4,990
```

If Kelly returns no edge (f* <= 0):
```
KELLY SIZING  (Half-Kelly | cap 5.0% | acct $1,000)
  p-win:  0.453 [EST]  |  f*: -2.1%  |  NO EDGE — position sizing suppressed
```

If contracts < 1:
```
  Long:   Risk $50.00  |  Contracts: < 1  (stop too wide for account size)
```

Colour scheme:
- Header line: C_LABEL
- `[EST]` label: C_WARN (amber)
- `[CAL]` label: C_BULL (green)
- `[CAPPED]` label: C_WARN (amber)
- `NO EDGE`: C_BAD (red)
- Contract count: C_VALUE (white)

---

## 7. Code Changes

### 7a. `Core/ScoringEngine_Types.vb`

Add to `VerdictResult`:

```vb
' Kelly sizing outputs — computed in RenderOutput, not ScoringEngine
' Set here for clean data passing; all fields zero/empty = Kelly suppressed
Public Property KellyF           As Double  ' raw f* (may be negative = no edge)
Public Property KellyFHalf       As Double  ' f*/2
Public Property KellyFApplied    As Double  ' after hard cap
Public Property KellyPWin        As Double  ' p used in calculation
Public Property KellyPMode       As String  ' "EST" or "CAL"
Public Property KellyCapped      As Boolean ' True if MaxRiskFraction cap applied
Public Property KellyContracts   As Integer ' recommended contracts (0 = < 1)
Public Property KellyRiskUsd     As Double  ' dollar risk amount
```

---

### 7b. `Core/Settings/EngineSettings.vb`

Add new `KellySettings` class and property:

```vb
Public Class KellySettings
    ''' <summary>Account size in USD. Default $1,000.</summary>
    Public Property AccountSizeUsd          As Double  = 1000.0
    ''' <summary>Use half-Kelly (True) or full Kelly (False). Default True.</summary>
    Public Property UseHalfKelly            As Boolean = True
    ''' <summary>Hard cap on risk fraction regardless of Kelly output. Default 0.05 = 5%.</summary>
    Public Property MaxRiskFraction         As Double  = 0.05
    ''' <summary>Deribit BTC-PERPETUAL contract face value in USD. Default 10.</summary>
    Public Property ContractFaceUsd         As Double  = 10.0
    ''' <summary>Min logged trades per verdict tier before switching from EST to CAL mode.</summary>
    Public Property MinCalibrationSamples   As Integer = 30
    ''' <summary>Score-to-probability band floor (pre-calibration). Default 0.45.</summary>
    Public Property EstProbFloor            As Double  = 0.45
    ''' <summary>Score-to-probability band scale (pre-calibration). Default 0.20.</summary>
    Public Property EstProbScale            As Double  = 0.20
End Class

' Add to EngineSettings class:
Public Property Kelly As New KellySettings
```

---

### 7c. `settings.json`

Add `kelly` block:

```json
"kelly": {
  "account_size_usd": 1000.0,
  "use_half_kelly": true,
  "max_risk_fraction": 0.05,
  "contract_face_usd": 10.0,
  "min_calibration_samples": 30,
  "est_prob_floor": 0.45,
  "est_prob_scale": 0.20
}
```

---

### 7d. `UI/MainForm_Render.vb`

Add `CalcKellySizing()` private helper and call it in `RenderOutput()` after the ATR block.

```vb
Private Sub CalcKellySizing(v As VerdictResult, cfg As EngineSettings)
    ' Only run for actionable verdicts
    If v.Verdict = "NO TRADE" OrElse v.Verdict = "" Then Return

    Dim isLong As Boolean = v.Verdict.Contains("LONG")
    Dim entryPrice As Double = If(isLong,
        v.AtrLongEntry, v.AtrShortEntry)
    Dim stopPrice  As Double = If(isLong,
        v.AtrLongStop,  v.AtrShortStop)

    ' --- Determine p ---
    Dim pWin  As Double
    Dim pMode As String
    ' TODO: check CalibrationReport.Status and WinRateByVerdict once READY logic is implemented
    ' For now always use EST mode
    Dim scoreRatio As Double = If(v.MaxScore > 0,
        CDbl(v.EffectiveLongScore) / v.MaxScore, 0.5)
    pWin  = Clamp(cfg.Kelly.EstProbFloor + scoreRatio * cfg.Kelly.EstProbScale,
                  cfg.Kelly.EstProbFloor,
                  cfg.Kelly.EstProbFloor + cfg.Kelly.EstProbScale)
    pMode = "EST"

    ' --- Kelly formula ---
    Dim b  As Double = cfg.Scoring.AtrTargetMultiplier / cfg.Scoring.AtrStopMultiplier
    Dim q  As Double = 1.0 - pWin
    Dim fStar As Double = (b * pWin - q) / b

    v.KellyF     = fStar
    v.KellyPWin  = pWin
    v.KellyPMode = pMode

    If fStar <= 0 Then
        v.KellyContracts = 0
        Return
    End If

    Dim fHalf    As Double = fStar / 2.0
    Dim fApplied As Double = Math.Min(fHalf, cfg.Kelly.MaxRiskFraction)
    Dim capped   As Boolean = (fApplied < fHalf)

    v.KellyFHalf    = fHalf
    v.KellyFApplied = fApplied
    v.KellyCapped   = capped

    ' --- Contract calculation ---
    Dim stopDist         As Double = Math.Abs(entryPrice - stopPrice)
    Dim riskUsd          As Double = cfg.Kelly.AccountSizeUsd * fApplied
    Dim riskPerContract  As Double = (stopDist / entryPrice) * cfg.Kelly.ContractFaceUsd
    Dim contracts        As Integer = If(riskPerContract > 0,
        CInt(Math.Floor(riskUsd / riskPerContract)), 0)

    v.KellyRiskUsd   = riskUsd
    v.KellyContracts = contracts
End Sub

Private Shared Function Clamp(value As Double, min As Double, max As Double) As Double
    Return Math.Max(min, Math.Min(max, value))
End Function
```

Rendering in `RenderOutput()` after ATR block:

```vb
' Kelly Sizing block
If v.Verdict <> "NO TRADE" AndAlso v.Verdict <> "" Then
    Dim kellyMode As String = If(cfg.Kelly.UseHalfKelly, "Half-Kelly", "Full Kelly")
    AppendRtf(rtb, $"{Environment.NewLine}KELLY SIZING  ({kellyMode} | cap {cfg.Kelly.MaxRiskFraction:P1} | acct ${cfg.Kelly.AccountSizeUsd:N0}){Environment.NewLine}", C_LABEL)

    If v.KellyF <= 0 Then
        Dim pLabel As Color = If(v.KellyPMode = "CAL", C_BULL, C_WARN)
        AppendRtf(rtb, $"  p-win:  {v.KellyPWin:F3} ", C_VALUE)
        AppendRtf(rtb, $"[{v.KellyPMode}]", pLabel)
        AppendRtf(rtb, $"  |  f*: {v.KellyF:P1}  |  ", C_VALUE)
        AppendRtf(rtb, $"NO EDGE — position sizing suppressed{Environment.NewLine}", C_BAD)
    Else
        Dim pLabel   As Color = If(v.KellyPMode = "CAL", C_BULL, C_WARN)
        Dim capLabel As String = If(v.KellyCapped, "  [CAPPED]", "")
        AppendRtf(rtb, $"  p-win:  {v.KellyPWin:F3} ", C_VALUE)
        AppendRtf(rtb, $"[{v.KellyPMode}]", pLabel)
        AppendRtf(rtb, $"  |  f*: {v.KellyF:P1}  |  f-half: {v.KellyFHalf:P1}  |  applied: {v.KellyFApplied:P1}", C_VALUE)
        If v.KellyCapped Then AppendRtf(rtb, "  [CAPPED]", C_WARN)
        AppendRtf(rtb, Environment.NewLine, C_VALUE)

        Dim direction As String = If(v.Verdict.Contains("LONG"), "Long", "Short")
        If v.KellyContracts < 1 Then
            AppendRtf(rtb, $"  {direction}:   Risk ${v.KellyRiskUsd:F2}  |  ", C_VALUE)
            AppendRtf(rtb, $"Contracts: < 1  (stop too wide for account size){Environment.NewLine}", C_WARN)
        Else
            AppendRtf(rtb, $"  {direction}:   Risk ${v.KellyRiskUsd:F2}  |  Contracts: {v.KellyContracts:N0}{Environment.NewLine}", C_VALUE)
        End If
    End If
End If
```

---

## 8. Files Changed Summary

| File | Change |
|---|---|
| `Core/ScoringEngine_Types.vb` | Add 8 Kelly output fields to `VerdictResult` |
| `Core/Settings/EngineSettings.vb` | Add `KellySettings` class + `Kelly` property to `EngineSettings` |
| `settings.json` | Add `kelly` block (7 keys) |
| `UI/MainForm_Render.vb` | Add `CalcKellySizing()` helper + Kelly block rendering in `RenderOutput()` |

No changes to `ScoringEngine_Calculate.vb`. No new indicators. No CSV logging changes.

---

## 9. Settings Keys Summary

| Key | Default | Purpose |
|---|---|---|
| `account_size_usd` | 1000.0 | Account size in USD |
| `use_half_kelly` | true | Half-Kelly vs full Kelly |
| `max_risk_fraction` | 0.05 | Hard cap (5%) on risk per trade |
| `contract_face_usd` | 10.0 | Deribit BTC-PERPETUAL face value |
| `min_calibration_samples` | 30 | Min trades per tier to use CAL mode |
| `est_prob_floor` | 0.45 | Estimated p floor (pre-calibration) |
| `est_prob_scale` | 0.20 | Estimated p scale range (pre-calibration) |

---

## 10. What This Does NOT Do

- Does **not** change stop or target distances
- Does **not** affect any scoring, verdict, or CalibrationReport logic
- Does **not** auto-execute trades or send orders to Deribit
- Does **not** account for fees/slippage (display is pre-fee gross sizing)
- Does **not** implement fractional Kelly modes beyond half — full Kelly available via `use_half_kelly: false`

---

## 11. Open Questions for Review

1. **CalibrationReport integration** — `WinRateByVerdict` dictionary does not yet exist in
   `AnalysisLogger.vb`. When implementing CAL mode, add a `Dictionary(Of String, Double)` to
   `CalibrationReport` populated from the CSV verdict column win/loss outcomes. Confirm the
   outcome column exists (or add it) in `analysis_log.csv` before implementing CAL mode.
2. **Score direction** — `CalcKellySizing` uses `EffectiveLongScore` for long verdicts.
   Confirm this is the correct field (post-penalty effective score, not raw `LongScore`).
3. **VerdictResult ATR fields** — the helper references `v.AtrLongEntry`, `v.AtrLongStop` etc.
   These fields do not currently exist on `VerdictResult` (ATR values are computed locally in
   `RenderOutput`). Either add them to `VerdictResult` or pass entry/stop prices as parameters
   to `CalcKellySizing`. The latter is simpler.
4. **NO TRADE display** — confirm Kelly block should be fully suppressed on NO TRADE
   (including MTF-blocked verdicts that have a bracket verdict like `NO TRADE [WEAK LONG]`).
5. **Clamp helper** — confirm `Clamp` does not already exist elsewhere in the codebase
   to avoid a duplicate definition.
