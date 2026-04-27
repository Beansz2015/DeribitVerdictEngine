# Spec: Swing Pivot Structure Detection (5m primary, 15m context)
**Proposed:** 2026-04-27
**Status:** PROPOSED — pending user approval
**Target files:** `Core/IndicatorResults.vb`, `Core/Indicators_Structure.vb`, `Core/ScoringEngine_Helpers.vb`, `Core/ScoringEngine_Calculate_Verdict.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_Render_Header.vb`, `UI/MainForm_Render_Sections.vb`, `settings.json`

This is the largest of the four planned indicator additions. Three integration points (Step 5b target cap, `CalcHoldStatus`, ATR display block) and one optional 15m context layer.

---

## 1. Problem Statement

The trader-profile (Section 2) explicitly defines the trading style:

> **Profit target:** previous swing high (longs) / previous swing low (shorts). **Structural, not fixed-% or ATR-based.**
> **Stop-loss:** below previous swing low (longs) / above previous swing high (shorts). **Structural, not ATR-based.**

The engine does not compute swing levels. It outputs `ATR × scale × multiplier` stops and targets, with VPFR HVN as an opportunistic cap. **None of those reference the actual structural levels you trade off.**

The mismatch shows up everywhere:

- **ATR display block** shows `Stop X | Entry Y | Target Z (R:R 1:1.7)` — a calculation derived from ATR, not from price structure. The numbers are always tradeable but rarely *what you'd actually use*.
- **Step 5b HVN cap** brings POC into the picture, but POC is volume gravity, not price structure. POC and the most recent swing high agree perhaps half the time.
- **CalcHoldStatus** has microstructure exits (MicroCVD/OFI/TFI/CVD adverse) and momentum exits (ROC crossing 0, RSI thresholds). It does not have a **structural break** exit — "price closed through the prior swing low while in a long" — which is the cleanest exit signal in your stated playbook.
- **VerdictContext** classifies STRUCTURALLY_WEAK based on signal counts. It cannot detect "entry is mid-channel between swings, no clean structural target available" — a real structural-weakness condition.

This spec adds swing-pivot detection on 5m candles (primary) and 15m candles (context). It then plumbs the resulting levels into:

1. ATR display block — show structural levels alongside ATR levels
2. Step 5b cap arbitration — prefer swing target over HVN/POC when present and closer
3. CalcHoldStatus — new structural-break exit conditions
4. VerdictContext — sharper STRUCTURALLY_WEAK definition

The pivot scan algorithm is the same one already used by `CalcRSIDivergence` (`Indicators_Momentum.vb` line 165+) — left/right wing of N bars confirms a swing. Reuse the pattern; do not invent a new scanner.

Zero new API calls. 5m candles already fetched. 15m candles already fetched (with TTL cache).

---

## 2. Computation

### 2a. Pivot Definition

A **swing high** at index `i` is a local maximum where:
- `candles(i).High > candles(i-w).High` for all `w` in `[1, pivotWing]`
- `candles(i).High > candles(i+w).High` for all `w` in `[1, pivotWing]`

A **swing low** at index `i` is the symmetric local minimum on `Low`.

`pivotWing` is the half-width of the confirmation window. With `pivotWing = 3` on 5m candles, a swing requires 3 bars left and 3 right to confirm — meaning the **most recent confirmable swing is at least 3 bars old** (we cannot confirm a swing inside the right-wing zone).

### 2b. Most Recent Confirmed Swing

For the engine's purposes we need the **most recent** confirmed swing high and swing low — these are the structural reference levels for current trade decisions.

```
Given: candles = List(Of Candle), most recent last
       pivotWing  (default 3)
       lookbackBars (default 30)  -- how far back to scan

Procedure:
  scanEnd   = candles.Count - 1 - pivotWing      ' rightmost confirmable index
  scanStart = Math.Max(pivotWing, scanEnd - lookbackBars)

  lastSwingHighIdx = -1; lastSwingHighPrice = 0
  lastSwingLowIdx  = -1; lastSwingLowPrice  = 0

  ' Walk backward from scanEnd toward scanStart; first swing high found = most recent
  For i = scanEnd Down To scanStart Step 1
      If isSwingHigh(candles, i, pivotWing) AndAlso lastSwingHighIdx = -1 Then
          lastSwingHighIdx   = i
          lastSwingHighPrice = candles(i).High
      End If
      If isSwingLow(candles, i, pivotWing) AndAlso lastSwingLowIdx = -1 Then
          lastSwingLowIdx   = i
          lastSwingLowPrice = candles(i).Low
      End If
      If lastSwingHighIdx >= 0 AndAlso lastSwingLowIdx >= 0 Then Exit For
  Next
```

