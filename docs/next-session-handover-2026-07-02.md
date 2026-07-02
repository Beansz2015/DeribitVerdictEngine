# Next-Session Handover — 2026-07-02 (AUDIT + CODE-TRACE mission)

**For:** a fresh conversation (Fable 5) tasked with a **full audit and code trace** of the DeribitVerdictEngine.
**Supersedes** `next-session-handover-2026-07-01.md` (that was the coordinator/spec-author "continue the work" handover; this one is a dedicated audit brief).
**Author:** Opus 4.8 seat, 2026-07-02. Everything below is front-loaded so you can start the audit without re-exploring — you're a fresh context and (per the trader's plan) turns are costly, so read this in full first, then the start-protocol docs, then trace.

---

## 0. Start protocol — do this first (you are a fresh conversation)

Per `CLAUDE.md`, before touching code:
1. Read `docs/DeribitIndicatorProject.md` **in full** (~540 lines / ~49K tokens — it exceeds a single Read cap, so page through it: offsets 1 / 235 / 470). It's the authoritative project handover: purpose, file inventory (§3), indicator map (§4), settings pointer (§6), version history (§15), backlog (§16.6).
2. Read `docs/architecture.md` **in full** — directory layout, the single-run data-flow diagram, settings data-flow, and the **Design Decisions** + **Display Behaviour Clarifications** tables (the latter is your "do not re-flag" list — §9 below).
3. Load the `crypto-trading-context` skill (carries the trader profile + writing style). Don't separately read `docs/trader-profile.md`; the skill loads it.
4. Read the frontier memory `project_websocket_migration` (the WS→P4 arc through geometric v46 + the on-close fix) and skim the memory index `MEMORY.md`.
5. `git log --oneline -20` + `git status`.
6. **Do not** read individual `.vb` files at session start — open them only when the trace/audit reaches them.

---

## 1. The mission

A **full audit + code trace.** The trader will specify the exact focus; this doc gives you (a) the trace spine, (b) the prioritized audit targets, (c) the invariant checklist that defines "correct," (d) how to verify a claim, and (e) what is intentional and must **not** be reported as a bug. Suggested method:

- **Trace the single-run flow end-to-end** (§5) — follow one `RunAnalysisAsync` from click/timer to CSV row, naming every transform.
- **Check each layer against the invariant checklist** (§7). An invariant violation is a real finding; a style nit is not.
- **Run the gate** (§8) to establish the current state is green before you start, and to validate any fix.
- **Cross-check `settings.json` ↔ POCO** (§6E) and **display parity across the 3 surfaces** (§6G) — these are the two drift classes this codebase has actually shipped bugs in.
- **Verify the OFI geometric math by hand** (§6B) — the most recent scoring-path change.
- **Report findings faithfully** — if something is uncertain, say what's uncertain. Don't upgrade "looks odd" into "is a bug" without a concrete failure scenario. Don't fix scoring/novel behaviour without a spec + trader sign-off (§10).

---

## 2. What the engine is (orientation)

VB.NET / .NET 8 **Windows Forms** desktop app. Polls Deribit (now over **WebSocket**, REST fallback) for BTC-PERPETUAL, computes technical indicators on 1m/3m/5m/15m candles, scores them through a multi-tier **additive** pipeline, and emits a directional verdict (STRONG LONG → STRONG SHORT / NO TRADE) with ATR-based entry/stop/target levels and a display-only Kelly sizing advisory. All thresholds live in `settings.json` (no magic numbers in code). There is **no unit-test suite** — verification is the **OrderCheck harness** (host-agnostic fixtures A1–A20i) + live runs + the `analysis_log.csv` calibration log. Full data-flow diagram: `architecture.md`.

---

## 3. Current state / frontier

