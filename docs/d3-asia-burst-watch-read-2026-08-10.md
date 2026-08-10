# D3 / v65 ASIA aggressor-velocity arming watch — FIRST READ (2026-08-10)

**From:** the orchestrator seat that opened on [`seat-handover-2026-08-10.md`](seat-handover-2026-08-10.md). Its **§0 named this read as the first task**; the trader supplied the prerequisite AWS copy-back the same day.

**Watch definition:** [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) **§5 row D3-5**.
**Recipe:** that document's §4 population rule, itself taken verbatim from [`aggressor-velocity-s52-derivation-2026-07-13.md`](aggressor-velocity-s52-derivation-2026-07-13.md) §3.
**Effort:** Opus / high, as `seat-handover-2026-08-10.md` §0 specified.

---

## 0. Verdict — the watch PASSES on both stated criteria

| Read | Days | Rows | Fire rate | Same-side |
|---|---:|---:|---:|---:|
| **Post-ship (the watch proper)** — 2026-08-03…08-07 | 5 | 792 | **11.99 %** | **89.47 %** |
| **Full fully-covered AWS record** — 2026-07-23…08-07 | 12 | 1,905 | **10.97 %** ± 0.78 pp | **89.47 %** |

**No re-fit is triggered. `burst_ratio_threshold` = 5.5 stands for ASIA.**

**The 12-day read is the better estimate of the underlying rate and it is comfortably inside the 8–12 % design band, not at its edge.** Pre-v65 rows are admissible for *this* comparison and the derivation used the same device (§4 of `asia-burst-threshold-derivation-2026-08-01.md`): `AggrVelBurstRatio` is computed identically whether or not a session is armed — arming changes only whether a burst modifies the TFI *vote* — so re-classifying pre-ship rows at 5.5 measures the same quantity. The post-ship 11.99 % is an ordinary high draw, **+1.02 pp above the 12-day mean, well inside the 0.78 pp standard error's noise envelope.**

⚠ **Two qualifications travel with that, and neither is optional.**

1. **The post-ship sample alone is five weekdays** — the shortest read that satisfies the multi-day rule, not a comfortable one. The 12-day figure earns its confidence by borrowing pre-ship rows, which is valid for the fire rate and same-side criteria and for nothing else.
2. **This is a DISTRIBUTIONAL pass, not an outcome pass.** The only outcome-linked read on this knob remains the W6-4 ceiling audit: `AggrVelBurstRatio` **AUC 0.5179 (n=217)** — essentially no demonstrated edge. Per `asia-burst-threshold-derivation-2026-08-01.md` §5.2 this does not refute arming, but a PASS here must never be reported as if outcomes agreed. **They were not measured, then or now.**

---

## 1. Population and method

| Item | Value |
|---|---|
| Source | `AWS-copybacks\aws-copyback-2026-08-10\analysis_log.csv` (15,499 data rows, ends 2026-08-10 14:28:06 UTC) |
| Band | **2026-08-03 … 2026-08-07 — Mon–Fri, five weekdays** |
| Session | ASIA = UTC hours **00–07 inclusive**, matching `settings.json` `session_volume.sessions[ASIA]` (`start_hour` 0 / `end_hour` 7) |
| Execution resolution | **3** on every ASIA row (2,720 of 2,720 checked) |
| Population rule | `AggrVelBurstRatio ≠ ""` — **792 rows** |
| Fire | `AggrVelSignal ∈ {BURST_BUY, BURST_SELL}` |
| Same-side | `BURST_BUY` ↔ `TFISignal = BUY PRESSURE`; `BURST_SELL` ↔ `SELL PRESSURE`. TFI `NEUTRAL` counts as neither |

**Local book excluded, and it changes nothing.** `bin\Debug\net8.0-windows\analysis_log.csv` holds **24** post-arming ASIA rows, all on Saturday 2026-08-08. It contributes **zero weekday ASIA rows**, so the AWS-preferred pooling of the 2026-07-31 dedup ruling has nothing to resolve. This is the D1-a bias note behaving exactly as predicted — the local box runs active hours, not ASIA.

---

## 2. Results

### Per session-day

| Day | Weekday | Rows | Fires | Fire rate | Same | Contra | TFI neutral |
|---|---|---:|---:|---:|---:|---:|---:|
| 2026-08-02 | Sun | 160 | 21 | 13.1 % | 17 | 2 | 2 |
| **2026-08-03** | **Mon** | **159** | **17** | **10.7 %** | 16 | 0 | 1 |
| **2026-08-04** | **Tue** | **160** | **24** | **15.0 %** | 20 | 0 | 4 |
| **2026-08-05** | **Wed** | **157** | **24** | **15.3 %** | 23 | 0 | 1 |
| **2026-08-06** | **Thu** | **157** | **13** | **8.3 %** | 13 | 0 | 0 |
| **2026-08-07** | **Fri** | **159** | **17** | **10.7 %** | 13 | 2 | 2 |
| 2026-08-08 | Sat | 160 | 14 | 8.8 % | 14 | 0 | 0 |

