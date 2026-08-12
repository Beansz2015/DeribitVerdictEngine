# Gap repair cannot heal downtime — hole-derived resume cursor

> ## ✅ D-TABLE TICKED IN FULL — 2026-08-12. D-1 … D-6 all approved as recommended.
>
> **Part A is authorised and ready to hand to an implementer as written.**
>
> ⚠ **Gate G-1 is now PARTIALLY answered and the answer changes Part B — read §2 before building it.** The outage was a **venue-wide Deribit outage**, not a box-local fault. **Part B is authorised but its value has dropped**, and §2 carries a stop-and-ask: build Part A, watch one outage self-heal, then re-decide whether Part B is worth touching the live feed supervisor. **Do not open a Part B session before that.**

**Status:** ✅ **PART A IS BUILT — 2026-08-13, commit `c6c6942`.** §7 D-table ticked in full 2026-08-12. Gate `prepush` PASSED, harness ALL PASS, fixtures A56a–f proven by five mutations. ⚠ **NOT YET DEPLOYED to AWS.** Review packet: [`trade-store-downtime-repair-spec-back.md`](trade-store-downtime-repair-spec-back.md) — it carries **one deviation from this document's §0 trap 2**, ranked first, and shows why this document's own **A56d** shape would not have caught it. **Part B remains UNBUILT and deferred by §2.4's stop-and-ask.**
**Fixes:** [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md) **§5a** and **§5a-bis**.
**Author:** the orchestrator seat that opened on [`seat-handover-2026-08-12.md`](seat-handover-2026-08-12.md), 2026-08-12.

---

## 0. Implementer brief — model, effort, and where it slips

**Model: Opus. Effort: high. One session for Part A; Part B is a separate session and is gated on §2's G-1.**

**Why that tier.** The judgment work below is done and the change surface is small — one new pure function, one call-site loop, six fixtures. It is still Opus/high for three reasons, all of them about the failure mode rather than the size:

- It lives in the **same never-throws write path** as the write-guard defect. A wrong fix here does not throw, does not log, and looks exactly like correct behaviour — which is what let the `<=` guard survive ten months.
- **The obvious fix is the wrong one.** Widening `gap_repair_lookback_hours` changes nothing; the cursor skips the hole, not the window. An implementer who reads only §5a-bis's headline will ship a settings change and close the item.
- The correctness property is **two-sided** — no missed holes *and* no phantom holes — against a store that has three eras, at least one known out-of-order block, and a documented `AbsentSeq` sentinel. One-sided reasoning passes its own tests.

### Where it will slip — four concrete traps

1. ⚠ **The store is not sorted, and this is recorded in the code you are editing.** `TradeStoreWriter.LastTradeTimestamp`'s own doc comment says it reads *the file's last line, not its maximum timestamp*, and that *"the store already holds one out-of-order block"*. Repair appends its pages **after** whatever streaming has already written. **Scanning the file in append order reports a phantom hole at every repair-block boundary, and each phantom costs a REST fetch.** Sort the scanned window before detecting holes.
2. ⚠ **An absent `trade_seq` is not a sequence number.** `TradeRecord.AbsentSeq` is **−1** and every pre-2026-08-10 row carries it. Feeding −1 into the gap arithmetic makes each legacy→identified boundary look like a hole ~296 million wide. **This is the same trap the identity build recorded as "never key on an absent identity", one seam over** — see the `⚠ NEVER key on an absent or empty identity` comment in `Core/TradeStoreWriter.vb`. Skip rows without a sequence; require **both** bracketing rows to carry one.
3. ⚠ **An unfillable hole is re-detected on every pass, forever.** A hole past Deribit's ~24 h retention will be found, fetched, return nothing, and be found again six hours later. The pre-fix era holds **7,471 gap runs**. The lookback clamp bounds this — but only if it is applied to **each hole**, not just to the pass's outer window. Clamping the window and not the hole reintroduces the whole cost.
4. ⚠ **Do not widen `gap_repair_lookback_hours`.** Stated again here because it is the single most likely wrong turn, and because it will *appear* to work on a hand-built fixture.

