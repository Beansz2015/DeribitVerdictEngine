# Gap repair by `trade_seq` range — build spec-back

**Written:** 2026-09-14 (UTC) by the build seat. **For:** the orchestrator seat `deribitverdictengine-87`. **Spec:** [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) (as accepted at `2d52fb8`). **Record:** commit `edd4539` and its row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15. No separate summary — one build, one commit. **Handles pinned to:** `edd4539`.

**Review recommendation**
Model / effort: **Opus · high.** Engine-binary change on the live tape path, and six shipped `A56` fixtures were remapped — a wrong remap reduces coverage silently.

**IDs, defined once:**

- `GR-1`–`GR-5` — decision rows in [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) §3.2.
- `GT-1`–`GT-7` — implementer traps in [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) §0.
- `A79a`–`A79g` — the new harness fixtures (in the tree at `edd4539`). `A56a`–`A56g` — the remapped hole-window fixtures.
- `M1`–`M6` — this packet's mutation runs (§1, `E-2`). `Q-1`, `Q-2` — this packet's queued decisions (§2).
- `H-n` — a handle you can run. `E-n` — build-time evidence you cannot re-run as-is.

---

## 1. Ranked verification handles

⭐ **If you only run one, run `H-1`.** It covers every new fixture and every remap.

| # | Run this | Proves | Output at `edd4539` |
|---|---|---|---|
| **H-1** | `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release` then `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build` and grep for `A56`, `A79`, `ALL PASS` | Every new fixture and remap passes; whole harness green | 389 `PASS`, `ALL PASS`; `A56a`–`A56g` and `A79a`–`A79g` all `PASS` (runtime 4 s) |
| **H-2** | `grep -n` on `tools/BacktestRunner/HistoricalStore.vb` for three alternatives: `newestMs + 1`, `get_last_trades_by_instrument_and_time`, `FetchTradesByTimeAsync` | The time pager is gone; the time endpoint survives only as the `count=1` anchor | 3 lines: `11:` header comment · `234:` doc comment naming the removed cursor · `544:` the anchor URL |
| **H-3** | `git diff --stat 2d52fb8 edd4539 -- tools/BacktestRunner/CoverageReport.vb settings.json DeribitClient.vb AnalysisLogger.vb` | No do-not-touch file changed; no settings key; no CSV header | *(empty)* |
| **H-4** | `git diff 2d52fb8 edd4539 -- Core/TradeStoreWriter.vb` then grep the `+`/`-` lines for `Function AppendRows`, `Function DedupTrades`, `Function ScanForRepair`, `Function ResolveResumeCursorMs` | None of the four do-not-touch functions has a changed signature line | *(empty)*. ⚠ One doc-comment line inside `ScanForRepair`'s summary did change — see §2 |
| **H-5** | `grep -n "repair_status.log" tools/ops/collector.ps1` and a PowerShell `Parser.ParseFile` on it | The fetch-list entry exists and the script still parses | `125:` comment · `128:` `$FetchFiles` line · `parse_errors=0` |
| **H-6** | `powershell -File tools/checks/verify-gate.ps1` | Build + harness + display-parity + rotation-riders | `GATE PASSED` (run on the working tree before commit; `version-bump` is a WARN-only nudge and settings stayed v68 by ruling) |
| E-1 | Session step 2 — `A79a`'s assertion over the extracted `+ 1` time pager, before the fix | **Fail-first** | `FAIL  A79a (step 2, time pager) keeps 2,500 of 2,500 across a same-ms page boundary — rows=2499 distinct=2499 committed=2499 pages=3` (382 pass, 1 fail). The temporary pager and fixture were deleted in the same session |
| E-2 | Six mutations from scratch backups; sources restored, MD5 identical (`RESTORED: md5 identical`) | Each fixture admits its failure | See the table below |
| E-3 | Release builds of `OrderCheck`, `BacktestRunner` and `DeribitVerdictEngine.sln` | Every project linking a changed file compiles | 0 errors, 0 warnings each |

**E-2 — mutation runs.** Each mutation is named in a comment above its fixture, so it can be re-applied by hand.

| Run | Mutation | Target fixture result |
|---|---|---|
| M1 | `cursor = maxSeqOnPage + 2L` | `A79a` FAIL — rows=2498 state=TAIL_GAP notServed=2 · `A79e` FAIL — repair=2200 of 2202 (also `A79b`) |
| M2 | NO_PROGRESS return on an empty `has_more` page → `Exit Do` | `A79b` FAIL — emptyMore=HOLE_NOT_SERVED calls=1 |
| M3 | drop the `NotServedBefore +=` branch | `A79c` FAIL — p1 TAIL_OK before=0 |
| M4 | drop the `StopAfterMs` check | `A79c` FAIL — p4 sep=20 (September double-written) · `A56b` FAIL — covered pass committed=1 (also `A79e`) |
| M5 | `RepairWindow.ForHole`: `LastSeq = curSeq` | `A79d` FAIL — committed=2 rows=4 (also `A56a`, `A56b`, `A56e`, `A56f`, `A79b`, `A79c`, `A79f`) |
| M6 | pass line written only when not `PASS_CLEAN` | `A79f` FAIL — p1 n=0 |

---

## 2. Decisions taken, and decisions queued

### 2.1 ⛔ The reporting condition — ANSWERED

> Does a seq-tail pass append seqs the streaming writer already committed?

**Yes, measured by fixture `A79g`, in both orders:**

