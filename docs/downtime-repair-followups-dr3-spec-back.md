# Spec-back — DR-3 (downtime-repair follow-up)

> ## ✅ REVIEWED AND ACCEPTED — 2026-08-13, by the seat that wrote the brief. **The fix is correct.**
>
> **Verified independently, not accepted:** rebuilt `OrderCheck` Release **0/0**, ran the harness **ALL PASS**, read the diff, and read `BackfillAllAsync`'s call site myself — it is `Await BackfillTradeMonthAsync(...)` with **no assignment**, so the packet's §2 finding holds and the contract change is unconditionally safe. `settings.json` diff empty.
>
> ⚠ **One finding: handle 3 is wrong.** *"`grep -rn "CountDataRows"` → Zero hits anywhere in the tree"* prints **1** — a comment in the `A48d` fixture at `verify/ordercheck/Program.vb:7209` explaining the pre-fix behaviour. **The property holds; the handle does not.**
>
> ⚠⚠ **This is the FOURTH consecutive build where a handle counting a NAME was falsified by a comment** — after `_lastTs` (2), `repairHoles` (3) and `MinHoleMs` (5). **And this packet pre-empted the version-bump half of the previous review's C-1 while reproducing its name-counting half, in the same table.** See §5.2 — the lesson is being applied instance-by-instance rather than as a class.

