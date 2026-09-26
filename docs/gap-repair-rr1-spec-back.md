# RR-1 gap-repair sort-order fix — spec-back

> Reviewer working document, per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).
> Outcome record: [`gap-repair-rr1-batch-summary.md`](gap-repair-rr1-batch-summary.md).
> Built against [`gap-repair-rr1-seq-order-spec.md`](gap-repair-rr1-seq-order-spec.md).

## 1. Ranked verification handles

**If you only run one, run `H-1`.**

**`H-1` — full harness, before/after count.**
```bash
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release
```
Load-bearing values: `486 PASS`, `1 SKIP` (`A81b`, pre-existing), `0 FAIL`, and the final line
reads `ALL PASS`. `483 + 3(A91a-c) = 486` — the arithmetic identity that confirms nothing else
silently changed pass/fail count.

**`H-2` — the comparator, current vs. spec's §4.3.**
```bash
sed -n '1020,1046p' Core/TradeStoreWriter.vb
```
Confirms the shipped code matches the spec's §4.3 comparator exactly (same branch order, same
tie-break line) and that the `TRAP 1` comment now cites `RR-1` rather than describing the deleted
`(Timestamp, TradeSeq)` sort.

**`H-3` — `A56c`/`A56d` parity, isolated.**
```bash
dotnet run --project verify/ordercheck/OrderCheck.vbproj -c Release 2>&1 | grep -E "^(PASS|FAIL)  A56c|^(PASS|FAIL)  A56d"
```
Both must read `PASS` with the same detail strings as the pre-fix run (`docs/gap-repair-rr1-batch-summary.md`
§2 has the before/after table). This is the parity proof the spec's §4.4 argues for by hand.

**`H-4` — the diff is scoped to the sort only.**
```bash
git show 72f262e --stat
git diff 72f262e~1 72f262e -- Core/TradeStoreWriter.vb
```
Confirms `ScanForRepair`, `AppendRows`, `DedupTrades` and the bracket-selection rule are untouched
— the third escalation trigger's ground.

**`E-1` — the `A91c` tie-break mutation (build-time evidence, NOT re-runnable).** The temporary
test and the no-tie-break mutant comparator were both reverted after capturing output; they are
not in the tree. Pasted output, for the record:
```
order1= [0]kind=Hole first=5001 last=5004 [1]kind=Tail first=5006 last=-1
order2= [0]kind=Tail first=5006 last=-1
```
Two constructions of the same three rows (a legacy row and an identified row sharing an exact
millisecond, plus a third row five seqs later) resolve differently: order 1 correctly finds
`Hole[5001,5004]`; order 2 silently drops it. This is why the shipped comparator's tie-break line
(`Return a.Seq.CompareTo(b.Seq)` on the time-equal path) is load-bearing, not decorative. If you
need to re-verify this rather than trust the pasted output, the reconstruction is in §2 below —
it takes about five minutes to re-apply, run, and revert.

## 2. Decisions queued — with my read

### D-1 — `A91b`'s row table deviates from the spec's §6.2 literal numbers

**What happened.** The spec's own table for `A91b` puts both legacy rows chronologically BEFORE
the first identified row (`t=0`, `t=30000` vs. `I1` at `t=100000`), and gives the four identified
rows contiguous seqs (`M`, `M+1`, `M+2`, `M+3`). I built this literally first. Running it against
the category-partition mutant from the spec's own §4.1 (legacy always sorts before identified,
each half sorted on its own key), it **passed** — the mutant did not produce a phantom hole.

**Why.** Two independent reasons, either one sufficient: (1) the legacy rows already sort before
`I1` under a correct time-based reading too, since `0 < 100000` — partitioning them "ahead of
`I1`" changes nothing, because they were already going to be there; (2) the four identified rows
are seq-contiguous (`M..M+3`), so sorting them purely by seq (which the category-partition mutant
also does for the identified half) produces zero holes regardless of any legacy interleaving —
there is no seq gap for a legacy block to wrongly "cover" or wrongly "expose."

