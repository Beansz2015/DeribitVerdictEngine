# Spec: Funding Rate Momentum Signal
**Proposed:** 2026-04-17
**Status:** APPROVED — open questions resolved 2026-04-18
**Target files:** `Core/IndicatorResults.vb`, `Core/Indicators_OrderFlow.vb`, `Core/ScoringEngine_Calculate.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Layout.vb`, `UI/MainForm_Analysis.vb`, `settings.json`

---

## 1. Problem Statement

The current funding rate modifier (Step 3) treats funding as a flat, one-dimensional threshold:
if the rate exceeds a configured level, a fixed penalty or boost fires. This misses the
directional momentum of funding — a rate rising quickly toward the high threshold is a
crowding signal *before* the penalty fires, while a rate falling away from an extreme is a
de-crowding signal *before* the boost recovers.

Concretely:

- Funding at +0.006% and rising → longs are accumulating leverage at pace; a squeeze is building
- Funding at +0.008% and falling → crowding is unwinding; the penalty is still firing but conditions are improving for longs

Both scenarios trigger the same `FundingHighPositive` penalty today. The momentum dimension
is being discarded.

Adding a `FundingMomentum` field (RISING / FALLING / FLAT) to `IndicatorResults`, computed
from a short rolling window of funding samples, allows Step 3 to apply a modifier *on top of*
the existing threshold penalties — amplifying the penalty when momentum confirms crowding and
softening it when momentum signals relief. A pre-emptive soft penalty also fires when funding
is in the neutral zone but momentum is RISING toward `FundingLowPositive`.

Zero scoring architecture changes. Zero new API calls. No new data sources.

---

## 2. FundingMomentum Computation

### 2a. Data Source

`GetFundingRateAsync()` in `DeribitClient.vb` already returns the current funding rate.
Momentum is derived from a ring buffer of the last `FundingHistoryMax` (10) samples maintained
in `MainForm_Layout.vb` alongside `_oiHistory` — the canonical home for shared live-run state.

### 2b. Signal Logic

```
Given: fundingHistory = List(Of Double), most recent last
       cfg.Indicators.Funding.MomentumWindow    (default 3)
       cfg.Indicators.Funding.MomentumThreshold (default 0.0001)

Compute:
  If fundingHistory.Count < 2 → FundingMomentum = "FLAT"   ← cold-start fallback, accepted

  recent   = fundingHistory.Last()
  priorIdx = Math.Max(0, fundingHistory.Count - 1 - MomentumWindow)
  delta    = recent - prior

  If delta >  MomentumThreshold → "RISING"
  If delta < -MomentumThreshold → "FALLING"
  Else                          → "FLAT"
```

**Threshold rationale (Q5 resolved):** Deribit returns funding to 6 decimal places. A delta of
0.0001 between 1-minute poll intervals represents a 1 basis point shift — a genuine intraday
signal, not float noise. Default is correct and safe; no change needed.

Add to `IndicatorResults.vb`:
```vb
Public Property FundingMomentum As String  ' "RISING" | "FALLING" | "FLAT"
```

---

## 3. Step 3 Modifier Integration

### 3a. Existing Step 3 (unchanged)

```vb
If fr > cfg.Scoring.FundingHighPositive Then
    ls -= cfg.Scoring.FundingHighPenalty : ss += cfg.Scoring.FundingHighBoost
ElseIf fr > cfg.Scoring.FundingLowPositive Then
    ls -= cfg.Scoring.FundingLowPenalty
ElseIf fr < cfg.Scoring.FundingHighNegative Then
    ss -= cfg.Scoring.FundingHighPenalty : ls += cfg.Scoring.FundingHighBoost
ElseIf fr < cfg.Scoring.FundingLowNegative Then
    ss -= cfg.Scoring.FundingLowPenalty
End If
ls = Math.Max(0, ls)
ss = Math.Max(0, ss)
```

### 3b. New Step 3b — Momentum amplifier/softener + pre-emptive neutral zone penalty

Placed immediately after the existing Step 3 clamps.

