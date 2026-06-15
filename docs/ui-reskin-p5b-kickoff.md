# UI Reskin P5b — Implementation Kickoff

**Phase:** P5b — Deletion sweep. Removes the legacy RTF render pipeline + the `txtOutput` writers + the verification dump card + the P/Invoke surface + consolidates helpers. Final phase of the UI reskin.
**Spec source:** `docs/ui-reskin-proposal.md` §8 P5 + handover §3 P5 + `docs/ui-reskin-p5a-spec-back.md` §5
**Predecessor:** **P5a must be shipped and trader-verified before this kickoff is actionable.** See §0.5 for the gating check.
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-25 (skeleton — will be re-flowed against the P5a spec-back when the trader signals readiness)
**Recommended model:** **Opus 4.7 Medium.** P5b is mechanical: walk a list of files / methods / constants and delete them. The synthesis-heavy `BuildPlaintextSnapshot` work landed in P5a. The risk here is missing an orphan reference — caught by the build, not by synthesis.

---

## 0. What this phase is (and isn't)

**Is:** the deletion sweep that finalises the UI reskin. After P5b:
- `MainForm_Render_Header.vb` and `MainForm_Render_Sections.vb` are gone.
- The `RenderOutput` call in `RunAnalysisAsync` is removed.
- All `txtOutput` writers in `MainForm_Analysis.vb` are removed.
- The verification dump card is gone from the layout.
- The P/Invoke surface (EM_SETMARGINS / SendMessage / RECT / SetOutputMargins) is gone.
- `OUTPUT_CHARS` / `OUTPUT_LINES` / `SizeToContent` are gone.
- `BuildPlainSectionHeader` is consolidated into `MakeSectionHeader`.

**Isn't:**
- ❌ Any new visual feature.
- ❌ Scoring / indicator / engine change.
- ❌ `UI/Controls/*.vb` modification.
- ❌ `MainForm.Designer.vb` edit. The `txtOutput` field declaration stays — it's the locked carve-out. After P5b, `txtOutput` is a zombie field: declared in Designer, instantiated by `InitializeComponent`, never written to, never read from, `.Visible = False`, no parent card.
- ❌ Settings.json change.
- ❌ CSV schema change.
- ❌ Spec C work (SC column parity). Scheduled for post-P5b as Phase 6.

---

## 0.5. Step 0 — Gating prerequisites (DO NOT START WITHOUT THESE)

**P5b is gated on the trader explicitly signing off after the P5a verification window.** Verify all four before writing any code:

1. **P5a has shipped** — `git log --oneline | grep "P5a"` shows the two P5a commits in the main branch (not pushed; local-only is fine).
2. **`docs/ui-reskin-p5a-spec-back.md` exists** with §6.2 + §6.3 dump-parity gates marked clean (i.e., the snapshot output was confirmed bit-identical-to-shape against the legacy txtOutput).
3. **Trader sign-off recorded.** The user has explicitly stated something like "P5b ready" or "go for the deletion sweep" — not "P5a looks good." Distinct go/no-go signal.
4. **No open card-binding fix specs** that surfaced gaps during the P5a verification window. If the trader found anything missing from the cards during the verification window, those fixes must ship FIRST as discrete card-binding specs. P5b only proceeds when the trader is satisfied that the card grid covers every item the legacy display surfaced.

If any of these is missing, **stop** and surface back to the spec author.

---

## 1. What you inherit (from P5a)

After P5a, the codebase has the following state:

- `BuildPlaintextSnapshot(v, r, norms, cfg)` exists in `UI/MainForm_PlaintextSnapshot.vb` (or wherever P5a's implementer placed it — confirm via grep).
- `AnalysisOutputDump.Append` is called from `MainForm_Analysis.vb` (post-`UpdatePerformanceLabels`), passing the snapshot.
- `RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)` still runs at `MainForm_Analysis.vb:441` and writes to `txtOutput` via `MainForm_Render_Sections.vb`.
- `MainForm_Render_Header.vb` and `MainForm_Render_Sections.vb` still exist but have been stripped of the helpers (`FormatRR`, `BuildCalibrationReport`, `UpdateLogInfo`, the link handlers) which P5a migrated out.
- `lnkCalibCheck_LinkClicked` now uses `AnalysisReportForm`.
- The verification dump card still hosts `txtOutput` and shows legacy RTF output for parity.

P5b removes everything in the above list except `BuildPlaintextSnapshot` and the AnalysisReportForm migration.

---

## 2. Commit plan

One commit. The deletions are tightly coupled — removing `RenderOutput`'s caller without removing the legacy render files leaves orphan code, and vice versa. Batch them.

| # | Subject | Scope | LoC est. |
|---|---|---|---|
| 1 | `chore(ui-reskin): P5b — delete legacy RTF pipeline + verification dump card` | All deletions per §3. Helper consolidation. | ~150-200 deletions |

If commit 1 grows past ~300 LoC of touched files, split into:
- Commit 1a: delete `MainForm_Render_Header.vb` + `MainForm_Render_Sections.vb` + remove their callers
- Commit 1b: P/Invoke + constants + verification dump card + helper consolidation

Decision belongs to the implementation conversation based on actual diff size.

---

## 3. Deletion checklist

Walk this in order; each item's removal should leave the build clean.

### 3.1 Render files

```
git rm UI/MainForm_Render_Header.vb
git rm UI/MainForm_Render_Sections.vb
```

Confirm via grep that no other file references their public surface:

```
grep -rn "AppendRtf\|RenderOutput\b\|RenderOutputHeader\b\|SectionHeader\b\|Sub Divider\b" UI/ Core/
```

Should return zero matches in non-deleted files. If anything matches, P5a missed a helper migration — surface back to the spec author.

### 3.2 `MainForm_Analysis.vb` cleanup

Remove the six `AppendRtf(txtOutput, ...)` writes and the `RenderOutput` call:

| Line (pre-P5b) | Action |
|---|---|
| ~33: `AppendRtf(txtOutput, "Fetching data from Deribit...", ...)` | Delete. `btnAnalyze.Text = "Fetching..."` already signals state. |
| ~41: `AppendRtf(txtOutput, "ERROR: " & ex.Message & ...stackTrace, ...)` | Replace with `MessageBox.Show(...)`. See §3.2.1. |
| ~110: `AppendRtf(txtOutput, "ANALYSIS SKIPPED: {skipReason}", ...)` | Delete. Surfaced in SKIPPED panel (P4f). |
| ~111: `AppendRtf(txtOutput, "Skip count this session: {N}", ...)` | Delete. Surfaced in LOG sub-box (P4e). |
| ~112: `AppendRtf(txtOutput, "Engine continues — next auto-run cycle will retry.", ...)` | Delete. Surfaced as hint in SKIPPED panel (P4f). |
| ~441: `RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)` | Delete — **but see §3.2.2 below for the Kelly hoisting requirement.** |
| `txtOutput.Clear()` adjacent calls | Delete. |

After this, `MainForm_Analysis.vb` should have zero references to `txtOutput` and zero references to `RenderOutput`.

#### 3.2.2 Critical: hoist `CalcKellySizing` into `RunAnalysisAsync`

Per P5a spec-back §8: in the current P5a interim, `RenderOutputHeader` (legacy) calls `ScoringEngine.CalcKellySizing(v, atrStop, cfg)` and populates `v.KellyPWin` / `v.KellyF` / etc. as a side effect. `BindCardKelly` (at `MainForm_Analysis.vb:458`) runs **after** `RenderOutput` (line 441) and silently depends on this population.

`BuildPlaintextSnapshot` (P5a) also calls `CalcKellySizing` internally for its KELLY block — idempotent today because the call is deterministic and both renderers produce identical Kelly values. But after P5b deletes `RenderOutput`, the legacy call vanishes, and `BindCardKelly` reads `v.Kelly*` fields that were set by the snapshot builder... only if the snapshot builder ran first. The ordering currently is:

```
Line 441: RenderOutput → populates v.Kelly* via legacy CalcKellySizing
Line ~445: BindCardKelly → reads v.Kelly*
```

After deleting line 441, the population disappears. The card would render zeros for one cycle until the next snapshot run.

**Fix:** add an explicit `ScoringEngine.CalcKellySizing(verdict, atrStop, cfg)` call inside `RunAnalysisAsync` between `ScoringEngine.Calculate(...)` and `BindCardKelly(...)`. `atrStop` is whatever value the existing legacy site passed — verify by reading `RenderOutputHeader` before deleting it. Likely `verdict.StopLong` or `verdict.StopShort` depending on direction; trace through.

This is one line of inserted code. If the implementation conversation can't find a clean `atrStop` value to pass (because the legacy site computed it inline from `r.ATR * norms.ATRScaleFactor`), replicate that computation inline at the new call site.

Verification: post-P5b, run an analysis with a non-zero Kelly setup. Confirm the KELLY card renders the same values as it did pre-P5b.

#### 3.2.1 MessageBox for the ERROR path

```vb
' Replace:
AppendRtf(txtOutput, "ERROR: " & ex.Message & Environment.NewLine & ex.StackTrace, Theme.ACC_SHORT)

' With:
MessageBox.Show(
    "Analysis failed:" & Environment.NewLine & Environment.NewLine &
    ex.Message,
    "Analysis Error",
    MessageBoxButtons.OK,
    MessageBoxIcon.Error)
```

Trade-off captured in the P5a kickoff §8.3: blocks the auto-run timer briefly, but errors are rare and the trader explicitly wants to see them. If post-P5b live testing reveals this is too disruptive, switch to a transient `Pill` in the header strip — separate spec.

### 3.3 P/Invoke surface

Search and delete from `MainForm_Layout.vb` (and any MainForm partial that has them):

```
grep -n "DllImport\|EM_SET\|SendMessage\|RECT\b\|SetOutputMargins\|OnFormHandleCreated\|OUTPUT_CHARS\|OUTPUT_LINES\|SizeToContent" UI/
```

Each match falls into one of three buckets:

| Match | Action |
|---|---|
| `<DllImport("user32.dll")>` block (if specific to `EM_SET*` calls) | Delete the block. |
| `EM_SETMARGINS`, `EM_SETRECT`, `EM_SETRECTNP` constants | Delete. |
| `RECT` struct | Delete IF only used by `SetOutputMargins`. The `RECT` in `tools/resize-mainform.ps1` and `tools/screenshot-mainform.ps1` is a separate PowerShell type. |
| `SendMessage` overload | Delete IF only used by `SetOutputMargins`. |
| `SetOutputMargins()` method | Delete. |
| `OnFormHandleCreated` override (if only calls `SetOutputMargins`) | Delete. If it has any other body, surface back — don't delete blindly. |
| `OUTPUT_CHARS`, `OUTPUT_LINES` constants | Delete. |
| `SizeToContent()` method | Delete. Superseded by `ApplyInitialFormSize` in P4a. |

### 3.4 Verification dump card

In `MainForm_Layout.vb`:

```
grep -n "_cardVerificationDump\|ReparentVerificationDumpControls" UI/MainForm_Layout.vb
```

Each match either deletes (declaration + initialisation + layout row + reparent method) or is the call site in `BuildCardGridLayout` (also delete).

After removal, `_gridRoot` has one fewer row. The form's vertical extent shrinks by 400 px (the verification dump card's previous height). That's the expected layout shift; nothing else moves.

