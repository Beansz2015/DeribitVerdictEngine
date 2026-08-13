# Seat handover — 2026-08-12 (orchestrator seat)

**From:** the Opus orchestrator seat that opened on [`seat-handover-2026-08-10.md`](seat-handover-2026-08-10.md) and ran the D3 ASIA watch read, the v66/D2 ship, the AWS store merge, the discovery-to-deploy arc of the trade-store write-guard defect, the seam audit, and two decision consults.

**Read in this order:** CLAUDE.md session-start protocol (**step 6 is the state rule**) → [`trader-tick-queue.md`](trader-tick-queue.md) **§0a first — what is OWED** → this doc → **§0 below.**

> **The one thing to carry.** *Commission the attack, not the review.* Four of my conclusions were overturned this session — every one by a **measurement**, none by an argument, and in three cases the measurement was one I ran on myself after being asked to check. The seam audit found a defect in code I had written the day before. An implementer's spec-back found a false justification I had put in my own spec. My review of that spec-back found their headline verification handle was wrong. **None of it was caught by the gate, the harness, or the build — all of it by someone reading the thing they were handed instead of trusting it.** When a recommendation is load-bearing, hand it to someone told to break it.

---

## 0. YOUR FIRST TASK — read `trader-tick-queue.md` §0a, then pick from §2

