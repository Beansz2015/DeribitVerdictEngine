# Gap repair — a failed store scan is loud; repair-path readers share the file — SPEC

> ✅ **RULED 2026-09-15 (UTC), trader via the orchestrator (`deribitverdictengine-b6`):** `DUP-1` = (a), fix now, and the S2 deploy WAITS for it. `DUP-2` = both directions: a scan failure fails the pass loudly, AND repair-path readers open with share mode ReadWrite. `ScanForRepair` comes off the gap-repair do-not-touch list for this build only; `AppendRows`, `DedupTrades` and `ResolveResumeCursorMs` stay frozen. Written and built in one session, as allowed when no decision row below is reserved. Build record: [`gap-repair-scan-failure-build-spec-back.md`](gap-repair-scan-failure-build-spec-back.md). Code handles pinned to `346c6d1`.

**IDs, defined once:**

- `DUP-1`, `DUP-2` — the decision rows in [`trade-store-duplicate-rows-read-2026-09-15.md`](trade-store-duplicate-rows-read-2026-09-15.md) §7, now ruled.
- `SF-1`–`SF-8` — this spec's decision rows (§3). `SX-1`–`SX-4` — this spec's implementer traps (§0).
- `F-1` — the cross-month seed fix (`165f780`), whose previous-month read also goes through `ScanForRepair`.
- **B-3** — the file-sharing collision in [`venue-check-schedule-plan.md`](venue-check-schedule-plan.md): a plain `StreamReader` cannot open beside a writer, and while it holds the file the writer's open fails. **V-3** — that plan's held item, "Core readers open with share mode ReadWrite"; this build un-holds its reader half for the repair path only.
- `A79l`, `A79m`, `A79n`, `A79o`, `A79p` — the new harness fixtures (§4).

---

## 0. Implementer brief

**Model: Opus · Effort: HIGH · one session.** Engine-binary, live tape path.

**Where it slips:**

| # | Trap | Input that exposes it |
|---|---|---|
| SX-1 | ⛔ **Using the rows read before the exception.** A partial read leaves no rows or only an old bracket — exactly the whole-lookback rewrite | `A79l` |
| SX-2 | ⛔ **Reading past the last complete line.** Share mode ReadWrite lets a scan meet a row mid-append; a row torn inside `trade_seq` parses as a tiny seq, and the tail starts from the venue's retention edge | `A79p` |
| SX-3 | **A seed-read failure dropping silently to single-file windows** | `A79n` |
| SX-4 | **Treating a missing month file as a failure.** A month file does not exist before its first trade; failing that would block every new month | `A56b` part 3 (empty store) must stay an anchored tail |

**Escalation:** stop if the fix needs `AppendRows`, `DedupTrades` or `ResolveResumeCursorMs`, a settings key, or a change to a non-repair reader (`ReadTradeFile`, `ReadTradeFileTail`, `LastTradeTimestamp`).

---

## 1. The defect — measured

From [`trade-store-duplicate-rows-read-2026-09-15.md`](trade-store-duplicate-rows-read-2026-09-15.md) §1–§2:

- **298,932 of September's 298,934 duplicate rows** came from 3 repair passes that each rewrote the full 20 h lookback.
- **`ScanForRepair` swallows any exception** and returns no rows, or only an old bracket.
- **The pass then reads the store as empty,** fetches the whole lookback and appends it again.
- **After `edd4539` + `165f780` the same failure** gives an anchored tail at `segStartMs`, or a seq tail from the retention edge. `repair_status.log` would record it `PASS_CLEAN`.
- **`F-1`'s previous-month read goes through the same function,** so a failed seed read silently falls back to single-file windows.
- **The trigger is unconfirmed.** The leading candidate is B-3: `ScanForRepair` opens with a plain `StreamReader` (share mode Read).

## 2. The fix

**`TradeStoreWriter.ScanForRepair`:**

1. **Opens through a new `Friend Shared Function OpenStoreForScan(path)`:** `FileAccess.Read`, share mode **ReadWrite**. It succeeds beside the streaming writer's `StreamWriter` (which shares Read), and the writer's own open succeeds beside it.
2. **Reads only up to the last line feed present at open time.** A final row still being appended, or torn by a crash, is not read.
3. **Gains a required `ByRef failure As String`.** On any exception it sets `failure` to `"<ExceptionType>: <message>"`, logs to `Console`, and returns an EMPTY list with no bracket. The rows read before the exception are never used.
4. **A missing file is not a failure:** empty list, `failure` Nothing, as today.

**`TradeStoreWriter.ResolveRepairWindowsCore`:**

- **Main scan failed** → return exactly one window of the new kind `ScanFailure`, carrying the failure text. No hole, no tail: nothing is fetched for that month file.
- **Seed read failed** (the `F-1` previous-month scan) → a `SeedReadFailure` window first, then the current file's own holes and tail as if no seed existed. There is no cross-month hole, so no phantom.

**`RepairWindow`** gains a `Failure As String` field and two kinds: `ScanFailure`, `SeedReadFailure`.

**`HistoricalStore.FetchRepairWindowAsync`** returns a failure-kind window at once — no venue call, 0 pages — with state `SCAN_FAILED` or `SEED_READ_FAILED` and the failure text as its reason.

