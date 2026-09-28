# RR-1 review items `D-2` + `D-2b` — build spec-back

> Reviewer working document, per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).
> Rulings built: [`gap-repair-rr1-spec-back.md`](gap-repair-rr1-spec-back.md) §2, "Rulings — 2026-09-26" (`D-2`, `D-2b`).
> Original spec: [`gap-repair-rr1-seq-order-spec.md`](gap-repair-rr1-seq-order-spec.md) §0, §4, §6.
> Code commit: `2dd65fb` (built from `d66ef1f`). Doc commit: the one that adds this file.
> Built 2026-09-28 (UTC). Not pushed. Not deployed (the deploy is reserved to the trader).

**Reviewer: Model: Sonnet 5 · Effort: HIGH.**

- **Why that tier.** Every handle below is runnable, and the code diff is small. The one piece of judgment is the narrowed
  total-order condition in the `TRAP 1` comment (`tsC < tsB ≤ tsA`, upper end inclusive). Check it by hand against
  `RepairSortCompare`; it takes about five minutes.
- **Where a reviewer can slip.** (1) Reading `A91c` part 1b's `cycleViolations > 0` as a failure. It is the check
  proving the pairwise instrument CAN fail. (2) Treating `local-fast`'s "no engine-path change" as a verdict on
  `2dd65fb`. It only diffs the working tree against `HEAD`; see `H-4`.
- **Escalation trigger.** Move to Opus if `H-2` does not reproduce E-1's shape, or if the hand check of the
  `tsC < tsB ≤ tsA` condition disagrees with the comment.

## 1. Ranked verification handles

All handles below are `H-n`: the reader can run them. There is no `E-n` in this build. Every mutation is a one-line
edit that is written out in full, so the reader can re-apply it.

**If you only run one, run `H-1`.**

**`H-1` — full harness, before and after.**
```bash
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```
Before (`d66ef1f`): `486 PASS`, `1 SKIP` (`A81b`, pre-existing), `0 FAIL`, `ALL PASS`.
After (`2dd65fb`), pasted:
```
ALL PASS
PASS=489 SKIP=1 FAIL=0
```
Identity: `486 + 3 = 489`. The three are `A92a`–`A92c`. `A91c` was reworked in place, so its ID count does not change.
The before/after ID diff (`grep -oE '^(PASS|SKIP|FAIL)  [A-Z][0-9]+[a-z]*'`, sorted) printed exactly:
```
480a481,483
> PASS  A92a
> PASS  A92b
> PASS  A92c
```
No existing fixture changed its expected value (escalation trigger 1: not hit).

**`H-2` — the tie-break mutation fails `A91c` part 2.** In `Core/TradeStoreWriter.vb` `RepairSortCompare`, replace the
last line `Return a.Seq.CompareTo(b.Seq)` with `Return 0`, then run `H-1`. Pasted:
```
FAIL  A91c ST-1 — ... — part1: n=2 [0]Hole 9002..9002 [1]Tail 9004..-1 violations=0(want 0) · part1b: cycleViolations=1(want >0) · part2: order1=n=2 [0]Hole 5001..5004 [1]Tail 5006..-1 order2=n=1 [0]Tail 5006..-1 (want Hole[5001,5004] Tail 5006 each) violations=0(want 0)
1 FAILURE(S)
PASS=488 FAIL=1
```
Order 2 reproduces `gap-repair-rr1-spec-back.md` §1 `E-1` exactly (`order2= [0]kind=Tail first=5006`). `A91c` is the
only failure, so it is the only guard on that line. Restored with the inverse edit; the re-run gave `ALL PASS`, 489.
`Return 0` is used instead of a bare delete: a VB function that falls off its end also returns 0, so the semantics
match, and no compiler warning appears.

**`H-3` — the three `D-2b` mutations.** Each is in `Core/TradeStoreWriter.vb`. Apply one, run `H-1`, restore with the
inverse edit. All three were restored and the final run gave `ALL PASS`, 489.

