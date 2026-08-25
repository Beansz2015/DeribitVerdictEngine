# C1-coverage F1 — trailing-edge gap mis-attribution: implementation spec

**Status:** ✅ **BUILD-AUTHORIZED 2026-08-25 — every decision is ruled, nothing is open.** ⛔ **§4b is the SINGLE AUTHORITATIVE BUILD LIST. Read it and build from it.** §4's table is **superseded in two rows** and is kept only as the record of how the decisions were reached — its "My read" column still recommends the losing option on both. §4a carries the evidence behind the two re-rulings.
**Author seat:** Opus, 2026-08-25. **Amended:** Opus, 2026-08-25 — post-tick review (§4a) and final ruling (§4b). **Origin:** [`c1-session1-review-2026-08-04.md`](c1-session1-review-2026-08-04.md) §3 · [`trader-tick-queue.md`](trader-tick-queue.md) §2.

**Ruling history, in one line:** first tick **D-1 (a) · D-2 (a) · D-3 (a) · D-4 (c) · D-5 (c) · D-6 (a)** → §4a re-opened **D-3** and **D-6** on evidence and raised **D-5.1…D-5.5** → second tick **D-3 (c) · D-6 (c) · D-5.1…D-5.5 all (a)**. Both ticks trader-directed 2026-08-25, standing direction *"do this properly instead of the minimum."*

---

## 0. Model + effort — **Sonnet, effort HIGH**, one session

⚠ **THE QUEUE'S SIZING IS WRONG AND IS CORRECTED HERE.** That row reads *"Small-medium; one new fixture."* The investigation found **six existing fixtures that a naive fix breaks**, **three more at risk**, **six coupled decisions** — since grown to eleven — and ~~a change that **moves the CLI's `--strict` exit code**~~. It is not small-medium and it is not one fixture.

**Why HIGH and not medium.** This changes hour classification in `CoverageReport` — *"the precondition instrument for every data-gated item"*, the thing that decides whether future collection gaps are seen at all. **Being wrong here is expensive and hard to notice**, which is CLAUDE.md's own high-effort criterion. ~~It also alters `--strict`'s exit code, so it can fail a scheduled job.~~

⛔ **THE `--strict` CLAIM IS STRUCK — it was written when `D-5` (a) was the recommendation, and `D-5` was ruled (c).** Struck rather than deleted, per the quote-and-label convention. **Under the ruling `--strict` does NOT move**: it stays keyed on `HourClass.Defect` alone (`D-5.2`), and `Defect` counts are expected to be unchanged. ⚠ **That is now a thing to PROVE, not a risk to manage** — §7 requires running `--strict` both ways and reporting the exit code as unmoved. **A `Defect` count that shifts means the new class is being reached through the wrong branch.**

**Where the implementer will slip — four traps, all with code in §5:**

1. ⛔ **`ResolveBoundaryUtc` does NOT protect the two fixtures everyone assumes it protects.** With no evidence files it returns `toUtc` unchanged, so `A49e` and `A49g` get no bound at all. **"The store simply ends" is a DIFFERENT bound from "the evidence boundary."** This is the crux — §3.
2. ⛔ **`A49u` forecloses the obvious implementation.** It asserts `span0.LongestGapMs = 0` *exactly*, so folding the trailing gap into `LongestGapMs` breaks it. That fixture, not taste, decides new-field-vs-widen.
3. ✅ **CLOSED BY `D-3` (c).** *A new `HourStoreStats` field silently poisons four hand-built fixtures — a `LastTsMs` defaulting to `0` reads as "last trade at 1970-01-01".* **The ruled `Long?` defaults to `Nothing`, so the four are protected by the type.** ⚠ **Re-opens the moment anyone "simplifies" the field to a plain `Long`.**
4. ✅ **CLOSED BY `D-5` (c).** *A "Captured with a warning" design is invisible — `BuildMarkdown` skips every `Captured` hour.* **This was the argument against option (b), and (b) was not taken.** A new class renders for free; the two count surfaces that do NOT come free are explicit build items (`D-5.3`, `D-5.4`).

