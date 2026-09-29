# Audit: tools/ops/SwingFallbackRead + tools/WsTradeProbe

**Date:** 2026-09-24 (UTC)
**Scope requested:** `tools/ops/SwingFallbackRead/*.vb`, `tools/WsTradeProbe/*.vb`
**Method:** Static reading of the shipped source, full files, no partial reads. No build, no run — this environment has no `aws_fetch`/`backtest_data` fixtures and no live network path to Deribit, so nothing here was executed. Every claim below is a citation of the code as written, not an observed runtime result.
**Coverage:** All 9 `.vb` files in scope were read in full and reviewed. None skipped.
- `tools/ops/SwingFallbackRead/SwingFallbackRead.vb`
- `tools/ops/SwingFallbackRead/MediumTierDiagnosisExport.vb`
- `tools/ops/SwingFallbackRead/MediumTierRescore.vb`
- `tools/ops/SwingFallbackRead/PocGateDefect.vb`
- `tools/ops/SwingFallbackRead/TierDemotionCensus.vb`
- `tools/ops/SwingFallbackRead/TierOrderStability.vb`
- `tools/ops/SwingFallbackRead/LiquidationFlagCheck.vb`
- `tools/WsTradeProbe/WsTradeProbeProgram.vb`
- `tools/WsTradeProbe/LiqFlagProbe.vb`

Context assumed per the brief: BTC-PERPETUAL (inverse), maker 1.5 / taker 3.5 bps. These are one-off research readers whose outputs have justified trader rulings (MEDIUM-tier diagnosis, swing-vs-fallback reads); a wrong number here can ship as a live change later.

---

## --- LOGIC TRACE ---

Take a single flush-hour row: 2026-09-11 14:23:07 UTC, STRONG SHORT, during a violent BTC-PERPETUAL move. Assume the box's `analysis_log.csv` hit a rotation trigger sometime between the pooled book's cutoff (2026-09-09) and this fetch's capture (2026-09-13), so this row landed in `analysis_log.csv.v0.7.bak`, not in `analysis_log.csv` and not yet folded into the pooled book.

- `SwingFallbackReadProgram.RunAsync` builds `merged` from `pooled.Concat(live)` only (`SwingFallbackRead.vb:268`). The row is in neither. It is never added to `merged`, never becomes a `Sig`, and never touches `nLoaded` — it doesn't even reach the population funnel's exclusion buckets (nNonDir, nPreV51, nConcurrent, …). It is arithmetically invisible.
- Meanwhile `boxLog` — used only for §1's eval-cache join diagnostics — is built from `bak.Concat(live)` (`SwingFallbackRead.vb:260-263`), so it *does* contain the row. The join cross-check loop (`For Each s In sigs`, `SwingFallbackRead.vb:678`) never finds a matching `Sig` for it, so it silently drops out of `popCollector`/`popJoined` too, even though `boxLog` "knows" about it.
- `--mode pocgate`, `--mode rescore`, `--mode census` all iterate the same `merged` dictionary passed down from `RunAsync` (`PocGateDefect.vb:111`, `MediumTierRescore.vb:159`, `TierDemotionCensus.vb:135`). The row is absent from every one of them — it never gets VPFR-recomputed, never re-scored, never counted in the tier-demotion census, and none of their funnel tables show a line for it. `--mode diagexport` (`MediumTierDiagnosisExport.vb`) walks `sigs`, so it inherits the same gap silently.
- `--mode liqflag`, §3 ("Collector rows whose trade window holds a liquidation") loads `collector` from `bakPath` + `livePath` directly (`LiquidationFlagCheck.vb:160-161`) — the row **is** present here, and if its 500-trade window holds a liquidation it will appear in the §3 hit table with its logged `LiqSignal`. But §5 ("Logged LiqSignal") re-merges from `pooledPath` + `livePath` only (`LiquidationFlagCheck.vb:209`) — the row is **absent** from that tally.

So one row, one flush hour, produces four different truths inside a single generated report: absent from the default population and every scoring/tier diagnostic, absent from the eval-cache cross-check, present in liqflag §3, absent from liqflag §5 — with no STOP, no warning, and no funnel line anywhere that says "N rows dropped because they exist only in the .bak file." A reader trusts the funnel tables precisely because every other exclusion category is enumerated; this is the one silent one.

---

## Findings

### SEVERITY: CRITICAL

