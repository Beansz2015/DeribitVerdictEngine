# Seat handover — 2026-09-10 (UTC)

> ## ⛔ SUPERSEDED FOR STATE 2026-09-11 (UTC) — read [`seat-handover-2026-09-11.md`](seat-handover-2026-09-11.md) FIRST.
>
> ⭐ **What still BINDS in this document, and is not repeated in the successor:** **§11.1a–§11.1d** (the segmented memory read, the three-band model, and the **attached-probe confound** that is still unresolved) and **§8.1**'s lesson table. **Open it for those.**
>
> ⛔ **What is STALE here — corrected 2026-09-11, my first pass over-flagged it:** **§2 FIRST TASKS** and **§7 What is OPEN**. **38 commits landed after this document**, including the whole coverage/venue arc.
>
> ⛔⛔ **§7 IS THE ONE THAT CAN MISLEAD A GREP: its §7.1 opens with *"TWO VERIFIED DEFECTS"* and BOTH have shipped.** A marker sits at §7 itself, because a banner at the top protects a top-down reader and **does NOT protect someone who lands mid-document from a search — which is how every stale-row incident in this project has actually happened.**
>
> ✅ **NOT stale, and I wrongly listed them as such in the first pass: §3** (*"What shipped — 8 commits"*) **is a TRUE historical record of that session and does not rot**, and **§6** (*"every recent spec is BUILT"*) **is still true, merely incomplete.** ⚠ **Flagging an accurate record as stale teaches a reader to distrust it — that is its own defect.**

**Read after** `CLAUDE.md`'s session-start protocol and [`trader-tick-queue.md`](trader-tick-queue.md) §0a. **This is the STATE read.**

**Prior handover: [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md)** — superseded for STATE. That document's §4 Kelly arithmetic, its §5 pooled-book warning and its §8 lessons still bind.

**Settings: v68**, unchanged all session. ⛔ **Run `git status -sb` — never inherit a push state from this line.** At close it read **clean, 0 unpushed** (8 commits pushed `74aeb60..70e4dde`).

---

## 0. ⛔⛔ READ THIS BEFORE YOUR FIRST REPLY — the trader's output format

⛔⛔ **THE TRADER'S REPLY-FORMAT RULES KEEP DYING IN HANDOVERS. Carry this section forward verbatim into the next one.**

**They live in `C:\Users\user\.claude\CLAUDE.md` under *"Output format — applies to every reply, in every project"*. READ THEM THERE.** Also recorded as the memory `feedback-output-format-is-a-standing-rule`, because **memory loads automatically and a handover does not**.

**The short form, so you cannot claim you did not know:**

- ⭐ **Point form and tables. Not paragraphs.** Prose blocks stay to two or three sentences. **A table whenever you compare more than two things.**
- ⛔ **NEVER a bare section number.** Write `` `docs/trader-tick-queue.md` §0a ``, never `§0a`. **Repeat the document name on later mentions in the same reply.**
- ⛔ **NEVER a bare ID.** Give the source, the kind and the meaning on first use **in every reply** — `` `DS-7` (a finding row in `docs/doc-status-sweep-and-queue-archive-spec.md` §1.1) ``, never `DS-7`.
- **Simplified technical English.** One instruction per sentence. Active voice. ~20 words. ⚠ **Sentence STRUCTURE only — keep every domain term.**
- **Model + effort on its own labelled line.**
- ⭐ **Separate what you verified from what you carried.**

⛔ **Before sending: scan for `§` and for capital-letter IDs. Each must carry its document or its source.**

---

## 0a. ⭐⭐ READ THIS BEFORE YOUR FIRST DECISION — the default has FLIPPED

⛔⛔ **ADDED 2026-09-11 (UTC), after this handover was written. A STANDING RULING CHANGED HOW YOU WORK. It is in `CLAUDE.md` under *"AUTO-PROCEED ON YOUR OWN RECOMMENDATION"* — READ IT THERE, this is only the pointer.**

**The short form:**

- ⭐ **When a decision has an option you can recommend, TAKE IT.** Record it and keep working. **Do not stop and ask.** This covers D-tables and one-off decisions alike.
- ⛔ **The gate is REVERSIBILITY AND BLAST RADIUS — explicitly NOT *"can I recommend"*.** You can almost always produce a recommendation, so that test filters nothing, and it is self-assessed.
- ⛔ **SIX classes are RESERVED and still go to the trader every time:** `settings.json` changes · **anything that affects SCORING** · anything that moves a rendered value · writes to the live collector or trade store · schema and CSV-header changes · **any decision where your own pick is the CHEAPER or LESS-INFORMATION option.**
- ⚠ **Scoring and rendered-value are DISTINCT sets — neither contains the other.** `VerdictContext` and Kelly are rendered with zero scoring impact; a scoring threshold moved where no current row crosses it renders identically and is still a live scoring change. ⛔ **A scoring change that is invisible today is exactly the one that must not auto-proceed** — a code revert does not un-write the `analysis_log.csv` rows logged under it.
- ⭐⭐ **The measured bias, and why the last reserved class exists: 39 D-tables ticked *"as recommended"* against 8 overruled — but the eight are not random.** Four read in full all share one shape: **the recommendation optimised for ECONOMY, the ruling optimised for NOT LOSING INFORMATION.** **When you notice your pick is the cheaper one, that is the signal to ask.**
- **Obligation attached: log every auto-proceeded decision in ONE line** — decision, options, pick, why — in the spec's D-table or the batch summary.