⚠ **Trap 1 remains live and is now the ONLY one the fixtures cannot be trusted to catch**, because the implementer revises those same fixtures. §6 therefore specifies the blast radius as a table of *named existing fixtures* and what each must assert afterwards — **revising a fixture your own change breaks is this project's named hazard, and the way through it is to say so, name why, and re-verify.** ⛔ **Two NEW silent traps arrived with the second ruling and are not in this list because they are not fixture-shaped: `D-5.1`'s precedence and `D-4`'s superset trap. Both are in §4b.**

**Escalation trigger — stop and move to Opus/high if:** the chosen bound requires `ClassifyHour`/`ClassifySpan` to know about anything beyond a single extra scalar argument, **or** if `ObservedLongestGapMs` / `GapBreachHours` (D-6) turn out to need restructuring rather than an added term. Either means the blast radius has left this spec.

⭐ **BOTH HALVES OF THAT TRIGGER ARE PRE-CLEARED BY THE RULING — do not re-derive them.** §4a.1 shows `D-4` (c)'s three bounds collapse into **one** caller-computed scalar, and the ruled `D-6` (c) **adds a counter pair beside** `ObservedLongestGapMs`/`GapBreachHours` rather than restructuring them. **Added trigger for the ruled `D-5` (c):** stop if `HourClass` needs more than ONE new value, or if `D-5.1`'s precedence needs anything other than a single insertion between `Defect` and `Captured`.

⚠ **The amendment does NOT change the tier.** Every item §4a adds is mechanical once ruled — one enum value, one combine insertion, one console line, one verdict-line term, one scalar through two signatures. **What keeps it at HIGH is unchanged and is not size: two of the ways to get this wrong (D-5.1's precedence, D-4's superset trap) fail SILENTLY, on the instrument that decides whether future collection gaps are seen at all.**

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

## 4. D-table — ⛔ SUPERSEDED IN TWO ROWS. The build list is §4b

⛔ **DO NOT BUILD FROM THIS TABLE.** It is preserved verbatim as written pre-tick, per the quote-and-label convention, because it is the record of how the decisions were reached. **Its "My read" column recommends `D-3` (a) and `D-6` (a) — both were re-ruled to (c) after evidence found in the code.** ⚠ **A reader taking this table at face value builds a `-1` sentinel and folds the header counters, which is the exact opposite of what was ruled.** **§4b is the build list.**


| # | Decision | Options | **My read** |
|---|---|---|---|
| **D-1** | Where does the rule live? | (a) in `ClassifySpan` — both paths for free · (b) only the non-split branch of `ClassifyHour` | ⭐ **(a).** (b) leaves split hours carrying the original defect, and SH-1 exists precisely because split hours were being mis-scoped. ⚠ **But (a) applies an absolute 300 s threshold to spans as narrow as 60 s** — see D-5's coupling |
| **D-2** | New field, or widen `LongestGapMs`? | (a) new `LastTsMs` field · (b) fold the trailing gap into `LongestGapMs` | ⭐ **(a), and the choice is FORCED, not aesthetic.** `A49u` asserts `span0.LongestGapMs = 0` exactly; (b) breaks it. **Keeping the two separable also keeps "gap between trades" and "silence to the edge" distinguishable in the reason string, which is the whole reader-facing point** |
| **D-3** | Default for the new field | (a) sentinel `-1` = "unknown, do not evaluate" · (b) `0` and update the four fixtures | ⭐ **(a) sentinel.** `0` means 1970-01-01 — a 56-year trailing gap — on all four hand-built fixtures, and it fails **silently and in the alarming direction**. A sentinel forces an explicit guard, which is the fail-closed posture the AutoTweaker F1 review just settled |
| **D-4** | ⛔ **Which bound?** — §3 | (a) evidence boundary only · (b) evidence boundary **AND** store-end · (c) (b) plus hour-aligning the partial final hour | ⭐ **(b) at minimum; (c) is correct but larger.** (a) alone flags the final hour of most manual runs — a false defect on the instrument whose credibility is the point. ⚠ **If (c) is deferred, say so in the report note rather than leaving it silent** |
| **D-5** | What does a trailing-edge hour BECOME? | (a) `Defect` · (b) `Captured` + a reason · (c) a new `HourClass` | ⚠ **(a), reluctantly, and this is the one I am least sure of.** (b) is **invisible** — `BuildMarkdown:1127` drops every `Captured` row (trap 4). (c) is honest but adds a class to a six-value enum, the D-3 combine, and every count surface. ⛔ **(a) changes `--strict`'s exit code**, so it can fail a scheduled job the first time it runs. **If that is unacceptable, (c) is the answer and this spec grows** |
| **D-6** | Coherence with the header counters | (a) fold trailing gaps into `ObservedLongestGapMs`/`GapBreachHours` · (b) leave them | ⭐ **(a).** Those read only from `hourStats` (`:975-976`). Leaving them makes the console report a *smaller* longest-gap and *fewer* breaches than its own defect count implies — **a visible internal contradiction in one report**, which is worse than either number alone |

