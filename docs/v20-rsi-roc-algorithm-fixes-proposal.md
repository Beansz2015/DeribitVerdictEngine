# Spec: v20 RSI Divergence + ROC Sensitivity Algorithm Fixes
**Proposed:** 2026-04-30
**Status:** APPROVED 2026-04-30
**Target files:** `Core/Indicators_Momentum.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Analysis.vb`, `settings.json`

Two related algorithmic fixes flagged in the v18→v19 calibration audit but deferred from the threshold-tuning pass because they require code changes:

1. **`CalcRSIDivergence` over-firing at 80% non-NONE.** The detector finds any swing pivot in the lookback and labels pullbacks as divergence. Real divergence is rare — the algorithm semantics are wrong, not just the thresholds.
2. **`ROC.slope_sensitivity` cfg key conflated across three distinct scoring decisions.** One threshold serves slope-delta classification, partial-ROC magnitude check, and Pass 2c ROC activation. Tuning it for one purpose breaks the other two.

Both fixes ship together in v20 because both touch the ROC/RSI scoring layer and they share the calibration window. Single commit.

**Coordination with v19.** v20 must ship **after** v19. v19 tunes `slope_sensitivity = 0.05` as a blanket lowering; v20 splits the key and migrates the value. If you ship v20 first, the v19 cfg key reference becomes invalid.

---

## 1. Problem Statement

### 1a. RSI Divergence Over-Firing

Across 618 calibration rows, `RSIDivergence` distribution:

| Value | Count | Share |
|---|---|---|
| BEARISH | 280 | 45.3% |
| BULLISH | 214 | 34.6% |
| NONE | 124 | 20.1% |

**80% non-NONE rate is too frequent for what should be a rare exhaustion signal.** Real bearish divergences (price makes higher high, RSI fails to confirm) are rare reversal patterns — typical detection rates in scalping strategies are 5–15% of runs.

**Root cause** in `CalcRSIDivergence` (`Core/Indicators_Momentum.vb`):

```vb
If bestHighPivotIdx >= 0 AndAlso
   bestHighPrice > currentPrice * (1.0 + priceGate) AndAlso
   bestHighRSI > currentRSI + rsiDelta Then
    Return "BEARISH"
End If
```

