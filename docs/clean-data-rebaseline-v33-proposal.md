# Clean-Data Re-Baseline — v33 Calibration Proposal

**Date:** 2026-06-13 (review run against CSV snapshot through 2026-06-12 15:44 UTC)
**Status:** PROPOSED — settings-only; zero code changes. All changes approval-gated by the trader.
**Brief:** `docs/clean-data-rebaseline-brief.md` | Re-baseline table: `engine-correctness-pass-proposal.md` §8 | WATCHING row: `DeribitIndicatorProject.md` §12.
**Inputs:** `analysis_log.csv` (v0.6, 489 rows), `analysis_report_20260612_153050.md`, `analysis_eval_cache.csv` (v2, 987 entries), `settings.json` v32, Deribit public-API 1m re-fetch (see Method).

---

## 0. Gate ruling: CalibrationReport NOT-READY vs the re-baseline trigger

The in-app CalibrationReport says **NOT YET READY** while this review proceeded anyway. These are two different gates and the conflict is by design:

| Gate | Criteria | Status at review |
|---|---|---|
| CalibrationReport READY (`MainForm_Calibration.vb`) | ≥300 rows AND ≥3 regimes with ≥50 rows each AND ≥3 session days | FAILS: 2 days, 2 regimes ≥50 (TRENDING_UP 353, RANGE_BOUND 86; TRANSITIONAL 35, TRENDING_DOWN 15) |
| Re-baseline trigger (proposal §8 / §12 WATCHING) | ≥300 clean v0.5+ rows spanning ≥2 sessions | MET: 489 rows, 2 session days |

The READY verdict gates a **full recalibration** of market-scale thresholds (RSI bands, funding bands, OFI ratios) — those need regime and session diversity. This pass re-baselines only the **signal-coupled thresholds whose units or semantics changed in v31** — the brief's Step 0 explicitly prescribes partial re-baseline on a lopsided sample rather than waiting, because the OBV gate is an uncalibrated seed: every row collected under it is collected under a known-wrong dead-band.

The three values proposed below are all **normalized / ratio quantities** (volume-normalized drift, proportional USD dead-bands) — exactly the subset a single-session sample can support. Everything session- or regime-shaped is deferred (§3).

---

## 1. Step 0 — sample composition

489 rows, 2026-06-11 10:01 → 2026-06-12 15:44 UTC. Collection blocks: 06-11 16–20h UTC (416 rows), 06-12 14–15:45h UTC (71 rows), plus 2 stray rows at 10h.

| Axis | Distribution | Supportable? |
|---|---|---|
| Session bucket (UTC) | **NY 487, LONDON 2, ASIA 0** | NY only. Session-bucket items deferred. |
| Regime | TRENDING_UP 353, RANGE_BOUND 86, TRANSITIONAL 35, TRENDING_DOWN 15 | TRENDING_UP + RANGE_BOUND (≥50). TRANSITIONAL / TRENDING_DOWN too thin. |
| Verdict tier | STRONG 19, MEDIUM 89, WEAK 183, NO TRADE 198 (incl. 49 MTF-blocked, 1 [TIE]) | OK for distribution sanity; STRONG too thin for outcome stats. |
| Days | 2 (06-11, 06-12) | Below CalibrationReport's 3-day bar — noted, not blocking (see §0). |

Note: the brief's "≥2 sessions" was met as two trading days, but in **bucket** terms this is a one-session (NY) sample. The proposed changes were screened for session-dependence; absolute-USD knobs that would NOT transfer across sessions were left alone or made proportional.

## 2. Method — offline recompute

`ohlc_1m_cache.csv` stores **Volume = 0 on every bar** (it exists for barrier evaluation only), so OBV drift and dynamic volume thresholds were recomputed from a fresh Deribit public-API fetch (`get_tradingview_chart_data`, 1m, 2026-06-11 05:30 → 06-12 16:30 UTC, 2,101 bars, BTC `volume` field — same field the live engine scores on).

Validation against live-logged classifications:
- **OBV:** recomputed `obvChange` (250-bar window, mean-volume normalised, per F5) reproduces the logged OBVTrend at gate 10.0 on **472/489 rows (96.5%)** — residual mismatch is partial-bar timing at the gate boundary.
- **MicroCVD:** exact reclassification from logged E/M/L columns at current settings reproduces the logged mix to within 1 row (233/239/17 vs logged 232/239/18).

