# Rider-travel check — build spec

**Created 2026-09-21 (UTC).** Build spec for `tools/checks/rider-travel.ps1`, a **review aid** that checks whether every `TRAVELLING` rider in [`csv-rotation-riders.md`](csv-rotation-riders.md) actually arrived in a proposed new `AnalysisLogger.Header`.

⛔ **This is NOT a gate and must not be wired into [`../tools/checks/verify-gate.ps1`](../tools/checks/verify-gate.ps1) in this build.** See `D-3`.

---

## 0. ⭐ Model and effort — read before starting

**Model: Sonnet. Effort: medium.**

**Why that tier.** The judgment work is already done. The question design was settled by a measured run (§6), the header extractor **already exists and is proven** (`Get-HeaderText` in [`../tools/checks/rotation-riders.ps1`](../tools/checks/rotation-riders.ps1)), and every mechanical piece has an in-repo template: that same script for the PowerShell-plus-git shape, and this spec's §5 for the API call shape. Nothing here needs a derivation.

**Where Sonnet will specifically slip — three traps, all concrete.**

1. ⛔⛔ **Composing two Nouls with `AND`. This is the trap, and it is MEASURED, not predicted.** In the 2026-09-21 validation run on a different task, the combination `evidence_shows_shipped > 0.5 AND row_claims_outstanding > 0.5` scored **0 of 8** while a single Choice question over the same state scored **4 of 8**. The two Nouls were not independent: one went low exactly where the other went high. The natural implementation here is `if ($arrived.noul -lt 0.5 -and $landsInHeader.noul -gt 0.5)`. **Do not write it.** The verdict comes from ONE Choice question; the Nouls are diagnostics printed beside it, never combined. Source: `docs.typesafe.ai/model-jaggedness/jev-1.13.md`, "Common-sense structural invariants".
2. ⛔ **Silent zero.** If the ledger parser matches no rows — a table reformat, a stray character in an ID cell — the tool prints "all riders arrived", which is indistinguishable from success. §4.3 makes coverage output mandatory and a zero count a non-zero exit.
3. ⛔ **Copying `Get-HeaderText` instead of sharing it.** A second copy drifts the first time the header declaration is refactored. `CLAUDE.md` already rules on this class for constants: the fixture must read the production value, not restate it. Same reasoning, same defect.

⚠ **The fixtures cannot be relied on to catch trap 1.** The implementer writes them, so a fixture built from a misunderstanding of the composition encodes that same misunderstanding and still reads green. Trap 1 is a **review item**, checked by reading the verdict expression, not by running the harness.

**Escalation trigger — stop and come back, do not work around.** Either of these means the shape differs from what this spec assumes:

- `Get-HeaderText` cannot be shared without changing `rotation-riders.ps1`'s observable behaviour (see `D-2`'s regression check).
- The [`csv-rotation-riders.md`](csv-rotation-riders.md) §1 table cannot be parsed by a table-row regex.

**Session split: none.** One session.

---

## 1. The problem

[`csv-rotation-riders.md`](csv-rotation-riders.md) §0 rule 2 states the gap in its own words:

> **The gate forces this file to be TOUCHED; it cannot force the CONTENT to be right. That stays a review item.**

[`../tools/checks/rotation-riders.ps1`](../tools/checks/rotation-riders.ps1) verifies that the ledger **changed** when `AnalysisLogger.Header` changed. It does not and cannot verify that each rider's column actually **arrived**. That verification is a human read of prose rider descriptions against a 116-column header string.

⛔ **The cost of getting it wrong is recorded.** The 2026-09-01 absorption-instrumentation rotation fired and none of its riders travelled. One of them, `RIDER-7` (the `RecentTradeCount` column), had never appeared on any list and was found only by a later sweep of specs for deferrals.

⭐ **Eight riders are `TRAVELLING` with the absorption S2 rotation right now** — `RIDER-1` through `RIDER-7` plus `RIDER-9`. This check runs before that commit.

---

## 2. Why this needs a judgment model and not a regex

**Rider descriptions are prose; header entries are identifiers.** The ledger's §1 rows read, verbatim:

- `RIDER-3` — *"`TriggerMode` column — what FIRED the run"*
- `RIDER-7` — *"`RecentTradeCount` column"*
- `RIDER-9` — *"`VPFRSignal` and `VPFRPoc` columns: the volume-profile label and POC the scoring vote and the placed-target POC tier read"*

⭐ **And the rider set is heterogeneous — not every rider is a column at all:**

