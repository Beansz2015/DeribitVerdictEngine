# #5 Aggressor Velocity — §5.1 Correlation-Gate Verdict

**Date:** 2026-07-13 · **Seat:** Fable coordinator · **Type:** data verdict (no code changed)
**Recipe:** `aggressor-velocity-proposal.md` §5.1, decision rule §10.2 (trader-ticked 2026-07-01) · **Queue:** `month-handover-2026-07-07.md` Q2
**Rule:** redundant ⇔ `|Spearman(lean, TFI)| > 0.7` **AND** directional fire-overlap `> 80%` ⇒ #5 closes display-only. Otherwise it carries independent information and proceeds to the §5.2 re-baseline + scoring wire-in.

## Verdict: **NOT REDUNDANT — the gate CLEARS. #5 proceeds toward the TFI-modifier scoring sub-version (§5.2 first).**

The expected outcome ("likely closes display-only") did not materialize: the correlation condition fails decisively on every session-day.

---

## 1. Data

Frozen copy of `analysis_log.csv` (v0.8), 2,246 rows, 2026-07-03 15:57 → 07-10 20:35 UTC. Gate population: **NY×1 rows with non-empty AggrVel numerics** (EMPTY excluded per the handover instruction, not coerced): **1,960 rows, 400 burst fires (20.4%)** across **5 weekday NY session-days** (07-03 partial; 07-07/08/09/10 full). Overlap precision at n=400: ±4pp (95%). Spearman precision at n=1,960: ±~0.03. Res-3 (Asia/London): 267 rows / 36 fires — reported for context, excluded from the gate.

Note: `lean` itself is not a CSV column; `AggrVelNet` (the signed USD/s numerator of lean) is the logged directional intensity and is the runnable form of the gate's first argument.

## 2. Decision metrics

| Metric | Pooled | Per-day range (5 days) | Line | Met? |
|---|---|---|---|---|
| **Spearman(AggrVelNet, TFIValue)** | **0.61** | 0.53 – 0.68 | > 0.7 | **NO — on every day** |
| **Same-side fire-overlap** P(TFI same side \| BURST) | **86.0%** | 82.1 – 89.2% | > 80% | yes |

Redundancy requires **both**. The correlation condition fails everywhere ⇒ **not redundant** under the ticked rule. No borderline judgment needed: the pooled ρ sits 3 CI-widths below the line, and no single session-day reaches 0.7.

## 3. Supporting metrics (why the split verdict is the *expected* signature of a valid modifier)

| Metric | Value |
|---|---|
| Base P(TFI directional) | **91.4%** |
| P(TFI directional \| BURST) | 94.0% (lift vs base: ~nil) |
| **Converse: P(BURST same side \| TFI directional)** | **19.2%** |
| P(MicroCVD same-side ACCEL \| BURST) | 50.5% (base P(accel) 29.3%) |
| P(VolumeRatio ≥ 3 \| BURST) | 2.5% (base 0.5%) |
| Spearman(AggrVelBurstRatio, VolumeRatio) | 0.59 |
| Spearman(AggrVelNet, MicroCVDLate) | 0.71 |

Reading: TFI is directional on **91% of all rows** (its 0.15 threshold makes it nearly always-on), so "burst agrees with TFI when it fires" (86%) is close to unavoidable and carries little information — the base-rate context §5.1 asks for. What the burst adds is **selectivity**: it fires on only 20% of rows (19.2% of TFI-directional ones), singling out the bars with genuine USD-rate spikes. That is precisely the approved integration shape (§10.1: a modifier that upgrades same-side TFI and softens contra — not a second vote). Closed-bar `VolumeRatio ≥ 3` fires on 0.5% of rows and overlaps bursts at 2.5% — the intra-bar burst measure sees impulse that closed-bar volume structurally cannot.

**Honest wrinkle:** ρ(Net, MicroCVDLate) = 0.71 — marginally at the line against MicroCVD's *late USD segment* (same units, overlapping window; expected). The decision rule keys on TFI, and at the signal level MicroCVD's accel/decel product overlaps bursts at only 50.5% — level-of-rate vs change-of-rate remain distinct products. Recorded, not verdict-changing.

## 4. Caveats

- On-close 1-min rows are autocorrelated; effective n < nominal. Mitigated by the per-day slice — the verdict holds within every independent session-day.
- One-fortnight, one-regime book (the standing caveat class from the placed-geometry derivation). The §5.2 re-baseline gives the natural re-check point.
- 20.4% burst fire rate is **high** for "genuine impulse moments" — the provisional `burst_ratio_threshold` under-selects on the NY tape. This is exactly what §5.2 exists to fix; expect the re-baseline to raise selectivity before any wire-in.

## 5. What happens next (per the approved specs — nothing builds without the D-table tick)

1. **§5.2 per-session firing-rate re-baseline** (derivation, no code): set `burst_ratio_threshold` (+ confirm `norm_window_sec`) per session so BURST fires at genuine-impulse rates; before/after net-microstructure fire-rate table (v40/v41 format). **NY×1 is data-ready now** (400 fires). **Res-3 is NOT** (36 fires total) — recommended shape: **NY-first activation** via the existing per-session override machinery (v40 pattern), Asia/London staying effectively display-only until their own samples accumulate (~150 fires per session).
2. **Scoring wire-in sub-version** (⚠, own boundary, trader D-table sign-off): flip `scoring_enabled`, TFI-modifier semantics per §4.5 (`upgrade_bonus`/`contra_penalty` already in settings, tweaker-reachable once on).
3. **Sequencing (handover Q3):** the #5 scoring sub-version lands **first**, the funding time-anchored window build **after** — both at their own boundaries, one at a time (rule 1).

## Appendix — reproduction

Population filter: `ExecResolution==1 && AggrVelBurstRatio != ""`. Spearman = Pearson on average ranks (O(n²) awk, tie-corrected). Overlap: `AggrVelSignal ∈ {BURST_BUY, BURST_SELL}` matched against `TFISignal ∈ {BUY PRESSURE, SELL PRESSURE}` same-side. Columns: VolumeRatio 19, MicroCVDLate 91, MicroCVDSignal 93, ExecResolution 94, AggrVelBurstRatio 96, AggrVelNet 97, AggrVelSignal 98, TFIValue 99, TFISignal 100. Full command set in the 2026-07-13 session transcript.
