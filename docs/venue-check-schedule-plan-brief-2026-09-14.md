# Brief — plan the scheduled store-vs-Deribit trade check (`--verify-venue`)

**Written by:** the orchestrator seat of 2026-09-14b. **For:** a new, single-task conversation. **Source task:** `docs/seat-handover-2026-09-14b.md` §1, task 2 (detail in that doc's §1.2).

⛔ **This seat writes a PLAN. It does not create anything on the collector box.**

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, medium.** The design is settled (§2 below). The work is discovery (what is on the box, how files reach it) plus a precise plan. No derivation |
| **Where it will slip** | (1) Doing instead of planning: copying a binary, registering a task or editing the deploy manifest on the box. All reserved. (2) Window arithmetic: Deribit keeps about 24 h of public trades, so a task that runs late silently loses the start of its window. (3) Trusting the exit code: `--strict` grades `Defect` hours only and never reads the venue result |
| **Escalate / stop** | Stop and ask if the plan needs a change to the `collector.ps1 deploy` manifest or allowlist, a new credential, or anything outside the app root. Record it as a D-table row |
| **Allowed on the box** | Read-only checks only, such as `tools/ops/collector.ps1 status`. Read `collector.ps1` first to confirm the verb writes nothing. If unsure, do not run it; list the check as owed |
| **Scope** | Trader-directed scoped seat. Skip the full `CLAUDE.md` session-start reads. Read only §3. Run `date -u` first |

## 1. Why it is due

- The check was suspended 2026-08-08 until trade-store rows carried `trade_id`. `trade_id` and `trade_seq` were deployed to AWS 2026-08-10.
- No scheduled task exists. Each unscheduled day can never be verified later, because the venue history is gone after about 24 h.

## 2. Settled design (trader ruled AWS only)

- Runs on the AWS collector only.
- Output goes to a folder under the app root.
- The window ends on the previous whole hour, never "now". This clears the 30 s `trade_store.flush_seconds` lag.
- A background scheduled task that needs no interactive login.
- A person reads the result. Add a one-line verdict to the daily glance in `docs/aws-collector-deploy-checklist.md`.

## 3. Inputs

| File | What to get from it |
|---|---|
| `docs/seat-handover-2026-09-14b.md` §1.2 | The task as handed over |
| `docs/trader-tick-queue-archive.md` §D row `trim-2026-09-14b-22` and §C row `trim-2026-09-14-49` | The full original rows, including the withdrawn "16,459 more trades" figure (do not quote it) |
| `tools/BacktestRunner/BacktestProgram.vb`, argument parsing near lines 80–130 (`--to`, `--strict`, `--verify-venue`) | The exact command line and what `--strict` covers |
| `tools/ops/collector.ps1` — verbs `status`, `fetch`, `deploy`; the deploy manifest | Whether `BacktestRunner` ships today (a 2026-09-14 grep found no reference), and how a new file would reach the box |
| `docs/absorption-d2-stage1-rotation-build-spec.md` (grep for the deploy step only) | The one planned deploy (build step S2). The plan should ride that deploy rather than add a second one |

## 4. The plan must answer

| # | Question |
|---|---|
| P1 | Where does the `BacktestRunner` binary on the box come from, and which deploy carries it? |
| P2 | The exact command, with the window arithmetic written out: start, end, time zone, and the margin against the 24 h venue limit |
| P3 | The task definition: trigger time (UTC), run-as account, no-login setting, timeout, and what happens if the box is down at trigger time |
| P4 | Output: file naming (one file per day, so a missing file shows a missed run), location, retention |
| P5 | How the trader reads it: the exact line added to the daily glance, and how `collector.ps1 status` or `fetch` could surface it |
| P6 | The `--strict` gap, as a D-table. At least: (a) parse the venue section of the output · (b) extend `--strict` to fail on venue loss. ⚠ Lead with the more truthful option; a green exit code that cannot see venue loss is the silent-hole class this repo rejects |
| P7 | Failure modes: the task never ran · it ran but Deribit was unreachable · the store file was mid-rollover |

## 5. Deliverable

1. `docs/venue-check-schedule-plan.md` answering P1–P7, with a D-table for every reserved choice and your read on each row.
2. If `--strict` option (b) is your read, write a small spec section for it. **Do not build it** — it ships with the S2 deploy.
3. Commit locally, `[no-engine-change]`. Do not push.

## 6. Rules that bind this seat

- **Reserved:** any write to the collector box, deploy-manifest changes, `settings.json`.
- **Dense-doc edits:** Edit tool only.
- **Output format:** `C:\Users\user\.claude\CLAUDE.md` — point form, no bare section numbers or IDs, verified separated from carried.

## 7. Report back

At most 10 lines for the orchestrator: the P1 answer, the command, the task trigger, the D-table rows with your reads, which read-only checks ran on the box, commit hash.
