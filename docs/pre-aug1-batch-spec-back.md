# Pre-Aug-1 batch — spec-back to the Fable seat

**From:** the Opus orchestrator that executed `docs/pre-aug1-opus-batch-2026-07-31.md` on 2026-07-30.
**To:** the Fable coordinator who authored it, for the final double-check.

**This is not the summary.** `docs/pre-aug1-batch-summary.md` is the outcome record — per-item result, raw tables, commit hashes — and the trader relays that. This document is the **review packet**: what to verify and how to verify it cheaply, what decisions are queued for your ruling, and where the batch spec's own assumptions held or broke. Nothing here is repeated from the summary except by pointer.

Ten local commits, `ff1d34c` → `bd26c0b`, none pushed. Settings still **v63**. Next free fixture family **A47**.

---

## 1. Verification handles — the cheap checks

Ranked by how much of the batch each one covers. None needs a re-run.

| # | Claim to check | How, in one step |
|---|---|---|
| 1 | **The A45a fixture is a regression test, not a tautology** | Read sub-check **(iv)** in `A45a_VwapSessionAnchorParameterization` (`verify/ordercheck/Program.vb`). It asserts the *default* call on a historical set falls through to the whole-list fallback and **provably differs** from the correct anchored answer. Without (iv), a fixture that only checks "default ≡ UtcNow" passes even if someone re-hardwires the anchor. (i)–(iii) are the ordinary pins. |
| 2 | **Lane A's window fix is exact, not approximately exact** | `grep VWAPSessionCandles` in the §10 table of `docs/backtest-overlap-validation-2026-07-30.md`: **100.00 %, mean \|Δ\| 0, max \|Δ\| 0** on 840 rows. Mean-and-max-both-zero is the load-bearing number; the match-rate alone would tolerate off-by-one. |
| 3 | **The pooled snapshot's dedup is arithmetically closed** | 5,001 AWS kept + 2,078 AWS dropped = **7,079** = AWS raw ✓. 8,338 local + 5,001 = **13,339** = pooled ✓. Both identities in `docs/pooled-report-runner-spec-back.md` §4.1. If either failed, rows were lost or double-counted. |
| 4 | **Lane C's counterfactual is measurement, not inference** | It reads real closed candles from `backtest_data/candles_{1,3}m_2026-07.csv` with the engine's own formula (`vol[i] / mean(vol[i−8..i])` — the `CalcVolumeSMA` window, current bar included). Reproduction recipe is in §6 of the investigation. The gap it measures is 65–800× in the median; no sampling artefact reaches that. |
| 5 | **Lane A changed nothing live** | `git show ff1d34c -- Core/Indicators_Volatility.vb` — three signatures gained a trailing `Optional nowUtc As DateTime? = Nothing`, and the only behavioural line is `If(nowUtc.HasValue, nowUtc.Value, DateTime.UtcNow)`. Both existing call sites pass `session2Hour`/`session2Minute` positionally, so appending last cannot shift an argument. |
| 6 | **Nothing else in `Core/` was touched** | `git diff origin/master..HEAD --stat -- Core/` returns exactly one file. The batch's "item A's one parameter ONLY" constraint is checkable in one command. |

---

## 2. Decisions queued for you

Five. I have a read on two and say so; none is mine to make.

### D1 — Does the VWAP-sensitive study clearance open?

§7.5's conditional clearance withheld the VWAP class "until the anchor fix lands and re-validates." It landed and re-validated, and the result splits: **the window is exact (100.00 %, max \|Δ\| 0), the values are not (53–56 %, still under §9.6's <60 % "do not use" cut).** Funding is untouched and unchanged at 22.02 %.

That is not the binary §7.5 anticipated. Options as I see them: clear the class outright / keep it withheld on the value match / clear it for studies that consume the *window* (session membership, `VWAPSessionCandles`) but not the *level*. **I have no read here** — the clearance criterion was yours and only you know whether it was about the anchor or about the agreement number.

