# Gap repair — `RR-1` phantom-hole sort order — SPEC

> **Spec only. No `.vb` file touched by this seat.** Written 2026-09-25 (UTC) against
> [`gap-repair-repeat-fills-read-2026-09-25.md`](gap-repair-repeat-fills-read-2026-09-25.md) (finding `RR-1`,
> commit `d6ad3a9`) and the tracked `Core/TradeStoreWriter.vb` at the commit named in §11. Answers the queue row
> in [`trader-tick-queue.md`](trader-tick-queue.md) §2, *"Gap repair re-repairs the same `trade_seq` holes on
> later passes."*

**IDs used in this document, defined once:**

| ID | Source and kind | Meaning |
|---|---|---|
| `RR-1` | [`gap-repair-repeat-fills-read-2026-09-25.md`](gap-repair-repeat-fills-read-2026-09-25.md) | The finding this spec fixes: sorting by `(Timestamp, TradeSeq)` splits a complete `TradeSeq` run into a phantom hole when a higher seq is stamped an earlier millisecond than a lower one |
| `SO-1` | This spec §3 | The top-level decision: which sort key `ResolveRepairWindowsCore` uses. Options (a)–(d) are inherited unchanged from the read's §5 sketch |
| `SO-2` | This spec §4 | A sub-decision under `SO-1` (a): how a seq-less (legacy) row sorts against an identified one. Not in the read — found while proving (a) against the existing fixtures |
| `ST-1` | This spec §4.2 | The implementer trap this spec exists to name: the "obvious" comparator for `SO-2` is not a valid total order, and a DIFFERENT "obvious" comparator silently breaks a shipped fixture. Both are worked in §4 |
| `TRAP 1` / `TRAP 2` | `Core/TradeStoreWriter.vb:1020`–`1034` / `:1042`–`1059` | The two comment blocks this spec must not contradict — the append-order defence, and the legacy-row break-not-invent rule |
| `GT-3` | [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) §0 | A tail must stop at its segment end. Unaffected by this spec — named so a reviewer can confirm that in one line |
| `A56c` / `A56d` | Harness fixtures, shipped | `A56c` pins the append-order defence TRAP 1 protects. `A56d` pins the legacy break-not-invent rule TRAP 2 protects, in two shapes: era-boundary and INTERLEAVED. Both are the load-bearing coverage this spec must not break |
| `A91a`–`A91c` | This spec §6, **planned** fixture IDs | Free at this session — verified by grep (§6.0) |

---

## 0. Implementer brief

**Model: Sonnet 5 · Effort: HIGH · One session, one commit (plus the doc/queue follow-up).**

**Why that tier, and why not lower.** The code diff is tiny — one `Function` literal inside `rows.Sort(...)`,
`Core/TradeStoreWriter.vb:1030`–`1034`. Line count is not the reason for HIGH. The reason is that **two
different "obvious" fixes for the mixed-population requirement are both wrong, and both look plausible enough
to ship on a first pass:**

