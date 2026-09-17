# Seat handover — 2026-09-17 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-14b.md`](seat-handover-2026-09-14b.md), superseded for state. Its §6 lessons still bind.

**State at close (2026-09-17 16:4x UTC):**
- Settings **v68**, untouched this seat.
- ⛔ **`master` is 63 commits ahead of `origin/master`, none pushed.** Most are docs. **Engine code is among them:** the three gap-repair fixes and new harness fixtures (§3).
- Harness **409 PASS, `ALL PASS`** on `master` (re-run by this seat).
- The collector box still runs the **2026-09-01 build**. Nothing was deployed this seat.
- Worktree `claude/sharp-carson-96658f` is merged and can be removed.
- ⛔ Run `git status -sb` anyway; never inherit a push state.

---

## 0. ⛔ Before your first reply

| Rule | Where it lives |
|---|---|
| **Output format:** point form and tables; never a bare section number or bare ID; short active sentences that keep every domain term; separate verified from carried | `C:\Users\user\.claude\CLAUDE.md` |
| **Decision rule:** take reversible calls and log each in one line. Reserve `settings.json`, anything scoring, rendered values, collector or trade-store writes, schema changes, and any pick that is cheaper AND less truthful | `CLAUDE.md` "AUTO-PROCEED" |
| **Clock:** the workstation is GMT+8, the project is UTC. Run `date -u` first | memory `project_roadmap_signal_bridge` |
| **Vocabulary (NEW, trader-ruled):** success rate · gross / net breakeven rate · gross / net edge · **net EV per trade** (the headline). Breakevens pool distance-weighted | `DeribitIndicatorProject.md` §5a |
| **How the trader works now:** new work goes to scoped seats (a fresh conversation with a paste text, or a background agent). Seats send spec-backs to the orchestrator via `SendMessage`; the orchestrator re-runs the handles, reviews, and relays rulings | This seat's practice, §5 |

---

## 1. ⭐ FIRST TASKS — in this order

| # | Task | Why now | Class |
|---|---|---|---|
| **1** | **Spec, then build, the ruled engine fixes** (§2.1): the POC-gate swap; the liquidation flag (measure first); maker-side attribution; manual corrections; the analysis report's lean column; the card's funding-momentum effect | All ruled. The NY VPFR retest (`F-3`) waits for the POC and liquidation fixes | Spec: auto-proceed. The build moves scoring and rendered values: reserved values are already ruled; the deploy is reserved |
| **2** | **S1 and S2 of [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md), then ONE deploy** | ⛔ **The deploy carries the three gap-repair fixes.** Until it ships, an outage across **2026-10-01 00:00 UTC** loses trades for good (the cross-month defect), and the same-millisecond skip and scan-failure re-append stay live. **If S1/S2 cannot deploy by about 2026-09-28, put a separate gap-repair deploy to the trader** (it is not an `analysis_log.csv` dataset boundary) | Builds: per spec. Deploy: reserved |
| **3** | **`Q-1` option (d):** measure target and stop distance by tier (why success rate rises at the top score bins while net EV does not) | Decides `F-1` (no threshold re-cut) and `F-4a` (flatten tiers for Kelly sizing) | Analysis: auto-proceed |
| **4** | **The postponed reads,** at the next routine fetch (`tools/ops/collector.ps1 fetch`; always read from the dated `aws_fetch/<stamp>/` folder) | Postponed by the trader for the MEDIUM work. No data expires | Analysis |
| | a. The final absorption episode-age read (the `D-1` diagnostic columns) | Its 10-weekday data gate passed 2026-09-16 | |
| | b. The ASIA burst re-derivation read: brief [`asia-burst-rederivation-read-brief-2026-09-14.md`](asia-burst-rederivation-read-brief-2026-09-14.md) | Owed by ruling T-5 after the 2026-09-14 watch miss | |
| | c. The first live venue sample: the post-fetch hook runs by itself | ⚠ The sample-review reminder (`trader-tick-queue.md` §4, on or after 2026-10-05, needs 5 samples) will need re-dating | |

**Model / effort:** task 1 spec: Opus, high (two render surfaces, a scoring change, a live measurement first). Task 2: Opus, high, per its spec. Tasks 3–4: Opus, medium.

---

## 2. Ruled but not built

### 2.1 The engine fix build (task 1)