> **ANSWERED 2026-07-31 (incoming orchestrator seat) — option 1, clear the class outright, on the VWAP axis.** The question dissolved rather than being decided: the 53–56 % values were produced by a **~64,000× unit error** in the synthesizer's forming stub, not by the anchor and not by a tolerance. Post-fix, same 840 rows, only the stub arithmetic changed: **VWAP 100.00 %**, σ bands 99.76–100.00 %. There is no values/window split left to arbitrate. The `~1.3 bps` noise floor recorded with the interim PARTIAL ruling is **withdrawn**, and fine dev-threshold sweeps (< 5 bps) are **cleared** — their withholding was gated on volume fidelity, and volume was the defect.
>
> **The boundary that survives:** `VolumeRatio` at **65.00 %** keeps volume-**magnitude** studies *advisory only*. That is the D3 forming-bar effect, not the unit bug — `VolumeRatio` **is** the partial terminal bar, while VWAP merely contains it among 240 bars. Funding (22.02 %) and ATR/ADX/RSI are untouched by any of this.
>
> **Why this sat undone:** [`fable-handover-2026-07-31.md`](fable-handover-2026-07-31.md) §5 ranked it #2 of three items worth the Friday budget and called it "one line, given §1." The budget went elsewhere. Canonical record: [`backtest-overlap-validation-2026-07-30.md`](backtest-overlap-validation-2026-07-30.md) §10.4.

### D2 — Is the 53–56 % residual a code problem or a tolerance problem?

**I do have a read, offered as hypothesis.** With the window provably exact the residual cannot be an anchor error; it is inside a correct window. VWAP is a volume-weighted mean over up to 240 bars whose last bar is the §7.1 forming stub, carrying whatever printed in a 2-second slice. The NumTight tolerance for this class is `max(0.01, 0.01 % × |live|)` ≈ **$6.4** at BTC 64 k — a single near-zero-volume terminal bar can move a 240-bar volume-weighted mean past that.

So my read is that **the tolerance class may be the mis-set thing, not the code** — NumTight was designed for point-in-time candle-derived values, and a long-window accumulator is a different animal. If that's right, chasing the code would be chasing a correctly-computed number against the wrong yardstick.

I did not act on it. It is a spec-first question and it interacts with D3.

### D3 — Does lane C's finding get a slot, and at what priority?

The investigation is deliberately option-neutral. What it establishes: the behaviour is **specified** (v44 §3 names "closed bars only" a non-goal in those words), and the consequence is that **the Volume signal can vote on ≤ 2.01 % of the book against ~20 % on the same instrument's closed bars**, with the trader's own 3× breakout rule firing 10–40× less often than closed bars would produce.

The part that makes this more than a display curiosity: the volume vote is also a **Pass-2 cross-confirmation input**, so its silence suppresses upgrades elsewhere in the pipeline. It is not neutral-by-absence the way a disabled indicator would be.

Four options are laid out with their continuity cost. The one worth your attention if you want a middle path: the narrowest useful version of Option B is **one function** — `CalcVolumeSMA` excluding the last bar plus `CurrentVolume` reading `candles(Count − 2)` would move VolumeRatio onto the closed-bar distribution and touch nothing else. Its blast radius is knowable in advance from the §2 indicator table. That is scoping information, not a recommendation.

**Note D2 and D3 share a root.** Both are the forming stub seen from different sides — D2 is the stub inside a 240-bar accumulator, D3 is the stub as a 1-bar volume reading. A ruling on one may constrain the other.

### D4 — Does lane A get a §15 row in `DeribitIndicatorProject.md`?

CLAUDE.md requires a §15 entry for commits that change engine behaviour. Lane A touched `Core/` with **zero** behaviour change and no settings bump, so by that rule it doesn't qualify — but there is precedent for settings-untouched §15 rows (the W6-4 ceiling-audit tool has one, keyed on no version).

I left it out to respect the batch's tight scope. **If you want one it is a one-line addition** and I'd suggest it keyed as "§7.5 VWAP session anchor (settings-untouched)" to match the W6-4 precedent.

### D5 — The lane-E reports carry their own flags; do you want them read as a guard-rail matter?

Pointing, not interpreting — item E fences interpretation to the Friday session and I drew nothing. Two structural facts about the **output** that affect how the tables are read:

- The full-book runs flag **⚠ DIVERGENT** on every cell of both grids; the `--from 2026-07-08` runs flag none.
- The geometry grid's winner **differs by span** — full-book picks `(1, 1)`, `--from 2026-07-08` picks `(0, 0)`, which is the live baseline.

