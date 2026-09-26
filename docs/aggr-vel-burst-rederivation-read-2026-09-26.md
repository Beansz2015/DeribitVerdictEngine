# Aggressor-velocity burst — re-derivation read (2026-09-26 UTC)

**Owed by:** trigger T-5, fired by [`d3-asia-burst-watch-read-2026-09-14.md`](d3-asia-burst-watch-read-2026-09-14.md) and confirmed by [`d3-asia-burst-watch-read-2026-09-26.md`](d3-asia-burst-watch-read-2026-09-26.md). **Answers:** the four questions in `d3-asia-burst-watch-read-2026-09-14.md` §8. **Instrument:** [`tools/ops/aggr-vel-regime-read.ps1`](../tools/ops/aggr-vel-regime-read.ps1) (committed with this doc; read-only). **Effort:** Opus 5.5, high.

⛔ **This is a READ. It changes no setting and proposes no value for ship.** Every option in §5 that moves `settings.json` or scoring is reserved to the trader.

---

## 0. Answers

| # | Question (`d3-asia-burst-watch-read-2026-09-14.md` §8) | Answer |
|---|---|---|
| 1 | Is the burst fire rate volatility-conditional by construction? | ✅ **Yes, measured.** In every session the burst ratio's upper tail shrinks as ATR rises, while its median barely moves. The fire rate falls about 5–13× from the lowest to the highest ATR fifth (ASIA 15.46 % → 2.87 %, LONDON 18.65 % → 1.86 %, NY 18.55 % → 1.48 %). The dependence also holds inside the post-2026-08-20 regime alone |
| 2 | Does the 11.0 % reference (ruling T-1) describe any regime? | **Only a low-to-middle ATR band.** ASIA reads 15.5 % at ATR 8–34 and 8.5 % at ATR 34–51. The 11 % reference sits at ASIA ATR of roughly 35–40, the volatility of the derivation window. The current regime (ASIA median ATR ~70) reads 4.4 % |
| 3 | Is the ±3 pp band (ruling T-2) still correctly sized? | **Yes, inside one regime.** ASIA's daily design effect since 2026-08-20 is 1.30 (sd 2.13 pp against 1.65 pp binomial), close to the 1.20 the band was sized on. The 5.37 pp sd in the second read came from mixing two regimes. **The reference level is what is wrong, not the band width** |
| 4 | Does the regime step also show in LONDON and NY? | ✅ **Yes, in all three sessions.** LONDON 16.44 % → 3.44 %, NY 14.04 % → 4.60 %, ASIA 14.16 % → 4.36 %. It is a market-wide volatility regime, not an ASIA defect |

⭐⭐ **A consequence nobody has read: the NY watch is also out of band.** `DeribitIndicatorProject.md` §12 carries the v52 NY watch at a burst fire rate of 8–12 %. NY reads **4.60 %** over 25 weekdays since 2026-08-20. That watch's trigger (out of band on 2 consecutive weekday sessions) has been met since late August. No read recorded it. LONDON has no written band, but it reads 3.44 %.

---

## 1. The mechanism, stated from the code

`Core/AggressorVelocityAccumulator.vb` lines 23–25:

```
grossFast  = (AbuyF + AsellF) / tauFast          ' USD/sec, burst horizon (fast_window_sec = 5)
grossNorm  = (AbuyN + AsellN) / tauNorm          ' USD/sec, rolling baseline (norm_window_sec: NY 60, else 120)
burstRatio = grossFast / max(grossNorm, floor)   ' floor = gross_floor_usd_per_sec = 50
```

- `burstRatio` is scale-free. It does not measure how much flow there is. It measures how **uneven** flow is between a 5-second and a 60- or 120-second horizon.
- A quiet market trades in clumps: long gaps, then a few prints. A 5-second clump against a thin norm gives a large ratio. A busy, volatile market trades more continuously, so the norm is high and a 5-second window rarely stands far above it.
- ⚠ **That explanation is an inference.** The CSV logs `AggrVelBurstRatio` but not `grossFast` or `grossNorm`, so the two parts cannot be separated per row. What §2 measures is the consequence: the ratio's tail shrinks as ATR rises, in both the 1-minute NY config (60 s norm, threshold 4.5) and the 3-minute ASIA and LONDON config (120 s norm, threshold 5.5).

