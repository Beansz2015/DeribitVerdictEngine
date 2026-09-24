# Proofs: 2026-09-24 SwingFallbackRead / WsTradeProbe audit

**There is no proof code here.** This directory is empty apart from this README.

The audit at `docs/audits/2026-09-24-swing-fallback-read-wstradeprobe.md` was produced by
static reading of the shipped source (`tools/ops/SwingFallbackRead/*.vb`,
`tools/WsTradeProbe/*.vb`), full files, in this session. No reproduction script,
harness, or fixture data was written to the scratchpad during the audit, and nothing
in the report was built or run — this environment has neither the `aws_fetch`/
`backtest_data` inputs the tools read nor network access to Deribit. Every finding is
substantiated by a direct file:line citation into the tracked source, not by a runnable
proof.

There is therefore no run command and no captured output to report here. Writing
one would misrepresent what was actually done.

## What a real reproduction would need, if this is wanted as a follow-up

- **Finding 1/2 (the `.bak` merge gap):** a minimal fixture — a `pooled.csv`, a
  `live.csv`, and an `analysis_log.csv.v0.7.bak` each with a distinct, non-overlapping
  timestamp — run through `SwingFallbackReadProgram.RunAsync`'s merge step (or a
  standalone harness calling `LoadWithInstance` on all three and reproducing the
  `pooled.Concat(live)` vs. `bak.Concat(live)` construction) to show the `.bak`-only
  row's timestamp present in `boxLog`/`collectorIds` but absent from `merged`/`sigs`.
  This needs `dotnet build tools/ops/SwingFallbackRead/SwingFallbackRead.vbproj` to
  work, which needs the project's full dependency graph (`Core/`, `analysis/`) to
  compile — not attempted here.
- **Finding 3 (missing-bar walk bias):** a synthetic `bars` dictionary with a
  deliberate gap spanning a bar that would have hit `target`, fed to `Walk()`
  directly, showing the outcome resolve to `0` (open) instead of `1`/`2`.
- **Finding 4 (`LfKey` collision):** two synthetic `LfTrade` records sharing
  `(Timestamp, Price, Amount, Direction)` but different `TradeId`s, fed through the
  `byKey` construction in `LiquidationFlagCheck.vb`, showing them collapse into one
  tape entry.
- **Finding 5 (window definition mismatch):** not independently reproducible from
  this repo alone — it requires comparing `LiquidationFlagCheck.vb`'s window logic
  against `Core/TradeStoreWriter.vb`'s actual buffer behavior, which was cited but
  not opened in this audit (out of the requested scope).

None of the above was built. If you want runnable proofs for any of these, say
which finding and I'll build the harness and paste the real output here.
