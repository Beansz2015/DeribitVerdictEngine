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
