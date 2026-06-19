# WebSocket Migration — P1 Implementer Hand-Off (foundation, additive-only)

**Parent spec:** `docs/websocket-migration-proposal.md` (APPROVED 2026-06-12). This hand-off is the *how* for **P1 only** (§8 of the proposal) + the coordinator reconciliation of the proposal against the current engine (it was written at v33; we are at **v37 → bumping to v38**).
**Seat:** fresh Opus implementer. **Routing:** spec-back → coordinator review (re-run builds + harness, audit the diff, standalone WS soak) → local commit. **Local-first — NEVER push; the trader tests + pushes.**
**Risk posture:** P1 is **additive-only** — new root files + the `network` settings keys, nothing else. The live verdict path is **not touched**, so P1 cannot regress the app or the in-flight (B)/tweaker/hold-window data collection. The WS feed is built and soak-tested **standalone**; it is dormant (unreferenced) in the live app until P2 wires it in.

---

## 0. Scope — what P1 is and is NOT

**P1 builds the foundation, dormant:** `DeribitWsFeed` + `MarketState` + `IMarketDataSource` + `RestMarketDataSource` + `WsMarketDataSource` + the 6 `network.*` settings keys. The WS feed connects, subscribes, heartbeats, reconnects, and populates `MarketState` — verified by a **throwaway standalone soak** (§9), not by wiring into `RunAnalysisAsync`.

**P1 does NOT:** touch `RunAnalysisAsync` or any consumer; change any indicator/scoring/norms/logging; flip transport; change the CSV. The app runs **pure REST, byte-identical to today.** Consumer routing (`_marketSource` swap) is **P2**; cutover is **P3** (and P3 stays gated behind the data-gated re-baselines, per proposal §8).

This honors the proposal's explicit P1 line: *"No consumer changes; app still runs pure REST."*

---

## 1. Reconciliation vs the approved proposal (read this first — 3 deltas since v33)

1. **Settings version: v33 → v38.** The `network` block never changed across v33–v37 (still the 3 REST keys at `settings.json:402` / `EngineSettings.vb:695`), so the 6 `ws_*` keys add cleanly with zero collision. Bump `version` to **38**, prepend a `change_log` entry, add a §15 row (coordinator adds §15 at commit).
2. **The 3-min execution stream (the real delta — proposal predates v36).** The proposal subscribes to chart `1 / 5 / 15`. But v36 made execution resolution session-conditional: Asia/London run on **3-min** (`MainForm_Analysis.vb:98` fetches `GetCandlesAsync(execRes.ToString(), 250)` with `execRes ∈ {1,3}`). So `MarketState` must hold a **3-min series** and `DeribitWsFeed` must subscribe to **`chart.trades.BTC-PERPETUAL.3`** as well. Subscriptions become **1 / 3 / 5 / 15**. (Trade-stream indicators — CVD `slope_min_usd`, MicroCVD `accel_threshold` — read the fixed 500/50-trade stream and are resolution-independent per v36, so the single ascending trade buffer serves them unchanged.)
3. **Funding field = `funding_8h`, NOT `current_funding`.** Proposal §2 says "`current_funding` → funding rate," but the REST path deliberately uses **`funding_8h`** (the time-invariant 8h-projected settlement rate — `DeribitClient.vb:190-201`). To stay semantics-neutral the WS source **must** serve `funding_8h` from the ticker payload. `current_funding` is a different quantity and would silently break parity. **Verify at implementation:** confirm the `ticker.BTC-PERPETUAL.*` payload carries `funding_8h` (Deribit ticker typically exposes both `current_funding` and `funding_8h`); if it only carries `current_funding`, STOP and flag — that is a semantics mismatch, not an implementation detail.

---

## 2. Files to add (all root-level, host-agnostic — zero `System.Windows.Forms`, no `MainForm`/`Control.Invoke`)

The root `.vbproj` globs `**/*.vb`, so new root files auto-compile. They are **public but unreferenced** in P1 (dormant) — VB allows this, build stays 0/0.

