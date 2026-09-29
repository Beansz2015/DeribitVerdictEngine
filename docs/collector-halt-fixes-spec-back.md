# Collector-halt fixes (C-8) — build record and spec-back

**Build:** 2026-09-29 (UTC), this seat (Opus 5.5, medium), against [`collector-halt-fixes-spec.md`](collector-halt-fixes-spec.md). Started from `2bc1863`. **Local only, not pushed, not deployed.** One build lane, so the outcome summary and the review packet share this file.

---

## 1. Outcome

| Fix (spec §2) | Done | Where |
|---|---|---|
| F-1 `run_errors.log` | ✅ | `Core/RunErrorLog.vb` (new); linked into the seven projects that link `DeribitClient.vb` |
| F-2 no modal box on auto-run | ✅ | `UI/MainForm_Analysis.vb` `btnAnalyze_Click` |
| F-3 global handlers | ✅ | `Program.vb`; `MainForm.OnUiThreadException` in `UI/MainForm_AutoRun.vb` |
| F-4 tracker hang | ✅ | `LivePerformanceTracker.vb` (`UpdateAsync` order, `MarkInitSkipped`, `ResetInitForTest`); `UI/MainForm_Layout.vb` start-up |
| F-5 status-line overflow | ✅ | `UI/MainForm_Layout.vb` `BuildWsStatusSegment` |
| F-6 per-request timeout | ✅ | `DeribitClient.vb` (`ResolveRequestTimeout`, `CurrentRequestTimeout`, `GetStringOrRecordAsync`) |
| F-7 ops | ✅ built · ⚠ `restart` not run | `tools/ops/collector.ps1` (`restart` verb, `run_errors.log` in fetch); `tools/ops/collector-readback.ps1` (`RUNERR_TAIL`) |

No scoring, settings, CSV, snapshot or card change. Settings stay v69. `docs/DeribitIndicatorProject.md` §15 has a new row dated 2026-09-29; `docs/architecture.md` maps the new file.

---

## 2. Handles — `H-n` you can run, `E-n` build-time evidence

Run from the repo root at this build's commit.

**H-1 — the harness.**

```bash
dotnet run --project verify/ordercheck/OrderCheck.vbproj
```

Printed on 2026-09-29: `ALL PASS`, 494 `PASS` lines (489 before, plus `A93a`–`A93e`), 1 `SKIP` (`A81b`, the known-defect fixture for maker-side liquidations).

**H-2 — the five new fixtures only.**

```bash
dotnet run --project verify/ordercheck/OrderCheck.vbproj | grep -E "^(PASS|FAIL)  A93"
```

Printed five `PASS` lines, `A93a` to `A93e`.

**H-3 — the mutations.** Apply one row's edit, run H-2, see the named fixture go red, then undo exactly that edit.

| Mutation | File | Seen 2026-09-29 |
|---|---|---|
| `Clean` returns `s.Trim()` (no sanitising) | `Core/RunErrorLog.vb` | `A93a` FAIL |
| `Await _initTcs.Task` moved back above the `Enabled` check | `LivePerformanceTracker.vb` | `A93b` FAIL, `A93c` PASS |
| `MarkInitSkipped` body commented out | `LivePerformanceTracker.vb` | `A93c` FAIL, `A93b` PASS (run alone) |
| `rejected = seconds < 0 OrElse …` | `DeribitClient.vb` | `A93d` FAIL |
| Static constructor sets `_http.Timeout = FromSeconds(RequestTimeoutSeconds)` | `DeribitClient.vb` | `A93e` FAIL (`Timeout=00:00:15`) |

The first, second, fourth and fifth ran together in one harness run: 4 failures, `A93c` green. The third ran alone: 1 failure. Every mutation was undone by an inverse edit, and a grep for the mutation marker afterwards found no file.

**H-4 — every project that links `DeribitClient.vb` builds.**

```bash
dotnet build DeribitVerdictEngine.sln -c Release
```

