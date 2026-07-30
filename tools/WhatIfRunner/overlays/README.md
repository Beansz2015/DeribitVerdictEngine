# WhatIfRunner overlays — geometry-study recipes

Committed so the geometry-study re-read reproduces byte-identically without
depending on session-scratchpad files. Each JSON is a whitelisted overlay
consumed by `tools/WhatIfRunner`; the `sweep: {from, to, step}` shape is the
one `WhatIfOverlay.Parse` recognises (A36e fixture).

## Files

- **`geometry-study-36cell.json`** — the 36-cell geometry study: target arbitration
  mode × stop arbitration mode × target buffer_pct × stop buffer_pct
  (2 × 2 × 3 × 3 = 36 cells). Answers the "does the closer/wider arbitration mode
  or a buffered placed price change EV for a fixed hold horizon" question.
- **`target-bound-sweep.json`** — `target_max_atr_mult` sweep 1.25 → 3.5 step 0.25
  (10 cells). Answers the "does raising or lowering the ATR-multiple bound on the
  placed target change EV" question in isolation.
- **`w61-london-stop-grid.json`** — `stop_max_atr_mult` sweep 1.6 → 2.2 step 0.2
  (4 cells), everything else live. The W6-1 LONDON stop candidate named in the
  runner's own guard-rail §4 "registered use cases".
- **`geometry-4cell-mode-x-pivot.json`** — `target_arbitration_mode` {0,1} ×
  `use_best_pivot_candidate` {0,1} (4 cells). Pairs the v56 arbitration mode with
  the v63 D2-v2 best-pivot candidate so the P1 promotion question is measured
  against the ladder/nearest choice rather than in isolation.

Both were run for the pre-Aug-1 batch (item E) against a frozen copy of the live
book, full-book and `--from 2026-07-08`; raw rankings in
`docs/pre-aug1-batch-summary.md` §E.

## Re-run

From the repo root, against a frozen copy of `analysis_log.csv`:

```
dotnet run --project tools/WhatIfRunner -- tools/WhatIfRunner/overlays/geometry-study-36cell.json --csv <frozen copy> [--from yyyy-MM-dd]
dotnet run --project tools/WhatIfRunner -- tools/WhatIfRunner/overlays/target-bound-sweep.json     --csv <frozen copy> [--from yyyy-MM-dd]
```

Or with the published binary directly:

```
WhatIfRunner <overlay> --csv <frozen copy> [--from date]
```

The runner writes `whatif_report_<stamp>.md` beside the CSV (baseline vs overlay,
side by side); analysis-only, never writes `settings.json`.
