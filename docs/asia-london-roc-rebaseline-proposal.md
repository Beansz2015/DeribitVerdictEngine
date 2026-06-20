# Asia/London ROC Re-baseline — `resolution_profiles["3"]` settled with measured 3-min values

**Status: APPROVED (trader, 2026-06-20) — values + schema move signed off. Provisional pending a 3rd weekday session.**
**Workstream (B)** of the v36 session-timeframe split (see `project-v36-session-timeframe`): the manual Asia/London accuracy fix. NOT the auto-tweaker (workstream A, NY×1 only) and NOT Phase-2b automation (C).

---

## 1. Problem

v36 Phase 1 moved Asia/London execution to 3-min and seeded the two timeframe-sensitive ROC keys at **×2.1** (the measured 1→3-min *ATR* ratio, used as a *proxy* for ROC scaling):

```
resolution_profiles["3"] = { roc_magnitude_threshold: 0.21, roc_slope_delta_threshold: 0.105 }   // both ×2.1
```

A single shared 3-min profile applied to *both* Asia and London. Phase-1 Asia/London verdicts were flagged provisional by design; this pass settles them with measured values.

## 2. Data

Live book `analysis_log.csv`, frozen snapshot, **weekday-only**: Thu 2026-06-18 + Fri 2026-06-19 (the v34 weekend confound is gone). Three populations by `(session × ExecResolution)`:

| Population | rows (CSV runs) | closed 3-min bars (slope) |
|---|---|---|
| NY-1m (reference) | 1642 | 316 (1-min, 06-19) |
| ASIA-3m (hr 6–7) | 335 | 80 |
| LONDON-3m (hr 8–12) | 851 | 200 |

## 3. Method — firing-rate matching (not rescaling)

No session runs both resolutions, so resolution-scaling can't be isolated from session-volatility. We therefore **match each 3-min session's firing rate to NY-1m's accepted selectivity**, rather than rescale by a ratio.

- **Magnitude** (`roc_magnitude_threshold`) — gates "ROC active" (`|ROC| ≥ threshold`; partial-scoring + Pass 2c). Measured directly from logged `|ROC|` (CSV col 15). NY-1m fires **53.8%** at the current 0.10. Match each 3-min session to that rate.
- **Slope** (`roc_slope_delta_threshold`) — gates RISING/FALLING vs FLAT (`|ROC[last] − ROC[last−1]|`). The raw delta is **not** logged (only the label), so it was **re-derived** from live Deribit 3-min candles reusing the exact `IndicatorEngine.CalcROCSeries` (throwaway harness, deleted after). **Fidelity validated:** re-derived NY-1m active@0.05 = **46.5%** vs the engine's logged **47.6%** (1.1 pp — the gap is forming-bar intra-interval resampling).

## 4. Findings

| Key | Population | Measured match | Current 3-min `0.21`/`0.105` fires | Decision |
|---|---|---|---|---|
| **Magnitude** | ASIA-3m | **0.20** | 49% (≈ on target) | ≈ unchanged |
| | LONDON-3m | **0.11** | 22% (suppresses ROC on 78% of bars) | **the real fix** |
| **Slope** | ASIA-3m | 0.063 | 26% | shared |
| | LONDON-3m | 0.060 | 25% | shared |

Two conclusions, differing in shape — which is exactly why one shared ×2.1 proxy could not serve both keys:

1. **Magnitude needs per-session.** ROC *level* tracks session volatility: Asia (hr 6–7) runs ~1.8× hotter than London (hr 8–12), whose 3-min `|ROC|` is barely above NY-1m (p50 0.120 vs 0.110). The ×2.1 seed happens to fit Asia (true ratio ~1.9×) but is ~2× too high for London (true ~1.1×). A single `resolution_profiles["3"]` cannot express this.
2. **Slope is ONE shared 3-min value ≈ 0.06.** Bar-to-bar ROC *change* is nearly identical across the two sessions (p50 `|delta|` 0.059 Asia vs 0.054 London) even though the *levels* are 1.8× apart — a second-difference is far less sensitive to the level. Both match NY's 46.5% selectivity at ≈0.06. The current 0.105 is ~2× too high. Note 0.06 is only marginally above the 1-min 0.05 — slope barely scales with resolution.

