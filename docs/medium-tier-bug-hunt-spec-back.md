# Spec-back — MEDIUM-tier bug hunt, session 1 (stopped on a gate defect)

**Written:** 2026-09-16 (UTC) by the session 1 seat. **Brief:** [`docs/medium-tier-diagnosis-brief-2026-09-16.md`](medium-tier-diagnosis-brief-2026-09-16.md). **Outcome record:** [`docs/medium-tier-bug-hunt-2026-09-16.md`](medium-tier-bug-hunt-2026-09-16.md) (the read). **Format:** [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md). **Model / effort used:** Opus, high.

**One-line result:** the POC-tier gate at `Core/SignalEmitter.vb:330-332` reads the VPFR NEAR_HVN labels with inverted geometry. Session 1 stopped under `docs/medium-tier-diagnosis-brief-2026-09-16.md` §0. No score is affected; 143 of 8,508 verified population rows (1.68 %) would change verdict.

**Legend. IDs used in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` | This packet, handle | A check the reader can run, with its output pasted from this seat's run |
| `E-n` | This packet, build-time evidence | A check whose instrument was a scratch copy; the reader can rebuild it from the recipe but not run it as committed |
| `D-n` | This packet, decision | A decision queued for the trader or the orchestrator (section 3) |
| `A80a` / `A80b` | Harness fixtures, `verify/ordercheck/Program.vb:15620` / `:15647` | `A80a` pins which side of price the POC sits on for each NEAR_HVN label. `A80b` is the known-defect repro of the gate's documented intent; it runs only with `ORDERCHECK_KNOWN_DEFECTS=1` |
| `A26b` | Harness fixture, `verify/ordercheck/Program.vb:3330` | Pre-existing v51 ladder-walk fixture that hand-sets `VPFRSignal = "NEAR_HVN_RESIST"` with the POC above price |
| `P10` | Parked observation, `docs/DeribitIndicatorProject.md` §16.6 | "POC tier of the target ladder rarely places"; its action proposes removing the HVN gate |
| `508f33d` / `3afb674` / `721882e` | Git commits, 2026-04-09 UTC | Added `CalcVPFRLite` and its labels / the VPFR score vote / the VPFR target cap (ancestor of the POC-tier gate) |
| `25c5883` | Git commit, 2026-09-15 18:31:07 UTC | The brief |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It shows the defect on the shipped code in one command.

### H-1 — the failing fixture on the shipped code

```
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
PASS  A80a price inside the POC bucket BELOW the POC → NEAR_HVN_SUPPORT, and the POC sits ABOVE price
PASS  A80a price inside the POC bucket ABOVE the POC → NEAR_HVN_RESIST, and the POC sits BELOW price
FAIL  A80b long: the POC is the only structure on the target side (100020.0 vs entry 100005.0, label NEAR_HVN_SUPPORT) → the HVN-gated POC tier places at it, as 721882e specifies ('wall above price') — KNOWN DEFECT: got target 100040.0 (FALLBACK_ATR). Core/SignalEmitter.vb:331 opens the long POC tier only on NEAR_HVN_RESIST or IN_LVN_BEAR, which CalcVPFRLite emits when the POC sits on the OTHER side of price
FAIL  A80b short: the POC is the only structure on the target side (100020.0 vs entry 100035.0, label NEAR_HVN_RESIST) → the HVN-gated POC tier places at it, as 721882e specifies ('floor below price') — KNOWN DEFECT: got target 100000.0 (FALLBACK_ATR). Core/SignalEmitter.vb:332 opens the short POC tier only on NEAR_HVN_SUPPORT or IN_LVN_BULL, which CalcVPFRLite emits when the POC sits on the OTHER side of price
2 FAILURE(S)
```

- **Load-bearing value:** `A80a` PASS on both lines (the producer puts the POC above price for `NEAR_HVN_SUPPORT`) together with `A80b` FAIL on both lines. Either alone proves nothing.

### H-2 — the documented intents, straight from git

```
$ git show 721882e | grep -n "wall above price\|floor below price"
44:+        '   Long  : VPFRSignal = NEAR_HVN_RESIST or IN_LVN_BEAR  (wall above price)
46:+        '   Short : VPFRSignal = NEAR_HVN_SUPPORT or IN_LVN_BULL (floor below price)
$ git show 508f33d | grep -n "below POC (long-friendly)\|above POC (short-friendly)"
64:+    '   NEAR_HVN_SUPPORT -- price within hvnProximityPct below POC (long-friendly)
65:+    '   NEAR_HVN_RESIST  -- price within hvnProximityPct above POC (short-friendly)
$ git show 3afb674 | grep -n "near POC from"
24:+'          NEAR_HVN_SUPPORT -> +1 LONG  (price near POC from below = HVN bounce zone)
25:+'          NEAR_HVN_RESIST  -> +1 SHORT (price near POC from above = HVN rejection zone)
```

- **The identity to check:** `508f33d` says RESIST = price ABOVE the POC. `721882e` says RESIST = a wall ABOVE price. Both cannot hold. The code follows `508f33d` for the label and `721882e` for the gate.

### H-3 — the share of rows, and the proof that the recompute is trustworthy

```
dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode pocgate --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
```

Key lines from `docs/medium-tier-bug-hunt-2026-09-16-poc-gate-output.md`:

```
| ALL | population rows | 8810 | 8508 | 96.6 |
| ALL | all kinds | 39594 | 38665 | 97.7 |
- Verified rows with logged placed levels: 38665. ComputeSideLevels as shipped reproduces all four logged Placed* values: 38662 (99.99 %).
- Rows where the counterfactual gate moved a STOP (must be 0; the gate reads targets only): 0.
- Logged TargetCapReason = poc in the whole swing read population (all months, verified or not): 0 of 8810.
| ALL | STRONG | 496 | 5 | 5 | 5 | 5 (1.01 %) |
| ALL | MEDIUM | 2351 | 34 | 34 | 34 | 34 (1.45 %) |
| ALL | WEAK | 5661 | 104 | 104 | 104 | 104 (1.84 %) |
| ALL | ALL | 8508 | 143 | 143 | 143 | 143 (1.68 %) |
| ALL | ALL | 8233 | 144 | 0 |
- Verified rows: 38665. Rows where the long or the short placed target moves: 1288 (3.33 %). These values reach the CSV Placed* columns and the bridge payload levels.
```

- **Load-bearing values:** the 99.99 % placed-level reproduction (without it the counterfactual means nothing), and "moves = to POC = flips" on every row of output §7.1 (the intended gate only ever adds a sub-floor POC target).
- **Arithmetic identity:** 5 + 34 + 104 = 143, and 496 + 2,351 + 5,661 = 8,508.
- The first run fetches about 83,000 Deribit bars into a gitignored cache (about 20 s). A second run from the cache matched the first on every statistic.

### H-4 — nothing in the engine changed, and the default gate is green

```
$ git diff --stat HEAD -- Core UI settings.json "*.vbproj" AnalysisLogger.vb DynamicNorms.vb
(empty)
$ dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
...
PASS  A80a price inside the POC bucket BELOW the POC → NEAR_HVN_SUPPORT, and the POC sits ABOVE price
PASS  A80a price inside the POC bucket ABOVE the POC → NEAR_HVN_RESIST, and the POC sits BELOW price
SKIP  A80b known-defect repro (the POC-tier gate reads the NEAR_HVN labels with inverted geometry) — set ORDERCHECK_KNOWN_DEFECTS=1 to run; docs/medium-tier-bug-hunt-2026-09-16.md
...
ALL PASS
```

### H-5 — the swing read's default mode is unchanged by the new branch

```
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
diff <(grep -v "^- Run at (UTC)" docs/swing-vs-fallback-target-read-2026-09-15-output.md) <(grep -v "^- Run at (UTC)" backtest_data/swing-fallback-read/swing-fallback-read-output.md) && echo "DEFAULT MODE IDENTICAL (apart from the run timestamp line)"
```

```
DEFAULT MODE IDENTICAL (apart from the run timestamp line)
```

### E-1 — reverse mutation: `A80b` can pass (scratch instrument, not committed)

- **Recipe:** copy `verify/ordercheck/OrderCheck.vbproj` to a scratch folder; rewrite `..\..\` to the absolute repo path; set `EnableDefaultCompileItems` false; include `verify/ordercheck/Program.vb` explicitly; point the `Core\SignalEmitter.vb` include at a scratch copy with `sed '331s/"NEAR_HVN_RESIST"/"NEAR_HVN_SUPPORT"/; 332s/"NEAR_HVN_SUPPORT"/"NEAR_HVN_RESIST"/'`; set `OutputPath` under `verify/ordercheck/bin/` so the harness still finds the repo root. Build and run with `ORDERCHECK_KNOWN_DEFECTS=1`.
- **Result on this seat's run:**

```
FAIL  A26b swing+HVN too loose → HVN-gated POC places (62050) — target=62070 reason=''
PASS  A80a price inside the POC bucket BELOW the POC → NEAR_HVN_SUPPORT, and the POC sits ABOVE price
PASS  A80a price inside the POC bucket ABOVE the POC → NEAR_HVN_RESIST, and the POC sits BELOW price
PASS  A80b long: the POC is the only structure on the target side (100020.0 vs entry 100005.0, label NEAR_HVN_SUPPORT) → the HVN-gated POC tier places at it, as 721882e specifies ('wall above price')
PASS  A80b short: the POC is the only structure on the target side (100020.0 vs entry 100035.0, label NEAR_HVN_RESIST) → the HVN-gated POC tier places at it, as 721882e specifies ('floor below price')
1 FAILURE(S)
```

- The tracked `Core/SignalEmitter.vb` stayed at blob `6dc5d716741a69db59d02cc76dd9fa4c6072b57a` (= `HEAD`) and MD5 `fed34ff7f8f2b9a6b96ecf61c4040df2` before and after. The scratch build output was deleted.
- ⚠ **This scratch project is the sole proof that `A80b` can pass.** It was not committed. The recipe above rebuilds it.

---

## 2. Decisions taken (auto-proceed, one line each)

- **Stop scope:** stopped after the four stop-report items (file and line, failing fixture, share of rows, evidence); `docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.1-§2.4 not started — the brief says "stop there".
- **Known-defect fixture gating:** options (a) failing fixture in the default run, (b) uncalled fixture, (c) run only with `ORDERCHECK_KNOWN_DEFECTS=1` → **(c)**. Not a cheaper-and-less-truthful trade: (a) turns every session's pre-push gate red, and a permanently red gate hides the next regression behind the known one; (b) cannot be run. The default run still prints a SKIP line that names the defect and this read.
- **`A80a` in the default run:** it passes on shipped code and pins the producer geometry that `A26b` bypasses → kept always-on.
- **Instrument home:** a new `--mode pocgate` in `tools/ops/SwingFallbackRead` (new file `PocGateDefect.vb`, one branch in `SwingFallbackRead.vb`) → reuses the swing read's population funnel; default mode re-verified identical (`H-5`).
- **Measuring an unlogged label:** options (a) infer it from scores, (b) default it to `NEUTRAL` like the What-If adapter, (c) recompute `CalcVPFRLite` and keep only rows that reproduce four logged VPFR fields → **(c)**; (b) hides this exact defect, (a) cannot tell the NEAR_HVN branch from the LVN branch.
- **Candle source for August and September:** Deribit public chart data into the instrument's own gitignored cache, not into `backtest_data/candles_*` → the backtest store stays untouched for other tools.
- **Counterfactual gate:** swap the NEAR_HVN labels in `r` and call the shipped `ComputeSideLevels` → one seam, no copy of the arbitration; valid because `pocGated` is the only `VPFRSignal` reader in that file.
- **Read doc date:** 2026-09-16, the UTC date at writing (`date -u` 09:18).

