# Spec: OFI Momentum Modifier
**Proposed:** 2026-04-27
**Status:** PROPOSED — pending user approval
**Target files:** `Core/IndicatorResults.vb`, `Core/Indicators_OrderFlow.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Layout.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_Render_Sections.vb`, `settings.json`

---

## 1. Problem Statement

The current OFI signal is a **single-snapshot ratio** of weighted bid volume to weighted ask volume. The classification (`BUY DOMINANT` / `SELL DOMINANT` / `BALANCED`) reflects the order book *now*. It discards the *trajectory* of order book pressure across the recent run history.

Concretely, two scenarios produce identical `OFIRatio = 1.5`:

- A) Ratio steady at ~1.5 across last 5 runs → bid-leaning but stable, no urgency
- B) Ratio rising 0.8 → 1.0 → 1.2 → 1.4 → 1.5 → bid pressure *building*, leading-indicator signal

The level is the same; the direction of change is opposite in information content. Scenario B is a much stronger BUY DOMINANT signal than the level alone suggests; A is borderline noise.

This proposal adds an `OFIMomentum` field (RISING / FALLING / FLAT) to `IndicatorResults`, computed from a short rolling window of OFI ratio samples. It feeds the scoring pipeline as a **modifier on the existing OFI signal** — confirming the level when momentum agrees, suppressing the level when momentum contradicts it. Pattern matches `FundingMomentum` (`docs/funding-rate-momentum-proposal.md`) almost exactly; the implementation is heavily template-based.

Zero new API calls. No new data sources.

**Correlation note (per trader-profile Section 7).** OFIMomentum is OFI-derived and therefore highest-correlation-risk among the proposed indicators. The design must demonstrate it adds information rather than re-encoding OFI. The chosen integration (modifier, not standalone score) ensures momentum can only **suppress** an OFI vote when it conflicts and **confirm** when aligned — it cannot independently award a score. This avoids double-counting.

---

## 2. OFIMomentum Computation

### 2a. Data Source

`r.OFIRatio` is computed every run by `CalcOFI` in `Indicators_OrderFlow.vb`. We append it to a ring buffer (`_ofiHistory`) in `MainForm_Layout` after the call site, identical pattern to `_fundingHistory`.

### 2b. Signal Logic

```
Given: ofiHistory = List(Of Double), most recent last
       cfg.Indicators.OFI.MomentumWindow    (default 3)
       cfg.Indicators.OFI.MomentumThreshold (default 0.15)

Compute:
  If ofiHistory.Count < 2 → OFIMomentum = "FLAT"   ← cold-start fallback, accepted

  recent   = ofiHistory.Last()
  priorIdx = Math.Max(0, ofiHistory.Count - 1 - MomentumWindow)
  delta    = recent - prior

  If delta >  MomentumThreshold → "RISING"
  If delta < -MomentumThreshold → "FALLING"
  Else                          → "FLAT"
```

**Threshold rationale.** OFI ratio in BTC-PERPETUAL typically swings between 0.5 and 2.0 during normal flow. A delta of 0.15 over 3 samples represents a meaningful drift — roughly 15% of the BALANCED-zone half-width — without firing on tick noise. Default tunable; review after 50+ live runs.

Add to `IndicatorResults.vb`:
```vb
Public Property OFIMomentum As String  ' "RISING" | "FALLING" | "FLAT"
```

---

## 3. Scoring Integration

### 3a. Position in Pipeline

The OFI base score (`AddFull(state, ofiBuy, ofiSell, SignalCategory.Microstructure)`) fires as it does today. **OFIMomentum acts as a post-OFI gate** — modifying the score *after* the base award, *before* Pass 2 partial upgrades.

This is positioned in `RunScoringPipeline()` immediately after the existing OFI block.

### 3b. Scoring Logic

