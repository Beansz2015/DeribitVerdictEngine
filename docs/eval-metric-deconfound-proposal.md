# Eval-Metric De-Confound — Absolute Favourable-Barrier Floor (Proposal)

**Date:** 2026-06-13
**Author:** Opus 4.8 (spec-author seat)
**Status:** **APPROVED in principle** — trader set the floor value (0.08% of price, ≈ $50 at $62k, sized to clear slippage). Analysis-layer only; **no scoring votes/thresholds/vetoes change** (not under the scoring-approval gate), but it re-bases every logged outcome and the metric the auto-tweaker optimizes, so it gets full spec + acceptance rigor.
**Implementer:** Opus, fresh conversation (cheap context). Verify anchors before editing.
**Origin:** the 2026-06-13 ATR-confound finding (`clean-data-rebaseline-v34-brief.md` post-v34 section): success rate inversely tracks ATR because the favourable barrier is `k×ATR`, so low-ATR targets are sub-tradeable moves tagged by noise (Asia <12 ATR = 86.8% "success" on ~26-point/0.04% targets).
**Settings:** none (measurement constant in `AnalysisConstants`, deliberately off the tweaker surface). **Eval cache schema v2 → v3.**

---

## 0. Goal

A trade only counts as reaching the take-profit target if price moved **at least 0.08% from entry** (the minimum move worth taking after slippage). Below that, the favourable barrier is non-tradeable and a "hit" is noise. Implement as an absolute floor on the favourable-barrier distance, applied everywhere the favourable barrier is derived from ATR — so the failure-rate matrix (auto-tweaker's input) and the live perf strip both stop rewarding low-ATR chop.

## 1. The floor

```
favourableDistance = max(k × ATR, FAV_BAR_ABS_FLOOR_PCT × entryPrice)
```
- `FAV_BAR_ABS_FLOOR_PCT = 0.0008` (0.08%). New const in `analysis/AnalysisConstants.vb`, beside `AdverseFallbackAtrMultiplier`. Price-relative so it tracks BTC (≈ $50 at $62k, ≈ $72 at $90k) with no recalibration.
- `k` = the existing per-tier favourable multiples (`StrongAtrThresholds {0.5, 0.8}`, `MediumAtrThresholds {0.3, 0.5}`). The floor applies to **each** threshold.
- **Adverse barrier unchanged** (`1.2 × ATR` fallback / structural). The asymmetry is intentional — see §4.

**Where it binds (at $62k):** `0.8×ATR ≥ 0.0008×62000` needs ATR ≥ 62; `0.5×ATR` needs ATR ≥ 99; `0.3×ATR` needs ATR ≥ 165. So in the current low-vol regime the floor **dominates** the favourable barrier for most rows (Asia ATR ~13, NY ~68). That is the intended large correction — the trader and the tweaker have been reading a metric that mostly measured sub-tradeable targets. Expect the failure-rate matrix to shift materially.

## 2. Files & changes

| File | Change |
|---|---|
| `analysis/AnalysisConstants.vb` | Add `Public Const FavBarAbsFloorPct As Double = 0.0008` + comment (price-relative; sized to clear slippage; floors the favourable barrier so "success" means a tradeable move). |
| `analysis/FailureRateMatrix.vb` | Wherever the favourable barrier price is computed from `threshold × ATR` for the barrier walk (the `TargetHitWalk` / barrier-walk paths), apply `max(thresholdDist, FavBarAbsFloorPct × entryPrice)`. This is the auto-tweaker's metric — primary target. |
| `LivePerformanceTracker.vb` | Same floor wherever FavBar is computed from ATR for the eval cache + perf strip (`[B]`/`[T]`). Consume `AnalysisConstants.FavBarAbsFloorPct` (verify the file already references `AnalysisConstants`; if not, add the reference rather than duplicating the const). |

Both consumers must use the one shared const so they never drift.

## 3. Eval cache re-evaluation (schema v2 → v3)

Existing entries were walked with un-floored barriers, so history must re-base or the tweaker's first fire still sees the confound.

- Bump the eval-cache schema marker v2 → v3 (mirror the v28 v1→v2 `TargetEverHit` migration pattern). Detect a v2 file by the absence of the v3 marker; on first load, re-evaluate every matured entry.
- **Re-flooring needs no ATR:** entries store `EntryPrice` and `FavBar` (a price). New favourable distance = `max(|FavBar − EntryPrice|, FavBarAbsFloorPct × EntryPrice)`; rebuild `FavBar` on the correct side (long: `EntryPrice + dist`; short: `EntryPrice − dist`). Re-walk the OHLC cache (`ohlc_1m_cache.csv` — real OHLC, Volume=0 doesn't matter for the price walk) over the existing hold windows; rewrite `EvalOutcome` + `TargetEverHit`. `AdvBar` unchanged.
- If an entry's OHLC span is missing from the cache, mark it `PENDING`/excluded rather than guessing (don't fabricate outcomes).

## 4. Interactions (all intended — state them, don't fix them)

1. **Adverse left un-floored** ⇒ in low ATR the floored favourable (≈50pt) now sits *outside* a tight adverse (≈15pt at ATR 13), so low-ATR trades will mostly hit adverse first or expire → success collapses toward reality. That is the de-confound working. (A symmetric adverse floor is a separate question — §6.)
2. **`[T]` vs `[B]` may now diverge in low ATR** (they're identical today): `[T]` (target ever hit, ignoring adverse) can still register the floored target on a later spike, while `[B]` (favourable *before* adverse) fails when the tight adverse triggers first. The gap is itself informative ("eventually reached a tradeable move but would've been stopped first") — keep both.
3. **Failure-rate-matrix threshold resolution collapses in low ATR** — all sub-floor `k×ATR` thresholds map to the same floored barrier, so the matrix differentiates mainly on the **window** dimension (5/10/15 min: "how long to reach a tradeable move"). Correct: sub-floor thresholds were never tradeable and shouldn't be distinguished. The tweaker now optimizes a meaningful surface.

## 5. Acceptance

Re-run the diagnostic that found the confound, post-floor:
- **Asia <12 ATR success must drop sharply** from 86.8% (the floored ~50pt target is unreachable at ATR ~13 inside the window). A realistic post-floor Asia <12 success in the teens–low-double-digits is the pass signal.
- **High-ATR NY (ATR ≥ ~62) roughly unchanged** where the floor doesn't bind; only its lower-ATR rows move.
- **Within-session monotonicity flips or flattens:** success should stop *rising* as ATR falls. If low-ATR still scores highest, the floor isn't applied on a path the metric reads.
- Re-evaluation completes over the existing ~1078 matured entries without fabricated outcomes; PENDING count sane.
- `dotnet build` clean. No settings.json change. No scoring CSV change (this is eval-cache + matrix only).

Provide the before/after Asia-ATR-bucket table in the spec-back.

## 6. Out of scope (separate decisions)

- **Absolute-ATR *trade gate* (scoring, candidate #2):** suppress/downgrade verdicts when the engine's own target can't clear 0.08% (at $62k, ~ATR < 25 for targetMult 2.0). This is the scoring follow-on the de-confounded data will justify — **defer until this metric fix lands and confirms low-ATR trades fail**, then spec it with evidence. It's a large behaviour change (much of the current low-vol book) → spec-first, approval-gated, trader sets the gate.
- **Symmetric adverse-barrier floor:** only affects failure *attribution* (expiry vs adverse), not the success *rate*, so secondary. Leave adverse un-floored per the trader's TP-only framing; revisit if the [T]/[B] gap (§4.2) proves noisy.
- **Profile ATR-band recalibration** (Low<80 → for $62k): display/profile housekeeping, independent.

## 7. Routing

Opus, fresh conversation, this doc as kickoff. Local commits only; the trader reviews the before/after acceptance table and pushes. **This should land before the supervised auto-tweaker first fire** — otherwise the tweaker inherits the confound (the first-fire caution in the v34 brief stands until this ships).