Weekend rows shown for completeness only. **They are excluded from every figure below.**

### Pooled weekday band

| Measure | Value | Reference |
|---|---:|---|
| Population rows | 792 | — |
| Fires | 95 | — |
| **Fire rate** | **11.99 %** | design point 9.7 %; band 8–12 % |
| **Same-side** | **89.47 %** (85) | bar ≥85 %; derivation 91.0 % |
| Contra | 2.11 % (2) | — |
| TFI neutral | 8.42 % (8) | — |
| Contra/day | 0.40 | derivation 0.21–0.43 |
| Rows/day | **158.4** | derivation quoted **~106** — see §4 |

**The contra arm remains effectively dead**, at 0.40/day inside the derivation's own 0.21–0.43 range. `asia-burst-threshold-derivation-2026-08-01.md` §3 stands unchanged: **on res-3 this modifier is in practice upgrade-only**, and the §4.5 warning half must not be claimed as operative here.

---

## 3. The like-for-like comparison — this is the number that matters

**11.99 % against 9.7 % is not the right comparison, and reading it that way overstates the drift.**

The derivation's 9.7 % came from a **pooled** book of 14 session-days, of which roughly half predate the AWS collector and are thin local-box rows. Reconstructing the derivation window from AWS alone, re-classified at T=5.5, gives the honest baseline:

| Window | Box | Weekday session-days | Rows | Rows/day | Fire rate @ 5.5 |
|---|---|---:|---:|---:|---:|
| 2026-07-22 … 2026-08-01 | AWS only | 7 | 1,113 | 159.0 | **10.24 %** |
| 2026-08-03 … 2026-08-07 | AWS only | 5 | 792 | 158.4 | **11.99 %** |

Same box, same coverage, same classifier threshold. **The shift is +1.75 pp, z = 1.20, p ≈ 0.23 — inside sampling noise.**

Day-level test on the five weekday rates (10.69 / 15.00 / 15.29 / 8.28 / 10.69): mean 11.99 %, sd 3.05, **t = 1.68 on 4 df, p ≈ 0.17 against the 9.7 % design point.** Also not significant.

Row-level against 9.7 % gives z = 2.18, p ≈ 0.029 — but rows inside a day are not independent (bursts cluster), so that figure overstates confidence and is recorded only to show it was computed and set aside.

---

## 4. A hypothesis I formed, tested, and REJECTED — recorded because rejecting it is the finding

There is a strong hour-of-session gradient in the ASIA band:

| UTC hour | 00 | 01 | 02 | 03 | 04 | 05 | 06 | 07 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Fire rate | 11.2 % | 6.1 % | 13.0 % | 13.1 % | 13.1 % | 9.1 % | 14.1 % | 16.0 % |

The tempting explanation for 11.99 % > 9.7 % was **coverage composition**: the derivation's book averaged ~106 rows/day against today's ~158, so if the missing rows sat in the quiet early hours the old rate would read low for a mechanical reason.

**It does not hold.** The local book *is* hour-skewed — over 2026-07-13…2026-08-01 it carries **zero** ASIA rows in hours 00–03 and 305 rows concentrated in 04–07. **But the AWS book of the same period is fully covered** — 138–140 rows in every one of the eight hours. The derivation's ASIA population was AWS-dominated and evenly covered, so the composition story has nothing to explain the gap with.

`seat-handover-2026-08-10.md` §0 warns that a ready explanation for a surprising result is a warning sign. This one was ready, and it was wrong. **The real answer is the duller one in §3: there is no significant gap to explain.**

**What the exercise did surface, and it should be corrected wherever quoted:** the **"~106 AggrVel rows/day"** figure — carried in `asia-burst-threshold-derivation-2026-08-01.md` §3, `trader-tick-queue.md` §4, `seat-handover-2026-08-10.md` §0 and the v65 `change_log` — is an artefact of that mixed-coverage book. **On the all-AWS book ASIA carries ~158–160 rows/day.** Since ~106 is the stated *reason* the per-day band is wide and NY's ±2 pp does not transfer, the small-sample argument is ~50 % weaker than recorded. The multi-day rule still binds — the observed weekday spread is 8.3–15.3 pp — but the stated basis needs updating.