| ID | Edit | Result (pasted tail) | Fixture that fails |
|---|---|---|---|
| `M-a` | `CountSeqlessAfterCutover`: `p.TsMs < TradeIdentityCutoverMs` → `p.TsMs <= TradeIdentityCutoverMs` (cutover exclusive) | `1 FAILURE(S)` · `PASS=488 FAIL=1` · `A92a … findings=0 … chain=0(want 1, count 1)` | `A92a` |
| `M-b` | same line → `If p.Seq >= 0 Then Continue For` (count every seq-less row) | `2 FAILURE(S)` · `PASS=487 FAIL=2` · `A92a … count=2 first_ts=2026-08-10T23:59:30.000Z` · `A92c … count=2` | `A92c` (and `A92a`) |
| `M-c` | `ResolveRepairWindowsCore`: `If seqlessCount > 0 AndAlso` → `If seqlessCount >= 0 AndAlso` (finding when there is none) | `2 FAILURE(S)` · `PASS=487 FAIL=2` · `A92b … count=0 first_ts=1970-01-01T00:00:00.000Z` · `A92c` likewise | `A92b` (and `A92c`) |

**`H-4` — build and gate.**
```bash
dotnet build DeribitVerdictEngine.sln -c Release -t:Rebuild
powershell -NoProfile -File tools/checks/verify-gate.ps1
powershell -NoProfile -File tools/checks/verify-gate.ps1 -Mode prepush
```
Pasted: `DeribitVerdictEngine -> …\bin\Release\net8.0-windows\DeribitVerdictEngine.dll` · `0 Warning(s)` · `0 Error(s)`.
`local-fast` gate: `GATE PASSED`. ⚠ Its `version-bump` line read `OK no engine-path change`, because `local-fast`
diffs only the working tree against `HEAD` (`tools/checks/verify-gate.ps1` line 98), and `2dd65fb` was already
committed. The `prepush` run covers the range against `origin/master`: see §1a below.

**`H-5` — the comparator extraction is byte-identical.**
```bash
git show 2dd65fb -- Core/TradeStoreWriter.vb | grep -nE '^[-+].*(a\.Seq >= 0 AndAlso b\.Seq >= 0|Dim c As Integer = a\.TsMs|If c <> 0 Then Return c|Return a\.Seq\.CompareTo|rows\.Sort)'
```
Pasted:
```
57:+        If a.Seq >= 0 AndAlso b.Seq >= 0 Then Return a.Seq.CompareTo(b.Seq)
58:+        Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
59:+        If c <> 0 Then Return c
60:+        Return a.Seq.CompareTo(b.Seq)
155:-        rows.Sort(Function(a, b)
156:-                      If a.Seq >= 0 AndAlso b.Seq >= 0 Then Return a.Seq.CompareTo(b.Seq)
157:-                      Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
158:-                      If c <> 0 Then Return c
159:-                      Return a.Seq.CompareTo(b.Seq)
183:+        rows.Sort(AddressOf RepairSortCompare)
```
Four lines out, the same four lines in, differing only in indentation (escalation trigger 2: not hit).

**`H-6` — the cutover holds in the store.** Needs the 2026-09-28 copy-back at `aws_fetch/20260928-121255/backtest_data/`
(untracked). A row is seq-less when its 7th field is absent or not a non-negative integer, which is `TryParseRow`'s rule.
```bash
cd aws_fetch/20260928-121255/backtest_data && CUT=1786406400000 && for f in trades_2026-07.csv trades_2026-08.csv trades_2026-09.csv; do awk -F, -v cut=$CUT -v f=$f 'NR==1{next} NF<5{bad++;next} $1!~/^[0-9]+$/{bad++;next} {seqok=(NF>=7 && $7 ~ /^[ \t]*[0-9]+[ \t]*$/); if(!seqok){n++; if($1>=cut){after++} if($1>lastless)lastless=$1} else {s++; if(fs==""||$1<fs)fs=$1}} END{printf "%s rows_seqless=%d seqless_at_or_after_cut=%d last_seqless_ts=%s rows_seq=%d first_seq_ts=%s unparsable=%d\n",f,n,after,lastless,s,fs,bad}' $f; done
```
Pasted (this exact command, re-run 2026-09-28):
```
trades_2026-07.csv rows_seqless=3243 seqless_at_or_after_cut=0 last_seqless_ts=1785542387381 rows_seq=0 first_seq_ts= unparsable=0
trades_2026-08.csv rows_seqless=278330 seqless_at_or_after_cut=0 last_seqless_ts=1786370857001 rows_seq=1894962 first_seq_ts=1786370857120 unparsable=0
trades_2026-09.csv rows_seqless=0 seqless_at_or_after_cut=0 last_seqless_ts= rows_seq=4180693 first_seq_ts=1788220801070 unparsable=0
```
`1786406400000` = 2026-08-11 00:00:00 UTC. The last seq-less row is `1786370857001` = 2026-08-10 14:07:37.001 UTC. The
first seq row is 14:07:37.120 UTC. Margin to the cutover: 9 h 52 min. **Zero seq-less rows at or after the cutover
(escalation trigger 3: not hit).**