- `RIDER-1` is a **naming behaviour** in `AnalysisLogger.EnsureLogFile`, not a header column.
- `RIDER-2` lands in `tools/ops/` scripts and a deploy-checklist doc.
- `RIDER-8` is `CONDITIONAL` and explicitly does **not** ride this rotation.

So the first judgment is *"does this rider land in the CSV header at all?"* — which the ledger's own "Lands in" column answers in prose. Classifying that, then matching a prose description to a column identifier, is a two-step semantic task. A regex on backticked tokens gets `RIDER-7` right and `RIDER-9` half right, and has no way to handle `RIDER-1`.

---

## 3. Decisions

All five auto-proceeded under `CLAUDE.md`'s auto-proceed ruling and recorded here. **None falls in a RESERVED class:** no `settings.json` key, no scoring effect, no rendered value, no write to the live collector or trade store, and **no schema or CSV-header change — this tool READS a proposed header, it never changes one.**

| ID | Decision | Options | Taken, and why |
|---|---|---|---|
| `D-1` | Where the tool lives | `tools/checks/` · `tools/ops/` | **`tools/checks/`.** It sits beside the rider gate it complements and shares its library |
| `D-2` | Header extraction | copy `Get-HeaderText` · extract it to a shared file both scripts dot-source · re-implement | **Extract to `tools/checks/lib/HeaderText.ps1`, both dot-source it.** A copy drifts on the first header refactor, and that is the defect class this repo names elsewhere. ⚠ Carries a mandatory regression check, §4.5 |
| `D-3` | Gate or advisory | wire into `verify-gate.ps1` now · advisory, run manually | ⛔ **Advisory.** Three reasons: the detector is unvalidated on its first run, a pre-push gate must not block on an unvalidated model judgment, and CI holds no API key. Revisit only after the check has run against a real rotation |
| `D-4` | The concurrent check | trust the operator to write their read first · the tool refuses to call the API until a baseline file exists | ⛔ **Structural refusal.** A safety held by a comment is the defect class — the ledger's own `S-1` reasoning. See §4.4 |
| `D-5` | API or parse failure | warn and continue · fail loud, non-zero exit | **Fail loud.** Precedent is the repair scan: a failed scan is `SCAN_FAILED`, never an empty store. A rider check that degrades to "all clear" on an HTTP error is worse than no check |
| `D-6` | Baseline-vs-detector disagreement and the exit code | disagreement fails the run · disagreement is reported, exit code reads the detector verdict alone | ⭐ **TRADER-RULED 2026-09-21 (UTC), as recommended: report it, do not fail on it.** The tool reports to the seat; a disagreement is a finding to READ, not a build failure. Raised by the implementer as a gap the spec left unstated |
| `D-7` | Baseline `unsure` against verdict `ambiguous` | merge them as one state · keep them distinct | ⭐ **TRADER-RULED 2026-09-21 (UTC), as recommended: keep them DISTINCT.** They mean different things — `unsure` is *the seat could not tell*, `ambiguous` is *the rider text does not name its columns precisely enough*. Merging would hide which one occurred, and the two call for different fixes: more reading against a better-written ledger row |

---

## 4. What to build

`tools/checks/rider-travel.ps1`. PowerShell 5.1, same conventions as [`../tools/checks/rotation-riders.ps1`](../tools/checks/rotation-riders.ps1).

### 4.1 Inputs

| Parameter | Meaning |
|---|---|
| `-AfterFile` | Path to the `AnalysisLogger.vb` carrying the PROPOSED new header. Default `AnalysisLogger.vb` |
| `-BeforeRev` | Git revision for the current header. Default `HEAD` |
| `-LedgerPath` | Default `docs/csv-rotation-riders.md` |
| `-BaselinePath` | The operator's own read, written BEFORE this runs. Default `rider-travel-baseline.json` |
| `-OutPath` | Where the comparison lands. Default `rider-travel-report.md` |

### 4.2 Steps

1. Dot-source `tools/checks/lib/HeaderText.ps1`. Extract the BEFORE header from `-BeforeRev` and the AFTER header from `-AfterFile`. Split both on `,` into column lists.
2. Parse [`csv-rotation-riders.md`](csv-rotation-riders.md) §1. For each table row capture: the ID cell, the Rider description cell, the Status cell, the "Lands in" cell.
3. Keep rows whose Status cell contains `TRAVELLING`. Report the others by ID and status; do not silently drop them.
4. Compute `added` = columns in AFTER not in BEFORE, and `removed` = the reverse. Removed columns are reported — a rotation that drops a column is a finding in its own right.
5. **Refuse to proceed unless `-BaselinePath` exists** (§4.4).
6. One Jev request per `TRAVELLING` rider (§5).
7. Emit the report (§4.3) and exit per §4.6.

