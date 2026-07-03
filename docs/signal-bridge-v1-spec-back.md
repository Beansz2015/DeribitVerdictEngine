# Signal Bridge v1 — Engine-Side Implementation Spec-Back

**Status:** IMPLEMENTED 2026-07-03 (settings v48→**v49**), local commit — trader tests + pushes.
**Parent spec:** `signal-bridge-v1-proposal.md` (schema v1 **FROZEN** 2026-07-03, §8 D1–D10 all ticked). Canonical consumer copy: `DeribitOrderPlacementApp/docs/integration-contract-verdictengine.md`; proposal §3 is the verbatim emission/parity mirror this build implements.
**Scope:** the engine-side lane only (§4/§7). The consumer lane runs in the order-app repo against their canonical doc.

## 1. What was built

| Piece | File | Notes |
|---|---|---|
| Emitter | `Core/SignalEmitter.vb` (new) | Pure `BuildOk`/`BuildSkipped` → `JsonObject` (§3 key order), pinned `DeriveDirection`/`DeriveWsHealth`, `Serialize` (System.Text.Json, indented), `ResolveOutputPath` (empty ⇒ beside the exe), atomic `TryWrite` (tmp + `File.Replace`, create-dir-if-missing, catch + console — never throws). Host-agnostic, zero WinForms. |
| Process identity | `Core/ProcessIdentity.vb` (new) | `InstanceId` GUID (static init) + `NextSignalId()`/`CurrentSignalId` (Interlocked). Ticks once per completed run regardless of `signal_bridge.enabled` — the shared primitive §4 requires for the #5 v0.8 CSV columns. |
| WinForms glue | `UI/MainForm_SignalBridge.vb` (new) | `EmitBridgeSignal` (success) + `EmitBridgeSkipped` (skip), both try/catch-hardened and gated on `signal_bridge.enabled`; `CurrentBridgeWsHealth` threads feed state into the pure derivation; ARM checkbox handler + visibility sync. |
| Call sites | `UI/MainForm_Analysis.vb` | Success: id ticked right after `Calculate()` (see §3.3), emission after the `_lastSuccessful*` capture (post snapshot + card binds — Kelly populated, values = rendered values). Skip: tick + reduced payload in the skip branch. No other changes to `RunAnalysisAsync`. |
| ARM AUTOTRADE | `UI/MainForm_Layout.vb` (strip row) | Third column on the TAPE strip row (row 3b), right edge — the TAPE-checkbox pattern. Runtime-only, default OFF (unchecked) every start, never persisted; visible only while the bridge is enabled. |
| Settings | `Core/Settings/EngineSettings.vb` + `settings.json` | New `SignalBridgeSettings` POCO + top-level `signal_bridge {enabled:false, output_path:"C:\\Dev\\DeribitBridge\\verdict_signal.json"}`. Version 48→49 + change_log entry + §15 row. |
| Tweaker fence | `SettingsDiffApplier.vb` + `PromptBuilder.vb` | `"signal_bridge."` in `RejectedPathPrefixes` + **HARD CONSTRAINT 18** (transport plumbing, `network.*` class). |
| Harness | `verify/ordercheck/` | `OrderCheck.vbproj` links the two new Core files; new **A22a–g** (20 checks — see §4). |

## 2. Acceptance record (2026-07-03)

- `tools/checks/verify-gate.ps1 -Mode prepush`: solution (Release) + AutoTweaker + OrderCheck build **0/0**; harness **ALL PASS** — A1–A21b unregressed + A22a–g new; display-parity guard clean (no snapshot/card change).
- **Outstanding (trader):** the §7 live smoke — enable on the bin copy, confirm the file updates every run (OK + a forced skip), values eyeball-match the rendered card, ARM toggle flips `autotrade_armed` in the next payload. The `enabled` flip itself stays a dated action after the consumer's log-only validation (D3).

## 3. Deviations & judgement calls (all faithful to spec intent)

