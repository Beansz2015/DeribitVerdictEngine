# Book Absorption — Target-Engagement Derivation (§5 calibration pass)

**Date:** 2026-07-23 · **Scope:** analysis-only, no code / no settings changes
**Data source:** `bin\Debug\net8.0-windows\analysis_log.csv` frozen to scratchpad `abs_engage_frozen.csv` (6510 rows, first ts 2026-07-03 15:57 UTC, last ts 2026-07-22 16:58 UTC)
**Population filter:** weekday-only, `InstanceId` prefix `8706ebae*` excluded (192 rows), leaving **5946 weekday rows**
**Feature under calibration:** #6 book absorption v54 (`scoring_enabled:false`, all anchors PROVISIONAL per proposal §5)
**Related docs:** [book-absorption-proposal.md](book-absorption-proposal.md) §4/§5/§6 · [book-absorption-spec-back.md](book-absorption-spec-back.md)

---

## 1. Headline finding

**Engagement under the shipped anchors = 0.00% in every session, well below the 3–8% target band, and the anchor set as currently written cannot be re-tuned into that band from this data.** The failure is not "anchors slightly too tight"; it is threefold and the largest gap is at the metric ceiling itself.

- **Episode-row counts** (rows where `AbsorptionRatio` was populated, i.e. the tracker was proximity-ACTIVE at run time): **143 rows** across all sessions (392 including empty-string rows counted as populated by the "non-empty" rule — 249 of those carry the literal string of "0"). Cross-checked against the setup's ~390 count: the 390 figure counts *any* non-empty ratio string (including "0"); the 143 is the strict "value was serialised" subset. Both agree the engagement rate is 0%.
- **Pressed rows** (`AbsorptionAggrUsd > 0`, meaning aggressive prints actually hit the band during the episode): **18 rows total, 5 weekday days, 3 sessions** (ASIA 1, LONDON 3, NY 14).
- **Depleted rows** (`AbsorptionRatio > 0`, band actually net-depleted at least once): **12 rows total** (ASIA 0, LONDON 2, NY 10).
- **`AbsorptionSignal` values across all 143 populated rows: NONE for every row.** The ratio threshold (`absorb_ratio ≥ 3.0`) is never met; the aggr floor (`min_aggr_usd ≥ 150 000`) is never met either.

### Which anchor binds

All three shipped anchors are above the observed ceiling; joint failure is universal:

| Session | Episode rows | fail `aggr ≥ 150k` | fail `ratio ≥ 3.0` | fail `pull ≤ 0.5` | pass all |
|---|---|---|---|---|---|
| ASIA | 6 | 6 (100%) | 6 (100%) | 5 (83.3%) | **0** |
| LONDON | 32 | 32 (100%) | 32 (100%) | 22 (68.8%) | **0** |
| NY | 105 | 105 (100%) | 105 (100%) | 53 (50.5%) | **0** |

The `min_aggr_usd` and `absorb_ratio` anchors are BOTH above the entire observed distribution simultaneously — the joint "which binds first" test is degenerate. Observed maxima across all sessions:

| Session | max `AggrUsd` | max `Ratio` | max `PullFrac` |
|---|---|---|---|
| ASIA | 30 | 0.00 | 2.87 |
| LONDON | 3 000 | 0.12 | 2.12 |
| **NY** | **50 010** | **2.00** | **9.35** |

The tallest NY event (2026-07-22 15:07:04, level 65 844.5) hit ratio 1.10 on 27 540 USD pressed — the only row in the whole dataset that comes within an order of magnitude of any shipped anchor. Even it fails `ratio ≥ 3.0` and fails `aggr ≥ 150 000`.

---

## 2. Per-session distributions

### 2a. Populations (weekday, non-bad-instance)

| Session | All rows | Directional rows | Post-v54 all | Post-v54 directional |
|---|---|---|---|---|
| ASIA | 261 | 115 | 90 | 50 |
| LONDON | 967 | 460 | 390 | 196 |
| NY | 4 718 | 1 765 | 1 248 | 493 |

"Post-v54" = timestamp ≥ 2026-07-17 (first date with any nonzero episode; v54 shipped that day). Engagement denominator uses directional runs (verdicts containing LONG/SHORT), per §4.4/§5 convention.

### 2b. Percentiles on "pressed" rows (`aggr > 0`) — the only genuinely usable subset

