# Doc scanner — build spec (harness 4)

**Created 2026-09-22 (UTC).** Harness 4 of the Jev programme ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6). **Written AFTER its measurement, per [`seat-handover-2026-09-22.md`](seat-handover-2026-09-22.md) §5:** [`doc-scanner-measurement-2026-09-22.md`](doc-scanner-measurement-2026-09-22.md). Every premise below is a measured number from that doc; read it first.

**Job:** find lines in the docs that claim to be current and are not — version rot, identifier misuse, stale state.

⛔ **NOT a gate.** Advisory, same three reasons as [`rider-travel-check-spec.md`](rider-travel-check-spec.md) `D-3`. Never wired into `verify-gate.ps1` or the pre-push hook.

---

## 0. ⭐ Model and effort (for the implementer)

**Model: Opus. Effort: medium.**

**Why.** The judgment design is done and measured, and every mechanical piece has an in-repo template: the candidate enumerators already exist and are tested ([`../tools/checks/measure/doc-scanner/`](../tools/checks/measure/doc-scanner/)), and [`../tools/checks/fixture-parser.ps1`](../tools/checks/fixture-parser.ps1) carries the baseline gates, the 5-sample loop and the report. What is new is small, but two pieces are subtle enough that today's own errors say not to drop a tier.

**Where it will slip — five traps.**

1. ⛔⛔ **Running Jev on the MEASURED window during acceptance.** That spends it for ever ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a, §5). The acceptance window is `-Docs` on historical docs only (§5 item 7). **Never run a keyed pass over the living set.**
2. ⛔ **The `E2` number-to-key pairing (§4.2).** The measurement's parser paired numbers with the wrong key on 13 of 13 `never_shipped` hits. Fixing it can silently drop true positives. **Acceptance measures BOTH directions** (§5 item 3).
3. ⛔ **Unstable item IDs.** A line number moves with every edit. The tool reads docs at a pinned revision (`-Rev`), and an ID carries that revision (§4.4).
4. ⛔ **Python string escapes.** A non-raw string containing `\b` or `\1` writes a control byte into the file — it happened twice in the measurement. **Use raw strings for every regex, and scan changed files for control bytes before committing.**
5. ⚠ **Quoted history in fix notes.** All three living-set `E1_strong` hits today are fix notes quoting the old value. That is the detector's job to call history — do not filter them out in code, or the tense question never sees its hardest case.

**Escalation trigger — stop and report.**

- §5 item 2 (parity with the measurement instrument) fails after one fix attempt.
- §5 item 3 leaves more than 2 `never_shipped` hits, or drops any of the 11 labelled `E2` true positives.

**Session split: none.**

---

## 1. Why this needs a judgment model — and where it does not

Measured ([`doc-scanner-measurement-2026-09-22.md`](doc-scanner-measurement-2026-09-22.md) §3–§4): **code catches every rot shape that has a mechanical source of truth**, and for three of those shapes the only thing code cannot decide is **tense** — does this line assert the value as CURRENT, or narrate HISTORY? `E2`'s 20 living-set hits split 11 current / 8 history / 1 unsure, exactly on that question.

⭐ **The split, as in harnesses 1–3: code enumerates and computes the truth; Jev judges tense or meaning; code decides.** Jev never sees `settings.json`, never counts, never compares dates — `docs.typesafe.ai/model-jaggedness/jev-1.13.md`'s first anti-pattern.

Arms with a mechanical answer and no tense question stay **code-only** (§4.1).

---

## 2. The population — measured, see the measurement doc

| | Value | Source |
|---|---|---|
| Docs | 420 top-level, 8.08 MB; 5 over Jev's 32k-token state | measurement §1 |
| Living set | **12 docs, 646,541 B** | measurement §2 |
| Candidates in the living set, arms kept by this spec | `E1_strong` 3 · `E2_was_shipped` 22 rows · `E6` 4 · fixture-ID meaning 65 mentions | measurement §2, §4 |
| Seeded live positives | **5**, left unfixed on purpose (trader-ruled 2026-09-22) | measurement §5 |
| Pre-registered seat labels | 111, at revision `cbc2c91` | [`harness-runs/doc-scanner-20260922T193818Z-prelabels.json`](harness-runs/doc-scanner-20260922T193818Z-prelabels.json) |

⚠ **These move. Smoke anchors, never assertions.**

---

## 3. Decisions

Auto-proceeded and recorded. **None RESERVED** — the tool reads docs and git history and writes a report. No settings key, no scoring, no rendered value, no collector or store write, no schema change.

