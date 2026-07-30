# Forming-bar live investigation — 2026-07

**Trigger:** `docs/backtest-synthesizer-proposal.md` §7.2, raised while mirroring live's forming-bar convention in the synthesizer (§7.1).
**Status: REPORT ONLY. No code was changed in any file for this document.** Any change to live last-bar handling is a **maximal ⚠** touching dataset continuity; this document exists to inform a later Fable/trader ruling and does nothing else.
**Scope guard:** §4 enumerates options and states what each would do to dataset continuity. **It deliberately does not recommend one.**

---

## 0. The question, and the short answer to each half

§7.2 asked whether live's last-bar-is-a-forming-bar convention is **intended (v44 on-close design)** or **a latent systematic defect the whole calibration book has absorbed.**

Those turn out to be two separable questions with different answers:

- **Is the behaviour specified?** **Yes, explicitly and deliberately** — the v44 spec named "closed bars only" as a non-goal in so many words, on the correct grounds that changing it would be a re-baseline (§1).
- **Was the *magnitude* of its effect understood?** Nothing in the v44 spec, its spec-back, or the §12 watch list quantifies it, and the numbers in §3 are large: **the Volume signal — one of the trader's PREFERRED indicators and the stated breakout confirmation — can vote on at most 2.01 % of the pooled book, against ~20 % on the same instrument's closed bars.**

Both halves matter and neither settles the other. A specified behaviour with an unmeasured 10× consequence is exactly the shape of thing the §7.2 note was right to name.

---

## 1. What the v44 on-close spec intended for the last-bar state

`docs/on-close-analysis-mode-proposal.md` (APPROVED 2026-06-25, trader sign-off, all §9 decisions confirmed as recommended) is unambiguous. §3 *Explicit non-goals*, verbatim:

> **No engine change.** `RunAnalysisAsync` is untouched — same fetch, same indicators, same `Calculate()`, **same handling of the forming last bar.** On-close does **not** drop the forming bar or evaluate "closed bars only" — that would change the computation (a re-baseline) and is out of scope. The verdict at an on-close fire is exactly what a timer fire at that instant would produce.

Three things follow, and it is worth keeping them apart:

1. **The forming-bar carry predates v44.** It is not something on-close introduced; on-close inherited it from interval mode and deliberately declined to touch it. The whole calibration book back to the WS cutover — and before it, over REST — reads a forming last bar.
2. **The reason given for not touching it is still correct.** Dropping the forming bar changes every candle-derived indicator's input on every row. That is a re-baseline. The spec was right to fence it out of a trigger-only change.
3. **What v44 claimed to improve was the *decision moment*, not the *data*.** §1: "a run happens the instant a bar completes." §12's mode table calls on-close "bar-aligned" in the **bar phase** column. That describes *when the run fires*. It does not claim the run sees a completed bar — and §3 says plainly that it does not.

There is a real tension inside the spec between §2's motivation and §3's non-goal, and it should be named rather than smoothed over. §2 justifies on-close by quoting the trader profile:

> "price breaks above/below previous swing high/low, confirmed by impulse (ROC) and **volume spike** … **Structural breakout required — no chasing candles.**"

and argues that "a breakout is confirmed when a bar *closes* beyond the swing level," so on-close "fires the verdict the instant the confirming bar closes." But by §3's non-goal, the run that fires at that instant computes its **volume** and **range** indicators on the bar that just *opened*, not the one that just closed. The confirming bar is present in the series — it is the second-to-last element — but the volume-spike test reads the last one.

**Nothing in the spec, the spec-back, or §12 quantifies that.** This document does, in §3.

## 1.1 Mechanism — verified in code, not inferred

`MarketState.ApplyChartTick` ([MarketState.vb:90](MarketState.vb:90)) maintains each series so its last element is always the forming bar:

```vb
Dim last As Candle = s(s.Count - 1)
If c.Timestamp = last.Timestamp Then
    s(s.Count - 1) = c                 ' forming bar update
ElseIf c.Timestamp > last.Timestamp Then
    s.Add(c)                           ' bar roll
```

The on-close watcher fires *because* it detected that roll — `DetectBarRoll` compares `series.Last().Timestamp` against the last-seen open-time (proposal §4.2). So at an on-close fire the `s.Add(c)` branch has just run, and **the last element is the brand-new bar, typically 1–2 seconds old.** The just-closed bar is at `Count - 2`.

