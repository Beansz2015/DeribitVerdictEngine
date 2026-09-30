# Collector watchdog — SPEC-BACK

**Build of:** [`collector-watchdog-spec.md`](collector-watchdog-spec.md) (the spec below; its §0 brief: Opus 5.5, medium, one session).
**Date:** 2026-10-01 (UTC). **Commits:** `105fcef` (code, tests, `collector.ps1`, read-back) and the commit that adds this file (the offline verb smoke harness + docs). **Local only: not pushed, not installed, no box contacted.**
**Class:** ops tooling. No engine `.vb`, no `settings.json`, no CSV header, no scoring, no rendered value. Nothing here is in a reserved class (`CLAUDE.md` "AUTO-PROCEED" table).
**Format:** per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). `H-n` = a handle the reader can run. `E-n` = build-time evidence the reader cannot re-run as-is.

---

## 1. Outcome

| Item (`collector-watchdog-spec.md` section) | File | State |
|---|---|---|
| Watchdog: `Get-WatchdogDecision` rules R1–R7 (§2.2), constants at the top (`WDG-2`), tick events (§2.3), restart count from the log (`WDG-3`), pause marker (§2.4) | `tools/ops/collector-watchdog.ps1` | ✅ built. Paths are parameters with box defaults. Dot-sourcing runs no tick |
| Offline tests T1–T9 + the 24 h log edge (§4.1) | `tools/ops/collector-watchdog.tests.ps1` | ✅ **21 PASS, 0 FAIL** (`H-1`). Adds `C1`–`C2` (last-row reader) and `K1`–`K6` (whole ticks against a hidden stand-in process) |
| Mutations, one per case (§4.1, `CLAUDE.md` fixture rules) | — | ✅ **17 of 17 red** on their named case (§3 below) |
| `install-watchdog` / `uninstall-watchdog` verbs (§2.5) | `tools/ops/collector.ps1` | ✅ built. Plan-only without `-Yes`. Smoke-tested offline against an `aws` stub (`H-3`) |
| `deploy` and `restart` write `watchdog.pause` before the stop, remove it after the gate on every path (§2.4, `WDG-4`) | `tools/ops/collector.ps1` | ✅ built as a `try/finally`. `restart` smoke-tested on success and failure arms (`H-3` S6–S8). `deploy`: read, not executed (§6) |
| `watchdog.log` in `$FetchFiles` (§2.5) | `tools/ops/collector.ps1` | ✅ with a comment |
| `WATCHDOG_TAIL` + task state in the read-back, still read-only (§2.5) | `tools/ops/collector-readback.ps1` | ✅ also prints the pause marker age. Read-only proof in `H-4` |
| Parse check, 0 errors (§5) | all five `tools/ops/collector*.ps1` | ✅ `H-2` |
| Offline verb smoke harness (not in the spec) | `tools/ops/collector-watchdog-verbs.smoke.ps1` | ✅ **12 PASS**; 3 mutations red. Added because it is the only offline cover for the verbs |

**Escalation triggers** (`collector-watchdog-spec.md` §0): none hit. The watchdog writes only `C:\DeribitEngine\watchdog.log` (outside the six-item deploy allowlist). No password is stored anywhere. The task runs as `administrator`, Interactive, not as SYSTEM.

---

## 2. Handles — run at `105fcef`, output pasted

