# Plan — scheduled store-vs-Deribit trade check (`BacktestRunner coverage --verify-venue`)

**Written:** 2026-09-14 (UTC), by a scoped seat working [`venue-check-schedule-plan-brief-2026-09-14.md`](venue-check-schedule-plan-brief-2026-09-14.md). **Status:** PLAN. Nothing was created on the collector box.

⛔⛔ **Headline: the check cannot be scheduled as the tool stands. Three defects block it, and two of them are measured, not suspected.**

| # | Blocker | How it was found | Effect if scheduled today |
|---|---|---|---|
| **B-1** | **The venue fetch does not paginate.** `CoverageReport.RunVenueDiffAsync` makes ONE call to `HistoricalStore.FetchTradesByTimeAsync(start, end, 1000)` | Code read, then a local run: the venue list came back at **exactly 1,000 trades** for a 24 h window | The report prints *"0 missing trade(s) in [24 h window]"* after checking about the first 40 minutes. **A silent hole that reports itself as a full-day pass** |
| **B-2** | **Memory.** The runner reads whole month files into a `List(Of TradeRecord)`, twice (hour walk, then venue diff) | Local run against the 2026-09-13 copy-back (`trades_2026-09.csv` 88.8 MB): **peak private memory 789 MB** | The box has **1,024 MB total, 90 MB available, and already pages out at 160 pages/s average** (`collector.ps1 status`, 2026-09-14 15:2x UTC). The runner would push the live collector into the pagefile |
| **B-3** | **File-share conflict with the live writer.** `TradeStoreWriter.ReadTradeFile` opens `New StreamReader(path)` (share = Read). `TradeStoreWriter.AppendRows` opens `New StreamWriter(path, append)` and on failure *"log and drop"* | Code read. **Not reproduced** | While the runner holds the month file open (seconds, on a 100 MB file), the collector's flush cannot open it and **drops that batch of tape**. The verifier would cause the loss it exists to detect. ⚠ Whether a dropped batch is retried or repaired later is **not verified** |

⭐ **All three are tool or shared-seam code, revertible, no scoring, no rendered value.** They are specced in this plan's "Spec" section and ride the S2 build — S2 is the `analysis_log.csv` header-rotation build in [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) — and its one deploy.

---

## 1. Answers P1–P7 (the brief's questions)

### P1 — where the binary comes from