**`RepairWindowOutcome.IsFailure`** includes both states, so the pass line is `PASS_FAILED`. The next pass retries by itself.

**`repair_status.log`** — same four fields; two new window lines:

```text
<utc> | SCAN_FAILED | <instance> | file=trades_2026-09.csv kind=scan committed=0 … pages=0 reason=IOException: <message>
<utc> | SEED_READ_FAILED | <instance> | file=trades_2026-09.csv kind=seed_read committed=0 … pages=0 reason=IOException: <message>
```

## 3. Decision table

| # | Question | Options | Read | Class |
|---|---|---|---|---|
| **SF-1** | How does a scan failure reach the pass? | **(a)** a failure-kind `RepairWindow` that the month loop turns into an outcome and a log line · **(b)** `ResolveRepairWindows` throws, and the month loop catches · **(c)** a `ByRef` out parameter on `ResolveRepairWindows` | **(a).** It flows through the same outcome and log machinery as every other window, and cannot be dropped by the one consumer. (b) is equally loud but breaks the resolver's never-throws contract for every caller. (c) is easy for a caller to ignore. No information is traded | Step 1 — taken |
| **SF-2** | Main scan failed: what is fetched? | **(a)** nothing for that file; retry next pass · **(b)** a tail from the last row read | **(a)** — the ruling. (b) is the rewrite this build removes | Ruled |
| **SF-3** | Seed read failed: what then? | **(a)** fail the whole month file — no fetch · **(b)** loud `SEED_READ_FAILED`, then the current file's own holes and tail | **(b).** Equally loud (`PASS_FAILED`), and it repairs the current file this pass instead of 6 h later. With no seed there is no cross-month hole, so no phantom. (a) is simpler and recovers less — the reserved pattern, so the richer option is taken | Step 1 — the richer option, taken |
| **SF-4** | Rows read before an exception? | **(a)** discard them all · **(b)** use them | **(a).** (b) is the defect's own mechanism | Step 3 — taken |
| **SF-5** | Which readers get share mode ReadWrite? | **(a)** the repair-path scans (`ScanForRepair`, both reads) · **(b)** every `TradeStoreWriter` reader | **(a)** — the ruling scopes it. `ReadTradeFileTail` (the streaming writer's seed) has the same open class, but it fails toward admitting duplicates, per its own comment, and the September data shows no case. `ReadTradeFile` and `LastTradeTimestamp` are not on the repair path | Ruled scope — step 3 |
| **SF-6** | A missing month file? | **(a)** not a failure · **(b)** a failure | **(a).** A month file does not exist before its first trade (`SX-4`) | Step 3 — taken |
| **SF-7** | Failure text in the log | exception type and message | As ruled | Ruled |
| **SF-8** | A final row still being written, or torn by a crash? | **(a)** read only to the last line feed present at open · **(b)** rely on `TryParseRow` rejecting it | **(a).** (b) accepts a row torn inside `trade_seq` as a tiny sequence and re-fetches from the retention edge (`SX-2`). Share mode ReadWrite makes this reachable. Mechanism | Step 3 — taken |

**No row is reserved.** No settings key, no CSV or schema change, no frozen function touched; `repair_status.log` gains two states in the existing four-field format.

## 4. Fixtures

| ID | Asserts | Fail-first against `346c6d1` | Mutation that must fail it |
|---|---|---|---|
| **`A79l`** | Month file held exclusively → `SCAN_FAILED` with `IOException`, no venue call, 0 rows appended, `PASS_FAILED` | Rows doubled: the whole lookback re-appended | `failure = Nothing` in `ScanForRepair`'s `Catch` |
| **`A79m`** | Beside a held streaming `StreamWriter`: the old plain `StreamReader` open fails (shown in-fixture); the repair scan succeeds and returns a normal tail | Empty store → anchored tail | `OpenStoreForScan` shares Read only |
| **`A79n`** | Previous month file held exclusively → `SEED_READ_FAILED`, no hole, September's own tail still `TAIL_OK`, `PASS_FAILED` | `TAIL_OK` only, silently | Drop the seed failure instead of adding the window |
| **`A79o`** | While a handle from `OpenStoreForScan` is held, `AppendRows` still writes its batch; with a plain `StreamReader` held, the same batch is dropped (the other half of B-3, shown in-fixture) | — (new function) | `OpenStoreForScan` shares Read only |
| **`A79p`** | A final row torn inside `trade_seq` ("…,98", no line end) is not read: tail from 980010, not 99 | Tail from seq 99 | Bound the read at the file length |
| `A56g` | Call site updated for `ByRef failure`; asserts it stays Nothing | — | — |

## 5. Not verified before the build

- **Which exception fired in September.** This build removes the leading candidate and makes any other failure loud; it cannot confirm the cause.
- **A torn row in the MIDDLE of the file** — a crash mid-write followed by later appends glues a partial row onto the next one. The bound handles only the final line. Pre-existing, and out of this ruling's scope.
- **`CoverageReport.ReadStoreWindow`** (tools-only) already reads with share mode ReadWrite and has the same torn-final-row exposure for its diff counts. Named, not changed.
