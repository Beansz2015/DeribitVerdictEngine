# Dual-Scoring Fix — Spec Response & Implementation Instructions

## Summary

All four proposed changes in `dual-scoring-fix-proposal.md` are approved.
Implement as described below.

---

## Q1: No Adverse Liq → Convert to Penalty-Only ✅ APPROVED

**Rationale:**
"No adverse liquidations" is the normal market state (~95% of the time).
Rewarding it with +1 to both sides is permanent background noise with no
directional information. A positive reward for the absence of something bad
is not a signal — it is padding.

The penalty-only approach is correct because:
- It only fires when something meaningful actually happens
- It penalises only the adversely affected direction, not both
- The display becomes honest: dot/neutral = nothing detected, mark = penalty fired

**Implementation:**

Replace the current `AddFull(state, noLongLiq, noShortLiq, ...)` block entirely
with the scaled penalty logic defined in Q4 below. Do not retain any +1 reward
for calm conditions.

Display row behaviour:
- LiqSignal = "NONE"       → show neutral dot, no mark
- LiqSignal = "LONG LIQS"  → show [L] penalty mark on long side only
- LiqSignal = "SHORT LIQS" → show [S] penalty mark on short side only

---

## Q2: Funding OK → Remove from Step 2 Scoring ✅ APPROVED

**Rationale:**
Step 3 already applies a directional funding modifier that adjusts `ls`/`ss`
based on the same rate thresholds. Keeping Funding OK in Step 2 scoring
constitutes double-counting. Extreme funding conditions get penalised twice:
once via `fundOkLong/fundOkShort` being false in Step 2, and again via the
Step 3 modifier.

**Implementation:**
- Remove `fundOkLong` and `fundOkShort` from all `AddFull` calls
- Remove from score accumulation entirely
- Keep the Funding OK row in the breakdown display as display-only
- Display row shows: current rate value + bias label (NEUTRAL / LONGS CROWDED /
  SHORTS CROWDED)
- No [L] or [S] marks on the Funding OK display row

---

## Q3: Score Denominator → Update to /13 ✅ APPROVED

**Rationale:**
After removing the 2 non-directional padding points, the true maximum
achievable score is 13. Displaying /15 is misleading — a score of 12/15
appears to be 80% but is actually 92% of all achievable signals. It also
creates a false impression that 2 points are permanently "missing".

**Implementation:**
- Update display denominator from /15 to /13
- Example: "Score: Long 10/13 | Short 4/13"

**Verdict tier thresholds remain unchanged at 6 / 9 / 12.**

Proportional check confirms thresholds still work correctly at max=13:

| Tier         | Threshold | % of Max (old /15) | % of Max (new /13) |
|--------------|-----------|--------------------|--------------------|
| STRONG       | 12–13     | 80–100%            | 92–100%            |
| MEDIUM       | 9–11      | 60–73%             | 69–85%             |
| WEAK         | 6–8       | 40–53%             | 46–62%             |
| NO TRADE     | < 6       | < 40%              | < 46%              |

No threshold adjustments needed.

---

## Q4: Liquidation Penalty → Scaled -1 / -2 ✅ APPROVED

**Rationale:**
A flat -1 treats a 50 BTC liquidation the same as a 500 BTC cascade.
Large liquidation events materially increase directional cascade risk and
deserve a stronger penalty. A scaled approach reflects real-world impact.

**Thresholds:**

| LiqSize       | Penalty |
|---------------|---------|
| > 50 BTC      | -1      |
| > 200 BTC     | -2      |

Note: 50 BTC and 200 BTC are initial calibration values for BTC-PERPETUAL
on Deribit as of Q1 2026 with BTC price ~$80,000–$100,000. After 2–4 weeks
of live data, review the distribution of observed LiqLongSize / LiqShortSize
values and adjust the 200 BTC threshold to approximately the 90th percentile
of liquidation events seen in your data.

**Implementation (VB.NET):**

```vbnet
' Scaled liquidation penalty — replaces previous AddFull(noLongLiq, noShortLiq) block
If r.LiqSignal = "LONG LIQS" Then
    Dim penalty As Integer = If(r.LiqLongSize > 200, 2, 1)
    state.LongScore = Math.Max(0, state.LongScore - penalty)
ElseIf r.LiqSignal = "SHORT LIQS" Then
    Dim penalty As Integer = If(r.LiqShortSize > 200, 2, 1)
    state.ShortScore = Math.Max(0, state.ShortScore - penalty)
End If
```

The `Math.Max(0, ...)` guard prevents scores from going negative.

---

## Impact Summary

| Item                        | Before Fix        | After Fix                  |
|-----------------------------|-------------------|----------------------------|
| No Adverse Liq              | +1 both sides     | 0 (calm), -1 or -2 (event) |
| Funding OK                  | +1 both sides     | Display-only, no scoring   |
| Max achievable score        | 15                | 13                         |
| Score denominator display   | /15               | /13                        |
| Verdict tier thresholds     | 6 / 9 / 12        | 6 / 9 / 12 (unchanged)     |
| Padding points removed      | —                 | 2                          |
| All remaining points        | Mixed             | All directional            |

---

## Files to Modify

- `ScoringEngine.vb` — apply Q1, Q2, Q4 code changes
- Any display/UI file rendering the score denominator — update /15 to /13
- Any display/UI file rendering the Funding OK breakdown row — remove [L]/[S] marks
- Any display/UI file rendering the Liq breakdown row — update to show penalty
  marks only when penalty fires (not when calm)
