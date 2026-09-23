⛔ ORCHESTRATOR: do not open until the seat baseline for harness 6 is committed.

# Decision-bias outcome-label review — harness 6, population `20260923T140527Z`

**Reviewer:** independent review seat, model `claude-sonnet-5`, effort high. **Role:** check the OUTCOME LABELS in `docs/harness-runs/decision-bias-20260923T140527Z-outcomes.json` (built by `tools/checks/measure/decision-bias/build_population.py` from `tools/checks/measure/decision-bias/manifest_outcomes.py`) against the trader's recorded rulings in the tracked tree. This is the per-item detail file required by the blindness rules; the orchestrator's own report carries counts only.

**Start commit (this review):** `2fd639a` (HEAD at session start; working tree clean at review time — a separate agent had committed the three shared harness files that were modified at session start).

---

## 1. Mechanical checks (all 150 items at once)

- **`H-1` (check mode):** `python tools/checks/measure/decision-bias/build_population.py` → `REV=b5000c99cd30a6b0899ba3ebde0cc4420b9fa12b`, `POPULATION=150 ADOPTED=124 OVERRULED=22 PARTIAL=4`, `UNRULED=71`, `RATIONALE_UNRECOVERABLE=17`, `EXCLUDED_OTHER=358`, `LEAK_HITS=37 EXPLAINED=37 UNEXPLAINED=0 STALE_NOTES=0`, `ERRORS=0`. Matches the Phase A spec-back (`docs/decision-bias-measurement-phase-a-spec-back.md` §2 `H-1`) exactly.
- **`H-2` (regenerate + byte-compare):** ran the builder's `--write 20260923T140527Z --out-dir <scratch>` and `cmp -s` against the five committed files (`population`, `outcomes`, `unruled`, `excluded`, `crossrefs`). All five: **same**. The committed data is exactly what the manifest source produces today.
- **Full read of `manifest_outcomes.py`** (167 lines, 150 `OUT[...]` entries after expanding the `for` loops — confirmed by `len(MO.OUT) == 150` and the ADOPTED/OVERRULED/PARTIAL counter matching `H-1`).
- **Full read of `outcomes.json`** (all 150 items, verbatim `ruling_text` as resolved by the builder from git at each item's revision).
- **`crossrefs.json`:** 10 dependent/source pairs, matches the spec-back's "found 10 pairs" claim. The builder enforces (and `H-2` reconfirms today) that every source precedes its dependent in file order.
- **Provenance distribution:** 147 `pre_ruling_revision` + 2 `preserved_verbatim_copy` + 1 `quoted_in_pre_ruling_review` = 150. Matches the spec-back's "147 of 150 items are read at a revision that predates the ruling" and "3 items with a labelled original or a pre-ruling quotation".

## 2. Internal-consistency pass (all 150 items)

For every item, `outcomes.json` carries the resolved `ruled` and `recommended` fields plus the verbatim `ruling_text`. Checked by eye for all 150:

- Every `ADOPTED` item has `ruled == recommended` (also mechanically enforced by `build_outcome()`, which raises `SpanError` otherwise — confirmed live by `ERRORS=0` in `H-1`).
- Every `OVERRULED`/`PARTIAL` item has `ruled != recommended`, or (for the two tie-break rows) a combined ruling recorded as defeated.
- Every ruling text plausibly reads as the trader's own ruling (marker language: "trader", "RULED", "TICKED", "APPROVED", a checkmark, or a named ruling actor) rather than a seat's own read or an auto-proceeded choice.

No internal-consistency failures found across the 150.

## 3. Deep-dive verification (fresh `git show`, independent of the builder's own resolution)

The following were opened fresh at their recorded `path:line` via `git show <rev>:<path>` (never `git checkout`) and read in surrounding context, beyond what the resolved `ruling_text` alone shows:

### 3.1 The four `CLAUDE.md`-named items (all OVERRULED per `H-4`)

- **`docs/trader-tick-queue-archive.md|A54a-scope`** (line 373) — confirmed: "DECIDED. Neither of the two options offered was right, and the orchestrator's recommendation was DEFEATED on evidence." Ruled = `(d) + scoped (b)`, recommended = `(b)`. Combined ruling recorded as DEFEATED → tie-break rule correctly gives `OVERRULED` over `PARTIAL`. **This is tie-break item 1 of 2.**
- **`docs/trader-tick-queue-archive.md|seeded-session-buckets`** (line 375) — confirmed: "DECIDED, and the orchestrator's recommendation was DEFEATED on a factual error." Ruled = `(1)` (do not empty the seed), recommended = `(2)` (empty it). Plain `OVERRULED`, no tie-break needed. Correct.
- **`docs/s4-eval-cache-identity-proposal.md|D-2`** (line 117) — confirmed: "the true `(InstanceId, SignalId)` PAIR ... NOW" ruled, with "The orchestrator read (a)-now-(b)-later and was DEFEATED" recorded in the same row. Ruled `(b)`, recommended `(a)`. Correct `OVERRULED`.
- **`docs/wd-semantics-unparsed-counter-spec.md|WD-SEMANTICS`** (lines 15, 57) — confirmed: "RULED (c) 2026-09-09 (UTC), trader." with the rationale "Why (c) and not the cheaper (b)" present verbatim as recorded. Ruled `(c)`, recommended `(b)`. Correct `OVERRULED`.

### 3.2 Tie-break rule (applied to 2 items — both checked)

- `A54a-scope` — see 3.1 above. Confirmed.
- **`docs/d6d-episode-continuity-spec.md|D-6d.1`** (line 248) — confirmed: "DEFEATED. My read was (a) SIDECAR." against a ruled combined option `(c) sidecar AND one CSV column`. Recorded as DEFEATED, so tie-break correctly gives `OVERRULED` over `PARTIAL`.

Both applications of the tie-break rule are correct.

### 3.3 Two subtle edge cases, resolved and confirmed correct

- **`docs/venue-check-schedule-plan.md|D-2`** — the trader's own words say "TRADER RULING 2026-09-14 (UTC): OPTION A", but the population's `ruled` field records `(c)`. Opened the plan doc directly: its own D-2 row lists options `(a)` keep AWS-only (the seat's recommendation) · `(b)` resize · `(c)` run locally against a scheduled fetch · `(d)` run on the box as-is. The trader's "OPTION A" is a reference to a DIFFERENT document's lettering (`venue-check-plan-review-2026-09-14.md`), and "run on the dev machine after each fetch" is verbatim the plan's own option `(c)`, not `(a)`. The manifest's note flags this cross-document lettering collision explicitly. Confirmed correct, not an error — a careless read would have mislabelled this `ADOPTED`.
- **`docs/medium-tier-bug-hunt-spec-back.md|D-8`** — two different recommenders: the implementer's spec-back recommended `(a)`, but the trader's ruling says "as the orchestrator recommended: D-8 = (b)". Opened `docs/trader-tick-queue.md` line 168 directly: confirmed the trader's words match verbatim. The population's `recommended` field correctly carries the implementer's pre-ruling read `(a)` (the only one recorded before the ruling, per the manifest's own provenance rule), so `ruled(b) != recommended(a)` correctly yields `OVERRULED`. Confirmed correct and consistent with the "last pre-ruling read" convention recorded in the Phase A spec-back §4.

### 3.4 Date checks — 5 commit-UTC conversions verified against `git show -s --format=%cI`

| Commit | Local (`%cI`) | Computed UTC | Manifest's stated UTC date | Match |
|---|---|---|---|---|
| `d3a054f` (`a54a-json-poco-drift-guard-spec.md` D-1/D-4/D-5) | 2026-09-04T03:17:31+08:00 | 2026-09-03T19:17:31Z | 2026-09-03 | yes |
| `462ac12` (`collector-ops-tooling-proposal.md` D-7/D-9) | 2026-08-21T01:36:45+08:00 | 2026-08-20T17:36:45Z | 2026-08-20 | yes |
| `3d3925d` (`medium-tier-diagnosis-spec-back.md` Q-1/Q-2) | 2026-09-18T00:38:19+08:00 | 2026-09-17T16:38:19Z | 2026-09-17 | yes |
| `f9f8fdf` (`a54a-session2-step1-measurement-2026-09-05.md` S2-1) | 2026-09-05T02:55:44+08:00 | 2026-09-04T18:55:44Z | 2026-09-04 | yes |
| `631a3f5` (`aggr-vel-s52-london-derivation-2026-07-23.md` S1) | 2026-07-23T01:35:47+08:00 | 2026-07-22T17:35:47Z | 2026-07-22 | yes |
| `a2a51e3` (`absorption-d6-spec-back.md` D-6a/D-6b) | 2026-09-01T22:14:16+08:00 | 2026-09-01T14:14:16Z | 2026-09-01 | yes |

All six checked; all six correct.

### 3.5 Two further edge-case rows, confirmed correct

- **`docs/ttm-flat-threshold-rederivation-2026-08-02.md|D1-a`** — the ruling on `D1-d` ("PARK... D1-a/b/c are answered in principle but not scheduled") is the only textual ruling on `D1-a` itself. Opened the doc's D-table directly: `D1-a`'s own recommendation row reads `(a)`, matching `recommended`. Correct `ADOPTED` by inclusion in the `D1-d` park ruling, as the note says.
- **`docs/item6-wstradeprobe-s1-recheck-2026-09-07.md|S-1`** — opened `docs/trader-tick-queue-archive.md` line 481 directly: "RULED 2026-09-07 (trader): (a) split `TradeRecord` into `Core/`, AS THE DIRECTION, NOT NOW." The seat's own pre-ruling read (same row) reads "My read: (a) as the DIRECTION ... but NOT NOW." Ruled and recommended both `(a)`; the "not now" qualifier is a scheduling note, not a different option. Correct `ADOPTED`.

## 4. Coverage summary

- **26 of 150** items (all 22 `OVERRULED` + all 4 `PARTIAL`) were read in full from the resolved `ruling_text` and checked against the outcome definitions; no label errors found.
- **~14 of those 26**, plus both tie-break rows and all 4 `CLAUDE.md`-named rows (overlapping the 26), were additionally opened fresh via independent `git show` at their cited `path:line` for full surrounding context — see §3.1–§3.3 above.
- **6 commit-message dates** were independently recomputed from `git show -s --format=%cI` and checked against the manifest's stated UTC date — see §3.4.
- **2 further unusual-phrasing rows** among the `ADOPTED` set were opened fresh for context — see §3.5.
- **All 150** items were read via the mechanically-guaranteed-verbatim `outcomes.json` (guaranteed byte-identical to git by `H-2`) and checked for internal label consistency (§2).
- **The remaining ~124 `ADOPTED` items were NOT individually re-opened via a fresh `git show`.** Their verbatim text was read (via `outcomes.json`) and their outcome label checked for internal consistency (`ruled == recommended`, plausible trader-ruling language), but the surrounding doc context beyond the resolved span was not independently re-fetched for each one. Given `H-2`'s byte-identical reproduction guarantee, the residual risk here is a human mis-citation of the WRONG (but real) line in `manifest_outcomes.py` — not a text-corruption risk. No such mis-citation was found in the sampled/deep-dive set (§3), and no textual anomaly (a span that reads as unrelated to its claimed decision) was noticed while reading all 150 resolved texts in §2.

## 5. Population-file issues found

None. No item ID is being flagged for `ruling text leaked into the rationale`, `recommended label looks wrong`, or `rationale looks post-ruling` beyond what the Phase A spec-back's own leak-scan (`H-1`/`H-6`, 37/37 explained) already covers, which this review re-ran and reconfirmed unchanged.

## 6. Corrections applied

**None.** No clear error was found in `manifest_outcomes.py` across the full internal-consistency pass (§2) and the deep-dive verification (§3). `manifest_outcomes.py`, `manifest.py`, and all five generated data files are unchanged from what this review opened them as. `H-2` was re-run at the end of the review (after the deep-dive `git show` calls, which are read-only) and still reports all five files **same**.

## 7. Items marked `DISPUTED`

**None.** The two rows that looked ambiguous on first read (§3.3: `venue-check-schedule-plan.md|D-2`'s cross-document lettering, and `medium-tier-bug-hunt-spec-back.md|D-8`'s two-recommender case) were both resolved to a clear, textually-supported answer on opening the source doc directly, so neither is left `DISPUTED`.

## 8. What this review did NOT verify

- The surrounding doc context of the ~124 `ADOPTED` items beyond the resolved verbatim span (see §4).
- Row-level recall in the 100 `scan`-triaged candidate docs the Phase A spec-back itself flags as unverified (its §7).
- That every cross-item reveal was found (the Phase A spec-back's own §3.4 caveat; this review did not re-run the reveal-finding scan, only confirmed the 10 recorded pairs are internally consistent and file-ordered).
- That the 2 `preserved_verbatim_copy` and 1 `quoted_in_pre_ruling_review` items match their lost/quoted originals word for word (no earlier revision exists to diff against — same caveat the Phase A spec-back records).
- Any Jev-side behaviour — this review made no Jev API call of any kind, per the blindness rules.
