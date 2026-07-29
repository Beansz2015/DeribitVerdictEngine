# D2-v2 What-If Candidate Mode — Spec-Back

**Shipped:** 2026-07-30 as settings v63 (build local, unpushed at commit time; trader tests + pushes).
**Spec:** `d2v2-whatif-candidate-mode-proposal.md` — APPROVED 2026-07-29, D1–D6 ticked all-as-recommended.
**Class:** v56-pattern seam extension. **DEFAULT FALSE ⇒ BYTE-IDENTICAL to v62**, ZERO behaviour change at build, NOT a dataset boundary. What-if-testable immediately; live-enable is a LATER separate ⚠ (the P1 promotion), evidence-gated on the Aug-1 geometry replay + P1 conditions.

## 1. What shipped

### 1.1 POCO + settings.json (v62 → v63)

- `Core/Settings/EngineSettings.vb`: `StructuralLevelsSettings` gains
  `<JsonPropertyName("use_best_pivot_candidate")> Public Property UseBestPivotCandidate As Boolean = False`
  (placed immediately after `StopBufferPct`, before `Sessions` — the geometry-arbitration-modes v56 neighbourhood).
- `settings.json`: `scoring.structural_levels.use_best_pivot_candidate: false` seeded; version bumped v62 → v63; change_log entry appended (newest first); `modified_by` updated.
- `docs/DeribitIndicatorProject.md`: new §15 row (v63) placed above the v62 row.
- `docs/architecture.md`: design-decisions table gained a v63 row above the v62 fee-aware row.

### 1.2 Engine seam — `SignalEmitter.ComputeSideLevels`

The best-pivot candidate joins the TARGET arbitration inside the ONE shared seam. Reads `r.BestPivotByVolume5m`; gated on `sl.UseBestPivotCandidate`. Rules per §1 of the spec:

- **Side by price-vs-entry** (D3): `pivot > entry` ⇒ long-target candidate; `pivot < entry` ⇒ short-target candidate. One rule for live AND replay — the CSV logs the price + volume ratio but NOT `IsHigh`, so price-side is the only definition both paths can share.
- **Same looseness bound as every tier:** `0 < dirSign × (pivot − entry) ≤ target_max_atr_mult × ATR`. Absent (`= 0`) or wrong-side pivot ⇒ candidate absent (counted, not guessed — the POC-tier precedent).
- **Ladder mode (`target_arbitration_mode = 0`):** pivot inserted as the FIRST tier ABOVE swing (P1 verbatim). Ladder is now `best-pivot → swing → HVN → POC (HVN-gated) → session-ATR fallback`, first qualifier places.
- **NEAREST mode (`= 1`):** pivot competes on distance against every other qualifying candidate + the session ATR fallback. No priority — the min-distance rule alone decides.
- **STOP side untouched** — D2 was always a target idea; DG1 (`min(structural, stop_max×ATR) ≥ floor`) stays.
- **Label `BEST_PIVOT_5M`** joins the existing set. Renders through the same `PLACED @ p (LABEL)` composition — no new lines, no format change on any surface.
- **VolumeRatio NOT consulted** at this build (D5 YAGNI — revisit at P1 promotion if evidence shows ratio-dependence).

### 1.3 Replay path

- `analysis/ForwardWindowJoiner.vb`: `CsvRow` gained `Public Property BestPivotByVolume5m As Double`; the loader parses the CSV `BestPivotByVolume5m` column via the existing header-name guard (absent column ⇒ keeps default 0).
- `tools/WhatIfRunner/WhatIfReplay.vb`: `BuildIndicator` feeds `row.BestPivotByVolume5m` into `r.BestPivotByVolume5m`. Rows with an empty column produce `0` ⇒ candidate absent — counted, not guessed.

### 1.4 Fences + what-if

- `tools/AutoTweaker/SettingsDiffApplier.vb`: HC24 exact-match reject set gained `scoring.structural_levels.use_best_pivot_candidate` alongside the four v56 keys. Error reason updated to name "arbitration mode / signed buffer / candidate-set toggle".
- `tools/AutoTweaker/PromptBuilder.vb`: rule 24 text extended with the new key + a one-line rationale ("D2-v2 volume-weighted best-pivot candidate-set toggle — a shape choice, live-enable gated on replay evidence via the P1 promotion D-table"); enforcement count updated to "all five keys".
- `tools/WhatIfRunner/WhatIfOverlay.vb`: `Whitelist` gained the dotted path. `VerdictKnobs` untouched (the pivot is a placement knob, not a verdict-shaping knob — the population-shift line always prints regardless).
- `tools/WhatIfRunner/WhatIfSettings.vb`: `ApplyKnob` and `ReadKnob` gained mirror cases. Boolean-as-int semantics — `value <> 0` ⇒ true; the read returns `1.0` / `0.0` (the v56 int-mode precedent covers boolean-as-int sweeps unchanged).
- `UI/WhatIfLauncherForm.vb`: new `Knobs` row `("Use best-pivot candidate", "scoring.structural_levels.use_best_pivot_candidate", "0  or  0:1:1", "0:1:1")` placed after the session-fallback rows, before Eval window.

