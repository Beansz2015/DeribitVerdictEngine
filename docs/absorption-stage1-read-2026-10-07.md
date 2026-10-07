# Absorption `D-6d` Stage 1 read — 2026-10-07 (UTC)

**Spec:** [`d6d-episode-continuity-spec.md`](d6d-episode-continuity-spec.md) §4 (the instrument), §5 (Stage 2 options), §0 (escalation triggers). Deploy and watch: [`absorption-d2-s2-batch-summary.md`](absorption-d2-s2-batch-summary.md) §5 step 6. Seat: Opus 5.5, high.

**Data:** fetch `aws_fetch/20261007-180541` (gitignored). `absorption_episodes.log`: 11,874 lines, 0 malformed, 2026-09-24 18:46 → 2026-10-07 18:05 UTC, 11 instance ids, all after the 2026-09-24 deploy (`25951567…`). Weekday-scoped (the weekday-scope ruling): 3,678 weekend lines excluded, **10 weekday dates** (09-24 and 10-07 partial). Ruled read length: ~2 weekday-weeks. Met.

**Script:** [`tools/ops/absorption-stage1-read.py`](../tools/ops/absorption-stage1-read.py) — `python tools/ops/absorption-stage1-read.py aws_fetch/20261007-180541`. Output MD5 `08e1f9a88270af7a8bd19ad9c35a4074` (run twice, identical).

---

## 0. Verdict

| Question | Answer |
|---|---|
| Counting gap, USD, pooled | **34.0 %** (day-block 95 % CI 30.3–39.0 %). Excluding shadow after a `BreakThrough` close: **33.3 %** |
| Escalation trigger "outside roughly 20–45 % ⇒ the §2 diagnosis is wrong" (`d6d-episode-continuity-spec.md` §0) | **Does not fire.** Inside the band, CI included. See §3 for why the 31 % comparison is not like-for-like |
| Which close path drops the flow | ⭐ **`LadderSpanLost`: 46.5 % of closes, 49.9 % of the dropped (shadow) USD.** Then `TouchCrossed`: 26.7 % of closes, 31.2 % of shadow |
| `d6d-episode-continuity-spec.md` §5 selection | `LadderSpanLost` dominates ⇒ **F-1 (ladder hysteresis) or F-3 (deeper book)**. **F-4 (nothing) is ruled out:** `BreakThrough` carries 13.1 % of closes and 3.0 % of shadow |
| `D-2` STOP watch: flagged rate up **and** ratio distribution shifted left | **Not met.** Flagged share of active reads 0.70 % → 1.35 %, but the ratio moved **right** (p90 0.080 → 0.390), as `D-2` predicts |
| Stage 2 build | **None now.** Holiday freeze, and the choice is reserved (decisions `S1R-1`, `S1R-2` in §5) |

---

## 1. Counting gap

`gap = shadow_usd / (press_accrued_usd + shadow_usd)`. Both terms are interval flows, reset at each take (`Core/LevelAbsorptionTracker.vb` `TakeSide`, read this session), so lines pool by summation.

| Population | Live press accrued (USD) | Shadow (USD) | Gap | Gap excl. `BreakThrough` shadow |
|---|---:|---:|---:|---:|
| ALL | 384,744,940 | 198,442,020 | **34.0 %** | 33.3 % |
| ABOVE | 193,102,230 | 111,321,100 | 36.6 % | 35.9 % |
| BELOW | 191,642,710 | 87,120,920 | 31.3 % | 30.5 % |
| ASIA (00–07 UTC) | 125,948,470 | 57,445,080 | 31.3 % | 30.7 % |
| LONDON (08–12 UTC) | 88,941,000 | 63,672,000 | **41.7 %** | 41.2 % |
| NY (13–23 UTC) | 169,855,470 | 77,324,940 | 31.3 % | 30.5 % |

- Per weekday: 09-24 34 % · 09-25 34 % · 09-28 49 % · 09-29 36 % · 09-30 28 % · 10-01 28 % · 10-02 34 % · 10-05 30 % · 10-06 29 % · 10-07 33 %. One high day (09-28); no trend.
- LONDON runs about 10 pp above ASIA and NY. Not tested; noted only.
- ⚠ Session hours here are the read's own UTC buckets (the raw-book test's buckets), not the engine's `session_volume` table.

## 2. Close reasons — attribution

Weekday, both sides. `mean life` = summed lifetimes ÷ closes (`Reset` has no clock).

