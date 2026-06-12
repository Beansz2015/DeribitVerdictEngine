# UI Reskin — Consolidated Fix Spec-Back

**Date:** 2026-06-12
**Implementer:** Fable 5 (trader redirected from the kickoff's Opus routing)
**Spec:** `docs/ui-reskin-consolidated-fix-kickoff.md` (supersedes `docs/ui-reskin-atr-clip-fix-kickoff.md`)
**Findings source:** `docs/p5-test-snapshot-review-report.md`
**Commits (local, unpushed):**

| Hash | Subject |
|---|---|
| `ef3bf61` | fix(ui-reskin): consolidated fix — pixel budgets + binder gaps (review F-01..F-11) |
| `a4ff9d5` | docs(ui-reskin): snapshot review report + consolidated-fix kickoff + handover rulings |
| `caefa3d` | fix(ui-reskin): card parity with Tier D text output (Kelly notional, ATR ratio, Donchian pill) |

Status: **awaiting trader visual sign-off** on the regenerated `verify/p5-test` PNGs. After sign-off: P5-test cleanup commit → P5b deletion sweep → Spec C, per the reskin roadmap.

---

## 1. Kickoff items — all shipped

### 1.1 Pixel-budget family (one sizing pass)

| Item | What shipped | Where |
|---|---|---|
| **1a KNOWN-0** | ATR row-4 budget 150 → 200 px. The R:R cell's `(risk N / rwd N)` line moved off the 3-line label onto its own small-font sub-label (`AtrRowControls.RRSubValue`, 8.0/7.0 pt by row weight) — it can no longer clip vertically and fits the column even at case-20-scale numbers (`risk 6000.0 / rwd 10000.0`). CAPPED column 20% → 23% (ENTRY 18% → 15%) so the longest reason label `(NEAREST_HVN_ABOVE)` renders fully at the primary font. | `MainForm_Layout.vb` (row budget, `AtrRowControls`), `MainForm_Render_Cards.vb` (`BuildAtrZoneRow`, `BindAtrRow`) |
| **1b F-01** | Structural row-5 budget 110 → 130 px. R:R column 25% → 34% (22/20/24/34) with the same stacked sub-label treatment (`StructuralCardControls.RRSubValue`). `(risk 300.0 / rwd 300.0)` now renders complete on both cards. Side effect: the per-side missing-leg notes (`— no swing target above` etc.) were always full in code and merely wrap-clipped — the height bump un-clipped them, resolving review OBS-F with no wording change. | `MainForm_Layout.vb`, `InitStructuralCard`, `BindCardStructural` |
| **1c F-08** | `RegimeAnchorWarn` word-wraps: height measured via `TextRenderer.MeasureText` against current width (re-measured on resize), painted with `TextFormatFlags.WordBreak`. Wording unchanged (kept legacy parity rather than compressing — the kickoff allowed either). The hero row is no longer fixed: `BindCardVerdict` grows it per run for whichever conditional rows are visible (eff/penalty +18, hold reason +32, banner +height+4), so the banner gets its own pixels instead of occluding the chip grid. Verified symmetric on 41/42. | `UI/Controls/RegimeAnchorWarn.vb`, `InitVerdictCard`, `BindCardVerdict`, `_heroRowIndex`/`HERO_ROW_BASE` in `MainForm_Layout.vb` |
| **1d F-09** | HOLD chip shows the compact action (text before the `" -- "` separator, e.g. `EXIT LONG (Layer 1.5)`); a new full-width wrapping row (`_lblHoldReason`, 2-line budget) under the grid carries the complete CalcHoldStatus string prefixed `HOLD/EXIT:`. Case 44's structural-break prices (`49680.0` / `49700.0`) are fully readable in the card — required before P5b deletes the legacy block, which was the only other surface carrying them. | `InitVerdictCard`, `BindCardVerdict` |

### 1.2 Binder fixes

| Item | What shipped |
|---|---|
| **2a F-07** | Capped rows: CAPPED cell now renders `raw → capped` (`50160.0 → 50080.0`) restoring the dropped raw target; the R:R cell ratio is computed from the effective (capped) target — `FormatRR(rwdUsd, riskUsd)` where `rwdUsd = \|adjusted − entry\|` — so case 11 reads `1:0.8 (risk 96.0 / rwd 80.0)` instead of the misleading raw-ATR `1:1.7`. Uncapped rows are numerically unchanged. v30 sub-tick suppression arm unaffected (case 15 still renders a plain target). **Report correction shipped with this:** the review's F-07(b) claimed the risk/rwd subline was raw-ATR — the code read showed it was already capped-aware; the 3× zoom had misread KNOWN-0-clipped glyph slivers. The visible raw ratio + dropped raw target were the real defects. Corrected in the report, `a4ff9d5`. |
| **2b F-10** | `vwapWarmup` (the warmup-candles threshold the text renderers receive as a parameter) threaded into `BindCardIndicatorDetails` and `BindCardSignalBreakdown` (optional param, −1 → live cfg fallback). VWAP details box shows `[WARMUP]` in the amber title; the VWAP Dev breakdown note appends `\| warmup`. Root cause was source divergence: the card recomputed warmup from live cfg while legacy compared against the passed-in threshold — identical in production, divergent in the harness (case 48 pins 60 vs cfg 15). Both call sites updated (`MainForm_Analysis.vb`, `BindAllCardsForTest`). |
| **2c F-11** | Liq pill maps `LONG_CASCADE`/`SHORT_CASCADE` to the `L LIQ`/`S LIQ` pills plus a side-derived fallback for unrecognised strings. Discovery: the engine only emits `LONG LIQS`/`SHORT LIQS`/`NONE` (`CalcLiquidations`) — `LONG_CASCADE` is a harness-invented string, so F-11 was a robustness gap, not a live bug. The mapping now survives either vocabulary. SHORT_CASCADE remains fixture-only (no live arm). |

### 1.3 One-liners

- **F-03**: best-pivot sub-note spans the full breakdown row (cols 0–3, ~500 px, was NOTE+SC ~305 px) and the tail trimmed `vs avg. pivot` → `vs avg`. Full string renders: `↳ Best vol. pivot (5m): HIGH @ 50050.0 (vol ×1.8 vs avg)`.
- **F-04**: RSI comparator renders `=` at exactly 50.0 (`50.0 = 50.0`); `<`/`>` unchanged elsewhere.
- **F-05**: STATE pill column 75 → 85 px — `NW SHRTS` renders whole; trader's requested abbreviation kept rather than re-abbreviated (G8 restored by width, not wording).
- **OBS-F**: no code needed — clip artifact, resolved by 1b (see table above).

### 1.4 Handover §4 locked rulings (shipped `a4ff9d5`)

Three triage rulings added to `docs/ui-reskin-handover-2026-05-27.md` §4: breakdown NOTE column stays lowercase (F-02 ACCEPT AS-IS); `Near HVN resist` renders direction-red, not spec-amber (F-06 blessed deviation); context-tag severity colour mapping FADING-amber / STRUCT_WEAK-red (OBS-E blessed).

---

## 2. Findings beyond the kickoff (the engine-drift family)

The single most important outcome of this cycle is not any one fix — it's a **pattern that bit three separate times**: engine-side commits changed what the text renderers (legacy RTF + `BuildPlaintextSnapshot`) emit, after the card binders were written, and nothing forced the card to follow. The 55/55 text-parity gate is structurally blind to this class — it compares legacy↔snapshot, both regenerated from the same build, so a line both texts gained and the card never learned passes the gate forever.

| # | Engine commit | Drift | Card symptom | Fix |
|---|---|---|---|---|
| 1 | `ccdd652` (v31 F3) | `VerdictResult.MTFGateReason` field default changed `"MTF PASS"` → `""` | Harness cases authored against the old default regenerated with an empty `Reason:` line; the verdict-card chip degraded to `MTF state: —` | Harness `NeutralVerdict` re-pins `MTFGateReason = "MTF PASS"` (test-side only; production composes the reason in Step 4b) — in `ef3bf61` |
| 2 | `0bd1b63` (Tier D D1, v32) | New `Notional: ≈ $N · N.N× lev [LEV CAPPED]` line in both text renderers + inverse-contract Kelly math + `KellyLevCapped` field | KELLY card silently lacked the Notional row (trader's catch) | `BindCardKelly` renders the row when contracts ≥ 1, amber when lev-capped, mirroring legacy — in `caefa3d` |
| 3 | `482c9bb` (Tier D D2, v32) | Legacy NORMS row relabelled `ATR scale` → `ATR ratio` | Details card kept the old label | Card follows per G5 — in `caefa3d` |

Closure evidence: a **distinct line-label inventory** across all 55 regenerated legacy files, compared against the May-28 reviewed baseline (case 01 held verbatim from the review), shows `Notional`, `ATR ratio`, and the Contracts value semantics were the complete set of post-review deltas. No other binder is affected.

Also resolved in this family's spirit: **Donchian pill `NEUT` → `NONE`** (trader query) — same engine state, but the pill now uses the legacy signal word per G5, consistent with the Liq pill. And **HVN@POC** confirmed as designed: `NO` is omitted (locked Q4a); `YES` renders as the `(HVN@POC)` tag beside the POC value (case 26).

---

## 3. The hard rule (installed)

> **Engine display-string parity rule.** Any commit that adds, removes, renames, or re-formats a line emitted by the legacy renderer (`MainForm_Render_Header.vb` / `MainForm_Render_Sections.vb`, until P5b) or `BuildPlaintextSnapshot` — including `VerdictResult` / `IndicatorResults` field-default changes that alter rendered output — MUST update the corresponding binding in `UI/MainForm_Render_Cards.vb` in the same commit, or state explicitly in the commit message why no card surface is affected. The text-parity harness cannot catch this drift: it diffs legacy↔snapshot, which move together. After P5b deletes the legacy renderer, the rule applies to snapshot↔card.

Installed in two places so engine-side sessions (which don't read the UI handover) see it: `CLAUDE.md` → Collaboration Rules, and `docs/ui-reskin-handover-2026-05-27.md` → §5 process lessons (§5.14). Three instances in one cycle is the evidence base; cite this spec-back when enforcing.

---

## 4. Verification

- `dotnet build` clean (0 warnings) at every step.
- Harness regenerated 4× across iterations; **55/55 text parity on every run**.
- Crop-and-zoom (fit-scale never used as evidence) on the kickoff's mandated set: 01, 04, 08, 11–14, 16–18, 20, 41–46, 48, 51 — plus KELLY/NORMS/Donchian re-crops after `caefa3d`. All pass criteria met, including `(NEAREST_HVN_ABOVE)` / `(NEAREST_HVN_BELOW)` fully rendered (caught truncating at first pass via a **live** capped row; CAPPED column widened in response — the live run earned its place in the loop).
- Whole-form check: no collateral clipping; form grew 3124 → 3194 px (ATR +50, structural +20); hero row grows only on banner/hold/penalty cases.
- **Live ANALYZE run** (real Deribit data): MTF chip renders the post-v31 composed reason (`MTF state: BULL \| 15m +DI:38.3 -DI:13.6`) — review OBS-B resolved as a stale-harness artifact; a live HVN-capped short row rendered `raw → capped` + full label + capped-aware R:R correctly. Note: this appended one legitimate row to the clean v0.6 CSV.
- Foreground-steal note: `tools/send-ctrl-shift-t.ps1` lacks the `AttachThreadInput` bypass that `screenshot-mainform-full.ps1` has — harness triggering silently no-ops when the terminal isn't foreground. Worked around with an inline robust sender; not patched in `tools/` since the script deletes in the cleanup commit. If cleanup is deferred, port the bypass.

## 5. Arms still untestable from this artifact set

Carried over from the review report §4 (unchanged by these fixes): KELLY no-`[CAPPED]` arm; SHORT_CASCADE pill (now coded, fixture-only); R:R `< 0.1` display literal; HOLD rendering with an actually-declared position radio. None block sign-off; all are live-run or future-fixture territory.

## 6. What's next

1. **Trader visual sign-off** on the regenerated 55 PNGs (this is the gate).
2. P5-test cleanup commit — delete `UI/MainForm_TestHarness.vb`, `UI/TestHarnessCases.vb`, the `Ctrl+Shift+T` ElseIf, `tools/send-ctrl-shift-t.ps1` (gap-fix proposal §9.3).
3. P5b deletion sweep, then Spec C.
4. Push only after the trader's test gate, per the local-first workflow.
