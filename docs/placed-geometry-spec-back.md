# Placed Geometry — Structural-First Levels — Implementer Spec-Back (B4b, v51)

**Date:** 2026-07-06 (B4b implementer seat). **Status:** BUILT — local commit stack, gate green; trader tests + pushes; coordinator review follows.
**Governing docs:** `placed-geometry-structural-first-proposal.md` (APPROVED D1–D8, D3=clamp) + `placed-geometry-derivation-2026-07-06.md` §4 (**DG1–DG5 ticked 2026-07-06** — the shipped values). §7 of the proposal is superseded by the derivation table; this build implements the derivation values.
**Boundary:** its own ⚠ dataset boundary (settings v50→v51), landed WITHOUT waiting for the #5 correlation gate — pre-agreed (proposal D7 + roadmap 3b: the #5 gate measures signal-level quantities, geometry-invariant).
**Sequencing consequence:** autotrade live-at-minimum-size unlocks when this ships (proposal §8 hard rule). The bridge log-only soak was never gated on it.

---

## 1. What shipped (faithful-to-spec summary)

**ONE seam.** `SignalEmitter.ComputeSideLevels` is now the structural-first arbitration itself. Every placed-level consumer reads it:

| Surface | Call site | Notes |
|---|---|---|
| Scoring (Step 5b) | `ScoringEngine_Calculate_Verdict.vb` — delegates when `structural_levels.enabled`, copies outputs onto `Adjusted*Target` / `TargetCapReason*` | **Step 5c (v35 min-move gate) evaluates the PLACED target** — `placedLong/ShortTarget` locals fed straight from the arbitration (legacy path computes the byte-identical pre-B4b expression). Ordering verified: Step 5b → Step 5c inside `Calculate()`; early returns (regime veto / MTF block) never reach either, exactly as before. |
| Bridge payload | `SignalEmitter.BuildOk` → `levels.*` | Values change origin; **schema untouched** (prices are prices; `cap_reason` pinned-informational vocabulary grows). |
| CSV `Placed*` | `AnalysisLogger.LogRun` | Column VALUES change semantics at this boundary, names don't (v31/v36 precedent; noted in the v51 change_log). `TargetCapReason` categorical buckets (`swing`/`hvn`/`poc`/`none`) unchanged — fallback rows project `none`, `NormaliseCapReason` needed zero changes. |
| Snapshot | `BuildPlaintextSnapshot` ATR block | New rows render from the returned `SideLevels`; legacy branch preserved verbatim. |
| Card | `BindCardAtrLevels` / `BindAtrRow` / `BindAtrStopCell` | Refactored to consume `SideLevels`; legacy output byte-identical when labels are `Nothing`. |

**Target ladder (per side, D1):** swing → nearest HVN → POC (HVN-gated exactly as the legacy cap) → ATR fallback. First tier with `0 < dist ≤ target_max_atr_mult×ATR` places — structure now wins even when **farther** than the ATR level (the key behavioural delta; A26a). Labels `SWING_HIGH_5M`/`SWING_LOW_5M`/`NEAREST_HVN_ABOVE`/`NEAREST_HVN_BELOW`/`POC`/`FALLBACK_ATR`; reason string **`PLACED @ p (LABEL)`** (was `CAPPED @` — vocabulary extension, contract-safe). The v30 sub-tick noise suppression is preserved, now measured against the **fallback ATR price** (a structural target within `max(0.5, ATR×0.02)` of the fallback places the value but reports/renders uncapped).

**Stop (D2 amended by DG1):** placed = `min(structural swing stop, stop_max_atr_mult×ATR)` — structure places only when tighter and ≥ `stop_min_floor_ticks × $0.5`. Labels: `SWING_STOP` (structural placed) / `STOP_CLAMPED` (structural exists, bound binds — D3 clamp) / `FALLBACK_ATR` (absent, wrong-side, or sub-floor). With `stop_max = fallback stop = 1.6` the clamp price equals the fallback price; the label still distinguishes the cases honestly.

**Values (derivation §4, DG2–DG4):** `scoring.atr_target_multiplier` 2.0→**1.75**, `scoring.atr_stop_multiplier` 1.2→**1.6** (same keys re-purposed as the fallback multipliers); new `scoring.structural_levels` block — `enabled:true`, `target_max_atr_mult:3.5`, `stop_max_atr_mult:1.6`, `stop_min_floor_ticks:4`, `stop_too_loose_mode:"clamp"`, `sessions:{NY:{}, LONDON:{fallback_target_atr_mult:2.0}, ASIA:{fallback_target_atr_mult:1.25}}`.