This is not a WS-only artifact. The REST chart endpoint returns the same shape — the synthesizer's own comment records it as "(count-1) closed bars + 1 forming bar" ([ReplayLoop.vb:271](tools/BacktestRunner/ReplayLoop.vb:271)), which is why §7.1 had to add a stub to mirror live at all.

---

## 2. Which indicators consume the last bar, and how

Every live indicator runs on `candlesExec` ([MainForm_Analysis.vb:236](UI/MainForm_Analysis.vb:236) onward), whose last element is the forming bar. But they are **not** uniformly affected, and the split is sharper than "everything is polluted."

The distinguishing question is **which field of the last bar an indicator reads**:

- A forming bar's **Close is the current traded price** — the freshest, most correct value available. Close-based indicators are arguably *better* for reading it.
- A forming bar's **Volume is a few seconds of flow** where a full bar's is 60 or 180 seconds. Volume-weighted reads are understated by one to three orders of magnitude.
- A forming bar's **High/Low span a few seconds**, so its true range is near zero. Range-based reads are understated, diluted by the averaging window.

| Indicator | Last-bar field read | Sensitivity | Effect of the forming bar |
|---|---|---|---|
| **VolumeRatio** (`CalcVolumeSMA` + `CurrentVolume`) | **Volume**, numerator AND denominator | **SEVERE — the primary finding** | `CurrentVolume` = the stub's seconds of flow; `CalcVolumeSMA` uses `Skip(Count − period)`, so the stub is *also* one of the 9 SMA bars, pulling the denominator down ~1/9. Numerator collapses ~30–200×, denominator falls ~10 %. Measured in §3. |
| **OBV** (`CalcOBV`) | **Volume** + Close | **HIGH** | The stub contributes ~0 to the OBV accumulation, and `meanVol` — the normaliser — includes it, so the whole normalised `obvChange` inflates. Independently observed in `backtest-overlap-validation-2026-07-30.md` §9.5 (OBVTrend 97 %→71 % once the synthesizer started mirroring the stub). |
| **VPFR-lite** (`CalcVPFRLite`) | **Volume** + H/L/C | **MODERATE, structurally awkward** | Buckets accumulate `c.Volume × weight` with exponential recency decay (0.985). The stub takes the **highest** weight slot in the profile while carrying ~zero volume, so the most recent price region is systematically under-represented in POC/HVN/VAH/VAL. |
| **ATR** (`CalcATR`) | **High, Low**, prev Close | **MODERATE** | The stub's true range is ~0, entering the Wilder recursion as the final step: `atr = (atr×(p−1) + tr)/p` ⇒ ATR is pulled down by roughly 1/period (~14 % at period 7) on the last step. ATR feeds fallback levels, the min-move floor comparison, the eval barriers and Kelly. |
| **BBW** (`CalcBBW`) | **Close only** | **LOW** | Std-dev of closes over the window; the stub's close is live price. Correct by construction. |
| **TTM Squeeze** (`CalcTTMSqueeze`) | **Close only** | **LOW** | `candles(i).Close − SMA(close)`. Same as BBW. |
| **EMA 9/21/50, EMA200_5m** (`CalcEMA`) | **Close only** | **LOW — arguably the point** | The final smoothing step uses the stub's close = current price. This is the freshest correct value; dropping the forming bar would make EMAs *staler*. |
| **ROC** (`CalcROCSeries`) | **Close only** | **LOW** | `(close[i] − close[i−9])/close[i−9]`. Reads live price against a closed bar's price. Faithful. |
| **RSI, RSI divergence** (`CalcRSI`, `CalcRSIDivergence`) | **Close only** | **LOW** | Wilder smoothing over close deltas; divergence compares `candles.Last().Close` to pivot prices. Faithful. |
| **DMI / ADX** (`CalcDMI`, on 5m) | High, Low, Close | **LOW–MODERATE** | Directional movement uses H/L; a near-zero-range final bar contributes ~0 DM and a tiny TR into a smoothed series. Diluted by the 5m window and Wilder smoothing. |
| **Donchian(20)** (`CalcDonchian`) | **none — excluded** | **NONE by construction** | `Skip(Count − period − 1).Take(period)` takes the `period` bars ending at `Count − 2`. **The forming bar is deliberately excluded.** Corroborated empirically: DonchianUpper/Lower hit **100.00 %** synthetic↔live agreement once the stub was mirrored (validation report §9.2). |
| **Swing pivots** (`CalcSwingPivots`) | High/Low, but wing-gated | **NONE in practice** | A confirmed pivot needs `wing` bars strictly beyond it on *both* sides, so the last bar can never be a confirmed pivot. |
| **MTF gate** (`CalcMTFGate`, 15m) | Close (EMA) + H/L (DMI) | **LOW** | Same classes as EMA/DMI, on a 15-minute bar where a 1–2 s stub is proportionally negligible. |
| **VWAP / VWAP bands** | Volume-weighted typical price | **LOW** | Volume-weighted over the whole session window (up to 240 bars), so one near-zero-volume bar barely moves the mean. It changes `VWAPSessionCandles` by 1. |

