# Seat handover — 2026-09-22b (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md), superseded for state. Its §1 (what Jev is), §2 (the three harnesses), §5 (the failure patterns) and §6 (the collector read) **still bind** and are not repeated here.

⛔⛔ **RUN `date -u` BEFORE ANY DATED CLAIM. The trap fired AGAIN this session, on me.** The desktop app displayed *"today is 2026-09-23"* — that is the **GMT+8 local** date. UTC was still **2026-09-22 18:44**. I stamped four documents `2026-09-23` before catching it; all four are corrected. **The app's displayed date is not the project's date.** This is the ninth consecutive session.

**State at close (2026-09-22 18:50 UTC):**
- ⛔ **`master` is 6 commits AHEAD of `origin`, UNPUSHED.** All six are `[no-engine-change]`. The trader tests, then pushes.
- Settings **v68**, untouched. No settings key changed. Nothing deployed. **The collector was not touched at all this session** — not even a read.
- Fixture harness **425 PASS / 0 FAIL**, verified on a clean `dotnet build -t:Rebuild` (0 warnings, 0 errors).
- ⭐ **The Jev programme's second detector is now measured.** Harness 3's `FP-Q1` was found to under-scope badly, the cause was fixed in code, and the fix is reviewed and accepted.

---

## 0. ⛔⛔ FIRST ACTIONS — outstanding work, in order

**Nothing is running.** No agent, no watcher, no scheduled task.

