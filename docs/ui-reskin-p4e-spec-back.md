# UI Reskin P4e — Spec-Back Report

**Author:** Claude (Opus 4.7, implementation conversation)
**Date:** 2026-05-24
**Parent specs:** `docs/ui-reskin-proposal.md` §4.9 + `docs/ui-reskin-p4-gap-checklist.md` (GAP-72/73/74)
**Kickoff doc:** `docs/ui-reskin-p4e-kickoff.md`
**Commits:** `f7ec66e` → `cf07d03` (4 commits, all local-only, none pushed)

Findings, decisions, and deviations to pass back to the spec conversation.

---

## 1. Executive summary

P4e shipped all three planned commits + one post-verification polish commit. Build clean throughout. The user ran the app four times against live data and confirmed every verification gate in kickoff §4 passed: TOOLS rows render in full, cog click opens the OutputDumpSettings dialog, Output Dump LinkRow opens the dump file, REPEAT⇄SINGLE chip flips on radio toggle, full CSV path appears in the `lblLogInfo` tooltip.

**Deviations from spec, by category:**
- **1 pixel-budget mismatch** — kickoff §3.1 estimated ~280 px for the card row; reality was 340 px. +60 px from the kickoff estimate, +40 px from the implementation's initial 300 px guess. Fixed in the post-verification polish commit.
- **1 deliberate decision** taken on the cog placement (kickoff §6 offered two options; the chosen one reuses the existing `lnkOutputDumpSettings` LinkLabel rather than adding a clickable label inside `LinkRow`, which would have required modifying the locked P3 control).
- **1 locked-P3 control defect surfaced** — `AnalysisReportButton`'s 📊 icon (U+1F4CA) renders as tofu `[]` because the configured Geist Mono font has no glyph for that codepoint.

**Suggestion priority for the spec conversation:** §8.1 (font fallback) is the only item worth pulling into a near-term P3 maintenance pass; everything else is documentation-only.

---

## 2. Spec ↔ reality mismatches

### 2.1 Card row 11 height — kickoff estimated 280 px, reality is 340 px

**Kickoff §3.1 said:**

> | **Card total** | ~260 px + 16 px outer padding ≈ **280 px** |
>
> The row currently allocated to `_cardSettingsTools` in `_gridRoot` is sized for the placeholder. Bump to ~280 px in commit 1. If clipped, raise to 320 — spec-back §8 lesson 3 calls this out.

**Reality:** 340 px is the minimum that doesn't clip the third TOOLS LinkRow.

The +60 px breaks down as:

1. **Placeholder header consumes ~30 px**, not the implied ~0. The kickoff's pixel budget didn't allocate space for the `AddPlaceholderHeader("SETTINGS & TOOLS")` text that the design wireframe omits but every other card in the layout includes (`KELLY SIZING`, `OI × CVD CROSS`, `VOLUME PROFILE`, etc.). The implementation kept the header for consistency with the rest of the card grid. Cost: ~30 px the kickoff didn't account for.
2. **`SectionGroup`'s internal title bar consumes 22 px before content begins.** Three `LinkRow`s at Y = 26, 52, 78 plus a 22 px row height + a few pixels for the cog clearance need ~110 px of *content* area, which is ~130 px of *SectionGroup* area once the title bar is included. The kickoff's ~110 px figure was for SectionGroup *content* but it was applied against the card's available room as if it were the SectionGroup's total height.
3. **Outer TableLayoutPanel needs its own top padding** to sit below the placeholder header (set to 30 px in the implementation). Subtracts from the available row area for the three child rows.

**Recommendation:** if the spec doc gets corrected, the canonical height is **340 px** with the current SectionGroup title-bar + placeholder-header conventions. If the spec author wants the wireframe-exact 280 px, removing the outer `AddPlaceholderHeader` saves ~28 px and adjusting the SectionGroup title padding could save another ~20 px — both touch the visual hierarchy of the card grid, so neither should happen unilaterally.

---

## 3. Decisions surfaced and resolved

### 3.1 Commit 1 — cog placement, option A vs option B in kickoff §6

**Kickoff §6 offered two paths:**

> 1. Render the cog as the `TrailingIcon` of the Output Dump `LinkRow` (one row, not two). Wire `_outputDumpRow.LinkClicked → lnkOutputDump_LinkClicked` and add a separate `Click` handler on a small label inside the row for the ⚙.
> 2. Or: keep two `LinkRow`s — one with ⚙ trailing, one without. Either acceptable; first is closer to design.

