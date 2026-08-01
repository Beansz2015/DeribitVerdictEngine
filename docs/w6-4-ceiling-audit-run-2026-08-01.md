# W6-4 ceiling audit — first run (2026-08-01)

**Run by:** the incoming orchestrator seat, at the trader's direction. **Method:** [`w6-4-ceiling-audit-method-proposal.md`](w6-4-ceiling-audit-method-proposal.md) (APPROVED 2026-07-23, K1–K6 ticked), executed by the shipped `tools/CeilingAudit` runner at defaults (margin ±0.030, bootstrap B=1000 session-hour blocks, seed 42, min-test-days 7).
**Input:** the pooled AWS-preferred book (2026-07-31 dedup ruling), 14,104 rows, span 2026-07-03 16:23 → 2026-07-31 14:32 UTC. The runner's own header calls for exactly this — *"the pooled analysis_log.csv (local + AWS-collector externally concatenated)."*
**Data gate:** "~3–4 weeks of v0.8 rows, early August" — **met** (4 weeks).

> ## VERDICT: INCONCLUSIVE. The queue does not unlock, and §4's instruction is explicit — *"re-run at the next book doubling. No spend meanwhile."*

---

## 1. Result

| Population | Decisive | N labelled | N test | Test days | Baseline AUC → Challenger | ΔAUC | 95% CI | Verdict |
|---|---|---:|---:|---:|---|---:|---|---|
| **NY×1** | **yes** | 1,689 | 484 | 6.99 | 0.5407 → 0.5128 | **−0.0291** | [−0.1971, +0.1239] | **INCONCLUSIVE** |
| LONDON×3 | indicative | 567 | 184 | 7.00 | 0.5489 → 0.4871 | −0.0526 | [−0.1930, +0.1041] | INCONCLUSIVE |
| ASIA×3 | indicative | 456 | 220 | 4.28 | 0.5671 → 0.4469 | −0.1140 | [−0.3004, +0.0917] | INCONCLUSIVE |

**The challenger loses on every population.** An L2 logistic with access to every logged feature and free rein to weight them ranks outcomes *worse* than the engine's own dominant-effective-score. That is not evidence of headroom — if anything it is weak evidence against it — but **the CIs are far too wide to conclude either way**, which is what INCONCLUSIVE means here.

**The CI width is the real finding.** NY's is [−0.197, +0.124]: a ±0.16 band around a ±0.030 margin. At 484 test rows over 7 days the instrument cannot resolve the question it was built for. The §4 protocol anticipated this and the answer is to wait for more book, not to re-analyse this one.

**What stays parked, per the method's own gating:** W6-5 (B1), the D3–D6 backlog refinements (5m RSI-div · Donchian×BBW · smart OBV · MFI), and any W6-7 Tier-C spend. **No spend meanwhile** is a decisive outcome even though the statistic is not.

---

## 2. What the run says beyond its verdict

**Baseline AUCs are 0.54 / 0.55 / 0.57.** The engine's own score ranks directional outcomes only modestly better than chance on the placed-geometry yardstick. Stated flatly and with its caveat: this is a *ranking* metric computed over directional rows only, on touch-based barriers, so it is not a claim about trading performance — but it is the number the ceiling question is asked against, and it is low.

**Absorption has never fired on a single evaluated row.** `AbsorptionSignal (any)` shows **N=0** on all three populations. That is an independent corroboration of the Path B story (0 flags / 778 episode rows) from a different instrument and a different code path — the mechanism-revision spec is arguing about something that has genuinely never produced an observation.

**Structural placement is not predictive of success — and on ASIA it is anti-predictive.** Univariate test AUC for `TargetCapReason (structural fired)`: **NY 0.4683** (n=199) · LONDON 0.5000 (n=62) · **ASIA 0.3221** (n=107). All at or below chance. This is an independent corroboration of the W6-1 finding from the opposite direction: W6-1 showed the *stop* side is de-facto ATR; this shows the *target* side's structural rung is not earning its slot either. It is univariate and small-n, so it is a flag rather than a verdict — but it points the same way as the v56 geometry work and the D2-v2 question, and all three should be read together at the geometry session.

**First outcome-linked read on ASIA aggressor velocity — and it is weak.** The informational side-column (ASIA has AggrVel un-armed, so its fields sit there rather than in the design matrix): `AggrVelBurstRatio` univariate test AUC **0.5179** (n=217) · `AggrVelNet` **0.4654** (n=217) · `AggrVelSignal` 0.5127 (n=45). Essentially no demonstrated edge. **This bears directly on D3** — see §3.

---

## 3. Consequence for D3 (arming ASIA at T=5.5)

The [ASIA derivation](asia-burst-threshold-derivation-2026-08-01.md) §4 recorded that nothing was joined to outcomes and that whether the engaged rows are the profitable ones was unmeasured. **This run measured it incidentally, and the answer is ~0.52 AUC.**

