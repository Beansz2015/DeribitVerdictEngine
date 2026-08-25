# C1-coverage F1 — trailing-edge gap mis-attribution: implementation spec

**Status:** PROPOSED. ⛔ **D-1…D-6 in §4 ALL await a trader tick. Do not begin coding until they are ruled** — this is not a spec with one open question, it is six coupled ones.
**Author seat:** Opus, 2026-08-25. **Origin:** [`c1-session1-review-2026-08-04.md`](c1-session1-review-2026-08-04.md) §3 · [`trader-tick-queue.md`](trader-tick-queue.md) §2.

---

## 0. Model + effort — **Sonnet, effort HIGH**, one session

⚠ **THE QUEUE'S SIZING IS WRONG AND IS CORRECTED HERE.** That row reads *"Small-medium; one new fixture."* The investigation found **six existing fixtures that a naive fix breaks**, **three more at risk**, **six coupled decisions**, and a change that **moves the CLI's `--strict` exit code**. It is not small-medium and it is not one fixture.

**Why HIGH and not medium.** This changes hour classification in `CoverageReport` — *"the precondition instrument for every data-gated item"*, the thing that decides whether future collection gaps are seen at all. **Being wrong here is expensive and hard to notice**, which is CLAUDE.md's own high-effort criterion. It also alters `--strict`'s exit code, so it can fail a scheduled job.

**Where the implementer will slip — four traps, all with code in §5:**

1. ⛔ **`ResolveBoundaryUtc` does NOT protect the two fixtures everyone assumes it protects.** With no evidence files it returns `toUtc` unchanged, so `A49e` and `A49g` get no bound at all. **"The store simply ends" is a DIFFERENT bound from "the evidence boundary."** This is the crux — §3.
2. ⛔ **`A49u` forecloses the obvious implementation.** It asserts `span0.LongestGapMs = 0` *exactly*, so folding the trailing gap into `LongestGapMs` breaks it. That fixture, not taste, decides new-field-vs-widen.
3. ⚠ **A new `HourStoreStats` field silently poisons four hand-built fixtures.** They construct the type with property initialisers; a `LastTsMs` defaulting to `0` reads as "last trade at 1970-01-01" — a 56-year trailing gap.
4. ⚠ **A "Captured with a warning" design is invisible.** `BuildMarkdown` skips every `Captured` hour, and the console prints class counts only. The reason string would go nowhere.

⚠ **The fixtures cannot be trusted to catch traps 1 and 3**, because the implementer revises those same fixtures. §6 therefore specifies the blast radius as a table of *named existing fixtures* and what each must assert afterwards — **revising a fixture your own change breaks is this project's named hazard, and the way through it is to say so, name why, and re-verify.**

**Escalation trigger — stop and move to Opus/high if:** the chosen bound requires `ClassifyHour`/`ClassifySpan` to know about anything beyond a single extra scalar argument, **or** if `ObservedLongestGapMs` / `GapBreachHours` (D-6) turn out to need restructuring rather than an added term. Either means the blast radius has left this spec.

---

## 1. The defect

**`Captured` does not mean "fully covered". It means "no gap ENDING in this hour breached the threshold."**

