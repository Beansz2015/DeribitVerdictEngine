# Candle-store derivation batch — summary (2026-07-31)

**Seat:** Opus orchestrator. **Brief:** the 2026-07-31 handover, JOB 2.
**Class:** derivations. **No `settings.json` change, no scoring change, no version bump, nothing pushed.** Settings remain **v64** (owned by the trade-store build).
**Full analysis:** [`candle-store-derivations-2026-07-31.md`](candle-store-derivations-2026-07-31.md). **Review packet:** [`candle-store-derivation-batch-spec-back.md`](candle-store-derivation-batch-spec-back.md). **JOB 1 review:** [`trade-store-capture-review-2026-07-31.md`](trade-store-capture-review-2026-07-31.md).

---

## §0 — Two findings that change how the rest of this reads

**§0.1 — `indicators.TTM.flat_threshold` is ~100× too small, and production confirms it.**
`flat_threshold` gates a quantity in **price units (USD)** — the 7-bar drift of `close − SMA20`. Its measured median is **52.6 on 1m** and **76.5 on 3m**. The live value is **0.5**. The FLAT dead-zone therefore captures **0.55% / 0.41%** of bars in the derivation and **0.69% / 0.61%** in the live book. TTM awards a directional +1 on **~64%** of runs, and whether it does is decided almost entirely by whether the histogram sign happens to match the drift sign. This is the v53 funding-count-window defect class: **arithmetic, not sampling**. It is not a threshold sweep and it escapes the §12 W1 absorption.

**§0.2 — the session-volume multiplier is a dial on a signal that fires 0.69% of the time.**
Item 4's whole lever is **inert on the live engine**. `VolumeRatio`'s numerator is the *in-progress* bar, and with `trigger_mode:"on_close"` a run fires as a bar closes, so `.Last()` is the newly-opened bar: live `VolumeRatio` p50 is **0.0100** (res-1) and **0.0018** (res-3) against a closed-bar 0.66–0.76. Measured live volume-vote fire rate: **0.69%** of NY runs, **2.66%** of res-3 runs. **No multiplier recommendation is made — that is the finding.** Item 4 is blocked by the D3 forming-bar question, and that dependency is not currently in `backlog-dependency-map.md`.

**And one correction to my own working, placed here because it governs every population number:** I flagged a 15pp store-vs-production gap on 3-min OBV and chased it as a possible production defect. It was **my sampling design**, not production. See §5.

---

## 1. Item-by-item outcome

| # | Brief item | Outcome |
|---|---|---|
| 1 | §12 3-min weekday-ASIA `session_volume` re-verify | **Live half already CLOSED by v58** (2026-07-22). **Bundled candle half DONE**: OBV re-anchor + clamp-binding check + fire rate. |
| 2 | §12 TTM `flat_threshold` | **DONE — the headline.** Recommend 1m **25.0** / 3m **40.0**. Measured 3m/1m ratio **1.45**, not the ×2.1 proxy. |
| 3 | §12 swing pivot wing/lookback | **DONE — recommend no change**, both knobs, with evidence. Lookback is provably not binding. |
| 4 | §12 session volume multipliers | **DONE as a blocking finding.** No multiplier recommended; the lever is inert live (§0.2). |

## 2. Item 1 — split, as the brief asked

**The live half is closed, and §12 is stale about it.** `docs/roadmap.md`'s 2026-07-22 update and `settings.json`'s v58 entry both record the row as CLOSED: the re-verify ran on n=344 weekday 3-min ASIA rows (`asia-session-volume-reverify-2026-07-22.md`), ASIA was dialed 1.10/1.05 → **1.00/1.00**, and `backlog-dependency-map.md` line 10 is ticked. **But `DeribitIndicatorProject.md` §12 (line 330) still lists it as an open Medium row.** Doc-drift; the roadmap and `settings.json` are right.

The v58 read explicitly deferred the bundled offline half — its §5 says *"Flag for the OBV `|obvChange|` re-anchor bundle the roadmap W1 row pairs with this row."* **That half is what I did:**

- **OBV `|obvChange|` re-anchor.** 6-month p50: NY(1m) **24.23**, ASIA(3m) **22.24**, LONDON(3m) **23.95**. v33 set `trend_gate` 18.0 from a *one-session* p50 of 18.5 targeting ~50% directional; it now runs 58–62%. **Recommend 18.0 → ≈23** to restore v33's own design point.
- **A decision-of-record worth keeping:** the three p50s span 22.2–24.2 (±4%), and the 3m/1m ratio is **≈0.95**. `obvChange` is dimensionless by construction and the data confirms it. **OBV needs no per-session or per-resolution split** — nobody should spend a pass on one.
- **ASIA/LONDON clamp-binding check — closes clean, no change indicated.** Over 6 months the *minimum* clamps (2.0 / 1.5) bind **0.0%** of the time on every session, and the low-SD static-fallback branch fires **0.0%**. The maxima bind occasionally and only where expected (NY 6.8% / 3.7%, the most dispersed session).
- **ASIA fire rate.** Closed-bar full-fire 3.17%, any-fire 9.83% — but see §0.2 for what that means live.

**What I could not do:** the row's other half — ASIA trade-rate / `RANGE_BOUND` share on weekday 3-min rows — needs logged CSV rows, not candles. It was already done by v58 on live rows and needs no repeat.

