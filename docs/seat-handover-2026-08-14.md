# Seat handover — 2026-08-14 (orchestrator seat)

**From:** the Opus orchestrator seat that opened on [`seat-handover-2026-08-12.md`](seat-handover-2026-08-12.md) and ran the downtime-repair arc end to end — spec, build, three follow-ups, review, deploy — plus the SH-1 coverage build, the E5 and F3 rulings, the 2026-08-14 copy-back, and the §15 trim.

**Read in this order:** CLAUDE.md session-start protocol (**step 6 is the state rule**) → [`trader-tick-queue.md`](trader-tick-queue.md) **§0a first — what is OWED** → this doc → **§0 below.**

> **The one thing to carry.** *A rule written in a document does not enforce itself at the moment of writing.* §15 names C1 as the exemplar of "one item gets ONE row" — and **I added three rows for one item four days later**, because at the time each looked like its own build. The same shape ran through the whole session: a stale "deploy still owed" line I repeated four times without checking the ledger that contradicted it; a containment check I wrote that could not fail; four consecutive builds shipping a verification handle that counted a name. **None of these were caught by the gate, the harness, or the build. Every one was caught by someone re-reading the thing they had just written or been handed.** The previous seat's lesson — *commission the attack, not the review* — held all session and is worth inheriting verbatim.

---

## 0. YOUR FIRST TASK — ⚠ BLIND RE-DERIVATION, then compare. Two parts, in order.

**Trader-directed 2026-08-14.** The absorption mechanism proposal **has already been written** — [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) — by the seat that wrote this handover.

> ## ⛔ PART 1 — DO NOT OPEN THAT PROPOSAL YET.
>
> **Derive the population yourself, from the raw books, and write your numbers down BEFORE you read mine.**
>
> **Model: Opus. Effort: high.**

⚠⚠ **Why this is structured as a blind check and not a review.** *Commission the attack, not the review* is this project's standing rule, and **nothing in the absorption chain has an independent eye: one seat wrote this handover, the measurement scripts, and the proposal.** A reviewer who reads the conclusion first will confirm it — the previous seat recorded four of its own conclusions being overturned, **every one by a measurement and none by an argument.** ⚠ **If you read my numbers first, this exercise is worth nothing.** That is not a figure of speech; it is the whole design.

### Part 1 — measure these, from `bin\Debug\net8.0-windows\analysis_log.csv` and `analysis_log_aws.csv`

**The definitions, so you measure the same thing I did — the values are deliberately withheld:**

- **Scope:** weekday only, UTC, **since v61 shipped (2026-07-23)** — pre-v61 geometry is a different shell.
- ⚠ **Report PER BOOK, not pooled.** The two boxes run different session coverage; a pooled count hides which contributed. Pool **only** for a stage-loss funnel, and say when you do.
- **Absorption columns:** `AbsorptionSignal` 100 · `AbsorptionLevel` 101 · `AbsorptionRatio` 102 · `AbsorptionAggrUsd` 103 · `AbsorptionPullFrac` 104 · `Verdict` 2 · `InstanceId` 109.
- **Episode row** = the tracker produced a level. **Pressed** = `AbsorptionAggrUsd > 0`.
- ⚠ **Session buckets: use the SHIPPED ones** — ASIA 0–7, LONDON 8–12, NY 13–23. **The 2026-07-23 derivation used ASIA 22–07 / LONDON 07–13 / NY 13–22, so its per-session figures are NOT comparable.** That doc flags the divergence itself; the 07-30 pass and `settings.json` both use the shipped set.

**Produce:** proximity-ACTIVE episodes · pressed · **pressed / active** · the funnel against the **shipped** v61 anchors (read them from `settings.json`, do not restate them) · `aggrUsd` and `AbsorptionRatio` percentiles · the `AbsorptionPullFrac` **distribution shape**, not just its median.

### Part 2 — then read the proposal and compare

**Where we agree**, say so and say what you checked. **Where we disagree, your number wins until one of us shows the other's method is wrong** — then edit [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) accordingly and record the correction in it, plainly.

⚠ **Four claims are load-bearing. Attack these first:**

