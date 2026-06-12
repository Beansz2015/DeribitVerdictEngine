# Clean-Data Re-Baseline Review — Brief

**Date:** 2026-06-12
**Trigger met:** ~487 clean CSV rows (v0.6, post-correctness-pass) vs the ≥300-row / ≥2-session threshold (`engine-correctness-pass-proposal.md` §8; `DeribitIndicatorProject.md` §12 WATCHING row).
**Reviewer model:** Fable at **Extra** (calibration judgement; settings-only output). One conversation.
**Output:** a settings-only calibration proposal (v33) — value changes + rationale per threshold, change_log entry, §15 row. **No code changes.** All changes approval-gated by the trader before applying.

## Inputs (read in this order)

1. `CLAUDE.md` + `docs/DeribitIndicatorProject.md` §12 (WATCHING table) — context.
2. `docs/engine-correctness-pass-proposal.md` §8 — the re-baseline table: what moves and why.
3. The trader-generated artifacts: latest **Analysis Report** (markdown, via the in-app 📊 button) + **CalibrationReport** output + `analysis_log.csv` itself (v0.6, header-named columns).
4. `settings.json` (v32) — current values; `change_log` for tuning history (v14/v19/v20/v22 precedents show the expected rigor).

## Step 0 — sample composition gate (do this before touching any threshold)

Tabulate the 487 rows: regime distribution, session (ASIA/LONDON/NY) spread, verdict-tier counts, days covered. **If a regime or session has <50 rows, re-baseline only what that sample can support** and report what needs more collection — partial re-baseline beats overfitting a lopsided sample (the v19 lesson: tuned on an ultra-quiet 618-row sub-window, recalibrated twice since). One row is a UI-verification artifact (2026-06-12 live run) — ignorable noise at n=487.

## Review items (from the §8 table)

| Threshold | Current | What to check |
|---|---|---|
| `indicators.OBV.trend_gate` | 10.0 **(seeded guess — priority)** | OBVTrend RISING/FALLING/FLAT distribution vs run-level price drift; gate should make OBV directional on trending stretches, FLAT in chop |
| `indicators.CVD.slope_min_usd` / `slope_pct_of_value` | 12000 / 0.01 | CVDSlope distribution post-fix (slope now chronologically correct); RISING/FALLING vs forward returns |
| `indicators.MicroCVD.accel_threshold` + dynamic pct/floor | 10000 / 0.03 / 0.25 | **Now CSV-logged (cols 89–93)** — ACCEL/DECEL/FLAT mix; check the dynamic arm actually binds vs the static floor |
| `session_volume` multipliers | ASIA .80/.85, NY 1.15/1.10 | Volume-signal fire rates per session against the now-current baseline (H1 fixed the stale window the multipliers were compensating for) |
| `indicators.Volume` dynamic clamps | 2.0–6.0 / 1.5–4.0 | Where the computed thresholds actually land in the clamp range per session |
| `indicators.Donchian.quartile_pct` | 0.25 | Full-vs-partial signal mix now that full breakouts fire (F6) |
| Verdict tiers / NO TRADE rate per regime | `verdict_*_pct` | Sanity: tier distribution post-H3 (no wrong-side WEAKs); NO TRADE rate neither collapsed nor exploded; `[TIE]` frequency |
| MTF BLOCK rate per side | `mtf_gate.*` | Post-H2 per-side block distribution (`MTFGatePassLong/Short` columns) |
| `scoring.atr_target_multiplier` / `atr_stop_multiplier` | 2.0 / 1.2 | v28 target-hit vs barrier-hit gap on linear-geometry rows (post-`482c9bb` only — mark the boundary) |

Anything outside this table needs new evidence to touch (trader-profile rule: don't re-open settled decisions). Funding bands in particular were calibrated v22 with documented rationale — leave unless the data screams.

## After the v33 pass lands

**Supervised auto-tweaker first fire** (still held; manual button only): confirm `dry_run_enabled: true` in `tweaker_config.json`, run it once with the trader watching, review its proposed diff against the fresh v33 rationale before any apply. Note: rows scored under v31/v32 thresholds precede v33 — expect the first windows to reflect pre-re-baseline behaviour; that's informational, not a defect.
