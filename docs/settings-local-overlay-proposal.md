# `settings.local.json` — Per-Box Settings Overlay

**Status:** **AWAITING TRADER** — D1–D6 in §7. Spec-first; nothing built.
**Target:** code-only in `Core/Settings/SettingsLoader.vb` + one `.gitignore` line. **No new settings keys, no version bump.**
**Scoring impact:** **NONE by construction** — the overlay whitelist (§2) admits only blocks already fenced off the auto-tweaker surface for having no failure-rate linkage.
**Dataset boundary:** **NONE.** Same reason: nothing an overlay can touch reaches the scoring path.
**Gate to build:** safe anytime.
**Origin:** trader ruling on v64-review **F6** (2026-07-31) — capture ON for AWS, OFF for the local box. Executing that ruling exposed the fact that **the project has no mechanism for per-box settings at all**, and the workaround it forces is a recurring manual chore with a silent failure mode (`aws-collector-deploy-checklist.md` §1a).

---

## 0. The problem, stated precisely

Two boxes run the same binary from the same tracked `settings.json`:

- **AWS** — the sole raw-tape capturer (D1), deployed by xcopy of `bin\Release\net8.0-windows\` (checklist §1.1). **The tracked file is what lands there.**
- **Local `bin\Debug`** — the canonical analysis book, intermittently up, and explicitly *not* a capturer.

They need different values for `trade_store.enabled`, and there is no way to express that. `SettingsLoader.Initialise(path)` reads exactly one file, and `settings.json` is `CopyToOutputDirectory=PreserveNewest`, so **every build with a newer tracked file overwrites the local copy** and silently restores the shared value. This is the same stomping mechanism v57 was written to defuse, and it has now bitten a second time on a different key.

The current workaround puts the burden in the wrong place. Because the failure directions are asymmetric — tracked `false` un-corrected on AWS loses tape *permanently and silently*; tracked `true` un-corrected locally costs disk — the tracked seed must carry the AWS value, which makes **the local box the hand-edited exception, re-applied after every settings bump, with no reminder and no symptom until someone notices a growing directory.**

That is a process patch over a missing feature. This spec is the feature.

**A note on scope discipline.** The obvious generalisation — "let any key be overridden per box" — is the wrong feature, and §2 is the reason.

---

## 1. Mechanism

After loading `settings.json`, if `settings.local.json` exists **in the same directory**, deep-merge it over the base and deserialise the result.

```
<exe>\settings.json         tracked, shared, version-bearing  — the base
<exe>\settings.local.json   gitignored, per-box, no version   — the overlay
```

- **Deep per-key merge, not block replacement.** `{"trade_store": {"enabled": false}}` overrides exactly that key and leaves `store_dir`, `flush_seconds` and the rest at their base values. Implemented on `JsonNode`: parse both, walk the overlay, assign leaf-by-leaf, then deserialise once into `EngineSettings`. Arrays replace wholesale (there is no sane per-element merge for `session_volume.sessions[]`, and §2 fences that block out anyway).
- **The overlay carries no `version` and no `change_log`.** The base owns those. An overlay that names `version` is rejected (§2).
- **It survives builds**, which is the entire point: it is not a project item, so nothing copies over it. `dotnet clean` removes it along with the rest of `bin\`, and **the failure direction is the bad one** — on the local box, losing the overlay silently switches capture back **on**, the exact state F6 ruled against. §3's principle applies to the overlay's *absence* as much as its presence, so the title-bar `+local` marker is what makes absence observable: **add "title bar reads `+local`" to the daily glance in `aws-collector-deploy-checklist.md` §3**, which the trader already performs. That closes the loop for the cost of one line.
- **Absent overlay ⇒ byte-identical to today.** The merge branch is skipped entirely.

### 1.1 Hot reload

`SettingsLoader` already watches `settings.json` with a `FileSystemWatcher`. The watcher must also fire on `settings.local.json`, and a reload must redo the merge from both files — otherwise editing the overlay appears to do nothing until restart, which is exactly the kind of quiet surprise this feature exists to remove.

### 1.2 `Save()` — the load-bearing detail

`SettingsLoader.Save(cfg, changeNote)` writes `Current` back to `settings.json`. **With an overlay active, the naive implementation writes the merged values into the tracked file** — promoting a local-only override into the shared file, which then propagates to AWS on the next deploy. For `trade_store.enabled` that is precisely the catastrophic direction the checklist §1a asymmetry warns about: the local box's "don't capture" would silently become AWS's.

So `Save` must operate on the **base** document, never the merged one: keep the parsed base `JsonNode` in memory, apply the caller's changes to it, write that, then re-merge the overlay on top. The live UI saves (`MIN NET MOVE %`, output-dump settings) all route through `Save`, so this is not a corner case — it is the normal path.

**This is the single most important thing to get right in the build, and A50c exists for it.**

---

## 2. What may be overridden — a whitelist, and why not "anything"

> **Revised 2026-07-31** against [`settings-local-overlay-review-2026-07-31.md`](settings-local-overlay-review-2026-07-31.md). The review's finding is correct and accepted: the first draft's whitelist admitted keys that demonstrably move the failure rate, which defeated this section's own argument. It is redrawn at key granularity below, the rule is restated honestly, and the block-by-block audit the review asked for (§6 of the review) is done. **Two corrections back to the review are in §2.5.**

> ⚠ **SECOND-PASS RE-AUDIT 2026-07-31 — three of §2.4's conclusions do not survive. Read [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) before ticking D1.** The first reviewer audited 2 of the blocks and both failed; nobody had re-checked the other seven. They have now been:
>
> 1. **`alerts.` must move to REJECT.** `AlertsTracker.FoldTrade` returns at `Core/AlertsTracker.vb:116` when `Not cfg.Enabled`, and the sidecar writes live *inside* that method (`:143`, `:157`); `DeribitWsFeed.vb:439` skips the fold entirely. So `alerts.enabled:false` ⇒ **`liq_events.log` is never written** — and that file is the **sole gate on A4**, with both boxes counted as two instruments and their sidecars pooled. `cascade_min_trades`/`cascade_window_sec` make it worse than binary: they change the evidence *content* while both boxes stamp the same version.
> 2. **`mtf_gate` is named nowhere** — not in the admit list, not the split, not the reject, not the "everything else" enumeration (which covers 16 of `settings.json`'s 17 settings blocks). It is the **hard veto**. The catch-all protects it only if the build is an allow-list — which D1 implies but never states — and **A50d pins only `scoring.*`, `indicators.*`, `version`.** Name it explicitly, state the allow-list constraint in D1, extend A50d.
> 3. **`performance_display.` stays admitted but is not "Clean"** — it gates the eval cache, the OHLC cache and the gap-fill outright (`LivePerformanceTracker.vb:188-191`, `:494`, `:261-266`). No queued decision reads the eval cache and the offline stack is independent, so it passes today; **revisit if anything ever gates on it** (Kelly CAL is the near candidate).
>
> **The rule in §2.1 is ratified and EXTENDED (J-D):** *a key is safe to diverge when (i) it cannot move the failure rate **and (ii)** it cannot change what any evidence instrument records that a queued decision or standing watch depends on.* §2.4 could not have caught `alerts.` because §2.1 as written does not ask about (ii).
>
> **Also:** the arithmetic is off — §2.4 audits **nine** blocks, not eight; "six pass" should be "seven". Post-re-audit the honest count is **5 clean · 2 instrument-breaking · 1 split · 1 rejected · 1 unlisted**. And §1.2 enumerates two live-save paths but there are **four** — `live_strip.enabled` and `performance_display.metric_mode` are also UI-writable, which on an overlaid box writes the *shared tracked file* (the AWS xcopy source) while the overlay keeps winning locally; the `live_strip` checkbox visibly snaps back within ~2 s. **A50c pins base-vs-merge; nothing pins whitelist ∩ UI-writeback — add a fixture.**

An unrestricted overlay would silently break the **same-settings pooling discipline** (`aws-collector-deploy-checklist.md` §4.5): rows from the two boxes are only comparable while both run the same settings, and the CSV's settings-version column is what makes a straddle visible and filterable. An overlay changes behaviour **without changing the version**, so an unrestricted one would make two boxes disagree on scoring while both stamp the same version — producing an invisible, unfilterable straddle in the pooled book. That is a worse failure than the chore this spec removes.

### 2.1 The rule, stated honestly

*A key is safe to diverge per box **when it cannot move the failure rate**. The HC fences are a **good first filter** for that — not a proof.* A block can be fenced off the tweaker for a reason that has nothing to do with scoring impact, and `network.` is exactly that case: it carries HC12 because the tweaker has no business tuning timeouts and retries, **not** because transport cannot move scoring. Where the two justifications diverge, the block needs per-key review.

The first draft asserted the stronger claim — one list, two uses — and it does not hold.

### 2.2 The whitelist, at key granularity

**Admitted whole (audited, §2.4):** `trade_store.` · `signal_bridge.` · `alerts.` · `live_strip.` · `exit_guard.` · `performance_display.` · `analysis_logging.`

**`network.` — admitted per key:**

| Admit | Reject |
|---|---|
| `request_timeout_seconds`, `retry_count`, `retry_backoff_ms` — these change whether a run **skips**, never what a completed run computes. A skipped run emits no row, so the rows that do exist stay comparable. | **`transport`** (§2.3) · **`ws_fallback_to_rest`**, **`ws_stale_after_sec`**, **`ws_heartbeat_sec`**, **`ws_cooldown_sec`** — every one of these modulates connection health, and connection health *selects the data source* through `ResolveSource` · **`shadow_parity`** (rejected on side-effects, not scoring — §2.5) |
| `ws_url` — a different endpoint for the same venue and the same data. | |

**`auto_run.` — rejected entirely.** `trigger_mode`, `interval_minutes` and `interval_seconds` are all scoring-relevant, and there is no demonstrated per-box need for the block. The repo carries its own proof on both keys: the **whole v53 funding time-anchored-window rewrite** exists because cadence changed `FundingMomentum` (FLAT 52.1 % at 60 s vs ~0 % on 3-minute sessions), and the JOB 2 §3.2 volume finding depends entirely on `on_close` being why `VolumeRatio` reads a newly-opened bar — live p50 **0.0100** against a closed-bar **0.66**, and a volume-vote fire rate of **0.69 %**. A box on `interval` scores differently. **`trigger_mode` is not yet a CSV column** (it rides the next natural rotation), so a diverged value would be invisible in the pooled book — the straddle this section exists to prevent.

Everything else — `scoring.`, `indicators.`, `session_volume.`, `resolution_profiles.`, `regime_*`, `kelly.`, `version` — is **rejected**, loudly (§3).

### 2.3 Why `transport` is the sharpest case — the mechanism, verified

The review argued from data quality (100 ms streaming book vs snapshot polling). The real mechanism is more direct and more damning. Three sites in `UI/MainForm_Analysis.vb` gate a signal on the **identity** of the resolved source:

```
:387   If ofiCfg.AveragingEnabled AndAlso (src Is _wsSource) …   ' time-averaged OFI
:442   If avCfg.Enabled          AndAlso (src Is _wsSource) …   ' aggressor velocity
:463   If absCfg.Enabled         AndAlso (src Is _wsSource) …   ' book absorption
```

On a `transport: rest` box the time-averaged OFI silently reverts to snapshot OFI, and **the v52 aggressor-velocity TFI modifier never fires at all**. That is not a data-quality difference that might wash out — it is a different scoring computation, by construction, on every row.

And `ResolveSource` (`:729`) shows the second-order path the review's own ADMIT column missed: a `transport: ws` box whose feed is degraded returns `_restSource` for that run, closing all three gates. `IsDegraded()` reads `ws_stale_after_sec` and the cooldown state, and the fallback is gated on `ws_fallback_to_rest` — so those keys decide **how often a WS box silently scores like a REST box**. Same defect, one level down.

### 2.4 The block-by-block audit (review §6 asked for this before build)

Method: enumerate every `cfg.<Block>` / `SettingsLoader.Current.<Block>` read across `Core/ScoringEngine_*.vb`, `Core/Indicators_*.vb`, `DynamicNorms.vb` and `Core/SignalEmitter.vb`, then check the run path in `UI/MainForm_Analysis.vb` for indirect gates.

The scoring path reads exactly: `Indicators`, `Scoring`, `SessionVolume`, `RegimeGates`, `RegimeWeights`, `MTFGate`, `Kelly`, `ATR`, `Volume`, `VWAPDynamic`, `Version`, `AutoRun`.

| Block | Verdict |
|---|---|
| `exit_guard.` · `live_strip.` · `alerts.` · `signal_bridge.` · `performance_display.` | **Clean.** None is read anywhere on the scoring path, and none appears in the run path in `MainForm_Analysis`. |
| `analysis_logging.` | **Clean.** The only one that touches the run at all — `:664–665`, post-render, feeding the output dump. |
| `trade_store.` | **Clean by construction** (v64: no indicator reads it, no CSV column, no verdict path, no bridge field). |
| `auto_run.` | **REJECTED** — see §2.2. Its one direct read is `SignalEmitter:131`, which only *reports* `trigger_mode` into the bridge payload; the real impact is indirect, via cadence, and is documented twice in-repo. |
| `network.` | **Split per key** — see §2.2/§2.3. |

**Two of eight blocks failed the review's inspection; the remaining six pass mine.** Six blocks admitted whole, one split, one rejected.

### 2.5 Two corrections back to the review

1. **`shadow_parity` cannot move scoring.** `ResolveSource` returns `_restSource` on the very first branch whenever `transport ≠ "ws"`, *before* shadow parity is ever consulted — and when `transport = "ws"` shadow parity only adds a comparison log. It belongs in the reject column, but for **side-effects**: it constructs a `MarketState` and starts a WS feed, which since v64 also runs trade-store capture. The stated reason matters because it becomes the precedent for the next key.
2. **`ws_fallback_to_rest` and `ws_stale_after_sec` were in the review's ADMIT column** and should not be (§2.3). They are the same defect the review correctly caught in `transport`, reached one indirection later.

---

## 3. Visibility — an overlay must never be silent

Every defect this project has chased in the last two days was **silent divergence**: a 28-day funding hole, a month of candles, a capture flag that stomps itself. An override mechanism is a divergence generator, so it has to announce itself:

- **Title bar** already renders `settings v{N}` (v53). With an overlay active it renders **`settings v{N} +local`**. A live status element ⇒ **display-parity exempt** (the v62 `MIN NET MOVE %` / EXIT GUARD precedent) — no snapshot line, no card binding.
- **Console at startup**, one line naming every key the overlay actually changed and its before→after values. This is the line a future seat greps when a box behaves oddly.
- **Rejected keys are loud and non-fatal**: a whitelist violation logs `[SettingsLoader] settings.local.json: 'scoring.x' is not overridable — IGNORED` and the base value stands. Refusing to start would be worse; a box that will not boot loses more data than a box running shared settings.

---

## 4. Reversibility, fences, parity

- **Delete the file ⇒ pre-build behaviour exactly.** That is the rollback, and it needs no flag.
- **Tweaker:** no new HARD CONSTRAINT. The overlay adds no settings keys, and its whitelist is *derived from* the existing fences rather than adding to them. The tweaker keeps reading and writing the base file (§1.2), so its view of what it is tuning is unchanged.
- **Display parity:** one live status element, exempt as above. No snapshot, card, CSV or payload change.
- **`.gitignore`:** add `settings.local.json`. The running copy already sits under the ignored `bin/`, but a root-level convenience copy must never be committed — committing one would defeat the entire purpose by making it shared again.

---

## 5. Acceptance + fixtures

Fixture family **A50** (A49 reserved by `trade-store-coverage-report-proposal.md`; next free after this is **A51**).

- **A50a** — absent overlay ⇒ the deserialised `EngineSettings` is byte-identical to today across a full settings tree.
- **A50b** — deep merge: `{"trade_store":{"enabled":false}}` flips exactly that key; every sibling in the block and every other block keeps its base value.
- **A50c** — **`Save` writes the BASE, not the merge.** With an overlay flipping `trade_store.enabled` to false, a `Save` triggered by an unrelated UI edit leaves `trade_store.enabled: true` in `settings.json` and preserves the edit. *The regression trap: inverting this promotes a local override into the shared file and, from there, onto AWS.*
- **A50d** — whitelist: each fenced prefix applies; `scoring.*`, `indicators.*` and `version` are ignored with a logged reason; the base value survives; startup still succeeds.
- **A50e** — malformed overlay (bad JSON) ⇒ logged and ignored, base settings load normally, app starts.
- **A50f** — hot reload: touching the overlay re-merges; deleting it reverts to base without a restart.
- **A50g** — arrays replace rather than merge, and a whitelisted block containing an array is handled without partial-element surprises.
- **A50h** — **the scoring-surface pin** (review §4): build `EngineSettings` with a `scoring.*` overlay present and assert the verdict is byte-identical through the REAL `Calculate()`, the A42a / A36a pattern. A50d proves the key is ignored *at parse*; the header's claim ("Scoring impact: NONE by construction") deserves a pin at the surface it makes the claim about.
- **A50i** — `network.transport` and `auto_run.trigger_mode` are rejected by the key-granular whitelist while a sibling in the same block (`network.request_timeout_seconds`) is admitted. Pins §2.2's split, which a block-granular reading would silently undo.

Build acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0 Release**; A1–A49x unregressed + A50a–i; verify-gate `prepush` **GATE PASSED**.

**Build-time question, settled by the review:** `SettingsLoader.vb` is on the engine path (`verify-gate.ps1:126` lists `Core/`), so the version-bump guard trips — but it is **WARN-only** ("nudge only", line 141) and the `[no-engine-change]` token satisfies it outright. One caveat carried from the v64 review's F5: the gate reads **committed** messages, so the token must be in the commit, and a pre-commit gate run sees neither the change nor the token.

### 5.1 Cross-spec dependency — `trade-store-coverage-report-proposal.md` D7

The coverage report's **D7** decides how the report distinguishes a capture defect from capture being switched off. Once this overlay exists, `trade_store.enabled` on the local box lives in `settings.local.json`, **not** in `settings.json` — so **D7 must read the MERGED value, not the base file.** Reading the base would see `true` and report every up-hour on the local store as a capture defect, which is precisely the false alarm D7 exists to prevent. Whichever spec builds second inherits this; it is noted in both.

---

## 6. Out of scope

- Per-box **scoring** divergence — deliberately impossible (§2), and the pooling discipline is why.
- A UI editor for the overlay. It is a text file edited rarely, on two boxes, by one person.
- Environment-variable or command-line overrides. A file that survives builds is the requirement; a second mechanism is not.
- Any change to `settings.json`'s schema, the tweaker surface, or the scoring path.

---

## 7. D-table — awaiting the trader

| # | Decision | Options | My read |
|---|---|---|---|
| **D1** | Whitelist the overridable blocks, or allow anything? | (a) whitelist, **key-granular per the redrawn §2.2** · (b) unrestricted · (c) whitelist just `trade_store.` | **(a), as redrawn.** The review was right that the first draft's list admitted scoring-relevant keys; six blocks are now admitted whole, `network.` is split per key, `auto_run.` is rejected outright. (b) breaks pooling invisibly — two boxes disagreeing on scoring while stamping the same settings version is an unfilterable straddle, worse than the chore this removes. (c) solves today and gets re-opened at the next key. **Reviewer position: (a) with the list redrawn — agreed and done.** |
| **D2** | File name / location. | (a) `settings.local.json` beside `settings.json` in the exe dir · (b) a `--settings` CLI arg · (c) an env var | **(a).** It survives builds, which is the actual requirement; (b)/(c) do not survive a Startup-folder shortcut or a reboot, which is how the collector runs. |
| **D3** | Whitelist violation: ignore-and-log, or refuse to start? | (a) ignore + log loudly · (b) fatal | **(a).** A box that will not boot loses more data than a box running shared settings — and on AWS, "will not boot" means silent tape loss, the exact failure D1 is about. |
| **D4** | Show the overlay in the title bar? | (a) `settings v{N} +local` · (b) console only | **(a) plus console.** Every defect chased in the last two days was silent divergence; an override mechanism that hides itself is one more. Parity-exempt as a live status element. |
| **D5** | Does the auto-tweaker need to know about the overlay? | (a) no — it reads/writes the base, unchanged · (b) make it overlay-aware | **(a).** Its whole surface is scoring keys, which §2 forbids overriding, so a tweaker-visible overlay is impossible by construction. Worth re-checking if D1 goes to (b). |
| **D6** | Apply the same overlay to `tweaker_config.json`? | (a) no, out of scope · (b) yes | **(a).** The tweaker runs on one box. Adding a second overlay surface with no demonstrated need is how a small feature becomes a subsystem. |
| **D7** | A REST-mode box is a legitimate need that §2.2 now blocks. How should it be expressed? | (a) tracked `settings.json` change with a version bump · (b) admit `network.transport` to the overlay, accepting the straddle · (c) admit it AND exclude that box's rows from the pooled book | **(a) — ANSWERED 2026-07-31**, orchestrator note [`overlay-d7-and-row-source-stamp-2026-07-31.md`](overlay-d7-and-row-source-stamp-2026-07-31.md). Adequate because **a REST box is break-glass, not routine**, and a break-glass change *should* be deliberate and version-visible — the version bump is the feature, not the cost. My "a real capability removed" framing was too generous to (b). **Not a permanent close:** (c) becomes available once rows carry their effective data source. **Recorded, because the straddle predates the overlay:** a `transport: ws` box already emits REST-scored rows whenever `ResolveSource` falls back on a degraded feed (§2.3) — unmarked, at a rate nobody can measure, and no D7 answer changes that by one row. **Rider on (a):** a tracked flip to REST propagates to AWS on the next xcopy, so `aws-collector-deploy-checklist.md` §1a is where it must be caught. |

**Reviewer positions:** D2 (a) · D3 (a) · D4 (a) + console, extended to cover the marker's *absence* · D5 (a), conditional on D1 staying (a) · D6 (a) — all agreed, all unchanged. D1 was the only contested item and is now redrawn.

---

## 8. Revision record

Against [`settings-local-overlay-review-2026-07-31.md`](settings-local-overlay-review-2026-07-31.md) (verdict: *"the mechanism is right and the problem is real — but the whitelist as drawn admits three keys that demonstrably move the failure rate"*).

**Accepted:** the whitelist is redrawn at key granularity (§2.2); the rule is restated as *HC fences are a good first filter, not a proof* (§2.1); the block-by-block audit the review deferred is done and the remaining six blocks pass (§2.4); the `dotnet clean` failure direction is named and answered with a daily-glance line (§1); the cross-spec D7 dependency is recorded in both specs (§5.1); A50h pins the scoring claim at the scoring surface; the verify-gate question is settled (WARN-only + committed token).

**Extended:** the review's own ADMIT column carried the same defect — `ws_fallback_to_rest` and `ws_stale_after_sec` feed `IsDegraded()`, which `ResolveSource` consults to serve a WS-configured run from REST, closing the three `src Is _wsSource` gates (§2.3). Rejected accordingly, along with `ws_heartbeat_sec` / `ws_cooldown_sec` on the same reasoning.

**Corrected:** `shadow_parity` cannot move scoring — `ResolveSource` short-circuits on `transport ≠ "ws"` before parity is consulted. Still rejected, on side-effects (it starts a WS feed, which since v64 also runs capture), but the stated reason had to change because it sets the precedent for the next key.

**Sharpened:** the `transport` case rests on three explicit `src Is _wsSource` gates rather than on streaming-vs-polling data quality — a different computation by construction, not a difference that might wash out.

**New decision surfaced, and since answered:** D7 — §2.2 removes the REST-box capability the first draft offered. Answered **(a)** by the orchestrator, and my framing of it as "a real capability removed" was too generous: a REST box is **break-glass, not routine**, so requiring a deliberate, version-visible change is the argument *for* (a) rather than a cost of it.

**What D7 exposed, which is larger than D7:** the straddle it guards against **already exists in the live book**. §2.3's own finding is the proof — a `transport: ws` box emits REST-scored rows whenever `ResolveSource` falls back on a degraded feed, with time-averaged OFI reverted to snapshot OFI and the v52 aggressor-velocity modifier not firing. `analysis_log.csv` has 111 columns and **none records transport, WS health, effective source, or a degraded flag**; `InstanceId` identifies a process, not the source that scored a run. So those rows are unmarked and their rate is unmeasured, and any study pooling them dilutes by an unknown amount — #5 and #6 do not fire on them. That reframes (a): **it is not "we keep the book clean by refusing the overlay", it is "the overlay is not where this leaks from."**

**The queued rider that makes (c) available later:** log the effective data source per row. `SignalEmitter.DeriveWsHealth(transportIsWs, degradedThisRun, feedExists, feedConnected)` already returns `OK` / `DEGRADED` / `DOWN` / `REST` every run and ships it in the bridge payload — it simply never reaches the CSV. **A column addition, not a new computation.** It rides the next natural rotation alongside `TriggerMode` and **must never force one**. It buys three things: (c) becomes implementable without re-opening D7; the degradation rate becomes measurable for the first time; and the coverage report's S1 gains a positive per-run source record instead of an inference from a transition-only log. Tracked in `backlog-dependency-map.md`, not here.
