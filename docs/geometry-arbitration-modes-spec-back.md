## Geometry Arbitration Modes + Signed Buffers · Spec-Back

**Built:** 2026-07-22 · **Spec:** `geometry-arbitration-modes-proposal.md` (G1-G6 ALL TICKED — G1/G2/G3/G5/G6 2026-07-21, G4 2026-07-22, BUILD-AUTHORIZED).
**Type:** engine seam extension in `SignalEmitter.ComputeSideLevels`; settings v55 → **v56** (four new POCO keys, all defaults byte-identical to v51 B4b — zero live impact at build). NOT a dataset boundary. Live activation of any non-default mode/buffer is a later, separate ⚠ D-table gated on replay evidence (and, for the stop side, on consumer sizing-by-stop-distance — L3).
**State:** local commit; solution + AutoTweaker + WhatIfRunner + OrderCheck build **0/0** Release; harness **180 checks ALL PASS** (174 pre-A36 + 6 new A36a-f); verify-gate `prepush` **GATE PASSED**.

---

## 1. What was built

Four new `structural_levels` keys turn the shipped v51 B4b ladder + tight-stop shape into a knob-selectable arbitration, so the geometry-shape question (ladder vs closest-first target; tightest vs widest stop; pullback/protection buffers) becomes a standing what-if study rather than a one-shot code-first proposal. All defaults preserve v51 B4b byte-identically — the knobs are a what-if instrument at build; live activation is later and evidence-gated.

| Key | Default | Meaning |
|---|---|---|
| `target_arbitration_mode` | **0** | 0 = ladder (v51 B4b verbatim: swing → HVN → POC → session-ATR fallback, priority-with-bound). 1 = **NEAREST**: among qualifying structural candidates (in-bound `0 < dist ≤ target_max_atr_mult × ATR`, same POC HVN-gating as today) AND the session-resolved ATR fallback target, place whichever is CLOSEST to entry. Fallback wins ⇒ TargetReason = `FALLBACK_ATR`, `Capped=False`, `Reason=Nothing` — matches today's fallback semantics. |
| `stop_arbitration_mode`   | **0** | 0 = tightest (v51 B4b DG1 verbatim: `min(structural swing stop, stop_max_atr_mult × ATR) ≥ floor`, `STOP_CLAMPED` semantics kept). 1 = **WIDEST**: `max(structural swing stop distance, stop_max × ATR)`, still `≥ 4-tick floor`, UNCLAMPED above. Label truthfully — `SWING_STOP` when structure wins wider, `FALLBACK_ATR` when the ATR distance wins. |
| `target_buffer_pct`       | **0.0** | Signed % of the placed target's distance from entry, applied AFTER arbitration. Negative shaves toward entry (the trader's pullback); positive pushes beyond. Formula: `placed' = entry + (placed − entry) × (1 + pct/100)`. |
| `stop_buffer_pct`         | **0.0** | Signed % of the placed stop's distance from entry, same shape. Positive pushes the stop farther from entry (the trader's buffer); negative tightens. The 4-tick floor applies to the BUFFERED stop (a punishing negative buffer snaps to the floor rather than crossing entry). |

At the defaults (mode 0/0, buffer 0.0/0.0) `ComputeSideLevels` returns bit-for-bit the same `SideLevels` v51 B4b did — proven by A36a running the entire A26 case set through the new code with an explicit default cfg. The A26 fixtures themselves remained unmodified and continue to pass — the parity guarantee is doubly-pinned (A26 tests the shipped behaviour; A36a tests that the shipped behaviour is what the default path produces under v56).

---

## 2. Files touched

