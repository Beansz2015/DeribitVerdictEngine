# `D-2` — re-word the Kelly EST advisory line

✅ **RULED (c) 2026-09-09 (UTC), trader.** ⛔ **Option (b) — "leave it, another doubling will come" — is STRUCK.** Decision record: [`kelly-w6-4-spec-back.md`](kelly-w6-4-spec-back.md) §2.

> ## ✅✅ BUILT AND SHIPPED — commit `517f7b6`, 2026-09-09 (UTC). **THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION. DO NOT HAND IT TO AN IMPLEMENTER.**
>
> ✅ **Candidate A was signed off by the trader** and is live on both surfaces: *"p(win) is ASSUMED from the confidence tier — the calibration read did not separate the tiers."* — `UI/MainForm_PlaintextSnapshot.vb:252` and `UI/MainForm_Render_Cards.vb:1589`. `verify-gate.ps1` reported **`no snapshot/card drift detected`**; harness **346 unchanged**; Release `-t:Rebuild` **0/0**; `settings.json` untouched at **v68**. A `docs/DeribitIndicatorProject.md` §15 entry rode with it, so the commit is deliberately **not** tagged `[no-engine-change]`.
>
> ⛔ **`AC-3` AS WRITTEN BELOW IS WRONG AND FAILED — read §4 with that in mind.** It demanded `grep -rn "book doubling" --include=*.vb` return zero hits in `UI/`. It returned **one**: the new code COMMENT quoting the retired wording to explain the change. **That is the standing defect `CLAUDE.md` names — counting a NAME is a copy of the property and drifts the moment a comment mentions it.** The correct check excludes VB comment lines and passes with zero.
>
> ⚠ **`AC-6` was NOT done** — the line was never seen rendered in the running app. It is 94 characters against the retired line's 96, so it fits by construction, but that is reasoning, not observation.