1. *"Sort purely by `TradeSeq`, treating `AbsentSeq` (`-1`) as just another value."* This passes `A56c` and
   most of `A56d`, but **silently breaks the seq-less-newest-row tail case** (`Core/TradeStoreWriter.vb:924`'s
   own doc comment: *"a legacy (seq-less) newest row ⇒ an AnchoredTail at its timestamp + 1"*). Under a pure
   numeric sort, every `AbsentSeq = -1` row sorts to the FRONT of the list regardless of its real time, so
   `rows(rows.Count - 1)` (the tail's `newest`) stops being the true newest row whenever that row happens to be
   legacy. This is exactly the "affects a rendered-adjacent code path in a case the fixture set doesn't force"
   shape CLAUDE.md's fixture-literal-provenance rule warns about, except here it is a control-flow trap, not a
   literal one.
2. *"Fine — partition ALL legacy rows before ALL identified rows, by category, and sort each half on its own
   key."* This is transitively valid (see `ST-1`, §4.2) and does NOT have trap 1's problem. **It still breaks a
   shipped fixture** — `A56d` part 2 (`verify/ordercheck/Program.vb:12234`–`12241`), which INTERLEAVES legacy
   rows between two identified rows specifically so their true time position breaks the walk in the right place.
   A category partition moves those legacy rows out of that position and reunites the two identified rows as
   direct neighbours, reintroducing the exact phantom `A56d` exists to prevent. **This seat tried this design,
   ran it by hand against `A56d`'s actual row data, and it fails** — see §4.1.

The comparator this spec settles on (§4.3) survives both checks. **Build it as specified; do not re-derive
one from scratch.** Re-deriving invites trap 1 or trap 2 again, and neither trap is caught by casual reading —
each looks correct until walked against the specific fixture it breaks.

**Where a lesser tier will slip.**

| # | Trap | The input that exposes it |
|---|---|---|
| **ST-1a** | Pure numeric `TradeSeq` sort (`AbsentSeq` treated as a plain low value) | A store whose scanned rows are ALL legacy and whose newest is legacy (any `A56d`-style era-only case) still passes if no identified row is present — the trap needs a MIXED window, which is why `A91b` must combine a legacy block with an identified block, not test either alone |
| **ST-1b** | Category-partition comparator (legacy always before identified, regardless of measured time) | `A56d` part 2 specifically, unmodified — run it BEFORE writing anything new |
| **ST-1c** | Deleting or "simplifying" `A56d` part 2 because it "looks superseded" by the new fixtures | It is not superseded. It is the one fixture already in the tree that a wrong comparator fails. If the implementer's build needs to change what `A56d` asserts (not just re-run it), the comparator has diverged from this spec |
| **ST-1d** | Leaving the `TRAP 1` comment block (`:1020`–`1034`) as it is after changing the code under it | The comment currently says the sort key is `(Timestamp, TradeSeq)`. Left unedited, it will describe code that no longer exists — update it in the same commit, referencing `RR-1` |

**Escalation triggers — stop and come back.**

- Any EXISTING fixture (`A56*`, `A79*`, or any other) needs its **expected value** changed, not merely
  re-run, to pass. §4.3's comparator is proven in this spec to leave every existing fixture's assertions
  untouched (§4.1, §4.4) — if the implementer's build disagrees, the comparator has drifted from this design.
- `List.Sort` throws `InvalidOperationException` ("inconsistent results") on any input, fixture or otherwise.
  That is the direct symptom of a non-total-order comparator (`ST-1`). Do not wrap it in `Try/Catch` to make it
  go away — come back and re-derive.
- The fix needs a `settings.json` key, or touches `ScanForRepair`, `AppendRows`, `DedupTrades`, or the
  bracket-selection logic in `ScanForRepair` (the "maximum timestamp below `segStartMs`" rule). None of those
  should need to change for this fix; needing to touch one is a sign the design has grown past this spec's scope.

**Session plan.** One session is enough — this is a single comparator change plus three new fixtures, not a
new subsystem.

| Step | Work |
|---|---|
| 1 | Run the full harness once, clean, to get the current PASS count as a baseline |
| 2 | Write `A91a` (§6.1) against **today's** code. Confirm it FAILS, and paste the failure output — this is the fail-first proof the phantom is real |
| 3 | Change the comparator (§4.3) and update the `TRAP 1` comment block. Re-run `A91a` — it must now PASS |
| 4 | Run `A56c` and `A56d` alone. Both must PASS UNCHANGED (same assertions, same expected values) — this is the parity proof for TRAP 1 and TRAP 2 |
| 5 | Write `A91b` and `A91c` (§6.2, §6.3). Run the named mutation for each of `A91a`–`A91c` from a scratch copy; paste the failing output |
| 6 | Full harness, `dotnet build -c Release`, `tools/checks/verify-gate.ps1`. `DeribitIndicatorProject.md` §15 row (settings-untouched, engine-binary change on the repair path) |

Report back with a summary and a spec-back per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).
Do not push. **Do not deploy** — see §7.

---

## 1. The defect, recapped from the read

`ResolveRepairWindowsCore` (`Core/TradeStoreWriter.vb:950`–`1123`) scans a month's trade rows, sorts them, then
walks `TradeSeq` deltas to cut holes (`RepairWindow.ForHole`, `:800`–`809`). The sort, at `:1030`–`1034`, is:

```vb
rows.Sort(Function(a, b)
              Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
              If c <> 0 Then Return c
              Return a.Seq.CompareTo(b.Seq)
          End Function)
```

`RR-1`, measured against the fetched AWS store copy-back (`gap-repair-repeat-fills-read-2026-09-25.md` §4.2):
when the venue stamps a higher `TradeSeq` an earlier millisecond than a lower one — verified example, seq
`300377528` at `…804` ms against seqs `526`/`527` at `…805` ms — the `(Timestamp, TradeSeq)` sort places `528`
BEFORE `526` and `527`. The delta walk then reports two holes (`[526,527]` and `[528,528]`) inside a run that
is, in `TradeSeq` terms, completely contiguous. Every repair pass inside the 20 h lookback re-derives the
identical two phantom windows and re-fetches and re-appends rows that were never missing. Measured cost over
2.5 days: 5 phantom re-detections, 19 duplicate rows, 5 extra REST fetches. The tape itself is complete —
confirmed by the venue check reading `CLEAN`.

---

## 2. Why the `(Timestamp, TradeSeq)` sort exists — read from the code, not assumed

`Core/TradeStoreWriter.vb:1020`–`1029`, `TRAP 1`:

> *"The store is NOT sorted, and `LastTradeTimestamp`'s own summary records why: repair appends its pages AFTER
> whatever streaming has already written, and the store already holds one out-of-order block. Walking the file
> in append order reports a PHANTOM HOLE at every repair-block boundary, and each phantom costs a REST fetch."*
> *"Sorted by (Timestamp, TradeSeq), not by Timestamp alone. List.Sort is unstable, and same-millisecond
> siblings are the defining feature of this tape … Ordering those arbitrarily would manufacture negative deltas
> inside a millisecond."*

Fixture `A56c` (`verify/ordercheck/Program.vb:12168`) pins this exactly: two `AppendRows` calls, the
second carrying OLDER timestamps than the first (a genuinely out-of-order file, not notionally so), and asserts
zero phantom holes and a tail that starts past the MAXIMUM stored seq, not past the file's last physical line.

**This spec's fix does not remove sorting — it changes the sort KEY, and only for pairs of rows that both
carry a `TradeSeq`.** Sorting by `TradeSeq` for those rows is at least as strong a defence against append order
as sorting by time: `TradeSeq` is the venue's own assignment order and does not depend on which order the
*store's writer* physically appended rows, which is the entire problem TRAP 1 names. Hand-verified against
`A56c`'s own row data (§4.4): the new comparator produces the identical sorted order `A56c` already asserts,
because that fixture's rows have no timestamp/seq inversion — TRAP 1's protection is preserved, not weakened.

`GT-3` (a tail must stop at `segEndInclMs`) and the bracket-selection rule in `ScanForRepair` (the "maximum
timestamp below `segStartMs`" row, `:1188`–`1194`) are outside the code this spec touches — see §5's "unchanged"
list.

---

## 3. Fix options — `SO-1`

| Option | Mechanism | What it records / trade-off | My read |
|---|---|---|---|
| **(a) Fix the walk's sort key** | Sort primarily by `TradeSeq` for any pair of rows that both carry one; a seq-less row still sorts by `Timestamp` (§4 works out exactly how, `SO-2`) | Removes the false-positive class **at its source** — a fully-contiguous seq run can never be split, because `TradeSeq` is the same key the hole arithmetic already uses. `LeftTsMs`/`RightTsMs` (the log's `span=` field) are unaffected: they were always the raw timestamps of the two bracket rows, not a derived range, and they are simply not written when no hole exists | **Recommended.** More truthful (fixes the mechanism, not a symptom) and no cheaper alternative gives up nothing — see the auto-proceed test below |
| **(b) De-dup before diagnosing** | Collapse duplicate rows (by the identity `DedupTrades` already uses) before the walk, so a hole already patched by an earlier pass's duplicate append is not re-counted | Does not stop the FIRST phantom detection — for `300377526..528` the very first `HOLE_REPAIRED` line was already phantom, before any duplicate existed (read §4.2). Treats a symptom of the recurrence, not `RR-1` itself | Not recommended as a substitute for (a). Adds no correctness (a) doesn't already give |
| **(c) Verify-before-fetch** | Before emitting a window, check whether every seq in `[FirstSeq, LastSeq]` is already present among the scanned rows; skip the fetch if so | Correct in effect for the FETCH, but the phantom `RepairWindow` (and its wrong `span=` log fields) still gets constructed and logged — a second full pass over the scanned rows, per window, per pass, forever | Not recommended. Costs an extra scan to avoid fixing the one comparison that is actually wrong |
| **(d) Do nothing** | Current behaviour: self-terminating at the 20 h lookback edge, no data loss, small absolute cost | Matches "tape completeness is not in doubt today." Duplicate-row count still grows without bound as more inversions occur, and every recurrence is an operator-facing log line that looks like a real defect | Not recommended. The read's own cost table (§4.4 of the read) shows this scales with tape busyness, and the fix is cheap and precisely scoped |

