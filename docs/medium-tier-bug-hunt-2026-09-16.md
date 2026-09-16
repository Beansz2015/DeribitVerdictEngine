# MEDIUM-tier bug hunt, session 1 — STOPPED: the POC-tier gate does the opposite of its documented intent

**Written:** 2026-09-16 (UTC; `date -u` read 09:18) by the session 1 seat. **Brief:** [`docs/medium-tier-diagnosis-brief-2026-09-16.md`](medium-tier-diagnosis-brief-2026-09-16.md), session 1 only. **Model / effort:** Opus, high. **Review packet:** [`docs/medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md).

**Vocabulary:** [`docs/DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §5a. **Full instrument output:** [`docs/medium-tier-bug-hunt-2026-09-16-poc-gate-output.md`](medium-tier-bug-hunt-2026-09-16-poc-gate-output.md). Section references of the form "output §N" point into that file.

**Legend. IDs used in this doc:**

| ID | Source and kind | Meaning |
|---|---|---|
| `A80a` | Harness fixture, `verify/ordercheck/Program.vb` (added by this seat) | Pins the label geometry of `CalcVPFRLite`: which side of price the POC sits on for each NEAR_HVN label |
| `A80b` | Harness fixture, `verify/ordercheck/Program.vb` (added by this seat) | Known-defect repro: the POC-tier gate should place the POC when the POC is the only structure on the target side. Runs only with `ORDERCHECK_KNOWN_DEFECTS=1` |
| `A26b` | Harness fixture, `verify/ordercheck/Program.vb:3330` (v51 ladder walk) | Pre-existing fixture that hand-sets a VPFR label |
| `P10` | Parked observation, `docs/DeribitIndicatorProject.md` §16.6 | "POC tier of the target ladder rarely places" |
| `508f33d`, `3afb674`, `721882e` | Git commits, 2026-04-09 UTC | `508f33d` added `CalcVPFRLite` and its labels. `3afb674` added the VPFR score vote. `721882e` added the VPFR target cap, the ancestor of today's POC-tier gate |

---

## 1. Verdict

- **Bug found. Session 1 stopped** under the stop rule in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §0: a gate whose code does the opposite of its documented intent.
- **What:** the POC-tier gate of the placed-target ladder reads the VPFR labels `NEAR_HVN_SUPPORT` and `NEAR_HVN_RESIST` with inverted geometry. Its NEAR_HVN half opens only where the POC sits on the wrong side of price, so it can never place.
- **File and line:** `Core/SignalEmitter.vb:330-332` (the `pocGated` gate, live since v51). Legacy twin with the same label pairs: `Core/ScoringEngine_Calculate_Verdict.vb:223-224` (runs only when `scoring.structural_levels.enabled` is false; no row in the book).
- **Failing fixture:** `A80b` fails for the long side and the short side (pasted in section 2.5 of this doc).
- **Share of rows affected** (verified rows only; the method is section 4 of this doc):

| Measure | Rows | Share |
|---|---|---|
| POC-tier placements under the shipped gate | 0 of 38,665 verified trading-week rows | 0 % |
| Population rows (trading-week directional) that the gate's documented intent turns into NO TRADE | 143 of 8,508 | 1.68 % |
| ... STRONG | 5 of 496 | 1.01 % |
| ... MEDIUM | 34 of 2,351 | 1.45 % |
| ... WEAK | 104 of 5,661 | 1.84 % |
| ... by session: NY / LONDON / ASIA | 114 of 5,183 / 17 of 1,629 / 12 of 1,696 | 2.20 % / 1.04 % / 0.71 % |
| NO TRADE rows vetoed by Step 5c (`BELOW_MIN_MOVE`) that the documented intent would make directional | 0 of 8,233 | 0 % |
| Trading-week rows, all verdicts, whose long or short placed target changes | 1,288 of 38,665 | 3.33 % |

- **No score changes.** The gate sits in the placed-level arbitration (Step 5b). It reaches a verdict only through the Step 5c min-move gate. It also sets the placed target that reaches the CSV `Placed*` columns and the bridge payload levels.
- ⚠ **The brief's stop rule describes the finding it expects as "a live scoring defect affecting verdicts now".** This one changes no score. I stopped because the rule's trigger is "a vote or gate", and this gate matches it.
- **My read (hypothesis): this defect does not explain the MEDIUM-tier gap.** MEDIUM is not over-exposed (1.45 %, against WEAK 1.84 %). Scores, the tier walk and the re-score reconstruction do not read this gate. The only coupling to the remaining session 1 work is Step 5c on 1.68 % of population rows.

---

## 2. The defect

### 2.1 What the label producer means

| Source | Text |
|---|---|
| `Core/Indicators_Structure.vb:171-176` | Near the POC and `currentPrice < poc` → `NEAR_HVN_SUPPORT`, else `NEAR_HVN_RESIST` |
| Commit `508f33d`, `CalcVPFRLite` header | "NEAR_HVN_SUPPORT -- price within hvnProximityPct below POC (long-friendly)"; "NEAR_HVN_RESIST -- price within hvnProximityPct above POC (short-friendly)" |
| `docs/UserManual.md` lines 950-951 | `HVNNearPoc AND CurrentPrice < POC` → `NEAR_HVN_SUPPORT`; `CurrentPrice ≥ POC` → `NEAR_HVN_RESIST` |
| Fixture `A80a` | PASS on the shipped code (section 2.5 of this doc) |

- **`NEAR_HVN_SUPPORT` means the POC sits ABOVE price. `NEAR_HVN_RESIST` means the POC sits AT or BELOW price.**

### 2.2 What the gate's spec says

| Source | Text |
|---|---|
| Commit `721882e`, cap comment | "Long : VPFRSignal = NEAR_HVN_RESIST or IN_LVN_BEAR (wall above price)"; "Short : VPFRSignal = NEAR_HVN_SUPPORT or IN_LVN_BULL (floor below price)" |
| `Core/SignalEmitter.vb:329` | "POC tier keeps the legacy HVN-proximity gate (VPFRSignal flags the side)." |
| `docs/vpfr-lite-v2-proposal.md` line 290, worked example | "POC < currentPrice (78285), so hvnAbove = false" |
| `docs/UserManual.md` line 961 | Long target capped at the POC "when NEAR_HVN_RESIST or IN_LVN_BEAR AND POC > CurrentPrice" |

### 2.3 The contradiction

- The long gate opens on `NEAR_HVN_RESIST`. The producer emits that label only when the POC is at or below price. There the POC is behind a long: `pocDist ≤ 0` at `Core/SignalEmitter.vb:348-349`, so the tier cannot place.
- The long gate stays shut on `NEAR_HVN_SUPPORT`. That is the only NEAR_HVN label with the POC above price: the "wall above price" that commit `721882e` names.
- The short gate mirrors this.
- The `IN_LVN_*` half of the gate agrees with its spec: `IN_LVN_BEAR` means price at or below the POC.
- **In the data:** the POC tier placed **0** targets in 38,665 verified rows under the shipped gate (output §6). The logged `TargetCapReason` is `poc` on **0** of 8,810 population rows.
- **Reasoning, not measured per row:** the `IN_LVN_*` half never won either, because the nearest-HVN tier places first. The POC bucket is itself an HVN, so the nearest HVN on the target side is never farther than the POC unless price sits inside the POC bucket.
- **Noticed before, never diagnosed:** `P10` and `docs/architecture.md` Display Behaviour Clarifications ("POC tier 3 of the target cap never fires in practice | By design + geometry").
- **The score vote is a separate question.** `Core/ScoringEngine_Calculate_Scoring.vb:457-458` agrees with its own spec (section 5 of this doc).

### 2.4 Why the harness did not catch it

- `A26b` hand-sets `VPFRSignal = "NEAR_HVN_RESIST"` with the POC at 62050, above the 62000 entry (`verify/ordercheck/Program.vb:3346`). `CalcVPFRLite` never emits that state. The fixture passes on a label geometry the producer contradicts.
- `A80a` and `A80b` build `IndicatorResults` from the real `CalcVPFRLite` output instead.

### 2.5 The failing fixture, and proof it can pass

With the known-defect fixture enabled, on the shipped code (`ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release`):

```
PASS  A80a price inside the POC bucket BELOW the POC → NEAR_HVN_SUPPORT, and the POC sits ABOVE price
PASS  A80a price inside the POC bucket ABOVE the POC → NEAR_HVN_RESIST, and the POC sits BELOW price
FAIL  A80b long: the POC is the only structure on the target side (100020.0 vs entry 100005.0, label NEAR_HVN_SUPPORT) → the HVN-gated POC tier places at it, as 721882e specifies ('wall above price') — KNOWN DEFECT: got target 100040.0 (FALLBACK_ATR). Core/SignalEmitter.vb:331 opens the long POC tier only on NEAR_HVN_RESIST or IN_LVN_BEAR, which CalcVPFRLite emits when the POC sits on the OTHER side of price
FAIL  A80b short: the POC is the only structure on the target side (100020.0 vs entry 100035.0, label NEAR_HVN_RESIST) → the HVN-gated POC tier places at it, as 721882e specifies ('floor below price') — KNOWN DEFECT: got target 100000.0 (FALLBACK_ATR). Core/SignalEmitter.vb:332 opens the short POC tier only on NEAR_HVN_SUPPORT or IN_LVN_BULL, which CalcVPFRLite emits when the POC sits on the OTHER side of price
2 FAILURE(S)
```

Default run (what `tools/checks/verify-gate.ps1` runs), same build:

```
PASS  A80a price inside the POC bucket BELOW the POC → NEAR_HVN_SUPPORT, and the POC sits ABOVE price
PASS  A80a price inside the POC bucket ABOVE the POC → NEAR_HVN_RESIST, and the POC sits BELOW price
SKIP  A80b known-defect repro (the POC-tier gate reads the NEAR_HVN labels with inverted geometry) — set ORDERCHECK_KNOWN_DEFECTS=1 to run; docs/medium-tier-bug-hunt-2026-09-16.md
ALL PASS
```

Reverse mutation: a scratch copy of the harness project, with `Core/SignalEmitter.vb` lines 331-332 swapped (`NEAR_HVN_SUPPORT` ↔ `NEAR_HVN_RESIST`) in a scratch copy of that file only:

```
FAIL  A26b swing+HVN too loose → HVN-gated POC places (62050) — target=62070 reason=''
PASS  A80a price inside the POC bucket BELOW the POC → NEAR_HVN_SUPPORT, and the POC sits ABOVE price
PASS  A80a price inside the POC bucket ABOVE the POC → NEAR_HVN_RESIST, and the POC sits BELOW price
PASS  A80b long: the POC is the only structure on the target side (100020.0 vs entry 100005.0, label NEAR_HVN_SUPPORT) → the HVN-gated POC tier places at it, as 721882e specifies ('wall above price')
PASS  A80b short: the POC is the only structure on the target side (100020.0 vs entry 100035.0, label NEAR_HVN_RESIST) → the HVN-gated POC tier places at it, as 721882e specifies ('floor below price')
1 FAILURE(S)
```

- `A80b` passes once the gate matches its spec, so it is not vacuous.
- `A26b` fails under the same change. Any fix must rewrite `A26b`, because it encodes the shipped label assumption.
- The tracked `Core/SignalEmitter.vb` was never edited: git blob `6dc5d716741a69db59d02cc76dd9fa4c6072b57a` equals `HEAD`, and MD5 `fed34ff7f8f2b9a6b96ecf61c4040df2` is the same before and after the mutation run.

---

## 3. Why the documented intent adds NO TRADE rows instead of better targets

- The intended gate can place the POC only when price sits inside the POC bucket, with no swing target and no other HVN on that side.
- The POC centre is then less than half a bucket from price. That target falls inside the Step 5c floor (0.08 % of price: 3 bps round-trip fee plus the 0.05 % minimum net move).
- **Every one of the 143 population rows whose target moves becomes NO TRADE** (output §7.1). **No `BELOW_MIN_MOVE` row becomes directional** (output §7.2).
- So correcting the gate to its spec would act as a new veto: "price inside the POC bucket with no nearer structure → no trade". Whether that veto helps is an outcome question this doc does not measure.

---

## 4. Method for the share of rows

- **The problem:** `analysis_log.csv` logs neither `VPFRPoc` nor `VPFRSignal`. This is trap (1) in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §0. The What-If adapter defaults the label to `NEUTRAL`, which would hide this defect completely.
- **Recompute:** `tools/ops/SwingFallbackRead` `--mode pocgate` (`tools/ops/SwingFallbackRead/PocGateDefect.vb`) runs the shipped `CalcVPFRLite` with the tracked VPFR keys on exchange candles with volume at each row's time.
- **Verify:** a row counts only when the recomputed profile reproduces all four logged VPFR fields (`VPFRVAH`, `VPFRVAL`, `VPFRNearestHvnAbove`, `VPFRNearestHvnBelow`) within 0.006 USD.

| Check (output §3, §4) | Result |
|---|---|
| Trading-week rows recomputed (swing read funnel, NO TRADE rows kept) | 39,594 |
| Verified | 38,665 (97.7 %) |
| Population rows verified | 8,508 of 8,810 (96.6 %) |
| Candle window that verifies first | 249 closed bars + a zero-volume forming bar at the logged price: 38,194 rows |
| Shipped `ComputeSideLevels` reproduces all four logged `Placed*` values on verified rows | 38,662 of 38,665 (99.99 %) |
| Counterfactual moved a stop (must be 0) | 0 |
| Verified directional rows whose shipped target already fails Step 5c (must be 0) | 0 |
| Verified `BELOW_MIN_MOVE` rows whose shipped target passes Step 5c (must be 0) | 1 (a logged-precision edge; see section 7 of this doc) |
| Verified share by UTC month: July / August / September | 96.8 % / 98.1 % / 97.9 % |

- **Counterfactual:** the shipped `SignalEmitter.ComputeSideLevels` runs a second time with `NEAR_HVN_SUPPORT` and `NEAR_HVN_RESIST` swapped in `r.VPFRSignal`. `pocGated` is the only reader of `VPFRSignal` in that file (`git grep` shows lines 329, 331, 332 only), so the swap is exactly the gate commit `721882e` describes. Step 5c is re-applied with the composed floor from `scoring.trade_costs`.
- **Candles:** the backtest store (`backtest_data/candles_{1m,3m}_2026-07.csv`, read-only) to 2026-07-30 19:36 UTC. After that: Deribit `public/get_tradingview_chart_data`, fetched 2026-09-16 and cached in `backtest_data/swing-fallback-read/pocgate_candles_{1m,3m}.csv` (gitignored). No bar is missing in the needed span (output §2).
- **Settings:** the tracked `settings.json` (v68). No VPFR, `structural_levels`, `trade_costs` or verdict-percentage value read here changed between v51 and v68. v56 added modes at 0 and buffers at 0; v62 composed the floor to the retired 0.0008; v63 added a flag at false.
- **Determinism:** a second run from the cache matches the first on every statistic. Only the run timestamp and the candle-source counts differ.

---

## 5. What session 1 established before the stop

| Item | Result | How checked |
|---|---|---|
| MicroCVD decel penalty, `Core/ScoringEngine_Calculate_Scoring.vb:380-381` (flagged in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §1) | **AGREES** with documented intent: `BULL_DECEL` penalises SHORT | `CalcMicroCVD` (`Core/Indicators_OrderFlow.vb:451-482`): `BULL_DECEL` = net window flow positive and decelerating. `docs/UserManual.md` lines 1389-1390: "net flow is still bullish ... shorts are clearly NOT in control, so penalise any short-side score". `docs/dynamic-microcvd-accel-proposal.md` line 201: "opposing-side penalty" |
| VPFR score vote, `Core/ScoringEngine_Calculate_Scoring.vb:457-458` | **AGREES with its own spec.** Commit `3afb674`: "NEAR_HVN_SUPPORT -> +1 LONG (price near POC from below = HVN bounce zone)". ⚠ But `docs/UserManual.md` line 967 describes `NEAR_HVN_RESIST` as "price is sitting just below a high-volume node acting as resistance", the opposite geometry. Queued as a decision, not a stop | Commit text and code read. Measured context (output §5, §8): NEAR_HVN labels on 69.8 % of verified rows; on population rows that carry one, the vote points against the verdict side on 2,844 of 4,440 (64.1 %), descriptive only |
| Tier floor, `Core/ScoringEngine_Helpers.vb` `TierFloor` | **Inert at the shipped penalties.** With penalties 2 and 1, `raw − penalty` beats every floor (12→9, 9→6, 6→3) for raw ≥ 3. The floor only clamps at zero for raw ≤ 2. The TRANSITIONAL demotion is pure penalty | Arithmetic on `settings.json` v68 `scoring.tier_floor` and `regime_gates` |
| Scoring eras inside the book | `docs/medium-tier-diagnosis-brief-2026-09-16.md` §1 lists v65 and v66 only. v52, v58 and v60 also change scoring inside the population span. v53 and v66 change indicator inputs that the CSV logs | Scripted diff of the scoring-relevant `settings.json` keys across every commit since 2026-07-01 (spec-back section 3) |

---

## 6. Not done (stopped under `docs/medium-tier-diagnosis-brief-2026-09-16.md` §0)

- `docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.1, the re-score reconstruction: not started.
- `docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.2, the mirror-symmetry fixtures: not started. ⚠ This defect is mirror-symmetric (both sides inverted), so a mirror fixture would pass on it. See spec-back section 3.
- `docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.3, the vote-site audit table: not built. Only the two sites in section 5 of this doc were checked.
- `docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.4, the tier-demotion census: not started.

---

## 7. What I verified, and what I did not

### Verified, and how

| Claim | How |
|---|---|
| The label producer's geometry | `A80a` PASS on the shipped code; commit `508f33d` text (`git show`); `docs/UserManual.md` lines 950-951 |
| The gate's documented intent | Commit `721882e` text (`git show`); `Core/SignalEmitter.vb:329`; `docs/vpfr-lite-v2-proposal.md` line 290 |
| The gate does the opposite on the shipped code | `A80b` FAIL for both sides with the environment variable set |
| `A80b` can pass | Reverse mutation in a scratch harness copy: `A80b` PASS for both sides |
| The engine files were not changed | `git diff --stat HEAD -- Core UI settings.json "*.vbproj" AnalysisLogger.vb DynamicNorms.vb` is empty; `Core/SignalEmitter.vb` blob and MD5 unchanged |
| The default gate stays green | Default harness run: `A80a` PASS ×2, `A80b` SKIP, ALL PASS, exit 0 |
| The share-of-rows numbers | `--mode pocgate` output, run twice; 97.7 % profile verification; 99.99 % placed-level reproduction |
| The swing read's default mode is unchanged by the new branch | Default-mode re-run matches `docs/swing-vs-fallback-target-read-2026-09-15-output.md` line for line, apart from the run timestamp |
| The one `BELOW_MIN_MOVE` row that passes Step 5c on recompute is a precision edge | A one-off Python scan of the logged values finds 4 `BELOW_MIN_MOVE` rows within 0.02 USD under the floor (for example target distance 63.05 against floor 63.0612) |

### Not verified

- **Unverified rows are not classified:** 929 trading-week rows, 302 of them population rows. Every share in this doc is over verified rows only.
- **The 3 verified rows where the shipped path does not reproduce the logged `Placed*` values:** not investigated.
- **Net EV per trade of the 143 rows the documented intent would veto:** not measured. It decides whether the intended gate helps.
- **The legacy path** (`Core/ScoringEngine_Calculate_Verdict.vb:223-224`): read only, no fixture. It affects no row in the book, because every row is after the v51 edge.
- **Other readers of the labels:** the card colours in `UI/MainForm_Render_Cards.vb` (lines 1838-1839, 3440-3442) follow the vote's meaning. Not audited further.
- **Exchange candle revisions:** the fetched August and September bars were not cross-checked against the store. The verification filter is the only guard.
- **`--mode stability`** of the swing read was not re-run after the edit. The default mode was.
- **Carried over without checking:** the swing read's population funnel and trading-week rule (`tools/ops/SwingFallbackRead/SwingFallbackRead.vb`), reused as-is.

---

## 8. Re-run

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode pocgate --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

- The instrument reads the gitignored inputs and writes `backtest_data/swing-fallback-read/poc-gate-defect-output.md`. A first run without the candle cache fetches about 83,000 bars from Deribit.

---

## R. Resumed session — STOPPED AGAIN: the live liquidation flag never reaches scoring

**Resumed:** 2026-09-16 (UTC) on the orchestrator's D-3 ruling (resume session 1), with three carry-ins: the corrected era list, a label-consumer audit in place of the vote-site audit, and the tier-floor statement for the census. Decisions D-1 and D-2 of the stop record stay with the trader; this seat did not act on them. **Full instrument output:** [`docs/medium-tier-bug-hunt-2026-09-16-liquidation-output.md`](medium-tier-bug-hunt-2026-09-16-liquidation-output.md) ("liquidation output §N" below).

**Legend. IDs added in this section:**

| ID | Source and kind | Meaning |
|---|---|---|
| `L-1` | This section, stop-class finding | The WebSocket trade stream never delivers the `liquidation` flag, so the liquidation penalty never fires |
| `L-2` | This section, stop-class finding | `CalcLiquidations` books a maker-side (`M`) liquidation on the taker's side |
| `L-3` | This section, documentation finding | `large_liq_size` is written as BTC in the manual but compared with USD sums |
| `DOC-1`, `DOC-3` | This section, documentation findings | `docs/UserManual.md` line 1200 (spread side rule) and line 967 (VPFR geometry, already the stop record's D-2) contradict their specs |
| `ALERT-1` | This section, display/alert finding | The liquidation cascade alarm inherits `L-1` and `L-2` |
| `DESIGN-1` | This section, design-claim finding | `docs/websocket-migration-proposal.md` line 43 says the trade fields, liquidation included, "map 1:1" |
| `DATA-1` | This section, data finding | The trade store holds two copies of 93 liquidation trades that differ only in the flag |
| `A81a` / `A81b` | Harness fixtures, `verify/ordercheck/Program.vb` (added by this seat) | `A81a` pins the taker-side (`T`) mapping. `A81b` is the known-defect repro for the maker-side (`M`) flag; it runs only with `ORDERCHECK_KNOWN_DEFECTS=1` |

### R.1 Verdict

- **Stopped again**, under the orchestrator's rule: a label consumer that feeds scores disagrees with its producer. Two findings qualify: `L-1` and `L-2`.
- **`L-1`, the material one:** on the live WebSocket path every liquidation trade reaches `CalcLiquidations` with flag `none`. `LiqSignal` is `NONE` on **51,107 of 51,107** merged rows since the v51 edge and **8,810 of 8,810** population rows. The liquidation penalty, a PREFERRED signal in `docs/trader-profile.md` ("Cascade detection. Penalty-only signal"), has never applied in the book.
- **`L-2`, latent:** `CalcLiquidations` books a maker-side liquidation on the wrong side. 1 such trade in the store span; **0** rows affected.
- **Not started (stopped):** the re-score reconstruction, the mirror-symmetry fixtures and the demotion census (`docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.1, §2.2, §2.4).
- **Done before the stop:** the whole label-consumer audit (section R.4 of this doc), the era confirmation (R.6) and the tier-floor statement (R.7).
- **My read (hypothesis): neither finding explains the MEDIUM gap.** The liquidation vote is absent from every tier alike.

### R.2 `L-1` — the live stream drops the liquidation flag

| Evidence (liquidation output §1-§3, §5) | Result |
|---|---|
| Liquidation-flagged rows in the collector's trade store (2026-07-31 21:49 to 2026-09-13 15:36 UTC) | 97 (T buy 14, T sell 82, M buy 1) |
| ... with an identical copy (same timestamp, price, amount, direction) flagged `none` | 93 |
| ... where the `none` copy was appended EARLIER in the same file, i.e. the streamed copy | 93 |
| ... where both copies carry a trade id, and the ids are equal | 91 of 91 |
| Collector log rows in the store span | 36,327 |
| ... whose 500-trade window holds at least one liquidation trade (copies collapsed) | **32** (19 directional verdicts, 13 NO TRADE) |
| ... that logged `LiqSignal` other than `NONE`, or a non-zero liquidation size | **0** |
| Merged rows since the v51 edge with `LiqSignal` other than `NONE` | 0 of 51,107 |

- **Mechanism, verified in code:** `DeribitWsFeed.vb:488` reads `liquidation` from each streamed trade and writes `none` when the key is absent. `MarketState.AppendTrade`, `MarketState.GetTrades` and `WsMarketDataSource.GetRecentTradesAsync` pass that same record to `CalcLiquidations` unchanged. So the streamed copies carrying `none` are what scoring saw.
- **Where the real flags came from:** a later REST copy of the same trade (same trade id), appended to the store after the streamed one. Deribit's REST trade endpoint documents the `liquidation` field.
- **Not verified:** whether Deribit's `trades.BTC-PERPETUAL.100ms` channel never sends the field or sends it under another name. The channel page could not be fetched. The data shows only that the stream parse never kept it.
- **Share of rows:** 32 of 36,327 collector rows in the store span (0.09 %) had a liquidation to score. Before 2026-07-31 21:49 UTC there is no store to measure. `NONE` on every earlier row is consistent with the same defect, but that is inferred, not measured.
- **What each row lost is not measured:** the penalty side depends on which side was liquidated, and whether a verdict would change needs the re-score, which is not started.
- **No failing harness fixture is possible:** `DeribitWsFeed` needs a live socket and is not linked into `verify/ordercheck`. The failing check is the data instrument `--mode liqflag` (section R.8 of this doc).

### R.3 `L-2` — a maker-side liquidation is booked on the taker's side

- **Producer contract, verified:** Deribit `public/get_last_trades_by_instrument` says `direction` is the "Trade direction of the taker", and `liquidation` is "M" when the maker side of the trade was under liquidation, "T" when the taker side was, "MT" when both.
- **Consumer:** `Core/Indicators_OrderFlow.vb:268-272` books every flagged trade by the taker's direction. That is right for `T` and wrong for `M`: a taker buy against a liquidated maker is a long being sold out, but it lands on `liqShortSize`.
- **Failing fixture:** `A81b` fails for both sides. A scratch copy that books by the liquidated side (`(t.Direction = "buy") <> (t.Liquidation = "M")` on line 268) makes `A81b` pass. The tracked file was not edited: blob `1f8dd1060c686b3d1db3dda4b1485b0118f90f51` equals `HEAD`.
- **Share of rows:** 1 maker-side liquidation in the store span (2026-09-11 12:30:07 UTC, taker buy, 20 USD); **0** collector rows had it in their window (liquidation output §4). `MT` is not handled either; 0 occurrences.

### R.4 Label-consumer audit

- **Scope:** every read of a direction-bearing label or signal string in `Core/ScoringEngine_Calculate_Scoring.vb`, `Core/ScoringEngine_Calculate_Verdict.vb`, `Core/ScoringEngine_Helpers.vb`, `Core/SignalEmitter.vb`, `ExitGuardEvaluator.vb`, `Core/AlertsTracker.vb` and `LiveMicrostructureEvaluator.vb`, plus the two places that turn the exchange's trade fields into labels.
- **Method:** each consumer's reading is checked against the **producer function's emission geometry**, read in the producer's code, and then against the consumer's own spec where one exists.
- **Class:** **S** = feeds scores, tiers, placed levels or the bridge payload (stop-and-report). **D** = display or alert only.

| # | Consumer (file:line) | Label and what the consumer does with it | Producer (file:line): what the label means | Class | Result |
|---|---|---|---|---|---|
| 1 | `Core/ScoringEngine_Calculate_Scoring.vb:170-177` | `ROCSlope` RISING with ROC > 0 → long vote; partial above the magnitude | `UI/MainForm_Analysis.vb:235-239`: RISING = bar-to-bar ROC delta above the slope threshold | S | AGREES (`docs/UserManual.md` 566-569) |
| 2 | `...Scoring.vb:193-200` | `RSIDivergence` BEARISH with RSI > 65 → −1 long; BULLISH with RSI < 35 → −1 short | `Core/Indicators_Momentum.vb:223-270`: BEARISH = price at or above an overbought swing high with lower RSI | S | AGREES (manual 597) |
| 3 | `...Scoring.vb:254-277` | `SqueezeStatus` ACTIVE → −2 both sides; else `TTMSignal` BULL_BUILDING → +1 long, BEAR_BUILDING → +1 short | `Core/Indicators_Volatility.vb:137-143` (ACTIVE = BBW at or below the squeeze percentile); `:195-205` (BULL_BUILDING = histogram > 0 and rising) | S | AGREES (manual 768) |
| 4 | `...Scoring.vb:280-281` | `EMAAlignment` BULL → long vote | `UI/MainForm_Analysis.vb:303-309`: 9 > 21 > 50 | S | AGREES (manual 813) |
| 5 | `...Scoring.vb:286-289` | `OISignal` NEW LONGS / NEW SHORTS full; COVERING long partial; CAPITULATION short partial | `UI/MainForm_Analysis.vb:373-383`: OI up + price up = NEW LONGS; OI down + price up = COVERING | S | AGREES (manual 1142-1145) |
| 6 | `...Scoring.vb:298-299` | `OFISignal` BUY DOMINANT → long vote | `Core/Indicators_OrderFlow.vb:153-163`: weighted bid ÷ ask above the buy ratio | S | AGREES (manual 1233) |
| 7 | `...Scoring.vb:308-325` | `OFIMomentum` confirms or suppresses the OFI vote (disabled since v49) | `Core/Indicators_OrderFlow.vb:556-569`: ratio rising = RISING | S | AGREES (inert) |
| 8 | `...Scoring.vb:328-333` | `CVDSlope` RISING with CVD > 0 → long vote; `CVDDivergence` BEARISH → −1 long | `Core/Indicators_OrderFlow.vb:335-351`: BEARISH = last bar up with net CVD < 0 | S | AGREES (manual 1313-1315) |
| 9 | `...Scoring.vb:336-337` | `TFISignal` BUY PRESSURE → long vote | `Core/Indicators_OrderFlow.vb:385-393`: (buy − sell) ÷ total above the threshold | S | AGREES (manual 1344) |
| 10 | `...Scoring.vb:356-373` | `AggrVelSignal` BURST_BUY same side → +1; opposite → −1 | `Core/Indicators_OrderFlow.vb:178-181` + `Core/AggressorVelocityAccumulator.vb:139`: lean = (buy − sell) ÷ gross; lean ≥ floor = BURST_BUY | S | AGREES (`docs/history-archive.md` §E, v52 row) |
| 11 | `...Scoring.vb:376-381` | `MicroCVDSignal` ACCEL → vote; BULL_DECEL → −1 short; BEAR_DECEL → −1 long | `Core/Indicators_OrderFlow.vb:451-482`: BULL_* = net window flow > 0 | S | AGREES (manual 1386-1390) |
| 12 | `...Scoring.vb:385-393` | MicroCVD FLAT stall: price above VWAP with CVD ≤ 0 → −1 long | numeric inputs | S | AGREES (manual 1391-1393) |
| 13 | `...Scoring.vb:399-407` | `LiqSignal` LONG LIQS → long penalty | `Core/Indicators_OrderFlow.vb:259-282` | S | Consumer AGREES with producer (manual 1452, 1464). **The producer's inputs are wrong: rows 41 and 42** |
| 14 | `...Scoring.vb:417-432` | `SpreadStatus` WIDE → penalty on the ROC side; both sides when ROC is flat | `Core/Indicators_OrderFlow.vb:615-626` | S | AGREES with `docs/bid-ask-spread-proposal.md` §3b. **DOC-1:** `docs/UserManual.md` line 1200 says "dominant-side only" |
| 15 | `...Scoring.vb:441-444`, `:606-608` | `DonchianSignal` LONG (+ PARTIAL) → vote and Pass 2c range alignment | `UI/MainForm_Analysis.vb:534-547`: close ≥ prior upper = LONG | S | AGREES (manual 883; `docs/adaptive-regime-weights-proposal.md` line 74) |
| 16 | `...Scoring.vb:449-452`, `:511-512` | `OBVTrend` RISING without BEARISH divergence → vote; BEARISH blocks the upgrade | `Core/Indicators_Structure.vb:68-83`: BEARISH = price up, OBV down | S | AGREES (manual 916-919) |
| 17 | `...Scoring.vb:457-458` | `VPFRSignal` NEAR_HVN_SUPPORT / IN_LVN_BULL → long vote | `Core/Indicators_Structure.vb:171-185` | S | AGREES with its own spec (commit `3afb674`). **DOC-3:** `docs/UserManual.md` line 967 (stop record D-2) |
| 18 | `...Scoring.vb:469-473` | `VPFRValueAreaSignal` ABOVE_VAH → long partial (disabled in every era) | `Core/Indicators_Structure.vb:220-226` | S | AGREES (inert) |
| 19 | `...Scoring.vb:528-547` | Pass 2b: OI long + CVD bullish → +1; OI long + CVD bearish → −1 | rows 5 and 8 | S | AGREES |
| 20 | `...Scoring.vb:564-573` | Pass 2c trending: `Regime`, EMA BULL, ROC > 0, CVD bullish align with long | `UI/MainForm_Analysis.vb:256-273` (TRENDING_UP = ADX above threshold with +DI > −DI); rows 4, 8 | S | AGREES (`docs/adaptive-regime-weights-proposal.md`) |
| 21 | `...Scoring.vb:602-605` | Pass 2c range: price above VWAP and RSI above 50 align with long | numeric inputs | S | AGREES (`docs/adaptive-regime-weights-proposal.md` lines 72-73) |
| 22 | `...Scoring.vb:648-698` | `TrendStructure` UPTREND with long dominant → +1 | `Core/Indicators_Structure.vb:422-428`: HH + HL | S | AGREES |
| 23 | `...Scoring.vb:714-729` | Step 3 funding thresholds: high positive → −2 long, +1 short | numeric; `FundingBias` uses the same thresholds (`UI/MainForm_Analysis.vb:314-324`) | S | AGREES (`docs/architecture.md` Step 3) |
| 24 | `...Scoring.vb:737-764` | Step 3b: longs crowded + momentum RISING → −1 long; FALLING → +1 long; mirrors; NEUTRAL cases | `UI/MainForm_Analysis.vb:314-324`; `Core/Indicators_OrderFlow.vb:502-527` (delta above threshold = RISING) | S | AGREES (`docs/architecture.md` Step 3b) |
| 25 | `...Scoring.vb:46-113` | `CalcVerdictContext` (payload `verdict_context`): BULL_DECEL and BULL_FADING count as long fading; swing target and stop; breakdown hit labels | rows 3, 11; `UI/MainForm_Analysis.vb:581-584` | S | AGREES (`docs/verdict-context-tag-proposal.md` 50-53) |
| 26 | `Core/ScoringEngine_Calculate_Verdict.vb:33-78` | `Regime` veto: TRENDING_UP with short dominant → NO TRADE; TRANSITIONAL penalty | row 20 producer | S | AGREES |
| 27 | `...Verdict.vb:99-118` | Dominant LONG reads `MTFGatePassLong` | `Core/Indicators_Structure.vb:519-520`: pass long = 15m trend not BEAR | S | AGREES |
| 28 | `...Verdict.vb:223-224` | Legacy POC cap gate | `Core/Indicators_Structure.vb:171-176` | S | **DISAGREES** (the stop record; legacy path inactive) |
| 29 | `Core/ScoringEngine_Helpers.vb:136-169` | Fast-exit primitives: BEAR_ACCEL / BEAR_DECEL, SELL DOMINANT, SELL PRESSURE, CVD falling and negative are adverse to a long | rows 6, 8, 9, 11 | S (payload `hold_status`) | AGREES |
| 30 | `...Helpers.vb:189-243` | `CalcHoldStatus`: OBV BEARISH → EXIT long; RSI BEARISH → EVALUATE long; mirrors | rows 2, 16 | S (payload `hold_status`) | AGREES |
| 31 | `Core/SignalEmitter.vb:91-96` | `DeriveDirection`: NO TRADE prefix before the direction words | verdict strings | S (payload `direction`) | AGREES |
| 32 | `...SignalEmitter.vb:327-332` | Swing target and nearest HVN by side; `pocGated` | `UI/MainForm_Analysis.vb:581-584`; `Core/Indicators_Structure.vb:171-176` | S | Swing and HVN AGREE; **`pocGated` DISAGREES** (the stop record) |
| 33 | `...SignalEmitter.vb:457` | Swing stop by side | `UI/MainForm_Analysis.vb:581-584` | S | AGREES |
| 34 | `ExitGuardEvaluator.vb:120`, `:157-160` | Shared primitives; strip wording "OFI SELL" for a long | rows 6, 9, 29 | D | AGREES |
| 35 | `Core/AlertsTracker.vb:114-169`, `:203-211` | Liquidation cascade: buy-side liquidations = "SHORTS getting stopped out" | rows 41, 42 | D | **DISAGREES** (`ALERT-1`: never fires on the stream; `M` trades on the wrong side) |
| 36 | `Core/AlertsTracker.vb:295-317` | Nearest carried level above and below by price | price only | D | AGREES |
| 37 | `LiveMicrostructureEvaluator.vb:161-167` | Imbalance side "bid" when ratio > 1 | row 6 producer | D | AGREES |
| 38 | `LiveMicrostructureEvaluator.vb:187-190` | Burst tag | row 10 producer | D | AGREES |
| 39 | `LiveMicrostructureEvaluator.vb:207-212` | Absorption tag ABSORB_ABOVE / ABSORB_BELOW | `Core/Indicators_OrderFlow.vb:208-224` | D | AGREES |
| 40 | `LiveMicrostructureEvaluator.vb:249-280` | Bracketing levels by price | price only | D | AGREES |
| 41 | `DeribitWsFeed.vb:488` | Streamed trade `liquidation` → `TradeRecord.Liquidation` | Deribit: flag T / M / MT on liquidation trades (REST copies carry it) | S (feeds `CalcLiquidations`) | **DISAGREES in effect: `L-1`** |
| 42 | `Core/Indicators_OrderFlow.vb:268-272` | Flagged trade booked by taker direction | Deribit: M = maker side liquidated | S | **DISAGREES for M: `L-2`** |

- **Stop-class disagreements:** rows 28 and 32 (the POC-tier gate, already reported), 41 (`L-1`) and 42 (`L-2`).
- **Display-only disagreement:** row 35 (`ALERT-1`).
- **Documentation and data findings, not stop-class:**
  - `DOC-1`: `docs/UserManual.md` line 1200 contradicts `docs/bid-ask-spread-proposal.md` §3b; the code follows the spec.
  - `DOC-3`: `docs/UserManual.md` line 967 (already stop-record D-2).
  - `L-3`: `docs/UserManual.md` lines 1452 and 1463 call `large_liq_size` "200 BTC" (about 15 M USD). The code compares it with USD sums; every store amount is a whole 10 USD contract (liquidation output §6), and the median liquidation trade is 6,000 USD. `docs/trader-profile.md` gives the value without a unit and says to calibrate it against the logged sizes, which are USD. So the manual's unit is the error. Once `L-1` is fixed, almost every liquidation window would take the large penalty (2) instead of the standard one (1).
  - `DESIGN-1`: `docs/websocket-migration-proposal.md` line 43 says the trade fields "map 1:1 (price, amount, direction, timestamp, liquidation)". The data contradicts that for `liquidation`.
  - `DATA-1`: the store kept two copies of 93 liquidation trades, 91 with the same trade id, differing only in the flag. Not investigated; it belongs to the trade-store owners.
- **Not audited:** `UI/MainForm_Render_Cards.vb` and `UI/MainForm_PlaintextSnapshot.vb` label renderings.

### R.5 Producer tie asymmetries (exemptions for the mirror fixtures, when resumed)

| Producer (file:line) | Exact tie | Resolves to |
|---|---|---|
| `Core/Indicators_OrderFlow.vb:452` `CalcMicroCVD` | net window flow = 0 | the bear branch |
| `UI/MainForm_Analysis.vb:370` `OISignal` | price equal to the close 15 bars back | "not up" → NEW SHORTS or CAPITULATION |
| `Core/Indicators_OrderFlow.vb:275-278` `CalcLiquidations` | long size exactly dominance × short size | LONG LIQS (≥ against >) |
| `Core/Indicators_Structure.vb:471`, `:485-496` `CalcMTFGate` | 15m +DI = −DI | a bear vote (and the ADX-strong vote goes bear) |
| `Core/Indicators_Structure.vb:172-182` `CalcVPFRLite` | price = POC | NEAR_HVN_RESIST (short vote); IN_LVN_BEAR in an LVN |
| `UI/MainForm_Analysis.vb:522-524` `PriceVsEMA200` | price = EMA200 | BELOW (display only; scoring uses the number) |
| `Core/ScoringEngine_Calculate_Scoring.vb:46` `CalcVerdictContext` | long score = short score | read as long |

### R.6 Era confirmation (carry-in 1)

| Version | Commit, UTC | Scoring change | Evidence |
|---|---|---|---|
| v52 | `3bc2a1e`, 2026-07-14 14:19 | NY aggressor-velocity TFI modifier armed | `docs/history-archive.md` §E v52 row: "#5 aggressor-velocity TFI-modifier scoring WIRE-IN — its own ⚠ dataset boundary" |
| v53 | `1811b8d`, 2026-07-15 15:07 | Funding momentum window; changes a logged input (`FundingMomentum`), not the scoring code | `docs/history-archive.md` §E v53 row ("BUNDLED at the v52 boundary") |
| v58 | `bd31a1a`, 2026-07-22 09:35 | ASIA volume multipliers 1.10 / 1.05 → 1.00 / 1.00; moves the ASIA Volume vote thresholds | `settings.json` change_log v58 entry; `docs/history-archive.md` §G line 235. ⚠ §E has no v58 row |
| v60 | `631a3f5`, 2026-07-22 17:35 | LONDON burst threshold 5.5 arms the LONDON TFI modifier | `docs/history-archive.md` §I line 420 ("LONDON was armed at v60"); settings diff. ⚠ §E has no v60 row |
| v65 | `970087b`, 2026-08-01 18:45 | ASIA burst threshold 5.5 arms the ASIA TFI modifier | `docs/DeribitIndicatorProject.md` §15 v65 row |
| v66 | `fd52299`, 2026-08-10 18:06 (deployed 18:35) | OBV `trend_gate` 18 → 23; changes a logged input (`OBVTrend`) | `docs/DeribitIndicatorProject.md` §15 v66 row |

- The re-score will run each row under the settings of its commit-time era, and test the adjacent eras on a mismatch. Not started.

### R.7 Tier floor (carry-in 3)

- At the shipped penalties (2 below ADX 22.5, 1 up to 25) the tier floor never binds for raw ≥ 3: `raw − penalty` always beats the floor (12→9, 9→6, 6→3). The TRANSITIONAL demotion is the ADX penalty alone. The census will state this with the logged `RegimePenalty` check; the census is not started.

### R.8 Re-run

```
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode liqflag --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

### R.9 What I verified, and what I did not (resumed session)

| Verified claim | How |
|---|---|
| Deribit's `direction` and `liquidation` field meanings | WebFetch of `docs.deribit.com` `public/get_last_trades_by_instrument`, quoted |
| Streamed copies of liquidation trades carry `none` | `--mode liqflag` output §2: 93 of 97 twins, streamed copy first, 91 of 91 same trade id |
| Scoring never saw a liquidation | Output §3 and §5: 32 rows with a liquidation in their window, 0 logged; 51,107 of 51,107 rows `NONE` |
| The record path from the stream to `CalcLiquidations` passes the flag through unchanged | Read `DeribitWsFeed.vb:470-507`, `MarketState.vb:127-134`, `:248-252`, `WsMarketDataSource.vb:91-106` |
| `A81b` fails on shipped code and passes on the booked-by-liquidated-side variant | Harness runs with the environment variable; scratch mutation; tracked file blob unchanged |
| The default gate stays ALL PASS | Default harness run: `A81a` PASS ×2, `A81b` SKIP, ALL PASS |
| The swing read's default mode is still unchanged | Re-run after adding `--mode liqflag`; identical apart from the timestamp |

- **Not verified:**
  - The Deribit WebSocket channel's field list (the page could not be fetched).
  - The side each of the 32 rows would have penalised, and whether any verdict would change.
  - Rows before 2026-07-31 21:49 UTC (no store).
  - Why the store admitted same-id duplicates (`DATA-1`).
  - The UI renderers' label readings.
  - Whether `MT` ever occurs on this instrument.
