# Gap repair — a failed store scan is loud; repair scans share the file — build spec-back

**Written:** 2026-09-15 (UTC) by the build seat. **For:** the orchestrator seat `deribitverdictengine-b6`. **Spec:** [`gap-repair-scan-failure-spec.md`](gap-repair-scan-failure-spec.md) (`5b8febb`). **Record:** commit `fd94e0c` and the extended gap-repair row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15. **Handles pinned to:** `fd94e0c`.

**Review recommendation**
Model / effort: **Opus · high.** Engine-binary change on the live tape path; the share-mode change introduces a new read hazard (a torn final row) that this build closes.

**IDs, defined once:**

- `DUP-1`, `DUP-2` — the trader-ruled decision rows in [`trade-store-duplicate-rows-read-2026-09-15.md`](trade-store-duplicate-rows-read-2026-09-15.md) §7.
- `SF-1`–`SF-8` — decision rows in [`gap-repair-scan-failure-spec.md`](gap-repair-scan-failure-spec.md) §3.
- `A79l`–`A79p` — the new harness fixtures. `ML`, `MM`, `MN`, `MP` — this packet's mutation runs.
- **B-3** — the file-sharing collision in [`venue-check-schedule-plan.md`](venue-check-schedule-plan.md): a plain `StreamReader` cannot open beside a writer, and a writer cannot open beside it.

---

## 1. Ranked verification handles

⭐ **If you only run one, run `H-1`.**

| # | Run this | Proves | Output at `fd94e0c` |
|---|---|---|---|
| **H-1** | `dotnet build verify/ordercheck/OrderCheck.vbproj -c Release` then `dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release --no-build`, grep `A79` and `ALL PASS` | Every fixture, old and new, passes | 398 `PASS`, `ALL PASS`; `A56g` and `A79a`–`A79p` all `PASS` |
| **H-2** | `git diff --stat 346c6d1 fd94e0c` | The change set | `Core/TradeStoreWriter.vb` · `Core/RepairStatusLog.vb` · `tools/BacktestRunner/HistoricalStore.vb` · `verify/ordercheck/Program.vb` · `docs/DeribitIndicatorProject.md` |
| **H-3** | `git diff 346c6d1 fd94e0c -- Core/TradeStoreWriter.vb` and grep the changed lines for `Function AppendRows`, `Function DedupTrades`, `Function ResolveResumeCursorMs`, `Function ReadTradeFile`, `Function LastTradeTimestamp` | Frozen functions and non-repair readers untouched | *(empty)* |
| **H-4** | `powershell -File tools/checks/verify-gate.ps1` | Build, harness, display parity, rotation riders | See §1.1 |
| E-1 | The four fail-first fixtures run against `346c6d1` before the fix | **Fail-first** | Table below |
| E-2 | Mutations from a scratch backup of `Core/TradeStoreWriter.vb`; restored, `RESTORED: md5 identical` | Each fixture admits its failure | Table below |
| E-3 | `--no-incremental` Release builds of `OrderCheck`, `DeribitVerdictEngine.sln`, `BacktestRunner` | Every project linking a changed file compiles clean | 0 errors, **0 warnings** each (see §2.1 for the 6 warnings fixed on the way) |

**E-1 — fail-first, against `346c6d1`.**

| Fixture | Output before the fix |
|---|---|
| `A79l` | `venueCalls=2 rows=100(want 50) outcomes=1 state=TAIL_OK pass=PASS_CLEAN scanLine=none` — ⭐ **the September defect reproduced exactly: the whole lookback re-appended under a clean pass** |
| `A79m` | `oldOpenFailed=True windows=1 firstKind=AnchoredTail firstSeq=-1` — the scan failed beside a held writer and the store read as empty |
| `A79n` | `states=TAIL_OK pass=PASS_CLEAN sepRows=1` — the seed read failed silently |
| `A79p` | `windows=1 firstKind=Tail firstSeq=99` — a torn final row read as seq 98 |

**E-2 — mutation runs.** Each mutation is named in a comment above its fixture.