| Item | Ruling (trader, 2026-09-16) | Recorded in |
|---|---|---|
| **POC-tier gate inverted** (`D-1`): `CalcVPFRLite` emits `NEAR_HVN_RESIST` when price ≥ POC, and the long POC tier opens on that label (`Core/SignalEmitter.vb` ~330-332; legacy twin `ScoringEngine_Calculate_Verdict.vb` ~223-224). 0 POC placements in 38,665 rows | **(a) follow the spec: swap the labels in both copies.** Effect measured by the seat: 1.68 % of population rows flip to NO TRADE via the Step 5c minimum-move gate; 3.33 % get a different placed target; fixture `A26b` must be rewritten; `A80b` is the known-defect repro | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §3 |
| **Manual line 967** describes the VPFR vote's geometry backwards (`D-2`) | **(a) correct the manual** | same, §3 |
| **Liquidation flag never reaches scoring or the cascade alarm** (`L-1`, `D-4`): `LiqSignal` = NONE on 51,107 of 51,107 rows; streamed store copies flagged `none`, REST copies flagged. ⚠ The parse DOES read `liquidation` (`DeribitWsFeed.vb:488`), so the stream (`trades.BTC-PERPETUAL.100ms`) does not deliver it under that name; not verified why | **Measure first:** capture raw 100 ms trade messages until a liquidation passes (read-only, dev machine; ~2 a day). Then (a) parse the field if it exists under another name, else (b) enrich from REST. Never retire the vote | same, §R.3 |
| **Maker-side attribution** (`L-2`, `D-5`): `CalcLiquidations` books every flagged trade by taker direction | **`M` → maker's side, `T` → taker's side, `MT` → both.** Fix together with `L-1` (it goes live when `L-1` is fixed). `A81b` is the repro | same, §R.3 |
| **`large_liq_size` unit** (`L-3`, `D-6`): manual says 200 BTC, code compares USD sums | **Correct the manual to USD now; re-derive the value from real liquidation sizes after the fix** (`trader-profile.md` §7 method) | same, §R.3 |
| **Analysis report context table counts 7,195 lean NO TRADE rows as trades** (`EVAL-1`, `D-8`) | **(b) show lean rows as a separate labelled column** | same, §R.6.3 |
| **Card funding-momentum colour ignores the funding sign** (`DISP-1`, `D-9`) | **(b) render what the funding-momentum step actually did** | same, §R.6.3 |

**Consequence to keep in view:** the A4 liquidation × OFI flip gate was never market-gated; it is blocked by `L-1` (`trader-tick-queue.md` §1 row `E7`, `roadmap.md` W2 A4 row, both corrected).

### 2.2 S2 rotation additions

- **`RIDER-9`** (`VPFRSignal` + `VPFRPoc` columns), trader-ruled 2026-09-16, is TRAVELLING with S2 ([`csv-rotation-riders.md`](csv-rotation-riders.md)). The S2 commit must mark `RIDER-1` to `RIDER-7` and `RIDER-9` CONSUMED.
- **S2 deploy holds**, all recorded in the build spec's §0 deploy row: rider 2b merged (`T-7`) · the same-millisecond gap-repair fix (`GR-5` (b)) ✅ built · the cross-month fix (`F-1` (a)) ✅ built · the scan-failure fix (`DUP-1` (a)) ✅ built.

---

## 3. What this seat did (commits local unless noted)

| Arc | Outcome | Key commits / docs |
|---|---|---|
| **ASIA burst watch, second read** | **MISS** on fire rate (7.75 % vs band 8–14 %); a regime step at 2026-08-20. Re-derivation read owed (task 4b) | `e66da61`; brief `462b483` |
| **Venue check** | Trader ruled **option A:** run on the dev machine after each fetch, not on the box. Built: `venue_status.log` wiring into coverage, Deribit fetch pagination, venue verdicts and machine line, post-fetch hook, 21-column local ledger | Plan `c0fbd5e`; build `dc02219` · `6509a0f` · `9b6fd0b` · `a86fd08` |
| **Gap repair** | Same-millisecond page skip → **repair by `trade_seq` range** (`edd4539`); **cross-month leading gap** fixed (`165f780`); **scan failure** now loud, no lookback re-append, share mode ReadWrite (`fd94e0c`). The September duplicate rows were 3 failed-scan passes. All reviewed and merged (`346c6d1`, `cf0ec90`). **Not deployed** | Specs and spec-backs `docs/gap-repair-*` |
| **Swing vs ATR-fallback targets** | No session shows a separable swing advantage; in STRONG + MEDIUM fallback is at least as good; LONDON fallback sits at breakeven | `989b89c`, headline made precise `24a98ef` |
| **Tier-order stability (Part A)** | 3 STABLE, none significant. **Part B parked** until the tiers are fixed | `b2d30fc` |
| **Performance vocabulary** | `DeribitIndicatorProject.md` §5a + `trader-profile.md` §6 line | `26b345a` |
| **MEDIUM bug hunt, Session 1** | No scoring-vote bug. Re-score 99.31 % match, 0 unexplained. Mirror fixtures `A82a`–`A82c`. Found the POC-gate and liquidation defects (§2.1). Demotion is the TRANSITIONAL ADX penalty alone | `c49dc1f` · `58ac811` · `85e11d5` |
| **MEDIUM diagnosis, Session 2** | ⛔ **The score does not rank outcomes** (net EV flat against score share in every session, both halves). The MEDIUM gap does not replicate. **NY VPFR vote on longs is predictive (+5.0 bps, CONFIRMED)**; ASIA OFI +3.0 (carried) CONFIRMED | `3288d32`, `F-4` split `70a4483` |
| **Kelly placed-payoff proposal** | Kelly's payoff ratio should use the placed levels net of fees (today a fixed 1.094 gross). **Parked** for the trader's app-wide fee revamp | `fc49175` · `1594c14` |

