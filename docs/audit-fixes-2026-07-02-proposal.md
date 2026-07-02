# Audit Fixes — F1/F2/F3 + Nits (spec for implementer)

**Status:** ✅ **APPROVED 2026-07-02** — trader signed off all four §7 decisions **as recommended** (D1 remove, D2 take the next version now, D3 keep snapshot + document, D4 fence `scoring.hold_`). For D3/F3 the trader will additionally run the observational watch: during live holds, watch for EXIT GUARD strip latches the next run's HOLD\EXIT row does not corroborate; if they read as alarm fatigue, flip to the §4 align-branch as a contained follow-up. **Implementer-ready — no open decisions.**

**Live-collection caution (added at sign-off):** the v47 geometric OFI data collection is running from 2026-07-02 through ~end of the NY session on the standalone Debug exe. Do not disturb it: Release builds only (§0 already requires this — never build Debug), and the commit-1 edit to `bin\...\settings.json` is hot-reload-safe by construction (the dead-key removal changes no value the engine reads; the 8 F2 keys are already present in the bin copy — effective behaviour is unchanged, so the FileSystemWatcher reload is a behavioural no-op mid-collection). Sync the bin copy's `version` field to the new number in the same edit so tracked and bin agree.
**Source:** `docs/fable5-audit-2026-07-02.md` (the 2026-07-02 full audit; read it first — it carries the evidence for every item here).
**Recommended implementer:** Opus 4.8, standard/medium effort, single conversation. Everything here is mechanical with exact anchors — no novel design, no scoring-math changes. Fable is not needed.
**Baseline:** origin/master `a00ac35` + the two local docs commits (`b758edc` handover, `0804308` audit report). settings.json currently **v46** — read line 1 before bumping; bump from whatever is current.

## 0. Repo rules that bind this work (summary for a fresh conversation)

- **Local-first. NEVER push** — the trader tests + pushes. Commit locally per the plan in §5. Exclude the 3 pre-existing dirty docs (`p3-maintenance-pass-proposal.md`, `ui-reskin-handover-2026-05-22.md`, `websocket-migration-p1-spec-back.md`) and all untracked non-engine files (`.codex/`, `configure-claude-deepseek.ps1`, `models-full.json`, `tools/*.ps1` UI-automation scripts, `tools/tools/`, `docs/*.txt`).
- **Gate:** `tools/checks/verify-gate.ps1 -Mode prepush` must be green before you start (baseline) and after every commit (3 Release builds 0/0 + harness A1–A20i + parity/version guards). Run it, don't rebuild it. The trader's Debug exe may be running — the gate builds Release, which avoids the file lock; do not build Debug.
- **Display-string parity rule:** none of these items adds/removes/renames a rendered line. State that explicitly in each commit message that touches display-adjacent code (N1, F3).
- **`MainForm.Designer.vb` is never edited.** No item here touches UI layout.
- **Version bump:** items F1+F2 change settings schema → one `version` bump + one `change_log` entry (newest-first) + a §15 row in `DeribitIndicatorProject.md` + the §6 pointer. See D2 in §7 for the numbering decision.

---

## 1. F1 — remove the dead key `regime_gates.transitional_adx_penalty_low`

**Why:** orphaned by v31 F8 (the first TRANSITIONAL penalty arm covers `[0, penalty_mid)` — `Core/ScoringEngine_Calculate_Verdict.vb:70–75` reads only `TransitionalAdxPenaltyMid`/`High`). The key is read nowhere, but it *resolves* in settings.json and is on no reject list, so an auto-tweaker proposal against it would validate → apply → version-bump → be recorded APPLIED as a silent no-op, corrupting failure-rate attribution (the C-6 class). Must land **before** the first live tweaker fire.

**Change (pending D1 = remove, the recommendation):**
1. Delete `"transitional_adx_penalty_low": 20.0` from the tracked `settings.json` (`regime_gates` block, ~line 399) **and** from `bin\Debug\net8.0-windows\settings.json` (the copy the live app + tweaker read).
2. Delete the POCO property `TransitionalAdxPenaltyLow` (`Core/Settings/EngineSettings.vb:799`). Safe: System.Text.Json ignores unknown JSON properties, so any stale file still carrying the key deserialises fine.
3. **Do NOT add the key to `SettingsDiffApplier.RejectedPathFragments`.** The v15 precedent (oi_prev15m etc.) predates the snapshot system; every `settings_snapshots/` snapshot taken since v31 *contains* this key, and `ValidateSnapshotContent` checks fragments on wholesale reverts — a fragment entry would poison all existing snapshots. Post-removal, C-6 already closes the proposal path completely (unresolvable path → Validate rejects; Apply never creates keys). If belt-and-braces is wanted, note in the change_log that pre-removal snapshots remain revertable and would harmlessly restore the (unread) key.

**Acceptance:** new harness fixture (next free A-series id, e.g. A21a): `SettingsDiffApplier.Validate` against the post-change settings JSON **rejects** a diff on `regime_gates.transitional_adx_penalty_low` (unresolvable-path reason), and **accepts** a diff on the live sibling `regime_gates.transitional_penalty_mid` (the block stays tunable). Grep proves zero remaining references.