| Run | Mutation | Result |
|---|---|---|
| ML | `failure = Nothing` in `ScanForRepair`'s `Catch` | `A79l` FAIL — `rows=100 state=TAIL_OK pass=PASS_CLEAN` (also `A79n`) |
| MM | `OpenStoreForScan` shares Read only | `A79m` FAIL — `firstKind=ScanFailure` · `A79o` FAIL — `besideScan=0 rows=10` (the flush was dropped) |
| MN | drop the `SeedReadFailure` window | `A79n` FAIL — `states=TAIL_OK pass=PASS_CLEAN` |
| MP | bound the read at the file length | `A79p` FAIL — `firstSeq=99` |

### 1.1 Gate

Run after `fd94e0c`: `GATE PASSED` — AutoTweaker, WhatIfRunner, CeilingAudit, BacktestRunner and OrderCheck built; harness `ALL PASS`; no snapshot/card drift; no header rotation.

---

## 2. Decisions taken, and decisions queued

### 2.1 Decisions taken — one line each

- ⭐ **`SF-8` added beyond the ruling: read only to the last line feed present at open.** Share mode ReadWrite lets a scan meet a row mid-append. A row torn inside `trade_seq` parsed as a tiny seq, and the tail would start at the venue's retention edge — the same rewrite this build removes. `A79p` shows the hazard already exists today for a crash-torn file.
- **`SF-3` = (b):** a failed seed read is loud (`SEED_READ_FAILED`, `PASS_FAILED`), and the current file's own holes and tail still run. The richer of the two options; no cross-month hole, so no phantom.
- **`SF-1` = (a):** failures travel as failure-kind `RepairWindow`s through the same outcome and log path. `ResolveRepairWindows`' signature is unchanged; `ScanForRepair` gains a required `ByRef failure`, so no caller can ignore it.
- **The failure text is `"<ExceptionType>: <message>"`.** `A79l` and `A79n` assert `IOException`.
- **A missing month file stays "empty", not a failure (`SF-6`).**
- **`ReadTradeFileTail`, `ReadTradeFile` and `LastTradeTimestamp` are unchanged (`SF-5`).** `ReadTradeFileTail` — the streaming writer's seed read — has the same open class, but it fails toward admitting duplicates and the September data shows no case.
- **Six compiler warnings fixed before commit:** adding the `Failure` string made `RepairWindow` a structure with a reference member, so `Dim w As RepairWindow` raised BC42109 in the factories (3 per project that links the file). Now `Dim w As New RepairWindow()`.
- **`A79o` is feasible and built:** the other half of B-3. Beside a held plain `StreamReader`, `AppendRows` drops the batch (0 written); beside `OpenStoreForScan`'s handle it writes (1).
- **Commits:** spec `5b8febb` (`[no-engine-change]`), code `fd94e0c` (engine change, no token). `DeribitIndicatorProject.md` §15 gap-repair row extended; summary cell 796 B (`wc -c`).

### 2.2 Queued for the orchestrator

**None.** No `SF` row is reserved. No settings key, no CSV or schema change, no frozen function touched.

---

## 3. Feedback on the spec and the ruling

- **The ruling's share-mode half creates a new hazard that neither the ruling nor the read named: torn rows.** A reader that shares ReadWrite can read what a writer has half-written. The bound at the last line feed closes it for the final row; `A79p` is the evidence.
- **The fail-first shape for `A79l` needed the lock released mid-pass.** An exclusive lock alone also blocks the re-append, which hides the rewrite. The fixture's venue stub releases the lock on its first call, so a pass that fetches can append. Reusable for any "failure must not become a write" fixture.
- **`B-3`'s writer half was only inferred until now** (the venue-check build half-reproduced it). `A79o` reproduces it in the harness: a flush beside a plain reader is dropped.

---

## 4. What I did not verify

- **Which exception fired on the box in September.** This build removes the leading candidate trigger and makes any other failure loud; it cannot confirm the cause.
- **A torn row in the MIDDLE of a month file** — a crash mid-write followed by later appends glues a partial row onto the next one. The bound handles only the final row. Pre-existing; out of this ruling's scope.
- **`CoverageReport.ReadStoreWindow`** (tools-only) already reads with share mode ReadWrite and has the same torn-final-row exposure for its diff counts. Named, not changed.
- **Scan duration on the box** — how long `ScanForRepair` holds the file, and so how often the old mode dropped a flush. Not measured.
- **`TradeStoreGapRepair.RepairOnceAsync` end to end** — no fixture links that file; the failure path is covered through `BackfillTradeMonthCoreAsync` and `RepairStatusLog.ComposePassLines`.
- **Not deployed.** The S2 deploy waits for this build.
