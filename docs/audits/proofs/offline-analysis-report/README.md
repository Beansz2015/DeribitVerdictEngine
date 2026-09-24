# Proof code — offline analysis report audit (2026-09-24)

Audit: [`../../2026-09-24-offline-analysis-report.md`](../../2026-09-24-offline-analysis-report.md)

## What this is

`trace.py` is a **Python replica** of the VB pipeline, written branch-for-branch against
`ForwardWindowJoiner.PopulateForwardBars`, `FailureRateMatrix.Compute` / `WalkBars` / `WilsonCI`,
`AnalysisRunner.ComputeContextOutcomes` (filter at `analysis/AnalysisRunner.vb:278-283`) and
`OutlierAudit.ComputeOiCvdAsymmetry` (`analysis/OutlierAudit.vb:95-107`).

⚠ **It is not the VB binary.** The audit container had no .NET SDK, so the VB could not be compiled
or run. There was also no `analysis_log.csv` in the tree, so the input is a **synthetic** flush day
(seeded, deterministic). The numbers prove *mechanisms*, not figures from the live book. There is no
`.vb` proof file, so nothing needed the `.vb.txt` rename.

The fee-inclusive EV column is NOT something the report computes — it is added by the replica to
show what the report omits (target path −3 bps, stop/ambiguous path −5 bps, window expiry
mark-to-close −5 bps).

## Run command

From the repo root (Python 3, standard library only):

```
python3 docs/audits/proofs/offline-analysis-report/trace.py
```

## Output obtained (2026-09-24, run from the committed copy)

The first line's dict key ORDER varies between runs (Python set iteration order); the counts and
every other line are deterministic under `random.seed(7)`.

```
--- verdict mix: {'LONG': 3, 'NO TRADE [WEAK SHORT]': 22, 'WEAK LONG': 16, 'NO TRADE': 38, 'SHORT': 8, 'STRONG SHORT': 6, 'STRONG LONG': 7}
tier            W    n    succ Wilson(succ)       EV bps outcomes
MEDIUM_LONG     5    3     33% [  6%- 79%]      -2.0 {'SUCCESS': 1, 'WINDOW_EXPIRED': 2}
MEDIUM_LONG    10    3     33% [  6%- 79%]      -3.7 {'SUCCESS': 1, 'WINDOW_EXPIRED': 2}
MEDIUM_LONG    15    3     33% [  6%- 79%]      -7.1 {'SUCCESS': 1, 'WINDOW_EXPIRED': 1, 'ADVERSE_HIT': 1}
MEDIUM_SHORT    5    8     38% [ 14%- 69%]       2.9 {'WINDOW_EXPIRED': 5, 'SUCCESS': 3}
MEDIUM_SHORT   10    8     75% [ 41%- 93%]       7.4 {'SUCCESS': 6, 'WINDOW_EXPIRED': 2}
MEDIUM_SHORT   15    8     88% [ 53%- 98%]       8.7 {'SUCCESS': 7, 'WINDOW_EXPIRED': 1}
STRONG_LONG     5    7     43% [ 16%- 75%]      -0.2 {'SUCCESS': 3, 'WINDOW_EXPIRED': 4}
STRONG_LONG    10    7     43% [ 16%- 75%]      -3.2 {'SUCCESS': 3, 'WINDOW_EXPIRED': 2, 'ADVERSE_HIT': 2}
STRONG_LONG    15    7     43% [ 16%- 75%]      -3.7 {'SUCCESS': 3, 'WINDOW_EXPIRED': 2, 'ADVERSE_HIT': 2}
STRONG_SHORT    5    6     33% [ 10%- 70%]      -8.0 {'SUCCESS': 2, 'ADVERSE_HIT': 4}
STRONG_SHORT   10    6     33% [ 10%- 70%]      -8.0 {'SUCCESS': 2, 'ADVERSE_HIT': 4}
STRONG_SHORT   15    6     33% [ 10%- 70%]      -8.0 {'SUCCESS': 2, 'ADVERSE_HIT': 4}
STRONG_SHORT: 6 rows but 1 distinct runs (overlapping 15m windows)
MEDIUM_SHORT: 8 rows but 4 distinct runs (overlapping 15m windows)
context-table rows that are NOT trades (EVAL-1): 22 e.g. NO TRADE [WEAK SHORT] -> walked as SHORT
res-3 row ForwardBars keys: [15, 30, 45] | context fallback w=10 present? False
OIxCVD 3 regimes x (8L,1S): ASYMMETRIC_ALGORITHM  <- no regime has n>=10; spread=0-1=-1
OIxCVD one regime (40L,2S) + one (0L,9S): INCONCLUSIVE
first eligible bar opens THROUGH target: 7  THROUGH stop: 4  mean signed entry drift T0->T+2m (USD, + = favourable): 34.7  range -170 443
```
