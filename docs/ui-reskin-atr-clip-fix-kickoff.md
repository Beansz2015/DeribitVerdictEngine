# UI Reskin — ATR ENTRY LEVELS Row-Clip Fix (Kickoff)

**Date:** 2026-06-10
**Implementer model:** Opus (mechanical layout fix with a prescribed verification loop)
**Track:** UI reskin (track A). This is the one bug blocking the trader's visual sign-off on the P5-test gap-fix work. It does not touch scoring, bindings, or formats.
**Status (2026-06-10, later): ON HOLD — do not route yet.** A systematic Fable review of the 55-case p5-test snapshot set runs first (`docs/p5-test-snapshot-review-brief.md`); its findings get consolidated with this fix into one Opus kickoff so the build → fix → regenerate → re-review cycle runs once. If the review finds nothing new, this kickoff routes as-is.

## The bug

The P5-test gap-fix commits (`7c8de56`→`465b257`) added a **third content line** to cells of the ATR ENTRY LEVELS card — the Q2 `(risk N / rwd N)` line under R:R, and the C1b cap-reason `(label)` appended inline in the CAPPED cell — but the card's pixel budget was never raised. The third line **clips**. Found in the 2026-06-10 crop-and-zoom visual pass; invisible in the full-form fit-scale screenshot (this is exactly the known lesson: fit-scale hides clipping).

The STRUCTURAL card renders its risk/rwd line fine — use it as the sizing reference.

## Where the pixels live (verify against tree before editing)

- `UI/MainForm_Layout.vb` ~:440 — the main grid row for the card: `' Row 4: ATR ENTRY LEVELS. Bumped from 110 to 150 in P4 retro-fix`. The card's total height comes from here.
- `UI/MainForm_Render_Cards.vb` ~:541 `InitAtrLevelsCard` — internal layout: 18 px section header + 14 px sub-header (absolute), then LONG and SHORT zone rows split the remainder 50/50. At 150 px total, each zone row gets roughly (150 − 32 − card padding)/2 — not enough for three lines of text.
- ~:579 `BuildAtrZoneRow` — the 6-column zone row (DirLabel 70 px abs + STOP 24% / R:R 16% / ENTRY 18% / CAPPED 20% / TARGET 22%). **Column widths are locked** — they were rebalanced in gap-fix commit 1; don't touch them.
- ~:650 `MakeZoneLabel` — each zone cell is a single Label rendering header + value as multi-line text; the gap-fix made some cells three-line.

## The fix

Raise the vertical budget until all three lines render fully at current fonts. Expected shape: bump the `MainForm_Layout` row-4 height (150 → whatever three-line zone rows need — measure, don't guess; likely ~180-200) and confirm the internal 50/50 split distributes it. Touch card internals only if the Layout bump alone can't deliver the pixels to the right place.

**Locked — do not do instead:** shrinking fonts, dropping the third line (it *was* the gap-fix), reflowing the zone columns, edits to `MainForm.Designer.vb`.

**Side-effect check:** row 4 feeds the whole-form height/flow — after the bump, verify no card below it newly clips and the form still renders sane at the locked 1100 px width.

## Mandatory verification loop

Build → run → screenshot → **crop-and-zoom** → measure. The fit-scale full-form screenshot does not count as verification for this bug.

1. `dotnet build` clean; run the app.
2. The P5-test harness still exists (its deletion is deferred to the post-sign-off cleanup commit): `Ctrl+Shift+T` / `tools/send-ctrl-shift-t.ps1` drives the 55 synthesised cases and regenerates PNGs. Regenerate at least the cases that exercise: CAPPED with a reason label, structural risk/rwd present, both LONG and SHORT sides populated.
3. `tools/screenshot-mainform-full.ps1` for live shots; crop the ATR card and zoom ≥2× (`tools/README.md` has the loop).
4. Pass criteria: in both LONG and SHORT zone rows, every cell's third line is fully visible — Q2 `(risk N / rwd N)` under R:R, and the CAPPED cell's `→ price (label)` with the parenthetical label unclipped. Compare against the STRUCTURAL card for visual consistency.
5. Whole-form screenshot: no new clipping anywhere else.

## Out of scope

- The design question "CAPPED price duplicates TARGET price when cap ≈ target" — that is a trader decision at sign-off, not this fix.
- Any binding/format/content change to any card.
- The P5-test cleanup commit and P5b deletion sweep — sequenced after the trader signs off the regenerated PNGs (`docs/ui-reskin-handover-2026-05-27.md` §3 has the roadmap).

## Report back

Commit hash + the cropped before/after PNGs (or paths to them) + regenerated harness PNG paths. Local commit only; the user reviews visually, then the sign-off → cleanup → P5b sequence proceeds per the reskin handover.