| Session | n | AggrUsd p50 / p75 / p95 / max | Ratio p50 / p75 / p95 / max | PullFrac p50 / p75 / p95 / max |
|---|---|---|---|---|
| ASIA | 1 | 30 / 30 / 30 / 30 | 0.00 / 0.00 / 0.00 / 0.00 | 1.62 / 1.62 / 1.62 / 1.62 |
| LONDON | 3 | 450 / 1 725 / 2 745 / 3 000 | 0.02 / 0.07 / 0.11 / 0.12 | 1.00 / 1.00 / 1.00 / 1.00 |
| NY | 14 | 1 330 / 7 840 / 35 404 / 50 010 | 0.06 / 0.32 / 1.41 / 2.00 | 0.83 / 0.99 / 1.08 / 1.17 |

**Share with `pullFrac > 0.5`** (rows the D8 spoof guard would veto at the shipped `max_pull_frac = 0.5`): ASIA 100%, LONDON 100%, NY 78.6%. Even relaxed to `pullFrac ≤ 1.0`, NY loses ~21% of pressed rows to D8 alone; LONDON and ASIA populations do not survive at all because their episodes bunch tightly around pullFrac ≈ 1.00.

### 2c. Distribution on full episode rows (including `aggr = 0`, `ratio = 0` — most of the population)

Given that 105 of 143 episode rows have `aggr = 0` and 133 of 143 have `ratio = 0`, percentiles on the full episode pool are dominated by zeros (p50 = 0 for all three fields in every session). The tracker registers "ACTIVE by proximity" far more often than it registers "the band was actually pressed with observable size change" — that gap is the diagnostic that matters and is discussed in §5.

---

## 3. Candidate anchor sweep

Grid: `min_aggr_usd ∈ {100, 250, 500, 1k, 2k, 5k, 10k, 25k}`; `absorb_ratio ∈ {0.05, 0.1, 0.2, 0.3, 0.5, 1.0, 1.5}`; `max_pull_frac ∈ {0.5, 1.0, 1.5}`.
Engagement = passing rows ÷ post-v54 directional rows.

**No tuple in the entire grid — for any session — lands engagement in [3%, 8%].** The ceiling is imposed by the tiny "pressed" population: even at the floor of the grid, NY tops out at ~1.62% (8/493). ASIA and LONDON never reach 1%.

### 3a. Best-case NY tuples (loosest still-meaningful settings)

| min_aggr | min_ratio | max_pull | passes | eng (post-v54 NY) |
|---|---|---|---|---|
| 100 | 0.05 | 1.50 | 8 | **1.62%** |
| 1 000 | 0.05 | 1.50 | 8 | 1.62% |
| 2 000 | 0.05 | 1.00 | 6 | 1.22% |
| 5 000 | 0.20 | 1.00 | 5 | 1.01% |
| 10 000 | 0.30 | 1.00 | 5 | 1.01% |
| 25 000 | 0.30 | 1.00 | 3 | 0.61% |
| 25 000 | 1.00 | 0.50 | 1 | 0.20% |

Note the plateau at 5–6 passes for a wide range of `min_aggr` and `min_ratio`: this is the small handful of NY events with any real pressing (§1 table above); the moment `min_ratio` climbs to 0.5+ the passing set collapses to 1–2 rows.

### 3b. LONDON and ASIA

No tuple with `min_ratio ≥ 0.2` fires at all. The strictest still-permissive tuple (`ma=100, mr=0.05, mp=1.5`) fires **0 rows in ASIA** (max ratio observed is 0.00) and **2 rows in LONDON** (1.02% engagement). Both sessions currently lack the observation budget to derive per-session anchors that mean anything.

---

## 4. Recommended action — the trader's J-table

The 3–8% engagement band cannot be reached from this data with any reasonable anchor set. The mechanistic reason (§5) is that the tracker sees very few "band was actually pressed with observable size change" events — not that the thresholds are one tick too high. Two responses are honest:

**Path A — SHIP a display-only loosening now** (small settings pass, no ⚠), letting the loosened anchors surface any ABSORB events to CSV so §5 has a real signal budget to work from next cycle. Anchors below are set to just-above-the-current-ceiling values so the tracker CAN fire without setting thresholds so low the metric becomes meaningless.

**Path B — HOLD the shipped anchors** and wait until enough episode volume accumulates that percentile-derived re-anchors are statistically defensible. Given only 18 pressed rows in 5 weekday days, holding another 2–4 weekday weeks would raise the sample to ~70–150 pressed rows and permit an actual §5-quality derivation.

### J-table (trader tick)

