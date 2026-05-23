# UI Reskin P4d — Spec-Back Report

**Author:** Claude (Opus 4.7, implementation conversation)
**Date:** 2026-05-23
**Parent specs:** `docs/ui-reskin-proposal.md` + `docs/ui-reskin-p4-gap-checklist.md`
**Kickoff doc:** P4d kickoff (pasted into the implementation conversation)
**Commits:** `1385a3a` → `9934e9c` (7 commits, all local-only, none pushed)

Findings, decisions, and deviations to pass back to the spec conversation. Each item lists what the spec assumed vs. what the codebase actually exposes, and what the implementation did.

---

## 1. Executive summary

P4d shipped all four planned commits + two post-verification polish commits. Build clean throughout. Every numeric value displayed in the new cards cross-checks against the legacy `txtOutput` dump on two live runs (2026-05-23 17:26:41 and 17:32:04 UTC+8).

**Deviations from spec, by category:**
- **4 field-name / API mismatches** between kickoff pseudocode and the real `EngineSettings` / `IndicatorResults` / `SectionGroup` / `OiCvdBadge` surfaces. All resolved by using the real names (no kickoff-mandated changes were impossible to honour).
- **4 deliberate decisions** taken after surfacing options to the user (recorded in the implementation transcript). All four favoured "preserve existing features / use what's there" over "follow the kickoff literally."
- **1 pre-existing P4c bug** uncovered and fixed during live verification (Pass 2c CONFLICT case).
- **2 post-verification polish fixes** (card height clipping on KELLY and INDICATOR DETAILS).
- **1 NICE-severity gap deferred** (OFI Bid/Ask Vol — GAP-72/73 — has no display location in the new UI).

**Suggestion priority for the spec conversation:** items in §8 are ranked by how much they would have saved implementation time if the kickoff had been right the first time.

---

## 2. Spec ↔ reality mismatches

### 2.1 `SpreadSettings.WidePenaltyThresholdBps` does not exist

**Kickoff said** (§3b OI × CVD CROSS bind helper):
```vb
Dim spreadPct As Single = CSng(Math.Min(100.0,
    (r.SpreadBps / SettingsLoader.Current.Indicators.Spread.WidePenaltyThresholdBps) * 100.0))
```

**Real POCO** (`Core/Settings/EngineSettings.vb:398-400`):
```vb
Public Class SpreadSettings
    <JsonPropertyName("wide_threshold_bps")>  Public Property WideThresholdBps  As Double = 5.0
    <JsonPropertyName("tight_threshold_bps")> Public Property TightThresholdBps As Double = 1.5
End Class
```

**Implementation used:** `cfg.Indicators.Spread.WideThresholdBps`. Functionally identical to what the kickoff intended — just the wrong name.

**Spec amendment:** s/WidePenaltyThresholdBps/WideThresholdBps/ in any future P4d-derivative kickoff. The "penalty" suffix may have leaked from the related `cfg.Scoring.SpreadWidePenalty` field (which controls the scoring penalty integer, not the threshold).

---

### 2.2 `OiCvdBadge.OiCvdOutcome` enum naming

**Kickoff said:**
```vb
Private Shared Function MapOiCvdOutcome(s As String) As OiCvdBadge.OiCvdOutcome
    Select Case s.ToUpper()
        Case "CONFIRMED_LONG"  : Return OiCvdBadge.OiCvdOutcome.ConfirmedLong
        Case "CONFIRMED_SHORT" : Return OiCvdBadge.OiCvdOutcome.ConfirmedShort
        ...
```

**Real control** (`UI/Controls/OiCvdBadge.vb:17-22`):
```vb
Public Enum OiCvdOutcomeKind
    CONFIRMED_LONG
    CONFIRMED_SHORT
    CONFLICT
    NEUTRAL
End Enum
```

**Implementation used:** `OiCvdBadge.OiCvdOutcomeKind.CONFIRMED_LONG` etc. (SCREAMING_SNAKE_CASE, suffix `Kind`, not PascalCase as the kickoff implied).

