# Eval Cache — NO_DATA Outcome (F4 fix) · Proposal

**Date:** 2026-07-21 · **Status:** ✅ **BUILD-AUTHORIZED — N1–N5 ALL TICKED 2026-07-21** (see §3; header corrected 2026-08-01, it had read *"PROPOSED — D-table awaits trader"* since the tick, which made a build-ready item look like an outstanding decision) · **Priority: FIRST of the F-series** (correctness; every measurement inherits it — F1's re-read, W6 audits, Kelly CAL inputs).
**Evidence:** `offline-matrix-placed-target-spec-back.md` §8 F4 — `LivePerformanceTracker.EvaluateEntry` records an **empty bar-list as `WINDOW_EXPIRED` (a failure)** while `FailureRateMatrix.Compute` excludes the same condition from the denominator. Same condition, opposite handling: live rates bias **downward**, invisibly. Proven instance: 2026-07-03 NY = 22/22 fabricated expiries (backfill without OHLC coverage; other days 5–19%).
**Type:** eval/measurement only — zero scoring impact, no ⚠ boundary, no settings keys.

## 1. Change

1. **New outcome `NO_DATA`** returned by `EvaluateEntry` wherever the walk cannot run (`bars.Count = 0`, and the degenerate-barrier early-outs stay as they are). `TargetEverHit` stays `Nothing` on such rows.
2. **Aggregation excludes it**: `BuildWindowAggregate` treats `NO_DATA` like PENDING/EXCLUDED (not in numerator or denominator) — mirroring the offline side exactly. Tooltip's `TotalRange` still counts it (visible, not scored).
3. **Load-time reclassification sweep (the retroactive fix):** on first load under the new schema, every stored `WINDOW_EXPIRED` row is re-walked; rows whose bars are STILL unavailable (e.g. 07-03 — aged out of the 7-day OHLC cache) become `NO_DATA`; rows with coverage keep their genuine outcome from the fresh walk. One-time, gated by the eval-cache schema comment **v5 → v6** (`no-data outcome`), the D6 rotation pattern (old cache → `.v5.bak`).
4. **Provenance note:** the `.0000000Z` whole-second backfill signature (F8) goes into the eval-cache header comment as documentation — a free diagnostic for future audits.

## 2. Riders (same commit event)

- **R1 (F10):** add `tools/WhatIfRunner` to `verify-gate.ps1`'s build set — a project broken by #6 sat invisible for four days because the gate never builds it. One line; the gate's runtime cost is one extra small build.
- **R2 (F9, decision N4):** `RoundStatsBuilder` is the last synthetic-yardstick measurement (private `FavAtrThreshold = 0.5`). Recommendation: migrate it onto `ResolveFavourableBarrier`/placed geometry for consistency — OR document the fixed yardstick inline if the trader prefers round-stats stable across re-bases.

## 3. D-table

| # | Decision | Recommendation |
|---|---|---|
| **N1** | Outcome name + semantics | `NO_DATA`; excluded from both numerator and denominator; `TargetEverHit = Nothing` |
| **N2** | Retroactive shape | Load-time reclassification sweep + v6 schema comment + `.v5.bak` (fixes 07-03 permanently AND closes the asymmetry; a targeted re-backfill alone would leave the bug) |
| **N3** | Offline side | No change needed (already correct) — but `MarkdownReportWriter`'s §1 diagnostics gain a `No-data excl.` count so both surfaces disclose it |
| **N4** | RoundStatsBuilder (F9) | ✅ **TICKED 2026-07-21: migrate to placed geometry** |
| **N5** | Sequencing / model | Build NOW, before the F2/F3/F12 display pass (they read rates this fixes); **Opus, medium-low**; one conversation; spec-back `eval-no-data-outcome-spec-back.md` |

**N1–N5 ALL TICKED 2026-07-21 (N1–N3, N5 as recommended; N4 migrate) — BUILD-AUTHORIZED.**

## 4. Acceptance

Builds 0/0; harness unregressed + fixtures: empty-bars ⇒ `NO_DATA` (not WINDOW_EXPIRED); aggregation excludes it while TotalRange counts it; the v5→v6 sweep reclassifies a synthetic uncovered row and preserves a covered one; gate builds WhatIfRunner (R1 — break it deliberately in a scratch check if cheap, else assert the build set); round-stats change per N4. Live smoke: after rebuild+restart, the strip's rates move only by the removal of fabricated failures (07-03 aged out anyway; current windows span 07-18+ — expect ~no visible change, which is itself the check).
