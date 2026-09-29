# Audit — backtest synthesizer, what-if runner, ceiling audit (2026-09-24, UTC)

Hostile read of `tools/BacktestRunner`, `tools/WhatIfRunner` and `tools/CeilingAudit`, checked against the live
path in `UI/MainForm_Analysis.vb` and the scoring engine. Tree at `6e74181`. Proofs and their exact output:
[`proofs/backtest-whatif-ceilingaudit/`](proofs/backtest-whatif-ceilingaudit/README.md).

## ⚠ Coverage — files in the prompt NOT read in full

| File | What was read | Not read |
|---|---|---|
| `tools/BacktestRunner/CoverageReport.vb` (2,052 lines) | Header, `HourClass`/`HourStoreStats`, `AccumulateHourStats`, `AccumulateSplitSpanStats`, `ClassifySpan`, `ClassifyHour`, the sequence-gap fold, `SeqContiguousLabel`, venue-check constants. About 40% | Evidence parsers (`ParseWsHealthEvidence`, `ParseAnalysisLogEvidence`, `ParseDeclaredSchedule`, `ParseVenueWindows`), `BuildUpIntervals`/`ClassifyUptime*`, `ResolveScope`, candle/funding completeness, venue fetch/diff/dump, `BuildResult`, console/markdown builders |
| `tools/WhatIfRunner/WhatIfReport.vb` | `WhatIfEvStat`, `WhatIfGridCell`, `MinCellN` and flag lines | The report body (`Build`) |
| `tools/CeilingAudit/CeilingAuditProgram.vb` | Lines 1–80 (args, settings load) | `EvaluatePopulation`, `BuildInformationalTable`, the rest of the run flow |
| `tools/CeilingAudit/CsvFeatureBuilder.vb` | Lines 92–200 (burst filter), 297–466 (features, populations, labels) | `LoadAndBuild` body before line 135, `ParseCsvRow`, field helpers |
| `tools/CeilingAudit/AuditReport.vb` | Grep hits only | Whole file |
| `tools/CeilingAudit/FeatureMatrix.vb` | Not opened | Whole file |
| `tools/CeilingAudit/L2Logistic.vb` | Not opened | Whole file |
| `tools/WhatIfRunner/overlays/*.json` | Listed only | Contents |

Read in full: `ReplayLoop.vb`, `BacktestRowWriter.vb`, `OverlapValidator.vb` (lines 1–560 in full, 560–754
skimmed), `BacktestProgram.vb` (arg parsing, `replay`/`validate`/`report`/`coverage` verbs), `WhatIfReplay.vb`,
`WhatIfOverlay.vb`, `WhatIfSettings.vb`, `WhatIfProgram.vb`, `CeilingAudit/AuditMetrics.vb` (lines 40–299).
Out-of-scope files read for reference: `UI/MainForm_Analysis.vb` (60–420, 615–640), `AnalysisLogger.LogRun`,
`Core/ScoringEngine_Calculate_Verdict.vb` (1–330), parts of `ScoringEngine_Calculate_Scoring.vb`,
`ScoringEngine_Helpers.vb`, `SignalEmitter.ComputeSideLevels`, `analysis/ForwardWindowJoiner.vb`,
`analysis/FailureRateMatrix.WalkBars`, `HistoricalStore` funding fetch.

⚠ The CLAUDE.md session-start doc reads were skipped to spend the budget on the audited files.

---

## LOGIC TRACE

I couldn't run anything in .NET here because the container has no runtime. So I ported the relevant shipped
functions line-for-line to Python (`CalcATR`, `CalcDMI`, `CalcMTFGate`, `ClassifySpan`'s sequence path,
`DeriveVerdict`, and the Step-4 TRANSITIONAL arithmetic) and ran them on a synthetic 1-second tape. The scripts
are in [`proofs/backtest-whatif-ceilingaudit/`](proofs/backtest-whatif-ceilingaudit/README.md). Treat them as
evidence of how the code behaves, not as harness runs.

