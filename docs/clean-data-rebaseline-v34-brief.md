# Clean-Data Re-Baseline v34 — Brief (FULL recalibration, CalibrationReport READY)

**Date:** 2026-06-13
**Trigger:** CalibrationReport flipped **READY** at ~930 rows (≥3 regimes ≥50, ≥3 session days, ≥300 rows). This unlocks the **full** recalibration that v33 explicitly deferred (v33 was a partial pass on normalized/ratio quantities from an NY-only sample).
**Reviewer model:** Fable @ Extra (calibration judgement) **or** Opus @ High if budget-constrained — the v33 proposal (`clean-data-rebaseline-v33-proposal.md`) is the methodology template; follow its structure (Step 0 gate → per-item recompute → diff + change_log + first-100 checks).
**Output:** settings-only v34 proposal, approval-gated. No code changes (any code-semantics findings get flagged for a separate spec, as v33 §3.4 did).

## Carried-forward findings (diagnosed 2026-06-13 against the 930-row CSV — start here)

### A. Asia session over-trades chop — the headline item (was deferred in v33 §3.4 for lack of data; now have 233 ASIA rows)
Hard data, per-session trade rate + regime mix:
| Session | Trade rate (non-NO-TRADE) | RANGE_BOUND regime % |
|---|---|---|
| ASIA | **63.1%** (highest) | **27.5%** (highest — choppiest) |
| NY | 59.8% | 17.8% |
| LONDON | 46.0% | 9.4% |

Asia is simultaneously the **choppiest** session and the one the engine **trades the most** — the opposite of the profile's "pure chop = no trade." Two causes, separate the fix from the accept:
- **Settings (fixable now):** `session_volume` ASIA multipliers `high_mult 0.80 / mid_mult 0.85` *lower* the volume-confirmation bar in Asia (a "volume spike" registers on weaker Asia flow), so breakout confirmations fire on chop noise. This is backwards for a thin session. **Recommended direction: raise ASIA multipliers toward/above 1.0** so Asia demands *stronger* confirmation → fewer, higher-quality trades. Re-baseline against the 233 rows; verify the volume-HIGH fire rate and win-rate by session from the eval cache.
- **Market structure (accept, don't "fix"):** Asia (GMT+8 daytime) is genuinely lower-liquidity and more range-bound; momentum-breakout scalping has structurally lower edge there. Part of the underperformance is real — the correct response is *trade less in Asia*, which raising the multipliers achieves. Don't chase Asia parity with NY.

### B. Funding momentum structurally pinned FLAT (item 4.1) — real miscalibration, low current value
FundingMomentum = FLAT on **930/930** rows. Root cause is quantified, not theoretical:
- `FundingDelta` (abs, nonzero n=909): p50 **1e-8**, p90 6e-8, p99 1.5e-7, **max 1.9e-7**.
- `momentum_threshold` = **1e-5** — that is **~50× the max observed delta** and ~1000× the median. RISING/FALLING can never fire by construction.
- FundingRate itself spans only ±1.1e-5 (15 distinct values/930 rows) — Deribit `current_funding` in a near-zero-basis regime.
- **Ruling:** lower `indicators.funding.momentum_threshold` to ~**5e-8** (≈ p85 of observed deltas) to make the classifier live. **But flag honestly:** even correctly thresholded, funding momentum is a low-signal Step-3b adjunct in this regime (funding rarely reaches "crowded," so the amplify/soften it gates rarely fires). Cheap one-value fix; do not over-invest. Re-check after a funding-regime change.

### C. Spread signal dead at REST cadence (item 4.2) — NOT a bug, accept + document
SpreadBps spans **0.078–0.080 bps** across all 930 rows (max 0.0803) — i.e. the book is *always exactly 1 tick* ($0.50 / ~$62k); the tiny variance is just price in the denominator. BTC-PERPETUAL is that liquid.
- `wide_threshold_bps` 5.0 would need a ~60× widening (~30-tick book = flash-crash). The WIDE penalty (spread's only scoring contribution) is structurally unreachable; spread is always classified TIGHT.
- **Ruling:** not a settings emergency — this is a REST-snapshot limitation. Continuous spread sampling (catching transient widening during flushes) is a **WebSocket P4 item** (`websocket-migration-proposal.md` §11). Options: (i) accept and document as REST-dead; (ii) tighten `wide_threshold_bps` to ~0.15 (2× the 1-tick floor) to catch the rare 2–3 tick spread — marginal value. Recommend (i) + revisit post-WS. Don't manufacture a signal that isn't there at this cadence.

## Full-scope items unlocked by READY (beyond v33's partial)
With 3 regimes ≥50 and 3 session days, the v33-deferred and market-scale items are now reviewable:
- **`session_volume` multipliers** (all three buckets) — item A above is the priority.
- **`indicators.Volume` dynamic clamps** placement across ASIA/LONDON (v33 §3.5 confirmed NY unclamped; check other sessions).
- **Market-scale thresholds** v33 left untouched for lack of diversity: RSI bands, funding bands, OFI ratios, `verdict_*_pct` per regime — review only where multi-session data now shows a classifier stuck or mis-firing (trader-profile rule: don't re-open settled values without evidence).
- **Re-confirm the v33 three** (OBV 18.0 / CVD 0.05 / MicroCVD 0.30) on the larger multi-session sample — v33 set them from NY-only; verify the OBV median-split and CVD/MicroCVD FLAT shares hold across ASIA/LONDON. v33 §5 first-100 checks become full-sample checks here.
- **Partial-bar volume-starvation** (v33 §3.4 code finding): VolumeRatio samples the in-progress bar against a completed-bar SMA → HIGH fires ~1%. This is a **code-semantics spec**, not a multiplier knob — flag separately if confirmed across sessions.

## Step 0 (mandatory, as v33) — sample composition gate
Tabulate the ~930 rows by session bucket / regime / verdict tier / days before touching anything; re-baseline only what each cell can support (≥50). Known from today: ASIA 233, LONDON ~213, NY 488; ASIA regime mix is range-heavy. Confirm TRANSITIONAL / TRENDING_DOWN depth (were thin at v33) before touching regime-specific values.

## After v34 lands — auto-tweaker sequencing decision
The supervised auto-tweaker first fire is still pending (held since the correctness pass). **Recommendation: manual v34 first, then hand maintenance to the supervised tweaker.** The findings above (Asia settings-vs-accept philosophy, funding "fix-but-low-value," spread "accept REST-dead") need human calibration judgement the auto-tweaker can't supply — it tunes the failure-rate matrix, it doesn't reason about regime structure. Set the v34 baseline by hand, *then* let the supervised first fire (dry-run, trader watching, diff reviewed against v34 rationale) validate the loop and take over ongoing maintenance. Don't let an unproven first-fire debut on the foundational re-baseline.
