# Absorption Geometry Rescale — Build Spec-Back

**Date:** 2026-07-23 · **Settings:** v60 → **v61** · **Scope:** display/CSV-only revision
(`scoring_enabled` stays false; ZERO scoring impact; NOT a dataset boundary — the CSV
Absorption* columns already exist since v54's rotation, values start populating rather
than staying empty, header unchanged, no `.bak`).

**Spec of record:** `docs/absorption-geometry-rescale-proposal.md` (APPROVED 2026-07-23;
V1–V4 all ticked). **Evidence:** `docs/absorption-engagement-derivation-2026-07-23.md`
+ the map ruling: 0% engagement because the tick-scaled geometry (band 4t=$2,
proximity 12t=$6 vs ATR≈$44) measured a shell almost nothing prints in; all three
anchors bound at 100%; the loosest re-anchor caps at 1.6% vs the 3–8% design band.

## 1. What shipped (map to proposal §1)

| Proposal §1 item | Shipped |
|---|---|
| Retire `proximity_ticks` / `band_ticks` / `break_tol_ticks` | ✅ POCO properties + JSON keys removed. Retired keys are applier-unresolvable ⇒ C-6 rejects them; deliberately NOT added to `RejectedPathFragments` (the v47-F1 snapshot-poisoning lesson — the fragment list substring-matches and would swallow the new keys). |
| Add `proximity_atr_frac 0.30` / `band_atr_frac 0.10` / `break_tol_atr_frac 0.05` | ✅ `AbsorptionSettings.ProximityAtrFrac/BandAtrFrac/BreakTolAtrFrac` with those defaults + JSON keys. |
| Resolve per run from `r.ATR` at the SetAbsorptionLevels carry site | ✅ (deviation §2.1 — carry-site signature extended so the tracker consumes absolute dollars; no ATR read inside the tracker). |
| Tracker internals stay absolute dollars | ✅ New tracker state `_proximityUsd/_bandUsd/_breakTolUsd`, set once per run at `SetLevels`; `FoldBook`/`FoldTrade` use these state values instead of `cfg.*Ticks × SignalEmitter.TickSize`. No tick math anywhere in the tracker (the `SignalEmitter.TickSize` reads are gone). |
| `depletion_floor_usd` 25000 → 5000 (provisional) | ✅ POCO default + settings.json. |
| `default.min_aggr_usd` 150000 → 20000 (provisional) | ✅ POCO default + settings.json. |
| `absorb_ratio` 3.0 → 1.5 (provisional) | ✅ POCO default + settings.json. |
| `max_pull_frac` 0.5 → 0.75 (provisional) | ✅ POCO default + settings.json. |
| D8 conservation / visibility mask unchanged | ✅ formulas untouched; they follow whatever band is passed in, so widening flows through by construction. |
| HC23 fences carry to new key names | ✅ `SettingsDiffApplier.vb` (comment) + `PromptBuilder.vb` rule 23 name the new `*_atr_frac` keys as the flat tunables; the exact-match reject on `enabled`/`scoring_enabled` and the prefix rejects on `default.`/`sessions.` are unchanged in shape. |
| Settings v60 → v61 + POCO + newest-first change_log + §15 row | ✅ (change_log entry prepended; §15 v61 row added). |
| A31 re-pin to fraction-resolved dollars + two-ATR scale-invariance fixture (V3) | ✅ A31a–g re-pinned via the extended `SetLevels` signature (fixed 6/2/1 USD to preserve the historic v54 test geometry byte-identical); **new A31h** two-ATR scale-invariance test. |

## 2. Deviations & decisions

**2.1 Carry-site signature extended; tracker becomes ATR-agnostic.** The proposal wrote
"resolved per run from `r.ATR` at the SetAbsorptionLevels carry site … tracker keeps
working in absolute dollars internally — only the config→dollars conversion moves;
implementer's choice how the dollars arrive." Shipped: `MarketState.SetAbsorptionLevels`
and `LevelAbsorptionTracker.SetLevels` both grow three additional `proximityUsd /
bandUsd / breakTolUsd` arguments. The dollars are computed at the WinForms carry site
(`UI/MainForm_Analysis.vb`) as `r.ATR × cfg.*AtrFrac` and passed straight through. The
tracker stores them as instance state alongside the four candidate-level fields and
consults them in `FoldBook`/`FoldTrade`; no cfg-tick reads remain, no `SignalEmitter.TickSize`
reads remain. Rationale: keeps `AbsorptionSettings` a value bag (no ATR knowledge), keeps
the tracker a value bag (no cfg-key coupling to a specific naming), and puts the
resolution decision on the ONLY host that has `r.ATR` in hand — the analysis run. It also
means the fixtures pass a fixed dollar geometry directly (§2.2), the classical harness
pattern.

**2.2 A31a–g re-pinned via explicit dollar arguments; fixtures ATR-agnostic.** The seven
existing A31 fixtures pass `AbsProxUsd=6.0 / AbsBandUsd=2.0 / AbsBreakTolUsd=1.0` — the
exact dollar geometry the pre-v61 tick defaults (12t/4t/2t at TickSize $0.5) produced.
The book geometry (level 100010, ladder from 100008, etc.) is byte-identical, so the
episode lifecycle / analytic-ratio / D8-veto / break-through / reset-and-cold / CSV
reserved-column pins remain the exact same tests they were at v54. Only the
`SetLevels` signature changed and the A31g fence-test's JSON literal + proposable-key
name shifted onto the new `*_atr_frac` names.

**2.3 A31c check-message reworded from `> 0.5` to `> 0.75`.** The D8-veto asserts
`pullFrac 2.0 > max_pull_frac`. The old default 0.5 became 0.75; the boolean assertion
still passes (2.0 > 0.75) but the printed message drifted, and was corrected. Log text
only — no behaviour delta.

