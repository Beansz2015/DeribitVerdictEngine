# AutoTweaker weekday-only row filter — implementation spec

**Status:** ✅ **APPROVED — D-1…D-5 ALL TICKED AS RECOMMENDED, trader-directed 2026-08-25.** Clear to implement. The D-table in §4 is the decision of record; where this document disagrees with itself elsewhere, §4 wins.
**Author seat:** Opus, 2026-08-25. **Origin:** [`weekday-scope-ruling-2026-08-03.md`](weekday-scope-ruling-2026-08-03.md) · [`trader-tick-queue.md`](trader-tick-queue.md) §2 "Weekday filters — 3 surfaces" · [`roadmap.md`](roadmap.md) W5.

---

## 0. Model + effort — **Sonnet, effort MEDIUM**, one session

**Why that tier.** The judgment work is done in this document, and every mechanical piece has an in-repo template: the load-time population filter (`AutoTweakerCore.vb:114-130`) is the exact shape the new filter takes, the re-seed gate (`:150-168`) already exists and absorbs the change for free, and fixture family `A15a–e` — particularly `A15e`, which drives the real `AutoTweakerCore.RunAsync` against a temp CSV/settings/state — is the fixture template. **No new design decisions remain once §4 is ruled.**

**Where Sonnet will specifically slip — four traps, all named in §5 with the code that causes them:**

1. **The cursor coordinate system.** `LastEvaluatedRowIndex` indexes the **post-filter** list, while `RoundSummary.WindowStartRow`/`WindowEndRow` carry **absolute CSV** indices. Two coordinate systems in one state file.
2. **`DateTime.MinValue.DayOfWeek` is MONDAY.** A row whose timestamp failed to parse passes a naive weekday filter as a valid Monday row.
3. **`CrossesSessionBoundary` has a `>= 24h` early-out.** Removing weekends makes every Friday→Monday adjacency trip it.
4. **`ConditionsExtractor` re-reads raw CSV lines by absolute index**, so filtered-out rows re-enter the prompt.

⚠ **The fixtures cannot be relied on to catch traps 2 and 3, because the implementer writes the fixtures too** — a misunderstanding of either propagates into its own test. §6 therefore specifies those two fixtures by their **required failing input**, not by their intent.

**Escalation trigger — stop and move to Opus/high if:** the D-3 fix to `ConditionsExtractor` cannot be made without changing `RoundSummary`'s schema, **or** any change proves necessary inside `CrossesSessionBoundary`. Both mean the blast radius has left this spec.

---

## 1. What this fixes, and why it is worth a slot now

**The weekday-scope ruling of 2026-08-03 made evaluation weekday-only.** Verified 2026-08-25: it is enforced in code in exactly **two** places — `tools/BacktestRunner/CoverageReport.vb:550` and `tools/CeilingAudit/CsvFeatureBuilder.vb:199-203`.

⛔ **`tools/AutoTweaker/` contains ZERO references to `DayOfWeek`, weekday or weekend** — confirmed across all eleven files. **And the AutoTweaker is the only surface in this repo that WRITES `settings.json`.** Unfiltered, it would tune the engine on sessions that are never traded, and the result lands in the file every other component reads.

### ⚠ Correction to the standing evidence — "never fired" is right, "no state file" is wrong

[`roadmap.md`](roadmap.md) and [`trader-tick-queue.md`](trader-tick-queue.md) both justify this item as safe-to-do-now with *"verified never fired — no tweaker state file, `settings_snapshots/` empty."* **There IS a state file.** `tools/AutoTweaker/state.json` (untracked) records:

```
last_run_at_iso          2026-06-18T13:37:17Z
last_run_outcome         SKIPPED_INSUFFICIENT_TIER
last_evaluated_row_index 81
population_filter_key    NY|1
round_history            1 round, skipped
picked_cell_history      []
```

**The conclusion survives — it has never proposed or applied a settings change, and `settings_snapshots/` is genuinely empty. But it is LIVE, not dormant.** It ran, it skipped on tier-eligibility, and its cursor advanced to 81. **The gate keeping it quiet is data, not design.** That strengthens the case for doing this now rather than weakening it.

---

## 2. Verified map of the code being changed

**Every claim below carries file:line and was verified in the tree on 2026-08-25. Do not re-derive; do verify anything you are about to depend on.**

