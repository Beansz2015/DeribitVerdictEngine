# Indicator settings and calibration — findings, suggestions and order — FOR ADVERSARIAL REVIEW

**Written:** 2026-10-02 (UTC) by the orchestrator seat, at the trader's request, from a Q&A on 2026-10-01/02.
**Purpose:** the trader asked whether settings or indicator tweaks could raise net EV, what they would disturb in the queue, and which aspects of every indicator should be calibrated or added. This file collects the answers, the suggestions and the proposed order, so the next orchestrator can review them **adversarially** before any of it is built.
**Class:** analysis and planning only. Nothing here changes code, settings or scoring. Every change it points to is reserved (scoring or `settings.json`) and follows the post-holiday rules: nothing ships before 2026-11-25; `roadmap.md` §5 rule 1 (one scoring boundary at a time, or a signed-off bundle); `AT-6` (b) for burst run 2 ([`adversarial-audit-triage-2026-09-29.md`](adversarial-audit-triage-2026-09-29.md) §7a).

**For the reviewing seat:**
- Model: Opus 5.5 (or Fable 5.1) · Effort: high.
- Why that tier: the claims below rest on several earlier reads and on the adversarial audit. The review's value is in re-checking the claims marked "carried" against code and data, and in attacking the order and the classification. That is judgment work, not mechanics.
- Where it can slip: accepting a "carried" figure as verified. §7 lists which claims this seat checked and which it did not.

---

## 1. The trader's questions, and the short answers

| # | Question | Short answer |
|---|---|---|
| Q1 | Are there `settings.json` tweaks that would raise net EV? | **None is evidence-backed today.** The measured lever is individual votes, not thresholds (§2), and the instruments needed to choose a tweak are biased (§3) |
| Q2 | Would tweaks disturb ongoing or future queue items? | **Yes, almost every read** (§4) |
| Q3 | Isn't net EV per session more practical than per trade? | **Yes, as a total per session-day counted by episodes.** Added to the house vocabulary (trader-ruled 2026-10-01; `DeribitIndicatorProject.md` §5a). Same sign as per-trade EV for the same population; it adds frequency, day-to-day spread, and a way to find positive pockets (§5) |
| Q4 | Are all the audit's settings findings queued? | All the §C decisions were; row F5 (daylight saving) and rows C14–C16 were not. **All four are now queued or ruled** (2026-10-01) |
| Q5 | Are all possible tweaks queued? | Every evidence-backed candidate known today is. The pipelines that would find new ones are queued too, but behind the measurement fixes |
| Q6 | What about the indicators' own settings (lengths, triggers, and every other aspect)? | All are in `settings.json`, but **most were never calibrated**. The highest-value work is not tuning: it is fixing indicators that do not measure what they claim (§6, class A) |

---

## 2. What the project's own reads say about tweaking

| Read | Finding | Consequence |
|---|---|---|
| [`medium-tier-diagnosis-read-2026-09-17.md`](medium-tier-diagnosis-read-2026-09-17.md) | **The score does not rank outcomes**: net EV is flat against score share in every session and both halves (NY −0.3 [−0.8, +0.2] bps per 0.1 of regime max) | Moving verdict thresholds (`scoring.verdict_*_pct`) or tier floors will not lift net EV; the tiers do not separate |
| Same read | **Single votes carry value:** NY VPFR on longs +5.0 [+2.4, +7.2] bps CONFIRMED; ASIA OFI +3.0 bps CONFIRMED (carried window) | The real lever is per-vote weighting: the `F-2` vote-value study, ruled to go only through a pre-registered, settings-era-aware study with forward validation |
| Kelly v69 ([`kelly-one-class-placed-payoff-spec.md`](kelly-one-class-placed-payoff-spec.md), AC-4, 2026-09-25) | f* < 0 in every session and every payoff bucket | The book's net edge is negative at current geometry. ⚠ Audit row C1 (frozen OHLC stubs in the live eval cache) may bias this in NY |
| Placed-geometry watch (`DeribitIndicatorProject.md` §12) | The stop clamp binds on 95–100 % of structural-stop rows | Stops are effectively ATR stops; the live geometry question is the L9 un-clamp, gated on the order app |
| Burst outcome read ([`burst-outcome-read-spec.md`](burst-outcome-read-spec.md)) | Pre-registered; run 1 after 2026-11-25, run 2 per session | The burst threshold change (`AVR-2` option (c)) waits for it |

---

## 3. Why no tweak should be chosen yet — the instruments

