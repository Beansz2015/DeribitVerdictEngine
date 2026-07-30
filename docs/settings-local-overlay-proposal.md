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
- **It survives builds**, which is the entire point: it is not a project item, so nothing copies over it. `dotnet clean` removes it along with the rest of `bin\` — documented, not defended against.
- **Absent overlay ⇒ byte-identical to today.** The merge branch is skipped entirely.

### 1.1 Hot reload

`SettingsLoader` already watches `settings.json` with a `FileSystemWatcher`. The watcher must also fire on `settings.local.json`, and a reload must redo the merge from both files — otherwise editing the overlay appears to do nothing until restart, which is exactly the kind of quiet surprise this feature exists to remove.

### 1.2 `Save()` — the load-bearing detail

`SettingsLoader.Save(cfg, changeNote)` writes `Current` back to `settings.json`. **With an overlay active, the naive implementation writes the merged values into the tracked file** — promoting a local-only override into the shared file, which then propagates to AWS on the next deploy. For `trade_store.enabled` that is precisely the catastrophic direction the checklist §1a asymmetry warns about: the local box's "don't capture" would silently become AWS's.

So `Save` must operate on the **base** document, never the merged one: keep the parsed base `JsonNode` in memory, apply the caller's changes to it, write that, then re-merge the overlay on top. The live UI saves (`MIN NET MOVE %`, output-dump settings) all route through `Save`, so this is not a corner case — it is the normal path.

**This is the single most important thing to get right in the build, and A50c exists for it.**

---

## 2. What may be overridden — a whitelist, and why not "anything"

An unrestricted overlay would silently break the **same-settings pooling discipline** (`aws-collector-deploy-checklist.md` §4.5): rows from the two boxes are only comparable while both run the same settings, and the CSV's settings-version column is what makes a straddle visible and filterable. An overlay changes behaviour **without changing the version**, so an unrestricted one would make two boxes disagree on scoring while both stamp the same version — producing an invisible, unfilterable straddle in the pooled book. That is a worse failure than the chore this spec removes.

The whitelist is therefore **exactly the set of top-level blocks already fenced off the auto-tweaker surface for having no failure-rate linkage** — the same justification, the same list, no new judgement call:

| Block | Fenced by | Why it is per-box-legitimate |
|---|---|---|
| `trade_store.` | HC27 | The originating case (D1: AWS captures, local does not) |
| `signal_bridge.` | HC18 | Only one box has a consumer |
| `alerts.` | HC25 | Audible cues on an unattended box are pointless |
| `live_strip.` | HC15 | Display preference |
| `exit_guard.` | HC13 | Display/alert preference |
| `auto_run.` | HC14 | Run cadence is operational |
| `network.` | HC12 | Transport plumbing; a box may need `transport: rest` |
| `performance_display.`, `analysis_logging.` | — | Display/logging only, no scoring path |

Anything else — `scoring.`, `indicators.`, `session_volume.`, `resolution_profiles.`, `regime_*`, `kelly.`, and `version` — is **rejected**, loudly (§3).

**The alignment is not a coincidence and is worth stating as the rule:** *a key is safe to diverge per box exactly when it is safe to keep off the auto-tweaker surface* — both mean "this cannot move the failure rate." One list, two uses. A future block that gets an HC fence should join this whitelist in the same commit.

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

Build acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0 Release**; A1–A49x unregressed + A50a–g; verify-gate `prepush` **GATE PASSED**.

**One build-time question to settle then, not now:** `SettingsLoader.vb` is on the engine path, so the gate's version-bump guard will want a bump for a change that adds no keys. The `[no-engine-change]` commit token used for the §7.5 VWAP anchor edit is the precedent; confirm it applies before committing.

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
| **D1** | Whitelist the overridable blocks, or allow anything? | (a) whitelist = the HC-fenced non-scoring blocks · (b) unrestricted · (c) whitelist just `trade_store.` | **(a).** (b) breaks pooling invisibly — two boxes disagreeing on scoring while stamping the same settings version is an unfilterable straddle, worse than the chore this removes. (c) solves today and gets re-opened at the next key. The HC-fence alignment means (a) needs no new judgement call. |
| **D2** | File name / location. | (a) `settings.local.json` beside `settings.json` in the exe dir · (b) a `--settings` CLI arg · (c) an env var | **(a).** It survives builds, which is the actual requirement; (b)/(c) do not survive a Startup-folder shortcut or a reboot, which is how the collector runs. |
| **D3** | Whitelist violation: ignore-and-log, or refuse to start? | (a) ignore + log loudly · (b) fatal | **(a).** A box that will not boot loses more data than a box running shared settings — and on AWS, "will not boot" means silent tape loss, the exact failure D1 is about. |
| **D4** | Show the overlay in the title bar? | (a) `settings v{N} +local` · (b) console only | **(a) plus console.** Every defect chased in the last two days was silent divergence; an override mechanism that hides itself is one more. Parity-exempt as a live status element. |
| **D5** | Does the auto-tweaker need to know about the overlay? | (a) no — it reads/writes the base, unchanged · (b) make it overlay-aware | **(a).** Its whole surface is scoring keys, which §2 forbids overriding, so a tweaker-visible overlay is impossible by construction. Worth re-checking if D1 goes to (b). |
| **D6** | Apply the same overlay to `tweaker_config.json`? | (a) no, out of scope · (b) yes | **(a).** The tweaker runs on one box. Adding a second overlay surface with no demonstrated need is how a small feature becomes a subsystem. |
