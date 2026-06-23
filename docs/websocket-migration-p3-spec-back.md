# WebSocket Migration — P3 Spec-Back (cutover build: 15m-TTL collapse + parity volume tolerance + A16 harness)

**Seat:** Opus implementer. **Date:** 2026-06-24. **Routes to:** coordinator review (re-run the three Release builds + the harness, audit the diff, confirm the `transport=rest` byte-identical proof) → local commit confirmation. **Local-first — NOT pushed; the trader tests, flips, and pushes.**
**Parent:** `docs/websocket-migration-proposal.md` §7/§8. **Build spec:** `docs/websocket-migration-p3-cutover-spec.md` (READY FOR IMPLEMENTER; G1+G2 MET 2026-06-24). **Predecessors:** P1 (`9cde370`, pushed), P2 (`docs/websocket-migration-p2-spec-back.md`, local), P3 §3 trades-gate (`808f510`, shipped early as the G1 prerequisite).
**Status:** BUILT + harness-green (A1–A16e), three Release builds 0/0, **NOT pushed.**

> **Coordinator review — APPROVED (2026-06-24, sanity-check seat).** Independently re-verified:
> - **Builds 0/0** — solution + AutoTweaker + OrderCheck, all Release.
> - **Harness A1–A15h unregressed + A16a–A16e new — ALL PASS (43 fixtures).** A16e is the byte-identical-at-rest proof (REST 15m retains the TTL); A16a/b/c lock the §3 connection-health trades gate (served-when-quiet / withheld-when-down / legacy age-gate preserved).
> - **Diff audited line-by-line.** Scope contained to `MtfRefreshPolicy.vb` (new) + `ShadowParityComparer.vb` + `MainForm_Analysis.vb` — **Core / AutoTweaker / analysis / settings.json all untouched** (`git diff --name-only` confirmed). §4's REST arm reduces *exactly* to the pre-P3 inline gate (`(Not (x IsNot Nothing)) OrElse s>=ttl` ≡ `x Is Nothing OrElse s>=ttl`); `ResolveSource()` untouched → null-at-rest guard intact. §1#5 widens only the **Volume** term's tolerance (`0.0001`→`ClosedBarVolumeRelTol` 0.05 const); OHLC stays `PriceEpsilon`-exact, so a real desync still trips; the comparer is `shadow_parity`-only, off the CSV/scoring path.
> - **Accepted the §4 harness-linking reconciliation** — my "WS classes aren't harness-compiled" constraint scoped the *feed + integrated path*; §6(a)'s stub `healthCheck` test legitimately requires compiling `WsMarketDataSource` (+ `MarketState`), which the §3 `Func(Of Boolean)` delegate was added precisely to enable. The feed (`DeribitWsFeed`) correctly stays out (live-gate-validated). Sound.
> - **No §15 version entry** — agreed: the live `transport=rest` path is byte-identical, so there is no engine behaviour to version; the dated §15 marker is the trader's, on the flip. Added the `MtfRefreshPolicy.vb` line to `architecture.md`'s directory map at this review.
> - **Display-string parity rule:** no rendered line changed (`mtfStale` is control flow; the comparer logs to a side file). Satisfied.
> - **Edge case (transport=ws but feed not started)** documented in §2 is benign and correct — `ResolveSource()` returns `_restSource`, so 15m is fetched from REST every run (a few extra HTTP calls, same data) until the §5 restart. No concern.
>
> **Remaining (trader, live):** re-run the ≥50-run §7 parity gate (now with the §3 trades fix + the 5% volume tolerance — the 3-min volume resets should be gone), reconnect/fallback re-confirm if desired, then the dated `transport=ws` flip (§5). Local-first — not pushed.

---

## 1. Scope built (final — §3 already shipped via `808f510`)

