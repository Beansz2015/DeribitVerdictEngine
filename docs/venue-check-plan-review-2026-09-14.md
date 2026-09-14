# Orchestrator review — `venue-check-schedule-plan.md`, plus build instructions

**Written:** 2026-09-14 (UTC) by the orchestrator seat of 2026-09-14b. **Revised 2026-09-14 16:0x UTC after the trader's ruling.** **For:** the implementer seat that wrote [`venue-check-schedule-plan.md`](venue-check-schedule-plan.md) (commit `c0fbd5e`).

⛔ **THIS REVISION REPLACES THE FIRST VERSION OF THIS FILE (commit `cc42ffd`).** If you started from that version, stop and re-read. The trader ruled **option A** below: `BacktestRunner` does **not** go onto the AWS box.

---

## 0. The ruling (trader, 2026-09-14 UTC)

**Option A — run the venue check on the dev machine, right after each `collector.ps1 fetch`.** No scheduled task, no `deploy-tool` verb, nothing new on the collector box.

| Why | Detail |
|---|---|
| Gap repair does not need `BacktestRunner` | The in-app repair finds holes by `trade_seq` gaps and refills them on its own timer |
| What the venue check is actually for | Testing the one assumption repair and the coverage report share: that `trade_seq` is a gap-free counter on Deribit's public tape. A loss with no `trade_seq` gap is invisible to both |
| Why not on the box | The box runs with 90–120 MB free and already pages out. A second .NET process risks the live tape |
| What A gives up | It audits a sample of hours (one ~13 h window per fetch), not every hour. A sample is enough to prove or disprove the assumption |
| What comes next | A dated review of the samples (§5). If a sample shows loss with no `trade_seq` gap, the follow-up is **option B**: a small comparison inside the app's own repair timer, specced then. Not now |

## 1. What to do, in this order

| Step | Work | Status | Model / effort |
|---|---|---|---|
| **1** | **The `venue_status.log` wiring fix** (§2). Commit on its own | ✅ **PROCEED** | Sonnet-level, medium |
| **2** | **`V-1`** (paginate the Deribit fetch) and **`V-4`** (`--venue-hours`, the `VENUE_CHECK` machine line, `--strict` venue exit codes) from `venue-check-schedule-plan.md` §2, plus a raw-page dump flag (§3) | ✅ **PROCEED** | Opus, **high** |
| **3** | **Post-fetch hook** in `tools/ops/collector.ps1 fetch` (§3) | ✅ **PROCEED** | Opus, medium |
| **4** | **Update `venue-check-schedule-plan.md`** with the ruling (§4) | ✅ **PROCEED** | — |
| — | **`V-2`** (windowed streaming store read) and **`V-3`** (reader share mode in `Core/TradeStoreWriter.vb`) | ⏸ **HOLD.** Do not build. The dev machine has the memory, and nothing new reads the store on the box. Keep the specs in the plan, marked held | — |
| — | `deploy-tool` verb, box scheduled task, box ledger, `DOTNET_GCHeapHardLimit`, trigger times | ⛔ **DROPPED** under option A | — |

- **Order matters.** Steps 1 and 2 both edit `tools/BacktestRunner/BacktestProgram.vb` near line 358. Finish step 1 first.
- **Commit locally. Do not push.** Tag commits `[no-engine-change]`. No step touches an engine-binary file now that `V-3` is held.
- **Nothing touches the collector box.** `collector.ps1 fetch` itself is allowed: it copies files back and writes nothing on the box.
- **Stop and ask** if a fixture passes without first failing against the unfixed code. The plan's own escalation rule.

## 2. Step 1 — the `venue_status.log` wiring fix: PROCEED

**Why it still matters under option A.** The coverage report now runs on the dev machine against each copy-back. `collector.ps1 fetch` already copies `venue_status.log` (line 117). Without this fix, the venue-outage hour class `OutOfScopeVenue` stays dead in every report. Hours when Deribit itself was down would then be blamed on the collector.

**The defect (verified by the orchestrator 2026-09-14):**

