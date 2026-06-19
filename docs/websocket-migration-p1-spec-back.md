# WebSocket Migration — P1 Spec-Back (foundation, additive-only)

**Seat:** Opus implementer. **Date:** 2026-06-19. **Routes to:** coordinator review (re-run builds + harness, audit the diff, standalone WS soak) → local commit.
**Parent:** `docs/websocket-migration-proposal.md` (APPROVED 2026-06-12). **Hand-off:** `docs/websocket-migration-p1-implementer-handoff.md`.
**Status:** BUILT + soak-passed, local-uncommitted. **Local-first — NOT pushed.**

---

## 1. What was built (additive-only, dormant)

Five new root-level, host-agnostic `.vb` files (zero `System.Windows.Forms`, no `MainForm`/`Control.Invoke`; all diagnostics via `Console.WriteLine`):

| File | Role | Notes |
|---|---|---|
| `IMarketDataSource.vb` | The 5-shape transport contract | Count-based `GetCandlesAsync(res,count)` only; time-range overload deliberately excluded (backfill stays REST). Reuses existing `Candle`/`OrderBookSnapshot`/`TradeRecord`. |
| `RestMarketDataSource.vb` | Verified-identical fallback | Pure pass-through to the untouched `DeribitClient`. |
| `MarketState.vb` | Thread-safe snapshot store | One `SyncLock`; readers get copies. 4 series (caps 250/250/210/70), 5k ascending trade ring, top-10 ladder, ticker (`Funding8h`/`OI`/`mark`/`index`), per-stream `LastUpdate`. |
| `DeribitWsFeed.vb` | WS connection + receive/heartbeat/reconnect + REST seed | Raw `ClientWebSocket`; see §3 for the package note. |
| `WsMarketDataSource.vb` | Serves the 5 shapes from `MarketState` | Staleness gate on book/trades/ticker; candle getter defers to `IndicatorEngine.IsFresh` (returns `Nothing` only if never seeded). |

Settings (v37 → **v38**):
- `Core/Settings/EngineSettings.vb` — `NetworkSettings` POCO gains 6 fields (`Transport`, `WsUrl`, `WsHeartbeatSec`, `WsStaleAfterSec`, `WsCooldownSec`, `WsFallbackToRest`) with matching defaults.
- `settings.json` — 6 `network.*` keys appended (3 REST keys kept), `version` → 38, `change_log` entry prepended.

**The live verdict path is untouched** — `RunAnalysisAsync` and every consumer still call `DeribitClient` directly; the WS classes are public but unreferenced. The app runs pure REST, byte-identical to v37.

---

## 2. API-drift verification (against current Deribit docs, 2026-06-19)

Every channel name and payload field was verified against the live docs (the proposal is year-stale by design). **No STOP condition triggered.**

| Item | Verified | Result |
|---|---|---|
| **§1.3 funding gate** | `ticker.BTC-PERPETUAL.100ms` payload | ✅ Carries **`funding_8h`** (perpetual-only) alongside `current_funding`. Feed serves `funding_8h`; never touches `current_funding`. Soak read `f8h ≈ 0.0000072`. |
| ticker channel | name + interval | ✅ `ticker.(instrument).(interval)`, intervals `raw`/`100ms`/`agg2`. Fields confirmed: `funding_8h`, `open_interest`, `mark_price`, `index_price`. |
| chart resolution **3** | `chart.trades.(instrument).(resolution)` | ✅ Resolutions `1,3,5,10,15,30,60,120,180,360,720,1D` — `1/3/5/15` all valid. OHLCV fields: `tick`, `open`, `high`, `low`, `close`, `volume`, `cost`. |
| trades channel | name + fields | ✅ `trades.(instrument).(interval)`; fields `price`/`amount`/`direction`/`timestamp`/`liquidation`(`M`/`T`/`MT`)/`trade_id`/`instrument_name`. |
| depth-limited book | `book.(instrument).(group).(depth).(interval)` | ✅ `none`/`10`/`100ms` all valid. **Snapshot semantics** (aggregated full state per interval) — confirms "no change-application/checksum logic." `bids`/`asks` = `[price, amount]` pairs. |
| heartbeat | `public/set_heartbeat` | ✅ Interval min **10s**; server sends `test_request` → client **must** reply `public/test` or it drops the connection. Feed replies to every one. |

