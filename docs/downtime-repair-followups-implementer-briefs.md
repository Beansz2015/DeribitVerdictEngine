# Downtime-repair follow-ups — three implementer briefs

**Source:** the 2026-08-13 review of [`trade-store-downtime-repair-spec-back.md`](trade-store-downtime-repair-spec-back.md) §6. **Build under repair:** `c6c6942` (Part A, hole-derived repair windows).
**Status:** ⚠ **DR-1 carries one decision (§1.2). DR-2 and DR-3 are ready to hand over as written.**

---

## 0. ⚠⚠ READ FIRST — the IDs were renamed, because the old ones collided

These were first written as **F-1**, **F-2** and **R7**. **All three names were already taken in this project**, and an implementer grepping for them would land on the wrong thing:

| Old name | What it ALSO means in this repo |
|---|---|
| ~~`F1`~~ | **C1-coverage F1** — the trailing-edge attribution fix, still an open build slot |
| ~~`F2`~~ | **C1-coverage F2** — the split-hour rule, *ruled 2026-08-12* · **and** the `ResetBufferState` race at `DeribitWsFeed.vb:298`, a live open defect |
| ~~`R7`~~ | A finding index reused in every spec-back in `docs/` |

**They are now `DR-1`, `DR-2`, `DR-3`** (DR = downtime repair). ⚠ **Use the new names.** [`trader-tick-queue.md`](trader-tick-queue.md) §2 and [`trade-store-downtime-repair-spec-back.md`](trade-store-downtime-repair-spec-back.md) §6 have been updated to match.

### 0.1 ⚠ How to split these across conversations — NOT one each

| Conversation | Carries | Why |
|---|---|---|
| **1** | **DR-1 + DR-2 together** | ⚠ **Both edit `Core/TradeStoreWriter.vb`'s new code and both touch the `A56` fixture family. Two parallel sessions WILL collide.** |
| **2** | **DR-3 alone** | Different seam — a return-value contract in `HistoricalStore` / `TradeStoreGapRepair`. No overlap |

**Neither blocks the Part A deploy.** ~~⚠ **But DR-3 should land before the post-deploy verification read** — see §3.~~

> ⚠ **CLARIFIED 2026-08-13 — that sentence read as if DR-3 gated the deploy. It does not, and DEPLOY FIRST is the ruling.**
>
> **DR-3 gates one *reading*, not the deploy.** ✅ **The verification that matters — `V6`, `trade_seq` completeness across a span containing a known outage — reads the STORE, not the repair log, and DR-3 is not on that path at all.** What DR-3 fixes is that the repair log's headline number is unusable as a recovery metric.
>
> ⚠ **Three reasons the deploy goes first:** (1) the defect being fixed is **unrecoverable tape loss on a ~24 h clock**, and DR-3 changes nothing about what gets captured — every day undeployed is permanent loss bought for a logging fix; (2) `V6` is store-based, so nothing is lost by waiting; (3) ⚠ **DR-3 is the only one of the three that can touch a path OUTSIDE gap repair** — its own §3.3 step 1 exists because nobody has yet read what `BackfillAllAsync` does with the return value. **Bundling it would widen the deploy's blast radius for zero capture benefit.**
>
> **The operational cost of deploying without it, stated so it is not a surprise:** during the post-deploy watch, `[TradeStoreGapRepair] pass complete — N row(s) appended` reports the **whole month file** on every healthy pass. **Do not read it as a recovery metric. Read `trade_seq` completeness from the store instead.**

---

## 1. DR-1 — `MinHoleMs` filters a completeness signal on a TIME axis

### 1.1 Brief

> **Model: Sonnet. Effort: medium.** *(Raised from the "low" first quoted: the edit is three lines, but two shipped fixtures assert the behaviour being removed and one of them exists solely to pin it. Deciding what those fixtures should assert instead is the work.)*
>
> **Why that tier.** The judgment is done and recorded below, and every mechanical piece has an in-repo template — `A56b` and `A56f` are the files you edit and they are twenty lines apart. What makes it more than a one-liner is that **removing a constant means deciding what its fixtures now test**, and a fixture that is deleted rather than repointed silently reduces coverage.
>
> **Where it slips.** ⚠ **Deleting `A56b` part 4 instead of repurposing it.** That part is the only fixture touching the narrow-hole path at all; if the floor goes, the part should become *"a 1-ms-wide sequence gap is UNFETCHABLE and is dropped, and says so in the log"* — the property that genuinely survives. ⚠ **And `A56f` asserts `w(0).WidthMs >= MinHoleMs` as a sanity check** that the cap, not the floor, produced its count; with no floor that assertion needs replacing, not removing.
>
> **Escalation trigger — stop and come back.** If removing the floor makes any existing `A48*` or `A56*` fixture fail for a reason you cannot explain in one sentence, **stop**. The floor may be load-bearing somewhere this review did not look.

