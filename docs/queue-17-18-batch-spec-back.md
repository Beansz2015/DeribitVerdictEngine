# Spec-back — queue items 17 and 18

**Date:** 2026-09-03. **Audience:** the reviewing seat. **Outcome record:** [`queue-17-18-batch-summary.md`](queue-17-18-batch-summary.md) — read that first for *what happened*; this is *what to check, what's still open, and where the specs were wrong.*
**Specs reviewed against:** [`fixture-literal-provenance-a14b-spec.md`](fixture-literal-provenance-a14b-spec.md) (item 17) · [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) (item 18).

---

## 1. Ranked verification handles

**If you only run one: `tools/checks/verify-gate.ps1`.** It re-runs the harness (all `A14`/`A61` fixtures included), the display-parity check, and the version-bump check in one command, and it's what I ran last before writing this. Exit should print `GATE PASSED`.

Ranked below that, cheapest first:

1. **Item 17 — the literal is gone.** `grep -n "0.105" verify/ordercheck/Program.vb` → **must return nothing.** This is the acceptance item's own check and the cheapest possible confirmation that R1 landed.
2. **Item 17 — the seven consumers, unchanged.** `dotnet run --project verify/ordercheck/OrderCheck.vbproj --no-build 2>/dev/null | grep -E "A14a|A14b|A14d|A14e|A14i|A14j|A15a"` → all seven **PASS**. This is the load-bearing check, not the literal-removal grep alone: a mechanism literal is *defined* as one no assertion depends on, and this is what proves it.
3. **Item 18 — the arithmetic identity, by hand.** `A61a`'s own PASS line prints both candidate values: `correctSpanMs=1499999 buggyHourEndMs=3299999`. Recompute both from the fixture's own timestamps without running anything: `correctSpanMs = (flip−1) − tradeA = 1,799,999 − 300,000 = 1,499,999` · `buggyHourEndMs = (hourEnd) − tradeA = 3,599,999 − 300,000 = 3,299,999`. If these don't match what's printed, something drifted between the fixture and this packet.
4. **Item 18 — no debug residue.** `grep -n "TEMP REVERT" tools/BacktestRunner/CoverageReport.vb` → **must return nothing.** I used this marker twice during the before/after proofs (§2 below) and grepped for it myself before the final build; a reviewer re-checking costs one line.
5. **Item 18 — R4's guard, the pair that matters.** `A61c` PASS confirms an unsplit hour's figure is bit-for-bit today's formula. This is the fixture to mutate if you want to independently confirm the fix didn't reach further than the defect: change `A61c`'s `stats.LastTsMs` by 1ms and confirm it goes red.
6. **T3 — no rendered surface moved.** `git diff tools/BacktestRunner/CoverageReport.vb | grep -n "BuildConsoleSummary\|BuildMarkdown"` → the only hit should be inside a comment (`HourResult`'s new doc-comment mentions both function names; neither function itself has a diff hunk).

---

## 2. Decisions queued, with my read

None of these block the build — both specs' own decisions were already ruled before I started. These are judgment calls I made filling in gaps the specs left open, flagged so you can overrule any of them cheaply.

**D-A: A61a as integration-level (`BuildResult`), A61b as unit-level (`ClassifyHour`) — a split the spec didn't specify.**
The spec's table describes A61a as *"the defect, reproduced"* and A61b as *"the fix,"* both against *"the same input,"* without saying which API level either calls. I read "the defect" as living in `BuildResult` (that's literally where §1's buggy code snippet is) and "the fix" as living in the mechanism `ClassifyHour`/`HourResult` exposes (T1's structural-field mandate). This let me avoid two fixtures asserting the same thing at the same level.
**My read:** correct as built — A61a needed real trade-store files and a real month-boundary trick (a second trade in the next hour, to push `observedBoundMs` past span 0 without polluting span 0's own stats) precisely *because* it's exercising `BuildResult`'s real `AccumulateHourStats`/`AccumulateSplitSpanStats` plumbing, which A61b sidesteps by passing `HourStoreStats` and `observedBoundMs` directly. If you'd rather have both at the same level, say so — it's a fixture-shape choice, not a decision that touches the ruling.

**D-B: I updated `trader-tick-queue.md` (banner, the §0a box, item 17's row) — not explicitly requested.**
CLAUDE.md's own maintenance instruction for that doc is *"update State on every tick"*, and there's a same-week precedent (`096ecbb`, closing item 21 the same way: strike the stale claim, keep the original text below it, point at the packet). I judged leaving both decisions marked "open" in a doc that says *"read this before believing any row below"* would be a worse failure than an unrequested doc edit.
**My read:** keep it — but I have no read on whether you want it in the *same* commit as the code change or split out `[no-engine-change]`-style like `096ecbb` was. That's yours.

**D-C: item 18's real-copy-back check (acceptance item 7) found zero split+trailing hours — I did not go looking for a second copy-back that might have one.**
The spec's own §6 says *"if none exists in the sample, acceptance item 7 reports 'no change' and that is a complete answer"* — so this isn't a gap in what was asked. But `AWS-copybacks/` holds several other stamped copy-backs I didn't touch (`aws-copyback-2026-08-07` through `-2026-08-28`), and at least one of them might contain a real split-hour-that-is-also-trailing-edge case, which would be strictly better evidence than the synthetic `A61a`.
**I have no read on whether this is worth a session.** It's pure confirmation (the fixtures already prove the mechanism), not a correctness gap — flagging it because it's the cheapest possible upgrade from synthetic to real evidence if you want it, not because I think it's owed.

---

## 3. Spec-back proper — feedback on the specs

### Item 17 — what it got right

**The proof in §1 did the actual work; the build was mechanical, exactly as the brief predicted.** The brief called this "Sonnet, low" because "the analysis is done", and it was — I made zero design decisions building it. The two named traps (T1: don't touch the resolution-1 POCO-default literals; T2: the check name repeats the numbers) were both real and both would have been easy to miss without the spec naming them explicitly — T2 especially, since the check-name string and the assertion literals are 35 lines apart in the file and nothing else ties them together.

### Item 18 — what it got right

**T1's warning shaped the whole implementation, not just one line.** *"If the span is not available structurally, add a structural field — never scrape one"* is why `ClassifySpan`'s return tuple grew a `TrailingMs As Long?` member instead of me reaching for a regex over `Reason`. That's the wording doing real work: it pre-empted the design question before I had a chance to take the shortcut.

**The brief's tier correction ("low is wrong, it's medium") was right, and right for the stated reason.** The arithmetic itself — `Math.Min(spanEndMsInclusive, boundMs)` instead of `Math.Min(hourEndMs, boundMs)` — is a one-token change. The actual work was confirming that change was *safe*: tracing that `ClassifySpan` already had everything it needed inline (no restructuring of the F1 D-5.1 precedence, which the escalation trigger explicitly forbade touching), and that threading a new nullable field through the split-path's `winner` selection couldn't leak a stale value from a non-winning span. Both held, but neither was free to confirm.

### Where the spec was narrower than its own words

**Acceptance item 7 ("run `coverage --evidence-dir` ... before and after") is a CLI-level check the spec doesn't say requires a second build.** `verify/ordercheck` and `tools/BacktestRunner` are separate `.vbproj`s; confirming the real-world CLI output actually moved (or didn't) meant building `BacktestRunner.vbproj` in Release separately from the harness, and — to get a genuine "before" rather than inferring it from `A61a` — reverting the fix a *second* time at the `BuildResult` call site, rebuilding that project, and running the real command against a real copy-back before restoring. An implementer reading acceptance item 7 as "just re-run the harness with `--evidence-dir`" would have skipped this; the harness project has no such flag. Worth a line in the next spec of this shape: *"acceptance item N needs a second build of `BacktestRunner.vbproj`, not the harness."*

**§6's third open question ("read [`HourStoreStats`'s per-span key semantics]; do not assume it is the span start") was exactly right to flag, and it wasn't the span start in the way I first guessed.** `AccumulateSplitSpanStats`'s dictionary is keyed by `spanStartMs` as resolved by `SpanStartFor` (the last boundary `≤` the trade's timestamp) — which *is* the span start, but only because `boundsAscending(0)` is contractually the hour start and every subsequent entry is a marker time, per that function's own doc comment. I read the body before assuming this, per the spec's own instruction; it would have been easy to assume "per-span key = span start" from the name alone and get lucky, since that's what it turned out to be.

### A constraint pair that nearly conflicted, and the escape hatch

**R2 ("Nothing when the hour is not TrailingEdge") vs. the split-path's worst-of combine picking `winner` by classification alone.** On first read this looked like it needed an explicit guard: *what if a non-winning span's `TrailingMs` were non-Nothing and somehow leaked into `result.TrailingMsForHour`?* It can't, and the reason is structural rather than something I had to add code to enforce: `ClassifySpan` only ever returns a non-Nothing `TrailingMs` from the one `Return` statement that also returns `HourClass.TrailingEdge` — every other `Return` in that function pairs a different classification with `Nothing`. So `winner.TrailingMs` is non-Nothing if and only if `winner.Classification = TrailingEdge`, which is if and only if `finalCls = TrailingEdge` (since `winner` is selected to match `finalCls`). No extra guard needed; R2 falls out of R1's own tuple shape for free. Naming the hatch so the next spec author doesn't add a defensive `If finalCls = TrailingEdge Then ... Else Nothing` that would just be dead code.

---

## 4. What I did not verify, and cannot

- **Whether any OTHER real copy-back contains a genuine split-hour-that-is-also-trailing-edge case.** I checked exactly one (`aws-copyback-2026-09-01`) over its full captured range. `AWS-copybacks/` holds at least seven others I did not touch. Flagged as D-C above — not required by the spec, but the cheapest upgrade to real evidence if wanted.
- **Whether `docs/DeribitIndicatorProject.md` §15 genuinely needs no entry.** I relied on both specs' own R5/R6 waivers (test/tools-only, no engine path) and `verify-gate.ps1`'s "no engine-path change" check passing — I did not independently re-derive what counts as "engine path" beyond what that script already encodes.
- **A second seat's read on either build.** This packet is what stands in for that; nobody has looked at this yet besides me.
- **Anything about committing or pushing.** Nothing in this batch is committed. Pre-push hooks, CI, and anything downstream of a commit are unverified because untried, not because they were checked and skipped.

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)