**Auto-proceed test (CLAUDE.md).** Step 1: is there an option that records more, or is more self-describing,
than (a)? No — (a) removes the false-positive class outright; (b) and (c) both leave it partly or wholly
intact. Step 2/3 do not apply: there is no richer option being passed over for cost, and no forbidden-mechanism
reason to prefer a cheaper one. **(a) is both the cheaper (b)/(c) do more work for less coverage — and the
more truthful pick. Not reserved as a design choice.** Logged here per CLAUDE.md's auto-proceed obligation.

⛔ **What IS reserved: deploying it.** See §7.

---

## 4. `SO-2` — how a seq-less row sorts, and the trap in getting there (`ST-1`)

### 4.1 Why "sort by `TradeSeq`, category-partition the rest" fails `A56d`

`A56d` part 2 (`verify/ordercheck/Program.vb:12234`–`12241`) builds this store, in this order:

| Row | Kind | Timestamp | `TradeSeq` |
|---|---|---|---|
| R1 | identified | `t=0` | `3000` |
| R2 | legacy | `t=30000` | `AbsentSeq` |
| R3 | legacy | `t=60000` | `AbsentSeq` |
| R4 | legacy | `t=90000` | `AbsentSeq` |
| R5 | identified | `t=120000` | `3500` |

The comment above it is explicit about why: *"The legacy rows COVER the ground between seq 3000 and 3500, so
there is no hole. Skipping past them reports 499 missing sequences and a 90-second phantom window over ground
the store already holds."* The fixture asserts exactly one window, a `Tail` with `FirstSeq = 3501` — i.e. **no
hole is ever computed for `[3001, 3499]`.**

A category-partition comparator ("every legacy row sorts before every identified row, then each half sorts on
its own key") produces the order **R2, R3, R4, R1, R5** — moving R1 out of first position because its category
(identified) outranks its actual time (`t=0`, earlier than every legacy row here). The walk now sees R1 and R5
as direct neighbours: `delta = 3500 − 3000 = 500 > 1` → a phantom `Hole [3001, 3499]` is emitted. **Hand-walked,
not just argued** — this is the failure this seat found by tracing the exact fixture data, which is why this
spec names it rather than leaving it for the implementer to discover the same way.

### 4.2 Why the "obvious" fix for 4.1 is not automatically a valid comparator (`ST-1`)

The fix that keeps R1 in front of R2–R4 (matching its true `t=0`) is: **compare by `Timestamp` whenever either
row is seq-less; compare by `TradeSeq` only when both are identified.** This is NOT automatically a valid
total order — a `Comparison(Of T)` must be transitive, and mixing two different keys depending on which pair
is being compared can produce a cycle. Worked counter-example: legacy row `B` at `t=200`; identified row `A`
at `(seq=100, t=500)`; identified row `C` at `(seq=200, t=100)` — i.e. `C` is an `RR-1`-style inversion, higher
seq but earlier time than `A`. `Compare(A, C)`: both identified → seq → `A < C`. `Compare(B, C)`: `B` legacy →
time → `C < B` (100 < 200). `Compare(B, A)`: `B` legacy → time → `B < A` (200 < 500). Chain: `A < C`, `C < B`,
`B < A` — a cycle. `List.Sort` given a genuinely inconsistent comparer can throw
`InvalidOperationException`, or on a short list silently return SOME order without detecting the
inconsistency — neither is acceptable in code that decides what gets re-fetched from a live venue.

**The cycle needs `B`'s timestamp to fall strictly between two identified rows' timestamps that are themselves
seq-inverted.** That cannot happen if every legacy row's timestamp is earlier than every identified row's
timestamp in the set being sorted — which is a real, structural fact here, not a hopeful assumption: identified
`TradeSeq` values did not exist before the 2026-08-10 cutover (`Core/TradeStoreWriter.vb:926`), so no legacy row
in the tracked store can carry a timestamp later than any identified row's. Under that invariant the "compare
by time whenever either side is legacy" branch can only ever agree with what a strict single-partition view
would already say for a legacy-vs-identified pair — the cycle's precondition (a legacy timestamp sandwiched
between two identified ones) is structurally excluded.