---

## 5. The four traps from `seat-handover-2026-08-10.md` §0 — each addressed

| # | Trap | How it was handled |
|---|---|---|
| 1 | Read over a multi-day band, never one session-day | Five weekdays pooled. Per-day rates shown but **never** used to conclude |
| 2 | NY's ±2 pp does not transfer to res-3; do not re-fit off one day | **Nothing was re-fitted.** This is a read, not a derivation |
| 3 | v65 spans multiple InstanceIds; the obvious filter drops data | **Checked, not assumed.** The whole weekday band is a single id, `09c747f8…`, 800 of 800 rows. Coverage is **160/160 theoretical 3-min slots on all five days.** See §7 for a sixth id the ledger does not carry |
| 4 | Weekday-only evaluation | Enforced by day-of-week computation. Sun 08-02 and Sat 08-08 shown separately and excluded from every statistic |

**Extra check the traps did not ask for — arming verification.** On all 1,112 post-v65 ASIA rows carrying a burst ratio, `BURST_*` and `BurstRatio ≥ 5.5` agree **exactly**: 130 fires, **zero** bursts below 5.5, **zero** non-bursts at or above it. On pre-v65 ASIA rows the minimum burst ratio among fires is **2.507**, the old exploratory default. **The D3 arming is confirmed live from the data, independently of the InstanceId ledger.**

---

## 6. What this unblocks — D2, and the reason is narrower than the queue implies

[`trader-tick-queue.md`](trader-tick-queue.md) §0a gates **D2** (OBV `trend_gate` 18 → ~23) on the D3 watch **reading**. It has now read, and it passes. **D2's gate (ii) is satisfied.**

**One structural point makes this safer than the queue's wording suggests, and it was verified in the tree rather than reasoned from the docs.** The queue's concern is that D2 and D3 "both push the same ASIA path," so shipping D2 during an open D3 watch corrupts the evidence. That is true of **outcome** evidence. It is **not** true of the two criteria this watch actually measures:

- `AggrVelBurstRatio` is produced by `Core/AggressorVelocityAccumulator.vb` from the trade stream.
- `TFISignal` comes from `CalcTFI` over `recentTrades`.
- Neither `Core/Indicators_OrderFlow.vb` nor `MarketState.vb` references OBV at all — grep-verified, no hits.

**So `OBV.trend_gate` cannot move the fire rate or the same-side share.** Both criteria stay re-readable at any future date whether or not D2 ships. What D2 would permanently confound is the ASIA *outcome* question — which `asia-burst-threshold-derivation-2026-08-01.md` §4 already records as never measured, and which is not part of this watch.

**My read for the trader:** D2 may go. The residual argument for holding it is that five weekdays is a thin read and a second read would be more comfortable — but a second read stays available after D2 ships, because the criteria are OBV-independent. Waiting buys confidence in a number that is not at risk.

---

## 7. ⚠ A sixth v65 InstanceId, absent from the ledger

`seat-handover-2026-08-10.md` §0 trap 3 and `trader-tick-queue.md` §0a both say v65 spans **five** InstanceIds. **It spans six.**