| # | Fact | Site |
|---|---|---|
| 2.1 | Rows load as `List(Of CsvRow)` | `AutoTweakerCore.vb:112` → `analysis/ForwardWindowJoiner.vb:107` |
| 2.2 | Population filter applied **at load time, before windowing** | `AutoTweakerCore.vb:114-130` |
| 2.3 | Predicate | `AutoTweakerCore.MatchesPopulation`, `:809-819` |
| 2.4 | **Cursor indexes the POST-FILTER list** | `AutoTweakerCore.vb:221-229`; also `:161`, `:179`, `:195`, `:209` all compare `filtered.Count` |
| 2.5 | Population key + re-seed gate | `AutoTweakerCore.vb:150-168` |
| 2.6 | ⚠ Re-seed resets the cursor **only** — round history, picked-cell history and the streak all survive | `AutoTweakerCore.vb:161-165` |
| 2.7 | `CsvRow.Timestamp` is `DateTime`, `Kind = Unspecified` | `ForwardWindowJoiner.vb:35`, parsed `:128-134` |
| 2.8 | ⭐ **Parsed `TryParseExact`, `"yyyy-MM-dd HH:mm:ss"`, `InvariantCulture`** | `ForwardWindowJoiner.vb:128-134` |
| 2.9 | ⭐ **The CSV `Timestamp` column is UTC** | `AnalysisLogger.vb:180` — `DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")` |
| 2.10 | Session-boundary check, pairwise on adjacent **filtered** rows | `AutoTweakerCore.vb:244-269`, predicate `:826-840` |
| 2.11 | Tier eligibility — verdict string only | `AutoTweakerCore.vb:272-276` |
| 2.12 | `TweakerState` has **no schema version**; `Load` fails open | `TweakerState.vb:130-140` |
| 2.13 | UI mirrors that must move in lockstep | `UI/TweakSettingsForm.vb:126-128` (key), `:274-324` (`CountPopulationRows`), `:408-422` (boundary twin) |

### ⭐ 2.9 is the fact this spec rests on, and it needed checking

`CsvRow.Timestamp` carries `Kind = Unspecified`, so `.DayOfWeek` is computed from the raw Y/M/D fields. **That is a UTC day-of-week only because `AnalysisLogger.vb:180` writes the column from `DateTime.UtcNow`.** The type does not carry it; the writer convention does.

