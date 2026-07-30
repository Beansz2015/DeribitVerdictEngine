# Book Absorption — Anchor Re-Derivation (post-v61 geometry, §12 <1%-engagement branch)

**Date:** 2026-07-30 · **Scope:** analysis-only, no code / no settings change in this pass
**Trigger:** §12 post-ship watch on the v61 geometry rescale — the "<1% engagement after 5 weekday sessions ⇒ re-derive with looser default" branch fired. Post-v61 pooled weekday collection shows **0 ABSORB flags across 1 569 directional runs** while 208 of those runs carry populated episode numerics (the tracker forms episodes; the classification anchors never trip).
**Data source:** frozen 2026-07-30 CSVs in scratchpad — `frozen_local_20260730.csv` (8 338 rows) + `frozen_aws_20260730.csv` (7 079 rows), dedup-pooled local-preferred per (UTC date, UTC hour) hour bucket, AWS backfills only hours with no local coverage. Pooled: 13 339 rows across 206 local hour-buckets + AWS gap-fill.
**Feature under calibration:** #6 book absorption v54 → v61 geometry rescale (`scoring_enabled:false`, all anchors PROVISIONAL per v61 spec §2.8).
**Method template:** [absorption-engagement-derivation-2026-07-23.md](absorption-engagement-derivation-2026-07-23.md) — this doc reruns that recipe against the post-v61 population.
**Related docs:** [absorption-geometry-rescale-proposal.md](absorption-geometry-rescale-proposal.md) · [absorption-geometry-rescale-spec-back.md](absorption-geometry-rescale-spec-back.md) · [book-absorption-proposal.md](book-absorption-proposal.md) §5.

---

## 1. Headline finding

**The v61 ATR-fraction geometry rescale widened the shell as designed, but the fundamental sparsity of "actually pressed with observable size change" events survives — engagement under the shipped v61 anchors = 0.00% pooled and per-session, and no cell in the candidate grid lands NY in the 3–8% design band.** The mechanism that limited engagement under v54 (per the 07-23 §5 diagnostic — 143 proximity-ACTIVE vs 18 actually-pressed) reproduces under v61: 208 proximity-ACTIVE vs 21 rows with `AggrUsd > 0` (14 with `Ratio > 0`).

