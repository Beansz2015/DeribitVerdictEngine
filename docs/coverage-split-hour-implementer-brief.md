# SH-1 — split the coverage hour at a capture-state marker

**Ruling of record:** [`trader-tick-queue.md`](trader-tick-queue.md) §0a, ruled by the trader 2026-08-12. Origin: [`c1-session1-review-2026-08-04.md`](c1-session1-review-2026-08-04.md) finding F2.
**Status:** ✅ **RULED AND READY TO BUILD.** Two small decisions in §5 carry my read; neither blocks the session.

---

## 0. ⚠ The ID, first — because this is the exact name that has bitten this project

**This item is `SH-1` (split hour). It was called "C1-coverage F2" and that name is unusable:**

| `F2` also means | Where |
|---|---|
| The `ResetBufferState` race | `DeribitWsFeed.vb:298` — a live, open, unrelated defect |
| An audit-fixes finding | The 2026-07-02 audit set, alongside `F1` and `F3` |
| An ordinary finding index | Every spec-back in `docs/` |

⚠ **`F1` and `F3` collide the same way** — `F3` names two different watches, only one of which was retired. **Use `SH-1`. Always name the document when citing anything else.**

---

## 1. Implementer brief — model, effort, and where it slips

> **Model: Sonnet. Effort: medium. One session.**
>
> **Why that tier.** The judgment is done and recorded below — including the one thing the ruling did **not** name (§3), which I found by reading the code and have already routed. What remains is a contained build in one file against a settled design, with an existing fixture family to extend.
>
> ⚠ **Where it slips — three, and the first is the whole task.**
>
> 1. ⚠⚠ **`HourStoreStats` cannot answer a sub-hour question, and nothing in the ruling says so.** It carries exactly `RowCount` and `LongestGapMs`, accumulated per whole UTC hour by `AccumulateHourStats` bucketing on `(t.Timestamp \ HourMs) * HourMs`. **So "was the ON part of this hour clean?" is unanswerable from the data `ClassifyHour` is handed today.** Solving this is the work; splitting the hour is the easy half. §4 routes it.
> 2. ⚠ **`LongestGapMs` is carried ACROSS hours and months** — `prevTs` persists through the whole accumulation, deliberately, so a gap spanning a boundary is still measured. **Any sub-span stat must keep that carry discipline**, or a gap that starts before the marker is silently lost.
> 3. ⚠ **Do NOT scale the gap threshold to the sub-span's length.** `gapMs` (300,000 default) is an absolute "how long may the tape go quiet" bound, not a proportional one. A 10-minute ON window with a 6-minute gap is a defect on the same terms as a full hour with one.
>
> **Escalation triggers — stop and come back.**
>
> - ⚠ **If the sub-span stats cannot be obtained without restructuring `AccumulateHourStats`**, stop. That function streams a multi-month store month-by-month specifically to avoid materialising it, and changing it is a different risk class from this item. §4's route (b) exists to keep you out of it.
> - ⚠ **If the new fixture cannot be made to FAIL on current code before the fix lands, stop** — the test is not testing what it claims. Four builds in this workstream have held that line.
> - **If splitting forces `HourResult` to stop being one-row-per-hour**, stop and re-put §5's D-1 — that ripples into `CountByClass`, the markdown table and every consumer.

---

## 2. The ruling

> **An hour containing a capture-state marker is split AT the marker. Each part is classified against the state that governed it. The hour is reported DEFECTIVE if EITHER part is defective.**

⚠ **Two options were considered and REJECTED. Do not re-propose them:**

- **Scope by the LATER marker.** One line, and it does move the error to the preferred side. **Rejected:** it manufactures a **false defect on every deploy**, and a check that cries wolf at each deploy stops being read — the alarm-fatigue failure this project has already recorded once.
- **Exclude split hours from the denominator.** ⛔ **Rejected outright.** A silent hole in a coverage report is the exact defect class this store keeps producing, and the report exists to make gaps visible.

---

## 3. The defect, precisely

**File:** [`tools/BacktestRunner/CoverageReport.vb`](tools/BacktestRunner/CoverageReport.vb).

`ResolveScope(hourStartMs, markers)` picks the marker with the **greatest `UtcMs` ≤ `hourStartMs`** — its own summary explains why: *"a process reads its settings once at start, so that reading scopes everything until the NEXT recorded process start."* Sound for whole hours; wrong for the hour the flip lands in.

`ClassifyHour` then acts on it immediately:

```vb
Dim scope = ResolveScope(hourStartMs, markers)
If scope.Kind = "unknown" Then  … Return   ' UnknownScope
If scope.Kind = "off"     Then  … Return   ' NotCapturing
```

