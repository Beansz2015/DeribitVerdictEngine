# Spec: VPFR-Lite v2 — Value Area + Nearest HVN/LVN Walls
**Proposed:** 2026-04-27
**Status:** PROPOSED — pending user approval
**Target files:** `Core/IndicatorResults.vb`, `Core/Indicators_Structure.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/ScoringEngine_Calculate_Verdict.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Render_Sections.vb`, `settings.json`

---

## 1. Problem Statement

`CalcVPFRLite` currently returns three things:

- `r.VPFRPoc` — bucket centre of the highest-volume bucket
- `r.VPFRHVNearPoc` — boolean, "is current price within `hvnProximityPct` of POC?"
- `r.VPFRSignal` — `NEAR_HVN_SUPPORT / NEAR_HVN_RESIST / IN_LVN_BULL / IN_LVN_BEAR / NEUTRAL` based on **the current price's bucket only**, against POC volume

This is a slice of a full Volume Profile. Three real things are missing:

1. **Value Area High / Low (VAH / VAL)** — the upper and lower boundaries of the 70% volume area. These are the most-tested structural levels in volume profile theory; large traders treat VAH/VAL breakouts and rejections as primary signals. Engine has all inputs (`bucketVol` array); a sorted cumulative-volume scan to find the 70% boundaries is ~15 lines.

2. **Nearest HVN above and below current price** — explicit fields rather than "is current bucket near POC". Turns the HVN target cap from "POC happens to be in the way" into "the closest volume wall in my direction of travel is at $X". Better target candidate, better swing-vs-VPFR target arbitration once swing structure ships.

3. **Nearest LVN above and below current price** — symmetric to (2). LVNs are vacuum zones — price tends to move quickly through them. Useful as projected fast-move targets and as "no support" markers.

This proposal adds these three signal classes without removing or replacing the existing POC and current-bucket classification. Profile shape (D/P/b/bimodal) and multi-session profile (naked POCs from prior sessions) are intentionally **deferred to post-WebSocket / post-calibration backlog** — see `post-websocket-post-calibration-backlog.md`.

Zero new API calls. No new data sources. Compute cost is one extra pass over the same `bucketVol` array.

---

## 2. Computation

### 2a. Existing Computation (unchanged)

`CalcVPFRLite` already builds `bucketVol(numBuckets - 1)` from time-decayed candle volumes anchored at `priceLow`. POC is the argmax. Keep all of this.

### 2b. New: Value Area High / Low

```
Given: bucketVol(0..numBuckets-1) -- already populated
       priceLow, priceHigh, bucketSize -- already computed
       cfg.Indicators.VPFR.ValueAreaPct (default 0.70)

Procedure:
  1. totalVol = Σ bucketVol(i)
  2. targetVol = totalVol * ValueAreaPct
  3. Start at the POC bucket; cumVol = bucketVol(pocIdx)
  4. Expand outward greedily — at each step, compare bucketVol of the
     next-higher-untouched bucket vs the next-lower-untouched bucket,
     add whichever is larger (tie: lower, to bias toward conservatism).
  5. Repeat until cumVol >= targetVol.
  6. The highest-index bucket touched defines VAH; the lowest defines VAL.
  7. Compute as bucket centres (same convention as POC):
        VAH = priceLow + (vahIdx + 0.5) * bucketSize
        VAL = priceLow + (valIdx + 0.5) * bucketSize
```

**Edge cases:**
- If `ValueAreaPct >= 1.0`: VAH = priceHigh, VAL = priceLow (degenerate, full range).
- If `bucketVol` has a single dominant bucket holding ≥ ValueAreaPct of total: VAH = VAL = POC bucket centre. Acceptable degenerate state.

### 2c. New: Value Area Status

```
If currentPrice >= VAH → ValueAreaSignal = "ABOVE_VAH"   ← potential breakout up
If currentPrice <= VAL → ValueAreaSignal = "BELOW_VAL"   ← potential breakout down
Else                   → ValueAreaSignal = "INSIDE_VA"   ← rotational territory
```

Optionally distinguish "BREAKING_OUT_HIGH" (just crossed VAH this run, given prior was inside) — requires storing prior-run VA membership. Defer to v3.

### 2d. New: Nearest HVN / LVN Above and Below

