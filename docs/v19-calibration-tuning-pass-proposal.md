# Spec: v19 Calibration Tuning Pass — Empirical Threshold Recalibration + Liq Window
**Proposed:** 2026-04-30
**Status:** APPROVED 2026-04-30
**Target files:** `settings.json`, `UI/MainForm_Analysis.vb`

This is a **calibration adjustment pass** driven by 618 rows of live analysis data accumulated under v18. Five thresholds (settings-only) are recalibrated against observed market scale. One small code change widens the liquidation trade-detection window from 100 to 500 trades.

No new indicators. No new scoring logic. No CSV schema change. The behavioural change is that previously-dormant classifiers (`FundingBias`, `OISignal`, `OiCvdOutcome`, `FundingMomentum`, `ROCSlope`) start firing on the actual scale of observed BTC-PERPETUAL movements. Calibration data becomes informative rather than degenerate.

---

## 1. Problem Statement

A 618-row CalibrationReport accumulated across two sessions (~10 hours, both UTC overnight quiet periods) revealed that five categorical CSV columns are stuck on a single value across the entire dataset, plus one column (`ROCSlope`) at 94% one value. The columns affected:

| Column | Distinct values | Top value share | Cause |
|---|---|---|---|
| `FundingBias` | 1 | 100% NEUTRAL | Funding band thresholds calibrated for 10× larger moves |
| `FundingMomentum` | 1 | 100% FLAT | Momentum threshold (10 bp) vs observed range (0.9 bp) |
| `OISignal` | 1 | 100% NEUTRAL | OI change threshold (1%) vs observed peak (0.61%) |
| `OiCvdOutcome` | 1 | 100% NONE | Cascades from OISignal=NEUTRAL |
| `LiqLongSize` / `LiqShortSize` / `LiqSignal` | 1 | 100% zero/NONE | 100-trade fetch window often misses sparse liquidations |
| `ROCSlope` | 3 | 94% FLAT | Slope sensitivity (0.1%) vs typical 1m bar-to-bar ROC delta |

Five of the six are **pure threshold mismatches** between settings.json defaults and the actual scale of BTC-PERPETUAL movements during quiet basis regimes. The sixth (liquidations) is a window-size issue.

**Why now, mid-calibration?** Continuing to accumulate rows with these columns degenerate produces no signal for the auto-tweaker (Section 16.1). 300+ rows of `FundingMomentum=FLAT` is not informative data. Better to recalibrate early and reset the log so all subsequent rows are usable.

**Reasoning for thresholds is empirical, not estimated.** v14 set the funding/OI thresholds based on assumptions about BTC funding scale that the live data has now contradicted. v19 sets them against the actual observed distribution.

**Out of scope:**

- Algorithmic changes to indicators (e.g., RSI divergence over-firing at 80% non-NONE — separate spec required, see Section 9).
- Scoring logic changes.
- Display changes.
- New cfg keys.

---

## 2. Funding Band Thresholds (FundingBias)

### 2a. Observed scale

Across 618 rows:
- Funding rate distinct values: **8 total** (`-0.000009` to `0.000000`)
- Range: **0.9 basis points** (0.000009 absolute)
- Distribution skews negative (slight short-funding regime)

### 2b. v18 thresholds

```json
"funding_high_positive":  0.0003,   // 3 bp
"funding_low_positive":   0.00005,  // 0.5 bp
"funding_high_negative": -0.0003,
"funding_low_negative":  -0.00005
```

The "low" threshold of ±0.5 bp is **5× higher** than the observed peak of 0.09 bp. NEUTRAL band swallows everything.

### 2c. v19 recalibration

```json
"funding_high_positive":  0.00003,   // 0.3 bp (was 3 bp)
"funding_low_positive":   0.000005,  // 0.05 bp (was 0.5 bp)
"funding_high_negative": -0.00003,
"funding_low_negative":  -0.000005
```

Effect: `FundingBias` will fire `LONGS CROWDED` / `SHORTS CROWDED` when the rate exceeds ±0.05 bp (instead of ±0.5 bp), and `LONGS HEAVILY CROWDED` / `SHORTS HEAVILY CROWDED` at ±0.3 bp (instead of ±3 bp). Matches the actual observed scale.

**Step 3 scoring impact.** Step 3 funding modifier in `RunScoringPipeline` uses these thresholds to apply penalties/boosts. After v19, the modifier fires more frequently, so funding will affect verdict scores more often than under v18. This is the intended effect — v18 funding was effectively dormant.

**Caveat: regime sensitivity.** The current quiet basis regime won't last forever. When BTC enters a trending basis (funding routinely 5+ bp), v19 thresholds may fire too often. Re-tuning will be needed at that point. Consider this a calibration to *current conditions*, not a permanent fix. Auto-tweaker (Section 16.1) is the long-term answer.

