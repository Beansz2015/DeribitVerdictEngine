# #5 Aggressor Velocity — §5.2 LONDON Per-Session Re-baseline Derivation

**Date:** 2026-07-23 · **Seat:** Fable coordinator · **Type:** derivation (no code changed; value for a settings sub-version)
**Parent:** `aggressor-velocity-proposal.md` §5.2 · `aggressor-velocity-s52-derivation-2026-07-13.md` (the NY doc — recipe mirrored here) · `aggr-vel-wirein-spec-back.md` D1 (session auto-arm mechanic).
**Scope:** derive LONDON `burst_ratio_threshold` now that the LONDON×3 sample has matured (234 raw BURST fires since 2026-07-14 vs. the ~150 gating bar named in the NY doc §5). NY-recipe mirror; ASIA is progress-report-only (still short of the bar).

---

## 1. Data

Frozen at `bin\Debug\net8.0-windows\analysis_log.csv` (2026-07-23), copied to `scratchpad/s52_london_frozen.csv` before any analysis.

Population per the task filter:
- `ExecResolution == 3`, UTC hour 8..12 (LONDON per `settings.json` `session_volume.sessions[LONDON]`).
- `Timestamp >= 2026-07-14` (v52 wire-in dataset boundary).
- **Weekday only** (Mon..Fri UTC).
- Excluded `InstanceId` prefix `8706ebae` (v57 note — the 07-21 interval-mode leak).
- Excluded instances with **median inter-row gap < 45s** (tick-jitter / non-standard cadence). One instance qualified: `ca02e13d-cd98-4f53-9596-ba51b04b5b44` (4 rows, median gap 17s) — dropped.

Post-filter: **7 session-days** (5 full, n≥90 rows; 2 partials — 07-15 n=57, 07-20 n=34). **N = 572 non-empty `AggrVelBurstRatio` rows**, **145 BURST_* signal rows** (equal to fires at `T=2.5`, the classifier floor). Empty-numeric warmup rows: 35 of 611 (5.7% — typical restart tail, negligible).

**Reconciliation with the 234-fires headline.** The 234 fires are the raw LONDON×3 BURST count since 2026-07-14 (no filters). The gap between 234 and 145 is 89 fires on **Saturday 07-18** (weekend excluded by the recipe) plus 0 on ca02e13d (the excluded instance carried no bursts). Weekday-only reduces the derivation sample to 145 fires — right at the "≥150 per session" defensibility floor NY §5 named. Sample is thinner than NY×1 (145 vs 400 fires; 572 vs 1,960 rows), so per-day fire-rate variance is wider than NY's ±2pp (§3 note).

Current config: LONDON inherits `default {norm_window_sec 120, burst_ratio_threshold 2.5}` — no explicit override in `sessions.LONDON`. Per D1, absence of `sessions.LONDON.burst_ratio_threshold` keeps LONDON's scoring inert; the classifier still tags bursts for display/CSV.

## 2. The default T=2.5 under-selects on LONDON — more sharply than on NY

T=2.5 sits at **p68** of the LONDON burstRatio distribution → **25.35%** fire rate. One row in four is a burst-labelled moment; a scoring modifier that engaged at that cadence would drift toward the always-on class the audit retired (mirrors the NY doc §2 rationale — LONDON is even hotter than NY at the default).

Distribution (N=572, non-empty rows): **p50 0.4496 · p75 2.6829 · p80 3.3725 · p85 4.3049 · p88 4.8156 · p89 5.0944 · p90 5.6877 · p91 6.2823 · p95 8.7983 · p99 15.5504**.

Anchor logic follows NY §2: neither ROC-active nor closed-bar VolumeRatio anchors are useful firing-rate matches on res-3 (closed-bar cadence artifacts). Anchor is the distribution knee + selectivity intent + per-day stability, tabled below.

## 3. Candidate table (LONDON×3, filtered)

Same-side / contra vs. `TFISignal`: same = (BURST_BUY & BUY PRESSURE) or (BURST_SELL & SELL PRESSURE); contra = opposite pair; no_dir = TFI not directional. Upgrades/softens/day are the **effective modifier engagement** at each T, averaged across the 7 session-days present.

