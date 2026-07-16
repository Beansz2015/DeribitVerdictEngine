# Autonomous WinForms Test Harness — Handoff for the Order-App Coordinator

**From:** DeribitVerdictEngine coordinator seat, 2026-07-16 · **For:** the DeribitOrderPlacementApp coordinator, to replicate the engine's autonomous build/drive/verify loop.
**What it enables:** an AI seat (or any script) can compile the app, launch it, click buttons, set textboxes/radios, dismiss popups, take screenshots (including content scrolled off-screen), and verify results — with **zero third-party dependencies**: everything is PowerShell + the .NET assemblies already on a stock Windows box.

---

## 0. The two-layer philosophy (adopt both)

1. **Logic harness** — a separate console project that links the app's core source files and runs fixture checks (our `verify/ordercheck/`: ~130 fixtures, exit code 0/1). Fast, deterministic, CI-able. This is where 95% of verification lives.
2. **UI automation** — PowerShell scripts that drive the *running* app. Exists because **a clean build proves nothing about geometry or wiring**: two of our UI bugs (a blank card, missing borders) were build-clean and only visible in a screenshot. Use this layer for visual verification and end-to-end smoke, never as a substitute for layer 1.

Wrap both in a gate script (`tools/checks/verify-gate.ps1` pattern: build all projects → run the harness → any repo-specific guards → one `GATE PASSED/FAILED` line) and wire it to a git pre-push hook. The AI seat runs the gate before calling anything done.

## 1. Foundation: window discovery (every script starts here)

All scripts locate the form via **UI Automation** by title *substring*:

```powershell
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
$root  = [System.Windows.Automation.AutomationElement]::RootElement
$forms = $root.FindAll([System.Windows.Automation.TreeScope]::Children,
                       [System.Windows.Automation.Condition]::TrueCondition)
$form = $null
foreach ($f in $forms) {
  $n = $f.Current.Name
  if ($n -and $n.IndexOf($TitleSubstring, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $form = $f; break }
}
if ($form -eq $null) { Write-Error "MainForm not found"; exit 1 }
```

**Convention that keeps this stable:** the window title must carry a **fixed prefix** that never changes ("Deribit Verdict Engine — …"). When we later made our title dynamic (version suffix), we deliberately preserved the prefix because the scripts match on it. Pick the order-app's prefix on day one and treat it as load-bearing.

**Exit codes on every script** (0 = success, 1 = app not running, 2 = target not found, 3 = action failed) — this is what makes them composable by an AI seat: it reads `$LASTEXITCODE`, not prose.

## 2. Driving controls (UI Automation patterns)

**Click a button by name substring** (our `tools/click-mainform-button.ps1`):

```powershell
$buttons = $form.FindAll([System.Windows.Automation.TreeScope]::Descendants,
  (New-Object System.Windows.Automation.PropertyCondition(
     [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
     [System.Windows.Automation.ControlType]::Button)))
foreach ($b in $buttons) {
  if ($b.Current.Name -match $NamePattern) {
    $b.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke(); exit 0
  }
}
# on no match: PRINT ALL BUTTON NAMES before exiting 2 — the self-diagnostic
# that lets the AI seat correct its own pattern without a human.
```

The same skeleton with a different ControlType + pattern covers everything else:

| Control | ControlType filter | Pattern + call |
|---|---|---|
| **Textbox (write)** | `Edit` | `ValuePattern` → `.SetValue("...")` |
| **Radio** | `RadioButton` | `SelectionItemPattern` → `.Select()` (our `select-mainform-radio.ps1`) |
| **Checkbox** | `CheckBox` | `TogglePattern` → `.Toggle()` (check `.Current.ToggleState` first) |
| **Popup/dialog close** | find the window itself | `WindowPattern` → `.Close()` (our `close-popup-window.ps1`) |
| **Read any value/label** | any | `.Current.Name`, or `TextPattern` for rich text |

WinForms exposes control **Text** as the UIA `Name` automatically — no app changes needed. For unlabeled controls, set `AccessibleName` in the app (one property) rather than fighting blind matching.

**Layout verification without pixels** (our `inspect-mainform-tree.ps1`): dump every element's `BoundingRectangle` (`Y= X= W= | 'Name'`), optionally regex-filtered. Two virtues: it sees elements **scrolled off-screen** (screenshots can't), and diffing two dumps detects layout regressions numerically. Note: UIA reports **physical screen pixels**; the form's internal logical coordinates differ under DPI scaling — compare UIA-to-UIA, not UIA-to-Designer.

