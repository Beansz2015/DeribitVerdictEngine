# Venue-check build — spec-back (option A)

**Written:** 2026-09-14 (UTC) by the implementer seat. **For:** the orchestrator seat of 2026-09-14b. **Record:** [`venue-check-build-summary.md`](venue-check-build-summary.md). **Brief reviewed against:** [`venue-check-plan-review-2026-09-14.md`](venue-check-plan-review-2026-09-14.md). **Handles pinned to:** `b441fab`.

**Review recommendation — model: Opus · effort: medium.** The code is fixture-covered and each fixture was shown to fail. The judgment left is the four rulings in section 2; R-2 is the one that needs care.

---

## 1. Ranked verification handles

`H-n` = you can run it. `E-n` = build-time evidence you cannot re-run as-is.

| # | Run this | What it proves | Output at `b441fab` |
|---|---|---|---|
| **H-1** | `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release` then `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build \| grep -E "^(PASS\|FAIL)  A78\|FAILURE\|ALL PASS"` | All five new fixtures pass, whole harness green | `PASS A78a` · `PASS A78b` · `PASS A78c` · `PASS A78d` · `PASS A78e` · `ALL PASS` |
| **H-2** | `git diff --stat fb780d8..b441fab -- Core/ DeribitVerdictEngine.vbproj tools/BacktestRunner/HistoricalStore.vb settings.json` | No engine-binary file and no settings touched — the no-engine-change claim | *(empty)* |
| **H-3** | `git check-ignore -v aws_fetch/venue_check_ledger.csv` | The ledger location is gitignored, as the brief required | `.gitignore:440:aws_fetch/	aws_fetch/venue_check_ledger.csv` |
| **H-4** | `curl -s "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time?instrument_name=BTC-PERPETUAL&start_timestamp=<now−30h ms>&end_timestamp=<now−29h ms>&count=5&sorting=asc"` | The venue returns an empty list past retention — the reason `VENUE_SHORT` exists | `"trades":[],"has_more":false` |
| **H-5** | `powershell -File tools/ops/venue-check.ps1 -FetchFolder aws_fetch/20260913-153704 -ToUtc 2026-09-13T15:00:00Z -LedgerPath <scratch.csv>` | The hook end to end: build, run, four outputs, one ledger row | the row in the summary; `VENUE_SHORT`, exit 2 |
| E-1 | Mutation run (scratch backup of `CoverageReport.vb`, not committed) | Each fixture admits its failure | `A78b` 2,499/2,500 under `cursor = newest + 1` · `A78c` retention case `CLEAN` with the span arm removed · `A78d` `NOT_RUN "cannot access the file"` with a plain `StreamReader` · `A78e` `InvalidDataException` without gzip. Restored file MD5 identical (`3e1c08ca…`). Each mutation is named in a comment above its fixture, so it can be re-applied by hand |
| E-2 | Step 1's copied-bug run | `A78a` fails when the overload drops `VenueLog` | `hour10=UnknownScope` |

## 2. Rulings requested