| # | Action | Model + effort | Why it ranks here |
|---|---|---|---|
| **1** | ⛔⛔ **The `8a` RE-MEASURE.** Write a seat baseline for the **six newly-opened `FP-1` sites**, then run | **Opus, HIGH** | ⭐ **THE MOST PERISHABLE THING IN THE REPO.** One careless run prints a verdict on them and the clean population is gone **for ever** ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §5). It is owed to the SEAT and must never be delegated (§4c). **Do this before touching the harness again** |
| **2** | ⛔ **The 2026-09-24 06:00 UTC collector check** | Sonnet, low | Dated, ~35 h out at close. ⚠ Read [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §6 first — `collector.ps1 status` **cannot** read `ws_feed.log`, only `fetch` retrieves it. A clean result shows the box is healthy, **not** that the re-entrancy gates work |
| **3** | `8f` + `8i` — the fourth `FP-Q3` shape: a callee's own default-fallback body | Opus, medium | ⛔ Fixing `8f` closes `8i` as a side effect, which is why they pair. See §2.3 |
| **4** | `8h` — sample `FP-Q1` | Sonnet, medium | It runs **unsampled** by design (`FP-D14`), so nothing in the harness can see a flip. Mechanical: the 5-sample loop already exists for `FP-1` |
| **5** | `8e` — `MTF_TTL_SECONDS` is `Private Const` | ⛔ **Trader's call, not a build** | The only clean fix edits a live-path file for a fixture's benefit. §2.4 |
| **6** | **Harness 4 — the doc scanner** | Opus, high | ⛔ **Measure BEFORE writing the spec**, per [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §5. Harness 3's spec was the first written that way and produced no factual defect in its premises |
| **7** | Harness 5 — the doc re-ranker | Sonnet, medium | Judged on whether it speeds the seat up, not on one run |
| **8** | Harness 2's measurement | — | Needs post-`2026-08-12T17:21:40Z` commits the seat has not seen. ~200/month, so weeks |
| **9** | Harness 1's first run | — | Gated on the absorption S2 header rotation. Not in our control |
| **10** | `commit-walker` §10.10 | — | Deferred deliberately; it OVER-flags, the safe direction |

⛔ **THE ENGINE QUEUE STAYS ON HOLD** by trader direction until the Jev programme closes. See §4.

---

## 1. What happened this session

Six commits, all `[no-engine-change]`, all local.

| Commit | What |
|---|---|
| `9f0e1c3` | Seat baseline for `FP-Q1`, written and committed **before** the detector ran |
| `bc9d304` | The measurement: `FP-Q1` under-scopes, 17 of 23 |
| `3c320b8` | Item `8d` — MECHANISM declared on five shipped-equal literals |
| `d31cec3` | Items `8a`/`8b`/`8c` — the `FP-Q3` enumeration gaps closed |
| `9bce1bd` | Seat review of the two build commits, plus a correction to my own record |
| *(this doc)* | Handover |

**Owed items 1 and 2 of [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §4 are both CLOSED.**

---

## 2. ⭐⭐ The `FP-Q1` measurement — the session's real result

**Full record: [`harness-runs/fixture-parser-scope-run-2026-09-22.md`](harness-runs/fixture-parser-scope-run-2026-09-22.md).** Baseline: [`harness-runs/fixture-parser-scope-20260922T152622Z-baseline.json`](harness-runs/fixture-parser-scope-20260922T152622Z-baseline.json).

### 2.1 The result

**23 parameters. Agreement 17 of 23 (73.9 %)** — and **12 of 18 (66.7 %)** once the five parameters whose answers [`fixture-parser-check-spec.md`](fixture-parser-check-spec.md) §7.5 *prescribes* are removed. **Every miss is in that clean subset, so the headline understates the gap.**

⛔ **All six disagreements run ONE way.** The detector found **1** of the **7** settings-derived thresholds in its own population.

⛔ **What that hid:** fixture `A23a` passes **four** literals that all equal shipped values, every one scoped out, none declaring a class. The `A43b` breach shape, four at once, in a fixture the harness reported as covered.

### 2.2 ⛔⛔ The cause I proposed was WRONG, and the refutation is the lesson

A **perfect 1-for-1 correspondence across all 23 items** said the state's mapping-class label drove the verdict. A controlled probe swapping only that field — 5 samples a cell, two cells as controls — **reproduced both controls and flipped neither test. Refuted.**

⭐ **The deciding class held exactly ONE item.** A correspondence can be perfect across a whole population and still be `n=1`. **Count the items in the arm that decides, not the rows in the table.**

⭐⭐ **What survived the refutation is the fix, because it never depended on diagnosing Jev:** every miss had a mechanically derivable cfg path the enumeration missed. **Programme finding 3 for the fourth time, and the first time with positive evidence — improving the judge's input framing measured ZERO effect.**

### 2.3 What was built, and what it exposed

`8a` closed two gaps — the **fixture-local cfg builder** and the **one-hop forwarding wrapper**. All six parameters now resolve a cfg path **by construction**, so `FP-Q1` is never consulted for them.

`8b` split `NOT_CFG_SOURCED` (an earned negative) from `SOURCE_NOT_TRACEABLE` (the search did not complete). ⛔ **Measured 0 / 22 — not one site the old label wrote off was an earned negative.**

`8c` gave `FP-Q1` its own baseline refusal (`-ScopeBaselinePath`), scoped to the **call** rather than the run, so it does not conflict with `FP-D11`'s ordering.

⛔⛔ **Two things the review then found:**

- **`8i` — the `8d` declaration pushed `staleAfterSec` OUT of `FP-Q1` scope.** It was the detector's **only** hit and the one site both judges agreed on; after the MECHANISM comment it is excluded on both review runs, **so `FP-1` will never judge the declaration just written for it.** ⚠ The comment is the plausible cause and is **NOT established**. **Fixing `8f` makes such sites immune**, because a derived key puts them in scope by construction.
- **`8h` — `FP-Q1` is not answer-stable.** `ttlSeconds` flips: excluded on five runs, in scope on a sixth, a delta of exactly that one parameter's six sites.

### 2.4 `8e`, stopped deliberately — **your call**

`MTF_TTL_SECONDS` is `Private Const` in `UI/MainForm_Layout.vb:72`, and the fixture restates `60` at `verify/ordercheck/Program.vb:2127-2141`. `CLAUDE.md` rules such a value `Public Const` so the fixture **reads** it.

⛔ **`Public Const` alone does not work here.** `verify/ordercheck/OrderCheck.vbproj` links **no** `UI/*.vb` file by design; compiling `MainForm` in would drag `System.Windows.Forms` into the fixture project and break the host-agnostic boundary that vbproj asserts in six comments.

⭐ **The move that WOULD work, unbuilt and unruled:** put the constant on the host-agnostic `MtfRefreshPolicy` (already linked) and have `MainForm` read it there. **That edits a live-path file for a fixture's benefit, which is why it is a trader decision and not an implementer's.**

---

## 3. ⭐ Lessons worth carrying

1. ⛔⛔ **`date -u`, every time. The displayed date is GMT+8.** It caught me this session after eight prior sessions recorded the same trap. Knowing the rule did not prevent breaking it — **running the command does.**
2. ⭐⭐ **A perfect correspondence can be `n=1`.** §2.2. Count the deciding arm.
3. ⭐ **Commit the baseline BEFORE the run.** Then the ordering is provable from git rather than asserted, which is worth more than any sentence claiming it.
4. ⭐ **Get the candidate list with the API key UNSET.** `SCOPE_JEV_CALLS=0` means the list carries no judgment, which is exactly protocol §2 step 2 and costs nothing.
5. ⭐⭐ **The implementer overturned my suggestion with a better reason than I had.** I proposed swapping `A23a`'s literals for off-shipped values; it refused, because those four are the hardest members of the population `FP-1` exists to judge and neutering them is the same error as tuning the question. **Say in a brief that your read is overturnable, and mean it.**
6. ⭐ **A declaration can un-judge its own site.** `8i`. A tool whose scope filter reads the comment block has a feedback loop in it.

---

## 4. ⛔ The engine queue — STILL ON HOLD, still untouched

**Nothing in [`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §4 has been touched for three sessions.** Still waiting: the **engine-fix build** ([`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md), fully ruled, untouched) · **absorption S1/S2** with its **eight riders**, `T-7` the only gate · **`Q-1` option (d)** · the UI-thread liveness heartbeat, gated on the 09-24 check.

⚠ **"No deadline" is not "no urgency"** — that is how those riders accumulated, and `RIDER-7` was already lost once.

---

## 5. ⚠ What I did NOT verify

- **That 23 parameters generalise.** It is the whole current population, not a sample. One population is not a benchmark.
- **What drives `FP-Q1`'s split.** §2.2 refuted one cause and **named no replacement**. That is deliberate.
- **That the `8d` comment caused `staleAfterSec` to leave scope.** Plausible, unestablished — `ttlSeconds` flips unprompted, so one sample cannot separate cause from noise.
- **Anything about the collector.** ⛔ **Not read at all this session.** Every reading in [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §6 is now over 28 h old, and the `pagesOUT/s` question is untouched.
- **The `8a` build's own claim of `IN_SCOPE_PARAMS=25`.** I measure **24** twice; the difference is the unstable `ttlSeconds`. Its acceptance table states a single draw from a variable it separately discloses as unstable.
- **`FP-2` in any form.** Untouched this session.
- **Harness 2, 4 and 5.** Not run, not built.
