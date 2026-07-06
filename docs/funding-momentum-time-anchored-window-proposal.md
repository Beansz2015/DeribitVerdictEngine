# Funding Momentum — Time-Anchored Window (mini-spec)

**Status:** PROPOSED 2026-07-06 (Fable seat). Sign-off decisions in §8.
**Trigger:** the retune §5 post-ship watch (first pass, 2026-07-06) — see §1. This is the "time-anchored window" code fix the signal-health audit deferred (`signal-health-audit-2026-07-03.md` §7; `signal-health-retune-proposal.md` §7). The deferral trigger has fired: the cadence-dependence is no longer a corner case, it is the operating mode.
**Class:** ⚠ scoring-behaviour change (Step 3b input state becomes cadence-independent). Boundary rules apply (§5).

---

## 1. Watch finding (2026-07-06, first 2 weekday sessions post-v50)

Population: the full v0.8 book, 314 rows (07-03 15:57 → 07-06 10:24 UTC; the collector runs `trigger_mode=on_close`).

| Session | Cadence | n | FLAT% | 3b-engaged% | per-step \|ΔF\| p50 |
|---|---|---|---|---|---|
| Fri 07-03 NY (res 1) | 60 s on-close | 290 | **52.1%** | **27.6%** | 5.0e-8 |
| Mon 07-06 LONDON (res 3) | 180 s on-close | 24 | **0.0%** | **95.8%** | 6.5e-7 |
| Target (retune §5) | — | — | 60–70% | 15–25% | — |

R1 (OFIMomentum retire) passes its render check — the OFI row shows `MOM:state` with no modifier suffix. R2's 2e-7 threshold **restores the intended profile only at the cadence it was derived on** (the ~30 s-interval audit book):

- The momentum window is **3 funding *changes***, and on WS funding changes nearly every run — so the window's wall-clock span ≈ 3 × run cadence. Derivation cadence ~30 s → ~90 s span. On-close NY (60 s) → ~3 min span → window deltas ~2× the derivation distribution → FLAT 52% vs the derived 67.6%.
- On-close 3-min sessions → ~9 min span; a **single** 3-min step (p50 6.5e-7) already exceeds the whole-window threshold (2e-7) — momentum is **structurally always-active** on Asia/London. Step 3b moved scores on 95.8% of today's London rows. The adjunct invariant R2 was shipped to restore is violated again, by arithmetic, on every 3-min session.

No per-cadence threshold fixes this (the backstop timer, gaps, and session hand-offs change effective cadence within a session). The window must be anchored in time, not samples.

## 2. Current mechanism (for reference)

`MainForm_Analysis` appends `fundingRate` to `_fundingHistory` **only on value change** ([S9] dedup), ring max 10. `CalcFundingMomentum` takes `delta = last − history[count−1−MomentumWindow]` and classifies against `MomentumThreshold` (`Indicators_OrderFlow.vb:409`). `r.FundingDelta` = last ring step.

## 3. Proposed mechanism

Replace the count-indexed ring with a **timestamped ring**; classify on the delta vs the sample **≥ W minutes back**.