⚠ **This ruling post-dates everything else in this document. Where an older section says a decision "needs a trader tick", re-read it against the six reserved classes above.**

✅ **Already re-read for you — the ONE tick actually pending is STILL RESERVED, so nothing changes for it:** the **Absorption mechanism revision (Path B), §6**. It unblocks the *"Absorption Step-2 penalty wire-in"*, which is **a scoring change** — reserved class 2. ⛔ **It stays with the trader. Do not auto-proceed on it, and do not open an implementer session until it is ticked.** ⚠ **It has been outstanding since 2026-08-14 — about four weeks — so it is worth raising rather than inheriting silently.**

---

## 1. ⛔ THE CLOCK TRAP HAS NOW FIRED IN EIGHT CONSECUTIVE SESSIONS

⛔⛔ **The workstation is GMT+8. The harness announces the LOCAL date. All project dates are UTC.**

**This session it fired twice more, both caught before anything was written:**
- The harness announced **2026-09-10** when UTC was 2026-09-09 17:56.
- The harness announced **2026-09-11** when UTC was 2026-09-10 16:17.

⛔ **RUN `date -u`. EVERY TIME. Knowing about the trap does not prevent it — eight sessions prove that.** ⭐ **What DID work: running `date -u` as the very first tool call of the session, before reading anything.** Do that.

---

## 2. ⭐ FIRST TASKS

| Pick | Item | Notes |
|---|---|---|
| ⭐ **1st** | **Nothing is owed and no build slot is open.** | ⛔ **Verify before offering work** — read [`trader-tick-queue.md`](trader-tick-queue.md) §0a, then §2's **12 live rows**. Everything else in §2 is now a one-line index pointing at the archive |
| **2nd** | **THREE verified defects are ready to spec** — see §7.1 of this document | ⭐ **`RM-1` is the biggest: [`roadmap.md`](roadmap.md)'s *"Ready to build"* list offers five items and FOUR have shipped.** All three found by tree-checking a carried claim. None is urgent |

⛔ **Do NOT offer any recent spec as work. ALL are BUILT** — see §6 of this document.

---

## 3. What shipped — 8 commits, `settings.json` in NONE of them

| Item | Commit |
|---|---|
| `WD-SEMANTICS` + `S-4` queue rows marked BUILT; the sweep specced | `c90df3b` |
| Two bad handles corrected, `DS-7` ruled, implementer answered | `62631dd` |
| ⭐ **`CLAUDE.md`'s `A56b` ID collision fixed**; `D-1` ticked (a) | `2f46679` |
| Nine stale-status specs marked BUILT | `6cc4d8f` |
| `DS-7` split banner | `e8ea9af` |
| Two tidy fixes on the session-1 banners | `71aa14c` |
| ⭐⭐ **The queue archive split** | `08c04a1` |
| Two independently-stale claims in the queue's own prose | `70e4dde` |

**Harness 349 throughout — unchanged, as expected for doc-only work.** `GATE PASSED` on every run including the pre-push hook. **Settings v68 untouched.**

---

## 4. ⭐⭐ WHAT THIS ARC ACTUALLY FIXED — the state read was offering shipped work

⛔ **Twelve documents told a reader that already-built work was available.** The trigger was one row; the sweep found eleven more.

| Surface | Was | Now |
|---|---|---|
| `WD-SEMANTICS` row, [`trader-tick-queue.md`](trader-tick-queue.md) §0a **and** §2 | *"READY TO HAND OVER"* | BUILT `ab5600f` |
| `S-4` row, [`trader-tick-queue.md`](trader-tick-queue.md) §2 | *"NOW A BUILD SLOT"* | BUILT `1aeae5a` |
| **Ten spec/brief headers** (`DS-1`…`DS-10`) | `BUILD-AUTHORIZED` / *"ready to hand over"* | ✅✅ BUILT banners, each with a runnable handle |

⭐ **`DS-7` is the one that is genuinely HALF built and now says so** — [`value-copy-guard-implementer-brief.md`](value-copy-guard-implementer-brief.md). Guard half shipped `3a89093`; **probe-parse-site half DEFERRED** per the `S-1` ruling (a) as direction, NOT NOW. **Verified: `tools/WsTradeProbe/WsTradeProbeProgram.vb` still parses independently.**

### 4.1 ⛔⛔ The `A56b` ID collision — now fixed in `CLAUDE.md`

**`CLAUDE.md`'s fixture-literal provenance rule cited *"the value-copy guard (A56b)"*. That was an ID collision, not a typo:**