| New file | Role |
|---|---|
| `IMarketDataSource.vb` | Interface mirroring the 5 live call shapes (§3). |
| `RestMarketDataSource.vb` | Thin pass-through to the existing `DeribitClient` (the fallback path). `DeribitClient` stays **untouched**. |
| `MarketState.vb` | Thread-safe latest-snapshot store (§4). |
| `DeribitWsFeed.vb` | One WS connection + receive loop + heartbeat + reconnect + subscriptions + REST seeding (§5). |
| `WsMarketDataSource.vb` | Serves the 5 shapes from `MarketState` (§4.3). |

---

## 3. `IMarketDataSource` — the exact contract (mirror `DeribitClient` signatures verbatim)

```vb
Public Interface IMarketDataSource
    Function GetCandlesAsync(resolution As String, count As Integer) As Task(Of List(Of Candle))
    Function GetFundingRateAsync() As Task(Of Double?)
    Function GetBookSummaryAsync() As Task(Of (OI As Double, MarkPrice As Double)?)
    Function GetOrderBookAsync(depth As Integer) As Task(Of OrderBookSnapshot)
    Function GetRecentTradesAsync(count As Integer) As Task(Of List(Of TradeRecord))
End Interface
```

- Return types/nullability are **identical** to `DeribitClient`'s — `Nothing`/`Nothing?` means "unavailable," which the existing skip-gate (`MainForm_Analysis.vb:101-109`) already handles. Reuse the existing `Candle` / `OrderBookSnapshot` / `TradeRecord` types (defined in `DeribitClient.vb:294/304/309` — `Candle{Timestamp(Long),Open,High,Low,Close,Volume(BTC),VolumeUSD}`; `OrderBookSnapshot{Bids/Asks As List(Of (Price,Size))}`; `TradeRecord{Price,Amount,Direction(String),Liquidation(String),Timestamp(Long)}`). Do not redefine them.
- **Only the count-based `GetCandlesAsync(resolution, count)` overload is in the interface** (the live indicator path). The **time-range overload `GetCandlesAsync(resolution, startMs, endMs)`** (`DeribitClient.vb:138`, used by `DeribitOhlcFetcher` for historical bulk fetch) is **NOT** in the interface and stays a direct `DeribitClient` call — WS only streams forward, so all backfill/seeding stays REST (this also covers `OhlcCache` gap-fill and `LivePerformanceTracker.FetchGapChunked`).

`RestMarketDataSource` implements each method as `Return DeribitClient.GetXxxAsync(...)` (pass-through). It is the verified-identical fallback.

---

## 4. `MarketState` — thread-safe snapshot store

### 4.1 Holdings (each with a `LastUpdateUtc As DateTime`)
- **4 rolling candle series**, keyed by resolution string: `"1"` (cap 250), `"3"` (cap 250), `"5"` (cap 210), `"15"` (cap 70). Caps match the live fetch counts (`MainForm_Analysis.vb:63/64/72/98`). Current (forming) bar updates in place; a new bar on roll appends and trims to cap.
- **Trade ring buffer**, cap **5,000**, **chronological ascending** (oldest first, newest last — the F1 contract; matches `GetRecentTradesAsync`). Append on each trade message; trim oldest beyond cap.
- **Top-of-book ladder**: latest `OrderBookSnapshot` (top-10 bids/asks).
- **Ticker fields**: `Funding8h`, `OpenInterest`, `MarkPrice` (plus `IndexPrice` if cheap — unused by current consumers, fine to store).

### 4.2 Concurrency
Single-writer (the receive loop), multi-reader (future analysis runs). Use a lock around mutation + read, OR swap immutable snapshots (copy-on-write). Either is fine; keep it simple and correct. No `Control.Invoke`, no UI types.