**2.4 A31h fixture goes beyond the proposal V3 wording.** V3 asks for the
scale-invariance point. Shipped covers three items in one fixture: (a) the arithmetic
(`ATR=44 ⇒ 13.2/4.4/2.2`; `ATR=88 ⇒ exact 2×`); (b) the tracker opens ACTIVE at both
scales on a scaled book; (c) break-through arithmetic scales identically (a print at
`level + break_tol_usd + ε` breaks at both scales, a print inside the tolerance would
not). The invariant covered — "tracker internals stay absolute dollars; only the
config→dollars conversion moves" — holds across a 2× ATR shift.

**2.5 Warmup posture: `r.ATR = 0` collapses distances to zero, tracker stays IDLE.**
The proposal is silent on the warmup case. Shipped: the carry site sends
`r.ATR × frac` as-is; if `r.ATR = 0`, all three resolved distances are zero, so the
tracker's proximity gate cannot open (`|lvl − bestAsk| ≤ 0` is only true at exact
touch, and the visible-ladder mask still excludes crossing levels). This mirrors the
pre-v61 warmup posture (no episodes fire until the ATR series has enough bars), and
the tracker's own defensive posture (zero geometry ⇒ never active) makes the invariant
robust.

**2.6 Display-string parity — no snapshot/card line.** The absorption surface is
strip-only (the v54 D6 precedent). The `ABS↑ 60510 (3.4×)` tag continues to render the
same shape from the same `AbsorptionSignal/Level/AbsorbRatio` fields; the plaintext
snapshot and card do not emit an Absorption line, so no card binding needs updating in
this commit. Stated here and in the commit message per the display-string parity rule
(CLAUDE.md).

**2.7 Retired tick keys — NOT added to `RejectedPathFragments`.** The v47-F1 lesson:
substring-matched fragments would also block the new `proximity_atr_frac` key
(`proximity_ticks` is not a fragment of `proximity_atr_frac`, but earlier examples of
similar re-namings burned on the fragment list masking new siblings — see the v53
funding `momentum_window` retire, change_log 2026-07-15). Applier-unresolvable path
rejection (C-6) handles the retired keys cleanly.

**2.8 Rescaled floors are PROVISIONAL by proposal §1.** The four rescaled anchors
(`depletion_floor_usd` 5000, `default.min_aggr_usd` 20000, `absorb_ratio` 1.5,
`max_pull_frac` 0.75) are the proposal-recommended values. They are provisional
pending the post-rescale §5 re-derivation (recipe = the 07-23 doc, re-run after ~1–1.5
weekday-weeks of collection under honest geometry). Post-ship watch row appended to
§12 in the change_log entry.

**2.9 POCO defaults ride the commit (v33/v34/v41 precedent).** The three retired
`Integer` properties (`ProximityTicks/BandTicks/BreakTolTicks`) are DELETED from the
POCO along with their `JsonPropertyName` tags. Absent JSON keys resolve to the new
defaults; the retired JSON keys, if present in an older `settings.json`, are ignored
on load (System.Text.Json permissive parse by default). The five value defaults
(`AbsorbRatio 1.5 / DepletionFloorUsd 5000 / MaxPullFrac 0.75 / AbsorptionDefaults.MinAggrUsd 20000`
plus the three new `*AtrFrac` defaults) are the shipped values.

**2.10 SettingsDiffApplier comment updated; PromptBuilder rule 23 updated.** Both
locations named the retired tick keys in their FLAT-tunables narratives; both are
updated to name the new `*_atr_frac` keys. The exact-match reject on `enabled` /
`scoring_enabled` and the prefix rejects on `default.` / `sessions.` are unchanged
in shape — HC23 as a fence is a value-invariant rewrite.

**2.11 Manuals untouched.** The UserManual / TraderGuide absorption references talk
about the display tag and the D8 pull-fraction, not the tick-vs-fraction geometry
naming. Fold into the next manual refresh (flagged for that pass alongside the
pending What-If manual).

## 3. Acceptance record

- **Builds (Release):** solution + AutoTweaker + WhatIfRunner + CeilingAudit +
  OrderCheck — **0 warnings / 0 errors**.
- **Harness:** A1–A31g unregressed on the extended `SetLevels` signature; A31c
  check-message reworded; **new A31h** two-ATR scale-invariance passes; all A32–A39
  populations unregressed.
- **verify-gate:** `-Mode prepush` green at commit (v61 bump present ⇒ version
  check OK; parity token not required — this pass does not touch the snapshot or
  card surfaces).
- **Reversibility:** setting `absorption.enabled:false` ⇒ tracker Fold+Snapshot
  early-out, byte-identical to pre-build. Reverting the four rescaled floor values
  is a settings-only change; reverting the geometry keys is a settings + tracker
  signature change (i.e. this PR).

## 4. Open items (not this build's scope)

- **Post-rescale §5 re-derivation** (proposal §2): re-run
  `docs/absorption-engagement-derivation-2026-07-23.md` on ~1–1.5 weekday-weeks of
  collection under honest geometry. Anchors above are provisional pending that pass.
- **§5 activation gates** (v54 open item, still open): 5.1 independence (|Spearman|
  < 0.7 vs OFIRatio/burstRatio/TFI, fire-overlap < 80%) AND 5.2 outcome gradient
  (≥10 pp worse success on n≥30 flagged evaluated rows).
- **Activation sub-version** (still its own ⚠ boundary): Step-2 penalty wire-in +
  `Absorption` breakdown row (snapshot + card in the same commit) + §12 watch row.