**No single task is gating anything.** Unlike the last handover, there is no blocked watch and no unread instrument. **Two things are owed by the trader** (the F3 watch decision · C1-coverage F2's split-hour rule) and everything else is a build slot you can schedule.

> ## ⚠ AMENDED 2026-08-12, after this doc was written — **NOTHING IS OWED BY THE TRADER ANY MORE.**
>
> All three decisions closed the same day, and **three of them turned into work rather than into nothing:**
>
> | Decision | Outcome | What it became |
> |---|---|---|
> | **E5** absorption Path B | ✅ **TICKED — Path B.** Anchors hold at v61; no settings change, no ⚠ | A **mechanism-revision proposal** — [`trader-tick-queue.md`](trader-tick-queue.md) §2. Opus / high |
> | **F3 watch** (B4b trigger) | ✅ **RETIRED**, explicitly | ⛔ Nothing. The tooling row is cancelled |
> | **C1-coverage F2** split-hour | ✅ **RULED — split the hour at the marker** | A **build slot** — queue §2. Sonnet / medium |
>
> **And the highest-value item below is now specced AND authorised:** [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md), D-table ticked in full. ⚠ **Part A only for now** — gate G-1 came back "venue-wide Deribit outage", which demotes Part B to a latency improvement; that spec's §2.4 carries a stop-and-ask.
>
> ⚠⚠ **`F3` names TWO different watches and only the B4b one was retired.** The 2026-07-02 audit-fixes F3 (EXIT GUARD strip vs HOLD\EXIT during holds) is untouched and its state is unverified. [`roadmap.md`](roadmap.md)'s F3 row conflates them — flagged there, **not fixed**.

**If you want the highest-value item, it is this one:**

> ⚠⚠ **Gap repair cannot heal downtime loss** — [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md) **§5a-bis**. Live, undocumented until 2026-08-12, and it has already cost an hour of tape. **Model: Opus, effort: high.** ✅ **Specced 2026-08-12** — see the amendment above.

⚠ **Do not read that section and conclude the fix is a bigger `gap_repair_lookback_hours`.** The **cursor** skips the hole, not the window. The section says so; a reader in a hurry will miss it.

---

## 1. State — verified in the tree 2026-08-12, with how to re-check

| Fact | Value | Re-check |
|---|---|---|
| Settings version | **v66** (D2, OBV `trend_gate` 18→23) | `Get-Content settings.json -TotalCount 2` |
| Push state | **ahead 2** at handover | `git status -sb` — never inherit |
| Local `bin\Debug` settings | **v66** — in sync, overlay present, capture correctly **OFF** | `Get-Content bin\Debug\net8.0-windows\settings.json -TotalCount 2` |
| Next free fixture family | **A56** (A55g is high-water) | `Select-String verify/ordercheck/Program.vb -Pattern '\bA[0-9]{2}[a-z]_'` |
| Next free hard constraint | **HC28** (HC27 high-water) — unchanged all session | `Select-String tools/AutoTweaker/*.vb -Pattern 'HARD CONSTRAINT (\d+)'` |
| AWS collector | **live, `a5d701ad-eea1-4ba0-97a5-2ea05274c8c5` since 2026-08-11 17:18:42Z** — the write-guard fix | `ws_health.log` at next copy-back |
| **AWS is the SOLE capturer** | Local capture OFF, overlays verified in both `bin` trees | `Test-Path bin\*\net8.0-windows\settings.local.json` |
| Repo store | July **122,018** · August **278,761** | `backtest_data\` |
| ⚠ **Copy-back 2026-08-12 is NOT merged** | Its August file holds **307,141** rows against the repo's 278,761 | see §3 |

⚠ **The store now has THREE eras, and any tape-derived measure must split on them:** five-field identity-less (before 2026-08-10 14:08) · identified but **~50 % complete and biased** (to 2026-08-11 17:18) · identified and complete (after). **The middle era's loss is not random — it dropped the later legs of sweeps.**

---

## 2. What happened this session

**Shipped:** **v66 / D2** — `indicators.OBV.trend_gate` 18.0 → 23.0, a six-month re-anchor, ⚠ a live scoring change and dataset boundary. **The trade-store write-guard fix** (`1cec1ea`) — discovered, evidenced three ways, gated on a live venue probe, specced, built by an implementer, reviewed, deployed, **and verified in production against a prediction written before the fix: 61.88 % → 100 % `trade_seq` completeness.**

**Read:** the **D3 ASIA arming watch**, first time since arming — **PASSED**, 10.97 % fire rate over 12 fully-covered weekday session-days, same-side 89.47 %. Its tolerance (T-1…T-5) was ruled, replacing a trigger value that had no band and no read length.

**Ran:** a **seam audit** by a fresh seat (7 findings) and **two decision consults** by an independent reviewer.

**Ruled:** the A54a scope (option **(d)** — a reflection guard, not the two options offered) · the session-bucket seed (**do not empty; guard it**) · the minute-key dedup (**keep it**) · Q1–Q5 on the write-guard build.

**New standing rules in CLAUDE.md:** fixture-literal provenance · `Public Const` for ruled constants · verification handles must test the property, not a string that mentions it.

---

## 3. What is open

**Read [`trader-tick-queue.md`](trader-tick-queue.md) §0a and §2.** Highlights only:

| Item | Model + effort | Note |
|---|---|---|
| ⚠⚠ **Repair cannot heal downtime** | Opus / high | §0 above. The biggest live gap. ✅ **SPECCED 2026-08-12** — [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md), §7 D-table awaits a tick, §2 gate G-1 unanswered |
| **Absorption mechanism revision** | Opus / high | ⚠ **NEW 2026-08-12** — what ticking E5 Path B bought. A proposal file, no code, no settings |
| **Value-copy guard** | Sonnet / medium | [`value-copy-guard-implementer-brief.md`](value-copy-guard-implementer-brief.md) — **ready to hand over as-is** |
| ⚠ **`ws_health.log` under-reports outages by ~34 min** | Sonnet / medium | Affects `CoverageReport`'s S1 join |
| **Eval-cache identity key** | Sonnet / medium | Latent, measured zero occurrences |
| **The `0.105` stale fixture literal** | Sonnet / low | First real test of the new provenance rule |
| **Merge the 2026-08-12 copy-back** | ops | ⚠ **Count rows before and after; every number must rise.** August 278,761 → expect ~307,141 |

**Owed by the trader:** ✅ **NOTHING — all three closed 2026-08-12.** ~~E5 absorption Path B~~ **ticked (Path B)** · ~~the F3 watch decision~~ **retired** · ~~C1-coverage F2's split-hour rule~~ **ruled (split the hour)**. See §0's amendment for what each became.

⚠ **The AWS cost path is still unresolved** and now has better inputs: `docs/roadmap.md` §5b parks the Postgres migration with DuckDB recorded as the cheaper answer, and the intentional-downtime scoping row has widened.

---

## 4. Flagged, not ruled

- ⚠ **Re-run the post-fix completeness check on a larger sample.** The 100 % figure rests on **66 rows** — enough to prove the rate *changed*, not enough to distinguish 100 % from ~97 %.
- **F2** (`ResetBufferState` race, `DeribitWsFeed.vb:298`) is untouched and now **explicitly distinguished** from the same-millisecond drop in the queue. Different bug, different fix.
- **How much pre-fix tape is repair-written (complete) vs streaming-written (~50 %)** — determines how usable that era is for re-derivation. No urgency.
- ⚠ **`DeribitIndicatorProject.md` measures 47K tokens, not the "~24K" CLAUDE.md step 1 claims.** §15 has re-grown; CLAUDE.md's own note predicts exactly this. **A trim to the archive is owed.**

---

## 5. Conventions established this session

1. **Commission the attack, not the review.** A reviewer told to concur will concur. Both consults this session were explicitly instructed to break the argument, and both did — on evidence.
2. **A verification handle must test the property, not a string that mentions it.** Counting a name is a copy of the property and drifts from it.
3. **A value ruled into a constant goes `Public Const`.** The ruling is about tunability, not visibility.
4. **A fixture passing a settings-derived literal must declare whether it asserts SHIPPED BEHAVIOUR (derive from cfg) or MECHANISM (literal + comment).** No tool can tell them apart.
5. **Write the falsifiable prediction before the fix.** *"Completeness should go to ~100 %; if gaps remain, this build did not find the whole problem."* It made the post-deploy check a one-line verdict instead of an argument.
6. **When an item keeps acquiring deadlines that do not survive checking, doubt the deadline.**

---

## 6. Things I got wrong, recorded plainly

1. ⚠⚠ **My spec justified the write guard with a mechanism that does not exist.** I wrote that `SeedAsync`'s REST re-seed reaches the store. It does not — it goes to the in-memory ring, and `store.Buffer` has exactly one caller. **I inherited v64's comment without checking it.** The implementer found it while writing their spec-back, after the gate had passed.
2. ⚠ **I introduced a fourth trade-parse site the day before an audit found it.** `tools/WsTradeProbe` was deliberately standalone for good reasons and reintroduced the defect class anyway — in the instrument built to certify that class's fix.
3. ⚠ **Both my recommendations on the seam-audit decisions were defeated.** Option (b) would not have fixed the copy that actually broke (A6 passes the literal explicitly), and my "wrong is silent, absent is detectable" argument was **factually wrong** — both degraded modes announce via the same banner.
4. **I claimed the minute-key dedup needed fixing before the next pooled read.** Measured: it costs the Kelly trigger **one row in 281**. Reproducibility beat it.
5. ⚠ **I attached false urgency to §5b three separate times**, and it was wrong every time. See §5b's own retirement note.
6. **My v66 commit said "POCO in lockstep."** There are four copies; it moved two.
7. **I corrupted `history-archive.md`** with a PowerShell encoding mistake mid-edit — 166 changed lines and a stray BOM. Caught by checking the diff, reverted, redone with the Edit tool.

---

## 7. What I did not verify

- **Anything on the AWS box directly.** Every AWS fact is from copied-back files or the trader's report.
- **The write-guard build's mutation table.** I read `§9.2`; I did not re-run each mutation.
- **The seam audit's fixture claims** beyond the four I checked in the tree. The audit itself ran read-only, so its fixture claims are hand-arithmetic, and it says so.
- **The 2026-08-12 copy-back beyond the three questions asked** — same-ms behaviour, the latest InstanceId, and the `3be7f4c9` timestamp. **I did not run a coverage report on it.**
- **Whether Deribit ever resets `trade_seq`.** Still unverified; nothing shipped depends on it.
- **Concurrency anywhere.** No live run was performed this session at all.
