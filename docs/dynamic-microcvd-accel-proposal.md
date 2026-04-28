# Spec: Dynamic MicroCVD AccelThreshold (Self-Scaling)
**Proposed:** 2026-04-27
**Status:** PROPOSED — pending user approval
**Target files:** `Core/Indicators_OrderFlow.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Analysis.vb`, `settings.json`

---

## 1. Problem Statement

`CalcMicroCVD` classifies acceleration / deceleration by comparing the late-segment USD flow against the early-segment USD flow plus a fixed offset:

```vb
If microLate > microEarly + accelThreshold Then microMomentum = "ACCELERATING"
```

`accelThreshold` is currently a static USD value (10000 in v14, raised from 5000 in earlier versions). The static threshold has two well-known failure modes:

- **Quiet sessions (Asian hours, low-flow periods):** $10K is a high bar relative to total window flow. Real acceleration in a $100K-flow window may not reach $10K differential, so genuine bull/bear acceleration gets classified as FLAT.
- **Active sessions (NY peak, news events):** $10K is trivial relative to $1M+ window flow. Random noise easily produces $10K differentials, so spurious ACCELERATING/DECELERATING classifications fire.

The existing engine pattern is **self-scaling thresholds** — `DynamicNorms` already does this for volume (`VolHighThreshold` / `VolMidThreshold` against rolling mean+stdev), VWAP deviation (`VWAPDevThreshold`), and ATR (`ATRScaleFactor`). MicroCVD's acceleration detection is the only intra-engine classifier still on a fixed USD threshold.

This spec lifts `accelThreshold` to a self-scaling formula:

```
threshold_dynamic = totalWindowUsd × dynamicPct
threshold         = max(threshold_dynamic, threshold_static × floorPct)
```

Where `totalWindowUsd = Σ trade.Amount` over the MicroCVD window. The floor against the static value prevents pathological dead-flow windows from producing nonsensically small thresholds.

Default behaviour after this spec ships: dynamic mode active with `dynamicPct = 0.03` (3% of window flow). Setting `dynamicPct = 0` reverts to the current static-only behaviour exactly.

Trader-profile alignment (Section 12 backlog): "MicroCVD `accel_threshold`: Default raised to 10000 USD in v14 (was 5000); consider dynamic scaling vs VolumeSMA on quiet sessions." This spec is the dynamic-scaling implementation. The chosen scaling source is total window USD flow (cleaner semantic match) rather than VolumeSMA9 (per-candle BTC, mismatched time horizon).

Zero new API calls. No new fetches. No new fields on `IndicatorResults`. Pure threshold formula change.

---

## 2. Computation

### 2a. Existing CalcMicroCVD (unchanged)

```vb
Public Shared Sub CalcMicroCVD(trades As List(Of TradeRecord),
                                ByRef microEarly As Double,
                                ByRef microMid As Double,
                                ByRef microLate As Double,
                                ByRef microMomentum As String,
                                ByRef microSignal As String,
                                Optional microWindowSize As Integer = 50,
                                Optional accelThreshold As Double = 5000)
```

The current acceleration check uses `accelThreshold` as a fixed USD offset.

### 2b. Updated Signature

```vb
Public Shared Sub CalcMicroCVD(trades As List(Of TradeRecord),
                                ByRef microEarly As Double,
                                ByRef microMid As Double,
                                ByRef microLate As Double,
                                ByRef microMomentum As String,
                                ByRef microSignal As String,
                                Optional microWindowSize  As Integer = 50,
                                Optional accelThreshold   As Double  = 10000,
                                Optional dynamicPct       As Double  = 0.0,
                                Optional floorPct         As Double  = 0.25)
```

Default `dynamicPct = 0.0` preserves current static behaviour at the function level. The call site in `MainForm_Analysis.vb` passes `cfg.Indicators.MicroCVD.AccelThresholdDynamicPct` which defaults to 0.03 in `settings.json` — so the **engine default is dynamic**, but the function default is static. This is intentional: the function should not silently change behaviour for any direct caller; the dynamic mode is opted into via cfg.

### 2c. Updated Acceleration Logic

Inside `CalcMicroCVD`, after `window` and segment sums are computed:

```vb
' Compute effective acceleration threshold
Dim effThreshold As Double = accelThreshold
If dynamicPct > 0.0 Then
    Dim totalUsd As Double = 0.0
    For Each t In window
        totalUsd += t.Amount
    Next
    Dim dyn As Double = totalUsd * dynamicPct
    Dim floor As Double = accelThreshold * floorPct
    effThreshold = Math.Max(dyn, floor)
End If

' Existing acceleration logic, now using effThreshold
If isBull Then
    If microLate > 0 AndAlso microLate > microEarly + effThreshold Then
        microMomentum = "ACCELERATING"
    ElseIf microLate < 0 OrElse microLate < microEarly - effThreshold Then
        microMomentum = "DECELERATING"
    Else
        microMomentum = "FLAT"
    End If
Else
    ' (mirror for bearish branch)
End If
```