---

## 3. Funding Momentum Threshold

### 3a. Observed scale

Maximum single-step delta in `_fundingHistory` (after dedup): **0.000009** (0.9 bp). The momentum threshold compares this delta against `momentum_threshold = 0.0001` (10 bp). Always FLAT.

### 3b. v19 recalibration

```json
"funding": {
  ...
  "momentum_threshold": 0.000005,   // was 0.0001 — 20x lower, matches observed scale
  ...
}
```

Effect: `FundingMomentum` will fire `RISING` / `FALLING` when delta exceeds 0.5 bp over the 3-sample window. Captures the genuine variations observed in the data while staying above tick-noise floor.

**Step 3b scoring impact.** Step 3b modifier fires only when `FundingMomentum != "FLAT"` AND `FundingBias` is non-NEUTRAL or trending toward crowding. Since both Section 2 and Section 3 changes work together, Step 3b will engage more often.

---

## 4. OI Change Threshold

### 4a. Observed scale

`OIChange15m` across 618 rows:
- Range: **-0.6139% to +0.2660%**
- Peak |delta|: **0.61%**
- All 618 rows: `OISignal = NEUTRAL`

The threshold to classify NEW LONGS/SHORTS/COVERING/CAPITULATION is `change_threshold_pct = 0.01` (1.0%). Never crossed.

### 4b. v19 recalibration

```json
"OI": {
  "neutral_band_pct": 0.05,
  "change_threshold_pct": 0.003   // was 0.01 — 3.3x lower
}
```

Effect: OI direction signals fire when 15m OI change exceeds ±0.3% (instead of ±1.0%). Captures genuine BTC OI movements during the calibration window's regime.

**Pass 2b cascade.** Once OI signals fire non-NEUTRAL, Pass 2b can trigger CONFIRMED/CONFLICT outcomes. `OiCvdOutcome` column starts populating across the spectrum instead of stuck at NONE.

---

## 5. ROC Slope Sensitivity

### 5a. Observed distribution

`ROCSlope` across 618 rows:
- 582 FLAT (94.2%)
- 20 RISING (3.2%)
- 16 FALLING (2.6%)

### 5b. Why it matters

`slope_sensitivity` controls **two** scoring decisions in the engine:

1. **ROCSlope classification:** `delta = rocSeries.Last() - rocSeries(rocSeries.Count - 2)`; classified RISING if `delta > slope_sensitivity`, FALLING if `< -slope_sensitivity`, FLAT otherwise.
2. **rocPartialLong/Short threshold:** `r.ROC > slope_sensitivity` is the magnitude check for partial ROC scoring.
3. **Pass 2c regime-alignment activation:** `Math.Abs(r.ROC) >= slope_sensitivity` determines whether ROC participates in the TRENDING alignment check.

Three uses, one cfg key. Currently 0.1 (0.1%).

### 5c. Trade-off

For BTC 1m bar-to-bar ROC delta, 0.1% is too coarse — only 6% of runs detect a non-FLAT slope. Lowering to 0.03 would fire ~30% of the time, which is the natural distribution.

But lowering also affects (2) and (3): partial ROC fires on smaller ROC values, Pass 2c activation triggers more often. More signal, more noise.

### 5d. v19 recalibration

```json
"ROC": {
  "period": 9,
  "slope_sensitivity": 0.05,   // was 0.1 — 2x lower, conservative compromise
  "series_lookback": 3
}
```

