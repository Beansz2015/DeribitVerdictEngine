# P3 Maintenance Pass — Proposal

**Status:** PROPOSED
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-24
**Spec target:** `UI/Controls/` (P3 library) + one field removal in `UI/MainForm_Layout.vb`
**Settings.json impact:** none
**Scoring / engine impact:** none

---

## 1. Motivation

P4d and P4e spec-backs flagged four cleanup items that fall inside the P3 control library. They've accumulated as P4 phases shipped without unilaterally touching P3 (the library is locked once shipped per the reskin workflow). Bundling them into one short maintenance pass closes them before P4f / P5 land.

The items:

1. **HIGH — `AnalysisReportButton` 📊 icon renders as tofu** (P4e spec-back §5.1 + §8.1). Geist Mono has no glyph for U+1F4CA; GDI+ `Label.DrawString` doesn't invoke font-link fallback to Segoe UI Emoji, so a tofu square renders.
2. **LOW — `Pass2cBadge` is design-stale and unused.** P4c shipped a 4-state SIGNAL BREAKDOWN footer (SUPPRESSED / ALIGNED↑ / ALIGNED↓ / CONFLICT) via inline `MakeFooterAggregate(...)`. The P3 control was specced as a 3-state enum that doesn't match what the engine actually emits.
3. **LOW — `SegmentedToggle` is unused.** P4e §4.9 chose the "reuse radios as source of truth" path with a `Pill` chip mirror. The `SegmentedToggle` wrapper around `rbSingle`/`rbRepeat` is now orphan code.
4. **LOW — Vestigial `_analyzeButton As FlatButton` field.** Declared at [UI/MainForm_Layout.vb:148](UI/MainForm_Layout.vb:148) during P4a, never instantiated, never assigned. The Designer `btnAnalyze` Button retains its role per the handover's locked decision; the field is dead.

Not in scope (per locked decisions in the handover doc §4):

- `FlatButton` replacing `btnAnalyze` — "keep Designer Buttons for now (backlog) | accept"
- `ChipNumeric` replacing `nudMinutes`/`nudSeconds` — "keep Designer NUDs for now (backlog) | accept"

These remain backlogged. Don't reopen them here.

---

## 2. Non-goals

- No new controls. No control-API changes that would force consumers to update (the prune path deletes whole controls; no consumer touches either of them).
- No font bundling changes (no extra .ttf). Fix #1 stays inside the existing Geist Mono surface.
- No scoring, indicator, or settings.json changes.
- No engine-side changes. Pure UI library hygiene.

---

## 3. Fix #1 — `AnalysisReportButton` 📊 tofu

### 3.1 Recommended approach

**Option 1 from P4e spec-back §5.1 — swap U+1F4CA for a BMP-range glyph that Geist Mono carries.**

Smallest surface change. Single character edit in [UI/Controls/AnalysisReportButton.vb:24](UI/Controls/AnalysisReportButton.vb:24):

```vb
' BEFORE
Me.IconText = Char.ConvertFromUtf32(&H1F4CA)   ' 📊

' AFTER (initial choice — verify on first screenshot, see §3.1.1)
Me.IconText = "▤"   ' U+25A4 SQUARE WITH HORIZONTAL FILL — bar-chart silhouette in monospace
```

#### 3.1.1 Glyph fallback policy

`▤` lives in Geist Mono's BMP coverage but at 12pt bold may render as a thin three-bar stack rather than reading as a chart icon. **First verification screenshot decides.** If `▤` reads poorly, swap in the same commit to:

| Priority | Codepoint | Glyph | Why |
|---|---|---|---|
| Initial | U+25A4 | `▤` | Closest bar-chart silhouette. Try first. |
| **First fallback** | **U+2261** | **`≡`** | Triple horizontal bar — Geist Mono hints it cleanly at small sizes; widely used in menu/list/report affordances. **Use this if `▤` looks thin.** |
| Second fallback | U+25A6 | `▦` | Square with grid — denser, reads as "table" |
| Last resort | U+25B0 | `▰` | Black parallelogram — closer to "tile" than "chart" |
| Escape hatch | (drop) | — | The `→` trailing arrow already signals action. Cheapest. |

No structural code change between options — only the string literal at line 24. The implementation conversation pre-commits to nothing; the kickoff verification gate (§8 step 2) is the decision point.

### 3.2 Rejected approaches (already considered)

- **Option 2 — composite font split in `FlatButton.OnPaint`.** Would require splitting icon paint from text paint inside the base `FlatButton`, then applying `Segoe UI Emoji` to the icon and `Geist Mono` to the label. Touches every `FlatButton` consumer indirectly. High blast radius for one icon.
- **Option 3 — drop the icon entirely.** Acceptable but loses the visual hierarchy that distinguishes the ANALYSIS REPORT CTA from a plain `FlatButton`. The amber glow halo + → arrow remain, but the design intent was a "report" affordance — keeping a BMP glyph preserves that read better than nothing.

