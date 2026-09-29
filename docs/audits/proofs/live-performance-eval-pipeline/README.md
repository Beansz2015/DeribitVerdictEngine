# Proofs — live performance / eval pipeline audit (2026-09-24)

Isolated proof harnesses for [`../../2026-09-24-live-performance-eval-pipeline.md`](../../2026-09-24-live-performance-eval-pipeline.md).
They **link the shipped `.vb` sources** (no copies) and write only to the OS temp dir and their own
`bin/` folder (`AnalysisLogger` hardcodes `AppDomain.BaseDirectory`). They are not part of any build.

Files are stored with a `.txt` suffix so no project glob or solution picks them up:

| File | Restore as | What it is |
|---|---|---|
| `AuditProof.vb.txt` | `AuditProof.vb` | Tests T1–T11 (T1 runs twice: the bar that touches both levels, then a bar that only wicks the target) |
| `AuditProof.vbproj.txt` | `AuditProof.vbproj` | The `verify/ordercheck/OrderCheck.vbproj` compile list with the paths made absolute, plus `AnalysisOutputDump.vb` |
| `Bench.vb.txt` | `Bench.vb` | Timing for repeated `WriteEvalCache` of 90,000 rows |
| `Bench.vbproj.txt` | `Bench.vbproj` | Same compile list as `AuditProof.vbproj` |
| `proof-output.txt` | — | Full stdout of the `AuditProof` run below |
| `bench-output.txt` | — | Full stdout of the `Bench` run below |

## Run

Environment used: Ubuntu 24.04 container, .NET SDK 8.0.131 (`apt-get install dotnet-sdk-8.0`).
The `.vbproj` files contain **absolute paths to `/home/user/DeribitVerdictEngine/`**. To run
elsewhere, replace that prefix with your repo root (on Windows,
`C:/Dev/DeribitVerdictEngine/`).

```bash
mkdir -p /tmp/proof && cd /tmp/proof
cp <repo>/docs/audits/proofs/live-performance-eval-pipeline/AuditProof.vb.txt     AuditProof.vb
cp <repo>/docs/audits/proofs/live-performance-eval-pipeline/AuditProof.vbproj.txt AuditProof.vbproj
dotnet build -nologo -v q
dotnet run --no-build

mkdir -p /tmp/bench && cd /tmp/bench
cp <repo>/docs/audits/proofs/live-performance-eval-pipeline/Bench.vb.txt     Bench.vb
cp <repo>/docs/audits/proofs/live-performance-eval-pipeline/Bench.vbproj.txt Bench.vbproj
dotnet build -nologo -v q
dotnet run --no-build
```

Exact commands as run for the output below (the scratchpad folders held the files under their
`.vb` / `.vbproj` names):

```bash
cd <scratchpad>/proof && dotnet run --no-build > ../proof-output.txt 2>&1   # exit 0
cd <scratchpad>/bench && dotnet run --no-build > ../bench-output.txt 2>&1   # exit 0
```

## Caveats

- **T3 and T4 use the real clock** (`DateTime.UtcNow`) for the backfill rows. Their timestamps
  and the gap-fill console lines change from run to run, but the outcomes don't.
- **Timings vary between runs.** The first run reported in the audit gave T5 = 588 ms and
  T6 = 307 ms; the run captured below gave 643 ms and 348 ms. Both runs used Linux with no
  antivirus. The Windows collector with Defender real-time scanning is not measured here.
- T3/T4 rewrite the **temp copy** of `settings.json` (`min_net_move_pct` 0.0005 → 0.0001). The
  tracked file is only read (`File.Copy`).
- T8 uses `FileShare.None` from the same process to stand in for another process's lock. It
  does not test Excel.

## Output — `AuditProof` (`proof-output.txt`)