### 4.3 `WsMarketDataSource` getters (serve from `MarketState`)
- `GetCandlesAsync(res, count)` → last `count` of the `res` series (or all if fewer). **No staleness gate here** — candle freshness is the consumer's existing `IndicatorEngine.IsFresh` (D5) job; don't double-gate. Return `Nothing` only if the series is empty (never seeded).
- `GetRecentTradesAsync(count)` → last `count` of the ascending buffer.
- `GetOrderBookAsync(depth)` → latest ladder, top-`depth`.
- `GetFundingRateAsync()` → `Funding8h` (Double?).
- `GetBookSummaryAsync()` → `(OpenInterest, MarkPrice)`.
- **Staleness (book/trades/ticker only):** if `nowUtc − stream.LastUpdateUtc > ws_stale_after_sec`, the corresponding getter returns `Nothing` → consumer skip-gate/fallback handles it exactly like a REST failure. (Candle series defer to `IsFresh`, above.)

---

## 5. `DeribitWsFeed`

- One `ClientWebSocket` to `network.ws_url` (`wss://www.deribit.com/ws/api/v2`), **public channels only, no auth**. `System.Net.WebSockets` is in .NET 8 (no package).
- **Receive loop** on a background `Task` (not the UI thread). Parse JSON-RPC frames; route `subscription` notifications to the matching `MarketState` updater.
- **Subscriptions** (public/subscribe; **verify channel names + payload field names against current Deribit API docs at implementation — API-drift guard**):
  - `book.BTC-PERPETUAL.none.10.100ms` → ladder (depth-limited snapshot channel; each msg is a full top-10 ladder — no change-application/checksum logic).
  - `trades.BTC-PERPETUAL.100ms` → append to buffer (map price/amount/direction/timestamp/liquidation 1:1 to `TradeRecord`).
  - `ticker.BTC-PERPETUAL.100ms` → `funding_8h` (§1.3), `open_interest`, `mark_price`.
  - `chart.trades.BTC-PERPETUAL.1` / `.3` / `.5` / `.15` → OHLCV per series (forming bar in place; roll appends). **Confirm roll/tick semantics produce series identical to the REST snapshots** (shadow parity in P2 is the real proof; note it as a P2 watch).