### Escalation triggers — stop and come back

- **If fixture A56a cannot be made to FAIL on current code before the fix lands, stop.** The test is not testing what it claims. This is the write-guard build's precedent and it is the only thing separating a real test from a passing one.
- ⚠ **If hole detection needs a TIME-GAP THRESHOLD** — *"no rows for more than N seconds means a hole"* — **stop and re-take §7's D-2.** A time threshold is a tolerance, and this project's recorded store-integrity lesson is that *a guard checking a fixed tolerance rather than completeness turns one bad fetch into permanent silent loss*. ASIA at 03:00 is legitimately sparse; a tolerance will either miss real holes or invent them. `trade_seq` gives completeness with no threshold at all, which is the entire reason to use it.
- **If Part A's change needs to touch `HistoricalStore`'s network code** beyond looping over windows, stop — the split that put `TradeStoreWriter` in `Core/` exists precisely so this decision stays network-free and harness-reachable.

---

## 1. The defect, in one line

> **`TradeStoreWriter.ResolveResumeCursorMs` seeds the fetch cursor from the file's LAST WRITTEN ROW. Once streaming reconnects, that row is current again, so every hole behind it is permanently "already covered" and is never fetched.**

```vb
Dim resumeMs As Long = LastTradeTimestamp(path)   ' the file's LAST LINE
cursorMs = resumeMs + 1
If clampToSegStart AndAlso cursorMs < segStartMs Then cursorMs = segStartMs
If cursorMs > segEndInclMs Then Return -1          ' "already covered" ⇒ no fetch
```

**The measured instance.** The tape carries a **60.3-minute hole, 2026-08-11 08:59:56 → 10:00:12, zero rows, never filled** despite repair having over seven hours and two scheduled passes. `3be7f4c9` ran unbroken across the outage, so no restart fired the start-triggered pass; by the 12:36 scheduled pass, streaming had long since made the tail current. **Cost: one hour of tape, now past Deribit's ~24 h retention and unrecoverable at any price.**

**The rule this establishes, because it is not what the v64 design assumes:**

> **Gap repair recovers downtime ONLY if the app is RESTARTED after the outage, or if a scheduled pass happens to land inside the outage window. An app that rides through an outage and reconnects on its own loses the hole within seconds.**

⚠ **That is the common case, not the rare one.** A WS feed reconnecting after a venue blip is exactly what the supervisor in `DeribitWsFeed.RunLoopAsync` is built to do. The v64 header comment on `TradeStoreGapRepair.vb` states the assumption in its own words — *"streaming capture is complete while the app runs and recovers NOTHING from downtime"* — and **the first half of that sentence was already falsified by the write-guard defect; this proposal falsifies the practical value of the second half.**

---

## 2. Gate G-1 — ⚠ PARTIALLY ANSWERED 2026-08-12, and it demotes Part B

**The question was:** during 2026-08-11 09:00–10:00 UTC, did the feed **disconnect and reconnect**, or stay connected and go silent?

### 2.1 The trader's observation — what it establishes

**Reported 2026-08-12: both boxes showed the amber `ANALYSIS SKIPPED` hero with no indicator readings.** That is the P4f VERDICT-card skipped state — `MainForm_Render_Cards.vb`, 28 pt bold in `ACC_AMBER_DEEP` with a reason sub-line in `ACC_WARN` — reached from `MainForm_Analysis.vb`'s `skipReason` branch when a **required REST fetch returns nothing**.

**Three things this settles:**

1. ✅ **The outage was VENUE-WIDE, not box-local.** Both boxes, simultaneously. That rules out an AWS network fault and rules out a box-specific silent stall.
2. ✅ **REST was down too**, not only the WS stream — the skip branch fires on the REST fetch, and it fired.
3. ✅ **The app was alive and running throughout.** It rendered the skipped card each cycle. It did not crash, so the start-triggered repair pass never fired — confirming §5a-bis's trace by a second route.