- `BacktestProgram.vb:358` calls `CoverageReport.BuildResult(opts, storeDir, analysisLogPath, wsHealthPath, markerPath, schedulePath)`. It never passes the optional `venueLogPath` (`CoverageReport.vb:1299`).
- So `OutOfScopeVenue` (built in `16b19f6`, spec `c3b-venue-scoping-spec.md` Part B) **can never fire from the CLI**.
- Why nothing caught it: that spec never names the CLI call site, and its fixtures call `BuildResult` directly.
- No copy-back holds a `venue_status.log` until the S2 deploy puts the venue-status instrument on the box. **The fix must land before that deploy.**

**Two requirements:**

1. **Print the venue-log path** the way the schedule line does (`BacktestProgram.vb` ~line 355): the full path, plus `[not found — no venue windows]` when the file is absent. A missing file must be visible, not silent.
2. **Test the CLI's own path resolution, not `BuildResult` directly.** A fixture that calls `BuildResult` with the path repeats the exact gap. If the harness cannot reach the CLI's resolution, extract it into a small testable function and fixture that. If even that is not possible, **say so in the commit and the report** — the gap then has no automated cover.

## 3. Steps 2 and 3 — the tool fixes and the post-fetch hook

**Step 2 — `V-1` and `V-4` as specced in the plan, with two additions:**

- **Raw-page dump.** Add `--venue-dump <path>`: write every Deribit page fetched for the window, gzipped, to that path. Deribit history is gone after about 24 h, so the pages are the only copy of what the venue said. On the dev machine the disk cost is not a constraint.
- **`tool_commit`.** The `VENUE_CHECK` line carries the commit the build came from. `B-1` (the 1,000-trade truncation) proves the tool itself can report a false clean. A row that names its build lets a future seat find and re-grade rows written by a defective build.

**Step 3 — post-fetch hook in `collector.ps1 fetch`:**

*Revised 2026-09-14 after rulings R-2 and R-3 (`venue-check-build-spec-back.md` §2): ledger schema and key read below updated to the shipped build (`a86fd08`).*

| Item | Value |
|---|---|
| When | At the end of every successful `fetch`, after the copy-back verifies |
| Opt-out | `-SkipVenueCheck` switch |
| `--to` | The previous whole UTC hour **before the fetch started** (clears the 30 s `trade_store.flush_seconds` lag) |
| Window | `--venue-hours 13`, `--from` = `--to` − 13 h. The window start is about 13–14 h old at run time, inside Deribit's ~24 h retention |
| Evidence | `--evidence-dir <the dated aws_fetch folder>` |
| Outputs, all inside the dated fetch folder | `venue_check_<to:yyyyMMdd-HH>Z.md` (report) · `.log` (stdout, stderr, `EXIT=<n>`) · `venue_pages_<to>Z.json.gz` (raw pages) |
| Ledger | Append one row to `aws_fetch/venue_check_ledger.csv` (21 columns, shipped order): `run_utc, fetch_folder, window_from_utc, window_to_utc, verdict, venue_trades, identity_matched, fallback_matched, missing, missing_inside_seq_span, store_legacy_only, store_trades, store_outside_venue_span, pages, venue_first_ts, venue_last_ts, seq_contiguous, tool_commit, dump, exit_code, reason` |
| `seq_contiguous` | Context: whether the coverage report found the store's `trade_seq` contiguous over the same window. **The decisive read of option A is `LOSS` with `missing_inside_seq_span > 0`** — a lost trade strictly inside the store's `trade_seq` span, which the sequence walk should have seen. A `LOSS` with `missing_inside_seq_span = 0` is an edge effect, not decisive. See §5 |
| No binary, or no `VENUE_CHECK` line | Write a `NOT_RUN` ledger row with the reason, and print it loudly. **Absence of evidence is recorded as not-run, never as clean** |

Confirm `aws_fetch/` is gitignored before writing the ledger there. If it is not, put the ledger in a gitignored location and say where.

## 4. Step 4 — update `venue-check-schedule-plan.md`