| Meaning | Where |
|---|---|
| `A56b` = "the guard itself" | **PLANNED** in [`value-copy-guard-implementer-brief.md`](value-copy-guard-implementer-brief.md) §5 |
| `A56a`–`A56g` = trade-store hole detection | **WHAT THE TREE ACTUALLY HOLDS** (`A56b_CoveredStoreReturnsTailOnlyAndA48dHolds`) |
| `A62a`–`A62g` = `WalkPocoVsJson` | **WHERE THE GUARD REALLY SHIPPED**, commit `3a89093` |

⭐⭐ **The generalisable lesson, now written into `CLAUDE.md`: a fixture ID quoted from a SPEC is a PLAN. Only the tree says what shipped. Check the harness before quoting a fixture number in a rule.**

---

## 5. ⭐⭐ The queue archive split — measured, not estimated

| | Before | After |
|---|---|---|
| [`trader-tick-queue.md`](trader-tick-queue.md) | **284,401 B** | **160,383 B** — a **44 % cut** |
| [`trader-tick-queue-archive.md`](trader-tick-queue-archive.md) | — | **133,814 B** |

**What moved out of [`trader-tick-queue.md`](trader-tick-queue.md):** its 39 finished §2 rows, and all of its former §5. **What stayed in [`trader-tick-queue.md`](trader-tick-queue.md):** §0 · §0a · §1 · **12 live §2 rows** · §3 · §4 · §6.

⭐ **`D-1` was ruled (a) by the trader: the live file keeps a ONE-LINE INDEX ROW per archived item** — title · commit · `→ archive`. **35 index entries for 39 rows**, because 4 items each hold a BUILT row plus its struck superseded original under the quote-and-label convention. **One index line per ITEM, which is what (a) specified.**

✅ **Verified by me, not carried:** all **39 archived rows are BYTE-IDENTICAL** to their pre-split originals (`grep -qxF` against `08c04a1^`, 0 mismatches). **Nothing was deleted, so any misclassification is one revert away.**

⭐ **Why the archive was the right remedy here, and this reasoning does NOT transfer:** [`trader-tick-queue.md`](trader-tick-queue.md) §2's finished rows averaged **2,641 B**, its live rows **2,593 B** — **the same size.** The driver was AGE, not cell sprawl. ⛔ **`docs/DeribitIndicatorProject.md` §15 is the opposite case** — `CLAUDE.md` records that collapsing rows there fixed the count and the file still GREW, because cell CONTENT was the driver. **Do not carry this arc's conclusion to that file.**

---

## 6. ⛔ Every recent spec is BUILT — none is available work

| Spec | Shipped |
|---|---|
| [`wd-semantics-unparsed-counter-spec.md`](wd-semantics-unparsed-counter-spec.md) | `ab5600f` |
| [`s4-eval-cache-identity-proposal.md`](s4-eval-cache-identity-proposal.md) | `1aeae5a` |
| [`doc-status-sweep-and-queue-archive-spec.md`](doc-status-sweep-and-queue-archive-spec.md) | **BOTH sessions** — `6cc4d8f` · `e8ea9af` · `71aa14c` · `08c04a1` · `70e4dde` |
| The other ten (`DS-1`…`DS-10`) | Each carries its own BUILT banner naming its commit |

⛔ **Every one of these now opens with *"THIS DOCUMENT IS A RECORD, NOT AN INSTRUCTION."*** ⭐ **Write that banner IN THE SAME COMMIT that ships the build. This entire arc exists because that step was left for a sweep.**

---

## 7. ⚠ What is OPEN

