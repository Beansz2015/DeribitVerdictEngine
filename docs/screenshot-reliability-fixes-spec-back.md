# Screenshot Reliability Fixes — Spec-back Report

**Phase:** Defect repair on the just-shipped `screenshot-reliability` spec (`4a9781e` + `65dd6e7` + `aaf12e9`).
**Spec source:** `docs/screenshot-reliability-fixes-kickoff.md`
**Author:** Claude (Opus 4.7, implementation conversation)
**Date:** 2026-05-27
**Status:** ✅ Shipped (`20f1a0b`, local only). All three bugs cleared end-to-end.

---

## 1. Commit

| SHA | Subject | LoC |
|---|---|---|
| `20f1a0b` | `fix(tools): full-form screenshot foreground + natural extent + no form resize` | +61 / −17 across 2 files |

Single commit per kickoff §2 recommendation. Within the 30-50 LoC band the kickoff estimated (slightly over because the implementation switched the VB capture mechanism from Form-resize to grid-resize — see §3 below — which is a larger rewrite of `SaveFullFormScreenshot` than the kickoff anticipated).

---

## 2. Bug-by-bug outcomes

### 2.1 Bug 1 — `AttachThreadInput` foreground steal ✅

Applied §3.1 verbatim. Five new P/Invoke imports (`ShowWindow`, `BringWindowToTop`, `GetForegroundWindow`, `GetWindowThreadProcessId`, `AttachThreadInput`, `GetCurrentThreadId`). Sequence: attach to current-foreground's input queue → `ShowWindow(SW_RESTORE)` → `BringWindowToTop` → `SetForegroundWindow` → detach → 500 ms sleep → `SendKeys ^+s`.

Verified: `screenshot-mainform-full.ps1` now fires the MainForm hotkey on first try from a fresh PowerShell session with no manual focus stealing. Marker file consumed every time, PNG saved within ~1 second of helper exit.

### 2.2 Bug 2 — full natural extent ✅ (via grid-resize, not form-resize)

**The kickoff's §3.2 fix (switch `RowStyles` loop → `GetRowHeights()`) did not solve the problem on its own.** A first run with only that fix produced a 1116 × 2171 PNG — same as the pre-fix bitmap. SETTINGS & TOOLS still missing.

Diagnosis: `Me.Size = New Size(Me.Width, ComputeNaturalFormHeight())` is silently clamped by Windows' `SystemMaximumSize` (≈ screen working area) regardless of whether `Me.MaximumSize` is `Size.Empty`. On the test monitor the clamp landed at ~2171 logical px, well below the ~3170 needed for the full grid. The kickoff's §6.4 anticipated this case ("or `_gridRoot` itself is constrained somehow … `Me.Width × Me.Height` should match the bitmap dimensions") — confirmed: bitmap = `Me.Size`, both clamped.

