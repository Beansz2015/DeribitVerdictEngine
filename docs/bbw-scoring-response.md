# BBW Squeeze Scoring — Spec Response & Implementation Instructions

## Summary

Option B (Squeeze-State Only) is approved. Implement as described below.
All five questions from `bbw-scoring-proposal.md` are answered.

---

## Q1: Chosen Option — Option B (Squeeze-State Only) ✅ APPROVED

**Rationale against Option A (Direction-Confirmed):**
Option A creates a correlated dependency between BBW and EMA alignment.
If EMA is already scoring +1 for bullish alignment and BBW also scores +1
for the same EMA condition, the result is amplifying one signal rather than
adding an independent one. This is a subtler form of the double-counting
problem fixed in v0.17 for Funding OK. The multi-indicator stack's value
comes from independent signal sources — correlation between BBW and EMA
undermines this.

**Rationale against Option C (Bug Fix Only):**
Keeping non-directional padding after removing it from Funding OK and Liq
would be inconsistent design. The principle established in v0.17 is correct:
calm/normal conditions are not signals and should not be rewarded.

**Rationale for Option B:**
BBW's real value is detecting regime transitions — the squeeze forming
(ACTIVE) and the squeeze breaking (RELEASING). Normal, non-squeezed
conditions (NONE) are the baseline state, not an achievement worth
rewarding. Option B scores only the two meaningful state changes.

---

## Q2: Directional Confirmation Signal (for Option A — moot)

Option A was not selected. For the record: ROC would have been preferred
over EMA alignment or DMI, as ROC is the fastest and least correlated
signal with BBW. EMA is too slow and correlated; DMI introduces awkward
cross-timeframe dependency (5m vs 1m).

---

## Q3: RELEASING Reward — ROC-Directional Only ✅ APPROVED

**Rationale:**
RELEASING means a breakout is imminent or underway. At that point, the
direction of the breakout is already being expressed by ROC. Rewarding
both sides during RELEASING is nearly as meaningless as rewarding both
during NONE — it says "a breakout is happening but I don't know which
way, so here's a point for everything."

ROC is used as the directional tiebreaker with the following thresholds:

- ROC > +0.10  → LongScore  += 1
- ROC < -0.10  → ShortScore += 1
- ROC between ±0.10 (chop zone) → no award

A squeeze releasing into a ROC chop zone is an unreliable breakout signal
and should not be rewarded.

---

## Q4: ACTIVE Squeeze — Apply -1 Penalty Both Sides ✅ APPROVED

**Rationale:**
An active squeeze means volatility is compressed and a directional move
is loading but unconfirmed. Entering any trade during this state is lower
quality regardless of direction — the breakout direction is unknown and
the risk of being on the wrong side of a sharp move is elevated.

The -1 to both sides correctly reduces confidence for all pending signals.
This is not the same pattern as the Liq/Funding OK fix — those rewarded
normality. This penalises a specific anomalous state (compression), which
is meaningful and directionally symmetric.

---

## Q5: Max Score Denominator — Keep /13 ✅ NO CHANGE

**Rationale:**
ACTIVE squeeze applies a penalty (cannot push maximum above 13).
RELEASING gives +1 to one side via ROC — this replaces the old NONE
pathway rather than adding a new point ceiling.
The maximum achievable score remains 13. Display denominator stays /13.

---

## Implementation (VB.NET)

Replace the entire current BBW scoring block with the following:

```vbnet
' BBW Squeeze-State Scoring (Option B)
Select Case r.SqueezeStatus

    Case "ACTIVE"
        ' Volatility compressed, breakout direction unknown
        ' Reduce confidence on both sides
        state.LongScore  = Math.Max(0, state.LongScore  - 1)
        state.ShortScore = Math.Max(0, state.ShortScore - 1)

    Case "RELEASING"
        ' Breakout underway — reward only in direction of ROC momentum
        If r.ROC > 0.1 Then
            state.LongScore += 1
        ElseIf r.ROC < -0.1 Then
            state.ShortScore += 1
        End If
        ' ROC in chop zone (±0.10): no award — unreliable breakout

    Case "NONE"
        ' Normal conditions — no score change

End Select
```

---

## Display Row Behaviour

Update the BBW breakdown row display logic as follows:

| SqueezeStatus | ROC         | Display            |
|---------------|-------------|--------------------|
| NONE          | Any         | Neutral dot        |
| ACTIVE        | Any         | [-L][-S] (penalty) |
| RELEASING     | > +0.10     | [L] only           |
| RELEASING     | < -0.10     | [S] only           |
| RELEASING     | ±0.10 chop  | Neutral dot        |

---

## Re: Observed Bug (v0.17 Screenshot)

The [S] mark not appearing during TRENDING_DOWN despite both scores
receiving +1 is a display rendering issue, not a scoring issue. The
breakdown row display logic likely suppresses [S] when the regime is
already SHORT-biased (a display optimisation that is hiding data).

This bug becomes irrelevant once Option B is implemented, since the NONE
state will no longer award either side. Investigate the UI layer separately
only if similar suppression issues appear on other rows after this change.

---

## Impact Summary

| State      | Before Fix         | After Fix                          |
|------------|--------------------|------------------------------------|  
| NONE       | +1 both sides      | No score change                    |
| RELEASING  | Partial upgrade only, no directional score | +1 to ROC-aligned side only (or 0 in chop) |
| ACTIVE     | No score           | -1 both sides                      |
| Max score  | 13                 | 13 (unchanged)                     |

---

## Files to Modify

- `ScoringEngine.vb` — replace current BBW scoring block with code above
- Display/UI file rendering BBW breakdown row — update mark logic per
  display table above
