# Session-Timeframe Resolution (v36) — Spec-Back (finalization, for the coordinating spec-writer)

**Date:** 2026-06-15
**Spec-writer seat:** Opus 4.8 (finalized the threshold profile + coordination per the brief).
**Brief:** [`session-timeframe-resolution-spec-writer-brief.md`](session-timeframe-resolution-spec-writer-brief.md) | **Approval artifact:** [`session-timeframe-resolution-proposal.md`](session-timeframe-resolution-proposal.md) | **Build spec produced:** [`session-timeframe-resolution-implementer-handoff.md`](session-timeframe-resolution-implementer-handoff.md)
**Status:** **FINALIZED (design).** Hand-off written; threshold profile **trader re-confirmed 2026-06-15** (2.1× ROC seed). **No code applied, no settings bump** — Phase-1 implementation is a separate seat. This closes the coordination loop on the brief.

**What you're reading this for:** I diverged from the approved proposal on **one material point** (the magnitude-scale list dropped from 4 keys to 2), tightened **one coordination call past the brief** (§4.1 sequencing is a hard ordering, not "orthogonal"), and routed **one knob back to the trader** (the ROC seed factor). Everything else in brief §5/§4 resolved as the brief anticipated. The divergence and the tightening are where I want your eyes.

---

## 0. Result summary — every brief §5 question + the key §4 calls

| Decision | Brief / proposal said | Finalized | Basis | Confidence |
|---|---|---|---|---|
| **Magnitude-scale set** | proposal §4: **4 keys** ×2.1 (ROC mag, ROC slope-Δ, MicroCVD floor, CVD slope-min) | **2 keys** (ROC mag 0.1→0.21, ROC slope-Δ 0.05→0.105); CVD/MicroCVD **unchanged** | CVD/MicroCVD read the **fixed trade stream**, not candles — resolution-independent (§1) | **High** (verify — contradicts approved proposal) |
| §5 Q1 config taxonomy | extend `sessions[]` vs new block (recommended: extend) | **extend `session_volume.sessions[].execution_resolution`** | DRY — one session-bucket definition; a 2nd source drifts (the `ResolveSessionLabel` off-by-one is exactly that) | High |
| §5 Q2 `resolution_profiles` shape | sketch only | top-level map keyed by `"1"`/`"3"`; **nullable** override fields; `"1"` empty | "absent ⇒ inherit global" must be unambiguous; `0.0` default would be a real override | High |
| §5 Q3 bar-count windows | default keep-count, "call it explicitly" | **keep-count** (ATR 7, RSI 9, Vol 9, BBW 20, Donchian 20, EMA 9·21·50) | a coherent 3-min chart *is* standard 3-min indicators; rescaling makes 3m mimic 1m (§2) | High |
| §5 Q4 Phase-1 seed | minimal vs full | **minimal — the 2 ROC keys only** | only ROC is candle-magnitude-gated; the rest self-scale / are invariant / are trade-stream | High |
| §5 Q5 A14 fixtures | enumerate | **8 fixtures** incl. NY byte-identical guard + hour-7→ASIA boundary (hand-off §8) | — | High |
| §5 Q6 `session_volume` in profile | flag the interaction | **out of the profile**, stays 1-min-calibrated | VolumeRatio ≈ scale-invariant (ratio of same-res quantities); avoid coupling two calibrations (§2) | Med-High |
| §4.1 first-fire sequencing | "orthogonal… don't block either way" | **hard ordering: v35 NY/1-min first fire BEFORE v36 ships 3-min rows** | tweaker slicer is chronological + resolution-blind (§5) | High |
| §4.5 ROC seed factor | 2.1× | **2.1×** (trader-confirmed) | the measured 1→3min ATR ratio, used as a proxy | factor Medium; as-default High |

---

## 1. The material divergence — why 2 seeded keys, not 4

The proposal §4 is internally contradictory: one bullet lists CVD `slope_min_usd` and MicroCVD `accel_threshold` among the "magnitude-type, scale ×2.1" keys; another says "trade-count windows (CVD/MicroCVD/TFI take from the trade stream, not candles): unaffected by candle resolution." Both cannot hold. I read the indicator code to adjudicate, and it is decisive:

- **`CalcCVD`** ([Core/Indicators_OrderFlow.vb:167](Core/Indicators_OrderFlow.vb:167)) segments the **entire `trades` list** — which is `GetRecentTradesAsync(500)`, a fixed 500-trade fetch ([MainForm_Analysis.vb:62](UI/MainForm_Analysis.vb:62)) — into thirds and gates `weightedSlope` against `slope_min_usd`. The only place candles enter is the divergence price-gate ([:210-213](Core/Indicators_OrderFlow.vb:210)). So the USD magnitude the threshold gates is computed over **500 trades, identical whether the execution chart is 1m or 3m.**
- **`CalcMicroCVD`** ([Core/Indicators_OrderFlow.vb:269](Core/Indicators_OrderFlow.vb:269)) takes **no candle argument at all** — it's `LastN(trades, 50)`. Its `accel_threshold` (and the dynamic `max(totalUsd×pct, floor×pct)`) gate USD flow over 50 trades — **resolution-independent.**

**Why scaling them isn't just unnecessary but actively wrong:** the trade stream doesn't change when the execution candle resolution changes. Multiplying `slope_min_usd` 12 000→25 000 and the MicroCVD floor 10 000→21 000 would raise the bar on an unchanged quantity, making CVD and MicroCVD **fire less often in Asia/London** — i.e. degrading two PREFERRED order-flow signals in exactly the sessions v36 exists to rescue. That's a net loss, not a calibration.

**Contrast — ROC genuinely scales.** `CalcROCSeries` ([Core/Indicators_Momentum.vb:273](Core/Indicators_Momentum.vb:273)) reads the execution candles; ROC(9) on 3-min spans 27 min of return vs 9 min, so |ROC| runs ~2.1× larger and its magnitude gate must move with it or it never gates anything.

**Sanity-check:** confirm the trade-stream reading. My claim is that `GetRecentTradesAsync(500)` returns a count-bounded (not time-bounded) window, so CVD/MicroCVD magnitudes are invariant to execution resolution. If the trade fetch were ever made *time-windowed* (e.g. "last N minutes of trades") this would flip and those thresholds would scale — but today it is fixed-count, so they don't.

---

## 2. Why the rest stayed at 1-min (the "not 3-min" rationale)

Four distinct reasons, not one — and each is a different kind of "doesn't need scaling":

1. **DynamicNorms is self-scaling — no seed possible or needed.** Volume thresholds (`volMean+kσ`, [DynamicNorms.vb:53](DynamicNorms.vb:53)), the VWAP-dev threshold ([:91](DynamicNorms.vb:91)), and `ATRRef` ([:130](DynamicNorms.vb:130)) are all **computed from the candle window each run**. Feed them 3-min candles and they recompute against 3-min distributions automatically. The only change required is feeding `Compute(candlesExec, …)` instead of `candles1m`. Seeding these would be double-correction.

2. **Bar-count windows — keep the count (coherent-timeframe argument).** ATR(7), RSI(9), Vol-SMA(9), BBW(20), Donchian(20), EMA(9/21/50) define the *character* of the timeframe. A 3-min RSI(9) is the RSI any trader reads on a 3-min chart. Rescaling to RSI(27) to preserve 1-min wall-clock would make the 3-min chart behave like a 1-min chart — defeating the entire Path-B premise ("the engine analyzes the timeframe it advises on"). The wall-clock memory of the DynamicNorms baselines lengthens (100 bars = 5h at 3m vs 100min), which is acceptable and called out in the hand-off.

3. **Ratio / bounded signals — scale-invariant.** RSI zones (RSI is 0–100), VWAP-dev %, VolumeRatio (current/SMA, same resolution top and bottom), TFI (`(buy−sell)/total ∈ [−1,1]`), EMA alignment (ordering). None carry an absolute-magnitude threshold, so resolution doesn't move them.

