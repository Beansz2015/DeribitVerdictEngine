# Spec: Settings Exposure Pass — Lift Hardcoded Scoring Constants to settings.json
**Proposed:** 2026-04-27
**Status:** PROPOSED — pending user approval
**Target files:** `Core/ScoringEngine_Helpers.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/Indicators_Volatility.vb`, `Core/Indicators_OrderFlow.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Analysis.vb`, `settings.json`

This is a **mechanical exposure pass**. No new behaviour. Every literal scoring-affecting magic number currently embedded in the engine becomes a `settings.json` key with the **current value preserved as default**. After this pass, an external tweaker (frontier-LLM auto-tuner per `DeribitIndicatorProject.md` Section 16) can adjust any scoring-relevant parameter without code changes.

The audit driving this spec is documented in the conversation that produced it (2026-04-27 review). 19 items identified across 4 categories.

VPFR-specific items (`hvn_proximity_pct`, `decay_base`) are folded into `vpfr-lite-v2-proposal.md` because they're CalcVPFRLite parameters — same domain. This spec covers the rest.

---

## 1. Problem Statement

`settings.json` exposes most scoring tunables. But not all. A non-trivial set of scoring-affecting literals live as hardcoded constants in:

- `ScoringEngine_Helpers.RegimeMaxScore` — base score ceilings per regime
- `ScoringEngine_Helpers.TierFloor` — TRANSITIONAL ADX penalty floor breakpoints
- `ScoringEngine_Calculate_Scoring.CalcVerdictContext` — MOMENTUM_FADING and STRUCTURALLY_WEAK classification thresholds
- `ScoringEngine_Calculate_Scoring.RunScoringPipeline` Pass 2c — RSI midline (50) for RANGE_BOUND alignment
- `Indicators_Volatility.CalcBBW` — squeeze percentile threshold and series window multiplier
- `Indicators_Volatility.CalcTTMSqueeze` — internal SMA + linear regression periods
- `Indicators_OrderFlow.CalcCVD` — late/early segment slope weights
- `MainForm_Analysis` Donchian quartile pct

These cannot be tuned without recompiling. For an external auto-tweaking pipeline (Section 16), every scoring decision boundary must be reachable through `settings.json`.

This pass moves **19 literals** to `settings.json` with current values preserved as defaults. Zero behavioural change. Zero risk to existing calibration. After this pass, `settings.json` is the complete tuning surface for scoring.

---

## 2. Items to Expose

### 2a. RegimeMaxScore Base Values

**Current code (`ScoringEngine_Helpers.vb`):**
```vb
Public Shared Function RegimeMaxScore(regime As String, cfg As EngineSettings) As Integer
    Dim baseMax As Integer
    Select Case regime
        Case "TRENDING_UP", "TRENDING_DOWN" : baseMax = 19
        Case "RANGE_BOUND"                  : baseMax = 18
        Case Else                           : baseMax = 15
    End Select
    ' ... regime weights bonus ...
End Function
```

**After:**
```vb
Select Case regime
    Case "TRENDING_UP", "TRENDING_DOWN" : baseMax = cfg.Scoring.RegimeMaxScore.Trending
    Case "RANGE_BOUND"                  : baseMax = cfg.Scoring.RegimeMaxScore.RangeBound
    Case Else                           : baseMax = cfg.Scoring.RegimeMaxScore.Transitional
End Select
```

**Rationale.** These ceilings determine what "HIGH confidence" means in each regime. Tuning them lets the auto-tweaker shift the absolute difficulty of triggering STRONG verdicts independently of `verdict_strong_pct`. For example: lowering TRENDING from 19 → 18 makes STRONG harder without changing the percentage; raising RANGE_BOUND from 18 → 20 makes the gate looser in chop conditions.

**Caveat.** The base values are loosely derived from the count of scored signal slots. Setting them dramatically below that count (e.g., TRENDING = 12) will saturate the score before the verdict thresholds proportionally adjust. The auto-tweaker should bound adjustments to ±3 from current defaults to stay sensible.

### 2b. TierFloor Breakpoints

**Current code (`ScoringEngine_Helpers.vb`):**
```vb
Private Shared Function TierFloor(rawScore As Integer) As Integer
    If rawScore >= 12 Then Return 9
    If rawScore >= 9 Then Return 6
    If rawScore >= 6 Then Return 3
    Return 0
End Function
```