```
settings v68  EffectiveMinMovePct=0.0007999999999999999  perf.enabled=True  tmp=/tmp/auditproof_ef29eee0

==== T1 forming-bar stub is frozen into the live OHLC cache (both-touch bar) ====
init: ok — ohlcBars=0 evalRows=0 backfilled=0
signal @ 14:00:01.200Z entry=59003.50 placed target=59079.15 (SWING_HIGH_5M) stop=58811.13 (FALLBACK_ATR)
bar CloseTime 14:05  cached O/H/L/C = 59020/59020/59020/59020   exchange O/H/L/C = 59020/59085/58790/58950
cached bars after first run with High=Low (stubs): 37 of 37
eval cache outcome (live path): WINDOW_EXPIRED, TargetEverHit=False
same row against the exchange's complete bars: AMBIGUOUS, TargetEverHit=True
PROVEN: live strip outcome differs from the tape
NY cell: success=0 failure=1 targetHits=0

==== T1 forming-bar stub is frozen into the live OHLC cache (target-wick bar) ====
init: ok — ohlcBars=0 evalRows=0 backfilled=0
signal @ 14:00:01.200Z entry=59003.50 placed target=59079.15 (SWING_HIGH_5M) stop=58811.13 (FALLBACK_ATR)
bar CloseTime 14:05  cached O/H/L/C = 59020/59020/59020/59020   exchange O/H/L/C = 59020/59085/59000/59060
cached bars after first run with High=Low (stubs): 37 of 37
eval cache outcome (live path): WINDOW_EXPIRED, TargetEverHit=False
same row against the exchange's complete bars: SUCCESS, TargetEverHit=True
PROVEN: live strip outcome differs from the tape
NY cell: success=0 failure=1 targetHits=0

==== T2 perf-strip session blocks drop the last (inclusive) hour of every session ====
  session ASIA: 0-7 UTC, res 3; engine bucket for hour 7 = 3
  session LONDON: 8-12 UTC, res 3; engine bucket for hour 12 = 3
  session NY: 13-23 UTC, res 1; engine bucket for hour 23 = 1
  Asia block (06:30 + 07:30 rows)        now=Thu 08:30Z  n=1 (rows really in session: 2)  IsActive=False  range(UTC+8) 09-24 08:00->09-24 15:00
  London block (11:30 + 12:30 rows)      now=Thu 13:30Z  n=1 (rows really in session: 2)  IsActive=False  range(UTC+8) 09-24 16:00->09-24 20:00
  NY block (22:30 + 23:30 rows)          now=Fri 00:30Z  n=1 (rows really in session: 2)  IsActive=False  range(UTC+8) 09-24 21:00->09-25 07:00
  London while LIVE at 12:45 UTC         now=Thu 12:45Z  n=1 (rows really in session: 2)  IsActive=False  range(UTC+8) 09-24 16:00->09-24 20:00
  NY while LIVE at 23:45 UTC             now=Thu 23:45Z  n=1 (rows really in session: 2)  IsActive=False  range(UTC+8) 09-24 21:00->09-25 07:00

==== T10 partial OHLC coverage is judged as full coverage (1 of 13 bars -> WINDOW_EXPIRED, not NO_DATA) ====
  bars available: 1 of 13 -> outcome WINDOW_EXPIRED (counted as FAILURE in AggregateRange)

==== T11 T+3 floor: a stop-out inside the first 2 minutes is invisible; later target touch scores SUCCESS ====
  stop pierced at bar CloseTime 14:02 (58,780 < 58,811.13); eval outcome = SUCCESS

==== T5 WriteEvalCache truncates first: any exception mid-write destroys the cache; cost at production size ====
  full rewrite of 75,000 rows: 9.1 MB in 643 ms (runs on the UI thread on every PENDING resolution)
  ComputeWindows over 75,000 rows: 8 ms
[LivePerformanceTracker] WriteEvalCache failed: Object reference not set to an instance of an object.
  after a mid-write fault: file holds 40,000 of 75,000 rows; the previous good file is gone

==== T6 AnalysisOutputDump at cap: every run re-reads and rewrites the whole dump on the UI thread ====
  dump at cap: 3,000 blocks, 16.9 MB (production archive was 16.8 MB)
  mean Append() at cap: 348 ms per run (ReadAllLines + WriteAllLines of the whole file); runs still 3000

==== T7 FailureRateMatrix.IsMostProfitable is structurally pinned to the longest window ====
  200 random-walk books x 4 tiers: IsMostProfitable flagged 800 cells, 0 of them NOT the 15-min window

==== T8 AnalysisLogger.LogRun drops the row silently when another process holds the CSV (e.g. Excel) ====
  rows before=1 after locked LogRun=1; console output during the drop: ''

==== T9 CSV Timestamp is culture-formatted (the ':' and the calendar follow CurrentCulture) ====
  ICU cultures: 22 specific cultures produce a different CSV timestamp. Examples:
    ar-SA -> 1448-04-13 14:00:01
    as-IN -> 2026-09-24 14.00.01
    ckb-IR -> 1405-07-02 14:00:01
    da-DK -> 2026-09-24 14.00.01
    da-GL -> 2026-09-24 14.00.01
    en-DK -> 2026-09-24 14.00.01
  th-TH LogRun -> backfill parser Timestamp=2569-09-24 13:21:28; offline loader Timestamp=2569-09-24

==== T3 a hot-reloaded floor change re-stamps floor_pct without re-evaluating -> the self-heal never fires ====
[LivePerformanceTracker] Gap-fill call 1 of 1: 2026-09-17T13:21Z → 2026-09-24T08:20Z (9780 bars expected)
[LivePerformanceTracker] Gap-fill call 1 received: 0 bar(s) (0 new, 0 duplicate/already-present)
[LivePerformanceTracker] Gap-fill complete: 1 gap(s) filled, 0 bar(s) added across 2 call(s)
  start, floor 8 bps:                          R1(target 6.0 bps)=EXCLUDED_BELOW_MIN_MOVE  file floor_pct=0.0007999999999999999
  floor now 0.00039999999999999996
  after one live run (R2 resolved, file rewrite): R1(target 6.0 bps)=EXCLUDED_BELOW_MIN_MOVE  file floor_pct=0.00039999999999999996
[LivePerformanceTracker] Gap-fill call 1 of 1: 2026-09-17T13:21Z → 2026-09-24T08:20Z (9780 bars expected)
[LivePerformanceTracker] Gap-fill call 1 received: 0 bar(s) (0 new, 0 duplicate/already-present)
[LivePerformanceTracker] Gap-fill complete: 1 gap(s) filled, 0 bar(s) added across 2 call(s)
  after restart at floor 4 bps:                R1(target 6.0 bps)=EXCLUDED_BELOW_MIN_MOVE  file floor_pct=0.00039999999999999996
[LivePerformanceTracker] Gap-fill call 1 of 1: 2026-09-17T13:21Z → 2026-09-24T08:20Z (9780 bars expected)
[LivePerformanceTracker] Gap-fill call 1 received: 0 bar(s) (0 new, 0 duplicate/already-present)
[LivePerformanceTracker] Gap-fill complete: 1 gap(s) filled, 0 bar(s) added across 2 call(s)
  control: fresh cache built at floor 4 bps -> R1 = SUCCESS

==== T4 a floor change at startup re-walks rows older than the 7-day OHLC cache -> resolved outcomes become NO_DATA ====
  before: STRONG LONG=SUCCESS | SHORT=ADVERSE_HIT
[LivePerformanceTracker] Gap-fill call 1 of 1: 2026-09-17T13:21Z → 2026-09-24T08:20Z (9780 bars expected)
[LivePerformanceTracker] Gap-fill call 1 received: 0 bar(s) (0 new, 0 duplicate/already-present)
[LivePerformanceTracker] Gap-fill complete: 1 gap(s) filled, 0 bar(s) added across 2 call(s)
[LivePerformanceTracker] min-tradeable-move floor re-eval (0.040 %): 0 excluded, 0 recovered.
  after restart with floor 4 bps: STRONG LONG=NO_DATA/TEH=null | SHORT=NO_DATA/TEH=null

done
```

## Output — `Bench` (`bench-output.txt`)

```
rewrite #1: 90,000 rows 10.9 MB 181 ms
rewrite #2: 90,000 rows 10.9 MB 338 ms
rewrite #3: 90,000 rows 10.9 MB 177 ms
rewrite #4: 90,000 rows 10.9 MB 110 ms
rewrite #5: 90,000 rows 10.9 MB 108 ms
rewrite #6: 90,000 rows 10.9 MB 160 ms
```

The audit quoted 0.1–0.6 s for this rewrite. That range spans this run, the earlier bench run
(105–473 ms) and T5's single 75,000-row rewrites (588 ms and 643 ms).