**Residual, named risk — not eliminated, bounded.** If a post-cutover row's `TradeSeq` field is ever corrupted
(not a torn line — those are already excluded by `ScanForRepair`'s `LastCompleteLineEnd` guard, `SF-8` — but a
COMPLETE line whose seventh field fails `TryParseRow`'s parse, `Core/TradeStoreWriter.vb:500`–`505`) it is
indistinguishable from a genuine legacy row and could in principle violate the invariant above. This is not
new exposure this spec introduces: `cur.Seq < 0` already treats such a row as legacy today, under the OLD sort,
and the effect there is only ever to make the walk MISS a hole (TRAP 2, `:1048`–`1055`), never to invent one or
crash. Under the new comparator the same malformed row could, in the narrow window described above, produce an
inconsistent comparer. `RepairOnceAsync`'s existing top-level exception catch (named in
`gap-repair-same-ms-page-skip-spec.md` §4.4's `PASS_FAILED`/`reason=exception` state) turns that into a loud,
logged failed pass, not silent bad data or a process crash — an existing safety net, not one this spec adds.
Not fixture-tested here beyond `A91c` (§6.3), which exercises the comparator on an adversarial but
invariant-respecting input; a genuinely corrupted modern row is named in §8 as not verified.

### 4.3 The comparator this spec specifies

```vb
' Sorted primarily by TradeSeq — the venue's own assignment order, immune to the append-order
' problem TRAP 1 names AND immune to the same-millisecond timestamp noise RR-1 measured
' (docs/gap-repair-rr1-seq-order-spec.md). A seq-less (legacy, pre-2026-08-10) row carries no
' TradeSeq to compare by, so it sorts by Timestamp against ITS neighbours instead — this is what
' keeps an INTERLEAVED legacy row in its true chronological position, which TRAP 2 depends on
' (A56d part 2: skipping past a mis-sorted legacy row reports covered ground as a hole). Valid as
' a total order because every legacy row's timestamp precedes every identified row's timestamp in
' this store (TradeSeq did not exist before the 2026-08-10 cutover) — see SO-2 in the spec above
' for the proof and its one named, bounded residual risk.
rows.Sort(Function(a, b)
              If a.Seq >= 0 AndAlso b.Seq >= 0 Then Return a.Seq.CompareTo(b.Seq)
              Dim c As Integer = a.TsMs.CompareTo(b.TsMs)
              If c <> 0 Then Return c
              Return a.Seq.CompareTo(b.Seq)
          End Function)
```

**Provable characterisation, worth stating verbatim in the commit message:** for any set of rows containing no
seq-less row, OR containing no timestamp/seq inversion among the seq-carrying rows, this comparator produces
the IDENTICAL order to today's `(Timestamp, TradeSeq)` sort. It changes ordering ONLY for a pair of
identified rows whose timestamp order disagrees with their seq order — precisely, and only, `RR-1`'s trigger
condition.

### 4.4 Hand-walk against `A56d` part 2 under the new comparator

`Compare(R1, R2)`: R2 legacy → time → `0 < 30000` → R1 first. `Compare(R1, R5)`: both identified → seq →
`3000 < 3500` → R1 first. `Compare(R2, R5)`: R2 legacy → time → `30000 < 120000` → R2 first. Result: **R1, R2,
R3, R4, R5 — identical to today's order**, because R1's true time (`t=0`) already precedes every legacy row
here; the new comparator never needed to override time for this fixture. Walk: R1 (`hasPrev=False→True,
prev=3000`) → R2/R3/R4 (legacy, each resets `hasPrev=False`) → R5 (`hasPrev` is `False` from R4 → no delta
computed, `prev=3500`). **Zero holes. `Tail.FirstSeq = 3501`, matching `A56d`'s existing assertion exactly.**

---

## 5. What this spec does not touch

`ScanForRepair` (the scan, the bracket-selection "maximum timestamp below `segStartMs`" rule, the
`LastCompleteLineEnd`/`SF-8` torn-row guard, the truncation/floor logic) · the cross-month seed (§1b,
`Core/TradeStoreWriter.vb:996`–`1018`) · the tail/`AnchoredTail` resolution (§8, `:1106`–`1120`) · `GT-3`'s
`StopAfterMs` check · `MaxHolesPerPass` / `MaxScanRows` · `AppendRows` · `DedupTrades` · `settings.json` (no
key) · `repair_status.log`'s format · `analysis_log.csv` · every rendered surface (`BuildPlaintextSnapshot`,
`MainForm_Render_Cards.vb`) — this is a repair-path defect, not a scoring or display defect, so the
display-string parity rule does not apply. **One observable side effect, worth naming so it is not mistaken
for a new defect:** `repair_status.log` will simply stop emitting the `HOLE_REPAIRED` lines for whichever
brackets were phantom — an operator comparing before/after logs will see fewer lines, which is the fix working,
not data going missing.

