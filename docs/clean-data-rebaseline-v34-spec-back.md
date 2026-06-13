# Clean-Data Re-Baseline v34 — Interim Spec-Back (for sanity check)

**Date:** 2026-06-13
**Analyst/implementer:** Opus 4.8 @ High (budget path per brief; the Fable-@-Extra reviewer seat was not used)
**Brief:** `docs/clean-data-rebaseline-v34-brief.md` | Proposal authored + applied: `docs/clean-data-rebaseline-v34-proposal.md` | Methodology template: `clean-data-rebaseline-v33-proposal.md`
**Status:** **APPLIED + PUSHED** (settings v33→v34, commit `61b4532`; §12 WATCHING follow-up `6d8441f`). `dotnet build` 0/0, JSON validated, bin-copy propagated. **Interim** — the data verdict (first-N-row checks) and the supervised first fire are still pending. This document is for a reasoning/decision sanity-check *before* those land, not a closeout.

**Sanity-check outcome (2026-06-13):** spec-writer review **PASSED** — all five flagged calls defensible, no reverts. Two revisions folded: CVD guard made two-sided (§2.4, §5, §6) and the offline volume re-fetch bundled into the weekday-ASIA re-verify (§3, §6). One review finding — the barrier metric is empirically **ATR-confounded**, which *vindicates* call 1 — changes downstream sequencing: the auto-tweaker first fire now waits for the **v35 de-confound pair**, not just weekday data. Full detail in **§7**.

**Why an interim spec-back:** I was both proposal-author and implementer here (brief → proposal → apply), so there was no independent review seat in the loop. Four of the calls below either **diverge from the brief's framing** or **extend it with findings the brief didn't have**. Those are the ones I want a second pair of eyes on before the live data starts being read against them.

---

## 0. Result summary

| Knob | Brief said | Applied | Basis | My confidence |
|---|---|---|---|---|
| `session_volume` ASIA | item A: "raise toward/above 1.0" | **0.80/0.85 → 1.10/1.05** | trade-rate 63.1% + RANGE_BOUND 27.5% (both highest) + ASIA VolumeRatio tail fatter than NY (p90 2.01 vs 1.37) + clamp-floor mechanics | **Direction high, magnitude LOW** |
| `funding.momentum_threshold` | item B: "lower to ~5e-8" | **1e-5 → 5e-8** | FLAT 975/975; 3-sample Δ nonzero on only 4.2% of rows | High (but low signal value) |
| `CVD.slope_pct_of_value` | "re-confirm the v33 three" | **0.05 → 0.10 (changed)** | CVDSlope FLAT still 5.5% < v33's 10% floor → v33 pre-commitment fired | **Medium (weightedSlope unlogged)** |
| `spread.wide_threshold_bps` | item C: "accept REST-dead" | KEEP 5.0 | 0/975 rows ≥ 5.0 | High |
| OBV 18.0 / MicroCVD 0.30 / vol clamps / RSI / OFI / funding bands / verdict pcts / ATR 2.0/1.2 | "re-confirm / review only if stuck" | **KEEP** | re-confirmed on the multi-session sample (§1) | High |
| `OI.change_threshold_pct` | (not in brief scope) | KEEP → **WATCHING** | OISignal 95% NEUTRAL, borderline-stuck | Deliberately held |

Commits (both pushed to `origin/master`): `61b4532` settings v34 + proposal doc + handover §15/§6; `6d8441f` §12 WATCHING (weekday-ASIA re-verify + first-fire sequencing + marked the post-correctness re-baseline row RESOLVED).

Sample: 975 rows, 3 days (06-11 Thu / 06-12 Fri / 06-13 Sat), ASIA 233 / LONDON 254 / NY 488; regimes TRENDING_UP 642, RANGE_BOUND 200, TRANSITIONAL 108 (all ≥50), TRENDING_DOWN 25 (thin). CalibrationReport READY confirmed.

---

## 1. What matched the brief exactly (low-risk, no second opinion needed)

