# W6-4 Offline Ceiling Audit — Method · Proposal

**Date:** 2026-07-23 · **Status:** APPROVED — **K1–K6 ALL TICKED 2026-07-23, BUILD-AUTHORIZED. Implementer model: Opus, and use the HIGHEST effort tier available (Opus Extra/Max) — trader-directed, this instrument gates five parked items.** §2 amended at tick time (trader Q): the decision-rule feature set = SCORED inputs only; logged-but-unscored signals demoted to an informational side-column · **Type:** measurement INSTRUMENT — offline, one-off (re-runnable), zero scoring impact, no ⚠ boundary. **The model never ships and never scores live** (interpretability principle, roadmap W6-4: "an INSTRUMENT, never a live scorer").
**Purpose (roadmap W6-4):** quantify how much predictive signal the additive +1/−1 pipeline leaves unharvested in its own inputs. **Model ≈ pipeline ⇒ declare the system ceiling reached and STOP spending on combination** (closes W6-5/B1 and the D3/D4/D5/D6 parked family honestly; blocks any W6-7 Tier-C spend). **Model ≫ pipeline ⇒ the delta IS the B1 prize, measured** — W6-5 proceeds with the model's top signals as its evidence base.
**Data gate:** ~3–4 weeks of v0.8 rows (early Aug; the AWS supplementary collector accelerates LONDON/ASIA depth). Build can precede the gate; the RUN waits for it.

## 1. Population & label

- **Rows:** v0.8 evaluable directional rows (STRONG+MEDIUM+WEAK — the model may find WEAK carries signal; the pipeline's tier ladder is itself under F1 review). Weekday-only; burst instances (`8706ebae`, `f90f59c4`-class sub-cadence stretches) excluded; NO_DATA excluded (F4). Per population — **NY×1 is the decision population** (depth); LONDON×3/ASIA×3 reported indicative-only.
- **Label:** placed-vs-placed barrier SUCCESS at the population's tracker horizon (the one truth the whole eval stack now measures). Binary; AMBIGUOUS/expiry = failure per the standing conservative rule.

## 2. Models compared

- **Baseline = the pipeline itself:** the engine's own effective score (dominant-side effective score / regimeMax) as a single ranking feature → AUC/Brier. This IS the pipeline's harvest, measured on the same rows.
- **Challenger = L2-regularized logistic** on the pipeline's OWN **SCORED** inputs as logged (trader clarification at tick, 2026-07-23: scoring inputs ONLY — everything that votes or modifies in Step 2 / Pass 2b / Pass 2c / Step 3/3b): per-indicator signal states (one-hot: ROC/RSI/RSI-div/DMI/Volume/VWAP/BBW-TTM/EMA/funding+momentum/OI/OI×CVD/OFI/Liq/CVD/MicroCVD/TFI(+burst where armed)/Donchian/OBV/VPFR/structure) + the key logged numerics behind those votes (ATR, VolumeRatio, ADX, VWAPDevPct, SpreadBps, OFIRatio) + regime + session-hour. **Display-only surfaces are EXCLUDED** (VerdictContext, Kelly, exit-guard/TAPE metrics). **Logged-but-unscored signals (Absorption*, AggrVel on un-armed sessions) go in an INFORMATIONAL side-column only — reported coefficients, never in the §4 decision delta** (#6's activation has its own dedicated evidence gate and must not be double-judged here). **No feature the pipeline cannot see** — the question is combination, not new information.
- **Optional nonlinearity probe** (only if the logistic delta is ambiguous): depth-2 interaction terms on the top-|coef| features — still linear machinery, still dep-free. Full boosting is OUT of v1 (adds a dependency for a question the interactions answer well enough).

## 3. Validity discipline (the part that makes the number honest)

- **Walk-forward split only** — train on the earlier block, test on the later; NEVER random k-fold (rows are heavily autocorrelated; shuffling leaks regime). Split point chosen so the test block spans ≥1 full week incl. all sessions.
- **Regularization strength chosen on the train block only** (small internal walk-forward), reported on test only.
- **Uncertainty: block bootstrap** over session-hour blocks (respects autocorrelation) → CI on the AUC delta.
- **Report Brier + AUC + lift at the actionable operating point** (the fraction of rows the pipeline actually trades — STRONG+MEDIUM share), not AUC alone: a model that only reshuffles sub-threshold rows has no harvestable prize.

## 4. Decision rule (pre-committed, so the answer can't be argued after the fact)

- **ΔAUC (model − pipeline) test-block, with block-bootstrap CI:**
  - CI upper bound < **+0.03** ⇒ **CEILING DECLARED** — combination spend stops; W6-5/B1 + D3–D6 close as "no measured headroom"; W6-7 Tier-C stays refused.
  - CI lower bound > **+0.03** ⇒ **B1 prize measured** — W6-5 may spec, scoped to the model's top-weighted signals/interactions.
  - CI straddles ⇒ inconclusive — re-run at the next book doubling; no spend meanwhile.
- The ±0.03 margin is proposed, trader-adjustable at K4 (rationale: below it, the live win-rate gain at n≈current book is within noise of zero).

## 5. Tooling

Dep-free standalone console **`tools/CeilingAudit/`** (net8.0, zero WinForms, own .vbproj per the root-glob rule): CSV loader (reuse `ForwardWindowJoiner` parsing conventions), hand-rolled standardized L2 logistic (gradient descent — ~200 lines, deterministic seed), walk-forward + block bootstrap, markdown report writer (population tables + coefficient table + the §4 verdict line). Joins the verify-gate build set (the F10 lesson). **Opus, medium**; fixtures: loss decreases monotonically on a synthetic separable set, AUC=0.5 on label-shuffled data (leakage canary), split respects chronology.

## 6. K-table

| # | Decision | Recommendation |
|---|---|---|
| **K1** | Feature set | §2 as written — logged pipeline inputs only, no external features |
| **K2** | Population/label | §1 — NY×1 decisive, WEAK included, placed-vs-placed success at horizon |
| **K3** | Validity | Walk-forward + block bootstrap + train-only tuning (§3) |
| **K4** | Ceiling margin | ΔAUC ±0.03 with the §4 three-way rule |
| **K5** | Tooling | Dep-free console per §5; report doc = the deliverable |
| **K6** | Slot | Build any gap (Opus); RUN at the data gate (~early Aug) — pooled local+AWS book if the AWS collector is live by then |
