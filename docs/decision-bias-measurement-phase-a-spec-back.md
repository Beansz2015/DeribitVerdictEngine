# Decision-bias tripwire (harness 6) — Phase A spec-back

**Written:** 2026-09-23 (UTC) by the Phase A implementer seat, model `claude-opus-5-5`.
**For:** the orchestrator seat, which writes the blind baseline and runs Phase B itself.
**Reports against:** the Phase A brief for harness 6 (sent in conversation, not committed). Protocol: [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md).
**Format:** [`batch-review-packet-convention.md`](batch-review-packet-convention.md). Single lane, so this packet carries the outcome record too.

**The population revision is `b5000c9`** (`b5000c99cd30a6b0899ba3ebde0cc4420b9fa12b`), the `HEAD` recorded at this seat's start. The resume message named `928aaeb` only as the fallback if no revision had been recorded. It had been recorded, so `b5000c9` stands.

**Tooling commits:** `e2432cf` to `934c298`, all tagged `[no-engine-change]`, none pushed. No `.vb` file, no `settings.json`, no collector access.

---

## 0. ⛔ Read before you open anything — the blind-labelling rules

- **Open ONLY `docs/harness-runs/decision-bias-20260923T140527Z-population.json` until your baseline is committed.**
- These files carry rulings or hint at them. Do not open them before the baseline commit:

| File | What it carries |
|---|---|
| `docs/harness-runs/decision-bias-20260923T140527Z-outcomes.json` | every ruling |
| `tools/checks/measure/decision-bias/manifest_outcomes.py` | the same rulings, as source |
| `docs/harness-runs/decision-bias-20260923T140527Z-excluded.json`, `-unruled.json` | ids and reason codes only; some doc names imply what was built |
| `tools/checks/measure/decision-bias/manifest.py` | state spans only. Its exclusion and leak notes name no population ruling, but skip it anyway |

- **Label in FILE ORDER and do not read ahead.** 10 items quote or imply an EARLIER item's ruling. In every pair the earlier item comes first in the file (checked by the builder). `crossrefs.json` lists the pairs as ids only.
- ⚠ **You are not blind to four items, and you cannot be.** `CLAUDE.md` "The measured bias" names them as overruled. Label them anyway. The scorer's `--exclude-named` drops them as a sensitivity.
- ⚠ **Your memory of the repo is a second, wider leak.** Many of these rulings are in handovers you have read. Where you recognise the ruling, the protocol's answer is `unsure` or an honest label plus a note. Do not re-read a doc to check.

---

## 1. Model and effort for the reviewing seat

**Recommended review tier:** Opus, effort high.

- **Why high:** the judgment is in the manifest, not the code. 150 option splits, recommended labels and pre-ruling revisions were chosen by hand. The builder only proves the text is verbatim.
- **Where a reviewer will slip:** reviewing the code and trusting the manifest. The handles below prove verbatim extraction and reproducibility. They cannot prove the recommended label or the revision choice is right.
- ⛔ **The conflict:** the reviewer who checks outcome labels must not be the seat that labels blind. Check outcomes only after the baseline commit.

---

## 2. Ranked verification handles

`H-n` = a handle the reader can run from the tree. `E-n` = build-time evidence the reader cannot re-run as-is. All `H-n` outputs below were run at tooling commit `5016ad8` (population data at `b5000c9`). `H-7` was re-run after `934c298` with the same result.

**If you only run one, run `H-2`.** It regenerates every data file from git and byte-compares it.

### `H-1` — the builder in check mode (counts, leak check, verbatim spans)

```
python tools/checks/measure/decision-bias/build_population.py
```

```
REV=b5000c99cd30a6b0899ba3ebde0cc4420b9fa12b
POPULATION=150  ADOPTED=124  OVERRULED=22  PARTIAL=4
UNRULED=71  RATIONALE_UNRECOVERABLE=17  EXCLUDED_OTHER=358
UNRECOVERABLE_SHARE=10.2% of 167 candidates meeting all three criteria
EXCLUDED_BY_REASON=mirror:14, mooted_by_other_ruling:5, no_explicit_recommendation:14, no_trader_ruling:46, not_a_decision:4, options_not_explicit:270, rationale_unrecoverable:17, recommendation_not_single:4, superseded_before_ruling:1
LEAK_HITS=37  EXPLAINED=37  UNEXPLAINED=0  STALE_NOTES=0
ERRORS=0
```

