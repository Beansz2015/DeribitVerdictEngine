# Collector-halt fixes — adversarial-audit decision C-8, plus a `collector.ps1 restart` verb — SPEC

**Status:** written and auto-proceeded 2026-09-29 (UTC). Build in the same seat. **The deploy is the trader's** (a write to the live collector).

**Why now.** Trader rulings `AT-1` (b) and `AT-4` (a) in [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §6. The trader is away 2026-10-14 → 2026-11-25, and one exception in an auto-run stops the collector until a human clicks OK. The restart verb covers the halts this build cannot (a hang, a crash, a reboot).

**Source rows** are rows of [`adversarial-audit-2026-09-24.md`](adversarial-audit-2026-09-24.md) §B, Band B ("the collector stops"). Decision IDs `CH-1` to `CH-7` are new (checked free across the repo on 2026-09-29).

**Class.** No scoring change, no `settings.json` key, no CSV or schema change, no snapshot or card line (the display-string parity rule does not fire). One new sidecar log. Settings stay v69. Engine behaviour changes, so `DeribitIndicatorProject.md` §15 gets a settings-untouched row.

---

## 0. Implementer brief

- **Model: Opus 5.5 · Effort: medium.** The design is settled below and each fix has an in-repo template (`Core/WsHealthLog.vb` for the log, `Stop-RemoteApp`/`Start-RemoteApp`/`Wait-DeployGate` for the verb, the audit's proof P14 in `verify/auditproofs/Program.vb:774-784` for the hang fixture).
- **Where it slips:**
  1. **The tracker's init task is `Shared` and harness-wide.** A fixture that relies on "nothing completed it yet" passes by accident once any earlier fixture calls `InitialiseAsync`. Give the fixture its own precondition (a test reset), then mutate once per decision.
  2. **The per-request timeout.** `HttpClient.Timeout` must become infinite, or the old poisoning path stays. The timeout then surfaces as a `TaskCanceledException` from the linked token; `ExecuteWithRetry` already treats `TaskCanceledException` as a transient timeout, and there is no caller token to confuse it with. Do not add one.
  3. **`MainForm_*.vb` is not harness-reachable** (OrderCheck compiles none of it). Rows B2 and B3 are verified by review. Keep the logic there thin.
- **Escalation trigger:** a fixture that needs a sleep longer than the timeout it guards, or any change that would alter `analysis_log.csv`, a snapshot or card line, or a settings key. Stop and ask.

---

## 1. The defects, verified on `master` at `b00b644` (2026-09-29)

| Row | Defect | Where on `master` |
|---|---|---|
| **B2** | Any exception in a run raises a modal `MessageBox`. The `Finally` that re-enables `btnAnalyze` waits for OK, and auto-run returns early while `btnAnalyze` is disabled. The collector stops until someone clicks OK | `UI/MainForm_Analysis.vb:33-49`; `UI/MainForm_AutoRun.vb:166` |
| B2, wider | `Program.vb` attaches no `Application.ThreadException` handler, so any other UI-thread exception shows the default .NET dialog, also modal. Nothing records a non-UI-thread crash | `Program.vb:8-15` |
| **B1** | With `performance_display.enabled` false at start-up, `InitialiseAsync` is never called, so the tracker's init task never completes. `UpdateAsync` awaits it **before** its own `Enabled` check, and every run hangs there | `UI/MainForm_Layout.vb:488`; `LivePerformanceTracker.vb:666`, `:669`; `UI/MainForm_Analysis.vb:719` |
| **B3** | The status line computes `CInt((UtcNow − LastFrameUtc).TotalSeconds)`. Before the first frame, `LastFrameUtc` is `DateTime.MinValue`, and `CInt` overflows | `UI/MainForm_Layout.vb:1996` |
| **B4** | The REST timeout is set once in a static constructor. A value of 0 makes the `HttpClient.Timeout` setter throw, the type initialiser fails, and every REST call fails for the life of the process, even after the file is fixed | `DeribitClient.vb:26-29` |

Shipped config does not trigger B1 or B4 (`performance_display.enabled: true`, `request_timeout_seconds: 15`). Both need a settings edit. They are fixed anyway: small, and each one is a total halt.

---

## 2. The fixes

| # | Fix | Files |
|---|---|---|
| **F-1** | New host-agnostic `Core/RunErrorLog.vb`: an append-only `run_errors.log` beside the CSV, one line per failure, never throws. Line: `utc | origin | instance_id | trigger | exception type | message | top frame`. Pipes, CR and LF in any field are replaced; the message and the frame are capped (`Public Const`) | new file; linked into `verify/ordercheck/OrderCheck.vbproj` |
| **F-2** | `btnAnalyze_Click`: always log the failure (origin `RUN`). Show the `MessageBox` **only when auto-run is not engaged** — a human is then at the screen, which keeps the P5b §3.2.1 choice. The `Finally` then always runs | `UI/MainForm_Analysis.vb` |
| **F-3** | `Program.vb`: `SetUnhandledExceptionMode(CatchException)` before the form exists; an `Application.ThreadException` handler that logs (origin `UI_THREAD`) and shows a box only when auto-run is not engaged; an `AppDomain.UnhandledException` handler that logs (origin `APPDOMAIN`) before the process dies | `Program.vb`; a thin `Friend` handler on `MainForm` |
| **F-4** | `LivePerformanceTracker`: `UpdateAsync` checks `Enabled` **before** awaiting the init task; new `MarkInitSkipped()` completes the init task, and the form calls it when the tracker is disabled at start-up. A later flip to true stays inert until a restart (the existing empty-path guard), which the code comment says | `LivePerformanceTracker.vb`; `UI/MainForm_Layout.vb` |
| **F-5** | Status line: when `LastFrameUtc = DateTime.MinValue`, print `WS OK · awaiting first frame · trades N`; otherwise compute the age as a `Double` and cap it before any integer conversion | `UI/MainForm_Layout.vb` |
| **F-6** | `DeribitClient`: `_http.Timeout` becomes `Timeout.InfiniteTimeSpan`; each GET runs under a `CancellationTokenSource` built from the **current** `request_timeout_seconds`. New `ResolveRequestTimeout(seconds)`: 1 to 300 s is used as given; anything else falls back to the POCO default and is logged once per distinct bad value (origin `CONFIG`) | `DeribitClient.vb` |
| **F-7** | Ops: `collector.ps1 fetch` copies `run_errors.log`; `collector-readback.ps1` prints its last 3 lines as `RUNERR_TAIL` (still read-only); new verb `collector.ps1 restart` (§3) | `tools/ops/collector.ps1`, `tools/ops/collector-readback.ps1` |

---

## 3. The `restart` verb

`collector.ps1 restart -InstanceId <id> [-Yes]`

1. **Resolve the install directory without needing a live process.** A crashed collector has none. Use the running process's directory if exactly one runs, else `C:\DeribitEngine` if `DeribitVerdictEngine.exe` exists there. Otherwise, fail and change nothing.
2. **Print the plan.** Without `-Yes`, stop here and exit 1 with nothing changed. A seat's shell is non-interactive, so there is no prompt.
3. `Stop-RemoteApp` (idempotent), then `Start-RemoteApp`, then `Wait-DeployGate` with no settings-version expectation. That gate needs two new CSV rows at least 45 s apart within 12 minutes.
4. Exit 0 on a pass. Exit 2 when the app was restarted but the gate failed: **stop and investigate by hand**, as for a deploy.

It changes no file on the box. ⚠ **The auto-mode classifier may block it.** A seat cannot edit its own permissions, so the trader adds the allow rule if one is wanted (as for `collector-readback.ps1`).

---

## 4. Decisions — auto-proceeded, one line each

| ID | Decision | Options | Picked | Why |
|---|---|---|---|---|
| `CH-1` | How wide the B2 fix is | (a) the Analyze handler only · (b) (a) + global UI-thread and AppDomain handlers · (c) remove every `MessageBox` | **(b)** | Guarantees more: no UI-thread exception anywhere can block auto-run, and a crash leaves a record. (c) removes the confirmation boxes a human answers on purpose (reset log, interval check) |
| `CH-2` | When a box is still shown | (a) never · (b) only when auto-run is not engaged · (c) always, as today | **(b)** | Keeps the P5b §3.2.1 choice (the trader wants to see errors) exactly where a human is present; never blocks an unattended loop |
| `CH-3` | What the log records | (a) transition-only, like `ws_health.log` · (b) every failure | **(b)** | Records more: a failure that repeats every run is a count worth seeing. Worst case about 60,000 lines (about 18 MB) over six weeks at one per minute |
| `CH-4` | B1: flip to true after a disabled start | (a) inert until restart, said in code · (b) initialise lazily on the flip | **(a)** | Step 3 of the CLAUDE.md test: (b) changes when the perf strip renders, a reserved rendered-value change; (a) removes the hang, which is the defect |
| `CH-5` | B3's text before the first frame | (a) `awaiting first frame` · (b) widen to `Long` and print the huge age | **(a)** | (b) is true arithmetic and a false statement. The status line is not a snapshot or card surface, and it has no value today, because it throws |
| `CH-6` | B4's fix | (a) clamp in the static constructor · (b) per-request timeout from current settings, validated | **(b)** | More truthful: the running timeout always matches the file, including after a hot reload. (a) leaves a fixed file ignored until restart |
| `CH-7` | The restart verb's confirmation | (a) an interactive prompt · (b) `-Yes`, else print the plan and stop | **(b)** | A seat's shell cannot answer a prompt. The explicit switch keeps the deliberate step |

---

## 5. Fixtures (`A93a`–`A93e`, checked free 2026-09-29)

| Fixture | Asserts | Mutation that must fail it |
|---|---|---|
| `A93a` | `RunErrorLog` writes one line with the seven fields; pipes and newlines in the message are replaced; an over-long message is capped at the constant | Remove the sanitising |
| `A93b` | With the tracker disabled and its init task fresh (test reset), `UpdateAsync` completes within 3 s | Move the `Enabled` check back after the await |
| `A93c` | Fresh init task, `MarkInitSkipped()`, tracker enabled: `UpdateAsync` completes within 3 s | Make `MarkInitSkipped` a no-op |
| `A93d` | `ResolveRequestTimeout`: 15 → 15 s; 1 → 1 s; 300 → 300 s; 0, −5, 301 → the POCO default. **SHIPPED BEHAVIOUR** for the default (read from `New NetworkSettings()`), **MECHANISM** for the range literals | Accept 0 |
| `A93e` | `DeribitClient`'s `HttpClient.Timeout` is infinite after type initialisation (reflection on the private field) | Restore the static assignment |

Rows B2, B3 and the `restart` verb are not harness-reachable. They are verified by review, and the verb by a live use before 2026-10-13 with the trader's go.

---

## 6. Acceptance

- `dotnet build` Release, 0 errors and 0 warnings, and the full harness `ALL PASS` (489 PASS, 1 SKIP before this build; five new fixtures).
- Each fixture's mutation run and seen red.
- `tools/checks/verify-gate.ps1` passes.
- A spec-back with runnable handles, per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).