| Reason | Closes | Share | Mean life (s) | Shadow after it (USD) | Shadow share |
|---|---:|---:|---:|---:|---:|
| `DegenerateLadder` | 0 | 0.0 % | — | 0 | 0.0 % |
| `LevelRemap` | 801 | 2.2 % | 6.07 | 18,592,750 | 9.4 % |
| `ProximityShut` | 4,164 | 11.5 % | 4.42 | 12,823,790 | 6.5 % |
| ⭐ **`LadderSpanLost`** | **16,871** | **46.5 %** | 2.60 | **99,091,550** | **49.9 %** |
| `BreakThrough` | 4,759 | 13.1 % | 4.08 | 5,977,930 | 3.0 % |
| `Reset` | 10 | 0.0 % | — | 23,270 | 0.0 % |
| `TouchCrossed` | 9,694 | 26.7 % | 3.54 | 61,932,730 | 31.2 % |

- Shadow prints 42,589; idle intervals ended by a break-class print 11,762.
- Per session, `LadderSpanLost` share of closes: ASIA 58 % · LONDON 57 % · NY 38 % (NY has more `ProximityShut`, 17 %). Shadow share: ASIA 50 % · LONDON 63 % · NY 39 %. LONDON's higher gap sits on `LadderSpanLost`.
- ⭐ **What `LadderSpanLost` means in the shipped code** ([`absorption-d2-s1-spec-back.md`](absorption-d2-s1-spec-back.md) §3.2 table): the old level is beyond the worst visible ask (or bid) AND within proximity of the touch — **only the ten-deep book closed it.** So half the dropped flow is a top-10 visibility artefact by construction, not a market event. This answers the question `d6d-episode-continuity-spec.md` §2.3 and §10 said could not be measured from stored data: the ladder is the binding term about half the time.
- **Cross-check against the raw-book probe** ([`raw-book-absorption-measure-spec.md`](raw-book-absorption-measure-spec.md) §1.4, arm A, 24 h): 1,560 `LadderSpanLost` closes per day. Here: 16,871 over ~9.0 effective weekday-days ≈ 1,900 per day. Same order.

## 3. The 20–45 % band and the 31 % — not like-for-like

The trigger passes. The comparison behind it does not hold, and the spec should say so:

| Item | 2026-08-19 replay (`absorption-blind-rederivation-2026-08-19.md` §5.2(b)) | This read |
|---|---|---|
| What 31 % is | ⚠ the share the engine **counted**: 22 logged-pressed rows of 72 that qualified. The **missed** share by that count is 69 % | the **missed** share: shadow ÷ (counted + shadow) |
| Unit | rows | USD |
| Numerator code | pre-`D-2`: 10 s window, wiped at every close | post-`D-2`: episode-cumulative |

- `d6d-episode-continuity-spec.md` §4.1 calls the gap "directly comparable to the replay's 31 %". It is not: 31 % is the counted share in rows; the gap is the missed share in USD, after `D-2` removed the window truncation.
- So the 34 % neither confirms nor contradicts the replay. It is the first direct measurement of the idle-drop loss.
- ⚠ The 20–45 % band was written against the 31 %. I applied it literally (it passes). I did not re-derive what band the spec should have set.

## 4. `D-2` STOP watch and the pull veto — the raw-book caveat

Weekday rows of `analysis_log.csv`. "Pre" = the 116-column book from 2026-09-01 15:50 (the instrumentation deploy) to the 09-24 deploy; "post" = since the 09-24 deploy.

| Book | Rows | Absorption-active | Flagged `ABSORB_*` | Flagged / active | Ratio p50 · p75 · p90 | `pullFrac` p50 · p90 | `pullFrac` > 0.75 |
|---|---:|---:|---:|---:|---|---|---:|
| Pre | 14,508 | 2,134 | 15 | 0.70 % | 0.000 · 0.000 · 0.080 | 0.705 · 3.156 | 47.8 % |
| Post | 8,202 | 1,407 | 19 | 1.35 % | 0.000 · 0.000 · **0.390** | 0.563 · 4.298 | 45.6 % |

- **The STOP condition is not met.** The ratio shifted right, which is `D-2`'s designed effect (episode-cumulative press over episode-scoped depletion). 15 and 19 flags are too few to read a rate change.
- ⛔ **The pull veto's input is biased LOW.** The raw-book test ([`raw-book-absorption-test-results-2026-10-06.md`](raw-book-absorption-test-results-2026-10-06.md), session 3, quarantined, one 24 h run) confirmed H-NET: the 100 ms fold nets within-bucket posts and pulls away, so `pullFrac` reads lower than the raw book's. At `max_pull_frac` 0.75, 19.5 % of passing aligned pairs flip to VETO on raw (5.4 % the other way).
  - The 45.6 % share above 0.75 is therefore a **lower bound** on what a raw measure would show.
  - Some of the 19 post-deploy flags passed the veto on an understated `pullFrac`. Which ones cannot be said from this data. On the probe day the `ABSORB` tag fell from 162 to 71 per day on raw (`raw-book-absorption-measure-spec.md` §1.4).
  - **No veto change follows from this read.** `max_pull_frac` stays 0.75 (trader ruling 2026-10-06: re-derive on raw-measured data inside the activation calibration).
