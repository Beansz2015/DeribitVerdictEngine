# Full Audit + Code Trace — 2026-07-02

**Auditor:** Fable 5 seat, per `docs/next-session-handover-2026-07-02.md` (the audit brief).
**Baseline:** origin/master `a00ac35` (local commits above it are docs-only). settings.json **v46**, live on WS.
**Gate:** `verify-gate.ps1 -Mode prepush` run **before** the audit — **GATE PASSED** (3 Release builds 0/0, harness A1–A20i all pass, display-parity + version guards OK). No code was changed during the audit, so the green baseline stands.

**Headline: the engine is sound.** The full single-run trace (WS fetch → indicators → scoring → render → CSV) checks out against every invariant in the brief's §7 checklist. The geometric OFI accumulator is mathematically correct, the scoring ledger accounting is airtight at all 22+3 emission sites, the trade-stream ascending contract holds at the source and every consumer, and the host-agnostic constraint is clean. Findings below: **2 low/medium settings-hygiene defects, 1 design-drift decision item, and a handful of nits** — nothing in the scoring math, the WS transport, or the data path.

---

## 1. Coverage

| Target (brief §6) | Files traced | Verdict |
|---|---|---|
| A. Scoring pipeline | `ScoringEngine_Calculate_Verdict/_Scoring/_Helpers/_Types/_Kelly.vb` | **CLEAN** |
| B. OFI geometric | `OfiAccumulator.vb`, `Indicators_OrderFlow.vb` (shared helpers), fold/reset sites in `DeribitWsFeed`/`MarketState`, averaged-path gate in `MainForm_Analysis` | **CLEAN** (math hand-verified, §4) |
| C. WS feed + transport | `DeribitWsFeed.vb`, `MarketState.vb`, `WsMarketDataSource.vb`, `RestMarketDataSource.vb`, `MtfRefreshPolicy.vb`, `ResolveSource()` | **CLEAN** |
| D. Indicators | `Indicators_Momentum/Volatility/OrderFlow/Structure.vb`, `DeribitClient.GetRecentTradesAsync` | **CLEAN** |
| E. Settings integrity | `settings.json` (tracked + bin) ↔ `EngineSettings.vb` (scripted key diff), `SettingsLoader.vb` | **2 findings** (§3 F1/F2) |
| F. Offline analysis + tweaker | `SettingsDiffApplier.vb` (full), `AnalysisRunner`/`AnalysisConstants`/`ForwardWindowJoiner`/`FailureRateMatrix` (targeted), `AnalysisLogger` timestamp/culture | **CLEAN** (F1 exposes a tweaker no-op path) |
| G. Display parity | `MainForm_PlaintextSnapshot.vb` (full) ↔ `MainForm_Render_Cards.vb` (binding spot-check of every past-drift surface) + the gate's parity check | **PASS** |
| H. Host-agnostic | Repo-wide grep for `System.Windows.Forms` / `Control.Invoke` / `Me.Invoke` / `Partial Class MainForm` | **PASS** — only `AnalysisReportForm` (allowed thin viewer), `AutoRunTimer` (the WinForms impl of the interface), and `UI/`+`Program.vb`+Designer |

Also traced end-to-end: `RunAnalysisAsync` spine, `ExitGuardEvaluator`, `LiveMicrostructureEvaluator`, `BarCloseDetector`, `ExecutionResolution`, `DynamicNorms`.

---

## 2. Invariant checklist (brief §7) — all 13 HOLD

