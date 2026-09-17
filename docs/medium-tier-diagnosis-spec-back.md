# Spec-back — MEDIUM-tier diagnosis, session 2

**Written:** 2026-09-17 (UTC) by the session 2 seat. **Brief:** [`docs/medium-tier-diagnosis-brief-2026-09-16.md`](medium-tier-diagnosis-brief-2026-09-16.md) §3 and §4. **Outcome record:** [`docs/medium-tier-diagnosis-read-2026-09-17.md`](medium-tier-diagnosis-read-2026-09-17.md) (the read). **Format:** [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md). **Model / effort used:** Opus, high.

**One-line result:** no CONFIRMED cause of a MEDIUM gap, because the gap does not replicate and the score does not rank outcomes in any session; the VPFR score vote is not anti-predictive, and on NY LONG trades it is predictive (CONFIRMED).

**Legend. IDs used in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` | This packet, handle | A check the reader can run, with output pasted from this seat's run |
| `E-n` | This packet, build-time evidence | A check the reader cannot re-run as committed |
| `Q-n` | This packet, decision queued | A decision for the trader or the orchestrator (section 3). Named `Q-` so it does not collide with the brief's question IDs `D-1` … `D-12` |
| `D-1` … `D-12` | Questions in `docs/medium-tier-diagnosis-brief-2026-09-16.md` §3 | The diagnosis questions |
| `F-1` … `F-4` | Candidate fix classes, the read section 3 | D-table rows with no values chosen |
| `L-1` | Finding in `docs/medium-tier-bug-hunt-2026-09-16.md` section R.2 | The liquidation vote never fires live |
| `A82b` | Harness fixture, `verify/ordercheck/Program.vb` (session 1) | Mirror fixture: a mirrored market gives mirrored scores and verdicts |
| `TOOL-2` | This packet, tool finding | `tools/ops/SwingFallbackRead/MediumTierRescore.vb` writes the Class field of `rescore-attribution.csv` unquoted; 56 rows carry a comma in it |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It reads the four load-bearing lines from the committed output in one command. `H-2` proves that output regenerates.

**Prerequisites for the re-run handles (`H-2`, `H-3`, `H-5`):** `dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj -c Release`, then `--mode rescore` (regenerates the gitignored attribution file, MD5 `816853b8864456e97a5485fc8b3a2273`) and `--mode diagexport` (the read section 6).

### H-1 — the headline numbers

```
grep -E "^\| NY \| MEDIUM - WEAK \| -1.0|^\| NY \| slope of net EV on effective score share of regime max \(slope per 0.1\) \| -0.3|opposes the verdict side on|^\| NY \| NEAR_HVN \| main \| agree - oppose, LONG trades only" docs/medium-tier-diagnosis-2026-09-17-output.md
```

```
| NY | MEDIUM - WEAK | -1.0 [-2.2, +0.2] (1552/3523) | -1.2 [-2.7, +0.5] (630/1393) | -0.8 [-2.5, +0.8] (922/2130) | NO DIFFERENCE SHOWN |
| NY | slope of net EV on effective score share of regime max (slope per 0.1) | -0.3 [-0.8, +0.2] (5417) | -0.3 [-0.8, +0.2] (2160) | -0.3 [-1.1, +0.5] (3257) | NO DIFFERENCE SHOWN |
- Reproduces session 1's descriptive share (`docs/medium-tier-bug-hunt-2026-09-16.md` section 5): on verified population rows with a NEAR_HVN label, the vote opposes the verdict side on 2844 of 4440 (64.1 %).
| NY | NEAR_HVN | main | agree - oppose, LONG trades only | +5.0 [+2.4, +7.2] (445/841) | +4.4 [+0.2, +7.5] (223/454) | +5.5 [+1.6, +8.7] (222/387) | CONFIRMED (d > 0) |
```

- **Load-bearing values:** the NY slope CI upper bound (+0.2) **and** the same-sign slope in H1 and H2. A flat FULL slope alone could hide opposite halves.
- ⚠ **The NY VPFR LONG-only label is resample-sensitive.** A 200-resample pilot labelled it DISCOVERY ONLY (H2); at 10,000 its H1 CI lower bound is +0.2. Quote it only from the 10,000-resample output.

### H-2 — the full run is deterministic

```
python tools/ops/medium_tier_diagnosis.py --root . --out backtest_data/swing-fallback-read/identity-diagnosis.md > /dev/null 2>&1
diff <(grep -v "^- Run at (UTC)" docs/medium-tier-diagnosis-2026-09-17-output.md) <(grep -v "^- Run at (UTC)" backtest_data/swing-fallback-read/identity-diagnosis.md) > /dev/null && echo "DIAGNOSIS OUTPUT IDENTICAL (apart from the run timestamp line)" || echo "DIAGNOSIS OUTPUT DIFFERS"
```

```
DIAGNOSIS OUTPUT IDENTICAL (apart from the run timestamp line)
```

- About 15 minutes.

### H-3 — the export carries the swing read's outcomes

```
python -c "
import csv,collections
g=collections.defaultdict(list)
for x in csv.DictReader(open('backtest_data/swing-fallback-read/diagnosis-rows.csv',encoding='utf-8')): g[(x['Session'],x['Tier'])].append(float(x['MainNetEv']))
print(len(g[('NY','MEDIUM')]), round(sum(g[('NY','MEDIUM')])/len(g[('NY','MEDIUM')]),2))"
grep -E "^\| main window \| NY \| (none|swing|hvn) \| MEDIUM" docs/tier-order-stability-read-2026-09-15-output.md
```

```
1552 -4.19
| main window | NY | none | MEDIUM | 939 | 939 | -4.3 | -4.32 | -0.02 |
| main window | NY | swing | MEDIUM | 488 | 488 | -4.3 | -4.32 | -0.02 |
| main window | NY | hvn | MEDIUM | 125 | 125 | -2.7 | -2.72 | -0.02 |
```

- **Identity:** 939 + 488 + 125 = 1,552; (939 × −4.32 + 488 × −4.32 + 125 × −2.72) ÷ 1,552 = −4.19.

### H-4 — the self-checks

```
awk '/^## 0. Self-checks/,/^\| Session \| Rows/' docs/medium-tier-diagnosis-2026-09-17-output.md; grep -n "Parser check\|disagree (must be 0)" docs/medium-tier-diagnosis-2026-09-17-output.md
```

```
## 0. Self-checks

