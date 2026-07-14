# #5 Aggressor-Velocity Scoring Wire-in — Implementer Brief (fresh conversation handoff)

**Date:** 2026-07-14 · **Status:** build-authorized (S1–S5 ticked: S1 NY threshold **4.5**, S2 option (a), S3 magnitudes 1/1, S4 own ⚠ boundary, S5 HC22 rider) · **Model/effort: Opus, medium — one conversation.** Coordinator review after.
**⚠ This is a scoring change** — its own dataset boundary; the funding time-anchored build queues AFTER it (handover Q3/D4). Run the CLAUDE.md session-start protocol first.

## Specs (read in order)
`aggressor-velocity-proposal.md` §4.5 (modifier mechanics, confirmed §10.1) + §6 (config) · `aggressor-velocity-s52-derivation-2026-07-13.md` (values + S-table) · gate verdict `aggressor-velocity-correlation-gate-verdict-2026-07-13.md` (context).

## Scope
1. **Settings (v52 bump + change_log dataset note + §15 row):** `indicators.aggressor_velocity.scoring_enabled` → `true`; `sessions.NY.burst_ratio_threshold: 4.5`. The `default` 2.5 stays (res-3 display/collection unchanged — S2a).
2. **Step-2 TFI modifier** (`ScoringEngine_Calculate_Scoring.vb`), per §4.5, appears ONCE: TFI directional vote + `BURST_*` same side → `+upgrade_bonus` (1) on that side, capped at regimeMax; `BURST_*` contra → `−contra_penalty` (1) soften; NORMAL → no-op. **S2(a) scoping:** the modifier applies only when the run's session has an explicit `sessions[s].burst_ratio_threshold` (NY today); classifier/display/CSV keep running everywhere. `scoring_enabled:false` ⇒ byte-identical current behaviour (harness-proven, the rollback).
3. **Breakdown note:** append a burst suffix to the existing TFI note row (OFI `MOM:` precedent — no line added/renamed ⇒ no card change; state this in the commit message).
4. **S5 rider:** exact-match tweaker fence for `session_volume.enabled` → `SettingsDiffApplier.Validate` + PromptBuilder **HARD CONSTRAINT 22** + fixture (HC16 pattern; siblings unaffected).
5. **Post-ship watch (name in change_log + §12):** NY burst fire rate 8–12% + same-side ≥85% over the first 2 weekday sessions; TFI-modifier engagement ≈ 5–10% of NY directional votes.

## Constraints
Local-first, NEVER push; `verify-gate.ps1 -Mode prepush` green; Release-only builds while the collector runs; spec-back (`aggr-vel-wirein-spec-back.md`) with every deviation; POCO defaults ride the commit. **Serialize vs the D6 migration lane** — both add fixtures to `verify/ordercheck/Program.vb`; do not run concurrently (wire-in first is fine; D6 is any-gap).

## Acceptance
Builds 0/0; A1–A26g unregressed + new fixtures: (a) upgrade/soften/no-op through the real `Calculate()`, (b) regimeMax cap holds, (c) S2a scoping — res-3 session with no explicit threshold ⇒ modifier inert, (d) `scoring_enabled:false` byte-identical, (e) HC22 fence rejects `session_volume.enabled`, accepts a sibling tunable.
