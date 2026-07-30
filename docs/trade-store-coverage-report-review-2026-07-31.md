# Review — trade-store coverage report proposal (`f66ea3b`)

**Reviewer:** the Opus orchestrator seat, 2026-07-31. **Reviewed:** [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md), AWAITING TRADER, D1–D6 open, nothing built.
**Verdict: the spec is well-founded and I'd build it.** Its §0 derivation is the best part — I re-derived every figure from the store and **all six match exactly**. Five findings, one of which is the reason to raise the priority rather than lower it.

---

## 0. The finding that changes the case for building this

**The spec's premise is not hypothetical. The hole already exists, it is ~4 weeks wide, and it is in the funding stream.**

The spec opens by saying *"a three-week hole runs a sweep happily and prints a number."* Checking S4's own arithmetic against the current store:

| Stream | Have | Expected | Missing |
|---|---:|---:|---:|
| Candles 1m | 259,974 | 259,974 | **0** |
| Candles 3m | 86,658 | 86,658 | **0** |
| Candles 5m | 51,995 | 51,995 | **0** |
| Candles 15m | 17,332 | 17,332 | **0** |
| **Funding** | **3,644** | **4,326** | **682 (15.8%)** |

**`funding` has a 28.2-day hole: 2026-06-30 23:00 UTC → 2026-07-29 05:00 UTC.**

It is not a baseline error. The cadence is exactly hourly — 3,637 of 3,643 intervals are 60 minutes, 5 are 120 (five genuinely dropped samples), and **one is 40,680 minutes**. Per-month counts confirm the shape: Feb 671 · Mar 743 · Apr 719 · May 743 · Jun 719 — essentially complete — against **July 30**. July funding covers only 07-29 05:00 → 07-30 10:00.

It is also **not a venue retention cap**: the endpoint served complete February. A cap cannot explain a hole in the *newest* month while the oldest is intact. That points at the fetch, which is precisely the failure class S4 was written for — *"a bad or truncated fetch … a silent hole there corrupts a threshold recommendation with no symptom at all."*

Three consequences:

1. **Move this into §0 as a measured fact.** Right now §3.1's illustrative output reads `funding 4,344 / 4,344 samples OK`, which is the *opposite* of the store's actual state and is the one line a reader will anchor on. The real number is the strongest argument in the document.
2. **Anything consuming the store's funding is currently working on ~5 months, not 6** — a funding-momentum re-baseline or the §12 per-resolution funding watch would silently fit on a truncated book.
3. **My own JOB 2 derivations are unaffected**, and I checked rather than assumed: none of the four consumed funding, and the candle store is **100% complete at all four resolutions**, so the TTM/OBV/volume/swing distributions have no holes under them.

---

## 1. What I verified, and it is unusually clean

Every §0 figure re-derived from `backtest_data/trades_2026-07.csv`:

| Claim | Spec | Measured |
|---|---:|---:|
| Trades in sample | 118,775 | **118,775** ✅ |
| Span | 30.2 h | **30.18 h** ✅ |
| Positive-length intervals | 62,507 | **62,507** ✅ |
| p50 | 137 ms | **137** ✅ |
| p99 | 24,674 ms | **24,674** ✅ |
| p99.9 | 61,327 ms | **61,327** ✅ |
| p99.99 | 97,310 ms | **97,310** ✅ |
| **max** | **161,652 ms** | **161,652** ✅ |
| Hour-buckets non-empty | 31 of 31 | **31** ✅ |
| File size | 4,799,134 B | **4,799,134** ✅ |

Both incidental corrections to the v64 spec check out: 4,799,134 B / 30.18 h × 24 = **3.82 MB/day** (vs the claimed ~2.4), and 118,775 / 30.18 × 24 = **94,440 trades/day** (vs the claimed ~60,594). The §1.3 argument does strengthen — the raw-trades-per-CSV-row ratio rises to ~250.

`ws_health.log` is as described: **32 lines**, 2026-07-23 → 07-30, format `utc | state | instance_id`, and `Core/WsHealthLog.vb` really does carry a distinct `LogStart` alongside `LogTransition`. **15 distinct instance GUIDs**, i.e. 15 process lives. States present: DOWN 15 · OK 16 · DEGRADED 1 (no REST observed locally).

§10's warning is correct — `LoadTradeRange` does `HashSet`-dedup the whole range and sort it, so it is the wrong tool for a counting pass. **A49 is correctly the next free family** (A48 consumed by v64); next free after this spec is **A50**.

---

## 2. Findings

### C2 — moderate. S1's "was the app up?" is an *inference*, and the ambiguity resolves the dangerous way.

`ws_health.log` is **transition-only**. That means:

> A healthy app that logs nothing for 12 hours and a **dead** app that logs nothing for 12 hours are **byte-identical in the file.**

The only disambiguator is GUID continuity across the *next* line — same GUID ⇒ the process never died; new GUID ⇒ it restarted somewhere in between, and "somewhere" can be many hours wide. So the resolution is **retrospective**, and the **trailing interval is permanently ambiguous** — which is exactly the interval a daily coverage check cares about most.

