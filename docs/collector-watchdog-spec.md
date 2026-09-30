# Collector watchdog on the box, plus auto-logon — SPEC

**Status:** written 2026-09-30 (UTC). Trader-ruled option A (queue pre-holiday row 6a in [`trader-tick-queue.md`](trader-tick-queue.md)). **Due installed and tested by 2026-10-10.** The build goes to one agent after the trader says go; installing on the box is the trader's.
**Why:** measured 2026-09-30 on `i-0d6c133058876273e`: no watchdog, no Startup shortcut, `AutoAdminLogon` empty ([`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §2). A crash, a hang or a Windows Update reboot stops collection until someone RDPs in, and during 2026-10-14 → 11-25 nobody will. Seats cannot help: they run on the dev machine, which is off.
**Class:** ops tooling. No engine code, no `settings.json` key, no scoring, no CSV or rendered value. **Writes to the live box are reserved:** the install, the uninstall and the live tests are the trader's.
**Decision IDs:** `WDG-1` to `WDG-8` (checked free across the repo 2026-09-30).

---

## 0. Implementer brief

- **Model: Opus 5.5 · Effort: medium · one agent, one session.**
- **Why that tier:** each piece has an in-repo template. The launch into the interactive session follows `Start-RemoteApp`, and the stop follows `Stop-RemoteApp` (both in `tools/ops/collector.ps1`). The install verb follows `Invoke-Restart`'s shape. What is hard is two races and one false-success, all named below.
- **Where it will slip:**
  1. **The deploy race.** `collector.ps1 deploy` stops the app for about 2 minutes. A watchdog tick in that gap sees no process and launches the OLD exe. That locks the files, so the deploy's copy fails or its relaunch refuses ("instance already running"). **The pause marker (§2.4) is not optional.** `deploy` and `restart` must write it before the stop and remove it after the gate, including on every failure path.
  2. **A launch that returns success and starts nothing.** On a box with no logged-on session, `schtasks /run` returns SUCCESS and produces zero processes (measured 2026-08-22, [`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md)). The watchdog must count processes after it launches, never trust the launch call.
  3. **A decision rule that cannot fail its test.** The rule (§2.2) is a pure function, tested offline with named failing inputs and one mutation per rule (`CLAUDE.md` fixture rules).
- **Escalation triggers:** anything that needs the watchdog to write inside the six-item deploy allowlist; any need to store a password in a file; any design where the watchdog runs as SYSTEM and still has to show a window.

---

## 1. Auto-logon — the trader, over RDP (do this first)

Without an interactive session, nothing can start a WinForms app after a reboot, the watchdog included.

1. RDP into the box as `administrator`.
2. Download Sysinternals **Autologon** (`Autologon64.exe`, from Microsoft Learn / Sysinternals).
3. Run it. Enter user `administrator`, domain = the box's computer name, and the password. Click **Enable**. It stores the password as an LSA secret, not in plain registry text.
4. **Disconnect** the RDP session. Do not sign out: signing out ends the session the app runs in.
5. Check from the dev machine: `collector-readback.ps1` should print `AUTOLOGON=1 USER=administrator`.

⚠ **Trade-off, stated so it is a choice:** whoever has administrator access to the box can recover a stored auto-logon password. The box already runs the collector as administrator, and its access is through AWS SSM with your credentials.

---

## 2. The watchdog

### 2.1 Where it lives and how it runs

| Item | Value |
|---|---|
| Script | `tools/ops/collector-watchdog.ps1` in the repo. Installed to `C:\DeribitEngine\watchdog\collector-watchdog.ps1`, **outside the deploy allowlist**, so a deploy never touches it |
| Scheduled task | `DeribitCollectorWatchdog`, **user `administrator`, logon type Interactive ("run only when user is logged on")** (`WDG-1`). Triggers: **at logon of `administrator`** (the boot path, with §1) + **every 10 min**, indefinitely. It does not start a new instance while the previous one is still running |
| Launch | Directly with `Start-Process` from inside the interactive session: the task already runs there. No nested `schtasks /it` task |
| Log | `C:\DeribitEngine\watchdog.log`, append-only, one line per event: `utc | event | detail` |

### 2.2 The decision rule — one pure function, `Get-WatchdogDecision`

**Inputs:** the process count; the last `analysis_log.csv` row's `Timestamp` (UTC, from the file's last line); the process's start time (UTC); now (UTC); the number of restarts in the last 24 h; the age of the pause marker (absent = none).