The runner's own guard-rail §2 says to trust the holdout over the selection half and to treat single-cell wins on a swept grid with suspicion. Whether that changes the Friday agenda is yours.

---

## 3. Spec-back proper — where the batch spec held, and where it didn't

This is the part that is feedback on `pre-aug1-opus-batch-2026-07-31.md` itself.

### 3.1 What the spec got right, specifically

- **Item A was exact.** Scope, the fixture requirement, the re-validation instruction and the settings fence were all unambiguous, and "expect the ~44 % family to recover … **report, don't chase**" was the correct hedge — it turned a missed prediction into a reportable result instead of an invitation to keep working. That phrasing did real work; without it I would have been in ambiguous territory when the recovery came in at 56 % instead of 99 %.
- **Item B leaving the host open with "orchestrator's call, state it"** was the right shape. The decision needed the codebase in front of it (BacktestRunner already linking `DeribitOhlcFetcher` is what made the verb four lines of `.vbproj`), and forcing the choice in the spec would have got it wrong in either direction.
- **Item D's "if the trader already ran it, skip" hedge** was worth including. It hadn't been run — `minTierTip` was still a method-local — so the lane stayed in, but the hedge cost nothing and would have saved a wasted lane.
- **"STOP that lane, record the finding, move on"** got used exactly once (the `BacktestProgram.vb` format-placeholder leak) and worked. Having the rule pre-stated meant no judgment call at the moment of finding it.

### 3.2 Where an assumption didn't hold — item C, sub-question 3

**This is the one piece of spec feedback that matters.** Item C asked me to:

> quantify from the live CSV: VolumeRatio distribution **on on-close rows vs backstop/interval rows** (the 0.0002-class fingerprint)

**The live CSV cannot answer that as written.** There is no `TriggerMode` column — the field exists in `settings.json` and rides the bridge payload, but `AnalysisLogger` never wrote it. On-close fires and backstop fires are not separable by column in any existing row.

What I substituted, and why each was necessary:

1. **A bar-phase proxy** — seconds between the row timestamp and its execution-resolution boundary. It works, but imperfectly, and the imperfection is asymmetric: the live backstop is 60 s and the NY bar is also 60 s, so in NY the two classes nearly coincide and only the res-3 sessions separate them cleanly.
2. **A closed-bar counterfactual** from the historical store — which turned out to be the stronger instrument anyway, because it sidesteps the roll/backstop question entirely and answers the underlying one directly.

The consequence you should know about: **the ATR and ROC cross-band numbers in that report are inconclusive by construction** and are labelled as such rather than quietly presented. Late-phase rows are enriched with backstop fires from quiet or feed-degraded periods, so those columns mix a stub effect with a selection effect. Only the VolumeRatio gradient is used as mechanism evidence, because there the mechanism predicts both direction and magnitude.

**Option D in the investigation exists solely to close this** — add `TriggerMode` to the CSV and the question becomes answerable directly. It is listed without recommendation, but it is the cheapest of the four and it is the only one that makes the *next* investigation better rather than changing behaviour.

### 3.3 Where the spec was narrower than its own words — item B's fixture

Item B asked for:

> Fixture: report generated from a small fixture CSV contains the §2 matrix + §9 band-ladder sections.

Read literally that is an **end-to-end run of the verb**, which requires a live Deribit OHLC fetch — not something a deterministic harness fixture can hold. A46a drives the real `Load → PopulateForwardBars → FailureRateMatrix.Compute → BandLadder.Compute → MarkdownReportWriter` chain from a real CSV on disk and supplies the OHLC map synthetically.

So **the fixture is narrower than the sentence**: it covers everything except the network hop. That hop is the pre-existing shared `DeribitOhlcFetcher` path, unchanged by this lane and exercised for real by the RUN — but A46a alone would not catch a regression in it. Stated in the fixture's own comment, in the commit, and in the spec-back, and repeated here so it isn't discovered later.

### 3.4 A constraint pair that could have conflicted, and didn't

Worth recording for whoever writes the next batch of this shape.

The standing constraints held both "**Gate per lane: GATE PASSED**" and "**No settings.json version bumps unless an item says so**", while item A authorized an engine `.vb` edit. Those three together look like a trap: the verify-gate's version-bump guard fires on any `Core/` change without a settings bump.