**After:**
```vb
Private Shared Function TierFloor(rawScore As Integer, cfg As EngineSettings) As Integer
    Dim tf = cfg.Scoring.TierFloor
    If rawScore >= tf.HighThreshold Then Return tf.HighFloor
    If rawScore >= tf.MedThreshold  Then Return tf.MedFloor
    If rawScore >= tf.LowThreshold  Then Return tf.LowFloor
    Return 0
End Function
```

Caller sites in `_Verdict.vb` Step 4 must pass `cfg`:
```vb
effectiveLS = Math.Max(ls - adxPenalty, TierFloor(ls, cfg))
effectiveSS = Math.Max(ss - adxPenalty, TierFloor(ss, cfg))
```

**Rationale.** This implements the graceful-degradation cap on TRANSITIONAL ADX penalties. A score of 12+ can never drop below 9 from the penalty; 9-11 can never drop below 6; etc. The breakpoints define how aggressively the penalty can cap. The auto-tweaker may want to relax (lower floors) or tighten (raise floors) this graceful degradation behaviour.

### 2c. VerdictContext Thresholds

**Current code (`ScoringEngine_Calculate_Scoring.vb` `CalcVerdictContext`):**
```vb
If r.MicroCVDEarly > 0 AndAlso r.MicroCVDLate < r.MicroCVDEarly * 0.5 Then fadingCount += 1
If r.MicroCVDEarly < 0 AndAlso r.MicroCVDLate > r.MicroCVDEarly * 0.5 Then fadingCount += 1
' ...
If fadingCount >= 2 Then Return "MOMENTUM_FADING"
' ...
If structScore < 2 AndAlso flowScore < 2 Then
    Return "STRUCTURALLY_WEAK"
End If
```

**After:**
```vb
Dim ctx = cfg.Scoring.ContextTag
If r.MicroCVDEarly > 0 AndAlso r.MicroCVDLate < r.MicroCVDEarly * ctx.MomentumFadingDecayRatio Then fadingCount += 1
If r.MicroCVDEarly < 0 AndAlso r.MicroCVDLate > r.MicroCVDEarly * ctx.MomentumFadingDecayRatio Then fadingCount += 1
' ...
If fadingCount >= ctx.MomentumFadingCountMin Then Return "MOMENTUM_FADING"
' ...
If structScore < ctx.StructurallyWeakStructMin AndAlso flowScore < ctx.StructurallyWeakFlowMin Then
    Return "STRUCTURALLY_WEAK"
End If
```

**Important — distinct from existing keys.** The existing `ContextTagStructuralMin` (3) and `ContextTagFlowMax` (1) gate **FLOW_UNCONFIRMED**. The new `StructurallyWeakStructMin` (2) and `StructurallyWeakFlowMin` (2) gate **STRUCTURALLY_WEAK**. They serve different classifications and should remain separate.

### 2d. Pass 2c RANGE_BOUND RSI Midline

**Current code (`ScoringEngine_Calculate_Scoring.vb` Pass 2c):**
```vb
Dim rsiAligned As Boolean = If(p2cIsLong, r.RSI > 50, r.RSI < 50)
```

**After:**
```vb
Dim rsiAligned As Boolean = If(p2cIsLong,
                               r.RSI > cfg.Indicators.RSI.Pass2cMidline,
                               r.RSI < cfg.Indicators.RSI.Pass2cMidline)
```

**Rationale.** 50 is the natural RSI midline, but the auto-tweaker may want to bias the alignment check (e.g., 48 to bias slightly bullish). Marginal flexibility, low risk.

### 2e. Donchian Quartile Threshold

**Current code (`UI/MainForm_Analysis.vb`):**
```vb
Dim q1 As Double = r.DonchianLower + channelRange * 0.25
Dim q3 As Double = r.DonchianUpper - channelRange * 0.25
```

**After:**
```vb
Dim qPct As Double = cfg.Indicators.Donchian.QuartilePct
Dim q1 As Double = r.DonchianLower + channelRange * qPct
Dim q3 As Double = r.DonchianUpper - channelRange * qPct
```

