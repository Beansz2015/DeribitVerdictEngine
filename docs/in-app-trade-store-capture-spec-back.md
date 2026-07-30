# In-App Trade-Store Capture — spec-back

**Build:** settings **v64**, 2026-07-31. Local commit; trader tests + pushes.
**Against:** `in-app-trade-store-capture-proposal.md` (APPROVED 2026-07-31, D1–D5 ticked).
**Acceptance:** six Release builds **0/0** (solution · AutoTweaker · WhatIfRunner · CeilingAudit · BacktestRunner · OrderCheck); harness **A1–A47b unregressed + A48a–h**, ALL PASS; verify-gate `prepush` **GATE PASSED** (display-parity clean, version-bump clean).

---

## 1. Ranked verification handles

What to look at first if you only look at a few things.

1. **`Core/TradeStoreWriter.vb` — the whole seam.** Everything else is plumbing around it. Read `Buffer` / `Flush` / `ResetBufferState` (the monotonic guard's three states) and `AppendRows` (month split + header-on-create + never-throws). If this file is right, the build is right.
2. **`DeribitWsFeed.SeedAsync`, the trade-store block.** The one place where getting the ordering wrong loses data rather than duplicating it: it **flushes first, then un-seeds** the guard. Flush-after-unseed would re-admit the pending buffer's own trades; unseed-without-flush would silently drop captured tape on every reconnect.
3. **`TradeStoreWriter.ResolveResumeCursorMs` + its one caller.** This is the "overlap is a no-op by construction" claim, extracted so it is testable without a live HTTP call (§2.1 below). The `clampToSegStart` flag is the retention guard — the interesting failure is the one it prevents, described at §3.1.
4. **`ResolveStoreDir` and A48h.** D3 in one function. The fixture moves the process working directory mid-test and asserts the resolved path does not move with it.
5. **HC27 in both places.** `SettingsDiffApplier.RejectedPathPrefixes` and `PromptBuilder` rule 27. A48g checks all seven keys reject, a sibling `scoring.*` key still passes, and the prompt actually mentions the fence.

Not worth your time: the settings POCO, the `settings.json` block, the `MainForm` start/stop pair. Mechanical and covered.

---

## 2. Deviations from the spec — both mechanical, neither a design decision

### 2.1 The reader moved onto the seam, and so did the resume cursor

§2's table gives the writer "file naming / monthly rollover, the append + monotonic guard, the row format, `LastTradeTimestamp`". The build puts **two more things** there.

**The row PARSE.** `HistoricalStore.LoadTradeRange` now delegates its per-file read to `TradeStoreWriter.ReadTradeFile`. Reason: A48a asks for "write via the writer, read via the shipped reader, round-trip identical", and the fixture project deliberately does not link `HistoricalStore` (it owns the `HttpClient` the §2 split exists to keep out). Re-implementing the parse in the fixture would have been exactly the copy §2 forbids. Putting the parse on the seam makes the round-trip test exercise the shipped code *and* satisfies §2's stated goal better than the original split did — "the format lives in exactly one place, so the writer and the reader cannot drift" is more true when the reader is also there.

**The resume-cursor decision** (`ResolveResumeCursorMs`). Same reason: A48d's claim is about arithmetic that lived inline in a network function. It is now a pure function the network backfill calls verbatim, so the tested decision and the live decision are the same code rather than two things that look alike.

Neither changes behaviour. Both make a fixture honest that would otherwise have been a re-implementation wearing a fixture's name.

### 2.2 `BacktestFundingSample` hoisted out of `ReplayLoop.vb`

D5 puts gap repair in the app, so the root project must reach `HistoricalStore.BackfillTradeMonthAsync`. The root `.vbproj` excludes `tools\**\*.vb`, and re-including `HistoricalStore.vb` alone does not compile: it references `BacktestFundingSample`, which was declared at the top of `ReplayLoop.vb` — 37 KB of replay machinery the app has no business linking.

The struct now lives in `tools/BacktestRunner/BacktestFundingSample.vb`. Five lines, no behaviour. The root project links two tools files (`BacktestFundingSample.vb` + `HistoricalStore.vb`), following the existing `TweakerConfig.vb` re-include precedent. Note the original hoist to top-level was done for the *same class* of reason — keeping `HistoricalStore`'s `HttpClient` off the harness's link surface — so this is that decision applied once more, not a new one.

---

## 3. Decisions I made inside the spec, with my read

### 3.1 Gap repair clamps its resume cursor; the historical backfill does not

`BackfillTradeMonthAsync` resumes from the last on-disk timestamp. After an outage longer than Deribit's ~24 h trade retention, that cursor points at a window the venue **refuses** — so an unclamped repair pass would recover *nothing*, including the recent hours that are still served. That is a silent total failure in exactly the scenario repair exists for.

So the method gained `clampToSegStart` (default `False`), and only the in-app repair passes `True`. I deliberately did **not** make the clamp unconditional: with it always on, a historical backfill asked for a window starting later than its stored data would skip the hole between the two instead of filling it. Under `BackfillAllAsync` that case does not arise, but silently narrowing the runner's behaviour as a side effect of a capture build is the wrong trade. A48d pins both arms.

**My read:** this is the right shape, and `gap_repair_lookback_hours: 20` is doing real work rather than being a round number — it is the clamp's value.

### 3.2 One process-wide append lock

Streaming capture and gap repair append to the same monthly file from different threads. Without serialisation a flush could interleave mid-line with a backfill page. `AppendRows` takes a static lock; ordering *across* the two is not guaranteed and does not need to be, because `LoadTradeRange` sorts by timestamp and dedups on the whole row.

**My read:** correct and cheap. The alternative — making repair wait for a quiet feed — buys nothing and adds a coordination surface.

### 3.3 The flush timer is a real timer, not an elapsed-time check in `ApplyTrades`

D2's time trigger exists for the quiet-hour case. A quiet hour is precisely when no batch arrives to run a per-batch elapsed check, so folding the time trigger into `ApplyTrades` would have disarmed it exactly when it matters. `System.Threading.Timer` in the feed — host-agnostic, so the Linux port drives it unchanged. Period is re-read each tick so `flush_seconds` hot-reloads like the rest of the block.

### 3.4 Gap repair starts independently of transport

§5 says `transport="rest"` ⇒ no stream ⇒ repair alone carries it. So `_tradeStoreRepair` is constructed and started in `InitMarketDataSources` **outside** the `wantWs` branch. Worth a glance because it is the one place the build does something the spec states in prose but does not draw.

---

## 4. Feedback on the spec's own assumptions

**§4's "Reversibility: `enabled:false` ⇒ byte-identical to pre-build" is true but slightly under-specified, and A48f pins the stronger reading.** `enabled` gates the streaming fold; `gap_repair_enabled` gates repair *within* an enabled block. So there are two off-switches with an asymmetry — `enabled:false` disables both, `gap_repair_enabled:false` disables only repair. The fixture asserts both independently. Nothing to change; recording it so the asymmetry is not rediscovered as a bug.

**§5's "Two boxes both capturing — cannot arise" is right about topology but the code does not enforce it.** D1 ruled AWS-only, and `enabled` ships `true`, so a local dev run of the app *will* capture into its own `<exe>\backtest_data\`. That is what §5's second sentence anticipates ("its store is its own and pools at read time"), and it is harmless — but it means a local debug session quietly writes trade files, and the exe-relative resolution (D3) is what keeps those out of the repo's analysis store. Worth knowing before someone finds capture files under `bin\Debug\` and reports it.

**§0's framing held up under implementation, with one addition worth carrying.** The proposal argues the app is the right host because it "already receives every trade". Building it surfaced a second reason the spec does not state: the app is also the only place that *knows when it reconnected*, which is what makes the monotonic guard cheap. An external capturer would have to infer duplicate windows from the data; the feed just knows.

---

## 5. What I did not verify

- **No live run.** The app builds and the harness passes, but nothing in this build has been exercised against a real WS stream — no trade has actually gone through `ApplyTrades` into a file. The first flush, the first month rollover on real data, and the first gap-repair HTTP call are all unverified. **That is the trader's test gate**, and it is the one that matters here.
- **No network call in any fixture.** By design: `HistoricalStore` is unlinked in the harness. So `BackfillTradeMonthAsync`'s paging loop, its interaction with `AppendRows`, and the actual Deribit refusal behaviour past ~24 h are all untested by A48. A48d tests the *decision* that guards them, not the fetch.
- **Concurrency is reasoned, not stress-tested.** The append lock's correctness under simultaneous flush + backfill is argued (§3.2) and structurally simple, but no fixture drives both paths concurrently.
- **Disk growth unobserved.** §9 projects ~2.4 MB/day / ~900 MB/year from store measurements; I did not re-derive it.
- **The AWS deployment.** Everything here is local. Whether the deployed engine's exe directory is writable, and whether `<exe>\backtest_data\` lands somewhere the copy-back can reach, is unverified from here.

---

## 6. Rider for the trader — carried from §7.1

`aws-collector-deploy-checklist.md` §3 prescribes a ~30-second daily RDP glance. Post-build that check is **the only thing standing between an unnoticed app death and permanently lost tape**, because under D1 there is no second box capturing. Adding the store's newest-file mtime to that glance turns a liveness check into a data-integrity check for roughly zero extra effort.
