# History data store — trades from the history host on the dev machine, and the staged retirement of the collector's trade store — SPEC

**Asked by the trader 2026-09-29 (UTC):** reduce reliance on forward-collected data wherever the history host can supply it, and keep only forward-only data on the collector. Advice given and agreed the same day; this spec writes it down.
**Evidence:** [`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) (§1 and the §5a validation read). **Author:** orchestrator seat. **Class:** tools and ops. Stages 1–3 touch no engine code, no `settings.json`, no scoring. Stage 4 is reserved to the trader.

⛔ **Holiday rule:** the trader is away 2026-10-14 → 2026-11-25. Nothing in this spec is built or switched before 2026-11-25. The collector keeps capturing trades through the holiday as insurance.

---

## 0. Implementer brief — for the stage-2 build (after 2026-11-25)

**Model: Opus 5.5 · Effort: high.**
- **Why that tier:** the paging rule is subtle, the store is shared with every trade reader in the repo, and a quiet mistake (a lost same-millisecond trade, a duplicated page) corrupts every study built on it. The format, dedup and parse seams already exist (`Core/TradeStoreWriter.vb`); the new work is the history-host pager, resume, and the comparison.
- **Where it will slip:**
  1. **Paging by timestamp.** Never. The history host widens a `start_seq`/`end_seq` range to whole milliseconds; page by seq, de-duplicate on `trade_seq`, split a page whose `has_more` is true (§3.2). `+1 ms` time paging lost 70 trades in production once (the 2026-09-14 same-ms page-skip finding).
  2. **Writing trades younger than the settle margin.** The `liquidation` flag arrives about 60 min after the trade (n = 1). A row written before that is stored `none` and never corrected (§3.4).
  3. **A fixture that cannot fail.** Every fixture below names the input that makes it fail and is mutation-proved (`CLAUDE.md` rules; the "fixture shape must admit the failure" lesson).
- **Escalation triggers — stop and come back:**
  - The comparison (§4) finds any trade in the box store that the dev store lacks, or any field mismatch.
  - The history host returns errors or throttling that the pacing rule (§3.5) does not absorb.
  - The build seems to need a `settings.json` key, an engine file, or a change to `Core/TradeStoreWriter.vb`'s row format beyond the ruled `HDS-1` option.
- **Sessions:** one for the tool and fixtures; the backfill itself is a long unattended run, started by the seat and checked later.

---

## 1. Inventory — every engine input, and whether history exists for it

| Input | Engine use | History source | Status after this spec |
|---|---|---|---|
| **Trades** (price, amount, direction, ids, `trade_seq`, `liquidation`, `mark_price`, `index_price`, `tick_direction`, `contracts`) | CVD, MicroCVD, TFI, aggressor velocity, liquidations; backtest replay | ✅ `history.deribit.com`, complete and exact in the tested windows, back to at least 2020, with liquidation flags (validation read §5a) | **Moves to the dev machine** (stages 2–4) |
| Candles 1 / 3 / 5 / 15 m | Most indicators | ✅ Main host (`get_tradingview_chart_data`); the backtest store already fetches them on the dev machine | Unchanged |
| Funding rate | Step 3 / 3b | ✅ Main host funding history; already in the backtest store | Unchanged |
| **Order book** (top 10) | OFI, absorption, spread | ❌ No history source known | Forward-only. **Not stored today** (live in memory only) |
| **OI, sub-minute ticker fields** | OI delta, strip | ❌ None known (⚠ not verified this session) | Forward-only. Not stored today |
| **Engine outputs** (`analysis_log.csv`, eval cache) | Every study | ❌ They are the engine's own record | **Stay on the collector** |

**Consequence to state plainly:** a replay over backfilled trades regenerates the **trade-derived** signals for any past period. It does **not** regenerate past verdicts exactly, because book-based votes (OFI, spread, absorption) have no history. Studies whose arms depend on logged verdicts (for example the burst outcome read, [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md)) still need forward rows.

**Readers of the trade-store format today** (grep 2026-09-29): `Core/TradeStoreWriter.vb` (format, parse, dedup), `tools/BacktestRunner/HistoricalStore.vb`, `CoverageReport.vb`, `ReplayLoop.vb`, `tools/ops/SwingFallbackRead/LiquidationFlagCheck.vb`, `tools/ops/history_host_validate.py`, fixtures in `verify/ordercheck/Program.vb`. The dev store uses the **same file layout and row format**, so every reader works on it unchanged.

---

## 2. Staged plan

| Stage | What | When (UTC) | Gate to start | Reserved? |
|---|---|---|---|---|
| **1** | This spec, and the trader's rulings on §6 | Before 2026-10-14 | — | Rulings: yes |
| **2** | **Build** the dev-machine history store (§3) with fixtures and the comparison tool (§4) | After 2026-11-25 | Stage 1 ruled | No (tools only) |
| **2b** | **Backfill** to the ruled depth (`HDS-2`), unattended, in resumable chunks | After the build | Stage 2 accepted | No |
| **3** | **Parallel run:** the box keeps capturing; the dev store tops up from the history host; the comparison runs after every `collector.ps1 fetch` | After 2b | Backfill complete | No |
| **4** | **Switch off** box capture: `trade_store.enabled` → `false` in tracked `settings.json` (version bump); `collector.ps1 fetch` stops copying `backtest_data`; the box's existing store is archived to the dev machine, then removed from the box | After stage 3 passes (§4.2) | Stage 3 clean | ⛔ **Yes** — a settings change, and a deletion on the box |
| **5** | Retire what existed only to keep the box store complete: gap repair, the write guard, `CoverageReport`, the venue check. Its own spec | After stage 4 has run clean for a while | Stage 4 done | Spec first; engine-code removal and deploy are reserved |

**Rollback at any stage:** `trade_store.enabled` → `true` restarts capture; the dev store keeps everything already fetched.

### 2a. ⛔ DROP LIST — items that retire with the box trade store (trader-directed 2026-09-29)

**Whoever runs stage 4 or stage 5 must work through this list and mark each item DROPPED in `trader-tick-queue.md` with the date and this section as the reason.** Each item exists only to keep the box's trade store complete or to judge that completeness. Until stage 4, keep them.

| # | Item | Where it lives | Drop at | Note |
|---|---|---|---|---|
| 1 | `ws_health.log` under-reports capture outages | `trader-tick-queue.md` §2 row | Stage 5 | Its consequence was the coverage report treating `OK` as capture evidence. ⚠ Check first that nothing else uses `ws_health.log` as capture evidence (not verified 2026-09-29); `WsHealth` itself stays (CSV column, signal bridge) |
| 2 | `D-2b` `SEQLESS_AFTER_CUTOVER` watch and its read-backs | `trader-tick-queue.md` state banner (pre-holiday row 2); [`gap-repair-rr1-d2-d2b-spec-back.md`](gap-repair-rr1-d2-d2b-spec-back.md) | Stage 5 (with gap repair) | Keep the read-backs until then |
| 3 | The automatic post-fetch venue check (`tools/ops/venue-check.ps1`, the `collector.ps1` hook) | `trader-tick-queue.md` §2 "Run S0 `--verify-venue`" row | Stage 4 | Its dated review was dropped 2026-09-29; the check keeps running as a monitor until box capture stops |
| 4 | Gap repair (`TradeStoreGapRepair.vb`, `HistoricalStore` repair path, `repair_status.log`), the write guard, `CoverageReport` | Code | Stage 5 | Engine-code removal: its own spec, reserved deploy |
| 5 | `collector.ps1 fetch` copying `backtest_data` | `tools/ops/collector.ps1` | Stage 4 | Part of stage 4 itself |
| 6 | **The adversarial audit's box-store rows, NOT fixed on the box** (trader ruling `AT-3` (b), 2026-09-29): rows E1–E6 (tape-store integrity), row C19 and decision C-19's coverage-report fixes, and row B5's gap-repair half, all in [`adversarial-audit-2026-09-24.md`](adversarial-audit-2026-09-24.md) §B/§C | [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §2.3 | Stage 4 (C19, with the venue check) · Stage 5 (the rest, with gap repair) | Their lessons go into the dev store (§3.7). ⚠ Until stage 4, a `--strict` coverage pass is not proof of completeness (row C19); the history host is the completeness evidence |

### 2b. Items the history store reshapes — edit them when the store exists

| Item | Where | Change when the store exists (after stage 2b) |
|---|---|---|
| Re-derive `large_liq_size` from real liquidation sizes (finding `L-3`, ruling `D-6`) | `trader-tick-queue.md` §2 liquidation-flag row | **No longer waits for engine-fix B2.** Run it on the dev store's flagged trades |
| `A4` liquidation × OFI flip (queue item `E7`) | `trader-tick-queue.md` §1 Cluster E | The liquidation half is available historically; the OFI half (book) is still forward-only, so the study stays blocked on the book side |
| Engine-fix B2 | `trader-tick-queue.md` §2 liquidation-flag row | Research no longer needs it; only live scoring does. Still gated on the raw-channel result |

---

## 3. Stage 2 — the dev-machine history store

### 3.1 Home and layout

| Item | Rule |
|---|---|
| Tool | A new verb in `tools/BacktestRunner` (VB, host-agnostic, no WinForms), e.g. `history --to <date> [--from <date>]`. It reuses `TradeStoreWriter`'s file naming, row format, `TryParseRow` and `DedupTrades`. It never touches the collector |
| Store location | A configurable folder **outside the repo**, default `C:\DeribitData\history\` (the dev machine had 38 GB free on 2026-09-29) |
| Layout | Monthly files `trades_YYYY-MM.csv`, the same as the box store, so `ReplayLoop`, `CoverageReport` and the other readers work unchanged |
| Row format | The box store's 7 columns, **plus** the columns ruled under `HDS-1` |

### 3.2 Paging (measured 2026-09-28; non-negotiable)

- Page with `public/get_last_trades_by_instrument` on `https://history.deribit.com/api/v2/`, `start_seq` / `end_seq`, `count=1000`, `sorting=asc`.
- The host **widens the range to whole milliseconds**: every trade sharing a ms with either bound comes back, unordered inside the ms. **De-duplicate on `trade_seq`.**
- If `has_more` is true, **split the range and re-request** so no millisecond group is truncated.
- Find a day's seq bounds with `get_last_trades_by_instrument_and_time` (first trade at or after the day start, last before its end), then page by seq. **Never page by timestamp.**
- Template: `tools/ops/history_host_validate.py` (`page_range`, `seq_at`).

### 3.3 Resume and idempotence

- Work in day chunks. After each day, the day's file is written and the day is recorded as complete in a small checkpoint file beside the store.
- A re-run over a completed day is a no-op; a re-run over a partial day re-fetches that day and de-duplicates. **No duplicate rows and no gaps across a kill at any point** (fixture in §5).

### 3.4 Settle margin — the late liquidation flag

- Only ingest trades **older than 24 h** at fetch time. The flag was seen arriving ~60 min late (n = 1, [`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) §0000); 24 h is a wide margin and costs nothing, because the dev store serves research, not live scoring.
- The margin is a `Public Const` (`CLAUDE.md`: a ruled constant is public so fixtures read it).

### 3.5 Pacing

- Sequential requests, a 150 ms pause (the measured 2026-09-28 pace: ~1.1 req/s, ~880 trades/s, 0 errors in 451 requests).
- On an error: back off (2 s, 4 s, …), retry up to 5 times, then stop the chunk and report. Never skip a range.
- **Do not add parallelism** without first reading Deribit's published rate limits and recording them; untested.

### 3.6 Top-up

- A session-start command: fetch every complete day from the last checkpoint to (now − 24 h). Expected cost: a few minutes per day of tape.

### 3.7 Lessons from the 2026-09-24 adversarial audit (trader ruling `AT-3` (b), 2026-09-29)

The audit found box-store defects that are not fixed on the box, because that store retires ([`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §2.3). The dev store must not repeat them. Row IDs are rows of [`adversarial-audit-2026-09-24.md`](adversarial-audit-2026-09-24.md) §B.

- **A torn row must not poison a scan (row E1).** On the box, a half-written row mid-file becomes a phantom hole of about 3×10¹⁵ seqs, a lost trade, or a tail window disabled for the month. Parse defensively: a row that does not parse is counted and reported, and it never becomes a seq bound.
- **Count rows only after the write is flushed (row E2).** On the box, repair reports `PASS_CLEAN` over pages that never reached disk, and a full disk still counts rows as appended. A day is checkpointed complete (§3.3) only after its file is flushed and re-read.

---

## 4. The comparison — the gate for stage 4

### 4.1 Tool

- Extend `tools/ops/history_host_validate.py`, or add a verb to the same tool, so it compares **the box store (from a fetch folder) against the dev store**, window by window: seqs only in the box, seqs only in the dev store, field mismatches (`trade_id`, `timestamp`, `price`, `amount`, `direction`), and liquidation flags.
- Wire it after `collector.ps1 fetch`, like the venue check. **Read-only.**

### 4.2 Pass rule for stage 4 (fixed now)

- **At least 3 comparisons spanning at least 14 calendar days**, each with: 0 seqs only in the box store, 0 field mismatches. Seqs only in the dev store are expected (box holes) and are counted, not failures.
- Any failure stops the plan and goes to the trader.

---

## 5. Acceptance for the stage-2 build — handles

| Handle | What | Pass |
|---|---|---|
| `H-1` | Backfill one day that the box store also holds (for example 2026-09-27), then run the comparison against `aws_fetch/20260928-121255` | 0 seqs only in the box store, 0 field mismatches; the dev store's seq span is contiguous |
| `H-2` | Kill the backfill mid-day, re-run | The day ends contiguous, with no duplicate `trade_seq` |
| `H-3` | Re-run a completed day | No file change (byte-identical) |
| `H-4` | Settle margin | No row younger than the constant at fetch time |
| `H-5` | Harness and gate | Fixtures pass; `tools/checks/verify-gate.ps1` passes |

**Fixtures (new family at the next free ID):** the paging dedup (a synthetic page set with overlapping ms groups gives each seq once), the `has_more` split (a truncated page triggers a split, no seq lost), resume (a partial day re-run gives the same file as an uninterrupted run), and the settle margin. Each mutation-proved.

---

## 6. Decisions for the trader — `HDS-1` to `HDS-4` (new IDs; checked free in `docs/`, `Core/`, `tools/`, `verify/` on 2026-09-29)

✅ **ALL RULED 2026-09-29 (trader): `HDS-1` = (b) (append the 4 fields), `HDS-2` = 12 months newest-first then background to 2025-01-01 (see its row), `HDS-3` = (b) (no box fallback), `HDS-4` = (a) (archive, then delete from the box at stage 4).** Build consequence for §3: **the backfill runs newest day first**, so the most representative data lands first and a study can start before the backfill completes.

| ID | Question | Options | Orchestrator read |
|---|---|---|---|
| `HDS-1` | Extra trade fields in the dev store? | (a) Keep the box store's 7 columns · (b) Append `mark_price`, `index_price`, `tick_direction`, `contracts` as columns 8–11 (`TryParseRow` already tolerates longer rows) | **(b).** It records more, the history host gives them for free, and adding columns later means re-fetching the whole history. ⛔ Reserved: a schema change to a shared format. The box store stays at 7 columns (it is being retired) |
| `HDS-2` | How far back to backfill? — ⭐ **RE-RULED 2026-09-29 (trader), supersedes the ruling below: ALL 21 MONTHS (to 2025-01-01) IN ONE CLOUD BACKFILL ON A TEMPORARY AWS INSTANCE** (same region as the collector, `EC2-SSM-Access` profile, month by month compressed to S3, pulled to the dev machine, validated there with §4, instance terminated). **Never on the collector box** (1 GB RAM, ~7 GB disk, live engine). Runs the stage-2 tool after its build; no second code path. — ~~Earlier ruling the same day: 12 months first, newest day first, then continue backwards to 2025-01-01 as a low-priority background job while disk allows** (reasoning: a study's own power check sets depth; older data is less representative; extension costs the same per day later; the background leg is insurance against the history host changing). Original options and read kept below | (a) 2026-07-22 (the box store's start) · (b) 2025-01-01 · (c) 2024-01-01 · (d) 2020-01-01 | **(b) 2025-01-01**, about 21 months: roughly 6–7 GB uncompressed at 7 columns, ~9 GB with `HDS-1` (b), and ~35–55 h of fetching in overnight chunks, within the dev machine's 38 GB free. It covers several volatility regimes (the burst finding shows regime matters). Deeper is a later top-up if a study needs it. ⚠ This is the cheaper option against (c)/(d); I pick it on disk space (38 GB free, 92 % used), not on effort. Say if you want deeper |
| `HDS-3` | Build the box gap-repair fallback to the history host (holes older than 24 h)? | (a) Yes · (b) No, the dev store covers history and the box store is being retired | **(b).** It would be engine code, a deploy, and work thrown away at stage 5. During the parallel run a box hole is simply "only in the dev store" |
| `HDS-4` | What happens to the box's existing trade store at stage 4? | (a) Archive to the dev machine, then delete from the box · (b) Leave it on the box, frozen · (c) Delete without archiving | **(a).** The dev store supersedes it, but the archive keeps the exact box copy for audit. (c) loses information for no gain |

---

## 7. What this spec does not cover

- Real-time liquidation data for scoring — that is the raw-channel test ([`raw-channel-liquidation-test-spec-back.md`](raw-channel-liquidation-test-spec-back.md)).
- Order book, OI and ticker history — none known; they stay forward-only.
- Stage 5's code removal — its own spec.
- Re-planning individual studies onto the dev store — each study decides when the store exists.

## 8. What I did not verify

- The history host's rate limits and terms of use.
- Completeness beyond the tested windows (36 h in September 2026, one day in June 2025).
- That no public history source exists for the order book or OI.
- The yearly size (~3.7 GB) and fetch time (~16–20 h): extrapolated from ~170k trades/day and the measured pace.
- Whether the rising page-in rate on the box is caused by repair scans over the trade store (the hoped-for side benefit of stage 4).
