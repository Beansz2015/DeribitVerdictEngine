# LIVE Microstructure Strip — Implementer Spec-Back (P4 #3)

**Built:** 2026-06-29 against `live-microstructure-strip-proposal.md` (APPROVED 2026-06-25, §9 settled).
**Settings:** v44 → **v45** (one new top-level block `live_strip`).
**Scope honoured:** display/**awareness** only — deliberately NOT a verdict. The strip never calls `ScoringEngine.Calculate`, never writes the CSV, no schema change, **no re-baseline**. The engine path is untouched, so the verdict output is byte-identical with the strip on or off.
**Status:** solution(Release) + AutoTweaker + OrderCheck build **0/0**; harness **A1–A18e unregressed + new A19a–e** all pass. **Both §9 post-build VISUAL checkpoints cleared by the trader 2026-06-29** (live LONDON · EXEC 3m). Local commits `e48ed00` (build) + `3bd9990` (checkpoint follow-up) — NOT pushed; trader tests + pushes.

---

## 1. What shipped, file by file

| File | Change |
|---|---|
| `LiveMicrostructureEvaluator.vb` | **New host-agnostic** root file. `MicrostructureSnapshot` + `MicrostructureLevel` + `Evaluate(state, lastRun, cfg, Optional nowUtcMs) → MicrostructureSnapshot`. Reuses the pure `CalcTFI` / `CalcSpread` / `CalcOFI` fns + **carried** last-run levels (`LastSwingHigh/Low5m`, `VPFRNearestHvnAbove/Below`) + a tape-speed window scan. Never throws (Try/Catch → safe blanks). No WinForms. |
| `Core/Settings/EngineSettings.vb` | `LiveStripSettings` POCO (`enabled` / `refresh_sec` / `tape_window_sec`) + `EngineSettings.LiveStrip` property (`<JsonPropertyName("live_strip")>`). |
| `settings.json` | `live_strip {enabled:true, refresh_sec:2, tape_window_sec:10}`; bump v44→v45; `change_log` entry. |
| `UI/MainForm_LiveStrip.vb` | **New** thin WinForms host. `StartLiveStrip`/`StopLiveStrip` (~2s `System.Windows.Forms.Timer`); `OnLiveStripTick` (the §4.1 gate + evaluator call + render); `ComposeLiveStrip` + format helpers; `OnLiveStripCheckChanged` (the "TAPE" checkbox toggle) + per-tick checkbox sync. |
| `UI/MainForm_Layout.vb` | Strip row built in `BuildCardGridLayout` directly under the verdict-header hero row (a 2-col TLP: "TAPE" `CheckBox` + the data `Label`), inserted **after** `_heroRowIndex` is captured; `StartLiveStrip()` in the constructor; `StopLiveStrip()` in `OnFormClosing`. |
| `tools/AutoTweaker/SettingsDiffApplier.vb` | `"live_strip."` added to `RejectedPathPrefixes`. |
| `tools/AutoTweaker/PromptBuilder.vb` | **HARD CONSTRAINT 15** — never propose `live_strip.*`. |
| `verify/ordercheck/OrderCheck.vbproj` + `Program.vb` | Links `LiveMicrostructureEvaluator.vb`; new **A19a–e** fixtures. (gitignored — local-only.) |
| `docs/DeribitIndicatorProject.md` | §6 version → v45 + `live_strip` block note; §15 row. |

---

## 2. Deviations from the proposal (all faithful realisations, flagged for review)

1. **The toggle resolved a real §7-vs-§10 tension — settled at the §9 #1 checkpoint.** §7 says "disabled → hidden"; §10 lists "a toggle (checkbox/menu)". A fully-hidden strip at the spec's `enabled=false` default leaves **no in-app way to enable it for the visual checkpoint**. The first build (`e48ed00`) resolved this with a right-click context menu + an `off · right-click to enable` token. At the checkpoint the trader asked for a **visible control like the existing radios**, so the follow-up (`3bd9990`) made it a **visible "TAPE" `CheckBox`** (mirrors the SINGLE/REPEAT + INTERVAL/ON-CLOSE radio toggles): the data label hides when off so "disabled → hidden" (§7) **does** hold for the readout, while the toggle stays reachable (§10). This is the §9 ⟳ checkpoint working exactly as designed ("may ask for a small display tweak before final confirmation, no re-spec"), not a unilateral design change.

2. **`enabled` default flipped `false → true` at the checkpoint (§9 #1).** The settled value was `false` (opt-in) pending the trader's confirmation of the overall look. The trader reviewed the live render and chose default-on. Both the POCO default and `settings.json` were updated; this is the checkpoint's intended outcome.

3. **The evaluator returns a structured `MicrostructureSnapshot`; the host composes the rendered line.** §4.3/§4.4 split the host-agnostic evaluator (fields) from the WinForms render (string). Kept that split clean: the evaluator emits typed fields + presence flags, the WinForms host (`ComposeLiveStrip`) builds the `·`-separated string. Benefit — the A19 harness asserts the **values**, not display text, so the evaluator stays host-agnostic and unit-testable; the cosmetic string lives only on the form.

4. **Tape-speed `now` is an optional `nowUtcMs` param (default −1 → `DateTimeOffset.UtcNow`).** §4.2 specifies `now = wall-clock` so a lull reads ~0 — production omits the param and uses the wall clock. The optional override exists purely so A19c/A19d can pin a deterministic `now` against fixed trade timestamps. No production behavioural difference.

5. **Spread reuses `CalcSpread` with default thresholds.** Only `spreadBps` is read, and the bps value is threshold-independent (the TIGHT/NORMAL/WIDE status — which the strip doesn't show — is the only threshold-dependent output). So `CalcSpread(book, …)` is called without the cfg threshold args. OFI/TFI **do** pass cfg params per §4.2/§5 (`OFI.BookDepth` reused — no duplicate key, §5).

---

## 3. Behavioural notes

- **Levels are carried, not recomputed.** Read off `_lastSuccessfulIndicators` (the same carried `IndicatorResults` the exit guard uses) — `LastSwingHigh/Low5m` + `VPFRNearestHvnAbove/Below`. Only price/TFI/spread/imbalance/tape-speed recompute live. If no full run has completed yet, levels blank (`--`) and the rest still render.
- **Bracketing rule (§9 #4).** All four carried fields are generic candidates; nearest-above = **min** of the `>price` candidates, nearest-below = **max** of the `<price` candidates, each labelled by source (`SH`/`SL`/`HVN↑`/`HVN↓`) with signed Δ. When price is **below all** carried structure (a down-leg that broke the 5m swing low), only the above-bracket fills and the below-bracket is genuinely empty until the next full run maps a level there — confirmed live (first checkpoint screenshot showed `SL 59860 (+56)` alone; the second showed both, `SL 59662 (−116) | SH 59852 (+74)`, once price sat inside structure).
- **Feed gate.** Renders the readout only when `enabled` AND `_wsFeed.IsConnected AndAlso Not IsCoolingDown` AND book/trades age ≤ `ws_stale_after_sec`; otherwise `WS only`. `transport=rest` (no `_marketState`/`_wsFeed`) → `WS only` — never stale numbers as live (§7).
- **No flicker / sync.** Label text is set only when it changes (§4.4). The checkbox is re-synced to `live_strip.enabled` each tick (covers a `settings.json` hot-reload), guarded by `_liveStripSyncing` so the programmatic `Checked` change doesn't loop back into a re-save. The runtime toggle saves `bumpVersion:=False` (operational; v36 §10a precedent).
- **Independent lifecycle.** Own `System.Windows.Forms.Timer`, started at form load, disposed on close — independent of the auto-run timer, the exit-guard timer, and the on-close watcher. Always-on when enabled (NOT position-gated — useful flat watching a level for entry, or in a hold).
- **OFF the auto-tweaker surface** — `"live_strip."` prefix-rejected in `SettingsDiffApplier` + HARD CONSTRAINT 15 (display preference, no failure-rate linkage — same class as `kelly.`/`exit_guard.`/`auto_run.`).
- **No card-binding obligation** — the strip is a live status-bar element like `BuildWsStatusSegment` / the exit-guard strip, NOT the RTF verdict / `BuildPlaintextSnapshot` / a card (spec §6). The verdict output is wholly unaffected.

---

## 4. §9 settled decisions — all honoured

| # | Decision | Status |
|---|---|---|
| 1 | `enabled` default ⟳ | **false → true** at the checkpoint (trader confirmed the look) |
| 2 | Placement = thin full-width line under the verdict header | ✅ grid row directly under the hero row |
| 3 | Field set/order = price · Δ-to-levels · TFI · spread · book imbalance · tape speed | ✅ |
| 4 | Nearest levels = BOTH above & below ⟳ | ✅ confirmed live (both brackets render inside structure) |
| 5 | Tape speed = both `tr/s` and `$/s` | ✅ |
| 6 | `refresh_sec` / `tape_window_sec` = 2s / 10s | ✅ |

---

## 5. Acceptance (proposal §8)

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck. ✅
- **No scoring change:** A1–A18e unregressed (the strip never calls `Calculate()`). ✅
- **A19a** TFI/spread/imbalance computed via the reused pure fns (BUY PRESSURE / 2.0 bps / bid-heavy). ✅
- **A19b** nearest-level selection — above vs below, nearest wins (`HVN↑ +20` / `HVN↓ −10` bracket a 100000 price). ✅
- **A19c** tape-speed window — recent counted, older excluded (5 in / 5 out → 0.5 tr/s, $10000/s). ✅
- **A19d** lull → 0 (all trades older than the window). ✅
- **A19e** empty buffer → blanks, no throw. ✅
- `transport=rest` / disabled → strip inert; verdict path byte-identical. ✅ (host-gated; engine untouched)
- `LiveMicrostructureEvaluator` references no `System.Windows.Forms`. ✅

---

## 6. Not done / left for the trader

- **Live verification** — the ~2s timer, the checkbox toggle, the under-hero placement, and the render are host glue (the WinForms timer isn't harness-compiled; the evaluator **is**, via A19). Validated by the two live LONDON · EXEC-3m runs in the checkpoint screenshots (both-levels bracketing, TFI/spread/imbalance/tape-speed all live, checkbox on). Trader confirms over a longer stretch, then pushes `e48ed00` + `3bd9990`.
- **Display wave complete.** With #3, the zero-scoring display features from `websocket-migration-proposal.md` §11 are done (#1 exit guard + #2 on-close + #3 this strip + #10 WS-health line already shipped). What remains in P4 are the ⚠ re-baseline indicator upgrades — **#4 time-averaged OFI** is the first, then #5 aggressor-velocity scoring, #6 book absorption — each its own spec + re-baseline.
