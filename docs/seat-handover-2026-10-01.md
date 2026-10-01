# Seat handover — 2026-10-01 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.** Prior: [`seat-handover-2026-09-29.md`](seat-handover-2026-09-29.md), superseded for state.

⛔ **Run `date -u` first.** This seat ran 2026-09-29 19:52 → 2026-10-01 ~16:30 UTC. The workstation shows GMT+8.

⛔⛔ **The trader is away 2026-10-14 → 2026-11-25; the last working day is 2026-10-13.** The plan of record is the TRADER AWAY block at the top of the [`trader-tick-queue.md`](trader-tick-queue.md) state banner. **Remind the trader of its pre-holiday list at every seat start** (due from 2026-10-01).

**State at close:**
- `master` is **13 commits ahead of `origin`**: docs, tools and the history-store build, no engine scoring code. **The trader pushes; ask them to push before the backfill.**
- Settings **v69**, tracked and on the box.
- **Collector:** instance `e162060a-a52b-4705-949f-6708a46c9ff7`, started 2026-09-30 20:42 UTC. Running build `bb14dc8`, which carries the C-8 collector-halt fixes, deployed 2026-09-29. Healthy at the 20:48 UTC read-back.
- **The box can now recover by itself.** Watchdog task `DeribitCollectorWatchdog` installed; auto-logon on. Tested live 2026-09-30:
  - kill test: relaunched on the next tick;
  - reboot test: relaunched with no RDP;
  - two coexistence tests with `restart`: both passed.
  
  Ledger: [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5.
- **Fixtures:** 513 PASS, 1 SKIP (`A81b`, the known-defect fixture) at `6c87e0d`.
- **Nothing is running on the dev machine.** The history-host flag watcher finished 2026-09-30 17:30 UTC.

---

## 0. FIRST ACTIONS

| # | Action | Model + effort | Notes |
|---|---|---|---|
| **1** | `date -u` · `git status -sb` · read-back: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-readback.ps1 -InstanceId i-0d6c133058876273e` | seat, low | Check `WATCHDOG_TAIL` (a `HEARTBEAT` each hour), `RUNERR_TAIL`, `COMMIT_FREE_MB`. After the 2026-09-30 reboot the commit limit fell to 2,191 MB as the page file restarted small |
| **2** | ⭐ **Walk the trader through the 21-month history backfill** — [`history-store-backfill-runbook.md`](history-store-backfill-runbook.md). The trader starts it **now** (2026-10-02 local) | seat, medium | ⛔ **None of its AWS/SSM steps or its Linux binary has ever run.** Read each output the trader pastes. **The self-test step first:** it must print the SHA-256 the runbook lists. Measured: 71.8M trades, about 22.7 h, about 6 GB. Then the pull (step e1), the top-up (e2) and the comparison (e3). **Terminate the temporary instance only after e1 passes**, and only that instance id, never the collector. S3 deletes uploads after 7 days |
| **3** | ⭐ **Adversarial review of [`indicator-calibration-review-2026-10-02.md`](indicator-calibration-review-2026-10-02.md)** — the trader will ask for it once the backfill is running | Opus 5.5 or Fable 5.1, high | Its §8 lists six questions and its §9 the claims this seat carried without checking. Attack the order (§7) and the classes (§6). Write the review as a doc |
| **4** | **Rule `D-6`** with the trader: the stage-4 pass rule counts its 14 days between comparison **run dates** (strict) | Trader | This seat's read: strict. It costs nothing: comparisons ~10-04, ~10-08, 10-13 plus the first fetch after 11-25 span 52 days, so the rule can pass on ~11-25 either way. The trader asked to shorten it to 10 days; this seat pushed back because shortening gains nothing. **Not yet ruled** |
| **5** | Fetches with the history comparison: ~10-04, ~10-08, 10-13 (`collector.ps1 fetch`; its step 6 runs the comparison once the dev store exists at `C:\DeribitData\history\`) | seat, low | ⛔ `verdict=FAIL` stops the plan and goes to the trader |
| **6** | Dated reads: absorption Stage 1 (~10-08) · ASIA and NY burst-watch reads (~10-09, `tools/ops/burst-watch-read.ps1`) · final read-back and fetch on 10-13 | per row | Carried from their docs |
| **7** | The raw order-book absorption test: run it **only** if the pre-holiday list is done before the Stage 1 read, with time to spare; else after 2026-11-25 | Opus 5.5, high | Trader ruling 2026-10-01 |

⚠ **Agents (trader rule, 2026-09-30):** builds go to agents, at most 2 at once, 1 for a large build. **Ask the trader before every dispatch.**

---

## 1. What happened this seat

| Item | Result | Record |
|---|---|---|
| Adversarial audit (PR #3) | Merged locally (`b00b644`), triaged, rulings `AT-1`–`AT-6` | [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §6, §7a, §7b |
| Collector-halt fixes (audit decision C-8) | Built (`bcb8e56`), launch-tested, **deployed** 2026-09-29 20:51 UTC | [`collector-halt-fixes-spec-back.md`](collector-halt-fixes-spec-back.md) |
| `collector.ps1 restart` | Built, live-tested by the trader, allow rule added (dev machine only) | Deploy ledger |
| **Collector watchdog + auto-logon** | Spec → agent build → installed, all 4 live tests passed | [`collector-watchdog-spec.md`](collector-watchdog-spec.md); [`collector-watchdog-spec-back.md`](collector-watchdog-spec-back.md) |
| History-host liquidation-flag test | 7 of 7 flagged ~59.5 min late → **`D-4` re-ruled (a): park the vote and the alarm**, with the maker-side fix (`L-2`) as a rider | [`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) §00000; [`liquidation-park-spec.md`](liquidation-park-spec.md), `LP-1`–`LP-8` ruled; **build after 2026-11-25** |
| History store | Reshape rulings `HSR-1`–`HSR-13`; **stage 2 built** (`d9c77a2`), backfill pulled before the holiday | [`history-store-queue-reshape-evaluation.md`](history-store-queue-reshape-evaluation.md); [`history-data-store-build-spec-back.md`](history-data-store-build-spec-back.md) |
| Burst outcome read | Daylight-saving seam stratum built (`4de2a3b`); **run 2 per session**, and its data start moves to the first row after the daylight-saving session-hours deploy | [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md) §6.1, §6.2 |
| Rulings 2026-10-01 | Measurement first (C-9, C-10, with rows C14–C16) · row F5 (daylight-saving-aware session hours, before run 2) · ADX/RSI design-point re-anchor before the vote-value study (`F-2`) · net EV per session-day added to `DeribitIndicatorProject.md` §5a | `trader-tick-queue.md` §2 rows |