1. **Thresholds in settings.json / no magic numbers** — holds on the *bin* copy; see F2 for an 8-key gap on the *tracked* copy. One dead key found (F1). One deliberate hardcode noted: the REGIME ANCHOR 3.0×ATR threshold (`MainForm_PlaintextSnapshot.vb:104`) is documented as lift-if-tuning-needed (§15 post-v30 entry) — not a violation.
2. **Additive scoring; regimeMax ceiling; `ceil(max × pct)` thresholds** — verified. `Threshold()` is `CInt(Math.Ceiling(maxScore * pct))`; every bonus site (OFI momentum, Pass 2b, Pass 2c, structure, funding boost/soften) caps at regimeMax; every penalty floors at 0.
3. **MTF hard veto, direction-aware** — verified at Step 4b (`_Verdict.vb:94–135`): dominant side from effective scores, tie → NONE, per-side flag consulted, BLOCK forces NO TRADE, one composed reason string in the three locked formats.
4. **Trade lists ascending; windows from END; no `Take(n)` on trades** — verified at the source (`DeribitClient.vb:285` `list.Reverse()`), the WS buffer (`MarketState` append-ascending, sources take `GetRange(Count−n, n)`), and every consumer (TFI/MicroCVD via `LastN`, CVD full-walk, evaluators mirror). Repo-wide `.Take(` sweep: hits are candle windows, book levels, and Wilder seed windows only.
5. **VerdictContext + Kelly display-only** — verified. Context is Step-5-post; Kelly's only call site is `BuildPlaintextSnapshot:144` (pre-card-bind ordering intact), never in `Calculate()`.
6. **Funding only Steps 3/3b** — verified; Step 2 has no funding vote; the "Funding (info)" breakdown row is False/False hits carrying the ls/ss delta.
7. **Pass 2b penalises only full OI conflict** — verified (`_Scoring.vb:497–513`): upgraded partials confirm-only; penalty arms test `oiLong`/`oiShort` (full) exclusively.
8. **MicroCVD net USD deltas, negatives valid** — verified.
9. **Session volume adjusts thresholds, not signals** — verified (`DynamicNorms.ApplySessionVolume` multiplies `VolHigh/MidThreshold` only).
10. **Dual-session VWAP 00:00 + 13:30 UTC** — verified (`GetSessionCandles`, cfg-driven).
11. **No rejected patterns** — verified. BBW awards only on directional BULL/BEAR_BUILDING; TRANSITIONAL penalty is the two-arm ADX-proximity scale (heavier 2 below `penalty_mid` 22.5, then 1 to 25); no fixed-% targets; the SC ledger guard (`CheckLedger` before all 4 returns) makes silent double-counting structurally impossible — I hand-walked all snapshot-pair regions in `RunScoringPipeline` and every mutation is attributed to exactly one row.
12. **Host-agnostic `analysis/`+`tools/`+WS core** — verified (coverage table row H).
13. **Display parity snapshot ↔ cards** — verified for every load-bearing binding, incl. the three historical drift casualties: `MTFGateReason` (cards :850/:2313), Kelly notional + `[LEV CAPPED]` (:1472–1474), "ATR ratio" relabel (:2184), plus the v36 EXEC tag (:956 mirrors the snapshot ATR header) and the OFI card reading `r.OFIRatio` (:3090) so the v46 averaged value flows with no card edit — exactly as the v46 entry claimed.

---

## 3. Findings

### F1 (LOW-MED) — `regime_gates.transitional_adx_penalty_low` is a dead key that the auto-tweaker can "tune" as a silent no-op

`transitional_adx_penalty_low: 20.0` exists in **both** settings.json copies and the POCO (`EngineSettings.vb:799`), but is read **nowhere** — the v31 **F8** fix made the first TRANSITIONAL penalty arm cover `[0, penalty_mid)` (`_Verdict.vb:70–75` reads only `TransitionalAdxPenaltyMid`/`High`), orphaning the 20.0 boundary.

**Failure scenario:** `regime_gates` is a failure-rate-relevant block and this key is on no reject list. A tweaker round proposes `regime_gates.transitional_adx_penalty_low: 20.0 → 15.0`; the path **resolves** (key exists in the JSON), old-value matches, Validate passes, Apply writes it, version bumps, the round is recorded APPLIED — and the engine's behaviour is unchanged. The next window then evaluates a no-op as if it were a tweak, corrupting failure-rate attribution. This is exactly the class the Tier-C C-6 fix targeted; it slips through because C-6 rejects *unresolvable* paths and this one resolves.

**Fix (needs trader sign-off — settings schema):** remove the key from both JSON copies and the POCO field, or (if keeping for future use) add it to a reject list. One-line either way.

### F2 (LOW) — tracked settings.json is missing 8 POCO-backed keys that the bin copy carries; the two copies present different tweaker surfaces

Scripted key diff (tracked settings.json ↔ `EngineSettings.vb`): zero silently-ignored JSON keys ✓, but 8 POCO fields have **no key in the tracked file** and ride defaults:

- `indicators.MicroCVD.decel_penalty` = 1 and `indicators.CVD.divergence_penalty` = 1 — both are **live Step-2 scoring penalties**
- the 6 `scoring.hold_*` CalcHoldStatus thresholds (`hold_roc_take_profit_long/short`, `hold_rsi_hold_long/short`, `hold_rsi_evaluate_long/short`)

Because `SettingsLoader.Save` serialises the **full POCO**, any operational UI save materialises all 8 into the file it writes — and the **bin copy (v46) already has them** (verified: `divergence_penalty` :311, `decel_penalty` :324, `hold_roc_take_profit_long` :386). Consequences:

- The tracked file no longer mirrors the engine's real config surface; a fresh deploy from git differs from the live bin until the first UI save.
- The tweaker surface is copy-dependent: on the bin copy (which the tweaker reads per v36 §10a) the two scoring penalties are proposable; against the tracked copy they'd be rejected as unresolvable. The v17 "every scoring-affecting parameter reachable through settings.json" claim currently holds only for the bin copy.

