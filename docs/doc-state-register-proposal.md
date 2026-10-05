# Doc state register — proposal

**Status:** PROPOSAL, 2026-10-03 (UTC). Trader-requested 2026-10-02: *"Are there better ways to organize all of the .md files … Being stale is an issue."* Nothing is built. The decisions for the trader are in §8 (`DSR-1`–`DSR-8`; the prefix was checked free in `docs/`, `Core/`, `tools/`, `verify/` on 2026-10-03).

**Measured on:** the working tree at `0af9025`, before this proposal was written. Census script and its saved output: `docs/audits/proofs/doc-state-census-2026-10-03/` (see §2).

---

## 0. Model + effort for the build

| Phase (§7) | Model + effort | Why that tier |
|---|---|---|
| P1 — register, schema, gate checks C1/C2/C5, seed | **Opus 5.5, medium** for the build; **the seat, high** for the seed | The build is a JSONL reader and three PowerShell checks with in-repo templates (`tools/checks/rotation-riders.ps1`, the `docs/outstanding.json` check). The seed is judgment: each "open" row must be read in its own D-table, because status prose is the thing that rots |
| P2 — stale-claim check C3, collision check C4 | **Opus 5.5, high** | Scoping C3 to changed lines without false FAILs on 555 legacy files is the hard part |
| P3 — generated views | **Opus 5.5, medium** | Mechanical once P1 holds |
| P4 — frontmatter, Obsidian | seat, low | Optional |

**Where a build will slip:**
- **Treating a bare ID as unique.** `D-2` keys rows in 27 docs (§2). Every lookup must use the full key `<doc-stem>#<id>`. A fixture that seeds one `D-2` cannot catch this; seed two docs with the same local ID.
- **C3 fires on history.** A line that quotes an old "owed" inside a dated addendum is history, not a claim. Scope C3 to added lines in the push range, and test it on a doc that has a struck-through row.
- **Writing the seed from status prose.** The seed is exactly where a stale row would be copied into the register and become "truth".

**Escalate (move up a tier, or stop and ask) when:** a check needs a natural-language judgement to decide (that is harness 4's job, not a gate's); or the seed finds a row whose ruling state the D-table itself does not settle.

---

## 1. The problem

The docs go stale because **one state fact is restated in prose in many places**, and only some copies are updated. The format is not the cause. Obsidian, HTML or a database holding the same prose would go stale the same way.

**Instances on record:**
- **2026-08-01:** 4 of 13 queue rows described shipped work as outstanding (`CLAUDE.md` Session Start item 6).
- **2026-09-07:** 11 rows in `docs/trader-tick-queue.md` §2 described finished work (commit `0767245`).
- **2026-10-02:** found while filling `docs/outstanding.json`:
  - the TRADER AWAY block in `docs/trader-tick-queue.md` called `HDS-1`–`HDS-4` (the history-store decisions) "owed BEFORE 2026-10-14". All four were ruled 2026-09-29 (`docs/history-data-store-spec.md` §6);
  - the history-host row in `docs/trader-tick-queue.md` §2 called `HH-1`–`HH-3` "queued for the trader". All three were ruled 2026-09-28.
- **The state banner of `docs/trader-tick-queue.md`** carries 9 "PREVIOUS STATE READ" blocks above the current one.
- **ID collisions:** `CLAUDE.md` records an `A56b` that named two different fixtures (a plan and the shipped tree).

## 2. Measurements (2026-10-03, working tree at `0af9025`)

⚠ **The census reads the working tree.** The saved `output.txt` was produced after this proposal existed, so it counts this doc's own rows too: 2,022 rows · 211 docs · 839 IDs · 268 ruling markers. The collision count (291) and the open-marker count (68) are the same in both runs. The table below is the run before this doc existed.

