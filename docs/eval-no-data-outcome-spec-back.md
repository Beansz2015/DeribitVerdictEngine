## Eval Cache — NO_DATA Outcome (F4 fix) · Spec-Back

**Built:** 2026-07-21 · **Spec:** `eval-no-data-outcome-proposal.md` (N1–N5 TICKED 2026-07-21).
**Type:** eval/measurement layer — zero scoring impact, no ⚠ dataset boundary, no settings-version bump (stays v54).
**State:** local commit; solution + AutoTweaker + WhatIfRunner + OrderCheck build 0/0 Release; harness ALL PASS (165 checks, +3 vs pre-F4) incl. new A33a–c; verify-gate `prepush` GATE PASSED.

---

## 1. What was built

The eval-cache classifier gained a distinct **NO_DATA** outcome for the "walk cannot run" branch of `LivePerformanceTracker.EvaluateEntry` (empty bar-list at evaluation time). The offline matrix already excluded that condition from its denominator; the live tracker had folded it into `WINDOW_EXPIRED`, biasing every rate downward and invisibly (F4 evidence: the 2026-07-03 NY slice reads 22/22 fabricated expiries — the backfill day with no OHLC coverage). Both surfaces now handle the same condition the same way: excluded from both numerator and denominator.

A one-time **v5 → v6 reclassification sweep** re-walks every stored `WINDOW_EXPIRED` row on first load: bars still absent become `NO_DATA`; bars now available keep their honest fresh outcome. The pre-v6 cache is copied to `.v5.bak` first (defensive archive — the sweep is in-place, unlike D6's pre-v5 rotation which discards).

Rider **R1 (F10)** added `tools/WhatIfRunner/WhatIfRunner.vbproj` to the verify-gate build set — the hole that let a broken WhatIfRunner sit invisible for four days after #6.

Rider **R2 (F9, N4 ticked)** migrated `RoundStatsBuilder` off its private `FavAtrThreshold = 0.5` synthetic yardstick onto the offline `ResolveFavourableBarrier` / `ResolveAdverseBarrier` semantics, so round-stats now scores the same placed geometry every other eval surface uses.

---

## 2. Files touched

| File | Change |
|---|---|
| `LivePerformanceTracker.vb` | `EvaluateEntry` empty-bar branch returns `NO_DATA` (was `WINDOW_EXPIRED`); degenerate-barrier early-out unchanged (stays `WINDOW_EXPIRED`). New Friend overload takes an explicit `ohlcLookup` so the harness + the sweep can drive it without touching module state; production overload delegates to it with `_ohlcLookup`. `GetEligibleBars` similarly gains an explicit-lookup overload. `AggregateRange` `Select Case` unchanged in shape (NO_DATA falls through, so it's counted in `TotalRange` — the tooltip — but not in `SuccessCount`/`FailureCount` — the strip rate); a comment now spells that out. New Friend `IsPreV6Schema` (comment-line detection on the `"no-data outcome"` marker, mirroring `IsPreV5Schema`) + new Friend `ReclassifyWindowExpiredForNoData(entries, ohlcLookup, nowUtc)`. `EVAL_SCHEMA_COMMENT` bumped v5 → v6 with the `"no-data outcome"` marker (the gate) and the `.0000000Z` whole-second backfill-provenance note (F8). Backward substrings preserved: `"min-tradeable-move"` (keeps `IsPreV3Schema` correct) and `"placed-level"` (keeps `IsPreV5Schema` correct). New Step 2.8 in `InitialiseAsync` runs the sweep when `preV6Schema`: `File.Copy` to `.v5.bak` (timestamp-suffixed on collision), sweep in memory, `WriteEvalCache` re-stamps the header. |
| `analysis/MarkdownReportWriter.vb` | §1 barrier-diagnostics table column relabeled `"No-OHLC excl."` → `"No-data excl."` — the offline logic itself is already correct (per N3 D-table); this aligns both surfaces on the same name so a reader can see NO_DATA counted the same way live and offline. |
| `tools/AutoTweaker/RoundStatsBuilder.vb` | N4 rider — `Private Const FavAtrThreshold = 0.5` deleted; `CsvCols` gained the four `Placed*` column indices + `HasPlacedSchema` flag; `ResolveColumns` populates them; `RenderAccuracy` builds a lightweight `CsvRow` adapter and calls `FailureRateMatrix.ResolveFavourableBarrier` / `ResolveAdverseBarrier` for each row. Placed rows read their logged targets/stops unfloored; pre-v0.8 rows fall back to the legacy formula (`engineTargetMult × ATR` floored at `floorPct × entry`, `SwingStop*` else ATR fallback). Multipliers/floor default to `AnalysisConstants.{EngineTargetAtrMultiplier, FavBarAbsFloorPct}` since round-stats doesn't carry a live `EngineSettings`. |
| `tools/checks/verify-gate.ps1` | R1 rider — one `Build 'tools/WhatIfRunner/WhatIfRunner.vbproj'` line inserted between the AutoTweaker and OrderCheck builds. |
| `verify/ordercheck/Program.vb` | New `A33a_EmptyBarsProducesNoData`, `A33b_AggregationExcludesNoData`, `A33c_V6SweepReclassifiesUncoveredPreservesCovered` registrations + fixture bodies. |
| `docs/DeribitIndicatorProject.md` | §15 one-line entry (eval-only change, no settings bump — stated inline). |

---

## 3. Decisions the spec left to the implementer

Two calls the D-table didn't cover.

**(a) The v5 file is COPIED to `.v5.bak`, not moved.** The spec quotes "the D6 rotation pattern (old cache → .v5.bak)". D6 literally moves the file and cold-start-rebuilds — that discards every stored outcome. The F4 sweep exists precisely to *preserve* stored outcomes on covered rows and only re-stamp uncovered ones; a discard would defeat the point. Read the D6 language as "the D6 file-naming convention", not "the D6 discard-and-rebuild mechanism". `File.Copy` (with timestamp-suffix on collision) leaves the pre-sweep file next to the live one as a defensive archive. On restart the schema comment is v6 → `IsPreV6Schema` returns False → sweep does not re-fire.

**(b) N3 read as a rename, not an added column.** The spec says "MarkdownReportWriter §1 diagnostics gain a `No-data excl.` count". The count already exists — `PopulationReport.ExcludedRows` is exactly "rows excluded from all windows for lack of OHLC coverage", surfaced under `"No-OHLC excl."`. Adding a second column with the same semantics would confuse readers and misrepresent the offline logic (the D-table's N3 line explicitly says "already correct"). Renaming to `"No-data excl."` gives both surfaces one name for the same concept, which is the disclosed intent ("so both surfaces disclose it"). Zero offline behaviour change.

---

## 4. Additions beyond the §2 inventory

None. The spec inventory was small and the implementation stayed inside it.

Worth noting one non-addition: I did **not** extend the log line in the v5→v6 sweep to break down per-day (e.g. how many 07-03 rows became NO_DATA). The counters that ship are `beforeExpired / afterNoData / recovered` — the same shape D6's rotation logs. A per-day breakdown belongs in a diagnostic script over the `.v5.bak`, not the hot path.

---

## 5. Acceptance (spec §4)

| Requirement | Result |
|---|---|
| Builds 0/0 | Solution (Release), AutoTweaker, WhatIfRunner, OrderCheck — all **0 errors / 0 warnings**. Release-only per the standing collector-protection rule. |
| Harness unregressed | **165 checks, ALL PASS**, 0 failures (162 pre-F4 + 3 new). No existing assertion regressed — the outcome change is on a branch the harness had no fixture over (empty bars). |
| Empty-bars ⇒ NO_DATA | **A33a** — `EvaluateEntry` with an empty lookup returns `("NO_DATA", Nothing)`; a covered call on the same entry still returns `("SUCCESS", True)`; a degenerate-barrier row (FavBar=0) stays `WINDOW_EXPIRED` (proposal §1 — degenerate early-outs untouched). |
| Aggregation excludes it / TotalRange counts it | **A33b** — 5-row list containing SUCCESS + WINDOW_EXPIRED + 2×NO_DATA + PENDING: `SuccessCount=1`, `FailureCount=1`, `TotalRange=5`, `BarrierRatePct=50.0` (denominator = SuccessCount + FailureCount, NO_DATA outside). |
| v5→v6 sweep semantics | **A33c** — synthetic list of three WINDOW_EXPIRED rows: (i) 07-03 timestamp with no lookup coverage → `NO_DATA`, TargetEverHit `Nothing`; (ii) 07-20 timestamp with a target-hitting wick in the T+3..T+15 window → `SUCCESS`, TargetEverHit `True`; (iii) degenerate `FavBar=0` → stays `WINDOW_EXPIRED`. `IsPreV6Schema` correctly gates on a v5 header comment. |
| Gate builds WhatIfRunner | verify-gate `prepush` now runs `Build 'tools/WhatIfRunner/WhatIfRunner.vbproj'` and it lands 0/0 — visible in the gate output between the AutoTweaker and OrderCheck steps. |
| Round-stats per N4 | `RoundStatsBuilder` no longer references `FavAtrThreshold`; barriers route through `FailureRateMatrix.ResolveFavourableBarrier` / `ResolveAdverseBarrier` with the same `AdverseBarrierMode.Placed` default the offline matrix uses. Behavioural pin: for a v0.8 CSV row with `PlacedTargetLong = P`, the accuracy panel's `favBar` is now `P` (was `entry + 0.5 × ATR`); the two only coincide by accident. |
| verify-gate | `prepush` mode — **GATE PASSED**, exit 0. `display-parity`: no snapshot/card drift. `version-bump`: **WARN** — "engine-path change without a settings.json version bump (nudge only)" *(coordinator re-run 2026-07-21; the original spec-back claimed "no engine-path change", corrected here — `LivePerformanceTracker.vb` is on the engine path)*. The WARN is the expected, accepted outcome: code-only eval change, no config keys, D6 precedent — change_log untouched, §15 row states "no settings bump". |
| Display parity | **The perf strip is a status element and its numbers move only by removal of fabricated failures on any pre-v6 cache; no snapshot/card line changed.** Stated per the parity rule in the commit message. |

---

## 6. Not verified by the implementer (runtime, trader-observed)

The **expected effect** on the running app — after first launch under v6, the strip rebuilds fresh: the `.v5.bak` sidecar appears next to `analysis_eval_cache.csv`, a single `[LivePerformanceTracker] v5→v6 no-data sweep: N WINDOW_EXPIRED re-walked → M NO_DATA` line is logged, and the six strip cells move only by the removal of fabricated failures — is a live-runtime observation. The currently-displayed strip cells span 07-18 onward (07-03 has aged out of the 7-day OHLC cache and the rolling display windows), so the practical visible change is near-zero, which is itself the check (§7a in the offline-matrix spec-back predicted this).

---

## 7. Coordinator review checklist
- [ ] Empty-bars branch of `EvaluateEntry` returns `NO_DATA` (not `WINDOW_EXPIRED`); degenerate-barrier branch unchanged (spec §1).
- [ ] `AggregateRange` counts NO_DATA in `TotalRange` only; success/failure denominator excludes it.
- [ ] Pre-v6 cache is COPIED to `.v5.bak` (not moved); sweep is in-place; WriteEvalCache re-stamps the v6 header; idempotent on restart.
- [ ] `IsPreV6Schema` gate uses the `"no-data outcome"` substring; comment retains `"min-tradeable-move"` (v3 gate) and `"placed-level"` (v5 gate).
- [ ] `MarkdownReportWriter` §1 renames the column to `"No-data excl."`; offline exclusion behaviour unchanged.
- [ ] `RoundStatsBuilder` sources barriers from `ResolveFavourableBarrier` / `ResolveAdverseBarrier` with placed-mode default; `FavAtrThreshold` gone.
- [ ] `verify-gate.ps1` builds WhatIfRunner between AutoTweaker and OrderCheck.
- [ ] Zero scoring impact; no `settings.json` bump; §15 note present.
- [ ] Deviations in §3 above accepted.