### 4.3 ⛔ Coverage output is mandatory, and zero is an error

Every run prints, before anything else:

```
RIDERS_IN_LEDGER=<n>
RIDERS_TRAVELLING=<n>
RIDERS_JUDGED=<n>
HEADER_COLUMNS_BEFORE=<n>
HEADER_COLUMNS_AFTER=<n>
COLUMNS_ADDED=<n>
COLUMNS_REMOVED=<n>
```

⛔ **`RIDERS_IN_LEDGER=0` or `RIDERS_TRAVELLING=0` exits non-zero with `LEDGER_PARSE_FAILED`.** Without this the tool reports success when its parser has broken. The repo's own ruling applies: a counter reading 0 is the tripwire, not waste.

**Sanity anchor for the implementer:** against the ledger as of this spec, `RIDERS_IN_LEDGER` should be 10 (nine numbered plus one consumed row) and `RIDERS_TRAVELLING` should be 8. Against the tracked `AnalysisLogger.vb` at `HEAD`, `HEADER_COLUMNS_BEFORE` is **116** — verified by running the extractor on 2026-09-21 (UTC). ⚠ **Treat these as a smoke check, not an assertion: the ledger changes and the count will move.**

### 4.4 ⛔ The baseline refusal — `D-4`

The tool **exits non-zero without calling the API** if `-BaselinePath` does not exist. The message names the file and says what belongs in it.

**Why this is structural and not a convention.** The whole value of the first run is comparing an independent human read against Jev's. Jev answers in under a second; on a live task the pull to look first is strong, and one look makes the comparison worthless for ever. The refusal removes the choice.

Baseline format — the operator writes this by hand before running:

```json
{ "RIDER-1": "arrived", "RIDER-2": "not_a_header_column", "RIDER-3": "missing" }
```

Permitted values: `arrived`, `missing`, `not_a_header_column`, `unsure`.

### 4.5 ⚠ `D-2` regression check — mandatory, run it and paste the output

Extracting `Get-HeaderText` changes a script wired into the pre-push gate. Before and after the extraction, run the standalone test documented in [`../tools/checks/rotation-riders.ps1`](../tools/checks/rotation-riders.ps1)'s own header comment and confirm `ROTATION_STATUS` and `ROTATION_DETAIL` are byte-identical across the two runs. **If they differ at all, the escalation trigger in §0 has fired.**

### 4.6 Exit codes

| Code | Meaning |
|---|---|
| 0 | Every `TRAVELLING` rider judged `arrived` or `not_a_header_column` |
| 1 | At least one rider judged `missing` or `ambiguous` |
| 2 | Parse failure, baseline missing, or API failure — `D-5` |

---

## 5. The Jev request

One request per `TRAVELLING` rider. `POST https://api.typesafe.ai/v1/systemone`, `model: "jev-latest"`, bearer token from `TYPESAFE_API_KEY`. The key lives in the gitignored `typesafe.local.env` at the repo root.

**State** — one object per request. ⚠ Send the column lists, not the raw header string: `docs.typesafe.ai/model-jaggedness/jev-1.13.md` warns that accuracy falls as state grows with irrelevant detail.

```json
{
  "rider": { "id": "...", "description": "...", "lands_in": "...", "status": "..." },
  "columns_added_by_this_rotation": ["..."],
  "all_columns_after": ["..."]
}
```

**Questions** — three. ⛔ **`verdict` is the answer. The two Nouls are diagnostics and are never combined with it or with each other. See §0 trap 1.**

| ID | Type | Asks |
|---|---|---|
| `lands_in_header` | noul | Whether this rider describes one or more columns in `analysis_log.csv`, as opposed to a code behaviour, an ops script, or a document |
| `column_present` | noul | Whether a column matching the rider's description appears in `all_columns_after` |
| `verdict` | choice | `arrived` · `missing` · `not_a_header_column` · `ambiguous` |

Write `verdict`'s criteria so each option describes a concrete situation:

- `arrived` — this rider describes CSV columns and every column it describes is present
- `missing` — this rider describes CSV columns and at least one is absent
- `not_a_header_column` — this rider lands somewhere other than the CSV header, so column presence does not apply
- `ambiguous` — the description does not name its columns precisely enough to decide

⚠ **Instructions are read literally** (same source, "Literal reading"). Name the state paths in backticks, as `all_columns_after`, and state the exact condition rather than the intent behind it.

---

## 6. Where the design came from

