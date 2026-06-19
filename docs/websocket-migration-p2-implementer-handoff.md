# WebSocket Migration — P2 Implementer Hand-Off (consumer routing + shadow parity + status + drills)

**Parent spec:** `docs/websocket-migration-proposal.md` (APPROVED 2026-06-12), §7 (shadow parity) + §8 (P2). **Predecessor:** P1 shipped + coordinator-reviewed + pushed (`origin/master` ≥ `9cde370`); the dormant foundation (`IMarketDataSource`, `Rest`/`WsMarketDataSource`, `MarketState`, `DeribitWsFeed`, 6 `network.*` keys) is in place. **Cleanup since P1:** the unused `Websocket.Client 5.1.2` package was removed (`4bf3333`) — P2 uses the framework `ClientWebSocket` already in `DeribitWsFeed` (§7).
**Seat:** fresh Opus implementer. **Routing:** spec-back → coordinator review (builds + harness + the drills you *can* run headless) → local commit. **Local-first — NEVER push; the trader tests + pushes.**
**Risk posture:** P2 keeps `network.transport = "rest"` (the **cutover is P3**), so the live verdict still runs on REST and the calibration dataset is **unaffected**. P2 adds (a) the source-routing seam, behaviorally null at `transport=rest`; (b) an *observational* shadow-parity comparison; (c) a status surface; (d) the resilience drills. Zero dataset impact by construction.

---

## 0. Scope

P2 delivers five things, each a separate local commit:
1. **Consumer routing** — `RunAnalysisAsync` fetches through an `IMarketDataSource` selected per run by `network.transport` (+ the per-run REST fallback, proposal §3). Behaviorally identical to today at `transport=rest` (RestMarketDataSource is the verified pass-through).
2. **Shadow-parity mode** — new `network.shadow_parity` flag: with `transport=rest` + the WS feed running alongside, each run also reads the WS source and logs a field-level WS-vs-REST comparison (console + side log, **never the CSV**). The verdict still uses REST → zero dataset impact. The ≥50-consecutive-run parity result is the proposal §7 acceptance gate.
3. **Status-bar surface** — WS health (OK / DEGRADED-REST-fallback / DOWN-reconnecting + reconnect count + stream ages), via the existing status cascade.
4. **Resilience drills** — forced-reconnect, per-run fallback, 24h soak (proposal §7).
5. **Follow-up — auto-tweaker `network.*` hardening** (§6): HARD CONSTRAINT 12 + a code-level `Validate` reject. Independent of the WS work; bundled here at trader request.

**NOT in P2:** the cutover (`transport` default → `ws`) and the 15m-TTL collapse on the WS path → **P3** (gated on the data-gated re-baselines). No indicator/scoring/CSV change. The P4+ feature catalogue stays future specs.

---

## 1. Settings (v38 → v39)

Add ONE key to the `network` block:
```
network.shadow_parity   false   ' when true (+ transport=rest), run the WS feed alongside and log per-run WS-vs-REST parity. Dev/validation mode; default off = zero WS overhead.
```
POCO: `NetworkSettings.ShadowParity As Boolean = False` (`<JsonPropertyName("shadow_parity")>`). Bump `version` → **39**, prepend a `change_log` entry, §15 row (coordinator at commit). Adding a new key bumps the version (unlike the §10a operational-*value* saves). `shadow_parity` is **off the auto-tweaker surface** — it rides the §6 blanket `network.*` exclusion.

---

## 2. Consumer routing (`_marketSource`) + per-run fallback

### 2.1 MainForm wiring (thin host glue — the feed itself stays host-agnostic)
- Add fields: `_marketState As MarketState`, `_wsFeed As DeribitWsFeed`, and a helper that returns the per-run source.
- **Feed lifecycle:** if `Network.Transport = "ws"` **OR** `Network.ShadowParity = True`, construct `_marketState` + `_wsFeed = New DeribitWsFeed(_marketState)` and `_wsFeed.StartAsync()` once at form load; `_wsFeed.Stop()` on form close. If neither, don't start it (pure REST, zero overhead — today's behavior). (The CLI port will start/stop the feed equivalently; keep the lifecycle calls trivial.)

### 2.2 The 7 live call sites
In `RunAnalysisAsync` (`MainForm_Analysis.vb`), replace the static `DeribitClient.GetXxxAsync(...)` calls (lines ~63–99, incl. the 15m TTL branch and the v36 exec-resolution fetch at line ~99) with `src.GetXxxAsync(...)` where `src As IMarketDataSource` is chosen **per run** by `ResolveSource()`:

```
Private Function ResolveSource() As IMarketDataSource
    Dim net = SettingsLoader.Current.Network
    If net.Transport <> "ws" Then Return _restSource          ' "rest" (default) — pass-through, unchanged behavior
    ' transport = "ws": per-run fallback (proposal §3) — if the feed is unhealthy/stale
    ' and fallback is on, serve this run from REST and surface DEGRADED.
    If net.WsFallbackToRest AndAlso _wsFeed IsNot Nothing AndAlso _wsFeed.IsDegraded() Then
        _wsDegradedThisRun = True
        Return _restSource
    End If
    Return _wsSource                                          ' WsMarketDataSource(_marketState)
End Function
```
- `_restSource = New RestMarketDataSource()`; `_wsSource = New WsMarketDataSource(_marketState)` (constructed when the feed starts).
- **`DeribitWsFeed.IsDegraded()`** (add): true when not connected, in cooldown, or all primary streams are stale (`> ws_stale_after_sec`). This is the per-run health gate.
- The existing skip-gate (`MainForm_Analysis.vb:101-109`) is unchanged — if the WS source returns `Nothing` for a required shape *despite* passing the health gate (a race), the run skips exactly like a REST failure (no row lost).
- **15m TTL:** leave the TTL cache logic as-is in P2 (correct for the REST path; on the WS path the source serves a fresh copy each time — harmless). The TTL collapse is P3.
- **Backfill stays REST:** `OhlcCache`, `LivePerformanceTracker.FetchGapChunked`, and the time-range `GetCandlesAsync(res,startMs,endMs)` overload are NOT routed — direct `DeribitClient` (P1 §3 contract).

At `transport=rest` (the P2 default) `ResolveSource()` always returns `_restSource` → byte-identical to today.

---

## 3. Shadow-parity comparison (the proposal §7 gate)

When `Network.ShadowParity = True` (with `transport=rest`, so REST is authoritative), after the primary REST fetch in a run, also read the **WS** source (`_wsSource`, cheap in-memory `MarketState` reads) and compare, via a new host-agnostic `ShadowParityComparer` (root, `Console.WriteLine` + a side log file `ws_parity_log.txt` in the exe dir — **never the CSV, never the scoring path**):

| Field | Compare | Pass tolerance |
|---|---|---|
| Candles 1/3/5/15 | **last CLOSED bar** OHLCV (index `Count-2`; the forming last bar differs by nature — exclude it) | exact on closed bars |
| Top-of-book | best bid + best ask | within one tick |
| Ticker | `funding_8h`, `open_interest`, `mark_price` | funding exact; OI/mark within one update |
| Trades | buffer **superset-consistency** (REST last-N present in the WS buffer by `timestamp`+`price`; the newest few may differ by timing) + last-trade price/timestamp | superset + last within a few trades |