It isn't one, for two reasons worth knowing: the guard is **WARN-only, never FAIL** (`tools/checks/verify-gate.ps1`, the `Warn` branch), and there is an escape hatch — the `[no-engine-change]` commit token, whose established meaning is "engine path touched, no config keys, settings unchanged" (`5dc9646`, the offline-matrix migration). I used the token in `ff1d34c` and the gate reports `OK engine path changed but [no-engine-change] token present`.

Had the guard been FAIL-level the batch would have deadlocked on lane A. A future batch authorizing an engine edit under a no-bump constraint should say which token to use, rather than leaving it to be discovered.

### 3.5 Two judgment calls I made that the spec didn't cover

Both are in the summary's §7; restated here because they are spec gaps rather than outcomes.

1. **Committed the two lane-E overlays** (`9273416`). Item E called the existing overlays "format examples" and said nothing about committing new ones. The overlays README states its own purpose — recipes are committed so a study re-read reproduces "without depending on session-scratchpad files" — and the Friday session interprets these grids, so it will want to re-run them. Two 1-line JSONs and a README entry. Reversible in one revert if you disagree.
2. **No §15 row for lane A** — D4 above.

---

## 4. What I did not verify, and cannot

Stated plainly so nothing is assumed covered.

- **Lane D is not visually verified.** I changed a ToolTip's lifetime and did not hover it. Launching the app locally would append collector rows (the v57 stomp lesson), so this stays the trader's test gate — same disposition as the v62 min-net-move UI row, which is also still unverified.
- **GC lifetime is not harness-observable**, so lane D has no fixture by nature, not by omission. The evidence that it was a real defect is the `c508d93` precedent, where the identical pattern was found and fixed on the min-net-move row.
- **A46a does not cover the network hop** — §3.3.
- **The ATR/ROC columns in lane C are inconclusive** — §3.2.
- **I ran no Debug build at any point**, per the collector stomp rule. Every build in this batch was Release.

---

## 5. If you want one thing checked first

**Read A45a sub-check (iv)**, then the `VWAPSessionCandles` row in §10.

Those two together establish that lane A's engine edit is correct, complete and protected against silent regression — which is the only irreversible thing in the batch. Everything else is a document, a tool verb, a tooltip, or a study run, and all four are cheap to undo.

---

## 6. Fable double-check — VERDICT + the five rulings (2026-07-31)

**Batch APPROVED.** All six §1 verification handles checked and pass (A45a(iv) is a genuine regression trap; the lane-A diff is the one optional parameter; exactly one `Core/` file in the whole range; dedup identities close; the §10 |Δ| stats verified at source). Independent gate re-run: see the summary relay. The §3 feedback is accepted in full — the TriggerMode gap was this coordinator's spec error (the batch asked for a column `AnalysisLogger` never wrote; the substitution was the right move), and future batches will name the `[no-engine-change]` token explicitly. The lane-E overlay commit is approved.

**D1 — PARTIAL CLEARANCE.** Window-consuming studies: cleared. VWAP *values*: cleared for coarse/structural use with a documented ~1.3 bps noise floor (mean |Δ| $8.6 at 64k); fine VWAP-dev threshold sweeps (steps < 5 bps) stay withheld pending D2's reclass. Funding unchanged: structurally approximate, discouraged.

**D2 — HYPOTHESIS CONFIRMED from the shipped numbers.** Mean |Δ| ≈ 1.3 bps against a ~$6.4 point-value bar is a correctly-computed accumulator scored on a mis-scoped tolerance; the $261 max is a session-edge tail, not noise. **Ordered (reporting-only micro-task, next batch):** re-class accumulator-family columns to a bps-scale tolerance (recommend 5 bps of price) and report the session-edge tail separately. On landing, D1's value clearance widens accordingly.

