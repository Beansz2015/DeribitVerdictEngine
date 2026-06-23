# WebSocket Migration P3 — Cutover (trades-staleness resolution + `transport→ws` + 15m-TTL collapse)

**Status: READY FOR IMPLEMENTER (2026-06-24).** Both hard preconditions (§2) — **G1** (live parity + 12h soak + reconnect/fallback drills) and **G2** (single-transport re-baselines closed on REST data) — are now **MET**, so this draft is promoted to the build hand-off; scope below is final. Local-first — coordinator authors; a fresh implementer builds → coordinator reviews → the trader tests + flips + pushes.

**Parent:** `websocket-migration-proposal.md` (§7 acceptance, §8 P3). **Builds on:** P1 (`9cde370`, pushed) + P2 (coordinator-approved, local, awaiting the live gate). **Memory:** `project-websocket-migration`.

---

## 1. Scope

P3 is the **semantics-neutral cutover** — flip the live path from REST polling to the WS feed without changing what any indicator sees. Three changes + one marker:

1. **Trades-staleness resolution** (the one substantive design decision — §3). Gate the WS trades stream on **connection-health, not last-trade-age**. *Trader-agreed 2026-06-20.*
2. **`network.transport` flip** rest → ws (§5) — a deliberate, reversible settings edit, not a silent POCO-default change.
3. **15m-TTL collapse on the WS path** (§4) — the 60s MTF cache is pointless when 15m streams in-memory.
4. **Dataset-boundary marker** — a dated §15 entry (informational; semantics unchanged by design). No CSV column (§5).
5. **Closed-bar volume tolerance** (§7 decision (a)) — a small *relative* volume tolerance on `ShadowParityComparer`'s closed-bar check so benign WS 3-min aggregation drift (~2.5%, OHLC-exact) stops resetting the parity-gate streak. Parity-instrument only (runs under `shadow_parity`) — **not** a cutover-mechanics change; supports the §6 gate re-run.

**Out of scope (P4+, each its own re-baseline-flagged spec):** sub-minute cadence, time-averaged OFI, liquidation-stream cascades, book absorption, event-driven scoring. P3 buys entry-moment freshness at **unchanged cadence/shapes/windows** — nothing that moves calibration.

## 2. Hard preconditions — these gate the SHIP, not this spec

Both must hold before `transport` is flipped. **Both are now MET (2026-06-24)** — status callouts below.

- **(G1) P2's §7 acceptance passes LIVE.** ≥50 *consecutive* shadow-parity runs (closed-bar OHLCV identical; book/ticker within tolerance — book widened to 5 ticks in P2; trades buffer superset-consistent) **+** 24h connection soak (heartbeats answered, zero unintended drops) **+** forced-reconnect drill (kill network 60s → auto-recovery → parity restored) **+** fallback drill (`transport=ws` + feed down → runs continue via REST, status shows degraded). All trader-run; P2's 8-run bench soak was clean but is not the gate. **Re-run G1 *after* the §3 trades fix** — the quiet-market case that resets the streak today should then pass.
- **(G2) Single-transport calibration closed.** Per proposal §8, P3 waits until the data-gated re-baselines close on **REST-collected** data so the first WS-era recalibration doesn't straddle transports. Open today: the **(B) Asia/London ROC** Monday refine, the **§12 3-min hold-window** recal, and the ≥300-row re-baseline review. Until these close on REST, the flip stays parked.

The design below is robust to G1's outcome (if parity fails, P3 doesn't ship — the cutover mechanics are unaffected). What firms up only after G1 is the **go/no-go** and any 15m tuning (§7).

> **G1 STATUS (2026-06-24): essentially MET.** After the §3 trades fix + the storm-guard fix, the trader re-ran the gate live: parity **51/50** (the quiet-market `WS-NOT-READY` resets gone), reconnect drill ✓ (~20s recovery), fallback drill ✓ (silent REST fallback). Then a **12h connection soak ✓** — 1471 runs, **90.6% parity-clean**, peak streak **79/50**, **zero `WS-NOT-READY trades`** resets, continuous 30s cadence with no drops/stalls. **One residual the soak surfaced — the WS 3-min closed-bar volume undercount (§7) — folds into the build as decision (a).**