### 2.2 ⚠ What it does NOT settle, stated plainly

**It does not directly observe whether the WS socket dropped.** `ANALYSIS SKIPPED` is about the REST fetch; WS connection state renders separately in the LOG-line segment (`WS DOWN — reconnecting (Xs backoff, R reconnects)` vs `WS DEGRADED` vs `WS OK`), which the trader does not recall.

**The mechanism strongly implies DISCONNECTED** — a venue answering 503 on every endpoint does not hold WebSocket sessions open — but that is an **inference, not an observation.** ⚠ Recorded as an inference on purpose: a spec justified by an unobserved mechanism is the exact failure in [`seat-handover-2026-08-12.md`](seat-handover-2026-08-12.md) §6 item 1. **The observable proof, if anyone wants it, is `ReconnectCount` climbing or `connecting to …` lines in the collector console log for that window.**

### 2.3 ⚠ The consequence — and it cuts AGAINST Part B

**A venue outage removes one of the two escape hatches in §5a-bis's rule.** That rule allows recovery if *"a scheduled pass happens to land inside the outage window"*. **For a venue outage that hatch does not exist** — a pass landing inside the window calls a REST endpoint that is returning 503, fetches nothing, and appends nothing. Only a pass **after the venue returns** can help.

**And that squeezes Part B from both sides:**

| | |
|---|---|
| Part B fires | on WS reconnect — i.e. **the instant the venue comes back** |
| It must then | issue a REST backfill **against a venue that has only just returned from maintenance**, which may still be serving partial results or 503s |
| And it must | win a race against streaming's first flush (≤ one flush interval, 30 s) or the cursor is current again |
| Part A meanwhile | retries **every 6 h for as long as the hole is inside the 20 h lookback** — roughly three attempts before the hole ages out |

⚠ **So Part A is the robust mechanism and Part B is a single shot fired at the worst possible moment.** Part A's retry loop is also Part B's safety net, which means Part B's only marginal benefit is *filling the hole in seconds instead of within six hours* — and nothing on the board needs that latency.

### 2.4 The stop-and-ask

> **Build Part A. Deploy it. Wait for one real outage and confirm the hole self-heals on the scheduled cadence. THEN re-decide whether Part B is worth touching `DeribitWsFeed.RunLoopAsync`.**

**D-3 is ticked and Part B stays authorised** — this is a sequencing instruction, not a reversal. But **do not open a Part B session before Part A has been observed working**, and if Part A self-heals cleanly, say so and re-put the question rather than building Part B by momentum.

**Part A does not depend on G-1 at all**, which is why §4 orders it first.

---

## 3. Why the two candidate fixes are not equal

| | **(a) Hole-derived cursor** | **(b) Fire a pass on WS reconnect** |
|---|---|---|
| What it changes | *Where* repair starts fetching | *When* repair runs |
| Covers a **crash / restart** outage | ✅ (already covered today) | ✅ (already covered today) |
| Covers a **ride-through reconnect** | ✅ | ✅ **only if the feed actually disconnected** — G-1 |
| Covers a **silent stall** (WS up, no messages) | ✅ | ❌ |
| Covers a **flush / disk failure** (`AppendRows` logs and drops) | ✅ | ❌ |
| Depends on winning a race against streaming's first flush | ❌ | ⚠ **yes** — the window is one flush interval |
| Fires against a venue that has **just** left maintenance | ❌ — next scheduled pass, hours later | ⚠ **yes, by construction** — see §2.3 |
| Retries if the first attempt fails | ✅ **every 6 h while the hole is in the lookback** | ❌ **single shot** |
| Would it have recovered the measured 60.3-minute hole? | ✅ **yes, at the existing 12:36 pass** | ⚠ **probably, but only by winning a ≤30 s race against a just-recovered venue** — see §2 |

