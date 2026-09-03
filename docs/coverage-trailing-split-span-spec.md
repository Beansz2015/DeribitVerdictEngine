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