- ⚠ This prints aggregate outcome counts. It prints no per-item label.

### `H-2` — regenerate every data file and byte-compare

```
T=$(mktemp -d); python tools/checks/measure/decision-bias/build_population.py --write 20260923T140527Z --out-dir "$T"
for k in population outcomes unruled excluded crossrefs; do cmp -s "$T/decision-bias-20260923T140527Z-$k.json" docs/harness-runs/decision-bias-20260923T140527Z-$k.json && echo "$k same"; done
```

```
population same
outcomes same
unruled same
excluded same
crossrefs same
```

### `H-3` — the recall ledger: every candidate doc is accounted for

```
python tools/checks/measure/decision-bias/enumerate_candidates.py
```

```
REV=b5000c99cd30a6b0899ba3ebde0cc4420b9fa12b
CANDIDATE_DOCS=231  (R1=189  R2=134)
WITH_MANIFEST_ENTRIES=127
TRIAGED_NO_ENTRY=104  mirror_or_status:37, reads_briefs_data_tables:30, ruled_before_or_without_options:4, spec_back_not_trader_ruled:33
UNTRIAGED=0
MANIFEST_DOCS_OUTSIDE_CANDIDATES=4
STALE_TRIAGE=0
```

- `R1` (a candidate rule: the doc holds a recommendation marker AND a ruling marker). `R2` (the doc holds a decision-shaped table).
- ⚠ 100 of the 104 triaged docs carry method `scan`: only their marker lines and tables were checked, not every row. See this packet's §7.

### `H-4` — the four decisions `CLAUDE.md` names (prints yes/no only)

```
python -c "import json; o={x['id']:x['outcome'] for x in json.load(open('docs/harness-runs/decision-bias-20260923T140527Z-outcomes.json',encoding='utf-8'))['items']}
for i in ('docs/trader-tick-queue-archive.md|A54a-scope@b5000c9','docs/trader-tick-queue-archive.md|seeded-session-buckets@b5000c9','docs/s4-eval-cache-identity-proposal.md|D-2@b5000c9','docs/trader-tick-queue-archive.md|WD-SEMANTICS@b5000c9'): print(i, 'present' if i in o else 'MISSING', 'OVERRULED=yes' if o.get(i)=='OVERRULED' else 'OVERRULED=NO')"
```

```
docs/trader-tick-queue-archive.md|A54a-scope@b5000c9 present OVERRULED=yes
docs/trader-tick-queue-archive.md|seeded-session-buckets@b5000c9 present OVERRULED=yes
docs/s4-eval-cache-identity-proposal.md|D-2@b5000c9 present OVERRULED=yes
docs/trader-tick-queue-archive.md|WD-SEMANTICS@b5000c9 present OVERRULED=yes
```

- ⚠ It opens `outcomes.json`. It is safe before the baseline only because it prints nothing beyond what `CLAUDE.md` already states.

### `H-5` — the runner's three refusals, each exit 2, no API call

```
R=tools/checks/measure/decision-bias/run-decision-bias.ps1; P=tools/checks/measure/decision-bias/synthetic/synthetic-population.json
for B in tools/checks/measure/decision-bias/synthetic/NO-SUCH-baseline.json tools/checks/measure/decision-bias/synthetic/baseline-incomplete.json tools/checks/measure/decision-bias/synthetic/baseline-wrong-rev.json; do TYPESAFE_API_KEY=dummy-not-a-key powershell -NoProfile -File $R -Population $P -Baseline $B -DebugState; echo "EXIT=$?"; done
```

Actual output, trimmed only of the four-line candidate list that each run prints first:

```
EXIT_REASON=BASELINE_MISSING
No baseline file at 'tools/checks/measure/decision-bias/synthetic/NO-SUCH-baseline.json'. The SEAT writes its own read of every id above BEFORE this runs, ...
EXIT=2
EXIT_REASON=BASELINE_INCOMPLETE
UNBASELINED_ITEMS=1 -- no seat label for:
  synthetic/S4-vague
Nothing was judged. An unbaselined item judged once is spent for ever (protocol section 5).
EXIT=2
EXIT_REASON=BASELINE_REV_MISMATCH
Baseline rev 'deadbee' is not the population rev 'b5000c99cd30a6b0899ba3ebde0cc4420b9fa12b'. ...
EXIT=2
```