⚠ **The decisive line is the last one.** With a hole-derived cursor and **no schedule change at all**, the already-scheduled 12:36 pass would have found the 09:00–10:00 hole — inside both the 20 h lookback and Deribit's ~24 h retention — and filled it. **(b) alone might have recovered nothing.**

**(b) also carries a race that (a) does not.** Repair fired on reconnect runs concurrently with streaming; if streaming's first flush lands first, the tail is current again and the pass is a no-op. That is recoverable — capture the resume point *before* subscribing and pass it in explicitly — but it is a second mechanism to get right for a strictly smaller benefit.

**Recommendation: (a) is the fix. (b) is a timeliness improvement on top, worth having, and worth its own session.**

---

## 4. The design

### 4.1 Part A — `ResolveRepairWindowsMs`, a new pure function on the existing seam

**Home:** `Core/TradeStoreWriter.vb`. Host-agnostic, network-free, harness-reachable. This follows the precedent already set by `ResolveResumeCursorMs`, whose own doc comment says it was extracted onto this seam *"so the claim is testable without a live HTTP call, and so the live decision and the tested decision are the same code."* **Same reason, same place.**

```vb
Public Shared Function ResolveRepairWindowsMs(path As String,
                                              segStartMs As Long,
                                              segEndInclMs As Long,
                                              clampToSegStart As Boolean) As List(Of LongRange)
```

**Behaviour, in order:**

1. Read the rows of `path` whose `Timestamp >= segStartMs`, streaming, bounded by a constant row cap.
2. **Sort by `Timestamp`.** ⚠ Trap 1. Non-negotiable.
3. Walk the rows that **carry a sequence** (`TradeSeq >= 0`), skipping the rest. ⚠ Trap 2.
4. Where two consecutive sequence-carrying rows have `seq(next) − seq(prev) > 1`, emit the hole window `[ts(prev) + 1, ts(next) − 1]`.
5. Drop any hole that falls entirely outside `[segStartMs, segEndInclMs]`; clamp a partially-overlapping hole into it. ⚠ Trap 3 — **clamp each hole, not only the outer window.**
6. Drop holes narrower than `MinHoleMs`.
7. Cap the result at `MaxHolesPerPass`, keeping the **largest** by missing-sequence count, and **log how many were dropped** — a silent cap reads as "covered everything" when it did not.
8. **Append the trailing window last:** `[lastRowTs + 1, segEndInclMs]`, omitted when `lastRowTs + 1 > segEndInclMs`.

**The invariant that makes this safe, and the one the fixtures must pin:**

> **The LAST window this function returns is exactly today's `ResolveResumeCursorMs` result**, and an empty list is exactly today's `−1`. Everything else is strictly additional.

That makes the existing no-op property (fixture A48d — *"gap-repair overlap is a no-op by construction"*) a **special case** of the new function rather than something the change has to be argued not to have broken.

### 4.2 Part A — the call site

`HistoricalStore.BackfillTradeMonthAsync` currently resolves one cursor and pages forward. It gains an **opt-in** parameter and loops over the returned windows, paging within each:

```vb
Optional repairHoles As Boolean = False
```

⚠ **Opt-in, so the historical backfill path is byte-identical.** `BackfillTradeMonthAsync` is shared: `TradeStoreGapRepair.RepairOnceAsync` passes `clampToSegStart:=True`, the historical backfill passes `False`. **Only gap repair opts in.** This mirrors how `clampToSegStart` was added and keeps the blast radius at one caller.

**Two constants, not settings keys** — per the 2026-08-11 ruling, a value ruled into a constant goes `Public Const` so fixtures read the production number instead of restating it:

| Constant | Proposed | Why a constant |
|---|---|---|
| `MinHoleMs` | **2,000** | Below the flush interval; no failure-rate linkage; nobody will tune it |
| `MaxHolesPerPass` | **32** | A backstop against a pathological era, not a tuning knob |

