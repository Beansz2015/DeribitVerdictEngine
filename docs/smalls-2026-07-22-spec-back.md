# Smalls — 2026-07-22 (three-item spec-back)

Compact record of three small independent items landed the same day. One local commit each, never pushed. gate `-Mode prepush` GREEN (WARN-only) after each. RELEASE-only builds. Zero scoring impact throughout.

Format: eval-no-data spec-back — what, why, deviations.

---

## 1. ws_health.log persistence (W4 row)

**What.**
- New `Core/WsHealthLog.vb` (host-agnostic, never-throws, mirrors `AlertsSidecar`): sidecar `ws_health.log` beside the CSV (`AppDomain.CurrentDomain.BaseDirectory + "ws_health.log"`), append-only, never rotated.
  - `LogStart(state, iid)` — unconditional single line at process start; sets the in-process baseline.
  - `LogTransition(state, iid)` — writes only if `state <> _lastState` (same state twice ⇒ zero lines). First call in a process (Nothing baseline) is treated as a transition and writes.
  - Format: `utc | state | instance_id` (`yyyy-MM-ddTHH:mm:ss.fffZ`, invariant culture, `\n` line terminator).
- Wired into `UI/MainForm_Layout.vb` `New()` after `InitMarketDataSources()`: one `LogStart` call, feed usually not yet connected → seeds baseline honestly.
- Wired into `UI/MainForm_SignalBridge.vb`: new `LogWsHealthTransitionForRun(cfg)` helper called at the TOP of both `EmitBridgeSignal` and `EmitBridgeSkipped`, UNCONDITIONAL on `signal_bridge.enabled` (feed history matters even on the pure-REST configuration). Uses the same `CurrentBridgeWsHealth(cfg)` pinned derivation the payload uses (`SignalEmitter.DeriveWsHealth`), so the sidecar can never disagree with the payload's `health.ws`.
- `CurrentBridgeWsHealth` promoted from `Private` to `Friend` so `MainForm_Layout.New()` can call it before the run path exists.
- No settings keys, no version bump (the file exists whether or not the bridge is enabled).

**Why.**
Closes the roadmap W4 caveat that feed health was inferred from live UI state and never persisted — a soak-review reader had to trust that the strip's status matched what actually happened. Transition-only + process-start gives a tiny append-only ledger that a future post-mortem can grep for `DEGRADED`/`DOWN` bands without adding a run-frequency line to a hot file.

**Deviations.**
- Chose `Friend` (not `Private`) for `CurrentBridgeWsHealth` so the constructor could call it. This is the minimum surface change; no other consumers added.
- Process-start line is emitted from `MainForm.New()` rather than `Program.Main()` because `SettingsLoader.Initialise` + `_wsFeed` construction both happen inside the constructor — logging earlier would require duplicating that setup.
- Fixture family = A38 (next free after A37). Two sub-cases:
  - A38a — same state twice ⇒ one line; each real transition adds one; further repeats after a flip do not.
  - A38b — `LogStart` always writes; format shape `utc | state | iid`; `LogTransition` after `LogStart` at the same state is a no-op (shared baseline).

---

## 2. §6 report clarity — DIRECTIONAL vs NO-TRADE-LEAN sub-tables

**What.**
- `analysis/AnalysisReport.vb`: added `PopulationReport.LeanContextCounts As Dictionary(Of String, Integer)` — per-tag row counts on NO-TRADE rows in this population. No barrier / no outcome for lean rows (NO-TRADE runs are logged `EXCLUDED_NO_PREDICTION`), so we surface counts, not success rates.
- `analysis/AnalysisRunner.vb`: new `ComputeLeanContextCounts(popRows)` populates it (iterates rows where `Verdict` starts with `NO TRADE`, tallies by `VerdictContext`, includes empty-tag as `"(untagged)"`).
- `analysis/MarkdownReportWriter.vb` `AppendContextOutcomes` rewritten to render TWO sub-tables per session:
  - (a) **DIRECTIONAL** — the existing `ContextOutcomes` (CONFIRMED / MOMENTUM_FADING / STRUCTURALLY_WEAK / FLOW_UNCONFIRMED on committed directional rows, per D7 filter). Success% + n + Wilson CI.
  - (b) **NO-TRADE LEAN** — counts of `VerdictContext` tags carried on NO-TRADE rows (ALIGNED plus any others). n only — no barrier for these rows.
