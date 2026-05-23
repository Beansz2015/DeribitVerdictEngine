# P3 Maintenance Pass — Spec-Back Report

**Author:** Claude (Opus 4.7, implementation conversation)
**Date:** 2026-05-24
**Parent spec:** `docs/p3-maintenance-pass-proposal.md`
**Commits:** `ba52994` → `f54bdad` (5 commits — 1 spec commit + 4 emergent polish/doc commits, all local-only, none pushed)

Findings, decisions, and emergent work to pass back to the spec conversation.

---

## 1. Executive summary

The P3 maintenance pass itself shipped clean in **`ba52994`** — exactly the four items in spec §3-§6 (📊 → ▤ glyph swap, `Pass2cBadge.vb` delete, `SegmentedToggle.vb` delete, dead `_analyzeButton` field delete) plus the doc work in spec §5.3 + §7 (handover promotion from worktree to project root, §4 locked-decision row added, `DeribitIndicatorProject.md:20` count refresh, §15 entry).

**The glyph choice held first try.** `▤` (U+25A4) reads cleanly in Geist Mono at 12pt bold — the §3.1.1 fallback to `≡` (U+2261) was not needed. Spec author can promote `▤` from "initial choice — verify on first screenshot" to canonical.

**Four polish items emerged during live verification** that the spec didn't anticipate. All four sit downstream of P3 in the visual chain — they only surfaced once the freshly-rendered SETTINGS & TOOLS card sat alongside the rest of the layout under live data. Total scope creep: ~17 lines of code + 1 paint-style modification to a locked P3 control. User authorised each in turn.

**One handover-doc amendment.** The fourth polish item required modifying `UI/Controls/SectionGroup.vb`. To keep the precedent from being mis-read as a general "P3 is unlocked now" signal, the UI-reskin handover §4 was amended with a **paint carve-out** (commit `f54bdad`): pure paint-style tweaks inside existing controls are allowed when card-grid consistency demands it; new controls and API surface changes stay off-limits.

---

## 2. Spec ↔ reality mismatches

### 2.1 None for the spec's own scope

Every item in spec §7 *Implementation surface summary* landed exactly as written. The grep verifications (`Pass2cBadge`, `SegmentedToggle`, `_analyzeButton`, Designer.vb) all held. SDK-style `*.vb` globbing meant no `.csproj` edit was needed, as predicted in R4. The `git mv` from the worktree path failed cleanly (worktree was inside `.claude/` which is gitignored in the main repo) — fell back to plain copy per §5.3's escape clause without issue.

### 2.2 Glyph fallback policy never invoked

§3.1.1 anticipated `▤` might read thin. It didn't. Recommend updating the canonical glyph to `▤` and demoting `≡` / `▦` / `▰` / drop to a "if user requests change later" footnote rather than a structured fallback table.

---

## 3. Decisions surfaced and resolved

### 3.1 SIGNAL BREAKDOWN + STRUCTURAL headers — tinted, then reverted

**Decision history (two flips):**

1. **`be7f64b`** — When unifying card section header sizes, I tinted SIGNAL BREAKDOWN with `Theme.ACC_INFO` (cyan) to mirror the existing STRUCTURAL (LONG/SHORT) row convention. Rationale: both are "primary data" sections; cyan creates visual coherence.

2. **`12dd54f`** — User pushed back on the cyan tint: "might conflict with the coloured content. Please change structural long/short and Signal breakdown back to the default grey." Reverted both to default `FG_SECONDARY`.

**Lesson for the spec conversation.** Section header colour decisions should consider *what sits inside the card*. STRUCTURAL cards have coloured `STRUCT STOP` / `STRUCT TARGET` / `ENTRY` / `R:R` values (red/cyan/white/dim). SIGNAL BREAKDOWN has coloured STATE pills (BULL/BEAR/NEUT in green/red/grey). A cyan header competes with those interior accents instead of framing them. Default grey (`FG_SECONDARY`) lets the content carry the colour weight.

If future card specs want a coloured header, the test is: "would the colour out-shout the data inside?" If yes, keep it grey.

---

## 4. Polish items the spec didn't anticipate

All four emerged during live verification of the P3 maintenance pass commit and were authorised individually by the user.

### 4.1 Card section header size mismatch (`be7f64b`)

**Symptom.** Two helpers in `UI/MainForm_Render_Cards.vb` rendered card section headers at different sizes:
- `MakeSectionHeader` at 9pt + `FG_QUATERNARY` (SCORE, VERDICT, LAST PRICE, ATR ENTRY LEVELS, STRUCTURAL, SIGNAL BREAKDOWN)
- `BuildPlainSectionHeader` at 11pt + `FG_SECONDARY` (OI × CVD CROSS, VOLUME PROFILE, INDICATOR DETAILS, KELLY SIZING via BuildCardHeaderWithTags)

