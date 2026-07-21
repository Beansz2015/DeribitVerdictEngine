# Eval Display Semantics — Success Orientation + WEAK Exclusion + Band Vocabulary (F2/F3/F12) · Proposal

**Date:** 2026-07-21 · **Status:** PROPOSED — E-table awaits trader (open questions marked ❓) · **Sequence: after the F4 fix** (`eval-no-data-outcome-proposal.md`) — this pass changes how rates READ; F4 changes what they ARE.
**Evidence:** `offline-matrix-placed-target-spec-back.md` §8 F2 (strip = success rate, report = failure rate — mental inversion between surfaces), F3 (strip counts WEAK, 62.5% of a sampled block's denominator, though WEAK is never traded — the bridge refuses it by contract), F12 (the three confidence bands carry four spellings; the middle band is never called MEDIUM on any surface the trader reads — this cost a real round-trip when "STRONG vs LONG" and "STRONG vs MEDIUM" turned out to be the same finding).
**Type:** display/report semantics — zero scoring impact, no ⚠ boundary. **One hard invariant:** the auto-tweaker's failure-oriented trigger comparison (`aggregateRatePct < FailureRateThresholdPct`) must NOT flip — internal truth stays failure-oriented; conversion to success happens ONLY at render boundaries.

## 1. F2 — unify on SUCCESS (trader-called)

Report render text flips to success rates at the `MarkdownReportWriter` boundary: §2 matrix, §3 before/after, §4 recommended, §5 decomposition (already counts), §6 context×outcome, §7 hold-window, §8 pending, the summary CSV column name, interpretation blurbs, and **`PromptBuilder`'s matrix table + surrounding prose together** (flipping the table alone would actively mislead the tweaker's LLM reasoning). `FailureCellResult.Failures/FailureRate` stay the internal truth; ★ (most-precise) / ◆ (best-outcome) pick semantics unchanged, captions re-worded. The strip already shows success — untouched by F2.

## 2. F3 — strip excludes WEAK at display time (trader-called)

`IsEligibleVerdict` stays permissive at STORAGE (the cache keeps WEAK rows — reversible, no rotation); the exclusion applies in `BuildWindowAggregate`/display filtering, keyed off the verdict band. Makes the strip ≡ the matrix population (STRONG+MEDIUM) — the §7a cross-check becomes like-for-like.
- ❓ **E2a — WEAK visibility:** drop entirely, or a separate **dimmed WEAK cell/tooltip line**? F1 shows WEAK currently *out-performing* MEDIUM (not significant, but information). Recommendation: **tooltip line, not a cell** (`WEAK excl.: 39% (n=…)`) — keeps the headline clean and the information reachable.
- ❓ **E2b — `min_sample_for_render`:** the denominator drops ~2.6×; the current floor of 4 is far too low for a rate moving in ~3pp steps. Recommendation: **10** (session cells show `--%` a bit longer, and mean it).

## 3. F12 — band vocabulary (canonical: HIGH / MEDIUM / LOW)

The frozen wire enums are untouched (`confidence HIGH|MEDIUM|LOW` — verified against 8,025 rows; re-spelling costs a schema bump for cosmetics). Changes are display/docs only:
1. **The middle band gets named where humans read it.** The bare `LONG`/`SHORT` verdict is the root hazard. ❓ **E3a — format:** recommendation: the verdict header line renders the band beside every directional verdict — `LONG · MEDIUM`, `STRONG LONG · HIGH`, `WEAK SHORT · LOW` — on **snapshot + card in the same commit** (display-parity rule; this is NOT strip-only). The CSV `Verdict` column and payload strings are UNCHANGED (book continuity + frozen contract). Alternative if the trader finds the suffix noisy: rely on the existing CONFIDENCE line + docs only — but that line's distance from the verdict is exactly what failed us this week.
2. **Report legend:** one line in the matrix header mapping tier ↔ verdict string ↔ band (`STRONG_*↔STRONG*↔HIGH · MEDIUM_*↔bare*↔MEDIUM · (WEAK excluded)`).
3. **Docs:** UserManual/TraderGuide state the canonical band names once, and F11's leftover (§18 quoting the retired threshold constants) gets corrected in the same pass.
4. ❓ **E3b — `scoring.tier_floor` rename** (reads as tier vocabulary; is actually the TRANSITIONAL penalty floor on raw score — a false friend): rename to `penalty_floor.*` needs POCO + tweaker-fence + snapshot-compat care. Recommendation: **backlog it** as its own micro-spec (P-class), not this pass — record here so it isn't lost.

## 4. Riders

- **F7:** session-cell tooltip gains the word "block" (`most-recent London block, 0/26`) — kills the "London is broken" misread.
- The §7a cross-check recipe (strip replication + cache↔CSV join + `.0000000Z` provenance) is referenced from the report header as the standing parity instrument.

## 5. E-table

| # | Decision | Recommendation / ❓ |
|---|---|---|
| **E1** | Success orientation at render; internal failure-truth + tweaker comparison untouched | As §1 |
| **E2a** ❓ | WEAK visibility after exclusion | Tooltip line (not a cell, not discarded) |
| **E2b** ❓ | `min_sample_for_render` | 10 |
| **E3a** ❓ | Band label placement | Verdict-line suffix `· MEDIUM` on snapshot+card |
| **E3b** ❓ | `tier_floor` rename | Backlog (own micro-spec) |
| **E4** | Sequencing / model | After F4 ships; **Opus, medium**; one conversation; spec-back `eval-display-semantics-spec-back.md` |

## 6. Acceptance

Builds 0/0; harness unregressed (A27c-class report-heading pins will re-pin); fixtures: tweaker trigger comparison unchanged under the flipped render (a BELOW_THRESHOLD case stays BELOW_THRESHOLD); WEAK-excluded aggregate vs WEAK-inclusive tooltip; band suffix renders on both surfaces (parity fixture); report legend + success captions. Live smoke: strip NY cell ≈ the directional-only rate from the last cross-check (~33% at the time — F4 may have moved it).
