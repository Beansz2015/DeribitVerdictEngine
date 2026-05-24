# VPFR Bucket Exposure + VolumeHistogramMini — Spec-Back Report

**Author:** Claude (Opus 4.7, implementation conversation)
**Date:** 2026-05-24
**Parent spec:** `docs/vpfr-buckets-histogram-proposal.md`
**Kickoff doc:** `docs/vpfr-buckets-histogram-kickoff.md`
**Commits:** `ae61be8` → `b77703d` (3 commits, all local-only, none pushed)

Findings to pass back to the spec conversation, plus a pre-existing bug surfaced during sanity verification.

---

## 1. Executive summary

Spec B shipped clean in **`ae61be8`** — exactly the four items in kickoff §2 (engine signature + DTO + call site + UI binding) plus the row-7 height bump in §3.4. Build clean. Bucket-reversal math worked first try; POC bar position and current-price line both verified against the existing VOLUME PROFILE level-stack values.

Two anticipated follow-ups landed on top of Spec B:

- **`94bb78a`** — UI-side downsample to 8 visual buckets. Kickoff §4 / §5 explicitly anticipated this ("If 50 bars at the chosen height read as a noisy block on the verification screenshot, surface back to the spec author as a follow-up rather than tweaking inline"). Live verification confirmed the venetian-blind look at 50 raw buckets in 90 px — `VolumeHistogramMini.OnPaint`'s `barH = usableH/n − 1` clamps to 1 px at that density, and the control's own header comment specs "8-bar".
- **`b77703d`** — Visual buckets 8 → 16. User feedback on the 8-bar render: bars too wide. 16 bars splits the difference (~4.5 px per bar in 90 px, each visual bucket aggregates ~3 raw engine buckets at default `cfg.Indicators.VPFR.NumBuckets = 50`).

**One pre-existing bug surfaced** during the final sanity check — a 1-point delta between the SIGNAL BREAKDOWN per-row SC column and the TOTAL row. Confirmed on three runs by the user and independently by my count. Details in §5. Out of Spec B scope; flagged for a dedicated investigation spec.

---

## 2. Spec ↔ reality mismatches

### 2.1 None for the engine / DTO / call-site surface

The kickoff §2.1 trap (`Dim bucketSize` shadowing the new `ByRef bucketSize` parameter) was real — the warning in kickoff §7.1 was exactly correct. Used the `bucketSizeOut` / `bucketPriceLowOut` / `bucketVolumesOut` naming pattern verbatim. No silent uninitialised-output bug.

### 2.2 Width — kickoff §3.3 option 1 sufficient

Fixed `Size = New Size(500, 90)` rendered correctly at form width 1100 px. No need for §3.3 option 2 (anchor-based) or option 3 (Dock-Top panel wrapper). Spec author can promote option 1 from "recommended first" to canonical for this card geometry.

### 2.3 Row 7 height — 320 px held

Kickoff §3.4 estimated 320 px would fit with a 12 px safety margin. Lived up to the estimate. No clipping. No re-verification bump needed.

### 2.4 Histogram bucket count — kickoff anticipated, but downsample shape differed

Kickoff §4 skip list said the follow-up would be "~10 LOC (aggregate groups of `Ceiling(n/8)` buckets, recompute PocIndex)" with `n/8` aggregation factor. Reality used a different parameterisation:

- Implementation uses **fixed `VISUAL_BUCKETS` constant** (initially 8, then 16 after user feedback) and computes group boundaries as `floor(i × n / VISUAL_BUCKETS)` rather than `ceil(n / 8)`.
- POC index = **highest aggregated group**, not the group containing the engine POC bucket. This guarantees the amber bar is always the longest visually, which matches user intuition. The two converge in practice because the engine POC's group usually IS the highest aggregated, but using "highest aggregated" is more defensive against edge cases where the POC bucket sits adjacent to a denser group.

Both differences were judgment calls during implementation. Either approach works; the fixed-constant approach is easier to tune (`VISUAL_BUCKETS = 8 → 16` was a single-line change for the bar-thickness adjustment). Final LoC was ~25 vs the kickoff's ~10 estimate — slightly larger but still bounded.

---

## 3. Decisions surfaced and resolved

### 3.1 Bucket count tuning — 8 vs 16

Two iterations:

1. **`94bb78a`** shipped 8 visual bars per the kickoff's explicit hint. Bars rendered ~10 px tall each — readable but coarse. Each visual bucket aggregated ~6 raw engine buckets ≈ ~115 points wide at the ATR active that session.
2. User feedback: "bars are too wide. Can you adjust to 16?" — **`b77703d`** bumped `VISUAL_BUCKETS` 8 → 16. Bars now ~4.5 px tall, each visual bucket ~3 raw engine buckets ≈ ~57 points wide.