1. **The headline ratio is stable across a 14× larger sample and both books agree independently.** If it is not stable, the proposal's central argument — that under-engagement is *structural* rather than small-sample — collapses, and Path A becomes arguable again.
2. **The funnel's biggest loss is at the observation stage, not at the anchors.** Check the stage counts add up and that my "pressed" definition is not quietly doing work.
3. **A hypothesis I FALSIFIED:** that `AbsorptionPullFrac` piles at exactly 1.000 (the ladder-shift signature). I found it does not, and therefore recommended **not** touching `max_pull_frac`. ⚠ **If I am wrong about that, the recommendation inverts** — so check the distribution shape yourself rather than the median.
4. **That the §8 residual is not diagnosable from the book at all**, because neither `pullLB`/`postLB` nor episode duration is logged. **If you find a way to test it from existing data, D-1 changes.**

⚠ **Things I could not measure and did not:** episode duration, `pullLB`/`postLB`, and therefore whether the 90 % observation loss is `window_sec` or a too-wide proximity gate. **If you can separate those two from logged data, that is the single most valuable thing you can contribute.**

### What the proposal is, once you are past Part 2

It re-opens the book-absorption proposal's **§8 residuals** — **`window_sec` too short** · **episode-cumulative pressing** · **D8 `pullFrac` inflation on sparse `postLB`** — on the evidence that the v61 geometry rescale alone did not lift flag rates into the 3–8 % design band. **It ends at a D-table and is NOT buildable until the trader ticks it.**

**The evidence base:** [`absorption-anchor-rederivation-2026-07-30.md`](absorption-anchor-rederivation-2026-07-30.md) §4–§5 · [`absorption-engagement-derivation-2026-07-23.md`](absorption-engagement-derivation-2026-07-23.md) · [`book-absorption-proposal.md`](book-absorption-proposal.md) §8.

### ⚠ Four things that will cost you if you skip them

1. ⚠⚠ **Do NOT re-derive anchors.** E5 ruled 2026-08-12 that they **hold at v61**, and Path A's V-table (0.5 / 5 000 / 1.0) is **rejected** — it projects **NY 0.000 %**, POOLED 0.064 %, and ~10–15 weekday-weeks to the activation gate. The finding is that **no anchor set reachable from the observable population lands NY in-band.** `absorption-anchor-rederivation-2026-07-30.md` §5a carries a dead-text banner saying so; **a reader landing on that table without the banner would ship the rejected path.**
2. ⚠ **Start by re-counting the population on the post-2026-08-11 book.** That doc's own §5c says plainly that its 208 episode rows over 6 weekday days may not be representative. **Path B's whole premise is that the mechanism under-observes** — so inheriting the old population would be assuming the thing you are testing.
3. ⚠ **The tape is now worth using and it was not before.** The store holds **441,279 August rows** and the era after 2026-08-11 17:18 is **`trade_seq`-verified 100.000 % complete** (134,204 rows, zero missing). Before that fix roughly half the tape was silently discarded, **biased toward dropping the later legs of sweeps** — exactly the aggressive prints absorption is about. **Any pressing measure taken from pre-2026-08-11 tape understates aggression, and the store has THREE eras (see §1).**
4. **`scoring_enabled` stays `false` throughout.** This is a display-only surface and the pass ships no settings change.

**When it is written, it goes to the trader as a proposal with a D-table — not to an implementer.**

---

## 1. State — verified in the tree 2026-08-14, with how to re-check

