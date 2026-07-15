# UI Space Pass — Spec-back (2026-07-15)

**Type:** display/layout only — **zero engine, config, scoring or CSV impact**. No rendered line is
added/removed/renamed anywhere, so `BuildPlaintextSnapshot` is untouched and the CLAUDE.md
display-string parity rule has nothing to sync (card-geometry only).
**Driver:** trader request — "tighten the spaces so I can see more without scrolling", then four
rounds of screenshot-driven follow-ups.
**Commits:** `ea10168` (space pass) · `0400de0` (SETTINGS & TOOLS restructure) · `5705093`
(follow-ups) · `0a4e3f6` (real border fix + padding special-case).
**Related:** the ATR-levels work (`c138cb7`) + the TAPE burst highlight are recorded in
`aggr-vel-wirein-spec-back.md` §4b/§4c — they rode the aggressor-velocity wire-in.

**Net result: form height ~2850 → 2518 px (~330 px less scrolling).**

---

## 1. Method — measure, don't eyeball

Every value here came from **launching the app and pixel-scanning a screenshot** (a PowerShell
`GetPixel` sweep reporting contiguous rows of non-background luminance → exact text-line positions
and gaps), not from guessing. The harness does **not** cover WinForms layout, so a clean build
proves nothing about geometry. Two bugs in this pass (a blank card, missing borders) were
build-clean and only visible in a screenshot.

Loop: `dotnet build -c Debug` → launch → `tools/click-mainform-button.ps1 ANALYZE` →
`tools/screenshot-mainform-full.ps1` → scan → adjust. (Debug because the screenshot helper writes
its marker to the Debug bin. **Rebuild Release before the collector resumes.**)

---

## 2. What shipped

| Section | Before | After | Why |
|---|---|---|---|
| SIGNAL BREAKDOWN | 500 | **452** | `outer` row 2 is Percent → it absorbed all slack and pushed the footer down, leaving a **69 px** dead gap above the OI × CVD divider. Now ~21 px. |
| OI × CVD | 150 (of a 320 row) | **132** | Content measures ~100 px + 24 padding. |
| KELLY | own 220 row | **220, beside OI × CVD** | Moved into OI × CVD's dead space; its full-width row is gone. |
| VOLUME PROFILE | 320 | **row-spans the pair** | Bottom now tracks KELLY's *by construction*. |
| INDICATOR DETAILS | 760 | 760 (+ box matching) | Per-row box heights equalised. |
| SETTINGS & TOOLS | 366 | **276** | CTA row removed, LOG row trimmed, top padding special-cased. |

**Key structural moves**

- **KELLY beside OI × CVD.** Row 7's LEFT column stacks OI × CVD over KELLY; VOLUME PROFILE
  row-spans BOTH on the right. The trader asked for "grow VP to match Kelly's bottom" — the
  row-span makes that automatic, so there is **no second number to keep in sync**.
- **INDICATOR DETAILS per-row box matching** (`MatchRowBoxHeights`). The AutoSize RowStyle already
  made each *row* `max(left, right)` tall, but each bordered body still sized to its **own**
  content, so the shorter side's outline stopped short. Equalised via `MinimumSize` (AutoSize
  honours it and can still grow). **Per-row only** — rows stay independent, per the trader's rule.
- **ANALYSIS REPORT into the TOOLS box.** Was a full-width 56 px row; now half width, right side,
  74 px tall (spans the LinkRow block y 26..100). The box had ~700 px of dead space beside its
  LinkRows.
- **Settings cog** moved from the box's right edge to sit right of the "Output Dump" label.
- **SETTINGS & TOOLS top padding special-cased** 12 → 6 (`SETTINGS_CARD_PAD_TOP`). Measured
  card-top→LOG **36 → 24 px**.

---

## 3. Sizing for the MAX state (the recurring trap)

Cards are absolute-height rows, so they must fit the **worst-case** content, not the state that
happens to be on screen. Both misses in this pass were this:

- **KELLY** renders header + 2-line advisory + up to **SIX** KV rows — the
  `Notional ≈ $X · N× lev` row appears **only when `KellyContracts >= 1`**. 196 was measured off a
  bias-only run (5 rows) and clipped the 6th on a real signal. KV rows are exactly **20 px**;
  **220** lands it with ~26 px spare. *This state cannot be forced offline* — bias-only runs never
  render it, so it must be reasoned about, not observed.
- **SIGNAL BREAKDOWN** columns are fixed row-sets — left = CORE(5) + TIER 1(8) + 2 tier labels =
  15 lines; right = TIER 2(8) + TIER 3(4) + 2 tier labels + the **conditional** "Best vol. pivot"
  sub-line = 15. The measured run happened to carry that sub-line, so it was a true max.

Consequence, accepted deliberately: **quiet states show some slack.** That is the cost of never
clipping.

---

## 4. WinForms traps hit here (all cost a screenshot round-trip)

These are the expensive lessons. All are now also in code comments at their sites.

1. **A nested `TableLayoutPanel` inside an ABSOLUTE-width column collapses the whole row.**
   Wrapping the ATR `DirLabel` in a panel inside the 70 px absolute col 0 drove every value column
   to 0 width — the card rendered **blank** except LONG/SHORT. Nested panels are fine in **percent**
   columns (the STOP cell and row 7 both prove it). Col 0 must hold its label directly.
2. **`Anchor = None` does NOT mean "stay put".** An unanchored child is re-positioned
   *proportionally* on parent resize, so the cog drifted to mid-box. `Top|Left` pins it. (Same
   reason the LOG box's labels render centre-ish — pre-existing, untouched.)
3. **A fixed-width `LinkRow` swallows anything placed over it.** `rowDump` was a 320 px hit area
   for a ~124 px label, so a cog next to the *text* landed inside the row and lost the z-order
   fight even after `BringToFront()`. Fixed by narrowing the row to its label, which also stops
   the cog sitting on a link's click target.
4. **A Percent row absorbs every trim — starving it clips silently.** SETTINGS & TOOLS' TOOLS row
   is Percent; trimming the *card height* alone starved it and dropped **Output Dump** with no
   error. Trim the *fixed* sibling rows instead and keep TOOLS' ~120 px slice.
5. **A TLP with `ColumnStyles` but no `RowStyles` ignores its row height.** ← *the missing-border
   bug.* `row1` (LOG | AUTO-RUN) declared only ColumnStyles, so its single row fell back to
   AutoSize and took the `SectionGroup` Panel's ~100 px **preferred** height regardless of the row
   above. At the old 110 the box happened to fit; once the row shrank, the box stayed ~100 while
   its cell got smaller and the border `SectionGroup` paints at `Height − 0.5` was clipped away.
   An explicit Percent RowStyle makes the box exactly its cell's height — and only then does the
   row height actually control the box. **A first fix (110 → 98) treated the symptom and failed.**
6. **Absolutely-positioned children ignore `Padding`.** `AddPlaceholderHeader` places the section
   title at a literal `Location`, so special-casing a card's top padding buys nothing unless the
   header moves too — hence its new optional `topY`.

---

## 5. Tunables introduced

| Const | Value | Meaning |
|---|---|---|
| `ATR_VALUE_BAND` | 75.5 | ATR card: shared value/sub-label baseline (higher % = lower). |
| `DIR_LABEL_BOTTOM_PAD` | 14 | ATR card: **complement of the band** — retune the two together. |
| `OICVD_CARD_H` / `KELLY_CARD_H` | 132 / 220 | Row-7 split. VP row-spans, so it follows. |
| `OUTPUT_DUMP_COG_X` | 140 | Cog X; also sets `rowDump`'s width (`− 14`). |
| `SETTINGS_CARD_PAD_TOP` | 6 | SETTINGS & TOOLS' special-cased top padding **and** its header `topY`. |

---

## 6. SectionGroup unification — DONE (`fec366c`)

There were **two** sub-box mechanisms; there is now one.

- `SectionGroup` (`UI/Controls/SectionGroup.vb`) — owner-drawn **rounded** rect (4 px radius),
  `AccentColor` pen, `Solid`|`Dashed`. Used by SETTINGS & TOOLS (LOG / AUTO-RUN / TOOLS).
- `BuildGroupInline` (`MainForm_Render_Cards.vb`) — *was* a `FlowLayoutPanel` with
  `BorderStyle = FixedSingle` (square 1 px system border). Used by INDICATOR DETAILS.

`BuildGroupInline` existed **only because `SectionGroup` hardcoded its title colour** — those 12
groups tint their titles to the regime tag (REGIME · TRENDING_UP green, MICROCVD · BEAR_DECEL red,
…). Two new `SectionGroup` properties removed the reason:

| Property | Default | Why |
|---|---|---|
| `TitleColor` | `FG_SECONDARY` | The whole reason for the second mechanism. |
| `TitleUpper` | `True` | `OnPaint` force-upper-cased the title — right for LOG / AUTO-RUN / TOOLS (already caps) but it would have silently rewritten mixed-case titles: **"REGIME (5m)" → "REGIME (5M)"**. INDICATOR DETAILS passes `False`. |

`BuildGroupInline` now returns `(host As SectionGroup, body As FlowLayoutPanel)` — the **tuple
shape is unchanged, so all 12 `BuildGroup*` callers were untouched**. Two porting notes:

- The **body docks Top, not Fill.** A `Dock.Fill` child cannot drive a Panel's `AutoSize` (circular
  measure); `Dock.Top` lets the host measure it. The host keeps `Dock.Top` + AutoSize so the box
  still stretches to the column width (C3a) and grows from content (C3b).
