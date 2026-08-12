# Spec-back — hole-derived repair windows (Part A)

> ## ✅ BUILT AND GATE-PASSED. One deviation from the spec text, ranked first, proven by mutation.
>
> **The deviation is not a judgment call I am asking you to bless after the fact — it is a defect in the spec's algorithm, and the spec's own fixture shape would not have caught it.** Read **R1** before anything else.

**Build:** commit `c6c6942`, 2026-08-13. **Spec:** [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md), §7 D-table ticked in full 2026-08-12.
**Author:** the implementer seat that opened on the proposal, 2026-08-13. **Effort run:** Opus / high, as the spec's §0 recommended.
**Deploy state:** ⚠ **NOT DEPLOYED.** `settings.json` does not move, so AWS keeps losing every ride-through outage until it gets the new binary.

---

## 0. What was built, in one paragraph

`TradeStoreWriter.ResolveRepairWindowsMs` is a new pure function on the existing `Core/` seam. It returns the `trade_seq`-bracketed holes **behind** the tail as well as the tail itself. `HistoricalStore.BackfillTradeMonthAsync` gains an opt-in `repairHoles` parameter and pages within each returned window; `TradeStoreGapRepair.RepairOnceAsync` is the only caller that opts in. Three `Public Const`, no settings keys, no version bump, no rendered surface. Six fixtures, `A56a`–`A56f`.

**Files touched:** [`Core/TradeStoreWriter.vb`](Core/TradeStoreWriter.vb) · [`tools/BacktestRunner/HistoricalStore.vb`](tools/BacktestRunner/HistoricalStore.vb) · [`TradeStoreGapRepair.vb`](TradeStoreGapRepair.vb) · [`verify/ordercheck/Program.vb`](verify/ordercheck/Program.vb) · plus this document, the proposal's status header, and `docs/DeribitIndicatorProject.md` §15.

---

## 1. Verification handles, ranked

⚠ **Each tests the property, not a string that mentions it** (hard rule, 2026-08-11). **`V4` as the spec wrote it fails this rule and is restated below — see R2.**

| # | Handle | Expected | Result |
|---|---|---|---|
| **V1** | `powershell -NoProfile -File tools\checks\verify-gate.ps1 -Mode prepush` | **GATE PASSED**, six projects Release 0/0, harness **ALL PASS** | ✅ **as expected** |
| **V2** ⭐ | **Mutation 1** — comment out the `For Each k In kept : result.Add(k.Range)` loop in `ResolveRepairWindowsMs`, rebuild `OrderCheck`, re-run | ⚠ **`A56a` FAILS.** If it passes, stop — the test is not testing what it claims | ✅ **`A56a`, `A56e`, `A56f` all FAILED** |
| **V2b** ⭐⭐ | **Mutation 3** — change the walk's `If cur.Seq < 0 Then hasPrev = False : Continue For` to a bare `Continue For` (the spec's literal "skip") | ⚠ **`A56d` FAILS on its interleaved part and PASSES on its era-boundary part** | ✅ **exactly that — this is R1's proof** |
| **V3** | `Select-String settings.json -Pattern '"version"' -TotalCount 1` | **`"version": 66`, unchanged** — no key moved | ✅ **v66, `git diff settings.json` empty** |
| **V4** | ⚠ **restated, see R2.** `grep -rn "BackfillTradeMonthAsync(" --include=*.vb .` and read the **call sites** | Exactly two. `HistoricalStore.vb:575` (`BackfillAllAsync`, the historical backfill) passes **no** `repairHoles` argument; `TradeStoreGapRepair.vb:127` passes `repairHoles:=True` | ✅ **as expected** |
| **V5** | Release rebuild of the gate's six projects | **0 warnings, 0 errors** | ✅ **plus `tools/WsTradeProbe`, which is outside the gate build set — see F4** |
| **V6** | Post-deploy, next copy-back: `trade_seq` completeness across a span containing a known outage | **~100 %**, hole filled | ⛔ **NOT RUN — requires the deploy and a real outage** |

### 1a. The mutation matrix — five mutations, not the one the spec asked for