**`ffced26c-aaab-4cbc-b7e8-a0f1882dd3b3`** — AWS, DOWN 2026-08-10 09:59:56.592Z → OK 10:00:03.328Z, 129 CSV rows through 14:07:05. It is **not** in the [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a ledger.

Full AWS chain: `09c747f8…` → `ec487909…` → **`ffced26c…`** → `d8678d2b…`. Local: `3916540f…` → `ad7cadf4…`. **All six are v65 and all six have ASIA armed.** It does not touch this read — the weekday band predates it — but it will contaminate the next one.

---

## 8. An intentional-downtime hole that ends the current watch window

**AWS captured no analysis rows between 2026-08-08 08:33:26 and 2026-08-10 10:00:01 UTC — about 49.5 hours.** Trader-confirmed 2026-08-10 as an **intentional weekend instance stop**, which is why `ws_health.log` carries no DOWN line: an EC2 stop kills the process without running shutdown code.

- The **tape** hole is smaller — 29.4 h, 2026-08-08 08:33:25 → 2026-08-09 14:00:01 — because gap repair back-filled from the 10:00 restart. `settings.json` `gap_repair_lookback_hours` = **20**, and 10:00 − 20 h = 14:00 exactly.
- The analysis CSV has **no** repair path, so all ~49.5 h of rows are gone.
- ⚠ **Monday 2026-08-10's entire ASIA session (00:00–07:00 UTC) sits inside the hole.** The box returned at 10:00 UTC, after ASIA closed. **The stop cost a weekday ASIA session-day, which is exactly the scarce resource this watch consumes.**

**Consequence for the part-time-running option** in `seat-handover-2026-08-10.md` §3: it already identifies **00:00–12:00 UTC** as the right window. This episode adds the operational corollary — **a restart must land before 00:00 UTC.** A restart during the working day in GMT+8 costs that day's ASIA session even when the box is up for the rest of it.

---

## 9. What I did not verify

- **Outcomes.** Nothing here is joined to forward returns. Identical scope to the derivation, and stated for the same reason.
- **The norm-window coupling.** 5.5 is derived *for* `fast_window_sec` 5 / `norm_window_sec` 120. I confirmed ASIA carries no session override but did **not** re-derive the coupling.
- **LONDON and NY.** Not re-read. This is the ASIA watch only.
- **Whether the 8–12 % band is the right tolerance for ASIA.** ⚠ **`asia-burst-threshold-derivation-2026-08-01.md` row D3-5 states the trigger value (≈9.7 %) but never states a tolerance**, and it explicitly says NY's ±2 pp does not transfer. See §10 — a tolerance is now proposed, and it needs a trader tick.
- **Anything on the AWS box itself.** All AWS facts here come from the copied-back files.

---

## 10. The ASIA watch tolerance that was never specified — ✅ **T-1 … T-5 ALL TICKED 2026-08-11 (trader), as proposed**

**The problem.** `asia-burst-threshold-derivation-2026-08-01.md` row D3-5 gives a trigger *value* (≈9.7 %) and removes NY's per-day rule, but never replaces it with a numeric tolerance or a read length. A watch with a target and no tolerance cannot be passed or failed except by judgement.

**The two numbers the tolerance has to respect, both measured here:**

| Quantity | Value | How measured |
|---|---:|---|
| ASIA fire rate at T=5.5 | **10.97 %** | 12 fully-covered AWS weekday session-days, n=1,905 |
| Day-to-day sd of the daily rate | **2.71 pp** | same 12 days |
| Binomial-only sd expected at n=160 | 2.47 pp | — |
| **Design effect** | **1.20** | 2.71 / 2.47 — day-to-day variation is **almost pure sampling noise**, not real drift |

⚠ **The recorded design point of 9.7 % is low, and the reason is now known.** It came from a book averaging ~106 rows/day, roughly half of it thin pre-AWS local rows. On a fully-covered AWS book the rate is **10.97 %**. That is 1.63 standard errors above 9.7 % — not a significant difference, but the point estimate has moved and the old one should not stay as the reference.

**Why the 8–12 % design band cannot serve as the watch tolerance.** With the baseline at 10.97 %, the 12 % ceiling is only ~1.2 se away on a 10-weekday read. That band would false-alarm on the upper side in roughly one read in nine — a watch that cries wolf gets ignored, which is the failure `trader-tick-queue.md` §0a already records twice.

**The replacement — ✅ TICKED 2026-08-11. Doc-only, no key moves, no ⚠ boundary.** It **supersedes row D3-5** of [`asia-burst-threshold-derivation-2026-08-01.md`](asia-burst-threshold-derivation-2026-08-01.md) §5, which is amended in place to point here.

| # | Element | Ruled |
|---|---|---|
| **T-1** | Reference rate | **11.0 %**, replacing 9.7 %. Measured, 12 fully-covered AWS weekday session-days |
| **T-2** | Band | **8–14 %** (±3 pp). At a 10-weekday read that is ±3.5 se ⇒ ~0.05 % false-alarm rate. The 8 % floor coincides with the design band's floor |
| **T-3** | Read length | **≥10 weekday session-days**, pooled, fully covered. Five is too short: against any band it false-alarms at >10 % |
| **T-4** | Same-side | **≥85 %** on the same pooled band — unchanged. Currently 89.47 % on both the 5-day and 12-day reads |
| **T-5** | What the trigger does | Fires a **re-derivation read** — cheap, no boundary. The re-derivation, not the watch, decides whether the threshold moves |

**Recorded and deliberately not acted on:** 11.0 % sits in the upper half of the 8–12 % design band, so T=6.0 would be the natural candidate if a re-derivation ever happens for another reason. **Do not spend a ⚠ boundary on it now** — `asia-burst-threshold-derivation-2026-08-01.md` §5.2 rules no further scoring spend without weighing the AUC 0.5179 finding, and a 1 pp move inside the design band is exactly the over-tuning that ruling exists to prevent.

**Next band-eligible read: ~2026-08-17**, assuming AWS covers ASIA on 08-11 … 08-14 and 08-17. 2026-08-10's ASIA was lost to the weekend stop (§8).
