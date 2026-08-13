# Spec-back — hole-derived repair windows (Part A)

> ## ✅ REVIEWED AND ACCEPTED — 2026-08-13, by the seat that wrote the spec. **Q1–Q5 ruled in §6 below.**
>
> **The build is sound. Every finding this packet raises against the spec is correct, and I verified each one in the tree rather than taking the packet's word for it** — the harness was re-run independently (**ALL PASS**, six A56 fixtures green), `repairHoles` really does print **3**, and the two `BackfillTradeMonthAsync` call sites are exactly as described.
>
> ⚠ **Two NEW findings, both defects in MY spec that the build implemented faithfully — see §6.** `MinHoleMs` filters a completeness signal on a TIME axis, and the scan's truncation path removes rows in FILE order while claiming to keep the newest.
>
> ⚠⚠ **Q4's premise is factually wrong and would have mis-planned the deploy trip.** The write-guard fix is **already deployed and production-verified** (2026-08-11 17:18:42Z, `a5d701ad…`, 61.88 % → 100 %). **The undeployed companion is the v66 settings change, which is a ⚠ dataset boundary and makes the trip more delicate, not less.**

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
- ⛔ **`docs/trader-tick-queue.md` §2 and the rest of `roadmap.md`.** I read the queue's §0 and §0a only. **Nothing in this document should be read as a claim about what else is outstanding.** ⚠ **This disclosure is load-bearing — it is exactly why Q4's premise is wrong. See §6.**

---

## 6. Review — 2026-08-13, by the seat that wrote the spec

**Effort: Opus / high.** A spec-back on a build in a never-throws write path, per CLAUDE.md's effort rule.

### 6.1 What I verified myself, and how

| Claim | How checked | Result |
|---|---|---|
| Harness ALL PASS | Built `OrderCheck.vbproj` Release, ran it | ✅ **0 warnings, 0 errors; ALL PASS; all six A56 fixtures green** |
| R2 — `repairHoles` prints 3 | `(Select-String … 'repairHoles').Count` | ✅ **3.** The spec's `V4` would have rejected a correct build |
| `V4` restated — two call sites | Enumerated `BackfillTradeMonthAsync(` | ✅ `HistoricalStore.vb:575` no argument · `TradeStoreGapRepair.vb:127` `repairHoles:=True` |
| `settings.json` unmoved | `git diff c38ea02..HEAD -- settings.json` | ✅ empty; still **v66** |
| `DeribitIndicatorProject.md` §15 row | `git show --stat b7db9c7` | ✅ present (1 line) |
| Every spec quotation in this packet | Compared against the proposal text | ✅ **all accurate** — no strawmen |
| R4 — `A56b` *calls* the shipped function | Read the fixture | ✅ it does, and `A56b` part 4 derives its width from `MinHoleMs` |
| A56f reads the production constant | Read the fixture | ✅ reads `MaxHolesPerPass` **and** `MinHoleMs`; fixture-literal provenance honoured |
| `MaxTradePages` not multiplied by 32 | Read the loop | ✅ `page` is shared across windows and `aborted` exits the outer loop — **no amplification** |
| `EnumerateMonths` clamps to the request | Read it | ✅ `segStart = If(cur < fromUtc, fromUtc, cur)` — the scan window is 20 h, not a whole month |

### 6.2 ⚠ `DR-1` (new) — `MinHoleMs` filters a COMPLETENESS signal on a TIME axis. My spec, not their build.

⚠ **RENAMED from `F-1` on 2026-08-13.** `F1` and `F2` already name the C1-coverage report's two findings **and** the `ResetBufferState` race, so the original labels would have sent a grep to the wrong defect. **Implementer brief: [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §1** — it carries one decision.

**D-2 rules detection on `trade_seq` only, "never on a time gap", because a time threshold is a tolerance.** Step 6 then drops any hole narrower than **`MinHoleMs` = 2,000 ms** — **a time tolerance, applied to the output of the completeness check.**

**What it hides.** A hole is dropped if the two bracketing trades are under ~2 s apart, **however many sequence numbers are missing between them.** A downtime hole is minutes, so the fix's headline case is unaffected — but **in-flight loss is not.** ⚠ **`F2`, the `ResetBufferState` race at `DeribitWsFeed.vb:298`, is still open and unfixed, and the losses it produces are exactly the sub-2-second shape this filter discards.**

