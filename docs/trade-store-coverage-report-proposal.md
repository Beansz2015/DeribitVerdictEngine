# Trade-Store Coverage Report — Proposal

**Status:** **AWAITING TRADER** — D1–D6 in §9. Spec-first; nothing built.
**Target:** Part A is **tools-only** — no settings keys, **no version bump**, `[no-engine-change]`. Part B (D5) touches the app and would take a bump.
**Scoring impact:** **NONE.** Reads the store and a sidecar; writes a report. No indicator, no CSV column, no verdict path, no bridge field.
**Dataset boundary:** **NONE**, either part.
**Gate to build:** safe anytime.
**Origin:** trader question 2026-07-31, after the v64 capture build — *"what's the consequence of a missing day of tape, assuming we read through 6 months at a time?"* The honest answer was **statistically almost nothing** (~0.55 % of a 182-day window; under one row in the scarcest STRONG × session cell), and that a lost day matters as a **smoke alarm**, not as data loss. Which exposed the real gap: **nothing reports coverage**, so a three-week hole runs a sweep happily and prints a number.

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
| **Trades** | **No.** The tape is whatever the market did; nobody can say how many trades "should" have printed. | Only *indirect* signals — §2. |

Trades are the stream that matters and the only one without ground truth. That asymmetry is the design problem.

---

## 2. Four signals, ranked by how much they can be trusted

### S1 — Uptime cross-reference (strongest; no market baseline at all)

`ws_health.log` already exists beside the CSV (`Core/WsHealthLog.vb`, v-W4): append-only, transition-only, `utc | state | instance_id`, plus one line per process start. Verified present locally — 32 lines spanning 2026-07-23 → 07-30, with the instance GUID changing on every process start, so **restarts are directly visible**.

That converts an unanswerable question into an answerable one:

> ~~Should there have been tape in this hour?~~ → **Was the app up in this hour, and did we capture while it was?**

- App **DOWN / absent** ⇒ missing tape is **expected**, reported as such, not as a defect. This is what stops the report crying about the pre-capture era and about legitimate maintenance windows.
- App **OK** but the store has no trades ⇒ **capture defect**, which is exactly the failure class v64's §1.1 must never hit silently.
- App **DEGRADED / REST** ⇒ no WS stream, so streaming capture is expected-absent and repair is meant to carry it — a distinct third state worth naming, because it tells you *which* mechanism to look at.

This needs no baseline, works from day one, and is the only signal that separates "the market was quiet" from "we broke."

**Dependency it creates:** the health log must travel with the store on copy-back. Under D1 (AWS-only) studies already take a store copy-back; this adds one small file beside it. That is D2.

### S2 — Empty hours inside an up-interval (strong; no baseline)

An hour with **zero** stored trades while the app was OK is unambiguous. No threshold, no tuning. In the 30.2-hour sample, **31 of 31 hour-buckets are non-empty**, so this signal is quiet on healthy data — which is what makes it worth having.

### S3 — Longest gap per hour vs a threshold (weak; explicitly provisional)

The §0 numbers make this the *third*-best signal, not the first. Proposed default **300,000 ms (5 min)** — 1.85× the observed max of 161,652 ms. Deliberately loose: it will not catch a 4-minute stall, and it is not supposed to. It catches the multi-minute-to-hours outages that S1 might miss if the health log is absent or the app died without writing a DOWN line.

**Provisional anchor, re-anchor on evidence** (the `alerts.*` / absorption discipline): re-derive after the first full week of *streaming* capture that includes a weekend, since weekend Asia is where the natural max will actually live. The report should print the observed max alongside the threshold so the re-anchor is a read, not a project.

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

```
TRADE STORE COVERAGE  2026-02-01 → 2026-07-31
  capture begins      2026-07-29 10:43 UTC  (179 earlier days outside capture)
  app-up hours        742   (ws_health.log, 32 transitions, 9 process starts)
  captured hours      736
  UP BUT UNCAPTURED     6   ← capture defects
  empty hours (up)      2
  longest gap        161.7s  (threshold 300.0s — 0 breaches)
  candles 1m         259,974 / 259,974 bars   OK
  funding              4,344 / 4,344 samples  OK
  VERDICT: 6 defect hour(s) — see coverage.md §2
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

Build acceptance: solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0 Release**; A1–A48h unregressed + A49a–h; verify-gate `prepush` **GATE PASSED**.

---

## 7. What this cannot do — stated so it is not assumed

- **It cannot detect a partial hour.** If capture dropped 30 % of trades in an hour but kept the rest, S1 and S2 both pass and S3 almost certainly does too. Nothing short of a per-hour volume baseline catches that, and §0 shows the baseline needs weeks of weekend-inclusive history before it means anything. **Deliberately out of scope** — revisit once there is a quarter of streaming capture to fit it on.
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
| **D2** | Does the report consume `ws_health.log` — which means it must be copied back alongside the store? | (a) yes, degrade gracefully when absent · (b) store-only signals | **(a).** S1 is the only signal that separates "quiet market" from "we broke"; without it the report is three weak heuristics. Cost is one small file on copy-back. |
| **D3** | `--gap-ms` as a CLI flag, or a settings key? | (a) CLI flag · (b) `trade_store.coverage_gap_ms` | **(a) CLI flag.** Keeps Part A tools-only — no POCO, no bump, no fence question. It is an analysis parameter, not engine configuration. |
| **D4** | Default gap threshold. | 300,000 ms (1.85× the observed max), or another value | **300,000 ms, explicitly provisional**, re-anchored after the first streaming week that includes a weekend. §0 is why this is looser than intuition wants. |
| **D5** | Build Part B (live `TAPE STORE` status element) now, later, or not? | (a) now, same build · (b) later, own spec · (c) no | **(a) or (b), not (c).** Part B is the half that actually retires the daily glance; Part A reports a hole after the fact. It touches the app and takes a version bump, which is the only reason it is a separate question. |
| **D6** | Should `--strict` be the default rather than opt-in? | (a) opt-in as specced · (b) strict by default | **(a) opt-in.** Interactive runs during a study should not fail on a known historical hole; the scheduled use passes the flag. |

---

## 10. Notes for whoever builds it

- `HistoricalStore.EnumerateMonths` already gives the month-segment walk; coverage wants an hour walk over the same window — a small addition beside it, not a new traversal concept.
- The per-file read is already on the shared seam (`TradeStoreWriter.ReadTradeFile`), so coverage reads the store through exactly the code the writer writes with. Do not add a second parse.
- A full-store pass at current size is ~120k rows/day of capture; a 6-month window will eventually be ~17 M rows. Stream the files and accumulate per-hour counters — do **not** materialise `LoadTradeRange` for this, which sorts and dedups the whole range in memory and is the wrong tool here.
