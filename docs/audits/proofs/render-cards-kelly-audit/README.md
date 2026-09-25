# Proof — Kelly sizing / placed-geometry disconnect

Supports the CRITICAL finding in
`docs/audits/2026-09-24-render-cards-kelly-audit.md` ("Kelly sizing computed from a fixed
ATR-multiple ratio, ignoring the actual placed structural stop/target").

`kelly_disconnect_proof.py` is a line-for-line Python re-implementation of
`Core/ScoringEngine_Kelly.vb::CalcKellySizing`, parameterized with the live
`settings.json` values (`scoring.atr_stop_multiplier`, `scoring.atr_target_multiplier`,
`kelly.*`), run twice:

1. With `b` computed exactly as the VB code computes it —
   `cfg.Scoring.AtrTargetMultiplier / cfg.Scoring.AtrStopMultiplier` — which is what the
   engine actually ships in the KELLY card, the plaintext snapshot, and
   `verdict_signal.json`'s `kelly` block.
2. With `b` set to `0.39` — the real placed R:R from `ComputeSideLevels`'s structural
   arbitration for the LOGIC TRACE scenario in the audit (STRONG LONG, ATR 120, post-flush
   structural target close overhead) — to show what the identical formula says once it is
   fed the trade the engine is actually about to place.

No VB/.NET file is included: the source formulas are cited by file/line in both the script's
docstring and the audit doc rather than reproduced as a compiled fixture, since this sandbox
has no `dotnet` toolchain to build against. The rename rule ("each `.vb` file to `.vb.txt`")
does not apply — there is no `.vb` proof file here, by design.

## Run command

```
python3 docs/audits/proofs/render-cards-kelly-audit/kelly_disconnect_proof.py
```

## Output obtained (verbatim, captured at audit time)

```
==============================================================================
WHAT THE ENGINE ACTUALLY RENDERS (both the KELLY card and
verdict_signal.json's `kelly` block):
==============================================================================
  b                = 1.09375
  p                = 0.65
  q                = 0.35
  KellyF           = 0.33
  KellyFHalf       = 0.165
  KellyFApplied    = 0.05
  KellyCapped      = True
  KellyRiskUsd     = 50.0
  KellyContracts   = 500
  KellyLevCapped   = True
  Notional         = 5000.0
  Leverage         = 5.0

==============================================================================
WHAT THE SAME FORMULA SAYS IF FED THE *REAL PLACED* R:R (0.39)
shown two inches below it in the ATR ENTRY LEVELS row this run:
==============================================================================
  b                = 0.39
  p                = 0.65
  KellyF           = -0.24743589743589736
  edge             = False

==============================================================================
DELTA
==============================================================================
  Shipped KellyContracts : 500  ($5000 notional, 5.00x leverage)
  Honest  KellyF*        : -0.2474  (NO EDGE -> size should be ZERO)
```

## Reading the result

The engine ships a 500-contract / $5,000-notional / 5.0×-leverage recommendation for a run
whose actual placed geometry, fed through the exact same Kelly formula, has no positive edge
at all (`f* ≈ −0.25`). Both numbers come from the same `VerdictResult`/`IndicatorResults` for
the same run; nothing in the codebase reconciles them.
