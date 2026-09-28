# Proof artifacts: backtest/ceiling remainder audit (2026-09-25)

Companion to [`docs/audits/2026-09-25-backtest-ceiling-remainder.md`](../../2026-09-25-backtest-ceiling-remainder.md).

All proofs run the **shipped** code at commit `6e74181`: the coverage proofs run the real
`BacktestRunner coverage --strict` CLI; the CeilingAudit and What-If proofs link the real
`tools/CeilingAudit/*.vb` and `tools/WhatIfRunner/*.vb` through project files that copy the
shipped projects' own `Compile Include` lists. `.vb` and `.vbproj` files carry a `.txt`
suffix here because the root project compiles every `.vb` outside `tools/` and `verify/`.

Run environment: Linux, .NET SDK 8.0.131 (`apt-get install -y dotnet-sdk-8.0`), Python 3.

## Files

| File | What it is |
|---|---|
| `gen_coverage_fixtures.py` | Writes seven evidence directories (store, candles, funding, `analysis_log.csv`, `ws_health.log`, `capture_marker.log`, `venue_status.log`) for scenarios S-A … S-E1 |
| `run_coverage_proofs.sh` | Generates the fixtures and runs the shipped CLI with `--strict --out` on each; prints counts, verdict, the markdown hour table and the exit code |
| `coverage_proof_output.txt` | Output of the run below |
| `ProofCeiling.vbproj.txt`, `ProofCeiling.Program.vb.txt` | CeilingAudit proofs P-CA1 … P-CA4 |
| `ceiling_proof_output.txt` | Output of the run below |
| `ProofWhatIf.vbproj.txt`, `ProofWhatIf.Program.vb.txt` | `WhatIfReport.Build` proofs P-WI1, P-WI2 |
| `whatif_proof_output.txt` | Output of the run below |

## Exact commands run

```bash
git worktree add /tmp/audit-6e74181 6e74181
cd /tmp/audit-6e74181

# 1. Coverage (shipped CLI)
dotnet build tools/BacktestRunner/BacktestRunner.vbproj -c Release
WT=/tmp/audit-6e74181 FIX=/tmp/claude-0/proof/fixtures \
  bash <this dir>/run_coverage_proofs.sh          # gen_coverage_fixtures.py must sit beside it

# 2. CeilingAudit
mkdir -p tools/proofceiling
cp <this dir>/ProofCeiling.vbproj.txt      tools/proofceiling/ProofCeiling.vbproj
cp <this dir>/ProofCeiling.Program.vb.txt  tools/proofceiling/Program.vb
dotnet run --project tools/proofceiling/ProofCeiling.vbproj -c Release

# 3. What-If report
mkdir -p tools/proofwhatif
cp <this dir>/ProofWhatIf.vbproj.txt      tools/proofwhatif/ProofWhatIf.vbproj
cp <this dir>/ProofWhatIf.Program.vb.txt  tools/proofwhatif/Program.vb
dotnet run --project tools/proofwhatif/ProofWhatIf.vbproj -c Release
```

