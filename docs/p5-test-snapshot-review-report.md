# P5-Test Snapshot Review — Findings Report

**Date:** 2026-06-12
**Reviewer:** Fable 5, Max effort (adversarial re-review per `docs/p5-test-snapshot-review-brief.md`)
**Mode:** FIND-ONLY. No source edits, no commits to source. Crop tooling + per-region evidence crops live in `verify\p5-review-crops\` (gitignored).
**Artifact set:** `bin\Debug\net8.0-windows\verify\p5-test\` — 55 cases × (PNG + legacy.txt + snapshot.txt), generated 2026-05-28, post-gap-fix commits `7c8de56`→`465b257`, pre-Tier-C/pre-v31.

**Method actually used:** programmatic border-scan of the 1100×3124 captures to fix the region map; per-region crops at 1.4–2.0× (fit-scale full PNGs never used as evidence); 3× micro-crops where clipping needed resolution; a programmatic colour sweep of the VERDICT/CONTEXT text pixels across all 55 PNGs against the Theme hex ramp; per-case legacy diffing to identify which regions carry non-baseline content. Every case's breakdown TOTAL that was directly read (38 of 55) matched legacy exactly; no TOTAL mismatch found anywhere.

**Headline:** no direction-colour inversion exists anywhere in the set — the colour sweep clears all 55 verdict headlines against the 7-tier ramp exactly, and every side-sensitive element checked (pills, SC signs, EMA ribbon order, comparators, OI×CVD side labels, structural/ATR side emphasis) is correctly mirrored. The defects found are clipping/truncation and two content-parity gaps — the same disease class as KNOWN-0, in five more places.

---

## 1. Findings table

Severities per brief: CRITICAL = wrong value/direction/colour-inversion; HIGH = clipped/illegible load-bearing value; MED = misalignment/box defects/format-guideline breaks; LOW = cosmetic.

| Ref | Sev | Case(s) | Card / region | What's wrong | Evidence |
|---|---|---|---|---|---|
| **KNOWN-0** | HIGH (known) | all 55 | ATR ENTRY LEVELS zone rows | Confirmed as baseline, three manifestations: (a) active-side row (larger font) — `(risk N / rwd N)` line **fully invisible** (01–03 long side, 06–08 short side); (b) inactive-side row — line renders but bottom-clipped mid-glyph; (c) NO TRADE — both rows render large, **both** lines invisible (04, 05, 09…); (d) CAPPED cell reason label clipped: `(SWING_HIGH_5M)` top-sliver (11), `(NEAREST_HVN_ABOVE/BELOW)` fully invisible (12, 13), `(POC)` sliver (14). Matches `docs/ui-reskin-atr-clip-fix-kickoff.md`. | `01__c2z_atr_money.png`, `04__c2z_atr_money.png`, `11__zoom_rr_capped.png`, `12__c2z_atr_money.png` |
| **F-01** | HIGH | all cases with structural pairs (~46) | STRUCTURAL LONG + SHORT cards, R:R cell | `(risk 300.0 / rwd 300.0)` **right-truncated at the cell edge**: renders `(risk 300.0 / rwd` — the reward value is lost on every structural card, both sides. Same disease as KNOWN-0, horizontal axis. The KNOWN-0 kickoff's "STRUCTURAL card renders its risk/rwd line fine" claim is true only vertically. | `01__c2z_atr_money.png`, `01__struct_zoom.png` |
| **F-07** | HIGH (triage: consider CRITICAL) | 11, 12, 13, 14 | ATR ENTRY LEVELS capped row | Two coupled content-parity gaps: (a) the **raw ATR target is dropped** — legacy shows `Target 50160.0 --> 50080.0`, card TARGET cell shows the capped value only; (b) the **R:R ratio stays raw-ATR** — `1:1.7` (`FormatRR(atrTarget, atrStop)`) next to the capped TARGET. Legacy deliberately omits R:R on capped rows. A trader can read R:R 1:1.7 against a target whose actual reward is 80 points. **Correction (2026-06-12 code read):** the risk/rwd *subline* already computes reward from the adjusted target (`rwd 80.0`) — the original report's "subline read via 3× zoom as rwd 160.0" was a misread of KNOWN-0-clipped glyph slivers. The finding stands on the visible ratio + the dropped raw value. | `11__zoom_rr_capped.png`, `13__c2z_atr_money.png` |
| **F-08** | HIGH | 41, 42 | VERDICT card, REGIME ANCHOR banner | Banner right-truncated at the card edge: `…STRONG LONG fightin` / `…STRONG SHORT fighti` — the conclusion "fighting intermediate bear/bull" is lost (values 3.1× ATR, above/below 5m EMA(200) visible). The banner also **overlays the chip row**: CONTEXT/REGIME/MTF chip bottoms are clipped behind it and the HOLD slot is hidden. Symmetric on both cases. | `41__c1_hero.png`, `42__c1_hero.png` |
| **F-09** | HIGH | 43, 44, 45, 46 | VERDICT card, HOLD chip | HOLD/EXIT text truncated: 43 `EXIT LONG (Layer 1) -- 2+ adverse` (drops "microstructure: OFI flipped, CVD diverging"); **44 `EXIT LONG (Layer 1.5) -- price closed` — drops the break prices 49680.0 / 49700.0 entirely (worst arm)**; 45 `EXIT SHORT (Layer 2) -- OBV bullish`; 46 `HOLD LONG (Layer 3) -- single adverse: OFI`. The full reason text currently renders only in the bottom legacy verification block, which **P5b deletes** — this fix must land before the P5b sweep or the information is gone from the UI. | `43__c1_hero.png`, `44__c1_hero.png`, `45__c1_hero.png`, `46__c1_hero.png`, `43__c6_plain_bottom.png` |
| **F-02** | MED | all 55 | SIGNAL BREAKDOWN, NOTE column | Gap-fix guideline **G7 (capitalise NOTE starting characters) is unapplied** in the breakdown table: `none` (RSI Div / BBW/TTM / Liq), `accelerating`, `decelerating`, `flat`, `upper qtr`, `lower qtr`, `mid`, `inside va`, `above vah`, `releasing`, `bullish`, `bearish`, `mixed alignment`, `insuff.`, `long_cascade`, `ratio 1.45 ·…`, `within σ1`. The VP-card items (C5a/C5b) *were* capitalised, so the guideline landed selectively. No gap-fix spec-back exists documenting a deliberate skip. Triage: trader may accept breakdown brevity; if so, lock it in the handover §4. | `01__c3a_breakdown.png`, `51__c3a_breakdown.png` |
| **F-03** | MED | all 55 | SIGNAL BREAKDOWN, Swing Pivots sub-note | The C4b-spec'd string `Best vol. pivot (5m): HIGH @ 50050.0 (vol × 1.8 vs avg. pivot)` does not fit and ellipsis-truncates: `…(vol × 1.8 vs av…`. Value, side and ratio are visible; the "avg. pivot" tail is lost on every case incl. the LOW variant (33: `LOW @ 49680.0 (vol × 2.1 vs a…`). | `01__c3b_breakdown.png`, `33__c3b_breakdown.png` |
| **F-05** | MED | 07, 08, 28 | SIGNAL BREAKDOWN, OI Change STATE pill | `NW SHRTS` does not fit the pill and truncates to `NW SHR…` while `NW LNGS` fits (01, 02). The C4c.i abbreviation is **asymmetric in effect** — a G8 violation on exactly the short side. The details-card header (`OPEN INTEREST · NEW SHORTS`) renders the full name fine. | `07__c3b_breakdown.png`, `08__c3b_breakdown.png` |
| **F-10** | MED | 48 | INDICATOR DETAILS, VWAP box | Legacy renders `VWAP (reset 13:30 UTC)  [WARMUP]:` — the card's VWAP box header has **no [WARMUP] tag** (Candles: 20 and Dev +0.050% are correct), and the breakdown VWAP Dev row doesn't carry it either. Warmup state (dev value unreliable, scoring gated) is invisible in the card UI. | `48__c5_full.png`, `48__c3a_breakdown.png` |
| **F-11** | MED | 51 | SIGNAL BREAKDOWN, Liq row | Pill shows **NONE** while the same row's note reads `long_cascade` and SC = −1; the details box header correctly shows `LIQUIDATIONS · LONG_CASCADE` with sizes 420/85. The pill mapping falls to a default on cascade signals. A trader scanning pills sees "no liquidation event" during a cascade. (SHORT_CASCADE mirror unexercised by the set — assume affected.) | `51__c3a_breakdown.png`, `51__c5_full.png` |
| **F-06** | LOW | 08, 23 | VOLUME PROFILE, VPFR signal label | `Near HVN resist` renders **red**, not the gap-fix C5a-spec'd `ACC_WARN` amber. Direction-consistent (resist = short-favouring) and arguably better than the spec — but it is a spec deviation. Triage: bless or fix. (`Near HVN support` green ✓; `In LVN bull/bear` green/red symmetric ✓; `Above VAH` green / `Below VAL` red ✓.) | `08__c4_oicvd_vp_kelly.png`, `23__c4_oicvd_vp_kelly.png` |
| **F-04** | LOW | 04, 09, 11–15 (RSI=50 cases) | SIGNAL BREAKDOWN, RSI note | At RSI exactly 50.0 the comparator defaults to `>`: note renders `50.0 > 50.0` (false as written), on 13/14 paired with a BEAR pill and SC −1. Comparator is otherwise side-correct (`44.0 < 50.0`, `64.0 > 50.0`). Synthetic-data edge; real ties are rare. | `04__c3a_breakdown.png`, `13__c3a_breakdown.png` |

### Observations (not defects; for triage/awareness)

| Ref | Case(s) | Note |
|---|---|---|
| OBS-A | all | LAST PRICE card clock shows capture wall-time (02:39–02:42 UTC+8 across the set), not `v.Timestamp` (legacy pins 2026-01-01 12:00:00). The card binder reads the live clock. Invisible in production (sub-second gap) but a latent inconsistency for replayed/stale verdicts. Distinct from the triaged Q1 (which covered the pinned text timestamps). |
| OBS-B | 01–04 etc. | MTF chip renders `MTF PASS [—]` — it appends a `[—]` direction placeholder when the reason string carries no bracket. Pre-v31 harness strings (`MTF PASS`) trigger it; post-v31 composed reasons should carry direction. Verify on a live run. |
| OBS-E | 03 vs 06 | Context-tag colour mapping: MOMENTUM_FADING amber, STRUCTURALLY_WEAK red. Confirm the severity mapping is intended (CONFIRMED is green on both sides — fine). |
| OBS-F | 16, 17, 18 | Structural missing-leg notes condensed vs legacy: `— no swing stop above` (16), `— no swing` (17 — least specific), `— no swing stop below` (18). Legacy carries `(stop unset: no swing high above entry within lookback)`. Information loss is minor; 17's target-note could at least name the side like 16/18 do. |
| OBS-H | 49 | Spread WIDE: legacy note `-1 WIDE 4.5 bps`; card shows pill WIDE + `4.50 bps` + SC 0 — the −1 penalty magnitude is not surfaced. Same family as the Spec-C SC-under-reporting deferral (Q3g); a cheap interim fix is carrying "−1" in the NOTE text. |
| OBS-C/D | 01, 04 | Pill-vs-state pattern: CVD note omits the slope word (pill carries it); ROC pill binds the value's sign even on FLAT slope (BULL pill, SC 0). Consistent with the locked Q3k pill-vs-SC distinction; listed for completeness. |

---

## 2. Coverage matrix

All 55 cases × 12 worksheet regions. Regions: TB = top bar; HERO = SCORE/VERDICT/LAST PRICE; ATR; SL/SS = STRUCTURAL LONG/SHORT; BD = SIGNAL BREAKDOWN (+ TOTAL vs legacy); OC = OI×CVD; VP = VOLUME PROFILE; KY = KELLY; ID = INDICATOR DETAILS; PT = bottom plaintext (legacy verification) block; PAR = legacy-parity tick-off.

Cell legend: **OK** = directly read from crops. **OK=** = inherited — that region's legacy input is byte-identical to a directly-verified case and the binding code path was directly verified on ≥3 other cases (per-case legacy diffs computed programmatically; see §4 caveat 1). **OK—** = region correctly renders missing-data placeholders. **OK\*** = template-static region, spot-checked (see caveat 2). **K0** = KNOWN-0 present. **F-nn** = finding applies. `(a,b)` = OBS-A/OBS-B apply (all heroes).

| Case | TB | HERO | ATR | SL | SS | BD | OC | VP | KY | ID | PT | PAR |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 01 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK | OK | OK |
| 02 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK | OK | OK |
| 03 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK | OK* | OK |
| 04 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03,F-04 | OK | OK | OK | OK | OK* | OK |
| 05 | OK | OK(a) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 06 | OK | OK(a,b) | K0 | OK— | OK— | F-02,F-03 | OK | OK | OK | OK | OK* | OK |
| 07 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03,F-05 | OK= | OK= | OK= | OK= | OK* | OK |
| 08 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03,F-05 | OK | OK,F-06 | OK | OK | OK* | OK |
| 09 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK | OK |
| 10 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 11 | OK | OK=(a,b) | K0,F-07 | F-01 | F-01 | F-02,F-03,F-04 | OK | OK | OK | OK= | OK* | OK |
| 12 | OK | OK=(a,b) | K0,F-07 | F-01 | F-01 | F-02,F-03 | OK= | OK | OK | OK= | OK* | OK |
| 13 | OK | OK=(a,b) | K0,F-07 | F-01 | F-01 | F-02,F-03,F-04 | OK | OK= | OK | OK= | OK* | OK |
| 14 | OK | OK=(a,b) | K0,F-07 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK= | OK* | OK |
| 15 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 16 | OK | OK=(a,b) | K0 | F-01 | OK— | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 17 | OK | OK=(a,b) | K0 | OK— | OK— | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 18 | OK | OK=(a,b) | K0 | OK— | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 19 | OK | OK=(a,b) | K0 | OK— | OK— | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 20 | OK | OK(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK | OK* | OK |
| 21 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 22 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 23 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK,F-06 | OK | OK= | OK* | OK |
| 24 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK= | OK* | OK |
| 25 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK= | OK* | OK |
| 26 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK= | OK* | OK |
| 27 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK | OK | OK= | OK* | OK |
| 28 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03,F-05 | OK | OK= | OK= | OK= | OK* | OK |
| 29 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK= | OK= | OK= | OK* | OK |
| 30 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK= | OK= | OK | OK* | OK |
| 31 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 32 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 33 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 34 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 35 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 36 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 37 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 38 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK= | OK= | OK | OK* | OK |
| 39 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 40 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 41 | OK | F-08(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 42 | OK | F-08(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 43 | OK | F-09(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK= | OK= | OK= | OK | OK |
| 44 | OK | F-09(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 45 | OK | F-09(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 46 | OK | F-09(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 47 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK | OK* | OK |
| 48 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | F-10 | OK* | OK |
| 49 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK= | OK= | OK= | OK* | OK |
| 50 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK | OK= | OK= | OK= | OK* | OK |
| 51 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03,F-11 | OK= | OK= | OK= | OK | OK* | OK |
| 52 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 53 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 54 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |
| 55 | OK | OK=(a,b) | K0 | F-01 | F-01 | F-02,F-03 | OK= | OK= | OK= | OK= | OK* | OK |

PAR column: every legacy value was located in the PNG or its absence is accounted for by a listed finding (KNOWN-0 / F-01 / F-03 tail / F-07 raw target / F-09 hold tail / F-10 warmup tag) or a documented design decision (MTF breakdown row omission; HVN@POC:NO omission; `(legacy ref)` tag on VWAP dev thr dropped — trivial). HERO colour correctness for all 55 verified programmatically (colour sweep, §4 caveat 3).

---

## 3. Regression confirmations (gap-fix items + v30 arms + KNOWN-0)

Confirmed still fixed / behaving per spec:

- **Q2** risk/rwd: present on structural cards (but F-01 truncates the rwd value) and emitted on ATR rows (but KNOWN-0 clips the line). The binding landed; the pixels don't.
- **C1b** dedup: single in-cell CAPPED (`CAPPED → price` + reason label) — no duplicate second label (11–14).
- **C1c** stop-deeper prefix: all arms verified — prefix on the side(s) whose structural stop is deeper (01 both, 16 long-only, 18 short-only, 17 both), **ATR-deeper fallback (no prefix) both sides on 20**, no-data arm (06, 19).
- **C2a** structural column order STOP | ENTRY | TARGET ✓ (all cases).
- **C3a/C3b** uniform sub-section box widths + L/R header alignment ✓ (01, 03, 08, …).
- **C3c.i/ii/iii** `DYNAMIC NORMS [LIVE]`, `Vol threshold: H=2.00x | M=1.30x`, `ATR scale: 1.00x (ATR=80.00 | ref=80.00)` ✓ (exact at ATR=5000 too, case 20).
- **C3d/e/f/g** `REGIME (5m)`, `BBW / TTM SQUEEZE`, `EMA RIBBON (1m)`, `MTF GATE (15m)` headers ✓.
- **C3h.i** `Config: Enabled=Y | Soften=+1 | Amplify=-1` ✓.
- **Q3a** Volume note `1.60x SMA | $8.0M | 100.0000 BTC` ✓ (+ `$450` / `$48.5K` formats, 54/55).
- **Q3c** OBV note `RISING | div: NONE` ✓; **Q3e** CVD div surfaced `+12,500 | div: NONE` ✓; **Q3f** RSI `64.0 > 50.0` ✓ (side-aware, see F-04 edge); **Q3i** `none | TTM=BULL_BUILDING` ✓ (case-correct but lowercase per F-02); **Q3j** `9>21>50 = BULL alignment` ✓ with correct bear mirror `50>21>9 = BEAR alignment`; **Q3m** sub-note emphasis (semibold, brighter) ✓ — but see F-03 truncation.
- **C4a** `Donchian(20)` ✓; **C4b** Best vol. pivot expansion ✓ shipped but truncates (F-03); **C4c.i** `NW LNGS` ✓ but `NW SHRTS` truncates (F-05); **C4d** `EMA200 (5m)` ✓.
- **C5a/C5b** `Near HVN support` green / `Inside VA` ✓ capitalised + coloured (resist colour deviates — F-06).
- **C6** DMI/ADX note `ADX=32.5 | +DI=28.0 | -DI=14.0` ✓ (SC clamp remains Spec-C-deferred, locked).
- **Q4a** HVN@POC: NO omitted by design ✓; **YES arm surfaces** as `(HVN@POC)` beside POC (26) — better than the triage assumed.
- **Q4b** LVN walls: `LVN↑/LVN↓` slots render — populated (23: 49870.0) and em-dashed ✓.
- **Q3l/Q3h** locked omissions intact: no MTF row in card breakdown (all cases); VWAP Bands display-only row SC `—` ✓.
- **v30 arms:** sub-tick CAPPED suppression (15: plain `Target 50159.0`, no amber) ✓; negative-zero funding clamp (40: `0.0000% · NEUTRAL`) ✓; Kelly pluralisation/`< 1 contract` arms ✓; `[BIAS ONLY — NO TRADE]` + `Lean:` arm ✓ (04, 21, 22, 26); structural per-side missing notes ✓ (condensed — OBS-F).
- **KNOWN-0**: confirmed as the open baseline defect, all 55 cases, with the arm inventory in §1 — not re-reported as new.
- **Colour system:** verdict ramp exact on all 55 (programmatic); SCORE gauge arc follows dominant side (green long / red short / slate NO TRADE); regime header colours (UP green / DOWN red / RANGE amber / TRANSITIONAL grey); MicroCVD 4-state pills (BULL+/BULL−/BEAR+/BEAR− with side-correct colours); OI×CVD badges (CONFIRMED LONG green / CONFIRMED SHORT red / NEUTRAL slate / CONFLICT amber + penalty); funding HEAVY/RISE amber crowding pills; spread meter WIDE red / TIGHT green; TRANSITIONAL penalty line (10: `eff. L 8/15 | S 2/15 · penalty −2`) ✓; MTF BLOCK chip + red details header with verbatim reason (05) ✓.

---

## 4. Caveats

1. **Inheritance protocol (OK= cells).** The 55 cases share a synthesized baseline; per-case legacy diffs (computed programmatically) show each case varies 1–5 lines from it. Cells marked OK= were not re-read as pixels for that case; their legacy input is byte-identical to a directly-verified case and the binding was directly verified on multiple other cases. The binder is deterministic, so identical input ⇒ identical pixels (the one known exception — the wall-clock in LAST PRICE — is OBS-A). Direct reads: 38/55 breakdown TOTALs (all exact), all 10 ATR-variant cases, all VP/KELLY/details variants, all four HOLD heroes, both ANCHOR heroes.
2. **PT column (bottom block).** The block renders the legacy RTF output whose text content the 2026-05-28 harness already machine-verified 55/55 against snapshots. Pixel-rendering of the block was directly read on 6 cases spanning the distinct shapes (01, 02, 09, 43 + spot-scrolls); remaining cases are marked OK* (template-static). The block scrolls; only its top ~22 lines are visible in the capture — content below the fold was not pixel-verified (it is text-verified).
3. **Colour sweep.** Verdict/context colour verification for all 55 heroes was done programmatically (dominant saturated pixel in the text region vs Theme hex, ±16/channel quantisation), not by eye. Script embedded in the session transcript; trivially re-runnable.
4. **Arms this artifact set cannot test** (need a live run or a regenerated set): MTF reason format 3 — `MTF state: …` exists only inside the legacy breakdown row here, never in a card-bound field; KELLY no-`[CAPPED]` arm (22's slug intends it but legacy itself prints `[CAPPED]`); SHORT_CASCADE Liq pill (F-11 mirror); R:R `< 0.1` display literal; HOLD chip with an actually-declared position (harness leaves the radios at No Position and bypasses the gate); post-v31 MTF reason strings (set predates v31 — OBS-B's `[—]` placeholder may vanish with composed reasons); ALIGNED context tag (harness pins CONFIRMED on NO TRADE rows; v30 relabel lives in the engine, so cards correctly mirror legacy here).
5. **Freshness.** Set generated 2026-05-28; predates the 2026-06-10 Tier C commits (no happy-path card rendering touched) and the 2026-06-11 v31 engine pass (changes MTF reason composition, trade ordering, OBV/Donchian semantics — none of which alter card *binding* code, but reason-string content will differ live).
6. **Severity judgement on F-07** is the triage owner's call: I rated HIGH because the displayed 1:1.7 is a correct *ATR-basis* number in a misleading *position*; if the trader reads the R:R cell as "R:R of the displayed target", it is functionally a wrong value (CRITICAL by the brief's definition).

---

## 5. Suggested consolidation for the fix kickoff (non-binding)

KNOWN-0 + F-01 + F-08 + F-09 are one family: **content lines exceeding their pixel budget** (vertical, horizontal, banner, chip). The ATR-clip kickoff's fix (raise row-4 budget) covers KNOWN-0's zone rows; the same pass should size-audit the STRUCTURAL R:R cell (F-01), the anchor banner width/flow (F-08), and the HOLD chip (F-09 — or move the full reason into a wrapping row; note the P5b dependency). F-07 (capped-row content) and F-10/F-11 (binding gaps) are small binder edits in `MainForm_Render_Cards.vb`. F-02/F-03/F-05 are formatting decisions the trader should rule on before any code (accept-as-is vs fix). F-04/F-06 + observations are triage-and-document.

**End of report.** Review artifacts: `verify\p5-review-crops\` (crop.ps1 + ~450 region crops + worksheet.md with batch-by-batch notes).