- **Why this proves no call:** the key is a dummy, so a real call would end in `EXIT_REASON=API_FAILED`, not a refusal. `-DebugState` prints one `STATE_DEBUG` line per call, and none appears. No `CALLS=` line appears.
- The same `BASELINE_MISSING` refusal was run on the REAL population with no key in the environment: 150 ids printed, `EXIT=2`, no `STATE_DEBUG` line.

### `H-6` — the leak check, exact command

```
python tools/checks/measure/decision-bias/build_population.py --show-hits
```

- Prints `LEAK_HITS=37  EXPLAINED=37  UNEXPLAINED=0  STALE_NOTES=0` and no `UNEXPLAINED` line. Every hit is listed in this packet's §3.3.

### `H-7` — the scorer end to end on the synthetic set

```
D=tools/checks/measure/decision-bias/synthetic; T=$(mktemp -d)
python tools/checks/measure/decision-bias/score_decision_bias.py --population $D/synthetic-population.json --outcomes $D/synthetic-outcomes.json --baseline $D/synthetic-baseline.json --runner $D/synthetic-jev-dryrun.json --out $T/s.md > /dev/null
cmp -s $T/s.md $D/synthetic-score.md && echo IDENTICAL
```

- Prints `IDENTICAL`. ⚠ Compare with `--out` and `cmp`, not by piping stdout to `diff`: Windows stdout adds CRLF, and a stdout diff reports 63 changed lines that are line endings only. That false failure happened once while this packet was written.

### `H-8` — no control bytes in any changed file

```
grep -lP '[\x00-\x08\x0B\x0C\x0E-\x1F]' tools/checks/measure/decision-bias/*.py tools/checks/measure/decision-bias/*.ps1 tools/checks/measure/decision-bias/synthetic/* docs/harness-runs/decision-bias-*
```

- Prints nothing. ⚠ It did catch one: a Python edit through a bash heredoc turned `\b` into a backspace byte inside the leak regex. Found by this scan and fixed before commit `b0d7159`.

### `E-1` — the live synthetic dry run (re-runnable, not reproducible)

- **Command:** `run-decision-bias.ps1 -Population .../synthetic-population.json -Baseline .../synthetic-baseline.json -OutPath .../synthetic-jev-dryrun.json -Samples 5`, key loaded from `typesafe.local.env`, 2026-09-23 14:07:51 UTC.
- **Why `E` and not `H`:** a re-run spends about 14,000 input tokens, and Jev is not deterministic ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4b), so the numbers can move.
- ⚠ **The shared library was mid-edit during this run.** `tools/checks/lib/InvokeJev.ps1` held another agent's UNCOMMITTED revision (a bounded-retry change; working-tree blob `c561f55`, `HEAD` blob `09fa52a`). This seat did not touch or stage it. The call shape is positional and unchanged, so the runner works with either version.

---

## 3. Results against the brief's acceptance items

### 3.1 Population and outcome counts, against `CLAUDE.md`'s 39 / 8