- Log per-run: `PARITY ok` or `PARITY MISMATCH <field>: rest=… ws=…`. Track a **running consecutive-pass counter**; surface `parity NN/50` in the status bar.
- `TradeRecord` has no `trade_id` (fields are Price/Amount/Direction/Timestamp/Liquidation) — match on `timestamp`+`price` (the proposal's "id" is satisfied by timestamp+price; do **not** add a column).
- **Known watches to log explicitly** (don't fail the gate prematurely — record + surface): `chart.trades` forming-bar vs roll-vs-REST-snapshot drift (P1 spec-back §2 nuance), ticker-OI vs book-summary-OI equality (P1 §2 nuance 3), and the seed→subscribe boundary gap (P1 `SeedAsync` comment). These are exactly what shadow parity exists to confirm.

**Gate:** ≥50 consecutive runs all-fields-pass on closed bars + within-tolerance book/ticker + superset-consistent trades (proposal §7). Report the run count + any mismatch classes in the spec-back.

---

## 4. Status-bar surface (WS health)

Mirror the existing status cascade (`SettingsLoader.LastLoadError` / `_ledgerWarn` / skip-counter pattern in `MainForm_Layout`/`_Render_Header`). Add a WS line that renders only when the feed is active (`transport=ws` or `shadow_parity`):
- `WS OK · 1m/3m/5m/15m fresh · trades N` — healthy.
- `WS DEGRADED — REST fallback (stream stale)` — per-run fallback fired (`_wsDegradedThisRun`).
- `WS DOWN — reconnecting (Xs backoff, R reconnects)` — disconnected.
- Shadow mode adds `· parity NN/50`.

Expose the needed read-only state on `DeribitWsFeed`/`MarketState`: `IsConnected`, `ReconnectCount`, `LastFrameUtc`, per-stream `LastUpdate` (MarketState already has these), and `IsDegraded()` (§2.2). No `Control.Invoke` inside the feed — the UI reads these on the analysis thread the same way it reads other state.

---

## 5. Resilience drills (proposal §7 — acceptance evidence)

- **Forced-reconnect drill:** with the feed running, sever the network ~60s → confirm auto-reconnect (backoff), resubscribe + re-seed, parity restored after recovery, zero rows lost. (The trader runs the live network-kill; the implementer can simulate a forced socket close in a throwaway to exercise the path.)
- **Per-run fallback drill:** `transport=ws` + feed forced down → runs continue via REST, status shows DEGRADED, no row lost, CSV byte-identical to a REST run.
- **24h connection soak:** heartbeats answered, zero unintended drops, reconnect count sane. (Trader-run; implementer reports the mechanism.)

---

## 6. Follow-up 1 — auto-tweaker `network.*` hardening (HARD CONSTRAINT 12)

Pre-existing gap surfaced in the P1 review: `PromptBuilder.Build` inlines the full `settings.json`, and `network.*` (the 3 REST keys + the new `ws_*`/`shadow_parity`) is **not** excluded — same un-excluded status as display keys, but transport is worth hardening now that it's a behavioral flag. Mirror the Phase-2a kelly./resolution_profiles. guard:
1. **Code-level (load-bearing):** add `"network."` to `SettingsDiffApplier.Validate`'s `RejectedPathPrefixes` (NOT `ValidateSnapshotContent` — reverts may legitimately carry these). A crafted `network.transport` diff must fail `Validate` + never apply.
2. **Prompt-level:** add **HARD CONSTRAINT 12** to `PromptBuilder` — "never propose `network.*` (transport plumbing, no failure-rate linkage)."
3. **Harness:** extend the A15-series (e.g. `A15h`) — a `network.transport` / `network.ws_url` diff is rejected by `Validate`; a legitimate scoring key still passes.

Self-contained; its own commit. Low urgency (tweaker is held/dry-run/coordinator-reviewed) but closes the gap cleanly.

## 7. Follow-up 2 — WS library (informational; already actioned)

The unused `Websocket.Client 5.1.2` package was removed (`4bf3333`); P1/P2 use the framework `System.Net.WebSockets.ClientWebSocket` with the custom heartbeat/reconnect/seed loop. **If P2's drills expose a reconnect edge the hand-rolled loop handles poorly**, a managed-reconnect library is an option — re-add the **latest** version then (don't repin 5.1.2) and switch `DeribitWsFeed.RunLoopAsync` to it. Default: stay on raw `ClientWebSocket` (it passed the P1 soak + the trader's network-kill drill).

---

## 8. Out of scope for P2 (→ P3 / P4+)

Cutover (`network.transport` default → `"ws"`) + the 15m-TTL collapse on the WS path → **P3**, gated on the data-gated re-baselines (the first recalibration must close on a single-transport dataset, proposal §8). Sub-minute cadence, time-averaged OFI, realtime exit guard, etc. → P4+ separate specs.

---

## 9. Acceptance

- **Builds 0/0:** solution + `AutoTweaker` + `OrderCheck`.
- **Harness:** A1–A15g unregressed + the new **A15h** (§6). The routing change touches `RunAnalysisAsync` but not scoring — A1–A15g must stay green; if any moves, STOP (the pass-through routing should be behaviorally null).
- **`transport=rest` regression check:** with `shadow_parity=false`, a normal run is byte-identical to pre-P2 (same CSV row, same verdict) — the routing seam is null. Prove it (diff a run's CSV row vs a v38 run, or reason it from the pass-through).
- **Shadow-parity evidence:** ≥50-consecutive-run parity report (§3) + the mismatch-class log; the three known-watch findings (bar roll, OI equality, seed gap) characterized.
- **Drills:** the §5 evidence (implementer runs what's headless-possible; trader runs the live network-kill + 24h soak).
- **Spec-back** (`docs/websocket-migration-p2-spec-back.md`): routing diff, the shadow-parity run count + findings, status-surface screenshots (delete after, per [[delete-test-screenshots]]), the §6 hardening + A15h result. Routes to coordinator.

## 10. Commit checklist (each a separate local commit)
1. Routing: `_marketSource`/`ResolveSource()` + feed lifecycle in MainForm; the 7 call sites; `DeribitWsFeed.IsDegraded()`/health props. `network.shadow_parity` key + POCO + v39.
2. Shadow-parity: `ShadowParityComparer` + the per-run hook + side log.
3. Status surface: WS health line.
4. Follow-up 1: `network.*` hardening (Validate guard + HARD CONSTRAINT 12 + A15h).
5. (coordinator at commit) `DeribitIndicatorProject.md` §15 v39 row + §6 → v39; `architecture.md` data-flow note (RunAnalysisAsync routes through `IMarketDataSource`).

> Build in a fresh Opus conversation against this hand-off + the parent proposal + the P1 spec-back (the bar-roll/OI/seed-gap watches). Verify any new channel/payload assumptions against current Deribit docs. Local commits as you go; the trader tests + pushes. P3 (cutover) waits on the data-gated re-baselines.