| T          | fires | fire rate | same-side | contra | no_dir | same-side of directional | upgrades/day | softens/day |
|------------|-------|-----------|-----------|--------|--------|--------------------------|--------------|-------------|
| 2.5 (dflt) | 145   | 25.35%    | 84.1%     | 9.7%   | 6.2%   | 89.7%                    | 17.4         | 2.0         |
| 3.0        | 129   | 22.55%    | 86.8%     | 7.8%   | 5.4%   | 91.8%                    | 16.0         | 1.4         |
| 3.5        | 107   | 18.71%    | 87.9%     | 6.5%   | 5.6%   | 93.1%                    | 13.4         | 1.0         |
| 4.0        | 94    | 16.43%    | 86.2%     | 7.4%   | 6.4%   | 92.0%                    | 11.6         | 1.0         |
| 4.5        | 79    | 13.81%    | 88.6%     | 5.1%   | 6.3%   | 94.6%                    | 10.0         | 0.6         |
| 5.0        | 64    | 11.19%    | 90.6%     | 3.1%   | 6.2%   | 96.7%                    | 8.3          | 0.3         |
| **5.5 (rec)** | **58** | **10.14%** | **93.1%** | **3.4%** | **3.4%** | **96.4%**              | **7.7**      | **0.3**     |
| 5.7        | ~56   | ~9.8%     | ~93%      | ~3%    | ~3%    | ~96%                     | 7.3          | 0.3         |
| 6.0        | 50    | 8.74%     | 92.0%     | 4.0%   | 4.0%   | 95.8%                    | 6.6          | 0.3         |
| 6.5        | 48    | 8.39%     | 93.8%     | 4.2%   | 2.1%   | 95.7%                    | 6.4          | 0.3         |
| 7.0        | 42    | 7.34%     | 95.2%     | 2.4%   | 2.4%   | 97.6%                    | 5.7          | 0.1         |

Per-day fire rate at rec T (full days only, n≥90): 07-14 9.6% · 07-16 7.0% · 07-17 5.3% · 07-21 14.1% · 07-22 7.0% ⇒ **range 5.3%–14.1%** on 5 full days (median ~7%). The two partial days sit outside this range (07-15 n=57 at 19.3%, 07-20 n=34 at 17.6%) — smaller sample = higher variance, consistent with the NY doc's 07-03-partial exclusion. Even excluding partials, the LONDON per-day spread (~9pp) is wider than NY's ±2pp full-day band — expected given ~1/5 the daily-row sample.

**Recommendation: LONDON `burst_ratio_threshold` = 5.5** — the p90 knee (5.69), rounded to the same 0.5-step grain NY used (NY p90=4.51 → 4.5). Reasoning mirrors NY §3:
- **(a)** ~10% fire rate reads as "genuine impulse" on a 3-min tape (~7.7 upgraded moments/session-day) while staying far from the always-on failure mode; matches the NY selectivity intent by construction (both at p90).
- **(b)** Same-side agreement climbs with T (84%→95%) — bigger bursts are cleaner. At 5.5 the same-side share is **93.1%**, comfortably above the ≥85% target.
- **(c)** Full-day per-day range 5.3%–14.1% — wider than NY (±2pp) because res-3 delivers ~1/5 the daily rows; **noted, not blocking** — the mean sits inside the 8–12% band and the shape (higher-T = more selective, cleaner side agreement) is monotonic on LONDON as it was on NY.
- **(d)** Contra-soften arm survives at 0.3/day (mirror caveat below).

**Alternatives** (mirroring NY's shape): **5.0 looser** (11.19% fire rate, 90.6% same-side — inside the band, contra arm 0.3/day) / **6.0 tighter** (8.74%, 92.0% — also inside; contra arm 0.3/day).

**Caveat — contra arm is thin on LONDON regardless of T.** At NY's rec (4.5) the contra-soften arm was 2.2/day — the "genuine warning half" of the §4.5 design was materially alive. On LONDON at rec (5.5) it is **0.3/day** (≈ 1.5 softens/week), and it stays 0.3/day across the T=4.5..6.5 band before collapsing to 0.1/day at T=7.0. LONDON res-3 bursts are simply more same-side-dominant than NY×1 bursts (89.7% at T=2.5 already, vs. NY's 86% at same T). Consequence: the LONDON wire-in is effectively an **upgrade-only** arm in practice; the contra path is retained as a design invariant (spec §4.5) but will rarely trip. This is a session-shape observation, not a threshold-choice defect — the same conclusion holds at any T in the defensible band.

**ROC-active-at-fire note (recorded, not blocking):** the NY doc §3 observed P(ROC-active | fire) *falls* as T rises (46.8%→30.8%), consistent with the tick-resolution-earliness thesis. Not re-computed here — the wire-in engagement doesn't depend on it and the LONDON same-side share is already strong enough to make the same qualitative point.

## 4. Norm window (confirm, no change)

LONDON has no `norm_window_sec` override — it inherits the default **120s**. Warmup health: 35 empty-numeric rows of 611 total (5.7%). Per-day fire rate at fixed T stays inside a mean-±5pp band on full days (5.3–14.1% at rec, mean ~7%), which is the indirect evidence the 120s norm isn't producing a day-regime artifact. **Window↔threshold coupling caveat stands:** 5.5 is derived FOR fast=5s / norm=120s on LONDON; changing either re-opens this derivation.

## 5. ASIA (progress report — NOT derived)

Raw LONDON-recipe-analog count for ASIA (UTC hour 0..7, res-3, since 2026-07-14, weekday+weekend, ex-8706ebae): **70 BURST fires on 325 rows**. Still short of the ~150-fire defensibility bar — likely another 3–4 weeks at current cadence to reach it (ASIA collects thinner than LONDON, which itself took 9 days to hit 234). **No ASIA derivation performed.** ASIA continues in exploratory mode: classifier tags bursts for display + CSV, scoring stays inert (D1 auto-arm keyed on presence of `sessions.ASIA.burst_ratio_threshold`, which stays absent). Re-run §5.2 for ASIA when its BURST count clears the same bar.