4. **Secondary candle-magnitude keys — deferred to Phase 2, and the un-scaled direction is the *conservative* one.** `TTM.flat_threshold` and the CVD/RSI `divergence_price_gate`s are technically candle-magnitude (they'd scale by the same logic as ROC). I left them at 1-min for Phase 1 to keep the seeded set minimal and auditable, and because the bias is safe: leaving the divergence price-gates un-scaled makes adverse-divergence detection *more eager* on 3-min (a penalty/veto firing more readily = fewer marginal directional calls), which aligns with the trader's low false-positive tolerance. **Sanity-check:** is excluding `TTM.flat_threshold` from the seed defensible, or should it scale ×2.1 alongside ROC for consistency? My lean: defer — TTM is squeeze-detection (trader profile: "NOT used for OB/OS"), lower-impact than ROC, and minimizing the seed set is worth more than cosmetic consistency. But it's the weakest of my "keep at 1-min" calls.

5. **`session_volume` — out of the profile (§5 Q6).** Each session has exactly one resolution (3/3/1), so its multiplier only ever applies to its own resolution's volume — there's no pooling *within* `session_volume`. And because VolumeRatio is ≈ scale-invariant, the v34 ASIA multiplier (1.10/1.05) transfers approximately. Folding it into the resolution profile would couple it to the already-open weekday-ASIA re-verify (which was itself weekend-confounded in v34). Cleaner: leave it, and the existing re-verify simply **becomes a 3-min weekday re-verify** once ≥50 such rows exist. Flagged, not silently inherited (the brief's §4.2 ask).

---

## 3. The 2.1× seed — routed to the trader, confirmed, with an honest hole

This was the one genuinely trader-facing knob (it changes verdict behaviour in Asia/London), so per the brief's "re-confirm the final threshold profile before code" I put it to him: 2.1× (data-backed default) / conservative ~2.5× / no scaling. **He chose 2.1×.**

The honest hole I flagged to him and carry here: **2.1× is the measured 1→3min *ATR* ratio used as a proxy for *ROC* scaling — they're not the same quantity.** ATR(7) measures single-bar range; ROC(9) measures return over a 9-bar (now 3×-wall-clock) horizon. ROC's true scaling is bracketed ~1.7× (pure-noise √3) to ~3× (pure trend). 2.1× sits inside that bracket and is the only number the Phase-0 study actually produced, so it's the best available seed — but both ROC keys are the **Phase-2 re-baseline priority**, and Phase-1 Asia/London verdicts are provisional by construction. **Sanity-check:** if you'd rather not seed ROC off the ATR proxy at all, the alternative is √3≈1.73× (the noise-floor estimate) — but I judged the single measured number the more defensible seed than a theoretical one, and the trader agreed.

---

## 4. The coordination call I tightened past the brief — §4.1 sequencing

The brief framed v35-first-fire vs v36 as "more orthogonal than they look… don't block v36 on the first fire or vice-versa." I think that's **too loose**, and the hand-off §6.1 states it as a **hard ordering**:

The auto-tweaker's fixed-window slicer walks **disjoint chronological** row slices (`allRows[LastEvaluatedRowIndex .. +WindowSize]`, per architecture.md) and is **resolution-blind until Phase 2**. The moment v36 Phase 1 ships and 3-min Asia/London rows start interleaving into `analysis_log.csv`, no later window can isolate a pure-1-min population — a slice straddles resolutions and the tweaker pools them. So the v35 supervised first-fire dry-run (which needs a clean 1-min window) **must consume the clean history before v36 adds 3-min rows.** This matches the constraint already recorded in the project memory ("run NY/1-min dry-run BEFORE v36 Phase 1 ships 3-min data"). Order: **v35 first-fire dry-run → then v36 Phase 1 → then tweaker resolution-awareness gates any later fire.** **Sanity-check:** agree this is a precondition, not a preference? If the first fire slips, v36 should wait — or we accept the first fire runs later on resolution-filtered data (which needs §6.3's filter built first).

---

## 5. Coordination items closed as the brief expected

- **§4.2 v34 re-verify reconciliation** — resolved by keeping `session_volume` 1-min/out-of-profile (§2.5); the re-verify becomes the 3-min weekday re-verify. No double-run.
- **§4.3 schema batching** — CSV v0.6→v0.7 (`ExecResolution` + the v34-flagged `weightedSlope`), eval cache v3→v4 (`ExecResolution`), settings v35→v36. Spec C SC/TOTAL parity: batch into v0.7 only if landing concurrently, else don't block.
- **§4.4 auto-tweaker resolution-awareness** — specced as a Phase-2 precondition: filter the failure-rate matrix + rows by `(session × resolution)`; `execution_resolution` added to PromptBuilder HARD CONSTRAINT 11's exclusion list (it's a strategy lever, never a tweaker proposal).
- **§4.6 mandatory details** — session resolver reuses the **engine** bucket via a shared `MatchSessionBucket` extracted from `ApplySessionVolume` (guarantees the resolution boundary == gate/eval boundary, bypassing the display `ResolveSessionLabel` `<7` off-by-one); `IsFresh` takes the execution resolution; fetch keeps 1m (eval walk + cache) and adds `candlesExec`; ROC magnitude override threads via `r.ExecResolution`; display-parity obligation flagged if an `EXEC 3m` tag is surfaced.