**The pattern:** the forming bar makes **close-based** indicators fresher and **volume/range-based** indicators understated. `Donchian` and the swing pivots are the only ones immune, and `Donchian` is immune because someone wrote it that way — which shows the convention was at least locally considered somewhere in the codebase's history.

---

## 3. Quantification from the live CSV

### 3.1 Method, and what it can and cannot resolve

**Source:** the deduped pooled snapshot built for batch item B — local + AWS-collector rows, local-preferred per UTC session-hour per `aws-collector-deploy-checklist.md` §4.3b. **13,339 rows, 2026-07-03 15:57:49 → 2026-07-30 08:48:02 UTC.** Populations: NY×1 9,780, LONDON×3 1,897, ASIA×3 1,662.

**The CSV does not log `TriggerMode`**, so on-close fires cannot be separated from backstop fires by column. The proxy used here is **bar phase** — seconds between the row timestamp and its execution-resolution bar boundary, `((minute mod execRes) × 60) + second`. A roll fire lands at phase ≈ 0; a backstop fire lands anywhere.

**Limits of that proxy, stated up front:**

- Live config is `trigger_mode: on_close` with a **60-second** interval backstop. In NY the bar is *also* 60 s, so roll and backstop cadences nearly coincide and NY rows are overwhelmingly phase ≈ 0 regardless. **The res-3 sessions are where the two classes genuinely separate**, and that is where the phase gradient is cleanest.
- The late-phase band is **enriched with backstop fires**, which happen preferentially when the feed has stalled or the tape is quiet. So any cross-band difference in **ATR** or **ROC** mixes a stub effect with a selection effect and should not be read as a stub measurement. **Only the VolumeRatio gradient is used as mechanism evidence below**, because there the mechanism predicts the direction *and* the magnitude.

### 3.2 The fingerprint, confirmed and sized

§7.2 cited "a live CSV row shows VolumeRatio 0.0002 mid-bar" as a corroborating fingerprint. It is not an outlier — it sits in the **10th–25th percentile of bar-aligned NY rows**.

VolumeRatio percentiles by execution resolution and bar phase:

| res | phase band | n | p10 | p25 | **p50** | p75 | p90 | p99 | share ≥ 0.5 |
|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | ≤ 5 s (aligned) | 5,998 | 0.0001 | 0.0009 | **0.0088** | 0.0635 | 0.2806 | 1.9524 | 5.8 % |
| 1 | 6–15 s | 2,021 | 0.0000 | 0.0005 | **0.0110** | 0.1003 | 0.4325 | 3.4523 | 9.1 % |
| 1 | 16 s+ | 1,761 | 0.0000 | 0.0000 | **0.0015** | 0.1507 | 0.7191 | 3.5036 | 13.6 % |
| 3 | ≤ 5 s (aligned) | 1,852 | 0.0000 | 0.0001 | **0.0010** | 0.0131 | 0.0785 | 0.6501 | 1.6 % |
| 3 | 6–15 s | 715 | 0.0000 | 0.0000 | **0.0010** | 0.0245 | 0.1341 | 1.0396 | 3.1 % |
| 3 | 16 s+ | 992 | 0.0000 | 0.0000 | **0.0027** | 0.1512 | 0.6168 | 3.0738 | 13.5 % |

The share of rows reaching VolumeRatio ≥ 0.5 climbs monotonically with bar phase in **both** resolutions (res-1 5.8→9.1→13.6 %; res-3 1.6→3.1→13.5 %), and p90 climbs with it (res-1 0.28→0.43→0.72). That is what a partially-formed bar does: the later in the bar you sample, the more volume has accumulated.

