# WebSocket Migration — Proposal (v1: semantics-neutral cutover)

**Date:** 2026-06-11
**Author:** Fable 5 (spec-author seat)
**Status:** **APPROVED 2026-06-12** — user confirmed go, with the expectation of amendments during testing (additions land as edits to this doc + change_log notes, not as silent scope growth). §11 added at approval: the post-migration feature catalogue.
**Implementer:** Opus, fresh conversation per phase (§8). Designed to be implemented past the Fable window — every judgement call is made in this spec; remaining unknowns are explicitly marked as implementation-time verification steps.
**Settings:** ~~v33~~ → **v38** at implementation (new `network` keys, §6). **No CSV schema change.**

> **Coordinator reconciliation 2026-06-18 (P1 hand-off written: `websocket-migration-p1-implementer-handoff.md`).** Proposal predates v33→v37; three deltas folded into the P1 hand-off, none changing the approved design: (1) settings v33 → **v38** (the `network` block never churned; the 6 `ws_*` keys add cleanly); (2) **v36 3-min execution resolution** — subscriptions add **`chart.trades.…3`** + a 3-min `MarketState` series (the engine now runs Asia/London on 3-min), so §2 streams `1/3/5/15` not `1/5/15`; (3) funding maps **`funding_8h`** (what the REST path uses), **not** `current_funding` as §2 says — a semantics-neutrality fix. P1 is scoped **additive-only** (new files + settings; `RunAnalysisAsync` untouched) so it can't regress the live app or the in-flight data collection; consumer routing is P2, cutover P3 (still gated on the data-gated re-baselines).

---

## 0. Goal and the one design rule

Replace REST polling with WebSocket market data **without changing what any indicator sees**. The cutover is *semantics-neutral by construction*: same run cadence, same data shapes, same windows, same contracts — so the calibration built on REST-collected clean data carries across unchanged, and the migration is **not** a recalibration event.

Everything that makes WS *better* than REST — sub-minute cadence, time-averaged OFI, absorption/liquidation-stream indicators, event-driven scoring — is **explicitly out of scope for v1** (§9). Each lands later as its own spec with its own re-baseline flag. v1 buys: entry-moment data freshness at unchanged cadence, elimination of per-run REST burst (7 HTTP calls/run), the platform for the later upgrades, and removal of the rate-limit ceiling on cadence.

Architectural pre-payment (verified): the v18 resilience pass froze the `GetXxxAsync` call-site contracts precisely so the transport could swap without touching indicators or scoring. F1 (2026-06-11) added the chronological-ascending trade contract. This spec swaps the transport behind those contracts.

## 1. Architecture

```
                    ┌─ RestMarketDataSource ── DeribitClient (existing, unchanged)
RunAnalysisAsync ───┤                                              (fallback path)
  (cadence: auto-   └─ WsMarketDataSource ──── MarketState ◄── DeribitWsFeed
   run timer,                                  (snapshot store)    (one WS connection,
   UNCHANGED)                                                       background receive loop)
```

- **`DeribitWsFeed`** (new, root, host-agnostic — zero WinForms): owns one connection to `wss://www.deribit.com/ws/api/v2` (public channels only, no auth), a `ClientWebSocket` receive loop on a background task, heartbeat handling, reconnect with backoff, and subscription management.
- **`MarketState`** (new, host-agnostic): thread-safe latest-snapshot store — top-of-book ladder, rolling trade buffer (cap 5,000, **chronological ascending**, matching the F1 contract), three rolling candle series (1m×250 / 5m×210 / 15m×70), ticker fields (funding, mark, index, **open interest**), each with `LastUpdateUtc`. Updates swap immutable snapshots or copy-on-read under a lock — single-writer (receive loop), multi-reader (analysis runs).
- **`IMarketDataSource`** (new interface): mirrors today's seven call shapes exactly — `GetCandlesAsync(res, count)`, `GetFundingRateAsync()`, `GetBookSummaryAsync()`, `GetOrderBookAsync(depth)`, `GetRecentTradesAsync(count)` — same return types, same nullability semantics (`Nothing` = unavailable → existing skip-gate handles it).
  - `RestMarketDataSource`: thin pass-through to the existing `DeribitClient` (which stays untouched, as the fallback).
  - `WsMarketDataSource`: serves the same shapes from `MarketState` — trades = last N of the buffer (already ascending), candles = the rolling series tail, order book = latest ladder snapshot, funding/OI = latest ticker fields. Returns `Nothing` when the relevant stream is stale (§4), so a WS outage degrades exactly like a REST failure.
