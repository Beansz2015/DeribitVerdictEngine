# Screenshot Reliability + Verification Tooling — Implementation Kickoff

**Phase:** Dev-tooling polish. Slots into the P5a verification window, **before P5-test**, so the harness's 50-case loop runs on reliable plumbing.
**Spec source:** Spec-author feasibility study 2026-05-26 (in P5a sign-off conversation). User committed to the bundled scope: popup positioning + full-form capture + two harness-supporting UIA helpers.
**Predecessor:** P5a shipped (`bcfdfd7`, `f707165`). Spec-back `docs/ui-reskin-p5a-spec-back.md` §4 flagged the popup-position issue.
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-26
**Recommended model:** **Opus 4.7 Medium.** Mechanical fixes + small new helpers. No synthesis.

---

## 0. What this phase is (and isn't)

**Is:** four small, independent reliability fixes for the self-screenshot verification workflow:

1. **Popup positioning:** popups (`AnalysisReportForm`, `TweakSettingsForm`, `OutputDumpSettingsForm`, `RoundStatsForm`, `MessageBox.Show`) currently land at `CenterScreen` of the **primary monitor** or at stale persisted positions. On dual-monitor setups they spawn off-screen relative to the MainForm. Fix: position popups on the same screen as MainForm.

2. **Full-form capture:** `tools/screenshot-mainform.ps1` uses Win32 `PrintWindow`, which captures visible pixels only. When the form is taller than the display working area (anything <2160 vertical), the bottom cards never appear in the PNG. Fix: hotkey on MainForm that uses `Form.DrawToBitmap` (renders all content regardless of screen visibility) + companion PowerShell helper.

3. **Radio button UIA helper:** the harness needs to toggle `posState` (None/InLong/InShort) for Hold/Exit test cases. P5a spec-back §5 noted that radio buttons need `SelectionItemPattern`, not `InvokePattern` (which `click-mainform-button.ps1` uses). New helper.

4. **Window-close helper:** defensive — if any popup spawns during a harness run (e.g., the P5b MessageBox replacement for the ERROR path fires during a force-skip case), the harness needs to dismiss it to avoid stalling the loop. New helper.

**Isn't:**
- ❌ Engine / scoring / indicator change. Read-only.
- ❌ `UI/Controls/*.vb` modification. Paint carve-out NOT invoked. None of the four fixes touch P3 controls.
- ❌ `MainForm.Designer.vb` edit.
- ❌ Settings.json change.
- ❌ CSV / dump schema change.
- ❌ P5-test harness implementation (separate kickoff, runs after this).
- ❌ P5b deletion sweep work.
- ❌ Fix for the SCORE arc 300ms fill animation interfering with harness screenshots (deferred — harness can wait 400ms per case instead, no P3 modification needed).
- ❌ Fix for initial form position on launch (deferred — harness can call `resize-mainform.ps1` at the start of its run).
- ❌ Standalone diff-files PowerShell helper (deferred — harness includes its own ~30 LoC diff per `ui-reskin-p5-test-harness-kickoff.md` §3.3).

---

## 1. What you inherit

### 1.1 Popup forms in the codebase

```
analysis/AnalysisReportForm.vb       — Analysis Report CTA + Calibration Readiness (post-P5a)
UI/TweakSettingsForm.vb              — Tweak Settings link
UI/OutputDumpSettingsForm.vb         — Output Dump cog
UI/RoundStatsForm.vb                 — performance breakdown
```

All four have `Me.StartPosition = FormStartPosition.CenterScreen` in their constructors. All four exhibit the same off-screen-on-secondary-monitor symptom.

`MessageBox.Show` calls in the codebase — `grep -n "MessageBox\.Show" UI/ analysis/` to find them. Post-P5b adds one more (the ERROR path replacement at `MainForm_Analysis.vb:41`).

### 1.2 Existing self-screenshot helpers

```
tools/screenshot-mainform.ps1         — Win32 PrintWindow capture (current, visible-only)
tools/click-mainform-button.ps1       — UIA InvokePattern on buttons (substring name match)
tools/inspect-mainform-tree.ps1       — UIA element tree dump
tools/resize-mainform.ps1             — SetWindowPos wrapper
tools/README.md                       — workflow doc
```

