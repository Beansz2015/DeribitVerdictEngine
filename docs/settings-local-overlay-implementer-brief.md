# `settings.local.json` overlay — Implementer Brief (fresh conversation handoff)

**Date:** 2026-08-01 · **Status:** build-authorized once the trader ticks **D1–D6** (D7 was answered 2026-07-31 by the orchestrator seat — option (a), not a permanent close) — **⚠ read §1 before ticking, D1 changed materially on 2026-07-31.** · **Model/effort: Opus, medium — one conversation.** Coordinator review after (this seat).
**Not a scoring change, no dataset boundary, no settings keys, no version bump.** Code-only in `Core/Settings/SettingsLoader.vb` + one `.gitignore` line. Run the CLAUDE.md session-start protocol first.
**Verified unbuilt 2026-08-01:** `SettingsLoader.vb` contains no overlay, merge, or `settings.local` handling of any kind. This is a from-scratch build.

## Why it is on the critical path

It is the clean way to land v64. `trade_store.enabled` ships `true` — correct for AWS, wrong for the local box, which D1 ruled out of the capture topology. `settings.json` is `CopyToOutputDirectory=PreserveNewest` and the tracked file is already newer than `bin\Debug`'s, so **the build that tests v64 is the build that starts local capture** (~900 MB/yr into an unwatched directory). This overlay is what stops that, and the F6 ruling is named in the spec's own header as its origin. **Nothing else in the v64 landing can proceed until this is decided.**

## Specs (read in order)

