# C1 Session 1 — orchestrator review, 2026-08-04

**Verdict: ✅ ACCEPTED. Session 2 may proceed.** Two findings, **neither blocking**, both recorded below with my read.
**Reviewed:** `dc2c0bd` (build) + `67ccb38` (spec-back), local, unpushed.
**Method:** every load-bearing claim re-verified **in the tree**, not read from the spec-back — the brief's §0 said the fixtures cannot self-catch a misunderstanding the implementer also wrote the fixture under, so the two traps were checked against the shipped code directly.

---

## 1. Independently verified

| Claim | How I checked it | Result |
|---|---|---|
| **Trap 1 — `DOWN` proves the app was ALIVE** | Read `ParseWsHealthEvidence` (`CoverageReport.vb:143`) | ✅ Splits on `" | "`, reads `parts(0)` (utc) + `parts(2)` (iid). **`parts(1)`, the state token, is never referenced anywhere.** `OK`/`DOWN`/`DEGRADED`/`REST` contribute identically. Correct by construction, not by convention |
| **Trap 2 — overlap, not containment** | Read `ClassifyUptime:242` | ✅ `hourEndMs >= iv.FirstUtcMs AndAlso hourStartMs <= iv.LastUtcMs` — a true interval-overlap test. The narrower containment check described in §2.6 is genuinely gone |
| **Six classes, none collapsed** | Read the `HourClass` enum + `ClassifyHour` | ✅ All six declared; `ClassifyHour` returns each from its own branch. No class is reachable only via another |
| **Weekday scope applied first** | `ClassifyHour:346` | ✅ Unconditional, before scope/uptime/store. Matches the weekday ruling exactly |
| **Scope from positive record only** | `ResolveScope` signature + a search for `SettingsLoader` in `CoverageReport.vb` | ✅ Takes only `List(Of MarkerRecord)`. **Nothing in the classification path reads config at all** — D7=(b) is not merely unused, it is unreachable |
| **Marker reads the MERGED value** | Read the `MainForm_Layout.vb` diff | ✅ `SettingsLoader.Current.TradeStore` + `TradeStoreWriter.ResolveStoreDir`. Double-wrapped never-throws (inner `Try` in `LogStart`, outer at the call site). Placed beside `WsHealthLog.LogStart` |
| **Build + harness + gate** | Independent Release rebuild of `OrderCheck` and a fresh run | ✅ 0 warnings / 0 errors; **0 FAIL, ALL PASS**; **A49a–l all twelve present** |
| **§2.11 smoke-run figures** | Cross-checked against my own measurement earlier this session | ✅ Their run reports capture beginning **2026-07-29 10:43 UTC**; I independently measured the store span as 2026-07-29 10:43 → 07-30 16:54 on 2026-08-03. **Two unrelated paths, same answer** |

## 2. The three judgment calls — I agree with all three

- **§2.1 `expected-missing` = before-first-process-life only.** Logically tight, and I checked the enumeration rather than the argument: once any process life exists, a no-evidence hour is structurally either *trailing* or *cross-GUID*, and **J-B assigns both to `defect` explicitly**. That leaves before-first as the only unambiguous case. The narrow reading is the correct one.
- **§2.2 S1 skipped only when BOTH sources are empty.** The implementer read A49g's literal text against the proposal's own C2 revision — which made `analysis_log.csv` primary *specifically* so a missing `ws_health.log` cannot blind S1 — and built the compound condition. Building A49g literally would have contradicted the revision this same build exists to honour. **Good catch; this is the spec being internally inconsistent, not the implementer deviating.**
- **§2.3 store-clean wins as `Captured`.** Not an override of J-B, and worth stating precisely: **J-B's rule is about resolving *ambiguity*, and clean in-hour store data means there is no ambiguity left to resolve.** The premise doesn't obtain. Sound — subject to F1 below, which is about what "clean" measures, not about the precedence.

**§2.9's WARN handling is also right.** Declining to add `[no-engine-change]` because `Core/` genuinely gained a file — while correctly taking no version bump, since no settings keys changed — is the honest signal. Agreed.

---

## 3. Findings

