# Adversarial review of `indicator-calibration-review-2026-10-02.md`

> ✅ **RULED 2026-10-07 (UTC), trader, in their words** (after the orchestrator's read of this doc):
> 1. *"Agreed with your reads; ICR-4 (b)"* → **`ICR-1` (a) · `ICR-2` (a) · `ICR-3` (a) · `ICR-4` = (b)**, AUD-19 ships on its own burst-path boundary after burst run 2 (the self-describing option; ⚠ the `AVR-2` (c) threshold must then be validated on the decayed instrument) **· `ICR-5` (a) · `ICR-7` (a) · `ICR-8` (a).** `ICR-6` see item 3.
> 2. *"Volume design point = Follow the engine's dynamic high threshold."* → the forming-bar spec (`ICR-1`) keeps the `DynamicNorms` high threshold (about 3.4–3.9× raw on closed bars, about 4.5× in NY), NOT `trader-profile.md` §3's 3× SMA(9). ⚠ `trader-profile.md` §3 (Volume SMA row) now disagrees with the engine by ruling; the profile row is annotated, not rewritten.
> 3. *"ICR-6 = Please propose the most accurate/truthful option."* → **ADX half: (a), chart parity first.** **RSI half: the orchestrator's proposal is §1b below, awaiting the trader's confirmation** (a scoring design point, reserved).
> 4. *"ICR-7 = Yes, but this must be revisited once the order app's fixes this."* → **`ICR-7` (a)** applied to `DeribitIndicatorProject.md` §5a the same day; ⏰ **revisit when the order app fixes audit rows A8 and A17** (`adversarial-audit-2026-09-24.md` §B) and realised fills exist (`W6-6`).

**Written:** 2026-10-07 (UTC; `date -u` read 20:09 at seat start) by a fresh seat, as the trader ruled ([`seat-handover-2026-10-07.md`](seat-handover-2026-10-07.md) §0 row 6).
**Reviews:** [`indicator-calibration-review-2026-10-02.md`](indicator-calibration-review-2026-10-02.md) (the "review pack" below). Brief: attack the order in `indicator-calibration-review-2026-10-02.md` §7 and the classes in `indicator-calibration-review-2026-10-02.md` §6, answer the questions in `indicator-calibration-review-2026-10-02.md` §8, and check the carried claims in `indicator-calibration-review-2026-10-02.md` §9.
**Class:** analysis only. No engine code, settings or scoring changed. One new read-only tool: [`tools/ops/indicator_review_reads.py`](../tools/ops/indicator_review_reads.py).
**Pinned to:** commit `92c60d9` (HEAD at seat start), settings v69. **Data:** fetch `aws_fetch/20261007-180541` — `analysis_log.csv` (124 columns, 2026-09-24 18:46 → 2026-10-07 18:05 UTC), its rotated `.bak` (116 columns, 2026-09-01 15:50 → 2026-09-24 18:43 UTC), `analysis_eval_cache.csv` and the box trade store. Weekday rows only, the house scope.

---

## IDs used in this doc

| ID | Source and kind | Meaning |
|---|---|---|
| `ICR-1` … `ICR-8` | This doc §1, decisions for the trader | New. Prefix checked free in `docs/`, `Core/`, `tools/`, `verify/` on 2026-10-07 |
| A1, C1, C4, D1, D6, F5 … | Rows of [`adversarial-audit-2026-09-24.md`](adversarial-audit-2026-09-24.md) §B (defects) | e.g. C1 = the live OHLC cache freezes forming-bar stubs; D6 = MicroCVD in the exit logic |
| C-1 … C-21 | Decisions of `adversarial-audit-2026-09-24.md` §C | e.g. C-6 = 15m data fail-closed; C-14 = the exit tools and live surfaces |
| ⚠ **C14 vs C-14** | Row vs decision, same audit | **Different things.** Row C14 = "session cells drop the last UTC hour" (S3). Decision C-14 = "no design change to D1–D7 until the real-tape false-latch rate exists". The review pack uses both without saying so |
| AUD-07, -12, -13, -14, -19 | Findings of `adversarial-audit-2026-09-24.md` §3 (line-level audit) | 15m gate fails open · OFI fold weights · VWAP session fallback · 5m pivot repaint · burst read as of the last trade |
| `L-7`, `X-3` | Lane and external check, `adversarial-audit-2026-09-24.md` §D | `L-7` = the independent review of the audit. `X-3` = the exit guard's false-latch rate on real tape |
| `AT-6`, `AT-L4`, `AT-L6` | Decisions of [`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §7 and §7a | `AT-6` (b): a scoring fix off the burst path may ship during burst run 2, with an era stratum; a burst-path fix is held. `AT-L4`: `L-7` runs before any reserved audit fix ships. `AT-L6`: `X-3` runs before C-14 is ruled |
| `F-2` | Fix class in [`medium-tier-diagnosis-read-2026-09-17.md`](medium-tier-diagnosis-read-2026-09-17.md) §3 | The vote-value study. ⚠ Not `F2`, a loose-end sweep finding in `DeribitIndicatorProject.md` §15 |
| `D-C` | Ruling in [`job2-read-2026-07-31.md`](job2-read-2026-07-31.md) §3 | Session-volume calibration parks behind the forming-bar question. ⚠ Audit row D1 (pivot repaint), the tick queue's "D1 TTM" and the POC-gate ruling `D-1` are three more different things |
| `D-9` | Question in `medium-tier-diagnosis-brief-2026-09-16.md` §3 | The path-into-MEDIUM question. Its episode rule: same-side signals, each within the session's main window of the previous one |
| `AVR-2` | Decision in [`aggr-vel-burst-rederivation-read-2026-09-26.md`](aggr-vel-burst-rederivation-read-2026-09-26.md) | No burst threshold change until an outcome read; option (c) is the threshold change |
| `W6-7` | Row of [`roadmap.md`](roadmap.md) §3 W6 | The bar a new indicator must clear: an orthogonal signal class, not another angle |
| Options A–D | [`forming-bar-live-investigation-2026-07.md`](forming-bar-live-investigation-2026-07.md) §4 | A = closed-bar slice for every indicator · B = stub-aware variant per indicator · C = status quo · D = log `TriggerMode` first |
| `HSR-7` | Trader ruling in [`history-store-queue-reshape-proposal.md`](history-store-queue-reshape-proposal.md) | Re-gates the TTM unit fix on the history store and the synthesizer's clearance |
| `W6-4`, `W6-5`, `W6-6` | Rows of `roadmap.md` §3 W6 | Offline ceiling audit (ran inconclusive) · per-indicator weights (B1, parked) · bridge realised-outcome calibration |
| L9 | `roadmap.md` / `DeribitIndicatorProject.md` §12 | The placed-stop un-clamp, gated on the order app |
| S0, S3 | Severities in `adversarial-audit-2026-09-24.md` §A | S0 = a wrong-side, unprotected or unbounded order on the exchange under shipped config. S3 = an edge case, a latent path or an advisory inconsistency |
| Q1 … Q6 | The questions in `indicator-calibration-review-2026-10-02.md` §8 | Answered in this doc §2 |
| `H-n` / `E-n` | This doc §8 | `H-n` = a handle the reader can run · `E-n` = evidence the reader cannot re-run |

---

## 0. Verdict

- **The spine of the review pack's `indicator-calibration-review-2026-10-02.md` §7 order is right:** measurement fixes, then daylight saving, then mechanism fixes, then the `F-2` vote-value study, then additions. Three things are wrong with the rest.
  1. **It splits one root cause across three classes.** The forming bar (the in-progress last candle the engine reads at an on-close fire) drives the volume vote, the CVD divergence gate (new here), the 5m pivot repaint, OBV, VPFR, ATR and the eval-cache stubs (row C1). The pack files them as class A, class B and measurement. **Rule them as one decision** (`ICR-1`).
  2. **It omits queued work and gates that outrank class A:** the audit scoring fixes and the liquidation park build (`seat-handover-2026-10-01.md` §3, items 5 and 6), `L-7` before any audit fix (`AT-L4`), `X-3` before C-14 (`AT-L6`), and the burst-path hold (`AT-6` (b)).
  3. **It ranks by overfitting risk alone.** Measured on the live book, the class A fixes range from zero logged rows (AUD-13, the 15m fail-open) to roughly a tenth of all rows (the volume vote, once it can fire) (this doc §3 table). The CLAUDE.md gate is blast radius. **Measure reach and value offline before a fix takes a scoring slot** (`ICR-2`).
- **Three carried figures are stale** (this doc §5): the volume vote fires less than the pack says; CVD divergence no longer "under-fires 8× on 3-min" — it is near-dead at both resolutions, and the cause is the forming bar, not a unit; the stop clamp binds 90–95 % in LONDON and ASIA, not 95–100 %.
- **The main conclusion of `indicator-calibration-review-2026-10-02.md` §2 survives audit rows C2–C4** (`indicator-calibration-review-2026-10-02.md` §8 Q2). Row C3 does not apply to the read it rests on. Three caveats remain (this doc §2 Q2).
- **Two class A rows are misfiled.** MicroCVD (row D6) is an exit-logic defect with zero scoring effect. Decision C-14 is the exit-tools decision, gated on `X-3`; it does not queue the scoring pivot fix.
- **AUD-13 fired on 0 of 143 rows in the first four minutes after a VWAP anchor. AUD-19 sits on the burst path** and rides with the burst threshold decision. **AUD-12 needs a measurement before a queue row** (this doc §2 Q3).
- **Class D:** the 1h bias fails the pack's own `W6-7` gate as written. The swing-breakout event is double counting if it is added as a vote (this doc §2 Q5).

---

## 1. Decisions for the trader

Each is a sequencing or scoping ruling on reserved work (scoring or a dataset boundary), so each comes to you. `ICR-2` and `ICR-5` ask only for offline reads, which are auto-proceed class, but they reorder ruled work, so they come too.

| ID | Decision | Options | My read |
|---|---|---|---|
| `ICR-1` | How to scope the forming-bar fixes | (a) **One forming-bar spec for the whole family:** volume vote, CVD divergence gate, 5m pivot right wing (AUD-14), OBV normaliser, VPFR recency slot, ATR last step. Each indicator gets its own option (A, B or C) and its own D-table row; the measurement half (row C1, decision C-9) stays with C-10 as ruled · (b) the pack's split: volume as class A, pivots under C-14, CVD divergence as a class B unit fix · (c) volume vote only now, the rest later | **(a).** One root, one spec, one explicit choice per indicator. (b) misdiagnoses the CVD gate and files the pivot under an exit-tool decision gated on `X-3`. (c) leaves CVD divergence silent for the same reason the volume vote is silent. The spec must also rule the volume design point: on closed bars the dynamic high threshold sits near 3.4–3.9× raw, about 4.5× in NY after its 1.15 multiplier (carried from [`candle-store-derivations-2026-07-31.md`](candle-store-derivations-2026-07-31.md) §3.1), not the trader's 3× SMA(9) |
| `ICR-2` | Measure before the forming-bar fix takes a scoring slot | (a) **Offline reads first:** per indicator, the closed-bar fire rate and the share of rows that change; and the closed-bar volume vote's and CVD divergence's agree/oppose net EV on the existing live rows, by the `medium-tier-diagnosis-read-2026-09-17.md` §2.3 method · (b) ship on mechanism grounds, no outcome read · (c) leave value to `F-2` | **(a).** The pack calls the volume fix "the single most valuable" with no outcome evidence: the vote has never fired, so no read exists. The candle store and the swing read's walk can produce one with no scoring change. (b) spends a scoring boundary blind |
| `ICR-3` | Shipping shape for latent fixes that move almost no rows | (a) **Bundle the measured near-zero fixes** — C-6 (15m fail-closed; 0 rows with no 15m data in 22,710) and AUD-13 (VWAP fallback; 0 of 143 rows in the first four minutes after an anchor) — into the first audit scoring boundary, with your sign-off (`roadmap.md` §5 rule 1) · (b) one boundary each · (c) leave them unfixed | **(a).** A separate boundary for a fix that changed no logged row in five weeks buys an attribution read with nothing to attribute: the richer option measures nothing. Both stay scoring changes, so both stay reserved |
| `ICR-4` | When AUD-19 (burst decay) ships | (a) **With the `AVR-2` (c) threshold change**, after burst run 2's per-session reads, with an offline decay replay on the trade store in the (c) spec · (b) its own burst-path boundary after run 2 · (c) leave it (S3) | **(a).** `AT-6` (b) holds both until run 2 is read. One change to the burst instrument, with the decay's own effect measured offline first, gives the attribution (b) would give at one boundary instead of two |
| `ICR-5` | AUD-12 (OFI fold weights) | (a) **Measure first:** replay the 100 ms book (the raw-book probe's 24 h in `C:\DeribitData\rawbook-run1\out\` if it carries the snapshots) through both fold rules and count OFI signal flips at run instants · (b) queue the fix now · (c) leave it (S3) | **(a).** OFI carries the only CONFIRMED order-flow vote (ASIA, carried window), and the v48 thresholds were fitted on the shipped fold. A blind fix moves both |
| `ICR-6` | The framing of the queued ADX/RSI re-anchor read (ruled 2026-10-01; not re-opened) | (a) **ADX: test chart parity first** — the engine's ADX(9) on 5m against the trader's chart DMI(9,9). Re-anchor only on a parity failure, because `trader-profile.md` §3 states the design point (ADX < 20 range, > 25 trend). RSI 60/40: you state the entry-vote design point, because `trader-profile.md` §3 uses RSI in trade management, not at entry · (b) as queued: re-anchor all four levels to a stated share per session and resolution | **(a).** The OBV precedent (v66) re-anchored an engine-internal key with a documented design rate. ADX 25/20 has a documented design point too, and it is your chart. `CalcDMI` is standard Wilder smoothing (read at `Core/Indicators_Momentum.vb:38-95`), so parity is likely; I did not test it against chart values |
| `ICR-7` | The net EV per session-day episode rule (`DeribitIndicatorProject.md` §5a) | (a) **Restate it as an engine-side signal-episode model and add the bridge filters it omits:** confidence tiers (HIGH + MEDIUM only), flat-only, exits by the placed levels with no eval-window end, and the named cooloff value. Drop "which is how the bridge trades" until the order app's rows A8 and A17 are fixed and fills exist (W6-6) · (b) keep the wording | **(a).** As worded, the rule counts WEAK rows the bridge refuses and ends trades at a window the bridge does not have. `DeribitIndicatorProject.md` §5a is your ruled vocabulary, so the wording is yours |
| `ICR-8` | Where the forming-bar boundary sits against the audit scoring fixes | (a) **Audit stop geometry first** (C-1 engine half as a payload flag, C-2, C-3), then the forming-bar family · (b) the pack's implied order: forming-bar family straight after daylight saving | **(a).** Row A1 is an S0 path, and every outcome read — including the `ICR-2` reads and `F-2` — is computed at placed geometry. Settle the geometry, then measure and fix the votes |

**Decision-bias tripwire (harness 6):** see this doc §1a — my labels were written before the run.

### 1a. Harness 6 result

Run 2026-10-07 20:35 UTC on the eight rows at `26c6629`, after my labels were written and committed to the baseline file. Files: `docs/harness-runs/decision-bias-20261007T2150Z-icr-{population,baseline,jev}.json`. 40 Jev calls, 32,148 input tokens.

| ID | My label (written first) | Jev (5 of 5 samples each) | Mean top p |
|---|---|---|---:|
| `ICR-1` | no_richer_option | richer_option_wrong | 0.856 |
| `ICR-2` | no_richer_option | richer_option_wrong | 0.544 |
| `ICR-3` | richer_option_wrong | richer_option_wrong | 0.852 |
| `ICR-4` | **gives_up_for_economy** | richer_option_wrong | 0.852 |
| `ICR-5` | no_richer_option | richer_option_wrong | 0.748 |
| `ICR-6` | richer_option_wrong | richer_option_wrong | 0.562 |
| `ICR-7` | no_richer_option | richer_option_wrong | 0.770 |
| `ICR-8` | no_richer_option | richer_option_wrong | 0.708 |

- **Jev raised no economy flag.** The no_richer_option / richer_option_wrong disagreements both mean "no trade of information for work", so they do not change any read.
- ⚠ **My own label flags `ICR-4`.** Option (b), its own boundary, leaves a self-describing edge in the live data that isolates the decay fix. My pick gives that up for one burst-path boundary instead of two, and leans on an offline replay for the attribution. **That is the economy class by `CLAUDE.md`'s three-step test. Treat (b) as the more truthful option**; I still read (a), but the call is yours with that trade stated.
- **Harness 4 (doc scanner) on this doc**, key-less pass at `26c6629`: 0 version, value and pointer candidates; 0 missing-member and 0 line-past-end flags. All four Jev arms had 0 items, so the scanner exits `ENUMERATOR_SUSPECT` — by design a zero is a tripwire, not a clean bill.

### 1b. `ICR-6` RSI half — the orchestrator's proposal (2026-10-07; the trader asked for "the most accurate/truthful option")

**Facts, read in code and settings at `a87eeb4`:**
- The entry vote is a **momentum** vote: long on `r.RSI > overbought` (60), partial long on `55 < RSI ≤ 60`; mirror for short (`Core/ScoringEngine_Calculate_Scoring.vb:182-185`).
- `r.RSI = CalcRSI(candlesExec, 9)` (`UI/MainForm_Analysis.vb:264`), Wilder smoothing (`Core/Indicators_Momentum.vb:127-145`). So it runs on **1-minute bars in NY and 3-minute bars in ASIA and LONDON** (v36 execution resolution). `resolution_profiles["3"]` carries no RSI key: 60/40 applies at both.
- `trader-profile.md` §5 states one RSI rule: hold while RSI(9) > 60, exit below 40 — "momentum intact", on the trader's **1-minute** chart.

**Options:**

| Option | What the design point is | Problem |
|---|---|---|
| **(a) Trader-rule anchor, carried across resolution** | 60/40 (partial 55/45) on 1-minute bars ARE the design point: the trader's own momentum-intact line. On 3-minute bars, the levels are the ones that mark the **same market state**: per session, on closed bars from the candle store, the 3-minute RSI(9) levels whose exceedance share over the same minutes equals the 1-minute RSI(9) > 60 (< 40, and the partial pair) share. Plus an RSI(9) parity test against the trader's chart, as for ADX | Needs new per-resolution RSI keys in `resolution_profiles["3"]` (a settings change, reserved). The mapping is a time-matched quantile match, not an outcome fit |
| (b) Fire-share re-anchor (as queued 2026-10-01) | A chosen target share per session and resolution | The target share is a new number nobody has stated; it replaces the trader's rule with an invented one |
| (c) Outcome-fit the levels | Whatever maximises net EV | Overfitting; it is `F-2`'s job (vote value), not a design point |
| (d) Keep 60/40 at every resolution | The trader's 1-minute rule, applied unchanged to 3-minute bars | RSI(9) on 3-minute bars spans 27 minutes, so 60 there marks a different state than on the trader's chart. The vote's meaning differs by session with nothing in the code saying so |

**Read: (a).** It is the only option where every level traces to something the trader stated, and where the code would say what is true: 1-minute levels are the trader's; 3-minute levels are derived to mean the same thing. (d) is cheaper and silently changes the meaning by resolution. Order: inside the ADX/RSI read (merged order step 8, this doc §4), after the forming-bar family, on closed bars only.

**Harness 6** (2026-10-07 21:13 UTC, rev `eaf6f62`, my label `no_richer_option` written first): Jev `richer_option_wrong`, 5 of 5, mean top p 0.728. No economy flag. Files `docs/harness-runs/decision-bias-20261007T2113Z-icr6-*.json`.

⚠ **Not verified:** how far the 3-minute levels would move (not measured); whether the trader's chart RSI uses Wilder smoothing (code read only).

---

## 2. Answers to the review pack's `indicator-calibration-review-2026-10-02.md` §8 questions

### Q1 — Is the volume-vote defect still as described?

**The mechanism is unchanged. The fire rate is lower than the pack says.**

- **Code, at `92c60d9`:** `UI/MainForm_Analysis.vb:266-269` sets `VolumeRatio = candlesExec.Last().Volume / CalcVolumeSMA(candlesExec, 9)`; `DynamicNorms.vb:36-60` builds the thresholds from the last 100 completed bars and excludes the forming bar; `Core/ScoringEngine_Calculate_Scoring.vb:215-219` votes on `VolumeRatio >= VolHighThreshold` plus ROC and VWAP agreement.
- **No commit since 2026-07-30 touches the path.** `git log` over the four files shows no change to these lines; the only `-S'VolumeRatio'` hit in engine code (`fded077`) is `bestPivotVolumeRatio`, unrelated (`H-2`).
- **The line citations in `roadmap.md` W1 are stale:** `MainForm_Analysis.vb:237` is now `:266-269`.
- **Fire rate, measured** (`H-1`, read `volume`). Upper bounds: every row at or above the clamp-minimum threshold times the session multiplier. The live threshold is at least that high.

| Book | Session | Rows | VolumeRatio p50 | Partial possible (≥ mid floor) | Full possible (≥ high floor, side agrees) |
|---|---|---:|---:|---:|---:|
| 2026-09-24 → 10-07 | NY | 5,886 | 0.0178 | ≤ 0.19 % | ≤ 0.03 % |
| 2026-09-24 → 10-07 | LONDON | 876 | 0.0060 | ≤ 0.11 % | ≤ 0.00 % |
| 2026-09-24 → 10-07 | ASIA | 1,440 | 0.0072 | ≤ 0.14 % | ≤ 0.07 % |
| 2026-09-01 → 09-24 | NY | 10,580 | 0.0091 | ≤ 0.39 % | ≤ 0.10 % |
| 2026-09-01 → 09-24 | LONDON / ASIA | 1,488 / 2,440 | 0.0025 / 0.0021 | ≤ 0.07 % / ≤ 0.08 % | ≤ 0.00 % / ≤ 0.08 % |

- The pack's "0.69 % NY, 2.66 % at execution resolution 3" was an actual fire rate on the July book. Today's upper bounds are below it in every session. The July res-3 figure carried backstop fires in late bar phase; those are gone.
- **The investigation's Option D has shipped.** `TriggerMode` has been a CSV column since the 2026-09-24 rotation. NY: 5,877 of 5,886 rows are `ON_CLOSE`. The roll-versus-backstop confound that `forming-bar-live-investigation-2026-07.md` §3.1 could not resolve is resolved. The forming-bar decision has its data.

### Q2 — Is "the score does not rank outcomes" robust to audit rows C2–C4?

**Yes against C2–C4. Three caveats remain, and the pack should carry them.**

| Audit row | Applies to the medium-tier read? | Why |
|---|---|---|
| C3 (walk starts at T+3, entry priced at T) | **No** | The read's main outcome is `SwingFallbackRead`'s `W<max window>` walk (`TierOrderStability.vb:61`), which starts at `RowMin + 1` with bars keyed by close time (`SwingFallbackRead.vb:25`, `:383`): the 1-minute bar that contains the signal. The `+3` lives in `analysis/ForwardWindowJoiner.vb` and one other `SwingFallbackRead` mode (`:708`), neither used here |
| C4 (minute rows counted as independent trades) | **No for the CIs** | The read's CIs bootstrap whole UTC days, the house method. Row dependence inflates significance, so it can produce a false finding, not a false null |
| C2 (no fee-inclusive EV; the loss path is taker) | **Mostly no** | The read is net of a maker/maker 3 bps round trip. A taker loss path adds about 2 bps per loss. Success rate rises only at the top bin (NY ≥ 0.70: 45.9 % against 38.7–41.5 %), so the extra cost falls slightly more on low bins. My estimate is a slope change under +0.1 bps per 0.1 of regime max, inside the CI. **Not measured** |
| C1 (stub OHLC cache) | No | The read walks venue OHLC fetched by `DeribitOhlcFetcher`, not the live cache |

**Caveats the pack drops:**

1. **The slope is row-weighted.** Long episodes weigh more. The read ran first-in-episode only for MEDIUM − WEAK (−0.5 [−3.3, +2.4]), not for the slope. An episode-level slope is a cheap re-run of `tools/ops/medium_tier_diagnosis.py`.
2. **It holds at current geometry only.** Success rate rises at the top bin while net EV does not, and the read leaves the reason open (`medium-tier-diagnosis-read-2026-09-17.md` §5). If target geometry spends the ranking, "moving thresholds will not lift net EV" is true today and may stop being true after C-2, C-3 or the L9 un-clamp.
3. **The two vote findings are thin.** 2 outcome findings CONFIRMED in 780 comparisons, 0.56 false CONFIRMED expected; the read calls each "one replication away from certain". The NY VPFR long finding also predates the 2026-09-24 POC-gate fix, which moved 1,288 placed targets.

**Kelly is the weaker leg of `indicator-calibration-review-2026-10-02.md` §2, not the stronger.** The v69 Kelly book reads the live eval cache. Its OHLC bars are stubs (row C1, confirmed in code: `LivePerformanceTracker.vb:700-710` adds a bar only when `CloseTime > maxExisting`, so the completed bar never replaces the stub). Touches still register — live NY outcomes since 2026-09-01 are 34.6 % success, 47.9 % adverse, 17.4 % expired (`H-1`, read `eval`) — but they register on one sampled price per minute. **The medium-tier read's negative net EV in every NY score bin (−3.1 to −4.8 bps) is the stronger evidence** that the book's edge is negative at current geometry; cite it first.

### Q3 — Are AUD-12, AUD-13 and AUD-19 material enough to queue?

All three are present at `92c60d9` (read in code). Measured reach:

| Finding | Code | Reach on the live book | Read |
|---|---|---|---|
| AUD-13 VWAP session fallback | `Core/Indicators_Volatility.vb:38` | **0 of 143** rows in the first 4 minutes after the 00:00 and 13:30 anchors used the fallback (`H-1`, read `vwap`). On-close rows are stamped 1–5 s after the close, when the new bar exists | **Rightly S3.** A latent path on REST and interval mode. Bundle it (`ICR-3`); no queue row of its own |
| AUD-19 burst read as of the last trade | `Core/AggressorVelocityAccumulator.vb:127-150` (no decay to read time) | Last trade ≥ 2 s before the row stamp on 26 of 193 NY BURST rows (2026-09-24 → 10-07) and 84 of 487 (September). **A loose upper bound:** the stamp trails the snapshot by up to about 5 s. BURST rows are 3–5 % of NY rows | **S3 at the row level, but on the burst path.** `AT-6` (b) holds it until burst run 2 is read. Ship it with `AVR-2` (c) (`ICR-4`). The run-2 test validates the shipped, undecayed instrument, which is what is live |
| AUD-12 OFI fold weights | `Core/OfiAccumulator.vb:83-91` (the new sample takes `alpha(dt)` of the gap before it) | **Not measured.** The CSV cannot show it; it needs a book-stream replay | **Measure first** (`ICR-5`). `r.OFIRatio` is the accumulator's value on WS (`UI/MainForm_Analysis.vb:418-422`), so the fix moves the scored OFI vote, the only CONFIRMED order-flow vote, and the v48 baseline |

### Q4 — Should the swing-pivot fix come before the volume vote?

**No.**

- **Measured reach is small.** Inside one forming 5m bar, `LastSwingHigh5m` or `LastSwingLow5m` changed on **0.68–0.70 %** of NY row pairs and **1.7–2.7 %** in ASIA and LONDON (`H-1`, read `repaint`; an upper bound).
- **The stop channel is nearly inert.** The clamp binds on **99.1–99.5 %** of NY structural-stop rows and **90.0–94.6 %** in LONDON and ASIA (`H-1`, read `clamp`). The pivot reaches placed stops on 0.5–10 % of them.
- **Targets are the real channel.** Swing targets place on **31–50 %** of directional rows (`H-1`, read `targets`).
- **The fix is bigger than the defect.** AUD-14's own fix (`scanEnd = Count − 2 − wing`) delays every pivot by up to one 5m bar, not only the repainted ones. Its blast radius is far larger than 0.7 % and is **not measured**. A narrower design exists — keep a pivot once the forming bar confirms it, and mark it broken when the forming bar takes it out. That is a choice for the `ICR-1` spec.
- **C-14 cannot carry it.** C-14's audit read holds D1–D7 until `X-3`, and `AT-L6` ruled `X-3` first. The scoring half of AUD-14 belongs in the forming-bar spec, where the order question disappears.
- **If you rule them apart anyway, volume first:** a PREFERRED indicator suppressed by one to two orders of magnitude (NY ≥ 3× SMA: 0.05 % live against 8.47 % on closed bars, the latter carried from `forming-bar-live-investigation-2026-07.md` §3.4) outranks a ≤ 0.7 % repaint.

### Q5 — Do a 1h bias or a swing-breakout event conflict with `trader-profile.md` §4?

- **1h bias: no `trader-profile.md` §4 conflict. It fails other rules.**
  - `trader-profile.md` §8: "Do not propose changes that increase indicator correlation."
  - `roadmap.md` §3 W6: "refuse new-indicator proposals that don't clear the W6-7 bar (orthogonal signal CLASS …)". A 1h trend bias is another angle on trend.
  - The engine already has a higher-timeframe anchor: EMA(200) on 5m spans about 16.7 h, and the 15m MTF gate reads 70 bars, about 17.5 h.
  - The pack gates class D on `W6-7` and then lists a candidate that fails it.
- **Swing-breakout event: it depends on the form.**
  - **As an extra vote, it is double counting.** It re-votes ROC and volume, which already vote in Step 2: the pattern removed in v0.17 for funding (`trader-profile.md` §4, "Funding OK in Step 2 scoring"). Push back on that form.
  - **As a replacement** (an AND-gate instead of the separate parts), it is a combination change: `W6-5` (B1 weights) territory, parked by `W6-4`. That parking rests on CeilingAudit, which row C18 says cannot measure the question (decision C-19).
  - Either form needs the forming-bar volume fix first. Donchian(20) already encodes a price breakout.
- **Volume-weighted pivots (P1):** its promotion condition needs auto-tweaker output (`DeribitIndicatorProject.md` §16.6 P1). The tweaker is dormant behind decision C-12, so the condition cannot be met as written.

### Q6 — Is the session-day episode rule a fair model of the bridge?

**A fair model of signal episodes. Not yet of the bridge.**

| Gap | Effect on the metric | Source |
|---|---|---|
| The rule takes the "first directional row". The bridge enters only on HIGH and MEDIUM by default and refuses WEAK | Counts trades the bridge never takes | [`signal-bridge-v1-proposal.md`](signal-bridge-v1-proposal.md) line 99 (action mapping R1) |
| Resolution includes "window end". The bridge has no eval window; it holds until its stop or target fills | Ends trades early, so it skips too few later rows | `DeribitIndicatorProject.md` §5a; bridge action mapping |
| Row A8: a stand-down never cancels the working entry, and the chase re-prices it | A later fill lands on a stale signal at another price; the model's entry price is optimistic or wrong | `adversarial-audit-2026-09-24.md` §B row A8 |
| Row A17: a close during a socket gap leaves the cooloff unanchored, so the bridge re-enters at once | More trades than the model | Row A17 |
| Row A2 and row A18: the triggered stop is a chased post-only limit, capped at a fixed 70 USD | Realised losses differ from the placed stop | Rows A2, A18 |
| The cooloff value is not in this repo | The rule cannot be computed as defined | `indicator-calibration-review-2026-10-02.md` §5 |

Read: `ICR-7` (a). The bridge-faithful version needs W6-6 (realised fills).

---

## 3. Attack on the classes (`indicator-calibration-review-2026-10-02.md` §6)

**The pack orders by overfitting risk. Order by root cause, then by measured reach.**

| Item | Pack's class | What it is | Real-tape reach (`H-1`) | Proposed home |
|---|---|---|---|---|
| Volume spike vote | A | Forming bar: numerator is the stub | Full vote ≤ 0.03–0.10 % NY; partial ≤ 0.19–0.39 % | **Forming-bar family** (`ICR-1`) |
| CVD divergence price gate | B ("1-min values on 3-min bars, under-fires 8×") | **Forming bar.** `priceChange = (candles.Last().Close − candles(Count−2).Close) / …` (`Core/Indicators_OrderFlow.vb:344-345`) on `candlesExec` (`UI/MainForm_Analysis.vb:459`): the move in the first seconds of the new bar | **0.12–0.22 % res-1, 0.09–0.15 % res-3.** Near-dead at both; ratio about 1.4×, not 8× | **Forming-bar family** |
| 5m swing pivots (AUD-14, row D1) | A | Forming bar in the right wing (`Core/Indicators_Structure.vb:287`) | Repaint ≤ 0.7 % NY, ≤ 2.7 % res-3 | **Forming-bar family**; the exit-guard half stays with C-14 |
| OBV, VPFR, ATR last step | not listed | Forming bar (`forming-bar-live-investigation-2026-07.md` §2) | Not measured here | **Forming-bar family** |
| Eval OHLC stubs (row C1) | measurement (`indicator-calibration-review-2026-10-02.md` §3) | Forming bar, measurement side | Every NY bar a stub (code read) | C-9, with C-10 (ruled first) |
| MicroCVD (row D6) | A | **Exit logic only:** `ComputeFastExitPrimitives` (`Core/ScoringEngine_Helpers.vb:136-142`) counts BEAR_DECEL as adverse for a long. Zero scoring effect | Not measured (exit guard) | C-14, after `X-3` |
| 15m MTF gate (AUD-07, row A6) | A | Fail-open latent path | **0 rows** with no 15m data in 22,710 | C-6; bundle (`ICR-3`) |
| VWAP fallback (AUD-13) | A | Latent path | **0 of 143** rows in the first 4 min after an anchor | Bundle (`ICR-3`) |
| Burst decay (AUD-19) | A | Burst-path estimator | ≤ 26 of 193 NY BURST rows (loose bound) | With `AVR-2` (c) (`ICR-4`) |
| OFI fold (AUD-12) | A | Estimator weighting on the scored OFI | Not measured | Measure first (`ICR-5`) |
| TTM `flat_threshold` | B | **Unit, confirmed:** `delta` is in USD against an absolute 0.5 (`Core/Indicators_Volatility.vb:182-191`) | FLAT on 0.4–0.8 % of rows: the band is inert | Class B, as the pack has it (gated by `HSR-7`) |
| 100-bar volume and 50-bar VWAP-dev baselines on 3-min bars | B | Unit, and **hardcoded** | — | Class B. ⚠ The pack's "all indicator parameters are externalised" is false here: the windows (100, 50), the σ multipliers (2.0, 1.0) and the 0.05 low-SD guard are literals in `DynamicNorms.vb:36-60` |
| ADX 25/20, RSI 60/40 | C | Design-point read, queued | — | `ICR-6` framing |

**Other faults in the pack's class tables:**

- **"Class A — highest value, lowest risk".** Lowest overfitting risk, yes. Not lowest risk: the volume fix moves the vote from about 0.2 % of rows to about 8–10 % (closed-bar counterfactual, carried from `candle-store-derivations-2026-07-31.md` §3.2), a large scoring boundary. Value is unmeasured (`ICR-2`).
- **Pivots "feed stops".** On 90–99.5 % of structural-stop rows the clamp decides the stop (Q4).
- **C-14 "queued" for the pivot fix and MicroCVD.** C-14 is the exit-tools decision. Its read holds D1–D7 until `X-3` (`AT-L6`).
- **Lengths "leave alone", levels "re-anchor".** ADX 25/20 is as much the trader's chart rule as ADX(9) is (`trader-profile.md` §3). See `ICR-6`.

---

## 4. Attack on the order (`indicator-calibration-review-2026-10-02.md` §7)

**Two orders of record exist** — `indicator-calibration-review-2026-10-02.md` §7 and `seat-handover-2026-10-01.md` §3 — and the pack does not reconcile them. The pack drops the audit scoring fixes (handover item 5) and the liquidation park build (item 6). Proposed merged order; every scoring step obeys `roadmap.md` §5 rule 1 and `AT-6` (b):

| # | Step | Status | Change from the pack |
|---|---|---|---|
| 1 | Measurement: C-9 and C-10, with rows C14–C16; spec C-9 with the `_evalCache` decoupling | Ruled 2026-10-01 | None. Note in the C-9 spec that the stub is the forming bar, so it shares vocabulary with step 5 |
| 2 | Daylight-saving session hours (row F5), before burst run 2 | Ruled 2026-10-01 | None |
| 3 | **Offline reads** (no scoring): forming-bar blast radius and closed-bar vote value (`ICR-2`); the AUD-12 replay (`ICR-5`); an episode-level slope for Q2 | New | Inserted. Analysis class |
| 4 | `L-7`, then audit stop geometry: C-1 engine half (payload flag, `roadmap.md` §5 rule 5), C-2, C-3 with fixture A41 (it pins maker/maker drag on the stop arm; audit row C17); bundle C-6 and AUD-13 here (`ICR-3`) | `AT-L4` ruled; fixes queued | **Restored** from the 10-01 handover; placed before the forming-bar family (`ICR-8`) |
| 5 | **Forming-bar family**, one spec, one boundary (`ICR-1`) | New scope | Replaces the pack's step 3 (volume) and the C-14 pivot item, and moves CVD divergence out of class B |
| 6 | Liquidation park build with Rider 3 (per-session `large_liq_size`) | Queued | **Restored** from the 10-01 handover |
| 7 | Class B: the 3-min baselines (needs new settings keys or code), TTM (`HSR-7`) | Queued | CVD divergence moved to step 5 |
| 8 | Class C: the ADX/RSI read, framed per `ICR-6`; then the other design points | Ruled before `F-2` | Framing only. The read is candle-only and can run any time, but a value change is a boundary |
| 9 | `F-2` on the first clean era after step 5 | Queued | Its population starts after the last mechanism boundary |
| 10 | AUD-19 with `AVR-2` (c), after run 2 per session (latest 2027-06-30) | Held by `AT-6` (b) | **Moved** out of the pack's class A step (`ICR-4`) |
| 11 | C-14 exit tools, after `X-3` | `AT-L6` | **Separated** from the scoring pivot fix |
| 12 | Class D, each through `W6-7` | — | The 1h bias fails the gate as written (Q5) |

**Calendar cost the pack does not state:** each scoring step is a boundary, and burst run 2 adds an era stratum per boundary inside its window (`AT-6` (b)). Steps 4, 5, 6 and 8 are four or more boundaries before `F-2` can read a clean era. Fewer, signed-off bundles shorten that; `ICR-3` is the measured-safe case.

---

## 5. The review pack's `indicator-calibration-review-2026-10-02.md` §9 carried claims, checked

| Claim | Verdict | How |
|---|---|---|
| Volume vote mechanism (forming-bar numerator, closed-bar threshold) | ✅ **Verified** | Code read at `92c60d9` (Q1); `git log` shows no change since 2026-07-30 |
| Volume vote fire rates 0.69 % NY, 2.66 % res-3 | ⚠ **Stale** — lower now | `H-1` read `volume`: upper bounds ≤ 0.39 % NY, ≤ 0.14 % res-3 (Q1 table) |
| `indicator-calibration-review-2026-10-02.md` §2: score slope NY −0.3 [−0.8, +0.2] | ✅ Matches its source | Read against `medium-tier-diagnosis-read-2026-09-17.md` §1.1 and §2.1. Not recomputed |
| `indicator-calibration-review-2026-10-02.md` §2: NY VPFR long +5.0 [+2.4, +7.2]; ASIA OFI +3.0 carried | ✅ Matches its source | `medium-tier-diagnosis-read-2026-09-17.md` §2.3. Not recomputed. Caveats in Q2 |
| `indicator-calibration-review-2026-10-02.md` §2: Kelly f* < 0 everywhere | ✅ Matches its source, ⚠ weak evidence | `DeribitIndicatorProject.md` §15 Kelly row. Not recomputed. The book walks stub bars (Q2) |
| `indicator-calibration-review-2026-10-02.md` §2: stop clamp binds 95–100 % | ⚠ **Partly stale** | `H-1` read `clamp`: NY 99.1–99.5 %, LONDON 90.0–94.6 %, ASIA 93.1–93.9 % |
| CVD divergence under-fires 8× on 3-min | ❌ **Wrong now** | `H-1` read `divergence`: 0.12–0.22 % res-1, 0.09–0.15 % res-3. The gate reads the forming bar's first seconds (code read; the causal link is my inference, strongly implied by the code) |
| TTM unit finding | ✅ **Verified** (mechanism and symptom) | Code read; FLAT 0.4–0.8 %. k ≈ 0.25–0.30 carried, not re-derived |
| 12.6 rows per episode | ✅ **Reproduced approximately, with a citation fix** | The figure originates in `medium-tier-diagnosis-read-2026-09-17.md` §2.9 under the `D-9` rule; the audit's row C4 cites that read, and `DeribitIndicatorProject.md` §5a rule 3a cites the audit. Re-measured under the `D-9` rule: 11.6 on both books. Strict consecutive rows: 2.4–2.6. Gaps ≤ 5 min: 5.9–6.2. Quote it with its rule |
| "Not read in code: AUD-12, AUD-13, AUD-19, the C-14 code paths" | ✅ **Now read** | All present at `92c60d9` (Q3; D6 at `Core/ScoringEngine_Helpers.vb:136-142`; AUD-14 at `Core/Indicators_Structure.vb:287`) |

---

## 6. Other errors in the review pack

- **ID collisions not flagged:** row C14 vs decision C-14; audit row D1 vs the tick queue's D1 vs `D-1`; `F-2` vs `F2`. The global output rule asks for a warning; the triage doc gives one (`adversarial-audit-triage-2026-09-29.md` line 9); the pack does not.
- **"All indicator parameters are externalised"** (`indicator-calibration-review-2026-10-02.md` §6 opening) is false for `DynamicNorms` (§3 table).
- **`indicator-calibration-review-2026-10-02.md` §2 cites Kelly as the evidence of a negative edge.** The medium-tier read's per-bin net EV is the stronger evidence (Q2).
- **`indicator-calibration-review-2026-10-02.md` §6 class A, MTF row:** the status "Queued as decision C-6" is right; the reach is zero rows in this period, which the pack does not say.

**Doc drift found elsewhere, not fixed here:**

- `roadmap.md` §5 rule 1 still names "the v65/D3 ASIA arming watch" as the open window. That watch passed 2026-08-11 (`trader-tick-queue.md` §0a).
- `roadmap.md` §3 W1 cites `MainForm_Analysis.vb:237` and `DynamicNorms.vb:36-40`; the lines are now `:266-269` and `:36-60`.
- `DeribitIndicatorProject.md` §5a rule 3a cites audit row C4 for the 12.6 figure; its origin is the medium-tier read's `D-9` rule.

---

## 7. Seats this review proposes (after 2026-11-25)

**Offline reads (`ICR-2`, `ICR-5`, the episode-level slope):**
- **Model + effort:** Opus 5.5, high.
- **Why:** derivations against pre-registered method, where a wrong number is hard to notice. Templates exist (`tools/ops/medium_tier_diagnosis.py`, the swing read, `candle-store-derivations-2026-07-31.md` §3).
- **Where it slips:** pairing each live row with the bar the engine *should* have read (the last completed bar at the fire instant, not the forming one); pooling across the `.bak` header; quoting an upper bound as a rate.
- **Escalation:** any read whose closed-bar fire rate disagrees with `candle-store-derivations-2026-07-31.md` §3.2 by more than 2× — stop and reconcile first.

**Forming-bar spec (`ICR-1`):**
- **Model + effort:** Opus 5.5 or Fable 5.1, high.
- **Why:** six indicators, each with an A/B/C choice and a different blast radius; a design-point row for the volume threshold; a scoring boundary.

---

## 8. What I verified and what I did not

### Handles the reader can run (pinned to `92c60d9`)

- **`H-1`:** `python -I tools/ops/indicator_review_reads.py --fetch aws_fetch/20261007-180541` — every measured figure in this doc. Run here 2026-10-07; output excerpted in this doc §2, §3 and §5. Needs the local fetch folder (gitignored).
- **`H-2`:** `git log --since=2026-07-30 --format='%h %ad %s' --date=short -S'VolumeRatio' -- UI/ Core/ DynamicNorms.vb` — prints one commit, `fded077` (its hits are `bestPivotVolumeRatio`, unrelated). Run here: `fded077 2026-09-05 refactor(indicators): A54a S2 step 2 commit 1 - delete Optional default…`. Without the path filter it also lists tools and docs commits.
- **`H-3`:** `grep -n "CloseTime > maxExisting" LivePerformanceTracker.vb` — run here: `707:                    If bar.CloseTime > maxExisting Then`. The stub freeze (row C1): the completed bar has the same `CloseTime`, so it never replaces the stub.
- **`H-4`:** `grep -n "candles.Count - 2).Close" Core/Indicators_OrderFlow.vb` — run here: `344:        Dim priceChange As Double = (candles.Last().Close - candles(candles.Count - 2).Close) /`. The CVD divergence price change runs from the last closed bar to the forming bar.

### Verified, and how

- The volume path, the stub freeze, the CVD gate, the TTM unit, AUD-12, AUD-13, AUD-14, AUD-19 and D6: read in code at `92c60d9`.
- Every measured figure: `H-1`, read here.
- The medium-tier read's walk start: `SwingFallbackRead.vb:25`, `:383` and `TierOrderStability.vb:61`.
- The `change_log` has no entry naming the ADX thresholds or the RSI 60/40 keys: parsed `settings.json` (the partial and divergence RSI keys did change).

### Not verified

- **The closed-bar value of the volume vote and CVD divergence** — the point of `ICR-2`.
- **The blast radius of a closed-bar pivot scan.** The one-bar delay on every pivot is reasoned from the code, not measured.
- **AUD-12's reach.** **AUD-19's true reach**: the bound is loose because the row stamp trails the snapshot.
- **The C2 slope effect** (under +0.1 bps per 0.1 of regime max): my estimate.
- **Engine ADX against chart ADX.** Code read only.
- **The Kelly book's numbers.** Carried from `DeribitIndicatorProject.md` §15.
- **The order app's code** for rows A2, A8, A17 and A18: carried from the audit.
- **Carried without checking:** the closed-bar counterfactual rates and thresholds in `candle-store-derivations-2026-07-31.md` §3; the medium-tier read's numbers; the audit's verdicts.