⛔ **The "do not build" instruction below is SPENT. Kept per the quote-and-label convention.** ~~DO NOT BUILD YET. §3's exact strings need the trader's sign-off first~~ — [`kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md) requires it (*"Exact strings — sign these off before the code lands"*). Everything else in this spec is settled.

**Baseline commit: `d5ce4a5`.** Line numbers read at that commit.

---

## 0. Model and effort

> ### Model: **Sonnet**
> ### Effort: **MEDIUM**
> ### One short session, after sign-off.

**Why MEDIUM and not LOW.** The diff is **one string on two lines**. The weight is entirely in the **engine display-string parity rule** (`CLAUDE.md`, hard rule): the text renderer and the card binding must move in the **same commit**, and this project has three recorded drift instances in one cycle from exactly this seam. **The risk is not writing the line; it is changing one surface and shipping.**

### 0.1 Where this will slip

⛔ **Trap 1 — TWO surfaces, identical text, and nothing enforces the pairing.** The string lives at `UI/MainForm_PlaintextSnapshot.vb:252` **and** `UI/MainForm_Render_Cards.vb:1580`. **The P5-test text-parity harness cannot catch this drift** — it diffs legacy↔snapshot, which move together; the card is the unchecked third surface. **Changing one and not the other compiles, runs, and looks right on whichever surface you happened to open.**

⚠ **Trap 2 — the replacement must carry NO measured number.** The 2026-08-02 decision states the constraint and the reason, and `UI/MainForm_Render_Cards.vb:1573-1577` repeats it in the code: *"Deliberately carries no measured numbers: a string with '46.8%' in it goes stale the moment the book grows."* ⛔ **A replacement containing "47.1 %" reintroduces exactly the defect the original line was written to avoid.**

⚠ **Trap 3 — do NOT touch the `p(win) [MODE]:` row.** `UI/MainForm_Render_Cards.vb:1585-1586` records the design: the mode tag reads off `v.KellyPMode`, not a literal, *"so when CAL ships it renders `p(win) [CAL]:` on its own and **only the advisory line needs retiring**."* **This build retires the advisory line and nothing else.**

⚠ **Trap 4 — the line is NOT fixture-pinned, and that is a hazard, not a relief.** Verified: `grep -rn "book doubling" --include=*.vb` returns exactly the two UI call sites (plus an unrelated `tools/CeilingAudit/AuditReport.vb:218`). **No harness fixture asserts it, so nothing goes red if you change one surface only.** The check is `AC-3`'s grep, run by hand.

### 0.2 Escalation trigger

⛔ **If the chosen wording needs a measured number to be honest, STOP and re-raise.** That would mean the ruling's premise is wrong and the block should be suppressed rather than re-worded — a different decision, not a wording tweak.

---

## 1. Why option (b) is not available

[`kelly-est-honesty-decision-2026-08-02.md`](kelly-est-honesty-decision-2026-08-02.md) sets the watch this line owes: *"ladder still flat, or STRONG still below breakeven ⇒ the line must be re-worded or the block suppressed — **what it must not do is silently promise another doubling**."*

| Then | Now |
|---|---|
| The line promised *"Actual numbers after next book doubling"* | ⭐ **The doubling HAS happened** — the Kelly trigger was met 2026-09-09 at **407 weekday STRONG against ≥406** |
| The promise was pending | **The numbers arrived. The ladder did not separate, and pooled STRONG sits below breakeven** |

⛔ **So leaving the wording converts a pending promise into a false statement on screen.** That is why (b) is struck, and it is the whole argument.

---

## 2. What changes

**Exactly one line, on exactly two surfaces, in one commit.**

| Surface | Line | Current text |
|---|---|---|
| Text renderer | `UI/MainForm_PlaintextSnapshot.vb:252` | `  p(win) is ASSUMED from the confidence tier — Actual numbers after next book doubling.` |
| Card binding | `UI/MainForm_Render_Cards.vb:1580` | `p(win) is ASSUMED from the confidence tier — Actual numbers after next book doubling.` |

⚠ **The snapshot version carries two leading spaces; the card version does not.** Preserve each surface's own indentation — the card's `BuildCardAdvisory` handles its own layout.

⚠ **Also update the comment block at `UI/MainForm_Render_Cards.vb:1573-1577`**, which still explains the line in terms of the 2026-08-02 `F1` read. It should cite this build and the 2026-09-09 measurement instead. **Keep the no-measured-numbers rationale — that constraint survives.**

---

## 3. ⛔ THE STRINGS — the trader picks one. Nothing is built until then.

**All three obey Trap 2: no measured number, no new open-ended promise.**

| # | Candidate | Character count | Note |
|---|---|---:|---|
| **A** | `p(win) is ASSUMED from the confidence tier — the calibration read did not separate the tiers.` | 94 | ⭐ **My recommendation.** Closest in length to the current 96, states the outcome, promises nothing |
| **B** | `p(win) is ASSUMED from the confidence tier — measured at the 2026-09-09 doubling; tiers did not separate.` | 106 | Carries the date, so a reader knows how fresh the claim is. **A date is a record, not a forecast**, so it does not violate Trap 2 — but it does go stale in feel, and it is 10 chars longer |
| **C** | `p(win) is ASSUMED from the confidence tier — tiers unseparated at the last two calibration reads.` | 98 | Conveys that this is the **second** inconclusive read, which is the honest signal. Slightly harder to parse at a glance |

⚠ **Length matters on the card**, which renders inside a fixed-width stack. **The current line is 96 characters and fits**, so anything at or under ~100 is safe; **B at 106 should be eyeballed on screen before it lands.**

⛔ **If none of these is right, say so and give the wording — do not let the implementer invent it.** The sign-off requirement exists because this text is the engine's honesty statement about its own sizing advice.

---

## 4. Acceptance criteria

| # | Criterion |
|---|---|
| **AC-1** | Solution builds `Build succeeded`, **0 errors 0 warnings**, on a full `-t:Rebuild` |
| **AC-2** | Harness `ALL PASS`, count **unchanged at 345**. ⛔ **This build adds no fixture** — the line is display text with no behaviour behind it, and inventing a fixture that asserts a literal string would pin the wording against the next re-word |
| **AC-3** | ⛔ **THE LOAD-BEARING ONE.** `grep -rn "book doubling" --include=*.vb .` returns **only** `tools/CeilingAudit/AuditReport.vb:218` (unrelated). **Zero hits in `UI/`.** Paste the real output |
| **AC-4** | Both surfaces carry the **identical** chosen string — paste both lines side by side |
| **AC-5** | `verify-gate.ps1 -Mode local-fast` → `GATE PASSED`, including its snapshot/card drift check |
| **AC-6** | ⭐ **Seen on screen.** Run the app, open the Kelly card, and confirm the line renders without wrapping or truncation. ⚠ **Delete any screenshot afterwards — do not leave image artefacts in the repo** |
| **AC-7** | `settings.json` untouched — `git diff --stat` must not list it |

### 4.1 Rules that bind the commit

- ⛔ **Engine display-string parity rule — this build IS the case it exists for.** Both surfaces in one commit. **State in the commit message that both moved and name both file:line.**
- ⚠ **`docs/DeribitIndicatorProject.md` §15 — an entry IS owed.** This changes a rendered line the trader reads before sizing. **Do not tag the commit `[no-engine-change]`.**
- ⚠ **No `settings.json` version bump** — no key added, changed or removed.

---

## 5. What to report back

Two documents per [`batch-review-packet-convention.md`](batch-review-packet-convention.md). ⛔ **Handles `H-n` / `E-n`, never an `E-n` first.** `AC-3`'s grep and `AC-4`'s side-by-side are both `H-n` — cheap, local, and exactly what a reviewer should re-run.

**State plainly:** which candidate was signed off, and whether `AC-6` was actually seen on screen or only reasoned about.

---

## 6. What this must NOT do

- ⛔ Change one surface without the other
- ⛔ Put a measured percentage in the string (Trap 2)
- ⛔ Touch the `p(win) [MODE]:` row or `v.KellyPMode` (Trap 3)
- ⛔ Suppress the Kelly block — the ruling is **re-word**, not suppress
- ⛔ Add a fixture pinning the literal string (`AC-2`)
- ⛔ Tag the commit `[no-engine-change]` — a §15 entry is owed
- ⛔ Leave a screenshot in the repo
- ⛔ Touch `settings.json`
- ⛔ Run any `git` write command — commit is the orchestrator's
