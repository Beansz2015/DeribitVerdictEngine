# Seat handover — 2026-09-11 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-09-10.md`](seat-handover-2026-09-10.md)** — superseded for STATE. **Its §11.1a–§11.1d memory work and its §8.1 lesson table still bind.**

**Settings: v68**, unchanged all session. ⛔ **Run `git status -sb` — never inherit a push state from this line.** At close it read **in sync, 0 unpushed, clean, no worktrees.**

---

## 0. ⛔⛔ READ THIS BEFORE YOUR FIRST REPLY — the trader's output format

**They live in `C:\Users\user\.claude\CLAUDE.md` under *"Output format — applies to every reply, in every project"*. READ THEM THERE.** Also the memory `feedback-output-format-is-a-standing-rule`, because **memory loads automatically and a handover does not.**

- ⭐ **Point form and tables. Not paragraphs.** **A table whenever you compare more than two things.**
- ⛔ **NEVER a bare section number.** Write `` `docs/trader-tick-queue.md` §0a ``, never `§0a`. **Repeat the document name on later mentions in the same reply.**
- ⛔ **NEVER a bare ID.** Give source, kind and meaning on first use **in every reply**.
- **Simplified technical English.** One instruction per sentence. ~20 words. ⚠ **Sentence STRUCTURE only — keep every domain term.**
- **Model + effort on its own labelled line.** ⭐ **Separate what you verified from what you carried.**

---

## 0a. ⭐⭐ READ THIS BEFORE YOUR FIRST DECISION — the default FLIPPED on 2026-09-11

⛔⛔ **THE BIGGEST CHANGE THIS SESSION IS NOT CODE. IT IS HOW YOU DECIDE.** **Full text in `CLAUDE.md` under *"AUTO-PROCEED ON YOUR OWN RECOMMENDATION"*. READ IT THERE.**

- ⭐ **When a decision has an option you can recommend, TAKE IT.** Record it in one line and keep working. **Do not stop and ask.** Covers D-tables and one-offs alike.
- ⛔ **The gate is REVERSIBILITY AND BLAST RADIUS — explicitly NOT *"can I recommend"*.**
- ⛔ **SIX RESERVED classes still go to the trader:** `settings.json` changes · **anything affecting SCORING** · anything moving a rendered value · writes to the live collector or trade store · schema and CSV-header changes · **any decision where your pick is CHEAPER *AND* LESS TRUTHFUL than an available alternative.**
- ⚠ **Scoring and rendered-value are DISTINCT sets.** A scoring threshold moved where no current row crosses it renders identically and is still a live scoring change. **A code revert does not un-write the `analysis_log.csv` rows already logged under it.**

### 0a.1 ⭐⭐ THE TRUTHFULNESS PRIOR — the trader's own words, and the reason generalises

> **"My default choice is usually the more truthful choice, because it logically tracks if future orchestrators lose track in the docs and can't deduce it reliably from code."**

⭐⭐ **DOCS ROT, CODE SURVIVES.** **A truthful behaviour is SELF-DESCRIBING — a future seat reads the code and learns what is true. A behaviour that quietly tolerates a known-bad state is a lie the same seat CANNOT detect, because nothing says what was tolerated or why.**

### 0a.2 ⛔ WHAT "CHEAPER" MEANS — settled 2026-09-11, three-step test

| Step | Ask | Then |
|---|---|---|
| **1** | Is there an option that **records more, guarantees more, or is more self-describing**? | **No → TAKE YOURS.** Cheapness is irrelevant |
| **2** | Yes, and your reason for skipping it is *"mine is adequate"* | ⛔⛔ **RESERVE IT** |
| **3** | Your reason is *"the richer option is mechanically WRONG or uninterpretable"* | ✅ **TAKE YOURS and NAME WHICH** |

⭐⭐ **THE TELL IS THE WORD, NOT THE COST: "adequate" · "good enough" · "buys nothing" · "defer until something forces it".**

⛔ **Two readings are WRONG and the record kills both:** *"cheapest of all options"* — refuted by `A54a`, whose recommendation reads verbatim *"My read: (b). **Bigger diff**"*, the MORE expensive option and still the failure. *"cheaper by any margin"* — refuted on consequence: cheaper-that-gives-up-nothing is simply correct, and flagging it fires the tripwire constantly.

---

