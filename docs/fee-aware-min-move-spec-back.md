# Fee-Aware Min-Move Floor — Build Spec-Back

**Date:** 2026-07-27 · **Settings:** v61 → **v62** · **Scope:** settings restructure + one
shared resolver. **Defaults byte-identical to v61 ⇒ NOT a dataset boundary at ship**
(the v56 arbitration-modes precedent). Subsequent knob turns are ordinary live floor
changes the v35 machinery already absorbs (the eval cache stores the floor-in-effect and
re-walks on change).

**Spec of record:** [`docs/fee-aware-min-move-proposal.md`](fee-aware-min-move-proposal.md)
(APPROVED 2026-07-27, D1–D6 ticked all-as-recommended). **Order-app relay:**
[`fee-aware-order-app-relay-2026-07-27.md`](fee-aware-order-app-relay-2026-07-27.md).

**Trigger:** Deribit fee change effective 2026-08-01 — maker 1.5 bps / taker 3.5 bps. The
v35 floor (`scoring.min_tradeable_move_pct = 0.0008`) was sized "to clear slippage" under
zero-maker-fee execution, so a nonzero maker fee invalidates its derivation basis.

---

## 1. What shipped (map to proposal §1–§5)

| Proposal item | Shipped |
|---|---|
| §1 New block `scoring.trade_costs` with the four keys | ✅ `TradeCostSettings` in [Core/Settings/EngineSettings.vb](../Core/Settings/EngineSettings.vb) — `maker_fee_bps 1.5`, `taker_fee_bps 3.5`, `round_trip_style "maker_maker"`, `min_net_move_pct 0.0005`; `ScoringSettings.TradeCosts` sits in the retired key's former slot, so the JSON block lands in the same place. |
| §1 One shared resolver | ✅ `TradeCostSettings.EffectiveMinMovePct` = `RoundTripFeePct + MinNetMovePct`, with `RoundTripFeePct` summing **both legs** per style. Both are `<JsonIgnore>` read-only — derived, never serialised (deviation §2.1 on where it lives). |
| §1 Every `cfg.Scoring.MinTradeableMovePct` read routes through it | ✅ 15 code reads swept: Step 5c ([ScoringEngine_Calculate_Verdict.vb](../Core/ScoringEngine_Calculate_Verdict.vb)), `LivePerformanceTracker` ×4 (incl. `_floorPctInEffect` ×2), `AnalysisRunner` ×4, `BandLadder`, CeilingAudit `CsvFeatureBuilder`, `AutoTweakerCore`, `WhatIfReplay`, `WhatIfReport` ×2. Comment-only references in `AnalysisConstants` / `AnalysisReport` / `FailureRateMatrix` re-pointed too. |
| §1 (D3) Old key retires — JSON key + POCO property REMOVED, NOT fence-fragmented | ✅ Both gone. Also removed: the now-dead exact-match reject in `SettingsDiffApplier` and the `min_tradeable_move_pct` clause of PromptBuilder HC11 (deviation §2.2). |
| §1 Byte-identity at defaults | ✅ 2 × 1.5 bps = 0.0003, + 0.0005 = **0.0008**. Pinned to 1e-12 in A40a and through the real `Calculate()` in A40b. |
| §2 UI row `MIN NET MOVE % (after fees)`, operational save | ✅ New row in the SETTINGS & TOOLS card ([UI/MainForm_Layout.vb](../UI/MainForm_Layout.vb) — `BuildMinNetMoveRow` / `RefreshMinNetMoveRow` / `CommitMinNetMove`). Commits on Enter or blur via `SettingsLoader.Save(bumpVersion:=False)`; Escape reverts; invalid input is flagged in place, not silently reverted. Live status element ⇒ display-parity exempt. Fees + style stay settings-file-only. |
| §3 No snapshot/card/payload/CSV line changes | ✅ None touched. `BELOW_MIN_MOVE` renders identically. |
| §3 Eval cache stores the composed value | ✅ `_floorPctInEffect = cfg.Scoring.TradeCosts.EffectiveMinMovePct` at both assignment sites — same number at defaults ⇒ no re-walk at ship. |
| §4 Tweaker fence HC26, `scoring.trade_costs.` **prefix** | ✅ `RejectedPathPrefixes` entry + PromptBuilder rule 26. Next-free confirmed against PromptBuilder at build (HC24 geometry modes, HC25 alerts → 26). |
| §4 What-if whitelist migration | ✅ Old path out, `scoring.trade_costs.min_net_move_pct` in (`WhatIfOverlay.Whitelist` + `VerdictKnobs`, `WhatIfSettings` Apply/ReadKnob mirrors, launcher row relabelled `Min net move % (after fees)` with a 0.0003:0.0009:0.0002 default sweep). Fee/style keys deliberately absent. |
| §5 A40a–e | ✅ All five, next-free confirmed (A39e was the previous last). Plus A13d re-pointed and A15f's exemplar re-pinned. |
| §5 Acceptance | ✅ solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0** Release; harness **ALL PASS** (A1–A39e unregressed); settings v61 → v62 + newest-first change_log + §15 row + an architecture.md design-decision entry. |

---

## 2. Deviations & decisions

**2.1 The resolver lives on the POCO, not on `ScoringEngine`.** The proposal wrote
"*e.g.*, `ScoringEngine.EffectiveMinMovePct(cfg)` beside the other Helpers". That
placement doesn't work: `Core/ScoringEngine_Helpers.vb` is **not** linked by
`AutoTweaker.vbproj`, `WhatIfRunner.vbproj` or `CeilingAudit.vbproj` — all three consume
the floor and all three would have needed the whole scoring partial (and its transitive
deps) dragged in to reach it. `Core/Settings/EngineSettings.vb` is already linked by every
project, so the resolver ships as a `<JsonIgnore>` read-only property on
`TradeCostSettings`, read as `cfg.Scoring.TradeCosts.EffectiveMinMovePct`. The proposal's
actual requirement — **one** implementation, every consumer through it — is met, and the
accessor is greppable as a single name with no forwarder alias to drift.

