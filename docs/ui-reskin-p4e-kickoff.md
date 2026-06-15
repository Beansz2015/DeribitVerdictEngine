# UI Reskin P4e — Implementation Kickoff

**Phase:** P4e — SETTINGS & TOOLS section restructure
**Spec source:** `docs/ui-reskin-proposal.md` §4.9 + §5 items #1, #2, #27
**Gap source:** `docs/ui-reskin-p4-gap-checklist.md` GAP-72, GAP-73, GAP-74 (commit 0 bundle)
**Predecessor spec-back:** `docs/ui-reskin-p4d-spec-back.md` — §8 lessons folded into this kickoff
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-23
**Recommended model:** **Opus 4.7 Medium.** Structural but well-bounded — no synthesis beyond the design spec.

---

## 0. What this phase is (and isn't)

**Is:** the long-overdue restructure of the bottom status bar into the SETTINGS & TOOLS section the proposal has been pointing at since rev 2. Plus two small spec-back follow-ups bundled as commit 0.

**Isn't:**
- ❌ ANALYSIS SKIPPED degraded render (P4f).
- ❌ `txtOutput` deletion (P5).
- ❌ Any change to scoring, indicators, settings.json, or CSV.
- ❌ Any change to `UI/Controls/` (the P3 library is locked).
- ❌ Any change to `MainForm.Designer.vb`.
- ❌ Any change to legacy render files (`MainForm_Render_Header.vb`, `MainForm_Render_Sections.vb`) — they stay alive until P5. Click handlers inside them remain wired to the moved controls; you are only changing the *visual housing*, not the handlers.

---

## 1. What you inherit

### 1.1 Existing reparenting stub

`UI/MainForm_Layout.vb:641` has a placeholder `ReparentSettingsToolsControls()` from P4a. It currently dumps all eight controls into a single `FlowLayoutPanel` inside `_cardSettingsTools`. **Your P4e work replaces this method's body with the grouped layout.** Don't add a parallel method — overwrite this one. The call site (search for `ReparentSettingsToolsControls`) doesn't need changing.

### 1.2 Status-bar controls you'll reparent

From `MainForm_Layout.vb:51-61` and `:195-198`:

| Control | Type | Declared where | Click handler location |
|---|---|---|---|
| `lblLogInfo` | Designer `Label` | `MainForm.Designer.vb` | (text-only, no handler) |
| `lblCountdown` | Designer `Label` | `MainForm.Designer.vb` | (text-only, no handler) |
| `lnkResetLog` | Designer `LinkLabel` | `MainForm.Designer.vb` | `MainForm_Render_Header.vb:21` |
| `lnkCalibCheck` | Designer `LinkLabel` | `MainForm.Designer.vb` | `MainForm_Render_Header.vb:35` |
| `lnkAnalysisReport` | Programmatic `LinkLabel` (P4a) | `MainForm_Layout.vb:58, 195` | `MainForm_Render_Header.vb:40` (Async) |
| `lnkTweakSettings` | Programmatic `LinkLabel` (P4a) | `MainForm_Layout.vb:59, 196` | `MainForm_Layout.vb:847` |
| `lnkOutputDump` | Programmatic `LinkLabel` (P4a) | `MainForm_Layout.vb:60, 197` | `MainForm_Layout.vb:868` |
| `lnkOutputDumpSettings` | Programmatic `LinkLabel` (P4a) | `MainForm_Layout.vb:61, 198` | `MainForm_Layout.vb:886` |
| `rbSingle`, `rbRepeat` | Designer `RadioButton` | `MainForm.Designer.vb` | Wired in `MainForm_AutoRun.vb` |
| `nudMinutes`, `nudSeconds` | Designer `NumericUpDown` | `MainForm.Designer.vb` | Wired in `MainForm_AutoRun.vb` |

**Critical:** `lnkResetLog` and `lnkCalibCheck` use `Handles ...LinkClicked` declarative wiring (file-internal partial-class dispatch) — moving them to a new parent **does not** unbind the handlers. The same is true for the programmatic `WithEvents`-declared links. Reparenting is safe.