⛔ **This is settled, not open.** [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §7 recorded a latent defect — *"the book's `Timestamp` column is LOCAL time"* — and it is **REFUTED as of 2026-08-25**. §7 was read in full and its central claim checked three ways:

- `AnalysisLogger.vb:180` assigns `ts` from `DateTime.UtcNow`, `ts` is never reassigned, and it is the first field of `sw.WriteLine(String.Join(",", ts, …))` at `:194`.
- **`AnalysisLogger.vb` contains ZERO references to `VerdictResult.Timestamp`.**
- The eval cache is UTC (`BuildLiveEntry` → `BuildEntry(nowUtc, …)`, serialised `{0:o}`) and the bridge is UTC (`generated_at_utc`).

**`verdict.Timestamp = DateTime.Now` reaches only the rendered `TIME` line and the output dump — both display, the latter deliberately local.** The refutation and its downstream consequences live at [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md) §7.0.

⭐ **Consequence for this spec: the ground is FIRMER than when §2.9 was first written, not weaker.** `CsvRow.Timestamp` is parsed from a column its writer stamps in UTC, so `.DayOfWeek` is a UTC day-of-week by construction and the filter is correct on any host.

---

## 3. Mechanism

**Fold the weekday test into the existing load-time filter. Do not add a second filtering stage.**

**3.1 — Predicate.** Extend `MatchesPopulation` (`:809-819`), or add a sibling applied in the same `Where` at `:121-123`:

```vb
' Weekday-scope ruling 2026-08-03: evaluation is weekday-only.
' MinValue guard FIRST - DateTime.MinValue.DayOfWeek is MONDAY, so an
' unparsed timestamp would otherwise pass as a valid weekday row.
If r.Timestamp = DateTime.MinValue Then Return False
Dim dow As DayOfWeek = r.Timestamp.DayOfWeek
If dow = DayOfWeek.Saturday OrElse dow = DayOfWeek.Sunday Then Return False
```

⚠ **Matches `CsvFeatureBuilder.vb:198-203` deliberately** — same guard order, same two-day test, same `CsvRow.Timestamp` source. **One convention across the three surfaces.**

**3.2 — Key participation.** Add a weekday term to `populationKey` at `:150-154` (e.g. `"NY|1|WD"`). **This is what makes the change safe for free:** the stored key is `"NY|1"`, so the first run after deployment takes the existing re-seed branch, moves the cursor to `filtered.Count`, and does not evaluate that round. **The cursor at 81 currently indexes a list that includes weekend rows; without a key change it would silently point at the wrong row after filtering.**

**3.3 — UI lockstep.** Mirror the term into `TweakSettingsForm.vb:126-128` and the weekday test into `CountPopulationRows` (`:274-324`), **or the UI shows "Re-seed pending" permanently and its row count disagrees with the core.** ⚠ Note `CountPopulationRows` parses timestamps with `TryParse` + `AssumeUniversal Or AdjustToUniversal` — **a different parse path from the core's `TryParseExact`.** Do not unify them in this spec; do make the weekday test behave identically on well-formed rows.

**3.4 — Telemetry.** Extend the existing population line at `:124-130` rather than adding a new one. **AutoTweakerCore has no `LoadStats` object; introducing one for this is scope creep** (D-4).

---

## 4. D-table — ✅ **ALL FIVE TICKED AS RECOMMENDED, trader-directed 2026-08-25**

**Every row went the way the recommendation pointed. This table is now the decision of record.**

| # | Decision | ✅ RULED | Rationale carried into the build |
|---|---|---|---|
| **D-1** | Config key, or unconditional? | ✅ **(a) UNCONDITIONAL — no new key** | The weekday-scope ruling is unconditional; a key on the one surface that writes `settings.json` is a switch that can be flipped back. **The one-time re-seed still fires, because the key STRING changes** (`"NY\|1"` → `"NY\|1\|WD"`) even though the behaviour is not optional |
| **D-2** | ⚠ The weekend-gap burn — see §5.3 | ✅ **(a) ACCEPT, and record the observable** | (b) would touch a shipped function carrying an explicit *"keep identical to `TweakSettingsForm.FormCrossesSessionBoundary`"* contract (`AutoTweakerCore.vb:825`) — double blast radius. ⭐ **P9 in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §16.6 already exists for exactly this waste.** ⛔ **This spec RAISES P9's trigger rate and must say so in its §15 row. It must not absorb P9's fix** |
| **D-3** | ⛔ The `ConditionsExtractor` leak — see §5.4 | ✅ **(a) FIX IT HERE — in scope** | ⚠ **Without it the spec does not achieve its own purpose** — weekend rows re-enter the prompt and the tweaker still reasons over untraded sessions. It also closes the pre-existing population-filter leak in the same move. **See the escalation trigger in §0: if this cannot be done without changing `RoundSummary`'s schema, STOP and escalate** |
| **D-4** | Telemetry convention | ✅ **(a) LOCAL COUNTER + CONSOLE**, extending the line at `:124-130` | Consistency inside the file beats consistency with a different tool. ⛔ **`RoundSummary` has no exclusion field and must not grow one for this** |
| **D-5** | Version bump / §15 row? | ✅ **No settings change, NO version bump. ONE §15 row. NO display-parity obligation** | Touches `tools/AutoTweaker/` and `UI/TweakSettingsForm.vb` only. §15 row per the settings-untouched precedent (C1 coverage report). **State the parity exemption explicitly per the hard rule** — the tweaker has no snapshot line, card binding, CSV column or bridge field |

---

## 5. The four traps, with the code that causes them

**5.1 — Two coordinate systems in one state file.** `state.json` carries `last_evaluated_row_index` in **filtered** coordinates and `round_history[].window_start_row`/`window_end_row` in **absolute CSV** coordinates (`AutoTweakerCore.vb:241-242`, from `CsvRow.Index`). **Do not "fix" this to agree.** Preserve it; §5.4 is where it bites.

**5.2 — ⚠⚠ `DateTime.MinValue.DayOfWeek` is `Monday`.** `0001-01-01` is a Monday, and `MinValue` is the sentinel for a timestamp that failed `TryParseExact` (`ForwardWindowJoiner.vb:128-134`). **A naive `If dow = Saturday Or dow = Sunday Then exclude` therefore ADMITS every unparsed row as a Monday.** `CsvFeatureBuilder.vb:198` guards `MinValue` first for precisely this reason. **The guard is not defensive padding; it is load-bearing.**

**5.3 — ⚠⚠ The weekend gap trips the session-boundary check unconditionally.** `CrossesSessionBoundary` (`:826-840`) opens with:

```vb
If (later - earlier).TotalHours >= 24.0 Then Return True
```

Filtering out Saturday and Sunday makes the Friday-last-row → Monday-first-row adjacency roughly **63 hours**. **Every fixed window spanning a weekend will therefore emit `SKIPPED_SESSION_BOUNDARY` and advance the cursor by a full `WindowSizeVerdicts` without evaluating those rows.** At the shipped `window_size_verdicts` of 75 that is one burned window per weekend.

⚠ **Pre-existing in kind, worse in degree:** the shipped `NY|1` filter already removes hours 0–12, so consecutive filtered rows across a midnight already cross hour 0. **This adds a much larger hole.** ⛔ **Do not quote a burn rate — it depends on row cadence and window size, and nobody has measured it.** The observable is in §7.

**5.4 — ⛔ `ConditionsExtractor` re-reads raw CSV lines by absolute index.** `ConditionsExtractor.vb:115-120`:

```vb
Dim startIdx As Integer = Math.Max(1, round.WindowStartRow + 1)
Dim endIdx As Integer = Math.Min(lines.Length - 1, round.WindowEndRow + 1)
For r As Integer = startIdx To endIdx
```

**It iterates every raw line in the span, including rows the filter excluded.** Called from `AutoTweakerCore.vb:501-502`, `:736-737`, `:747-748`, `:762-763`. **Weekend rows would re-enter conditions extraction and reach the prompt.** This is D-3.

---

## 6. Fixtures — family **A59** (next free; `A58c` is the high-water mark, counted 2026-08-25)

**Template: `verify/ordercheck/Program.vb:1245-1370`, the `A15a–e` population-filter family. `A15e` (`:1328-1370`) drives the real `AutoTweakerCore.RunAsync` against a temp CSV/settings/state — copy that shape.**

| Fixture | Asserts | **Required failing input** |
|---|---|---|
| **A59a** | Weekday rows survive, Saturday and Sunday rows are excluded; the kept/total count is reported | A CSV holding rows on **both** weekend days and at least two weekdays |
| **A59b** | The key change triggers exactly one re-seed: cursor → `filtered.Count`, outcome `INELIGIBLE`, round not evaluated | Pre-seed `state.json` with `population_filter_key = "NY\|1"` — the **live** value |
| **A59c** | ⚠ **Pins the D-2 burn so it cannot change silently** — a window spanning a weekend emits `SKIPPED_SESSION_BOUNDARY` | Rows that are **contiguous across a Friday→Monday gap after filtering** |
| **A59d** | ⛔ **Conditions extraction sees no weekend row** (D-3) | A round whose absolute `WindowStartRow..WindowEndRow` span **contains** weekend lines |
| **A59e** | ⚠⚠ **The `MinValue` trap** — a row with an unparseable timestamp is EXCLUDED, not admitted as Monday | A CSV row whose `Timestamp` cell is malformed, e.g. ISO-8601 with a `T` separator, which `TryParseExact` rejects |

⛔ **MUTATION-PROOF REQUIRED, and this is not optional — it is this project's standing convention.** For each of **A59c** and **A59e**, revert the guard it tests and confirm the fixture **FAILS**. ⚠ **A fixture that passes on the unfixed code proves nothing about the fix** — three fixtures in the `A55` family had to be re-written after they were found to pass against the defect they were written for.

---

## 7. Acceptance

- **Builds:** solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck, **0/0 Release**, each run separately.
- **Harness:** `ALL PASS`, `A1`–`A58c` unregressed, plus `A59a`–`A59e`.
- **Gate:** `tools/checks/verify-gate.ps1` — expect **no** version-bump nudge (D-5: no settings key moves).
- **Post-change observable, to be read from `state.json` after the first weeks of running:** the count of `SKIPPED_SESSION_BOUNDARY` entries in `round_history`. ⭐ **This is the D-2 measurement and the P9 trigger.** If it dominates the outcomes, P9 becomes worth building — **record the count, do not act on it inside this spec.**
- ⚠ **The tweaker is data-gated and will very likely not fire during acceptance.** Do not treat "no round fired" as a failure, and do not manufacture a fire to prove it works — `A59b` and `A59c` exist because the live path cannot be exercised on demand.

---

## 8. Out of scope — named so they are not assumed back in

- **P9** (`SKIPPED_SESSION_BOUNDARY` advancing by a full window rather than to the boundary). This spec **raises its trigger rate** and must say so in the §15 row.
- **`CrossesSessionBoundary` itself** — untouched under D-2(a).
- **The `TryParseExact` vs `TryParse` split** between `AutoTweakerCore` and `TweakSettingsForm.CountPopulationRows`. Real, pre-existing, and a separate item.
- **`FailureRateMatrix.Compute` called without the `resolution` argument** at `AutoTweakerCore.vb:413-415`, defaulting to `1`. Correct only while the population is pinned to a res-1 session. **Unrelated to this change but in the same call path** — flagged, not fixed.
- **The other two weekday surfaces** — `LivePerformanceTracker` and `AnalysisRunner`/`WhatIfRunner`. ⚠ `LivePerformanceTracker.vb:560`'s `DayOfWeek` is **Monday week-anchoring, not a filter**; do not mistake it for one. Expect *fewer* rendered cells once it is filtered, since n shrinks against `min_sample_for_render`.
- **The local-time question in §2.9.** ✅ **Settled 2026-08-25 — REFUTED, the book is UTC.** Nothing is owed to this spec from it. ⚠ **One thing IS owed elsewhere:** the constraint *"collector hosts run UTC"* was relayed to the hostel-app seat citing this defect, and the defect is not real. **Re-ground the constraint or downgrade it to a convention** — [`seat-handover-2026-08-24.md`](seat-handover-2026-08-24.md) §2.1.