1. **`kelly` on no-edge is zeros, not null.** The frozen schema defines `kelly` as an object with `contracts`/`risk_usd`/`lev_capped`; it has no null form. When the display suppresses the Kelly block (no edge, `KellyPWin = 0`) the payload emits `{0, 0.0, false}`. Kelly is advisory-only in v1 (D5) — the consumer never sizes from it, so the distinction is cosmetic; flagged here in case v2 wants a null form (schema bump).
2. **SKIPPED `health.ledger_mismatch` = false.** No `VerdictResult` exists on a skip, so there is nothing to mismatch this run. The consumer stands down on SKIPPED regardless, so the field is inert there.
3. **`signal_id` tick placement (success path): right after `Calculate()`, BEFORE `AnalysisLogger.LogRun`.** The spec pins CSV `SignalId` ≡ payload `signal_id` per run; ticking before the CSV write means `ProcessIdentity.CurrentSignalId` is already this run's id when the #5 v0.8 columns read it at `LogRun` — no re-plumbing needed at that build. Emission itself still happens at the end (§2 ordering). Consequence: an exception between tick and emission burns an id (gap, no payload) — consistent with "never on error paths mid-run"; gaps are legal, monotonicity is the guarantee. A future-#5 caveat: that same abort window would leave a CSV row whose id has no payload — an error-path artifact the join analysis should tolerate.
4. **`instance_id` minting is static-init-on-first-touch**, not an explicit `Program.vb` call. The first analysis run (or the emitter/CSV) touches it, so it is effectively minted at process start while keeping the primitive dependency-free for the Linux CLI port.
5. **ARM checkbox visibility syncs at creation + once per run** (in the emission glue, which runs even when the bridge is disabled). A `signal_bridge.enabled` hot-reload flip therefore surfaces the checkbox on the next run, not instantly — acceptable because the enable flip is a dated, deliberate action (D3). No timer added for this.
6. **`health.ws` mapping edge:** `transport=ws` with a missing feed object (shouldn't occur — the feed starts whenever transport=ws) maps to **DOWN**: the engine cannot truthfully claim OK/DEGRADED there. Full pinned precedence: `transport≠ws → REST`; `degraded-this-run → DEGRADED`; `feed missing/disconnected → DOWN`; else `OK`.
7. **Serialization is `WriteIndented`** — the file is machine-read but the §7 smoke and later debugging are eyeball checks; whitespace is semantically irrelevant to the consumer's JSON parse. Default System.Text.Json string escaping is kept (informational strings may carry `\uXXXX` escapes; they parse identically — never gate on free-string *values*, per the contract).
8. **A22g added beyond the §4 a–f list** — a tweaker-fence fixture (rejects `signal_bridge.enabled`, accepts a sibling `scoring.*` tunable), following the A20g/A21b precedent so HARD CONSTRAINT 18 is code-tested, not prompt-only.
9. **`hold_status` sentinel mapping:** the `"N/A -- no open position"` sentinel emits `null`, mirroring the snapshot's HOLD/EXIT row suppression (display parity); any other value rides verbatim (informational until v2 makes `posState` truthful).
10. **Bin-copy note:** the tracked `settings.json` gains the block; the bin copy the live app reads picks it up on the next build (or materialises it on any UI save via the full-POCO serialise). An absent block is harmless — the POCO default is `enabled:false`, identical behaviour.

## 4. Harness coverage (A22, `verify/ordercheck/Program.vb`)

| Fixture | Covers |
|---|---|
| A22a (10 checks) | Full-payload field-by-field on parsed JSON: head, engine block, verdict/scores, price/atr/trigger, **uncapped + genuinely-capped + cap-noise-suppressed** target cases (§3 field-sourcing rule incl. the v30 `max(0.5, ATR×0.02)` floor), structural verbatim, `hold_status` null-sentinel + verbatim pass-through, kelly, health. |
| A22b | SKIPPED reduced shape — state/reason/engine/health present; `verdict`/`levels`/`price`/`kelly` **absent**. |
| A22c | The full `AppendLean` output space (`NO TRADE`, `[WEAK LONG]`, `[WEAK SHORT]`, `[TIE]`) ⇒ `direction:"NONE"`; WEAK/plain/STRONG directional verdicts carry their side; end-to-end payload keeps the lean text in `verdict` while `direction` reads NONE. |
| A22d | `DeriveWsHealth` all four pinned values + precedence; `confidence` pass-through for HIGH/MEDIUM/LOW/N-A. |
| A22e | `autotrade_armed` reflects the toggle; `InstanceId` stable across reads; `NextSignalId` strictly monotonic + `CurrentSignalId` agrees. |
| A22f | Serialization pins under a hostile `de-DE` thread culture: dot decimals, numbers as JSON numbers, `generated_at_utc` exact ISO-8601 `Z`. |
| A22g | HARD CONSTRAINT 18 fence (reject `signal_bridge.enabled`, accept `scoring.verdict_med_pct`). |

## 5. What this build deliberately does NOT do (per spec)

- No CSV change — the `InstanceId`/`SignalId` columns join at the **#5 v0.8 header rotation**, reading the primitive this build ships.
- No card/snapshot line changes — parity rule satisfied by construction; A22a is the standing third-surface guard.
- No consumer-side behaviour — watcher/debounce/staleness-poll/de-dupe/gates are the order-app lane (§5).
- No v2 feedback file, no actionable exits, no Kelly-driven sizing, no network transports, no multi-instrument (§6).