- **settings.json = v46.** Engine is **live on WebSocket** (`network.transport=ws`) since the P3 cutover (v42).
- **v46 = geometric (log-ratio) time-averaged OFI** — shipped + pushed. The single-snapshot book imbalance was replaced by a time-weighted average folded feed-side; the averaging space is **geometric** (`Ratio = exp(EMA(ln ratio))`) after a DIAG test showed an arithmetic mean manufactured a 12:1 buy-skew on a net-flat session (AM≥GM shape bias). See `docs/time-averaged-ofi-*.md` + `docs/ofi-geometric-construction-*.md`.
- **Immediate frontier = the v47 OFI dominance-threshold re-baseline** (data-gated, not yet run). This is *why* `indicators.OFI.buy_dominant_ratio`/`sell_dominant_ratio` still read **2.0 / 0.5** — those are the pre-averaging values; the firing-rate-match to re-derive them awaits data. **Data state (2026-07-01):** only ~94 geometric rows exist (one partial day, Asia-tail + London-morning, **zero NY×1**) — insufficient; needs ~2–3 more weekday session-days including NY. So the OFI thresholds being "un-retuned" is **expected**, not a finding.
- **P4 #5 aggressor-velocity** — spec written this session (`docs/aggressor-velocity-proposal.md`), **not built**, sequenced after v47. Display trio #1 exit-guard (v43) / #2 on-close (v44) / #3 live strip (v45) all shipped.

---

## 4. Git / push state (read before you judge "uncommitted" anything)