| # | Decision | Recommendation | Rationale |
|---|---|---|---|
| **J1a** | ASIA anchors `min_aggr_usd` / `absorb_ratio` / `max_pull_frac` | **No override yet — inherit `default`.** ASIA had 0 usable rows (max ratio 0.00). Any per-session pick would be fabrication. | Sample too small; wait for more data. |
| **J1b** | LONDON anchors | **No override yet — inherit `default`.** Only 2 depleted rows; max ratio 0.12. | Same reason. |
| **J1c** | NY anchors — if Path A | **`min_aggr_usd: 5 000`, `absorb_ratio: 0.5`, `max_pull_frac: 1.0`** (projected engagement ~0.6–1.0%). This lets the strongest 2–5 NY events per weekday-week reach ACTIVE-signal state. | Anchors sit at NY's p75 / p85 (aggr) and just above p75 (ratio) — meaning if the underlying process was really absorbing, the top-quartile events fire while junk stays silent. Still well below 3–8% target — that's the "collect first, re-derive later" trade-off. |
| **J1d** | `default` block — if Path A | **`min_aggr_usd: 5 000`, `absorb_ratio: 0.5`** (single-tier fallback used by all sessions until per-session data exists). | Matches J1c; ASIA/LONDON inherit until their populations grow. |
| **J1e** | Path A vs Path B | **Path A (ship the display-only loosening).** The current anchors emit exactly zero ABSORB events, which means no adverse-outcome gradient can ever be measured (§5.2 needs n ≥ 30 flagged evaluated rows). Loosening moves the population toward measurability without breaking the display-only stance. | Neutral to the trader — no scoring change, no ⚠. |
| **J2** | Ship shape | **DISPLAY-ONLY settings pass**, `scoring_enabled` stays `false`, no new fixtures, single settings-block edit + `change_log` + §15 entry. Version bump, no dataset boundary. | Same class as the v54 build — behaviour-neutral for scoring; only CSV emission and TAPE-strip tag change. |
| **J3** | Flagged-row activation clock | **Restarts at ship.** With projected engagement ~1% NY (0.5–1.0 flagged rows per NY session), reaching n ≥ 30 flagged evaluated rows will take **~6–10 weekday-weeks** post-ship, not the ~1.5–2 weeks the standard band would give. If the trader wants faster convergence, choose more aggressive J1 values (e.g. `min_ratio: 0.3`, `min_aggr: 2 000` — projected NY engagement ~1.2%, still under target). | This is the honest cost of a small usable population. |

**Sanity note (mandatory):** all projected engagement figures assume the logged 18 pressed rows are representative of what future episodes will look like. They may not be — the tracker only started collecting on 2026-07-17 and 5 weekday days is a small window; the ratio/aggr distribution may broaden materially with more data (or the D8 pullFrac distribution may prove even worse). The post-ship watch is the only real re-verification.

---

## 5. Diagnostic — why the tracker under-engages (out of scope for J-table; flagged for review)

The three-way anchor failure is a symptom; the mechanism is that the tracker sees "proximity-ACTIVE" **143 times** but "actually pressed" only **18 times**. Two candidate root causes worth naming (not yet worth acting on):

1. **`window_sec = 10` is short vs how long price actually presses a carried level in the top-10 depth.** A 5m swing being tested during a slow grind may take minutes to unfold, but the rolling 10-second aggregator resets so aggressive prints separated by more than 10 s never sum. The proposal picked 10 s to keep the tracker responsive; the collected data hints it also keeps it blind.
2. **`max_pull_frac = 0.5` is a mid-quartile clip.** NY p50 of pressed-row `PullFrac` is 0.83 and LONDON/ASIA are ~1.0. The D8 formula (`pullLB / max(postLB, 25000)`) blows up when `postLB` is small (few posts observed) even without spoofing — the top-10 snapshot feed genuinely shifts ladder window position between snapshots, and D8's "visibility mask" (§4.2 implementer note) may not be excluding those shifts as intended.

Both are proposal-§8 territory ("residual stated honestly") and neither can be acted on without a spec revision + trader tick. Flag only; do not fix in this pass.

---

## 6. Method & reproducibility

Frozen CSV: `C:\Users\user\AppData\Local\Temp\claude\C--Dev-DeribitVerdictEngine\<session>\scratchpad\abs_engage_frozen.csv` (this pass).
Analysis scripts (scratchpad): `analyze.py`, `audit.py`, `analyze2.py`, `sweep3.py`, `peek.py`.
Session buckets (UTC hour): ASIA 22–07, LONDON 07–13, NY 13–22 (matches `DynamicNorms.ApplySessionVolume` convention).
Directional = `Verdict` contains "LONG" or "SHORT" (STRONG / WEAK / plain — the same set used by §5 outcome-gradient audits).
