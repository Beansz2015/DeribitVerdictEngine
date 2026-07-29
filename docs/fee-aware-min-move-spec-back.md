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

**Commits (local, unpushed at time of writing — trader tests + pushes):**

| Commit | Contents |
|---|---|
| `ce4ce37` | The build: settings restructure, shared resolver, call-site sweep, HC26, what-if migration, UI row, A40a–e, docs. |
| `c508d93` | UI follow-up after the trader sighted the row — tooltip scope + lifetime fix (§2.9). |

Both gate green independently; the second run also reports the version-bump guard
satisfied (v62 is committed by then, so the guard can see it).

---

## 1. What shipped (map to proposal §1–§5)

| Proposal item | Shipped |
|---|---|
| §1 New block `scoring.trade_costs` with the four keys | ✅ `TradeCostSettings` in [Core/Settings/EngineSettings.vb](../Core/Settings/EngineSettings.vb) — `maker_fee_bps 1.5`, `taker_fee_bps 3.5`, `round_trip_style "maker_maker"`, `min_net_move_pct 0.0005`; `ScoringSettings.TradeCosts` sits in the retired key's former slot, so the JSON block lands in the same place. |
| §1 One shared resolver | ✅ `TradeCostSettings.EffectiveMinMovePct` = `RoundTripFeePct + MinNetMovePct`, with `RoundTripFeePct` summing **both legs** per style. Both are `<JsonIgnore>` read-only — derived, never serialised (deviation §2.1 on where it lives). |
| §1 Every `cfg.Scoring.MinTradeableMovePct` read routes through it | ✅ 15 code reads swept: Step 5c ([ScoringEngine_Calculate_Verdict.vb](../Core/ScoringEngine_Calculate_Verdict.vb)), `LivePerformanceTracker` ×4 (incl. `_floorPctInEffect` ×2), `AnalysisRunner` ×4, `BandLadder`, CeilingAudit `CsvFeatureBuilder`, `AutoTweakerCore`, `WhatIfReplay`, `WhatIfReport` ×2. Comment-only references in `AnalysisConstants` / `AnalysisReport` / `FailureRateMatrix` re-pointed too. |
| §1 (D3) Old key retires — JSON key + POCO property REMOVED, NOT fence-fragmented | ✅ Both gone. Also removed: the now-dead exact-match reject in `SettingsDiffApplier` and the `min_tradeable_move_pct` clause of PromptBuilder HC11 (deviation §2.2). |
| §1 Byte-identity at defaults | ✅ 2 × 1.5 bps = 0.0003, + 0.0005 = **0.0008**. Pinned to 1e-12 in A40a and through the real `Calculate()` in A40b. |
| §2 UI row `MIN NET MOVE % (after fees)`, operational save | ✅ New row in the SETTINGS & TOOLS card ([UI/MainForm_Layout.vb](../UI/MainForm_Layout.vb) — `BuildMinNetMoveRow` / `RefreshMinNetMoveRow` / `CommitMinNetMove`). Commits on Enter or blur via `SettingsLoader.Save(bumpVersion:=False)`; Escape reverts; invalid input is flagged in place, not silently reverted. Live status element ⇒ display-parity exempt. Fees + style stay settings-file-only. **Visually verified by the trader 2026-07-27** (§2.3). |
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

**Visually verified by the trader on 2026-07-27** — the row renders as designed between the
LOG / AUTO-RUN pair and TOOLS, and TOOLS keeps all four LinkRows plus the CTA (the starve-
and-clip failure mode §2.7 guards against did not occur). The build seat did **not** launch
the app: doing so appends collector rows under a fresh `InstanceId` (the v57 stomp lesson),
so layout was verified by arithmetic only and the render left to the trader's test gate.
That gate found one real defect — see §2.9.

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

**2.9 Tooltip scope + lifetime — fixed post-sighting (`c508d93`).** The trader looked at the
live row and asked for a hover explainer. One existed, but it was defective in two ways
worth recording because both are reusable traps:

- **Lifetime.** The `ToolTip` was a local in `BuildMinNetMoveRow`. WinForms controls hold
  **no back-reference** to the `ToolTip` component serving them, so once the method returned
  nothing kept it alive and it was collectible — the hover could stop working at an arbitrary
  later moment, with no error. Every other tooltip in the form is a field (`_perfTip`,
  `_logInfoTooltip`, `_liveStripTooltip`); this one was the outlier. Now `_minNetMoveTip`.
  (`UI/TweakSettingsForm.vb:724` still has a local `minTierTip` with the same latent bug —
  out of scope here, flagged for whoever next touches that dialog.)