## 1. ⛔ THE CLOCK TRAP HAS NOW FIRED IN TEN CONSECUTIVE SESSIONS

⛔⛔ **The workstation is GMT+8. The harness announces the LOCAL date. All project dates are UTC.**

**This session the local clock read 2026-09-10 and then 2026-09-11 while UTC was a day behind each time.** ⛔ **RUN `date -u`. EVERY TIME. Knowing about it does not prevent it — ten sessions prove that.**

---

## 2. ⭐ FIRST TASKS

| Pick | Item | Notes |
|---|---|---|
| ⭐ **1st** | **Read [`trader-tick-queue.md`](trader-tick-queue.md) §0a and verify before offering work** | **Nothing is owed. No ruled build slot is open** |
| **2nd** | ⛔ **Do NOT offer any spec from this session as work** — every one is BUILT | See §3 of this document |
| **3rd** | ⚠ **The absorption Path B §6 tick is the ONE thing waiting on the trader** | Outstanding since 2026-08-14 — **about four weeks.** It unblocks a SCORING change, so it is correctly reserved. **Raise it; do not inherit it silently** |

---

## 3. What shipped — 38 commits, `settings.json` in NONE of them

**6 of the 38 changed engine behaviour; the rest are `[no-engine-change]`.**

| Item | Commit |
|---|---|
| Stale-status sweep — 12 docs offered shipped work | `c90df3b` · `6cc4d8f` · `e8ea9af` · `71aa14c` |
| `CLAUDE.md` `A56b` ID collision fixed | `2f46679` |
| Queue archive split — 284,401 → 160,383 B | (session-2 of the sweep) |
| **Atomic writes — SIX sites, not the billed five** | `5b0f8cc` |
| `RM-1` — `roadmap.md`'s stale ready-list | `aa979a8` · `8e47715` |
| 2026-09-05 DEGRADED event CLOSED — zero tape lost | `7a4518c` |
| Segmented memory read — second 24 h sample | `a760b7c` |
| **Coverage cluster `C-1` + `C-2`** | `ea8907e` |
| **`C-3a` declared operating schedule** | `25b8691` |
| `D-7` ruled + `A73i` | `b4e35f1` · `7ec60a2` |
| **Venue-status instrument** | `88538d7` → `67b139c` |
| **`VENUE_RPC_<code>`** | `519ce53` → `a5edaa4` |
| **`C-3b` Part A (live defect) + Part B** | `85361fe` · `16b19f6` → `20ea9bf` |
| The auto-proceed ruling · truthfulness prior · three-step test | `3db0541` · `b4e35f1` · `4296eb8` |

**Harness 349 → 376.** `GATE PASSED` throughout. **Settings v68 untouched.**

**Measured at close:** next free fixture family **`A78`** (`A77e` is high-water) · `HourClass` has **10** members · `DeribitIndicatorProject.md` §15 holds **24** rows.

---

## 4. ⭐⭐ The coverage/venue arc — read this before touching either

**Four specs, all BUILT. Each opens with its own BUILT banner.**

| Spec | State |
|---|---|
| [`coverage-report-cluster-spec.md`](coverage-report-cluster-spec.md) | `C-1`, `C-2`, `C-3a` — ✅ BUILT |
| [`venue-status-instrument-spec.md`](venue-status-instrument-spec.md) | ✅ BUILT. `D-1` ruled **(d)** |
| [`venue-status-rpc-code-fix-spec.md`](venue-status-rpc-code-fix-spec.md) | ✅ BUILT |
| [`c3b-venue-scoping-spec.md`](c3b-venue-scoping-spec.md) | ✅ BUILT, both parts |

### 4.1 ⛔ The `V-1` rule that governs all of it — do not weaken it

⛔⛔ **`venue_status.log` records ONLY a response the venue itself returned.** **A timeout, DNS failure or TCP refusal writes NOTHING — those are indistinguishable from our own box being broken, and logging them would let the coverage report scope out OUR defects.**

⭐ **The guard is STRUCTURAL, not conditional:** `GetAsync` throws before `RecordVenueIfNeeded` is reached. **It cannot be defeated by editing the policy function.**

### 4.2 ⛔ And the same rule one level down, in the consumer

**The log records our own bad requests too** (`VENUE_RPC_10009` and the like). ⛔ **Only VENUE-SIDE codes scope an hour out** — any 5xx, plus `VENUE_RPC_11051`. **Everything else, including `VENUE_RPC_UNKNOWN` and any unrecognised code, stays `Defect` with the code carried in the Reason.**

