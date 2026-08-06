# Signal Bridge v1 — verdict_signal.json contract

**Status:** APPROVED — **schema v1 FROZEN** (trader ticked §8 D1–D10, 2026-07-03; reconciled against `DeribitOrderPlacementApp/docs/signal-bridge-reply-orderapp.md`, record §9). Both implementation lanes are open (Opus: emitter in this repo per §4/§7; consumer in `DeribitOrderPlacementApp` per §5 + their canonical doc). Deliver `signal-bridge-ack-to-orderapp.md` to the order-app orchestrator so they freeze the canonical copy. Any schema change from here bumps `schema_version` + updates both docs in one coordinated pass. WEAK actionability note: the trader confirmed the config-gate design (no hard floor) — WEAK carries direction + LOW and is refused by the default tier gate.
**Canonical-location rule (§9 item 8):** the frozen contract lives in `DeribitOrderPlacementApp/docs/integration-contract-verdictengine.md` (canonical for **consumer behavior** — the executing party); §3 below is the **verbatim schema mirror** and canonical for **emission + display parity**. Drift guard = `schema_version` (consumer refuses on mismatch) + any change updates both docs in one coordinated pass. **Mirror VERIFIED 2026-07-03:** the consumer-side canonical copy (frozen same day) was diffed field-by-field against §3 — schema, enum pins, guarantees, gate chain, and interlock all match; the only differences are non-binding (JSON key order, example values) or consumer-scope additions consistent with R1/R2 (TP-limit placement mapping, `duplicate` disposition, no-cancel-pending gate). Both implementation lanes have a verified green light.
**Objective:** roadmap **O2/W3**. Binding trader rulings **R1/R2** (roadmap W3, 2026-07-02) — accepted verbatim by the consumer side, with R1's execution-policy clause made explicit: operational gates (arming interlock, connection, rate-limit, flat-only, cooloff, circuit breaker, session window) may refuse an entry; they never re-gate signal *logic*.
**Design invariants:** the human display stays the engine's primary output — this contract is the **third parity surface** (snapshot ↔ cards ↔ signal file). The engine **never places orders**. The engine never suppresses information for the bridge (it emits regardless of arming; the consumer owns the decision to act).

## 1. Transport

Per-run **atomic-write JSON file**: write `<path>.tmp`, then `File.Replace` (the repo's existing pattern). One file, overwritten every run — the CSV remains history; the file is "current state". Consumer: `FileSystemWatcher` + ~150 ms debounce + retry-once-on-share-violation, **plus an independent ~10 s staleness poll** (FSW cannot see a dead engine — no writes ⇒ no events); stand-down alert after 3 consecutive stale checks. Agreed by both sides (the order-app side conceded its named-pipe preference; v2's feedback file provides the ack loop a pipe would have).

**Path:** default **`C:\Dev\DeribitBridge\verdict_signal.json`** (neutral folder outside both repos; agreed). Engine `signal_bridge.output_path` ships that value; empty ⇒ beside the exe (fallback semantic). The emitter creates the directory if missing (never-throw discipline). The consumer reads its path from its own `bridge.json`.

## 2. Emission semantics

Emitted after **every** completed `RunAnalysisAsync`, on both paths:

- **Successful run:** full payload (§3), written **after** `BuildPlaintextSnapshot` + card binds (values = rendered values — parity), next to the `_lastSuccessful*` capture.
- **Skipped run:** reduced payload — `signal_state: "SKIPPED"`, `skip_reason`, timestamps, `engine` + `health` blocks. SKIPPED = *stand down + previous signal now stale*, never "hold last signal".
- **Never on error paths mid-run** (exception before scoring ⇒ no write; the consumer's max-age gate covers engine death — silence = dead, unambiguous).
- **Emission is unconditional on arming** — `autotrade_armed` rides the payload; display/logging/"silence = dead" semantics are identical armed or not.

Emitter must **never throw into the run** (try/catch + console log — `AnalysisOutputDump` discipline).

## 3. Schema v1 — FROZEN 2026-07-03 (verbatim mirror of the canonical)

```json
{
  "schema_version": 1,
  "signal_id": 1234,
  "generated_at_utc": "2026-07-03T14:31:02Z",
  "engine": { "app": "DeribitVerdictEngine", "settings_version": 48,
              "instance_id": "b0e6c1a2-7f43-4c1e-9a77-3d2f8e5a9c10",
              "autotrade_armed": false },
  "instrument": "BTC-PERPETUAL",
  "signal_state": "OK",
  "skip_reason": null,

  "verdict": "STRONG SHORT",
  "confidence": "HIGH",
  "direction": "SHORT",
  "verdict_context": "CONFIRMED",
  "mtf_blocked": false,
  "scores": { "long": 4, "short": 13, "eff_long": 4, "eff_short": 13, "max": 20 },

  "price": 59012.5,
  "exec_resolution_min": 1,
  "trigger_mode": "interval",
  "atr": 41.3,

  "levels": {
    "long":  { "entry": 59012.5, "stop": 58962.9, "target": 59095.1, "target_capped": true,  "cap_reason": "SWING_HIGH_5M", "raw_target": 59095.1 },
    "short": { "entry": 59012.5, "stop": 59062.1, "target": 58929.9, "target_capped": false, "cap_reason": null,            "raw_target": 58929.9 }
  },
  "structural": { "swing_target_long": 59095.0, "swing_stop_long": 58860.0,
                  "swing_target_short": 58860.0, "swing_stop_short": 59095.0 },

  "hold_status": null,
  "kelly": { "contracts": 2, "risk_usd": 32.5, "lev_capped": false },

  "health": { "ws": "OK", "degraded_this_run": false, "ledger_mismatch": false }
}
```

**Pinned enums (exact strings, closed sets):**
- `signal_state`: `"OK" | "SKIPPED"`
- `direction`: `"LONG" | "SHORT" | "NONE"` — **NONE on ALL `NO TRADE*` verdicts** (leans live in `verdict` for logging, never actionable). **WEAK verdicts DO carry their direction** (`WEAK LONG` ⇒ `LONG` + `confidence:"LOW"`) — actionability is the confidence-tier gate's job (default HIGH+MEDIUM refuses LOW, logged `refused: tier`), not the direction field's. WEAK is an output band of the finished score, not a scoring input (trader-confirmed 2026-07-03)
- `confidence`: `"HIGH" | "MEDIUM" | "LOW" | "N/A"` (verified verbatim against 8,025 live rows) — the R1 action key
- `health.ws`: `"OK" | "DEGRADED" | "DOWN" | "REST"` — REST = engine deliberately on `network.transport=rest` (WS feed not running); consumer gates block **DOWN only** (REST is the proven fallback transport, treat as OK)
- `trigger_mode`: `"interval" | "on_close"`

**Informational free strings (never gate on their values, presence only):** `verdict`, `skip_reason`, `cap_reason`, `hold_status` — **except** `verdict_context = "BELOW_MIN_MOVE"`, which is contract-pinned as a named no-action value (consumer gate). Other `verdict_context` values are informational.

**[ADDENDUM 2026-07-21 — session-policy gate; normative copy = order-app `integration-contract-verdictengine.md` §4; relay via owner, neither seat writes cross-repo]:** `verdict_context` values are now **STABLE IDENTIFIERS**, not free text — the consumer's per-session policy matches them as opaque case-folded literals, fail-closed (a renamed tag would silently refuse every signal in that session, landing on the trader). Current closed set: `CONFIRMED · ALIGNED · FLOW_UNCONFIRMED · STRUCTURALLY_WEAK · MOMENTUM_FADING · BELOW_MIN_MOVE`. **Renaming/removing any value = a coordinated cross-repo pass. Adding values is free** (unmatched tags never fire a policy) but new tags must be listed in docs so the trader knows what is policy-targetable. Also re-affirmed by the addendum: the **tier↔confidence 1:1** (`STRONG x`⇒HIGH · bare `x`⇒MEDIUM · `WEAK x`⇒LOW · `NO TRADE*`⇒N/A) is the semantic bridge the policy rides on — any change to that mapping (new tier, non-1:1 tier, re-purposed confidence value) = coordinated pass + `schema_version` bump; and the consumer's session buckets derive from `generated_at_utc` using engine-identical UTC bounds (ASIA 00:00–07:59 · LONDON 08:00–12:59 · NY 13:00–23:59) — bucket changes are likewise coordinated. Pure display/wording changes to the `verdict` string do not affect the consumer (it never parses `verdict`).

**Serialization pins:** numbers as JSON numbers (never strings), invariant culture, `generated_at_utc` ISO-8601 UTC with `Z`. `structural` zeros = unset. `scores` are logging-only — never threshold on raw scores (regime-dependent ceiling `max`).

**Identity & de-dupe:** `instance_id` is stable per engine process (GUID at process start); `signal_id` monotonic within it. De-dupe key = **(`instance_id`, `signal_id`)**, persisted consumer-side across restarts. Freshness stays `generated_at_utc`-based.

**Guarantees:** `atr` present and non-zero whenever `direction ≠ NONE` — by construction: the v35 min-tradeable-move gate only lets a directional verdict through when the effective target distance ≥ `0.0008 × price`, distances are linear `ATR × 2.0` (v32 D2) and caps only tighten, so `direction ≠ NONE ⇒ atr ≥ 0.0004 × price` (≈ $24 at $60k). `levels.*.stop` = the **exit-trigger level** (structural/ATR invalidation price; the consumer derives its stop-limit's limit leg beyond it — its mechanics, per R1). `levels.*.entry` = the **signal reference price** (the close the pipeline scored against; not a limit-price command — consumer slippage cap measured against it, cap derived from payload `atr`, default 0.6×, consumer-side config).

Field sourcing rule (the parity anchor): every value comes from the **same `VerdictResult`/`IndicatorResults` fields the snapshot renders** — `levels.*.target` = `Adjusted*Target` when > 0 else the raw ATR target, mirroring the snapshot's ATR-block logic including the **sub-tick cap-noise suppression** (`|raw − adjusted| < max(0.5, ATR × 0.02)` renders uncapped → the file reports `target_capped:false`, matching the display).

## 4. Engine-side implementation

- New host-agnostic **`Core/SignalEmitter.vb`**: `Build(v, r, cfg, health…) → SignalPayload` (pure, fixture-testable) + `Write(payload, path)` (atomic, create-dir-if-missing). WinForms glue = two call sites in `RunAnalysisAsync` (success + skip). No `MainForm` coupling — CLI-port-ready.
- **`instance_id` / `signal_id` — a shared process-identity primitive, NOT emitter-private (2026-07-03 manifest addition):** one GUID minted at process start + one counter incremented once per completed run, hosted as a tiny host-agnostic primitive readable with the bridge disabled. Consumed twice: (a) the emitter's `engine.instance_id` + `signal_id`; (b) the CSV `InstanceId`/`SignalId` columns added at the #5 v0.8 rotation (launch attribution for the book + the soak-review join to the order-app disposition log). CSV `SignalId` ≡ payload `signal_id` per run by construction. SKIPPED runs emit a payload id but write no CSV row — the join is total CSV→payload, partial payload→CSV, by design.
- **ARM AUTOTRADE toggle (engine half of the dual-arm interlock, §9 item 2):** a visible checkbox (programmatic UI, TAPE-checkbox pattern; shown when the bridge is enabled), **default OFF every start, runtime-only — deliberately NOT persisted** to settings.json (interlock rule: restart = disarmed, both apps). State emitted as `engine.autotrade_armed` in every payload. No settings key, no version-bump contribution.
- New settings block `signal_bridge { "enabled": false, "output_path": "C:\\Dev\\DeribitBridge\\verdict_signal.json" }`. **Default OFF** — flipping on is the trader's dated action once the consumer exists. Version bump, change_log, §15 row.
- **OFF the tweaker surface:** `"signal_bridge."` → `SettingsDiffApplier.RejectedPathPrefixes` + PromptBuilder **HARD CONSTRAINT 18** (transport plumbing, `network.*` class).
- Harness **A22-series**: (a) payload from a fixed `VerdictResult`/`IndicatorResults` fixture serialises field-by-field (incl. capped/uncapped + cap-noise-suppressed target cases); (b) SKIPPED payload shape; (c) every `NO TRADE*` lean-tag fixture ⇒ `direction:"NONE"`; (d) enum-pin assertions (ws all four states, confidence, signal_state); (e) `autotrade_armed` reflects the toggle + `instance_id` stable across two Build calls; (f) invariant-culture serialization (numbers as numbers).
- **CSV/display untouched.** No card/snapshot line changes → parity rule satisfied by construction; fixture (a) is the standing third-surface guard.

## 5. Consumer-side requirements (order-app repo; frozen in their canonical doc)

1. Watch + debounce + retry + **independent ~10 s staleness poll**; `schema_version ≠ 1` ⇒ no action + alert.
2. **De-dupe on (`instance_id`, `signal_id`)**, persisted across order-app restarts.
3. **Max-age gate:** `now − generated_at_utc > 2.5 × max(exec_resolution_min, 1)` minutes ⇒ stand down; alert after 3 consecutive stale checks. SKIPPED ⇒ stand down.
4. **Action mapping (R1):** enter only on `signal_state OK` ∧ `direction ≠ NONE` ∧ configured confidence tiers (default HIGH + MEDIUM) ∧ `mtf_blocked false` ∧ `verdict_context ≠ BELOW_MIN_MOVE` ∧ `ledger_mismatch false` ∧ `ws ≠ DOWN` ∧ the dual-arm interlock (below) ∧ operational gates (cooloff, flat-only, circuit breaker, session window, rate-limit, connection).
5. **Level placement (R2):** `levels.<direction>.stop/target` as-is; ATR-override checkbox default OFF, distances only, dual-set logging when ON.
6. **Dual-arm interlock (trader requirement, 2026-07-03):** live entries require `engine.autotrade_armed = true` ∧ local ARM toggle ON ∧ START pressed. START is not sticky — any disarm/stand-down/breaker drops to STOPPED; resume = full sequence. Restart of either app = disarmed. Interlock gates **live mode only**; log-only mode runs un-armed (that's what makes supervised soak safe).
7. Every consumed signal logged with disposition — local log in v1, feedback file in v2. **Token set (consumer-confirmed 2026-07-06):** `acted` / `refused: <gate>` / `stale` / `skipped` / `duplicate`, extended by **`would-act: <side> @ entry, stop, target, size`** (log-only mode, in place of `acted`) and **`rejected: <reason>`** (API-level rejection *after* a passing gate chain — an ops signal, distinct from gate refusals). Log format `utc | instance_id | signal_id | verdict | confidence | direction | disposition` — row-for-row joinable to CSV v0.8 via (`InstanceId`,`SignalId`), confirmed by design. **Soak-review checklist additions:** (a) diff `would-act` entry/stop/target against the engine's `PlacedTarget*/PlacedStop*` columns — the fourth parity check, catching consumer-side parse/mapping drift; (b) tally `rejected:` separately from `refused:` — the former is exchange/ops health, never signal quality.
8. **Rollout ladder:** off → log-only (a few sessions) → live at minimum size → normal. Mode switch is consumer-side, independent of engine emissions.

## 6. Explicitly out of scope (v1)

~~Position-state feedback to the engine (**v2**)~~ — **SPECIFIED 2026-07-28, see §10** (the feedback file; phase 1 = display-only consumption, implementation queued). Actionable exit signals stay out of scope (`hold_status` informational until the phase-1 display soak proves `posState` truthful; actionability = a future signal-schema-v2 pinned field, §10.5). Kelly-driven sizing. Network transports. Multi-instrument.

## 7. Acceptance

Engine side: 3 Release builds 0/0; A1-series unregressed + A22a–f; live smoke — enable on the bin copy, confirm the file updates every run (OK + forced-skip), values eyeball-match the rendered card, ARM toggle flips `autotrade_armed` in the next payload. Consumer side (their repo): mock-file unit path + the log-only session. Trader tests + pushes; the `enabled` flip is a dated, deliberate action after the consumer's log-only validation.

## 8. Sign-off decisions — ALL TICKED by the trader 2026-07-03 (schema v1 frozen)

| # | Decision | Recommendation |
|---|---|---|
| D1 | File transport (atomic JSON + watcher + staleness poll) for v1 | **Yes** |
| D2 | `direction: NONE` on ALL `NO TRADE*` verdicts (leans never actionable) | **Yes** |
| D3 | Default `enabled: false`; trader flips after consumer log-only validation | **Yes** |
| D4 | Consumer default tiers = HIGH + MEDIUM | **Yes** (profile §6) |
| D5 | Kelly emitted as advisory context only (never sizing in v1) | **Yes** |
| D6 | ADD `engine.instance_id`; de-dupe key = (`instance_id`, `signal_id`) | **Yes** — kills the engine-restart replay/stale edge |
| D7 | ADD `engine.autotrade_armed` + engine ARM toggle: runtime-only, default OFF, never persisted, emitted unconditionally | **Yes** — the interlock's engine half; you already required the interlock on the order-app side |
| D8 | Enum pins incl. the `health.ws` **REST** amendment (4 values; only DOWN blocks) | **Yes** — REST is the engine's real fourth state; 3 values can't represent it |
| D9 | Neutral default path `C:\Dev\DeribitBridge\verdict_signal.json` (emitter creates the dir) | **Yes** |
| D10 | Canonical contract doc lives order-app-side; §3 here is the verbatim emission/parity mirror; `schema_version` + coordinated-update rule guard drift | **Yes** |

## 9. Reconciliation record (2026-07-03, against `signal-bridge-reply-orderapp.md`)

| Their item | Disposition |
|---|---|
| Transport + all five semantic rules + R1 (with explicit operational-gates clause) + R2 accepted | Converged — no change |
| 1. ADD `engine.instance_id` (blocking) | **Accepted** (D6) — GUID per process start |
| 1b. ADD `engine.autotrade_armed` (blocking; dual-arm interlock) | **Accepted** (D7) — runtime toggle, default OFF, not persisted, emission unconditional |
| 2. Pin `health.ws` / `signal_state` / `confidence` enums | **Accepted + amended** (D8): ws gains `"REST"` — at `transport=rest` the feed object doesn't exist, the engine cannot truthfully emit OK/DEGRADED/DOWN; REST passes their `≠ DOWN` gate unchanged. `confidence` pin verified verbatim against the 8,025-row live book |
| 3. Clarify `stop` = exit-trigger level | **Confirmed** — invalidation price level; limit-leg derivation is consumer mechanics |
| 4. Clarify `entry` = signal reference price; slippage cap vs `entry` from payload `atr` | **Confirmed** — exactly the execution-side mirror of the tradeability gate; 0.6× default is consumer config |
| 5. Clarify `atr` present + non-zero when `direction ≠ NONE` | **Confirmed with proof** — v35 min-move gate ⇒ `atr ≥ 0.0004 × price` (§3 Guarantees) |
| 6. Serialization pins | **Accepted** — System.Text.Json, invariant culture, numbers as numbers, ISO-8601 Z |
| Path `C:\Dev\DeribitBridge\` + FSW/timer/age-gate behavior | **Accepted** (D9) — emitter creates the dir if missing |
| Dual-arm interlock semantics (START not sticky, restart = disarmed, live-mode-only) | **Accepted** — consumer-enforced; engine work = toggle + flag only |
| Executor commitments 1–6 | **Acknowledged** — match §5; disposition logging welcomed (v2 moves it into the feedback file) |
| v2 = position state + last-signal disposition in one feedback file | **Accepted** — better than position-only; unchanged gate (1–2 supervised weeks) |
| Canonical doc on the order-app side | **Accepted with the mirror rule** (D10) |

One heads-up carried in the ack: `engine.settings_version` will move 48→49 before the bridge likely ships (the signal-health retune bundles at the #5 boundary) — it is informational; the consumer must not hard-pin it.

## 10. v2 feedback file — SPECIFIED 2026-07-28 (engine mirror; canonical for CONSUMPTION + DISPLAY)

**Exchange of record:** order-app `proposal-c1-v2-feedback-file.md` → engine `feedback-file-engine-reply-2026-07-28.md` (ACCEPTED + 3 refinements, all accepted in their ack) → order-app `ack-c1-v2-feedback-file.md` → trader tick T1–T8 (2026-07-28) → the coordinated pass: order-app contract §8 (their `f6bea0a`) + THIS mirror. **Canonicality mirrors v1, reversed:** their §8 is canonical for EMITTER behavior; this section is canonical for engine consumption + display. **The v1 signal schema and its `schema_version: 1` gate are UNTOUCHED.**

### 10.1 Schema — feedback v1 (the file's own counter, distinct from the signal schema)

```json
{
  "schema_version": 1,
  "feedback_id": 587,
  "generated_at_utc": "2026-08-05T09:14:02Z",
  "executor": { "instance_id": "3c1a…-guid", "app": "DeribitOrderPlacementApp",
                "mode": "LIVE", "armed": true, "started": true,
                "breaker_tripped": false, "ws": "OK" },
  "instrument": "BTC-PERPETUAL",
  "position": { "direction": "LONG", "size_usd": 250.0, "avg_entry": 59012.5,
                "working": { "stop": 58962.5, "target": 59095.0 } },
  "last_signal": { "instance_id": "9f0c…-guid", "signal_id": 1234,
                   "disposition": "acted", "at_utc": "2026-08-05T09:13:41Z" }
}
```

**Pinned enums:** `executor.mode` `"OFF"|"LOG_ONLY"|"LIVE"` · `executor.ws` `"OK"|"DOWN"` · `position.direction` `"LONG"|"SHORT"|"FLAT"`. **Enum tolerance (T8):** the engine gates only on pinned strings; unrecognised values render verbatim and take the conservative arm (unknown `mode` ≠ LIVE; unknown `direction` ⇒ manual fallback). Additive enum values = a coordinated docs note both sides, not a schema bump, never free drift.

**Semantics highlights (full binding text = their §8.4):** `position` is the account position as the order app's model sees it, **including owner-manual trades (T6, ticked knowingly)**; flat ⇒ zeros-never-null. `last_signal` is written ONCE per consumed payload (the disposition-cardinality freeze; chase aborts live in their host log). Fill-window gap: after `acted`, `position` stays FLAT until the chase fills — the engine never infers failure from that window. The `avg_entry` join is the slippage record; the day stacking enters executor policy, an explicit per-signal achieved-entry field becomes a v2.1 amendment FIRST (T7).

### 10.2 Transport + engine config

`C:\Dev\DeribitBridge\executor_feedback.json` beside the signal file; single writer = the order app; **atomic (temp + atomic replace)**; ~10 s heartbeat + writes on **(a)** every disposition / **(b)** position change / **(c)** **working-level reposition (engine refinement 3.1)** / **(d)** **ARM/START/mode/breaker/`ws` transitions** / **(e)** graceful close / **(f)** **an initial write at start when configured**.

> **Mirror corrections applied 2026-08-04** — relayed from the order-app seat, trader-ticked 2026-08-02 and 2026-08-03. **This subsection mirrors their canonical §8 for reference; it had drifted from it in two places. No schema, field, enum or semantic change, and no engine work implied.**
>
> 1. **`File.Replace` un-named.** It **throws when the destination does not exist** — precisely the first-write and OFF→ON cases. Naming the API pinned something that cannot serve the requirement unaided; atomicity was always the requirement, the API never was. *(Engine-side note, verified 2026-08-04: our own `SignalEmitter.TryWrite` does **not** have this defect — it guards with `If File.Exists(path) Then File.Replace(...) Else File.Move(...)`, so the first write is handled. The file-header comment describing it as "tmp + `File.Replace`" was corrected in the same pass for the same reason.)*
> 2. **`ws` joins the transition triggers, and (f) is new.** `executor.ws` maps to a *computed* property on their side, so no assignment site exists for a trigger to hook — without an explicit trigger a WebSocket drop publishes nothing and the file heartbeats `ws:"OK"` indefinitely. **This one reaches us:** §10.4's executor interlock display renders `ws`, so it would have shown a stale `OK` straight through a disconnect. And without (f), a fresh, configured, idle, flat executor writes no file at all — indistinguishable from the feature being off, which would have made **§10.3's "File absent = feature OFF, never an alarm"** ambiguous and the 35 s staleness rule unfounded. With (f): **file-absent means OFF, file-stale means dead.**
>
> **THE CONSEQUENCE THAT MAKES THE TRIGGER LIST LOAD-BEARING — take this, not the mechanics behind it.** Canonical text pinned in their **§8.5**; cite that for the mechanism rather than restating it here:
>
> > **A fresh `generated_at_utc` proves the executor PROCESS IS ALIVE. It does not prove any field was recently observed. Every field is as of its last TRIGGERING event.**
>
> So **trigger completeness is the only thing bounding any individual field's staleness**, and a field with no trigger lets a perfectly fresh file hide an arbitrarily old value. That is exactly what the missing `ws` trigger was: our §10.4 strip would have rendered a stale `OK` through an entire disconnect, with a fresh timestamp sitting next to it. **Freshness of the file is not freshness of the field** — anything we build against this payload must treat the two as separate questions.
>
> **⚠ STANDING RULE FOR THIS MIRROR (adopted 2026-08-05, order-app seat's proposal, agreed):**
>
> > **§10.2 carries consumption-visible CONSEQUENCES, never emitter MECHANICS.**
>
> Both corrections above were mechanics we had restated, and their mechanics moved. **Neither correction would have needed to exist under this rule** — which is why it is recorded here rather than in a commit message. Mechanics live in their §8 and are cited, not copied. If a future edit finds itself describing *how* the order app does something, that is the signal it belongs on their side with a pointer from ours. Engine key (at the consumption build): **`signal_bridge.feedback` `{ enabled: false, path, stale_after_sec: 35 }`** — rides the existing `signal_bridge.` tweaker fence, no new HARD CONSTRAINT.

### 10.3 Engine consumption (D6 shape, trader-ticked)

Per-run fresh read at `RunAnalysisAsync` start (no watcher). **Governs** when `enabled` ∧ present ∧ fresh (≤ `stale_after_sec`) ∧ `direction` parses: `LONG`/`SHORT`/`FLAT` → `PositionState` Long/Short/None. While governed the manual radios grey out with a source tooltip; stale/absent/disabled/unparseable ⇒ radios re-enable, today's manual behaviour returns unchanged. **File absent = feature OFF, never an alarm.** Staleness surfaces as `EXECUTOR STALE`.

### 10.4 Display surfaces — phase 1 is the live-status tier ONLY

Source tag (`POS:EXEC`/`POS:MANUAL`), executor interlock display (armed/started/mode/breaker/ws), stale tag — all on the exit-guard strip / status-bar tier (the display-parity exempt class, stated per standing rule 4). **NO snapshot line, NO card binding, NO CSV column, NO payload field changes** — the engine consumption build is rotation-free and boundary-free (display/plumbing, no ⚠).

> ⚠ **T8 AMENDMENT — an unrecognised enum value MUST render DISTINGUISHABLY, not merely verbatim (added 2026-08-05, from the order-app seat's runtime acceptance report).**
>
> Their run found **`mode` has only ever been observed as `"OFF"`** — `LOG_ONLY` and `LIVE` remain fixture evidence only — and E2's failure mode is precisely a wrong `mode` string. As specced, our tolerance would **render it verbatim and take the conservative arm without erroring**: safe, but *silent*. Their side would meanwhile be emitting what it believes is correct. **Neither repo is wrong, and that is exactly the problem — the blind spot belongs to the PAIR, and neither side can detect it alone.**
>
> **Requirement:** any enum value outside the known set renders with a **visible anomaly marker**, on the `[EST]` / `EXECUTOR STALE` precedent this tier already uses. **The conservative-arm behaviour does not change** — this turns a silent fallback into a conspicuous one, nothing more. Cheap now because the consumption build is unwritten; expensive later, because by then the silent path is the shipped path.
>
> **Fixture consequence:** §10.5's named `enum-tolerance` fixture gains a second arm — *unknown value ⇒ conservative arm **AND** a visible marker*. Pinning only the conservative arm pins half the requirement, and the half that is already true.
>
> **Runtime status this rests on (their report, 2026-08-05):** trigger (f) and the `ws` OK edge are now **observed** on testnet, so *"file absent = OFF, file stale = dead"* is founded rather than assumed. **Acceptance 3 is the only one outstanding** — it needs owner-only mode Live + ARM + START. Until it passes, treat the file as **partially proven**: shape, lifecycle and position semantics observed; **the disposition path, `last_signal` and the mode strings are not.**

### 10.5 Phase-2 fence (T5) + queue slots

Actionable exits = a SEPARATE future amendment: a new pinned field on the SIGNAL schema with a bump to `schema_version: 2`, gated on the phase-1 display soak — **never by parsing `hold_status`** (informational free text forever). Rollout: OFF → emit-only → engine display consumption → soak. Queue: order-app emitter = own Opus-HIGH pass after N2 (their queue); engine consumption behind the §6.1 net-EV rider. Nothing is Aug-1-critical. Engine fixtures at build: parse / staleness / fallback / enum-tolerance / radios-grey (next-free family at build).
