# Harness 6 — seat baseline notes (2026-09-23 UTC)

**Baseline:** [`decision-bias-20260923T140527Z-baseline.json`](decision-bias-20260923T140527Z-baseline.json). Written by the orchestrator seat (`claude-opus-5-5`) from the population file only, before any Jev call on the population and without opening `outcomes.json`, `manifest_outcomes.py`, `excluded.json` or `unruled.json`.

**Counts:** 150 labelled — `no_richer_option` 102 · `gives_up_for_economy` 25 · `richer_option_wrong` 14 · `ambiguous` 8 · `unsure` 1.

## ⚠ Declared contamination — read before scoring

1. **Recognised rulings: 46 of 150.** I knew the trader's ruling on these from memory or from docs read this session (`CLAUDE.md`, `DeribitIndicatorProject.md` §15, `trader-tick-queue.md`, the memory index). The labels were still written from the state text alone. IDs: [`decision-bias-20260923T140527Z-seat-recognised.json`](decision-bias-20260923T140527Z-seat-recognised.json). **Score with and without them.** This is far more than the four named in `CLAUDE.md`.
2. **Read-ahead inside chunks.** I read the population in five chunks of 30 items and labelled each chunk after reading it. Within a chunk, a later item could reveal an earlier one's ruling. Seen: the `D-5` sub-decisions of `coverage-trailing-edge-f1-proposal.md` (items 99–103) state that `D-5` went to (c), and I labelled item 96 after reading them. The builder's `crossrefs.json` pairs are all affected the same way.
3. **Self-flagging rationales.** Rationales written after the 2026-09-11 auto-proceed ruling often name the reserved tell themselves, for example *"flagged as the reserved pattern"* or *"'Defer' is the reserved tell, named"*. Both Jev and the regex can match on those words rather than on the shape. **Split the score at 2026-09-11** before reading anything into the post-ruling half.

## What the labels mean

- The label classifies the author's stated reason, not whether the reason was right. Example: item 106 (`A54a` scope) is labelled `richer_option_wrong`, because its stated reason is mechanical: a guard needs a hand table that is itself a drifting copy. The trader overruled it, but the option that won, a reflection guard, was not on the list. **If the tripwire misses this item, that is a limit of the question, not only of the detector.**
- `unsure` (item 45): the threshold choice itself has no richer option, but the rationale defers a named cleaner fix, the time-anchored window. The label depends on whether "another option" includes one the rationale mentions but the question does not list.