Finer-grained on res-3, where the phase range is a full 180 s — median VolumeRatio against the uniform-volume expectation `phase/180`:

| phase (s) | n | median VR | uniform expectation | ratio |
|---|---:|---:|---:|---:|
| 0–19 | 2,677 | 0.0010 | 0.0556 | 0.02 |
| 20–39 | 433 | 0.0001 | 0.1667 | 0.00 |
| 40–59 | 109 | 0.0009 | 0.2778 | 0.00 |
| 60–79 | 53 | 0.1011 | 0.3889 | 0.26 |
| 80–99 | 47 | 0.1949 | 0.5000 | 0.39 |
| 100–119 | 44 | 0.1053 | 0.6111 | 0.17 |
| 120–139 | 45 | 0.2333 | 0.7222 | 0.32 |
| 140–159 | 50 | 0.3953 | 0.8333 | 0.47 |
| 160–179 | 101 | 0.0048 | 0.9444 | 0.01 |

The 60→159 s climb (0.10 → 0.19 → 0.11 → 0.23 → 0.40) tracks bar phase directly and is the mechanism visible in the data. Two features need honest annotation rather than being quietly dropped:

- **75 % of res-3 rows sit in the 0–59 s bucket** and read ~0.001, well *below* even the uniform-volume expectation for their phase. Intra-bar volume is front/back-loaded rather than uniform, so "uniform" is a crude yardstick — but a 50× shortfall is not explained by that. These rows are seeing an essentially empty bar.
- **The 160–179 s bucket collapses back to 0.0048** instead of approaching 1.0. The most economical reading is that these are roll fires whose timestamp landed a second or two *before* the boundary they were reacting to — i.e. the same population as phase ≈ 0, wrapped. It cannot be confirmed without a `TriggerMode` column.

### 3.3 The consequence: how often the volume signal can vote

The Volume signal needs `VolumeRatio ≥ VolMidThreshold` for a partial vote and `≥ VolHighThreshold` plus ROC/VWAP agreement for a full one ([ScoringEngine_Calculate_Scoring.vb:215](Core/ScoringEngine_Calculate_Scoring.vb:215)). Those thresholds are dynamic but **clamped from below** at 1.5 (mid) and 2.0 (high), then session-multiplied. Using the clamp minimum gives the *lowest possible* threshold, hence an **upper bound** on the fire rate:

| Session | n | VR ≥ mid floor (partial possible) | VR ≥ high floor (full possible) |
|---|---:|---:|---:|
| LONDON | 1,897 | **3.06 %** | 2.79 % |
| NY | 9,780 | **2.07 %** | 1.26 % |
| ASIA | 1,662 | **0.48 %** | 0.30 % |
| **All** | **13,339** | **2.01 %** | **1.36 %** |

And the trader's own stated breakout rule — volume > 3× SMA(9):

| Session | live rows: VR ≥ 3.0 |
|---|---:|
| NY | 0.89 % |
| LONDON | 0.53 % |
| ASIA | 0.18 % |

### 3.4 The counterfactual — the same instrument's closed bars

The historical store (`backtest_data/`, fetched for the synthesizer) holds real **closed** candles for the same instrument and period. Computing VolumeRatio the way the engine computes it — `volume[i] / mean(volume[i−8..i])`, the `CalcVolumeSMA` window including the current bar — over closed bars gives what the engine would see if it read a completed bar:

| Population | closed bars n | **closed-bar p50 VR** | closed-bar VR ≥ mid floor | closed-bar VR ≥ 3.0 |
|---|---:|---:|---:|---:|
| NY hrs, closed 1m | 5,280 | **0.525** | 20.11 % | **8.47 %** |
| LONDON hrs, closed 3m | 855 | **0.653** | 20.35 % | **5.50 %** |
| ASIA hrs, closed 3m | 1,352 | **0.668** | 27.96 % | **7.25 %** |
| All hours, closed 1m | 11,917 | 0.497 | 20.35 % | 8.72 % |

Side by side:

| Population | live p50 VR | closed p50 VR | live ≥ 3.0 | closed ≥ 3.0 | breakout-gate suppression |
|---|---:|---:|---:|---:|---:|
| NY×1 | 0.0081 | 0.525 | 0.89 % | 8.47 % | **≈ 9.5×** |
| LONDON×3 | 0.0017 | 0.653 | 0.53 % | 5.50 % | **≈ 10×** |
| ASIA×3 | 0.0008 | 0.668 | 0.18 % | 7.25 % | **≈ 40×** |

