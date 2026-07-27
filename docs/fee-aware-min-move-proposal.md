# Fee-Aware Min-Move Floor — Proposal (v61 → v62)

**Date:** 2026-07-27 (Fable coordinator). **Status:** **APPROVED 2026-07-27 — D1–D6 ticked all-as-recommended (trader). BUILD-READY** (Opus, medium effort; kickoff: "Implement docs/fee-aware-min-move-proposal.md"). Order-app relay: `fee-aware-order-app-relay-2026-07-27.md`.
**Class:** settings restructure + one shared resolver; **defaults byte-identical to v61 ⇒ NOT a dataset boundary at ship** (v56 arbitration-modes precedent). Subsequent trader knob-turns are live floor changes the v35 machinery already handles (eval stores the floor-in-effect and re-walks on change).
**Trigger:** Deribit fee change effective 2026-08-01 — maker 1.5 bps / taker 3.5 bps (trader-supplied 2026-07-27). The v35 floor (`scoring.min_tradeable_move_pct = 0.0008`) was sized "to clear slippage" under zero-maker-fee execution; a nonzero maker fee invalidates its derivation basis, so this is a re-derivation, not a re-opened settled decision.

---

## 0. The one principle

The engine never knows trade size — and never needs to. Deribit perp fees are **proportional to notional**, so cost and move live in the same unit (% of price): a maker/maker round trip costs 3.0 bps of notional at any size, and a target at distance d% nets d% − 3.0 bps. The floor decomposes as:

```
EffectiveMinMovePct = round_trip_fee_pct(style) + min_net_move_pct
```

- `round_trip_fee_pct` — derived from the fee schedule + the assumed execution style.
- `min_net_move_pct` — the trader's minimum acceptable **net** move: a risk preference, UI-adjustable (the trader's explicit ask), hot-reloadable, never auto-tuned.