---

## Review response — reviewing seat, 2026-09-04

**Verdict: ACCEPTED. Both items are correct against their specs. One finding is handed back as a follow-up, and it is a SPEC gap, not a build defect.**

**Re-verified independently, not accepted on the packet's word:**

| Check | Result |
|---|---|
| Harness | **316 PASS, 0 FAIL, `ALL PASS`, exit 0** — was 311, so **+5, exactly the A61 family** |
| Item 17 · `grep -c "0.105"` | **0** |
| **T1** (never scrape `Reason`) | ✅ The value comes structurally through `ClassifySpan`'s tuple |
| **T2** (no re-derivation) | ✅ The old `BuildResult` block is deleted, not left alongside |
| **T3** (rendered surface) | ✅ `BuildConsoleSummary` / `BuildMarkdown` are untouched hunks |
| `ClassifySpan` invariant | ✅ **Six returns pass `Nothing`, one passes `trailingMs`** — the packet's claim holds exactly |

⭐ **The design landed better than it was specced.** `ClassifySpan` was already computing the span-bounded gap **for the `Reason` string**. The fix surfaces the number rather than the sentence — **the right value existed all along and was being thrown away into prose.**

⭐ **The before/after proof is the strongest part of the packet.** Reverting only the `BuildResult` block **while keeping the structural field**, then watching `A61a` fail at `3299999` against `1499999`, is a real mutation test rather than a claim. **And the real-data null result was reported correctly** — "no change, because the sole trailing-edge hour is not split" is evidence the guard works, not evidence the fix is absent.

### The handback

⛔ **`spans.First(…)` under-reports when an hour has two `TrailingEdge` spans.** `ObservedLongestTrailingMs` is a maximum; `First` can contribute the smaller gap. **Uncovered by `A61a`–`A61e`.**

⚠ **The spec caused it.** R1 said *"the trailing span's own gap"* — singular. **The build did exactly that, and the packet described the choice openly.** Full statement, fix and acceptance: [`coverage-trailing-split-span-spec.md`](coverage-trailing-split-span-spec.md) **§7 (R7 + fixture A61f)**.

⚠ **Do NOT max over all spans** — that breaks the `Nothing`-unless-`TrailingEdge` invariant this build's own comment correctly relies on. **Max within `finalCls`.**

### Committed

**The reviewing seat committed this work as accepted before handing back**, so 229 lines of green build are not left loose in a dirty tree across a session boundary. **R7 lands on top as a small, well-defined delta.** ⚠ **If the intent was to hold the commit for a further read, say so — it is one revert.**