**Fix.** Bumped `MakeSectionHeader` default to 11pt + `FG_SECONDARY` to match the larger style. STRUCTURAL passes its colour explicitly so the cyan override stayed at the time of this commit (later reverted in `12dd54f` per §3.1).

**Suggestion for the spec author.** If a P5 cleanup spec lands, consider consolidating the two helpers into one — there's no reason to keep both. `BuildPlainSectionHeader` is now functionally equivalent to a no-colour-arg `MakeSectionHeader`.

### 4.2 SIGNAL BREAKDOWN TOTAL row out-shouted the header (`12dd54f`)

**Symptom.** The TOTAL row at line 1845 was 12pt bold — larger than the 11pt section header above it. User: "TOTAL seems to be larger than headers. Please reduce size to be smaller than headers but larger than the other content in that section."

**Fix.** 12pt → 10.5pt bold. Now sits cleanly between the 11pt header and the 9.5pt signal-row content (`MakeBreakdownCell`).

### 4.3 VERDICT card crowding under long verdict strings (`12dd54f`)

**Symptom.** When the verdict ran long (e.g. `NO TRADE [WEAK LONG]` under MTF-blocked weak signals), the 22pt verdict text + 2×2 sub-grid (CONTEXT / REGIME / MTF / HOLD) + eff/penalty sub-row + regime-anchor warn row all overlapped vertically inside the 160 px hero row. The recent 11pt section-header bump tightened things further.

**Fix.** Two-pronged:
- Verdict text 22pt → 18pt. Still the visual headline; no longer pushes into the sub-grid on long strings.
- Hero row 160 → 180 px in `_gridRoot`. SCORE arc and LAST PRICE block had headroom to spare so they don't look stretched.

User explicitly authorised both approaches: "You can reduce the big verdict text's size and/or increase the section's height (together with score/last price) if necessary." Applied both for margin.

**Suggestion for the spec author.** Card pixel budgets in future kickoffs should anticipate "worst-case content string length" for the dominant headline label, not the median case. The 22pt budget assumed the verdict was 1-2 words ("LONG", "WEAK LONG"); MTF-blocked relabelling produces 3-word strings that don't fit.

### 4.4 SectionGroup title size mismatch (`bb7cd57`)

**Symptom.** After §4.1 unified card section headers to 11pt + `FG_SECONDARY`, the sub-box titles inside the SETTINGS & TOOLS card (LOG / AUTO-RUN / TOOLS) still read at 9pt + `FG_QUATERNARY` because they're painted by `SectionGroup.OnPaint` in the locked P3 control. Visual hierarchy was inverted: card headers louder than sub-box titles, which is correct, but the magnitude difference looked unintentional.

**Fix (touches P3).** With explicit user authorisation:
- `SectionGroup.vb` title font: 9pt → 11pt bold.
- Title `ForeColor`: `FG_QUATERNARY` → `FG_SECONDARY`.
- Border rect Y: 18.5 → 20.5 to give the larger title ~3 px clearance below the glyph descenders.

No API surface change. No constructor/property changes. No consumer code touched.

**Locked-decision tracking** (covered in §5 below).

---

## 5. Handover-doc amendments (`f54bdad`)

Touching `UI/Controls/SectionGroup.vb` in §4.4 crossed the "no further modifications" line in the UI-reskin handover §4. To prevent that precedent from being mis-read as "P3 is unlocked now," the user instructed me to formalise a paint carve-out.

Two rows in §4 updated:

1. **"16 custom controls in `UI/Controls/`, no further modifications"** →
   *"14 custom controls in `UI/Controls/`. No new controls and no API surface changes. Paint carve-out: pure paint-style tweaks (font size, ForeColor, border placement) inside an existing control are allowed when card-grid consistency demands it — e.g. SectionGroup title bumped 9pt FG_QUATERNARY → 11pt FG_SECONDARY in `bb7cd57` to match the global MakeSectionHeader change. No consumer code touched, no constructor/property surface changes."*

   Count refreshed 16 → 14 (Pass2cBadge + SegmentedToggle were deleted in the maintenance pass itself).

2. **"`SectionGroup` not modified — title-colour requires inline composition"** →
   *"SectionGroup — per-instance title colour/font overrides still require inline composition, not new properties on the control. Global default style changes (e.g. the 2026-05-24 `bb7cd57` 9pt→11pt bump) are allowed under the §4 paint carve-out."*

   The original rule was about preventing per-card colour overrides creeping into the control's property surface. That principle still holds. The new sentence scopes it correctly so global-default tweaks aren't blocked.

