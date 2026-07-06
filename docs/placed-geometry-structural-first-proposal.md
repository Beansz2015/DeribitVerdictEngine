# Placed Geometry — Structural-First Levels (target + stop) — Proposal

**Status:** APPROVED — trader ticked §9 D1–D8, 2026-07-03 (D3 = **(a) clamp**). The multiplier/bound values remain PROVISIONAL until the §6 derivation runs (next Fable session; data exists) and the trader reviews the derived table; the implementation may be authored against this spec in parallel but ships only with reviewed values, at the §8 vehicle.
**Supersedes/absorbs:** the roadmap W1 "directional reach-target calibration" item (D7 spin-off) — that calibration becomes this spec's fallback-parameter derivation. Trader directive (2026-07-03): **structural first is priority; the design problem is the looseness fallback.**
**Scoring impact:** ⚠ YES — the placed/effective levels change, which shifts BELOW_MIN_MOVE outcomes and therefore the verdict population. Lands at a dataset boundary (§8). Display + bridge payload values change (schema untouched — prices are prices).
**Origin:** the O2 bridge made the emitted levels *executed* values (R2: placed as-is). The current geometry has two defects the bridge exposes: the ATR fallback **target** (2.0×) reaches only ~43% (D7; window-expiry-dominated), and the placed **stop** is pure ATR (1.2×) — the profile explicitly **rejects ATR stops for execution** (§4: "swing structure defines natural invalidation better than ATR multiples"). That rejection was harmless while levels were display-only; autotrading them mechanically reintroduces the rejected pattern. This spec closes both.

---

## 1. Summary

Today: target = `min(2.0×ATR, structural cap)` (structure only ever *tightens*); stop = `1.2×ATR` always (structure never placed). Proposed: **structure is the primary source for BOTH placed levels; ATR becomes the fallback and the looseness guard**, per side:

- **Target ladder:** swing target → nearest HVN → calibrated-ATR fallback. A structural tier wins when it exists and its distance ≤ `target_max_atr_mult × ATR`; too loose → next tier; no tier survives → the fallback multiplier (re-derived, §6 — replaces the inherited 2.0).
- **Stop:** the structural invalidation stop (below prior swing low / above prior swing high) when it exists and its distance ≤ `stop_max_atr_mult × ATR`; else the fallback handling per D3 (recommended: clamp). A tightness floor (`stop_min_*`) guards the degenerate near-zero-distance case.
- The v35 min-move gate keeps evaluating the **placed** target automatically (it already reads the post-arbitration effective target — the arbitration replaces Step 5b's outputs in place).

The trade thesis and the placed exit finally agree: the trader's entry premise is structural, and now the resting TP/SL sit at the structural prices, with ATR doing what the profile says it should — sizing, reference, and *fallback* — not execution geometry.

**Coherence bonus:** the exit-guard's Layer 1.5 structural-break exit already alarms at exactly the level the stop will now rest at — alarm and resting order align instead of the alarm firing while the ATR stop sits somewhere unrelated.

## 2. What does NOT change

- **The frozen bridge contract.** `levels.*.stop/target` stay absolute prices placed as-is; the canonical semantics already read "structural/ATR invalidation." Zero consumer changes, no re-freeze. `cap_reason` is a pinned-informational free string — its vocabulary extension (§5) is contract-safe.
- Swing/VPFR **derivation** (this consumes existing levels), the MTF gate, Step 2 scoring votes, regime logic, `CalcHoldStatus`, Kelly mechanics (display-only; feeding it placed R:R is a later item).
- The structural `structural{}` payload block (still the raw swing bookkeeping, informational).

## 3. Arbitration mechanism (per side, replacing Step 5b's cap arbitration)

```
TARGET (long side shown; short mirrors):
  candidates in priority order: SwingTargetLong → VPFRNearestHvnAbove → (POC if HVN-gated, as today)
  placed = first candidate with 0 < dist(candidate) <= target_max_atr_mult × ATR
  none survives → placed = entry + fallback_target_atr_mult × ATR        [FALLBACK_ATR]
  label = SWING_HIGH_5M / NEAREST_HVN_ABOVE / POC / FALLBACK_ATR

STOP:
  s = SwingStopLong (below prior swing low)
  s exists AND stop_min_floor <= dist(s) <= stop_max_atr_mult × ATR → placed = s   [SWING_STOP]
  dist(s) > stop_max_atr_mult × ATR → D3 handling (recommended CLAMP:
      placed = entry − stop_max_atr_mult × ATR, label STOP_CLAMPED)
  s absent or dist(s) < stop_min_floor → placed = entry − fallback_stop_atr_mult × ATR  [FALLBACK_ATR]

GATE: v35 min-move evaluates the placed target distance (unchanged mechanism, new inputs).
```

Notes: the ladder preserves today's HVN/POC tier machinery — re-purposed from "closest-wins cap" to "priority-with-looseness-bound." When structure is *tighter* than ATR, behavior is unchanged from today (the cap already picked it); the behavioral deltas are (a) structural targets now win when *further* than the ATR level (up to the bound) and (b) stops become structural at all.

## 4. The honest trade-offs (answering "do we sacrifice anything")

1. **Calibration continuity (the big one).** The entire eval stack (failure matrix, perf strip, tweaker, eval cache barriers) measures k×ATR barriers. Once placed geometry goes structural, executed trades diverge from measured barriers. Phased answer (D6): log the placed levels (D5 columns), keep the ATR-barrier eval as the yardstick through the transition, migrate the eval to placed-geometry barriers as its own analysis-layer follow-up reading those columns. Divergence is bounded and documented, not silent.
2. **Clamped stops sit inside structure** (when D3-clamp binds): a noise-stop risk on exactly the wide-structure trades. Mitigation: derive `stop_max_atr_mult` so the clamp is *rarely binding* (§6 sets it from the structural-stop distance distribution, ~p90), and surface `STOP_CLAMPED` on every surface so a clamped trade is never mistaken for a structural one. The alternative (skip-on-too-loose, a new no-trade gate) is offered as D3-b.
3. **Variable R:R becomes executed reality** — structural targets further than the old cap raise per-trade variance. The profile accepts this explicitly ("variable R:R is a feature"); v1 fixed sizing means $ risk varies with stop distance — bounded by the D3 clamp.
4. **Fewer BELOW_MIN_MOVE suppressions on tight structure? The reverse on loose?** The gate now sees structural distances — the verdict population shifts both ways. This is why the change is boundary-disciplined (§8) and why the derivation reports the projected gate-rate shift before shipping (§6).

## 5. Display parity (three surfaces)

ATR ENTRY LEVELS block: the placed stop gains a source label (`SWING_STOP` / `STOP_CLAMPED` / `FALLBACK_ATR`) mirroring the target's existing cap label; the target's label vocabulary gains `FALLBACK_ATR`. Snapshot + card bindings in the same commit (hard rule). Bridge payload: `levels.*` values change origin, `cap_reason` vocabulary extends (contract-safe); A22 fixtures re-pinned. CSV `TargetCapReason` carries the new labels (header-name readers unaffected).

## 6. Derivation recipe (runs on the existing book before shipping; values PROVISIONAL until then)

Per session×resolution on WS-era directional rows (the audit's slicing), walking forward OHLC over the hold windows:

1. **MFE/MAE distributions** in ATR units (max favorable / adverse excursion).
2. **`fallback_target_atr_mult`**: chosen so the fallback target's reach-rate hits the design point (D4 — recommended ~55–60% vs today's 43%; ≈ MFE p40–p45).
3. **`fallback_stop_atr_mult`**: from winners' MAE (≈ p90 of adverse excursion among target-reachers — the stop that survives 9 in 10 winning paths; sanity-check vs the current 1.2).
4. **`target_max_atr_mult`**: ≈ MFE p75–p90 — beyond it, reach within the hold window is improbable, so a structural target further than this is "loose" by evidence, not opinion.
5. **`stop_max_atr_mult`**: ≈ p90 of observed structural-stop distances (clamp rarely binds) ∩ a $-risk ceiling at v1 fixed size.
6. **Structural-target reach-rate** vs ATR-target reach-rate on rows with clean pairs (the direct evidence for D1), and the projected BELOW_MIN_MOVE rate shift.

## 7. Config — `scoring.structural_levels` block (+ existing keys re-purposed)

`scoring.atr_target_multiplier` / `atr_stop_multiplier` **become the fallback multipliers** (same keys, re-derived values — no duplication). New block:

```json
"structural_levels": {
  "enabled": true,                  // master switch — OFF tweaker surface (exact-match + HC line)
  "target_max_atr_mult": 3.5,       // PROVISIONAL — target looseness bound, ON surface
  "stop_max_atr_mult": 2.0,         // PROVISIONAL — stop bound / clamp level, ON surface
  "stop_min_floor_ticks": 4,        // degenerate-tightness floor, ON surface
  "stop_too_loose_mode": "clamp",   // "clamp" | "skip" (D3) — hand-toggle, OFF surface
  "sessions": { "NY": {}, "LONDON": {}, "ASIA": {} }   // nullable overrides — hand-tuned tier (HC11 class)
}
```

`enabled:false` ⇒ byte-identical to current geometry (the rollback; prove in harness).

**§7 values SUPERSEDED 2026-07-06:** the derived + trader-approved values live in `placed-geometry-derivation-2026-07-06.md` §4 (DG1–DG5 ticked — notably DG1 amends the D2 stop shape to `min(structural, stop_max×ATR)` for v1, with `stop_max = fallback stop = 1.6`; un-clamping is a later pass gated on consumer sizing-by-stop-distance, derivation §6b). B4b implements against THAT table.

## 8. Sequencing (binding)

- **D5 is time-sensitive:** reserve 4 CSV columns — `PlacedTargetLong`, `PlacedStopLong`, `PlacedTargetShort`, `PlacedStopShort` — **in #5's v0.7→v0.8 rotation** (the placed levels are not logged today at all; the eval migration and the derivation validation both need them). Decide before the #5 build freezes the header.
- **Vehicle (D7):** bundle at the #5 boundary commit if its build has not yet landed (one boundary event; precedent v31/v35 bundles); else its own boundary immediately after. **Hard rule either way: ships before autotrade steps to live-at-minimum-size** — the geometry defect is tolerable in log-only, not with resting orders. The bridge log-only soak does not wait for this.
- Rule 1 respected: one boundary event; the eval migration (D6 follow-up) is analysis-layer and boundary-free.

## 9. Sign-off decisions — ALL TICKED by the trader 2026-07-03 (D3 = clamp)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Structural-first **target** ladder (swing → HVN → POC → calibrated-ATR fallback) with the looseness bound | **Yes** |
| D2 | Structural-first **stop** (swing invalidation when within bound) — closes the rejected-pattern gap | **Yes** |
| D3 | Too-loose stop handling: **(a) clamp** to `stop_max_atr_mult×ATR` with `STOP_CLAMPED` surfaced, or (b) skip (new no-trade gate) | **(a) clamp** — availability + bounded risk; bound derived to bind rarely; (b) is the conservative alternative |
| D4 | Fallback-target design reach-rate ~55–60% (the reach-target calibration proper) | **Yes** |
| D5 | Reserve the 4 `Placed*` columns at #5's v0.8 rotation — **decide before #5 builds** | **Yes** |
| D6 | Eval alignment phased: ATR-barrier yardstick retained now; eval migrates to placed-geometry barriers as a follow-up analysis pass | **Yes** |
| D7 | Vehicle: bundle at the #5 boundary if possible, else own boundary immediately after; always before autotrade live mode | **Yes** |
| D8 | Per-session overrides in the hand-tuned tier (v40/HC11 pattern) | **Yes** |

## 10. Acceptance

Build: 3 Release builds 0/0; `enabled:false` byte-identical regression; harness — arbitration fixtures per tier (structural within bound / too loose → next tier / fallback; stop swing / clamped / floored / fallback; min-move gate on placed values; mirror short), label parity across snapshot+card+payload (A22 re-pin), tweaker fences. Post-derivation: the §6 table (per-session values + projected reach-rate + gate-rate shift) in the spec-back; trader reviews before the values ship. Post-ship §12 watch: realized reach-rate + STOP_CLAMPED frequency + BELOW_MIN_MOVE rate over the first weekday sessions.

## 11. Out of scope

Kelly on placed R:R (display follow-up); consumer-side sizing from stop distance (v2+ with the feedback file); eval-migration implementation (D6 follow-up pass); any change to swing/VPFR derivation; trailing/dynamic stop management (v2 exit-guard actionability).
