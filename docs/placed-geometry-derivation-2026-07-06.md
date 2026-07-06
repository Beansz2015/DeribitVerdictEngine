# Placed Geometry §6 Derivation — Results & Proposed Values

**Date:** 2026-07-06 (Fable seat). **Status:** DERIVED — awaiting trader review of §4 (values) + §5 (decisions DG1–DG5). B4b ships only after this tick.
**Parent:** `placed-geometry-structural-first-proposal.md` (APPROVED D1–D8, D3=clamp; §7 values were PROVISIONAL pending this pass).

## 1. Method & data

MFE/MAE forward walks on **1,869 directional verdict rows** (NY×1 = 1,024, LONDON×3 = 658, ASIA×3 = 187) from the v0.7 book, window **2026-06-22 00:00 → 07-03 10:11 UTC** — the v41-config-stable population (post-ASIA-recalibration, pre-v50). Fresh 1-min OHLC from Deribit public API (16,501 bars, gap-free). House conventions: T+3 execution floor, horizons T+15 (1-min) / T+45 (3-min), excursions in execution-resolution ATR units. Winners-MAE = max adverse **before** the target-hit bar (exclusive; same-bar ordering is unknowable).

**Caveats (bind every number):** rows are autocorrelated (30–60 s cadence — effective n is far below nominal; one violent hour contributes dozens of rows); excursions are **touch-based** (a printed high is not a guaranteed fill); the window is a single, strongly-trending fortnight (65k→59k→61k); and everything is **horizon-conditioned** — live autotrade positions in v1 have no exit management beyond TP/SL, so they can run past the measured horizon, which the walks cannot model. Values below deserve ~0.25-step precision claims, no finer.

## 2. Results (full tables in the session record; per-population summary)

```
            reach@1.25  @1.5   @1.75  @2.0 | MFE p50/p75 | winners-MAE p75/p90 | struct-tgt present/in-bound-reach | struct-stop p50 / <=2xATR
NY (1024)     66.7%    61.8%  56.8%  52.2% |  2.13/4.22  |  ~0.95 / ~2.1       |  36.5% / 64.0%                    |  9.19xATR / 1.0%
LONDON (658)  70.2%    67.3%  62.6%  61.1% |  3.49/5.14  |  1.63 / 2.35        |  34.5% / 33.3%  <-- inverted      |  4.17xATR / 2.7%
ASIA (187)    55.6%    49.7%  44.4%  40.6% |  1.47/2.46  |  ~0.77 / ~2.4-3.0   |  66.3% / 68.4%                    |  6.13xATR / 13.8%
```

BELOW_MIN_MOVE projection at the §4 values: NY ≈ 4–6% (interpolated between 12.5% @1.5 and 0% @2.0), LONDON 0%, ASIA 1.6%. Structural targets are **never** under the floor (0% in every population) — the ladder is gate-safe.

## 3. Findings

**F1 — The structural-STOP leg of D2 is inoperative at fixed sizing.** 5m swing-invalidation stops are an order of magnitude wider than the execution stop: NY p50 = **9.2×ATR** (~$370 at current ATR), LONDON 4.2, ASIA 6.1; within ANY sane bound they almost never qualify (≤2×ATR: NY 1.0%, LONDON 2.7%, ASIA 13.8%). Your discretionary style pairs structural stops with **stop-distance-scaled size** — v1 autotrade sizing is fixed, so the pairing doesn't exist yet. The honest v1 stop is therefore **`min(structural, k×ATR)` — structure only when tighter** (the target-cap philosophy applied to stops), with genuine structural stops deferred to the v2-era when sizing-by-distance exists. The SWING_STOP code path still ships and fires on the tight-structure minority (labeled honestly).

**F2 — The current 1.2×ATR stop does bleed winners, mostly in LONDON.** Winners-MAE p75: NY ~0.95 (1.2 survives ~78% of NY winners), but LONDON 1.63 — today's stop stops ~30% of eventual London winners before their target. The p90 design point from the spec recipe is **~2.2×ATR** globally — but at a 1.75 target that's a 0.8:1 fallback R:R, and touch-EV does not clearly favor it (fat tail vs doubled loss size). §4 recommends the balanced middle; the p90-per-recipe and keep-1.2 options are listed.

