# Signal Bridge — A2 go-ahead: the engine emitter is LIVE-READY

**From:** the DeribitVerdictEngine side (coordinator seat). **To:** the DeribitOrderPlacementApp orchestrator. **Re:** your frozen `integration-contract-verdictengine.md` (schema v1, 2026-07-03). Self-contained.

## Status on our side

The engine emitter (your A1 counterpart) is **implemented, coordinator-reviewed, live-smoke-tested, and pushed** (engine repo commit `23fd8b9`). It emits exactly the frozen schema v1 — field-by-field serialization fixtures (A22a–g) pin every enum, the three target-cap cases, the NO-TRADE-lean→`direction:"NONE"` mapping, and invariant-culture output under a hostile thread culture. Emission is currently **OFF** (`signal_bridge.enabled: false`) — the flip is the trader's dated action once your log-only consumer exists, per D3.

**Your lane (A2) is clear to implement** against your canonical doc: §4 gate chain, §5 placement mapping, §6 interlock, §7 rollout ladder.

## Implementation facts now concrete (build-verified, not just spec)

1. **File:** `C:\Dev\DeribitBridge\verdict_signal.json` — atomic tmp + `File.Replace`, the engine creates the directory. Written after **every** completed run (OK and SKIPPED), on the engine's 30 s–3 min cadence. Indented JSON (whitespace is not contractual), numbers as JSON numbers, ISO-8601 `Z`.
2. **`engine.settings_version` is already 50** and moves without notice (48→49→50 since the freeze — two bumps in one day). It is informational; `schema_version` is the only version your gates read. Do not hard-pin it.
3. **Two emitter behaviors to expect** (recorded deviations, contract-consistent):
   - `kelly` on a no-edge run emits `{"contracts":0, "risk_usd":0.0, "lev_capped":false}` — zeros, never null. Advisory-only in v1 regardless.
   - SKIPPED payloads always carry `health.ledger_mismatch: false` (no verdict exists on a skip; you stand down on SKIPPED anyway).
4. **`health.ws` precedence as implemented:** engine on REST transport → `"REST"`; per-run degradation → `"DEGRADED"`; feed missing/disconnected → `"DOWN"`; else `"OK"`. Only `DOWN` blocks, per contract.
5. **`signal_id` gaps are legal.** SKIPPED runs consume ids; a rare mid-run abort can burn one. Monotonicity within an `instance_id` is the guarantee; your persisted (`instance_id`, `signal_id`) de-dupe handles restarts as you committed.
6. **Join bonus for the soak review:** as of the engine's CSV v0.8, every logged run carries `InstanceId`/`SignalId` columns equal to the payload's identity fields — your disposition log (`acted / refused:<gate> / stale / skipped / duplicate`) joins row-for-row to the engine's book. The soak reviewers will use this; keep the disposition log faithful.

## Testing path

Develop against mock files first (your §7 mock-file unit path — the canonical §3 example is byte-representative). When you want live payloads for the log-only soak, ask the trader to flip `signal_bridge.enabled` on the engine's bin copy — the engine is live and collecting, so you'll get real per-run files immediately, including genuine SKIPPED and stale windows (the engine powers down overnight local, which conveniently exercises your staleness stand-down for free).

## One NEW cross-project constraint since the freeze (rollout ladder)

Your ladder stands: off → log-only (a few sessions) → live at minimum size → normal. **The step to live-at-minimum-size is now additionally gated on an engine-side geometry pass** ("placed-geometry structural-first" — approved on our side; it changes the *origin* of `levels.*` from ATR-derived to structural-first, ships after a derivation review). Nothing changes for you — schema, semantics, and your placement mapping are untouched (prices stay prices; `cap_reason` may gain new label values, which your contract already treats as informational) — but **do not step to live until the trader confirms that pass is live on the engine.** Log-only does not wait for it.

## Unchanged

Schema v1 exactly as your canonical doc records — no field, enum, or semantic changes; the interlock split (your three-action sequence, our ARM toggle emitting `engine.autotrade_armed` unconditionally) is built as agreed; v2 (feedback file) stays gated on the 1–2-week v1 soak.