---

## 3. Decisions queued

### D-1 — the POC-tier gate (trader ruling; ⚠ reserved: moves placed levels on a live surface and verdicts through Step 5c)

**RULED (trader), 2026-09-16 UTC: (a).** The POC-tier gate follows its spec: swap the NEAR_HVN labels in both copies (`Core/SignalEmitter.vb` and the legacy twin in `Core/ScoringEngine_Calculate_Verdict.vb`). Trader's reason: volume-profile scoring is core to the strategy. Not implemented in this session; the orchestrator specs a separate fix build.

D-table row as queued, **no values chosen**:

| Option | What changes | Measured effect |
|---|---|---|
| (a) Make the gate follow its spec: swap the NEAR_HVN labels at `Core/SignalEmitter.vb:331-332` (and the legacy twin at `Core/ScoringEngine_Calculate_Verdict.vb:223-224`) | The POC tier can place when price sits inside the POC bucket with no nearer structure | 143 of 8,508 verified population rows (1.68 %) become NO TRADE through Step 5c; 1,288 of 38,665 verified rows (3.33 %) get a different placed target; `A26b` must be rewritten |
| (b) Remove the label gate: the POC places as a pure geometric fallback (the `P10` action) | The POC tier opens on geometry alone | **Not measured** |
| (c) Keep the shipped behaviour; correct the comments and `docs/UserManual.md` line 961 to say the POC tier never places | Nothing in behaviour | 0 rows; the tier stays dead code |

