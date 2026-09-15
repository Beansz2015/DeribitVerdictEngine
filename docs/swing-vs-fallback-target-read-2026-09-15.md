# Swing targets vs ATR-fallback targets: the observational read (session 1)

**Written:** 2026-09-15 (UTC) by a scoped analysis seat. **Brief:** [`docs/swing-vs-fallback-target-read-brief-2026-09-14.md`](swing-vs-fallback-target-read-brief-2026-09-14.md), session 1 only: the join, the forward walk in both outcome modes, and Part A. **Part B (the counterfactual geometry) was not run.**

**Vocabulary:** [`docs/DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a (success rate · gross/net breakeven rate · gross/net edge · net EV per trade).

**Instrument:** [`tools/ops/SwingFallbackRead/`](../tools/ops/SwingFallbackRead/SwingFallbackRead.vb), a host-agnostic VB console project. **Full real output:** [`docs/swing-vs-fallback-target-read-2026-09-15-output.md`](swing-vs-fallback-target-read-2026-09-15-output.md). The tables in §4 and §5 of this doc copy rows from that output. Era labels drop their numeric sort prefix, and zero values print as `0.0`.

Abbreviations used in tables: **S+M** = STRONG and MEDIUM tiers together (the bands the bridge trades). **BE** = breakeven rate. **CI** = 95 % confidence interval, cluster-robust by session-day. **`none`** = the `TargetCapReason` value for an ATR-fallback target.

---

## 1. Verdict

**No. Swing targets do not earn a better net EV per trade than ATR-fallback targets in any session, in either outcome mode.** No cell with n ≥ 30 has a net EV per trade whose CI lies above zero.

| Session | Tiers | ATR fallback, main window | Swing, main window | ATR fallback, carried 24 h | Swing, carried 24 h |
|---|---|---|---|---|---|
| NY | ALL | −3.8 [−5.0, −2.6], n 2,852 | −3.4 [−4.9, −2.0], n 1,758 | −3.9 [−5.2, −2.6] | −3.3 [−5.0, −1.6] |
| NY | S+M | −3.8 [−5.6, −1.9], n 1,198 | −4.9 [−6.8, −2.9], n 547 | −3.9 [−5.8, −2.0] | −5.0 [−7.3, −2.8] |
| LONDON | ALL | −0.1 [−2.3, +2.2], n 891 | −2.4 [−4.5, −0.2], n 642 | +0.4 [−2.1, +2.9] | −2.4 [−5.5, +0.7] |
| LONDON | S+M | +0.1 [−2.6, +2.8], n 427 | −3.0 [−5.3, −0.7], n 158 | +0.8 [−2.0, +3.6] | −3.8 [−6.6, −1.1] |
| ASIA | ALL | −2.4 [−4.5, −0.4], n 734 | −1.8 [−4.1, +0.5], n 846 | −2.4 [−4.5, −0.3] | −2.1 [−4.7, +0.6] |
| ASIA | S+M | −2.5 [−5.0, 0.0], n 302 | −3.0 [−6.4, +0.5], n 153 | −2.6 [−5.4, +0.1] | −3.0 [−6.7, +0.7] |

Net EV per trade in bps, maker/maker fees (3 bps round trip). Main window = NY 15 min, LONDON and ASIA 45 min.

**Per session:**

- **NY.** Both target types lose 3–4 bps a trade. At ALL tiers swing is 0.4–0.6 bps better, inside the CI. For S+M swing is about 1 bps worse. The direction call carries no gross edge: carried gross edge is −3.7 pp for the fallback and −1.2 pp for swing.
- **LONDON.** The fallback sits at breakeven in both modes; its CI straddles zero. Swing loses about 2.4 bps. The fallback is better by 2.3–2.8 bps, but the CIs overlap. The direction call shows a gross edge (carried +5.8 pp fallback, +4.1 pp swing). That edge is smaller than the fee, which costs about 8 pp of breakeven rate.
- **ASIA.** Both lose about 2 bps. There is no separation.

**Across all sessions:**

- **Distance-matched, swing is no better than fallback** (§5 of this doc). At equal target distance swing equals the fallback in NY and is 1–2 bps worse in LONDON and ASIA. The "structure helps" reading is not supported.
- **Carrying trades to conclusion changes nothing.** UNRESOLVED is 0–0.1 % at the 24 h cap. p90 time to resolution is 9–65 min. For `none` and `swing`, net EV per trade moves by at most 0.6 bps at ALL tiers and 0.8 bps for S+M, against the main window. The extra swing timeouts in the brief's preliminary read did not hide an edge.
- **Funding is negligible:** the largest mean funding effect in any carried cell is 0.010 bps. Per brief rule 4 in `docs/swing-vs-fallback-target-read-brief-2026-09-14.md` §3.2, funding is not added to net EV.
- **Best cells.** Largest positive cell with a real sample: LONDON `none` MEDIUM, carried 24 h, **+1.2 bps [−1.7, +4.2], n 321**. Best cell with n ≥ 50: ASIA `none` STRONG, carried 24 h, **+3.0 bps [−3.6, +9.6], n 51**. The top cell by net EV is ASIA swing STRONG, carried, +8.9 [+1.7, +16.0], at **n 12**. It is one of about 200 cells and I read it as noise. **No split-half holdout was run.** Holdout belongs to Part B.
- **No geometry or `settings.json` change is justified, so there is no D-table.** No stop condition in `docs/swing-vs-fallback-target-read-brief-2026-09-14.md` §0 fired.

**Against the brief's preliminary read** (`docs/swing-vs-fallback-target-read-brief-2026-09-14.md` §1: 59.5 % join subset, timeouts scored flat):

| Session | Target set by | Preliminary net EV | This read, main window | This read, carried 24 h |
|---|---|---|---|---|
| NY | ATR fallback | −3.9 | −3.8 | −3.9 |
| NY | Swing | −3.6 | −3.4 | −3.3 |
| NY | HVN | −3.2 | −2.6 | −1.7 |
| LONDON | ATR fallback | −0.6 | −0.1 | +0.4 |
| LONDON | Swing | −1.9 | −2.4 | −2.4 |
| ASIA | ATR fallback | −3.1 | −2.4 | −2.4 |
| ASIA | Swing | −2.9 | −1.8 | −2.1 |

- The preliminary claim "LONDON is the closest session to breakeven" **holds**.
- The claim "swing is not better than fallback in any session" **holds for S+M**. At ALL tiers swing is marginally better in NY and ASIA, inside the CI.
- The claim that the timeouts would change the picture **does not hold**.

---

## 2. The join finding

**Cause: the eval cache holds every pre-2026-09-01 row twice.** One copy is the live row. The other is a cold-start backfill copy, stamped with a whole-second `.0000000Z` timestamp. A one-to-one ±3 s join can match only one copy of each pair, so the other copy counts as "unjoined".

| Measure | Value |
|---|---|
| Eval cache rows | 78,845 |
| Trading-week directional outcome rows | 12,616 (12,144 excluding `NO_DATA`) |
| of which backfill copies (whole-second) | 5,333 |
| of which live copies (sub-second) | 7,283 |
| Naive join (±3 s, same verdict, one eval row per log row, `NO_DATA` excluded) | **7,223 of 12,144 = 59.5 %**, the brief's figure exactly |
| Log rows carrying two eval copies | 5,334 (none carries three) |
| Duplicate pairs whose outcomes disagree | 520 |
| Live-copy lag behind its log row | 0–3 s for 6,978 rows; 4–21 s for 304 rows |
| **Fixed join** (live copy to the nearest earlier log row within 60 s, backfill copy exact, one copy per log row) | **7,281 of 7,282 = 99.99 %** |
| Collector-era population rows with a joined eval row | 7,281 of 7,283 = 99.97 % |

- **The duplicates come from the backfill dedup-key namespace defect.** `LivePerformanceTracker.BackfillNewEntries` documents this defect in its own comment: identity-keyed log rows could not match timestamp-keyed cache rows, and the fix shipped as commit `1aeae5a` (the S-4 eval-cache identity fix). The box cache is still schema v6, so these copies pre-date that fix.
- **I did not use the eval cache for outcomes, even after fixing the join.** It cannot supply the 5/10 or 15/30 min windows, a mark for a timeout, or a carried outcome. Its production walk also starts at T+3, not at the signal. I walked every row from 1-minute Deribit candles for both modes, as the brief's fallback allows. The fixed join serves only as a cross-check.
- **Cross-check against the candle walk:** with the production window (bars closing T+3 to T+15 × resolution) and the eval row's own barriers, **6,722 of 7,281 outcomes agree (92.32 %)**. The eval barriers equal the logged placed levels on 7,280 rows.

| Eval outcome → candle-walk outcome | Rows |
|---|---|
| `WINDOW_EXPIRED` → `ADVERSE_HIT` | 208 |
| `WINDOW_EXPIRED` → `SUCCESS` | 159 |
| `ADVERSE_HIT` → `SUCCESS` | 99 |
| `SUCCESS` → `ADVERSE_HIT` | 86 |
| `SUCCESS` → `AMBIGUOUS` | 4 |
| `ADVERSE_HIT` → `AMBIGUOUS` | 3 |

- ⚠ **The disagreement is one-sided.** No eval row records a barrier hit that the venue candles lack. That pattern fits eval-side bars with understated high/low ranges, for example a forming 1-minute bar stored before it closed. **I did not verify the cause.** It affects the live perf strip and any eval-cache read, not this read.

---

## 3. Method

### 3.1 Population

| Step | Rows |
|---|---|
| Loaded (pooled book + box live log) | 58,705 |
| Identical duplicate timestamps dropped (the pooled book already holds the live log to 2026-09-09) | 7,284 |
| Conflicting duplicate timestamps | 0 |
| Non-directional verdicts | 41,583 |
| Directional rows before the v51 geometry boundary (2026-07-06 13:08:51 UTC) | 59 |
| Non-collector instance rows inside the collector era | 2 |
| Outside the trading week | 967 |
| Invalid placed levels | 0 |
| **Population** | **8,810** |

- **Span:** 2026-07-07 08:39 to 2026-09-11 22:55 UTC.
- **Trading week:** Monday 00:00 to Friday 24:00 UTC. Read from `session_volume.sessions` in `settings.json` v68: ASIA hours 0–7, LONDON 8–12, NY 13–23.
- **Session:** the `session_volume` bucket of the signal's UTC hour. Every row's logged `ExecResolution` matches its session's configured resolution (0 mismatches).
- **Inputs:** `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`, plus `aws_fetch/20260913-153704/analysis_log.csv` for rows after 2026-09-09. The eval cache and `.v0.7.bak` come from the same `aws_fetch` folder.

### 3.2 Label meaning — confirmed in code and in data

- **Code.** `SignalEmitter.ComputeStructuralSideLevels` places the target by a ladder: swing → nearest HVN → POC → session ATR fallback. `AnalysisLogger.NormaliseCapReason` writes `none` whenever the reason string is empty. So `none` means the ATR fallback, or a structural target within the noise floor of the fallback price (max of 0.5 USD and 0.02 × ATR).
- **Data.** 100 % of `none` rows sit within 0.021 × ATR of the session fallback multiple (NY 1.75, LONDON 2.00, ASIA 1.25), in every era. 100 % of `swing` rows equal the logged `SwingTarget` for the side. 100 % of `hvn` rows equal the logged nearest HVN.
- **The stop barely differs by label.** The median stop is 1.60 × ATR in every cell. 85–100 % of `none` and `swing` stops sit at the 1.6 × ATR bound. Small `hvn` cells go down to 60 %. **Swing and fallback differ in the target only.**
- **Target distance:** median 1.9–2.6 × ATR for `swing` by session and era (p5–p95 about 0.8–3.4), against the fixed fallback multiple.

### 3.3 Outcome walk

- **Candles:** 1-minute BTC-PERPETUAL bars from `analysis/DeribitOhlcFetcher.vb`, Monday open to Friday close, 10 weeks from 2026-07-06. **7,200 of 7,200 bars in every week; 0 missing; 0 population rows with a missing bar in any walk.**
- **First bar:** the bar that contains the signal. The sensitivity table (§5.4 of this doc) starts at the next full bar instead.
- **Same-bar ambiguity** (target and stop inside one bar) counts as a stop hit.
- **Window-bound mode:** NY 5/10/15 min, LONDON and ASIA 15/30/45 min, from `AnalysisConstants.HoldWindowsForResolution`. A timeout is marked to the window-close bar close. Windows are also capped at Friday's close.
- **Carried mode:** first touch of the placed target or stop. The cap is the earlier of 24 h and Friday's session close. An open trade at the cap is marked to the cap-bar close and counted as UNRESOLVED. A session-end variant caps at the signal's session close.
- **Funding:** Deribit `public/get_funding_rate_history` supplies `interest_1h` hourly (1,680 records, 0 hours missing). It accrues per minute over the hold.
- **Fees:** `scoring.trade_costs` gives maker/maker at 3.00 bps round trip. The taker-stop case charges 3.00 bps on a target hit and 5.00 bps on a stop hit or a marked exit.
- **Breakeven rates** are distance-weighted from each row's own placed distances in bps, per `docs/DeribitIndicatorProject.md` §5a rule 1. Taker-stop net BE uses Σ(stop + fee on loss) ÷ Σ(target − fee on win + stop + fee on loss).

### 3.4 Decisions taken on my own recommendation (auto-proceed log)

| Decision | Options | Picked | Why |
|---|---|---|---|
| Outcome source | (a) fixed eval join for the window-bound mode · (b) candle walk for both modes | **(b)** | The eval cache mechanically cannot supply the 5/10 and 15/30 min windows, timeout marks or carried outcomes. Its bars also disagree with venue candles on 7.7 % of rows. The richer data is the candles |
| Pre-v51 rows (59) | include · exclude | **exclude, counted** | Settings v51 (commit `9ab3f04`, the structural-first geometry) is a dataset boundary in its own `change_log`. Earlier `TargetCapReason` values mean a different ladder with a 2.0 × ATR target and a 1.2 × ATR stop, so including them is mechanically wrong |
| Pre-collector dev runs, 2026-07-07 to 07-22 | include · exclude | **include as their own era** | Richer option. Their label geometry matches the collector era 100 % (§3.2 of this doc) |
| Non-collector rows inside the collector era (2) | include · exclude | **exclude, counted** | Concurrent duplicates of the same market minute from a second process |
| Funding model | settlement-crossing only · continuous hourly accrual | **continuous accrual** | Records more. The largest effect is 0.010 bps, so neither model can move a cell |
| CIs | iid · cluster-robust by session-day | **cluster-robust** | Signals inside one session overlap heavily; iid CIs would be too narrow |
| Distance buckets | raw ATR multiple · rounded to 3 dp | **rounded** | Keeps fallback rows at exactly 2.00 × ATR inside the 2.0–2.5 bucket, not split by float noise |

---

## 4. Part A tables

Full tier rows (MEDIUM and WEAK included) are in the output file, sections 6, 8 and 9. The rows below are copied verbatim, with MEDIUM and WEAK omitted.

### 4.1 Window-bound, main window (NY 15 min, LONDON and ASIA 45 min), maker/maker

| Session | Target set by | Tier | n | Success rate [CI] | Stop hit % | Timeout % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Net EV bps [CI] | Med target / stop ×ATR | Resolution p50 / p90 min |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | none | ALL | 2852 | 42.3 [38.8, 45.8] | 52.6 | 5.1 | 47.7 | 58.3 | -5.4 | -16.0 | -3.8 [-5.0, -2.6] | 1.75 / 1.60 | 3 / 9 |
| NY | none | S+M | 1198 | 43.8 [38.4, 49.3] | 52.3 | 3.8 | 47.8 | 58.0 | -3.9 | -14.2 | -3.8 [-5.6, -1.9] | 1.75 / 1.60 | 3 / 9 |
| NY | none | STRONG | 259 | 51.4 [42.9, 59.8] | 47.9 | 0.8 | 47.8 | 57.2 | +3.6 | -5.9 | -1.8 [-4.9, +1.4] | 1.75 / 1.60 | 3 / 9 |
| NY | swing | ALL | 1758 | 35.0 [30.2, 39.8] | 54.7 | 10.3 | 41.8 | 52.2 | -6.8 | -17.2 | -3.4 [-4.9, -2.0] | 2.42 / 1.60 | 4 / 11 |
| NY | swing | S+M | 547 | 32.4 [25.0, 39.7] | 56.9 | 10.8 | 43.2 | 53.6 | -10.8 | -21.3 | -4.9 [-6.8, -2.9] | 2.21 / 1.60 | 4 / 11 |
| NY | swing | STRONG | 59 | 22.0 [9.3, 34.7] | 71.2 | 6.8 | 43.0 | 53.2 | -20.9 | -31.2 | -9.2 [-14.7, -3.7] | 2.20 / 1.60 | 5 / 12 |
| NY | hvn | ALL | 807 | 45.6 [40.4, 50.8] | 44.2 | 10.2 | 50.0 | 60.1 | -4.4 | -14.5 | -2.6 [-4.0, -1.3] | 1.82 / 1.60 | 4 / 10 |
| NY | hvn | S+M | 149 | 49.7 [39.5, 59.9] | 38.9 | 11.4 | 53.2 | 61.9 | -3.5 | -12.2 | -3.0 [-6.2, +0.2] | 1.67 / 1.60 | 3 / 10 |
| NY | hvn | STRONG | 24 | 45.8 [19.9, 71.7] | 45.8 | 8.3 | 59.8 | 67.3 | -13.9 | -21.4 | -4.6 [-11.5, +2.4] | 1.14 / 1.60 | 3 / 9 |
| LONDON | none | ALL | 891 | 45.1 [39.3, 51.0] | 47.7 | 7.2 | 44.4 | 52.7 | +0.7 | -7.6 | -0.1 [-2.3, +2.2] | 2.00 / 1.60 | 10 / 32 |
| LONDON | none | S+M | 427 | 47.3 [40.4, 54.2] | 46.8 | 5.9 | 44.4 | 53.0 | +2.9 | -5.7 | +0.1 [-2.6, +2.8] | 2.00 / 1.60 | 9 / 29 |
| LONDON | none | STRONG | 106 | 41.5 [28.5, 54.5] | 53.8 | 4.7 | 44.4 | 53.5 | -2.9 | -12.0 | -1.3 [-6.4, +3.8] | 2.00 / 1.60 | 9 / 23 |
| LONDON | swing | ALL | 642 | 42.1 [35.8, 48.3] | 48.0 | 10.0 | 43.7 | 52.9 | -1.6 | -10.8 | -2.4 [-4.5, -0.2] | 2.12 / 1.60 | 10 / 31 |
| LONDON | swing | S+M | 158 | 41.1 [31.5, 50.8] | 48.7 | 10.1 | 46.8 | 56.2 | -5.6 | -15.0 | -3.0 [-5.3, -0.7] | 1.78 / 1.60 | 7 / 31 |
| LONDON | swing | STRONG | 22 | 36.4 [14.2, 58.6] | 50.0 | 13.6 | 46.5 | 54.8 | -10.2 | -18.5 | -4.1 [-8.9, +0.8] | 1.78 / 1.60 | 9 / 39 |
| LONDON | hvn | ALL | 132 | 37.9 [26.2, 49.6] | 43.9 | 18.2 | 50.9 | 59.5 | -13.0 | -21.6 | -2.2 [-5.6, +1.1] | 1.43 / 1.60 | 8 / 23 |
| LONDON | hvn | S+M | 31 | 35.5 [13.7, 57.3] | 51.6 | 12.9 | 55.3 | 64.0 | -19.8 | -28.5 | -5.6 [-12.9, +1.7] | 1.29 / 1.60 | 9 / 30 |
| ASIA | none | ALL | 734 | 54.6 [48.5, 60.8] | 41.1 | 4.2 | 56.1 | 66.3 | -1.5 | -11.7 | -2.4 [-4.5, -0.4] | 1.25 / 1.60 | 8 / 24 |
| ASIA | none | S+M | 302 | 53.3 [47.1, 59.5] | 42.1 | 4.6 | 56.1 | 66.0 | -2.8 | -12.7 | -2.5 [-5.0, 0.0] | 1.25 / 1.60 | 7 / 24 |
| ASIA | none | STRONG | 51 | 66.7 [54.2, 79.1] | 27.5 | 5.9 | 56.1 | 64.8 | +10.5 | +1.9 | +2.6 [-2.3, +7.5] | 1.25 / 1.60 | 5 / 21 |
| ASIA | swing | ALL | 846 | 41.5 [35.0, 48.0] | 47.4 | 11.1 | 43.4 | 52.4 | -1.9 | -10.9 | -1.8 [-4.1, +0.5] | 2.09 / 1.60 | 11 / 34 |
| ASIA | swing | S+M | 153 | 43.8 [31.5, 56.1] | 47.7 | 8.5 | 47.2 | 56.1 | -3.4 | -12.3 | -3.0 [-6.4, +0.5] | 1.76 / 1.60 | 9 / 30 |
| ASIA | swing | STRONG | 12 | 66.7 [37.2, 96.1] | 16.7 | 16.7 | 53.1 | 62.5 | +13.6 | +4.2 | +6.5 [-0.4, +13.4] | 1.29 / 1.60 | 4 / 10 |
| ASIA | hvn | ALL | 148 | 50.0 [36.1, 63.9] | 46.6 | 3.4 | 51.3 | 59.6 | -1.3 | -9.6 | -5.5 [-12.4, +1.5] | 1.64 / 1.60 | 10 / 30 |
| ASIA | hvn | S+M | 30 | 56.7 [28.9, 84.4] | 40.0 | 3.3 | 56.7 | 64.1 | 0.0 | -7.4 | -5.3 [-17.3, +6.7] | 1.58 / 1.60 | 10 / 28 |

**Every window** (output file section 7), ALL tiers, net EV per trade in bps:

| Session | Target set by | Short window | Middle window | Main window | Timeout % at the short window |
|---|---|---|---|---|---|
| NY (5 / 10 / 15 min) | none | −3.7 | −3.8 | −3.8 | 32.6 |
| NY (5 / 10 / 15 min) | swing | −3.3 | −3.4 | −3.4 | 43.4 |
| LONDON (15 / 30 / 45 min) | none | −0.8 | −0.6 | −0.1 | 37.4 |
| LONDON (15 / 30 / 45 min) | swing | −2.8 | −2.4 | −2.4 | 41.1 |
| ASIA (15 / 30 / 45 min) | none | −2.7 | −2.6 | −2.4 | 26.6 |
| ASIA (15 / 30 / 45 min) | swing | −2.9 | −2.4 | −1.8 | 45.0 |

- Net EV per trade is almost flat across windows, because a timeout marked to the close carries its own result. A short window mostly converts stop hits and target hits into small marks.

### 4.2 Carried to conclusion, cap = earlier of 24 h and Friday close, maker/maker

Funding columns omitted: every cell is within ±0.010 bps (output file section 8).

| Session | Target set by | Tier | n | Success rate [CI] | Stop hit % | UNRESOLVED % | Gross BE % | Net BE % | Gross edge pp | Net edge pp | Net EV bps [CI] | Med target / stop ×ATR | Resolution p50 / p90 min |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| NY | none | ALL | 2852 | 44.1 [40.4, 47.7] | 55.9 | 0.0 | 47.7 | 58.3 | -3.7 | -14.2 | -3.9 [-5.2, -2.6] | 1.75 / 1.60 | 4 / 11 |
| NY | none | S+M | 1198 | 45.1 [39.5, 50.7] | 54.9 | 0.0 | 47.8 | 58.0 | -2.7 | -13.0 | -3.9 [-5.8, -2.0] | 1.75 / 1.60 | 3 / 10 |
| NY | none | STRONG | 259 | 51.4 [42.9, 59.8] | 48.6 | 0.0 | 47.8 | 57.2 | +3.6 | -5.9 | -1.8 [-4.9, +1.3] | 1.75 / 1.60 | 3 / 9 |
| NY | swing | ALL | 1758 | 40.6 [35.1, 46.0] | 59.4 | 0.1 | 41.8 | 52.2 | -1.2 | -11.6 | -3.3 [-5.0, -1.6] | 2.42 / 1.60 | 5 / 16 |
| NY | swing | S+M | 547 | 37.5 [29.5, 45.4] | 62.5 | 0.0 | 43.2 | 53.6 | -5.7 | -16.1 | -5.0 [-7.3, -2.8] | 2.21 / 1.60 | 5 / 16 |
| NY | swing | STRONG | 59 | 23.7 [9.4, 38.1] | 76.3 | 0.0 | 43.0 | 53.2 | -19.2 | -29.5 | -10.0 [-15.5, -4.5] | 2.20 / 1.60 | 5 / 15 |
| NY | hvn | ALL | 807 | 50.3 [44.8, 55.8] | 49.7 | 0.0 | 50.0 | 60.1 | +0.3 | -9.8 | -1.7 [-4.0, +0.6] | 1.82 / 1.60 | 4 / 16 |
| NY | hvn | S+M | 149 | 55.7 [45.1, 66.3] | 44.3 | 0.0 | 53.2 | 61.9 | +2.5 | -6.2 | -1.5 [-4.6, +1.7] | 1.67 / 1.60 | 4 / 16 |
| NY | hvn | STRONG | 24 | 50.0 [26.0, 74.0] | 50.0 | 0.0 | 59.8 | 67.3 | -9.8 | -17.3 | -3.1 [-9.8, +3.7] | 1.14 / 1.60 | 4 / 14 |
| LONDON | none | ALL | 891 | 50.2 [43.9, 56.4] | 49.8 | 0.0 | 44.4 | 52.7 | +5.8 | -2.6 | +0.4 [-2.1, +2.9] | 2.00 / 1.60 | 11 / 40 |
| LONDON | none | S+M | 427 | 51.8 [44.7, 58.9] | 48.2 | 0.0 | 44.4 | 53.0 | +7.3 | -1.2 | +0.8 [-2.0, +3.6] | 2.00 / 1.60 | 9 / 36 |
| LONDON | none | STRONG | 106 | 45.3 [33.5, 57.1] | 54.7 | 0.0 | 44.4 | 53.5 | +0.8 | -8.2 | -0.6 [-5.6, +4.3] | 2.00 / 1.60 | 9 / 30 |
| LONDON | swing | ALL | 642 | 47.8 [40.6, 55.0] | 52.2 | 0.0 | 43.7 | 52.9 | +4.1 | -5.1 | -2.4 [-5.5, +0.7] | 2.12 / 1.60 | 12 / 45 |
| LONDON | swing | S+M | 158 | 44.9 [35.5, 54.4] | 55.1 | 0.0 | 46.8 | 56.2 | -1.8 | -11.3 | -3.8 [-6.6, -1.1] | 1.78 / 1.60 | 9 / 51 |
| LONDON | swing | STRONG | 22 | 36.4 [14.2, 58.6] | 63.6 | 0.0 | 46.5 | 54.8 | -10.2 | -18.5 | -8.3 [-15.5, -1.1] | 1.78 / 1.60 | 14 / 61 |
| LONDON | hvn | ALL | 132 | 47.7 [34.5, 61.0] | 52.3 | 0.0 | 50.9 | 59.5 | -3.2 | -11.8 | -2.4 [-6.3, +1.6] | 1.43 / 1.60 | 9 / 64 |
| LONDON | hvn | S+M | 31 | 38.7 [15.7, 61.7] | 61.3 | 0.0 | 55.3 | 64.0 | -16.6 | -25.2 | -7.4 [-15.5, +0.7] | 1.29 / 1.60 | 12 / 54 |
| ASIA | none | ALL | 734 | 56.7 [50.5, 62.8] | 43.3 | 0.0 | 56.1 | 66.3 | +0.5 | -9.7 | -2.4 [-4.5, -0.3] | 1.25 / 1.60 | 8 / 27 |
| ASIA | none | S+M | 302 | 55.0 [48.5, 61.4] | 45.0 | 0.0 | 56.1 | 66.0 | -1.2 | -11.0 | -2.6 [-5.4, +0.1] | 1.25 / 1.60 | 7 / 27 |
| ASIA | none | STRONG | 51 | 68.6 [54.8, 82.5] | 31.4 | 0.0 | 56.1 | 64.8 | +12.5 | +3.8 | +3.0 [-3.6, +9.6] | 1.25 / 1.60 | 6 / 29 |
| ASIA | swing | ALL | 846 | 46.9 [39.3, 54.6] | 53.1 | 0.0 | 43.4 | 52.4 | +3.5 | -5.5 | -2.1 [-4.7, +0.6] | 2.09 / 1.60 | 13 / 48 |
| ASIA | swing | S+M | 153 | 48.4 [36.5, 60.2] | 51.6 | 0.0 | 47.2 | 56.1 | +1.2 | -7.7 | -3.0 [-6.7, +0.7] | 1.76 / 1.60 | 10 / 42 |
| ASIA | swing | STRONG | 12 | 83.3 [64.2, 100.0] | 16.7 | 0.0 | 53.1 | 62.5 | +30.2 | +20.8 | +8.9 [+1.7, +16.0] | 1.29 / 1.60 | 5 / 47 |
| ASIA | hvn | ALL | 148 | 51.4 [38.2, 64.5] | 48.6 | 0.0 | 51.3 | 59.6 | +0.1 | -8.3 | -5.4 [-12.4, +1.5] | 1.64 / 1.60 | 11 / 33 |

- **Carried gross edge is the clean direction test** (`docs/swing-vs-fallback-target-read-brief-2026-09-14.md` §3.2). NY shows none at ALL tiers: success rate sits at or below the gross breakeven rate for both target types. LONDON shows +4 to +6 pp at ALL tiers. ASIA shows +0.5 pp for the fallback and +3.5 pp for swing.
- **Tier does not order the gross edge consistently** (output file section 8). For `none`, S+M carries more carried gross edge than WEAK: NY −2.7 against −4.4 pp, LONDON +7.3 against +4.4 pp. For `swing`, S+M carries less: NY −5.7 against +0.8 pp, LONDON −1.8 against +6.0 pp.

### 4.3 Carried to conclusion, cap = end of the signal's session

ALL tiers (output file section 9):

| Session | Target set by | n | Success rate [CI] | UNRESOLVED % | Gross edge pp | Net EV bps [CI] | Resolution p90 min |
|---|---|---|---|---|---|---|---|
| NY | none | 2852 | 44.1 [40.4, 47.7] | 0.0 | -3.7 | -3.9 [-5.2, -2.6] | 11 |
| NY | swing | 1758 | 40.6 [35.1, 46.0] | 0.2 | -1.2 | -3.3 [-5.0, -1.6] | 16 |
| LONDON | none | 891 | 46.0 [39.8, 52.2] | 6.7 | +1.6 | -0.2 [-2.4, +1.9] | 39 |
| LONDON | swing | 642 | 45.6 [39.1, 52.1] | 7.0 | +1.9 | -1.8 [-4.8, +1.1] | 41 |
| ASIA | none | 734 | 55.3 [49.3, 61.3] | 1.9 | -0.8 | -2.6 [-4.6, -0.6] | 27 |
| ASIA | swing | 846 | 44.0 [36.9, 51.1] | 5.9 | +0.6 | -2.6 [-4.8, -0.3] | 45 |

- The session-end cap binds mostly in LONDON, a five-hour session (6.7–7.0 % UNRESOLVED), and on ASIA swing (5.9 %). For `none` and `swing`, net EV per trade moves by at most 0.6 bps against the 24 h cap.

### 4.4 Side split

(output file section 10, n ≥ 30)

- **No side is net-positive with a CI above zero.**
- LONDON `none` LONG is the best large side cell: +1.3 [−2.6, +5.2] main window and +1.4 [−2.5, +5.3] carried, n 413. LONDON `none` SHORT is −1.2 and −0.5.
- In NY every side cell is negative, except `hvn` LONG carried at +0.2 [−4.1, +4.5].

---

## 5. Confound controls

### 5.1 Distance-matched comparison

ALL tiers. Each session's fallback rows sit in one bucket; the rows below are that bucket (output file section 11).

| Session | Target bucket (×ATR) | Target set by | n | Med stop ×ATR | Main window: success [CI] | Gross BE % | Net EV bps [CI] | Carried 24 h: success [CI] | Gross BE % | Net EV bps [CI] |
|---|---|---|---|---|---|---|---|---|---|---|
| NY | 1.5-2.0 | none | 2852 | 1.60 | 42.3 [38.8, 45.8] | 47.7 | -3.8 [-5.0, -2.6] | 44.1 [40.4, 47.7] | 47.7 | -3.9 [-5.2, -2.6] |
| NY | 1.5-2.0 | swing | 318 | 1.60 | 43.4 [36.3, 50.5] | 47.6 | -3.9 [-5.9, -1.9] | 46.5 [38.9, 54.2] | 47.6 | -3.8 [-5.9, -1.7] |
| NY | 1.5-2.0 | hvn | 179 | 1.60 | 49.7 [41.3, 58.2] | 47.6 | -0.7 [-3.0, +1.5] | 52.5 [44.4, 60.6] | 47.6 | -0.2 [-2.8, +2.4] |
| LONDON | 2.0-2.5 | none | 884 | 1.60 | 45.1 [39.3, 51.0] | 44.4 | -0.1 [-2.3, +2.2] | 50.0 [43.8, 56.2] | 44.4 | +0.4 [-2.1, +2.8] |
| LONDON | 2.0-2.5 | swing | 126 | 1.60 | 42.9 [32.5, 53.2] | 40.4 | -1.7 [-5.0, +1.6] | 45.2 [35.2, 55.3] | 40.4 | -1.7 [-5.1, +1.6] |
| LONDON | 2.0-2.5 | hvn | 18 | 1.60 | 27.8 [0.0, 61.9] | 41.0 | +2.8 [-7.0, +12.7] | 44.4 [14.1, 74.8] | 41.0 | -1.0 [-9.5, +7.6] |
| ASIA | 1.0-1.5 | none | 734 | 1.60 | 54.6 [48.5, 60.8] | 56.1 | -2.4 [-4.5, -0.4] | 56.7 [50.5, 62.8] | 56.1 | -2.4 [-4.5, -0.3] |
| ASIA | 1.0-1.5 | swing | 151 | 1.60 | 51.7 [39.9, 63.4] | 55.8 | -3.4 [-7.0, +0.2] | 54.3 [43.7, 64.9] | 55.8 | -3.3 [-6.9, +0.3] |
| ASIA | 1.0-1.5 | hvn | 34 | 1.60 | 44.1 [22.0, 66.2] | 54.6 | -9.3 [-17.9, -0.7] | 44.1 [22.0, 66.2] | 54.6 | -9.3 [-17.9, -0.7] |

- **At equal target distance and equal stop, swing equals the fallback in NY** (−3.9 against −3.8). It is worse in LONDON (by 1.6 main window, 2.1 carried) and in ASIA (by about 1.0). The difference between the populations is the target distance, not the structure.
- **Swing across distance, NY carried** (the widening question, observed only): success rate tracks the gross breakeven rate in every bucket.

| NY swing, carried 24 h, target bucket (×ATR) | 1.0-1.5 | 1.5-2.0 | 2.0-2.5 | 2.5-3.0 | 3.0-3.5 |
|---|---|---|---|---|---|
| n | 194 | 318 | 378 | 416 | 395 |
| Gross edge pp | −7.3 | −1.1 | −1.6 | +3.0 | −0.7 |
| Net EV bps | −5.1 | −3.8 | −3.8 | −1.4 | −2.7 |

- Net EV per trade improves with distance mainly because the fixed 3 bps fee is a smaller share of a wider target. No bucket shows a gross edge that clears its CI. This is an observational hint for Part B, not a result.
- ⚠ **NY `hvn` looks about 3 bps better than the fallback at matched distance**: −0.7 [−3.0, +1.5] against −3.8 [−5.0, −2.6]. **It is not regime-stable** (§5.2 of this doc). The NY `hvn` net EV in the main window runs +1.7 in the pre-collector era, then −1.9, −3.1 and −4.0 (output file section 12). I do not read it as a target-type effect.

### 5.2 Regime split

ALL tiers, `none` and `swing` (output file section 12; `hvn` rows are there too). Era edges:
- **Collector start:** 2026-07-22 16:24:54 UTC.
- **v66 OBV scoring edge:** 2026-08-10 18:36:01 UTC, the first collector process after commit `fd52299` (v66, the OBV `trend_gate` 18 → 23 change, committed 2026-08-10 18:06:57 UTC).
- **ATR step:** 2026-08-20 00:00 UTC, from `docs/d3-asia-burst-watch-read-2026-09-14.md`.

| Session | Era | Target set by | n | Main window: success [CI] | Net EV bps [CI] | Carried 24 h: success [CI] | Gross edge pp | Net EV bps [CI] | Med ATR bps |
|---|---|---|---|---|---|---|---|---|---|
| NY | pre-collector dev runs | none | 558 | 44.8 [38.8, 50.8] | -3.8 [-5.3, -2.3] | 46.8 [40.5, 53.0] | -1.0 | -3.8 [-5.3, -2.4] | 6.9 |
| NY | pre-collector dev runs | swing | 417 | 27.1 [15.8, 38.4] | -4.9 [-8.0, -1.9] | 33.1 [18.4, 47.8] | -8.7 | -5.3 [-9.0, -1.6] | 6.5 |
| NY | collector to v66 | none | 620 | 41.3 [33.9, 48.7] | -3.6 [-5.4, -1.9] | 43.2 [35.9, 50.6] | -4.5 | -4.0 [-5.6, -2.5] | 7.0 |
| NY | collector to v66 | swing | 379 | 37.2 [25.8, 48.6] | -1.5 [-5.1, +2.0] | 42.0 [30.4, 53.5] | +1.8 | -1.7 [-5.5, +2.1] | 6.0 |
| NY | v66 to 2026-08-20 | none | 357 | 45.4 [31.5, 59.2] | -1.0 [-6.1, +4.0] | 50.1 [36.3, 64.0] | +2.4 | -0.6 [-5.8, +4.7] | 6.5 |
| NY | v66 to 2026-08-20 | swing | 169 | 38.5 [31.2, 45.7] | -2.0 [-3.9, -0.1] | 47.9 [42.4, 53.5] | +5.1 | -0.8 [-2.2, +0.5] | 6.2 |
| NY | from 2026-08-20 | none | 1317 | 40.9 [35.9, 45.8] | -4.6 [-6.3, -2.9] | 41.7 [36.6, 46.7] | -6.0 | -4.8 [-6.6, -3.0] | 7.9 |
| NY | from 2026-08-20 | swing | 793 | 37.3 [31.3, 43.3] | -3.8 [-5.9, -1.7] | 42.2 [35.4, 49.1] | +0.1 | -3.6 [-6.1, -1.1] | 7.9 |
| LONDON | pre-collector dev runs | none | 158 | 46.8 [31.3, 62.3] | -0.3 [-6.0, +5.3] | 48.1 [32.3, 63.9] | +3.7 | -0.2 [-5.6, +5.2] | 7.1 |
| LONDON | pre-collector dev runs | swing | 112 | 30.4 [15.5, 45.2] | -5.6 [-10.6, -0.6] | 36.6 [13.9, 59.3] | -6.3 | -5.3 [-11.2, +0.5] | 6.3 |
| LONDON | collector to v66 | none | 287 | 49.1 [36.9, 61.3] | -1.2 [-4.4, +2.0] | 51.6 [37.5, 65.6] | +7.1 | -1.2 [-4.6, +2.3] | 6.1 |
| LONDON | collector to v66 | swing | 160 | 44.4 [32.5, 56.3] | -1.3 [-3.3, +0.8] | 50.6 [39.3, 61.9] | +9.3 | -1.6 [-3.9, +0.8] | 5.5 |
| LONDON | v66 to 2026-08-20 | none | 61 | 27.9 [10.0, 45.7] | -6.0 [-9.9, -2.1] | 37.7 [18.8, 56.6] | -6.7 | -4.5 [-8.6, -0.5] | 5.6 |
| LONDON | v66 to 2026-08-20 | swing | 34 | 38.2 [4.4, 72.1] | -1.8 [-9.3, +5.7] | 55.9 [14.0, 97.8] | +17.0 | -0.4 [-8.9, +8.1] | 5.2 |
| LONDON | from 2026-08-20 | none | 385 | 44.2 [37.4, 51.0] | +1.8 [-2.0, +5.7] | 51.9 [45.1, 58.8] | +7.6 | +2.6 [-1.8, +7.1] | 10.6 |
| LONDON | from 2026-08-20 | swing | 336 | 45.2 [37.1, 53.4] | -1.9 [-5.4, +1.6] | 49.4 [39.9, 58.9] | +4.6 | -2.0 [-7.5, +3.5] | 10.2 |
| ASIA | pre-collector dev runs | none | 70 | 62.9 [51.0, 74.7] | -1.2 [-4.1, +1.7] | 64.3 [51.1, 77.5] | +8.1 | -1.6 [-5.2, +1.9] | 7.9 |
| ASIA | pre-collector dev runs | swing | 48 | 52.1 [31.8, 72.4] | +2.3 [-1.3, +6.0] | 60.4 [36.5, 84.3] | +20.1 | +2.5 [-4.4, +9.4] | 6.6 |
| ASIA | collector to v66 | none | 225 | 49.3 [34.7, 64.0] | -4.0 [-7.9, 0.0] | 51.1 [37.3, 65.0] | -5.0 | -4.1 [-8.0, -0.2] | 8.1 |
| ASIA | collector to v66 | swing | 264 | 36.0 [23.6, 48.4] | -3.8 [-7.5, -0.1] | 40.2 [24.3, 56.0] | -2.3 | -4.4 [-8.8, +0.1] | 7.3 |
| ASIA | v66 to 2026-08-20 | none | 13 | 46.2 [27.6, 64.7] | -5.0 [-8.5, -1.6] | 46.2 [27.6, 64.7] | -10.0 | -5.0 [-8.5, -1.6] | 6.9 |
| ASIA | v66 to 2026-08-20 | swing | 80 | 37.5 [17.0, 58.0] | -2.4 [-6.2, +1.4] | 37.5 [17.0, 58.0] | -2.6 | -3.4 [-7.7, +0.8] | 4.9 |
| ASIA | from 2026-08-20 | none | 426 | 56.3 [49.1, 63.6] | -1.7 [-4.5, +1.1] | 58.7 [51.2, 66.2] | +2.5 | -1.6 [-4.6, +1.4] | 10.4 |
| ASIA | from 2026-08-20 | swing | 454 | 44.3 [35.0, 53.6] | -0.9 [-4.6, +2.7] | 51.1 [40.9, 61.3] | +6.9 | -1.0 [-5.1, +3.2] | 9.7 |

- **No era reverses the verdict.** Swing beats the fallback in some NY and ASIA eras and loses in every LONDON era. The sign is not stable, and every swing-minus-fallback gap sits inside the CIs.
- **LONDON's breakeven comes from the post-2026-08-20 era:** `none` +1.8 main window, +2.6 carried, n 385. Median ATR in bps rose from 5.6–7.1 to 10.6 across the step. A wider distance in bps makes the fixed 3 bps fee a smaller share of the target. Treat this as a fee-dilution effect until a later era confirms it.
- ASIA and LONDON ATR in bps rose about 1.5× at the 2026-08-20 step. NY rose less (6.0–7.0 → 7.9).

### 5.3 Fees: the taker-stop case

Maker entry; taker exit on a stop hit or a marked exit (output file section 13).

| Session | Target set by | Tier | n | Net BE %, maker/maker | Net BE %, taker-stop | Net EV bps taker-stop, main window | Net EV bps taker-stop, carried 24 h |
|---|---|---|---|---|---|---|---|
| NY | none | ALL | 2852 | 58.3 | 61.1 | -4.9 | -5.0 |
| NY | none | S+M | 1198 | 58.0 | 60.7 | -4.9 | -5.0 |
| NY | swing | ALL | 1758 | 52.2 | 55.3 | -4.7 | -4.5 |
| NY | swing | S+M | 547 | 53.6 | 56.6 | -6.2 | -6.3 |
| LONDON | none | ALL | 891 | 52.7 | 55.2 | -1.2 | -0.6 |
| LONDON | none | S+M | 427 | 53.0 | 55.5 | -0.9 | -0.2 |
| LONDON | swing | ALL | 642 | 52.9 | 55.6 | -3.5 | -3.4 |
| LONDON | swing | S+M | 158 | 56.2 | 58.8 | -4.2 | -4.9 |
| ASIA | none | ALL | 734 | 66.3 | 68.5 | -3.3 | -3.3 |
| ASIA | none | S+M | 302 | 66.0 | 68.1 | -3.4 | -3.5 |
| ASIA | swing | ALL | 846 | 52.4 | 55.1 | -2.9 | -3.1 |
| ASIA | swing | S+M | 153 | 56.1 | 58.6 | -4.1 | -4.0 |

- A taker stop costs about 1 bps a trade everywhere. It removes LONDON's breakeven.

### 5.4 Sensitivity: walk start

The next full bar instead of the bar containing the signal (output file section 14).

- The largest change in any cell's net EV per trade is 0.45 bps (LONDON `hvn`). For `none` and `swing` it is at most 0.24 bps. The first-bar choice does not move the verdict.

---

## 6. What I verified, and what I did not

### Verified, and how

| Claim | How |
|---|---|
| The join cause is duplicated eval rows | Counted backfill-provenance and live-provenance copies. Reproduced the brief's 59.5 % exactly with a naive one-to-one join. Reached 99.99 % with the fixed join |
| The eval barriers are the logged placed levels | 7,280 of 7,281 joined rows match to ±0.011 USD |
| Label meaning (`none` = ATR fallback, `swing` = swing target) | Read `SignalEmitter.ComputeStructuralSideLevels` and `AnalysisLogger.NormaliseCapReason`. Data match is 100 % for every label, session and era |
| Candle coverage | 0 missing 1-minute bars across 10 trading weeks; 0 population rows with a missing bar in any walk |
| Funding is negligible | Accrued Deribit hourly `interest_1h`, 0 hours missing. The largest cell effect is 0.010 bps |
| The instrument is re-runnable | A second run from the candle and funding cache reproduced run 1 byte-for-byte, except the fix for zero values printing as `-+0.0` |
| `settings.json` was not touched | The instrument loads a copy (SHA-256 prefix `A059DEC578D8B4C7`, identical to the tracked file). `git status` shows only the new tool folder |
| The trading-week filter follows `session_volume` | The instrument derives Monday 00:00 to Friday 24:00 from the v68 session hours, and refuses to run if a session wraps midnight |

### Not verified

- **Why the eval cache disagrees with venue candles on 7.7 % of rows.** The one-sided pattern fits eval-side bars with understated ranges, for example a forming bar stored before it closed. I did not read the live candle path to confirm this.
- **The Deribit funding mechanism.** I assumed continuous accrual, based on my knowledge of the venue. The repo does not document it. It cannot move any cell by more than 0.01 bps.
- **The v66 era edge.** I used the first collector process start after the v66 commit. I did not check which settings version that process loaded.
- **The pre-collector dev runs** (2026-07-07 to 07-22) came from other processes on settings between v51 and v58. I did not check those settings versions beyond the label geometry, which matches.
- **Cause of the 2026-08-20 ATR step.** Carried over from `docs/d3-asia-burst-watch-read-2026-09-14.md` without checking.
- **Independence.** CIs treat session-days as independent. Carried trades that span two sessions are not modelled as correlated.
- **Multiple comparisons.** About 200 cells, with no correction. The one CI-positive cell (ASIA swing STRONG carried, n 12) is expected noise at that count.
- **Execution.** Fills at the exact target and stop price. No slippage on stops. Same-bar ambiguity counts as a stop (conservative).
- **Split-half holdout.** Not run; it belongs to Part B.

---

## 7. Next

- **Part B is unblocked:** the join is resolved, coverage is complete, and no stop condition fired.
- Part A lowers the prior for Part B's widening grid (`docs/swing-vs-fallback-target-read-brief-2026-09-14.md` §4, item B-2). In NY, success rate already tracks the gross breakeven rate across target distances.
- The one place with gross directional content is LONDON: carried gross edge +4 to +6 pp.

**Model / effort for session 2 (Part B):** Opus, high. Unchanged from the brief; the counterfactual geometry and the holdout are judgment-heavy.

**Re-run:**

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
```

- Candles and funding cache to `backtest_data/swing-fallback-read/` (gitignored).
- The report writes to `backtest_data/swing-fallback-read/swing-fallback-read-output.md`.
