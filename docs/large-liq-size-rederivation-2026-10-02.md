# `large_liq_size` re-derivation from the history store — READ

**Written:** 2026-10-02 (UTC) by the orchestrator seat, on the trader's go (item 5 of the day: apply [`history-data-store-spec.md`](history-data-store-spec.md) §2b now that the dev store exists).
**Read-only. Research only.** No `settings.json` edit, no `.vb` edit. Settings stays at v69. [`liquidation-park-spec.md`](liquidation-park-spec.md) puts this study out of that build's scope, and ruling `HSR-4` makes it research while the vote is parked.
**Data:** the dev history store `C:\DeribitData\history\`: 2025-01-01 → 2026-09-30, 71,766,893 BTC-PERPETUAL trades, liquidation flags final (the history host's flag, which arrives ~60 min late).
**Instrument:** [`audits/proofs/large-liq-size-rederivation-2026-10-02/derive.py`](audits/proofs/large-liq-size-rederivation-2026-10-02/derive.py), run at `10d7d8a`. Full output in [`output.txt`](audits/proofs/large-liq-size-rederivation-2026-10-02/output.txt) beside it. Runtime 2 min 25 s.

**IDs used here:**

| ID | Source and kind | Meaning |
|---|---|---|
| `L-3` | [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md), finding | The manual calls `large_liq_size` 200 BTC; the code compares it with USD sums |
| `D-6` (unit) | Same packet, R.3, a trader ruling of 2026-09-16 | Correct the manual's unit to USD; re-derive the value from real liquidation sizes. ⚠ **Not the same `D-6` as the stage-4 pass-rule decision in [`history-data-store-build-spec-back.md`](history-data-store-build-spec-back.md)** |
| `D-4` · `D-5` | Same packet, trader decisions | `D-4`: the live liquidation flag, re-ruled 2026-09-30 to (a) **park the vote**. `D-5`: book `M` to the maker's side, `T` to the taker's, `MT` to both (a rider in the park build) |
| `HSR-4` | [`history-store-queue-reshape-evaluation.md`](history-store-queue-reshape-evaluation.md), ruling | This study runs on the history store, gated on `D-4`; research only while the vote is parked |
| `LLS-1` | This read, a decision queued for the trader | The value to ship at un-park (section 4) |

---

## 0. Verdict

> - **The pre-stated rule gives `large_liq_size` ≈ 60,000 USD.** The rule is the ~90th percentile of observed `LiqLongSize`/`LiqShortSize` ([`trader-profile.md`](trader-profile.md) §7). The shipped 200 is 300× too small.
> - **Today 88 % of fired minutes take the LARGE penalty.** `large_liq_size` = 200 USD sits at the 10th–25th percentile. So "large" means "almost any", and the standard/large split does nothing. That is `L-3`'s consequence, now measured.
> - **The `D-5` booking does not move the threshold.** p90 = 59,800 USD under the current booking and 60,000 USD under `D-5`.
> - **`dominance_ratio` barely matters.** 97.8 % of minutes with any liquidation in the window are one-sided. The ratio decides only 0.9 % of those minutes. No re-derivation is needed.
> - ⚠ **The p90 varies by session and by period** (section 2). One pooled value is a choice, not a fact. It goes to the trader at un-park as `LLS-1`.

## 1. What was measured

The script mirrors `IndicatorEngine.CalcLiquidations` (`Core/Indicators_OrderFlow.vb:259-282`) and the penalty site (`Core/ScoringEngine_Calculate_Scoring.vb:400-405`):

- **Window:** the last 500 trades (the live `rtc=500`), taken at every UTC minute close. That is 918,719 minutes, a superset of the engine's 1-min and 3-min `ON_CLOSE` runs.
- **Sizes:** the sum of `Amount` (USD) of flagged trades, per side.
- **Signal:** `LONG LIQS` if L > 0 and L ≥ S × 2.0; `SHORT LIQS` if S > 0 and S > L × 2.0.
- **Penalty size:** the dominant side's sum.
- **Two bookings:** CURRENT, the code today (any flag; direction `buy` → short liquidation). And `D-5` (`T` → taker's side, `M` → maker's side, `MT` → both).

**Flagged trades:** `T` 51,557 · `M` 5,711 · `MT` 66, out of 71,766,893.

**Per-trade size (USD):** `T` p50 3,230, p90 18,690. `M` p50 5,460, p90 50,000.

## 2. Results

| Measure | CURRENT booking | `D-5` booking |
|---|---:|---:|
| Minutes with any liquidation in the window | 20,911 (2.28 %) | 20,911 (2.28 %) |
| Minutes where the signal fires | 20,729 (2.26 %) | 20,770 (2.26 %) |
| Dominant size when fired: p50 | 5,000 | 5,000 |
| p75 | 15,000 | 15,000 |
| **p90** | **59,800** | **60,000** |
| p95 | 137,240 | 137,240 |
| p99 | 472,846 | 487,310 |
| Fired minutes above the shipped 200 | **88.07 %** | **88.10 %** |

**p90 by segment** (CURRENT booking; `D-5` is within 3 %):

| Period | All sessions | ASIA | LONDON | NY |
|---|---:|---:|---:|---:|
| 2025 H1 | 60,090 | 91,000 | 32,800 | 41,768 |
| 2025 H2 | 57,170 | 77,650 | 57,170 | 44,366 |
| 2026 H1 | 68,448 | 56,220 | 106,740 | 60,360 |
| 2026 H2 (Jul–Sep only) | 30,376 | 25,051 | 85,310 | 22,500 |
| **All 21 months** | **59,800** | **69,430** | **80,240** | **48,512** |

- **The pooled p90 is stable from half to half** (57–68k) until 2026 H2. That half is 3 quiet months, and its p90 halves to ~30k.
- **The session p90s differ by up to 1.7×** (NY 48.5k vs LONDON 80.2k), and single cells swing more. The n per cell is 430–3,105 fired minutes.
- **Dominance:** two-sided windows number only 457 (CURRENT) and 342 (`D-5`). Their max/min ratio has p50 2.8. A ratio below 2.0 blocks the signal in 177 and 139 minutes respectively: under 1 % of liquidation minutes.

## 3. What this does NOT tell you

- **Whether the vote earns its penalty.** This is a size distribution, not an outcome study. Whether `LONG LIQS`/`SHORT LIQS` predicts anything is the A4 study and the vote-value work, neither of which this read runs.
- **The live distribution.** These flags are final. Live, the stream never carries them (finding `L-1`) and the history host flags ~60 min late. A real-time source would see the same trades, but only if one is found (the un-park condition in [`liquidation-park-spec.md`](liquidation-park-spec.md)).
- **Side flips under `D-5`.** About 10 % of flagged trades (`M`) change side under `D-5`. I did not count how many fired minutes change direction.

## 4. Queued for the trader — not applied

| # | Question | Options | My read |
|---|---|---|---|
| **`LLS-1`** | What `large_liq_size` to ship **at un-park** (not before: the vote is parked, so a value change now does nothing live and lands mid-instance for no reason) | (a) pooled p90, **60,000 USD** · (b) per-session values, a new settings shape · (c) re-derive at un-park on the then-current store | **(c), seeded with (a).** The 2026 H2 p90 is half the pooled value. A threshold fixed now would be 2+ months stale at un-park. (a) is the default if un-park needs a number at once. (b) buys accuracy but adds a settings shape for a parked vote. ⚠ (b) is the more informative option; I am not picking it because the vote is parked, not because (a) is "adequate". **Reserved:** `settings.json` + scoring |

✅ **`LLS-1` RULED 2026-10-02 (trader): (b), per-session values** — "liquidations should differ between session types". Where it takes effect:

- **The threshold is read in one place only:** the Step 2 penalty (`Core/ScoringEngine_Calculate_Scoring.vb:400-405`). Live it never fires (finding `L-1`).
- **Replay fires it today**, with 200 (`tools/BacktestRunner/ReplayLoop.vb:494` and `:628`). The park build (`LP-2` (a), [`liquidation-park-spec.md`](liquidation-park-spec.md) §4.3) removes the penalty from replay too, so replay matches live. From then until un-park, nothing reads the threshold.
- **Liquidation research uses the sizes, not the threshold.** It passes the per-session values as study parameters. Use this read's `D-5` row: ASIA 69,535 · LONDON 83,250 · NY 49,724 USD.
- **Code shape:** a per-session `large_liq_size` is a settings shape + scoring change (reserved). ✅ **TICKED 2026-10-02 (trader):** built as Rider 3 of the park build ([`liquidation-park-spec.md`](liquidation-park-spec.md) scope table), after 2026-11-25, with values re-derived by `H-1` on the then-current store under the `D-5` booking.

**Auto-proceeded:** 1-minute sampling instead of replaying the engine's exact run instants. The p90 of a 918k-minute superset does not move enough to change any option above. The exact replay needs the logged run grid, which exists only for the box era.

## 5. Handle

**`H-1`** — re-runs the whole read in ~2.5 min. Needs the dev store.

```bash
python docs/audits/proofs/large-liq-size-rederivation-2026-10-02/derive.py
```

Pasted (head; full output in `output.txt`):

```text
STORE C:/DeribitData/history | minute samples 918719 | flags {'none': 71709559, 'T': 51557, 'M': 5711, 'MT': 66}
== booking CUR | minutes with any liq in window: 20911 (2.28 %) | signal fired: 20729 (2.26 %)
  dominant size when fired (USD): p10=150 p25=1,500 p50=5,000 p75=15,000 p90=59,800 p95=137,240 p99=472,846 max=4,210,130
  share of fired minutes ABOVE the shipped 200 (=> large penalty today): 88.07 %
== booking D5 | minutes with any liq in window: 20911 (2.28 %) | signal fired: 20770 (2.26 %)
  dominant size when fired (USD): p10=150 p25=1,500 p50=5,000 p75=15,000 p90=60,000 p95=137,240 p99=487,310 max=5,151,530
```

## 6. ⚠ What I did not verify

| Claim | Status |
|---|---|
| Session hours | Taken from `settings.json` `session_volume`, end hour read as inclusive (ASIA 0–7, LONDON 8–12, NY 13–23). Not checked against `DynamicNorms` |
| The engine's window is exactly the last 500 trades at run time | From `analysis_log.csv` `rtc=500` and the call site (`UI/MainForm_Analysis.vb:453`); the trade-list order rule (`CLAUDE.md`, chronological ascending) not re-checked |
| `D-5`'s maker-side mapping (`M` + `buy` → long liquidation) | My reading of the ruling's text ("`M` to the maker's side"); the park spec's build will define it in code |
| `docs/UserManual.md` still says 200 BTC | The `D-6` (unit) manual fix is ruled but belongs to the park build; not checked here |
