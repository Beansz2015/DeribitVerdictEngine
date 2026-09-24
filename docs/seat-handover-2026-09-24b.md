# Seat handover — 2026-09-24b (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-24.md`](seat-handover-2026-09-24.md), superseded for state. Its §2 (the trader's Jev rulings and the armed-harness table) and §3 (lessons) still bind.

⛔ **Run `date -u` first.** This seat ran 2026-09-24 13:19 → about 18:35 UTC. The workstation shows GMT+8.

⭐ **UPDATE 2026-09-24 18:50 UTC: THE TRADER PUSHED, AND THE ONE DEPLOY IS DONE.** New instance `25951567-9721-4644-86e4-b486840dfbf4` from 18:46:06 UTC, commit `55788bb`, all post-deploy checks passed (ledger row in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5). **§0 action 2 is DONE; the "unpushed" and "deploy HELD" lines below are history.** Still owed from the deploy: a second `fetch` showing two `DISCOVERED_BAK=` lines. Post-deploy watch: the flagged absorption rate rising while the ratio shifts left means STOP; the Stage 1 read after about two weekday-weeks.

**State at close (before the update above):**
- ⛔ **`master` is 25 commits AHEAD of `origin`, UNPUSHED.** They include engine code (engine-fix A and C, absorption S1 and S2). The trader pushes after compiling and testing.
- Settings **v68**, untouched.
- Fixtures **468 PASS, `ALL PASS`**; the only SKIP is `A81b` (engine-fix B2's fixture). Re-run by this seat.
- ⛔ **THE DEPLOY IS HELD by the trader.** Nothing new is on the box except the probe.
- ⭐ **A liquidation probe runs ON THE AWS COLLECTOR BOX** (run 4). Check it at every seat start (§2).
- The account was at 94 % of its usage limit at close. **Run at most one heavy agent**; the trader allows two agents at most, more only with a go-ahead.

---

## 0. FIRST ACTIONS

| # | Action | Model + effort | Why |
|---|---|---|---|
| **1** | Check probe run 4 is alive and read its pairing count (§2) | seat, low | It is the only thing gating engine-fix B2 |
| **2** | ⏸ **The ONE deploy — only when the trader lifts the hold.** Plan: [`absorption-d2-s2-batch-summary.md`](absorption-d2-s2-batch-summary.md) §5. Before it: a live `collector.ps1 fetch` must print `DISCOVERED_BAK=` (trap `T-7`). Carries `a6b33fe`, `ea32818`, `549b2c3`, `5dfc91a` (+ `526ecf2`, no boundary). Ledger sentence: TWO boundaries for the engine-fix build, because B2 misses this deploy | seat, high | Reserved to the trader |
| **3** | Engine-fix **B2** (`D-4`, `D-5`, `D-6`) when the probe pairs a liquidation. Brief: [`engine-fix-sessions-a-c-b2-brief.md`](engine-fix-sessions-a-c-b2-brief.md) step 4; rebase first | Opus 5.5, high | ⚠ The brief's "stop by 2026-09-28" date came from the discharged gap-repair deadline and no longer binds. B2 takes its own later boundary |
| **4** | Small spec: Kelly one-class sizing (`QD-1` (c) + `QD-2` measured p), **paired with** the parked `K-1`–`K-4` payoff proposal ([`kelly-placed-payoff-proposal.md`](kelly-placed-payoff-proposal.md)), trader-ruled 2026-09-24 | Opus 5.5, high (spec) | Display-only; the Kelly block goes silent. Reserved rendered value |
| **5** | Small spec: gap-repair `RR-1` fix ([`gap-repair-repeat-fills-read-2026-09-25.md`](gap-repair-repeat-fills-read-2026-09-25.md)). ⚠ A pure-`TradeSeq` sort must handle legacy rows with no seq | Sonnet 5, medium (spec) | Tape is complete; cost is 19 duplicate rows and 5 extra fetches per ~2.5 days |

---

## 1. What happened this seat

| Item | Result | Record |
|---|---|---|
| Engine-fix Session A (`D-1` POC gate, `D-2` manual) | BUILT `a6b33fe`. `H-3` reproduced 143 / 1,288 exactly. Fixtures 425 → 435 | [`engine-fix-session-a-spec-back.md`](engine-fix-session-a-spec-back.md) |
| Engine-fix Session C (`D-8` lean column, `D-9` Step 3b effect, both surfaces) | BUILT `ea32818`. Fixtures → 453 | [`engine-fix-session-c-spec-back.md`](engine-fix-session-c-spec-back.md) |
| Absorption S1 (`D-2` episode-cumulative pressing, Stage 1 instrument) | BUILT `549b2c3`. Fixtures → 461 | [`absorption-d2-s1-spec-back.md`](absorption-d2-s1-spec-back.md) |
| Absorption S2 (header 116 → 124, eight riders) | BUILT `5dfc91a`. Fixtures → 468 | [`absorption-d2-s2-spec-back.md`](absorption-d2-s2-spec-back.md) |
| Harness 1 (rider-travel) first real run | 8 of 8 agree with the seat baseline; weak evidence (easy items, declared recognition) | [`harness-runs/rider-travel-first-run-2026-09-24.md`](harness-runs/rider-travel-first-run-2026-09-24.md) |
| Gap-repair repeat fills | Cause MEASURED: `RR-1`, a timestamp-inverted seq bracket makes a phantom hole | [`gap-repair-repeat-fills-read-2026-09-25.md`](gap-repair-repeat-fills-read-2026-09-25.md) |
| `Q-1` (d), tier geometry | The top-tier success-rate rise is a payoff shift plus fewer timeouts; no tier outcome gap CONFIRMED | [`q1d-tier-geometry-read-2026-09-24.md`](q1d-tier-geometry-read-2026-09-24.md) |
| Stale queue rows | Closed or corrected in `trader-tick-queue.md` | commits `7711e74` onward |

**Trader rulings this seat (all recorded in their spec-backs):** `Q-1`/`Q-C1`/`Q-C2` of engine-fix A and C · absorption S1 `Q-1`–`Q-3` and S2 `Q-1`–`Q-5`, all as read · `QD-1` (a)+(c), `QD-2` measured p, and pair the Kelly change with `K-1`–`K-4` · the probe runs on the collector box, guarded · the deploy is held.

---

## 2. ⭐ The liquidation probe (engine-fix B1) — on the collector box

Record: [`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) §0 to §000.

| Item | Value |
|---|---|
| Box | `i-0d6c133058876273e` (1 GB RAM, 1 vCPU) |
| Directory | `C:\probe-runs\liq-2026-09-24\` |
| Run 4 | started 2026-09-24 16:53 UTC, PID 11248 (`probe.pid`), BelowNormal, `DOTNET_GCHeapHardLimit` 200 MB, built from `eff6def` |
| At +45 min | 257 REST polls, 1 REST error survived, probe 38 MB private, commit free 1,625 MB, collector writing every minute |
| Pairings | 0 at handover (no liquidation had passed) |
| Trader's guard | keep it running unless memory is predicted to almost run out before it completes |

**Check:** an SSM command that tails `C:\probe-runs\liq-2026-09-24\probe-console.log` (a status line every 5 min, with `rest polls`, `rest errors`, `index`, `priv MB`, `paired`). The scratch script this seat used is not committed; the `status` verb of `tools/ops/collector.ps1` does not read the probe.
**Stop:** SSM `New-Item C:\probe-runs\liq-2026-09-24\STOP` (summary within 30 s).
**History:** run 1 died unnoticed on 2026-09-21; run 3 froze its REST arm on an `HttpClient` timeout (fixed `eff6def`).

---

## 3. Lessons — each one cost something this seat

| Lesson | Where it bit |
|---|---|
| **A long-running process needs a liveness check at every seat start.** A handover's "nothing is running" did not name a dead probe | Probe run 1 died after 110 minutes and three handovers missed it |
| **`HttpClient.Timeout` is an `OperationCanceledException`.** A `Catch ex As OperationCanceledException` meant for a stop request also swallows a timeout | Probe run 3's REST arm ended silently on the slow box |
| **Size an in-memory cap in BYTES against the target host, not in entries** | `IndexCap` 400,000 was ~370 MB, more than the collector box's free RAM |
| **A `Get-ChildItem` length on a file held open by another process can read 0** | The probe's raw dump looked empty while it held 64 MB |
| **A fix commit that claims "every client" should be grepped for the class** | `584c616` (the WPAD fix) missed the probe's two clients |

---

## 4. ⚠ What I did NOT verify

- Whether the order app shows or uses the payload's advisory `kelly` block (another repo).
- The absorption UI plumbing (trigger mode, WS health, load error, trade count, shadow column): review only, no app run.
- Whether the box's paging (~400 `pagesOUT/s` with the probe) differs from a probe-free baseline at the same hour.
- The probe's raw-dump retention path (a rotation takes ~5 h; reviewed by reading).
