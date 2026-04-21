# Proposal: Adaptive Scoring Weights by Regime
**Status:** Draft — peer review addressed (pass 1)  
**Target version:** settings v13

---

## 1. Problem Statement

`MaxScore` is regime-aware (TRENDING=19, RANGE_BOUND=18, TRANSITIONAL=15), but every indicator's vote is a fixed ±1 regardless of regime context. This means:

- In **TRENDING**: VWAP deviation gets full credit even though price can legitimately run far from VWAP for many 1m candles without signalling weakness. The signal fires "wrong" during strong momentum.
- In **RANGE_BOUND**: ROC and EMA ribbon get full credit despite being notoriously noisy in slow, choppy range conditions. A weak ROC blip earns the same score as a genuine trend burst.

The result is that the engine over-scores regime-irrelevant signals and under-differentiates high-confidence setups from marginal ones.

---

## 2. Design Constraint: DMI Circularity

**DMI/ADX is the primary regime classifier.** Giving DMI directional signals extra weight *because* we are in TRENDING would be circular — the regime was declared TRENDING precisely because DMI said so.

**EMA(200) on 5m** is the secondary regime anchor. Same circularity concern.

**Both are excluded from adaptive weighting.**

The 1m EMA ribbon (9/21/50) is a distinct calculation on a different timeframe from the regime anchor and is safe to weight.

---

## 3. Design Options

### Option A: Double-vote (±2 for selected signals in preferred regime)
Selected signals contribute ±2 instead of ±1 in their "preferred" regime. `RegimeMaxScore()` adds headroom equal to the number of boosted signals.

- Pro: Conceptually simple — signals vote harder when relevant.
- Con: `RegimeMaxScore` becomes dynamic and complex; breakdown table shows ±2 which may confuse; rounding noise if ever extended to non-integer weights.

### Option B: Pass 2c — Regime Alignment Gate (recommended)
After the existing Pass 2b (OI×CVD gate), run a new Pass 2c that checks whether the 2–3 highest-relevance signals for the active regime are mutually aligned with the directional conclusion. Award a small bonus on full alignment, a penalty on full conflict. No changes to Step 2 signal scoring.

- Pro: Structurally identical to Pass 2b — no integer model changes, transparent in breakdown table, independently tunable, easily disabled.
- Con: Does not actually change *per-indicator* weight — it adds a confirmation gate on top of existing votes.

### Option C: Float scoring with regime coefficients
Replace integer ScoreState with float accumulator; each signal has a baseline vote multiplied by a regime float coefficient before accumulation. MaxScore also floated.

- Pro: Most expressive — true per-indicator weighting.
- Con: Breaks the integer additive model; MaxScore % bands lose clean interpretability; high implementation risk; requires revalidating every threshold.

**Recommendation: Option B.** Consistent with the existing Pass 2b pattern, keeps the integer pipeline intact, and is configurable and disableable. The engine description ("static weights over-score weak signals") is addressed by the confirmation gate — regime-irrelevant signals still vote, but the regime-relevant ones must agree before a bonus fires.

---

## 4. Pass 2c — Regime Alignment Gate Design

### TRENDING regime — tracked signals

| Signal | Field | Alignment condition |
|---|---|---|
| EMA ribbon (1m) | `r.EMAAlignment` | BULL → LONG; BEAR → SHORT |
| ROC(9) | threshold-gated: `Abs(r.ROCSlope) >= cfg.Indicators.ROC.PartialThreshold` | clears threshold and positive → LONG; clears threshold and negative → SHORT; below threshold → neutral (not counted toward alignment or conflict) |
| CVD weighted slope | `r.CVDSlope` | bullish → LONG; bearish → SHORT |

If all three align with the verdict direction → **+N bonus** (cfg `TrendingAlignBonus`, default 1).  
If all three conflict with the verdict direction → **−N penalty** (cfg `TrendingConflictPenalty`, default 1).  
Partial agreement (1 or 2 of 3 aligned) → **no change**.

### RANGE_BOUND regime — tracked signals

| Signal | Field | Alignment condition |
|---|---|---|
| VWAP deviation | `r.VWAPDevPct` sign vs verdict | price above VWAP → supports LONG; below → supports SHORT |
| RSI(9) position | `r.RSI` vs 50 | RSI > 50 → LONG; RSI < 50 → SHORT |
| Donchian(20) | `r.DonchianSignal` | LONG/PARTIAL LONG → LONG; SHORT/PARTIAL SHORT → SHORT |

> **Note (Issue 1):** The original spec included VWAP σ bands here. VWAP deviation and VWAP σ bands are derived from the same base calculation and are near-always co-directional — their agreement does not constitute two independent signals. VWAP bands replaced with RSI(9) position relative to 50, which is a genuinely independent measure and a meaningful range-state indicator (RSI oscillating around 50 vs. trending away from it distinguishes range from developing momentum).

Same logic: all three aligned → **+N bonus** (cfg `RangeBoundAlignBonus`, default 1).  
All three conflicting → **−N penalty** (cfg `RangeBoundConflictPenalty`, default 1).  
Partial agreement → **no change**.

### TRANSITIONAL regime

No bonus or penalty applied. Regime is uncertain; adding a directional amplifier here would work against the conservative TRANSITIONAL handling already in Step 4.

### Zero-score suppression

If `ScoreState.NetScore = 0` at Pass 2c entry, the gate is suppressed entirely — no bonus, no penalty. A zero net score means the preceding steps have not established a directional lean; applying a regime alignment bonus or penalty in this state would produce an arbitrary directional push from an ambiguous starting point. Pass 2c only fires when the pipeline has already committed to a direction.

---

## 5. MaxScore Adjustment