---

## 4a. ⚠ POST-TICK AMENDMENT — the EVIDENCE behind the two re-rulings

⭐ **If you only want to know what to build, skip to §4b.** This section exists so the two re-rulings can be checked rather than taken on trust — **the `D-6` one overturns a recommendation this same document made, and that is worth being able to audit.**

**All line references re-verified against `tools/BacktestRunner/CoverageReport.vb` and `verify/ordercheck/Program.vb` on 2026-08-25, after the tick.**

### 4a.1 ✅ D-1, D-2, D-4, D-5 — the tick stands. Three notes, no re-ruling

**D-1(a) — §4's own "absolute 300 s on a 60 s span" worry is NOT a defect.** A span narrower than `gapMs` can never trailing-flag. That is exactly how inter-trade gaps already behave: `ClassifySpan:505` reads `stats.LongestGapMs <= gapMs`, so a 55 s gap does not flag either. **The rule is consistent with the one beside it. Do not add threshold scaling.**

⚠ **What IS worth knowing on the split path:** span K's trailing edge and span K+1's leading gap are the **same silence**, and `AccumulateSplitSpanStats` carries `prevTs` continuously across span bounds (`:451-470`), so span K+1 already charges the whole gap. **One incident will flag on two spans of one hour.** The worst-of combine makes the hour's class identical either way — **named so the implementer does not chase it as a double-count bug.**

**D-2(a) — confirmed forced, verified at the fixture.** `verify/ordercheck/Program.vb:8446` asserts `span0.LongestGapMs = 0` exactly. Widening breaks it.

**D-4(c) — correct, and it does NOT trip §0's escalation trigger.** Read as three bounds it looks like it does. It does not, because the three collapse into one scalar the caller computes:

```
observedEndMs = MIN(spanEndMsInclusive, boundaryMs, storeEndMs)
flag when (observedEndMs − lastTsMs) > gapMs
```

`ClassifySpan` and `ClassifyHour` each take **one** new scalar; `BuildResult` does the `Min`. That is inside §0's envelope.

⛔ **§8's last bullet bites D-4 directly, and the spec never connects them.** `AccumulateHourStats` reads whole month files, so its returned dictionary is a **superset of the walked hours**. **Deriving `storeEndMs` as `Max(LastTsMs)` across that dictionary reads a trade from OUTSIDE the walked range and silently un-exempts the true last hour** — the bound then fails on exactly the fixtures it exists to protect. **Take `storeEndMs` from the walk's own final `prevTs`** (`AccumulateHourStats` already holds it at `:419` and can return it) **or clamp the max to `walkToUtc`.** This is the single most likely way to get D-4 wrong.

**D-5(c) — right, and RIGHTER than §4 argues.** §4's D-5 row omits the strongest point in (c)'s favour: `BuildMarkdown:1127` skips `Captured OrElse OutOfScopeWeekend` **only**, so a new class **renders automatically**. ⭐ **Trap 4 — the invisibility that kills option (b) — does not apply to (c) at all, at zero cost.**