**Lesson for the spec author.** The kickoff hinted "~10 LOC" for the downsample but didn't pin a target visual count. For future card specs that consume `VolumeHistogramMini` (or any density-sensitive control), recommend pinning the visual-bucket target up-front based on the typical price range of the underlying instrument and the desired resolution. For BTC-PERPETUAL with ATR-scale 0.7-1.5×, 16 visual buckets at ~50-80 points each matches the trader's intra-session swing resolution.

### 3.2 POC = highest aggregated group vs POC = group containing engine POC

Implementation picked **highest aggregated group**. Reasoning:

- The amber bar always being the longest is the most natural visual ("POC = peak").
- In practice the two approaches converge because aggregating raw buckets that include the engine POC almost always produces the highest aggregate.
- The alternative (mark the group containing engine POC even if a neighbour aggregated higher) would produce occasional visual ambiguity where the longest bar is not the amber one.

If the spec author prefers the alternative semantics ("POC bar = the exact price area of greatest single-bucket activity, post-aggregation"), the change is two lines. No strong preference from this conversation.

---

## 4. Polish items the spec didn't anticipate

None outside the kickoff-anticipated downsample work. Spec B's scope held cleanly.

---

## 5. Bugs surfaced — SC column row sum vs TOTAL row delta

**Severity: medium. Pre-existing, not introduced by Spec B work. Flagged because the user caught it independently during the final sanity check.**

### 5.1 Symptom

The SIGNAL BREAKDOWN table's per-row SC column does not arithmetically sum to the TOTAL row's `Long N/M | Short N/M` values. Observed on three separate runs during P4-era verification:

| Run | Regime | Per-row SC sum (my count) | TOTAL row display | Delta |
|---|---|---|---|---|
| #1 (2026-05-24, POC=76864, Trending Up) | TRENDING_UP | Long 4, Short 5 | Long 4 / Short 4 | Short +1 over-counted by my hand-tally |
| #2 (2026-05-24, POC=76396, weak short tied) | TRENDING_DOWN | Long 4, Short 5 | Long 4 / Short 4 | Short +1 |
| #3 (2026-05-24, POC=76396, weak short bias) | TRENDING_DOWN | Long 3, Short 7 | Long 3 / Short 6 | Short +1 |

**Consistent off-by-one on the short side, all three TRENDING regime runs. Long-side count matched TOTAL in every run.**

### 5.2 Likely culprit — Trend Str row SC accounting

Comparing the Trend Str row across the runs:

- Run #1 (tied scores): `Trend Str DOWN LH/LL` SC = 0
- Run #2 (tied scores): `Trend Str DOWN LH/LL` SC = 0
- Run #3 (Short > Long, Pass 2c hint): `Trend Str DOWN LH/LL` SC = **−1**

Same state (`DOWN LH/LL`), different SC values. The row's SC appears to encode Pass 2c structure-bonus information conditionally, not a pure Step 2 vote. If the row's SC reflects "what Trend Str would contribute via Pass 2c structure bonus if Pass 2c isn't suppressed" but the TOTAL row only counts actual Step 2 votes (with Pass 2c added separately as the footer aggregate), the SC column overstates by one when:

- Regime is TRENDING (not TRANSITIONAL)
- Structure agrees with dominant side (DOWN + Short bias, or UP + Long bias)
- Pass 2c isn't otherwise contributing through the alignment path

The "Pass 2c SUPPRESSED +1 regime" footer hint visible in Run #3 also points at Pass 2c being the load-bearing piece — the engine evaluated structure alignment, decided to suppress (for a reason that isn't `Long == Short` since they aren't equal here), but still emitted the row SC as if it had voted.

### 5.3 What would close this

A separate bug-investigation spec, scoped to:

1. Read `BindCardSignalBreakdown` / `BuildBreakdownFooter` in `MainForm_Render_Cards.vb` to confirm where the Trend Str row's SC is computed and what it reflects.
2. Cross-check against `RunScoringPipeline` Pass 2c logic in `Core/ScoringEngine_Calculate_Scoring.vb` to confirm what's actually added to `ScoreState`.
3. Decide one of:
   - **(a)** Trend Str row SC should be 0 in Step 2 (it's a Pass 2c-only contributor, not a Step 2 voter) → fix the row render.
   - **(b)** Trend Str row SC should be the actual contribution that lands in the TOTAL → re-verify the TOTAL calculation includes Pass 2c structure bonus.
   - **(c)** The SC column should explicitly mean "post-Pass 2c contribution" and the TOTAL should match that meaning.

The fix is ~10 lines depending on which interpretation wins. Out of Spec B scope; surfacing for a fresh spec.

### 5.4 Why this is pre-existing

The render path that produces the SC column lives in `MainForm_Render_Cards.vb` and the breakdown notes come from `Core/ScoringEngine_Calculate_Scoring.vb` — neither touched by Spec B or any of the recent UI-reskin commits. The discrepancy would have existed in P4d and earlier. It's surfaced now because the implementation conversation hand-tallied the column for the final sanity check (a step previous spec-back loops didn't perform explicitly).