`AccumulateHourStats` charges a gap to the bucket of the trade that **ends** it (`CoverageReport.vb:415-420`, `stats` resolved from `t`'s own hour at `:408-413`). So trades at :00–:05 followed by silence until the next hour's :30 charge that 85-minute gap to the **following** hour. The hour it started in reads `Captured` despite ~55 minutes of silence.

**Not data loss** — the following hour still flags `Defect`. But a reader reasonably takes `Captured` to mean complete, and the report currently ships a note admitting this rather than fixing it (`CoverageReport.vb:1100-1110`).

⭐ **The note itself records why it was not fixed blind**, and that reasoning still binds: *"the bounded fix needs the trailing-evidence boundary this same file already computes, and rushing it risks a worse mis-attribution than the one it fixes."*

---

## 2. Verified map — every claim file:line, checked 2026-08-25

| # | Fact | Site |
|---|---|---|
| 2.1 | `HourStoreStats` is **exactly** `RowCount` + `LongestGapMs` | `CoverageReport.vb:93-96` |
| 2.2 | Gap is charged to the **ending** trade's hour | `:415-420` |
| 2.3 | Trades are sorted ascending before the walk | `:405` |
| 2.4 | `prevTs` carries across hour **and month** boundaries, deliberately | `:394-395`, `:419` |
| 2.5 | ⭐ **The last-trade timestamp is available but never persisted** — `prevTs` holds it; the natural capture point is beside `stats.RowCount += 1` | `:414` |
| 2.6 | `storeClean` — the line the defect lives in | `:505` |
| 2.7 | `ClassifySpan` is shared by BOTH the whole-hour and split paths | `:491-524`, called `:565` and `:596` |
| 2.8 | A sibling accumulator exists for spans and must be mirrored | `AccumulateSplitSpanStats`, `:435-472` |
| 2.9 | ⛔ `ResolveBoundaryUtc` has **exactly one caller**, and its result is **not** passed to `ClassifyHour` | defined `:664-669`, called `:924` |
| 2.10 | `ClassifyHour` signature carries no boundary/last-hour parameter | `:541-547` |
| 2.11 | Span combine order (D-3, ruled 2026-08-13) | `:609-624` |
| 2.12 | ⚠ `BuildMarkdown` **skips every `Captured` hour** | `:1127` |
| 2.13 | ⚠ `--strict` exits non-zero on any `Defect` | `BacktestProgram.vb:321` |
| 2.14 | `ObservedLongestGapMs` / `GapBreachHours` read **only** from `hourStats`, never spans | `:975-976` |
| 2.15 | The F1 placeholder note this spec must delete | `:1100-1110` |

---

## 3. ⛔ THE CRUX — the bound everyone assumes is the wrong bound

The origin ruling says: bound `hourEnd − lastTradeInHour` against **`ResolveBoundaryUtc`**. **That is necessary but NOT sufficient, and the gap is not obvious.**

`ResolveBoundaryUtc` returns `toUtc` unchanged when there is **no evidence** (`:665`). Two shipped fixtures run exactly that way:

- **`A49e`** — window is exactly one hour; trades at 05:01 and 05:06; the hour runs silent to 05:59:59.999. **A ~54-minute trailing edge, and `ClassifyHour` there receives no boundary at all.**
- **`A49g`** — runs through `BuildResult` with `ToUtc = day + 6h`, trades at 05:01/05:02, no evidence files ⇒ `walkToUtc = 06:00`. **The boundary does not protect hour 05.**

⭐ **So there are TWO distinct trailing-edge exclusions and they are not the same thing:**

| Bound | Means | Protects |
|---|---|---|
| **Evidence boundary** (`ResolveBoundaryUtc`) | "we stopped observing here" | Hours past the newest evidence |
| **Store-end** ("the last hour that has any data") | "the tape simply ends here" | `A49e`, `A49g`, and every ad-hoc run over a finite store |

**A fix that implements only the first still flags the last hour of every run that has no `ws_health` evidence — which is most manual invocations.** D-4 rules which bound(s) apply.

⚠ **A third, separate edge:** `walkToUtc` is **not hour-aligned** and the loop is `While cursor < walkToUtc` (`:968`), so a **partial final hour** is walked and classified as a whole hour. A naive `hourEndMs − lastTs` measures to `:59:59.999` rather than to the boundary instant. **Same defect class as the one being fixed, in the opposite direction.**

---

## 4. D-table — ⛔ all six await a trader tick

| # | Decision | Options | **My read** |
|---|---|---|---|
| **D-1** | Where does the rule live? | (a) in `ClassifySpan` — both paths for free · (b) only the non-split branch of `ClassifyHour` | ⭐ **(a).** (b) leaves split hours carrying the original defect, and SH-1 exists precisely because split hours were being mis-scoped. ⚠ **But (a) applies an absolute 300 s threshold to spans as narrow as 60 s** — see D-5's coupling |
| **D-2** | New field, or widen `LongestGapMs`? | (a) new `LastTsMs` field · (b) fold the trailing gap into `LongestGapMs` | ⭐ **(a), and the choice is FORCED, not aesthetic.** `A49u` asserts `span0.LongestGapMs = 0` exactly; (b) breaks it. **Keeping the two separable also keeps "gap between trades" and "silence to the edge" distinguishable in the reason string, which is the whole reader-facing point** |
| **D-3** | Default for the new field | (a) sentinel `-1` = "unknown, do not evaluate" · (b) `0` and update the four fixtures | ⭐ **(a) sentinel.** `0` means 1970-01-01 — a 56-year trailing gap — on all four hand-built fixtures, and it fails **silently and in the alarming direction**. A sentinel forces an explicit guard, which is the fail-closed posture the AutoTweaker F1 review just settled |
| **D-4** | ⛔ **Which bound?** — §3 | (a) evidence boundary only · (b) evidence boundary **AND** store-end · (c) (b) plus hour-aligning the partial final hour | ⭐ **(b) at minimum; (c) is correct but larger.** (a) alone flags the final hour of most manual runs — a false defect on the instrument whose credibility is the point. ⚠ **If (c) is deferred, say so in the report note rather than leaving it silent** |
| **D-5** | What does a trailing-edge hour BECOME? | (a) `Defect` · (b) `Captured` + a reason · (c) a new `HourClass` | ⚠ **(a), reluctantly, and this is the one I am least sure of.** (b) is **invisible** — `BuildMarkdown:1127` drops every `Captured` row (trap 4). (c) is honest but adds a class to a six-value enum, the D-3 combine, and every count surface. ⛔ **(a) changes `--strict`'s exit code**, so it can fail a scheduled job the first time it runs. **If that is unacceptable, (c) is the answer and this spec grows** |
| **D-6** | Coherence with the header counters | (a) fold trailing gaps into `ObservedLongestGapMs`/`GapBreachHours` · (b) leave them | ⭐ **(a).** Those read only from `hourStats` (`:975-976`). Leaving them makes the console report a *smaller* longest-gap and *fewer* breaches than its own defect count implies — **a visible internal contradiction in one report**, which is worse than either number alone |

---

## 5. The four traps, with the code

**5.1 — the boundary that isn't.** `ResolveBoundaryUtc:665`: `If evidence Is Nothing OrElse evidence.Count = 0 Then Return toUtc`. **No evidence ⇒ no protection.** §3.

**5.2 — `A49u` forecloses widening.** `Program.vb:8445` asserts `span0.LongestGapMs = 0`. Any fix that widens `LongestGapMs` to carry the trailing edge changes that value and breaks the fixture. **The fixture decides D-2.**

**5.3 — the default-value poison.** Four sites build `HourStoreStats` with initialisers and get the declaration default for anything new: `Program.vb:7757`, `:7791`, `:8094`, `:8311`. **A `LastTsMs = 0` makes all four read as a 1970 last-trade.**

**5.4 — the invisible reason.** `BuildMarkdown:1127` — `If h.Classification = HourClass.Captured OrElse … Then Continue For`. A Captured-with-reason hour renders **nowhere**; the console prints class counts only (`:1009-1014`).

---

## 6. Fixture blast radius — the six that break and the three at risk

⛔ **Every row below is an EXISTING fixture. The implementer will be revising tests they did not write, to accommodate their own change. Name each revision and why, in the spec-back.**

| Fixture | Why it moves | Must assert afterwards |
|---|---|---|
| **`A49e`** | ⛔ **Most exposed.** One-hour window, trades 05:01/05:06, ~54 min trailing, **no boundary passed at all** | Whatever D-4 rules. **If it still asserts `Captured`, the bound works; if it must change, D-4(a) was insufficient** |
| **`A49g`** | Hour 05 has ~57 min trailing and the store ends; `walkToUtc = 06:00` gives no protection | Same — this is D-4's second witness |
| **`A49b`, `A49c`, `A49l`, `A49r`** | Hand-built `cleanStats` inherit the new field's default (trap 3) | Unchanged classification, once D-3's sentinel is in |
| **`A49r`** *(also)* | ⚠ **Doubly exposed** — additionally asserts `String.IsNullOrEmpty(hr.Reason)` | Forecloses "Captured + explanatory reason" for the non-split path |
| **`A49o`, `A49t`** | 🟡 Per-span rule: ON spans with ~27 min and ~10 min trailing edges | Depends on D-1 + D-5 |
| **`A49s`, `A49u`** | 🟡 Assert exact `HourStoreStats` contents | `A49u` is D-2's decider |
| **`A49v`** | 🟡 Asserts no classification, so it survives — **but hour 00 is the exact F1 shape** (one trade at 00:05, then silence all day) | ⭐ **Extend this one as the F1 regression trap** — it is the only fixture that already reproduces the defect end-to-end through `BuildResult` |

**New fixtures required — specified by their required FAILING input, not their intent:**

| New | Required input |
|---|---|
| **F1-a** | Trades early in an hour, silence to hour end, **and a later trade well into the next hour** — the canonical shape. Must flag under D-5's ruling |
| **F1-b** | ⛔ **The bound.** The same shape but where the hour IS the last hour with data. Must **NOT** flag |
| **F1-c** | The partial-final-hour case (boundary mid-hour, D-4(c)) — must not measure to `:59:59.999`. **If D-4(c) is deferred, this fixture pins the DEFERRAL** so the gap is recorded rather than forgotten |

⛔ **Mutation-proof `F1-a` and `F1-b`**, per standing convention: revert the guard, confirm each FAILS, restore, re-run. **A fixture that passes on the unfixed code proves nothing.**

---

## 7. Acceptance

- Six projects build **0/0 Release**, each separately.
- Harness **ALL PASS**, `A1`–`A59e` unregressed except the revisions §6 names, plus the new F1 fixtures. ⚠ **Assert the new fixtures EXECUTED** — a harness that runs none of them still prints ALL PASS.
- `verify-gate.ps1` — `tools/` is not an engine path; expect no version-bump nudge.
- ⛔ **Delete the placeholder note at `CoverageReport.vb:1100-1110`** — it documents the defect being fixed. **Leaving it is the fix half-done.**
- ⚠ **Run the real `coverage` verb against the AWS store copy and diff the class counts against the last run.** The count of `Defect` hours **will** change under D-5(a); the spec-back must state by how much and confirm each new one is a genuine trailing edge, not the bound misfiring.

---

## 8. Out of scope

- **The `gapMs` TIME-tolerance question** — its own queue row (*"a TIME tolerance standing in for a COMPLETENESS check, the DR-1 pattern, third instance"*). ⚠ **Related and NOT to be bundled:** that one argues `trade_seq` makes the time tolerance redundant where sequences exist. **This spec keeps `gapMs` as-is.**
- **`UpInterval.IsTrailing`** (`:90`, set `:303`) — exists, currently read nowhere in classification. **Available to a fix; do not repurpose it without saying so.**
- **The leading edge** (`ResolveCaptureBeginsUtc`, `:894-907`) — a separate, already-handled bound.
- ⚠ **`AccumulateHourStats` applies no per-row `[fromUtc, toUtc)` filter** — `EnumerateMonths` reads whole month files, so the returned dictionary is a **superset** of the walked hours. Harmless today because `BuildResult` iterates its own cursor. **Named because a trailing-edge rule that iterates the dictionary instead of the cursor would inherit it.**
