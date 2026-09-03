# Spec-back — queue item 18, R7 handback

**Date:** 2026-09-04. **Audience:** the reviewing seat (the orchestrator). **Delta reviewed against:** [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) §7 — the R7 handback added at review 2026-09-04, on top of the item-18 build already accepted and committed at `5cd7269`.
**Not yet committed.** The fix and fixture are in the working tree only.

---

## 1. Ranked verification handles

**⭐ The one that matters — the before/after on `A61f`, run explicitly, with a temporary revert, twice.**

R7's acceptance item 1 needed `A61f` to fail pre-fix. The `.Max()` change landed *before* `A61f` was written (I fixed `CoverageReport.vb` first, then built the fixture against the already-fixed code), so the first harness run after adding `A61f` was **not** a genuine before/after — it only proved the fixture passes post-fix, which `winner.TrailingMs` alone cannot distinguish from a fixture that never exercised the divergence at all. I caught this and ran the proof properly, **twice** (once while building, once again just now to produce fresh evidence for this packet):

```
1. Reverted CoverageReport.vb's line to:  result.TrailingMsForHour = winner.TrailingMs
2. Rebuilt, ran the harness:
     FAIL  A61f ... — A: classification=TrailingEdge trailingMsForHour=899999 smaller=899999 larger=2099999 | B: classification=Defect trailingMsForHour=Nothing
     totals: PASS=316 FAIL=1
3. Restored:  result.TrailingMsForHour = spans.Where(Function(s) s.Classification = finalCls).Select(Function(s) s.TrailingMs).Max()
4. Rebuilt, ran the harness:
     PASS  A61f ...
     totals: PASS=317 FAIL=0
```

This confirms two separate things at once: **(a)** the fix is load-bearing — reverting it flips exactly one fixture red — and **(b)** your claim is exactly right: scenario A's smaller span (899,999ms, the *first* one built) is what `winner.TrailingMs` reports, against the larger (2,099,999ms) that `.Max()` correctly picks. `First` and `Max` genuinely diverge on this input; `A61f` is not accidentally passing both ways.

Ranked below that, cheapest first:

1. **The one-line arithmetic check.** `2099999 <> 899999` — the two candidate values in scenario A are not equal, so a fixture that happened to pick either value by luck is ruled out. This is the identity that makes the before/after in §1 meaningful rather than cosmetic.
2. **No debug residue.** `grep -n "TEMP REVERT" tools/BacktestRunner/CoverageReport.vb` → **must return nothing.** I used this marker twice (both re-verification passes) and grepped for it after each restore.
3. **The fix is one expression, as R7 required.** `git diff tools/BacktestRunner/CoverageReport.vb` shows exactly one changed statement (`result.TrailingMsForHour = winner.TrailingMs` → the `.Where().Select().Max()` chain) plus a comment; `InstanceId` still reads `winner.InstanceId`, untouched.
4. **The invariant, scenario B, in isolation.** `hrB.Classification = Defect AndAlso Not hrB.TrailingMsForHour.HasValue` — worth re-deriving by hand: `span0B` has `RowCount = 0`, so `ClassifySpan` never reaches the `TrailingEdge` branch for it (`storeClean` requires `RowCount > 0`); with `s1Skipped:=True` it returns `Defect` with `TrailingMs = Nothing` directly. `span1B` would be `TrailingEdge` alone but never wins the worst-of combine. The filtered set `spans.Where(Classification = Defect)` therefore contains exactly `span0B`, whose `TrailingMs` is `Nothing` — `Max()` over a single `Nothing` is `Nothing`.
5. **Harness total.** `317 PASS, 0 FAIL, ALL PASS`; `tools/checks/verify-gate.ps1` → `GATE PASSED`.

**If you only run one:** the before/after in the block above — it's the only check that distinguishes "the fixture is real coverage" from "the fixture happens to pass."

---

## 2. Decisions queued

**None.** R7 was fully specified (the fix, the scoping, the fixture shape, the acceptance table) and nothing in the build required a judgment call outside what was written. The one design choice I made — folding scenario B into the same `Check()` as scenario A rather than a separate `A61g` — is a fixture-shape call, not a decision that touches the ruling; noted in §3 below rather than queued here, since I'm confident in it and it doesn't need your sign-off to stand.

---

## 3. Spec-back proper — attacking R7 itself

You're right that R7 is the newest, least-reviewed thing in this pair — it was written after my build, not before it, so nothing has attacked it yet. Here's the attack.

**Was "max within `finalCls`" the right scoping? I did not find a case where it's wrong.**

