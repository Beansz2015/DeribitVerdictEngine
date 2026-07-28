# Engine-Seat Reply — C1 v2 Feedback File — 2026-07-28

**Re:** order-app `proposal-c1-v2-feedback-file.md` (DRAFT 2026-07-28, relayed by owner 2026-07-28). **Process:** the v1 reconciliation-trail pattern per their §0 — this reply → order-app ack → ONE coordinated documentation pass amending contract §8; neither side edits the other's repo; implementation only after ack + owner tick. **Paste-ready for owner relay.**

## 1. Verdict: ACCEPTED — the draft is build-shaped. Concurrences, three refinements, and the two answers their §8 asked of this seat.

The §2 principles are exactly the v1 discipline this seat would have demanded (telemetry-never-commands preserves R1 in both directions; frozen v1 untouched; the §2.5 disposition-cardinality freeze gets a second reader, not a side door — consistent with the 07-27 chase-abort correction this seat already accepted). The §3 schema, §4 semantics, and §7 phase fence are accepted as drafted, subject to the items below.

## 2. Concurrences on their D-table (owner still ticks)

- **D1 ACCEPT** — `executor_feedback.json` beside the signal file. Engine-side key naming (their §6.5 leaves it to this seat): new `signal_bridge.feedback` block — `{ enabled: false, path: "C:\\Dev\\DeribitBridge\\executor_feedback.json", stale_after_sec: 35 }`. Rides the existing `signal_bridge.` tweaker fence — **no new HARD CONSTRAINT needed**. Settings bump at the engine build (v63 or wherever the counter sits then).
- **D2 ACCEPT** — 10 s heartbeat / 35 s staleness as proposal defaults; engine consumes per-run (a fresh read at `RunAnalysisAsync` start — no watcher plumbing; the staleness check happens at the same site).
- **D3 ACCEPT** (include `position.working`) — with one emission-trigger refinement, §3.1 below.
- **D4 ACCEPT** (`breaker_tripped` + `ws`) — interlock display wants exactly "can the executor act". The 2-state `ws` enum is fine (informational; the engine will not map it onto its own 4-state health enum).
- **D5 RATIFY** — phase 2 arrives as a pinned field on the signal schema with a bump to 2, never by parsing `hold_status`. This is the engine's own never-gate-on-free-strings rule read back to us; fenced exactly right.

## 3. Three engine-side refinements (accept-or-counter in the ack)

**3.1 Add working-level repositions to the §5 emission triggers.** §5(b) covers position open/close/size; working stop/target moves during an entry chase or SL reposition are *order* state, not position state, and would go stale between heartbeats while D3 renders them. Since §6.2's single-writer is self-coalescing last-wins, a (b2) "working-level change" trigger costs nothing at emission and keeps the D3 block honest during exactly the windows it is most watched.

**3.2 The `avg_entry` join (their §8 open question): ACCEPTED — with the stacking trigger named.** The join (after `acted` for this engine's (`instance_id`,`signal_id`), the next non-flat `position.avg_entry` is signal N's achieved fill) is sufficient for slippage-aware pricing v1, and this seat prefers it for the same cardinality reason. Its one failure mode: if executor policy ever allows a second `acted` signal to ADD to an open position, `avg_entry` blends and per-signal attribution breaks silently. So the acceptance carries a named trigger: **the day stacking/pyramiding enters executor policy, an explicit per-acted-signal achieved-entry field becomes a v2.1 amendment** — until then the join is the record. (The §4 fill-window gap semantics — FLAT between `acted` and fill, abandonment leaves it FLAT forever — are accepted as stated; the engine will not infer failure from that window.)

**3.3 Pinned-enum tolerance, stated now so neither side discovers it in a fixture.** Per v1 discipline the engine gates/branches only on the pinned strings; an unrecognised value in a pinned enum field renders verbatim in display and behaves as the conservative arm for logic (unknown `mode` ≠ LIVE for interlock display; unknown `direction` → treated as absent ⇒ manual fallback). This keeps a future enum addition from being a coordinated-freeze event when it only needed to be additive.

## 4. D6 answer — engine-side `posState` consumption shape (this seat's to propose; owner ticks)

**Feedback-authoritative when governing; radios become the fallback, not an override.**

- The file **governs** when: `feedback.enabled` AND file present AND fresh (≤ `stale_after_sec`) AND `position.direction` parses. Mapping: `LONG`/`SHORT`/`FLAT` → `PositionState` Long/Short/None.
- While governed, the manual radios (`MainForm_ExitGuard.vb` `rbLong`/`rbShort`) are **greyed out with a tooltip** ("position state from executor feedback") — not consulted, not silently overridden-in-place. No both-sources arbitration exists, so no drift confusion can either.
- Stale / absent / disabled / unparseable ⇒ radios re-enable and today's manual behaviour returns unchanged. **File absent = feature OFF, never an alarm** (their §5 rule, accepted). Staleness additionally surfaces as an `EXECUTOR STALE` tag.
- **Where state surfaces: the exit-guard strip / status-bar tier only** (source tag `POS:EXEC` / `POS:MANUAL`, the executor armed/started/mode/breaker interlock display, the stale tag). These are live status elements — the display-parity exempt class, stated per standing rule 4. **Phase 1 changes NO snapshot line, NO card binding, NO CSV column, NO payload field** — the engine build is rotation-free and boundary-free (display/plumbing only, no ⚠). Any CSV attribution of "hold evaluated against executor state" is deliberately deferred to phase 2, where it belongs with actionability.
- Consequence the owner should tick knowingly: with feedback governing, `HOLD \ EXIT` and the exit guard evaluate against the executor's position model **including owner-manual trades** (their §4 semantics — this seat agrees it is the right default, and it is precisely what makes the row useful during manual sessions with the executor up).

## 5. Engine-side build commitments (at implementation, not before)

Own pass, Opus, coordinator review + gate per standing methodology; A22-style fixture family for the consumption path (parse / staleness / fallback / enum-tolerance / radios-grey state), next-free family letter confirmed at build. Sequencing per the engine plate: the doc exchange runs now; the engine consumption build slots into the implementation queue behind the §6.1 net-EV rider — nothing in phase 1 is time-critical for Aug 1.

## 6. Also corrected on the engine record (separate item, same relay batch)

The fee relay §0's "duplicate-constant" note under-counted: the order app carried a third, older hardcoded 2024 taker constant (`TakerFeeRate = 0.0005`, ~43% overstated vs the Aug-1 schedule) driving a comms default — found by their EV review, queued for repoint (`spec-fee-comms-repoint.md`). No contract impact. Recorded in `fee-aware-order-app-relay-2026-07-27.md` §0 addendum; noted here so the batch travels together. The engine side carries no analogous constant — fees first entered engine code at v62, all four values in `scoring.trade_costs`.
