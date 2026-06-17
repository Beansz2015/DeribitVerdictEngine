# Auto-Tweaker Phase-2b — Per-Population Auto-Tuning (DRAFT / living spec)

**Date:** 2026-06-17 (draft) · **From:** coordinator / spec-author seat.
**Status:** **DRAFT — NOT ready to build.** This is a living spec to be finalized once 3-min data exists. It is the **lowest-priority** item in the v36 arc and may not be built at all if the manual (B) re-baseline proves low-effort enough. Blocked on three gates (§1). Sections marked **[FILL IN]** need the accumulated-data findings before they can be settled; the trader signs off §3 once those land.
**Reads with:** [`auto-tweaker-session-resolution-filter-proposal.md`](auto-tweaker-session-resolution-filter-proposal.md) + [`-implementer-handoff.md`](auto-tweaker-session-resolution-filter-implementer-handoff.md) + [`-spec-back.md`](auto-tweaker-session-resolution-filter-spec-back.md) (Phase-2a (A), shipped + pushed); `DeribitIndicatorProject.md` §12 (MinTier-floor + Phase-2 carry-forward WATCHING items), §16.1 (auto-tweaking as the long-arc goal).

> **What this is NOT.** It is **not** the Asia/London accuracy fix. That is **(B), the *manual* `resolution_profiles["3"]` re-baseline** — settle the 2.1× ROC proxy + extend the profile to TTM/divergence gates, v33/v34 settings method, gated only on data. (B) ships the accuracy. **This spec (C) only *automates* future tuning** — it teaches the tweaker to tune the Asia/London populations itself, safely, on the foundation (B) lays. If the manual cadence is light, (C) may never be worth building.

---

## 0. Scope in one paragraph

Phase-2a (A) restricts the tweaker to **one** designated population (NY × 1-min), switched by editing `population_filter`. Phase-2b lifts that to **many** populations the tweaker services on their own schedules: each `(session × resolution)` gets its own evaluated-row cursor, its own window/MinTier/threshold, its own isolated picked-cell/round history, and — the crux — its own **tunable home** so an Asia/London tune lands in `resolution_profiles` (per-resolution), never in the global keys NY depends on. No population can pool with, or overwrite, another.

---

## 1. Prerequisites (hard gates — do not start before all three)

1. **(A) population filter shipped.** ✅ (`e4742b2`, pushed 2026-06-17).
2. **≥50 weekday-3-min rows per session** (ASIA and LONDON separately). Same data gate as (B). Weekend rows excluded (the v34 weekend-confound lesson).
3. **(B) the manual `resolution_profiles["3"]` re-baseline done.** Two reasons it must precede (C): (a) it replaces the provisional 2.1× ROC seed with measured values, so the tweaker auto-tunes *from a sane baseline* rather than chasing a proxy; (b) it **extends `resolution_profiles["3"]` to the candle-magnitude keys** (TTM, divergence gates), which is the tunable home (C) writes into. Plus the **schema-home decision (§3)** signed off with the data.

If any gate is unmet, (C) is premature — stop and run the manual path.

---

## 2. Components

### 2.1 Per-population evaluated-row cursors
Phase-2a uses one `LastEvaluatedRowIndex` and *re-seeds* it whenever `population_filter` changes (one population at a time). Phase-2b replaces it with a **per-population map** so populations resume independently:
```vb
' TweakerState
<JsonPropertyName("population_indices")>
Public Property PopulationIndices As New Dictionary(Of String, Integer)()   ' "NY|1" → lastEvaluatedRowIndex (in that population's filtered sequence)
```
Migration: seed each active population's index from the current single `LastEvaluatedRowIndex`/`PopulationFilterKey` on first Phase-2b run (preserve, don't re-evaluate). Keep `LastEvaluatedRowIndex` as the no-filter fallback.

**Selection policy** — which population to evaluate each run. Recommend **most-unevaluated-rows-first** (maximizes coverage as data accrues unevenly across sessions); round-robin is the simpler alternative. [FILL IN — pick once we see the per-session accrual rates.]

### 2.2 Per-population windowing config
NY tolerates `window=75`/`MinTier=15` (≈23% actionable). Asia/London likely have lower actionable-directional rates → they need smaller windows / lower MinTier or they never clear the floor (§12 MinTier item). Replace the single `population_filter` with a list:
```json
"populations": [
  { "session": "NY",     "execution_resolution": 1, "window_size": 75, "min_tier": 15, "failure_threshold_pct": 40 },
  { "session": "ASIA",   "execution_resolution": 3, "window_size": <DATA>, "min_tier": <DATA>, "failure_threshold_pct": <DATA> },
  { "session": "LONDON", "execution_resolution": 3, "window_size": <DATA>, "min_tier": <DATA>, "failure_threshold_pct": <DATA> }
]
```
`population_filter` (Phase-2a singular) remains accepted as the degenerate one-population case for back-compat. **[FILL IN]** the Asia/London values from the measured per-session actionable-directional rate.