**Spec amendment:** when the spec quotes types from existing P3 controls, copy the exact identifier. PascalCase enum members are a C#/.NET convention, but the actual P3 controls use SCREAMING_SNAKE matching the engine's `OiCvdOutcome` string emissions.

---

### 2.3 `SectionGroup` has no `Title` / `TitleColour` properties

**Kickoff said** (§4 INDICATOR DETAILS — `NewSectionGroup` helper):
```vb
Return New SectionGroup() With {
    .Title = title,
    .TitleColour = If(titleAccent.IsEmpty, Theme.FG_TERTIARY, titleAccent),
    ...
}
```

**Real control** (`UI/Controls/SectionGroup.vb`):
- Property `Title` exists ✓
- Property `TitleColour` does **not** exist. Title text is hardcoded to `Theme.FG_QUATERNARY` inside the control's `OnPaint`.
- Exposes `AccentColor` (border tint) and `BorderStyle2`. Neither controls title-text colour.

**Kickoff's "If you get stuck" section anticipated this** — the user explicitly confirmed the fallback: build groups inline (header `Label` + bordered `FlowLayoutPanel`).

**Implementation used:** `BuildGroupInline(title, titleColour)` returning a `(host, body)` tuple. Twelve groups across both columns built this way. `SectionGroup` not used.

**Spec amendment options (pick one):**
1. **Extend `SectionGroup`** with a `TitleColour` property (P3 control change, separate kickoff). Then future cards can use it.
2. **Document the "title colour requires inline composition" pattern** in the proposal §6 controls reference. The `BuildGroupInline` helper added in commit 4 could be promoted to a thin reusable control if cards beyond INDICATOR DETAILS need it.
3. **Leave as-is** — only INDICATOR DETAILS needed coloured per-group titles. The inline pattern is fine for one card.

Recommendation: option 2. `SectionGroup` was originally specced for the SETTINGS & TOOLS sub-boxes (LOG / AUTO-RUN / TOOLS) which don't need title colour. Don't bloat the control for a single consumer.

---

### 2.4 Section 3 bind helpers (`AddBadgeRow`, `AddMiniMeter`, `AddSubLabel`, `AddSectionHeader`) didn't exist

**Kickoff said** (§3 VOLUME PROFILE + OI × CVD CROSS):
```vb
AddSectionHeader(_cardVolumeProfile, "VOLUME PROFILE")
AddBadgeRow(_cardOiCvdCross, badge, ResolveOiCvdNote(...))
AddMiniMeter(_cardOiCvdCross, "Funding Mom", ...)
AddSubLabel(_cardVolumeProfile, r.VPFRSignal.Replace(...), Theme.FG_TERTIARY)
```

**Reality:** only `MakeSectionHeader` existed in `MainForm_Render_Cards.vb`. The other three did not.

**User confirmed:** add as private helpers in `MainForm_Render_Cards.vb` (same pattern as `AddCardHeaderWithTags` / `AddCardKvRow` from commit 2).

**Implementation added:**
- `BuildPlainSectionHeader(text)` — reused by KELLY / VOLUME PROFILE / OI × CVD / INDICATOR DETAILS cards. Named differently from existing `MakeSectionHeader` because it returns a slightly larger 11pt bold header (the existing one is 9pt for the row 3-5 cards).
- `BuildBadgeRow(badge, noteText)`
- `BuildMiniMeter(label, valueText, pct, barColour)`
- `BuildSubLabel(text, colour)`
- `AddLevelRow(parent, label, value, colour, bold, suffix)` — VOLUME-PROFILE-specific price-level row factory

**Spec amendment:** add a "composition helpers" subsection to the proposal §6 listing the existing `Make*` / `Build*` / `Add*` helpers in `MainForm_Render_Cards.vb` with their signatures. Several kickoffs assumed they existed; adding the inventory removes that ambiguity.