---

## 4. Rulings from this seat, not yet built (index)

| Ruling | Meaning | Recorded in |
|---|---|---|
| `Q-1` = (d) first | Measure target/stop geometry by tier before deciding `F-1` and `F-4a` | [`medium-tier-diagnosis-spec-back.md`](medium-tier-diagnosis-spec-back.md), end section |
| `F-2` | Weight votes by measured value, only via a pre-registered, era-aware vote-value study with forward validation | same |
| `Q-2` / `F-3` | Retest the NY VPFR vote after the POC and liquidation fixes ship | same |
| `F-4a` | Flatten tiers for Kelly sizing only; gated on `Q-1` (d) | same |
| ⛔ `F-4b` | **REJECTED:** the payload `confidence` field stays untouched | same |
| Part B | Parked until the tiers are fixed | `trader-tick-queue.md` §2 |
| Kelly `K-1` to `K-4` | Parked for the fee revamp | [`kelly-placed-payoff-proposal.md`](kelly-placed-payoff-proposal.md) status line |
| `Q-3`, `D-10` | Orchestrator-scheduled tools fixes, low priority: quote the attribution CSV `Class` field; `OverlapValidator` OI labels | `trader-tick-queue.md` §2 |

---

## 5. ⭐ Lessons from this seat

| # | Lesson | Instance |
|---|---|---|
| 1 | **Docs called defects "by design", three times.** Read a "narrow by geometry" or "market-gated" claim as a hypothesis | The POC tier ("by design + geometry"); A4 ("market-gated only"); audit F9 ("plumbing verified") |
| 2 | **A mirror test cannot see a defect that inverts both sides.** Audit label consumers against the producer's actual emission, not against another consumer's comment | The POC gate passed the symmetric reasoning; the label-consumer audit caught it |
| 3 | **After a seat compacts, re-run its instruments before building on them.** Cheap and decisive | Session 1: re-score and census re-run identical; attribution MD5 unchanged |
| 4 | **An agent's headline can overstate its own table.** Check CIs and the tier split before relaying | "Swing does not beat fallback in any session" while ALL-tier point estimates favoured swing |
| 5 | **"The bridge" is two things:** the advisory `kelly` block and the `confidence` action key. Only the second moves orders | `F-4` had to be split into `F-4a` and `F-4b` |
| 6 | **Seat names change after restarts, and usage-limit stops kill background agents mid-task.** Use `ListAgents`; resume agents with an explicit "check what you left behind" | This seat was `-87`, `-a3`, `-b6`, then "Seat handover documentation" |
| 7 | **Two seats editing adjacent rows of the queue produce merge conflicts.** Take the branch version and re-insert the other row; check pipe counts | The scan-failure merge |
| 8 | **The fee-inclusive breakeven is the number that matters.** Every tier and target type sits below its net breakeven; fees are 20–60 % of a 1-minute ATR target | `DeribitIndicatorProject.md` §5a; swing read |

---

## 6. ⚠ What I did NOT verify

- The order app's live bridge consumer: the checkout at `C:\Users\user\source\repos\DeribitOrderPlacementApp` was last committed 2025-08-19 and has no bridge code. Bridge answers came from the engine-side contract mirror.
- Why the 100 ms trades stream lacks the liquidation flag.
- The label-consumer audit rows 43–57 and the side findings (reasoning-based; nothing to re-run).
- Collector health: `collector.ps1 status` not run this seat.
- The fetch that `D-1` and the ASIA read need has not been taken.