### 1.2 ⚠ The one decision — my recommendation, tick or overrule before opening the session

**D-4 of [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md) ruled `MinHoleMs = 2000` as a constant. Removing it amends a ruled decision, so it needs a tick.**

> ✅ **My recommendation: REMOVE the width floor entirely. Do not replace it with a count floor.**

**Why removal rather than a smaller number:** any non-zero time floor hides real loss, because time width and completeness are different axes. **Why not a count floor instead:** the smallest possible real hole is `delta = 2` (one missing sequence), and `delta <= 1` already emits no window — so a count floor of 1 is exactly equivalent to no floor. There is nothing for it to filter.

⚠ **If you overrule and want a floor kept**, the implementer's instruction changes to: *replace `MinHoleMs` with `MinMissingSeqs`, applied to `h.MissingSeqs`, and log every drop* — same fixtures, different assertion.

### 1.3 What is wrong

`docs/trade-store-downtime-repair-proposal.md` **D-2** rules hole detection on `trade_seq` **only**, *"never on a time gap"*, because a time threshold is a tolerance and this project's store-integrity lesson is that **a guard checking a fixed tolerance rather than completeness turns one bad fetch into permanent silent loss.**

Step 6 of that same spec then drops any hole whose window is narrower than **`MinHoleMs` = 2,000 ms** — **however many sequence numbers are missing inside it.** A tolerance, applied to the output of the completeness check.

**What it hides.** Downtime holes are minutes, so the headline case is unaffected. **In-flight loss is not.** ⚠ **The `ResetBufferState` race (`DeribitWsFeed.vb:298`) is still open and unfixed, and the losses it produces are exactly this sub-2-second shape.**

⚠ **And the drop is SILENT.** The `MaxHolesPerPass` cap logs what it dropped, per the no-silent-caps convention. `MinHoleMs` logs nothing. Neither does the inverted-range case.

**What the floor buys on healthy tape: nothing.** `delta = 0` (a surviving duplicate) and `delta < 0` (a discontinuity) already emit no window, so a complete store produces zero holes with or without it.

### 1.4 What to change

**File:** [`Core/TradeStoreWriter.vb`](Core/TradeStoreWriter.vb), `ResolveRepairWindowsMs` steps 5–6.

1. **Delete the `If clamped.WidthMs < MinHoleMs Then Continue For` filter.**
2. **Delete the `Public Const MinHoleMs`** — or keep it only if a fixture still reads it. Do not leave an unread public constant.
3. ⚠ **KEEP the inverted-range drop** (`If e < s Then Continue For`). A window where `prev.Ts = cur.Ts`, or where the two are one millisecond apart, is **unfetchable** — the venue API takes a time range, so there is no sub-millisecond query. This is inherent, not a defect.
4. ⚠ **LOG the inverted-range drop.** It is the write guard's own failure signature (same-millisecond siblings), so if that guard ever regresses, this log line is how anyone notices. **A silent drop here is the same mistake in miniature.**

### 1.5 Fixtures

| Fixture | Change |
|---|---|
| `A56b` **part 4** | ⚠ **Repurpose, do not delete.** It currently asserts *"a sub-`MinHoleMs` gap is not fetched"*. It should become: **a real sequence gap between two trades 1 ms apart is dropped as unfetchable AND logged**, while **a real sequence gap of any fetchable width IS returned** — which is the property that replaces the floor |
| `A56f` | ⚠ Its `widthOk` check reads `MinHoleMs` to prove the cap, not the floor, produced its count. **Replace that assertion** — assert the count against `MaxHolesPerPass` directly |
| **new part** | A hole spanning **well under 2,000 ms** with several missing sequences ⇒ **returned**. ⚠ **This must FAIL on current code.** It is the whole point |

⚠ **Fixture-literal provenance (hard rule):** the `A56` family header already declares these fixtures assert **MECHANISM**, so constructed timestamps and sequence numbers stay literals with the comment saying so. Anything read from a production constant must be **read**, never restated.

### 1.6 Verification handles

⚠ Each tests the property, not a string that mentions it.

| # | Handle | Expected |
|---|---|---|
| **1** | New sub-2-second fixture run against **pre-change** `OrderCheck` | ⚠ **FAILS.** If it passes, the test is not testing what it claims — **stop** |
| **2** | `tools\checks\verify-gate.ps1 -Mode prepush` | GATE PASSED, six projects Release 0/0, harness **ALL PASS** |
| **3** | `git diff settings.json` | **empty** — no key, no version bump |
| **4** | Grep `MinHoleMs` across the tree | **Either zero hits, or hits only where a fixture reads it.** No orphaned public constant |

---

## 2. DR-2 — the scan's truncation path removes rows in FILE order while claiming to keep the newest