- **Scope.** It was attached to the 78 px `TextBox` only, so hovering the label or the
  derived read-out — most of the row's width — did nothing. Now set on the label, the box,
  the read-out and the host panel.

Text expanded from four terse lines to a real explainer: what the value means (a fraction of
**price**, not ATR, with a dollar anchor), the `floor = fee + min-net` composition and what
crossing it does (`NO TRADE` / `BELOW_MIN_MOVE`), why the fee keys are not editable there
(venue facts) plus the D1 maker/maker rationale in one sentence, and the save / Esc-revert /
valid-range mechanics. `AutoPopDelay` set to 30 s — WinForms' 5 s default truncates text this
long mid-read.

No settings, scoring, fixture or contract impact; UI-only, and the row remains
display-parity exempt.

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

**Aug-1 mechanics, so nobody has to re-derive them under time pressure.** Edit
`maker_fee_bps` / `taker_fee_bps` in `settings.json` (hot-reload picks them up; the row's
read-out confirms the new composed floor on the next run). Then decide `min_net_move_pct`
**independently** — that is the point of the split. Two anchors for that decision:

- *Hold the floor where it is* (0.0008): whatever fees do, keep demanding the same gross
  move. Net edge per trade shrinks by the fee increase.
- *Hold the net where it is* (min_net 0.0005): keep demanding the same take-home move, and
  let the floor rise with fees. Fewer trades clear the gate; the ones that do are worth the
  same to you as before.

They coincide today only because the current fee is already priced in. The what-if runner
can quantify the trade-off before the knob is turned — sweep
`scoring.trade_costs.min_net_move_pct` (`0.0003:0.0009:0.0002` is the launcher default) and
read EV-in-ATR with the split-half holdout. Worth doing on real numbers rather than
guessing, given it changes the population the collector accumulates.

---

## 5. §6.1 rider build — eval net-EV (2026-07-29)

The first of the three §6-out-of-scope items ships. Analysis-only, zero scoring impact, no
settings keys, no version bump; the ranking metric the What-If runner already prints is now
**net of round-trip fees** using the same composed fee model settings v62 introduced.

**Rider trigger:** the fee change lands 2026-08-01. Once the trader starts turning
`min_net_move_pct` under nonzero fees, the ranking metric must price the drag or the tuner
sees a rosier profitability picture than the trader actually pockets. §6.1's derivation was
double-checked against `WhatIfReplay.ComputeEvAtr` on 2026-07-27; this build implements it
verbatim modulo the one deliberate deviation flagged below.

### 5.1 What shipped

| Proposal item | Shipped |
|---|---|
| §6.1 Per-row fee drag in ATR units = `round_trip_fee_pct × entry_price / rep.ATR` | ✅ `WhatIfReplay.ComputeEvAtr` computes `feeDragAtr` from `cfg.Scoring.TradeCosts.RoundTripFeePct × rep.Price / rep.ATR`. `cfg` threaded into `ComputeEvAtr` from the existing `RunCell` call site (~line 204) — no new plumbing. |
| §6.1 Subtraction unconditional across all three outcome arms (SUCCESS / stop / WINDOW_EXPIRED) | ✅ Applied in every `Select Case` arm — `SUCCESS: targetDistAtr − feeDragAtr`, `ADVERSE_HIT/AMBIGUOUS: −stopDistAtr − feeDragAtr`, `WINDOW_EXPIRED: (endClose − entry)/ATR − feeDragAtr`. A41b pins the WINDOW_EXPIRED arm specifically because it is the arm most likely to drift under future refactors (no "trade closed" event to hang a drag off). |
| §6.1 Entry-price basis = the row's logged `Price` (exit-leg re-pricing error second-order, ignored) | ✅ `rep.Price` used verbatim; no exit-price interpolation. |
| §6.1 Report says "net of fees" — the E1 rendered-semantics-disclose-orientation lesson | ✅ Section title now "**Grid ranking — per-trade EV/ATR (net of fees)**". Caption spells out that the per-row drag is subtracted unconditionally and cites the §6.1 rider. A stand-alone italicised line under the caption states net-EV rankings are **not comparable** to pre-rider (gross) runs (deviation §5.2 on why one header note, not per-column). |
| §6.1 Dispersion column beside the mean (Sharpe-question resolution — metric follows cost model) | ✅ `WhatIfEvStat.StdPop` (population std of per-trade EvAtr samples) added; ranking table gains a `σ` column between `EV full` and `EV (sel)`. The split-half mechanics are untouched (they compare means; leaving their semantics alone was in-scope explicit). |
| §5 fixture family — next-free letter | ✅ A41a–d (A40 was the v62 build; next-free confirmed against the harness). Fixtures reach `ComputeEvAtr` through the same seam A30/A36f use — `WhatIfReplay.RunCell` drives the replay end-to-end and emits `WhatIfEvSample.EvAtr` — so `ComputeEvAtr` stays `Private Shared`, no visibility changes required. |
| §5 acceptance | ✅ solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0** Release; harness **ALL PASS** (A1–A40e unregressed + A41a–d new); `verify-gate.ps1 -Mode prepush` **GATE PASSED** (display-parity clean, version-bump clean — no engine-path change, per §5.2). |

