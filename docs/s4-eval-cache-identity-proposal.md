# `S-4` — eval-cache backfill: key on identity, and fix the loop

✅✅ **RULED 2026-09-08 (UTC, trader) — `D-1` (b) · `D-2` (b) · `D-3` (a). THIS IS NOW A BUILD-AUTHORIZED SPEC.**

⛔ **BUILD FROM §4b. It is the build list.** §4 below is the record of how it was reached and is **not** the instruction — same convention as [`s2-2-calcspread-split-proposal.md`](s2-2-calcspread-split-proposal.md) §4b/§4.

⛔⛔ **WRITING THIS FALSIFIED THE QUEUE ROW'S SIZING.** [`trader-tick-queue.md`](trader-tick-queue.md) §2 sizes `S-4` as **"two small changes, one seam"** and adds *"needs no schema change"*. **One half is exactly that. The other half is a persisted-schema change to `analysis_eval_cache.csv` and cannot be done without one** — see §2.3. Same shape as `S2-2`.

⚠⚠ **THE RULING OVERTURNED THE ORCHESTRATOR'S READ ON TWO OF THREE ROWS, AND THE REASONING IS WORTH MORE THAN THE ANSWER — see §3.1.** The short form: **a latent defect whose fix needs a schema change gets HARDER with delay, not easier**, because every day writes more identity-less rows that `D-3` (b) cannot honestly retrofit. **And "defer until something else forces a migration" is a bet this repo has already lost — the five riders have been parked since ~2026-08-20 waiting for a rotation the standing rule says never to force.**

**Baseline commit: `a6cfb8d`.** Every line number here was read at that commit. ⛔ **Re-read before editing if `HEAD` has moved.**

**Source:** [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md) `S-4`. **Dates to settings v26.**

---

## 0. Model and effort

> ### Model: **Sonnet**
> ### Effort: **HIGH**
> ### ONE session, and treat it as its own. Do not bundle other work into it.

**Why HIGH, now that `D-2` is ruled (b).** This is no longer a one-line fix. It adds fields to **two** types, threads them through `ParseAnalysisLog`, and performs a **v6 → v7 migration on `analysis_eval_cache.csv`** — the file that holds the perf strip's entire history. The migration machinery is well-trodden here (six prior migrations live in this class), **but a migration that mis-handles legacy rows silently corrupts history that cannot be rebuilt.** That is the "expensive and hard to notice" tier by definition.

⛔ **Do NOT inherit "Sonnet, medium" from the queue row.** That figure was attached to the sizing this document falsified, and the queue row now says so.

⚠ **This build is NOT split into sessions, deliberately.** `D-1` was ruled **(b)** — the loop fix and the key fix land **together**. Splitting them would mean writing a line, a fixture and a mutation proof against the timestamp key, then rewriting all three. See §3.1.

### 0.1 Where the implementer will slip

⚠ **Trap 1 — "just add the row to `existingTs` inside the loop" fixes half the defect and reads like the whole fix.** The queue row already warns of this, and it is right: that closes the cold/warm bias flip and leaves the key a bare `DateTime`. **Both halves, or a stated reason for only one.**

⛔ **Trap 2 — `SignalId` ALONE IS NOT AN IDENTITY. It is a per-`InstanceId` counter that restarts at 1.** Measured on `AWS-copybacks/aws-copyback-2026-09-06/analysis_log_aws.csv`: `SignalId` runs **min=1, max=4612 over 4,602 rows**, against exactly **one** distinct `InstanceId`. It is unique in that file **by accident of there being one process run.** Across a restart — or in a pooled book — `SignalId` collides. **The identity is the PAIR.**

⚠ **Trap 3 — legacy eval-cache rows have no identity, and it cannot be honestly retrofitted.** You could re-derive it by matching a legacy row back to its `analysis_log.csv` row on `Timestamp` — but that is matching on the very key the change exists to distrust. **Circular. `D-3` rules this explicitly.**