- **`RunAnalysisAsync`** swaps the source by config (`network.transport`). The 15m TTL cache logic collapses on the WS path (the stream keeps it current; the cache shape stays for the REST fallback). No indicator, scoring, norms, or logging code changes.

## 2. Subscriptions (verify channel names/payloads against current Deribit API docs at implementation time — API-drift guard)

| Channel | Replaces | Notes |
|---|---|---|
| `book.BTC-PERPETUAL.none.10.100ms` | `GetOrderBookAsync(10)` | **Depth-limited snapshot channel deliberately chosen over the raw change feed** — each message is a complete top-10 ladder, eliminating change-application + checksum-resync complexity entirely. OFI/spread consume top-10 only (OFI BookDepth=5). |
| `trades.BTC-PERPETUAL.100ms` | `GetRecentTradesAsync(500)` | Append to the ring buffer; fields map 1:1 (price, amount, direction, timestamp, liquidation). Buffer seeded at startup via one REST call so the first run has full windows. |
| `ticker.BTC-PERPETUAL.100ms` | `GetFundingRateAsync()` + `GetBookSummaryAsync()` | `current_funding` → funding rate; `open_interest` → OI. Verify field-name mapping vs the REST book-summary values on the same instrument during shadow parity (§7). |
| `chart.trades.BTC-PERPETUAL.1` / `.5` / `.15` | `GetCandlesAsync(...)` ×3 | Streaming OHLCV bar updates (current bar updates in place; new message at bar roll). Series **seeded at startup via the existing REST chart fetch** (WS only streams forward), then maintained from the stream. Bar-boundary semantics are an implementation-time verification item: confirm the channel's tick/roll behaviour produces series identical to REST snapshots (shadow parity will catch drift). |

Heartbeats: `public/set_heartbeat` (interval 30s); the client **must** answer `test_request` messages or Deribit drops the connection. This is the #1 known foot-gun — spec'd as a hard acceptance item.

## 3. Reconnect / resilience

- Receive-loop failure or socket close → reconnect with exponential backoff (1s → 2s → 4s → … cap 60s), resubscribe all channels, re-seed the trade buffer and candle series via REST (one burst), resume.
- Connection storms guard: if >5 reconnects in 10 minutes, hold the WS path down for `ws_cooldown_sec` and let the fallback carry runs.
- **Per-run fallback:** when `ws_fallback_to_rest = true` (default) and any required stream is stale at run time, `RunAnalysisAsync` uses `RestMarketDataSource` for that run and surfaces `WS DEGRADED — REST fallback` in the status bar (same cascade pattern as the skip counter / settings-parse warning). A WS problem therefore never costs a row — at worst the run is REST-fresh, exactly like today.

## 4. Staleness

`WsMarketDataSource` treats a stream as stale when `nowUtc − LastUpdateUtc > ws_stale_after_sec` (default 10s for book/trades/ticker; candles use the D5 `IsFresh` guard unchanged — it applies identically to both transports, one shared code path). Stale stream → that getter returns `Nothing` → skip-gate or fallback per §3.

## 5. Threading

Receive loop = one background `Task` (no `Control.Invoke`, no UI coupling — CLI-port aligned). `MarketState` is the only shared surface. Analysis runs read snapshots on demand; the existing UI-thread orchestration in `RunAnalysisAsync` is unchanged. The `SettingsLoader.Current` in-place-mutation convention noted in the audit (§3 concurrency soft spot) is unaffected — the feed reads settings once at start; transport changes require app restart (documented; hot-swap is out of scope).

## 6. Settings (v33 at implementation)

```
network.transport            "rest" | "ws"   default "rest"  — cutover flag (P3 flips default)
network.ws_url               wss://www.deribit.com/ws/api/v2
network.ws_heartbeat_sec     30
network.ws_stale_after_sec   10
network.ws_cooldown_sec      300
network.ws_fallback_to_rest  true
```

POCO additions in `NetworkSettings`; version bump + change_log; §15 entry. Auto-tweaker surface: **excluded** (transport plumbing, not scoring — same exclusion class as display keys).

## 7. Acceptance — shadow parity mode (the load-bearing gate)

Before any cutover, run with `transport=rest` while the WS feed runs alongside, and after each analysis log a field-level comparison of the two sources at that instant (console/side log, not CSV): last-candle OHLCV per resolution, last-trade id/price, top-of-book bid/ask, funding, OI.

