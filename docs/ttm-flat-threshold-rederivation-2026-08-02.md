# D1 / D-A — TTM `flat_threshold` re-derivation (2026-08-02)

**From:** the Opus orchestrator seat. **Supersedes the D-A ladder** in [`candle-store-derivation-batch-spec-back.md`](candle-store-derivation-batch-spec-back.md) and answers the re-derivation [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) §2 called for.
**Method:** full store replay, `backtest_data\` 1m + 3m, 2026-01 → 2026-07, recomputing `CalcTTMSqueeze` and `CalcBBW` exactly as `Core/Indicators_Volatility.vb` implements them (SMA 20 / linReg 7 / BB 20×2 / 100-bar series / `squeeze_percentile` 0.20). **n = 259,594 (1m) · 86,526 (3m).**
**D-F pre-flight:** store re-verified complete on 2026-07-31 (0 missing at all four resolutions); no repair ran between then and this replay.

> **Headline: the unit defect is real and confirmed, but BOTH numbers the previous work produced must be discarded — the 25.0/40.0 ladder *and* the 1.45 ratio it was to be rebuilt from. `flat_threshold` is denominated in the wrong kind of unit, not merely set to the wrong value.**

> ⛔ **OUTCOME — PARKED 2026-08-02 (trader).** No build, and **no ⚠ boundary is opened for this.** The finding is banked; the fix is not scheduled. **It rides the next scoring boundary that opens for another reason** — the same treatment the v64 `change_log` rider got, which could not justify a boundary alone and travelled on D3's. **What would un-park it: trades-covered replay**, at which point TTM's vote can be tested against outcomes instead of counted, and an ATR-relative form showing a real gradient would earn its own boundary. Until then `flat_threshold` stays **0.5** and the FLAT band stays inert — a known, recorded, deliberate state rather than an unnoticed one.

---

## 1. What reproduces

| Claim | Prior | This replay | Verdict |
|---|---|---|---|
| `flat_threshold` is in raw price units | — | `deltas.Add(Close − sma)`, compared directly | ✅ confirmed in code |
| The FLAT band is inert at 0.5 | FLAT 0.54 % / 0.41 % | **0.83 % / 0.41 %** | ✅ reproduces |
| Pre-gate award rate | AWARD 64.84 / 64.40 | **64.10 / 64.64** | ✅ reproduces (≤0.74 pp) |
| `AWARD%` is not the vote | vote 43.37 / 46.29 (CSV) | **46.54 / 46.29** (store) | ✅ confirmed |
| ~100× too small | — | \|delta\| p50 **40.25 / 71.39** vs a 0.5 threshold | ✅ ~80× / ~143× |

**The correction that `AWARD%` is not the vote is upheld, independently and on a different instrument.** The award block sits under `Case "RELEASING","NONE"`; ACTIVE rows (24.7 % / 25.5 % here) award nothing regardless of TTM's value.

---

## 2. What does NOT reproduce — the 1.45 ratio

**Measured 3m/1m ratio of \|delta\|: 1.774 at p50** (p25 1.800 · p75 1.736), against the recorded **1.457**.

It is not a windowing artefact. Per month: **1.714 · 1.846 · 1.776 · 1.726 · 1.782 · 1.769** — stable in every month of the store, and sitting essentially on **√3 = 1.732**, which is what tripling the bar duration gives for a random walk. That is a strong independent check that this figure is right.

I tested the most plausible alternative quantity: the prior read might have measured **\|histogram\|**, which *is* logged where `delta` is not. It gives **1.749** — also ~1.75. So the 1.457 does not come from the histogram either, and **I cannot reproduce it from any quantity on this path.**

> **Consequence: the instruction to "decide the 3m value from the measured 1.45 rather than a grid quotient" is itself built on an unreproducible number and must not be inherited.** This is the *second* figure in the same derivation that fails verification. The first (`AWARD%`) was caught by reading the code; this one only by recomputing.

---

## 3. What the proposed ladder would actually have done

| | vote at live 0.5 | vote at the proposed 25 / 40 |
|---|---|---|
| 1m | 46.54 % | **35.11 %** |
| 3m | 46.29 % | **35.92 %** |

This confirms the prior read's prediction of ~34–36 % — **away from the ~50 % the recommendation existed to reach**, not toward it. And in ATR terms 25.0 USD on 1m is ≈ **1.19 × ATR** at current volatility, an enormous band: it is not a mild over-shoot, it is roughly 4–5× larger than any moderate FLAT band would want.

---

## 4. The finding that changes the shape of the fix

**`delta` is a volatility quantity, not a price quantity.** Per-month coefficient of variation of \|delta\| p50 across 2026-02…07:

| Normalisation | 1m CV | 3m CV |
|---|---:|---:|
| Absolute USD *(what ships today)* | 26.5 % | 26.1 % |
| Basis points of price | 28.4 % | 28.0 % — **worse** |
| **Multiples of ATR(7)** | **11.7 %** | **6.5 %** |

Price-normalising makes it *worse*, and the data shows why plainly: from May to June the median close fell 78,126 → 63,135 while \|delta\| p50 **rose** 32.16 → 42.46. Price fell, the quantity grew. It tracks realised volatility.

**So an absolute USD constant cannot be right for more than one volatility regime** — the same defect class as the ATR display bands the trader profile already flags for re-checking when BTC moves, and the same class as the doc headers carrying stale version numbers. A number chosen today is wrong by ~26 % within months, in either direction.

### 4.1 In ATR units the per-resolution ladder dissolves

| k (× ATR) | 1m FLAT% | 1m VOTE% | 3m FLAT% | 3m VOTE% |
|---|---:|---:|---:|---:|
| 0.20 | 7.63 | 44.15 | 9.51 | 43.12 |
| **0.25** | **9.53** | **43.45** | **11.79** | **42.28** |
| **0.30** | **11.43** | **42.77** | **14.10** | **41.45** |
| 0.40 | 15.24 | 41.37 | 18.82 | 39.72 |
| 1.00 | 36.67 | 32.98 | 44.76 | 29.57 |

**One k serves both resolutions within ~2 pp on both metrics** — where the USD form needed 25 vs 40 to be comparable. The whole "what is the 3m/1m ratio" question, which consumed the previous derivation and produced an unreproducible number, **is an artefact of the wrong unit.**

### 4.2 The knob is low-leverage, and that cuts both ways

Across the entire plausible range the vote rate moves only 45.5 % (k=0.10) → 42.8 % (k=0.30). An 11 % FLAT band costs **3.8 pp of vote**, because most newly-FLAT rows were not voting anyway — either ACTIVE, or `histogram` and `direction` already disagreed. **Fixing the unit is cheap in behavioural terms: low risk, and correspondingly low expected reward.**

---

## 5. D-table — **AWAITING TRADER**

| # | Question | Options | Recommendation |
|---|---|---|---|
| **D1-a** | Fix the **unit** or just the **value**? | (a) ATR-relative multiple, replacing the USD constant · (b) new USD constant · (c) no change | **(a).** It is the only option that survives a volatility regime change, it collapses the per-resolution ladder, and the measured stability gain is **2–4×**. (b) buys a number that is ~26 % wrong within months. |
| **D1-b** | If (a), what multiple? | 0.20 / **0.25** / 0.30 / 0.40 | **0.25–0.30**, one value for both resolutions. FLAT lands ~9.5–14 % (a band that does something without gutting the signal) and the vote stays 41.5–43.5 %, i.e. within ~4 pp of today. **0.25 if the priority is minimal disturbance; 0.30 if the priority is a band that visibly bites.** |
| **D1-c** | If (b) — a USD value now | — | The honest equivalents at 2026-07 median ATR are **1m ≈ 5.2, 3m ≈ 11.7** (k=0.25) — an order of magnitude *below* the retired 25/40, and needing a re-check every regime. **Recorded for completeness; not recommended.** |
| **D1-d** | Ship it at all? | (a) build now · (b) park | ✅ **RULED 2026-08-02 (trader): (b) PARK.** No boundary is opened for this. **D1-a/b/c are answered in principle but not scheduled** — when this is built it goes ATR-relative at k ≈ 0.25–0.30, one value for both resolutions; until then nothing changes and `flat_threshold` stays 0.5. |

**D1-a = (a) is a CODE change**, not a settings tick: `CalcTTMSqueeze`'s `flatThreshold` parameter becomes a multiple applied to an ATR passed in, so the signature, the call site in `MainForm_Analysis`, the POCO key and the what-if/tweaker surfaces all move together. It needs its own spec and its own ⚠ boundary. **It is not the one-line change the queue row implies.**

---

## 6. What I did not verify, stated plainly

- **Nothing is joined to outcomes.** This is a distributional derivation exactly as its predecessor was. Whether *any* `flat_threshold` improves accuracy is unmeasured and stays unmeasurable until trades-covered replay exists.
- **It sits inside the three-instrument finding of 2026-08-02** — engine score AUC **0.5407** with no challenger beating it, tier bands that do not separate, structural placement 0.4683/0.5000/0.3221. A Step-2 threshold re-tune has no demonstrated outcome benefit, and W6-4's own instruction is *"no spend meanwhile."* **That is why D1-d is a real question and not a formality.**
- **ATR here is an SMA of true range over 7 bars.** If `CalcATR` uses Wilder smoothing the stability figures shift slightly; the *ranking* of the three normalisations would not, since the gap is 2–4× and not marginal.
- **The 1.457 discrepancy is unexplained, not merely contradicted.** I reproduced everything else in that read to within 0.74 pp, so this is a targeted failure rather than a general one. Whoever built it may have had a window or filter I have not reconstructed — but the ladder should not rest on it either way.
- **No per-session split examined.** The prior D-B rider found none was needed for OBV; I did not test whether TTM needs one, and an ATR-relative threshold is the form least likely to.