| # | Condition, first match wins | Decision |
|---|---|---|
| R1 | Pause marker present and younger than 45 min | `PAUSED`: do nothing |
| R2 | More than 1 process | `AMBIGUOUS`: do nothing, log it (the house rule: never guess which one is the app) |
| R3 | Restarts in the last 24 h ≥ 3 | `CAPPED`: do nothing; log `CAP_REACHED` once per 24 h window |
| R4 | 0 processes | `LAUNCH` |
| R5 | 1 process, started less than 10 min ago | `GRACE`: do nothing (the first rows take up to ~4 min on 3-min bars) |
| R6 | 1 process, and the last row is older than 30 min, or no row can be read | `RESTART` (stop, then launch) |
| R7 | Otherwise | `OK` |

**Constants** (`WDG-2`): the 30 min stale threshold · 10 min grace · 10 min tick · cap 3 per rolling 24 h · 45 min pause-marker expiry. They are ops constants at the top of the script, **not** `settings.json` keys: the watchdog is outside the app, and a settings file it depends on is one more thing that can break it. The 30 min matches trader ruling `AT-4`. It is well above the slowest normal gap: 3-min bars plus the 4-min feed-stall backstop.

**Why R3 exists:** a long Deribit outage makes every run skip, and a skipped run writes no row. Without a cap, the watchdog would restart a healthy app every 30 min for the whole outage. After 3 restarts it stops acting and says so.

### 2.3 Actions and what is logged

| Event | When |
|---|---|
| `START` | Every task start at logon (proves the boot path works) |
| `HEARTBEAT` | The first tick of each UTC hour, with the current decision and the last row's age (proves the watchdog is alive) |
| `LAUNCHED pid=… ` / `LAUNCH_FAILED` | After `LAUNCH`, **counting processes afterwards** (trap 2) |
| `RESTARTED old_pid=… new_pid=… last_row=…` / `RESTART_FAILED …` | After `RESTART`. The stop polls for zero processes, as `Stop-RemoteApp` does |
| `AMBIGUOUS count=…` · `CAP_REACHED` · `PAUSED` | As in §2.2. `PAUSED` is logged only when it starts and when it ends |
| `ERROR …` | Any exception in the tick. The script never throws out; the next tick runs anyway |

The restart count for R3 is **read back from `watchdog.log`**: the `LAUNCHED` and `RESTARTED` lines in the last 24 h. No separate state file (`WDG-3`): the log that the trader reads is the state the rule uses, so the two cannot disagree.

### 2.4 The pause marker — coexisting with `deploy` and `restart` (`WDG-4`)

- `collector.ps1 deploy` and `collector.ps1 restart` write `C:\DeribitEngine\watchdog.pause` before they stop the app, and delete it after the gate, **on success and on every failure and rollback path**.
- The watchdog ignores a marker older than 45 min. A crashed `deploy` then cannot disable the watchdog for good.
- A seat or a human doing manual work on the box writes the same marker.

### 2.5 Ops verbs and visibility

| Where | Change |
|---|---|
| `collector.ps1 install-watchdog -InstanceId … [-Yes]` | Copies the script to the box through the existing S3 path, registers the task (§2.1), and runs one tick. Without `-Yes`: plan only. Idempotent: re-install replaces the script and the task |
| `collector.ps1 uninstall-watchdog -InstanceId … [-Yes]` | Unregisters the task. It leaves the script and the log in place |
| `collector.ps1 deploy` / `restart` | Write and remove the pause marker (§2.4) |
| `collector.ps1 fetch` | Adds `watchdog.log` to `$FetchFiles` |
| `collector-readback.ps1` | Prints `WATCHDOG_TAIL` (the last 3 log lines) and the task's state. The `TASK` lines already list it. **Stays read-only** |

---

## 3. Decisions — auto-proceeded, one line each