### 2.3 History segregation by population
`PickedCellHistory` and `RoundHistory` must not bleed across populations (an ASIA tuning prompt must never see NY-picked cells). Add `population As String` to `PickedCellEntry` / `RoundSummary`; `PromptBuilder` + `ConditionsExtractor` filter to the active population. (Phase-2a left these homogeneous because it stays on one population — see its spec-back §6.)

### 2.4 Tunable surface by population — **the crux (§3)**
A res=3 tune must land where it can't corrupt NY. See §3.

### 2.5 Revert ↔ `resolution_profiles` interaction (carried from Phase-2a spec-back §4)
A wholesale revert restores `resolution_profiles` from the snapshot — clobbering a manual (B) re-baseline that landed later. Phase-2b must make reverts **population-scoped**: revert only the keys the tuned population owns (e.g. an ASIA revert restores only `resolution_profiles["3"].*` to the snapshot value), or take **per-population snapshots**. [Design alongside §3's outcome.]

---

## 3. The schema-home decision (data-gated; recommend Option 1 pending data)

**Where does an Asia/London (res=3) tune land so it (a) never touches NY's global keys, (b) is the right granularity?** The engine's general tunable surface is global; the only per-resolution home today is `resolution_profiles`.

- **Option 1 — resolution-conditional `resolution_profiles` (recommended pending data).** Under a res=3 filter the tweaker tunes `resolution_profiles["3"].*` (the keys (B) populated); under res=1 it tunes the global keys. The Phase-2a HARD CONSTRAINT 11 exclusion of `resolution_profiles.*` becomes **population-conditional** — excluded under res=1 (a 1-min tune must not touch the 3-min overrides), permitted under res=3. Minimal; reuses the existing override map; NY and Asia/London can never fight. **Limitation:** ASIA and LONDON are *both* res=3, so Option 1 tunes them **together** — fine *only if* their 3-min candle-magnitude distributions are similar.
- **Option 2 — per-session profiles (only if ASIA ≠ LONDON materially).** A `session_profiles` map keyed by session name. Distinguishes ASIA from LONDON but adds schema + read-site routing (every consumer resolves session-override → resolution-override → global). Justified only if the data shows ASIA-3min and LONDON-3min need *different candle-magnitude* thresholds. (Their *volume* character already differs — v34 set ASIA `session_volume` 1.10/1.05 vs LONDON 1.00/1.00 — but volume is handled by `session_volume` + DynamicNorms, not here.)

**Decision rule (run once ≥50 weekday-3-min/session exist):** compare the ASIA-3min vs LONDON-3min distributions of `roc_magnitude` / `roc_slope_delta` (+ TTM / divergence gates). **Similar → Option 1. Materially different → Option 2.** **[FILL IN]** with the measured distributions + the chosen option. Trader signs off here.

---

## 4. Open questions to settle with the data [FILL IN]

1. Per-session `window_size` / `min_tier` / `failure_threshold_pct` (§2.2) — from each session's actionable-directional rate.
2. ASIA vs LONDON candle-magnitude divergence → Option 1 vs Option 2 (§3).
3. Selection policy — most-unevaluated vs round-robin (§2.1) — from the per-session accrual pattern.
4. **Whether to build (C) at all.** If re-running the manual (B) per session a few times a month is low-effort, (C)'s automation may not earn its complexity. Decide after the first one or two manual (B) passes.

---

## 5. Acceptance (sketch — finalize with §3's option)

- **Per-population cursors resume independently** — a synthetic 3-population CSV (NY×1, ASIA×3, LONDON×3 interleaved) walks each population's window from its own index; no cross-population pooling (extends A15a).
- **Tunable-surface isolation** — under res=3, a `resolution_profiles["3"]` tune passes `Validate`; under res=1, `resolution_profiles.*` is still rejected (population-conditional guard); neither population's run can write the other's keys.
- **History segregation** — an ASIA tuning prompt contains zero NY-picked cells.
- **Population-scoped revert** — reverting an ASIA tune restores only its keys; a later NY tune / manual re-baseline is untouched.
- A1–A15 unregressed; isolated-replay (per Phase-2a method) on a real 3-min population once data exists; `dotnet build` clean.

---

## 6. Sequencing

(data accumulates) → **(B) manual `resolution_profiles["3"]` re-baseline** (accuracy; establishes the baseline + the tunable home) → **finalize this spec** with the §3/§4 data findings + trader sign-off → implement Phase-2b → supervised per-population dry-run fires (coordinator reviews each diff) → optional `auto_commit`.

**This document is a DRAFT and will be revised with data findings before it becomes a build spec.**