```vb
' Step 3b: Funding momentum modifier
' — In penalty zone: amplify when momentum confirms crowding; soften when de-crowding.
' — In neutral zone: pre-emptive soft penalty when rate is RISING toward FundingLowPositive
'   (positive side) or FALLING toward FundingLowNegative (negative side).
' MomentumAmplify is capped at FundingHighPenalty to prevent disproportionate impact
' on borderline verdicts where funding is the only adverse signal.
Dim fundingMomAdj   As Integer = 0
Dim safeAmplify     As Integer = Math.Min(cfg.Indicators.Funding.MomentumAmplify,
                                          cfg.Scoring.FundingHighPenalty)
Dim isHighPositive  As Boolean = (fr > cfg.Scoring.FundingHighPositive)
Dim isLowPositive   As Boolean = (fr > cfg.Scoring.FundingLowPositive)
Dim isHighNegative  As Boolean = (fr < cfg.Scoring.FundingHighNegative)
Dim isLowNegative   As Boolean = (fr < cfg.Scoring.FundingLowNegative)
Dim inPenaltyZone   As Boolean = isHighPositive OrElse isLowPositive OrElse
                                  isHighNegative OrElse isLowNegative
Dim inNeutralZone   As Boolean = Not inPenaltyZone

If cfg.Indicators.Funding.MomentumEnabled Then
    If inPenaltyZone Then
        If isHighPositive OrElse isLowPositive Then
            ' Positive funding zone
            If r.FundingMomentum = "RISING" Then
                ls = Math.Max(0, ls - safeAmplify)
                fundingMomAdj = -safeAmplify
            ElseIf r.FundingMomentum = "FALLING" Then
                ls = Math.Min(ls + cfg.Indicators.Funding.MomentumSoften, regimeMax)
                fundingMomAdj = +cfg.Indicators.Funding.MomentumSoften
            End If
        Else
            ' Negative funding zone
            If r.FundingMomentum = "FALLING" Then
                ss = Math.Max(0, ss - safeAmplify)
                fundingMomAdj = -safeAmplify
            ElseIf r.FundingMomentum = "RISING" Then
                ss = Math.Min(ss + cfg.Indicators.Funding.MomentumSoften, regimeMax)
                fundingMomAdj = +cfg.Indicators.Funding.MomentumSoften
            End If
        End If
    ElseIf inNeutralZone Then
        ' Pre-emptive soft penalty: neutral rate trending toward penalty zone
        ' Signals crowding buildup before the threshold fires.
        If r.FundingMomentum = "RISING" AndAlso fr > 0 Then
            ls = Math.Max(0, ls - safeAmplify)
            fundingMomAdj = -safeAmplify
        ElseIf r.FundingMomentum = "FALLING" AndAlso fr < 0 Then
            ss = Math.Max(0, ss - safeAmplify)
            fundingMomAdj = -safeAmplify
        End If
    End If
End If
ls = Math.Max(0, ls)
ss = Math.Max(0, ss)
```

**Q3 resolved:** `safeAmplify` is computed at the call site as
`Math.Min(MomentumAmplify, FundingHighPenalty)`. With defaults (amplify=1, penalty=2), combined
Step 3 impact is capped at 3. This is calibrated for high-frequency scalping on tight thresholds
— amplify cannot exceed the base penalty it reinforces.

**Q6 resolved:** Neutral zone pre-emptive penalty fires when `fr > 0` and RISING (positive
crowding building) or `fr < 0` and FALLING (negative crowding building). Magnitude is
`safeAmplify` (same 1-point cap). This makes the signal predictive rather than reactive.

---

## 4. SignalBreakdown Update

`FundingBias` is confirmed present on `IndicatorResults` (Q7 resolved).

Update the `Funding (info)` breakdown note in `ScoringEngine_Calculate.vb`.
`fundingMomAdj` must be declared (as `Dim fundingMomAdj As Integer = 0`) before Step 3,
so it is in scope here. The breakdown is built after Step 3 — no reordering required.

**Before:**
```vb
breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False,
    String.Format("{0:F4}% | {1}", r.FundingRate * 100, r.FundingBias)))
```

**After:**
```vb
Dim fundingNote As String = String.Format("{0:F4}% | {1} | mom:{2}",
    r.FundingRate * 100, r.FundingBias, r.FundingMomentum)
If fundingMomAdj <> 0 Then
    fundingNote &= String.Format(" | MOM ADJ {0}{1}",
        If(fundingMomAdj > 0, "+", ""), fundingMomAdj)
End If
breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False, fundingNote))
```