### F1 — `Captured` does not mean "fully covered" (low-medium, not blocking)

`AccumulateHourStats` carries `prevTs` globally across hours **and** months, so a gap is attributed to the hour containing the trade that **ENDS** it. Leading-edge gaps are therefore caught correctly — an hour whose first trade arrives at :59 inherits the whole preceding silence.

**The trailing edge is not measured.** An hour with trades from :00–:05 and then silence until the next hour's :30 charges that 85-minute gap to the *following* hour, and **reads `Captured` itself** despite 55 minutes of no tape.

**The incident is not lost** — the following hour flags `Defect`, and if the app never returns, later hours have `RowCount = 0` and flag too. So this is **mis-attribution by one hour, not blindness.** But a reader will reasonably take `Captured` to mean complete, and it does not.

**My read: do not fix it blind.** The obvious fix — treat `hourEnd − lastTradeInHour` as a candidate gap — would false-positive the final hour of every window, since data always ends somewhere; it needs bounding against the same trailing-evidence boundary `ResolveBoundaryUtc` already computes. **Cheapest correct action now: state it in the report's own legend** — "`captured` = rows present and no gap *ending* in this hour breached the threshold". Session 2 can decide whether the bounded fix is worth it.

### F2 — a capture-state transition mid-hour is scoped by the PREVIOUS marker (low, not blocking)

`ResolveScope` selects the marker with the greatest `UtcMs ≤ hourStartMs`. A process that starts at 10:30 with capture ON does not govern hour 10:00 — the previous process's marker does. So the hour containing a capture-state flip is classified by the pre-flip state.

Only reachable at a deploy or a capture-toggle, so rare. But note the direction: it errs toward **not** flagging, which is the opposite of J-B's stated preference that a false defect is the cheaper error. ~~**Flagging only** — a fix wants a rule for split hours, which is a spec question, not an implementation one.~~

> ## ✅ RULED 2026-08-12 (trader) — SPLIT THE HOUR. This is now a build slot.
>
> **The rule:** an hour containing a capture-state marker is **split at the marker**; each part is classified against the state that governed it; **the hour is reported defective if EITHER part is defective.**
>
> ⚠ **The two options NOT taken, recorded so they are not re-proposed:**
> - **(a) scope by the LATER marker** — one line, and it does move the error to J-B's preferred side. **Rejected:** it manufactures a **false defect on every deploy**, and a check that cries wolf at each deploy stops being read. That is the alarm-fatigue failure this project has already flagged once, on the F3 observational watch.
> - **(c) exclude split hours from the denominator** — ⛔ **rejected outright.** A silent hole in a coverage report is the exact defect class this store keeps producing, and the report exists to make gaps visible.
>
> ⚠ **The implementer must state what the classification UNIT becomes.** The report classifies whole hours today; splitting introduces sub-hour spans. Say plainly whether the output stays one row per hour with a worst-of verdict, or becomes one row per span.
>
> ⚠ **Where it slips:** `ResolveScope` has callers for which no split is possible. **Do not change its contract for all of them** — add the split at the classification site.
>
> **Fixtures must cover:** a flip at **:00** exactly · a flip at **:59** · an hour carrying **two** markers.
>
> **Model: Sonnet, effort: medium.** Tracked in [`trader-tick-queue.md`](trader-tick-queue.md) §2; the decision text of record is that doc's §0a row.

---

## 4. For Session 2

Nothing blocks it. Carry forward:

1. **F1's legend line** at minimum; the bounded gap fix only if it proves cheap.
2. **A49m** as specced — and note the implementer's own point that the weekend classification is already live code, so A49m pins it rather than introducing it.
3. **Part B must stay unconditional on weekends** — the one place the weekday ruling deliberately does not apply.
4. The **version bump** lands with Session 2 if and only if it adds settings keys. If Part B derives amber/red from the existing `flush_seconds`, there is still no bump — only a §15 entry. The same judgment §2.9 already got right.

**Model + effort for Session 2 stands as briefed: Sonnet 5, medium.** Nothing in this session's outcome argues for raising it — the build came in clean, the two traps were handled correctly, and the one real bug was caught by the implementer's own fixture.
