# Collector Ops Tooling — scripted deploy + copy-back over SSM, and auto-run on start

**Date:** 2026-08-21, trader-directed. **Origin:** SSM access was established on both collectors 2026-08-21; the interactive-launch proof-of-concept passed the same day (§2.1). **Class:** ONE small engine change (Part A) + ONE new ops script (Part B). **Part B touches no engine code at all.**

---

## 0. Model + effort — READ THIS FIRST

**Part A — auto-run on start: Sonnet, effort medium.**
**Part B — the ops script: Sonnet, effort high.**

**Why those tiers.** Part A is a settings key, a form-load branch and a fixture — every piece has an in-repo template, and the one hard question (does auto-start arm autotrade?) is already answered in §1.3. **Part B is mechanically simple PowerShell but its blast radius is a live collector holding unrecoverable tape**, which is what lifts it to high: being wrong is expensive and, in the worst case, silent.

**Where Sonnet will slip — four traps, named.**

- ⚠⚠ **TRAP 1 — backing up the wrong things.** The backup in §2.5 covers **only the six allowlist items**. A naive "back up the folder first" copies `backtest_data\` and doubles the tape on a box with finite disk, and copies `analysis_log.csv` — the two things [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §1 spends most of its words keeping apart.
- ⚠ **TRAP 2 — reusing the retired key name.** Part A's key is **`auto_run.start_engaged`**, NOT `auto_run.enabled`. The latter existed, was **never read**, and was deleted at v32. Reviving that exact name makes the version history unreadable and invites "wasn't this removed?" forever.
- ⚠ **TRAP 3 — verifying the deploy by reading back what you just wrote.** Comparing the copied file to the source proves the copy worked. **It does not prove the app runs.** The acceptance gate in §2.6 is a NEW CSV ROW appearing after restart — the only check that exercises the whole chain.
- ⚠ **TRAP 4 — assuming the launch lands in session 0.** It does not, and this is measured, not assumed (§2.1). Do not "fix" it by adding session-0 handling.

**Escalation trigger — stop and move up a tier if:**

- The restart verification in §2.6 fails and the rollback path has to run for real. **Stop. Do not iterate against a live collector.** Reproduce on the t2.micro test box.
- You find yourself wanting to widen the six-item allowlist. It is a positive record on purpose.
- Part A's overlay routing (§1.4) does not work as described — that means the overlay whitelist does not admit `auto_run.`, and the routing needs re-designing rather than forcing.

**Session split: two, sequenced by dependency.** Part A first — it is small, independently useful, and Part B's hands-off deploy is incomplete without it. Part B second.

---

## 1. Part A — auto-run on start

### 1.1 Why it is needed

**A scheduled-task launch produces a running app that is still stopped.** Auto-run has always started disengaged (v32 `D4` removed the dead `auto_run.enabled` key on the grounds that it was never read). So without this, every scripted deploy still ends with a human RDP-ing in to click **Start** — which defeats the point.

### 1.2 Mechanism

**ONE new key:**

```json
"auto_run": { "start_engaged": false }
```

- **Default `false` ⇒ byte-identical to today.** A box that does not set it behaves exactly as now.
- On form load, after the timer and controls are constructed, `If cfg.AutoRun.StartEngaged Then StartAutoRun()`.
- `StartAutoRun()` already reads `nudMinutes`/`nudSeconds` for the interval, so the seeded control values govern — **no second source of truth for the interval.**

### 1.3 ⚠ The safety property, verified before speccing

**Auto-starting the run loop CANNOT arm autotrade.** `chkArmAutotrade` is constructed at `UI/MainForm_Layout.vb:744` with no initial state read from settings, and `_autotradeArmed` is assigned from the checkbox (`UI/MainForm_SignalBridge.vb:39`). **ARM is off by construction on every start and is never persisted.** This is what makes Part A safe to default-on for collectors, and it must be stated in the commit message.

### 1.4 Routing — collectors on, dev box off

**Tracked `settings.json` sets `start_engaged: true`.** The dev box turns it back off through `settings.local.json` — **the same overlay that already carries `trade_store.enabled: false` for exactly the same reason.** One mechanism, one precedent, no new concept.

⚠ **Check the overlay whitelist admits `auto_run.` before building.** If it does not, that is a `D`-row for the trader, not a thing to force. It also keeps the deploy checklist's existing rule intact and gains meaning: *never copy `settings.local.json` to a collector* now protects capture **and** auto-start.

⚠ **`auto_run.` is already off the auto-tweaker surface** (the v45 class alongside `kelly.` / `exit_guard.` / `live_strip.`). Confirm the new key inherits that prefix reject; add nothing if it does.

### 1.5 Fixtures

- **A58a** — `start_engaged:false` (the default) leaves the load path byte-identical; no auto-run engagement.
- **A58b** — `start_engaged:true` engages auto-run on load, **and `chkArmAutotrade.Checked` is still False** — the §1.3 property, pinned rather than argued.
- **A58c** — the overlay routes it: base `true` + overlay `false` ⇒ disengaged, and the tweaker fence rejects `auto_run.start_engaged`.

**Settings version bump + `change_log` entry required.**

---

## 2. Part B — `tools/ops/collector.ps1`

Three verbs: **`status`**, **`fetch`**, **`deploy`**. PowerShell driving the AWS CLI, alongside the `tools/checks/verify-gate.ps1` precedent. **No engine code is touched.**

### 2.1 The launch mechanism — MEASURED, not assumed

**PoC run on the t2.micro test box, 2026-08-21:**

```
schtasks /create /tn <name> /tr <exe> /sc once /st 00:00 /ru administrator /it /f
schtasks /run /tn <name>
```

**Result: the process launched into SESSION 2 — the DISCONNECTED interactive session — while the SSM script itself ran in session 0.** The running engine (PID 4388, session 2) was untouched. Subject was `notepad.exe`, deliberately, so the live memory test was not disturbed.

⭐ **This is why the UI, and therefore the daily glance, survives a scripted deploy.** A session-0 launch would give an invisible app whose `MessageBox` error surfaces hang forever with nobody to dismiss them. **Do not go near session 0.**

### 2.2 Transport — S3, and it must self-clean

One bucket, same region as the instances.

- **No versioning.** Deletes would leave billable old versions.
- **Lifecycle rule: expire objects after 7 days, and abort incomplete multipart uploads after 1 day.** Without the second rule, failed uploads bill invisibly forever.
- EC2 → S3 in-region is free; storage at these sizes rounds to zero; an empty bucket costs nothing.
- Instance profiles need `s3:PutObject` + `s3:GetObject` scoped **to that bucket only**.

### 2.3 `status` — read-only, no S3 needed

Wraps what already exists: `ssm-mem.json` (memory + eviction), `ssm-apphealth.json` (rows/hour, cadence lag, store size, runtimes, session state). **This is the replacement for the daily RDP glance** and it is strictly better — it returns numbers rather than an impression, and it works on a disconnected box.

### 2.4 `fetch` — the copy-back, read-only on the box

Box → S3 → local. Targets: `analysis_log.csv`, `backtest_data\`, `ws_health.log`, sidecars.

- **Lands as `analysis_log_aws.csv` locally — NEVER overwriting the local book.** [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §3 step 1, unchanged.
- Prints row counts and date ranges both sides so a truncated transfer is visible immediately.
- ⚠ **Does not pool, dedup, or merge anything.** §3b's minute-key dedup stays a separate, deliberate step. Automating a transfer is not licence to automate an analysis decision.

### 2.5 `deploy` — the only verb that writes

**Ordered, and the order is the safety property.**

1. **Pre-flight, local.** Abort if the tree is unpushed or dirty. Abort if any of the six allowlist items is missing from `bin\Release\net8.0-windows\`. Read and display `settings.json` line 2.
2. **Pre-flight, remote.** SSM reachable; app present; report its current settings version and PID.
3. **Print the plan and ask.** Source tree + commit, target box, the six items with sizes and hashes, current-vs-incoming settings version, what will be stopped, where the backup goes. **Interactive y/n. Nothing has changed at this point.**
4. **Stop the app** (the `.exe`/`.dll` are locked while it runs).
5. ⚠⚠ **Back up ONLY the six allowlist items** to `<dir>\_deploy_backup\`, single generation, overwritten by the next deploy. **Never `backtest_data\`, never `analysis_log.csv`, never `settings.local.json`.**
6. **Upload → download → place** the six items. Nothing else. Ever.
7. **Verify by hash** against source.
8. **Restart** via the §2.1 scheduled task; delete the task afterwards.
9. **Verify for real** — see §2.6.

### 2.6 ⚠ Acceptance is a NEW CSV ROW, not a file comparison

A hash match proves the bytes arrived. **It proves nothing about whether the app runs.**

**The gate: within 5 minutes of restart, `analysis_log.csv` must gain a row with a timestamp later than the restart.** That single check exercises the launch, the interactive session, auto-run engagement, the WS connect, the seed, and the write path — everything the deploy could have broken.

**Also assert:** the process is in a **non-zero** session, and the reported settings version matches what was deployed.

**On failure: restore the six items from `_deploy_backup\`, restart, and re-run the same gate. Then stop and report — do not retry the deploy.**

### 2.7 Out of scope, named so they are not assumed

- **No pooling, dedup or analysis.** `fetch` moves bytes.
- **No settings editing on the box.** Settings travel as the tracked file or not at all.
- **No store manipulation.** `backtest_data\` is read-only to this tool, in both directions.
- **No Linux support yet.** SSM works on Linux; the paths and the launch mechanism do not. **The CLI port supersedes §2.1 entirely** — a headless service needs no interactive session, no scheduled task, and no Part A.

---

## 3. D-table — await trader

| # | Decision | Recommendation |
|---|---|---|
| **D-1** | Part A key named `auto_run.start_engaged`, default `false`, tracked `settings.json` sets **`true`**; dev box opts out via `settings.local.json` | **Yes** — §1.4. Reuses the `trade_store.enabled` overlay pattern exactly |
| **D-2** | Part A ships **before** Part B | **Yes.** Small, independently useful, and Part B is not hands-off without it |
| **D-3** | `deploy` prints the full plan then asks y/n; nothing changes before the answer | **Yes** — trader-chosen 2026-08-21 |
| **D-4** | Backup is **single-generation**, six items only, overwritten by the next deploy | **Yes** — trader-chosen 2026-08-21. ⚠ TRAP 1 is the whole risk here |
| **D-5** | Acceptance = a new CSV row within 5 min, not a hash match | **Yes** — §2.6 |
| **D-6** | Scope: `status` + `fetch` + `deploy` in one tool | **Yes** — trader-chosen 2026-08-21 |
| **D-7** | ⚠ **Does `deploy` target the t2.micro test box too, or production only?** | **Both, with the box named explicitly on every invocation and no default.** A tool that defaults to a target is a tool that deploys to the wrong one |
| **D-8** | ⚠ **S3 bucket name and region** — needs your answer; the region must match the instances | **Trader input required** |
| **D-9** | Should `fetch` run on a schedule (e.g. daily) rather than on demand? | **On demand only, this build.** A scheduled copy-back is a second thing to monitor, and §2.4 deliberately does not pool — an unattended transfer with no consumer earns nothing |

---

## 4. Acceptance

- Part A: five projects + OrderCheck build **0/0 Release**; A-series unregressed + `A58a`–`A58c`; `verify-gate.ps1 -Mode prepush` green; **settings version bumped + `change_log` entry**.
- Part A display-string parity: **no rendered line changes** — auto-run engagement uses the existing `UpdateCountdownLabel` path and the existing button state. **State this explicitly in the commit message.**
- Part B: **no engine build impact** (a `.ps1` outside every `.vbproj`). Prove `deploy` end-to-end **against the t2.micro test box first**, never production.
- ⛔ **Part B must never be run against production during its own build session.** First production use is trader-driven, after the test-box run is clean.