Returns `(lastSwingHighPrice, lastSwingLowPrice)`. Either may be 0 meaning "no confirmed swing in lookback window" — display as "—" and disable downstream consumers.

### 2c. New Function — Indicators_Structure.vb

```vb
''' <summary>
''' Scans candle list for the most recent confirmed swing high and swing low pivots.
''' A confirmed pivot has pivotWing bars on each side. Returns 0 for either if
''' no pivot is found within lookbackBars of the latest confirmable index.
''' </summary>
Public Shared Sub CalcSwingPivots(candles As List(Of Candle),
                                   ByRef lastSwingHighPrice As Double,
                                   ByRef lastSwingLowPrice As Double,
                                   Optional pivotWing As Integer = 3,
                                   Optional lookbackBars As Integer = 30)
    lastSwingHighPrice = 0
    lastSwingLowPrice  = 0
    If candles Is Nothing OrElse candles.Count < pivotWing * 2 + 2 Then Return

    Dim scanEnd As Integer = candles.Count - 1 - pivotWing
    If scanEnd < pivotWing Then Return
    Dim scanStart As Integer = Math.Max(pivotWing, scanEnd - lookbackBars)

    Dim foundHigh As Boolean = False
    Dim foundLow  As Boolean = False

    For i As Integer = scanEnd To scanStart Step -1
        If Not foundHigh Then
            Dim isHigh As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles(i - w).High >= candles(i).High OrElse
                   candles(i + w).High >= candles(i).High Then
                    isHigh = False : Exit For
                End If
            Next
            If isHigh Then
                lastSwingHighPrice = candles(i).High
                foundHigh = True
            End If
        End If

        If Not foundLow Then
            Dim isLow As Boolean = True
            For w As Integer = 1 To pivotWing
                If candles(i - w).Low <= candles(i).Low OrElse
                   candles(i + w).Low <= candles(i).Low Then
                    isLow = False : Exit For
                End If
            Next
            If isLow Then
                lastSwingLowPrice = candles(i).Low
                foundLow = True
            End If
        End If

        If foundHigh AndAlso foundLow Then Exit For
    Next
End Sub
```

### 2d. New IndicatorResults Fields

```vb
Public Property LastSwingHigh5m  As Double  ' price ($), 0 = no confirmed pivot in lookback
Public Property LastSwingLow5m   As Double
Public Property LastSwingHigh15m As Double  ' higher-timeframe context (optional, may stay 0)
Public Property LastSwingLow15m  As Double

' Convenience computed at scoring time
Public Property SwingTargetLong   As Double  ' = LastSwingHigh5m if > CurrentPrice, else 0
Public Property SwingStopLong     As Double  ' = LastSwingLow5m  if < CurrentPrice, else 0
Public Property SwingTargetShort  As Double  ' = LastSwingLow5m  if < CurrentPrice, else 0
Public Property SwingStopShort    As Double  ' = LastSwingHigh5m if > CurrentPrice, else 0
```

`SwingTarget*` and `SwingStop*` are bookkeeping wrappers — populate them in `MainForm_Analysis.vb` immediately after `CalcSwingPivots` returns. They make downstream consumer code self-documenting.

---

## 3. Call Site Wiring

In `UI/MainForm_Analysis.vb`, after the existing 5m-candle indicators (DMI, regime, EMA200_5m):