**Fix:** add the 8 keys to the tracked settings.json at their POCO-default values — zero behaviour change (whether that warrants a version bump is the trader's call; values are identical so I'd argue no, mirroring the "POCO defaults ride the next code commit" precedent in reverse).

### F3 (LOW — decision item, not a bug) — the exit guard evaluates snapshot OFI while the run evaluates the averaged OFI

Since v46, the full run's `r.OFISignal` on the healthy-WS path comes from the **geometric time-averaged** ratio, and `CalcHoldStatus` (the HOLD\EXIT row) consumes that. `ExitGuardEvaluator.Evaluate` (:89) still computes **snapshot** `CalcOFI` from the live book. The adverse *definitions* are shared (`ComputeFastExitPrimitives` — no drift there), but the OFI *input* now differs between the two consumers, so the EXIT GUARD strip and the HOLD\EXIT row can disagree on `OFI-adverse` for the same market state (a transient sweep flips the snapshot but not the average). The v43 guarantee — "same cfg params, same window, identical method; only fresher" — is no longer strictly true at the OFI input. The equivalent choice in `LiveMicrostructureEvaluator` was made deliberately and documented (v46 spec-back §3, "raw fresher readout"); the exit guard's wasn't addressed.

An argument for leaving it: an exit overlay *should* react to raw tape, and snapshot OFI is the more twitchy/conservative exit input. An argument for aligning it: one definition of the OFI signal during a hold, and the guard was specced as "identical method." Either is defensible — it should be a ruled decision (one line in the v46 spec-back or a small change wiring `MarketState.GetOfiAverage` into the guard).

### Nits (no action urgency)

