# Evaluation of `history-store-queue-reshape-proposal.md` (2026-09-29 UTC)

**By:** the seat that wrote [`history-data-store-spec.md`](history-data-store-spec.md), at the trader's request. **Proposal:** [`history-store-queue-reshape-proposal.md`](history-store-queue-reshape-proposal.md). **Verdict: FEASIBLE, with four rows narrowed and one item the proposal missed.** Nothing is actioned here except two small doc pins (§3); the `HSR-*` rulings are the trader's.

## 1. Row by row

| ID | Verdict | Notes |
|---|---|---|
| `HSR-1` | ✅ Agree — drop | The zero is the flag never reaching the stream (`L-1`), measured again this week: the flag arrives ~60 min late on the main host, and not at first delivery on the raw channel ([`raw-channel-liquidation-test-spec-back.md`](raw-channel-liquidation-test-spec-back.md) §7) |
| `HSR-2` | ✅ Agree, **with a condition** | Retire the three-era rule only after the stage-2b validation covers the two damaged spans specifically: 2026-08-10/11 (the write-guard era edge) and 2026-09-18 → 21 (the outage). Until the dev store exists, the rule stays |
| `HSR-3` | ✅ Agree — **can be done now** | Quote-and-label is docs only; no gate needed |
| `HSR-4` | ✅ Agree, **gated on `D-4`** | `large_liq_size` and `dominance_ratio` only matter live if `D-4` finds a live source (option (d): the history host, if it flags at first sight). If the vote is parked, this is research only and low priority. The `D-5` side rule caveat is right |
| `HSR-5` | ✅ Feasible, **two conditions** | (1) A replay parity check first: replayed `AggrVelBurstRatio` against the logged value, at the logged run instants, over the collector era. The replay folds on trade exchange timestamps like live (`tools/BacktestRunner/ReplayLoop.vb:279`, `Core/AggressorVelocityAccumulator.vb:72`), so parity is expected but not yet shown. (2) A 21-month reference is a **new ruling** superseding `AVR-1`'s frozen table |
| `HSR-6` | ✅ Agree | The replay must reproduce the live CVD trade window (the latest 500 trades per run) |
| `HSR-7` | ⚠ **Narrower** | TTM is candle-only, so the store adds nothing to TTM itself. What it adds is trade coverage for a verdict-level replay of the change. Gate: stage 2b **and** the synthesizer's §7.1/§7.5 clearance ([`backtest-synthesizer-proposal.md`](backtest-synthesizer-proposal.md)), plus `HSR-11` inside the collector era |
| `HSR-8` | ✅ Agree | TFI is trade-derived (30-trade window) |
| `HSR-9` | ✅ Agree | No work created |
| `HSR-10` | ⚠ **Narrower** | The §7.4 re-validation compares replayed verdicts with **live** verdicts, so its span is capped by the logged-verdict era, not by 21 months of trades. The backfill widens it from the box store's start (2026-07-22) to the start of the logged books. It does **not** fix the §7.1 forming-bar convention gap, which is the actual blocker named there |
| `HSR-11` | ✅ Consistent with "no fabricated book state", **with rules** | Observed values, joined by the run row (InstanceId + SignalId, or Timestamp + resolution), only on bars that have a live row; the replay's run instant must be the live run instant. The joined values carry the settings of their era (book depth, thresholds): re-thresholding the logged `OFIRatio` is fine, a different depth is not. ⛔ Keep the synthesizer's ban on live-population rates (Kelly win rates, tier rates) — a joined replay is still not the live population (trade completeness and settings eras differ) |
| `HSR-12` | ✅ Agree | |

## 2. Answers to the proposal's §6 questions

1. **Missed:** the `trader-tick-queue.md` §4 standing watch **"liq_events CASCADE ⇒ A4"**, and the `A4` gate text "Market-gated only (≥1 CASCADE line)". A CASCADE line cannot be written while the live stream never carries the flag. Re-gate both on `D-4`, like `HSR-1`. Nothing else in §2–§3 is already covered by `history-data-store-spec.md` §2a/§2b beyond what the proposal notes.
2. **`HSR-5` vs run 1's pre-registration: no conflict.** `tools/ops/burst_outcome_read.py` carries its own frozen copy of the ATR-fifth edges (lines 74–77, provenance commit `b5b4a7d`); a new watch reference cannot move run 1's bins. ⚠ `burst-outcome-read-spec.md` §5 pointed at the *watch script's* edges, which invites exactly that misreading. **Pinned in this commit** to the literal values.
3. **`HSR-11`: yes**, under the rules in its row above.
4. **Stage-2 build:** no design change. One acceptance handle added (**`H-6`**, `history-data-store-spec.md` §5): `ReplayLoop` reads a dev-store month file with the four extra columns (`HDS-1`) unchanged. `TryParseRow` tolerates longer rows, but no replay has run on one yet.

## 3. Doc changes made with this evaluation

- `burst-outcome-read-spec.md` §5, ATR-fifth row: pinned to the literal edges run 1 uses.
- `history-data-store-spec.md` §5: handle `H-6` added.

## 4. What I did not verify

- Replay parity for `AggrVelBurstRatio` (`HSR-5` condition 1) — not run.
- The start date of the logged-verdict books (`HSR-10`'s span) — not checked.
- That `TryParseRow` reads an 11-column row in practice — read from the spec's claim, not run.
