# D3 evidence lane — closed-bar volume A/B on the backtester

**Ordered by:** the Fable seat's D3 ruling (2026-07-31) — *"the closed-bar volume A/B runs on the BACKTESTER first (volume is candle-derived — squarely in the cleared class, and this is now its registered use case)."*
**Run:** 2026-07-31 (Opus). **Tools-only.** No engine file, no `settings.json`, no live behaviour. Settings **v63**.
**Status:** evidence produced, **no recommendation made.** Any live change remains its own maximal-⚠ D-table, sequenced after F1/Kelly-CAL per the ruling.

---

## 1. What was run

Two replay arms over the same window, from the same historical store, with **one variable**:

| Arm | Terminal bar of every candle series |
|---|---|
| **stub** (default, `--` no flag) | the §7.1 forming stub — trades in `[closeMs, closeMs + 2 s]`. **This is what live sees.** |
| **closed** (`--closed-bars`) | the last fully-**closed** bar. No stub. |

`ReplayLoop.Run` gained `Optional useFormingStub As Boolean = True`. The closed arm slices **N** closed bars where the stub arm slices **N−1** and appends, so **total window length is held constant** and the terminal bar is the only difference. Trade slicing is byte-identical in both arms — the closed-bar question is about candles, and moving two things at once would not answer it.

**Window:** 2026-07-29 12:00 → 07-30 08:00 UTC, **840 rows per arm** (660 res-1, 180 res-3). This is the full span the trade store covers — Deribit's public trades endpoint serves ~24 h, the constraint recorded in the synthesizer proposal §7.3.