**`H-7` — the probe still shows the silent failure.**
```bash
dotnet run -c Release --project tools/checks/sort-consistency-probe/SortConsistencyProbe.vbproj
```
Pasted (the output matches the 2026-09-26 run line for line):
```
.NET 8.0.31
random comparator, 2000 ints: trials=300 threw=40 lastType=ArgumentException
A91c shipped rows (3): trials=2000 outcomes=[none=2000] returnedOrderWithPairViolation=0
ST-1 cycle, 3 rows: trials=2000 outcomes=[none=2000] returnedOrderWithPairViolation=2000
ST-1 cycles repeated, 17 rows: trials=500 outcomes=[none=500] returnedOrderWithPairViolation=500
ST-1 cycles repeated, 40 rows: trials=500 outcomes=[none=500] returnedOrderWithPairViolation=500
ST-1 cycles repeated, 200 rows: trials=500 outcomes=[none=500] returnedOrderWithPairViolation=500
ST-1 cycles repeated, 2000 rows: trials=500 outcomes=[none=500] returnedOrderWithPairViolation=500
```

**`H-8` — scope of the diff.**
```bash
git diff --stat d66ef1f
```
See §1a for the pasted output (it includes the doc commit).

### 1a. Results pasted after the doc commit

**`H-4`, `prepush` mode** (run at `2dd65fb`, before the doc commit), pasted tail:
```
ALL PASS
OK    harness ALL PASS

=== display-parity ===
OK    no snapshot/card drift detected

=== version-bump ===
OK    engine path changed but [no-engine-change] token present

=== rotation-riders ===
OK    AnalysisLogger.vb not in the changed set - no header rotation possible

=== result ===
GATE PASSED
```
⚠ **The `version-bump` line passed on a token that is NOT on `2dd65fb`.** The range `origin/master..HEAD` held 9
commits and 8 carried `[no-engine-change]`; `2dd65fb` deliberately does not. The check is range-wide, so any token in
the range covers an untagged engine commit. It is WARN-only and no settings key changed here, so the outcome is
right. The check would not have caught a missing bump, though. See §3.

