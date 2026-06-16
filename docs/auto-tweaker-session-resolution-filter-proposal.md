# Auto-Tweaker (Session × Resolution) Population Filter — Proposal (v36 Phase-2 precondition)

**Date:** 2026-06-17
**From:** coordinator / spec-author seat.
**Status:** **PROPOSAL — needs trader sign-off on §5 (three design decisions) before implementation.** Spec-first per the working rules; this is a calibration-infrastructure change (it changes *which rows the tweaker tunes on*, not any scoring vote / threshold / veto). Local commits only; trader tests + pushes.
**Reads with:** [`session-timeframe-resolution-implementer-handoff.md`](session-timeframe-resolution-implementer-handoff.md) §6.3 (the requirement), [`session-timeframe-resolution-phase1-spec-back.md`](session-timeframe-resolution-phase1-spec-back.md) (v36 Phase-1, shipped + coordinator-approved), `DeribitIndicatorProject.md` §12 WATCHING (the auto-tweaker resolution-awareness + MinTier-floor items).

This is the **hard precondition** the hand-off flagged: *before the auto-tweaker fires on any post-v36 `analysis_log.csv`, it must filter the failure-rate matrix + CSV rows by `(session × resolution)` so it never pools 3-min Asia/London with 1-min NY.* v36 Phase-1 added the per-row `ExecResolution` stamp (CSV v0.7) that makes this possible; this spec consumes it.

---

## 0. Scope in one paragraph