**Session plumbing:** new `IndicatorResults.SessionUtcHour` (default **-1** = unstamped ⇒ global multiplier), stamped once per run in `RunAnalysisAsync` beside the `ExecResolution` stamp — all surfaces resolve the identical session by construction (no per-surface `UtcNow.Hour` reads that could straddle a boundary mid-run). Resolver: `ExecutionResolution.ResolveFallbackTargetMultiplier(cfg, utcHour)` (v40 nullable-override pattern; returns the plain global multiplier when `structural_levels` is disabled, so renderers call it unguarded).

**Rollback:** `structural_levels.enabled:false` ⇒ byte-identical v50 geometry — the legacy code paths are preserved verbatim in both `ComputeSideLevels` and Step 5b, and pinned by A26f + the re-anchored A12.

**Rode along:** the outstanding v37 POCO hygiene one-liner — `AtrSettings.StaticRef` 115.0 → 38.0 (the settings-only v37 flip's "next code commit" promise).

## 2. Display parity (four surfaces, one commit)

- **Snapshot** (`UI/MainForm_PlaintextSnapshot.vb`): new `AppendPlacedAtrRow` — placed stop always labelled (`Stop 61936.0 [STOP_CLAMPED]`); structural-placed targets keep the legacy arrow form (`Target 62070.0 --> 62100.0  [PLACED @ 62100.0 (SWING_HIGH_5M)]`); fallback/noise rows carry `[FALLBACK_ATR]`-style target labels plus the **true placed R:R** (computed from placed stop/target distances — the multiplier-ratio string would lie when the stop is structural). Header line shows the **session-resolved** target multiplier.
- **Card** (`UI/MainForm_Render_Cards.vb`): `BindAtrRow` consumes the shared `SideLevels`; STOP cell gains a `(label)` line; TARGET cell gains `(label)` on non-capped rows (capped rows keep the label in the CAPPED cell — no duplication); sub-header mirrors the snapshot header. The C1c STRUCT|STOP dual cell still shows the raw structural stop beside the placed one when deeper — under DG1-clamp that is exactly the `STOP_CLAMPED` case, so the true invalidation level is visible next to the bounded resting stop.
- **Payload:** `cap_reason` vocabulary grows (`PLACED @ …`); `target_capped`/noise semantics unchanged in shape. A22a re-pinned.
- **CSV:** `Placed*` values now structural-first; `TargetCapReason` buckets unchanged.

**Format nit (both render surfaces, same commit):** the header target multiplier renders **F2** (1.75 must not round to "1.8"); stop stays F1. Applies on the legacy path too (2.0 → "2.00") — the one deliberate cosmetic change to the disabled-path output; everything else is byte-identical. Flagged for coordinator: if strict byte-identity of the disabled path's header is preferred, the F2 could be made conditional — chosen not to, to keep one format string.

## 3. Deviations / judgment calls (coordinator attention)

1. **`stop_too_loose_mode` is a decision record, not a code branch.** D3 ticked (a) clamp; the D3-b "skip" alternative (a new no-trade gate) is NOT built. The key ships (`"clamp"`) so the decision is visible/toggleable-in-principle, but the arbitration implements clamp only — an unrecognised value (including `"skip"`) behaves as clamp. Documented in the POCO comment + the v51 change_log. If the trader ever wants skip, it's a small Step-5b-side spec (the arbitration is verdict-agnostic and cannot express "no trade").
2. **Eval divergence detail (D6 — deliberate, bounded).** `LivePerformanceTracker.BuildEntry` reads `v.AdjustedLongTarget/ShortTarget` for its favourable barrier: post-B4b those fields carry the structural-**placed** target when a structural tier wins (can now be *farther* than the old cap, up to 3.5×ATR), and 0 on fallback → the eval falls to its internal `FAV_ATR_MULT = 2.0` const, which no longer equals the 1.75 fallback the engine actually places. The tracker, `AnalysisConstants` (1.2/2.0 mirrors), and `FailureRateMatrix` are **deliberately untouched** per D6 ("ATR-barrier yardstick retained; eval migration onto the logged `Placed*` columns is the follow-up analysis pass" — F4 already established the eval barriers weren't pure k×ATR). Caveat inherited from re-purposing the keys: `analysis/AnalysisRunner` reads `cfg.Scoring.AtrTargetMultiplier` LIVE for its gate-mirror/exclusions, so offline reports regenerated post-v51 will mirror the 1.75 gate — consistent with the live gate, divergent from pre-v51 report vintages.
3. **Kelly advisory:** `CalcKellySizing` untouched — `b = AtrTargetMultiplier/AtrStopMultiplier` = 1.75/1.6 ≈ **1.09** (was 1.67), stop-distance input stays the global fallback `ATR×1.6`. It does NOT consume the placed levels or session overrides — Kelly-on-placed-R:R is explicitly out of scope (proposal §11). Expect visibly smaller Kelly fractions; display-only.
4. **`Adjusted*` population convention:** on the structural-first path, `AdjustedLongTarget/ShortTarget` + `TargetCapReason*` are set only when a structural tier placed AND survived noise suppression (mirrors the payload/display "capped" view); fallback rows leave them 0/"" so every downstream reader (CSV bucket projection, eval fallback, harness) keeps its existing semantics. The min-move gate does NOT read them — it gates on the arbitration's placed value directly (so a noise-suppressed structural placement still gates on its exact placed price).
5. **UserManual/TraderGuide not updated** (§199–200, §265, §314 reference 1.2/2.0 and the CAPPED-only vocabulary). Deliberate scope hold — flagged as a small doc follow-up (coordinator may fold it into review, the v50-cycle precedent).
6. **Re-pinned fixtures:** A12 re-anchored as the legacy pin (explicit `enabled:false` + 2.0/1.2 — doubling as rollback proof; its old "swing beyond raw target → uncapped" assertion is exactly what B4b inverts, now pinned by A26a). A13 refloored to the 1.75 fallback arithmetic (A13d floor 0.0004→0.0003). A14c 3-min bar range 27→32 (27×1.75 = 47.25 fell under the 49.6 gate floor; the point under test — 3-min ATR clears what 1-min can't — is unchanged). A22a/A24a re-pinned r-driven (the old manual `v.Adjusted*` injection is ignored by the structural-first path).

## 4. Acceptance evidence

- **Builds:** solution + AutoTweaker + OrderCheck, all Release, **0 warnings / 0 errors** (Release-only throughout — the live collector runs from bin\Debug and was never rebuilt).
- **Harness:** **119 fixtures ALL PASS** (was 103). A1–A25b unregressed (with the §3.6 re-pins); new **A26a–g**: farther-structural target places + short mirror (a), too-loose walks swing→HVN→POC→fallback (b), stop SWING/CLAMPED/no-struct/sub-floor/short-mirror (c), min-move gate reads PLACED values both directions through the real `Calculate()` — structural placement RESCUES a verdict the fallback would gate, and the same `r` with `enabled:false` gates (d), session multiplier resolution LONDON/ASIA/NY/unstamped/disabled + ASIA end-to-end (e), `enabled:false` byte-identical legacy trio (f), HC21 three-tier tweaker surface (g).
- **verify-gate:** `tools/checks/verify-gate.ps1 -Mode prepush` green before (baseline, 103 PASS) and after (see commit).
- **Tweaker fences:** `SettingsDiffApplier` — `scoring.structural_levels.sessions.` prefix + exact-match rejects for `enabled`/`stop_too_loose_mode`; PromptBuilder **HARD CONSTRAINT 21**; flat numerics (`target_max_atr_mult`, `stop_max_atr_mult`, `stop_min_floor_ticks`) + the re-purposed `atr_*_multiplier` keys remain ON the surface (A26g).

## 5. Post-ship §12 watch (first weekday sessions)

Per proposal §10 + derivation F3, on post-v51 rows:
1. **Realized structural-target reach-rate** vs the fallback's (the D1 validation on live placed geometry).
2. **STOP_CLAMPED frequency** — expected to bind on MOST structural-stop rows at v1 fixed sizing (F1: ≤1.6×ATR structural stops are only ~1–14% of rows by session); the un-clamp pass waits on consumer sizing-by-stop-distance (derivation §6b).
3. **BELOW_MIN_MOVE rate** — projected +4–6pp on NY, ~0 elsewhere (derivation §2).
4. **LONDON structural-target watch (F3, DG4):** LONDON in-bound structural reach was INVERTED (33% vs fallback 61%, n=227). Trigger: still <45% after ≥3 more London session-days → London session override or bound tightening.

Recipes: CSV v0.8 `PlacedTargetLong/Short` vs `SwingTarget*`/`VPFRNearestHvn*` distinguishes structural vs fallback placements; `TargetCapReason` bucket `swing|hvn|poc` = structural placement, `none` = fallback; `PlacedStop*` at exactly `±1.6×ATR` with `SwingStop*` present = clamped.