Effect: ROCSlope fires RISING/FALLING ~10–15% of the time (estimated from data — not 30% because we only halve, don't 3.3×). Modest improvement to scoring sensitivity without dramatically inflating partial-ROC noise.

**Conservative choice.** A more aggressive 0.03 would fire ROCSlope more often but increase noise across all three uses. 0.05 is the safe middle. If post-v19 data shows ROCSlope still > 80% FLAT, drop to 0.03 in a v20 calibration pass.

**Future spec opportunity.** The conflated cfg key (one threshold for three different decisions) is suboptimal. A future code change could split it into:

- `slope_delta_threshold` — for slope classification only
- `magnitude_threshold` — for partial scoring + Pass 2c activation

Not in scope for v19. Calibration adjustment only.

---

## 6. Liquidation Trade Fetch Window

### 6a. Observed scale

All 618 rows: `LiqLongSize = LiqShortSize = 0`, `LiqSignal = NONE`.

`GetRecentTradesAsync(100)` fetches the last 100 trades; the engine scans for `Liquidation != "none"`. During NY-session activity, 100 trades can span only 30–60 seconds. Liquidations from minutes earlier are invisible.

### 6b. v19 fix — code change

In `UI/MainForm_Analysis.vb`, find the existing fetch call:

```vb
Dim t_trades  = DeribitClient.GetRecentTradesAsync(100)
```

Change to:

```vb
Dim t_trades  = DeribitClient.GetRecentTradesAsync(500)
```

Single number, one location.

### 6c. Performance impact

Trivial. Deribit's `get_last_trades_by_instrument` accepts up to `count=1000`. 500 trades is roughly 5–10× larger response payload (~250 KB per call vs ~50 KB), still well within HttpClient's default capacity. The 15-second request timeout (post-v0.4 resilience) easily accommodates this.

### 6d. Why not bigger (e.g., 1000)?

- 500 covers ~3–5 minutes of typical NY activity, ~10+ minutes of quiet conditions. Long enough to catch most liq cascades.
- 1000 doubles the response size for marginal gain. Most liq events that matter to a 2–15 min hold cluster in the last few minutes.
- TFI / MicroCVD / CVD also consume `recentTrades`. Their windows (`tfi_window_size = 30`, `microcvd_window_size = 50`) are unchanged — they still slice the most-recent N trades from the same fetched list. Performance impact on those is zero.

### 6e. Why not promote `count` to settings?

It already exists implicitly — the v15 cleanup removed `cvd.trade_lookback` because it wasn't wired through. The 100/500 number is hardcoded at the call site. Could be lifted to settings, but: it's structural (affects multiple downstream consumers, not just liquidations) and rarely tuned. Keep as a code constant for now. Promote to settings if data shows further tuning is warranted.

---

## 7. Implementation Steps

Single commit, all changes together.

### 7a. Update `settings.json`

- Bump version to **v19**.
- Update the keys per Sections 2–5.
- Add a `change_log` entry summarising the recalibration.

### 7b. Update `UI/MainForm_Analysis.vb`

- Single line change per Section 6b.

### 7c. Build verification

`dotnet build` returns 0 warnings, 0 errors.

### 7d. Manual test (post-implementation)

1. **Reset Log** — the existing 618 rows have stuck columns; not useful with new thresholds.
2. Run 5–10 analyses across mixed conditions if possible.
3. Verify `analysis_log.csv` shows non-default values appearing in:
   - `FundingBias` (LONGS CROWDED / SHORTS CROWDED / NEUTRAL — at minimum NEUTRAL during truly zero-funding periods, but expect some non-NEUTRAL during typical conditions)
   - `OISignal` (some non-NEUTRAL during the test window — expect at least 1–2 in 10 runs)
   - `ROCSlope` (mix of RISING/FALLING/FLAT, expect 30–40% non-FLAT)
4. Liquidation visibility may still be sparse — depends on market activity. Verify `LiqLongSize`/`ShortSize` show non-zero on at least one run if any liq cascades occur during the test window.
5. Calibration report — confirm the distribution sections show variation across the new columns.

If verifications fail (e.g., FundingBias still 100% NEUTRAL), the recalibration was insufficient — either thresholds need further lowering or BTC funding is genuinely zero (rare but possible).

---

## 8. Files Changed Summary

| File | Change |
|---|---|
| `settings.json` | Bump to v19. Update funding band thresholds (4 keys), funding momentum threshold, OI change threshold, ROC slope sensitivity. Add change_log entry. |
| `UI/MainForm_Analysis.vb` | Change `GetRecentTradesAsync(100)` to `GetRecentTradesAsync(500)`. Single-line change. |

Approximate diff: ~12 lines settings.json, 1 line VB. No new tunables, no new code paths.

---

## 9. Out of Scope — Future Spec Candidates

These were observed during the audit but are not bundled into v19:

### 9a. RSI divergence over-firing (80% non-NONE)

`RSIDivergence` fires BEARISH 45% / BULLISH 35% / NONE 20% across 618 rows. Genuine RSI divergences are rare events (typically <10% of runs). The current rate suggests the algorithm is finding any swing high/low in the 30-bar lookback and labelling pullbacks as divergences.

Likely root cause in `CalcRSIDivergence`:

```vb
If bestHighPivotIdx >= 0 AndAlso
   bestHighPrice > currentPrice * (1.0 + priceGate) AndAlso
   bestHighRSI > currentRSI + rsiDelta Then
    Return "BEARISH"
End If
```

In a downtrending market, ANY recent swing high will satisfy `bestHighPrice > currentPrice * 1.001` and `bestHighRSI > currentRSI + 2`. The algorithm doesn't require the prior pivot to be in overbought territory or the RSI delta to be large enough to indicate exhaustion.

**Possible fixes** (require their own spec):

- Require prior pivot's RSI to be > 70 (overbought) for BEARISH divergence to fire.
- Require minimum vertical distance: e.g., `bestHighRSI > 65 AndAlso currentRSI < 55` to ensure exhaustion semantics.
- Tighten `rsiDelta` from 2 to 5+ to require meaningful RSI compression at higher highs.

Effect on scoring: `RSIDivergence` is used in `Step 2` to apply long/short penalties. Currently penalising 80% of runs is probably masking genuine divergence signal in the noise. Fixing this would tighten the signal but reduce the penalty rate substantially. **Should be specced separately** with care given to scoring impact and validation against historical data.

### 9b. ROC `slope_sensitivity` cfg key splitting

The conflation of three uses under one cfg key (Section 5b) is suboptimal. Future code change:

- `roc.slope_delta_threshold` — slope classification
- `roc.magnitude_threshold` — partial scoring + Pass 2c activation

Not blocking; v19 single-key tuning is sufficient for now.

### 9c. Liquidation count promotion to settings

If 500 trades proves insufficient or excessive, promote the count to `cfg.Indicators.Liquidations.TradeCount` (re-introducing the v15-removed key under a new name). Defer until data shows tuning is needed.

---

## 10. What This Does NOT Do

- Does **not** change scoring logic, indicators, MTF gate, Pass 2c, Kelly, regime classification.
- Does **not** modify `CalcRSIDivergence` (over-firing flagged in Section 9 for separate spec).
- Does **not** modify `CalcMicroCVD`, `CalcCVD`, or any indicator computation. Pure threshold + window adjustment.
- Does **not** add a CSV column. Schema unchanged from v0.3.
- Does **not** add new cfg keys. Adjusts existing ones only.
- Does **not** change the funding rate field type or the OI computation. Only the classification thresholds downstream.

---

## 11. Validation Plan

After implementation:

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors.
2. **Reset Log:** clear existing 618 rows (they have degenerate columns and are not useful with new thresholds).
3. **Smoke test:** run 10–20 analyses across the test window. Verify at least one non-default value appears in:
   - `FundingBias` (at minimum NEUTRAL during zero-funding windows; expect some non-NEUTRAL)
   - `OISignal` (expect at least 1 non-NEUTRAL in 10 runs)
   - `ROCSlope` (expect 30%+ non-FLAT)
4. **Calibration report:** confirm distribution sections show variation across the previously-stuck columns.
5. **Liquidation:** spot-check whether any liq events are detected during the test window. May still be zero if the market is genuinely calm — that's acceptable given the 500-trade window expansion is a *capability* improvement, not a guarantee of liq detection.

If FundingBias is still 100% NEUTRAL across 20 runs, BTC funding is genuinely at zero — unusual but possible during deep basis-compression regimes. No action required; thresholds will fire when funding moves.

---

## 12. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should we also recalibrate Pass 2c regime alignment thresholds? | **No.** Pass 2c uses `slope_sensitivity` (already tuned in Section 5) and structural signals (EMA, VWAP, RSI, Donchian, CVD) — no thresholds specific to Pass 2c that need adjustment | Resolved |
| Q2 | Should we ship v19 + RSI divergence fix together? | **No.** v19 is settings-only (1 line of code for liq window). RSI divergence fix is an algorithmic change requiring its own spec, validation, and scoring impact analysis. Ship separately | Resolved |
| Q3 | Should we reset the existing log or keep it? | **Reset.** 618 rows with 100% degenerate values in 5 columns are not useful for the auto-tweaker. Cleaner to start fresh post-v19 | Resolved |
| Q4 | What if BTC enters a trending basis regime mid-calibration and v19 funding thresholds fire too often? | **Tune in a v20 pass when observed.** v19 is calibrated to the *current* regime. The auto-tweaker (Section 16.1, future spec) is the long-term answer to regime-shifting tunables | Resolved |
| Q5 | Should `slope_sensitivity` be lowered to 0.03 instead of 0.05? | **0.05 first (conservative).** If post-v19 data shows ROCSlope still > 80% FLAT, drop to 0.03 in v20. The threshold affects three scoring decisions, so cautious tuning is better than aggressive | Resolved |
| Q6 | Should we promote the trade fetch count to settings? | **No.** Single hardcoded number, structural impact (affects TFI, CVD, MicroCVD, Liq downstream consumers via shared `recentTrades`), rarely tuned. Promote to settings only if data shows further tuning is needed | Resolved |
| Q7 | Should we widen the OI history retention beyond 70 minutes? | **No.** OIChange15m / 60m are already computed against the existing 70-min retention buffer. The threshold fix in Section 4 addresses the actual problem (classification cutoff too high). Retention is fine | Resolved |
| Q8 | Should we tune `tfi.threshold` (currently 0.15)? | **Not in v19.** TFI distribution wasn't audited as stuck. Defer until specific data shows it needs adjustment | Resolved |
