# Implementer brief — Sessions A, C and B2, one seat, sequential

**You are the single implementer for THREE sessions, in this order: A, then C, then B2.** Do them **one at a time in this same conversation**. Do not start the next until the previous one is committed.

**Spec:** [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md). **Read it in full before you touch anything.** Sessions A, C and B2 are its §3, §5 and §4. Its §6 carries four rulings (`EF-1` to `EF-4`) that change what you build. Its §7 is the trap list.

⛔ **A seat that reads only its own section gets trap `EFT-1` wrong.** That trap is the most likely way this build fails.

---

## 0. Why one seat, and what you own because of it

A parallel split was considered and rejected on measured evidence ([`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §0a). All three of your sessions write `verify/ordercheck/Program.vb`, and its fixture dispatcher is a **14-line block at lines 737-750** that every session must edit.

**So you own, across all three sessions:**

- **Fixture ID allocation.** `A83a` is a SUGGESTION in the spec, not a checked fact. **Verify it is free before you use it.** This repo already carries the `A56b` ID-collision scar, where a spec's planned id was taken by a different family in the tree.
- **The harness count.** It is one number. Baseline is **409 PASS, `ALL PASS`**, with `A80b` and `A81b` SKIP. Report the count after each session.
- **The dispatcher block.** One seat, one edit sequence, no conflicts.

---

## 1. Model and effort, per session

| Session | Model | Effort |
|---|---|---|
| **A** — `D-1` POC gate, `D-2` manual lines | **Opus** | **high** |
| **C** — `D-8` lean column, `D-9` on both surfaces | **Opus** | **high** |
| **B2** — `D-4`, `D-5`, `D-6` | **Opus** | **high** |

**Why high for all three.** Session A is a **live scoring-outcome change and a dataset boundary** — four lines of code whose blast radius is 1.68 % of population rows flipping to NO TRADE. Session C engages the **display-string parity hard rule** on both surfaces at once. Session B2 is a scoring change on a vote that has **never fired live**, with an `MT` case that has no live example anywhere.

**The judgment is already done — every value is ruled.** The effort is for not getting the mechanics wrong in places no test looks.

### Where you will specifically slip, per session

| Session | Trap | Why no fixture catches it |
|---|---|---|
| **A** | ⛔ Swapping all FOUR labels instead of the two `NEAR_HVN_*` labels | The `IN_LVN_*` halves are **already correct**. `A80b` exercises the `NEAR_HVN_*` path only, so a four-way swap passes `A80b`, passes `A80a`, and passes every other fixture in the tree |
| **A** | Rewriting `A26b` with another hand-set label the producer never emits | **You write the fixture, so the same misunderstanding propagates into its own test.** `A80a` is the only producer-built pin that exists |
| **C** | Parsing `fundingStep3bNote`'s text inside the card | The note is a display string. A test asserting the card matches the note passes while both are wrong, and breaks the day the wording changes |
| **C** | Fixing one of the THREE card funding-momentum sites | They are far apart — `UI/MainForm_Render_Cards.vb` lines 2071, 3221 and 3566. A partial fix looks complete on screen |
| **B2** | Using the `E-2` one-line boolean from the bug-hunt spec-back | It is right for `T` and `M` and **silently wrong for `MT`**. It was a mutation probe, not a design |

### Escalation triggers — stop and report

- **Session A:** the re-run of handle `H-3` does not reproduce **143 flipped population rows of 8,508** and **1,288 moved placed targets of 38,665** at your base commit. A different number means the counterfactual and the fix disagree, and the fix is not understood.
- **Session C:** carrying Step 3b's effect onto the snapshot needs a line **added or removed** rather than the existing `Momentum:` line re-formatted. `EF-4` (a) authorises a re-format of one line; it does not authorise a new line.
- **Session B2:** the probe's measurement shows the stream DOES carry a readable liquidation flag. That contradicts finding `L-1`, and the fix design changes.

⛔ **Honour these literally.** Surface the match and ask. Do not judge the fix mechanical and push through.

---

## 2. Session order and the gate between each

### Step 2 — Session A first

`D-1` is the dataset boundary and sits on the deploy critical path. Read [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §3 in full.

**Gate before moving on:** committed, harness at **413 or more** and `ALL PASS`, `A80b`'s `SKIP` line gone, and handle `H-3` re-run with its output pasted.

### Step 3 — Session C second

`D-8` is the offline report and never deploys; `D-9` is the card and the snapshot. Read [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §5 in full, and its §6 for `EF-2` and `EF-4`.

⛔ **`EF-4` (a) means the TEXT surface moves.** The snapshot change and every matching card binding go in **the same commit**, and the commit message names each binding. A commit that moves one surface and not the other breaks the display-string parity hard rule.

**Gate before moving on:** committed, `ALL PASS`, and the commit message naming every changed binding.

### Step 4 — Session B2 last, when the probe returns

Session B1 is running a measurement probe in a **separate seat**. B2 starts only when that probe has paired a flagged liquidation trade and written its read document.

⚠ **Rebase first.** B2 lands after Sessions A and C have merged, and its `A81b` rewrite sits about **20 lines** from Session A's `A80b` rewrite in `verify/ordercheck/Program.vb`.

⚠ **If the probe has not returned by about 2026-09-28 UTC, STOP and tell the trader.** B2 then ships later on its own dataset boundary rather than holding the deploy — this is already ruled, under `EF-1` in [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §6.

---

## 3. What you must NOT touch

- ⛔ **`tools/WsTradeProbe/`** — Session B1's seat owns it.
- ⛔ **`settings.json`.** No ruling in this build adds or changes a key. **It stays v68.** Say so explicitly in every commit message.
- ⛔ **`AnalysisLogger.Header`.** It does not change, so the rotation-rider gate in `tools/checks/verify-gate.ps1` does not fire and [`docs/csv-rotation-riders.md`](csv-rotation-riders.md) is untouched. **Confirm by reading your diff, not by assuming.**
- ⛔ **`D-1` option (b)** — removing the label gate. The trader ruled (a). Do not re-open it.
- ⛔ **Renaming the `NEAR_HVN_SUPPORT` and `NEAR_HVN_RESIST` labels.** Both misreadings trace to those names, but a rename is display-visible and is a separate reserved decision.
- ⛔ **`large_liq_size`'s VALUE.** Only the unit is ruled now. The re-derivation needs real liquidation sizes, which do not exist until `D-4` ships and runs.
- ⚠ **`analysis/` stays host-agnostic** — no `System.Windows.Forms`, no `Control.Invoke`, no `MainForm` coupling. The Linux CLI port depends on it.

---

## 4. Acceptance

Per-session acceptance is in the spec: §3.6 for Session A, §5.3 for Session C, §4.5 for Session B2. **All of it applies.** The items that are easiest to skip:

1. ⛔ **Run every handle and paste its actual output.** A handle that has not been run is a guess. **Pin each one to the commit your build started from, never to `HEAD`.**
2. **Re-run handle `H-3`** (Session A). The instruments are present and re-runnable — `tools/ops/SwingFallbackRead/`, `aws_fetch/20260913-153704`, `AWS-copybacks/pooled-book-2026-09-09`.
3. **State in each commit message** whether a render surface moved and why.
4. ⚠ **A build failure naming a locked `.exe`** (`MSB3021`, `MSB3027`) is **not a compile error** — the app is running. Close it, or build `verify/ordercheck/OrderCheck.vbproj`, which type-checks the same sources.

---

## 5. Reporting

Report back **per session**, with **two documents each**, per [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md):

- a `*-batch-summary.md` outcome record — what happened;
- a `*-spec-back.md` review packet — ranked verification handles with pasted output, decisions taken one line each, decisions queued with your read, feedback on this spec's own assumptions, and what you did not verify.

⛔ **Rank handles by whether the READER can run them.** Label an executable check `H-n` and build-time evidence the reader cannot re-run `E-n`. **Never rank an `E-n` first**, and never pair two handles "run both or neither" when one of them is unrunnable.

⚠ **If an instrument you build is the SOLE cover for a property, say so before deleting it.** A scratch console project was once the only thing exercising a call site the harness structurally cannot reach, and deleting it left the property unguarded.

⭐ **Log every auto-proceeded decision in ONE line** — the decision, the options, what you picked, why. Put it in the spec-back's decisions-taken section.

---

## 6. Standing rules that bind you

- ⛔ **Run `date -u` first.** The workstation is GMT+8; every project date is UTC.
- ⛔ **Run `git status -sb`. Never inherit a push state.**
- **Local-first commits.** Commit as you go; push only after it compiles and the trader has confirmed.
- **Fixture-literal provenance (hard rule).** A fixture passing a settings-derived threshold as a literal must declare in a comment at the call site whether it asserts SHIPPED BEHAVIOUR (then derive it from cfg) or MECHANISM (then a literal is right, and say why). ⚠ **MECHANISM means off-EVER-shipped, not off-currently-shipped** — check a literal against every tracked revision of `settings.json`, not today's.
- **A fixture for a gate that consumes a label must build the label from the PRODUCER**, not hand-set it. That is the lesson `A26b`'s own defect taught.
- **A mirror test cannot see a defect that inverts both sides.** Pair every mirror fixture with a per-site direction fixture built from documented intent.
- **Do not line-anchor greps over VB.** `^\s*Return` misses `If … Then Return`, the commonest form in this codebase. The same trap applies to `^\s*Throw`, `^\s*Exit` and `^\s*Continue`.
- **No scripted in-place edits on the dense docs.** `perl -0777 -i` mangles the ⚠ ⛔ ⭐ markers. Use `Edit` or `Write`, and check for mojibake and control bytes before committing a script-edited file.
- The output-format rules in `C:\Users\user\.claude\CLAUDE.md` apply to every reply: point form and tables, never a bare section number or bare ID, short active sentences that keep every domain term, and verified separated from carried.
