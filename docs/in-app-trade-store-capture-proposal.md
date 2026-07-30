# In-App Trade-Store Capture — Proposal

**Status:** **APPROVED 2026-07-31** — D1–D5 all ticked (D1 ruled AGAINST the recommendation; see §7). Ready for an implementer. Build the approved spec; do not invent design decisions mid-code (CLAUDE.md / trader-profile §7).
**Target:** settings **v63 → v64** (one new `trade_store` block).
**Scoring impact:** **NONE.** No indicator reads it, no CSV column, no verdict path, no bridge field. It writes a sidecar the *backtester* consumes offline. **Not a dataset boundary.**
**Gate to build:** safe anytime. Nothing it touches is on the scoring path.
**Origin:** trader question 2026-07-31 — *"wouldn't it be more intuitive to build the daily fetch into the app itself?"* Yes, and the app can do better than fetching.

---

## 0. Why — the thing that makes this urgent rather than nice-to-have

Deribit's public trades endpoint serves **≈ 24 hours** of history and refuses older windows (measured, `backtest-synthesizer-proposal.md` §7.3). Candles have no such cap — the 6-month store fetched 2026-07-31 proves it (259,974 1m bars in minutes). **Trades are the exception, and they are the input the backtester cannot synthesise around.**

That matters because trade-derived signals — CVD, MicroCVD, TFI, aggressor velocity, liquidations — can only be *re-derived under different settings* from the raw tick stream. The `analysis_log.csv` already stores their **outputs**; storing an output keeps the answer and discards the question, so it cannot substitute (§1.3).

**Every day that passes without capture is a day of flow that becomes permanently unobtainable at any price short of a vendor.** The trader declined the Tardis option on cost (2026-07-31, ~$350–$700/mo, subscription not one-time), so **append-forward is now the only path** and its reliability is the whole ballgame.

The currently-planned mechanism — an external Windows Scheduled Task running `BacktestRunner fetch` daily — has three failure modes that are each **silent and unrecoverable past 24 h**: the task not firing (no interactive session, disabled by an update), the separate binary drifting out of sync with the store format, and nobody noticing until a study needs the data. The app, by contrast, is already up 24/7, already watched daily, and — decisively — **already receives every trade**.

---

## 1. Design

Two mechanisms, deliberately redundant. Neither alone is sufficient; together they have no single point of loss.

### 1.1 Streaming capture (PRIMARY) — capture at the source, no API call

`DeribitWsFeed.ApplyTrades` ([DeribitWsFeed.vb:330](../DeribitWsFeed.vb)) is already the per-trade batch hook — it folds aggressor velocity, absorption and the #7/#8 alerts. The raw stream is *already in hand*. Appending it to the store is a write, not a fetch.

- Each streamed trade is appended to `trades_YYYY-MM.csv` in the **existing store format** (`Timestamp,Price,Amount,Direction,Liquidation`), so `HistoricalStore.LoadTradeRange` reads it with no reader change.
- **Buffered**, not per-trade — 60k trades/day would otherwise mean 60k file opens. Flush on a timer and/or a count threshold (D2).
- **Monotonic guard:** track the last-written timestamp; skip anything `<=` it. This is what makes reconnect-reseed idempotent — `SeedAsync` re-seeds the trade ring from REST on every (re)connect, so the same trades WILL arrive twice. Same shape as `BackfillTradeMonthAsync`'s existing resume cursor.
- **Never throws.** The `SignalEmitter.TryWrite` / `liq_events.log` discipline: a disk error logs to console and is dropped. Losing capture must never kill the feed or the run.

**What this eliminates:** the 24-hour deadline, the API dependency, and the entire class of "the task didn't run." Capture is continuous while the app is up.

### 1.2 Gap repair (SECONDARY) — the one thing streaming cannot do

Streaming is complete while the app runs and recovers **nothing** from downtime. The fetch is the mirror image: it can backfill up to the venue's ~24 h after a crash, reboot or deploy.

So a low-frequency in-app timer runs the existing `HistoricalStore` backfill over a lookback window (D4), healing any gap the stream missed. The append path already resumes from the last on-disk timestamp, so **overlap is a no-op by construction** — this is the property A48d pins.

**Do not collapse these into one mechanism.** Streaming without repair loses every restart; repair without streaming reinstates the 24-hour deadline this proposal exists to remove.

### 1.3 Why not the analysis CSV — recorded so it isn't re-proposed

The trader asked whether the raw data could ride the existing per-analysis CSV. It cannot, for two independent reasons, and both are worth pinning:

1. **Scale.** `analysis_log.csv` writes **~382 rows/day** (measured, 8,410 rows / 22 days). The tape prints **~60,594 trades/day** (busiest full day in the store). That is **~160 raw trades per row** — a blob per row that destroys the CSV as an analytics surface.
2. **The derived values are not a substitute for the ticks.** The CSV already carries CVD / TFI / MicroCVD / AggrVel *outputs*. The reason the backtester wants raw trades is to **re-derive them under different settings**. A sweep over CVD windows needs the ticks; last run's CVD answers only last run's question.

---

## 2. The refactor — split `HistoricalStore`, don't move it whole

`HistoricalStore.vb` lives in `tools/BacktestRunner/`, which the root project deliberately excludes from its `**/*.vb` glob. It also **owns a live `HttpClient`** — the reason `BacktestFundingSample` was hoisted to a top-level type in the first place, so the harness could compile the slicing helpers without dragging networking in.

Moving it wholesale would push an `HttpClient` into the app's feed path and into the fixture project. Split it instead:

| New home | Contents | Links into |
|---|---|---|
| **`Core/TradeStoreWriter.vb`** (new, host-agnostic, **no network**) | file naming / monthly rollover, the append + monotonic guard, the row format, `LastTradeTimestamp` | app, BacktestRunner, harness |
| `tools/BacktestRunner/HistoricalStore.vb` (stays) | the network backfill (`Backfill*Async`), which now *uses* the writer | BacktestRunner, and the app for §1.2 only |

The app links `TradeStoreWriter` for streaming. For gap repair it needs the backfill too — which is the one place the app takes a dependency on the runner's networking, and is why D5 exists (an alternative is to leave repair manual).

This is the "one seam, no copies" move already made for `SignalEmitter.ComputeSideLevels`: the format lives in exactly one place, so the writer and the reader cannot drift.

---

## 3. Settings — new `trade_store` block (v63 → v64)

```json
"trade_store": {
  "enabled": true,
  "store_dir": "backtest_data",
  "flush_seconds": 30,
  "flush_trade_count": 500,
  "gap_repair_enabled": true,
  "gap_repair_interval_hours": 6,
  "gap_repair_lookback_hours": 20
}
```