**`H-8`, `git diff --stat d66ef1f`** (working tree with the doc commit's files staged), pasted:
```
 Core/RepairStatusLog.vb                        |  33 ++-
 Core/TradeStoreWriter.vb                       | 105 ++++++--
 TradeStoreGapRepair.vb                         |   9 +-
 docs/DeribitIndicatorProject.md                |   1 +
 docs/gap-repair-rr1-d2-d2b-spec-back.md        | 225 +++++++++++++++++
 docs/gap-repair-rr1-seq-order-spec.md          |  18 ++
 docs/gap-repair-rr1-spec-back.md               |   4 +-
 tools/BacktestRunner/HistoricalStore.vb        |  13 +-
 tools/checks/sort-consistency-probe/Program.vb |   3 +-
 verify/ordercheck/Program.vb                   | 319 +++++++++++++++++++++----
 10 files changed, 657 insertions(+), 73 deletions(-)
```

## 2. Decisions taken — one line each (auto-proceeded per `CLAUDE.md`, all revertible by one revert)

| # | Decision | Options | Pick | Why |
|---|---|---|---|---|
| 1 | How the count reaches `TradeStoreGapRepair` | (a) an optional sink list threaded like `outcomes` · (b) change `ResolveRepairWindows`'s return type · (c) a second full-file scan in `RepairOnceAsync` | **(a)** | No existing call site changes. (c) records more rows, but rows outside the scan are never sorted, so they cannot enter `ST-1`. That is a mechanism argument (three-step test, step 3), not a cost one |
| 2 | ⚠ One line per pass or one per affected FILE | (a) exactly one line per pass · (b) one line per month file with count > 0 | **(b)** | The brief says "ONE extra line per pass" and gives `file=<name>`. A pass whose lookback crosses 00:00 UTC on the 1st scans two files. (a) would have to drop or merge the file name. (b) is the richer option, at most two lines per pass. **Deviates from the brief's literal wording — reviewer, confirm** |
| 3 | Where the line goes | before the PASS line · after it | **before** | The PASS line stays the pass's last line. `tools/ops/collector-readback.ps1` tails 3 lines |
| 4 | Which rows are counted | the file's scanned rows (in-window plus bracket), before the `F-1` seed | as stated | The seed belongs to the previous file, and inside the lookback that file's own resolve counts it. A truncated scan counts only retained rows: the dropped rows are never sorted |
| 5 | What `first_ts` means | earliest counted timestamp · first in file order | **earliest** | The file is not sorted, so file order carries no meaning here |
| 6 | Findings on the exception path | pass them to `WritePass` · drop them | **pass them** | A pass that threw after finding a corrupted row should still say so |
| 7 | Tie-break mutation form | delete the line · `Return 0` | **`Return 0`** | Same semantics as the delete (a VB function falling off its end returns 0), and no compiler warning |
| 8 | `A91c` part 1b (not in the brief) | add it · omit it | **add** | Without it, the new pairwise check is as unproven as the no-throw check it replaces. A 3-cycle has no consistent linear order, so the part is deterministic |
| 9 | `A92a` production-chain check (not in the brief) | add it · omit it | **add** | It drives `HistoricalStore.BackfillTradeMonthCoreAsync` with stub fetchers, which proves the sink reaches the caller. `RepairOnceAsync` itself is not reachable from a fixture (real network) |
| 10 | `ScanForRepair`'s doc comment (not in the brief) | fix · leave | **fix** | It still said the caller sorts by `(Timestamp, TradeSeq)`. Comment only |
| 11 | ⚠ `DeribitIndicatorProject.md` §15 | a new row (the brief) · extend `RR-1`'s row (§15's own "one item gets ONE row" rule) | **new row** | The brief orders it, and `D-2b` is a separate deploy with a new log line. **Tension with the §15 rule — reviewer, confirm.** Row measured at 1,003 B |
| 12 | Summary document | a separate `*-batch-summary.md` · the §15 row and the commit message as the record | **no summary** | The brief asks for the spec-back only. This is a single-lane build |
| 13 | Class name for the finding | `SeqlessAfterCutover` · `SeqlessRowFinding` | **`SeqlessRowFinding`** | Keeps it distinct from the state constant `RepairStatusLog.SeqlessAfterCutoverState` |

## 3. Feedback on the brief's assumptions

- **The cutover basis is off by about a minute.** The brief says instance `d8678d2b…`'s first row is 2026-08-10
  14:08:43 UTC. The store's first seq-carrying row is 14:07:37.120 UTC, 66 s earlier. The start-up repair backfill
  probably wrote it. The constant is unaffected: the margin is 9 h 52 min. I did not check the instance id.
- **`gap-repair-rr1-spec-back.md` `D-2b` says "the 2026-08-10 cutover"; the constant is 2026-08-11 00:00 UTC.** The
  brief ruled the later instant. The two readings differ only for rows on 2026-08-10 after 14:07, and there are none
  seq-less.
- ⚠ **The `A56`/`A91` fixture base (`A56Ms`) is 2026-08-11 08:00 UTC, after the cutover.** So every "legacy" row in
  `A56d`, `A91b` and `A91c` part 1 would count as a corrupted modern row under `D-2b`. This is harmless today, because
  those fixtures pass no sink. A future fixture that reuses `A56Ms` legacy rows with a sink will see findings. `A92`
  uses its own base (`A92Ms`, offsets from the constant) for this reason.
