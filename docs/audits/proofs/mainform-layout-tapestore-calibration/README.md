# Proofs: MainForm_Layout / TapeStoreStatus / Calibration audit (2026-09-24)

Supports `docs/audits/2026-09-24-mainform-layout-tapestore-calibration.md`.

## Status: NOT RUN

None of these proofs has been executed. The audit container had no .NET SDK. An attempt to install one with `dotnet-install.sh` failed: the egress proxy refused `builds.dotnet.microsoft.com` (`CONNECT tunnel failed, response 403`). **There is no output to report.** The expected results in the code comments come from reading the code, not from running it.

## Files

- `AuditProofs.vb.txt`: one VB module with four subs.
  - `P1_UpdateAsyncHangsWithoutInit`: finding 1. Expect `HANGS`.
  - `P2_NaNPassesValidationAndDisablesGate`: finding 5. Expect `parsed=True rejected=False`, `gateFires=False`, then the Save-throws line.
  - `P3_WsSegmentOverflow`: finding 4. Expect `OverflowException`.
  - `P4_MtfGateFailsOpen`: finding 3. Expect `passLong=True passShort=True`.

## How to run (Windows, .NET 8 SDK)

The module depends on `LivePerformanceTracker`, `VerdictResult`, `IndicatorResults` and `IndicatorEngine`. `verify/ordercheck/OrderCheck.vbproj` already links `Core/`, `analysis/` and the root `.vb` files, so it builds there without new project wiring.

1. Copy `AuditProofs.vb.txt` to `verify/ordercheck/AuditProofs.vb`. Keep it as a scratch file and don't commit it.
2. From `Main` in `verify/ordercheck/Program.vb`, call `AuditProofs.P2_...()`, `P3_...()`, `P4_...()`, then `P1_...()` **last**. P1 leaves a task that never completes, so run it after the others.
3. Run:

   ```
   dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
   ```

4. Delete `verify/ordercheck/AuditProofs.vb` and revert `Program.vb`.

Numeric literals in P2 and P4 are MECHANISM values, per the fixture-literal provenance rule: any value reaches the path under test.
