# Fixture parser — build spec

**Created 2026-09-21 (UTC).** Harness 3 of the Jev programme ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6).

**Two jobs, one parse of `verify/ordercheck/Program.vb`:**

- **`FP-1` — fixture-literal provenance.** `CLAUDE.md` makes this a hard rule and says plainly that **no tool can enforce it**, so it falls to review. It has failed twice in the tree, found by two different routes, neither by a test.
- **`FP-2` — fixture name against assertion.** Does a fixture's body assert what its name claims?

⛔ **NOT a gate.** Advisory, same three reasons as [`rider-travel-check-spec.md`](rider-travel-check-spec.md) `D-3`.

---

## 0. ⭐ Model and effort

**Model: Sonnet. Effort: medium.**

**Why.** Two complete in-repo templates exist — [`../tools/checks/commit-walker.ps1`](../tools/checks/commit-walker.ps1) for the full self-consistency shape, and [`../tools/checks/rider-travel.ps1`](../tools/checks/rider-travel.ps1) for the baseline refusal and coverage block. The population is measured (§2). The judgment design is settled. What remains is a parser and two question sets.

**Where Sonnet will slip — four traps.**

1. ⛔⛔ **Walking fewer than ALL 87 `settings.json` revisions.** `CLAUDE.md`'s rule is **off-EVER-shipped**, not off-currently-shipped. **Measured: the most recent 40 revisions return only `{18.0, 23.0}` for `indicators.OBV.trend_gate`; all 87 return `{0.001, 10.0, 18.0, 23.0}`.** A 40-revision walk misses two-thirds of the history and would clear a stale literal as novel.
2. ⛔ **Composing Nouls with AND.** Standing across this programme, measured at 0 of 8 against a single Choice's 4 of 8. Verdict reads ONE Choice. Copy `commit-walker.ps1`'s pattern.
3. ⛔ **Trusting a documented value history.** `docs/trader-tick-queue-archive.md` records `trend_gate` as starting at `8.0`; **`8.0` never existed in the tree.** Already recorded as finding `F6` in [`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md). **Ground truth comes from the revision walk, never from a doc.**
4. ⛔ **Regex-parsing VB with line anchors.** `CLAUDE.md` has a standing warning: an anchored scan misses VB's inline `If … Then Return`, the commonest form here. **Prefer unanchored patterns and filter by eye.**

**Escalation trigger — stop and report.**

- The revision walk yields a value set for fewer than half the matched keys. The extractor is then broken, not the history sparse.
- Fewer than 60 literal call sites are found. §2 measured 120; half that means the parser is missing a form.

**Session split: none.**

---

## 1. Why this needs a judgment model

`CLAUDE.md` states the problem in its own words:

> **no tool can tell the two apart, and one of them rots**

A fixture passing a settings-derived threshold as a literal is either **SHIPPED BEHAVIOUR** (then it must derive from cfg, never hardcode) or **MECHANISM** (then a literal is correct and the comment must say why). `CLAUDE.md`'s worked pair: two fixtures pass OFI thresholds at values that are neither the method default nor the shipped pair, and that is **legitimate** — they are refactor-equivalence tests where any consistent value serves. Another pins a value against a different shipped one and that is **stale**. **To a machine they are the same shape.**

⭐ **The split that makes this tractable: the VALUE question is code, the COMMENT question is judgment.**

- **Code** computes whether a literal matches any ever-shipped value of its key. Deterministic, and it is where trap 1 bites.
- **Jev** judges whether the comment at that call site *declares the right class for what the code found*. Semantic, and it is what no tool can do.

---

## 2. The population — measured 2026-09-21 (UTC)

| Quantity | Value |
|---|---|
| `verify/ordercheck/Program.vb` | 16,739 lines |
| Fixture subs (`Sub A<n><x>_Name`) | **359** |
| Harness checks reported at last run | 425 — **more checks than subs; do not conflate them** |
| Named-argument literal passes | **120** |
| Distinct parameter names among them | **43** |
| Call sites carrying `MECHANISM` | 44 |
| Call sites carrying `SHIPPED` | 18 |
| `settings.json` revisions to walk | **87** |

⚠ **These move. Treat as smoke anchors, never assertions.**

---

## 3. Decisions

Auto-proceeded and recorded. **None RESERVED** — this tool reads the harness and git history and writes a report. No settings key, no scoring, no rendered value, no collector or store write, no schema change.

| ID | Decision | Taken |
|---|---|---|
| `FP-D1` | Where the value history comes from | **The 87-revision walk, always.** Never a doc, never a recent slice. Trap 1 and trap 3 |
| `FP-D2` | Parameter-to-key matching | **Code proposes candidates by name normalisation** (camelCase to snake_case, strip a leading indicator prefix); **Jev adjudicates only where code finds zero or multiple candidates.** Most matches are mechanical; the residual is a naming judgment |
| `FP-D3` | `FP-2` ground truth | **Mutation.** Corrupt a known-good fixture's name, confirm the detector flags it, restore. ⭐ **The only route to positives here, and this repo already requires fixtures be mutation-proven** |
| `FP-D4` | Self-consistency | **5 samples, agreement rate on every row**, per [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4b. Not optional and not a later addition |
| `FP-D5` | Scope of `FP-2` on the first build | **The 44 `MECHANISM` and 18 `SHIPPED` sites only**, not all 359 subs. Those are the sites the rule governs; widening later is cheap |

---

## 4. What to build

`tools/checks/fixture-parser.ps1`. PowerShell 5.1. ⭐ **Mirror [`../tools/checks/commit-walker.ps1`](../tools/checks/commit-walker.ps1) throughout** — parameters, coverage block, baseline refusal, self-consistency loop, exit codes. Reuse `Invoke-Jev` from `tools/checks/lib/InvokeJev.ps1`; **never write a second HTTP call.**

### 4.1 Steps

1. Parse `verify/ordercheck/Program.vb`: every named-argument literal pass, its enclosing `Sub`, and the comment block above the call site.
2. Walk all 87 `settings.json` revisions. Build, per key path, the **set of ever-shipped values**. Cache it to a gitignored scratch file — the walk is slow and the history below `HEAD` is immutable.
3. Match each parameter to candidate settings keys (`FP-D2`). Record zero-candidate and multi-candidate cases separately.
4. Classify in code: does the literal equal any ever-shipped value of its matched key?
5. **Refuse without a baseline.** Structural, before any API key read.
6. Jev judges the residual — self-consistency, 5 samples.
7. Report, exit.

### 4.2 ⛔ Coverage block, and a broken parser is loud

```
FIXTURE_SUBS=<n>
LITERAL_CALL_SITES=<n>
PARAMS_DISTINCT=<n>
SETTINGS_REVISIONS_WALKED=<n>
KEYS_WITH_VALUE_HISTORY=<n>
MATCHED_ONE_KEY=<n>
MATCHED_ZERO_KEYS=<n>
MATCHED_MULTI_KEYS=<n>
LITERAL_EQUALS_EVER_SHIPPED=<n>
SITES_WITH_PROVENANCE_COMMENT=<n>
SITES_JUDGED=<n>
```

⛔ **Exit 2 with `PARSER_SUSPECT` when `LITERAL_CALL_SITES < 60`** (§2 measured 120) **or `SETTINGS_REVISIONS_WALKED < 80`** (§2 measured 87). Both are the escalation trigger firing rather than a quiet wrong answer.

### 4.3 The questions

**`FP-1`, per residual call site.** State: the enclosing sub's name, the call line, the comment block above it, the matched key, and **the ever-shipped value set the walk found**.

| ID | Type | Asks |
|---|---|---|
| `comment_declares_class` | noul | Whether the comment states which of the two classes this literal is |
| `class_matches_evidence` | noul | Whether the class the comment states is consistent with the supplied ever-shipped value set |
| `verdict` | choice | `mechanism_declared_ok` · `shipped_declared_ok` · `undeclared` · `declared_but_contradicted` · `ambiguous` |

**`FP-2`, per named fixture.** State: the sub name and its body.

| ID | Type | Asks |
|---|---|---|
| `name_matches_assertion` | noul | Whether the body asserts the property the name claims |
| `verdict` | choice | `name_matches` · `name_overclaims` · `name_understates` · `ambiguous` |

⛔ **`verdict` is the only answer code reads. Nouls are diagnostics, never combined.**

### 4.4 Baseline

⛔⛔ **First-run baseline to the TRACKED `docs/harness-runs/fixture-parser-<UTC-stamp>-baseline.json`**, per [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §2 step 3. Vocabulary is the verdict values above plus `unsure`.

⚠ **And §4a applies: the acceptance dry run's window must be disjoint from the measured run's.** Provide `-SubFilter <pattern>` so the two can be split by fixture-ID range.

---

## 5. Acceptance

1. All 87 revisions walked; `SETTINGS_REVISIONS_WALKED` printed. **Show `indicators.OBV.trend_gate` resolving to `{0.001, 10.0, 18.0, 23.0}`** — the §2 measurement, and the trap-1 proof.
2. `LITERAL_CALL_SITES` within sight of 120.
3. Forced `LITERAL_CALL_SITES < 60` exits 2 with `PARSER_SUSPECT`. Output pasted.
4. Runs without a baseline: exits 2, **no API call**. Output pasted.
5. Verdict reads the `verdict` Choice alone. **One line, confirmable by reading.**
6. Self-consistency: 5 samples, agreement rate on **every** row.
7. `Invoke-Jev` shared, not copied. One definition in the tree.
8. **`FP-D3` mutation:** corrupt one fixture's name, show the detector flags it, restore, show it stops. ⛔ **Restore via `git checkout` on that file and prove the tree is clean afterwards.**
9. A live acceptance run on a `-SubFilter` range. ⛔ **Counters, tokens and timings only. NO per-item verdicts, NO aggregates** — [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a.

---

## 6. What this spec does NOT verify

- **That the parameter-to-key matching is right.** `FP-D2` is a naming convention, not a contract. A wrong match produces a confidently wrong value set.
- **That an ever-shipped match means the literal is stale.** `CLAUDE.md`'s own example has two fixtures legitimately passing non-shipped values; the converse — a legitimate literal that happens to equal a shipped value — is exactly what the `MECHANISM` declaration exists to license. **Equality is evidence, never a verdict.**
- **Values that were shipped only in an untracked `settings.local.json` overlay.** The walk sees the tracked file alone.
- **`FP-2` beyond the 62 sites in `FP-D5`.**