Two new helpers ship from this kickoff:

```
tools/screenshot-mainform-full.ps1    — sends Ctrl+Shift+S, reads saved PNG
tools/select-mainform-radio.ps1       — UIA SelectionItemPattern on radio buttons
tools/close-popup-window.ps1          — UIA WindowPattern.Close on title-matched popups
```

`tools/README.md` updates to document them.

### 1.3 MainForm hooks needed

- New hotkey handler: `Ctrl+Shift+S` → save full-form screenshot
- New helper method: `Friend Sub PositionOnParentScreen(child As Form, parent As Form)` in `MainForm_Layout.vb` (or any partial)
- New helper method: `Friend Sub SaveFullFormScreenshot(outPath As String)` on MainForm

---

## 2. Commit plan

Two commits. Each compiles cleanly and produces a working app. Verification per commit per §5.

| # | Subject | Scope | LoC est. |
|---|---|---|---|
| 1 | `fix(ui): popup forms position on parent monitor` | `PositionOnParentScreen` helper + call sites at every popup spawn (~5-7 sites). `MessageBox.Show` calls migrate to the `(owner, …)` overload. | ~60-80 |
| 2 | `feat(tools): full-form screenshot + UIA radio/popup-close helpers` | `Ctrl+Shift+S` hotkey + `SaveFullFormScreenshot` on MainForm + three new PowerShell scripts + `tools/README.md` update. | ~150-200 |

Two commits because (1) is application-code-only and (2) is dev-tooling-only — clean review boundary.

### 2.1 Per-commit ship / skip

**Commit 1 (popup positioning):**
- ✅ `Friend Shared Sub PositionOnParentScreen(child As Form, parent As Form)` in `MainForm_Layout.vb` (or wherever the popup-launch handlers live post-P5a)
- ✅ Call sites updated: `lnkAnalysisReport_LinkClicked`, `lnkCalibCheck_LinkClicked`, `lnkTweakSettings_LinkClicked`, `lnkOutputDump_LinkClicked` (which spawns `OutputDumpSettingsForm` indirectly), `lnkOutputDumpSettings_LinkClicked`, and any others surfaced via grep
- ✅ All `MessageBox.Show(text, ...)` calls in `UI/` migrate to `MessageBox.Show(Me, text, ...)` or `MessageBox.Show(parentForm, text, ...)`
- ⏸ Don't change the forms' own `StartPosition` constructor lines — the runtime override via `PositionOnParentScreen` takes precedence; leaving the constructor at `CenterScreen` keeps the form usable standalone (e.g., if a future test spawns it without a parent)

**Commit 2 (capture + UIA helpers):**
- ✅ MainForm hotkey: `Ctrl+Shift+S` triggers `SaveFullFormScreenshot`
- ✅ `Friend Sub SaveFullFormScreenshot(outPath As String)` method on MainForm
- ✅ Output path mechanism: PowerShell helper writes target path to `verify/.screenshot-target`, hotkey handler reads it, deletes the file after capture
- ✅ `tools/screenshot-mainform-full.ps1` — writes target path, sends Ctrl+Shift+S, polls for output PNG, returns
- ✅ `tools/select-mainform-radio.ps1` — UIA SelectionItemPattern on a radio matched by name substring
- ✅ `tools/close-popup-window.ps1` — UIA WindowPattern.Close on a window matched by title substring (excludes the MainForm itself)
- ✅ `tools/README.md` — add the three new helpers to the script index + update the workflow loop to mention the full-form capture option

---

## 3. Implementation details

### 3.1 `PositionOnParentScreen` helper

Single method, ~10 LoC. Place in `MainForm_Layout.vb` since that file already hosts the link-click handlers post-P5a:

```vb
''' <summary>
''' Position a child form centered on whichever monitor the parent form
''' currently occupies. Survives multi-monitor / non-primary-display
''' layouts. Sets StartPosition = Manual so any subsequent layout code
''' doesn't override.
''' </summary>
Friend Shared Sub PositionOnParentScreen(child As Form, parent As Form)
    If child Is Nothing OrElse parent Is Nothing Then Return
    Dim host = Screen.FromControl(parent)
    child.StartPosition = FormStartPosition.Manual
    child.Location = New Point(
        host.WorkingArea.X + (host.WorkingArea.Width  - child.Width)  \ 2,
        host.WorkingArea.Y + (host.WorkingArea.Height - child.Height) \ 2)
End Sub
```

Call pattern at every popup site:

```vb
' BEFORE
Dim frm As New AnalysisReportForm(md, path)
frm.Show()

' AFTER
Dim frm As New AnalysisReportForm(md, path)
PositionOnParentScreen(frm, Me)
frm.Show()
```

`Screen.FromControl` returns the monitor the bulk of the parent occupies. Works correctly even when MainForm has been moved by the user post-launch.

### 3.2 `MessageBox.Show` migration

Grep first:

```
grep -n "MessageBox\.Show" UI/ analysis/
```

For each call site:

```vb
' BEFORE (no owner specified — defaults to top-level window of calling thread)
MessageBox.Show("error text", "Title", MessageBoxButtons.OK, MessageBoxIcon.Error)

' AFTER (owner specified — inherits parent's screen)
MessageBox.Show(Me, "error text", "Title", MessageBoxButtons.OK, MessageBoxIcon.Error)
```

`Me` resolves to MainForm inside its own partials; pass the explicit form ref otherwise. Subtle benefit: modal-relative-to-owner means the user can't accidentally interact with MainForm while the dialog is open (existing behaviour, just made explicit and screen-aware).

### 3.3 `SaveFullFormScreenshot` + hotkey

In `MainForm_Layout.vb` constructor — enable `KeyPreview` so the form sees keystrokes before child controls:

```vb
Me.KeyPreview = True
```

Hotkey handler:

```vb
Private Sub OnFormKeyDown(sender As Object, e As KeyEventArgs) Handles MyBase.KeyDown
    If e.Control AndAlso e.Shift AndAlso e.KeyCode = Keys.S Then
        Dim targetPath As String = ReadScreenshotTargetPath()
        If Not String.IsNullOrEmpty(targetPath) Then
            SaveFullFormScreenshot(targetPath)
            e.Handled = True
        End If
    End If
End Sub

''' <summary>
''' Reads the PowerShell-set screenshot target path from verify/.screenshot-target,
''' then deletes the marker file. Returns Nothing if no path is set
''' (so the hotkey is a no-op when no helper is waiting).
''' </summary>
Private Function ReadScreenshotTargetPath() As String
    Dim markerPath As String = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "verify", ".screenshot-target")
    If Not File.Exists(markerPath) Then Return Nothing
    Try
        Dim path As String = File.ReadAllText(markerPath).Trim()
        File.Delete(markerPath)
        Return path
    Catch
        Return Nothing
    End Try
End Function

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

''' <summary>
''' Computes the form's natural full height by summing _gridRoot's row
''' heights plus chrome. Used for full-form screenshot capture so off-screen
''' cards (when display working area is smaller than form content) get
''' rendered to the bitmap.
''' </summary>
Private Function ComputeNaturalFormHeight() As Integer
    Dim chromeH As Integer = Me.Height - Me.ClientSize.Height
    Dim totalRowH As Integer = 0
    For Each rs As RowStyle In _gridRoot.RowStyles
        If rs.SizeType = SizeType.Absolute Then totalRowH += CInt(rs.Height)
    Next
    Return totalRowH + _gridRoot.Padding.Top + _gridRoot.Padding.Bottom + chromeH + 16
End Function
```

The marker-file mechanism (`verify/.screenshot-target`) lets PowerShell choose the output path without needing inter-process arguments. Simple, no IPC complexity, gitignored anyway.

The temporary form-resize causes a brief visual flash (~100ms). Acceptable for dev-only workflow. If it bothers anyone, future polish could wrap with `Me.SuspendLayout()` and a temporary `BackColor = BG_BASE` to mask the resize.