> ⛔⛔ **STALE AS OF 2026-09-11 (UTC) — DO NOT TAKE WORK FROM THIS SECTION.** **Current open items: [`seat-handover-2026-09-11.md`](seat-handover-2026-09-11.md) §6.**
>
> **Both "verified defects" in §7.1 below have SHIPPED:** `AW-1` (the atomic-writes 5-vs-6 undercount) at **`5b0f8cc`** — and there were indeed SIX sites · `RM-1` ([`roadmap.md`](roadmap.md)'s ready-list, four of five already built) at **`aa979a8`** and **`8e47715`**.
>
> ⭐ **The FINDINGS below are still worth reading — the undercount and the stale ready-list are both instructive. It is only their STATUS that has moved.**


### 7.1 ⛔ TWO VERIFIED DEFECTS, both found by tree-checking a carried claim

**`AW-1` — the atomic-writes row in [`trader-tick-queue.md`](trader-tick-queue.md) §2 says "5 sites" and then lists SIX.**

- **5 files, 6 call sites.** ⛔ **An implementer sizing at five makes five changes and misses one — and the missed one is `OhlcCache.vb:144`, the exact site the row itself flags as *"safe only via a distant early-out."***
- **Same shape as the `WD-TIDY` undercount**, where the row's own count was the finding.

**`AW-2` — four of the six anchors have drifted since they were set 2026-08-07.** ✅ **Corrected, verified 2026-09-10 (UTC):**

| File | Row's anchor | Actual |
|---|---|---|
| `OhlcCache.vb` | 99 | **99** ✅ |
| `OhlcCache.vb` | 134 | **144** ⚠ |
| `Core/SignalEmitter.vb` | 563 | **574** ⚠ |
| `Core/Settings/SettingsLoader.vb` | 346 | **358** ⚠ |
| `tools/AutoTweaker/SettingsDiffApplier.vb` | 352 | **362** ⚠ |
| `tools/AutoTweaker/TweakerState.vb` | 162 | **162** ✅ |

⚠ **The WORK is unaffected — every `File.Replace` call still exists. Only the line numbers rotted.** Handle: `grep -rn 'File\.Replace(' --include=*.vb . | grep -v '/obj/'` → exactly 6.

**`RM-1` — ⛔⛔ [`roadmap.md`](roadmap.md)'s "Ready to build" list is FOUR-FIFTHS STALE. Found by the session-2 implementer while widening the link check; verified by me 2026-09-10 (UTC).**

**Both `roadmap.md` line 203 and line 211 name five items as *"Ready to build, no decision needed"*:**

| Item | Real state |
|---|---|
| the three weekday filters | ⛔ **SHIPPED 2026-09-07** |
| the atomic-write total-primitive swap | ✅ **genuinely open** — the only one |
| `C1-coverage F1` | ⛔ **SHIPPED 2026-08-26, `4032f9c`** |
| `G12` | ⛔ **SHIPPED 2026-09-07** |
| the CeilingAudit expected-version constant | ⛔ **SHIPPED 2026-08-25** |

⛔ **[`roadmap.md`](roadmap.md) is the EXECUTION-ORDER authority — `CLAUDE.md` says so — and four of the five things it offers are done.** ⚠ **Worse: its own line 205 records a *"CORRECTED 2026-08-12"* note about that very list, and `C1-coverage F1` survived the correction and then shipped two weeks later.** ⭐ **Same defect class this whole arc removed from the spec headers and the queue, now found in two more documents — [`roadmap.md`](roadmap.md) and [`backlog-dependency-map.md`](backlog-dependency-map.md).** **Deliberately NOT fixed: it is outside the sweep spec's scope and wants its own short pass.**

### 7.2 Carried from [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §7 — unchanged

- **Coverage-report cluster — 3 rows, all still LIVE in [`trader-tick-queue.md`](trader-tick-queue.md) §2:** `gapMs` as a TIME tolerance · an up-interval starting at the `DOWN` line · intentional-downtime scoping. All need short specs.
- **`ws_health.log` under-reports an outage** — it and the tape disagree by ~34 minutes.
- ✅✅ **The 2026-09-05 DEGRADED event — CLOSED 2026-09-10 (UTC). It cost ZERO tape.** `TradeSeq` is contiguous across the hole (delta 1), five controls show 0 missing, and the day was a Saturday. **Full measurement in §11.2a of this document. Do not re-raise it.** ⭐ **No `fetch` was needed — the data was already in `AWS-copybacks/aws-copyback-2026-09-06/`.**
- **Absorption** — date-gated, `D-2` read ~2026-09-15.
- **`S-1`** — ruled (a) as direction, **NOT NOW**. Nothing is owed.
- **Fills-import** — LATER. **S0 `--verify-venue`** — SUSPENDED.
- ⛔ **FIVE RIDERS that cannot travel alone** — they attach to the next `analysis_log.csv` header rotation, and the standing rule is **NEVER FORCE ONE**.
- ⚠ **Pre-flight `P-5`'s duplicate-`(InstanceId, SignalId)` half is uncovered by ruling.** That scan stays manual.

✅ **Verified this session, not carried: Next free fixture family `A72`** (`A71` is the high-water mark). **Next free hard constraint `HC29`** (`HC28` is the high-water mark).

---

## 8. ⭐ The lessons — the count IS the finding, again

### 8.1 ⛔⛔ TRAP `T-3` FIRED **SEVEN** TIMES IN ONE ARC — TWICE ON THE AUTHOR OF THE RULE, AND **FOUR TIMES THE INSTRUMENT ITSELF WAS THE FAULT**

**`T-3` is a named trap in [`doc-status-sweep-and-queue-archive-spec.md`](doc-status-sweep-and-queue-archive-spec.md) §0.1 — *"counting a NAME is not testing a property."*** ⛔ **Its author wrote it into the spec and then broke it twice in that same spec.**

| # | The miss | What it actually matched |
|---|---|---|
| **1** | `DS-2`'s handle `git log --grep='R-2 residual'` → `1ad7d6d` | **The `S2-2` spread seam.** `R-2` names two different findings. Correct: `cc44e9f` |
| **2** | `DS-7`'s handle `grep -q 'A56b'` | **A trade-store fixture.** The guard shipped as `A62a`–`A62g` |
| **3** | *"is `HC29` used?"* — a grep found it in four files | **Every hit was a sentence declaring it FREE.** `HC29` is free; a false defect was nearly reported |
| **4** | ⭐⭐ **A gate run reported `FAIL: 1`** | **The instrument's own echo line, written into the file it was measuring.** The harness said `ALL PASS`. **349 PASS, 0 FAIL** |

| **5** | *"the implementer's handle is NOT in the file"* | ⛔ **It was.** A `cut -c1-420` read truncated before it. **RETRACTED — see §8.4 of this document** |
| **6** | *"the box is thrashing at hour 18"* | ⚠ **UNRESOLVED, not wrong.** The bursting sample is SSM-**attached**; both zero-burst baselines are **detached**, and an attached session costs this box 30–50 MB. **See §11.1d of this document** |
| **7** | *"four markdown tables have broken pipe counts"* | ⛔ **None did.** The checker counted ALL pipes; a properly escaped `` `\|` `` is valid cell content. **The memory `feedback-markdown-tables-do-not-protect-pipes` predicts this exact false positive — *"a raw count blames the wrong row."*** |

⭐⭐ **Instances 4, 5 and 7 are the purest of the class and the ones to remember: FOUR of the seven misses were the INSTRUMENT, not the subject.** **A gate log polluted by its own echo · a truncated read reporting absence · a raw pipe count flagging correct rows · and an attached probe measuring its own cost.** ⛔ **Before reporting a defect, ask what the instrument would say if there were NO defect.** ⛔ **Findings 1 and 2 were caught by the IMPLEMENTER, not by their author.** **The rule does not protect you; the execution does.**

### 8.2 ⭐⭐ The escalation trigger EARNED ITS PLACE — it fired and it was right

**The spec told the implementer to stop if any LIVE row referenced a row being archived. It did, and they stopped before touching content.** They found **twelve** dangling pointers. ⭐ **Two carried a bonus finding — [`trader-tick-queue.md`](trader-tick-queue.md) §0a describing already-shipped work as open, the session-1 defect class inside the queue's own prose.**

⭐ **Ruling given: retarget the pointer, never reword the claim** — mechanical, greppable, convention-safe. **The two stale claims were ruled IN scope as their own commit (`70e4dde`): leaving known-stale text in the state read after finding it is what this arc spent five commits removing.**

⭐⭐ **And the implementer improved on the ruling.** On the one ambiguous pointer, they saw the sentence had **two** referents — one live, one archived — and split it, instead of retargeting generically as instructed. **That is better than what I told them to do.**

### 8.3 ⭐ An independent CLASSIFICATION beat the spec's own numbers

**The implementer's 39/12 split matched the spec's count, and they said plainly it was an independent classification but NOT a structurally independent method — so a shared misreading was not ruled out.** ⭐ **That is the right way to report a match.**

⛔ **And their classification was BETTER than the spec author's in two places:** they read `gapMs` as LIVE (correct — it is one of the three open coverage rows) and `S2-1` as finished (correct — *"IMPLEMENTED 2026-09-05"*). **The author's classifier had both wrong.**

### 8.4 ⛔⛔ A TRUNCATED READ MANUFACTURED A FALSE FINDING — and this section originally WAS that false finding

⛔ **RETRACTED 2026-09-10 (UTC). The finding that stood here was WRONG, and the correction is the lesson.**

**What this section claimed:** that the implementer's report described an `N-2` handle — `grep -c 'Private Sub F1[a-f]_TrailingEdge'` → 6 — which *"was NOT in the file"*, and billed it as the *"honest about its input, wrong about its output"* class.

⛔ **It WAS in the file, and had been since `71aa14c`.** ✅ **Verified: `docs/coverage-trailing-edge-f1-proposal.md` line 5 carries the declaration sentence AND the runnable handle; `git log -S` puts it in `71aa14c`.**

⭐⭐ **The cause was the reviewer's own instrument: the line was read with `cut -c1-420`, and the handle sits past character 420.** **A truncated read of a long line reported ABSENCE where there was only truncation.**

⛔ **This is the FIFTH instance of the measure-the-right-thing class in this one arc, and the SECOND caused by the reviewer's own tooling** — after the gate run whose `FAIL: 1` was its own echo line (§8.1 of this document, instance 4). ⭐ **The implementer pushed back with evidence rather than accepting the correction, and was right to.**

⚠ **Practical rule: never conclude a string is ABSENT from a `cut`/`head`-truncated read. Use `grep -c` on the whole file, or `fold` the line.** These docs routinely carry 2,000-character table cells and banner lines.

### 8.4a ⭐ The genuine version of that lesson still stands

**Check the artifact, not the report — but check it with an instrument that can see the whole artifact.** The original instinct was right; only the execution was wrong.

### 8.5 ⭐ Archive for AGE; collapse for SPRAWL — and MEASURE which you have

**See §5 of this document.** The two remedies are not interchangeable, and the same-average-size measurement is what told them apart. ⛔ **A file that grew because cells got longer will not shrink by archiving rows.**

---

## 9. Kelly and `W6-4` — unchanged, carried

⛔ **CARRIED from [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §4. NOT re-read this session.**

- **Kelly trigger MET** — 407 weekday STRONG against ≥406, measured 2026-09-09 14:04 UTC.
- **Kelly CAL: the tier ladder still does NOT separate.** Pooled STRONG **47.1 %** (n=518), below the **47.76 %** breakeven.
- **`W6-4` (the ceiling audit): still INCONCLUSIVE.** NY×1 ΔAUC +0.0356, CI [−0.045, +0.114]. **Standing instruction unchanged: re-run at the next book doubling, no spend meanwhile.**
- ⭐ **The instrument is COMMITTED — `tools/ops/kelly-trigger-read.ps1`, `-Mode Box`.** It enforces the calibration and refuses to read the box if it fails. **Stop rebuilding it in a scratchpad.**

⚠ **TWO GATES ON DIFFERENT BASES — do not conflate them.** Kelly CAL is 2 × 203 **EVALUABLE**; `W6-4` is 2 × 2,712 **ELIGIBLE** rows. **"Eligible" is not a raw row count.**

---

## 10. ⛔ The frozen pooled book — do not lose it

**`AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`** · 47,682 rows · MD5 `E8418846838FF97F3C90F782A95B3523` · **gitignored, persists on disk.** ✅ **File confirmed present 2026-09-09; MD5 NOT re-verified this session.**

⛔⛔ **NEITHER READER DISCOVERS A `.bak`.** `tools/CeilingAudit/CsvFeatureBuilder.vb` and `analysis/ForwardWindowJoiner.vb` each take **ONE path**. **Point either at the live `analysis_log.csv` alone and you read 70 weekday STRONG instead of 407 — a ~90 % under-count, silently.**

⚠ **`AWS-copybacks/local-book-rescue-2026-09-09/` holds the ONLY copy of the 2026-07-03 → 07-22 rows. Still untracked and unbacked-up.**

---

## 11. Collector health — ✅ READ 2026-09-10 18:22 UTC. **This is a real read, not carried.**

**Instrument: `tools/ops/collector.ps1 status -InstanceId i-0d6c133058876273e`.** ✅ **The box was confirmed from AWS itself (`aws ec2 describe-instances`), not from a doc: ONE running instance, `i-0d6c133058876273e`, tag `DeribitEngine`, t2.micro, eu-west-2.**

| Reading | Value |
|---|---|
| Host / OS | `EC2AMAZ-P0OPH8J` · Server 2019 Datacenter · **host uptime 21d 03:02** |
| App | `C:\DeribitEngine` · exe built **2026-09-01 15:48** · **app uptime 9d 02:32** · overlay **False** |
| Settings on the box | ✅ **v68** — matches tracked |
| Live `analysis_log.csv` | **8,427 rows**, `2026-09-01 15:50:01 → 2026-09-10 18:22:04` |
| Freshness | ⭐ **gap now-to-last-row 0.3 min — collecting right now** |
| Rate | **38.6 rows/hour** (box's own figure) |
| Store | `trades_2026-07.csv` 128 KB · `trades_2026-08.csv` 123,420 KB · `trades_2026-09.csv` 65,707 KB |

### 11.1 ⛔ NEW FINDING — the box is thrashing, and its own tool says so

| Memory | Value |
|---|---|
| total / avail / in use | **1,024 MB / 124 MB / 899 MB** |
| app working set / private | 75 MB / 118 MB |
| **`pagesOUT/s` — 30 s sustained** | ⛔ **289.3 avg · 4,339.9 max** |
| pagefile | 36.0 / 36.8 % |

⛔ **`collector.ps1`'s own annotation on that line reads *"eviction pressure; sustained >0 = thrashing."* A 30-second average of 289.3/s is sustained by any reading.** ⚠ **`pagesIN/s` (921.7 avg) carries the tool's *"noisy: counts mapped-file reads too"* caveat; `pagesOUT/s` does NOT.** So the outbound figure is the meaningful one.

⚠ **Do NOT judge that against a session average.** [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §1.1a rules the comparison basis: **compare against the PER-HOUR row, never a session average** — the NY/non-NY split is itself too coarse. **This read landed at 18:22 UTC, so hour 18 is the only relevant baseline.** ✅ **Segmented read done — see §11.1a of this document.**

### 11.1a ✅ SEGMENTED MEMORY READ — done 2026-09-10 (UTC) from a SECOND detached 24 h sample

⭐ **No new instrument was built and nothing was perturbed.** The box already carries a **detached** `logman` counter set — **`RIPPerfBaseline`, currently Stopped, owned by the COLOCATED HOSTEL APP** (`C:\RedInnPricing\logs\perf\`), sampling `\Memory\Pages Output/sec`, `\Memory\Available MBytes` and `\Paging File(_Total)\% Usage` at **10 s**. ⚠ **It is another team's instrument in another team's directory — READ ONLY. Do not start, stop or reconfigure it.**

⭐⭐ **It holds TWO 24-hour runs, and [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) only ever analysed the first:**

| File | Span (UTC) | Analysed before? |
|---|---|---|
| `baseline_08241501.csv` | 2026-08-24 15:01 → 08-25 15:01 | ✅ yes — every figure in [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §1.1 |
| **`soak_08271509.csv`** | **2026-08-27 15:09 → 08-28 15:09** | ⛔ **NO — analysed here for the first time** |

**Aggregated on the box (8,640 usable samples; the first row's rate field is blank by construction and was excluded):**

| Metric | 08-24 baseline | **08-27 soak** |
|---|---|---|
| Overall burst rate | 9.10 % | ⛔ **14.20 %** |
| `availMB` min / median | 31 / 168 | **31 / 152** |
| Page-file max | 74.79 % | ⛔ **79.77 %** (hour 15) |

### 11.1b ⭐⭐ THE SHAPE REPRODUCES; THE RATES DO NOT

| Band | 08-24 | 08-27 | Verdict |
|---|---|---|---|
| **Busy, 00–12 UTC** | 15.34 % | **22.50 %** | ⛔ **+47 % — NOT stable** |
| **Near-silent, 16–23 UTC** | 0.38 % | **0.24 %** | ✅ **stable, both ≈ zero** |

⛔ **So the per-hour table that the stop conditions depend on is NOT a fixed baseline. Its busy half moved by half again in three days** — hours 13 and 14 went 5.56 % → 18.61 % / 18.89 %, a **3.3×** move, and hours 05 and 07 roughly doubled. ⚠ **Anyone judging a future reading against [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §1.1a's busy-hour numbers is comparing against one day, not a baseline. Re-measure that half.**

⛔ **Two of that document's three stop conditions are already broken by its own sibling sample:** *"availMB routine floor 55 MB"* — the soak reads **31 MB** at hours 05 and 14 and under 55 in **seven** hours; *"page-file routine max 42.9 %"* — the soak reaches **79.77 %**.

### 11.1c ⭐ HOUR 18 IS THE STABLE PART — so the 09-10 observation IS anomalous

| Hour 18 | Samples | Bursts |
|---|---|---|
| 08-24 baseline | 360 | **0** |
| 08-27 soak | 360 | **0** |
| **Combined** | **720** | ⭐⭐ **ZERO** |

⭐ **Hour 18 reads 0.00 % on two independent detached days over 720 samples. The 2026-09-10 18:22 UTC reading of `pagesOUT/s` 289.3 sustained average, 4,339.9 max, is a genuine departure from that — not a wrong-denominator artefact.** ⭐ **And it is the trustworthy half of the model: the silent band reproduced while the busy band did not.**

### 11.1d ⛔⛔ WHAT THIS STILL CANNOT SETTLE — the attached-instrument confound

⛔ **I cannot separate "the box thrashes at hour 18 now" from "my own read caused it," and neither can this data.**

- **My 09-10 reading came through an SSM-attached PowerShell session.** [`seat-handover-2026-08-25.md`](seat-handover-2026-08-25.md) §1.1 measured that cost: **an SSM-attached session costs this box 30–50 MB**, and names *"it is detached, so it does not perturb what it measures"* as the reason the `logman` collector is the better instrument.
- **Both zero-burst hour-18 samples are DETACHED. My bursting hour-18 sample is ATTACHED.** ⛔ **That is exactly the shape a measurement artefact takes.**
- ⭐ **The clean test is cheap and needs no new tooling: one DETACHED 24 h counter run of our own, then read hour 18.** ⚠ **Do not reuse `RIPPerfBaseline` for it — it is the hostel app's.**

⚠ **This is the SIXTH instance of the measure-the-right-thing class in this arc, and it is unresolved rather than wrong.** Naming it beats reporting a thrashing box on evidence that cannot distinguish the box from the probe.

⚠ **Also still unexplained: host uptime 21d against app uptime 9d.** The restart matches the exe build stamp, so it **looks** like a deploy. **Not verified.**

⚠ **Also unexplained and worth one look: host uptime 21d but app uptime 9d.** The app restarted ~2026-09-01, matching the exe build stamp — **so it looks like a deploy, not a crash. Not verified.**

### 11.2 ⭐ The 2026-09-05 DEGRADED event now has EXACT bounds

⭐ **Unchecked across FIVE handovers, always as *"~32 minutes"*. The `ws_health` tail gives it precisely:**

| Event | Window | Duration |
|---|---|---|
| **2026-09-05** | `09:04:28.214Z DEGRADED → 09:36:50.789Z OK` | ⛔ **32 min 22.6 s** |
| **2026-09-06** (not previously recorded here) | `15:43:43.049Z DEGRADED → 15:43:59.547Z OK` | ✅ **16.5 s — negligible** |

### 11.2a ✅✅ RESOLVED 2026-09-10 (UTC) — THE 2026-09-05 EVENT COST **ZERO** TAPE

⭐⭐ **The item that survived FIVE handovers is CLOSED. It cost nothing.**

⭐ **NO `fetch` WAS NEEDED — the data was already on disk.** `AWS-copybacks/aws-copyback-2026-09-06/aws_fetch/20260906-155831/backtest_data/trades_2026-09.csv` — **600,395 rows spanning 2026-09-01 00:00:01 → 2026-09-06 15:58:26**, which brackets the window. ⛔ **Check the existing copy-backs before spending on a fetch; five handovers deferred this for a fetch that was never required.**

**First, the instrument was validated — a contiguous sequence only proves something if the venue assigns it.** ✅ **`TradeSeq` is Deribit's own `trade_seq`, a per-instrument monotonic sequence read straight off the payload** — `DeribitClient.vb:344` documents it, `TradeRecord.ReadTradeSeq` reads `t.TryGetProperty("trade_seq")`, and **both capture paths use the same reader** (`DeribitClient.vb:290` REST, `DeribitWsFeed.vb:494` WebSocket). **It is not generated locally, so contiguity is evidence and not a tautology.**

**The decisive measurement:**

| | Value |
|---|---|
| Last row **before** the hole | `2026-09-05 08:59:35.333` · seq **298,507,369** |
| First row **after** the hole | `2026-09-05 09:44:45.553` · seq **298,507,370** |
| Wall-clock hole | **45.2 min** |
| ⭐⭐ **`TradeSeq` delta** | **1** |
| ⭐⭐ **Trades lost** | ✅ **ZERO** |

**Five control windows of the same length, none overlapping the event — a positive record, not a baseline derived from the behaviour being judged:**

| Control | Rows | Missing seq |
|---|---|---|
| 09-05 08:32–09:04 (just before) | 1,217 | ✅ **0** |
| 09-05 09:36–10:09 (just after) | 1,513 | ✅ **0** |
| 09-04 09:04–09:37 (Friday) | 3,922 | ✅ **0** |
| 09-03 09:04–09:37 (Thursday) | 1,725 | ✅ **0** |
| 09-02 09:04–09:37 (Wednesday) | 2,486 | ✅ **0** |

⭐ **`trade_seq` is contiguous with ZERO gaps whenever capture is healthy — so the instrument has teeth, and it says nothing was missed here either.**

### 11.2b ⚠ THREE RIDERS — each changes how the next reader should think

⛔ **1. The tape hole does NOT align with the DEGRADED window, so the event did not cause it.** Hole `08:59:35 → 09:44:45`; the `ws_health` markers are `09:04:28 → 09:36:50`. **The hole starts ~5 min BEFORE the DEGRADED marker and ends ~8 min AFTER the OK marker.** ⭐ **This is a genuine market quiet period that OVERLAPS the event, not a consequence of it.**

⭐⭐ **2. `trade_id` would have given a FALSE POSITIVE of two lost trades.** Across the same hole, **`TradeId` delta is 3 while `TradeSeq` delta is 1.** Consistent with `trade_id` being broader than one instrument while `trade_seq` is per-instrument. ⛔ **The standing instruction — *"verify from `trade_seq` completeness, NOT the repair log"* — was RIGHT, and `trade_id` is also the wrong instrument. Use `trade_seq`.**

⭐ **3. 2026-09-05 is a SATURDAY, and the hole is the ONLY >30 min hole in all 600,395 rows.** So the duration genuinely is anomalous — but **it is anomalous market quiet, not anomalous capture**, and a weekend hole falls outside the weekday-scoped Kelly population regardless.

⚠ **What this does NOT settle:** the separate open item that **`ws_health.log` under-reports an outage.** ⭐ **This case is evidence for that item and points the OPPOSITE way to how it is currently worded — here the tape hole is LONGER than the DEGRADED window, not shorter.** **A `ws_health` DEGRADED marker is not a reliable proxy for tape loss in either direction.**

### 11.3 Accrual — the carried model is confirmed as NOT current

- **Row accrual: 7,250 (09-09 14:05 UTC) → 8,427 (09-10 18:22 UTC) = +1,177 over 28.3 h = 41.6 rows/h.** ✅ Consistent with the box's own 38.6 rows/h.
- **Verdict mix in the live file:** `NO TRADE` 5,459 · `WEAK SHORT` 709 · `NO TRADE [WEAK LONG]` 568 · `NO TRADE [WEAK SHORT]` 553 · `WEAK LONG` 543 · `SHORT` 330 · `LONG` 162 · **`STRONG SHORT` 63 · `STRONG LONG` 29** · `NO TRADE [TIE]` 11.
- ⛔⛔ **DO NOT COMPARE THAT 92 STRONG AGAINST THE KELLY GATE.** **92 is ALL-DAY STRONG in the live file only. The Kelly trigger counts WEEKDAY STRONG across the pooled book (`.bak` + live), which read 407.** ⚠ **This is exactly the two-gates-on-different-bases trap in [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §4.2, where an orchestrator compared raw rows and was wrong.**
- **For the weekday-STRONG figure use the committed instrument: `tools/ops/kelly-trigger-read.ps1 -Mode Box`.** ⚠ **NOT run this session.**

### 11.4 What this read did NOT cover

- ⚠ **`unparsed=0` — NOT confirmed.** The `status` verb does not report it. The carried 2026-09-09 reading said 0 on both files.
- ⚠ **The `.bak` row count — NOT reported** by `status`. Carried figure: closed at 33,911 rows.
- ⚠ **Weekday STRONG — NOT read.** See §11.3 of this document.
