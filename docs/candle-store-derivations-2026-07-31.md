# Candle-store derivations — TTM · OBV · session volume · swing pivots (2026-07-31)

**Class:** derivation / data read. **Recommendations only — no `settings.json` change, no scoring change, no commit of settings.**
**Instrument:** the 6-month candle store fetched 2026-07-31 (`backtest_data/`), replayed through the **shipped** indicator code.
**Frozen live comparator:** `analysis_log.csv` (8,452 rows), copied to scratchpad and read from there (live-append rule, the `asia-session-volume-reverify-2026-07-22.md` precedent).
**Reported against:** the Opus orchestrator brief of 2026-07-31 (JOB 2). Outcome record: [`candle-store-derivation-batch-summary-2026-07-31.md`](candle-store-derivation-batch-summary-2026-07-31.md). Review packet: [`candle-store-derivation-batch-spec-back.md`](candle-store-derivation-batch-spec-back.md).

---

## 0. Method, and why the numbers should be trusted

Every distribution below comes from the **real shipped functions**, not a re-implementation. A scratchpad-only .NET 8 console project compiles `Core/Indicators_Volatility.vb`, `Core/Indicators_Structure.vb` and `Core/Indicators_Momentum.vb` verbatim from the repo (a `<Compile Include>` of the actual files — no copies), supplying only the `Candle` DTO so the project need not link `DeribitClient.vb` and its `HttpClient`. **Nothing was added to the repo and no repo project file was touched.**

Two quantities the shipped functions do not *return* — TTM's `delta` and OBV's `obvChange` — were transcribed and then **pinned against the shipped function's own classification**:

| Pin | Sample | Result |
|---|---|---|
| `TtmDelta` vs `IndicatorEngine.CalcTTMSqueeze` direction @ `flat_threshold` 0.5 | 5,000 windows on 1m + 5,000 on 3m | **0 mismatches** |
| `ObvChange` vs `IndicatorEngine.CalcOBV` trend @ `trend_gate` 18.0 | 3,000 × 250-bar windows on 1m + 3,000 on 3m | **0 mismatches** |

Swing pivots call `IndicatorEngine.CalcSwingPivots` directly. The volume baseline is transcribed from `DynamicNorms.vb:39–62` + `ApplySessionVolume` (`:137–138`).

**The strongest check is the matched replay** (§0.2): the same OBV computed at the *exact* timestamps the live engine logged, compared row-by-row against the logged `OBVTrend`.

### 0.1 Store shape and data quality

| Resolution | Bars | Span | Non-positive OHLC | Zero-volume bars | Duplicate timestamps |
|---|---:|---|---:|---:|---:|
| 1m | 259,974 | 2026-01-31 → 2026-07-30 | **0** | 5,472 (2.10%) | **0** |
| 3m | 86,658 | same | **0** | 226 (0.26%) | **0** |
| 5m | 51,995 | same | **0** | 84 (0.16%) | **0** |
| 15m | 17,332 | same | **0** | 25 (0.14%) | **0** |

> **The table above is the store *as derived on*, and is left as the record.** The store has since been damaged and repaired (a 28.2-day funding hole, then a June candle wipe caused by its fix) and now reads 1m **260,137** / 3m **86,713** / 5m **52,028** / 15m **17,343**, 0 missing everywhere plus funding 4,336 / 4,336 — ~2.7 h longer than what these figures were computed on. **Every derivation below was re-run against the repaired store and reproduces**; comparison table in [spec-back §0](candle-store-derivation-batch-spec-back.md), incident history in [`store-integrity-check-2026-07-31-post-fix.md`](store-integrity-check-2026-07-31-post-fix.md).

**The brief's data-quality note does not reproduce.** It says "at least one candle in the store has a zero close." Scanning **all four resolutions** for a non-positive Open/High/Low/Close returns **zero bars**. What the store does contain is **zero-volume bars** — 2.1% of 1m bars, genuine no-trade minutes, not corruption. They matter only where volume is a divisor, and both such sites are already guarded (`CalcOBV`'s `If meanVol <= 0`, and `VolumeRatio`'s `If sma > 0`). Raw price extrema can be taken from this store without the filter the brief called for; **volume**-denominated work still wants the guard.

### 0.2 Matched replay — store vs production, like-for-like

OBV recomputed on store bars at the exact timestamps of every logged run, versus the logged `OBVTrend`:

| ExecResolution | n | Row agreement | Directional: store replay | Directional: production |
|---|---:|---:|---:|---:|
| 1 (NY) | 6,648 | **98.35%** | 63.60% | 63.88% |
| 3 (ASIA/LONDON) | 1,804 | **99.22%** | 77.05% | **77.05%** |

The store and the live engine compute the same thing. Residual disagreement is the WS-vs-REST bar difference already tracked by the §12 *WS 3-min closed-bar volume undercount* row.

**A correction to my own working, recorded because it changes how to read every population number below.** A uniform sweep over 6 months of weekday 3-min bars puts OBV's directional rate at 58–62%; production reads 77%. I treated that 15pp gap as a possible production defect and chased it — eliminating period (a July-only arm still gave 62%) and window length (a 40→250 sweep is monotonic, so no window ≤250 reaches 77%). The matched replay then reproduced production **exactly**. The gap was **my sampling design**, not production: the engine samples in bursts on the days it happens to be running, and the res-3 book is 78% LONDON concentrated on 2026-07-16…22, which were far more directional than the 6-month average. **The population value is the store's; the production book is a sample of it.** For threshold re-anchoring the population value is the right one — which is the whole reason to have the store.

---

## 1. TTM `flat_threshold` — the headline

### 1.1 What the knob actually gates

`CalcTTMSqueeze` (`Core/Indicators_Volatility.vb:146–179`) compares `flat_threshold` against

> `delta = mean(last 2 of 7) − mean(first 2 of 7)` where each of the 7 values is `close(i) − SMA20(close)`

`delta` is therefore in **price units (USD)**. The §12 row's framing — *"review FLAT vs RISING/FALLING against 1m candle range distribution"* — has the right order of magnitude but the wrong quantity; this is the momentum-oscillator drift, not the candle range.

### 1.2 The measured distribution

`|delta|`, weekday, each resolution restricted to the sessions that actually run it:

| Population | n | p10 | p25 | p50 | p75 | p90 | p95 | p99 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1m · NY | 84,714 | 8.90 | 23.09 | **52.58** | 101.53 | 172.83 | 232.95 | 396.40 |
| 3m · ASIA | 20,640 | 13.84 | 36.38 | **79.30** | 143.90 | 233.59 | 310.31 | 539.55 |
| 3m · LONDON | 12,900 | 12.65 | 32.55 | **72.31** | 132.69 | 209.14 | 274.69 | 460.46 |
| 3m · pooled | 33,540 | 13.33 | 34.66 | **76.48** | 139.29 | 224.33 | 296.71 | 510.83 |

**The live value is 0.5.** It sits below the 1st percentile of both distributions — roughly **100× too small** to do the job it was added for.

### 1.3 What that does operationally

Only `BULL_BUILDING` / `BEAR_BUILDING` award a vote (`ScoringEngine_Calculate_Scoring.vb:262–276`); `*_FADING` and `FLAT` award nothing.

| `flat_threshold` | 1m AWARD% | 1m FADING% | 1m FLAT% | 3m AWARD% | 3m FADING% | 3m FLAT% |
|---:|---:|---:|---:|---:|---:|---:|
| **0.5 (live)** | **64.84** | 34.62 | **0.55** | **64.40** | 35.19 | **0.41** |
| 5.0 | 62.27 | 32.10 | 5.63 | 62.66 | 33.58 | 3.76 |
| 10.0 | 59.32 | 29.45 | 11.23 | 60.81 | 31.67 | 7.53 |
| 15.0 | 56.39 | 26.94 | 16.66 | 58.85 | 29.92 | 11.23 |
| 20.0 | 53.40 | 24.69 | 21.91 | 57.02 | 28.20 | 14.78 |
| **25.0** | **50.46** | 22.60 | 26.94 | 55.07 | 26.65 | 18.28 |
| 30.0 | 47.71 | 20.55 | 31.74 | 53.14 | 25.16 | 21.70 |
| **40.0** | 42.32 | 17.16 | 40.52 | **49.28** | 22.33 | 28.39 |
| 52.0 | 36.58 | 13.85 | 49.57 | 44.77 | 19.21 | 36.02 |
| 76.0 | 26.73 | 9.04 | 64.23 | 36.21 | 14.03 | 49.76 |

**Production confirms the derivation.** Logged `TTMDirection`, 8,452 rows:

| | derived FLAT% @0.5 | logged FLAT% | derived AWARD% @0.5 | logged AWARD% |
|---|---:|---:|---:|---:|
| ExecRes 1 | 0.55 | **0.69** | 64.84 | **64.23** |
| ExecRes 3 | 0.41 | **0.61** | 64.40 | **63.92** |

So the FLAT dead-zone the design intended is **not functioning**: it suppresses ~0.6% of runs, and TTM contributes a directional +1 on ~64% of them. Whether a vote is awarded is decided almost entirely by whether the histogram sign happens to agree with the linreg drift sign — close to a coin flip.

This is the same defect class as the v53 funding count-window: **arithmetic, not sampling.** A threshold in the wrong units cannot be fixed by more data.

### 1.4 The ×2.1 question — measured, not proxied

§12's *v36 Phase-2 threshold carry-forward* asks whether `TTM.flat_threshold` should scale ≈2.1× on 3-min, *or* be re-measured. Measured:

> **3m/1m ratio of `|delta|` p50 = 76.48 / 52.58 = 1.45.** Not 2.1.

At the p25 anchor the ratio is 1.50; at p75, 1.37. The ATR-derived ×2.1 proxy **over-scales this key by ~40–50%**. This is the third independent confirmation of the F15 "measure, don't blind-scale" finding (after v40's ROC magnitude and slope).

### 1.5 Recommendation

Re-anchor in **price units, per execution resolution**, to a stated AWARD-rate target. Two defensible anchors:

| Principle | 1m | 3m | Resulting AWARD% (1m/3m) | Resulting FLAT% |
|---|---:|---:|---|---|
| **A — AWARD ≈ 50%** (recommended) | **25.0** | **40.0** | 50.5 / 49.3 | 26.9 / 28.4 |
| B — FLAT ≈ 22% | 20.0 | 30.0 | 53.4 / 53.1 | 21.9 / 21.7 |

**My read (hypothesis):** **A**. `AWARD%` is the operational quantity — it *is* the vote — whereas FLAT% is an intermediate. 50% is a defensible selectivity target for a Tier-1 confirmation signal and is a large improvement on 64.8% without gutting the signal. Under A the 3m value carries its own measured 1.60 ratio rather than an inherited proxy.

**The target rate itself is a trader/Fable call, not something the distribution answers.** The distribution says only what any given threshold buys.

**Homes:** `indicators.TTM.flat_threshold` is a flat global today. Per-resolution values need `resolution_profiles["3"]` to gain the key — the same shape v40 used for `roc_slope_delta_threshold`, which is precedent, not new machinery.

**⚠ This is a live scoring change.** Raising the threshold reduces TTM's vote rate, which lowers scores, which shifts the verdict distribution. It needs its own spec and its own dataset boundary. **Fenced out of this batch by the brief; not built, not proposed as a settings pass.**

---

## 2. OBV `trend_gate` re-anchor

The §12 3-min-ASIA row bundles *"the OBV `|obvChange|` median re-anchor on multi-session data"*. v33 set `trend_gate` 10.0 → **18.0** from a **one-session** offline recompute that put `|obvChange|` p50 at 18.5, targeting ~50% directional.

`obvChange = (obvLast − obvFirst) / meanVol` — **dimensionless by construction**, so it is comparable across sessions, resolutions and volume regimes. Six months confirms that design assumption held:

| Population | n | p25 | **p50** | p75 | p90 | p95 |
|---|---:|---:|---:|---:|---:|---:|
| 1m · NY | 16,943 | 12.01 | **24.23** | 40.63 | 59.98 | 73.15 |
| 3m · ASIA | 10,320 | 10.11 | **22.24** | 36.06 | 50.77 | 60.66 |
| 3m · LONDON | 6,450 | 10.63 | **23.95** | 38.86 | 53.51 | 62.32 |

Directional share (`|obvChange| > gate`):

| gate | NY | ASIA | LONDON |
|---:|---:|---:|---:|
| 14 | 70.6% | 67.0% | 67.1% |
| **18 (live)** | **62.2%** | **58.1%** | **60.3%** |
| 22 | 54.3% | 50.5% | 53.2% |
| 26 | 46.8% | 43.1% | 46.2% |
| 30 | 40.1% | 35.9% | 39.1% |

### Two results

**(a) Re-anchor recommendation: `trend_gate` 18.0 → ~23.** The pooled p50 has moved from the 18.5 of the one-session v33 fit to **22.2–24.2**. Restoring v33's own ~50% design point puts the gate at **≈23** (ASIA ~22, LONDON ~23.5, NY ~24). This is a modest, low-risk re-anchor of an existing intent on 6 months instead of one session.

**(b) A negative result worth recording as a decision-of-record: OBV needs no per-session or per-resolution split.** The three p50s span 22.2–24.2 — a ±4% spread across two resolutions and three sessions. **The 3m/1m ratio is ≈0.95**, so — like TTM, and for a different reason — the ×2.1 resolution proxy does not apply here either. A single global `trend_gate` is correct; nobody should spend a pass splitting it.

**⚠ Also a live scoring change** (OBV divergence blocks cross-category upgrade). Same fence: recommendation only.

---

## 3. Session volume multipliers — **the lever is inert on the live engine**

### 3.1 What the closed-bar analysis says

Weekday, each session at its own execution resolution, using the shipped `DynamicNorms` baseline (100 completed bars, `(mean+2sd)/mean` clamped to [2,6] / [1.5,4]) and the live multipliers (ASIA 1.00/1.00, LONDON 1.00/1.00, NY 1.15/1.10):

| Session | n | VolumeRatio p50 | VolHighThr p50 (with mult) | Full-fire ≥High | Partial Mid..High | Any |
|---|---:|---:|---:|---:|---:|---:|
| NY (1m) | 84,714 | 0.657 | 4.47 | **2.16%** | 6.01% | 8.17% |
| ASIA (3m) | 20,640 | 0.758 | 3.70 | **3.17%** | 6.66% | 9.83% |
| LONDON (3m) | 12,900 | 0.702 | 3.39 | **3.27%** | 6.09% | 9.36% |

Clamp binding (the other half the §12 row bundles):

| Session | highRaw @min 2.0 | highRaw @max 6.0 | midRaw @min 1.5 | midRaw @max 4.0 | low-SD static fallback |
|---|---:|---:|---:|---:|---:|
| NY | **0.0%** | 6.8% | **0.0%** | 3.7% | **0.0%** |
| ASIA | **0.0%** | 3.1% | **0.0%** | 1.4% | **0.0%** |
| LONDON | **0.0%** | 2.3% | **0.0%** | 0.8% | **0.0%** |

> **Clamp-binding check closes clean: no clamp change indicated.** The *minimum* clamps never bind on any session at any resolution over 6 months, and the low-SD static-fallback branch never fires. The maximum clamps bind occasionally and only in the expected direction (most in NY, the most dispersed session).

Two things follow from the fire rates. First, **NY is already the most selective session** (2.16% vs 3.17/3.27) despite sitting at 1.15 while the others sit at 1.00. Second, the *raw* adaptive threshold before any multiplier is **already higher in NY** (3.89 vs ASIA 3.70, LONDON 3.39) — `DynamicNorms` has itself absorbed the session's volume character, and the ×1.15 notch is a second, static correction layered on an adaptive one that already did the job. Firing-rate-matching ASIA/LONDON to NY needs **≈1.12 / ≈1.13** — i.e. the sessions are nearly identical once normalized, and the current asymmetry is not supported.

### 3.2 …and why none of that is actionable today

**The engine does not see this distribution.** `VolumeRatio` is computed as `candlesExec.Last().Volume / SMA9` (`UI/MainForm_Analysis.vb:236–239`), and `auto_run.trigger_mode` is `"on_close"`, so a run fires as a bar closes and `.Last()` is the **newly-opened** bar. Logged, frozen `analysis_log.csv`:

| | VolumeRatio p50 | p95 | p99 | ≥3.0× |
|---|---:|---:|---:|---:|
| ExecRes 1 | **0.0100** | 0.672 | 2.04 | 0.57% |
| ExecRes 3 | **0.0018** | 0.813 | 2.37 | 0.61% |
| *closed-bar counterfactual* | *0.657 / 0.70–0.76* | *~2.9–3.4* | *~5.0–5.5* | *~5–6%* |

A p50 of 0.0100 on a 1-minute bar is the bar sampled roughly **0.6 seconds** after it opened.

Actual live fire rate — logged partial-bar `VolumeRatio` against the store-derived `DynamicNorms` threshold for the same run:

| | full (≥High) | partial (Mid..High) | **any** |
|---|---:|---:|---:|
| ExecRes 1 | 0.17% | 0.53% | **0.69%** |
| ExecRes 3 | 0.33% | 2.33% | **2.66%** |
| *closed-bar counterfactual* | *2.2–3.3%* | *6.0–6.7%* | *8.2–9.8%* |

**The volume vote fires on 0.69% of NY runs.** The multiplier is a dial on a threshold that essentially nothing reaches.

The precise mechanism is an asymmetry inside one comparison: **`DynamicNorms` deliberately excludes the in-progress bar from its 100-bar baseline** (`DynamicNorms.vb:39–42`, and the comment says so), **but `VolumeRatio`'s numerator *is* the in-progress bar.** The threshold is built from closed bars; the value compared against it is a partial one. This refines lane C's measured "≤2.01% of the book" into a named mechanism and a per-resolution number.

**Two consequences.**

1. **Item 4 is blocked by D3, not by data.** Deriving session multipliers against closed-bar distributions describes the D3 closed-bar arm, not the live engine. Any multiplier pass should sequence *after* the forming-bar question is ruled. **This dependency is not currently recorded in `backlog-dependency-map.md`.**
2. **A note on v58, offered as evidence and not as a reversal.** v58 dialed ASIA 1.10/1.05 → 1.00/1.00, reasoning that "the above-neutral notch was suppressing trades the calmer weekday book does not warrant." The notch moved the ASIA high threshold from ~4.07 to ~3.70 against a live ratio whose p99 is 2.37 — so the *stated mechanism* could account for at most a fraction of a percentage point of runs. **v58's direction remains defensible on its own terms** (an unjustified weekend-set notch, dialed back), and the trade-rate change it observed is real; but the volume-vote channel was not what produced it. This matters because the same reasoning is queued next for LONDON and NY.

**No multiplier recommendation is made.** That is the finding.

---

## 4. Swing pivot wing / lookback

`CalcSwingPivots` on the shipped 210-bar 5m fetch, weekday, simulated live runs at every 3rd bar (n=12,349 per configuration). `tgt-placeable` = both sides land within the `structural_levels.target_max_atr_mult` = 3.5×ATR looseness bound, i.e. the swing tier can actually place.

| wing/lookback | both found | med. pivot age | med. dist high / ATR | med. dist low / ATR | **tgt-placeable** | crossed within 20 bars |
|---|---:|---:|---:|---:|---:|---:|
| w2 / lb30 | 99.9% | 5 | 1.43 | 1.39 | 53.8% | 61.1% |
| **w3 / lb30 (live)** | **99.3%** | **8** | **1.77** | **1.72** | **45.5%** | **56.5%** |
| w3 / lb45 | 100.0% | 8 | 1.77 | 1.73 | 45.6% | 56.3% |
| w4 / lb30 | 97.0% | 10 | 2.05 | 2.02 | 37.3% | 52.5% |
| w4 / lb45 | 99.7% | 11 | 2.06 | 2.02 | 37.5% | 52.4% |
| w5 / lb30 | 92.8% | 13 | 2.34 | 2.25 | 31.2% | 49.0% |
| w5 / lb45 | 98.9% | 13 | 2.35 | 2.25 | 31.8% | 48.9% |

### Recommendations — both are "leave it alone", with evidence

**`lookback_bars_5m = 30`: not binding. Do not spend a pass on it.** The median found pivot is **8 bars** old; lb45 changes placeability by **+0.1pp**. The lookback is never the constraint, at any wing. This closes that half of the §12 row as a negative result.

**`pivot_wing_5m = 3`: keep.** §12 suggests widening to 4–5 "on low-volatility sessions". The trade is bad: going 3→5 buys a **7.5pp** reduction in 20-bar level crossing (56.5% → 49.0%) and costs **14.3pp** of placeability (45.5% → 31.2%), plus it pushes the median level from 1.77 to 2.34 ATR away. Placeability feeds directly into the `STRUCTURALLY_WEAK` tag and the `FALLBACK_ATR` path that the whole v51 B4b structural-first arc exists to reduce. Paying ~2 points of structure to buy 1 point of level durability is the wrong direction for this engine.

### What this metric cannot say — stated because the §12 row asks for something else

The §12 row asks for a **false-positive rate**. Candles cannot produce one. "Crossed within 20 bars" is direction-blind: for a long position the swing high being crossed is the **target being reached**, which is a success; for a short it is the stop being taken. Separating them needs the verdict direction joined to outcomes — which is exactly what the brief's dividing line puts out of reach until trades-covered replay exists. **The placeability axis above is unambiguous and is what the recommendation rests on; the crossing column is context only.**

---

## 5. Reproduction

The harness is scratchpad-only and deliberately not committed (it is a one-off derivation instrument, not a tool the project maintains). To reproduce: a `net8.0` VB console project with `EnableDefaultCompileItems=false`, compiling `Program.vb` (a local `Candle`/`TradeRecord`/`OrderBookSnapshot` DTO trio plus the drivers) alongside `<Compile Include>` of the three `Core/Indicators_*.vb` files, reading `backtest_data/` and the frozen CSV copy. Pins: `PinTtm` / `PinObv` as described in §0. Path:
`…\scratchpad\deriv\` (session-scoped; regenerate rather than rely on it).

---

## 6. Confirmation re-run after the store repair (2026-07-31, trader-requested)

**Result: every recommendation stands. Nothing moved.**

Context: `store-integrity-check-2026-07-31-post-fix.md` found June destroyed at 3m/5m/15m and advised re-running once repaired. The store was repaired in `cb1ffb9` (merge-instead-of-overwrite in both backfill paths) and swept gap-free at all four resolutions plus funding. The same harness was rebuilt and re-run against it — **the original instrument, unmodified**, so this is a re-execution, not a re-implementation.

**Timeline note that changes how to read the diff:** the published run (`run5`, 02:39) predates the damage (03:36). So this is not damaged-vs-repaired — it is **intact-vs-repaired-plus-extra-data**. The repair fetch extended the store by ~2.7 hours of NY-session bars: 1m 259,974 → **260,137**, 3m 86,658 → **86,713**, 5m 51,995 → **52,028**. Since the 3m arms filter to ASIA/LONDON and the added hours are NY, **every 3m population is byte-identical** and only the 1m/5m arms move at all.

| Published | Re-derived | |
|---|---|---|
| TTM 1m \|delta\| p50 **52.58** | **52.50** | ✓ |
| TTM 3m \|delta\| p50 **76.48** | **76.4750** | ✓ exact |
| 3m/1m ratio **1.45** (not 2.1) | **1.457** | ✓ |
| Principle A, 1m 25 / 3m 40 → AWARD **50.5 / 49.3**, FLAT **26.9 / 28.4** | **50.44 / 49.28**, **26.97 / 28.39** | ✓ |
| Principle B, 1m 20 / 3m 30 → AWARD **53.4 / 53.1**, FLAT **21.9 / 21.7** | **53.39 / 53.14**, **21.93 / 21.70** | ✓ |
| OBV p50 span **22.2–24.2** (ASIA/LONDON/NY) | **22.24 / 23.95 / 24.21** | ✓ |
| OBV 3m/1m ratio **≈0.95** | **0.947** | ✓ |
| Volume vote fires on **0.69%** of NY runs | **0.69%** | ✓ exact |
| Swing w3/lb30: pivot age **8**, **1.77** ATR, placeable **45.5%**, crossed **56.5%** | 8, **1.76**, **45.5%**, **56.5%** | ✓ |
| Swing 3→5 costs **14.3pp** placeability, buys **7.5pp** crossing | **14.2pp** / **7.4pp** | ✓ |
| lb45 changes placeability by **+0.1pp** | **+0.1pp** | ✓ |
| Data quality: 0 bad-price bars, ~2.1% zero-volume 1m | 0 / **2.10%** | ✓ |

**The pins still hold:** 16,000 sampled windows (5,000 TTM × 2 resolutions, 3,000 OBV × 2) against the shipped `CalcTTMSqueeze` / `CalcOBV` — **0 mismatches**, unchanged.

**One number improved, and it is worth recording.** The matched replay's ExecRes-1 row agreement rose **98.35% → 98.59%**, and store-replay directional went **63.60% → 63.87%** against production's 63.88% — closing a 0.28 pp gap to **0.01 pp**. ExecRes 3 was already exact (99.22%, 77.05% vs 77.05%) and is unchanged. More store coverage makes the replay track production more closely, which is the direction that should reassure rather than worry.

**§0.2's conclusion is therefore reinforced, not revised:** the store reproduces production, and the 15 pp OBV gap chased in §0.2 remains a sampling-design artefact rather than a production defect.
