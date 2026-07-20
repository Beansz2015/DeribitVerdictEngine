# Offline What-If Replay Runner — Spec-Back

**Date:** 2026-07-16 (built) · 2026-07-17 (refinements + trader validation) · **Implementer:** Opus (medium), one conversation · **Status:** BUILT + **trader-validated** (CLI + in-app launcher, multiple live runs) — local (unpushed), **ready for reviewer**.
**Spec:** `docs/offline-whatif-replay-proposal.md` (APPROVED 2026-07-16, W1–W7). Analysis-only instrument: zero scoring impact, no ⚠ boundary, never writes `settings.json`, no `settings.json` version bump (adds no config keys — it *reads* existing keys through overlays).
**Reviewer note:** trader-facing docs are drafted but NOT yet inserted — see the manual-draft item in §4. Everything else is code-complete + harness-green.

---

## 1. What shipped

**New host-agnostic project — `tools/WhatIfRunner/` (net8.0 console, zero WinForms):**
- `WhatIfRunner.vbproj` — links the shipped `SignalEmitter` + `FailureRateMatrix` + `ForwardWindowJoiner` + `DeribitOhlcFetcher` + settings/indicator stack (AutoTweaker.vbproj precedent). The root vbproj already excludes `tools/**`, so the solution build is untouched.
- `WhatIfOverlay.vb` — overlay JSON parse, the §2 knob whitelist (enforced in code; off-list keys throw `WhatIfOverlayError` naming the path), the one-semantic grid expansion (blank=inherit / single=pin / `{"sweep":{from,to,step}}`=sweep), ratio-constraint pruning, and the ≤3,000-cell cap.
- `WhatIfSettings.vb` — reads live `settings.json` once, deserialises a fresh `EngineSettings` clone per cell (no mutation bleed), applies the cell's whitelisted knobs through strongly-typed setters (the whitelist's second code gate), and resolves live values for constraints + report marking.
- `WhatIfReplay.vb` — the replay core: the `CsvRow → IndicatorResults` adapter, the verdict re-derivation, per-cell replay → replayed `CsvRow`s + EV samples, and the EV-in-ATR computation.
- `WhatIfReport.vb` — the markdown writer: guard-rail banner, EV grid ranking + split-half, population-shift line, per-population baseline-vs-overlay failure matrix.
- `WhatIfProgram.vb` — the CLI entry point (arg parse, span filter, OHLC fetch, orchestration, overfit-counter file, report write).

**Shared code (additive, non-regressing — full harness stayed green):**
- `analysis/ForwardWindowJoiner.vb` — `CsvRow` gained the logged fields the replay needs (`LongScore`/`ShortScore`/`EffectiveLong`/`ShortScore`/`MaxScore`/`Confidence`/`MtfGatePassLong`/`Short`/`SwingTargetLong`/`Short`/`VpfrNearestHvnAbove`/`Below`/`TargetCapReason`), all header-name parsed and guarded — absent columns keep defaults, so `AnalysisRunner` / `AutoTweaker` / `ordercheck` are untouched.
- `analysis/FailureRateMatrix.vb` — `ToTier` now delegates to a new **public** `CanonicalTier` (same mapping) so the replay tags re-derived verdicts against the identical tier denominator.

**In-app launcher (W7) — `UI/WhatIfLauncherForm.vb` + a 4th "What-If Replay" `LinkRow` in SETTINGS & TOOLS:**
- The form is a launcher ONLY (no replay logic, no reference to the `tools/WhatIfRunner` project): it builds an overlay JSON from a whitelisted-knob grid (each row value-or-sweep) + a constraint field + span fields, writes it to a temp file, `Process.Start`s `WhatIfRunner.exe`, and opens the produced `whatif_report_*.md` in the existing `AnalysisReportForm` viewer. The `TweakSettingsForm` precedent exactly.
- `SETTINGS_CARD_H_BASE` bumped 250 → 278 (+28) for the 4th LinkRow at y=104 (the TOOLS row is `SizeType.Percent`, so the card grows and the row fits).

**Fixtures — `verify/ordercheck` (A30a–d), links `WhatIfOverlay.vb` + `WhatIfReplay.vb`:**
- **A30a** — the adapter feeds `SignalEmitter.ComputeSideLevels` identically: a replayed row's `Placed*` ≡ a direct `ComputeSideLevels` call (the "no copies / IS production" guarantee; the empty-overlay reproduction on real rows).
- **A30b** — the whitelist rejects an off-list key (`indicators.OFI.book_depth`) loudly and accepts a listed one (`stop_max_atr_mult`).
- **A30c** — a `verdict_med_pct` 0.53→0.45 overlay flips a row WEAK LONG → LONG (threshold re-derivation shifts the directional population).
- **A30d** — the POC ladder tier is closed in replay (the adapter zeroes `VPFRPoc`/`VPFRSignal`, so the ladder is swing → HVN → fallback).

