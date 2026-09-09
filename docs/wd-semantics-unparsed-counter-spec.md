# `WD-SEMANTICS` — separate the unparsed count from the weekend count, and fix the report's row label

✅✅ **RULED (c) 2026-09-09 (UTC), trader. BUILD-AUTHORIZED.** Queue row: [`trader-tick-queue.md`](trader-tick-queue.md) §2. Raised by the `WD-TIDY` build (`5996f01`) and deliberately not folded into it.

**Baseline commit: `7163802`.** Every line number here was read at that commit. ⛔ **Re-read if `HEAD` has moved.**

---

## 0. Model and effort

> ### Model: **Sonnet**
> ### Effort: **MEDIUM**
> ### One session.

**Why MEDIUM.** The change is mechanical — one new integer on three carriers, populated at three sites, rendered at four. **What lifts it above LOW is that the three producers do not share a shape:** two already classify explicitly and one **subtracts**, and the subtracting one cannot be extended without restructuring. §4 step 3 is the only real thinking in the build.

⭐ **Two things make this CHEAPER than the queue row implies, both verified at `7163802`:**
- ⚠ **The engine display-string parity rule does NOT fire.** The `Weekend excl.` line is a **perf-strip tooltip** built in `UI/MainForm_Layout.vb:1741-1744`. It is **not** emitted by `BuildPlaintextSnapshot` and has **no** counterpart in `UI/MainForm_Render_Cards.vb` — verified by grep. The parity rule binds those two surfaces only.
- ⚠ **`analysis/AnalysisReport.WeekendExcluded` is rendered NOWHERE.** So changing its semantics moves **no number a user sees**.

### 0.1 Where this will slip

⛔ **Trap 1 — `analysis/AnalysisRunner.vb:46` SUBTRACTS and therefore cannot be extended.** It reads `report.WeekendExcluded = loadedRows.Count - rows.Count`. **A subtraction cannot say WHY a row went**, which is the whole defect. **You must replace the `Where` + subtraction with an explicit classifying loop** (§4 step 3). Adding a second subtraction is not a fix — it is the same bug twice.

⛔ **Trap 2 — the guard order is load-bearing and must not be re-ordered.** At both counter sites the `DateTime.MinValue` test runs **FIRST**, before the weekday test. That is not defensive padding: **`DateTime.MinValue.DayOfWeek` is MONDAY**, so a naive day check admits every unparsed row as a valid weekday. **The new counter attaches to the existing first guard; it does not move it.**

⚠ **Trap 3 — `A68a` will still pass after a wrong build.** It asserts `agg.WeekendExcluded = 2` over a batch containing one `MinValue` row. Under this change that stays **2**, so `A68a` is satisfied whether or not the new counter is populated. **The new counter is unpinned unless §6 adds an assertion for it.**

⚠ **Trap 4 — do not change `WeekendExcluded`'s meaning on the two counter sites.** They already exclude `MinValue` correctly. **This build ADDS a counter there; it does not alter one.** Only `analysis/AnalysisRunner.vb` changes meaning, and only where nothing renders it.

### 0.2 Escalation trigger

⛔ **If populating `UnparsedExcluded` requires changing which rows reach the analysis population — STOP.** This build changes **reporting**, not **scope**. The set of rows that survive filtering must be byte-identical before and after, on all three surfaces.

---

## 1. What this fixes

**`WeekendExcluded` means two different things on three surfaces, and the difference is invisible.**

- A **weekend** row is **out of scope** — expected, benign, permanent.
- An **unparsed** row (`DateTime.MinValue`, a failed `TryParseExact`) is a **data defect** — a broken timestamp in the tape.
- ⛔ **Today those two are either merged or one of them is silently dropped, depending on which surface you read.**

⭐ **Why (c) and not the cheaper (b).** Under (b) — "unparsed drops silently everywhere" — an unparsed row would vanish with **no record on any surface**. `UI/MainForm_Layout.vb:1735` already names that failure in the project's own words: *"a count that is dropped without being shown is a silent hole."* [`trader-tick-queue.md`](trader-tick-queue.md) rejected a coverage-report option on the same basis. **A counter that reads `0` is not waste — it is the tripwire.**

⛔ **(a) is REJECTED outright, not merely not-chosen:** unifying on "unparsed counts as weekend" makes a corrupt timestamp indistinguishable from a Saturday, and buries a data-quality alarm inside a scope counter.

---

## 2. Measured state at `7163802`

