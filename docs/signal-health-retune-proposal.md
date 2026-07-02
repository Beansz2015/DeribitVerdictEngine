# Signal-Health Retune/Retire Pass — Proposal

**Status:** PROPOSED — spec-first, awaiting trader sign-off (§6). Nothing here ships until approved, and the scoring-affecting items ship only at the next dataset boundary (§4).
**Date:** 2026-07-03 (Fable seat). **Evidence:** `signal-health-audit-2026-07-03.md` (the W1 audit — read it first; this doc only decides).
**Class:** R1+R2 are ⚠ scoring-affecting (settings-only). R3–R8 are no-change rulings or coverage/hygiene riders.

---

## 1. Background

The roadmap W1 signal-health audit ran 2026-07-03 on the post-v42 book (WS era n=2,377; conditional outcomes on 711 evaluated directional runs). Three named questions came in with the handover; the audit answered all three and surfaced two coverage gaps and one feed-validation gap. This proposal converts the findings into one bundled retune/retire pass.

## 2. Recommendations

### R1 — Retire the OFIMomentum scoring modifier ⚠

`indicators.OFI.momentum_enabled: true → false` (settings-only, hot-reloadable, reversible).

**Why.** (a) The state is active ~90% of runs in every era, session, and OFI construction (audit §4) — as a *modifier gate* it discriminates almost nothing. (b) It cannot be fixed by threshold: the v48 spec-back fitted 0.136 ≈ the current 0.15; the ratio-delta distribution is fat, so any meaningful threshold is either always-on or always-off. (c) Conditional outcomes point the wrong way: the +1 CONFIRMED arm ran 53.1% SUCC while verdicts that fired through the −1 SUPPRESSED arm ran 72.5% (audit §5; survivorship caveats acknowledged — the case rests on (a)+(b), with (c) as corroboration that no value is being lost). (d) It stacks a second vote on top of the OFI level signal from the same book stream — the marginal-information bar for that is high and unmet.

**What is kept.** `CalcOFIMomentum` still computes; `r.OFIMomentum` and the CSV `OFIMomentum` column still populate (free diagnostics for the next audit cycle); the `_ofiHistory` ring is untouched. The OFI level signal and the v48 dominance pair are untouched.

**Display parity.** No line is added/removed/renamed. The OFI breakdown note is `Ratio:X | SIGNAL | MOM:state{suffix}` — the `MOM:state` segment renders from `r.OFIMomentum` regardless of `momentum_enabled`; only the modifier suffix (`+1[L] confirmed` / `-1[S] suppressed…`) stops appearing, exactly as it already doesn't on FLAT/BALANCED rows today. Snapshot and card render the same composed `SignalBreakdownItem.Note`, so no card-binding change. (Stated per the display-string parity rule.)

**Tweaker fence (hygiene rider).** With the modifier disabled, `momentum_window/threshold/bonus` become inert — leaving them proposable recreates the recorded-APPLIED-no-op class v47 F1 closed. Add prefix `indicators.OFI.momentum_` to `SettingsDiffApplier.RejectedPathPrefixes` + a PromptBuilder HARD CONSTRAINT line (mirrors `averaging_enabled` / HC 16; the prefix does not touch `book_depth`, `buy/sell_dominant_ratio`, or `avg_window_sec`, which stay on the surface). Reversal path: re-enable = flip the flag + drop the fence in one commit.

### R2 — Re-derive the funding momentum threshold for the WS funding feed ⚠

`indicators.funding.momentum_threshold: 5e-8 → 2e-7` (settings-only). `momentum_window`, `momentum_amplify`, `momentum_soften` unchanged.

**Why.** The v34 threshold was derived when REST funding was sticky (a 3-*change* window spanned hours). On WS, `funding_8h` changes on 96.5% of runs, so the same window now spans ~3 minutes and Step 3b moves scores on **36.8%** of runs (REST era: 16.0%) — the "funding modifier is adjunct" invariant is violated by drift, not by design. The signal's *direction* remains informative (flagged runs 45.3% vs 56.6% SUCC), so this is a retune, not a retire. At 2e-7 on the reconstructed WS window-delta distribution (95.1% validation agreement): FLAT becomes modal (67.6%) and 3b engagement lands at 18.8% ≈ the REST-era adjunct profile. Conservative alternative: 3e-7 → 14.3% (D2).

**Residual defect, deferred.** The 3-change window is cadence-dependent (30 s vs 60 s vs on-close spacing changes its wall-clock span). The clean fix is a time-anchored comparison (rate vs the sample ≥N minutes back) — small code change, own mini-spec, not this pass (§7).

### R3 — Spread: no retune; REJECT A1 spread-momentum

The revival premise is refuted: the WS book is a one-tick market (p50–p95 = 0.08–0.09 bps; p99 = 0.59; >5 bps on 0.1% of runs). Keep `wide_threshold_bps: 5.0` as an inert tail-guard — it costs nothing, distorts nothing, and fires only on genuine dislocation. **A1 spread-momentum is rejected with evidence** (a momentum signal on a constant is noise) — remove it from the W1 audit rider list. Declined option (for the record): re-anchoring `tight_threshold_bps` 1.5→0.15 so the TIGHT display label discriminates — zero scoring impact, near-zero value; not proposed.