---

## 2. Measurement

Command, run 2026-09-26 UTC against `8c0ce06` plus the new script:

```
powershell -NoProfile -File tools/ops/aggr-vel-regime-read.ps1 -FetchFolder 'aws_fetch\20260925-085341'
```

Population: weekday rows from 2026-08-02 (all three sessions armed) to 2026-09-25 08:51 UTC, `AggrVelBurstRatio` non-empty, all three books pooled (18,954 + 14,441 + 486 rows kept). ATR bins are five equal-row bins per session. Percentiles are of `AggrVelBurstRatio`.

### 2.1 Before and after the 2026-08-20 step

| Session (threshold) | Slice | Rows | Fire rate | Same-side | Median ATR | ratio p50 | ratio p90 | ratio p95 |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| ASIA (5.5) | before 08-20 | 1,744 | 14.16 % | 89.1 % | 30.8 | 0.56 | 7.04 | 11.40 |
| ASIA (5.5) | from 08-20 | 4,011 | **4.36 %** | 91.4 % | 69.8 | 0.68 | 3.39 | 5.20 |
| LONDON (5.5) | before 08-20 | 1,107 | 16.44 % | 84.1 % | 29.7 | 0.72 | 7.73 | 11.94 |
| LONDON (5.5) | from 08-20 | 2,382 | **3.44 %** | 92.7 % | 73.1 | 0.78 | 3.20 | 4.65 |
| NY (4.5) | before 08-20 | 8,347 | 14.04 % | 83.9 % | 19.9 | 0.67 | 5.76 | 8.25 |
| NY (4.5) | from 08-20 | 16,290 | **4.60 %** | 89.7 % | 45.2 | 0.78 | 3.06 | 4.36 |

- The ratio's **median** moves by 0.06–0.12. Its **p90** roughly halves in every session. The step is in the tail.
- Same-side rises after the step in every session. The fires that remain agree with TFI more often.

### 2.2 Fire rate by ATR fifth — all rows, and inside the post-step regime only

| Session | ATR fifth | All rows: fire rate (p90) | From 08-20 only: fire rate (p90) |
|---|---|---:|---:|
| ASIA | 1 (lowest) | 15.46 % (7.41) | 5.74 % (4.02) |
| ASIA | 2 | 8.51 % (4.92) | 4.61 % (3.73) |
| ASIA | 3 | 5.13 % (3.88) | 5.49 % (3.70) |
| ASIA | 4 | 4.69 % (3.31) | 3.12 % (2.71) |
| ASIA | 5 (highest) | 2.87 % (2.62) | 2.86 % (2.53) |
| LONDON | 1 | 18.65 % (9.21) | 6.30 % (4.22) |
| LONDON | 5 | 1.86 % (2.31) | 1.68 % (2.31) |
| NY | 1 | 18.55 % (7.17) | 9.52 % (4.44) |
| NY | 2 | 9.72 % (4.46) | 5.77 % (3.41) |
| NY | 3 | 5.78 % (3.40) | 4.27 % (3.06) |
| NY | 4 | 3.45 % (2.90) | 2.46 % (2.55) |
| NY | 5 | 1.48 % (2.23) | 0.98 % (2.14) |

- **Inside the post-step regime the dependence persists.** It is steep in NY (9.52 % → 0.98 %) and flatter in ASIA and LONDON, where the post-step ATR range is narrower. So the step is not just a one-off level shift; bar-level ATR predicts fire rate within a regime too.
- Day level, from 2026-08-20: r(daily median ATR, daily fire rate) = −0.23 ASIA, −0.25 LONDON, **−0.71 NY**. NY's daily design effect is 2.51, because NY days still vary widely in ATR.
- LONDON's middle fifths and the full script output are reproducible with the command above.