| Check | Result |
|---|---|
| Export rows | 8810 |
| Export rows without an attribution row (must be 0) | 0 |
| Attribution rows repaired (unquoted comma in the Class field) | 56 |
| Joined population rows | 8810 |
| Attribution InPopulation flag = 1 on joined rows | 8810 |
| Logged tier = tier of the logged effective score against Ceiling(MaxScore x pct) (mismatches, must be 0) | 0 |
| Rows whose re-score matches all six fields | 8749 |
| Rows with a verified VPFR profile | 8508 |
| Rows with MaxScore outside {20, 19, 15} | 0 |
| Rows with a missing main-window or carried outcome value | 0 |
| Matching rows where the trade side's breakdown points do not sum to the logged raw score (must be 0) | 0 |
| Era settings files read / magnitude groups that differ between eras (must be none) | 18 / none |

| Session | Rows | STRONG | MEDIUM | WEAK | H1 rows | H2 rows | Trading days |
748:- Verified rows where the recomputed label and the breakdown points disagree (must be 0): 0.
1109:- Parser check: matching trading-week sides where the funding points differ from the note's nominal AND the final raw score is above 1 (a clamp at 0 plus a Step 3b soften cannot explain these; must be 0): 0.
```

- **Load-bearing values:** the ledger 0 **and** the tier self-check 0. Without both, vote attribution and tiers could come from different rows.

### H-5 — the existing instrument modes are unchanged by the new mode

```
for pair in swing:swing-vs-fallback-target-read-2026-09-15-output.md census:medium-tier-bug-hunt-2026-09-16-census-output.md; do m=${pair%%:*}; d=${pair#*:}; dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --fetch aws_fetch/20260913-153704 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --mode $m --out backtest_data/swing-fallback-read/identity-$m.md > /dev/null; diff -q <(grep -v "Run at (UTC)" docs/$d) <(grep -v "Run at (UTC)" backtest_data/swing-fallback-read/identity-$m.md) > /dev/null && echo "$m: identical to docs/$d apart from the run timestamp" || echo "$m: DIFFERS from docs/$d"; done
```

```
swing: identical to docs/swing-vs-fallback-target-read-2026-09-15-output.md apart from the run timestamp
census: identical to docs/medium-tier-bug-hunt-2026-09-16-census-output.md apart from the run timestamp
```

### H-6 — no engine, settings, UI or manual file changed

```
git diff --stat 14e2e80 -- Core UI analysis settings.json docs/UserManual.md DeribitWsFeed.vb AnalysisLogger.vb DynamicNorms.vb MarketState.vb verify
```

```
(no output)
```

### E-1 — the 200-resample pilot runs that fixed the cause-set predicates

- The explanation-set predicates (output section 13a) and the four added cuts were chosen after two 200-resample pilot runs of the script, before the 10,000-resample run. The pilot outputs were scratch files and are not committed. The predicates are descriptive only; no label depends on them.

---

## 2. Decisions taken (auto-proceed, one line each)

- **Instrument split:** options (a) all statistics in VB inside `SwingFallbackRead`, (b) Python with numpy (not installed), (c) a VB export plus a Python standard-library analysis → **(c)**. The export reuses the swing read's `Walk` and `Result` unchanged, so no outcome is re-implemented. Not a truthfulness trade: same statistics, re-runnable, fixed seed.
- **Scores from the logged CSV, votes from the re-score attribution:** tiers must be what the engine logged; the breakdown exists only in the re-score. 61 non-matching rows kept, with a sensitivity section (no label changes).
- **`TOOL-2` handled in the reader, not fixed in `MediumTierRescore.vb`:** session 1's attribution file is pinned by MD5; changing the writer would change the pinned file. Queued as `Q-3`.
- **Vote lean = trade-side points minus other-side points:** a penalty on the other side helps the trade side, and the margin is what the verdict reads.
- **Applied mutation deltas from label points where the label carries only that mutation, nominal magnitudes elsewhere:** exact where exact is available; each row names its source in output section 5.
- **Label rule = the census rule, first match wins:** already reviewed in session 1; reusing it keeps the two reads comparable.
- **Rank 1 (the flat score) called a confirmed null, not CONFIRMED:** the label rule cannot confirm a zero; the read states the bound instead of stretching the label.
- **Fix classes raised for the null and the two CONFIRMED outcome findings, not for MEDIUM causes:** the brief asks for fix classes per confirmed cause, and none is MEDIUM-specific. Leaving the table empty would hide the one actionable result (`F-1`: do not re-cut).
- **No harness fixture added:** this session adds no logic the engine runs; the mirror and label fixtures from session 1 cover scoring code.
- **D-12 era segments are not split into halves:** the era is itself the split; halving eras of 1–3 weeks leaves nothing readable.
- **Output copied into `docs/` as `medium-tier-diagnosis-2026-09-17-output.md`:** session 1's convention for instrument outputs.

---

## 3. Decisions queued

### Q-1 — what to do with the tier ladder (trader; ⚠ reserved: scoring and sizing)

| Option | What changes | Measured basis |
|---|---|---|
| (a) `F-1` only: record that thresholds and the tier floor are not re-cut | Nothing | NY: one tier step is worth at most about +0.35 bps (slope CI upper bound) |
| (b) `F-2`: re-weight or gate votes by measured per-session value, via `tools/WhatIfRunner` split-half | Scoring weights | Only DISCOVERY-ONLY or H1-only vote effects in NY; ASIA OFI carried CONFIRMED |
| (c) `F-4`: treat all tiers as one class for Kelly sizing and the bridge tier until a score ranks outcomes | Sizing and payload | Rank 1 null |
| (d) First measure why success rate rises at the top score bins while net EV does not (target and stop distance by tier) | Nothing yet | Not measured |

- **Read (hypothesis): (d) first, then (a).** (d) is cheap, and it decides whether the ladder carries information that the target geometry spends. That changes whether (c) is right. ⚠ (a) alone is the cheaper option; I recommend it only after (d) because (d) records more.
- **Shares a root with `Q-2`:** both are vote-weight questions under one split-half test.

### Q-2 — the VPFR score vote in NY (trader; ⚠ reserved: scoring)

| Option | What changes |
|---|---|
| (a) Keep the vote as shipped | Nothing |
| (b) `F-3`: keep the long arm, test removing or reversing the short arm in NY | One vote arm, NY |
| (c) Retest after the POC-tier gate fix (session 1 decision D-1, ruled (a)) and the liquidation fix (`L-1`) ship, on new data | Nothing now |

- **Read (hypothesis): (c), then (b) if the LONG-only result replicates.** The finding is one of two outcome CONFIRMED labels in 445 readable comparisons (0.56 expected false). The trader rated volume profile core, so a replication on post-fix data is worth the wait. I have no read on the mechanism: the POC value is not in the attribution file.

### Q-3 — `TOOL-2`, the unquoted Class field (orchestrator; not reserved: offline tool)

- **Options:** (a) quote the field in `MediumTierRescore.vb` (changes the pinned MD5 on the next run; update the pin); (b) leave it; readers must rejoin.
- **Read: (a).** A CSV any reader must special-case is a trap; `medium_tier_diagnosis.py` asserts the field count, so a naive reader elsewhere would silently shift 56 rows.

---

## 4. Feedback on the brief

### What worked

- **"Sessions separate" and "H1 discovers, H2 confirms"** (`docs/medium-tier-diagnosis-brief-2026-09-16.md` §4). Without them the NY ROC and BBW/TTM results, and the ASIA gap, would read as causes.
- **`D-1` named its own failure outcome** ("If EV is not monotone in score, no tier cut-off can fix it. Name that outcome explicitly"). That turned the most important result from "nothing found" into a finding.
- **The orchestrator's carry-in "explanation sets, not first-match labels".** 75.7 % of NY MEDIUM rows sit in 3 or more candidate sets; any first-match attribution would have been arbitrary.

### What broke or was incomplete

- ⚠ **Highest value: the brief's premise is a gap that does not replicate.** `docs/medium-tier-diagnosis-brief-2026-09-16.md` §3 asks why MEDIUM underperforms; the evidence cited is an ordering of point estimates. **Test the gap under the same halves rule before asking for its causes.** Here it would have re-scoped `D-3` to `D-11` around `D-1`.
- ⚠ **The H1/H2 split date sits 18.6 h before the v66 deploy.** `D-12` (era edges) and the confirmation rule use nearly the same cut. Any future brief using these halves should say so, or split on a date away from an era edge.
- **`D-3` "which votes lift a signal into MEDIUM" degenerates.** With one-point votes, every agreeing vote lifts a row on the MEDIUM floor integer and none lifts a row above it. The agree / oppose / absent table answers the intended question better.
- **`D-5` asks for clamp binding, which the logs cannot give exactly.** The breakdown records applied points, so binding is exact only for labels that carry one mutation. A per-site counter would need an engine change (reserved).
- **`D-4` crossings are mostly NOT READABLE** outside Pass 2 upgrades, trend structure, Pass 2c, funding, squeeze, decel and the ADX penalty in NY.
- **`D-6` names the spread tail guard and the liquidation vote as score-range compression.** Both are penalties; they cannot shrink the reachable maximum. Only the Volume vote is a dead addable point.

### A constraint pair that nearly conflicted

- "Bootstrap whole trading days, 10,000 resamples" with "dozens of cuts" and no numpy: about 15 minutes per full run. **The escape hatch was a `--resamples` argument** for pilots, with point estimates unchanged. Say so in the next brief so the pilot and the pre-registration order are explicit.

---

## 5. What I did not verify

- Full list: the read section 5. The items that change a decision:
  - **Why success rate rises at the top score bins while net EV does not** (`Q-1` option (d)).
  - **The mechanism of the NY VPFR long finding** (`Q-2`); the POC value is not in the attribution.
  - **Era effects against half effects:** the split sits on the v66 edge.
  - **Exact clamp binding** for RSI divergence, OI × CVD and burst (nominal magnitudes).
- **Carried over without checking:** the swing read's population funnel and candle walk; session 1's re-score attribution as re-run by the orchestrator on commit `85e11d5`.