The adversarial audit ([`adversarial-audit-2026-09-24.md`](adversarial-audit-2026-09-24.md) §A item 3 and Band C) found the same three flaws in every success-rate surface (the perf strip, the failure matrix, the band ladder, the offline report, the what-if runner, the tweaker's trigger):

1. No fee-inclusive EV; the loss path costs more than the modelled maker/maker fee.
2. A two-minute blind spot after entry, with the entry still priced at T (row C3).
3. Consecutive minute rows counted as independent trades (row C4; about 12.6 NY rows per episode).

It also found that the what-if runner whitelists knobs it never applies (row C5) and picks winners with no minimum n (row C6). And the live OHLC cache freezes forming-bar stubs (row C1).

**Ruled 2026-10-01:** decisions C-9 (the stub cache) and C-10 (fee-inclusive, per-episode EV on every surface) come FIRST among the post-holiday reserved work. Rows C14–C16 travel with them.

---

## 4. What a settings change disturbs

| Item | Effect of a scoring settings change |
|---|---|
| The change itself | Reserved: it hot-reloads onto the live collector mid-instance and is a dataset boundary |
| Burst run 1 | One more era stratum. If it touches the burst path, it is held (`AT-6`) |
| Burst run 2 (per session; its data start moves to the first row after the daylight-saving deploy, ruled 2026-10-01) | An era stratum and a sensitivity run per `AT-6` (b). Burst-path changes are held until every session's run 2 is read |
| Burst-watch ATR-conditional reference (`AVR-1`) | Recalibrate if any aggressor-velocity key moves |
| Absorption Stage 1 read | Affected if absorption or level keys move |
| Kelly book | Any geometry or fee change moves its measured p and b |
| OFI and funding watches | Reset if their keys move |
| Tier-order forward test | Already parked for exactly this reason |
| The holiday (2026-10-14 → 11-25) | No change during it. A change just before it puts six unattended weeks under new settings |

---

## 5. Net EV per session-day (added to the vocabulary 2026-10-01)

- **Definition** (`DeribitIndicatorProject.md` §5a): the mean total net result per session-day, summing the trades actually takeable that day. A trade is taken at the first directional row; later rows are skipped until it resolves plus the order app's cooloff, which mirrors the bridge.
- **What it adds over net EV per trade:**
  1. **Frequency:** a filter that lifts per-trade EV but halves the count can lose in total.
  2. **Spread:** the share of positive session-days and the worst one.
  3. **Pockets:** a negative overall mean can hide positive sub-populations (session × condition) worth trading.
- **What it does not do:** averaging over days cannot flip the sign of the per-trade mean for the same population. Net EV per trade already averages wins, small wins and losses.
- **Open:** the order app's cooloff value is not recorded in this repo; reads must name the value used.

---

## 6. Indicator aspects — what to calibrate, fix or add

**All indicator parameters are externalised to `settings.json`** (lines 238–291 for the core blocks, read 2026-10-01). **Being externalised is not being calibrated.**

| Group | Examples | Ever re-derived? |
|---|---|---|
| Lengths | ROC 9, RSI 9, DMI/ADX 9, ATR 7, EMA 9/21/50, Volume SMA 9, BBW 20, Donchian 20 | No. These are the trader's chart style ([`trader-profile.md`](trader-profile.md) §3). Leave them unless the trader changes the chart too |
| Trigger levels | ADX trend 25 / range 20; RSI 60 / 40 | Not found: no `change_log` entry names these keys (grep 2026-10-01). **Queued 2026-10-01: a design-point re-anchor before `F-2`** |
| Re-derived thresholds | ROC magnitude/slope (v40/v41), OFI dominance (v48), funding momentum (v53), aggressor velocity (v52/v60/v65), OBV `trend_gate` (v66), absorption anchors (v61) | Yes, each with a derivation read. TTM `flat_threshold` re-derived, then parked |

The candidates below are ordered by **overfitting risk, lowest first**. Classes A and B are not tuning: the indicator does not measure what it claims.

### Class A — mechanism defects (highest value, lowest risk)

| Indicator | Defect | Source | Status |
|---|---|---|---|
| **Volume spike vote** | Numerator is the in-progress bar; threshold built from completed bars; the vote fires on **0.69 % of NY runs, 2.66 % at execution resolution 3**. The trader's core breakout confirmation ("volume > 3× SMA(9)") is effectively absent | `roadmap.md` W1, the "LONDON/NY `session_volume` multiplier passes" row; `DynamicNorms.vb:36-40`, `MainForm_Analysis.vb:237` as cited there | **Parked behind the forming-bar ruling** (JOB 2 decision D-C, 2026-07-31). **This seat's read: the single most valuable fix in this file — un-park the forming-bar decision** |
| **5m swing pivots** | "Confirmed" by the forming 5m bar; repaints inside the bar; the flush that breaks a pivot deletes it | Audit AUD-14, row D1 | Queued as audit decision C-14 (reserved). Feeds stops, targets, structure |
| **MicroCVD** | Votes "adverse" at exhaustion and abstains during the one-way leg | Audit row D6 | Queued under C-14 |
| **15m MTF gate** | Fails open on missing or short data; keeps a failed-fetch cache with no age bound; can only vote bull below 50 bars | Audit AUD-07, row A6 | Queued as decision C-6 |
| **Time-averaged OFI** | Each book update gets the weight of the gap before it | Audit AUD-12 (S3) | ⚠ **Not queued** |
| **VWAP** | The session fallback defeats its own warmup guard | Audit AUD-13 (S3) | ⚠ **Not queued** |
| **Aggressor-velocity burst** | Read as of the last trade, not as of now | Audit AUD-19 (S3) | ⚠ **Not queued** |

### Class B — unit and normalisation fixes (low risk)

| Indicator | Issue | Status |
|---|---|---|
| TTM `flat_threshold` | An absolute USD value where an ATR fraction is needed (k ≈ 0.25–0.30) | Re-derived, parked; re-gated by `HSR-7` |
| CVD / RSI divergence price gates | 1-minute values used on 3-minute bars; CVD divergence under-fires 8× there (audit F15, 2026-07-03) | Queued (`HSR-6` for the CVD half) |
| DynamicNorms 100-bar volume and 50-bar VWAP-dev baselines on 3-min bars | Span 3× the wall-clock; LONDON never gets a pure-session baseline | `DeribitIndicatorProject.md` §12 "v36 Phase-2 carry-forward" row, open |

### Class C — design-point re-anchors (no outcome data)

- **Queued:** ADX 25/20 and RSI 60/40, before `F-2`.
- **Method:** state each level's intended design point (for example the share of 5m bars ADX should mark trending) and re-anchor on years of candle history, per session and resolution. This is the v66 OBV precedent.
- **Same method could apply to:** Donchian `quartile_pct`, the BBW squeeze cut, VPFR bucket count and decay base, the OI change threshold, the CVD `slope_min_usd`. None is queued.

### Class D — additions not in the code (highest overfitting risk)

Gated on the W6-7 bar for a new signal class (`roadmap.md` W6: an orthogonal class, not another angle on flow) and on `F-2`.

| Candidate | Why | Caveat |
|---|---|---|
| **1h bias** | The trader's style uses 5m/15m/1h bias (`trader-profile.md` §2); the engine stops at 15m | Likely correlated with the 15m MTF gate; must show it adds information |
| **Swing-breakout event signal** | The trader's actual entry checklist (break of a swing + ROC impulse + volume spike) is not encoded as one event; the engine votes the parts separately | Built from existing votes, so a double-counting risk (`trader-profile.md` §4). Needs class A's volume fix first |
| **Cross-venue lead-lag** | The one non-marginal new class ([`cross-venue-lead-lag-proposal.md`](cross-venue-lead-lag-proposal.md), W6-7) | Waits behind the current queue |
| **Volume-weighted pivots as a target tier** (P1) | Built as a what-if candidate (v63) | Waits on its P1 promotion conditions |

---

## 7. Proposed order (post-holiday), for the reviewer to attack

1. **Measurement first:** decisions C-9 and C-10, with rows C14–C16 (ruled 2026-10-01). Spec C-9 with the `_evalCache` decoupling.
2. **Daylight-saving-aware session hours** (row F5), before burst run 2 (ruled 2026-10-01).
3. **Class A, starting with the volume vote:** un-park the forming-bar decision. Then the C-14 group (swing repaint, MicroCVD), C-6 (MTF gate), and **AUD-12, AUD-13, AUD-19 once queued** (this seat's suggestion; not yet ruled).
4. **Class B:** the 3-min baselines, the divergence gates (with the history store, `HSR-6`), TTM (`HSR-7`).
5. **Class C:** the ADX/RSI re-anchor (queued), then the other design points.
6. **`F-2` vote-value study:** pre-registered, settings-era-aware, forward-validated, reported in **net EV per trade and per session-day**.
7. **Class D**, each through the W6-7 bar.

Every scoring step obeys `roadmap.md` §5 rule 1 and `AT-6` (b), and is preceded by the independent audit review L-7 where it is an audit fix.

---

## 8. Questions for the adversarial reviewer

1. Is the volume-vote defect still as described (0.69 % NY fire rate; forming-bar numerator)? It is carried from a 2026-07-31 read. Has anything since changed `DynamicNorms` or the volume path?
2. Is "the score does not rank outcomes" robust to the audit's measurement flaws (rows C2–C4), or could it be an artefact of them? If an artefact, §2's main conclusion falls.
3. Are AUD-12, AUD-13 and AUD-19 material enough to queue, or are they rightly S3?
4. Is the class order right? In particular: should C-14's swing-pivot fix come before the volume vote, since pivots feed placed stops and targets?
5. Does adding a 1h bias or a swing-breakout event conflict with any rejected pattern in `trader-profile.md` §4?
6. Is the session-day metric's episode rule (first row, skip until resolution plus cooloff) a fair model of the bridge, given audit rows A8 and A17 (stand-downs and an unanchored cooloff)?

---

## 9. What this seat verified and what it did not

- **Verified this session:** the parameter values (`settings.json` lines 238–291); that no `change_log` entry names the ADX, RSI, EMA, ROC, DMI or ATR keys (grep); the audit rows quoted, read from the report's §A–§D and its section headings.
- **Carried, not re-checked:**
  - the volume vote's fire rates and mechanism;
  - every figure in §2;
  - CVD divergence under-firing 8×;
  - the TTM unit finding;
  - the 12.6 rows per episode.
- **Not read in code:** AUD-12, AUD-13, AUD-19; the C-14 group's code paths.
