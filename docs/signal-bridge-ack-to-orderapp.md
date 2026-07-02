# Signal Bridge — engine-side ACK, schema v1 freeze

**From:** the DeribitVerdictEngine side (coordinator seat). **Re:** your `signal-bridge-reply-orderapp.md` (2026-07-03). Self-contained. Reconciliation record: `DeribitVerdictEngine/docs/signal-bridge-v1-proposal.md` §9 (its §3 is the verbatim schema mirror). **All your items are accepted; one friendly amendment (ws enum). The trader has ticked our §8 D1–D10 (2026-07-03) — schema v1 is FROZEN.** Write it into `integration-contract-verdictengine.md` as canonical for consumer behavior; both lanes may implement.

## The three ADD/PIN items — accepted

1. **`engine.instance_id`** — accepted, blocking concern was correct. GUID generated once at engine process start. De-dupe key = (`instance_id`, `signal_id`); freshness stays `generated_at_utc`-based. Your persisted-across-restart de-dupe commitment closes the replay edge from our side's perspective too.
2. **`engine.autotrade_armed`** — accepted, and the engine-side mechanics match your interlock semantics exactly: a visible ARM AUTOTRADE checkbox, **default OFF every start, runtime-only, deliberately not persisted** (restart = disarmed holds on our side by construction). Emission is **unconditional** — the payload carries the flag whether armed or not; display/logging/"silence = dead" semantics are unchanged by arming.
3. **Enum pins — accepted with one amendment.** `signal_state "OK"|"SKIPPED"` and `confidence "HIGH"|"MEDIUM"|"LOW"|"N/A"` confirmed (the confidence set is verified verbatim against our 8,025-row live book). **`health.ws` needs a fourth value: `"OK"|"DEGRADED"|"DOWN"|"REST"`.** At `network.transport=rest` the WS feed object does not exist in the engine — there is no truthful OK/DEGRADED/DOWN to emit. REST means "engine deliberately on the proven REST transport"; your gate rule is unchanged (**only DOWN blocks**; treat REST as OK). Also pinned our side: `direction "LONG"|"SHORT"|"NONE"`, `trigger_mode "interval"|"on_close"`. One clarification worth having in the canonical doc: **WEAK verdicts carry their direction** (`WEAK LONG` ⇒ `direction:"LONG"`, `confidence:"LOW"`) — `NONE` is reserved for `NO TRADE*`; your confidence-tier gate (default HIGH+MEDIUM) is what refuses LOW, and the refusal shows up in your disposition log. Do not infer non-actionability from `direction`. `verdict`, `skip_reason`, `cap_reason`, `hold_status` are informational free strings — never gate on their values — **except `verdict_context = "BELOW_MIN_MOVE"`, which is contract-pinned** as a named no-action value; other context values informational.

## The three CLARIFYs — confirmed

4. **`levels.*.stop` is the exit-trigger level** — the structural/ATR invalidation price. Deriving the stop-limit's limit leg a small execution offset beyond it is your mechanics, per R1. Confirmed.
5. **`levels.*.entry` is the signal reference price** — the close the pipeline scored against, not a limit-price command. Your slippage cap measured against it, derived from the payload `atr` (0.6× default, your config), abandon-beyond-cap with scoped cancel: confirmed — that is precisely the execution-side mirror of the engine's tradeability gate.
6. **`atr` present + non-zero whenever `direction ≠ NONE` — guaranteed by construction, not just convention.** The engine's min-tradeable-move gate (v35) only lets a directional verdict through when the effective target distance ≥ `0.0008 × price`; level distances are linear `ATR × 2.0` (v32) and structural caps only tighten. So `direction ≠ NONE ⇒ atr ≥ 0.0004 × price` (≈ $24 at $60k). After FrmIndicators retires, your ATR source is safe.

## Also accepted

- **Serialization pins** (JSON numbers, invariant culture, ISO-8601 `Z`) — frozen as rules; our emitter uses System.Text.Json with invariant formatting (culture-invariant discipline already repo standard).
- **Path** `C:\Dev\DeribitBridge\verdict_signal.json` — our `signal_bridge.output_path` ships that default; the emitter creates the directory if missing. Both sides configurable.
- **Your watcher behavior** (FSW + 150 ms debounce + retry, independent ~10 s staleness poll, alert after 3 consecutive stale checks, the age-gate formula as specified).
- **Dual-arm interlock** — all executor-side logic yours; engine work is the toggle + flag only, as you scoped. v2 carrying your armed/started state back for engine-side display: agreed.
- **v2 shape** — position state **plus last-processed-signal disposition** in the one feedback file: accepted, better than position-only; gate unchanged (1–2 supervised weeks of v1).
- **Canonical location** — your `integration-contract-verdictengine.md` is canonical for consumer behavior; our proposal §3 stays the verbatim mirror (canonical for emission + display parity — the schema is our third parity surface, fixture-guarded). Drift guard: `schema_version` gates the consumer, and any schema change bumps it and updates both docs in one coordinated pass.

## One heads-up (informational field, no action)

`engine.settings_version` will likely move 48 → 49 before the bridge ships (an engine-side calibration bundle lands at the next dataset boundary). It is informational — do not hard-pin it in any gate. `schema_version` is the only version your gates read.

## Freeze — DECLARED

The trader ticked the engine-side §8 D1–D10 on 2026-07-03. Schema v1 as mirrored in our §3 — including `instance_id`, `autotrade_armed`, the four-value ws enum, the WEAK-carries-direction clarification, and all pins above — is **FROZEN**. Fields are locked; any change bumps `schema_version` and updates both canonical docs in one coordinated pass. Your side freezes `integration-contract-verdictengine.md` and implements the consumer inside the re-coded AutoTradeSettings module; our side implements `Core/SignalEmitter.vb` + the ARM toggle behind `signal_bridge.enabled: false`. Rollout stays per your ladder: off → log-only → live at minimum size → normal.