### 5.2 Deviations & decisions

**5.2.1 Maker→taker emergency loss-arm delta is NOT built.** §6.1 flags a +2 bps delta on
the loss arm only as *"an optional conservative toggle, default off (normal SL exits are
maker in the trader's flow)"*. Building the default-off toggle with no way to turn it on is
zero user value and a settings key we do not need. When the trader signals demand for the
worst-case pricing arm, it slots in as a `taker_stop_delta_bps` field on `TradeCostSettings`
plus a branch in `ComputeEvAtr`'s `ADVERSE_HIT/AMBIGUOUS` arm — analysis-only, still no
engine impact. This is a deliberate deviation, recorded here so nobody assumes the toggle is
buried somewhere.

**5.2.2 Net-of-fees disclosure is one caption + one italicised line, not per-column
suffixing.** The proposal names the section title/label; column-header labels ("EV full",
"EV (sel)", "EV (holdout)") stay compact because all three EV columns share the same
orientation — labeling one and leaving the others could imply mixed semantics. The caption
says it once, prominently, then names σ ("dispersion beside the mean") in the same
paragraph. The `## Population shift` and `## Baseline vs overlay — failure matrix` sections
are untouched — no EV numbers there, no orientation to disclose.

**5.2.3 `EvSel.Mean` remains the ranking key.** Winner selection in `WhatIfProgram` sorts by
`EvSel.Mean`, now net-of-fees on both sides of the sort. *(Precision correction, coordinator
review 2026-07-29 — the original text here claimed the net ranking is identical to gross
"when fees are constant across cells"; that holds ONLY for cells with identical row
populations, where the per-row drags are the same rows' drags and the mean shift is a
constant.)* The precise behaviour: fee **settings** are cell-constant (trade-cost knobs are
not sweepable), but the drag is per-ROW (`× price/ATR`), so a **population-changing** sweep —
`min_net_move_pct` being the headline case — admits different row sets per cell, mean drag
differs per cell, and the net ranking can legitimately diverge from the gross ranking.
**That divergence is the rider's purpose**: the tuner now sees that admitting low-move rows
costs relatively more in fees. Same-population sweeps (e.g. a pure geometry knob over a fixed
row set) shift every cell by the same constant and re-rank nothing. Preserving `EvSel.Mean`
as the sort key keeps the split-half mechanics — the guardrail against phantom winners —
intact in both cases.

**5.2.4 Gate reports "no engine-path change" — the accepted D6-precedent outcome.** The
version-bump check in `verify-gate.ps1` scopes engine paths to `Core/`, `DynamicNorms.vb`,
`analysis/`. This rider only touches `tools/WhatIfRunner/*.vb`, `verify/ordercheck/*.vb`,
`verify/ordercheck/OrderCheck.vbproj`, and `docs/fee-aware-min-move-spec-back.md`, so the
gate's `no engine-path change` line is correct and the task's own "no version bump" rule is
respected in the same breath. No settings key added, no `settings.json` line changed.

**5.2.5 Population std, not sample std.** `WhatIfEvStat.StdPop` divides by N, not N−1. The
σ column is a *description of the observed sample*, not an estimator of an unobserved
population parameter — sample std would carry an inferential connotation that misleads at
the small-N end of the ranking table (n<30 cells the caller is meant to distrust already).
Pinned in A41d against a known two-sample set (±1 ⇒ mean 0, StdPop 1.0 exactly).

### 5.3 Not done — deliberately, still

The other two §6-out-of-scope items remain unchanged:

1. **Display net-R:R rider** — optional, trader-demand; carries a snapshot+card same-commit
   parity obligation if pursued.
2. **Order-app relay** — EV-aware chase budget + the maker→taker 2 bps SL-emergency delta.
   Consumer-side, own doc. The bridge payload does **not** carry fees; no contract change.

### 5.4 Post-ship

Nothing new to watch beyond the §4 event: 2026-08-01, when the trader edits the fee bps and
picks a `min_net_move_pct` under them. The rider makes the What-If runner's EV numbers
finally reflect what the trader will actually earn, so the `0.0003:0.0009:0.0002` sweep the
launcher already suggests becomes a real decision tool instead of an inflated one.