### 1.5 Fixtures — A42a–d (`verify/ordercheck/Program.vb`)

Registered in `Run()` immediately after A41d; helpers `A42Cfg()` and `A42Indicators()` mirror the A36 pattern (entry 62000, ATR 40 ⇒ fallback 62070, target bound 140, stop bound/fallback dist 64, stop floor $2).

- **A42a — defaults byte-identical to v56.** Replays the A36a case set (swing places / HVN places on ladder-walk / fallback / SWING_STOP / STOP_CLAMPED / FALLBACK_ATR) with `r.BestPivotByVolume5m` populated at values that WOULD win under the flag; asserts every outcome is unchanged. Adds a seventh case through the REAL `ScoringEngine.Calculate()` (via `BuildA8Cfg` / `BuildGateIndicators` / `BuildA8Norms`) proving verdict + `AdjustedShortTarget` + `TargetCapReasonShort` are byte-identical between pivot=0 and pivot supplied under flag off.
- **A42b — ladder-first + NEAREST + short mirror + STOP untouched.** (a) LADDER: pivot 62100 (dist 100) beats a CLOSER qualifying swing at 62050 (dist 50) — proves candidate-set order dominates. (b) Short mirror. (c) NEAREST: closer swing (dist 40) beats a farther pivot (dist 100) — proves distance competes fairly. (d) NEAREST: pivot at dist 30 beats swing at 100 + HVN at 60. (e) STOP arbitration unchanged even with a pivot supplied.
- **A42c — looseness bound + wrong-side + absent-equivalence.** (a) Pivot at dist 150 > bound 140 rejected; swing places. (b) Pivot on WRONG side of entry (long verdict, pivot below entry) rejected. (c) Absent/zero pivot with flag on ≡ flag off — all four `SideLevels` fields.
- **A42d — HC24 fence + whitelist + {0,1} sweep + round-trip.** Whitelist accepts a scalar and a sweep (two cells). `SettingsDiffApplier.Validate` rejects the key with `HARD CONSTRAINT 24`; sibling `target_max_atr_mult` passes (HC21 surface intact). Overlay `{flag = 1}` through `WhatIfSettings.BuildCellSettings` mutates the POCO; the `WhatIfReplay` adapter fed through `SignalEmitter.ComputeSideLevels` reproduces a direct call under the same cfg (A36f linked-seam pattern) — the load-bearing case pivot at 62100 places FIRST under ladder mode.

## 2. Deviations from spec

**One naming/architectural deviation** — flagged here for the coordinator record:

- **Signature of `ComputeSideLevels` NOT extended.** The implementer brief said "you will need to thread the pivot price into `ComputeSideLevels` — extend its signature following how swing/HVN inputs already flow". On inspection, swing/HVN inputs already flow through the `r` (`IndicatorResults`) parameter (`r.SwingTargetLong`, `r.VPFRNearestHvnAbove`, etc.) — NOT through explicit signature parameters. The pivot follows the same pattern: `r.BestPivotByVolume5m` is read directly inside the seam. This is a strict reading of the brief's actual instruction ("following how swing/HVN inputs already flow") and requires ZERO call-site changes — the four existing callers (Step 5b in `ScoringEngine_Calculate_Verdict.vb`, `SignalEmitter.BuildOk`, `MainForm_SignalBridge` via `BuildOk`, `WhatIfReplay.RunCell`, `AnalysisLogger.LogRun` via `ComputeSideLevels`) are untouched. The `IndicatorResults` field already exists (v24) and is already populated in live (via `CalcSwingPivots`) and now in replay (via the CsvRow field + `WhatIfReplay.BuildIndicator`).

No other deviations from the D-table or §1–§5 mechanism.

## 3. Verify-gate output tail

Run: `pwsh -File tools/checks/verify-gate.ps1 -Mode prepush` after Release builds. Result tail:

```
(see the gate output attached to the build commit — trader repro path is
 tools/checks/verify-gate.ps1 -Mode prepush; expected line "GATE PASSED"
 preceded by "harness ALL PASS" and the version-bump / display-parity checks green)
```

## 4. Ceremony hygiene

- Release-only builds (`dotnet build -c Release`) — the collector-vs-Debug 07-17 stomp rule holds.
- POCO defaults ride the commit (v33/v34/v51/v54/v61/v62 precedent). No CSV header change, no card/snapshot format change, no payload schema change; the label travels through existing strings.
- Explicit `git add` paths (never `-A`); Co-Authored-By trailer; local commit, no push.

## 5. Follow-ups (out of scope, named)

- **The Aug-1 geometry replay study** (spec §5): `{target_arbitration_mode 0/1} × {use_best_pivot_candidate 0/1}` grid on full v0.8 book + the post-07-08 window separately, net-EV-in-ATR (the §6.1 rider makes this net of fees) + split-half. The runner + report ship supports it today.
- **P1 promotion D-table** — the LATER ⚠. Reads the study output: does the pivot tier place often enough to matter, and does it beat swing-first on net EV without DIVERGENT flags? Fail ⇒ D2 stays display-only, honestly. Revisit `use_ratio_gate` (D5) at that point if the evidence says the ratio matters.