---

## 5. Ring Buffer Implementation

**Q1 resolved:** `MainForm_Layout.vb` is the canonical home — same location as `_oiHistory`.

**Q2 resolved:** Cold-start FLAT fallback is accepted. No pre-seeding from historical API.
The first 2–3 runs are warm-up by nature for a live scalping engine.

### `UI/MainForm_Layout.vb` — add alongside `_oiHistory`

```vb
' Funding rate history ring buffer — for FundingMomentum computation in Step 3b
Private _fundingHistory As New List(Of Double)
Private Const FundingHistoryMax As Integer = 10
```

### `UI/MainForm_Analysis.vb` — after `GetFundingRateAsync()` returns

```vb
' Append to funding history ring buffer
_fundingHistory.Add(r.FundingRate)
If _fundingHistory.Count > FundingHistoryMax Then _fundingHistory.RemoveAt(0)

' Compute funding momentum before Calculate()
r.FundingMomentum = CalcFundingMomentum(_fundingHistory, cfg)
```

### `Core/Indicators_OrderFlow.vb` — new shared function

```vb
''' <summary>
''' Derives funding rate momentum from a rolling history of funding samples.
''' Returns "RISING", "FALLING", or "FLAT".
''' Cold start (fewer than 2 samples) returns "FLAT".
''' </summary>
Public Shared Function CalcFundingMomentum(
    history As List(Of Double),
    cfg     As EngineSettings) As String

    If history Is Nothing OrElse history.Count < 2 Then Return "FLAT"

    Dim window   As Integer = cfg.Indicators.Funding.MomentumWindow
    Dim priorIdx As Integer = Math.Max(0, history.Count - 1 - window)
    Dim delta    As Double  = history(history.Count - 1) - history(priorIdx)

    If delta >  cfg.Indicators.Funding.MomentumThreshold Then Return "RISING"
    If delta < -cfg.Indicators.Funding.MomentumThreshold Then Return "FALLING"
    Return "FLAT"
End Function
```

---

## 6. Worked Examples

### Example A — Penalty zone, RISING → amplify fires

```
History: [0.0030, 0.0035, 0.0038, 0.0042, 0.0048]
MomentumWindow = 3
recent = 0.0048, prior = history[1] = 0.0035
delta  = +0.0013 > 0.0001 → RISING

fr = 0.0048 > FundingHighPositive (0.004) → base penalty:
  ls -= FundingHighPenalty (2) → ls: 8→6
  ss += FundingHighBoost   (1) → ss: 4→5

Step 3b: inPenaltyZone, isHighPositive, RISING
  safeAmplify = Min(1, 2) = 1
  ls -= 1 → ls: 6→5
  fundingMomAdj = -1

Breakdown: "0.0048% | LONGS_CROWDED | mom:RISING | MOM ADJ -1"
```

### Example B — Penalty zone, FALLING → soften fires

```
History: [0.0060, 0.0055, 0.0052, 0.0049, 0.0045]
delta = 0.0045 - 0.0052 = -0.0007 → FALLING

fr = 0.0045 > FundingHighPositive → base penalty fires (ls-=2, ss+=1)

Step 3b: isHighPositive, FALLING
  ls += MomentumSoften (1) → ls partially restored
  fundingMomAdj = +1

Breakdown: "0.0045% | LONGS_CROWDED | mom:FALLING | MOM ADJ +1"
```

### Example C — Neutral zone, RISING → pre-emptive penalty fires

```
History: [0.0005, 0.0010, 0.0015, 0.0018, 0.0022]
delta = 0.0022 - 0.0010 = +0.0012 → RISING

fr = 0.0022 < FundingLowPositive (e.g. 0.003) → inNeutralZone = True
fr > 0 and RISING → pre-emptive soft penalty:
  ls -= safeAmplify (1)
  fundingMomAdj = -1

Breakdown: "0.0022% | NEUTRAL | mom:RISING | MOM ADJ -1"
```

---

## 7. Display Format

No changes to `MainForm_Render.vb` required. Funding is `(info)`-only in the breakdown and
does not appear in the scored signal table. Momentum tag and adjustment are visible in the
`Funding (info)` breakdown note line.

