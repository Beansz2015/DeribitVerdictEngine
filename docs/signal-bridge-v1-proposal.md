# Signal Bridge v1 — verdict_signal.json contract (proposal)

**Status:** PROPOSED — trader signs off §8, then implementation is **Opus-tier** on both sides (engine emitter in this repo; consumer in `C:\Users\user\source\repos\DeribitOrderPlacementApp`). Coordinator review per house convention.
**Objective:** roadmap **O2/W3** — the engine's verdict/score/direction/ATR feeds the order app's autotrade. Binding rulings **R1/R2** (roadmap W3, 2026-07-02): the engine **replaces** `FrmIndicators`' home-grown score entirely; the engine's **effective post-cap levels** are authoritative, the order-app ATR is a default-OFF override.
**Design invariants:** the human display stays the engine's primary output — this contract is an *additional* surface and joins the display-parity rule as the **third surface** (snapshot ↔ cards ↔ signal file carry equal values). The engine **never places orders**; it emits information, the order app owns every execution decision.

## 1. Transport

Per-run **atomic-write JSON file**: write to `<path>.tmp`, then `File.Replace` (the repo's existing pattern — `SettingsLoader.AtomicWriteAllText` is the model). One file, overwritten every run (the CSV remains the history; the file is "current state"). Consumer watches with `FileSystemWatcher` + ~150 ms debounce + retry-once-on-share-violation read (writer holds the file only for the rename instant, but Windows watchers double-fire).

Why not pipe/HTTP: run cadence is 30 s–3 min; sub-second push buys nothing, a file is inspectable/replayable/restart-proof, and both sides already have the machinery. Transport can evolve later without changing the schema.

## 2. Emission semantics

Emitted after **every** completed `RunAnalysisAsync`, on both paths:

- **Successful run:** full payload (§3), written **after** `BuildPlaintextSnapshot` + card binds (so the values are the rendered values — parity) — concretely, next to the `_lastSuccessful*` capture at the end of the run.
- **Skipped run:** a reduced payload — `signal_state: "SKIPPED"`, `skip_reason`, timestamps, health block. The consumer must treat SKIPPED as *stand down + previous signal now stale*, never as "hold last signal fresh".
- **Never on error paths mid-run** (exception before scoring ⇒ no write; the consumer's max-age gate covers engine death — that's the reason NO file update also means stand down).

Emitter must **never throw into the run** (try/catch, console log — same discipline as `AnalysisOutputDump`).

## 3. Schema v1 (`verdict_signal.json`)

```json
{
  "schema_version": 1,
  "signal_id": 1234,                      // monotonic per engine process; consumer de-dupes on it
  "generated_at_utc": "2026-07-03T14:31:02Z",
  "engine": { "settings_version": 47, "app": "DeribitVerdictEngine" },
  "instrument": "BTC-PERPETUAL",
  "signal_state": "OK",                   // OK | SKIPPED
  "skip_reason": null,

  "verdict": "STRONG SHORT",              // exact engine string incl. NO TRADE lean tags
  "confidence": "HIGH",                   // HIGH | MEDIUM | LOW | N/A  ← the R1 action key
  "direction": "SHORT",                   // LONG | SHORT | NONE (dominant side; NONE on tie/no-trade)
  "verdict_context": "CONFIRMED",         // incl. BELOW_MIN_MOVE
  "mtf_blocked": false,
  "scores": { "long": 4, "short": 13, "eff_long": 4, "eff_short": 13, "max": 20 },  // logging only — NOT for action thresholds

  "price": 59012.5,
  "exec_resolution_min": 1,
  "trigger_mode": "interval",             // interval | on_close — feeds the consumer max-age gate
  "atr": 41.3,

  "levels": {                             // R2: FINAL EFFECTIVE levels — place these, don't recompute
    "long":  { "entry": 59012.5, "stop": 58962.9, "target": 59095.1, "target_capped": true,  "cap_reason": "SWING_HIGH_5M", "raw_target": 59095.1 },
    "short": { "entry": 59012.5, "stop": 59062.1, "target": 58929.9, "target_capped": false, "cap_reason": null,            "raw_target": 58929.9 }
  },
  "structural": { "swing_target_long": 59095.0, "swing_stop_long": 58860.0,
                  "swing_target_short": 58860.0, "swing_stop_short": 59095.0 },   // 0 = unset; informational in v1

  "hold_status": null,                    // populated only when posState declared; INFORMATIONAL in v1 (v2 makes it actionable)
  "kelly": { "contracts": 2, "risk_usd": 32.5, "lev_capped": false },             // ADVISORY display context only

  "health": { "ws": "OK", "degraded_this_run": false, "ledger_mismatch": false }
}
```

Field sourcing rule (the parity anchor): every value comes from the **same `VerdictResult`/`IndicatorResults` fields the snapshot renders** — `levels.*.target` = `Adjusted*Target` when > 0 else the raw ATR target, mirroring the snapshot's ATR-block logic including the **sub-tick cap-noise suppression** (`|raw − adjusted| < max(0.5, ATR × 0.02)` renders uncapped → the file reports `target_capped:false` in that case, matching the display).

## 4. Engine-side implementation

- New host-agnostic **`Core/SignalEmitter.vb`**: `Build(v, r, cfg, health…) → SignalPayload` (pure, fixture-testable) + `Write(payload, path)` (atomic). WinForms glue = two call sites in `RunAnalysisAsync` (success + skip). No `MainForm` coupling inside the emitter — CLI-port-ready.
- New settings block `signal_bridge { "enabled": false, "output_path": "" }` (empty path ⇒ `verdict_signal.json` beside the exe). **Default OFF** — flipping on is the trader's dated action once the consumer exists. Version bump (one key add), change_log, §15 row.
- **OFF the tweaker surface:** `"signal_bridge."` → `SettingsDiffApplier.RejectedPathPrefixes` + PromptBuilder **HARD CONSTRAINT 18** (transport plumbing, no failure-rate linkage — `network.*` class).
- Harness: **A22-series** — (a) payload built from a fixed `VerdictResult`/`IndicatorResults` fixture serialises to the expected JSON (field-by-field, incl. the capped/uncapped and cap-noise-suppressed target cases); (b) SKIPPED payload shape; (c) NO TRADE lean-tag row → `direction: "NONE"`… note: `NO TRADE [WEAK SHORT]` still yields `direction` from the dominant side? **No** — `direction` mirrors the verdict's actionability: any `NO TRADE*` verdict ⇒ `direction: "NONE"` (the lean is visible in `verdict` for logging; the consumer must not act on leans).
- **CSV/display untouched.** No card/snapshot line changes → parity rule satisfied by construction (the fixture in (a) is the standing third-surface guard).

## 5. Consumer-side requirements (order-app repo; same spec governs)

1. Watch + debounce + parse; **schema_version ≠ 1 ⇒ no action + surface an alert**.
2. **De-dupe on `signal_id`** (never act twice on one signal).
3. **Max-age gate:** reject if `now − generated_at_utc > 2.5 × max(exec_resolution_min, 1)` minutes (covers both interval and on-close cadences). A stale file ⇒ stand down state, alert after N consecutive stale checks.
4. **Action mapping (R1):** act only on `signal_state OK` + `direction ≠ NONE` + configured confidence tiers (default HIGH + MEDIUM, per profile §6). `NO TRADE`, `BELOW_MIN_MOVE` context, `mtf_blocked`, `SKIPPED`, `ledger_mismatch:true`, `ws: DOWN` ⇒ no entry. Cooloff/sizing/position limits stay order-app config.
5. **Level placement (R2):** place `levels.<direction>.stop/target` as-is. The ATR-override checkbox (default OFF) recomputes distances from the payload's `atr` with the app's multipliers — it never overrides `direction`/tier gates, and when ON both level sets are logged per trade.
6. Replace the `FrmIndicators` score path per R1: `ProcessAutomatedSignal`'s scoring inputs swap to the bridge payload; the legacy indicator display can stay visible during the supervised period (shadow, never gating), then be decommissioned.
7. **Supervised rollout:** consumer ships with autotrade in **log-only mode** first (log the would-be orders against the payload for ≥ a few sessions), then live with minimum size.

## 6. Explicitly out of scope (v1)

Position-state feedback to the engine (v2 — gated on 1–2 supervised weeks of v1), actionable exit signals (`hold_status` is informational until v2's feedback loop makes `posState` truthful), Kelly-driven sizing (advisory field only), network transports, multi-instrument.

## 7. Acceptance

Engine side: 3 Release builds 0/0; A1–A21b unregressed + A22a–c; live smoke — enable on the bin copy, confirm the file updates every run (OK + a forced-skip case), values eyeball-match the rendered card for the same run. Consumer side (its repo): mock-file unit path + the log-only session. Trader tests + pushes; the `enabled` flip is a dated, deliberate action after the consumer's log-only validation.

## 8. Sign-off decisions (trader)

| # | Decision | Recommendation |
|---|---|---|
| D1 | File transport (atomic JSON + watcher) for v1 | **Yes** — cadence makes push transports pure complexity |
| D2 | `direction: NONE` on ALL `NO TRADE*` verdicts (leans never actionable) | **Yes** — leans are calibration/display info; acting on them violates the conservative bias |
| D3 | Default `enabled: false`; trader flips after consumer log-only validation | **Yes** — mirrors the v42 transport-flip discipline |
| D4 | Consumer default tiers = HIGH + MEDIUM | **Yes** (profile §6); configurable order-app-side |
| D5 | Emit Kelly as advisory context (never consumed for sizing in v1) | **Yes** |