**Rationale.** Controls when `LONG_PARTIAL` and `SHORT_PARTIAL` fire (top/bottom quartile of channel). Tunable: tighter (e.g., 0.15) requires deeper penetration before signalling; looser (0.35) fires earlier.

### 2f. CalcBBW Internals

**Current code (`Indicators_Volatility.vb`):**
```vb
Dim windowSize As Integer = Math.Min(candles.Count, period * 5)
' ... bbw series build ...
Dim pctIdx As Integer = CInt(Math.Floor(sorted.Count * 0.20))
```

**After:** Add two settings keys plus pass them through:
```vb
Public Shared Sub CalcBBW(candles As List(Of Candle), period As Integer, stdMult As Double,
                           ByRef bbw As Double, ByRef squeezeStatus As String,
                           Optional seriesWindowMultiplier As Integer = 5,
                           Optional squeezePercentile As Double = 0.20)
    ' ...
    Dim windowSize As Integer = Math.Min(candles.Count, period * seriesWindowMultiplier)
    ' ...
    Dim pctIdx As Integer = CInt(Math.Floor(sorted.Count * squeezePercentile))
    ' ...
End Sub
```

Call site in `MainForm_Analysis.vb`:
```vb
IndicatorEngine.CalcBBW(candles1m,
                        cfg.Indicators.BBW.Period,
                        cfg.Indicators.BBW.StdDev,
                        r.BBW, r.SqueezeStatus,
                        seriesWindowMultiplier:=cfg.Indicators.BBW.SeriesWindowMultiplier,
                        squeezePercentile:=cfg.Indicators.BBW.SqueezePercentile)
```

**Rationale.** `squeezePercentile` directly controls when `ACTIVE` fires (current default = bottom 20% of recent BBW values). Auto-tweaker may want this tighter (0.15) or looser (0.25). `seriesWindowMultiplier` controls how much history feeds the percentile — larger window = more stable threshold but slower to adapt.

### 2g. CalcTTMSqueeze Internals

**Current code:**
```vb
Public Shared Sub CalcTTMSqueeze(candles, ..., 
                                  Optional smaPeriod As Integer = 20,
                                  Optional linRegPeriod As Integer = 7,
                                  Optional flatThreshold As Double = 0.5)
```

**After:** Already has the optional params; just wire through cfg from call site:
```vb
IndicatorEngine.CalcTTMSqueeze(candles1m, r.TTMHistogram, r.TTMDirection, r.TTMSignal,
                                smaPeriod:=cfg.Indicators.TTM.SmaPeriod,
                                linRegPeriod:=cfg.Indicators.TTM.LinRegPeriod,
                                flatThreshold:=cfg.Indicators.TTM.FlatThreshold)
```

Add `SmaPeriod` and `LinRegPeriod` to `TtmSettings` (currently has only `FlatThreshold`).

**Rationale.** `smaPeriod` (20) is the SMA used for histogram delta computation. `linRegPeriod` (7) is the linear-regression window for fitting the histogram. Both control the smoothness vs responsiveness trade-off in TTM signal generation.

### 2h. CalcCVD Segment Weights

**Current code (`Indicators_OrderFlow.vb`):**
```vb
Dim weightedSlope As Double = lateDelta * 2.0 - earlyDelta * 1.0
```

**After:** Add optional params + cfg pass-through:
```vb
Public Shared Sub CalcCVD(trades, candles, ByRef cvdValue, ByRef cvdSlope, ByRef cvdDivergence,
                           Optional slopeMinUsd As Double = 50000,
                           Optional slopePctOfValue As Double = 0.05,
                           Optional divergencePriceGate As Double = 0.002,
                           Optional lateSegmentWeight As Double = 2.0,
                           Optional earlySegmentWeight As Double = 1.0)
    ' ...
    Dim weightedSlope As Double = lateDelta * lateSegmentWeight - earlyDelta * earlySegmentWeight
    ' ...
End Sub
```