```vb
' OFI Momentum Modifier --
'  - Confirmed: OFI base signal fires AND momentum agrees → small bonus
'  - Suppressed: OFI base signal fires AND momentum disagrees → cancel base award
'  - FLAT momentum: no effect (base award stands)
'
' Cap: bonus is at most cfg.Indicators.OFI.MomentumBonus (default 1) and total
' OFI contribution can never exceed regimeMax. Suppression simply removes the
' base +1; it never goes negative.
Dim ofiMomNote As String = ""
If cfg.Indicators.OFI.MomentumEnabled AndAlso ofiBuy <> ofiSell Then
    Dim bonus As Integer = cfg.Indicators.OFI.MomentumBonus
    If ofiBuy AndAlso r.OFIMomentum = "RISING" Then
        state.LongScore = Math.Min(state.LongScore + bonus, regimeMax)
        ofiMomNote = String.Format(" | MOM:RISING +{0}[L] confirmed", bonus)
    ElseIf ofiSell AndAlso r.OFIMomentum = "FALLING" Then
        state.ShortScore = Math.Min(state.ShortScore + bonus, regimeMax)
        ofiMomNote = String.Format(" | MOM:FALLING +{0}[S] confirmed", bonus)
    ElseIf ofiBuy AndAlso r.OFIMomentum = "FALLING" Then
        ' Base OFI says BUY, but pressure is unwinding. Cancel the base award.
        state.LongScore = Math.Max(0, state.LongScore - 1)
        state.FullLongCategories.Remove(SignalCategory.Microstructure)
        ofiMomNote = " | MOM:FALLING -1[L] suppressed (unwinding)"
    ElseIf ofiSell AndAlso r.OFIMomentum = "RISING" Then
        ' Base OFI says SELL, but pressure is unwinding. Cancel the base award.
        state.ShortScore = Math.Max(0, state.ShortScore - 1)
        state.FullShortCategories.Remove(SignalCategory.Microstructure)
        ofiMomNote = " | MOM:RISING -1[S] suppressed (unwinding)"
    End If
End If
```

**Note on category set membership:** the `state.FullLongCategories` set tracks which categories have a full-strength long signal, used by `HasCrossConfirm()` for partial upgrades. When OFI is suppressed, we must remove `Microstructure` from that set if no other Microstructure signal already filled it — otherwise downstream Pass 2 partial upgrades (e.g. VWAP partial) might still see Microstructure as confirmed when OFI was the only one. Implementation: track whether OFI was the *sole* contributor to Microstructure; only `Remove()` if so. See implementation note Q2.

### 3c. SignalBreakdown Note

Update the existing OFI breakdown row to append `ofiMomNote`:

```vb
breakdown.Add(New SignalBreakdownItem("OFI", ofiBuy, ofiSell,
    String.Format("Ratio:{0:F2} | {1} | MOM:{2}{3}",
                  r.OFIRatio, r.OFISignal, r.OFIMomentum, ofiMomNote)))
```

---

## 4. Display Integration

Update the existing ORDER FLOW row in `MainForm_Render_Sections.vb` to surface momentum:

```vb
AppendRtf(rtb, "  OFI Ratio: ", C_LABEL)
Dim ofiColour As Color = If(r.OFIRatio > 1.2, C_GOOD, If(r.OFIRatio < 0.8, C_BAD, C_VALUE))
AppendRtf(rtb, String.Format("{0:F2}  |  Bid Vol: {1:F0}  |  Ask Vol: {2:F0}  |  {3}  |  Mom: {4}",
                              r.OFIRatio, r.OFIBidVol, r.OFIAskVol, r.OFISignal,
                              r.OFIMomentum) & Environment.NewLine, ofiColour)
```

Optionally colour the Mom token: RISING green when also BUY DOMINANT, RED when contradicting. Implementation detail — keep simple in v1, just append the text.

---

## 5. Ring Buffer Implementation

### `UI/MainForm_Layout.vb` — add alongside `_fundingHistory`

```vb
' OFI ratio history ring buffer -- for OFIMomentum computation in scoring.
' Populated in RunAnalysisAsync after CalcOFI(); max OFIHistoryMax samples.
' Cold start (< 2 samples) returns FLAT from CalcOFIMomentum -- accepted warm-up.
Private _ofiHistory As New List(Of Double)
Private Const OFIHistoryMax As Integer = 10
```

### `UI/MainForm_Analysis.vb` — after `CalcOFI` returns

```vb
' OFI momentum: append to ring buffer, then compute momentum signal.
_ofiHistory.Add(r.OFIRatio)
If _ofiHistory.Count > OFIHistoryMax Then _ofiHistory.RemoveAt(0)
r.OFIMomentum = IndicatorEngine.CalcOFIMomentum(_ofiHistory, cfg)
```

### `Core/Indicators_OrderFlow.vb` — new shared function

