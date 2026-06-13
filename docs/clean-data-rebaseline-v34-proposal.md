# Clean-Data Re-Baseline — v34 Calibration Proposal (FULL recalibration)

**Date:** 2026-06-13 (review run against `analysis_log.csv` snapshot through 2026-06-13 10:05 UTC)
**Status:** APPROVED & APPLIED 2026-06-13 — `settings.json` v33→v34 written (local commit pending; not pushed). Settings-only; zero code changes. Trader chose ASIA **1.10/1.05** (pre-emptive over the neutral 1.00 baseline) and flagged the weekday/weekend confound (§1, §3.1).
**Brief:** `docs/clean-data-rebaseline-v34-brief.md` | Methodology template: `clean-data-rebaseline-v33-proposal.md` | WATCHING row: `DeribitIndicatorProject.md` §12.
**Inputs:** `analysis_log.csv` (v0.6, **975 rows**), `analysis_eval_cache.csv` (v2, 1532 entries), `settings.json` v33. Distributions computed directly from the logged columns; indicator mechanisms confirmed against source (`DynamicNorms.vb`, `Core/Indicators_OrderFlow.vb`).
**Reviewer:** Opus @ High (budget-constrained path per brief).

---

## 0. Gate ruling: CalibrationReport READY — full recalibration unlocked

Unlike v33 (which proceeded against a NOT-READY report on a one-session sample and re-baselined only normalized/ratio quantities), the in-app CalibrationReport is now **READY**:

| Criterion | Bar | Status |
|---|---|---|
| Rows | ≥ 300 | **975** ✅ |
| Regimes ≥ 50 | ≥ 3 | **3** (TRENDING_UP 642, RANGE_BOUND 200, TRANSITIONAL 108) ✅ |
| Session days | ≥ 3 | **3** (06-11, 06-12, 06-13) ✅ |

READY unlocks the session- and regime-shaped items v33 explicitly deferred (`session_volume` multipliers, per-session classifier behaviour, regime NO-TRADE rates). Two caveats carried into the rulings below:
- **TRENDING_DOWN is still thin (n=25)** — TRENDING_DOWN-specific values stay deferred.
- **Day weighting is lopsided** (06-11: 417 rows, 06-12: 73, 06-13: 485). Three calendar days, but two collection-heavy blocks. Session diversity is the gate that matters here and it is met (ASIA 233 / LONDON 254 / NY 488).

---

## 1. Step 0 — sample composition

975 rows, 2026-06-11 10:01 → 2026-06-13 10:05 UTC.

| Axis | Distribution | Supportable? |
|---|---|---|
| Session (UTC) | **ASIA 233, LONDON 254, NY 488** | All three ≥ 50 — session items now reviewable. |
| Regime | TRENDING_UP 642, RANGE_BOUND 200, TRANSITIONAL 108, **TRENDING_DOWN 25** | 3 regimes ≥ 50. TRENDING_DOWN deferred. |
| Verdict tier | NO TRADE 438 (122 MTF-blocked-directional, 4 [TIE]), WEAK 347, MEDIUM 157, STRONG 33 | OK for distribution sanity; STRONG (33) usable but light for outcome stats. |
| Session × regime | ASIA: TU 149 / RB 64 / TR 20 · LONDON: TU 141 / TR 54 / RB 49 / TD 10 · NY: TU 352 / RB 87 / TR 34 / TD 15 | Big cells ≥ 50; thin cells noted per item. |

**Weekday/weekend split (trader-flagged confound, matters for item A).** The session buckets are also a calendar split: **all 233 ASIA rows are Saturday 06-13 UTC** (100% weekend), LONDON is 252/254 Saturday, and **NY is 100% weekday** (Thu 416 + Fri 72). So the ASIA-vs-NY comparison in §3.1 is confounded by weekday/weekend, not just session-of-day — weekday Asia (UTC 0–7 Mon–Fri) is absent from this sample. The over-trading *direction* is robust (the 0.80/0.85 multiplier lowers the Asia volume bar on any day), but the chop *magnitude* (27.5% RANGE_BOUND) may be partly a thin-weekend effect. `session_volume` is hour-bucketed with no weekday/weekend split, so the v34 ASIA value applies to weekday Asia too — flagged for re-verification (§5, §7).

