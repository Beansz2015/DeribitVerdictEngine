# Gap repair — same-millisecond page skip — SPEC

> ✅ **BUILT 2026-09-14 (UTC) on the trader's go — local only, not deployed, awaiting orchestrator review.** Build record and review packet: [`gap-repair-same-ms-page-skip-build-spec-back.md`](gap-repair-same-ms-page-skip-build-spec-back.md). The text below is the spec as accepted; deviations are listed in that packet, not edited in here.
>
> **Written** 2026-09-14 (UTC) by a scoped Opus/high seat. **AMENDED** 2026-09-14 (UTC) for the rulings below. Code handles pinned to `19e356b`. Venue measurements run live 2026-09-14 19:03–19:06 UTC.
>
> ✅ **RULED 2026-09-14 (UTC)** — trader-ruled on the orchestrator's reads, relayed by the orchestrator seat (`deribitverdictengine-87`):
>
> | Row | Ruling |
> |---|---|
> | `GR-1` | **(d) NEW** — repair by `trade_seq` range via `public/get_last_trades_by_instrument` |
> | `GR-2` | **(b)** two fetchers plus a contract fixture |
> | `GR-3` | **Closed by `GR-1` (d)** — not ruled (a) |
> | `GR-4` | **(b)** durable repair log, fetched by `tools/ops/collector.ps1` |
> | `GR-5` | **(b)** the S2 deploy waits for this fix |
>
> **Deploy target:** the single stop → swap → start after session S2 of [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §0 ("the S2 deploy" below). **Under `GR-5` (b) that deploy waits for this build.**

**IDs used in this document, defined once:**

| ID | Source and kind | Meaning |
|---|---|---|
| `GR-1` to `GR-5` | This spec, decision rows | The five choices in this spec's §3.2. The `GR` prefix avoids the `D-2` used by the absorption build |
| `GT-1` to `GT-7` | This spec's §0, implementer traps | Where the build will slip. `GT` = gap-repair trap; chosen because `S-1` and `S-4` already name other items in this repo |
| `A79a` to `A79f` | This spec, **planned** harness fixture IDs | ⚠ A plan, not a fact. Take the next free family in `verify/ordercheck/Program.vb` at build time. `A79*` was free at `2fa22da` |
| `A78b` | Harness fixture, shipped in `6509a0f` | The venue-check time pager keeps 2,500 of 2,500 trades across a same-ms page boundary |
| `A56a` to `A56g` | Harness fixtures, shipped | Hole-derived repair windows (`TradeStoreWriter.ResolveRepairWindowsMs`) |
| `A48d` · `A66c` | Harness fixtures, shipped | Gap-repair overlap is a no-op · the repair User-Agent names the running host |
| `DR-1` | [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §1, a ruled follow-up | Removed the `MinHoleMs` width floor. Kept the inverted-range drop and called it "inherent". **Amended by `GR-1` (d)** |
| `DR-3` | Same brief, a ruled follow-up | `BackfillTradeMonthAsync` returns rows appended, not the file's row count |
| `e3781e57…` | AWS `InstanceId`, [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a | The restart at 2026-08-17 16:23:05 UTC |
| `E-1` to `E-3`, `H-1` to `H-10` | This spec's §10 | `H-n` = a handle the reader can run. `E-n` = evidence the reader cannot re-run as-is |
| "seq endpoint" · "time endpoint" | Deribit public API | `get_last_trades_by_instrument` (by `trade_seq`) · `get_last_trades_by_instrument_and_time` (by timestamp) |

---

## 0. Implementer brief

**Model: Opus · Effort: HIGH · One session, three local commits.**

**Why that tier.** Three coupled changes land together, and each one moves shipped fixtures:

- **A new fetcher on a second endpoint.** Its contract is measured (this spec's §4.1), not documented, so the stub must copy the measurement exactly.
- **`ResolveRepairWindowsMs` returns `trade_seq` ranges instead of time ranges.** Every `A56` edge assertion remaps (this spec's §6.2). Deciding what each one asserts instead is the work; a deleted assertion reduces coverage silently.
- **A new durable sidecar**, written from the repair pass, plus one `collector.ps1` fetch-list entry.

**Templates in the tree:** `Core/VenueStatusLog.vb` and `Core/WsHealthLog.vb` for the sidecar · `A78VenueStub` / `A78PageJson` / `A78IdTrade` (`verify/ordercheck/Program.vb:14219`–`14258`) for page JSON · `CoverageReport.FetchVenueWindowAsync` stays as it is — it is the audit, not the template.

**Where it slips.** The implementer also writes the fixtures, so a misunderstanding can pass its own test. Each row names the input that exposes it.

| # | Trap | The input that exposes it |
|---|---|---|
| **GT-1** | ⛔ **The retention edge read as success.** A `start_seq` older than retention returns trades from the ~24 h edge, not an empty list (`H-9`). The skipped range must be counted as not served and logged | `A79c` part 1: tail from a `last.seq` below the stub's retention edge → `TAIL_PAST_RETENTION`, exact `not_served` count |
| **GT-2** | **A page loop that trusts `has_more` alone.** A page with `has_more: true` and zero trades loops forever | `A79b` part 2: stub returns `has_more: true`, 0 trades; the stub returns `Nothing` after 10 calls, so a missing guard shows as `FETCH_FAILED`, not a hang |
| **GT-3** | ⛔ **A tail that does not stop at `segEndInclMs`.** A seq tail with no `end_seq` runs to "now" (`H-9`). In a lookback that crosses a month end, the August pass would re-fetch September trades that streaming already wrote → duplicate rows | `A79c` part 4: two month files; the August tail must commit no trade after August's `segEnd` |
| **GT-4** | **The empty-file tail done with the time pager.** That reintroduces the time contract and its millisecond edge | `A79d` part 5: an empty file → one time call with `count=1`, then seq pages only |
| **GT-5** | **A clean pass writes no log line.** A dead repair timer then looks identical to a healthy one | `A79f` part 1: a clean pass writes exactly one `PASS_CLEAN` line |
| **GT-6** | **`A56b` part 5 deleted.** Its "unfetchable" 1 ms case is now an ordinary hole | `A56b` part 5 must assert the seq range is RETURNED |
| **GT-7** | **`1000` restated in a fixture.** `TradesPerPage` is `Private Const` today | Promote it to `Public Const` (CLAUDE.md "A value ruled into a CONSTANT goes `Public Const`"); fixtures read it |

**Escalation triggers — stop and come back.**

- ⛔ **The venue contract differs from this spec's §4.1** — a trade outside `[start_seq, end_seq]`, a duplicate or skipped seq across pages, or a cap that blocks a tail. **That is the orchestrator's named fallback trigger: `GR-1` (c) then needs a re-ruling.**
- Any `A48*` or `A56*` fixture fails for a reason you cannot explain in one sentence.
- The build needs a `settings.json` key, or a change to `TradeStoreWriter.AppendRows`, `DedupTrades`, `ScanForRepair`, `ResolveResumeCursorMs` or `CoverageReport.vb`.
- The repair log appears to need rotation or a size cap.

**Session plan — sequenced by dependency.**

| Step | Work | Commit |
|---|---|---|
| 1 | Extract today's time pager behind an injectable page source, with the `+ 1` cursor **kept**. `A48d`, `A56*` and `A66c` pass unchanged | 1 |
| 2 | Write `A79a` against the extracted time pager. Run it. ⛔ **It must FAIL at 2,499 of 2,500.** Paste the output into the spec-back as `E`-evidence | 1 |
| 3 | Add the seq fetcher (this spec's §4.3). Route `BackfillTradeMonthAsync` through it. **Delete the time pager.** `A79a` now passes on the seq path. Add `A79b`, `A79c` | 1 |
| 4 | Seq-range repair windows (this spec's §4.2). `A79d`. The `A56` remap (this spec's §6.2) | 2 |
| 5 | `Core/RepairStatusLog.vb`, the `TradeStoreGapRepair.vb` wiring, the `collector.ps1` `$FetchFiles` entry (this spec's §4.4). `A79f` | 3 |
| 6 | `A79e`. Full harness, `tools/checks/verify-gate.ps1`, Release build, the `DeribitIndicatorProject.md` §15 row | 3 |

⚠ **If context runs short, stop cleanly after commit 2.** Commit 3 (the log) depends on the outcome type from commit 1 but on nothing in commit 2.

Report back with a summary and a spec-back per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Do not push.

⛔ **Reporting condition — set by the orchestrator 2026-09-14 (UTC) at acceptance. No scope change.** The seq tail starts at `max.seq + 1` while the streaming writer is live, so one pass can fetch seqs that streaming commits during the same pass. `AppendRows` and `DedupTrades` stay untouched. **The build's spec-back must MEASURE and report whether a seq-tail pass appends seqs the streaming writer already committed** — by a fixture, or with a stated reason it cannot. Readers dedupe, so this is file growth, not data loss. ⛔ **Do not fix it in this build**; the fix belongs to the separate duplicate-rows task (this spec's §8).

---

## 1. The defect — verified

| Fact | How verified |
|---|---|
| `BackfillTradeMonthAsync` sets `cursorMs = newestMs + 1` after each page | `H-1`: `tools/BacktestRunner/HistoricalStore.vb:314` |
| Both time bounds are inclusive | `H-10`, re-runnable on fresh tape. `E-1` P1 and P4 |
| The skip is real on the venue | `E-1` P2 to P4, count 2 standing in for 1,000. Page 1 ends on seq `…126` at ms `…226`. A `+ 1` cursor returns nothing, so seq `…127` is lost |
| The live app runs this code | `TradeStoreGapRepair.vb:127` calls it with `repairHoles:=True`. `DeribitVerdictEngine.vbproj:51` compiles `HistoricalStore.vb` into the engine |
| `count` cannot exceed 1,000 on either endpoint | `E-1` P6 (time endpoint) · `H-7` (seq endpoint): `"value is too high"` |

---

## 2. Impact to date

### 2.1 ⛔ The loss is PERMANENT. The next pass cannot refill it, whatever the hole length.

The mechanism, from the code at `2fa22da`:

1. A repair page ends on trade `(T, s)`. The venue also holds `(T, s+1)` … `(T, s+k)`, which did not fit.
2. The cursor becomes `T + 1`. The next page starts at `(U, s+k+1)`, with `U ≥ T + 1`.
3. The next pass's walk sees `delta = k + 1` and emits a hole window `[T + 1, U − 1]` (`H-2`: `Core/TradeStoreWriter.vb:853`).
4. ⛔ **The missing trades sit at `T`. `T` is outside `[T + 1, U − 1]`.** Two outcomes, both empty:

| Case | What each pass does | Code |
|---|---|---|
| `U = T + 1` | The window inverts. It is dropped as "unfetchable" and logged | `Core/TradeStoreWriter.vb:881`, log at `:905`–`:910` |
| `U > T + 1` | The window is fetched. The venue returns zero trades. The loop exits on an empty page | `tools/BacktestRunner/HistoricalStore.vb:307` |

5. Each pass repeats this while the hole stays inside the 20 h lookback (`settings.json:558`). Then the hole ages out, and Deribit's ~24 h retention makes it unrecoverable. The orchestrator confirmed the seq endpoint does not extend retention (a 2026-08-10 seq returned 0 trades, even with `include_old=true`).

⚠ **This assumes `trade_seq` order agrees with timestamp order.** The walk already assumes it. Not proved.

### 2.2 ⭐ Production evidence — the defect has already fired

Source: the AWS store copy-back `aws_fetch/20260913-153704/backtest_data/` (fetched 2026-09-13). Scan: `H-4`.

| Measure | Value |
|---|---|
| `trade_seq` holes in `trades_2026-08.csv` (from 2026-08-12) | **17** |
| The one large hole | 14,405 trades, left row 2026-08-15 16:11:48 UTC. The part of a ~48 h outage past the 20 h lookback. **Not this defect.** Cause of the outage unrecorded ([`d3-asia-burst-watch-read-2026-09-14.md`](d3-asia-burst-watch-read-2026-09-14.md) §4, on `master`) |
| ⛔ **Small holes** | **16, missing 70 trades in total (1 to 16 each)** |
| Their dates | 2026-08-16 21:40:08 → 2026-08-17 16:23:04 UTC. **All inside the start-up pass's 20 h lookback** after the `e3781e57…` restart |
| ⭐ **Their file positions** | 13 left rows sit at 62,229 + 1,000·k rows from one block start; row 62,229 is the last row before the outage. **So each left row is the last row of a 1,000-row repair page.** The other 3 sit at exactly 2,000, 3,000 and 5,000 rows from a second block start |
| Still present | **Yes, 27 days later.** Hole-derived repair was live since `e551f15e…` (2026-08-13), so any later pass ran the current code |
| `trades_2026-09.csv` | **0 holes** in 1,235,249 distinct trades, 2026-09-01 → 2026-09-13 |

**Rate check.** The start-up window held 30,012 distinct trades, so about 30 page boundaries; 16 of about 30 is near 53 %. A simulation of `+ 1` paging over the local 2026-08-12 → 14 store gave 48.8 % (`E-2`).

### 2.3 When the skip happens

- **Only when a repair window holds more than 1,000 trades.** One page is ~14.7 min of tape at September's mean rate (67.8 trades/min, `E-3`).
- **A start-up pass after downtime longer than that**, or **a ride-through hole of that size.**
- **Not a normal deploy.** The 2026-08-13 deploy was a 5.5-second connect ([`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a, row `e551f15e…`).

### 2.4 ⚠ The wider class — not only page skips

**62.34 % of September's trades share a millisecond with a `trade_seq` neighbour** (`E-3`). The time window in this spec's §2.1 step 3 cannot fetch a lost trade that shares a millisecond with a stored neighbour, whatever lost it. **`GR-1` (d) removes the class: a seq range has no millisecond edge.**

---

## 3. Decisions

### 3.1 Fixed by the orchestrator

- **Deploy target: the S2 deploy.**
- ⚠ **WITHDRAWN 2026-09-14 (UTC) for the repair path:** the brief's "reuse `CoverageReport`'s contract; do not invent a third one". The orchestrator wrote it before repair was known to find holes by `trade_seq`. `CoverageReport.vb` keeps its time contract unchanged.

### 3.2 Decision table — with rulings

| # | Question | Options | Seat's read (as written) | ✅ Ruling |
|---|---|---|---|---|
| **GR-1** | How does repair fetch a hole or a tail? | **(a)** pager fix only · **(b)** time windows `[L.ts, R.ts]` bounded by `L.seq < seq < R.seq`; tail unchanged · **(c)** (b) plus tail `[max.ts, segEnd]` bounded by `seq > max.seq` · **(d)** fetch the missing `trade_seq` range itself via the seq endpoint | (c). (a) leaves this spec's §2.4 class unrepairable; `DR-1`'s "no sub-millisecond query" premise is false (`H-10`) | ✅ **(d) NEW — trader-ruled 2026-09-14 (UTC).** Hole = `[L.seq + 1, R.seq − 1]`; tail = from `max.seq + 1`. A hole IS a missing sequence range, so this is exact and self-describing. Amends `DR-1`: time is no longer the repair key. **Measured viable before amending (`H-6`–`H-9`); the stop condition did not fire.** (c) is the named fallback if the contract ever proves inexact |
| **GR-2** | One shared fetcher, or two? | **(a)** one fetcher in engine-linked code · **(b)** two fetchers plus a contract fixture · **(c)** a shared pure step function | (b). A shared fetcher makes the venue check share the defect it audits | ✅ **(b) — ruled 2026-09-14 (UTC).** Under (d) independence is stronger: repair fetches by `trade_seq`, the venue check by time |
| **GR-3** | A full page inside one millisecond? | (a) fail the window loudly · (b) seq fallback · (c) larger `count` — refused | (a), flagged cheaper | ✅ **CLOSED BY `GR-1` (d) — not ruled (a).** A seq page cannot stall inside one millisecond. The time pager is deleted (this spec's §3.3), so no time-paging path is left in `HistoricalStore.vb` |
| **GR-4** | A durable record of each repair outcome? | **(a)** Console only · **(b)** a durable repair log | (a) now, plus a queue row for (b) | ✅ **(b) — ruled 2026-09-14 (UTC).** An outcome-only sidecar in the `ws_health.log` / `venue_status.log` pattern, added to `collector.ps1` `$FetchFiles`. Console is captured nowhere in the tree (`H-3`) |
| **GR-5** | Not ready before the S2 deploy? | **(a)** S2 deploys without it · **(b)** S2 waits | (a) | ✅ **(b) — ruled 2026-09-14 (UTC).** Both are one-session builds. Re-rule only if this build stalls |

### 3.3 Auto-proceeded under the rulings — one line each

- **The time pager is deleted, the offline path included** (`repairHoles:=False`). One fetch path remains: at most one time call with `count=1` as an anchor, then seq pages. It closes `GR-3` everywhere, not only on the repair path.
- **Hole seq ranges get no time clamp.** `ScanForRepair` already scopes holes to the lookback. The venue's retention edge is reported as `not_served`, never hidden.
- **A seq tail from an old `last.seq` commits every trade the venue still serves**, including up to ~4 h older than the 20 h lookback (`H-9`: served from the 24 h edge). The richer option; exact by seq.
- **A tail stops at `segEndInclMs`** — mechanism (`GT-3`): otherwise a month-boundary pass double-writes the next month.
- **One `PASS_*` line per pass, even when clean.** It proves the timer ran — the gap this spec hit (this spec's §9). ~4 lines/day. The richer option over transition-only.
- **Trades returned outside the seq bounds, or without a `trade_seq`, are counted in the log and never committed.**
- **Dedup is by `trade_seq` within a window.**
- **`TradesPerPage` becomes `Public Const`**; the pager takes `pageSize` as a parameter (precedent: `ScanForRepair`'s `maxScanRows`, `Core/TradeStoreWriter.vb:948`–`950`).
- **Fixture IDs `A79a`–`A79f` — plan only.**

---

## 4. The fix shape

### 4.1 The seq endpoint contract — measured 2026-09-14 (UTC)

`public/get_last_trades_by_instrument?instrument_name=BTC-PERPETUAL&start_seq=…&end_seq=…&count=…&sorting=asc`

| Property | Measured result | Handle |
|---|---|---|
| `start_seq` and `end_seq` | **Both inclusive.** A range of exactly 1,000 returned 1,000 trades, first = `start_seq`, last = `end_seq` | `H-6` |
| Paging with `start_seq = last + 1` | **Exact.** A 2,501-seq range in 3 pages (1,000 · 1,000 · 501): 2,501 rows, 2,501 distinct, 0 duplicates, 0 missing | `H-6` |
| `has_more` | **More trades remain inside the requested range after this page.** Exactly 1,000 seqs with `count=1000` → `false`; 1,001 seqs → `true`; last page → `false` | `H-6` |
| `count` cap | **1,000.** `count=1001` → error `-32602`, `"value is too high"` | `H-7` |
| A seq with no trade | **None found** in 12,501 recent seqs (a further 10,000-seq range in 10 pages: 0 missing). `trades_2026-09.csv` also shows 0 holes | `H-8` |
| Empty returns | A future range and an inverted range (`start > end`) → 0 trades, `has_more: false`. A range wholly past retention → 0 trades, `has_more: false` | `H-8`, `H-9` |
| Tail, no `end_seq` | **Accepted.** Bounded by `count` and by the latest trade: from `M − 30` → 45 trades up to "now", `has_more: false`; with `count=10` → `has_more: true` | `H-9` |
| ⚠ **`start_seq` older than retention** | **Returns from the retention edge, not empty.** `start_seq = M − 400,000` and `M − 150,000` both returned first seq `299210562` at 2026-09-13 19:02:08 UTC — 24 h before the probe. The same with `end_seq = M − 50` | `H-9` |
| No `sorting` parameter | Ascending (a 10-seq range returned first `…601`, last `…610`). The build still sends `sorting=asc` | `H-9` |
| Time anchor, `count=1` | The time endpoint with `count=1` returns the first trade at or after `start_timestamp`, with its `trade_seq` | `H-9` |

⚠ **Measured on recent ranges, once each.** Not measured: a range spanning a venue maintenance window.

### 4.2 Repair windows — `Core/TradeStoreWriter.vb`

`ResolveRepairWindowsMs` returns seq-range windows. ⚠ Rename it `ResolveRepairWindows`; the `Ms` suffix would lie.

**New `Public Structure RepairWindow`** (replaces `LongRange` as the return type; `LongRange` stays if nothing else needs it — check by grep):

| Field | Meaning |
|---|---|
| `Kind` | `Hole` · `Tail` · `AnchoredTail` |
| `FirstSeq` | First `trade_seq` to fetch, inclusive. `AbsentSeq` for `AnchoredTail` until the anchor resolves it |
| `LastSeq` | Last `trade_seq` to fetch, inclusive. `AbsentSeq` = open |
| `AnchorMs` | `AnchoredTail` only: the time-endpoint `start_timestamp` |
| `StopAfterMs` | Tails only: commit nothing with a timestamp after this (`segEndInclMs`) |
| `LeftTsMs`, `RightTsMs` | Holes only: the bracket rows' timestamps. For the log, never for fetching |
| `MissingSeqs` | Holes only: `LastSeq − FirstSeq + 1`. The `MaxHolesPerPass` ranking key, unchanged |

**Rules.**

- **Hole:** for each walk `delta > 1` → `Hole` with `FirstSeq = prev.Seq + 1`, `LastSeq = cur.Seq − 1`. **No time clamp and no inverted-range drop** — `delta > 1` guarantees `FirstSeq ≤ LastSeq`. Rewrite the `DR-1` comment at `:868`–`:875` to say so. `MaxHolesPerPass`, `MaxScanRows` and the truncation log are unchanged.
- **Tail, last sorted row carries a seq:** `Tail`, `FirstSeq = last.Seq + 1`, `LastSeq` open, `StopAfterMs = segEndInclMs`. Emitted whenever `last.TsMs ≤ segEndInclMs`.
- **Tail, no rows:** `AnchoredTail`, `AnchorMs = segStartMs`, `StopAfterMs = segEndInclMs`.
- **Tail, last row has no seq** (legacy era, pre-2026-08-10 only): `AnchoredTail`, `AnchorMs = last.TsMs + 1` (clamped up to `segStartMs` when `clampToSegStart`). ⚠ The `+ 1` stays here and can miss legacy same-ms siblings; that era is closed.
- **Offline path** (`repairHoles:=False`, `HistoricalStore.vb:269`–`272`): `cursor0` from `ResolveResumeCursorMs` becomes `AnchoredTail`, `AnchorMs = cursor0`, `StopAfterMs = endMs`. `ResolveResumeCursorMs` itself is unchanged.

### 4.3 The seq fetcher — `tools/BacktestRunner/HistoricalStore.vb`

**Members.**

| Member | Role |
|---|---|
| `Public Const TradesPerPage As Integer = 1000` | Was `Private` |
| `Friend Shared Async Function FetchSeqPageJsonAsync(startSeq As Long, endSeq As Long?, count As Integer) As Task(Of String)` | The seq endpoint. HTTP plus retry-once on 5xx or timeout, lifted from `FetchTradesByTimeAsync` (`:343`–`:389`). Same `_http`, so `A66c` is unaffected |
| `Friend Shared Async Function FetchTimeAnchorJsonAsync(startMs As Long, endMs As Long) As Task(Of String)` | The time endpoint with `count=1`. The only time-endpoint use left in this file |
| `Friend Shared Function ParseTradesPage(json As String)` | Trades plus `has_more`. `Nothing` on a malformed body or a JSON-RPC error |
| `Public Class RepairWindowOutcome` | `Window` · `State` · `Committed` · `NotServedBefore` · `NotServedInside` · `NotServedAfter` · `Rejected` (out of bounds or no seq) · `Pages` · `Reason` |
| `Friend Shared Async Function FetchRepairWindowAsync(win, fetchSeq, fetchAnchor, commit, pageSize, pagesLeft, pageDelayMs) As Task(Of RepairWindowOutcome)` | This section's steps below. `fetchSeq` and `fetchAnchor` return a JSON body or `Nothing` — injectable, like `CoverageReport`'s `fetchPage` |
| `BackfillTradeMonthAsync` | Resolves windows, then calls `FetchRepairWindowAsync` per window with the shared page budget. Returns rows appended (`DR-3`, unchanged). **New `Optional outcomes As List(Of RepairWindowOutcome) = Nothing`** receives every outcome. The time-paging loop (`:285`–`:324`) and `FetchTradesByTimeAsync` are deleted |

**Steps for one window.**

1. **Resolve the start.** `Hole` / `Tail`: `cursor = FirstSeq`. `AnchoredTail`: call `fetchAnchor(AnchorMs, StopAfterMs)`. `Nothing` or unparseable → `FETCH_FAILED`. Zero trades → `TAIL_EMPTY` (Ok, nothing to do). Else `cursor` = that trade's `trade_seq`.
2. **Page budget.** Spent → `PAGE_CAP`. Stop the month, as today.
3. **Fetch** `fetchSeq(cursor, LastSeq or open, pageSize)`. `Nothing` or unparseable → `FETCH_FAILED`; abandon this window only.
4. **Walk the page in order.** `expected` starts at the resolved start. For each trade:
   - No `trade_seq`, or `seq < cursor`, or `seq > LastSeq` → `Rejected += 1`; skip.
   - A tail trade with timestamp `> StopAfterMs` → mark `reachedStop`; skip it and every later trade.
   - `seq` already seen in this window → `Rejected += 1`; skip.
   - `seq > expected` → the gap `seq − expected` adds to `NotServedBefore` if nothing has been served yet in this window, else to `NotServedInside`.
   - Keep the trade; `expected = seq + 1`.
5. **Commit** the kept trades (`TradeStoreWriter.AppendRows`), one call per page, as today.
6. **Stop** when `reachedStop`, or the page had zero trades, or `has_more` is `false` (absent → `page count ≥ pageSize`).
7. ⛔ **Progress guard:** `has_more: true` with zero kept or rejected-by-bound trades, or a last seq `< cursor` → `NO_PROGRESS`. Never loop.
8. **Else** `cursor = last seq on the page + 1`; wait `PoliteDelayMs`; go to step 2.
9. **Close a hole:** if `expected ≤ LastSeq`, add `LastSeq − expected + 1` to `NotServedAfter`.
10. **State.**

| State | When |
|---|---|
| `HOLE_REPAIRED` | Hole; every seq committed |
| `HOLE_PARTIAL` | Hole; `Committed > 0` and any `NotServed* > 0` |
| `HOLE_NOT_SERVED` | Hole; `Committed = 0` |
| `TAIL_OK` | Tail; `NotServedBefore = 0` and `NotServedInside = 0` |
| `TAIL_PAST_RETENTION` | Tail; `NotServedBefore > 0` — the venue served from its retention edge (`GT-1`) |
| `TAIL_GAP` | Tail; `NotServedInside > 0` |
| `TAIL_EMPTY` | Anchored tail; the anchor found no trade |
| `FETCH_FAILED` · `PAGE_CAP` · `NO_PROGRESS` | As in steps 1–3 and 7 |

### 4.4 The repair log — `GR-4` (b)

**New `Core/RepairStatusLog.vb`.** Contract mirrors `Core/VenueStatusLog.vb`: never throws · host-agnostic · no settings key · zero scoring impact · path `AppDomain.CurrentDomain.BaseDirectory` + `repair_status.log`, beside `ws_health.log` and `venue_status.log`.

**Line format** — the two existing logs' three fields, plus a detail field:

```text
utc | state | instance_id | detail
```

- `utc` — `yyyy-MM-ddTHH:mm:ss.fffZ`, as `VenueStatusLog`.
- `instance_id` — `ProcessIdentity.InstanceId` (`Core/ProcessIdentity.vb:37`).
- `detail` — space-separated `key=value`. ⛔ A `|` inside a value is replaced, so every line splits into exactly 4 fields.

**What is written — outcome-only, never per page.**

| Line | When | Detail keys |
|---|---|---|
| One **window line** | Every window whose state is not `TAIL_OK` or `TAIL_EMPTY` | `file` · `kind` · `seq=<first>..<last>` (`last` = `open` for a tail) · `committed` · `not_served_before` · `not_served_inside` · `not_served_after` · `rejected` · `pages` · `span=<LeftTs>..<RightTs>` (holes) · `reason` (failures) |
| One **pass line** | Every pass, after its windows | `windows` · `holes` · `committed` · `not_served` · `failed` · `pages` · `lookback_h` |

**Pass states.** `PASS_CLEAN` — no window line was written, or every one was `HOLE_REPAIRED` · `PASS_LOSS` — any `not_served > 0` · `PASS_FAILED` — any `FETCH_FAILED`, `PAGE_CAP` or `NO_PROGRESS`, or `RepairOnceAsync` caught an exception (`reason=exception`). `PASS_FAILED` wins over `PASS_LOSS`.

**Seams.** `Friend Shared Function FormatLine(...)` and `Friend Shared Sub AppendTo(path, ...)`, so `A79f` writes to a temp file; production calls go through `GetPath()`.

**Wiring — `TradeStoreGapRepair.vb`.** `RepairOnceAsync` passes one `outcomes` list to each `BackfillTradeMonthAsync` call, then writes the window lines and the pass line. Its "never throws" contract is unchanged. A disabled repair (`ShouldGapRepair` false) writes nothing.

**Ops — `tools/ops/collector.ps1:125`.** `$FetchFiles` gains the literal `'repair_status.log'`. A box that has not yet run a pass has no file; the existing absent arm handles it (`collector.ps1:342`, `:395` — "absent on box, skipped").

**Size.** ~4 pass lines/day at ~150 bytes ≈ 220 KB/year, plus window lines only on real holes. No rotation.

### 4.5 What does not change

`TradeStoreWriter.AppendRows` · `DedupTrades` · `ScanForRepair` · `ResolveResumeCursorMs` · `MaxHolesPerPass` · `MaxScanRows` · `CoverageReport.vb` · `settings.json` (no key) · `analysis_log.csv` header · every rendered surface.

---

## 5. Two fetchers, one tape — `GR-2` (b)

| Fact | Consequence |
|---|---|
| `HistoricalStore.vb` links into the engine (`DeribitVerdictEngine.vbproj:51`), `BacktestRunner` and `OrderCheck` | The repair fetcher lives on the engine side |
| `CoverageReport.vb` links into `BacktestRunner` and `OrderCheck` only | The venue check keeps its own time fetcher |
| ⭐ The venue check audits the store that repair writes | **Under (d) the two fetchers use different endpoints and different keys** — seq versus time. A defect in one cannot hide in the other |

`A79e` puts one stub tape behind both endpoints and asserts each fetcher against the tape, then against each other.

---

## 6. Fixtures

⚠ **Fixture-literal provenance (CLAUDE.md hard rule).** `TradesPerPage`, `MaxHolesPerPass` and `AbsentSeq` are READ, never restated. Timestamps, sequences, retention edges and small page sizes are MECHANISM; say so in a comment at the call site.

**Stub contract.** A new `A79SeqStub(tape, retentionEdgeSeq)` serves the seq endpoint exactly as this spec's §4.1 measured it: inclusive bounds · `count` cap · `has_more` = more remain in range · open `end_seq` → to the tape's end · `start_seq` below the edge → serve from the edge · a range wholly below the edge → empty. The time anchor reuses `A78VenueStub` with `count=1`. **Both stubs read one tape.**

### 6.1 New fixtures

| Planned ID | Asserts | ⛔ Mutation that must fail it |
|---|---|---|
| **`A79a`** | `A78b`'s tape (2,500 trades; trade 1000 shares trade 999's ms; seqs 700000–702499), empty store, one `AnchoredTail` over the window: **2,500 rows, 2,500 distinct `trade_id`s, `TAIL_OK`** | **Session step 2:** the same assertion over the extracted time pager → **2,499 of 2,500** (today's code). **Regression:** `cursor = last seq + 2` → rows skipped |
| **`A79b`** | **Part 1:** store holds seqs 800000 and 802600 only; tape holds 800000–802600 → one hole `[800001, 802599]`, 3 pages, 2,599 committed, `HOLE_REPAIRED`; a re-resolve returns no hole. **Part 2:** a stub page with `has_more: true` and 0 trades → `NO_PROGRESS`; the stub returns `Nothing` after 10 calls | Part 2: remove the progress guard → `FETCH_FAILED` (call cap), not `NO_PROGRESS` |
| **`A79c`** | **Part 1** (`GT-1`): retention edge at seq `E`; store `last.seq = E − 500` → `TAIL_PAST_RETENTION`, `NotServedBefore = 499`, committed from `E`. **Part 2:** a hole wholly below the edge → `HOLE_NOT_SERVED`, 0 committed, file unchanged. **Part 3:** tape missing one seq inside a hole → `HOLE_PARTIAL`, `NotServedInside = 1`. **Part 4** (`GT-3`): August file ends near month end, September file holds later rows; the August tail commits no trade after August's `segEndInclMs`, and no `trade_id` appears twice across the two files | Part 1: start `expected` at the first served seq → `NotServedBefore = 0`. Part 4: drop the `StopAfterMs` check → duplicates |
| **`A79d`** | `ResolveRepairWindows` under (d). **Part 1:** same ms `(T, s)`, `(T, s+2)` → `Hole [s+1, s+1]`. **Part 2:** 1 ms apart and 5 ms apart → `Hole [s+1, s+k]`. **Part 3:** tail after a seq row → `Tail`, `FirstSeq = last + 1`, open, `StopAfterMs = segEnd`. **Part 4:** end to end, part 1's hole through `A79SeqStub` commits exactly `s+1`. **Part 5** (`GT-4`): empty file → `AnchoredTail` at `segStartMs`; the anchor is called once with `count=1`. **Part 6:** seq-less last row → `AnchoredTail` at `last.TsMs + 1` | Part 4: restore the time window `prev.TsMs + 1L, cur.TsMs - 1L` → 0 committed |
| **`A79e`** | `GR-2` (b). One tape behind both stubs. `CoverageReport.FetchVenueWindowAsync(t0, t1)` and an `AnchoredTail` at `t0` stopping at `t1`: each `trade_id` set equals the tape's in-window set, and the two are equal | `cursor = last seq + 2` in the seq fetcher only → that fetcher misses the tape |
| **`A79f`** | `GR-4` (b), to a temp path. **Part 1** (`GT-5`): a clean pass → exactly one line, `PASS_CLEAN`. **Part 2:** a `FETCH_FAILED` window → its window line, then `PASS_FAILED`. **Part 3:** a `TAIL_PAST_RETENTION` window → `PASS_LOSS` with `not_served` equal to the window's count. **Part 4:** a `reason` containing a pipe character → every line splits into exactly 4 fields. **Part 5:** an unwritable path → no throw (the `A48e` pattern) | Part 1: skip the pass line when clean → 0 lines |

### 6.2 Existing fixtures to remap for (d) — do not delete

Found by an unanchored scan of `verify/ordercheck/Program.vb:10540`–`10960` for `+ 1` / `- 1` edges at `2fa22da`. Line numbers are from that commit.

| Fixture | Lines | Under `GR-1` (d) |
|---|---|---|
| `A56a` | `10566`–`10580` | Hole → `[1003, 1499]` with brackets `A56Ms(60000)` / `A56Ms(3660000)`; tail → `FirstSeq = 1502`, open, `StopAfterMs = segEnd` |
| `A56b` part 1 | `10617` | Tail `FirstSeq = 2011`. "Tail start equals `ResolveResumeCursorMs`" becomes: the **offline** anchor still equals `ResolveResumeCursorMs` (that function is unchanged) |
| `A56b` part 2 | — | A covered store (`segEnd = lastTs`) still returns the `Tail` window. The old "empty list" property becomes: **a covered pass commits 0 rows** (end to end through the stub) |
| `A56b` part 3 | — | Empty store → `AnchoredTail` at `segStartMs` |
| `A56b` part 4 | `10642`–`10644` | Hole → `[9001, 9004]` |
| `A56b` part 5 | `10658` | ⛔ **Invert** (`GT-6`): rows 1 ms apart → `Hole [9101, 9104]` is RETURNED. The unfetchable arm no longer exists |
| `A56c` | `10699`, `10709` | 0 phantom holes, unchanged; tail `FirstSeq = max seq + 1` |
| `A56d` | `10741`, `10753`, `10766` | 0 phantom holes, unchanged; per case, a seq-less last row → `AnchoredTail` at `last.TsMs + 1`, else a `Tail` from the last seq |
| `A56e` | `10795`–`10808` | ⚠ **Its property changes:** a hole straddling `segStartMs` is no longer clamped. Assert the seq range from the bracket row below `segStartMs`. Rename the fixture; keep the ID |
| `A56f` | `10840`–`10864` | The cap still keeps the largest by `MissingSeqs`; edges become seq ranges |
| `A56g`, `A48d`, `A66c`, `A78b` | — | Unchanged |

---

## 7. Acceptance and deploy

- ✅ Whole harness `ALL PASS`, including `A79a`–`A79f` and the remapped `A56` fixtures.
- ✅ Each new fixture's named mutation run once from a scratch backup; output recorded; file restored with an identical MD5 (the practice in [`venue-check-build-spec-back.md`](venue-check-build-spec-back.md) §1, `E-1`). Each mutation is named in a comment above its fixture.
- ✅ `A79a`'s fail-first run (this spec's §0 step 2) pasted as evidence.
- ✅ **The orchestrator's reporting condition (this spec's §0):** the spec-back states, by fixture or with a reason it cannot, whether a seq-tail pass appends seqs the streaming writer already committed.
- ✅ `dotnet build DeribitVerdictEngine.sln -c Release`: 0 errors. `tools/checks/verify-gate.ps1` passes.
- ✅ `grep -n "repair_status.log" tools/ops/collector.ps1` shows the `$FetchFiles` line.
- ✅ **A settings-untouched row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15:** engine-binary behaviour change on the tape path · repair now keyed by `trade_seq` · new `repair_status.log` sidecar · no scoring impact · not an `analysis_log.csv` dataset boundary · a store-completeness edge at the deploy's `InstanceId`.
- ✅ No `settings.json` bump. The commit message states that no card surface is affected (CLAUDE.md display-string parity rule). **No `[no-engine-change]` tag** on the build commits.
- ✅ **`GR-5` (b): the S2 deploy waits for this build.** ⚠ [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §0's deploy row does not yet say so — the orchestrator's edit, not this seat's.
- ✅ **At the S2 deploy:** the [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a row names these commits, says repair is keyed by `trade_seq` from that `InstanceId`, and names `repair_status.log`.
- ⏳ **Post-deploy, observational:** the first copy-back after the deploy carries `repair_status.log` with one `PASS_*` line per 6 h of uptime. After the next start-up repair longer than one page, `H-4` on that copy-back shows 0 small holes in the repaired span.

---

## 8. Out of scope — named so they are not lost

| Item | Why it is here |
|---|---|
| **`ResolveResumeCursorMs` still returns `last + 1`** (`Core/TradeStoreWriter.vb:690`) | Offline `BackfillAllAsync` only. Under (d) it becomes the anchor, so the offline resume point can still miss legacy same-ms siblings |
| ⚠ **`trades_2026-09.csv` holds 298,934 duplicate rows** — 1,534,183 rows, 1,235,249 distinct `trade_id`s (19.5 %), `H-5` | Cause not investigated; a task chip was raised. ⚠ **A hypothesis only, not verified:** the repair tail appends trades newer than the last flushed row, bypassing the streaming writer's `AlreadyCommitted` window, so a later streaming flush may write them again. At ~4 passes/day that cannot explain 298,934 rows alone |
| **No consumer reads `repair_status.log` yet** | The coverage report could grade an hour with `HOLE_NOT_SERVED` or `TAIL_PAST_RETENTION` as known venue-side loss. Its own spec |
| **A range spanning a venue maintenance window** | Not measured (this spec's §4.1). The `NO_PROGRESS` and `FETCH_FAILED` states cover it without looping |

---

## 9. ⚠ Not verified

- **That the 70 trades in this spec's §2.2 shared their left row's millisecond.** The venue history is gone. The inference rests on the 1,000-row spacing and the rate match.
- **That later 6-hourly passes ran after 2026-08-17 16:23 UTC.** No durable repair log exists — `GR-4` (b) closes this for the future.
- **That `trade_seq` order always agrees with timestamp order**, and **that the venue never skips a seq.** 12,501 recent seqs and 1,235,249 September trades show no gap; not proved. The fetcher counts a gap as `not_served` rather than assuming none.
- **The seq endpoint contract beyond what this spec's §4.1 measured** — each property once, on recent ranges.
- **How the AWS app's standard output is launched or captured.** Only the in-tree absence of `Console.SetOut` / `SetError` is verified.
- **`E-2`'s simulation** uses the local `backtest_data/trades_2026-08.csv` as a stand-in for venue tape.
- **Whether the coverage report grades an hour with such a hole `Defect`.** Carried from CLAUDE.md's worked example; not re-read in code.
- **Whether `LongRange` has users beyond `HistoricalStore.vb:266`–`287` and `Core/TradeStoreWriter.vb`.** Grep at `2fa22da` found none; re-check at build time.
- **No build and no harness run.** This is a spec.

---

## 10. Evidence handles

`H-1`–`H-5` were run 2026-09-14 (UTC) at `2fa22da`; `H-6`–`H-10` at 19:03–19:06 UTC the same day. The output shown is the actual output.

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

**H-6 to H-9 — the seq endpoint contract.** Live and read-only. Each run picks the latest seq `M`, so the probe does not decay. About 20 requests.

```bash
B="https://www.deribit.com/api/v2/public/get_last_trades_by_instrument?instrument_name=BTC-PERPETUAL"
seqs() { grep -o '"trade_seq":[0-9]*' | cut -d: -f2; }
tss()  { grep -o '"timestamp":[0-9]*' | cut -d: -f2; }
hm()   { grep -o '"has_more":[a-z]*' | cut -d: -f2; }
utc()  { date -u -d @$(( $1 / 1000 )) +%FT%TZ; }
page() { # $1 start_seq $2 end_seq(or -) $3 count -> prints "n first last has_more firstTs lastTs"; seqs to $PF
  local u="$B&start_seq=$1&count=$3&sorting=asc"; [ "$2" != "-" ] && u="$u&end_seq=$2"
  local R; R=$(curl -s "$u"); printf '%s' "$R" | seqs > "$PF"
  local n f l; n=$(wc -l < "$PF"); f=$(head -1 "$PF"); l=$(tail -1 "$PF")
  local t1 t2; t1=$(printf '%s' "$R" | tss | head -1); t2=$(printf '%s' "$R" | tss | tail -1)
  echo "n=$n first=${f:-none} last=${l:-none} has_more=$(printf '%s' "$R" | hm) ts=[${t1:+$(utc $t1)} .. ${t2:+$(utc $t2)}] err=$(printf '%s' "$R" | grep -o '"message":"[^"]*"')"
}
PF=$(mktemp); ALL=$(mktemp)
date -u
M=$(curl -s "$B&count=1&sorting=desc" | seqs | head -1); echo "latest seq M=$M"

echo "=== (a) paging a 2,501-seq range [M-2600, M-100], count 1000, start_seq = last+1"
S0=$((M-2600)); E0=$((M-100)); c=$S0; : > "$ALL"
for i in 1 2 3 4 5; do echo "page $i start_seq=$c: $(page $c $E0 1000)"; cat "$PF" >> "$ALL"; n=$(wc -l < "$PF"); [ "$n" -eq 0 ] && break; last=$(tail -1 "$PF"); h=$(curl -s "$B&start_seq=$c&end_seq=$E0&count=1000&sorting=asc" | hm); c=$((last+1)); [ "$h" = "false" ] && break; done
echo "union: rows=$(wc -l < "$ALL") distinct=$(sort -un "$ALL" | wc -l) dup_rows=$(( $(wc -l < "$ALL") - $(sort -un "$ALL" | wc -l) )) min=$(sort -n "$ALL" | head -1) max=$(sort -n "$ALL" | tail -1) expected=$((E0-S0+1)) missing_in_range=$(( E0-S0+1 - $(sort -un "$ALL" | wc -l) ))"
echo "exactly 1,000 seqs [M-1100, M-101]: $(page $((M-1100)) $((M-101)) 1000)"
echo "1,001 seqs [M-1100, M-100]:         $(page $((M-1100)) $((M-100)) 1000)"

echo "=== (b) count cap on this endpoint"
echo "count=1000: $(page $((M-1100)) $((M-101)) 1000 | cut -d' ' -f1,4)"
echo "count=1001: $(curl -s "$B&start_seq=$((M-1100))&end_seq=$((M-101))&count=1001&sorting=asc" | head -c 220)"

echo "=== (c) seq gaps over a wider recent range [M-12600, M-2601], paged"
S1=$((M-12600)); E1=$((M-2601)); c=$S1; : > "$ALL"; p=0
while [ $p -lt 15 ]; do p=$((p+1)); R=$(curl -s "$B&start_seq=$c&end_seq=$E1&count=1000&sorting=asc"); printf '%s' "$R" | seqs > "$PF"; cat "$PF" >> "$ALL"; n=$(wc -l < "$PF"); h=$(printf '%s' "$R" | hm); [ "$n" -eq 0 ] && break; c=$(( $(tail -1 "$PF") + 1 )); [ "$h" = "false" ] && break; done
echo "pages=$p rows=$(wc -l < "$ALL") distinct=$(sort -un "$ALL" | wc -l) expected=$((E1-S1+1)) missing=$(( E1-S1+1 - $(sort -un "$ALL" | wc -l) ))"
sort -un "$ALL" | awk 'NR>1 && $1-p>1 {printf "gap after %d: %d missing\n", p, $1-p-1; g++} {p=$1} END{print "gap_runs=" g+0}' | tail -5
echo "future range [M+100000, M+100010]: $(page $((M+100000)) $((M+100010)) 1000)"
echo "inverted range start>end:           $(page $((M-100)) $((M-200)) 1000)"

echo "=== (d) tail: start_seq with no end_seq"
echo "start_seq=M-30, no end, count 1000: $(page $((M-30)) - 1000)"
echo "start_seq=M+1,  no end:             $(page $((M+1)) - 1000)"
echo "start_seq=M-30, no end, count 10:   $(page $((M-30)) - 10)"
echo "=== (d2) retention straddle: a start_seq older than retention"
echo "start_seq=M-400000, no end, count 3:        $(page $((M-400000)) - 3)"
echo "start_seq=M-400000, end_seq=M-50, count 3:  $(page $((M-400000)) $((M-50)) 3)"
echo "start_seq=M-150000, no end, count 3:        $(page $((M-150000)) - 3)"
rm -f "$PF" "$ALL"
```

Output:

```text
Mon Sep 14 19:03:02 UTC 2026
latest seq M=299325429
=== (a) paging a 2,501-seq range [M-2600, M-100], count 1000, start_seq = last+1
page 1 start_seq=299322829: n=1000 first=299322829 last=299323828 has_more=true ts=[2026-09-14T18:36:53Z .. 2026-09-14T18:46:04Z] err=
page 2 start_seq=299323829: n=1000 first=299323829 last=299324828 has_more=true ts=[2026-09-14T18:46:04Z .. 2026-09-14T18:55:00Z] err=
page 3 start_seq=299324829: n=501 first=299324829 last=299325329 has_more=false ts=[2026-09-14T18:55:00Z .. 2026-09-14T19:01:30Z] err=
union: rows=2501 distinct=2501 dup_rows=0 min=299322829 max=299325329 expected=2501 missing_in_range=0
exactly 1,000 seqs [M-1100, M-101]: n=1000 first=299324329 last=299325328 has_more=false ts=[2026-09-14T18:48:31Z .. 2026-09-14T19:01:28Z] err=
1,001 seqs [M-1100, M-100]:         n=1000 first=299324329 last=299325328 has_more=true ts=[2026-09-14T18:48:31Z .. 2026-09-14T19:01:28Z] err=
=== (b) count cap on this endpoint
count=1000: n=1000 has_more=false
count=1001: {"jsonrpc":"2.0","error":{"code":-32602,"data":{"reason":"value is too high","param":"count"},"message":"Invalid params"},"testnet":false,"usIn":1789412590245366,"usOut":1789412590245497,"usDiff":131}
=== (c) seq gaps over a wider recent range [M-12600, M-2601], paged
pages=10 rows=10000 distinct=10000 expected=10000 missing=0
gap_runs=0
future range [M+100000, M+100010]: n=0 first=none last=none has_more=false ts=[ .. ] err=
inverted range start>end:           n=0 first=none last=none has_more=false ts=[ .. ] err=
=== (d) tail: start_seq with no end_seq
start_seq=M-30, no end, count 1000: n=45 first=299325399 last=299325443 has_more=false ts=[2026-09-14T19:02:49Z .. 2026-09-14T19:03:14Z] err=
start_seq=M+1,  no end:             n=14 first=299325430 last=299325443 has_more=false ts=[2026-09-14T19:03:05Z .. 2026-09-14T19:03:14Z] err=
start_seq=M-30, no end, count 10:   n=10 first=299325399 last=299325408 has_more=true ts=[2026-09-14T19:02:49Z .. 2026-09-14T19:02:49Z] err=
=== (d2) retention straddle: a start_seq older than retention
start_seq=M-400000, no end, count 3:        n=3 first=299210562 last=299210564 has_more=true ts=[2026-09-13T19:02:08Z .. 2026-09-13T19:02:08Z] err=
start_seq=M-400000, end_seq=M-50, count 3:  n=3 first=299210562 last=299210564 has_more=true ts=[2026-09-13T19:02:08Z .. 2026-09-13T19:02:08Z] err=
start_seq=M-150000, no end, count 3:        n=3 first=299210562 last=299210564 has_more=true ts=[2026-09-13T19:02:08Z .. 2026-09-13T19:02:08Z] err=
```

`H-6` = section (a) · `H-7` = section (b) · `H-8` = section (c) · `H-9` = sections (d), (d2) and the follow-up below.

**H-9 follow-up — a range wholly past retention, default sorting, the time anchor.**

```bash
B="https://www.deribit.com/api/v2/public/get_last_trades_by_instrument?instrument_name=BTC-PERPETUAL"
M=$(curl -s "$B&count=1&sorting=desc" | grep -o '"trade_seq":[0-9]*' | head -1 | cut -d: -f2); date -u; echo "M=$M"
p() { R=$(curl -s "$1"); echo "n=$(printf '%s' "$R" | grep -o '"trade_seq":' | wc -l) seqs=$(printf '%s' "$R" | grep -o '"trade_seq":[0-9]*' | cut -d: -f2 | sed -n '1p;$p' | tr '\n' ' ')has_more=$(printf '%s' "$R" | grep -o '"has_more":[a-z]*' | cut -d: -f2)"; }
echo "entirely past retention [M-400000, M-300000]:   $(p "$B&start_seq=$((M-400000))&end_seq=$((M-300000))&count=10&sorting=asc")"
echo "no sorting param, start_seq=M-20 end M-11:       $(p "$B&start_seq=$((M-20))&end_seq=$((M-11))&count=10")"
echo "time anchor count=1 at now-1h:                   $(p "https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time?instrument_name=BTC-PERPETUAL&start_timestamp=$(( $(date -u +%s%3N) - 3600000 ))&end_timestamp=$(date -u +%s%3N)&count=1&sorting=asc")"
```

Output:

```text
Mon Sep 14 19:05:59 UTC 2026
M=299325621
entirely past retention [M-400000, M-300000]:   n=0 seqs=has_more=false
no sorting param, start_seq=M-20 end M-11:       n=10 seqs=299325601 299325610 has_more=false
time anchor count=1 at now-1h:                   n=1 seqs=299319707 299319707 has_more=true
```

**H-10 — both time bounds inclusive, on fresh tape.**

```bash
B="https://www.deribit.com/api/v2/public/get_last_trades_by_instrument_and_time?instrument_name=BTC-PERPETUAL&sorting=asc"
now=$(date -u +%s%3N); L=$(curl -s "$B&start_timestamp=$((now-600000))&end_timestamp=$now&count=1000")
T=$(printf '%s' "$L" | grep -o '"timestamp":[0-9]*' | cut -d: -f2 | uniq -d | head -1)
N=$(printf '%s' "$L" | grep -o '"timestamp":[0-9]*' | grep -c ":$T\$")
echo "ms=$T trades_at_ms_in_list=$N"
echo "start=end=ms returns: $(curl -s "$B&start_timestamp=$T&end_timestamp=$T&count=1000" | grep -o '"trade_seq":[0-9]*' | wc -l)"
```

Output: `ms=1789411386112 trades_at_ms_in_list=2` · `start=end=ms returns: 2`

**E-1 — the original venue probes, 2026-09-14 ~18:29 UTC.** ⚠ The trades aged out after ~24 h; `H-10` replaces P1 and P4, `H-7` covers P6's cap on the seq endpoint.

| Probe | Request | Result |
|---|---|---|
| P1 | time endpoint, start = end = `1789406915226`, count 10 | seqs `299317126`, `299317127`; `has_more` false → **end inclusive** |
| P2 | start `1789406915224`, count 2 | `…125` at `…224`, `…126` at `…226`; `has_more` true |
| P3 | start `1789406915227` (a `+ 1` cursor), count 2 | no trades → **`…127` skipped** |
| P4 | start `1789406915226` (cursor = newest), count 2 | `…126`, `…127` → **start inclusive** |
| P5 | seq endpoint, `start_seq=299317126`, `end_seq=299317127` | both trades returned |
| P6 | time endpoint, count 1001 | error `-32602`, `"value is too high"` |

**E-2 — `+ 1` paging simulation.** Local `backtest_data/trades_2026-08.csv`, rows from 2026-08-12 00:00 UTC with a seq: 123,514 rows, seq-contiguous. Paging by 1,000 with a `+ 1` cursor: 123 boundaries, 60 with a skip (48.8 %), 242 trades skipped. One-off awk, not kept.

**E-3 — September production tape.** `trades_2026-09.csv`, deduplicated by `trade_id`: 1,235,249 trades over 18,217 min → 67.8 trades/min. 44.31 % of adjacent pairs share a millisecond; 62.34 % of trades share one with a `trade_seq` neighbour. Largest millisecond: 107 trades (September), 139 (August from 2026-08-12). ⭐ Re-runnable as `H-5` (largest millisecond) and `H-6` (same-ms share) in [`gap-repair-same-ms-page-skip-spec-back.md`](gap-repair-same-ms-page-skip-spec-back.md) §1. ⚠ That packet numbers its handles separately from this spec.