> **G2 STATUS (2026-06-24): MET.** All three single-transport re-baselines closed on REST-collected data: **(B) Asia/London ROC** v40 + v41 Monday refine (`53d896f` + `1beca1f`, pushed), the **3-min hold-window** recal (`3d473a1` + `1825fad`, implemented + **plateau-validated** on the 5275-row post-soak book — ASIA MEDIUM_LONG 24→13→13, flattens at 30m), and the **≥300-row review** (report `20260623_192312`: NY×1 2698 / LONDON×3 1780 / ASIA×3 797). The CONFIRMED-tag question that report raised was investigated + resolved (`post-websocket-post-calibration-backlog.md` D7 — not a tag defect, no pre-cutover work). **Both gates open → P3 is cleared to build.**

## 3. Decision 1 — trades-staleness: connection-health gating (SETTLED — IMPLEMENTED 2026-06-23)

> **IMPLEMENTED 2026-06-23, early, as the G1 prerequisite.** The first live shadow-parity gate run (NY, 65 runs) reset the streak 5× on `WS-NOT-READY trades: ws buffer empty/stale` — the trades age-gate tripping in an *active* market because the ~10s run cadence ≈ `ws_stale_after_sec` (10s), so natural trade-spacing lulls cross it. Confirmed this is the gate's hard blocker (not just a quiet-market nicety), so this fix shipped ahead of the rest of P3. `WsMarketDataSource` gains a `healthCheck As Func(Of Boolean)` ctor param (default `Nothing` ⇒ legacy age-gate, keeps all existing callers/tests byte-identical); `GetRecentTradesAsync` bails only when `Not healthCheck()`; `MainForm.InitMarketDataSources` wires `Function() _wsFeed.IsConnected AndAlso Not _wsFeed.IsCoolingDown` (feed constructed before the source). Book/ticker keep the age-gate. WS-path-only; transport stays `rest`; scoring/dataset untouched. Validation = the live gate re-run (the WS source isn't harness-compiled — consistent with all prior WS validation being live); builds 0/0, existing harness A1–A15h unregressed. The remaining gate reset was 1× book jitter (a 19-tick fast-NY move > 5-tick tol) — left as-is (rare; see §4-adjacent note in the gate report).

**The bug (verified in source).** `DeribitWsFeed.IsDegraded()` = `Not connected OR coolingDown OR (book AND trades AND ticker ALL stale)`. `WsMarketDataSource.GetRecentTradesAsync` returns `Nothing` when `now − TradesLastUpdate > ws_stale_after_sec` (10s). In a quiet market, book + ticker keep updating (live connection) so `IsDegraded()=False` (not *all* stale) → no whole-run REST fallback → but `GetRecentTradesAsync` returns `Nothing` → the required-result skip-gate fires → **the run SKIPS**, costing a row. That contradicts the proposal's "WS never costs a row vs REST".

**Root cause.** Trades *legitimately* go quiet — no trades means no trade messages. REST has **no** such gate: `GetRecentTradesAsync` returns whatever trades exist, however old. Age-gating WS trades mistakes "no new trades" for "stream broken." Book and ticker, by contrast, update continuously on a live connection, so *their* age-gate correctly flags a broken stream. The asymmetry is the bug.

**Resolution (trader-agreed).** Gate the **trades** stream on **connection-health**, not last-trade-age:

- `WsMarketDataSource.GetRecentTradesAsync` returns the buffer whenever the feed is **connected and not cooling down**; it returns `Nothing` only when the connection itself is unhealthy — which `IsDegraded()` already catches upstream, falling the *whole run* back to REST. A quiet-but-complete buffer is valid data and matches what REST would return in the same quiet market (parity restored).
- **Book + ticker stay age-gated** — staleness there genuinely means broken.
- **`IsDegraded()` stays all-stale.** It is the coarse whole-run "connection basically dead" backstop (`Not connected` / `coolingDown` / all-three-frozen). With trades no longer age-gated at the serving layer, its `tradesStale` term only contributes to the rare all-frozen case; leave it (book/ticker freshness keeps a live run on WS). Document the intent so a future reader doesn't "tidy" it.

