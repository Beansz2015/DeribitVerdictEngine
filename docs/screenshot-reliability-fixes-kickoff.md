# Screenshot Reliability Fixes — Implementation Kickoff

**Phase:** Small follow-up to the just-shipped `screenshot-reliability` spec. Three bugs surfaced when the spec-author conversation tested commit 2 (the items the implementer flagged in `screenshot-reliability-spec-back.md` §3.3 as "not directly verified in this session").
**Spec source:** Spec-author test session 2026-05-27 (after `aaf12e9`). Findings reported in the test report following `screenshot-reliability-spec-back.md` review.
**Predecessor:** `screenshot-reliability-kickoff.md` shipped as `4a9781e` + `65dd6e7`. This kickoff fixes runtime defects in commit 2's `SaveFullFormScreenshot` + `screenshot-mainform-full.ps1`.
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-27
**Recommended model:** **Opus 4.7 Medium.** Three mechanical fixes, all with known root causes. No synthesis required.

---

## 0. What this phase is (and isn't)

**Is:** three targeted fixes to make `tools/screenshot-mainform-full.ps1` + the MainForm `Ctrl+Shift+S` hotkey actually work end-to-end:

1. **Bug 1 — `SetForegroundWindow` blocked on Windows 11.** The PowerShell helper calls `SetForegroundWindow(hwnd)` which returns `True` but doesn't actually transfer foreground (Windows 11 foreground-steal restriction). `SendKeys ^+s` then goes to the still-foreground terminal, never reaching MainForm. Marker file stays in place, helper times out after 10s. Fix: `AttachThreadInput` trick to bypass foreground-steal lockout.

2. **Bug 2 — `ComputeNaturalFormHeight` under-counts.** The captured PNG is ~2171 logical px tall, but the form's true natural extent is ~3360+ logical px (SETTINGS & TOOLS section header sits at Y=4235 *physical* during the temporary expansion, well below the bitmap's bottom edge). SETTINGS & TOOLS, verification dump card, and any rows below INDICATOR DETAILS are silently cropped out of the screenshot. Fix: use `_gridRoot.GetRowHeights()` (runtime-resolved pixel heights for every row regardless of SizeType) instead of summing only `SizeType.Absolute` rows.

3. **Bug 3 — `Me.Size = originalSize` restore in the Finally doesn't stick.** After hotkey fires, the form stays at the expanded size (UIA showed H=2028 physical / ~1622 logical after capture, vs. launch-time ~1352 logical). User has to manually resize back or restart the app. Fix: investigate during implementation; probably needs `Application.DoEvents()` after the restore + an explicit `PerformLayout`, OR a `MinimumSize = MinimumSize` no-op assignment to force layout re-validation.

**Isn't:**
- ❌ Any new feature. Pure defect repair.
- ❌ Scoring / indicator / engine change. Read-only.
- ❌ `UI/Controls/*.vb` modification. Paint carve-out NOT invoked.
- ❌ `MainForm.Designer.vb` edit.
- ❌ Settings.json change.
- ❌ CSV / dump schema change.
- ❌ Touching any of the other tools/ helpers — `select-mainform-radio.ps1` and `close-popup-window.ps1` and `PositionOnParentScreen` all verified working.

---

## 0.5. Step 0 — No new prerequisites

Both screenshot-reliability spec commits (`4a9781e`, `65dd6e7`) shipped and the docs spec-back commit (`aaf12e9`) landed. All four `tools/` helpers exist. The marker-file IPC mechanism is in place. The hotkey handler in `MainForm_Layout.vb:1277-1320` works correctly *when the keystroke arrives* — verified during the test session with an aggressive-foreground workaround.

This kickoff just patches three runtime defects in the existing implementation.

---

## 1. What you inherit

### 1.1 Test evidence (from the 2026-05-27 spec-author test session)

- **Bug 1 confirmed:** `tools/screenshot-mainform-full.ps1` ran to completion, MainForm hotkey handler never fired, marker file `bin/Debug/net8.0-windows/verify/.screenshot-target` stayed in place. When the SAME marker was placed and the SAME `SendKeys ^+s` was issued with `AttachThreadInput` foreground trick added, the handler fired immediately, marker was consumed, PNG saved. Repeatable.
- **Bug 2 confirmed:** with the AttachThreadInput workaround applied, `SaveFullFormScreenshot` produced `verify/p5-rel-full-after-analyze.png` at 1116×2171 with all cards through INDICATOR DETAILS visible. **SETTINGS & TOOLS card was not in the bitmap.** UIA inspection at the same moment showed SETTINGS & TOOLS section header at Y=4235 physical (~3364 logical), well past the bitmap's 2171 logical bottom edge.
- **Bug 3 confirmed:** UIA after capture: `MainForm: X=1083 Y=30 W=1674 H=2028` (physical). Launch-time MainForm was ~1116×1352 logical = ~1395×1690 physical at 1.25× DPI. The form is left at the expanded state, not the original.

