# Spec: B1 — Per-Indicator Regime Weight Tuning (Stub — Blocked on Bundle 1)
**Proposed:** 2026-05-05
**Status:** PROPOSED — BLOCKED 2026-05-05
**Target files:** `Core/Settings/EngineSettings.vb`, `Core/ScoringEngine_Calculate_Scoring.vb` (Pass 2c), `settings.json`
**Blocks on:** `analysis-script-proposal.md` produces per-indicator hit-rate output

---

## 1. Status

This spec is a **stub**. Implementation cannot be designed without empirical hit-rate data per indicator per regime, which the offline analysis script (Bundle 1) produces.

Do not implement until:

1. `csv-expansion-v0.4-proposal.md` shipped
2. ≥500 v0.4 rows accumulated, ≥3 regimes covered
3. `analysis-script-proposal.md` shipped and run; output identifies which indicators correlate strongly with subsequent price action in each regime

---

## 2. Background

Pass 2c regime alignment currently uses a single `AlignmentBonus` / `ConflictPenalty` scalar per regime. EMA, ROC, CVD slope all carry equal weight in TRENDING; VWAP, RSI, Donchian all carry equal weight in RANGE_BOUND.

The simplification was deliberate (avoiding pre-data overfitting), but it almost certainly leaves accuracy on the table. Some indicators will empirically be more predictive in certain regimes than others.

The B1 spec replaces the single scalar with per-indicator weights, calibrated from Bundle 1 output.

---

## 3. Open Design Questions (To Be Filled After Bundle 1 Output)

Cannot answer without data. Listed here as a checklist for the v2 of this spec:

1. **Granularity** — separate weight per (indicator, regime) cell? Or just per indicator with a regime-specific multiplier?
2. **Tuning source** — auto-tweaker output (frontier LLM proposes)? Hand-tuned from analysis report? Both, with auto-tweaker as iterative refinement?
3. **Cross-validation** — split CSV into train/test halves before fitting? Risk of overfitting to specific regime windows is real.
4. **Bonus aggregation** — sum of indicator weights for the dominant side, capped at `regimeMax`? Or a softer logistic blend?
5. **Backward compat** — does the v1 single-scalar logic still apply when this is disabled? (Default: yes, the new system is opt-in via settings.)

---

## 4. Constraints from Trader-Profile (Already Known)

These hold regardless of data:

- Conservative bias — bonuses tuned should keep false-positive rate at or below current baseline
- No double-counting — per-indicator weights for indicators that already feed into a pre-Pass-2c score
  must subtract a corresponding amount from that earlier contribution, OR Pass 2c becomes opt-out for
  those indicators. To be decided after data is reviewed.
- Conflict penalty must remain meaningful — over-tuning can collapse the penalty side and produce
  asymmetric "rewards-only" behaviour, which is non-conservative.

---

## 5. Acceptance

Spec re-proposed in full once Bundle 1 output is available. Until then, this file exists as the placeholder so the item doesn't get lost.