### 2.1 Brief

> **Model: Sonnet. Effort: medium.** *(Also raised from "low": the code fix is small, but the fixture is the hard part — the condition needs 500,000 rows and the obvious way to test it changes a public constant.)*
>
> **Why that tier.** The defect is fully diagnosed below and the fix is a few lines. The difficulty is entirely in **proving it**, and the tempting shortcut damages the design.
>
> **Where it slips.** ⚠ **Do NOT demote `Public Const MaxScanRows` to a variable or a `Friend` field to make it testable.** A ruled constant goes `Public Const` (2026-08-11 ruling) and a fixture must read the production number, not move it. **The correct route is a private overload that takes the cap as a parameter, with the public entry point passing `MaxScanRows`** — the production path keeps one number and the fixture can drive a cap of 10. ⚠ **Second trap: "just sort the list every time it overflows."** That is O(n log n) on every overflow inside a read loop. Sort once at the cut, or track a cut timestamp.
>
> **Escalation trigger.** If the fix requires `ScanForRepair` to hold `TradeRecord`s rather than the two-`Long` `SeqPoint`, **stop** — that is a 6× memory increase on a background timer inside a WinForms app and it needs re-deciding, not absorbing.

### 2.2 What is wrong

**File:** [`Core/TradeStoreWriter.vb`](Core/TradeStoreWriter.vb), `ScanForRepair`.

```vb
inWindow.Add(p)
If inWindow.Count > MaxScanRows Then
    inWindow.RemoveRange(0, MaxScanRows \ 10)   ' comment claims: "Keep the NEWEST rows"
    truncated = True
End If
```

⚠ **`inWindow` is in FILE order at this point.** The sort happens later, in `ResolveRepairWindowsMs`. So `RemoveRange(0, N)` removes the **earliest-written** rows, not the oldest by timestamp — and for an out-of-order store those are different rows. **The store being out of order is the premise of trap 1 and the reason the sort exists at all.**

**Consequence.** The retained set acquires arbitrary interior gaps in the timeline. After sorting, the walk brackets across them and emits **phantom holes carrying very large missing-sequence counts** — which then **win the `MaxHolesPerPass` ranking and evict every real hole from the pass.** That is the exact failure mode the `AbsentSeq` guard exists to prevent, reached by a second route.

**Severity: LATENT, not live.** It needs more than 500,000 rows at or after `segStartMs` in one monthly file. At the measured true rate (31–60 trades/min) a 20 h lookback yields **37,000–72,000** — about **7× margin**, which is what the constant was sized for. ⚠ **The reachable path is `gap_repair_lookback_hours`, an unguarded settings key**: past roughly 140 h the cap bites. That is far beyond Deribit's ~24 h retention and therefore pointless — but nothing in the code stops it.

### 2.3 What to change

**The property to satisfy, stated so the fix can be judged against it:**

> **After truncation, the retained rows must be CONTIGUOUS IN TIME — no interior gaps — so that no bracketing pair in the walk spans a discarded row.**

Two acceptable routes; pick one and say which:

- **(a) Sort at the cut.** When the cap bites, sort `inWindow` by `(TsMs, Seq)`, drop the oldest block, and continue. Later arrivals below the new floor are filtered out as they are read.
- **(b) Track a cut timestamp.** Maintain a `cutMs` floor; on overflow, raise it and drop everything below it. Cheaper, and it makes the contiguity property explicit.

⚠ **Whichever you pick, the existing bracket handling stays correct and must not be dropped:** a truncated scan already discards the pre-`segStart` bracket row, and it must keep doing so — the rows the cap dropped sit between the bracket and the oldest retained row.

⚠ **Fix the COMMENT either way.** It currently asserts a property the code does not have, which is how a reader stops checking.

### 2.4 Fixtures

- **New `A56g`**: a scan whose cap is exceeded, over an **out-of-order** file, ⇒ **zero phantom holes**, and `truncated` is reported. Drive it through the private overload with a small cap.
- ⚠ **Must FAIL on current code.** Build it against the unfixed `ScanForRepair` first and watch it fail before writing the fix.
- The `A56` family is currently `A56a`–`A56f`; **`A56g` is the next free letter.** (Family high-water across the harness is `A56`; `A55g` was the previous.)

### 2.5 Verification handles

| # | Handle | Expected |
|---|---|---|
| **1** | New `A56g` against **pre-change** code | ⚠ **FAILS.** If it passes, **stop** |
| **2** | `verify-gate.ps1 -Mode prepush` | GATE PASSED, harness **ALL PASS** |
| **3** | Grep for `MaxScanRows` | **Still `Public Const`**, one production value, read by the fixture rather than restated |
| **4** | `git diff settings.json` | **empty** |

---

## 3. DR-3 — `TotalRowsRepaired` over-reports: it sums the whole FILE, not rows appended