| Producer | Unparsed counted as weekend? | Rendered |
|---|---|---|
| `tools/CeilingAudit/CsvFeatureBuilder.vb:203-207` | **No** — `MinValue` drops silently | `AuditReport.vb:84` · `CeilingAuditProgram.vb:138` |
| `LivePerformanceTracker.vb:771-775` | **No** — `MinValue` drops silently | `UI/MainForm_Layout.vb:1743` |
| `analysis/AnalysisRunner.vb:46` | **Yes** — subtraction, cannot distinguish | ⛔ **nowhere** |

⚠ **Unparsed rows measure ZERO on every book read so far** — the live collector file, the rotated `.bak`, and the 47,682-row pooled book. **This is a latent correctness fix, not a live number problem.** It matters when a parse failure occurs, which is exactly when the counter must be honest.

---

## 3. ⛔ The live mislabel, folded into this build

**`analysis/MarkdownReportWriter.vb:191` prints a label that is wrong today.**

- `analysis/AnalysisRunner.vb:47` sets `report.TotalRows = rows.Count` — the **post-filter** count.
- The writer renders it as **`- Rows in CSV: **{0}**`**.
- **Measured on the 2026-09-09 pooled book: the file holds 47,682 rows; the report printed `Rows in CSV: 37518`.** The line understates the book by **10,164 rows (21 %)**.

⭐ **It belongs in this build because it exists FOR THE SAME REASON:** the number that explains the gap — `WeekendExcluded` — is computed one line earlier and never shown. **Fix the label and render the counters beside it, and the discrepancy becomes self-explaining.**

⚠ **This is a mislabel, not a miscalculation.** Every figure in that report is correctly weekday-scoped. Only the label lies.

---

## 4. The build list

**Six steps, in dependency order. Build after each.**

| # | Step | Detail |
|---|---|---|
| **1** | Add the field to three carriers | `LivePerformanceTracker.WindowAggregate` (beside `WeekendExcluded` at `:91`) · `tools/CeilingAudit/CsvFeatureBuilder.LoadStats` (`:81`) · `analysis/AnalysisReport` (`:21`). Name it **`UnparsedExcluded`** on all three — ⛔ **one name, or the next seam audit finds three** |
| **2** | Populate the two counter sites | `LivePerformanceTracker.vb:771` and `tools/CeilingAudit/CsvFeatureBuilder.vb:203`. The existing line is `If <ts> = DateTime.MinValue Then Continue For`; it becomes a counted drop. ⛔ **The guard stays FIRST (Trap 2)** |
| **3** | ⛔ **Restructure `analysis/AnalysisRunner.vb:44-47`** | Replace the `Where` + subtraction with an explicit loop that classifies each row into **unparsed / weekend / kept**, then sets `TotalRows = kept.Count`. ⚠ **`WeekendExcluded` on this surface drops from (weekend + unparsed) to (weekend only) — no rendered value moves, verified §2** |
| **4** | Update the stated contract | `analysis/AnalysisReport.vb:18` reads *"TotalRows + WeekendExcluded = rows loaded"*. It becomes **`TotalRows + WeekendExcluded + UnparsedExcluded = rows loaded`**. ⚠ **A stale contract comment is how the next reader re-derives the wrong thing** |
| **5** | Render it — three existing surfaces | `UI/MainForm_Layout.vb` — a tooltip line **mirroring the `Weekend excl.` shape at `:1741-1744`**, gated on `> 0` the same way · `tools/CeilingAudit/AuditReport.vb:84` — a sibling table row · `tools/CeilingAudit/CeilingAuditProgram.vb:138` — a `PREFLIGHT_UNPARSED_EXCLUDED=` line beside the existing block |
| **6** | ⭐ **The label fix + the missing render** | `analysis/MarkdownReportWriter.vb:191` — relabel to state plainly that the count is post-filter, **and render `WeekendExcluded` and `UnparsedExcluded` beside it** so the arithmetic closes on screen |

⚠ **Suggested §3 wording, not mandated** — the implementer may improve it, but it must state the count is post-filter and must show both exclusions:

```
- Rows in CSV: 47682  (weekday-scoped: 37518 analysed · 10164 weekend excl. · 0 unparsed excl.)
```

⛔ **If the pre-filter total is not available at that point in `MarkdownReportWriter`, say so and render the two exclusion counts anyway** — do not invent a total by addition without checking it against the loader.

---

## 5. What this must NOT change

- ⛔ **Which rows survive filtering.** Reporting changes; scope does not (§0.2).
- ⛔ **`WeekendExcluded` on the two counter sites** — they are already correct (Trap 4).
- ⛔ **The `MinValue`-first guard order** (Trap 2).
- ⛔ **`settings.json`** — no key, no version bump.

---

