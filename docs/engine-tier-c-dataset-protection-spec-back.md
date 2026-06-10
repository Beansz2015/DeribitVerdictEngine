# Tier C — Dataset-Protection Fixes — Spec-Back

**Reports against:** `docs/engine-tier-c-dataset-protection-kickoff.md` (2026-06-10)
**Implementer:** Opus 4.8, 2026-06-10
**Status:** All 7 items (C-1 … C-7) implemented, build-clean per commit, **local commits only — nothing pushed.** Awaiting trader test + push per the local-first workflow.
**Constraints honoured:** zero scoring impact (no edits to `Core/ScoringEngine_*`, `Core/Indicators_*`, `DynamicNorms.vb`); no `settings.json` change, no version bump; host-agnostic rule kept (helpers duplicated into AutoTweaker, never cross-referenced); one commit per item; `dotnet build DeribitVerdictEngine.sln` clean after every commit.

---

## 1. What shipped

| Item | Hash | Subject | Files |
|---|---|---|---|
| C-1 (M3a) | `a86532a` | Atomic `settings.json` writes at all 3 sites | `Core/Settings/SettingsLoader.vb`, `tools/AutoTweaker/SettingsDiffApplier.vb` |
| C-2 (M3b) | `bea79a8` | Surface settings parse failure in status bar | `Core/Settings/SettingsLoader.vb`, `UI/MainForm_Layout.vb` |
| C-3 (M3c) | `dada03f` | Re-align POCO defaults with live v30 | `Core/Settings/EngineSettings.vb` |
| C-4 (M4) | `230b9b8` | Culture-invariant CSV formatting | `AnalysisLogger.vb`, `analysis/FailureRateMatrix.vb` |
| C-5 (M5) | `980ae8d` | Auto-tweaker stall guard after CSV shrink | `tools/AutoTweaker/AutoTweakerCore.vb` |
| C-6 (M6) | `70397ed` | Reject unknown settings paths in tweak diffs | `tools/AutoTweaker/SettingsDiffApplier.vb` |
| C-7 (S-3) | `57de688` | Date-aware session-boundary check (both mirrors) | `tools/AutoTweaker/AutoTweakerCore.vb`, `UI/TweakSettingsForm.vb` |

All anchors re-greped against the live tree before editing (the kickoff warned they drift). Line numbers below are post-change.

---

## 2. Per-item detail