### 3.3 Why no font bundling work

Geist Mono is a clean monospace family aimed at code/data display. Adding a second `.ttf` with emoji coverage solely to support one icon doesn't pay for itself; option 1 keeps the bundle small.

---

## 4. Fix #2 — Prune `Pass2cBadge`

### 4.1 What gets deleted

- **File:** `UI/Controls/Pass2cBadge.vb` — entire file removed.
- **Imports / consumers:** none. Verified via `grep "Pass2cBadge"` across the codebase — only references are inside the control file itself.

### 4.2 Why prune vs refactor

The control as built is a 3-state `Outcome` enum (`ALIGNED ↑` / `CONFLICT ↓` / `SUPPRESSED`). The engine emits four states the SIGNAL BREAKDOWN footer needs to render distinctly:

- SUPPRESSED (item not emitted)
- ALIGNED LONG (LongHit=True, ShortHit=False)
- ALIGNED SHORT (LongHit=False, ShortHit=True)
- CONFLICT (both hits False — Pass 2c penalty)

The inline `MakeFooterAggregate(...)` at [UI/MainForm_Render_Cards.vb:2566](UI/MainForm_Render_Cards.vb:2566) handles all four with directional arrow colour driven by side. Refactoring `Pass2cBadge` to 4 states would mean a control that does less than the inline pattern already does. Cleaner to drop.

### 4.3 If a future phase needs a typed Pass 2c badge

The inline code in `BuildBreakdownFooter` is the source of truth for the rendering pattern. A future spec can lift it into a fresh control with the correct 4-state enum if other cards need the same widget. No urgency.

---

## 5. Fix #3 — Prune `SegmentedToggle`

### 5.1 What gets deleted

- **File:** `UI/Controls/SegmentedToggle.vb` — entire file removed.
- **Imports / consumers:** none. Verified via grep.

### 5.2 Why prune vs retain

P4e §4.9 settled on the "Pill chip mirrors radio state" pattern with `rbRepeat`/`rbSingle` as the source of truth. This is what the proposal §4.9 itself recommends ("Reuse existing radios as the source of truth — don't replace them"). `SegmentedToggle` was specced before that resolution landed.

Retaining the control as "available for future use" carries an ongoing maintenance cost (paint code, theme-token references, the `FlatButton` dependency) for zero current value. If a future card wants a segmented toggle pattern, it can either:
- Re-create from the deleted source (it's in git history)
- Or use the cleaner P4e pattern (radios + Pill mirror, both reused)

### 5.3 Locked decision to capture in the UI-reskin handover doc

After this spec ships, add the following row to the **UI reskin handover doc's §4 *Locked architectural decisions* table** (NOT `docs/DeribitIndicatorProject.md` — that doc's §4 is the Indicator Signal Map):

> | REPEAT/SINGLE mode display: `Pill` chip mirroring `rbRepeat`/`rbSingle` radios (radios stay the source of truth). `SegmentedToggle` wrapper retired. | P4e + this spec | locked |

**Doc-location caveat.** The UI reskin handover currently lives at `.claude/worktrees/eloquent-aryabhata-bfee65/docs/ui-reskin-handover-2026-05-22.md` — inside a worktree, not at project root. This is fragile: the next session may not find it. **This spec's commit promotes the handover to project root** as `docs/ui-reskin-handover-2026-05-22.md` (or `docs/ui-reskin-handover.md` if the spec author prefers a date-less canonical name — pick one), then adds the SegmentedToggle row to §4 in the promoted file.

Two-step in the same commit:

1. `git mv .claude/worktrees/eloquent-aryabhata-bfee65/docs/ui-reskin-handover-2026-05-22.md docs/ui-reskin-handover-2026-05-22.md` (or use a plain copy if `git mv` from a worktree path doesn't resolve cleanly; in that case the worktree copy stays as a snapshot artifact and the project-root copy becomes canonical).
2. Edit the promoted file's §4 table to append the row above.

After this, the handover is discoverable from project root and the §4 table has the locked entry.

---

## 6. Fix #4 — Delete dead `_analyzeButton` field

Single-line removal:

```vb
' UI/MainForm_Layout.vb:148 — BEFORE
Friend _analyzeButton     As FlatButton

' AFTER — (line removed)
```

Verified via grep that nothing reads or writes the field. It was added during P4a as forward-looking scaffolding for a `FlatButton`-replacement that the handover §4 explicitly deferred to backlog.

---

## 7. Implementation surface summary

| File | Change | Specific edit |
|---|---|---|
| `UI/Controls/AnalysisReportButton.vb` | Line 24: replace `Char.ConvertFromUtf32(&H1F4CA)` with `"▤"` (or fallback per §3.1.1) | One literal swap |
| `UI/Controls/Pass2cBadge.vb` | Delete file | — |
| `UI/Controls/SegmentedToggle.vb` | Delete file | — |
| `UI/MainForm_Layout.vb` | Line 148: delete `_analyzeButton` field declaration | One line removed |
| `docs/DeribitIndicatorProject.md` | **Line 20:** the P3 shipments entry currently reads "16 controls in `UI/Controls/` (... plus 10 composition controls: ..., `SegmentedToggle`, ..., `Pass2cBadge`)". Update to "14 controls" and drop `SegmentedToggle` and `Pass2cBadge` from the parenthesised list (composition count drops 10 → 8). | Two glyph swaps + two list deletions on one line |
| `docs/DeribitIndicatorProject.md` | Recent shipments entry at `§15` for this maintenance pass | New table row |
| `docs/architecture.md` | **No edit required.** Verified via grep — architecture.md does not list individual controls in `UI/Controls/`. The §UI/ subtree comment lists `UI/Controls/` as a directory only. If a future doc refresh adds per-control listings, update then. | — |
| `docs/ui-reskin-handover-2026-05-22.md` (after promotion per §5.3) | §4 *Locked architectural decisions* table: add the SegmentedToggle row | New table row |

Build: one `dotnet build /c/Dev/DeribitVerdictEngine/DeribitVerdictEngine.sln` to confirm no orphan reference re-surfaces (Designer.vb shouldn't reference either deleted control; verify).

---

## 8. Verification

1. `dotnet build` clean — no missing-type errors from the file deletions.
2. Launch app. ANALYSIS REPORT CTA in the SETTINGS & TOOLS section shows `▤  ANALYSIS REPORT  →` (or the chosen glyph) instead of `[]  ANALYSIS REPORT  →`.
3. Run one analysis. SIGNAL BREAKDOWN footer renders the Pass 2c row unchanged (inline `MakeFooterAggregate` path is untouched).
4. Tab order unchanged — no controls were re-parented.
5. Auto-run continues normally.

No live-data dependency. Verification is single-cycle, ~2 minutes including build.

---

## 9. Phasing

One commit. Suggested subject:

```
fix(ui-reskin): P3 maintenance pass — 📊 tofu fix + prune unused controls

- AnalysisReportButton: swap U+1F4CA (📊) for U+25A4 (▤) (or U+2261
  fallback if ▤ reads thin at 12pt bold — verified on first
  screenshot). Geist Mono has no glyph for the supplementary-plane
  emoji and GDI+ doesn't trigger Segoe UI Emoji fallback inside
  Label.DrawString, so the icon was rendering as tofu in the new
  SETTINGS & TOOLS layout (P4e spec-back §5.1).
- Delete Pass2cBadge.vb — design-stale 3-state enum; SIGNAL BREAKDOWN
  footer uses inline MakeFooterAggregate with the correct 4-state
  shape (SUPPRESSED / ALIGNED↑ / ALIGNED↓ / CONFLICT).
- Delete SegmentedToggle.vb — superseded by P4e's Pill-mirrors-radio
  pattern (radios stay source of truth).
- Delete dead _analyzeButton field in MainForm_Layout (P4a scaffold
  that never got instantiated; UI reskin handover §4 locked btnAnalyze
  as Designer Button).
- Promote ui-reskin-handover-2026-05-22.md from worktree to docs/ root;
  add SegmentedToggle retirement to its §4 Locked Decisions table.
- Update docs/DeribitIndicatorProject.md:20 — 16 controls → 14;
  drop SegmentedToggle + Pass2cBadge from the parenthesised list.

No scoring impact. No settings.json change. No engine touch.
```

---

## 10. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | Chosen BMP glyph (`▤`) may not be hinted well at the 12pt size `AnalysisReportButton` uses. | Verification step 2 catches this. If it reads poorly, swap to `▦` or `≡` in a follow-up one-liner. |
| R2 | `MainForm.Designer.vb` could reference one of the deleted controls. | Verified via grep at draft time — no references. Build will catch any drift between now and the kickoff. |
| R3 | A future spec author may not see the SegmentedToggle retirement and propose adding it back. | §5.3 codifies the lock in the handover doc. Spec-back step on this proposal confirms the handover edit happened. |
| R4 | Deleting two `.vb` files may cause `.csproj` compile-include drift if the project file explicitly lists items. | The project uses SDK-style implicit globbing (`*.vb`) — verified. File deletion alone is sufficient; no `.csproj` edit needed. |

---

## 11. Approval gate

User reviews and either:
- Approves wholesale → kickoff drafted, single commit ships.
- Approves with revisions (e.g., different BMP glyph, drop one of the prunes) → spec updates first.

This is a small spec; the implementation conversation can be Opus 4.7 Medium with the kickoff being barely longer than this doc's §7 + §8.

---

**End of proposal.**
