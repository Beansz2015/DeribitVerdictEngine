# #7 Liquidation-Cascade Alarm + #8 Level-Approach Alerts — Build Spec-Back

**Date:** 2026-07-22 · **Settings:** v58 → **v59** · **Scope:** the H1–H5 build (H4 AMENDED — the first-liq-seen event is PERSISTED to a sidecar `liq_events.log` beside the CSV; the strip-tooltip flag reads the file's existence, so it survives restarts). Spec of record: `liq-cascade-level-alerts-proposal.md` (H1–H5 all ticked 2026-07-22). Display/alert only — ZERO scoring impact.

Everything below is either a confirmation that the build matches the proposal, or a **deviation/decision** the proposal did not pin. Deviations are numbered for the coordinator review.

## 1. What shipped (map to proposal §1–§4)

| Proposal item | Shipped |
|---|---|
| #7 cascade alarm (≥ `cascade_min_trades` liq-flagged in `cascade_window_sec`, either side) | ✅ `Core/AlertsTracker.vb` — time-stamped ring of liq-flagged trades, age-pruned per Fold + Snapshot; dominant side (by USD) names `CASCADE_ABOVE` / `CASCADE_BELOW` |
| #8 level-approach alerts (within `level_ticks` of a CARRIED level; re-arm on leave) | ✅ per-side approach episode state machine — the #6 absorption-episode pattern; carried candidates = the SAME `SetAlertsLevels(...)` refresh site that #6 uses |
| H1 — TAPE-strip tag + status-bar flash | ✅ `MainForm_LiveStrip.ComposeCascade` / `ComposeApproach` render compact `LIQ↑ 3× ($2.1M)` / `NEAR SH 60103 (7t)` tags while active; the strip's `ForeColor` swaps to `Theme.ACC_WARN` for a ~6 s flash on each pending event AND while an alert state is active |
| H1 `sound_enabled` (default OFF) | ✅ `System.Media.SystemSounds.Exclamation.Play()` fires on `CASCADE` events only; never on `FIRST_SEEN` (opt-in per §3) |
| H2 anchors `cascade_min_trades:3 / cascade_window_sec:10` | ✅ verbatim (POCO defaults = settings.json = spec) |
| H3 anchors `level_ticks:12` | ✅ verbatim (matches the #6 `proximity_ticks` anchor — one mental model) |
| H4 first-liq-seen diagnostic (AMENDED — PERSISTED) | ✅ `AlertsSidecar` in `Core/AlertsTracker.vb` — `File.AppendAllText`, never rotated, never throws (the `SignalEmitter.TryWrite` discipline) — see §2.1 |
| H5 fixtures | ✅ `verify/ordercheck/Program.vb` — new **A37a-e** family (details §3) |

## 2. Deviations & decisions

**2.1 `liq_events.log` path resolution.** The proposal says "beside the CSV". Shipped: `AlertsSidecar.GetPath()` = `Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "liq_events.log")` — the SAME rule `AnalysisLogger.GetLogPath()` uses, so the two files always co-locate WITHOUT the tracker coupling to `AnalysisLogger` (host-agnostic constraint — the CLI port must not carry the CSV writer to get the alert sidecar). The file is created on first append (`Directory.CreateDirectory(dir)` guards the empty-dir case). Never rotated, never truncated — the file is the durable A4 gate evidence and the rotate-back-to-zero would erase it.

**2.2 Cascade direction convention.** The proposal says "≥3 liq-flagged trades in 10s (either side)". Shipped names a direction on fire: buy-side liquidations (which are actually SHORTS being stopped out) name `CASCADE_ABOVE` (shorts squeezed → price impulses UP); sell-side liquidations name `CASCADE_BELOW`. Dominance is by USD, not count — one $2 M SELL-liq matters more than three $10 k BUY-liqs. This lets the strip tag render an ↑ / ↓ arrow (the #6 precedent) without inventing a new convention.

**2.3 CASCADE edge-detector re-arm.** The proposal implies the tag renders while the condition holds. Shipped: the CASCADE **event** (audible cue + sidecar line + flash) edge-fires ONCE per crossing of the threshold — the `_cascadeActive` bool. Re-arms when the window count drops back below `min_trades`. The **strip tag** itself renders continuously while the count sits at/above threshold (a live status readout). Result: one audible cue per real cascade, not one per fold; the tag stays visible for the trader as long as the tape is still liquidating.

**2.4 Level-approach: nearest carried level per side.** The proposal says "within `level_ticks` of an active CARRIED level (the TAPE strip candidate set)". Shipped: per side (ABOVE / BELOW), pick the NEAREST carried candidate to price on the correct side (max of `<price` candidates for BELOW, min of `>price` for ABOVE) — the same rule `LiveMicrostructureEvaluator.FillNearestLevels` uses, so the strip's bracketing levels and the approach episodes name the same levels by construction.

**2.5 Approach episode close on level re-map.** The proposal names "re-arm on leave" as the discipline. Shipped ALSO closes an active episode when `SetLevels` re-maps that side (the level identity check in `AlertsTracker.SetLevels`). Otherwise a 5m swing that shifts mid-approach would carry a stale episode reading against the new level — the #6 no-cross-level-bleed principle applied to a display-only surface. A37b pins this.

**2.6 `FIRST_SEEN` per-process AND file-persistent.** H4 amended says "written on the first liq-flagged trade ever observed". Shipped fires the sidecar append ONLY when BOTH conditions hold: (a) the per-process `_firstSeenWritten` flag is False, AND (b) `File.Exists(sidecar)` is False. So a restart on a machine that has an existing sidecar does NOT re-append a spurious FIRST_SEEN row — the file's earlier `FIRST_SEEN` line is the durable proof. Snapshot then reports `LiqEverSeenThisProcess = _firstSeenWritten OrElse SidecarFileExists()` — same file-existence check, so the strip tooltip appears immediately on a restart after prior events.

**2.7 Snapshot drains pending events.** The tracker maintains an internal `_pending` list of events fired since the last read; `Snapshot(...)` copies them into the returned `AlertsSnapshot.PendingEvents` and clears the internal list. Rationale: keeps the write-to-sidecar half in the tracker (host-agnostic, one owner) and moves the audible-cue + flash half to the WinForms host (`MainForm_LiveStrip`) without duplicating the append. If the strip tick misses events (e.g. the timer paused mid-cascade), the sidecar still has them — the file, not the in-memory list, is the durable record.

**2.8 Reset preserves the sidecar; clears in-memory pending.** `AlertsTracker.Reset()` clears the ring, cascade edge, approach episodes, pending list, carried levels, and last price — but leaves the sidecar file on disk. `DeribitWsFeed.SeedAsync` calls it on every reconnect (the #4/#5/#6 discipline). `_firstSeenWritten` also stays TRUE across a reconnect within the same process — the sidecar sentinel is a per-PROCESS-lifetime guarantee, not per-connection. A37d pins the Reset semantics.

**2.9 Sidecar line format.** Per H5-spec-back: `utc | kind(FIRST_SEEN|CASCADE) | side | usd | instance_id`, one event per line. UTC is `yyyy-MM-ddTHH:mm:ss.fffZ` (millisecond precision, Z suffix — the ISO-8601 pattern the bridge uses); USD is `F0` (integer USD); fields joined by `" | "` (space-pipe-space). `Culture.InvariantCulture` for both timestamp and USD — no locale drift. A37c pins the shape.

**2.10 Tweaker fence is a PURE PREFIX `alerts.`.** All five keys share it, so exact-match rules are unnecessary (mirrors the shape of `exit_guard.` / `live_strip.` / `signal_bridge.`). The prefix is safe because no other top-level `alerts.` keys exist. `PromptBuilder` HARD CONSTRAINT 25 states the whole block is display/alert plumbing with zero failure-rate linkage.

**2.11 Display-string parity statement.** No snapshot / card / CSV line is added, removed, renamed, or reformatted. The two new display elements — TAPE strip tags (LIQ / NEAR) and the strip ForeColor flash — are **live status elements**, the exempt class the display-parity rule names (the #3 / #5 / #6 precedent). Stated here AND in the commit message per the CLAUDE.md hard rule.

**2.12 IndicatorResults untouched.** No new fields on `IndicatorResults` and no CSV column additions — the alerts state is read directly from `MarketState.GetAlerts(...)` by `LiveMicrostructureEvaluator.Evaluate(...)` (the strip's own path). The alerts are ephemeral live-status, not per-run measurements — logging them into the CSV would blur the display/measurement boundary the proposal draws.

**2.13 Never-throw guarantees.** Both `AlertsTracker.FoldTrade` and `AlertsSidecar.TryAppend` are wrapped so that ANY exception logs to console + returns cleanly — the tracker is an observer, and a broken observer must never disrupt the run path (the `SignalBridge.EmitBridgeSignal` discipline the coordinator brief called out). A37c pins the null-input and general never-throw contract.

**2.14 Manuals untouched.** UserManual / TraderGuide gain no obligation from a strip-only display surface (the #3 / #5 / #6 precedent). The TAPE tags + sidecar path should fold into the next manual refresh, alongside the pending updates already flagged.

## 3. Acceptance record

- **Builds:** solution (Release) + AutoTweaker (Release) + WhatIfRunner (Release) + OrderCheck (Release) — **0 warnings / 0 errors**. Release-only throughout (Debug bin untouched — collector runs the Release exe).
- **Harness:** ALL PASS — A1–A36f unregressed, new **A37a–e**:
  - **A37a** cascade window math (2 non-liq trades → NONE / 2 liq trades under threshold → NONE with FIRST_SEEN drained / 3rd liq trade above threshold → CASCADE_ABOVE, edge-fires once / 4th liq trade in window → still CASCADE, NO double-fire / advance past window → NONE re-arm).
  - **A37b** level-approach lifecycle (outside band → inactive / within 12 ticks → ABOVE active / leave-proximity → re-arm / re-enter → new episode / carried-level re-map → mid-episode close per §2.5).
  - **A37c** sidecar append shape — 5-field `" | "`-split contract, ISO-8601 `Z` timestamps, F0 USD; null event never-throws; two events land as two lines, append-only.
  - **A37d** `Reset()` clears cascade + pending; `Enabled = False` cfg makes Fold + Snapshot inert (the byte-identical rollback).
  - **A37e** `alerts.*` HC25 fence — five keys rejected with "off-tweaker-surface"; sibling `scoring.verdict_med_pct` still passes.
- **verify-gate:** `-Mode prepush` GREEN (run at commit).
- **Reversibility:** `alerts.enabled=false` ⇒ Fold + Snapshot early-out; strip renders no alert tags; sidecar path is only created on the FIRST event ever, so a disabled system never touches disk — byte-identical to pre-build at every reachable surface.

## 4. Open items (not this build's scope)

- **A4 unlock check.** The scoring gate remains locked; the unlock predicate is now `File.Exists(liq_events.log) AndAlso file contains >= 1 CASCADE line`. Trader flips A4 live only after the first real cascade has been observed AND the CASCADE line is inspected against the market context (proposal §1).
- **Re-anchor after the first real cascade.** `cascade_min_trades` and `cascade_window_sec` are PROVISIONAL — the proposal says explicitly "re-anchor after the first real one" (§3 H2 rec). No calibration data can seed them today; the sidecar's stored CASCADE events are the seed for the re-anchor pass.
- **Sound cue tuning.** `sound_enabled` defaults OFF; if the trader flips it on and finds the exclamation chime wrong, the shape switches (SystemSounds → a bundled wav) become a follow-up display commit — no engine impact.

## 5. Addendum — v59 open-options rulings + follow-up build (2026-07-22)

The v59 alerts build (commit `46e7614`) left four open decisions in §4. Trader ruled:

1. **Cascade tag colour — SHIPPED as an ACC_CASCADE accent.** New palette token `Theme.ACC_CASCADE = Color.FromArgb(225, 29, 72)` (rose-600). Deliberately magenta-tinted so it is distinct from the verdict ramp's pure-red shorts (`ACC_STRONG_SHORT 239,68,68` / `ACC_SHORT 248,113,113` / `ACC_WEAK_SHORT 252,165,165`, all green=blue) and from the amber attention token (`ACC_WARN 251,191,36`). `MainForm_LiveStrip` picks it whenever the cascade signal is active OR the new `_cascadeFlashUntilUtc` window is open; other alert flashes (FIRST_SEEN, level-approach) keep `ACC_WARN`. Flash-source tracking added: any pending event still extends `_alertFlashUntilUtc`, but CASCADE events ALSO extend `_cascadeFlashUntilUtc` — so a transient cascade keeps painting the strip rose for the ~6-s tail even after the underlying signal clears. Strip is a single Label, so the whole strip tints (the LIQ tag effectively renders in the cascade accent by construction) — precedent-consistent with the P4 #3 strip's single-ForeColor design.
2. **Approach candidate set extended to include 15m swings — SHIPPED.** `AlertsTracker.SetLevels` grew from 4 → 6 args (`swingHigh5m, swingLow5m, hvnAbove, hvnBelow, swingHigh15m, swingLow15m`). `MarketState.SetAlertsLevels` grew in step; the sole call site in `UI/MainForm_Analysis.vb` now passes `r.LastSwingHigh15m` / `r.LastSwingLow15m`. Nearest-per-side selection (`NearestAbove` / `NearestBelow`) runs over all six candidates under the same max-below / min-above rule; episode re-map identity check (`IsCandidateAbove`) spans the same six. Fixture **A37b** re-pinned as 6-arg and extended with an added case that pins a 15m swing at 60150 with the 5m swing farther at 60300 — price 60148 fires the ABOVE episode against the closer 15m level, proving 15m candidates participate in the selection (not just the identity check).
3. **`FIRST_SEEN` bell — LEFT AS-IS.** `System.Media.SystemSounds.Exclamation` still fires on CASCADE events only when `sound_enabled=true`. FIRST_SEEN remains sidecar-only (silent, visual amber flash), matching the H4-amendment intent.
4. **In-app sidecar viewer — LEFT AS-IS.** No new UI. Sidecar remains inspectable via any text editor at `<BaseDirectory>\liq_events.log`; the persistent tooltip on the TAPE label still cites the path once the file exists.

**Not changed:** no `settings.json` keys added or renamed (no version bump — code + theme only; the verify-gate `version-bump` check passed on this run because the diff range still carries the v58→v59 bump from `46e7614`). No CSV / snapshot / card / bridge-payload field added, removed, renamed, or reformatted — the display change is a colour swap inside the exempt live-status class (§2.11). No manuals obligation (§2.14). Acceptance: solution + AutoTweaker + WhatIfRunner + OrderCheck build **0/0** Release; harness **ALL PASS** (A37b amended, A37a/c/d/e unchanged, A1–A36f unregressed); verify-gate `-Mode prepush` **GATE PASSED** (build + harness + display-parity + version-bump all OK).