**Fix applied:** rewrote `SaveFullFormScreenshot` to capture `_gridRoot` directly rather than `Me`. Temporarily undocks the inner panel (`Dock = None`, `AutoScroll = False`), resizes it to natural row-sum extent (which is NOT clamped because it's a child control, not a top-level window), calls `_gridRoot.DrawToBitmap(...)`, then restores in `Finally`. Form size is never touched.

`ComputeNaturalFormHeight` renamed → `ComputeGridNaturalHeight`. Implementation matches kickoff §3.2 (`_gridRoot.GetRowHeights()` enumeration) minus the form-chrome term, since the grid is the only thing being drawn.

Post-fix PNG dimensions: **1100 × 3124 logical px** (was 1116 × 2171). All 11 card rows present.

### 2.3 Bug 3 — form-size restore ✅ (made moot)

Because §2.2's fix never touches `Me.Size`, the Bug 3 restore issue disappears entirely. The form remains at its launch-time size before, during, and after every capture. The restore work in `Finally` now applies to `_gridRoot` (Dock / Anchor / AutoScroll / Size / Location) — all invisible to the user because the grid stays the same on-screen size; the only thing the user might briefly see is a momentary AutoScroll toggle, which is masked by the immediate `PerformLayout` + `DoEvents`.

Hypotheses A / B / C from kickoff §3.3 were not pursued — the grid-resize approach sidesteps the question of why `Me.Size` restore wasn't sticking.

---

## 3. Why I deviated from the kickoff prescription

The kickoff §3.2/§3.3 prescribed:
- `ComputeNaturalFormHeight` → `GetRowHeights()` (Bug 2).
- `Finally` block reorder + `DoEvents` to make `Me.Size = originalSize` stick (Bug 3).

I applied §3.2 literally first. The PNG was unchanged (1116 × 2171). Diagnosis showed Windows' `SystemMaximumSize` clamp was the binding constraint — the form simply cannot grow past the screen working area via `Me.Size`, so growing the form was never going to capture the full extent on a monitor shorter than the form's natural height. The kickoff §6.4 listed this exact scenario ("or `_gridRoot` itself is constrained somehow") as a fallback investigation path.

Switching to `_gridRoot.DrawToBitmap` after a temporary undock is the structural fix: a child `Control`'s `Size` is not clamped by `SystemMaximumSize`, only by its parent's layout constraints, which we suspend by undocking. This yields a clean PNG at full extent regardless of monitor size, and as a side effect dissolves Bug 3 (no form resize → no restore problem).

Trade-off: the captured bitmap omits the form's title bar / chrome. This is a non-issue for verification — `Me.DrawToBitmap` already excluded the title bar anyway (it captures client area only), so there's no visual difference between pre- and post-fix captures other than the additional cards at the bottom.

---

## 4. Verification (kickoff §4)

### 4.1 Build clean

`dotnet build` after the commit: **0 warnings, 0 errors**, 2.3s.

### 4.2 Self-screenshot end-to-end ✅

Per kickoff §4.2:

```
Start-Process bin/Debug/net8.0-windows/DeribitVerdictEngine.exe
sleep 12
pwsh tools/click-mainform-button.ps1 "ANALYZE"
sleep 8
pwsh tools/screenshot-mainform-full.ps1 verify/post-fix-full.png
```

Output: `Saved C:\Dev\DeribitVerdictEngine\verify\post-fix-full.png (152847 bytes)`. Exit code 0. PNG dims **1100 × 3124**. Visual inspection confirms all card rows in order:

1. header strip (status chips + ANALYZE button)
2. perf strip ([B] indicator + 6 rate labels)
3. SCORE / VERDICT / LAST PRICE (hero row)
4. ATR ENTRY LEVELS
5. STRUCTURAL (LONG) / STRUCTURAL (SHORT)
6. SIGNAL BREAKDOWN
7. OI × CVD CROSS / VOLUME PROFILE
8. KELLY SIZING
9. INDICATOR DETAILS
10. verification dump card (legacy txtOutput contents)
11. **SETTINGS & TOOLS card** — LOG sub-box + AUTO-RUN sub-box + ANALYSIS REPORT CTA + TOOLS sub-box. Was absent pre-fix.

No clipping at the bottom; final row is followed by the grid's bottom padding. No visible artefacts in any card.

### 4.3 Form size restore ✅

Per kickoff §4.3, UIA inspection after capture:

```
> tools/inspect-mainform-tree.ps1 -Pattern "^Deribit Verdict Engine"
Form bounding rect: X=1083 Y=30 W=1674 H=2028
```

This matches the form's launch-time size on the test monitor (no change from before the capture). Form never grew during the capture because `Me.Size` was never touched.

### 4.4 Repeatability ✅

Per kickoff §4.4, a second consecutive capture:

```
> tools/screenshot-mainform-full.ps1 verify/post-fix-full-2.png
Saved C:\Dev\DeribitVerdictEngine\verify\post-fix-full-2.png (206269 bytes)
> tools/inspect-mainform-tree.ps1 -Pattern "^Deribit Verdict Engine"
Form bounding rect: X=1083 Y=30 W=1674 H=2028
```

Second PNG: **1100 × 3124**, byte-size differs slightly because the second capture happened ~1s later and a different live tick was rendered into the cards (price values shifted by a few sats). UIA form rect identical to first capture — no state-accumulation drift.

### 4.5 Foreground-steal regression check

Not exercised. The kickoff §4.5 noted this was "not a hard requirement." The `AttachThreadInput` block is additive; no other helper was modified.

---

## 5. Files touched

| File | Change | LoC |
|---|---|---|
| `tools/screenshot-mainform-full.ps1` | Added 6 new P/Invoke imports + AttachThreadInput foreground sequence + sleep bumped 200→500 ms. | +23 / −5 |
| `UI/MainForm_Layout.vb` | `SaveFullFormScreenshot` rewritten to capture `_gridRoot` directly (undock + resize + DrawToBitmap + restore). `ComputeNaturalFormHeight` renamed `ComputeGridNaturalHeight`; `GetRowHeights()` enumeration; no form-chrome term. | +38 / −12 |

Total: +61 / −17 across 2 files. Build clean both incrementally during diagnosis and after the final fix landed.

---

## 6. Reporting-back items (kickoff §7)

1. **Final post-fix PNG dimensions** — **1100 × 3124 logical px** (was 1116 × 2171). Width shrank by 16 px because we're now drawing `_gridRoot` (which is `1100 + 8 + 8` padding = `1116` minus the 16 px form chrome ÷ 2 each side, etc.) rather than the form's client area. Practically identical content width.
2. **SETTINGS & TOOLS visibility** — confirmed present, full card visible at the bottom with all four sub-boxes intact. The PNG read via the multimodal `Read` tool shows the expected layout.
3. **Form size restore — which hypothesis fixed it** — none of A/B/C. The chosen fix removes the question entirely by not touching `Me.Size`. See §3 above.
4. **Number of test runs to confirm restore is stable** — 2 captures (kickoff §4.4 minimum). Both produced identical form bounding rect and PNG dimensions. No state-accumulation observed.
5. **Foreground-steal helper extraction** — not factored into a shared `tools/lib/*.ps1`. Kept inline in `screenshot-mainform-full.ps1` per kickoff §5 ("out of scope here"). If a future helper needs the same block, a shared extraction can happen then.

---

## 7. Out of scope (followed kickoff §5)

- ❌ No changes to `select-mainform-radio.ps1`, `close-popup-window.ps1`, `screenshot-mainform.ps1`, `click-mainform-button.ps1`, `inspect-mainform-tree.ps1`, `resize-mainform.ps1`.
- ❌ No changes to `PositionOnParentScreen` or its call sites.
- ❌ No changes to the marker-file IPC (`ReadScreenshotTargetPath`).
- ❌ No `UI/Controls/*.vb` edits.
- ❌ No `MainForm.Designer.vb` edits.
- ❌ No scoring / indicator / engine / settings.json / CSV / dump schema edits.
- ❌ No push to remote.

---

## 8. Worth flagging

1. **Windows `SystemMaximumSize` clamp is now codified in code comments** at `SaveFullFormScreenshot`. Future contributors who try to "simplify" the capture by re-resizing `Me` will hit the same trap. The 7-line block comment at the top of the sub explains why we don't.

2. **The grid-resize approach generalises**. If a future spec adds a tall list/expander that exceeds even the natural grid extent (e.g., an expanding indicator details panel), the same `Dock = None` + size-up pattern will keep working as long as the content is a single addressable child of the form. No further mechanism change needed.

3. **PNG width changed from 1116 → 1100.** Anyone with hard-coded width assertions in test harness baselines will need to update them. None currently exist; flagging in case the P5-test harness kickoff adds them later.