### C-1 (M3a) — Atomic settings.json writes
**Spec:** route `SettingsLoader.Save` + `SettingsDiffApplier.Apply`/`ApplyRevert` through the proven `TweakerState.Save` tmp + `File.Replace` pattern; duplicate the helper rather than cross-couple projects.
**Done:** added a private `AtomicWriteAllText(path, content)` to **each** class (writes `path & ".tmp"`, then `File.Replace` if the target exists else `File.Move`, deleting the tmp on exception). `SettingsLoader.Save` writes through it inside the existing write-lock; both `SettingsDiffApplier` writers route through their own copy. All three named sites covered in one commit (kickoff allowed combining C-1's write-sites).
**Verify:** build clean; on success `File.Replace`/`Move` consumes the `.tmp` (no stray left), on failure the `Catch` deletes it. UI manual-save (output-dump settings dialog) left for trader testing.
**Deviation:** none.

### C-2 (M3b) — Surface settings parse failure
**Spec:** expose load failure from `SettingsLoader`; MainForm shows a persistent status-bar message ("settings.json parse failed — running on code defaults"); follow the v18 skip-counter precedent; keep the console line.
**Done:** added `SettingsLoader.LastLoadError` (private field + `ReadOnly` property), set in the `LoadFromDisk` catch and cleared on a successful load; the `Console.WriteLine` is retained for the future CLI host. `MainForm_Layout.UpdateLogInfo` now prepends the kickoff's verbatim warning string to the LOG line when `LastLoadError` is non-empty.
**Verify:** build clean. Placement note below.
**Deviation (placement):** rather than a one-shot startup write, the warning lives inside `UpdateLogInfo` (the same method that owns the v18 ` · skipped N` suffix), so it is recomputed every render → **persistent across renders and self-healing** (clears automatically if a corrected `settings.json` later hot-reloads cleanly). This matches "follow the v18 skip-counter precedent for tone/placement." Wording is the kickoff string exactly. Mid-session hot-reload failures also set the flag; the message still reads "running on code defaults" in that edge case (engine is actually on last-good settings, not defaults) — acceptable since the actionable signal ("settings.json didn't parse") is correct; flagging in case you want stricter wording.

### C-3 (M3c) — Re-align POCO defaults with live calibration
**Spec:** don't spot-fix the 3 audit-named drifts — walk **every** POCO default vs live and align all. (Live is v31 only if the correctness pass landed; else v30.)
**Done:** correctness pass has **not** landed → aligned to **live v30** (so `OBV.trend_gate` stays `0.001`, already matching). Full field-by-field walk of `EngineSettings.vb` against `settings.json`. The **only** scalar drifts were the funding/OI cluster:

| Field | Old default | Live v30 |
|---|---|---|
| `OiSettings.ChangeThresholdPct` | 0.01 | 0.002 |
| `FundingSettings.MomentumThreshold` | 0.0001 | 0.00001 |
| `ScoringSettings.FundingHighPositive` | 0.0003 | 0.00008 |
| `ScoringSettings.FundingLowPositive` | 0.00005 | 0.00001 |
| `ScoringSettings.FundingHighNegative` | −0.0003 | −0.00008 |
| `ScoringSettings.FundingLowNegative` | −0.00005 | −0.00001 |

Inline default-drift comments updated; the stale "Default 0.0001 (1 bp)" doc-comment on `MomentumThreshold` corrected (and de-mislabelled — the audit §4 noted the "1 bp" label was wrong; it's 0.001% = 0.1 bp).
**Verify:** field-by-field diff; everything else matched live. POCO-only fields absent from `settings.json` (`CvdSettings.DivergencePenalty`, `MicroCvdSettings.DecelPenalty`, the `hold_*` block) are the operative live values and were left unchanged.
**Deviation / judgment call (the one item beyond the audit's named scalars):** `SessionVolumeSettings.Sessions` defaulted to an **empty list**, while live carries the 3 ASIA/LONDON/NY buckets and the class comment documents them as the "intended defaults." An empty default silently disables **all** session-volume scaling on the code-defaults path — a genuine calibration divergence, and exactly the danger C-3 exists to close. I populated the default with the live buckets. It is a settable `List`, so System.Text.Json **replaces** it wholesale on a successful load (no duplication of the live buckets; default only applies on the defaults path). **If you want C-3 kept strictly scalar, this hunk can be reverted on its own** — it is self-contained.

### C-4 (M4) — Culture-invariant CSV formatting
**Spec:** add a private `Inv(...)` invariant-culture helper to `AnalysisLogger` (87-col row) and its picked-cell writer; route every numeric format through it; verify a logged row is byte-identical before/after on this machine.
**Done:** the picked-cell writer is `analysis/FailureRateMatrix.AppendPickedCell` (the kickoff compressed this; audit M4 located it). Added `Imports System.Globalization` + a private `Inv(value As Double, fmt As String)` to **both** files (duplicated, host-agnostic). Every floating-point `ToString("F…")` now routes through `Inv` (45 in the row build, 4 in `AppendPickedCell`). Integer/Boolean `ToString()` left as-is (locale-safe).
**Verify:** proved byte-identity directly instead of capturing one literal row (stronger — covers all widths/signs, not just whatever one run logged): CurrentCulture = **en-MY**, and all **45** value × {F0,F2,F4,F6,F8} combinations (incl. negatives and zero) are identical current-culture vs invariant. The change only diverges on a comma-decimal host — the corruption it prevents.
**Deviation:** none.

### C-5 (M5) — Auto-tweaker stall guard after CSV shrink
**Spec:** when `currentRowCount < state.LastEvaluatedRowIndex`, re-seed the index to `currentRowCount`, persist, log a clear warning, don't tick the streak.
**Done:** inserted a shrink guard in `AutoTweakerCore.RunAsync` after the first-run-init block and **before** the fixed-mode window-full check (which is where the negative new-row count silently produced permanent INELIGIBLE). Logs `WARNING — CSV shrank below LastEvaluatedRowIndex (rows=N < index=M); re-seeding index to N…`, re-seeds, persists, returns INELIGIBLE (exit 2). No streak tick, no `RoundSummary` (it's a reset, not a round).
**Verify:** **ran the live AutoTweaker binary** against an isolated shrunk CSV (50 rows) with `state.last_evaluated_row_index = 4000` → guard fired, index re-seeded 4000→50, exit 2, settings copy untouched at v30, `DryRun=True` confirmed.
**Deviation:** none.

### C-6 (M6) — Reject unknown settings paths in tweak diffs
**Spec:** `Validate` rejects any diff item whose path doesn't resolve in current settings; `Apply` never creates keys.
**Done:** `SettingsDiffApplier.Validate` now rejects when `NavigatePath` returns Nothing ("Rejected: path '…' does not resolve in current settings (no key creation)."), before the stale-value check (which is preserved for resolved paths). `Apply` gains a `parent.ContainsKey(key)` guard — it only overwrites an existing leaf and logs (never creates) an unknown path.
**Verify:** **ran the live binary** via `--apply-manual` (no API key needed): typo path `indicators.RSI.overbough` → rejected "does not resolve … (no key creation)", exit 1; control (known path `indicators.RSI.overbought` with wrong `old_value`) → still reaches the existing "Stale diff" rejection, exit 1 — proving known paths resolve and stale-checking is intact; settings copy stayed v30 (no key created, no apply).
**Deviation (minor, additive):** `Apply`'s defence-in-depth branch logs the skipped unknown path to `Console.Error` rather than silently dropping it. This branch should be unreachable post-`Validate`; the log aids the headless host. No behavioural change on valid diffs.

### C-7 (S-3) — Date-blind session-boundary check
**Spec:** compare full timestamps (span ≥ 24h = crossing; hour-walk must account for date); fix both `AutoTweakerCore.CrossesSessionBoundary` and the identical `TweakSettingsForm.FormCrossesSessionBoundary`, keeping them identical.
**Done:** replaced both bodies with a date-aware version: order the two timestamps; if `(later − earlier).TotalHours >= 24` return True; else walk real `DateTime` top-of-hour boundaries in `(earlier, later]` testing each `.Hour` against the session-start set. The two function bodies are byte-identical (only the name + outer comment differ). The old `If h1 = h2 Then Return False` (the date-blind bug) is gone, and the walk now normalises arg order (callers pass them in opposite orders).
**Verify:** replicated the exact new logic and tested the audit's failing case (14:30 yesterday → 14:05 today, starts {0,13}) → now **True** (was False); plus order-swap, sub-hour same-hour (False), within-day cross/no-cross, boundary-at-endpoint, and 24h+ span — all correct.
**Deviation:** none. (Semantics: interval is half-open `(earlier, later]`, preserving the original's inclusion of the later endpoint's hour; conservative — auto-tweaker skips rather than mixes sessions.)

---

## 3. Environmental note — pre-existing build break I had to clear

The solution build was **already failing at session start**, unrelated to Tier C: the Fable-5 audit's throwaway harness `verify/ordercheck/` (its own `.vbproj` + a `Program` module) was being swept into the **root** project's default `**/*.vb` glob, producing `module 'Program' conflict` + duplicate-assembly-attribute errors. It is gitignored + untracked and the audit report explicitly marks it "safe to delete." I removed only that one subdirectory (left the unrelated `verify/*.png` screenshots). Without it, no commit could be build-verified. Recorded as a memory so the glob gotcha is recognised next time.

---

## 4. Doc drift spotted (not fixed — flagging for the spec writer)

- `docs/architecture.md` calls the AutoTweaker project `AutoTweaker.csproj`; it is actually **`AutoTweaker.vbproj`** (VB.NET). Same in the §3 file table of `DeribitIndicatorProject.md` ("AutoTweaker.csproj — separate .NET 8 project").
- `architecture.md` header still says "App version: settings.json v29" and the directory-layout comment says "All tunable parameters v25"; handover is v30. Pre-existing drift.
- **Version history:** Tier C is a no-`settings.json`-bump maintenance bundle. Per CLAUDE.md, behaviour-changing commits get a §15 entry in `DeribitIndicatorProject.md`. These are zero-scoring-impact and bump nothing, but the precedent (2026-05-17 audit-cleanup pass, 2026-05-24 P3 maintenance) is a **post-v30 §15 row with no version bump**. I did **not** edit the handover doc — recommend a single "post-v30 (2026-06-10) — Tier C dataset-protection bundle" §15 entry; your call on wording.

---

## 5. Downstream implications

- **No happy-path behaviour change.** C-1/C-2/C-4/C-5/C-6/C-7 are pure robustness/observability; C-3 only changes the **code-defaults path** (corrupt/absent `settings.json`). When `settings.json` loads normally, the engine is byte-for-byte unchanged.
- **No recalibration triggered.** Unlike the C1/C2/H1 correctness items in the audit, nothing here changes the meaning of any logged column or any scoring input on the normal path. The C-4 CSV format is byte-identical on this (dot-decimal) host.
- **C-3 makes the defaults path safer**, not different-on-happy-path: a silent-defaults run now matches live calibration (funding/OI thresholds + session buckets) instead of pre-v22 values.

---

## 6. Open decisions for the spec writer

1. **C-3 sessions-list hunk** — keep (aligns the defaults path fully, my recommendation) or revert to strictly-scalar C-3? Self-contained either way.
2. **C-2 wording** on the mid-session hot-reload-failure edge — keep the kickoff's "running on code defaults" string, or add a separate "latest settings.json didn't parse — kept last-good" variant for that case?
3. **§15 version-history entry** — want a post-v30 Tier C row added to `DeribitIndicatorProject.md`? (I left the handover untouched.)
4. **Doc-drift fixes** (§4) — fold the `.csproj`→`.vbproj` + version-string corrections into a docs pass, or leave?

---

## 7. How to re-verify (artifacts / commands)

- **Build:** `dotnet build DeribitVerdictEngine.sln` → 0/0.
- **C-4 culture proof:** any host — compare `x.ToString("Fn")` vs `x.ToString("Fn", InvariantCulture)`; identical on dot-decimal locales, divergent on comma-decimal.
- **C-5 / C-6 live-binary exercise:** copy `settings.json` + a CSV + a crafted `state.json` to a temp dir, point a `tweaker_config.json` copy at the copies (`dry_run_enabled: true`), then
  - C-5: shrink the CSV below `state.last_evaluated_row_index`, run `dotnet AutoTweaker.dll --config <copy>` → expect the shrink WARNING + exit 2 + re-seeded index.
  - C-6: `dotnet AutoTweaker.dll --apply-manual <diff-with-typo-path>.json --config <copy>` → expect "does not resolve … (no key creation)" + exit 1 + unchanged version.
- **C-7:** unit-replicate `CrossesSessionBoundary` against the cross-day same-hour case.

All verification this session ran against isolated temp copies; the real `settings.json`, `state.json`, and `settings_snapshots/` were never touched (confirmed via `git status`).

---

*Commits are local on `master`. Trader tests, then pushes per the local-first workflow.*