- **Item B (funding momentum).** Confirmed FLAT on 975/975; root cause is funding stickiness (15 distinct rates, ±1.1e-5), not just the threshold. Read `CalcFundingMomentum` (`Indicators_OrderFlow.vb:362`) — it's `rate[now] − rate[now−window]` vs `±threshold`, so my 3-sample proxy is faithful. 5e-8 makes it live but hard-capped at ~4% fire-rate (the 41 rows with any nonzero 3-sample Δ). Applied as specced; flagged low-value in the change_log, do not over-invest.
- **Item C (spread).** SpreadBps 0.0778–0.0803, 23 distinct values, 0 rows ≥ 5.0. Always TIGHT (`CalcSpread:411`). Rejected the brief's option (ii) (tighten to ~0.15) — max observed is 0.0803, so a 2-tick threshold would catch 0 rows in this sample. Accept REST-dead, revisit post-WS. No change.
- **Step 0 gate, market-scale sanity (RSI/OFI/funding bands/verdict pcts), ATR multipliers, auto-tweaker sequencing** — all as the brief prescribed; numbers in proposal §3.8–3.10. NO-TRADE rate orders correctly by regime quality (TRENDING_UP 32% → RANGE_BOUND 72%); adverse-hit 3.4% re-confirms v33 §3.9 across all three sessions.

---

## 2. Divergences & judgement calls — **flagged for sanity-check**

These are the calls where I want the spec writer to push back if the reasoning is thin.

1. **Item A reframed from "structural underperformance" → "selectivity fix."** The brief asserts Asia has "structurally lower edge." The barrier metric **contradicts that**: ASIA barrier-SUCCESS is the *highest* of the three (73.7% vs NY 61.3%), with the *lowest* expiry (22.4%). I read that as ATR-confounded — low Asia ATR → tiny 0.3×ATR barriers → easily tagged — not as genuine Asia edge, and concluded the metric simply *can't* adjudicate cross-session edge with ATR-scaled barriers. So I kept the multiplier change but recast its justification as **selectivity / false-positive discipline** (Asia over-trades chop), not "stop the losses." **Sanity-check:** is the ATR-confound reading right, or is it possible Asia genuinely tags favourable faster for a real (tradeable) reason, in which case raising the bar there costs more than it saves?

2. **Weekday/weekend confound — surfaced, not in the brief (trader-flagged, I verified).** All 233 ASIA rows are **Saturday 06-13** (100% weekend); LONDON is 252/254 Saturday; **NY is 100% weekday** (Thu/Fri). So the entire ASIA-vs-NY comparison underlying item A is weekday/weekend-confounded, and weekday Asia (UTC 0–7 Mon–Fri) is **absent from the sample**. I argued the over-trade *direction* is robust (0.80/0.85 lowers the volume bar on any day) but the *magnitude* is weekend-set, and queued a manual weekday-ASIA re-verify (§12 WATCHING, `6d8441f`). **Sanity-check:** does a 100%-weekend ASIA sample undermine the calibration enough that the conservative call was to **hold** the session_volume change (or apply only the neutral 1.00/1.00) until weekday data exists — rather than apply 1.10/1.05 now and re-verify later?

3. **ASIA value 1.10/1.05 (above the brief's "toward/above 1.0" and above my own 1.00 floor).** I floated 1.00/1.00 as the baseline-setting recommendation (neutralise, let the tweaker fine-tune); the **trader chose 1.10/1.05** to pre-empt, citing the weekend caveat himself. 1.10/1.05 floors the ASIA HIGH/MID thresholds at 2.2/1.575 (a notch above neutral), partially countering the ~1.5× thin-denominator VolumeRatio inflation. **Sanity-check:** given the magnitude is weekend-set (call 2), is going *above* neutral on the first re-baseline too aggressive — i.e. should the pre-emptive notch have waited for the tweaker / weekday data?

4. **CVD: "re-confirm" turned into a 4th change (0.05 → 0.10).** The brief listed CVD under "re-confirm the v33 three," expecting verification, not a move. But CVDSlope FLAT is **5.5%** — below v33's explicit 10% floor and its written pre-commitment ("if still <10%, raise next pass"). NY (busiest, |CVDValue| p50 337k) is the drag at 2.7% FLAT, proportional-bound, so I raised the **pct arm** (0.05→0.10), not the floor (kept 12k, protects thin-Asia CVD). **The honest hole:** `weightedSlope` is **not CSV-logged**, so I can't predict the resulting FLAT share — only monitor it. **Two-sided guard (folded per review):** target window **8–18%**; FLAT **< 8%** → under-delivered, go **0.12**; **> 20%** → revert **0.07** (both pct-arm; floor stays 12k). **Sanity-check:** is moving a PREFERRED signal *blind* the right call, or should CVD have stayed at 0.05 until the CSV logs `weightedSlope` (the enabling code spec) so the next pass can sweep it properly? v33's precedent says move; the no-prediction caveat says maybe wait.

5. **OBV KEEP despite 79.7% directional (v33 predicted 45–55%).** The overall rate is well above v33's first-100 expectation, which on its face says "still over-voting." I kept 18.0 anyway, arguing the **regime ordering is now healthy and correctly inverted** (TRENDING 87% > RANGE_BOUND 52% directional — v33's actual concern was that range was *more* directional than trend, and that reversed), and that the high overall rate is the 66%-trending sample, not a mis-set gate. **Sanity-check:** is the regime-ordering argument sufficient, or should I have re-anchored the |obvChange| median on the multi-session sample (which needs the offline re-fetch, §3) before calling KEEP?

