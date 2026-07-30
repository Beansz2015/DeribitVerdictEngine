# Trade-Store Coverage Report — Proposal

**Status:** **AWAITING TRADER** — D1–**D7** in §9. Spec-first; nothing built. **Revised 2026-07-31** against [`trade-store-coverage-report-review-2026-07-31.md`](trade-store-coverage-report-review-2026-07-31.md) — see §11 for what changed and the two places the review needs correcting.
**Target:** Part A is **tools-only** — no settings keys, **no version bump**, `[no-engine-change]`. Part B (D5) touches the app and would take a bump.
**Scoring impact:** **NONE.** Reads the store and a sidecar; writes a report. No indicator, no CSV column, no verdict path, no bridge field.
**Dataset boundary:** **NONE**, either part.
**Gate to build:** safe anytime.
**Origin:** trader question 2026-07-31, after the v64 capture build — *"what's the consequence of a missing day of tape, assuming we read through 6 months at a time?"* The honest answer was **statistically almost nothing** (~0.55 % of a 182-day window; under one row in the scarcest STRONG × session cell), and that a lost day matters as a **smoke alarm**, not as data loss. Which exposed the real gap: **nothing reports coverage**, so a three-week hole runs a sweep happily and prints a number.

---

## 0a. The hole is not hypothetical — it already exists, and it is in `funding`

The review found this by running S4's own arithmetic against the current store, and I reproduced it independently. **It is the strongest argument in this document**, so it goes first.

| Stream | Have | Expected | Missing |
|---|---:|---:|---:|
| Candles 1m / 3m / 5m / 15m | 259,974 / 86,658 / 51,995 / 17,332 | same | **0 — complete at all four resolutions** |
| **Funding** | **3,644** | **4,326** | **682 (15.8 %)** |

**`funding` has a 28.2-day hole: 2026-06-30 23:00 UTC → 2026-07-29 05:00 UTC** (measured gap 40,680 min). Per-month counts show the shape plainly — Feb 671 · Mar 743 · Apr 719 · May 743 · Jun 719, essentially complete, against **July 30**.

It is **not** a venue retention cap: the endpoint served a complete February, and a cap cannot explain a hole in the *newest* month while the oldest is intact. That points at the fetch — precisely the failure class S4 was written for, and precisely the failure class that has **no symptom at all** until something reports it.

**Consequence:** anything consuming the store's funding is currently working on ~5 months, not 6. A funding-momentum re-baseline or the §12 per-resolution funding watch would silently fit on a truncated book. Candle-derived work is unaffected — the candle store is complete.

### 0a.1 A correction to the review, and a second defect inside the first

The review reads the five 120-minute funding gaps as *"five genuinely dropped samples"* — noise alongside the big hole. They are not noise. **All five sit exactly on a month boundary** — 2026-02-01, 03-01, 04-01, 05-01, 06-01, each at **00:00 UTC** — which is **5 of 5 internal boundaries in the store, a 100 % hit rate.** That is not random loss; it is a deterministic off-by-one at the monthly-file seam in `BackfillFundingMonthAsync`, where the window is `[segStart, segEndExcl − 1 ms]` and the sample landing on the boundary instant falls in the crack between two months' fetches.

The distinction matters because the two defects want different responses: the 28.2-day hole is undiagnosed and needs investigation; the boundary drop is deterministic, reproducible, and a small fix. **Both belong to `HistoricalStore.BackfillFundingMonthAsync`, not to this spec** — flagged, not folded in. I have not diagnosed the 28.2-day hole's cause and am not proposing a fix here.

---

## 0. What building this found before it was built

I derived the store's actual gap distribution before writing this, and **it contradicted the intuition the idea rested on.** The pitch was "the tape prints continuously, so a silence is unambiguous." It is not.

**Sample:** the live store, `trades_2026-07.csv`, 2026-07-29 10:43 → 2026-07-30 16:54 UTC — **30.2 h, 118,775 trades, 62,507 positive-length inter-trade intervals** (the other ~56k share a millisecond with their predecessor — batched fills).

| Inter-trade gap | ms |
|---|---:|
| p50 | 137 |
| p99 | 24,674 |
| p99.9 | 61,327 |
| p99.99 | 97,310 |
| **max** | **161,652** (2 m 42 s) |

