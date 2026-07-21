# D7 CONFIRMED-tag re-read on placed geometry (READ, 2026-07-22)

**Class:** Verdict-draft data read. Classification only — **no fix recommendation** (the branch between display re-tune vs scoring-quality is a coordinator/trader call).
**Backlog row:** `docs/backlog-dependency-map.md` line 12 (**D7 CONFIRMED-tag re-read (RE-OPENED)**) → this read discharges that row.
**Source doc:** `docs/post-websocket-post-calibration-backlog.md` D7 (INVESTIGATED + RESOLVED 2026-06-24 on the old yardstick; RE-OPENED after the 2026-07-21 report §6 showed CONFIRMED 13.3% [8–21] n=113 vs MEDIUM baseline ~33–35% on placed-vs-placed geometry — CI-separated).
**Prior report:** `bin/Debug/net8.0-windows/analysis_report_20260721_165219.md` §6 — the report §6 the RE-OPEN was triggered from.
**Frozen inputs:** `analysis_log.csv` (5936 rows), `analysis_eval_cache.csv` (schema v6, 8847 rows).

---

## 1. Method

- Frozen CSV + eval-cache join, ±2-second tolerance (log = second-resolution, eval = sub-second). Preferred a same-`ExecResolution` match at each timestamp key, fell back to nearest.
- Filters: weekday-only (Mon–Fri UTC), test-burst `InstanceId` starting `8706ebae` excluded.
- Session buckets: ASIA UTC 0–7, LONDON 8–12, NY 13–23 (engine's `session_volume` bounds from v34).
- **Populations:** `(Session × ExecResolution)` — NY×1, LONDON×3, ASIA×3.
- **DIRECTIONAL** rows only: `Verdict` contains `LONG` or `SHORT` and NOT `NO TRADE`. This deliberately excludes `NO TRADE [WEAK …]` (MTF-blocked-directional) and NO-TRADE-with-lean rows so the comparison is like-for-like committed-trade outcomes only.
- **Success** = `TargetEverHit = 1` at the tracker horizon; **failure** = `TargetEverHit = 0`. `NO_DATA` / `PENDING` / empty `TargetEverHit` rows excluded (not evaluable).
- **Wilson 95% CI** for every proportion.
- `ALIGNED` context tag is NO-TRADE-only by design (v30 F11 in `Core/ScoringEngine_Calculate_Scoring.vb`) — reported separately below, not in the directional table.

**Yardstick note.** The report §6 that RE-OPENED D7 uses **placed-target barrier hit at each row's recommended hold window** (§6 header) — a session-specific, tier-specific horizon. This read uses **`TargetEverHit`** — the placed-target-barrier "ever" during the tracker's full watch (whichever is shorter: window-expiry or barrier-hit). So this read's success rates are systematically higher than §6's, and the two numbers are not directly comparable. That difference IS the like-for-like framing the coordinator asked for.

---

## 2. Population walk

| Filter | Count |
|---|---:|
| Log rows | 5,936 |
| Weekday, non-burst | 5,372 |
| Joined with eval (±2s) | 5,372 |
| Directional | 1,395 |
| Directional evaluable (`TargetEverHit` ∈ {0,1}) | **1,270** |

Directional EvalOutcome distribution: SUCCESS 545 · ADVERSE_HIT 643 · WINDOW_EXPIRED 82 · NO_DATA 120 · PENDING 5.

Directional-evaluable rows by `VerdictContext`: **MOMENTUM_FADING 473 · STRUCTURALLY_WEAK 362 · CONFIRMED 340 · FLOW_UNCONFIRMED 95**. (No ALIGNED on directional — confirms the v30 F11 constraint holds.)

---

## 3. Success rate by VerdictContext — directional evaluable, per (Session × ExecResolution)

### NY×1 (n=902 directional-evaluable)

| Context | k / n | success | Wilson 95% CI |
|---|---:|---:|---|
| CONFIRMED | 118 / 239 | **49.4%** | [43% – 56%] |
| FLOW_UNCONFIRMED | 37 / 66 | 56.1% | [44% – 67%] |
| MOMENTUM_FADING | 153 / 337 | 45.4% | [40% – 51%] |
| STRUCTURALLY_WEAK | 127 / 260 | 48.8% | [43% – 55%] |

### LONDON×3 (n=267 directional-evaluable)

| Context | k / n | success | Wilson 95% CI |
|---|---:|---:|---|
| CONFIRMED | 38 / 75 | 50.7% | [40% – 62%] |
| FLOW_UNCONFIRMED | 11 / 23 | 47.8% | [29% – 67%] |
| MOMENTUM_FADING | 48 / 96 | 50.0% | [40% – 60%] |
| STRUCTURALLY_WEAK | 33 / 73 | 45.2% | [34% – 57%] |

### ASIA×3 (n=101 directional-evaluable)

| Context | k / n | success | Wilson 95% CI |
|---|---:|---:|---|
| CONFIRMED | 18 / 26 | 69.2% | [50% – 83%] |
| FLOW_UNCONFIRMED | 1 / 6 | 16.7% | [3% – 56%] |
| MOMENTUM_FADING | 22 / 40 | 55.0% | [40% – 69%] |
| STRUCTURALLY_WEAK | 21 / 29 | 72.4% | [54% – 85%] |

---

## 4. CONFIRMED vs pooled non-CONFIRMED (directional)

Non-CONFIRMED-directional = FLOW_UNCONFIRMED ∪ MOMENTUM_FADING ∪ STRUCTURALLY_WEAK on the same population.

| Population | CONFIRMED k/n (Wilson 95%) | non-CONFIRMED-dir k/n (Wilson 95%) | CI overlap? |
|---|---|---|---|
| NY×1 | **118/239 = 49.4% [43–56]** | **317/663 = 47.8% [44–52]** | Fully overlapping — no gap |
| LONDON×3 | 38/75 = 50.7% [40–62] | 92/192 = 47.9% [41–55] | Overlapping — no gap |
| ASIA×3 | 18/26 = 69.2% [50–83] | 44/75 = 58.7% [47–69] | Overlapping (CONFIRMED slightly higher) |

**CONFIRMED does NOT underperform the pooled non-CONFIRMED directional baseline on the `TargetEverHit` yardstick, in any of the three populations. All three comparisons show fully-overlapping CIs, with CONFIRMED slightly *higher* than or equal to the pool in every session.**

---

## 5. Band composition of CONFIRMED vs non-CONFIRMED (mix confound check)

| Population | Group | n | STRONG | MEDIUM | WEAK |
|---|---|---:|---:|---:|---:|
| NY×1 | CONFIRMED | 239 | 8 (3%) | 71 (30%) | 160 (67%) |
| NY×1 | non-CONFIRMED-dir | 663 | 57 (9%) | 191 (29%) | 415 (63%) |
| LONDON×3 | CONFIRMED | 75 | 4 (5%) | 10 (13%) | 61 (81%) |
| LONDON×3 | non-CONFIRMED-dir | 192 | 22 (11%) | 50 (26%) | 120 (63%) |
| ASIA×3 | CONFIRMED | 26 | 0 (0%) | 5 (19%) | 21 (81%) |
| ASIA×3 | non-CONFIRMED-dir | 75 | 1 (1%) | 25 (33%) | 49 (65%) |

**Reading:** CONFIRMED is materially **WEAK-heavy** relative to non-CONFIRMED in all three sessions, and STRONG-light (3% vs 9% in NY, 5% vs 11% in LONDON). Yet CONFIRMED's `TargetEverHit` rate matches or slightly beats the mix-heavier pool. If STRONG rows have higher hit rates (typical), the band mix would predict CONFIRMED to *underperform* — but it doesn't. So the tag is doing *some* selection work at the WEAK/MEDIUM levels, enough to offset its STRONG deficit.

The 20260721 report §6 gap — CONFIRMED 13.3% vs MEDIUM baseline 33% — is measured **at a specific hold horizon** where CONFIRMED's WEAK-heavy composition and the horizon's tightness may compound. `TargetEverHit` gives the trade the full watch window and the gap disappears.

---

## 6. Pre / post 2026-07-08 (B4b) split — NY×1

n=239 in the NY×1 CONFIRMED cell is well above 50 → split is defensible.

| Half | CONFIRMED k/n (Wilson 95%) | non-CONFIRMED-dir k/n (Wilson 95%) |
|---|---|---|
| pre  (< 2026-07-08 UTC) | 15/32 = **46.9%** [31–64] | 65/91 = **71.4%** [61–80] |
| post (≥ 2026-07-08 UTC) | 103/207 = **49.8%** [43–57] | 252/572 = **44.1%** [40–48] |

**Reading:** the pre/post shift is on the *non-CONFIRMED* side, not on CONFIRMED. Pre-B4b, non-CONFIRMED-directional was hitting `TargetEverHit` on 71% of rows (n=91) — implausibly high, likely the pre-B4b geometry / min-tradeable-move floor letting easier targets get tagged. Post-B4b, non-CONFIRMED settles at 44% and CONFIRMED at 50%. CONFIRMED itself is stable at ~47–50% across the split.

The pre-half CI-separated *reverse gap* (non-CONFIRMED >> CONFIRMED) is real for the pre-B4b population; it disappears post-B4b. Neither half shows CONFIRMED underperforming on the `TargetEverHit` yardstick — the pre-half shows CONFIRMED being outperformed by an inflated non-CONFIRMED baseline, not CONFIRMED being bad.

---

## 7. ALIGNED (lean-tag) note

`ALIGNED` on the directional-evaluable population: 0 rows in all three (Session × Exec) buckets — confirms it labels NO-TRADE only. On the NO-TRADE-evaluable population there are also 0 rows with a filled `TargetEverHit` (NO-TRADE runs are logged `EXCLUDED_NO_PREDICTION` in the eval cache, so no barriers are set) — cannot report success/failure. Reporting it separately as the coordinator asked, but there is nothing to report from this frozen data.

---

## 8. Classification

**ARTIFACT — measurement-horizon and band-mix, not tag defect.**

On the like-for-like yardstick the D7 06-24 resolution used (directional-only, `TargetEverHit`, per session × resolution), **CONFIRMED does not underperform** — it matches or slightly beats the pooled non-CONFIRMED-directional baseline in NY×1, LONDON×3, and ASIA×3, with fully-overlapping CIs in every case. The 06-24 conclusion ("CONFIRMED ≈ the directional baseline, apples-to-oranges juxtaposition of directional vs lean tags") **still holds** when the yardstick is `TargetEverHit`.

The 20260721 report §6 CI-separated gap (CONFIRMED 13.3% [8–21] n=113 vs MEDIUM 33–35%) is not reproducible on the like-for-like frame this read uses. Two composition/measurement effects account for it:
1. **§6 uses a per-row recommended-hold-window horizon**, not `TargetEverHit`. Any horizon tighter than "ever" penalises WEAK-heavy populations more (less time to reach the target). This read's `TargetEverHit` erases that horizon effect.
2. **CONFIRMED is WEAK-heavy** (67% WEAK in NY×1 vs 63% in non-CONFIRMED; STRONG 3% vs 9%). Under a horizon-tight yardstick, the WEAK skew depresses the composite success rate more than under `TargetEverHit`.

**What this does not decide.** Whether §6's report frame is the *right* frame for a "quality of tag" reading is a separate question (§6's frame is the one the trader looks at). If the trader wants the tag to look good under the report-§6 lens, that is a **display-layer choice** (re-tune the tag's threshold, split it by band, or restrict CONFIRMED to STRONG-plus). If the trader wants the tag to *cause* the engine to size differently, that is the **scoring-quality branch** (scoring change, approval-gated). This read does not recommend either branch — per the task's scope.

---

## 9. Caveats

1. **Yardstick difference is the finding, not a bug.** `TargetEverHit` and "hit-at-recommended-hold" are different measures; each is defensible. This read is deliberately on the `TargetEverHit` yardstick because that is what the frozen eval cache exposes cleanly per row and what the coordinator asked for.
2. **±2 s join tolerance.** Log timestamps are second-resolution, eval sub-second; the join preferred same-`ExecResolution` matches. Cross-resolution near-misses are possible but rare (weekday-Asia and weekday-London 3-min share none of NY×1's timestamps by construction).
3. **ASIA×3 CONFIRMED n=26** — the ≥30-per-cell heuristic is not met for that specific cell; ASIA numbers are directional but n-bound.
4. **`NO_DATA=120` and `PENDING=5`** in the directional pool are correctly excluded; keeping them would inflate n but not change hit rates (they have no `TargetEverHit`).
5. **Pre-B4b (< 2026-07-08) non-CONFIRMED 71.4% [61–80] n=91** is high enough to be notable — the pre-half looks like the geometry / floor pass shifted a lot. Not this read's job, but worth naming.

---

*Data read only. No code, no `settings.json` change, no scoring recommendation.*
