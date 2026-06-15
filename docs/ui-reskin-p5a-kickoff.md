# UI Reskin P5a — Implementation Kickoff

**Phase:** P5a — `BuildPlaintextSnapshot` + dump rewire + calibration viewer migration. **P5b** (deletion of `txtOutput`, render files, RTF helpers, P/Invoke, verification dump card) is a separate later phase, drafted in `ui-reskin-p5b-kickoff.md` and gated on the trader's sign-off after a P5a verification window.
**Spec source:** `docs/ui-reskin-proposal.md` §8 P5 + §10 R1 + handover §3 P5
**Predecessor spec-back:** `docs/ui-reskin-p4f-spec-back.md`
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-25
**Recommended model:** **Opus 4.7 High.** P5a is the only phase the handover flags for High effort. `BuildPlaintextSnapshot` is synthesis-heavy — walk every `AppendRtf` call in `MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb` and reproduce as plaintext via `StringBuilder`. Lower models risk shape-drift in the dump file, which the auto-tweaker / future log readers will see as a schema break.

## Why the P5 split

The trader has explicitly requested keeping `txtOutput` and the verification dump card **alive** through P5a so the legacy RTF output can be displayed side-by-side with the new card grid during a verification window. This lets them confirm every item the legacy display surfaced is genuinely represented somewhere in the card grid before P5b removes the fallback. The pattern matches the conservative bias used throughout earlier phases (e.g., the implementer's parity gate for SC column verification).

After P5a ships, the trader runs the app for as long as needed (days / weeks of live use) to confirm parity. When satisfied, P5b ships the deletion sweep.

---

## 0. What this phase is (and isn't)

**P5a is:** introduce `BuildPlaintextSnapshot(v, r, norms, cfg) As String` to produce the markdown body that replaces `txtOutput.Text` as the input to `AnalysisOutputDump.Append`. Migrate the supporting helpers (`FormatRR`, `BuildCalibrationReport`, `UpdateLogInfo`, the three link click handlers) out of `MainForm_Render_Header.vb` so it can be deleted later. Migrate the calibration report viewer from `txtOutput` to `AnalysisReportForm`.

**P5a is NOT (deferred to P5b):**
- ❌ Deleting `MainForm_Render_Header.vb` or `MainForm_Render_Sections.vb`. **Both files stay alive** so the legacy RTF output keeps writing to `txtOutput`. This is the parity-verification surface for the trader during the in-between window.
- ❌ Removing the `RenderOutput()` call from `RunAnalysisAsync`. Stays running alongside the new snapshot path.
- ❌ Removing any `AppendRtf(txtOutput, ...)` call from `MainForm_Analysis.vb`.
- ❌ Deleting the `_cardVerificationDump` row from `_gridRoot`. The verification dump card stays visible.
- ❌ Removing the P/Invoke surface (`EM_SETMARGINS`, `SendMessage`, `RECT`, `SetOutputMargins`).
- ❌ Removing `OUTPUT_CHARS` / `OUTPUT_LINES` / `SizeToContent`.
- ❌ Helper consolidation (`MakeSectionHeader` / `BuildPlainSectionHeader` merge).

**Both P5a and P5b NEVER touch:**
- ❌ Scoring / indicator / engine code. Read-only access throughout.
- ❌ `UI/Controls/*.vb` files. Paint carve-out NOT invoked.
- ❌ `MainForm.Designer.vb`. Even in P5b, the `txtOutput` field declaration stays — the Designer file is locked.
- ❌ Settings.json. No version bump.
- ❌ CSV schema.
- ❌ Spec C work (SC column parity). Scheduled for post-P5b as Phase 6.

---

## 0.5. Step 0 — Pre-P5 baseline capture (MANDATORY FIRST TASK)

**Do this before writing any P5 code.** The dump-file parity check in §6.3 is the load-bearing verification gate for the entire phase. Without a clean pre-P5 baseline, you can't tell whether `BuildPlaintextSnapshot` matches the legacy `txtOutput.Text` source.

### 0.5.1 Capture the baseline dump

1. Build at HEAD (no changes yet): `dotnet build`
2. Run app: `dotnet run --project DeribitVerdictEngine.vbproj` (background)
3. Click Analyze 5 times across different states. Suggested coverage:
   - One TRENDING regime run
   - One RANGE_BOUND run
   - One run with BBW squeeze active (look for `BBW/TTM ACTIVE` in legacy output)
   - One run with funding penalty firing (any non-FLAT Funding state)
   - One run with `posState ≠ None` (toggle the In Long or In Short radio before clicking) so the HOLD\EXIT block renders

Use `pwsh tools/click-mainform-button.ps1 ANALYZE` between regime / state changes.

4. After the 5th analysis, locate `analysis_output_dump.md`:
   ```
   pwsh -Command "Get-ChildItem -Path bin/Debug/net8.0-windows/ -Filter analysis_output_dump.md"
   ```
5. Copy it: `cp bin/Debug/net8.0-windows/analysis_output_dump.md verify/p5-baseline.md`
6. Kill the app.

The baseline file becomes the truth source for §6.3. Don't commit `verify/p5-baseline.md` — `verify/` is gitignored.

### 0.5.2 Self-screenshot baseline

Per handover §4 locked workflow + tools/README.md:

```
pwsh tools/screenshot-mainform.ps1 verify/p5-baseline-state.png
```

Read the PNG to confirm the current visual baseline matches what's described in the P4f spec-back. This is the "before" reference for the §6 visual verification — after the deletion sweep, the card-grid render should be **bit-identical** because no card binding code is touched.

---

## 1. What you inherit

### 1.1 `AnalysisOutputDump.Append` — the surviving interface

[AnalysisOutputDump.vb:17-40](AnalysisOutputDump.vb:17):

```vb
Public Shared Sub Append(timestamp As DateTime, renderedText As String,
                          dumpPath As String, enabled As Boolean,
                          maxRuns As Integer,
                          Optional perfStripLine As String = Nothing)
```

P5 keeps this signature. The only change at the call site (in `MainForm_Render_Sections.vb:329`, soon deleted) is that `renderedText` will be passed `BuildPlaintextSnapshot(v, r, norms, cfg)` instead of `txtOutput.Text`. `perfStripLine` stays as-is (already built in `Render_Sections.vb:321`; migrate that 3-line builder into the new call site).

`AnalysisOutputDump` itself is host-agnostic per `Core/` design rules. Don't touch it beyond confirming the new caller passes the right strings.

### 1.2 `AnalysisReportForm` — calibration report viewer target

[analysis/AnalysisReportForm.vb:15](analysis/AnalysisReportForm.vb:15):

```vb
Public Sub New(markdownText As String, filePath As String)
```

Constructor takes a markdown string + a file path (used for the form's title bar / save-to-disk option). Migration pattern for `lnkCalibCheck`:

```vb
' BEFORE (MainForm_Render_Header.vb:51-53)
Private Sub lnkCalibCheck_LinkClicked(...) Handles lnkCalibCheck.LinkClicked
    txtOutput.Clear()
    AppendRtf(txtOutput, BuildCalibrationReport(), Theme.FG_PRIMARY)
End Sub

' AFTER (relocated to MainForm_Layout.vb or a new MainForm_Calibration.vb partial)
Private Sub lnkCalibCheck_LinkClicked(...) Handles lnkCalibCheck.LinkClicked
    Dim md As String = BuildCalibrationReport()
    Dim frm As New AnalysisReportForm(md, AnalysisLogger.GetLogPath())
    frm.Show()
End Sub
```

`BuildCalibrationReport()` already produces a markdown string (see [MainForm_Render_Header.vb:82-...](UI/MainForm_Render_Header.vb:82)) — no shape change needed. Pass `AnalysisLogger.GetLogPath()` as the filePath since the calibration report is derived from the analysis log; the form's title bar will say "Analysis Report" but the path field will identify the underlying log file.

`BuildCalibrationReport` itself must **migrate out of `MainForm_Render_Header.vb`** before that file is deleted — see §5.3.

### 1.3 Helpers that must migrate before the render files are deleted

| Helper | Current location | Used by | Target |
|---|---|---|---|
| `FormatRR(reward, risk)` | [MainForm_Render_Header.vb:397](UI/MainForm_Render_Header.vb:397) | `MainForm_Render_Cards.vb:903`, `:968` (R:R cell rendering on ATR + STRUCTURAL cards) | Move to `MainForm_Render_Cards.vb` as a `Private Shared Function`. |
| `BuildCalibrationReport()` | `MainForm_Render_Header.vb:82` | `lnkCalibCheck_LinkClicked` | Move to a new partial file, e.g. `MainForm_Calibration.vb`, OR into `MainForm_Layout.vb`. Reads `AnalysisLogger` and `_logRows` — host-agnostic logic, just lives in MainForm partial for now. |
| `UpdateLogInfo()` | `MainForm_Render_Header.vb:14` | Multiple sites (skip branch, every successful render, post-reset) | Move to `MainForm_Layout.vb`. It's now display-only (sets `lblLogInfo.Text` + `lblLastSuccess.Text` + tooltip); no RTF surface. |
| `lnkResetLog_LinkClicked` | `MainForm_Render_Header.vb:25` | Reset Log button | Move to `MainForm_Layout.vb`. |
| `lnkAnalysisReport_LinkClicked` | `MainForm_Render_Header.vb:60` (Async) | ANALYSIS REPORT CTA | Move to `MainForm_Layout.vb`. |
| `lnkCalibCheck_LinkClicked` | `MainForm_Render_Header.vb:51` | Calibration Readiness LinkRow | Move to `MainForm_Layout.vb` AFTER §4 migrates its body to `AnalysisReportForm`. |

After migration, `MainForm_Render_Header.vb` should contain only: `AppendRtf`, `AR`, `SectionHeader`, `Divider`, `RenderOutputHeader` itself, the verdict / ATR / KELLY / structural RTF blocks. All deletable in one commit.

### 1.4 Legacy surface to delete

After helpers migrate, delete in one pass:

| File / surface | Action |
|---|---|
| `UI/MainForm_Render_Header.vb` | Delete file. |
| `UI/MainForm_Render_Sections.vb` | Delete file. |
| `txtOutput.Clear()` + `AppendRtf(txtOutput, ...)` calls in `UI/MainForm_Analysis.vb:33, 41, 110-112` | Remove all six call sites. See §5.2 for what replaces them (mostly nothing — the new card UI surfaces the same info; ERROR path may need a `MessageBox`). |
| `RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)` call in `MainForm_Analysis.vb:441` | Replace with `BuildPlaintextSnapshot` invocation directly (see §3 for the wiring). |
| RTF helpers: `AppendRtf`, `AR`, `SectionHeader`, `Divider` | Deleted with their host file. |
| `FormatRR` | Migrated, then deleted from Header. |
| P/Invoke: `EM_SETMARGINS`, `EM_SETRECT`, `EM_SETRECTNP`, `SendMessage`, `RECT` struct, `SetOutputMargins`, `OnFormHandleCreated` (if only used for txtOutput margins) | Search `MainForm_Layout.vb` for the `<DllImport>` block and the constants; delete. |
| `OUTPUT_CHARS`, `OUTPUT_LINES` constants | Delete from `MainForm_Layout.vb`. |
| `SizeToContent` method (if still present) | Delete from `MainForm_Layout.vb`. Already superseded by `ApplyInitialFormSize` in P4a. |
| `txtOutput` field declaration in `MainForm.Designer.vb` | **DO NOT delete.** The Designer file is untouchable. Instead: `txtOutput.Visible = False` in `MainForm_Layout.vb` constructor (already the case after P4f); P5 leaves the field dormant. If a future cleanup spec wants it gone, that's a separate workflow that involves regenerating the Designer file. |
| `_cardVerificationDump` | Delete its row from `_gridRoot` in `MainForm_Layout.vb`. Verification dump card disappears. |
| `ReparentVerificationDumpControls()` | Delete the method. |

### 1.5 Helper consolidation (per handover §6 outstanding decisions)

After the deletion sweep, the helper consolidation lands in the same commit:

- `MakeSectionHeader(text, Optional colour)` at [MainForm_Render_Cards.vb](UI/MainForm_Render_Cards.vb) — keep.
- `BuildPlainSectionHeader(text)` at `MainForm_Render_Cards.vb` — **delete**. Four call sites (KELLY, OI × CVD CROSS, VOLUME PROFILE, INDICATOR DETAILS) replace with no-arg `MakeSectionHeader` calls.

Per `be7f64b`'s unification, both helpers produce identical output (11pt + `FG_SECONDARY` default). `BuildPlainSectionHeader` was the post-P4d helper that predated the unification.

---

## 2. Commit plan (P5a only — two commits)

Two commits in P5a. P5b's deletion-sweep commit is in `ui-reskin-p5b-kickoff.md`, drafted but not actionable until the trader signs off on the P5a verification window.

| # | Subject | Scope | Effort | LoC est. |
|---|---|---|---|---|
| 1 | `feat(ui-reskin): P5a — BuildPlaintextSnapshot + AnalysisOutputDump rewire` | New `BuildPlaintextSnapshot` builder + helper migrations (FormatRR, BuildCalibrationReport, UpdateLogInfo, 3 link click handlers). `AnalysisOutputDump.Append` call site moves to `MainForm_Analysis.vb` and switches its `renderedText` argument to `BuildPlaintextSnapshot(...)`. **`MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb` STAY ALIVE; `RenderOutput` STAYS RUNNING** so `txtOutput` keeps receiving the legacy RTF output — the trader needs this as the parity-verification surface during the window before P5b. | High | ~600-800 |
| 2 | `feat(ui-reskin): P5a — migrate calibration report to AnalysisReportForm` | Body of `lnkCalibCheck_LinkClicked` switches from `txtOutput`-based render to `AnalysisReportForm.Show()`. Independent of commit 1's snapshot work — could be committed first or second; depends only on §4 below. **Note:** unlike commit 1's parity-preserving design, this commit DOES remove a `txtOutput` writer (the calibration report's clear + append). Verification-dump-card parity loses one of its inputs, but that input was on-demand only (triggered by clicking Calibration Readiness), not part of the analysis cycle. Trader confirms acceptable in the P5a verification window. | Low | ~30 |

### 2.1 Per-commit ship / skip

**Commit 1:**
- ✅ `BuildPlaintextSnapshot(v, r, norms, cfg) As String` in a new partial `UI/MainForm_PlaintextSnapshot.vb` (clean isolation; deletion-friendly if the spec gets refactored later)
- ✅ Helper migrations per §1.3 (`FormatRR`, `BuildCalibrationReport`, `UpdateLogInfo`, all three link click handlers move to new homes outside `MainForm_Render_Header.vb`)
- ✅ Move `AnalysisOutputDump.Append` invocation out of `MainForm_Render_Sections.vb:329` (still alive after this commit, just not the dump caller anymore) and into `MainForm_Analysis.vb` after `UpdatePerformanceLabels`. Pass `BuildPlaintextSnapshot(...)` as `renderedText`; migrate the perf-strip line builder from Render_Sections:321 alongside.
- ✅ **Keep `RenderOutput` running.** Don't touch its invocation at `MainForm_Analysis.vb:441`. `txtOutput` keeps getting RTF-written. The verification dump card keeps displaying it.
- ⏸ Don't delete render files. P5b.
- ⏸ Don't migrate calibration viewer. Commit 2.
- ⏸ Don't touch `MainForm_Analysis.vb`'s six `AppendRtf(txtOutput, ...)` calls (lines 33, 41, 110-112). P5b.

**Commit 2:**
- ✅ Rewrite `lnkCalibCheck_LinkClicked` body to use `AnalysisReportForm` per §4.
- ⏸ Nothing else.

### 2.2 What's NOT in P5a (all moves to P5b)

The full P5b ship-list, captured here so the implementer knows the scope boundary:

- Delete `MainForm_Render_Header.vb` and `MainForm_Render_Sections.vb` entirely.
- Remove all six `AppendRtf(txtOutput, ...)` calls from `MainForm_Analysis.vb`.
- Remove the `RenderOutput` call from `MainForm_Analysis.vb`.
- Remove the P/Invoke surface (EM_SETMARGINS / SendMessage / RECT / SetOutputMargins).
- Remove `OUTPUT_CHARS` / `OUTPUT_LINES` constants and `SizeToContent` (if still present).
- Remove the `_cardVerificationDump` row from `_gridRoot` and delete `ReparentVerificationDumpControls`.
- Consolidate `BuildPlainSectionHeader` → `MakeSectionHeader` (handover §6 backlog).
- `txtOutput.Visible = False` line stays — the Designer field is locked off-limits.

---

## 3. `BuildPlaintextSnapshot` — shape specification

### 3.1 Signature

```vb
' UI/MainForm_PlaintextSnapshot.vb — new partial file.
Partial Public Class MainForm

    ''' <summary>
    ''' P5 — Produces the markdown-style text body that replaces the
    ''' legacy txtOutput.Text source of AnalysisOutputDump.Append. Shape
    ''' must match the pre-P5 dump file byte-for-byte (excluding timestamps)
    ''' so existing dump-readers continue to parse.
    ''' </summary>
    Friend Function BuildPlaintextSnapshot(
            v As VerdictResult,
            r As IndicatorResults,
            norms As DynamicNorms,
            cfg As EngineSettings) As String
        Dim sb As New System.Text.StringBuilder()
        ' ... blocks per §3.2 ...
        Return sb.ToString()
    End Function

End Class
```

`Friend` (not `Public Shared`) because it reads the form's display-time state (`_skipCount`, position radios, `_lastSuccessfulRenderTime` indirectly via the calibration helpers, etc.) and is called only from `MainForm_Analysis.RunAnalysisAsync`.

### 3.2 Block-by-block shape

The pre-P5 dump file is the truth source. Walk `MainForm_Render_Header.RenderOutputHeader()` and `MainForm_Render_Sections.RenderOutput()` end-to-end and reproduce each `AppendRtf` line as a `sb.AppendLine(...)`. The blocks in emission order:

#### Block A — Verdict header (`Render_Header.vb:~460-560`)

```
===========================================================
  VERDICT:    NO TRADE
  CONTEXT:    MOMENTUM_FADING
  CONFIDENCE: N/A
  SCORE:      Long 3/20  |  Short 3/20
  TIME:       2026-05-25 17:55:49 UTC+8
===========================================================
  LAST TRANSACTED PRICE:  76384.5
  HOLD\EXIT:   {value}     <- only when posState ≠ None
```

Key formatters:
- `v.Verdict` — string
- `v.VerdictContext` — emit only when non-empty
- `v.Confidence` — string ("N/A" / "LOW" / "MEDIUM" / "HIGH")
- `v.LongScore`, `v.ShortScore`, `v.MaxScore` — integers
- Timestamp: `DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd HH:mm:ss") & " UTC+8"`
- `recentTrades(0).Price` for last transacted (this is currently `lastTradePrice` in the call chain — pass it through)
- HOLD\EXIT gating: `If v.HoldStatus <> "N/A -- no open position" Then ...`

#### Block B — ATR ENTRY LEVELS (`Render_Header.vb:~530-640`)

```
ATR ENTRY LEVELS  (ATR {v:F2} x {scale:F2} scale | {stopMult}x stop / {targetMult}x target)
  Long:   Stop   {sL:F1}  |  Entry   {entry:F1}  |  Target   {tL:F1}    R:R 1:{rr}  (risk {x} / rwd {y})
  Long structural:  Stop   {sLs:F1}  |  Entry   {entry:F1}  |  Target   {tLs:F1}    R:R 1:{...}  (risk {...} / rwd {...})
  Short:  Stop   {sS:F1}  |  Entry   {entry:F1}  |  Target   {tS:F1} --> {capped:F1}  [CAPPED @ {tier} ({reason})]
  Short structural: ...
```

CAPPED branch: render only when `v.AdjustedLongTarget > 0` (or Short), AND `|raw - adjusted| >= max(0.5, ATR * 0.02)` (v30 sub-tick suppression). When suppressed, drop the `[CAPPED ...]` clause and the `-->` arrow; render the raw target as if no cap.

Use the migrated `FormatRR(reward, risk)` helper for the R:R column.

Cap reason strings: `v.TargetCapReasonLong` / `TargetCapReasonShort` — emit as-is.

Structural rows: render in cyan-equivalent (just plain text now, no colour codes in plaintext); when only one side exists, emit the per-side missing-data note (v30 F12 wording).

#### Block C — KELLY SIZING (`Render_Header.vb:~605-660`)

```
KELLY SIZING  [BIAS ONLY -- NO TRADE]  [CAPPED]
  Advisory (ATR-basis) -- R:R uses ATR multiples, not structural targets.
  Treat as directional bias indicator only.
  p(win):   {kPwin:F1}%
  f* / Half-Kelly:  {kF:F2}%  /  {kHalfKelly:F2}%
  Applied fraction: {appliedF:F2}%
  Risk $:    ${riskDollars:F2}
  Lean: {N} contract(s)  | Lean: < 1 contract  (bias only; not a trade signal)
```

Suppression: when `v.KellyPWin = 0`, drop the entire block.

Pluralisation: `1 contract` singular vs `N contracts` plural. v30 fix.

`[BIAS ONLY -- NO TRADE]` prefix when verdict starts with `NO TRADE`.
`[CAPPED]` suffix when `v.KellyCapped = True`.

#### Block D — DYNAMIC NORMS (`Render_Sections.vb:~30-45`)

```
DYNAMIC NORMS  [LIVE]
  Vol threshold : H:{volHigh}x  M:{volMid}x  (mean={mean} BTC  s={stddev})
  VWAP dev thr  : +/-{vwapThr}% (legacy ref)
  ATR scale     : {scale}x  (ATR={atr}  ref={ref})
```

`[LIVE]` vs `[STATIC FALLBACK]` per `norms.IsLive`.

#### Block E — REGIME (`Render_Sections.vb:~50-55`)

```
REGIME (5m): {r.Regime}
  ADX: {r.ADX:F1}  |  +DI: {r.PlusDI:F1}  |  -DI: {r.MinusDI:F1}
```

#### Block F — CORE SIGNALS, VWAP, BBW/TTM, EMA RIBBON, MARKET STRUCTURE, OI, ORDER FLOW, LIQUIDATIONS, MTF GATE, FUNDING

Each section: header line + 1-N indicator rows. Walk `Render_Sections.vb:~55-280` and reproduce.

#### Block G — SIGNAL BREAKDOWN table (`Render_Sections.vb:~285-310`)

```
===========================================================
  SIGNAL BREAKDOWN
===========================================================
  Signal               Long   Short  Note
  ----------------------------------------------------------------------
  ROC(9)                             {note}
  RSI(9)                             {note}
  ...
  Funding (info)                     {note}
  ...
  Trend Structure                    {note}
  MTF Gate (15m)                     {note}
  ----------------------------------------------------------------------
  TOTAL                   {LongScore}       {ShortScore}
```

The `[L]` / `[S]` direction marks for each row come from `it.LongHit` / `it.ShortHit`. The current legacy format uses 4-column-wide left-padding to align `[L]` and `[S]` columns. Reproduce exact spacing or the dump-diff in §6.3 will show a noise delta.

**Critical:** the per-row direction marks remain `[L]` / `[S]` / blank (NOT integer points). Spec C may upgrade these to magnitudes later; P5 leaves the legacy shape intact. CSV / auto-tweaker / dump readers all parse the current shape.

### 3.3 Approach — read once, write once

Don't try to abstract. Open `MainForm_Render_Header.vb` and `MainForm_Render_Sections.vb` side-by-side with the snapshot builder. Each `AppendRtf(rtb, "  VERDICT:    ", FG_TERTIARY)` becomes `sb.Append("  VERDICT:    ")`. Each newline-terminated `AppendRtf(..., text & Environment.NewLine, ...)` becomes `sb.AppendLine(text)`. Colours drop (plaintext).

The `AR(rtb, label, value, ...)` helper produces `"  " & label & value & Environment.NewLine` — replicate as `sb.AppendLine("  " & label & value)`. Same for `SectionHeader` and `Divider`.

Don't refactor the section composition. The current ordering is the truth source for the dump file's shape; reordering breaks parity.

### 3.4 Where the call site moves to

[MainForm_Analysis.vb:441](UI/MainForm_Analysis.vb:441) currently calls `RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)` which internally calls `AnalysisOutputDump.Append` at `Render_Sections.vb:329`.

After commit 1:

```vb
' Existing card bindings stay as-is (BindCardScore, BindCardVerdict, etc.)
BindCardScore(verdict)
BindCardVerdict(verdict, r)
' ... etc ...

' P5 — replace the legacy RenderOutput → AnalysisOutputDump.Append chain
' with a direct snapshot build + dump call. Migrated from
' MainForm_Render_Sections.vb:321-335.
Dim snapshot As String = BuildPlaintextSnapshot(verdict, r, norms, cfg)
Dim perfStripLine As String = ComposePerfStripLine()   ' migrate from Render_Sections.vb:321
AnalysisOutputDump.Append(
    timestamp:=verdict.Timestamp,
    renderedText:=snapshot,
    dumpPath:=cfg.AnalysisLogging.OutputDumpPath,
    enabled:=cfg.AnalysisLogging.OutputDumpEnabled,
    maxRuns:=cfg.AnalysisLogging.OutputDumpMaxRuns,
    perfStripLine:=perfStripLine)

' Legacy RenderOutput still runs alongside in commit 1 — kept so
' the post-commit-1 verification gate compares the new snapshot
' against the legacy txtOutput.Text output. Removed in commit 3.
RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)
```

After commit 3:

```vb
Dim snapshot As String = BuildPlaintextSnapshot(verdict, r, norms, cfg)
Dim perfStripLine As String = ComposePerfStripLine()
AnalysisOutputDump.Append(
    timestamp:=verdict.Timestamp,
    renderedText:=snapshot,
    dumpPath:=cfg.AnalysisLogging.OutputDumpPath,
    enabled:=cfg.AnalysisLogging.OutputDumpEnabled,
    maxRuns:=cfg.AnalysisLogging.OutputDumpMaxRuns,
    perfStripLine:=perfStripLine)
' RenderOutput deleted.
```

---

## 4. Calibration report migration (commit 2)

[MainForm_Render_Header.vb:51](UI/MainForm_Render_Header.vb:51):

```vb
' BEFORE
Private Sub lnkCalibCheck_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) _
        Handles lnkCalibCheck.LinkClicked
    txtOutput.Clear()
    AppendRtf(txtOutput, BuildCalibrationReport(), Theme.FG_PRIMARY)
End Sub

' AFTER (relocated to MainForm_Layout.vb or a new MainForm_Calibration.vb)
Private Sub lnkCalibCheck_LinkClicked(sender As Object, e As LinkLabelLinkClickedEventArgs) _
        Handles lnkCalibCheck.LinkClicked
    Dim md As String = BuildCalibrationReport()
    Dim frm As New AnalysisReportForm(md, AnalysisLogger.GetLogPath())
    frm.Show()
End Sub
```

`AnalysisReportForm` is non-modal — `.Show()` not `.ShowDialog()`. Multiple invocations create multiple windows; that's fine (matches the existing `lnkAnalysisReport_LinkClicked` pattern).

The form's title bar reads "Analysis Report" — same as the existing ANALYSIS REPORT button. The user opens calibration report ~weekly during recalibration cycles, much less frequently than ANALYSIS REPORT. If a future spec wants a separate "Calibration Report" title, add a third parameter to `AnalysisReportForm.New` — out of scope here.

### 4.1 `BuildCalibrationReport` ownership

`BuildCalibrationReport()` at [MainForm_Render_Header.vb:82](UI/MainForm_Render_Header.vb:82) returns a plaintext markdown string already — no RTF dependency. P5 moves it (along with its `Flag()` helper used at line 174ish) to a new partial `UI/MainForm_Calibration.vb` OR appends to `MainForm_Layout.vb`. Pick whichever keeps `MainForm_Layout.vb` under ~1000 LoC — at draft time Layout is ~870 lines, so a separate partial is cleaner.

---

## 5. Deletion sweep (P5b — out of P5a scope)

The following is the **P5b** plan, included here so the P5a implementer understands what survives and what's slated for removal later. None of the items below land in P5a. The detail mirrors what `ui-reskin-p5b-kickoff.md` will instantiate when the trader signs off.

### 5.1 `txtOutput` field — the Designer carve-out

`MainForm.Designer.vb` declares `txtOutput As RichTextBox`. **Do not delete this declaration.** The Designer file is untouchable per handover §4 lock + CLAUDE.md.

P5 leaves the field declared but inert:
- `txtOutput.Visible = False` in `MainForm_Layout.vb` (already set, leave it)
- Remove the field from the layout flow (don't add it to `_gridRoot`'s cells)
- All writes to `txtOutput.Clear()`, `txtOutput.Text`, `AppendRtf(txtOutput, ...)` are removed

Result: the field exists in the class but is invisible, never rendered, and has no writers. Equivalent to deletion for all practical purposes.

If a future workflow change ever decides to regenerate `MainForm.Designer.vb` (e.g., a "designer fresh-start" pass), it can remove the field then. Out of scope here.

### 5.2 `AppendRtf(txtOutput, ...)` calls in `MainForm_Analysis.vb`

| Line | Current text | P5 disposition |
|---|---|---|
| 33 | `"Fetching data from Deribit..."` | **Delete.** The `btnAnalyze.Text = "Fetching..."` line at 31 already signals the in-flight state on the button itself. |
| 41 | `"ERROR: " & ex.Message & ...stackTrace`  | **Replace with `MessageBox.Show(...)`.** Errors are rare (caught when `RunAnalysisAsync` itself throws, distinct from the resilience skip path). A blocking dialog forces the user to acknowledge; better than silently logging to an invisible textbox. |
| 110 | `"ANALYSIS SKIPPED: {skipReason}"` | **Delete.** The new VERDICT card SKIPPED panel surfaces this. |
| 111 | `"Skip count this session: {N}"` | **Delete.** Surfaced in the LOG sub-box via `· skipped {N}` already (P4e). |
| 112 | `"Engine continues — next auto-run cycle will retry."` | **Delete.** Surfaced as the hint sub-line in the SKIPPED panel ("Engine retains last-known indicator values. Skipping verdict generation until next successful fetch (auto-run continues).") |

After this commit, `MainForm_Analysis.vb` has zero references to `txtOutput`.

### 5.3 Render file deletions

```
git rm UI/MainForm_Render_Header.vb
git rm UI/MainForm_Render_Sections.vb
```

Confirm the migrations from §1.3 are all in place first. Build will fail if any helper / handler is still referenced from elsewhere.

### 5.4 P/Invoke + constants sweep

Search `MainForm_Layout.vb` for these and delete:

- `<DllImport("user32.dll")>` block (if present in MainForm partials)
- `EM_SETMARGINS`, `EM_SETRECT`, `EM_SETRECTNP` constants
- `RECT` struct (if it's specific to txtOutput margins — verify it's not used elsewhere; the `RECT` in `resize-mainform.ps1` is a separate PowerShell struct)
- `SendMessage` overload
- `SetOutputMargins()` method
- `OUTPUT_CHARS`, `OUTPUT_LINES` constants
- `SizeToContent()` method (superseded by `ApplyInitialFormSize` in P4a)
- `OnFormHandleCreated` override (verify it has no other body before deleting)

Use grep:
```
grep -n "EM_SET\|RECT\b\|SendMessage\|SetOutputMargins\|OUTPUT_CHARS\|OUTPUT_LINES\|SizeToContent" UI/*.vb
```

Each match either deletes cleanly or surfaces a use case the spec missed — pause and surface back.

### 5.5 Verification dump card

In `MainForm_Layout.vb`'s `BuildCardGridLayout` (around line 465 — search for `_cardVerificationDump`):

- Remove the `AddRow(_cardVerificationDump, 400)` call
- Remove the `ReparentVerificationDumpControls()` invocation
- Delete the `ReparentVerificationDumpControls()` method body
- Remove the `_cardVerificationDump` field declaration

`txtOutput` was the only child of that card — once removed from the layout, the card has nothing to host.

### 5.6 Helper consolidation

Per §1.5:

```vb
' Find all 4 call sites of BuildPlainSectionHeader:
grep -n "BuildPlainSectionHeader" UI/MainForm_Render_Cards.vb

' Each call site: BuildPlainSectionHeader("KELLY SIZING") → MakeSectionHeader("KELLY SIZING")
' Then delete BuildPlainSectionHeader's definition.
```

---

## 6. Verification gate

### 6.1 After each commit — build + screenshot

Per handover §10.9 (self-screenshot default workflow):

```
dotnet build                                                        # must be clean
dotnet run --project DeribitVerdictEngine.vbproj                    # run_in_background=true
sleep 10
pwsh tools/screenshot-mainform.ps1 verify/p5a-c{N}-state.png
pwsh tools/click-mainform-button.ps1 ANALYZE
sleep 8
pwsh tools/screenshot-mainform.ps1 verify/p5a-c{N}-after-analyze.png
# Read both PNGs. Inspect for clipping, missing cards, layout breakage.
```

Card-grid visual MUST be bit-identical across both commits — no card binding code is touched. If anything shifts visually, you've accidentally changed a card layout dependency. **The verification dump card showing legacy RTF output must stay visible and populated** — that's the trader's parity-check surface for the verification window.

### 6.2 After commit 1 — RenderOutput parallel parity (the critical P5a gate)

Commit 1 keeps `RenderOutput` running alongside the new snapshot path. The new dump-file output (sourced from `BuildPlaintextSnapshot`) must match what the legacy txtOutput-based output would have written:

```
# Capture the legacy txtOutput.Text via UIA after a fresh analysis.
pwsh tools/click-mainform-button.ps1 ANALYZE
sleep 8
# Extract txtOutput.Text content and the latest dump-file run block.
pwsh -Command @'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$forms = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)
foreach ($f in $forms) {
    if ($f.Current.Name -match "Deribit Verdict Engine") {
        $edits = $f.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::Edit)))
        foreach ($e in $edits) {
            $v = $e.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
            $text = $v.Current.Value
            if ($text -match "VERDICT:" -or $text -match "ATR ENTRY LEVELS") {
                $text | Out-File verify/p5a-txtout-snapshot.txt -Encoding utf8
                break
            }
        }
        break
    }
}
'@
# Extract latest dump block (everything between last "## Run" and the final "---").
tail -n 200 bin/Debug/net8.0-windows/analysis_output_dump.md > verify/p5a-dump-latest.txt
# Diff them. Modulo timestamp and "## Run …" header, content should match.
diff verify/p5a-txtout-snapshot.txt verify/p5a-dump-latest.txt
```

This diff is the immediate sanity gate — if it shows shape drift, iterate `BuildPlaintextSnapshot` until parity holds. The structural diff in §6.3 is the longer-running version, exercised throughout the verification window.

### 6.3 Pre/post P5a dump diff — STRUCTURAL PARITY GATE

After commit 1 lands:

```
# Save 5 fresh analyses worth of new-snapshot dump output.
cp bin/Debug/net8.0-windows/analysis_output_dump.md verify/p5a-postcommit.md

# Diff against the pre-P5a baseline captured in §0.5.
diff verify/p5-baseline.md verify/p5a-postcommit.md
```

Expected differences:
- ✅ `## Run yyyy-MM-dd HH:mm:ss` timestamps (different runs).
- ✅ Per-run market values (different prices, scores, regime, etc.).

NOT expected — these are bugs:
- ❌ Differences in section ordering.
- ❌ Differences in field labels, header text, divider style.
- ❌ Missing blocks (e.g., DYNAMIC NORMS block gone).
- ❌ Extra blocks (e.g., a new section the snapshot builder accidentally introduced).
- ❌ Differences in indicator formatting (precision changed, sign convention shifted, etc.).
- ❌ TOTAL row spacing changed.
- ❌ `[L]`/`[S]` column alignment shifted.

If `diff` shows ONLY timestamps and market values, the snapshot is faithful to the legacy shape. If it shows anything else, iterate until clean.

Practical workflow: capture both pre-P5a and post-P5a dump runs while a specific market condition holds (e.g., RANGE_BOUND regime with funding rate near zero). Same market = same expected content. Any non-market difference is a snapshot bug.

### 6.4 Calibration viewer smoke test (commit 2)

Click `Calibration Readiness` in the TOOLS section. The `AnalysisReportForm` should pop up showing the calibration report markdown. Compare with the pre-P5a behaviour (RTF render in txtOutput) — text content should be identical.

### 6.5 The trader-side parity window — what P5a hands off to the user

P5a does NOT have a deletion-sweep verification gate. Instead, after both commits ship and the §6.2 + §6.3 dump parity checks pass, the implementation conversation **hands the app off to the user for the verification window**:

1. App runs normally — card grid + verification dump card both visible.
2. Trader uses the app over a representative window (days to weeks of live use).
3. Trader walks through every indicator / state they care about and confirms each one is **legibly represented in the card grid**, not just in the legacy txtOutput.
4. If a gap is found (something visible in legacy that's missing from cards), it goes back to the spec author as a card-binding fix — a discrete spec, not part of P5b.
5. Only when the trader explicitly signals "go" does P5b ship the deletion sweep.

The implementation conversation should **end its spec-back with an explicit "handing off to verification window" note** so the next conversation knows P5b is gated on the trader, not on a build / test gate.

---

## 7. Out of scope — explicit skip list

If any of these tempt you, **stop** and surface back:

- ❌ Any modification to `UI/Controls/*.vb` files. Paint carve-out **not** invoked.
- ❌ Touching `MainForm.Designer.vb`. The `txtOutput` field stays declared (per §5.1).
- ❌ Scoring / indicator / `Core/` changes.
- ❌ Settings.json changes.
- ❌ CSV schema changes.
- ❌ Fix the SC column / TOTAL parity (Spec C — scheduled for post-P5 as Phase 6).
- ❌ Refactor the snapshot builder's section composition for "elegance." Match legacy shape exactly.
- ❌ Promote `verify/click-analyze.ps1` and `verify/resize-window.ps1` from previous phases — already done by the spec author in the helper-promotion commit.
- ❌ Move `AnalysisOutputDump.vb` out of project root. It's host-agnostic; staying in root is fine.
- ❌ Push to remote.

---

## 8. If you get stuck

Likely failure modes:

1. **Dump file diff in §6.3 shows extra whitespace / trailing newlines.** `AppendRtf` calls that ended with explicit `Environment.NewLine` vs ones that didn't produce subtle differences. `sb.AppendLine` always appends `\r\n`; `sb.Append` doesn't. Walk the legacy code call-by-call.

2. **`FormatRR` migration breaks the ATR card R:R column.** `FormatRR` uses `< 0.1` literal for sub-1dp ratios (v30 F3). Confirm both call sites in `MainForm_Render_Cards.vb:903, :968` still work after the helper moves.

3. **`MessageBox.Show` for errors (§5.2 line 41) blocks the auto-run timer.** It does — that's the trade-off. Errors are rare, and the user wants to see them. If the user objects after testing, replace with a brief `Pill` shown in the header strip for 5 seconds.

4. **Build fails after deleting render files because a helper was missed.** Search the codebase for any remaining reference: `grep -rn "AppendRtf\|RenderOutput\b\|RenderOutputHeader\b" UI/` should return nothing after commit 3.

5. **The `_cardVerificationDump` removal shifts the form's vertical layout.** The form height auto-derives from `_gridRoot` row sizes — losing the 400px verification dump card row shrinks the form by ~400px. That's expected; the form is just shorter now. If anything else moves visually, surface back.

6. **`BuildCalibrationReport` references `_logRows` or other MainForm state.** Fine when it lives in a `MainForm_*.vb` partial — that's the same class. Just ensure the migration target file is `Partial Public Class MainForm`.

---

## 9. Reporting back

Spec-back doc: `docs/ui-reskin-p5a-spec-back.md`. Same structure as past spec-backs.

Specifically worth reporting if they happen:

1. **Post-commit-1 dump-file diff line count** (excluding timestamp and market-value lines). Should be 0 if `BuildPlaintextSnapshot` matches the legacy shape. If non-zero, list the categories of drift.
2. **§6.2 inline RenderOutput-vs-snapshot parity** — did the txtOutput-vs-dump diff match first try, or did the snapshot need iteration? Note the iterations.
3. **`lnkCalibCheck` migration UX** — does the `AnalysisReportForm` popup feel right? Note any minor friction (form title, close behaviour, sizing) so a post-P5b polish spec can address.
4. **Self-screenshot helpers** — confirm `tools/screenshot-mainform.ps1` + companions still work for P5a verification. If anything broke, surface it.
5. **End of report**: explicit "handing off to verification window — P5b gated on trader sign-off." Include the timestamp of the last successful P5a analysis so the user knows the app state when they take over.

---

## 10. Workflow reminders

- **Local commits only.** Three commits per the plan. Do NOT push. User decides when to push after live-data verification.
- **No `--no-verify`**, no force ops, no `MainForm.Designer.vb` edits.
- **Self-screenshot is the default verification path** per handover §10.9. Use `tools/screenshot-mainform.ps1` + `click-mainform-button.ps1` + `inspect-mainform-tree.ps1`. Fall back to user-screenshot only on capability check failure.
- **Engine code untouched.** `Core/` is read-only. `analysis/` is read-only.
- **Settings.json untouched.** No version bump.
- **The §4 paint carve-out is NOT invoked.** No `UI/Controls/*.vb` modifications.
- **Pre/post P5 dump file diff is the load-bearing verification.** §6.3. Don't skip it.
- **`verify/p5-baseline.md` and `verify/p5-baseline-state.png` are mandatory** — capture before commit 1 per §0.5. Without them, the diff gate can't run.

---

**End of kickoff.** Drop this verbatim into a fresh Opus 4.7 High conversation as the opening message; the conversation has everything it needs to ship P5a in two commits + spec-back. **Recommended model is High, not Medium** — the snapshot synthesis is the heaviest single piece of work in the entire reskin.

After P5a ships, hand off to the user for the verification window. P5b ships when the user signals it's ready.
