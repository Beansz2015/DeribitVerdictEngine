# C1-coverage F1 — trailing-edge gap mis-attribution fix: spec-back

**For the reviewing seat.** Companion to [`coverage-trailing-edge-f1-batch-summary.md`](coverage-trailing-edge-f1-batch-summary.md) (what happened). This document is the working packet — what to check, what's queued, and feedback on the spec itself. Format per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).

Spec built against: [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) §4b.

---

## 1. Ranked verification handles

**If you only run one: the pre-fix/post-fix `Defect`-count diff in the batch summary §6.** It is a real comparison (`git stash`, rebuild, run, compare, restore, rebuild, run again), not an assertion — `4` both times, exit `1` both times under `--strict`, over the same real store window.

Ranked by how much of the build each one covers:

1. **`grep -c "^PASS  F1-" ` on a harness run** — must print `6`. Confirms the new fixtures actually executed, not merely that they compile and exist in source (the project's own named five-instance failure mode).
2. **The mutation table in the batch summary §4** — for each of `F1-a`, `F1-b`, `F1-d`, `F1-e`: does the paired mutation genuinely flip that fixture's `PASS` to `FAIL`, and does the revert genuinely restore green? This is the cheapest single check that the fixtures are testing the mechanism and not a tautology.
3. **`grep -n "TrailingEdge" tools/BacktestRunner/CoverageReport.vb"` should show the enum member, one combine-precedence insertion, one `ClassifySpan` branch, one console line, one VERDICT-line term, one `BuildMarkdown`-adjacent absence of change** (no `BuildMarkdown` edit — it renders the class for free). If a reviewer finds a SECOND place the class is checked or excluded, something drifted from D-5(c)'s "renders for free" premise.
4. **Arithmetic identity:** in the real-store run (batch summary §6), `captured + DEFECT + trailing-edge + expected-missing + not-capturing + unknown-scope + out-of-scope-weekend` should equal the total walked-hour count both pre-fix and post-fix, and the two totals should be equal to each other. (100+4+0+0+0+112+96 = 312 both times, matching a 13-day weekday-inclusive walk.)
5. **`git diff --stat`** should show exactly two source files touched (`tools/BacktestRunner/CoverageReport.vb`, `verify/ordercheck/Program.vb`) plus the two new docs and the `DeribitIndicatorProject.md` §15 entry. No `settings.json` diff, no `BacktestProgram.vb` diff.

---

## 2. Decisions queued

**None.** All eleven decisions in `coverage-trailing-edge-f1-proposal.md` §4b were already ruled before this build started, and the brief explicitly forbade re-opening them without new code-level evidence. Nothing found during the build contradicted any of the eleven rulings — every trap, forced value, and precedence claim in §4a/§4b checked out against the live file exactly as stated.

One **wording** note, not a decision (see §3 below, "where the spec was narrower than its own words") — resolved without needing the reviewer's judgment, but worth knowing it existed.

---

## 3. Spec-back proper — feedback on the spec

**What the spec got right, specifically:**

- The §6 fixture blast-radius table's estimates were not hand-waved — they were precise enough to double as a correctness check on my own implementation. It predicted A49o's ON-half span would produce "~27 min" trailing edges and A49t's middle span "~10 min"; my actual math landed at 26.99 min (1,619,999ms) and 9.9999 min (599,999ms) respectively. When a spec's estimate matches the implementation's arithmetic to four significant figures, that is strong evidence the author actually traced the code rather than reasoned about it abstractly — worth continuing this level of precision in future specs of this shape.
- §4a.1's "collapse into one caller-computed MIN" framing for D-4(c) was exactly right and made the parameter-threading design fall out cleanly: one scalar into `ClassifyHour`, one more `Min()` inside `ClassifySpan` against that span's own end. No wider signature change was needed anywhere.
- The explicit list of which existing fixtures need **no** edit (`A49b`/`A49c`/`A49l`/`A49r`, `A49g`, `A49u`) and *why* (nullable defaults, goes-through-`BuildResult`, knife-edge math) was directly checkable and all six held exactly as claimed on the first try — no fixture needed an edit the spec didn't already predict, and no fixture the spec said was safe actually broke.

**Which assumptions broke:** none. Every D-table entry, every trap description, and every fixture prediction in §4b/§6 matched the live code on inspection. This is unusual enough to be worth stating plainly rather than manufacturing a finding.

**Where the spec was narrower than its own words:**

- §4b's D-5.1 row and the closing "verified 2026-08-25" note both describe the enum insertion as "between `Defect` and `Captured`." Taken **literally against the enum's actual declared order** (`Captured` first, then `Defect` — confirmed at the top of the file before any edit), there is no position literally between those two names, since `Captured` is declared *first*. The intended position is recoverable from the spec's own verification handle, though: it states the insertion "shifts the ordinals of four members," and only one candidate position produces exactly four shifted ordinals — immediately after `Defect` (pushing `ExpectedMissing`/`NotCapturing`/`UnknownScope`/`OutOfScopeWeekend` down one each). I used that arithmetic to derive the position rather than guess from the prose, and it is worth flagging because a reader who takes "between Defect and Captured" as a literal declaration-order instruction (rather than a description of *combine precedence*, which is a separate, correctly-literal instruction elsewhere in the same row) could place it in the wrong spot without the ordinal count catching them — VB doesn't care about enum declaration order for any of this code's purposes, so nothing would fail loudly. Future specs prescribing a mid-enum insertion might state the target ordinal position directly, or state the "N members shift" fact as the primary instruction rather than a side verification.
- F1-c ("the partial-final-hour case") is listed in §6 as **not** requiring mutation-proofing, correctly — but building it exposed that D-4(c)'s three-way `MIN(spanEnd, boundaryMs, storeEndMs)` collapses to an effective two-way interaction in almost every constructible scenario: `storeEndMs`, once correctly filtered to `[fromUtc, toUtc)`, is *always* `< walkToUtcMs` by construction whenever any trade exists in range, so `walkToUtcMs`'s own contribution to the `Min()` is structurally redundant except when `storeEndMs` is `Nothing` (no trades in range at all). F1-c's assertion is behaviorally correct (it proves the end state isn't wrongly flagged) but does not — and structurally cannot, without a fixture shaped very differently from anything else in the table — isolate the `walkToUtcMs` term's contribution the way F1-a/F1-b/F1-e isolate `storeEndMs`'s. Worth naming so a future reader doesn't assume F1-c is doing more isolation work than it is; the practical proof that the boundary term does real work independent of `storeEndMs` is that `BuildResult`'s `walkToUtcMs` still participates literally in the `Min()` call and nothing in the ruled design suggested dropping it.