### 3.5 Helper consolidation

Per handover §6 outstanding decision:

```
grep -n "BuildPlainSectionHeader\b" UI/MainForm_Render_Cards.vb
```

Four call sites (KELLY SIZING, OI × CVD CROSS, VOLUME PROFILE, INDICATOR DETAILS). Each becomes:

```vb
' BEFORE
BuildPlainSectionHeader("KELLY SIZING")

' AFTER
MakeSectionHeader("KELLY SIZING")
```

Then delete the `BuildPlainSectionHeader` definition. `MakeSectionHeader` already produces identical output (11pt + `FG_SECONDARY`) post-`be7f64b` unification.

### 3.6 The locked `txtOutput` carve-out

**Do not touch `MainForm.Designer.vb`.** After all the above deletions, `txtOutput`:

- Still has its `Friend WithEvents txtOutput As System.Windows.Forms.RichTextBox` declaration in `Designer.vb`.
- Still gets instantiated by `InitializeComponent` (Designer-controlled).
- Has `.Visible = False` set in `MainForm_Layout.vb` (existing line; do not change).
- Is not added to any visible card or panel.
- Has zero writers (after §3.2).
- Has zero readers (after §3.1).

This is the locked end-state. If a future workflow wants the Designer declaration genuinely gone, that's a one-time "open in VS designer GUI, delete the control, let Designer.vb regenerate" pass — out of scope here.

---

## 4. Verification gate

### 4.1 Build clean

After all deletions:

```
dotnet build /c/Dev/DeribitVerdictEngine/DeribitVerdictEngine.sln
```

Must succeed with zero warnings, zero errors. Any compile error means an orphan reference is left behind — re-grep §3.1's pattern across the codebase.

### 4.2 Card grid bit-identical to P5a

Per handover §10.9 (self-screenshot default):

```
dotnet run --project DeribitVerdictEngine.vbproj    # run_in_background=true
sleep 10
pwsh tools/screenshot-mainform.ps1 verify/p5b-post-state.png
pwsh tools/click-mainform-button.ps1 ANALYZE
sleep 8
pwsh tools/screenshot-mainform.ps1 verify/p5b-post-analyze.png
```

Compare against `verify/p5a-c2-after-analyze.png` (the P5a final screenshot). Differences should be:
- ✅ The verification dump card region is gone — form is ~400 px shorter.
- ✅ Nothing else changes visually.