Call site:
```vb
IndicatorEngine.CalcCVD(recentTrades, candles1m, r.CVDValue, r.CVDSlope, r.CVDDivergence,
                        slopeMinUsd:=cfg.Indicators.CVD.SlopeMinUsd,
                        slopePctOfValue:=cfg.Indicators.CVD.SlopePctOfValue,
                        divergencePriceGate:=cfg.Indicators.CVD.DivergencePriceGate,
                        lateSegmentWeight:=cfg.Indicators.CVD.LateSegmentWeight,
                        earlySegmentWeight:=cfg.Indicators.CVD.EarlySegmentWeight)
```

**Rationale.** Trader-profile mentions the CVD 3-segment weighting as a deliberate choice ("late × 2 vs early × 1"). The ratio is the design knob. Tuning it shifts how much the slope reacts to late-segment behaviour vs the full window. Auto-tweaker may explore 1.5/1.0 or 3.0/1.0 ratios.

---

## 3. New Settings Classes & Keys

### `Core/Settings/EngineSettings.vb` — additions

#### Add to `ScoringSettings`:

```vb
''' <summary>Per-regime score ceilings. Auto-tweaker should keep within ±3 of defaults.</summary>
<JsonPropertyName("regime_max_score")> Public Property RegimeMaxScore As New RegimeMaxScoreSettings

''' <summary>TRANSITIONAL ADX penalty graceful-degradation floor breakpoints.</summary>
<JsonPropertyName("tier_floor")>       Public Property TierFloor      As New TierFloorSettings

''' <summary>VerdictContext Step 5b classifier thresholds.</summary>
<JsonPropertyName("context_tag_thresholds")> Public Property ContextTag As New ContextTagThresholds
```

#### New classes:

```vb
''' <summary>Per-regime score ceiling base values (before regime_weights bonus).</summary>
Public Class RegimeMaxScoreSettings
    <JsonPropertyName("trending")>     Public Property Trending     As Integer = 19
    <JsonPropertyName("range_bound")>  Public Property RangeBound   As Integer = 18
    <JsonPropertyName("transitional")> Public Property Transitional As Integer = 15
End Class

''' <summary>
''' Graceful-degradation floor for TRANSITIONAL ADX penalty.
''' If raw score >= HighThreshold, post-penalty floor is HighFloor (cannot drop below).
''' Same pattern at Med and Low. Below LowThreshold, no floor (can drop to 0).
''' </summary>
Public Class TierFloorSettings
    <JsonPropertyName("high_threshold")> Public Property HighThreshold As Integer = 12
    <JsonPropertyName("high_floor")>     Public Property HighFloor     As Integer = 9
    <JsonPropertyName("med_threshold")>  Public Property MedThreshold  As Integer = 9
    <JsonPropertyName("med_floor")>      Public Property MedFloor      As Integer = 6
    <JsonPropertyName("low_threshold")>  Public Property LowThreshold  As Integer = 6
    <JsonPropertyName("low_floor")>      Public Property LowFloor      As Integer = 3
End Class

''' <summary>
''' VerdictContext Step 5b classifier thresholds.
''' Distinct from ContextTagStructuralMin / ContextTagFlowMax which gate FLOW_UNCONFIRMED.
''' These gate MOMENTUM_FADING and STRUCTURALLY_WEAK.
''' </summary>
Public Class ContextTagThresholds
    ''' <summary>Late vs early MicroCVD ratio threshold for fading detection. Default 0.5.</summary>
    <JsonPropertyName("momentum_fading_decay_ratio")> Public Property MomentumFadingDecayRatio As Double  = 0.5
    ''' <summary>Min count of fading signals to classify MOMENTUM_FADING. Default 2.</summary>
    <JsonPropertyName("momentum_fading_count_min")>   Public Property MomentumFadingCountMin   As Integer = 2
    ''' <summary>Structural hits below this count + flow below StructurallyWeakFlowMin → STRUCTURALLY_WEAK. Default 2.</summary>
    <JsonPropertyName("structurally_weak_struct_min")> Public Property StructurallyWeakStructMin As Integer = 2
    ''' <summary>Flow hits below this count + structural below StructurallyWeakStructMin → STRUCTURALLY_WEAK. Default 2.</summary>
    <JsonPropertyName("structurally_weak_flow_min")>   Public Property StructurallyWeakFlowMin   As Integer = 2
End Class
```

#### Add to `RsiSettings`:

```vb
''' <summary>RSI midline for Pass 2c RANGE_BOUND alignment check. Default 50.</summary>
<JsonPropertyName("pass2c_midline")> Public Property Pass2cMidline As Double = 50.0
```