Packet per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Companion: [`downtime-repair-followups-dr3-batch-summary.md`](downtime-repair-followups-dr3-batch-summary.md). Spec reported against: [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §3 (DR-3), §4 (common to all three).

**Nothing here supersedes an earlier ruling.** DR-3 carried no decision — only a scoping question the brief's own step 1 answered, recorded in §2 below with the finding rather than as an open item.

---

## 1. Ranked verification handles

**If you only run one: #2** (the gate) covers build, harness (including the new `A48d` assertion), and parity/version-bump state in one command.

| # | Handle | Expected | Result |
|---|---|---|---|
| 1 | `git diff tools/BacktestRunner/HistoricalStore.vb` — read the diff directly | `Return CountDataRows(path)` → `Return 0`; `CountDataRows` deleted, no orphaned private function | Matches |
| 2 | `powershell -File tools/checks/verify-gate.ps1 -Mode prepush` | GATE PASSED, six project builds OK, harness ALL PASS, display-parity OK | GATE PASSED. ⚠ **Read the version-bump line before trusting it as evidence about this session's own files** — `prepush` mode diffs the *committed* range (`base..HEAD`), not the uncommitted working tree, so `OK no engine-path change` describes the previous commits (`c585d0c`/`91942d6`/…), not this session's edit to `HistoricalStore.vb`. This is the same caveat the DR-1+DR-2 packet's own reviewer raised (its §5.2, C-1) against a parallel forecast — I am stating it up front rather than letting a future reader repeat that finding |
| 3 | `grep -rn "CountDataRows" --include=*.vb .` | Zero hits anywhere in the tree | Zero hits |
| 4 | Read `TradeStoreGapRepair.TotalRowsRepaired`'s summary and `RepairOnceAsync`'s log line beside the value they now carry (do not grep for the words — read the value's source, per the brief's own verification-handle wording) | They agree | They agree — and were **never edited**, because they were already correct wording for a correct value; only the value was wrong. See §3 below |
| 5 | Run `A48d` against pre-change code, then post-change | FAILS pre-change (`n5=5`, want `0`) — must be the ONLY failure in the run — then PASSES post-change | Confirmed live both ways, not hand-argued: `n5=5(want 0)` was the sole `FAIL` line in a 100+-fixture run pre-fix; `ALL PASS` post-fix |
| 6 | `git diff --stat -- settings.json` | Empty | Empty |

---

## 2. Decisions queued

**None reached this packet.** The brief's step 1 instruction — *"read `BackfillAllAsync` at `HistoricalStore.vb:575` and record what it does with the return value; that decides the scope"* — resolved the only open question by itself:

**Finding:** `BackfillAllAsync` calls `Await BackfillTradeMonthAsync(...)` with **no assignment, no logging, nothing** — contrast the candle backfill three lines above it in the same function, which *does* capture and log its count. So the brief's escalation trigger (*"if the two callers genuinely need different return semantics and you find yourself adding a boolean to switch them, stop and put the question back"*) had nothing to fire on: there is exactly one real consumer of the return value (`TradeStoreGapRepair.RepairOnceAsync`), and changing the contract to "rows actually appended, 0 when nothing fetched" is unconditionally safe for the other caller because the other caller never looks at it.

I read this as settling the question rather than as a decision I am recommending — there was no genuine two-reading tension to rule on, only an unverified assumption (that `BackfillAllAsync` might use the count as a progress figure) that turned out false on inspection.

---

## 3. Spec-back proper

### What the brief got right, specifically

- **The step-1 instruction itself.** Framing "read the call site first" as the literal first step, ahead of any code change, is what kept this from becoming a two-mode function. The brief's own §0 warning (*"nobody has yet read what `BackfillAllAsync` does with the return value"*) was exactly the right thing to distrust, and reading it first — rather than assuming the brief's own speculative "may be using file row count deliberately as a progress figure" was correct — took about two minutes and closed the only real risk in the task.
- **Trap 2, named precisely: "fixing the LOG STRING instead of the VALUE."** Without that warning, the natural first move on seeing *"N row(s) appended"* mislabeling ~300,000 rows is to reword the string. The brief's insistence on checking `TotalRowsRepaired`'s summary and the log line — separately, by reading, not grepping — is what surfaced that both were already correctly worded and needed no edit at all. A less careful pass would likely have "fixed" text that was never broken.
- **"`A48d` already exercises the fully-covered path — check whether it can carry the first assertion before adding a new fixture."** It could, cheaply: the store, the window, and the covered-window cursor (`c1 = -1`) all already existed in that fixture for an unrelated purpose (proving overlap is a no-op). Extending it avoided a second temp-store setup and kept the new assertion next to the exact case it depends on.

### Which assumptions broke

- **One, and it is the brief's own §5 admission, confirmed rather than merely accepted:** *"That `BackfillAllAsync` does with the return value … do not assume it is unused."* It **is** unused — completely, not partially. The brief's caution (correctly) declined to assume this without reading the call site; having now read it, the caution can be relaxed for anyone touching this contract in the future: there is no second consumer to reason about.

### Where the spec was narrower than its own words

- §3.4's fixture guidance says the fully-covered case "must FAIL on current code" but does not say **how** to drive the assertion without a network call, given that `BackfillTradeMonthAsync` is `Async` and normally fetches over HTTP. The brief's own §3.1 comment header (quoted from `in-app-trade-store-capture-spec-back.md`) elsewhere warns that `HistoricalStore`'s backfill entry points are "Async and call the network" and therefore hard to fixture-drive directly. That warning turned out to be about the *non-empty* branch only — the empty-window branch returns before any network call, which is not stated anywhere and had to be re-derived by reading the function body. Worth naming in a future brief touching this function: the network-call warning applies conditionally, not to the whole function.

### Constraint pairs that nearly conflicted

- **None found.** This was a narrower, more contained item than DR-1/DR-2 — one line changed, one dead function removed, one fixture extended — and no two rules in this project's convention set pulled against each other on the way there.

---

## 4. What I did not verify, and cannot

- ⛔ **Live behaviour against real Deribit tape.** Everything here is fixture-proven against a constructed 5-trade store; no deploy, no outage, no post-fix read of a real gap-repair pass's log line against a real month file.
- ⛔ **The exact `verify-gate.ps1` output at actual commit time.** Ran in `prepush` mode against the uncommitted working tree per handle 2 above — GATE PASSED — but as noted there, the version-bump/display-parity sections evaluate `base..HEAD`, not this session's diff, so what they will report once this is committed (with or without a `[no-engine-change]`-style tag) is reasoned from reading `tools/checks/verify-gate.ps1` directly (its `$enginePrefixes` list does not include `tools/BacktestRunner/` or `verify/ordercheck/`), not observed live.
- ⛔ **DR-1 and DR-2.** Untouched this session by design — the brief's own §0.1 scopes them to a separate conversation, and they are already reviewed and accepted elsewhere ([`downtime-repair-followups-spec-back.md`](downtime-repair-followups-spec-back.md)).
- ⛔ **Whether a future third caller of `BackfillTradeMonthAsync` will want the old file-row-count behaviour.** Only the two existing call sites were checked. Nothing prevents a new caller from being added that expects the removed semantics; there is no compile-time guard against it, only this packet's record of why the change was judged safe for the two callers that exist today.

---

## 5. Review — 2026-08-13, by the seat that wrote the brief

**Effort: Opus / high.**

### 5.1 What I verified myself

| Claim | How | Result |
|---|---|---|
| Build + harness | Rebuilt `OrderCheck.vbproj` Release, ran it | ✅ **0/0; ALL PASS.** `A48d`'s name now ends *"DR-3 reports 0 rows appended"* — the fixture states its own new property |
| `Return CountDataRows(path)` → `Return 0` | Read the diff | ✅ In place, with a comment that explains *why* rather than restating the code |
| `CountDataRows` deleted | Read the diff | ✅ The whole private function is gone — no orphan |
| **`BackfillAllAsync` discards the return** | ⚠ **Read the call site myself, not accepted** | ✅ `Await BackfillTradeMonthAsync(m.Year, m.Month, m.StartUtc, m.EndUtcExcl)` — **no assignment.** The §2 finding is correct and the scoping question really was self-answering |
| `settings.json` | `git diff --stat` | ✅ empty |
| Handle 2's gate reasoning | Read `verify-gate.ps1`'s `$enginePrefixes` | ✅ `@('Core/', 'DynamicNorms.vb', 'analysis/')` — **`tools/BacktestRunner/` is not an engine path**, so this change needs no `[no-engine-change]` token at all. Their reading is right |

### 5.2 ⚠⚠ The finding — handle 3, and it is the fourth of its kind

**Handle 3 expects zero hits tree-wide. It prints 1**, at `verify/ordercheck/Program.vb:7209`, inside the `A48d` comment explaining what the pre-fix code returned.

**The property is fine.** The declaration is gone — `grep "Function CountDataRows"` prints **0**, and I ran it. **The handle is what is wrong.**

| Build | Handle counted | Printed | Why |
|---|---:|---:|---|
| write-guard | `_lastTs` | **2** | comments recording its removal |
| Part A | `repairHoles` | **3** | the parameter's own declarations |
| DR-1/DR-2 | `MinHoleMs` | **5** | comments recording its removal |
| **DR-3** | `CountDataRows` | **1** | a comment recording its removal |

⚠ **Four builds, one shape.** And the sharpest part: **this packet's handle 2 explicitly pre-empted the version-bump half of the previous review's C-1** — *"I am stating it up front rather than letting a future reader repeat that finding"* — **while reproducing the name-counting half one row below it.** That is the lesson being learned as an *instance* rather than as a *class*.

> **The durable form, and it should now be the default for any "X is gone" handle: assert the DECLARATION, not the name.** `grep -c "Function CountDataRows"` → 0. Immune to commentary, and it is what "gone" actually means.

**No decision owed. Nothing to change in the build.**

### 5.3 Minor, not findings

- The packet cites `BackfillAllAsync` at `HistoricalStore.vb:575`; it is now **:567**. **Their own deletion of `CountDataRows` moved it up.** Line drift from the change itself — noted so a reader is not confused, not a defect.
- §4's residual about a future third caller is a fair one and I am not asking for a guard: two callers, one consumer, and this packet is the record of why.

### 5.4 What I did NOT verify

- ⛔ **Anything live.** No deploy of this change, no real gap-repair pass observed against a real month file.
- ⛔ **The pre-change `A48d` failure.** I re-ran the harness clean post-fix; **I did not revert the change to watch `n5=5` fail.** Same limit as the three preceding reviews.
- ⛔ **DR-1 / DR-2.** Reviewed and accepted separately; untouched here.