---

## 2. Open decisions and suggestions (not ruled)

- **`D-6`:** strict or loose counting for the stage-4 pass rule (§0 item 4).
- **This seat's suggestions in the review pack**, held until the adversarial review (§0 item 3):
  - queue audit items AUD-12, AUD-13 and AUD-19;
  - un-park the forming-bar decision so the volume-spike vote can fire (0.69 % NY fire rate, carried).

---

## 3. After 2026-11-25 (summary; full list in the queue banner)

In order:
1. The first fetch after return → comparison #4 → the stage-4 pass rule.
2. Burst outcome run 1, with the seam stratum, by 12-04.
3. Measurement fixes C-9/C-10 plus C14–C16.
4. The daylight-saving session hours (F5) spec and build.
5. Audit scoring fixes per `AT-6` (b), each after the independent review L-7.
6. The liquidation park build.
7. The ADX/RSI re-anchor, then the vote-value study (`F-2`).
8. Burst run 2, per session.
9. Stage 4 (switch off box capture; reserved), then the drop list.

---

## 4. Lessons — saved to memory before this handover

- [[feedback-builds-go-to-agents]] — ask before dispatch; at most 2 agents, 1 for a large build.
- [[reference-collector-readback-allowlisted]] — only the read-back is allow-listed; even a read-only `restart` plan was blocked.
- [[feedback-read-roadmap-before-sequencing]] — an audit triage is sequencing work; read `roadmap.md` first.

---

## 5. ⚠ What I did not verify

- Every AWS step of the backfill runbook (never run).
- The order app's cooloff value, which the session-day metric needs.
- The figures the review pack marks "carried" (its §9).
- The box's Windows Update reboot policy. The watchdog covers a reboot regardless.