The invariant R7 wants (`TrailingMsForHour` is `Nothing` whenever the hour isn't `TrailingEdge`) holds for a structural reason that's stronger than "I tested it and it passed": `ClassifySpan` has exactly one `Return` statement that pairs a non-`Nothing` `TrailingMs` with anything, and that pairing is always `HourClass.TrailingEdge` (every other `Return` — `UnknownScope`, `NotCapturing`, `Captured`, the two `Defect` returns, `ExpectedMissing` — passes `Nothing` explicitly). So filtering `spans` to `Classification = finalCls` and only *then* taking `TrailingMs` can never surface a non-`Nothing` value unless `finalCls = TrailingEdge`, **regardless of what precedence rule produced `finalCls`** — the scoping doesn't depend on F1 D-5.1's specific ordering at all, which means it would still be correct even if that precedence changed later. That's a stronger property than R7 claims for it, worth recording: **the fix is robust to a future re-ordering of the worst-of combine**, not just correct against today's ordering.

I considered one alternative scoping and rejected it, matching R7's own instruction not to try it: maxing over *every* span regardless of `finalCls`. I didn't just take R7's word for why that's wrong — I traced it: a `Defect`-plus-`TrailingEdge` hour (exactly `A61f` scenario B) would report the `TrailingEdge` span's real gap even though the hour's own verdict is `Defect`, silently exposing a number the field's contract says shouldn't exist there. Confirmed this is what R7 meant by "breaks the invariant," not a hypothetical.

**Does `Max()` over `Long?` behave as R7's comment claims, on every path I exercised?** Yes, on the two paths reachable here — but one path in R7's own parenthetical is unreachable, and R7 doesn't say so.

R7's comment says `Max()` returns `Nothing` "when that set has no non-null `TrailingMs`" — which covers two cases in the .NET contract: the set is *empty*, or the set is *non-empty but every element is `Nothing`*. `A61f` scenario B exercises the second case. **The first case — an empty filtered set — is provably unreachable**, not just untested: `ClassifyHour`'s own combine logic only assigns `finalCls` to a value some span actually has (`spans.Any(Defect)`, `spans.Any(TrailingEdge)`, ... down to the bare `Else finalCls = NotCapturing`, which is reached only when every remaining span *is* `NotCapturing`, by elimination of the five `ElseIf`s above it). So `spans.Where(Classification = finalCls)` is non-empty for every possible `finalCls`, by construction of the caller — `Max()`'s empty-sequence branch is dead code at this call site. This doesn't make the comment wrong (the .NET behavior it describes is accurate), but the comment's phrasing implies both halves were worth stating as live concerns, and only one is. I'd tighten it to say "empty or all-`Nothing`, though empty cannot occur here" if this file gets touched again — not worth a churn-only edit on its own.

---

## 4. What I did not verify, and cannot

- **Whether `A61f`'s two-trailing-span shape can occur in a real book — checked, and it does not occur in the one sample I have.** I re-ran `coverage --evidence-dir` against the same 2026-09-01 copy-back used for item 18's original acceptance item 7 (`AWS-copybacks/aws-copyback-2026-09-01/aws_fetch/20260901-153838`, full captured range 2026-07-20→2026-09-01) and grepped the markdown `Reason` column for a row containing `TrailingEdge` twice. **None found.** That range contains exactly one split hour total (`2026-08-10 09:00`, `Captured`|`Defect(empty)` — no `TrailingEdge` span at all) and exactly one `TrailingEdge` hour total (`2026-08-18 09:00`, unsplit). So this one sample is consistent with R7's own "rare, not impossible" framing, but it's one ~6-week sample from one box — I did not check the other seven copy-backs under `AWS-copybacks/`, and a "did not occur here" is not "cannot occur."
- **A second seat's read on the "unreachable empty-sequence" claim in §3.** I traced it through `ClassifyHour`'s combine logic myself; nobody else has checked that trace.
- **Anything about committing or pushing.** Nothing in this delta is committed. Continues to sit on top of the working tree from the `5cd7269`-committed state.

---

## Also, directly: R7 acceptance item 2

**`A61a`–`A61e` pass unchanged**, confirmed on a fresh run at the current (fixed) state, output verbatim:

```
PASS  A61a ⭐ the defect reproduced — ...
PASS  A61b the fix — ...
PASS  A61c ⭐ R4 guard — ...
PASS  A61d no-trailing-span case — ...
PASS  A61e ⭐ F1 D-5.1 precedence untouched — ...
```

Also confirmed under the temporarily-reverted (`winner.TrailingMs`) code — same five lines, all still `PASS` — which is the expected shape of the check, not just a formality: every one of those five fixtures builds a single-trailing-span hour, so `First` and `Max` agree on a one-element filtered set by construction, and none of them moved in either direction. Only `A61f` (the two-trailing-span shape) is sensitive to which selector is used.

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