§7's third bullet acknowledges the hard-kill case and A49a's fixture names "a process that ends without a DOWN line", so this is not news to the author. But the §2 S1 table states the rule as *"App **DOWN / absent** ⇒ missing tape is **expected**, reported as such, not as a defect"* — and **"absent" is precisely the state that cannot be distinguished from "up and quiet."** As written, the strongest signal's headline rule is stated on a distinction the log cannot make.

**Recommend:** state the resolution rule explicitly, and default the ambiguous window toward **defect**, not expected-missing. A report that silently reclassifies real loss as expected is worse than no report, and that is the exact failure this document exists to prevent. Erring toward "defect" costs a human dismissal; erring the other way costs the tape.

**Related wording snag:** every process life's *first* line is `DOWN` — the `LogStart` pre-connect state. So `DOWN` is overloaded: "starting up" and "connection lost" share one token. For S1's purposes that is benign (both mean the app **is** running), but the phrase "App DOWN ⇒ missing tape is expected" reads as though DOWN meant the app was not running. It means the opposite — **a DOWN line proves the app was alive to write it.**

### C3 — moderate. The report cannot tell a capture defect from capture being switched off.

`trade_store.enabled:false` produces exactly S1's defect signature: app OK, no trades. This is not hypothetical — my [v64 review](trade-store-capture-review-2026-07-31.md) F6 recommends running the **local** box with `enabled:false`, since D1 ruled AWS-only and the shipped default is `true`. Run `coverage` against a local copy-back and **every up-hour reports as a capture defect**.

`ws_health.log` does not carry the flag. Two cheap fixes, either of which closes it: read `trade_store.enabled` from the `settings.json` beside the exe, or have `TradeStoreWriter` emit one marker line per process on first write. **Suggest a D7.**

### C4 — low, but it changes D2's wording. `ws_health.log` is not "beside the CSV."

`Core/WsHealthLog.vb:32` resolves it to `AppDomain.CurrentDomain.BaseDirectory + "ws_health.log"` — the **exe directory** — while D3 puts the store at `<exe>\backtest_data\`. It lives in the store's **parent**, not inside it. So a copy-back of `backtest_data\` alone **silently drops S1** and the report quietly degrades to three weak heuristics.

A49g means the degradation is *stated* rather than silent, which is the right guard. But D2's instruction ("one small file beside it") should name both paths explicitly, because the natural copy-back action is "copy the store directory" and that misses it.

### C5 — low. One §0 caveat cuts the other way, and it undermines D4's re-anchor plan.

The spec lists *"REST-backfilled rather than streamed (so it reflects the fetch path, not the capture path)"* among three caveats that all *loosen* the threshold. For S3's actual purpose it points the other way: a REST backfill is the **venue's own record of what printed**, which is exactly the ground truth you want for *"what is a natural silence?"* A streamed sample contains capture-path gaps **in addition** to natural ones, so it is a **noisier** basis for a natural-silence threshold, not a better one.

That matters for **D4**, which proposes re-anchoring after the first streaming week: it would re-anchor on a different and dirtier quantity, and would tend to *inflate* the threshold by absorbing capture gaps into the "natural" baseline — blunting the very signal S3 provides.

**Suggest instead:** keep deriving the natural-silence baseline from **REST-backfilled** windows (extended to include a weekend, which is the genuinely load-bearing caveat of the three), and use streamed data to measure the **difference** between the two — because that difference *is* the capture-gap signal, measured directly rather than inferred from a threshold.

---

## 3. Where I agree, briefly

- **D1 (new verb)** — agree. Different input, different question, and a schedulable exit code needs its own entry point.
- **D3 (CLI flag)** — agree, and it sidesteps the fence question cleanly rather than deferring it.
- **D5 (Part B)** — agree it is the higher-value half, and its parity-exemption reasoning matches the v62 `MIN NET MOVE %` precedent correctly. Given C2 (the trailing interval is the ambiguous one), Part B is also the only thing that makes the *current* hour observable at all — which strengthens the case for (a) over (b).
- **D6 (opt-in `--strict`)** — agree. Though note C3: until the enabled-flag question is settled, `--strict` against a local store would fail on every run.
- **§7's honesty about the partial-hour blind spot** is the right call, and the reasoning (a volume baseline needs weeks of weekend-inclusive history) is supported by the 10× hour-06/hour-07 spread the spec measured.

## 4. What I did not verify

- **Nothing was built or run** — there is no `coverage` verb yet. Every claim about its behaviour is a claim about the spec, not about code.
- **The streamed capture path.** The whole §0 sample is REST-backfilled; no streamed store exists yet locally, so the S3 threshold's fitness against *streamed* data is untested by anyone.
- **AWS-side paths.** Whether `<exe>\ws_health.log` and `<exe>\backtest_data\` on the deployed box are both reachable by the copy-back is unverified from here — and C4 is the reason it now matters.
- **The funding gap's cause.** I established that it exists, that it is 28.2 days, that the cadence is hourly, and that venue retention does not explain it. **I did not diagnose the fetch defect** — that belongs to whoever owns `HistoricalStore.BackfillFundingMonthAsync`, and it should be a separate item from this spec.