The algorithm:
1. Finds the **highest** swing high in the 30-bar lookback (`bestHighPrice`).
2. Fires BEARISH if **prior pivot price > current price** (i.e., we're below recent peak).
3. AND **prior pivot RSI > current RSI** (i.e., RSI is also below where it was at the peak).

**Three semantic problems:**

- **Direction inverted from canonical divergence pattern.** Regular bearish divergence is *price makes a HIGHER high, RSI makes a LOWER high* — current price should be at or above the prior pivot, not below it. The current code fires whenever current is below the recent peak, which is just "any pullback in a downtrend."
- **No overbought requirement at the prior pivot.** A pivot at RSI 55 is normal-bullish, not exhaustion. Genuine divergence requires the prior peak to have been in overbought territory (RSI ≥ 65 or 70) at the time. Without this gate, any random pivot qualifies.
- **"Highest pivot in lookback" biases toward old extremes.** A high pivot 30 bars ago that happens to be the highest in the window doesn't say anything about the current setup. The most recent pivot is what matters for current price action.

Bullish divergence has the symmetric problems.

Net effect: in a downtrending market, ANY pullback satisfies "below recent high AND lower RSI." BEARISH fires constantly. In an uptrending market, BULLISH fires constantly. Together they cover ~80% of runs.

**Step 2 scoring impact.** The existing scoring uses `RSIDivergence` to apply directional penalties:

```vb
If r.RSIDivergence = "BEARISH" AndAlso r.RSI > rsiDivPenaltyHigh Then
    state.LongScore = Math.Max(0, state.LongScore - 1)
End If
If r.RSIDivergence = "BULLISH" AndAlso r.RSI < rsiDivPenaltyLow Then
    state.ShortScore = Math.Max(0, state.ShortScore - 1)
End If
```

Note the secondary RSI gate (`r.RSI > 65` / `r.RSI < 35`) somewhat protects against the worst over-firing — the penalty doesn't apply unless current RSI is also extreme. But the divergence FLAG itself is in the CSV at 80% non-NONE, polluting calibration data.

**`CalcHoldStatus` impact.** Layer 2 of CalcHoldStatus reads RSIDivergence to suggest "EVALUATE -- watch for reversal." Currently surfacing this on 80% of in-position runs is noise.

### 1b. ROC `slope_sensitivity` Conflation

`cfg.Indicators.ROC.SlopeSensitivity` (currently 0.1 in v18, 0.05 in v19) controls **three semantically distinct scoring decisions**:

**Use 1 — Slope-delta classification** (`UI/MainForm_Analysis.vb`):

```vb
Dim delta As Double = rocSeries.Last() - rocSeries(rocSeries.Count - 2)
Dim slopeSens As Double = cfg.Indicators.ROC.SlopeSensitivity
r.ROCSlope = If(delta > slopeSens, "RISING", If(delta < -slopeSens, "FALLING", "FLAT"))
```

This is the **change in ROC** between two consecutive samples. For a 9-bar ROC on BTC, bar-to-bar ROC delta is typically ±0.02% to ±0.08% during quiet conditions. A threshold of 0.1% is too coarse → ROCSlope is FLAT 94% of the time.

**Use 2 — rocPartial magnitude check** (`Core/ScoringEngine_Calculate_Scoring.vb`):

```vb
Dim rocPartialLong  As Boolean = r.ROC > cfg.Indicators.ROC.SlopeSensitivity AndAlso r.ROCSlope <> "RISING"
Dim rocPartialShort As Boolean = r.ROC < -cfg.Indicators.ROC.SlopeSensitivity AndAlso r.ROCSlope <> "FALLING"
```

This is the **absolute ROC value** — used to award a partial signal when ROC is meaningfully off zero but slope hasn't yet been confirmed. A threshold of 0.1% means "ROC has moved at least 0.1% over the last 9 bars" — substantial but not extreme. Lowering this to 0.05 (v19) means partial fires on smaller ROC values, which adds noise.

**Use 3 — Pass 2c regime-alignment activation** (`Core/ScoringEngine_Calculate_Scoring.vb`):

```vb
Dim rocActive As Boolean = Math.Abs(r.ROC) >= cfg.Indicators.ROC.SlopeSensitivity
```

This is also the **absolute ROC value** — gates whether ROC participates in the TRENDING regime alignment check. A threshold of 0.1% is the natural "ROC is non-zero enough to vote" floor. Lowering to 0.05 makes ROC vote in Pass 2c on smaller magnitudes.

**The conflation problem:** uses 1, 2, and 3 measure different things (delta vs magnitude) on different scales. Tuning the single threshold for use 1 (where 0.1 is too coarse) breaks uses 2 and 3 (where 0.1 was correct).

v19 set `slope_sensitivity = 0.05` to make ROCSlope fire more often. This was the right direction for use 1 but adds noise to uses 2 and 3.

The fix is to **split the cfg key into two**, with each scoring use referencing the appropriate one.

---

## 2. Fix 1: RSIDivergence Algorithmic Redesign

### 2a. Algorithm — regular divergence only (no hidden divergence)

For **BEARISH** divergence:

1. Walk **backward** from the most recent confirmable index. The first confirmed swing high found is the most recent pivot. (Replaces "find the highest pivot in lookback.")
2. **Pivot must have been overbought** when it formed: `pivotRSI >= overboughtThreshold` (default 65). No divergence semantics without prior overbought.
3. **Current price must be at or above the prior pivot** (testing the high or breaking it): `currentPrice >= pivotPrice * (1 - priceGate)`. (Direction reversed from current code.)
4. **Current RSI must be meaningfully below the pivot's RSI**: `pivotRSI - currentRSI >= rsiDelta` (default 5, raised from 2). RSI compression at the new test = exhaustion.

For **BULLISH** divergence: mirror with oversold pivot (RSI ≤ 35) and current price at or below pivot price.

The four conditions together capture the canonical exhaustion pattern: price tests resistance, RSI fails to confirm. No false fires from "any pullback in a downtrend."

### 2b. Updated function signature

```vb
Public Shared Function CalcRSIDivergence(candles As List(Of Candle), period As Integer,
                                          priceGate As Double, rsiDelta As Double,
                                          Optional pivotWing As Integer = 3,
                                          Optional lookbackBars As Integer = 30,
                                          Optional overboughtThreshold As Double = 65.0,
                                          Optional oversoldThreshold As Double = 35.0) As String
```

Two new optional parameters: `overboughtThreshold` and `oversoldThreshold`. Defaults match the proposed cfg defaults.

### 2c. Updated implementation

Full rewrite of the function body in `Core/Indicators_Momentum.vb`:

```vb
Public Shared Function CalcRSIDivergence(candles As List(Of Candle), period As Integer,
                                          priceGate As Double, rsiDelta As Double,
                                          Optional pivotWing As Integer = 3,
                                          Optional lookbackBars As Integer = 30,
                                          Optional overboughtThreshold As Double = 65.0,
                                          Optional oversoldThreshold As Double = 35.0) As String
    Dim minNeeded As Integer = period + lookbackBars + pivotWing
    If candles.Count < minNeeded Then Return "NONE"

    Dim rsiSeries = CalcRSISeries(candles, period)
    If rsiSeries.Count < lookbackBars Then Return "NONE"

    Dim scanEnd   As Integer = rsiSeries.Count - 1
    Dim scanStart As Integer = Math.Max(pivotWing, scanEnd - lookbackBars)

    Dim currentRSI   As Double = rsiSeries(scanEnd)
    Dim currentPrice As Double = candles.Last().Close

    ' ---- BEARISH divergence ----
    ' Walk backward from most recent confirmable index. First confirmed swing high = most recent pivot.
    Dim foundHighIdx   As Integer = -1
    Dim foundHighPrice As Double  = 0
    Dim foundHighRSI   As Double  = 0
    For i As Integer = scanEnd - pivotWing To scanStart Step -1
        Dim candleIdx As Integer = i + period
        If candleIdx < pivotWing OrElse candleIdx >= candles.Count - pivotWing Then Continue For
        Dim iPrice As Double = candles(candleIdx).High
        Dim isSwingHigh As Boolean = True
        For w As Integer = 1 To pivotWing
            If candles(candleIdx - w).High >= iPrice OrElse
               candles(candleIdx + w).High >= iPrice Then
                isSwingHigh = False : Exit For
            End If
        Next
        If isSwingHigh Then
            foundHighIdx   = i
            foundHighPrice = iPrice
            foundHighRSI   = rsiSeries(i)
            Exit For
        End If
    Next

    If foundHighIdx >= 0 Then
        ' (1) Pivot must have been in overbought territory
        If foundHighRSI >= overboughtThreshold Then
            ' (2) Current price must be at or above pivot (testing the high or breaking it)
            If currentPrice >= foundHighPrice * (1.0 - priceGate) Then
                ' (3) RSI compression: current must be meaningfully lower than pivot's RSI
                If foundHighRSI - currentRSI >= rsiDelta Then
                    Return "BEARISH"
                End If
            End If
        End If
    End If

    ' ---- BULLISH divergence: mirror logic with oversold pivot ----
    Dim foundLowIdx   As Integer = -1
    Dim foundLowPrice As Double  = 0
    Dim foundLowRSI   As Double  = 0
    For i As Integer = scanEnd - pivotWing To scanStart Step -1
        Dim candleIdx As Integer = i + period
        If candleIdx < pivotWing OrElse candleIdx >= candles.Count - pivotWing Then Continue For
        Dim iPrice As Double = candles(candleIdx).Low
        Dim isSwingLow As Boolean = True
        For w As Integer = 1 To pivotWing
            If candles(candleIdx - w).Low <= iPrice OrElse
               candles(candleIdx + w).Low <= iPrice Then
                isSwingLow = False : Exit For
            End If
        Next
        If isSwingLow Then
            foundLowIdx   = i
            foundLowPrice = iPrice
            foundLowRSI   = rsiSeries(i)
            Exit For
        End If
    Next

    If foundLowIdx >= 0 Then
        ' (1) Pivot must have been in oversold territory
        If foundLowRSI <= oversoldThreshold Then
            ' (2) Current price must be at or below pivot (testing the low or breaking it)
            If currentPrice <= foundLowPrice * (1.0 + priceGate) Then
                ' (3) RSI rise: current must be meaningfully higher than pivot's RSI
                If currentRSI - foundLowRSI >= rsiDelta Then
                    Return "BULLISH"
                End If
            End If
        End If
    End If

    Return "NONE"
End Function
```

Net diff: ~80 lines (full rewrite of the divergence body within the function).

### 2d. RSI cfg key changes

In `Core/Settings/EngineSettings.vb` `RsiSettings` class, add two new properties:

```vb
''' <summary>
''' [v20] Pivot RSI must be at or above this for BEARISH divergence to fire.
''' Captures "exhaustion at prior overbought" semantics.
''' Default 65.0.
''' </summary>
<JsonPropertyName("divergence_overbought_threshold")>
Public Property DivergenceOverboughtThreshold As Double = 65.0

''' <summary>
''' [v20] Pivot RSI must be at or below this for BULLISH divergence to fire.
''' Captures "exhaustion at prior oversold" semantics.
''' Default 35.0.
''' </summary>
<JsonPropertyName("divergence_oversold_threshold")>
Public Property DivergenceOversoldThreshold As Double = 35.0
```

And tighten the existing `divergence_rsi_delta` default:

```vb
' Was: divergence_rsi_delta = 2.0
<JsonPropertyName("divergence_rsi_delta")>
Public Property DivergenceRsiDelta As Double = 5.0   ' raised from 2.0 — meaningful compression required
```

### 2e. Call site update — `UI/MainForm_Analysis.vb`

The existing call site already passes `pivotWing` and `lookbackBars` from cfg. Add the two new parameters:

**Before:**

```vb
r.RSIDivergence = IndicatorEngine.CalcRSIDivergence(candles1m,
                      cfg.Indicators.RSI.Period,
                      cfg.Indicators.RSI.DivergencePriceGate,
                      cfg.Indicators.RSI.DivergenceRsiDelta,
                      pivotWing:=cfg.Indicators.RSI.PivotWing,
                      lookbackBars:=cfg.Indicators.RSI.LookbackBars)
```

**After:**

```vb
r.RSIDivergence = IndicatorEngine.CalcRSIDivergence(candles1m,
                      cfg.Indicators.RSI.Period,
                      cfg.Indicators.RSI.DivergencePriceGate,
                      cfg.Indicators.RSI.DivergenceRsiDelta,
                      pivotWing:=cfg.Indicators.RSI.PivotWing,
                      lookbackBars:=cfg.Indicators.RSI.LookbackBars,
                      overboughtThreshold:=cfg.Indicators.RSI.DivergenceOverboughtThreshold,
                      oversoldThreshold:=cfg.Indicators.RSI.DivergenceOversoldThreshold)
```

### 2f. settings.json update

In the `RSI` block under `indicators`:

```json
"RSI": {
  "period": 9,
  "oversold": 40.0,
  "overbought": 60.0,
  "partial_oversold": 45.0,
  "partial_overbought": 55.0,
  "divergence_price_gate": 0.001,
  "divergence_rsi_delta": 5.0,
  "div_penalty_rsi_high": 65.0,
  "div_penalty_rsi_low": 35.0,
  "pivot_wing": 2,
  "lookback_bars": 20,
  "pass2c_midline": 50.0,
  "divergence_overbought_threshold": 65.0,
  "divergence_oversold_threshold": 35.0
}
```

Two new keys appended; `divergence_rsi_delta` raised from 2.0 to 5.0.

### 2g. Expected effect

Post-v20, RSIDivergence distribution should compress dramatically:

| Value | v18 share | v20 expected share |
|---|---|---|
| BEARISH | 45% | 5–10% |
| BULLISH | 35% | 5–10% |
| NONE | 20% | 80–90% |

When BEARISH or BULLISH does fire, it represents a genuine exhaustion pattern — prior pivot in overbought/oversold territory, current price testing/exceeding it, RSI compression at the new test.

---

## 3. Fix 2: ROC `slope_sensitivity` Split

### 3a. The split

Replace the single `slope_sensitivity` cfg key with two semantically-distinct keys:

| New key | Purpose | Default |
|---|---|---|
| `slope_delta_threshold` | Slope-delta classification (Use 1 — `r.ROCSlope`) | **0.05** (matches v19 value) |
| `magnitude_threshold` | Magnitude check (Uses 2 + 3 — partial scoring + Pass 2c) | **0.1** (back to v18 value) |

### 3b. Migration from v19

v19 set `slope_sensitivity = 0.05` as a blanket lowering. v20 splits and migrates:

- `slope_delta_threshold = 0.05` — preserves v19's intent for ROCSlope classification.
- `magnitude_threshold = 0.1` — restores v18's value for partial scoring + Pass 2c.

Net effect: ROCSlope still fires more often (good — addresses v19's calibration finding), partial-ROC and Pass 2c go back to the conservative v18 magnitude threshold (avoids the noise v19 introduced).

The old `slope_sensitivity` key is **removed** from `RocSettings` and `settings.json`. Schema-clean migration; documented in `change_log`.

### 3c. EngineSettings update

In `Core/Settings/EngineSettings.vb`, replace `RocSettings`:

**Before (v19):**

```vb
Public Class RocSettings
    <JsonPropertyName("period")>            Public Property Period           As Integer = 9
    <JsonPropertyName("slope_sensitivity")> Public Property SlopeSensitivity As Double  = 0.05
    <JsonPropertyName("series_lookback")>   Public Property SeriesLookback   As Integer = 3
End Class
```

**After (v20):**

```vb
Public Class RocSettings
    <JsonPropertyName("period")>                 Public Property Period               As Integer = 9
    ''' <summary>
    ''' [v20] Threshold for ROCSlope delta classification (ROC change between consecutive samples).
    ''' delta > this → RISING; delta < -this → FALLING; else FLAT. Default 0.05.
    ''' Was conflated with magnitude_threshold under slope_sensitivity in v18/v19.
    ''' </summary>
    <JsonPropertyName("slope_delta_threshold")>  Public Property SlopeDeltaThreshold  As Double  = 0.05
    ''' <summary>
    ''' [v20] Threshold for ROC magnitude in partial scoring (rocPartialLong/Short) and
    ''' Pass 2c regime-alignment activation. Default 0.1.
    ''' Was conflated with slope_delta_threshold under slope_sensitivity in v18/v19.
    ''' </summary>
    <JsonPropertyName("magnitude_threshold")>    Public Property MagnitudeThreshold   As Double  = 0.1
    <JsonPropertyName("series_lookback")>        Public Property SeriesLookback       As Integer = 3
End Class
```

### 3d. Code site updates — three call sites

**Use 1 — `UI/MainForm_Analysis.vb`** (slope-delta classification):

```vb
' Before
Dim slopeSens As Double = cfg.Indicators.ROC.SlopeSensitivity
r.ROCSlope = If(delta > slopeSens, "RISING", If(delta < -slopeSens, "FALLING", "FLAT"))

' After
Dim slopeDelta As Double = cfg.Indicators.ROC.SlopeDeltaThreshold
r.ROCSlope = If(delta > slopeDelta, "RISING", If(delta < -slopeDelta, "FALLING", "FLAT"))
```

**Use 2 — `Core/ScoringEngine_Calculate_Scoring.vb`** (rocPartial magnitude check):

```vb
' Before
Dim rocPartialLong  As Boolean = r.ROC > cfg.Indicators.ROC.SlopeSensitivity AndAlso r.ROCSlope <> "RISING"
Dim rocPartialShort As Boolean = r.ROC < -cfg.Indicators.ROC.SlopeSensitivity AndAlso r.ROCSlope <> "FALLING"

' After
Dim rocMagnitude As Double = cfg.Indicators.ROC.MagnitudeThreshold
Dim rocPartialLong  As Boolean = r.ROC > rocMagnitude AndAlso r.ROCSlope <> "RISING"
Dim rocPartialShort As Boolean = r.ROC < -rocMagnitude AndAlso r.ROCSlope <> "FALLING"
```

**Use 3 — `Core/ScoringEngine_Calculate_Scoring.vb`** (Pass 2c rocActive):

```vb
' Before
Dim rocActive  As Boolean = Math.Abs(r.ROC) >= cfg.Indicators.ROC.SlopeSensitivity

' After
Dim rocActive  As Boolean = Math.Abs(r.ROC) >= cfg.Indicators.ROC.MagnitudeThreshold
```

### 3e. settings.json update

```json
"ROC": {
  "period": 9,
  "slope_delta_threshold": 0.05,
  "magnitude_threshold": 0.1,
  "series_lookback": 3
}
```

`slope_sensitivity` key removed.

### 3f. Expected effect

| Behaviour | v18 | v19 | v20 |
|---|---|---|---|
| ROCSlope FLAT rate | ~94% | ~80% (estimated) | ~70–80% (similar to v19) |
| Partial ROC fires when |ROC| > | 0.1 | 0.05 (noisier) | 0.1 (back to clean) |
| Pass 2c ROC activates when |ROC| ≥ | 0.1 | 0.05 (noisier) | 0.1 (back to clean) |

v20 gets the best of both: ROCSlope fires more often (as v19 wanted), partial scoring and Pass 2c maintain conservative magnitude gating (as v18 had).

---

## 4. Files Changed Summary

| File | Change | Approx LOC |
|---|---|---|
| `Core/Indicators_Momentum.vb` | Rewrite `CalcRSIDivergence` body — backward pivot scan, overbought/oversold gates, direction-corrected price condition | ~80 lines (full rewrite within the function) |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Update 2 call sites to use `MagnitudeThreshold` instead of `SlopeSensitivity` | ~4 lines |
| `Core/Settings/EngineSettings.vb` | Replace `SlopeSensitivity` with `SlopeDeltaThreshold` + `MagnitudeThreshold` in `RocSettings`. Add `DivergenceOverboughtThreshold` + `DivergenceOversoldThreshold` to `RsiSettings`. Update `DivergenceRsiDelta` default 2.0 → 5.0 | ~20 lines |
| `UI/MainForm_Analysis.vb` | Update 2 call sites: ROC slope classification (rename), RSI divergence (add 2 params) | ~6 lines |
| `settings.json` | Bump to v20. Remove `ROC.slope_sensitivity`, add `ROC.slope_delta_threshold` + `ROC.magnitude_threshold`. Add `RSI.divergence_overbought_threshold` + `RSI.divergence_oversold_threshold`. Raise `RSI.divergence_rsi_delta` 2.0 → 5.0. Add change_log entry | ~12 lines |

Total: ~120 lines net code change. Single logical commit.

---

## 5. Settings Keys

### 5a. New keys

| Key | Default | Purpose |
|---|---|---|
| `indicators.RSI.divergence_overbought_threshold` | 65.0 | Pivot RSI must be at or above for BEARISH divergence |
| `indicators.RSI.divergence_oversold_threshold` | 35.0 | Pivot RSI must be at or below for BULLISH divergence |
| `indicators.ROC.slope_delta_threshold` | 0.05 | ROCSlope classification (delta) |
| `indicators.ROC.magnitude_threshold` | 0.1 | ROC magnitude for partial scoring + Pass 2c |

### 5b. Changed defaults

| Key | v19 | v20 |
|---|---|---|
| `indicators.RSI.divergence_rsi_delta` | 2.0 | **5.0** (meaningful compression required) |

### 5c. Removed keys

| Key | Reason |
|---|---|
| `indicators.ROC.slope_sensitivity` | Replaced by `slope_delta_threshold` + `magnitude_threshold` |

---

## 6. Worked Examples

### 6a. RSI divergence — pre-fix vs post-fix

**Setup:** BTC at 77100. Lookback 30 bars. Recent swing high at 77300 (8 bars ago, RSI=72 at the time). Current RSI=58.

**Pre-fix (v18 behaviour):**

- `bestHighPrice = 77300`, `bestHighRSI = 72`
- `bestHighPrice (77300) > currentPrice (77100) * 1.001 (77177)` → True (we're below the peak)
- `bestHighRSI (72) > currentRSI (58) + 2` → True (RSI is lower than at peak)
- → **BEARISH** fires

But this is just a normal pullback in a downtrend — not divergence.

**Post-fix (v20 behaviour):**

- Most recent confirmed swing high: 77300 at index 8 bars ago, RSI=72 there
- (1) `pivotRSI (72) >= overboughtThreshold (65)` → True ✓ (overbought at pivot)
- (2) `currentPrice (77100) >= pivotPrice (77300) * (1 - 0.001) = 77222.7` → False ✗ (we're below the pivot, not testing/exceeding it)
- → **NONE** (correctly identifies this as not a divergence pattern)

**Setup B (genuine bearish divergence):** BTC at 77310 (testing prior high). Recent swing high at 77300 (8 bars ago, RSI=72). Current RSI=66.

**Post-fix:**

- (1) `pivotRSI (72) >= 65` → True ✓
- (2) `currentPrice (77310) >= 77222.7` → True ✓ (testing/breaking the high)
- (3) `pivotRSI (72) - currentRSI (66) = 6 >= rsiDelta (5)` → True ✓ (RSI compression)
- → **BEARISH** fires (correct: price tests prior high but RSI weaker)

### 6b. ROC magnitude — split effect

**Setup:** ROC = 0.07. ROCSlope delta = 0.06 (RISING).

**v19 behaviour (single threshold = 0.05):**

- Slope: `delta (0.06) > 0.05` → RISING ✓
- rocPartialLong: `ROC (0.07) > 0.05` → True (partial fires)
- Pass 2c: `|ROC (0.07)| >= 0.05` → True (ROC active)
- All three fire on a small ROC magnitude → noise

**v20 behaviour (slope_delta = 0.05, magnitude = 0.1):**

- Slope: `delta (0.06) > 0.05` → RISING ✓ (still fires — good)
- rocPartialLong: `ROC (0.07) > 0.1` → False (partial does NOT fire — clean, ROC magnitude too small to count)
- Pass 2c: `|ROC (0.07)| >= 0.1` → False (ROC does NOT activate in Pass 2c — clean)

Net: slope classification responsive, magnitude-based scoring conservative. The trader gets ROCSlope information without the partial-noise tax.

---

## 7. What This Does NOT Do

- Does **not** add hidden divergence detection (price makes new high, RSI lower at the new high). Only regular divergence (price tests prior pivot, RSI compression). Hidden divergence is a separate continuation pattern; can be added in a v21 if needed.
- Does **not** change `divergence_price_gate` (kept at 0.001 = 0.1% — already tight enough).
- Does **not** change `pivot_wing` or `lookback_bars` (kept at 2 and 20 — pivot scan parameters are independent of the algorithmic change).
- Does **not** modify `CalcRSI` or `CalcRSISeries` — the RSI calculation itself is unchanged. Only divergence detection logic.
- Does **not** modify `CalcROCSeries` — the ROC calculation itself is unchanged.
- Does **not** change scoring weights or verdict thresholds. Only the upstream classifications.
- Does **not** change `CalcHoldStatus` directly — but the RSIDivergence input it consumes will fire ~5× less often, so "EVALUATE — RSI divergence" warnings will be ~5× less frequent. Side effect; acceptable.
- Does **not** add a new VerdictContext tag or Pass change. Pure indicator-layer fix.

---

## 8. Validation Plan

After implementation:

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors.
2. **Reset Log** before the smoke test (existing 618 rows have v18-era distributions and will be incomparable post-v20).
3. **Smoke test — RSIDivergence:** run 30 analyses (mix of conditions if possible). Expected:
   - `RSIDivergence = NONE` rate climbs to ~80–90% (from v18's 20%).
   - `BEARISH` and `BULLISH` each ≤ 10–15% of runs.
   - When BEARISH or BULLISH fires, manually inspect the Signal Breakdown row and confirm the prior pivot was in overbought/oversold territory.
4. **Smoke test — ROC split:** observe `ROCSlope` distribution and partial-ROC firing in the breakdown:
   - `ROCSlope` should still fire RISING/FALLING similar to v19 rates (~20–30% non-FLAT).
   - `ROC(9)` partial flag in Signal Breakdown should fire LESS often than v19 (because magnitude_threshold = 0.1 instead of 0.05).
5. **Calibration report:** confirm the verdict context distribution didn't catastrophically shift (e.g., MOMENTUM_FADING dropping to 0% would suggest the RSI fix broke something downstream).

If any validation fails, do not push.

---

## 9. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should we support hidden divergence in addition to regular? | **No for v20.** Regular divergence is the canonical exhaustion pattern; the trader profile favors it ("divergence detection for exhaustion"). Hidden divergence is a continuation pattern with different scoring implications. Spec separately if needed | Resolved |
| Q2 | Should `overbought_threshold = 70` be stricter? | **65 is the moderate default.** Tighter (70) would reduce divergence further; looser (60) would allow more pivots. Start at 65; tune in v21 calibration if data shows over- or under-firing | Resolved |
| Q3 | Should the algorithm pick the most recent pivot OR the most overbought pivot? | **Most recent.** The most recent pivot reflects the current setup. An old high pivot from 30 bars ago doesn't say anything about current price action | Resolved |
| Q4 | What if no swing high in the lookback exists? | Function returns NONE for that direction. Same as current code's behaviour when `bestHighPivotIdx = -1` | Resolved |
| Q5 | What if the current price is exactly at the pivot? | Counts as "testing the high" — fires if other conditions met. The `>= foundHighPrice * (1 - priceGate)` allows up to 0.1% below pivot to still count | Resolved |
| Q6 | Should `slope_delta_threshold` and `magnitude_threshold` start at the same value (0.05)? | **No.** Different semantic scales — delta classification needs lower threshold (smaller bar-to-bar moves), magnitude needs higher (avoid partial-noise on tiny ROC values). 0.05 / 0.1 is the right starting split | Resolved |
| Q7 | Should the old `slope_sensitivity` cfg key be kept as a deprecated alias? | **No.** Hard rename — settings.json change_log makes the migration clear. Aliases add complexity for a single-user project | Resolved |
| Q8 | Does this break any existing CSV columns? | **No.** RSIDivergence and ROCSlope columns continue to exist in the same positions. Only the *values* change (distribution shifts) | Resolved |
| Q9 | Should we ship before or after v19? | **After v19.** v19's `slope_sensitivity = 0.05` is the migration source for v20's `slope_delta_threshold = 0.05`. If v20 ships first, v19's reference becomes invalid | Resolved |
| Q10 | What about `div_penalty_rsi_high = 65` and `div_penalty_rsi_low = 35` in scoring? | **Unchanged.** These gate the *scoring penalty* application (Step 2 in scoring pipeline), separate from divergence detection. The fact that the new overbought/oversold thresholds match these values is coincidental — both encode "65 is overbought-ish for BTC RSI(9)" but they serve different scoring stages | Resolved |
| Q11 | What if BTC is in an extreme-volatility regime where RSI swings 30+ in a few minutes? | The `rsiDelta = 5` gate still requires meaningful compression; extreme moves easily satisfy this. The pattern still requires structural prior overbought + current testing — high volatility doesn't break the algorithm, just makes both conditions easier to meet | Resolved |

---

## 10. Coordination With v19

**v20 must ship after v19.** Specifically:

1. v19 sets `slope_sensitivity = 0.05` in settings.json.
2. v20 removes `slope_sensitivity` and replaces with `slope_delta_threshold = 0.05` + `magnitude_threshold = 0.1`.

If v20 shipped before v19, the v19 spec's reference to `slope_sensitivity` would be invalid (the field would no longer exist).

**Recommended sequence:**

1. Ship v19 (calibration tunings, including `slope_sensitivity = 0.05`).
2. Smoke test v19. Confirm clean build + observable column distribution shifts.
3. Reset log.
4. Ship v20 (RSI div algorithm + ROC split). v20's diff naturally builds on v19.
5. Smoke test v20.
6. Reset log.
7. Begin calibration accumulation against v20-grade data.

If for any reason v19 is skipped: v20 still works as a standalone (the RSI fix is independent; the ROC split needs to handle migration from v18's `slope_sensitivity = 0.1` instead of v19's 0.05). In that case, v20's `slope_delta_threshold` should ship at 0.05 (the empirically-correct value for slope classification) regardless of starting point.