Card binding code was not touched. SCORE / VERDICT / LAST PRICE / ATR / STRUCTURAL / SIGNAL BREAKDOWN / OI × CVD / VOLUME PROFILE / KELLY / INDICATOR DETAILS / SETTINGS & TOOLS render bit-identical.

### 4.3 Dump-file shape unchanged

The dump file is sourced from `BuildPlaintextSnapshot` (set up in P5a). P5b doesn't touch that builder. The dump output should be identical to P5a:

```
# Snapshot one analysis worth of dump output.
cp bin/Debug/net8.0-windows/analysis_output_dump.md verify/p5b-postcommit.md

# Diff against a P5a baseline (from the P5a verification window — the trader
# kept a recent dump for this purpose).
diff verify/p5a-last-known-good.md verify/p5b-postcommit.md
```

Expected: only the timestamp + per-run market values differ. Anything else is a regression — either a missed helper migration in P5a or an accidental code touch in P5b.

### 4.4 Off-screen UIA verification

```
pwsh tools/inspect-mainform-tree.ps1 -Pattern "SETTINGS|TOOLS|last|KELLY|INDICATOR|verification"
```

The `verification` pattern should match **zero elements** post-P5b (verification dump card gone). All other patterns should match the P5a element count.

### 4.5 Calibration smoke

Click Calibration Readiness. `AnalysisReportForm` pops up. Same behaviour as P5a.

### 4.6 MessageBox error-path smoke (optional)

Force a `RunAnalysisAsync` exception (e.g., temporary `Throw New Exception("test")` at the top of the function). Confirm the `MessageBox.Show` fires. Revert before commit.

---

## 5. Out of scope

- ❌ `MainForm.Designer.vb` edits (txtOutput field declaration stays).
- ❌ `UI/Controls/*.vb` modifications.
- ❌ Scoring / indicator / engine code.
- ❌ Settings.json.
- ❌ CSV schema.
- ❌ Spec C work (post-P5b).
- ❌ Any new visual feature.
- ❌ Push to remote.

---

## 6. If you get stuck

1. **Build fails after deleting render files.** `grep -rn "RenderOutput\b\|AppendRtf\b\|MainForm_Render_Header\|MainForm_Render_Sections"` to find the orphan reference. Most likely a helper P5a didn't fully migrate. Surface back to the spec author with the specific reference.
2. **Form layout breaks after removing the verification dump card row.** Check `_gridRoot.RowCount` is decremented along with the `RowStyles.Add` removal. Off-by-one row-style count vs row count causes silent layout collapse.
3. **`OnFormHandleCreated` has more than just `SetOutputMargins` in its body.** Surface back — don't delete the override blindly. There may be other handle-creation work that doesn't relate to txtOutput.
4. **`MessageBox.Show` chokes when called from inside an Async function.** It shouldn't — `MessageBox` is safe inside async on the UI thread. If it does, wrap with `Me.Invoke(Sub() MessageBox.Show(...))`.

---

## 7. Reporting back

Spec-back doc: `docs/ui-reskin-p5b-spec-back.md`.

Specifically worth reporting if they happen:

1. **Final post-deletion LoC delta** — pure deletions should show ~150-300 LoC removed across the affected files. Net additions only from the MessageBox replacement (~10 LoC).
2. **P/Invoke surface actually present** — list what was found vs. the §3.3 expected list. The kickoff list is the proposal's expectation; the real codebase may have fewer items.
3. **`OnFormHandleCreated` resolution** — was it deletable cleanly, or did it have non-txtOutput body that you preserved?
4. **`MessageBox` behaviour in live testing** — does it block the auto-run timer disruptively, or is it fine? Trader feedback during the post-P5b verification window will be definitive.
5. **Helper consolidation completion** — confirm `BuildPlainSectionHeader` deleted and all four call sites converted.
6. **`MainForm.Designer.vb` confirmation** — explicitly state that the file was NOT touched. Post-P5b reviewers should be able to see this in the commit ledger.
7. **The reskin is done.** State this explicitly. After P5b, the UI reskin work is complete. Phase 6 (Spec C) is the next discrete piece of work, separate from the reskin arc.

---

## 8. Workflow reminders

- **Local commits only.** Do not push. User decides after live-data verification.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- Self-screenshot is the default verification path (handover §10.9). Use the `tools/` helpers.
- Engine code untouched. Settings.json untouched.
- The §4 paint carve-out is NOT invoked.
- This is the final reskin commit. The spec-back should explicitly note "UI reskin complete."

---

**End of kickoff.** This is a skeleton — it will be re-flowed against the P5a spec-back's actual findings when the trader signals readiness for P5b. Drop the final version into a fresh Opus 4.7 Medium conversation as the opening message at that time.