---

## 6. Fixtures

### 6.0 ID availability, verified this session

```
grep -noE "A9[0-9][a-z]?" verify/ordercheck/Program.vb | sort -t: -k2 -u -V | tail -15
```
Highest existing family is `A90a`–`A90k` (the Kelly one-class/placed-payoff build, `31f57d0`). `A91` is free —
no `A91*` anywhere in `verify/ordercheck/Program.vb` or `docs/*.md` at this session. **`RR-1`, `SO-1`, `SO-2`,
`ST-1` also checked free** (grepped against `docs/*.md`, `Core/*.vb`, `verify/ordercheck/Program.vb`; only
prior hit is this spec's own source read).

**Fixture-literal provenance (CLAUDE.md hard rule).** Every timestamp and seq value below is **MECHANISM** —
small numbers chosen to reproduce the measured SHAPE (a run of consecutive seqs where one carries an earlier
millisecond than its lower-seq neighbours), not the production seq values themselves. Say so in the comment at
each call site, per the rule.

### 6.1 `A91a` — the fail-first phantom reproduction

**Shape**, mirroring `gap-repair-repeat-fills-read-2026-09-25.md` §4.2 exactly (seq `N` at `t=100000`, `N+1` and
`N+2` at `t=100001`, `N+3` at `t=100000` — ONE ms earlier than `N+1`/`N+2` despite the higher seq — `N+4` at
`t=100002`):

| Row | Seq | `t` (ms) |
|---|---|---|
| 1 | `N` | `100000` |
| 2 | `N+1` | `100001` |
| 3 | `N+2` | `100001` |
| 4 | `N+3` | `100000` |
| 5 | `N+4` | `100002` |

**Asserts:** `ResolveRepairWindows` over this store returns exactly ONE window — the `Tail`, `FirstSeq = N+5` —
and ZERO `Hole`-kind windows. **Session step 2 (§0): run this against TODAY'S code first.** It must FAIL,
returning two `Hole` windows (`[N+1, N+2]` and `[N+3, N+3]`), reproducing `RR-1`'s exact shape. Paste that
output into the spec-back as the fail-first proof.

**Mutation that must fail it:** revert the comparator to the current `(Timestamp, TradeSeq)` sort (i.e., the
code being replaced) → the fixture fails exactly as it does pre-fix. This IS the regression test.

### 6.2 `A91b` — mixed population: legacy block, interleaved, AND an inversion in the identified tail

Stacks TRAP 2's interleaving shape (`A56d` part 2) with `RR-1`'s inversion, so the fix is proven under both at
once rather than in isolation:

| Row | Kind | Seq | `t` (ms) |
|---|---|---|---|
| L1 | legacy | `AbsentSeq` | `0` |
| L2 | legacy | `AbsentSeq` | `30000` |
| I1 | identified | `M` | `100000` |
| I2 | identified | `M+1` | `130000` |
| I3 | identified | `M+2` | `129000` (⚠ earlier than I2, despite the higher seq) |
| I4 | identified | `M+3` | `140000` |

**Asserts:** exactly ONE window, `Tail`, `FirstSeq = M+4`. Zero `Hole` windows — neither a phantom from the
`I2`/`I3` inversion, nor a phantom from `L1`/`L2` being skipped past `I1`.

**Mutation that must fail it:** the category-partition comparator from §4.1 (`If a.Seq < 0 Xor b.Seq < 0 Then`
return legacy-first unconditionally, ELSE compare by seq) → `L1`/`L2` sort ahead of `I1` regardless of `t=0`,
`I1` and `I4` (or `I1` and the nearest surviving identified neighbour) become adjacent, and a phantom `Hole` is
emitted. This is the same failure worked by hand in §4.1, reproduced as a runnable regression.

### 6.3 `A91c` — comparator validity on an adversarial-but-invariant-respecting input

Directly exercises `ST-1`'s cycle precondition and confirms it cannot fire when the legacy-before-identified
invariant holds: one legacy row, plus the two seq/time-inverted identified rows from `A91a` (rows 2 and 4:
`N+1` at `t=100001`, `N+3` at `t=100000`), with the legacy row's timestamp placed BEFORE both of them (respecting
the invariant, unlike the `ST-1` counter-example where it was placed between two inverted rows by
construction — that placement is impossible here because the legacy row predates the cutover and both
identified rows postdate it).

