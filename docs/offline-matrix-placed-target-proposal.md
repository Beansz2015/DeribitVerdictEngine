# Offline Matrix — Placed-Target Migration (completing what D6 started)

**Date:** 2026-07-17 · **Status:** **APPROVED — M1–M5 ALL TICKED 2026-07-18.** #6 landed (`ae6678c`), so the M5 sequencing gate is satisfied — **buildable immediately** (roadmap-upgraded to next-build: this is the p-instrument for W6-3 Kelly CAL). **Implementer orders:** Opus, medium, one conversation; spec-back `offline-matrix-placed-target-spec-back.md`; §2 is the touchpoint inventory, §5 the acceptance; check `git status` before starting — the what-if manual fold-in lane may hold uncommitted manual/PDF work (no shared files, but serialize at commit boundaries; the trader intends to push that lane first). · **Type:** offline-analysis semantics — zero scoring impact, no ⚠ boundary, no settings keys.
**Evidence:** the 2026-07-17 Analysis Report — the per-tier ATR grid ({0.5,0.8}/{0.3,0.5}) sits entirely below the $51 min-move floor at ATR≈44, so **every grid column is identical by construction** (the grid was anchored at ATR≈115 and is degenerate at current vol). Plus the standing inconsistency: post-D6 the offline matrix is the lone hybrid (real placed stop, synthetic target grid) while the live tracker, D4, and the what-if runner all measure placed-vs-placed.

## 1. Change

`FailureRateMatrix` (and everything downstream) scores each row **placed target vs placed stop** — the logged `PlacedTarget*`/`PlacedStop*` (v0.8+; pre-v0.8 rows keep the full legacy formula, `LEGACY_YARDSTICK`-labelled, both sides). The **window dimension survives** (5/10/15 resolution-scaled — the hold-horizon question is geometry-independent); the **threshold columns collapse to one** placed-geometry column per (tier × session × window). `StrongAtrThresholds`/`MediumAtrThresholds` retire (threshold sweeping is the what-if runner's job now, done properly with EV + holdout).

## 2. Touchpoints (the honest inventory)

- `analysis/AnalysisConstants.vb` — the two threshold arrays retire; `HoldWindowsForResolution` stays.
- `analysis/FailureRateMatrix.vb` — favourable barrier = placed target (the D6 `ResolveAdverseBarrier` pattern mirrored as `ResolveFavourableBarrier`); cell space = (window) per tier.
- **Auto-tweaker cell-space:** `AutoTweakerCore` picks `(window, threshold)` → becomes `(window)`. The >40%-failure trigger semantics are unchanged (it reads the aggregate rate); the picked-cell history gains a schema note (old rows carry a threshold field, new rows don't — parse-tolerant, no rotation). MinTier logic untouched.
- `MarkdownReportWriter` — §2/§3/§4a/§8 lose the threshold column; **D4 collapses to one before→after column**; same pass fixes the two stale-text items: the Funding Momentum Diagnostic's canned recommendation (still says "defer to WebSocket migration" — two eras stale) and the section numbering (2/D6/3/4a/4/8/9 → sequential).
- What-if runner: unaffected (its matrix calls inherit the new favourable side; its *overlay* geometry already replays placed levels).
- Live tracker/eval cache: **unaffected** (already fully placed-vs-placed since D6).

## 3. What is deliberately lost

The "movement quality at k×ATR regardless of target" lens. Recorded as accepted: it was the pre-placed-geometry proxy for a question ("is this tier's directional call good for at least X?") that EV-on-executed-geometry answers better, and the grid instantiation of it is degenerate at current vol anyway. If a fixed-yardstick lens is ever wanted again, it's a one-line what-if overlay, not a standing report axis.

## 4. D-table

| # | Decision | Recommendation |
|---|---|---|
| **M1** | Favourable barrier | Logged `PlacedTarget*` (v0.8+); legacy formula + label pre-v0.8 |
| **M2** | Cell space | (tier × session × window), single geometry column; windows unchanged |
| **M3** | Tweaker pick-space | (window) only; trigger semantics unchanged; picked-cell history parse-tolerant, no rotation |
| **M4** | Riders | Funding-diagnostic text refresh + section renumbering (same file) |
| **M5** | Sequencing / model | **After #6** (same shared files/fixture class); Opus, medium; no boundary; dated change_log-style note in the report header ("matrix re-based 2026-07-xx: placed-vs-placed") |

## 5. Acceptance

Builds 0/0; harness unregressed + fixtures: placed-favourable routing (v0.8 vs legacy row), tweaker window-only pick + old-history parse, D4 single-column render, floored-grid impossibility (a synthetic low-ATR row shows distinct outcomes, not column collapse — the 2026-07-17 bug can't recur). Report regeneration on the live book sanity-checked against the live perf strip's [B] rates for the same span (they now measure the same thing and should agree within window/precision differences).