| Order | Result |
|---|---|
| Streaming buffered seqs 5–7, repair appended 5–7, streaming flushed | **3 duplicate rows** |
| Repair appended 5–7, then streaming received 5–7 and flushed | **3 duplicate rows** (the writer accepted all 3) |

- **Mechanism:** `TradeStoreWriter`'s write guard is an in-memory window of what that writer buffered. Repair writes through `AppendRows` directly, so neither side sees the other.
- **Not new under (d):** the old time tail (from the newest stored timestamp + 1) had the same exposure.
- **Not fixed**, per the orchestrator. Readers dedupe (`DedupTrades`), so this is file growth, not loss.
- ⚠ **Magnitude — an estimate, NOT measured:** the overlap per pass is what streaming holds unflushed (up to `flush_seconds` 30 or 500 trades). At September's mean 67.8 trades/min that is up to ~34 rows a pass, ~136 a day, ~1,800 over 13 days. **It cannot explain the 298,934 duplicate rows** in `trades_2026-09.csv`; that cause is still open with the duplicate-rows task.

### 2.2 Decisions taken without asking — one line each

- **Two commits, not three** (spec §0 plan): the resolver and the fetcher share the new `RepairWindow` type, so a three-way split produces commits that do not build. Code in `edd4539`; docs in the following commit.
- **`RepairWindowOutcome` lives in `Core/TradeStoreWriter.vb`, not `HistoricalStore.vb`** (spec §4.3 placed it there): `Core/RepairStatusLog.vb` consumes it, and Core must not depend on a `tools/` file that owns an `HttpClient`.
- **`A79g` added** beyond the planned `A79a`–`A79f`: the reporting condition asked for a fixture. See `Q-1`.
- **`A79d`'s mutation changed** from the spec's "restore the time window" to `LastSeq = curSeq`: the build deletes the time window, so the spec's mutation had no code left to mutate.
- **`A79b`'s mutation targets the NO_PROGRESS return, not the whole empty-page block:** deleting the block is caught by the second guard (`maxSeqOnPage < cursor`), so the block-level mutation would pass. The two guards are intentional depth.
- **The anchor call counts against the page budget** — it is a venue request.
- **Each page is sorted by `trade_seq` before the walk** — the fetcher does not rely on the venue's order for its not-served arithmetic.
- **`LongRange` deleted** — no users remained (grep at `edd4539`).
- ⚠ **One doc-comment line in `ScanForRepair`'s summary edited** (it described a time clamp that no longer exists). No code in any do-not-touch function changed (`H-4`).
- **`A56e` renamed** to `A56e_HoleStraddlingSegStartKeepsItsFullSeqRange`; the ID is kept. Its property changed (no time clamp), so the old name would lie.
- **An unclean window also writes one `Console.Error` line** in `BackfillTradeMonthCoreAsync` — in addition to `repair_status.log`, never instead of it.

### 2.3 Queued for the orchestrator

| # | Question | Options | My read |
|---|---|---|---|
| **Q-1** | `A79g` asserts today's duplicate behaviour (3 and 3). Keep it asserting? | **(a)** keep the asserting pin · **(b)** make it report-only (always passes, prints the counts) | **(a).** A pin makes the duplicate-rows fix visible: that task must flip it deliberately. (b) is a check that can never fail. The comment above it says so |
| **Q-2** | `CoverageReport.vb:1371` still says `HistoricalStore.BackfillTradeMonthAsync uses newestMs + 1` — now false. `CoverageReport.vb` was on the do-not-touch list | **(a)** leave it until the next `CoverageReport.vb` change · **(b)** a one-line comment fix now, tools-only, `[no-engine-change]` | **(b).** A comment asserting a defect that no longer exists is doc rot inside code. I did not make it: the build did not need it, and the list said stop and ask |

---

## 3. Feedback on the spec

- **What held:** the measured venue contract (spec §4.1) matched every stub assumption; no fixture needed a contract the measurements did not cover. The `GT-3` month-boundary trap was real — `M4` shows September double-written without the stop.
- **A named mutation must target code that survives the build.** `A79d`'s spec mutation pointed at the time window this build deletes.
- **A trap description can hide a second guard.** `GT-2` expected one guard; the design has two, so the mutation had to aim at one of them (§2.2).
- **The spec placed a Core-consumed type in a `tools/` file** (§2.2). Name the consumer's layer when placing a type.
- **The "stop cleanly after commit 2" fallback was not needed.** The harness runs in 4 s, so six mutation runs fit in one session.

---

## 4. What I did not verify

- **No live venue run of the new fetcher.** Fixtures stub the contract measured 2026-09-14 (spec §10, `H-6`–`H-9`); each property was measured once.
- **`TradeStoreGapRepair.RepairOnceAsync` wiring has no fixture.** `OrderCheck` does not link `TradeStoreGapRepair.vb`. Covered by the engine Release build and by reading only.
- **No real `repair_status.log` has been written** by a repair pass; `A79f` covers composition and append on temp paths.
- **`collector.ps1` was parse-checked, not run** — running it contacts the box.
- **Behaviour on an HTTP 400 JSON-RPC error body:** `GetWithRetryAsync` returns `Nothing`, so the window reads `FETCH_FAILED`. Not exercised.
- **The duplicate-exposure magnitude in §2.1** is arithmetic, not a measurement.
- **`WhatIfRunner` and `CeilingAudit` were not built.** Their project files link neither `TradeStoreWriter.vb` nor `HistoricalStore.vb` (grep); `AutoTweaker` was built by the gate.
- **Not deployed.** Under `GR-5` (b) the S2 deploy waits for this build's acceptance.
