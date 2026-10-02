# History data store — stage-2 build — SPEC-BACK

**Spec:** [`history-data-store-spec.md`](history-data-store-spec.md) (§0 brief, §3 store, §4 comparison, §5 handles; rulings `HDS-1`–`HDS-4` in its §6). **Runbook for the trader:** [`history-store-backfill-runbook.md`](history-store-backfill-runbook.md).
**Built:** 2026-10-01 (UTC), one agent (Opus 5.5, high). **Code commit:** `d9c77a2` — local, not pushed. **Class:** tools only; no `settings.json`, no scoring code, no `analysis_log.csv` change, no change to `Core/TradeStoreWriter.vb`.

---

## 0. Outcome

| Item | Result |
|---|---|
| **The tool** | `BacktestRunner history backfill \| topup \| status \| compare \| passrule` ([`HistoryCli.vb`](../tools/BacktestRunner/HistoryCli.vb)); logic in [`HistoryStore.vb`](../tools/BacktestRunner/HistoryStore.vb) and [`HistoryCompare.vb`](../tools/BacktestRunner/HistoryCompare.vb); the one HTTP seam in [`HistoryHostHttp.vb`](../tools/BacktestRunner/HistoryHostHttp.vb). Host-agnostic; publishes as a `linux-x64` self-contained ELF |
| **Rows** | The box store's 7 columns, written by `TradeStoreWriter.FormatRow` unchanged, plus `MarkPrice,IndexPrice,TickDirection,Contracts` (`HDS-1` (b), the extra-columns ruling in [`history-data-store-spec.md`](history-data-store-spec.md) §6) |
| **Fixtures** | Family `A94` (`A94a`–`A94k`), 19 checks. Harness **494 → 513 PASS, 1 SKIP** (`A81b`, the known-defect repro behind `ORDERCHECK_KNOWN_DEFECTS`). 16 mutations run: 15 red, 1 green by design (§3) |
| **Live smoke** (this machine → `history.deribit.com`) | 2026-09-27 fetched into a scratch folder: **108,811 rows, contiguous, 0 missing**. Compared with the box copy in `aws_fetch/20260928-121255`: **PASS — 0 only in the box, 0 field mismatches, 0 only in the dev store** |
| **Measured pace** | 877–889 trades/s, 1.12–1.13 requests/s, 0 retries, 0 has_more splits, over 4 live days |
| **Measured volume** | 71,766,892 trades for 2025-01-01 → 2026-09-30 (638 days). At 880 trades/s: **≈ 22.7 h**. Store ≈ 6.0 GB (83.2 B/row); gzip ≈ 1.1 GB |
| **Escalations** | **None.** No box trade missing from the dev store, no field mismatch, no host error, no need for an engine or settings change |
| **Also built** | `tools/ops/history-backfill/` (cloud loop script, 3 SSM payloads, pull script); `tools/ops/history-compare.ps1`, wired as `collector.ps1 fetch` step 6 — inert until the dev store exists (spec §4.1 "wire it after `collector.ps1 fetch`") |

---

## 1. Verification handles

Pinned to `d9c77a2`. Paths below use `$T`, any empty temp folder. `R` = `dotnet tools/BacktestRunner/bin/Release/net8.0/BacktestRunner.dll` after `dotnet build tools/BacktestRunner/BacktestRunner.vbproj -c Release`.

**If you run only one, run H-1** — it covers the whole live path (anchors, paging, de-dup, write, checkpoint, comparison) against an independent record (the box).