---

## 3. Decisions surfaced and resolved

### 3.1 Commit 1 — preserve `MakeSignalRow` existing features

**Kickoff sig:**
```vb
MakeSignalRow(label, state, stateColour, note, scText, scColour, Optional subNote)
```

**Existing sig:**
```vb
MakeSignalRow(label, state, stateColour, note, sc As Integer?, Optional subNote, Optional subNoteColour)
```

The kickoff sig drops two existing features: (a) `sc As Integer?` overload that renders "—" in `FG_DIM` overlay for non-voting rows, and (b) `subNoteColour` parameter for the Swing Pivots D2 best-vol sub-line.

**User confirmed:** preserve existing features. Kickoff sig was illustrative.

**Implementation kept the existing sig.** Internally derives `scText` / `scColour` from `Integer?`. Sub-note path preserved as a second TableLayoutPanel row spanning the NOTE+SC cells.

**Spec amendment:** when a kickoff replaces an existing function, explicitly state whether feature parity is required. Default should be "yes, unless you say otherwise."

---

### 3.2 Commit 4 — `LastTwoHighs5m.Newer/Older` field shape confirmed

**Kickoff asked:** confirm `IndicatorResults` exposes `LastTwoHighs5m` as a struct/tuple, not separate fields.

**Real shape** (`Core/IndicatorResults.vb:125-126`):
```vb
Public Property LastTwoHighs5m As (Older As Double, Newer As Double)
Public Property LastTwoLows5m  As (Older As Double, Newer As Double)
```

**Implementation:** used as specced — `r.LastTwoHighs5m.Newer` / `.Older`.

**No action needed.** Recording for completeness.

---

### 3.3 Commit 4 — `MicroCVDSignal` 5-state values confirmed

**Kickoff asked:** confirm `BULL_ACCEL` / `BULL_DECEL` / `BEAR_ACCEL` / `BEAR_DECEL` / `FLAT` are the emitted strings.

**Confirmed** via `Core/IndicatorResults.vb:75` comment. All five values used in `BuildGroupMicroCvd` with the same pale-green / pale-red distinction the SIGNAL BREAKDOWN row uses for `BULL_DECEL` / `BEAR_DECEL`.

---

### 3.4 Commit 3 — `v.OiCvdOutcome` field confirmed present

**Kickoff flagged:** field may not exist on `VerdictResult`; if absent, derive from SignalBreakdown lookup like P4c did for Pass 2c.

**Real shape** (`Core/ScoringEngine_Types.vb:53`):
```vb
Public Property OiCvdOutcome As String = "NONE"
```

Exists. Implementation reads it directly. No SignalBreakdown fallback needed.

---

## 4. Gaps deferred / not shipped

### 4.1 OFI Bid/Ask Vol (GAP-72 / GAP-73) — no display location

**Severity:** NICE (per gap checklist).

**Issue:** the kickoff text says these "live in OI × CVD CROSS card (commit 3), not here" (in the INDICATOR DETAILS commit). But the commit 3 OI × CVD CROSS bind spec only lists: outcome badge + Funding Mom MiniMeter + Spread MiniMeter. Bid/Ask Vol have no specified place in the new UI.

**Legacy dump shows them** (`MainForm_Render_Sections.vb:215-218`):
```
OFI Ratio: 1.21  |  Bid Vol: 1647600  |  Ask Vol: 1363830  |  BALANCED  |  Mom: FLAT
```

**Current state:** dropped from the new UI. The SIGNAL BREAKDOWN OFI row only shows `ratio 1.21` in the note column.

**Suggested resolution (spec author chooses):**
1. **Append to SIGNAL BREAKDOWN OFI row note:** `ratio 1.21  |  bid 1.65M ask 1.36M`. Cleanest, no new card real estate. Per gap checklist NICE severity this matches.
2. **Add as KV rows inside OI × CVD CROSS card** below the Spread meter. Card is currently 210 px and would grow ~30 px.
3. **Accept the drop.** NICE severity means trader can live without it.