**No new settings keys ⇒ no `settings.json` version bump ⇒ no dataset boundary and no ⚠.** The engine reads trades from `MarketState`'s in-memory ring, never from the store, so this change has **zero scoring impact** — the same argument the write-guard fix carried, and it holds for the same reason.

### 4.3 Part B — fire a pass on WS reconnect (conditional on G-1)

**Only if G-1 returns DISCONNECTED.** In `DeribitWsFeed.RunLoopAsync`, after a cycle that establishes a connection following a prior failure, request one repair pass. Separate session, separate review: it touches the live feed supervisor, and `RunLoopAsync` already carries a storm guard whose interaction with an extra REST burst needs its own thought.

⚠ **Part B must not fire on the FIRST connection of a process** — `TradeStoreGapRepair.Start()` already fires once on start with `dueTime 0`. Two passes racing on one file is exactly what the `_running` interlock exists to prevent, and it would log a skip rather than doing harm — but the correct behaviour is not to ask.

### 4.4 What this deliberately does NOT do

- **It does not backfill the pre-fix era.** Those 7,471 gap runs are past retention. They age out of the 20 h lookback on their own and must not be chased.
- **It does not change the schedule** (Part A). The measured hole was recoverable on the existing 6-hourly cadence.
- **It does not detect a hole that straddles a month boundary from the NEWER file.** The older month's file still has an old tail, so the existing trailing-window behaviour covers that span from the other side. **Named, not solved** — the residual is one outage landing within one flush of a month rollover.
- **It does not touch the write guard, `DedupTrades`, or the row format.** No schema change, no rotation.

---

## 5. Fixtures — family A56 (verified next free; A55g is high-water)

| # | Asserts | Must fail on current code? |
|---|---|---|
| **A56a** | A store with a `trade_seq`-bracketed hole in the middle ⇒ the hole window is returned | ⚠ **YES — this is the mutation proof** |
| **A56b** | A fully covered store ⇒ the trailing window only, and an already-covered window ⇒ empty list. **A48d's property must not regress** | No |
| **A56c** | An out-of-order store — a repair block appended after streaming rows — ⇒ **zero phantom holes**. ⚠ Trap 1 | ⚠ Likely yes |
| **A56d** | Legacy rows (`TradeSeq = AbsentSeq`) adjacent to identified rows ⇒ **zero phantom holes** at the era boundary. ⚠ Trap 2 | ⚠ Likely yes |
| **A56e** | A hole reaching back past `segStartMs` ⇒ clamped, never asks for a window the venue refuses. ⚠ Trap 3 | No |
| **A56f** | More holes than `MaxHolesPerPass` ⇒ capped at the production constant, largest kept. **Read `MaxHolesPerPass`; do not restate 32** | No |

⚠ **Prove the teeth by mutation.** Revert `ResolveRepairWindowsMs` to return only the tail window and confirm **A56a fails**. The trade-identity and write-guard builds both set this precedent, and it is the escalation trigger in §0.

⚠ **Fixture-literal provenance applies to every literal in this family** (hard rule, 2026-08-11). These fixtures assert **MECHANISM**, not shipped settings — sequence numbers and timestamps are constructed inputs, so literals are correct and each call site must say so in a comment. The one exception is `MaxHolesPerPass` in A56f, which asserts **SHIPPED BEHAVIOUR** and must be read from the constant.

---

## 6. Verification handles

⚠ **Each of these tests the property, not a string that mentions it** (hard rule, 2026-08-11 — a headline handle that counted a name printed 2 when the correct answer was 0, both hits inside comments).

| # | Handle | Expected |
|---|---|---|
| **V1** | Harness run | **0 FAIL, ALL PASS**, A56a–f present |
| **V2** | Mutation: tail-only return ⇒ re-run harness | ⚠ **A56a FAILS.** If it passes, stop |
| **V3** | `Select-String settings.json -Pattern '"version"' -TotalCount 1` | **unchanged** — no key moved |
| **V4** | `Select-String tools/BacktestRunner/HistoricalStore.vb -Pattern 'repairHoles'` on the historical-backfill call path | **no hit** — that path is untouched |
| **V5** | Release rebuild of all six projects | **0 warnings, 0 errors** |
| **V6** | Post-deploy, next copy-back: `trade_seq` completeness across a span containing a known outage | **~100 %**, hole filled |