**Decision: neither path verbatim — option C.** `LinkRow`'s `TrailingIcon` property exposes a label-text setter but no click event on the trailing label (the trailing label is private). Adding a click handler "on a small label inside the row" would require modifying `LinkRow.vb`, which is part of the locked P3 library.

Instead, the implementation:
- Did NOT set `TrailingIcon` on the Output Dump `LinkRow`.
- Reparented the existing `lnkOutputDumpSettings` LinkLabel (the ⚙ glyph control declared programmatically in `MainForm_Layout.vb`) into the TOOLS `SectionGroup` directly.
- Anchored the cog top-right via a `SectionGroup.SizeChanged` handler so it tracks the group's right edge on resize.

This preserves the existing `Handles lnkOutputDumpSettings.LinkClicked` partial-class wiring (no shim needed) and keeps the P3 control surface untouched.

Visually identical to design option A. Mechanically simpler than option B (no second LinkRow row).

---

## 4. Gaps deferred / not shipped

None for P4e itself. P4e's three commits closed the GAP-72/73/74 items it was scoped against, and the proposal §4.9 layout is complete.

The wider gap inventory that P4e doesn't touch (P4f, P5 cleanup) remains as scoped in the kickoff §5 skip list.

---

## 5. Bugs surfaced

### 5.1 `AnalysisReportButton` 📊 icon renders as tofu (P3 locked control)

**Severity:** cosmetic, no functional impact. Flagged here because the kickoff §7 explicitly asks for "any P3 control behaved differently from its constructor / property surface as I described it."

**Symptom:** the amber ANALYSIS REPORT CTA in the new SETTINGS & TOOLS layout shows `[] ANALYSIS REPORT →` instead of `📊 ANALYSIS REPORT →`. The square is the well-known font-fallback "tofu" glyph that Windows uses when no installed font has a glyph for a requested codepoint.

**Root cause:** `UI/Controls/AnalysisReportButton.vb` line 24 sets `Me.IconText = Char.ConvertFromUtf32(&H1F4CA)` (📊, BAR CHART, U+1F4CA — non-BMP / supplementary plane). The font configured on the control is Geist Mono (set via `Me.Font = Theme.FontMono(12.0F, FontStyle.Bold)` at line 25), which is the bundled `.ttf` shipped under `fonts/Geist Mono-*.ttf` and contains a strictly monospace ASCII + extended-Latin glyph set. It has no glyph for U+1F4CA.

Windows' GDI+ font fallback chain *would* normally substitute from Segoe UI Emoji, but `Label.DrawString` (which `FlatButton.OnPaint` ultimately calls for the icon) does not invoke the same DirectWrite font-link chain as native browsers — it falls back to the default replacement glyph instead. Result: tofu.

**Why P4e didn't fix it:** `UI/Controls/AnalysisReportButton.vb` is part of the locked P3 library, and kickoff §0 explicitly excludes any change to `UI/Controls/`. The defect exists upstream of P4e and would have shown the same tofu in any P4d card binding that used `AnalysisReportButton` — P4e is just the first phase that surfaces the control at a large enough size to make the failure obvious.

**Suggested fix (P3 maintenance pass, not P4e):**
- *Option 1 — replace the codepoint:* swap `&H1F4CA` for a BMP-range glyph that Geist Mono *does* carry. Candidates: `▤` (U+25A4 SQUARE WITH HORIZONTAL FILL — visually similar to a bar chart), `▦` (U+25A6), or geometric block characters. Quick fix; no font-loading code needed.
- *Option 2 — composite font:* set `Me.Font` for the icon paint specifically to a font with emoji coverage (`New Font("Segoe UI Emoji", 12.0F)`), leaving the button text in Geist Mono. Requires splitting the icon paint from the text paint inside `FlatButton.OnPaint`, which is more invasive.
- *Option 3 — drop the icon entirely:* the `→` trailing arrow already signals "this is an action button"; the 📊 is decorative. Cheapest fix.

The trader-profile preferences don't speak to UI iconography directly, so any of the three is acceptable from a behavioural standpoint. Option 1 (BMP-range glyph swap) is the smallest surface change.

---

## 6. Post-verification polish fixes

### 6.1 Card row 11 height bumped 300 → 340 px

Commit `cf07d03`. After the user's first live-run screenshot showed only two of the three TOOLS LinkRows rendering (Output Dump + cog clipped below the card's visible area), the kickoff §4's "+40 px bump" guidance was applied. The 300 → 340 raise restored the third row + cog. See §2.1 for why the kickoff's 280 px estimate was short.