| File | Change |
|---|---|
| `Core/Settings/EngineSettings.vb` | `StructuralLevelsSettings` gained four properties: `TargetArbitrationMode` (Integer, default 0), `StopArbitrationMode` (Integer, default 0), `TargetBufferPct` (Double, default 0.0), `StopBufferPct` (Double, default 0.0). All carry `<JsonPropertyName>` snake-case aliases matching settings.json. Each XML comment states the default byte-identity invariant + the fence class (HC24). |
| `Core/SignalEmitter.vb` | `ComputeStructuralSideLevels` rewritten to seam the two modes + apply the two buffers. Structure: (a) shared candidate collection (swing/HVN/POC qualification flags + fallback), (b) mode-0 ladder branch = v51 B4b verbatim / mode-1 NEAREST branch = pick closest qualifying candidate incl. fallback, (c) buffer application on placed target with cap-noise suppression against fallback ATR, (d) mode-0 stop branch = DG1 verbatim / mode-1 WIDEST branch = max(structural dist, stop_max×ATR), (e) buffer application on placed stop with 4-tick floor snap. Every mode-1/buffer branch is gated so `mode = 0 AndAlso pct = 0` skips it entirely — the default execution path traces the SAME instructions v51 shipped. New `Private Shared FormatBufferSuffix(pct)` renders the ` BUF ±N%` reason-string tag (only appended when buffer ≠ 0). |
| `settings.json` | Version 55 → **56**; `last_modified` + `modified_by` refreshed; newest-first `change_log` entry describes the four keys and the byte-identity invariant. New `structural_levels.target_arbitration_mode: 0`, `stop_arbitration_mode: 0`, `target_buffer_pct: 0.0`, `stop_buffer_pct: 0.0`. |
| `tools/WhatIfRunner/WhatIfOverlay.vb` | `Whitelist` HashSet gains the four dotted paths. Numeric leaves — integer modes sweep via the existing numeric machinery (A36e proves a `{0,1}` sweep parses to two cells with `IsSweep=True`). |
| `tools/WhatIfRunner/WhatIfSettings.vb` | `ApplyKnob` gains four cases (int modes via `CInt(value)`, buffers as raw doubles); `ReadKnob` gains the mirror four cases (constraint resolution + report vs-live marking). |
| `tools/AutoTweaker/SettingsDiffApplier.vb` | New exact-match rejection block for the four keys (HC24; HC11 class — hand-ruled geometry). Immediately after the HC21 `enabled` / `stop_too_loose_mode` block for locality. Prefix-safe: the flat `target_max_atr_mult` / `stop_max_atr_mult` / `stop_min_floor_ticks` remain proposable (HC21 unchanged). |
| `tools/AutoTweaker/PromptBuilder.vb` | New rule **24** in the system message: describes the four keys, why they're off-surface (shape choice, not failure-rate threshold), the LIVE activation gate (later ⚠ D-table + L3 for the stop-widest side), and reasserts the HC21 flat surface stays tunable. |
| `verify/ordercheck/Program.vb` | New A36a–f registrations + fixture bodies (~200 lines). Case set spelled out below §5. |
| `verify/ordercheck/OrderCheck.vbproj` | One new `<Compile Include>` for `tools/WhatIfRunner/WhatIfSettings.vb` — the linked-seam pattern A30a needs to prove the overlay-application path actually reaches the new POCO fields (A36f). |
| `docs/DeribitIndicatorProject.md` | §15 one-line entry (v56 settings-bump row). |

---

## 3. Decisions the spec left to the implementer

Two calls not covered by the G-table.

**(a) Reason-string buffer suffix format.** The proposal says "the reason string may carry a suffix — implementer's choice, document it". Chose `" BUF ±N%"` appended to the existing `"PLACED @ P (LABEL)"` — reads naturally and stays inside the same JSON field. Sign is explicit via `String.Format(inv, " BUF {0}{1:0.###}%", sign, pct)` (positive gets a literal `+`; negative gets `-` from the Double formatting) so `+10%` and `-5%` render unambiguously. Suffix appears ONLY when the buffer is non-zero AND the buffered price differs from the fallback ATR by ≥ the sub-tick noise floor (the same suppression the pre-existing `"PLACED @"` reason already uses); at defaults (`pct = 0`) the string is byte-identical to v51 B4b.