Both sweeps below are therefore distribution-faithful, not extrapolations.

---

## 3. Review items and rulings

### 3.1 `indicators.OBV.trend_gate` — **CHANGE 10.0 → 18.0** (the priority seed)

At the seeded gate 10.0, OBV is directional on **84.3%** of runs (FLAT 74/489) — and in RANGE_BOUND it is directional on **93%** (80/86 rows, all FALLING). A confirm-tier signal that votes 5 runs in 6, and votes *more* often in chop, is a noise vote.

Recomputed `|obvChange|` distribution (units: average-bar-volumes of net drift): p25 = 11.4, **p50 = 18.5**, p75 = 28.5, p90 = 35.0. Gate sweep (directional rate):

| gate | all | TRENDING | RANGE_BOUND |
|---|---|---|---|
| 10.0 (current) | 84.3% | 80.7% | 93.0% |
| 15.0 | 55.6% | 48.4% | 69.8% |
| **18.0 (proposed ≈ sample p50)** | **~50%** | **~43%** | **~63%** |
| 20.0 | 46.6% | 39.1% | 58.1% |
| 30.0 | 18.0% | 13.6% | 44.2% |

Rationale for 18.0: median-split — OBV votes only when net drift is in the top half of observed magnitude. Sign quality is already good (at gate 10, OBV sign agrees with the 250m price-drift sign 378:10 where |drift| > 0.15%), so the gate's job is purely vote-rate control.

**Known limitation (no gate fixes it):** RANGE_BOUND stays *more* directional than TRENDING at every gate. The OBV window (250 bars ≈ 4.2 h) is far longer than regime dwell time — a range-bound hour inside a trending afternoon still carries the trend's drift in its window. The brief's ideal ("directional on trends, FLAT in chop") is unreachable at this window length; documented here so the next reviewer doesn't chase it with gate values. Stays WATCHING; re-anchor the median once multi-session data exists.

### 3.2 `indicators.CVD.slope_pct_of_value` — **CHANGE 0.01 → 0.05**; `slope_min_usd` — KEEP 12000

Post-F1 (chronologically-correct slope), CVDSlope is FLAT on only **13/489 rows (2.7%)** — RISING/FALLING fire as a near-coin-flip, and forward returns confirm zero discrimination in this sample (15m forward: FALLING +18.1 bp vs RISING +8.7 bp — inverted, in an up-drifting tape). The classifier is under-thresholded for the post-fix flow scale.

The threshold is `max(slope_min_usd, |CVDValue| × slope_pct_of_value)`. At pct = 0.01 the proportional arm exceeds the 12k floor on only **48/489 rows (10%)** — the activity-scaling design is effectively dead, because |CVDValue| runs p50 = 341k / p90 = 1.19M on the 500-trade window. At **pct = 0.05** the proportional arm activates on **294/489 rows (60%)**, giving threshold p50 ≈ 17k, p75 ≈ 38k, p90 ≈ 59k — the dead-band widens exactly on busy-but-balanced windows, which is where false RISING/FALLING comes from. (0.05 is also the original `CalcCVD` function-default pairing.)

The numeric `weightedSlope` is not CSV-logged, so the resulting FLAT share can't be predicted precisely — first-100-row check below. If FLAT is still < 10% under v33, raise `slope_min_usd` in a follow-up; and the next CSV schema bump should log `weightedSlope` so this knob can be swept directly (code-side note, out of scope here).

### 3.3 `indicators.MicroCVD.accel_threshold_dynamic_pct` — **CHANGE 0.03 → 0.30**; `accel_threshold` 10000 + `accel_threshold_floor_pct` 0.25 — KEEP

Logged mix: ACCEL 232 / DECEL 239 / **FLAT 18 (3.7%)**. Effective threshold = `max(windowUSD × 0.03, 10000 × 0.25 = 2500)` ≈ 3.2k at the median window vs |late − early| p50 ≈ 50k — the dead-band is an order of magnitude too narrow, so accel/decel classify continuously.