**Wiring (host-agnostic).** `WsMarketDataSource` currently holds only `MarketState`. Add a connection-health signal via a lightweight delegate on the ctor — `New(state, healthCheck As Func(Of Boolean), Optional staleAfterSec)` — wired in `MainForm.InitMarketDataSources` as `Function() _wsFeed.IsConnected AndAlso Not _wsFeed.IsCoolingDown`. The delegate (not a `DeribitWsFeed` reference) keeps `WsMarketDataSource` decoupled from the feed's concrete type → still host-agnostic and unit-testable with a stub. `GetRecentTradesAsync` swaps `If IsStale(TradesLastUpdate)` → `If Not _healthCheck()`.

## 4. Decision 2 — 15m-TTL collapse on the WS path

**Today (REST):** `RunAnalysisAsync` only fetches 15m when `_mtfCandles15m Is Nothing OrElse (now − _mtfLastFetchTime) ≥ MTF_TTL_SECONDS` (60s), to save the HTTP call. **On WS:** `src.GetCandlesAsync("15", 70)` reads from `MarketState` — in-memory, current, zero API cost — so the TTL gate buys nothing.

**Change.** When `transport=ws`, **bypass the `mtfStale` gate and read 15m from the source every run**. When `transport=rest`, keep the TTL (still saves polling). The branch lives in `MainForm_Analysis` (the TTL state `_mtfCandles15m`/`_mtfLastFetchTime` is host state); keep it minimal.

**Semantics-neutrality guard.** On WS the MTF gate sees 15m up to ~60s fresher than REST (REST: ≤60s stale forming bar; WS: current). Same closed 15m bars — only the forming bar is more current, and 15m DMI/ADX + EMA move slowly, so the gate-flip rate is negligible. Still a (tiny) behaviour delta → the §7 parity gate already requires **closed-bar identity**; additionally **watch the MTF gate decision** for any WS-vs-REST divergence during G1. The collapse is **WS-path-only**, so REST stays byte-identical.

## 5. Decision 3 — the flip + rollout + marker