**(b) Fallback-wins-under-buffer semantics in NEAREST mode.** When mode 1 picks the fallback as closest-to-entry AND `target_buffer_pct ≠ 0`, the buffered fallback price is materialised into `lv.Target`, `TargetReason = FALLBACK_ATR`, and `Capped/Reason` are set with the `PLACED @ P (FALLBACK_ATR) BUF ±N%` suffix if the buffered price moved beyond the noise floor. At `pct = 0` this branch is silent (Capped=False, Reason=Nothing) — byte-identical to v51's fallback path. This routing is a slight deviation from "fallback wins ⇒ FALLBACK_ATR / no cap reason": it holds *at defaults*, but a non-zero buffer legitimately CAPS the fallback (the price the engine placed is no longer the raw ATR fallback), so the display should say so honestly. Non-issue at build because live buffer=0.

---

## 4. Additions beyond the §1/§2 inventory

- **`verify/ordercheck/OrderCheck.vbproj` gained `WhatIfSettings.vb`** as a `<Compile Include>`. A36f exercises the overlay-application path (`WhatIfSettings.BuildCellSettings`) end-to-end so the coordinator's "verify the overlay-application path actually reaches the new POCO fields" requirement has a runtime proof, not just a code-inspection claim. Trivial addition — the file is host-agnostic and already builds cleanly under WhatIfRunner.vbproj.

No other additions.

---

## 5. Acceptance (spec §3)

| Requirement | Result |
|---|---|
| Builds 0/0 (Release) | Solution + AutoTweaker + WhatIfRunner + OrderCheck all **0 errors / 0 warnings**. Release-only per the collector-protection rule. |
| Harness unregressed + new A36 family | **180 checks ALL PASS**, 0 failures (174 pre-A36 + 6 new). Every pre-existing A26 fixture continues to pass unchanged — the parity guarantee for v51 B4b. |
| **(a) Defaults byte-identical** — THE load-bearing fixture | **A36a** runs the entire A26 case set (swing places farther than fallback / tier walk to HVN / no tier → FALLBACK_ATR / SWING_STOP / STOP_CLAMPED / FALLBACK_ATR stop) through `ComputeSideLevels` under an explicit default cfg (mode 0/0, buffer 0/0). All six placements equal the v51 B4b outputs bit-for-bit — Target, TargetReason, Reason string, Capped, RawTarget, StopPx, StopReason. |
| **(b) Nearest picks minimum-distance qualifying candidate incl. fallback beating a farther swing** | **A36b** — under mode-1 target: fallback (dist 70) beats swing 62100 (dist 100) ⇒ Target=62070, TargetReason=FALLBACK_ATR (the load-bearing case); closer swing 62050 (dist 50) beats fallback ⇒ Target=62050, TargetReason=SWING_HIGH_5M; HVN 62040 (dist 40) beats both swing+fallback ⇒ Target=62040, TargetReason=NEAREST_HVN_ABOVE; short mirror confirmed. |
| **(c) Widest stop picks max and respects the 4-tick floor** | **A36c** — under mode-1 stop: wider swing 61900 (dist 100) beats bound 64 ⇒ StopPx=61900, StopReason=SWING_STOP (mode 0 would STOP_CLAMPED at 61936); tighter swing 61950 (dist 50) loses to bound 64 ⇒ StopPx=61936, StopReason=FALLBACK_ATR; no structural stop ⇒ FALLBACK_ATR at bound; a punishing −99% stop_buffer_pct snaps to the 4-tick floor (61998); short mirror confirmed. |
| **(d) Signed buffers move each side right direction and the min-move gate reads buffered prices** | **A36d** — target `+10%` at placed 62100 ⇒ 62110 (farther); `−5%` ⇒ 62095 (closer). Stop `+10%` at placed 61950 ⇒ 61945 (wider); `−20%` ⇒ 61960 (tighter). Reason string carries `BUF +10%` / `BUF -5%`. Gate proof through the REAL `ScoringEngine.Calculate`: at ATR 20 with swing target dist 60 (SWING_LOW_5M places, dist 60 > floor 49.6 → stands); adding `target_buffer_pct = -40%` reduces the placed distance to 36 < 49.6 ⇒ NO TRADE / BELOW_MIN_MOVE. Proves the buffered price flows through Step 5b onto `Adjusted*` and Step 5c reads it. |
| **(e) Whitelist accepts 4 keys; HC24 fence rejects them; a sibling still passes** | **A36e** — WhatIfOverlay parses the four dotted paths cleanly (including a `{0,1}` sweep expanding to two values with `IsSweep=True`); the SettingsDiffApplier rejects each of the four keys with a `HARD CONSTRAINT 24` error; the sibling `scoring.structural_levels.target_max_atr_mult` continues to pass Validate (HC21 flat surface unchanged). |
| **(f) Mode-1 overlay replays through the What-If adapter** | **A36f** — the A30a linked-seam pattern extended: an overlay `{target_arbitration_mode:1, target_buffer_pct:+5.0, stop_buffer_pct:+10.0}` fed through `WhatIfSettings.BuildCellSettings` on a tmp settings.json fragment produces a `cellCfg` with the four POCO fields correctly mutated, and a `WhatIfReplay.RunCell` on a fixture row produces `PlacedTargetLong` / `PlacedStopLong` bit-for-bit equal to a direct `SignalEmitter.ComputeSideLevels(_, r, cellCfg, isLong:=True)` call — proving the overlay reaches the new fields AND the runner arbitration IS production. |
| verify-gate `prepush` | **GATE PASSED**, exit 0. `display-parity`: no snapshot/card drift (nothing rendered at defaults changes). `version-bump`: OK — engine-path change accompanied by the v55 → v56 bump (the change is inside `Core/`, exactly what the check looks for). |
| Display parity | **No rendered surface changes at defaults by construction** — the seam edit lives inside `ComputeSideLevels`, and every mode-1/buffer branch is gated on non-default settings values; both the plaintext snapshot and the card render the same bytes for the same inputs. Stated in the commit message. |

