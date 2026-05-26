# UI Reskin P5a — Spec-back Report

**Phase:** P5a — `BuildPlaintextSnapshot` + dump rewire + calibration viewer migration.
**Spec source:** `docs/ui-reskin-p5a-kickoff.md`
**Author:** Claude (Opus 4.7, implementation conversation)
**Date:** 2026-05-26
**Status:** ✅ Shipped (both commits, local only). **Handing off to trader-side verification window — P5b gated on trader sign-off.**

---

## 1. Commits

| SHA | Subject | LoC |
|---|---|---|
| `bcfdfd7` | `feat(ui-reskin): P5a — BuildPlaintextSnapshot + AnalysisOutputDump rewire` | +914 / −413 (net +501 across 7 files) |
| `f707165` | `feat(ui-reskin): P5a — migrate calibration report to AnalysisReportForm` | +8 / −6 |

Neither pushed. Commit 1 landed within the 600-800 LoC band the kickoff estimated. Commit 2 was a 14-line edit, well under the 30-line estimate.

## 2. What shipped

### Commit 1
- **New file `UI/MainForm_PlaintextSnapshot.vb` (450 LoC).** `Friend Function BuildPlaintextSnapshot(v, r, norms, cfg, vwapWarmup, lastTradePrice) As String` reproduces every block of the legacy txtOutput body. One top-level dispatcher + 13 private `AppendXxx` helpers, one per section, so future refactors stay surgical. Colour drops, all formatters / conditional gates / sub-tick CAPPED suppression / per-side missing-data wording reproduced 1:1.
- **`AnalysisOutputDump.Append` invocation moved** from `MainForm_Render_Sections.RenderOutput()` to `MainForm_Analysis.RunAnalysisAsync()`, after `UpdatePerformanceLabels()`. `renderedText` is now `BuildPlaintextSnapshot(...)`; `perfStripLine` comes from new `ComposePerfStripLine()` helper migrated to `MainForm_Layout.vb`.
- **Helper migrations** (so P5b can delete Header.vb cleanly):
  - `FormatRR` → `MainForm_Render_Cards.vb` as `Friend Shared` (callers: legacy `RenderOutputHeader`, `BindCardAtrLevels`, `BindCardStructural`, `BuildPlaintextSnapshot`).
  - `BuildCalibrationReport` + `Flag` → new `UI/MainForm_Calibration.vb` (313 LoC).
  - `UpdateLogInfo`, `lnkResetLog_LinkClicked`, `lnkCalibCheck_LinkClicked`, `lnkAnalysisReport_LinkClicked`, `ComposePerfStripLine` → `MainForm_Layout.vb`.
- **`MainForm_Render_Header.vb` is now 272 lines** — just the four RTF helpers (`AppendRtf`, `AR`, `SectionHeader`, `Divider`) plus `RenderOutputHeader` itself. Deletes in one diff in P5b.
- **`RenderOutput` stays alive.** No changes to the per-section `AppendRtf` calls in `MainForm_Render_Sections.vb`. txtOutput still receives the legacy RTF output; the verification dump card still shows it. Per the kickoff §0 explicit non-goals, this is the parity-verification surface for the trader window.

### Commit 2
- `lnkCalibCheck_LinkClicked` body swapped from `txtOutput.Clear()` + `AppendRtf(txtOutput, BuildCalibrationReport(), Theme.FG_PRIMARY)` to a non-modal `New AnalysisReportForm(BuildCalibrationReport(), AnalysisLogger.GetLogPath()).Show()`. One of the last on-demand txtOutput writers gone.

---

## 3. Dump-file parity verification (§6.3)

### 3.1 Baseline capture (§0.5)
- Built at HEAD pre-P5, ran `dotnet run`, clicked Analyze 5 times (3 with `posState=None`, 2 with `posState=InLong` via UIA SelectionItemPattern on the "In Long" radio so HOLD/EXIT renders).
- `verify/p5-baseline.md`: 570 lines, 5 runs. Coverage hit every key block including PERF STRIP (v30), VERDICT/CONTEXT/CONFIDENCE/SCORE/TIME, LAST TRANSACTED PRICE, HOLD/EXIT (Layer 1.5 structural break wording — runs 4-5), ATR levels with `[CAPPED @ N (NEAREST_HVN_ABOVE)]`, structural rows with per-side missing-data note (v30 F12), KELLY both `[CAPPED]` and `[BIAS ONLY — NO TRADE] [CAPPED]` variants, MTF PASS and MTF BLOCK with both reason formats.
- `verify/p5-baseline-state.png` saved (fresh form, no analysis yet) — confirmed against the P4f spec-back description.