### 3.4 `tools/screenshot-mainform-full.ps1`

```powershell
# Sends Ctrl+Shift+S to the running DeribitVerdictEngine MainForm and
# polls for the saved PNG at the requested path. Captures the FULL form
# regardless of display height (uses MainForm.DrawToBitmap internally,
# which renders all content including off-screen cards).
#
# Usage: pwsh tools/screenshot-mainform-full.ps1 [output-path]

param([string]$OutputPath = "screenshot-full.png")

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms

# Resolve to absolute path so MainForm's hotkey handler doesn't get confused
# by working-directory drift.
$absOut = [System.IO.Path]::GetFullPath($OutputPath)
$markerPath = Join-Path (Split-Path $absOut -Parent) ".screenshot-target"

# Create the marker directory if needed.
$markerDir = Split-Path $markerPath -Parent
if (-not (Test-Path $markerDir)) {
    New-Item -ItemType Directory -Path $markerDir -Force | Out-Null
}

# Write the target path. The MainForm hotkey reads + deletes this.
# Use the bin/.../verify/.screenshot-target location since the running
# app's working dir is bin/Debug/net8.0-windows/.
$binMarker = Join-Path (Get-Location) "bin/Debug/net8.0-windows/verify/.screenshot-target"
$binMarkerDir = Split-Path $binMarker -Parent
if (-not (Test-Path $binMarkerDir)) {
    New-Item -ItemType Directory -Path $binMarkerDir -Force | Out-Null
}
$absOut | Out-File -FilePath $binMarker -Encoding utf8 -NoNewline

# Find and foreground the MainForm so SendKeys hits it.
$root = [System.Windows.Automation.AutomationElement]::RootElement
$forms = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)
$form = $null
foreach ($f in $forms) {
    $name = $f.Current.Name
    if ($name -and $name.IndexOf("Deribit Verdict Engine", [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $form = $f
        break
    }
}
if ($form -eq $null) { Write-Error "MainForm not found"; exit 1 }

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

# Poll for the PNG (the hotkey handler takes ~200-500ms including the
# temporary form-resize-and-redraw).
$deadline = (Get-Date).AddSeconds(10)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $absOut) {
        $size = (Get-Item $absOut).Length
        if ($size -gt 0) {
            Start-Sleep -Milliseconds 200   # allow Save to fully flush
            Write-Host "Saved $absOut ($size bytes)"
            exit 0
        }
    }
    Start-Sleep -Milliseconds 100
}
Write-Error "Timed out waiting for $absOut"
exit 2
```

### 3.5 `tools/select-mainform-radio.ps1`

Companion to `click-mainform-button.ps1`. Same fuzzy-match-by-name pattern but uses `SelectionItemPattern.Select` on a RadioButton:

```powershell
# Selects a radio button inside the MainForm by name substring match
# (UI Automation SelectionItemPattern). Used by the P5-test harness to
# toggle posState (No Position / In Long / In Short) for Hold/Exit cases.
#
# Usage:
#   pwsh tools/select-mainform-radio.ps1 "In Long"
#   pwsh tools/select-mainform-radio.ps1 "No Position"

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$NamePattern,
    [string]$WindowTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$forms = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)
$form = $null
foreach ($f in $forms) {
    $name = $f.Current.Name
    if ($name -and $name.IndexOf($WindowTitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $form = $f
        break
    }
}
if ($form -eq $null) { Write-Error "MainForm not found"; exit 1 }

$radios = $form.FindAll(
    [System.Windows.Automation.TreeScope]::Descendants,
    (New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
        [System.Windows.Automation.ControlType]::RadioButton)))

foreach ($r in $radios) {
    $name = $r.Current.Name
    if (-not $name) { continue }
    if ($name.IndexOf($NamePattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        try {
            $selPattern = $r.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
            $selPattern.Select()
            Write-Host "Selected: '$name'"
            exit 0
        } catch {
            Write-Error "Radio '$name' does not support SelectionItemPattern: $_"
            exit 3
        }
    }
}

Write-Error "No radio matched '$NamePattern'. Available radios:"
foreach ($r in $radios) {
    $n = $r.Current.Name
    if ($n) { Write-Host "  - '$n'" }
}
exit 2
```