---

## 3. Method & its limits (vs v33)

- **Distributions** are computed directly from the v0.6 CSV (975 rows) + the eval cache (1532 entries) — no extrapolation. Mechanisms read from source (`CalcFundingMomentum`, `CalcSpread`, `DynamicNorms.Compute`/`ApplySessionVolume`).
- **Session attribution verified against code, not assumed:** `Timestamp` is UTC (`AnalysisLogger.vb:128`); `ApplySessionVolume` buckets by `DateTime.UtcNow.Hour` against the settings bounds (`DynamicNorms.vb:120-122`) → engine ASIA = utcHour **0–7**. 120/233 ASIA rows sit at hour-07; correctly attributed to ASIA (the multiplier the engine applied). Noted the display-only `ResolveSessionLabel` off-by-one (`MainForm_Render_Cards.vb:1379`, uses `<7`).
- **What I did NOT do (the main method gap vs v33 §3.5):** no offline Deribit volume re-fetch. `ohlc_1m_cache.csv` stores Volume=0, so the per-run dynamic volume threshold (`clamp(1+2·CoV,2,6)×mult`) isn't reconstructable from the CSV, which means item A's exact post-change **fire rate** is bounded (via the clamp floor + logged VolumeRatio), not computed, and the §3.7 ASIA/LONDON clamp-binding check is deferred. **Sanity-check:** v33 did the re-fetch for NY; I judged the lighter method acceptable because item A is a baseline-setting/direction call, not a precision-tuned fire-rate. Is that an acceptable budget/rigor trade for a *foundational* re-baseline, or does item A need the re-fetch before 1.10/1.05 is trustworthy?
  **Review resolution:** acceptable for the direction call; the offline re-fetch is now **bundled into the weekday-ASIA re-verify pass** — that pass re-anchors OBV's median on multi-session data (closes call 4's thin basis), runs the ASIA/LONDON clamp-binding check, and computes the ASIA fire-rate properly. Calls 4 + 5 close together there.
- **No acceptance harness** — this is settings-only, zero code, so there's nothing to fixture. "Validation" = the distribution analysis (run via a throwaway PowerShell script, not committed) + JSON-parse + build. POCO defaults in `EngineSettings.vb` left untouched per the v33/Tier-C precedent (rides the next code commit).

---

## 4. Settings & docs touched

- `settings.json`: version 33→34, `last_modified`/`modified_by`, change_log prepended (one line, covers all three moves + the weekend confound + KEPT items + flagged code specs). Three values: CVD `slope_pct_of_value` 0.05→0.10, funding `momentum_threshold` 1e-5→5e-8, session_volume ASIA 0.80/0.85→1.10/1.05. No keys added/removed; CSV stays v0.6.
- `docs/clean-data-rebaseline-v34-proposal.md`: the full rationale (status now APPROVED & APPLIED with the 1.10/1.05 choice + weekend confound woven through §1/§3.1/§5/§7).
- `docs/DeribitIndicatorProject.md`: §15 v34 row; §6 pointer →v34; §12 WATCHING — added the weekday-ASIA re-verify (Medium), enriched the first-fire row with the dry-run/weekday-window sequencing, marked the post-correctness re-baseline row **RESOLVED** by v33+v34.
- **Code specs flagged (not settings, NOT actioned here):** (a) partial-bar volume starvation — `VolumeRatio` samples the in-progress bar vs a completed-bar SMA, structurally starving the volume signal; this *caps* item A's lever and is the real Asia unlock. (b) log `weightedSlope` next CSV bump — unblocks CVD precision (call 4). (c) continuous spread sampling (WS P4). (d) `ResolveSessionLabel` hour-7 off-by-one (display-only).

---

## 5. Sanity-check asks (condensed)

If the spec writer reads nothing else, these five:

1. **Item A ground truth** — is Asia's high barrier-SUCCESS really ATR-confounded noise (my read), or possible real edge that argues *against* raising the bar?
2. **Apply-vs-hold on a 100%-weekend ASIA sample** — was applying 1.10/1.05 now (re-verify later) right, or should the session_volume change have waited for weekday data?
3. **CVD blind move** — 0.05→0.10 with `weightedSlope` unlogged: move (v33 precedent) or hold (no-prediction)?
4. **OBV KEEP** — regime-ordering argument sufficient, or re-anchor the median via re-fetch first?
5. **Method** — is skipping the offline volume re-fetch acceptable for a foundational re-baseline?