### `H-1` — the offline tests (the one to run)

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-watchdog.tests.ps1
```

Takes about 30 s. It starts and kills a hidden copy of `ping.exe` named `WdTestApp.exe` in a temp folder, and deletes the folder after.

```
--- decision rule (Get-WatchdogDecision), spec section 4.1 ---
PASS  T1   marker 20 min old, 0 processes -> PAUSED (fails if R1 is dropped)  (got PAUSED)
PASS  T2   marker 60 min old, 0 processes -> LAUNCH (fails if the 45 min expiry is removed)  (got LAUNCH)
PASS  T3   2 processes (started 2 h ago, last row 40 min old) -> AMBIGUOUS (fails if R4/R6 run on a count > 1)  (got AMBIGUOUS)
PASS  T4   0 processes, 3 restarts in 24 h -> CAPPED (fails at cap 4)  (got CAPPED)
PASS  T5   0 processes, 2 restarts in 24 h -> LAUNCH (fails at cap 2)  (got LAUNCH)
PASS  T6   1 process started 5 min ago, last row 40 min old -> GRACE (fails if R5 is dropped)  (got GRACE)
PASS  T7   1 process started 2 h ago, last row 31 min old -> RESTART (fails at threshold 35)  (got RESTART)
PASS  T8   1 process started 2 h ago, last row 29 min old -> OK (fails at threshold 25)  (got OK)
PASS  T9   1 process started 2 h ago, CSV unreadable -> RESTART (fails if unreadable reads as fresh)  (got RESTART)
--- restart count read back from watchdog.log (WDG-3) ---
PASS  L1   restarts in 24 h: 23h59m counted, 24h01m not, *_FAILED and mentions not (fails at a 25 h window or a LAUNCH* prefix match)  (got 2)
--- last analysis_log.csv row (WDG-5) ---
PASS  C1   torn last line skipped, newest parseable row returned (fails if only the last line is parsed)  (got 2026-10-01T11:53:00Z)
PASS  C2   header-only file reads as no row (-> R6 RESTART)  (got NONE)
--- whole ticks against the stand-in process WdTestApp.exe (hidden ping) ---
PASS  K1   tick with 0 processes -> LAUNCH; LAUNCHED logged with the PID of the one process now running  (got LAUNCH|1|1|pid=29936)
PASS  K1b  first tick also logs START and HEARTBEAT exactly once  (got 1|1)
PASS  K2   stale row -> RESTART; old pid gone, one new pid, RESTARTED names both  (got RESTART|1|True|old_pid=29936 new_pid=26240)
PASS  K1c  START and HEARTBEAT not repeated on a later tick in the same logon and hour  (got 1|1)
PASS  K3   marker + 0 processes, two ticks -> PAUSED twice, PAUSED start logged ONCE, nothing launched  (got PAUSED|PAUSED|1|0|1)
PASS  K3b  marker removed -> PAUSED end logged, then LAUNCH  (got LAUNCH|1|2)
PASS  K4   3 restarts in the log, 0 processes, two ticks -> CAPPED twice, CAP_REACHED once, nothing launched  (got CAPPED|CAPPED|1|0)
PASS  K5   launch whose process exits at once -> LAUNCH_FAILED count=0, no LAUNCHED (fails if the launch call is trusted)  (got LAUNCH|1|0)
PASS  K6   exception inside a tick -> returns ERROR, logs one ERROR line, does not throw  (got ERROR|1)

RESULT  21 PASS  0 FAIL
EXIT=0
```

⚠ `K1b` and `K1c` count `HEARTBEAT` lines. A run that crosses a UTC hour boundary logs a second `HEARTBEAT` and fails them. Re-run; it is not a defect.

### `H-2` — parse check

```
Get-ChildItem tools/ops/collector*.ps1 | ForEach-Object { $e = $null; [void][System.Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$null, [ref]$e); "PARSE {0} errors={1}" -f $_.Name, $e.Count }
```
```
PARSE collector-readback.ps1 errors=0
PARSE collector-watchdog-verbs.smoke.ps1 errors=0
PARSE collector-watchdog.ps1 errors=0
PARSE collector-watchdog.tests.ps1 errors=0
PARSE collector.ps1 errors=0
```

### `H-3` — the verbs, offline, against an `aws` stub

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-watchdog-verbs.smoke.ps1
```

A global `aws` **function** shadows `aws.exe` (a function wins PowerShell command resolution). The script refuses to run if `aws` does not resolve to the stub. Every remote script the verbs would send is parsed; an unparseable one counts as bad. It needs `collector-watchdog.ps1` committed, because `install-watchdog` refuses an uncommitted script. About 2 min (the real `Invoke-RemotePs` sleeps 3 s per call).

```
PASS  S1   install-watchdog, no -Yes -> exit 1, pre-flight only, no upload  (got 1|INSTALL_PRE|0)
PASS  S2   install-watchdog -Yes -> exit 0; pre, upload, place, register, tick; every remote script parses  (got 0|INSTALL_PRE,S3_UPLOAD,INSTALL_PLACE,INSTALL_REGISTER,INSTALL_TICK|0)
PASS  S2b  the register script names Interactive, at-logon administrator, IgnoreNew, 10 min, no duration cap  (got True)
PASS  S2c  the tick runs THROUGH the task (Start-ScheduledTask), never the script from SSM (session 0)  (got True|False)
PASS  S3   register read-back shows a 1-day repetition -> exit 2 (not indefinite)  (got 2)
PASS  S4   the tick ran with a non-zero result (no session) -> exit 2  (got 2)
PASS  S5   restart, no -Yes -> exit 1, no pause marker, no stop  (got 1|RESTART_PRE)
PASS  S6   restart -Yes -> exit 0; marker BEFORE the stop, removed AFTER the gate  (got 0|RESTART_PRE,PAUSE_SET,STOP,LAUNCH,GATE,PAUSE_CLEAR|0)
PASS  S7   restart -Yes, relaunch fails -> exit 2 AND the marker is still removed (finally)  (got 2|RESTART_PRE,PAUSE_SET,STOP,LAUNCH,PAUSE_CLEAR)
PASS  S8   restart -Yes, marker not confirmed -> exit 1, the app is NOT stopped  (got 1|RESTART_PRE,PAUSE_SET)
PASS  S9   uninstall-watchdog, no -Yes -> exit 1, nothing unregistered  (got 1|UNINSTALL_PRE)
PASS  S10  uninstall-watchdog -Yes -> exit 0, unregistered and confirmed  (got 0|UNINSTALL_PRE,UNINSTALL|0)

RESULT  12 PASS  0 FAIL
EXIT=0
```