ATR enters nowhere in the floor. (% of ATR is the wrong unit for cost — fees don't scale with volatility. ATR-units appear only in the deferred eval rider, §6, where per-row cost in ATR = fee_pct × price / ATR.)

## 1. Mechanism

**New settings block `scoring.trade_costs`:**

| Key | Default | Meaning |
|---|---|---|
| `maker_fee_bps` | 1.5 | Deribit BTC-perp maker fee, bps of notional |
| `taker_fee_bps` | 3.5 | taker fee, bps (documented single source; loss-side/emergency analytics later — §6) |
| `round_trip_style` | `"maker_maker"` | style assumed by the viability floor: `maker_maker` \| `maker_taker` \| `taker_taker` |
| `min_net_move_pct` | 0.0005 | trader preference: minimum acceptable move AFTER costs (0.05%) |

**Why `maker_maker` is correct, not optimistic (D1):** the floor gates the **target-side** viability — the profit path. In the trader's flow, entry and TP are both maker always; taker occurs only on emergency SL repositioning and rare manual exits — the **loss** path, which the min-move floor does not price. Loss-side cost belongs to sizing/EV analytics (§6) and the order app.

**One shared resolver** (e.g., `ScoringEngine.EffectiveMinMovePct(cfg)` beside the other Helpers): returns the composition above. Every current `cfg.Scoring.MinTradeableMovePct` read routes through it — the known sites: Step 5c (`ScoringEngine_Calculate_Verdict.vb` ~:309), `LivePerformanceTracker` (×4 incl. `_floorPctInEffect`), `FailureRateMatrix`/`AnalysisRunner`/`BandLadder`/`AnalysisConstants` call-site passes, `WhatIfReport` (×2), harness uses. Mechanical sweep; the grep list is the implementer's checklist.

**Old key retires** (D3): `scoring.min_tradeable_move_pct` + POCO property REMOVED — applier-unresolvable (C-6 rejects), deliberately NOT added to any fence-fragment list (the v47-F1 snapshot-poisoning lesson; v53/v61 precedent).

**Byte-identity at defaults (the no-⚠ argument):** maker_maker round trip = 2 × 1.5 bps = 0.0003; + `min_net_move_pct` 0.0005 = **0.0008 = the v61 floor exactly**. Verdicts, eval floors, BELOW_MIN_MOVE rates all unchanged at ship. The fee reality arriving Aug 1 changes the ORDER APP's costs and the trader's preference — expressed thereafter by turning the knob, which the hot-reload + eval floor-change re-walk machinery (v35) already absorbs attributably.

## 2. UI (the trader's #3)

New editable row for `min_net_move_pct` on the existing settings-edit surface (label: **`MIN NET MOVE % (after fees)`**), saving via `SettingsLoader.Save(bumpVersion:=False)` (operational save, v36 §10a) → hot-reload applies next run. Fees + style are settings-file-only (they change when Deribit changes them, not per mood). **Implementer note:** v35's change_log claims UI-editability but no form control exists today (grep-verified 2026-07-27) — this row is NEW, not a re-point. Live status element ⇒ display-parity exempt (stated per standing rule 4).

## 3. Surfaces / parity

- **No snapshot/card/payload/CSV line changes.** `BELOW_MIN_MOVE` renders identically (frozen policy-targetable token — untouched, per the stable-identifiers rule). No CSV columns added.
- Eval cache: `_floorPctInEffect` stores the composed value — same number at defaults ⇒ no re-walk at ship; a knob turn triggers the existing re-walk path (that's the designed behaviour, pinned by fixture).

## 4. Tweaker + what-if

- **Tweaker fence — HARD CONSTRAINT 26** (confirm next-free against `PromptBuilder` at build; ledger: HC24 geometry modes, HC25 alerts): `scoring.trade_costs.` **PREFIX** reject in `SettingsDiffApplier` + PromptBuilder rule text. Fees are facts, the preference is a risk preference (kelly.* class) — nothing under the block is ever auto-tunable. The A15f exemplar (currently the retired key's exact-match reject) re-pins to the new prefix.
- **What-if whitelist migration:** `scoring.min_tradeable_move_pct` leaves the whitelist/launcher grid; **`scoring.trade_costs.min_net_move_pct` joins** (Apply/ReadKnob mirror cases; launcher row relabeled `Min net move % (after fees)`, sweep syntax unchanged). Fee/style keys are NOT sweepable (a fee sweep answers no question the trader can act on; the floor sweep IS the min-net sweep at fixed fees).

## 5. Fixtures / acceptance (A40 family — confirm next-free at build)

- **A40a** — resolver composition: all three styles × bps values; maker_maker default = 0.0008 exactly.
- **A40b** — defaults byte-identity through the REAL `Calculate()` across the A26 case set (v56 A36a pattern): v61 cfg vs v62-defaults cfg ⇒ identical verdicts/levels/contexts, incl. a BELOW_MIN_MOVE case.
- **A40c** — knob change moves the gate: min_net 0.0005→0.0010 flips a marginal directional to `NO TRADE / BELOW_MIN_MOVE` (and the eval floor-change re-walk trigger fires on the composed delta).
- **A40d** — fences: HC26 rejects all four `trade_costs.` keys; retired key unresolvable; sibling `scoring.` tunable still passes; what-if whitelist accepts `min_net_move_pct` + rejects `maker_fee_bps`.
- **A40e** — what-if round-trip: min_net overlay reproduces the gate through `WhatIfSettings.BuildCellSettings` (A36f linked-seam pattern).
- Acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + OrderCheck build **0/0 Release**; A1–A39e unregressed; `verify-gate.ps1 -Mode prepush` GATE PASSED; settings v61→v62 bump + change_log entry; §15 project-doc line. Release-only builds (collector rule).

## 6. Out of scope — named follow-ups

1. **Eval net-EV rider** (analysis-only, no ⚠) — *formula double-checked against `WhatIfReplay.ComputeEvAtr` 2026-07-27:* per-row fee drag in ATR units = `round_trip_fee_pct × entry_price / rep.ATR`, where `round_trip_fee_pct` = **both legs summed** (maker/maker = 3.0 bps at the trader's flow) and `entry_price` = the row's logged `Price` (the exit leg re-prices at the exit, but the error is fee_bps × the move — second-order, ignored). Subtraction is **unconditional across all three outcome arms** (SUCCESS / stop / WINDOW_EXPIRED mark-to-end) — every resolved trade pays the round trip; the maker→taker emergency delta (+2.0 bps) is an optional conservative toggle on the loss arm only, default off (normal SL exits are maker in the trader's flow). The drag model inherits the eval's existing filled-at-entry assumption (mid-touch labels; honest fills arrive with W6-6). Low-ATR rows show proportionally larger drag — that is the point, not an artifact. Slot: post-Aug-1, bundled with the EV ± dispersion report column discussed 2026-07-27 (the Sharpe-question resolution — metric follows cost model).
2. **Display net-R:R rider** (optional, trader-demand): net-of-fees R:R annotation on the Kelly/ATR-levels block for the manual-trading flow. Snapshot+card same-commit parity if pursued.
3. **Order-app relay note** (consumer-side, own doc): (a) EV-aware chase budget — stop repositioning when remaining net move `(placed_target − current_price − round_trip_fee)` < min_net, keeping the existing 0.6-ATR cap as the outer structural bound (all inputs already in the v1 payload); (b) fold the maker→taker 2-bps delta into the SL-emergency threshold. Cross-repo constants note: the order app owns its own fee numbers; the bridge payload does NOT carry fees (no contract change).

## 7. D-table (await trader)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Floor's assumed round-trip style | **`maker_maker`** — the floor prices the profit path, which is maker/maker in the trader's flow (§1) |
| D2 | `min_net_move_pct` default | **0.0005** — ships byte-identical to v61 (floor stays 0.0008); trader adjusts live thereafter |
| D3 | Old key disposition | **Retire** (applier-unresolvable, NOT fence-fragmented — v53/v61 pattern) |
| D4 | UI knob | **New row** for `min_net_move_pct` only, operational save, no version bump per edit |
| D5 | What-if surface | **`min_net_move_pct` sweepable; fees/style not**; old key leaves the whitelist |
| D6 | Tweaker fence | **HC26 `scoring.trade_costs.` prefix reject** |
