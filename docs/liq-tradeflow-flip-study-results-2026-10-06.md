# Liquidation × trade-flow flip study — results (session 2, 2026-10-06)

Spec of record: [`liq-tradeflow-flip-study-spec.md`](liq-tradeflow-flip-study-spec.md). Its §14 is the re-registration, §15 the tools and feasibility, §16 the full result. Run output: `docs/audits/proofs/liq-tradeflow-session2-2026-10-06/run3-output.txt`.

## Verdict

- **All three tests read NO DIFFERENCE SHOWN.** None passes Holm (three tests, familywise 0.05; ruling `A4L-9` (b), Holm across all tests in a study, in [`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) §9).
- **No engine change follows.** The live liquidation vote stays parked (ruling `D-4`, a trader decision in [`medium-tier-bug-hunt-spec-back.md`](medium-tier-bug-hunt-spec-back.md) §R.3).

## Population and measure

| Item | Value |
|---|---|
| Span | 2023-01-01 → 2026-07-02. The fence (2026-07-03 00:00 UTC) was never crossed |
| Events | 984 measured large-liquidation onsets. FLIP 171: taker flow turns against the cascade in the 60 s after the last liquidation print. NO-FLIP 813 |
| Control | 201,035 flow-only windows on a 5-min grid, traded with the flow |
| Outcome | The engine's placed levels, replayed at D with the shipped `Core/` (ruling `TFS-8` (d)). House net EV per trade, maker/maker 3 bps; windows NY 15 min, ASIA and LONDON 45 min |
| Feasibility | Replayable. Tape-built candles match the venue's candles (98 % of 1m bars identical). The placed-level reasons agree on 99 % of the event instants checked |

## The three tests

| Test | Effect, bps [95 % CI] | Holm | Label |
|---|---|---|---|
| `TFS-H1` FLIP − NO-FLIP, re-weighted by session × side | +2.8 [−2.2, +7.8] | not passed (p 0.27) | NO DIFFERENCE SHOWN |
| `TFS-H2` FLIP fade vs 0 | −1.1 [−5.1, +2.9] | not passed (p 0.59) | NO DIFFERENCE SHOWN |
| `TFS-H3` FLIP − flow-only control, re-weighted by session × side × ATR tercile | +2.0 [−2.1, +6.1] | not passed (p 0.32) | NO DIFFERENCE SHOWN |

- `TFS-H1` … `TFS-H3` are this study's hypotheses ([`liq-tradeflow-flip-study-spec.md`](liq-tradeflow-flip-study-spec.md) §5).
- The trailing 90-day thresholds change no sign.
- Measured σ of net EV per trade: FLIP 25.7 bps, NO-FLIP 38.2 bps.

## What it licenses

- The trade-flow flip adds no measurable edge to the liquidation fade. That holds against the fade without a flip and against following the flow alone. The upper CI bounds put any lift at about +8 bps at most.
- The fade loses after fees in every arm: FLIP −1.1, NO-FLIP −4.2, control −3.0 bps per trade.
- It weakens, but does not settle, the case for A4 (the liquidation × OFI flip detector in [`post-websocket-post-calibration-backlog.md`](post-websocket-post-calibration-backlog.md), section A). The sealed OFI sister study is untouched.
- It licenses nothing for `settings.json` or scoring. A positive result could not have shipped either: finding `L-1` (the live stream carries no liquidation flag) blocks any live use.

## Not licensed

- The LONDON split (+14.6 [+0.6, +30.0] bps on 21 FLIP rows) is one of about 20 descriptive cells. It is not a finding.
- Half-years and variants are descriptive only (rulings `TFS-10` and `TFS-15` in [`liq-tradeflow-flip-study-spec.md`](liq-tradeflow-flip-study-spec.md) §9 and §14.7).

## Process notes

- The registered run was launched three times, with no change of design:
  - Launch 1 was killed when its session ended.
  - Launch 2 crashed on a bug: control rows lack a trailing class.
  - Launch 3 completed.
- No test statistic was printed before launch 3. Each launch's printed lines match the next launch exactly. Details: [`liq-tradeflow-flip-study-spec.md`](liq-tradeflow-flip-study-spec.md) §15.6.
