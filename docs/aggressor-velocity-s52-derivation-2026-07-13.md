# #5 Aggressor Velocity — §5.2 Per-Session Re-baseline Derivation

**Date:** 2026-07-13 · **Seat:** Fable coordinator · **Type:** derivation (no code changed; values for the wire-in sub-version)
**Parent:** `aggressor-velocity-proposal.md` §5.2 (authorized by the §5.1 gate verdict, `aggressor-velocity-correlation-gate-verdict-2026-07-13.md` — NOT redundant) · **Precedent:** the placed-geometry derivation → DG-table → build pattern.
**Scope:** derive `burst_ratio_threshold` per session so `BURST_*` fires at genuine-impulse rates; confirm `norm_window_sec`; preview the modifier's engagement. The **wire-in build** (flip `scoring_enabled`, §4.5 modifier mechanics) happens only after the S-table below is ticked, as its own ⚠ boundary.

---

## 1. Data

Frozen v0.8 corpus, NY×1 gate rows n=1,960 (5 weekday NY session-days, 07-03 partial + 07-07..07-10 full), 400 fires at the provisional config. Res-3: 267 rows / 36 fires — **not derivable** (below any defensible sample; see S2).

Current config: `default {norm_window_sec 120, burst_ratio_threshold 2.5}`, NY override `norm_window_sec 60`, `direction_lean_floor 0.2`, `gross_floor_usd_per_sec 50`.

Method note: raising T only removes fires (the lean floor is unchanged), so fire rates at candidate T ≥ 2.5 are computed exactly from the logged book: `fires(T) = {AggrVelSignal = BURST_* ∧ AggrVelBurstRatio ≥ T}`.

## 2. The provisional threshold under-selects

T=2.5 sits at **p80** of the NY burstRatio distribution → **20.4%** fire rate. One bar in five is not a "genuine impulse moment"; a modifier this frequent drifts toward the always-on class the audit retired (OFIMomentum, ~90% active). Distribution: p50 0.67 · p75 2.02 · p80 2.57 · **p90 4.51** · p95 6.46 · p99 9.54.