The proof projects must sit two levels below the repo root (their `..\..\` paths match the
shipped projects' depth). Both proof programs are deterministic (fixed seeds); two runs gave
identical output.

## Output: coverage (`coverage_proof_output.txt`, abridged to the lines that matter)

| Scenario | What happened in hour 10 (and after) | Classification | VERDICT line | `--strict` exit |
|---|---|---|---|---|
| S-A | WS outage 10:07–10:52, process alive | 10:00 **Defect** | 1 defect hour | **1** |
| S-B | crash 10:07, restart 10:52 (marker splits the hour) | 10:00 **TrailingEdge** (`[10:52] Captured`) | 0 defect + 1 trailing-edge; seq line says 546 trades missing | **0** |
| S-C | outage 09:56–10:50, straddles the hour | 09:00 Captured, 10:00 **Captured** | **clean**; seq line says 648 trades missing | **0** |
| S-D0 | S-B plus a WS outage 12:10–12:30 | 12:00 **Defect** | 1 defect + 1 trailing-edge | **1** |
| S-D1 | S-D0 plus one `VENUE_503` line at 10:06, no `VENUE_OK` | 11:00, 12:00 **OutOfScopeVenue**; 12:00 missing from the markdown table | 0 defect + 1 trailing-edge | **0** |
| S-E0 | S-A store, no `ws_health.log`, no `analysis_log.csv` | 10:00 **Defect** (S1 skipped) | 1 defect hour | **1** |
| S-E1 | S-E0 plus a header-only `analysis_log.csv` | 10:00 **ExpectedMissing** | **clean** | **0** |

## Output: CeilingAudit (`ceiling_proof_output.txt`, verbatim)

```
=== P-CA1  side-blind design matrix vs side-aligned encoding (same rows, same labels) ===
  rows=3120  success rate=0.738
  SHIPPED encoding (BULLISH/BEARISH) baseAUC=0.6806 chalAUC=0.4429 pointΔ=-0.2378 printedΔ(bootstrap mean)=-0.2380 CI=[-0.3458,-0.1312] λ=1
                                     **Decisive verdict (NY×1):** CEILING DECLARED
  CONTROL encoding (ALIGNED/COUNTER) baseAUC=0.6806 chalAUC=0.8128 pointΔ=+0.1321 printedΔ(bootstrap mean)=+0.1340 CI=[+0.0580,+0.2161] λ=10
                                     **Decisive verdict (NY×1):** B1 PRIZE MEASURED
  shipped coef MTF15mTrend=BULLISH = +0.0134
  shipped coef MTF15mTrend=OTHER = +0.0000

=== P-CA3  GD convergence at the shipped 500 epochs (lr 0.5, decay 0.01) ===
  control fit: loss[0]=0.693147 loss[499]=0.418070 non-monotone steps=0
  control fit @20000 epochs: final loss=0.415054  chalAUC=0.8360 (500 epochs: 0.8128)

=== P-CA2  a challenger that is WORSE than baseline declares the ceiling ===
  CI=[-0.20,-0.100]  -> **Decisive verdict (NY×1):** CEILING DECLARED
  CI=[-0.05,+0.020]  -> **Decisive verdict (NY×1):** CEILING DECLARED
  CI=[+0.01,+0.029]  -> **Decisive verdict (NY×1):** CEILING DECLARED

=== P-CA4  a short (truncated / older-schema) row is admitted as HasPlaced=True with zero placed levels ===
  header columns=116  short row fields=66
  eligible rows=2  NonV08Excluded=0  UnparsedExcluded=0
  row 0: ts=10:08 HasPlaced=True PlacedTargetShort=64700 PlacedStopShort=64880 MTF15mTrend='BEARISH' OFIRatio=NaN SpreadBps=NaN
  row 1: ts=10:09 HasPlaced=True PlacedTargetShort=0 PlacedStopShort=0 MTF15mTrend='BEARISH' OFIRatio=NaN SpreadBps=NaN
  row 0 label=1  (settings v68)
  row 1 label=1  (settings v68)
```

The P-CA1 data is synthetic. Ground truth: success logit = 1.5·s(MTF15mTrend) + 0.1·Σ s(11
other directional signals), s = ±1 for agrees/opposes the row's side, each signal agrees
with the side 75 % of the time, LONG/SHORT 50/50. Baseline = aligned votes / 19 (the
pipeline's equal-weight vote share). The two encodings carry identical information; only
the orientation differs.

## Output: What-If report (`whatif_proof_output.txt`, the table rows)

```
=== P-WI1  ranking table vs the program's winner   (WhatIfProgram winner index = 1)
  | 1 | stop_max_atr_mult=2.2 | 12 | -0.400 | 0.000 | — | -0.400 [-0.400,-0.400] n=12 | n<30 |
  | 2 | stop_max_atr_mult=1.8 | 108 | -0.166 | 0.125 | -0.050 [-0.050,-0.050] n=58 | -0.300 [-0.300,-0.300] n=50 | ◆ winner ⚠ DIVERGENT |
  | 3 | stop_max_atr_mult=2 | 109 | -0.065 | 0.005 | -0.070 [-0.070,-0.070] n=57 | -0.060 [-0.060,-0.060] n=52 |  |
  | 4 | stop_max_atr_mult=1.6 | 115 | -0.085 | 0.005 | -0.080 [-0.080,-0.080] n=60 | -0.090 [-0.090,-0.090] n=55 | ⚠ DIVERGENT |
=== P-WI2  single-cell overlay: which EV does the report print?   (WhatIfProgram winner index = 0)
  ## ⚠ Guard-rails (binding)
  ## Population shift (directional count: baseline → overlay)
  ## Baseline vs overlay — failure matrix (winner cell)
```

P-WI1 uses constant per-cell EV samples, so every CI is degenerate; the DIVERGENT flags in
that table are an artefact of the synthetic input and are not claimed as a finding.
