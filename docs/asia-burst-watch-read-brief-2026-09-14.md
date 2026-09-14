# Brief — ASIA aggressor-velocity burst-watch read, second read (2026-09-14 UTC)

**Written by:** the orchestrator seat of 2026-09-14b. **For:** a new, single-task conversation. **Source task:** `docs/seat-handover-2026-09-14b.md` §1, task 1 (detail in that doc's §1.1).

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, medium.** The method is settled and has a worked template (`docs/d3-asia-burst-watch-read-2026-08-10.md` §1). The work is counting and segmentation, not derivation |
| **Escalate to high** | The pooled fire rate lands outside **8–14 %**; OR same-side share is below **85 %**; OR fewer than **10** weekday session-days qualify; OR an InstanceId in the window is missing from the deploy ledger. Any of these turns the read into a derivation |
| **Where it will slip** | (1) Counting only the live `analysis_log.csv` and missing the `.bak`. (2) Filtering on one `InstanceId`. (3) Using GMT+8 day-of-week instead of UTC. (4) Quoting the derivation's 9.7 % or its "~106 AggrVel rows/day". (5) Opening the wrong §5a — see §3 below |
| **Scope** | **Trader-directed scoped seat.** Skip the full `CLAUDE.md` session-start reads. Read only the files in §2. Run `date -u` first |

## 1. The question

Settings v65 armed the ASIA session's aggressor-velocity burst modifier: `indicators.aggressor_velocity.sessions.ASIA.burst_ratio_threshold` = 5.5, a live scoring change from 2026-08-02. The first watch read passed on 2026-08-11. **No read exists since.** Is the watch still in tolerance?

**Ruled tolerance** (trader, 2026-08-11, recorded as T-1 to T-5 in `docs/d3-asia-burst-watch-read-2026-08-10.md` §10):

| Criterion | Value |
|---|---|
| Fire-rate reference | 11.0 % |
| Fire-rate band | 8–14 % |
| Read length | ≥ 10 weekday session-days |
| Same-side TFI share | ≥ 85 % |
| On a miss | A re-derivation READ. **Never** a threshold change |

## 2. Inputs

| File | Role |
|---|---|
| `docs/d3-asia-burst-watch-read-2026-08-10.md` | **The template.** Its §1 is the population rule and method (weekday only, UTC day-of-week, ASIA session, `ExecResolution` = 3). Its §5 has the self-check. Its §7 and §8 are the InstanceId and downtime traps. Reuse its structure and its like-for-like comparison (§3) |
| `aws_fetch/20260913-153704/analysis_log.csv.v0.7.bak` | Rows up to the book rotation at 2026-09-01 15:49 UTC |
| `aws_fetch/20260913-153704/analysis_log.csv` | Rows from 2026-09-01 15:50:01 to 2026-09-13 15:36:07 UTC. **No overlap with the `.bak`. Count both** |
| `docs/aws-collector-deploy-checklist.md` §5a at **line 255** ("Version ↔ InstanceId ledger") | Maps each InstanceId to a build |
| Tracked `settings.json` `session_volume.sessions` | ASIA session hours, if §1 of the template does not state them |

**Columns** (same index in both files, checked 2026-09-14): `Timestamp` (1, UTC) · `ExecResolution` (94) · `AggrVelBurstRatio` (96) · `AggrVelNet` (97) · `AggrVelSignal` (98) · `TFIValue` (99) · `TFISignal` (100) · `InstanceId` (110). There is no session column; derive the session from the UTC hour.

⚠ Do not read the root `analysis_log_aws.csv`. `collector.ps1 fetch` overwrites it. Read the dated folder only.

## 3. Traps

| Trap | Handling |
|---|---|
| **Many InstanceIds** | v65 and every later build span several process restarts. Do not filter on one id. Check the arming instead: armed ASIA rows show `AggrVelSignal = BURST_*` exactly when `AggrVelBurstRatio ≥ 5.5` (template §5). Report any violation |
| **Two headings numbered §5a** in `docs/aws-collector-deploy-checklist.md` | The ledger is line 255. Line 324 is the v64 deploy record |
| **Window** | The copy ends Sunday 2026-09-13, so the last weekday ASIA session is Friday 2026-09-11. Report two slices: (a) all post-v65 weekday session-days, and (b) days after the first read's window only. State which slice the verdict rests on |
| **Partial days** | Exclude a session-day with a capture hole, as template §8 did. List every exclusion with its reason |
| **Tooling** | `python3` on this box is the Microsoft Store stub. Use PowerShell. The `.bak` is 31 MB; stream it rather than loading it whole |

## 4. Deliverable

1. **Instrument:** commit the counting script at `tools/ops/asia-burst-watch-read.ps1`, parameterised on the fetch folder, following `tools/ops/kelly-trigger-read.ps1`. A reviewer must be able to re-run it. Run it and paste its real output into the read doc.
2. **Read doc:** `docs/d3-asia-burst-watch-read-2026-09-14.md`. Verdict first, then per-session-day table, pooled band, like-for-like comparison, traps addressed, what was not verified.
3. **In the same commit:**
   - `docs/trader-tick-queue.md` §4: replace the "overdue" sentence with the verdict and the next due date.
   - `docs/DeribitIndicatorProject.md` §12, the row "v52 aggressor-velocity wire-in post-ship watch (S5.2)": add a one-line pointer to the new read.
4. **Commit locally. Do not push.** Tag the message `[no-engine-change]`.

## 5. Rules that bind this seat

- **Reserved (ask the trader):** any `settings.json` change, including the threshold. A miss means a re-derivation read, not a change.
- **Edits to dense docs:** use the Edit tool. No scripted in-place edits (they have mangled UTF-8 markers and inserted control bytes before).
- **Output format:** follow `C:\Users\user\.claude\CLAUDE.md` — point form, no bare section numbers, no bare IDs, verified separated from carried.

## 6. Report back

End with a report of **10 lines or fewer** that the trader can paste to the orchestrator: verdict (PASS / MISS), fire rate and same-side share per slice, session-day count, exclusions, commit hash, and anything that needs a trader decision.
