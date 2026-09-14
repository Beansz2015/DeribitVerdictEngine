# Gap repair — same-millisecond page skip — SPEC

> ⛔ **SPEC ONLY. NOT BUILT.** Written 2026-09-14 (UTC) by a scoped Opus/high seat. Handles pinned to `2fa22da`.
>
> **Deploy target:** the single stop → swap → start after session S2 of [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §0 ("the S2 deploy" below).
>
> ⛔ **Before the build opens, the trader rules `GR-1`, `GR-3`, `GR-4` and `GR-5`** (this spec's §3.2 table). The build writes to the live tape store and ships only with a deploy, so it is reserved as a whole.

**IDs used in this document, defined once:**

| ID | Source and kind | Meaning |
|---|---|---|
| `GR-1` to `GR-5` | This spec, decision rows | The five choices in this spec's §3.2. The `GR` prefix avoids the `D-2` used by the absorption build |
| `A79a` to `A79e` | This spec, **planned** harness fixture IDs | ⚠ A plan, not a fact. Take the next free family in `verify/ordercheck/Program.vb` at build time. `A79*` was free at `2fa22da` |
| `A78b` | Harness fixture, shipped in `6509a0f` | The venue-check pager keeps 2,500 of 2,500 trades across a same-ms page boundary |
| `A56a` to `A56g` | Harness fixtures, shipped | Hole-derived repair windows (`TradeStoreWriter.ResolveRepairWindowsMs`) |
| `A48d` | Harness fixture, shipped | Gap-repair overlap is a no-op |
| `A66c` | Harness fixture, shipped | The repair User-Agent names the running host |
| `DR-1` | [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §1, a ruled follow-up | Removed the `MinHoleMs` width floor. Kept the inverted-range drop and called it "inherent" |
| `DR-3` | Same brief, a ruled follow-up | `BackfillTradeMonthAsync` returns rows appended, not the file's row count |
| `e3781e57…` | AWS `InstanceId`, [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a | The restart at 2026-08-17 16:23:05 UTC |
| `E-1` to `E-3`, `H-1` to `H-5` | This spec's §10 | `H-n` = a handle the reader can run. `E-n` = evidence the reader cannot re-run as-is |
| `GT-1` to `GT-5` | This spec's §0, implementer traps | Where the build will slip. `GT` = gap-repair trap; chosen because `GT-1` and `S-4` already name other items in this repo |

---

## 0. Implementer brief

**Model: Opus · Effort: HIGH** if `GR-1` is ruled (b) or (c). **Opus · medium** if `GR-1` is ruled (a).

**Why that tier.**

- **The pager half is template work.** `CoverageReport.FetchVenueWindowAsync` (`tools/BacktestRunner/CoverageReport.vb:1376`–`1425`) is the contract. `A78b`'s stub (`verify/ordercheck/Program.vb:14219`–`14299`) is the fixture template.
- **The bracket half is not.** It amends `DR-1`'s ruled "inherent" drop in `Core/TradeStoreWriter.vb`. Six of the seven `A56` fixtures pin the old `+ 1` / `- 1` window edges. Deciding what each assertion becomes is the work. A deleted assertion reduces coverage silently.

**Where it slips.** The implementer also writes the fixtures, so a misunderstanding can pass its own test. Each row names the input that makes the fixture fail.

| # | Trap | The input that exposes it |
|---|---|---|
| GT-1 | ⛔ **`newest` computed after the seq or in-window filter.** A page whose newest trades are filtered then cannot move the cursor, and the pager reports a false stall | `A79d` part 3: page size 3, hole window `[T, U]`, the page ends on a trade at `U` whose seq is at or above the upper bound |
| GT-2 | **`A56b` part 5 deleted instead of inverted.** Under `GR-1` (b) or (c), a gap between rows 1 ms apart is fetchable. The assertion flips; it does not go away | `A56b` part 5 must now assert a hole window `[T, T+1]` |
| GT-3 | **A stall mutation hangs the harness.** Remove the stall guard and the cursor never moves; `MaxTradePages` is 200,000 | `A79b`'s stub returns `Nothing` after 10 calls, so the mutation fails with "fetch failed", not a hang |
| GT-4 | **`1000` restated in a fixture.** `TradesPerPage` is `Private Const` today | Promote it to `Public Const` (CLAUDE.md "A value ruled into a CONSTANT goes `Public Const`"); `A79a` reads it |
| GT-5 | **`A79e` builds its expected set from one pager's output.** Both pagers can then drift together and still agree | Assert each pager against the stub tape, then against each other |

**Escalation triggers — stop and come back.**

- Any `A48*` or `A56*` fixture fails for a reason you cannot explain in one sentence.
- The build needs to touch `TradeStoreWriter.AppendRows`, `DedupTrades`, `ScanForRepair`, `TradeStoreGapRepair.vb`, `CoverageReport.vb` or `settings.json`.
- `A79e` shows the two pagers disagree on an unbounded window. That is a contract divergence, not a fixture bug.
- `GR-1` is not ruled when the session opens. Do not touch `Core/TradeStoreWriter.vb`.

**Session plan — one session, two local commits, sequenced by dependency.**

| Step | Work | Commit |
|---|---|---|
| 1 | Extract the pager seams (this spec's §4.2) with the `+ 1` cursor **kept**. `A48d`, `A56*` and `A66c` pass unchanged | 1 |
| 2 | Write `A79a`. Run it. ⛔ **It must FAIL at 2,499 of 2,500.** Paste the output into the spec-back as `E`-evidence | 1 |
| 3 | Change the cursor to the contract (this spec's §4.1). `A79a` passes | 1 |
| 4 | `A79b`, `A79c`, `A79e` | 1 |
| 5 | `GR-1` bracket change, `A79d`, the `A56` repoints (this spec's §6.2) | 2 |
| 6 | Full harness, `tools/checks/verify-gate.ps1`, Release build, the `DeribitIndicatorProject.md` §15 row | 2 |

Report back with a summary and a spec-back per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Do not push.

---

## 1. The defect — verified

| Fact | How verified |
|---|---|
| `BackfillTradeMonthAsync` sets `cursorMs = newestMs + 1` after each page | `H-1`: `tools/BacktestRunner/HistoricalStore.vb:314` |
| `start_timestamp` is inclusive | Measured by the venue-check seat, [`venue-check-build-spec-back.md`](venue-check-build-spec-back.md) §3. Re-measured by this seat (`E-1` P4) |
| `end_timestamp` is **also** inclusive | ⭐ **New, this seat, `E-1` P1:** `start = end = 1789406915226` returned both trades in that millisecond |
| The skip is real on the venue | `E-1` P2 to P4, count 2 standing in for 1,000. Page 1 ends on seq `…126` at ms `…226`. A `+ 1` cursor returns nothing, so seq `…127` is lost. A cursor at `…226` returns `…126` and `…127`; dedup keeps `…127` |
| The live app runs this code | `TradeStoreGapRepair.vb:127` calls it with `repairHoles:=True`. `DeribitVerdictEngine.vbproj:51` compiles `HistoricalStore.vb` into the engine |
| `count` cannot exceed 1,000 | `E-1` P6: `count=1001` returns error `-32602`, `"value is too high"` |

---

## 2. Impact to date (question 1)

### 2.1 ⛔ The loss is PERMANENT. The next pass cannot refill it, whatever the hole length.

The mechanism, from the code at `2fa22da`:

1. A repair page ends on trade `(T, s)`. The venue also holds `(T, s+1)` … `(T, s+k)`, which did not fit.
2. The cursor becomes `T + 1`. The next page starts at `(U, s+k+1)`, with `U ≥ T + 1`.
3. The store now holds `(T, s)` and `(U, s+k+1)`. The next pass's walk sees `delta = k + 1` and emits a hole window `[T + 1, U − 1]` (`H-2`: `Core/TradeStoreWriter.vb:853`).
4. ⛔ **The missing trades sit at `T`. `T` is outside `[T + 1, U − 1]`.** Two outcomes, both empty:

| Case | What each pass does | Code |
|---|---|---|
| `U = T + 1` | The window inverts. It is dropped as "unfetchable" and logged | `Core/TradeStoreWriter.vb:881`, log at `:905`–`:910` |
| `U > T + 1` | The window is fetched. The venue returns zero trades, because no trade has a seq between `s+k` and `s+k+1`. The loop exits on an empty page | `tools/BacktestRunner/HistoricalStore.vb:307` |

5. Each pass repeats this for as long as the hole stays inside the 20 h lookback (`settings.json:558`). Each repeat costs one REST call and one `MaxHolesPerPass` slot. Then the hole ages out of the scan, and Deribit's ~24 h retention makes it unrecoverable.

⚠ **So "a hole again longer than one page" is not the failing condition. Every hole of this shape fails.** The window arithmetic excludes the bracket rows' own milliseconds, so the page skip and the repair miss are the same defect twice.

⚠ **This assumes `trade_seq` order agrees with timestamp order.** The walk already assumes it. `E-1` P1 to P4 agree with it. Not proved.

### 2.2 ⭐ Production evidence — the defect has already fired

Source: the AWS store copy-back `aws_fetch/20260913-153704/backtest_data/` (fetched 2026-09-13). Scan: `H-4`, rows from 2026-08-12 00:00 UTC with a `trade_seq`.

| Measure | Value |
|---|---|
| `trade_seq` holes in `trades_2026-08.csv` | **17** |
| The one large hole | 14,405 trades, left row 2026-08-15 16:11:48 UTC, 28.2 h wide. That is the outage before the `e3781e57…` restart, clamped by the 20 h lookback |
| ⛔ **Small holes** | **16, missing 70 trades in total (1 to 16 each)** |
| Their dates | 2026-08-16 21:40:08 → 2026-08-17 16:23:04 UTC. **All inside the start-up pass's 20 h lookback** (restart at 16:23:05, window from 2026-08-16 20:23:05) |
| ⭐ **Their file positions** | 13 left rows sit at 62,229 + 1,000·k rows from one block start. Row 62,229 is the last row before the outage. **So each left row is the last row of a 1,000-row repair page.** The other 3 sit at exactly 2,000, 3,000 and 5,000 rows from a second block start |
| Spans | 1 to 16,012 ms. One has a 1 ms span (2026-08-17 04:43:52 UTC) — the "unfetchable" case in this spec's §2.1 |
| Still present | **Yes, 27 days later** in the 2026-09-13 copy-back. Hole-derived repair was live since `e551f15e…` (2026-08-13), so any later pass ran this code |
| `trades_2026-09.csv` | **0 holes** in 1,235,249 distinct trades, 2026-09-01 → 2026-09-13 |

**Rate check.** The start-up window held 30,012 distinct trades, so about 30 page boundaries. 16 of about 30 is near 53 %. A simulation of `+ 1` paging over the local 2026-08-12 → 14 store gave 48.8 % of boundaries with a skip and 1.97 trades per boundary (`E-2`).

### 2.3 When the skip happens

- **Only when a repair window holds more than `TradesPerPage` (1,000) trades.** One page is ~14.7 min of tape at September's mean rate (67.8 trades/min, `E-3`).
- **Start-up pass after downtime longer than that.** The 2026-08-17 case above.
- **A ride-through hole of that size.**
- **Not a normal deploy.** The 2026-08-13 deploy was a 5.5-second connect ([`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a, row `e551f15e…`).
- **Near half of boundaries lose trades:** 48.8 % simulated, about 53 % observed.

### 2.4 ⚠ The wider class — not only page skips

**62.34 % of September's trades share a millisecond with a `trade_seq` neighbour** (`E-3`). The window arithmetic in this spec's §2.1 step 3 cannot fetch a lost trade that shares a millisecond with a stored neighbour. **So today's repair cannot heal most single-trade losses, whatever caused them.** This is why `GR-1` exists.

---

## 3. Decisions

### 3.1 Fixed by the orchestrator's brief — not a choice

- **The pager contract is `CoverageReport.FetchVenueWindowAsync`'s.** "Reuse that contract; do not invent a third one." This spec's §4.1 restates it step by step.
- **Deploy target: the S2 deploy.**

### 3.2 Decision table

| # | Question | Options | My read | Class |
|---|---|---|---|---|
| **GR-1** | Do the repair windows move to inclusive bracket milliseconds? | **(a)** pager fix only; hole windows stay `[L.ts + 1, R.ts − 1]` and the tail stays `max.ts + 1` · **(b)** hole windows become `[L.ts, R.ts]`, bounded by `L.seq < seq < R.seq`; tail unchanged · **(c)** (b), plus the tail becomes `[max.ts, segEnd]`, bounded by `seq > max.seq` | ⭐ **(c).** (a) leaves this spec's §2.4 class unrepairable by construction. `DR-1` item 3 kept the inverted drop because "there is no sub-millisecond query". **Both bounds are measured inclusive (`E-1` P1, P4), so `[T, T]` is a valid query and the premise is false.** (b) heals a tail same-ms loss only one pass later, and loses it if `T` leaves retention first. **Cost of (c):** `A56b`'s "tail start equals `ResolveResumeCursorMs`" and "covered store gives an empty list" stop holding; they are restated, not deleted | ⛔ **Reserved** — amends ruled `DR-1` and changes what is written to the tape store |
| **GR-2** | One shared pager, or two? | **(a)** one pager in engine-linked code; `CoverageReport` calls it · **(b)** two pagers; `A79e` runs both over the same stub tapes · **(c)** share only a pure page-step function in `Core/` | **(b)** — see this spec's §5. Each option guarantees a different thing: (a) guarantees no drift, (b) guarantees an independent audit. **(a) is mechanically wrong for an audit instrument:** a pager defect becomes common-mode, both lists miss the same trade, and the venue check reads `CLEAN`. (c) has the same flaw inside the step. Step 3 of the CLAUDE.md three-step test | Taken (step 3, named). The build is reserved as a whole |
| **GR-3** | A full page inside one millisecond — what then? | **(a)** abandon that window, mark it not-Ok, log; the hole stays in the store and the next pass retries · **(b)** fall back to `public/get_last_trades_by_instrument` with `start_seq` / `end_seq` for that millisecond (params accepted live, `E-1` P5) · **(c)** a larger `count` — ⛔ **not available**, the venue refuses 1,001 (`E-1` P6) | **(a).** ⚠ **This is the cheaper option and (b) records more — the reserved pattern, named.** My argument: the largest millisecond in the production store holds **139** trades (August) and **107** (September) against a 1,000 cap (`E-3`). (b) is a second pagination contract, which the brief ruled out. **Never a silent skip either way:** the remaining hole stays visible to the `trade_seq` walk | ⛔ **Reserved** — cheaper and records less |
| **GR-4** | Does a failed or stalled window need a durable record? | **(a)** Console only, as for today's fetch failures; the durable record is the `trade_seq` hole in the store · **(b)** a durable repair log line for every window outcome | **(a) for this build, plus a new queue row for (b).** ⚠ **Console is not durable here: no `Console.SetOut` or `SetError` exists in the tree (`H-3`).** But the gap is not new — every repair failure reason (fetch failure, page cap) has it today. A log format for all of them is its own spec. ⚠ **"Defer" is the reserved tell, named** | ⛔ **Reserved** |
| **GR-5** | What if this build is not accepted before the S2 deploy? | **(a)** S2 deploys without it; the next deploy carries it · **(b)** the S2 deploy waits | **(a).** The absorption spec's single-edge trap (`T-2`, two deploys make three eras — [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §0) is about `analysis_log.csv`, which this build does not touch. **The trade:** (a) accepts permanent loss on any outage longer than ~15 min before the fix ships. September shows 0 such holes | ⛔ **Reserved** — deploy |

### 3.3 Auto-proceeded — one line each

- **Dedup set covers the whole window**, not only the last millisecond — mirrors `CoverageReport`; same information; ~81k ids for a 20 h tail at September's mean rate.
- **Commit per page is kept**, not at window end — today's behaviour; a later page failure keeps earlier pages.
- **`newest` is taken over every page trade before any filter** — mechanism; otherwise trap GT-1 in this spec's §0.
- **Trades outside `[StartMs, EndInclMs]` are dropped** — the contract.
- **A trade without `trade_seq` inside a seq-bounded window is kept only when `StartMs < ts < EndInclMs`** — the one case today's code keeps; it cannot be a stored bracket row.
- **`TradesPerPage` becomes `Public Const`** — CLAUDE.md constant rule.
- **`FetchTradesByTimeAsync` splits into a JSON fetch and a parse that reads `has_more`.** The old `Public` function is deleted; its only call site is `HistoricalStore.vb:300` (grep, `2fa22da`).
- **The pager takes `pageSize` as a parameter** — precedent `ScanForRepair`'s `maxScanRows` (`Core/TradeStoreWriter.vb:948`–`950`); production passes `TradesPerPage`; fixtures that need small pages say MECHANISM.

---

## 4. The fix shape (question 2)

### 4.1 The pager contract — mirrors `CoverageReport.FetchVenueWindowAsync`

For one window `[StartMs, EndInclMs]`:

1. `cursor = StartMs`. Create an empty `seen` set.
2. If the shared page budget (`MaxTradePages`) is spent → window not-Ok, `"page cap"`. Stop the month, as today.
3. Fetch `(cursor, EndInclMs, pageSize)`. `Nothing` → window not-Ok, `"fetch failed at cursor N"`. Go to the next window.
4. Parse, including `has_more`. `Nothing` → window not-Ok, `"unparseable response at cursor N"`. Next window.
5. `newest = max(cursor, every trade's timestamp on the page)`. ⛔ **Before any filter.**
6. Keep a trade only when all hold: inside `[StartMs, EndInclMs]` · inside the window's seq bounds (`GR-1`) · its key is new to `seen`. The key is `"ID"` + `TradeId` when the trade has identity, else `"LK"` + `TradeStoreWriter.LegacyRowKey`. Commit the kept trades.
7. Empty page → window Ok.
8. `more = has_more` when present, else `page count ≥ pageSize`. Not `more` → window Ok.
9. ⛔ `newest ≤ cursor` → window not-Ok, `"stall: a full page inside one millisecond at cursor N"`. Next window (`GR-3` (a)).
10. `cursor = newest`. Wait `PoliteDelayMs`. Go to step 2.

### 4.2 Code layout — `tools/BacktestRunner/HistoricalStore.vb`

| Member | Role |
|---|---|
| `Public Const TradesPerPage As Integer = 1000` | Was `Private` |
| `Friend Shared Async Function FetchTradesPageJsonAsync(startMs, endMs, count) As Task(Of String)` | HTTP plus retry-once on 5xx or timeout, lifted from `FetchTradesByTimeAsync` (`:343`–`:389`). Same `_http`, so `A66c` is unaffected |
| `Friend Shared Function ParseTradesPage(json)` | Trades plus `has_more`. `Nothing` on a malformed body. Uses `TradeRecord.ReadTradeId` / `ReadTradeSeq` as today |
| `Friend Shared Async Function PageTradeWindowAsync(win, fetchPage, commit, pageSize, pagesLeft, pageDelayMs)` | This spec's §4.1 for one window. `fetchPage` is `Func(Of Long, Long, Integer, Task(Of String))` — the same shape as `CoverageReport`'s, so `A78VenueStub` drives both |
| `Friend Shared Async Function PageTradeWindowsAsync(windows, fetchPage, commit, pageSize, pageDelayMs)` | The month loop: shared page budget, abandon only the failed window. Returns committed rows, pages, failed windows, stalled windows |
| `BackfillTradeMonthAsync` | Resolves windows as today, then calls `PageTradeWindowsAsync(windows, AddressOf FetchTradesPageJsonAsync, Function(rows) TradeStoreWriter.AppendRows(dir, rows), TradesPerPage, PoliteDelayMs)`. Return value unchanged (`DR-3`). The log line adds failed and stalled window counts |

### 4.3 Window brackets — `Core/TradeStoreWriter.vb`, under `GR-1` (c)

- **`LongRange` gains `LoSeqExcl` and `HiSeqExcl`.** `AbsentSeq` means unbounded. The two-argument constructor keeps both absent, so `HistoricalStore.vb:271` (the offline path) is unchanged.
- **New `Public Shared Function InSeqBounds(t As TradeRecord, win As LongRange) As Boolean`** — network-free, next to the code that makes the bounds. The pager's step 6 calls it.
- **Hole window (step 3–4):** `New LongRange(prev.TsMs, cur.TsMs, prev.Seq, cur.Seq)`. The clamp stays on timestamps.
- **Tail window (step 8):** when the last sorted row carries a seq → `New LongRange(tailStart, segEndInclMs, last.Seq, AbsentSeq)`, with `tailStart = last.TsMs`, clamped up to `segStartMs` as today. A seq-less last row → today's `last.TsMs + 1`, unbounded. An empty scan → `segStartMs`, unbounded.
- **Rewrite the `DR-1` comment at `:868`–`:875`.** Its "no sub-millisecond query" premise is false. The inverted-range log arm stays; after the change only the clamp can reach it.
- **Under `GR-1` (b):** the hole change only; step 8 unchanged.

### 4.4 What does not change

`TradeStoreGapRepair.vb` · `TradeStoreWriter.AppendRows` · `DedupTrades` · `ScanForRepair` · `ResolveResumeCursorMs` · `CoverageReport.vb` · `settings.json` (no key) · `analysis_log.csv` header · every rendered surface.

---

## 5. Shared seam or copy (question 3) — `GR-2` (b)

| Fact | Consequence |
|---|---|
| `HistoricalStore.vb` links into the engine (`DeribitVerdictEngine.vbproj:51`), `BacktestRunner` and `OrderCheck` | A shared pager must live on the engine side |
| `CoverageReport.vb` links into `BacktestRunner` and `OrderCheck` only | It can call down into `HistoricalStore`, never the reverse |
| ⭐ `CoverageReport`'s venue check audits the store that repair writes | **A shared pager makes the auditor share the defect it audits.** Both lists would miss the same trade and the diff would read `CLEAN` |
| ⭐ This defect was found **because** the two pagers were separate | Independence already paid for itself once |

**Ruling:** two implementations of one contract. `A79e` runs both over the same stub tapes and asserts each against the tape. `CoverageReport.vb` stays untouched, so the venue check's `tool_commit` history does not move for an engine fix.

---

## 6. Fixtures (question 4)

⚠ **Fixture-literal provenance (CLAUDE.md hard rule).** `TradesPerPage`, `MaxHolesPerPass` and `AbsentSeq` are READ, never restated. Timestamps, sequences and small page sizes are MECHANISM; say so in a comment at the call site.

### 6.1 New fixtures

| Planned ID | Asserts | ⛔ Mutation that must fail it |
|---|---|---|
| **`A79a`** | `PageTradeWindowAsync` over `A78b`'s tape (2,500 trades; trade 1000 shares trade 999's millisecond), `pageSize = TradesPerPage`, committing into a temp store: **2,500 rows, 2,500 distinct `trade_id`s, Ok**. Again with `has_more` omitted | `cursor = newest + 1` → **2,499 of 2,500**. ⭐ **This is today's code: step 2 of this spec's §0 runs it before the fix** |
| **`A79b`** | 1,001 trades in one millisecond: not-Ok, reason starts `"stall"`, exactly 1,000 committed. The stub returns `Nothing` after 10 calls. A second window after the stalled one is still paged (`PageTradeWindowsAsync`) | Remove the stall check → reason `"fetch failed"`, not `"stall"` |
| **`A79c`** | Page 2 of window 1 fails: window 1 not-Ok, page 1's rows stay committed, window 2 is paged | `Exit For` instead of next window → window 2 has 0 rows |
| **`A79d`** | `GR-1`. **Part 1**, same ms: store `(T, s)`, `(T, s+2)`; tape adds `(T, s+1)` → `ResolveRepairWindowsMs` returns `[T, T]` with bounds `(s, s+2)`; paging commits exactly `s+1`; a re-resolve returns only the tail. **Part 2**, 1 ms apart and 5 ms apart: same shape, recovers the missing seqs, never re-commits `L` or `R`. **Part 3** (trap GT-1), `pageSize = 3`: store `(T, s)`, `(U, s+2)`, `U = T + 5`; tape `(T, s)`, `(T, s+1)`, `(U, s+2)`, `(U, s+3)` → Ok, commits only `s+1`. **Part 4** (`GR-1` (c) only), tail: store ends `(T, s)`; tape `(T, s)`, `(T, s+1)`, `(T+3, s+2)` → commits `s+1` and `s+2`, not `s` | Parts 1–2: restore `prev.TsMs + 1L, cur.TsMs - 1L` → 0 committed. Part 3: take `newest` after the filter → false stall. Part 4: restore `TsMs + 1L` → `s+1` missing |
| **`A79e`** | `GR-2` (b). The `A78b` tape, the stall tape, a tape with out-of-window trades, and a tape without `has_more`, through both `CoverageReport.FetchVenueWindowAsync` and `HistoricalStore.PageTradeWindowAsync` on unbounded windows: each pager's `trade_id` set equals the tape's in-window set, and the Ok / stall outcomes match | `+ 1` in one pager only → that pager misses the tape |

### 6.2 Existing fixtures to repoint — do not delete

Found by an unanchored scan of `verify/ordercheck/Program.vb:10540`–`10960` for `+ 1` / `- 1` edges at `2fa22da`.

| Fixture | Lines | Under `GR-1` (b) | Under `GR-1` (c) |
|---|---|---|---|
| `A56a` | `10566`–`10580` | Hole edges become the bracket timestamps, with seq bounds | Plus the tail edge at `10570` |
| `A56b` part 1 | `10617` | Unchanged | Tail start is `lastTs` with `LoSeqExcl` = last seq. The "equals `ResolveResumeCursorMs`" comparison is restated, not deleted |
| `A56b` part 2 | — | Unchanged | A covered store returns `[lastTs, lastTs]` bounded by the last seq, not an empty list |
| `A56b` part 4 | `10642`–`10644` | Hole edges | Plus the tail edge |
| `A56b` part 5 | `10658` | ⛔ **Invert:** the 1 ms gap is now fetched as `[T, T+1]` (trap GT-2) | Same, plus the tail edge |
| `A56c`, `A56d` | `10699`, `10741`, `10753` | Unchanged | Tail edges |
| `A56e` | `10795`–`10808` | Hole end edge | Plus the tail edge at `10796` |
| `A56f` | `10844`–`10846` | Hole start edge | Plus the tail edge |
| `A56g`, `A48d`, `A66c`, `A78b` | — | Unchanged | Unchanged |

---

## 7. Acceptance and deploy

- ✅ Whole harness `ALL PASS`, including `A79a`–`A79e` and the repointed `A56` fixtures.
- ✅ Each new fixture's named mutation run once from a scratch backup; output recorded; file restored with an identical MD5 (the practice in [`venue-check-build-spec-back.md`](venue-check-build-spec-back.md) §1, `E-1`). Each mutation is named in a comment above its fixture.
- ✅ `A79a`'s fail-first run (this spec's §0 step 2) pasted as evidence.
- ✅ `dotnet build DeribitVerdictEngine.sln -c Release`: 0 errors. `tools/checks/verify-gate.ps1` passes.
- ✅ **A settings-untouched row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15.** It says: engine-binary behaviour change on the tape path · no scoring impact · not an `analysis_log.csv` dataset boundary · a store-completeness edge at the deploy's `InstanceId`.
- ✅ No `settings.json` bump (no key). The commit message states that no card surface is affected (CLAUDE.md display-string parity rule). **No `[no-engine-change]` tag** on the build commits.
- ✅ **At the S2 deploy:** the [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a row names these commits and says the repair pager changed at that `InstanceId`.
- ⏳ **Post-deploy watch, observational:** after the next start-up repair longer than one page, run `H-4` on the next copy-back. Expect 0 small holes in the repaired span.

---

## 8. Out of scope — named so they are not lost

| Item | Why it is here |
|---|---|
| **`ResolveResumeCursorMs` still returns `last + 1`** (`Core/TradeStoreWriter.vb:690`) | The offline `BackfillAllAsync` path (`repairHoles:=False`). Same same-ms shape at the resume point. `BacktestRunner` only |
| ⚠ **`trades_2026-09.csv` holds 298,934 duplicate rows** — 1,534,183 rows, 1,235,249 distinct `trade_id`s (19.5 %), `H-5` | Cause not investigated. `HistoricalStore.LoadTradeRange` dedups, so readers through it are unaffected; raw counts and file size are not. I found no queue row for it (grep of `trader-tick-queue.md`) |
| **A durable repair log** | `GR-4` (b) |
| **Seq-based repair for hole windows** via `get_last_trades_by_instrument` `start_seq` / `end_seq` | Exact for a known seq range, no millisecond edge. `E-1` P5 shows the params accepted. A different design, not this fix |

---

## 9. ⚠ Not verified

- **That the 70 trades in this spec's §2.2 shared their left row's millisecond.** The venue history is gone. The inference rests on the 1,000-row spacing and the rate match.
- **That later 6-hourly passes ran after 2026-08-17 16:23 UTC.** No durable repair log exists. The app was alive: `ws_health.log` has 2026-08-18 entries, and the next restart is 2026-08-22.
- **That `trade_seq` order always agrees with timestamp order.** Assumed by the walk and by this spec's §2.1.
- **How the AWS app's standard output is launched or captured.** Only the in-tree absence of `Console.SetOut` / `SetError` is verified.
- **`E-2`'s simulation** uses the local `backtest_data/trades_2026-08.csv` (2026-08-12 → 14) as a stand-in for venue tape.
- **`end_timestamp` inclusive** — measured on one millisecond pair, once.
- **Whether the coverage report grades an hour with such a hole `Defect`.** Carried from CLAUDE.md's worked example (`storeClean = seqContiguous`); not re-read in code.
- **No build and no harness run.** This is a spec.

---

## 10. Evidence handles

Pinned to `2fa22da`. Every handle below was run by this seat on 2026-09-14 (UTC); the output shown is the actual output.

**H-1 — the cursor.**

```bash
grep -n 'cursorMs = newestMs + 1' tools/BacktestRunner/HistoricalStore.vb
```

Output: `314:                cursorMs = newestMs + 1`

**H-2 — the window arithmetic.**

```bash
grep -n 'New LongRange(prev.TsMs + 1L, cur.TsMs - 1L)\|tailStart = rows(rows.Count - 1).TsMs + 1L\|If e < s Then' Core/TradeStoreWriter.vb
```

Output: `853:` hole window · `881:` inverted drop · `918:` tail start.

**H-3 — no console capture in the tree.**

```bash
grep -rn 'Console.SetOut\|Console.SetError' --include=*.vb .
```

Output: empty.

**H-4 — `trade_seq` holes in the production copy-back.** Needs the gitignored folder in the main checkout.

```bash
F=/c/Dev/DeribitVerdictEngine/aws_fetch/20260913-153704/backtest_data/trades_2026-08.csv
T=$(mktemp)
awk -F, 'NR>1 && $7!="" && $1>=1786492800000 {print $7","$1","NR}' "$F" | sort -t, -k1,1n -k3,3n > "$T"
awk -F, '
FNR==NR { if (FNR==1) next; t=$1+0; if (FNR==2 || t<pt) bs=FNR; blk[FNR]=bs; pt=t; next }
{ s=$1+0; t=$2+0; ln=$3+0
  if (have && s-ps>1) { h++; m+=s-ps-1
    printf "%s miss=%d span_ms=%d L_pos_in_block=%d\n", strftime("%Y-%m-%dT%H:%M:%SZ", int(pt2/1000), 1), s-ps-1, t-pt2, pln-blk[pln]+1 }
  have=1; ps=s; pt2=t; pln=ln }
END { printf "holes=%d missing=%d\n", h, m }' "$F" "$T"
rm -f "$T"
```

Output:

```text
2026-08-15T16:11:48Z miss=14405 span_ms=101536281 L_pos_in_block=62229
2026-08-16T21:40:08Z miss=3 span_ms=4 L_pos_in_block=63229
2026-08-16T23:46:08Z miss=1 span_ms=4606 L_pos_in_block=66229
2026-08-17T01:14:35Z miss=16 span_ms=24 L_pos_in_block=68229
2026-08-17T01:50:18Z miss=1 span_ms=16012 L_pos_in_block=69229
2026-08-17T02:33:26Z miss=12 span_ms=1044 L_pos_in_block=70229
2026-08-17T02:56:29Z miss=1 span_ms=3 L_pos_in_block=71229
2026-08-17T04:43:52Z miss=15 span_ms=1 L_pos_in_block=73229
2026-08-17T06:07:30Z miss=1 span_ms=13 L_pos_in_block=2000
2026-08-17T06:49:41Z miss=1 span_ms=2 L_pos_in_block=3000
2026-08-17T08:01:54Z miss=4 span_ms=8669 L_pos_in_block=5000
2026-08-17T11:53:14Z miss=1 span_ms=71 L_pos_in_block=77229
2026-08-17T12:42:49Z miss=5 span_ms=61 L_pos_in_block=78229
2026-08-17T13:42:30Z miss=1 span_ms=6 L_pos_in_block=80229
2026-08-17T14:10:04Z miss=1 span_ms=8530 L_pos_in_block=81229
2026-08-17T15:50:29Z miss=2 span_ms=3 L_pos_in_block=85229
2026-08-17T16:23:04Z miss=5 span_ms=63 L_pos_in_block=86229
holes=17 missing=14475
```

A "block" starts at each row whose timestamp is lower than the row above it in file order.

**H-5 — duplicate rows in September.**

```bash
G=/c/Dev/DeribitVerdictEngine/aws_fetch/20260913-153704/backtest_data/trades_2026-09.csv
echo "rows=$(tail -n +2 "$G" | wc -l) distinct_trade_id=$(tail -n +2 "$G" | cut -d, -f6 | sort -u | wc -l)"
```

Output: `rows=1534183 distinct_trade_id=1235249`

**E-1 — live venue probes, 2026-09-14 ~18:29 UTC.** ⚠ The trades are from 2026-09-14 17:28:35 UTC; the venue drops them after ~24 h. To re-run, find a same-ms pair in the last hour and substitute its timestamps. Base URL `https://www.deribit.com/api/v2/public/`, `instrument_name=BTC-PERPETUAL`, `sorting=asc`.

| Probe | Request | Result |
|---|---|---|
| P1 | `get_last_trades_by_instrument_and_time`, start = end = `1789406915226`, count 10 | seqs `299317126`, `299317127`, both at `…226`; `has_more` false → **end inclusive** |
| P2 | start `1789406915224`, count 2 | `…125` at `…224`, `…126` at `…226`; `has_more` true |
| P3 | start `1789406915227` (a `+ 1` cursor), count 2 | no trades; `has_more` false → **`…127` skipped** |
| P4 | start `1789406915226` (cursor = newest), count 2 | `…126`, `…127`; `has_more` false → **start inclusive; dedup recovers `…127`** |
| P5 | `get_last_trades_by_instrument`, `start_seq=299317126`, `end_seq=299317127` | both trades returned |
| P6 | `get_last_trades_by_instrument_and_time`, count 1001 | error `-32602`, `"value is too high"`, param `count` |

**E-2 — `+ 1` paging simulation.** Local `backtest_data/trades_2026-08.csv`, rows from 2026-08-12 00:00 UTC with a seq: 123,514 rows, seq-contiguous. 51.90 % of adjacent pairs share a millisecond. Paging by 1,000 with a `+ 1` cursor: 123 boundaries, 60 with a skip (48.8 %), 242 trades skipped (1.967 per boundary). The one-off awk script was not kept.

**E-3 — September production tape.** `trades_2026-09.csv`, deduplicated by `trade_id`: 1,235,249 trades over 18,217 min → 67.8 trades/min, 14.7 min per 1,000. 44.31 % of adjacent pairs share a millisecond. 62.34 % of trades share a millisecond with a `trade_seq` neighbour. Largest millisecond: 107 trades (September), 139 trades (August from 2026-08-12). One-off awk commands, not kept.
