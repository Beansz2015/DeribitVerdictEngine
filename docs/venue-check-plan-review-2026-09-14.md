# Orchestrator review — `venue-check-schedule-plan.md`, plus build instructions

**Written:** 2026-09-14 (UTC) by the orchestrator seat of 2026-09-14b. **For:** the implementer seat that wrote [`venue-check-schedule-plan.md`](venue-check-schedule-plan.md) (commit `c0fbd5e`).

**Rulings status:** the plan's reserved decisions (`D-1`–`D-5`, `D-7`, and the new `D-8` below) are **NOT ruled by the trader yet**. The reads below are the orchestrator's. **The build steps in §1 do not depend on those rulings. Nothing touches the collector box.**

---

## 1. What to do, in this order

| Step | Work | Model / effort |
|---|---|---|
| **1** | **The `venue_status.log` wiring fix** (§2). Commit it on its own | Sonnet-level work, medium. Doing it in this seat is fine |
| **2** | **The plan's fix build** — `V-1` to `V-4` in `venue-check-schedule-plan.md` §2 (`V-1` paginate the Deribit fetch · `V-2` windowed streaming store read · `V-3` reader share mode · `V-4` `--venue-hours` and the `--strict` venue verdict), with the amendments in §3 | Opus, **high**, per that plan's §2 |
| **3** | **Update `venue-check-schedule-plan.md`**: fold in the §3 amendments and add `D-8` (§4) to its D-table. Same commit as step 2, or its own | — |

- **Order matters.** Step 1 and step 2 both edit `tools/BacktestRunner/BacktestProgram.vb` near line 358. Finish step 1 first.
- **Commit locally. Do not push.** Tag each commit `[no-engine-change]` unless it changes an engine-binary file. `V-3` does: it changes `Core/TradeStoreWriter.vb`, which ships with the S2 engine deploy. Say so in that commit message.
- **Stop and ask** if a fixture passes without first failing against the unfixed code. The plan's own escalation rule.

## 2. Step 1 — the `venue_status.log` wiring fix

**The defect (verified by the orchestrator 2026-09-14):**

- `BacktestProgram.vb:358` calls `CoverageReport.BuildResult(opts, storeDir, analysisLogPath, wsHealthPath, markerPath, schedulePath)`. It never passes the optional `venueLogPath` (`CoverageReport.vb:1299`).
- So the venue-outage hour class `OutOfScopeVenue` (built in `16b19f6`, spec `c3b-venue-scoping-spec.md` Part B) **can never fire from the CLI**.
- Why nothing caught it: that spec never names the CLI call site, and its fixtures call `BuildResult` directly.
- No live effect yet: `collector.ps1 fetch` already copies `venue_status.log` (line 117), but no copy-back holds one until the S2 deploy puts the venue-status instrument on the box. **It must land before that deploy.**

**Two requirements:**

1. **Print the venue-log path** the way the schedule line does (`BacktestProgram.vb` ~line 355): the full path, plus `[not found — no venue windows]` when the file is absent. A missing file must be visible, not silent.
2. **Test the CLI's own path resolution, not `BuildResult` directly.** A fixture that calls `BuildResult` with the path repeats the exact gap. If the harness cannot reach the CLI's resolution, extract it into a small testable function and fixture that. If even that is not possible, **say so in the commit and the report** — the gap then has no automated cover.

## 3. Amendments to the plan

| Plan item | Amendment | Why |
|---|---|---|
| **`D-4`** (cadence and window) — trigger times | Replace 00:07 and 12:07 UTC with **14:07 and 22:07 UTC**. The wrapper derives each window from the previous ledger row's `window_to_utc`, with 1 h overlap: 17 h for the 14:07 run, 9 h for the 22:07 run. The oldest window start is 17 h 07 m old at trigger, inside the 22 h cut-off. `--venue-hours` stays 1–24 | Both old triggers fall outside NY hours (13:00–23:00 UTC), where the box's paging burst rate is 14.29 %. Inside NY it is 1.83 %. ⚠ That split is pooled over 13:00–23:00; hours 14 and 22 were not checked individually |
| **`D-7`** (ledger schema) | Add a **`tool_commit`** column: the commit the `BacktestRunner` build came from | `B-1` (the 1,000-trade truncation) proves the tool itself can report a false clean. A row that names its build lets a future seat find and re-grade rows written by a defective build |
| **`D-2`** (run on the box) — `V-2` acceptance | Run the post-fix local memory re-measurement **with `DOTNET_GCHeapHardLimit` already set** to the value you propose for the box. It passes only if the run completes under that limit | The 789 MB peak was measured on the dev machine, where the .NET GC collects less often with plentiful RAM, so private memory there overstates the box figure (reasoning, not measured). A run under the real limit tests the limit itself |

## 4. New decision `D-8` — keep the raw Deribit pages?

Add this row to the plan's D-table.

| Options | Orchestrator read | Class |
|---|---|---|
| **(a)** keep only the verdict and ledger row (the plan as written) · **(b)** also keep each window's raw Deribit trade pages, gzipped, never auto-deleted | **(b), if the plan's check 3 (box disk free) shows room.** Deribit history is gone after about 24 h. With (b), any `LOSS` stays repairable later, and a fixed tool can re-grade old windows. Cost: close to a second copy of the tape on disk (`trades_2026-09.csv` holds about 7 MB/day raw, before gzip) | ⛔ **RESERVED** — a new write on the box |

If (b) is ruled, the pages are written by the `V-4` path. Do not build (b) until it is ruled; leave a clean seam for it.

## 5. Context — box memory is not resolved

The plan's `B-2` (memory) stands. For the record, so it is not described as resolved:

- The hostel-app colocation question **is** closed (`seat-handover-2026-08-29.md` §4; about 20.5 MB for about 7 s an hour).
- `_evalCache` (the never-trimmed eval-cache list in `LivePerformanceTracker`) has an estimated 8–17 month RAM runway (`seat-handover-2026-08-24.md` line 112).
- The box's own memory pressure is **not** resolved: 124 MB available and 289 `pagesOUT/s` on 2026-09-10, 104 MB and 548 on 2026-09-11, 90 MB and 160 in this plan. A burst-rate rise from 9.10 % to 14.20 % is unattributed. The detached 24 h counter run that would settle it is still owed (`seat-handover-2026-09-11.md` §5.1). All readings were SSM-attached, which costs 30–50 MB.

## 6. Agreed as written

`D-1` (a) the `deploy-tool` verb · `D-2` (a) AWS only after `V-2` · `D-3` (a) `FileShare.ReadWrite` · `D-5` (a) `SYSTEM` · `D-6` (b) the machine-readable `VENUE_CHECK` line with exit codes — a correct auto-proceed, correctly logged.

## 7. Report back

At most 10 lines: step 1 commit and how the fixture covers the CLI path (or why it cannot) · step 2 commit(s), fixtures failed-then-passed, gate result, post-fix peak memory under the heap limit · step 3 commit · anything that needs a trader ruling.