| # | Question | Options | My read | Class |
|---|---|---|---|---|
| **R-1** | How does the dated sample review (review brief §5) treat `VENUE_SHORT`? | (a) like `NOT_RUN` — not a valid sample · (b) count it | **(a).** It means the venue list did not cover the window, so its missing count proves nothing. Needs one row added to the review brief §5 table and the `trader-tick-queue.md` §4 reminder's "valid samples" wording | Orchestrator doc — I did not edit either, per the brief |
| **R-2** | ⛔ **A `LOSS` row with `seq_contiguous = true` can be an edge effect, not a false assumption.** The store's `trade_seq` walk only sees holes **between** the store's first and last sequence in the window. A trade lost in the first or last seconds of the window sits outside that span, so the walk cannot see it | (a) add a field `missing_inside_seq_span` (missing venue trades whose `trade_seq` lies strictly inside the store's first..last sequence) to the `VENUE_CHECK` line and the ledger · (b) leave it; the reviewer checks the dump by hand · (c) leave it | **(a).** Only a missing trade inside the span disproves the assumption. Under (b) the ledger carries a row that looks decisive but may not be. The ledger has zero rows, so a column costs nothing now | ⛔ **Reserved** — ledger schema change. This also changes the review brief §5 key read |
| **R-3** | Should the ledger carry `store_trades`, `store_outside_venue_span` and `dump`? They are on the `VENUE_CHECK` line and in the `.log`, not in the ledger | (a) add them · (b) keep the brief's 17 columns | **(a).** A `VENUE_SHORT` row is otherwise explained only by free text in `reason`. Same zero-row timing argument as R-2 | ⛔ **Reserved** — ledger schema |
| **R-4** | When does the first **live** `CLEAN`/`LOSS` run happen? No copy-back is within retention, so neither verdict has run end to end | (a) at the next routine fetch · (b) a dedicated fetch now | **(a).** The absorption `D-1` read (the five diagnostic CSV columns) already needs a fetch from 2026-09-16 UTC. A dedicated fetch costs the box an SSM session (30–50 MB, `venue-check-plan-review-2026-09-14.md` §6) for no extra information | Orchestrator sequencing |

**Decisions I took without asking (one line each):**

- **Added `VENUE_SHORT`** (exit 6), ranked above `LOSS`. The richer option; without it a retention-truncated window reads `CLEAN` (H-4).
- **Own HTTP fetch in `CoverageReport.vb`.** `HistoricalStore.vb` is linked into the engine binary. Mechanism argument: the richer shared path was forbidden by the no-engine-change ruling.
- **Tools-only windowed store reader** with `FileShare.ReadWrite` for the venue path. `V-4`'s "store read failure → `NOT_RUN`" needs a reader that reports failure; the Core reader swallows exceptions and is `V-3`'s held scope.
- **Diff counts read `na` when not run**, never 0.
- **The hook rebuilds `BacktestRunner` every run**, so `tool_commit` names what ran; `-dirty` marks uncommitted code.
- **The venue verdict never changes `collector.ps1 fetch`'s exit code.** The fetch succeeded; the script prints loudly and records the verdict.
- **The demonstration run used a scratch ledger**, so the real ledger holds no deliberately stale sample.

## 3. Feedback on the brief and plan

- **The plan's `V-1` inherited a false premise about the cursor.** `venue-check-schedule-plan.md` §2 said to avoid `newestMs + 1` but left `start_timestamp` semantics unverified. Measured inclusive, so "restart at the newest ms, dedup by id" is correct. `HistoricalStore.BackfillTradeMonthAsync` still uses `+1` — **that is the live repair path.** A trade sharing a page's last millisecond that did not fit on the page is skipped by repair. ⚠ Worth its own look; out of this build's scope, and `HistoricalStore.vb` is engine-binary.
- **The brief's verdict set had no "venue did not cover the window" state.** Deribit's empty-list-past-retention behaviour makes that state common for any late run. See R-1.
- **The brief's key read (review brief §5 "LOSS with `seq_contiguous = true`") is weaker than it looks.** See R-2.
- **The brief said "no step touches an engine-binary file".** True, but only because the venue fetch was duplicated. The plan's §2 `V-1` text ("new `CoverageReport.FetchVenueWindowAsync`") did not anticipate that `HistoricalStore` is linked into the app.
- **B-3 is now half-reproduced.** `A78d`'s mutation showed a plain `StreamReader` throws beside an open writer handle. The other half — the writer failing beside a reader and dropping tape — is still inferred.

## 4. Not verified

- **A live `CLEAN` or `LOSS` verdict** — no copy-back is inside retention (R-4).
- **`collector.ps1 fetch` calling the hook** — the call site is parse-checked, never executed (running it contacts the box).
- **A multi-page live fetch.** Pagination is fixture-proven on a stub honouring the verified venue contract; a real 13 h window (~20 pages) has not run.
- **`MarkToolCommitDirty` ordering on a clean machine.** Verified on this machine only: `…-dirty` on a dirty tree, clean hash (`9b6fd0b0f753`) after commit.
- **The one CLI line in `BacktestProgram.vb` that calls `BuildResult(opts, paths)`** — no fixture reaches it; the harness does not link that file.
- **Whether `A78d`'s exclusive-lock case matches a real collector lock pattern** — it proves the failure path, not that such a lock occurs.