⚠ **Trap 4 — the file has SIX prior schema migrations and they are order-sensitive.** `IsPreV3Schema` / `IsPreV4Schema` / `IsPreV6Schema` are all captured into locals **before** any rewrite, because a rewrite re-stamps the header comment and would make the later probes read false. A v7 probe must be captured in the same place, the same way.

### 0.2 Escalation trigger

⛔ **If `D-2` (b) is ruled and the v6 → v7 migration needs to touch, re-evaluate, or re-order any EXISTING row's outcome fields — stop.** This change is about a dedup key, not about re-judging history. A migration that only *adds two nullable columns* is right; one that re-walks outcomes has become a different build.

---

## 1. The defect, in one line

**The backfill dedups `analysis_log.csv` rows against the eval cache using a bare `DateTime`, and the set it tests against is never updated inside the loop — so the same batch behaves differently depending on whether the cache was warm.**

---

## 2. Measured state at `a6cfb8d` — not inherited

### 2.1 The two sites

⚠ **The queue row cited `LivePerformanceTracker.vb:431` and `:358`. Both had ROTTED** — the v5→v6 no-data sweep grew the file. Corrected in the queue at `a6cfb8d`. The real sites:

| Site | Code | Role |
|---|---|---|
| **`LivePerformanceTracker.vb:372`** | `Dim existingTs As New HashSet(Of DateTime)(_evalCache.Select(Function(e) e.Timestamp))` | Built **once**, from the loaded cache, **before** the loop |
| **`LivePerformanceTracker.vb:445`** | `If existingTs.Contains(row.Timestamp) Then Continue For` | Tested **inside** the loop — and **nothing is ever added to the set inside it** |

The loop appends to a separate `newEntries` list (`:464`), which is flushed to `_evalCache` and the file **after** the loop (`:466-470`). **`existingTs` therefore never learns about rows the loop itself just created.**

⛔ **Re-grep `existingTs` rather than trusting these numbers.** It returns exactly the two sites, and it will keep working after the file moves again.

### 2.2 Why the bias flips with cache state

Take two `analysis_log.csv` rows sharing one timestamp:

| Cache state | What happens | Result |
|---|---|---|
| **Cold** (neither row in `_evalCache`) | Row 1 misses the set → appended. Row 2 tests the **same unchanged set** → also misses → appended | **BOTH admitted** |
| **Warm** (row 1 already in `_evalCache` from an earlier run) | `existingTs` contains the timestamp → row 2 skipped | **ONE admitted** |

**Same input, different output, decided by whether the process restarted.** That is the defect, independent of whether the key is a good one.

### 2.3 ⛔ Why the second half CANNOT be "no schema change"

**`EvalCacheEntry` carries no identity.** Its full field list, read at `LivePerformanceTracker.vb:29-44`: `Timestamp` · `Verdict` · `EntryPrice` · `FavBar` · `AdvBar` · `EvalOutcome` · `TargetEverHit` · `ExecResolution`. **No `InstanceId`, no `SignalId`.**

**`LivePerformanceTracker.vb` references neither name anywhere** — verified by grep, zero hits. So `LogRow` does not carry them either.

✅ **The source does have them:** `analysis_log.csv` carries **`InstanceId` at column 110 and `SignalId` at column 111** (verified against the copy-back header).

**So keying on identity requires all of:** `LogRow` gains both fields · `ParseAnalysisLog` reads them · `EvalCacheEntry` gains both · `LoadEvalCache` / `AppendEvalRows` / `WriteEvalCache` handle two new columns · **a v6 → v7 schema migration** · and a rule for legacy rows.

⭐ **The apparent contradiction with the queue row resolves cleanly, and it must be stated so nobody re-raises it.** The row's *"Needs no schema change — do NOT bundle it with a CSV rotation"* is about **`analysis_log.csv`**, whose header rotation carries the five standing riders and must never be forced. **`analysis_eval_cache.csv` is a DIFFERENT FILE with its own migration machinery already in this class — v1→v2, v2→v3, v3→v4, pre-v5 rotate, v5→v6 all live at `LivePerformanceTracker.vb:354-438`.** A v7 bump there touches no rider and forces no `analysis_log.csv` rotation.

### 2.4 How exposed are we, really