`accelThreshold` is now repurposed as the **floor anchor** (when `dynamicPct > 0`) or the literal threshold (when `dynamicPct = 0`). This preserves backwards compatibility — existing callers passing only `accelThreshold` get unchanged behaviour.

### 2d. Floor Behaviour

`floorPct = 0.25` means the dynamic threshold cannot drop below 25% of the static anchor. Worked example:

- Static anchor: 10000 USD
- Floor: 10000 × 0.25 = 2500 USD
- Quiet window flow: 50000 USD
  - Dynamic: 50000 × 0.03 = 1500 USD
  - Effective: max(1500, 2500) = 2500 USD ← floor applies
- Normal window flow: 200000 USD
  - Dynamic: 200000 × 0.03 = 6000 USD
  - Effective: max(6000, 2500) = 6000 USD
- Active window flow: 500000 USD
  - Dynamic: 500000 × 0.03 = 15000 USD
  - Effective: max(15000, 2500) = 15000 USD ← stricter than static
- Very active: 1500000 USD
  - Dynamic: 1500000 × 0.03 = 45000 USD
  - Effective: 45000 USD ← much stricter than static, prevents false ACCEL on noise

The floor prevents the dead-flow degenerate case where windows with $20K total flow would trip ACCEL on $600 differentials.

### 2e. Why Total Window USD vs VolumeSMA9?

Section 13 suggested `VolumeSMA9 * 0.03`. Considered, rejected:

- `VolumeSMA9` is in **BTC**, computed over 9 **1-minute candles**. Converting to USD requires a price multiplier (`VolumeSMA9 × CurrentPrice × 0.03`), and the time horizon is fixed at 9 minutes regardless of MicroCVD's actual 50-trade window (which can be much shorter or longer than 9 minutes depending on flow rate).
- Total window USD is **directly in USD**, summed over the **same 50 trades** that produce `microEarly` / `microLate`. Same time horizon, same units, same data, no conversions.

The semantic match is cleaner. Either formulation gives self-scaling behaviour; the window-USD version has fewer moving parts.

---

## 3. Display Integration

No changes required. Display already reads `r.MicroCVDMomentum` and `r.MicroCVDSignal` from `IndicatorResults`. The classification logic just changes underneath.

Optional: append the effective threshold to the MicroCVD breakdown note (debugging visibility):

```vb
Dim microNote As String = String.Format("E:{0:F0} M:{1:F0} L:{2:F0} | {3} | {4}",
                                        r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                                        r.MicroCVDMomentum, r.MicroCVDSignal)
```

The threshold itself is internal to `CalcMicroCVD` and not currently exposed on `IndicatorResults`. Adding it would require a new field; **not in scope** for this spec. If debugging visibility becomes valuable post-ship, add `r.MicroCVDAccelThresholdUsed As Double` and append it to the note. Defer.

---

## 4. Call Site Wiring

In `UI/MainForm_Analysis.vb`, update the existing call:

**Before:**
```vb
IndicatorEngine.CalcMicroCVD(recentTrades,
                             r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                             r.MicroCVDMomentum, r.MicroCVDSignal,
                             microWindowSize:=cfg.Indicators.MicroCVD.WindowSize,
                             accelThreshold:=cfg.Indicators.MicroCVD.AccelThreshold)
```

**After:**
```vb
IndicatorEngine.CalcMicroCVD(recentTrades,
                             r.MicroCVDEarly, r.MicroCVDMid, r.MicroCVDLate,
                             r.MicroCVDMomentum, r.MicroCVDSignal,
                             microWindowSize:=cfg.Indicators.MicroCVD.WindowSize,
                             accelThreshold:=cfg.Indicators.MicroCVD.AccelThreshold,
                             dynamicPct:=cfg.Indicators.MicroCVD.AccelThresholdDynamicPct,
                             floorPct:=cfg.Indicators.MicroCVD.AccelThresholdFloorPct)
```

---

## 5. Files Changed Summary

| File | Change |
|---|---|
| `Core/Indicators_OrderFlow.vb` | Extend `CalcMicroCVD` signature with `dynamicPct` + `floorPct` optional params; add effective-threshold computation; replace `accelThreshold` literal in classification with `effThreshold` |
| `Core/Settings/EngineSettings.vb` | Extend `MicroCvdSettings` with `AccelThresholdDynamicPct` + `AccelThresholdFloorPct` |
| `UI/MainForm_Analysis.vb` | Pass new cfg keys through `CalcMicroCVD` call |
| `settings.json` | Add 2 new keys under `indicators.MicroCVD`; bump version |