**LOCATION:** `SwingFallbackRead.vb:252-277` (`RunAsync`, the `merged` population build); inherited by `PocGateDefect.vb:109-119`, `MediumTierRescore.vb:157-169`, `TierDemotionCensus.vb:131-191`, `MediumTierDiagnosisExport.vb` (via `sigs`), and indirectly `TierOrderStability.vb` (consumes `sigs`).

**DOWNSTREAM IMPACT:** Every published statistic from this instrument — success rates, net EV, the MEDIUM-tier re-score match rate, the tier-demotion census, the POC-gate defect share — is computed over a population that silently excludes any row living only in the current fetch's `.bak` rotation file. `collectorStart` and `collectorIds` are correctly derived from `bak.Concat(live)` and used to *filter out* other data (concurrent-instance rows), but the bak rows they were computed from are never themselves admitted to the population they're gating. The window `[collectorStart, first live-log timestamp)` can be entirely unpopulated by anything except stale `pooled` data, which — since `pooled` was captured before this fetch — has no coverage there either. Depending on how long the box ran before rotating, this isn't necessarily one row; it can be a multi-hour-to-multi-day blackout with no visible trace.

**FAILURE SCENARIO:** A trader ruling ("MEDIUM tier is inert" / "the swing target beats the ATR fallback in LONDON") is made from a §6-§15 table whose `n` and net-EV are quietly short by exactly the population that includes the box's rotation window — which, if rotation correlates with high message volume (a busy/volatile stretch is a plausible rotation trigger if size-based), is disproportionately likely to contain the highest-variance signals. The bias direction is not analyzable from the output because the excluded rows are never enumerated.

**ANALYTICAL CRITIQUE:** The comment on `nLoaded`'s funnel row literally reads `"Loaded (pooled book + box live log)"` — the omission of `bak` is stated as if it were the intended contract, not flagged as a gap. That's the tell that this was written by someone who forgot `bak` exists for the merge step, having already handled it correctly three lines above for `collectorIds`/`boxLog`. A funnel table that accounts for every excluded row *except* the one class it structurally cannot see is worse than no funnel at all — it manufactures false confidence that "Population" is the true eligible set.

---

### SEVERITY: HIGH

**LOCATION:** `LiquidationFlagCheck.vb:208-211` (§5, `merged` built from `pooledPath.Concat(livePath)`) vs. `LiquidationFlagCheck.vb:159-161` (§3, `collector` built from `bakPath` + `livePath`).

**DOWNSTREAM IMPACT:** This is the same defect class as above, independently reintroduced in a second file, and it creates an internal contradiction inside one output: §3's "collector rows whose window holds a liquidation" and §5's "Logged LiqSignal" tallies are drawn from two different row sets for what should be the identical underlying population from the v51 edge forward. A reviewer skimming both tables and seeing consistent-looking percentages has no way to know they were computed over different denominators.

**FAILURE SCENARIO:** Someone cites §5's "NONE: X of Y population rows" as corroborating §3's "hits.Count of inSpan rows logged LiqSignal NONE" as two independent confirmations of finding L-1 — they are not independent; they disagree on N for a documented, reconstructible reason, and neither total includes the flush-hour rows sitting in `bak`.

**ANALYTICAL CRITIQUE:** Given the file's own header explicitly reasons about "the collector's trade store copy" and "the collector's logs (.bak + live)" as its evidence base, the choice to re-derive `merged` from pooled+live in §5 rather than reusing `boxLog`-style bak+live loading looks like copy-paste from the main program's (already-buggy) merge pattern rather than a deliberate scope decision.

---

### SEVERITY: MEDIUM

**LOCATION:** `Walk()`, `SwingFallbackRead.vb:775-807`.

**DOWNSTREAM IMPACT:** A missing 1-minute bar inside a walk is silently skipped (`w.MissingBars += 1`, loop continues) rather than treated as a resolution-uncertain state. If the true target or stop touch occurred inside that missing bar, the walk proceeds past it undetected and can resolve later (at a worse/different price) or time out to OPEN, marking to the last *available* close instead. The aggregate "rows with at least one missing bar" count is reported once in §4, but no individual table cell excludes, weights, or footnotes the specific rows affected — a session×tier×cap cell's win rate and net EV can be silently distorted by exactly the trades whose true outcome is least certain.

