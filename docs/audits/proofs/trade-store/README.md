# Proof probe for `docs/audits/2026-09-24-trade-store.md`

An isolated console project. It compiles three **production files unchanged**:
`Core/TradeStoreWriter.vb`, `Core/StoreFiles.vb` and `tools/BacktestRunner/HistoricalStore.vb`.
It links them against `Stubs.vb.txt`, which holds two things:

- `TradeRecord`, copied **verbatim** from `DeribitClient.vb` lines 395–493 at commit `6e74181`.
  This is a copy and will drift if `TradeRecord` changes. Re-copy it before trusting a later run.
- Minimal stand-ins for `Candle`, `BacktestFundingSample`, `TradeStoreSettings` and
  `DeribitClient.GetCandlesAsync`. None of the probed code paths read their behaviour.
  They exist only so the three production files compile outside the app.

The `.vb` files are stored as `.vb.txt` so the app build and `verify/ordercheck` can't pick
them up. `Probe.vbproj` is kept exactly as it was run, so it carries the absolute repo path
`/home/user/DeribitVerdictEngine`. The command below rewrites that path.

## What each case proves

| Case | Finding | What it drives |
|---|---|---|
| T1a–T1d | F1 | `AppendRows` → truncate the file mid-row (a simulated kill) → `AppendRows` again → `ReadTradeFile` + `ResolveRepairWindows` |
| T2 | F2 | `HistoricalStore.FetchRepairWindowAsync` with a fully served hole and a commit that returns 0 |
| T3 | F6 | `HistoricalStore.BackfillFundingMonthAsync` with 12 vs 11 of 24 expected samples stored. With 12 it returns immediately. With 11 it attempts a network fetch. The fetch failing here is expected and doesn't matter: the proof is *whether* a fetch is attempted |
| T4 | F3 | `AppendRows` with the month file symlinked to `/dev/full` (Linux only) |
| T5 | F5 | `ReadTradeFileTail` over a synthetic 1.8M-row month file |

⚠ **T2's "pass line" is NOT `RepairStatusLog`.** It comes from `RepairStatusLogShim` in
`Program.vb.txt`. The shim restates the state rule in `RepairStatusLog.ComposePassLines`:
failure → `PASS_FAILED`, not-served → `PASS_LOSS`, else `PASS_CLEAN`. The window state
(`HOLE_REPAIRED`, `committed=0`, `isFailure=False`) is production output. The pass-line
word is a restatement. `ComposePassLines` is `Friend` and could be called directly, but that
means linking `RepairStatusLog.vb` and `ProcessIdentity`, which this probe doesn't do.

⚠ **Not covered by this probe:** F4 (share-mode collisions). Linux doesn't enforce
`FileShare` between a reader and a writer the way Windows does, so it can't be reproduced
here. F7–F12 are argued from the code only.

## Run command

Needs the .NET 8 SDK. Run it from the repo root:

```bash
P=$(mktemp -d); D=docs/audits/proofs/trade-store
cp $D/Probe.vbproj $P/
for f in Program Stubs; do cp $D/$f.vb.txt $P/$f.vb; done
sed -i "s#/home/user/DeribitVerdictEngine#$(pwd)#g" $P/Probe.vbproj
(cd $P && dotnet build -v q && dotnet bin/Debug/net8.0/ProbeHost.dll)
```

T3 writes `backtest_data/` under the probe's working directory. T1/T4/T5 write under
`$TMPDIR` (`/tmp/torn_*`, `/tmp/full4`, `/tmp/month5`, about 103 MB for T5).

## Output: full run from the committed copy (2026-09-24, Linux container, .NET SDK 8.0.131)

This run used the command above against commit `6e74181`'s production files.

```
Build succeeded.
T1a torn inside trade_seq (3 digits survive):
  torn tail on disk : '1790816400005,63005.00,100.00,buy,none,500000005,300'
  parsed rows       : 9 (10 trades were sent)
  merged row        : ts=1790816400005 amt=100 dir=buy liq=none id=500000005 seq=3001790816400006
  seq 300000006 present? False
  repair window     : Hole first=300000005 last=3001790816400005 missing=3001790516400001
  repair window     : Tail first=300000011 last=-1 missing=0
T1b torn inside trade_id:
  torn tail on disk : '1790816400005,63005.00,100.00,buy,none,5000'
  parsed rows       : 9 (10 trades were sent)
  merged row        : ts=1790816400005 amt=100 dir=buy liq=none id=50001790816400006 seq=-1
  seq 300000006 present? False
  repair window     : Tail first=300000011 last=-1 missing=0
T1c torn exactly at end of row (only newline missing):
  torn tail on disk : '1790816400005,63005.00,100.00,buy,none,500000005,300000005'
  parsed rows       : 9 (10 trades were sent)
  merged row        : ts=1790816400005 amt=100 dir=buy liq=none id=500000005 seq=-1
  seq 300000006 present? False
  repair window     : Tail first=300000011 last=-1 missing=0
T1d torn inside the timestamp (5 chars survive):
  torn tail on disk : '17908'
  parsed rows       : 9 (10 trades were sent)
  merged row        : ts=179081790816400006 amt=100 dir=buy liq=none id=500000006 seq=300000006
  seq 300000006 present? True
  repair window     : Hole first=300000005 last=300000006 missing=2
T2 hole fetch where the append commits nothing:
  state=HOLE_REPAIRED committed=0 notServed=0 isFailure=False
  pass line: PASS_CLEAN
T3 funding coverage check (24 hourly samples expected):
  stored=12 -> (calling BackfillFundingMonthAsync; a fetch attempt logs to stderr)
  stored=12 returned=12 elapsedMs=2
  stored=11 -> (calling BackfillFundingMonthAsync; a fetch attempt logs to stderr)
[HistoricalStore] Funding HTTP failure: Response status code does not indicate success: 403 (Forbidden).
[HistoricalStore] Funding fetch failed for 2026-08 — keeping 11 stored sample(s)
  stored=11 returned=11 elapsedMs=371
T4 AppendRows against a failing device:
[TradeStoreWriter] append to '/tmp/full4/trades_2026-10.csv' failed: No space left on device : '/tmp/full4/trades_2026-10.csv'
  written reported = 86 (disk accepted 0)
T5 ReadTradeFileTail cost on a full month (1.8M rows):
  file MB = 103
  run 1: 20000 rows in 1635 ms
  run 2: 20000 rows in 1620 ms
```

## Output: the runs the audit quotes (scratchpad, same session, before the copy)

The audit was written from two earlier runs of the same probe. The first run had no T1d.
T1d was added and the probe re-run, and only its T1d section was read. The first run's
T1a–T4 lines match the full run above except for two timing lines. T3's 11-sample call took
`elapsedMs=404` instead of 371, and T5 differed:

```
T5 ReadTradeFileTail cost on a full month (1.8M rows):
  file MB = 103
  run 1: 20000 rows in 1517 ms
  run 2: 20000 rows in 1610 ms
```

Those are the 1,517 / 1,610 ms figures quoted in F5. The re-run above gave 1,635 / 1,620 ms.
Expect the numbers to vary with the machine. On a burstable EC2 box they will be higher.