### 4a.2 ✅ D-5(c)'s five follow-on decisions — ALL RULED as recommended, 2026-08-25

**Every row below was ticked to my read, so the `My read` column IS the ruling.** ⚠ **Two of the five fail silently if built wrong — `D-5.1` and `D-5.3`.**

| # | Sub-decision | Options | My read |
|---|---|---|---|
| **D-5.1** | ⛔ **Where does the new class sit in the D-3 residual combine?** (`CoverageReport.vb:609-624`) | (a) between `Defect` and `Captured` · (b) below `Captured`, with the residual classes | ⭐ **(a), and it is FORCED.** Today's order is `Defect > Captured > UnknownScope > ExpectedMissing > NotCapturing`. Under (b), a split hour with one clean span and one trailing-edge span reports **`Captured`** — **SH-1's own defect reproduced in miniature**, which is the exact argument [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) was built on. ⚠ **Wrong here is SILENT** |
| **D-5.2** | Does `--strict` exit non-zero on the new class? | (a) no — leave `BacktestProgram.vb:321` keyed on `Defect` alone · (b) yes | ⭐ **(a).** Not moving `--strict` is the whole reason (c) beat (a) at D-5. ⚠ **But make it a STATED decision, not an accident of where that line happens to key** — otherwise the next person to tidy it changes the exit-code contract without knowing it was one |
| **D-5.3** | ⛔ **The VERDICT line** (`:1092-1098`) | (a) count the new class in the verdict · (b) leave it | ⭐ **(a) — this is the hole §4 never mentions.** `defectCount` reads `HourClass.Defect` only and prints **`VERDICT: clean — no capture defects`** at zero. **Under D-5(c), a report carrying ten 54-minute trailing silences prints "clean".** That is strictly worse than the placeholder note being deleted |
| **D-5.4** | The console count line (`:1009-1014`) | (a) add a seventh line · (b) fold into an existing one | ⭐ **(a).** Those six lines are hardcoded one-per-class. A seventh class with no line is counted **nowhere** in the console — trap 4 in a new costume |
| **D-5.5** | What is the class called? | (a) `TrailingEdge` · (b) `TrailingSilence` · (c) `PartiallyCaptured` | ⭐ **(a) `TrailingEdge`** — the name every document in this arc already uses. ⚠ **Not (c)** — `PartiallyCaptured` invites the reader to treat it as a flavour of `Captured`, which is precisely the precedence error D-5.1 forbids |

### 4a.3 ✅ D-6 — RE-RULED to (c), 2026-08-25. Its original rationale does not survive the code

**D-6(a)'s argument is:** *"leaving them makes the console report a smaller longest-gap and fewer breaches than its own defect count implies."*

⛔ **The longest-gap half is false, and false by construction.** For any hour whose trailing edge flags, the silence runs on to the next trade, and `AccumulateHourStats` charges that **whole** gap to the hour containing the **ending** trade (`:415-420`). The full gap is therefore **always ≥ the trailing edge** — the same silence plus the part spilling into the next hour — and is **already** in `ObservedLongestGapMs`.

**Worked, on this spec's own fixture.** `A49u` puts a trade at :25 and the next at :32. The straddling gap is **420,000 ms**; span 0's trailing edge to :29:59.999 is **299,999 ms**. The larger number is already recorded.

⭐ **So folding trailing edges into `ObservedLongestGapMs` is a strict NO-OP wherever a trailing flag fires.** The only cases where it would not be are those whose gap-ending trade falls outside the walked range — **and D-4's bounds exempt exactly those hours from flagging.**

⚠ **The `GapBreachHours` half is not a no-op — it is a double-count.** The following hour already counts the breach; adding the trailing hour counts one incident twice, silently changing the metric from *"hours where a breaching gap ended"* to *"hours touched by a breaching gap"*.

⚠ **And D-6(a) contradicts D-2(a).** D-2 was ticked to keep *"gap between trades"* and *"silence to the edge"* separable **because they are different quantities**. D-6(a) then merges them in the header counter — discarding in the summary exactly what D-2 preserved in the stats.