### 3.6 `tools/close-popup-window.ps1`

Defensive helper. Closes any non-MainForm window matched by title substring. Useful during the P5-test harness in case an unexpected popup (MessageBox after P5b, or an `AnalysisReportForm` someone leaves open) blocks the loop:

```powershell
# Closes any top-level window matched by title substring, EXCEPT the
# MainForm itself. Defensive helper for the P5-test harness.
#
# Usage:
#   pwsh tools/close-popup-window.ps1 "Analysis Report"
#   pwsh tools/close-popup-window.ps1 "Error"

param(
    [Parameter(Mandatory=$true, Position=0)]
    [string]$TitleSubstring,
    [string]$MainFormTitleSubstring = "Deribit Verdict Engine"
)

Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes

$root = [System.Windows.Automation.AutomationElement]::RootElement
$candidates = $root.FindAll(
    [System.Windows.Automation.TreeScope]::Children,
    [System.Windows.Automation.Condition]::TrueCondition)

$closedCount = 0
foreach ($w in $candidates) {
    $name = $w.Current.Name
    if (-not $name) { continue }
    # Skip the MainForm itself.
    if ($name.IndexOf($MainFormTitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) { continue }
    if ($name.IndexOf($TitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        try {
            $win = $w.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
            $win.Close()
            Write-Host "Closed: '$name'"
            $closedCount++
        } catch {
            Write-Warning "Could not close '$name': $_"
        }
    }
}

if ($closedCount -eq 0) {
    Write-Host "No windows matched '$TitleSubstring' (nothing to close)"
}
exit 0
```

### 3.7 `tools/README.md` update

Add three new entries to the script index table:

```markdown
| `screenshot-mainform-full.ps1` | Full-form capture via MainForm.DrawToBitmap. Works regardless of display height. Useful when the form exceeds the working area. |
| `select-mainform-radio.ps1` | UIA SelectionItemPattern on a radio button matched by name substring. Toggles posState for P5-test Hold/Exit cases. |
| `close-popup-window.ps1` | UIA WindowPattern.Close on a non-MainForm window matched by title substring. Defensive cleanup for harness loops. |
```

And extend the typical loop section to mention `screenshot-mainform-full.ps1` as the recommended capture when the form may exceed display height.

---

## 4. Implementation surface summary

