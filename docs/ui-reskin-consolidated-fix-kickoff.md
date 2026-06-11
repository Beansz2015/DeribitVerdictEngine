# UI Reskin — Consolidated Fix Kickoff (post-snapshot-review)

**Date:** 2026-06-12
**Implementer model:** Opus (mechanical; all decisions made below). Verify every anchor against the tree before editing.
**Supersedes:** `docs/ui-reskin-atr-clip-fix-kickoff.md` (KNOWN-0 is item 1a here).
**Source:** `docs/p5-test-snapshot-review-report.md` (triaged by the spec-author seat 2026-06-12; §5 consolidation adopted). Read the report's §1 findings + evidence crops (`verify\p5-review-crops\`) before coding.
**Sequencing:** this entire kickoff lands **before the P5b deletion sweep** — F-09's full hold-reason text currently exists only in the legacy block P5b deletes. After this ships: trader visual sign-off → P5-test cleanup commit → P5b → Spec C, per the reskin roadmap.

## Triage rulings (spec-author defaults — trader may override at routing time)

| Finding | Ruling |
|---|---|
| F-02 NOTE capitalisation (G7 unapplied in breakdown) | **ACCEPT AS-IS** — lock "breakdown NOTE column stays lowercase/brevity-style" into the reskin handover §4. Zero trading value; don't spend turns on 17 string sites. |
| F-06 `Near HVN resist` red instead of spec'd amber | **BLESS the deviation** — direction-consistent colour beats the spec; amber stays reserved for warnings/CAPPED. Lock in handover §4. |
| F-07 R:R policy on capped rows | **Recompute R:R + risk/rwd against the CAPPED target** (the actionable number), not suppress-like-legacy. Restore the dimmed raw→capped display (item 2a). |
| OBS-E context-tag colours (FADING amber / STRUCT_WEAK red) | **BLESS** — intended severity mapping. Lock. |
| OBS-A wall-clock, OBS-C/D pill patterns, OBS-H spread −1 | **ACCEPT/no action.** OBS-H waits for Spec C (same SC-attribution family — don't patch twice). |

## 1. Pixel-budget family (clipping/truncation — one sizing pass)

- **1a (KNOWN-0):** ATR ENTRY LEVELS zone rows — raise the row-4 budget (`MainForm_Layout.vb` ~:440, 150px) + card-internal split so all three content lines render in BOTH font sizes (active-side large font is the worst case) and in the NO TRADE both-large state. The CAPPED cell's reason label must render fully (`(NEAREST_HVN_ABOVE)` is the longest). All detail in the superseded kickoff + report §1.
- **1b (F-01):** STRUCTURAL L/S cards, R:R cell — `(risk N / rwd N)` right-truncates, losing the rwd value on ~46 cases. Widen/reflow that cell (column widths there are NOT the gap-fix-locked ATR columns — verify) or shorten the format (`r 300.0 / w 300.0` acceptable if width is tight; keep both values visible, that's the requirement).
- **1c (F-08):** REGIME ANCHOR banner (cases 41/42) — right-truncated AND overlays the chip row/HOLD slot. Re-flow: full text wraps or shortens (`fighting intermediate bear` may compress to `vs intermediate bear`), and the banner must not occlude chips. Both STRONG directions symmetric.
- **1d (F-09):** HOLD chip — truncated exit reasons; Layer 1.5 drops the break prices entirely (worst). Fix: let the HOLD text wrap to a second line or relocate the full reason to a wrapping row in the verdict card. The full string must be readable in the card UI — after P5b it exists nowhere else.

## 2. Binder fixes (`MainForm_Render_Cards.vb`)

- **2a (F-07):** capped ATR rows — (i) restore the raw target, dimmed, with the `→ capped` arrow (legacy parity: `50160.0 → 50080.0`); (ii) per the ruling above, recompute the R:R cell (ratio + risk/rwd subline) against the **capped** target on capped rows. Note the v30 sub-tick suppression arm still applies.
- **2b (F-10):** VWAP box + breakdown VWAP row — surface the `[WARMUP]` tag when the engine reports warmup (legacy: `VWAP (reset 13:30 UTC) [WARMUP]:`). Warmup gating must be visible in the card UI.
- **2c (F-11):** Liquidations STATE pill — maps cascade signals to NONE. Fix the mapping for `LONG_CASCADE` **and the SHORT_CASCADE mirror** (unexercised by the set — write it anyway, verify by fixture or live).

## 3. One-liners

- **F-03:** shorten the best-pivot sub-note so it fits: `HIGH @ 50050.0 (vol ×1.8 vs avg)` — drop " pivot" from the tail rather than resizing.
- **F-05:** OI pill — `NW SHRTS` truncates while `NW LNGS` fits. Shorten symmetrically (`N.LNG`/`N.SHRT`) or widen the pill; short side must render fully (G8).
- **F-04:** RSI note comparator at exactly 50.0 renders `50.0 > 50.0`. Side-consistent fix: render `=` on equality (or `≥/≤`).
- **OBS-F:** case-17 arm — structural missing-leg note `— no swing` should name the side like the 16/18 arms do.

## 4. Verification (mandatory)

Build → run → regenerate the harness PNGs (`Ctrl+Shift+T`, still alive pre-cleanup) → **crop-and-zoom every fixed region** (fit-scale doesn't count) on at least: 01, 04, 08, 11–14 (capped arms), 16–18 (missing-leg), 41–46 (banner+hold), 51 (cascade), 48 (warmup). Whole-form shot for collateral clipping. Also one **live** run: confirm the MTF chip no longer renders a `[—]` placeholder against post-v31 composed reasons (report OBS-B), and that `MTF state:` reasons display sanely on NO TRADE.
Handover §4: add the three locked rulings (F-02, F-06, OBS-E). Local commits only; trader signs off on the regenerated PNGs, then cleanup → P5b proceed.
