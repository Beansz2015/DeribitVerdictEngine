# Spec-back — trade-store write guard keyed on identity

**For:** the reviewing seat, before deploy.
**Spec reported against:** [`trade-store-write-guard-identity-proposal.md`](trade-store-write-guard-identity-proposal.md).
**Commits:** `1cec1ea` (the build) · `161aaa7` (a comment correction found while writing this packet — see §3.1).

> **Recommended review tier: Opus, effort high.** Not for the diff, which is small. Three of the four items in §3 are cases where a claim that had been true for months turned out to be false on checking, and two of them are in *this* build's own text. Judging those needs the tree open, not the docs.

**⚠ ONE DOCUMENT, DEVIATING FROM [`batch-review-packet-convention.md`](batch-review-packet-convention.md) ON PURPOSE.** The convention wants two — an outcome record plus this packet. **The record already exists as §9 of the proposal doc**, written there rather than in a separate file because that is where a future reader looks for what a spec became. Per the convention's own *"cross-reference; never duplicate"*, nothing from §9 is repeated here. **If you want the raw tables, gate tails and per-fixture mutation output, read `trade-store-write-guard-identity-proposal.md` §9 first — this packet assumes it.**

**State:** built, committed, **NOT pushed, NOT deployed.** AWS is discarding ~half the tape until it gets the binary, and that tape is unrecoverable past ~24 h. That clock is running while this is reviewed.

---

## 1. Ranked verification handles

Ordered by how much of the build each one covers. All are one command or one grep; none re-runs the build.

### ⭐ If you only run one

```bash
grep -c "_lastTs" Core/TradeStoreWriter.vb
```

**Must print `0`.** This is the §0 trap-1 check and it is the most likely way this build could have gone wrong, because keeping `_lastTs` "as a cheap pre-filter" reads as an optimisation and **reinstates the defect exactly** — same-millisecond siblings fail the pre-filter and never reach the new check. A non-zero count means the fix is cosmetic. Nothing else in this packet matters if this is not `0`.

### H2 — the relation is reused, not re-implemented (covers §3.2 and trap 2)

```bash
grep -n "HasIdentity\|LegacyRowKey" Core/TradeStoreWriter.vb | grep -v "^.*'"
```

**The load-bearing property is not that both names appear — it is the branch order.** `AlreadyCommitted` must test `t.HasIdentity` **first** and, for an identified row, return on `_windowIds` **alone**. Consulting the legacy set as well would reject a genuine sibling, and A55a catches that directly (its two real rows share all five legacy fields, so `accepted` would read 1).

⚠ **The case no A55 fixture covers is the reverse ordering**: an identity-less row arrives first, then an identified row with the same five legacy fields. `DedupTrades` keeps the identified row (identity-first); an `AlreadyCommitted` that also checked the legacy set would **drop** it — a real loss, not a duplicate. A55c presents the identified rows first, so it does not reach this. **Worth one read of the four-line function, not a new fixture** — and note it is the mirror image of Q1, where the same ordering produces a harmless duplicate rather than a drop.

### H3 — the fixtures have teeth, and the load-bearing value is `accepted`, not `PASS`

The mutation table is in the proposal doc §9.2. **Read the `accepted=` numbers, not the PASS/FAIL column.** A fixture asserting only "no exception thrown" or only "the file exists" would show PASS on both codebases. The three numbers that cannot be faked:

| Fixture | Fixed | Shipped `<=` | Why this number and not another |
|---|---:|---:|---|
| **A55d** | `accepted=10` | `accepted=1` | 10 → 1 is a **10×** separation on a single call. The largest signal in the build |
| **A55a** | `accepted=2` | `accepted=1` | On **real** Deribit rows, so it cannot be an artefact of synthetic construction |
| **A55f** | `onDisk=4` | `onDisk=2` | The restart path — the only one that exercises D-4's file-tail seed |

### H4 — ⚠ A55e's pairing, which is the one place a reviewer should be suspicious

A55e asserts `readmitted=True`. **On its own that is satisfied by a guard that has stopped guarding entirely.** The assertion that makes it meaningful is the second half in the same fixture, `insideStillRejected=True`. **Check that both are in the `Check(...)` condition, not just in the message string.** Same shape of trap as the A48f/F1 lesson this file already carries: a fixture that asserts a copy of the decision rather than the decision.

### H5 — the arithmetic identity for the window bound

In A55e, after buffering `1 + RecentWindowCapacity` trades:

> **`RecentWindowCount` must equal `RecentWindowCapacity` exactly — not `+1`, not `-1`.**