- Add the ruling at the top: option A, 2026-09-14 UTC, with a pointer to this file.
- Mark `D-1` (deploy verb), `D-2` (run on box), `D-5` (run-as account) and the original `D-8` question (raw pages on the box) as **dropped under option A**. Mark `D-7` (ledger schema) **replaced by the local ledger in §3**. Mark `D-4` (cadence) **replaced by "after every fetch"**.
- Mark `V-2` and `V-3` **HELD**. `V-3` stays the correct fix for any future box-side reader; note that.
- Keep the original text. Supersede it; do not delete it.
- ⚠ **Do not edit the `docs/trader-tick-queue.md` §2 venue row or its §4 reminder.** The orchestrator updated both. Add commit hashes only.

## 5. The dated review — recorded in `docs/trader-tick-queue.md` §4

**Due on or after 2026-10-05 UTC, AND only once at least 5 valid samples exist** (ledger rows whose verdict is `CLEAN` or `LOSS`, from builds at or after `6509a0f`, the `V-1` commit). If fewer exist on that date, re-date the reminder; do not review a thin sample.

✅ **Trader ruling `R-2` (a), 2026-09-14 UTC** (`venue-check-build-spec-back.md` §2). A missing trade at the window's edge sits outside the store's `trade_seq` span, so `seq_contiguous = true` alone does not prove the assumption false. The key read is **`missing_inside_seq_span > 0`**: missing venue trades whose `trade_seq` lies strictly inside the store's first..last `trade_seq` for the window. The field reads `na` when the check did not run.

| Samples show | Meaning | Next |
|---|---|---|
| All `CLEAN`, `seq_contiguous = true` | The `trade_seq` assumption holds on this sample | Keep the hook: it costs about a minute per fetch. No option B |
| Any `LOSS` with `missing_inside_seq_span > 0` | ⛔ **The assumption is false.** Repair and the coverage report can both miss loss | Spec option B, the comparison inside the app's repair timer |
| `LOSS` with `missing_inside_seq_span = 0` | Edge effect, not decisive: the lost trades sit outside the store's `trade_seq` span, which the sequence walk cannot see | Not evidence against the assumption; read the next sample |
| `LOSS` with `seq_contiguous = false` | Loss that `trade_seq` does see. A repair problem, not an assumption problem | Investigate repair |
| `NOT_RUN` or `INEXACT` | The tool or the evidence failed | Fix the tool before counting samples |
| `VENUE_SHORT` (ruled `R-1` (a), orchestrator, 2026-09-14) | Deribit's list did not cover the window, usually because the window is past its ~24 h retention. Its missing count proves nothing | **Not a valid sample.** It does not count toward the 5. If it appears on a fresh fetch, check the hook's window timing |

## 6. Context — box memory is not resolved

For the record, so it is not described as resolved:

- The hostel-app colocation question **is** closed (`seat-handover-2026-08-29.md` §4; about 20.5 MB for about 7 s an hour).
- `_evalCache` (the never-trimmed eval-cache list in `LivePerformanceTracker`) has an estimated 8–17 month RAM runway (`seat-handover-2026-08-24.md` line 112).
- The box's own memory pressure is **not** resolved: 124 MB available and 289 `pagesOUT/s` on 2026-09-10, 104 MB and 548 on 2026-09-11, 90 MB and 160 in the plan. A burst-rate rise from 9.10 % to 14.20 % is unattributed. The detached 24 h counter run that would settle it is still owed (`seat-handover-2026-09-11.md` §5.1). All readings were SSM-attached, which costs 30–50 MB.

## 7. Report back

At most 10 lines:

- Step 1: the commit, and how the fixture covers the CLI path (or why it cannot).
- Step 2: the commit, fixtures failed-then-passed, gate result.
- Step 3: the commit, plus one real run of the hook on an existing copy-back (paste its ledger row). ⚠ An old copy-back's window is past Deribit's retention; expect `LOSS` or a short venue list, and say so.
- Step 4: the commit.
- Anything that needs a trader ruling.

**Decisions taken by the orchestrator without asking (one line each):**

- The wiring fix proceeds under option A, because the local coverage report needs it and nothing is lost.
- Raw pages are kept locally, the richer option; the reason to reserve it was a write on the box, which option A removes.
- The post-fetch hook runs automatically, so samples accrue without anyone remembering; `-SkipVenueCheck` opts out.
- `V-2` and `V-3` are held, not deleted, because both are still correct for any future box-side reader.
