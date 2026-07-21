# 3-min weekday-ASIA `session_volume` re-verify (READ, 2026-07-22)

**Class:** Verdict-draft data read. Recommendation only — **no settings change**.
**Recipe sources:** `docs/clean-data-rebaseline-v34-proposal.md` §3.1 + `docs/clean-data-rebaseline-v34-spec-back.md` §7 + `docs/session-timeframe-resolution-implementer-handoff.md` §6.2.
**Backlog row:** `docs/backlog-dependency-map.md` line 10 (3-min weekday-ASIA `session_volume` re-verify) → this read discharges that row.
**Frozen inputs:** `analysis_log.csv` (5936 rows), `analysis_eval_cache.csv` (schema v6, 8847 rows), copied to scratchpad and read from there (live-append rule).

---

## 1. Method

- Frozen `analysis_log.csv` → filtered to `ExecResolution=3` (post-v36 3-min bar) rows on **UTC hour ∈ [0,7]** with **weekday Mon–Fri**. Test-burst `InstanceId` starting `8706ebae` excluded (contaminated ~13-s cadence, 2026-07-21 14:30–15:11 UTC).
- Computed the three §3.1 metrics: **trade-rate** (`Verdict` NOT starting `NO TRADE`), **RANGE_BOUND regime share**, **VolumeRatio tail** (p50/p75/p90/p95).
- Compared against the v34 §3.1 Saturday-sample values (100% Saturday 2026-06-13, 1-min).

**Confound flagged before the numbers:** the v34 ASIA baseline is **1-min** on Saturday. This read is **3-min** on weekday (v36 shipped the resolution split). Trade-rate / regime-share are comparable in *direction* (both measure engine behaviour under the current `session_volume` multipliers). The **VolumeRatio percentile scale is NOT apples-to-apples** because the partial-bar vs completed-bar-SMA gap widens under 3-min sampling (a 3-min bar takes 3× longer to complete, so any given poll samples a smaller relative fraction). Report the numbers, don't over-interpret the tail.

---

## 2. Sample composition and gate

| Item | Value |
|---|---|
| n (weekday, UTC 0–7, ExecResolution=3, non-burst) | **344** |
| ≥50-row gate | **MET** (n=344, ~7× the floor) |
| Date span | 2026-07-10 → 2026-07-21 UTC |
| Weekday spread | Mon 2 · Tue 54 · Wed 70 · Thu 77 · Fri 141 |
| Instance days seeing the bucket | 7 |

Fri-weighted, but no single day dominates and the ≥50 gate is comfortably cleared.

---

## 3. Numbers — weekday-ASIA 3-min (this read) vs Saturday 1-min (v34 baseline)

| Metric | Saturday 1-min (v34 §3.1) | Weekday 3-min (this read) | Delta |
|---|---:|---:|---:|
| trade-rate (Verdict ≠ `NO TRADE`) | **63.1%** | **29.36%** (101/344) | **−33.7 pp** |
| RANGE_BOUND regime share | **27.5%** | **10.17%** (35/344) | **−17.3 pp** |
| NO TRADE share | 36.9% | 70.64% | +33.7 pp |
| VolumeRatio p50 | ~ (not tabled) | 0.001 | — |
| VolumeRatio p75 | ~ (not tabled) | 0.009 | — |
| VolumeRatio p90 | 2.01 | 0.116 | resolution-confounded (see §1) |
| VolumeRatio p95 | 3.38 | 0.230 | resolution-confounded (see §1) |

Regime distribution on the 344-row weekday-ASIA set: TRENDING_UP 142 (41%), TRENDING_DOWN 126 (37%), TRANSITIONAL 41 (12%), RANGE_BOUND 35 (10%).

Verdict distribution: NO TRADE 203, NO TRADE [WEAK …] 40, WEAK-tier directional 70, MEDIUM 30, STRONG 1.

---

## 4. Reading

**On the two apples-to-apples axes, weekday Asia is materially calmer than the Saturday sample that motivated the v34 ASIA raise.**

- **Chop is roughly one-third of the Saturday level** (10.17% vs 27.5% RANGE_BOUND). The "ASIA over-trades the choppiest session" pathology that the v34 read documented does not reproduce on weekday 3-min.
- **Trade-rate is less than half** (29.4% vs 63.1%). The engine on current settings (v34 ASIA 1.10/1.05, layered on 3-min via §6.2 which explicitly kept `session_volume` 1-min-calibrated and transferred it approximately) is trading Asia far less often on weekday than the Saturday sample said it does.
- The VolumeRatio tail is on a different scale (§1 confound) so p90/p95 direct comparison is not a valid lever.

**Two paths on the numbers:**

- **v34 §3.1's original hypothesis (0.80/0.85 was backwards, ≥1.0 needed)** is not directly falsified — it was about not *lowering* the volume floor on a thin session, and any value ≥1.0 upholds that. Neutral 1.00/1.00 would too.
- **The trader's pre-emptive above-neutral notch (1.10/1.05)** was explicitly weekend-set (spec-back §7 call 2, sanity-check disposition). On weekday 3-min data, weekday Asia is not over-trading in chop — if anything it's under-trading (29.4% at n=344 with 10% chop). The above-neutral notch has no visible over-trading pathology to correct on weekday Asia.

---

## 5. Recommendation

**Dial ASIA `session_volume` back toward neutral — recommend 1.00/1.00** (from 1.10/1.05). Rationale:

1. The specific mispathology 1.10/1.05 was applied to counter (over-trading in the choppiest session, 63% trade-rate + 27.5% RANGE_BOUND) is not present on weekday Asia at 3-min.
2. Weekday Asia is trading at less than half the Saturday rate. Keeping the above-neutral notch is asking the engine to raise the bar further on a session where the bar is already, empirically, plenty high.
3. Neutral (1.00/1.00) preserves the *direction* of the v34 fix (0.80/0.85 was known-backwards) without carrying the weekend-set magnitude into weekday operation.

**Not recommended: revert to 0.80/0.85.** The v34 direction-of-fix argument (spec-back §7 call 1) still stands — lowering the volume floor on the thin Asia session was the original defect. Neutral, not below-neutral.

**Not this read's call:** whether to also address the deeper resolution-scale VolumeRatio distribution issue (§1 confound). That is a partial-bar / same-resolution calibration question that pre-dates and outlives this recommendation. Flag for the OBV `|obvChange|` re-anchor bundle the roadmap W1 row pairs with this row.

---

## 6. Caveats

1. **1-min vs 3-min confound.** VolumeRatio percentile comparison is invalid across resolutions; only trade-rate and regime-share are cleanly comparable. Recommendation rests on those two.
2. **Trader-only judgement call — no `settings.json` edit or commit here.** This is a verdict-draft data read; the config value change (if approved) is a separate settings pass and belongs with the auto-tweaker's rules of engagement (§6.3 of the resolution handoff — the tweaker cannot propose this itself because `session_volume` is on its exclusion list).
3. **Weekday-ASIA sample is 7 sessions.** ≥50-row gate is met by margin (n=344), but there is Fri-weighting. Consider re-reading after another 2 weeks if the trader wants Mon/Tue reinforcement before dialing.
4. **Fixed-window auto-tweaker.** Any change here must land before the next tweaker fire on Asia data (per handoff §6.3 — the tweaker keys on `(session × resolution)` since v36; a mid-window edit interleaves populations).

---

*Data read only. No code, no `settings.json` write, no commit-of-settings.*
