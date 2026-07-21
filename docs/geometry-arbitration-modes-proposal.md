# Geometry Arbitration Modes + Signed Buffers — What-If-First Integration · Proposal

**Date:** 2026-07-21 · **Status:** PROPOSED — G-table awaits trader (trader go-ahead given on the concept 2026-07-21; shape decisions below) · **Type:** engine seam extension, **all defaults byte-identical to v51 B4b behaviour — zero live impact at build**. Enabling any non-default mode/buffer LIVE is a later, separate ⚠ D-table gated on replay evidence (and, for the stop side, on consumer sizing-by-stop-distance — L3). No dataset boundary at build.
**Evidence trail (2026-07-21 strategic review):** (1) the trader's discretionary scheme — TP = min(struct, ATR) − buffer, SL = max(struct, ATR) + buffer — is not expressible by any current knob, so the roadmap had no way to ever test it; (2) live post-B4b read: NY×1 swing-tier target reach **31.5%** (n=321) vs ATR-fallback **43.7%** (n=444) vs HVN **71.6%** (n=95) — the derivation's NY structure-first validation (64% in-bound, `placed-geometry-derivation-2026-07-06.md` F3) has NOT replicated on the swing rung; (3) interim what-if bound sweep (`target_max_atr_mult` 1.25→3.5, post-07-08 book, report 20260721_155030): **FLAT** — all cells within noise (EV full −0.191..−0.209 ATR, CIs overlap; "winner 3.5" is a selection-half artifact with the worst holdout), so the bound knob cannot fix the swing drag; the arbitration SHAPE is the open question.
**Intent:** permanent instrument, not a one-off — geometry-shape questions become standing what-if grid sweeps (EV-in-ATR + split-half) on the logged book.

## 1. Change — `ComputeSideLevels` (the ONE seam; all four parity surfaces + the what-if runner inherit)

New `structural_levels` keys (settings v55→v56, POCO + change_log; all defaults = current behaviour):

| Key | Default | Meaning |
|---|---|---|
| `target_arbitration_mode` | **0** | 0 = ladder (current: swing→HVN→POC→session-ATR fallback, priority-with-bound). 1 = **nearest**: min distance over {qualifying structural candidates (in-bound, >0), session ATR fallback} — closest-first, the pre-B4b cap philosophy generalised. |
| `stop_arbitration_mode` | **0** | 0 = tightest (current DG1: min(structural, stop_max×ATR) ≥ floor). 1 = **widest**: max(structural swing stop, stop_max×ATR) ≥ floor, UNCLAMPED above — the trader's SL half. Mode 1 live-enable is **hard-gated on sizing-by-stop-distance (L3)** regardless of replay evidence (derivation F1: wide stops at fixed size = bigger losses, only ~14% of stop-outs reach target in-window). Replay use is free. |
| `target_buffer_pct` | **0.0** | Signed % of the placed target's distance from entry, applied AFTER arbitration. Negative shaves the target toward entry (the trader's pullback); positive pushes beyond. |
| `stop_buffer_pct` | **0.0** | Signed % of the placed stop's distance from entry. Positive pushes the stop beyond the level (the trader's buffer); negative tightens. |

Buffer application: `placed′ = entry ± dist × (1 + pct/100)` on the respective side; the 4-tick stop floor and the Step-5c min-move gate evaluate the BUFFERED prices (so a deep negative target buffer can honestly gate a verdict to BELOW_MIN_MOVE — in replay, where the gate re-derives). Labels gain a buffer suffix only when a buffer ≠ 0 (never renders live at defaults — no display-parity obligation at build; if a mode ever ships live, its ⚠ pass owns the label/parity work).

## 2. What-if integration (the point of the exercise)

- All 4 keys join `WhatIfOverlay.Whitelist`. Modes are integer-coded precisely so the numeric overlay/sweep machinery handles them unchanged (a sweep 0→1 step 1 = mode comparison in one grid).
- Recommended first study (runs after build, produces a verdict doc; NO live change without a subsequent ⚠ D-table): grid over `target_arbitration_mode` {0,1} × `stop_arbitration_mode` {0,1} × `target_buffer_pct` {−10, −5, 0} × `stop_buffer_pct` {0, +5, +10} = 36 cells, full v0.8 book and post-07-08 window separately, EV-in-ATR + split-half per the runner's existing conventions (expiry valued at the runner's standing mark-at-window-end convention — stated in the report header it generates). The trader-shape cell = (1, 1, −n, +n); closest-first-plain = (1, 0, 0, 0).
- Tweaker: all 4 keys fenced off the surface (**HC24** — hand-ruled geometry, HC11 class; exact-match on the two modes + the two buffers).

## 3. Acceptance

Builds 0/0 (Release); harness + new **A36 family**: (a) defaults byte-identical — placements equal v51 B4b on the A26 case set (the load-bearing pin); (b) nearest mode picks the minimum-distance qualifying candidate incl. beating the fallback; (c) widest stop picks max and respects the floor; (d) signed buffers move each side the right direction and the min-move gate reads buffered prices; (e) whitelist accepts the 4 keys, tweaker fence rejects them (HC24); (f) what-if adapter replays a mode-1 overlay through the same seam (A30a pattern). verify-gate prepush green (settings bump present → version check OK). Spec-back `geometry-arbitration-modes-spec-back.md`; §15 row.

## 4. G-table

| # | Decision | Recommendation |
|---|---|---|
| **G1** | Mode encoding | Integer-coded (0/1) for overlay/sweep compatibility — strings would need parser surgery for zero gain |
| **G2** | Buffer units | **% of placed distance** (the trader's stated framing; scale-free across regimes). Alternative: ATR-fraction (the W6-1 swing-buffer note's preference) — record whichever is ticked; the study sweeps the same either way |
| **G3** | Defaults byte-identical, no live change at build | Yes (A36a pins it) |
| **G4** | Whitelist + HC24 tweaker fence | Yes |
| **G5** | First study protocol (§2 grid, verdict doc, any live change = its own later ⚠ D-table; stop mode 1 additionally L3-gated) | Yes |
| **G6** | Build slot / model | Any gap (no boundary); **Opus, medium**; before the soak review if convenient — the study itself can run same-day on the existing book |
