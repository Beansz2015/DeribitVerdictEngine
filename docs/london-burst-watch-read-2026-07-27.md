# LONDON Burst Watch Read — 2026-07-27 (v60 arming, T=5.5) — **PASS**

**Read by:** Fable coordinator seat. **Population:** post-v60 LONDON×3 rows (UTC hours 08–12, `ExecResolution=3`), first 2 weekday LONDON sessions after arming — 2026-07-23 (Thu) + 2026-07-24 (Fri).
**Instrument:** the v52 §12 watch shape applied to LONDON (`aggressor-velocity-s52-derivation-2026-07-13.md` §7 recipe; LONDON values from `aggr-vel-s52-london-derivation-2026-07-23.md` — T=5.5 = p90 knee, predicted ~10% fire, same-side 93.1%). Armed in v60 (`631a3f5`).
**Data source:** the AWS supplementary collector's book — first copy-back `analysis_log_aws.csv`, taken 2026-07-27 08:18 UTC, frozen before stats. **The local book has zero post-v60 LONDON coverage** (local ran short manual sessions only, 07-23→07-25), so the AWS box is this read's sole source — the coverage-bound case it was deployed for. Reading instance: `fb908147-0312-4c55-b9d1-a23be310256e` (the standing collector since 07-22 19:25 UTC; header ≡ local 111-col v0.8; v61 settings confirmed on-box by the absorption episode numerics populating, which tick-geometry v54 could not produce).

## Numbers

| Session | rows (AggrVel non-empty) | bursts | fire rate | same-side (TFI-directional bursts) | TFI-modifier engagement |
|---|---|---|---|---|---|
| 2026-07-23 (Thu) | 100 | 12 | **12.0%** | 11/11 | 11.6% of TFI-directional rows |
| 2026-07-24 (Fri) | 100 | 7 | **7.0%** | 6/6 | 6.2% |
| **Pooled** | **200** | **19** | **9.5%** | **17/17 = 100%** | — |

Live LONDON burstRatio distribution (pooled 23+24, n=200): p50 0.64 · **p90 5.46** · p95 9.78 · max 20.72. Contra-side bursts: 0 (2 bursts carried non-directional TFI and are excluded from the same-side denominator, per the recipe).

## Verdict

**PASS — no trigger.**
- Band: fire 8–12%. Pooled 9.5% is centred; 07-23 sat at the top edge (12.0%), 07-24 one point below the floor (7.0%). The trigger requires **both** consecutive sessions outside the band — not met. At n=100/session one burst = 1pp, so 7 vs 8 bursts is single-count noise.
- Same-side ≥85%: 100% (17/17). Clears with margin; the derivation predicted 93.1%.
- Out-of-sample confirmation of the fit itself: the live distribution's p90 (5.46) lands on the shipped T=5.5 — the p90-knee anchor is holding on unseen data.
- Engagement (11.6% / 6.2% of TFI-directional rows) brackets the ~5–10% NY analogue; no band is defined for LONDON engagement — recorded for reference.

## Follow-ups

- Spot-check LONDON fire/same-side at the funding calm-week re-read and at the W6-1 audit re-run (same cadence as the NY watch's standing spot-checks).
- **ASIA §5.2 remains data-gated** (~150 fires/session; AWS coverage now accrues ASIA rows daily — check at the next copy-back).
- Cross-box discipline surfaced by this read (for F1 / W6-4 pooled stats, not this watch): local and AWS both run on-close at the same bar closes, so overlapping-hours rows are near-duplicate observations — pooled statistical reads need a dedup/preference rule (proposed: local-preferred where both boxes cover a session-hour, AWS fills gaps). To be ruled before the first pooled F1/W6-4 computation.