**The scenario.** It's NY, so execRes is 1. Price is 100,000 at 14:00 and 98,500 at 15:00, and 1.0% of the drop
lands between 14:05 and 14:15. I traced the close at 14:12:00.

| Field | Replay | Live at close+~2 s | Result |
|---|---|---|---|
| Timestamp | `14:12:00` | `14:12:02–05` (`DateTime.UtcNow`) | Both round down to the same minute for the forward bars. **Match.** |
| CurrentPrice | Last trade in the 2 s after the close | Forming bar's close at poll time | Match, unless API latency spikes during the flush |
| ATR(7), 1m | Built on (N-1) closed bars plus a 2 s stub | Built on the forming 1m bar | **Identical** (P1: largest difference over 59 closes = 0.000000). Stub ATR is 0.872× the closed-bar ATR, close to 6/7. **The low-ATR convention is mirrored correctly.** |
| ROC, RSI, EMAs, BBW, TTM, VWAP, Donchian, OBV, VPFR (1m) | Same series | Same series | Match. The VWAP anchor uses the bar close as "now" and agrees with the freshness gate. |
| 5m DMI and regime | Last bar is the 2 s stub | Last bar is the partly formed 14:10 bar | The inputs differ, but P2 found **0 of 60 regime flips**. DMI(9) smoothing absorbs it. |
| 15m MTF gate | At 14:14 it can't see the −0.96% in the forming 15m bar (P3) | Sees it | The input is badly different, but P3b found **≤2 gate flips in 1,200 closes** across 20 seeds and 3 pre-trends. |
| CVD, TFI, MicroCVD, liquidations, aggressor velocity | Taken from the trade store | Taken from the venue | **Match only if the store is complete.** A flush is peak load, which is when capture holes are most likely. See F5. |
| OFI | Always `BALANCED` | Most likely `SELL DOMINANT` (+1 short, plus the momentum bonus) | **Replay short score is lower** |
| OI | Always `NEUTRAL` | Most likely `NEW SHORTS` (+1 short) | **Replay short score is lower** |
| Spread | Always `NORMAL` | Most likely `WIDE`, which costs the short side a penalty | **Replay skips a penalty** |
| FundingMomentum | `FLAT`: the hourly value hasn't changed since 14:00 | Probably `FALLING` | Step 3b fires in live and not in replay. See F8. |
| Verdict | | | On a trending day the thresholds are strong 14, medium 11, weak 7. A 1–3 point gap is a full tier. |
| Placed levels | `ComputeSideLevels` on the same `r` | Same | Same geometry. Price is below every 5m swing low, so the target falls back to 1.75×ATR ≈ $131, and the stop is at most 1.6×ATR ≈ $120. |

**What-if on this row.** It passes the POC filter (the cap reason is `none`) and the min-move gate ($131 against a
$79 floor). EV is then walked on bars closing at **14:15 through 14:27**, with entry still at the 14:12:02 price.
The 2 minutes at −$100/min, where the $131 short target most likely prints, are never walked. The fee is charged
at 3 bps maker/maker, about $30 or 0.39 ATR. The winning cell is whichever has the highest mean on the selection
half.

The bottom line of the trace is that the synthesizer's candle side is a faithful mirror, including the ATR
convention you asked about. The bias comes in at three places: the muted order-flow signals, the what-if outcome
walk, and the data-integrity chain.

---

## Findings

### F1: The what-if walk skips the first 2 minutes but still enters at T
**SEVERITY:** HIGH
**LOCATION:** `analysis/ForwardWindowJoiner.vb:274` (`For closeMin = 3 To w`), consumed by `tools/WhatIfRunner/WhatIfReplay.vb:247-257`
**DOWNSTREAM IMPACT:** Stop-width and target sweeps rank the wrong way round. Tight stops look profitable because their fastest stop-outs are never seen.
**FAILURE SCENARIO:** P5 is a driftless walk at flush-level volatility, n=4000 per row, with a 1.75×ATR target:

| Stop | EV walked from entry | EV as what-if walks it | Bias |
|---|---|---|---|
| 0.6×ATR | +0.019 | +0.205 | **+0.186 ATR** |
| 1.0×ATR | +0.020 | +0.128 | +0.108 ATR |
| 1.6×ATR | −0.010 | +0.008 | +0.018 ATR |

**ANALYTICAL CRITIQUE:** The spec says the first two bars are skipped because they're "too quick to execute". If
that's the reason, entry has to move to the price at T+2. Keeping entry at T while throwing away the path from T to
T+2 hides both barriers. The tighter barrier gets hidden more often, so the size of the bias depends on the very
knob being swept. This isn't noise. It points one way.

### F2: The six `tier_floor.*` knobs are whitelisted but do nothing
**SEVERITY:** HIGH
**LOCATION:** `WhatIfOverlay.vb:73-78` and `:100-102` (they're even listed in `VerdictKnobs`), `WhatIfSettings.vb:76-81`, and `WhatIfReplay.vb:104-105`, which reads the logged `EffectiveLongScore`
**DOWNSTREAM IMPACT:** Every cell in a tier-floor sweep gets identical EV. `OrderByDescending` is a stable sort, so the "winner" is simply the first value in the sweep, and the report presents it as a result.
**FAILURE SCENARIO:** P7 uses a TRANSITIONAL row with a raw score of 12 and a penalty of 2. The logged effective score is 10, which is MEDIUM. With `high_floor=11` the engine computes 11, which is STRONG. The what-if still reads the logged 10 and still says MEDIUM.
**ANALYTICAL CRITIQUE:** This is the v47-F1 "silent no-op" pattern, which the overlay header says the whitelist
exists to prevent. `TierFloor` is only applied in Step 4, and the replay never re-runs Step 4. The fix is either
to rebuild the effective score from the raw score, the ADX and the regime, or to take these knobs off the
whitelist. Note that at the shipped floors the floor never binds with a penalty of 2 or less, so only floors above
live values would matter. Those are exactly the cells a sweep explores.

### F3: Winner selection has no minimum sample size, compares different populations, and uses iid error bars
**SEVERITY:** HIGH
**LOCATION:** `WhatIfProgram.vb:163-167`, `WhatIfReport.vb:40-43`, and `:177`, where `n<30` is only a display flag
**DOWNSTREAM IMPACT:** Out of up to 3,000 cells, the selected winner is likely to be one that trades very little.
**FAILURE SCENARIO:**
- A threshold cell that keeps only 4 selection-half rows averaging +2 ATR beats a cell with 400 rows averaging +0.3 ATR.
- With one sample, the confidence interval collapses to the mean (`xs.Count < 2`), so the `Divergent` check has no power.
- The standard error assumes independent samples, but rows land every minute with 15–45 minute outcome windows. Effective N is roughly N/10 or smaller.

**ANALYTICAL CRITIQUE:** Verdict knobs change which rows are traded, so each cell's mean EV comes from a different
population. Ranking on mean EV then rewards cells for being selective, not for being right. The overfit counter
records the damage but doesn't correct for it.

### F4: One fee model for every outcome, and every limit entry assumed filled
**SEVERITY:** MEDIUM-HIGH
**LOCATION:** `WhatIfReplay.vb:250-257`; settings `round_trip_style: maker_maker`
**DOWNSTREAM IMPACT:** Loss outcomes are under-costed. The stop on BTC-PERPETUAL is a stop order and fills as a taker: 3.5 bps plus slippage, where maker is 1.5 bps. In a flush, slippage of 5–20 bps is 0.08–0.33 ATR per stop-out. Entry is assumed filled at `row.Price` as a maker. That's the adverse-selection case: a limit order fills when price moves against you and misses when it runs.
**ANALYTICAL CRITIQUE:** This makes F1 worse. Tighter stops produce more stop-outs, so they carry more of the
missing cost. The code comment says "normal SL exits are maker in the trader's flow". That's a claim about order
routing, and I found nothing in the tree that enforces it.

### F5: Coverage calls a hole at the start of an hour Captured, and the replay never checks coverage anyway
**SEVERITY:** MEDIUM-HIGH
**LOCATION:** `CoverageReport.vb:781-795`, `BacktestProgram.vb:424-425`, and `ReplayLoop.vb:311-321`
**DOWNSTREAM IMPACT:** Trade-derived fields are computed over windows that contain holes. `--strict` still exits 0.
**FAILURE SCENARIO:** P6 has an outage from 09:40 to 10:25 with no repair, so 1,800 trades are missing.
- Hour 09 is classed `TrailingEdge`.
- Hour 10 has a 2,701,500 ms gap charged to it, the way the file header intends, and is still classed **`Captured`**. The sequence path returns clean on internal contiguity alone, which throws away `LongestGapMs`.
- `--strict` fails only on `Defect`.
- The range-wide sequence walk does count the 1,800 missing, but it isn't attributed to any hour and doesn't affect the exit code.

**ANALYTICAL CRITIQUE:** The sequence check only looks inside the hour, so it can't see a gap that crosses the hour
boundary. The fix is to carry the previous sequence number across the boundary, the same way `prevTs` is already
carried.

Separately, `ReplayLoop` always takes the last 500 trades regardless of coverage. Over a hole, CVD, TFI and
MicroCVD are scored on stale prints, and nothing gates it. Live has the v67 thin-trade gate; the replay only
checks for `Count = 0`.

### F6: The validator's OI check matches labels the engine never emits
**SEVERITY:** MEDIUM
**LOCATION:** `OverlapValidator.vb:511`, which checks for `LONG_PARTIAL/…_FULL`. The engine emits `NEW LONGS/NEW SHORTS/COVERING/CAPITULATION/NEUTRAL` (`MainForm_Analysis.vb:374-382`).
**DOWNSTREAM IMPACT:** Section 6 of the validation report ("muted-vote delta") only ever tests OFI. Rows where live OI voted are filed as "neutral". The report is supposed to measure how much muting costs, and it undercounts OI's part.
**ANALYTICAL CRITIQUE:** This is the empirical justification for the D2 muting, and half of it is dead. The table
header also prints the wrong labels.

### F7: Muted signals push replay verdicts in one direction
**SEVERITY:** MEDIUM
**LOCATION:** `ReplayLoop.vb:454-472`
**DOWNSTREAM IMPACT:** OFI (plus its momentum bonus), OI and the spread penalty can never fire on replay rows, but MaxScore stays at 19/18/15. Thresholds tuned on replay rows will ship to a live engine where those signals do vote. In the flush trace, replay is 0–3 points light on the short side and missing a penalty.
**ANALYTICAL CRITIQUE:** Muting is documented as ruling D2, but nothing downstream corrects for it or refuses
replay rows for threshold calibration.

### F8: Funding momentum on replay is a square wave locked to the top of the hour
**SEVERITY:** MEDIUM
**LOCATION:** `ReplayLoop.vb:340-451`; settings `momentum_window_minutes: 5`, `threshold: 2e-7`
**FAILURE SCENARIO:** In P4, replay is non-FLAT on 110 of 1,320 closes, and **all 110 fall in minutes :00–:04**. A linearly interpolated live proxy is non-FLAT on 603 of 1,320. That proxy is my assumption, not measured live behaviour.
**ANALYTICAL CRITIQUE:** Step 3b's ±1 then only fires in the first five minutes of each hour.

Two riders:
- The validator's printed caveat says the funding history is "8-hour anchors" (`OverlapValidator.vb:604`). `HistoricalStore` says it is hourly.
- **Not verified:** whether a Deribit `get_funding_rate_history` timestamp labels the start or the end of the period. If it's the start, `FundingAtOrBefore` looks up to an hour ahead.

### F9: Nothing downstream filters out synthetic `BACKTEST-` rows
**SEVERITY:** MEDIUM
**LOCATION:** There's no `InstanceId` check anywhere in `analysis/`, `WhatIfRunner`, or `CeilingAudit`. The CeilingAudit burst filter only drops instances with a median gap under 45 s, and replay rows are 60 s apart.
**DOWNSTREAM IMPACT:** A pooled book that picks up a replay CSV mixes muted rows into the CeilingAudit features, where `OFISignal=BALANCED` then partly tags a row as synthetic, and into what-if EV.
**ANALYTICAL CRITIQUE:** The `BACKTEST-` prefix was meant as the guard (spec §2), but only a naming convention
stands behind it. No consumer checks it.

### F10: The validator's tolerances and join are loose where it matters
**SEVERITY:** MEDIUM
**LOCATION:** `OverlapValidator.vb:42`, `:436`, `:271`, `:607`
**DOWNSTREAM IMPACT:** The validator overstates agreement in several ways:
- A relative tolerance of 1e-4 on price-level columns is ±$10 at $100k. That's about 0.15 ATR on `PlacedTarget/Stop` and 2.5× the 4-tick stop floor, so placement drift counts as a match.
- `ByBucketLatest` keeps the **latest** live row in each bucket, not the on-close fire. At execRes=3 that row can be about 3 minutes later.
- Rows with the wrong column count are dropped without being counted.
- The report always prints a hardcoded "validation window 2026-07-23 → 07-30" caveat, whatever `--from`/`--to` were.

### F11: The ReplayLoop comment's "byte-for-byte" claim is false for 5m and 15m, but it barely matters
**SEVERITY:** LOW (I suspected this was the headline and the simulation said otherwise)
**LOCATION:** `ReplayLoop.vb:286`, `:333-335`
**DOWNSTREAM IMPACT:** When a close isn't on the 5m or 15m grid, live's last bar holds up to 4 or 14 minutes of data, and the replay's holds 2 seconds. P2 found 0 of 60 regime flips and P3b found ≤2 of 1,200 MTF flips. The fix is cheap (build the partial bar from 1m bars plus the stub), but it isn't urgent. The comment should be corrected, because the next seat will trust it.

### F12: Smaller items
**SEVERITY:** LOW–MED
- **CeilingAudit baseline is compressed toward 0.5.** The baseline score is integer over MaxScore and only covers directional rows, so it takes a handful of values with many ties, which pulls its AUC toward 0.5. That inflates ΔAUC.
- **CeilingAudit keeps ATR as a feature.** Barriers are in ATR units while the floor is a percent of price. That's the same geometry-difficulty leak that got `TargetCapReason` demoted (`CsvFeatureBuilder.vb`).
- **POC exclusion leaks.** The logged `TargetCapReason` for a NO TRADE row takes the long side first. A row whose short side was POC-placed survives the filter, and under a cell that flips it SHORT it gets re-placed without POC (`WhatIfProgram.vb:102`).
- **Mixed settings eras.** The what-if baseline is the current `settings.json` applied to effective scores logged under older versions, and there's no version column to filter on.
- **Latent: `vDummy` in `WhatIfReplay.RunCell`.** If `structural_levels.enabled` is ever set false, the legacy path reads `v.AdjustedLongTarget = 0` and all caps silently disappear.
- **Coverage windows are matched on the hour's first millisecond.** Venue and declared windows (`CoverageReport.vb:889`, `:904`) are matched by hour start. A 09:50–10:10 window drops all of hour 10 and never covers hour 09.
- **`validate` ignores `--closed-bars`.**

---

**What I didn't read in full:** `HistoricalStore.vb`; roughly 60% of `CoverageReport.vb` (I read the
classification, sequence and exit paths and skimmed the rest); `WhatIfReport.vb` beyond the stats class and
flags; `FeatureMatrix`, `L2Logistic` and `AuditReport`. I also skipped the CLAUDE.md session-start doc reads to
spend the budget on the audited files. The coverage table at the top of this file is the precise list.

If you only fix three things, do F1, F2 and F3. Together they decide which cell the what-if hands you, and all
three are wrong in the same direction: they favour tighter and more selective settings.
