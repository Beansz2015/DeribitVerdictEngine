# Spec: Funding Rate Momentum Signal
**Proposed:** 2026-04-17
**Status:** AWAITING REVIEW
**Target files:** `Core/IndicatorResults.vb`, `Core/Indicators_OrderFlow.vb`, `Core/ScoringEngine_Calculate.vb`, `Core/Settings/EngineSettings.vb`, `settings.json`

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
softening it when momentum signals relief.

Zero scoring architecture changes. Zero new API calls. No new data sources. The funding rate
history is already available in the OI snapshot infrastructure.

---

## 2. FundingMomentum Computation

### 2a. Data Source

`GetFundingRateAsync()` in `DeribitClient.vb` already returns the current funding rate.
To compute momentum, we need a short rolling history — the last N funding samples.

Deribit funding settles every **8 hours** for perpetuals. However, the displayed funding rate
updates in real time (it is a running estimate). For a 1-minute scalping engine, the relevant
question is: *is the live funding estimate moving up or down over the last few engine runs?*

The simplest and most robust approach: maintain a **ring buffer of the last `FundingWindowSize`
funding rate samples** (default 5) in `MainForm_Layout.vb` alongside `_oiHistory`, and pass
the buffer into the indicator pipeline alongside `norms`. The buffer is populated on every
`RunAnalysisAsync()` call after `GetFundingRateAsync()` returns.

### 2b. FundingMomentum Signal Logic

```
Given: fundingHistory = List(Of Double), most recent last
       cfg.Indicators.Funding.MomentumWindow (default 3 — number of samples to compare)
       cfg.Indicators.Funding.MomentumThreshold (default 0.0001 — min delta to qualify as RISING/FALLING)

Compute:
  If fundingHistory.Count < 2 → FundingMomentum = "FLAT"

  recent = fundingHistory.Last()
  prior  = fundingHistory(fundingHistory.Count - cfg.Indicators.Funding.MomentumWindow)
            (clamped to index 0 if window > available samples)
  delta  = recent - prior

  If delta >  cfg.Indicators.Funding.MomentumThreshold  → "RISING"
  If delta < -cfg.Indicators.Funding.MomentumThreshold  → "FALLING"
  Else                                                   → "FLAT"
```

Add to `IndicatorResults`:
```vb
Public Property FundingMomentum As String  ' "RISING" | "FALLING" | "FLAT"
```

---

## 3. Step 3 Modifier Integration

Current Step 3 in `ScoringEngine_Calculate.vb`:

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
```

Proposed addition — a momentum amplifier/softener applied *after* the existing threshold logic:

```vb
' Step 3b: Funding momentum modifier
' Amplifies penalty when momentum confirms crowding; softens when momentum signals relief.
' Only fires when a threshold penalty has already been applied (fr outside neutral zone).
Dim fundingMomAdj As Integer = 0
Dim isHighPositive As Boolean = (fr > cfg.Scoring.FundingHighPositive)
Dim isLowPositive  As Boolean = (fr > cfg.Scoring.FundingLowPositive)
Dim isHighNegative As Boolean = (fr < cfg.Scoring.FundingHighNegative)
Dim isLowNegative  As Boolean = (fr < cfg.Scoring.FundingLowNegative)
Dim inPenaltyZone  As Boolean = isHighPositive OrElse isLowPositive OrElse
                                 isHighNegative OrElse isLowNegative

If cfg.Indicators.Funding.MomentumEnabled AndAlso inPenaltyZone Then
    If isHighPositive OrElse isLowPositive Then
        ' Positive funding zone: RISING momentum → amplify long penalty; FALLING → soften
        If r.FundingMomentum = "RISING" Then
            ls = Math.Max(0, ls - cfg.Indicators.Funding.MomentumAmplify)
            fundingMomAdj = -cfg.Indicators.Funding.MomentumAmplify   ' sign: long impact
        ElseIf r.FundingMomentum = "FALLING" Then
            ls = Math.Min(ls + cfg.Indicators.Funding.MomentumSoften, regimeMax)
            fundingMomAdj = +cfg.Indicators.Funding.MomentumSoften
        End If
    Else
        ' Negative funding zone: FALLING momentum → amplify short penalty; RISING → soften
        If r.FundingMomentum = "FALLING" Then
            ss = Math.Max(0, ss - cfg.Indicators.Funding.MomentumAmplify)
            fundingMomAdj = -cfg.Indicators.Funding.MomentumAmplify
        ElseIf r.FundingMomentum = "RISING" Then
            ss = Math.Min(ss + cfg.Indicators.Funding.MomentumSoften, regimeMax)
            fundingMomAdj = +cfg.Indicators.Funding.MomentumSoften
        End If
    End If
