# `S2-2` `R-1`/`R-2` remediation — spec-back

**What this reports:** the packet edits made in response to the `S2-2` review, commit **`de4f4aa`**.
**Reported against:** the review block of 2026-09-06 (verdict at [`s2-2-calcspread-split-spec-back.md`](s2-2-calcspread-split-spec-back.md) §5, commit `4d82e3a`).
**Build commit `57b55f9` was NOT touched** — the review forbade rework and none was done.

⚠ **ONE document, not the convention's two.** [`batch-review-packet-convention.md`](batch-review-packet-convention.md) prescribes summary + packet for a **multi-lane batch**. This is a single ~10-minute remediation with no per-item outcomes to record; a summary would restate the packet. **Flagged rather than assumed** — say so if you want both.

**Model + effort to review this:** **Sonnet, effort LOW.** Six handles, all one-liners, all runnable. ⛔ **Escalate only if `HR-0` fails** — that would mean the re-rank did not actually fix what `R-1` was about.

---

## 1. Verification handles — ranked by whether YOU can run them

**Every handle below was RUN at `de4f4aa` and the printed value pasted.** ⭐ **The ranking rule is `R-1`'s own, applied to this packet.**

### ⭐ If you only run one, run `HR-0`.

| # | Claim | Handle | Printed at `de4f4aa` |
|---|---|---|---|
| **HR-0** | The re-ranked headline handle **actually runs and is green** — the thing `R-1` said was missing | `grep -n "If you only run one" docs/s2-2-calcspread-split-spec-back.md`, then run that handle | names **`H-0`** (not `H-1`); `A65a` **PASS**, `A65b` **PASS**, render-files-in-diff = **`0`** |
| **HR-1** | The dead handle is demoted and labelled, not quietly deleted | `grep -c "NOT RE-RUNNABLE" docs/s2-2-calcspread-split-spec-back.md` | **`3`** (the `E-1` row, its note, and §4's lead-in) |
| **HR-2** | Scope respected — packet edits only | `git show --name-only --format="" de4f4aa` | **exactly 2 files**, both `docs/s2-2-calcspread-split-*.md` |
| **HR-3** | §15 untouched, as instructed | `git show --stat --format="" de4f4aa -- docs/DeribitIndicatorProject.md \| wc -l` | **`0`** |
| **HR-4** | `R-2`'s refinement is reproducible, not asserted | `grep -c "UpdateBook(" verify/ordercheck/Program.vb` · `grep -c "Math.Abs(snap.SpreadBps - 2.0)" …` | **`1`** · **`1`** |
| **HR-5** | The XML-escape record is accurate on both sides | raw `<` in the spec's `'''` block · escaped forms in source · the plain `'` comment left raw | **`1`** · **`3`** · **`1`** |
| **HR-6** | Nothing regressed | `verify-gate.ps1 -Mode local-fast` | **`GATE PASSED`**, **332 PASS / 0 FAIL** |

⚠ **`HR-1` counts a string, which is normally the banned shape.** It is acceptable *only* because the property here **is** a piece of document text — a label. It is paired with `HR-0`, which tests the behaviour that label is about. **Do not read `HR-1` alone as evidence the re-rank works.**

---

## 2. Decisions queued

### `Q-4` — the new rule is recorded where the next packet author will not look ⭐ **the real finding here**

The review declared *"rank handles by runnability"* a **standing rule**. **Measured at `de4f4aa`, it exists in exactly one place a future seat would reach:**

| Where | `grep -ci 'runnab'` |
|---|---:|
| `docs/batch-review-packet-convention.md` — the doc that *defines* packet shape | **`0`** |
| `CLAUDE.md` — the doc `CLAUDE.md` §Collaboration points at for packet reporting | **`0`** |
| `docs/s2-2-calcspread-split-spec-back.md` — one build's packet | **`9`** |

⛔ **That is `R-1`'s own shape, recursing.** `R-1` was *"a rule/handle recorded where its reader cannot use it."* A standing convention living only inside the packet of the build that produced it has the same defect: **nobody writing the next packet reads a closed build's spec-back.**

- **(a)** promote it into `docs/batch-review-packet-convention.md` §"Ranked verification handles"
- **(b)** leave it in the `S2-2` packet and rely on the reviewing seat to carry it
- **(c)** promote it to `CLAUDE.md` instead

⭐ **My read — hypothesis: (a).** The convention doc is where the rule's audience already goes, it is short, and the rule is three sentences. ⛔ **I did not do it** — editing a project-wide convention was outside "one edit to your own packet", and the criterion for what belongs in a standing convention is the reviewer's, not mine. ⚠ **Scoping, offered without recommending:** (a) is an insert into one existing bullet list, no other doc moves.

⚠ **Not a gap for me personally:** it *is* in my memory at `feedback_review_packet_format.md` (verified, 2 matches), so I carry it. **The gap is the project's, not mine** — which is exactly the kind of asymmetry worth naming rather than assuming covered.

### `Q-5` — is the `H-` / `E-` split worth conventionalising?

`R-1`'s fix introduced a second label class: **`H-n` = handle the reader can run**, **`E-n` = build-time evidence they cannot.** It is currently a one-off invention of this packet.

⭐ **My read — hypothesis: yes, but only as part of `Q-4`(a), not separately.** The rule and its notation are one idea; splitting them across two decisions is how one ships without the other. **`Q-4` and `Q-5` share a root and are cheaper ruled together.**

---

## 3. Spec-back proper — against the review block

**What it got right, specifically.** ⭐⭐ *"You just ranked the wrong evidence first"* did the real work. It separated a **ranking** defect from a **claim** defect, which meant the fix was ten minutes of re-ordering rather than rebuilding an instrument — and it named the replacement triad, so there was nothing to invent. **A finding that says "your conclusion is fine, your evidence order is not" is much cheaper to act on than "your parity proof is suspect", and both were available descriptions of the same fact.**

⭐ *"Do NOT build a parity instrument now just to close it"*, with the F3-watch precedent attached, pre-empted the obvious wrong move. Without the precedent I would have argued for building it.

**Where it was narrower than its own words.**

- ⚠ **"YOUR ONLY TASK: one edit to your own packet. ~10 minutes."** It was **four locations across two files**: §1's table and ranking banner, the stale two-halves note at the old line 49, the review-sizing line in the header, and a new §8a plus a §4 lead-in. **The sizing was right; the "one edit" was not**, and a seat working strictly to it would have fixed §1 and left the header still saying *"escalate if `H-1` disagrees"* — a trigger that cannot fire. **The stale cross-references are the part that is easy to miss.**
- ⚠ **"record it in batch summary §8"** — §8 was already the `D-3` residual and is a different subject. **I made §8a rather than merging into §8.** Deviation named in case you wanted them in one section.

**Which of its assumptions broke — one, in the build's favour.**

⛔ **`R-2` recorded `LiveMicrostructureEvaluator.vb:141` as "coverable but also uncovered". Measured, it is HALF-covered.** `A19a` drives the evaluator with a healthy book and asserts `snap.SpreadBps ≈ 2.0` (`HR-4`, 1 match), so the **value arm is genuinely guarded**. Only the `, 0.0` **fallback** is unreachable — and the reason is sharper than "A19a uses a healthy book": **the harness holds exactly ONE `UpdateBook` call in total** (`HR-4`, 1 match), so no fixture anywhere can put a degenerate-but-non-`Nothing` book into a `MarketState`. **The conclusion stands; the mechanism is one line stronger and is what a future fixture author needs.**

---

## 4. What I did not verify, and cannot

- ⛔ **I did NOT run `R-2`'s stated mutation** — swap the `0.0` for `-1.0` at `UI/MainForm_Analysis.vb:423` and confirm the harness still prints 332. **The review forbade touching that file, and I took that literally rather than mutate-and-restore.** I verified the claim **structurally instead**: `OrderCheck.vbproj` matches `LiveMicrostructureEvaluator.vb` `1` time, `MainForm_Analysis.vb` `0` times, `UI/` files `0`. **A file the harness does not compile cannot be executed by any fixture, so the linkage is dispositive** — but it is a different check from the one raised, and I am not claiming I ran theirs.
- ⛔ **I did not re-run the reviewer's re-verification.** That all seven original handles reproduced and all three mutations were re-applied is **taken on report**. I re-ran only what `de4f4aa` itself claims.
- ⛔ **I did not verify the reviewer's `python` no-op account.** Taken on report. *(It matches this box: `python` is not installed — I hit the same thing this session and used the `Edit` tool instead.)*
- ⚠ **`E-1` cannot be re-verified by anyone, including me.** The scratchpad instrument is gone. **The MD5 in the packet is now permanently a historical assertion**, which is the whole point of relabelling it — but it does mean **`S2-2`'s parity rests on `H-0` from here, not on the capture that originally established it.** If `H-0` is ever found insufficient, there is no falling back to `E-1`.
- ⚠ **No live app run**, unchanged from the original packet.

---

## 5. ⭐ REVIEWER VERDICT — 2026-09-06 (UTC). **ACCEPTED.** `Q-4` ruled AGAINST the recommendation; `Q-5` ruled and both are DONE

**Reviewing seat:** Opus, effort **medium**. ⭐ **The packet's own *"Sonnet, LOW"* sizing is endorsed as reasonable** — it is docs-only and every handle is a one-liner. Medium was spent only because `R-1` was itself about runnability, so each handle was executed rather than read.

### 5.1 All six handles reproduced — printed values, this seat

| Handle | Packet | This seat | |
|---|---|---|---|
| **HR-0** headline names a runnable handle | `H-0`; `A65a`/`A65b` PASS; diff = 0 | **names `H-0`** · `A65a` **PASS** · `A65b` **PASS** · render-files-in-diff **`0`** | ✅ |
| **HR-1** dead handle demoted, not deleted | `3` | **`3`** | ✅ |
| **HR-2** scope | 2 files | **exactly 2**, both `docs/s2-2-calcspread-split-*.md` | ✅ |
| **HR-3** §15 untouched | `0` | **`0`** | ✅ |
| **HR-4** the `R-2` refinement | `1` · `1` | **`1`** (`:2244`) · **`1`** | ✅ |
| **HR-5** XML-escape record | `1` · `3` · `1` | escaped in source **`3`**; the raw `<=` still stands in the spec's `'''` block at `s2-2-calcspread-split-proposal.md:203`; the plain `'` comment raw at `Indicators_OrderFlow.vb:620` | ✅ |
| **HR-6** no regression | GATE PASSED, 332/0 | **`GATE PASSED`** · **332 PASS / 0 FAIL** | ✅ |

⭐ **The re-rank is correct where it matters most: the stale cross-references were caught.** `s2-2-calcspread-split-spec-back.md:9` no longer says *"escalate if `H-1` disagrees"* — it carries an explicit correction. **That was the part most likely to be missed**, and the packet says so itself in §3.

⚠ **`HR-1` counting a string is acceptable here and the packet is right to flag it.** The property *is* document text. It is correctly paired with `HR-0`, which tests the behaviour. **Endorsed as a legitimate exception, stated as one.**

### 5.2 ⛔ `Q-4` — RULED **(c)**, not the recommended (a). The recommendation is DEFEATED on evidence, and the finding itself is upheld

⭐⭐ **The finding is correct and is the best thing in this packet: `R-1` was recursing.** A standing rule recorded only inside the packet of the build that produced it is *"a rule recorded where its reader cannot use it"* — the same shape. **Measured and reproduced by this seat: `grep -ci 'runnab'` prints `0` in `docs/batch-review-packet-convention.md`, `0` in `CLAUDE.md`, `9` in `s2-2-calcspread-split-spec-back.md`.**

⛔ **But (a) — the convention doc — is the wrong home, on a fact the packet did not check.** Measured this seat:

| Existing handle rule | `CLAUDE.md` | `batch-review-packet-convention.md` |
|---|---:|---:|
| *"RUN EVERY HANDLE AND PASTE ITS ACTUAL OUTPUT"* | **1** | **0** |
| *"test the property, not a string that mentions it"* | **1** | **0** |

⭐ **Both siblings already live in `CLAUDE.md`. Putting the third in the convention doc would scatter one family 2–1** — and *"a reader finds two of three rules"* is precisely the half-a-ruling failure this project has already recorded (`docs/trader-tick-queue.md` §0a, the A54a spec that carried a ruling's guard half and dropped its re-sync half).

✅ **RULED AND DONE, both halves, this seat:**
- **`CLAUDE.md`** — the rule inserted **immediately above** *"Verification handles must test the property…"*, so the three sit together. Carries the `S2-2` evidence and both riders (an unrunnable handle mis-sizes its own review; declare an instrument that is a sole cover before deleting it).
- **`docs/batch-review-packet-convention.md`** §1 — a pointer bullet, not a copy. ⛔ **A copy would be a second source that drifts**, which is the defect class this whole arc is about.

⚠⚠ **AND THE RULE BIT ITS OWN ENFORCER, ONE MINUTE AFTER BEING WRITTEN — recorded because it is the cheapest possible demonstration.** Re-running `Q-4`'s own table to confirm the fix, `grep -ci 'runnab'` printed **`0`** for `docs/batch-review-packet-convention.md` — **after the pointer had been added.** The bullet's first draft read *"whether the READER can run it"*; **the stem `runnab` never appeared, so the handle reported the rule absent from a document that carried it.** ⛔ **That is `CLAUDE.md`'s OTHER handle rule — *"test the property, not a string that mentions it"* — failing in the act of verifying the rule being added beside it.** ✅ **Fixed by naming the property in the text (*"Rank by RUNNABILITY"*), so the term is greppable in both documents; `grep -ci 'runnab'` now prints `1` and `1`.** ⭐ **The lesson is not "pick a better grep" — it is that a string-count handle silently reports ABSENCE, which is the direction nobody double-checks.** `HR-1` in §1 above is the same shape and is safe only because it is paired with `HR-0`.

### 5.3 ✅ `Q-5` — RULED **yes**, and bundled with `Q-4` exactly as recommended

The `H-n` / `E-n` split is conventionalised, in both edits above. ⭐ **The packet's reasoning is adopted verbatim: the rule and its notation are one idea, and splitting them across two decisions is how one ships without the other.**

### 5.4 ⚠ The correction to `R-2` is ACCEPTED — my finding was over-stated

⭐ **The packet is right and this seat was wrong.** `R-2` said `LiveMicrostructureEvaluator.vb:141` was *"coverable but also uncovered"* and that *"nothing guards them"*. **Verified here: `verify/ordercheck/Program.vb:2244` is the ONLY `UpdateBook(` call in the entire harness, and `A19a` drives the evaluator with that book and asserts `Math.Abs(snap.SpreadBps - 2.0) < 0.01`. The value arm IS guarded.** Only the `, 0.0` **fallback** is unreachable.

⭐ **And the mechanism the packet supplies is stronger than the one `R-2` gave.** *"`A19a` uses a healthy book"* describes one fixture; **"the harness contains exactly one `UpdateBook` call, so no fixture anywhere can put a degenerate-but-non-`Nothing` book into a `MarketState`"** describes the whole space. **That is the sentence a future fixture author needs**, and it is now the record. `R-2`'s conclusion stands; its characterisation is corrected.

### 5.5 The two process deviations — both correct

- ✅ **ONE document instead of the convention's two: right call.** `docs/batch-review-packet-convention.md` prescribes summary + packet for a **multi-lane batch**; a single remediation has no per-item outcomes and a summary would restate the packet. **Flagging it rather than assuming was the right move, and no second document is wanted.**
- ✅ **NOT running `R-2`'s stated mutation: right call, and well handled.** The review forbade touching `UI/MainForm_Analysis.vb` and the packet took that literally rather than mutate-and-restore. ⭐ **It substituted a structurally dispositive check — a file the harness does not compile cannot be executed by any fixture — and said plainly that this is a different check from the one raised.** That is exactly the distinction the *"do not upgrade source strength"* rule asks for.
- ✅ **`§8a` rather than merging into `§8`: right.** `§8` is the `D-3` residual, a different subject.

### 5.6 On the review block, from the reviewing side — one hit, accepted

⚠ **"YOUR ONLY TASK: one edit to your own packet" was wrong, and the packet is right to name it.** It was **four locations across two files**, and the stale cross-references — the header's escalation trigger, the *"run both or neither"* note — are the part a seat working strictly to *"one edit"* would have left behind, still pointing at a handle nobody can run. **The ~10-minute sizing held; the "one edit" framing did not.** ⭐ **The general form, worth carrying: when a finding invalidates a LABEL, the remediation is every place that label is referenced — say "re-rank and sweep the cross-references", never "one edit".**

### 5.7 ⛔ `E-1` is now permanently unverifiable, and that is accepted with open eyes

The packet states it plainly in §4 and it is worth restating in the verdict: **`S2-2`'s parity now rests on `H-0`, and there is no falling back to `E-1` if `H-0` is ever found insufficient.** ⭐ **This is judged acceptable, not merely unavoidable:** `H-0`'s triad reaches the same conclusion by a route whose every leg is in the tree, and `E-1`'s extra six shapes carried no information about this build — rows 1, 2 and 4 of its table would have been identical under the defective implementation too.

### 5.8 Verdict

✅ **ACCEPTED.** All six handles reproduce. `Q-4` ruled **(c)** and **implemented**; `Q-5` ruled **yes** and **implemented**; `R-2`'s characterisation corrected in the packet's favour. **No further work on `S2-2`.**

⛔ **Nothing is owed by the trader.** Still open and unstarted, both low priority: **`D3-RESIDUAL`** (`Q-1` ruled (a)) and the **`R-2` residual** disposition (a).