**Asserts:** `List.Sort` completes without throwing, AND the resulting order places the legacy row first,
`N+1`-before-`N+3` reversed to seq order (i.e. `N+3` before `N+1` by seq, since `N+3`'s seq is higher — same
as `A91a`'s no-hole result). **This is the direct regression guard for `ST-1`**: if a future edit reintroduces
a comparator that mixes keys inconsistently, this is the fixture most likely to surface it via
`InvalidOperationException` rather than a silently wrong window.

**Mutation that must fail it:** swap in the "compare by time whenever either side is legacy, else by seq"
comparator but WITHOUT the tie-break `Return a.Seq.CompareTo(b.Seq)` on the time-equal path, using a data
variant where the legacy row and an identified row share an exact millisecond — the assertion on tie-break
determinism fails, demonstrating why the tie-break line in §4.3 is not decorative.

---

## 7. Reserved class — deploy is the trader's, this spec is not

This changes engine-binary behaviour on the trade-store repair path (`ResolveRepairWindowsCore`, which decides
what `TradeStoreGapRepair` fetches and commits to `trades_YYYY-MM.csv`). Per CLAUDE.md's reserved-class table:

| Reserved class | Applies here? |
|---|---|
| `settings.json` changes | **No** — no key added or changed |
| Anything that affects scoring | **No** — the repair path does not feed `ScoringEngine`; `analysis_log.csv` is untouched |
| Anything that moves a rendered value | **No** — no UI card or `BuildPlaintextSnapshot` line changes (§5) |
| Writes to the live collector or trade store | **Yes** — this changes what `AppendRows` gets called with (fewer duplicate re-appends of already-present rows). Tape writes are permanent; the mechanism deciding them is exactly the reserved surface |
| Any schema or CSV-header change | **No** — `trades_YYYY-MM.csv` and `repair_status.log`'s format are both unchanged; only which lines get written |

**So: yes, this needs the trader's deploy ruling, on the "writes to the trade store" ground alone, even though
the change makes those writes strictly SMALLER (fewer duplicates) rather than different in kind.** Build and
harness-verify locally; do not deploy without a ruling, and do not fold it into an unrelated deploy silently —
name it in the deploy checklist the way `gap-repair-same-ms-page-skip-spec.md` §7 named its own commits.

---

## 8. What this seat did not verify

- **Whether a post-cutover row can ever legitimately fail `TryParseRow`'s seq field and be indistinguishable
  from a genuine legacy row** (§4.2's residual risk). Not reproduced; reasoned from `TryParseRow`
  (`Core/TradeStoreWriter.vb:487`–`512`) and `ScanForRepair`'s torn-row guard, not from an observed instance in
  the store.
- **Whether `TradeSeq` is ever non-monotonic across a GENUINE venue-side reset**, as opposed to the sub-
  millisecond, same-burst inversion `RR-1` measures. Carried forward from the existing code comment at
  `Core/TradeStoreWriter.vb:1064` ("whether Deribit ever RESETS trade_seq was never verified project-wide") —
  this spec does not newly verify it, and the new comparator's `delta < 0` branch becomes unreachable for
  seq-sorted identified pairs (a sorted list cannot produce a negative adjacent delta by construction). Left as
  harmless defensive code; not removed, since removing it is a separate, unrequested clean-up.
  [`gap-repair-same-ms-page-skip-spec.md`](gap-repair-same-ms-page-skip-spec.md) §9 raised the same open
  question ("that the venue never skips a seq … not proved") — still open, not re-closed here.
- **How often the `RR-1` inversion shape occurs beyond the one 2.5-day log window the read measured.** No wider
  scan of `repair_status.log` history was run by this seat; carried from the read's own §6.
- **No build and no harness run.** This is a spec. §0's session plan is the plan for the seat that builds it,
  not evidence that it has been executed.
- **Whether any consumer other than the repair pass reads `RepairWindow.LeftTsMs`/`RightTsMs`.** Grepped only
  inside `Core/TradeStoreWriter.vb` and `Core/RepairStatusLog.vb`'s described contract in the cited spec; not
  re-checked against the full tree at build time.

---

## 9. Acceptance

- `A91a`'s fail-first run pasted (§0 step 2), showing the two phantom `Hole` windows on today's code.
- `A56c` and `A56d` pass with **unchanged assertions** — this spec's parity claim (§4.4), not merely "still
  green."
- `A91a`–`A91c` pass, and each one's named mutation (§6.1–§6.3), run once from a scratch copy, is pasted as
  failing.
- Whole harness `ALL PASS`. `dotnet build DeribitVerdictEngine.sln -c Release`: 0 errors.
  `tools/checks/verify-gate.ps1` passes.
- The `TRAP 1` comment block (`Core/TradeStoreWriter.vb:1020`–`1029`) is rewritten to describe the new
  comparator and cites this spec, not left describing `(Timestamp, TradeSeq)` sorting that no longer exists.
- A settings-untouched row in `DeribitIndicatorProject.md` §15: engine-binary change on the repair path, no
  scoring impact, no `analysis_log.csv` boundary, a store-completeness fix (fewer phantom re-fetches and
  duplicate rows), reserved for deploy per §7.
- Commit message states which card surface is affected (none) per the display-string parity rule, and names
  §7's reserved-class ground explicitly so the orchestrator does not need to re-derive it.

---

## 10. Evidence handles

**`H-1` — the current comparator, for before/after diff.**
```bash
sed -n '1020,1034p' Core/TradeStoreWriter.vb
```

**`H-2` — `A56c` and `A56d`, run in isolation, before touching any code.**
```bash
grep -n "Private Sub A56c_OutOfOrderStoreProducesNoPhantomHoles\|Private Sub A56d_AbsentSeqRowsProduceNoPhantomHoles" verify/ordercheck/Program.vb
```

**`H-3` — next free fixture family, re-runnable.**
```bash
grep -noE "A9[0-9][a-z]?" verify/ordercheck/Program.vb | sort -t: -k2 -u -V | tail -15
```

---

## 11. Provenance

Written against `Core/TradeStoreWriter.vb` and `verify/ordercheck/Program.vb` as tracked at commit `410deb6`
(`HEAD` at session start, per `git status -sb`, working tree clean). Re-verify line numbers before quoting if
the build starts from a later commit — CLAUDE.md's rule for any handle written about code, extended here to a
forward-looking spec's line citations.