End If
ls = Math.Max(0, ls)
ss = Math.Max(0, ss)
```

The existing `Math.Max(0, ls)` / `Math.Max(0, ss)` clamp at the end of Step 3 already covers
negative overflow; no additional guard needed.

---

## 4. SignalBreakdown Update

The existing `Funding (info)` breakdown item is display-only (`LongHit = False`, `ShortHit = False`).
Update its note string to include momentum:

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
    Dim adjSign As String = If(fundingMomAdj > 0, "+", "")
    fundingNote &= String.Format(" | MOM ADJ {0}{1}", adjSign, fundingMomAdj)
End If
breakdown.Add(New SignalBreakdownItem("Funding (info)", False, False, fundingNote))
```

Note: `fundingMomAdj` must be declared before Step 3 and used in both the Step 3b block and
the breakdown note. The breakdown for `Funding (info)` is built after Step 3 in the existing
code — no reordering required.

---

## 5. Ring Buffer Implementation

In `UI/MainForm_Layout.vb`, alongside `_oiHistory`:

```vb
' Funding rate history ring buffer for momentum computation
Private _fundingHistory As New List(Of Double)
Private Const FundingHistoryMax As Integer = 10  ' keep last 10 samples; MomentumWindow uses cfg subset
```

In `UI/MainForm_Analysis.vb`, after `GetFundingRateAsync()` returns:

```vb
' Append to funding history ring buffer
_fundingHistory.Add(r.FundingRate)
If _fundingHistory.Count > FundingHistoryMax Then
    _fundingHistory.RemoveAt(0)
End If
```

Pass `_fundingHistory` into `CalcFundingMomentum()` (new helper in `Indicators_OrderFlow.vb`):

```vb
r.FundingMomentum = CalcFundingMomentum(_fundingHistory, cfg)
```

This call should be placed alongside the other indicator calculations, before `Calculate()` is
called, so `FundingMomentum` is populated on `r` when it reaches Step 3b.

### New helper — `Core/Indicators_OrderFlow.vb`

```vb
''' <summary>
''' Derives funding rate momentum from a short rolling history of funding samples.
''' Returns "RISING", "FALLING", or "FLAT".
''' </summary>
Public Shared Function CalcFundingMomentum(
    history As List(Of Double),
    cfg     As EngineSettings) As String

    Dim window As Integer = cfg.Indicators.Funding.MomentumWindow
    If history Is Nothing OrElse history.Count < 2 Then Return "FLAT"

    Dim priorIdx As Integer = Math.Max(0, history.Count - 1 - window)
    Dim delta    As Double  = history(history.Count - 1) - history(priorIdx)

    If delta >  cfg.Indicators.Funding.MomentumThreshold Then Return "RISING"
    If delta < -cfg.Indicators.Funding.MomentumThreshold Then Return "FALLING"
    Return "FLAT"
End Function
```

---

## 6. Worked Example

**Scenario:** Funding RISING through positive zone, amplify fires.

```
FundingRate history (last 5 samples): [0.0030, 0.0035, 0.0038, 0.0042, 0.0048]
MomentumWindow = 3
recent = 0.0048
prior  = history[5 - 1 - 3] = history[1] = 0.0035
delta  = 0.0048 - 0.0035 = +0.0013 > MomentumThreshold (0.0001) → RISING

FundingHighPositive threshold = 0.004
fr = 0.0048 > 0.004 → base penalty fires:
  ls -= FundingHighPenalty (2)   → ls = 8 - 2 = 6
  ss += FundingHighBoost  (1)    → ss = 4 + 1 = 5

Step 3b: inPenaltyZone=True, isHighPositive=True, momentum=RISING
  ls -= MomentumAmplify (1)      → ls = 6 - 1 = 5
  fundingMomAdj = -1

Breakdown: "0.0048% | LONGS_CROWDED | mom:RISING | MOM ADJ -1"
```

**Scenario:** Funding FALLING away from positive extreme, soften fires.

```
FundingRate history: [0.0060, 0.0055, 0.0052, 0.0049, 0.0045]
delta = 0.0045 - 0.0052 = -0.0007 → FALLING

fr = 0.0045 > FundingHighPositive (0.004) → base penalty fires:
  ls -= 2, ss += 1

Step 3b: isHighPositive=True, momentum=FALLING
  ls += MomentumSoften (1)       → ls partially restored
  fundingMomAdj = +1

Breakdown: "0.0045% | LONGS_CROWDED | mom:FALLING | MOM ADJ +1"
```

---

## 7. Display Format

No changes to `MainForm_Render.vb` required — funding is `(info)` only in the breakdown and
does not appear in the scored signal table. The momentum tag and adjustment are visible in the
`Funding (info)` breakdown note line, which already renders in the signal detail block.

---

## 8. Files Changed Summary