**2.2 Two dead fences removed with the key they guarded.** D3 retires the key; the
`SettingsDiffApplier` exact-match reject on `scoring.min_tradeable_move_pct` and the
`min_tradeable_move_pct` clause in PromptBuilder HC11 were both left dangling by that. Both
are deleted — the retired key now fails the C-6 resolve check (which is the *point* of
"applier-unresolvable"), and leaving a reject clause naming a key that no longer exists
would have made A40d's "unresolvable, not fence-banned" assertion untrue. Nothing was
added to `RejectedPathFragments` (the v47-F1 snapshot-poisoning lesson).

**2.3 The UI row carries a derived read-out.** The proposal specified one editable row.
Shipped as `MIN NET MOVE % (after fees)  [0.0005]  → floor 0.0800% (fee 0.0300%, maker_maker)`
— the trailing read-out is the resolver's own output, never a re-derivation, so it cannot
drift from the gate. Rationale: with fees now settings-file-only, the trader edits
`maker_fee_bps` on Aug 1 in a text editor and otherwise has no in-app confirmation of what
floor that produced. The read-out also re-reads after every analysis run, so a hot-reloaded
file-side fee edit surfaces without a restart.

**2.4 A40b reconstructs "the v61 cfg" rather than importing it.** The proposal asked for
"v61 cfg vs v62-defaults cfg". The v61 POCO property is gone, so a literal v61 cfg is not
constructible — and comparing v62 defaults against themselves would be circular. Shipped:
the comparison cfg is built with `maker_fee_bps = taker_fee_bps = 0` and
`min_net_move_pct = 0.0008`, which **is** the retired semantics (a flat 0.0008 floor with
no fee model). Two genuinely different compositions reaching the same floor, then asserted
to produce identical verdict / confidence / context / placed targets / cap reasons across
the A13 gate case set (including a BELOW_MIN_MOVE case). The proposal named the A26 case
set; A13 is the correct one here — A26/A36 exercise `ComputeSideLevels`, which the min-move
floor does not touch, whereas A13 is the gate's own case set and includes the
BELOW_MIN_MOVE case §5 asks for.

**2.5 A40c pins the re-walk *input*, not the re-walk.** §5's A40c asks that "the eval
floor-change re-walk trigger fires on the composed delta". `LivePerformanceTracker`'s
trigger lives inside `InitialiseAsync` behind `_floorPctInEffect` / `ReadSchemaFloorPct`,
both `Private Shared`, and driving it end-to-end needs a live OHLC fetch — the same
live-network boundary that keeps A16–A31 stubbed. What A40c pins is the value the trigger
consumes: a knob turn moves the composed floor by exactly the knob delta and clears the
tracker's 1e-7 epsilon. Stated here so the gap is on the record rather than implied.

**2.6 A40a gained a serialisation pin.** Not in §5, but load-bearing: the UI's operational
save serialises the whole POCO and the tweaker prompt inlines the whole settings file, so a
derived property leaking into JSON would become a phantom tunable. A40a asserts
`trade_costs` and its four real keys serialise while `EffectiveMinMovePct` /
`RoundTripFeePct` do not.

**2.7 Card grew 28 px.** The new row is an absolute `outer` row between the EXIT GUARD
strip and TOOLS, and `SETTINGS_CARD_H_BASE` went 278 → 306 to pay for it. TOOLS is a
Percent row — growing the card is mandatory, or TOOLS starves and silently clips its
fourth LinkRow (the failure mode recorded in the 2026-07-15 space pass).

**2.8 Unrecognised `round_trip_style` falls back to `maker_maker`, not to zero.** The
proposal doesn't specify the invalid-input arm. Falling back to the *cheapest priced*
style keeps a typo from silently producing a fee-free floor; `Nothing` and whitespace/case
variants are handled the same way. Pinned in A40a.

---

## 3. Not done — deliberately

All three are the proposal's own §6 out-of-scope list, unchanged:

1. **Eval net-EV rider** — per-row fee drag in ATR units, unconditional across all three
   outcome arms. Slot: post-Aug-1, bundled with the EV ± dispersion report column.
2. **Display net-R:R rider** — optional, trader-demand; would carry a snapshot+card
   same-commit parity obligation if pursued.
3. **Order-app relay** — EV-aware chase budget + the maker→taker 2 bps SL-emergency delta.
   Consumer-side, own doc. The bridge payload does **not** carry fees; no contract change.

**`docs/UserManual.md` updated; the PDF is NOT regenerated.** Six `.md` sites named the
retired key or its old label: the `BELOW_MIN_MOVE` context row, the off-surface key list
(now an off-surface *prefix* bullet), the tunable-class table, and three What-If launcher
sites (glossary entry, the "how to read it" note, and the 2-D grid example — all relabelled
`Min net move % (after fees)` with the sweep example re-based to `0.0003:0.0009:0.0002`).
The manual-fold-in lane last touched these files 2026-07-21 and the tree was clean, so
there was no in-flight edit to collide with. `UserManual.pdf` is now one revision behind —
republishing is that lane's job, not a same-commit obligation. `TraderGuide.md` carried no
references.

---

## 4. Post-ship

Nothing to watch: defaults are byte-identical, so there is no behavioural delta to observe.
The real event is **2026-08-01**, when the trader sets the live fee numbers and decides
what `min_net_move_pct` should be against them. That knob turn IS a live floor change —
attributable through the eval cache's floor-change re-walk, and worth a note in the §12
watch list at the time.