No changes to `IndicatorResults`, scoring engine, display, MTF gate, Pass 2c, or any other indicator.

---

## 6. Settings Keys

### `Core/Settings/EngineSettings.vb` — extend `MicroCvdSettings`

```vb
''' <summary>
''' [P4] v0.48: MicroCVD window independent of TFI. Default 50 trades.
''' [P13] v0.50: DecelPenalty -- opposing-side penalty magnitude. Default 1.
''' [dynamic-accel] AccelThresholdDynamicPct + AccelThresholdFloorPct: enable
'''   self-scaling acceleration threshold based on total window USD flow.
''' </summary>
Public Class MicroCvdSettings
    <JsonPropertyName("window_size")>     Public Property WindowSize     As Integer = 50
    ''' <summary>Static USD acceleration threshold. Used as floor anchor when dynamic mode active. Default 10000.</summary>
    <JsonPropertyName("accel_threshold")> Public Property AccelThreshold As Double  = 10000.0
    ''' <summary>
    ''' Dynamic acceleration threshold as fraction of total window USD flow. Default 0.03 (3%).
    ''' Set to 0.0 to disable dynamic mode (uses accel_threshold as a literal static value).
    ''' </summary>
    <JsonPropertyName("accel_threshold_dynamic_pct")> Public Property AccelThresholdDynamicPct As Double = 0.03
    ''' <summary>
    ''' Floor on the dynamic threshold as fraction of accel_threshold. Default 0.25 (25%).
    ''' Prevents pathological dead-flow windows from producing nonsensically small thresholds.
    ''' </summary>
    <JsonPropertyName("accel_threshold_floor_pct")>   Public Property AccelThresholdFloorPct   As Double = 0.25
    <JsonPropertyName("decel_penalty")>   Public Property DecelPenalty   As Integer = 1
End Class
```

### `settings.json` — extend `indicators.MicroCVD`

```json
"MicroCVD": {
  "window_size": 50,
  "accel_threshold": 10000.0,
  "accel_threshold_dynamic_pct": 0.03,
  "accel_threshold_floor_pct": 0.25,
  "decel_penalty": 1
}
```

| Key | Default | Purpose |
|---|---|---|
| `window_size` | 50 | Trade window for segmentation (existing) |
| `accel_threshold` | 10000.0 | Static USD threshold (existing); also serves as floor anchor in dynamic mode |
| `accel_threshold_dynamic_pct` | 0.03 | Fraction of total window USD flow used as dynamic threshold. 0 disables dynamic mode |
| `accel_threshold_floor_pct` | 0.25 | Dynamic threshold can't drop below `accel_threshold × floor_pct` |
| `decel_penalty` | 1 | Opposing-side penalty for DECEL signals (existing) |

---

## 7. Worked Examples

### Example A — Normal flow, dynamic active

```
50-trade window. trade.Amount values sum to totalUsd = 250000 USD.
Segments: microEarly = +30000, microMid = +50000, microLate = +95000
isBull = (30000 + 50000 + 95000) > 0 = True

dynamicPct = 0.03, floorPct = 0.25, accelThreshold = 10000
threshold_dynamic = 250000 * 0.03 = 7500
threshold_floor   = 10000 * 0.25 = 2500
effThreshold      = max(7500, 2500) = 7500

Check: microLate (95000) > microEarly (30000) + 7500 = 37500? Yes (95000 > 37500).
       microMomentum = ACCELERATING
       microSignal   = BULL_ACCEL

Note vs static-only world (effThreshold = 10000):
       microLate > microEarly + 10000 = 40000? Yes (95000 > 40000).
       Same result. Dynamic and static agree in normal-flow case. Fine.
```

### Example B — Quiet flow, dynamic active

```
Quiet Asian session. totalUsd = 80000 USD across 50 trades.
Segments: microEarly = +5000, microMid = +8000, microLate = +14000
isBull = True

threshold_dynamic = 80000 * 0.03 = 2400
threshold_floor   = 10000 * 0.25 = 2500
effThreshold      = max(2400, 2500) = 2500  ← floor applies

Check: microLate (14000) > microEarly (5000) + 2500 = 7500? Yes.
       microMomentum = ACCELERATING

Static-only (effThreshold = 10000):
       microLate > microEarly + 10000 = 15000? No (14000 < 15000).
       Static would have classified as FLAT.

Outcome: Dynamic catches the genuine quiet-session acceleration that the
         static threshold misses. The floor prevents the dynamic from
         dropping below 2500, ensuring some minimum signal-to-noise gate.
```

### Example C — Active flow, dynamic active