⚠ **Run all five if you re-verify. Mutation 1 alone leaves `A56c` and `A56d` green, so a build verified to the letter of the spec would have shipped with the no-phantom half of the property untested.**

| # | Mutation | Fixtures that FAIL |
|---|---|---|
| **1** | Tail-only return (the spec's `V2`) | `A56a`, `A56e`, `A56f` |
| **2** | Remove the `rows.Sort(...)` — Trap 1 | `A56c`, `A56e` |
| **3** | Skip seq-less rows instead of breaking — **R1** | `A56d` **(interleaved part only)** |
| **4** | Remove the `Seq < 0` guard entirely — Trap 2 | `A56d` **(both parts)** |
| **5** | Trailing window ignores the resume point | **all six** |

**Every fixture in the family is killed by at least one mutation.** `A56b` is the regression guard and is killed only by mutation 5 — that is correct for its job, and mutation 5 exists specifically so its teeth are not merely asserted.

---

## 2. Findings against the spec — ranked, with my read

### R1 ⚠⚠ — the walk must BREAK on a seq-less row, not SKIP past it. The spec says skip, and its own fixture cannot catch the difference.

**The spec text.** §0 trap 2: *"Skip rows without a sequence; require **both** bracketing rows to carry one."* §4.1 step 3: *"Walk the rows that **carry a sequence** (`TradeSeq >= 0`), skipping the rest."*

**Why skipping is wrong.** Take an identified row at T1 (seq 3000), three legacy rows at T2–T4 (no sequence), an identified row at T5 (seq 3500). Both bracketing rows carry a sequence, so the spec's stated requirement is satisfied. Delta is 500, so a hole is emitted over `[T1+1, T5−1]` — **ground the three legacy rows already cover.** A phantom, costing a REST fetch, in exactly the mixed-era store trap 2 exists to protect.

**Why the spec's fixture cannot catch it.** `A56d` as specified is *"Legacy rows (`TradeSeq = AbsentSeq`) adjacent to identified rows ⇒ zero phantom holes **at the era boundary**"*. In that shape — a legacy block beside an identified block — there are seq-carrying rows on **one side only**, so no bracketing pair spans the legacy rows and **no phantom can arise under either reading.** The fixture passes whichever decision you take. **It does not test the decision it was written for.**

**Proven, not argued.** `A56d` was built with **two** parts. Under mutation 3 (skip), part 1 — the spec's own shape — reports `era=True`. Part 2 — interleaved — reports `interleaved=False (n=2 ...)`: the phantom, exactly as predicted.

**What I did and why I did not stop to ask.** The walk **breaks**. Breaking can only ever *miss* a hole in legacy ground and can never invent one, and **D-6 already rules that the pre-fix era is not to be chased**. So the resolution is forced by a decision you have already taken; there was no new question to put. **It costs nothing today:** every row written since 2026-08-10 carries a sequence, so inside the 20 h lookback the two readings are byte-for-byte identical. The difference only appears on legacy data that D-6 excludes.

**⚠ My read: this needs your explicit tick anyway**, because it is a deviation from ruled spec text and because the reasoning above is mine, not yours. If you disagree, the change is one line and `A56d` part 2 is the test that flips.

---

### R2 ⚠ — `V4` counted a name instead of testing a property, and would have rejected a correct build

**The spec's handle:** *"`Select-String tools/BacktestRunner/HistoricalStore.vb -Pattern 'repairHoles'` on the historical-backfill call path → **no hit** — that path is untouched."*

**It prints three hits on a correct build**, because the new parameter is *declared* in that file — the doc comment, the signature, and the branch. A reviewer following the handle literally would have rejected a sound build.

⚠ **This is the R1 finding from [`trade-store-write-guard-spec-back.md`](trade-store-write-guard-spec-back.md) repeating, one build later.** That one counted `_lastTs` and printed 2 when the correct answer was 0, both hits inside comments explaining the field's removal. **The pattern is the same: the handle counted a name, and a name is a copy of the property.**

**The property is about the CALL SITE, not the file.** Restated in §1 above: enumerate the call sites and read whether each passes the argument. **My read: no decision owed — this is feedback on the spec's verification section, not on the build.**

---

### R3 — `MaxScanRows` is required by §4.1 but is absent from §4.2's table and from D-4

§4.1 step 1 says the scan is *"bounded by a constant row cap"*. §4.2's constant table lists only `MinHoleMs` and `MaxHolesPerPass`, and **D-4 rules only those two**. So a third constant is mechanically required and formally unruled.

**What I did:** added `Public Const MaxScanRows As Integer = 500000` on D-4's own stated principle — no failure-rate linkage, nobody will tune it. Sized at ~7× a 20 h lookback at the measured true rate. **Held as two `Long`s per row, not as `TradeRecord`s** — ~8 MB rather than the ~50 MB the records would cost, which matters because this runs on a background timer inside a WinForms app.

**When the cap bites, the NEWEST rows are kept and the truncation is LOGGED** — a silent cap reads as "covered everything". ⚠ **A truncated scan also discards the pre-`segStart` bracket row**, because the rows the cap dropped sit between the bracket and the oldest retained row, and bracketing across them would manufacture a phantom.

**My read: no decision owed, but D-4 should be amended to say three constants** so the next reader does not find an unexplained one.

---

### R4 — §4.1's stated invariant is not literally true, and `A56b` was told to pin it

**The spec:** *"The LAST window this function returns is exactly today's `ResolveResumeCursorMs` result."*

**`ResolveResumeCursorMs` reads the file's LAST LINE** — its own summary says so, and §0 trap 1 asserts the store is out of order. **After sorting, this function's tail comes from the MAXIMUM timestamp.** For an out-of-order store the two differ, so the invariant contradicts the trap the sort exists to fix.

**What I did:** used the maximum, and restated the invariant precisely at the function. **Max cannot under-cover** — the span between the last line and the maximum is bracketed by rows at *both* ends, so any sequence gap inside it is emitted as a hole by the walk. `A56b` asserts the equivalence by **calling** the shipped `ResolveResumeCursorMs` and comparing, on an in-order store; `A56c` asserts the deliberate divergence on an out-of-order one, and additionally asserts the file **really is** out of order so a future refactor that starts writing sorted cannot turn that fixture into a tautology.

**My read: no decision owed. The spec's sentence should be amended, not the code.**

---

### R5 — §4.1 step 1's row filter and step 5's clamp are in tension

Step 1 reads *"the rows of `path` whose `Timestamp >= segStartMs`"*. Step 5 says to *"clamp a partially-overlapping hole"* into the window. **With step 1's filter as written, a hole straddling `segStartMs` has no left bracket and is invisible, so step 5's clamp is unreachable for the case it exists to serve.**

**What I did:** the scan additionally keeps the **single row with the greatest timestamp below `segStartMs`** — the maximum, not the last one seen, because the file is not sorted and those are different rows. It is kept whether or not it carries a sequence: a seq-less bracket correctly *breaks* the walk rather than licensing a phantom across the boundary. `A56e` pins it.

**My read: no decision owed. It is what step 5 requires; step 1's wording is the loose part.**

---

### R6 — the AbsentSeq trap is dangerous for a different reason than the spec gives

**The spec:** *"Feeding −1 into the gap arithmetic makes each legacy→identified boundary look like a hole ~296 million wide."*

**True of the sequence COUNT, not of the time WINDOW.** The emitted window is `[ts(prev)+1, ts(next)−1]` — bounded by two *adjacent* rows' timestamps, so it stays narrow no matter how absurd the delta.

⚠ **The real damage is the ranking.** `MaxHolesPerPass` keeps the largest holes **by missing-sequence count**. A single AbsentSeq-derived phantom scores ~296 million, wins the top-32 outright, and **evicts every real hole from the pass.** Same fix, much worse consequence than the spec describes. Recorded at the code and in the `A56` family header so the next reader does not weaken the guard on the strength of "the window is small anyway".

---

### R7 — pre-existing, not introduced: `BackfillTradeMonthAsync`'s return value is not "rows appended"

When the window list is empty the function returns `CountDataRows(path)` — **the whole file's row count**, not rows appended. `TradeStoreGapRepair` sums that into `TotalRowsRepaired`, whose summary reads *"Total rows appended by repair passes this process"*, and `RepairOnceAsync` logs it as *"N row(s) appended"*.

**This is shipped behaviour and I preserved it exactly** rather than fix it inside a change whose stated virtue is a one-caller blast radius. **Flagging, not fixing. My read: worth its own small ticket; it makes the repair log unreadable as a recovery metric, which matters more now that repair actually recovers things.**

---

### R8 — feedback on §2.4's stop-and-ask: I agree, and Part A is untouched by G-1

Part B is **not** built. §2.4 says build Part A, watch one outage self-heal, then re-decide. Nothing in Part A depends on G-1's residual, which §8 already records as an inference. **No decision owed from me; recorded so the sequencing is not lost between seats.**

---

## 3. Decisions queued for you

| # | Question | My read |
|---|---|---|
| **Q1** ⚠ | **R1 — ratify BREAK over SKIP** on a seq-less row, and amend §0 trap 2 accordingly | **Break.** Forced by D-6, free today, and the only reading under which `A56d` tests anything. **Recommend: tick as built** |
| **Q2** | **R3 — amend D-4 to three constants**, adding `MaxScanRows` | **Yes.** Ruled on D-4's own principle; a table that lists two when the code has three is the drift class this project keeps finding |
| **Q3** | **R7 — spin off the `TotalRowsRepaired` over-report** as its own item? | **Yes, small.** Not urgent; it was harmless when repair recovered ~nothing and is misleading now that it does |
| **Q4** | **The deploy.** This and the write-guard fix are the **same trip** — both are code-only, both are undeployed | **Deploy both together.** Record the new `InstanceId` in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a. **⚠ Not my call to make** |
| **Q5** | **The queue.** [`trader-tick-queue.md`](trader-tick-queue.md) §0a still points at the proposal as *"authorised and ready to hand over"* | **Needs a tick.** I did not edit the queue — its §2 is your board and I have not read it in full |

---

## 4. The falsifiable prediction, unchanged from the spec

> **After Part A ships and one outage occurs, the tape hole for that outage is filled within `gap_repair_interval_hours` (6 h) of the venue returning, provided the outage is inside the 20 h lookback. If a hole survives a full pass cycle, Part A did not find the whole problem.**

**Kept verbatim on purpose.** It was written before the build and nothing I found while building weakens it.

---

## 5. What I did NOT verify

- ⛔ **Anything live.** No deploy, no real outage, no post-fix `trade_seq` completeness read. **`V6` is unrun and is the only handle that tests the fix against the venue.**
- ⛔ **That Deribit serves a complete result for a MID-HISTORY window.** Gap repair has depended on `get_last_trades_by_instrument_and_time` since v64, so this adds no new dependency — **but every window it has ever asked for was at the TAIL, and this change is the first thing that asks for one that is not.** The spec names this and I did nothing to close it. **The first post-deploy hole-fill is the test.**
- ⛔ **Concurrency.** The `_running` interlock and the process-wide `_appendLock` are unchanged and I reasoned about them only. No live run.
- ⛔ **The scan's read cost on a full month file.** One streaming pass plus a sort of the in-window rows, holding two `Long`s per row. **I reasoned it and bounded it with `MaxScanRows`; I did not measure it.**
- ⛔ **Whether Deribit ever resets `trade_seq`.** Still unverified project-wide. A reset mid-window presents as one enormous *count* against a *narrow* time window (R6), so `MaxHolesPerPass` and the per-hole clamp bound the damage to a wasted fetch — **but the behaviour is not designed for.**
- ⛔ **The month-boundary residual.** A hole straddling a month rollover is covered from the older file's side only. Named in the spec's §4.4, not solved, not tested.
- ⛔ **`docs/DeribitIndicatorProject.md` §15's own five-row rule.** I added a row because the write-guard fix — equally settings-untouched and equally zero-scoring-impact — has one, and that is the governing precedent. ⚠ **The section is now further over its stated five-row limit.** CLAUDE.md says the fix is to move the oldest rows to `history-archive.md`. **I did not do that; it is not this build's scope and it would bury an unrelated edit inside a store commit.**
- ⛔ **`docs/trader-tick-queue.md` §2 and the rest of `roadmap.md`.** I read the queue's §0 and §0a only. **Nothing in this document should be read as a claim about what else is outstanding.**