**Session-bucket boundary (verified, matters for item A).** `AnalysisLogger.vb:128` logs `Timestamp` as **UTC**. `DynamicNorms.vb:120-122` buckets `session_volume` by `DateTime.UtcNow.Hour` against the **settings.json bounds** — so the engine's ASIA bucket is **utcHour 0–7 inclusive**. 120 of the 233 ASIA rows sit at utcHour=07; they are correctly attributed to ASIA because that is the multiplier the engine *applied*. (Minor code-hygiene note: the display-only `ResolveSessionLabel` at `MainForm_Render_Cards.vb:1379` uses `< 7` for ASIA and would label these 120 rows LONDON — a one-hour off-by-one vs the engine's actual bucketing. Display-only, flagged in §6, not a settings item.)

---

## 2. Method

All distributions are computed directly from the v0.6 CSV columns (975 rows) — no extrapolation. Indicator decision logic was read from source to state each mechanism faithfully (`CalcFundingMomentum`, `CalcSpread`, `DynamicNorms.Compute`/`ApplySessionVolume`, CVD/MicroCVD thresholds per v33 §3.2–3.3).

**What this pass does NOT do (deferred precision, stated honestly):** `ohlc_1m_cache.csv` stores `Volume = 0` on every bar (it exists for barrier evaluation only), so the per-run dynamic volume threshold (`VolHighThreshold = clamp(1 + 2·volSD/volMean, 2, 6) × sessionMult`) is **not reconstructable from the CSV**. v33 §3.5 did a fresh Deribit re-fetch to recompute it for NY; this pass does **not** re-fetch. That makes item A's exact post-change *fire rate* unpredictable from logged data — but the recommendation there is a baseline-setting / direction call (neutralise a known-backwards multiplier and hand fine-tuning to the supervised tweaker), for which the logged `VolumeRatio` distribution + the clamp-floor mechanics are sufficient. Where a precise value would need the re-fetch, that is called out.

---

## 3. Review items and rulings

### 3.1 `session_volume` ASIA multipliers — **CHANGE 0.80/0.85 → 1.10/1.05** (the headline)

**Thesis (confirmed): ASIA over-trades the choppiest session.**

| Session | trade-rate | RANGE_BOUND % | NO-TRADE % | VolRatio p90 | VolRatio p95 | barrier-SUCCESS | ADVERSE | EXPIRED |
|---|---|---|---|---|---|---|---|---|
| **ASIA** | **63.1%** | **27.5%** | 36.9% | **2.01** | **3.38** | 73.7% | 3.9% | 22.4% |
| LONDON | 38.6% | 19.3% | 61.4% | 1.27 | 2.52 | 67.9% | 1.9% | 30.2% |
| NY | 59.8% | 17.8% | 40.2% | 1.37 | 1.80 | 61.3% | 3.6% | 35.1% |

ASIA trades the most (63.1%) while being the choppiest (27.5% RANGE_BOUND) — the opposite of the profile's "pure chop = no trade."

**Mechanism (quantified).** The session multiplier scales an already-clamped dynamic threshold: `VolHighThreshold = clamp(1 + 2·CoV, 2.0, 6.0) × highMult`, `VolMidThreshold = clamp(1 + CoV, 1.5, 4.0) × midMult`. Two compounding effects make ASIA confirm volume too easily:
1. **Thin-denominator inflation.** ASIA's SMA9 volume baseline is low, so `VolumeRatio` (current bar / SMA9) spikes higher on any blip — ASIA's *upper tail* is fatter than NY's (p90 2.01 vs 1.37; p95 3.38 vs 1.80) despite Asia being the "thin" session.
2. **The multiplier lowers the bar further.** At 0.80/0.85, the *floor* effective threshold is `2.0 × 0.80 = 1.6` (HIGH) / `1.5 × 0.85 = 1.275` (MID) — well inside ASIA's `VolumeRatio` mass (p90 2.01). NY's 1.15/1.10 floors at 2.30/1.65, which NY `VolumeRatio` rarely reaches. So Asia gets volume confirmation on chop that NY would reject.