**✅ RE-TICKED 2026-08-25 — (c). The three options as they were put:**

| Option | |
|---|---|
| **(a) as originally offered** — fold both | ⛔ **Not recommended.** No-op on one counter, double-count on the other, and it undoes D-2's distinction |
| **(b) leave both alone** | Defensible — both counters are already correct as they stand. The new class's count then carries the trailing-edge story alone |
| ⭐ **(c) NEW — leave `ObservedLongestGapMs` and `GapBreachHours` untouched; report the trailing edge as its own pair beside them (`TrailingEdgeHours`, `ObservedLongestTrailingMs`)** | **My read.** The reader gets both quantities, each correctly labelled, with no double-count and no no-op. **It is also the only option consistent with D-2(a)** |

⚠ **Pre-existing, out of scope, named because it lives in these same two counters:** the walk loop updates `ObservedLongestGapMs`/`GapBreachHours` **outside** `ClassifyHour` (`:973-977`), so **weekend hours contribute to both** while `ClassifyHour` returns `OutOfScopeWeekend` for them. **That predates this spec. Do not fix it here — and do not "tidy" it incidentally while adding a counter beside it.**

### 4a.4 ✅ D-3 — RE-RULED to (c), 2026-08-25. The nullable, not the sentinel

**D-3(a)'s reasoning is right and is not in question.** A default of `0` reads as 1970-01-01 on the four hand-built fixtures — `verify/ordercheck/Program.vb:7757`, `:7791`, `:8094`, `:8311`, **all four verified to use `With { … }` property initialisers** — which fails silently in the alarming direction. **A default meaning "unknown" is required.**

**But `-1` is a magic number, and this file already has the idiomatic form:**

| Option | |
|---|---|
| (a) sentinel — `Public Property LastTsMs As Long = -1` | Works. The guard is `If stats.LastTsMs >= 0 Then`, and every reader must know `-1` is magic |
| ⭐ **(c) NEW — `Public Property LastTsMs As Long?`** | **My read.** Identical fail-closed behaviour — `Nothing` is the default the four fixtures inherit — but absence lives **in the type**, the guard is `If stats.LastTsMs.HasValue Then`, and the **compiler** enforces the check instead of a convention. **In-idiom:** `CoverageResult` already carries `CaptureBeginsUtc As DateTime?` and `VenueCoveredFromUtc`/`ToUtc As DateTime?` (`:96-118`) |

⚠ **This is a form change, not a semantics change.** If the implementer prefers the sentinel for symmetry with `HourStoreStats`'s two existing non-nullable fields, that is defensible — **but say which and why, rather than defaulting to `-1` because §4 said so.**

---

## 4b. ✅ THE RULED STATE — build from THIS table

**Every decision is ticked. Nothing here is open.** Where this table and §4 disagree, **this table wins** — §4 is the pre-tick record, not the instruction.