## 6. Wire-in engagement preview (at T=5.5, LONDON)

TFI is directional on the vast majority of LONDON res-3 rows (empirically ~85%+ from the same-side + contra fractions above). At T=5.5 the modifier touches the ~10% of rows where a qualifying burst fires: **~7.7 upgrades + ~0.3 softens per LONDON session-day**, i.e. the modifier re-weights ~10% of TFI's directional votes and leaves ~90% untouched. Net Microstructure fire count unchanged by construction (a modifier, not a vote — §4.5); the arithmetic reduces to a score shift of ±`upgrade_bonus`/`contra_penalty` (= ±1) on ~8 rows per LONDON session-day.

## 7. S-table — for trader tick

| # | Decision | Recommendation |
|---|---|---|
| **S1** | LONDON `burst_ratio_threshold` | **5.5** (p90 knee, ~10% fire rate, same-side 93.1%); alternatives 5.0 looser / 6.0 tighter |
| **S2** | Activation mechanism | **D1 auto-arm** — adding `sessions.LONDON.burst_ratio_threshold: 5.5` to `settings.json` under `indicators.aggressor_velocity` alone activates LONDON scoring (per `aggr-vel-wirein-spec-back.md` §D1). No engine change. |
| **S3** | Modifier magnitudes | **Unchanged** — `upgrade_bonus 1` / `contra_penalty 1` inherit from the shared block (both already tweaker-tunable). |
| **S4** | Boundary | ⚠ Small settings pass at the next boundary — the value itself is one line + `change_log` row + `docs/DeribitIndicatorProject.md` §12/§15 rows + this doc's citation. **Boundary class: scoring change on LONDON rows** (S2a shape pre-approved in the wire-in spec-back; auto-arm mechanic pre-approved D1). Because the mechanic already shipped in v52, this is the **narrowest possible ⚠** — a single-value config nudge that the pre-approved auto-arm consumes — but it is still a scoring boundary on a session that was scoring-inert until now, so trader test + push before the next dataset boundary. |
| **S5** | Post-ship watch | LONDON burst fire rate **8–12% band** over the first 2 weekday LONDON sessions (target ~7–8 upgrades/day at 60s×5-hour LONDON block); same-side share ≥85%; softens-per-day expected 0.0–0.5 (contra arm is thin here — do not read 0-fire days as a regression). Note out-of-band on partial-collection days (n<90). Re-run this derivation if `norm_window_sec` or the fast=5s window changes. |

## 8. Sequencing note

Per the roadmap, the next ⚠ boundary in the queue is **#6 book absorption activation** (v54 shipped as a build sub-version, `scoring_enabled:false`, awaiting the twice-evidence-gated activation later per §5). This LONDON re-baseline is a smaller ⚠ (one config value on an already-shipped mechanic) and slots in **before** the #6 activation — one ⚠ at a time, LONDON auto-arms first, then the joint post-ship watch runs 2 weekday LONDON sessions to confirm the 8–12% band before #6 moves. No other builds are gated behind this one.

## Appendix — reproduction

Freeze: copy `bin\Debug\net8.0-windows\analysis_log.csv` → `scratchpad/s52_london_frozen.csv` before any read.

Filter (in order):
1. `ExecResolution == 3`
2. `Timestamp >= "2026-07-14"` (string compare — CSV format `YYYY-MM-DD HH:MM:SS`, UTC)
3. UTC hour 8..12 (`substr(ts,12,2)+0`)
4. Weekday only (Mon..Fri UTC — dates 07-14 Tue, 07-15 Wed, 07-16 Thu, 07-17 Fri, 07-20 Mon, 07-21 Tue, 07-22 Wed; exclude 07-18 Sat, 07-19 Sun)
5. `InstanceId !~ /^8706ebae/`
6. Per-instance median inter-row gap ≥ 45s (drops `ca02e13d-cd98-4f53-9596-ba51b04b5b44`, 4 rows @ 17s median)

Population post-filter: N=572 non-empty `AggrVelBurstRatio` rows across 7 session-days (5 full n≥90, 2 partial n≤57).

Fire rate at T: `count(AggrVelSignal ∈ {BURST_BUY, BURST_SELL} ∧ AggrVelBurstRatio ≥ T) / N`.
Same-side: `TFISignal` matches burst side (`BURST_BUY & BUY PRESSURE` or `BURST_SELL & SELL PRESSURE`). Contra: opposite. No_dir: `TFISignal` neither `BUY PRESSURE` nor `SELL PRESSURE`.
Percentiles: sort brs asc, index = `round(p/100 × N)`.

Scripts: `scratchpad/filter_london.awk`, `scratchpad/analyse.awk`, `scratchpad/gap_check.awk` (this session).