That single equality proves eviction fires exactly once per insert past the cap. An off-by-one in `Remember`'s `Do While` would show here and nowhere else. **A55e reads the production constant rather than restating `20000`**, so the identity survives a change to the constant.

### H6 — the six-project link trap

```bash
for p in DeribitVerdictEngine.sln tools/AutoTweaker/AutoTweaker.vbproj tools/WhatIfRunner/WhatIfRunner.vbproj tools/CeilingAudit/CeilingAudit.vbproj tools/BacktestRunner/BacktestRunner.vbproj verify/ordercheck/OrderCheck.vbproj; do dotnet build "$p" -c Release 2>&1 | grep -cE "error"; done
```

**Six zeros.** `Core/TradeStoreWriter.vb` is **not** linked by AutoTweaker / WhatIfRunner / CeilingAudit, so *"the writer links everywhere"* is false and only a Release build of all six catches it. This build added no cross-project surface, so it is a low-risk check — but it is the check the identity build needed and did not have.

### H7 — the display-parity claim, verifiable by absence

```bash
grep -rn "TradeStore\|trade_store" UI/MainForm_PlaintextSnapshot.vb UI/MainForm_Render_Cards.vb
```

Expect matches **only** in the TAPE STORE status element (Part B, a live status element and an established parity exemption), and **none** on any trade-row or store-content line. The gate agrees independently (`no snapshot/card drift detected`).

---

## 2. Decisions queued, with my read where I have one

None of these blocks the deploy. **Q1 and Q2 share a root** — both are places where the streaming guard is deliberately looser than `DedupTrades` — and are cheaper to rule together.

### Q1 — the order-dependence divergence from `DedupTrades`. Accept, or make the write path settle per batch?

`DedupTrades` settles identified rows first, which is what makes its result order-independent. A streaming guard sees arrival order only. So if an identity-less row arrives **before** an identified row sharing its five legacy fields, both are written where `DedupTrades` keeps one.

- **(a)** accept it — a duplicate on disk, removed by the read path.
- **(b)** buffer the notification batch and settle it with `DedupTrades` before admitting.

**My read, and it is a hypothesis: (a).** §3.1's asymmetry says the guard may only ever err toward writing, and this errs toward writing. (b) also **cannot** fully fix it — the two rows can arrive in different batches, so (b) buys partial order-independence for a real cost in the one path that must never throw. **Scoping, without recommending it:** (b) touches `Buffer` and `Flush` only, adds no new file I/O, and would need one new fixture; it does not touch `DedupTrades`, the reader, or any settings key.

### Q2 — window seeding at a month boundary

`EnsureSeeded` reads only the month of the *first* trade, matching the shipped behaviour. A restart in the first moments of a new month therefore seeds from an empty file and may re-admit a few rows from the previous month's tail.

- **(a)** leave it (commented at `EnsureSeeded`).
- **(b)** also read the previous month when the current file yields fewer than the cap.

**My read: (a).** The failure mode is duplicates, in the safe direction, in a window of at most a few seconds, at most once a month. **Scoping:** (b) is ~4 lines inside `EnsureSeeded` and one extra `ReadTradeFileTail` call per writer construction — cheap, but it doubles the seed's file I/O every time to fix something that happens monthly.

### Q3 — ⚠ a flush failure drops trades the window has already remembered

`Flush` copies the batch and clears `_pending` **before** `AppendRows` runs. On a disk failure `AppendRows` returns 0 and the batch is gone — while the window remembers those trades as committed, so a re-delivery is rejected. **This is unchanged from the shipped guard** (`_lastTs` advanced identically) and §3.4 forbids moving the advance to `Flush`. I am raising it because **A55g now pins that both same-millisecond siblings are accepted with no disk underneath**, which makes the loss in that scenario slightly *larger* than before — 2 rows instead of 1.

**My read: leave it, and I hold this one loosely.** §3.4's reason for advancing on `Buffer` is sound and specific. But "never throws" plus "advance on Buffer" together mean a transient disk error is silent permanent loss, and the §3.1 asymmetry argues against exactly that. **I have no read on whether it is worth a workstream** — that depends on how often AWS's disk actually fails, which I have not measured and cannot from here.

### Q4 — retire A48b now that A55b exists?

**My read: keep it.** A55b uses identified trades throughout; A48b's batch is entirely **identity-less**, so it is the only fixture exercising the legacy-fallback arm across a restart-from-disk. Its comment and label are corrected in `1cec1ea` to stop it claiming a guard shape that no longer exists. **Its real value now is documentary** — it is the fixture that missed the defect for ten days, and the comment says so at the call site.