⭐ **An unrecognised code FAILS SAFE TOWARD FLAGGING, deliberately: an incomplete code set costs FALSE DEFECTS, never false excuses.** ⛔ **Do not "improve" that by widening the default.** ⚠ **`11051` is CARRIED from a 2026-08-11 observation and is NOT verified against Deribit's documentation. Widen the set only on observed evidence.**

### 4.3 ⭐ `D-7` — a deliberate scope expansion, so nobody "fixes" it back

**For a span where EVERY row carries `trade_seq`, the sequence is the verdict: `storeClean = seqContiguous`, and the time tolerance is not consulted.** **Measured: captured 92 → 68, DEFECT 4 → 28 — 24 hours moved `Captured` → `Defect` and that is INTENDED.** ⭐ **Those hours genuinely hold missing trades; a report calling provably-incomplete tape `Captured` is the silent-hole class this repo rejects.**

---

## 5. Collector health — ✅ READ 2026-09-11 15:47 UTC. A real read.

**`tools/ops/collector.ps1 status -InstanceId i-0d6c133058876273e`.**

| Reading | Value |
|---|---|
| Settings on the box | ✅ **v68** — matches tracked |
| Live `analysis_log.csv` | **9,192 rows**, `2026-09-01 15:50:01 → 2026-09-11 15:47:01` |
| Freshness | ⭐ **gap 0.4 min — collecting now** |
| Rate | **38.3 rows/hour** |
| Host / app uptime | 22d 00:27 / **9d 23:57** (exe built 2026-09-01 15:48 — looks like a deploy, ⚠ not verified) |
| `ws_health` tail | ⭐ **No new events since 2026-09-06.** No new DEGRADED |
| Store | `trades_2026-09.csv` **77,916 KB** (was 65,707 KB on 09-10) |

### 5.1 ⚠ Memory — worse than yesterday, and the confound is UNRESOLVED

| | 2026-09-10 18:22 UTC | **2026-09-11 15:47 UTC** |
|---|---|---|
| `avail` | 124 MB | ⛔ **104 MB** (30 s avg **91**) |
| `pagesOUT/s` 30 s avg | 289.3 | ⛔ **548.2** |
| pagefile | 36.0 % | 36.8 % |

⛔⛔ **DO NOT COMPARE THESE TWO READINGS DIRECTLY — they are different HOURS and the baseline is per-hour.** **Yesterday's was hour 18, whose detached baseline is 0.00 % over 720 samples. This one is hour 15, whose baseline is 4.72–7.78 % — a band where bursting is EXPECTED.** ⭐ **So unlike yesterday's, this reading is NOT anomalous against its own hour.**

⛔ **And the confound from [`seat-handover-2026-09-10.md`](seat-handover-2026-09-10.md) §11.1d still stands: BOTH readings are SSM-ATTACHED, and an attached session costs this box 30–50 MB.** **The clean test remains ONE DETACHED 24 h counter run of our own** — ⚠ **not `RIPPerfBaseline`, which belongs to the colocated hostel app.**

### 5.2 ⛔ The Kelly trap — do not repeat it

**Live-file verdicts: `STRONG SHORT` 65 · `STRONG LONG` 29 = 94 all-day STRONG.** ⛔⛔ **That is NOT comparable to the Kelly gate.** **94 is all-day and live-file-only; the trigger counts WEEKDAY STRONG across the pooled book, which read 407.** **Use `tools/ops/kelly-trigger-read.ps1 -Mode Box`.** ⚠ **NOT run this session.**

---

## 6. ⚠ What is OPEN

- ⛔ **Absorption Path B §6 — the ONE trader tick outstanding.** ~4 weeks. Unblocks a scoring change, so correctly reserved.
- **`_evalCache` is unbounded**, on an 8–17 month clock, **and its obvious fix is destructive — no spec exists.** ⭐ **This is the only ungated no-spec item with real risk behind it.**
- **A detached 24 h memory counter run** — see §5.1 of this document.
- **Re-measure the BUSY half (00–15 UTC) of the per-hour memory table** — it moved **+47 %** in three days, so it is not a baseline.
- **`ws_health.log` under-reports an outage** — ⚠ **this session's evidence points the OPPOSITE way to how that row is worded**: the 09-05 tape hole was LONGER than the DEGRADED window.
- **Fills-import** — LATER. **`S-1`** — ruled (a), NOT NOW. **S0 `--verify-venue`** — SUSPENDED.
- ⛔ **FIVE RIDERS** that cannot travel alone; they attach to the next `analysis_log.csv` header rotation, and **NEVER FORCE ONE.**
- ⚠ **`11051` unverified** — §4.2 of this document.