That does not refute arming:
- NY and LONDON were both armed on **distributional** grounds too, and LONDON's post-ship watch **passed** on 2026-07-27.
- A univariate AUC on a 220-row test block is weak evidence in either direction.
- The modifier is a ±1 re-weight on ~10% of rows, not a vote — its effect size is small by construction, so a near-0.5 univariate reading is not surprising and would not be expected to show up strongly.

But it is now **the only outcome-linked evidence that exists** on this knob for this session, and it is neutral at best. Honest framing for the D-table: *arming ASIA restores the stated design intent and matches an already-proven sibling session; it is not supported by outcome evidence, because the outcome evidence available is ~0.52.*

---

## 3a. Could the 6-month store have helped? No — and it cannot later either

Asked by the trader 2026-08-01. **The 6-month candle+funding store is irrelevant to W6-4, and this is structural rather than a matter of timing.**

The audit's unit of observation is **a logged engine decision with its full feature vector** — tier, effective scores, and ~30 indicator outputs — joined to a forward-walked outcome. Those rows exist only in `analysis_log.csv`, and only from **2026-07-03** when the v0.8 schema began. Candles cannot reconstruct them: the store holds OHLCV and funding, not what the engine *decided* or what it saw when it decided. The one thing the store could have supplied — 1m bars for the label walk — the runner already fetches directly from Deribit (40,256 bars this run), so there was nothing to gain.

**The obvious follow-up — "then synthesize the rows" — does not work either, and the reason is specific.** That is what the [backtest synthesizer](backtest-synthesizer-proposal.md) exists for, but its clearance is class-by-class ([`backtest-overlap-validation-2026-07-30.md`](backtest-overlap-validation-2026-07-30.md) §10.4): VWAP now **fully cleared** at 100.00%, `VolumeRatio` **advisory** at 65.00%, and **ATR / ADX / RSI still in the *do not use* band at 46.6 / 49.8 / 43.0 %**.

W6-4's design matrix consumes exactly those three. From this run's own coefficient tables: `ADX` (NY −0.2726, ASIA −0.1756), `ATR` (ASIA −0.2441), `RSIDivergence` (NY −0.1662). **Feeding synthesized rows into the ceiling audit would inject unfaithful values into the very features being tested** — and a ceiling audit run on a matrix it cannot trust is worse than no audit, because it produces a number.

**So "re-run at the next book doubling" is not a scheduling preference — it is the only available path.** The store genuinely unblocked other work (the TTM, OBV, volume and swing derivations are all candle-derived and ran on it), which is precisely why the distinction is worth recording: **the dividing line is what a study consumes, and W6-4 consumes verdicts and outcomes.**

## 4. Maintenance items found in passing

- **The runner's expected-settings-version constant is stale:** `WARN: settings.json version = 64 (expected 59 at build time)`. The binary is current (rebuilt 2026-07-31) but the constant was not updated across v59→v64. Someone should confirm nothing the audit reads changed in that span and then bump the constant, or the warning becomes background noise that hides a real mismatch later.
- **34 rows excluded as burst-cadence** (median gap < 45 s) under InstanceId prefix `8706ebae`, and 34 excluded by the InstanceId rule — almost certainly the same 34, counted twice in the load table. Cosmetic reporting nit, not a data problem.

---

## 5. What I did not verify

- **No re-run at a different margin or seed.** Defaults only, as the method specifies. Sweeping either to obtain a different verdict would be exactly the multiple-comparisons failure the method's §4 guards against.
- **The challenger's coefficient tables are not interpreted here.** They are printed in the report and some are suggestive (NY's largest terms are funding-crowding, OBV divergence and MTF EMA alignment), but a losing challenger's coefficients are not evidence about the engine, and reading them as such is how a null result becomes a false lead.
- **Nothing about execution.** Same touch-based, no-slippage caveat that binds every offline surface in this project.
- ~~**The next re-run point.** §4 says "next book doubling" — from 2,712 eligible rows that is roughly late September at current accrual, and it is worth confirming that estimate before scheduling anything against it.~~
  **CONFIRMED AND CORRECTED 2026-08-02 — it is ~4 weeks, not ~2 months.** The "late September" figure was a hand-wave and it was wrong in the pessimistic direction. Measured accrual on the current two-box topology is **12.4 pooled weekday STRONG/weekday** across the 8 weekdays since AWS came up 2026-07-22; the whole-book rate of 10.1 understates it because it averages in the single-box era. **ETA for a doubling is ~2026-08-30**, and the F1/Kelly watch lands in the same window on its own basis — **bundle the two re-runs into one pooled freeze and one session.** Recorded because the original line explicitly asked for the estimate to be checked before anything was scheduled against it.