- **origin/master = `a00ac35`** — the geometric v46 stack + dev-workflow automation + on-close backstop fix + doc refresh, **all pushed** (the 2026-07-01 handover said this was "unpushed"; that was corrected this session — it's on origin).
- **local master = `a504768`, 5 commits AHEAD of origin, ALL docs-only** (the P4 #5 spec + 2 revisions + backlog P13/P14). Unpushed — **the trader pushes, never you** (§10).
- **Therefore the code you audit == origin/master `a00ac35`.** The 5 local commits touch only `/docs`; there are **no uncommitted code changes**. `git status` will show 3 pre-existing **dirty docs** (`p3-maintenance-pass-proposal.md`, `ui-reskin-handover-2026-05-22.md`, `websocket-migration-p1-spec-back.md`) and **untracked non-engine files** (`.codex/`, `configure-claude-deepseek.ps1`, `models-full.json`, `tools/click-mainform-button.ps1` / `inspect-mainform-tree.ps1` / `resize-mainform.ps1`, `tools/tools/`) — all pre-existing, **not part of the engine, exclude from any commit**, not audit targets.

---

## 5. Code-trace spine (single analysis run)

Follow this path; file anchors are approximate (verify before quoting a line).

```
[Analyze / auto-run tick / on-close bar roll]
  UI/MainForm_Analysis.vb :: RunAnalysisAsync()
    ├─ source = ResolveSource() by network.transport (ws → WsMarketDataSource, else RestMarketDataSource)
    │    per-run REST fallback if DeribitWsFeed.IsDegraded()
    ├─ Task.WhenAll: 1m/5m candles, 15m (MtfRefreshPolicy — always fetch on ws), funding_8h,
    │    book summary, order book(10), recent trades(500, CHRONOLOGICAL ASCENDING)
    │    → any required Nothing ⇒ render ANALYSIS SKIPPED, ++_skipCount, no CSV row
    ├─ IndicatorResults r filled field-by-field (Core/Indicators_*.vb — all PURE):
    │    Momentum (DMI/ADX 5m, ATR, EMA, RSI, RSIDivergence, ROCSeries, VolumeSMA),
    │    Volatility (dual-session VWAP @00:00+13:30 UTC, VWAPBands, BBW, TTM),
    │    OrderFlow (OFI, OFIMomentum, Liquidations, CVD 3-seg, MicroCVD, TFI, FundingMomentum),
    │    Structure (Donchian, OBV, VPFRLite v2, SwingPivots 5m+15m, ClassifyTrendStructure, MTFGate)
    │    ── OFI on the WS-warmed path (~line 363): r.OFIRatio/Signal/Bid/Ask sourced from the
    │       time-averaged accumulator (MarketState.GetOfiAverage → ClassifyOfiRatio); else snapshot CalcOFI
    ├─ DynamicNorms.Compute → ApplySessionVolume(utcHour) → session-adjusted vol thresholds
    ├─ ScoringEngine.Calculate(r, posState, norms, cfg)   [Core/ScoringEngine_Calculate_Verdict.vb]
    │    → RunScoringPipeline()                            [Core/ScoringEngine_Calculate_Scoring.vb]
    │    Step1 regime→MaxScore | Step2 signal votes ±1 | Pass2 cross-confirm | Pass2b OI×CVD |
    │    Pass2c regime-align | Step3/3b funding | Step4 regime veto + TRANSITIONAL ADX penalty |
    │    Step4b MTF gate HARD veto (direction-aware) | Step5 dominant-side tier walk → verdict |
    │    Step5b 3-tier target cap (swing→HVN→POC) + VerdictContext | Step5-post CalcHoldStatus
    ├─ CalcKellySizing()  [Core/ScoringEngine_Kelly.vb — called from render, NOT Calculate; display-only]
    ├─ RenderOutput(v, r) [UI/MainForm_Render_Header.vb + _Render_Sections.vb → RTF]
    │    + card bindings [UI/MainForm_Render_Cards.vb] + BuildPlaintextSnapshot [UI/MainForm_PlaintextSnapshot.vb]
    └─ AnalysisLogger.LogRun(r, v) → analysis_log.csv (95 cols; header @ AnalysisLogger.vb:51-79;
         path = AppDomain.CurrentDomain.BaseDirectory, i.e. the exe's own folder)
```

**OFI geometric sub-path (frontier):** `Core/OfiAccumulator.vb` (log-ratio EMA) ← folded by `DeribitWsFeed.ApplyBook → FoldOfiAverage` (~L339/349/355), reset in `SeedAsync`; the imbalance math + classification are the shared pure helpers `IndicatorEngine.ComputeOfiImbalance` + `ClassifyOfiRatio` (`Core/Indicators_OrderFlow.vb:102-158`), so snapshot `CalcOFI` and the WS-averaged path run identical cap/floor/weight logic.

---

## 6. Audit targets — prioritized

**A. Scoring pipeline (highest-value, the product core).** `ScoringEngine_*.vb`. Verify: votes are additive ±1 into `ScoreState`; `RegimeMaxScore` sets the ceiling (base 19/18/15, +alignment when `regime_weights.enabled`); verdict thresholds are `Math.Ceiling(regimeMax × pct)`; MTF BLOCK forces NO TRADE regardless of score (direction-aware since v31 — the per-side flag matching the dominant side is consulted); funding is Steps 3/3b only (never Step 2 — double-count guard); Pass 2b penalises only *full* OI×CVD conflict; the 3-tier cap picks the closest-to-entry cap.

**B. OFI geometric implementation (most recent scoring-path change).** `Core/OfiAccumulator.vb`. Verify by hand: first fold seeds `ln(max(ratio,1e-6))`; decay fold `emaLn += alpha*(ln(ratio) − emaLn)` with `alpha = 1 − exp(−dt/tau)`, `dt` floored at 0, `tau≤0` full-overwrite; `Snapshot.Ratio = exp(emaLn)`; `_emaBid/_emaAsk` stay **arithmetic**; warmup needs ≥`avg_window_sec` of **fold-stamp** coverage (not wall-clock "now") + `MinWarmupUpdates`; `Reset()` clears `_emaLnRatio` and runs on every `SeedAsync` (re)connect. Confirm `averaging_enabled=false` is **byte-identical to snapshot OFI** (the rollback) — harness A20a/A20b. Watch the intentional D1 cosmetic (§9).

**C. WS feed + transport.** `DeribitWsFeed.vb` (answers heartbeat `test_request`; backoff/storm-guard counts *flaps* not still-down retries; serves `funding_8h` for REST parity), `MarketState.vb` (one SyncLock, copy-on-read; ring caps 250/250/210/70 candles + 5000 trades + top-10 ladder), `IMarketDataSource`/`RestMarketDataSource`/`WsMarketDataSource` + `ResolveSource` + `IsDegraded` REST fallback, `WsMarketDataSource` trades staleness gated on connection-health (not last-trade-age — a quiet-but-connected buffer is valid), `MtfRefreshPolicy.vb`.

**D. Indicators (pure fns).** `Core/Indicators_*.vb`. **Trade-stream contract:** lists are chronological **ascending**; window consumers take the last-n via `IndicatorEngine.LastN` — **flag any `Take(n)` on a trade list** (selects the OLDEST n = a bug). Check CVD 3-segment positional thirds, MicroCVD accel/decel + dynamic threshold, TFI 30-trade window, dual-session VWAP anchoring, swing-pivot strict-inequality confirmation, VPFR exp decay, MTF gate 3-format reason.

**E. Settings integrity.** `Core/Settings/EngineSettings.vb` ↔ `settings.json` ↔ `SettingsLoader.vb`. Verify every JSON key has a POCO field and vice-versa (no silently-ignored keys — the v15 cleanup removed 13 such); `SettingsLoader.Current` singleton + FileSystemWatcher hot-reload; no hardcoded magic numbers in `.vb`.

**F. Offline analysis + auto-tweaker.** `analysis/` (per-`(session×resolution)` segmentation in `AnalysisRunner`; `FailureRateMatrix`; resolution-scaled hold windows `HoldWindowsForResolution`) and `tools/AutoTweaker/` (`SettingsDiffApplier` reject fragments/prefixes + exact-match + `DisabledGatedPaths`; `PromptBuilder` HARD CONSTRAINTs 1–16). **Both must be host-agnostic — zero `System.Windows.Forms`.** The tweaker path resolver `Split(".")`s — it cannot reach array-nested per-session keys (this is by design; HC11 excludes them anyway).

**G. Display-string parity (a hard rule that has drifted 3×).** Any line emitted by the text renderers (`MainForm_Render_Header.vb`/`_Render_Sections.vb`) or `BuildPlaintextSnapshot` must have a matching binding in `UI/MainForm_Render_Cards.vb`. The text-parity harness diffs legacy↔snapshot (which move together) — the **card is the unchecked third surface**. Check the three surfaces agree, especially anywhere `VerdictResult`/`IndicatorResults` field defaults changed.

**H. Host-agnostic / portability.** `analysis/`, `tools/`, and the WS core (`MarketState`, `OfiAccumulator`, `DeribitWsFeed`, `IMarketDataSource` and impls, `ExitGuardEvaluator`, `LiveMicrostructureEvaluator`, `BarCloseDetector`, `MtfRefreshPolicy`) must have **no** `System.Windows.Forms` / `Control.Invoke` / `MainForm` coupling (Linux-port constraint, `CLAUDE.md`).

---

## 7. Invariant checklist (the "correct" bar — from CLAUDE.md + architecture.md)

1. All thresholds in `settings.json`; `SettingsLoader.Current` is the accessor; no magic numbers.
2. Scoring is additive; regime MaxScore is the ceiling; thresholds = `ceil(regimeMax × pct)`.
3. MTF gate (15m) is a **hard veto** — BLOCK ⇒ NO TRADE, direction-aware.
4. Trade lists chronological ascending; window = END of list via `LastN`; `Take(n)` on trades is a bug.
5. VerdictContext (Step 5b) and Kelly are **display-only** — zero scoring impact.
6. Funding modifier is Steps 3/3b only — never Step 2 (no double-count).
7. OI×CVD is Pass 2b — bonus on agreement, penalty only on *full* conflict.
8. MicroCVD early/late are net USD deltas — negatives are valid.
9. Session volume norms adjust *thresholds*, not signals (not a hidden directional input).
10. Dual-session VWAP anchors at 00:00 + 13:30 UTC.
11. No rejected patterns (profile §4): non-directional rewards, double-counting, fixed-% targets, flat TRANSITIONAL penalties instead of ADX-proximity scale.
12. Host-agnostic `analysis/`+`tools/`+WS core.
13. Display parity across renderers ↔ cards ↔ snapshot.

---

## 8. How to verify (build + harness + gate)

- **Build:** `dotnet build` from repo root (solution). The AutoTweaker (`tools/AutoTweaker/AutoTweaker.vbproj`) and OrderCheck (`verify/ordercheck/OrderCheck.vbproj`) are separate projects — build all three. Release: `dotnet build -c Release`.
- **Harness (the closest thing to a test suite):** `verify/ordercheck/` — host-agnostic fixtures **A1–A20i** covering the scoring math, the OFI accumulator (A20c–i: steady-state, time-aware geometric step, warmup, reset, geometric symmetry), the CalcOFI byte-identical refactor (A20a/b), and the tweaker surface (A20g/h). Run it directly (`dotnet run --project verify/ordercheck`) or via the gate.
- **One-command gate:** `tools/checks/verify-gate.ps1 -Mode prepush` — 3 Release builds **0/0** + harness **A1–A20i** + display-parity/version heuristics. This is the pre-push hook + CI (`.github/workflows/verify.yml`). **Run it, don't rebuild it.** Establish green before auditing; re-run to validate any fix.
- **Windows/PowerShell (this is a win32 box):** don't `cd` in shell commands (harness manages CWD); use absolute paths; in Bash use forward-slash or quoted Windows paths. `MainForm.Designer.vb` is auto-generated — never hand-edit.
- No other automated tests. Behaviour verification = live runs + `bin\Debug\net8.0-windows\analysis_log.csv`.

---

## 9. Known-intentional — DO NOT report these as bugs

From `architecture.md` "Display Behaviour Clarifications" + this session's context:

- **`HOLD \ EXIT` row absent when flat** — guarded on `posState≠None`; the sentinel suppresses the whole block. By design.
- **POC tier-3 of the target cap "never fires"** — reachable but geometrically narrow (POC must beat both swing target and nearest HVN, only when the HVN gate is open). Refinement, not dead code.
- **STRONG verdict co-existing with STRUCTURALLY_WEAK / MOMENTUM_FADING context** — intentional; the tag surfaces a caveat the score didn't fold in.
- **MTF reason in three formats** (`MTF PASS [DIR]` / `MTF BLOCK [DIR vs TREND]` / `MTF state: …`) — deliberate signal of the no-direction case.
- **OFI averaged path: `OFIRatio` ≠ `OFIBidVol`/`OFIAskVol`** — the ratio is avg-of-ratios (geometric), the vols are avg-of-volumes (arithmetic); they only divide out on the snapshot path. Intentional (D1, `time-averaged-ofi-spec-back.md` §3). Harmless cosmetic.
- **OFI dominance thresholds still 2.0/0.5** — the v47 re-baseline is data-gated and hasn't run (§3). Not a bug.
- **WS 3-min closed-bar volume ~2.5% undercount vs REST** — known, accepted, has a §12 volume-spike standing watch.
- **The 3 dirty docs + untracked non-engine files** (§4) — not engine code.
- **Do not re-open settled design decisions** without new data or a concrete technical reason (profile §7).

---

## 10. Working rules (if the audit produces fixes)

- **Local-first, NEVER push** — the trader tests + pushes. Commit locally as you go, only the files you intend (exclude the 3 dirty docs + untracked junk).
- **Spec-first for scoring/novel changes** — a proposal `.md` + trader sign-off before code. `analysis/` + display + tooling are safe to edit directly. Push back (with a cited reason) if a change would reintroduce a rejected pattern.
- **Host-agnostic** `analysis/`/`tools/`/WS core; **display-parity** card ↔ snapshot on any rendered-line change (same commit).
- **Delete test screenshots** (Remove-Item any PNG artifacts from UI/render verification; send via file if the trader needs to see them).
- **Commit trailer:** `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>` (adjust to your model if required).
- **Report faithfully** — don't claim you ran/verified something you didn't; if tests fail, say so with the output.

---

## 11. Session tooling + memories

- **Gate:** `tools/checks/verify-gate.ps1` (+ `install-hooks.ps1`). Wired to a pre-push hook + GitHub CI + an advisory Stop hook.
- **UI-automation tools** (`tools/*.ps1`, per memory `reference_ui_automation_tools`): run + screenshot (Win32 PrintWindow) + click/inspect (UIAutomation) the WinForms app for visual/render verification without computer-use. Useful if the audit needs to see rendered output.
- **Frontier memories:** `project_websocket_migration` (WS→P4 arc, current), `project_engine_audit_calibration_trap` (v31–v37 + calibration state), `project_v36_session_timeframe` (the (A)/(B)/(C) auto-tweaker split), `project_dev_workflow_automation` (the gate), `reference_atr_bands_v37` (current ATR bands 1-min 20/55 / 3-min 42/115).

---

## 12. What changed this session (2026-07-02, the seat before you)

Docs-only; **no code touched.** All 5 commits local + unpushed:
- **Data-sufficiency finding for v47** — analysed `analysis_log.csv`: ~94 geometric rows, one partial day, zero NY → insufficient; ~2–3 more weekday session-days incl. NY needed. (The trader is running the Debug exe standalone to keep collecting — `bin\Debug\net8.0-windows\DeribitVerdictEngine.exe`, single instance, same CSV.)
- **P4 #5 aggressor-velocity spec** (`docs/aggressor-velocity-proposal.md`) — written, §10 trader-signed-off, then the tweaker-surface tiers corrected (per-session keys are hand-tuned/off-surface per HC11 + the array-nested path limit; the flat scoring magnitudes `upgrade_bonus`/`contra_penalty` are tweaker-reachable).
- **Backlog `DeribitIndicatorProject.md` §16.6:** P13 (document tweaker-tunable vs hand-tuned settings in the UserManual) + P14 (surface auto-tweaker Phase-2b / workstream C).
- **Memory fix:** `reference_atr_bands_v37` — the skill's bundled trader-profile ATR bands are no longer stale (re-synced to v37; verified this session).

**Nothing to push; nothing in-flight in code.** Your audit baseline is a clean, green tree at origin/master `a00ac35`.