---

## 7. ⭐ The lessons — the count is the finding again

### 7.1 ⛔⛔ FIVE instrument failures, and FOUR were the instrument, not the subject

| The instrument | How it lied |
|---|---|
| A gate log | polluted by its own echo line |
| A truncated read (`cut -c1-420`) | reported ABSENCE where there was only truncation |
| A raw pipe count | flagged four CORRECT tables |
| An attached probe | measured its own 30–50 MB cost |
| A `§15` row-count check | **I wrote "was 23" from memory and presented it as a test** |

⛔⛔ **BEFORE REPORTING A DEFECT, ASK WHAT THE INSTRUMENT WOULD SAY IF THERE WERE NO DEFECT.**

### 7.2 ⛔ A ONE-DIRECTIONAL ESCALATION TRIGGER IS HALF A TRIGGER

**The coverage spec's trigger named only `Defect → Captured`. The build moved 24 hours the OTHER way and had nothing to trip.** ⭐ **Make class-change triggers BIDIRECTIONAL and demand a before/after count.**

### 7.3 ⭐⭐ A SPEC CAN UNDER-SPECIFY A MODEL AND THE BUILD WILL BE CORRECT ANYWAY

**The venue instrument's parent spec said *"transition-only … mirrors `WsHealthLog`"* and then gave a trigger table that only logs PROBLEMS.** **`ws_health` logs `OK` transitions too.** **The implementer built exactly what was written — and the result was a log that records an outage's START and never its END, plus a live defect where a repeat outage was silent for a 9-day process.** ⛔ **"Mirror X" must say whether you mean the SHAPE or the MODEL.**

### 7.4 ⭐ A STRUCTURAL PROOF CAN BEAT THE EMPIRICAL ONE YOU ASKED FOR

**`AC-6` demanded a byte-identical report. The implementer gave a structural argument — empty path ⇒ empty array ⇒ empty list ⇒ zero loop iterations.** ⭐ **That is STRONGER: it covers every window, not one, and it answers the bidirectional trigger at the same time.** **Verify such an argument by reading; do not reject it for not being a run.**

### 7.5 ⚠ THE `model` PARAMETER TAKES ALIASES, NEVER A PINNED VERSION

**`Agent`'s `model` accepts only `sonnet`/`opus`/`haiku`/`fable`.** ⛔ **So a spec's *"Model: Sonnet"* cannot guarantee a generation, and `ListAgents` does not report what the alias resolved to.** ⭐ **Size specs so the recommendation survives the alias resolving a generation back — or name `opus` when the central trap is SEMANTIC rather than mechanical.** ⚠ **`C-3b` Part B's `B-1` trap was semantic and was specced `Sonnet, MEDIUM`; it came back correct, but that sizing was optimistic.**

### 7.6 ⭐ RUN A BUILD AGENT IN ITS OWN WORKTREE

**A background agent in the shared tree made the pre-push gate FAIL on a doc-only commit — it was building a half-finished tree, and it was right to refuse.** ⭐ **`isolation: "worktree"` on every build agent. Remove the worktree and delete the branch after merging.**

---

## 8. ⚠ What I did NOT verify

- ⚠ **`docs/DeribitIndicatorProject.md` was NOT read this session, and its token figure was NOT re-measured.** `CLAUDE.md` quotes **62,068 tokens at 148,336 B, measured 2026-09-06**. ⛔ **The file has grown since — do not scale, measure it.**
- ⚠ **`unparsed=0` and the `.bak` row count** — the `status` verb does not report either. Carried.
- ⚠ **Weekday STRONG / the Kelly gate** — NOT read. §5.2 of this document.
- ⚠ **`11051`** — carried, never verified against Deribit's docs.
- ⚠ **The harness was run via the gate, ALL PASS at 376.** **I did not audit individual older fixtures.**
- ⚠ **Of the 8 recorded "DEFEATED" recommendations behind the auto-proceed ruling, I read FOUR in full.** The economy-bias claim rests on those four.
