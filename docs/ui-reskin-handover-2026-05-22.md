# UI Reskin — Spec Author Handover

**Generated:** 2026-05-22
**For:** Next spec-author conversation taking over after P4d ships
**Purpose:** Capture everything needed to continue as spec-author through P4e → P4f → P5 without losing context

This is a handover between **spec-author conversations**, not implementation conversations. The implementation conversations are short-lived, one per kickoff. The spec-author role has been continuous up to this point — this doc transfers it to the next conversation cleanly.

---

## 1. State of the project (one paragraph)

The DeribitVerdictEngine UI reskin is **roughly 80% complete**. P1 (theme infrastructure), P2 (palette repaint), P3 (custom controls library), P4a (layout skeleton), P4b (top-half binding + retro-fixes), P4c (SIGNAL BREAKDOWN grid + state-from-hits fix), and P4d (KELLY expansion, VOLUME PROFILE extensions, OI×CVD CROSS card, INDICATOR DETAILS card) have all shipped. Legacy `txtOutput` is parked in a verification dump card at row 10 for side-by-side parity comparison. Three phases remain: **P4e** (SETTINGS & TOOLS section restructure — status bar reparenting into grouped sub-boxes), **P4f** (ANALYSIS SKIPPED degraded render state), and **P5** (delete `txtOutput`, build `BuildPlaintextSnapshot`, final cleanup). The engine itself is at settings.json v30 with the auto-tweaker awaiting first fixed-window fire — none of the reskin work has touched scoring/indicator code.

---

## 2. Session start protocol for the new conversation

Read in order:

1. **`CLAUDE.md`** (project root) — collaboration rules, shell tips, workflow conventions.
2. **`docs/DeribitIndicatorProject.md`** — current engine state. The "Recent shipments" section is current through P4d.
3. **`docs/architecture.md`** — codebase structure. Display Behaviour Clarifications appendix has notes on render-gated rows.
4. **`docs/ui-reskin-proposal.md` rev 2.1** — the authoritative design spec. §3 (palette + width), §4 (card inventory), §5 (gap coverage), §6 (custom controls), §8 (phase plan), §10 R/Q (decisions log).
5. **`docs/ui-reskin-p4-gap-checklist.md`** — atomic gap list. 86 IDs, severity tags, source line refs. Tracks what's shipped.
6. **`docs/ui-reskin-p4d-spec-back.md`** — most recent spec-back report from the implementation conversation. §8 has five process lessons to apply going forward.
7. **This handover doc.**
8. Load the **`crypto-trading-context`** skill.

**Read on-demand only:**
- Previous kickoff docs (`ui-reskin-p1-kickoff.md` through `ui-reskin-p4d-kickoff.md`) — reference for structure when drafting new kickoffs.
- `UI/MainForm_Render_Cards.vb` / `UI/MainForm_Layout.vb` — when writing P4e/P4f kickoffs, grep these for the existing helpers per spec-back §8 lesson 2.
- `UI/MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb` — for P5 `BuildPlaintextSnapshot` work (the legacy render code is the source of truth for what the output dump expects).

**Don't read at session start:**
- Any other `.vb` source file. Open on a specific need.
- Wireframe HTML bundle (the proposal already extracted what matters).

---

## 3. Roadmap — what's left

### P4e — SETTINGS & TOOLS section restructure

**Source spec:** `ui-reskin-proposal.md` §4.9.

**Scope:** reparent existing status-bar controls from their current flat-row layout into a grouped section with four sub-boxes. Per the design:

```
┌─ SETTINGS & TOOLS ──────────────────────────────────────────┐
│ ┌─ LOG ────────────┐  ┌─ AUTO-RUN (dashed cyan) ────────┐  │
│ │ Log: 2580 rows   │  │ Next run in: 00:42              │  │
│ │ · skipped 2      │  │ ▶ REPEAT                        │  │
│ │ ↺ Reset Log      │  │                                 │  │
│ └──────────────────┘  └─────────────────────────────────┘  │
│ ┌─────────────────────────────────────────────────────┐    │
│ │ 📊 ANALYSIS REPORT                              →   │    │  ← prominent amber CTA
│ └─────────────────────────────────────────────────────┘    │
│ ┌─ TOOLS ────────────────────────────────────────────┐    │
│ │ › Calibration Readiness                            │    │
│ │ › Tweak Settings                                   │    │
│ │ › Output Dump                                  ⚙   │    │
│ └────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

**Existing controls to reparent:**
- `lblLogInfo` → LOG group (and split: row count goes one row, `· skipped {N}` appended conditionally, full path moves to a tooltip OR drops since it's not in the design)
- `lblCountdown` → AUTO-RUN group (numeric "Next run in:" pattern)
- `lnkResetLog` → LOG group (red-bordered pill)
- `lnkAnalysisReport` → promoted to **AnalysisReportButton** (the existing P3 control)
- `lnkCalibCheck`, `lnkTweakSettings`, `lnkOutputDump`, `lnkOutputDumpSettings` → TOOLS group via `LinkRow` controls (existing P3)

**REPEAT/SINGLE chip:** derived from `rbRepeat.Checked` / `rbSingle.Checked`. Static label that flips when the user changes mode. Reuse existing radios as the source of truth — don't replace them.

**Controls used from P3:** `SectionGroup` (for LOG / AUTO-RUN / TOOLS sub-boxes), `LinkRow` (for each tool link with chevron), `AnalysisReportButton` (the prominent amber CTA — already built), `Pill` (for REPEAT chip).

**Pre-existing decisions for P4e:**
- Skipped count surfacing: only when `_skipCount > 0` (Q5 from rev 2.1 §10).
- AUTO-RUN box gets the cyan dashed border to visually distinguish "active timer" from passive groups.
- `lblLogInfo` currently shows path; the design drops it. Drop the path in P4e (becomes a tooltip on the LOG group instead — minor).

**Estimated scope:** ~400-600 LOC, 2-3 commits. Medium effort. Reuses P3 controls heavily.

**Notable risk:** the existing status-bar links have direct click handlers (`lnkResetLog_LinkClicked`, etc. in `MainForm_Render_Header.vb`). After reparenting, handlers stay; only the visual housing changes. The handlers' file location is `MainForm_Render_Header.vb` which P5 deletes — so during P4e, the handlers remain in place. P5 moves them to `MainForm_Layout.vb` or similar before deleting Render_Header.

### P4f — ANALYSIS SKIPPED degraded render

**Source spec:** `ui-reskin-proposal.md` §5.1.

**Scope:** new render path when `Task.WhenAll` in `RunAnalysisAsync` produces `Nothing` for any required dependency (1m, 5m, funding, book summary, order book, recent trades — 15m alone doesn't skip per existing resilience rules).

**State plumbing:**
- New shared fields in `MainForm_Layout.vb`:
  - `_lastSuccessfulVerdict As VerdictResult`
  - `_lastSuccessfulIndicators As IndicatorResults`
  - `_lastSuccessfulRenderTime As DateTime`
  - `_lastSkipReason As String`
- Captured at end of successful `RunAnalysisAsync` (after all bind methods complete).
- Set at skip site in `RunAnalysisAsync` before the early return.

**Render path:**
- New `RenderSkippedDashboard(reason)` method in `MainForm_Render_Cards.vb`.
- VERDICT card: verdict text → `ANALYSIS SKIPPED` in `Theme.ACC_AMBER_DEEP` (28pt bold with glow). Section accent → `ACC_WARN`. Two sub-lines:
  - Reason (colour `#FBBF24AA`): `"Deribit REST fetch failed — {which call} returned {error}"`
  - Hint (colour `FG_TERTIARY`): `"Engine retains last-known indicator values. Skipping verdict generation until next successful fetch (auto-run continues)."`
- All other cards rendered at opacity 0.4 (use `Panel.BackColor` tinting or a Graphics overlay — pick whichever works without modifying P3 controls). Section headers add small amber `(stale)` tag + age label (`2 min stale`).
- SETTINGS & TOOLS LOG group: `· skipped {N}` + new line `last 2026-05-23 14:21:09` showing last successful render timestamp.