**Anchor check (why not the engine's other impulse notions):** ROC-active (|ROC| ≥ 0.1) covers 46.1% of NY rows — too broad to anchor a burst tag. Closed-bar `VolumeRatio ≥ 1.5` covers 1.5% (≥3× = 0.5%) — an on-close-cadence artifact, too tight. Neither is a usable firing-rate-match reference, so the anchor is the distribution knee + selectivity intent + per-day stability, tabled below.

## 3. Candidate table (NY×1)

| T | fire rate | same-side TFI | upgrades/day | softens/day | per-day range (full days) |
|---|---|---|---|---|---|
| 2.5 (current) | 20.4% | 86.0% | 68.8 | 6.4 | — |
| 3.0 | 16.3% | 88.7% | 56.6 | 3.4 | — |
| 3.5 | 13.7% | 88.8% | 47.6 | 2.8 | 12.0–15.4% |
| 4.0 | 11.7% | 89.6% | 41.2 | 2.2 | 10.1–13.1% |
| **4.5 (rec)** | **9.9%** | **89.7%** | **35.0** | **2.2** | **8.0–12.1%** |
| 5.0 | 8.4% | 92.1% | 30.2 | 1.4 | — |
| 6.0 | 6.1% | 93.3% | 22.4 | 0.8 | — |

(07-03, the partial day starting 15:57 UTC, runs ~4pp hotter at every T — an hour-mix composition effect, excluded from the stability ranges.)

**Recommendation: NY `burst_ratio_threshold` = 4.5** (the p90 knee). Reasoning: (a) ~10% fire rate reads as "genuine impulse" on a 1-min tape (~35 upgraded moments/day) while staying far from the always-on failure mode; (b) same-side agreement improves with T (86→90%) — higher bursts are cleaner; (c) day-to-day stability ±2pp on full days; (d) the contra-soften arm (2.2/day) survives — at T≥5 it effectively dies (≤1.4/day), and that arm is the §4.5 "genuine warning" half of the design. T=5.0 is the defensible tighter alternative if you want single-digit fires with maximal agreement; T=4.0 the looser one.

Note (recorded, not blocking): P(ROC-active | fire) *falls* as T rises (46.8% → 30.8%) — big bursts often precede or sit outside closed-bar momentum. That is consistent with the tick-resolution-earliness thesis (§3 of the proposal), but it means burst-upgraded TFI votes will sometimes fire on bars the ROC stack calls quiet. The conditional-outcome evidence for whether those are the *good* ones arrives with the post-wire-in book.

## 4. Norm window (confirm, no change)

NY 60s / default 120s stand. Warmup health: only ~19 of 2,246 rows carry empty AggrVel numerics (restart warmups — negligible). Per-day fire-rate stability at fixed T (±2pp, §3) is the indirect evidence the 60s norm isn't producing day-regime artifacts. Window↔threshold coupling caveat stands: 4.5 is derived FOR fast=5s/norm=60s; changing either re-opens this derivation.

## 5. Res-3 (Asia/London): not derivable — NY-first activation

36 fires across all res-3 rows. A per-session threshold needs ~150+ fires per session (several more weeks at current cadence). Two activation shapes:

- **(a) — recommended:** the wire-in modifier applies only to sessions with a derived threshold (NY at activation). LONDON/ASIA keep the exploratory default for **display** (TAPE strip burst tags stay alive and keep collecting), but the scoring modifier is inert there until their own §5.2 pass.
- (b) simpler-but-blunt: sentinel-high `burst_ratio_threshold` in the LONDON/ASIA session overrides — one mechanism, but it kills the res-3 TAPE display and stops accumulating classified fires.

## 6. Wire-in engagement preview (at T=4.5, NY)

TFI is directional on 91.4% of NY rows; the modifier touches the ~10% of rows where a qualifying burst fires: **~35 upgrades + ~2 softens per NY session-day**, i.e. the modifier re-weights ~7% of TFI's directional votes and leaves 93% untouched. Net Microstructure fire count unchanged by construction (a modifier, not a vote — §4.5); the before/after fire-rate table for the spec-back reduces to this row-share statement plus the score-shift count (±`upgrade_bonus`/`contra_penalty` = ±1 on ~37 rows/day).

## 7. S-table (trader sign-off — the wire-in builds only after these are ticked)

| # | Decision | Recommendation |
|---|---|---|
| **S1** | NY `burst_ratio_threshold` | **4.5** (p90, ~10%); alternatives 4.0 looser / 5.0 tighter |
| **S2** | Res-3 activation shape | **(a)** modifier scoped to derived sessions; res-3 display + collection unchanged |
| **S3** | Modifier magnitudes at activation | `upgrade_bonus 1` / `contra_penalty 1` unchanged (smallest step; both already tweaker-tunable once live) |
| **S4** | Boundary | Wire-in = its own ⚠ sub-version at the next boundary; **funding time-anchored build lands AFTER it** (handover Q3 / D4 sequencing); one ⚠ at a time |
| **S5** (rider) | `session_volume.enabled` exact-match tweaker fence (**HC22**) — the unfenced feature switch found in the 07-13 re-check (A15g) | Ride the wire-in settings commit (HC16-class, one line + fixture) |

Post-ship watch (named now per rule 6): NY burst fire rate 8–12% band over the first 2 weekday sessions; same-side share ≥85%; §5.2 re-run per session when res-3 samples mature.

**07-13-evening confirmation (6th NY day, partial 12:45–16:43 UTC, n=225):** fires@2.5 = 17.3%, **fires@4.5 = 8.9%** — inside the recommended band out-of-sample; gate stability ρ(Net,TFI) = 0.635 (6th consecutive day < 0.7). Res-3 fires still 36 (Monday Asia/London barely collected — 2 + 15 rows; the collector ran ~04:22 UTC briefly, then from ~12:45). Recommendation unchanged.

## Appendix — reproduction

Population: `ExecResolution==1 ∧ AggrVelBurstRatio ≠ ""` on the frozen corpus. Fire-rate-at-T: `Signal=BURST_* ∧ BurstRatio ≥ T`. Same-side/contra vs `TFISignal`. Percentiles via sort. Columns per the gate-verdict appendix. Full command set in the 2026-07-13 transcript.