The dynamic arm does bind (the brief's question): windowUSD × 0.03 > 2500 on ≥ 60% of rows (lower-bound estimate; |E|+|M|+|L| understates gross window USD). The problem is its *level*, not its arm selection.

Exact reclassification sweep from logged E/M/L (FLAT shares are lower bounds for the same reason):

| dyn_pct | ACCEL | DECEL | FLAT |
|---|---|---|---|
| 0.03 (current) | 47.6% | 48.9% | 3.5% |
| 0.10 | 43.1% | 47.6% | 9.2% |
| 0.20 | 40.3% | 43.6% | 16.2% |
| **0.30 (proposed)** | **37.2%** | **40.1%** | **≥22.7%** |
| 0.40 | 31.9% | 36.4% | 31.7% |

At 0.30 the true FLAT share lands roughly 25–30%. Note the sign-arms cap FLAT structurally: 125/489 rows (25.6%) classify DECEL regardless of threshold (late segment sign-flipped against net flow) — that arm is semantics, not calibration, and is left alone. Scoring impact is conservative: ACCEL votes and DECEL penalties now require a late-vs-early gap ≥ 30% of window flow; FLAT applies the stall penalty only on genuine price/CVD contradiction.

### 3.4 `session_volume` multipliers — **DEFER** (cannot re-baseline: zero ASIA rows, 2 LONDON rows)

Untouchable until ASIA + LONDON collection exists. **Recorded observation for that future pass:** in NY, logged VolumeRatio runs p50 = 0.23 / p95 = 1.78, while the recomputed dynamic HIGH threshold (completed-bar basis) lands ~3.5 × 1.15 ≈ 4.1 — the Volume HIGH signal fires on ~1% of runs and MID on ~4%. The ratio samples the in-progress partial bar against a completed-bar SMA, so the signal is structurally starved at 30–60s polling offsets. That is a code-semantics question (partial-bar ratio), not a multiplier question — flag for a spec when the session pass happens; do not compensate with multipliers.

### 3.5 `indicators.Volume` dynamic clamps — **KEEP** (2.0–6.0 / 1.5–4.0)

Recomputed `highRaw` per run (NY): p25 = 3.15, p50 = 3.57, p75 = 3.80, p90 = 4.36. **Zero rows clamped at either end; zero static-fallback (low-variance) events.** The clamps are not binding; nothing to move. ASIA/LONDON placement unknown — rolls into the session pass.

### 3.6 `indicators.Donchian.quartile_pct` — **KEEP** (0.25)

Post-F6 mix: full LONG 24 + full SHORT 11 (7.2%), partials 235 (48.1%), NONE 219 (44.8%). Full breakouts genuinely fire now (the F6 objective); partials are upgrade-gated in Pass 2 so the 48% rate is not raw vote noise. Directional skew (LONG_PARTIAL 148 vs SHORT_PARTIAL 87) matches the up-trending tape. No evidence for movement.

### 3.7 Verdict tiers / NO TRADE per regime — **SANITY PASS**, no change to `verdict_*_pct`

NO TRADE rate: TRENDING_UP 36.5%, RANGE_BOUND 52.3%, TRANSITIONAL 60.0%, TRENDING_DOWN 20.0% (n=15) — ordered correctly by regime quality, neither collapsed nor exploded. `[TIE]` fired once in 489 runs (F4's no-direction case is appropriately rare). Tier outcomes (eval cache, barrier metric): WEAK ~60%, MEDIUM ~52%, STRONG ~71% success — STRONG n=7, not yet meaningful. The Analysis Report's stable cell (MEDIUM_LONG 15m / 0.3×ATR = 15.5% failure, n=71) is healthy.

### 3.8 MTF BLOCK rate per side — **SANITY PASS**, no `mtf_gate` change

Per-side flags are perfectly complementary in this sample: L-pass/S-block 343, S-pass/L-block 146, both-pass/both-block 0 — expected when 15m ADX stayed ≥ 20 with a clear trend throughout; both-pass needs a no-trend 15m state this window never produced. Post-H2 blocks redistribute both ways: 20 × `NO TRADE [WEAK LONG]` vs 29 × `NO TRADE [WEAK SHORT]`. Direction-aware veto behaving as specced.

### 3.9 `scoring.atr_target_multiplier` / `atr_stop_multiplier` — **KEEP** (2.0 / 1.2); v28 gap item clarified

Boundary marked per the brief: Tier D D2 (`482c9bb`, linear ATR geometry) landed 2026-06-11 15:51 UTC; eval barriers were *always* raw-ATR (D2 reconciled the display), so all eval rows are usable and post-boundary numbers match the full set.

Finding: the v28 target-vs-barrier gap is **0.0pp** — because `TargetEverHit` differs from barrier success only on adverse-first-then-recover sequences, and there were none: of ~590 directional eval entries, ~62% hit the favourable barrier first, **only 3.6% ever touched the adverse barrier**, ~35% expired without reaching favourable. Read: stops at 1.2×ATR are nowhere near too tight (they are almost never touched inside 15m); failures are reach-failures, not stop-outs. The §12 expectation of a 30–50pp gap came from the contaminated pre-reset probe and should be retired as written — re-frame the WATCHING item as "adverse-hit rate < 5%: stop multiplier has headroom; revisit only if adverse-hits materialise." No settings change.

---

## 4. Proposed settings.json diff (v32 → v33)

```diff
   "OBV":      { "trend_gate": 10.0, ... }            →  "trend_gate": 18.0
   "CVD":      { ..., "slope_pct_of_value": 0.01 }    →  "slope_pct_of_value": 0.05
   "MicroCVD": { ..., "accel_threshold_dynamic_pct": 0.03 }  →  "accel_threshold_dynamic_pct": 0.30
```

Three values; no keys added or removed. `version` → 33; `last_modified` / `modified_by` updated. POCO defaults in `EngineSettings.vb` deliberately untouched (settings-only pass; POCO re-alignment can ride the next code commit per Tier C precedent).

**change_log entry (newest-first), ready to paste:**

> v33 (2026-06-13): clean-data re-baseline pass (docs/clean-data-rebaseline-v33-proposal.md). Partial re-baseline on 489 clean NY-session rows (Step 0: ASIA/LONDON + TRANSITIONAL/TRENDING_DOWN too thin — session_volume and clamps deferred). indicators.OBV.trend_gate 10.0 → 18.0 — offline recompute (96.5% validation vs logged) put |obvChange| p50 at 18.5; seed left OBV directional on 84% of runs (93% in RANGE_BOUND). indicators.CVD.slope_pct_of_value 0.01 → 0.05 — proportional arm was dead (exceeded the 12k floor on 10% of rows; CVDSlope FLAT 2.7%, no forward-return discrimination); 0.05 activates scaling on 60% of rows. indicators.MicroCVD.accel_threshold_dynamic_pct 0.03 → 0.30 — exact E/M/L reclassification sweep; FLAT 3.7% → est. 25-30%; dead-band was ~3k vs |late-early| p50 ~50k. All three are normalized/ratio quantities supportable from a one-session sample. atr multipliers, Donchian quartile, mtf_gate, verdict pcts reviewed — no change (rationale in proposal §3.5-3.9).

**§15 row, ready to paste:** one-line summary referencing this doc + the three value moves.

## 5. Expected post-v33 impact + first-100-row checks

All three changes remove noise votes/penalties → mild score deflation, slightly more NO TRADE. That is the intended direction (conservative false-positive tolerance), but verify it stays *mild*:

1. **NO TRADE rate** per regime: expect +0 to +8pp vs §3.7 baselines. If it jumps >15pp, the OBV gate overshot — drop to 15.0.
2. **CVDSlope FLAT share**: expect 10–30%. If still <10%, raise `slope_min_usd` next pass (and log `weightedSlope` in the next CSV bump).
3. **MicroCVD FLAT share**: expect 20–35% (sweep says ≥22.7% lower bound).
4. **OBVTrend FLAT share**: expect ~45–55% on a comparable tape.

## 6. Still owed by data collection

- **ASIA + LONDON sessions** (≥50 rows each) → unlocks `session_volume` multipliers + clamp placement review (§3.4/3.5) and the partial-bar volume-starvation spec decision.
- **TRANSITIONAL ≥50 / TRENDING_DOWN ≥50 rows** + a third session day → CalibrationReport READY → full recalibration eligibility.
- **STRONG-tier outcomes** (n=19 verdicts, 7 evaluated) → Kelly `est_prob_*` review stays parked (§12).

## 7. After v33 lands

Per the brief: supervised auto-tweaker first fire — confirm `dry_run_enabled: true` in `tweaker_config.json`, trader watching, review its diff against this rationale before any apply. Rows scored under v31/v32 thresholds precede v33; the first windows reflect pre-re-baseline behaviour (informational, not a defect).