- **Read (hypothesis):** (c) is the cheapest and the least truthful; it keeps a gate whose code says one thing and does another, the kind of silent tolerance a future seat cannot detect from the code. Between (a) and (b) I have **no read on outcomes**: the evidence that decides is the net EV per trade of the rows each option would veto, which nobody has measured.
- **Scoping information:** the narrowest version of (a) touches two lines in one file plus the legacy twin, and changes no score. The cheapest measurement is the swing read's candle walk on the 143 rows (join `--mode pocgate` row flags to the existing walks).

### D-2 — the VPFR score vote's documentation (trader ruling on the doc; not a stop)

**RULED (trader), 2026-09-16 UTC: (a).** Correct `docs/UserManual.md` line 967 to the code's geometry. Not done in this session; the orchestrator owns the edit.

- **Facts:** the vote at `Core/ScoringEngine_Calculate_Scoring.vb:457-458` matches its own spec (`3afb674`: price near the POC from below → LONG). `docs/UserManual.md` line 967 says `NEAR_HVN_RESIST` is "price ... just below a high-volume node acting as resistance", the opposite geometry.
- **Options:** (a) correct `docs/UserManual.md` line 967 to the code's geometry; (b) treat line 967 as the intended thesis and re-spec the vote (a scoring change, reserved).
- **Read (hypothesis):** (a). The two specs written with the code agree with the code; line 967 is a reader's gloss that contradicts the same manual's own table (lines 950-951).
- ⚠ **Why it still matters:** NEAR_HVN labels sit on 69.8 % of verified rows, and on population rows that carry one the vote points against the verdict side 64.1 % of the time (2,844 of 4,440; descriptive, output §8). Whether this "toward the POC" vote is anti-predictive is `docs/medium-tier-diagnosis-brief-2026-09-16.md` §3 question D-3 (session 2), not a bug.
- **Shares a root with `D-1`:** the label names. `SUPPORT` names a POC above price. Both misreadings trace to that name. Renaming the labels would be a CSV-free, display-visible change (the card shows the label), so reserved.

### D-3 — resume session 1? (orchestrator)

**RULED (orchestrator), 2026-09-16 UTC: resume** session 1, with the era, label-consumer-audit and tier-floor carry-ins (section R of this packet).

- **Read (hypothesis): resume.** Scores, the re-score reconstruction, the mirror fixtures and the tier walk do not read this gate. The census couples to it only through Step 5c on 1.68 % of population rows, and MEDIUM is not over-exposed (1.45 % against WEAK 1.84 %).
- **If resumed, carry these into the resumed brief:** the corrected era list and the mirror-test limit in section 4 of this packet.

---

## 4. Feedback on the brief

### What worked

- **The stop rule named its report items** ("the file, the line, a failing fixture and the affected share of rows"). That turned a doc contradiction into a concrete, checkable packet instead of an opinion.
- **Trap (1) in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §0** ("silently defaulting a field the CSV does not log") was exactly the hazard. `VPFRSignal` is unlogged, and the What-If adapter's `NEUTRAL` default would have hidden this defect.
- **"Confirm the label semantics before concluding anything"** (the MicroCVD row in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §1). The same discipline, applied to the VPFR labels, found the defect.

### What broke or was incomplete

- ⚠ **Highest value: the mirror test in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.2 cannot catch this class.** This defect is mirror-symmetric: the long and the short gate are both inverted. So is any vote whose two sides are flipped together. A mirror fixture passes on it. **Pair every mirror fixture with a per-site direction fixture built from documented intent, on state from the real producer.**
- **`docs/medium-tier-diagnosis-brief-2026-09-16.md` §1, "Scoring-affecting settings eras inside the book: v65 and v66", is incomplete.** Verified by a scripted diff of the scoring-relevant `settings.json` keys over every commit since 2026-07-01:

| Version | Commit, UTC | Scoring change inside the population span |
|---|---|---|
| v52 | `3bc2a1e`, 2026-07-14 14:19 | `aggressor_velocity.scoring_enabled` true + NY `burst_ratio_threshold` 4.5: the NY TFI burst modifier goes live |
| v58 | `bd31a1a`, 2026-07-22 09:35 | ASIA `session_volume` multipliers 1.10 / 1.05 → 1.00 / 1.00: moves the ASIA Volume vote thresholds |
| v60 | `631a3f5`, 2026-07-22 17:35 | LONDON `burst_ratio_threshold` 5.5: the LONDON TFI burst modifier goes live |
| v65 | `970087b`, 2026-08-01 18:45 | ASIA `burst_ratio_threshold` 5.5 (listed in the brief) |
| v53, v66 | `1811b8d`, `fd52299` | Change indicator computation (`FundingMomentum`, `OBVTrend`), which the CSV logs, so not re-scoring eras for a logged-input reconstruction |

