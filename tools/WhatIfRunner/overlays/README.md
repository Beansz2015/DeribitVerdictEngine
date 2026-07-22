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