```
NY peak. totalUsd = 1500000 USD across 50 trades.
Segments: microEarly = +180000, microMid = +220000, microLate = +250000
isBull = True

threshold_dynamic = 1500000 * 0.03 = 45000
threshold_floor   = 10000 * 0.25 = 2500
effThreshold      = max(45000, 2500) = 45000

Check: microLate (250000) > microEarly (180000) + 45000 = 225000? Yes (250000 > 225000).
       microMomentum = ACCELERATING

Static-only (effThreshold = 10000):
       microLate > microEarly + 10000 = 190000? Yes (250000 > 190000).
       Same direction, but the static threshold would also fire on much smaller
       differentials in this same active window — late=181000 would trigger
       ACCEL falsely.

Outcome: Dynamic enforces a stricter bar in active conditions, suppressing
         spurious ACCEL classifications that the static threshold lets through.
```

### Example D — dynamicPct = 0 (static-only mode)

```
Same 80000 USD quiet window from Example B.

dynamicPct = 0.0 → dynamic branch skipped → effThreshold = accelThreshold = 10000

Check: microLate (14000) > microEarly (5000) + 10000 = 15000? No.
       microMomentum = FLAT

Outcome: Identical to current v15 behaviour. Setting accel_threshold_dynamic_pct
         to 0 in settings.json gives a clean revert path if dynamic mode misbehaves.
```

---

## 8. What This Does NOT Do

- Does **not** add fields to `IndicatorResults`
- Does **not** change scoring logic — only the threshold formula behind the existing `BULL_ACCEL` / `BEAR_ACCEL` / `BULL_DECEL` / `BEAR_DECEL` / `FLAT` classification
- Does **not** affect the FLAT-stall penalty (separate Step 2 logic, unchanged)
- Does **not** add CSV columns
- Does **not** affect MicroCVDMid (still computed and displayed; only the late-vs-early acceleration check uses the dynamic threshold)

---

## 9. Validation Plan

After 50+ live runs:

1. **Distribution check.** Log `effThreshold` (temporarily, via debug print or temporary CSV field) across runs. Distribution should:
   - Hit the floor (~2500 USD) during ~10–20% of runs (genuinely quiet windows)
   - Cluster around 5000–10000 USD during normal NY/London flow (close to v14 static value)
   - Run 15000–50000+ USD during peak activity
2. **Classification stability.** Compare ACCELERATING/DECELERATING/FLAT classification rates pre-spec (v15) vs post-spec at default `dynamic_pct = 0.03`. Expect:
   - Slight increase in ACCEL/DECEL classifications during quiet hours (genuine signals previously missed)
   - Slight decrease in ACCEL/DECEL classifications during peak hours (spurious signals previously fired)
3. **Tuning candidates.** If post-launch data shows classification rates skewed:
   - ACCEL too frequent overall → raise `dynamic_pct` from 0.03 to 0.04 or 0.05
   - ACCEL too rare overall → lower `dynamic_pct` to 0.02
   - Quiet windows producing too many ACCELs → raise `floor_pct` from 0.25 to 0.30 (raises the floor)

Add to Section 12 calibration backlog after implementation.

---

## 10. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Why total window USD flow vs VolumeSMA9 × price? | Cleaner semantic match — same window, same units, no time-horizon mismatch (50 trades vs 9 candles). VolumeSMA9 is a candle-bar metric; MicroCVD operates on a trade window | Resolved |
| Q2 | Should `floorPct` reference `accel_threshold` (static anchor) or be an absolute USD value? | **Reference accel_threshold.** Tying the floor to the existing tunable means moving one number (`accel_threshold`) shifts both the dynamic floor and the static fallback together — fewer independent moving parts | Resolved |
| Q3 | Should the function default for `dynamicPct` be 0.0 or 0.03? | **0.0 at the function level**, 0.03 at the settings.json level. This way any direct caller of `CalcMicroCVD` (test code, future CLI host) gets unchanged behaviour unless they explicitly opt in. The engine default via cfg is dynamic | Resolved |
| Q4 | Should we expose the effective threshold on `IndicatorResults` for breakdown display? | **No** in v1 — keep the spec minimal. Add `r.MicroCVDAccelThresholdUsed` post-launch if debug visibility becomes valuable | Resolved (deferred) |
| Q5 | What if `totalUsd = 0` (no trades in window)? | The early-return `If trades Is Nothing OrElse trades.Count = 0 Then Return` already handles this case before threshold computation. If somehow window has trades but all `Amount = 0`, dynamic = 0 and floor takes over (returns 2500). Safe | Resolved |
| Q6 | Should this also affect the DECEL classification logic? | **Yes** — the same `effThreshold` is used in both `> microEarly + effThreshold` (ACCEL) and `< microEarly - effThreshold` (DECEL) branches. Symmetric | Resolved |
| Q7 | Coordination with `settings-exposure-pass-proposal.md`? | No conflict. The exposure pass doesn't touch MicroCVD; this spec's two new keys are additive and won't be touched by exposure | Resolved |
