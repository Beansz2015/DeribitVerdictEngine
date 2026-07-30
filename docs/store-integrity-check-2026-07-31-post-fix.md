# Store integrity re-check after `aaee7c6` — funding fixed, **June candles damaged**

**Seat:** Opus orchestrator, 2026-07-31 (post-fix). **Trigger:** verifying the implementer's funding-gap fix at the trader's request.
**Result: the funding fix is complete and correct. It arrived alongside a new, separate data loss in the candle store.** Recoverable, but nothing candle-derived should be re-run until it is repaired.

---

## 1. What was fixed — verified

**Funding: complete.** The 28.2-day hole I reported ([coverage-report review §0](trade-store-coverage-report-review-2026-07-31.md)) is gone, and so are the five stray single-sample drops:

| | before | after |
|---|---:|---:|
| Samples | 3,644 | **4,336** |
| Expected (hourly) | 4,326 | **4,336** |
| Missing | 682 (15.8%) | **0** |
| Gaps > 2 h | 1 × 28.2 days | **none** |

Span 2026-01-31 04:00 → 2026-07-30 19:00 UTC, contiguous.

**F1 also fixed, exactly as proposed.** `TradeStoreWriter.ShouldCapture` now exists on the seam ([Core/TradeStoreWriter.vb:154](../Core/TradeStoreWriter.vb)); `DeribitWsFeed.ResolveTradeStore` calls it, and **A48f now calls the production gate** instead of a mirrored predicate. That closes the claim-strength gap in the v64 review.

---

## 2. The new problem — June is gone from 3m / 5m / 15m

Re-running the same completeness check that returned **0 missing at all four resolutions** before the fix:

| Resolution | Have | Expected | Missing | |
|---|---:|---:|---:|---|
| 1m | 260,137 | 260,137 | **0** | intact |
| **3m** | 72,713 | 86,713 | **14,000 (16.1%)** | ← |
| **5m** | 43,628 | 52,028 | **8,400 (16.1%)** | ← |
| **15m** | 14,543 | 17,343 | **2,800 (16.1%)** | ← |

All three are missing **the same window**: `2026-05-31 23:57 → 2026-06-30 04:00 UTC` — essentially **all of June 2026**. `14,000 × 3 = 8,400 × 5 = 2,800 × 15 = 42,000 minutes`, one hole, three resolutions.

Damage is **bounded to exactly three files**. Every other month at every resolution is intact, and 1m is untouched throughout:

| | Feb | Mar | Apr | May | **Jun** |
|---|---:|---:|---:|---:|---:|
| 1m | ok | ok | ok | ok | **43,200 ok** |
| 3m | ok | ok | ok | ok | **400 / 14,400** |
| 5m | ok | ok | ok | ok | **240 / 8,640** |
| 15m | ok | ok | ok | ok | **80 / 2,880** |

Each surviving June file holds **only the last ~20 hours of the month** — `2026-06-30 04:00 → 23:5x`, i.e. 19.95 / 19.92 / 19.75 hours. The month file was **overwritten with a short window rather than appended to**.

### What caused it — facts, not a diagnosis

- **The commit's code changes are funding-only.** `git show aaee7c6 -- tools/BacktestRunner/HistoricalStore.vb` touches `BackfillFundingMonthAsync`, `FetchFundingHistoryAsync`, `ExpectedFundingSamples`, `LoadFundingFile`, `LoadFundingRange`. **`BackfillCandleMonthAsync` is not in the diff at all** — zero hunks.
- **The damage is therefore from the fetch *run*, not from this commit's code.** File mtimes place it precisely: funding rewritten 03:35:18–03:35:24, the three damaged candle files 03:36:22–03:36:28. `candles_1m_2026-06.csv` still carries its **original 00:53:26** mtime — it was never re-written, which is why 1m survived.
- **The ~20-hour survivor window resembles `gap_repair_lookback_hours: 20`, but I am not asserting a link.** Gap repair backfills **trades only** (`TradeStoreGapRepair` → `BackfillTradeMonthAsync`), so it has no candle path. The resemblance is suggestive and nothing more; diagnosing it belongs to whoever owns the candle backfill, with the month-segment start computation the obvious first place to look.

### Recovery

**No git recovery** — `backtest_data/` is gitignored and untracked (`.gitignore:417`).

**But this is fully recoverable**, unlike the trade store: candles have **no venue retention cap**, which the v64 spec's own §0 establishes and the original 6-month fetch proved. Re-fetch 3m/5m/15m for 2026-06 and the store is whole again.

---

## 3. Does this change the JOB 2 derivations? **No — but do not re-run them until this is repaired.**

Three separate questions, answered separately:

**(a) Did the funding hole ever affect JOB 2?** No, and I checked rather than assumed at the time. None of the four derivations consumed funding — they read candles only, and the completeness check is recorded in [`candle-store-derivations-2026-07-31.md` §0.1](candle-store-derivations-2026-07-31.md).

**(b) Are the JOB 2 numbers still valid as computed?** **Yes.** They were computed against a store verified at **0 missing bars at all four resolutions**, and the transcription pins (0 mismatches on 16,000 windows) plus the matched-timestamp production replay (98.35% / 99.22% row agreement) are all recorded. Nothing about the funding fix retroactively changes those inputs.

**(c) Would re-running today reproduce them?** **No — it would produce worse numbers**, because the store on disk is now missing June at 3m/5m/15m. That is the operationally important point:

| JOB 2 arm | Resolution | Affected if re-run now |
|---|---|---|
| TTM 1m · NY | 1m | no |
| OBV 1m · NY | 1m | no |
| Volume 1m · NY | 1m | no |
| **TTM 3m · ASIA/LONDON** | 3m | **yes — loses ~16% of the sample** |
| **OBV 3m · ASIA/LONDON** | 3m | **yes** |
| **Volume 3m · ASIA/LONDON** | 3m | **yes** |
| **Swing pivots** | 5m | **yes** |

So the recommendations stand as published, and a re-run is **not** needed to validate them — but anyone who re-runs the harness before the store is repaired will get a quieter, June-less sample and may reasonably think my figures were wrong. **Repair first, then re-run only if you want confirmation.** When it is repaired, a re-run is a ~30-second check and worth doing as a matter of hygiene, since the store will then also carry an extra day (1m grew 259,974 → 260,137 as the fetch extended to 07-30 19:36).

---

## 4. The wider point

**This is the second real, undetected hole in this store inside 24 hours** — a 28-day funding gap that had gone unnoticed since it formed, and now a one-month, three-resolution candle gap created by the very run that fixed the first one. Both were invisible until someone counted rows against a deterministic expectation.

That is precisely the argument of [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md), and its S4 — *"nearly free and catches a class S1–S3 cannot: a bad or truncated fetch … a silent hole there corrupts a threshold recommendation with no symptom at all"* — would have caught **both**, immediately, from a single command. The spec's premise has now been validated twice by events in one day. **Worth raising its priority accordingly**, and worth running the check after every fetch rather than only on a schedule.