### `H-4` — the read-back addition is read-only

Parses the new remote command out of `collector-readback.ps1` and lists every command it calls:

```
$line = (Get-Content tools/ops/collector-readback.ps1 | Where-Object { $_ -like " '''WATCHDOG_TAIL*" }).Trim().TrimEnd(',')
$remote = ([System.Management.Automation.Language.Parser]::ParseInput($line, [ref]$null, [ref]$null)).EndBlock.Statements[0].PipelineElements[0].Expression.Value
([System.Management.Automation.Language.Parser]::ParseInput($remote, [ref]$null, [ref]$null)).FindAll({ $args[0] -is [System.Management.Automation.Language.CommandAst] }, $true) | ForEach-Object { $_.GetCommandName() } | Sort-Object -Unique
```
```
ForEach-Object
Get-Content
Get-Date
Get-Item
Get-ScheduledTask
Get-ScheduledTaskInfo
Test-Path
```

No verb in that list writes.

### `E-1` — the watchdog mutations (build-time evidence)

Instrument: a scratchpad script, not committed. It copied `collector-watchdog.ps1` and its tests to a scratch folder, applied one string replacement (checked to match **exactly once**), ran the tests, and recorded which cases failed. The tracked file was never edited; its SHA-256 was the same before and after (`tracked file unchanged: True`). The table in §3 is re-runnable by hand: apply the edit, run `H-1`, undo.

---

## 3. Mutations — one per case, each seen red

### 3.1 `Get-WatchdogDecision` and the tick (`E-1`)

| Mutation | Edit to `collector-watchdog.ps1` | Named case | Result | All cases that failed |
|---|---|---|---|---|
| `M-T1` | drop R1 (the `PAUSED` line) | T1 | **RED** | T1, K3, K3b |
| `M-T2` | remove the 45 min expiry (`… -lt $WdPauseExpiryMin` → `$true`) | T2 | **RED** | T2 |
| `M-T3` | drop R2, so R4/R6 run on a count > 1 | T3 | **RED** | T3 |
| `M-T4` | cap 3 → 4 | T4 | **RED** | T4, K4, K5 |
| `M-T5` | cap 3 → 2 | T5 | **RED** | T5, K3b |
| `M-T6` | drop R5 (grace) | T6 | **RED** | T6 |
| `M-T7` | stale threshold 30 → 35 | T7 | **RED** | T7 |
| `M-T8` | stale threshold 30 → 25 | T8 | **RED** | T8 |
| `M-T9` | treat unreadable as fresh (`$null -eq … -or` → `$null -ne … -and`) | T9 | **RED** | T9 |
| `M-L1a` | 24 h window → 25 h | L1 | **RED** | L1 |
| `M-L1b` | exact event match → `LAUNCH*` / `RESTART*` prefix match | L1 | **RED** | L1 |
| `M-C1` | read only the last CSV line (`-Tail 5` → `-Tail 1`) | C1 | **RED** | C1 |
| `M-K2` | skip the stop before a relaunch | K2 | **RED** | K2, K4, K5 |
| `M-K3` | log `PAUSED start` on every paused tick | K3 | **RED** | K3 |
| `M-K4` | log `CAP_REACHED` on every capped tick | K4 | **RED** | K4 |
| `M-K5` | **trust the launch call** (`Ok = $true`) — trap 2 in `collector-watchdog-spec.md` §0 | K5 | **RED** | K5 |
| `M-K6` | rethrow from the tick's `catch` | K6 | **RED** | K6 |

