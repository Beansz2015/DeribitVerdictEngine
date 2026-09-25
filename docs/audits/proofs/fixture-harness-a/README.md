# Proof artifacts — fixture-harness-a audit (2026-09-25)

Companion to [`docs/audits/2026-09-25-fixture-harness-a.md`](../../2026-09-25-fixture-harness-a.md).

All commands below were run inside a git worktree at commit `6e74181`
(`git worktree add /tmp/audit-6e74181 6e74181` on a Linux/WSL-style shell;
the actual audit session ran on Windows and used
`/c/Dev/audit-6e74181` as the worktree path — substitute your own).

## Files

- `ProofCheck.vbproj.txt` — rename to `ProofCheck.vbproj` and drop into
  `verify/proofcheck/ProofCheck.vbproj` inside the worktree (or any sibling
  of `verify/ordercheck/`, two directories below repo root — the
  `Compile Include` paths are `..\..\<file>`, matching `OrderCheck.vbproj`'s
  own depth). It links the exact same real shipped sources
  `verify/ordercheck/OrderCheck.vbproj` links, so it compiles against
  production code, not copies.
- `ProofCheck.Program.vb.txt` — rename to `Program.vb` next to the vbproj
  above.
- `proof_run_output.txt` — the exact stdout from running the proofs (below).
- `harness_A_run_output_full.txt` — the full stdout of the existing
  `verify/ordercheck` harness run at 6e74181 (425 PASS, 0 FAIL, `ALL PASS`,
  exit code 0), kept as evidence the harness itself was green at the audited
  commit.

## Exact commands run

```bash
git worktree add /tmp/audit-6e74181 6e74181
cd /tmp/audit-6e74181

# existing harness, unmodified — establishes the baseline the audit report cites
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release

# this proof harness (after renaming the two .txt files back and placing them
# at verify/proofcheck/{ProofCheck.vbproj,Program.vb})
dotnet run --project verify/proofcheck/ProofCheck.vbproj -c Release
```

`dotnet --version` in the audit session reported `9.0.318`; the project
targets `net8.0`. No `apt-get` install was needed — the host already had the
SDK.

## What the two proofs show (see `proof_run_output.txt` for the literal output)

**Proof 1** — `SignalEmitter.ComputeSideLevels`'s DG1 stop floor
(`StructuralLevelsSettings.StopMinFloorTicks × SignalEmitter.TickSize` =
4 × $0.5 = $2.00) is a flat dollar distance, independent of ATR. A structural
stop pinned just above that floor ($2.50) is accepted as `SWING_STOP`
identically at ATR=30 and ATR=140 (the audit brief's own flush scenario).
Expressed in bps of price it is ~0.40 bps — about 1/7 of the cheapest
round-trip fee the engine's own `TradeCostSettings` prices (maker_maker,
3.0 bps) and about 1/20 of the TARGET side's own fee-aware floor (8.0 bps).

**Proof 2** — `SettingsDiffApplier.Validate` accepts a diff moving
`scoring.structural_levels.stop_max_atr_mult` from 1.6 to **-50.0**, and a
diff moving `scoring.structural_levels.target_max_atr_mult` from 3.5 to
**999999.0**. Both return `IsValid = True`. Validate checks path
resolvability and the HARD CONSTRAINT prefix fences only — never the
proposed value's sanity.

Both proofs are read-only against in-memory objects: no settings.json file,
CSV, or log on disk is touched.