| ID | Decision | Options | Picked | Why |
|---|---|---|---|---|
| `WDG-1` | How the task runs | (a) `administrator`, Interactive · (b) SYSTEM, launching through a nested `schtasks /it` task | **(a)** | The app needs the interactive session, and (a) runs in it. (b) adds a second launch layer and its own false-success mode. With auto-logon, (a) always has a session |
| `WDG-2` | Where the thresholds live | (a) script constants · (b) `settings.json` keys | **(a)** | The watchdog must work when the app or its settings are broken. Changing them is a re-install, which is deliberate |
| `WDG-3` | Restart-count state | (a) derived from `watchdog.log` · (b) a state file | **(a)** | One source; the record the trader reads is the record the rule uses |
| `WDG-4` | Deploy coexistence | (a) pause marker · (b) disable and re-enable the task from `deploy` | **(a)** | A marker expires by itself if `deploy` dies midway; a disabled task stays disabled |
| `WDG-5` | What "stale" reads | (a) the last row's `Timestamp` · (b) the file's LastWriteTime | **(a)** | (a) is the fact that matters. (b) moves on a header rotation or any non-row write |
| `WDG-6` | A hung process with one instance | (a) kill and relaunch (R6) · (b) only log | **(a)** | A hang is the 2026-09-21 WPAD failure shape. It would sit for weeks |
| `WDG-7` | A notification when it acts | (a) none; the log only · (b) SNS email | **(a)**, for now | Email needs AWS setup for a trader who is away. The log is read at the first seat after 2026-11-25. **Revisit** if the trader wants to hear about restarts while away |
| `WDG-8` | Windows Update reboots | (a) leave the policy; rely on auto-logon + the at-logon trigger · (b) change the update policy | **(a)** | (b) is a box-configuration change outside this spec. The boot path covers a reboot |

---

## 4. Tests

### 4.1 Offline (the build, no box)

A test script, `tools/ops/collector-watchdog.tests.ps1`, runs `Get-WatchdogDecision` over a table of cases. It prints `PASS`/`FAIL` per case and exits non-zero on any failure. One case per rule, each naming the input that fails it:

| Case | Input | Expect | Mutation that must fail it |
|---|---|---|---|
| T1 | marker 20 min old, 0 processes | `PAUSED` | Drop R1 |
| T2 | marker 60 min old, 0 processes | `LAUNCH` | Remove the 45 min expiry |
| T3 | 2 processes | `AMBIGUOUS` | Let R4/R6 run on a count > 1 |
| T4 | 0 processes, 3 restarts in 24 h | `CAPPED` | Cap at 4 |
| T5 | 0 processes, 2 restarts in 24 h | `LAUNCH` | Cap at 2 |
| T6 | 1 process started 5 min ago, last row 40 min old | `GRACE` | Drop R5 |
| T7 | 1 process started 2 h ago, last row 31 min old | `RESTART` | Threshold 35 min |
| T8 | 1 process started 2 h ago, last row 29 min old | `OK` | Threshold 25 min |
| T9 | 1 process started 2 h ago, the CSV unreadable | `RESTART` | Treat unreadable as fresh |

Plus a log-parse test: 24 h counting from a sample `watchdog.log`, with a restart at 23 h 59 min (counted) and one at 24 h 1 min (not counted).

### 4.2 Live (the trader's go, before 2026-10-10)

1. Auto-logon (§1), then `install-watchdog` (plan only, then `-Yes`). The read-back shows the task `Ready` and one `HEARTBEAT` or `START` line.
2. **Kill test:** stop the app with `collector.ps1`'s stop through a pause-free path. The simplest: from RDP, end the process in Task Manager. Within 10 min, expect `LAUNCHED` and new rows (a new `InstanceId` in the read-back).
3. **Deploy-coexistence test:** a `collector.ps1 restart -Yes` while the watchdog is installed must pass its gate, with `PAUSED` lines and no `LAUNCHED` during it.
4. **Optional reboot test** (a few minutes of gap): reboot the box from the EC2 console. Expect `START`, then `LAUNCHED`, then rows, with no RDP.

---

## 5. Acceptance

- The offline test script: all `PASS`, and each mutation in §4.1 seen `FAIL`.
- `collector.ps1` and `collector-readback.ps1` parse clean (0 errors). `collector-readback.ps1` stays read-only.
- The live tests §4.2 steps 1–3 pass (the trader's), and step 4 if the trader chooses it.
- A spec-back with the offline handle pasted, per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).

---

## 6. What this spec does not cover, and did not verify

- **Not covered:** hangs inside a run that still write rows (the rows would be wrong but present); anything about the order app; an SNS notification (`WDG-7`).
- **Not verified:**
  - That a task with logon type Interactive and no stored password registers cleanly through SSM on this box's Windows Server 2019.
  - That `Start-Process` from that task lands in the same session the app normally uses.

  Both are exactly what live test §4.2 step 1 and step 2 prove.