| Quantity | Value | How measured |
|---|---|---|
| `.md` files under `docs/` | 555 (485 top-level), 14 MB | `find`, `du` |
| Seat handovers | 29 | `ls docs/seat-handover-*.md` |
| Table rows keyed by an ID in the first cell | **2,010**, in 210 top-level docs | census script (regex) |
| Distinct IDs | 831 | census |
| **IDs that key rows in more than one doc** | **291** (1,382 doc-ID pairs) | census |
| Worst cases | `D-2` in 27 docs · `D1` in 25 · `D-1`, `D3`, `D4` in 24 each · `Q1` in 21 | census |
| Rows with a ruling marker (RULED, TICKED, AUTO-PROCEEDED, ✅) | 267 | census |
| Rows with an open marker (owed, queued, awaits, pending) and no ruling marker | **68** | census — **candidates only**, see below |
| Distinct table-header shapes | 248; the commonest is `# · decision · recommendation` (150 rows) | census |

⚠ **What the census does NOT show:**
- The regex also matches finding tables (`# · sev · defect …`, 73 rows) and criteria tables. **2,010 over-counts decisions.** I did not separate them.
- The 68 open-marker rows are **not** verified as open. Some will be stale, which is the point, but no row was read.

**What follows from it:** a bare ID is not an identifier in this repo. **A register keyed on bare IDs would merge 291 sets of unrelated decisions.**

## 3. Options