- ✅ **The counting gap and the close-reason split do not use `pullFrac`.** They are not biased by H-NET. They ARE feed-dependent through the ladder: see `S1R-1`.

## 5. Decisions queued for the trader

Both are reserved: a Stage 2 change moves the `Absorption*` CSV columns and the TAPE-strip tag (a rendered-value change and a dataset boundary), and `S1R-1` interacts with the open `RBM-1` (`raw-book-absorption-measure-spec.md` §9). No build before 2026-11-25.

| # | Decision | Options | My read |
|---|---|---|---|
| **`S1R-1`** | Stage 2 for the dominant cause, `LadderSpanLost` | (a) **F-1** ladder hysteresis on the 100 ms top 10: hold the episode while the level is out of view; band size unmeasurable then (freeze or skip) · (b) **deeper ladder from the rebuilt raw book:** the raw-book build already rebuilds the full depth; hand the tracker more than 10 levels. No new subscription, OFI untouched (it reads the main feed). Unmeasured: needs a probe arm first · (c) **F-3 as written:** subscribe `book.<i>.none.20.100ms`; OFI reads the same ladder · (d) choose after the raw-book measure ships, from a Stage 1 re-read on the raw feed | ⭐ **(b), with a pre-registered probe arm before any build.** The raw-book spec measured `LadderSpanLost` ×2.7 on a raw top 10 (§1.4), and its §5 keeps the tracker at 10 levels — so `RBM-1` (a) alone makes the cause this read found worse. (b) removes the cause rather than bridging it. (a) carries a band trajectory nobody can see, which `d6d-episode-continuity-spec.md` §5 calls a silent lie. (c) moves OFI. (d) defers into a dataset boundary that (b) avoids paying twice |
| **`S1R-2`** | `TouchCrossed` (ruling `Q-2` (a): add a row once the read shows its share) — 26.7 % of closes, 31.2 % of shadow | (a) write a derivation of what the band trajectory means while the touch trades through the level, then decide · (b) treat these closes as legitimate (F-4 for this reason) · (c) add an F-5 option now: a crossing without a break-tolerance breach does not close | **(a).** The shadow after a `TouchCrossed` close is flow the live trade predicate classes as press, not break — evidence the crossing was not a break by the tracker's own rule. But keeping an episode open across a crossed touch measures the level from the wrong side of the book. That needs a derivation, not a guess. Order: after `S1R-1` |

**Harness 6 (decision-bias tripwire)** ran on both rows at `4c0a159`, after my labels were written (`no_richer_option` for both). Jev: `richer_option_wrong` for both, 5 of 5 samples (mean top p 0.866 and 0.614). **No economy flag.** Files: `docs/harness-runs/decision-bias-20261007T1814Z-s1r-{population,baseline,jev}.json`.

## 6. What this changes elsewhere

| Doc / item | Effect |
|---|---|
| `d6d-episode-continuity-spec.md` §5 | The `TouchCrossed` row is owed (`Q-2`); a pointer to this read is added to its header |
| `raw-book-absorption-measure-spec.md`, `RBM-1` | Its §1.4 "not verified: whether the `LadderSpanLost` closes are top-10 window artefacts" is now answered for the 100 ms feed: by construction yes, about half of all dropped flow. `S1R-1` should be ruled with `RBM-1` |
| The gated absorption mechanism-revision build | Gains a measured Stage 2 input. Still gated; nothing builds before 2026-11-25 |

## 7. Verified and not verified

**Verified (this session):**
- Every number above: the script's output, run twice, identical MD5.
- The four tally fields and the per-take resets: read in `Core/AbsorptionEpisodeLog.vb` (format comment, `FormatSide`) and `Core/LevelAbsorptionTracker.vb` (`CloseEpisode`, the idle arm, `TakeSide`).
- The `LadderSpanLost` / `TouchCrossed` geometry: read in `absorption-d2-s1-spec-back.md` §3.2, not in the code.
- The replay's 31 % meaning: read in `d6d-episode-continuity-spec.md` §1 ("It counts 31 % of what it should").

**Not verified:**
- The replay figures (22 / 72 / 31 %) — carried from `absorption-blind-rederivation-2026-08-19.md`, not re-run.
- That every take writes a sidecar line. A take whose append failed would lose its interval; the log has no sequence counter to show it.
- Whether the 19.5 % raw veto flip and the 162 → 71 tag drop hold beyond the one probe day.
- Whether option `S1R-1` (b) removes `LadderSpanLost` in practice. It is unmeasured; that is why the read asks for a probe arm.
- LONDON's higher gap: no test was run.