- **The flip is a settings edit, not a POCO default change** *(confirmed 2026-06-20, diverges from §8's literal "default → ws").* **Keep the POCO default `"rest"`**; the trader sets `network.transport: "ws"` in `settings.json` after G1+G2 — a deliberate, dated, reversible event. **Clinching reason — the POCO default is the failure-mode fallback:** when `settings.json` is absent or corrupt, `SettingsLoader` serves POCO defaults (the `LastLoadError` silent-defaults path). That fallback must stay the *proven REST path* — a `"ws"` default would degrade **unsafe** on a config failure, whereas `"rest"` makes any config breakage fall back safe. (The "silent-flip-every-install" concern is secondary to this.) The dated/reversible edit also keeps the cutover human-timed and gated, which is the point.
- **Flip is NOT symmetrically hot — restart asymmetry** (reconciles with proposal §5: "transport changes require app restart; hot-swap is out of scope"). The feed starts only in `InitMarketDataSources` at form load, gated on `wantWs = transport="ws" OR shadow_parity`:
  - **Rollback (ws → rest): genuinely hot** — next run `ResolveSource()` returns `_restSource` (`transport ≠ "ws"`); zero restart.
  - **Forward cutover (rest → ws): needs an app RESTART** from a cold REST start — a hot edit to `"ws"` does not re-run `InitMarketDataSources`, so `_wsSource` stays `Nothing` and `ResolveSource()`'s `_wsSource Is Nothing → _restSource` guard keeps the run on REST until restart. **Exception:** if the feed is already running because `shadow_parity` was on for the G1 gate/soak, the hot edit takes effect on the next run (no restart). So the natural cutover path is: run the gate with `shadow_parity=true` → flip `transport=ws` (feed already live) → it takes effect immediately; otherwise restart after the edit.
- **Rollback** = set `transport` back to `"rest"` (hot, zero code, next run). The per-run `IsDegraded()` fallback already absorbs *transient* WS failures; this manual rollback is for *sustained* distrust.
- **Dataset-boundary marker** = a dated §15 / `change_log` entry on the flip. **No CSV transport column** — a header rotation resets the live book + the tweaker's NY×1 accumulation mid-flight, for nothing the dated entry doesn't give (same no-rotation rule as the (B) delta-column and the v36 decisions). If continuous transport stamping is ever wanted, bundle it into the next natural schema bump.

## 6. Acceptance (P3 itself, once G1+G2 open)

- Build 0/0 — solution(Release) + AutoTweaker + OrderCheck.
- Harness: new checks for (a) trades served when connected-but-quiet (stub `healthCheck=True`, `TradesLastUpdate` old → buffer returned, not `Nothing`) and connection-down (`healthCheck=False` → `Nothing`); (b) the WS-path 15m read-every-run vs REST-path TTL retained. (A16-series; `verify/` harness.)
- **`transport=rest` byte-identical** — all §3/§4 changes are WS-path-only; prove via the rest-path regression (the established null-at-rest check).
- **Re-run the §7 ≥50-run parity gate after the §3 trades fix + the closed-bar volume tolerance (§1 #5)** — neither the quiet-market trades case nor the benign 3-min volume drift (the soak's 8-bar resets) should reset the streak.
- 24h soak + reconnect + fallback drills (trader-run, live).

## 7. Open / contingent (firms up after G1's live result)

- ~~The **go/no-go**~~ — **RESOLVED → GO (2026-06-24):** parity held ≥50 consecutive (peak 79/50) through the 12h soak; closed-bar OHLCV identical, the only residual being the volume drift (decision (a) below).
- Any **15m-specific tuning** if the soak surfaces MTF-gate divergence under the TTL collapse.
- **`funding_8h` + the 3-min chart parity under the long soak — TESTED (12h soak, 2026-06-23/24).** `funding_8h` (4× last-digit rounding, ~1e-9) and book/mark_price jitter were all benign / within tolerance. **One systematic finding: the WS 3-min closed-bar VOLUME undercounts REST by ~2.5%** — OHLC matched *exactly*; 8 distinct bars, **78/78 non-equal cases ws-low, never high**. The WS `chart.trades` 3-min bar is missing a sliver of boundary volume vs Deribit's server-side REST candle (likely a first/last-tick bucketing gap). **Impact small** — volume feeds DynamicNorms / volume-spike confirmation / VWAP; ~2.5% on ~6% of 3-min bars won't flip a 3× breakout gate — **but systematic, not random → a residual semantics-neutrality crack on the WS 3-min path, biting only at cutover** (`transport=rest` stays byte-identical regardless). **Pre-cutover decision (settle before `transport=ws`):** (a) *pragmatic* — add a small **relative** volume tolerance to `ShadowParityComparer`'s closed-bar check (mirrors the book/mark_price tolerances; stops flagging benign aggregation drift) and accept ~2.5% as immaterial to scoring; or (b) *thorough* — fix the `DeribitWsFeed` `chart.trades` bar boundary aggregation so WS volume matches REST. **DECISION (2026-06-24): (a)** — add the relative volume tolerance to the closed-bar check + a documented note (~2.5% is immaterial to scoring in normal flow). **Standing watch (`DeribitIndicatorProject.md` §12):** the one place 2.5% could bite is a **volume spike** at the breakout-confirm boundary (vol > 3× SMA-9) — watch whether the WS undercount ever pulls a reading below the 3× gate when REST would clear it. Escalate to (b) (fix the `chart.trades` boundary aggregation) only if that watch trips.

## 8. Sequencing

P2 live §7 gate + push ✓ → REST-single-transport re-baselines close ✓ [(B) v40/v41 ✓ + §12 3-min hold-window ✓ (plateau-validated) + ≥300-row review ✓] → **▶ P3 IMPLEMENT (NEXT)** (§3 trades-gate ✓ shipped early + §4 15m-collapse + §1 #5 volume tolerance + §6 harness) → **re-run §7 gate** → trader flips `transport=ws` (dated §15 marker) → monitor / rollback-ready → **P4** feature waves, each its own re-baseline-flagged spec (recommended first: realtime exit guard / on-close analysis — zero scoring impact — then time-averaged OFI as the first re-baseline upgrade).