Reference captures (gitignored):
- `verify/p5-rel-test.png` — earlier capture before realistic data, shows the under-counted natural extent
- `verify/p5-rel-full-after-analyze.png` — capture with In Long + analysis run, all cards through INDICATOR DETAILS visible, SETTINGS & TOOLS missing
- `verify/p5-rel-current-state.png` — PrintWindow capture taken AFTER the hotkey for comparison

### 1.2 Code under repair

| File | Lines | Current state |
|---|---|---|
| `tools/screenshot-mainform-full.ps1` | Foreground section ~50-75 | Plain `SetForegroundWindow($hwnd)` + `SendKeys` — fails silently on Windows 11. |
| `UI/MainForm_Layout.vb` | `SaveFullFormScreenshot` ~1300-1321 | Try/Finally with size + max-size restore that doesn't take effect. |
| `UI/MainForm_Layout.vb` | `ComputeNaturalFormHeight` ~1323-1330 | Sums only `SizeType.Absolute` rows; misses Percent / AutoSize / nested layout container rows. |

### 1.3 Code NOT touched

- `PositionOnParentScreen` helper in `MainForm_Layout.vb` — verified working (multi-monitor by user, single-monitor by spec author).
- `tools/select-mainform-radio.ps1` — verified working (selected "In Long", UIA confirmed `IsSelected=True`).
- `tools/close-popup-window.ps1` — verified working (spawned `AnalysisReportForm` via ANALYSIS REPORT button, closed cleanly; MainForm correctly excluded by `-MainFormTitleSubstring`).
- All `MessageBox.Show(Me, …)` overload migrations from commit 1 — verified compile clean, behaviour matches spec.
- Marker-file IPC mechanism in `MainForm_Layout.vb:1287-1298` (`ReadScreenshotTargetPath`) — verified working: BOM correctly stripped by `File.ReadAllText`, path read + marker deleted as designed.

---

## 2. Commit plan

Single commit. The three fixes are small, related, and each has clear scope.

| # | Subject | Files | LoC est. |
|---|---|---|---|
| 1 | `fix(tools): SaveFullFormScreenshot foreground + natural-height + size-restore` | `tools/screenshot-mainform-full.ps1`, `UI/MainForm_Layout.vb` | ~30-50 across two files |

If the implementer prefers a finer split for review clarity:
- **Commit 1a (tools):** PowerShell helper foreground fix.
- **Commit 1b (VB):** `ComputeNaturalFormHeight` + Finally restore fix.

Either path is fine. Single-commit is recommended for review simplicity since both fixes serve the same end-to-end workflow.

### 2.1 Ship list

- ✅ `tools/screenshot-mainform-full.ps1` — replace `[FG]::SetForegroundWindow($hwnd)` block with `AttachThreadInput` foreground steal per §3.1.
- ✅ `UI/MainForm_Layout.vb` `ComputeNaturalFormHeight` — switch from `_gridRoot.RowStyles` loop to `_gridRoot.GetRowHeights()` enumeration per §3.2.
- ✅ `UI/MainForm_Layout.vb` `SaveFullFormScreenshot` Finally — add layout-revalidation pass per §3.3.

---

## 3. Per-bug fix details

### 3.1 Bug 1 fix — `AttachThreadInput` foreground steal

Current code in `tools/screenshot-mainform-full.ps1` (around line 60):

```powershell
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WFG {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@
[WFG]::SetForegroundWindow([IntPtr]$form.Current.NativeWindowHandle) | Out-Null
Start-Sleep -Milliseconds 200

# Send Ctrl+Shift+S.
[System.Windows.Forms.SendKeys]::SendWait("^+s")
```

Replace with (verified working in the test session — produces correct foreground transfer, handler fires reliably):