The P3 cutover is the **semantics-neutral** flip-enabler. This build delivers the three remaining pieces so the trader can later set `network.transport: "ws"` as a deliberate, dated, reversible edit. **`transport` is NOT flipped here** (the POCO default stays `"rest"`; the flip is §5, the trader's action).

| § | Piece | Surface | Live-path impact |
|---|---|---|---|
| §4 | 15m-TTL collapse on the WS path | `MtfRefreshPolicy.vb` (new, host-agnostic) + `MainForm_Analysis` | **WS-path-only.** `transport=rest` byte-identical. |
| §1 #5 | Closed-bar relative volume tolerance | `ShadowParityComparer.vb` | **Observational instrument only** — runs under `shadow_parity`; never touches CSV/scoring/verdict. Zero dataset impact. |
| §6 | A16-series harness checks | `verify/ordercheck` (gitignored, local) + the WS source classes linked in | Test-only. |

**Risk posture held:** `network.transport` stays `"rest"`, `network.shadow_parity` stays `false` in `settings.json`. The live verdict still runs pure REST → the calibration dataset is **unaffected**. **No `settings.json` version bump** — the volume tolerance is a `Private Const` (mirrors `BookJitterTolUsd`/`MarkTolUsd`), not a config key, so no schema/change_log churn (consistent with the spec's no-rotation rule and the §1 #5 "follow whatever's there" instruction).

---

## 2. §4 — 15m-TTL collapse (WS-path-only)

**Change.** `MainForm_Analysis.RunAnalysisAsync` computed `mtfStale` inline:

```vb
Dim mtfStale As Boolean = _mtfCandles15m Is Nothing OrElse
                          (DateTime.UtcNow - _mtfLastFetchTime).TotalSeconds >= MTF_TTL_SECONDS
```

It now routes through a pure host-agnostic predicate:

```vb
Dim mtfStale As Boolean = MtfRefreshPolicy.ShouldRefresh(
                              cfg.Network.Transport,
                              _mtfCandles15m IsNot Nothing,
                              (DateTime.UtcNow - _mtfLastFetchTime).TotalSeconds,
                              MTF_TTL_SECONDS)
```

`MtfRefreshPolicy.ShouldRefresh`:
- `transport="ws"` → **always `True`** (read every run; 15m is in-memory from `MarketState`, zero API cost, the TTL — which only spares the REST HTTP call — buys nothing).
- `transport="rest"` → `(Not haveCached) OrElse secondsSinceLastFetch >= ttlSeconds` — **the identical expression** to the pre-P3 inline gate (`_mtfCandles15m IsNot Nothing` is the negation of the old `Is Nothing` term).

**Why a helper, not a one-liner.** §6 (b) requires a harness check for "WS-path 15m read-every-run vs REST-path TTL retained," but the live `RunAnalysisAsync` is WinForms-coupled and not harness-compiled. The decision is plain data, so I extracted it to a host-agnostic predicate the A16 harness exercises directly (A16d/A16e). The **branch + the `_mtfCandles15m`/`_mtfLastFetchTime` cache update stay host-side** in `MainForm_Analysis` exactly as §4 prescribes ("the branch lives in `MainForm_Analysis`"). The helper is the policy only.

**Semantics-neutrality.** On WS the MTF gate sees 15m up to ~60s fresher (current forming bar vs ≤60s-stale forming bar); the closed 15m bars are identical, and 15m DMI/ADX+EMA move slowly → negligible gate-flip rate (the §6 parity gate already requires closed-bar identity; the trader watches the MTF decision during the gate re-run).

**Edge interaction (benign, documented).** If `transport="ws"` is set but the feed never started (hot edit without restart — §5 says this needs a restart), `ResolveSource()` already returns `_restSource` (its `_wsSource Is Nothing` guard, untouched here). `mtfStale` is then `True` every run, so 15m is fetched from REST every run instead of TTL-cached — a few extra HTTP calls in a misconfigured/degraded state, same data, same semantics-neutrality (only the forming bar is fresher). This matches §4's literal "read 15m every run" on the WS transport and is strictly benign. **`ResolveSource()` is unchanged** — the null-at-rest safety property is fully preserved.

---

## 3. §1 #5 — closed-bar relative volume tolerance (parity instrument only)

`ShadowParityComparer.CandleEquals` already carried a relative volume band (`Math.Abs(a.Volume) * 0.0001`, 0.01%). The 12h soak (2026-06-23/24) found the WS `chart.trades` 3-min closed bar **systematically undercounts** Deribit's server-side REST candle by ~2.5% (OHLC exact; 78/78 non-equal cases ws-low) — a benign first/last-tick boundary-bucketing gap. The 0.01% band was far too tight, so this benign drift kept resetting the parity-gate streak.

**Change.** The magic `0.0001` becomes a named `Private Const ClosedBarVolumeRelTol As Double = 0.05` (5%) with an inline rationale block documenting the ~2.5% finding, the "immaterial to scoring in normal flow" judgment, and the one standing watch (a volume spike at the 3×-SMA-9 breakout-confirm boundary — `DeribitIndicatorProject.md` §12 / spec §7 decision (a)). 5% clears ~2.5% with margin while a real desync (orders of magnitude) still trips.

**Mechanism match.** Const, not config — mirrors the existing `BookJitterTolUsd`/`MarkTolUsd`/`OiRelTolerance` const tolerances in the same file. No `settings.json` bump. This comparer is dev/validation-only (runs under `shadow_parity`, `transport=rest`) and **never touches the CSV, scoring, or the verdict** — so this change has **zero engine/dataset impact** and does not affect the byte-identical-at-rest property of the live path.

---

## 4. §6 — A16 harness coverage

Added to `OrderCheck.vbproj`: the **stub-testable, host-agnostic** WS source classes (`IMarketDataSource`, `MarketState`, `WsMarketDataSource`) + `MtfRefreshPolicy`, so the §3 connection-health trades gate runs as the **real shipped code** (the harness philosophy: link real sources, no copies). The feed (`DeribitWsFeed`) + the integrated live path stay **out** — they need a live socket and are validated by the live shadow-parity gate.

> **Reconciliation of the "WS source classes aren't harness-compiled" hard constraint.** That line scopes the *feed + integrated live WS path* (un-unit-testable → live gate is the real validation). §6 (a) explicitly asks for a stub `healthCheck` test of `GetRecentTradesAsync`, which is only possible by compiling `WsMarketDataSource` + `MarketState` — and the §3 fix added the `Func(Of Boolean)` delegate *precisely* to make this class "host-agnostic and unit-testable with a stub." So the constraint's intent (don't pretend the live feed is unit-tested) holds, while §6's "harness covers the stub-testable logic" is satisfied by linking the two pure classes. `WsMarketDataSource` references the feed only through the delegate, so it compiles with no `DeribitWsFeed` dependency. Flagged here for the coordinator.

New fixtures (all green; tests pass `staleAfterSec:=10` explicitly so they never depend on `SettingsLoader.Initialise`):

| Fixture | Asserts |
|---|---|
| **A16a** | Connected-but-quiet: 300s-old buffer (past the 10s age-gate) + `healthCheck=True` → buffer **returned**. The exact case that reset the live parity streak pre-§3. |
| **A16b** | Connection down: **fresh** buffer + `healthCheck=False` → **`Nothing`** (whole-run REST fallback takes over). Proves connection-health, not age, governs WS trades. |
| **A16c** | Legacy path: **no** `healthCheck` (Nothing) → falls back to the original age-gate (old→`Nothing`, fresh→served), byte-identical to pre-§3. Guards the preserved legacy/test path. |
| **A16d** | §4 WS: `ShouldRefresh("ws", haveCached:=True, 0s, 60)` = `True` (reads every run even with a 0s-old cache; case-insensitive). |
| **A16e** | §4 REST (byte-identical-at-rest proof): `<TTL` skips, `>=TTL` refreshes, no-cache refreshes. |

---

## 5. Acceptance results (against spec §6)

- **Builds 0/0 — all three Release:** solution (`DeribitVerdictEngine` + `AutoTweaker`) and `verify/ordercheck` (OrderCheck). 0 warnings / 0 errors each.
- **Harness:** A1–A15h **unregressed** + A16a–A16e **new, all pass** → `ALL PASS`.
- **`transport=rest` byte-identical — proven by construction:**
  - §4: REST arm of `ShouldRefresh` is the identical TTL expression (A16e). `ResolveSource()` untouched → null-at-rest guard intact (feed not started → `_wsSource Is Nothing` → `_restSource`).
  - §1 #5: observational comparer, never on the scoring/CSV path; only active under `shadow_parity`.
  - No scoring/indicator/CSV/`settings.json` files touched. AutoTweaker / FailureRateMatrix / Core untouched.
- **No renderer / card-surface impact** (engine display-string parity hard rule): no text-renderer line (`BuildPlaintextSnapshot`) or `MainForm_Render_Cards` binding is added/removed/reformatted — `mtfStale` is control flow, `ShadowParityComparer` logs to a side file, the harness/helper are non-rendering. Stated in the commit messages.
- **Still trader-run (live):** the ≥50-run §7 parity gate re-run (now with the §3 trades fix + this volume tolerance), 24h soak, reconnect + fallback drills — then the dated `transport=ws` flip (§5).

---

## 6. Commits (local, NOT pushed)

| # | Scope | Files |
|---|---|---|
| 1 | §4 15m-TTL collapse on the WS path | `MtfRefreshPolicy.vb` (new), `UI/MainForm_Analysis.vb` |
| 2 | §1 #5 closed-bar relative volume tolerance | `ShadowParityComparer.vb` |
| 3 | P3 spec-back | `docs/websocket-migration-p3-spec-back.md` |

The A16 harness additions (`verify/ordercheck/Program.vb` + `OrderCheck.vbproj`) are **gitignored** (`verify/` is excluded — the local validation tool, same as A14/A15) and so are not in any commit; they run green on disk.

**No `DeribitIndicatorProject.md` §15 engine-version entry:** the live (`transport=rest`) path is byte-identical, so there is no engine behaviour change to version. The dated §15 / `change_log` **dataset-boundary marker is the trader's, on the flip** (spec §5) — not this build's.

---

## 7. For the coordinator

1. Re-run the three Release builds + `dotnet run -c Release --project verify/ordercheck` → expect 0/0 and `ALL PASS`.
2. Audit the diff: confirm §4 is WS-path-only (REST arm identical), `ResolveSource()` untouched, the volume tolerance is instrument-only.
3. Confirm the "WS classes not harness-compiled" reconciliation in §4 above is acceptable (link the two pure classes for the stub test; feed stays out).
4. On approval → the trader re-runs the ≥50-run parity gate (with the trades fix + volume tolerance), then performs the dated `transport=ws` flip.