## 6. Fixtures — family `A71` (✅ verified free at `7163802`)

| Fixture | Asserts |
|---|---|
| **`A71a`** | `LivePerformanceTracker.AggregateRange` over 1 Monday + 1 Saturday + 1 Sunday + 1 `MinValue` entry yields **`WeekendExcluded = 2` AND `UnparsedExcluded = 1`**. ⛔ **Extend `A68a` rather than duplicating it** — `A68a` already builds exactly this batch and asserts only the weekend half |
| **`A71b`** | `CsvFeatureBuilder.LoadAndBuild` on a CSV with a weekend row and an unparseable timestamp sets **both** counters independently |
| **`A71c`** | `analysis/AnalysisRunner`'s classification identity: **`TotalRows + WeekendExcluded + UnparsedExcluded = rows loaded`**, with a `MinValue` row present so the three-way split is actually exercised. ⭐ **This is the step-3 restructure's only guard** |

⛔ **Mutation-prove each.** Apply the mutation **with the editor** — `python` is **not installed on this box**, and a `python -c` mutation silently does nothing while the harness reports PASS against unmutated code. ⛔ **Assert `Build succeeded` before believing any harness result**, and ⛔ **restore by FILE COPY, never `git checkout -- <file>`.**

⚠ **The mutation that matters for `A71c`: revert step 3 to the subtraction.** `WeekendExcluded` then absorbs the unparsed row and `UnparsedExcluded` stays 0 — `A71c` must fail on that.

---

## 7. Acceptance criteria

| # | Criterion |
|---|---|
| **AC-1** | Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck` all `Build succeeded`, **0 errors 0 warnings**, on a full **`-t:Rebuild`**. ⚠ An incremental build hides new warnings — that is how a `BC42109` slipped through on `S-4` |
| **AC-2** | Harness `ALL PASS`, count **346 → 349** (`A71a` extends `A68a` in place, so `A71b`/`A71c` add two — **state the actual delta and reconcile it**). ⛔ **Measure by RUNNING it, never by grepping `Check(`** |
| **AC-3** | `A67a`, `A68a`, `A68b`, `A69a`–`A69e` all still pass — **no assertion edited except `A68a`'s deliberate extension** |
| **AC-4** | `verify-gate.ps1 -Mode local-fast` → `GATE PASSED` |
| **AC-5** | ⭐ **Run `--preflight` on the frozen pooled book** (`AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv`, MD5 `E8418846838FF97F3C90F782A95B3523`). **`PREFLIGHT_ELIGIBLE_ROWS` must still read `8269` and `PREFLIGHT_WEEKEND_EXCLUDED` must still read `9792`** — proof that scope did not move (§0.2). **`PREFLIGHT_UNPARSED_EXCLUDED` should read `0`** |
| **AC-6** | ⭐ **Run `BacktestRunner report --csv` on that same book. The row line must state `47682` as the file's row count**, not `37518`, and must show both exclusions. Paste the line |
| **AC-7** | `settings.json` untouched — `git diff --stat` must not list it |
| **AC-8** | Mojibake grep and an **unescaped-pipe** count (`grep -o '[^\\]|'`) on every doc row touched, compared against a neighbouring row |

### 7.1 Rules that bind the commit

- ⚠ **The engine display-string parity rule does NOT fire** — verified §0. **State that in the commit message with the reason**, so the next reader does not assume it was skipped.
- ⚠ **`docs/DeribitIndicatorProject.md` §15 — an entry IS owed.** This changes a rendered report line and adds a rendered counter. ⛔ **Do NOT tag the commit `[no-engine-change]`.**

---

## 8. What to report back

Two documents per [`batch-review-packet-convention.md`](batch-review-packet-convention.md) **only if this runs as a multi-lane batch**; a single spec-back is correct for one item against one spec.

⛔ **Label handles `H-n` / `E-n`, never rank an `E-n` first, and run every handle with its output pasted.**

**State plainly:** whether `AC-5`'s `8269`/`9792` held, what the `AC-6` line now reads, and whether `A71c`'s mutation was actually run.

---

## 9. What this build must NOT do

- ⛔ Change which rows survive filtering on any surface
- ⛔ Extend `analysis/AnalysisRunner.vb` with a second subtraction instead of a classifying loop
- ⛔ Move or weaken the `MinValue`-first guard
- ⛔ Give the new counter three different names
- ⛔ Add a weekend or unparsed **rate** — these are out-of-scope counts, not comparison populations
- ⛔ Touch `settings.json`
- ⛔ Tag the commit `[no-engine-change]`
- ⛔ Run any `git` write command — commit is the orchestrator's