| # | Handle (runnable) | Actual output | Covers |
|---|---|---|---|
| **H-1** | `R history backfill --from 2026-09-27 --to 2026-09-28 --store $T/h1` then `R history compare --box aws_fetch/20260928-121255/backtest_data --store $T/h1 --from 2026-09-27 --to 2026-09-28` (~2 min, network) | `DAY 2026-09-27 OK rows=108811 seq=301465038..301573848 missing=0 liq=0 req=139 retries=0 splits=0 ts_inversions=0 precision_loss=0 secs=123.7 rate=880/s` · `HISTORY_COMPARE verdict=PASS compared_days=1 box_rows=108811 dev_rows=108811 only_in_box=0 only_in_dev=0 field_mismatches=0 liq_box_only=0 dev_missing=0` | spec §5 `H-1`. **Identity:** box rows = dev rows = seq span 301573848 − 301465038 + 1 = 108,811 |
| **H-2** | `timeout -s KILL 45 R history backfill --from 2026-09-26 --to 2026-09-28 --store $T/h2`, then the same command without the kill; then `R history status --store $T/h2 --deep` and the 09-27 rows' SHA-256 against H-1's file | killed run: `exit=137`, store holds only the log (no month file, no checkpoint, no lock). Resume: `DAY 2026-09-27 OK rows=108811 …` · `DAY 2026-09-26 OK rows=73734 seq=301391304..301465037 missing=0 liq=1 …` · `DEEP 2026-09 rows=182545 unparseable=0 duplicates=0 missing=0` · `HISTORY_STATUS problems=0`. 09-27 rows SHA-256 `86714bd7…7e2b0c` in both stores; duplicate seqs 0. **Seam:** 09-26 last 301465037, 09-27 first 301465038 | spec §5 `H-2` (live kill fell inside the fetch; the kill between rename and checkpoint is `A94d`) |
| **H-3** | re-run H-1's backfill command | `to fetch 0 \| already complete 1` · `HISTORY_RUN exit=0 … requests=0` · file SHA-256 `f1ebd2ab…c6b0c` before and after, modified time unchanged (`2026-10-01 23:25:01`) | spec §5 `H-3` |
| **H-4** | fixture `A94c` (in H-5) | `PASS  A94c settle margin (H-4) — a run over a range reaching now writes no row at or after now − SettleMarginMs; only the two settled days are checkpointed` | spec §5 `H-4`. A live H-4 is not meaningful: every live day fetched was days old |
| **H-5** | `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release` and `powershell -File tools/checks/verify-gate.ps1 -Mode prepush` | harness: `ALL PASS`, 513 PASS, 1 SKIP. Gate: see §1.1 | spec §5 `H-5` |
| **H-6** | `R history backfill --from 2026-09-20 --to 2026-09-21 --store $T/h6` then `R replay --from 2026-09-20 --to 2026-09-21 --trade-dir $T/h6 --out $T/h6.csv` (needs this machine's gitignored `backtest_data` candles, which cover 2026-09-19 04:00 → 09-21 13:39) | `DAY 2026-09-20 OK rows=93079 seq=299905775..299998853 missing=0 liq=29 …` · `[Replay] Loaded: 1m=3460 3m=1154 5m=692 15m=231 trades=93079 funding=58` · `Rows written: 920`. File data rows: 93,079 | spec §5 `H-6`: every 11-column row read, count = file rows |
| **H-7** | `R history passrule --ledger <a ledger with one PASS row>` | `1 PASS comparison(s), need 3` · `PASS comparisons span 0.0 day(s), need 14` · `HISTORY_PASSRULE NOT_MET` | spec §4.2 pass rule |

### 1.1 Gate tail (H-5)

`powershell -File tools/checks/verify-gate.ps1 -Mode prepush` at `d9c77a2` (range `origin/master..HEAD`):

```
OK    build DeribitVerdictEngine.sln
OK    build tools/AutoTweaker/AutoTweaker.vbproj
OK    build tools/WhatIfRunner/WhatIfRunner.vbproj
OK    build tools/CeilingAudit/CeilingAudit.vbproj
OK    build tools/BacktestRunner/BacktestRunner.vbproj
OK    build verify/ordercheck/OrderCheck.vbproj
ALL PASS
OK    harness ALL PASS
OK    no snapshot/card drift detected
OK    no engine-path change
OK    AnalysisLogger.vb not in the changed set - no header rotation possible
GATE PASSED
```

### 1.2 Evidence the reader cannot re-run (`E-n`)

| # | Evidence | Why not runnable |
|---|---|---|
| **E-1** | The 16-mutation run (§3). Instrument: a scratchpad Python script that applied one string replacement, rebuilt the harness, ran it, and restored the exact prior bytes (MD5 of both source files checked after every run: `OK`) | Script not committed. ⚠ **It is not the sole cover of anything:** each mutation's target fixture is in the harness |
| **E-2** | Trade counts per sample day and the range total (runbook §1): `trade_seq` of the first trade at or after 00:00 UTC, via `get_last_trades_by_instrument_and_time`, ~19 read-only requests | Scratchpad script; reproducible by the same two-request method |
| **E-3** | `history-backfill-cloud.sh` verbs `upload`, `final`, `status` under Git Bash with `aws` stubbed to a local folder; `history-pull.ps1 -SkipDownload` on that staged copy: install with SHA-256 checks, a second run skips (`already present 1`), a changed store file stops it (`refusing to overwrite`) | Needs the stub and a staged copy; `start`/`loop` need systemd |
| **E-4** | `dotnet publish … -r linux-x64 --self-contained true` → `BacktestRunner: ELF 64-bit LSB pie executable, x86-64`, 72 MB | Build artefact; it never ran on Linux (§5) |
| **E-5** | `tools/ops/history-compare.ps1` against `aws_fetch/20260928-121255` and the H-2 store (`-SkipTopUp`): `HISTORY_COMPARE verdict=PASS compared_days=2 box_rows=182545 …`, ledger row written; with no store: `dev history store not installed … comparison skipped`, exit 0. `collector.ps1` parses (0 errors) | `collector.ps1 fetch` itself needs AWS (forbidden here) |

---

## 2. Fixtures — `A94`, each with the input that makes it fail

| Fixture | Asserts | Failing input |
|---|---|---|
| `A94a` | Paging by seq stores each seq of the day exactly once, in seq order | 50-trade millisecond groups straddling page edges; the fake host serves some seqs twice (asserted `served ≥ 2`) |
| `A94b` | A truncated page (`has_more`) is split and re-requested; a millisecond larger than a page FAILS the day, nothing written | 300-trade groups; one group of `TradesPerPage + 200` |
| `A94c` | Planner holds back days inside `SettleMarginMs`; order = never-fetched newest first, then GAP/FAILED; exact-boundary day fetched; `FetchDayAsync` refuses an unsettled day with 0 requests; a run reaching "now" writes no row ≥ now − margin (H-4) | A day ending 23 h before now; a GAP day newer than a never-fetched one |
| `A94d` | Kill at **every** request and every write stage (after tmp, after rename, after checkpoint) of 3 days across a month boundary, then re-run: month files byte-identical to an uninterrupted run, all OK, no duplicate, no tmp or lock left (30 kill points) | Kill after the rename and before the checkpoint |
| `A94e` | Re-run of completed days: 0 requests, file and checkpoint byte-identical and unwritten; with the checkpoint deleted the re-fetch REPLACES, byte-identical (H-3) | A second run over the same range |
| `A94f` | An 11-column file: header = box header + 4 columns; first 7 columns = `FormatRow`; `ReadTradeFile` and `HistoricalStore.LoadTradeRangeFrom` return every row with identity; extras round-trip, absent `contracts` stays absent (H-6) | The 4 columns placed before the identity columns |
| `A94g` | A page failing every time: 6 attempts, back-off 2/4/8/16/32 s, run STOPS (exit 2), day not checkpointed, no file; every answer followed by the 150 ms pause. A page failing twice: day OK, 2 retries | One page failing on every attempt |
| `A94h` | Box with a hole, duplicates, a seq-less legacy row and an unflagged liquidation copy → PASS (5 only in dev); FAIL on: a seq only in the box, a price mismatch, a box flag the dev store lacks, a differing DUPLICATE box row | Each of the four |
| `A94i` | Pass rule MET at 3 PASS over 14 days; NOT MET at 13 days, with one FAIL, or with 2 PASS | Each of the three |
| `A94j` | A torn row: the merge refuses (file unchanged, no tmp left); `status --deep` reports it (audit row E1, [`adversarial-audit-2026-09-24.md`](adversarial-audit-2026-09-24.md) §B) | A half-written row mid-file |
| `A94k` | A run whose month file carries the 7-column box header stops on **the header** (not the later row parse) and leaves the file unchanged | `--store` aimed at a box copy |

**Fixture-literal provenance** (`CLAUDE.md` rule): the ruled numbers (`SettleMarginMs`, `TradesPerPage`, `MaxRetries`, `FirstBackoffMs`, `PacingDelayMs`, `PassRuleMinComparisons`, `PassRuleMinSpanDays`) are read from their `Public Const`, and each such fixture says SHIPPED BEHAVIOUR. Dates, seqs, prices and group sizes are MECHANISM (the family header says so). Every `A94` fixture runs inside `A94Guard`, so an unexpected exception is a FAIL line, not a harness crash.

---

## 3. Mutation table (E-1)

| # | Mutation | Expected | Result |
|---|---|---|---|
| M1 | Key the page map on the millisecond, not `trade_seq` | `A94a` red | ✅ red (`A94a`, plus 6 others) |
| M2 | Ignore `has_more` | `A94b` red | ✅ red (both parts) |
| M3 | Planner ignores the settle margin | `A94c` red | ✅ red (planner + H-4) |
| M4 | Remove `FetchDayAsync`'s day-level settle guard | `A94c` red | ✅ red (0-requests check) — ⚠ green on the FIRST run, because the per-row guard still refused the day; the check now asserts 0 requests (§6) |
| M4b | Remove the per-row settle guard only | GREEN (shielded by M3's and M4's guards) | ✅ green — **this guard is unreachable while the other two stand; kept as a backstop, not separately proven** |
| M5 | Merge keeps the day's old rows (appends) | `A94d` red | ✅ red (`A94d`, `A94e`) |
| M6 | Checkpoint written before the merge | `A94d` red | ✅ red |
| M7 | Planner ignores the checkpoint | `A94e` red | ✅ red |
| M8 | The 4 extra columns before `TradeId,TradeSeq` | `A94f` red | ✅ red (9 failures; the merge's own re-read refuses the file) |
| M9 | Skip a range after the retries (empty page instead of stop) | `A94g` red | ✅ red |
| M10 | "Only in box" not a failure | `A94h` red | ✅ red |
| M11 | Compare one box copy per seq | `A94h` red | ✅ red (the differing duplicate passes) |
| M12 | Pass rule ignores a FAIL row | `A94i` red | ✅ red |
| M13 | Pass rule accepts a span one day short | `A94i` red | ✅ red |
| M14 | Merge skips a torn row | `A94j` red | ✅ red |
| M15 | Header check removed | `A94k` red | ✅ red (the reason becomes `unparseable row`) |
| M16 | Planner retries GAP/FAILED days first | `A94c` order red | ✅ red |

⚠ **Two fixture defects the mutation run found, both fixed before the commit:** M5, M6 and M8 first CRASHED the harness (a missing month file, a missing checkpoint key) instead of failing it — fixed by safe reads and `A94Guard`. M8 was a no-op in its first form (the replacement string equalled the original) — re-written and re-run.

⚠ **One build defect the fixtures found:** the first harness run failed `A94h`: a seq-less box row matched on its trade fields still counted its dev seq as "only in the dev store". Fixed in `HistoryCompare.Compare` before the commit.

---

## 4. Decisions taken (auto-proceed log; `CLAUDE.md` three-step test applied)

| # | Decision | Options | Pick and why |
|---|---|---|---|
| D-1 | How a day reaches its month file | (a) rewrite the month file per day, atomically · (b) per-day files, assembled later | **(a).** The month file is always the whole truth for every checkpointed day, in the layout every reader knows; no second layout. Cost < 3 % of the run (estimate, §5). Step 1: (b) records nothing more |
| D-2 | Day seq bounds | (a) spec §3.2: first trade at/after day start, last before day end · (b) anchors 60 s before the start and at the next day's first trade, contiguity checked over the whole range | **(b) — the richer option.** Measured: the time endpoint returns an arbitrary trade inside a millisecond (asc, count 1 at 2026-09-27 00:00 gave seq 301465042; the day's first is 301465038), and the start bound's inclusiveness is unknown. (b) is correct under both |
| D-3 | A day with seqs still missing after one re-request | (a) GAP: written, recorded with the count, retried every run · (b) stop the run · (c) not written | **(a).** It records the most; the checkpoint says what is missing. Transport failure still stops the run (spec §3.5) |
| D-4 | Line endings | (a) LF on every host · (b) the platform default | **(a).** The backfill runs on Linux and the top-up on Windows; (b) would mix CRLF and LF in one file and break byte-identity. Readers accept both |
| D-5 | Comparison failures beyond spec §4.2 | (a) only-in-box + 5 field mismatches · (b) also: a box flag the dev store lacks, differing flags, dev duplicates, dev missing seqs, unmatched seq-less box rows, unparseable dev rows | **(b), stricter and more truthful.** Unparseable BOX lines are a WARN, not a failure — **step 3, mechanism:** such a line has no timestamp, so no window can claim it; failing on it would fail every window of that month |
| D-6 | What "3 comparisons spanning 14 calendar days" measures | (a) run dates (`ComparedAtUtc`), last − first ≥ 14 d · (b) data windows | ✅ **(a) RULED 2026-10-02 (trader): run dates, the full 14 days, no shortening. The closing comparison runs after the holiday** (the first fetch after 2026-11-25). Comparison #1 ran 2026-10-02. No code change: (a) is what was built |
| D-7 | How the replay reads the dev store (H-6) | (a) a 5-line copy of `LoadTradeRange` in `ReplayLoop` · (b) `HistoricalStore.LoadTradeRangeFrom(dir, …)`, with `LoadTradeRange` delegating to it | **(b).** One seam, no copy that can drift. `HistoricalStore.vb` is linked into the app, but nothing in the app calls `LoadTradeRange` (grep), and the body moved verbatim. I first built (a); the three-step test said (a) was the cheaper, less self-describing pick, so I changed it |
| D-8 | Planner order | (a) newest first only · (b) never-fetched newest first, then GAP/FAILED | **(b).** Keeps `HDS-2`'s newest-first for fresh days; a chunked cloud run does not re-try one bad day at the head of every chunk |
| D-9 | One millisecond holding more than a page | (a) FAILED, not written · (b) store the truncated group | **(a).** (b) stores a known-incomplete millisecond as if complete. Never observed (0 splits live) |
| D-10 | Wire the comparison into `collector.ps1 fetch` now | (a) now, inert until the store exists · (b) at stage 3 | **(a).** Spec §4.1 asks for it; (b) is "defer until something forces it". `-SkipHistoryCompare` opts out; its verdict never changes the fetch exit code |
| D-11 | HTTP | gzip accepted, 30 s per-request timeout (not `HttpClient.Timeout`), `UseProxy:=False` | The `A93e` lesson and the WPAD lesson |
| D-12 | Transfer | Month by month gzip to S3 with a SHA-256 manifest; the pull checks both hashes and never overwrites | As ruled in `HDS-2`. ⚠ The bucket's 7-day expiry makes the pull deadline real (runbook §0) |
| D-13 | Instance | Amazon Linux 2023, `t3.small`, 30 GB `gp3`, collector subnet and security group, `EC2-SSM-Access` | SSM agent and AWS CLI are in the image; no .NET install (self-contained publish) |

### 4.1 Queued for the trader

| # | Question | My read (hypothesis) |
|---|---|---|
| **D-6** | Spec §4.2's "at least 3 comparisons spanning at least 14 calendar days": span of the **run dates** or of the **data windows**? | ✅ **RULED 2026-10-02 (trader): run dates, 14 days; the closing comparison is after the holiday.** **Run dates**, as built. The parallel run exists to catch divergence that appears over time; three comparisons run in one afternoon over old windows would not test that. Changing it is one line (`HistoryCompare.EvaluatePassRule`) plus `A94i` |

Scoping note: the `collector.ps1` change (D-10) touches the production fetch flow. It is not a reserved class (no settings, no scoring, no write to the box). Revert = one commit.

---

## 5. Spec feedback

**What the spec got right.** The paging rule in §3.2 ("page by seq, de-duplicate on `trade_seq`, split on `has_more`") and §3.7's two audit lessons (torn rows never become bounds; checkpoint only after flush and re-read) mapped one-to-one onto fixtures. The §0 escalation triggers were concrete enough to check, and none fired.

**Assumptions that broke or moved.**
- ⚠ **§3.2 "find a day's seq bounds with the time endpoint" is not exact as written.** The time endpoint's answer inside a millisecond is arbitrary (measured, D-2). The spec's method would still work when combined with the ms-widening of the seq endpoint, but only by luck of that widening; the build uses overshoot anchors instead.
- **The header-box duration worry resolves.** The box said 21 months fit in a day only if history averages ≤ ~120k trades/day. Measured mean: **112,500/day** → ≈ 22.7 h at the measured pace.
- **Size: spec §6 `HDS-2` read said ~9 GB with `HDS-1`;** measured 83.2 B/row gives ≈ 6.0 GB.
- **§5 `H-2` ("kill the backfill mid-day") cannot reliably hit the dangerous window live.** The dangerous kill is between the rename and the checkpoint, a millisecond window. `A94d` covers it by sweeping every point; the live H-2 hit the fetch.

**Where the spec was narrower than its words.**
- §3.1 names one verb (`history --to … [--from …]`). A top-up, a status check, a comparison and a pass rule are separate operations, so the build has five sub-verbs. `--to` is exclusive, matching the other verbs.
- §4.2's pass rule leaves the span reading open (D-6).

**Liquidation flags — one thing to watch, not a defect.** The 2026-09-28 validation read counted 94 flagged trades in 2026-09-27 00:00 → 2026-09-28 12:00. This build found **0 flagged on 2026-09-27** and 1 on 09-26 (29 on 09-20). That fits only if all 94 fall on 2026-09-28. ⚠ **Not checked** — the first full backfill will show it.

---

## 6. What I did not verify

| Claim | Status |
|---|---|
| Every AWS / SSM command in the runbook | **Not run** (forbidden by the brief). Syntax follows the AWS CLI and the proven `collector.ps1` pattern |
| The Linux build runs on Amazon Linux 2023 | Not run; the runbook's self-test is the first run, with an expected SHA-256 |
| The pace from London | Not measured |
| The month-file rewrite cost on a large month (Oct 2025 is the busiest sampled day, 518k trades) | Not measured; estimated under 1 h for the whole backfill |
| has_more splits and the unsplittable case against the real host | Fixtures only; 0 splits in every live run |
| Completeness of days other than 2026-09-20, 09-26, 09-27 | Not fetched; the backfill's deep check is the evidence |
| The history host's rate limits and terms of use | Not read (spec §8 says the same); pacing stays sequential at 150 ms |
| That every liquidation is flagged on the history host | Not verified (§5) |
| `collector.ps1 fetch` with the new step 6 | Parsed only; the hook script itself ran (E-5) |
| `history-backfill-cloud.sh start` and `loop` under systemd | Not run (E-3 covers the other verbs) |
