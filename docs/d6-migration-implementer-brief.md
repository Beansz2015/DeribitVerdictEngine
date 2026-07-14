# D6 Migration — Implementer Brief (fresh conversation handoff)

**Date:** 2026-07-14 · **Status:** spec APPROVED, build authorized · **Model/effort: Opus, medium — one conversation.** Coordinator review after (Fable seat reachable through ~Jul 19).

## Orders
Implement `d6-eval-placed-stop-migration-proposal.md` (D1–D5 ticked 2026-07-14: **D1 = Replace**, D2 rotate, D3 legacy-labelled, D4 both-ways report, D5 any-gap timing). Evidence/context: `d6-eval-yardstick-divergence-2026-07-08.md` (incl. the 07-13 addendum — offline sites pinned there). Run the CLAUDE.md session-start protocol first.

## Scope (one principle: eval barriers = the placed levels, via the ONE seam)
1. **Live tracker** — `LivePerformanceTracker.BuildEntry` (~:929-935): FavBar/AdvBar stop deriving from `Adjusted*`/`SwingStop*` + the 2.0/1.2 consts; source them from `SignalEmitter.ComputeSideLevels` (pure; `v`, `r`, `cfg` are in scope at the call site). Delete `FAV_ATR_MULT`/`ADV_ATR_MULT` (:105-106). The walk itself is unchanged — the cache already stores FavBar/AdvBar per row.
2. **Cache rotation (D2)** — schema v4→v5 comment line; old cache → `.bak`; rebuild via the existing cold-start path. Note: rebuilt/backfilled entries must ALSO use placed barriers — v0.8 CSV rows carry `PlacedTargetLong/StopLong/TargetShort/StopShort` (cols 106-109); pre-v0.8 rows keep the legacy formula, tagged per D3.
3. **Offline** — `FailureRateMatrix.vb:158-170` + `AnalysisRunner.vb:241-245` (+ consts `AnalysisConstants.vb:28/:44`): use `Placed*` when present (ForwardWindowJoiner gains the 4 columns), legacy formula otherwise, **`LEGACY_YARDSTICK` population label** in the report — no silent mixing. Per-tier favourable ATR grid is OUT of scope.
4. **D4 report** — both-ways re-walk of the same post-v51 rows (old vs placed barriers, per session×resolution×tier); ships with the commit.
5. Auto-tweaker: no code change; note in the spec-back that first-fire gate readings re-base.

## Constraints (standing rules bind)
- **Zero scoring impact** — verdict pipeline untouched; no settings-version bump unless a key is added (none expected; change_log note + §15 row still required, "eval-semantics boundary" wording).
- **Local-first, NEVER push**; `tools/checks/verify-gate.ps1 -Mode prepush` green before done; collector runs the Debug exe — **Release-only builds while it runs**.
- Display parity: perf-strip is a status element (no card/snapshot obligation), but state this in the commit message.
- Expected effect (sanity): failure rates RISE materially (the 1.6×ATR stop starts binding); [B]/[T] strip modes diverge again (they are currently identical — diagnosed 2026-07-13, handover §4 B4b row).
- Write a **spec-back** (`d6-migration-spec-back.md`) with every deviation; coordinator review checklist = handover §5.

## Acceptance
Builds 0/0; harness unregressed + new fixtures: (a) tracker barriers ≡ `ComputeSideLevels` outputs on capped/fallback/noise-suppressed cases, (b) offline Placed*-vs-legacy row routing, (c) D4 report renders both populations, (d) v5 rotation + rebuild path. Serialization note: eval cache writes booleans as 0/1.