- **`docs/medium-tier-diagnosis-brief-2026-09-16.md` §1, the tier-floor row, is imprecise.** "a raw 8–9 MEDIUM becomes 7, which is WEAK": at penalty 2, raw 8 → 6 and raw 9 → 7; at penalty 1, raw 8 → 7 and raw 9 → 8, which stays MEDIUM. The floors never bind at the shipped penalties for raw ≥ 3, so the demotion is pure penalty (arithmetic on `settings.json` v68).
- **`docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.3 scopes the audit to `_Scoring.vb` plus the Step 4 penalty.** This defect sits in the Step 5b arbitration (`SignalEmitter.ComputeSideLevels`), which that table would not have listed. The general wording of the stop rule in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §0 caught it. Add the Step 5b arbitration gates and Step 5c to the audit scope.
- **Fixture shape:** `A26b` hand-sets a label with a geometry the producer never emits. A fixture for any gate that consumes a label should build the label from the producer. Worth one line in a resumed brief.
- **The brief's header reads "Written: 2026-09-16 (UTC)", but commit `25c5883` is 2026-09-15 18:31:07 UTC.** This seat's first `date -u` read 2026-09-15 18:42 UTC. The header carries the GMT+8 date.

### A constraint pair that nearly conflicted

- "A failing fixture" (`docs/medium-tier-diagnosis-brief-2026-09-16.md` §0) against a shared pre-push gate that must read ALL PASS for every session. **The escape hatch was the environment-variable gate** (section 2 of this packet). Write it into the next brief so the next seat does not have to choose.

---

## 5. What I did not verify

- Full list: `docs/medium-tier-bug-hunt-2026-09-16.md` section 7. The items that change a decision:
  - **Net EV per trade of the 143 rows the documented intent would veto** — decides between `D-1` (a) and (b).
  - **`D-1` (b) is not measured at all.**
  - **929 unverified trading-week rows (302 population rows)** are not classified; every share is over verified rows.
  - **The 3 verified rows whose placed levels do not reproduce** were not investigated.
  - **`docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.1-§2.4** were not done (stopped), including the full audit; only the MicroCVD decel site and the VPFR vote were checked against intent.
  - **The brief's "33 vote and mutation sites"** count was not checked.
- **Carried over without checking:** the swing read's population funnel and trading-week rule; the v66 collector deploy time (2026-08-10 18:36:01 UTC) from `tools/ops/SwingFallbackRead/SwingFallbackRead.vb`.

---

## R. Resumed session (2026-09-16 UTC) — stopped again on `L-1` and `L-2`

**Ruling acted on:** the orchestrator's D-3 = resume session 1, with the era, label-consumer-audit and tier-floor carry-ins. D-1 and D-2 were not touched. **Outcome record:** `docs/medium-tier-bug-hunt-2026-09-16.md` section R.

**IDs added here:**

| ID | Source and kind | Meaning |
|---|---|---|
| `L-1` | The read, section R.2 (stop-class finding) | The live trade stream (`trades.BTC-PERPETUAL.100ms`, `DeribitWsFeed.vb:28`) never delivers a `liquidation` flag that the parse at `DeribitWsFeed.vb:488` can read, so the liquidation penalty has never fired. The parse itself does read the field; whether the channel omits it is unverified (wording corrected by the orchestrator, 2026-09-16) |
| `L-2` | The read, section R.3 (stop-class finding) | `CalcLiquidations` books a maker-side (`M`) liquidation on the taker's side |
| `L-3` | The read, section R.4 (documentation finding) | `large_liq_size` is "200 BTC" in the manual but compared with USD sums |
| `A81a` / `A81b` | Harness fixtures, `verify/ordercheck/Program.vb` | Taker-side mapping pin / maker-side known-defect repro (runs only with `ORDERCHECK_KNOWN_DEFECTS=1`) |
| `D-4`, `D-5`, `D-6` | This packet, decisions | Queued in section R.3 of this packet |

### R.1 Ranked handles

**If you only run one, run `H-6`.** It shows the dead liquidation vote on the collector's own data.

**H-6 — `L-1` on the collector's data** (`tools/ops/SwingFallbackRead --mode liqflag`; full output `docs/medium-tier-bug-hunt-2026-09-16-liquidation-output.md`)

```
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode liqflag --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv
```

```
| Liquidation-flagged store rows | 97 |
| ... with an identical copy (same timestamp, price, amount, direction) whose flag is `none` | 93 |
| ... where that `none` copy was appended EARLIER in the same file (the streamed copy) | 93 |
| ... carrying a trade id on both copies | 91 |
| ... where the `none` copy has the SAME trade id | 91 |
| Collector log rows inside the store span | 36327 |
| ... whose 500-trade window holds at least one liquidation trade | 32 |
| ... of which the collector logged LiqSignal other than NONE | 0 |
| ... of which the collector logged a non-zero liquidation size | 0 |
| All merged rows from the v51 edge (pooled book + box live log) | 51107 | 51107 | 0 | 0 | 0 |
| Swing read population rows | 8810 | 8810 | 0 | 0 | 0 |
```

- **Load-bearing values:** 93 streamed-first `none` copies **and** 32 rows with a liquidation in their window **and** 0 logged. Any one alone could be a store or window artefact.
- **Identity:** 93 + 4 unpaired = 97 flagged rows. The 4 without a streamed copy are the 2026-08-17 rows; the stream missed those trades, which repair then filled.

**H-7 — `L-2` failing fixture, and the default gate**

```
ORDERCHECK_KNOWN_DEFECTS=1 dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```

```
PASS  A81a taker-side liquidation (flag T, taker buy) books the TAKER's position → SHORT LIQS
PASS  A81a taker-side liquidation (flag T, taker sell) books the TAKER's position → LONG LIQS
FAIL  A81b maker-side liquidation (flag M, taker buy): the liquidated maker held a LONG → LONG LIQS, per Deribit's `liquidation` field — KNOWN DEFECT: got signal=SHORT LIQS long=0 short=1000. Core/Indicators_OrderFlow.vb:268-272 books every flagged trade by the taker's direction
FAIL  A81b maker-side liquidation (flag M, taker sell): the liquidated maker held a SHORT → SHORT LIQS, per Deribit's `liquidation` field — KNOWN DEFECT: got signal=LONG LIQS long=1000 short=0. Core/Indicators_OrderFlow.vb:268-272 books every flagged trade by the taker's direction
4 FAILURE(S)
```