- **`MatchRowBoxHeights` now equalises the HOSTS, not an inner body** — SectionGroup paints its
  border across its own whole bounds, so the host *is* the outline. Still `MinimumSize` (AutoSize
  honours it and can still grow), still per-row only.
- Body `Padding` dropped to 0: SectionGroup already brings `Padding(8, 22, 8, 8)`. That 22 px title
  inset is taller than the old ~16 px title label, so the boxes are slightly taller — the
  INDICATOR DETAILS row (760, absolute) still holds all six rows, verified live (TREND STRUCTURE /
  LIQUIDATIONS render fully).

## 7. Final polish (`3de518f`)

- **INDICATOR DETAILS centre gutter.** The pair's hosts now carry `Margin(0,0,4,6)` /
  `(4,0,0,6)` — a 4+4 gutter mirroring LOG/AUTO-RUN. Applied in `LayOutIndicatorRow` (renamed
  from `MatchRowBoxHeights`, which already had both hosts) rather than in the 12 `BuildGroup*`
  callers. The hosts dock Top inside 50% columns, so the margin narrows each box instead of
  overflowing the column.
- **LOG/AUTO-RUN labels left-aligned** — they were `Anchor = None` (trap 2), so WinForms drifted
  them toward centre instead of honouring `Location(10, …)`. Pinned `Top|Left`. This was the
  "pre-existing quirk" noted earlier; the trader called it, and it was the same trap as the cog.
- **LOG text vertical centring.** Once pinned, the ink measured 2285..2334 against a border of
  20.5..89.5 → 3.5 px above vs 16.5 px below (top-heavy). Labels moved 26/46/66 → **31/51/71**
  (≈9 above / ≈11 below). AUTO-RUN's countdown sits at **27** — centred in the gap between the
  border top (20.5) and the REPEAT/SINGLE chip (y=52).

## 8. Open / next

- Nothing outstanding from the trader's list.
- INDICATOR DETAILS' titles now paint at SectionGroup's 11 pt (was 9.5 pt) — deliberate, that IS
  the unified look, but it is the one visual delta beyond the border shape.