⚠ **`NotCapturing` returns before the store is ever consulted.** So a process that starts at 10:30 with capture ON leaves hour 10:00 governed by the previous process's `off` marker: the hour reads `NotCapturing`, **the 30 minutes of real capture are never examined, and a defect inside them cannot be reported.**

**Rarity:** only reachable at a deploy or a capture toggle. **Direction:** it errs toward **not** flagging — the opposite of the stated preference that a false defect is the cheaper error. That direction is why it needed a ruling rather than a patch.

⚠ **A marker landing exactly on `hourStartMs` is already handled** (`<=`), so only markers **strictly inside** the hour split it.

---

## 4. What to change

### 4.1 The split

Split the hour at every marker with `hourStartMs < UtcMs <= hourEndMs`. Each sub-span carries the scope in force during it: the pre-flip scope for the first, then each marker's own `Enabled` for the rest. **An hour can legitimately carry more than one marker** — a restart loop produces several — so handle N, not just one.

### 4.2 The sub-span stats — ⚠ this is the part the ruling did not name

`HourStoreStats` is `RowCount` + `LongestGapMs` **per whole hour**. Two routes:

| | Route | Verdict |
|---|---|---|
| **(a)** | Extend `AccumulateHourStats` to bucket on arbitrary boundaries | ⛔ **No.** It streams a multi-month store to stay bounded; making it marker-aware couples the hot path to the rare case |
| **(b)** | **A second, targeted pass over the store for split hours only** | ✅ **Recommended.** Split hours are rare *by construction* — deploys and toggles. The main accumulation path is untouched, memory is unchanged, and the blast radius is the new function |

**Take (b).** ⚠ **Carry the `prevTs` discipline into it** (slip 2): seed the sub-span's gap measurement from the last trade *before* the span, not from the span's first row.

### 4.3 Combining

- **Any part `Defect` ⇒ the hour is `Defect`.** That is the ruling, verbatim.
- Put the split detail in **`HourResult.Reason`**, which is already documented as *"Never consumed for classification, display/markdown only"* — exactly the right home.

### 4.4 Out of scope

- **No settings keys, no version bump.** ⚠ `tools/BacktestRunner/` is **not** an engine path (`verify-gate.ps1` scopes those to `Core/`, `DynamicNorms.vb`, `analysis/`), so the version-bump check reports *"no engine-path change"* regardless. Carry `[no-engine-change]` for consistency with this workstream.
- **No rendered engine surface.** The live TAPE STORE strip is Part B and is independent of hour classification. ⚠ **State that in the commit message** rather than leaving it unsaid — the display-string parity rule requires the statement, not just the absence.
- **Do not touch `ResolveScope`'s contract for other callers.** Add the split at the classification site.

---

## 5. Two small decisions — my read, not blocking

| # | Question | My read |
|---|---|---|
| **D-1** | Does the output stay **one row per hour**? | ✅ **Yes.** `CountByClass` aggregates over `Hours`, and the markdown table is per-hour; changing the unit ripples into every consumer for no gain. **Worst-of verdict, split detail in `Reason`.** ⚠ If the build finds this impossible, that is an escalation trigger, not a judgment call |
| **D-2** | When no part is `Defect` but the parts disagree — e.g. `NotCapturing` + `Captured` | ✅ **`Captured`.** It matches the existing design note that *"positive store evidence (clean rows) always wins as Captured, regardless of how ambiguous the uptime read is"*, and it keeps `NotCapturing` meaning "we were not capturing", not "we partly were". Record the split in `Reason` |

**Both are recorded so they are not invented mid-build. If you disagree with either, say so in the spec-back rather than silently choosing.**

---

## 5a. ⚠ D-3 — RULED 2026-08-13, after the build. The residual combine order.

**The question the brief did not name, correctly raised by the implementer:** when **no** span is `Defect` and **none** is `Captured`, and the survivors disagree — which class wins?

**As built:** `ExpectedMissing` > `NotCapturing` > `UnknownScope`.

