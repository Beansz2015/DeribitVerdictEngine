# #6 Book Absorption — Build Spec-Back

**Date:** 2026-07-17 · **Settings:** v53 → **v54** · **Scope:** the BUILD sub-version only (`scoring_enabled:false` — display/CSV, zero scoring), per `book-absorption-implementer-brief.md`. Spec of record: `book-absorption-proposal.md` (D1–D8 all ticked 2026-07-03). Gate: the joint v52+v53 watch PASS (`c8ed6c6`, 2026-07-16) green-lit the build.

Everything below is either a confirmation that the build matches the proposal, or a **deviation/decision** the proposal did not pin. Deviations are numbered for the coordinator review.

## 1. What shipped (map to proposal §11)

| Proposal §11 item | Shipped |
|---|---|
| `Core/LevelAbsorptionTracker.vb` | ✅ new — episode state machine + rolling pressing window + D8 conservation accumulators w/ visibility mask + `Snapshot(nowMs, cfg)`; host-agnostic, no WinForms |
| `Indicators_OrderFlow.vb` pure `ClassifyAbsorption` | ✅ (signature deviation — §2.1) |
| `MarketState.vb` — owns tracker under its lock; fold hooks; reset on seed; carried-levels setter | ✅ `SetAbsorptionLevels` / `FoldAbsorptionBook` / `FoldAbsorptionTrade` / `ResetAbsorption` / `GetAbsorption`, all under the ONE existing `_lock` |
| `DeribitWsFeed.vb` — the two folds | ✅ `ApplyBook` → `FoldAbsorptionBook` (post-`FoldOfiAverage`, receive-time stamp — the OFI-fold basis); `ApplyTrades` → per-trade `FoldAbsorptionTrade` (trade's own exchange stamp); cfg read per batch, `enabled:false` ⇒ zero feed work; `SeedAsync` → `ResetAbsorption` |
| `UI/MainForm_Analysis.vb` read path | ✅ WS-live gate identical to #5 (`enabled AndAlso src Is _wsSource AndAlso _marketState IsNot Nothing`); REST/fallback ⇒ NONE + nulls. Carried levels set at the `_lastSuccessfulIndicators` capture site (the strip's carry, §11 as written) |
| `Core/IndicatorResults.vb` 5 fields | ✅ `AbsorptionSignal` ("NONE" default) + 4 nullable numerics |
| `LiveMicrostructureEvaluator.vb` ABS tag | ✅ + `UI/MainForm_LiveStrip.vb` composer — tag renders ONLY while a state is active (D6) |
| `AnalysisLogger.vb` 5 columns | ✅ populates the v0.8 RESERVED columns — **rotation-free, header byte-identical, no `.bak`** (D4 held) |
| `EngineSettings.vb` + `settings.json` | ✅ block per §6 verbatim; v54 + change_log + §15 |
| `tools/AutoTweaker/` fences | ✅ exact-match `enabled`/`scoring_enabled` + `default.`/`sessions.` prefixes; **HC23** (next free per the brief) in SettingsDiffApplier + PromptBuilder |
| `verify/ordercheck/` fixtures | ✅ new family (numbering deviation — §2.2) |

## 2. Deviations & decisions

**2.1 `ClassifyAbsorption` returns a struct, and takes resolved scalars.** The proposal wrote `ClassifyAbsorption(snapshot, cfg)` returning the state. Shipped: `ClassifyAbsorption(snap, minAggrUsd, absorbRatioThreshold, maxPullFrac) As AbsorptionRead` — (a) scalars, because `min_aggr_usd` is session-resolved (`ExecutionResolution.ResolveAbsorptionMinAggrUsd`, the v40 chain) and cannot come from the raw cfg block alone; this mirrors `ClassifyAggressorBurst`, the named precedent. (b) A struct return (`Signal` + `HasEpisode` + the primary episode's `Level/Ratio/AggrUsd/PullFrac`), because D8 requires `AbsorptionPullFrac` logged **even on vetoed episodes** — one pure call yields both the classification and the CSV numerics, keeping run path and strip on identical logic.

**2.2 Fixture family is A31, not A30.** The brief said "new fixture family (A30)", but A30a–d was taken by the What-If runner (built 07-16, after the brief was written). A31a–g shipped.

**2.3 Both-sides-qualify tie.** With tight bracketing levels both sides can hold active, qualifying episodes simultaneously (proposal silent). Shipped: the higher `absorbRatio` wins the signal and the numerics. When neither qualifies but episodes are active, the larger `aggrUsd` side supplies the CSV numerics.

**2.4 Level selection and the ACTIVE gate, made concrete.** Nearest candidate **≥ best ask** (ABOVE) / **≤ best bid** (BELOW) from the four carried fields, additionally required inside the visible ladder span — the §8 `min(proximity, visible)` rule enforced by construction (a level the top-10 ladder can't see is never selected). Gate: `level − bestAsk ≤ proximity` (mirror below). The episode opens on the first in-proximity **book** fold (sizeStart needs a ladder), not on a trade.

**2.5 Break-through re-arm requires leaving proximity.** The proposal says break ⇒ state cleared instantly and "re-arms on the next approach". Shipped: after a break the side parks idle (`BrokenLevel`) until price leaves the proximity band **or** the level re-maps — otherwise a print into the `break_tol` zone would clear and instantly re-open a fresh episode at the broken price on the next snapshot, laundering the break. A31d pins the sequence.

**2.6 Fills are masked like ΔSize.** The proposal's visibility-mask note covers ΔSize; shipped masks the interval's fills to the same `[maskLo, maskHi]` range — the conservation identity only holds over a common price range, and an unmasked fill against a masked ΔSize would fabricate pulls exactly when the ladder window shifts.

**2.7 Progress test also reads the opposing touch.** Break fires on a trade print beyond `level ± break_tol` (per the proposal) **and** on the opposing touch trading through it (best bid > level + tol for ABOVE) at book folds — a gap-through with no band print must still clear the state ("a broken level must never carry a stale ABSORB reading").

**2.8 Pressing window is an exact timestamped queue.** "Rolling `window_sec` sum" is implemented literally (queue + running sum, pruned at fold and read), not as an EMA — the aggr threshold is an absolute USD level, so a decayed sum would change its meaning. Defensive cap 4096 entries.

**2.9 pullFrac denominator floor.** `pullFrac = pullLB / max(postLB, depletion_floor_usd)` — §4.2's formula verbatim (the floor is shared with the ratio denominator; no separate key).

**2.10 Conservation interval alignment.** The D8 fold runs only for an episode that was already active at the previous book fold — the opening interval (which spans pre-episode time) is skipped, so cross-boundary noise can't seed `pullLB`. Fills accumulate only while active, so intervals align by construction; fill-to-interval assignment is by timestamp arrival order between book folds (the proposal's "episode-aggregated bounds tolerate the 100 ms jitter").

**2.11 Strip tag glyph.** `ABS↑ 60510 (3.4×)` — the proposal wrote `(3.4x)`; shipped uses `×` to match the strip's existing burst/imbalance convention. Cosmetic; strip-only surface.

**2.12 `AnalysisLogger.vb` linked into the harness.** For A31f (reserved-column population as the real shipped `LogRun`, in the harness bin dir, cleaned up after). New link in `OrderCheck.vbproj` alongside the tracker.

**2.13 WS-only/REST-inert harness boundary.** The run-path WS gate is WinForms-side and stays out of the harness (the A16–A23 boundary). REST-inertness is pinned at the surfaces the harness can reach: nothing folds the tracker off the WS feed by construction, the cold tracker reads NONE/null (A31e), and the null-row CSV shape — exactly what a REST run logs — is A31f's second row.

**2.14 Display-string parity statement.** No snapshot or card line is added, removed, or renamed — the only display surface is the TAPE strip tag (live status element, the #3/#5 precedent). Stated here and in the commit message per the parity rule.

**2.15 Manuals untouched.** UserManual/TraderGuide gain no obligation from a strip-only build surface; the ABS tag + CSV columns should fold into the next manual refresh (flagged for that pass, alongside the pending What-If manual).

## 3. Acceptance record

- **Builds:** solution (Release) + AutoTweaker + OrderCheck — **0 warnings / 0 errors**. Release-only throughout (the collector runs the Debug exe — never touched).
- **Harness:** ALL PASS — A1–A30d unregressed, new **A31a–g**: lifecycle (open / leave-proximity close / re-map reset onto the new level), analytic absorbRatio case (aggr 210 000, depletion 60 000 → ratio 3.5; sitting defender pullFrac 0 → ABSORB_ABOVE; threshold edges NONE), D8 churn (pullLB 120 000 / postLB 60 000 → pullFrac 2.0 → veto with ratio 3.5 that would otherwise fire; vetoed pullFrac surfaced), break-through instant-NONE + parked-idle + re-arm, reset/cold/degenerate never-throw (Nothing book / empty ladder / Nothing cfg / zero-price trades), reserved-column population (values row + REST-shape null row, header unrotated, no `.bak`), session min_aggr_usd resolution + HC23 fences (switches + tiers rejected, flat siblings accepted).
- **verify-gate:** `-Mode prepush` green (run at commit).
- **Reversibility:** `enabled:false` ⇒ feed does zero absorption work, tracker never folds, reads return NONE/null, CSV logs the same empty shape as pre-build — byte-identical at every reachable surface.

## 4. Open items (not this build's scope)

- **§5 gates** run on the post-build collection (episodes join the eval cache via the standing W1 audit instrument): 5.1 independence (|Spearman| < 0.7 vs OFIRatio/burstRatio/TFI, fire-overlap < 80%) AND 5.2 outcome gradient (≥10 pp worse success on n≥30 flagged evaluated rows). Both reported with the go/no-go recorded either way.
- **Calibration on activation:** target-engagement design (3–8% of directional runs; per-session `min_aggr_usd` overrides expected).
- **Activation sub-version** (its own ⚠ boundary): Step-2 penalty wire-in + `Absorption` breakdown row (snapshot + card in the same commit) + §12 watch row.
- **W4 trigger evidence:** the logged `pullFrac` distribution is the incremental-book fidelity-binds measurement — accrues passively from here.
