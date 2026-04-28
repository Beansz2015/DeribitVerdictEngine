# Spec: Bid-Ask Spread Microstructure Signal
**Proposed:** 2026-04-27
**Status:** APPROVED 2026-04-28
**Target files:** `Core/IndicatorResults.vb`, `Core/Indicators_OrderFlow.vb`, `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_Render_Sections.vb`, `settings.json`

---

## 1. Problem Statement

The order book is already fetched on every analysis run via `DeribitClient.GetOrderBookAsync(10)`. The best-bid / best-ask spread is computable from data we already pay for, but the engine currently ignores it.

Spread is a **leading microstructure signal**: when market makers widen the spread, they're pricing in execution risk — usually because liquidity is being pulled or volatility is rising before price moves. A widening spread on the entry side typically precedes:

- Liquidity flushes (one side pulls quotes, price gaps through thin levels)
- Funding cascades (forced unwinds eat the available depth)
- Pre-news stillness (MMs anticipate but won't quote tight until clarity)

A tight, stable spread confirms healthy two-way flow and good execution conditions. There's no symmetric reward for tight spread — that's the baseline state of a liquid market.

REST polling caveat: spread you see is a snapshot, not a stream. WebSocket migration would meaningfully improve signal quality. But ignoring spread entirely is strictly worse than reading the snapshot.

Zero new API calls. No new data sources.

---

## 2. SpreadBps Computation

### 2a. Data Source

`orderBook.Bids(0).Price` and `orderBook.Asks(0).Price` are the best bid and best ask. Already fetched.

### 2b. Signal Logic

```
Given: orderBook with at least one bid and one ask level
       cfg.Indicators.Spread.WideThresholdBps  (default 5.0)
       cfg.Indicators.Spread.TightThresholdBps (default 1.5)

Compute:
  bestBid = orderBook.Bids(0).Price
  bestAsk = orderBook.Asks(0).Price
  mid     = (bestBid + bestAsk) / 2
  spread  = bestAsk - bestBid
  spreadBps = (spread / mid) * 10000        ← basis points

  If spreadBps >= WideThresholdBps  → SpreadStatus = "WIDE"
  If spreadBps <= TightThresholdBps → SpreadStatus = "TIGHT"
  Else                              → SpreadStatus = "NORMAL"
```

**Threshold rationale.** BTC-PERPETUAL on Deribit during normal liquid hours runs ~0.5–2 bps spread. NY high-volume periods can compress under 1 bp. Wide thresholds activate above 5 bps — distinctly elevated for BTC perp, suggesting MM caution. Subject to revision after 50+ live runs (added to Section 12 backlog).

Add to `IndicatorResults.vb`:
```vb
Public Property SpreadBps    As Double  ' basis points
Public Property SpreadStatus As String  ' "TIGHT" | "NORMAL" | "WIDE"
```

---

## 3. Scoring Integration

### 3a. Position in Pipeline

Spread is a **directional execution-quality penalty**, not an entry signal. It applies in the scoring pass alongside Liquidations — a pre-existing penalty-only category. Place it after the Liquidations block in `RunScoringPipeline()`, before Pass 2 (partial upgrades).

### 3b. Scoring Logic

```vb
' Spread microstructure penalty -- WIDE spread on either side reduces signal quality
' on that side. Penalty applies to the side aligned with current price velocity:
'   ROC > 0  → penalise long side (entering long into widening ask)
'   ROC < 0  → penalise short side (entering short into widening bid)
'   ROC ~= 0 → penalise both sides (general execution warning)
Dim spreadPenaltyLong  As Integer = 0
Dim spreadPenaltyShort As Integer = 0
If r.SpreadStatus = "WIDE" Then
    Dim pen As Integer = cfg.Scoring.SpreadWidePenalty
    Dim slopeSens As Double = cfg.Indicators.ROC.SlopeSensitivity
    If r.ROC > slopeSens Then
        spreadPenaltyLong = pen
        state.LongScore = Math.Max(0, state.LongScore - pen)
    ElseIf r.ROC < -slopeSens Then
        spreadPenaltyShort = pen
        state.ShortScore = Math.Max(0, state.ShortScore - pen)
    Else
        ' ROC near zero -- penalise both sides equally
        spreadPenaltyLong  = pen
        spreadPenaltyShort = pen
        state.LongScore  = Math.Max(0, state.LongScore  - pen)
        state.ShortScore = Math.Max(0, state.ShortScore - pen)
    End If
End If
```

No reward for TIGHT — staying directional-only per trader-profile (no non-directional padding). NORMAL is the baseline state.

### 3c. SignalBreakdown Note

Add a breakdown row after the existing Liq Penalty row:

```vb
Dim spreadNote As String = String.Format("{0:F2} bps | {1}", r.SpreadBps, r.SpreadStatus)
If spreadPenaltyLong > 0 Then spreadNote &= String.Format(" | PENALTY -{0} [L]", spreadPenaltyLong)
If spreadPenaltyShort > 0 Then spreadNote &= String.Format(" | PENALTY -{0} [S]", spreadPenaltyShort)
breakdown.Add(New SignalBreakdownItem("Spread", spreadPenaltyLong > 0, spreadPenaltyShort > 0, spreadNote))
```

---

## 4. Display Integration

Add a new line to the `ORDER FLOW` section in `MainForm_Render_Sections.vb`, after OFI Ratio:

```vb
AppendRtf(rtb, "  Spread:    ", C_LABEL)
Dim spreadColour As Color = If(r.SpreadStatus = "WIDE", C_BAD,
                                If(r.SpreadStatus = "TIGHT", C_GOOD, C_VALUE))
AppendRtf(rtb, String.Format("{0:F2} bps  |  {1}",
                              r.SpreadBps, r.SpreadStatus) & Environment.NewLine, spreadColour)
```

---

## 5. Call Site Wiring

In `UI/MainForm_Analysis.vb`, after `CalcOFI` (which already consumes `orderBook`):

```vb
IndicatorEngine.CalcSpread(orderBook, r.SpreadBps, r.SpreadStatus,
                           wideThresholdBps:=cfg.Indicators.Spread.WideThresholdBps,
                           tightThresholdBps:=cfg.Indicators.Spread.TightThresholdBps)
```

In `Core/Indicators_OrderFlow.vb`:

```vb
''' <summary>
''' Computes basis-point spread from the best bid/ask of the order book snapshot.
''' Classifies as TIGHT / NORMAL / WIDE against configurable thresholds.
''' </summary>
Public Shared Sub CalcSpread(orderBook As OrderBookSnapshot,
                              ByRef spreadBps As Double,
                              ByRef spreadStatus As String,
                              Optional wideThresholdBps  As Double = 5.0,
                              Optional tightThresholdBps As Double = 1.5)
    spreadBps = 0 : spreadStatus = "NORMAL"
    If orderBook Is Nothing Then Return
    If orderBook.Bids Is Nothing OrElse orderBook.Bids.Count = 0 Then Return
    If orderBook.Asks Is Nothing OrElse orderBook.Asks.Count = 0 Then Return

    Dim bestBid As Double = orderBook.Bids(0).Price
    Dim bestAsk As Double = orderBook.Asks(0).Price
    If bestBid <= 0 OrElse bestAsk <= 0 Then Return
    Dim mid As Double = (bestBid + bestAsk) / 2.0
    If mid <= 0 Then Return

    spreadBps = ((bestAsk - bestBid) / mid) * 10000.0

    If spreadBps >= wideThresholdBps Then
        spreadStatus = "WIDE"
    ElseIf spreadBps <= tightThresholdBps Then
        spreadStatus = "TIGHT"
    Else
        spreadStatus = "NORMAL"
    End If
End Sub
```

---

## 6. Worked Examples

### Example A — TIGHT spread, no penalty

```
bestBid = 95000.0, bestAsk = 95000.5
mid     = 95000.25
spreadBps = 0.5 / 95000.25 * 10000 = 0.053 bps

0.053 <= TightThresholdBps (1.5) → SpreadStatus = "TIGHT"
No penalty fires. Display shows "0.05 bps | TIGHT" in green.
Breakdown row absent (penaltyLong = penaltyShort = 0; both `False` flags).
```

### Example B — WIDE spread, ROC > 0, long-side penalty

```
bestBid = 95000.0, bestAsk = 95005.0
mid = 95002.5
spreadBps = 5 / 95002.5 * 10000 = 0.526 bps

Wait — that's still tight. Let me revise the example to demonstrate WIDE.

bestBid = 95000.0, bestAsk = 95050.0  (50-point spread during a flush)
mid = 95025.0
spreadBps = 50 / 95025 * 10000 = 5.26 bps

5.26 >= WideThresholdBps (5.0) → SpreadStatus = "WIDE"
ROC = +0.4 > SlopeSensitivity (0.1) → penalise long side
  state.LongScore -= 1 (cfg.Scoring.SpreadWidePenalty default)
  spreadPenaltyLong = 1

Display: "5.26 bps | WIDE" in red.
Breakdown: "Spread [L] -- 5.26 bps | WIDE | PENALTY -1 [L]"
```

### Example C — WIDE spread, ROC near zero, both sides penalised

```
spreadBps = 6.1, SpreadStatus = "WIDE"
ROC = +0.05, |ROC| < SlopeSensitivity (0.1) → both sides

  state.LongScore  -= 1
  state.ShortScore -= 1
  spreadPenaltyLong = spreadPenaltyShort = 1

Breakdown row shows hits on both sides.
```

---

## 7. Files Changed Summary

| File | Change |
|---|---|
| `Core/IndicatorResults.vb` | Add `SpreadBps As Double` and `SpreadStatus As String` |
| `Core/Indicators_OrderFlow.vb` | Add `CalcSpread()` shared sub |
| `Core/ScoringEngine_Calculate_Scoring.vb` | Add spread-penalty block after Liq Penalty in `RunScoringPipeline()`; add Spread breakdown row |
| `Core/Settings/EngineSettings.vb` | Add `SpreadSettings` class + `Spread` property; add `SpreadWidePenalty` to `ScoringSettings` |
| `UI/MainForm_Analysis.vb` | Call `CalcSpread()` after `CalcOFI()` |
| `UI/MainForm_Render_Sections.vb` | Add Spread line to ORDER FLOW section |
| `settings.json` | Add `"spread"` block under `"indicators"`; add `spread_wide_penalty` under `"scoring"` |

---

## 8. Settings Keys

### `Core/Settings/EngineSettings.vb`

```vb
''' <summary>Bid-ask spread microstructure thresholds.</summary>
Public Class SpreadSettings
    ''' <summary>Spread (bps) at or above this is WIDE -- triggers entry-side penalty.</summary>
    <JsonPropertyName("wide_threshold_bps")>  Public Property WideThresholdBps  As Double = 5.0
    ''' <summary>Spread (bps) at or below this is TIGHT -- display-only marker, no score impact.</summary>
    <JsonPropertyName("tight_threshold_bps")> Public Property TightThresholdBps As Double = 1.5
End Class

' Add to IndicatorSettings class:
<JsonPropertyName("spread")> Public Property Spread As New SpreadSettings

' Add to ScoringSettings class:
''' <summary>Penalty applied to entry side when SpreadStatus = WIDE. Default 1.</summary>
<JsonPropertyName("spread_wide_penalty")> Public Property SpreadWidePenalty As Integer = 1
```

### `settings.json` — add under `"indicators"`

```json
"spread": {
  "wide_threshold_bps": 5.0,
  "tight_threshold_bps": 1.5
}
```

### `settings.json` — add under `"scoring"`

```json
"spread_wide_penalty": 1
```

| Key | Default | Purpose |
|---|---|---|
| `indicators.spread.wide_threshold_bps` | 5.0 | At or above → WIDE |
| `indicators.spread.tight_threshold_bps` | 1.5 | At or below → TIGHT |
| `scoring.spread_wide_penalty` | 1 | Score points deducted on entry side when WIDE |

---

## 9. What This Does NOT Do

- Does **not** add new API calls — order book is already fetched
- Does **not** reward TIGHT spread (per trader-profile rule: no non-directional padding)
- Does **not** affect VerdictContext, Pass 2c, MTF gate, Kelly sizing
- Does **not** add a CSV column initially (add to Section 12 backlog after 50+ live runs)
- Does **not** track spread momentum or rolling history (REST snapshot only — WebSocket version is post-WebSocket work, see `post-websocket-post-calibration-backlog.md`)

---

## 10. Validation Plan

After 50+ live runs:

1. Distribution check: what % of WIDE classifications fired during genuine flush events vs noise during quiet hours? Target: WIDE fires <10% of total runs and >50% of those coincide with significant 1m candle range expansion (>2× ATR).
2. Threshold tuning: if WIDE fires too often during normal NY-session two-way flow, raise `wide_threshold_bps` to 7.0 or 8.0.
3. Penalty effectiveness: review `analysis_log.csv` for runs where Spread WIDE pushed an otherwise-WEAK verdict to NO TRADE — did those entries underperform when traded anyway?

Add to Section 12 calibration backlog after implementation.

---

## 11. Open Questions

| # | Question | Default Answer | Status |
|---|---|---|---|
| Q1 | Should TIGHT spread give a small reward? | No — non-directional padding is rejected per trader-profile Section 4 | Resolved |
| Q2 | Penalty when ROC near zero — both sides or neither? | Both — execution risk is symmetric when there's no directional bias | Resolved |
| Q3 | Should MTF gate consider spread? | Not yet — MTF is structural (15m timeframe), spread is sub-second microstructure. Different timescales | Resolved |
| Q4 | Is `wide_threshold_bps = 5.0` too high or too low for BTC-PERPETUAL? | Best initial guess; tune after 50+ runs | Open — calibration |
| Q5 | Should spread momentum be added (rolling delta over N runs)? | No — defer to post-WebSocket work where snapshot quality justifies it | Resolved (deferred) |