- One caption line above the sub-tables: **"These two sub-tables are NOT comparable. (a) measures committed-directional outcomes; (b) is lean-drift on rows that never traded. Juxtaposing the two produced the D7 CONFIRMED-inversion twice (2026-06-24, 2026-07-21)."**
- Offline logic/population/matrix cell space UNCHANGED — this is render segmentation only. The existing `ContextOutcomes` dict is populated exactly as before.

**Why.**
Discharges D7 spin-off 2 (`docs/d7-confirmed-reread-2026-07-22.md` §8 classification) — the report §6 that RE-OPENED D7 juxtaposed directional-context success rates with a lean-tag baseline that has no committed outcomes at all. The current single-table shape encourages a reader to compare them; the two-sub-table split makes the incompatibility structural.

**Deviations.**
- Fixture check for existing A34/A35 pins on §6 text: none. `grep -n "§6\|Verdict Context Tag\|## 6\." verify/ordercheck/Program.vb` returned only §6-in-comment references, no text-pinning fixture. So no re-pinning needed; added **A34f** asserting both sub-table headers, the caption (naming the D7 dates 2026-06-24 + 2026-07-21), a directional row with `n=50`, both lean rows with `n=42` / `n=3`, and count-descending order (ALIGNED before untagged).
- "Any tag on NO TRADE rows" includes the empty string case (a NO-TRADE row with `VerdictContext=""`), rendered as `(untagged)` in the lean table so the total lines up with the population's NO-TRADE row count.
- `AnalysisPopulationReport.LeanContextCounts` is a new POCO field but zero settings impact (analysis is offline-only).

---

## 3. WhatIfRunner overlay recipes (docs-only)

**What.**
- New directory `tools/WhatIfRunner/overlays/` with three files:
  - `geometry-study-36cell.json` — the exact 36-cell geometry overlay used in the current geometry re-read (target/stop arbitration mode × target/stop buffer_pct sweep).
  - `target-bound-sweep.json` — `target_max_atr_mult` sweep 1.25 → 3.5 step 0.25.
  - `README.md` — names both files' purpose and pins the re-run command (`WhatIfRunner <overlay> --csv <frozen copy> [--from date]`).
- Overlay JSON payloads use the `sweep: {from, to, step}` shape the shipped `WhatIfOverlay.Parse` already recognises (A36e whitelist).

**Why.**
The geometry-study re-read was depending on ephemeral session-scratchpad overlays. Committing the two recipes anchors them into the repo so a later re-read reproduces byte-identically without hunting a scratchpad path.

**Deviations.**
- Verified root `DeribitVerdictEngine.vbproj` has no default `Content Include` glob for `**/*.json`; the .json files are compile-invisible. `dotnet build` after the add stayed 0 error / 0 warning (Release).
- Docs-only commit — no engine path changed, no version bump (gate version-nudge = "no engine-path change", no WARN).

---

## Gate results (verbatim quotes)

Each commit was followed by `pwsh -File tools/checks/verify-gate.ps1 -Mode prepush`. Results recorded in commit-body notes; the full gate output (build + harness + parity/version heuristics) tail is at the bottom of each commit message. WARN-only outcomes are the D6-precedent accepted state (engine-path change without a settings.json version bump is a nudge, not a hard failure).

## Open questions

- None. All three items are self-contained; each of the four warn-check axes (parity, version, buildwarn, harness) came out either OK or the accepted WARN.
