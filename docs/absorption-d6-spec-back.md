# Absorption D-6 — spec-back against the D-table

**Date:** 2026-09-01 · **Seat:** Opus, effort **high** · **Status:** REVIEW ONLY. No code, no settings change, no source file touched.

**Reviews:** [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 D-table, row **D-6** only. Every other row is untouched by this packet.

**Trigger:** the trader asked whether D-6's missing measurement can be done. D-6 carries *"(no author recommendation — the author did not have this measurement)"*.

⚠ **ID COLLISION, READ BEFORE GREPPING.** Two unrelated things in `docs/` are called D6:

| Written | Where | What it is |
|---|---|---|
| **`D-6`** (hyphen) | [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 | **This packet.** A D-table row about `proximity_atr_frac` vs `band_atr_frac` |
| **`D6`** (no hyphen) | [`d6-migration-spec-back.md`](d6-migration-spec-back.md) | A 2026-07-14 **build name** — the eval placed-stop migration. Nothing to do with absorption |

**No companion summary document.** [`batch-review-packet-convention.md`](batch-review-packet-convention.md) requires a two-document pair when an implementer executes a multi-item batch. **No batch was run.** This is a review of one D-table row, so the packet stands alone.

---

## 0. ⭐ The finding that changes how the row reads

> ## D-6 IS THREE QUESTIONS WEARING ONE LABEL. ONE IS ALREADY ANSWERED. ONE IS ANSWERABLE FROM THE CODE WITH NO MEASUREMENT. ONE CANNOT BE MEASURED FROM ANY DATA THIS PROJECT HOLDS.
>
> | | The question inside D-6 | Verdict |
> |---|---|---|
> | **M1** | What does the 3× mismatch cost, in observed rows? | ✅ **Already measured** by [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md), and **reproduced independently in this pass** on a book two weeks longer |
> | **M2** | Is the mismatch intended? What does the wide shell buy? | ⭐ **Answerable now, from [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb). No measurement is needed.** The author searched for a number when the answer was a code path |
> | **M3** | Is 3× the right ratio? Would another pair do better? | ⛔ **NOT measurable. No stored data can answer it, and none can be made to** |
>
> ⛔ **A second finding, and it points the row somewhere else.** D-6's right-hand column calls the geometry *"the largest single leak in the funnel."* **The measurements in this packet do not support that**, and the evidence against it is already inside [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3 box (b).

### 0.1 M2 — the design reason is in the code

Read this session in [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb):

| Line | Statement | What it establishes |
|---|---|---|
| `:251` | `gateOpen = If(side.IsAbove, lvl - bestAsk <= prox, bestBid - lvl <= prox)` | The episode **arms on proximity** — `proximity_atr_frac`, 0.30 |
| `:292` | `' Episode opens on the first in-proximity snapshot.` | The arming instant, stated by the tracker's own author |
| `:295` | `side.SizeStart = bandSize` | `bandSize` sums the ladder over `[bandLo, bandHi]` — the **band**, `band_atr_frac`, 0.10 |

⭐ **The outer shell decides WHEN to sample. The inner shell decides WHAT is sampled.** That is arm-early / measure-tight, and it has a purpose: it captures the band's resting depth **before price arrives**, so `SizeStart − SizeMin` measures depletion against a pre-test baseline.

- `absorbRatio = aggrUsd / max(sizeStart − sizeMin, floor)`.
- **Collapsing proximity to 0.10 would sample `SizeStart` after price is already at the level** — after early depletion has happened. `SizeStart` falls, so `SizeStart − SizeMin` falls, so the denominator falls, so the ratio rises. **More flags, spuriously.**
- ⚠ **So "a 3× mismatch" is a misnomer.** The two fractions are not two attempts at one quantity. They are a sampling window and a measurement shell.
- ⚠ **One qualifier** from [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:42`: the effective outer shell is `min(proximity, visible ladder span)`. **0.30 is a ceiling, not always the operative distance.**

⭐ **D-6's second branch — *"or state why a 3× mismatch is intended"* — can therefore be answered today, as a documentation ruling, with no build and no wait.**

### 0.2 The "largest single leak" claim does not hold

Measured this pass on **both books**, weekday, UTC, since v61 — the method in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §2. Share / press rate in each cell:

| `abs(level − price) / ATR` | check AWS | **mine AWS** | check LOCAL | **mine LOCAL** |
|---|---:|---:|---:|---:|
| ≤ 0.10 — inside the band | 29.9 % / 30.3 % | **32.8 % / 34.2 %** | 29.7 % / 31.7 % | **30.4 % / 32.2 %** |
| (0.10, 0.30] — the annulus | 47.0 % / 1.6 % | **46.0 % / 2.8 %** | 45.0 % / 2.7 % | **44.7 % / 2.6 %** |
| > 0.30 | 23.1 % / 0.8 % | **21.3 % / 0.8 %** | 25.3 % / 0.7 % | **25.0 % / 0.5 %** |
| unpressed active rows outside the band | 76.8 % | **75.3 %** | 77.3 % | **76.8 %** |

**Population sizes, this pass:** AWS 23 643 weekday rows / 3 355 active / 425 pressed. LOCAL 5 498 / 777 / 86. ⚠ **The books are NOT pooled** — [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §2 forbids it, and the two boxes run different session coverage. **Active / all rows: 14.19 % AWS, 14.13 % LOCAL.** *(The 2026-08-19 check reports the pair 14.68 % / 14.71 % across its two books but does not say which book is which, so no per-book comparison is offered here.)*

✅ **The 2026-08-19 geometry table reproduces on BOTH books, by a different hand, on books that run longer than the check's.** That is now a third independent measurement of the same quantity, and it is the strongest thing in this packet.

⚠ **The LOCAL book ends 2026-08-20** — the local box stopped capturing when AWS became the sole capturer. It is a shorter window than AWS, not a stale copy of the same window.

⛔ **But the inference drawn from it does not follow:**

- The annulus (0.10, 0.30] holds **46 %** of active rows at a **2.8 %** press rate. **Deleting it recovers almost nothing — there is almost no pressing there to recover.**
- The recoverable mass sits **inside** the band, where the press rate is only **34.2 %**. **Two-thirds of in-band rows record nothing.** No change to either shell can touch that.
- [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3 box (b) already names the cause: the engine **"counts 31 % of what it should — the side is not `Active` when the flow arrives."**

⛔ **The ≈ 77 % is a share of UNPRESSED ACTIVE ROWS. It is not a share of RECOVERABLE PRESSING.** D-6 reads the first as though it were the second. ⚠ **That is the same denominator error the 2026-08-19 check itself caught and recorded in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3 box (c).**

### 0.3 ⛔ A substitute for `AbsorptionEpisodeSec` was tested and it FAILED

[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5 calls `AbsorptionEpisodeSec` *"the single most valuable missing number"* and says it has no substitute. **I tested the obvious substitute and it does not work.**

- **Method:** group adjacent active rows sharing one `AbsorptionLevel` into an episode. Read duration from the first and last timestamps.
- **Result: 89.3 % of reconstructed episodes are a SINGLE CSV row (AWS, 2 975 episodes). Median duration 0 seconds.** ✅ **Replicated on the LOCAL book: 89.6 %, 685 episodes.**
- ⛔ **That is a sampling artefact, not a measurement.** The tracker folds `UpdateBook` snapshots at ~100 ms. `analysis_log.csv` samples at the auto-run cadence, minutes apart. **The book cannot see episode life.**

> ## ⚠ CORRECTED 2026-09-01 BY ITS OWN AUTHOR — I NAMED ONE CAUSE AND THERE ARE TWO.
>
> **"A sampling artefact" asserts a cause. It is one of two live candidates, and I did not separate them.**
>
> **The second: episodes are genuinely being KILLED at run boundaries.** [`../Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:265` — `If side.Active AndAlso lvl <> side.LevelPrice Then side.CloseEpisode()`. **Every level re-map discards the episode**, and with it `PressSum`, `SizeStart`, `SizeMin`, `PullLB` and `PostLB`.
>
> **Measured 2026-09-01, AWS book, weekday, since v61 — consecutive-run change rates:**
>
> | Source | Changed between consecutive runs |
> |---|---:|
> | `LastSwingHigh5m` | 3.7 % |
> | `LastSwingLow5m` | 3.9 % |
> | **`VPFRNearestHvnAbove`** | ⚠ **30.1 %** |
> | **`VPFRNearestHvnBelow`** | ⚠ **29.6 %** |
> | **`AbsorptionLevel`, adjacent ACTIVE row pairs** | ⛔ **50.4 %** (386 of 766) |
>
> ⭐ **Swings are stable. HVNs churn, and `NearestAbove`/`NearestBelow` pick from both.** So the carried level moves at the run boundary far more often than the swing structure does.
>
> ✅ **The section's CONCLUSION survives untouched** — the book still cannot resolve episode life, and `AbsorptionEpisodeSec` still has no substitute. **What does not survive is my attribution.** ⚠ **The two causes have opposite implications: sampling means the episodes are fine and we cannot see them; re-map churn means the episodes are being destroyed.**
>
> ⭐ **And this is a live candidate mechanism for D-6d.** A re-map fires `CloseEpisode()`, `Active` goes False, and [`../Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:159` — `If Not side.Active Then Return` — **drops every trade until a book snapshot re-arms the side.** That is precisely §4.3 box (b)'s *"the side is not `Active` when the flow arrives."* ⛔ **HYPOTHESIS, NOT A DIAGNOSIS.** It is not shown that the 50 missed rows coincide with re-maps, nor that an HVN was the selected level in those cases.
- ✅ **So §5's "no substitute" claim STANDS, and it now stands on a test rather than on an assumption.**
- ⛔ **It also caps this packet's own episode-level numbers.** Any statement of the form "N % of armed episodes never reached the band" is an **upper bound**, because an episode can dip into the band between two polls unseen. **No such number is offered as evidence anywhere in this packet.**

---

## 1. Ranked verification handles

⭐ **If you run only one, run H1.** It carries §0.2 entirely, and §0.2 is the finding that moves the row.

**Two books, and every handle runs on both.** Weekday, UTC, since 2026-07-23. Weekday is computed inline by Zeller's congruence — **validated against `date -d` on all 36 dates in the AWS book, 0 mismatches.**

| Book | Path | Coverage |
|---|---|---|
| **AWS** | `AWS-copybacks/aws-copyback-2026-08-28/aws_fetch/20260828-153838/analysis_log.csv` | 2026-07-22 → 2026-08-28 |
| **LOCAL** | `bin/Debug/net8.0-windows/analysis_log.csv` | 2026-07-03 → 2026-08-20 |

⚠ **Substitute the LOCAL path into H1 and H5 to check the second book.** Both are the same 111-field schema, verified. **Expected LOCAL results: H1 `active=777` with press rates 32.2 % / 2.6 % / 0.5 %; H5 `episodes=685 single-row=614 (89.6%)`.**

### H1 — the geometry table and the press rates ⭐

```bash
awk -F',' 'function dow(ds, y,m,d,K,J,h){y=substr(ds,1,4)+0;m=substr(ds,6,2)+0;d=substr(ds,9,2)+0;if(m<3){m+=12;y--}K=y%100;J=int(y/100);h=(d+int(13*(m+1)/5)+K+int(K/4)+int(J/4)+5*J)%7;return h} FNR==1{next}{ds=substr($1,1,10); if(ds<"2026-07-23")next; if(dow(ds)<2)next; if($102==""||$65+0<=0||$2+0<=0)next; a++; f=($102-$2); if(f<0)f=-f; f/=$65; p=($104+0>0); if(f<=0.10){b1++;p1+=p} else if(f<=0.30){b2++;p2+=p} else {b3++;p3+=p}} END{printf "active=%d\n<=0.10  n=%4d %5.1f%%  press %5.1f%%\n(.1,.3] n=%4d %5.1f%%  press %5.1f%%\n>0.30   n=%4d %5.1f%%  press %5.1f%%\n", a,b1,100*b1/a,100*p1/b1,b2,100*b2/a,100*p2/b2,b3,100*b3/a,100*p3/b3}' AWS-copybacks/aws-copyback-2026-08-28/aws_fetch/20260828-153838/analysis_log.csv
```

**Must print `active=3355`, and the load-bearing pair is the two press rates: `<=0.10` at 34.2 % against `(.1,.3]` at 2.8 %.** ⚠ **The shares alone are not enough** — they show the annulus is large, which is what D-6 already says. **The press rates are what show it is empty.**

**Arithmetic identity:** `1099 + 1542 + 714 = 3355`. A silent filter error breaks this sum.

### H2 — the arm-early / measure-tight pair, in executable code

```bash
grep -n 'gateOpen = If(side.IsAbove' Core/LevelAbsorptionTracker.vb; grep -n 'side.SizeStart = bandSize' Core/LevelAbsorptionTracker.vb
```

**Must print one hit each, at `:251` and `:295`.** ⚠ **Both are executable statements, not comments** — per the 2026-08-11 ruling that a handle must test the property and not a string that mentions it. **Do not substitute a `grep -c` on the word `proximity`**; the file's header comments discuss it at length, so the count would be meaningless.

### H3 — `SizeStart` / `SizeMin` are not in the book

```bash
head -1 AWS-copybacks/aws-copyback-2026-08-28/aws_fetch/20260828-153838/analysis_log.csv | tr ',' '\n' | grep '^Absorption'
```

**Must print exactly five lines:** `AbsorptionSignal`, `AbsorptionLevel`, `AbsorptionRatio`, `AbsorptionAggrUsd`, `AbsorptionPullFrac`. **This is the blocker for M3.** It asserts the shipped column set, rather than counting a name that comments could also carry.

### H4 — no order-book depth is stored anywhere

```bash
grep -n 'Public Const HeaderLine' Core/TradeStoreWriter.vb
```

**Must print `Timestamp,Price,Amount,Direction,Liquidation,TradeId,TradeSeq` — seven fields, none of them depth.** The trade store holds prints, not the ladder. ⚠ **Order-book capture is explicitly out of scope** per the v64 entry in the tracked `settings.json` change log. **Together with H3 this closes M3: the counterfactual needs ladder depth at an arming instant nobody recorded.**

### H5 — the failed episode-duration substitute

```bash
awk -F',' 'function dow(ds, y,m,d,K,J,h){y=substr(ds,1,4)+0;m=substr(ds,6,2)+0;d=substr(ds,9,2)+0;if(m<3){m+=12;y--}K=y%100;J=int(y/100);h=(d+int(13*(m+1)/5)+K+int(K/4)+int(J/4)+5*J)%7;return h} FNR==1{next}{ds=substr($1,1,10); act=($102!=""&&$65+0>0&&$2+0>0); if(!act){prev="";next} keep=(ds>="2026-07-23" && dow(ds)>=2); if($102!=prev){ if(n>0&&k){ep++; if(n==1)one++} ; n=0; k=keep } n++; prev=$102} END{ if(n>0&&k){ep++; if(n==1)one++}; printf "episodes=%d  single-row=%d (%.1f%%)\n", ep, one, 100*one/ep }' AWS-copybacks/aws-copyback-2026-08-28/aws_fetch/20260828-153838/analysis_log.csv
```

**Must print `episodes=2975  single-row=2657 (89.3%)`.** ⚠ **A 15-minute inter-row gap rule was also tried and split nothing** — the two reconstructions agree to the row, so the simpler one above is the handle.

---

## 2. Decisions queued, with my read

⚠ **D-1 through D-5 in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 are NOT touched by this packet.** Only D-6 is.

⭐ **Flagging a shared root: D-6a and D-6b below both come from reading the same code path, so rule them together.** Ruling one without the other can produce a document that says the shells are intended and also says their difference is the biggest problem.

### D-6a — how to answer the "reconcile … or state why" branch

> ✅ **RULED 2026-09-01 — option a.** The pair is INTENDED. **Deliverable written up at [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3a**, and the D-6 row now points at it.

| Option | |
|---|---|
| ⭐ **a** | **Rule the pair INTENDED and document the mechanism** — arm-early / measure-tight, citing [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) `:251` and `:295`. No code, no settings, no wait |
| b | Rule it intended but leave it undocumented |
| c | Rule it a defect and reconcile the two fractions to one value |

**My read — a, and I hold it firmly.** The code shows two different jobs, not one job done twice. ⚠ **Option c is the dangerous one: reconciling to 0.10 truncates the `SizeStart` baseline and biases `absorbRatio` UP**, which manufactures flags rather than finding them. **That is the shape [`trader-profile.md`](trader-profile.md) rejects — a change that raises the signal count by weakening its denominator.**

### D-6b — the "largest single leak" claim

> ✅ **RULED 2026-09-01 — option a. THE CLAIM IS WITHDRAWN.** ⚠ **My read was a hypothesis and it was not attacked before it was ruled.** The dispute is a denominator argument on a table both sides agree about. **If the denominator reading is wrong, this ruling inverts and the geometry becomes the primary target** — that risk is now carried, not resolved.

| Option | |
|---|---|
| ⭐ **a** | **Withdraw it and re-ground the row** on §0.2 above — the annulus presses at 2.8 %, so there is little there to recover |
| b | Keep it and note the dispute |

**My read — a, and it is a hypothesis I would like attacked.** The claim and my rebuttal use the same table, so we are not disagreeing about data. We are disagreeing about which denominator the ≈ 77 % belongs to. **If my reading of the denominator is wrong, D-6b inverts and the geometry does become the primary target.**

### D-6c — the sizing question

> ⛔ **OPEN 2026-09-01 — option a. Parked behind the §5 instrumentation**, which is now specced at [`absorption-instrumentation-spec.md`](absorption-instrumentation-spec.md). **This is the only part of D-6 the "wait for §5" condition ever covered.**

| Option | |
|---|---|
| ⭐ **a** | **Park behind the [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5 instrumentation.** Say plainly in the row that it is unmeasurable today |
| b | Move a fraction on judgement, with no measurement |

**My read — a.** ⛔ **Option b is exactly the threshold re-tune Path B was authorised to stop.**

**Scoping, supplied without recommending it.** The narrowest change that would make D-6c measurable later: add `AbsorptionSizeStart` and `AbsorptionSizeMin` to the CSV. Two `InvOpt` calls at [`AnalysisLogger.vb`](../AnalysisLogger.vb) `:295-299`, two names in the header at `:108`, two fields carried on the result type. **It is a strict subset of the §5 instrumentation and rides along with it at no extra cost.** ⚠ **A CSV column addition is a rotation — [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5 already warns that `analysis_log.csv` has a different reader from the trade store, and that backward compatibility must be re-verified rather than assumed.**

### D-6d — the item that is not in the D-table at all

> ⚠ **NOT RULED 2026-09-01, and deliberately so — I offered no read and none was invented.** ⛔ **It is now the largest unaddressed item in the whole proposal.**

⭐ **[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3 box (b)'s counting gap has no D-table row.** The engine logs pressing on 22 rows where its own band-and-window predicate admits flow on 72. **The side is not `Active` when the flow arrives.**

- It is **larger than the geometry and larger than `window_sec`**, by that box's own numbers.
- **13 of the 50 missed rows carried ≥ 20 000 USD**, against 19 rows clearing `min_aggr_usd` in the entire 17-day book.
- ⛔ **I have no read on the fix.** Diagnosing it means reading the `SeedAsync` reset discipline and both fold paths, which I did not do. **This packet raises it as a missing row, not as a solved problem.**

**Model + effort for whoever takes D-6d: Opus, effort high.**

- **Why that tier.** It is a state-machine defect across a dual-fed path under one `MarketState` lock. [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §0 already names those fold paths as where a change slips.
- **Where it will slip.** Widening or re-timing the trade-side fold without matching the book-side trajectory produces a ratio whose numerator and denominator cover different spans. **That reads as a stronger signal and is pure artefact.**
- **Escalation trigger.** If the flagged rate rises while the ratio distribution shifts left, stop. **That is the artefact, not a fix.**

---

## 3. Spec-back proper — feedback on the proposal

### 3.1 What the proposal got right, specifically

- ⭐ **The quote-and-label convention did real work here.** Every correction in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) sits in place, with the superseded text kept underneath. **I could see what the 2026-08-14 author believed and what the 2026-08-19 check changed, without opening a third document.** D-6 only reads as a contradiction because both layers are visible.
- ⭐ **"Measure it, do not assume it"** in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3's title is the reason this packet exists. **The section that named its own ignorance is the section that got answered.**
- ✅ **Reporting per book and never pooling** is what let me validate my parse against the published AWS figures. **A pooled table would have hidden a parse error inside a blend.**

### 3.2 Which assumptions broke

- ⛔ **"the author did not have this measurement" framed D-6 as a measurement problem. It is not one.** M2's answer was in the tracker the whole time. ⚠ **The proposal reached for the book because the book is where the previous three passes found their answers. Nobody re-read the code path that sets `SizeStart`.**
- ⛔ **D-6 conflates three questions with three different verdicts.** As one row it cannot be ticked, because any tick is right about one part and wrong about another.
- ⚠ **[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §5's "no substitute" claim was correct but untested.** It is now tested — see §0.3 above. **The claim survives and is stronger for it.**

### 3.3 Where the proposal was narrower than its own words

⚠ **[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §2 labels its rows "Proximity-ACTIVE episodes" while its own method line says "Episode row = the tracker produced a level."** **Those are ROWS, not episodes** — the file makes both readings available, and its tables use the row reading.

- The numbers are correct under the row reading, and the check's `active / rows = 14.71 %` confirms it.
- ⛔ **But the word invites an episode-level inference the data does not support**, and §0.3 above shows why: the CSV cannot resolve an episode at all.
- **Suggested wording: "proximity-ACTIVE rows".** One word, and it closes the trap.

### 3.4 A constraint pair that nearly conflicted

⚠ **[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §0 forbids opening an implementer session before §6 is ticked. D-6 cannot be ticked without reading the tracker source.** Read strictly together, the row could not be resolved at all.

**The hatch: reading source to answer a D-table question is not an implementer session.** No file was edited and no build was run in this pass. **Worth writing into the next proposal explicitly**, so the next seat does not stall on the same pair.

---

## 4. What I did not verify, and cannot

- ✅ ~~**AWS book only.**~~ **CLOSED 2026-09-01 — the LOCAL book was run and every figure in §0.2 and §0.3 now carries both boxes.** The geometry table and the single-row episode share both replicate. **This limitation no longer stands.**
- ⚠ **The two books overlap in time but are not independent samples of the same thing.** They are two boxes watching one venue, with different session coverage and different uptime. **Agreement between them rules out a parse error and a box-specific artefact. It does not rule out a venue-wide or engine-wide one**, because both books come from the same engine build.
- ⛔ **Nothing live.** No run was performed and no box was touched.
- ⛔ **The `pullFrac` point mass and the floor-multiple enrichment** in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §3.2 and §4.2 — **not re-checked. Outside D-6's scope, and I carried them over unverified.**
- ⛔ **`Core/LevelAbsorptionTracker.vb`'s fold paths.** I read the arming and band-sampling path only. **I did not read `SeedAsync`, the `AppendTrade` fold, or the reset discipline** — which is exactly where D-6d lives, and is why I offer no read on it.
- ⛔ **Whether the poll instants are independent of price geometry.** H1's marginal distribution is unbiased **only if** the auto-run timer does not correlate with where price sits relative to a level. **I believe it does not, because the timer is fixed-interval and the levels move — but I did not test it, and every share in H1 rests on it.**
- ⛔ **The tape.** Not touched. The 2026-08-11 17:18 era split does not apply here, because `analysis_log.csv` is unaffected by the trade-store eras — **that is the proposal's own statement in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §7, carried over, not re-verified.**