| Option | Fixes staleness? | Agents can use it? | Verdict |
|---|---|---|---|
| Obsidian | No. It is a viewer over the same `.md` | Yes (files stay `.md`) | Optional navigation aid (P4) |
| HTML | No | Worse: more tokens, noisy diffs | Generated output only, never a source |
| SQLite database | For state only | Via scripts; a binary file cannot be diffed or merged in git | Rejected as source. Allowed as a cache built from text |
| Notion / Google Docs / wiki | Partly | Needs connectors; leaves git | **Rejected:** a doc change must land in the same commit as the code change |
| GitHub Issues / Projects | Yes, for item lifecycle | Via `gh` | Rejected for now: state outside the repo, invisible offline and to `git log -S` |
| Agent memory system — [Hindsight](https://github.com/vectorize-io/hindsight) (reviewed 2026-10-03) | **No.** An LLM extracts facts at write time, and recall is ranked. Conflicting input *"strengthens, weakens or extends an existing belief"* (its README), whereas a ruling is binary | Yes, via MCP | **Rejected as a source of state.** Its entity resolution may also merge colliding IDs (`D-2` in 27 docs; inferred from the design, not tested). **Candidate as a SEARCH layer** over the narrative docs: compare it against harness 5 (the doc re-ranker, `CLAUDE.md` Session Start item 7) once harness 5's trial reaches a verdict. Costs: Docker or Postgres, plus LLM calls on every write |
| **Structured registers in git + generated views + gate checks** | **Yes** | **Yes** | **Proposed** |

## 4. Design

### 4.1 Three kinds of content

| Kind | What | Format | Rule |
|---|---|---|---|
| **State** | Is a decision open or ruled? Is an item outstanding? | Registers: `docs/registers/decisions.jsonl` (new), `docs/outstanding.json` (exists) | One home per fact |
| **Narrative** | Specs, reads, spec-backs, reviews: options, rationale, evidence | `.md`, as now | Frozen once accepted. Corrections are dated addenda (already the practice) |
| **Views** | "What is open now" | Generated `.md` under `docs/generated/` | Never edited by hand. Header line: `GENERATED by <script> at <sha> — do not edit` |

### 4.2 The decisions register — `docs/registers/decisions.jsonl`

One JSON object per line. JSONL because:
- each decision is one line, so a diff shows exactly what changed;
- two seats adding decisions rarely produce a merge conflict;
- PowerShell 5.1 parses each line with `ConvertFrom-Json` (no YAML module needed), and Python with `json`.

```json
{"key":"history-data-store-build-spec-back#D-6","id":"D-6","doc":"docs/history-data-store-build-spec-back.md","anchor":"decision table","title":"Stage-4 pass rule: count 14 days between run dates or data windows","owner":"trader","reserved":false,"status":"ruled","ruling":"(a) run dates, 14 days, no shortening","ruled_utc":"2026-10-02","ruled_by":"trader","superseded_by":null}
```

| Field | Meaning |
|---|---|
| `key` | **`<doc-stem>#<local-id>`. Unique. The only identifier the tools use.** |
| `id`, `doc`, `anchor` | The local ID as the doc writes it; the doc; where the D-table sits |
| `title` | One line: what is decided |
| `owner` | `trader` (a tick or ruling) · `seat` (auto-proceed) |
| `reserved` | `true` when it falls in a `CLAUDE.md` RESERVED class |
| `status` | `open` · `ruled` · `withdrawn` · `superseded` |
| `ruling`, `ruled_utc`, `ruled_by` | One line; date; `trader` / `seat` / `auto-proceeded` |
| `superseded_by` | The `key` of a re-ruling, e.g. a re-ruled `D-4` |

**Display rule:** a view or the pane shows `D-6 (history-store build — stage-4 pass rule)`, never a bare `D-6`. That follows the global "no bare ID" rule.

### 4.3 Where the ruling text lives (decision `DSR-3`)

**Proposed: in both places, and the gate keeps them consistent.**
- The doc's D-table row keeps the options, the read and the dated ruling, as now. **The doc stays self-describing.** A seat that reads only the spec still learns the ruling.
- The register carries the status. Checks and views read it.
- Gate check C2 (§4.5) fails when the two disagree. This makes the duplicate copy safe: drift becomes a push failure instead of something a reader must notice.

### 4.4 Relationship to `docs/outstanding.json`

- It stays as is (schema 1).
- **P3:** the `waiting_on_trader` rows with `tick: true` are generated from register rows with `status: open` and `owner: trader`. The seat then records a queued decision once, in the register, and the pane follows.

### 4.5 Gate checks — a new section in `tools/checks/verify-gate.ps1`

Logic goes in `tools/checks/doc-state.ps1`, so it is testable without a build (the `rotation-riders.ps1` precedent).

| Check | Rule | local-fast | prepush / ci |
|---|---|---|---|
| **C1** register well-formed | Every line parses; `key` unique; `doc` exists; the `id` appears in `doc` | FAIL | FAIL |
| **C2** register ↔ doc consistent | `ruled` → the doc row for that `id` carries a ruling marker; `open` → it carries none | WARN | FAIL |
| **C3** stale claim | An **added line in the push range** pairs an open-word (owed, queued, awaits, not yet ruled) with an ID that the register marks `ruled` **for that doc's key**, or for a doc the line links | WARN | FAIL |
| **C4** new collision | A new D-table row's bare `id` already exists under another doc: WARN, asking the author to cite it with its doc everywhere | WARN | WARN |
| **C5** `outstanding.json` | The check from `docs/outstanding-json-seat-instructions.md` §6 | FAIL | FAIL |

**Why C3 only on added lines:** a whole-tree C3 would fail on the 555 existing files from day one. Scoping it to added lines is a ratchet: no new stale claim gets in, and old ones are fixed when touched.

**Relationship to the Jev harnesses** (`CLAUDE.md` Session Start item 7):
- **Harness 4 (doc scanner)** is advisory: Jev judges whether a prose line *asserts* the current state. This proposal is deterministic and decides nothing in prose. The two complement each other: the register gives the truth, and harness 4 finds prose that claims to be current.
- **The two PARKED rows in `docs/trader-tick-queue.md` §2** — the "shipped-state sweep harness" and the "cross-doc contradiction sweep" — overlap C2/C3. See `DSR-8`.

### 4.6 Generated views (P3)

`tools/docs/render-state.py` (host-agnostic, per the Linux-port rule in `CLAUDE.md`) writes `docs/generated/state.md`:
- open decisions grouped by owner, with their docs;
- recent rulings (last 14 days);
- items from `docs/outstanding.json`.

Seat handovers keep their hand-written §0 FIRST ACTIONS, because that is judgment. Their "open decisions" section becomes a link to the generated view.

### 4.7 Doc lifecycle and Obsidian (P4, optional)

- Optional YAML frontmatter on specs: `status: proposal | accepted | superseded | closed`, `superseded_by: <doc>`.
- Opening `docs/` as an Obsidian vault costs nothing, because the files stay plain `.md`. Add `.obsidian/` to `.gitignore`. The Dataview plugin can list docs by frontmatter status for the trader; scripts do the same for seats.

## 5. Role of `docs/trader-tick-queue.md` (decision `DSR-6`)

**Proposed:**
- the queue keeps **order and gates** (its own stated purpose: *"This doc carries ORDER and GATES"*);
- it **stops carrying state**;
- the 9 "PREVIOUS STATE READ" blocks move verbatim to `docs/trader-tick-queue-archive.md` (the 2026-09-14 trim precedent);
- `CLAUDE.md` Session Start item 6 changes from "read the queue for what is outstanding" to "read `docs/generated/state.md` and `docs/outstanding.json` for state, and the queue for order".

## 6. Backfill (decision `DSR-4`)

⚠ **Revised 2026-10-05 (UTC) with measured costs.** The first version said the full backfill costs "several sessions" and recommended (a). That cost was a guess, and it overstated the reading part. The measurements are below; the superseded text is quoted at the end of this section.

### 6.1 Measured cost (working tree at `d83d8ac`)

"Decision row" here means a table row keyed by an ID, under a header that names a decision, question, option or ruling. **Token counts are the `Read` tool's own counts**, not a bytes-to-tokens conversion (`CLAUDE.md` forbids scaling one from the other).

| Set | Docs | Decision rows | Tokens, decision rows only | Tokens, whole docs |
|---|---|---|---|---|
| **Full** (top-level docs) | 119 | 768 | **91,812** (measured) | ~0.95–1.0 M (estimate) |
| **Living docs** (§6.3) | 56 | 366 | **49,140** (measured) | ~0.5 M (estimate) |
| Unit: `docs/DeribitIndicatorProject.md`, the first session-start read | 1 | — | — | **35,166** (measured) |

- **The whole-doc figures are estimates.** The files are 2.3 MB and 1.2 MB, too large for `Read` to count. The estimate applies the 2.28–2.42 bytes/token measured on the three counted files. Treat them as ±15 %.
- **Reading is cheap; judgment is the cost.** A script pulls out the rows: 92K tokens, about 2.6× `docs/DeribitIndicatorProject.md`. The seat's work is deciding, for each row:
  - is it a decision or a finding?
  - is it ruled or open?
  - **was it superseded in a later cell or another doc?** For example, absorption decision `D-2` in `docs/absorption-mechanism-revision-proposal.md` was re-ruled across three cells and two docs.
- Instruments: `docs/audits/proofs/doc-state-census-2026-10-03/` (`backfill_size.py`, `concat_sets.py`, and their outputs).

### 6.2 What no backfill captures

Even (b) records **every decision written into a decision table**, not every decision ever made:

| Gap | Size |
|---|---|
| Rulings written in prose, outside tables | 205 lines in top-level docs, 54 in the archive docs (lines carrying RULED, TICKED or AUTO-PROCEEDED) |
| Archive docs | 145 ID rows, 1 under a decision-style header: their decisions are mostly prose |
| The earliest decisions | Docs start 2026-03-13. Earlier choices live in `settings.json` `change_log`, `docs/DeribitIndicatorProject.md` §15 and git history |
| Decisions made in conversation and never written down | Not recoverable |
| Older table formats ("Resolved", "✅ Yes"; 248 header shapes) | Found by script, judged by a seat |

The register links each decision to its doc. **The reasoning stays in the doc and is reached by the link; it is not copied.**

### 6.3 Options

**Living docs** = the current state docs plus every doc they link to directly. The state docs are `docs/trader-tick-queue.md`, `docs/roadmap.md` and the seat handovers of the last 30 days (17 on 2026-10-05). In practice: the docs a seat is likely to cite this month.

| Option | Records | Seat sessions (Opus 5.5, high) — estimate |
|---|---|---|
| (a) **Ratchet:** seed with every decision **open today**; add every new and newly-ruled decision from P1 on; backfill a ruled row when its doc is next touched | Open state complete from day one; history fills in over time | under 1 |
| (b) **Full backfill:** every decision row in the 119 docs (768 rows) | Every decision recorded in a decision table | 2–3 |
| (c) **Living docs:** (a) plus every decision row in the 56 living docs (366 rows, 48 % of full) | Everything currently in play | 1–2 |

The session counts are not measured: they depend on how often a row needs a cross-doc check.

### 6.4 Read

⚠ **This is the class `CLAUDE.md` reserves:** (b) records more than (a) or (c). **My revised read is (b), or (c) as a first pass with (b) directly after.** Under the three-step test, (b)'s extra cost over (c) is now known and small, about one session, and my economy argument for (a) no longer holds. The trader decides.

> *Superseded text, kept per the quote-and-label convention:* ~~"(b) Full backfill … Several sessions." · "My read is (a), and my reason is partly cost, so the trader decides. My argument for (a) beyond cost: no check or view consumes old ruled rows. C2 and C3 only bite on rows the register holds, and the ratchet adds a row the moment its doc is touched, which is exactly when a stale copy could be written. Against (a): until a doc is touched, a seat that greps an old ID gets no register answer."~~

## 7. Phases and timing

| Phase | Content | Gate to start |
|---|---|---|
| **P1** | Schema; `docs/registers/decisions.jsonl` seeded per `DSR-4`; C1, C2, C5; fixtures for the checks (two docs with the same local ID; a struck-through row) | Trader ticks this proposal |
| **P2** | C3, C4 | P1 accepted |
| **P3** | `render-state.py`; `docs/generated/state.md`; `outstanding.json` tick rows generated; the queue change in §5 | P2 accepted |
| **P4** | Frontmatter; Obsidian note in `CLAUDE.md` | Optional |

**Timing (decision `DSR-7`):**
- The trader is away 2026-10-14 → 2026-11-25, and the holiday rule bars builds.
- The pre-holiday list is full.

## 8. Decisions for the trader

| # | Decision | Options | My read |
|---|---|---|---|
| `DSR-1` | Register format | (a) JSONL in git · (b) YAML in git · (c) SQLite · (d) GitHub Issues | **(a).** It diffs per line, merges cleanly and parses natively in PS 5.1. (b) needs a YAML module for the gate. (c) cannot be diffed. (d) leaves the repo |
| `DSR-2` | Key scheme | (a) `<doc-stem>#<local-id>` · (b) renumber every decision to a global ID · (c) bare IDs | **(a).** (c) is mechanically wrong: 291 IDs collide (§2). (b) rewrites 210 docs and breaks every existing citation. (a) changes no doc |
| `DSR-3` | Where the ruling text lives | (a) doc row and register, kept consistent by C2 · (b) register only; doc rows point to it · (c) doc only; register holds status | **(a).** It is the more self-describing option: the spec still says what was ruled if the register is lost. C2 removes the drift risk of the duplicate |
| `DSR-4` | Backfill scope | (a) ratchet · (b) full · (c) living docs (§6) | **Revised 2026-10-05: (b), or (c) then (b). Reserved to you.** Measured cost in §6.1: (b) reads 92K tokens of rows and needs about one session more than (c). ~~(a), reserved to you. Cheaper and records less~~; argument in §6 |
| `DSR-5` | C3 severity | (a) FAIL on prepush, added lines only · (b) WARN only · (c) FAIL on the whole tree | **(a).** (b) is the alarm that gets ignored. (c) fails on day one across 555 files |
| `DSR-6` | The queue's role | (a) order and gates only; state moves out; banner archived (§5) · (b) leave it; add the generated view beside it | **(a).** (b) keeps two state homes, which is the cause of the problem |
| `DSR-7` | Timing | (a) P1 after 2026-11-25 · (b) P1 before 2026-10-13 | **(a).** The pre-holiday list is full, and a half-built gate check over the holiday helps no one. Meanwhile seats keep `docs/outstanding.json` current, which already works |
| `DSR-8` | The two parked Jev rows (shipped-state sweep, cross-doc contradiction sweep) | (a) keep parked; build this deterministic layer first; revisit them after P2 · (b) merge them into this proposal · (c) drop them | **(a).** The register gives the ground truth both sweeps lack. Re-scope them after P2, when what remains is pure prose judgment |

## 9. Not verified

- The census regex over-counts decisions: it includes finding tables. The 2,010, 831 and 291 figures are upper bounds for decisions. The collision finding still stands at any plausible scale (`D-2` in 27 docs).
- None of the 68 open-marker rows was read.
- C3's false-positive rate on real pushes is unmeasured. P2 should measure it on the last 30 days of commits before turning it to FAIL.
- Whether Obsidian's Dataview reads the repo's long-cell tables usefully. Not tried.