My own lean on each: (1) confounded, but worth an explicit second look; (2) apply was defensible given direction-safety + the dry-run-gated tweaker downstream, but reasonable people could hold; (3) move, narrowly — v33 pre-committed and the guard caps the downside; (4) sufficient — the ordering inversion *is* the signal v33 cared about; (5) acceptable for a baseline-setting pass, not for a precision pass. Push back on any.

---

## 6. Open / pending (interim status — sequencing revised by review, see §7)

1. **First-N-row checks (proposal §5)** ride the fresh v34-scored rows — funding RISING/FALLING should appear (~2–4%, was 0%); **CVDSlope FLAT target 8–18%** (two-sided guard: < 8% → 0.12, > 20% → 0.07); ASIA trade-rate down a touch. Judge ASIA against **weekday** rows, not the Saturday baseline.
2. **v35 de-confound pair lands first** (separate conversation): `eval-metric-deconfound-proposal.md` + `min-tradeable-move-gate-proposal.md`. Until history is re-based, the failure-rate matrix is ATR-confounded and the auto-tweaker can't read it (§7).
3. **Weekday-ASIA re-verify pass** (post-v35; §12 WATCHING) — manual, once ≥50 weekday-Asia rows exist (the auto-tweaker has no day-of-week concept). **Now bundles the offline volume re-fetch** → re-anchors OBV median + ASIA/LONDON clamp check + ASIA fire-rate (closes calls 4 + 5).
4. **Supervised first fire** — dry-run confirmed safe (`tweaker_config.json`: `dry_run_enabled: true`, `auto_commit_enabled: false`, window 30 verdicts, fires > 40% failure). **Now gated on v35** (not just weekday data); run on a weekday window; hold any `session_volume` proposal; diff reviewed by the sanity-check seat before any apply.
5. **Still owed by data:** TRENDING_DOWN ≥50; STRONG-tier outcomes for Kelly `est_prob_*`.

---

## 7. Sanity-check outcome (2026-06-13)

Spec-writer review **PASSED** — all five flagged calls defensible, none warrant a revert. Resolutions:

1. **Item A (ATR-confound):** correct, and *stronger* than I argued — the barrier metric is positively biased in low-ATR up-drift (small targets noise-tag before expiry), so it cannot argue against raising the Asia bar. Selectivity reframe stands.
2. **Apply-vs-hold on weekend-only Asia:** apply was right — 0.80/0.85 is known-harmful; any ≥ 1.0 is the safer conservative. 1.10/1.05 is the trader's risk call, within latitude.
3. **CVD 0.05→0.10:** acceptable (executes v33's pre-commitment, guarded) — **revision: guard made two-sided** (< 8% → 0.12, 8–18% target, > 20% → 0.07; folded into §2.4 / §5 / §6).
4. **OBV KEEP:** regime-ordering argument sufficient (the inversion v33 cared about is fixed); basis is thinner than v33's median-recompute — re-anchored by the bundled re-fetch (rev. 5).
5. **Method (no re-fetch):** acceptable for the direction call — **revision: bundle the offline re-fetch into the weekday-ASIA re-verify**, closing calls 4 + 5 in one pass.

**Review finding that changes sequencing (now in the v34 brief).** The success/barrier metric is **ATR-confounded**: success inversely tracks ATR (Asia **< 12 ATR → 86.8%** vs **16+ → 52.8%**; Asia 74% > NY 61% full-sample *despite the lowest ATR*), because the favourable barrier is `k × ATR`, so low-ATR targets are sub-tradeable moves tagged by noise. This **empirically vindicates call 1**. The fix is a paired **v35**: (a) a measurement de-confound (`eval-metric-deconfound-proposal.md`) that re-bases history by **excluding** gate-killed low-ATR trades (`EXCLUDED_BELOW_MIN_MOVE` — **not** counted as failures), and (b) a min-tradeable-move scoring gate (`min-tradeable-move-gate-proposal.md`, favourable-barrier floor **0.08%**) that stops the engine emitting those trades going forward.

**Consequence — the auto-tweaker first fire now waits for v35, not just weekday data.** The tweaker optimises the failure-rate matrix, which is confounded until v35 re-bases it. **Revised sequence:** commit this spec-back (revisions folded) → **v35 pair lands** (separate conversation) → **weekday-ASIA re-verify** (with the bundled re-fetch) → **supervised dry-run first fire** → **diff reviewed by the sanity-check seat before any apply**.

*Revisions folded; spec-back committed.*