---

## 6. Plumbing decisions worth your awareness (host-agnostic rule)

- **`r.ExecResolution` carries the resolution into scoring** rather than a new `Calculate` parameter or a cfg mutation — `r` is already passed, so the ROC magnitude override resolves at the read sites via a pure `ResolveRocMagnitude(cfg, r.ExecResolution)`. No singleton race, no hot-reload hazard.
- **Resolver lives in `Core/`** (e.g. `Core/ExecutionResolution.vb`), WinForms-free, per the Linux-port rule. Fetch + session detection stay UI-side in `MainForm_Analysis` (allowed).
- **`MatchSessionBucket` is the single bucket-matcher**, shared by `ApplySessionVolume` and the resolver — the DRY win that also satisfies §4.6's "must equal the gate/eval boundary."

---

## 7. Docs touched (no code, no settings bump)

- **Created** `docs/session-timeframe-resolution-implementer-handoff.md` — the build spec (config taxonomy + exact JSON/POCO + resolver + fetch rewiring + ROC override sites + data layer + A14 + commit checklist).
- **Created** this spec-back.
- **Edited** `docs/session-timeframe-resolution-proposal.md` §4 — inserted a correction note (the 4→2 narrowing) pointing to the hand-off, so the approved artifact doesn't strand a future reader on the stale 4-key list.
- **Memory** — recorded v36 state + the 4→2 finding + the §6.1 ordering constraint for session continuity.
- **NOT touched yet (rides the implementing commit):** `settings.json` (v35→v36 bump + change_log + the `resolution_profiles` block + `execution_resolution` keys), `EngineSettings.vb` POCO, `DeribitIndicatorProject.md` §15/§6/§12. Listed in the hand-off §10 checklist for the implementer.

---

## 8. Sanity-check asks (condensed)

If you read nothing else, these four:

1. **The 4→2 correction** — confirm CVD/MicroCVD are fully candle-independent (fixed-count trade fetch, not time-windowed). This contradicts the approved proposal, so it's the one to verify. *My lean:* certain on the code; the only way it flips is if the trade fetch becomes time-windowed.
2. **Bar-count keep vs rescale** — agree that "the whole stack moves to 3-min" means standard 3-min indicators (keep counts), not wall-clock-preserving rescales? *My lean:* keep-count is the only reading consistent with Path B.
3. **`TTM.flat_threshold` excluded from the seed** — defer to Phase 2 (my call) or scale ×2.1 with ROC for consistency? *My lean:* defer — weakest of my keep-at-1-min calls, but minimal-seed wins.
4. **§4.1 as a hard ordering** — agree the v35 first fire is a *precondition* to v36 shipping, not an independent track? *My lean:* yes — the chronological resolution-blind slicer forces it.

*Design finalized; awaiting your review + Phase-1 implementer seat.*
