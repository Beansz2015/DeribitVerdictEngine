# `ObservedLongestTrailingMs` — measure it per-span, not per-hour

**Status:** ✅ **BUILT 2026-09-03.** Harness `ALL PASS` (`A61a`–`A61e`), gate `PASSED`. A61a proven to fail pre-fix and pass post-fix; a real copy-back run before/after showed no change (its one trailing-edge hour is not split). Packet: [`queue-17-18-batch-spec-back.md`](queue-17-18-batch-spec-back.md).
**Item:** queue item **18** ([`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §2, raised 2026-08-26 from the C1-coverage F1 review).
**Ruling:** ⭐ **PER-SPAN — trader, 2026-09-03.** The alternative (stop reporting the figure on split hours) is rejected: the number is recoverable, so discarding it is a silent hole in a coverage report, which is the defect class this report exists to expose.
**Author:** the orchestrator seat of 2026-09-03.

---

## 0. Implementer brief — model, effort, and where it slips

> ### Model: **Sonnet.** Effort: **MEDIUM.**
>
> ⚠ **The queue row says "Sonnet, low." That is too low and the row predates the design.** Low was costed when the fix looked like a local arithmetic change. **It is not: the deciding span is not visible to the code that reports the number, so this changes `ClassifyHour`'s output contract and touches a ruled decision's boundary (D-1, "one row per hour").** The arithmetic is trivial; the contract is not.
>
> **Why not higher.** ⭐ **The hard part is already done and shipped.** `AccumulateSplitSpanStats` exists, is called by `BuildResult` at `:1045`, and is already threaded into `ClassifyHour`. **No new accumulation, no new pass over the store, no new file.** This is re-pointing a figure at data already in the room.
>
> **Where it will slip — three named traps:**
>
> | | Trap |
> |---|---|
> | **T1** | ⛔ **Do NOT recover the deciding span by parsing `HourResult.Reason`.** `ClassifyHour`'s own doc says the split detail *"goes in Reason only"* — that is a DISPLAY string. **Deriving a number from a display string is the failure class this project has recorded repeatedly** (the `_lastTs` count, the `grep -c` trap). If the span is not available structurally, add a structural field — never scrape one |
> | **T2** | ⚠ **Do NOT re-derive the span in `BuildResult`.** `ClassifyHour` already selects it, and a second selector is two implementations that must agree forever. **Compute the figure where the span is known and return it** |
> | **T3** | ⚠ **`HourResult` is rendered.** Confirm `BuildConsoleSummary` and `BuildMarkdown` do not enumerate its properties generically before adding a field. **A new field must change no rendered byte** — if it does, that is a display-parity event and this spec did not authorise one |
>
> **Escalation trigger — stop and move to Opus/high if:**
>
> - The deciding span cannot be identified inside `ClassifyHour` without restructuring its precedence logic. **The precedence is ruled (F1 D-5.1: TrailingEdge sits below Defect and above Captured) and this spec does not authorise touching it.**
> - Any existing fixture's expected value changes. **A per-span figure should differ from the whole-hour one ONLY on split hours** — a whole-hour case moving means the change reached further than intended.
>
> **One session.**

---

## 1. The defect, verified in the tree 2026-09-03

[`../tools/BacktestRunner/CoverageReport.vb`](../tools/BacktestRunner/CoverageReport.vb) `:1071-1078`:

```vb
If hr.Classification = HourClass.TrailingEdge Then
    result.TrailingEdgeHours += 1
    If stats IsNot Nothing AndAlso stats.LastTsMs.HasValue Then
        Dim hourEndMs As Long = hourStartMs + HourMs - 1
        Dim observedEndMs As Long = Math.Min(hourEndMs, observedBoundMs)
        Dim trailingMs As Long = Math.Max(0L, observedEndMs - stats.LastTsMs.Value)
        If trailingMs > result.ObservedLongestTrailingMs Then result.ObservedLongestTrailingMs = trailingMs
    End If
End If
```

⛔ **`stats` is the WHOLE-hour record** — `hourStats.TryGetValue(hourStartMs, stats)` at `:1058-1059`. **`splitSpanStats`, computed at `:1045` and passed to `ClassifyHour` at `:1061`, is not consulted here.**

**The case that breaks it**, from the F1 review that raised this: a split hour whose trailing span is **not** the last one — span 0 capture-ON ending in silence, a marker turns capture off, span 1 `NotCapturing` with no trades. **The gap is then measured to the HOUR end, which lies inside a span that was legitimately not capturing.** ⛔ **It reports a value no span actually had.**

⚠ **Why this is worse than its two siblings, and the F1 spec-back got this wrong.** `ObservedLongestGapMs` measures a genuine whole-hour quantity even when imprecise. **This one can invent a number.** The comment at `:1078-1080` calls it *"the same simplification the two sibling counters already accept"* — **that framing is withdrawn by this spec.**

⚠ **Reachable by inspection; NEVER DEMONSTRATED.** Nobody has constructed the case. **§4's first fixture is the first job.**

---

## 2. What is already in place — do not rebuild it

| | |
|---|---|
| `AccumulateSplitSpanStats` | ✅ Exists, `:480`. Returns `Dictionary(Of Long, HourStoreStats)` keyed per span |
| Called by `BuildResult` | ✅ `:1045`, guarded on `spanBoundsByHour.Count > 0` |
| Threaded into `ClassifyHour` | ✅ `:1061`, as the optional `spanStats` parameter |
| `observedBoundMs` | ✅ Already resolved once by `BuildResult` and threaded in |

⭐ **Everything needed is in the room. Only the reporting line reads the wrong record.**

---

## 3. The change

### R1 Compute the trailing figure inside `ClassifyHour`

**It already selects the span that makes the hour `TrailingEdge`.** Compute the trailing span's own gap there — bounded by **that span's end**, not the hour's end, and still clamped by `observedBoundMs`.

### R2 Return it structurally on `HourResult`

Add one nullable field — e.g. `TrailingMsForHour As Long?`. **`Nothing` when the hour is not `TrailingEdge`, or when the deciding span has no `LastTsMs`.**

⚠ **This touches D-1's boundary and the spec says so rather than letting a reviewer find it.** D-1 ruled **one row per hour**. **A non-rendered field adds no row and does not reopen D-1** — but state that reasoning in the spec-back, do not assume it passes unremarked.

### R3 `BuildResult` maxes over the returned value

Replace the whole-hour arithmetic at `:1073-1077` with a max over `hr.TrailingMsForHour`. ⛔ **Delete the local recomputation — do not leave both.**

### R4 Non-split hours must be bit-for-bit unchanged

**An hour with no split has one span: the hour.** The per-span figure must equal today's whole-hour figure exactly. ⭐ **This is the regression guard and it is the acceptance item that matters.**

### R5 Withdraw the misleading comment

`:1078-1080` claims parity with the sibling counters. **Replace it with what is now true:** the siblings measure a real whole-hour quantity; this one is per-span **because it could otherwise report a span that never existed.**

### R6 No settings key, no version bump, no §15 entry

`tools/` only. No engine path, no rendered surface, no CSV column. ⚠ **Confirm T3 before claiming the last one.**

---

## 4. Fixtures — family **A61**

⚠ **A61a is the first job, before any fix.** The queue row is explicit that the case has never been constructed.

| | Fixture | Asserts |
|---|---|---|
| **A61a** ⭐ | **The defect, reproduced** | A split hour: span 0 capture-ON, last trade early, then a marker turning capture OFF, span 1 `NotCapturing`, no trades. ⛔ **Build it FIRST and watch the CURRENT code report a gap measured to the HOUR end.** If it does not, the defect is not what this spec says and **stop** |
| **A61b** | The fix | Same input; the reported figure equals the **span 0** trailing gap |
| **A61c** | Non-split parity | An unsplit `TrailingEdge` hour reports **exactly** today's value — R4's guard |
| **A61d** | No-trailing-span case | A split hour where the deciding span has no `LastTsMs` ⇒ `Nothing`, and `ObservedLongestTrailingMs` is not advanced |
| **A61e** | Precedence untouched | A split hour with one clean span and one trailing span still classifies **`TrailingEdge`**, never `Captured` (F1 D-5.1) |

⚠ **Fixture-literal provenance rule applies.** These pass timestamps and a `gapMs`. **Declare at each call site:** the timestamps are **MECHANISM** (a literal is correct — say so and why); any `gapMs` asserting shipped classification behaviour must derive from `cfg`.

⭐ **Mutation-test every one:** break the thing it guards, watch it go red, restore. **A61a and A61c are the pair that matter** — one proves the defect exists, the other proves the fix did not reach further than the defect.

---

## 5. Acceptance

| | Check |
|---|---|
| 1 | `dotnet build` clean |
| 2 | ⭐ **A61a fails on unfixed code and passes after.** Recorded as a before/after, not asserted |
| 3 | ⭐ **A61c: every non-split figure unchanged.** R4's guard |
| 4 | Harness green including A61a–e |
| 5 | `tools/checks/verify-gate.ps1` green |
| 6 | **T3 confirmed:** `BuildConsoleSummary` and `BuildMarkdown` output byte-identical on a store with no split hours |
| 7 | ⭐ **Run `coverage --evidence-dir` at a real copy-back before and after; report whether `longest trailing` moved.** ⚠ **Either answer is fine and both must be reported** — no split hours in the sample means no change, which is evidence the guard works, not evidence the fix is absent |

**Report per [`batch-review-packet-convention.md`](batch-review-packet-convention.md).**

---

## 6. What this spec does not know

- ⛔ **Whether any real copy-back contains the triggering shape.** The case is reachable by inspection only. **If none exists in the sample, acceptance item 7 reports "no change" and that is a complete answer.**
- ⛔ **Whether `ClassifyHour` can name its deciding span without restructuring.** ⚠ **I read its signature and doc comment, NOT its body.** **That reading is the implementer's first real task and it is the escalation trigger above.**
- ⛔ **`HourStoreStats`' per-span key semantics** — I confirmed `AccumulateSplitSpanStats` returns `Dictionary(Of Long, HourStoreStats)` but did not verify what the key is. **Read it; do not assume it is the span start.**

---

## 7. R7 — HANDBACK, added at review 2026-09-04. `spans.First` under-reports a metric named "longest".

⛔ **This is a SPEC GAP, not an implementation defect.** R1 said *"compute the trailing span's own gap"* — **singular, assuming one trailing span per hour.** The build implemented exactly that, and its packet describes the choice openly as *"the winning span in the worst-of combine."* **Nothing was hidden and nothing was done wrong against the spec as written.**

### The defect

`CoverageReport.vb`, the split path:

```vb
Dim winner = spans.First(Function(s) s.Classification = finalCls)
result.TrailingMsForHour = winner.TrailingMs
```

⛔ **When an hour has TWO `TrailingEdge` spans, this takes the FIRST — not the largest.** `CoverageResult.ObservedLongestTrailingMs` is a **maximum**, so such an hour can contribute the smaller of its two trailing gaps.

- **Reachable:** an hour carrying ≥2 markers where two spans each exceed `gapMs` (default 300 000 ms). **Rare; not impossible.**
- **Direction: under-reports.** The safer way to be wrong — and still wrong for a field whose name is *longest*.
- ⚠ **Uncovered.** None of `A61a`–`A61e` builds an hour with two trailing spans.

### R7 The fix — max over the deciding class, NOT over all spans

⛔ **Do not simply max over every span's `TrailingMs`.** That breaks the invariant the current comment correctly relies on: **`TrailingMsForHour` is `Nothing` whenever the hour is not `TrailingEdge`.**

**Max over spans whose `Classification = finalCls`:**

```
result.TrailingMsForHour = max( s.TrailingMs ) over spans where s.Classification = finalCls
                           ( Nothing when that set has no non-null TrailingMs )
```

⭐ **The invariant survives for free.** When `finalCls` is `Defect`, every span in that set returned `Nothing` from `ClassifySpan` — verified: `ClassifySpan` has six returns passing `Nothing` and exactly one passing `trailingMs`, on the `TrailingEdge` branch alone.

⚠ **`InstanceId` keeps using `First`.** It is a representative, not an aggregate. **Only the trailing figure changes** — say so in the diff rather than letting a reviewer wonder why one line moved and its neighbour did not.

### A61f — the missing fixture

| | Fixture | Asserts |
|---|---|---|
| **A61f** | **Two trailing spans in one hour** | A split hour where **both** spans classify `TrailingEdge` with **different** gaps. ⭐ **The reported figure is the LARGER.** Build the failing case first: on the current code it reports the FIRST span's gap — **watch that happen before fixing it**, exactly as `A61a` was proven |

⚠ **Mutation-test it like the rest:** break the `Max` back to `First`, watch `A61f` go red, restore.

### Acceptance for the handback

| | Check |
|---|---|
| 1 | ⭐ **`A61f` fails on the current code and passes after.** Recorded as a before/after, not asserted |
| 2 | **`A61a`–`A61e` still pass unchanged.** A one-trailing-span hour has a single-element set, so `Max` and `First` agree — **if any of them moves, the change reached too far** |
| 3 | Harness green at **317**, gate `PASSED` |
| 4 | `TrailingMsForHour` still `Nothing` on a `Defect` hour that contains a trailing span — **the invariant, asserted directly** |

**Model: Sonnet. Effort: LOW.** One expression, one fixture. ⚠ **Not zero — `A61f` must construct a two-trailing-span hour**, a variant of `A61a`'s shape with a second marker and a second silent span.

### ✅ R7 BUILT 2026-09-04

`CoverageReport.vb`'s split path now reads `spans.Where(Function(s) s.Classification = finalCls).Select(Function(s) s.TrailingMs).Max()` — one expression, `Enumerable.Max(Of Long?)` doing the null-ignoring work for free. `InstanceId` still reads `winner.InstanceId` (unchanged, per R7's own note).

`A61f` built with two scenarios in one fixture (one `Check()`, per house convention): scenario A — two `TrailingEdge` spans of different lengths (899,999ms first, 2,099,999ms second) — reports the **larger**, not the first; scenario B — a `Defect` span alongside a would-be-trailing span — `TrailingMsForHour` stays `Nothing` (acceptance item 4, asserted directly in the same `Check`).

**Before/after proof:** reverted the fix to `winner.TrailingMs` alone, rebuilt — `A61f` **FAILED**, reporting `899999` (scenario A's first/smaller span) against `expected larger=2099999`; scenario B was unaffected (its invariant holds under `First` too, since `First` and `Max` agree on a single-element set). Restored the fix, rebuilt — **PASSED**.

Harness **317 PASS, 0 FAIL, ALL PASS** — exactly the predicted count. `tools/checks/verify-gate.ps1` **GATE PASSED**. `A61a`–`A61e` re-verified unchanged (acceptance item 2).

### R7 review response — reviewing seat, 2026-09-03 UTC

**ACCEPTED.** Packet: [`queue-18-r7-handback-spec-back.md`](queue-18-r7-handback-spec-back.md). **Everything in it reproduced independently**, including the headline: reverting to `winner.TrailingMs` gives **exactly one FAIL, `A61f`, at `trailingMsForHour=899999 smaller=899999 larger=2099999`** — byte-identical to their reported evidence. File restored and `diff`-verified identical. **317 PASS / 0 FAIL** at the fixed state; no `TEMP REVERT` residue in either file; `InstanceId` still reads `winner.InstanceId`.

⭐ **Their attack on R7 is CORRECT and it improves on what R7 claimed.** The invariant does not depend on F1 D-5.1's ordering: `ClassifySpan` pairs a non-`Nothing` `TrailingMs` with exactly one class, so filtering to `finalCls` before selecting can never surface a value unless `finalCls = TrailingEdge` — **whatever precedence produced it. The fix is robust to a future re-ordering of the worst-of combine**, which R7 did not claim and should have.

> ## ⚠ ONE LINK THEIR PROOF LEFT OPEN — and closing it found a LATENT DEFECT that predates this whole item.
>
> **They argued the empty-sequence branch is unreachable "by elimination of the five `ElseIf`s above it."** ⛔ **That elimination is not sound on its own: `HourClass` has SEVEN members and the combine handles SIX.** `Captured` · `Defect` · `TrailingEdge` · `ExpectedMissing` · `NotCapturing` · `UnknownScope` — **and `OutOfScopeWeekend`, which appears in no branch.**
>
> ✅ **Their conclusion still holds, for a reason one function away:** `ClassifySpan` has seven `Return`s and **none emits `OutOfScopeWeekend`**. It is produced only at `CoverageReport.vb:630`, on `ClassifyHour`'s pre-split path. **So no span can carry it, the `Else` is reached only when every span is `NotCapturing`, and the filtered set is non-empty.**
>
> ⛔ **But the guard is a fact about the PRODUCER, not about the combine — and it is unstated and untested.** **If anyone ever teaches `ClassifySpan` to return `OutOfScopeWeekend`** (a weekend span inside a split hour is not far-fetched), **an all-weekend split hour falls to `Else finalCls = NotCapturing` with no matching span, and `spans.First(…)` at `:709` THROWS `InvalidOperationException` — before `Max()` is ever reached.**
>
> ⭐ **This is PRE-EXISTING. `spans.First` predates R7, predates the item-18 build, and predates the split-hour work's own F1 review.** Neither seat introduced it; reviewing R7 surfaced it. **Not fixed here — it is a separate item, and it needs its own ruling** (guard the `Else`, or make the combine exhaustive over all seven, or assert the producer's return set). **Recorded so the next seat to touch that combine does not have to rediscover it.**

---

## 8. R8 — the combine handles six of seven classes. RULED (c): tripwire, not a fix.

**Ruled by the trader 2026-09-03 UTC**, on the finding surfaced while reviewing R7. **This is an amendment, not a new spec — same shape as R7, so it can be picked up from this file without a fresh read.**

> ### Model: **Sonnet.** Effort: **LOW.**
>
> ⛔⛔ **READ THIS BEFORE ANYTHING ELSE: THIS PATH CANNOT BE MADE TO FAIL TODAY, AND THAT IS NOT A DEFECT IN YOUR WORK.**
>
> **Three times in this batch a fixture was proven by making it go red first — A61a, A61f, and the A60c mutation before them. That method DOES NOT APPLY HERE.** The path is unreachable, so **no fixture can exercise it without first breaking `ClassifySpan`.** ⛔ **Do not spend a session trying to construct a failing case. There isn't one. If you find yourself editing `ClassifySpan` to make a test go red, stop — that is the trap this paragraph exists to prevent.**
>
> **Acceptance is therefore presence-and-parity, not before/after:** the harness still reads **317**, and the guard is in place. **Say exactly that in the spec-back rather than implying a proof you could not run.**

### The finding

**`HourClass` has SEVEN members. `ClassifyHour`'s worst-of combine handles SIX** — `Defect`, `TrailingEdge`, `Captured`, `UnknownScope`, `ExpectedMissing`, and `NotCapturing` via the bare `Else`. ⛔ **`OutOfScopeWeekend` appears in no branch.**

✅ **It cannot fire today**, for a reason **one function away**: `ClassifySpan` has seven `Return`s and none emits `OutOfScopeWeekend` — it is produced only at `CoverageReport.vb:630`, on `ClassifyHour`'s pre-split path. **So every span carries one of the six, the `Else` is reached only when all spans are `NotCapturing`, and `spans.First(…)` always matches.**

✅ **`spans` itself is never empty** — `boundaries` is initialised `From {hourStartMs}`.

⛔ **But the guard is a fact about the PRODUCER, and it is unstated and untested.** Teach `ClassifySpan` to return `OutOfScopeWeekend` — a weekend span inside a split hour is not far-fetched — and an all-weekend split hour sets `finalCls = NotCapturing` with **no matching span**, so **`spans.First(…)` at `:709` throws `InvalidOperationException`**, before `Max()` is reached.

⭐ **PRE-EXISTING.** `spans.First` predates R7, the item-18 build, and the split-hour F1 review. **Neither seat introduced it. Reviewing R7 surfaced it.**

### R8 The change — two parts, both small

**1. Make the failure NAMED instead of opaque.** Replace `spans.First(Function(s) s.Classification = finalCls)` with a `FirstOrDefault` plus an explicit check that fails **naming `finalCls`** — so the day it fires, the message says which class had no span, not `InvalidOperationException`.

⚠ **Keep `winner` a single value used by both `InstanceId` and nothing else.** R7's `TrailingMsForHour` line already filters `spans` independently and **must not be re-pointed at `winner`** — that would undo R7.

**2. State the dependency where the assumption lives.** A comment on the `Else` branch:

> *reached only because `ClassifySpan` never emits `OutOfScopeWeekend` — the seventh `HourClass`, handled by no branch here. If that ever changes, this `Else` and the combine's exhaustiveness must be revisited together.*

### ⛔ What was rejected, and why — do not re-propose

| | |
|---|---|
| **Add an `ElseIf` for `OutOfScopeWeekend`** | ⛔ **Requires inventing a precedence position for a class that cannot occur.** D-3 ruled the existing ordering with stated reasons; **considered-looking precedence for an impossible case is worse than the gap**, because the next reader takes it as deliberate |
| **Invert the derivation** (pick the winning span first, derive `finalCls` from it) | ⚠ **Architecturally the better answer — it kills the class by construction.** Rejected on risk: it **rewrites D-3-ruled combine logic to fix a path that cannot execute.** Recorded because it is the right shape if that combine is ever rewritten for another reason |

### ✅ R8 BUILT 2026-09-04

Two parts, both landed: **(1)** `winner = spans.First(...)` → `spans.FirstOrDefault(...)` plus an explicit `If winner.Classification <> finalCls Then Throw ...` naming `finalCls` in the message; **(2)** a comment on the `Else` branch stating the `OutOfScopeWeekend` dependency. No fixture — confirmed unreachable given `ClassifySpan`'s current seven `Return`s, not attempted by editing `ClassifySpan` to manufacture a red. Harness **317 PASS, 0 FAIL, ALL PASS** (unchanged). `TrailingMsForHour`'s line re-confirmed byte-identical by diff — the R8 edit's hunk ends before it. Gate **PASSED**. Packet: [`queue-18-r8-handback-spec-back.md`](queue-18-r8-handback-spec-back.md).

### Acceptance

| | Check |
|---|---|
| 1 | ⭐ **Harness reads exactly 317 PASS, 0 FAIL, `ALL PASS`** — unchanged. **This is a parity check: R8 must alter no outcome** |
| 2 | The named-failure guard is present, and `InstanceId` still resolves as before |
| 3 | ⭐ **R7's `TrailingMsForHour` line is UNCHANGED** — still `spans.Where(… = finalCls).Select(… .TrailingMs).Max()`. **Confirm by diff; this is the regression that would silently undo R7** |
| 4 | `tools/checks/verify-gate.ps1` green |
| 5 | **No new fixture.** ⚠ **State in the spec-back that none is possible and why** — not that none was needed |

### R8 review response — reviewing seat, 2026-09-03 UTC. ACCEPTED, with one amendment folded in.

**Packet: [`queue-18-r8-handback-spec-back.md`](queue-18-r8-handback-spec-back.md). All five acceptance items verified independently** — harness **317 / 0**, gate `PASSED`, and ⭐ **R7's `TrailingMsForHour` line does not appear in the R8 diff at all** (`grep -c` on the diff → 0), which was the acceptance item most likely to be silently violated.

⭐ **Their §1 is the standout: they distinguished "no fixture was POSSIBLE" from "no fixture was needed", and named why this differs from `A61f`** — where the case was rare but constructible. **They did not try to manufacture a red, and said so.**

⭐ **They also found the ordinal-0 concern themselves, unprompted, and reasoned it correctly for today's enum.**

> ## ⛔ THE AMENDMENT — the guard as first written made the enum's own comment FALSE.
>
> **`HourClass`'s comment states: *"Ordinal position here is inert … never by ordinal … so the insertion point is free."*** ⛔ **`FirstOrDefault` on a value tuple returns `default`, whose `Classification` is `CType(0, HourClass)` — an ORDINAL read.** The guard's soundness depended on ordinal 0 being `Captured`.
>
> ⛔ **The failure was precisely targeted: a future seat reorders so `NotCapturing` sits at ordinal 0 — which that comment explicitly authorises — and the default tuple then carries `NotCapturing`, `winner.Classification = finalCls`, the guard does NOT fire, and `result.InstanceId` is silently `Nothing`.** **The tripwire would be disarmed by an edit the file licences.**
>
> ✅ **Folded in at review: membership check FIRST (`spans.Any`), then `First()`.** Ordinal-independent; `First()` provably cannot throw after it; same named message; **and the enum's comment stays true.** Verified: harness still **317 / 0** (parity), R7's line untouched, gate `PASSED`.
>
> ⚠ **Two process notes worth keeping.** (1) A `sed`-splice appeared to leave a duplicated `End If`; **the build was the arbiter and said 0 errors — "fixing" the apparent duplicate would have broken the file.** (2) `grep -c FirstOrDefault` returns **1** after the amendment — **from the comment that explains what was deliberately not used.** That is the count-a-name trap from the 2026-08-11 ruling, hit again. **Assert the declaration, not the name.**

⚠ **One stale claim in the packet, harmless but worth naming:** it says *"neither has been committed"* of R7 and R8. **R7 was committed as `6a6f93e`.** An inherited state claim, against this project's own standing rule to run `git status -sb` rather than carry one forward.