### Parity nuances carried into the mapping
1. **chart `volume`/`cost` → `Candle.Volume`(BTC)/`VolumeUSD`** mapped exactly as `DeribitClient` maps the REST `get_tradingview_chart_data` response, including the `cost`-absent fallback `Volume * Close` (`DeribitClient.vb:122-126`).
2. **`liquidation` absent on normal trades → `"none"`** — matches `DeribitClient.vb:281` verbatim (not `""`, not `Nothing`).
3. **`GetBookSummaryAsync` from ticker** — REST uses `get_book_summary`; WS reads ticker `open_interest`/`mark_price`. *Watch (P2):* confirm ticker OI == book-summary OI under shadow parity. Soak showed `OI ≈ 1.033B`, `mark ≈ 62450`, both plausible.

---

## 3. Deviations & discoveries (coordinator decisions)

1. **`Websocket.Client` 5.1.2 is already referenced** in `DeribitVerdictEngine.vbproj:13` (added 2026-04-01 in `cf15dd8` "Phase 1A+1B", **referenced nowhere** — zero `.vb` usages). The hand-off prescribes raw `System.Net.WebSockets.ClientWebSocket` ("no package"). **Decision: followed the spec — raw `ClientWebSocket`.** Rationale: (a) the written/approved design specifies it with a detailed custom reconnect/heartbeat/seed loop the library's built-in reconnect would fight; (b) the package is unused speculative scaffolding, not a design dependency; (c) touching the `.vbproj` would break the §9 additive-only proof. **Left the package in place** (removing it is a separate cleanup, out of P1 scope). *P2 option:* the coordinator may prefer switching `DeribitWsFeed` to `Websocket.Client` for its managed reconnect — flagged, not actioned.

2. **Auto-tweaker exclusion (§6/§10.4).** Verified: `PromptBuilder.Build` embeds the **full `settings.json`** in the user message (`PromptBuilder.vb:118-121`), and the HARD CONSTRAINTS do **not** blanket-exclude `network.*` — same status as the pre-existing 3 REST keys *and* all display keys (`performance_display.*`, `analysis_logging.*`, `auto_run.*`). **Decision: did NOT modify `PromptBuilder` in P1**, to preserve the §9 clean 2-file additive proof. The new `ws_*` keys are in the exact same (accepted) risk class as the existing un-excluded `network.*`/display keys — transport plumbing with no failure-rate linkage, which the tweaker won't touch. **Recommendation:** a *separate* hardening commit adding a HARD CONSTRAINT 12 that blanket-excludes all `network.*` (covering the 3 REST keys too, since they were never excluded). Pre-existing gap, not P1-introduced.

3. **Observability:** added one `Log("heartbeat test_request → public/test")` line in `DeribitWsFeed.SendTestAsync` (aids the soak + P2 reconnect debugging). Internal to the dormant new file — no surface impact.

4. **Pre-existing dirty docs (diff-audit heads-up):** `docs/p3-maintenance-pass-proposal.md` and `docs/ui-reskin-handover-2026-05-22.md` show as modified in `git diff` but were **already dirty at session start** (in the session-open git snapshot) — unrelated to P1, not my edits. My only P1 modifications are `Core/Settings/EngineSettings.vb` + `settings.json`.

---

## 4. Acceptance