```vb
''' <summary>
''' Derives OFI momentum from a rolling history of OFI ratio samples.
''' Returns "RISING", "FALLING", or "FLAT".
''' Cold start (fewer than 2 samples) returns "FLAT".
''' </summary>
Public Shared Function CalcOFIMomentum(
    history As List(Of Double),
    cfg     As EngineSettings) As String

    If history Is Nothing OrElse history.Count < 2 Then Return "FLAT"

    Dim window   As Integer = cfg.Indicators.OFI.MomentumWindow
    Dim priorIdx As Integer = Math.Max(0, history.Count - 1 - window)
    Dim delta    As Double  = history(history.Count - 1) - history(priorIdx)

    If delta >  cfg.Indicators.OFI.MomentumThreshold Then Return "RISING"
    If delta < -cfg.Indicators.OFI.MomentumThreshold Then Return "FALLING"
    Return "FLAT"
End Function
```

---

## 6. Worked Examples

### Example A — Confirmed: BUY DOMINANT + RISING

```
History: [0.9, 1.1, 1.4, 1.7, 2.1]
MomentumWindow=3, recent=2.1, prior=history[1]=1.1
delta = +1.0 > MomentumThreshold (0.15) → RISING

OFIRatio = 2.1 > BuyDominantRatio (2.0) → ofiBuy = True
Base award: state.LongScore += 1 (Microstructure)

Modifier: ofiBuy AND RISING → confirmed
  state.LongScore += MomentumBonus (1) → +1 more
  ofiMomNote = " | MOM:RISING +1[L] confirmed"

Net OFI contribution: +2 to long score.
Breakdown: "OFI [L] -- Ratio:2.10 | BUY DOMINANT | MOM:RISING | MOM:RISING +1[L] confirmed"
```

### Example B — Suppressed: BUY DOMINANT + FALLING (unwinding)

```
History: [2.5, 2.3, 2.1, 1.9, 1.7]
delta = 1.7 - 2.3 = -0.6 → FALLING

OFIRatio = 1.7 -- still > 1.2 BALANCED upper but not > 2.0. Hmm, set this differently:

OFIRatio = 2.1 (just over BuyDominantRatio 2.0)
History recent: 2.4 → 2.3 → 2.2 → 2.1 → 2.1
delta = 2.1 - 2.3 = -0.2 → FALLING
ofiBuy = True (level still firing)

Modifier: ofiBuy AND FALLING → suppressed
  state.LongScore -= 1 → cancel the base award
  state.FullLongCategories.Remove(Microstructure)  ← only if OFI was sole contributor
  ofiMomNote = " | MOM:FALLING -1[L] suppressed (unwinding)"

Net OFI contribution: 0 to long score (wash). The level signal was a trailing flag; momentum reveals pressure has peaked and is releasing.
Breakdown: "OFI [L] -- Ratio:2.10 | BUY DOMINANT | MOM:FALLING | MOM:FALLING -1[L] suppressed (unwinding)"
```

### Example C — FLAT momentum, no effect

```
History: [1.4, 1.45, 1.42, 1.40, 1.43]
delta = 1.43 - 1.45 = -0.02 → |delta| < threshold (0.15) → FLAT

OFIRatio = 1.43 → BALANCED, ofiBuy = ofiSell = False
Modifier: ofiBuy <> ofiSell condition fails (both False) → no-op
Base award: also nothing fires (no level signal)
Net OFI contribution: 0. ofiMomNote = ""
Breakdown: "OFI -- Ratio:1.43 | BALANCED | MOM:FLAT"
```

### Example D — Cold start

```
First run after engine starts: _ofiHistory has 1 sample.
CalcOFIMomentum returns "FLAT" (history.Count < 2 fallback).
No modifier fires regardless of ofiBuy/ofiSell.
Acceptable warm-up behaviour.
```

---

## 7. Files Changed Summary

| File | Change |
|---|---|
| `Core/IndicatorResults.vb` | Add `OFIMomentum As String` property |
| `Core/Indicators_OrderFlow.vb` | Add `CalcOFIMomentum()` shared function |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Add OFI momentum modifier block immediately after the existing OFI scoring; update OFI breakdown row note |
| `Core/Settings/EngineSettings.vb` | Add `MomentumEnabled` / `MomentumWindow` / `MomentumThreshold` / `MomentumBonus` to `OfiSettings` |
| `UI/MainForm_Layout.vb` | Add `_ofiHistory` + `OFIHistoryMax = 10` |
| `UI/MainForm_Analysis.vb` | Append to `_ofiHistory` after `CalcOFI()`; call `CalcOFIMomentum()` |
| `UI/MainForm_Render_Sections.vb` | Append Mom token to existing OFI line |
| `settings.json` | Extend `indicators.ofi` block with 4 momentum keys |

No changes to MTF gate, Pass 2c, Kelly, ATR levels, regime classification.

---

## 8. Settings Keys