| Measurement | Result |
|---|---|
| Duplicate timestamps in `analysis_log_aws.csv` (4,602 rows, 2026-09-06 copy-back) | **ZERO** |
| Same check, 2026-08-11 (10,779 local + 15,499 AWS rows) | **ZERO** — carried from the queue row, not re-run by me |
| `SignalId` distinct / total | **4,602 / 4,602** — fully populated, no blanks |
| `InstanceId` distinct in that file | **1** |

⚠ **The defect is LATENT.** At 60 s / 180 s cadences two rows sharing a whole second is barely reachable. **That is an argument about priority, not about correctness** — and it is the main input to `D-2`.

---

## 3. ✅ THE D-TABLE — RULED IN FULL 2026-09-08 (UTC). Do not re-open.

| # | Decision | ✅ RULING | Options, and the reads that were defeated |
|---|---|---|---|
| **`D-1`** | Build the loop fix (half A) independently of the key fix (half B)? | ✅ **(b) — THEY LAND TOGETHER.** ⛔ No "safe pair first", no separate session | **(a)** ship A now, decide B separately · **(b)** together. ⚠ **The orchestrator read (a) and was DEFEATED** — see §3.1. **The queue row's *"Both halves, or state why only one"* is satisfied by building both** |
| **`D-2`** | What is the dedup key? | ✅ **(b) — the true `(InstanceId, SignalId)` PAIR, with the v6 → v7 migration, NOW** | **(a)** keep `Timestamp`, fix only the loop · **(b)** the identity pair · **(c)** composite of persisted fields. ⚠ **The orchestrator read (a)-now-(b)-later and was DEFEATED** — §3.1. ⛔ **(c) STAYS REJECTED and must not be re-proposed: it is a HEURISTIC dressed as an identity.** Two genuinely distinct runs at the same second, same verdict, same resolution and same entry price still collide — and a key that is *nearly* unique invites the "held safe by an assumption" failure this project has hit repeatedly |
| **`D-3`** | What happens to legacy eval-cache rows that have no identity? | ✅ **(a) — null identity, FALL BACK TO `Timestamp` for those rows only** | **(a)** fall back · **(b)** re-derive by matching back to `analysis_log.csv` on `Timestamp` · **(c)** rotate and rebuild. ⭐ **The orchestrator's read, upheld.** ⛔ **(b) is CIRCULAR — it matches on the very key this change exists to distrust — and is rejected explicitly so it is not re-proposed as a "completeness" improvement.** (c) throws away the perf strip's history for a latent defect |

### 3.1 ⭐⭐ Why the ruling overturned the orchestrator, recorded because the reasoning generalises

⛔ **`D-2` — the cost of deferring ACCRETES; it is not flat.** Every day of delay writes more eval-cache rows **without** identity, and `D-3` (b) cannot honestly retrofit them. **So deferral permanently enlarges the legacy set the fix must special-case.** A defect that gets more expensive while you wait is a bad deferral candidate however latent it is.

⛔⛔ **`D-2` — "defer until something else forces a migration" is a BET, and this repo is the proof it loses.** The **five riders** have been parked since ~2026-08-20 waiting for an `analysis_log.csv` header rotation, and the standing rule is **never force one**. The orchestrator's read would have made half B a **sixth rider in spirit**.

⚠ **`D-2` — the specific error, named so it is not repeated:** §2.4 of this document states that latency *"is an argument about priority, not correctness"* — and the orchestrator then let the zero-duplicates measurement pick the weaker key anyway. **A zero measurement justifies scheduling calmly. It does not justify choosing the worse fix.**

⭐ **`D-1` follows FROM `D-2`, and that coupling was missed.** The orchestrator's (a) was coherent only while the key fix was deferred. Once the key changes, half A's own line changes shape — `existingTs.Add(ts)` becomes an identity-keyed add — so shipping A first means writing a line, a fixture and a mutation proof, then **rewriting all three**. Worse, **A alone makes the cold path match the warm path ON THE TIMESTAMP KEY**, cementing the wrong key inside `A69a`/`A69b`. ⚠ **And the urgency argument for (a) — "close a live bias now" — evaporates against zero measured duplicates: there is no live harm being closed.**