**A 2m42s silence is normal on this instrument, on a weekday.** Any "detect the silence" threshold has to sit well above that, which makes it a much blunter instrument than it sounds. Per-hour volume is no better as a baseline — the same sample runs **1,555 trades in hour 06 against 15,602 in hour 07, a 10× spread** — so a flat per-hour expectation is meaningless and even a per-hour median needs weeks of history before it means anything.

**Three caveats on that sample, all of which loosen the threshold further:** it was REST-backfilled rather than streamed (so it reflects the fetch path, not the capture path); it is Wednesday–Thursday only, with **no weekend**, and weekend Asia hours are the quietest tape there is; and 30 hours is a thin basis for a p99.99.

The conclusion that follows is the whole design: **stop trying to infer coverage from the market, and cross-reference the app's own uptime record instead.**

**Two incidental corrections to the v64 spec, recorded so they are not carried forward:**
- §9 projects **~2.4 MB/day, ~900 MB/year**. Measured on the real file: 4,799,134 bytes / 30.2 h = **~3.8 MB/day, ~1.4 GB/year**, ~1.6× the estimate. Still not a problem, still not worth compaction.
- §1.3 cites **~60,594 trades/day** as the busiest full day. That was computed when the store held 19,529 rows; the current file runs **~3,930 trades/hour ⇒ ~94k/day**. The §1.3 *argument* is unaffected — it gets stronger, since the raw-trades-per-CSV-row ratio rises from ~160 to ~250.

---

## 1. What "coverage" can honestly mean, per stream

The streams do not share a notion of ground truth, and a report that pretends they do is worse than none.

| Stream | Is there ground truth for "what should be here"? | So what can be checked |
|---|---|---|
| **Candles** (1m/3m/5m/15m) | **Yes, exactly.** A UTC day at 1m has 1,440 bars, deterministically. | Missing-bar count per day, per resolution. A *fetch* defect, precisely detectable. |
| **Funding** | **Nearly.** ~1 sample/hour. | Sample count per day vs 24. Coarse but real. |
| **Trades**, older than ~24 h | **No.** The tape is whatever the market did; nobody can say how many trades "should" have printed. | Only *indirect* signals — S1–S3. |
| **Trades**, inside the ~24 h retention window | **Yes, exactly — the venue will tell you.** | An exact set diff against `get_last_trades_by_instrument_and_time` — S0. |

Trades are the stream that matters, and the honest statement is **not** that they have no ground truth — it is that they have ground truth for about a day and none after. The original draft of this spec said flatly that trades have no ground truth; that is wrong, and it is wrong in the place where it costs the most, because **the daily-check use case lives entirely inside the window where ground truth exists.** §2's S0 is the consequence.

---

## 2. Five signals, ranked by how much they can be trusted

### S0 — Venue diff inside the retention window (exact; the only signal with ground truth)

For any window younger than Deribit's ~24 h trade retention, `get_last_trades_by_instrument_and_time` **is** the answer key. Fetch what the venue says printed; diff it against what the store holds. The difference is not an estimate of capture loss — it **is** capture loss, enumerated.

Two properties make this the top signal rather than a nice extra:

- **It measures residual loss *after* repair**, which is the number that actually matters. If gap repair is working, S0 reads zero for everything older than one repair interval; a non-zero S0 means both mechanisms missed, which is the only genuinely alarming state the v64 design admits.
- **It needs no threshold, no baseline and no uptime record.** It is immune to every ambiguity S1–S3 have to reason around.

Costs, both real: it is a **network call**, so the verb stops being purely read-only, and it only answers for the last day. Therefore **opt-in via `--verify-venue`**, default off, and the report states plainly which hours S0 covered and which fell back to S1–S3.

This signal exists because of the review's C5 — see §11.

### S1 — Uptime cross-reference (strong; no market baseline)

Converts an unanswerable question into an answerable one:

> ~~Should there have been tape in this hour?~~ → **Was the app up in this hour, and did we capture while it was?**

**The primary uptime record is `analysis_log.csv`, not `ws_health.log`.** The original draft had this backwards, and the review's C2 is right that `ws_health.log` alone cannot carry S1 — it is transition-only, so a healthy app that logs nothing for twelve hours and a dead one are byte-identical in the file. Measured locally: `ws_health.log` holds **32 lines across seven days**. Over the same two days, `analysis_log.csv` holds a row per completed run at **p50 60 s / p90 155 s** spacing, with an `InstanceId` column (col 110) attributing every row to a process life. One is a ~60-second heartbeat; the other is a weekly handful of transitions.