## 2. F2 — sync the 8 missing keys into the tracked settings.json

**Why:** `SettingsLoader.Save` serialises the full POCO, so the bin copy already carries these 8 keys; the tracked file doesn't. The two copies present different config/tweaker surfaces, and a fresh deploy from git differs from the live bin until the first UI save.

**Change:** add to the tracked `settings.json`, values = POCO defaults (verified identical to the bin copy — zero behaviour change):
- `indicators.CVD.divergence_penalty: 1`
- `indicators.MicroCVD.decel_penalty: 1`
- `scoring.hold_roc_take_profit_long: 0.6`, `scoring.hold_roc_take_profit_short: -0.6`
- `scoring.hold_rsi_hold_long: 60.0`, `scoring.hold_rsi_hold_short: 40.0`
- `scoring.hold_rsi_evaluate_long: 40.0`, `scoring.hold_rsi_evaluate_short: 60.0`

(Take the exact 8 values from the **bin** copy / POCO defaults at implementation time — the shorts listed here are the POCO defaults per `EngineSettings.vb:621–628`; verify each rather than trusting this table.)

**Placement:** keep JSON block order consistent with the bin copy so future diffs of tracked-vs-bin are clean.

**Tweaker-surface consequence (deliberate):** `decel_penalty` and `divergence_penalty` are genuine Step-2 scoring penalties → legitimately tweaker-tunable on both copies after this (same philosophy as putting `avg_window_sec` on-surface in v46). The 6 `hold_*` keys are the trader's hold/exit discipline with **no failure-rate linkage** (HoldStatus never feeds the failure matrix) → fence them per D4.

## 3. D4 (recommended) — fence `scoring.hold_*` off the tweaker surface

**Change (pending D4):** add `"scoring.hold_"` to `SettingsDiffApplier.RejectedPathPrefixes` (`tools/AutoTweaker/SettingsDiffApplier.vb:63–70`, with a comment: trader hold-discipline preference, no failure-rate linkage — same class as `kelly.*`) + PromptBuilder **HARD CONSTRAINT 17** stating the six keys are hand-tuned and that sibling `scoring.*` tunables stay proposable. Prefix semantics are safe here: all six keys start `scoring.hold_` and no tunable key shares the prefix.

**Acceptance:** fixture A21b: Validate **rejects** `scoring.hold_rsi_hold_long`, **accepts** `scoring.verdict_med_pct`.

## 4. F3 — rule the exit-guard OFI input (recommendation: KEEP SNAPSHOT, document it)

**The drift:** since v46 the full run's `OFISignal` on the healthy-WS path is the geometric time-averaged ratio (and `CalcHoldStatus` inside the run consumes it), while `ExitGuardEvaluator.Evaluate` (`ExitGuardEvaluator.vb:89`) feeds the shared primitive from snapshot `CalcOFI`. The adverse *definitions* are shared; the OFI *input* is not. The equivalent snapshot choice in `LiveMicrostructureEvaluator` was ruled and documented (v46 spec-back §3); the guard's was not.

**Recommendation (D3 = keep snapshot):**
- The guard's purpose is fast reaction to raw tape *between* runs at a 3s cadence; the averaged ratio is a ~`avg_window_sec` (10s)-lagged signal by construction — aligning would blunt the guard's entire reason to exist.
- The twitchiness is bounded: a single OFI-adverse can never fire EXIT (D3 ruling dropped the Warn tier; EXIT needs **2+ adverse** or a structural break), so snapshot OFI only matters as the *marginal second vote* — and reacting to a real sweep that coincides with a second adverse signal is correct exit behaviour, not noise.
- Cost asymmetry favours twitchy on the exit path: a slightly early EXIT cue costs re-entry consideration; a 10s-blunted one costs money.

**If D3 = keep (recommended):** doc/comment only — (a) a short comment block at `ExitGuardEvaluator.vb:89` stating the ruling and rationale; (b) one amendment line in `time-averaged-ofi-spec-back.md` §3 (next to the existing strip ruling): "ExitGuardEvaluator also deliberately stays on snapshot CalcOFI — ruled 2026-07-0X: raw-tape reaction is the guard's purpose; single-OFI-adverse cannot fire EXIT alone, bounding the twitchiness."
**If D3 = align:** wire the averaged read into the guard, mirroring `RunAnalysisAsync`: `Evaluate` gains the averaged branch — when `cfg.Indicators.OFI.AveragingEnabled` and `state.GetOfiAverage(cfg.Indicators.OFI.AvgWindowSec).HasWarmup`, set `r.OFIRatio/OFISignal` from the snapshot's `Ratio` via `IndicatorEngine.ClassifyOfiRatio`; else snapshot `CalcOFI` (identical fallback chain). ~8 lines; add a fixture mirroring A20c's constant-fold setup asserting the guard's OFI signal matches the averaged classification.