- **Today: nowhere.** `tools/ops/collector.ps1` `deploy` ships a six-item allowlist only: `DeribitVerdictEngine.exe`, `.dll`, `.deps.json`, `.runtimeconfig.json`, `settings.json`, `fonts\`. Its own comment says *"never widen it here"*. No verb ships `BacktestRunner`.
- **Presence on the box: not verified.** A read-only SSM probe (search for `BacktestRunner*`, time zone, runtimes, scheduled tasks) was **denied by the session's permission classifier**. It is owed (see section 4).
- **Runtime.** `BacktestRunner.vbproj` targets `net8.0` (framework-dependent, no WinForms). The box lists `Microsoft.WindowsDesktop.App 8.0.30`. The Desktop runtime installer also installs `Microsoft.NETCore.App` — **carried from general .NET knowledge, not verified on the box** (the `status` payload filters to `WindowsDesktop`).
- **Proposed source:** a Release build of `tools/BacktestRunner/BacktestRunner.vbproj` from the same pushed commit as the S2 deploy. Files: `BacktestRunner.exe`, `.dll`, `.deps.json`, `.runtimeconfig.json`, plus the tracked wrapper `tools/ops/venue-check-task.ps1` (to be written). Target folder: `C:\DeribitEngine\tools\venue-check\`.
- **Which deploy carries it:** decision **D-1** below (reserved).

### P2 — the command and the window arithmetic

⛔ **A 24 h venue window is mechanically wrong for a scheduled run.** The code fixes the venue window to `[--to − 24 h, --to]`. With `--to` on the previous whole hour, the window start is **already more than 24 h old** when the run starts. The head of every window sits past Deribit's ~24 h retention. This is decision **D-4**.

**Proposed:** a 13 h venue window, run every 12 h, 1 h overlap. This needs a new flag `--venue-hours <h>` (default 24, so interactive use is unchanged).

| Item | Value |
|---|---|
| Triggers | **00:07 and 12:07 UTC** |
| `--to` | the trigger's previous whole hour: 00:00Z or 12:00Z |
| `--from` | `--to` − 13 h (the hour walk covers the same span as the venue window) |
| Flush-lag margin | `--to` is 7 min before the trigger; `trade_store.flush_seconds` is 30 s |
| Retention margin | window start age at trigger = **13 h 07 m**; against ~24 h retention that leaves **~10 h 53 m** for a late run. ⚠ The ~24 h figure is carried from the queue rows, **not measured** |
| Overlap | each 12 h run overlaps the previous window by 1 h (23:00–00:00, 11:00–12:00), so a boundary trade is never uncovered |
| Time zone | all arithmetic in UTC, computed by the wrapper from `[DateTime]::UtcNow`. The box's time zone affects only when the trigger fires — see P3 |

**Worked example (trigger 2026-09-16 00:07 UTC):**

```
C:\DeribitEngine\tools\venue-check\BacktestRunner.exe coverage --from 2026-09-15T11:00 --to 2026-09-16T00:00 --venue-hours 13 --verify-venue --strict --evidence-dir C:\DeribitEngine --out C:\DeribitEngine\venue_check\venue_check_20260916-00Z.md
```

- `--evidence-dir C:\DeribitEngine` is **required, not optional.** `BacktestProgram.SetWorkingDirectoryToRepoRoot` walks up looking for `DeribitVerdictEngine.sln`. On the box there is none (not verified, but no deploy ships it), so the working directory would be whatever the task sets. `--evidence-dir` pins the store (`C:\DeribitEngine\backtest_data`), `analysis_log.csv`, `ws_health.log` and `capture_marker.log` explicitly.
- `--from`/`--to` with `THH:mm` parse through `BacktestProgram.ParseDate` (`AssumeUniversal`). **Verified by the local run**, which accepted `2026-09-13T17:00`.

### P3 — the task definition

| Setting | Value | Why |
|---|---|---|
| Name | `DeribitVenueCheck` | distinct from `DeribitEngineDeploy`, the transient task `collector.ps1` creates and deletes |
| Run as | **`NT AUTHORITY\SYSTEM`** (decision **D-5**) | no stored password, no dependence on the `administrator` session that auto-logon holds. The app itself launches with `/it` into that session; this task must not |
| Logon | runs whether or not anyone is logged on (implicit for SYSTEM) | |
| Triggers | daily at 00:07Z and 12:07Z, `StartBoundary` written with an explicit `Z` | ⚠ that an explicit-`Z` boundary fires at the UTC instant on a non-UTC box is **carried, not verified**. The wrapper computes windows from UTC regardless, so a wrong fire time delays a run but cannot mis-window it |
| `StartWhenAvailable` | **true** | a trigger missed while the box was down fires at boot |
| `MultipleInstances` | `IgnoreNew` | a catch-up run and a trigger cannot overlap |
| `ExecutionTimeLimit` | 30 min | measured local run took 14.4 s for one page; a full paginated 13 h fetch is ~20–25 pages at 200 ms polite delay. 30 min is headroom, not an estimate |
| Priority | below normal (7) | the collector keeps the CPU |
| Action | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\DeribitEngine\tools\venue-check\venue-check-task.ps1` | |

**What the wrapper does, in order:**

1. **Low-memory guard.** Read available MB. Below a floor (value set from the post-fix measurement, decision **D-2**) → write a `NOT_RUN` ledger row, reason `LOW_MEMORY <n> MB`, and exit. Recorded, never silent.
2. **Catch-up.** List every expected window (`--to` on 00:00Z/12:00Z) since the last ledger row. For each one without a row:
   - window start age **< 22 h** → run it, oldest first.
   - window start age **≥ 22 h** → write a `MISSED` row. The window is gone; the ledger says so.
