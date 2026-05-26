# tools/

Dev-side helper scripts for verifying the WinForms app during implementation and spec-author conversations. None of these affect application behaviour; they exist only to support visual + structural verification without round-tripping through the user every iteration.

## Scripts

| Script | Purpose |
|---|---|
| `screenshot-mainform.ps1` | Win32 `PrintWindow` capture of the MainForm to PNG. Works on non-foreground windows. Visible pixels only — bottom cards clip on displays shorter than the form. |
| `screenshot-mainform-full.ps1` | Full-form capture via the MainForm's `Ctrl+Shift+S` hotkey (`DrawToBitmap` internally). Renders all content regardless of display height — use this when the form may exceed the working area. |
| `click-mainform-button.ps1` | UI Automation `Invoke` on a named button (substring match). Triggers Analyze Now etc. without focus. |
| `select-mainform-radio.ps1` | UI Automation `SelectionItemPattern.Select` on a radio matched by name substring. Toggles `posState` (No Position / In Long / In Short) for Hold/Exit test cases. |
| `close-popup-window.ps1` | UI Automation `WindowPattern.Close` on any non-MainForm top-level window matched by title substring. Defensive cleanup for harness loops. |
| `resize-mainform.ps1` | `SetWindowPos` wrapper for moving/resizing the form. Brings off-screen card regions into capture range when the display working area is shorter than the form. |
| `inspect-mainform-tree.ps1` | UI Automation element tree dump filtered by regex on element Name. Verifies elements exist at expected positions when they're outside the screenshot frame. |

All scripts target `Deribit Verdict Engine` as a window-title substring (overridable via `-WindowTitleSubstring`), so they survive the `v0.NN [Pn]` version-suffix bumps that happen each phase.

## Self-screenshot workflow (default for verification)

As of P4f, **self-screenshot is the default verification path** for both implementation conversations and spec-author conversations that need to double-check visual correctness. The legacy "implement, ask the user to screenshot, iterate on their feedback" path remains the fallback for sessions without GUI access.

Typical loop:

```
# 1. Launch app in background.
dotnet run --project DeribitVerdictEngine.vbproj   # run_in_background=true

# 2. Wait for the form to render (~8-10 seconds).
sleep 10

# 3. Capture initial state.
pwsh tools/screenshot-mainform.ps1 verify/state-initial.png

# 4. Trigger an analysis (gets real data into the cards).
pwsh tools/click-mainform-button.ps1 ANALYZE
sleep 8

# 5. Capture post-analysis.
pwsh tools/screenshot-mainform.ps1 verify/state-analyzed.png

# 6. Read the captured PNGs via Claude's Read tool to inspect visually.

# 7. For elements past the display working area (lower cards on smaller
#    monitors), inspect via UI Automation tree dump instead.
pwsh tools/inspect-mainform-tree.ps1 -Pattern "SETTINGS|TOOLS|last"

# 8. If you need to bring lower cards into screenshot range, either move
#    the form upward and re-snap with PrintWindow, OR (preferred) use the
#    full-form helper which renders the entire form regardless of display
#    height — no resize needed.
pwsh tools/screenshot-mainform-full.ps1 verify/state-full.png

# 8b. Legacy fallback when DrawToBitmap renders something incorrectly:
pwsh tools/resize-mainform.ps1 -Y -700 -H 2000
pwsh tools/screenshot-mainform.ps1 verify/state-lower-half.png

# 9. Kill the app when done.
powershell -Command "Stop-Process -Name DeribitVerdictEngine -Force"
```

The `verify/` directory is gitignored — captured PNGs don't get committed.

## What self-screenshot catches

- Card clipping / overlap / sizing issues
- Text colour problems (font-fallback tofu, contrast issues)
- Layout breakage (misaligned controls, missing rows)
- Visual artifacts (overlay corner mismatch, broken rounded corners)

## What it doesn't catch

- Data correctness (scores, indicator values, verdict semantics)
- Edge cases requiring specific data conditions
- Interaction states (hover, focus, mid-animation)
- Whether the trader's mental model of the layout matches the render

The user remains the second-pass reviewer for substance. Self-screenshot absorbs the first-pass discovery of visual issues that previously needed a round-trip.

## Force-skip verification for ANALYSIS SKIPPED state

The skipped-render path requires forcing a Deribit fetch failure. The
canonical pattern (Approach A from the P4f kickoff):

1. Temporarily edit `DeribitClient.GetFundingRateAsync` to return
   `Nothing` after N successful calls.
2. Rebuild, relaunch, trigger `N+1` analyses.
3. Screenshot the skipped state.
4. **Revert the edit before committing.** `git diff DeribitClient.vb`
   must show zero lines at commit time.

There's no helper for this — the edit is too situational. Document
it in the spec-back if the verification path was exercised.

## Sub-tools (separate compile target)

- `AutoTweaker/` — host-agnostic console app (Bundle 2). Separate .NET 8
  project that runs on Linux. Not related to the self-screenshot pattern.
