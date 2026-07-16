# Autonomous Windows-App Test Harness — Portable Recipe

**Origin:** DeribitVerdictEngine (`C:\Dev\DeribitVerdictEngine`), 2026-07-16 · **For:** any project coordinator replicating the engine's autonomous build/drive/verify loop (first consumers: DeribitOrderPlacementApp, DeribitContango, CustomerSalesIDSystem).
**What it enables:** an AI seat (or any script) can compile the app, launch it, click buttons, set textboxes/radios, dismiss popups, take screenshots (including content scrolled off-screen), and verify results — with **zero third-party dependencies**: everything is PowerShell + the .NET assemblies already on a stock Windows box.
**Applicability:** WinForms is first-class (everything below is proven there). WPF apps: §1/§2 (UI Automation driving) work unchanged; the §3(b) full-form capture hook is WinForms-specific (`DrawToBitmap`) — WPF's equivalent is `RenderTargetBitmap`. Web/console apps: this recipe does not apply (different tooling).

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

## 5. ⚠ SAFETY — calibrate to what the app can DO (read twice)

The harness clicks whatever matches a pattern. Before the first autonomous run, classify the app's worst side effect and apply the matching profile:

**Trading / order apps (DeribitOrderPlacementApp, anything with an exchange key):**
1. **Testnet only** for autonomous sessions (`test.deribit.com` credentials) — never the live key. Put the environment in the window title so every screenshot self-documents which world it was in, and scripts can *refuse* (find-window on a "TESTNET" prefix).
2. **Never automate the trade path blind:** order-placing clicks live in separate, loudly-named scripts (`click-PLACES-ORDER-*.ps1`), testnet-sessions only. A generic click script matching "Buy" as a substring is how an accident happens — keep patterns exact-ish and lean on the print-all-buttons-on-miss diagnostic.
3. **Kill rule:** any script that can't confirm the environment (title check fails) exits non-zero and does nothing.
4. **The human flips anything touching live trading** — automation proves mechanics, never authorizes exposure.

**Data / records apps (CustomerSalesIDSystem, anything holding customer PII):**
1. **Never drive a production database:** autonomous sessions run against a test copy or synthetic seed data; make the DB/environment visible in the title, same refuse-rule as above.
2. **Screenshots are a data-leak vector:** a PNG of a form full of real customer names/IDs ends up in repos, chats, and AI-conversation context. Autonomous sessions use synthetic data, or the capture step is scoped to windows known clean. Delete screenshots after use regardless (standing rule).
3. Gate destructive actions (delete/merge/export buttons) the same way trade buttons are gated: separate named scripts, test-environment-only.

**Read-only / analysis apps (DeribitContango-class, this engine):** the general rules suffice — know which bin you're driving, kill between iterations, delete screenshots. If the app ever grows a write path, re-classify.

## 6. Suggested build order (one conversation each)

1. Gate script + logic-harness skeleton (if the project lacks one, start with ~5 fixtures around its purest functions — parsers, calculators, file consumers — whatever computes without UI).
2. `find-window` + `click-button` + `inspect-tree` (the §1/§2 skeletons) + title-prefix convention.
3. Screenshot (a), then the in-app hotkey + (b) if full-form capture is needed.
4. `set-textbox` / `select-radio` / `close-popup` as needed.
5. The §5 safety wrappers **before** any script that can reach a trade button, a production database, or a destructive action.

Copy freely from the origin repo's `tools/*.ps1` (`click-mainform-button`, `inspect-mainform-tree`, `select-mainform-radio`, `close-popup-window`, `resize-mainform`, `screenshot-mainform`, `screenshot-mainform-full`) — they're small, commented, and battle-tested through a four-round UI overhaul driven entirely by this loop. The only per-project edits are the window-title substring and, for the full-capture hook, the in-app hotkey handler.
