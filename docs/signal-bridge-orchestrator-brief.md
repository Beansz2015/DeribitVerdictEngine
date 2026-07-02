# Signal Bridge — proposal to the DeribitOrderPlacementApp orchestrator

**From:** the DeribitVerdictEngine side (coordinator seat). **Purpose:** agree one signal contract between the two apps before either side implements. This is self-contained — no engine-repo context needed. The engine-side full spec is `DeribitVerdictEngine/docs/signal-bridge-v1-proposal.md`; this brief is the cross-project surface of it.

## What the engine will provide

The verdict engine analyses BTC-PERPETUAL on a 30 s–3 min cadence (event-driven on bar close, or interval) and emits a directional verdict with levels. Under this bridge it becomes the **sole signal source** for your autotrade path. Two rulings from the trader are fixed constraints:

- **R1 — replacement, not gating.** The engine's verdict replaces the order app's internal EMA/VWAP/DMI score entirely (those are a lagging subset of the engine's inputs; gating one with the other double-counts and blocks early entries). The order app keeps **execution policy**: cooloff, tier selection, sizing, order mechanics, kill switches.
- **R2 — engine levels are authoritative.** The order app places the engine's **final effective stop/target levels** (they embed structural caps — swing/HVN — and the engine's tradeability gate passed *because of* them). The app's own ATR-based distance math becomes a **default-OFF checkbox override**; when ON it may recompute distances only, never resurrect a verdict the engine suppressed, and both level sets get logged per trade.

## Proposed transport

One JSON file, overwritten after **every** engine run with an atomic write (temp file + `File.Replace` — a reader never sees a torn file): `verdict_signal.json`, path configurable on both sides. Consumer watches it (`FileSystemWatcher` + ~150 ms debounce + one retry on share-violation, since Windows watchers double-fire). Rationale: at this cadence a push transport buys nothing; a file is inspectable, replayable, and restart-proof. The schema is versioned so the transport can evolve later without renegotiation.

## Proposed schema (v1)

```json
{
  "schema_version": 1,
  "signal_id": 1234,                      // monotonic per engine process — de-dupe on this
  "generated_at_utc": "2026-07-03T14:31:02Z",
  "engine": { "settings_version": 48, "app": "DeribitVerdictEngine" },
  "instrument": "BTC-PERPETUAL",
  "signal_state": "OK",                   // OK | SKIPPED (engine ran but data was unusable)
  "skip_reason": null,

  "verdict": "STRONG SHORT",              // engine display string, incl. "NO TRADE [WEAK LONG]" leans
  "confidence": "HIGH",                   // HIGH | MEDIUM | LOW | N/A  ← the action key
  "direction": "SHORT",                   // LONG | SHORT | NONE. NONE on ALL "NO TRADE*" verdicts —
                                          // leans are logging context, never actionable
  "verdict_context": "CONFIRMED",         // engine caveat tag; BELOW_MIN_MOVE = untradeably small move
  "mtf_blocked": false,
  "scores": { "long": 4, "short": 13, "eff_long": 4, "eff_short": 13, "max": 20 },
                                          // for logging/analytics ONLY — never threshold on raw scores
                                          // (the ceiling "max" varies by market regime)

  "price": 59012.5,
  "exec_resolution_min": 1,               // engine execution bar (1 = NY session, 3 = Asia/London)
  "trigger_mode": "on_close",             // feeds your max-age gate
  "atr": 41.3,                            // execution-resolution ATR (for the override math + logging)

  "levels": {                             // FINAL EFFECTIVE levels — place these as-is (R2)
    "long":  { "entry": 59012.5, "stop": 58962.9, "target": 59095.1,
               "target_capped": true, "cap_reason": "SWING_HIGH_5M", "raw_target": 59095.1 },
    "short": { "entry": 59012.5, "stop": 59062.1, "target": 58929.9,
               "target_capped": false, "cap_reason": null, "raw_target": 58929.9 }
  },
  "structural": { "swing_target_long": 59095.0, "swing_stop_long": 58860.0,
                  "swing_target_short": 58860.0, "swing_stop_short": 59095.0 },  // 0 = unset; informational in v1

  "hold_status": null,                    // informational in v1 (see v2 note)
  "kelly": { "contracts": 2, "risk_usd": 32.5, "lev_capped": false },            // advisory display only — do not size from it in v1

  "health": { "ws": "OK", "degraded_this_run": false, "ledger_mismatch": false }
}
```

## Consumer-side obligations (what the engine side needs you to enforce)

1. **De-dupe on `signal_id`** — never act twice on one signal.
2. **Max-age gate:** reject if `now − generated_at_utc > 2.5 × max(exec_resolution_min, 1)` minutes. A stale file means the engine is down ⇒ stand down (and alert after N consecutive stale checks). A `SKIPPED` payload also means stand down — it is not "hold the last signal".
3. **Schema gate:** `schema_version ≠ agreed` ⇒ no action + alert.
4. **Action mapping:** enter only on `signal_state OK` AND `direction ≠ NONE` AND configured confidence tiers (recommended default: HIGH + MEDIUM) AND `mtf_blocked false` AND `verdict_context ≠ BELOW_MIN_MOVE` AND `health.ledger_mismatch false` AND `health.ws ≠ DOWN`. Everything else = no entry. Cooloff/sizing/position limits are yours.
5. **Level placement:** `levels.<direction>.stop / target` as-is; the ATR-override checkbox per R2.
6. **Rollout:** ship with autotrade in **log-only mode** first — log would-be orders against live payloads for a few sessions, then go live at minimum size.

## What the engine side guarantees

Emit after every run including skips (so silence = engine dead, unambiguous); atomic writes; values byte-equal to what the engine's own display shows for the same run (it's a parity-checked surface with test fixtures); `direction: NONE` on every non-actionable verdict; no order placement of any kind on the engine side.

## Deliberately out of scope in v1 → planned v2

Position-state **feedback** (order app → engine, so the engine's hold/exit logic knows the real position), which then makes `hold_status` and the engine's realtime exit-guard **actionable exit signals** in this contract; slippage-aware signal pricing. v2 is gated on 1–2 supervised weeks of v1.

## How to converge

Reply with a field-level diff against the schema above (adds / renames / removals / type changes) plus your preferred file path + polling expectations. We reconcile once, freeze **schema v1**, and only then does either side implement. The semantic rules worth treating as fixed (they encode the trader's rulings): `direction: NONE` on NO TRADE leans, effective-levels-as-placed-values, SKIPPED-means-stand-down, de-dupe on `signal_id`, tier-based (never raw-score) action mapping.