**What I did.** Rebuilt the fixture with `I1` placed EARLY (`t=0`, mirroring `A56d` part 2's `R1`)
and a genuine wide seq gap (`M` to `M+500`) bracketed by the interleaved legacy rows, keeping the
`RR-1`-style inversion in the two rows after the gap (`M+500`/`M+501`). Re-ran against the
category-partition mutant: **fails**, phantom `Hole[7001,7499]`, matching `A56d` part 2's own
failure shape exactly.

**My read:** this is a bug in the spec's worked fixture, not a design question — the spec's own
§4.1 prose describes the failure mechanism correctly, but the §6.2 table doesn't reproduce the
precondition (a seq gap) the mechanism needs. I fixed the numbers, not the design (`SO-1` (a) is
unchanged), and named the deviation inline in the fixture's own comment and in the commit message.
Not reserved — no design decision was made, a factual error in a worked example was corrected.

### D-2 — `A91c`'s expected result deviates from the spec's own §6.3 prose

**What happened.** The spec's table for `A91c` uses rows 2 and 4 of `A91a` (`N+1` at `t=100001`,
`N+3` at `t=100000`) plus one legacy row, and its prose asserts the result is "the same as `A91a`'s
no-hole result" — i.e. one `Tail` window, zero `Hole` windows. Building it literally: since
`A91a`'s full row set is `N, N+1, N+2, N+3, N+4` and `A91c` only carries `N+1` and `N+3`, seq `N+2`
is **never present** in this fixture — it is a genuine, not phantom, one-wide gap.

**Why the literal expectation is wrong.** Under the shipped comparator, `N+1` and `N+3` sort by
seq (ascending: `N+1` then `N+3`), and the walk correctly computes `delta = 2 > 1` between them —
a real `Hole[N+2,N+2]`, followed by a `Tail` from `N+4`. That is the CORRECT result of the
comparator working properly, not a defect. Under the OLD (pre-fix) comparator I confirmed by hand
and by running: it sorts `N+3` before `N+1` by time, computes a negative delta (`discontinuity, not
loss` per the existing code comment), and silently produces only a `Tail` from `N+2` — **missing
the real gap entirely**. So the spec's asserted "no-hole" expectation actually describes the OLD,
BROKEN behaviour, not the fixed one.

I also could not reconcile the spec's parenthetical `"i.e. N+3 before N+1 by seq, since N+3's seq
is higher"` — ascending seq order puts the LOWER seq first, so `N+1` before `N+3`, not the reverse.
I read this as a wording slip in the spec, not a hint I'm missing a design distinction, but I have
low confidence in that reading — see §3 below.

**What I did.** Changed the assertion to the mechanically correct result: `List.Sort` does not
throw, `windows.Count = 2`, `Hole[N+2,N+2]` then `Tail(FirstSeq=N+4)`. Named the deviation inline
and in the commit message.

**My read:** same as `D-1` — a bug in the spec's own worked arithmetic, not a design question.
Fixing it makes the fixture a STRONGER regression guard than the spec intended: it now proves the
comparator finds a real gap that the old comparator was silently swallowing, which is a genuine
correctness improvement worth stating plainly to the trader (`gap-repair-rr1-batch-summary.md` §1
doesn't currently say this — flagging it here since it's the kind of finding CLAUDE.md's
auto-proceed obligation asks to be logged where the trader will see it).

### D-3 — was the escalation trigger hit by `D-1`/`D-2`?

**Options.** (a) No — the trigger says *"EXISTING fixture ... needs its expected value changed"*,
and `A91b`/`A91c` were authored this session, never shipped with a different expected value before
now. (b) Yes in spirit — the spec's own author (this seat, in the same document) got two fixture
designs wrong on the first pass, which is exactly the kind of thing the trigger exists to catch
before it ships quietly.

**My read:** (a), on the literal wording, and that is what I acted on — I did not stop. I flag (b)
explicitly because I have no way to self-assess this cleanly (CLAUDE.md's own reasoning: self-
assessment is the faculty that fails). If the trader reads the trigger's intent as covering this
case, the two fixtures are already fixed and hand-walked twice each (once against the mutant,
once against the shipped comparator) — no rebuild would be needed, only a different classification
of what happened.

### Rulings — 2026-09-26 (UTC), trader

Orchestrator review of 2026-09-25: both fixtures re-derived by hand, and one mutation run (item 3 of the note below).

| Decision | Ruling | Basis |
|---|---|---|
| `D-1` | ✅ **ACCEPTED as the implementer read it** — the rebuilt `A91b` table stands | Orchestrator re-derived both tables by hand: the §6.2 literal table passes the category-partition mutant (no phantom), the rebuilt one fails it (`Hole[7001,7499]`) and fails the old comparator (`Hole[7501,7501]`); the shipped comparator gives one `Tail` at 7503 |
| `D-3` | ✅ **(a) — the escalation trigger was not hit** | The trigger guards comparator drift from §4.3; the comparator is verbatim and `A56c`/`A56d` pass unchanged. The corrections were surfaced, not buried |
| `D-2` | ⏸ **OPEN — decide after the check below** | The corrected expected value (`Hole[N+2,N+2]` + `Tail` from `N+4`) was re-derived and is correct. Three problems were found around it; see the note |