- The 4 failures are `A81b` ×2 and `A80b` ×2 (the POC-tier gate from the stop record). The default run prints `A81a` PASS ×2, `A81b` SKIP, `A80b` SKIP and ALL PASS, exit 0.

**H-8 — the record path passes the flag through unchanged**

```
$ grep -n "liquidation" DeribitWsFeed.vb
488:            rec.Liquidation = If(t.TryGetProperty("liquidation", liqEl), liqEl.GetString(), "none")
$ grep -n "Sub AppendTrade\|_trades.Add(rec)\|Function GetTrades\|Return New List(Of TradeRecord)(_trades)" MarketState.vb
127:    Public Sub AppendTrade(rec As TradeRecord, nowUtc As DateTime)
130:            _trades.Add(rec)
248:    Public Function GetTrades() As List(Of TradeRecord)
250:            Return New List(Of TradeRecord)(_trades)
```

- **What it proves:** the flag scoring sees is exactly what line 488 parsed from the stream. No later step clears it.

**E-2 — reverse mutation for `A81b`** (scratch project; not committed)

- **Recipe:** the section 1 `E-1` recipe of this packet, with the `Core\Indicators_OrderFlow.vb` include pointed at a scratch copy edited by `sed '268s/If t.Direction = "buy" Then/If (t.Direction = "buy") <> (t.Liquidation = "M") Then/'`.
- **Result:** `A81b` PASS for both sides. The run showed 2 failures, both `A80b`. Tracked `Core/Indicators_OrderFlow.vb` blob `1f8dd1060c686b3d1db3dda4b1485b0118f90f51` equals `HEAD`.

### R.2 Decisions taken (one line each)

- **Stop again:** `L-1` and `L-2` are consumers feeding scores that disagree with their producer → stop-and-report under the orchestrator's rule. No materiality threshold exists in that rule, so the 0-row `L-2` counts as well.
- **Finish the audit before stopping:** a static read with no new build risk. One report then covers every stop-class row, instead of one stop per row.
- **`L-3` classified as documentation, not stop-class:** the only unit claim is the manual's. `docs/trader-profile.md` gives "200" with no unit and says to calibrate against logged USD sizes. The code is not contradicting an authoritative intent; the manual is wrong.
- **`L-1` failing check = a data instrument, not a fixture:** `DeribitWsFeed` needs a live socket and is not linked into the harness. Reaching it would take an engine refactor, which is reserved.
- **`A81b` gated like `A80b`:** same reason as section 2 of this packet; a permanently red gate would hide the next regression.
- **Window rule in `--mode liqflag`:** trade copies collapsed before counting 500. Not collapsing would double-count 93 trades and shrink every window, which understates `L-1`.
- **Era evidence for v58 and v60 from the `settings.json` change log and `docs/history-archive.md` §G and §I:** `docs/history-archive.md` §E has no v57, v58 or v60 rows. Using the change log is the more complete source, not a cheaper one.

### R.3 Decisions queued

**D-4 — the live liquidation flag, `L-1`** (trader ruling; ⚠ reserved: scoring change and a dataset boundary)

**RULED (trader), 2026-09-16 UTC: measure first.** Capture raw `trades.BTC-PERPETUAL.100ms` messages until a liquidation passes (read-only, dev machine). Then (a) parse the field if it is present under another name, else (b) enrich from REST. Never (c) retire the vote. Not implemented in this session; the orchestrator specs the fix build.

| Option | What changes | Measured effect |
|---|---|---|
| (a) Find the stream's liquidation field (or subscribe to a channel that carries it) and parse it | The liquidation penalty starts firing live | 32 of 36,327 collector rows in the store span had a liquidation to score; which side each would penalise is not measured |
| (b) Enrich streamed trades from REST when a liquidation is suspected | Same end state, more moving parts | Not measured |
| (c) Retire the liquidation vote as "never live" and record it | Nothing in behaviour | 0 rows; `docs/trader-profile.md` lists it PREFERRED, so this contradicts the profile |

- **Read (hypothesis):** (c) is the cheapest and the least truthful. The book was scored without a PREFERRED signal and nothing in the code says so. Between (a) and (b) I have no read: the first step is confirming what Deribit's `trades.BTC-PERPETUAL.100ms` notification actually carries, which this seat could not fetch.
- **Shares a root with `D-5` and `D-6`:** all three concern a vote that has never run on live data. Rule them together, or a fix of `L-1` goes live with the wrong maker-side attribution and a threshold in the wrong unit.
- **Scoping information:** the parse is one line (`DeribitWsFeed.vb:488`) and it already reads `liquidation`; the unknown is what the subscribed channel (`DeribitWsFeed.vb:28`) delivers, not the parse. The cascade alarm reads the same flag at `DeribitWsFeed.vb:506`.

**D-5 — maker-side attribution, `L-2`** (trader ruling; ⚠ reserved: scoring)

**RULED (trader), 2026-09-16 UTC:** book `M` to the maker's side, `T` to the taker's side, `MT` to both sides. Not implemented in this session.

- **Options:** (a) book by the liquidated side: `M` → the maker's side, `T` → the taker's side; (b) book `MT` to both sides or split it; (c) keep taker-side booking and document it.
- **Read (hypothesis):** (a) for `M`. It is what Deribit's field means, and `A81b` already asserts it. I have **no read** on `MT` (0 occurrences, and no spec).

**D-6 — the unit of `large_liq_size`, `L-3`** (trader ruling on the value; the manual correction is documentation)

**RULED (trader), 2026-09-16 UTC:** correct the manual's unit to USD; re-derive the value from real liquidation sizes after the fix. Not done in this session.

- **Facts:** the manual says 200 BTC (about 15 M USD, "a genuine cascade event"). The code compares 200 with USD sums; the median liquidation trade in the store is 6,000 USD.
- **Read (hypothesis):** correct the manual's unit now. The threshold value is a calibration question for after `D-4`, against the logged size distribution, as `docs/trader-profile.md` already says.