⚠ **The falsifiable prediction, written before the build** — the convention that made the write-guard verification a one-line verdict:

> **After Part A ships and one outage occurs, the tape hole for that outage is filled within `gap_repair_interval_hours` (6 h) of the venue returning, provided the outage is inside the 20 h lookback. If a hole survives a full pass cycle, Part A did not find the whole problem.**

---

## 7. D-table — ✅ TICKED IN FULL, 2026-08-12

**All six ticked as recommended.** Recorded here as the decision text; where any summary disagrees with this table, this table wins.

| # | Decision | Tick | Note |
|---|---|---|---|
| **D-1** | Hole-derived resume cursor as the primary fix | ✅ **TICKED — yes** | The only candidate that covers a silent stall and a flush failure, and the only one that provably would have recovered the measured hole |
| **D-2** | Detect holes on **`trade_seq` only**, never on a time gap | ✅ **TICKED — seq only** | A time gap is a tolerance. ASIA at 03:00 is legitimately sparse. ⚠ **§0's escalation trigger stands**: if a time threshold becomes necessary, stop and re-take this row |
| **D-3** | Also fire a pass on WS reconnect (Part B) | ✅ **TICKED — yes, own session** | ⚠ **But §2.4's stop-and-ask now applies.** G-1 came back "venue-wide outage", which demotes Part B to a latency improvement over Part A's retry loop. **Ship Part A, watch one outage self-heal, then re-put this question.** The tick is not withdrawn; the sequencing is fixed |
| **D-4** | `MinHoleMs` = 2,000 and `MaxHolesPerPass` = 32 as `Public Const`, not settings keys | ✅ **TICKED — constants** | No failure-rate linkage; a key costs a version bump and a boundary question for nothing. **Fixtures read the constants, never restate the numbers** |
| **D-5** | `repairHoles` as an opt-in parameter so the historical backfill is unchanged | ✅ **TICKED — opt-in** | Mirrors `clampToSegStart`; keeps the blast radius at one caller |
| **D-6** | Do nothing about the pre-fix era's 7,471 gap runs | ✅ **TICKED — nothing** | Past retention. They age out of the lookback on their own |

---

## 8. What I did not verify

- ⚠ **G-1's residual — whether the WS SOCKET actually dropped.** §2.2. The trader's report establishes a **venue-wide** outage with REST down and the app alive; it does **not** observe the socket state. **I am inferring DISCONNECTED from the venue's 503s, and labelling it an inference.** The observable proof is `ReconnectCount` or the `connecting to …` lines in the collector console log. **Part A does not depend on it. Part B does, and §2.4 defers Part B for a different reason anyway.**
- **Anything on the AWS box directly.** Every AWS fact here is from the 2026-08-12 copy-back or from [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md).
- **That Deribit's `get_last_trades_by_instrument_and_time` returns complete results for a mid-history window.** Gap repair already depends on this and has done since v64, so the change adds no new dependency — but it has never been tested against a window that is *not* at the tail, which is precisely what this proposal starts asking for. **The first post-deploy hole-fill is the test.**
- **Whether Deribit ever resets `trade_seq`.** Still unverified project-wide. ⚠ It matters slightly more here than it did for the write guard: a reset mid-window would present as one enormous hole. `MaxHolesPerPass` and the per-hole clamp bound the damage to one refused or empty fetch, but the behaviour is not designed for.
- **The read cost of the scan on a full month file.** It is the same single streaming pass `LastTradeTimestamp` already makes on every pass, plus a sort of the in-window rows — I reasoned it, I did not measure it.
- **Concurrency.** No live run was performed.
