# Auto-Tweaker (Session × Resolution) Population Filter — Implementer Hand-off (v36 Phase-2a)

**Date:** 2026-06-17
**From:** coordinator / spec-author seat.
**To:** implementer seat (Opus, fresh conversation; this doc + the proposal below as kickoff).
**Status:** **APPROVED — build spec.** The *why* is the proposal ([`auto-tweaker-session-resolution-filter-proposal.md`](auto-tweaker-session-resolution-filter-proposal.md)), trader-signed-off on all three §5 design decisions (2026-06-17). This doc is the *how*; where it and the proposal differ, **this doc wins** (§1 — the proposal assumed a code guard that does not exist). Scoring-adjacent (changes which rows the tweaker tunes on, not any scoring vote) → spec-first, **local commits only, trader tests + pushes.**
**Reads with:** the proposal (approval artifact / rationale), `session-timeframe-resolution-implementer-handoff.md` (the v36 hand-off whose conventions this mirrors), `DeribitIndicatorProject.md` §12 WATCHING (the auto-tweaker resolution-awareness + MinTier items).

Build target: the auto-tweaker (`tools/AutoTweaker/`, separate `.vbproj`, **zero WinForms**) consumes the v0.7 `ExecResolution` stamp and filters its working set to one `(session × resolution)` population, so it can never pool 3-min Asia/London with 1-min NY. Initial population **NY × 1-min**.

---

## 0. Scope in one paragraph

Filter the tweaker's CSV rows to a configured `(session, execution_resolution)` population **at load time**, before windowing. Everything downstream (the fixed-window slice, the `CrossesSessionBoundary` skip, the tier check, `FailureRateMatrix.Compute`, the prompt, the picked-cell history) is unchanged and becomes single-population because it only ever sees filtered rows. Add a **code-level** reject in `SettingsDiffApplier.Validate` for the trader-owned-risk + `resolution_profiles.*` keys (HARD CONSTRAINT 11 is prompt-only today — §1). State re-seeds on filter change. Per-population auto-tuning, Asia/London tuning, and the manual `resolution_profiles["3"]` re-baseline are **out of scope** (proposal §6 (B)/(C)).

---

## 1. The one correction to the proposal — `Validate` does not enforce HARD CONSTRAINT 11

The proposal (T7, §4) implies `SettingsDiffApplier.Validate` rejects a diff touching `resolution_profiles.*`. **It does not.** [`SettingsDiffApplier.vb`](../tools/AutoTweaker/SettingsDiffApplier.vb) rejects only `RejectedPathFragments` (dead keys / `_fixed_pct_` / `bbw_none_bonus`), `DisabledGatedPaths` (mtf_gate / regime_weights `.enabled`), the `version` key, unresolved paths, and stale old-values. `kelly.*`, `scoring.min_tradeable_move_pct`, and `resolution_profiles.*` are guarded **only by the PromptBuilder text** (HARD CONSTRAINT 11) — a convention the model can ignore, exactly as the v35 spec-back flagged for `kelly.*` ("only convention-protected").

**This hand-off closes that gap** (§6.2): add a code-level reject for the trader-owned-risk + resolution keys in `Validate`. This hardens the existing `kelly.*` / `min_tradeable_move_pct` convention *and* covers the new `resolution_profiles.*` surface in one move, and makes the acceptance test real. (Note: `session_volume.sessions[].execution_resolution` is already unreachable via `NavigatePath`, which only traverses `JsonObject`, not the `sessions` `JsonArray` — an array-index path returns `Nothing` → rejected as unresolved. Belt-and-suspenders covers it in the prompt anyway.)

---

## 2. Config + POCO

### 2.1 `tweaker_config.json` (gitignored bin/local file)
Add, populated to the initial population:
```json
"population_filter": { "session": "NY", "execution_resolution": 1 }
```
Both fields nullable (`null` = "any"). Absent block ⇒ no filter ⇒ exactly today's behaviour.

### 2.2 `TweakerConfig.vb`
```vb
Public Class PopulationFilter
    <JsonPropertyName("session")>              Public Property Session As String = Nothing
    <JsonPropertyName("execution_resolution")> Public Property ExecutionResolution As Integer? = Nothing
End Class
' TweakerConfig gains:
<JsonPropertyName("population_filter")> Public Property PopulationFilter As PopulationFilter = Nothing
```