Recommendation: option 1. The trader's mental model treats Bid/Ask Vol as supporting detail for OFI Ratio, not a standalone metric. SIGNAL BREAKDOWN's NOTE column already carries that kind of supporting detail.

---

### 4.2 `SpreadStatus` (GAP-74) — partially covered

**Severity:** MUST (per gap checklist).

**Issue:** GAP-74 in the checklist says "Spread status text (`r.SpreadStatus` — `WIDE` / `TIGHT` / `NORMAL`) — OI × CVD CROSS card / Signal Breakdown".

**Current state:**
- **SIGNAL BREAKDOWN** row "Spread" shows the status as a state pill (TIGHT green, WIDE red, NORMAL grey) — covers the MUST requirement. ✓
- **OI × CVD CROSS card** Spread MiniMeter colours the bar by status but doesn't render the status word. Bar colour alone might be ambiguous.

**Suggested resolution:** add a state suffix to the Spread MiniMeter value text: `"0.07 bps · TIGHT"` instead of just `"0.07 bps"`. Trivial 5-line change. Did not ship in P4d because GAP-74's MUST requirement is already met by the SIGNAL BREAKDOWN row.

---

## 5. Pre-existing bugs surfaced and fixed

### 5.1 Pass 2c CONFLICT case rendered as SUPPRESSED

**Origin:** P4c (predecessor commit, before P4d). Not introduced by P4d.

**Engine emission permutations** (from `Core/ScoringEngine_Calculate_Scoring.vb:433-510, 753`):

| Engine state | Item emitted? | LongHit | ShortHit | Note prefix |
|---|---|---|---|---|
| SUPPRESSED (TRANSITIONAL / zero-net) | NO | — | — | — |
| ALIGNED LONG  | YES | True | False | `+N REGIME ALIGN [...]` |
| ALIGNED SHORT | YES | False | True | `+N REGIME ALIGN [...]` |
| CONFLICT (any direction) | YES | **False** | **False** | `-N REGIME CONFLICT [...]` |

**Bug:** P4c code had a "both hits True → CONFLICT" branch that was **dead code** (engine never sets both). The CONFLICT case (both False) fell through to the final `Else → SUPPRESSED`, so a `-1 REGIME CONFLICT` run rendered the badge as `SUPPRESSED` with no penalty indication.

**Fix** (commit `9934e9c`):
- Drop the dead Both-True branch.
- Long-only / Short-only branches now require negation of the other side.
- The trailing Else treats both-False as CONFLICT.
- Tag text (`±N regime`) derives ±N magnitude from the note prefix via new `ExtractPass2cTag` helper. Non-default `RegimeWeights.AlignmentBonus` / `ConflictPenalty` config values render correctly instead of always saying `±1`.

**Spec amendment:** the gap checklist GAP entries didn't capture Pass 2c footer logic since that's not a "missing field" per se — it was an existing-impl bug. Suggest adding an "Engine emission semantics audit" pass to any future kickoff that consumes `SignalBreakdownItem.LongHit/ShortHit` — read the emission site and tabulate all permutations before writing the consumer.

---

## 6. Post-verification polish fixes

Both card heights underestimated in the original commits. Fixed in follow-up commits.

| Card | Original | Fixed | Reason |
|---|---|---|---|
| `_cardIndicatorDetails` (row 9) | 480 | 760 | 6 inline groups × ~100-130 px each per column ≈ 720; needed margin |
| `_cardKelly` (row 8) | 180 | 220 | 7-element vertical stack lands ~180 px content; needed margin |

**Spec amendment:** when the proposal sizes a card by "rough content estimate," add an explicit verification step: "build, run, screenshot, measure clipping." The card row heights are absolute pixels (not auto-grow), so under-sizing silently clips lower content without raising an error.

---

## 7. Commit ledger