So:

- **`analysis_log.csv` rows present in hour H ⇒ the app was definitively up in hour H.** No inference. This collapses C2's ambiguous window from *hours* to *the minutes since the last row*.
- **`ws_health.log` supplements it** with the two things the CSV cannot say: the **DEGRADED / REST** state (which tells you *which* capture mechanism should have been carrying the hour), and the all-runs-skipped case where the app is up but writing no CSV rows.
- **Residual ambiguity — the trailing window and cross-GUID gaps — defaults to DEFECT, not expected-missing** (C2's recommendation, accepted). A report that silently reclassifies real loss as expected is worse than no report. Erring toward defect costs a human dismissal; erring the other way costs the tape.

**Wording correction the review caught, and it is worth stating loudly because the file's token is genuinely misleading:** `WsHealthLog.LogStart` writes a `DOWN` line at *process start*, before the socket connects. So **a `DOWN` line proves the app was alive to write it.** The original §2 said "App DOWN ⇒ missing tape is expected", which is exactly backwards. `DOWN` means running-but-not-connected; **absent lines**, not DOWN lines, are what indicate a dead process — and absence is the ambiguous state the rule above now resolves toward defect.

**Dependency it creates:** the uptime records must travel with the store on copy-back — see D2 and C4.

### S2 — Empty hours inside an up-interval (strong; no baseline)

An hour with **zero** stored trades while the app was OK is unambiguous. No threshold, no tuning. In the 30.2-hour sample, **31 of 31 hour-buckets are non-empty**, so this signal is quiet on healthy data — which is what makes it worth having.

### S3 — Longest gap per hour vs a threshold (weak; explicitly provisional)

The §0 numbers make this the *third*-best signal, not the first. Proposed default **300,000 ms (5 min)** — 1.85× the observed max of 161,652 ms. Deliberately loose: it will not catch a 4-minute stall, and it is not supposed to. It catches the multi-minute-to-hours outages that S1 might miss if the health log is absent or the app died without writing a DOWN line.

**Provisional anchor, re-anchor on evidence** (the `alerts.*` / absorption discipline) — but **re-anchor on REST-backfilled data, not streamed data.** The original draft listed "REST-backfilled rather than streamed" among three caveats that all *loosen* the threshold. The review's C5 shows it cuts the other way and I accept the correction: a REST backfill is **the venue's own record of what printed**, which is exactly the ground truth you want for *"what is a natural silence?"* A streamed sample carries capture-path gaps **on top of** natural ones, so re-anchoring on it would absorb capture loss into the "natural" baseline and inflate the threshold — blunting the very signal S3 provides.

The genuinely load-bearing caveat of the three is the missing **weekend**, since weekend Asia is where the natural max will actually live. So: extend the REST-derived window to include a weekend, and re-anchor from that. The report prints the observed max alongside the threshold so the re-anchor is a read, not a project.

**And the difference between the two samples is itself the measurement** (C5's constructive half): REST-vs-streamed over the same window is capture loss observed directly rather than inferred from a threshold. That is S0, promoted out of this note to the top of the ranking where it belongs.

### S4 — Candle / funding completeness (deterministic; different failure)

Nearly free and catches a class S1–S3 cannot: a bad or truncated *fetch*. Worth including because the 6-month candle store is what most of the §4.2 unblocked derivations consume, and a silent hole there corrupts a threshold recommendation with no symptom at all.

---

## 3. Design — a new `coverage` verb

```
BacktestRunner coverage --from 2026-02-01 --to 2026-07-31 [--gap-ms 300000] [--out coverage.md] [--strict]
```

- **New verb, not a section in `report`** (D1). `report` runs `AnalysisRunner` over a **logged CSV** — a different input and a different question. Bolting store health onto it conflates two surfaces and makes neither runnable alone.
- **Reads only.** Never fetches, never writes to the store. Safe to run against a copy-back at any time.
- **Self-bounding on the capture era.** Coverage is reported from the **first trade timestamp present in the store**, not from `--from`. Days before that are summarised in one header line (`trades available from 2026-07-29; 179 earlier days are outside capture`) rather than 179 rows of noise. No settings key, no configured start date — the store says when capture began.
- **Degrades gracefully.** No `ws_health.log` ⇒ S1 is skipped with a stated reason and S2–S4 still run. The report must never be *silently* weaker than it looks.
- **`--gap-ms` is a CLI flag, not a settings key.** Keeps Part A tools-only: no POCO field, no `settings.json` block, no version bump, no HC. A tuning knob that lives in the command line cannot drift into the tweaker surface.

### 3.1 Output

Console summary always (this is meant to be glanceable); `--out` additionally writes markdown next to the other reports.

The illustrative numbers below are the **store's real current state**, not invented ones — the review's first recommendation, and correctly so: the original draft's example line read `funding 4,344 / 4,344 samples OK`, which is the exact opposite of the truth and was the one line a reader would have anchored on.

```
TRADE STORE COVERAGE  2026-02-01 → 2026-07-31
  capture begins      2026-07-29 10:43 UTC  (179 earlier days outside capture)
  app-up hours          742   (analysis_log.csv, 8,498 rows, 89 instances
                               + ws_health.log, 32 transitions, 15 process lives)
  captured hours        736
  UP BUT UNCAPTURED       6   ← capture defects
  ambiguous (→ defect)    1   ← trailing window since last heartbeat
  empty hours (up)        2
  longest gap         161.7s  (threshold 300.0s — 0 breaches)
  candles 1m/3m/5m/15m       complete at all four resolutions   OK
  funding             3,644 / 4,326 samples   *** 682 MISSING (15.8%) ***
                      28.2-day hole 2026-06-30 23:00 → 2026-07-29 05:00 UTC
                      + 5 boundary drops, one per month seam (deterministic)
  venue diff (S0)     not run — pass --verify-venue
  VERDICT: 7 defect hour(s) + funding incomplete — see coverage.md §2
```

The markdown adds the per-day table and names the defect hours with their instance IDs, so a hole is traceable to a specific process life.

### 3.2 Exit code

`--strict` ⇒ exit 1 when any **defect** hour exists (up-but-uncaptured, or empty-while-up). Expected-missing (app down, pre-capture, DEGRADED) never fails. Without `--strict` it always exits 0, so interactive use is never noisy. That makes the verb schedulable — which is the point, since the whole origin of this is that the daily human glance is load-bearing.

---

## 4. Part B (D5) — the thing that actually retires the RDP glance

A verb still requires someone to run it. The check that would genuinely replace the daily glance is **in the app**: a live status element showing capture health, e.g.

```
TAPE STORE: 12s · 47.3k rows
```

— seconds since the last **successful flush** (not last trade seen: a flush proves the whole chain to disk, a trade only proves the stream), and rows committed this process. Amber past `3 × flush_seconds`, red past `10 ×`.

`TradeStoreWriter` already tracks the state; this is a read plus a label. It is a **live status element ⇒ display-parity exempt**, on the v62 `MIN NET MOVE %` / EXIT GUARD strip precedent — no snapshot line, no card binding.

Kept as a separate decision because it touches the app and would take a version bump, whereas Part A does not. **My read: Part B is the higher-value half** — Part A tells you about a hole after the fact, Part B tells you about it while it is still one hour old. But Part A is what was asked for, and Part A alone is coherent.

---

## 5. Fences, parity, boundary

- **Tweaker:** **no new HARD CONSTRAINT for Part A** — it adds no settings keys at all. If D3 goes the other way and `gap_ms` becomes a settings key, it belongs under the existing `trade_store.` prefix and is therefore **already fenced by HC27**; no new constraint either way.
- **Display-string parity:** Part A has **no rendered surface** (console + a markdown file, neither of which is a parity surface — the offline-report precedent). Part B is a live status element, **parity-exempt** per §4.
- **Dataset boundary: NONE.** Nothing on the scoring path; `analysis_log.csv` untouched.
- **Reversibility:** Part A is additive and read-only — not running the verb *is* the rollback.

---

## 6. Acceptance + fixtures

Fixture family **A49** (A48 consumed by the v64 capture build; next free after this is **A50**).

- **A49a** — uptime parse: a synthetic `ws_health.log` with OK/DOWN/REST transitions across two process lives resolves to the correct up-intervals, including the open-ended trailing interval and a process that ends without a DOWN line.
- **A49b** — the S1 join: given up-intervals and a store, up-but-uncaptured hours are identified exactly; app-down hours are reported **expected-missing**, never as defects.
- **A49c** — DEGRADED/REST hours are their own third state, not conflated with either.
- **A49d** — capture-era self-bounding: a store whose first trade is day N reports days before N as outside capture, not as gaps, and the count is right.
- **A49e** — S3 threshold: a synthetic series with a known longest gap reports that max exactly and breaches only above the threshold; the observed max is always printed.
- **A49f** — S4 candle completeness: a month file short by k bars reports exactly k missing at the right resolution.
- **A49g** — absent `ws_health.log` ⇒ S1 skipped **with a stated reason in the output**, S2–S4 still run, exit code unaffected.
- **A49h** — `--strict` exits 1 on a defect hour and 0 on expected-missing-only; default (no flag) always exits 0.
- **A49i** — S1 primary/supplement precedence: an hour with `analysis_log.csv` rows resolves **up** even when `ws_health.log` is silent across it; an hour with neither, inside a cross-GUID or trailing window, resolves **defect** and not expected-missing (the C2 rule, pinned as a regression trap — this is the arm whose inversion loses tape silently).
- **A49j** — S0 venue diff: against a stubbed venue response, trades present at the venue but absent from the store are enumerated exactly; an identical set reports zero; hours outside the retention window are reported as **not covered by S0** rather than as clean. No live HTTP in the fixture.

Build acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0 Release**; A1–A48h unregressed + A49a–h; verify-gate `prepush` **GATE PASSED**.

---

## 7. What this cannot do — stated so it is not assumed

- **It cannot detect a partial hour older than ~24 h.** If capture dropped 30 % of trades in an hour but kept the rest, S1 and S2 both pass and S3 almost certainly does too. Inside the retention window **S0 catches it exactly**; outside it, nothing short of a per-hour volume baseline does, and §0 shows that baseline needs weeks of weekend-inclusive history before it means anything. **Deliberately out of scope** — revisit once there is a quarter of streaming capture to fit it on. This is the main argument for running the check *daily* rather than before a study: within a day the answer is exact, after a day it is a heuristic.
- **It cannot recover anything.** It is an alarm, not a repair. What it finds within ~24 h, gap repair can still fix; what it finds later is gone.
- **It cannot tell you the app died if the app died without a DOWN line.** A hard kill leaves the health log's last line as OK. S2/S3 are the backstop for exactly that case — which is why S1 alone is not enough and the weaker signals still earn their place.

---

## 8. Out of scope

- Per-hour volume baselining (above).
- Order-book capture coverage — nothing captures the book.
- Auto-triggering a repair from the report. Repair already runs on its own schedule; a report that mutates the store stops being safe to run against a copy-back.
- Any change to `analysis_log.csv`, the tweaker surface, or the scoring path.

---

## 9. D-table — awaiting the trader

| # | Decision | Options | My read |
|---|---|---|---|
| **D1** | New `coverage` verb, or a section inside the existing `report`? | (a) new verb · (b) fold into `report` | **(a) new verb.** `report` consumes a logged CSV and answers a different question; folding store health in makes neither runnable alone, and a schedulable exit code needs its own entry point. |
| **D2** | Does the report consume the uptime records — which means they must be copied back alongside the store? | (a) yes, degrade gracefully when absent · (b) store-only signals | **(a).** S1 is what separates "quiet market" from "we broke". **Naming the paths explicitly, per the review's C4, because the natural copy-back action misses them:** the store is `<exe>\backtest_data\`, but **both** `analysis_log.csv` and `ws_health.log` sit in `<exe>\` — the store's *parent*, not inside it. Copying `backtest_data\` alone silently drops S1. The CSV is already routinely copied back, which is a further reason to make it the primary record. |
| **D3** | `--gap-ms` as a CLI flag, or a settings key? | (a) CLI flag · (b) `trade_store.coverage_gap_ms` | **(a) CLI flag.** Keeps Part A tools-only — no POCO, no bump, no fence question. It is an analysis parameter, not engine configuration. |
| **D4** | Default gap threshold, and what to re-anchor it on. | 300,000 ms (1.85× the observed max), or another value | **300,000 ms, explicitly provisional**, re-anchored on a **REST-backfilled** window extended to include a weekend — **not** on streamed data (C5 accepted; streamed data would fold capture loss into the "natural" baseline and inflate the threshold). §0 is why this is looser than intuition wants. |
| **D7** | How does the report tell a capture **defect** from capture being **switched off**? (`trade_store.enabled:false` produces S1's exact defect signature — and the v64 review's F6 recommends setting it false on the local box.) | (a) one marker line per process recording the flag + store_dir · (b) read `trade_store.enabled` from the `settings.json` beside the exe · (c) scope the verb to AWS copy-backs and say so | **(a) marker line.** (b) tells you the flag's value *now*, not during the historical window, so a single flip misreads all of history. **The cost of (a) is that Part A stops being tools-only — but that cost is zero if D5 is (a)**, since Part B already takes a bump. If D5 is (b)/(c), (c) here is the honest fallback: state the assumption rather than fake the signal. |
| **D5** | Build Part B (live `TAPE STORE` status element) now, later, or not? | (a) now, same build · (b) later, own spec · (c) no | **(a) or (b), not (c).** Part B is the half that actually retires the daily glance; Part A reports a hole after the fact. It touches the app and takes a version bump, which is the only reason it is a separate question. |
| **D6** | Should `--strict` be the default rather than opt-in? | (a) opt-in as specced · (b) strict by default | **(a) opt-in.** Interactive runs during a study should not fail on a known historical hole; the scheduled use passes the flag. |

---

## 10. Notes for whoever builds it

- `HistoricalStore.EnumerateMonths` already gives the month-segment walk; coverage wants an hour walk over the same window — a small addition beside it, not a new traversal concept.
- The per-file read is already on the shared seam (`TradeStoreWriter.ReadTradeFile`), so coverage reads the store through exactly the code the writer writes with. Do not add a second parse.
- A full-store pass at current size is ~120k rows/day of capture; a 6-month window will eventually be ~17 M rows. Stream the files and accumulate per-hour counters — do **not** materialise `LoadTradeRange` for this, which sorts and dedups the whole range in memory and is the wrong tool here.
- S0's fetch should reuse `HistoricalStore.FetchTradesByTimeAsync` rather than a second HTTP path, and must **not** write to the store — the diff is a measurement; repairing what it finds is gap repair's job, already scheduled.

---

## 11. Revision record — what the review changed

Against [`trade-store-coverage-report-review-2026-07-31.md`](trade-store-coverage-report-review-2026-07-31.md) (verdict: *"well-founded and I'd build it"*, all six §0 figures independently re-derived and matching).

**Accepted in full:**

- **The funding hole leads the document** (§0a). It was the review's first recommendation and it is right — a measured 28.2-day hole is a stronger argument than any hypothetical, and the original illustrative output claimed the opposite of the truth.
- **C2 — the ambiguous window defaults to defect**, and the `DOWN`-token wording is corrected (§2 S1). But C2's premise is softened by something neither document used: **`analysis_log.csv` is a ~60-second liveness heartbeat with `InstanceId` attribution**, against `ws_health.log`'s 32 lines per week. Making the CSV the primary uptime record shrinks the ambiguous window from hours to minutes. C2's rule still applies to the residual.
- **C4 — D2 now names all three paths.** Both uptime records live in `<exe>\`, the store's parent, so "copy the store directory" misses them.
- **C5 — accepted, and it was the sharpest finding.** I had the REST-vs-streamed caveat pointing the wrong way. Its constructive half is promoted to **S0**, the top of the ranking: within the retention window the venue *is* ground truth, so capture loss can be enumerated rather than inferred. That also corrects §1's flat claim that trades have no ground truth.
- **C3 — raised as D7**, with the note that its cost is zero if D5 is (a).

**Where the review needs correcting:**

- **The five 120-minute funding gaps are not "five genuinely dropped samples."** They are 5 of 5 internal month boundaries, every one at 00:00 UTC — a deterministic off-by-one at the monthly-file seam, not random loss (§0a.1). Different defect, different fix.
- Minor: the review reads `ws_health.log`'s 32 lines as **15 process lives**; the local `analysis_log.csv` carries **89 distinct `InstanceId` values** over its full range, so the health log is capturing a small fraction of process lives even as a restart record — further reason it is the supplement and not the primary.

**Unchanged by the review:** D1, D3, D5, D6 (all agreed), §7's partial-hour honesty (now sharpened by S0's window), and A49 as the fixture family.