| Count | Value |
|---|---:|
| Scored population | **150** |
| `ADOPTED` | 124 |
| `OVERRULED` | 22 |
| `PARTIAL` (reported apart) | 4 |
| Unruled (listed in `unruled.json`) | 71 = 63 auto-proceeded + 8 never ruled |
| `rationale_unrecoverable` | 17 of 167 candidates meeting all three criteria = **10.2 %** (the brief's stop line is 25 %) |
| Other exclusions | 358, by reason code in `H-1` |

**The comparison with `CLAUDE.md` "The measured bias" (39 D-tables ticked as recommended, 8 recorded as DEFEATED or overruled, counted 2026-09-10 UTC):**

| Reading of my data, rulings on or before 2026-09-10 | Value | Against |
|---|---:|---|
| Docs holding at least one `ADOPTED` row, excluding the April doc-level approvals | 37 | 39 |
| `OVERRULED` rows whose ruling text itself says defeated, overruled, against the recommendation, reversed or flipped | 7 | 8 |
| All `OVERRULED` rows | 14 | 8 |

- ⚠ **This is a reading, not a reconstruction.** No list behind the 39 / 8 was committed, so no row-level match is possible.
- **Why the population is 150 and not 47:**
  - **Unit.** This population counts decision ROWS. The 39 counts D-TABLES. One D-table held up to seven scored rows.
  - **Doc-level approvals.** 20 rows come from April "Open Questions" tables, where the ruling is the spec's move from "PROPOSED — pending user approval" to "APPROVED". The 39 likely did not count these.
  - **Date.** 26 scored rows were ruled after 2026-09-10: 17 `ADOPTED`, 8 `OVERRULED`, 1 `PARTIAL`.
  - **What `OVERRULED` covers.** The brief defines it as "the ruling differs", so it includes 7 rows ruled before 2026-09-10 where the doc does not use a defeat word. The 8 likely counted only recorded defeats.
  - **What it excludes.** Rulings by a reviewing seat are not trader rulings, so they are excluded even where the doc says "defeated".

### 3.2 The four named decisions

| Decision (`CLAUDE.md` "The measured bias") | Present | `OVERRULED` |
|---|---|---|
| The `A54a` scope | yes | yes |
| Seeded session buckets | yes | yes |
| The `S-4` identity key (`s4-eval-cache-identity-proposal.md` row `D-2`) | yes | yes |
| `WD-SEMANTICS` | yes | yes |

- ⚠ **Seeded session buckets is the one item with a non-default provenance.** Its queue row names the options but carries no read. The recommendation survives only as a verbatim quotation inside [`seam-audit-decisions-second-opinion-2026-08-11.md`](seam-audit-decisions-second-opinion-2026-08-11.md), a review written for the decision before it was ruled but committed in the ruling commit `ee16d03`. Its `provenance` field says `quoted_in_pre_ruling_review`.

### 3.3 The leak check

- **Command:** `H-6`. **Result:** 37 hits, 37 explained, 0 unexplained.
- **Markers scanned** (in question, options and rationale): `RULED`, `ruled`, `ticked`, upper-case `TICK`, `trader`, `overrul`, `defeat`, the check-mark glyph, `as recommended`, `adopted`, and any date later than the state revision's own UTC commit date.
- **Two false-positive classes were fixed in the pattern, not explained away:** lower-case `tick` is a price unit here, and a case-blind `trader` matched the type name `TradeRecord`.

| Item | Field | Marker | Hits | Why it carries no ruling of this item |
|---|---|---|---:|---|
| `docs/a54a-json-poco-drift-guard-spec.md\|D-1` | option (a) | ruled | 1 | a design property of the option |
| `docs/a54a-json-poco-drift-guard-spec.md\|D-1` | rationale | trader | 1 | cites earlier rulings of other decisions |
| `docs/a54a-session2-step1-measurement-2026-09-05.md\|S2-1` | rationale | ruled | 2 | cites an earlier ruling of ANOTHER POPULATION ITEM, one of the four named (a cross-reference pair) |
| `docs/absorption-d2-stage1-rotation-build-spec.md\|RD-1` | rationale | ruled | 2 | cites earlier rulings, one of them ANOTHER POPULATION ITEM, one of the four named (a cross-reference pair) |
| `docs/absorption-d6-spec-back.md\|D-6a` | rationale | trader | 2 | a link to `trader-profile.md` |
| `docs/asia-burst-threshold-derivation-2026-08-01.md\|D3-1` | question | later date | 1 | the doc's title date, the GMT+8 local date of the same commit |
| `docs/coverage-report-cluster-spec.md\|D-6` | rationale | ticked | 1 | pre-ruling text addressed to the trader as the future decider |
| `docs/coverage-trailing-edge-f1-proposal.md\|D-6r` | rationale | ticked | 1 | cites the earlier tick of ANOTHER POPULATION ITEM (a cross-reference pair, this packet's §3.4) |
| `docs/d6d-episode-continuity-spec.md\|D-6d.3` | question, option (a) | later date | 2 | a planned future gate date |
| `docs/doc-status-sweep-and-queue-archive-spec.md\|D-1` | question | trader | 2 | a link to `trader-tick-queue.md` |
| `docs/engine-fix-build-spec-2026-09-21.md\|EF-1` | rationale | later date | 1 | a future deadline named as a constraint |
| `docs/engine-fix-build-spec-2026-09-21.md\|EF-1` | rationale | trader | 1 | pre-ruling text addressed to the trader |
| `docs/engine-fix-build-spec-2026-09-21.md\|EF-4` | rationale | ruled | 1 | a conditional written before the ruling |
| `docs/eval-no-data-outcome-proposal.md\|N4` | rationale | trader, overrul | 3 | pre-ruling text addressed to the trader |
| `docs/geometry-arbitration-modes-proposal.md\|G2` | rationale | ticked | 1 | pre-ruling text addressed to the trader |
| `docs/geometry-arbitration-modes-proposal.md\|G2` | rationale | trader | 1 | an earlier trader statement used as an input |
| `docs/kelly-w6-4-spec-back.md\|D-3` | rationale | trader | 1 | pre-ruling text addressed to the trader |
| `docs/medium-tier-bug-hunt-spec-back.md\|D-2`, `\|D-5` | question | trader | 2 | a routing tag naming who must rule |
| `docs/medium-tier-bug-hunt-spec-back.md\|D-8` | rationale | trader | 1 | pre-ruling text addressed to the trader |
| `docs/medium-tier-diagnosis-spec-back.md\|Q-1`, `\|Q-2` | question | trader | 2 | a routing tag naming who must rule |
| `docs/medium-tier-diagnosis-spec-back.md\|Q-2` | option (c) | ruled | 1 | cites an earlier ruling of an EXCLUDED decision |
| `docs/medium-tier-diagnosis-spec-back.md\|Q-2` | rationale | trader | 1 | an earlier trader statement used as an input |
| `docs/s2-2-calcspread-split-proposal.md\|D-1` | rationale | ruled | 2 | cites the earlier re-ruling of ANOTHER POPULATION ITEM (a cross-reference pair) |
| `docs/session-policy-gate-proposal.md\|P1` | rationale | ticked | 1 | a conditional written before the ruling |
| `docs/trade-store-write-guard-identity-proposal.md\|D-2` | option (2) | overrul | 1 | the option is literally named "overrule" |
| `docs/trade-store-write-guard-identity-proposal.md\|D-2` | rationale | overrul | 1 | a conditional written before the ruling |
| `docs/venue-check-schedule-plan.md\|D-2` | rationale | trader | 1 | pre-ruling text addressed to the trader |

- All ids carry the suffix `@b5000c9`. The full note for every hit is in `manifest.py`, `LEAK_NOTES`.

### 3.4 ⛔ A leak class the marker check cannot see: cross-item reveals

- **Found after the population was first written.** A later decision's own pre-ruling text can state an earlier decision's ruling, because it builds on it. Often no marker word appears.
- **A code scan** (another item's label next to an option letter, plus `re-ruled`, `RE-OPENED`, `beat (a)` and the named decisions) found **10 pairs**. Each was read by hand.
- **What was done:** the pairs are in `manifest.py` `CROSSREFS` and in `crossrefs.json` (ids only). The builder refuses unless each source precedes its dependent in file order. The scorer's `--exclude-revealed` drops the 7 revealed sources as a sensitivity.
- ⚠ **Not verified:** that the scan found every pair. A reveal phrased without a label and an option letter would pass it.

### 3.5 The live dry run — SYNTHETIC items only

- **Four decisions written by this seat for the acceptance window**, in `tools/checks/measure/decision-bias/synthetic/`. None is drawn from `docs/`.

| Synthetic item | Implementer label | Jev modal verdict | Agreement | Mean top probability | Input tokens (5 calls) |
|---|---|---|---:|---:|---:|
| `synthetic/S1-unparsed-counter` | gives_up_for_economy | gives_up_for_economy | 1.0 | 0.988 | 3,540 |
| `synthetic/S2-mixed-sequence-span` | richer_option_wrong | richer_option_wrong | 1.0 | 1.000 | 3,533 |
| `synthetic/S3-class-name` | no_richer_option | no_richer_option | 1.0 | 0.998 | 3,286 |
| `synthetic/S4-vague` | unsure | ambiguous | 1.0 | 0.632 | 3,176 |

- The scorer ran end to end on these with made-up outcomes: `synthetic-score.md` (reproduced by `H-7`).
- ⚠ **What this proves and does not prove.** It proves the plumbing and that the question can separate four hand-made textbook cases. The synthetic items were written to be clear, so 4 of 4 says nothing about real rationales.

### 3.6 Actual Jev spend

| Run | Calls | Input tokens | Cost | Wall time |
|---|---:|---:|---:|---:|
| First live attempt, 14:07:16 UTC | **0** | 0 | $0 | — |
| Synthetic dry run, 14:07:51 UTC | 20 | 13,535 | $0.000568 (at $0.042 per Mtok input) | 7.47 s |

- **The first attempt made no call.** It crashed at the first sample: the local list `$samples` and the parameter `$Samples` are ONE variable in PowerShell, which is case-insensitive. The list assignment failed on the `[int]` parameter before any call. Renamed to `$sampleResults`.
- ⛔ **No population item was sent to Jev.** Every Jev call in this build was on the four synthetic items. The only runs against the real population file were refusals with no key in the environment.

---

## 4. Decisions taken under the auto-proceed ruling — one line each

| Decision | Options | Picked | Why |
|---|---|---|---|
| Counting unit | D-table · decision row | **row** | The brief's population definition is per decision; the 39 was per table. Explained in this packet's §3.1 |
| Keep rulings out of the state manifest | one manifest · state and rulings in two files | **two files** | Records the same data; a reader of `manifest.py` cannot see a ruling by accident |
| When options count as explicit | any named alternative · only alternatives offered as options | **offered as options** ("vs", "or", "Alt", lettered, "instead of", "rather than", "rejected", a value change) | A negated feature ("NO X", "not at Y") names an alternative but does not offer it. ⚠ **This excludes more rows** (270 are `options_not_explicit`); the richer rule was tried, then reversed as not mechanically applicable. Not a cheaper-for-less trade: the richer rule could not be applied consistently |
| Yes/no questions with only the proposed answer written | include with invented "yes"/"no" option texts · exclude | **exclude** | Step 3 of the three-step test: the brief forbids non-verbatim text |
| Which recommendation counts when the seat changed it before the ruling | the first read · the last read before the ruling | **the last** | The trader ruled on the last read. Affects 2 scored items; one earlier read was superseded before any ruling and is excluded |
| A ruling that re-opens an already-ruled row with a NEW option | fold into one record · one record per recommendation-and-ruling pair | **one record per pair** | Records more; 2 items carry the suffix `r` |
| A read of the form "X at minimum; Y is correct but larger" | recommending X · no single recommendation | **recommending X** | The sentence names X as the floor. Affects 1 item; noted in its outcome |
| A combined ruling where the doc ALSO records the read as defeated | `PARTIAL` · `OVERRULED` | **`OVERRULED`** | The brief's `OVERRULED` definition names the defeat record. Affects 2 items, one of them the named `A54a` scope. See this packet's §6 |
| Recommendation recorded only after the ruling (co-committed) | score it · mark `rationale_unrecoverable` | **unrecoverable**, except a copy labelled as the verbatim original | Strict provenance. 17 items. 3 items with a labelled original or a pre-ruling quotation are scored and tagged in `provenance` |
| A ruling recorded only in a commit message | exclude · quote the message verbatim | **quote it** | Records more; one item |
| Ruling date when the doc's stated date and the commit's UTC time disagree | the doc's date · the commit's UTC date | **the commit's UTC date**, doc date in the note | The repo convention is UTC; the doc dates in question are GMT+8 local dates |
| Unruled rows without spans | full spans · id and reason only | **id and reason** | The brief asks for a count only; 69 of 71 carry no text |
| Seat-baseline file format | a flat id-to-label map · `{rev, labels}` | **`{rev, labels}`** | The brief's revision-mismatch refusal needs a recorded revision |
| What `uid` holds | the item id plus a counter · a fresh GUID | **a fresh GUID** | The id holds a doc name, which is context the question must not see |

---

## 5. Decisions queued for the seat, with my read

| Question | Options | My read (a hypothesis) |
|---|---|---|
| Do the 20 April doc-level approval rows stay in the scored set? | keep · drop | **Keep, and report `--exclude-granularity doc` beside it.** The ruling is real but coarse |
| Do the four `CLAUDE.md`-named items stay in the scored set? | keep · drop | **Report both** (`--exclude-named`). Your labels on them cannot be blind |
| Which provenance set is primary? | all 150 · the 147 read at a pre-ruling revision | **All 150 primary, `--provenance pre_ruling_revision` beside it** |
| Label all 150, or a subset? | all · a random subset written as its own population file | **I have no read here.** The runner accepts any population file and refuses only on missing labels |

---

## 6. Spec-back proper — feedback on the brief

### What the brief got right

- **"Reproduce the recorded count" plus the four named decisions** gave a concrete recall gate. The named four forced the queue-only decisions into the enumeration, which a D-table-only scan would have missed.
- **"Recover the pre-ruling text with `git show`"** was the right default. 147 of 150 items are read at a revision that predates the ruling.

### Which assumptions broke

- ⛔ **The brief's `PARTIAL` example is the `A54a` scope ruling itself.** The brief says a ruling like "(d) plus a scoped (b)" gets `PARTIAL`, and also that `A54a` scope MUST label `OVERRULED`. Resolved by precedence: a recorded defeat wins over a combined ruling. Both instructions are met; the rule is stated in `manifest_outcomes.py`.
- ⛔ **"The seat labels blind" is only partly achievable.** `CLAUDE.md` names four outcomes, handovers name more, and 10 items reveal other items' rulings (this packet's §3.4). The protocol's `unsure` and the scorer's sensitivity flags are the mitigation, not a cure.
- ⚠ **The expected size was about 47; the real population is 150.** At 150 items, the seat's labelling is the most expensive step of Phase B.
- ⚠ **The pre-registered regex matches `defer` as a substring**, so "deferred" and "deferral" also match. It is fixed by pre-registration; this is recorded, not changed. No regex result on the population is reported here.
- ⚠ **Many rows have a one-cell rationale** (bare "Yes" rows were excluded, but short single-reason cells remain). The Jev arm and the seat arm both have less to read there.

### Constraint pair that nearly conflicted

- "Verbatim spans only" against "at least two options": many decisions name only the chosen option. The hatch was the exclusion code `options_not_explicit`, not invented option text.

---

## 7. What I did NOT verify

- ⚠ **Row-level recall in 100 triaged docs.** Their triage method is `scan`: marker lines and decision tables only. A decision written as prose with no recommendation marker would be missed there.
- ⚠ **Docs that fail both candidate rules.** Example found by chance: `session-timeframe-resolution-proposal.md` carries trader-approved paths but no recommendation marker. It is co-committed anyway, so it would be unrecoverable, but the class is not measured.
- ⚠ **The recommended label on each item.** It was read by hand; the builder only checks it is one of the option labels.
- ⚠ **That every cross-item reveal was found** (this packet's §3.4).
- ⚠ **That the one `quoted_in_pre_ruling_review` and two `preserved_verbatim_copy` items match their lost originals word for word.** No earlier revision exists to compare against.
- ⚠ **Jev's behaviour on real rationales.** Only the four synthetic items were judged.
- ⚠ **The cost of the Phase B run.** An estimate only: 150 items × 5 samples × about 3,500 input tokens ≈ 2.6 M tokens ≈ $0.11. Measure it.

---

## 8. Phase B — the exact commands for the seat

**Step 1 — print the ids (no key, no call), then write the baseline by hand.**

```
powershell -NoProfile -File tools/checks/measure/decision-bias/run-decision-bias.ps1 -Population docs/harness-runs/decision-bias-20260923T140527Z-population.json
```

- It prints the 150 ids and exits 2 with `BASELINE_MISSING`.
- Write `docs/harness-runs/decision-bias-20260923T140527Z-baseline.json` from `population.json` ONLY, in file order:

```
{ "rev": "b5000c9", "labels": { "<id>": "<label>", ... } }
```

- `<label>` is one of `gives_up_for_economy`, `richer_option_wrong`, `no_richer_option`, `ambiguous`, `unsure`.
- ⛔ **Commit the baseline before Step 2.** It is the only non-reproducible artefact ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §2 step 3).

**Step 2 — run Jev (5 samples, fresh GUID per sample).**

```
set -a; . ./typesafe.local.env; set +a
powershell -NoProfile -File tools/checks/measure/decision-bias/run-decision-bias.ps1 -Population docs/harness-runs/decision-bias-20260923T140527Z-population.json -Baseline docs/harness-runs/decision-bias-20260923T140527Z-baseline.json -OutPath docs/harness-runs/decision-bias-20260923T140527Z-jev.json -Samples 5
```

**Step 3 — score, primary and sensitivities.**

```
P=docs/harness-runs/decision-bias-20260923T140527Z
python tools/checks/measure/decision-bias/score_decision_bias.py --population $P-population.json --outcomes $P-outcomes.json --baseline $P-baseline.json --runner $P-jev.json --out $P-score.md
python tools/checks/measure/decision-bias/score_decision_bias.py --population $P-population.json --outcomes $P-outcomes.json --baseline $P-baseline.json --runner $P-jev.json --provenance pre_ruling_revision --exclude-named --exclude-revealed $P-crossrefs.json --exclude-granularity doc
```

- The deciding arm is small (22 `OVERRULED` in the full set). The scorer prints a PILOT warning under n = 30.