---

## 6. Commit ledger

| # | SHA | Subject | Files | LoC |
|---|---|---|---|---|
| 1 | `ae61be8` | `feat(ui-reskin): VPFR bucket exposure + VolumeHistogramMini wiring` | `Core/Indicators_Structure.vb`, `Core/IndicatorResults.vb`, `UI/MainForm_Analysis.vb`, `UI/MainForm_Layout.vb`, `UI/MainForm_Render_Cards.vb`, plus `docs/vpfr-buckets-histogram-{kickoff,proposal}.md` (new) | +875 / −10 |
| 2 | `94bb78a` | `fix(ui-reskin): downsample VPFR histogram to 8 visual buckets` | `UI/MainForm_Render_Cards.vb` | +49 / −7 |
| 3 | `b77703d` | `fix(ui-reskin): VPFR histogram bucket count 8 → 16` | `UI/MainForm_Render_Cards.vb` | +9 / −8 |

All commits local-only. None pushed. None touched `MainForm.Designer.vb`, `settings.json`, scoring engine logic (only the `CalcVPFRLite` signature got byproducts), CSV schema, indicators, or analysis pipeline.

Net engine surface delta: `CalcVPFRLite` gains 3 `ByRef` outputs. `IndicatorResults` gains 3 properties. Zero scoring change.

Net UI delta: 1 `VolumeHistogramMini` instance added to `BindCardVolumeProfile`. Row 7 height 210 → 320 px. `VolumeHistogramMini.vb` itself untouched — paint carve-out not invoked.

---

## 7. Suggestions for the spec author

### 7.1 (HIGH) Open a separate spec for the SC-column / TOOL row discrepancy

§5 above is the meat. The implementation conversation has enough information to point at the likely Trend Str row SC accounting issue but not enough authority to call which interpretation is correct (per §5.3 options a/b/c — that's a scoring-semantics decision, not a render-code decision).

This is the only finding from Spec B's sanity check that warrants a follow-up spec. Everything else is documentation polish.

### 7.2 (LOW) Promote kickoff §3.3 option 1 to canonical for `VolumeHistogramMini` width

Fixed 500 px held cleanly at form width 1100 px. The "anchor-based" and "Dock-Top panel wrapper" alternatives in option 2/3 didn't need to be invoked. Save future kickoffs the indecision.

### 7.3 (LOW) Promote `VISUAL_BUCKETS = 16` as the default for `VolumeHistogramMini` consumers

For BTC-PERPETUAL with default `cfg.Indicators.VPFR.NumBuckets = 50` and the typical ATR-scale 0.7-1.5× session range, 16 visual buckets ≈ 57-points-per-bucket lands at the trader's intra-session swing resolution. 8 reads too coarse; 32 reads venetian. 16 is the goldilocks zone. Worth pinning in any future spec that re-uses the control.

### 7.4 (NICE) The kickoff §7.1 shadowing warning was load-bearing

> "VB.NET overload resolution will not warn loudly on this; the compiler may silently bind the local to the inner scope and leave the output uninitialised, which the UI will see as `bucketSize = 0` and silently skip the histogram. The bug presents as 'histogram never appears' — not as a build error. Watch for it."

This was correct and the `bucketSizeOut` rename prevented a real silent-failure path. Keep this pattern in any future spec that proposes `ByRef` parameter additions to existing methods.

---

## 8. What was NOT done (scope boundary)

The three-commit chain stayed strictly inside Spec B + its anticipated follow-up:

- ❌ No scoring engine changes (only added `ByRef` outputs to `CalcVPFRLite`; existing outputs bit-identical).
- ❌ No `UI/Controls/*.vb` changes — paint carve-out not invoked. `VolumeHistogramMini` accepted any bucket count via its existing `Buckets As Single()` setter; no API surface change needed.
- ❌ No `settings.json` changes. No new keys; bucket count stays at `cfg.Indicators.VPFR.NumBuckets`.
- ❌ No CSV schema changes.
- ❌ No `MainForm.Designer.vb` edits.
- ❌ No engine-side downsampling. UI-side aggregation only, per kickoff §6 explicit prohibition.
- ❌ No value-area band colouring on the histogram body (kickoff §6, proposal §11).
- ❌ No click-to-anchor or event surface on the histogram (kickoff §6, proposal §11).
- ❌ No fix to the SC-column / TOTAL discrepancy (§5) — flagged for a separate spec.
- ❌ Nothing pushed to the remote.

The histogram is now a working consumer of the engine's bucket data. Future enhancements (value-area band shading, click-anchored price interaction) remain available as separate specs without needing to re-touch the engine surface.

---

**End of report.**
