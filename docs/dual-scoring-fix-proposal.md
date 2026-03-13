# Dual-Scoring Fix Proposal -- No Adverse Liq & Funding OK

## Context

This document proposes changes to ScoringEngine.vb to eliminate non-directional
score padding from two signals: No Adverse Liq and Funding OK.

These signals currently fire [L] AND [S] simultaneously every time conditions
are calm, padding both Long and Short scores equally with no directional value.

---

## Current Behaviour (the problem)

### No Adverse Liq

Current code:

    Dim noLongLiq As Boolean = r.LiqSignal <> "LONG LIQS"
    Dim noShortLiq As Boolean = r.LiqSignal <> "SHORT LIQS"
    AddFull(state, noLongLiq, noShortLiq, SignalCategory.Microstructure)

When LiqSignal = "NONE": noLongLiq=True, noShortLiq=True, so both scores +1.
This fires roughly 95% of the time and rewards calm conditions rather than
penalising adverse ones. It adds a permanent +1 to both sides with no
directional information.

### Funding OK

Current code:

    Dim fundOkLong As Boolean = r.FundingRate <= 0.0005
    Dim fundOkShort As Boolean = r.FundingRate >= -0.0005
    AddFull(state, fundOkLong, fundOkShort, SignalCategory.Microstructure)

When funding is near zero: both conditions are true, so both scores +1.
Funding is already handled directionally in Step 3 (the funding modifier
which adjusts ls/ss based on rate thresholds). This is double-counting.

---

## Proposed Fix

### No Adverse Liq -- Convert to Directional Penalty

Remove the positive reward entirely. Instead apply a -1 penalty to the
adversely affected side only when liquidations are actually detected.

Proposed replacement code:

    ' No scoring when calm -- only penalise when adverse liqs are present
    If r.LiqSignal = "LONG LIQS" Then
        state.LongScore = Math.Max(0, state.LongScore - 1)
    ElseIf r.LiqSignal = "SHORT LIQS" Then
        state.ShortScore = Math.Max(0, state.ShortScore - 1)
    End If

Breakdown row: display [L] mark (penalty fired on long side) or [S] mark
(penalty fired on short side) only when a penalty actually fires.
Show dot/neutral when LiqSignal = NONE.

### Funding OK -- Remove from Step 2 Scoring Entirely

Since Step 3 already applies a directional funding modifier that adjusts
ls/ss based on funding rate thresholds, the Step 2 Funding OK signal is
redundant and constitutes double-counting.

Proposed change: remove fundOkLong/fundOkShort from AddFull calls and from
score accumulation. Keep the Funding OK row in the breakdown as a
display-only row showing the rate and bias label -- no [L] or [S] marks.

---

## Impact on Max Score

    Current max: 15 (13 directional + 2 non-directional padding)
    After fix:   13 (all directional)

Verdict thresholds (6/9/12) remain unchanged. The tighter range makes
each point more meaningful. Strong Long/Short now requires 12 out of 13
real directional signals.

---

## Questions for the Spec

1. Do you agree with converting No Adverse Liq to a penalty-only signal
   (no reward for calm, only -1 when adverse liqs detected)?

2. Do you agree Funding OK should be removed from Step 2 scoring and
   kept as a display-only row?

3. Should the score denominator in the display be updated from /15 to /13
   to accurately reflect the new max, or left as /15 to preserve
   the existing tier boundary intuition for the user?

4. Should the liquidation penalty be -1 flat, or -2 for large liquidation
   events (e.g. LiqLongSize or LiqShortSize above a significant threshold)?
   If yes, what threshold in BTC or USD would you recommend?