```powershell
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WFG {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
}
"@

# Windows 11 blocks SetForegroundWindow under foreground-steal restrictions.
# Attaching to the current foreground's input queue bypasses the lockout.
$hwnd = [IntPtr]$form.Current.NativeWindowHandle
$fgHwnd   = [WFG]::GetForegroundWindow()
$fgThread = [WFG]::GetWindowThreadProcessId($fgHwnd, [IntPtr]::Zero)
$curThread = [WFG]::GetCurrentThreadId()
[WFG]::AttachThreadInput($curThread, $fgThread, $true) | Out-Null
[WFG]::ShowWindow($hwnd, 9) | Out-Null    # SW_RESTORE — undo any minimize
[WFG]::BringWindowToTop($hwnd) | Out-Null
[WFG]::SetForegroundWindow($hwnd) | Out-Null
[WFG]::AttachThreadInput($curThread, $fgThread, $false) | Out-Null
Start-Sleep -Milliseconds 500

# Send Ctrl+Shift+S.
[System.Windows.Forms.SendKeys]::SendWait("^+s")
```

Differences:
- Adds five new P/Invoke imports.
- Performs `AttachThreadInput → ShowWindow(SW_RESTORE) → BringWindowToTop → SetForegroundWindow → AttachThreadInput(false)` sequence.
- `Start-Sleep` bumped 200ms → 500ms to give Windows time to actually finish the foreground transition before keys are sent.

The same fix pattern applies to any future helper that needs to drive MainForm via SendKeys. If desired, factor the foreground-steal block into a small helper (e.g., `tools/lib/foreground-steal.ps1`) — out of scope here, just note for future tooling.

### 3.2 Bug 2 fix — use `GetRowHeights()` for true natural extent

Current code in `UI/MainForm_Layout.vb` around line 1323:

```vb
Private Function ComputeNaturalFormHeight() As Integer
    Dim chromeH As Integer = Me.Height - Me.ClientSize.Height
    Dim totalRowH As Integer = 0
    For Each rs As RowStyle In _gridRoot.RowStyles
        If rs.SizeType = SizeType.Absolute Then totalRowH += CInt(rs.Height)
    Next
    Return totalRowH + _gridRoot.Padding.Top + _gridRoot.Padding.Bottom + chromeH + 16
End Function
```

Replace with:

```vb
''' <summary>
''' Computes the form's natural full height so SaveFullFormScreenshot can
''' temporarily expand the form to capture every card via DrawToBitmap.
'''
''' Uses TableLayoutPanel.GetRowHeights() which returns the runtime-resolved
''' pixel height of each row regardless of SizeType (Absolute / Percent /
''' AutoSize). The previous implementation summed only SizeType.Absolute
''' rows, which under-counted any Percent / AutoSize rows and dropped the
''' SETTINGS & TOOLS card from captures (it sits below the under-counted
''' bound).
''' </summary>
Private Function ComputeNaturalFormHeight() As Integer
    Dim chromeH As Integer = Me.Height - Me.ClientSize.Height
    Dim totalRowH As Integer = 0
    For Each h As Integer In _gridRoot.GetRowHeights()
        totalRowH += h
    Next
    Return totalRowH + _gridRoot.Padding.Top + _gridRoot.Padding.Bottom + chromeH + 16
End Function
```

`TableLayoutPanel.GetRowHeights()` returns `Integer()` with one entry per row, each in pixels at the current layout state. Captures every row regardless of `SizeType`.

**Important verification:** call `_gridRoot.PerformLayout()` BEFORE `GetRowHeights()` if the form has been resized in the interim. The current `SaveFullFormScreenshot` already calls `Me.PerformLayout()` after the `Me.Size` assignment but BEFORE this helper would be invoked. Confirm via a single screenshot post-fix: the bitmap should be ~3360+ logical px tall (covering all cards through SETTINGS & TOOLS) instead of 2171.

**Edge case:** when `GetRowHeights()` is called before the form has finished its initial layout (e.g., during the constructor), it returns the design-time heights from `RowStyles`. That's actually fine for our use case since the function is only called inside `SaveFullFormScreenshot`, well after construction. Don't bother adding a guard.

### 3.3 Bug 3 fix — Finally block restore actually sticks

Current code in `UI/MainForm_Layout.vb:1300-1321`:

```vb
Friend Sub SaveFullFormScreenshot(outPath As String)
    Dim originalSize = Me.Size
    Dim originalMax  = Me.MaximumSize
    Try
        Me.MaximumSize = Size.Empty
        Me.Size        = New Size(Me.Width, ComputeNaturalFormHeight())
        Me.PerformLayout()
        Application.DoEvents()
        Dim dir = Path.GetDirectoryName(outPath)
        If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
            Directory.CreateDirectory(dir)
        End If
        Using bmp As New Bitmap(Me.Width, Me.Height)
            Me.DrawToBitmap(bmp, New Rectangle(0, 0, Me.Width, Me.Height))
            bmp.Save(outPath, Imaging.ImageFormat.Png)
        End Using
    Finally
        Me.Size        = originalSize
        Me.MaximumSize = originalMax
        Me.PerformLayout()
    End Try
End Sub
```