### Q5 — is `RecentWindowCapacity = 20000` sized against the right thing?

D-3 sized it as *"≈4 h of tape"*. **See §3.1: the replay window it must actually cover is a WS re-subscribe, not the 500-trade REST seed the spec named.** 20,000 is comfortably larger than either.

**My read: no change.** The ruling stands and the number is generous. **I flag it only because the ruling's stated reason was wrong**, and a future seat trimming the constant on that reasoning would be trimming against the wrong quantity.

---

## 3. Spec-back proper — feedback on the spec itself

### 3.1 ⚠ THE ASSUMPTION THAT BROKE — and it was in the spec, in v64's code, and in my own first draft

`trade-store-write-guard-identity-proposal.md` §2 and §3.4 both justify the guard with *"`SeedAsync` re-seeds on every reconnect… so duplicates genuinely arrive"*, and `DeribitWsFeed.vb:294` went further: *"idempotent against the REST re-seed window below"*.

**All of it is wrong, and it had been wrong since v64.** Verified in the tree:

| Claim | Reality |
|---|---|
| `SeedAsync`'s REST trades reach the store | ⛔ They go to `_state.SeedTrades` — the **in-memory ring**. `MarketState.SeedTrades` holds no store reference |
| The guard covers a 500-trade REST re-seed | ⛔ `store.Buffer` has **exactly one caller**, `DeribitWsFeed.ApplyTrades`. The duplicates are **WS re-subscribe replays** and a fresh writer over an existing file |
| Gap repair's writes pass the guard | ⛔ `HistoricalStore.vb:269` calls `TradeStoreWriter.AppendRows` directly, so repair **bypasses the guard entirely**; the read path dedups those |

**What it costs: nothing in correctness, and that is exactly why it survived.** The guard is still needed, and A48b/A55b test a replay through `Buffer`, which is what a WS replay is. What it cost was **a wrong reason in the file for ten months**, which is the input a future seat would use to size or remove the window (Q5).

⚠ **The part worth the reviewing seat's attention is not the error, it is how it was found.** I copied the SeedAsync justification into three new comments and only checked it while writing this packet — after the build was committed and the gate had passed. **Nothing in the acceptance criteria could have caught it**: the comment is not a display string, the harness does not read comments, and the behaviour is correct. Corrected in `161aaa7`.

### 3.2 What the spec got right, specifically — and one sentence did most of the work

> *"A fixture that does not put two distinct trades on the same millisecond does not test this."*

**That sentence is why this build has teeth.** A55b, A55f and A55g were each drafted as ordinary regression guards, and **each one passed on the shipped `<=` code**. Without that sentence I would have shipped three fixtures that assert a property while proving nothing about the fix, which is precisely A48b's failure repeated inside its own repair. Each was given a sibling pair until it failed.

Also load-bearing, and worth reusing verbatim in future specs:

- **§0's escalation trigger stated as an observable** — *"if the new fixture cannot be made to FAIL against the current code, STOP"* — is falsifiable, unlike "test it thoroughly".
- **§3.1's asymmetry given before the mechanism.** Having "when uncertain, ADMIT" settled up front made Q1, Q2 and the eviction-refcount choice fall out in one direction each, with no re-litigating.
- **§1a's caveat 2** — noting the maintenance window biases the drop rate **down**, so 49.2 % is a floor. A spec that argues against its own headline number is rare and it is the reason I trusted the rest of §1a.

### 3.3 Where the spec was narrower than its own words

- **§5 lists A55b/f/g as "must not regress" properties.** Read literally, that describes fixtures which pass on the old code — the exact thing §5's own mutation paragraph forbids two lines earlier. **The two requirements are in tension and the spec does not resolve it.** I resolved it toward mutation. A future spec should say "every fixture, including regression guards, must fail on the unfixed code" rather than leaving the reader to notice.
- **§6 requires "mutation results stated"** but does not say *where*, so a reader could satisfy it in a commit message that is then unfindable. Recorded in the proposal doc §9.2 instead, next to the spec it tests.

### 3.4 A constraint pair that nearly conflicted, and the hatch

**D-3 says the window is a CONSTANT, not a settings key. The F1 lesson in the harness says fixtures must test the production decision, not a restatement of it.** Together those looked like a deadlock: A55e needs the window boundary, and a `Private Const` cannot be read from the fixture — which pushes toward hardcoding `20000` in the fixture, i.e. the restatement F1 forbids.

