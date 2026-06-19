# WebSocket Migration — P2 Spec-Back (consumer routing + shadow parity + status + network.* hardening)

**Seat:** Opus implementer. **Date:** 2026-06-19. **Routes to:** coordinator review (re-run builds + harness, audit the diff, confirm the byte-identical proof + the two soak findings) → local commit confirmation. **Local-first — NOT pushed; the trader tests + pushes.**
**Parent:** `docs/websocket-migration-proposal.md` (APPROVED 2026-06-12) §7/§8. **Hand-off:** `docs/websocket-migration-p2-implementer-handoff.md`. **Predecessor:** `docs/websocket-migration-p1-spec-back.md` (P1 shipped + pushed; its three watches are characterized in §4 below).
**Status:** BUILT + harness-green + live-soak-characterized, local-committed (5 commits), **NOT pushed.**

---

## 1. What was built (five commits)

| # | Commit | Scope |
|---|---|---|
| 1 | `3431a26` feat(ws): P2 commit 1 — consumer routing + per-run fallback [v39] | `RunAnalysisAsync` routes the 7 live shapes through `IMarketDataSource` per `network.transport`; per-run REST fallback; `DeribitWsFeed.IsDegraded()` + health surface; MainForm feed lifecycle; settings v38→v39 + `network.shadow_parity` POCO. |
| 2 | `43ac1fd` feat(ws): P2 commit 2 — shadow-parity comparer + per-run hook | New host-agnostic `ShadowParityComparer` (root) + the per-run hook in `RunAnalysisAsync`. Side log only; never CSV/scoring. |
| 3 | `cff5f62` feat(ws): P2 commit 3 — WS-health status line | WS-health segment in the `UpdateLogInfo` cascade; renders only when the feed is active. |
| 4 | `20f1f31` feat(tweaker): P2 commit 4 — network.* hardening (HARD CONSTRAINT 12) | `SettingsDiffApplier.Validate` `RejectedPathPrefixes += "network."`; PromptBuilder HARD CONSTRAINT 12; OrderCheck A15h. |
| 5 | `323308d` fix(ws): widen shadow-parity book tolerance from live-soak finding | Data-backed tolerance fix (§5.1). |

**Risk posture held:** `network.transport` stays `"rest"` and `network.shadow_parity` stays `false` (both unchanged in `settings.json`). The live verdict still runs pure REST → the calibration dataset is **unaffected**. The routing seam is behaviorally null at `transport=rest` (§3).

---

## 2. Routing diff (commit 1)