`store_dir` is resolved **relative to the exe directory** (D3): the capturing box writes `<exe>\backtest_data\`. On AWS that is beside the deployed engine; the repo's own `backtest_data\` (which `BacktestRunner` uses from the repo root) is a *different* directory and stays the local analysis store, populated by copy-back.

**Add `A48h`:** the resolved path is exe-relative and does **not** depend on the process working directory — the app's cwd is not guaranteed, and a cwd-relative store would silently scatter files.

---

## 4. Fences, parity, boundary

- **Tweaker — HARD CONSTRAINT 27:** `trade_store.` **prefix** reject in `SettingsDiffApplier` + `PromptBuilder` rule 27. Data-capture plumbing has no failure-rate linkage — the same class as `alerts.` (HC25), `exit_guard.`, `live_strip.`, `signal_bridge.`. Prefix-safe: no other top-level `trade_store.` keys exist.
- **Display-string parity: NO OBLIGATION.** There is no rendered surface — no snapshot line, no card binding, no CSV column, no bridge field. Stated explicitly so the commit can say so.
- **Dataset boundary: NONE.** Nothing on the scoring path changes; `analysis_log.csv` is untouched in schema and content. Rows before and after remain fully comparable.
- **Reversibility:** `enabled:false` ⇒ the fold early-outs and nothing is written — byte-identical to pre-build, harness-proven (A48f).

---

## 5. Edge cases and safety

| Case | Handling |
|---|---|
| Reconnect re-seed delivers duplicate trades | Monotonic last-written guard drops them (A48b) |
| Month rollover mid-stream | Writer opens the new monthly file; header written only on create (A48c) |
| Disk full / path unwritable | Logged to console, capture silently degrades; **feed and analysis unaffected** (A48e) |
| App killed between flushes | Up to `flush_seconds` of trades lost; gap repair recovers them |
| Two boxes both capturing | **Cannot arise — D1 ruled AWS-only.** Ship with `enabled` defaulting appropriately for a single capturing box; if the local box is ever run with capture on, its store is its own and pools at read time (§7.1). |
| Gap repair overlaps streamed data | Append resumes from last on-disk timestamp ⇒ no-op (A48d) |
| REST fallback / `transport=rest` | No WS stream ⇒ no streaming capture; gap repair alone carries it |

---

## 6. Acceptance + fixtures

Fixture family **A48** (A47 consumed by the D3 A/B; next free after this is **A49**).

- **A48a** — appended rows are byte-compatible with `HistoricalStore.LoadTradeRange`: write via the writer, read via the shipped reader, round-trip identical.
- **A48b** — monotonic guard: replaying an identical batch twice writes once.
- **A48c** — month rollover: a batch straddling the boundary lands in two files, header written only on create.
- **A48d** — gap-repair overlap is a no-op: stream a window, then backfill the same window, assert no duplicate rows.
- **A48e** — unwritable path never throws and never blocks the fold.
- **A48f** — `enabled:false` ⇒ zero writes, fold inert.
- **A48g** — HC27 fence rejects every `trade_store.*` key; a sibling `scoring.*` tunable still passes.

Build acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0 Release**; A1–A47b unregressed + A48a–g; verify-gate `prepush` **GATE PASSED**.

---

## 7. D-table — awaiting the trader

| # | Decision | RULED 2026-07-31 |
|---|---|---|
| **D1** | Both boxes capture, or AWS only? | **AWS ONLY — ruled against the recommendation, on a better reason than the one I offered.** I argued for redundancy; the trader's ground is that **the end goal is the app running on AWS and not on the local box at all**, so dual capture builds for a topology the project is leaving. That supersedes the redundancy argument: redundancy across a box you intend to retire is not redundancy, it is migration debt. **Consequences, which the build must honour — see §7.1.** |
| **D2** | Flush policy | **Both triggers** as proposed — every `flush_seconds` (30) *or* `flush_trade_count` (500), whichever first. Time alone loses a burst on a crash; count alone can sit unflushed through a quiet Asia hour. |
| **D3** | Store location | **A directory inside the exe's own directory** — i.e. `store_dir` relative, resolved against the exe dir (`<exe>\backtest_data\`). As proposed, now explicit. |
| **D4** | Gap-repair cadence | **Every 6 h, 20 h lookback** as proposed. |
| **D5** | Does the app own gap repair at all? | **Yes** as proposed. |

### 7.1 What D1 = AWS-only changes — binding on the implementer

The single-box ruling removes the fallback the recommendation was resting on, so two things get *more* weight, not less:

1. **Gap repair (§1.2) is now the only recovery mechanism there is.** With no second box, an AWS outage means the stream stops and nothing else is capturing. Repair-on-restart must therefore be reliable and must run *promptly* after startup — **fire once on start, then on the D4 interval**, rather than waiting a full 6 h for the first tick. That is a build requirement, not a nicety.
2. **The daily health check carries real data risk now.** `aws-collector-deploy-checklist.md` §3 already prescribes a ~30-second daily RDP glance. Post-build that check is the only thing standing between an unnoticed app death and permanently lost tape. Worth adding the store's newest-file mtime to that glance.

Also settled by D1: **the store lives on AWS**, so any study needing trades takes a store copy-back alongside the CSV copy-back (§4.3b). There is no local store to pool with, so the merge question in §5 falls away entirely.

---

## 8. Implementation map

- **`Core/TradeStoreWriter.vb`** (new) — host-agnostic writer: paths, rollover, buffered append, monotonic guard, never-throws.
- **`tools/BacktestRunner/HistoricalStore.vb`** — format/append logic delegates to the writer; backfill retained.
- **`DeribitWsFeed.vb`** — `ApplyTrades` gains the buffered append (cfg read once per batch, the existing #5/#6/#7 pattern); flush timer; `SeedAsync` resets buffer state.
- **`Core/Settings/EngineSettings.vb` + `settings.json`** — `TradeStoreSettings` POCO + block; bump v63 → v64 + `change_log` + §15 entry.
- **`tools/AutoTweaker/SettingsDiffApplier.vb` + `PromptBuilder.vb`** — HC27.
- **`verify/ordercheck/Program.vb`** — A48a–g.
- **`tools/BacktestRunner/BacktestRunner.vbproj`** + root `.vbproj` — link the new shared file.

---

## 9. Out of scope

- **Order-book capture.** Vastly larger, and #6 absorption already ruled snapshot-based folding sufficient for now.
- **Backfilling the existing gap.** Nothing recovers pre-capture history; that was the Tardis question, declined.
- **Store compaction / retention.** ~2.4 MB/day, ~900 MB/year — revisit when it matters, not now.
- **Changing `analysis_log.csv`.** Explicitly untouched (§1.3).
