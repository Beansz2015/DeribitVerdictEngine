# Spec: Verdict Sub-Context Tag
**Proposed:** 2026-04-13  
**Status:** APPROVED — open questions resolved 2026-04-14  
**Target file:** `Core/ScoringEngine_Calculate.vb` (Step 5b), `Core/ScoringEngine_Types.vb` (VerdictResult), `UI/MainForm_Render.vb` (display)

---

## 1. Problem Statement

A WEAK LONG (or WEAK SHORT) verdict currently has three structurally distinct causes
that require different trader responses, but the engine cannot distinguish between them:

| Case | What it means | Correct trader response |
|---|---|---|
| **FLOW_UNCONFIRMED** | Structural signals aligned (VWAP, EMA, DMI), but order flow (OFI, CVD, TFI, MicroCVD) has not yet confirmed. Price has moved but aggressor volume hasn't backed it. | Wait — setup may be valid but early. Re-run in 1-2 candles. |
| **MOMENTUM_FADING** | Directional signals present but exhaustion indicators firing. RSI extended, MicroCVD decelerating, TTM fading. Move is likely late-stage. | Skip or fade — unfavourable entry timing. |
| **STRUCTURALLY_WEAK** | Neither structure nor flow is strongly aligned. Score is weak because there is genuinely no dominant driver. Noise. | No trade — engine is correct to be low-confidence. |

Without this distinction, a WEAK LONG tempts discretionary entry on all three cases equally,
when only Case 1 (and occasionally Case 1 transitioning to confirmed) warrants attention.

This also affects LONG and STRONG LONG verdicts — a STRONG LONG driven purely by structural
signals with flat order flow is a lower-quality setup than one with both structure and flow
firing. The tag system captures this across all verdict levels.

---

## 2. Proposed Solution

Add a **Step 5b** diagnostic pass in `ScoringEngine_Calculate.Calculate()` that:

1. Runs **after** the verdict is set (Step 5) and **after** MTF veto (Step 4b)
2. Inspects the `ScoreState` and `IndicatorResults` fields already computed
3. Sets a `VerdictContext` string on `VerdictResult` — one of four values:
   - `FLOW_UNCONFIRMED`
   - `MOMENTUM_FADING`
   - `STRUCTURALLY_WEAK`
   - `CONFIRMED` (all tiers aligned — no warning needed)
4. Appends the tag to the verdict display line in `RenderOutput`

No new data fetches. No new indicators. No scoring weight changes.
Pure diagnostic read of already-computed state.

---

## 3. Detection Logic

### 3a. MOMENTUM_FADING — check first (highest priority)

Firing condition (long direction): **2 or more** of the following are true:
- `r.MicroCVDSignal = "BULL_DECEL"` — momentum segmentation shows deceleration
- `r.TTMSignal = "BULL_FADING"` — TTM histogram direction declining
- `r.RSI >= cfg.Indicators.RSI.DivPenaltyRsiHigh` (default 65) — RSI in extended zone
- CVD late-segment weaker than early: `r.MicroCVDLate < r.MicroCVDEarly * 0.5` — late delta less than half early delta

Firing condition (short direction): mirror with BEAR equivalents.

**Rationale:** Requiring 2-of-4 prevents single-signal false positives (e.g. RSI briefly
touching 65 on a healthy trend). All four inputs are already in `IndicatorResults`.

---

### 3b. FLOW_UNCONFIRMED — check second

Firing condition (long direction): **structural score high, flow score low**

Define:
- `structuralLong` = points from: VWAP [L], BBW/TTM [L], EMA [L], DMI [L], ADX [L], Donchian [L], 5m EMA200 [L]
- `flowLong` = points from: OFI [L], CVD [L], TFI [L], MicroCVD [L], OI [L], ROC [L], Volume [L]

Firing condition:
- `structuralLong >= 3` (meaningful structural alignment)
- `flowLong <= 1` (order flow not confirming)

Firing condition (short direction): mirror.

**Rationale:** Threshold of 3 structural / ≤1 flow is conservative enough to avoid
firing on legitimate setups where one flow signal is intentionally neutral.
Thresholds externalised to `settings.json` (see Section 5).

---

### 3c. STRUCTURALLY_WEAK — check third (catch-all)

Firing condition:
- Neither MOMENTUM_FADING nor FLOW_UNCONFIRMED applies
- Total winning score (long or short) is at or below the WEAK threshold
- `structuralLong < 2` AND `flowLong < 2` — no dominant driver in either tier

**Rationale:** This is the residual case — low score with no identifiable cause means
genuine noise or transition. No specific intervention needed beyond the NO TRADE
or WEAK verdict already emitted.

---

### 3d. CONFIRMED — default

Set when none of the above conditions fire. Indicates cross-tier alignment.
Not displayed in the verdict line to keep the output clean (absence of tag = confirmed).

---

## 4. Code Changes

### 4a. `Core/ScoringEngine_Types.vb`

Add one property to `VerdictResult`:

```vb
''' <summary>
''' Post-scoring diagnostic context for weak/ambiguous verdicts.
''' Values: FLOW_UNCONFIRMED | MOMENTUM_FADING | STRUCTURALLY_WEAK | CONFIRMED
''' CONFIRMED is not displayed -- absence of tag means all tiers aligned.
''' </summary>
Public Property VerdictContext As String = "CONFIRMED"
```

---

### 4b. `Core/ScoringEngine_Calculate.vb`

Add Step 5b after Step 5 (threshold comparison) and before Step 6 (CalcHoldStatus).
Insert a private helper `CalcVerdictContext` to keep `Calculate()` readable.

```vb
' Step 5b: Verdict sub-context diagnostic
v.VerdictContext = CalcVerdictContext(v, r, state, cfg)
```

```vb
Private Shared Function CalcVerdictContext(
    v       As VerdictResult,
    r       As IndicatorResults,
    state   As ScoreState,
    cfg     As EngineSettings) As String

    ' Determine which direction is dominant for context evaluation
    Dim isLong As Boolean = (v.LongScore >= v.ShortScore)

    ' --- Build tier sub-scores from SignalBreakdown ---
    ' Structural signals: VWAP, BBW/TTM, EMA 9/21/50, DMI +/-DI, ADX>*, Donchian(20), 5m EMA(200)
    ' Flow signals: OFI, CVD, TFI, MicroCVD, OI Delta, ROC(9), Volume
    ' NOTE: ADX label is dynamic ("ADX>" & adxTrend) -- use StartsWith("ADX>") not exact match.

    Dim structScore As Integer = 0
    Dim flowScore   As Integer = 0
    For Each item In v.SignalBreakdown
        Dim hit As Boolean = If(isLong, item.LongHit, item.ShortHit)
        If Not hit Then Continue For
        Dim lbl As String = item.Label
        ' Structural
        If lbl = "VWAP"         OrElse lbl = "BBW/TTM"     OrElse
           lbl = "EMA 9/21/50"  OrElse lbl = "DMI +/-DI"   OrElse
           lbl.StartsWith("ADX>")                           OrElse
           lbl = "Donchian(20)" OrElse lbl = "5m EMA(200)" Then
            structScore += 1
        End If
        ' Flow
        If lbl = "OFI"    OrElse lbl = "CVD"      OrElse lbl = "TFI" OrElse
           lbl = "MicroCVD" OrElse lbl = "OI Delta" OrElse
           lbl = "ROC(9)"  OrElse lbl = "Volume"   Then
            flowScore += 1
        End If
    Next

    ' --- Check MOMENTUM_FADING first (highest priority) ---
    Dim fadingCount As Integer = 0
    If isLong Then
        If r.MicroCVDSignal = "BULL_DECEL"                                    Then fadingCount += 1
        If r.TTMSignal = "BULL_FADING"                                         Then fadingCount += 1
        If r.RSI >= cfg.Indicators.RSI.DivPenaltyRsiHigh                       Then fadingCount += 1
        If r.MicroCVDEarly > 0 AndAlso
           r.MicroCVDLate < r.MicroCVDEarly * 0.5                              Then fadingCount += 1
    Else
        If r.MicroCVDSignal = "BEAR_DECEL"                                    Then fadingCount += 1
        If r.TTMSignal = "BEAR_FADING"                                         Then fadingCount += 1
        If r.RSI <= cfg.Indicators.RSI.DivPenaltyRsiLow                        Then fadingCount += 1
        If r.MicroCVDEarly < 0 AndAlso
           r.MicroCVDLate > r.MicroCVDEarly * 0.5                              Then fadingCount += 1
    End If
    If fadingCount >= 2 Then Return "MOMENTUM_FADING"

    ' --- Check FLOW_UNCONFIRMED second ---
    If structScore >= cfg.Scoring.ContextTagStructuralMin AndAlso
       flowScore   <= cfg.Scoring.ContextTagFlowMax Then
        Return "FLOW_UNCONFIRMED"
    End If

    ' --- Check STRUCTURALLY_WEAK (catch-all for low-score, no dominant driver) ---
    If structScore < 2 AndAlso flowScore < 2 Then
        Return "STRUCTURALLY_WEAK"
    End If

    Return "CONFIRMED"
End Function
```

---

### 4c. `Core/Settings/EngineSettings.vb`

Add two fields to `ScoringSettings`:

```vb
' Verdict context tag thresholds
Public Property ContextTagStructuralMin As Integer = 3   ' min structural hits to trigger FLOW_UNCONFIRMED
Public Property ContextTagFlowMax       As Integer = 1   ' max flow hits to trigger FLOW_UNCONFIRMED
```

---

### 4d. `settings.json`

Add to `scoring` block:

```json
"context_tag_structural_min": 3,
"context_tag_flow_max": 1
```

---

### 4e. `UI/MainForm_Render.vb`

In the verdict block, append `VerdictContext` when not CONFIRMED:

```vb
' After verdict line, before confidence line:
If v.VerdictContext <> "CONFIRMED" AndAlso v.VerdictContext <> "" Then
    AppendRtf(rtb, "  CONTEXT:    ", C_LABEL)
    Dim ctxColour As Color
    Select Case v.VerdictContext
        Case "MOMENTUM_FADING"   : ctxColour = C_BAD
        Case "FLOW_UNCONFIRMED"  : ctxColour = C_WARN
        Case "STRUCTURALLY_WEAK" : ctxColour = C_DIM
        Case Else                : ctxColour = C_VALUE
    End Select
    AppendRtf(rtb, v.VerdictContext & Environment.NewLine, ctxColour, bold:=True)
End If
```

Output will render as:
```
===========================================================
  VERDICT:    WEAK LONG
  CONTEXT:    FLOW_UNCONFIRMED
  CONFIDENCE: LOW
  SCORE:      Long 8/19  |  Short 1/19
```

For NO TRADE with a vetoed direction:
```
  VERDICT:    NO TRADE [WEAK LONG]
  CONTEXT:    MOMENTUM_FADING
```

---

## 5. Settings Keys Summary

| Key | Default | Purpose |
|---|---|---|
| `context_tag_structural_min` | 3 | Min structural signal hits to trigger FLOW_UNCONFIRMED |
| `context_tag_flow_max` | 1 | Max flow signal hits to trigger FLOW_UNCONFIRMED |

All fading/RSI thresholds reuse existing cfg keys (`div_penalty_rsi_high`, `div_penalty_rsi_low`).
No new indicator parameters required.

---

## 6. Files Changed Summary

| File | Change |
|---|---|
| `Core/ScoringEngine_Types.vb` | Add `VerdictContext As String` to `VerdictResult` |
| `Core/ScoringEngine_Calculate.vb` | Add Step 5b call + `CalcVerdictContext` helper |
| `Core/Settings/EngineSettings.vb` | Add `ContextTagStructuralMin`, `ContextTagFlowMax` to `ScoringSettings` |
| `settings.json` | Add `context_tag_structural_min`, `context_tag_flow_max` |
| `UI/MainForm_Render.vb` | Render `CONTEXT:` line in verdict block when not CONFIRMED |

---

## 7. What This Does NOT Do

- Does **not** change any scores or verdict thresholds
- Does **not** add new indicators or data fetches
- Does **not** affect CalibrationReport or CSV logging (deferred — see Section 10)
- Does **not** override MTF gate or regime veto logic

---

## 8. Calibration Note

The `ContextTagStructuralMin` (3) and `ContextTagFlowMax` (1) defaults are conservative
starting points. After 50+ logged trades, review:
- How often FLOW_UNCONFIRMED precedes a confirmed entry on the next run
- How often MOMENTUM_FADING correctly identifies the last WEAK LONG before reversal

If FLOW_UNCONFIRMED fires too frequently (>40% of WEAK LONGs), raise `ContextTagStructuralMin` to 4.
If MOMENTUM_FADING misses obvious exhaustion, lower the 2-of-4 requirement to 1-of-4 for high RSI only.

---

## 9. Open Questions — RESOLVED 2026-04-14

1. **CSV logging** — **DEFERRED.** Do not add `VerdictContext` to `analysis_log.csv` now.
   Pick up when CalibrationReport is approaching READY threshold (≥300 rows).
   See `DeribitIndicatorProject.md` Section 12 (WATCHING backlog) for the pickup note.

2. **CONFIRMED display** — **SILENT.** Do not render a `CONTEXT:` line when verdict is CONFIRMED.
   Absence of the line is the signal. Adding it would create noise on the majority of runs.

3. **SignalBreakdown label matching** — **RESOLVED.** Labels verified against source in
   `ScoringEngine_Calculate.vb`. One fix applied vs. original spec: ADX label is dynamic
   (`"ADX>" & adxTrend.ToString("F0")`), so matching uses `item.Label.StartsWith("ADX>")`
   instead of exact `HashSet` lookup. All other labels confirmed exact match.

4. **MicroCVDEarly sign guard** — **CONFIRMED.** `MicroCVDEarly` is a net USD delta and can
   be negative. The `> 0` guard in the long-direction fading check is correct — it ensures
   the ratio comparison is directionally meaningful (deceleration of a bullish early segment).
   Short-direction mirror arithmetic (`MicroCVDEarly < 0 AndAlso MicroCVDLate > MicroCVDEarly * 0.5`)
   is also correct: both values are negative, so the condition correctly identifies a
   less-negative late segment (weaker bear pressure).

---

## 10. CSV Logging Pickup Note

When `CalibrationReport` approaches READY (≥300 rows, ≥3 sessions, ≥3 regimes):
- Add `VerdictContext` as a column to `analysis_log.csv` in `AnalysisLogger.LogRun()`
- Update `CalibrationReport` to correlate each context tag with subsequent directional accuracy
- This enables per-tag win rate analysis (e.g. how often does FLOW_UNCONFIRMED resolve
  to a confirmed long on the next run vs. reversing)