The current `RegimeMaxScore()` returns a fixed ceiling that signals cannot exceed. Adding a potential +1 bonus requires the ceiling to grow by that amount so the score stays within defined bounds and the `Math.Ceiling(regimeMax × pct)` verdict thresholds remain meaningful.

| Regime | Current MaxScore | Proposed MaxScore | Bonus headroom |
|---|---|---|---|
| TRENDING | 19 | 20 | +1 (TrendingAlignBonus) |
| RANGE_BOUND | 18 | 19 | +1 (RangeBoundAlignBonus) |
| TRANSITIONAL | 15 | 15 | unchanged |

The verdict % thresholds remain identical (`verdictStrongPct`, `verdictMedPct`, `verdictWeakPct`). `Math.Ceiling(regimeMax × pct)` auto-adjusts, making the STRONG/LONG/WEAK thresholds fractionally more conservative — consistent with the conservative false-positive tolerance preference.

`RegimeMaxScore()` will accept the `cfg.Scoring.RegimeWeights` block and add bonus headroom only when `Enabled = True`.

**Bonus magnitude rationale (+1, not +2):** A +1 bonus is intentionally subtle — a "tiebreaker," not a substitute for a missing signal. At `verdictStrongPct ≈ 0.63`, the TRENDING STRONG threshold shifts from `Ceil(19 × 0.63) = 12` to `Ceil(20 × 0.63) = 13`. Full regime alignment adds genuine weight but does not allow a setup that is one signal short to reach STRONG. A +2 bonus would risk amplifying marginal setups before calibration data confirms the gate is reliable. Magnitude is configurable (`alignment_bonus` in cfg); review after 50+ live runs if alignment proves consistently informative.

---

## 6. settings.json Additions

New top-level block `regime_weights` (settings bump to **v13**):

```json
"regime_weights": {
  "enabled": true,
  "trending": {
    "alignment_bonus": 1,
    "conflict_penalty": 1
  },
  "range_bound": {
    "alignment_bonus": 1,
    "conflict_penalty": 1
  }
}
```

Signal selection for each regime is hardcoded to the groups above (EMA ribbon + ROC + CVD for TRENDING; VWAP dev + RSI(9) + Donchian for RANGE_BOUND). These are structural choices, not runtime config — making them config would add complexity with no calibration benefit at this stage.

---

## 7. Pipeline Changes

| File | Change |
|---|---|
| `Core/Settings/EngineSettings.vb` | Add `RegimeWeightSettings` POCO with `Enabled`, `Trending.*`, `RangeBound.*` |
| `Core/ScoringEngine_Helpers.vb` | `RegimeMaxScore(regime, cfg)` — adds bonus headroom when `cfg.RegimeWeights.Enabled` |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Add `RunRegimeAlignmentPass(r, ss, regime, cfg)` called after Pass 2b |
| `settings.json` | Add `regime_weights` block; bump version to v13 |
| `UI/MainForm_Render_Sections.vb` | Signal breakdown table: show Pass 2c row if bonus/penalty fired |

### Step placement in pipeline

```
Step 2:   Signal scoring (unchanged)
Pass 2:   Partial upgrade on cross-category confirmation (unchanged)
Pass 2b:  OI × CVD cross-confirm (unchanged)
Pass 2c:  [NEW] Regime alignment gate
Step 3:   Baseline funding modifier (unchanged)
...
```

### Breakdown display row

The display row reflects which signals were actually active. ROC is neutral (not counted) when `Abs(r.ROCSlope) < cfg.Indicators.ROC.PartialThreshold`; the label omits ROC and notes its neutrality.

| Condition | Display |
|---|---|
| All three active and aligned | `REGIME ALIGN  [TRENDING: EMA+ROC+CVD ✓]   +1` |
| ROC neutral, EMA+CVD aligned | `REGIME ALIGN  [TRENDING: EMA+CVD ✓ (ROC neutral)]   +1` |
| All three active and conflicting | `REGIME CONFLICT [TRENDING: EMA+ROC+CVD ✗]  -1` |
| ROC neutral, EMA+CVD conflicting | `REGIME CONFLICT [TRENDING: EMA+CVD ✗ (ROC neutral)]  -1` |

Row suppressed when partial agreement, zero net score entry, TRANSITIONAL regime, or feature disabled.

---

## 8. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| DMI circularity | DMI and 5m EMA(200) explicitly excluded from weighting groups |
| Score inflation | MaxScore bumped by exact bonus headroom; thresholds auto-adjust upward |
| Signal correlation in TRENDING | EMA ribbon, ROC, CVD are correlated in strong trends — conservative all-three-align gate means partial agreement is neutral and the bonus only fires on genuine convergence |
| VWAP signal correlation (RANGE_BOUND) | VWAP deviation and VWAP σ bands share the same baseline calculation and are near-always co-directional. VWAP bands replaced with RSI(9) vs 50 — a genuinely independent measure. |
| Noisy behaviour in TRANSITIONAL | TRANSITIONAL is explicitly excluded from bonus/penalty |
| Negative score accumulation | `ScoreState` has no floor guard — a Pass 2c −1 penalty on an already-low score will accumulate to a negative total. This is safe: all verdict comparisons use `NetScore >= threshold` and negative values produce NO TRADE without special handling. Pass 2b uses the same penalty pattern with no issues. Known-safe assumption. |
| Calibration unknowns | `enabled` flag allows full feature disable; bonus/penalty values are tunable via cfg |

---

## 9. Out of Scope

- Float-based per-indicator weights (incompatible with integer additive model)
- Auto-tuning of regime weights from CSV log (separate feature, requires calibration data first)
- DMI weight amplification (circular — see Section 2)
- VPFR or Liquidations in the regime alignment groups (these are structural veto/cap signals, not directional amplifiers)