⚠⚠ **THE HONEST COUNTERWEIGHT, stated rather than buried: (b) IS the bigger and riskier build.** It migrates the file holding the perf strip's entire history; (a) carried no data-loss risk at all. **That is a real trade, not a free win.** It is a reason to pin it hard — `AC-8` and the §0.2 escalation trigger are exactly that — **not a reason to reverse.**

---

## 4. How the build list was reached — RECORD ONLY. ⛔ DO NOT BUILD FROM THIS SECTION; §4b is the instruction.

⚠ **This section was written while `D-2` was open, so it is framed as "half A / half B, B only if…". `D-2` was ruled (b) and `D-1` (b), so BOTH halves are in scope and they land TOGETHER.** The mechanics below are still correct; the conditionals are spent. **§4b is the list to work from.**

### 4.1 Half A — the loop fix. Applies under EVERY `D-2` outcome.

At `LivePerformanceTracker.vb:445`, after the entry is built and added to `newEntries` (`:464`), **record the key** so the rest of the batch sees it:

```vb
newEntries.Add(entry)
existingTs.Add(row.Timestamp)   ' [S-4] the set must learn about rows THIS loop created,
                                ' or a cold start admits two same-second rows while a warm
                                ' start drops one — the bias flips with cache state.
```

⚠ **Under `D-2` (b) this line adds the identity key instead**, but the structural point is the same: **the set is updated inside the loop.**

### 4.2 Half B — ✅ `D-2` WAS ruled (b), so this is IN SCOPE. (Conditional wording kept as written; §4b is the instruction.)

Sequenced by dependency:

1. `LogRow` gains `InstanceId As String` + `SignalId As Long`; `ParseAnalysisLog` (`:1448`) reads columns `InstanceId` / `SignalId` **by name from `colIdx`**, never by position — ⛔ column 110/111 is what today's file happens to be, not a contract.
2. `EvalCacheEntry` gains the same two, **nullable/absent-tolerant**.
3. Eval-cache **v6 → v7**: two new columns. ⛔ **Capture `IsPreV7Schema` into a local alongside the existing probes at `:362-368`, BEFORE any rewrite** — the existing ones are written that way for a reason and a v7 probe placed after a rewrite reads false.
4. `LoadEvalCache` / `AppendEvalRows` / `WriteEvalCache` handle the two columns, absent-tolerant for legacy rows.
5. The dedup key becomes the pair, with `D-3` (a)'s fallback for identity-less legacy rows.

---

## 4b. ⭐ THE BUILD LIST — build from this

**Seven steps, sequenced by dependency, all in ONE session. Build in this order and build after each step.**

| # | Step | Detail |
|---|---|---|
| **1** | `LogRow` gains identity | Add `InstanceId As String` and `SignalId As Long`. ⛔ **`ParseAnalysisLog` (`LivePerformanceTracker.vb:1448`) must read them BY NAME from `colIdx`, never by position** — columns 110/111 are what today's file happens to be, not a contract. ⚠ **Absent-tolerant:** a pre-identity `analysis_log.csv` has neither column; that must parse, not throw |
| **2** | `EvalCacheEntry` gains identity | Same two fields, nullable/absent-tolerant. ⚠ **Do NOT give them non-null defaults** — an absent identity must stay distinguishable from a real one, or `D-3` (a)'s fallback cannot tell which rows are legacy |
| **3** | v6 → v7 probe | Add `IsPreV7Schema`. ⛔⛔ **Capture it into a local ALONGSIDE the existing probes at `LivePerformanceTracker.vb:362-368`, BEFORE any rewrite.** The existing probes are written that way because a rewrite re-stamps the header comment and makes a later probe read false. **A v7 probe placed after a rewrite reads false and the migration silently never runs** |
| **4** | Reader / writer | `LoadEvalCache`, `AppendEvalRows`, `WriteEvalCache` handle two new columns, absent-tolerant for legacy rows |
| **5** | The migration itself | v6 → v7 **adds two nullable columns and NOTHING ELSE**. ⛔ **It must not touch, re-walk, re-judge or re-order any existing `EvalOutcome`, `TargetEverHit`, `FavBar` or `AdvBar`.** That is the §0.2 escalation boundary and `AC-8` pins it |
| **6** | The key swap | The dedup set becomes the identity pair, with `D-3` (a)'s `Timestamp` fallback for identity-less legacy rows. ⛔ **`SignalId` ALONE IS NOT THE KEY** — §0.1 Trap 2 |
| **7** | The loop fix | At `LivePerformanceTracker.vb:445`, **add the key to the set inside the loop**, next to `newEntries.Add(entry)` at `:464`. ⚠ **Written against the IDENTITY key from the start — never against `Timestamp` and then migrated.** That is what `D-1` (b) exists to avoid |