**Method called:** instead of `RenderDashboard(v, r, norms, cfg)` when the skip branch is taken in `RunAnalysisAsync`.

**Estimated scope:** ~300-400 LOC, 1-2 commits. Medium effort. Mostly state plumbing + an opacity-overlay pattern.

**Notable risk:** the opacity-0.4 overlay on the existing cards needs a clean implementation. Options:
1. Add a semi-transparent `Panel` overlay above the affected cards (cheap, no P3 control change).
2. Per-card `.Enabled = False` (greys controls but may look harsh).
3. Per-card colour adjustment (more work, more uniform look).

Recommend option 1 in the kickoff. Surface choice to the user.

### P5 — Final cleanup + `BuildPlaintextSnapshot`

**Source spec:** `ui-reskin-proposal.md` §8 P5 + §10 R1.

**Scope (high-risk phase):**

1. **Hide `txtOutput`** (`.Visible = False` in code; can't delete the field — Designer.vb owns the declaration).
2. **Remove the verification dump card** (`_cardVerificationDump`) from layout.
3. **Remove `lblVerdict` text writes** in any remaining code path (the field is currently `.Visible = False` but `RenderOutput` still writes to it).
4. **Delete `UI/MainForm_Render_Header.vb`** entirely.
5. **Delete `UI/MainForm_Render_Sections.vb`** entirely.
6. **Remove `RenderOutput()` call** from `RunAnalysisAsync`.
7. **Remove RTF helpers** (`AppendRtf`, `AR`, `SectionHeader`, `Divider`, `FormatRR`) — if `FormatRR` is needed by P4 binding code, move it to `MainForm_Render_Cards.vb` first.
8. **Remove P/Invoke surface** (`EM_SETMARGINS`, `EM_SETRECT`, `EM_SETRECTNP`, `SendMessage` overload, `RECT` struct, `SetOutputMargins`).
9. **Remove `OUTPUT_CHARS` / `OUTPUT_LINES` constants** and `SizeToContent` (already replaced by `ApplyInitialFormSize` in P4a).
10. **Build `BuildPlaintextSnapshot(v, r, norms, cfg) As String`** — produces the same markdown shape `AnalysisOutputDump.Append` currently gets from `txtOutput.Text`. Update the call site in `MainForm_Analysis.vb` to pass the snapshot text instead.

**Critical sub-task: `BuildPlaintextSnapshot`.** The output dump file (`analysis_output_dump.md`) is the trader's primary debugging surface. Format must stay byte-identical (or very close) so existing dump readers still parse it.

The snapshot must reproduce:
- The `===` divider sections (VERDICT block, ATR block, KELLY block dividers)
- Every section header (`DYNAMIC NORMS [LIVE]:`, `REGIME (5m): {regime}`, `CORE SIGNALS (1m):`, ...)
- Every indicator row with the legacy formatting
- The SIGNAL BREAKDOWN table with `Long | Short | Note` columns and TOTAL row
- The `PERF STRIP [B/T] | Cur.Wk: 47% | ...` line that v30 added

Walk `MainForm_Render_Sections.vb` end-to-end and replicate each `AppendRtf` call as plaintext via a `StringBuilder`. Same data flow, same format, no RTF colour codes.

**`lnkCalibCheck` clears `txtOutput` and renders the calibration report into it.** After P5 deletes `txtOutput`, the calibration report needs a different viewer. Options:
- Pop up a non-modal form similar to `AnalysisReportForm` (probably simplest)
- Show in a `MessageBox` (ugly for multi-line)
- Reuse `AnalysisReportForm` directly with the calibration text (cleanest)

Recommend reusing `AnalysisReportForm` — already a markdown-style viewer.

**Estimated scope:** ~400-500 LOC, 2-3 commits. **High effort** for the `BuildPlaintextSnapshot` work; rest is mechanical deletion.

**Notable risk:** existing scripts and consumers of `analysis_output_dump.md` may rely on the exact format. Verify by:
1. Saving a "pre-P5" dump file
2. Running P5 and saving a "post-P5" dump file
3. `diff` the two — only timestamps should differ

If diff is clean, P5 is safe to merge. If formatting drifts, fix `BuildPlaintextSnapshot` until it converges.

---

## 4. Locked architectural decisions

These have been settled across earlier conversations and **must not be re-litigated**:

| Decision | Locked in | Source |
|---|---|---|
| Single dark theme, no theme switching | rev 2 §2 | Q3 follow-up |
| Hard width ceiling 1280 px (= 3840 / 3) | rev 2.1 §3.6 | Q2 |
| Vertical unconstrained, scrolling acceptable | rev 2.1 §3.6 | implicit |
| Geist Mono bundled + fallback chain | P1 kickoff | locked |
| Palette tokens at design hex values | P2 kickoff | locked |
| 16 custom controls in `UI/Controls/`, no further modifications | P3 kickoff | hard rule |
| Card-based grid replaces `RichTextBox` rendering | P4a kickoff | locked |
| Legacy `txtOutput` stays parked until user explicitly green-lights P5 | rev 2 §10 R1 + user Q2 follow-up | locked |
| Hybrid A + selective B — DIAGNOSTICS card for verbose data, NOTE column for single-value enrichments | gap audit | locked |
| **Card name: INDICATOR DETAILS** (not "DYNAMIC NORMS" / "DIAGNOSTICS" / "REFERENCE") | gap audit | user choice |
| **No `[L]/[S]` columns** — STATE pill + SC encode direction | gap audit GAP-86 | user choice |
| **MicroCVD restored to 4-state colour distinction** (BULL_ACCEL / BULL_DECEL / BEAR_ACCEL / BEAR_DECEL / FLAT) | gap audit GAP-82 | user choice |
| STATE derivation for RSI(9), DMI/ADX, BBW/TTM uses `SignalBreakdownItem.LongHit/ShortHit` | P4c state-fix | locked |
| Score arc value = `max(EffectiveLongScore, EffectiveShortScore)` | P4c review §1 | locked |
| ATR card renders BOTH directions (Long + Short) | P4b retro-fix GAP-06 | user Q1 |
| KELLY card eff/penalty rows live in VERDICT card, not SCORE card | P4c review §4a | locked |
| ChipNumeric vs Designer NUDs: **keep Designer NUDs** for now (backlog) | P4c review §5 | accept |
| FlatButton vs Designer Buttons: **keep Designer Buttons** for now (backlog) | P4c review §5 | accept |
| `lblVerdict` hidden in P4, deleted in P5 | stabilisation | locked |
| Designer NumericUpDown spinner arrows visible — accepted | P4a review | accept |
| Hybrid A architecture name: "INDICATOR DETAILS" card with 12 inline groups | gap audit + P4d | locked |
| `SectionGroup` not modified — title-colour requires inline composition | P4d spec-back §2.3 | locked |
| `BuildGroupInline` helper local to `MainForm_Render_Cards.vb`, not promoted to P3 control | P4d spec-back §2.3 | locked |
| Pass 2c CONFLICT case = `LongHit=False AND ShortHit=False AND note prefix matches CONFLICT` | P4d spec-back §5.1 | locked (fixed) |
| Form width: **1100 px** (P4a settled at the floor of the rev 2.1 range) | P4a fix `3298ccf` | accept |
| REPEAT/SINGLE mode display: `Pill` chip mirroring `rbRepeat`/`rbSingle` radios (radios stay the source of truth). `SegmentedToggle` wrapper retired. | P4e + P3 maintenance pass | locked |

---

## 5. Spec-back §8 lessons — apply to all future kickoffs

From `docs/ui-reskin-p4d-spec-back.md` §8. The implementation conversation surfaced these after P4d shipped. Each saves 5-15 minutes per kickoff if applied:

1. **Quote real type names exactly.** Kickoff pseudocode in PascalCase / C#-style has drifted twice (`WidePenaltyThresholdBps` → real is `WideThresholdBps`; `OiCvdOutcome.ConfirmedLong` → real is `OiCvdOutcomeKind.CONFIRMED_LONG`). Codebase uses VB SCREAMING_SNAKE enums and snake_case JSON properties. **Before quoting any enum/property in a kickoff, grep the target file for the exact identifier.**

2. **Audit existing helpers before declaring new ones.** P4d kickoff §3 listed `AddBadgeRow` / `AddMiniMeter` / `AddSubLabel` / `AddSectionHeader` as if they existed; three of four didn't. **Each kickoff's "What you inherit" section should include a `grep "Private (Function|Sub)" MainForm_Render_Cards.vb` output enumerating existing helpers.**

3. **"Build, screenshot, measure" gate per card.** Two card heights clipped on first P4d run (KELLY at 180 px, INDICATOR DETAILS at 480 px). Both fixed in single-line follow-up commits. **Each card-binding step in a kickoff should state an explicit pixel verification: build, run, screenshot, measure clipping. Card row heights are absolute; under-sizing silently clips.**

4. **Engine-emission semantics audit before consuming `SignalBreakdownItem.LongHit/ShortHit`.** The Pass 2c CONFLICT shape (both False) was non-obvious; spec assumed Both True. **Before specifying any new consumer of hit booleans, tabulate all permutations the engine emits — read the emission site in `Core/ScoringEngine_Calculate_Scoring.vb`.**

5. **NICE-severity gaps need explicit per-commit ship/skip lists.** GAP-72/73 fell between P4d commits 3 and 4 (mentioned in 4's exclusion list, missing from 3's spec). Dropped from UI as a result. **Per-commit specs should enumerate every gap touched (ship) and every gap explicitly skipped, with the rationale.**

Bonus lesson from earlier rounds:

6. **Don't over-specify field-level pseudocode in the kickoff.** Once the agent has the per-indicator mapping table, the binding code writes itself. Heavy `Public Sub BindCardXxx` skeletons in the kickoff create more drift risk than guidance. The mapping table + the `What you inherit` section + the gap reference are usually enough.

---

## 6. Outstanding decisions (none critical)

Minor pending items the user hasn't formally locked. None block P4e — but if not addressed, they'll surface as agent clarifying questions:

| Item | Default if not addressed | Locked decision needed? |
|---|---|---|
| Spread status in OI×CVD CROSS card meter — append `· TIGHT` / `· NORMAL` / `· WIDE` to the value text | yes, append (trivial 5-line fix) | bundle into P4e commit 0 |
| OFI Bid/Ask Vol (GAP-72/73) — recommended location: append to SIGNAL BREAKDOWN OFI row note | yes, append `· bid 0.92M ask 1.56M` | bundle into P4e commit 0 |
| AUTO-RUN box minute/second NUDs — keep current Designer NUDs (P4c carry-forward) | yes, keep | already locked |
| Calibration Report viewer post-P5 — reuse `AnalysisReportForm` | yes | flag in P5 kickoff |
| LiveQuant scripts that read `analysis_output_dump.md` — pre/post P5 diff check | yes | flag in P5 kickoff |

---

## 7. Bundled "commit 0" for P4e — the small spec-back follow-ups

P4d spec-back §4 surfaced two small items that didn't ship in P4d but should land before P4e proper begins. Bundle as P4e's commit 0 (or first commit before the SETTINGS & TOOLS work).

### 7.1 GAP-72/73 — OFI Bid/Ask Vol in SIGNAL BREAKDOWN OFI row note

**Current state:** SIGNAL BREAKDOWN OFI row note shows `ratio 0.59` only. Bid/Ask volumes are dropped.

**Fix:** extend the note format string to append bid/ask in compact form.

```vb
' BEFORE (in BindCardSignalBreakdown for OFI row):
note = $"ratio {r.OFIRatio:F2}"

' AFTER:
note = $"ratio {r.OFIRatio:F2} · bid {FormatBigNum(r.OFIBidVol)} ask {FormatBigNum(r.OFIAskVol)}"
```

`FormatBigNum` produces compact M/K notation (already implemented as `FormatUsd` for Volume row — generalise or copy).

**Severity:** NICE. ~5 LOC.

### 7.2 GAP-74 — Spread status text in OI×CVD CROSS card MiniMeter

**Current state:** OI × CVD CROSS card Spread meter shows `0.07 bps` value with bar coloured by status. Status word missing.

**Fix:** append status to the value text.

```vb
' BEFORE (in BindCardOiCvdCross for Spread MiniMeter):
AddMiniMeter(_cardOiCvdCross, "Spread", $"{r.SpreadBps:F2} bps", spreadPct, spreadColour)

' AFTER:
AddMiniMeter(_cardOiCvdCross, "Spread", $"{r.SpreadBps:F2} bps · {r.SpreadStatus}", spreadPct, spreadColour)
```

**Severity:** MUST (per gap checklist), but already partially met by SIGNAL BREAKDOWN Spread row. ~3 LOC.

### 7.3 Commit message template

```
fix(ui-reskin): P4e commit 0 — GAP-72/73 OFI vol + GAP-74 spread status

Two small fixes deferred from P4d (per spec-back §4):

- GAP-72/73: append bid/ask volumes to SIGNAL BREAKDOWN OFI row note
  in compact M/K notation. Was previously shown only in legacy
  txtOutput dump.
- GAP-74: append status text (TIGHT/NORMAL/WIDE) to OI × CVD CROSS
  card Spread MiniMeter value. SIGNAL BREAKDOWN Spread row already
  shows status as colour pill; this surfaces it textually too.

Both NICE/MUST-already-partial severity per gap checklist. ~10 LOC
total. Foundation for the SETTINGS & TOOLS section restructure
(rest of P4e).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
```

---

## 8. Gap checklist status snapshot

From `docs/ui-reskin-p4-gap-checklist.md`. As of P4d ship:

**Shipped (78 of 86):**
- GAP-01..06: SCORE / LAST PRICE / ATR retro-fixes ✓ (P4b retro + P4c)
- GAP-07..16: KELLY card expansion ✓ (P4d commit 2)
- GAP-17..51: NORMS / REGIME / MTF / VWAP / BBW/TTM / EMA / Funding / OI / MicroCVD / Liq / Volume / Trend Structure detail ✓ (P4d commit 4 — INDICATOR DETAILS)
- GAP-52..68: SIGNAL BREAKDOWN NOTE enrichments ✓ (P4c)
- GAP-69..71, 75..82: ORDER FLOW / OPEN INTEREST / LIQ detail ✓ (P4c + P4d INDICATOR DETAILS)
- GAP-62..66: VOLUME PROFILE extensions ✓ (P4d commit 3)
- GAP-83..86: Signal breakdown TOTAL, MicroCVD 4-state, hit-marks dropped ✓ (P4c)

**Pending in P4e commit 0 (per §7 above):**
- GAP-72/73: OFI Bid/Ask Vol — append to SIGNAL BREAKDOWN OFI row note
- GAP-74: Spread status in OI × CVD CROSS card MiniMeter

**Not gaps but P4e scope:**
- SETTINGS & TOOLS section restructure (status bar reparenting)
- Skipped session count surfacing (`_skipCount > 0` → `· skipped {N}` in LOG group)

**Not gaps but P4f scope:**
- ANALYSIS SKIPPED render path
- Last-successful state capture
- Stale tags on cards during skipped state

**Not gaps but P5 scope:**
- Delete `txtOutput` + render files + RTF helpers + P/Invoke surface
- Build `BuildPlaintextSnapshot`
- Migrate `lnkCalibCheck` to a new viewer

---

## 9. File reference index

### Specs (read these for context)

- `docs/ui-reskin-proposal.md` — rev 2.1, the master design spec
- `docs/ui-reskin-p4-gap-checklist.md` — atomic gap list with severity + source line refs
- `docs/ui-reskin-p4d-spec-back.md` — most recent implementation feedback
- This handover doc

### Previous kickoffs (templates for structure)

- `docs/ui-reskin-p1-kickoff.md` — theme infrastructure
- `docs/ui-reskin-p2-kickoff.md` — palette repaint
- `docs/ui-reskin-p3-kickoff.md` — custom controls library
- `docs/ui-reskin-p4-kickoff.md` — P4a + P4b foundation
- `docs/ui-reskin-p4-stabilisation-kickoff.md` — crash fix + perf strip
- `docs/ui-reskin-p4-perf-strip-active-highlight-kickoff.md` — active session highlight
- `docs/ui-reskin-p4c-kickoff.md` — SIGNAL BREAKDOWN binding + P4b retros
- `docs/ui-reskin-p4d-kickoff.md` — KELLY + VOLUME PROFILE + OI × CVD CROSS + INDICATOR DETAILS

### Implementation files (open on demand)

- `UI/MainForm_Layout.vb` — `BuildCardGridLayout`, all card containers, perf strip, position radios, auto-run controls. Owns `ApplyInitialFormSize`.
- `UI/MainForm_Render_Cards.vb` — all `BindCardXxx` methods, inline group builders (`BuildGroupInline`, `BuildPlainSectionHeader`, etc.), `MakeSignalRow`.
- `UI/MainForm_Analysis.vb` — `RunAnalysisAsync`, the data-flow entry point.
- `UI/MainForm_Render_Header.vb` — **deleted in P5.** Currently holds RTF helpers + `RenderOutputHeader` + `BuildCalibrationReport` + link click handlers.
- `UI/MainForm_Render_Sections.vb` — **deleted in P5.** Holds `RenderOutput` + all indicator section blocks. **Source of truth for `BuildPlaintextSnapshot` shape.**
- `UI/Theme/Theme.vb` — palette + `FontMono` factory. Do not modify.
- `UI/Controls/` — 16 custom controls + `Helpers/PaintHelpers.vb`. Do not modify.
- `Core/IndicatorResults.vb`, `Core/ScoringEngine_Types.vb` — read-only references for field shapes.

### Engine docs

- `CLAUDE.md` — project root, collaboration rules
- `docs/DeribitIndicatorProject.md` — engine handover
- `docs/architecture.md` — codebase structure + display behaviour notes
- `docs/history-archive.md` — pre-v27 settings rationale, full version history (rarely needed)

---

## 10. Workflow conventions

Carried forward from `crypto-trading-context` skill + project handover:

1. **Local commits only.** User pushes after testing. Never push from any conversation.
2. **No `--no-verify` on commits, no force-push, no destructive git ops.**
3. **Designer.vb is untouchable.** All overrides programmatic.
4. **Spec-first.** Novel features get a proposal doc before coding.
5. **Settings.json version is strictly monotonic.** No reskin work touches settings.json — engine config stays at v30.
6. **Fresh Opus 4.7 Medium conversation per phase kickoff.** Spec author (you) stays continuous across phases via handover docs like this one. Implementation conversations are one-shot per kickoff.
7. **High effort reserved for synthesis-heavy work.** P3 was High. P4a/b/c/d/e were Medium. P4f Medium. **P5 may need High** for the `BuildPlaintextSnapshot` work — surface the choice to the user when drafting the P5 kickoff.
8. **Push gate is user-side.** User tests the running app, then pushes.

---

## 11. Recent commit history (P4 phase)

All local-only on master. Most recent first:

```
9934e9c  fix(ui-reskin): P4d/c — Pass 2c CONFLICT footer + KELLY card clip
aa7e16f  fix(ui-reskin): P4d — INDICATOR DETAILS card height 480 → 760
b7ea350  feat(ui-reskin): P4d — INDICATOR DETAILS card (GAP-15..51 absolute detail)
d084d5d  feat(ui-reskin): P4d — VOLUME PROFILE + OI × CVD CROSS card bindings
399f71b  feat(ui-reskin): P4d — KELLY card expansion (GAP-07..16)
1385a3a  fix(ui-reskin): P4c — align SIGNAL BREAKDOWN columns to headers
<earlier> feat(ui-reskin): P4c — SIGNAL BREAKDOWN card binding
<earlier> fix(ui-reskin): P4c — RSI/DMI/BBW state derivation from breakdown hits
<earlier> P4b retro-fixes (Score / Last Price / ATR parity)
<earlier> P4 stabilisation (perf strip, crash fix, lblVerdict hidden)
<earlier> P4a + P4b card grid skeleton + top-half binding
<earlier> P3a + P3b custom controls library
<earlier> P2 visual repaint
<earlier> P1 theme infrastructure
```

The user has pushed nothing past P3 — all P4 work sits local pending end-to-end verification.

---

## 12. Quick-reference / when in doubt

- **User runs the app on auto-run all day.** Don't disrupt scoring. Reskin is UI-only.
- **BTC has been in a macro bear since Oct 2025.** Calibration data skews short. Don't read STRONG_SHORT failure rates as steady-state.
- **Width hard ceiling 1280 px.** Don't let any layout exceed this. Form currently at 1100 px (the floor of the rev 2.1 range).
- **Vertical scroll is acceptable.** The form is tall (~2000 px with all cards bound). User's monitor is 4K vertical, so scrolling works fine.
- **Trader-profile rejected patterns are absolute** (no fixed-% targets, no non-directional rewards, no double-counting, no flat regime penalties). None of the reskin work touches these but worth keeping in mind.
- **Conservative bias wins ties.** When a UX decision is between "show more / show less," favour show-less in cards the trader scans quickly, show-more in cards the trader references during calibration.
- **Don't push proactively.** Test gate is user-side. Every conversation needs to be told this; it's standard.
- **Don't propose new indicator work.** The bottleneck is data, not UI. The user runs the engine to gather data; the reskin's job is to display it cleanly.
- **Ask before doing.** Especially for anything touching scoring logic, settings.json structure, or CSV schema.

---

## 13. Suggested first action for the new conversation

After reading the cited files (§2), draft the **P4e kickoff** as the immediate next deliverable. The structure follows previous kickoff templates with these adjustments per spec-back §8:

1. "What you inherit" section grepping `MainForm_Render_Cards.vb` for existing helpers (`AddCardHeaderWithTags`, `AddCardKvRow`, `BuildBadgeRow`, `BuildMiniMeter`, `BuildSubLabel`, `BuildPlainSectionHeader`, `AddLevelRow`, `BuildGroupInline`, `MakeSignalRow`, etc.). Spec-back §8 lesson 2.
2. Quote enum/property names from `Core/Settings/EngineSettings.vb`, `Core/IndicatorResults.vb`, `Core/ScoringEngine_Types.vb` exactly — verify via grep before quoting. Lesson 1.
3. Per-card "build + screenshot + measure" verification gate. Lesson 3.
4. Per-commit ship-list + skip-list for any NICE-severity gaps touched. Lesson 5.
5. Engine-emission audit only if P4e consumes `SignalBreakdownItem` hits (it shouldn't — status-bar restructure is layout-only). Lesson 4.

Suggested P4e commit structure:
1. Commit 0: GAP-72/73 + GAP-74 polish (§7 above)
2. Commit 1: SETTINGS & TOOLS layout skeleton + reparenting (LOG / AUTO-RUN / TOOLS groups + ANALYSIS REPORT CTA)
3. Commit 2: Skipped-count surfacing in LOG group + REPEAT chip wiring + title-bar marker bump to [P4e] (or hold marker for P5)
4. Optional commit 3: any polish caught during commit 2 verification

Recommended model: **Opus 4.7 Medium**. P4e is structural but well-bounded; no synthesis needed beyond what the design spec already provides.

Fresh conversation. Paste the P4e kickoff as opening message.

---

**End of handover.** The next conversation has everything needed to draft P4e, then P4f, then P5 — through full implementation feedback cycles — without referring back to this conversation.