```vb
' Swing pivots on 5m -- structural reference for target/stop arbitration
IndicatorEngine.CalcSwingPivots(candles5m,
                                 r.LastSwingHigh5m, r.LastSwingLow5m,
                                 pivotWing:=cfg.Indicators.Swing.PivotWing5m,
                                 lookbackBars:=cfg.Indicators.Swing.LookbackBars5m)

' 15m context (already-cached candles15m)
If candles15m IsNot Nothing AndAlso candles15m.Count > 0 Then
    IndicatorEngine.CalcSwingPivots(candles15m,
                                     r.LastSwingHigh15m, r.LastSwingLow15m,
                                     pivotWing:=cfg.Indicators.Swing.PivotWing15m,
                                     lookbackBars:=cfg.Indicators.Swing.LookbackBars15m)
End If

' Direction-aware bookkeeping
r.SwingTargetLong  = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)
r.SwingStopLong    = If(r.LastSwingLow5m  < r.CurrentPrice AndAlso r.LastSwingLow5m  > 0, r.LastSwingLow5m, 0)
r.SwingTargetShort = If(r.LastSwingLow5m  < r.CurrentPrice AndAlso r.LastSwingLow5m  > 0, r.LastSwingLow5m, 0)
r.SwingStopShort   = If(r.LastSwingHigh5m > r.CurrentPrice, r.LastSwingHigh5m, 0)
```

---

## 4. ATR Display Block — Side-by-Side Structural

`MainForm_Render_Header.vb` currently renders ATR levels only:

```
ATR ENTRY LEVELS  (ATR 12.83 x 0.59 scale | 1.2x stop / 2.0x target)
  Long:   Stop  78275.9  |  Entry  78285.0  |  Target  78300.2    R:R 1:1.7  (risk 9.1 / rwd 15.2)
  Short:  Stop  78294.1  |  Entry  78285.0  |  Target  78269.8    R:R 1:1.7  (risk 9.1 / rwd 15.2)
```

Updated rendering — add a **structural row beneath each ATR row** when swing levels are available:

```
ATR ENTRY LEVELS  (ATR 12.83 x 0.59 scale | 1.2x stop / 2.0x target)
  Long:   Stop  78275.9  |  Entry  78285.0  |  Target  78300.2    R:R 1:1.7  (risk 9.1 / rwd 15.2)
  Long structural:    Stop  78240.0  |  Entry  78285.0  |  Target  78420.0    R:R 1:3.0  (risk 45.0 / rwd 135.0)
  Short:  Stop  78294.1  |  Entry  78285.0  |  Target  78269.8    R:R 1:1.7  (risk 9.1 / rwd 15.2)
  Short structural:   (no swing low below entry within lookback)
```

Render logic:

```vb
' After existing Long ATR row:
If r.SwingTargetLong > 0 AndAlso r.SwingStopLong > 0 Then
    Dim risk   As Double = r.CurrentPrice - r.SwingStopLong
    Dim reward As Double = r.SwingTargetLong - r.CurrentPrice
    Dim swingRR As String = If(risk > 0, String.Format("1:{0:F1}", reward / risk), "—")
    AppendRtf(rtb, "  Long structural:  ", C_LABEL)
    AppendRtf(rtb, String.Format("Stop {0,9:F1}  |  Entry {1,9:F1}  |  Target {2,9:F1}    R:R {3}  (risk {4:F1} / rwd {5:F1})",
                                  r.SwingStopLong, r.CurrentPrice, r.SwingTargetLong, swingRR, risk, reward) & Environment.NewLine, C_HIT)
ElseIf r.SwingTargetLong > 0 Then
    AppendRtf(rtb, "  Long structural:  ", C_LABEL)
    AppendRtf(rtb, String.Format("Target {0,9:F1}  (no swing low below entry within lookback)", r.SwingTargetLong) & Environment.NewLine, C_DIM)
ElseIf r.SwingStopLong > 0 Then
    AppendRtf(rtb, "  Long structural:  ", C_LABEL)
    AppendRtf(rtb, String.Format("Stop {0,9:F1}  (no swing high above entry within lookback)", r.SwingStopLong) & Environment.NewLine, C_DIM)
End If
' (mirror for Short)
```

Use `C_HIT` (cyan) for the full structural row to visually distinguish from the ATR baseline. Use `C_DIM` for partial / one-sided cases.

---

## 5. Step 5b Cap Arbitration — 3-Tier

Replace the existing HVN cap in `_Verdict.vb` Step 5b. The new arbitration prefers, in order:

1. **Swing target** in the entry direction (closest to trader's actual rule)
2. **Nearest HVN in direction of travel** (from VPFR-lite v2)
3. **POC** (broader gravity — fallback)

The cap fires when **any** of these is closer than the raw ATR target. The "winner" is the closest among those that qualify.

### 5a. Long Cap — Updated Logic

```vb
Dim hvnAbove As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR")

Dim capLongTarget As Double = 0
Dim capLongLabel  As String = ""

' 1. Swing target -- highest priority
If r.SwingTargetLong > 0 AndAlso r.SwingTargetLong < rawLongTarget Then
    capLongTarget = r.SwingTargetLong
    capLongLabel  = "SWING_HIGH_5M"
End If

' 2. Nearest HVN above (from VPFR-lite v2; ignore if v2 not yet shipped → falls through)
If r.VPFRNearestHvnAbove > 0 AndAlso r.VPFRNearestHvnAbove > r.CurrentPrice AndAlso
   r.VPFRNearestHvnAbove < rawLongTarget AndAlso
   (capLongTarget = 0 OrElse r.VPFRNearestHvnAbove < capLongTarget) Then
    capLongTarget = r.VPFRNearestHvnAbove
    capLongLabel  = "NEAREST_HVN_ABOVE"
End If

' 3. POC fallback
If hvnAbove AndAlso r.VPFRPoc > r.CurrentPrice AndAlso r.VPFRPoc < rawLongTarget AndAlso
   (capLongTarget = 0 OrElse r.VPFRPoc < capLongTarget) Then
    capLongTarget = r.VPFRPoc
    capLongLabel  = "POC"
End If

If capLongTarget > 0 Then
    res.AdjustedLongTarget = capLongTarget
    res.TargetCapReason    = String.Format("CAPPED @ {0:F1} ({1})", capLongTarget, capLongLabel)
End If
```

### 5b. Short Cap — Mirror Logic

Same pattern with `SwingTargetShort`, `VPFRNearestHvnBelow`, and the existing `hvnBelow` POC condition.

### 5c. Backwards Compatibility

If swing detection ships **before** VPFR-lite v2, layer 2 (`r.VPFRNearestHvnAbove`) is unavailable. The arbitration code references the field; if v2 hasn't shipped, those fields don't exist → compile error. **Implementation order matters**: ship VPFR-lite v2 first (it's lower-risk and self-contained), then ship swing detection on top. If you reverse the order, omit layer 2 from the swing spec implementation and add it in a follow-up patch when v2 lands.

If swing detection ships **after** VPFR-lite v2 (recommended), all three layers work as designed.

---

## 6. CalcHoldStatus — Structural Break Exit

`Core/ScoringEngine_Helpers.vb` — `CalcHoldStatus` Layer 2 currently has:

```vb
' Layer 2: structural divergence exits
If r.ROC < 0 Then Return "EXIT -- momentum break (ROC crossed below 0)"
If r.OBVDivergence = "BEARISH" Then Return "EXIT -- OBV bearish divergence"
If r.RSIDivergence = "BEARISH" Then Return "EVALUATE -- RSI bearish divergence, watch for reversal"
```

Insert a **new Layer 1.5** between Layer 1 (microstructure) and Layer 2 (momentum/divergence):

```vb
' Layer 1.5: structural break exit -- highest-priority structural signal
' If price closes through the prior 5m swing low while long, the trade thesis is
' structurally invalidated regardless of momentum/microstructure.
If r.LastSwingLow5m > 0 AndAlso r.CurrentPrice <= r.LastSwingLow5m Then
    Return String.Format("EXIT -- structural break (closed at/below swing low {0:F1})", r.LastSwingLow5m)
End If
```

Mirror for `InShort`:
```vb
If r.LastSwingHigh5m > 0 AndAlso r.CurrentPrice >= r.LastSwingHigh5m Then
    Return String.Format("EXIT -- structural break (closed at/above swing high {0:F1})", r.LastSwingHigh5m)
End If
```

This sits **above** the OBV/RSI divergence layer because structural break is the cleanest invalidation signal in the trader's stated playbook (Section 5: "Exit if RSI < 40 or ROC crosses below 0" is the *fallback*; "trend structure hasn't broken" is the *primary* hold condition).

The microstructure 2-of-N fast exit (Layer 1) still has highest priority — order flow can deteriorate before price reaches the swing low, and getting out before structure breaks is strictly better than after.

---

## 7. VerdictContext Refinement — Sharper STRUCTURALLY_WEAK

`CalcVerdictContext` currently classifies STRUCTURALLY_WEAK when `structScore < 2 AND flowScore < 2`. This is signal-count-based, which is OK as a fallback but misses the actual structural condition: **"entry has no clean structural target within reach"**.

Add a structural-target test as a **first-priority STRUCTURALLY_WEAK condition**, before the existing signal-count check:

```vb
' First check: is there a clean structural target in the entry direction?
' If swing detection has run AND the swing target is missing, the entry is
' structurally ambiguous (mid-channel, no clean exit reference).
Dim hasStructuralTarget As Boolean = If(isLong,
    r.SwingTargetLong > 0 AndAlso r.SwingStopLong > 0,
    r.SwingTargetShort > 0 AndAlso r.SwingStopShort > 0)
If r.LastSwingHigh5m > 0 OrElse r.LastSwingLow5m > 0 Then
    ' Swing detection has produced at least one level -- meaningful evaluation possible
    If Not hasStructuralTarget Then
        Return "STRUCTURALLY_WEAK"
    End If
End If

' Fall through to existing signal-count logic
```

This refinement only fires when swing detection produces output — graceful degradation if swing data is missing (e.g. very early in the session). Otherwise the existing rule still applies.

---

## 8. Files Changed Summary

| File | Change |
|---|---|
| `Core/IndicatorResults.vb` | Add 8 properties: `LastSwingHigh5m/Low5m/High15m/Low15m`, `SwingTargetLong/StopLong/TargetShort/StopShort` |
| `Core/Indicators_Structure.vb` | Add `CalcSwingPivots()` shared sub |
| `Core/ScoringEngine_Helpers.vb` | Insert Layer 1.5 structural-break exit in `CalcHoldStatus` |
| `Core/ScoringEngine_Calculate_Verdict.vb` | Replace Step 5b cap with 3-tier arbitration (swing → HVN → POC) |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Update `CalcVerdictContext` with structural-target first check |
| `Core/Settings/EngineSettings.vb` | Add `SwingSettings` class + `Swing` property |
| `UI/MainForm_Analysis.vb` | Call `CalcSwingPivots(candles5m, ...)` and `(candles15m, ...)`; populate `SwingTarget*` / `SwingStop*` |
| `UI/MainForm_Render_Header.vb` | Add structural rows beneath ATR Long/Short rows |
| `UI/MainForm_Render_Sections.vb` | Optional: add a "STRUCTURE (5m)" section showing both swing levels + 15m context |
| `settings.json` | Add `"swing"` block under `"indicators"` |

No changes to OFI, CVD, MTF gate, Pass 2c, regime classification, Kelly.

---

## 9. Settings Keys

### `Core/Settings/EngineSettings.vb`

```vb
''' <summary>
''' Swing pivot detection parameters for 5m primary and 15m context.
''' </summary>
Public Class SwingSettings
    ''' <summary>Pivot wing on 5m (bars left/right confirming a swing). Default 3.</summary>
    <JsonPropertyName("pivot_wing_5m")>     Public Property PivotWing5m     As Integer = 3
    ''' <summary>Lookback bars on 5m to scan for the most recent swing. Default 30.</summary>
    <JsonPropertyName("lookback_bars_5m")>  Public Property LookbackBars5m  As Integer = 30
    ''' <summary>Pivot wing on 15m. Default 2 (slower timeframe needs less wing).</summary>
    <JsonPropertyName("pivot_wing_15m")>    Public Property PivotWing15m    As Integer = 2
    ''' <summary>Lookback bars on 15m. Default 20 (proportionally smaller window).</summary>
    <JsonPropertyName("lookback_bars_15m")> Public Property LookbackBars15m As Integer = 20
End Class

' Add to IndicatorSettings class:
<JsonPropertyName("swing")> Public Property Swing As New SwingSettings
```

### `settings.json` — add under `"indicators"`

```json
"swing": {
  "pivot_wing_5m": 3,
  "lookback_bars_5m": 30,
  "pivot_wing_15m": 2,
  "lookback_bars_15m": 20
}
```

| Key | Default | Purpose |
|---|---|---|
| `pivot_wing_5m` | 3 | 3 bars left/right confirms a 5m swing (≈ 15min on either side) |
| `lookback_bars_5m` | 30 | Scan back ~150 minutes for the most recent confirmed swing |
| `pivot_wing_15m` | 2 | 2 bars left/right on 15m (≈ 30min either side) |
| `lookback_bars_15m` | 20 | Scan back ~5 hours on 15m for higher-timeframe context |

---

## 10. Worked Examples

### Example A — Long with clean swing structure

```
Setup: BTC at 78285. 5m candles show:
  - Swing low confirmed 8 bars ago at 78240.0
  - Swing high confirmed 4 bars ago at 78420.0

CalcSwingPivots returns:
  LastSwingHigh5m = 78420.0
  LastSwingLow5m  = 78240.0
SwingTargetLong = 78420.0  (above entry)
SwingStopLong   = 78240.0  (below entry)
Risk = 78285 - 78240 = 45.0
Reward = 78420 - 78285 = 135.0
Swing R:R = 1:3.0

ATR levels (existing): Stop=78275.9 / Target=78300.2 (R:R 1:1.7, ATR-basis)

Step 5b cap: r.SwingTargetLong (78420) is NOT < rawLongTarget (78300.2)
              → swing target is FURTHER than ATR target → no cap fires
              (the ATR target is reachable before swing high)

Display:
  ATR ENTRY LEVELS (ATR 12.83 x 0.59 scale | 1.2x stop / 2.0x target)
    Long:               Stop 78275.9  |  Entry 78285.0  |  Target 78300.2    R:R 1:1.7  (risk 9.1 / rwd 15.2)
    Long structural:    Stop 78240.0  |  Entry 78285.0  |  Target 78420.0    R:R 1:3.0  (risk 45.0 / rwd 135.0)
    Short:              Stop 78294.1  |  Entry 78285.0  |  Target 78269.8    R:R 1:1.7  (risk 9.1 / rwd 15.2)
    Short structural:   Target 78240.0  (no swing high above entry within lookback)
                        ← only swing low exists; can serve as short target but no structural stop above

VerdictContext: hasStructuralTarget = True → fall through to signal-count check (unchanged)

CalcHoldStatus (if InLong): currentPrice (78285) > LastSwingLow5m (78240) → no structural break, normal evaluation
```

### Example B — Long mid-channel, no clean target

```
Setup: BTC at 78285. 5m candles:
  - Most recent confirmed swing high: 78420.0
  - Most recent confirmed swing low:  78250.0
  - But: rawLongTarget (ATR-derived) = 78380.0 → swing high is FURTHER than ATR target
    (entry has clean room to ATR target)

This is NOT mid-channel; this is normal. Example A handled it.

Mid-channel actually means: BOTH swing high and swing low are far from entry, OR
the swing target is FURTHER than where ATR predicts price will go AND there's no
HVN wall in between either.

Concretely, mid-channel = no swing target within the ATR target distance, no HVN
nearer than the swing target. The signal: structurally the entry is in a "no man's
land" between previous decisions. The engine can still trade it, but VerdictContext
should warn STRUCTURALLY_WEAK.

VerdictContext refinement check:
  r.LastSwingHigh5m = 78420 (swing detection ran)
  hasStructuralTarget for long = (SwingTargetLong > 0 AND SwingStopLong > 0)
  SwingTargetLong = 78420 > entry → present (>0)
  SwingStopLong   = 78250 < entry → present (>0)
  hasStructuralTarget = True → no STRUCTURALLY_WEAK fires from new check

So this example passes the structural test. Good.
```

### Example C — Long with swing high BELOW entry (degenerate)

```
Setup: BTC at 78420 (just broke above the prior swing high of 78400). 5m:
  - Most recent confirmed swing high: 78400.0 (now BELOW entry)
  - Most recent confirmed swing low:  78310.0

SwingTargetLong = 0  (LastSwingHigh5m=78400 is NOT > entry=78420)
SwingStopLong   = 78310.0 (still below)

Display:
  Long structural:    Stop 78310.0  (no swing high above entry within lookback)

VerdictContext check:
  r.LastSwingHigh5m > 0 → swing detection ran
  hasStructuralTarget = False (SwingTargetLong = 0)
  → STRUCTURALLY_WEAK fires

Reasoning: just made a new high, no prior structural target above for a long. Trader needs
to either wait for the next swing high to form, or treat this as breakout-without-target
(speculative). The tag warns appropriately.
```

### Example D — Structural break exit while in long

```
Setup: User in long. BTC was 78285, now 78230. LastSwingLow5m = 78240.

CalcHoldStatus called with posState = InLong:
  Layer 1: microstructure check (suppose adverseCount = 1, no fast exit)
  Layer 1.5: r.LastSwingLow5m (78240) > 0 AND r.CurrentPrice (78230) <= r.LastSwingLow5m
             → return "EXIT -- structural break (closed at/below swing low 78240.0)"

Display: HOLD/EXIT row shows the structural break message in C_WARN bold.
```

---

## 11. What This Does NOT Do

- Does **not** add swing-pivot scoring to the breakdown table — pivots are display + arbitration + exit, not a scored signal (avoids double-counting structural signals already covered by EMA/Donchian/VPFR)
- Does **not** track multiple historical swings — just the most recent confirmed in each direction
- Does **not** add Higher High / Higher Low / Lower High / Lower Low (HH/HL/LH/LL) trend structure classification — that's a separate spec, deferred to backlog
- Does **not** add session-anchored swings (yesterday's high/low) — deferred to multi-session work in `post-websocket-post-calibration-backlog.md`
- Does **not** affect MTF gate, Pass 2c, OFI, CVD, Kelly, regime classification
- Does **not** add CSV columns initially — backlog
- Does **not** depend on VPFR-lite v2 to ship — but the 3-tier cap arbitration (Section 5) requires VPFR v2 fields. Implementation order: ship VPFR v2 first, then swing.

---

## 12. Validation Plan

After 30+ live runs:

1. **Pivot detection sanity.** Spot-check 5–10 logged runs against TradingView 5m chart — does the engine's `LastSwingHigh5m` / `LastSwingLow5m` agree with what you'd manually annotate?
2. **Cap arbitration impact.** How often does the cap fire on swing target vs HVN vs POC vs no cap? Distribution should make sense — swing wins most often when entry is near recent structure, HVN wins in mid-range, POC rarely (fallback).
3. **Structural break exit accuracy.** When Layer 1.5 fires "EXIT — structural break", does the price action vindicate the call (continuation lower) or did it whip back? If whip rate > 30%, raise `pivot_wing_5m` to 4 (more conservative pivot confirmation).
4. **STRUCTURALLY_WEAK frequency.** New tag firing in mid-channel should be uncommon (< 15% of WEAK verdicts). If higher, the structural-target test is over-firing.

Add to Section 12 backlog after implementation.

---

## 13. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should swing pivots score (e.g. award a structural-aligned bonus) or only inform display + arbitration? | **Display + arbitration + exit only.** Scoring overlap with EMA/Donchian/VPFR is real and would double-count structural signals | Resolved |
| Q2 | Pivot wing on 5m — 2, 3, or 4? | **3** is the standard scalping default. Wing 2 catches noise; wing 4 misses fresh swings during fast moves. Tunable | Resolved |
| Q3 | What if both swing high and swing low are missing (very early in session)? | Display shows "—" for both; cap arbitration falls back to HVN/POC; CalcHoldStatus skips Layer 1.5; VerdictContext skips structural-target check (graceful degradation) | Resolved |
| Q4 | Should 15m swing levels appear in the ATR display block? | Not in the ATR row — keep that 5m-only. Surface 15m in a separate STRUCTURE section if rendered (optional v1 component) | Resolved |
| Q5 | How does this interact with Donchian(20) on 1m? | They're independent — Donchian is 1m channel high/low (last 20 bars); swing pivots are 5m structural pivots. Both display. Donchian doesn't cap targets — it only scores. No conflict | Resolved |
| Q6 | What if the most recent confirmed swing high was 60+ bars ago (longer than lookback_bars_5m)? | Returns 0 → swing target missing → cap arbitration uses HVN/POC fallback. The 30-bar lookback (~150min) is generous for 1m-execution scalping; if you regularly need longer, raise the setting | Resolved |
| Q7 | What about pivot quality (depth of swing, volume at swing)? | Out of scope for v1 — equal-depth swings are treated equally. Volume-weighted pivot ranking is a v2 feature, deferred | Resolved (deferred) |
| Q8 | Should swing detection respect VPFR HVN — i.e. only count pivots that occurred at HVN levels? | No — that's combining two signals. Keep them independent; arbitration in Step 5b already orders them by priority | Resolved |
| Q9 | Should `SwingTargetLong = 0` when the swing high is *below* entry (price has broken out)? | **Yes** — bookkeeping fields are direction-aware. A swing high below entry is no longer a target; it's a former resistance now acting as support. The raw `LastSwingHigh5m` remains populated for VerdictContext / display purposes | Resolved |