#### Add to `DonchianSettings`:

```vb
''' <summary>Quartile threshold for LONG_PARTIAL / SHORT_PARTIAL (fraction of channel range). Default 0.25.</summary>
<JsonPropertyName("quartile_pct")> Public Property QuartilePct As Double = 0.25
```

#### Add to `BbwSettings`:

```vb
''' <summary>BBW series window = period × multiplier. Default 5 (period 20 × 5 = 100 bars of history).</summary>
<JsonPropertyName("series_window_multiplier")> Public Property SeriesWindowMultiplier As Integer = 5
''' <summary>Percentile of BBW series below which SqueezeStatus = ACTIVE. Default 0.20 (bottom 20%).</summary>
<JsonPropertyName("squeeze_percentile")>       Public Property SqueezePercentile      As Double  = 0.20
```

#### Add to `TtmSettings`:

```vb
''' <summary>SMA period for TTM histogram delta computation. Default 20.</summary>
<JsonPropertyName("sma_period")>     Public Property SmaPeriod     As Integer = 20
''' <summary>Linear regression period for histogram fit. Default 7.</summary>
<JsonPropertyName("lin_reg_period")> Public Property LinRegPeriod  As Integer = 7
' (FlatThreshold already present)
```

#### Add to `CvdSettings`:

```vb
''' <summary>Late-segment weight in 3-segment CVD slope formula (lateDelta × this − earlyDelta × early_segment_weight). Default 2.0.</summary>
<JsonPropertyName("late_segment_weight")>  Public Property LateSegmentWeight  As Double = 2.0
''' <summary>Early-segment weight in 3-segment CVD slope formula. Default 1.0.</summary>
<JsonPropertyName("early_segment_weight")> Public Property EarlySegmentWeight As Double = 1.0
```

---

## 4. settings.json Additions

Bump `version` to v16. Add a change_log entry.

```json
"indicators": {
  "RSI":      { ..., "pass2c_midline": 50.0 },
  "BBW":      { ..., "series_window_multiplier": 5, "squeeze_percentile": 0.20 },
  "Donchian": { "period": 20, "quartile_pct": 0.25 },
  "CVD":      { ..., "late_segment_weight": 2.0, "early_segment_weight": 1.0 },
  "TTM":      { "flat_threshold": 0.5, "sma_period": 20, "lin_reg_period": 7 }
},
"scoring": {
  ...
  "regime_max_score": {
    "trending": 19,
    "range_bound": 18,
    "transitional": 15
  },
  "tier_floor": {
    "high_threshold": 12, "high_floor": 9,
    "med_threshold":  9,  "med_floor":  6,
    "low_threshold":  6,  "low_floor":  3
  },
  "context_tag_thresholds": {
    "momentum_fading_decay_ratio": 0.5,
    "momentum_fading_count_min": 2,
    "structurally_weak_struct_min": 2,
    "structurally_weak_flow_min": 2
  }
}
```

---

## 5. Files Changed Summary

| File | Change |
|---|---|
| `Core/Settings/EngineSettings.vb` | Add 3 new classes (`RegimeMaxScoreSettings`, `TierFloorSettings`, `ContextTagThresholds`); extend `RsiSettings`, `DonchianSettings`, `BbwSettings`, `TtmSettings`, `CvdSettings`, `ScoringSettings` |
| `Core/ScoringEngine_Helpers.vb` | `RegimeMaxScore` reads from `cfg.Scoring.RegimeMaxScore`; `TierFloor` takes `cfg` param and reads from `cfg.Scoring.TierFloor` |
| `Core/ScoringEngine_Calculate_Verdict.vb` | Pass `cfg` to `TierFloor()` calls in Step 4 |
| `Core/ScoringEngine_Calculate_Scoring.vb` | `CalcVerdictContext` reads from `cfg.Scoring.ContextTag`; Pass 2c reads `cfg.Indicators.RSI.Pass2cMidline` |
| `Core/Indicators_Volatility.vb` | `CalcBBW` accepts `seriesWindowMultiplier` + `squeezePercentile` optional params; thread through |
| `Core/Indicators_OrderFlow.vb` | `CalcCVD` accepts `lateSegmentWeight` + `earlySegmentWeight` optional params; thread through |
| `UI/MainForm_Analysis.vb` | Wire all new cfg keys through call sites: `CalcBBW`, `CalcCVD`, `CalcTTMSqueeze`, Donchian quartile pct |
| `settings.json` | Bump v15 → v16. Add new keys per Section 4 |

