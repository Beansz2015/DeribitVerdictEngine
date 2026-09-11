# `C-3b` — venue-outage scoping, and the recovery marker it needs first

**Status:** ✅✅ **BUILD-AUTHORIZED. All decisions taken under the auto-proceed ruling, recorded in §4. Nothing is owed.**

**Author seat:** Opus, 2026-09-11 (UTC). **Baseline commit: `a5edaa4`.** ⛔ **Re-read every line number if `HEAD` has moved.**

**Origin:** trader directed *"build it anyway and close the case"* after the `VENUE_RPC` fix landed. **Writing it surfaced a live defect in the shipped instrument — see §1, which is why this is two parts and not one.**

⚠ **I was wrong twice and both corrections are load-bearing, so they are stated up front rather than buried in §7:**
1. **I said the consumer *"cannot be tested until real data exists."* That was too strong.** The consumer's logic is fully testable from synthetic log lines — the coverage report already parses synthetic `ws_health.log` lines in `A49`. **What needs real data is validating the real-world SHAPE, not the logic.**
2. ⛔ **The parent spec under-specified the transition model, and that is MY error, not the implementer's.** [`venue-status-instrument-spec.md`](venue-status-instrument-spec.md) §1 said *"Transition-only … Mirrors `WsHealthLog`"* and then gave a trigger table that only ever logs PROBLEMS. **`ws_health.log` logs `OK` transitions too. The instrument mirrored the shape and not the model, exactly as written.**

---

## 0. Model and effort

> ### Part A — the recovery marker: **Sonnet · LOW**
> ### Part B — the consumer: **Sonnet · MEDIUM**
> ### Two sessions, SEQUENTIAL, same implementer. **Part B is impossible without Part A.**

**Why Part A is LOW.** One `ShouldRecord`-adjacent call, one state reset, one fixture family. **The template is `WsHealthLog`, which already does exactly this.**

**Why Part B is MEDIUM.** The classifier arm itself is small and mirrors `OutOfScopeDeclared`. **What lifts it is the policy: which codes scope an hour out, and the fail-safe direction when a code is unrecognised.** §4 settles both; the implementer must not re-open them.

### 0.1 ⛔ Where the implementer will slip

