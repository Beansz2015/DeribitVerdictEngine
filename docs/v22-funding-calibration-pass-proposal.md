# Spec: v22 Funding Calibration Pass — Regime-Aware Threshold Recalibration
**Proposed:** 2026-05-01
**Status:** IMPLEMENTED 2026-05-01
**Target files:** `settings.json`

This is a **settings-only calibration pass**. No code change. No new keys. Recalibrates funding band thresholds and the funding momentum threshold against the broader BTC-PERPETUAL funding scale observed in Deribit's 1-month / 7-day / 8-hour funding rate charts (April 2026).

The previous v19 calibration was based on a 618-row sample taken during an exceptionally quiet basis sub-window (rate stable at ~-0.0009% across the entire dataset). v19's thresholds were tuned aggressively for that micro-regime and over-fire on routine fluctuations during normal conditions. v22 widens the bands to match the actual scale of BTC funding moves across mixed regimes.

---

## 1. Problem Statement

### 1a. The empirical gap

A second 499-row calibration run completed under v19+v20+v21 still produced 100% `FundingMomentum = FLAT`. CSV inspection confirmed the funding rate was effectively stable at -0.0009% across the entire run window — not a threshold sensitivity issue but a regime issue: the engine kept polling during a quiet sub-window where no spike events occurred.

To calibrate against typical BTC funding behaviour rather than only the captured sub-window, this pass uses Deribit's published BTC-PERPETUAL funding rate charts (1m / 7d / 8h views) to infer the actual scale of funding moves.

### 1b. Inferred scale from chart data

Reading the 1-month and 7-day charts (April 2026 BTC funding):

| Magnitude class | Observed range | Engine decimal | Rough frequency |
|---|---|---|---|
| Near-zero baseline | within ±0.001% | within ±0.00001 | ~80–90% of polls |
| Mild crowding | ±0.001% to ±0.005% | ±0.00001 to ±0.00005 | a few times per session |
| Spike events | ±0.005% to ±0.010% | ±0.00005 to ±0.0001 | ~10–20 per month |
| Extreme | ±0.013% to ±0.015% | ±0.00013 to ±0.00015 | 1–2 per month |

Note: chart values are **approximate** — extracted by visual reading of pixel position relative to y-axis tick marks. Values are rounded to typical magnitudes. Calibration based on order-of-magnitude correctness, not precise pixel measurements.

The 8-hour chart additionally shows that some spike events are very brief (single-tick width), suggesting they may occur entirely between 60s polls — meaning the engine sometimes misses real funding spikes regardless of threshold tuning. v22 doesn't address this; it would require sub-minute polling or WebSocket subscription (post-WebSocket roadmap).

### 1c. v19 threshold mismatch

v19 set:

| Key | v19 value | Behavior at observed scale |
|---|---|---|
| `funding_high_positive` | 0.00003 (0.003%) | Saturates at HEAVILY CROWDED on routine ±0.003% moves |
| `funding_low_positive` | 0.000005 (0.0005%) | Fires CROWDED on basically anything off zero |
| `funding_high_negative` | -0.00003 | mirror |
| `funding_low_negative` | -0.000005 | mirror |
| `funding.momentum_threshold` | 0.000005 (0.0005%) | Fires on tick-noise during quiet periods |

During the 618-row v18 dataset, all values returned NEUTRAL because v18 had even higher thresholds. v19 over-corrected for the ultra-quiet window. v22 splits the difference and aligns with the broader regime.

### 1d. Why widen now, given the user's data still has FLAT momentum?

The user's specific 499-run window had a stable funding rate. Tightening v19's already-aggressive thresholds further wouldn't change the result — the rate didn't move. Loosening to v22's regime-appropriate thresholds means:

- **Current quiet window:** during truly quiet sub-periods, v22 will return NEUTRAL more often than v19 did. This is **correct** — quiet IS neutral. The auto-tweaker (Section 16.1) needs accurate signal, not over-firing noise.
- **Next spike event:** v22 will fire CROWDED (LOW) on rate ±0.001%-0.005%, HEAVILY CROWDED on ±0.005%-0.010%+. Provides differentiated signal across the full magnitude range.
- **Trending regime (future):** v22 saturates at HEAVILY CROWDED when funding goes ±0.01%+ — same as v19 would, but without the false intermediate signals during regime transitions.

The point of calibration data is to capture **variation**. Setting thresholds so low they fire constantly produces stuck columns just as much as setting them too high. v22 targets the middle.

---

## 2. v22 Recalibration

### 2a. Funding bands

```json
"funding_high_positive":  0.00008,    // was 0.00003 (0.003%) → 0.008% (8 bp)
"funding_low_positive":   0.00001,    // was 0.000005 (0.0005%) → 0.001% (1 bp)
"funding_high_negative": -0.00008,
"funding_low_negative":  -0.00001
```