- **Binding order (ratio → aggr → pull), unchanged from v54:**
  - `AbsorptionRatio ≥ 1.5` passes **1/208** pooled episode rows (a single ASIA outlier — 2026-07-28 03:30, ratio 3.03).
  - `AbsorptionAggrUsd ≥ 20 000` passes **2/208** (35 000 NY + 55 160 LONDON — only one of the two also carries a non-trivial ratio; the LONDON row's ratio is 0.14).
  - `AbsorptionPullFrac ≤ 0.75` passes **77/208** — the most permissive of the three.
- **Joint pass under current v61 anchors** (`ratio ≥ 1.5` AND `aggr ≥ 20 000` AND `pull ≤ 0.75`): **0** in every session. The two aggr-passing rows both fail ratio; the single ratio-passing row fails aggr; and the intersection is empty.
- **NY is the barren session under v61:** episode rows n=112, max ratio 0.51 (single event), max aggr 35 000 (single event), and **zero rows anywhere in the extended grid land NY in [3%, 8%]**.

The geometry rescale did what it was scoped to do (moves the tracker from measuring a $2/$6 tick shell to measuring a ~$4/$13 ATR-fraction shell; the proximity-ACTIVE population grew from 143 to 208 rows across a comparable weekday-week window). It did **not** address the deeper mechanism the 07-23 §5 diagnostic flagged — the `window_sec = 10` rolling reset and the D8 `pullFrac` inflation on sparse `postLB`. Those remain proposal §8 residuals awaiting a spec revision.

---

## 2. Data window + populations

- **UTC window (weekday, post-v61 cutoff `>= 2026-07-23`):** 2026-07-23 → 2026-07-30 (6 weekday dates: 07-23, 07-24, 07-27, 07-28, 07-29, 07-30). 07-30 is a partial day (n=177 rows vs ~880/day baseline).
- **Session buckets (UTC hour):** ASIA 0–7, LONDON 8–12, NY 13–23 (task-specified — differs slightly from the prior derivation's ASIA 22–07 / LONDON 07–13 / NY 13–22 in the boundary hours; recomputed populations under the task convention below).

| Slice | All rows | Directional rows | Episode-nonnull directional |
|---|---|---|---|
| POOLED (post-v61 weekday) | 4 599 | 1 569 | **208** |
| NY | 3 141 | 928 | 112 |
| LONDON | 516 | 252 | 33 |
| ASIA | 942 | 389 | 63 |

"Episode-nonnull" = `AbsorptionRatio` is a populated (non-blank) field, i.e. the tracker was carrying a primary episode at snapshot time — including NONE-classified and D8-vetoed episodes, by design (spec-back §2.2, CSV columns populated on any episode presence, not only ABSORB signals). All 1 569 directional rows carry `AbsorptionSignal = NONE`; **zero ABSORB flags anywhere in the pool**, confirming the §12 <1% watch trigger.

Sanity: pool size (13 339 rows) exceeds either source alone (local 8 338 + AWS gap-fill 5 001), and the per-date breakdown (855 / 907 / 880 / 887 / 893 / 177) is well-formed — no date collapse or bucket duplication.

---

## 3. Per-session distributions

Percentiles on episode-nonnull rows. Format: `n = min | p10 p25 p50 p75 p90 p95 | max`.

### 3a. POOLED (n=208)

| Field | Distribution |
|---|---|
| `AbsorptionRatio` | 0 &vert; 0 0 0 0 0 0.016 &vert; 3.030 |
| `AbsorptionAggrUsd` | 0 &vert; 0 0 0 0 3 146 &vert; 55 160 |
| `AbsorptionPullFrac` | 0 &vert; 0 0.170 0.969 1.516 3.981 9.574 &vert; 69.664 |

### 3b. NY (n=112)

| Field | Distribution |
|---|---|
| `AbsorptionRatio` | 0 &vert; 0 0 0 0 0 0 &vert; **0.510** |
| `AbsorptionAggrUsd` | 0 &vert; 0 0 0 0 0 4 &vert; **35 000** |
| `AbsorptionPullFrac` | 0 &vert; 0 0.030 0.869 1.524 4.144 9.551 &vert; 32.708 |

NY is the barren session: p95 ratio is 0.00; only the single 07-23 16:31 event (level 64 767.87, ratio 0.51, aggr 35 000, pull 1.17) touches the top-decile scale. NY has zero rows with ratio ≥ 1.0.

### 3c. LONDON (n=33)

| Field | Distribution |
|---|---|
| `AbsorptionRatio` | 0 &vert; 0 0 0 0 0.008 0.098 &vert; 0.240 |
| `AbsorptionAggrUsd` | 0 &vert; 0 0 0 0 32 3 132 &vert; **55 160** |
| `AbsorptionPullFrac` | 0 &vert; 0 0.620 0.993 1.383 2.937 8.556 &vert; 69.664 |

LONDON's max aggr (55 160, 2026-07-28 08:33) is the largest single pressed volume in the entire pool — but the paired ratio is 0.14 (pressing = 0.14× the depletion), well below any meaningful "absorption" floor.

### 3d. ASIA (n=63)

| Field | Distribution |
|---|---|
| `AbsorptionRatio` | 0 &vert; 0 0 0 0 0.008 0.038 &vert; **3.030** |
| `AbsorptionAggrUsd` | 0 &vert; 0 0 0 0 112 1 629 &vert; 15 150 |
| `AbsorptionPullFrac` | 0 &vert; 0 0.374 1.052 1.579 3.625 8.154 &vert; 44.416 |

ASIA carries the single unambiguous absorption reference event in the whole pool: 2026-07-28 03:30, ratio 3.03, aggr 15 150, pull 0.84 — the only row that would satisfy ratio ≥ 1.5 anywhere.

### 3e. Marginal pass-rates on episode-nonnull rows

Marginal counts (each anchor alone, on episode-nonnull rows):

| Anchor at current v61 value | POOLED | NY | LONDON | ASIA |
|---|---|---|---|---|
| `AbsorptionRatio ≥ 1.5` | 1/208 | 0/112 | 0/33 | 1/63 |
| `AbsorptionAggrUsd ≥ 20 000` | 2/208 | 1/112 | 1/33 | 0/63 |
| `AbsorptionPullFrac ≤ 0.75` | 77/208 | 47/112 | 10/33 | 20/63 |

**Ratio binds hardest.** Even at loose ratio thresholds (0.05, 0.1, 0.2, 0.3, 0.5), pooled pass-rates on episode rows are 8, 6, 3, 2, 2 — the ratio field is dominated by zero-valued rows (proximity-ACTIVE but no observable depletion within the window), a shape that mirrors the 07-23 diagnostic.

---

## 4. Candidate grid — projected flag rate = passing rows ÷ directional rows

Flag rate approximates run-level engagement from row-level numerics: it counts snapshot rows whose episode fields satisfy the tuple, over all directional snapshot rows. This differs mildly from run-level engagement (a single episode may span multiple auto-run snapshots) but is representative for order-of-magnitude comparison; the true engagement rate is bounded above by this figure.

Grid: `absorb_ratio ∈ {1.0, 1.2, 1.5}` × `min_aggr_usd ∈ {5000, 10000, 20000}` × `max_pull_frac ∈ {0.75, 0.9, 1.0}`. Flag rate reported as percent.

### 4a. POOLED

| ratio | aggr | pull | passes | flag % |
|---|---|---|---|---|
| 1.0 | 5 000 | 0.75 | 0 | 0.000 |
| 1.0 | 5 000 | 0.90 | 1 | 0.064 |
| 1.0 | 5 000 | 1.00 | 1 | 0.064 |
| 1.0 | 10 000 | 0.90 | 1 | 0.064 |
| 1.0 | 20 000 | 1.00 | 0 | 0.000 |
| 1.2 | 5 000 | 0.90 | 1 | 0.064 |
| 1.5 | 5 000 | 0.90 | 1 | 0.064 |
| 1.5 | 10 000 | 1.00 | 1 | 0.064 |
| 1.5 | 20 000 | 1.00 | 0 | 0.000 |

The single ASIA event (ratio 3.03, aggr 15 150, pull 0.84) is the one row that survives any tuple with `ratio ≥ 1.0 AND aggr ≤ 15 000 AND pull ≤ 0.9`. **No POOLED cell lands in [3%, 8%].** Full grid in `scratchpad/extended.out.txt`; the cells above cover the informative diagonal.

### 4b. NY

**Every cell in the task-specified grid produces 0 passes and 0.000% flag rate.** NY has zero episode rows meeting `ratio ≥ 1.0` and only 1 row meeting `aggr ≥ 5 000`; the two sets do not overlap.

Extended lower-ratio cells (informational — outside the task-specified grid): even at `ratio ≥ 0.05, aggr ≥ 100, pull ≤ 1.5`, NY passes 0 rows (the top NY events fail `pull ≤ 1.5`; the 07-23 16:31 event has pull 1.17). The two NY events with any traction — 07-23 16:31 (ratio 0.51 / aggr 35 000 / pull 1.17) and 07-29 19:05 (ratio 0.13 / aggr 2 860 / pull 1.06) — both fail every reasonable pull ceiling short of removing D8 entirely, and both fail even generously loose aggr+ratio combinations because they aren't jointly strong.

### 4c. LONDON

| ratio | aggr | pull | passes | flag % |
|---|---|---|---|---|
| 1.0..1.5 | any | any | 0 | 0.000 |

LONDON has zero episode rows with `ratio ≥ 1.0`. At `ratio ≥ 0.05, aggr ≤ 2 000, pull ≤ 0.9`, LONDON gets 1 row (0.397%). No cell in-band.

### 4d. ASIA

| ratio | aggr | pull | passes | flag % |
|---|---|---|---|---|
| 1.0..1.5 | 5 000 | 0.90 | 1 | 0.257 |
| 1.0..1.5 | 5 000 | 1.00 | 1 | 0.257 |
| 1.0..1.5 | 10 000 | 0.90 | 1 | 0.257 |
| 1.0..1.5 | 20 000 | any | 0 | 0.000 |

The single 07-28 03:30 event carries ASIA's entire pass count. No cell in-band.

**Summary of grid coverage:** the task-specified grid produces a peak POOLED flag rate of 0.064% and a peak NY flag rate of 0.000%. No cell lands NY in the 3–8% design band; no cell lands POOLED in-band either. Loosening below the task grid (`ratio → 0.05`, `aggr → 100`, `pull → 1.0`) yields at best 0.127% POOLED. **The 3–8% target cannot be reached from the v61 population with any reasonable anchor set — the same conclusion as the v54 07-23 pass, now confirmed post-geometry-rescale.**

### 4e. `depletion_floor_usd` (V4)

The `depletion_floor_usd` anchor governs the divide-by-nothing guard inside the ratio computation (`aggr / max(depletion, floor)`). It is not directly observable from the CSV — the CSV carries the resolved `AbsorptionRatio`, not the underlying `depletion` value. Distributions do not suggest the floor is dominating the shape (ratios cluster at 0.00 because pressing itself is zero, not because the ratio is being compressed by the floor); the ceiling is set by observable pressed volume, not the divisor. **Leave `depletion_floor_usd` untouched at 5 000 (v61 provisional).**

---

## 5. Recommended anchor set

Two honest paths, matching the 07-23 J-table structure:

- **Path A — SHIP the loosest still-semantically-meaningful anchors** as a display-only settings pass, generating whatever thin ABSORB stream is possible so §5.2 (n ≥ 30 flagged evaluated rows) has *any* signal to build from. Under the recommended V-table below, projected flag rate is ~0.06% POOLED / 0.00% NY / 0.26% ASIA — meaning it takes **~10–15 weekday-weeks** to accumulate 30 flagged evaluated rows even with the loosening. That is a long clock.
- **Path B — HOLD v61 anchors** and pursue a spec-side revision (proposal §8 residuals: `window_sec` too short; D8 `pullFrac` inflation on sparse `postLB`) before touching the anchors. The rationale: no anchor set on the observable population reaches the design band; loosening moves the problem sideways rather than solving it. This aligns with the trader-profile conservative-false-positive posture — the honest failure mode is "the tracker doesn't observe enough real pressing", not "the thresholds are one tick too high".

**Coordinator recommendation: Path B.** The 07-23 J-table already picked Path A once (as J1e, on the same reasoning); Path A was executed via the v61 geometry rescale (widening the shell to unblock observation). The v61 collection shows the widened shell is not the limiting factor — the observation mechanism itself is. Repeating Path A now, on evidence that the widened shell alone did not raise flag rates into a usable band, buys collection but not answers. The next honest lever is the `window_sec`/D8 spec revision (a separate proposal, out of this pass's scope), and holding v61 anchors while that spec is drafted keeps the tracker in a coherent state.

**But if the trader ticks Path A** (matching the 07-23 precedent and prioritising *any* stream over zero), the V-table below is the loosest-still-meaningful anchor set from the post-v61 evidence.

### 5a. V-table (await trader tick)

| # | Decision | Recommended value | Rationale |
|---|---|---|---|
| **V1** | `absorb_ratio` | **0.5** | Sits at NY p95+ (0.00 → 0.51 max), at LONDON p95+ (0.098 → 0.240 max), and just above ASIA p95 (0.038). Selects the single ASIA event (3.03) and would select the two NY-nearest events if their `aggr`/`pull` also cleared. Semantically the minimum floor for calling a state "absorption" (pressing must exceed half of depletion, otherwise it is just aggressive volume). |
| **V2** | `default.min_aggr_usd` | **5 000** | POOLED p95 aggr is 146 USD, max 55 160; 5 000 sits at ~p97, filtering junk-aggr episodes while keeping the two 5k+ NY/LONDON events and the ASIA reference event (15 150) in play. Below 5 000 the population is dominated by <100 USD scratches — not signal. |
| **V3** | `max_pull_frac` | **1.0** | POOLED p90 pull is 3.98, p95 is 9.57 — the D8 spoof-guard is filtering ~half the "real" candidate events at 0.75 (NY 47/112 pass; LONDON 10/33; ASIA 20/63). During collection under sparse observation, D8's `pullFrac` inflation on small `postLB` is a proposal §8 residual — loosening to 1.0 (still filters the >1.0 anomalies that indicate visibility-mask issues, keeping the guard's shape) reduces the D8 clip during the collection window without disabling it. |
| **V4** | `depletion_floor_usd` | **Y (untouched, 5 000)** | Not directly observable from CSV; ratio distributions do not suggest the floor is dominating the shape. Leave alone until a spec revision addresses the observation mechanism. |

Projected engagement under `(0.5, 5 000, 1.0)`:

| Session | passes | directional | flag % |
|---|---|---|---|
| POOLED | 1 | 1 569 | 0.064 |
| NY | 0 | 928 | 0.000 |
| LONDON | 0 | 252 | 0.000 |
| ASIA | 1 | 389 | 0.257 |

**Per-session overrides — none recommended.** The 07-23 J-table declined per-session overrides on sample-size grounds (J1a–J1b). Same reasoning holds — the ASIA population (n=63 episode rows, 1 event passing) cannot support a per-session anchor derivation. Ship `default` only; `sessions.{}` stays empty.

### 5b. Ship shape (if Path A ticks)

- Settings-only pass. `scoring_enabled` stays **false**. Display/CSV-only surface (the CSV columns already exist since v54, values continue populating).
- **NO ⚠**, NOT a dataset boundary — the CSV header is unchanged, the tracker output shape is unchanged, only three anchor values move. The scoring pipeline is untouched.
- **§5 activation gates unchanged** — still ~2 weekday-weeks minimum after the re-anchor before the twice-evidence-gated activation review can run (5.1 independence + 5.2 outcome gradient on n ≥ 30 flagged evaluated rows). With projected ~0.06% POOLED flag rate, the n=30 clock is materially longer than the 07-23 estimate (~10–15 weekday-weeks vs the ~6–10 weeks the prior derivation projected). The re-collection clock **restarts at the knob turn** per the §12 watch convention.
- Version bump + `change_log` newest-first + §15 row + POCO defaults ride the commit (v33/v34/v41/v53 pattern).
- Display-string parity — strip-only surface (v54 D6 precedent, spec-back §2.6); no snapshot/card binding obligation; state so in the commit message.
- HC23 fences carry to the anchor keys (v61 spec-back §2.10) — unchanged in shape.

### 5c. Sanity note (mandatory)

All projected flag rates assume the logged 208 episode rows are representative of what future episodes will look like under v61 geometry. They may not be — 6 weekday days is a small window; ratio/aggr distributions may broaden materially with more data (or may confirm the sparsity is structural). The post-ship watch is the only real re-verification. In particular: if the LONDON 55 160 aggr event or the NY 35 000 aggr event proves rare rather than representative, even the projected 0.06% POOLED rate may not hold.

**Alternative if Path B ticks:** the next artifact is a proposal file re-opening proposal §8 residuals — `window_sec` and D8 `pullFrac` inflation — with the evidence that geometry-rescale alone did not raise flag rates. No settings change in this pass; the anchors stay at v61 provisional values pending that spec.

---

## 6. Method & reproducibility

- **Frozen CSVs:** `scratchpad/frozen_local_20260730.csv` + `scratchpad/frozen_aws_20260730.csv`.
- **Dedup rule:** local-preferred per (UTC date, UTC hour) hour bucket — all local rows kept in any bucket local has coverage in; AWS backfills only hours with zero local coverage. 206 local hour-buckets + AWS-only hours.
- **Analysis scripts (scratchpad, ASCII-only, PowerShell 5.1):** `rederive.ps1` (primary — populations, distributions, joint pass, task grid), `extended.ps1` (marginal thresholds + extended grid + top-event listings), `cross_check.ps1` (Signal-value distribution + field-population cross-check). Outputs pinned to `rederive.out.txt` and `extended.out.txt`.
- **Session buckets (UTC hour):** ASIA 0–7, LONDON 8–12, NY 13–23 (task-specified).
- **Directional definition:** `Verdict` contains "LONG" or "SHORT" (STRONG / plain / WEAK — the same set used by §5 outcome-gradient audits).
- **Episode-nonnull definition:** `AbsorptionRatio` field is populated (non-blank string, including "0" and "0.0" which are valid tracker outputs indicating an episode formed but no depletion was observed in the window).