| File | Change | Specific edit |
|---|---|---|
| `UI/MainForm_Layout.vb` (or wherever popup handlers live post-P5a) | Add `PositionOnParentScreen` helper. Update 5-7 popup spawn sites. Add `Me.KeyPreview = True` in constructor. Add hotkey handler. Add `SaveFullFormScreenshot` + `ReadScreenshotTargetPath` + `ComputeNaturalFormHeight`. | ~150 LoC |
| `UI/MainForm_Analysis.vb` (and any other `MessageBox.Show` site) | Migrate calls to `MessageBox.Show(Me, ...)` overload. | ~5 LoC (more if P5b's MessageBox replacement already shipped) |
| `tools/screenshot-mainform-full.ps1` | New file. | ~70 LoC |
| `tools/select-mainform-radio.ps1` | New file. | ~50 LoC |
| `tools/close-popup-window.ps1` | New file. | ~40 LoC |
| `tools/README.md` | Document the three new helpers + update workflow loop. | ~20 LoC |

Total: ~330 LoC, mostly mechanical.

---

## 5. Verification

### 5.1 After commit 1 — popup positioning

Use the existing `tools/` helpers + manual checks:

```
dotnet run --project DeribitVerdictEngine.vbproj    # background
sleep 10
# Click Calibration Readiness, confirm popup appears on the same monitor as MainForm.
pwsh tools/click-mainform-button.ps1 "Calibration Readiness"
sleep 1
pwsh tools/inspect-mainform-tree.ps1 -Pattern "Analysis Report" | head -5
# Popup's bounding rect should overlap with MainForm's bounding rect (same screen).
```

Repeat for: Analysis Report CTA, Tweak Settings, Output Dump, Output Dump cog. **All popups should land centered on whichever monitor MainForm is on.**

If you have access to a multi-monitor setup, move MainForm to the secondary monitor first, then spawn each popup — confirm they follow.

### 5.2 After commit 2 — full-form capture + UIA helpers

**Full-form capture:**

```
pwsh tools/screenshot-mainform-full.ps1 verify/test-full.png
```

Read `verify/test-full.png` via the `Read` tool. Confirm the screenshot shows:
- All cards top to bottom (SCORE → VERDICT → LAST PRICE → ATR → STRUCTURAL × 2 → SIGNAL BREAKDOWN → OI×CVD / VOLUME PROFILE → KELLY → INDICATOR DETAILS → verification dump → SETTINGS & TOOLS)
- No clipping at the bottom
- No visual flash artifacts at card edges (from the temporary resize)

**Radio selector:**

```
pwsh tools/select-mainform-radio.ps1 "In Long"
sleep 1
pwsh tools/inspect-mainform-tree.ps1 -Pattern "In Long" | head -3
# The "In Long" radio's IsSelected property should now be True.

pwsh tools/select-mainform-radio.ps1 "No Position"
```

**Popup-close:**

```
# Spawn a popup, then close it.
pwsh tools/click-mainform-button.ps1 "Calibration Readiness"
sleep 1
pwsh tools/close-popup-window.ps1 "Analysis Report"
# Subsequent inspect should show only MainForm in the top-level window list.
```

---

## 6. Risks

| # | Risk | Mitigation |
|---|---|---|
| R1 | `DrawToBitmap` renders some control incorrectly (e.g., ScoreArcGauge's anti-aliasing differs from on-screen). | Acceptable. Visual verification is qualitative; minor anti-aliasing differences don't affect parity assessment. If a specific control renders completely wrong, fall back to scroll-and-stitch via PrintWindow for that case. |
| R2 | Temporary form-resize during full-form capture causes layout reflow that interacts badly with `_staleOverlays` (P4f) or other cached layout state. | The resize is wrapped in try/finally with size restoration. Stale overlays don't fire unless `RenderSkippedDashboard` is active — harness mode shouldn't trigger that. |
| R3 | `SendKeys` for Ctrl+Shift+S goes to the wrong window (e.g., the PowerShell terminal). | Helper calls `SetForegroundWindow` on MainForm before sending. If foreground steal fails (rare on Windows 10+), the user can manually focus MainForm before running the helper. |
| R4 | Marker file mechanism leaves stale `.screenshot-target` files that trigger spurious captures. | Hotkey handler deletes the marker after reading. If the app crashes mid-capture, the marker stays — but the next hotkey-without-marker is a no-op. Helper writes a fresh marker each time, so stale state self-heals. |
| R5 | `Screen.FromControl(parent)` returns the primary screen when the parent form's coordinates are in a no-man's-land (e.g., during DPI change events). | Rare. If it happens, the popup lands on primary — same as today's bug. Net no worse. |
| R6 | `MessageBox.Show(Me, ...)` modal-relative-to-owner blocks the MainForm UI thread. | This is the existing modal-MessageBox behaviour for any owner-specified call. Already accepted in the P5b kickoff for the ERROR path. |
| R7 | The `SaveFullFormScreenshot` resize might exceed the user's physical screen extent, causing a visible glitch. | The temporary resize is hidden (PostMessage + DoEvents complete fast). If a glitch is visible, the user can ignore — it's dev-only tooling. |
| R8 | `ComputeNaturalFormHeight` sums only Absolute-sized rows; if any row uses Percent or AutoSize, the natural height is wrong. | Verified: every `AddRow(...)` in the current codebase uses absolute pixel heights. Should a future row use a different SizeType, this function under-counts and the screenshot clips. Add an assertion: `Debug.Assert(rs.SizeType = SizeType.Absolute)` in the loop. |

---

## 7. Out of scope (deferred)

- **SCORE arc fill animation interference with harness screenshots.** Harness paces itself with a 400ms wait per case (cheaper than modifying ScoreArcGauge — that'd be a P3 control change requiring the paint carve-out).
- **Stable initial form position on launch.** Harness can call `resize-mainform.ps1 -X 50 -Y 50` at the start of its run.
- **Standalone diff-files PowerShell helper.** Harness has its own ~30 LoC inline diff per `ui-reskin-p5-test-harness-kickoff.md` §3.3.
- **Modifying the popup forms' own `StartPosition = CenterScreen` constructor lines.** Leaves them usable standalone; runtime `PositionOnParentScreen` takes precedence.
- **Persisted window-position serialization.** Out of scope — that's a separate UX feature.

---

## 8. If you get stuck

1. **`DrawToBitmap` produces a blank or partial PNG.** Try `Application.DoEvents()` twice (before and after `PerformLayout`) to ensure paint cycle completes. If still blank, check that `Me.Visible = True` — DrawToBitmap on a hidden form silently produces nothing.
2. **`Screen.FromControl` returns null.** Doesn't happen for visible forms. If it does, fall back to `Screen.FromPoint(parent.Location)`.
3. **`SetForegroundWindow` returns false in `tools/screenshot-mainform-full.ps1`.** Windows blocks foreground-steal in certain conditions. Workaround: prepend `Add-Type -AssemblyName Microsoft.VisualBasic` and use `[Microsoft.VisualBasic.Interaction]::AppActivate($pid)` — older API but more permissive.
4. **Hotkey doesn't fire.** Confirm `Me.KeyPreview = True`. Confirm the KeyDown handler is wired via `Handles MyBase.KeyDown` (not just `AddHandler` — the latter can be missed if the form's GotFocus / Activated cycle hasn't completed).
5. **`MessageBox.Show(Me, ...)` causes a "cross-thread operation" error.** Means the call site is inside an async continuation that resumed off the UI thread. Wrap: `Me.Invoke(Sub() MessageBox.Show(Me, ...))`. Rare but possible.

---

## 9. Reporting back

Spec-back doc: `docs/screenshot-reliability-spec-back.md`. Same structure as past spec-backs.

Specifically worth reporting:

1. **Confirmed popup-positioning behaviour on multi-monitor.** If you can't test multi-monitor in your session, note that explicitly and rely on the user's verification.
2. **Full-form capture PNG dimensions.** Should be ~1116 wide × ~2000 tall (or whatever the form's natural extent is). Compare against the screenshots from P4f's `verify/p4f-lower-cards.png` to confirm coverage.
3. **`DrawToBitmap` rendering quality.** Note any controls that render differently from on-screen (anti-aliasing differences, missing glyphs, etc.). Forecasts what the P5-test harness will see.
4. **UIA helper exit codes.** Confirm `select-mainform-radio.ps1` exits 0 on success, 2 on no-match (with diagnostic list), 3 on unsupported pattern.
5. **Whether `close-popup-window.ps1` correctly excludes MainForm.** Test: open MainForm, run `pwsh tools/close-popup-window.ps1 "Deribit"` — should report "no windows matched" since MainForm is excluded.
6. **README update content.** Quick visual confirmation that the table and workflow loop are consistent with the new helpers.

---

## 10. Workflow reminders

- **Local commits only.** Two commits. Do NOT push.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits, no `UI/Controls/*.vb` modifications.
- Engine code untouched. `Core/` is read-only.
- Settings.json untouched.
- Self-screenshot is the default verification path per handover §10.9 — use the existing tools/ helpers for commit 1's verification, and the new ones for commit 2's verification.

---

**End of kickoff.** Drop into a fresh Opus 4.7 Medium conversation as the opening message. Ships during the P5a verification window, before the P5-test harness kickoff becomes actionable. After this lands, the harness has reliable plumbing — popups don't escape to the wrong monitor, full-form screenshots work in one shot, and the harness can drive radio buttons + dismiss popups via dedicated helpers.
