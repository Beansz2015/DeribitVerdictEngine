# #6 Book Absorption — Implementer Brief (fresh conversation handoff)

**Date:** 2026-07-16 · **Status:** build-authorized pending ONE gate — **do not start until the v52+v53 joint watch has passed** (2 weekday sessions post-07-15; expected readable ~Fri 07-17 evening; the trader or coordinator confirms). · **Model/effort: Opus, HIGH — the hardest remaining build.** Budget a careful spec-back; coordinator review after.
**Spec:** `book-absorption-proposal.md` — **D1–D8 ALL TICKED 2026-07-03** (incl. D8 pull-fraction spoof guard). The proposal is the mechanism authority; this brief adds only sequencing + acceptance context. Run the CLAUDE.md session-start protocol first.

## Orders
Implement the **build sub-version only**: `scoring_enabled:false` — display/CSV, ZERO scoring. Activation is a LATER, separately-gated ⚠ boundary (evidence-gated twice: #5-rule independence AND ≥10pp conditional-outcome gradient on n≥30 flagged evaluated rows — not your concern now).

## Scope (per the proposal — defer to it on every detail)
- Level-scoped episode tracker on the nearest CARRIED levels (the TAPE strip's candidate set: swing5m + VPFR HVN); `absorbRatio` = pressing USD per USD net band depletion on the 100ms book snapshots; **dual-fed (book + trades) under the `MarketState` lock — the first of its kind; this is where the review will focus.** D8: per-interval conservation ΔSize = Posts − Pulls − Fills → `pullFrac` spoof veto (`max_pull_frac`, ON-surface).
- **Rotation-free:** the 5 CSV columns (`AbsorptionSignal/Level/Ratio/AggrUsd/PullFrac`) shipped RESERVED-empty at v0.8 — populate them, no header rotation, no `.bak`.
- Settings: new `indicators.absorption` block per proposal §6 (sub-version bump; tweaker tiers per the proposal — check `SettingsDiffApplier`/PromptBuilder needs; HC ledger next free = **HC23**).
- Display: TAPE-strip enrichment only if the proposal says so — strip-only surface, no card/snapshot obligation (#3/#5 precedent); otherwise state no-display in the commit.

## Constraints
Local-first, NEVER push; `verify-gate.ps1 -Mode prepush` green; **collector runs the Debug exe — Release-only builds**; spec-back (`book-absorption-spec-back.md`) with every deviation; `git status` before starting (no other lane should be open — the wire-in/D6/funding lanes are all closed and pushed). Thread-safety note for the reviewer: every fold happens inside the existing `MarketState` `SyncLock` discipline (the OFI/AggrVel accumulator precedents — follow their reset-on-`SeedAsync` + warmup-gate patterns).

## Acceptance
Builds 0/0; A1–A29e unregressed + new fixture family (**A30**): episode lifecycle (open/accumulate/close on level change), absorbRatio math vs an analytic case, D8 conservation-bound + `pullFrac` veto, warmup/reset re-arm, WS-only/REST-inert, reserved-column population (empty ⇒ values, no rotation), tweaker fences. Cold/degenerate inputs never throw.