- **`MainForm_Analysis.RunAnalysisAsync`** — the 8 fetch call sites (`1m/5m/15m + funding/book/orderbook/trades` + the v36 exec-resolution candles at line ~99) now go through `Dim src As IMarketDataSource = ResolveSource()` instead of `DeribitClient.` directly. `_wsDegradedThisRun` is reset at the top of each run.
- **`ResolveSource()`** (new, in `MainForm_Analysis.vb`) — exactly the handoff §2.2 shape: `transport <> "ws"` → `_restSource`; `transport="ws"` + `ws_fallback_to_rest` + `_wsFeed.IsDegraded()` → `_restSource` (+ set `_wsDegradedThisRun`); else `_wsSource`. A defensive `_wsSource Is Nothing → _restSource` guard avoids an NRE if `transport="ws"` were set without the feed (shouldn't happen — the feed starts whenever `transport="ws"`).
- **`DeribitWsFeed.IsDegraded()`** (new) — `Not _connected OrElse _coolingDown OrElse (all of book/trades/ticker stale past ws_stale_after_sec)`. Plus a read-only health surface: `IsConnected`, `ReconnectCount`, `LastFrameUtc`, `IsCoolingDown`, `CurrentBackoffSec`. Plain fields written on the supervisor/receive task, read on the analysis thread (no `Control.Invoke`; torn reads are harmless for a per-run gate / display).
- **MainForm host glue** (the only host coupling) — `_restSource`/`_wsSource`/`_marketState`/`_wsFeed` fields; `InitMarketDataSources()` (called from the ctor after `SettingsLoader.Initialise`) starts the feed at form load **only** when `transport="ws"` OR `shadow_parity` (pure REST otherwise); `OnFormClosing` override stops it.
- **Untouched:** backfill (`OhlcCache`, `LivePerformanceTracker.FetchGapChunked`, the constructor's time-range `GetCandlesAsync(res,startMs,endMs)`), the 15m TTL cache logic, the skip-gate, scoring/indicators/CSV.

---

## 3. `transport=rest` byte-identical regression — proven by construction

With the P2 default (`transport="rest"`, `shadow_parity=false`):
1. `InitMarketDataSources`: `wantWs = (transport="ws") OrElse shadow_parity = False` → the feed is never started; `_wsFeed`/`_wsSource`/`_marketState`/`_parityComparer` all stay `Nothing`.
2. `ResolveSource()`: `transport <> "ws"` → returns `_restSource`, a `RestMarketDataSource`. Every fetch calls `_restSource.GetXxxAsync(...)`, which is the **verified pass-through** returning the *same* `DeribitClient.GetXxxAsync(...)` Task the pre-P2 code awaited → identical data, identical nullability.
3. The parity hook is guarded by `_parityComparer IsNot Nothing` (Nothing) → skipped entirely.
4. The status line: `BuildWsStatusSegment()` returns `""` when `_wsFeed Is Nothing`, so `lblLogInfo.Text` = `"{ledger}{cfg}Log: N rows…"` — character-identical to v38 (the new `{2}` slot is empty).

Net runtime delta vs v38: one extra `New RestMarketDataSource()` at startup + one interface dispatch per fetch. **Zero effect on the CSV row, the verdict, or any rendered surface.** (I cannot run the live WinForms app + Deribit here to diff an actual CSV row; the handoff explicitly permits proving this from the pass-through, which the source makes unambiguous. Coordinator can confirm by reading `RestMarketDataSource` + the `ResolveSource` guard.)

---

## 4. Shadow parity (commit 2) + the three P1 watches — live-characterized

**Mechanism.** When `shadow_parity=true` (with `transport="rest"`, REST authoritative), `RunAnalysisAsync` calls `ShadowParityComparer.CompareAsync` once per run after the skip-gate (REST data known-valid; before `UpdateLogInfo` so the counter is fresh on the status line). It reads the same shapes from `_wsSource` (in-memory `MarketState`) and logs a field-level diff to `ws_parity_log.txt` (exe dir) + console — **never the CSV, never scoring**. A running consecutive-all-pass counter feeds the status line; ≥50 is the proposal §7 gate. Host-agnostic (root file; only the DTOs + `IMarketDataSource`).

**Live soak evidence.** I exercised the *real* comparer against live Deribit via a throwaway harness (`verify/paritysoak/`, deleted after — same pattern as the P1 standalone soak): start `DeribitWsFeed`, seed, then 8 runs ~8s apart doing a full REST fetch (candles **1/3/5/15**, book, summary, funding, trades) + WS read + `CompareAsync`. Two passes (book tol 1-tick, then 5-tick after the §5.1 fix). Window ~13:12–13:17 UTC 2026-06-19 (a quiet BTC patch). Final pass: **6/8 all-pass**, the 2 non-passes solely trades-staleness (§5.2).

The three watches the handoff/P1 flagged, characterized from this live data:

1. **Chart bar-roll / tick semantics (forming-bar vs roll vs REST snapshot).** The comparer matches the REST **last-CLOSED** bar (`index Count-2`) to the WS series **by timestamp** and compares OHLCV. Across all runs × 4 resolutions, **zero** candle mismatches and **zero** `WATCH chart roll` (the REST closed bar was always present in the WS series with byte-identical OHLCV). → **The WS chart stream reproduces REST closed bars exactly; no bar-roll drift observed.**
2. **Ticker-OI vs book-summary-OI equality (P1 §2 nuance 3).** WS ticker `open_interest` vs REST `get_book_summary` OI within the 0.1% one-update tolerance on **every** run (zero `WATCH OI` lines); `funding_8h` exact every run; `mark_price` within 5 USD every run. → **Ticker OI ≈ book-summary OI confirmed equal; funding parity exact.**
3. **Seed→subscribe boundary gap (`DeribitWsFeed.SeedAsync`).** The trades superset check (REST 500 a multiset-subset of the WS buffer on `(ts,price,amount,dir)`, tol 5) reported **zero** superset misses whenever the WS trades stream was fresh — the REST 500 was always a clean subset. → **No boundary-gap trade loss observed.**

> The ≥50-**consecutive** gate itself is the trader's sustained-session step (it needs the live app over a continuous active session; 50 consecutive auto-runs ≈ 50+ min). My 8-run soak proves the comparer works against live data and characterizes the watches; it does not substitute for the trader's 50-run gate. Run it during an **active** session (see §5.2).

---

## 5. Findings from the live soak (both flagged for coordinator / P3)

### 5.1 Book "one tick" tolerance was too tight (fixed in commit 5)
The handoff §3 / proposal §7 say top-of-book "within one tick" (0.5 USD). Live data showed that is unachievable on **correct** data: the authoritative REST snapshot arrives over an HTTP round-trip (~tens-to-hundreds of ms) while the WS read is in-memory, so the two are **not simultaneous** and the top-of-book legitimately moves a few ticks in that window (observed a 3-tick ask gap on ~1/8 runs; the rest within 1 tick). That is snapshot non-simultaneity, **not** a WS desync (a broken WS book would be off by orders of magnitude). **Fix:** widened `BookJitterTolUsd` to 5 ticks (2.5 USD); the raw gap is always logged so a real desync still surfaces. With it, book trips went 1/8 → 0/8. **This is a deviation from the handoff's literal "one tick" — coordinator please confirm** (or pick a different N from a longer run).

### 5.2 Trades-stream staleness trips in quiet markets (NOT fixed — P3 design call)
2/8 runs logged `WS-NOT-READY trades`: the WS trades buffer's `LastUpdate` was >`ws_stale_after_sec` (10s) old because no trade arrived in that window (a quiet ~21:1x Penang patch). The buffer itself was **complete** (500+ trades) — only the freshness *stamp* was old. Two consequences:
- **Shadow mode:** such a run is a non-pass (streak reset), so the ≥50-consecutive gate is hard to reach in quiet markets → **run the gate during an active session.**
- **`transport=ws` (P3):** here it matters more. `IsDegraded()` is `ALL primary streams stale` (per the handoff §2.2), so a **lone**-stale trades stream (book + ticker still fresh) does **not** trigger the per-run REST fallback → `WsMarketDataSource.GetRecentTradesAsync` returns `Nothing` → the existing skip-gate **skips the run**. That contradicts proposal §3's "a WS problem never costs a row." **P3 decision needed**, options: (a) make `IsDegraded()` "ANY required stream stale" (matches proposal §3, but then quiet markets fall back to REST often), (b) gate the **trades** stream on connection-health rather than last-trade-age (the buffer is valid when quiet — arguably the trades stream shouldn't have an age-staleness gate at all, unlike a quote), or (c) raise `ws_stale_after_sec` (blunt — also delays book/ticker fallback). I recommend (b) for P3: a complete-but-quiet trade buffer is valid data; per-stream staleness semantics belong with the cutover. **No P2 change** — `transport` stays `rest`, so this is observational only right now.

---

## 6. Status surface (commit 3)
A WS-health segment was added to the `UpdateLogInfo` LOG cascade (after `_ledgerWarn`/`cfgWarn`, before `Log: N rows`), rendering **only** when `_wsFeed IsNot Nothing` (feed active). Exact formats:
- `WS OK · 1/3/5/15 fresh · trades N · ` — connected, last frame within `ws_stale_after_sec`.
- `WS OK · streams Xs stale · trades N · ` — connected but last frame older than the threshold (quiet).
- `WS DEGRADED — REST fallback (stream stale) · ` — `_wsDegradedThisRun` (per-run fallback fired).
- `WS DOWN — reconnecting (Xs backoff, R reconnects) · ` — disconnected.
- Shadow mode appends `· parity NN/50` (the comparer's consecutive-pass counter).

Reads the feed's plain health fields on the UI thread (no `Control.Invoke`). **Screenshots:** the segment only renders with the live app running under `transport=ws`/`shadow_parity` + a live feed, which a headless session can't drive; the exact rendered strings are above. The trader can screenshot during the live gate run (delete after, per the repo convention).

---

## 7. Auto-tweaker network.* hardening (commit 4)
- **Code-level (load-bearing):** `"network."` added to `SettingsDiffApplier.Validate`'s `RejectedPathPrefixes` (NOT `ValidateSnapshotContent` — a wholesale revert may legitimately restore these). Any `network.*` diff fails `Validate` and never applies. Reject message generalised to "HARD CONSTRAINT 11/12".
- **Prompt-level:** PromptBuilder **HARD CONSTRAINT 12** — never propose `network.*` (transport plumbing; no failure-rate linkage).
- **Harness:** OrderCheck **A15h** — `network.transport` + `network.ws_url` diffs rejected by `Validate`; a legitimate scoring key (`indicators.OBV.trend_gate`) still passes. (A15h lives in the gitignored `verify/ordercheck` harness — working-tree only, the established pattern; the harness output below is the evidence.)

---

## 8. Resilience drills (proposal §7 / handoff §5)
- **Per-run REST fallback (`transport=ws` + feed down):** proven by construction — `IsDegraded()` true when `Not _connected` → `ResolveSource` returns `_restSource`, the run completes on REST, status shows `WS DEGRADED`, and the CSV row is byte-identical to a REST run (RestMarketDataSource pass-through). Trader confirms live.
- **Forced-reconnect:** the reconnect path (1→60s backoff + `>5/10min` storm cooldown + REST re-seed) was implemented + audited in P1 and **already passed the trader's live network-kill drill** (P1 spec-back §4, two cycles). The P2 health surface (`IsConnected`/`ReconnectCount`/`CurrentBackoffSec`) is now wired to the status line so the trader sees `WS DOWN — reconnecting (Xs backoff, R reconnects)` live.
- **24h soak:** trader-run (heartbeats answered, zero unintended drops, reconnect count sane). The P1 standalone soak already showed heartbeat replies every ~10s with zero drops over the run window.

---

## 9. Acceptance
- **Builds 0/0:** solution **Release** (main + AutoTweaker) `--no-incremental` 0W/0E; `verify/ordercheck` 0W/0E. (Release dodges the running-app Debug exe lock, per P1.)
- **Harness:** `OrderCheck` re-run → **A1–A15g unregressed + the new A15h — ALL PASS (39 checks)**. The routing change touches `RunAnalysisAsync` but no scoring path, so A1–A15g are unmoved as designed.
- **`transport=rest` byte-identical:** proven by construction (§3).
- **Shadow parity:** comparer live-validated (§4); the three watches characterized (no candle/ticker drift, no trade-boundary loss); two findings raised (§5). ≥50-consecutive gate = trader's active-session step.
- **Local commits only — NOT pushed.** Tracked edits: `Core/Settings/EngineSettings.vb`, `DeribitWsFeed.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_Layout.vb`, `settings.json`, `ShadowParityComparer.vb` (new), `tools/AutoTweaker/SettingsDiffApplier.vb`, `tools/AutoTweaker/PromptBuilder.vb`. (The pre-existing dirty docs `p3-maintenance-pass-proposal.md` / `ui-reskin-handover-2026-05-22.md` / `websocket-migration-p1-spec-back.md` are unrelated, from the session-open snapshot — excluded from every commit, same as P1 §3.4.)

---

## 10. Open items for coordinator / trader / P3
- **[coordinator]** Confirm the §5.1 book-tolerance deviation (5 ticks vs the handoff's literal 1 tick), the §5.2 trades-staleness finding (a P3 design call), and the `IsDegraded()` any-vs-all nuance (§5.2). Re-run builds + harness; audit the routing diff + the comparer. Add at commit (handoff §10.5, mirrors P1): `DeribitIndicatorProject.md` §15 v39 row + §6 version pointer → v39; `architecture.md` data-flow note (RunAnalysisAsync routes through `IMarketDataSource`; `ShadowParityComparer` in the directory layout).
- **[trader]** Run the ≥50-consecutive shadow-parity gate during an **active** session (`shadow_parity=true`, `transport=rest`, auto-run; watch `parity NN/50` + `ws_parity_log.txt`). Then the live 24h soak + a `transport=ws` per-run-fallback smoke. Then push.
- **[P3]** Cutover (`transport` default → `"ws"`) + the 15m-TTL collapse on the WS path, **gated on the data-gated re-baselines** (the first recalibration closes on a single-transport dataset). Fold in the §5.2 trades-staleness decision before flipping the default.

---

> ## Coordinator review — APPROVED (2026-06-19)
>
> **Builds (0/0):** main **Release** + AutoTweaker + OrderCheck. **Harness:** A1–A15g unregressed + the new **A15h — ALL PASS (39)**. The 6 P2 commits (`3431a26`→`debec5a`) are in; working tree clean except the 3 pre-existing dirty docs (unrelated, excluded — same as P1).
> **Source-verified the load-bearing claims (not just doc-checked):**
> - **Routing null-at-rest (§3):** `ResolveSource()` returns `_restSource` (the verified `DeribitClient` pass-through) at `transport≠"ws"`; all **8** fetch sites route through `src`; the parity hook is `_parityComparer IsNot Nothing`-guarded; the status segment returns `""` when the feed is `Nothing`. With the P2 defaults the feed never starts → behaviorally identical to v38. ✓
> - **Parity isolation (§4):** `ShadowParityComparer` writes only `ws_parity_log.txt` + `Console` — **zero** `analysis_log`/`LogRun`/`.csv`/scoring touch (grep-confirmed). ✓
> - **Hardening (§7):** `"network."` is in `SettingsDiffApplier.Validate`'s `RejectedPathPrefixes` (alongside `kelly.`/`resolution_profiles.`), in `Validate` not `ValidateSnapshotContent`; A15h proves `network.transport`/`ws_url` reject while a scoring key passes. ✓
> **Deviations — both accepted:**
> - **§5.1 book tolerance 1→5 ticks:** agreed. REST-snapshot-vs-in-memory-WS non-simultaneity legitimately moves top-of-book a few ticks over the HTTP round-trip; a real desync is orders-of-magnitude off, and the raw gap is still logged so it surfaces. Data-backed, conservative.
> - **§5.2 trades-staleness / `IsDegraded()` any-vs-all:** correctly a **P3** call, **no P2 change** (transport stays rest → observational). Coordinator recommendation recorded: option **(b)** — gate the trades stream on connection-health, not last-trade-age (a complete-but-quiet buffer is valid data, unlike a stale quote). Must be resolved before the P3 default flip so a quiet market can't cost a row (proposal §3).
> **Coordinator added at commit:** `DeribitIndicatorProject.md` §15 v39 row + §6 → v39; `architecture.md` `ShadowParityComparer` directory entry + the routing note.
> **Verdict: APPROVED — local commit.** Remaining (trader): the ≥50-consecutive shadow gate during an active session + the 24h soak + a `transport=ws` fallback smoke, then **push**. P3 stays gated on the re-baselines.