**Docs + housekeeping:**
- `.gitignore` — `whatif_report_*.md` + `whatif_overlay_counter.json` now ignored (the runner + launcher write reports to the repo root so "Open Last Report" can find them; generated artefacts, never committed — same treatment as the other report/cache outputs).
- `docs/whatif-manual-draft.md` — a **review draft** of the trader-facing documentation: per-field reference mapped to the app displays it drives, the blank/single/sweep semantics, a field-nuances section (verdict-threshold ceiling + per-regime max, the fallback-vs-clamp stop distinction, session-override precedence, min-move coupling, eval-window ranking-only), and four worked examples. **Pending trader approval**, then folds into the two manuals (see §4).
- Proposal status line flipped to BUILT; `architecture.md` directory tree updated; `DeribitIndicatorProject.md` §15 build-history entry added (analysis-only, no settings bump).
- **`tools/build-manual-pdfs.ps1` + `tools/manual-pdf-header.tex` (new).** The manuals had **no PDF build script** — the toolchain had to be reverse-engineered to republish them, so it is now pinned: pandoc + XeLaTeX, Cambria (body) + Cascadia Mono (code), 1in margins. Two things the script encodes that cost real debugging: (a) pdflatex hard-fails on the manuals' Unicode and **Consolas silently drops** `⚠ ★ ✓ ✗ ⌈ ⌉ ∈` inside code blocks, hence Cascadia Mono; (b) six glyphs no installed text font covers are routed to Segoe UI Symbol via the header's `\newunicodechar` map. A missing glyph is only a pandoc *warning* (the character just vanishes from the PDF), so **the script treats any missing-glyph warning as a build failure** and names the codepoints.

---

## 2. Key design decisions (and why they're faithful)

**Baseline = the replay under LIVE settings (empty overlay), not the logged verdict.** So baseline-vs-overlay is apples-to-apples on the identical replay path, and the delta isolates the overlay. Because the empty-overlay replay reproduces the logged verdicts + placed levels (A30a pins the placement identity), the baseline column reproduces the standing failure matrix for the span (§7 acceptance) — by construction, not by a separate code path.

**Verdict re-derivation reproduces Step 4→5 from logged inputs (no score rebuild — §2):**
- Regime veto from `row.Regime` + **raw** `LongScore`/`ShortScore` (TRENDING_UP short / TRENDING_DOWN long → NONE), exactly as `ScoringEngine_Calculate_Verdict` pre-empts. *This is a fidelity point the proposal's one-line sketch omitted — without it a regime-vetoed row could wrongly replay directional.*
- Dominant + tier from `EffectiveLong`/`ShortScore` vs `Math.Ceiling(MaxScore × pct)` (the shipped `Threshold()` formula; `MaxScore` IS the logged regimeMax).
- MTF veto from the logged per-side `MTFGatePass` flags (`mtf_gate.enabled` stays live — not whitelisted).
- Min-move gate on the **replayed placed target** (from `ComputeSideLevels`) — this is *more* accurate than `FailureRateMatrix`'s ATR-fallback approximation, because the replay actually has the placed target. Rows a tradeable tier lost to the min-move gate are tallied as the report's `BELOW_MIN excluded`.

**Maximum reuse (the "one seam, no copies" rule):** placement = the shipped `SignalEmitter.ComputeSideLevels`; the per-tier failure matrix = the shipped `FailureRateMatrix.Compute` run twice (baseline-cfg rows vs overlay-cfg rows) on identical input rows; EV outcomes = the shipped `FailureRateMatrix.WalkBars`; CIs = `WilsonCI`; load/fetch = `ForwardWindowJoiner` + `DeribitOhlcFetcher`. The replay builds modified `CsvRow` copies and lets the standing matrix engine do the rest.

**POC tier (W3 — exclude-and-label):** `VPFRPoc`/`VPFRSignal` are unlogged, so the adapter leaves them at the no-POC values and the replay ladder is swing → HVN → fallback; logged `poc`-bucket rows (via the `TargetCapReason` column) are excluded up front and counted in the report header. Measured population on the live book was 0 over the smoke spans.

**Grid-sweep (§3b):** one semantic (blank/single/sweep), cartesian product, ratio constraints prune cells before running, ≤3,000-cell cap (readability/statistics). Ranking objective = per-trade EV in ATR units (`WalkBars` outcome → +targetDist / −stopDist / mark-to-window-end, normalised by ATR), never win-rate. Split-half validation ON by default: winner selected on the selection half (alternating session-days), reported beside its unseen-half EV, flagged **DIVERGENT** when the holdout mean drops below the selection-half CI. The overfit counter (`whatif_overlay_counter.json`, keyed by book span) increments by cell count and the header prints the expected-false-winners arithmetic.

**Memory:** the grid-ranking pass runs `RunCell(..., keepRows:=False)` so only EV samples + tallies are retained per cell; the baseline and the winner run with `keepRows:=True` for the matrices. Real grids are tiny (sweep one knob = a handful of cells); the ≤3,000 cap bounds the worst case.

---

## 3. Acceptance (proposal §7)