---

## 6. Worked Example — RegimeMaxScore Tuning

Suppose the auto-tweaker observes that STRONG verdicts in TRENDING regime are firing too rarely (bias the system toward more cautious classification). It can:

**Option A — Lower regime_max_score.trending from 19 to 18:**
- New strong threshold = ceil(18 × 0.70) = 13 (was 14 with 19)
- Score of 13 now triggers STRONG; previously needed 14
- Effect: easier to reach STRONG. Looser.

**Option B — Raise verdict_strong_pct from 0.70 to 0.75:**
- New strong threshold = ceil(19 × 0.75) = 15 (was 14)
- Score of 14 no longer triggers STRONG
- Effect: harder to reach STRONG. Tighter.

These are independent levers. Pre-exposure-pass, only Option B was reachable. Post-pass, Option A becomes available — and the two can be combined for fine-grained control.

---

## 7. What This Does NOT Do

- Does **not** change any current behaviour at default values
- Does **not** add new indicators or signals
- Does **not** change scoring logic — only lifts literals to cfg
- Does **not** touch display or MTF gate
- Does **not** affect Kelly, VPFR, swing pivots, OFI momentum, or bid-ask spread (those have their own specs)

---

## 8. Validation Plan

After implementation:

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors.
2. **Default-equivalence test:** Run analysis with the new `settings.json` v16 (current values preserved). Compare verdict + breakdown to the v15 output on the same market state. Must match exactly.
3. **Tuning sanity test:** Adjust one new key (e.g., `regime_max_score.trending` from 19 to 17). Verify the verdict threshold and any score caps shift accordingly. Revert to default.

---

## 9. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should `RegimeMaxScore` clamp to a sane range (e.g., ±3 from defaults)? | **No clamp in code** — the auto-tweaker should respect the bound noted in Section 2a. Adding a clamp adds complexity for marginal safety | Resolved |
| Q2 | Should `TierFloor` be derived from `RegimeMaxScore` instead of independent? | **No** — they serve different purposes (regime ceiling vs penalty graceful degradation). Keep independent | Resolved |
| Q3 | Should the auto-tweaker have access to verdict_*_pct AND regime_max_score (overlapping levers)? | **Yes** — orthogonal fine-tuning. The tweaker is responsible for not over-tuning | Resolved |
| Q4 | Should `momentum_fading_decay_ratio` be split into positive-side and negative-side ratios? | **No** — symmetry simplifies the tuner. Asymmetric tuning is a v2 spec | Resolved |
| Q5 | Should we expose the OI direction epsilon (`0.9999`)? | **No** — too marginal; risk of breaking `priceUp` boolean classifier with wide tweaks. Skipped from this pass | Resolved |
| Q6 | Should we expose 5m EMA200 period (200)? | **No** — trader-profile specifies 200 EMA on 5m. Structural choice, not tunable | Resolved |
| Q7 | Should we expose `MTF_TTL_SECONDS` (60s)? | **No** — infrastructure cache, not scoring | Resolved |
| Q8 | Should we expose `WinFormsAutoRunTimer.MIN_INTERVAL_MS` (10s)? | **No** — UX safety floor, not scoring | Resolved |

---

## 10. Coordination With Other Specs

- **`vpfr-lite-v2-proposal.md`** independently exposes `hvn_proximity_pct`, `decay_base`, `hvn_vol_pct`, `lvn_vol_pct` (CalcVPFRLite parameters). Implement either spec in either order; no conflict.
- **`bid-ask-spread-proposal.md`** adds `spread.wide_threshold_bps` etc. Independent.
- **`ofi-momentum-proposal.md`** adds OFI momentum keys. Independent.
- **`swing-pivot-proposal.md`** adds `indicators.swing` keys. Independent.

If implemented after the four indicator specs, this exposure pass is the closing audit before opening the auto-tweaking pipeline.