| # | Ruled | What to build |
|---|---|---|
| **D-1** | **(a)** | The rule lives in `ClassifySpan` (`CoverageReport.vb:491-524`), so the whole-hour and split paths share one implementation. ⚠ **No threshold scaling for narrow spans** — §4a.1 explains why the apparent problem is not one |
| **D-2** | **(a)** | A **NEW field** on `HourStoreStats`. **Never widen `LongestGapMs`** — `A49u` forecloses it |
| **D-3** | ⭐ **(c)** — *supersedes §4's (a)* | `Public Property LastTsMs As Long?`. Guard on `.HasValue`. ⛔ **No `-1` sentinel anywhere.** The four hand-built fixtures inherit `Nothing` and are protected by construction |
| **D-4** | **(c)** | `observedEndMs = MIN(spanEndMsInclusive, boundaryMs, storeEndMs)`, computed in `BuildResult`, passed as **ONE** scalar into `ClassifyHour` → `ClassifySpan`. Flag when `(observedEndMs − lastTsMs) > gapMs` — **`>`, so the at-threshold convention at `:505` is preserved.** ⛔ **`storeEndMs` comes from the walk's own final `prevTs`, NOT from `Max()` over the `AccumulateHourStats` dictionary** — §4a.1 |
| **D-5** | **(c)** | One new `HourClass` value |
| **D-5.1** | **(a)** | It sits **between `Defect` and `Captured`** in the combine at `:609-624`. **New order: `Defect > TrailingEdge > Captured > UnknownScope > ExpectedMissing > NotCapturing`.** ⚠ **Silent if wrong** |
| **D-5.2** | **(a)** | `--strict` stays keyed on `HourClass.Defect` alone. **Leave `BacktestProgram.vb:321` untouched** — and state in the spec-back that leaving it is the DECISION, not an oversight |
| **D-5.3** | **(a)** | The VERDICT line (`:1092-1098`) counts the new class. ⛔ **A report with trailing-edge hours and zero `Defect` hours must NOT print `clean`.** ⚠ **Silent if wrong** |
| **D-5.4** | **(a)** | A seventh console count line beside the six at `:1009-1014` |
| **D-5.5** | **(a)** | The value is named **`TrailingEdge`** |
| **D-6** | ⭐ **(c)** — *supersedes §4's (a)* | `ObservedLongestGapMs` and `GapBreachHours` are **UNTOUCHED**. Add `TrailingEdgeHours` and `ObservedLongestTrailingMs` as their own pair, rendered beside them. ⚠ **Do not "tidy" the weekend-hour contribution to the existing two while working next to them** — §4a.3 |

⭐ **Verified 2026-08-25, so D-5's mid-enum insertion is safe:** every `HourClass` reference across the tree is **by name**. There are **no ordinal casts, no `CInt`, no serialisation by number** — `CountByClass` compares values (`:135-139`), `BuildMarkdown` and the split-detail Reason use `ToString()`. **Inserting `TrailingEdge` between `Defect` and `Captured` shifts the ordinals of four members and breaks nothing.** ⚠ **Re-run that check if the enum is later persisted anywhere.**

⚠ **One consequence of `D-5.1` worth stating, because it is free and desirable:** a span classified `TrailingEdge` inside a split hour renders as `TrailingEdge` in the existing split-detail Reason string (`:634-640`), with no new formatting code.

---

## 5. The four traps, with the code

**5.1 — the boundary that isn't.** `ResolveBoundaryUtc:665`: `If evidence Is Nothing OrElse evidence.Count = 0 Then Return toUtc`. **No evidence ⇒ no protection.** §3.

**5.2 — `A49u` forecloses widening.** `Program.vb:8445` asserts `span0.LongestGapMs = 0`. Any fix that widens `LongestGapMs` to carry the trailing edge changes that value and breaks the fixture. **The fixture decides D-2.**

**5.3 — the default-value poison.** Four sites build `HourStoreStats` with initialisers and get the declaration default for anything new: `Program.vb:7757`, `:7791`, `:8094`, `:8311`. **A `LastTsMs = 0` makes all four read as a 1970 last-trade.** ✅ **CLOSED BY `D-3` (c)** — a `Long?` defaults to `Nothing`, so all four are protected by the type rather than by a convention the reader has to know. ⚠ **The trap is only closed while the field stays nullable; a later "simplification" to a plain `Long` re-opens it silently.**

**5.4 — the invisible reason.** `BuildMarkdown:1127` — `If h.Classification = HourClass.Captured OrElse … Then Continue For`. A Captured-with-reason hour renders **nowhere**; the console prints class counts only (`:1009-1014`). ✅ **CLOSED BY `D-5` (c) + `D-5.3` + `D-5.4`** — the new class falls through `BuildMarkdown`'s skip list and renders for free, and the two count surfaces that do NOT come free are now explicit build items. ⛔ **This trap was the argument against option (b); it is not an argument against (c), and §4's D-5 row failed to say so.**

---

## 6. Fixture blast radius — the six that break and the three at risk

⛔ **Every row below is an EXISTING fixture. The implementer will be revising tests they did not write, to accommodate their own change. Name each revision and why, in the spec-back.**