The test session confirmed that after this sub returns, `Me.Size` is STILL at the expanded value (not `originalSize`). The Finally block runs (no exception suppressed the path) but the size restore doesn't visibly take effect.

Three hypotheses, fix accordingly:

**Hypothesis A — restore needs DoEvents after the relayout.** Without `DoEvents`, the WM_SIZE / WM_WINDOWPOSCHANGED messages may not pump before whatever observer reads `Me.Size`. Fix:

```vb
Finally
    Me.MaximumSize = originalMax    ' restore max FIRST so subsequent Size assignment isn't clamped wrong
    Me.Size        = originalSize
    Me.PerformLayout()
    Application.DoEvents()
End Try
```

Order matters — restore `MaximumSize` first so `Me.Size = originalSize` isn't constrained by a stale max.

**Hypothesis B — `ApplyInitialFormSize` or similar re-runs on resize.** If the form has a `Resize` handler or layout-engine override that re-clamps to working area, the restore is fighting that. Search `MainForm_Layout.vb` for `Resize`-related event handlers; confirm whether any re-clamp logic exists.

**Hypothesis C — `Me.Size` assignment respects a `MinimumSize` that exceeds `originalSize`.** If `MinimumSize` is e.g. `(950, 1700)` and `originalSize` is `(1116, 1352)`, the assignment effectively gets clamped to `(1116, 1700)`. Check current `MinimumSize` value; reduce or temporarily clear it during the restore.

**Recommended implementation order:**

1. Try Hypothesis A first (cheapest — just reorder + DoEvents).
2. Build, screenshot, verify via UIA that `Me.Size` matches `originalSize` after the call.
3. If A doesn't fix: inspect via `Debug.WriteLine($"After restore: Size=({Me.Width},{Me.Height})")` to confirm what value the assignment actually produced.
4. If the assignment is being clamped: investigate Hypothesis B (event handler) or C (MinimumSize).

After fixing, the form should be at its launch-time size after every hotkey capture, with NO visible aftermath.

---

## 4. Verification gate

### 4.1 Build clean

`dotnet build` must be clean after the changes. Zero warnings, zero errors.

### 4.2 Self-screenshot end-to-end (the load-bearing test)

Per handover §10.9 + tools/README.md workflow:

```
dotnet run --project DeribitVerdictEngine.vbproj    # background
sleep 10

# Trigger analysis so the form has real card content.
pwsh tools/click-mainform-button.ps1 "ANALYZE"
sleep 8

# THE TEST: full-form capture should work first try, no aggressive-foreground
# workaround needed.
pwsh tools/screenshot-mainform-full.ps1 verify/post-fix-full.png

# Expected output:
#   "Saved C:\Dev\DeribitVerdictEngine\verify\post-fix-full.png (NNNNNN bytes)"
# Exit code 0.
```

Then read the PNG via the `Read` tool. Expected:
- PNG dimensions ~1116 × ~3300+ logical px (taller than the old ~2171 buggy output)
- ALL cards visible: header strip + perf strip + SCORE + VERDICT + LAST PRICE + ATR ENTRY LEVELS + STRUCTURAL × 2 + SIGNAL BREAKDOWN + OI × CVD CROSS + VOLUME PROFILE + KELLY SIZING + INDICATOR DETAILS + verification dump card (with txtOutput legacy content) + **SETTINGS & TOOLS card at the bottom (was missing pre-fix)**

### 4.3 Form size restore — UIA check

After the capture, before killing the app:

```
pwsh tools/inspect-mainform-tree.ps1 -Pattern "^Deribit Verdict Engine"
```

The "Form bounding rect" line should match the form's launch-time size. If MainForm logical launch size is ~1116 × ~1352, then physical at 1.25× DPI is ~1395 × ~1690. The reported `W` and `H` should match (within a few px).

Pre-fix value (broken): `W=1674 H=2028` (form left at expanded size).
Post-fix expectation: `W=1395 H=~1690` (or whatever the form's natural launch size is on the implementation conversation's display).

### 4.4 Repeatability — second hotkey should also restore cleanly

After the first capture restores cleanly, fire the hotkey a second time:

```
pwsh tools/screenshot-mainform-full.ps1 verify/post-fix-full-2.png
```

Re-check UIA: form should AGAIN be at launch size. If after a single capture the restore works but a second capture leaves the form expanded, there's a state-accumulation bug.