⛔ **Steps 1–6 are inert without step 7, and step 7 is the wrong fix without steps 1–6.** That is `D-1` (b) in one line.

### 4b.1 The one line that is the whole of half A

```vb
newEntries.Add(entry)
existingKeys.Add(KeyFor(row))   ' [S-4, D-1 (b)] the set must learn about rows THIS loop
                                ' created. Without it a cold start admits two colliding rows
                                ' while a warm start drops one — the bias flips with cache
                                ' state. Pinned by A69a/A69b.
```

⚠ **`KeyFor` is the identity pair with the `D-3` (a) `Timestamp` fallback — one helper, one place, so the load path and the loop path cannot drift.** The current code builds a raw `HashSet(Of DateTime)` at `:372`; that becomes a set of the composite key.

---

## 5. Fixtures — family `A69` (✅ verified free at `a6cfb8d`; `A68` was taken 2026-09-08)

⛔ **Every fixture must be mutation-proven, and the mutation must be applied WITH THE EDITOR.** `python` is **not installed on this box** — a `python -c` mutation silently does nothing and the harness then reports PASS against unmutated code. ⛔ **Assert `Build succeeded` before believing any harness result.** ⛔ **Restore by FILE COPY, never `git checkout -- <file>`** — that also reverts uncommitted work in the same file.

| Fixture | Asserts |
|---|---|
⛔ **ALL FOUR ARE IN SCOPE — `D-2` was ruled (b), so `A69c` and `A69d` are no longer conditional.**

| Fixture | Asserts |
|---|---|
| **`A69a`** | **The cold/warm asymmetry is gone.** Two `analysis_log` rows sharing one **identity key**, backfilled into an **empty** cache, must yield the **same count** as the warm path. ⛔ **If it passes before the fix, it is not testing the defect** |
| **`A69b`** | The set is updated **inside** the loop — a three-row batch with two colliding rows admits exactly one, not two |
| **`A69c`** | Two rows with the **SAME timestamp** but **DIFFERENT `(InstanceId, SignalId)`** are **BOTH admitted**. ⭐ **This is the fixture that proves the key actually changed** — under the old timestamp key it fails, and it is the only one that distinguishes `D-2` (b) from `D-2` (a) |
| **`A69d`** | A legacy row with **no identity** still dedups by `Timestamp` (`D-3` (a)), **and** a v6 file migrates to v7 with **every pre-existing outcome field byte-identical** — the §0.2 escalation boundary, pinned |

⛔⛔ **`A69c` MUST use two different `InstanceId` values, not two different `SignalId`s.** `SignalId` is a per-instance counter that restarts at 1 (§0.1 Trap 2), so two rows differing only in `SignalId` do **not** exercise the collision the pair exists to resolve — and a fixture built that way passes under both keys and proves nothing.

⚠ **`A69a` is the one most likely to be written so it cannot fail.** `AggregateRange`'s sibling `A68a` was inert until its range start was set to `DateTime.MinValue`; the analogous risk here is a fixture whose two rows do not actually share a timestamp, or whose cache is not genuinely cold. **State the input that makes it fail, then mutate.**

---

## 6. Acceptance criteria

Forward-looking — the implementer runs these and pastes actual output.