**Caveats on this comparison, which are real but do not close the gap.** The closed-bar set is every bar in the window; the live set is only bars where a run fired, which is a non-random subset. The live book also spans a slightly different range than the store. And the WS feed is known to undercount 3-min closed-bar volume by ~2.5 % (§12 watch item) — a rounding error at this scale. None of these is remotely large enough to account for a **65–800× gap in the median** or a 10–40× gap in gate fire rate. The gap is the forming bar.

### 3.5 Other measurable artifacts

Per the §7.2 brief, other columns were checked for stub fingerprints:

- **ATR** shows a large cross-band difference (res-1 mean 33.42 aligned vs 18.54 late; res-3 38.42 vs 23.45) — but in the **opposite** direction to the stub mechanism, which predicts aligned rows read *lower*. This is the selection confound of §3.1: late-phase rows are enriched with backstop fires from quiet or feed-degraded periods, which are genuinely lower-volatility. **This measurement cannot separate the two effects and is reported as inconclusive, not as evidence.**
- **ROC** shows the same pattern (mean |ROC| 0.1217 aligned vs 0.0693 late on res-1) and carries the same confound. Consistent with §2's expectation that ROC is close-based and largely faithful.
- **RSI** shows essentially no gradient (mean |RSI−50| 12.81 / 11.84 / 12.33 on res-1), which is what §2's table predicts for a close-based indicator.
- **OBV** was not separable from the live CSV alone; the independent evidence is the synthetic↔live comparison in `backtest-overlap-validation-2026-07-30.md` §9.5, where mirroring the stub moved OBVTrend 97.38 % → 71.43 %.

---

## 4. Options — enumerated, not recommended

Four, including doing nothing. **This section deliberately makes no recommendation.** Each entry states the mechanism and the effect on dataset continuity, which is the axis that makes any of these expensive.

### Option A — close-bar-only slice at fire time

Drop the last element from each series before the indicator pass, so every indicator reads the completed bar.

- **Fixes:** the volume class outright — VolumeRatio would move to the §3.4 closed-bar distribution, and the 3× breakout gate would fire ~8.5 % of NY rows instead of ~0.9 %. ATR, OBV and VPFR recover simultaneously.
- **Costs:** every close-based indicator gets **staler** by up to one full bar. EMA/ROC/RSI/BBW/TTM currently read live price; under A they read the last close, so at 59 s into a NY bar the engine would be pricing a minute-old market. That is a real loss on the entry-timing side, and it is the reverse of what v44 §2 wanted from on-close.
- **Continuity: MAXIMAL BREAK.** Every candle-derived column changes on every row. The entire calibration book becomes non-comparable: failure-rate matrices, the §9 band ladder, the v48 OFI baseline, every threshold fitted against post-cutover data, the eval cache, the auto-tweaker's population history. Effectively a fresh dataset epoch with a re-fit of anything volume-sensitive. Also silently re-scopes what "the trader's 3× rule" means, since the thresholds were tuned (v14 S5 raised the dynamic floors "toward trader's 3× breakout rule") against a distribution where the gate almost never fired.
- **Non-obvious side effect:** the on-close *trigger* would become nearly redundant with respect to the data — a run at any phase would compute the same closed-bar values, so on-close would only affect *when* the row is stamped, not what it contains.

### Option B — stub-aware indicator variants

Leave the series alone; teach the affected indicators that the last bar may be partial. Shapes range from narrow (exclude the last bar from `CalcVolumeSMA` + `CurrentVolume`, the way `CalcDonchian` already excludes it) to broad (a `lastBarIsPartial` flag threaded through the volume/range family, with each indicator choosing).

