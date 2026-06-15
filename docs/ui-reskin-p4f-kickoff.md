# UI Reskin P4f — Implementation Kickoff

**Phase:** P4f — ANALYSIS SKIPPED degraded render
**Spec source:** `docs/ui-reskin-proposal.md` §5.1 + handover §3 P4f section
**Predecessor spec-back:** `docs/vpfr-buckets-histogram-spec-back.md` — lessons folded in
**Author:** Claude (Opus 4.7, spec-author conversation)
**Date drafted:** 2026-05-25
**Recommended model:** **Opus 4.7 Medium.** State plumbing + visual-state pattern + small layout add. No engine touch, no settings change, no P3 control modifications.

---

## 0. What this phase is (and isn't)

**Is:** the render path that fires when `RunAnalysisAsync` hits the existing resilience check at `MainForm_Analysis.vb:86-113` — replacing the current "clear `txtOutput`, write 'ANALYSIS SKIPPED' to RTF, set `lblVerdict.Text = 'SKIPPED'`" behaviour with a card-based degraded state that surfaces last-known indicator values dimmed, plus an `ANALYSIS SKIPPED` hero in the VERDICT card.

**Isn't:**
- ❌ Any scoring / indicator / engine change. Read-only access to existing types.
- ❌ Any `UI/Controls/` modification. Paint carve-out **not** invoked.
- ❌ P5 work (`txtOutput` deletion, `BuildPlaintextSnapshot`). Legacy text dump stays parked.
- ❌ Spec C work (SC column parity). Scheduled for post-P5 per handover §6.
- ❌ Settings.json changes.
- ❌ `MainForm.Designer.vb` edits.
- ❌ CSV schema changes.

---

## 0.5. Step 0 — Screenshot capability check (DO THIS FIRST)

**Mandatory first task before any P4f code is written.** The spec author has explicitly requested implementer self-screenshot capability be established as a precondition. If it works, commits 1 and 2 use it for visual verification on every iteration, absorbing the first round of layout/clipping/artifact discovery that the user has been performing manually. If it fails, fall through to the user-verification pattern used in prior phases.

### 0.5.1 What you're checking

1. PowerShell is available and can call Win32 APIs.
2. The session has access to a Windows GUI surface (not a headless / no-display environment).
3. `PrintWindow` can capture the DeribitVerdictEngine MainForm even when it's not the foreground window.
4. The captured PNG is readable via the `Read` tool.

### 0.5.2 Steps

```
1. Author the helper script at `tools/screenshot-mainform.ps1` per §0.5.4.
2. Build the project at HEAD: dotnet build (no code changes yet).
3. Launch the app in background: Bash run_in_background=true, command="dotnet run --project DeribitVerdictEngine.sln"
4. Wait ~8 seconds for the form to render (or poll for window existence).
5. Run the screenshot helper: pwsh tools/screenshot-mainform.ps1 verify/capability-check.png
6. Read the PNG with the Read tool. Confirm you can see the rendered MainForm — cards, perf strip, all controls visible.
7. Kill the background dotnet process. Discard the verify/ folder.
```

### 0.5.3 Decision

**If steps 1-6 all succeed** → screenshot capability is available. Proceed to **§0.5.5 (commit 0)** to ship the helper script, then resume with commits 1 and 2 using screenshot-driven verification on every iteration.

**If any step fails** (FindWindow returns Zero, PrintWindow throws, the Read tool can't render the PNG, the session is headless, PowerShell isn't `pwsh`-callable from Bash, etc.) → screenshot capability is unavailable. **Do not commit the helper script.** Skip to commit 1 and use the existing user-verification fallback for §4.2.

Either path is acceptable; the user has signed off on both. The failure path is no worse than how P4e / Spec A / Spec B / B were verified.

### 0.5.4 Helper script

`tools/screenshot-mainform.ps1` — write verbatim:

```powershell
# Captures the DeribitVerdictEngine MainForm window to a PNG via Win32
# PrintWindow. Works on non-foreground windows. Returns exit 0 on success,
# non-zero on failure (window not found, capture failed, etc.).
#
# Usage: pwsh tools/screenshot-mainform.ps1 [output-path]
#   Default output-path = "screenshot.png" in the current working dir.

param([string]$OutputPath = "screenshot.png")

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W {
    [DllImport("user32.dll", CharSet=CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

# MainForm.Text is set in MainForm_Layout's constructor. If the title
# differs from "DeribitVerdictEngine" at runtime, adjust this string.
$titleCandidates = @("DeribitVerdictEngine", "Deribit Verdict Engine", "MainForm")
$hWnd = [IntPtr]::Zero
foreach ($title in $titleCandidates) {
    $hWnd = [W]::FindWindow($null, $title)
    if ($hWnd -ne [IntPtr]::Zero) { break }
}
if ($hWnd -eq [IntPtr]::Zero) {
    Write-Error "MainForm not found. Tried: $($titleCandidates -join ', ')"
    exit 1
}

$rect = New-Object W+RECT
$ok = [W]::GetWindowRect($hWnd, [ref]$rect)
if (-not $ok) { Write-Error "GetWindowRect failed"; exit 2 }

$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
if ($w -le 0 -or $h -le 0) { Write-Error "Invalid window dimensions: ${w}x${h}"; exit 3 }

$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
$ok = [W]::PrintWindow($hWnd, $hdc, 0)
$g.ReleaseHdc($hdc); $g.Dispose()

if (-not $ok) {
    $bmp.Dispose()
    Write-Error "PrintWindow returned false"
    exit 4
}

$dir = Split-Path $OutputPath -Parent
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Saved $OutputPath (${w}x${h})"
exit 0
```

### 0.5.5 Commit 0 (only if capability check succeeded)

If §0.5.2 step 6 produced a readable PNG, ship the helper as commit 0 — a standalone commit so it's available to subsequent P-phases:

```
chore(tools): add MainForm screenshot helper for visual verification

Adds tools/screenshot-mainform.ps1 — Win32 PrintWindow-based capture
that works on non-foreground windows. Used by implementation
conversations for self-verification of card layout, clipping, and
visual artifacts before reporting back.

No app behaviour change. Helper is invoked manually during dev
verification only.
```

Also add `verify/` to `.gitignore` if not already covered, so captured PNGs don't get committed accidentally.

### 0.5.6 If capability check fails

