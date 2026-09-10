# Doc-status sweep + `trader-tick-queue.md` archive split — implementation spec

> ## ✅✅ BUILT, BOTH SESSIONS — 2026-09-09/2026-09-10 (UTC). **THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION. DO NOT HAND IT TO AN IMPLEMENTER.**
>
> **Session 1 — verified in the tree:** all ten `DS-1`…`DS-10` docs carry a `✅✅ BUILT` banner (`DS-7` a split banner, per §1.4), committed `6cc4d8f` + `e8ea9af`, with two tidy fixes `71aa14c`. `AC-1`–`AC-5` all passed, `verify-gate.ps1 -Mode local-fast` GATE PASSED.
>
> **Session 2 — verified in the tree:** [`trader-tick-queue.md`](trader-tick-queue.md) §2's 39 finished rows and all of former §5 moved byte-identical to [`trader-tick-queue-archive.md`](trader-tick-queue-archive.md); 12 live §2 rows plus 35 one-line index entries remain; line 330's stray separator dropped; 10 of 12 dangling `§2` pointers retargeted to the archive (2 — the stale-claim special cases named in §3a's ruling — corrected in a following commit); §0's authority table now names the archive. `AC-6`–`AC-11` run below.
>
> *(The build-authorization banner follows, kept per the quote-and-label convention. It was true when written.)*

**Status:** ~~✅✅ **BUILD-AUTHORIZED — BOTH SESSIONS. `D-1` ticked (a) by the trader, 2026-09-09 (UTC).** Nothing is owed. ⛔ **Build session 1 first, then session 2 — the order is a dependency, not a preference (§0).**~~

**Author seat:** Opus, 2026-09-09 (UTC). **Baseline commit: `74aeb60` plus the three uncommitted edits described in §1.2.** ⛔ **Re-read every line number if `HEAD` has moved.**

**Origin:** the 2026-09-09 (UTC) orientation read found `WD-SEMANTICS` (a queue item in [`trader-tick-queue.md`](trader-tick-queue.md) §2 — the `UnparsedExcluded` counter) offered as available work one day after it shipped. Fixing it uncovered nine more.

---

## 0. Model and effort

> ### Session 1 — the stale-status sweep
> ### Model: **Sonnet** · Effort: **MEDIUM**
>
> ### Session 2 — the archive split
> ### Model: **Sonnet** · Effort: **MEDIUM**
>
> ⛔ **Run them in this order. Session 2 depends on session 1.**

**Why MEDIUM and not LOW.** The edits are doc-only and the target list is already verified below, so no discovery is needed. **What lifts it above LOW is that all three of this repo's known doc-editing traps are live at once** — the UTF-8 marker set, the markdown table-pipe rule, and the quote-and-label convention. **A LOW-tier pass will reach for `sed -i` or `perl -0777 -i` and corrupt the file silently.**

**Why not HIGH.** No judgment is delegated. Every build state in §1.1 is verified against the tree and the evidence is written out. **The implementer confirms, it does not derive.**

### 0.1 ⛔ Where Sonnet will specifically slip

| # | Trap | Why it bites here |
|---|---|---|
| **T-1** | ⛔⛔ **Scripted in-place edits corrupt the markers** | `perl -0777 -i` and `sed -i` mangle `⚠ ⛔ ⭐ · → ↔ ✅ ~~`. **Every edit in both sessions MUST use the `Edit` or `Write` tool.** The failure is silent — the file still parses |
| **T-2** | ⛔ **A `\|` inside backticks still splits a table cell** | §2 of [`trader-tick-queue.md`](trader-tick-queue.md) is a 3-column table and the new cells are long. **Never assert a raw pipe count — a raw count blames the wrong row.** Compare the UNESCAPED count against a NEIGHBOURING row |
| **T-3** | ⛔ **Counting a NAME is not testing a property** | `grep -c "BUILD-AUTHORIZED"` returning 0 does **not** prove the sweep worked — the string legitimately survives inside struck-through text and inside this spec. **Assert the UN-STRUCK occurrence**, or the sweep will read as done while the banners still mislead |
| **T-4** | ⛔ **Deleting is not archiving** | The quote-and-label convention keeps superseded text. **Session 2 MOVES cells; it never deletes them.** A moved cell must land in the archive byte-identical |
| **T-5** | ⛔ **The clock** | The workstation is GMT+8 and every project date is UTC. **Run `date -u` before writing any date.** It has fired in seven consecutive sessions |

### 0.2 ⛔ Escalation trigger — stop and ask

- **Any doc in §1.1 whose build state you cannot confirm with the stated grep.** Do not substitute a sibling doc's claim for a tree read.
- **Any doc NOT in §1.1 that you believe is also stale.** Report it; do not widen the scope yourself.
- **Session 2: any finished row that a LIVE row references.** Archiving it would break the live reader's link. Stop and list them.

---

## 1. Session 1 — the stale-status sweep

### 1.1 ⭐ The target list — all ten VERIFIED BUILT against the tree, 2026-09-09 (UTC)

⛔ **Every row below reads as authorized-but-unbuilt in its opening four lines, and every one is shipped.** Confirm each with the handle, then write the banner.

| # | Doc | Reads as | Verification handle — run it | Commit |
|---|---|---|---|---|
| **`DS-1`** | [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md) | `BUILD-AUTHORIZED` | `grep -rq 'WalkPocoVsJson' --include=*.vb .` | see §1.3 |
| **`DS-2`** | [`a54a-r2-r3-followup-spec.md`](a54a-r2-r3-followup-spec.md) | `BUILD-AUTHORIZED` | ⛔ **CORRECTED 2026-09-09 (UTC).** Use `git log --oneline --grep='R-2 dict completeness' -i` → `cc44e9f` | **`cc44e9f`** |
| **`DS-3`** | [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) | `BUILD-AUTHORIZED 2026-08-25` | queue §2 row records `DONE 2026-08-26` | see §1.3 |
| **`DS-4`** | [`s2-2-calcspread-split-proposal.md`](s2-2-calcspread-split-proposal.md) | `BUILD-AUTHORIZED 2026-09-06` | `grep -rq 'CalcSpreadBps' --include=*.vb .` | `57b55f9` + `9e418e0` |
| **`DS-5`** | [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md) | *"Part A is authorised and ready to hand to an implementer"* | `grep -rn 'Hole-derived repair windows' --include=*.vb .` → `Core/TradeStoreWriter.vb:699` | `c6c6942` |
| **`DS-6`** | [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) | *"DR-2 and DR-3 are ready to hand over as written"* | `grep -rq 'MinHoleMs' --include=*.vb .` and `git log --oneline --grep='DR-3'` → `5346bc0` | `5346bc0` |
| **`DS-7`** | [`value-copy-guard-implementer-brief.md`](value-copy-guard-implementer-brief.md) | `BUILD-AUTHORIZED` | ⛔⛔ **THE HANDLE IN THIS ROW WAS WRONG AND IS WITHDRAWN — see §1.4. This doc is HALF built and needs a SPLIT banner, not a BUILT banner** | **`3a89093`** (guard half only) |
| **`DS-8`** | [`coverage-split-hour-implementer-brief.md`](coverage-split-hour-implementer-brief.md) | `RULED AND READY TO BUILD` | queue §2 `SH-1` row | see §1.3 |
| **`DS-9`** | [`w6-4-ceiling-audit-method-proposal.md`](w6-4-ceiling-audit-method-proposal.md) | `BUILD-AUTHORIZED` | `test -d tools/CeilingAudit` — built AND run; read executed 2026-09-09 | see §1.3 |
| **`DS-10`** | [`wd-semantics-spec-back.md`](wd-semantics-spec-back.md) | `BUILD-AUTHORIZED` in its header line | it is a spec-back **of a shipped build** — `ab5600f` | `ab5600f` |

⚠ **`DS-10` is the mildest of the ten** — a spec-back is already understood as a record. Fix it anyway: it costs one line and the string is what the sweep greps for.

### 1.2 ⭐ Three edits are ALREADY DONE — do not redo them

The orientation seat fixed these on 2026-09-09 (UTC). **They are the worked example for the banner shape.** Copy it.

| Where | What was done |
|---|---|
| [`wd-semantics-unparsed-counter-spec.md`](wd-semantics-unparsed-counter-spec.md) header | BUILT banner added; the `BUILD-AUTHORIZED` line struck, not deleted |
| [`trader-tick-queue.md`](trader-tick-queue.md) §0a banner | New BUILT banner on top; old ruling banner struck and kept **with its rationale intact** |
| [`trader-tick-queue.md`](trader-tick-queue.md) §2, the `WD-SEMANTICS` and `S-4` rows | Both re-headed as BUILT with the commit and the tree evidence |

### 1.3 What to write — the banner shape

**For each of `DS-1` … `DS-10`, insert immediately after the `# ` title line:**

```
> ## ✅✅ BUILT <UTC date>, commit `<sha>`. **THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION. DO NOT HAND IT TO AN IMPLEMENTER.**
>
> **Verified in the tree <UTC date>, not carried:** <the handle from §1.1 and what it returned>.
>
> *(The build-authorization banner follows, kept per the quote-and-label convention. It was true when written.)*
```

**Then strike the stale status text** — wrap it in `~~ ~~`. ⛔ **Strike ONLY the status clause. Never strike the rationale**, which is still the reason the decision was taken.

⚠ **Where §1.1 says "see §1.3" for the commit:** find it with `git log --oneline --all --grep='<item>' -i` or `git log --oneline -S'<symbol>' -- <file>`. **If you cannot find a specific commit, write the tree evidence and say the commit was not identified.** ⛔ **Do not invent a sha. An unverified sha is worse than no sha.**

### 1.4 ⛔⛔ `DS-7` — the handle was wrong, the doc is HALF built, and there is a live ID collision

⛔ **TWO handles in §1.1 as first written were trap `T-3` — they matched a NAME, not the property. Both were caught by the session-1 implementer, not by their author.** ⭐⭐ **This spec NAMED `T-3` and then broke it twice. The rule does not protect you; running the handle does.**

| Handle as first written | Why it was wrong |
|---|---|
| `DS-2`: `git log --grep='R-2 residual'` → `1ad7d6d` | **`R-2` names two different findings.** `1ad7d6d` is `refactor(indicators): R-2 residual — extract ApplySpread` — the **`S2-2` spread seam**, touching `Core/Indicators_OrderFlow.vb`. The A54a follow-up is **`cc44e9f`**, touching `Core/Settings/EngineSettings.vb` |
| `DS-7`: `grep -q 'A56b' verify/ordercheck/Program.vb` | **`A56b` names two different fixtures.** In the tree it is `A56b_CoveredStoreReturnsTailOnlyAndA48dHolds` — a **trade-store** fixture. It has nothing to do with the value-copy guard |

#### ⛔⛔ The `A56` family collision — verified in the tree 2026-09-09 (UTC)

- **`docs/value-copy-guard-implementer-brief.md` §5 PLANNED `A56a`–`A56d`** — trade-parse sites · **`A56b` = "the guard itself"** · `CalcCVD` slope · `CalcMicroCVD`.
- **The tree holds `A56a`–`A56g`, and they are ALL trade-store / hole-detection** — e.g. `A56c_OutOfOrderStoreProducesNoPhantomHoles`, `A56d_AbsentSeqRowsProduceNoPhantomHoles`.
- ⭐ **The guard actually shipped as the `A62` family — `A62a`–`A62g`, the `WalkPocoVsJson` reflection walk, commit `3a89093`** — under [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md), which is `DS-1`.
- ⛔⛔ **`CLAUDE.md` carries the BRIEF's meaning against a tree that holds the OTHER one.** Its fixture-literal provenance rule reads *"the value-copy guard (A56b) explicitly cannot cover this case"*. **A reader who greps `A56b` lands on a trade-store fixture and the paragraph stops making sense.** ⚠ **RAISED TO THE TRADER 2026-09-09 (UTC). Do NOT edit `CLAUDE.md` in this build — it encodes rulings and is the trader's file.**

#### What to write for `DS-7`

**A SPLIT banner, the same shape used on [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md):**

- **The guard half — BUILT BY SUPERSESSION**, commit `3a89093`, as `A62a`–`A62g`, **not** as the planned `A56b`. Say so explicitly.
- **The probe-parse-site half — DEFERRED, not built.** Ruling of record: [`trader-tick-queue.md`](trader-tick-queue.md) ITEM 6, `S-1` ruled **(a) as the direction, NOT NOW**, 2026-09-07. ✅ **Verified in the tree: `tools/WsTradeProbe/WsTradeProbeProgram.vb` still parses independently — no shared-reader call.**
- ⛔ **Record the `A56` collision in the banner itself.** Without it the next reader repeats exactly this mistake — the doc's own fixture numbers point at other people's fixtures.

### 1.5 Acceptance for session 1

⭐ **Every handle below was RUN on 2026-09-09 (UTC) against the working tree, not written from memory.** ⚠ **`AC-1` exits non-zero because `grep -c` returns 1 when a count is 0 — read the printed counts, not the exit code.**

| # | Check | Expected |
|---|---|---|
| `AC-1` | Mojibake scan over **only the files you edited**, this spec EXCLUDED: `git diff --name-only \| grep '\.md$' \| grep -v doc-status-sweep \| xargs grep -c 'â\|Ã'` | **0 on every file** — no mojibake (T-1). ⛔ **DO NOT scan `docs/*.md`: this spec QUOTES the mojibake pattern, so a whole-directory scan returns 2 hits and a correct build reads as FAILED. That is trap `T-3` firing on this spec's own acceptance criterion — it was caught in review, and it is the worked example** |
| `AC-2` | ⛔ **UN-STRUCK** authorization strings. Handle: `head -6 <doc> \| grep -cE 'BUILD-AUTHORIZED\|READY TO BUILD\|ready to hand\|BUILD-READY'` — **run per doc, not globbed** ⭐ **BASELINE MEASURED 2026-09-09 (UTC): all ten return exactly `1` today.** After the sweep all ten must return **`0`**, with the BUILT banner appearing FIRST | **`1` → `0` on all ten** (T-3) |
| `AC-3` | Unescaped-pipe count on every edited table row equals its neighbours | **equal** (T-2) |
| `AC-4` | `git diff --stat` touches **docs only** | no `.vb`, no `settings.json` |
| `AC-5` | `tools/checks/verify-gate.ps1 -Mode local-fast` | `GATE PASSED` |

**Commit tag:** `[no-engine-change]`. **No `settings.json` bump. No `DeribitIndicatorProject.md` §15 entry** — doc hygiene is not engine behaviour.

---

## 2. Session 2 — the `trader-tick-queue.md` archive split

### 2.1 ⭐ The measurement that motivates it — MEASURED 2026-09-09 (UTC), not estimated

| Region of [`trader-tick-queue.md`](trader-tick-queue.md) | Size | Finished |
|---|---|---|
| Whole file | **283,209 B · 455 lines** | — |
| §2 *"Not ticks — these need a build slot"* | **134,103 B · 51 rows** | ⛔ **39 rows / 102,984 B = 77 % FINISHED** |
| §2 live remainder | 31,119 B · 12 rows | — |
| §5 *"Closed — recorded so they are not re-asked"* | **25,170 B** | **100 % by its own title** |

⭐ **The archivable total is ≈128,154 B — 45 % of the file.**

⭐⭐ **The mechanism is AGE, not cell sprawl, and that is what makes the archive the right remedy here.** Finished §2 rows average **2,641 B**; live rows average **2,593 B**. **They are the same size.** ⛔ **This is the OPPOSITE of the `DeribitIndicatorProject.md` §15 case, where `CLAUDE.md` records that collapsing rows fixed the count and the file still GREW because cell CONTENT was the driver.** Do not carry that lesson across — it does not apply to this file.

### 2.2 The split

- **New file: `docs/trader-tick-queue-archive.md`.** Precedent and shape: [`history-archive.md`](history-archive.md), which serves the same role for [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md).
- **Moves:** the 39 finished §2 rows, and all of §5.
- **Stays:** §0, §0a, §1, the 12 live §2 rows, §3, §4, §6.
- ⛔ **A moved cell lands byte-identical.** No re-wording, no summarising, no trimming (T-4).

### 2.3 ⛔ The one thing that must NOT be lost

**§5 exists so closed items are not re-asked, and §1a/§1b of [`trader-tick-queue.md`](trader-tick-queue.md) exist because shipped work kept being offered as available.** **A finished row that vanishes entirely defeats both.**

⭐ **The fix: the live file keeps a ONE-LINE index row per archived item** — title · commit · `→ archive`. **Full cell moves; the pointer stays.** At ~110 B per line against a ~2,641 B cell that is **~4 % of the byte cost for 100 % of the anti-re-raise function.**

### 2.4 Header requirements

**On `docs/trader-tick-queue-archive.md`:**
- ⛔ **THIS FILE IS A RECORD. NOTHING IN IT IS AVAILABLE WORK.**
- The UTC split date and the commit it was split at.

**On [`trader-tick-queue.md`](trader-tick-queue.md) §0:**
- A row naming the archive and what it is authoritative for — **history only, never current state.**
- ⚠ **§0 already scopes what every other doc is authoritative FOR. The archive must be added there, or the next orchestrator will not know it exists.**

### 2.5 Acceptance for session 2

| # | Check | Expected |
|---|---|---|
| `AC-6` | Every archived cell appears byte-identical in the archive | **exact match** (T-4) |
| `AC-7` | Live file retains **12** live §2 rows plus one index line per archived item | count matches §2.1 |
| `AC-8` | Every markdown link that pointed INTO a moved row still resolves | **no broken links** |
| `AC-9` | Mojibake scan on **the two queue files by name only** — `trader-tick-queue.md` and `trader-tick-queue-archive.md`. ⛔ **Never glob `docs/*.md`; see `AC-1`** | **0 on both** (T-1) |
| `AC-10` | Live file byte size | **≈155,000 B**, down from 283,209 B |

---

## 3. ✅ D-table — TICKED 2026-09-09 (UTC). Session 2 is BUILD-AUTHORIZED

> ## ✅✅ `D-1` RULED **(a)** — 2026-09-09 (UTC), trader.
>
> **The live [`trader-tick-queue.md`](trader-tick-queue.md) keeps a ONE-LINE INDEX ROW per archived item — title · commit · `→ archive`.** The full cell moves to `docs/trader-tick-queue-archive.md`.
>
> ⛔ **Session 2 is now BUILD-AUTHORIZED. Nothing else is owed on this spec.**
>
> ⚠ **Write the BUILT banner on THIS document the moment session 2 ships** — this spec exists because that step was skipped. **Do not leave it for a sweep.**

### 3.1 The decision as put

| # | Question | Options | My read |
|---|---|---|---|
| **`D-1`** | What does the live [`trader-tick-queue.md`](trader-tick-queue.md) keep for an archived item? | **(a)** a one-line index row — title, commit, `→ archive` · **(b)** nothing; the archive is the only record · **(c)** keep the full row and archive a copy | ⭐ **(a).** **(b) re-opens the exact defect §5 was built to close** — an item nobody can see gets re-raised, and this repo has done that at least three times. **(c)** saves nothing and creates a second copy that will drift, which is the multi-copy class the `A54a` arc spent two sessions removing |

⚠ **Session 1 needs no tick. Start it whether or not `D-1` is answered.**

---

## 3a. ⭐ Answers to the session-1 implementer's three questions — 2026-09-09 (UTC)

**Both of your corrections are ACCEPTED. I re-ran them in the tree rather than take them on report, and both hold.**

| Your finding | My independent check | Verdict |
|---|---|---|
| `DS-2`'s `1ad7d6d` is a false match | `git show --stat 1ad7d6d` → `refactor(indicators): R-2 residual — extract ApplySpread`, touching `Core/Indicators_OrderFlow.vb`. **That is the `S2-2` spread seam.** `cc44e9f` touches `Core/Settings/EngineSettings.vb` | ✅ **You are right** |
| `A56b` is not the value-copy guard | `A56b_CoveredStoreReturnsTailOnlyAndA48dHolds` — a trade-store fixture. **The whole `A56a`–`A56g` family in the tree is trade-store / hole-detection** | ✅ **You are right, and it is worse than one ID — see §1.4** |

### `Q-1` — `DS-7` split banner: ⭐ **YES. Write it.**

**Your proposed shape is correct.** Three additions, all in §1.4 of this document:

- **Cite `3a89093` and name the fixture family `A62a`–`A62g`.** ⛔ **Say explicitly that the guard did NOT ship as the planned `A56b`** — otherwise the doc's own §5 fixture table keeps pointing at other people's fixtures.
- **Record the `A56` collision inside the banner.** ⚠ **The next reader will otherwise repeat exactly the mistake this spec's author made.**
- **The deferred half:** ✅ **I verified it independently — `tools/WsTradeProbe/WsTradeProbeProgram.vb` still parses independently, no shared-reader call.** Cite the `S-1` ruling, (a) as direction, **NOT NOW**, 2026-09-07.

### `Q-2` — session 2: ✅✅ **UNBLOCKED. `D-1` TICKED (a) by the trader, 2026-09-09 (UTC).**

⭐ **Keep a one-line index row per archived item — title · commit · `→ archive`.** Full cell moves to `docs/trader-tick-queue-archive.md`. **Session 2 is BUILD-AUTHORIZED; see §3.** ⚠ **Still run session 1 to completion first — the order is a dependency.**

*(This row read "HOLD, do not start it" until the tick landed. Kept per the quote-and-label convention.)*

### `Q-3` — commit session 1: ✅ **YES, commit now.**

**I verified your nine edits in the shared working tree before saying so, rather than taking the gate result on report:**

- **`AC-1`** — mojibake scan over your nine files: **0 on every one.**
- **`AC-2`** — un-struck authorization strings: **9 of 10 now return `0`.** `value-copy-guard-implementer-brief.md` still returns `1`, correctly, because you left `DS-7` for this ruling.
- **Tag `[no-engine-change]`.** No `settings.json` bump, no [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 entry.

⭐ **Commit `DS-7` separately once you have written the split banner** — it carries a finding, not just a status fix, and it deserves its own message.

---

## 4. What I did NOT verify

- **I did not run the harness or build the solution.** Harness 349 is carried from [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 and [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §3.
- **I did not read all 359 files in `docs/`.** The §1.1 list comes from a heuristic scan of the first 8 lines of each, then a per-doc tree check on the recent hits. ⚠ **Older docs — `APPROVED 2026-04-29` and similar — were deliberately excluded as too obviously historical to mislead. That judgment is not proven; it is a judgment.**
- **I did not verify that the 12 live §2 rows are genuinely live.** They were classified by marker text, not by a tree read on each.
- **I did not check whether any archived row is referenced from [`roadmap.md`](roadmap.md) or [`backlog-dependency-map.md`](backlog-dependency-map.md).** `AC-8` covers links inside the queue only. ⚠ **Session 2 should widen that grep.**