⚠ **Note for `D-2` — verify later, before ruling.** Orchestrator findings of 2026-09-25, to check:

1. The claim that the old comparator "silently never fetched" the real `N+2` gap is **false**: the old sort ends on `N+1`, so the `Tail` starts at `N+2` (`Core/TradeStoreWriter.vb:1122-1125`). In general the old walk only over-fetches. RR-1 improves precision, not recall. The `A91c` comment and this document's `D-2` text should not tell the trader otherwise.
2. ⏳ **TO VERIFY:** `A91c` probably cannot surface `InvalidOperationException`. On 3 rows .NET sorts by insertion sort (≤16 elements), which never detects an inconsistent comparer, so the "no throw" check may be vacuous and `A91c` is not an `ST-1` guard. **Source: .NET runtime knowledge, not verified in this repo.** Verify by checking the runtime's introsort threshold, or by running an inconsistent comparer over a 3-row and a 17+-row list.
3. **Measured:** no fixture guards the tie-break line. Deleting `If c <> 0 Then Return c` / `Return a.Seq.CompareTo(b.Seq)` in `Core/TradeStoreWriter.vb` left the harness at **486 PASS, ALL PASS**. The only cover was the reverted scratch test `E-1`.

Orchestrator read for `D-2`: accept the values, fix the two false comments, and add `E-1`'s construction as a permanent part 2 of `A91c` (both insertion orders must give `Hole[5001,5004]`), plus narrow the code comment's legacy-before-identified invariant to the true condition (no legacy row with `tsC < tsB ≤ tsA` for a seq-inverted pair).

## 3. Spec-back proper — feedback on the spec

**What the spec got right, specifically.**

- §4.2's `ST-1` counter-example (the transitivity cycle) is exactly right and saved real
  implementation time — I did not need to re-derive why "compare by time whenever either side is
  legacy" fails; the spec's worked cycle (`A < C`, `C < B`, `B < A`) made the trap obvious before
  writing any code.
- §4.1's hand-walk of `A56d` part 2 under the category-partition mutant is correct and matches
  what I measured when I ran it (`A56d` failed under that mutant exactly as predicted, era part
  passing / interleaved part failing).
- §4.3's comparator code is correct as written — I shipped it verbatim, no changes needed.
- The fixture-literal provenance comment convention (MECHANISM, not SHIPPED) made it easy to keep
  the new fixtures honest about their invented seq bases.

**Which assumptions broke.** Both fixture tables in §6.2 and §6.3 assert results that don't follow
from their own row data — see `D-1`/`D-2` above. Neither is a deep design error; both look like
arithmetic worked out for a slightly different (and unstated) row set and then not re-checked
against the table that actually shipped in the spec.

**Where the spec was narrower than its own words.** §0's escalation trigger names "EXISTING
fixture" but the spec's own two new fixture designs (§6.2, §6.3) needed correction before they
were internally consistent — the trigger doesn't have language for "a fixture this same spec
defines turns out to assert the wrong thing." `D-3` above is exactly this gap.

**Constraint pairs that nearly conflicted.** None found this session — the "don't touch
`ScanForRepair`" scope boundary and the comparator-only fix lined up cleanly; no escape hatch was
needed.

## 4. What I did not verify, and cannot

- **Whether a post-cutover row can ever legitimately fail `TryParseRow`'s seq field** and become
  indistinguishable from a genuine legacy row (`SO-2`'s named residual risk). Not reproduced;
  reasoned from the code only, same as the spec itself.
- **Whether `TradeSeq` is ever non-monotonic across a genuine venue-side reset**, as opposed to the
  sub-millisecond same-burst inversion `RR-1` measures. Pre-existing open question, not newly
  closed or newly opened by this build.
- **How often the `RR-1` inversion shape occurs beyond the one 2.5-day log window** the read
  measured. No wider scan of `repair_status.log` history was run.
- **Whether any consumer other than the repair pass reads `RepairWindow.LeftTsMs`/`RightTsMs`.**
  Not re-checked against the full tree.
- **Deploy.** Reserved to the trader per `CLAUDE.md` (writes to the trade store) — not attempted,
  not pushed.