**Better-decision path (how to validate the ruling cheaply):** no new instrumentation needed — the two surfaces are both already live during a hold: the HOLD\EXIT row (averaged, per run) and the EXIT GUARD strip (snapshot, 3s). Disagreements are directly observable: watch a few held positions for strip EXIT latches that the next run's row does not corroborate. If those latches feel like alarm fatigue in practice, flip D3 to "align" using the ready-made branch above — it's a contained follow-up, not a re-spec.

## 5. Nits N1–N4

**N1 (code):** `DynamicNorms.Compute` gains `Optional utcHour As Integer = -1`; `ApplySessionVolume` takes the hour as a parameter instead of reading `DateTime.UtcNow.Hour` itself (`DynamicNorms.vb:125`); `-1` ⇒ fall back to `DateTime.UtcNow.Hour` (keeps every existing caller and harness fixture byte-identical). `RunAnalysisAsync` passes its captured `utcHour` (`MainForm_Analysis.vb:58` → the `DynamicNorms.Compute(candlesExec, r.ATR)` call at :204) so execRes and the volume bucket can never resolve from different hours at an hour rollover. While in the file: rename the stale `candles1m` parameter → `candles` (callers are positional; verify no named-argument call sites with a grep for `candles1m:=`).

**N2 (docs/comment):** `Core/ScoringEngine_Helpers.vb` header comment (priority list, ~:155–161) and `DeribitIndicatorProject.md` §9 both list OBV-divergence before the ROC<0 exit; the code returns the ROC<0 "momentum break" EXIT first. Both produce EXIT so this is message-selection only, and harness A17g pins the code as canonical — **fix the comment and the doc**, not the code.

**N3 (doc only):** with `mtf_gate.enabled: false` (non-default, tweaker-unreachable via `DisabledGatedPaths`), a failing gate still composes `MTF BLOCK [...]` as the reason string while no block occurs (`_Verdict.vb:110–111` doesn't consult `Enabled`). Do **not** change the code — the three reason formats are locked by design. Add a row to `architecture.md` → *Display Behaviour Clarifications* documenting it as a config-edge display quirk.

**N4 (docs):** P5b render-reality doc-rot. Update: (a) `CLAUDE.md` UI table + data-flow snippet — `MainForm_Render_Header.vb`/`MainForm_Render_Sections.vb` were deleted in P5b; the text surfaces are `UI/MainForm_PlaintextSnapshot.vb` (`BuildPlaintextSnapshot`) + `UI/MainForm_Render_Cards.vb`; restate the parity rule accordingly. (b) `architecture.md` directory tree + the render section of the data-flow diagram, same correction. Keep edits surgical — do not restructure either doc.

## 6. Commit plan + acceptance

| # | Commit | Contents | Gate |
|---|---|---|---|
| 1 | `fix(settings): F1+F2 — drop dead transitional_adx_penalty_low, sync 8 default-riding keys` (+ D4 fence if approved) | tracked + bin settings.json, `EngineSettings.vb:799` removal, SettingsDiffApplier/PromptBuilder (D4), version bump + change_log + §15 + §6, fixtures A21a/A21b | prepush green |
| 2 | `docs(exit-guard): F3 ruling — snapshot OFI input, rationale` (or `feat(exit-guard): align OFI input to averaged ratio` if D3=align) | comment + spec-back amendment (or the 8-line branch + fixture) | prepush green |
| 3 | `fix(norms): N1 — thread the run's utcHour into ApplySessionVolume` | `DynamicNorms.vb`, `MainForm_Analysis.vb` (one arg) | prepush green |
| 4 | `docs: N2/N3/N4 — hold-status ordering text, MTF disabled-edge clarification, P5b render-surface doc-rot` | CLAUDE.md, architecture.md, DeribitIndicatorProject.md §9, `ScoringEngine_Helpers.vb` comment | prepush green |

Global acceptance: gate green after every commit; A1–A20i unregressed + new A21a/A21b (and the D3=align fixture if applicable); **no rendered-line change anywhere** (say so in commits 2–3); CSV v0.7 / eval cache v4 untouched; scoring path byte-identical except nothing (F1/F2 values equal current effective behaviour; N1 changes only the hour source within the same hour).

## 7. Sign-off decisions — ✅ ALL APPROVED AS RECOMMENDED (trader, 2026-07-02)

| # | Decision | Ruling |
|---|---|---|
| D1 | F1: remove key+POCO vs fence-only | ✅ **Remove** (v15 dead-key precedent; C-6 then closes the tweaker path automatically; no RejectedPathFragments entry — §1 step 3) |
| D2 | Version numbering | ✅ **This pass takes the next version** (read settings.json line 1 and bump from current); the OFI threshold re-baseline slides to the number after |
| D3 | F3: exit guard OFI input | ✅ **Keep snapshot + document** (§4 doc/comment path). Trader runs the live observational watch — strip-vs-row corroboration during holds; the §4 align-branch is the ready-made fallback if alarm fatigue shows |
| D4 | Fence `scoring.hold_*` from the tweaker (HC17) | ✅ **Yes.** `decel_penalty`/`divergence_penalty` stay tunable — real Step-2 levers |