> ## ✅ THE RULING: `Defect` > `Captured` > **`UnknownScope`** > `ExpectedMissing` > `NotCapturing`.
>
> **`ExpectedMissing` > `NotCapturing` is CORRECT as built — keep it.** `NotCapturing` asserts *"we deliberately did not capture"*; over a span where that was not the recorded state, that assertion is false. `ExpectedMissing` makes the weaker claim and is the honest one.
>
> ⚠⚠ **`UnknownScope` is in the wrong place. It must move to the TOP of the residual, not the bottom.**
>
> **Three reasons, ranked:**
>
> 1. ⚠ **Bottom-placing it launders an uncharacterisable span into a confident label — which is the SH-1 defect in miniature.** `UnknownScope` means *no marker applies at all*: the report cannot say what the box was doing. Reporting `NotCapturing` for that hour tells a reader "capture was off, nothing to see" about a span where we have **no** evidence either way. **SH-1 exists because a confident label hid an unexamined span. Do not reintroduce it in the combine.**
> 2. ✅ **It contradicts the function's own existing precedence.** Shipped `ClassifyHour` — and `ClassifySpan`, which this build wrote — check `unknown` **before** `off` and return early. **So `UnknownScope` already outranks `NotCapturing` on the single-scope path.** The combine as built silently reverses that for split hours. One function, two orders, is exactly the inconsistency this item exists to remove.
> 3. **It matches the stated preference that a false defect is the cheaper error.** Erring toward *"look at this"* beats erring toward *"nothing here"*.
>
> **`Captured` stays above `UnknownScope`** — positive store evidence on disk outranks an absent marker, per the existing design note and D-2. Unchanged.

⚠ **A fixture IS owed, and the "too contrived to fixture" judgment does not hold.** The discriminating case is a **first-ever marker landing mid-hour**, which:

- **has already happened in production once** — [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5a records that `capture_marker.log` **begins** on AWS at `ec487909…`, 2026-08-07 16:02:41, so the 16:00 hour is genuinely `unknown` + *(marker state)*; and
- **discriminates whenever that first marker records `off`** — which is what **any box brought up with the capture overlay in place** produces, and the local `bin\Release` box is exactly that.

**Not contrived. One fixture, `A49w`.**

---

## 6. Fixtures

**Extend `A49` where an existing fixture already builds the right scaffolding** — that is what worked for DR-3, which extended `A48d` rather than standing up a second temp store. **Otherwise the next free family is `A57`** (`A56g` is high-water; re-check with `Select-String verify/ordercheck/Program.vb -Pattern '\bA[0-9]{2}[a-z]_'`).

Required cases:

| Case | Asserts | Must fail on current code? |
|---|---|---|
| Flip **ON at :30**, clean store in the ON half | Hour is **not** `NotCapturing` | ⚠ **YES — this is the mutation proof** |
| Flip **ON at :30**, **silent** store in the ON half | Hour is `Defect` | ⚠ **YES** |
| Flip **OFF at :30**, defect in the ON half | Hour is `Defect` — the defect is not laundered by the later `off` | ⚠ likely |
| Marker at **:00 exactly** | Unchanged from today — `ResolveScope` already owns it | No |
| Marker at **:59** | Split still happens; the 1-minute part is judged on the absolute `gapMs`, **not** a scaled one | ⚠ likely |
| **Two markers** in one hour | Three parts, all classified | ⚠ likely |
| Gap **straddling** the marker | Attributed correctly — slip 2 | ⚠ likely |

⚠ **Fixture-literal provenance (hard rule):** these assert **MECHANISM** — constructed markers and timestamps — so literals are correct and the family header must say so. Anything read from a production constant (`gapMs`, thresholds) must be **read**, never restated.

---

## 7. Verification handles

⚠ **Each asserts a property, not a string that mentions it.** **Four consecutive builds in this workstream shipped a handle that counted a NAME and was falsified by a comment** — `_lastTs` printed 2, `repairHoles` 3, `MinHoleMs` 5, `CountDataRows` 1. **Assert the declaration or the executable reference, never the bare name.**

| # | Handle | Expected |
|---|---|---|
| **1** | The ON-half fixture against **pre-change** code | ⚠ **FAILS.** If it passes, stop |
| **2** | `tools\checks\verify-gate.ps1 -Mode prepush` | GATE PASSED, six projects Release 0/0, harness **ALL PASS** |
| **3** | `git diff --stat -- settings.json` | **empty** |
| **4** | Read `AccumulateHourStats`'s body | **Unchanged** — route (b) means the hot path is untouched |
| **5** | Count `HourResult` rows for a day containing a split hour | **24** — one row per hour, D-1 preserved |

---

## 8. What I did not verify

- ⛔ **That route (b)'s targeted second pass is cheap in practice.** Reasoned from split hours being deploy-only; **not measured.**
- ⛔ **How many split hours exist in the real AWS store.** The ledger implies roughly one per deploy or toggle, which is single digits — **I did not count them.**
- ⛔ **Anything live.** No coverage run was performed against a real store for this brief.
- ⛔ **Whether `A49`'s existing fixtures can carry these cases.** I read their shape, not their internals — §6 says "extend where it fits" precisely because I have not proved it does.