This is backwards for a thin session. **Raise ASIA to neutral (1.00/1.00)** — lifts the floor to 2.0/1.5 and stops the engine from *lowering* the confirmation bar in the session that already over-trades.

**Honest magnitude — do not oversell.** Two caveats temper how much this lever moves:
- **The barrier win-rate does NOT show Asia "losing."** ASIA barrier-SUCCESS is actually the *highest* (73.7%) — but that is confounded: low Asia ATR → tiny 0.3×ATR barriers → easily tagged (lowest expiry, 22.4%). The brief's "structurally lower edge" claim is **not supported by the barrier metric** in this sample (it can't adjudicate edge across sessions with ATR-scaled barriers). What *is* supported is over-trading in chop. So this fix is about **selectivity / false-positive discipline**, not stopping losses.
- **Volume confirmation is partial-bar-starved** (v33 §3.4, reaffirmed): `VolumeRatio` samples the in-progress bar against a completed-bar SMA, so it clears even 1.6 on only ~10–15% of ASIA rows. Neutralising the multiplier trims volume-HIGH votes on a few percent of rows — the right direction, a modest lever. The larger Asia gap is structural (accept; trade less), and the real unlock is the partial-bar fix (code spec, §6).

**Value: 1.10/1.05 (trader's choice — pre-emptive, applied).** The trader elected to pre-empt the tweaker rather than only neutralise: 1.10/1.05 floors the ASIA HIGH/MID thresholds at 2.2/1.575 — a notch above neutral — partially countering the ~1.5× thin-denominator tail inflation (vs the 1.00/1.00 baseline I'd floated, which only removes the backwards bias). Correct in direction regardless of the weekday/weekend confound (§1): 0.80/0.85 lowers the Asia bar on *any* day. The *magnitude* is weekend-set (the 233-row ASIA sample is 100% Saturday), so the supervised tweaker + §5 checks must re-validate it against weekday Asia. **LONDON (1.00/1.00) and NY (1.15/1.10) KEEP** — LONDON already neutral and trades least; NY trades at a healthy 59.8% with low chop. Precise ASIA fire-rate targeting still wants the offline volume re-fetch (the §2 limitation).

### 3.2 `indicators.funding.momentum_threshold` — **CHANGE 0.00001 → 0.00000005** (5e-8; live-but-low-signal)

FundingMomentum is **FLAT on 975/975 rows**. Root cause confirmed numerically:
- `CalcFundingMomentum` (line 362) classifies on `delta = rate[now] − rate[now − window(3)]` vs `±momentum_threshold`.
- The 3-sample funding delta is **nonzero on only 41/972 rows (4.2%)** — funding rate is extraordinarily sticky (15 distinct values across 975 rows, spanning only ±1.1e-5; Deribit `current_funding` in a near-zero-basis regime). `|FundingDelta|` (1-step): p50 1e-8, p85 6e-8, p99 1.3e-7, max 1.9e-7.
- Current threshold 1e-5 is ~50× the max observed delta → RISING/FALLING cannot fire.

Fire-rate sweep on the actual 3-sample deltas (mirrors the function exactly):

| momentum_threshold | RISING/FALLING fires |
|---|---|
| 1e-5 (current) | 0.6% (and 0% live, history-window timing) |
| 1e-6 | 2.4% |
| **5e-8 (proposed)** | **4.2%** |
| 1e-8 | 4.2% (same — flat across [1e-8, 1e-7]) |

**Ruling:** lower to 5e-8 (≈ p85 of 1-step deltas; anywhere in [1e-8, 1e-7] yields the same 4.2%). This makes the classifier *live* but the fire rate is **hard-capped at ~4% by funding stickiness** — even a threshold of zero can't exceed it. As the brief said: cheap one-value fix, genuinely low signal value in this regime (the Step-3b amplify/soften it gates rarely fires because funding rarely reaches "crowded"). Do not over-invest; re-check after a funding-regime change. *(Leave `indicators.OFI.momentum_threshold` 0.15 alone — different classifier, healthy.)*

