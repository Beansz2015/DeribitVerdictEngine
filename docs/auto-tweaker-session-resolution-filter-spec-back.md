# Auto-Tweaker (Session × Resolution) Population Filter (v36 Phase-2a) — Implementer Spec-Back

**Date:** 2026-06-17
**Seat:** implementer (Opus 4.8, fresh conversation).
**Build spec:** [`auto-tweaker-session-resolution-filter-implementer-handoff.md`](auto-tweaker-session-resolution-filter-implementer-handoff.md) (authoritative — where it and the proposal differ, the hand-off wins per its §1). Supporting: [`-proposal.md`](auto-tweaker-session-resolution-filter-proposal.md) (approval artifact / rationale).
**Status:** **IMPLEMENTED — local commit only** (`e4742b2`). Solution + AutoTweaker + verify harness all build **0/0**; A1–A14 unregressed; **A15a–A15g green** (38 checks ALL PASS). **Routes to the COORDINATOR seat for independent review** (hand-off §8). Trader compiles, runs the isolated-replay, supervises the first NY×1 fire, and pushes — I did not push.

**Three things to put your eyes on** (§3): (1) the `effectiveRowCount` reading of §4.3; (2) the **integration replay is not an automated harness fixture** — it's the trader's supervised step (network + isolated bin copy); (3) the **A15g spec-vs-reality correction** (array paths are unreachable, so the over-match test had to change shape). Everything else implements the spec as written.

> **Coordinator review (2026-06-17, sanity-check seat) — APPROVED; cleared for push.** Independent verification + I ran the isolated replay (the trader couldn't drive it from the app UI — it's a console run, not a UI action):
> - **Re-ran builds + harness:** solution + AutoTweaker + harness all 0/0; **38 checks (A1–A14 unregressed + A15a–A15g) ALL PASS.** Source-verified the `Validate` code guard (§1 correction — `RejectedPathPrefixes` = `kelly.`/`resolution_profiles.` prefix + exact `scoring.min_tradeable_move_pct`, in `Validate` only, with the revert rationale comment) and the `AutoTweakerCore` filter threading (hoisted `settings`; load-time `filtered`; `effectiveRowCount` helper; re-seed-first; all four windowing sites + both skip branches → `filtered.Count`; the five shared evaluable branches → `effectiveRowCount`; sliding-arm guard; trigger name via `MatchSessionBucket`).
> - **Isolated replay (dry-run, isolated copies, ZERO live-book touch):** Stage 1 — the filter isolated **1149/1640 NY rows** on the real v0.6 book and the `'' → 'NY|1'` re-seed fired. Stage 2 — a forced fire on the 2026-06-15 13:18–13:46Z NY cluster ran the **full pipeline** (filter → window → Deribit OHLC fetch → `FailureRateMatrix` → prompt → `DRY_RUN_WRITTEN`, exit 0); the trigger cites `NY session`; the payload window is 100% 06-15 NY; well-formed (no PromptBuilder brace crash). Live book confirmed still 1641 rows / v0.6 / tweaker state untouched afterward.
> - **The four §7 asks — all confirmed:** **§3.1** `effectiveRowCount` — keep (the sliding arm is `pop Is Nothing`-guarded so `filtered === allRows`; sliding behaviour is byte-unchanged). **§3.2** harness/replay split — correct (the harness must stay offline/deterministic; A15e drives the real `RunAsync` to the network boundary); the network fire is now **discharged by this replay**. **§3.3** A15g adaptation — correct, and a good catch: the hand-off's "still passes" assertion was impossible (`NavigatePath` can't traverse the `sessions` array → unresolved-reject regardless), and the adaptation tests the real over-match intent directly. **§4** revert-vs-manual-`resolution_profiles["3"]` deferred to Phase-2b — agreed (guarding `ValidateSnapshotContent` would break every revert; reverts are `auto_commit`-gated + supervised; no manual re-baseline exists yet).
> - **Remaining (trader gate):** the push (yours, never mine); the live supervised first NY×1 fire is your operational call — **DATA-gated** (a real >40%-failure NY×1 window; my replay forced threshold 0 to exercise the path), keep `dry_run_enabled=true`/`auto_commit_enabled=false` and review the proposed diff first.