- Delete the unused `tools/screenshot-mainform.ps1` (don't leave it half-written in the working tree).
- No commit 0.
- Proceed to commit 1, using the user-verification fallback throughout: implement, build, ask the user to screenshot, iterate based on user feedback. Same pattern as P4e / Spec A / Spec B.
- Note in the spec-back §7 that the capability check failed and what specifically failed — the spec author will record it for the next phase's planning.

### 0.5.7 What this changes in commits 1 and 2

If capability succeeded, every "verify" step in §4.2 becomes implementer-self-verify:

```
1. Build clean.
2. Apply Approach A force-skip edit.
3. Launch app in background.
4. Click Analyze Now (or wait for auto-run cycle).
5. Run pwsh tools/screenshot-mainform.ps1 verify/p4f-c1-skip.png
6. Read the PNG. Inspect visually. Iterate if anything looks wrong.
7. Revert Approach A. Repeat for successful render screenshot.
8. ONLY when self-verification passes obvious-issue checks, report back to user with the screenshot embedded for second-pass review.
```

If capability failed, every "verify" step stays as it is in §4.2 — describe what you implemented, ask the user to screenshot and report back, iterate based on their feedback.

---

## 1. What you inherit

### 1.1 Current skip site — replace this

`UI/MainForm_Analysis.vb:86-113`:

```vb
' Resilience check: if any required fetch failed, skip cleanly.
Dim skipReason As String = Nothing
If candles1m Is Nothing OrElse candles1m.Count < 50 Then
    skipReason = "1m candles unavailable"
ElseIf candles5m Is Nothing OrElse candles5m.Count < 30 Then
    skipReason = "5m candles unavailable"
ElseIf Not fundingRate.HasValue Then
    skipReason = "funding rate unavailable"
ElseIf Not bookSummary.HasValue Then
    skipReason = "book summary unavailable"
ElseIf orderBook Is Nothing Then
    skipReason = "order book unavailable"
ElseIf recentTrades Is Nothing OrElse recentTrades.Count = 0 Then
    skipReason = "recent trades unavailable"
End If

If skipReason IsNot Nothing Then
    _skipCount += 1
    txtOutput.Clear()
    AppendRtf(txtOutput, String.Format("ANALYSIS SKIPPED: {0}" & Environment.NewLine, skipReason), Theme.ACC_WARN, bold:=True)
    AppendRtf(txtOutput, String.Format("Skip count this session: {0}" & Environment.NewLine, _skipCount), Theme.FG_QUATERNARY)
    AppendRtf(txtOutput, "Engine continues — next auto-run cycle will retry.", Theme.FG_QUATERNARY)
    lblVerdict.Text      = "SKIPPED"
    lblVerdict.BackColor = Color.FromArgb(120, 100, 60)
    UpdateLogInfo()
    RaiseEvent AnalysisCompleted(Me, EventArgs.Empty)
    Return
End If
```

After P4f, the skip branch:

```vb
If skipReason IsNot Nothing Then
    _skipCount += 1
    _lastSkipReason = skipReason
    ' Keep the legacy txtOutput write for P5-pre parity (the verification dump card
    ' still reads from it). P5 deletes both the txtOutput write and the legacy
    ' RTF helpers.
    txtOutput.Clear()
    AppendRtf(txtOutput, String.Format("ANALYSIS SKIPPED: {0}" & Environment.NewLine, skipReason), Theme.ACC_WARN, bold:=True)
    AppendRtf(txtOutput, String.Format("Skip count this session: {0}" & Environment.NewLine, _skipCount), Theme.FG_QUATERNARY)
    AppendRtf(txtOutput, "Engine continues — next auto-run cycle will retry.", Theme.FG_QUATERNARY)
    UpdateLogInfo()
    RenderSkippedDashboard(skipReason)
    RaiseEvent AnalysisCompleted(Me, EventArgs.Empty)
    Return
End If
```

Note: `lblVerdict.Text = "SKIPPED"` and `lblVerdict.BackColor = …` lines drop. `lblVerdict` is hidden per handover §4 locked decision; no need to update it.

### 1.2 Success-path capture site

`MainForm_Analysis.vb:439-462` — the end of the successful render flow:

```vb
UpdateLogInfo()                                                      ' line 439

RenderOutput(r, verdict, norms, vwapWarmup, lastTradePrice)          ' line 441 — legacy
' ... card grid bindings (BindCardScore, BindCardVerdict, etc.) ... ' lines 443+

Await LivePerformanceTracker.UpdateAsync(verdict, r, candles1m, ...)  ' line 459
UpdatePerformanceLabels()                                            ' line 460

RaiseEvent AnalysisCompleted(Me, EventArgs.Empty)                    ' line 462
```

**Capture inserts between line 460 and line 462:**

```vb
UpdatePerformanceLabels()                                            ' line 460

' P4f: capture last-successful state for the skipped-render fallback.
_lastSuccessfulVerdict     = verdict
_lastSuccessfulIndicators  = r
_lastSuccessfulNorms       = norms
_lastSuccessfulCfg         = cfg
_lastSuccessfulRenderTime  = DateTime.Now
ClearStaleOverlays()                                                 ' if previous run was skipped

RaiseEvent AnalysisCompleted(Me, EventArgs.Empty)                    ' line 462
```

Capture happens **after** all rendering finishes — so the captured state always reflects a card-grid that successfully painted.

### 1.3 State fields to add — `UI/MainForm_Layout.vb` shared fields

Append after the existing `_skipCount` field (line ~51) and the `_logInfoTooltip` field (line ~67):

```vb
' P4f — last-successful capture for ANALYSIS SKIPPED degraded render.
Friend _lastSuccessfulVerdict     As VerdictResult
Friend _lastSuccessfulIndicators  As IndicatorResults
Friend _lastSuccessfulNorms       As DynamicNorms
Friend _lastSuccessfulCfg         As EngineSettings
Friend _lastSuccessfulRenderTime  As DateTime
Friend _lastSkipReason            As String

' P4f — overlay panels tracked per card for opacity-dim during skipped state.
' Created lazily on first skip, cleared on next successful render.
Friend _staleOverlays As New List(Of Control)()
```

Defaults are sensible: object refs default to `Nothing` (caught by null-checks in `RenderSkippedDashboard`), `_lastSuccessfulRenderTime` defaults to `DateTime.MinValue` (renders as "—" when no successful run has happened yet).

### 1.4 Existing helpers you'll use

From `MainForm_Render_Cards.vb`:
- `BuildPlainSectionHeader(text)` — section header builder
- `MakeSectionHeader(text, Optional colour)` — alt header builder (post-unification commit `be7f64b`, both produce identical output now)
- `BuildSubLabel(text, colour)` — sub-label / hint text
- `BuildBadgeRow(...)` / `BuildMiniMeter(...)` — not needed for skipped state but available
- All existing `BindCardXxx` methods stay live; the skip path doesn't call them (it leaves the last-successful render in place)

From `UI/Controls/`:
- `Pill` — for the amber `(stale)` tag in section headers
- Nothing else needed. **Paint carve-out not invoked.**

### 1.5 Existing skip-related fields

- `_skipCount As Integer` at [Layout.vb:51](UI/MainForm_Layout.vb:51) — already wired to `UpdateLogInfo` per P4e
- `UpdateLogInfo()` at [Render_Header.vb:14](UI/MainForm_Render_Header.vb:14) — currently produces `"Log: N rows[ · skipped M]"` and pushes path to tooltip. P4f extends this to add the "last successful" line — see §3.4.

---

## 2. Commit plan

Two or three commits depending on §0.5 capability-check outcome. Split for cleaner verification gates and to keep the opacity-overlay work isolated.

| # | Conditional? | Subject | Scope | LoC est. |
|---|---|---|---|---|
| 0 | **Only if §0.5 succeeded** | `chore(tools): add MainForm screenshot helper for visual verification` | Adds `tools/screenshot-mainform.ps1` + `.gitignore` entry for `verify/`. | ~50 |
| 1 | Always | `feat(ui-reskin): P4f — ANALYSIS SKIPPED state plumbing + verdict-card render` | State fields + capture site + `RenderSkippedDashboard` that updates only the VERDICT card (other cards keep last-painted content unchanged). LOG box gets `last {timestamp}` line. | ~180-220 |
| 2 | Always | `feat(ui-reskin): P4f — stale overlays + (stale) section tags` | Opacity-0.4 semi-transparent overlay per card + amber `(stale)` Pill injected into card section headers + age label. | ~120-180 |

If commit 1 reveals issues that make commit 2 trivial (e.g., the user is fine with "last-known cards stay sharp, only VERDICT swaps") then commit 2 can be deferred or dropped. Surface this back if you encounter it.

### 2.1 Per-commit ship / skip

**Commit 1:**
- ✅ Six state fields + `_staleOverlays` list in `MainForm_Layout.vb`
- ✅ Capture block after `UpdatePerformanceLabels()` in `RunAnalysisAsync`
- ✅ New `RenderSkippedDashboard(reason As String)` method in `MainForm_Render_Cards.vb`
- ✅ Skip-branch replacement at `MainForm_Analysis.vb:102-113` (calls `RenderSkippedDashboard`)
- ✅ VERDICT card switches to ANALYSIS SKIPPED layout (28pt amber hero + reason + hint sub-lines)
- ✅ `UpdateLogInfo` extends to render `last {HH:mm:ss}` line below the row count
- ⏸ Opacity overlays — deferred to commit 2
- ⏸ `(stale)` Pill in section headers — deferred to commit 2

**Commit 2:**
- ✅ `ApplyStaleOverlayToCards()` method paints semi-transparent overlays on every card except VERDICT
- ✅ `ClearStaleOverlays()` removes overlays at start of next successful render
- ✅ Each affected card section header gets a `Pill` injected with text `"(stale)"` or `"N min stale"` based on age
- ✅ Age math: `(DateTime.Now - _lastSuccessfulRenderTime).TotalMinutes`
- ⏸ Nothing else.

---

## 3. Spec details

### 3.1 `RenderSkippedDashboard(reason)` — VERDICT card layout

Replace the VERDICT card's content (the section header stays via `BuildPlainSectionHeader` or `MakeSectionHeader`) with three stacked elements:

```
┌─ VERDICT ─────────────────────────────────────────────┐
│                                                       │
│  ANALYSIS SKIPPED                                     │   ← 28pt bold, ACC_AMBER_DEEP, glow
│                                                       │
│  Deribit REST fetch failed — {reason}                 │   ← 10pt, ACC_WARN
│  Engine retains last-known indicator values.          │   ← 9pt, FG_TERTIARY
│  Skipping verdict generation until next successful    │
│  fetch (auto-run continues).                          │
│                                                       │
└───────────────────────────────────────────────────────┘
```

**Verdict text size — exception to handover §4 lock.** The §4 row "VERDICT hero text 18pt bold" was locked because the 2×2 sub-grid (CONTEXT / REGIME / MTF / HOLD) crowded under long verdict strings at 22-28pt. **In the SKIPPED state the 2×2 sub-grid is replaced with two static sub-lines**, so the crowding concern doesn't apply. The original proposal §5.1 spec'd 28pt + glow for SKIPPED hero; honour that.

Document the exception in the kickoff implementation: add a code comment at the point where the 28pt font is set explaining why it diverges from the locked 18pt under normal conditions.

**Glow:** approximated via `TextRenderer.DrawText` with a `PathGradientBrush` underlay (same pattern as the existing P4b verdict glow at 18pt — extend by ~20px radius for the 28pt size). If the glow looks tofu-bad or expensive, drop it — the amber colour + bold is sufficient visual weight on its own.

**Colours:**
- "ANALYSIS SKIPPED" text: `Theme.ACC_AMBER_DEEP` (`#D97706`)
- Reason sub-line: `Theme.ACC_WARN` (`#FBBF24`) — proposal §5.1 suggested `#FBBF24AA` (alpha 0.67) but Theme doesn't expose alpha tokens; plain `ACC_WARN` is acceptable
- Hint sub-line: `Theme.FG_TERTIARY` (`#8B8B92`)

### 3.2 Reason and hint text

**Reason:**
```
Deribit REST fetch failed — {reason}
```

Where `{reason}` is the existing `skipReason` string passed to `RenderSkippedDashboard`. Examples:
- `"Deribit REST fetch failed — 1m candles unavailable"`
- `"Deribit REST fetch failed — funding rate unavailable"`

**Hint** (fixed text, multi-line acceptable):
```
Engine retains last-known indicator values. Skipping verdict
generation until next successful fetch (auto-run continues).
```

Wrap manually with `Environment.NewLine` or let `Label.AutoSize = True` + width constraint handle it. Width constraint matters — card is ~520px so the hint can be one or two lines depending on font size; both are fine visually.

### 3.3 Stale overlays (commit 2) — option 1 from handover §3 P4f

Recommended approach: semi-transparent `Panel` overlay above each card except VERDICT.

```vb
Private Sub ApplyStaleOverlayToCards()
    ' Cards to dim: every bound card except VERDICT (which is the
    ' ANALYSIS SKIPPED hero) and the perf strip + SETTINGS & TOOLS
    ' (which stay live).
    Dim cardsToDim As Control() = {
        _cardScore, _cardLastPrice,
        _cardAtrLevels, _cardStructLong, _cardStructShort,
        _cardSignalBreakdown,
        _cardOiCvdCross, _cardVolumeProfile,
        _cardKelly, _cardIndicatorDetails
    }
    For Each card In cardsToDim
        If card Is Nothing Then Continue For
        Dim overlay = New Panel() With {
            .Dock = DockStyle.Fill,
            .BackColor = Color.FromArgb(153, Theme.BG_BASE),  ' 60% opaque tint
            .Margin = New Padding(0),
            .TabStop = False
        }
        ' Critical: must be added LAST and brought to front so it
        ' covers existing content but doesn't capture clicks below.
        overlay.Enabled = False  ' transparent to input
        card.Controls.Add(overlay)
        overlay.BringToFront()
        _staleOverlays.Add(overlay)
    Next
End Sub

Private Sub ClearStaleOverlays()
    For Each overlay In _staleOverlays
        If overlay Is Nothing Then Continue For
        Dim parent = overlay.Parent
        If parent IsNot Nothing Then parent.Controls.Remove(overlay)
        overlay.Dispose()
    Next
    _staleOverlays.Clear()
End Sub
```

**Alpha value (153 = 60%):** the proposal §5.1 said "opacity 0.4" which in WinForms-overlay-on-dark-base terms means a 60%-opaque tint of `BG_BASE` painted ON TOP of the card's content. Result: card content reads at ~40% effective brightness. Tune during verification if it reads too dim or not dim enough.

**Why `Panel` with `Enabled = False`:** Disabled child Panels in WinForms don't intercept mouse events and don't visually grey out their own paint (because they only paint their own background, not the host). The 60%-alpha tint comes from `Color.FromArgb(alpha, …)` — works because the cards' own `RoundedCardPanel.OnPaint` already runs before the overlay paints on top.

**Risk:** if `RoundedCardPanel` uses transparent painting that the Panel-overlay defeats (e.g., the card painted a rounded corner with anti-aliasing and the Panel overlay paints a square corner), there will be visible artifacts at the card corners. **Verify on the first screenshot.** If artifacts appear, switch to overlay shape matching (paint a `GraphicsPath` for rounded corners) or use a custom `OnPaint`-overriding overlay.

### 3.4 `(stale)` tag in section headers (commit 2)

Each dimmed card's section header gets an amber `Pill` injected to the right of the existing text:

```
SCORE  (stale)
SCORE  2 min stale     ← if older than 1 min
```

**Implementation pattern:** inject a `Pill` as a sibling of the section header in the card's content layout. Two paths:

**Path A — modify `MakeSectionHeader` to accept an optional `staleAge` parameter.** Cleaner; consumer code just passes the age. But changes a shared helper — verify all four existing call sites tolerate the change.

**Path B — inject the `Pill` inline at overlay-apply time.** Walks each card's `Controls` collection, finds the section header `Label`, adds a `Pill` next to it. Mechanical, isolated per-card. No helper change.

**Recommended: Path B.** Lower blast radius. The `Pill` lives only during the SKIPPED state; `ClearStaleOverlays` also removes the Pills.

**Pill spec:**
- Text: `"(stale)"` if age < 1 minute, else `"{N} min stale"` where N = `(DateTime.Now - _lastSuccessfulRenderTime).TotalMinutes` rounded down
- `BgColor`: `Theme.BG_CARD_RAISED`
- `FgColor`: `Theme.ACC_WARN`
- `BorderColor`: `Theme.ACC_WARN`
- Position: right of the section header text, vertically centred

### 3.5 `UpdateLogInfo` extension — `last {timestamp}` line

Currently at [Render_Header.vb:14](UI/MainForm_Render_Header.vb:14):

```vb
Private Sub UpdateLogInfo()
    Dim rows As Integer = AnalysisLogger.GetRowCount()
    Dim path As String  = AnalysisLogger.GetLogPath()
    Dim skipSuffix As String = If(_skipCount > 0, String.Format(" · skipped {0}", _skipCount), "")
    lblLogInfo.Text = String.Format("Log: {0} rows{1}", rows, skipSuffix)
    If _logInfoTooltip IsNot Nothing Then
        _logInfoTooltip.SetToolTip(lblLogInfo, path)
    End If
End Sub
```

P4f adds a second `Label` (`lblLastSuccess`) inside the LOG SectionGroup showing `last {HH:mm:ss}` when `_lastSuccessfulRenderTime > DateTime.MinValue`:

```vb
Private Sub UpdateLogInfo()
    Dim rows As Integer = AnalysisLogger.GetRowCount()
    Dim path As String  = AnalysisLogger.GetLogPath()
    Dim skipSuffix As String = If(_skipCount > 0, String.Format(" · skipped {0}", _skipCount), "")
    lblLogInfo.Text = String.Format("Log: {0} rows{1}", rows, skipSuffix)
    If _logInfoTooltip IsNot Nothing Then
        _logInfoTooltip.SetToolTip(lblLogInfo, path)
    End If
    ' P4f: last-successful render timestamp.
    If lblLastSuccess IsNot Nothing Then
        If _lastSuccessfulRenderTime > DateTime.MinValue Then
            lblLastSuccess.Text = "last " & _lastSuccessfulRenderTime.ToString("HH:mm:ss")
            lblLastSuccess.Visible = True
        Else
            lblLastSuccess.Visible = False
        End If
    End If
End Sub
```

`lblLastSuccess` declaration goes in `MainForm_Layout.vb` shared fields. Instantiation goes inside `ReparentSettingsToolsControls` at the LOG `SectionGroup` build site (around line 700 in current Layout). Layout: below `lblLogInfo`, above `lnkResetLog`. May need to shift `lnkResetLog` Y position down by ~18-20px to fit.

LOG sub-box current height is 92px allocated from the `row1` styles inside `ReparentSettingsToolsControls`. Adding an 18px label means content total grows from ~70px to ~88px. **Verify clipping** — bump the row1 height to 110px in commit 1 if `lnkResetLog` clips at the bottom.

---

## 4. Verification gate

### 4.1 Forcing the SKIPPED state for testing

The natural skip path needs a Deribit fetch to genuinely fail — hard to arrange on demand. Two viable approaches:

**Approach A — temporary code edit (recommended).** In `DeribitClient.vb`, hardcode one of the `GetXxxAsync` methods to return `Nothing` after a successful first call:

```vb
' TEMPORARY for P4f verification — DO NOT COMMIT
Private _verificationSkipCount As Integer = 0
Public Async Function GetFundingRateAsync() As Task(Of Double?)
    _verificationSkipCount += 1
    If _verificationSkipCount > 2 Then
        Return Nothing  ' force skip after 2 successful runs
    End If
    ' ... existing implementation ...
End Function
```

Run app, let auto-run cycle 3 times (or click `Analyze Now` three times), watch the SKIPPED state activate. **Revert the edit before committing.** A `git diff DeribitClient.vb` at commit time will catch it.

**Approach B — disconnect from network.** Run the app, then toggle the network adapter off. Next analysis fetches fail. More cumbersome; recovery requires reconnect + waiting for retry. Use Approach A.

### 4.2 Build-screenshot-measure gate (per-commit)

**Commit 1:**
1. `dotnet build` clean.
2. Run app. Click `Analyze Now`. Confirm normal card render.
3. Click `Analyze Now` two more times (warming the `_lastSuccessfulX` capture).
4. Apply Approach A edit. Click `Analyze Now`. **VERDICT card should switch to ANALYSIS SKIPPED hero + reason + hint.** Other cards retain last-painted content.
5. Confirm LOG box shows `last HH:mm:ss` line.
6. Confirm legacy `txtOutput` (still parked in verification dump card) shows the same SKIPPED text via the AppendRtf calls. Parity check.
7. Revert Approach A edit. Click `Analyze Now`. Confirm VERDICT card returns to normal render.

**Commit 2:**
1. `dotnet build` clean.
2. Re-apply Approach A edit.
3. Trigger skip. **Every non-VERDICT card now shows the semi-transparent overlay.** Section headers show `(stale)` or `{N} min stale` pill.
4. Wait 1-2 minutes (or set system clock forward) and trigger another skip — the age label should update.
5. Revert Approach A. Trigger successful analysis. Overlays disappear, pills disappear, normal render returns.

### 4.3 Implementer self-screenshot — see §0.5

The helper script + capability-check workflow lives in §0.5 (top of this kickoff). It runs **before** commit 1 begins. The outcomes:

- **Capability available** (helper shipped as commit 0) → use it on every verification cycle in §4.2. Read each captured PNG, inspect visually, only report back when self-verification passes obvious-issue checks. The user becomes the second-pass reviewer for substance + layout-matches-mental-model.
- **Capability unavailable** → fall back to the user-verification pattern in §4.2 as written. Same flow as P4e / Spec A / Spec B.

**What self-screenshot catches:** clipping, overlapping text, weird artifacts, font tofu, layout breakage, colour clashes.

**What it doesn't catch:** data-correctness issues, scoring drift, semantic correctness of the SKIPPED reason text. The user remains the second-pass reviewer for those regardless of which capability path landed.

---

## 5. Out of scope — explicit skip list

If any of these tempt you, **stop** and surface back:

- ❌ Modify any `UI/Controls/*.vb` file. The §4 paint carve-out is **not** invoked.
- ❌ Delete or modify `txtOutput` or the verification dump card. P5 owns that.
- ❌ Delete or modify legacy RTF helpers (`AppendRtf`, `AR`, `SectionHeader`, `Divider`). P5.
- ❌ Modify `MainForm.Designer.vb`.
- ❌ Touch scoring / indicators / `Core/`. Pure UI work.
- ❌ Settings.json changes.
- ❌ CSV schema changes.
- ❌ Build `BuildPlaintextSnapshot` (P5).
- ❌ Fix the SC column / TOTAL parity issue (Spec C, post-P5).
- ❌ Push to remote.

---

## 6. If you get stuck

Likely failure modes:

1. **Overlay paints over rounded card corners as squares, leaving visible corner artifacts.** Switch the `Panel` overlay to a `UserControl` subclass with `OnPaint` override using `PaintHelpers.RoundedRect` (exists in `UI/Controls/Helpers/`) for a rounded-corner overlay shape. ~20 LOC addition; doesn't touch `UI/Controls/`-locked files (the Helpers folder is shared paint code).

2. **`lblLastSuccess` doesn't appear inside the LOG sub-box.** The `SectionGroup` paints its title at top; child controls are positioned via explicit `Location` properties. Verify your `lblLastSuccess.Location.Y` doesn't collide with the `SectionGroup`'s 22px title bar. Add ~25-30 px Y offset from the top of the SectionGroup.

3. **VERDICT card flicker when toggling between successful and skipped renders.** `_cardVerdict.SuspendLayout() / ResumeLayout(True)` wrappers around the `Controls.Clear() / Controls.Add(...)` cycle suppress paint-during-rebuild flicker. Existing `BindCardXxx` methods already follow this pattern — mirror it in `RenderSkippedDashboard`.

4. **The screenshot helper fails with `FindWindow returns Zero`.** Either the app didn't launch (check Bash background output), or the window title isn't exactly "DeribitVerdictEngine". `MainForm.Text` may differ — adjust the FindWindow argument to match `Me.Text` set in `MainForm_Layout`'s constructor.

5. **`ClearStaleOverlays` leaves visual artifacts on the next render.** Confirm overlays are removed from their parent's `Controls` collection AND disposed. WinForms keeps a strong reference inside `Controls` even after `.Visible = False`; `Remove` + `Dispose` is the correct cleanup.

---

## 7. Reporting back

Spec-back doc: `docs/ui-reskin-p4f-spec-back.md`. Same structure as past spec-backs.

Specifically worth reporting if they happen:

1. The opacity alpha value you ended up with (153 ≈ 60% tint was the spec's recommendation; actual might be 128 / 178 / etc.).
2. Whether `Pill` injection in section headers (commit 2 Path B) caused any unexpected layout shifts in the cards.
3. Whether the screenshot helper worked or failed — if it worked, write a one-paragraph note in the spec-back so the next implementation conversation can adopt the pattern.
4. Final LOG sub-box height (kickoff estimated +18px so 110 px total; actual may differ).
5. Any rounded-corner artifact issues with overlays — and how you resolved.
6. If commit 2 ended up trivial after commit 1, whether you bundled or kept separate.

---

## 8. Workflow reminders

- **Local commits only.** **Do not push.** User verifies after local commits, then decides when to push.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- No engine code touched. Pure UI.
- Settings.json untouched.
- The §4 paint carve-out is **not** invoked.
- If you author the screenshot helper, place it in `tools/` and add to `.gitignore` if not already covered. Don't commit captured PNGs.
- The verification "Approach A" edit to `DeribitClient.vb` is **strictly temporary**. Revert before committing. `git diff DeribitClient.vb` should show no changes at commit time.

---

**End of kickoff.** Drop this verbatim into a fresh Opus 4.7 Medium conversation as the opening message; the conversation has everything it needs to ship P4f in two commits + spec-back.