## 3. Screenshots — two techniques, know which you need

**(a) External capture, zero app changes:** P/Invoke `PrintWindow` on the form's `NativeWindowHandle` (our `screenshot-mainform.ps1`). Captures the **visible area only**. Fine for most checks.

**(b) Full-form capture including off-screen content — requires a tiny in-app hook** (our `screenshot-mainform-full.ps1` + a MainForm handler):
- App side: a debug hotkey (we use **Ctrl+Shift+S**) whose handler reads an output path from a **marker file** (`verify/.screenshot-target` relative to `AppDomain.BaseDirectory`), calls `MainForm.DrawToBitmap` (renders the complete form, even parts past the display clip), saves the PNG, deletes the marker. ~200–500ms.
- Script side: write the absolute output path into the marker (note: into the **bin** directory the app runs from), foreground the form, `SendKeys ^+s`, then **poll for the PNG** (10s deadline) rather than sleeping.

**The Windows 11 foreground-steal bypass** — SendKeys silently goes to the wrong window without this; copy it verbatim:

```powershell
# Win11 blocks SetForegroundWindow; attaching to the current foreground's
# input queue lifts the lockout so focus genuinely transfers.
$fgThread = [WFG]::GetWindowThreadProcessId([WFG]::GetForegroundWindow(), [IntPtr]::Zero)
$cur      = [WFG]::GetCurrentThreadId()
[WFG]::AttachThreadInput($cur, $fgThread, $true)  | Out-Null
[WFG]::ShowWindow($hwnd, 9); [WFG]::BringWindowToTop($hwnd); [WFG]::SetForegroundWindow($hwnd)
[WFG]::AttachThreadInput($cur, $fgThread, $false) | Out-Null
```

(P/Invoke declarations via `Add-Type` — see our script for the 6-line class.)

**(c) Measure, don't eyeball:** for geometry work, a `GetPixel` sweep over the PNG reporting contiguous rows of non-background luminance gives exact text-line positions and gaps. Our UI space-pass tuned every margin from these numbers, not from squinting.

## 4. The autonomous loop

```
dotnet build -c Debug
  → launch the exe (bin\Debug\...)          # Start-Process, then poll for the window (§1)
  → drive: click / set / select (§2)
  → wait: poll UIA state or file outputs — never bare sleeps where a signal exists
  → capture: tree dump + screenshot (§2/§3)
  → assert: exit codes, dump diffs, pixel scans
  → Stop-Process; iterate
```

Practical rules we learned: **know which bin you're driving** (Debug vs Release — our collector owns the Debug exe, so UI sessions must not rebuild Debug while it runs; define the order-app's equivalent rule); kill the app between iterations rather than trusting hot state; keep screenshots out of the repo (delete after use — standing rule here).

## 5. ⚠ ORDER-APP-SPECIFIC SAFETY (read twice)

This harness will be pointed at an app that **places real orders**. Non-negotiables before the first autonomous run:

1. **Testnet only:** autonomous sessions run against `test.deribit.com` credentials/config — never the live key. Make the environment visible in the window title so every screenshot self-documents which world it was in (and scripts can refuse: find-window on "TESTNET" prefix).
2. **Never automate the trade path blind:** scripts that click order-placing buttons must be separate, clearly named (`click-PLACES-ORDER-*.ps1`), and used only in testnet sessions. The generic click script matching on a substring like "Buy" is exactly how an accident happens — our click script's *print-all-buttons-on-miss* diagnostic is your friend; substring patterns should be exact-ish.
3. **A kill rule:** any script that can't confirm the environment (title check fails) exits non-zero and does nothing.
4. The AI seat's standing instruction should mirror ours: local-first, gate before done, and **the human flips anything that touches live trading** — automation proves mechanics, never authorizes exposure.

## 6. Suggested build order (one conversation each)

1. Gate script + logic-harness skeleton (if the order app lacks one, start with 5 fixtures around the signal-file consumer — it already has pure functions worth pinning).
2. `find-window` + `click-button` + `inspect-tree` (the §1/§2 skeletons) + title-prefix convention.
3. Screenshot (a), then the in-app hotkey + (b) if full-form capture is needed.
4. `set-textbox` / `select-radio` / `close-popup` as needed.
5. The safety wrappers (§5) **before** any script that can reach a trade button.

Copy freely from this repo's `tools/*.ps1` — they're small, commented, and battle-tested through a four-round UI overhaul driven entirely by this loop.
