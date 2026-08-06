# Seat handover — 2026-08-05 (orchestrator/ruling seat)

**From:** the Opus orchestrator seat that opened on [`seat-handover-2026-08-02.md`](seat-handover-2026-08-02.md) and ran through the v65/D3 landing, the C1 build, and the order-app bridge exchange.
**Read in this order:** CLAUDE.md session-start protocol (**step 6 is the state rule**) → [`trader-tick-queue.md`](trader-tick-queue.md) **§0a first — the short list of what is OWED** — then §1 for detail → this doc. Everything else is reachable from the queue.

> **The one thing to carry:** *check before reporting, not before concluding.* Concluding wrongly is unavoidable and is not the failure — the discipline lives entirely in the gap between concluding and reporting. I broke it once this session (called `OhlcCache.vb:137` a bug, then found the early-out that makes it safe) and it cost nothing only because the check happened before the claim left the room. The order-app seat hit the identical shape the same week. Two independent instances is a rule, not a coincidence.

---

## 1. State — verified in the tree 2026-08-05, with how to re-check

| Fact | Value | Re-check |
|---|---|---|
| Settings version | **v65**, tracked | `Get-Content settings.json -TotalCount 2` — the **tracked** file; `bin\` legitimately lags |
| Push state | **in sync**, HEAD `f1e3335` | `git status -sb` |
| Next free fixture family | **A53** (A49a–n, A50a–k, A51a–e, A52a all consumed) | `Select-String verify/ordercheck/Program.vb -Pattern '\bA[0-9]{2}[a-z]_'` |
| Next free hard constraint | **HC28** — verified, HC27 is the high-water mark | `Select-String tools/AutoTweaker/*.vb -Pattern 'HARD CONSTRAINT (\d+)'` |
| AWS collector | **live on v65**, `09c747f8-1efb-4ffe-8716-ec8cedfa54c6` since 2026-08-01 19:02:31Z; capturing since 2026-08-01 17:50Z | `ws_health_aws.log` at next copy-back |
| Local collector | **not running at handover** — that is the default state, not a defect | `Get-Process DeribitVerdictEngine` |
| Local capture | **OFF in BOTH `bin\Debug` and `bin\Release`** | overlay present in each; `backtest_data\` absent under Debug |

**Local build policy (trader, 2026-08-05):** run **Debug** for local collection — it carries the latest build and its overlay keeps tape capture off, so it contributes `analysis_log.csv` rows to the pooled book and no tape. Release now carries the same overlay as a safety net.

---

## 2. What happened

**Shipped:** **v65 / D3** — ASIA aggressor velocity armed at 5.5, live on both boxes from 2026-08-01 19:02Z, boxes restarted **1.6 s apart** so the version edge is cleanly attributable. **C1 trade-store coverage report**, both sessions, reviewed and accepted, one review finding fixed (`c6a7a63`).

**Ruled:** **J-B scoping** — scope a defect rule by a *positive record of intent*, never by a baseline derived from the behaviour being judged (unblocked C1, ruled out D7=(b)) · **weekday scope** (corrects J-C) — **capture stays 24/7, *evaluation* is weekday-only** · **D1 TTM re-derived then PARKED** · **tape retention** — keep all tape unless it is a copy; merge first, judge duplication after · two process rules into CLAUDE.md (**briefs carry model+effort**; **effort is self-set per task**).

**Cut:** the session-start doc read by **37 %** (337,595 → 214,283 chars) by applying §15's own five-row rule, and killed three doc headers that carried stale version numbers.

**Cross-repo:** a long exchange with the order-app seat — §10.2 mirror corrections, the standing rule that **the mirror carries consumption-visible consequences, never emitter mechanics**, and the **T8 amendment** (§4 below).

---

## 3. What is open

**Read [`trader-tick-queue.md`](trader-tick-queue.md) §0a.** Do not rebuild it from a grep over doc prose — that is how 4 of 13 rows went wrong on 2026-08-01. Re-run the §1b sweep instead.

At handover, **owed by the trader:** D2 (derived, but blocked behind the D3 watch), E5 absorption Path B, and the F3 watch decision. **Everything else on the board is a build slot**, not a decision: the three weekday filters (`AutoTweaker` first — the only surface that writes settings), the atomic-write total-primitive swap (§5), C1's F1 trailing-edge fix, F2, G12, the CeilingAudit version constant.

**Live watches:** the **D3 ASIA watch** — fire rate ≈9.7 %, same-side ≥85 %, **read over a multi-day band, never one session-day** (per-day spans 4.5–13.8 % on ~106 rows/day). And the **dated Kelly trigger**, ≥406 pooled weekday STRONG, ETA ~2026-08-30, to be bundled with the W6-4 re-run.

---

## 4. Flagged, not ruled — for whoever picks these up

- **C1's F1** — `captured` means "rows present and no gap *ending* in this hour breached", not "fully covered". A trailing-edge silence is charged to the following hour. Legend line shipped; the bounded fix is queued and **needs care** — the obvious version false-positives every window's final hour and must be bounded against `ResolveBoundaryUtc`.
- **C1's F2** — a capture-state flip mid-hour is scoped by the *previous* marker. Rare, but it errs toward *not* flagging, opposite to J-B's preference. **Needs a split-hour rule, which is a spec question.**
- **The `bin\Release` tape** — 78,798 trades, 2026-08-03 21:44 → 2026-08-05 13:13 UTC, captured by the un-overlaid Release build. **Kept per the retention rule.** AWS almost certainly covers the span but its store has not been copied back, so the comparison cannot be made yet. Resolve at the next copy-back.
- **T8 / the order-app `mode` blind spot** — see [`signal-bridge-v1-proposal.md`](signal-bridge-v1-proposal.md) §10.4. **This is a different class from every other correction in that exchange: no text is wrong anywhere.** Their emitter is right, our tolerance is right, and the defect lives only in the composition — which is why neither side's review could find it alone. Their **acceptance 3** (owner-only: mode Live + ARM + START) is the gate; until it passes the file is **partially proven** — shape, lifecycle and position semantics observed, **disposition path / `last_signal` / mode strings NOT**.

---

## 5. Conventions established this session — these have reach

1. **Scope a defect rule by a positive record of intent, never by a statistical baseline derived from the behaviour being judged.** A box that dies permanently converges to its own baseline and reports healthy.
2. **Capture is not evaluation.** Keep the cheap irreversible thing (recording); drop the expensive misleading one (scoring what you never trade).
3. **A guard is what you OWE when the primitive is PARTIAL. Choosing a TOTAL primitive removes the obligation rather than satisfying it.** (Order-app seat's framing.) `File.Replace` is partial; `File.Move(overwrite:=True)` is total. **Queued as a build slot** — it deletes a hazard class rather than documenting it.
4. **A summary silently drops the caveat its source had.** Distinct from the stale class: nothing is stale, no single copy looks defective, and the absent qualifier reads as "there never was one." Our original spec stated the `File.Replace` fallback correctly; **three summaries of it each dropped the guard.**
5. **When a claim is retired, quote and label it rather than delete it — and grep EVERY copy, code comments included, before calling a correction done.** Docs-only sweeps miss XML summaries in `.vb` files, which is the copy that bit us.
6. **A doc header must not carry a number that lives somewhere else.** The doc-side form of the display-string rule. `architecture.md` read v54 against a v65 tree; CLAUDE.md read v31.
7. **One item gets ONE §15 row**, and the §15 cap governs the **whole** table, not just versioned rows — the untouched category was uncapped and grew to five beside the five versions.

---

## 6. Things I got wrong, recorded plainly

1. **I ran `git add -A` in a tree holding the implementer's uncommitted Session 2 work** and buried 400+ lines under a commit message about bridge wording. Recovered with `git reset --soft`, nothing lost. **Every commit before that used the same pattern and got away with it because the tree happened to be clean.** Stage explicit paths.
2. **I called `OhlcCache.vb:137` a real bug before checking reachability.** It is safe via an early-out twenty lines up. Corrected before it left the room, but the conclusion was wrong first.
3. **I reported E1 Kelly as an open trader decision from a stale queue row** — while §4 of the same document, four sections below, described the display as shipped and pointed at the decision doc. **The contradiction was visible in the file I had already read** and I did not reconcile it.
4. ⚠ **A `Grep` for aggressor-velocity fixtures returned a false "No matches found"**, and I nearly proceeded on it — which would have missed that A28c pinned ASIA as the *un-armed* exemplar, and D3 would have broken the harness. It surfaced only because a later, differently-worded search contradicted it. **Treat a surprising negative from a search tool as unproven until a second phrasing agrees.**

---

## 7. What I did not verify

- **The TAPE STORE strip's amber/red transitions have never been seen.** Only its visible/neutral state was observed, and that was *accidental* — it appeared on the un-overlaid Release build, which is itself the symptom of the overlay gap. On a correctly-overlaid local box the element is hidden by design, so **the colour transitions remain untested on any box**.
- **AWS's store has never been copied back**, so: the `bin\Release` tape overlap is unresolved, and no coverage report has ever run against the real AWS store.
- **The D3 watch has no reading yet** — it needs a multi-day band and the boxes only reached v65 on 2026-08-01.
- **`ClassifyTapeStoreTier`'s new start-clock is fixture-proven, not runtime-proven** (A49n).
- **The order-app feedback file is partially proven** — their acceptances 3–7 were unrun at the last report; acceptance 3 is the only one left and is owner-gated.