The extra failures are cascades: the stand-in process state carries from one `K` case to the next. Each named case went red.

⚪ **No mutation for:** `C2`, `K1`, `K1b`, `K1c`, `K3b`. `K1`'s count-after-launch is covered by `M-K5`. `K3b` went red under `M-T1` and `M-T5`.

### 3.2 `collector.ps1` verbs (`H-3`), in place, each undone by the inverse edit

| Mutation | Edit | Named case | Result |
|---|---|---|---|
| `M-S7` | remove `Clear-WatchdogPause` from `restart`'s `finally` | S7 | **RED** (S6 also red) |
| `M-S8` | do not exit when the marker is not confirmed | S8 | **RED** — the app would have been stopped with no marker |
| `M-S3` | drop the `$` anchor on the 10-min trigger's empty duration | S3 | **RED** — a 1-day repetition would pass as installed |

After the three undos: `git diff --quiet -- tools/ops/collector.ps1` → no diff against `105fcef`.

---

## 4. Decisions taken — auto-proceeded, one line each

IDs continue the spec's series; `WDG-9` to `WDG-15` were checked free with `git grep` on 2026-10-01.

| ID | Decision | Options | Picked | Why |
|---|---|---|---|---|
| `WDG-9` | What `START` means | (a) the first tick after this logon session began (from `Win32_LogonSession.StartTime`; the boot time if that cannot be read) · (b) the first tick after boot · (c) a second task whose action passes `-AtLogon` | **(a)** | A task cannot tell which trigger fired. (c) records the trigger exactly, but needs a second task, and `collector-watchdog-spec.md` §2.1 specifies one. **Step 3 of the three-step test: the richer option conflicts with the spec's design, it is not cheaper-and-adequate.** (a) logs `src=`, `logon=`, `boot=`, `session=` and `user=`, so the line says what it measured. It also fires on the first tick after an install into an existing logon |
| `WDG-10` | Which CSV line is "the last row" | (a) the newest parseable `Timestamp` in the last 5 lines · (b) the last line only (the spec's words) | **(a)** | (b) reads a torn line while the app writes, finds no timestamp, and R6 restarts a **healthy** app. (a) gives up nothing: a file with no parseable row in its last 5 lines still reads as unreadable. Tested by `C1` / `M-C1` |
| `WDG-11` | `deploy` / `restart` when the marker cannot be confirmed | (a) abort before the stop (exit 1, nothing changed) · (b) warn and continue | **(a)** | (b) re-opens trap 1 (the deploy race) exactly when it matters. Tested by S8 / `M-S8` |
| `WDG-12` | Task settings the spec does not state | run level; run time limit | **Limited**; **5 min** | Limited matches `Start-RemoteApp`'s `schtasks /it` launch, so the watchdog can stop what `deploy` started and vice versa. 5 min: a tick takes seconds, and with `IgnoreNew` a **hung** tick would block every later tick for the 72 h default |
| `WDG-13` | The rollback's marker age | (a) re-write the marker at the start of `Invoke-Rollback` · (b) leave it | **(a)** | A gate-timeout rollback can run 30+ min after the marker was written. Re-writing resets its 45 min age. The `finally` still removes it |
| `WDG-14` | `install-watchdog` pre-flight | refuse a script that is uncommitted, non-ASCII or has parse errors | **refuse** | The installed file must be traceable to a commit. Windows PowerShell 5.1 reads a BOM-less file as the ANSI code page, so a non-ASCII byte could change what runs on the box |
| `WDG-15` | How the mutations were run | (a) on a scratch copy, hash-checked (the watchdog) · (b) in place with inverse edits (the brief's words) | **(a)** for the watchdog, **(b)** for `collector.ps1` | (a) never touches the tracked file, so there is nothing to restore; the hash check proves it. `collector.ps1` was already committed, so (b) plus `git diff --quiet` proved each undo |

Also, not a decision but worth knowing: `deploy`'s steps 4–9 now sit inside `try { … } finally { Clear-WatchdogPause }`. The block is **not re-indented**, to keep the diff reviewable. `exit` inside `try` runs the `finally`; checked on PS 5.1 on this machine.

---

## 5. Feedback on the spec

- **`LAUNCH_FAILED` and `RESTART_FAILED` do not count toward the cap** (`collector-watchdog-spec.md` §2.3, taken literally). Consequence: if no process ever appears (for example, the exe is missing), the watchdog tries again every 10 min, forever, and logs one `LAUNCH_FAILED` line each time. That is harmless to the box and loud in the log. An app that starts and then crashes **does** count, because a process exists at the count and `LAUNCHED` is logged, so R3 caps it. No change made; named so it is a choice.
- **`START` "at logon"** cannot be measured as written; see `WDG-9`.
- **"The file's last line"** in `collector-watchdog-spec.md` §2.2 would restart a healthy app on a torn read; see `WDG-10`.
- **The pause-marker race needs an order, not just a marker.** A marker alone leaves a window: the tick reads "no marker", then `deploy` writes the marker and stops the app, then the tick counts 0 processes and launches. The watchdog reads the **process count first and the marker second**, and re-checks the marker immediately before `LAUNCH` or `RESTART`. A window of milliseconds between that re-check and `Start-Process` remains. If it is ever hit, `deploy`'s own `Start-RemoteApp` refuses to launch onto a running process, so the failure is loud, not a double instance. This is worth one sentence in `collector-watchdog-spec.md` §2.4.
- **The spec's live test §4.2 step 1** expects "one `HEARTBEAT` or `START` line". With `WDG-9`, an install normally logs **both**.

---

## 6. What I did not verify

Everything below needs the box. Live tests in `collector-watchdog-spec.md` §4.2 cover the first six.

| Claim | Why it is unverified | What proves it |
|---|---|---|
| A task with logon type Interactive and no stored password registers through SSM on Server 2019 | Registered on no machine; only built in memory here | §4.2 step 1 (`install-watchdog -Yes` exit 0) |
| On Server 2019, omitting `-RepetitionDuration` gives an **empty** (indefinite) duration | Checked only on this Windows 11 machine (PS 5.1.26100): empty. `install-watchdog` **refuses** anything else and exits 2 | §4.2 step 1 |
| `Start-Process` from the task lands the app in the administrator's interactive session, with a visible window | No box | §4.2 step 2 (`LAUNCHED`, then new rows) |
| **The launched app survives the tick's `powershell.exe` exiting** — Task Scheduler does not kill the task's child processes on normal completion | Believed, not measured. If wrong, every `LAUNCHED` app dies within seconds, and the cap stops it after 3 | §4.2 step 2: rows must keep coming **more than 10 min** after `LAUNCHED` |
| The 5 min run limit (`WDG-12`) never kills an app launched in that tick | A tick is seconds long; the kill behaviour at the limit was not measured | Only a hung tick would show it; read `watchdog.log` for a `LAUNCHED` with no rows after |
| Interactive tasks run while the RDP session is **disconnected** (not signed out) | No box. The app already runs in such a session today | §4.2 step 2, run after disconnecting RDP |
| Whether `-WindowStyle Hidden` still flashes a console window every 10 min in the admin session | Known Windows behaviour, not observed here. Harmless on an unattended box | Watch once over RDP |
| `Win32_LogonSession.StartTime` is readable from the task on the box (`WDG-9`) | Readable on this machine (logon type 2). On failure the watchdog falls back to the boot time and says `src=boot` | The first `START` line's `src=` |
| `deploy`'s marker handling end to end | `deploy` needs a clean, pushed tree and a Release build; not run. The code shape is the same `try/finally` as `restart`, which `H-3` S6–S8 exercised | The next real deploy: its output must show "pause marker written (deploy)" and "pause marker removed (deploy)" |
| `collector-readback.ps1`'s new line on the box | Parsed only (`H-4`) | The next read-back prints `WATCHDOG_TAIL` |

Carried from the spec without checking: the 2026-08-22 measurement that `schtasks /run` returns success with no session (trap 2); the 30 min stale threshold's basis (trader ruling `AT-4`).

---

## 7. Before installing — for the trader

1. **Auto-logon first** (`collector-watchdog-spec.md` §1). `install-watchdog` warns when `AUTOLOGON` is not `1`, and when no administrator session is logged on.
2. **`install-watchdog` needs `tools/ops/collector-watchdog.ps1` committed** on the machine you run it from. Pushing is not required by the verb.
3. **The install's own tick acts.** If the app is down at install time, that tick launches it (rule R4).
4. **From now on, run `deploy` and `restart` only from this `collector.ps1`.** An older copy does not write the pause marker, and the watchdog could launch the old exe during the deploy's stop.
5. **Manual work on the box:** create `C:\DeribitEngine\watchdog.pause` first (any content). The watchdog ignores it after 45 min, so re-save it for longer jobs.
6. To remove the watchdog: `collector.ps1 uninstall-watchdog -InstanceId … -Yes`. It leaves the script and `watchdog.log` in place.