**Suggestion for the spec author.** The paint carve-out is narrow on purpose:
- ✅ Allowed: font size, `ForeColor`, border placement / corner radius — anything inside an existing control's paint code that doesn't change what consumers must pass in.
- ❌ Not allowed: new public properties / events, signature changes, new controls, deleting controls (the maintenance pass spec is the only authorised vector for deletions).

If a future need pushes against the boundary, that's a spec call, not an implementation call.

---

## 6. Commit ledger

| # | SHA | Subject | Files | LoC |
|---|---|---|---|---|
| 1 | `ba52994` | `fix(ui-reskin): P3 maintenance pass — 📊 tofu fix + prune unused controls` | `UI/Controls/AnalysisReportButton.vb`, `UI/Controls/Pass2cBadge.vb` (D), `UI/Controls/SegmentedToggle.vb` (D), `UI/MainForm_Layout.vb`, `docs/DeribitIndicatorProject.md`, `docs/ui-reskin-handover-2026-05-22.md`, `docs/p3-maintenance-pass-proposal.md` | +688 / -180 |
| 2 | `be7f64b` | `fix(ui-reskin): unify card section-header style + tint SIGNAL BREAKDOWN` | `UI/MainForm_Render_Cards.vb` | +10 / -3 |
| 3 | `12dd54f` | `fix(ui-reskin): revert header tinting + VERDICT card crowding + TOTAL row size` | `UI/MainForm_Render_Cards.vb`, `UI/MainForm_Layout.vb` | +17 / -9 |
| 4 | `bb7cd57` | `fix(ui-reskin): bump SectionGroup title 9pt FG_QUATERNARY → 11pt FG_SECONDARY` | `UI/Controls/SectionGroup.vb` | +10 / -5 |
| 5 | `f54bdad` | `docs(ui-reskin): handover §4 — formalise P3 paint carve-out` | `docs/ui-reskin-handover-2026-05-22.md` | +2 / -2 |

All commits local-only. None pushed. None touched `MainForm.Designer.vb`, `settings.json`, scoring engine, indicators, or analysis pipeline.

Net library delta: 16 → 14 controls. Net code delta inside `UI/Controls/`: −252 LoC (two file deletions, one tiny paint tweak).

---

## 7. Suggestions for the spec author

### 7.1 (LOW) Consolidate `MakeSectionHeader` and `BuildPlainSectionHeader`

Now that both produce identical output (11pt + FG_SECONDARY default, optional colour override), one of them is redundant. The natural keeper is `MakeSectionHeader` since it accepts the optional colour parameter. `BuildPlainSectionHeader` can be replaced with no-arg `MakeSectionHeader` calls at four call sites and deleted. Cosmetic cleanup; no functional change.

### 7.2 (LOW) Bake the worst-case-string-length budget into kickoff templates

Card pixel budgets like the hero row's 160 px (later 180 px) and the verdict label's 22pt (later 18pt) implicitly assumed median-case content. Add a one-line check to future card kickoffs: "what's the longest plausible string for the dominant headline label?" — and size from there.

### 7.3 (LOW) The §4 paint carve-out is precedent-setting; reference it in future P3-touching specs

If a future spec proposes any change to `UI/Controls/*.vb`, it should:
- Cite the §4 carve-out language.
- Confirm the change is paint-style (no API surface impact).
- Confirm no consumer code needs updating.

If any of those don't hold, it's a structural P3 change and needs a fresh locked-decision discussion, not just the carve-out invocation.

### 7.4 (NICE) The post-`▤` glyph fallback policy in spec §3.1.1 can be demoted

`▤` worked first try. The fallback table can become a footnote rather than a structured table.

---

## 8. What was NOT done (scope boundary)

The five-commit chain stayed strictly inside UI / docs:

- ❌ No scoring engine touches.
- ❌ No indicator code touches.
- ❌ No `settings.json` changes — no new keys, no version bump.
- ❌ No `MainForm.Designer.vb` edits.
- ❌ No changes to legacy render files beyond what the spec authorised.
- ❌ No `txtOutput` / `_cardVerificationDump` changes (P5 territory).
- ❌ No P4f / P5 work.
- ❌ No CSV schema changes.
- ❌ Nothing pushed to the remote.

The P3 paint carve-out is the only locked-decision change. Everything else is consistent with the pre-P3-maintenance posture of the codebase.

---

**End of report.**