---

## 1. What was built (every §8 checklist item)

| Checklist item | Done | Where |
|---|---|---|
| `tweaker_config.json`: `population_filter = { "session": "NY", "execution_resolution": 1 }` | ✅ (local — gitignored) | [tweaker_config.json](../tools/AutoTweaker/tweaker_config.json) |
| `TweakerConfig.vb`: `PopulationFilter` POCO + property (both fields nullable = "any") | ✅ | [TweakerConfig.vb](../tools/AutoTweaker/TweakerConfig.vb) |
| `AutoTweaker.vbproj`: add `Core/ExecutionResolution.vb` (after `SettingsLoader`, before `analysis\*`) | ✅ | [AutoTweaker.vbproj](../tools/AutoTweaker/AutoTweaker.vbproj) |
| `ForwardWindowJoiner.vb`: `CsvRow.ExecResolution` + header-name parse (default 1) | ✅ | [ForwardWindowJoiner.vb](../analysis/ForwardWindowJoiner.vb) |
| `AutoTweakerCore.vb`: hoist `settings`; load-time filter + `filtered`/`filtered.Count` threading; `MatchesPopulation`; reroute trigger session-name through `MatchSessionBucket`; sliding-arm guard | ✅ | [AutoTweakerCore.vb](../tools/AutoTweaker/AutoTweakerCore.vb) |
| `TweakerState.vb`: `PopulationFilterKey` + re-seed-on-change in `RunAsync` | ✅ | [TweakerState.vb](../tools/AutoTweaker/TweakerState.vb) + AutoTweakerCore |
| `PromptBuilder.vb`: HARD CONSTRAINT 11 adds `resolution_profiles.*` | ✅ | [PromptBuilder.vb](../tools/AutoTweaker/PromptBuilder.vb) |
| `SettingsDiffApplier.vb`: `RejectedPathPrefixes` (`kelly.`, `resolution_profiles.`) + `scoring.min_tradeable_move_pct` exact reject in **`Validate`** (NOT `ValidateSnapshotContent`) | ✅ | [SettingsDiffApplier.vb](../tools/AutoTweaker/SettingsDiffApplier.vb) |
| `verify/` harness: A15a–A15g + the dry-run integration replay; A1–A14 unregressed | ⚠️ partial — see §3.2 | [verify/ordercheck/Program.vb](../verify/ordercheck/Program.vb), [OrderCheck.vbproj](../verify/ordercheck/OrderCheck.vbproj) |
| `DeribitIndicatorProject.md` §12: Phase-2a addressed + Phase-2b carry-forward | ✅ | [DeribitIndicatorProject.md](DeribitIndicatorProject.md) §12 |
| **Local commit only** | ✅ | `e4742b2` (not pushed) |

**The §1 correction landed as specified.** `SettingsDiffApplier.Validate` now has a **code-level** reject (prefix semantics for `kelly.*` / `resolution_profiles.*`, exact for `scoring.min_tradeable_move_pct`), placed in the `For Each item` loop right after the `RejectedPathFragments` check and **before** path-resolution/stale checks — so an off-surface key is refused regardless of whether it resolves. Guard is on `Validate` only; `ValidateSnapshotContent` is untouched so a wholesale revert still restores `resolution_profiles`/`kelly` unchanged (§6.2).

**Engine untouched.** No `settings.json` change, no scoring change. This is a tweaker-tooling change that alters *which rows the tweaker tunes on*, not any vote.

---

## 2. Filter mechanics as built (the single-population invariant)

`ForwardWindowJoiner.Load` → `pop = config.PopulationFilter` → `filtered = If(pop Is Nothing, allRows, allRows.Where(MatchesPopulation).ToList())`. Everything downstream in fixed mode reads `filtered`:

- **`MatchesPopulation(r, pop, settings)`** (`Friend Shared` so the harness exercises it): resolution matched against the authoritative `r.ExecResolution` stamp (never re-derived from the timestamp); session derived via the shared `ExecutionResolution.MatchSessionBucket` (inclusive `<=`, engine-identical). A null `pop` field = "any" on that axis.
- **`LastEvaluatedRowIndex` now indexes the filtered sequence.** The shrink guard, window-full check, and slice all use `filtered`/`filtered.Count`; the slice reads `filtered(idx)`. `CsvRow.Index` retains the **absolute** CSV index (correct for `ForwardBars` — timestamp-keyed — and `WindowStartRow`/`WindowEndRow` logging).
- **Re-seed on filter change** (first fixed-mode statement): `populationKey = If(pop Is Nothing, "none", "{Session}|{ExecutionResolution}")`. On mismatch vs `state.PopulationFilterKey`, re-seed `LastEvaluatedRowIndex = filtered.Count`, store the key, write INELIGIBLE, save, `Return 2` — identical to the v29 first-run init, applied to the filtered view.
- **Trigger session-name** rerouted to `MatchSessionBucket(settings, windowRows.Last().Timestamp.Hour)?.Name`, dropping the duplicate `settings2` deserialise and the old `hour < EndHour` form (the v34 hour-7 off-by-one). Label and filter can no longer disagree.
- **Sliding arm guarded**: a configured `population_filter` under `window_mode=sliding` → log + INELIGIBLE + `Return 2` (population windowing is undefined there). Filter applies to fixed mode only.
- **`CrossesSessionBoundary` kept unchanged** (hand-off §4.5): within an NY-only set it still skips the overnight 23:xx→13:xx straddle (the walk hits hour 0 = ASIA start), a genuine real-time discontinuity; intraday NY windows fire cleanly.

Confirmed live in the A15e run output:
```
[AutoTweaker] population filter NY×1m — 3/5 rows in population.
[AutoTweaker] INFO — population filter changed ('ASIA|3' → 'NY|1'); re-seeding
              LastEvaluatedRowIndex to filtered.Count=3. ...
```

---

## 3. Decisions / deviations worth your eyes

### 3.1 `effectiveRowCount` — my reading of §4.3 "`LastRunCsvRowCount = filtered.Count` in fixed mode"

§4.3 names four sites to thread (`filtered`/`filtered.Count`) plus "`state.LastRunCsvRowCount = filtered.Count` in fixed mode." The four windowing sites and the two **fixed-mode-only** skip branches (`SKIPPED_SESSION_BOUNDARY`, `SKIPPED_INSUFFICIENT_TIER`) and the shrink-guard write I set to `filtered.Count` directly. But the **five shared evaluable branches** (`BELOW_THRESHOLD`, empty-diff, `DRY_RUN_WRITTEN`, REVERT, TWEAK) that write `LastRunCsvRowCount` run in **both** modes. A blanket `filtered.Count` there would subtly change the deprecated sliding-mode cooldown (which still consumes `LastRunCsvRowCount`).

**Decision:** one helper at the filter point — `Dim effectiveRowCount As Integer = If(isFixedMode, filtered.Count, currentRowCount)` — used in the shared branches. Fixed mode stores `filtered.Count` (§4.3, informational since cooldown is a no-op there); sliding stores `currentRowCount` exactly as before (and in sliding `filtered === allRows` anyway, since the arm is guarded to `pop Is Nothing`). Net: zero behavioural change to sliding, faithful to §4.3 for fixed. **Coordinator ask: confirm this reading.** My lean: keep — it's the minimal-risk way to honour "filtered.Count in fixed mode" without touching the deprecated arm.

### 3.2 The integration replay is the trader's supervised step, NOT an automated fixture