**Note on radios + NUDs:** `rbSingle/rbRepeat` and `nudMinutes/nudSeconds` are part of the HEADER row (already laid out by P4a's `LayoutAutoRunCluster` at `MainForm_Layout.vb:540-595`), **not** the status bar. They stay in the header. The AUTO-RUN sub-box in SETTINGS & TOOLS shows `lblCountdown` + a derived REPEAT/SINGLE chip; the radios themselves remain in the header row. Read-source-of-truth pattern (proposal §4.9 explicitly: "Reuse existing radios as the source of truth — don't replace them").

### 1.3 P3 controls available

From `UI/Controls/` (all locked — do not modify):

| Control | Constructor / key properties | Notes |
|---|---|---|
| `SectionGroup` | `.Title`, `.AccentColor`, `.BorderStyle2` ∈ `{Solid, Dashed}` | **No `TitleColour` property** (per P4d spec-back §2.3). Title is hardcoded to `Theme.FG_QUATERNARY`. Fine for LOG/AUTO-RUN/TOOLS where dim grey titles are the design intent. |
| `AnalysisReportButton` | Inherits `FlatButton`. Pre-configured Solid + amber CTA + 📊 icon + `→` arrow + glow halo. Default `Height = 44`. | Wire click via `AddHandler .Click, AddressOf <existing handler>` since `FlatButton` raises `Click`, but `lnkAnalysisReport_LinkClicked` is signed as `LinkLabelLinkClickedEventArgs`. **You'll need a thin shim** — see §3.2 below. |
| `LinkRow` | `.LinkText`, `.TrailingIcon`, event `LinkClicked As EventHandler` | Used for the three TOOLS rows. Trailing icon for Output Dump's ⚙. |
| `Pill` | `.BgColor`, `.FgColor`, `.BorderColor`, `.Text`, `.CornerRadius` (default 8) | Used for REPEAT/SINGLE chip. |

### 1.4 Helpers in `MainForm_Render_Cards.vb`

Grepped at `Private (Function|Sub) [A-Z]\w+`:

```
51   Private Sub InitBoundCardContents()
63   Private Sub InitScoreCard()
110  Private Sub InitVerdictCard()
221  Private Sub InitLastPriceCard()
281  Private Sub InitAtrLevelsCard()
322  Private Function BuildAtrZoneRow(...)
386  Private Sub InitStructuralCard(...)
627  Private Function BindAtrRow(...)
```

Public `BindCardXxx` methods exist for: Score, Verdict, LastPrice, AtrLevels, Structural, SignalBreakdown, OiCvdCross, VolumeProfile, Kelly, IndicatorDetails. **No `BindCardSettingsTools` yet** — this phase creates it. By convention, place the new `BindCardSettingsTools()` method in `MainForm_Render_Cards.vb` alongside the others. State that's display-derived from `_skipCount`, `rbRepeat.Checked`, etc. lives in `MainForm_Layout.vb` shared fields.

Existing static formatters you'll reuse for commit 0:
- `FormatUsdShort(usd As Double) As String` at `MainForm_Render_Cards.vb:2087` — produces `$1.6M` / `$840K` / `$120`. Use this for OFI Bid/Ask Vol compact rendering.

### 1.5 Existing log-line surface

`MainForm_Render_Header.vb:14` — `UpdateLogInfo()`:

```vb
Private Sub UpdateLogInfo()
    Dim rows As Integer = AnalysisLogger.GetRowCount()
    Dim path As String  = AnalysisLogger.GetLogPath()
    Dim skipSuffix As String = If(_skipCount > 0, String.Format("  |  Skipped: {0}", _skipCount), "")
    lblLogInfo.Text = String.Format("Log: {0} rows{1}  |  {2}", rows, skipSuffix, path)
End Sub
```

**Already surfaces `_skipCount`** — but with the legacy format. P4e changes the formatting to match the design's `· skipped {N}` style and drops the path (path moves to tooltip per proposal §4.9 + handover §2). Don't move this method out of `MainForm_Render_Header.vb` — P5 deletes that file and will relocate it as part of the cleanup. Editing in place is fine.

---

## 2. Commit plan

Three commits. Each compiles, runs through ≥3 auto-run cycles, and verifies the affected card by screenshot before moving on.

| # | Subject | Scope | LOC est. |
|---|---|---|---|
| 0 | `fix(ui-reskin): P4e commit 0 — GAP-72/73 OFI vol + GAP-74 spread status` | Append bid/ask volumes to SIGNAL BREAKDOWN OFI row note. Append status text to OI × CVD CROSS Spread MiniMeter value. | ~10 |
| 1 | `feat(ui-reskin): P4e — SETTINGS & TOOLS grouped layout` | Replace `ReparentSettingsToolsControls()` body with grouped LOG / AUTO-RUN / ANALYSIS REPORT CTA / TOOLS layout using `SectionGroup`, `LinkRow`, `AnalysisReportButton`, `Pill`. | ~400-500 |
| 2 | `feat(ui-reskin): P4e — skipped-count surfacing + REPEAT chip` | Reformat `UpdateLogInfo` to `Log: N rows · skipped M` (conditional). Drop path from text → tooltip. Wire REPEAT/SINGLE `Pill` to mirror `rbRepeat.Checked` (and update on `CheckedChanged`). | ~80-120 |

Title-bar marker stays `[P4]` — bump to `[P4e]` is optional; if you bump, mention in commit 2's message.

### 2.1 Per-commit ship / skip lists

**Commit 0:**
- ✅ GAP-72 + GAP-73 (OFI Bid/Ask Vol → SIGNAL BREAKDOWN OFI row note)
- ✅ GAP-74 (Spread status → OI × CVD CROSS Spread MiniMeter value)
- ⏸ Nothing else.

**Commit 1:**
- ✅ Proposal §4.9 layout (LOG / AUTO-RUN / ANALYSIS REPORT CTA / TOOLS sub-boxes)
- ✅ AUTO-RUN sub-box gets `BorderStyle2 = Dashed` with `AccentColor = Theme.BORDER_DASHED_INFO` (cyan)
- ✅ All 6 reparented controls land in their grouped sub-boxes
- ✅ `AnalysisReportButton` replaces `lnkAnalysisReport` visually; old `LinkLabel` is hidden (`.Visible = False`) and its click handler stays bound — see §3.2 for the wiring shim
- ✅ TOOLS group uses three `LinkRow` rows: "Calibration Readiness", "Tweak Settings", "Output Dump" (⚙ trailing icon)
- ⏸ Skipped-count format change (deferred to commit 2)
- ⏸ REPEAT/SINGLE chip (deferred to commit 2)
- ⏸ Path → tooltip move (deferred to commit 2; commit 1 keeps the legacy `UpdateLogInfo` output untouched, just lands it inside the new LOG box visually)

**Commit 2:**
- ✅ `UpdateLogInfo` format → `Log: {N} rows[ · skipped {M}]`
- ✅ Full path moves to `_logInfoTooltip.SetToolTip(lblLogInfo, path)` (new ToolTip instance on `MainForm`)
- ✅ REPEAT/SINGLE chip — `Pill` next to `lblCountdown` inside AUTO-RUN box; mirrors `rbRepeat.Checked` (REPEAT) vs `rbSingle.Checked` (SINGLE) via a `CheckedChanged` handler on both radios that calls a new `UpdateAutoRunChip()` helper
- ⏸ Nothing else.

---

## 3. Spec details — read these once, ignore the pseudocode trap

### 3.1 Layout shape (commit 1)

Three rows inside `_cardSettingsTools`:

```
Row 1 (TableLayoutPanel 2 cols, equal width):
  ┌─ LOG ─────────────────────┐  ┌─ AUTO-RUN (dashed cyan) ────┐
  │ lblLogInfo                │  │ lblCountdown                │
  │ lnkResetLog (red hover)   │  │ [REPEAT|SINGLE] Pill        │
  └───────────────────────────┘  └─────────────────────────────┘

Row 2 (full-width):
  ┌─ AnalysisReportButton ──────────────────────────────────┐
  │ 📊  ANALYSIS REPORT                                  →  │
  └─────────────────────────────────────────────────────────┘

Row 3 (full-width):
  ┌─ TOOLS ────────────────────────────────────────────────┐
  │ › Calibration Readiness                                │
  │ › Tweak Settings                                       │
  │ › Output Dump                                       ⚙  │
  └────────────────────────────────────────────────────────┘
```

Pixel sizing (these are starting estimates — verify per §4):

| Region | Height | Notes |
|---|---|---|
| LOG / AUTO-RUN row | 90 px | SectionGroup has 22 px title padding + ~60 px content + 8 px bottom |
| ANALYSIS REPORT CTA row | 56 px (= `AnalysisReportButton.Height` 44 + 12 px margin) | |
| TOOLS row | ~110 px (22 + 3 × 26 + 8) | 26 px is `LinkRow` default 22 + 4 margin |
| **Card total** | ~260 px + 16 px outer padding ≈ **280 px** | Update `_cardSettingsTools` row height in `_gridRoot` accordingly. See §4. |

The row currently allocated to `_cardSettingsTools` in `_gridRoot` is sized for the placeholder. Bump to ~280 px in commit 1. If clipped, raise to 320 — spec-back §8 lesson 3 calls this out.

### 3.2 AnalysisReportButton wiring (commit 1)

`AnalysisReportButton` extends `FlatButton` which raises `Click As EventHandler`. The existing handler is signed for `LinkLabelLinkClickedEventArgs`. **Don't change the existing handler.** Add a shim:

```vb
AddHandler _btnAnalysisReport.Click,
    Sub(s, e) lnkAnalysisReport_LinkClicked(s, Nothing)
```

The handler at `MainForm_Render_Header.vb:40` doesn't reference its `e` argument other than as a method-sig param, so passing `Nothing` is safe. Leave the old `lnkAnalysisReport` LinkLabel in place but `.Visible = False` — its `Handles` clause is still satisfied; the shim wires the visual surface. P5 deletes the old LinkLabel.

### 3.3 REPEAT/SINGLE chip (commit 2)

```
Pill colours when REPEAT active:  BgColor = Theme.BG_CARD_RAISED, FgColor = Theme.ACC_INFO,    BorderColor = Theme.ACC_INFO     · Text "▶ REPEAT"
Pill colours when SINGLE active:  BgColor = Theme.BG_CARD_RAISED, FgColor = Theme.FG_SECONDARY, BorderColor = Theme.BORDER_CARD · Text "▶ SINGLE"
```

Hook `AddHandler rbRepeat.CheckedChanged, AddressOf UpdateAutoRunChip` (and same for `rbSingle`). Initial call in `BindCardSettingsTools` after creating the chip.

### 3.4 LOG-line reformat (commit 2)

```vb
Private Sub UpdateLogInfo()
    Dim rows As Integer = AnalysisLogger.GetRowCount()
    Dim path As String  = AnalysisLogger.GetLogPath()
    Dim skipSuffix As String = If(_skipCount > 0, String.Format(" · skipped {0}", _skipCount), "")
    lblLogInfo.Text = String.Format("Log: {0} rows{1}", rows, skipSuffix)
    _logInfoTooltip.SetToolTip(lblLogInfo, path)
End Sub
```

Declare `_logInfoTooltip As ToolTip` once in `MainForm_Layout.vb` shared fields and instantiate it in the constructor with `InitialDelay = 400, AutoPopDelay = 8000, ReshowDelay = 200, IsBalloon = False`.

### 3.5 Commit 0 — GAP-72/73 OFI Bid/Ask Vol

Edit `MainForm_Render_Cards.vb:2317` — `BuildRowOfi`:

```vb
' BEFORE
Dim note As String = $"ratio {r.OFIRatio:F2}"

' AFTER
Dim note As String = $"ratio {r.OFIRatio:F2} · bid {FormatUsdShort(r.OFIBidVol)} ask {FormatUsdShort(r.OFIAskVol)}"
```

Field names verified: `Core/IndicatorResults.vb:50,53,54` — `OFIRatio`, `OFIBidVol`, `OFIAskVol` (all `Double`, weighted top-3 vol per the comment).

**Watch:** `FormatUsdShort` is `Private Shared`, and `BuildRowOfi` is also `Private Shared` in the same file — direct call works. Don't promote either.

### 3.6 Commit 0 — GAP-74 Spread status in MiniMeter

Edit `MainForm_Render_Cards.vb:1253` — `BindCardOiCvdCross`:

```vb
' BEFORE
stack.Controls.Add(BuildMiniMeter("Spread", $"{r.SpreadBps:F2} bps",
                                  spreadPct,
                                  ResolveSpreadColour(If(r.SpreadStatus, ""))))

' AFTER
Dim spreadStatus As String = If(r.SpreadStatus, "").ToUpperInvariant()
Dim spreadValue As String = If(String.IsNullOrEmpty(spreadStatus),
                               $"{r.SpreadBps:F2} bps",
                               $"{r.SpreadBps:F2} bps · {spreadStatus}")
stack.Controls.Add(BuildMiniMeter("Spread", spreadValue,
                                  spreadPct,
                                  ResolveSpreadColour(spreadStatus)))
```

Field verified: `Core/IndicatorResults.vb:56` — `SpreadStatus As String` with values `"TIGHT" | "NORMAL" | "WIDE"`.

---

## 4. Verification gate (spec-back §8 lesson 3)

After **each** commit:

1. `dotnet build` — clean.
2. Run app. Trigger one analysis (manual `Analyze Now`).
3. Screenshot the affected card region. Compare against the proposal §4.9 wireframe (commit 1) or the legacy `txtOutput` dump (commit 0).
4. Measure clipping. Card row heights in `_gridRoot` are **absolute** — they silently clip lower content if under-sized. If you see clipping, bump the row height by 40 px and re-verify before committing.
5. For commit 1: tab-order sweep. Press Tab from the form load state; confirm focus traverses POSITION radios → header NUDs → ANALYZE → reparented controls in a sensible order. Set `TabStop = False` on `SectionGroup` panels and the `_cardSettingsTools` host so focus skips decorative chrome.

Live-data parity check (after all three commits):

- Run app for 5 auto-cycles. Confirm:
  - `lblLogInfo` shows row count, increments live, surfaces `· skipped N` only when `_skipCount > 0` (force a skip by temporarily breaking one `GetXxxAsync` call to test — revert before commit).
  - REPEAT/SINGLE chip flips when the user clicks the header radios.
  - All four TOOLS / ANALYSIS REPORT actions still open their respective dialogs / dumps.
  - OFI row in SIGNAL BREAKDOWN shows `ratio 1.21 · bid $1.6M ask $1.4M` style.
  - Spread MiniMeter shows `0.07 bps · TIGHT` style.

---

## 5. Out of scope — explicit skip list

If any of these tempt you, stop and defer to P4f or P5:

- ❌ `_lastSuccessfulVerdict` / `_lastSuccessfulIndicators` / `_lastSkipReason` plumbing (P4f).
- ❌ `RenderSkippedDashboard` (P4f).
- ❌ Per-card opacity overlay for stale state (P4f).
- ❌ "last successful at HH:mm:ss" timestamp line in LOG box (P4f).
- ❌ Touching `txtOutput`, `_cardVerificationDump`, or any RTF helper (P5).
- ❌ Modifying `SectionGroup` to add a `TitleColour` property (locked per P4d spec-back §2.3 recommendation 2 — inline composition is the pattern for coloured titles, not P4e's concern).
- ❌ Renaming or relocating any click handler (P5 does the file-deletion cleanup that warrants relocation).
- ❌ Settings.json changes — none.

---

## 6. If you get stuck

If `ReparentSettingsToolsControls` becomes a fight (e.g., a control resists being moved out of its existing parent), check:

1. Designer-declared controls are owned by `Me.Controls` directly. `Me.Controls.Remove(c)` then `flow.Controls.Add(c)` is the canonical reparent. Already proven by P4a's stub at line 658-670.
2. `Anchor` / `Dock` settings on Designer controls may pull them back to the form on next layout. Set `c.Anchor = AnchorStyles.None; c.Dock = DockStyle.None` after reparenting, before adding to the new sub-box.
3. `lnkOutputDumpSettings` is the ⚙ glyph LinkLabel. In TOOLS, render it as the `TrailingIcon` of the `LinkRow` for "Output Dump" (one row, not two). Wire `_outputDumpRow.LinkClicked → lnkOutputDump_LinkClicked` and add a separate `Click` handler on a small label inside the row for the ⚙. Or: keep two `LinkRow`s — one with ⚙ trailing, one without. Either acceptable; first is closer to design.

If `AnalysisReportButton.Click` doesn't fire the existing async handler, double-check the shim's `AddHandler` syntax (lambda capture is fine in VB.NET; `AddressOf` over a non-async sig also works since the async sub still returns `Sub`).

If commit 1 produces a layout that visually clashes with the existing perf strip or VERDICT row, **resize the row first** rather than tweaking margins inside the card — the card has plenty of internal room; the issue is almost always the absolute row height in `_gridRoot`.

---

## 7. Reporting back

Spec-back doc goes to `docs/ui-reskin-p4e-spec-back.md`. Follow the P4d structure (Executive summary → Spec↔reality mismatches → Decisions surfaced → Gaps deferred → Bugs surfaced → Polish fixes → Commit ledger → Suggestions for spec author → Not-done boundary).

Three things specifically worth reporting if they happen:
1. Card row height ended up materially different from the 280 px estimate (so the spec doc gets corrected).
2. The `AnalysisReportButton` click shim needed a different shape than §3.2 (so future kickoffs use the right pattern).
3. Any P3 control behaved differently from its constructor / property surface as I described it (so the spec author's mental model of the locked P3 library stays accurate).

---

## 8. Workflow reminders

- Local commits only. **Do not push.** User runs the app and decides when to push (per `crypto-trading-context` workflow rule).
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- One commit per scope section above — don't bundle 0+1 or 1+2.
- Settings.json untouched. Engine code untouched. If you need to grep engine source to confirm a field shape (e.g., `OFIBidVol` units), that's read-only and fine.

---

**End of kickoff.** Drop this verbatim into a fresh Opus 4.7 Medium conversation as the opening message; the conversation has everything it needs to ship P4e through three commits + spec-back.