- Ring: `List(Of (UtcMs As Long, Rate As Double))`, appended **every completed run** (no dedup — anchoring is by age, so identical consecutive samples are harmless and informative), evicted when older than 30 min (the audit's segment-reset horizon; ≤ 60 entries at the fastest historical cadence).
- Classification: anchor = the **newest** sample with age ≥ `momentum_window_minutes` (W). `delta = current − anchor.Rate`; `> T` → RISING, `< −T` → FALLING, else FLAT.
- Cold start / post-gap: no sample ≥ W old (fresh start, or the 30-min eviction just cleared the ring) → **FLAT** — same warm-up posture as today.
- The pure function moves to the host-agnostic pattern: `CalcFundingMomentum(history As List(Of (Long, Double)), nowUtcMs, cfg)` — stays in `IndicatorEngine`, ring stays host-side (and is named in the W4 run-state extraction inventory; the two specs compose).
- **W = 5 minutes (proposed):** ≥ 1 full bar at every execution resolution (1 m and 3 m), spans ≥ 2 samples at every cadence the engine has ever run, and sits at the front edge of the 2–15 min hold horizon — "is crowding building *now*, at trade-decision timescale."
- Step 3b itself is untouched — same amplify/soften, same crowding gate. Only the momentum state's input window changes meaning: **funding moved more than T per ≥ W minutes**, identical at every cadence.

## 4. Threshold derivation — **T = 2e-7 at W = 5 min** (pooled 9-day fit, 2026-07-06)

Method: firing-rate-match, same as R2 — fit T so the profile lands in the retune §5 bands (FLAT 60–70%, 3b engagement 15–25%). Two fit populations, both run 2026-07-06:

1. **v0.8 book** (exact F8 funding path; n=305 anchored windows, 07-03→07-06): a HOT-funding stretch — \|Δ5min\| p50 3.9e-7; in-band T would be ~5–6e-7 here alone.
2. **`.bak` WS-era book** (06-25→07-03, n=1,689 windows): funding path **reconstructed from the `FundingDelta` (F8) change-step stream** (the audit §7 method — F6 `FundingRate` seeds each >30-min segment, steps accumulate when the delta value or the F6 rate moves; ~95%-fidelity class). A QUIET-funding period — \|Δ5min\| p50 3.0e-8, 13× smaller than the v0.8 stretch.

The 5-min funding-move distribution is **regime-dependent week to week** (quiet weeks vs crowding builds). A single T therefore holds the band only on a multi-regime book — and once the window is time-anchored, above-band engagement during a genuine crowding build is *honest signal*, not the cadence artifact R2 chased. Pooled fit (n=1,994 windows, ~9 days, both regimes):

| T | pooled FLAT% | pooled 3b-engaged% |
|---|---|---|
| 1.5e-7 | 65.1 | 18.2 |
| **2e-7 (recommended)** | **68.7** | **17.1** |
| 2.5e-7 | 72.4 | 15.4 |
| 3e-7 | 75.7 | 13.9 |
| 5e-7 | 81.9 | 10.9 |

**T = 2e-7** sits mid-band on both metrics — numerically the same value R2 shipped, now attached to a cadence-independent construction (coincidence of scale, not of meaning: the count-window at the 30s derivation cadence spanned ~90s; the anchored window spans 5–6 min at every cadence).

**Caveats, honestly stated:**
- The `.bak` segment rides the reconstruction (~95% fidelity; F6 seeds; possible slight undercount of movement → FLAT% marginally overstated there). The v0.8 segment is exact.
- Expect engagement **above** band on hot-funding weeks and **below** on quiet weeks (10–34% across the two segments) — by design; the bands are calibration targets for the *average* book, and the bounded ±amplify/soften modifier caps the verdict impact either way.
- The original "re-fit on ≥5 weekday sessions" gate is **substantially discharged** by the pooled fit; what remains is the standard post-ship §5-style watch re-run (per-resolution, §7) — no further pre-ship data gate.

## 5. Sequencing (boundary discipline)

⚠ scoring-behaviour change → must not land mid-window. The #5 collection window is OPEN; B4b (placed-geometry) lands as its own pre-agreed boundary (its D7). Options, trader's call at D4: bundle at the B4b boundary (R1/R2-at-#5 precedent — one boundary, one reset), or own boundary after the #5 §5.1 gate clears (~Jul 8–9). **Until it lands, read Step 3b on 3-min sessions as always-engaged** — anyone consuming the current book for calibration should treat `FundingMomentum` on res-3 rows as uninformative (the #5 correlation gate is unaffected — burst columns don't touch funding; verdict contamination is bounded by the ±amplify/soften modifier on crowded rows only).

## 6. Surface / parity / CSV

- **Settings** (`indicators.funding`): `momentum_window` (count) → retired; new `momentum_window_minutes` (5). `momentum_threshold` re-derived at ship per §4. Version bump + change_log. POCO defaults ride the commit.
- **Tweaker surface:** `momentum_threshold`/`amplify`/`soften` stay ON (unchanged); `momentum_window_minutes` ON, matching the `OFI.avg_window_sec` precedent (it shapes the signal), with the same window↔threshold coupling caveat recorded; `momentum_enabled` status unchanged.
- **Display:** FUNDING momentum row renders the same three states, same format — no line added/removed/renamed → no card/snapshot/payload change (parity rule satisfied; state *values* will differ on 3-min sessions, which is the point).
- **CSV:** `FundingMomentum` column semantics shift (cadence-independent states) — documented, **no header rotation** (v31/v36 rule). `FundingDelta` (D3): with the dedup gone, `last − prev` becomes the per-run step (0 on unchanged runs) — recommend **keep the column as the per-run step** and document; the audit's reconstruction method survives (it segments on resets and sums steps).

## 7. Acceptance

- 3 Release builds 0/0; `verify-gate.ps1 -Mode prepush` green; A-series unregressed.
- New fixtures: anchored classification (RISING/FALLING/FLAT at synthetic timestamped rings); cold-start + post-eviction → FLAT; anchor picks the *newest* ≥W sample (not the oldest); 30-min eviction; cadence-invariance (same funding path sampled at 30 s vs 180 s → same states at the same wall-clock instants — the fixture that pins the whole point).
- Post-ship watch: same §5 bands, re-checked per-resolution — **both res-1 and res-3 must sit in-band**, that's the success criterion the count-window could never meet.

## 8. Sign-off decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | Adopt the time-anchored window (mechanism, §3) | **Yes** — pre-agreed direction (audit §7), trigger fired |
| D2 | W = 5 minutes | **Yes** (rationale §3; T is fit *given* W, so W is a design choice, not a calibration) |
| D3 | `FundingDelta` column = per-run step (dedup retired) | **Keep column, document semantics** (§6) |
| D4 | Boundary: bundle at B4b vs own boundary post-#5-gate | **Own boundary post-#5-gate** — B4b is geometry-only and already reviewed as such; keep its diff clean. Bundle only if B4b slips past the #5 gate anyway |
| D5 | T = **2e-7** at W = 5 min (pooled 9-day / 2-regime fit, n=1,994 — §4 as amended 2026-07-06; the pre-ship re-fit gate is discharged, the post-ship per-resolution watch is the confirmation) | **Yes** |