⚠ **And the drop is SILENT.** The `MaxHolesPerPass` cap logs what it dropped, per the no-silent-caps convention. `MinHoleMs` logs nothing, and neither does the inverted-range case (`prev.Ts = cur.Ts`, which is unfetchable by a time-windowed API and is the write guard's own failure signature).

**What the filter buys:** on healthy tape, nothing — `delta = 0` and `delta < 0` already emit no window, so a complete store produces zero holes with or without it. **It protects against nothing and hides real loss.**

**My read: the axis is wrong.** The right floor is on **missing-sequence count** (which is the completeness measure) or no floor at all, with `MaxHolesPerPass` doing the bounding it already does. ⚠ **Not urgent and not deploy-blocking** — it cannot cause loss, only fail to recover some. But `A56b` part 4 now **pins the current behaviour as correct**, so the longer it stands the more it costs to change. **Its own item: Sonnet, low.**

### 6.3 ⚠ `DR-2` (new) — the truncation path removes rows in FILE order while claiming to keep the newest. Latent.

⚠ **RENAMED from `F-2` on 2026-08-13** — see §6.2. **Implementer brief: [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §2.** `DR-3` is R7 below, brief §3.

```vb
inWindow.Add(p)
If inWindow.Count > MaxScanRows Then
    inWindow.RemoveRange(0, MaxScanRows \ 10)   ' comment: "Keep the NEWEST rows"
```

**`inWindow` is in FILE order at this point** — the sort happens later, in `ResolveRepairWindowsMs`. So `RemoveRange(0, N)` removes the **earliest-written** rows, not the oldest by timestamp. ⚠ **For an out-of-order store — the premise of trap 1 — those are different rows.**

**Consequence:** the retained set acquires arbitrary interior gaps in the timeline. After sorting, the walk brackets across them and emits **phantom holes with very large missing-sequence counts** — which then **win the `MaxHolesPerPass` ranking and evict every real hole.** That is R6's failure mode reached by a second route, inside the guard written to prevent it.

**Severity: LATENT, not live.** It needs > 500,000 rows at or after `segStartMs` in one monthly file. At the measured true rate (31–60 trades/min) a 20 h lookback yields **37,000–72,000** — roughly **7× margin**, which is what the constant was sized for. ⚠ **The reachable path is `gap_repair_lookback_hours`, an unguarded settings key**: past ~140 h the cap bites. That is far beyond Deribit's ~24 h retention and therefore pointless — but nothing stops it.

**My read: fix it when convenient, do not hold the deploy.** Either sort before truncating, or track the cut by timestamp. **Its own item: Sonnet, low.** ⚠ **Either way, correct the comment** — it currently asserts a property the code does not have, which is how a reader stops checking.

### 6.4 ⚠⚠ Q4's premise is factually wrong

> *"This and the write-guard fix are the same trip — both are code-only, both are undeployed."*

**The write-guard fix is deployed and production-verified:** AWS `a5d701ad-eea1-4ba0-97a5-2ea05274c8c5` since **2026-08-11 17:18:42Z**, `trade_seq` completeness **61.88 % → 100 %**. Recorded in [`seat-handover-2026-08-12.md`](seat-handover-2026-08-12.md) §1 and in the banner of [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md).

**This is a direct consequence of §5's honest disclosure that the queue's §2 was not read** — the packet says so plainly, which is why the error is catchable rather than hidden. **It is a good argument for that disclosure convention, not against it.**

⚠ **The real companion on this trip is the v66 settings change**, which is still undeployed on both boxes — and that makes the trip **more** delicate, not less: `settings.json` **hot-reloads**, so a version edge can land mid-`InstanceId` and become unfilterable. **Stop → swap → start on both boxes**, then record both ids in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a.

### 6.5 ⚠ R7 degrades the instrument that verifies this build

The packet ranks R7 as *"worth its own small ticket"*. **It is more entangled than that.** `BackfillTradeMonthAsync` returns `CountDataRows(path)` — the **whole file's row count** — when the window list is empty, and **empty is the common case on a healthy box.** So every healthy pass logs the entire August file (~300,000) as *"N row(s) appended"*, and `TotalRowsRepaired` accumulates it.

⚠ **§4's falsifiable prediction is verified by observing that a hole gets filled.** Anyone reading the repair log to check it will read a number that is meaningless. **Verify `V6` from the store's own `trade_seq` completeness, not from the repair log** — or fix R7 first. Pre-existing, correctly not fixed inside a one-caller change; the packet's judgment there was right.

### 6.6 Rulings — Q1 … Q5

| # | Ruling |
|---|---|
| **Q1** | ✅ **RATIFIED — BREAK, as built.** The reasoning is correct, the mutation proves it, and the spec's `A56d` shape genuinely could not have caught it. §0 trap 2 of the proposal is **amended**, with the disclosed cost (covers less on legacy ground) written into §4.1's invariant |
| **Q2** | ✅ **YES — D-4 now rules three constants.** The proposal's §4.2 table is amended and `MaxScanRows` carries its rationale |
| **Q3** | ✅ **YES** — see §6.5. ⚠ **CLARIFIED 2026-08-13: this does NOT gate the deploy.** `V6` reads `trade_seq` completeness from the **store**, not the repair log, so the verification that matters is unaffected. **Deploy first; DR-3 after.** Its own brief §0.1 carries the three reasons. **What it costs meanwhile: the repair log's "N row(s) appended" is unusable as a recovery metric — do not read it as one** |
| **Q4** | ⚠ **Premise corrected — see §6.4.** Deploy this binary **with the v66 settings**, stop → swap → start on both boxes, record both `InstanceId`s. **Trader's call to schedule** |
| **Q5** | ✅ **Done.** [`trader-tick-queue.md`](trader-tick-queue.md) §2 updated by me — the row now reads BUILT + REVIEWED, awaiting deploy. **You were right not to edit it** |

### 6.7 What I did NOT verify

- ⛔ **Anything live.** No deploy, no outage, no `V6`. Unchanged from the packet's own §5.
- ⛔ **Mutations 1–5.** I read the mutation matrix and re-ran the harness clean; **I did not re-run each mutation.** Same limit the previous seat recorded against the write-guard build's mutation table.
- ⛔ **The truncation path's behaviour under an actual 500,000-row scan.** F-2 is derived from reading the code, not from producing the condition.
- ⛔ **Read cost.** Reasoned, not measured — same as the packet.
