# Seat handover — 2026-09-10 (UTC)

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
- ⚠ **The 2026-09-05 DEGRADED event — STILL unchecked across FIVE handovers.** 32 minutes; nobody has verified whether it cost tape. **Verify from `trade_seq` completeness, NOT the repair log.**
- **Absorption** — date-gated, `D-2` read ~2026-09-15.
- **`S-1`** — ruled (a) as direction, **NOT NOW**. Nothing is owed.
- **Fills-import** — LATER. **S0 `--verify-venue`** — SUSPENDED.
- ⛔ **FIVE RIDERS that cannot travel alone** — they attach to the next `analysis_log.csv` header rotation, and the standing rule is **NEVER FORCE ONE**.
- ⚠ **Pre-flight `P-5`'s duplicate-`(InstanceId, SignalId)` half is uncovered by ruling.** That scan stays manual.

✅ **Verified this session, not carried: Next free fixture family `A72`** (`A71` is the high-water mark). **Next free hard constraint `HC29`** (`HC28` is the high-water mark).

---

## 8. ⭐ The lessons — the count IS the finding, again

### 8.1 ⛔⛔ TRAP `T-3` FIRED FOUR TIMES IN ONE ARC — TWICE ON THE AUTHOR OF THE RULE

**`T-3` is a named trap in [`doc-status-sweep-and-queue-archive-spec.md`](doc-status-sweep-and-queue-archive-spec.md) §0.1 — *"counting a NAME is not testing a property."*** ⛔ **Its author wrote it into the spec and then broke it twice in that same spec.**

| # | The miss | What it actually matched |
|---|---|---|
| **1** | `DS-2`'s handle `git log --grep='R-2 residual'` → `1ad7d6d` | **The `S2-2` spread seam.** `R-2` names two different findings. Correct: `cc44e9f` |
| **2** | `DS-7`'s handle `grep -q 'A56b'` | **A trade-store fixture.** The guard shipped as `A62a`–`A62g` |
| **3** | *"is `HC29` used?"* — a grep found it in four files | **Every hit was a sentence declaring it FREE.** `HC29` is free; a false defect was nearly reported |
| **4** | ⭐⭐ **A gate run reported `FAIL: 1`** | **The instrument's own echo line, written into the file it was measuring.** The harness said `ALL PASS`. **349 PASS, 0 FAIL** |

⭐⭐ **Instance 4 is the purest of the class and the one to remember: the measurement contaminated its own input.** ⛔ **Findings 1 and 2 were caught by the IMPLEMENTER, not by their author.** **The rule does not protect you; the execution does.**

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

## 11. Collector health — CARRIED AND STALE, re-read before quoting

⛔ **NOT read this session.** The last real read was **2026-09-09 14:05 UTC** ([`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §9): one process, live `analysis_log.csv` at 7,250 rows, `.bak` closed at 33,911, **`unparsed=0` on both**, settings v68.

⚠ **That is over a day old. Re-read it before quoting any figure.**

⚠ **Accrual was running well below the ~13–14 weekday STRONG per day model: 09-07 = 13 · 09-08 = 5 · 09-09 = 3 at 14:04 UTC. Do not quote the model as current.**