**Fixture:** **A47a** pins the arm shape, including one property worth stating because it is easy to assume away — holding total length constant means the **closed arm reaches one bar further back**. The arms therefore differ in exactly two places: the terminal bar (under test) and one extra *old* bar. For tail-window indicators (`VolumeRatio`'s SMA-9, ATR-7) that old bar falls outside the window and the comparison is clean; for full-series indicators (VWAP session window, OBV `meanVol`, BBW percentile series) it is a ~1/N perturbation riding along.

---

## 2. A defect found on the way, and fixed before any result was read

The first run produced a **stub arm with *higher* volume than the closed arm** — p50 `VolumeRatio` 2.3547 against live's 0.0123, with 48.1 % of rows at exactly zero and a dense cluster at ~8.9–9.0.

That shape is arithmetic, not market behaviour: `ratio = V / ((8 real + V)/9) → 9` as `V` dominates the SMA-9. Root cause: **`BuildFormingStub` summed `TradeRecord.Amount` straight into `Candle.Volume`.** On Deribit perpetuals `Amount` is **USD notional** (contracts are $10); `Candle.Volume` is **base currency (BTC)** — the chart endpoint's `volume`, with `cost` (USD) landing in `VolumeUSD`. Store evidence: mean 1m candle `Volume` **2.3937** (and `cost/volume ≈ spot`), mean trade `Amount` **2909.10**. A ~**64,000×** unit error in every stub since §7.1 shipped.

Fixed (`ae8a1f6`): `Volume = Σ(amount / price)`, `VolumeUSD = Σ amount`. Pinned by new fixture **A47b**, which checks the stub against *real store scale* rather than against itself — the check A43f structurally could not make, since internal-consistency arithmetic cannot detect a unit error.

**Effect on synthetic↔live agreement** — same window, same 840 rows, only the stub arithmetic changed:

| Column | before | after |
|---|---:|---:|
| **VWAP** | 56.19 % | **100.00 %** |
| VWAPSigma1Upper / 2Upper | 55.00 / 54.52 % | **100.00 / 100.00 %** |
| VWAPSigma1Lower / 2Lower | 55.00 / 53.45 % | 99.88 / 99.76 % |
| VWAPDevPct | 40.60 % | 78.21 % *(bounded by Price at 77.62 %)* |
| **VolumeRatio** | 23.57 % | **65.00 %** |
| **OBVTrend / OBVDivergence** | 71.43 / 84.17 % | **99.76 / 99.52 %** |
| **Verdict / Tier agreement** | 74.05 / 81.43 % | **79.64 / 86.19 %** |
| ATR / ADX / RSI | 46.55 / 49.76 / 42.98 % | unchanged |

**This supersedes the D2 ruling and my own hypothesis behind it.** There was no noise floor and no mis-scoped tolerance — VWAP now agrees **exactly** on all 840 rows. It also resolves the §9.5 OBV regression, which had been attributed to the stub's *near-zero* volume dragging `meanVol` down; the cause was the stub's *64,000×-oversized* volume. ATR / ADX / RSI are untouched and remain the honest residual, with separate causes.

---

## 3. The A/B result

All figures post-fix. Live is the same window from the frozen local book, for reference.

| | **stub** (live mirror) | **closed bars** | *live actual* |
|---|---:|---:|---:|
| p50 `VolumeRatio` | 0.0000 | **0.6001** | *0.0123* |
| p90 `VolumeRatio` | 0.0883 | **2.4290** | *0.1920* |
| mean | 0.0460 | **1.0058** | *0.1000* |
| rows at exactly 0 | 50.0 % | 0.5 % | — |
| **VR ≥ 1.5** (partial vote possible) | 0.48 % | **22.26 %** | *1.28 %* |
| **VR ≥ 3.0** (the trader's breakout gate) | 0.00 % | **6.19 %** | *0.51 %* |

**Reading closed bars raises the breakout gate from ~0–0.5 % to 6.19 %, and the partial-vote threshold from ~0.5–1.3 % to 22.26 %.** Roughly **12× and 17×** against live.

This independently reproduces lane C's counterfactual, which reached the same place by a different route — reading raw closed candles straight out of the store (NY×1: p50 0.525, VR ≥ 3.0 = 8.47 %). Two methods, one answer.

**The stub arm now tracks live closely** (p50 0.0000 vs 0.0123; gate 0.00 % vs 0.51 %), which is what makes the closed arm interpretable — the mirror is faithful, so the difference between arms is the change under test rather than synthesizer error.

### 3.1 What it does to verdicts

| Arm | rows | directional | STRONG | MEDIUM | WEAK |
|---|---:|---:|---:|---:|---:|
| stub | 840 | 216 (25.7 %) | 6 | 44 | 166 |
| closed | 840 | 218 (26.0 %) | 7 | 36 | 175 |

**A 12–17× increase in volume-signal engagement moves the directional share by 0.3 pp.** The tier mix shifts slightly (MEDIUM 44 → 36, WEAK 166 → 175) but the headline population is effectively unchanged on this window.

Stated as a fact, not a conclusion: the volume vote is one of ~20 inputs into integer-ceiling thresholds, so a large change in one input's *engagement* need not move the *verdict* distribution. Whether that makes the live change cheap (little disruption) or pointless (little benefit) is exactly the trade-off the D-table exists to weigh, and it is not decided here.

---

## 4. What this does NOT establish

- **No outcome measurement.** This compares signal engagement and verdict mix, not win rate or EV. The 840-row window yields ~217 directional rows — thin for outcome statistics, and the two arms would need a placed-vs-placed re-walk to compare honestly.
- **One 20-hour window, one price regime.** Bounded by the ~24 h trades cap (proposal §7.3), not by choice. The append-forward store fetch (ops item F) is what widens this.
- **The A/B is synthesizer-internal.** Both arms are replay. It answers "what would the engine have seen," not "what would have happened."
- **The one-extra-old-bar confound** (§1) rides on the full-series indicators. Immaterial for the volume result, which is tail-window.
- **Nothing here touches live.** No engine file was opened.

---

## 5. Reproduction

```bash
dotnet run --project tools/BacktestRunner/BacktestRunner.vbproj -c Release -- replay --from 2026-07-29T12:00 --to 2026-07-30T08:00 --out ab_stub.csv
```

```bash
dotnet run --project tools/BacktestRunner/BacktestRunner.vbproj -c Release -- replay --from 2026-07-29T12:00 --to 2026-07-30T08:00 --out ab_closed.csv --closed-bars
```

Commits: `ae8a1f6` (unit fix + `--closed-bars`), `4bc6c93` (A47a/A47b + A43f correction). verify-gate `prepush` **GATE PASSED**, A1–A46a unregressed.