### 2.3 `AutoTweaker.vbproj`
Add `<Compile Include="..\..\Core\ExecutionResolution.vb" />` (host-agnostic; gives the tweaker the engine's exact `MatchSessionBucket`). Place it before the `analysis\*` includes; confirm `dotnet build tools/AutoTweaker/AutoTweaker.vbproj` is 0/0 after.

---

## 3. Row layer — resolution stamp + session

### 3.1 `CsvRow.ExecResolution` ([analysis/ForwardWindowJoiner.vb](../analysis/ForwardWindowJoiner.vb), `CsvRow` class, line 33)
Add `Public Property ExecResolution As Integer = 1`. Parse it in `Load` via the existing header-name `colIdx` lookup, **defaulting to 1** when the column is absent (legacy v0.6 rows). The parse helpers there return strings (`GetStr(parts, colIdx, "Verdict")`); add an integer read mirroring them:
```vb
row.ExecResolution = ParseIntOr(GetStr(parts, colIdx, "ExecResolution"), 1)
' where ParseIntOr returns the fallback when the string is empty or unparseable.
```
This is the value the engine actually ran — authoritative for filtering. Do **not** re-derive resolution from the timestamp.

### 3.2 Session derivation (engine-identical boundary)
Session is **not** a CSV column → derive it from the timestamp via the shared engine bucket:
`ExecutionResolution.MatchSessionBucket(settings, row.Timestamp.Hour)?.Name` (inclusive `<=`). Do **not** use the line-415 `hour < s.EndHour` form (the display off-by-one that mislabels hour-7).

---

## 4. The filter + index threading ([tools/AutoTweaker/AutoTweakerCore.vb](../tools/AutoTweaker/AutoTweakerCore.vb))

### 4.1 Hoist `settings` to method scope
Today `settings` is declared **inside** the `Try` at [:79](../tools/AutoTweaker/AutoTweakerCore.vb) and is out of scope at the filter point (line 107) and the session-name block re-deserializes it (`settings2`, line 412). Declare `Dim settings As EngineSettings = Nothing` before the `Try` (line ~64), assign inside, and reuse it at the filter and the trigger-name site — delete the duplicate `settings2` deserialize.

### 4.2 Filter at load
Immediately after `Dim allRows = ForwardWindowJoiner.Load(config.CsvPath)` ([:107](../tools/AutoTweaker/AutoTweakerCore.vb)):
```vb
Dim pop = config.PopulationFilter
Dim filtered As List(Of CsvRow) =
    If(pop Is Nothing, allRows, allRows.Where(Function(r) MatchesPopulation(r, pop, settings)).ToList())
If pop IsNot Nothing Then
    Console.WriteLine($"[AutoTweaker] population filter {If(pop.Session,"any")}×{If(pop.ExecutionResolution?.ToString(),"any")}m — " &
                      $"{filtered.Count}/{allRows.Count} rows in population.")
End If
```
Helper (private shared):
```vb
Private Shared Function MatchesPopulation(r As CsvRow, pop As PopulationFilter, settings As EngineSettings) As Boolean
    If pop.ExecutionResolution.HasValue AndAlso r.ExecResolution <> pop.ExecutionResolution.Value Then Return False
    If Not String.IsNullOrEmpty(pop.Session) Then
        Dim b = ExecutionResolution.MatchSessionBucket(settings, r.Timestamp.Hour)
        If b Is Nothing OrElse Not String.Equals(b.Name, pop.Session, StringComparison.OrdinalIgnoreCase) Then Return False
    End If
    Return True
End Function
```

### 4.3 Thread `filtered` / `filtered.Count` through fixed mode
**Every** fixed-mode reference to `allRows` / `currentRowCount` uses `filtered` / `filtered.Count`: the first-run init ([:119](../tools/AutoTweaker/AutoTweakerCore.vb)), the CSV-shrink guard ([:139](../tools/AutoTweaker/AutoTweakerCore.vb)), the window-full check ([:153](../tools/AutoTweaker/AutoTweakerCore.vb)), the slice ([:166-171](../tools/AutoTweaker/AutoTweakerCore.vb)). `LastEvaluatedRowIndex` now indexes the **filtered sequence**. `CsvRow.Index` keeps the absolute CSV index (correct for `ForwardBars` — timestamp-keyed — and for `WindowStartRow`/`WindowEndRow` logging). **Leave the sliding-mode arm untouched** (deprecated; the filter applies to fixed mode only — guard the sliding arm with `If pop IsNot Nothing Then Throw`/log-and-ineligible, since population windowing is undefined there).

`state.LastRunCsvRowCount = filtered.Count` in fixed mode (informational; cooldown is no-op there).

### 4.4 Reroute the trigger session-name (fix the off-by-one)
The session-name block ([:408-419](../tools/AutoTweaker/AutoTweakerCore.vb)) uses `hour >= StartHour AndAlso hour < EndHour`. Replace with `ExecutionResolution.MatchSessionBucket(settings, windowRows.Last().Timestamp.Hour)?.Name`, so the trigger label and the filter agree.

### 4.5 `CrossesSessionBoundary` skip — unchanged
Keep it. Within an NY-only filtered set it still (correctly) skips a window straddling the overnight 23:xx→13:xx gap (the boundary walk hits hour 0 = ASIA start), a genuine real-time discontinuity. Intraday NY windows fire cleanly.

---

## 5. State — re-seed on filter change ([tools/AutoTweaker/TweakerState.vb](../tools/AutoTweaker/TweakerState.vb))

`LastEvaluatedRowIndex` was an absolute index; under a filter it indexes the filtered sequence, so a stale persisted value (the live re-seed to 1639) is wrong for the filtered view. Add:
```vb
<JsonPropertyName("population_filter_key")> Public Property PopulationFilterKey As String = ""
```
In `RunAsync`, compute the current key (e.g. `$"{pop?.Session}|{pop?.ExecutionResolution}"`, or `"none"` when `pop Is Nothing`). When it ≠ `state.PopulationFilterKey` (first introduction **or** the trader switching populations), **re-seed**: `state.LastEvaluatedRowIndex = filtered.Count`, store the new key, write `INELIGIBLE`, save, `Return 2` — identical to the v29 first-run init ([:119-128](../tools/AutoTweaker/AutoTweakerCore.vb)). This makes switching populations safe and the shrink guard operate on the filtered count.

> **Picked-cell / round history** stays as-is for Phase-2a (the filter stays on NY×1, so the history is homogeneous). Segregating history by population is a **Phase-2b** concern (proposal §6) — flag it in the §15 note, do not build it.

---

## 6. Enforcement of the off-limits keys

### 6.1 PromptBuilder HARD CONSTRAINT 11 (text — first line of defence)
[`PromptBuilder.vb`](../tools/AutoTweaker/PromptBuilder.vb) line ~42-46: add `'resolution_profiles.*' (the per-resolution 3-min ROC overrides — provisional seed, manual re-baseline only)` to the never-tune list alongside `min_tradeable_move_pct` / `kelly.*` / `execution_resolution`.

### 6.2 `SettingsDiffApplier.Validate` (code — the real guard; §1)
Add a rejected-**prefix** check (the existing `RejectedPathFragments` is a substring match; these need prefix/exact semantics to avoid over-matching). In `Validate`, inside the `For Each item` loop, after the `RejectedPathFragments` check:
```vb
Private Shared ReadOnly RejectedPathPrefixes As String() = {
    "kelly.", "resolution_profiles."        ' trader-owned-risk + provisional per-resolution overrides
}
' …and treat "scoring.min_tradeable_move_pct" as an exact-match reject.
' In the loop (path already = item.Path.Trim().ToLower()):
For Each pre In RejectedPathPrefixes
    If path.StartsWith(pre) Then
        result.IsValid = False
        result.ErrorReason = $"Rejected: '{item.Path}' is a trader-owned / off-tweaker-surface key (HARD CONSTRAINT 11)."
        Return result
    End If
Next
If path = "scoring.min_tradeable_move_pct" Then
    result.IsValid = False
    result.ErrorReason = "Rejected: 'scoring.min_tradeable_move_pct' is the trader's slippage floor (HARD CONSTRAINT 11)."
    Return result
End If
```
**Do NOT add these to `ValidateSnapshotContent`** — a revert restores a *whole* snapshot, which legitimately contains `resolution_profiles` / `kelly` unchanged; rejecting their *existence* would break every revert. The guard belongs only on proposed *changes* (`Validate`). (Known interaction to note, not fix: a wholesale revert restores `resolution_profiles` from the snapshot — if a manual `resolution_profiles["3"]` re-baseline has landed since that snapshot, the revert would roll it back. Reverts are `auto_commit`-gated + supervised; flag for the reviewer, Phase-2b problem.)

---

## 7. Acceptance — extend the `verify/` harness (A15a–A15g); A1–A14 stay green

Host-agnostic fixtures (the harness already links the AutoTweaker deps + `Core/ExecutionResolution.vb` per §2.3):
- **A15a — filter excludes non-matching:** a synthetic `List(Of CsvRow)` interleaving NY(res 1, hours 13-23) + ASIA(res 3, hours 0-7) → `MatchesPopulation(·, NY×1, settings)` keeps only the NY res-1 rows; assert count + that no res-3 row survives.
- **A15b — resolution homogeneity:** every row that passes `NY×1` has `ExecResolution = 1`.
- **A15c — legacy default:** a `CsvRow` parsed from a v0.6 line (no `ExecResolution` column) at an NY-hour timestamp gets `ExecResolution = 1` and passes `NY×1`.
- **A15d — session derivation == engine bucket:** via `MatchSessionBucket`, hour 7 → ASIA (**not** LONDON — guards the off-by-one), hour 13 → NY, hour 12 → LONDON.
- **A15e — re-seed on filter change:** with `state.PopulationFilterKey="ASIA|3"` and config `NY×1`, the key-mismatch path sets `LastEvaluatedRowIndex = filtered.Count` and returns INELIGIBLE (evaluates nothing).
- **A15f — `Validate` rejects off-surface keys:** diffs touching `resolution_profiles.3.roc_magnitude_threshold`, `kelly.max_leverage`, and `scoring.min_tradeable_move_pct` each fail `Validate`; a control diff on a normal tunable key (e.g. `indicators.OBV.trend_gate`) still passes.
- **A15g — `Validate` still passes a normal session_volume multiplier:** `session_volume.sessions...high_multiplier` is **not** caught by the new guard (only `execution_resolution` is off-limits, and that's array-unreachable) — guards against over-matching.

**Integration (mirror the 2026-06-15 mechanics replay):** throwaway config+state, `dry_run_enabled=true`, `population_filter = NY×1`, pointed at a dense historical **NY** window with the failure threshold forced low → assert a full fire completes (`DRY_RUN_WRITTEN`, well-formed payload) and the payload's window is 100% res-1; a second pass with the threshold forced high → `BELOW_THRESHOLD` + streak path, all res-1. Use an **isolated copy** of the bin output (NOT the live book — see the v36 live-test lesson: running against the live `analysis_log.csv` fires the schema migrations).

`dotnet build` solution + AutoTweaker + harness all 0/0; `dotnet run --project verify/ordercheck` → ALL PASS.

---

## 8. Commit checklist

- [ ] `tweaker_config.json`: `population_filter = { "session": "NY", "execution_resolution": 1 }`.
- [ ] `TweakerConfig.vb`: `PopulationFilter` POCO + property.
- [ ] `AutoTweaker.vbproj`: add `Core/ExecutionResolution.vb`.
- [ ] `ForwardWindowJoiner.vb`: `CsvRow.ExecResolution` + header-name parse (default 1).
- [ ] `AutoTweakerCore.vb`: hoist `settings`; load-time filter + `filtered`/`filtered.Count` threading; `MatchesPopulation`; reroute trigger session-name through `MatchSessionBucket`; sliding-arm guard.
- [ ] `TweakerState.vb`: `PopulationFilterKey` + re-seed-on-change in `RunAsync`.
- [ ] `PromptBuilder.vb`: HARD CONSTRAINT 11 text adds `resolution_profiles.*`.
- [ ] `SettingsDiffApplier.vb`: `RejectedPathPrefixes` (`kelly.`, `resolution_profiles.`) + `scoring.min_tradeable_move_pct` exact-match reject in `Validate` (NOT `ValidateSnapshotContent`).
- [ ] `verify/` harness: A15a–A15g + the dry-run integration replay; A1–A14 unregressed.
- [ ] `DeribitIndicatorProject.md` §12: mark the auto-tweaker resolution-awareness item *Phase-2a addressed (population filter + code-level off-surface guard)*; note Phase-2b (per-population windowing / history-segregation / auto-tuning) + the manual `resolution_profiles["3"]` re-baseline carry-forward.
- [ ] **Local commit only.** Trader compiles + isolated-replay-validates + supervises the first NY×1 fire before any push. Spec-back routes to the coordinator seat.

---

## 9. Out of scope (do not build — proposal §6)

(B) the manual `resolution_profiles["3"]` re-baseline (the Asia/London accuracy fix — separate settings pass, data-gated); (C) auto-tweaker Phase-2b: per-population `LastEvaluatedRowIndex` / round-robin, per-population `WindowSize`/`MinTier`/threshold, picked-cell/round-history segregation by population, and the schema-home decision for where session-specific tuned values live. No `settings.json` engine change here — this is a tweaker-tooling change; the engine is untouched.
