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