A bucket is an **HVN** if `bucketVol(i) >= pocVol * cfg.Indicators.VPFR.HvnVolPct` (default 0.6 — already used by current code).
A bucket is an **LVN** if `bucketVol(i) <= pocVol * cfg.Indicators.VPFR.LvnVolPct` (default 0.2 — already used).

Find:

```
curIdx = bucket index containing currentPrice  (already computed in CalcVPFRLite)

NearestHVNAbove = price of the lowest-index HVN with index > curIdx, else 0
NearestHVNBelow = price of the highest-index HVN with index < curIdx, else 0
NearestLVNAbove = price of the lowest-index LVN with index > curIdx, else 0
NearestLVNBelow = price of the highest-index LVN with index < curIdx, else 0

Each price is bucket-centre: priceLow + (idx + 0.5) * bucketSize
0 = no such bucket exists in the visible range (at the edges or in flat profiles).
```

These give explicit walls in each direction, separate from the POC.

### 2e. New IndicatorResults Fields

```vb
Public Property VPFRVah               As Double  ' Value Area High ($)
Public Property VPFRVal               As Double  ' Value Area Low ($)
Public Property VPFRValueAreaSignal   As String  ' "INSIDE_VA" | "ABOVE_VAH" | "BELOW_VAL"
Public Property VPFRNearestHvnAbove   As Double  ' nearest HVN price above current ($), 0 = none
Public Property VPFRNearestHvnBelow   As Double  ' nearest HVN price below current ($), 0 = none
Public Property VPFRNearestLvnAbove   As Double  ' nearest LVN price above current ($), 0 = none
Public Property VPFRNearestLvnBelow   As Double  ' nearest LVN price below current ($), 0 = none
```

The existing `VPFRPoc`, `VPFRHVNearPoc`, `VPFRSignal` stay untouched.

---

## 3. Scoring Integration

### 3a. Existing VPFR Score (unchanged)

```vb
Dim vpfrLong  As Boolean = (r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL")
Dim vpfrShort As Boolean = (r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR")
AddFull(state, vpfrLong, vpfrShort, SignalCategory.MarketStructure)
```

Stays exactly as today.

### 3b. New: Value Area Boundary Bonus (Optional, default OFF)

When price closes **outside** the value area on rising volume (potential breakout), award a small partial that can upgrade via Pass 2 cross-confirmation. **This is opt-in via a config flag** so the v2 ships with display + target-cap improvements only by default; bonus scoring activates after observation.

```vb
' ValueArea breakout partial -- only when cfg flag enabled.
Dim vaPartialLong  As Boolean = False
Dim vaPartialShort As Boolean = False
If cfg.Indicators.VPFR.ValueAreaScoringEnabled Then
    Dim volExpansion As Boolean = r.VolumeRatio >= norms.VolMidThreshold
    If r.VPFRValueAreaSignal = "ABOVE_VAH" AndAlso r.ROC > 0 AndAlso volExpansion Then
        vaPartialLong = True
    ElseIf r.VPFRValueAreaSignal = "BELOW_VAL" AndAlso r.ROC < 0 AndAlso volExpansion Then
        vaPartialShort = True
    End If
End If
```

Hook into Pass 2 partial-upgrade pipeline:

```vb
Dim vaLongUpgraded  As Boolean = vaPartialLong AndAlso HasCrossConfirm(state.FullLongCategories, SignalCategory.MarketStructure)
Dim vaShortUpgraded As Boolean = vaPartialShort AndAlso HasCrossConfirm(state.FullShortCategories, SignalCategory.MarketStructure)
If vaLongUpgraded Then state.LongScore += 1
If vaShortUpgraded Then state.ShortScore += 1
```

Default config: **`ValueAreaScoringEnabled = false`** so v2 ships display + target cap behaviour only. User flips it on after observing how often `ABOVE_VAH` / `BELOW_VAL` fires and validates that breakouts genuinely confirm.

### 3c. SignalBreakdown Update

Replace the existing VPFR-lite breakdown row with one that surfaces VAH/VAL and nearest walls:

```vb
Dim vpfrNote As String = String.Format("POC:{0:F0} | VAH:{1:F0} VAL:{2:F0} | {3} | HVN^:{4:F0} HVNv:{5:F0}",
    r.VPFRPoc, r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
    r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow)
breakdown.Add(New SignalBreakdownItem("VPFR-lite",
    vpfrLong OrElse vaLongUpgraded,
    vpfrShort OrElse vaShortUpgraded,
    vpfrNote))
```

If `ValueAreaScoringEnabled = false`, `vaLongUpgraded` / `vaShortUpgraded` are always False, so the row behaves exactly as today. The richer note text shows regardless.

---

## 4. Step 5b ATR Target Cap — Use Nearest HVN/LVN

The existing HVN cap in `_Verdict.vb` Step 5b reads `r.VPFRPoc` and caps based on whether POC sits between entry and ATR target. This is correct but brittle: in a session where POC is far below the entry, the cap never fires even though there's a closer HVN wall the price will hit first.

### 4a. Updated Long Target Cap

Replace:
```vb
If hvnAbove AndAlso r.VPFRPoc > r.CurrentPrice AndAlso r.VPFRPoc < rawLongTarget Then
    res.AdjustedLongTarget = r.VPFRPoc
    res.TargetCapReason = String.Format("HVN_CAPPED @ {0:F1} (POC wall -- {1})", r.VPFRPoc, r.VPFRSignal)
End If
```

With:
```vb
' Prefer nearest-HVN-above when present and closer than POC; fall back to POC.
Dim capLongTarget As Double = 0
Dim capLongLabel  As String = ""
If r.VPFRNearestHvnAbove > 0 AndAlso r.VPFRNearestHvnAbove > r.CurrentPrice AndAlso r.VPFRNearestHvnAbove < rawLongTarget Then
    capLongTarget = r.VPFRNearestHvnAbove
    capLongLabel  = "NEAREST_HVN_ABOVE"
ElseIf hvnAbove AndAlso r.VPFRPoc > r.CurrentPrice AndAlso r.VPFRPoc < rawLongTarget Then
    capLongTarget = r.VPFRPoc
    capLongLabel  = "POC"
End If
If capLongTarget > 0 Then
    res.AdjustedLongTarget = capLongTarget
    res.TargetCapReason = String.Format("HVN_CAPPED @ {0:F1} ({1})", capLongTarget, capLongLabel)
End If
```

### 4b. Updated Short Target Cap

Mirror logic with `VPFRNearestHvnBelow` preferred over POC.

### 4c. Note on Swing Structure Interaction

When swing-pivot detection ships (separate spec), the target cap arbitration becomes:

1. Swing target (most aligned with trader's actual exit rule)
2. Nearest HVN in direction of travel (volume wall)
3. POC (broader gravity centre — fallback)

This v2 spec only covers (2) and (3). The (1) layer is added when swing detection lands. The cap reason string already supports labelling, so the swing spec just adds `"SWING_TARGET"` as a reason when applicable.

---

## 5. Display Integration

Update the MARKET STRUCTURE section in `MainForm_Render_Sections.vb`:

```vb
' Existing VPFR line stays:
AppendRtf(rtb, "  VPFR-lite: ", C_LABEL)
Dim vpfrColour As Color = If(r.VPFRSignal = "NEAR_HVN_SUPPORT" OrElse r.VPFRSignal = "IN_LVN_BULL", C_GOOD,
                             If(r.VPFRSignal = "NEAR_HVN_RESIST" OrElse r.VPFRSignal = "IN_LVN_BEAR", C_BAD, C_DIM))
AppendRtf(rtb, String.Format("POC:{0:F1}  |  {1}  |  HVN@POC:{2}",
                              r.VPFRPoc, r.VPFRSignal,
                              If(r.VPFRHVNearPoc, "YES", "NO")) & Environment.NewLine, vpfrColour)

' New: value area row
AppendRtf(rtb, "  Value Area: ", C_LABEL)
Dim vaColour As Color = If(r.VPFRValueAreaSignal = "INSIDE_VA", C_VALUE,
                            If(r.VPFRValueAreaSignal = "ABOVE_VAH", C_GOOD, C_BAD))
AppendRtf(rtb, String.Format("VAH:{0:F1}  |  VAL:{1:F1}  |  {2}",
                              r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal) & Environment.NewLine, vaColour)

' New: nearest walls row
AppendRtf(rtb, "  HVN walls: ", C_LABEL)
Dim hvnAboveStr As String = If(r.VPFRNearestHvnAbove > 0, r.VPFRNearestHvnAbove.ToString("F1"), "—")
Dim hvnBelowStr As String = If(r.VPFRNearestHvnBelow > 0, r.VPFRNearestHvnBelow.ToString("F1"), "—")
Dim lvnAboveStr As String = If(r.VPFRNearestLvnAbove > 0, r.VPFRNearestLvnAbove.ToString("F1"), "—")
Dim lvnBelowStr As String = If(r.VPFRNearestLvnBelow > 0, r.VPFRNearestLvnBelow.ToString("F1"), "—")
AppendRtf(rtb, String.Format("Above:{0}  Below:{1}  |  LVN: ^{2} v{3}",
                              hvnAboveStr, hvnBelowStr, lvnAboveStr, lvnBelowStr) & Environment.NewLine, C_DIM)
```

---

## 6. Worked Example

```
Setup: BTC at 78285, 50-bucket profile spans 78000-78600 (priceLow=78000, bucketSize=12)

Bucket volumes (sample — only key buckets shown):
  idx=18 (78216-78228): 4500   ← POC (highest)
  idx=19 (78228-78240): 3800
  idx=20 (78240-78252): 3200
  idx=21 (78252-78264): 2700
  idx=22 (78264-78276): 2100
  idx=23 (78276-78288): 1900   ← contains current price 78285
  idx=24 (78288-78300): 1600
  idx=25 (78300-78312): 1400   ← <= pocVol * 0.2 = 900? No: 1400 > 900, not LVN
  ...
  idx=30 (78360-78372): 800    ← LVN (≤ 900)
  idx=35 (78420-78432): 2900   ← HVN (≥ pocVol * 0.6 = 2700)
  idx=42 (78504-78516): 2750   ← HVN
  idx=45 (78540-78552): 600    ← LVN

POC: 78222.0 (idx=18 centre)
totalVol = 50000 (suppose); targetVol = 35000

VAH/VAL expansion from POC (greedy):
  Round 1: cumVol=4500, expand higher (idx=19, 3800) > expand lower (idx=17, 2200)
    → take idx=19. cumVol=8300
  Round 2: idx=20 (3200) vs idx=17 (2200) → idx=20. cumVol=11500
  ...continues until cumVol >= 35000...
  Result: vahIdx=24, valIdx=12 (say)
  VAH = 78000 + (24+0.5)*12 = 78294.0
  VAL = 78000 + (12+0.5)*12 = 78150.0

ValueAreaSignal: 78285 < 78294 AND 78285 > 78150 → "INSIDE_VA"

Nearest walls (curIdx=23):
  HVN^: nearest HVN above 23 → idx=35 (2900) → 78420.0
  HVNv: nearest HVN below 23 → idx=18 (4500=POC) → 78222.0
  LVN^: nearest LVN above 23 → idx=30 (800) → 78366.0
  LVNv: no LVN below 23 → 0 (no flat patches below)

Step 5b cap (long target = 78300.2 from prior worked output):
  rawLongTarget = 78300.2
  NearestHvnAbove = 78420.0 — NOT < 78300.2 → doesn't cap
  POC = 78222.0 — but POC < currentPrice (78285), so hvnAbove = false → POC also doesn't cap

  Result: AdjustedLongTarget = 0 (no cap; raw ATR target stands)

Display rows added:
  Value Area:  VAH:78294.0  |  VAL:78150.0  |  INSIDE_VA              ← grey/value
  HVN walls:   Above:78420.0  Below:78222.0  |  LVN: ^78366.0 v—       ← dim
```

In this example, the upgraded cap logic gives the same result as the old logic (no cap fires) but exposes structural context (78420 wall above, 78222 below, vacuum at 78366) the old display hid.

---

## 7. Files Changed Summary

| File | Change |
|---|---|
| `Core/IndicatorResults.vb` | Add 7 new properties: `VPFRVah`, `VPFRVal`, `VPFRValueAreaSignal`, `VPFRNearestHvnAbove`, `VPFRNearestHvnBelow`, `VPFRNearestLvnAbove`, `VPFRNearestLvnBelow` |
| `Core/Indicators_Structure.vb` | Extend `CalcVPFRLite` signature with 7 ByRef outputs; add VAH/VAL greedy expansion + nearest-wall scans after the POC lookup |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Optional ValueArea breakout partial (off by default); update VPFR-lite breakdown row to include VAH/VAL and HVN walls |
| `Core/ScoringEngine_Calculate_Verdict.vb` | Step 5b cap arbitration: prefer nearest-HVN-above/below over POC when present and closer |
| `Core/Settings/EngineSettings.vb` | Extend `VpfrSettings` with `ValueAreaPct`, `HvnVolPct`, `LvnVolPct`, `ValueAreaScoringEnabled` |
| `UI/MainForm_Render_Sections.vb` | Add Value Area row + HVN walls row in MARKET STRUCTURE section |
| `UI/MainForm_Analysis.vb` | Pass new ByRef args + new cfg keys into `CalcVPFRLite` call |
| `settings.json` | Extend `indicators.vpfr` block with 4 keys |

No changes to MTF gate, Pass 2c, Kelly, regime classification, OBV, Donchian.

---

## 8. Settings Keys

### `Core/Settings/EngineSettings.vb` — extend `VpfrSettings`

```vb
Public Class VpfrSettings
    <JsonPropertyName("num_buckets")>      Public Property NumBuckets      As Integer = 50
    ''' <summary>Fraction of total volume defining the value area. Default 0.70 (industry standard).</summary>
    <JsonPropertyName("value_area_pct")>   Public Property ValueAreaPct    As Double  = 0.70
    ''' <summary>Bucket volume / POC volume ratio above which a bucket is HVN. Default 0.6.</summary>
    <JsonPropertyName("hvn_vol_pct")>      Public Property HvnVolPct       As Double  = 0.6
    ''' <summary>Bucket volume / POC volume ratio below which a bucket is LVN. Default 0.2.</summary>
    <JsonPropertyName("lvn_vol_pct")>      Public Property LvnVolPct       As Double  = 0.2
    ''' <summary>
    ''' Proximity threshold for VPFRHVNearPoc / VPFRSignal classification.
    ''' Current price is considered "near POC" when |price - POC| / POC ≤ this value.
    ''' Default 0.002 (0.2%).
    ''' Currently hardcoded as a CalcVPFRLite optional default; lifting to cfg as part of v2.
    ''' </summary>
    <JsonPropertyName("hvn_proximity_pct")> Public Property HvnProximityPct As Double = 0.002
    ''' <summary>
    ''' Exponential decay base for time-weighting candle volumes when building the profile.
    ''' Each candle's volume is multiplied by decay_base ^ age (age=0 for most recent).
    ''' Default 0.985 gives ~22% weight reduction per 15 bars.
    ''' Lower values (e.g. 0.97) make POC track recent structure more aggressively;
    ''' higher (e.g. 0.995) preserve session-long structure longer.
    ''' Currently hardcoded as a CalcVPFRLite optional default; lifting to cfg as part of v2.
    ''' </summary>
    <JsonPropertyName("decay_base")>       Public Property DecayBase       As Double  = 0.985
    ''' <summary>Enable optional value-area-breakout partial in scoring pipeline. Default False — display + cap only.</summary>
    <JsonPropertyName("value_area_scoring_enabled")> Public Property ValueAreaScoringEnabled As Boolean = False
End Class
```

Note: `HvnVolPct`, `LvnVolPct`, `HvnProximityPct`, and `DecayBase` already exist as hardcoded optional params on `CalcVPFRLite`. Move all four to settings as part of this work — closes the settings-exposure gap for VPFR. The call site in `MainForm_Analysis.vb` passes them through alongside `NumBuckets`:

```vb
IndicatorEngine.CalcVPFRLite(candles1m, r.CurrentPrice,
                             vpfrPoc, vpfrHVNearPoc, vpfrSignal,
                             ' new ByRef args added in v2:
                             r.VPFRVah, r.VPFRVal, r.VPFRValueAreaSignal,
                             r.VPFRNearestHvnAbove, r.VPFRNearestHvnBelow,
                             r.VPFRNearestLvnAbove, r.VPFRNearestLvnBelow,
                             ' all-cfg pass-through:
                             numBuckets:=cfg.Indicators.VPFR.NumBuckets,
                             hvnVolPct:=cfg.Indicators.VPFR.HvnVolPct,
                             lvnVolPct:=cfg.Indicators.VPFR.LvnVolPct,
                             hvnProximityPct:=cfg.Indicators.VPFR.HvnProximityPct,
                             decayBase:=cfg.Indicators.VPFR.DecayBase,
                             valueAreaPct:=cfg.Indicators.VPFR.ValueAreaPct)
```

### `settings.json` — extend `indicators.vpfr`

```json
"vpfr": {
  "num_buckets": 50,
  "value_area_pct": 0.70,
  "hvn_vol_pct": 0.60,
  "lvn_vol_pct": 0.20,
  "hvn_proximity_pct": 0.002,
  "decay_base": 0.985,
  "value_area_scoring_enabled": false
}
```

| Key | Default | Purpose |
|---|---|---|
| `num_buckets` | 50 | Profile resolution (existing) |
| `value_area_pct` | 0.70 | Industry-standard 70% value area |
| `hvn_vol_pct` | 0.60 | Bucket vol / POC vol threshold for HVN classification |
| `lvn_vol_pct` | 0.20 | Bucket vol / POC vol threshold for LVN classification |
| `hvn_proximity_pct` | 0.002 | Price proximity threshold for VPFRHVNearPoc / VPFRSignal (0.2%) |
| `decay_base` | 0.985 | Time-decay base for volume weighting (per-bar age multiplier) |
| `value_area_scoring_enabled` | false | Flip to true after 50+ live runs to activate VA breakout partial |

---

## 9. What This Does NOT Do

- Does **not** add profile shape classification (D / P / b / bimodal) — deferred to post-WebSocket / post-calibration backlog
- Does **not** add multi-session profile (yesterday's POC, naked POCs) — needs persistent state, deferred
- Does **not** activate VA breakout scoring by default — opt-in via config flag, observe first
- Does **not** affect VWAP, OFI, CVD, MTF gate, Pass 2c, Kelly, regime classification
- Does **not** add new API calls
- Does **not** add CSV columns initially — backlog

---

## 10. Validation Plan

After 30+ live runs:

1. **VAH/VAL stability.** Are the boundaries stable across consecutive runs (changes ≤1 bucket per run during steady price), or do they whiplash? Whiplash suggests `value_area_pct` is too sensitive; raise to 0.75 if so.
2. **Nearest-HVN cap effectiveness.** How often does `VPFRNearestHvnAbove` cap a long target where POC alone wouldn't have? If never, the new logic is redundant. If frequently, the upgrade was worth it.
3. **VA breakout signal quality (when enabled).** After flipping `value_area_scoring_enabled = true`, track ABOVE_VAH and BELOW_VAL events vs subsequent 5-15 minute price action. Does breakout-with-volume confirm directionally? Calibration data needed.

Add to Section 12 backlog after implementation.

---

## 11. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should VAH/VAL use bucket centres or bucket edges? | Bucket centres — matches existing POC convention | Resolved |
| Q2 | Greedy expansion tie-breaker (higher vs lower bucket equal volume) | Lower — bias toward conservative VAL extension | Resolved |
| Q3 | What if `value_area_pct >= 1.0`? | Degenerate: VAH=priceHigh, VAL=priceLow. Allowed but pointless | Resolved |
| Q4 | What if profile is bimodal (two peaks of similar size)? | POC = global argmax; VA expansion runs from POC outward and may capture both peaks if cumulative reaches threshold. Real bimodal handling deferred to profile-shape spec (post-WebSocket) | Resolved (deferred) |
| Q5 | Should the existing `HvnVolPct=0.6` and `LvnVolPct=0.2` defaults change? | No — they're calibrated and visible already. Migration to settings is cosmetic | Resolved |
| Q6 | Activate `value_area_scoring_enabled` immediately or after observation? | After observation — ship display + cap first, scoring later. Avoids over-fitting to noisy initial data | Resolved |
| Q7 | What if no LVN exists in current range? | `VPFRNearestLvnAbove`/`VPFRNearestLvnBelow` = 0; display shows `—` | Resolved |
| Q8 | Should target cap arbitration prefer NEAREST_HVN over POC even when POC is closer? | No — prefer the **closer wall**, whichever it is. The current draft logic implements this correctly | Resolved |