Hand-off §7 lists a dry-run integration replay (`DRY_RUN_WRITTEN` + `BELOW_THRESHOLD`/streak) "mirror[ing] the 2026-06-15 mechanics replay … pointed at a dense historical NY window." That replay needs a **live Deribit OHLC fetch** (`DeribitOhlcFetcher.FetchOhlcRange`, step 5 of `RunAsync`) and an **isolated copy of the bin output**. I deliberately did **not** bake it into the harness: `dotnet run --project verify/ordercheck` must stay deterministic and offline (a network call would make ALL PASS flaky and CI-hostile).

What the harness **does** cover offline: **A15e drives the real `RunAsync`** end-to-end and asserts the re-seed path returns INELIGIBLE before any fetch — so the wiring (config → filter → fixed-mode re-seed → state write) is exercised against the shipped code, just short of the network boundary. The full fire (`DRY_RUN_WRITTEN`, payload 100% res-1) is exactly the supervised step §8 already gates ("Trader compiles + isolated-replay-validates + supervises the first NY×1 fire"). This mirrors how the Phase-1 spec-back handled the WinForms-coupled EXEC-tag render (verified by live run, not in-harness). **Coordinator ask: confirm this split is acceptable** (offline A15a–A15g in the harness; the network dry-run as the trader/coordinator's supervised replay).

### 3.3 A15g — array paths are `NavigatePath`-unreachable, so the over-match test changed shape

§7 A15g asks that `session_volume.sessions...high_multiplier` "still passes `Validate`" to prove the new guard doesn't over-match. But as §1 itself notes, `session_volume.sessions[]` is an **array** and `NavigatePath` only traverses `JsonObject` — so **any** `sessions[].x` path resolves to `Nothing` → rejected as *unresolved*, never "passes." A faithful "still passes" assertion on that exact path is impossible.

**Adaptation (the intent is over-match protection, so I tested that directly):** A15g now asserts (1) a **resolvable, non-guarded** `session_volume.enabled` diff **passes `Validate` cleanly** — direct proof the new prefix guard doesn't catch the `session_volume` subtree; and (2) the array path `session_volume.sessions.0.high_multiplier` is rejected **but its reason does NOT cite "HARD CONSTRAINT 11"** (it's the unresolved-path reject, not the guard) — proof the guard didn't fire. Both deterministic, both green. **Coordinator ask: confirm the adaptation honours A15g's intent.** (The `execution_resolution` array key being array-unreachable is the belt; the prompt is the suspenders — both still hold.)

### 3.4 Minor, non-asking

- `MatchesPopulation` is `Friend` (not `Private` as the §4.2 sketch shows) purely so the same-assembly harness can call it; no external surface.
- **Re-seed gate ordering:** placed *first* in the fixed-mode block, ahead of the v29 first-run init. On a fresh **filtered** state the re-seed subsumes the first-run init (both seed `filtered.Count`). One benign consequence for the **no-filter** path: on the first run after this ships, `key "" → "none"` triggers a single extra INELIGIBLE re-seed round, then steady-state. Harmless (the index semantics genuinely changed), and the live NY×1 rollout *wants* the `"" → "NY|1"` re-seed anyway.
- **Harness linkage:** A15a–A15g pull the AutoTweaker chain into `OrderCheck.vbproj` (`TweakerConfig`/`TweakerState`/`SettingsDiffApplier`/`PromptBuilder`/`ClaudeApiClient`/`ConditionsExtractor`/`CompositeScorer`/`RoundStatsBuilder`/`SnapshotManager`/`AutoTweakerCore` + `analysis/DeribitOhlcFetcher`). **`AutoTweakerProgram.vb` is excluded** — its `Main` would collide with the harness's `Module Program`. Root `.vbproj`'s `Compile Remove verify/**` keeps the harness out of the solution build.

---

## 4. Known interaction flagged, NOT fixed (Phase-2b, per hand-off §6.2)