- **"ONE extra line per pass"** does not cover a two-file pass. See decision 2.
- **The brief's measured claims held on re-run.** The tie-break is guarded by nothing but `A91c` part 2 (488 PASS with
  it mutated). The probe output is unchanged.
- **Escalation trigger 2 ("the extraction changes any sort order")** is proved by textual identity (`H-5`), not by an
  order-level fixture. No fixture compares a sorted list to the old lambda's. I judged the textual proof enough.
- **The brief's `H-4` names the default gate (`local-fast`).** That mode cannot see a committed change: it diffs the
  working tree against `HEAD`. `prepush` sees the range, but its `version-bump` token test is range-wide (§1a).
  Neither mode checked `2dd65fb` on its own. Not a defect in this build; a gap in the gate worth one line in a
  future brief.

## 4. What I did not verify, and cannot

- **Live behaviour.** Not deployed. No `SEQLESS_AFTER_CUTOVER` line has been seen in a real `repair_status.log`.
- **`TradeStoreGapRepair.RepairOnceAsync`'s wiring.** Compile-checked only. No fixture reaches it, and the
  exception-path hand-off (decision 6) is untested.
- **Whether a corrupted modern row can occur in practice** (a complete line whose seq field fails `TryParseRow`). Not
  reproduced; carried from `gap-repair-rr1-spec-back.md` §4.
- **The live box's current files.** `H-6` ran on the 2026-09-28 copy-back only.
- **Other readers of `repair_status.log`.** In-repo, I found only `tools/ops/collector.ps1` (fetch) and
  `tools/ops/collector-readback.ps1` (tail 3). Nothing outside the repo was checked.
- **`ST-1` behaviour above 2,000 rows or on another .NET runtime.** Carried from the probe; not extended.
- **Performance.** The count is one O(n) pass over rows already in memory. Not measured.

---

## 5. Orchestrator review — 2026-09-28 (UTC)

**Verdict: ACCEPTED.** Re-run against `5071476`: `H-1` gives 489 `PASS`, 0 `FAIL`, 1 `SKIP` (`A81b`), `ALL PASS`; `A91a`–`A91c` and `A92a`–`A92c` pass. Diff scope matches `H-8`.

| Item | Ruling |
|---|---|
| The narrowed invariant `tsC < tsB ≤ tsA` in the `TRAP 1` comment | ✅ **Correct, hand-checked.** `tsB = tsC`: time ties, the tie-break puts `B` (seq −1) before `C`, and `B` before `A` by time, so the order `B, A, C` is consistent — no cycle. `tsB = tsA`: the tie-break puts `B` before `A`, `C` before `B` by time and `A` before `C` by seq, so the cycle `C < B < A < C` forms. Lower end strict, upper end inclusive, as the comment says |
| Decision 2 — one `SEQLESS_AFTER_CUTOVER` line per affected file, not per pass | ✅ **Accepted.** It is the richer option, and the brief's "one line per pass" did not foresee a pass that spans two month files. The brief was wrong, not the build |
| Decision 11 — a new §15 row rather than extending `RR-1`'s row | ✅ **Accepted.** `D-2b` is a separate deploy with its own new behaviour (a log line), and folding it into `RR-1`'s row would push that cell far past §15's ~1,000 B cap. The brief ordered the new row; the tension with "one item gets ONE row" is noted, not resolved by merging |
| §3 finding — `verify-gate.ps1` `prepush` `version-bump` test is range-wide | ⚠ **Real gap, queued, not fixed here.** Any `[no-engine-change]` commit in `origin/master..HEAD` masks an untagged engine commit in the same range. It is WARN-only today. Small tools fix; recorded in `trader-tick-queue.md` §2 |
| §3 finding — `A56Ms` sits after the cutover | Noted. Harmless while those fixtures pass no sink |

**Deploy:** `D-2b` changes collector code. The trader pushes, then a seat deploys and reads back. Per the pre-holiday plan (`trader-tick-queue.md` state banner), this goes out with a few days of read-backs **before 2026-10-14**. First live check: `repair_status.log` should show **no** `SEQLESS_AFTER_CUTOVER` line (the store has no such row, `H-6`), and every pass line unchanged in form.