The question shape, the single-Choice verdict and the coverage requirement all come from a measured run on 2026-09-21 (UTC) against a different task: detecting stale rows in [`trader-tick-queue.md`](trader-tick-queue.md) §2, scored against the 11 rows closed by commit `0767245`. Numbers from that run, for calibration only:

- 42 rows, two arms, 84 API calls: **$0.0046 total, about 9 seconds of wall clock.** Cost is not a consideration at this scale.
- Row text alone: both the model and the human seat scored **1 of 8**. Evidence supplied by code lifted it to 5 of 8 and 6 of 8 respectively.
- ⭐ **The binding constraint was evidence gathering, not judgment.** Both rows missed by both judges were rows where the enumeration code found nothing to reason over. That is why §4.3 exists.

---

## 7. Acceptance

1. `tools/checks/lib/HeaderText.ps1` exists; both scripts dot-source it; no second copy of the extraction logic exists in the tree.
2. The §4.5 regression check was run and its two outputs pasted into the spec-back.
3. The tool refuses to run without a baseline file, proved by running it without one and pasting the output.
4. A forced `RIDERS_TRAVELLING=0` — point `-LedgerPath` at an empty file — exits 2 with `LEDGER_PARSE_FAILED`, output pasted.
5. The verdict expression reads from `verdict` alone. **A reviewer must be able to confirm this by reading one line.**
6. A dry run against the tracked `AnalysisLogger.vb` as both BEFORE and AFTER: zero columns added, and every header-landing rider judged `missing`. ⭐ **This is the fail-first proof** — with no rotation applied, the riders genuinely have not arrived, so anything else means the tool is not reading the header.

⚠ **Not acceptance, because it cannot be run yet:** the real S2 rotation comparison. That is the first live use, and it is where the detector gets validated.

---

## 8. What this spec does NOT verify

- **That the ledger is complete.** [`csv-rotation-riders.md`](csv-rotation-riders.md) §4 already records that its own sweep may have missed deferrals phrased another way. This tool checks riders that are ON the list; it cannot find one that was never written down, which is exactly how `RIDER-7` was lost.
- **That an arrived column is CORRECT** — right name, right position, right value. Presence only.
- **Settings-touch riders.** Out of the ledger's scope and out of this tool's.

---

## 9. ⭐ REVISION 1, 2026-09-22 (UTC) — after the adversarial review, before the first run

**Source:** [`jev-harnesses-adversarial-review-2026-09-22.md`](jev-harnesses-adversarial-review-2026-09-22.md) findings 2 (HIGH) and 5; trader "go". Decision records `RT-R1-1` to `RT-R1-5` in the script header. **The first run is this harness's only measurement, so every change was made before it.**

| Change | Why | Verified |
|---|---|---|
| **`RT-R1-1`** Code decides every rider whose column is named: `Lands in` is `header`, and a backticked identifier sits before the first "column(s)" word. Exact membership in the proposed header | Set membership is exact arithmetic; the Jev docs' first anti-pattern is asking the model what code can compute. **At today's ledger: 6 of 8 travelling riders.** `RIDER-4`'s `DeriveWsHealth` (a method named after the word "column") is correctly NOT taken as a column | Key-stripped run on the real ledger: 6 CODE, 2 JEV, extraction printed per rider |
| **`RT-R1-2`** The other riders go to Jev 5 times each, fresh `uid`; plurality, agreement rate and top probability on every row; UNSTABLE exits 1 | [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4b requires sampling of every harness; this one predated the rule | Synthetic keyed run: STABLE 5/5 on the one Jev rider |
| **`RT-R1-3`** `RIDER_CANDIDATES` printed before the gate | Protocol §2 step 2 | As above |
| **`RT-R1-4`** `EXIT_REASON=NO_ROTATION` when the proposed header adds nothing | A run before the rotation would have spent all 8 riders on an unrotated header | Real ledger, today's header: `NO_ROTATION`, nothing judged |
| **`RT-R1-5`** Item-level refusal, `BASELINE_INCOMPLETE`; `-AllowUnbaselinedItems` for routine re-runs | A rotation commit edits the ledger, so a rider can appear after the seat's read | Synthetic ledger, partial baseline: refused, the missing rider listed |

⛔ **Caught while testing, before commit:** the local list `$samples` overwrote the `-Samples` parameter, because PowerShell variable names are case-insensitive. The Jev row came back empty while tokens were spent. Renamed to `$draws`; re-run clean. The same trap is recorded for VB in this repo's memory.

⚠ **All keyed tests used a synthetic ledger and header** (`SYN-1`–`SYN-3`, scratch files). **No real rider was judged**; the first run is still unspent. Its baseline now needs 8 lines — the code-decided riders are compared too.