No other polish fixes required. The remaining items the user confirmed manually:
- Cog click → OutputDumpSettings dialog opens.
- Output Dump LinkRow click → dump file opens in the default viewer.
- SINGLE↔REPEAT chip flips on header-radio toggle.
- `lblLogInfo` tooltip shows the full CSV path.

---

## 7. Commit ledger

| # | SHA | Subject | Files | LoC |
|---|---|---|---|---|
| 0 | `f7ec66e` | `fix(ui-reskin): P4e commit 0 — GAP-72/73 OFI vol + GAP-74 spread status` | `UI/MainForm_Render_Cards.vb` | +7 / -3 |
| 1 | `045cb18` | `feat(ui-reskin): P4e — SETTINGS & TOOLS grouped layout` | `UI/MainForm_Layout.vb` | +162 / -28 |
| 2 | `34564ee` | `feat(ui-reskin): P4e — skipped-count surfacing + REPEAT/SINGLE chip` | `UI/MainForm_Layout.vb`, `UI/MainForm_Render_Header.vb` | +55 / -2 |
| 3 | `cf07d03` | `fix(ui-reskin): P4e — bump SETTINGS & TOOLS card row 300 → 340 px` | `UI/MainForm_Layout.vb` | +5 / -3 |

All commits local-only. None pushed. None touched `MainForm.Designer.vb`, `settings.json`, scoring engine, or P3 controls.

---

## 8. Suggestions for the spec author

### 8.1 (HIGH) Open a P3 maintenance ticket for the `AnalysisReportButton` tofu

The 📊 icon failure (§5.1 above) is upstream of every consumer of `AnalysisReportButton` and will surface again in any future card that hosts the control at meaningful size. The fix is one-line (BMP glyph swap) or more involved (composite font) depending on whether the design wants to keep the emoji silhouette. Worth scheduling before P5 cleanup so the locked P3 library is genuinely correct when txtOutput is deleted.

### 8.2 (MEDIUM) Correct kickoff §3.1's pixel budget

If the kickoff template is reused for future P-phases, the budget should account for:
- `AddPlaceholderHeader` consuming ~28 px when the card uses the convention (the P4d card grid uses it on KELLY, OI × CVD CROSS, VOLUME PROFILE).
- `SectionGroup`'s 22 px title bar coming *out of* the SectionGroup's height, not adding to it.

The pattern that matched P4e reality: card row height = `placeholder_header (28) + sum(sub_box_heights) + outer_padding (~16)`. Each `sub_box_height = section_group_content + 30` (22 title + 8 bottom padding).

### 8.3 (LOW) Document the option-C cog-placement pattern

If future kickoffs need a clickable trailing icon on a `LinkRow`, the cleanest path (without modifying P3) is to reparent an existing `LinkLabel` into the host `SectionGroup` and anchor it via a `SizeChanged` handler. This pattern worked clean here and avoids the temptation to add a click event to `LinkRow.TrailingIcon` (which would change the P3 control's API surface).

### 8.4 (LOW) The kickoff's `AnalysisReportButton` click-shim shape was exactly right

Kickoff §3.2:

```vb
AddHandler _btnAnalysisReport.Click,
    Sub(s, e) lnkAnalysisReport_LinkClicked(s, Nothing)
```

Compiled and worked first try with the existing async `LinkLabelLinkClickedEventArgs`-signed handler. No deviation required. Future kickoffs that need the same FlatButton ↔ LinkLabel adapter pattern can reuse this verbatim.

---

## 9. What was NOT done (kickoff scope boundary)

Strict adherence to kickoff §5 skip list. The following items are *not* in any P4e commit and remain in their pre-P4e state:

- ❌ `ANALYSIS SKIPPED` degraded render — P4f.
- ❌ `_lastSuccessfulVerdict` / `_lastSuccessfulIndicators` / `_lastSkipReason` plumbing — P4f.
- ❌ `RenderSkippedDashboard` — P4f.
- ❌ Per-card opacity overlay for stale state — P4f.
- ❌ "last successful at HH:mm:ss" timestamp line in LOG box — P4f.
- ❌ Any change to `txtOutput`, `_cardVerificationDump`, or RTF helpers — P5.
- ❌ Any modification to `UI/Controls/` (P3 library locked).
- ❌ Any change to `MainForm.Designer.vb`.
- ❌ Any change to legacy render files `MainForm_Render_Header.vb`, `MainForm_Render_Sections.vb` beyond the one-line `UpdateLogInfo` reformat allowed by kickoff §2.1 commit 2.
- ❌ Any settings.json change. No new config keys introduced; no version bump.
- ❌ Title-bar marker bump `[P4]` → `[P4e]` — kickoff §2 marked this optional and the implementation chose not to.

---

**End of report.**
