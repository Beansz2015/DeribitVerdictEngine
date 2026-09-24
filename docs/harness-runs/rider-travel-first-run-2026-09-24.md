# Rider-travel check (harness 1) — first real run, 2026-09-24 (UTC)

**Trigger:** the absorption S2 header rotation, commit `5dfc91a` (`docs/absorption-d2-s2-spec-back.md`, ruling `Q-4` (a): run before the push).
**Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md). Seat baseline written and committed first (`b1cfb1f`), then the detector ran.

## Inputs

| Input | Value |
|---|---|
| BEFORE header | `AnalysisLogger.vb` at `14b6179` (the commit before the rotation), 116 columns |
| AFTER header | `AnalysisLogger.vb` at `37b3582` (the tree), 124 columns |
| Ledger | `docs/csv-rotation-riders.md` as of `14b6179`, so the riders still read `TRAVELLING` (the S2 commit marked them `CONSUMED`) |
| Baseline | [`rider-travel-20260924T1749Z-baseline.json`](rider-travel-20260924T1749Z-baseline.json) |
| Report | [`rider-travel-20260924T1749Z-report.md`](rider-travel-20260924T1749Z-report.md) |
| Model | `jev-latest` requested 10 times, resolved `jev-1.13.0` 10 times; run 17:50:10 UTC |

Coverage: `RIDERS_IN_LEDGER=10` · `RIDERS_TRAVELLING=8` · `COLUMNS_ADDED=8` · `COLUMNS_REMOVED=0` · 6 decided by code, 2 by Jev · exit 0.

## Result

| Rider | Judge | Verdict | Seat baseline | |
|---|---|---|---|---|
| `RIDER-3` `TriggerMode` | code | arrived | arrived | agree |
| `RIDER-4` `WsHealth` | code | arrived | arrived | agree |
| `RIDER-5` `SettingsVersion` | code | arrived | arrived | agree |
| `RIDER-6` `SettingsLoadError` | code | arrived | arrived | agree |
| `RIDER-7` `RecentTradeCount` | code | arrived | arrived | agree |
| `RIDER-9` `VPFRSignal`, `VPFRPoc` | code | arrived | arrived | agree |
| `RIDER-1` (`.bak` naming in `EnsureLogFile`) | Jev, STABLE 5/5, mean top prob 0.794 | not_a_header_column | not_a_header_column | agree |
| `RIDER-2` (pooled reads, ops scripts) | Jev, STABLE 5/5, mean top prob 0.766 | not_a_header_column | not_a_header_column | agree |

**8 of 8 agree. No rider is missing.** The eighth added column, `AbsorptionShadowAggrUsd`, is the `D-6d.1` (c) column, not a rider.

## What this measures, and what it does not

- ⚠ **Weak evidence for the Jev arm.** Both Jev items are the easy class: `RIDER-1` and `RIDER-2` are the spec's own worked examples of a non-column rider (`rider-travel-check-spec.md` §2). A stable `not_a_header_column` on them is "a stable OK", which the protocol says is not evidence of skill.
- ⚠ **Declared recognition.** Before writing the baseline, the seat had read the implementer's report, which claimed all eight riders `CONSUMED`, and the spec's §2 examples. The seat read the eight added columns itself. So the baseline is not blind on any item; it is a check of the tool, not a measurement of the seat.
- ⚠ **The diagnostic Nouls disagree with the verdict, as the spec predicts.** `RIDER-1` reads `lands_in_header` 0.66 (above 0.5) while its verdict is `not_a_header_column`; both non-column riders read `column_present` 0.81 and 0.91. This is why `rider-travel-check-spec.md` §0 trap 1 forbids combining the Nouls. The verdict alone is correct.
- **Not verified:** that each arrived column is correct in position and value (presence only, by design); that the ledger is complete.