---

## 3. What this means

- **The fixed threshold was fitted in a low-volatility window.** The ASIA derivation (`asia-burst-threshold-derivation-2026-08-01.md` §1) put p90 at 5.35 over 14 days ending 2026-08-01, when ASIA ATR ran about 30–50. Post-step ASIA p90 is 3.39.
- **The watch assumes a stable fire rate; the mechanism does not deliver one.** A fixed-reference watch on a volatility-conditional rate will read MISS for as long as volatility stays high, then possibly MISS high when it falls. As designed, it alarms on the market, not on the engine.
- **The live effect is fewer upgrades, not wrong ones.** On res-3 the modifier is upgrade-only (`asia-burst-threshold-derivation-2026-08-01.md` §3). In high volatility it fires less and, when it fires, agrees with TFI more. That is the conservative direction `trader-profile.md` §6 prefers. **It is not evidence of edge in either regime.** The only outcome read is still the W6-4 AUC 0.5179 (n = 217), neutral at best.
- **Whether this is a defect or intended is a design question, and the data cannot settle it alone.** "Burst" means "unusual relative to recent flow". In a busy market fewer moments are unusual, which is a defensible reading. The ~10 % design point in the derivations was an engagement target, never an edge finding.

---

## 4. Not measured

- **Outcomes.** Nothing here is joined to forward returns. Whether a burst-upgraded TFI vote earns net EV per trade, in either regime, is unknown.
- **`grossFast` and `grossNorm` per row.** Not logged, so §1's intermittency mechanism is inferred, not decomposed.
- **The cause of the 2026-08-20 volatility regime.** No market-event, funding or OI check.
- **Row-level lean.** `NORMAL` rows at ratio ≥ threshold are attributed to the lean floor as in the earlier reads; `lean` is not logged.
- **ATR units across resolutions.** Bins are per session, so 1-minute and 3-minute ATR are never compared directly.

---

## 5. Decisions queued for the trader — `AVR-1` and `AVR-2` (new IDs for this read; checked free in `docs/`, `Core/`, `verify/`)

### `AVR-1` — what the burst watches measure against

| Option | What it does | Class |
|---|---|---|
| (a) Keep T-1's fixed 11 % reference | The ASIA watch (and the NY 8–12 % band) keeps reading MISS while volatility is high. Alarm fatigue | Watch ruling |
| **(b) Make the reference ATR-conditional** | Replace the fixed reference with the measured fire rate per ATR fifth (§2.2, "all rows" column) per session. A read then flags a session only when its fire rate departs from what its own ATR predicts. Band width T-2 stays; §0 row 3 shows it still fits inside a regime | Watch ruling; no settings, no scoring |
| (c) Retire the burst watches | Stops the alarm; loses the only instrument on this modifier | Watch ruling |

**My read: (b).** It records more than (a) and (c): it keeps the watch and makes a MISS mean something the engine did. Three-step test (`CLAUDE.md`): (b) is the richer option, so no trade exists. ⚠ It is still the trader's call, because it re-rules T-1, which the trader ruled on 2026-08-11. It also covers the NY watch in `DeribitIndicatorProject.md` §12, which would move to the same form.

### `AVR-2` — whether the threshold itself changes