| Fact | Value | Re-check |
|---|---|---|
| Settings version | **v66** — tracked **and** `bin\Debug` | `Get-Content settings.json -TotalCount 2` |
| Push state | **ahead 3** at handover | `git status -sb` — **never inherit** |
| Next free fixture family | **A57** (`A56g` is high-water) | `Select-String verify/ordercheck/Program.vb -Pattern '\bA[0-9]{2}[a-z]_'` |
| Next free hard constraint | **HC28** (HC27 high-water) — unchanged for weeks | `Select-String tools/AutoTweaker/*.vb -Pattern 'HARD CONSTRAINT (\d+)'` |
| AWS collector | **live, `e551f15e-b245-4392-8e71-89e749636f1c` since 2026-08-13 13:10:32Z** — Part A + DR-1/DR-2 | `ws_health.log` at next copy-back |
| **AWS is the SOLE capturer** | Local capture OFF — overlay present in **both** bin trees, verified | `Test-Path bin\*\net8.0-windows\settings.local.json` |
| Repo store | July **122,018** · August **441,279** | `backtest_data\` |
| `analysis_log_aws.csv` | **18,984 rows**, installed 2026-08-14 | `bin\Debug\net8.0-windows\` |
| `DeribitIndicatorProject.md` | **121.5 KB / ~41K tokens** after the trim | ⚠ CLAUDE.md step 1 has been wrong twice — re-check, do not budget against it |

⚠ **The store still has THREE eras and any tape-derived measure must split on them:**

| Era | Boundary | State |
|---|---|---|
| Identity-less | before **2026-08-10 14:08:39Z** | five-field rows, no `trade_id`/`trade_seq`; **unmergeable, permanently** |
| Identified but incomplete | to **2026-08-11 17:18:42Z** | ⚠ **~50 % complete AND BIASED** — the guard kept one leg per millisecond, dropping the later legs of sweeps |
| Identified and complete | after that | ✅ **`trade_seq`-verified 100.000 %**, 134,204 rows, zero missing |

---

## 2. What happened this session

**Shipped and deployed:** the **downtime-repair arc**, four commits — Part A (hole-derived repair windows) plus follow-ups **DR-1** (removed a time tolerance filtering a completeness signal), **DR-2** (truncation cut made time-contiguous), **DR-3** (repair log stopped reporting the whole file as "rows appended"). Deployed 2026-08-13 13:10:32Z.

**Shipped:** **SH-1** — the coverage report now splits an hour at a capture-state marker, classifies each part, and reports `Defect` if either part is. ✅ **Confirmed working on real AWS data** at hour 2026-08-10 09:00.

**Ruled by the trader:** **E5 → Path B** · **the F3 watch → RETIRED** · **C1-coverage F2 → split the hour**. ✅ **The decision table is now empty — nothing is owed by the trader.**

**Ruled by this seat, after builds:** **D-3** (the residual combine order — the implementer raised it and was half right) · **Q1–Q5** on the DR builds · the **DR-1 width-floor removal**.

**Ops:** the **2026-08-14 copy-back**, installed. **The §15 trim**, 14 rows → 10.

**Corrected:** **five stale-state claims** in the queue (four "deploy still owed" lines and a malformed table row), **three stale rows** in `roadmap.md`'s state table — including one giving **A53** as the next free fixture family when `A53a–h` and `A55a–g` both existed — and **two stale deploy claims** inside §15.

---

## 3. What is open

**Read [`trader-tick-queue.md`](trader-tick-queue.md) §0a and §2.** Highlights only:

| Item | Model + effort | Note |
|---|---|---|
| ⭐ **Absorption proposal — BLIND re-derivation, then compare** | Opus / high | **§0 above — your first task.** The proposal is written; **the check is what is owed** |
| ⚠ **`V6` — the falsifiable prediction** | a read, not a build | ⛔ **ARMED BUT UNTESTED.** No outage since the deploy. **Read `trade_seq` completeness from the STORE, not the repair log** |
| ⚠ **`ws_health.log` under-reports outages** | Sonnet / medium | ✅ **Now a FINDING, not an investigation** — two routes named, spec must pick one |
| **An up-interval starts at the `DOWN` line** | Sonnet / medium | ⚠ **NEW** — a connect window reads as capture time. ⚠ **The obvious fix trades a false defect for a missed one** |
| **`CoverageReport`'s `gapMs` time tolerance** | Sonnet / medium | ⚠ **NEW** — the DR-1 pattern, third instance. **LOW severity, SAFE direction** |
| **Value-copy guard** (A54a option (d)) | Sonnet / medium | Brief ready to hand over as-is |
| **`WsTradeProbe`** shared reader · **eval-cache identity key** · the **`0.105`** literal · **C1-coverage F1** · two doc corrections | Sonnet/medium → Haiku/low | |
| ⚠ **`F2` — the `ResetBufferState` race** | | **Got more valuable this session:** DR-1 means repair can now *see* the sub-2-second holes it produces |

⚠ **The AWS cost path is still unresolved.** `roadmap.md` §5b parks the Postgres migration with DuckDB recorded as the cheaper answer.

---

## 4. Flagged, not ruled

- ⚠ **`D-3`'s residual ORDER is untested against real data.** Every AWS marker records `True`, so no `UnknownScope`+`NotCapturing` pair exists in the store. Fixture `A49w` is the only evidence.
- ⚠ **The coverage report pools `trade_seq` gaps across the write-guard boundary.** A run over a window spanning it reports **17,707 missing** — all of it the retired pre-fix era, while the current era is provably 100 %. **A reader would misread that as ongoing loss.** The report has no way to say so.
- ⚠ **`F3` names FOUR different things** — the B4b watch (retired), the 2026-07-02 EXIT GUARD watch (**not** retired, state unverified since v47), a `User-Agent` item in the queue's §2, and an ordinary finding index. `roadmap.md`'s F3 row carries one watch's status against another's description; **flagged there, deliberately not fixed** — resolving it needs someone to read the 2026-07-02 watch.
- **`Part B`** of the downtime repair is ticked but deferred by its own §2.4 stop-and-ask. **Do not open a Part B session until Part A has healed one real outage.**

---

## 5. Conventions established this session

1. ⚠⚠ **A verification handle asserts the DECLARATION, never the bare name.** **Four consecutive builds** shipped a handle that counted a name and was falsified by a comment — `_lastTs` printed 2, `repairHoles` 3, `MinHoleMs` 5, `CountDataRows` 1. The fourth packet even pre-empted the *other* half of the previous review's finding while reproducing this half one row below it. **The lesson was being learned instance-by-instance, not as a class.**
2. ⚠⚠ **CONCATENATE the store; never dedup on disk.** Whole-line dedup applies a **weaker** relation than the read path's identity-first `DedupTrades` and the loss is permanent — measured at **23,806 genuinely distinct rows** on one file. Duplicates on disk are harmless by design.
3. ⚠ **A containment check must compare each source against the OTHER source, not against the result.** Mine compared each against the union containing it, so "rows lost: 0" could not fail.
4. **Check §15's ROW COUNT before reaching for the archive.** Both trims so far were **sprawl, not age**.
5. **Use `git commit -F <file>`, never a PowerShell here-string.** Double quotes get mangled by native-argument passing and git parses the fragments as pathspecs. Cost two failed attempts.
6. **Write the falsifiable prediction before the fix** — carried from the previous seat, and it paid again: the write-guard's *"completeness should go to ~100 %"* was settled this session on a sample 2,033× larger.

---

## 6. Things I got wrong, recorded plainly

1. ⚠⚠ **I repeated a stale "the v66 deploy is still owed" claim FOUR times, including inside deploy instructions I handed the trader** — while the ledger in `aws-collector-deploy-checklist.md` §5a had recorded that deploy since 2026-08-10. **I read the queue and never opened the ledger the queue itself points at.** The dates looked contradictory and were not: the commit is stamped GMT+8, the ledger UTC.
2. ⚠ **My first containment check for the copy-back was vacuous**, and I presented a `*** DO NOT INSTALL ***` result whose reasoning was wrong before catching it. The *conclusion* to stop was right; the *evidence* was not.
3. ⚠ **I flagged the undocumented 2026-08-11 20:47 `DEGRADED` as a probable tape hole.** It was not — zero missing sequences across the window.
4. ⚠ **I contributed three §15 rows for one queue item**, four days after reading the rule that names exactly that violation.
5. ⚠ **I broke `trader-tick-queue.md`'s tables three times** — twice by pasting report output containing literal pipes, once by inserting a multi-line block into a table cell. My own checker caught all three, but I introduced them.
6. **I planned the §15 trim as an archive move.** It was not: retention was already at its cap and nothing qualified. The remedy was collapsing, not archiving.

---

## 7. What I did not verify

- ⛔ **Anything on the AWS box directly.** Every AWS fact is from copied-back files or the trader's report.
- ⛔ **Any mutation, in any of the five review passes.** I re-ran the harness clean each time; **I did not re-apply a single mutation myself.** Every mutation claim in this session's packets is the implementer's, not mine.
- ⛔ **`V6`.** No outage has occurred since the deploy.
- ⛔ **The `A49u` gap arithmetic** (`span1.LongestGapMs = 420000`) — the fixture asserts it and passes; I did not recompute it.
- ⛔ **`DR-2`'s truncation path under a real 500,000-row scan.** Derived from reading the code.
- ⛔ **The token count in §1.** It is the 2026-08-12 measurement scaled by the byte reduction, not a fresh count. Treat as ±10 %.
- ⛔ **Concurrency, anywhere.** No live run was performed this session at all.