[`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md) — the spec · [`overlay-whitelist-reaudit-2026-07-31.md`](overlay-whitelist-reaudit-2026-07-31.md) — **the second-pass re-audit; §2.2/§2.4 of the spec are wrong in three places and this is the correction of record** · [`settings-local-overlay-review-2026-07-31.md`](settings-local-overlay-review-2026-07-31.md) — the first review (context; its findings are already folded in).

## 1. Corrections that override the spec as written

The spec was revised once already against the first review. A **second** pass found more, and the spec carries a correction block at §2 pointing here. **Implement the corrected version:**

1. **`alerts.` is REJECTED, not admitted.** `AlertsTracker.FoldTrade` returns at `Core/AlertsTracker.vb:116` when `Not cfg.Enabled`, and the sidecar writes live *inside* that method (`:143` FIRST_SEEN, `:157` CASCADE); `DeribitWsFeed.vb:439` skips the fold entirely. So `alerts.enabled:false` ⇒ **`liq_events.log` is never written** — and that file is the sole gate on **A4**, with both boxes' sidecars pooled. Do not admit it.
2. **`mtf_gate` must be named explicitly in the reject list**, and **D1's allow-list constraint must be stated in code**. `settings.json` has 17 settings blocks; the spec's enumeration covers 16. The missing one is the **hard veto**. The catch-all protects it only if the implementation is an allow-list — build it as one, and do not rely on the prose.
3. **`performance_display.` stays admitted but is not "Clean"** — it gates the eval cache, the OHLC cache and the gap-fill outright (`LivePerformanceTracker.vb:188-191`, `:494`, `:261-266`). Admit it, and put a comment at the whitelist entry recording that it is admitted *because no queued decision reads the eval cache*, so a future seat knows what to re-check.
4. **§1.2 lists two live-save paths; there are four.** Also UI-writable: `live_strip.enabled` (`UI/MainForm_LiveStrip.vb:341-343`) and `performance_display.metric_mode` (`UI/MainForm_Layout.vb:1689-1691`), on top of `MIN NET MOVE %` and the output-dump form. On an overlaid box these become one-way mirrors — the click writes the **shared tracked file that gets xcopied to AWS** while the overlay keeps winning locally, and the `live_strip` checkbox visibly snaps back within ~2 s (the refresh tick re-syncs from merged config at `:90-93`).

## 2. Scope

1. **Merge** — after loading `settings.json`, if `settings.local.json` exists **in the same directory**, deep per-key merge over the base on `JsonNode`, then deserialise once into `EngineSettings`. Arrays replace wholesale. Absent overlay ⇒ byte-identical to today (skip the branch entirely).
2. **`Save()` operates on the BASE, never the merge** (§1.2). Keep the parsed base `JsonNode` in memory, apply the caller's change to it, write that, re-merge the overlay on top. **This is the single most important thing to get right** — inverting it promotes a local override into the shared file and from there onto AWS. A50c exists for it.
3. **Hot reload** — the existing `FileSystemWatcher` must also fire on `settings.local.json`, and a reload redoes the merge from both files.
4. **Whitelist, allow-list by construction, key-granular.** Admitted whole: `trade_store.` · `signal_bridge.` · `live_strip.` · `exit_guard.` · `performance_display.` · `analysis_logging.` **(six — `alerts.` removed per §1.1).** `network.` admitted per key: `request_timeout_seconds`, `retry_count`, `retry_backoff_ms`, `ws_url` only. Everything else rejected, **`mtf_gate` and `alerts.` named explicitly**.
5. **Visibility (§3)** — title bar renders `settings v{N} +local`; one startup console line naming every key the overlay actually changed, before→after; rejected keys log `[SettingsLoader] settings.local.json: 'x' is not overridable — IGNORED` and are **non-fatal**.
6. **`.gitignore`** — add `settings.local.json`.

## 3. Constraints

Local-first, **NEVER push** — trader tests and pushes. **Release-only builds while the collector runs** (`bin\Debug` is the live collector; `verify-gate.ps1` is Release-only at line 50, which is the hatch). **Run `verify-gate.ps1 -Mode prepush` AFTER committing** — it derives its changed-file set from committed diffs, so a pre-commit run passes `display-parity` and `version-bump` **vacuously** and still prints `GATE PASSED` (the v64 F5 lesson). Title bar is a live status element ⇒ **display-parity exempt, but say so in the commit message.** Spec-back `settings-local-overlay-spec-back.md` with every deviation.

## 4. Acceptance

Fixture family **A50** (reserved for this build; A49 reserved for the coverage report, next free after is **A52** — A51 is consumed). All six Release builds 0/0; A1–A51e unregressed; `verify-gate prepush` **GATE PASSED post-commit**.

A50a absent overlay ⇒ byte-identical settings tree · A50b deep merge flips one key, siblings and other blocks survive · **A50c `Save` writes the BASE, not the merge** (the regression trap) · A50d whitelist rejects `scoring.*`, `indicators.*`, `version` **and `mtf_gate.*` and `alerts.*`** with a logged reason, base value survives, startup succeeds · A50e malformed overlay ⇒ logged, ignored, app starts · A50f hot reload re-merges; deleting the overlay reverts without restart · A50g arrays replace · A50h scoring-surface pin — a `scoring.*` overlay leaves the verdict byte-identical through the **real** `Calculate()` (A42a/A36a pattern) · A50i `network.transport` and `auto_run.trigger_mode` rejected while `network.request_timeout_seconds` is admitted · **A50j (NEW, re-audit §F4) — whitelist ∩ UI-writeback:** a `Save` triggered from a UI path whose block is overlaid writes the base and leaves the overlay winning, with no silent promotion.

## 5. Cross-spec dependency to honour

[`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md) **D7 must read the MERGED value**, not the base file — reading the base would see `enabled:true` and report every up-hour on the local store as a capture defect. That spec builds second and inherits this; it is noted in both.

## 6. After the build — the trader's landing sequence

Deliberately **not** the implementer's job, recorded so the build is shaped for it: place `settings.local.json` in `bin\Debug\net8.0-windows\` containing `{"trade_store":{"enabled":false}}` **before** the first post-overlay Debug build. Then `PreserveNewest` can copy the v64 tracked settings and the merge still resolves capture to false, so local capture never starts. The gap in between is harmless — the currently-running binary is pre-v64 and has no capture code at all.