| Option | What it does | Class |
|---|---|---|
| **(a) No threshold change until an outcome read exists** | Thresholds stay NY 4.5, LONDON 5.5, ASIA 5.5. The next step is an outcome read: net EV per trade of burst-upgraded versus non-upgraded directional rows, split by ATR fifth, pre-registered before it is run | Read only |
| (b) Re-fit the fixed thresholds to the current regime (post-step p90: ASIA about 3.4, LONDON about 3.2, NY about 3.1) | Restores ~10 % now; over-fires if volatility falls back (the low-ATR fifth's p90 is 7.2–9.2) | ⛔ Reserved: live scoring change and dataset boundary |
| (c) Make the threshold volatility-conditional (ATR-scaled, or a rolling percentile of the ratio) | Holds the fire rate across regimes by design | ⛔ Reserved: live scoring change, needs a spec |

**My read: (a).** ⚠ **This is the cheaper option, so it is argued, not assumed.** Options (b) and (c) buy a stable fire rate. A stable fire rate is an engagement property; nothing yet links it to edge, and the only outcome number (AUC 0.5179) is neutral. Changing scoring to hold an unvalidated design point would be tuning a dial whose value is unknown. The outcome read in (a) is the option that produces the missing information; (b) and (c) produce none. The "defer" tell in `CLAUDE.md` applies to deferring a schema or information cost; here (a) is the information-gathering option, not the deferral. ⛔ **If you read it the other way, (c) is the more principled of the two scoring options:** (b) re-fits to one regime and breaks in the next.

---

### Rulings — 2026-09-26 (UTC), trader

| Decision | Ruling | Follow-up |
|---|---|---|
| `AVR-1` | ✅ **(b) — the watch reference becomes ATR-conditional** (measured fire rate per ATR fifth, per session). It re-rules T-1 of `d3-asia-burst-watch-read-2026-08-10.md` §10 and applies to the NY watch in `DeribitIndicatorProject.md` §12 as well. T-2's band width stays | Build: the watch scripts read against the §2.2 per-fifth reference. Tools only |
| `AVR-2` | ✅ **(a) — no threshold change until the outcome read**, taken on the condition that the outcome read is useful for (c). Orchestrator answer: **it is**, for three reasons below | ⏰ **REMINDER: when the outcome read is done, re-open (c)** (a volatility-conditional threshold) with its result |

**Why the outcome read is useful for (c).** The trader asked this before ruling.

1. **It decides whether (c) is worth doing at all.** (c) adds burst upgrades in high-volatility markets to hold the fire rate steady. If burst-upgraded rows earn no net EV in the high-ATR fifths, (c) adds upgrades exactly where they lose. If they earn it in every fifth, (c) captures more of it.
2. **It gives (c) its target.** The per-fifth split shows which ATR range the burst carries edge in, so (c) can be shaped to fire where it pays, rather than to an engagement rate of ~10 %.
3. **It gives (c) a clean before-and-after.** (c) is a live scoring change and a dataset boundary. With a pre-registered outcome read on the fixed threshold, the same read re-run after (c) ships measures what (c) changed.

⚠ **Power risk, named so it is not a surprise.** Bursts are rare in the high-ATR fifths (about 1–3 % of rows since 2026-08-20). The outcome read may be underpowered there. If it is, it must say so, and the (c) decision then rests on the fifths it can read.

## 6. What I did not verify

✅ **Closed 2026-09-26 (UTC), after the first draft:**
- **The derivation window's volatility, re-counted on the AWS book.** ASIA weekday rows 2026-07-22 … 07-31 (before arming): n = 1,113, ratio p90 **5.59**, median ATR **44.1**, fire rate 22.10 % at the old default 2.5. The derivation reported p90 5.35 and 21.7 % on a pooled book of n = 1,489 that also held local-box rows, so the two are not the same population, but they agree closely. §3's "fitted in a low-volatility window" stands: ATR 44 against ~70 now. Command: `tools/ops/aggr-vel-regime-read.ps1 -FetchFolder 'aws_fetch\20260925-085341' -From 2026-07-22 -StepAt 2026-08-01`.
- **No NY watch read after 2026-08-20 exists in `docs/`.** A grep for an NY burst or `AggrVel` watch or fire-rate mention found only `aggressor-velocity-s52-derivation-2026-07-13.md` and `aggr-vel-wirein-implementer-brief.md`, which define the watch and predate the step.

Still open:
- LONDON's written band, if any. I found none in the docs I read this session.
- Bin boundaries fall between equal-ATR rows arbitrarily; ties at a boundary go to whichever bin the sort puts them in.
