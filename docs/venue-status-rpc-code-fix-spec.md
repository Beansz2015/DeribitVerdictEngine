# `VENUE_RPC` — record WHICH error the venue declared, not just that it did

**Status:** ✅✅ **BUILD-AUTHORIZED. Every decision is taken under the auto-proceed ruling and recorded in §3. Nothing is owed.**

**Author seat:** Opus, 2026-09-11 (UTC). **Baseline commit: `67b139c`.** ⛔ **Re-read every line number if `HEAD` has moved.**

**Origin:** review of the venue-status instrument (`88538d7`, merged `67b139c`). **The build was accepted — this is a residual the review named, not a defect in what shipped.**

---

## 0. Model and effort

> ### Model: **Sonnet** · Effort: **LOW**
> ### One session.

**Why LOW.** **One policy function, one state string, one doc comment, one fixture updated and two added.** No new machinery — the instrument, its log writer, its fetch-list entry and its `Friend` seam all exist and all work.

⭐ **Why it is worth doing NOW rather than later, and this is the whole argument: the log was created hours ago, the consumer does not exist yet, and NO DATA HAS ACCUMULATED.** ⛔ **Every day of delay writes more `VENUE_200` lines whose error code is lost permanently and cannot be retrofitted.** **This project's own memory names that shape: *"defer a schema fix and it accretes"* — and the five parked riders are what losing that bet looks like.**

### 0.1 ⛔ Where the implementer will slip

| # | Trap | Why |
|---|---|---|
| **R-1** | ⛔⛔ **`A74e` ASSERTS THE OLD STRING AND WILL FAIL** | `verify/ordercheck/Program.vb:13624` reads `stateOk = parts.Length = 3 AndAlso parts(1) = "VENUE_200"`. **It MUST be updated in the same commit.** ⚠ **`A74a` asserts `VENUE_503` at `:13459` and must NOT change — shape A keeps `VENUE_<http-code>`** |
| **R-2** | ⛔ **The format lives in a doc comment too** | `Core/VenueStatusLog.vb:15` states *"where state is `VENUE_<http-status-code>`, e.g. `VENUE_503` or `VENUE_200`"*. **A format change that leaves that line behind creates the doc-rot this repo keeps paying for** |
| **R-3** | ⛔ **Do NOT filter at write time** | See `D-2`. **Recording only maintenance codes destroys information permanently and cannot be undone** |
| **R-4** | ⚠ **Transition-only is keyed on the WHOLE state string** | Once the code is in the state, `VENUE_RPC_11051` → `VENUE_RPC_10009` is a **real transition** and correctly writes a second line. **Do not add special-casing to suppress it** |
| **R-5** | ⛔ **The clock** | GMT+8 workstation, UTC project dates. **Run `date -u`.** It has fired in nine consecutive sessions |

### 0.2 Escalation trigger

- **Any change to shape A's state string.** It stays `VENUE_<http-code>`; only shape B changes.
- **Any change to what `ShouldRecord` returns True for.** ⛔ **This fix changes WHAT IS RECORDED IN THE LINE, not WHICH responses are recorded.** If you find yourself narrowing the trigger, stop — that is `R-3`.
- **Any need for a `settings.json` key.** Reserved class; stop.

---

## 1. The residual, precisely

**As shipped, `ShouldRecord` returns True for a 200 carrying ANY JSON-RPC `error` object, and the helper writes `"VENUE_" & code` — which for every shape-B case is the literal string `VENUE_200`.**

⛔ **So the log cannot distinguish these two, and they are opposites:**

| The venue returned | Whose fault | What the log says today |
|---|---|---|
| `{"error":{"code":11051,"message":"system_maintenance"}}` | ⭐ **The venue's** — correctly scoped out | `VENUE_200` |
| `{"error":{"code":10009,...}}` or an `invalid_params` | ⛔ **OURS — a bug in our request** | `VENUE_200` |

⛔⛔ **That is the `V-1` failure one level down: when `C-3b` consumes these lines it would scope an hour out of the coverage report because OUR OWN request was malformed.** **The instrument exists specifically to stop our defects excusing themselves.**

