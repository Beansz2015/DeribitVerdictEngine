# Tier C — Dataset-Protection Fixes (Kickoff)

**Date:** 2026-06-10
**Implementer model:** Opus (mechanical, tightly specified; no in-flight design judgement needed)
**Spec basis:** `docs/fable5-audit-report.md` findings M3–M7 + S-3, confirmed against source 2026-06-10. This kickoff is the spec — Tier C items are spec-light by agreement (they have zero scoring impact and protect the calibration dataset everything else depends on).
**Relationship to other work:** independent of `docs/engine-correctness-pass-proposal.md`. Can land before or in parallel. If the correctness pass lands first, re-grep all line anchors (its commits touch some of the same neighbourhoods).

## Session start (read in this order)

1. `CLAUDE.md` (repo root) — build commands, collaboration rules, host-agnostic constraint.
2. This kickoff.
3. The audit report sections for M3–M7 (`docs/fable5-audit-report.md` §1, rows M3–M7; §2 row S-3; §3 auto-tweaker notes) — for mechanism detail.

Do not read unrelated engine files. **Verify every file:line below against the current tree before editing** — anchors drift.

## Hard constraints

- **Zero scoring impact.** Do not touch `Core/ScoringEngine_*`, `Core/Indicators_*`, `DynamicNorms.vb`. If a fix seems to require it, stop and report back instead.
- **No settings.json changes** — no keys, no values, no version bump. (C-3 touches POCO *defaults* in `EngineSettings.vb` only; that file change does not bump the JSON version.)
- Host-agnostic rule: anything under `tools/` and `analysis/` gets no WinForms references. Duplicating a 15-line helper into the AutoTweaker project is preferred over cross-project coupling.
- Local commits only — one commit per item (C-1 may combine its two write-sites). Never push. No `MainForm.Designer.vb` edits.
- `dotnet build` clean after every commit.

## Items

### C-1 (M3a) — Atomic settings.json writes

`SettingsLoader.Save` (`Core/Settings/SettingsLoader.vb`, ~:54-70) and `SettingsDiffApplier.Apply` / `ApplyRevert` (`tools/AutoTweaker/SettingsDiffApplier.vb`, ~:192 and ~:257) write `settings.json` with bare `File.WriteAllText` — a mid-write crash truncates the most critical file in the repo. The repo already has the approved pattern: `TweakerState.Save` (`tools/AutoTweaker/TweakerState.vb`) writes to a `.tmp` then `File.Replace` (the 2026-05-17 audit-cleanup pass). Apply the same pattern at all three sites. Duplicate the helper into the AutoTweaker project rather than referencing across projects.

### C-2 (M3b) — Surface settings parse failure

A corrupt/truncated `settings.json` at startup currently parse-fails to `Console.WriteLine` only, and the engine silently runs on POCO defaults. Surface it: expose the load failure from `SettingsLoader` (e.g. a `LastLoadError As String` set in the catch), and have MainForm check it at startup and show a persistent status-bar message ("settings.json parse failed — running on code defaults"). The status-bar cascade lives in `UI/MainForm_Layout.vb`; follow the v18 skip-counter precedent for tone/placement. Keep the console line (future CLI host).

### C-3 (M3c) — Re-align POCO defaults with live calibration

POCO defaults in `Core/Settings/EngineSettings.vb` have drifted from live `settings.json` values — they only matter on the silent-defaults path (C-2's scenario), which is exactly when drift is dangerous. Known drifted (audit-confirmed): `OiSettings.ChangeThresholdPct` 0.01 vs live 0.002; `FundingSettings.MomentumThreshold` 0.0001 vs live 0.00001; `ScoringSettings` funding bands at pre-v22 values (live: ±0.00008 high, ±0.00001 low). **Do not spot-fix just these three** — walk every POCO default against the live file and align all of them (v15 did this once; it drifted again). If the engine-correctness pass has landed by then, the live file is v31 (`indicators.OBV.trend_gate = 10.0`) — align to whatever is current.

### C-4 (M4) — Culture-invariant CSV formatting

`AnalysisLogger.vb` (~:126-213, the 87-column row build) and its `AppendPickedCell` format numerics with culture-sensitive `ToString("F…")`. Every parser in the repo uses `InvariantCulture`, and the v26+ cache writers already format invariantly (`FormatEvalEntry` is the in-repo pattern). On a comma-decimal locale every numeric field splits the row — total silent corruption; latent today (en-MY is dot-decimal) but the Linux port is on the roadmap. Add a private `Inv(...)` helper using `CultureInfo.InvariantCulture` and route every numeric format in both writers through it. **Verification:** capture one logged row before and after the change on this machine — must be byte-identical.

### C-5 (M5) — Auto-tweaker stall guard after CSV shrink

`tools/AutoTweaker/AutoTweakerCore.vb` (~:108-129): `state.LastEvaluatedRowIndex` is an absolute row index; the CSV can shrink (schema-mismatch rotation — has happened twice — or the UI Reset Log link, or the planned data reset). Row count < stale index → INELIGIBLE forever, silently. Fix: when `currentRowCount < state.LastEvaluatedRowIndex`, re-seed the index to `currentRowCount`, persist state, and log a clearly-worded warning line. Do not tick the streak.

### C-6 (M6) — Reject unknown settings paths in tweak diffs

`tools/AutoTweaker/SettingsDiffApplier.vb`: `Validate` (~:109-125) skips the stale-value check when the proposed path doesn't resolve in current settings, and `Apply` (~:153-165) then **creates** the unknown key — a typo'd path from the model becomes a silent no-op tweak recorded as APPLIED. Fix: `Validate` rejects any diff item whose path does not resolve in the current settings tree; `Apply` never creates keys.

### C-7 (S-3, rider) — Session-boundary check is date-blind

`tools/AutoTweaker/AutoTweakerCore.vb` (~:690-702): `CrossesSessionBoundary` compares hour fields only — two timestamps in the same clock-hour on different days return False. Compare full timestamps: any span ≥ 24 h is a boundary crossing, and the hour-walk between the two `DateTime`s must account for the date. Mirror logic exists in `UI/TweakSettingsForm.vb` (~:317, "Identical logic to AutoTweakerCore.CrossesSessionBoundary") — fix both, keep them identical.

## Verification (beyond per-item notes)

- Build clean after each commit.
- C-1: manual save from the UI (e.g. output-dump settings dialog) → file intact, no stray `.tmp` left.
- C-5/C-6/C-7: exercise via AutoTweaker dry-run (`tweaker_config.json` has `dry_run_enabled: true` by default) against a **copy** of the CSV where feasible; for C-5, hand-shrink the copy. Do not let any test run write a real settings tweak — confirm `dry_run_enabled` before running.
- Report back: per-item commit hashes + one line each on how it was verified. User tests, then pushes.