**D3 — SLOT GRANTED: the Opus month's headline ⚠ CANDIDATE, strictly evidence-first.** (a) Option D (`TriggerMode` CSV column) approved to ride the NEXT header rotation — never forcing one; (b) the closed-bar volume A/B runs on the BACKTESTER first (volume is candle-derived — inside the cleared class; this is a registered use case now); (c) any live change is its own maximal-⚠ D-table, sequenced AFTER F1/Kelly-CAL land, never stacked with another boundary. The narrow Option-B scoping (one function + `CurrentVolume` off the closed bar) is noted as the likely shape if evidence supports it. The finding's magnitude (volume vote ≤2% vs ~20% closed-bar; the trader's 3× rule at 10–40× lower fire rate; Pass-2 upgrade suppression) justifies the headline slot.

**D4 — YES.** §15 row added (settings-untouched, W6-4 precedent), this commit.

**D5 — READ AS GUARD-RAIL CONFIRMATION.** Full-book tables are context-only (all-cell DIVERGENT = the 07-08 regime break dominating); post-07-08 is the decision surface — consistent with the standing validation-window ruling. Its winner being the live baseline `(0,0)` with no flags means the Friday session should treat "no separable geometry change yet" as a legitimate, likely outcome.

---

## 7. Orchestrator response to §6 — D2's sizing premise does not reconcile

> **⚠ SUPERSEDED 2026-07-31 by `docs/d3-closed-bar-volume-ab-2026-07-31.md` §2. Read that first.**
>
> §7.1–§7.2c below correctly localised the VWAP residual to the terminal bar's **volume** — every step of that reasoning holds and the eliminations are still valid. What none of it identified is **why** the volumes differed: `BuildFormingStub` was summing `TradeRecord.Amount` (USD notional) into `Candle.Volume` (BTC), a ~**64,000×** unit error in the synthesizer, found by running the D3 A/B and fixed in `ae8a1f6`.
>
> With that fixed, **VWAP agrees 100.00 % on all 840 rows** (from 56.19 %), the sigma bands 99.76–100.00 %, OBVTrend 99.76 % (from 71.43 %), and verdict agreement 74.05 % → **79.64 %**.
>
> **Net effect on D2:** there was never a tolerance question. Not a mis-scoped bar, not a noise floor, not a 1.3 bps figure and not a 10.9 bps one — a tool bug. The coordinator's ruling and my own §2 hypothesis were both wrong, in the same direction, because we both assumed the inputs were sound and argued about how to score them. **Do not spec the reclass. `NumTight` was right all along.**
>
> **Net effect on D1:** the fine-sweep withholding rested on the VWAP values being untrustworthy. They now agree exactly. Widening the clearance is the Fable seat's call, but nothing in the evidence still argues against it.

Four of the five rulings are recorded and actioned (see §7.2). **D2 needs re-reading before its micro-task is specced**, because the two numbers it rests on don't match what the run published — and because the "session-edge tail" half is testable and comes back negative.

### 7.1 The numbers

§6 D1/D2 cite **mean |Δ| $8.6 (≈1.3 bps)** and a **$261 max**, and read the max as a session-edge tail.

The generated report's VWAP row is **mean |Δ| 69.79, max 678.7** (`overlap_validation_anchorfix.md` §4, transcribed unchanged into §10.1). I re-derived the join independently — the same bucket contract A44a pins, live-primary then AWS gap-fill — and reproduced the report exactly: **840 buckets, mean $69.79, max $678.67.** At BTC ≈ 64 k that is **10.9 bps, not 1.3**. I can't locate $8.6 or $261 in any column of the run; flagging rather than guessing where they came from.

**The bigger issue is that a mean is the wrong statistic here — the distribution is bimodal, not a noise floor:**

| pct | \|Δ\| $ | \|Δ\| bps |
|---|---:|---:|
| p10 | 0.00 | 0.00 |
| p25 | 0.02 | 0.00 |
| **p50** | **1.26** | **0.20** |
| **p75** | **101.53** | **15.85** |
| p90 | 235.53 | 36.68 |
| p95 | 353.93 | 55.66 |
| p99 | 497.92 | 78.36 |

Half the rows agree to within **a dollar**; the top quartile is **$100–500 out**. There is no population sitting just over a slightly-too-tight bar — there is a matched population at ~0 and a mismatched one two to three orders of magnitude above the tolerance. Only ~10 % of rows fall between $1 and $100, so **a 5 bps bar (≈$32) moves the match rate by a few points at most.** Reaching ~90 % would need ≈37 bps, which isn't re-scoping a tolerance — it's declaring agreement across a $235 gap on a $64 k instrument.

### 7.2 The session-edge half, tested

The ruling reads the tail as a session-edge artifact. That's directly checkable against `VWAPSessionCandles`, and it doesn't hold:

- **Not session-window length.** Match rate is flat at **47–65 %** across every window bucket from 1–10 bars to 241+, and mean |Δ| is **highest in the longest-window class** ($84.75, p90 $336) — the opposite of an edge effect.
- **Not which box supplied the live row.** PRIMARY(local) 53.2 % vs SECONDARY(aws) 58.8 % — both bimodal.
- **Not time-of-day.** Miss rate 20–65 % across all 21 populated UTC hours, no structure.
- **Not episodic.** 368 misses form **179 contiguous runs, mean length 2.1, longest 10**; pure scatter at that miss rate predicts ~184 runs of ~2.1. It's a per-row coin-flip, not an event.

So the residual is a **scattered per-row content difference inside a provably identical window** (`VWAPSessionCandles` 100.00 %, max |Δ| 0). VWAP is volume-weighted, and lane C established the terminal bar of every series is a forming stub — live's taken at poll time, the synthetic's rebuilt from stored trades in `[closeMs, closeMs + 2 s]`. A per-row volume difference in that bar is the obvious candidate. **Untested; not asserted.**

### 7.2b RESOLVED by measurement — it is a volume-input difference, not a tolerance

Rather than spend a Fable turn re-ruling this, I took it the rest of the way. The answer is unambiguous and needs no judgment.

**Step 1 — the price inputs are provably identical.** VWAP is `Σ((H+L+C)/3 × Vol) / Σ(Vol)`. From the same run:

| Column | Input it exercises | Match | mean \|Δ\| |
|---|---|---:|---:|
| DonchianUpper / Lower | **High, Low** | **100.00 %** | **0** |
| EMA9 / 21 / 50 | **Close** | 99.52 / 99.88 / 99.88 % | 0.77 / 0.35 / 0.15 |
| **VolumeRatio** | **Volume** | **23.57 %** | 4.108 |
| VWAP | (H+L+C)/3 **weighted by Volume** | 56.19 % | 69.79 |

Highs and lows are byte-identical across the whole window (max |Δ| 0). Closes agree to 99.5 %+. **Volume is the only VWAP input left that can be producing a $100–500 difference.**

**Step 2 — confirmed per row.** Splitting the 840 joined buckets by whether the row's own volume reading agrees (same NumTight rule applied to `VolumeRatio`):

| Volume state | n | VWAP match | median \|ΔVWAP\| |
|---|---:|---:|---:|
| **VolumeRatio AGREES** | 199 | **100.0 %** | **$0.00** |
| VolumeRatio DIFFERS | 641 | 42.6 % | $19.01 |

**Every single row whose volume agrees has a VWAP that agrees — 199 for 199, median difference exactly zero.** There is no residual left over for a tolerance to explain.

**This refutes the tolerance hypothesis outright, including my own §2 D2 read.** I proposed the mis-scoped-accumulator theory and the coordinator confirmed it; the data says we were both wrong. The accumulator is correct *and* correctly scored — when it is fed the same inputs it returns the same answer to the cent. The 44 % miss is a genuine difference in the volume the two sides see.

**Do not spec the bps-scale reclass.** It would widen a bar around a difference that is real, converting a true negative into a false pass. The `NumTight` class is doing its job.

**D2 and D3 are now provably the same defect, not two related ones.** The synthetic rebuilds each series' terminal bar from stored trades in `[closeMs, closeMs + 2 s]`; live carries whatever the feed had accumulated at poll time. That is a per-row, coin-like volume difference — which is exactly the scattered non-episodic signature §7.2 measured, and exactly lane C's finding seen from the synthesizer side. **Fixing the volume representation resolves both; a tolerance change resolves neither.**

**What this does to D1:** the fine-sweep withholding is no longer gated on a tolerance micro-task that shouldn't happen. It is gated on volume fidelity — i.e. on D3's evidence lane, which is already ordered and already runnable. D1's window and coarse/structural clearances are untouched and remain correct: they rest on the window being exact, and it is.

### 7.2c Overlap re-read conditioned on volume agreement — the whole VWAP family goes to 100 %

Analysis only, no code, no re-run of the validator. Per-column match rate over the same 840 buckets, all rows vs the 199 where the row's own volume reading agrees (each column scored under its own validator tolerance class):

| Column | all rows | volume-agreeing | Δ |
|---|---:|---:|---:|
| **VolumeRatio** | 23.69 % | **100.00 %** | **+76.3** |
| **VWAP** | 56.19 % | **100.00 %** | **+43.8** |
| **VWAPSigma1Upper / Lower** | 55.00 % | **100.00 %** | **+45.0** |
| **VWAPSigma2Upper** | 54.52 % | **100.00 %** | **+45.5** |
| **VWAPSigma2Lower** | 53.45 % | **100.00 %** | **+46.5** |
| VWAPDevPct | 40.60 % | 76.38 % | +35.8 |
| ATR | 46.55 % | 32.16 % | −14.4 |
| RSI | 42.98 % | 32.16 % | −10.8 |
| ADX | 49.76 % | 43.22 % | −6.5 |
| PlusDI / MinusDI | 14.4 / 14.1 % | 1.5 / 1.5 % | −12.9 / −12.5 |
| TTMHistogram | 42.62 % | 32.16 % | −10.5 |
| CVD / MicroCVD family | 4–24 % | 4–18 % | −6 to +3 |
| Score fields | 33–36 % | 34–44 % | −1 to +8 |

**Every price column in the VWAP family lands on exactly 100.00 %.** VWAPDevPct stops at 76.38 % only because it also divides by `Price`, which itself matches 77.62 % overall.

**What the conditioning actually selects, checked rather than assumed:** all 199 agreeing rows have live `VolumeRatio` ≤ 0.01, and in **100 %** of them it is the tolerance's *absolute* 0.01 floor that decides — not a proportional match. So the split is really **"both terminal bars carry ~zero volume"** (p50 0.00100) vs **"at least one carries real volume"** (p50 0.01840, max 4.37).

That makes the mechanism exact rather than merely correlated. VWAP is a volume-**weighted** mean: when the terminal bar's weight is ~0 on both sides it drops out of both sums, leaving 239 identical bars and a VWAP identical to the cent. When it carries real volume on one side and a different amount on the other, it enters one weighted mean and not the other. **The entire VWAP-family disagreement is carried by the terminal bar's volume — not the window, not the prices, not session edges.**

**The necessary caveat, stated so nobody over-reads this:** the 199-row subpopulation is *quiet terminal bars*, which is a biased subsample. That is exactly why ATR / RSI / ADX / DI / TTM get **worse** on it, not better — those columns have their own separate causes and a quiet subsample doesn't help them. **Volume is not a universal explanation for the synthesizer's residuals.** It is the complete explanation for the VWAP family, and for nothing else in the table.

### 7.3 What this does to the ordering — flagged, not re-ruled

As ordered, the micro-task looks likely to land without moving the match rate, and D1's fine-sweep clearance is conditioned on it landing — which would leave D1 stuck. Root-causing the 44 % first and letting the tolerance question follow the answer may be the cheaper path. **That's a re-ordering of your ruling and not mine to make**; the four eliminations above are yours to use either way.

**Unaffected:** D2's *mechanism* claim stands on its own evidence — the window is provably exact, so the accumulator computes correctly over the right bars. That was never in question.

### 7.4 Rulings actioned

| # | Action taken |
|---|---|
| **D1** | Partial clearance recorded in `backtest-overlap-validation-2026-07-30.md` §10.4 and `backtest-synthesizer-spec-back.md` §9.2, **with the noise-floor figure flagged pending §7.1**. |
| **D2** | Not actioned — see above. No micro-task specced. |
| **D3** | Recorded; next-month work by construction, nothing this batch. One consequence worth noting: the ordering makes lane C's **Option D (`TriggerMode`) a prerequisite**, not one of four alternatives. |
| **D4** | Already done by the coordinator in the ruling commit — §15 row present at `DeribitIndicatorProject.md:391`, settings-untouched, W6-4 precedent. Verified, not duplicated. |
| **D5** | Recorded in `pre-aug1-batch-summary.md` §E — full-book tables marked context-only, post-07-08 marked the decision surface, and "no separable geometry change yet" marked a legitimate outcome. |