| Fixture | Why it moves | Must assert afterwards |
|---|---|---|
| **`A49e`** | ⛔ **Most exposed.** One-hour window, trades 05:01/05:06, ~54 min trailing, **no boundary passed at all** | ✅ **Under `D-4` (c) it must STILL assert `Captured`** — the store-end bound is what carries it, since the evidence bound alone does not. **If it cannot be kept at `Captured` without contorting the fixture, the store-end bound is built wrong** |
| **`A49g`** | Hour 05 has ~57 min trailing and the store ends; `walkToUtc = 06:00` gives no protection | ✅ Same — `D-4` (c)'s second witness. **Still `Captured`** |
| **`A49b`, `A49c`, `A49l`, `A49r`** | Hand-built `cleanStats` inherit the new field's default (trap 3) | ✅ **Unchanged classification, and under `D-3` (c) they need NO EDIT AT ALL** — `Long?` defaults to `Nothing`, the guard skips, the hours stay as they are. **Re-run them; do not touch them** |
| **`A49r`** *(also)* | ⚠ **Doubly exposed** — additionally asserts `String.IsNullOrEmpty(hr.Reason)` | ✅ **Survives untouched under the ruling.** Its hour is `Captured` with a hand-built `LastTsMs = Nothing`, so no trailing reason is ever composed. ⚠ **`D-5` (c) puts the signal in the CLASS, not the Reason — so nothing here pushes back on the empty-Reason assertion** |
| **`A49o`, `A49t`** | 🟡 Per-span rule: ON spans with ~27 min and ~10 min trailing edges | ⚠ **Now resolvable: under `D-1` (a) the rule DOES apply per span, and under `D-5.1` a `TrailingEdge` span outranks `Captured`.** Work out each span's `observedEndMs` before assuming the classification moves — **a span whose trailing edge is bounded by store-end does not flag.** State the arithmetic per span in the spec-back |
| **`A49s`, `A49u`** | 🟡 Assert exact `HourStoreStats` contents | `A49u` is D-2's decider |
| **`A49u`** *(also — added by §4a)* | ⚠ **A knife-edge witness for D-4's measurement convention.** Span 0's trade sits at :25 and the span ends :29:59.999, so its trailing edge is **299,999 ms against a 300,000 ms threshold — a 1 ms margin** | **Its assertions do NOT break** (the hour is already `Defect` via span 1, and worst-of absorbs a span-0 flag). ⛔ **Named so nobody "fixes" the fixture:** measuring the trailing edge off the span's EXCLUSIVE end instead of its inclusive one moves this to exactly 300,000 ms, and `<=` still holds — but any move to `<` flips it. **Keep the `<=` the gap rule already uses (`:505`)** |
| **`A49e`** *(mechanism note — added by §4a)* | ⛔ **`ClassifyHour` is called DIRECTLY here**, not through `BuildResult` (`Program.vb:7874-7875`) | **This is what forces D-4's bound to be a PARAMETER, not something `BuildResult` resolves internally.** The revision is clean and preserves the fixture's real intent — it tests the gap threshold's `<=` boundary, nothing about trailing edges — by passing an `observedEndMs` equal to the hour's last trade |
| **`A49v`** | 🟡 Asserts no classification, so it survives — **but hour 00 is the exact F1 shape** (one trade at 00:05, then silence all day) | ⭐ **Extend this one as the F1 regression trap** — it is the only fixture that already reproduces the defect end-to-end through `BuildResult` |

**New fixtures required — specified by their required FAILING input, not their intent:**

