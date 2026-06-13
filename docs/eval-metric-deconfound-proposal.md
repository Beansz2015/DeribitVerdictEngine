# Eval-Metric De-Confound — Absolute Favourable-Barrier Floor (Proposal)

**Date:** 2026-06-13
**Author:** Opus 4.8 (spec-author seat)
**Status:** **APPROVED in principle** — trader set the floor value (0.08% of price, ≈ $50 at $62k, sized to clear slippage). Analysis-layer only; **no scoring votes/thresholds/vetoes change** (not under the scoring-approval gate), but it re-bases every logged outcome and the metric the auto-tweaker optimizes, so it gets full spec + acceptance rigor.
**Implementer:** Opus, fresh conversation (cheap context). Verify anchors before editing.
**Origin:** the 2026-06-13 ATR-confound finding (`clean-data-rebaseline-v34-brief.md` post-v34 section): success rate inversely tracks ATR because the favourable barrier is `k×ATR`, so low-ATR targets are sub-tradeable moves tagged by noise (Asia <12 ATR = 86.8% "success" on ~26-point/0.04% targets).
**Settings:** the floor is now a shared, UI-editable key `scoring.min_tradeable_move_pct` (0.0008) defined in `min-tradeable-move-gate-proposal.md` — **AMENDED 2026-06-14**: source the floor from `cfg.Scoring.MinTradeableMovePct`, not the `AnalysisConstants.FavBarAbsFloorPct` const named below (keep that const only as the POCO-default mirror if convenient). Eval cache re-evaluates when the key changes (store the floor it was computed with; on load, if the live value differs, re-walk — self-healing, same pattern as the v2→v3 migration). Off the auto-tweaker surface. **Eval cache schema v2 → v3.**

> **2026-06-14 amendment:** this proposal pairs with `min-tradeable-move-gate-proposal.md` (the scoring gate that stops *emitting* sub-floor-target trades). Same 0.08% floor, one shared editable key. Read §5 of that doc for the interaction; everywhere this proposal says `FavBarAbsFloorPct` / `FAV_BAR_ABS_FLOOR_PCT`, read `cfg.Scoring.MinTradeableMovePct`. Implement the pair together (one v35 bump); both must land before the auto-tweaker first fire.

> **2026-06-14 design refinement (trader) — EXCLUDE gate-killed trades, don't re-score them as failures. This supersedes the "floor → low-ATR trades fail" mechanism for the gate-killed subset (§3, §5).**
>
> Re-scoring low-ATR trades as failures keeps them in the denominator, making the historical metric *pessimistic* vs the future gated regime (where the gate makes them NO TRADE, fully excluded). Closer fit: **the eval mirrors the gate** — a directional trade whose effective TP < floor (one the v35 gate would NO-TRADE) is reclassified `EXCLUDED_BELOW_MIN_MOVE` and removed from success/fail counts, exactly like a NO TRADE row. It is **not** a prediction failure — it's a trade the engine won't take. Net changes:
> - **Re-evaluation (§3):** for each historical *directional* entry, join `analysis_log.csv` (ATR + Price — the eval cache lacks ATR) and apply the gate condition `AtrTargetMultiplier × ATR < MinTradeableMovePct × Price` (the dominant low-ATR case; refine with the logged `SwingTarget`/`TargetCapReason` for the near-swing case where cheap — the log records the cap *reason* but not always the adjusted *value*, so HVN/POC-capped near-swing exclusions may be approximated; this affects only the historical re-base, future rows gate exactly). Gated → `EXCLUDED_BELOW_MIN_MOVE` (new NO-TRADE-equivalent outcome, out of counts). **Survivors** (TP ≥ floor) are still scored *with* the favourable-barrier floor (their "success" still requires a tradeable move).
> - **One-time forensic:** the migration reports how many historical directional trades get excluded and how many were `SUCCESS` under the old confounded barrier (the inflation the confound created) — capture once, then it's gone from the live metric.
> - **Acceptance (§5):** Asia <12 ATR trades should now mostly **EXCLUDE** (not appear as failures); the evaluated population becomes tradeable-ATR only, and its success rate is the **forward-comparable baseline** (matches what the gated engine will produce). High-ATR NY largely unchanged.
>
> Why: (a) historical and future rates measured identically (the trader's goal); (b) conceptually honest; (c) cleaner for the auto-tweaker — it optimizes only the book the engine will actually trade, instead of "fixing" failures the gate already removed. One floor condition drives both the gate and the eval exclusion.

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