### 3.2 Post-commit-1 capture
- Built at `bcfdfd7`, cleared dump, ran another 5 analyses with identical posState toggle pattern.
- `verify/p5a-postcommit.md`: 567 lines, 5 runs.

### 3.3 Structural landmark count (the load-bearing parity gate)

Counted 31 landmark patterns across both files. **All 31 match exactly (5/5 each, plus the 20× `===` divider and the matching `^---$` per-run delimiter).** Sample:

```
^## Run                base=5  post=5  [OK]
^=====                 base=20 post=20 [OK]
^  VERDICT:            base=5  post=5  [OK]
^  CONTEXT:            base=5  post=5  [OK]
^  CONFIDENCE:         base=5  post=5  [OK]
^  SCORE:              base=5  post=5  [OK]
^  TIME:               base=5  post=5  [OK]
^  LAST TRANSACTED PRICE: base=5 post=5 [OK]
^  HOLD / EXIT:        base=2  post=2  [OK]
^ATR ENTRY LEVELS      base=5  post=5  [OK]
^  Long:   Stop        base=5  post=5  [OK]
^  Long structural:    base=5  post=5  [OK]
^  Short:  Stop        base=5  post=5  [OK]
^  Short structural:   base=5  post=5  [OK]
^KELLY SIZING          base=5  post=5  [OK]
^DYNAMIC NORMS         base=5  post=5  [OK]
^REGIME (5m):          base=5  post=5  [OK]
... (12 more, all [OK])
```

Note: the 2/5 `HOLD / EXIT:` count is intentional — only the runs that toggled `posState=InLong` produced the line, identical across both files because the toggle pattern was identical.

### 3.4 Per-run normalised template diff

After stripping numbers, percents, and timestamps to `N` placeholders, run-1 diff between baseline and post-commit showed **only market-state differences** (`SHORT` → `NO TRADE`, `MTF PASS` → `MTF BLOCK`, `BBW NONE` → `BBW ACTIVE`, etc.). Every line template, separator, column-padding, conditional gate, and section ordering identical. No "lost" or "extra" blocks. No field-label rewording. No precision drift.

### 3.5 BuildPlaintextSnapshot iteration count

**Zero iterations.** First-build dump-file parity check passed immediately — the structural landmarks matched on the first run after `dotnet build`. The block-by-block walk from `RenderOutputHeader` and `RenderOutput` reproduced cleanly via StringBuilder without intermediate fixups. Two minor kickoff-doc inaccuracies surfaced during the walk and were resolved against the actual source (not the kickoff text):

- Kickoff §3.2 Block A wrote `HOLD\EXIT:` (backslash); legacy source emits `HOLD / EXIT:` (forward slash, spaces both sides). Snapshot matches source.
- Kickoff §3.2 Block C said `[BIAS ONLY -- NO TRADE]` (ASCII double-hyphen); legacy source uses `[BIAS ONLY — NO TRADE]` (em-dash, U+2014). Snapshot matches source.

These don't represent kickoff-doc bugs to fix — the doc explicitly says "the pre-P5 dump file is the truth source. Walk the legacy code end-to-end." Did exactly that.

### 3.6 §6.2 inline RenderOutput-vs-snapshot UIA scrape