**F3 — The structural-first TARGET ladder is validated pooled, inverted in LONDON.** Within the 3.5×ATR bound, structural targets reach **64%** (NY) and **68%** (ASIA) vs their ATR fallbacks' 52%/41–56% — the D1 ladder earns its place. LONDON inverts: 33% in-bound structural reach vs 61% fallback (n=227, autocorrelated — not enough to carve an exception). Ship the ladder global, put LONDON on a §12 watch (trigger: in-bound structural reach still <45% after ≥3 more London session-days → London session override or bound tightening).

**F4 — Eval-barrier definition question (feeds D6).** The eval cache's barrier prices don't correspond to simple k×ATR distances in all rows (capped/floored barriers) — the ATR-yardstick eval and this raw-geometry derivation measure related but not identical things. One more reason the D6 eval migration onto the logged `Placed*` columns matters; no action this pass.

## 4. Proposed values (replaces §7 PROVISIONAL; trader review)

| Key | Current | **Recommended** | Alternatives |
|---|---|---|---|
| `scoring.atr_target_multiplier` (= fallback target) | 2.0 | **1.75** global (NY reach 56.8% — the 55–60% design point) | keep 2.0 (conservative; NY 52.2%) |
| session override — LONDON target | — | **2.0** (keep — already 61.1%; no change for London) | — |
| session override — ASIA target | — | **1.25** (55.6%; ASIA never reaches far targets) | 1.5 (49.7%) |
| `structural_levels.target_max_atr_mult` | 3.5 prov | **3.5 confirmed** (in-bound structural reach 66.7% pooled ≥ fallback) | — |
| `scoring.atr_stop_multiplier` (= fallback stop) | 1.2 | **1.6** global (≈ survives ~80–85% of winners pooled; covers LONDON's p75 1.63; fallback R:R 1.09) | 1.2 keep (R:R 1.46 at 1.75 target; bleeds ~30% of London winners) · 2.2 (the literal p90 recipe; R:R 0.80) |
| `structural_levels.stop_max_atr_mult` | 2.0 prov | **1.6 — deliberately = the fallback** (F1: stop becomes `min(structural, 1.6×ATR)`; struct places only when tighter) | 2.5 if you want a wider structural-stop window (still <7% of rows qualify) |
| `structural_levels.stop_min_floor_ticks` | 4 prov | **4 confirmed** (near-moot — struct stops are almost never tight) | — |
| `stop_too_loose_mode` | clamp (D3) | **clamp, unchanged** | — |

Display consequence to expect: the fallback ATR block reads R:R ≈ 1:1.1 instead of 1:1.7 (Kelly display shifts accordingly — advisory only). Gate consequence: BELOW_MIN_MOVE +4–6pp on NY, ~0 elsewhere (§2).

## 5. Decisions (trader — one tick unblocks B4b)

| # | Decision | Recommendation |
|---|---|---|
| DG1 | Amend D2's stop shape for v1: `min(structural, stop_max×ATR)` — structure only when tighter; true wide structural stops deferred until sizing-by-stop-distance exists (v2 era) | **Yes** (F1) |
| DG2 | Fallback stop 1.2 → **1.6** (vs keep-1.2 vs p90-2.2) | **1.6** |
| DG3 | Fallback target 2.0 → **1.75** global + LONDON 2.0 + ASIA 1.25 session overrides | **Yes** |
| DG4 | `target_max_atr_mult` 3.5 confirmed + **LONDON structural-target §12 watch** (trigger in F3) | **Yes** |
| DG5 | Re-derive on a calmer regime window before UN-bounding anything further (this fortnight was one-regime); the D6 eval migration inherits F4 | **Yes** (recorded, no action now) |

## 6. Next steps on tick

B4b implements against the parent spec §3/§7 with these values (the `ComputeSideLevels` seam from the v50 build is the single edit point); its own ⚠ boundary; **autotrade live-at-minimum-size unlocks only after B4b is live**. The B4b brief should also carry the `AtrSettings.StaticRef` POCO hygiene one-liner (115.0 → 38.0, the outlived v37 promise).