| # | Trap | Why |
|---|---|---|
| **B-1** | ⛔⛔ **SCOPING OUT ON ANY VENUE LINE** | The log now records **our own bad requests** too (`VENUE_RPC_10009` and the like). ⛔ **If every venue line scopes an hour out, OUR defects excuse themselves — the `V-1` failure the whole instrument exists to prevent, arriving through the consumer instead of the writer.** §4 `D-3` settles it: **only venue-side codes scope out; everything else stays `Defect` with the code in the reason** |
| **B-2** | ⛔ **An unrecognised code must FAIL SAFE TOWARD FLAGGING** | [`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md) holds that a false defect is the cheaper error. **A code we do not recognise is NOT evidence of a venue outage — leave the hour as `Defect`.** ⛔ **Defaulting unknown codes to "scoped out" is the same defect as `B-1` with an extra step** |
| **B-3** | ⚠ **The venue-side code set is a CONSTANT, and it must be READABLE BY FIXTURES** | Per the standing ruling: *"a value ruled into a CONSTANT goes `Public Const`, not `Private Const`"* — **`Private` forces the fixture to restate the literal, which rots the first time the set moves.** ⛔ **Do NOT make it a `settings.json` key; that is a reserved class and `D-5` rejects it** |
| **B-4** | ⛔ **`ClassifySpan` must NOT emit the new class** | Both existing out-of-scope classes are emitted by `ClassifyHour` only — the code says so twice (`CoverageReport.vb:870`). **Follow that, and place the arm beside `OutOfScopeDeclared` at `:783-792`** |
| **B-5** | ⚠ **Part A changes a file Part B parses** | **Build and land Part A first, and write its fixtures first.** A consumer written against a log that has no recovery line will silently never close a window |
| **B-6** | ⛔ **The clock** | GMT+8 workstation, UTC project dates. **Run `date -u`.** Ten consecutive sessions |

### 0.2 ⛔ Escalation trigger

- **Any hour that moves to the new class on a code NOT in the venue-side set.** That is `B-1`/`B-2`.
- ⛔ **BIDIRECTIONAL, per the correction the coverage cluster forced: report ANY hour that changes class in EITHER direction, with a before/after count.** **The cheap way: run the report at the pre-change commit and at yours over the same window and diff the summary counts.**
- **Any need for a `settings.json` key.** Reserved class; stop.

---

## 1. ⛔⛔ PART A FIRST — a live defect in the shipped instrument

**`Core/VenueStatusLog.vb` has no production path that resets `_lastState`.** ✅ **Verified 2026-09-11: the only reset is `ResetForTest`, whose own doc comment reads *"Test-only — reset the in-process transition baseline"*, and `ShouldRecord` returns False for every successful response, so a success calls nothing.**

**Two consequences. The second is worse than the gap that motivated this spec.**

| # | Consequence | Severity |
|---|---|---|
| **1** | ⛔ **NO RECOVERY MARKER.** The log records a venue problem's START and never its END, so **an outage window cannot be derived** — and `OutOfScopeDeclared`, the pattern Part B mirrors, works on `[StartMs, EndMs)` windows | **Blocks `C-3b`** |
| **2** | ⛔⛔ **A REPEAT OUTAGE WITH THE SAME CODE IS SILENT FOR THE LIFE OF THE PROCESS.** Venue fails 09:00 with `11051` → logged. Recovers. Fails again 14:00 with `11051` → `_lastState` still equals `VENUE_RPC_11051`, so **NOTHING is written** | ⛔⛔ **LIVE DEFECT.** The collector's app uptime was **9 days** at the 2026-09-10 read — this suppresses every repeat outage for days |

⭐ **ONE change fixes both: write a `VENUE_OK` line on the first success after a venue-error state.** It gives the window its end **and** resets the baseline so a repeat outage logs again.

⭐⭐ **And it is what the parent spec meant to say: `ws_health.log` logs `OK` transitions, and "mirror `WsHealthLog`" should have carried the model, not just the line shape.**

### Part A build list

1. **`Core/VenueStatusLog.vb`** — a `RecordOk`-style entry that writes `VENUE_OK` **only when `_lastState` is currently a venue-error state**, then clears the baseline. ⛔ **It must NOT write on every success** — that would append a line per poll and the file would need rotation, which the design exists to avoid.
2. **`DeribitClient.RecordVenueIfNeeded`** — call it on the success path. ⚠ **`Return body` must remain the last line; the drop-in property is not negotiable.**
3. ⛔ **Shape C is unchanged: no response ⇒ still nothing, neither an error line NOR an OK line.** A timeout is not evidence the venue recovered. **This is still `V-1`.**
4. **Fixtures `A76a`–`A76c`** — ✅ **`A76` is the next free family, verified 2026-09-11: `A75b` is the high-water mark.**

---

## 2. Part B — the consumer

**A tenth `HourClass`: `OutOfScopeVenue`.** Emitted by `ClassifyHour` only, placed beside `OutOfScopeDeclared` at `CoverageReport.vb:783-792`, **after** the weekend and declared arms and **before** every uptime and store test.

**Its window comes from Part A's log:** a venue-error line opens a window, the next `VENUE_OK` (or the end of the read range) closes it.

⛔ **An hour only takes this class when the opening line's code is in the VENUE-SIDE set.** Everything else — our-fault codes, unrecognised codes — **leaves the hour to normal classification, with the code carried in the Reason string so it is visible rather than lost.**

### The nine current classes, for the precedence work

`Captured` · `Defect` · `TrailingEdge` · `ExpectedMissing` · `StartupWindow` · `NotCapturing` · `UnknownScope` · `OutOfScopeWeekend` · `OutOfScopeDeclared`

⚠ **`OutOfScopeVenue` is the tenth. Add it to `BuildConsoleSummary`'s counts** (`CoverageReport.vb:1322` is where `out-of-scope-weekend` prints) **and to the markdown writer, or the class exists and nobody sees it.**

---

## 3. Which codes are venue-side

| Signal | Venue-side? |
|---|---|
| **Any 5xx** — `VENUE_500`, `VENUE_502`, `VENUE_503`, `VENUE_504` | ✅ **YES.** A 5xx is the venue's own edge declaring it cannot serve |
| **`VENUE_RPC_11051`** (`system_maintenance`) | ✅ **YES** |
| **`VENUE_RPC_UNKNOWN`** | ⛔ **NO.** We could not read the code, so we do not know whose fault it was. `B-2` |
| **Any other `VENUE_RPC_<code>`** | ⛔ **NO by default.** Not recognised ⇒ not evidence of a venue outage |

⚠ **`11051` is CARRIED from the 2026-08-11 observation and is NOT verified against Deribit's documentation** — see §7. ⭐ **The design tolerates that: an unrecognised code fails safe toward `Defect`, so a wrong or incomplete set produces FALSE DEFECTS, never false scope-outs.** ⛔ **That asymmetry is deliberate and is J-B's cheaper error. Do not "improve" it by widening the default.**

---

## 4. ⭐ D-table — all taken

✅ **Reserved-class test:** no scoring impact · no rendered value (`CoverageReport` reaches no snapshot line or card binding — verified earlier this arc, `grep -c 'CoverageReport' UI/MainForm_Layout.vb` → 0) · no `settings.json` key · no CSV-header change. ⭐ **Every pick below is the MORE-truthful option, so the cheaper-and-less-truthful class does not fire.**

| # | Decision | Options | ✅ TAKEN, and why |
|---|---|---|---|
| **`D-1`** | Fix the recovery marker, or build Part B on the log as-is? | (a) build on it as-is · **(b) fix it first** | ✅ **(b).** ⛔ **(a) is impossible, not merely worse — a window with no end cannot be computed.** ⭐ **And the fix closes a LIVE defect worth more than this whole spec: repeat outages are currently silent for the life of a 9-day process** |
| **`D-2`** | Write `VENUE_OK` on every success, or only on recovery? | (a) every success · **(b) only when leaving a venue-error state** | ✅ **(b).** (a) writes a line per poll — thousands a day — and forces rotation onto a design whose whole point is staying tiny |
| **`D-3`** | Which venue lines scope an hour out? | (a) any venue line · **(b) venue-side codes only** · (c) none, annotate only | ✅✅ **(b), the load-bearing row.** ⛔ **(a) is `B-1` — our own bad requests would excuse our defects.** **(c) leaves the original problem unsolved: the report was noise because venue outages counted as defects.** ⭐ **(b) solves the noise AND keeps our-fault codes visible as defects with the code in the reason** |
| **`D-4`** | An unrecognised `VENUE_RPC_<code>` | (a) scope out · **(b) leave as `Defect`, carry the code in the reason** | ✅ **(b).** ⭐ **A code we cannot attribute is not evidence of a venue outage.** **Fails safe toward flagging — J-B's cheaper error — so an incomplete code set costs false defects, never false excuses** |
| **`D-5`** | Where does the venue-side code set live? | (a) `settings.json` · (b) `Private Const` · **(c) `Public`/`Friend` shared constant in code** | ✅ **(c).** ⛔ **(a) is a reserved class and this is not operational intent.** ⛔ **(b) forces fixtures to restate the literal — the exact rot the `Public Const` ruling forbids.** **(c) lets `A77` read the production set** |
| **`D-6`** | Class name | `OutOfScopeVenue` vs `VenueOutage` | ✅ **`OutOfScopeVenue`** — it reads as a sibling of `OutOfScopeWeekend` / `OutOfScopeDeclared`, which is what it is |

---

## 5. Fixtures

### Part A — family `A76`

| # | Asserts | ⛔ The mutation that must fail it |
|---|---|---|
| **`A76a`** | A venue error then a success writes **two** lines: the error, then `VENUE_OK` | Drop the success hook → one line |
| **`A76b`** | ⛔⛔ **THE LIVE DEFECT, ASSERTED.** error(`11051`) → success → error(`11051`) writes **THREE** lines, not one. **The repeat outage is visible** | Remove the baseline reset → the third line vanishes and the fixture fails. ⭐ **This is the regression that shipped** |
| **`A76c`** | ⛔ **`V-1` holds:** a timeout after a venue-error state writes **NOTHING** — no error line and **no `VENUE_OK`** | Treat a timeout as recovery → an OK line appears, fixture fails |

### Part B — family `A77`

| # | Asserts | ⛔ The mutation that must fail it |
|---|---|---|
| **`A77a`** | An hour inside a `VENUE_503` → `VENUE_OK` window classifies **`OutOfScopeVenue`** | Remove the arm → reverts to `Defect` |
| **`A77b`** | ⛔⛔ **THE ONE WITH TEETH (`B-1`).** An hour inside a **`VENUE_RPC_10009`** window stays **`Defect`**, with the code in the reason | Scope out on any venue line → the hour wrongly reads `OutOfScopeVenue`. ⭐ **This is our-defect-excusing-itself, asserted** |
| **`A77c`** | `VENUE_RPC_UNKNOWN` stays **`Defect`** (`D-4`) | Default unknown to scoped-out → fails |
| **`A77d`** | An unterminated window (error line, no `VENUE_OK`) closes at the **end of the read range** and does not scope out hours beyond it | Treat it as open-ended → later hours wrongly scope out |
| **`A77e`** | The new class appears in the console summary counts | Omit the counter → the class is invisible |

⛔ **MUTATION-PROVE EVERY FIXTURE AND PASTE THE ACTUAL OUTPUT.** ⚠ **A fixture that throws instead of failing aborts the harness and hides every later fixture — guard any file read.** ⚠ **Code literals in fixtures are MECHANISM, not shipped behaviour — label them.**

---

## 6. Acceptance

| # | Check | Expected |
|---|---|---|
| `AC-1` | Harness after Part A | **368 → 371, ALL PASS** |
| `AC-2` | Harness after Part B | **371 → 376, ALL PASS** |
| `AC-3` | Solution Release `-t:Rebuild` | **0 errors, 0 warnings.** ⛔ **The SOLUTION, not just `OrderCheck` — the last build skipped this and I had to run it** |
| `AC-4` | `verify-gate.ps1 -Mode local-fast` | **GATE PASSED** |
| `AC-5` | `settings.json` | ⛔ **untouched at v68** |
| `AC-6` | A coverage run over a window with **no** venue lines | ⭐ **byte-identical report to before.** **This is the over-reach catch** |
| `AC-7` | Display parity | **does not fire** — confirm from the gate's own *"no snapshot/card drift detected"* |
| `AC-8` | `ClassifySpan` still emits no out-of-scope class | **`B-4` holds** |

**Part A: a `DeribitIndicatorProject.md` §15 entry IS owed** — `DeribitClient.vb` is an engine path and this changes what it writes. **APPEND to the existing venue-status row** per §15's one-item-one-row rule. **Part B: `tools/BacktestRunner` is not an engine path** — no §15 entry; say so in the commit. **No `settings.json` bump either part.**

---

## 7. What I did NOT verify

- ⛔ **`11051` is NOT verified against Deribit's documentation.** It is carried from the 2026-08-11 observation. ⭐ **The design is built to tolerate that — unknown codes fail safe toward `Defect` — but the SET is a guess and should be widened only on observed evidence, never speculatively.**
- ⚠ **I did not confirm a venue outage produces a 5xx rather than a 200-with-error.** Both are handled, so the spec does not depend on it; **the code set's coverage does.**
- ⚠ **I did not read `BuildMarkdown` or `BuildConsoleSummary` in full** — only located the weekend counter at `CoverageReport.vb:1322`. **`A77e` exists because I could not confirm by reading how many surfaces need the new counter.**
- ⚠ **The harness deltas in `AC-1`/`AC-2` assume three and five new fixtures respectively and no others.**
- ⚠ **I did not re-run the harness this session.** **368 is from the `a5edaa4` review.**
