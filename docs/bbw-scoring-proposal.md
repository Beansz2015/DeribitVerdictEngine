# BBW Squeeze Scoring Proposal

## Context

BBW (Bollinger Band Width) currently awards +1 to BOTH Long and Short scores
when SqueezeStatus = "NONE" (no squeeze active). This is the same
non-directional padding pattern that was fixed for Funding OK and No Adverse
Liq in v0.17.

However, unlike those two signals, BBW does carry genuine market information:
a squeeze indicates compression before a breakout, and the direction and
resolution of that squeeze is meaningful. The question is not whether BBW
should affect scoring, but HOW it should do so directionally.

---

## Current Behaviour (the problem)

Current code:

    Dim bbwFull As Boolean = r.SqueezeStatus = "NONE"
    Dim bbwPartial As Boolean = r.SqueezeStatus = "RELEASING"
    If bbwFull Then
        state.LongScore += 1
        state.ShortScore += 1
        state.FullLongCategories.Add(SignalCategory.Microstructure)
        state.FullShortCategories.Add(SignalCategory.Microstructure)
    End If

When SqueezeStatus = "NONE": both scores +1. This fires the majority of the
time and rewards the absence of compression rather than the presence of a
meaningful squeeze signal.

When SqueezeStatus = "RELEASING": partial upgrade eligible but no direct
score, and no directional information is used.

When SqueezeStatus = "ACTIVE": no score at all -- but an active squeeze is
an important caution signal that currently goes unrepresented.

---

## Observed Bug

In the current v0.17 screenshot (TRENDING_DOWN regime, Short verdict), the
BBW Squeeze row shows [L] only (not [L][S]) even though the code awards both.
This suggests the [S] mark is being suppressed or the Short score is not
receiving the BBW point. This needs to be investigated alongside the redesign.

---

## Proposed Redesign Options

Three options are presented for spec decision:


OPTION A: Direction-Confirmed Scoring

BBW scores in alignment with the dominant direction signal (EMA alignment
or DMI direction), not unconditionally:

    If r.SqueezeStatus = "NONE" Then
        ' Award to whichever side the trend confirms
        If r.EMAAlignment = "BULL" Then state.LongScore += 1
        If r.EMAAlignment = "BEAR" Then state.ShortScore += 1
        ' MIXED: no award (ambiguous)
    ElseIf r.SqueezeStatus = "RELEASING" Then
        ' Same directional logic but as partial (upgrade-eligible)
    ElseIf r.SqueezeStatus = "ACTIVE" Then
        ' Active squeeze: penalise both directions (reduce confidence)
        state.LongScore = Math.Max(0, state.LongScore - 1)
        state.ShortScore = Math.Max(0, state.ShortScore - 1)
    End If

Pros: Directional, penalises indecision, rewards trend clarity.
Cons: Creates dependency between BBW and EMA signals (correlated).


OPTION B: Squeeze-State Only (Penalty for ACTIVE, bonus for RELEASING)

No reward for NONE (calm = neutral). Only fire when something notable happens:

    If r.SqueezeStatus = "ACTIVE" Then
        ' Compression = reduce confidence on both sides
        state.LongScore = Math.Max(0, state.LongScore - 1)
        state.ShortScore = Math.Max(0, state.ShortScore - 1)
    ElseIf r.SqueezeStatus = "RELEASING" Then
        ' Breakout imminent -- reward direction of current momentum (ROC)
        If r.ROC > 0 Then state.LongScore += 1
        If r.ROC < 0 Then state.ShortScore += 1
    End If
    ' NONE: no score change

Pros: Only fires when meaningfully different from normal conditions.
Cons: NONE state (most common) contributes nothing.


OPTION C: Keep direction-neutral but fix the bug only

Keep the current NONE = +1 both sides logic but investigate and fix
the display/scoring bug where [S] is not appearing. Accept that BBW
remains non-directional padding (same as before v0.17 changes).

Pros: Minimal change, consistent with original spec intent.
Cons: Does not fix the underlying non-directional padding problem.

---

## Impact on Max Score

    Option A or B: Max score remains 13 (no net change if ACTIVE penalty
                   and RELEASING reward cancel out on average)
    Option C: Max score remains 13 but BBW still pads both sides

---

## Questions for the Spec

1. Which option do you prefer: A (direction-confirmed), B (squeeze-state only),
   or C (bug fix only)?

2. If Option A: should the directional confirmation use EMAAlignment, DMI
   direction, or ROC direction?

3. If Option B: should RELEASING reward go to both sides (momentum-neutral)
   or only to the side matching current ROC direction?

4. Should an ACTIVE squeeze apply a -1 penalty to both sides regardless of
   which option is chosen? An active squeeze reduces trade quality in all
   directions.

5. Does the max score denominator need updating if BBW can now also subtract
   points (i.e., is /13 still the right display max)?