| # | Hash | Subject | LOC |
|---|---|---|---|
| 1 | `1385a3a` | fix(ui-reskin): P4c — align SIGNAL BREAKDOWN columns to headers | +113/−87 |
| 2 | `399f71b` | feat(ui-reskin): P4d — KELLY card expansion (GAP-07..16) | +217/−2 |
| 3 | `d084d5d` | feat(ui-reskin): P4d — VOLUME PROFILE + OI × CVD CROSS card bindings | +272/−1 |
| 4 | `b7ea350` | feat(ui-reskin): P4d — INDICATOR DETAILS card (GAP-15..51 absolute detail) | +340/−7 |
| 5 | `aa7e16f` | fix(ui-reskin): P4d — INDICATOR DETAILS card height 480 → 760 | +5/−1 |
| 6 | `9934e9c` | fix(ui-reskin): P4d/c — Pass 2c CONFLICT footer + KELLY card clip | +61/−16 |

All commits local-only. Push gate: user verifies end-to-end on live data (per `crypto-trading-context` workflow rule).

---

## 8. Suggestions for the spec author

Ranked by implementation-time impact. The first three would each have saved 5-15 minutes if the kickoff had carried them.

1. **Quote real type names exactly.** Kickoff pseudocode used `OiCvdBadge.OiCvdOutcome.ConfirmedLong`; real enum is `OiCvdBadge.OiCvdOutcomeKind.CONFIRMED_LONG`. Same for `SpreadSettings.WidePenaltyThresholdBps` (real: `WideThresholdBps`). Pseudocode that mirrors C#/PascalCase conventions diverges from this codebase's VB SCREAMING_SNAKE enums and snake-case JSON property names — easy to drift.

2. **Audit existing helpers before declaring new ones.** Kickoff §3 listed `AddBadgeRow` / `AddMiniMeter` / `AddSubLabel` / `AddSectionHeader` as if they existed. Three of the four didn't. Quick `grep` of the target file before drafting the bind code would catch this.

3. **Bind specs should state "build, screenshot, measure" gates per card.** Two card heights clipped on first run (KELLY, INDICATOR DETAILS). Both fixed in single-line follow-up commits, but cost a verification cycle each.

4. **Engine emission semantics audit.** Add a "before consuming `SignalBreakdownItem.LongHit/ShortHit`, tabulate all permutations the engine emits" rule. The Pass 2c CONFLICT-both-False shape is non-obvious and the kickoff didn't cover it.

5. **NICE-severity gaps need an explicit ship/skip decision per commit.** OFI Bid/Ask Vol (GAP-72/73) fell into a gap between commits 3 and 4 — the kickoff text mentioned them in commit 4's exclusion list but commit 3's spec didn't include them. Result: dropped from the UI. A "ship list" + "skip list" per commit removes that ambiguity.

6. **Document the `SectionGroup` title-colour limitation** in the proposal §6 controls reference. The fallback (inline composition) works fine but next consumer will hit the same wall.

---

## 9. What was NOT done (kickoff scope boundary)

Confirming the kickoff's "this is not" list was honoured:

- ✅ No P4e (SETTINGS & TOOLS section restructure) — row 11 placeholder stays empty.
- ✅ No P4f (ANALYSIS SKIPPED degraded render) — no `RenderSkippedDashboard` code touched.
- ✅ No P5 (`txtOutput` deletion) — legacy verification dump still parked at row 10.
- ✅ No new Theme tokens. `UI/Theme/Theme.vb` untouched (verified via `git diff`).
- ✅ No `UI/Controls/` modifications.
- ✅ No `settings.json` changes.
- ✅ No `MainForm.Designer.vb` edits.
- ✅ No scoring / indicator logic changes. Engine source only read (read-only) to verify Pass 2c emission semantics.
- ✅ No legacy render-file edits (`MainForm_Render_Header.vb`, `MainForm_Render_Sections.vb` untouched).