---

## 8. Files Changed Summary

| File | Change |
|---|---|
| `Core/IndicatorResults.vb` | Add `FundingMomentum As String` property |
| `Core/Indicators_OrderFlow.vb` | Add `CalcFundingMomentum()` shared function |
| `Core/ScoringEngine_Calculate.vb` | Declare `fundingMomAdj` before Step 3; add Step 3b block; update `Funding (info)` breakdown note |
| `Core/Settings/EngineSettings.vb` | Add `FundingSettings` class + `Funding` property to `EngineSettings` |
| `UI/MainForm_Layout.vb` | Add `_fundingHistory As New List(Of Double)` + `FundingHistoryMax As Integer = 10` |
| `UI/MainForm_Analysis.vb` | Append to `_fundingHistory` after `GetFundingRateAsync()`; call `CalcFundingMomentum()` |
| `settings.json` | Add `"funding"` block under `"indicators"` (5 keys) |

No changes to `MainForm_Render.vb`. No new API calls. No CSV logging changes.

---

## 9. Settings Keys

### `Core/Settings/EngineSettings.vb`

```vb
Public Class FundingSettings
    ''' <summary>Enable Step 3b funding momentum modifier. Default True.</summary>
    Public Property MomentumEnabled   As Boolean = True
    ''' <summary>Lookback sample count for momentum delta. Default 3.</summary>
    Public Property MomentumWindow    As Integer = 3
    ''' <summary>Min absolute delta to classify as RISING or FALLING. Default 0.0001 (1 bp).</summary>
    Public Property MomentumThreshold As Double  = 0.0001
    ''' <summary>Penalty added when momentum confirms crowding. Capped at FundingHighPenalty at call site. Default 1.</summary>
    Public Property MomentumAmplify   As Integer = 1
    ''' <summary>Score restored when momentum signals de-crowding. Default 1.</summary>
    Public Property MomentumSoften    As Integer = 1
End Class

' Add to EngineSettings class:
Public Property Funding As New FundingSettings
```

### `settings.json` — add under `"indicators"`

```json
"funding": {
  "momentum_enabled": true,
  "momentum_window": 3,
  "momentum_threshold": 0.0001,
  "momentum_amplify": 1,
  "momentum_soften": 1
}
```

| Key | Default | Purpose |
|---|---|---|
| `momentum_enabled` | true | Master switch for Step 3b |
| `momentum_window` | 3 | Samples to look back for delta |
| `momentum_threshold` | 0.0001 | Min delta (1 bp) to call RISING/FALLING |
| `momentum_amplify` | 1 | Penalty when momentum confirms crowding (capped at `FundingHighPenalty`) |
| `momentum_soften` | 1 | Score restored when momentum signals de-crowding |

---

## 10. What This Does NOT Do

- Does **not** change verdict thresholds or MaxScore
- Does **not** add new API calls or new data sources
- Does **not** replace existing Step 3 threshold logic — Step 3b is additive on top
- Does **not** log `FundingMomentum` to CSV (add to Section 12 backlog alongside `VerdictContext` once CalibrationReport approaches READY)
- Does **not** affect `CalcHoldStatus`

---

## 11. Open Questions — Resolved 2026-04-18

| # | Question | Resolution |
|---|---|---|
| Q1 | Ring buffer ownership | `MainForm_Layout.vb` — canonical home alongside `_oiHistory` |
| Q2 | Cold-start FLAT fallback | Accepted — no pre-seeding; first 2–3 runs are warm-up |
| Q3 | MomentumAmplify cap | Capped at `FundingHighPenalty` via `safeAmplify` guard at call site. Default 1 is correct for HF scalping profile |
| Q4 | Soften upper bound | `Math.Min(ls + soften, regimeMax)` — correct ceiling |
| Q5 | Threshold at 0.0001 | Correct and safe — 1 bp shift between 1m polls is a genuine signal, not float noise |
| Q6 | Neutral zone pre-emptive penalty | Confirmed: RISING neutral funding (fr > 0) triggers soft long penalty; FALLING neutral (fr < 0) triggers soft short penalty |
| Q7 | `FundingBias` field | Confirmed present — `Public Property FundingBias As String` in `IndicatorResults.vb` line 38 |