## 5. Change

**Per-session magnitude on the session bucket; shared slope on the resolution profile.**

```jsonc
// settings.json
resolution_profiles["3"].roc_slope_delta_threshold:  0.105 → 0.06     // shared (both sessions)
resolution_profiles["3"].roc_magnitude_threshold:    0.21  (kept as the 3-min fallback for any
                                                            3-min session lacking a bucket override)
session_volume.sessions[ASIA].roc_magnitude_threshold:   + 0.20        // ≈ unchanged from 0.21
session_volume.sessions[LONDON].roc_magnitude_threshold: + 0.11        // halves the over-suppression
// NY: no override → inherits the base 0.10 (correct for 1-min). Byte-identical.
```

**Code (the per-session magnitude routing — the only code in this pass):**

- `SessionBucketSettings` POCO: add nullable `<JsonPropertyName("roc_magnitude_threshold")> RocMagnitudeThreshold As Double?` (absent ⇒ inherit, mirroring `ResolutionProfile`). Default buckets leave it `Nothing` (silent-defaults path stays 1-min/base, consistent with `execution_resolution`).
- `ExecutionResolution`: add `ResolveRocMagnitudeForHour(cfg, utcHour)` — bucket override first, else `ResolveRocMagnitude(cfg, ResolveResolution(cfg, utcHour))` (the existing resolution_profiles→base fallback). The 2-arg `ResolveRocMagnitude(cfg, execRes)` is **unchanged** (still the profile-map lookup; harness A14b/A14d untouched).
- `IndicatorResults`: add `RocMagnitudeThreshold As Double = 0` (0 ⇒ inherit). `MainForm_Analysis` stamps it from the run's UTC hour alongside `r.ExecResolution`.
- `ScoringEngine_Calculate_Scoring.vb`: the 3 magnitude read sites go through a local `EffRocMag(r, cfg)` = `If(r.RocMagnitudeThreshold > 0, r.RocMagnitudeThreshold, ResolveRocMagnitude(cfg, r.ExecResolution))`. The fallback makes every existing harness fixture (which leaves the field 0) behave **identically** to today — zero fixture churn.

**Slope** needs no code — `ResolveRocSlopeDelta` already reads `resolution_profiles["3"]`; only the value changes.

## 6. Caveats (provisional)

- **2 weekday days only** (Thu+Fri). Hard gate (≥50 weekday-3min/session) met comfortably; lock-confidence is thin. Re-measure after Monday 06-22 adds a 3rd weekday session.
- Magnitude is on CSV runs (n 335/851); slope on closed bars (n 80/200) — thinner.
- Confound: can't isolate resolution from session-vol (no session runs both) → firing-rate-*matched*, not rescaled.
- London magnitude could tighten if restricted to the trader's real hr 8–10 (this pass used full hr 8–12).
- `execution_resolution` and `resolution_profiles.*` stay OFF the auto-tweaker surface (PromptBuilder HARD CONSTRAINT 11); the new `session_volume.sessions[].roc_magnitude_threshold` is a `session_volume` key — confirm it is covered by the same exclusion (manual-rebaseline-only) at implementation.

## 7. Acceptance

- `dotnet build` 0/0 on solution + AutoTweaker + OrderCheck.
- Harness A1–A15h unregressed + new **A14e** (`ResolveRocMagnitudeForHour`: ASIA hour→0.20, LONDON hour→0.11, NY hour→0.10).
- **NY byte-identical**: NY hour → no bucket override → base 0.10; `r.RocMagnitudeThreshold` default-0 fallback path proves the harness scoring is unchanged.
- **Display parity**: the ROC magnitude/slope thresholds are not rendered on any card or `BuildPlaintextSnapshot` line → no card-binding obligation (hard rule satisfied; stated in the commit).
- settings v39 → v40 + `change_log` entry; `DeribitIndicatorProject.md` §15 row + §6 pointer.

Local-first — coordinator commits locally; the trader tests + pushes.