### 3.3 `indicators.spread.wide_threshold_bps` — **KEEP 5.0; accept + document as REST-dead**

`SpreadBps` spans **0.0778–0.0803** across all 975 rows (23 distinct values) — the book is always exactly 1 tick ($0.50 / ~$63k); the tiny variance is just price in the denominator. `CalcSpread` (line 411) classifies WIDE only at `≥ wide_threshold_bps`; **0 rows reach 5.0** (would need a ~60× widening = flash crash). The WIDE penalty (spread's only scoring contribution) is structurally unreachable; spread is always TIGHT.

**Ruling: accept, no change.** Option (ii) from the brief — tighten `wide_threshold_bps` to ~0.15 to catch a 2-tick book — would catch **0 rows in this sample** (max observed 0.0803, a 2-tick spread ≈ 0.156 never occurred). Manufacturing a signal that isn't there at REST cadence adds noise, not information. Continuous spread sampling (catching transient widening during flushes) is a **WebSocket P4 item** (`websocket-migration-proposal.md` §11); revisit post-WS.

### 3.4 `indicators.CVD.slope_pct_of_value` — **CHANGE 0.05 → 0.10** (v33's pre-committed follow-up)

v33 first-100 check #2 set the bar: *"CVDSlope FLAT share: expect 10–30%. If still < 10%, raise next pass."* On the full multi-session sample, **CVDSlope FLAT is 5.5%** (ASIA 9.9% / LONDON 7.1% / **NY 2.7%**) — below the 10% floor. CVD is still under-thresholded.

The threshold is `max(slope_min_usd=12000, |CVDValue| × slope_pct_of_value)`. The proportional arm binds on **54% of rows** (`|CVD| × 0.05 > 12000`). The drag is **NY**: busiest flow (`|CVDValue|` p50 337k vs ASIA 158k), so its weighted slope is large and clears the 0.05 proportional threshold almost always → FLAT 2.7%. The lever for the overall target is therefore the **proportional arm (pct), not the floor** (v33's note said "raise slope_min_usd," but the multi-session split shows the floor governs the already-near-target thin sessions, while the busy NY rows are proportional-bound).

**Ruling: `slope_pct_of_value` 0.05 → 0.10** (clean 2× of the proportional arm; effective-threshold median 14k → 28k, p90 60k → 120k), **`slope_min_usd` KEEP 12000** (protects thin-Asia CVD from over-suppression — ASIA FLAT is already 9.9%, near target).

**Honesty caveat (carried from v33 §3.2):** the numeric `weightedSlope` is **not CSV-logged**, so the resulting FLAT share can't be predicted precisely — only monitored. v33's 0.01 → 0.05 (×5) moved FLAT 2.7% → 5.5% (sublinear response), so this ×2 is a measured step unlikely to overshoot; expect FLAT ~8–14%. **Guard:** if FLAT lands > 20%, revert toward 0.07; if still < 10%, raise the floor next pass. The enabling fix is to **log `weightedSlope` in the next CSV schema bump** (code spec, §6) so this knob can finally be swept directly instead of moved on faith.

### 3.5 `indicators.OBV.trend_gate` — **KEEP 18.0** (re-confirm; v33's concern reversed)

OBV is directional on 79.7% overall (FLAT 198/975) — above v33's predicted ~45–55%, but that prediction was naïve about sample composition: this tape is 66% TRENDING_UP, where high net OBV drift is *correct*. The regime split is now **healthy and correctly ordered**:

| Regime | OBV directional |
|---|---|
| TRENDING_UP | 87.4% |
| TRENDING_DOWN | 100% (n=25) |
| TRANSITIONAL | 79.6% |
| **RANGE_BOUND** | **52.5%** (FLAT 47.5%) |

v33's actual worry was that RANGE_BOUND was *more* directional than TRENDING (a window-length artifact at gate 10.0). At gate 18.0 that has **reversed**: OBV is now most-FLAT in chop (RANGE_BOUND 47.5% FLAT, ASIA 37.3% FLAT) and most-directional in trends — exactly the desired behaviour. The high overall rate is the trending sample, not a mis-set gate. **KEEP 18.0.** (Re-anchoring the median precisely would need the offline volume re-fetch — deferred; the regime ordering is sufficient to confirm the gate is doing its vote-rate job.)

### 3.6 `indicators.MicroCVD.accel_threshold_dynamic_pct` — **KEEP 0.30** (re-confirm)

v33 first-100 check #3 expected FLAT 20–35%. On the full sample, MicroCVD signal grouping is **ACCEL 365 / DECEL 411 / FLAT 199 = 20.4% FLAT** — squarely in target, confirming the v33 value holds across sessions (v33 set it from NY-only). `|late − early|` p50 39k vs the dynamic dead-band; the classifier requires a late-vs-early gap ≥ 30% of window flow before voting. **KEEP.**

### 3.7 `indicators.Volume` dynamic clamps — **KEEP 2.0–6.0 / 1.5–4.0** (clamp-binding check deferred)

Whether the clamps bind in ASIA/LONDON depends on per-run volume dispersion (`1 + 2·CoV`), which needs the volume series (`ohlc_1m_cache` is zero-volume — the §2 limitation). v33 §3.5 confirmed NY is unclamped via re-fetch; this pass does not re-fetch. No evidence to move; **KEEP**, and roll the ASIA/LONDON clamp-binding check into the same offline volume pass that would precisely target the §3.1 ASIA multiplier.

### 3.8 Market-scale classifiers (RSI / OFI / funding bands / verdict %) — **SANITY PASS, no change**

Reviewed only for "stuck or mis-firing." None qualify:

- **RSI(9):** p50 52.8, full-range spread (OB>60: 31.6%, mid[45–55]: 27.1%, OS<40: 18.4%). Not stuck; mild high-skew tracks the up-trending tape. Bands KEEP.
- **OFI:** BUY DOMINANT 372 / BALANCED 325 / SELL DOMINANT 278 — a healthy 3-way split at the 2.0/0.5 ratio thresholds. KEEP. *(Observation: `OFIRatio` p99 100 / max 1000 = thin-ask-book artifacts; doesn't affect the dominance classification.)*
- **Funding bands:** NEUTRAL 788 (80.8%) / LONGS CROWDED 142 / SHORTS CROWDED 45. The LOW band (±1e-5) fires CROWDED on the top distinct rate values (~19% of rows) — alive and reasonable. The HIGH band (±8e-5) is dormant because rate maxes at ±1.1e-5 — by design (rare-spike tier in a near-zero-basis regime, exactly v22's rationale). KEEP.
- **verdict_*_pct (NO-TRADE per regime):** TRENDING_UP 32.4% → TRENDING_DOWN 52.0% (n=25) → TRANSITIONAL 67.6% → RANGE_BOUND 72.0%. Correctly ordered by regime quality; STRONG fires 30× in TRENDING_UP, **0× in TRANSITIONAL** (correctly suppressed). Neither collapsed nor exploded. KEEP.

### 3.9 `indicators.OI.change_threshold_pct` (OISignal 95% NEUTRAL) — **WATCHING, no v34 change**

OISignal is NEUTRAL on 927/975 (95.1%); `|OIChange15m|` p90 = 0.2% sits right at the 0.2% (`0.002`) threshold, so only the top ~decile of 15m OI moves register. This is **borderline-stuck**: it means OI×CVD Pass 2b rarely has a full OI signal to cross-confirm. But lowering the threshold trades against the conservative-false-positive philosophy (a 0.1% OI move is small), the threshold was deliberately calibrated v19→v20, and OI-change magnitude is volatility-/regime-dependent (this is one 3-day window). **Not moved in v34.** Added to §12 WATCHING: if the OI×CVD gate proves too dormant to add value over more sessions, consider `0.002 → 0.001`.

### 3.10 `scoring.atr_target_multiplier` / `atr_stop_multiplier` — **KEEP 2.0 / 1.2** (re-confirm v33 §3.9, now multi-session)

Adverse-barrier hit rate is **3.4% overall** (ASIA 3.9% / LONDON 1.9% / NY 3.6%) — stops at 1.2×ATR are nowhere near too tight; failures are reach-failures (EXPIRED 32.2%), not stop-outs. The v33 §3.9 ruling holds across all three sessions: *"adverse-hit < 5% → stop multiplier has headroom; revisit only if adverse-hits materialise."* No change.

---

## 4. Proposed `settings.json` diff (v33 → v34)

```diff
   "CVD":      { "slope_min_usd": 12000.0, "slope_pct_of_value": 0.05, ... }
                                          → "slope_pct_of_value": 0.10
   "funding":  { ..., "momentum_threshold": 0.00001, ... }
                                          → "momentum_threshold": 0.00000005
   "session_volume.sessions[ASIA]": { "high_multiplier": 0.80, "mid_multiplier": 0.85 }
                                          → { "high_multiplier": 1.10, "mid_multiplier": 1.05 }
```

Three knobs (ASIA carries two values); no keys added or removed. `version` → 34; `last_modified` / `modified_by` updated. POCO defaults in `EngineSettings.vb` deliberately untouched (settings-only pass; POCO re-alignment rides the next code commit, per the Tier C / v33 precedent).

**change_log entry (newest-first), ready to paste:**

> v34 (2026-06-13): clean-data FULL re-baseline (docs/clean-data-rebaseline-v34-proposal.md). CalibrationReport READY (975 rows, 3 regimes >=50, 3 session days; ASIA 233 / LONDON 254 / NY 488). session_volume ASIA 0.80/0.85 -> 1.10/1.05 (applied; trader pre-emptive over neutral 1.00) — ASIA over-trades the choppiest session (trade-rate 63.1% / RANGE_BOUND 27.5%, both highest) and the 0.80/0.85 multiplier *lowered* the volume-confirmation floor (2.0x0.80=1.6) into ASIA's fatter thin-denominator VolumeRatio tail (p90 2.01 vs NY 1.37); neutralising removes a backwards bias and hands fine-tuning to the supervised tweaker. CONFOUND (trader-flagged): ASIA sample is 100% Saturday (weekend), NY 100% weekday -> magnitude weekend-set, re-verify vs weekday Asia. Honest scope: barrier win-rate does NOT show Asia losing (73.7% success is ATR-barrier-confounded), so this is a selectivity fix; partial-bar volume starvation caps the lever (code spec). indicators.funding.momentum_threshold 0.00001 -> 0.00000005 — FundingMomentum FLAT on 975/975; 3-sample delta nonzero on only 4.2% of rows (funding stickiness: 15 distinct rates, +/-1.1e-5); 5e-8 makes it live but hard-capped at ~4% (low signal, do not over-invest). indicators.CVD.slope_pct_of_value 0.05 -> 0.10 — v33's pre-committed follow-up: CVDSlope FLAT still 5.5% (<10% target), NY 2.7% the drag (proportional-bound on big flow); 0.10 doubles the proportional arm, floor kept at 12k to protect thin-Asia CVD. weightedSlope still unlogged -> monitor (guard: revert toward 0.07 if FLAT>20%). Reviewed & KEPT: OBV 18.0 (regime ordering now healthy — trend 87% > range 52% directional), MicroCVD 0.30 (FLAT 20.4%, in target), spread 5.0 (REST-dead, accept), volume clamps, RSI/OFI/funding bands/verdict pcts (sanity pass), ATR multipliers (adverse-hit 3.4%). OISignal 95% NEUTRAL -> WATCHING. spread continuous-sampling, partial-bar volume, weightedSlope-logging, ResolveSessionLabel hour-7 off-by-one -> flagged for code specs.

**§15 row, ready to paste:** one-line summary referencing this doc + the three value moves (ASIA session_volume, funding momentum_threshold, CVD slope_pct_of_value).

---

## 5. Expected post-v34 impact + first-N-row checks

All three changes increase selectivity (mild score deflation, slightly more NO TRADE / FLAT) — the intended direction, but verify it stays *mild*:

1. **ASIA trade-rate / NO-TRADE (weekend-confounded — judge against weekday rows):** expect ASIA trade-rate to drop and NO-TRADE to tick up. The 233-row ASIA baseline is 100% Saturday, so evaluate the post-change rate against *weekday* ASIA rows as they arrive — not against this weekend baseline. If weekday-ASIA NO-TRADE jumps > 15pp, 1.10/1.05 overshot — fall back toward 1.00.
2. **FundingMomentum:** expect RISING/FALLING to appear on ~2–4% of rows (was 0%). If still 0% after 100 rows, the live `_fundingHistory` window timing is suppressing it — investigate (don't lower further; the ceiling is funding stickiness).
3. **CVDSlope FLAT share:** expect 8–14% (was 5.5%), driven mostly by NY rising off 2.7%. If > 20%, revert `slope_pct_of_value` to 0.07. If still < 10%, raise `slope_min_usd` next pass.
4. **No regression in MicroCVD FLAT (~20%), OBV regime ordering, or adverse-hit (<5%)** — these are KEEPs; watch they don't drift.

---

## 6. Code-semantics findings flagged for separate specs (NOT settings)

1. **Partial-bar volume starvation** (v33 §3.4, reaffirmed multi-session): `VolumeRatio` samples the in-progress partial bar against a completed-bar SMA9 → structurally low (p50 ~0.17–0.23 all sessions), so the volume signal is starved regardless of multipliers. This caps item A's lever and is the real Asia unlock. Spec candidate: complete-bar volume sampling, or partial-bar extrapolation.
2. **`weightedSlope` not CSV-logged** — blocks precise CVD threshold calibration (§3.4). Log it in the next CSV schema bump so the knob can be swept directly.
3. **Continuous spread sampling** — `SpreadBps` is REST-dead (§3.3); transient flush-widening needs the WebSocket stream (P4).
4. **`ResolveSessionLabel` hour-7 off-by-one** (`MainForm_Render_Cards.vb:1379`) — display card buckets utcHour=07 as LONDON; the engine's `ApplySessionVolume` buckets it as ASIA. Display-only, but 120/975 rows are affected; align the card to the settings bound.

---

## 7. Still owed by data collection

- **TRENDING_DOWN ≥ 50 rows** (currently 25) → unlocks TRENDING_DOWN-specific review.
- **Weekday ASIA rows** (UTC 0–7 Mon–Fri) → de-confound the §3.1 ASIA reading (current sample is 100% Saturday) and validate the 1.10/1.05 magnitude.
- **Offline volume re-fetch** (fresh Deribit 1m with `volume`) → precise ASIA `session_volume` fire-rate targeting (§3.1) + ASIA/LONDON clamp-binding check (§3.7).
- **STRONG-tier outcomes** (33 verdicts) → Kelly `est_prob_*` review stays parked (§12).
- **More balanced day weighting** → reduces the 06-11/06-13 collection-block dominance.

---

## 8. After v34 lands — auto-tweaker sequencing

Per the brief: **manual v34 first, then hand maintenance to the supervised tweaker.** The judgement calls above — neutralise-vs-accept on Asia, "fix-but-low-value" on funding, "accept REST-dead" on spread, the barrier-confound nuance — are exactly what the auto-tweaker can't reason about (it tunes the failure-rate matrix, it doesn't reason about regime/session structure or metric confounds). Set this baseline by hand; then let the supervised first fire (dry-run, `dry_run_enabled: true`, trader watching, diff reviewed against this rationale) validate the loop and take over ongoing maintenance. Rows scored under v33 thresholds precede v34 — the first post-apply windows reflect pre-re-baseline behaviour (informational, not a defect).

---

*Settings-only; awaiting trader approval before any `settings.json` write or commit. No code changes in this pass.*
