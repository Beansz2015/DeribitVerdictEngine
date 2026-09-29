# Proof set — ordercheck payload/fee/MTF audit (2026-09-24)

These files back the findings in `docs/audits/2026-09-24-ordercheck-payload-fee-mtf-audit.md`.

## What this is, and what it is not

No standalone repro project was built and no `dotnet run`/`dotnet build` was executed
during this audit — every claim below rests on static reading of the tracked source
(via the `Read` tool) and text search (via `Grep`/`grep`), not on executing the
harness. The `.vb.txt` files in this directory are **verbatim excerpts copied from the
tracked repository**, not code written from scratch in a scratchpad — this session's
scratchpad was empty at audit time (confirmed with `ls -la`/`find` before writing
anything here). Each excerpt carries a header naming its exact source file and line
range and what it is proof of. `.vb` was renamed to `.vb.txt` per instruction; the
`.vbproj` file is renamed to `.vbproj.txt` for the same reason (it is not meant to be
built standalone from this directory — it has no companion sources here).

## Files

| File | Source | Lines |
|---|---|---|
| `CalcMTFGate.vb.txt` | `Core/Indicators_Structure.vb` | 441-521 |
| `TradeCostSettings.vb.txt` | `Core/Settings/EngineSettings.vb` | 917-983 |
| `Program_A9_MtfPerSideFlags.vb.txt` | `verify/ordercheck/Program.vb` | 1210-1293 |
| `Program_A22a_PayloadFieldByField.vb.txt` | `verify/ordercheck/Program.vb` | 2756-2871 |
| `Program_A24a_PlacedLevelsEqualPayloadLevels.vb.txt` | `verify/ordercheck/Program.vb` | 3182-3237 |
| `Program_A40_A41_FeeModel.vb.txt` | `verify/ordercheck/Program.vb` | 6853-6992 |
| `OrderCheck.vbproj.txt` | `verify/ordercheck/OrderCheck.vbproj` | 1-177 (full file) |

## Exact commands run and their actual output

All commands were run from the repo root, `/home/user/DeribitVerdictEngine`, via the
`Bash` tool during this session.

### 1. Line counts (scope sizing)

```
$ wc -l verify/ordercheck/Program.vb verify/ordercheck/OrderCheck.vbproj
  16764 verify/ordercheck/Program.vb
    177 verify/ordercheck/OrderCheck.vbproj
  16941 total
```

### 2. SettingsDiffApplier call-site tally (Finding 2)

```
$ grep -n "SettingsDiffApplier\." verify/ordercheck/Program.vb \
  | sed -E 's/^([0-9]+):.*(SettingsDiffApplier\.[A-Za-z]+).*/\1: \2/' \
  | sort -t: -k2 | uniq -c -f1 | sort -rn | head -20
     67 11078: SettingsDiffApplier.Validate
      1 5939:    ' SettingsDiffApplier. The strip render + audible cue stay OUT — same
      1 3016:    ' via ExecutionResolution; tweaker surface via SettingsDiffApplier.
      1 1991: SettingsDiffApplier.ParseDiff
      1 12564: SettingsDiffApplier.vb
```

Reading: `Validate` is called 67 times across the file (the printed line number
`11078` is an artifact of `uniq`'s grouping, not a claim that all 67 calls sit on that
line). `ParseDiff` is called once. `Apply` and `ApplyRevert` do not appear as call
sites at all — only in a comment and the `.vb` filename. This is the entire evidence
base for "Apply/ApplyRevert are never called by this harness."

### 3. MTF fail-open branch — searched for any fixture driving it (Finding 1)

```
$ grep -n -i "insufficient 15m\|CalcMTFGate(New List\|CalcMTFGate(Nothing" verify/ordercheck/Program.vb
(no matches)
```

Confirms no fixture in `Program.vb` constructs the `candles15m Is Nothing OrElse
candles15m.Count < adxPeriod + 2` branch of `CalcMTFGate`
(`Core/Indicators_Structure.vb:455-459`, reproduced in `CalcMTFGate.vb.txt`).

### 4. Geometry invariant search (Finding 4)

```
$ grep -n -i "geometry invariant\|StopPx < \|StopPx > \|Target > .*StopPx\|Target < .*StopPx\|Random()\|for each.*random\|Enumerable.Range.*ComputeSideLevels" verify/ordercheck/Program.vb
(no matches)
```

Confirms no property-style / randomised fixture asserts a general stop/entry/target
ordering invariant anywhere in the file — every structural-levels fixture (A22, A24,
A26, A36) pins point literals for hand-picked cases instead.

### 5. `CalcMTFGate` definition site

```
$ grep -n "Public Shared Sub CalcMTFGate\|Function CalcMTFGate" -r /home/user/DeribitVerdictEngine
Core/Indicators_Structure.vb:441:    Public Shared Sub CalcMTFGate(candles15m As List(Of Candle),
```

### 6. Scratchpad check (confirms nothing pre-existing to copy verbatim)

```
$ ls -la /tmp/claude-0/.../8785a997-728d-5641-9ee4-c1dc7390f824/scratchpad
total 8
drwx------ 2 root root 4096 Sep 24 12:21 .
drwx------ 4 root root 4096 Sep 24 12:21 ..
$ find /tmp/claude-0/.../8785a997-728d-5641-9ee4-c1dc7390f824/scratchpad -type f
(no output — empty)
```

## Not run

`dotnet build` / `dotnet run --project verify/ordercheck` against the harness was
**not executed** in this session. The findings are about coverage gaps (fixtures that
don't exist) and a design contradiction (documented in source comments), both provable
by reading and searching the tracked source; none of them required executing the
harness to establish.