## 3. Item 2 — TTM (headline; full tables in the derivation doc §1)

| `flat_threshold` | 1m AWARD% | 1m FLAT% | 3m AWARD% | 3m FLAT% |
|---:|---:|---:|---:|---:|
| **0.5 (live)** | **64.84** | **0.55** | **64.40** | **0.41** |
| 25.0 | **50.46** | 26.94 | 55.07 | 18.28 |
| 40.0 | 42.32 | 40.52 | **49.28** | 28.39 |

**Recommendation: 1m 25.0 / 3m 40.0** (AWARD ≈ 50%). Alternative anchor at FLAT ≈ 22% gives 1m 20.0 / 3m 30.0. The *target rate* is a trader/Fable call; the distribution only says what each threshold buys.

**Direct answer to the §12 *v36 Phase-2 threshold carry-forward* row:** the measured 3m/1m ratio for this key is **1.45** (p25 1.50, p75 1.37). The ATR-derived **×2.1 proxy over-scales it by ~40–50%**. Third independent confirmation of the F15 "measure, don't blind-scale" finding, after v40's ROC magnitude and slope.

## 4. Item 3 — swing pivots (full table in the derivation doc §4)

| wing/lb | tgt-placeable (both ≤3.5×ATR) | crossed within 20 bars | med. pivot age |
|---|---:|---:|---:|
| **w3 / lb30 (live)** | **45.5%** | **56.5%** | **8 bars** |
| w3 / lb45 | 45.6% | 56.3% | 8 |
| w4 / lb30 | 37.3% | 52.5% | 10 |
| w5 / lb30 | 31.2% | 49.0% | 13 |

**`lookback_bars_5m = 30`: not binding — do not spend a pass.** Median pivot age is 8 bars; lb45 moves placeability by +0.1pp.
**`pivot_wing_5m = 3`: keep.** §12's "widen to 4–5" costs **14.3pp** of placeability to buy **7.5pp** of level durability — the wrong direction for an engine whose `STRUCTURALLY_WEAK` / `FALLBACK_ATR` path the v51 B4b arc exists to shrink.
**Caveat carried, not buried:** "crossed within 20 bars" is **not** a false-positive rate. It is direction-blind — a crossed swing high is a *reached target* for a long and a *taken stop* for a short. Separating them needs outcomes, which the brief's dividing line puts out of reach. The recommendation rests on the placeability axis, which is unambiguous.

## 5. The number that did not reconcile, and whose fault it was

TTM reconciled against production immediately (derived FLAT 0.55%/0.41% vs logged 0.69%/0.61%). **OBV did not**: a uniform 6-month weekday sweep gave 58–62% directional on 3-min against a logged **77.05%**. I treated the 15pp gap as a possible production defect and worked it:

1. **Period** — a July-only arm still gave ~62%. Eliminated.
2. **Window length** — `WsMarketDataSource.GetCandlesAsync` returns `Math.Min(count, series.Count)`, so a cold WS series shortens the window. A 40→250 sweep showed `|obvChange|` rises **monotonically** with window (p50 7.31 → 22.93), so no window ≤250 reaches 77%. Eliminated — and it predicted the wrong sign anyway.
3. **Matched replay** — recomputing OBV on store bars at the *exact* logged timestamps: **store 77.05% vs production 77.05%**, row agreement **99.22%** (res-1: 63.60% vs 63.88%, 98.35%).

**The gap was my sampling design.** The engine samples in bursts on the days it runs; the res-3 book is 78% LONDON concentrated on 2026-07-16…22, which were far more directional than the 6-month average. My uniform bar sweep was never like-for-like. **The store carries the population value and the production book is a sample of it** — which is why the re-anchor in §2 is taken from the store and not from the live book.

Recorded plainly because the closing note asked for it, and because the same trap is live for anyone comparing a store sweep against a logged rate.

## 6. Data quality — the brief's note does not reproduce

The brief says *"at least one candle in the store has a zero close … do not take raw price extrema from the store without filtering."* Scanning **all four resolutions** for a non-positive Open/High/Low/Close returns **zero bars**:

| Res | Bars | Non-positive OHLC | Zero-volume | Duplicate timestamps |
|---|---:|---:|---:|---:|
| 1m | 259,974 | **0** | 5,472 (2.10%) | 0 |
| 3m | 86,658 | **0** | 226 | 0 |
| 5m | 51,995 | **0** | 84 | 0 |
| 15m | 17,332 | **0** | 25 | 0 |

**Raw price extrema can be taken from this store unfiltered.** What exists instead is **zero-volume bars** — genuine no-trade minutes, not corruption — which matter only where volume is a divisor. Both such sites are already guarded (`CalcOBV`'s `meanVol <= 0`, `VolumeRatio`'s `sma > 0`). Bar counts match the handover's stated 259,974 exactly, with no duplicates at month boundaries.

## 7. Scope kept

No settings touched, no scoring path touched, no fixture family consumed (**A49 still free**, **HC28 still free**), no commit. The derivation harness is scratchpad-only — it `<Compile Include>`s the real `Core/Indicators_*.vb` files rather than copying them, and **no repo project file was modified**. Everything fenced by the brief — live scoring/geometry changes, the D3 forming-bar live change, the `TriggerMode` column, the lane-E/F1 reads, the bridge contract, Tardis — was left alone.