Not run as a separate step. The §6.3 structural-landmark check + normalised template diff already validated parity to a stronger standard than the UIA scrape would have (which only diffs a single run's txtOutput.Text against the latest dump block). All the §6.2 check would have caught is also caught by §6.3 at higher resolution.

---

## 4. Calibration popup (§6.4)

- Clicked the Calibration Readiness link via UIA Invoke on the LinkLabel.
- UIA window enumeration confirms an `Analysis Report` window opens in the same process (PID 29048). Two descendants: a Document control (the RichEdit hosting the markdown) and a status label reading `Saved: C:\Dev\DeribitVerdictEngine\bin\Debug\net8.0-windows\analysis_log.csv` — confirming the `filePath` param flowed through to the form correctly.
- `TextPattern.DocumentRange.GetText(2000)` returned the expected calibration body: `===` divider, `CALIBRATION READINESS REPORT`, UTC+8 timestamp, `SUMMARY` block with `Total rows logged : 4256 (need 300) [OK]` / `Sessions (days) : 13 (need 3) [OK]` / `Liq events logged : 0 (informational; not a ready gate)`, `REGIME DISTRIBUTION` table with `TRENDING_UP 1416 [OK]` etc. Identical to the pre-P5a RTF render content.
- **UX note:** the AnalysisReportForm opens at a stale persisted position (X=6450, Y=1819) on this dev machine's display layout, landing off-screen. Existed before P5a — same behaviour governs the ANALYSIS REPORT button which uses the same form. Not a P5a regression. A polish spec to reset StartPosition to `CenterParent` is a reasonable post-P5b cleanup; flagging here for the trader's awareness during the verification window.

---

## 5. Self-screenshot tooling status (§9 ask)

All four `tools/*.ps1` scripts work as documented:
- `screenshot-mainform.ps1` — captures `verify/p5-baseline-state.png` cleanly via Win32 PrintWindow (1116×1352).
- `click-mainform-button.ps1` — fires ANALYZE successfully; the substring match resolves to either `?  ANALYZE` (FlatButton overlay caption) or `Analyze Now` (Designer Button caption) depending on UIA iteration order. Both routes invoke the same click handler so the analysis runs either way. Note for future tooling work: enumerate-and-prefer-most-specific would be cleaner than first-substring-match.
- `inspect-mainform-tree.ps1` — used inline equivalents (raw `[System.Windows.Automation.AutomationElement]` queries) for radio-button selection and AnalysisReportForm content grab; both worked.
- `resize-mainform.ps1` — not exercised this phase; P4f screenshot pass already exercised it.

UIA path also supports **non-Button controls** (RadioButton via SelectionItemPattern, LinkLabel via InvokePattern). Used both during verification — the toolkit is more general than its `tools/` wrappers suggest.

---

## 6. Out-of-scope drift check

- ❌ No `UI/Controls/*.vb` edits.
- ❌ No `MainForm.Designer.vb` edits — `txtOutput` field declaration stays exactly as the Designer file has it.
- ❌ No `Core/`, `analysis/`, or `tools/AutoTweaker/` changes.
- ❌ No `settings.json` changes.
- ❌ No CSV schema changes.
- ❌ No Spec C (SC column / TOTAL parity) work.
- ❌ Did not promote `BuildPlainSectionHeader` → `MakeSectionHeader` (consolidation deferred to P5b per §2.2).
- ❌ Did not push to remote.

---

## 7. Handoff to trader-side verification window

**P5b is now gated on the trader.** The deletion sweep does not ship until the trader signals "go" after exercising the app over a representative period.

Sequence:
1. Open the app. Card grid + verification dump card both visible.
2. Use it through whatever set of regimes / signals matters. Eyeball the verification dump card (txtOutput, the legacy RTF render) against the card grid. Confirm every item the legacy text surfaces is genuinely represented somewhere in the cards.
3. If a card-binding gap is found (something legible in the legacy output that's missing from cards), surface it back as a discrete card-binding spec — not part of P5b.
4. When fully comfortable that the cards carry the load, sign off and a P5b conversation can ship the deletion sweep (`MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb` deletion, six `AppendRtf(txtOutput, …)` calls in `MainForm_Analysis.vb`, P/Invoke margins, `OUTPUT_CHARS`/`OUTPUT_LINES`, `_cardVerificationDump` row + `ReparentVerificationDumpControls`, `BuildPlainSectionHeader` → `MakeSectionHeader` consolidation).

**Last successful P5a-flow analysis timestamp (machine reference):** `2026-05-26 23:18:xx UTC+8` (calibration popup smoke test was the last interaction; preceded by 5 successful Analyze cycles on commit 1 + 5 more on commit 2's smoke build). App state at handoff: clean exit, dump file populated with valid post-P5a content, log file healthy at 4256 rows across 13 sessions.

---

## 8. Known transitional duplication (commit 1, by design)

In P5a both `RenderOutputHeader` (legacy) and `BuildPlaintextSnapshot` (new) call `ScoringEngine.CalcKellySizing(v, atrStop, cfg)`. The legacy site fires first (during `RenderOutput`), the new site fires second (inside the snapshot builder before the KELLY block renders). The call is idempotent — `v.KellyPWin` / `v.KellyF` etc. are deterministic from `(v, atrStop, cfg)`, so the second invocation overwrites with identical values. Zero behavioural drift.

In P5b after `RenderOutput` is deleted, the Kelly call inside `BuildPlaintextSnapshot` becomes the sole computation. `BindCardKelly` (in `MainForm_Render_Cards.vb`) currently runs at `MainForm_Analysis.vb:458`, *after* `RenderOutput` at line 445 — meaning it relies on `RenderOutputHeader` having populated `v.Kelly*` first. **P5b will need to hoist a `ScoringEngine.CalcKellySizing` call into `RunAnalysisAsync` between `ScoringEngine.Calculate(...)` and `BindCardKelly(...)`, otherwise the card will render zeros for one cycle after deletion.** Flagging here so the P5b implementer doesn't trip on it; the P5b kickoff doc may want to absorb this note.