- **Heartbeat (the #1 foot-gun — hard requirement):** `public/set_heartbeat` interval `ws_heartbeat_sec` (30); the loop **must** reply to every `test_request` with `public/test`, or Deribit drops the connection.
- **Seeding (startup + every reconnect):** before/just-after subscribing, one REST burst seeds full windows so the first reads are complete — all 4 candle series via `DeribitClient.GetCandlesAsync(res, count)` (1/3/5/15), and the trade buffer via `GetRecentTradesAsync(500)` (already ascending). WS streams forward from there.
- **Reconnect:** receive-loop failure/close → exponential backoff 1s→2s→4s→…cap 60s, resubscribe all, re-seed, resume. Storm guard: >5 reconnects / 10 min → hold the feed down for `ws_cooldown_sec` (the fallback carries runs; relevant from P2 on).
- **Lifecycle:** expose `StartAsync()` / `Stop()`. In P1 nothing calls them in the live app (dormant) — only the §9 soak does. Reads settings once at start (transport change needs restart; hot-swap is out of scope).

---

## 6. Settings (v38)

Add to the `network` block (append; keep the 3 REST keys):
```
network.transport            "rest"   ' "rest" | "ws" — cutover flag; P3 flips default. Stays "rest" in P1/P2.
network.ws_url               "wss://www.deribit.com/ws/api/v2"
network.ws_heartbeat_sec     30
network.ws_stale_after_sec   10
network.ws_cooldown_sec      300
network.ws_fallback_to_rest  true
```
POCO: add the 6 fields to `NetworkSettings` (`EngineSettings.vb:695`) with `<JsonPropertyName(...)>` + these defaults. Bump `settings.json` `version` → **38**, prepend a `change_log` entry (ref this hand-off). **Auto-tweaker surface: excluded** — transport plumbing, not scoring (same class as display keys; add `network.transport`/`ws_*` to the PromptBuilder exclusion note if the other `network.*` keys aren't already blanket-excluded — verify).

---

## 7. REST ↔ WS field-mapping table (the parity contract)

| Consumer getter | REST source (today) | WS source (`MarketState`) | Watch |
|---|---|---|---|
| `GetCandlesAsync(r,n)` | `get_tradingview_chart_data` | `chart.trades.…{r}` series tail | bar roll/tick semantics; 4 resolutions incl. **3** |
| `GetRecentTradesAsync(500)` | `get_last_trades_by_instrument` (desc→reversed) | ascending buffer tail | ascending order preserved |
| `GetOrderBookAsync(10)` | `get_order_book?depth=10` | `book.…none.10.100ms` ladder | top-10 only |
| `GetFundingRateAsync()` | ticker **`funding_8h`** | ticker **`funding_8h`** | **NOT `current_funding`** (§1.3) |
| `GetBookSummaryAsync()` | `get_book_summary` `open_interest`/`mark_price` | ticker `open_interest`/`mark_price` | confirm ticker OI == book-summary OI |

---

## 8. Out of scope for P1 (do not build here)

Consumer routing (`_marketSource` into `RunAnalysisAsync`) → **P2**. Shadow-parity comparison + status-bar surface + reconnect/fallback drills → **P2**. Cutover (`transport` default → ws) + 15m-TTL collapse on the WS path → **P3** (gated on the data-gated re-baselines). Any indicator/cadence/aggregation change → P4+ separate specs. Backfill/historical (`OhlcCache`, `LivePerformanceTracker`, the time-range candle overload) stays REST — never routed through `IMarketDataSource`.

---

## 9. Acceptance

- **Build 0/0:** solution + `tools/AutoTweaker` + `verify/ordercheck`. The new files compile (dormant). No existing file changed except `settings.json` + `EngineSettings.vb` — **prove it with `git diff --name-only`** (expect only those two modified; everything else new/untracked). This is the additive-only guarantee.
- **Harness unregressed:** `OrderCheck` A1–A15g all pass (this change touches no scoring path — it's a sanity check, not a new fixture).
- **Standalone WS soak (throwaway — needs live network; remove after, per [[delete-test-screenshots]] hygiene):** a temporary entry (dev `Sub Main` branch or a one-off console) that `New DeribitWsFeed(...)` + `StartAsync()`, runs ~3–5 min, and asserts/logs: connection opens; all 4 chart + trades + book + ticker channels subscribed; **heartbeats answered, zero unintended drops**; `MarketState` populates (4 series non-empty after seed, trade buffer growing, ladder + ticker present, `funding_8h` non-Nothing); and a **forced-reconnect drill** (kill network ~60s → auto-recovery → state resumes). Capture the console evidence in the spec-back; delete the throwaway. (The 24h soak + the full shadow-parity ≥50-run gate are **P2** — don't attempt them here.)
- **Spec-back** (`docs/websocket-migration-p1-spec-back.md`): what was built, the §1.3 funding-field verification result, the §2 channel-name/payload verifications (API-drift), any deviations, the soak evidence. Routes to coordinator.

## 10. Commit checklist
1. 5 new root `.vb` files (§2).
2. `settings.json` — 6 `network.*` keys + `version` 38 + `change_log`.
3. `EngineSettings.vb` — 6 `NetworkSettings` POCO fields.
4. PromptBuilder exclusion note for `network.transport`/`ws_*` if needed (§6).
5. (coordinator at commit) `DeribitIndicatorProject.md` §15 row + §6 version pointer → v38; `architecture.md` directory-layout note for the 5 new files.

> Author the P1 build in a fresh Opus conversation against this hand-off + the parent proposal. Verify every channel name/payload against the **current** Deribit API docs as you wire each subscription — the proposal is a year-stale on API specifics by design (API-drift guard, proposal §2). Local commits as you go; the trader tests + pushes.