Give the auto-tweaker a **population filter** — a configured `(session, execution_resolution)` pair. On each run, rows are filtered to the matching population **before** windowing, so every window, the failure-rate matrix, the prompt, and the picked-cell history are single-population **by construction** — a 3-min Asia/London row can never enter a working set scoped to NY × 1-min, and vice-versa. The initial population is **NY × 1-min** (the engine's clean primary regime and the target of the already-validated v35 first-fire). The 3-min ROC overrides (`resolution_profiles.*`) join the tweaker's exclusion list — they stay manual-rebaseline-only while Asia/London verdicts are provisional. Asia/London tuning, per-population windowing, and per-population history segregation are **Phase-2b** (separate spec, gated on ≥50 weekday-3-min rows/session).

---

## 1. The problem

### 1.1 What the tweaker does today
`AutoTweakerCore.RunAsync` (fixed mode) walks **disjoint chronological** slices `allRows[LastEvaluatedRowIndex .. +WindowSize-1]`, computes `FailureRateMatrix.Compute` on that one window, and — if the window's aggregate failure rate clears the threshold — asks the LLM for a `settings.json` diff. `LastEvaluatedRowIndex` advances by `WindowSize` per completed round.

### 1.2 What partial protection already exists
There is a `CrossesSessionBoundary` skip ([AutoTweakerCore.vb:186-211](../tools/AutoTweaker/AutoTweakerCore.vb)): a window whose time-span contains any session-**start** hour is dropped as `SKIPPED_SESSION_BOUNDARY` (index still advances `WindowSize`; streak untouched). Because the only two resolution transitions under the fixed 3/3/1 map are at **hour 13** (LONDON 3-min → NY 1-min, = NY's `start_hour`) and the **00:00 wrap** (NY 1-min → ASIA 3-min, = ASIA's `start_hour`), both are session-start hours, so this skip *does* prevent resolution-mixing **inside a single window** today.

### 1.3 Why that is not enough — three residual gaps
1. **The tweaker is population-blind, not just boundary-safe.** Each accepted window is single-session, but the tweaker has no notion of *which* population it observed — it computes a matrix on (say) NY rows and proposes **global** `settings.json` changes. The next window might be Asia, proposing conflicting global changes. They overwrite each other; the engine oscillates. This is the "session-blind" gap the hand-off names, now compounded by resolution.
2. **It is fragile, not explicit.** The resolution-safety in §1.2 is an *emergent* property of the session-boundary skip + the current 3/3/1 map + a correctly-loaded `sessionStartHours`. The degenerate default (`{0, 13}` when settings fail to load, [AutoTweakerCore.vb:101-104](../tools/AutoTweaker/AutoTweakerCore.vb)) drops the hour-8 LONDON boundary — harmless today (ASIA+LONDON are both 3-min) but exactly the kind of thing that silently breaks under a future re-map (e.g. ASIA 3→5). Nothing asserts resolution-homogeneity directly.
3. **Boundary-straddle waste + the MinTier floor.** On interleaved data, every window that straddles a transition is skipped, burning up to `WindowSize-1` rows (parked obs **P9**). Combined with the post-v35 actionable-directional rate (NY ~23%; §12 WATCHING MinTier item), a chronological slicer on mixed data can starve the tier floor and never fire.

### 1.4 Why pooling is dangerous (not merely untidy)
The failure-rate matrix is **ATR-confounded across resolutions**: a 3-min bar's ATR runs ~2.1× a 1-min bar's, so the favourable-barrier distances, the min-move exclusions, and the per-cell failure rates are on different scales. Pooling 3-min and 1-min rows produces a matrix that means nothing, and the tweaker would "fix" a failure rate that is an artifact of mixing. This is the same class of error as the original ATR-confound (`9d871ba`) that motivated the v35 de-confound — do not re-introduce it through the back door.

---

## 2. Design — the population filter

**Principle:** isolate the working set at **load time**. Everything downstream (windowing, the boundary skip, the tier check, the matrix, the prompt, the picked-cell history) is unchanged and becomes single-population for free because it only ever sees filtered rows.

### 2.1 Config — `tweaker_config.json`

```json
"population_filter": {
  "session": "NY",
  "execution_resolution": 1
}
```

- Both fields **nullable** (`null` = "any"). `{ "execution_resolution": 1 }` alone already isolates NY under 3/3/1; setting `session` too is belt-and-suspenders and future-proofs against a re-map that puts two sessions on 1-min.
- Absent block ⇒ **no filter** ⇒ exactly today's behaviour (so the change is inert until the trader opts in — but see §5 Q1: we ship it populated to NY × 1).
- `execution_resolution` here selects *which rows the tweaker reads*; it is **not** a proposable key (HARD CONSTRAINT 11 already forbids the tweaker changing the engine's `execution_resolution`).

POCO ([TweakerConfig.vb](../tools/AutoTweaker/TweakerConfig.vb)):
```vb
Public Class PopulationFilter
    <JsonPropertyName("session")>               Public Property Session As String = Nothing
    <JsonPropertyName("execution_resolution")>  Public Property ExecutionResolution As Integer? = Nothing
End Class
' TweakerConfig gains:
<JsonPropertyName("population_filter")> Public Property PopulationFilter As PopulationFilter = Nothing
```

### 2.2 Row-level resolution + session

- **Resolution (authoritative — from the stamp):** add `CsvRow.ExecResolution As Integer` ([analysis/ForwardWindowJoiner.vb](../analysis/ForwardWindowJoiner.vb), the `CsvRow` class at line 33). Parse it from the v0.7 `ExecResolution` column using the existing header-name lookup (`GetInt(parts, colIdx, "ExecResolution")`), **defaulting to 1** when the column is absent (legacy v0.6 rows — same legacy default the engine's eval-cache migration uses). This is the value the engine *actually ran*, so it is the truth for filtering.
- **Session (derived — engine-identical boundary):** add `Core/ExecutionResolution.vb` to `AutoTweaker.vbproj` (it is host-agnostic; the project already links `EngineSettings`/`SettingsLoader`). Derive the row's session as `ExecutionResolution.MatchSessionBucket(settings, row.Timestamp.Hour)?.Name`. This reuses the **inclusive `<=`** engine bucket and **fixes the tweaker's own off-by-one** at [AutoTweakerCore.vb:415](../tools/AutoTweaker/AutoTweakerCore.vb) (`hour < s.EndHour`), which currently mislabels hour-7 rows as LONDON — the same `<7` bug v36 fixed for the display `ResolveSessionLabel`. Route the `sessionName` derivation (line 408-419) through `MatchSessionBucket` too, so the trigger-line session label and the filter agree.

### 2.3 Apply the filter + index re-seed

In `RunAsync`, immediately after `Dim allRows = ForwardWindowJoiner.Load(config.CsvPath)` ([AutoTweakerCore.vb:107](../tools/AutoTweaker/AutoTweakerCore.vb)):

```vb
Dim pop = config.PopulationFilter
Dim filtered = If(pop Is Nothing, allRows,
    allRows.Where(Function(r) MatchesPopulation(r, pop, settings)).ToList())
If pop IsNot Nothing Then
    Console.WriteLine($"[AutoTweaker] population filter {pop.Session}×{pop.ExecutionResolution}m — " &
                      $"{filtered.Count}/{allRows.Count} rows in population.")
End If
```
`MatchesPopulation` = `(pop.Session Is Nothing OrElse SessionOf(r, settings) = pop.Session) AndAlso (pop.ExecutionResolution Is Nothing OrElse r.ExecResolution = pop.ExecutionResolution.Value)`.

Then **every fixed-mode reference to `allRows` / `currentRowCount` uses `filtered` / `filtered.Count`.** `LastEvaluatedRowIndex` now indexes the **filtered sequence**. `CsvRow.Index` keeps the absolute CSV index (still correct for `ForwardBars` keying — which is timestamp-based — and for `WindowStartRow`/`WindowEndRow` logging).

**State migration (mandatory — the index changes meaning).** `state.LastEvaluatedRowIndex` was an absolute index; under a filter it indexes the filtered sequence. Add `state.PopulationFilterKey As String` (e.g. `"NY|1"`). On any run where the computed key ≠ the stored key (first introduction *or* the trader switching populations), **re-seed** `LastEvaluatedRowIndex = filtered.Count` and store the new key — preserving filtered history without re-evaluating it, identical to the v29 first-run init pattern ([AutoTweakerCore.vb:119-128](../tools/AutoTweaker/AutoTweakerCore.vb)). This makes switching populations safe and makes the CSV-shrink guard ([:139](../tools/AutoTweaker/AutoTweakerCore.vb)) operate on the filtered count.

### 2.4 Downstream is single-population for free
- The `CrossesSessionBoundary` skip stays **as-is**. Within an NY-only filtered set it still (correctly) skips a window that straddles the overnight 23:xx→13:xx gap (the walk hits hour 0 = ASIA start), because that is a genuine ~14 h real-time discontinuity, not a continuous session. Intraday NY windows fire cleanly.
- `FailureRateMatrix.Compute(windowRows, …)` is automatically single-resolution → the favourable-barrier floor `max(k×ATR, pct×price)` is now coherent (one ATR scale).
- `PromptBuilder` receives a single-population window + (because the tweaker only ever runs this one population) a single-population `PickedCellHistory`.

---

## 3. Proposal-scoping policy (the global-apply reality)

The engine's tunable surface is **almost entirely global keys**. There is no per-session or per-resolution home for a tuned scoring threshold today, except `resolution_profiles.*` (the 3-min ROC overrides) — which are **provisional** (the 2.1× proxy; Phase-2 manual re-baseline priority) and which we are explicitly **adding to the exclusion list** (§4). So:

> **A tune computed on the NY × 1-min population is applied to the GLOBAL keys.** Those globals feed NY directly and feed Asia/London by profile-inheritance for every key not in `resolution_profiles["3"]`.

This is the honest, pragmatic contract: the tweaker tunes the engine's primary regime (NY/1-min, the bulk of clean data), and Asia/London inherit those globals while their *ROC* is separately, manually re-baselined. It is not "per-session optimal," but per-session-optimal tuning has nowhere to land until Phase-2b decides where session-specific values live (§4). The filter's job here is **safety** (never pool, never corrupt the 3-min overrides), not multi-population optimisation.

**Consequence — a real benefit:** once this filter is live, the hand-off's hard-ordering pressure ("run the NY/1-min first-fire *before* 3-min rows accumulate") is **relieved**. The filter isolates the NY × 1-min population regardless of how many 3-min Asia/London rows interleave, so the supervised first-fire can run on clean NY × 1 data at any time after v36 ships. (Mechanics were already validated via the 2026-06-15 isolated replay.)

---

## 4. HARD CONSTRAINT 11 — add `resolution_profiles.*`

[`PromptBuilder.vb`](../tools/AutoTweaker/PromptBuilder.vb) HARD CONSTRAINT 11 already excludes `execution_resolution`, `min_tradeable_move_pct`, and `kelly.*`. **Add `resolution_profiles.*`** (the 3-min ROC override map). Rationale: the prompt inlines full `settings.json`, so the 3-min ROC keys are reachable; while the filter is on NY × 1-min the tweaker observes only 1-min data and must never edit the 3-min overrides, and even under a future res=3 filter those values are **manual re-baseline territory** (v33/v34 precedent — settings-only manual passes), not auto-tuned, until Phase-2b says otherwise. This closes the one path by which a 1-min tune could corrupt the provisional 3-min seed.

---

## 5. Design decisions needing trader sign-off

1. **Initial population = NY × 1-min?** *(Recommend YES.)* It is the engine's clean primary regime, the bulk of post-reset data, and the exact target of the already-validated v35 supervised first-fire. The interim `window_size_verdicts=75` / `min_tier_eligible_rows=15` pairing was set for precisely this population (≈17 eligible per 75 at NY's ~23% actionable rate).
2. **Accept GLOBAL-apply scoping (§3) + `resolution_profiles.*` off-surface (§4)?** *(Recommend YES.)* Matches the global tunable surface and the provisional-Asia/London design; the alternative (per-session tunable targets) is a larger calibration-design change deferred to Phase-2b.
3. **Defer Asia/London tuning + per-population windowing/MinTier + history-segregation to Phase-2b?** *(Recommend YES.)* Gated on ≥50 weekday-3-min rows/session (the WATCHING re-verify) and on deciding where session-specific tuned values live. Out of scope here.

If any answer is "no," that is a genuine design change → it routes back to a revised proposal, not to implementation.

---

## 6. Out of scope (Phase-2b — separate spec)

- **Per-population windowing** (a `LastEvaluatedRowIndex` per `(session×resolution)` so populations resume independently and round-robin) — fixes the last of the P9 waste; not needed while we tune one population at a time.
- **Per-population `WindowSize` / `MinTier` / `failure_rate_threshold`** — Asia/London will want their own (likely smaller windows; lower actionable rates). Dovetails with the §12 MinTier-floor recalibration.
- **Picked-cell / round-history segregation by population** — while the filter stays on one population the history is homogeneous; *switching* populations needs tag-by-population (or archive+reset) so an Asia tuning prompt never sees NY-picked cells. Specced when Asia/London tuning begins.
- **Where session-specific tuned values live** — making `resolution_profiles.*` (or a new per-session block) tunable under a res=3 filter. The prerequisite for actually optimising Asia/London rather than just inheriting NY globals.

---

## 7. Acceptance (extend the `verify/` harness; isolated-replay-validate before any live fire)

Unit fixtures (host-agnostic; the harness already links the AutoTweaker deps):
- **T1 — filter excludes non-matching:** synthetic CSV interleaving NY(res 1) + ASIA(res 3) rows → with `NY×1`, the working set contains **only** NY res-1 rows; assert no res-3 row in any window.
- **T2 — resolution homogeneity:** every produced window's rows share `ExecResolution = 1`.
- **T3 — legacy default:** a v0.6 row (no `ExecResolution` column) at an NY-hour timestamp parses as res 1 and is **included** under `NY×1`.
- **T4 — session derivation == engine bucket:** hour 7 → ASIA (inclusive `<=`, **not** LONDON — guards the line-415 off-by-one); hour 13 → NY; hour 12 → LONDON.
- **T5 — re-seed on filter change:** changing `PopulationFilterKey` re-seeds `LastEvaluatedRowIndex = filtered.Count` and evaluates nothing that run (returns INELIGIBLE/first-run init).
- **T6 — matrix single-population:** `FailureRateMatrix.Compute` receives only filtered rows (assert the row set handed in).
- **T7 — `resolution_profiles.*` rejected:** a crafted diff touching `resolution_profiles.3.roc_magnitude_threshold` fails `SettingsDiffApplier.Validate` (HARD CONSTRAINT 11).

Integration (mirror the 2026-06-15 mechanics replay): throwaway config+state, `dry_run_enabled=true`, `population_filter = NY×1`, pointed at a dense historical NY window with the failure threshold forced low → assert a full fire completes (`DRY_RUN_WRITTEN`, well-formed payload) and that the payload's window + picked-cell history are 100% res-1. Then a second pass with the threshold forced high → `BELOW_THRESHOLD` + streak/snapshot path, all res-1. **A1–A14 must stay green; `dotnet build` solution + AutoTweaker + harness clean.**

---

## 8. Commit checklist (after §5 sign-off)

- [ ] `tweaker_config.json`: add `population_filter` = `{ "session": "NY", "execution_resolution": 1 }` (gitignored bin/local file; document the default in the spec + state file).
- [ ] `TweakerConfig.vb`: `PopulationFilter` POCO + property.
- [ ] `AutoTweaker.vbproj`: add `<Compile Include="..\..\Core\ExecutionResolution.vb" />`.
- [ ] `ForwardWindowJoiner.vb`: `CsvRow.ExecResolution` + header-name parse (default 1).
- [ ] `AutoTweakerCore.vb`: load-time filter + `filtered`/`filtered.Count` threading; `MatchesPopulation` + `SessionOf` via `MatchSessionBucket`; reroute the `sessionName` trigger derivation through `MatchSessionBucket` (fix line-415 `<` → engine `<=`).
- [ ] `TweakerState.vb`: `PopulationFilterKey` + re-seed-on-change.
- [ ] `PromptBuilder.vb`: HARD CONSTRAINT 11 adds `resolution_profiles.*`; `SettingsDiffApplier.Validate` rejects it (confirm the key-path guard covers nested maps).
- [ ] `verify/` harness: T1–T7 + the dry-run integration replay; A1–A14 unregressed.
- [ ] `DeribitIndicatorProject.md` §12: mark the auto-tweaker resolution-awareness item *Phase-2a addressed (filter)*; note Phase-2b carry-forward.
- [ ] **Local commit only.** Trader compiles + isolated-replay-validates + supervises the first NY×1 fire before any push.

---

## 9. Sequencing

v36 push (trader) → **implement + isolated-replay-validate this filter** → supervised **NY × 1-min** first fire **under the filter** (dry-run, coordinator reviews the diff) → accumulate ≥50 weekday-3-min rows/session → **Phase-2b** (Asia/London tuning + per-population windowing/thresholds/history). The filter is the gate: **the auto-tweaker must not fire on any `analysis_log.csv` containing 3-min rows until this lands and is replay-validated.**