- **Pass:** ≥50 consecutive runs with candles identical (closed bars), book/ticker within one-tick/update tolerance, trades buffer superset-consistent.
- Plus: 24h connection soak (heartbeats answered, zero unintended drops), a forced-reconnect drill (kill network 60s → auto-recovery → parity restored), and a fallback drill (`transport=ws` + feed down → runs continue via REST with the status surface showing degraded).
- The ordercheck harness is untouched (indicators don't change); parity mode is the acceptance evidence.

## 8. Phasing (each = one Opus conversation, local commits, user tests + pushes)

- **P1** — `DeribitWsFeed` + `MarketState` + `IMarketDataSource` + both source impls + seeding. No consumer changes; app still runs pure REST. Build + unit-level soak.
- **P2** — shadow parity mode + status-bar surface + the §7 drills. Runs alongside normal collection (zero dataset impact).
- **P3** — cutover: `transport` default → `"ws"`; §15 entry marks the dataset boundary date (informational — semantics are unchanged by design, but mark it anyway); 15m TTL simplification on the WS path.
- **P4+ (separate future specs, each re-baseline-flagged):** sub-minute cadence; time-averaged OFI (replaces snapshot OFI in its existing slot); liquidation-stream cascade detection; book-absorption at swing levels; event-driven scoring.

Sequencing vs everything else: P1/P2 can be implemented in parallel with clean-data collection (they don't touch the data path until P3). **P3 waits until after the ≥300-row re-baseline review** so the first recalibration closes on a single-transport dataset.

## 9. Explicitly out of scope (v1)

Cadence changes; any indicator redesign; raw (non-aggregated) channels; authenticated/private channels; order placement of any kind; CLI host (separate roadmap item — but everything new here is host-agnostic by construction); settings hot-swap of transport.

## 11. Post-migration feature catalogue (P4+ candidates — each needs its own spec; listed at user request 2026-06-12)

Profile-filtered (directional only, no double-counting, conservative bias). Ordered by expected value for a 2–15 min structural-breakout scalper. Items marked ⚠ affect scoring/logged data → standard spec + re-baseline flag; unmarked items are display/notification-only.

1. **Realtime exit guard** — when a position is declared (`posState ≠ None`), re-run the Layer-1/1.5 microstructure exit checks every ~2–5 s on streaming trades/book (recomputing MicroCVD/TFI/OFI from `MarketState`), with a status-bar alarm (optional sound) on EXIT. Your exit cues at tick freshness instead of poll freshness — the highest-value single feature WS enables for this trading style. Display/alert only; scoring pipeline untouched.
2. **On-close analysis mode** — toggle: trigger the full run exactly at each 1m bar close (event-driven) instead of on the timer; kills the 0–60 s poll lag at the moment that matters most for breakout entries.
3. **LIVE microstructure strip** — continuously updating mini-row (last price vs nearest swing/HVN level, TFI, spread, top-book imbalance, tape speed) between full runs. The "realtime analysis toggle" you suggested, scoped to the fast indicators so the verdict itself stays a deliberate, full-pipeline product.
4. ⚠ **Time-averaged OFI** — replace the single-snapshot book imbalance with a time-weighted average over the run interval. Same indicator slot, materially better signal (snapshot OFI is the noisiest input in the engine). Re-baseline of OFI thresholds required.
5. ⚠ **Aggressor velocity / tape burst** — USD-per-second aggression vs a rolling norm: the tick-resolution version of your entry-impulse confirmation. Must be specced against TFI for correlation (it may *upgrade* TFI rather than join it — profile's anti-correlation rule).
6. ⚠ **Book absorption at structural levels** — resting-size depletion at the active swing high/low without price progress: breakout-quality vs fakeout filter, directly serving structural-breakout entries.
7. **Liquidation cascade alarm** — liq-flagged trades stream in real time; banner/alert when a cascade is in progress rather than discovering it at the next poll (the scoring penalty already exists; this is timeliness).
8. **Level-approach alerts** — notify when price comes within N ticks of the active swing/HVN/POC level. Pure display.
9. **Provisional forming-bar verdict** — optional preview score computed on the open bar, visually marked PROVISIONAL (dimmed, never logged). Conservative-bias caveat: must be unmistakably distinct from the confirmed verdict.
10. **Connection-quality strip** — WS health/latency/reconnect counters (ops hygiene, ships with P2 anyway).

Sub-minute full-run cadence remains the P4 baseline item (§8). Recommended first wave after P3 cutover: 1 + 2 + 10 (zero scoring impact, immediate workflow value), then 4 as the first re-baseline-flagged upgrade.

## 10. Open items for post-Tier-D amendment

Exact channel interval choice (100ms vs raw — 100ms aggregation is sufficient at unchanged cadence and cheaper to process); whether the trade-buffer cap (5,000) should scale with future cadence work; `chart.trades` roll-tick semantics (verified in P2 shadow mode regardless).