---

## 6. Not verified by the implementer (runtime, trader-observed)

Nothing — the build is a what-if-first instrument with all defaults inert. The trader will:

1. Notice no live behaviour change on the next Analyze Now run (that IS the check).
2. Optionally run a 36-cell grid (mode T × mode S × target_buffer × stop_buffer) via the What-If launcher on the logged book to answer the arbitration-shape question the proposal raises (§2 recommended first study).
3. If a cell wins meaningfully (EV-in-ATR + split-half holdout), the LIVE activation of any non-default mode/buffer is its own subsequent ⚠ D-table; the stop-widest side (`stop_arbitration_mode = 1`) is additionally hard-gated on consumer sizing-by-stop-distance (L3, derivation F1: wide stops at fixed size = bigger losses).

---

## 7. Coordinator review checklist
- [ ] `StructuralLevelsSettings` carries four new POCO properties with correct defaults (0 / 0 / 0.0 / 0.0) and `<JsonPropertyName>` aliases matching settings.json.
- [ ] `settings.json` version bumped 55 → 56; four new keys present under `structural_levels` at their defaults; newest-first change_log entry.
- [ ] `ComputeStructuralSideLevels` mode-0 branches trace the v51 B4b instructions verbatim (candidate qualification, ladder walk, DG1 stop min/floor/clamp semantics); mode-1 branches sit on top without altering mode-0.
- [ ] Buffer application is inside the seam (post-arbitration, pre-return); the 4-tick stop floor snaps a punishingly-negative-buffer stop rather than allowing it to cross entry; the Step-5c min-move gate reads the buffered target (A36d proves it through real Calculate).
- [ ] The four keys land on the WhatIfOverlay whitelist AND on `WhatIfSettings.ApplyKnob`/`ReadKnob` (no whitelist/setter drift — the runner asserts this).
- [ ] `SettingsDiffApplier` exact-match rejects the four keys with `HARD CONSTRAINT 24` in the error text; the flat structural_levels numerics (HC21 surface) still pass.
- [ ] `PromptBuilder` rule 24 present; the FLAT sibling reassertion protects HC21 from misreading.
- [ ] Harness 180 checks ALL PASS; the A26 family unchanged; A36a is the load-bearing default-parity pin.
- [ ] Deviations in §3 above accepted (buffer suffix format; fallback-wins-under-buffer routing).