**Constraint pairs that nearly conflicted:** none rose to the level of a near-deadlock. The closest was reconciling D-2's "keep `LongestGapMs` and the new field separable" with D-6(c)'s "report the new pair beside, not folded into, the old pair" — both point the same direction (keep the two kinds of silence distinguishable end to end, from the per-hour stats through the header counters), so there was no tension to resolve, just two rulings that happened to reinforce each other. Named only because it's the kind of alignment worth confirming rather than assuming.

---

## 4. What I did not verify, and cannot

- **No live pull from the production AWS box (`i-0d6c133058876273e`).** The pre-fix/post-fix comparison in the batch summary §6 runs against the local `AWS-copybacks/aws-copyback-2026-08-14/` snapshot, ~12 days stale relative to today (2026-08-26). The comparison methodology (git-stash, rebuild, run, diff, restore) is sound and would produce the same *kind* of confirmation against fresher data, but I did not have SSH/SSM access to pull a fresher copy this session, and did not attempt to authenticate one that wasn't already asked for.
- **`--verify-venue` (S0) was not exercised at all.** It needs live HTTP and this fix does not touch `ComputeVenueDiff`/`RunVenueDiffAsync`/`VenueDiffResult` in any way — I read but did not modify that code path, and did not run it live to confirm it still composes correctly with the rest of `CoverageResult`. Low risk (no shared state with the trailing-edge fields), but genuinely unexercised this session.
- **`ObservedLongestTrailingMs`'s precision on a split hour whose winning span is `TrailingEdge`.** `BuildResult`'s counter computation (§4a.3-style simplification, matching the pre-existing imprecision the sibling `ObservedLongestGapMs`/`GapBreachHours` counters already carry for split/weekend hours) uses the WHOLE-HOUR `HourStoreStats`, not the specific winning span's stats, to compute the displayed trailing-ms figure. `F1-d` proves the *classification* is correct for this case (asserts `.Classification`), but no fixture asserts the exact `ObservedLongestTrailingMs` value in a split-hour scenario — I reasoned through the arithmetic by hand (documented in-code and in the batch summary) rather than pinning it with a fixture, since the spec's D-6(c) ruling only requires the counter to exist beside the other two, not that it be split-span-exact, and the classification correctness (the part that actually gates `Defect`/`TrailingEdge`/`Captured`) is fully fixture-proven independent of this display-only number.
- **The `docs/DeribitIndicatorProject.md` §15 entry I added** is my own judgment call to follow an observed convention (the 2026-08-25 AutoTweaker weekday-filter entry is tools-only and got one) — the brief did not explicitly require it. If it's unwanted for tools/-only changes, it's a one-paragraph revert.