| File | Change |
|---|---|
| `Core/IndicatorResults.vb` | Add `FundingMomentum As String` property |
| `Core/Indicators_OrderFlow.vb` | Add `CalcFundingMomentum()` shared function |
| `Core/ScoringEngine_Calculate.vb` | Add Step 3b momentum amplifier/softener block; update `Funding (info)` breakdown note |
| `Core/Settings/EngineSettings.vb` | Add `FundingSettings` sub-class with 5 new keys |
| `UI/MainForm_Layout.vb` | Add `_fundingHistory` ring buffer + `FundingHistoryMax` const |
| `UI/MainForm_Analysis.vb` | Append to `_fundingHistory` after `GetFundingRateAsync()`; call `CalcFundingMomentum()` |
| `settings.json` | Add `funding` block under `indicators` (5 keys) |

No changes to `MainForm_Render.vb`. No new API calls. No CSV logging changes.

---

## 9. Settings Keys

Add a `FundingSettings` class to `EngineSettings.vb`:

```vb
Public Class FundingSettings
    ''' <summary>Enable funding momentum amplifier/softener in Step 3b. Default True.</summary>
    Public Property MomentumEnabled     As Boolean = True
    ''' <summary>Number of historical samples to look back for delta. Default 3.</summary>
    Public Property MomentumWindow      As Integer = 3
    ''' <summary>Min absolute funding delta to qualify as RISING or FALLING. Default 0.0001.</summary>
    Public Property MomentumThreshold   As Double  = 0.0001
    ''' <summary>Additional penalty applied when momentum confirms crowding direction. Default 1.</summary>
    Public Property MomentumAmplify     As Integer = 1
    ''' <summary>Score points restored when momentum signals de-crowding. Default 1.</summary>
    Public Property MomentumSoften      As Integer = 1
End Class

' Add to EngineSettings:
Public Property Funding As New FundingSettings
```

Add to `settings.json` under `"indicators"`:

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
| `momentum_enabled` | true | Master switch for Step 3b block |
| `momentum_window` | 3 | Lookback samples for delta calculation |
| `momentum_threshold` | 0.0001 | Min delta (in rate terms) to call RISING/FALLING |
| `momentum_amplify` | 1 | Penalty added when momentum confirms crowding |
| `momentum_soften` | 1 | Score restored when momentum signals de-crowding |

---

## 10. What This Does NOT Do

- Does **not** change any verdict thresholds or MaxScore
- Does **not** add new API calls or new data sources
- Does **not** change how existing `FundingHighPenalty` / `FundingHighBoost` fire — Step 3b
  is additive on top of the existing Step 3 logic, not a replacement
- Does **not** log `FundingMomentum` to CSV (can be added to Section 12 backlog alongside
  `VerdictContext` once CalibrationReport approaches READY)
- Does **not** affect `CalcHoldStatus` — funding momentum is a pre-verdict signal, not a
  hold/exit signal

---

## 11. Open Questions for Review

1. **Ring buffer ownership** — `_fundingHistory` is proposed in `MainForm_Layout.vb` alongside
   `_oiHistory`. Confirm this is the correct location, or whether it should live in
   `AnalysisLogger` / a dedicated `FundingSnapshot` helper class analogous to `OiSnapshot.vb`.

2. **Buffer seeding on cold start** — on the first 1–3 engine runs, `_fundingHistory` will have
   fewer than `MomentumWindow` samples. `CalcFundingMomentum` falls back to `"FLAT"` in this
   case. Confirm this is acceptable (no momentum signal on cold start) vs. pre-seeding from a
   historical funding API call.

3. **MomentumAmplify cap** — with default `MomentumAmplify = 1` and `MomentumSoften = 1`, the
   maximum combined Step 3 impact is `FundingHighPenalty + 1 = 3` on a single score. Confirm
   this magnitude is acceptable, or whether `MomentumAmplify` should be capped relative to
   `FundingHighPenalty` (e.g., cannot exceed the base penalty it amplifies).

4. **Soften upper bound** — `MomentumSoften` currently adds back up to 1 point even if the
   score is already near `regimeMax`. The `Math.Min(ls + soften, regimeMax)` guard is included
   but confirm this is the correct ceiling (vs. capping at the pre-Step-3 score before any
   funding modifier was applied).

5. **MomentumThreshold calibration** — default `0.0001` (1 basis point delta per window).
   BTC-PERPETUAL funding is typically ±0.005–0.010% per 8h at moderate conditions. Confirm
   this threshold is tight enough to be sensitive to real rate movement without firing on
   rounding noise from the Deribit API response.

6. **Neutral zone behaviour** — Step 3b only fires when `inPenaltyZone = True` (fr outside
   neutral band). Confirm momentum signal should be suppressed entirely when funding is neutral,
   or whether a RISING neutral rate approaching `FundingLowPositive` should trigger a
   pre-emptive soft penalty.

7. **`FundingBias` field** — confirm `r.FundingBias` is already populated in `IndicatorResults`
   by `RunAnalysisAsync()` / `DeribitClient`. If not, the breakdown note format will need
   adjustment.
