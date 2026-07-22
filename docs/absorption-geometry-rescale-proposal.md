# Absorption Geometry Rescale (ATR-Fraction Distances) · Proposal

**Date:** 2026-07-23 · **Status:** APPROVED — trader agreed to the coordinator ruling 2026-07-23; V-table values below are the remaining ticks · **Type:** display/CSV-only signal revision (`scoring_enabled` stays false) — NO ⚠ boundary. **Evidence:** `absorption-engagement-derivation-2026-07-23.md` + the map ruling: 0% engagement because the tick-scaled geometry (band 4t=$2, proximity 12t=$6 vs ATR≈$44) measures a shell almost nothing prints in; all three anchors bind at 100%; loosest re-anchor caps at 1.6% vs the 3–8% design band. Units verified correct (same USD field AggrVel validated).

## 1. Change — distances become ATR-fractions (session-resolved ATR, the execution-resolution ATR)

Replace the three tick-scaled keys with ATR-fraction keys (retired keys removed — applier-unresolvable, the v53 pattern; fragments NOT added to RejectedPathFragments, the v47-F1 lesson): `proximity_atr_frac` (**0.30** ≈ $13 at ATR 44), `band_atr_frac` (**0.10** ≈ $4.4), `break_tol_atr_frac` (**0.05** ≈ $2.2). Resolved per run from `r.ATR` at the SetAbsorptionLevels carry site (the ATR is execution-resolution by construction); the tracker keeps working in absolute dollars internally — only the config→dollars conversion moves. Floors rescale to the widened shell as PROVISIONAL anchors pending the post-rescale §5 re-derivation: `depletion_floor_usd` 25000→**5000**, `default.min_aggr_usd` 150000→**20000**, `absorb_ratio` 3.0→**1.5**, `max_pull_frac` 0.5→**0.75** (the derivation showed 0.5 vetoing 50–83% at tiny volumes where the lower-bound ratio is noise). D8 conservation/masking unchanged (follows the new band). Settings v60→v61 + POCO + change_log; HC23 fences carry over to the new key names.

## 2. Retro-filter question (trader Q, answered)

**Not possible.** The tracker folds ~100ms book snapshots + per-trade stream at fold time; neither is persisted — the CSV carries only the per-run episode summary under OLD geometry, and episodes that never opened (price within $6 of a level is rare) left no record at all. Re-collection under honest geometry is unavoidable: **~1–1.5 weekday-weeks → §5 engagement re-derivation (recipe = the 07-23 doc, re-run) → then the activation gates** (~mid-Aug, the Opus month).

## 3. V-table

| # | Decision | Recommendation |
|---|---|---|
| **V1** | Fractions 0.30/0.10/0.05 + rescaled provisional floors (§1) | As stated — all PROVISIONAL until the post-rescale re-derivation |
| **V2** | Retire tick keys (unresolvable) vs keep-with-null | Retire (v53 pattern) |
| **V3** | Fixtures | Re-pin A31 family to fraction-resolved dollars; add: fraction→dollar resolution at two ATRs (the scale-invariance point) |
| **V4** | Slot / model | **Opus, medium, NOW** (restarts the collection clock soonest); spec-back `absorption-geometry-rescale-spec-back.md`; §15 row; v61 |