**FAILURE SCENARIO:** Public 1-minute candle coverage from `DeribitOhlcFetcher`/`DeribitClient.GetCandlesAsync` is most likely to have gaps precisely during high-volatility flush conditions (API backpressure, aggregation lag) — the same conditions that produce the largest, most decision-relevant price excursions. A stop that was actually hit inside a data gap gets scored as a timeout mark instead of a full stop-loss, understating the tail risk of exactly the tier/session cell a trader is deciding whether to keep live.

**ANALYTICAL CRITIQUE:** This is a defensible best-effort design given the data source, and it is disclosed in aggregate — but "disclosed in aggregate, unaccounted for per-cell" is not the same as "safe to build a D-table on." A hard rule elsewhere in this repo (fixture-literal provenance, display-parity) insists on precision for far lower-stakes drift; a walk that can silently reclassify STOP→OPEN under exactly the conditions correlated with real tail events deserves the same discipline (e.g., a per-cell "rows with missing bars in this cell" column, or excluding affected rows from headline win-rate/EV cells with a stated count).

---

### SEVERITY: MEDIUM

**LOCATION:** `LfKey`, `LiquidationFlagCheck.vb:245-247`; consumed by the dedup/`byKey` construction at `LiquidationFlagCheck.vb:105-125` and the collapsed `tape` at `LiquidationFlagCheck.vb:146-156`.

**DOWNSTREAM IMPACT:** The key is `(Timestamp_ms, Price, Amount, Direction)` — no trade_id, by design (the doc comment explains the streamed/REST twins can carry different trade_ids). But this means two genuinely distinct trades sharing a millisecond, price, size and direction collapse into one tape entry. That's exactly the fill pattern a liquidation cascade produces: an engine matching a large liquidated position against several counterparties at the same price, batched into the same 100ms aggregation window.

**FAILURE SCENARIO:** During a flush, a real pair of distinct liquidation fills at identical (ts, price, amount, direction) get merged into one `byKey` bucket. If one carries a flag and the other doesn't, §2's "copies of one liquidation trade" (L-1 evidence) reports a "twin" relationship that is actually two unrelated trades — inflating apparent confirmation of the double-write theory precisely in the busiest, most liquidation-dense minutes, where the true tape may have had a fill that was never streamed at all (a genuine miss, not a duplicate).

**ANALYTICAL CRITIQUE:** The file separately checks `idComparable`/`sameId` using trade_id specifically to sanity-check this ambiguity — which shows the author was aware trade_id existed and mattered — but that check only fires when both copies happen to carry a trade_id, and the primary grouping (`byKey`) is still keyed without it. The collision risk is not evaluated or bounded anywhere in the output (no "count of tuple keys with >2 members" diagnostic), so its magnitude is unmeasured even though the instrument's whole purpose is measuring liquidation-flag reliability.

---

### SEVERITY: LOW (methodological)

**LOCATION:** `LiquidationFlagCheck.vb:162-174` (the 500-trade window over the deduped `tape`) vs. `WsTradeProbeProgram`'s own G3 replay of `TradeStoreWriter.vb:149`'s same-millisecond drop guard (documented in `WsTradeProbeProgram.vb:1-30` as measuring ~50% raw-trade loss).

**DOWNSTREAM IMPACT:** The "last 500 trades" window this tool uses to test whether a collector row's window "holds a liquidation" is built from the content-deduplicated tape — a third definition of "the window," distinct from both the raw venue stream and whatever the shipped `TradeStoreWriter` ring buffer actually held after its own (separately documented, ~50%) same-millisecond drop. §3's counts are therefore evidence about a synthetic window, not a direct replay of what `CalcLiquidations` actually saw on the live run.

**FAILURE SCENARIO:** If someone treats §3's "hits.Count" as "the number of times the shipped engine's window contained a liquidation," they are overstating certainty — the true production window, after the guard's own drop behavior, spans a different and generally *longer* wall-clock range (since roughly half the raw trades that would have advanced the window are themselves dropped), which could pull in liquidations §3's window definition misses, or vice versa.

**ANALYTICAL CRITIQUE:** This is a scope note, not a defect — the file never claims byte-identical replay — but given this repo's own hard rule that build-time evidence a reader can't re-run must be labeled `E-n` and never headline a review, a window definition that matches neither the venue nor the shipped guard deserves the same explicit caveat in the output text, not just in a private mental model.