| New | Required input |
|---|---|
| **F1-a** | Trades early in an hour, silence to hour end, **and a later trade well into the next hour** — the canonical shape. Must flag under D-5's ruling |
| **F1-b** | ⛔ **The bound.** The same shape but where the hour IS the last hour with data. Must **NOT** flag |
| **F1-c** | The partial-final-hour case (boundary mid-hour, D-4(c)) — must not measure to `:59:59.999`. ✅ **D-4(c) is RULED, so this fixture pins the BEHAVIOUR, not a deferral** |
| **F1-d** *(added by §4a — D-5.1)* | ⛔ **A SPLIT hour, one span clean and one span trailing-edge, where the trailing span is NOT `Defect`.** Must report the new class, **never `Captured`**. This is the only fixture that catches the silent precedence error, and the naive combine order passes every other test in this table |
| **F1-e** *(added by §4a — D-4's superset trap)* | ⛔ **A store whose month file carries trades PAST `walkToUtc`**, with the walk's last hour ending in silence. Must still exempt that hour — **a `storeEndMs` derived by `Max()` over the `AccumulateHourStats` dictionary fails this and passes `F1-b`** |
| **F1-f** *(added by §4a — D-5.3)* | ⚠ **A report with trailing-edge hours and ZERO `Defect` hours.** The VERDICT line must **not** print `clean` |

⛔ **Mutation-proof `F1-a`, `F1-b`, `F1-d` and `F1-e`**, per standing convention: revert the guard, confirm each FAILS, restore, re-run. **A fixture that passes on the unfixed code proves nothing.** ⚠ **`F1-d` and `F1-e` need their OWN mutations, not the guard revert** — `F1-d` mutates the combine order to put the new class below `Captured`, `F1-e` mutates `storeEndMs` to the dictionary-wide `Max()`. **Both mutations leave `F1-a`/`F1-b`/`F1-c` green, which is the whole reason they are listed separately.**

---

## 7. Acceptance

- Six projects build **0/0 Release**, each separately.
- Harness **ALL PASS**, `A1`–`A59e` unregressed except the revisions §6 names, plus the new F1 fixtures. ⚠ **Assert the new fixtures EXECUTED** — a harness that runs none of them still prints ALL PASS.
- `verify-gate.ps1` — `tools/` is not an engine path; expect no version-bump nudge.
- ⛔ **Delete the placeholder note at `CoverageReport.vb:1100-1110`** — it documents the defect being fixed. **Leaving it is the fix half-done.**
- ⚠ **Run the real `coverage` verb against the AWS store copy and diff the class counts against the last run.** ✅ **D-5(c) is ruled, so `Defect` should be UNCHANGED and the new class should absorb every trailing edge** — the spec-back must state the new class's count, confirm each is a genuine trailing edge rather than the bound misfiring, and ⛔ **explicitly report `Defect` as unmoved. A `Defect` count that moves under D-5(c) means the new class is being reached through the wrong branch.**
- ⛔ **Assert `--strict`'s exit code is UNCHANGED on that same store copy** (D-5.2). Run it both ways. **The exit code is the contract a scheduled job depends on, and it is the thing D-5(c) was chosen to protect** — a claim that it did not move is worth nothing unless the run happened.
- ⚠ **Confirm the new class appears on ALL THREE surfaces** — the console count line, the markdown table, and the VERDICT line (D-5.3/D-5.4). **`BuildMarkdown` renders it for free; the other two do not.** Paste the rendered output into the spec-back rather than asserting it.

---

## 8. Out of scope

- **The `gapMs` TIME-tolerance question** — its own queue row (*"a TIME tolerance standing in for a COMPLETENESS check, the DR-1 pattern, third instance"*). ⚠ **Related and NOT to be bundled:** that one argues `trade_seq` makes the time tolerance redundant where sequences exist. **This spec keeps `gapMs` as-is.**
- **`UpInterval.IsTrailing`** (`:90`, set `:303`) — exists, currently read nowhere in classification. **Available to a fix; do not repurpose it without saying so.**
- **The leading edge** (`ResolveCaptureBeginsUtc`, `:894-907`) — a separate, already-handled bound.
- ⚠ **`AccumulateHourStats` applies no per-row `[fromUtc, toUtc)` filter** — `EnumerateMonths` reads whole month files, so the returned dictionary is a **superset** of the walked hours. Harmless today because `BuildResult` iterates its own cursor. **Named because a trailing-edge rule that iterates the dictionary instead of the cursor would inherit it.**