- **Builds 0/0** — `WhatIfRunner.vbproj` compiles clean; the solution + main project compile clean (the only build error is the exe-copy lock from the trader's *running* app instance, unrelated).
- **ordercheck unregressed + the new fixtures** — full harness `ALL PASS`, including A30a (empty-overlay reproduction on real captured-shape rows), A30b (whitelist reject/accept), A30c (threshold-replay population shift), A30d (POC-row exclusion).
- **Live smoke** — a real `stop_max_atr_mult` 2.0 overlay over 2026-07-15→16 on the live `analysis_log.csv` produced a baseline-vs-overlay report whose baseline column reproduces the standing matrix, and whose overlay column shows the expected effect (looser stop lifts NY MEDIUM_LONG success, cuts adverse-first hits; directional population unchanged — a stop knob doesn't move verdicts). A 3-cell diagonal grid-sweep (with a `ratio == 1.0` constraint that pruned 9→3) exercised the EV ranking + split-half, which correctly flagged the winner **DIVERGENT** (holdout EV below the selection-half CI) — the overfit guard doing its job.
- **Expanded functional matrix (CLI)** — over the live book: all-13-knobs pinned (1 cell, every setter fires); a 100-cell 3-knob grid (EV ranking + split-half); every-field-swept (correctly rejected at the cap); a random blank+pin+2-sweep+ratio-constraint combo (pruned 12→9). Reports were content-checked — population-shift, session-override precedence (LONDON/ASIA shielded from a global `atr_target` sweep), the fallback-only footprint of `atr_stop_multiplier` (identical EV across its sweep), and the DIVERGENT flags all behaved as designed.
- **W7 launcher — visually verified + driven live.** The 4th "What-If Replay" LinkRow renders correctly (UIAutomation tree + full-window capture: the four TOOLS rows at even 39 px spacing, not clipped, card grew to fit). End-to-end driven from the app — link → launcher → fields set → Run → runner executes → report opens in `AnalysisReportForm` — with the launcher's field→JSON mapping confirmed (`Stop max ×ATR = 2.0` → nested `stop_max_atr_mult`). The trader has since run several of his own multi-field sweeps and read the DIVERGENT/overfit output correctly.
- **Post-refinement re-verify** — after the cap raise + top-50 truncation: harness `ALL PASS` (A30a–d unregressed), the trader's `5×5×3×5×3 = 1,125`-cell sweep runs and the ranking truncates to top 50, and a 5,000-cell grid still rejects with the corrected message.

---

## 4. Open items

- ~~Trader-facing docs~~ — **DONE 2026-07-17.** Draft approved and inserted: `TraderGuide.md` §17 gained a condensed *What-If Replay — backtesting a settings change* subsection; `UserManual.md` gained full **§26 What-If Replay (Backtesting)** (+ TOC entry). Both PDFs regenerated. `docs/whatif-manual-draft.md` is retained as the approved source draft.
- **Fixture-file serialization vs #6 (W5):** A30 lives in `verify/ordercheck/Program.vb` alongside #6's lane. This build was authored before #6's lane opened (the W5 rule); if #6 has already landed at merge time, its lane takes priority and A30 rebases after it.
- **Runner exe path** — the launcher points at `tools/WhatIfRunner/bin/Debug/net8.0/WhatIfRunner.exe`; the project must be built once (Debug) before the in-app launcher can run it (the CLI works regardless). Trader has built it.
- **Not committed** — the whole change set is local on `master`, per the trader's test-then-push workflow. Trader validation is done; the push is his to make.

---

## 5. Post-build refinements (2026-07-17, trader-driven)

- **Sweep-prefill checkbox (launcher).** "Prefill default sweep ranges" fills every knob field
  with the sweep shown in its hint (and clears them when unticked), so the trader can start from
  the full sweep set and edit down instead of retyping ranges. Bulk toggle; the label warns that
  all knobs at once exceeds the cap.
- **Grid cap 1,000 → 3,000.** Measured on the ~4.2k-row book (replay re-walks per cell): 1,000
  cells ≈ 9 s, 3,000 ≈ 23 s, 5,000 ≈ 40 s, 10,000 > 2 min. 3,000 is the compute-safe knee with
  headroom as the book grows — the proposal's "compute is a non-issue" held only for a
  shared-excursion design, not this per-cell replay, so the cap's rationale is now compute +
  multiple-comparisons (the rejection message was corrected accordingly). A 5-field sweep (the
  trader's `5×5×3×5×3 = 1,125`) now runs.
- **Ranking table truncated to the top 50 cells** (winner is always rank 1 by construction),
  with a "showing top 50 of N" note — decouples cells-run from rows-displayed so the report stays
  readable at any grid size; the overfit banner still prints the full evaluated count. This is the
  actual fix for the "readability" leg of the cap, letting the run cap be compute-bound.
- **Follow-up option if the trader ever needs > 3,000:** a shared-excursion refactor (walk each
  row's forward bars once, evaluate all cells' barrier pairs against it) would make compute near-
  free and let the cap rise substantially. Not built — out of scope for this pass.