### 3.1 Brief

> **Model: Sonnet. Effort: medium.** *(The change is small; the scoping is the work.)*
>
> **Why that tier.** ⚠ **This is a shared function with two callers that may want different answers**, and the naive fix changes behaviour for a caller that is not the one complaining. Working out which caller wants what is the task; the edit is a few lines.
>
> **Where it slips.** ⚠ **Changing the return value for BOTH callers without checking what `BackfillAllAsync` does with it.** The historical backfill at [`tools/BacktestRunner/HistoricalStore.vb`](tools/BacktestRunner/HistoricalStore.vb):575 may be using "file row count" deliberately as a progress figure. **Read that call site before touching the contract.** ⚠ **Second trap: fixing the LOG STRING instead of the VALUE.** Rewording *"N row(s) appended"* leaves `TotalRowsRepaired` — a public property whose summary says *"Total rows appended by repair passes this process"* — still wrong.
>
> **Escalation trigger.** If the two callers genuinely need different return semantics and you find yourself adding a boolean to switch them, **stop and put the question back** — that is a second `repairHoles`-shaped flag on a function that just got one, and it may want a separate entry point instead.

### 3.2 What is wrong

**Pre-existing behaviour, not introduced by `c6c6942`** — but newly consequential.

`BackfillTradeMonthAsync` returns `CountDataRows(path)` — **the whole file's row count** — when there is nothing to fetch:

```vb
If windows.Count = 0 Then Return CountDataRows(path)
```

⚠ **"Nothing to fetch" is the common case on a healthy box.** `TradeStoreGapRepair.RepairOnceAsync` sums that into `TotalRowsRepaired` and logs it as *"N row(s) appended"*. So **every healthy pass reports the entire month file — currently ~300,000 rows — as rows it just recovered.**

⚠ **Why this matters more than tidiness: it degrades the instrument that verifies the Part A build.** That build's falsifiable prediction is *"the tape hole for that outage is filled within 6 h of the venue returning"*, and the natural place to check it is the repair log. **That number is meaningless.** Until this is fixed, **verify from the store's own `trade_seq` completeness instead.**

### 3.3 What to change

1. **Read `BackfillAllAsync` at `HistoricalStore.vb:575`** and record what it does with the return value. That decides the scope.
2. Make the repair path return **rows actually appended**, zero when nothing was fetched.
3. Update `TradeStoreGapRepair.TotalRowsRepaired`'s summary and the `RepairOnceAsync` log line so the words and the number agree.
4. ⚠ **If the historical backfill genuinely wants the file count, do not force one contract on both** — say so in the spec-back and propose the split rather than picking silently.

### 3.4 Fixtures

- A repair pass over a **fully covered** store ⇒ the reported total is **0**, not the file's row count. ⚠ **Must FAIL on current code.**
- A repair pass that appends **n** rows ⇒ reports **n**.
- ⚠ **`A48d` already exercises the fully-covered path** — check whether it can carry the first assertion before adding a new fixture.

### 3.5 Verification handles

| # | Handle | Expected |
|---|---|---|
| **1** | New fully-covered-store fixture against **pre-change** code | ⚠ **FAILS** |
| **2** | `verify-gate.ps1 -Mode prepush` | GATE PASSED, harness **ALL PASS** |
| **3** | Read `TotalRowsRepaired`'s summary and the log line beside the value they now carry | **They agree.** ⚠ Do not grep for the words — read the value's source |
| **4** | `git diff settings.json` | **empty** |

---

## 4. Common to all three

- **No settings keys, no version bump, no ⚠ dataset boundary.** The engine reads trades from `MarketState`'s in-memory ring and never from the store, so all three have **zero scoring impact**.
- **No rendered surface.** ⚠ The engine display-string parity rule still applies: **state in the commit message that no card surface is affected**, rather than leaving it unsaid.
- **Every commit that changes engine behaviour gets a `docs/DeribitIndicatorProject.md` §15 row.** ⚠ That section is already over its own five-row limit and a trim to `history-archive.md` is owed — **add the row, do not do the trim inside a store commit.**
- **Each of these needs its own spec-back** on the [`batch-review-packet-convention.md`](batch-review-packet-convention.md) pattern if the session produces findings against these briefs. **Say plainly what you did not verify.**

---

## 5. What I did not verify

- ⛔ **That removing `MinHoleMs` breaks nothing outside the `A56` family.** I grepped its call sites; **I did not run the harness with it removed.** That is DR-1's handle 1.
- ⛔ **DR-2's condition.** It is derived from reading the code, not from producing 500,000 rows.
- ⛔ **What `BackfillAllAsync` does with the return value.** DR-3 step 1 exists because I did not read it — **do not assume it is unused.**
- ⛔ **Anything live.** No deploy, no outage, no post-fix completeness read.