A **wholesale revert** restores `resolution_profiles` from the snapshot. If a manual `resolution_profiles["3"]` re-baseline has landed *since* that snapshot, the revert rolls it back. The guard correctly does **not** block this (rejecting `resolution_profiles`' *existence* in a snapshot would break every revert — `ValidateSnapshotContent` is intentionally untouched). Reverts are `auto_commit`-gated + supervised. **Recorded as a Phase-2b problem; not fixed here.** Flagging for your sign-off.

---

## 5. Acceptance results

```
dotnet build DeribitVerdictEngine.sln              → Build succeeded, 0 warnings, 0 errors
dotnet build tools/AutoTweaker/AutoTweaker.vbproj  → Build succeeded, 0/0
dotnet build verify/ordercheck/OrderCheck.vbproj   → Build succeeded, 0/0
dotnet run --project verify/ordercheck             → ALL PASS (38 checks)
```

**A15 coverage:**
- **A15a** filter excludes non-matching — interleaved NY(res1, hr13-23) + ASIA(res3, hr0-7) → `MatchesPopulation(·, NY×1)` keeps 3, no res-3 survivor.
- **A15b** resolution homogeneity — every NY×1 survivor has `ExecResolution = 1`.
- **A15c** legacy default — a v0.6 line (no `ExecResolution` column) at an NY-hour timestamp parses to `ExecResolution = 1` and passes NY×1 (real `ForwardWindowJoiner.Load` over a temp file).
- **A15d** session derivation == engine bucket — hr7→ASIA (**not** LONDON; guards the off-by-one), hr12→LONDON, hr13→NY.
- **A15e** re-seed on filter change — real `RunAsync`; `state.PopulationFilterKey="ASIA|3"` + config NY×1 → key-mismatch path sets `LastEvaluatedRowIndex = filtered.Count = 3`, key→`NY|1`, INELIGIBLE, `rc=2`, evaluates nothing.
- **A15f** `Validate` rejects off-surface keys — `resolution_profiles.3.roc_magnitude_threshold`, `kelly.max_leverage`, `scoring.min_tradeable_move_pct` each fail; control `indicators.OBV.trend_gate` passes.
- **A15g** over-match guard — `session_volume.enabled` passes; array multiplier rejected without citing HARD CONSTRAINT 11 (§3.3).

**Not verified in-harness (verified live by the trader's replay instead):** the full `DRY_RUN_WRITTEN` fire on a dense historical NY window + the payload's 100%-res-1 property (network + isolated bin copy — §3.2). The offline harness asserts the filter, homogeneity, legacy default, session boundary, and the re-seed path that the fire sits on top of.

---

## 6. Out of scope (confirmed not done — proposal §6 / hand-off §9)

- **(B) Manual `resolution_profiles["3"]` re-baseline** — the Asia/London accuracy fix; a separate **settings** pass, data-gated (≥50 weekday-3-min/session), done by hand (v33/v34 method), NOT the tweaker.
- **(C) Auto-tweaker Phase-2b** — per-population `LastEvaluatedRowIndex`/round-robin, per-population `WindowSize`/`MinTier`/threshold, picked-cell/round-history segregation by population, the schema-home decision for where session-specific tuned values live, and the revert-vs-`resolution_profiles` interaction (§4). Picked-cell/round history stays homogeneous for Phase-2a because the filter stays on NY×1.

**Sequencing reminder (hand-off §0 / [[project_engine_audit_calibration_trap]]):** the tweaker stays HELD until this filter lands; the (A) population filter now isolates NY×1 regardless of 3-min interleaving, so the old "fire before 3-min data accumulates" race is **moot** — build (A) [done], then the supervised NY×1 fire.

---

## 7. Coordinator asks (summary)

1. **§3.1** — confirm the `effectiveRowCount` reading of §4.3 (`filtered.Count` in fixed, `currentRowCount` in sliding; sliding behaviour unchanged).
2. **§3.2** — confirm the harness/replay split (offline A15a–A15g + A15e-through-`RunAsync`; the network `DRY_RUN_WRITTEN` fire as the supervised replay, not an automated fixture).
3. **§3.3** — confirm the A15g adaptation honours the over-match-protection intent given array paths are `NavigatePath`-unreachable.
4. **§4** — sign off on the revert-vs-manual-`resolution_profiles["3"]` interaction being deferred to Phase-2b.