| ID | Decision | Taken, and why |
|---|---|---|
| `DS-D1` | Scope | **The 12-doc living set, as a named list in the tool, plus the newest `seat-handover-*.md` by filename.** Measured: historical docs contribute noise, not defects (every `E1_strong` hit outside the set is a dated record). Deriving the set by parsing prose tables was rejected on **mechanism**: fragile, and a parse failure would silently shrink the scope. The tool prints the list on every run, so the scope is never implicit |
| `DS-D2` | Language | **The committed Python enumerators generate candidates; a PowerShell 5.1 script (`tools/checks/doc-scanner.ps1`) does the gates, the Jev calls through `tools/checks/lib/InvokeJev.ps1`, and the report.** A PowerShell port was rejected: the Python code is the measured, replayed instrument, and a port can only diverge from it. One HTTP call site stays one |
| `DS-D3` | Arms | **Keep** `E1_strong`, `E2`, `E6` (Jev, tense), the fixture-ID-meaning arm (Jev, meaning), `E3_cfg_member_missing`, `E3_line_past_eof` and dated-state age (code-only). **Drop** `E1_weak_cue` (0 of 20), `E3_missing_file` (0 of 20), and `E4`'s absent-ID check (all hits were one namespace overlap). Each drop is measured |
| `DS-D4` | `E2` pairing | **Fix the pairing in code before any judge sees it** (§4.2). `never_shipped` hits are parser errors, never a tense question |
| `DS-D5` | Self-consistency | **5 samples, agreement rate on every row.** ⛔ **And a stable row is not a correct row** ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4g) — the report never collapses to a pass/fail count |
| `DS-D6` | Baselines | **File-level AND item-level refusal**, the `FP-D24` pattern from `tools/checks/fixture-parser.ps1`: no baseline file → exit 2; any judged item absent from it → exit 2, `BASELINE_INCOMPLETE`, before any Jev call. `-AllowUnbaselinedItems` for routine re-runs only |
| `DS-D7` | Revision pinning | **Docs are read with `git show <Rev>:<path>`, never from the working tree.** `-Rev` defaults to `HEAD`; the baseline file records its revision, and a run refuses when they differ (IDs would not align) |
| `DS-D8` | Acceptance window | **`-Docs` restricted to historical docs outside the living set** (§5 item 7). Disjoint by CONTENT, not just by revision — an older revision of a living doc shares most lines with today's |
| `DS-D9` | Dated-state arm | **Code-only in v1.** One typed claim gets a computed truth: *"next free fixture family"* against the highest `A\d+` family in `verify/ordercheck/Program.vb`. Every other dated state section is reported by age (horizon **7 days, the measured value** — any other value is unmeasured), not judged: measured 4 true / 3 false / 5 unsure on age alone |

---

## 4. What to build

### 4.1 Arms

| Arm | Candidates (code) | Code computes | Jev asks | Finding |
|---|---|---|---|---|
| `VERSION` (`E1_strong`) | settings version quoted in a current form, below the live one | live version; the doc's version | **Q-TENSE** | `asserts_current` |
| `VALUE` (`E2`) | a settings key with a paired number ≠ live value, and that number **was** once shipped | live value; the ever-shipped set; when the doc value last shipped | **Q-TENSE** | `asserts_current` |
| `POINTER` (`E6`) | a "current / state read / start here" line linking a handover older than the newest | the newest handover filename | **Q-TENSE** | `asserts_current` |
| `FIXTURE_MEANING` | an `A\d+[a-z]` ID in the doc that resolves in `Program.vb` | the fixture's `Sub` name and every `Check` title with that ID | **Q-MEANING** | `describes_something_else` |
| `CFG_MEMBER` · `LINE_PAST_EOF` | `E3` shapes | existence | — | code-only |
| `DATED_STATE` | dated state headers over the horizon; the "next free fixture family" claim | age; the highest fixture family | — | code-only |

### 4.2 The `E2` pairing fix

The measurement parser took the first number within 22 characters after a key name. Required instead:

1. The number must follow the key with **only** `` ` `` `*` `(` `=` `:` whitespace between them, or sit inside the same backtick span.
2. A second key name between the key and the number **cancels** the pair.
3. A slash pair — `` `A`/`B` 1.75/1.6 `` — pairs by position.
4. `never_shipped` hits that survive are **reported code-only**, never sent to Jev.

### 4.3 The questions

**Q-TENSE**, per `VERSION` / `VALUE` / `POINTER` candidate. State: doc path · heading chain · the line · two lines either side · a code-written `claim` sentence (for example *"The line gives `buy_dominant_ratio` = 2.0. The live value is 1.6. 2.0 was last shipped at v47."*).

| ID | Type | Asks |
|---|---|---|
| `verdict` | choice | `asserts_current` — the line states this as the value or pointer in force now · `history_or_quote` — it narrates a past state, quotes an old value, or explains a correction · `ambiguous` |

**Q-MEANING**, per `FIXTURE_MEANING` mention. State: the line · two lines either side · the fixture ID · its `Sub` name · its `Check` titles.

| ID | Type | Asks |
|---|---|---|
| `verdict` | choice | `describes_this_fixture` · `describes_something_else` · `no_description` (a bare ID) · `ambiguous` |

⛔ **`verdict` is the only answer code reads.** No Noul is combined with another.

### 4.4 Item IDs

`<arm>|<path>|<line>@<rev7>` — for example `VALUE|docs/DeribitIndicatorProject.md|75@cbc2c91`. A line with two candidates of one arm gets a `#n` suffix.

### 4.5 Coverage block — a broken enumerator is loud

```
REV=<sha7>
LIVING_DOCS=<n>  (list printed)
CANDIDATES_VERSION=<n>  CANDIDATES_VALUE=<n>  CANDIDATES_POINTER=<n>  CANDIDATES_FIXTURE_MEANING=<n>
VALUE_NEVER_SHIPPED_CODE_ONLY=<n>
CFG_MEMBER_MISSING=<n>  LINE_PAST_EOF=<n>  DATED_STATE_OVER_HORIZON=<n>  NEXT_FREE_FAMILY_STALE=<0|1>
ITEMS_JUDGED=<n>  JEV_CALLS=<n>  USAGE_INPUT_TOKENS=<n>  WALL_TIME_SEC=<n>
```

⛔ **Exit 2 with `ENUMERATOR_SUSPECT` when `LIVING_DOCS` < 12 or all four Jev arms return zero candidates.** A zero is the tripwire, not a clean bill ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §5).

### 4.6 Steps

1. Enumerate at `-Rev` over the living set (Python). Emit candidates JSON with the computed facts.
2. Print the coverage block, then the candidate list **with no judgments**.
3. Refuse without a baseline file; refuse on any unbaselined judged item (`DS-D6`); refuse on a revision mismatch (`DS-D7`).
4. Jev judges, 5 samples each.
5. Report: per item, the verdict, agreement rate, top probability, and the baseline value with AGREE / DISAGREE. Exit.

**Exit codes:** 0 — no finding and every row stable · 1 — any finding or any unstable row · 2 — a gate, a suspect enumerator, or an API failure.

---

## 5. Acceptance

1. **Code-only recall parity:** a `-Replay` mode reproduces `tools/checks/measure/doc-scanner/replay_recall.py`'s four results for the kept arms. Paste both outputs.
2. **Code-only candidate parity at `cbc2c91`:** the living-set counts for `VERSION`, `POINTER`, `CFG_MEMBER` and `LINE_PAST_EOF` equal the measurement's (3 · 4 · 0 · 0). Paste them.
3. ⛔ **The pairing fix, both directions, at `cbc2c91`:** `never_shipped` on the living set falls from 13 to **≤ 2**, AND all 11 lines labelled `true_positive` for `E2` in the pre-registered labels file **stay flagged**. Paste both numbers.
4. The five seeded rows (measurement §5, excluding the fixed `trader-tick-queue.md` row) all surface at `cbc2c91`: the `DeribitIndicatorProject.md` OFI line and the ten `UserManual.md` lines as `VALUE` candidates; the `A73` claim as `NEXT_FREE_FAMILY_STALE=1`; the `A5` lines in `roadmap.md` and `backlog-dependency-map.md` and the `roadmap.md` §2 snapshot as `DATED_STATE`. Paste every ID.
5. Running without a baseline exits 2 with **no API call**; an incomplete baseline exits 2 with `BASELINE_INCOMPLETE`; a revision mismatch exits 2. Paste all three.
6. `verdict` alone is read — one line, confirmable by reading. `Invoke-Jev` is shared, not copied.
7. ⛔ **A live acceptance run ONLY on `-Docs` naming historical docs outside the living set** (for example `seat-handover-2026-08-2*.md`). Per-item verdicts may be reported for that window only. ⛔⛔ **DO NOT run a keyed pass on the living set, and DO NOT write a baseline for it.** The seat writes that ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4c). Stop at the refusal and paste it.

---

## 6. The first measured run — the seat's, not the implementer's

- **Revision `cbc2c91`**, living set. The seat's labels for `VERSION`, `VALUE`, `POINTER` exist already (pre-registered); **map them to the tool's item IDs before the run** — a label that does not map is recorded, not guessed.
- **`FIXTURE_MEANING` has no labels yet.** 65 mentions. The seat writes them first, from the same state Jev gets.
- The five seeded positives are the recall check. They must not be fixed before this run.

---

## 7. What this spec does NOT verify

- **That Jev can judge tense on these lines.** Not measured; no Jev call was made in the measurement, on purpose.
- **Rot shapes no arm sees:** a doc contradicting itself, forward-looking claims, state superseded by events rather than age, file-size claims. Measured as missed; out of v1.
- **Docs outside the living set.** Rot in a historical record is not treated as a defect.
- **The living-set list staying right.** It is a named list; a new living doc must be added by hand, and the tool prints the list so a reader can see it.