### R4 — RSI-divergence penalty: KEEP (validated)

38.6% SUCC when the penalty hit the verdict side vs 55.2% baseline — the strongest working penalty in the stack. No change.

### R5 — Pass 2b OI×CVD: KEEP (validated)

CONFLICT 38.9% vs CONFIRMED 55.6%; rare (2.9% engagement) but pointing the right way at both ends. No change.

### R6 — CVD slope vote + MicroCVD: KEEP, re-audit next cycle

Conditional gradients are flat, but conditional-on-fired data cannot distinguish "no value" from "fully absorbed by the threshold", the flow stack shows no redundancy (max pairwise agreement 69.3%), and no settings lever maps to the observation. The F15 measurement (CVD divergence fires 1.3% on 3-min vs 10.3% on 1-min — the v36 un-scaled `divergence_price_gate` did NOT over-fire; it under-fires) goes to the **v36 Phase-2 carry-forward** row: re-measure on 3-min data before touching the gate, do not blind-scale ×2.1.

### R7 — OISignal: KEEP as-is

91.9% NEUTRAL is the 15m/60m OI market's base rate at the current band, and full-aligned fires run 60% SUCC (n=50) — rare but good. A band retune would re-semanticize a working signal on thin evidence; declined this pass.

### R8 — Liquidations: unproven-live; validate via #7; gate A4

Plumbing verified end-to-end (REST parse, WS parse, dominance gate) yet zero liq-marked trades in 8,025 runs including the 06-24 flush day. No scoring change (the penalty is costless while silent). **Directives:** (a) the #7 liq-cascade-alarm spec MUST include a first-liq-seen diagnostic (status/log line) so one real cascade settles the question; (b) **A4 (liq×OFI flip) is gated on that validation** — roadmap W2 note on approval.

## 3. Coverage riders (non-scoring)

| # | Fix | Vehicle |
|---|---|---|
| C1 | Add `TFIValue`,`TFISignal` CSV columns (close the F11 audit blindness) | rides the #5 aggressor-velocity v0.7→v0.8 schema rotation — one rotation, not two |
| C2 | `FundingRate` logging F6→F8 (`AnalysisLogger` format only; same column, no rotation) | rides the same boundary commit |

## 4. Sequencing — dataset boundary (binding)

The v48 collection window is OPEN (§4a per-session fire-rate watch needs ≥2 further weekday sessions). Per roadmap rule 1, R1+R2 must NOT land mid-window. **They land bundled AT the #5 aggressor-velocity build boundary** (spec already approved; build opens the next window anyway): one commit event = #5 build + R1 + R2 + C1 + C2 + the R1 tweaker fence, settings **v48→v49**, one `change_log` entry, POCO defaults riding the same code commit (v33/v34 precedent). One boundary, one reset, no overlapping windows. If #5 slips badly and the v48 watch closes first, R1+R2 may ship alone at their own boundary (trader's call at that point).

## 5. Acceptance

- 3 Release builds 0/0 (solution + AutoTweaker + OrderCheck); `verify-gate.ps1 -Mode prepush` green.
- Harness unregressed + new fixtures: (i) `momentum_enabled=false` leaves the OFI level award byte-identical to the no-modifier path; (ii) `SettingsDiffApplier` rejects `indicators.OFI.momentum_bonus` (prefix fence) and still accepts `indicators.OFI.avg_window_sec`; (iii) funding: threshold change is value-only (no new fixture needed beyond A-series unregression).
- Post-ship watch: FundingMomentum FLAT% ≈ 60–70% and 3b engagement ≈ 15–25% over the first 2 weekday sessions (recipe: audit §7 table row); OFI row renders MOM:state with no modifier suffix.
- §12/§15 updates: version entry; audit-discharged rows move per audit §9.

## 6. Sign-off decisions

| # | Decision | Recommendation |
|---|---|---|
| D1 | R1 retire OFIMomentum modifier (momentum_enabled=false) | **Yes** |
| D2 | R2 threshold: 2e-7 vs 3e-7 vs keep 5e-8 | **2e-7** |
| D3 | R3 keep spread 5.0 inert + reject A1 | **Yes** |
| D4 | C1 TFI columns ride the #5 v0.8 rotation | **Yes** |
| D5 | R1 rider: fence `indicators.OFI.momentum_` off the tweaker surface | **Yes** |
| D6 | Bundle-at-#5-boundary sequencing (§4) | **Yes** |

## 7. Out of scope (recorded, not proposed)

- Time-anchored funding-momentum window (code change; own mini-spec when the trader wants the cadence-dependence gone).
- OISignal `change_threshold_pct` retune (R7 — insufficient evidence).
- Any CVD/MicroCVD scoring change (R6 — re-audit first; F15 re-measure goes to the Phase-2 carry-forward).
- London 3-min adverse-cluster response (F13 — stays a data-gated §12/(B) watch; 58 evaluated rows is not a calibration base).
- Reach-target calibration (separate W1 item, elevated by O2 — unchanged).
- `tight_threshold_bps` display re-anchor (declined, R3).
