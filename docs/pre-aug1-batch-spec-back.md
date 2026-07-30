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