- **N1** — `DynamicNorms.ApplySessionVolume` reads `DateTime.UtcNow.Hour` itself rather than the run's captured `utcHour`, so a run starting within milliseconds of an hour rollover can resolve `execRes` and the volume bucket from different hours (volume multiplier only, one run, self-corrects). This is the temporal cousin of the boundary drift the shared `MatchSessionBucket` was built to kill — threading `utcHour` through would close it.
- **N2** — `CalcHoldStatus` returns the `ROC < 0` "momentum break" EXIT *before* the OBV-divergence EXIT; the file-header comment and `DeribitIndicatorProject.md` §9 list OBV first. Both are EXITs, so this is message-selection only, and A17g pins the current behaviour as canonical. Fix the comment/doc, not the code.
- **N3** — with `mtf_gate.enabled: false` (non-default), a failing gate still composes the reason string as `MTF BLOCK [...]` (`_Verdict.vb:110–111` doesn't consult `Enabled`) while no block occurs. Display-only, unreachable at current config; `DisabledGatedPaths` prevents the tweaker from ever creating this state.
- **N4 (doc-rot)** — `CLAUDE.md` (UI table, data-flow) and `architecture.md` (directory tree, render pipeline) still present `MainForm_Render_Header.vb`/`MainForm_Render_Sections.vb` as live files; they were deleted in P5b (`BuildPlaintextSnapshot` + cards are the only surfaces — the parity rule's "until P5b" clause acknowledges it but the tables/diagrams don't). Also `DynamicNorms.Compute`'s parameter is still named `candles1m` though it receives `candlesExec` since v36 (comment-level).

---

## 4. OFI geometric math — hand verification (brief §6B)

`Core/OfiAccumulator.vb`, against the checklist:

- **Seed:** first fold sets `_emaLnRatio = ln(max(ratio, 1e-6))`, bid/ask seeded arithmetic, `updateCount=1`, coverage start stamped ✓.
- **Decay fold:** `alpha = 1 − exp(−dt/tau)` with `dt = max(0, Δms/1000)` (non-monotonic stamps can't produce negative or >1 alpha); `tau ≤ 0 → alpha = 1` full overwrite ✓. `_emaLnRatio += alpha·(ln(max(ratio,1e-6)) − _emaLnRatio)` — the standard irregular-interval EMA, converging to the sample under constant input and honouring the window under bursty arrivals ✓.
- **Read:** `Snapshot.Ratio = Exp(_emaLnRatio)` (geometric mean) ✓; `_emaBid`/`_emaAsk` stay arithmetic ✓ (the documented D1 cosmetic: averaged Ratio ≠ BidVol/AskVol).
- **Warmup:** `_hasState AndAlso updateCount ≥ 5 AndAlso CoverageSeconds ≥ minCoverageSec`, where coverage = last-fold − first-fold **stamps** (a stalled feed stops accruing; "now" is never used) ✓.
- **Reset:** clears all state; called from `SeedAsync` before `SubscribeAsync` on every (re)connect, so no fold for a connection precedes its reset ✓.
- **Clamp symmetry (the geometric-specific risk):** `ComputeOfiImbalance` caps the ratio to `[1/1000, 1000]` — exactly reciprocal, i.e. `±ln(1000)` symmetric in log space, so the geometric EMA inherits **no clamp bias**. Degenerate one-sided books land at the respective bound (bid-only → 1000, ask-only → 0.001); zero-total returns False → no fold ✓.
- **Wiring:** fold per book update in `DeribitWsFeed.FoldOfiAverage` (reads `SettingsLoader.Current` per fold — hot-reload honest; skips entirely when `averaging_enabled=false`); all accumulator access under MarketState's single SyncLock; the run-path gate is `AveragingEnabled AndAlso (src Is _wsSource) AndAlso HasWarmup` so REST-fallback runs and pre-warmup take snapshot `CalcOFI` — and `averaging_enabled=false` short-circuits to the snapshot path everywhere (the rollback), corroborated by A20a/b ✓.

Corroborated by harness A20c–i (steady-state, time-aware step 1.5500 at dt=tau, warmup, reset re-arm, geometric symmetry alternating 2.0/0.5 → ~1.0).

---

## 5. Trace notes (what was checked and held, per layer)

- **Spine** (`RunAnalysisAsync`): source resolution by transport with degraded-fallback; MTF policy (WS always / REST TTL byte-identical); skip-gate on any missing shape + exec/5m freshness; parity hook correctly inert at `transport=ws`; last-trade price from the END of the ascending list; funding dedup ring (S9) + delta from distinct samples; OI ring trimmed to 70 min with oldest-in-window deltas; `priceUp` deliberately on 15×1m bars (wall-clock match to the OI window); v46 OFI gate as in §4; swing bookkeeping direction-aware; **Kelly ordering dependency intact** (`BuildPlaintextSnapshot` populates `v.Kelly*` before `BindCardKelly`); CSV row written with `DateTime.UtcNow` (display `TIME:` line is local — the offline session bucketing and the UTC OHLC join are therefore consistent, verified `AnalysisRunner:90` + `ForwardWindowJoiner:129`).
- **Scoring:** Spec-C ledger hand-walk — every mutation (Step 2 votes, RSI-div/CVD-div/MicroCVD-decel/flat-stall/liq/spread penalties, Pass 2 upgrades, 2b, 2c, 2c-struct, Steps 3/3b with both clamps inside the funding capture) sits inside a pL/pS snapshot region attributed to exactly one breakdown row; the MTF rows carry 0/0; `CheckLedger` runs before all four returns.
- **WS feed:** heartbeat `test_request` answered outside the JsonDocument scope; storm guard counts connect-then-drop flaps only; `ApplyBook` constructs a **new** snapshot per update (so `MarketState.GetBook` returning the reference is race-free by replace-not-mutate); ticker preserves funding on absent field; `IsDegraded` = not-connected / cooling / ALL-of-book+trades+ticker stale; trades health-gate delegate with legacy age-gate fallback.
- **Offline/tweaker:** `HoldWindowsForResolution` routed through all five consumers with `FailureRateMatrix.Compute`'s `Optional resolution = 1` keeping the NY×1 tweaker byte-identical; `AnalysisRunner` partitions by shared `MatchSessionBucket` × logged `ExecResolution`; culture-invariant numeric writes; `SettingsDiffApplier` enforces the six reject prefixes + two exact matches (HC11 floor, HC16 averaging flag) + disabled-gate protection + stale-value + no-key-creation in both Validate and Apply, atomic writes throughout; the `Split(".")` resolver leaves array-nested per-session keys unreachable (by design).

---

## 6. Known-intentional items encountered and NOT flagged (brief §9)

OFI dominance 2.0/0.5 un-retuned (v47 data-gated); averaged `OFIRatio` ≠ `OFIBidVol/OFIAskVol` (D1 cosmetic); POC tier-3 geometric narrowness; STRONG verdicts with warning context tags; the three MTF reason formats; `HOLD \ EXIT` row suppressed when flat; WS 3-min closed-bar volume undercount (§12 watch); the 3 dirty docs + untracked non-engine files.

## 7. Recommended follow-ups (trader's call, roughly in order)

1. **F1** — kill or fence the dead `transitional_adx_penalty_low` key before the tweaker's first live fire on a >40% NY window (it's the only found path to a recorded-APPLIED no-op).
2. **F2** — sync the 8 missing keys into the tracked settings.json (default values, no behaviour change).
3. **F3** — rule the exit-guard OFI input (snapshot vs averaged) and record it in the v46 spec-back either way.
4. **N4** — CLAUDE.md/architecture.md render-pipeline doc-rot (fold into the next docs pass; P13 in §16.6 is adjacent).

No code changes were made in this audit. Baseline remains origin/master `a00ac35`, gate-green.