- **Build 0/0:** main project (Release, to dodge the running app's locked Debug exe) **0W/0E**; `tools/AutoTweaker` **0W/0E**; `verify/ordercheck` **0W/0E**. *(Note: the Debug solution build fails only with `MSB3027`/`MSB3021` file-copy locks because the live app — PID 29620 — is running; compilation itself is clean. Coordinator can confirm with the app closed.)*
- **Additive-only proof:** `git diff --name-only` (P1 scope) = `Core/Settings/EngineSettings.vb` + `settings.json` only; the 5 new files are untracked (`??`). ✓ (plus the 2 pre-existing dirty docs, §3.4).
- **Harness:** `OrderCheck` A1–A15g — *not re-run here* (this change touches no scoring/indicator path; the new files aren't in OrderCheck's compile set). Coordinator to re-run as the standing sanity check.
- **Standalone WS soak (throwaway, deleted after):** ~50s live run, heartbeat=10s. **All 8 assertions PASSED.** Evidence:

```
[WS] connecting to wss://www.deribit.com/ws/api/v2
[WS] connected; seeding via REST…
[WS] subscribed to 7 channels
t= 5s | candles 1m=250 3m=250 5m=210 15m=70 | trades=500 (+) | book=10x10 | tickerAge=0.2s mark=62450.8 OI=1033146310 f8h=0.00000738
t=10s | candles ...                            | trades=503 (+) | book=10x10 | tickerAge=0.2s mark=62451.0 OI=1033147490 f8h=0.00000736
[WS] heartbeat test_request → public/test
t=20s | candles ...                            | trades=512 (+) | book=10x10 | ...
[WS] heartbeat test_request → public/test   (answered every ~10s, zero drops)
t=50s | candles 1m=250 3m=250 5m=210 15m=70 | trades=558 (+) | book=10x10 | tickerAge=0.2s mark=62453.6 OI=1033289570 f8h=0.00000719
  PASS  4 candle series non-empty (seeded)
  PASS  trade buffer populated
  PASS  trade buffer grew past 500 seed (streaming)
  PASS  book ladder present (bids & asks)
  PASS  ticker fresh (<15s)
  PASS  funding_8h non-Nothing
  PASS  mark_price > 0
  PASS  open_interest > 0
=== SOAK PASSED ===
```

- **Forced-reconnect drill — NOT run in this session.** An automated headless session can't sever the OS network for ~60s. The reconnect path (exponential backoff 1→60s, >5/10min storm cooldown, resubscribe + re-seed) is implemented and visible in `DeribitWsFeed.RunLoopAsync`, but the live network-kill drill should be run by the **trader** (toggle WiFi mid-soak) — and P2 explicitly owns the full reconnect/fallback drills. **This is the one §9 acceptance item left open.**

---

## 5. Commit checklist (§10) status

1. ✅ 5 new root `.vb` files.
2. ✅ `settings.json` — 6 `network.*` keys + `version` 38 + `change_log`.
3. ✅ `EngineSettings.vb` — 6 `NetworkSettings` POCO fields.
4. ⏸ PromptBuilder exclusion — **deferred to a separate hardening commit** (§3.2), to keep the additive-only proof clean.
5. ⏳ (coordinator at commit) `DeribitIndicatorProject.md` §15 row + §6 version pointer → v38; `architecture.md` directory note for the 5 new files.

---

## 6. Open items for coordinator / P2

- **[coordinator]** Confirm the raw-`ClientWebSocket` vs `Websocket.Client` decision (§3.1); confirm the `PromptBuilder` exclusion is a separate commit (§3.2); run the harness with the app closed; add §15/§6/architecture notes at commit.
- **[trader]** Run the forced-reconnect network-kill drill live (§4).
- **[P2]** Shadow-parity (≥50 runs) — especially the chart bar roll/tick semantics (forming-bar vs roll vs REST snapshot) and ticker-OI vs book-summary-OI equality (§2 nuance 3); the seed→subscribe boundary gap (`DeribitWsFeed.SeedAsync` comment); 24h soak; consumer routing (`_marketSource`) + status-bar surface + reconnect/fallback drills.
- **[P3]** Cutover (`transport` default → `ws`) + 15m-TTL collapse, gated on the data-gated re-baselines.

---

> ## Coordinator review — APPROVED (2026-06-19)
>
> Independently re-ran builds + harness and audited all 5 files line-by-line.
>
> **Builds (all 0W/0E):** main solution **Release** (compiles the 5 new root files; Release dodges the running-app Debug exe lock — confirms the implementer's MSB3027 was a file-copy lock, not a compile error), `tools/AutoTweaker`, `verify/ordercheck`.
> **Harness:** re-ran `OrderCheck` (the implementer hadn't) → **A1–A15g ALL PASS** (38 checks). P1 touches no scoring path, so unregressed as designed.
> **Additive-only proof confirmed** (`git status`): P1's only tracked edits are `Core/Settings/EngineSettings.vb` + `settings.json`; the 5 new files are untracked. The live verdict path is untouched. (The 2 pre-existing dirty docs §3.4 are unrelated and excluded from the commit.)
> **Source audit — all 5 files correct, faithful to the hand-off, no bugs:**
> - `IMarketDataSource` / `RestMarketDataSource` — exact 5-shape contract; Rest is a clean pass-through (byte-identical fallback). ✓
> - `MarketState` — single `SyncLock`, copy-on-read; correct forming-bar / roll / out-of-order candle semantics; ascending trade ring (cap 5000); funding-preserving ticker update (a frame without funding doesn't clobber). Caps 250/250/210/70. ✓
> - `WsMarketDataSource` — staleness gate on book/trades/ticker, candle getter defers to `IsFresh` (no double-gate), correct ascending tails. ✓
> - `DeribitWsFeed` — **the heartbeat foot-gun is handled** (`method=heartbeat` + `params.type=test_request` → reply `public/test`, sent outside the JsonDocument scope); `_sendLock` serializes outbound sends (concurrent `SendAsync` is illegal — necessary and correct); reconnect backoff 1→60s + reset-on-healthy + >5/10min storm cooldown; REST re-seed on every reconnect; multi-frame receive accumulation; **payload mappings match `DeribitClient` for parity** — `funding_8h` (not `current_funding`), liquidation default `"none"`, `cost`→VolumeUSD fallback `volume*close`. ✓
> **Soak (spec-back §4):** accepted as the standalone-soak acceptance — 8/8 assertions, heartbeats answered every ~10s zero drops, `MarketState` populated (4 series + growing trades + ladder + live `funding_8h`); corroborated by the source audit of the heartbeat/reconnect/parse logic. (I can't re-run a ~50s live-network throwaway here; the evidence + audit are sufficient.)
> **§3 decisions — accepted:**
> - **§3.1 raw `ClientWebSocket`** (not the unused `Websocket.Client` 5.1.2 in the vbproj): **agreed** — the hand-off specifies it, the custom heartbeat/reconnect/seed loop is purpose-built, and touching the vbproj would break the additive proof. The unused package is separate dead-scaffolding cleanup (P2 may reconsider a managed-reconnect library, or just remove it).
> - **§3.2 PromptBuilder exclusion DEFERRED**: **agreed** it's out of P1's additive scope and a *pre-existing* gap (the 3 REST `network.*` keys + all display keys were never excluded either; same accepted risk class — no failure-rate linkage). **Recommended follow-up** (not bundled here, to keep the P1 review clean): a small separate commit adding `"network."` to `SettingsDiffApplier.Validate`'s `RejectedPathPrefixes` + a PromptBuilder **HARD CONSTRAINT 12** (mirrors the Phase-2a kelly./resolution_profiles. guard), with an A15-series harness check. Low urgency — the tweaker is held/dry-run/coordinator-reviewed, and transport has no rational failure-rate proposal.
> **Open (carried):** the forced-reconnect **network-kill drill** is the trader's live step (§4) — the reconnect code is implemented + audited, but a headless session can't sever the OS network; P2 owns the full reconnect/fallback drills anyway.
> **Coordinator added at commit:** `DeribitIndicatorProject.md` §15 v38 row + §6 version pointer → v38; `architecture.md` directory-layout note for the 5 new files.
> **Verdict: APPROVED — local commit.** Trader: run the network-kill drill + smoke-test, then push. Then P2 (consumer routing + shadow parity).