3. **Run** the command in P2 with `DOTNET_GCHeapHardLimit` set for the child process only (value from the post-fix measurement). A regression then fails the run, not the box.
4. **Retry once** after 10 min if the machine line reports `NOT_RUN` for a network reason, while the window start age stays < 22 h.
5. **Record** the machine line into the ledger (P4). No machine line in the output → `NOT_RUN`, reason `no VENUE_CHECK line (exit <n>)`. **Absence of evidence is recorded as not-run, never as clean.**

The 22 h cut-off leaves 2 h against the carried ~24 h retention. It is measured once before arming (section 4, check 5).

### P4 — output

| Item | Value |
|---|---|
| Folder | `C:\DeribitEngine\venue_check\` |
| Per window | `venue_check_<to:yyyyMMdd-HH>Z.md` (the `--out` markdown) and `venue_check_<to:yyyyMMdd-HH>Z.log` (stdout, stderr, `EXIT=<n>`) |
| Ledger | `venue_check\venue_check_ledger.csv`, one row per expected window, including `MISSED` and `NOT_RUN`. Columns: decision **D-7** |
| Missed-run visibility | the ledger carries a row for **every** expected window, so a gap is a written `MISSED` row, not an absent file. If the task itself is gone, nothing writes; the P5 staleness rule catches that |
| Retention | **keep everything, never auto-delete.** A window cannot be re-verified after ~24 h, so these files are unrepeatable evidence. Size not measured; two small markdown files per 12 h |

### P5 — how the trader reads it

**The line for `aws-collector-deploy-checklist.md` §3 (the daily one-glance health check).** Add it at arming time, not before:

> - **`venue_check\venue_check_ledger.csv` last two rows** → both `CLEAN`, and the newest `window_to_utc` is no more than 13 h old. **Any `LOSS`, `INEXACT`, `NOT_RUN` or `MISSED` row, or a newest row older than 13 h, needs action today.** Venue history is gone after ~24 h, so the window cannot be re-checked later.

**Surfacing it without RDP (tooling changes, land with the build, auto-proceed class):**

- `ssm-apphealth.json` (the payload behind `collector.ps1 status`): append one command that prints the ledger's last two rows, or `venue_check ledger absent`. Read-only on the box.
- `collector.ps1` `fetch`: add `venue_check` to `$FetchDirs`, so every copy-back carries the full ledger and reports. This is the **fetch** list, not the deploy allowlist.

### P6 — the `--strict` gap

**Today:** `If strict AndAlso covResult.CountByClass(HourClass.Defect) > 0 Then Return 1`. The venue result is never read. The local run printed `venue diff (S0) 1000 missing trade(s)` and then `VERDICT: 0 defect hour(s)` — **the verdict line ignores venue loss too, not only the exit code.**

| Option | What it is | Read |
|---|---|---|
| **(a)** | The wrapper parses the human console line `venue diff (S0) N missing trade(s)` | ⛔ A regex over a display string. It matches a string that mentions the property, and drifts the first time the line is reformatted |
| **(b)** ✅ | Extend `--strict`: distinct exit codes for venue outcomes, **plus** one machine-readable `VENUE_CHECK` line that the wrapper records | **Take (b).** The truth lives in the tool, so a future seat reading `BacktestProgram.vb` learns what a venue failure is. Not a cost trade: (a) is cheaper and less truthful, (b) is the more truthful option, so no reservation fires |
| (c) | Both | Redundant once (b) exists — the wrapper reads the machine line, which is (b) |

Logged as an auto-proceeded decision (tool code, one revert, no live surface). Spec in section 2.

### P7 — failure modes

| Failure | Today's behaviour | Planned behaviour |
|---|---|---|
| **Task never ran** — box down at the trigger | nothing | `StartWhenAvailable` fires at boot; wrapper catch-up runs every window still < 22 h old and writes `MISSED` for the rest |
| **Task deleted or disabled** | nothing | nothing writes. **Caught only by the P5 rule "newest row older than 13 h"**, via `status` or the glance |
| **Deribit unreachable** | `FetchTradesByTimeAsync` returns `Nothing`; stderr `S0 not run`; **exit 0** even under `--strict` | `NOT_RUN` (exit 4), one retry after 10 min, then catch-up on the next trigger while < 22 h |
| **Fetch fails mid-pagination** | n/a (no pagination) | `NOT_RUN`. **A partial venue list is never diffed** — a partial list under-reports loss |
| **Store file locked or mid-write** | `ReadTradeFile` catches the exception, prints to stderr, returns an **empty or partial list**. Exit 0. The report then shows false loss or an undercount. **And B-3: the collector's flush can drop tape while the runner holds the file** | Reader opens with `FileShare.ReadWrite` (removes B-3). Any read exception → `NOT_RUN`, reason `STORE_READ` |
| **Month rollover** (window spans two files) | `EnumerateMonths` reads both; a new month's file that does not yet exist reads as empty, which is correct | unchanged. A half-written last line at EOF fails `TryParseRow` and is skipped; the window ends ≥ 7 min before the trigger, so no in-window row is still being written |
| **Out of memory** | would page the collector (B-2) | windowed streaming read; `DOTNET_GCHeapHardLimit` turns a regression into an exception; `Main` returns 1 with no machine line → wrapper records `NOT_RUN` |
| **Store rows without `trade_id`** in the window | printed `*** diff is NOT exact while this is non-zero ***`, exit 0 | `INEXACT` (exit 5) |

---

## 2. Spec — `--verify-venue` fixes and `--strict` option (b)

**Do not build in this seat. Build before the S2 deploy, from the same commit.**

**Model / effort: Opus, high.**

- **Why that tier:** four small changes, but three are trap-shaped — pagination boundaries, file-share semantics, and a verdict that must never map "not run" to "clean". A wrong answer on any of them reads as a pass.
- **Where it slips:** (1) The pagination cursor. `BackfillTradeMonthAsync` uses `cursorMs = newestMs + 1`, which **skips any trade sharing the last page's final millisecond but falling on the next page**. Do not copy it into the venue path. (2) Treating `trades.Count < 1000` as the end, when the response may carry an explicit end flag. (3) Writing a share-mode fixture that opens both handles from the same `FileStream` class in a way that cannot fail. It must reproduce the IOException on the OLD open first.
- **Escalate:** if the fixtures pass without first failing against the unfixed code, stop. The fixture shape does not admit the failure.
- **Session split:** one session. Order: V-3 (shared seam, smallest), V-1, V-2, V-4, then the local memory measurement.

| # | Change | Detail |
|---|---|---|
| **V-1** | **Paginate the venue fetch** (fixes B-1) | New `CoverageReport.FetchVenueWindowAsync(startMs, endMs)`. Loop pages ascending. Cursor = `newestMs` (**not** +1); de-duplicate across pages by `trade_id`. Stop on an empty page or a short page. ⚠ Whether the endpoint returns `has_more` is **not verified** — check one live response at build time and prefer the explicit flag if present. Any page returning `Nothing`, or the page cap, → the whole fetch is **failed**, never partial. Report `pages`, `venue_first_ts`, `venue_last_ts` |
| **V-2** | **Windowed streaming store read** (fixes B-2) | New `TradeStoreWriter.ReadTradeFileWindow(path, startMs, endMs, ByRef ok As Boolean)`. One streaming pass; keep only rows inside the window; `ok = False` on any exception. Use it in `RunVenueDiffAsync` **and** in the coverage hour walk and `AccumulateSequenceGaps`, which today materialise whole months. Acceptance: re-run the local measurement below; **peak private memory must be reported in the build's spec-back**, and the `DOTNET_GCHeapHardLimit` and low-memory floor are set from it |
| **V-3** | **Reader share mode** (fixes B-3) | `ReadTradeFile`, `ReadTradeFileTail`, the new windowed reader and the other `StreamReader(path)` sites in `TradeStoreWriter.vb` (lines ~586, ~617, ~647, ~967 at `462b483`) open `New FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)`. ⚠ This file is linked into the **engine binary**, so the change ships with the S2 engine deploy. No scoring, no rendered value. Fixture: hold a reader open and assert `AppendRows` writes; run it against the old open first and watch it fail |
| **V-4** | **`--venue-hours` and `--strict` option (b)** | `--venue-hours <h>`, default 24, integer 1–24, window `[--to − h, --to]`. When `--verify-venue` is passed, always print one line: `VENUE_CHECK verdict=<v> from=<iso> to=<iso> venue=<n> identity=<n> fallback=<n> missing=<n> legacy=<n> pages=<n> first=<iso> last=<iso> reason=<text>`. Verdicts, most severe first: **`NOT_RUN`** (fetch failed, store read failed) → **`LOSS`** (missing > 0) → **`INEXACT`** (store rows without `trade_id` > 0) → **`CLEAN`**. Under `--strict`: `NOT_RUN` = exit 4, `LOSS` = 3, `INEXACT` = 5; otherwise the existing Defect rule (exit 1). The console `VERDICT:` line names the venue verdict too |

**Fixtures** go beside `A49j_S0VenueDiffEnumeratesExactly` and `A53g_VenueDiffSeparatesIdentityAndFallbackMatches` in `verify/ordercheck/Program.vb` (fixture IDs taken from the tree at `462b483`, not from a spec). Minimum set: three stub pages with a same-millisecond pair straddling a page boundary; a failing second page → `NOT_RUN`; each verdict mapping and its exit code; windowed read excludes out-of-window rows and reports `ok = False` on an unreadable file; the share-mode test above.

**Local measurement (acceptance for V-2):** the command used this session, re-run on the fixed build.

```
tools\BacktestRunner\bin\Release\net8.0\BacktestRunner.exe coverage --from 2026-09-13T17:00 --to 2026-09-14T17:00 --verify-venue --evidence-dir aws_fetch\20260913-153704
```

Sample `PrivateMemorySize64` every 100 ms. Baseline before the fix: **789 MB peak, 14.4 s, venue list 1,000 trades**. Use the largest available month file (a month-end copy-back) for the final figure.

---

## 3. D-table

| # | Decision | Options | Read | Class |
|---|---|---|---|---|
| **D-1** | How `BacktestRunner` and the wrapper reach the box | **(a)** a new `collector.ps1` verb `deploy-tool` with its own allowlist (4 runner files + wrapper) into `C:\DeribitEngine\tools\venue-check\`, hash-verified like `deploy`, **no app stop** · (b) widen `deploy`'s six-item allowlist · (c) a one-off manual SSM/S3 copy | **(a).** (b) breaks the allowlist's positive-record rule, and a runner file would then gate an engine deploy. (c) leaves no tracked record and no hash check. (a) runs in the same window as the S2 deploy, from the same commit, but does not stop the collector | ⛔ **RESERVED** — deploy tooling and a write to the box |
| **D-2** | Run on the box at all, given 90 MB available and existing paging (re-opens the "AWS only" ruling with new data) | **(a)** keep AWS-only, after V-2, with `DOTNET_GCHeapHardLimit` and a recorded low-memory skip · (b) resize the instance · (c) run locally against a scheduled `fetch` · (d) run on the box as the tool stands | **(a).** (d) is mechanically wrong: 789 MB into a 90 MB margin risks the live tape. (c) breaks the AWS-only ruling and the local machine is not 24/7. ⚠ **(b) is a separate question the trader should see anyway:** the box already pages out at 160 pages/s average with a 2,413 peak, before this task exists | ⛔ **RESERVED** — box resources and a prior ruling |
| **D-3** | Reader share mode in `TradeStoreWriter` (V-3) | **(a)** readers open with `FileShare.ReadWrite` · (b) the runner copies the file first · (c) accept the conflict | **(a).** (b) is mechanically no fix: `File.Copy` opens its source with `FileShare.Read`, the same conflict. (c) lets the verifier drop tape | Shared seam in the engine binary; ships only with a reserved deploy. **Trader to see**, because it changes a file the live writer uses |
| **D-4** | Cadence and window | **(a)** 13 h window every 12 h, 1 h overlap, catch-up < 22 h, `MISSED` rows · (b) 24 h window once a day | **(a).** (b) is mechanically wrong: the window head is always past the ~24 h retention at run time. One missed daily run is also a permanent 24 h hole under (b); under (a) the catch-up recovers it | ⛔ **RESERVED** — defines the task on the box |
| **D-5** | Run-as account | **(a)** `SYSTEM` · (b) `administrator` with a stored password · (c) `administrator` interactive `/it`, like the app launch | **(a).** (b) needs a new credential on the box (a brief escalation condition). (c) dies with the session and runs in the collector's desktop | ⛔ **RESERVED** — task on the box |
| **D-6** | `--strict` gap | see P6 | **(b)** | Auto-proceeded (tool code) |
| **D-7** | Ledger schema | columns: `run_utc, window_from_utc, window_to_utc, verdict, venue_trades, identity_matched, fallback_matched, missing, store_legacy_only, pages, venue_first_ts, venue_last_ts, exit_code, reason` | Take it. Every column is on the `VENUE_CHECK` line except `run_utc`, `exit_code` and `reason`, which the wrapper owns | ⛔ **RESERVED** — a new CSV schema |

**Auto-proceeded, one line each:**

- `--venue-hours` flag rather than re-purposing `--from` as the venue start — keeps interactive `coverage` semantics unchanged; no information given up.
- Keep all output, no pruning — the richer option; the evidence is unrepeatable.
- Fixes ride the S2 build and deploy — per the brief; V-3 is engine-binary and needs a deploy regardless.

---

## 4. Checks owed before arming, in order

| # | Check | Why |
|---|---|---|
| 1 | Build V-1 to V-4; fixtures fail first, then pass; gate passes | section 2 |
| 2 | Local memory re-measurement on the fixed build; record peak | sets D-2's heap limit and floor |
| 3 | **Read-only box probe** (denied this session): time zone, `dotnet --list-runtimes` in full, disk free, existing scheduled tasks, whether any `BacktestRunner` already exists | P1, P3 |
| 4 | Read what `AppendRows`' *"log and drop"* path loses, and whether gap repair refills it | sizes B-3's real impact to date. **Not needed to justify V-3**, which is right either way |
| 5 | Measure Deribit trade retention once: fetch windows starting 23 h and 25 h back, compare first trade timestamps | confirms D-4's 22 h cut-off |
| 6 | Trader rules D-1, D-2, D-3, D-4, D-5, D-7 | reserved |
| 7 | At the S2 deploy window: `deploy-tool`, register the task, **watch the first run by hand**, then add the P5 glance line to `aws-collector-deploy-checklist.md` | |

---

## 5. Verified vs carried

**Verified this session:**

- `date -u` = 2026-09-14 15:16 UTC at start.
- `RunVenueDiffAsync` single call with `count=1000` — code read (`CoverageReport.vb` ~line 1266) **and** a local run returning exactly 1,000 venue trades for a 24 h window.
- Peak private memory 789 MB, 14.4 s — local run, 100 ms sampling, `aws_fetch\20260913-153704`.
- `--strict` reads Defect hours only — `BacktestProgram.vb` line 385; the local run's `VERDICT:` line ignored 1,000 missing trades.
- Reader/writer open modes — `TradeStoreWriter.vb` lines 586 and 1064. The *conflict* is inferred from .NET share semantics; **not reproduced**.
- Box memory and store sizes — `tools/ops/collector.ps1 status -InstanceId i-0d6c133058876273e`. I read `ssm-mem.json` and `ssm-apphealth.json` first; both are read-only (`Get-Process`, `Get-Content`, CIM, `Get-Counter`, `quser`).
- The deploy allowlist has no `BacktestRunner` entry — `collector.ps1` lines 83–94.

**Carried, not verified:**

- Deribit's ~24 h public-trade retention (from the queue rows).
- `Microsoft.NETCore.App` present on the box; box time zone; no existing `BacktestRunner` on the box.
- A `Z`-suffixed `StartBoundary` fires at the UTC instant.
- Whether the trades endpoint returns an explicit `has_more` flag.
- Whether `AppendRows`' dropped batches are ever repaired.

**Noticed, out of scope:** the CLI calls `CoverageReport.BuildResult` without its optional `venueLogPath`, so `venue_status.log` is never read and `OutOfScopeVenue` can never fire from `BacktestRunner coverage` (`BacktestProgram.vb` line 358 vs `CoverageReport.vb` line 1299). Whether that is intentional was not checked.