- **Fixes:** whatever it is applied to, and only that. It can take the volume class while leaving the close-based family reading live price — i.e. it is the only option that keeps both properties v44 §2 and §3 each wanted.
- **Costs:** the largest surface area of the four. Every touched indicator needs its own decision, its own fixture, and its own entry in the display/CSV parity chain. It also introduces a per-indicator asymmetry that a future reader has to hold in their head — though note the codebase already has exactly that asymmetry, undocumented, in `CalcDonchian`.
- **Continuity: BREAK, scoped to the indicators touched.** A volume-only variant re-bases VolumeRatio (and anything Pass-2/2c reads from the volume vote) while leaving ROC/RSI/EMA/BBW columns byte-identical. That is a *narrower* break than A, and — unlike A — its blast radius is knowable in advance from the §2 table. The what-if runner and offline matrix would still need a re-walk on the affected columns.
- **Scoping note:** the narrowest useful version is one function. `CalcVolumeSMA` excluding the last bar plus `CurrentVolume` reading `candles(Count − 2)` would move VolumeRatio to the closed-bar distribution and touch nothing else.

### Option C — status quo

Change nothing; treat the forming-bar convention as the specified behaviour it is (§1) and the low volume-vote rate as a known property of the book.

- **Fixes:** nothing.
- **Costs:** the Volume signal stays effectively silent — ~2 % of rows can carry any volume vote at all. It is a PREFERRED indicator in the trader profile and the stated breakout confirmation, so the engine is running with one of its named entry criteria contributing almost nothing. Any future calibration of volume thresholds keeps fitting to a distribution whose location is set by sampling latency rather than by the market. Note also that the signal is **not** neutral-by-absence in the way a disabled indicator would be: the volume vote is one of the inputs Pass 2 uses for cross-category confirmation, so its silence suppresses upgrades elsewhere.
- **Continuity: PERFECT.** No break, no re-fit, no re-walk. The book stays one comparable dataset back to the last real boundary.
- **Worth stating plainly:** status quo is the only option with zero continuity cost, and continuity has been treated as expensive throughout this project's history. That is an argument for it that does not depend on the behaviour being right.

### Option D (not in the §7.2 brief, surfaced by the data) — measure first, decide later

Add `TriggerMode` (and/or the row's bar phase) to the CSV so roll fires and backstop fires are separable by column instead of by the §3.1 proxy, and re-read after a week.

- **Fixes:** nothing directly. It removes the single largest weakness in this document — that ATR/ROC cross-band differences are confounded (§3.5) and cannot currently be attributed.
- **Costs:** a CSV schema rotation (v0.8 → v0.9), which is its own coordination cost, plus a week of waiting.
- **Continuity: NO BREAK to the scoring columns**, but a header rotation is a schema event with its own handling (`AnalysisLogger` rotates on header mismatch, and the pooled-read discipline in `aws-collector-deploy-checklist.md` §4.3b requires both boxes on the same schema before pooling).
- **Why it is listed:** the §3.4 counterfactual is strong enough on the volume question that D is probably unnecessary *for that question*. It is listed because the ATR question is currently unanswerable, and because a ruling on A or B would benefit from knowing the true roll/backstop split rather than inferring it.

---

## 5. What this document does not do

- It does not rule on whether the behaviour is a defect. §1 establishes it is specified; §3 establishes the consequence is large. Those are inputs to a judgment, not the judgment.
- It does not recommend an option, rank them, or estimate implementation effort beyond the scoping notes above.
- It does not touch code, settings, or fixtures. Nothing in this lane changed a `.vb` file, `settings.json`, or the harness.
- It does not re-open the v44 design. On-close as a *trigger* is doing what its spec said and is not in question here.

**Anything acted on from here is a maximal ⚠ and belongs to the Fable/trader seat.**

---

## 6. Reproduction

Every number above comes from two files and no tooling beyond text processing:

- **Live:** the deduped pooled snapshot per `aws-collector-deploy-checklist.md` §4.3b — 13,339 rows, described in `docs/pooled-report-runner-spec-back.md` §4.1. Bar phase = `((minute mod ExecResolution) × 60) + second` from column `Timestamp`; `VolumeRatio` is column 19, `ExecResolution` column 94.
- **Counterfactual:** `backtest_data/candles_1m_2026-07.csv` and `candles_3m_2026-07.csv` (epoch-ms timestamps, `ts,open,high,low,close,volume,cost`), with `VR[i] = volume[i] / mean(volume[i−8..i])` and session hours derived as `int((ts/1000 mod 86400)/3600)`.

Thresholds quoted from `settings.json` v63: `indicators.Volume.dynamic_mid_clamp_min` 1.5, `dynamic_high_clamp_min` 2.0; session multipliers ASIA 0.80/0.85, LONDON 1.00/1.00, NY 1.15/1.10.
