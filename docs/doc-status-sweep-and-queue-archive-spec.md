# Doc-status sweep + `trader-tick-queue.md` archive split — implementation spec

**Status:** ⛔ **DRAFT — NOT BUILD-AUTHORIZED.** §3's D-table needs one trader tick before session 2 starts. **Session 1 needs no tick and can start immediately.**

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
| **`DS-2`** | [`a54a-r2-r3-followup-spec.md`](a54a-r2-r3-followup-spec.md) | `BUILD-AUTHORIZED` | `git log --oneline --grep='R-2 residual' -i` → `1ad7d6d` | `1ad7d6d` |
| **`DS-3`** | [`coverage-trailing-edge-f1-proposal.md`](coverage-trailing-edge-f1-proposal.md) | `BUILD-AUTHORIZED 2026-08-25` | queue §2 row records `DONE 2026-08-26` | see §1.3 |
| **`DS-4`** | [`s2-2-calcspread-split-proposal.md`](s2-2-calcspread-split-proposal.md) | `BUILD-AUTHORIZED 2026-09-06` | `grep -rq 'CalcSpreadBps' --include=*.vb .` | `57b55f9` + `9e418e0` |
| **`DS-5`** | [`trade-store-downtime-repair-proposal.md`](trade-store-downtime-repair-proposal.md) | *"Part A is authorised and ready to hand to an implementer"* | `grep -rn 'Hole-derived repair windows' --include=*.vb .` → `Core/TradeStoreWriter.vb:699` | `c6c6942` |
| **`DS-6`** | [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) | *"DR-2 and DR-3 are ready to hand over as written"* | `grep -rq 'MinHoleMs' --include=*.vb .` and `git log --oneline --grep='DR-3'` → `5346bc0` | `5346bc0` |
| **`DS-7`** | [`value-copy-guard-implementer-brief.md`](value-copy-guard-implementer-brief.md) | `BUILD-AUTHORIZED` | `grep -q 'A56b' verify/ordercheck/Program.vb` | see §1.3 |
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

### 1.4 Acceptance for session 1

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

## 3. ⛔ D-table — ONE decision, needed before session 2 only

| # | Question | Options | My read |
|---|---|---|---|
| **`D-1`** | What does the live [`trader-tick-queue.md`](trader-tick-queue.md) keep for an archived item? | **(a)** a one-line index row — title, commit, `→ archive` · **(b)** nothing; the archive is the only record · **(c)** keep the full row and archive a copy | ⭐ **(a).** **(b) re-opens the exact defect §5 was built to close** — an item nobody can see gets re-raised, and this repo has done that at least three times. **(c)** saves nothing and creates a second copy that will drift, which is the multi-copy class the `A54a` arc spent two sessions removing |

⚠ **Session 1 needs no tick. Start it whether or not `D-1` is answered.**

---

## 4. What I did NOT verify

- **I did not run the harness or build the solution.** Harness 349 is carried from [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15 and [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §3.
- **I did not read all 359 files in `docs/`.** The §1.1 list comes from a heuristic scan of the first 8 lines of each, then a per-doc tree check on the recent hits. ⚠ **Older docs — `APPROVED 2026-04-29` and similar — were deliberately excluded as too obviously historical to mislead. That judgment is not proven; it is a judgment.**
- **I did not verify that the 12 live §2 rows are genuinely live.** They were classified by marker text, not by a tree read on each.
- **I did not check whether any archived row is referenced from [`roadmap.md`](roadmap.md) or [`backlog-dependency-map.md`](backlog-dependency-map.md).** `AC-8` covers links inside the queue only. ⚠ **Session 2 should widen that grep.**