**The hatch: `Public Const`.** It satisfies D-3 (still not a settings key, still no version bump, still untunable at runtime) while letting the fixture read the production number. **Name this in future specs that rule a value into a constant** — otherwise the next implementer hardcodes it, and the fixture rots silently the first time the constant moves. This is the same failure the new fixture-literal provenance hard rule was written for.

### 3.5 Two of the spec's own numbers were wrong, both harmlessly

Detailed in the proposal doc §9.4: D-6's expected version-bump WARN fires one step later than implied (the gate reads **committed** paths), and D-3's cost figures understate both coverage and memory. Neither changes a ruling. **Both are the kind of number a later seat would quote as measured.**

---

## 4. What I did not verify, and cannot

Stated so nothing is assumed covered.

| Item | Why not |
|---|---|
| ⭐ **That the fix actually raises store completeness on AWS** | Needs the deploy. §9.5 records the falsifiable prediction *before* the fact: because G2 showed the feed loses nothing, completeness should go to **~100 %**, not to a partial improvement. **If a post-fix `trade_seq` check still shows gaps, this build did not find the whole problem.** ⚠ This is the one item that could still overturn the build |
| **The §1a gate figures** | Carried from the proposal doc §1a. I did not re-derive them from `ws_trade_probe_20260811-144742.csv`, and I did not re-run the probe. The 49.2 % / G1 / G2 numbers are inherited, not confirmed by me |
| **Live behaviour of any kind** | No live run. The app was never started: `bin\Debug` is stale by design and starting it appends collector rows into the real dataset under a fresh `InstanceId` (the v57-stomp precedent). Everything here is harness + tree evidence |
| **Concurrency** | The window lives under the existing `SyncLock _pending`, same lock and same scope as `_lastTs` did, so no new lock ordering exists. **But I did not test concurrent `Buffer`/`Flush`/`ResetBufferState`**, and the gap-repair thread writing the same file through `AppendRows` is unchanged and untested here |
| **Memory under a full window** | The "few MB" figure in §9.4 is calculated from string sizes, **not measured**. No allocation profiling. It is small either way, but do not quote it as measured |
| **That `ReadTradeFileTail` is fast enough on AWS's real file** | Tested only against fixture-sized files. It is one streaming pass with a bounded ring — the same single pass `LastTradeTimestamp` already made on the same path — so it should cost no more than the guard it replaced. **That is reasoning, not a measurement on a 200k-row file** |
| **The A48/A53 fixtures beyond the four I read** | I read A48a/b/d/e and A53a/b/c/e/f/h. The rest are covered only by "ALL PASS" |
| **F2** (`ResetBufferState` race, `DeribitWsFeed.vb:298`) | Out of scope per §7. Untouched and unexamined |
| **Whether Deribit ever resets `trade_seq`** | Still unverified — the same gap the identity build recorded. D-2's ratification means nothing in this build depends on it |

---

# ✅ REVIEW — orchestrator seat, 2026-08-11. **ACCEPTED. Deploy authorised.**

**Verified in the tree, not read off this packet.** Harness re-run: **272 PASS / 0 FAIL** (265 → 272 = A55a–g).

## R1. ⚠ The packet's own headline check is WRONG — and it is the session's defect class again

**H1 says `grep -c "_lastTs" Core/TradeStoreWriter.vb` "must print `0`". It prints `2`.**

**The build is correct and the handle is broken.** Both hits are inside comments *explaining that `_lastTs` is gone* (`:76`, `:81`); the field is **not declared** and **no executable line references it**. A reviewer following H1 literally would read `2`, conclude trap 1 had fired, and reject a sound build.

⚠ **Two things make this worth more than a typo correction.** First, **H2 applies a comment filter (`grep -v "^.*'"`) and H1 does not** — the hazard was known and missed on the one check billed as *"if you only run one."* Second, and this is the point: **H1 counts a STRING as a proxy for "is the field gone."** That is a copy of the property, not the property, and it drifts from it the moment a comment mentions the name. **It is the same shape as everything else this cycle** — copy 4, the `<=` guard, the POCO defaults.

**The correct handles, either of which is exact:**

```bash
grep -c "Private _lastTs" Core/TradeStoreWriter.vb          # 0 — the field is gone
grep -n "_lastTs" Core/TradeStoreWriter.vb | grep -vE ":\s*'"   # empty — no executable reference
```

## R2. H2's flagged gap — read, and it resolves clean

The packet asks for one read of `AlreadyCommitted` rather than a new fixture. Done:

```vb
If t.HasIdentity Then Return _windowIds.ContainsKey(t.TradeId)
Return _windowLegacy.ContainsKey(LegacyRowKey(t))
```