### 4.5 Negative test — Aggressive-foreground workaround should still work

The `AttachThreadInput` block in §3.1 is additive. If someone wanted to run the OLD plain-`SetForegroundWindow` from another script, both paths should still work. Not a hard requirement, just confirm no regression in basic Win32 calls.

---

## 5. Out of scope

- ❌ Touching `select-mainform-radio.ps1`, `close-popup-window.ps1`, `screenshot-mainform.ps1`, `click-mainform-button.ps1`, `inspect-mainform-tree.ps1`, `resize-mainform.ps1`. All verified working as-is.
- ❌ Touching `PositionOnParentScreen` or its 5-7 popup call sites. Verified working.
- ❌ The marker-file IPC mechanism in `ReadScreenshotTargetPath`. Verified working.
- ❌ Touching `UI/Controls/*.vb`. Paint carve-out NOT invoked.
- ❌ `MainForm.Designer.vb` edit.
- ❌ Scoring / indicator / engine / settings.json / CSV / dump schema.
- ❌ Factoring foreground-steal into a shared `tools/lib/*.ps1` helper. Worth doing later if multiple tools need it; not now.
- ❌ The P5-test harness's own use of these helpers. Separate kickoff already drafted (`docs/ui-reskin-p5-test-harness-kickoff.md`).
- ❌ Push to remote.

---

## 6. If you get stuck

1. **`AttachThreadInput` returns False.** That happens if the foreground thread has already exited or doesn't exist. Fall back to plain `SetForegroundWindow` + a longer sleep, or use `Microsoft.VisualBasic.Interaction.AppActivate($pid)` as an alternate path. Both work less reliably than `AttachThreadInput` but still better than nothing.

2. **`GetRowHeights()` returns all zeros.** That happens if called before the form's first layout pass. The fix in §3.2 calls `GetRowHeights` after `Me.PerformLayout()` has run inside `SaveFullFormScreenshot`, so this shouldn't trigger. If it does, add `_gridRoot.PerformLayout()` explicitly before reading heights.

3. **After §3.3 Hypothesis A, form is still left expanded.** Move to Hypothesis B or C. Debug via `Debug.WriteLine` inside the Finally to confirm what value `Me.Size` actually holds at each step.

4. **DrawToBitmap output still misses the SETTINGS & TOOLS card after §3.2 fix.** Either the `Me.Size` assignment to `ComputeNaturalFormHeight`-derived value didn't actually grow the form (Hypothesis C — `MaximumSize` not properly cleared, or some other constraint), or `_gridRoot` itself is constrained somehow. Inspect via UIA during the capture window; `Me.Width × Me.Height` should match the bitmap dimensions.

5. **Form flicker during capture is noticeable.** Acceptable trade-off (dev-only workflow). If actually disruptive, wrap the try/finally with `Me.SuspendLayout()` / `Me.ResumeLayout(False)` to mask the visible reflow.

---

## 7. Reporting back

Spec-back doc: `docs/screenshot-reliability-fixes-spec-back.md`. Same structure as past spec-backs.

Specifically worth reporting:

1. **Final post-fix PNG dimensions.** Should be ~1116 × ~3300+ logical px. Compare against the pre-fix ~1116 × 2171 to confirm the natural extent is now correctly computed.
2. **SETTINGS & TOOLS card visibility in post-fix capture.** Read the PNG, confirm the bottom of the form shows the SETTINGS & TOOLS card with LOG sub-box + AUTO-RUN sub-box + ANALYSIS REPORT CTA + TOOLS sub-box.
3. **Form size restore — which hypothesis fixed it.** If A worked, note that. If you had to go to B or C, describe what you found.
4. **Number of test runs to confirm restore is stable.** The §4.4 second-capture test should produce identical form state to the first capture. If it took more than 2 captures to surface a state-accumulation bug, that's worth noting.
5. **Foreground-steal helper extraction.** If you factored the block into a shared `tools/lib/foreground-steal.ps1` despite the out-of-scope note, mention it. Otherwise confirm you didn't.

---

## 8. Workflow reminders

- **Local commits only.** Do not push.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- Self-screenshot is the default verification path per handover §10.9. After the fixes land, `tools/screenshot-mainform-full.ps1` becomes a primary verification tool — exercise it during the verification gate.
- Engine code untouched.
- The §4 paint carve-out is NOT invoked.

---

**End of kickoff.** Drop into a fresh Opus 4.7 Medium conversation as the opening message. Small spec — single commit, ~30-50 LoC, well-scoped. After this lands, the P5-test harness kickoff becomes actionable on a reliable screenshot pipeline.