Also build `tools/BacktestRunner`, `tools/AutoTweaker`, `tools/CeilingAudit`, `tools/WhatIfRunner` and `tools/ops/SwingFallbackRead`. On 2026-09-29 all six reported 0 errors and 0 warnings. ⚠ `verify/auditproofs` fails with 34 errors. **None is from this build:** they are the audit proofs' calls into the v68 Kelly API (`EstProbFloor`, `CalcKellySizing`'s old signature), which v69 changed. The audit handoff runs those proofs at the pin `6e74181`, not on `master`.

**H-5 — the gate.**

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File tools/checks/verify-gate.ps1 -Mode local-fast
```

Printed `GATE PASSED` before the commit. ⚠ **`prepush` will WARN on version-bump:** `Core/RunErrorLog.vb` is an engine path, and there is no settings bump because no key changed. `[no-engine-change]` would be false, so the commit does not carry it. The WARN is a nudge, not a failure.

**E-1 — read-back on the live box (old build).** `tools/ops/collector-readback.ps1` ran 2026-09-29 20:23 UTC with `SSM_STATUS=Success` and printed the new `RUNERR_TAIL` section header. The box still runs the old build, so no `run_errors.log` exists yet.

**E-2 — the `restart` verb was NOT run.** Even its plan-only form (no `-Yes`, read-only pre-flight) was denied by the auto-mode classifier as "Production Reads". Parse-checked only (0 errors).

---

## 3. Decisions

All seven (`CH-1`–`CH-7`) were auto-proceeded, as logged in the spec's §4. None is queued.

**For the trader (reserved or yours):**
- **Deploy.** A write to the live collector. Plan: after this build is pushed, `collector.ps1 deploy` by about 2026-10-06, then two or three days of read-backs before 2026-10-13. Watch `RUNERR_TAIL`: a line there is now the evidence of a failure the old build would have hidden behind a modal box.
- **The `restart` verb needs one live use before the holiday, run by you:** `tools/ops/collector.ps1 restart -InstanceId i-0d6c133058876273e`, first without `-Yes` (plan only), then with it. **If a holiday seat is to use it (ruling `AT-4`), it needs an allow rule in `.claude/settings.local.json`.** A seat cannot add its own permissions.

---

## 4. Feedback on the spec's own assumptions

- The spec said the fixture harness had never called `InitialiseAsync`, and that stays true (grep, 2026-09-29). `A93b`/`A93c` do not rely on it: each resets the init task first.
- The spec did not foresee that `DeribitClient.vb` is linked into seven projects. Logging a rejected timeout to `run_errors.log` made all seven link `Core/RunErrorLog.vb`. That is mechanical, and H-4 covers it.

---

## 4a. Launch test — run 2026-09-29 20:37–20:40 UTC, trader-authorised

The Release build at `f3bc9fc` ran on the dev machine, with the overlay keeping tape capture off, for three `ON_CLOSE` runs:
- 3 rows at 20:38:02, 20:39:01 and 20:40:06. Each was `WsHealth=OK`, `SettingsVersion=69`, `NO TRADE`.
- No `run_errors.log` was written, and no error window appeared.
- Status line (read by UI Automation): `WS OK · 1/3/5/15 fresh · trades 560 · Log: 3 rows`. At about 4 s it already read `fresh`, so the `awaiting first frame` text was **not observed**: the first frame arrived before the first render.

The app closed normally. This covers the normal path only. The error path (a run exception with auto-run on) was not forced.

## 5. What I did not verify

- **Rows B2 and B3 in the running app.** `MainForm_*.vb` and `Program.vb` are outside the harness. The edits were checked by compile and by reading. I did not launch the app. The dev overlay in `bin\Release` does keep tape capture off (`{"trade_store": {"enabled": false}}`, read 2026-09-29), but a launch starts auto-run (`start_engaged` is true in the tracked file) and the signal bridge writes `verdict_signal.json`. That is a live run on your machine, and it is yours to start. **Your pre-push test:** launch, confirm the status line reads `WS OK · awaiting first frame` for the first second or so, then `fresh`, and that a normal run writes no `run_errors.log`.
- That `SetUnhandledExceptionMode` placement works at run time. It is called before any window exists, as .NET requires, but this was not executed.
- The `restart` verb (E-2).
- How often an auto-run exception happened on the box before this build. The old build recorded nothing.