Effect:

- **NEUTRAL** band: `|fundingRate| < 0.001%` — captures the near-zero baseline (most polls).
- **CROWDED (LOW)**: `|fundingRate| ≥ 0.001%` — fires on mild crowding (a few times per session, including during the 1m chart's typical fluctuation range).
- **HEAVILY CROWDED (HIGH)**: `|fundingRate| ≥ 0.008%` — fires on spike events (rare, captures the meaningful dips/peaks visible on the 1m chart).

Step 3 scoring impact: the funding modifier (penalty/boost) fires more selectively. v19's "always fires HEAVILY CROWDED on a 0.003% move" becomes v22's "fires CROWDED on mild bias, HEAVILY CROWDED only on real spikes." Funding's contribution to verdict becomes proportional to the actual magnitude of the bias.

### 2b. Funding momentum threshold

```json
"funding": {
  ...
  "momentum_threshold": 0.00001,     // was 0.000005 (0.0005%) → 0.001% (1 bp)
  ...
}
```

Effect: momentum classification fires `RISING` / `FALLING` when delta over the 3-sample window exceeds 1 bp. Captures meaningful spike build-up and recovery while filtering tick-noise during stable periods.

Worked example with v22 momentum on a recovery from spike:

```
History after dedup:  [0,  -0.003%, -0.008%,  -0.005%,  0]
Current poll: 0
window = 3, priorIdx = max(0, 4 - 1 - 3) = 0
delta = current - history[0] = 0 - 0 = 0
Threshold 0.00001 → FLAT (no change from oldest sample)
```

```
History after dedup:  [-0.008%, -0.005%, -0.003%, 0]
Current poll: 0
window = 3, priorIdx = max(0, 3 - 1 - 3) = 0
delta = 0 - (-0.008%) = +0.008% = 0.00008 decimal
Threshold 0.00001 → RISING (recovery from spike captured)
```

```
History after dedup:  [0]
Current poll: 0
history.Count = 1 < 2 → FLAT
```

The third case (history with 1 entry) is what the user's 499-run quiet window produced. v22 doesn't change this — when the rate genuinely doesn't move, momentum is FLAT. Correct behaviour.

### 2c. Settings.json change_log entry

```
"v22: [funding-calibration] Recalibrate funding bands + momentum threshold against broader",
"     BTC-PERPETUAL funding scale (Deribit 1m/7d/8h chart context, April 2026).",
"     v19 was tuned for an ultra-quiet 618-row sub-window where rate was stable at -0.0009%.",
"     v19's thresholds saturated at HEAVILY CROWDED on routine 3 bp moves and fired",
"     CROWDED on basically any non-zero reading. v22 widens to match the actual scale:",
"     LOW threshold ±0.001% (1 bp) — fires on mild crowding (a few per session)",
"     HIGH threshold ±0.008% (8 bp) — fires on real spike events (rare per month)",
"     momentum_threshold 0.001% (1 bp) — meaningful build-up vs micro-noise",
"     During current quiet windows, v22 returns NEUTRAL/FLAT more often (correct).",
"     During typical regimes, v22 differentiates mild vs spike crowding properly."
```

---

## 3. Files Changed Summary

| File | Change |
|---|---|
| `settings.json` | Bump version to v22. Update 5 funding-related thresholds: `funding_high_positive` / `funding_low_positive` / `funding_high_negative` / `funding_low_negative` / `funding.momentum_threshold`. Add change_log entry. |

Approximate diff: ~10 lines of settings.json. **No code change. No new keys. No tunables removed.**

---

## 4. What This Does NOT Do

- Does **not** add code logic. Pure settings adjustment.
- Does **not** address the underlying issue that 60s REST polling can miss sub-minute spike events. That's a polling-cadence / WebSocket migration concern, not a threshold one.
- Does **not** modify scoring weights, verdict thresholds, MTF gate, Kelly, regime classification, or any indicator computation.
- Does **not** change the OI threshold (already adjusted to 0.002 in v20 by user).
- Does **not** change ROC sensitivity (already adjusted in v19 / split in v21).
- Does **not** change RSI divergence algorithm (already fixed in v21).
- Does **not** add CSV columns or change schema.
- Does **not** require log reset — existing post-v21 rows remain valid; v22 just changes the classification thresholds for new rows. Optional reset for cleaner segmentation between v21 and v22 calibration data.

---

## 5. Validation Plan

After implementation:

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors. (Should be unchanged — no code touched.)
2. **Smoke test — quiet window:** if running during a low-funding period (rate stable near 0), expect:
   - `FundingBias = NEUTRAL` (rate within ±0.001%)
   - `FundingMomentum = FLAT` (no meaningful change)
   - This is **correct behaviour** for a quiet regime.
3. **Smoke test — spike window:** wait for or seek a period with funding spike activity (per the 8h chart, these happen sporadically). Expect:
   - `FundingBias` flips to `LONGS CROWDED` / `SHORTS CROWDED` when rate enters mild range
   - `FundingBias` escalates to `HEAVILY CROWDED` on spike events
   - `FundingMomentum = RISING` or `FALLING` during spike build-up and recovery
4. **Calibration distribution check:** after 100+ rows that include at least one spike event, the calibration report should show non-zero counts in `FUNDING MOMENTUM DISTRIBUTION` for RISING and FALLING (not 100% FLAT).

If after a week of run time across mixed market conditions the columns are still 100% NEUTRAL / FLAT, the issue is REST polling cadence — funding spikes are happening between polls. v22 calibration is correct; the gap is upstream. Plan WebSocket migration (Section 16.4 of project handover).

---

## 6. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should v22 also adjust `funding_high_penalty` / `funding_high_boost` / `funding_low_penalty` (the score impact magnitudes, not the trigger thresholds)? | **No.** The score-impact magnitudes are independent of the trigger thresholds. v22 changes when funding fires; it doesn't change how much funding affects scores when it does fire. Penalty magnitudes were tuned in v0.49 / v6 era and remain appropriate | Resolved |
| Q2 | Should v22 split funding thresholds for positive vs negative regimes (BTC bull markets often have higher positive funding than negative)? | **No, keep symmetric for v22.** Asymmetric thresholds would be a v23+ refinement after observing actual asymmetry in calibration data. Symmetric simplicity matches the trader-profile's contrarian intent (crowding is crowding regardless of direction) | Resolved |
| Q3 | Should the LOW threshold be ±0.0005% (between v19 and v22) for sensitivity in quiet regimes? | **No.** ±0.001% (1 bp) is the right floor — anything below ±0.001% is essentially noise on Deribit's funding stream. v19's ±0.0005% fires on micro-noise. If empirical data shows v22 misses real signal in quiet regimes, drop to ±0.0008% in v23. Don't preempt | Resolved |
| Q4 | What if BTC enters a strong bull regime where funding routinely sits at +0.05%/8h? | v22 saturates at HEAVILY CROWDED for sustained periods (correct — heavy crowding is heavy crowding). The auto-tweaker (Section 16.1, future spec) is the long-term answer to regime-shift recalibration. v22 is "appropriate for typical BTC range," not "perfect across all regimes" | Resolved |
| Q5 | Should we increase polling cadence to capture brief spikes? | **Out of scope for v22.** Cadence is in `auto_run.interval_seconds`. The user's 60s setting is already aggressive; tighter polling stresses the API resilience layer (v18). Real fix is WebSocket subscription — see Section 16.4 of project handover | Resolved |
| Q6 | Should the existing 618 + 499 rows of CSV data be reset before v22 takes effect? | **Optional.** v22 doesn't break existing rows (they keep their v19/v20 classifications). Resetting gives cleaner v22-only calibration data. User's choice | Resolved |
| Q7 | Should momentum_window be tuned alongside momentum_threshold? | **No.** `momentum_window = 3` (samples back to compare) is structural — it defines what "recent change" means. Threshold is the right knob for sensitivity. Window-tuning would be a separate v23+ concern if observed data warrants | Resolved |
| Q8 | What about asymmetric momentum thresholds (e.g., RISING fires faster than FALLING because crowding-build is the actionable signal)? | **No.** Symmetric is simpler and correct — both crowding-build (forewarning) and crowding-release (signal weakening) carry information. Step 3b in scoring already differentiates direction in how it applies the modifier | Resolved |

---

## 7. Coordination With Earlier Specs

- **v19** (calibration tuning) shipped with funding thresholds set 10x lower than v18 to address the all-NEUTRAL state at v18. v22 backs off from v19's aggressive tuning toward a regime-appropriate middle.
- **v20** (OI threshold tweak) and **v21** (RSI/ROC algorithm fixes) are independent of v22. No conflict.
- **Auto-tweaker (Section 16.1)** is the eventual home for ongoing threshold recalibration. v22 is the last manual tuning pass before that infrastructure ships.

If the auto-tweaker pipeline lands before the next manual recalibration is needed, v22's thresholds become the *starting point* for automated tuning rather than a permanent fix.

---

## 8. Migration Notes

- **No code change** — implementer runs `dotnet build` to confirm baseline is clean, then edits settings.json only.
- **Settings.json bump:** version `21` → `22`, `last_modified` updated, `modified_by` set to `funding-calibration-v22`.
- **Existing CSV log:** unaffected. Existing rows retain their v21-era classifications. New rows after the change use v22 thresholds.
- **No reset required** unless the user wants segmentation between v21 and v22 calibration data.