| # | Criterion |
|---|---|
| **AC-1** | Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck` all `Build succeeded`, 0/0 |
| **AC-2** | Harness `ALL PASS`. Count **339 → 343** (`A69a`–`A69d`). ⛔ **Measure by RUNNING it, never by grepping `Check(`** |
| **AC-3** | `A67a`, `A68a`, `A68b` and every `A14*`/`A33*` fixture that drives `AggregateRange` still pass **unchanged** |
| **AC-4** | `tools/checks/verify-gate.ps1 -Mode local-fast` → `GATE PASSED` |
| **AC-5** | `grep -n "existingTs" LivePerformanceTracker.vb` shows the set is **added to inside the loop** — paste the real output |
| **AC-6** | Every fixture mutation-proven, **with pasted failure output** |
| **AC-7** | `settings.json` **untouched** — verify with `git diff --stat`; it must not appear. `S-4` dates to v26 but needs **no key** |
| **AC-8** | ⛔ **THE LOAD-BEARING ONE. Take a real v6 `analysis_eval_cache.csv`, migrate it to v7, and prove every pre-existing `EvalOutcome`, `TargetEverHit`, `FavBar` and `AdvBar` is BYTE-IDENTICAL afterwards.** Capture before/after and diff — **paste the diff, or the hashes, not a claim that you checked.** This is the §0.2 escalation boundary made testable |
| **AC-9** | ⛔ **`A69c` must FAIL if the key is reverted to `Timestamp` alone.** Run that mutation and paste the failure. **It is the only fixture that distinguishes the ruled `D-2` (b) from the defeated (a)** — without this proof the build cannot show it did what was ruled |

### 6.1 Rules that apply to the commit

- ⚠ **Engine display-string parity.** `AggregateRange` feeds the perf strip. **The dedup key change CAN move a rendered count** — if two rows previously collapsed to one now both survive, `TotalRange` and the success rate move. ⛔ **Measure it: back-fill a real book before and after, and report whether any rendered value changed.** If one does, the card binding in `UI/MainForm_Render_Cards.vb` must be updated in the same commit; if none does, **state that as a measurement, not an assumption.**
- ⛔ **`docs/DeribitIndicatorProject.md` §15 — AN ENTRY IS OWED.** A v6 → v7 eval-cache migration is a persisted-schema change. ⚠ **Do NOT tag this commit `[no-engine-change]`** — that token was correct for the doc-only work around it and is wrong here.
- ⚠ **`settings.json` is still untouched** (`AC-7`). `S-4` dates to v26 but needs **no key** and **no version bump**.

---

## 7. What to report back

Two documents per [`batch-review-packet-convention.md`](batch-review-packet-convention.md): `docs/s4-batch-summary.md` and `docs/s4-spec-back.md`.

⛔ **Label reader-runnable handles `H-n` and build-time evidence `E-n`; never rank an `E-n` first. Run every handle and paste its actual output, pinned to the commit the build started from.**

**State plainly:** whether the duplicate-timestamp rate was re-measured or carried from §2.4, and whether `SignalId`'s per-instance reset was re-verified or taken from §0.1 Trap 2.

---

## 8. What this build must NOT do

- ⛔ Force or bundle an `analysis_log.csv` header rotation — that carries the five standing riders and the rule is **never force one**
- ⛔ Re-walk, re-judge or re-order any existing eval-cache outcome (§0.2)
- ⛔ Adopt `D-2` (c), the composite heuristic key, or `D-3` (b), the circular re-derivation — **both are ruled out, not merely unpreferred**
- ⛔ Key on `SignalId` alone — it is a per-`InstanceId` counter that restarts at 1
- ⛔ Ship the loop fix separately, or write it against `Timestamp` and migrate it later — `D-1` (b) exists to prevent exactly that
- ⛔ Give the new identity fields non-null defaults — an absent identity must stay distinguishable, or `D-3` (a)'s fallback cannot fire
- ⛔ Tag the commit `[no-engine-change]` — a v6 → v7 schema change owes a `docs/DeribitIndicatorProject.md` §15 entry
- ⛔ Touch `settings.json`
- ⛔ Run any `git` write command — commit is the orchestrator's