**D-7 — resume session 1 again? (orchestrator)**

**RULED (orchestrator), 2026-09-16 UTC: resume** session 1 (`docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.1-§2.4), treating `LiqSignal` as logged (`NONE`), under a narrowed stop rule (section R.6 of this packet).

- **Read (hypothesis): resume.** The liquidation vote is absent from every row, so it adds nothing to the tier comparison. The re-score reconstruction should treat `LiqSignal` as logged (`NONE`), which reproduces what the engine scored.

### R.4 Feedback on the orchestrator's carry-ins

- ⭐ **The label-consumer framing found what a vote-site audit would not.** Every scoring consumer of `LiqSignal` is correct; the defect is two layers down, where the exchange's fields become the label. **Add "exchange contract → first label" to the producer side of the audit.** The contract lives in the exchange docs, not in any repo function.
- **"Check the reading against the producer's emission geometry, not another consumer's comment"** did real work twice: for the POC gate, and here, where `docs/UserManual.md` line 1443 documents the taker-side booking as if it were Deribit's meaning.
- **A data check beats a code read for transport claims.** `docs/websocket-migration-proposal.md` line 43 asserted the liquidation field maps 1:1; only the store's duplicate copies showed it does not. Any other "fields map 1:1" claim in that spec is unverified to the same degree.
- **`docs/history-archive.md` §E is not a complete era index:** v57, v58 and v60 have no rows. The `settings.json` change log was the complete source.

### R.5 What I did not verify (resumed session)

- Full list: the read, section R.9. The items that change a decision:
  - **What Deribit's WebSocket trade notification carries** (`D-4` depends on it).
  - **The penalty side and verdict effect for the 32 rows** (needs the re-score, not started).
  - **Rows before 2026-07-31 21:49 UTC** (no trade store; `NONE` there is consistent with `L-1` but not measured).
  - **Why the store admitted same-id duplicate rows** (`DATA-1` in the read).
  - **The re-score reconstruction, the mirror fixtures and the demotion census** (not started: stopped).

### R.6 Resumed again (2026-09-16 UTC) — no new stop; session 1 complete

**Rulings acted on:** the orchestrator's D-7 = resume session 1 (`docs/medium-tier-diagnosis-brief-2026-09-16.md` §2.1–§2.4) with `LiqSignal` as logged. **The narrowed stop rule** (orchestrator, 2026-09-16 UTC):

- Findings in an already-reported class (POC gate, liquidation flag or attribution) are recorded without stopping.
- Stop only for a NEW defect that changes the score, tier or placed level of rows actually in the population, measured, not latent.
- Latent defects are recorded, not stopped on.

The trader's rulings on D-1, D-2 and D-4 to D-6 are recorded in section 3 and section R.3 of this packet. None was implemented: no engine `.vb`, `settings.json` or `docs/UserManual.md` edit. **Outcome record:** `docs/medium-tier-bug-hunt-2026-09-16.md` section R.10.

**IDs added here:**

| ID | Source and kind | Meaning |
|---|---|---|
| `A82a`, `A82b`, `A82c` | Harness fixtures, `verify/ordercheck/Program.vb` | Mirror completeness guard; mirrored market → mirrored scores, breakdown, verdict and levels; every vote site reached |
| `M1`, `M2` | Scratch mutations (not committed) | `M1` moves the `BULL_DECEL` penalty to the long score; `M2` swaps both decel arms |
| `K1`–`K4` | Census comparisons, `tools/ops/SwingFallbackRead/TierDemotionCensus.vb` header | Demoted rows against native rows of their new tier; `K3`, `K4` TRANSITIONAL-only |
| `EVAL-1`, `DISP-1`, `TOOL-1` | The read, section R.10.4 (findings) | Analysis report admits lean NO TRADE rows; card funding-momentum colour ignores the funding sign; offline validator compares `OISignal` with labels it never takes |
| `D-8`, `D-9`, `D-10` | This packet, decisions | Queued in section R.6.3 of this packet |

#### R.6.1 Ranked handles

**If you only run one, run `H-9`.** It shows the reconstruction headline and that no mismatch lacks an input-uncertainty explanation.

**H-9 — re-score reconstruction** (`--mode rescore`; full output `docs/medium-tier-bug-hunt-2026-09-16-rescore-output.md`)

```
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode rescore --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv | grep -E "^\| \*\*All six\*\*|^### 5\.4|^- None\.|^\| Burst modifier applied|^\| No burst modifier"
```

```
| **All six** | 8749 of 8810 | 99.31 | 39331 of 39594 | 99.34 |
| Burst modifier applied | 3101 | 76 | 2.45 | 59 | 68 | 25 |
| No burst modifier | 36493 | 187 | 0.51 | 101 | 154 | 0 |
### 5.4 Rows no input-uncertainty kind explains (the defect candidates)
- None.
```

- **Columns:** "All six" = population rows matching, %, all trading-week rows matching, %. The burst rows = rows, re-scored, mismatching, %, explained by F (forming-bar VPFR volume), by V (another VPFR label), by B (burst modifier not applied).
- **Load-bearing values:** 99.31 **and** "- None.". A high match rate alone could hide a small defect class; the empty candidate list rules that out.
- **Build first:** `dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release`.

**H-10 — mirror fixtures and the default gate**

```
dotnet run --project verify/ordercheck/OrderCheck.vbproj 2>/dev/null | grep -E "A82|^ALL PASS|FAILURE"
```

```
PASS  A82a every IndicatorResults property (116) sits in exactly one mirror class
PASS  A82a every label map is a set of disjoint swap pairs
PASS  A82a mirroring twice returns the original state, and mirroring once changes every non-kept property
PASS  A82b the tracked RSI zones and funding bands are side-symmetric (the mirror of RSI and funding is exact)
PASS  A82b a mirrored market gives the mirrored scores, breakdown, verdict and placed levels (7524 states: 2088 single-site, 5400 joint, 3 cfgs x 4 regimes x 3 session hours)
PASS  A82c the A82b runs reached every vote and mutation site, every tier in every regime and every placed-level tier (72 markers)
PASS  A82c the OBV partial never upgrades across the same runs (v0.42 adverse-divergence block; the partial is that state)
ALL PASS
```

- **`A82c` is load-bearing for `A82b`:** a property test over states that never reach a site proves nothing about that site.

**H-11 — tier-demotion census** (`--mode census`; full output `docs/medium-tier-bug-hunt-2026-09-16-census-output.md`)

```
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode census --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv | grep -E "^\| ALL \(counts only|TierFloor arm wins|^- (main window|carried 24 h) \| NY \| K[13] "
```

```
| TRANSITIONAL rows where the TierFloor arm wins with a floor above zero (either side) | 7965 TRANSITIONAL rows | 0 |
| ALL (counts only, never pooled for outcomes) | 8810 | 882 | 538 | 95 | 2362 | 360 | 5455 | 0 | 5.2 | 51.6 |
- main window | NY | K1 demoted-to-WEAK vs native WEAK | NO DIFFERENCE SHOWN
- main window | NY | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NO DIFFERENCE SHOWN
- carried 24 h | NY | K1 demoted-to-WEAK vs native WEAK | NO DIFFERENCE SHOWN
- carried 24 h | NY | K3 demoted-to-WEAK vs native WEAK, TRANSITIONAL only | NO DIFFERENCE SHOWN
```

- **ALL row columns:** rows, TRANSITIONAL rows, native STRONG, demoted-to-MEDIUM, native MEDIUM, demoted-to-WEAK, native WEAK, demoted two tiers, demoted % of all rows, demoted % of TRANSITIONAL rows.
- Every other comparison (NY `K2`, `K4`; all LONDON and ASIA) is NOT READABLE.

**H-12 — the four existing instrument modes are unchanged by the new code**

```
for pair in swing:swing-vs-fallback-target-read-2026-09-15-output.md pocgate:medium-tier-bug-hunt-2026-09-16-poc-gate-output.md stability:tier-order-stability-read-2026-09-15-output.md liqflag:medium-tier-bug-hunt-2026-09-16-liquidation-output.md; do m=${pair%%:*}; d=${pair#*:}; dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --mode $m --out backtest_data/swing-fallback-read/identity-$m.md > /dev/null; diff -q <(grep -v "Run at (UTC)" docs/$d) <(grep -v "Run at (UTC)" backtest_data/swing-fallback-read/identity-$m.md) > /dev/null && echo "$m: identical to docs/$d apart from the run timestamp" || echo "$m: DIFFERS from docs/$d"; done
```

```
swing: identical to docs/swing-vs-fallback-target-read-2026-09-15-output.md apart from the run timestamp
pocgate: identical to docs/medium-tier-bug-hunt-2026-09-16-poc-gate-output.md apart from the run timestamp
stability: identical to docs/tier-order-stability-read-2026-09-15-output.md apart from the run timestamp
liqflag: identical to docs/medium-tier-bug-hunt-2026-09-16-liquidation-output.md apart from the run timestamp
```

- Covers the project-file change (three `Core/ScoringEngine_*.vb` includes) and the `PgRecomputeVpfr` refactor in `PocGateDefect.vb`.

**H-13 — `EVAL-1`, the filter and its size**

```
sed -n 278,284p analysis/AnalysisRunner.vb
python -c "
import csv,collections
seen=set();c=collections.Counter()
for p in ['AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv','aws_fetch/20260913-153704/analysis_log.csv']:
    r=csv.reader(open(p,encoding='utf-8'));h=next(r);iv,ic,ia=h.index('Verdict'),h.index('VerdictContext'),h.index('ATR')
    for x in r:
        if not x or x[0] in seen or len(x)<=ic: continue
        seen.add(x[0]);v=x[iv].strip().upper()
        if x[ic].strip().upper() in ('CONFIRMED','ALIGNED','FLOW_UNCONFIRMED','MOMENTUM_FADING','STRUCTURALLY_WEAK') and float(x[ia] or 0)>0 and v not in ('','NO TRADE') and not v.startswith('WEAK'):
            c[v if v.startswith('NO TRADE') else 'STRONG or MEDIUM verdict']+=1
print(dict(sorted(c.items())))
"
```

```
            Dim ctxRows = popRows.Where(Function(r)
                Return String.Equals(r.VerdictContext, ctx, StringComparison.OrdinalIgnoreCase) AndAlso
                       r.ATR > 0 AndAlso
                       r.Verdict <> "" AndAlso
                       r.Verdict.ToUpper() <> "NO TRADE" AndAlso
                       Not r.Verdict.ToUpper().StartsWith("WEAK")
            End Function).ToList()
{'NO TRADE [TIE]': 59, 'NO TRADE [WEAK LONG]': 3019, 'NO TRADE [WEAK SHORT]': 4117, 'STRONG or MEDIUM verdict': 3305}
```

- The count mirrors the filter over the merged book only; the report's own population cut is not applied (not verified).

**H-14 — no engine, settings, UI, analysis or manual file changed**

```
git diff --stat 920de06 -- Core UI analysis settings.json docs/UserManual.md DeribitWsFeed.vb AnalysisLogger.vb DynamicNorms.vb MarketState.vb
```

```
(no output)
```

**E-3 — reverse mutation for `A82b`** (scratch projects; not committed)

- **Recipe:** the section 1 `E-1` recipe of this packet (scratch copy of `verify/ordercheck/OrderCheck.vbproj`, absolute includes, output under `verify/ordercheck/bin/<name>-proof/`), with the `Core\ScoringEngine_Calculate_Scoring.vb` include pointed at a scratch copy.
- **`M1`:** line 380 `state.ShortScore = Math.Max(0, state.ShortScore - …)` → `state.LongScore = Math.Max(0, state.LongScore - …)`. Result: `FAIL A82b … 847 asymmetries`; first asymmetry `[tracked | TRENDING_UP | hour 0 | MicroCVD bull decel against a short vote] breakdown rows 22 vs mirror 21`; `1 FAILURE(S)`.
- **`M2`:** lines 380 and 381 both swapped. Result: `ALL PASS`, which demonstrates that a mirror test cannot see a symmetric inversion.
- **Tracked file:** MD5 `5ad308a0876d03e3a9ea4aea822b357f` before and after; blob `f0c4ac4059d7ba64418f4c8e3a8104bb619e0ad5` equals `HEAD`. Scratch outputs deleted.

#### R.6.2 Decisions taken (one line each)

- **Re-score every trading-week row, NO TRADE included, and report the population separately:** more rows checked, nothing lost; the two coincide for directional rows (0 invalid placed levels).
- **Record every explanation per mismatch, not the first match:** the first-match run labelled 25 burst rows "settings era" when F, V or P explained them too. Sets keep the ambiguity visible.
- **Accept a forming-bar (F) explanation only when the four logged VPFR fields still verify:** stricter than a bare label swap (V), which is kept as its own weaker kind.
- **`VPFRValueAreaSignal` from the recompute, not from a price-versus-VAH comparison:** the producer's own rule.
- **Mirror cfgs add every optional modifier ON and arbitration mode 1:** reaches the sites the tracked flags disable (OFI momentum, value-area scoring, best pivot).
- **Pin the OBV partial upgrade as never reached instead of dropping the marker:** a change to the partial or the v0.42 gate becomes visible.
- **`M1` moves one arm, and `M2` swaps both:** the brief's "flip one vote's side" is ambiguous. A both-arm flip passes, so the packet shows the limit instead of hiding it.
- **Census rules written into the instrument header before the first run:** pre-registration, per `docs/medium-tier-diagnosis-brief-2026-09-16.md` §4.
- **Census ALL-sessions row is counts only:** the brief's §4 keeps sessions separate for outcomes.
- **The attribution CSV stays gitignored:** it is row-level data derived from logs that are gitignored by design (brief §5). A prior convention keeps row data out of git; the re-run regenerates it in about 12 s.
- **`EVAL-1`, `DISP-1`, `TOOL-1` recorded and queued, none fixed:** `EVAL-1` and `DISP-1` move rendered values (reserved); `TOOL-1` is outside this seat's scope.

#### R.6.3 Decisions queued

**D-8 — `EVAL-1`, the analysis report's context table** (⚠ reserved: moves a rendered report value)

| Option | What changes |
|---|---|
| (a) Filter with the directional test the tier matrices already use (`IsDirectionalVerdict`, `analysis/AnalysisRunner.vb:197`) and print the excluded lean-row count | The table shows trades only; the count keeps the exclusion visible |
| (b) Keep lean rows as a separate "NO TRADE lean" column | The table shows both, labelled |
| (c) Leave it | Lean rows keep counting as trades; "[TIE]" keeps counting as short |

- **Read (hypothesis): (a).** ⚠ (b) records more, and my pick is the cheaper one. So this needs the trader under the reserved "cheaper and less truthful" class. For (a): the table's frame is trade outcomes, and a lean NO TRADE row is not a trade.

**D-9 — `DISP-1`, the card's funding-momentum colour** (⚠ reserved: rendered value)

| Option | What changes |
|---|---|
| (a) Make the word and colour bias-aware (read `FundingBias`) | FALLING with shorts crowded renders as crowding, RISING as de-crowding |
| (b) Render Step 3b's actual effect from the breakdown (the signed points and side) | The card shows what scoring did, with no heuristic |
| (c) Leave it and document | No change |

- **Read (hypothesis): (b).** It is the more truthful option: it cannot drift from Step 3b, because it reads Step 3b's output.

**D-10 — `TOOL-1`, the offline overlap validator** (not reserved: an offline tool; outside this seat's scope)

- **Options:** (a) compare `OISignal` with the OI producer's labels (NEW LONGS and NEW SHORTS full; COVERING and CAPITULATION partial); (b) leave it.
- **Read: (a).** The orchestrator schedules it.

#### R.6.4 Feedback on the brief and the carry-ins

- ⭐ **The brief's mismatch classes (unlogged input · settings era · unexplained) are not exclusive.** 25 rows fit "settings era" and "unlogged input" at once. A first-match classifier then reports a settings problem that has no mechanism. **Ask for explanation SETS.**
- ⭐ **The forming bar is the hidden unlogged input.** `CalcVPFRLite` gives it the top decay weight, and the CSV logs neither `VPFRSignal` nor `VPFRPoc`. Burst rows mismatch 4.8 times as often. A future CSV rider (log `VPFRSignal` and `VPFRPoc`) would close it. That is a schema change for `docs/csv-rotation-riders.md`, not for this seat.
- **"Flip one vote's side" in the brief's §2.2 needs "one ARM".** A both-arm flip is invisible to any mirror test (`M2`).
- **Coverage should be a machine check, not a claim.** `A82c` found the unreachable OBV upgrade on its first run. A hand-written site list would have listed it as covered.
- **The census frame misses the larger flow.** The brief's §2.4 counts raw MEDIUM → WEAK and raw STRONG → MEDIUM. The penalty also pushes 1,856 rows from WEAK to NO TRADE (NY 1,376), and those rows sit outside the population, so no outcome exists for them.
- **Readability:** TRANSITIONAL demotion cells clear n ≥ 100 only for NY WEAK. LONDON and ASIA need more data before the census can answer for them.
- **The narrowed stop rule worked as written:** `EVAL-1` is a real defect in a rendered report, recorded without a stop, because it changes no score, tier or placed level.

#### R.6.5 What I did not verify (resumed again)

- Full list: the read, section R.10.8. The items that change a decision:
  - **`EVAL-1`'s size inside the report** after its own population cut (`D-8`).
  - **The intended semantics of the funding-momentum colour** (`D-9`): no card spec was read.
  - **LONDON and ASIA demotion outcomes:** NOT READABLE at n < 100.
  - **The effect of the missing liquidation vote (`L-1`) on the 32 rows:** the re-score reproduces the logged `NONE`; it does not score the liquidations the stream dropped.
  - **Mirror symmetry of the indicator producers** (`Core/Indicators_*.vb`): `A82` covers `ScoringEngine.Calculate` and `SignalEmitter.ComputeSideLevels` only.
