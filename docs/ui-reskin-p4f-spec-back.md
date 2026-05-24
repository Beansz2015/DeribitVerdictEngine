# UI Reskin P4f — Spec-Back

**Phase:** P4f — ANALYSIS SKIPPED degraded render
**Kickoff:** `docs/ui-reskin-p4f-kickoff.md`
**Implementer:** Claude (Opus 4.7 Medium, P4f implementation conversation)
**Date completed:** 2026-05-25
**Commits:** `3c72cec` (helper), `c8ebac6` (commit 1), `5f724ad` (commit 2)
**Branch:** `master` — local commits only, not pushed

---

## 0. Outcome

Shipped in three local commits matching the kickoff's commit plan. Verification gate per §4.2 passed for both commits — VERDICT-card SKIPPED hero swap, consecutive-skip pill-age refresh, success-path overlay/pill teardown all confirmed by self-screenshot. Approach A force-skip edits to `DeribitClient.GetFundingRateAsync` were reverted before each commit; `git diff DeribitClient.vb` returned zero lines at commit time.

No engine, scoring, settings, CSV, or Designer changes. `UI/Controls/` files untouched. Paint carve-out not invoked.

---

## 1. Items the kickoff §7 asked to report

### 1.1 Overlay alpha value

Kept at **153** (the kickoff's recommendation) — but the visible result diverges from spec intent in a way worth flagging for the next phase.

The kickoff at §3.3 expected a `Panel` with `BackColor = Color.FromArgb(153, Theme.BG_BASE)` to render the card body at "~40% effective brightness" by alpha-blending the tint over the existing card content. In practice:

- `Panel.BackColor` with alpha < 255 is silently stripped to opaque RGB unless `SupportsTransparentBackColor` is set via `SetStyle` — so a naïve Panel paints the tint as fully opaque.
- I corrected this with a private nested subclass `StaleOverlayPanel` (in [MainForm_Render_Cards.vb](UI/MainForm_Render_Cards.vb)) that enables `SupportsTransparentBackColor` and paints the alpha tint via `SolidBrush` in `OnPaint` — alpha-aware GDI+.
- Even so, the underlying card *labels* don't dim through the overlay. WinForms doesn't composite sibling child controls into a shared pixel buffer: each child paints into its own region, and the topmost-z child *occludes* (does not blend with) what's beneath it. The overlay can alpha-blend with the parent `RoundedCardPanel`'s background, but not with the SCORE arc / VERDICT label / SIGNAL BREAKDOWN table that are siblings of the overlay inside the card.

**Visible result:** every dimmed card paints as a uniform dark rectangle with the (stale) pill at top-right. The pill is the primary staleness signal; the darker fill reinforces it. Trader's takeaway is unchanged ("don't read these values — they're stale and the engine couldn't analyze") but the spec's literal "content reads at ~40% brightness" isn't delivered.

**Path to spec-faithful dimming** (not implemented; flagged for future): walk each card's `Controls` recursively at apply-time, snapshot every `Label`'s `ForeColor` into a `Dictionary(Of Label, Color)`, set each `ForeColor` to a blended-toward-`BG_BASE` value (e.g., `Color.FromArgb((orig.R + bg.R) \ 2, …)`), and restore from the dictionary in `ClearStaleOverlays`. ~30–40 LoC. Decided against in this phase because (a) the (stale) pill already communicates the state cleanly, (b) custom controls like `ScoreArcGauge` / `ContextBadge` / `MtfRow` paint their own colours and would also need a dimming hook, multiplying the surface area, and (c) the overlay+pill approach worked at the build/verify gate.

### 1.2 Pill injection layout shifts (Path B)

Used **Path B** as recommended (§3.4) — inject pills directly into each card's `Controls` collection at apply-time rather than modifying `MakeSectionHeader`. No layout shifts in the underlying cards. Pills anchor to Top|Right with `Location = (card.ClientSize.Width - 86 - 12, 10)` and `Size = (86, 18)`. The 86 px width fits "(stale)" and "{NN} min stale" up to two-digit minutes; if a card stays skipped for 100+ minutes the text would clip, but that's outside any realistic auto-run cycle.

Pills land cleanly to the right of the section header text (e.g., "SCORE" at top-left, pill at top-right). No collision with the OFI Mom / Spread footer rows in the OI × CVD CROSS card or the histogram in VOLUME PROFILE.

### 1.3 Screenshot helper outcome

**Capability check passed.** PowerShell + Win32 `PrintWindow` works against the WinForms host even when the form isn't foreground, and the resulting PNG is readable via the `Read` tool. Shipped as commit 0 (`tools/screenshot-mainform.ps1` + `verify/` gitignore entry).

One adjustment vs. the kickoff §0.5.4 verbatim helper: the form's runtime title is `"Deribit Verdict Engine v0.47 [P4]"` (set in [MainForm_Layout.vb:165](UI/MainForm_Layout.vb#L165)) — the version suffix changes between phases, so an exact-match `FindWindow` would break each time. Replaced the exact-title `FindWindow` with an `EnumWindows` + substring-match (`IndexOf("Deribit Verdict Engine", OrdinalIgnoreCase)`) so the helper survives version bumps. Exact-title fallback retained for harness setups where `EnumWindows` is restricted.

The self-verification workflow was valuable. Caught two issues that user-only verification would have needed a round-trip for:

1. **Alpha stripping** (the §1.1 finding) was visible immediately on the first commit-2 screenshot — overlay was fully opaque rather than dim. Fixed in-loop with the nested-subclass rewrite.
2. **Click counter miscount** during the swap-back verification — saved a confused back-and-forth with the user.

Subsequent phases should reuse this pattern. Pair with `verify/click-analyze.ps1` (kept locally during this phase, gitignored) for analyze-on-demand triggering.

### 1.4 Final LOG sub-box height

Bumped row1 from **92 → 110 px** ([MainForm_Layout.vb:688](UI/MainForm_Layout.vb#L688)) — matches the kickoff's estimate. `lnkResetLog.Location.Y` shifted **48 → 66** to leave 20 px between `lblLogInfo` (Y=26) and the new `lblLastSuccess` (Y=46). No clipping observed on the live form (verification ran with the LOG sub-box in view at the resized 1100×1900 capture).

### 1.5 Rounded-corner artifacts

**None observed.** The `RoundedCardPanel` paints its rounded border around the perimeter and the card's client rectangle (where overlay children live) is the inner rounded region. The overlay's `Dock = Fill` makes it exactly match the inner client rectangle, so the rounded corners stay visible as the *card's* painted border peeking around the overlay's square edges. Looks clean in all captured PNGs.

If a future phase wants the overlay itself rounded (e.g., to mask card content right up to the visible border with no border-coloured corners), the kickoff §6 point 1 path — paint a `GraphicsPath` rounded-rect fill in `OnPaint` instead of `FillRectangle` — is the right pattern. Skipped here because the current visual is acceptable.

### 1.6 Commits 1 + 2 — bundled or separate

Kept **separate** per the kickoff's commit plan. Commit 2 was non-trivial — the WinForms alpha-stripping fix required the nested `StaleOverlayPanel` subclass, which adds ~30 LoC of paint logic that's worth isolating from the state-plumbing diff. Combining them would have blurred the review boundary (one commit doing state + verdict swap, the next doing overlay paint with its own correctness considerations).

---

## 2. Code-shape notes

### 2.1 `ClearStaleOverlays` split

The kickoff named one method for "swap verdict back + remove overlays." Once commit 2 needed idempotent re-application (consecutive skips refreshing the pill age), I split into:

- `ClearStaleOverlays()` (public, success-path): verdict swap-back + overlay teardown.
- `RemoveStaleOverlayControls()` (private): overlay/pill teardown only, no verdict swap. Called by `ClearStaleOverlays` and by `RenderSkippedDashboard` (the latter so a 2nd consecutive skip wipes the old pill before applying the fresh one with updated age text).

Two methods, one clean teardown loop. No behaviour difference at the call sites — the public name `ClearStaleOverlays` stays the kickoff's contract.

### 2.2 VERDICT panel toggle vs. tear-down

Chose visibility-toggle over tear-down-and-rebuild for the VERDICT card swap. Reasons:

- `InitVerdictCard` populates several `_lblVerdictText` / `_contextBadge` / `_lblRegime` / `_mtfRow` / `_lblHold` / `_lblVerdictEffPenalty` / `_regimeAnchorWarn` field refs that `BindCardVerdict` reads on every successful render. Rebuilding the normal panel each cycle would orphan stale refs and require re-assigning them.
- Two panels under one parent with `Dock = Fill` and Visible toggle is well-supported by WinForms — `_verdictNormalPanel.BringToFront()` ensures correct z-order on swap-back.

The kickoff §1.3 listed the new state fields without prescribing the toggle/rebuild decision, so this is a free judgment call.

### 2.3 Capture site

Inserted between `UpdatePerformanceLabels()` and the `AnalysisCompleted` event (per §1.2). One adjustment: I called `UpdateLogInfo()` *after* the capture so the `lblLastSuccess` line updates to the just-captured `_lastSuccessfulRenderTime`. The kickoff §3.5 had the timestamp update inside `UpdateLogInfo` but didn't specify the call order in the success path — calling it after the capture is the only ordering that puts the right value on the label.

---

## 3. What didn't change

Items the kickoff §5 listed as out-of-scope, confirmed untouched:

- No `UI/Controls/*.vb` modifications. The new `StaleOverlayPanel` lives as a private nested class inside `MainForm_Render_Cards.vb` — does not touch the locked controls folder.
- `txtOutput` and the legacy RTF helpers stay live. The verification-dump card still receives the SKIPPED text on every skip cycle.
- `MainForm.Designer.vb` not edited.
- No engine, scoring, indicator, or `Core/` changes.
- `settings.json` unchanged.
- CSV schema unchanged.
- SC column / TOTAL parity (Spec C) deferred per kickoff.
- No remote push.

---

## 4. Files touched

```
commit 0 (3c72cec):
  tools/screenshot-mainform.ps1   (new)
  .gitignore                       (verify/ added)

commit 1 (c8ebac6):
  UI/MainForm_Layout.vb            (state fields + lblLastSuccess wiring + row1 height)
  UI/MainForm_Analysis.vb          (skip-branch rewrite + capture site)
  UI/MainForm_Render_Header.vb     (UpdateLogInfo extension)
  UI/MainForm_Render_Cards.vb      (InitVerdictCard refactor + SKIPPED panel +
                                     RenderSkippedDashboard + ClearStaleOverlays v1)

commit 2 (5f724ad):
  UI/MainForm_Render_Cards.vb      (StaleOverlayPanel subclass +
                                     ApplyStaleOverlayToCards +
                                     RemoveStaleOverlayControls +
                                     pill composition)
```

`DeribitClient.vb` — net diff zero. Approach A force-skip applied + reverted four times during verification (success/skip warmup, skip-only, skip-with-overlays, skip+swap-back); `git diff` clean before each commit.

---

## 5. Open items for the next phase

1. **Label dimming through the overlay** (§1.1). Currently the overlay fully occludes underlying card content. If P5 or later wants the trader to read stale values dimly through the tint, the path is in-place `ForeColor` dimming with bookkeep-and-restore. Decide whether the (stale) pill alone is enough signal.

2. **Pill width when minutes ≥ 100.** Fixed 86 px. Realistically the auto-run timer recovers within a few cycles; if anyone deliberately leaves the engine in a sustained skip state for 100+ minutes the pill text will clip. Not a real concern right now.

3. **`verify/click-analyze.ps1` and `verify/resize-window.ps1`** stayed local during this phase (gitignored). Subsequent phases that want one-shot analyze-triggering and form-resizing during self-verification will need to recreate them — worth considering whether to promote them to `tools/` alongside `screenshot-mainform.ps1` if the pattern keeps recurring.

4. **P5 deletion of `txtOutput`.** The skip branch still writes the legacy "ANALYSIS SKIPPED: …" lines to `txtOutput` for verification-dump parity. P5 deletes both the `txtOutput` write and the legacy `AppendRtf` helper.

---

**End of spec-back.**