**The identified branch returns on `_windowIds` alone and never consults the legacy set**, so the reverse ordering (identity-less first, then an identified row sharing its five legacy fields) yields a **duplicate, not a drop** — the safe direction. **The concern was correctly raised and correctly scoped to a read.** No fixture owed.

## R3. §3.1 is right, and the error is mine

All three sub-claims verified: `SeedAsync` hands its 500 REST trades to `_state.SeedTrades` (the in-memory ring, no store reference) · `store.Buffer` has **exactly one caller**, `DeribitWsFeed.vb:515` · gap repair calls `TradeStoreWriter.AppendRows` directly at `HistoricalStore.vb:269`, bypassing the guard.

**I wrote that justification into the spec, inheriting v64's comment without checking it.** The correction is right, the assessment of its cost is right — a wrong *reason*, not wrong behaviour — and **finding it while writing the packet, after the gate had passed, is the packet doing its job.**

## R4. Rulings on the queued decisions

| # | Ruling | Note |
|---|---|---|
| **Q1** | ✅ **(a) accept** | Agreed. **And a reason the packet did not give, which settles it harder:** (b) would introduce a **second dedup contract in the write path** — the copy-class this entire cycle exists to remove. Do not build a second relation to make one path order-independent |
| **Q2** | ✅ **(a) leave** | Agreed. Doubling seed I/O on every writer construction to fix a few-second window once a month is a bad trade |
| **Q3** | ✅ **Leave — and it is safer than the packet argues** | A **persistent** disk failure is **not** silent: `AppendRows` returns 0 forever, `LastFlushUtc` never advances, and `ClassifyTapeStoreTier` escalates the TAPE STORE strip **UNKNOWN → AMBER → RED**. So the exposure is bounded to **one batch per TRANSIENT failure**, not to unbounded silent loss. That is acceptable, and it is a better reason than "unchanged from before". **No workstream** |
| **Q4** | ✅ **Keep A48b** | Agreed, and the documentary argument is the right one: it is the fixture that missed the defect for ten days, and the call site now says so |
| **Q5** | ✅ **No change to 20000** | Agreed. ⚠ **Flagging it was correct precisely because of the new fixture-literal provenance hard rule** — a constant whose *stated reason* is wrong is exactly what a future seat trims against |

## R5. Two spec lessons, both mine, both recorded

- **§3.3 is a real internal contradiction in my spec.** §5 listed A55b/f/g as *"must not regress"* properties two lines after §5's own mutation paragraph forbids fixtures that pass on unfixed code. **The implementer resolved it toward mutation, which is correct**, and all seven fixtures were proven by mutation. Future specs: *"every fixture, including regression guards, must fail on the unfixed code."*
- **§3.4's `Public Const` hatch is genuinely reusable and is now a standing rule** — see `CLAUDE.md`. Verified applied: `TradeStoreWriter.RecentWindowCapacity` is `Public Const` at `:118`, and A55e reads it rather than restating `20000`.

## R6. Verified independently

| Check | Result |
|---|---|
| `_lastTs` field declared | **No** — gone |
| `AlreadyCommitted` branch order | ✅ identity-first, `_windowIds` alone |
| A55e's pairing inside the `Check` condition, not the message | ✅ all four conjuncts in the condition |
| A55e reads the production constant | ✅ `TradeStoreWriter.RecentWindowCapacity` |
| Harness | ✅ **272 PASS / 0 FAIL** |
| §9 outcome record present | ✅ §9.1–9.5 — the one-document deviation is legitimate |

**Not re-verified by me:** the §9.2 mutation table (I read it, did not re-run each mutation), the §1a gate figures (I derived those originally), and everything in §4.

## R7. ⚠ Deploy — the clock the packet names is real

**Ship it.** Trader tests → push → `dotnet build -c Release` from the pushed tree → deploy per `aws-collector-deploy-checklist.md` §1.2's six-item allowlist → record the new InstanceId in §5a.

⚠ **`settings.json` does not move, so this is NOT a scoring boundary** — but it IS a tape boundary: rows before the new id are ~50 % complete and rows after should be ~100 %.

⚠ **Take the D-7 / §5b measurement from a copy-back BEFORE too much post-fix tape accumulates** — not because the fix destroys it (it does not; that was corrected), but because the outage band is easiest to locate while it is near the tail.

**The falsifiable prediction stands and must be checked after deploy:** G2 showed the feed loses nothing, so a post-fix `trade_seq` check should read **~100 % complete**. **If gaps remain, this build did not find the whole problem.**
