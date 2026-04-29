# Spec: v17 Follow-up Fixes — VerdictContext Precedence + Section 16.3 Doc Marker
**Proposed:** 2026-04-29
**Status:** APPROVED 2026-04-29
**Target files:** `Core/ScoringEngine_Calculate_Scoring.vb`, `docs/DeribitIndicatorProject.md`

Two small fixes flagged during the v17 code review. Both are low-risk and small-surface. Bundled into one spec because they're related to the same review pass and ship cleanly as a single logical commit.

---

## 1. Problem Statement

### 1a. VerdictContext STRUCTURALLY_WEAK precedence

The swing-pivot spec (`docs/swing-pivot-proposal.md` Section 7) added a new STRUCTURALLY_WEAK condition to `CalcVerdictContext`: when swing detection has produced at least one pivot but the entry direction lacks a clean target+stop pair, classify as STRUCTURALLY_WEAK.

The spec wording said *"first-priority STRUCTURALLY_WEAK condition, before the existing signal-count check."* Sonnet 4.6 implemented this faithfully by placing the new check at the **top** of `CalcVerdictContext` — before the MOMENTUM_FADING check.

Side effect: when both the structural-target and momentum-fading conditions would fire, STRUCTURALLY_WEAK now wins. Pre-spec behaviour was MOMENTUM_FADING priority (the new check didn't exist).

This is wrong for accuracy reasons:

- **MOMENTUM_FADING is rarer and stronger.** Its trigger requires 2+ confirming signals from independent indicators (BULL_DECEL/BEAR_DECEL micro, BULL_FADING/BEAR_FADING TTM, RSI extreme, MicroCVD decay ratio). When it fires, the engine is making a high-conviction "this move is dying" call.
- **STRUCTURALLY_WEAK from missing swing target is frequent and often transient.** Fires during consolidation, just-broke-out scenarios, and any time pivots haven't formed yet in the entry direction. The next swing usually forms within ~30 min of price action.
- **Frequency asymmetry causes signal masking.** STRUCTURALLY_WEAK firing more often means MOMENTUM_FADING gets swallowed when both would apply. The rarer, more diagnostic signal disappears from the display *and* from the future CSV log used by the auto-tweaker (`docs/analysis-log-csv-expansion-proposal.md`). Calibration data becomes harder to interpret.
- **In-position structural concerns are already handled in `CalcHoldStatus` Layer 1.5** (structural break exit via prior swing low/high). VerdictContext is a per-analysis classification, not in-position guidance — its job is to surface the most diagnostically useful warning, which is the rarer signal.

The fix: relocate the new STRUCTURALLY_WEAK check to fire **after** MOMENTUM_FADING, before the existing FLOW_UNCONFIRMED / signal-count STRUCTURALLY_WEAK checks.

Single-tag classification preserved. No display change. No CSV schema change. No new tunables.

### 1b. Section 16.3 item 1 ✅ marker

`docs/DeribitIndicatorProject.md` Section 16.3 prerequisite item 1 currently reads:

```
1. Accuracy plateau. ✅ All six indicator/feature specs (...) shipped 2026-04-29.
   Engine verdict accuracy must now stabilise across 100+ live runs before
   further structural changes.
```

The ✅ marker is misleading. The prerequisite is "accuracy plateau" — which requires both (a) specs shipping AND (b) accuracy stabilising across 100+ live runs. Only (a) is satisfied. A reader scanning for green checks could conclude the prerequisite is met when 100+ runs hasn't happened yet.

Fix: split item 1 into 1a (specs shipped — ✅) and 1b (100+ runs validation — ⏳).

---

## 2. Fix 1 — Precedence Move

### 2a. Current Code (in `RunScoringPipeline`'s `CalcVerdictContext` function in `Core/ScoringEngine_Calculate_Scoring.vb`)

```vb
Private Shared Function CalcVerdictContext(...) As String
    Dim isLong As Boolean = (v.LongScore >= v.ShortScore)

    Dim structScore As Integer = 0
    Dim flowScore   As Integer = 0
    For Each item In v.SignalBreakdown
        ' ... compute structScore / flowScore ...
    Next

    ' First check: is there a clean structural target in the entry direction?
    ' Fires only when swing detection has produced at least one level (graceful degradation
    ' when candle history is too short). Both target AND stop must be present for a clean trade;
    ' missing target alone (e.g. just made a new high) is structural ambiguity → STRUCTURALLY_WEAK.
    Dim hasStructuralTarget As Boolean = If(isLong,
        r.SwingTargetLong > 0 AndAlso r.SwingStopLong > 0,
        r.SwingTargetShort > 0 AndAlso r.SwingStopShort > 0)
    If r.LastSwingHigh5m > 0 OrElse r.LastSwingLow5m > 0 Then
        ' Swing detection has produced at least one level — meaningful evaluation possible
        If Not hasStructuralTarget Then
            Return "STRUCTURALLY_WEAK"
        End If
    End If

    Dim ctx = cfg.Scoring.ContextTag
    Dim fadingCount As Integer = 0
    If isLong Then
        ' ... compute fadingCount ...
    Else
        ' ... compute fadingCount ...
    End If
    If fadingCount >= ctx.MomentumFadingCountMin Then Return "MOMENTUM_FADING"

    If structScore >= cfg.Scoring.ContextTagStructuralMin AndAlso
       flowScore <= cfg.Scoring.ContextTagFlowMax Then
        Return "FLOW_UNCONFIRMED"
    End If

    If structScore < ctx.StructurallyWeakStructMin AndAlso flowScore < ctx.StructurallyWeakFlowMin Then
        Return "STRUCTURALLY_WEAK"
    End If

    Return "CONFIRMED"
End Function
```

### 2b. Updated Code

Move the structural-target block to **after** the MOMENTUM_FADING return, **before** the FLOW_UNCONFIRMED check:

```vb
Private Shared Function CalcVerdictContext(...) As String
    Dim isLong As Boolean = (v.LongScore >= v.ShortScore)

    Dim structScore As Integer = 0
    Dim flowScore   As Integer = 0
    For Each item In v.SignalBreakdown
        ' ... compute structScore / flowScore ...
    Next

    Dim ctx = cfg.Scoring.ContextTag
    Dim fadingCount As Integer = 0
    If isLong Then
        ' ... compute fadingCount ...
    Else
        ' ... compute fadingCount ...
    End If
    If fadingCount >= ctx.MomentumFadingCountMin Then Return "MOMENTUM_FADING"

    ' Structural-target check: if swing detection has run AND the entry direction
    ' has no clean target+stop pair, classify as STRUCTURALLY_WEAK before falling
    ' through to the signal-count classifiers. Fires after MOMENTUM_FADING so the
    ' rarer / stronger fading signal isn't masked by a frequent / transient
    ' structural-target absence (per v17-followup-fixes-proposal.md).
    Dim hasStructuralTarget As Boolean = If(isLong,
        r.SwingTargetLong > 0 AndAlso r.SwingStopLong > 0,
        r.SwingTargetShort > 0 AndAlso r.SwingStopShort > 0)
    If r.LastSwingHigh5m > 0 OrElse r.LastSwingLow5m > 0 Then
        If Not hasStructuralTarget Then
            Return "STRUCTURALLY_WEAK"
        End If
    End If

    If structScore >= cfg.Scoring.ContextTagStructuralMin AndAlso
       flowScore <= cfg.Scoring.ContextTagFlowMax Then
        Return "FLOW_UNCONFIRMED"
    End If

    If structScore < ctx.StructurallyWeakStructMin AndAlso flowScore < ctx.StructurallyWeakFlowMin Then
        Return "STRUCTURALLY_WEAK"
    End If

    Return "CONFIRMED"
End Function
```

The block is identical content; only its position changes. Net diff: ~10 lines moved.

### 2c. Resulting Classification Priority

After the fix:

```
1. MOMENTUM_FADING       (rarer, stronger — fires first)
2. STRUCTURALLY_WEAK     (from missing swing target — new check)
3. FLOW_UNCONFIRMED      (existing signal-count classifier)
4. STRUCTURALLY_WEAK     (existing signal-count classifier — fallback)
5. CONFIRMED             (default)
```

A run with both fading momentum and a missing structural target now classifies as MOMENTUM_FADING. A run with a missing structural target but no fading momentum still classifies as STRUCTURALLY_WEAK (the new check). A run with neither falls through to existing classifiers, unchanged.

### 2d. Why Not Both-Fire (Composite Tag)?

Considered: a composite tag like `MOMENTUM_FADING_NO_TARGET` to surface both warnings. **Rejected:**

- Display is one-line. CSV is one column. Composite tags require display redesign (two CONTEXT lines? mixed colours?) and CSV schema changes (multiple columns or comma-split values). Combinatorial cost — 4+ new tags possible — for marginal diagnostic benefit.
- Tag is meant to be glanceable. Compound tags break that.
- Single-tag with proper priority ordering is the established design. The fix preserves it.

If post-calibration data shows users would benefit from seeing both warnings in some specific case, that's a future spec.

---

## 3. Fix 2 — Section 16.3 Doc Marker Split

### 3a. Current Text

`docs/DeribitIndicatorProject.md` Section 16.3:

```
### 16.3 KIV Prerequisites

Before either 16.1 or 16.2 should be specced and scheduled:

1. **Accuracy plateau.** ✅ All six indicator/feature specs (bid-ask-spread, OFI momentum, dynamic MicroCVD, VPFR-lite v2, swing pivots, settings-exposure) shipped 2026-04-29. Engine verdict accuracy must now stabilise across 100+ live runs before further structural changes.
2. **Settings exposure pass complete.** ✅ ...
```

### 3b. Updated Text

```
### 16.3 KIV Prerequisites

Before either 16.1 or 16.2 should be specced and scheduled:

1a. **Indicator/feature specs shipped.** ✅ All six (bid-ask-spread, OFI momentum, dynamic MicroCVD, VPFR-lite v2, swing pivots, settings-exposure) shipped 2026-04-29.
1b. **Accuracy plateau.** ⏳ Engine verdict accuracy must stabilise across 100+ live runs before further structural changes. Calibration accumulation begins after the v0.3 CSV expansion ships (`docs/analysis-log-csv-expansion-proposal.md`).
2. **Settings exposure pass complete.** ✅ ...
```

The 1a/1b split keeps the original numbering (item 2 still item 2) and makes the partial-completion state visible. Reader can no longer mistake "specs shipped" for "accuracy validated."

---

## 4. Files Changed Summary

| File | Change |
|---|---|
| `Core/ScoringEngine_Calculate_Scoring.vb` | Move structural-target STRUCTURALLY_WEAK block from top of `CalcVerdictContext` to after the MOMENTUM_FADING return statement. ~10 lines relocated; comment updated to reference this proposal. |
| `docs/DeribitIndicatorProject.md` | Split Section 16.3 item 1 into 1a (specs shipped — ✅) and 1b (accuracy plateau — ⏳). |

Approximate diff: ~15 lines code change + ~3 lines doc change.

---

## 5. Settings Keys

**None.** Both fixes are structural — no new tunables.

---

## 6. What This Does NOT Do

- Does **not** add a composite tag (e.g. `MOMENTUM_FADING_NO_TARGET`). Single-tag design preserved.
- Does **not** change MOMENTUM_FADING firing logic — only its priority relative to the new STRUCTURALLY_WEAK check.
- Does **not** change the FLOW_UNCONFIRMED or fallback STRUCTURALLY_WEAK logic — both still fire as before.
- Does **not** affect display, CSV, MTF gate, Pass 2c, Kelly, ATR levels, regime classification, or any indicator.
- Does **not** add CSV columns. Schema unchanged.
- Does **not** bump `settings.json` version — no settings change.

---

## 7. Validation Plan

1. **Build clean:** `dotnet build` returns 0 warnings, 0 errors.
2. **Smoke test the precedence fix:**
   - Run 5–10 analyses across a session.
   - Confirm: when CONTEXT line shows MOMENTUM_FADING, the swing structural state is whatever it is — fading wins regardless. When CONTEXT shows STRUCTURALLY_WEAK from the new check, fading is NOT firing (otherwise MOMENTUM_FADING would have fired first).
   - Visual inspection only — no CSV column for VerdictContext yet (lands with the v0.3 CSV expansion spec).
3. **Doc fix verification:** open `docs/DeribitIndicatorProject.md` Section 16.3 and confirm the 1a/1b split renders correctly.

---

## 8. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Why not surface both warnings (composite tag)? | Display is single-line, CSV is single-column, combinatorial complexity not justified by marginal diagnostic gain. Single-tag with proper priority ordering preserved | Resolved |
| Q2 | Why MOMENTUM_FADING priority over STRUCTURALLY_WEAK? | MOMENTUM_FADING is rarer (requires 2+ confirming signals from independent indicators) and stronger; STRUCTURALLY_WEAK from missing swing target is frequent and often transient. Masking the rarer signal degrades both display value and auto-tweaker calibration data | Resolved |
| Q3 | What about runs early in a session where swing detection hasn't produced any level yet (`r.LastSwingHigh5m = 0 AndAlso r.LastSwingLow5m = 0`)? | New check has the existing graceful-degradation guard: `If r.LastSwingHigh5m > 0 OrElse r.LastSwingLow5m > 0 Then ...`. When neither is present, the structural-target check is skipped entirely (no STRUCTURALLY_WEAK from this path). Falls through to existing signal-count classifiers. Unchanged from current behaviour | Resolved |
| Q4 | Will this fix change the behaviour of existing CONFIRMED outputs? | No. CONFIRMED only fires when none of the warning classifiers match. Reordering the warnings doesn't change which runs are CONFIRMED — only which warning fires when multiple would | Resolved |
| Q5 | Should this commit be combined with the CSV expansion (`analysis-log-csv-expansion-proposal.md`) implementation? | No — keep them separate commits. CSV expansion is a schema-only mechanical pass; this is a behavioural fix. Different commit messages, different review concerns. Either order is fine | Resolved |
| Q6 | Does the doc fix need its own commit or can it ride with the code fix? | Either is fine — small enough to ride. Recommend including in the same commit as the code fix, since both are v17-followup items and the spec covers both | Resolved |
