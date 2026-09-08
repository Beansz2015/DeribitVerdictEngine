# `S-4` — eval-cache backfill: key on identity, and fix the loop

⛔ **THIS IS A PROPOSAL WITH AN OPEN D-TABLE, NOT A BUILD-AUTHORIZED SPEC. Do not start building from it.** §3 needs a trader tick first.

⛔⛔ **WRITING IT FALSIFIED THE QUEUE ROW'S SIZING, which is why it is a proposal.** [`trader-tick-queue.md`](trader-tick-queue.md) §2 sizes `S-4` as **"two small changes, one seam"**. **One half is exactly that. The other half is a persisted-schema change to `analysis_eval_cache.csv` and cannot be done without one** — see §2.3. This is the same shape as `S2-2`, where writing the proposal falsified the fix as specified.

**Baseline commit: `a6cfb8d`.** Every line number here was read at that commit. ⛔ **Re-read before editing if `HEAD` has moved.**

**Source:** [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md) `S-4`. **Dates to settings v26.**

---

## 0. Model and effort — per option, because they are not the same job

| If §3 rules… | Model / effort | Why |
|---|---|---|
| **`D-2` (a)** — fix the loop only | **Sonnet, low–medium**, one short session | One line plus a fixture. No schema, no migration, no persisted field |
| **`D-2` (c)** — composite key from persisted fields | **Sonnet, medium** | Still no schema change, but the key's collision behaviour needs reasoning about and pinning |
| **`D-2` (b)** — true `(InstanceId, SignalId)` identity | **Sonnet, HIGH**, and treat it as its own session | Adds fields to two types, a **v6 → v7 eval-cache migration**, reader/writer changes, and a legacy-row rule. The migration machinery is well-trodden here but a migration that mis-handles legacy rows corrupts the perf strip's history |

⛔ **Do not let the tier be inherited from the queue row's "Sonnet, medium".** That figure was attached to the falsified sizing.

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

## 3. ⛔ THE D-TABLE — needs a trader tick before any build

| # | Decision | Options | My read |
|---|---|---|---|
| **`D-1`** | Build the loop fix (half A) independently of the key fix (half B)? | **(a)** yes — ship A now, decide B separately · **(b)** no — they land together or not at all | ⭐ **(a).** A is a real bug with a zero-schema fix and is strictly an improvement whatever B becomes. Holding it hostage to a schema decision buys nothing. ⚠ **The queue row's *"Both halves, or state why only one"* is SATISFIED by this document** — that is what §2.3 and `D-2` are |
| **`D-2`** | What is the dedup key? | **(a)** keep `Timestamp`, fix only the loop · **(b)** true `(InstanceId, SignalId)` pair + v6→v7 migration · **(c)** composite of already-persisted fields — `(Timestamp, Verdict, ExecResolution, EntryPrice)` | ⭐ **(a) NOW, (b) as the DIRECTION — and defer (b) until something else forces an eval-cache migration.** The measured duplicate rate is **zero on every sample ever taken**, so (b) buys correctness against a collision nobody has observed, at the cost of a persisted-schema change. ⛔ **(c) is REJECTED and should not be re-proposed: it is a HEURISTIC dressed as an identity.** Two genuinely distinct runs at the same second on the same verdict, resolution and entry price would still collide — and a key that is *nearly* unique invites exactly the "held safe by an assumption" failure this project has hit repeatedly |
| **`D-3`** | Under (b), what happens to legacy eval-cache rows that have no identity? | **(a)** null identity, **fall back to `Timestamp`** for those rows only · **(b)** re-derive identity by matching back to `analysis_log.csv` on `Timestamp` · **(c)** rotate the eval cache and rebuild from scratch | ⭐ **(a).** ⛔ **(b) is CIRCULAR — it matches on the very key the change exists to distrust — and must be rejected explicitly so it is not proposed as a "completeness" improvement.** (c) throws away the perf strip's history for a latent defect, which is a bad trade the pre-v5 rotate already made once for a real reason |

⚠ **If `D-2` is ruled (a), then `D-3` does not arise and the build is small.** Rule `D-2` first.

---

## 4. The build list

### 4.1 Half A — the loop fix. Applies under EVERY `D-2` outcome.

At `LivePerformanceTracker.vb:445`, after the entry is built and added to `newEntries` (`:464`), **record the key** so the rest of the batch sees it:

```vb
newEntries.Add(entry)
existingTs.Add(row.Timestamp)   ' [S-4] the set must learn about rows THIS loop created,
                                ' or a cold start admits two same-second rows while a warm
                                ' start drops one — the bias flips with cache state.
```

⚠ **Under `D-2` (b) this line adds the identity key instead**, but the structural point is the same: **the set is updated inside the loop.**

### 4.2 Half B — only if `D-2` is ruled (b)

Sequenced by dependency:

1. `LogRow` gains `InstanceId As String` + `SignalId As Long`; `ParseAnalysisLog` (`:1448`) reads columns `InstanceId` / `SignalId` **by name from `colIdx`**, never by position — ⛔ column 110/111 is what today's file happens to be, not a contract.
2. `EvalCacheEntry` gains the same two, **nullable/absent-tolerant**.
3. Eval-cache **v6 → v7**: two new columns. ⛔ **Capture `IsPreV7Schema` into a local alongside the existing probes at `:362-368`, BEFORE any rewrite** — the existing ones are written that way for a reason and a v7 probe placed after a rewrite reads false.
4. `LoadEvalCache` / `AppendEvalRows` / `WriteEvalCache` handle the two columns, absent-tolerant for legacy rows.
5. The dedup key becomes the pair, with `D-3` (a)'s fallback for identity-less legacy rows.

---

## 5. Fixtures — family `A69` (⚠ verify it is still free; `A68` was taken 2026-09-08)

⛔ **Every fixture must be mutation-proven, and the mutation must be applied WITH THE EDITOR.** `python` is **not installed on this box** — a `python -c` mutation silently does nothing and the harness then reports PASS against unmutated code. ⛔ **Assert `Build succeeded` before believing any harness result.** ⛔ **Restore by FILE COPY, never `git checkout -- <file>`** — that also reverts uncommitted work in the same file.

| Fixture | Asserts |
|---|---|
| **`A69a`** | **The cold/warm asymmetry is gone.** Two `analysis_log` rows sharing one timestamp, backfilled into an **empty** cache, must yield the **same count** as the warm path. ⛔ **This fixture is the whole point of half A — if it passes before the fix, it is not testing the defect** |
| **`A69b`** | The set is updated **inside** the loop — i.e. a three-row batch with two colliding rows admits exactly one, not two |
| **`A69c`** *(only under `D-2` (b))* | Two rows with the **same timestamp** but **different `(InstanceId, SignalId)`** are **BOTH admitted** — the case the timestamp key wrongly collapses |
| **`A69d`** *(only under `D-2` (b))* | A legacy row with **no identity** still dedups by `Timestamp` (`D-3` (a)), and a v6 file migrates to v7 **without altering any existing outcome field** — the §0.2 escalation boundary, pinned |

⚠ **`A69a` is the one most likely to be written so it cannot fail.** `AggregateRange`'s sibling `A68a` was inert until its range start was set to `DateTime.MinValue`; the analogous risk here is a fixture whose two rows do not actually share a timestamp, or whose cache is not genuinely cold. **State the input that makes it fail, then mutate.**

---

## 6. Acceptance criteria

Forward-looking — the implementer runs these and pastes actual output.

| # | Criterion |
|---|---|
| **AC-1** | Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck` all `Build succeeded`, 0/0 |
| **AC-2** | Harness `ALL PASS`. Count **339 → 341** under `D-2` (a), **339 → 343** under (b). ⛔ **Measure by RUNNING it, never by grepping `Check(`** |
| **AC-3** | `A67a`, `A68a`, `A68b` and every `A14*`/`A33*` fixture that drives `AggregateRange` still pass **unchanged** |
| **AC-4** | `tools/checks/verify-gate.ps1 -Mode local-fast` → `GATE PASSED` |
| **AC-5** | `grep -n "existingTs" LivePerformanceTracker.vb` shows the set is **added to inside the loop** — paste the real output |
| **AC-6** | Every fixture mutation-proven, **with pasted failure output** |
| **AC-7** | `settings.json` **untouched** — verify with `git diff --stat`; it must not appear. `S-4` dates to v26 but needs **no key** |
| **AC-8** | ⚠ **Under `D-2` (b) only:** an existing v6 `analysis_eval_cache.csv` migrates to v7 and **every pre-existing `EvalOutcome`, `TargetEverHit` and `FavBar`/`AdvBar` value is byte-identical afterwards.** Capture before/after and diff |

### 6.1 Rules that apply to the commit

- ⚠ **Engine display-string parity.** `AggregateRange` feeds the perf strip. If **any** rendered value could move, the card binding in `UI/MainForm_Render_Cards.vb` must be updated in the same commit **or the commit message must state why no card surface is affected.** Under `D-2` (a) nothing rendered moves — **say so explicitly rather than leaving it silent.**
- ⚠ **`docs/DeribitIndicatorProject.md` §15** — a v6→v7 eval-cache migration **is** a schema change and wants an entry. Half A alone does not; tag it `[no-engine-change]`.

---

## 7. What to report back

Two documents per [`batch-review-packet-convention.md`](batch-review-packet-convention.md): `docs/s4-batch-summary.md` and `docs/s4-spec-back.md`.

⛔ **Label reader-runnable handles `H-n` and build-time evidence `E-n`; never rank an `E-n` first. Run every handle and paste its actual output, pinned to the commit the build started from.**

**State plainly:** whether the duplicate-timestamp rate was re-measured or carried from §2.4, and whether `SignalId`'s per-instance reset was re-verified or taken from §0.1 Trap 2.

---

## 8. What this build must NOT do

- ⛔ Force or bundle an `analysis_log.csv` header rotation — that carries the five standing riders and the rule is **never force one**
- ⛔ Re-walk, re-judge or re-order any existing eval-cache outcome (§0.2)
- ⛔ Adopt `D-2` (c), the composite heuristic key, or `D-3` (b), the circular re-derivation
- ⛔ Touch `settings.json`
- ⛔ Run any `git` write command — commit is the orchestrator's