⚠ **The spec's own instruction was satisfied in letter and empty in practice.** It said *"record the STATUS CODE so there is a slot for a richer signal later."* **For shape B the HTTP status is ALWAYS 200, so the slot carries zero information exactly where it is needed most.**

---

## 2. The change

**Shape B's state string becomes `VENUE_RPC_<rpc-code>`** — e.g. `VENUE_RPC_11051`.

| Shape | State string | Changed? |
|---|---|---|
| **A** — non-2xx | `VENUE_<http-code>` — `VENUE_503`, `VENUE_502` … | ✅ **unchanged** |
| **B** — 200 + JSON-RPC error | ⭐ **`VENUE_RPC_<rpc-code>`** | ⛔ **changed from `VENUE_200`** |
| **B** — 200 + error object with **no readable code** | **`VENUE_RPC_UNKNOWN`** | new — see `D-3` |
| **C** — no response | *nothing written* | ✅ **unchanged.** `V-1` |

⭐ **`RPC` is the shape marker, so the HTTP 200 need not be repeated** — the token only ever occurs on a 200.

**`IsRpcError` gains a sibling that extracts the code** (or keep one function returning the code and let `Nothing` mean "not an RPC error" — implementer's call, `D-4`). **Both must keep the existing `Try/Catch` → treat-as-not-an-error behaviour on an unparseable body.**

---

## 3. ⭐ D-table — all taken under the auto-proceed ruling

✅ **Reserved-class test run against this change:** no scoring impact · no rendered value (no snapshot line, no card binding) · no `settings.json` key · **not a schema change in the sense the reserved class means** — the file was created hours ago, holds no historical data, and has no consumer. ⭐ **And every pick below is the MORE-information option, so the last reserved class does not fire.**

| # | Decision | Options | ✅ TAKEN, and why |
|---|---|---|---|
| **`D-1`** | Shape B's state string | (a) keep `VENUE_200` · **(b) `VENUE_RPC_<code>`** · (c) `VENUE_200_<code>` | ✅ **(b).** (a) is the status quo and loses the code. (c) is redundant — `RPC` already implies 200. ⭐ **(b) is self-describing: a future seat reading `VENUE_RPC_11051` in a log needs no document to know what happened. That is the truthfulness prior applied** |
| **`D-2`** | Filter at write time, or record all and filter at the consumer? | (a) log only known maintenance codes · **(b) log every RPC error, let `C-3b` decide which codes scope an hour out** | ✅✅ **(b), and this is the most important row.** ⛔ **(a) destroys information at the only moment it exists.** The consumer's policy will change; **the record cannot be re-made.** ⭐⭐ **And it is the `CLAUDE.md` prior exactly: a log holding `VENUE_RPC_10009` lets a future seat discover *"that was our bug, not maintenance."* A log that silently dropped it teaches them nothing, and nothing in the code would say what was being dropped or why** |
| **`D-3`** | A 200 with an `error` object but no readable `code` | (a) skip it · **(b) `VENUE_RPC_UNKNOWN`** | ✅ **(b).** ⛔ **(a) is a silent hole — the class this repo has now rejected three times.** **A line saying "the venue errored and we could not tell how" is a tripwire; no line is invisible** |
| **`D-4`** | One function returning `Integer?`, or `IsRpcError` plus an extractor | either | ✅ **Implementer's call — record which you chose in one line.** Both are correct; the only requirement is that an unparseable body still reads as "not an RPC error" |
| **`D-5`** | Rename `venue_status.log`, since it can now record our own bad requests? | (a) rename · **(b) keep the name** | ✅ **(b).** ⭐ **The name is already truthful: it is a log of what the VENUE RESPONDED. The venue really did return `10009`.** ⚠ **Document that in the file header** — the log records venue responses, **some of which reveal OUR bug rather than their outage, and telling them apart is the consumer's job.** ⛔ **A rename would also mean touching `collector.ps1`'s fetch list again for no gain** |

---

## 4. Build list

1. **`Core/VenueStatusLog.vb`** — add the RPC-code extraction; **keep `ShouldRecord`'s trigger set EXACTLY as it is** (`R-3`).
2. **`DeribitClient.RecordVenueIfNeeded`** — compose `VENUE_RPC_<code>` for shape B, leave shape A alone.
3. **`Core/VenueStatusLog.vb:15`** — update the format doc comment (`R-2`), and add the `D-5` note about whose fault a line may reveal.
4. **`verify/ordercheck/Program.vb:13624`** — update `A74e` (`R-1`). ⛔ **Do not touch `A74a` at `:13459`.**
5. **Fixtures `A75a` / `A75b`** — ✅ **`A75` is the next free family, verified 2026-09-11: `A74e` is the high-water mark.**

### Not in this build

- ⛔ **The `C-3b` consumer.** Still blocked on real data existing. **This fix makes that data worth consuming.**
- ⛔ **Any change to which responses are recorded.** `R-3`.

---

## 5. Fixtures

| # | Asserts | ⛔ The mutation that must fail it |
|---|---|---|
| **`A74e`** *(updated)* | Shape B logs **`VENUE_RPC_11051`**, and the body is still **returned unchanged** | Revert the state string to `VENUE_200` → fails on the state, and the drop-in half still passes, proving the two assertions are independent |
| **`A75a`** | ⛔⛔ **THE ONE WITH TEETH.** Two DIFFERENT RPC codes produce two DISTINGUISHABLE lines — `VENUE_RPC_11051` and `VENUE_RPC_10009` — so the consumer can tell maintenance from our own bad request | Compose the state from the HTTP status instead of the RPC code → **both lines read `VENUE_200` and the fixture fails.** ⭐ **This is the residual, asserted** |
| **`A75b`** | A 200 whose `error` object has no readable code logs **`VENUE_RPC_UNKNOWN`**, not nothing | Skip on a missing code → no line, fixture fails (`D-3`) |

⛔ **MUTATION-PROVE ALL THREE AND PASTE THE ACTUAL OUTPUT.** ⚠ **A fixture that throws instead of failing aborts the harness and hides every later fixture — guard any file read.**

---

## 6. Acceptance

| # | Check | Expected |
|---|---|---|
| `AC-1` | Harness | **366 → 368, ALL PASS** (⚠ `A74e` is updated, not added; **+2**, not +3) |
| `AC-2` | Solution Release `-t:Rebuild` | **0 errors, 0 warnings** |
| `AC-3` | `verify-gate.ps1 -Mode local-fast` | **GATE PASSED** |
| `AC-4` | `settings.json` | ⛔ **untouched at v68** |
| `AC-5` | `grep -c 'VENUE_200' Core/VenueStatusLog.vb DeribitClient.vb verify/ordercheck/Program.vb` | ⭐ **0 outside a superseded-text comment.** ⚠ **Count it per file, never globbed, and read the hits — a comment recording the old format is legitimate** |
| `AC-6` | `A74a`'s `VENUE_503` assertion | **still present and passing** — shape A did not move |
| `AC-7` | Display parity | **does not fire.** Confirm from the gate's own *"no snapshot/card drift detected"* |

**Tag:** ⚠ **`DeribitClient.vb` is an engine path and this changes what it writes, so a `DeribitIndicatorProject.md` §15 entry IS owed** — append to the existing venue-status row rather than adding a second one, per §15's one-item-one-row rule. **No `settings.json` bump: no config key moves.**

---

## 7. What I did NOT verify

- ⚠ **I did not confirm Deribit's actual maintenance error code.** `11051` / `system_maintenance` is **carried** from the queue row's 2026-08-11 observation. ⭐ **This fix does not depend on it** — `D-2` records every code precisely so the exact value need not be known in advance. **But the fixture literals are illustrative, not shipped-behaviour: label them MECHANISM per the fixture-literal provenance rule.**
- ⚠ **I did not re-run the harness this session.** **366 is carried from the `67b139c` review.** `AC-1`'s delta assumes it.
- ✅ **CLOSED before handover, not left open: `A74b`, `A74c` and `A74d` assert NO state string** — `grep -c 'VENUE_'` inside each `Sub` body returns **0**. ⭐ **Exactly TWO fixtures assert one, and both are named in the build list: `A74a` at `:13459` (`VENUE_503`, must NOT change) and `A74e` at `:13624` (`VENUE_200`, must change).** **There is no third.**