### `Core/Settings/EngineSettings.vb` — extend `OfiSettings`

```vb
Public Class OfiSettings
    <JsonPropertyName("book_depth")>          Public Property BookDepth         As Integer = 5
    <JsonPropertyName("buy_dominant_ratio")>  Public Property BuyDominantRatio  As Double  = 2.0
    <JsonPropertyName("sell_dominant_ratio")> Public Property SellDominantRatio As Double  = 0.5
    ''' <summary>Master switch for OFI momentum modifier. Default True.</summary>
    <JsonPropertyName("momentum_enabled")>   Public Property MomentumEnabled   As Boolean = True
    ''' <summary>Lookback sample count for OFI ratio delta. Default 3.</summary>
    <JsonPropertyName("momentum_window")>    Public Property MomentumWindow    As Integer = 3
    ''' <summary>Min absolute delta to classify as RISING/FALLING. Default 0.15.</summary>
    <JsonPropertyName("momentum_threshold")> Public Property MomentumThreshold As Double  = 0.15
    ''' <summary>Bonus added when OFI level + momentum confirm. Default 1.</summary>
    <JsonPropertyName("momentum_bonus")>     Public Property MomentumBonus     As Integer = 1
End Class
```

### `settings.json` — extend `indicators.ofi`

```json
"OFI": {
  "book_depth": 5,
  "buy_dominant_ratio": 2.0,
  "sell_dominant_ratio": 0.5,
  "momentum_enabled": true,
  "momentum_window": 3,
  "momentum_threshold": 0.15,
  "momentum_bonus": 1
}
```

| Key | Default | Purpose |
|---|---|---|
| `momentum_enabled` | true | Master switch — set false to bypass modifier entirely |
| `momentum_window` | 3 | Samples to look back for delta |
| `momentum_threshold` | 0.15 | Min ratio delta to call RISING/FALLING |
| `momentum_bonus` | 1 | Score added when level + momentum confirm; suppression always removes the full base award (1) |

---

## 9. What This Does NOT Do

- Does **not** add a standalone OFI Momentum row to the breakdown (modifier on existing OFI row, per anti-double-counting rule)
- Does **not** create a new `SignalCategory` enum value — momentum lives in the same Microstructure category as OFI base
- Does **not** fire when OFI base is BALANCED (no level signal to modify)
- Does **not** add new API calls, new fetches, or change `CalcOFI` itself
- Does **not** add a CSV column initially (Section 12 backlog after 50+ runs)

---

## 10. Validation Plan

After 50+ live runs:

1. **Suppression rate.** What % of OFI BUY DOMINANT / SELL DOMINANT base signals get suppressed by FALLING / RISING momentum? If >50%, the threshold (0.15) may be too aggressive and is filtering valid signals.
2. **Confirmation impact.** Of runs where momentum confirmed (bonus +1 fired), did the resulting verdict outperform an equivalent run with bonus disabled? Requires CSV column (deferred).
3. **Threshold tuning.** Review `analysis_log.csv` distribution of OFI ratio over a session — confirm 0.15 covers genuine drift without firing on adjacent-tick noise.

Add to Section 12 backlog after implementation.

---

## 11. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should OFI momentum be a standalone score row instead of a modifier? | No — would double-count OFI category, violates trader-profile rule | Resolved |
| Q2 | When suppressing OFI, should we always remove `Microstructure` from `FullLongCategories` set, or only when OFI was sole contributor? | **Only when sole contributor.** Implementation: before suppression, check whether *any* other Microstructure-category signal had fired in this run (`vwapLong`, `cvdLong`, etc.). If yes, leave the set membership alone. If no, remove. The implementation should track this via a small bookkeeping check, not by re-evaluating each signal. | Resolved |
| Q3 | Threshold of 0.15 — too tight for tick noise? | Best initial guess. OFI ratio commonly drifts 0.05–0.10 between snapshots in steady flow; 0.15 catches genuine pressure shifts. Calibrate after 50+ runs | Open — calibration |
| Q4 | Should momentum gate fire when ofiBuy/ofiSell *level* is BALANCED? | No — modifier only acts on existing level signals. A standalone "rising OFI in BALANCED zone" would be a separate signal class and risks double-counting | Resolved |
| Q5 | Should the modifier interact with the Pass 2c regime alignment gate? | No — Pass 2c reads CVD, not OFI. OFI sits earlier in the pipeline. The two gates are independent | Resolved |
| Q6 | What about OFI momentum during VWAP warmup? | No special handling needed — OFI is independent of VWAP state | Resolved |
