# Eval Display Semantics — Success Orientation + WEAK Exclusion + Band Vocabulary (F2/F3/F12) · Proposal

**Date:** 2026-07-21 · **Status:** APPROVED — E1–E4 ALL TICKED 2026-07-21 (§5; E3a revised same day to DISPLAY-RENDERING only), build-authorized · **Sequence: after the F4 fix** (`eval-no-data-outcome-proposal.md`) — this pass changes how rates READ; F4 changes what they ARE.
**Evidence:** `offline-matrix-placed-target-spec-back.md` §8 F2 (strip = success rate, report = failure rate — mental inversion between surfaces), F3 (strip counts WEAK, 62.5% of a sampled block's denominator, though WEAK is never traded — the bridge refuses it by contract), F12 (the three confidence bands carry four spellings; the middle band is never called MEDIUM on any surface the trader reads — this cost a real round-trip when "STRONG vs LONG" and "STRONG vs MEDIUM" turned out to be the same finding).
**Type:** display/report semantics — zero scoring impact, no ⚠ boundary. **One hard invariant:** the auto-tweaker's failure-oriented trigger comparison (`aggregateRatePct < FailureRateThresholdPct`) must NOT flip — internal truth stays failure-oriented; conversion to success happens ONLY at render boundaries.

## 1. F2 — unify on SUCCESS (trader-called)

Report render text flips to success rates at the `MarkdownReportWriter` boundary: §2 matrix, §3 before/after, §4 recommended, §5 decomposition (already counts), §6 context×outcome, §7 hold-window, §8 pending, the summary CSV column name, interpretation blurbs, and **`PromptBuilder`'s matrix table + surrounding prose together** (flipping the table alone would actively mislead the tweaker's LLM reasoning). `FailureCellResult.Failures/FailureRate` stay the internal truth; ★ (most-precise) / ◆ (best-outcome) pick semantics unchanged, captions re-worded. The strip already shows success — untouched by F2.

## 2. F3 — strip excludes WEAK at display time (trader-called)

`IsEligibleVerdict` stays permissive at STORAGE (the cache keeps WEAK rows — reversible, no rotation); the exclusion applies in `BuildWindowAggregate`/display filtering, keyed off the verdict band. Makes the strip ≡ the matrix population (STRONG+MEDIUM) — the §7a cross-check becomes like-for-like.
- **E2a — WEAK visibility (TICKED):** a tooltip line, not a cell — joins the **EXISTING** `_perfTip` tooltip (`WEAK excl.: 39% (n=…)`); keeps the headline clean and the information reachable. Rationale kept: F1 shows WEAK currently *out-performing* MEDIUM (not significant, but information).
- **E2b — `min_sample_for_render` (TICKED): 10.** The denominator drops ~2.6×; the old floor of 4 was far too low for a rate moving in ~3pp steps. Session cells show `--%` a bit longer, and mean it.

## 3. F12 — band vocabulary (**E3a FINAL 2026-07-21 — REVISED same day: DISPLAY-RENDERING change only**)

**Final E3a (trader re-weighed on the blast-radius inventory):** the snapshot + card **render** the middle band as `MEDIUM LONG` / `MEDIUM SHORT` (the on-screen ladder reads STRONG / MEDIUM / WEAK explicitly — exactly what the trader wants to see) while the **stored/wire strings stay `LONG` / `SHORT` unchanged** (CSV, payload, eval cache, fixtures' stored-string pins, every string-matching site — all untouched). Display-form vs stored-form divergence has precedent (cap-reason rich string vs CSV bucket); the two render sites change in the SAME commit (parity rule) and the commit message states the mapping. **The rename inventory below is RETAINED FOR THE RECORD as the road not taken** — decision-of-record: a genuine rename was considered and rejected because the motivating failure was screen-side, the machine surfaces already carry the canonical `confidence` key, and a rename would create a permanent CSV vocabulary era-boundary + a forever both-era canonicalization tax. **Consequences of the revision: the order-app heads-up prerequisite is CANCELLED (nothing on the wire changes); the CSV vocabulary-boundary note is CANCELLED; the fixture sweep shrinks to the two render surfaces' pins.** Report legend gains the one mapping line: `MEDIUM_x ↔ displayed "MEDIUM x" ↔ stored "x" ↔ payload MEDIUM · STRONG↔HIGH · WEAK excluded from the matrix`.

*(Superseded rename-scope inventory, kept for the record:)*

**The middle-band verdict strings rename: `LONG` → `MEDIUM LONG`, `SHORT` → `MEDIUM SHORT`.** The ladder reads STRONG / MEDIUM / WEAK explicitly on every surface. This is a REAL string change (not a display suffix), so its scope is wider than the original recommendation — inventoried here so nothing is discovered mid-build:

- **One canonical constant set** for the verdict strings; every producer reads it (`ScoringEngine_Calculate_Verdict` incl. any bracketed-lean forms that can carry the middle band).
- **Both-era canonicalization at EVERY string-matching consumer** — historical rows/caches keep `LONG`/`SHORT` forever: `FailureRateMatrix.CanonicalTier` (public since the migration — the one mapping), `LivePerformanceTracker.IsEligibleVerdict`/`IsLongVerdict`, `AutoTweakerCore`'s tier-eligibility filter, `RoundStatsBuilder`, the what-if runner's verdict re-derivation, `UpdateVerdictLabel`'s colour mapping, `CalibrationReport`. Fixture sweep: every harness literal pinning a bare `LONG`/`SHORT` verdict re-pins.
- **All four parity surfaces move together in one commit** (same field: snapshot, card, payload `verdict`, CSV `Verdict`) + §15 row + a **dated CSV-vocabulary-boundary note** (v31/v36 semantic-note precedent; NO settings bump — no keys change). Eval cache: no rotation (stored strings stay; canonicalization handles both eras).
- **HARD PREREQUISITE — order-app heads-up BEFORE the engine ships:** the payload `verdict` string changes vocabulary. Actionability is contract-safe (`direction` + `confidence` untouched — the R1 action keys), but the consumer must CONFIRM nothing parses `verdict` literally (the F12(b) trap). Paste-ready text for the trader to send:
  > Heads-up per the F12 vocabulary fix: the engine's `verdict` payload field renames its middle band — `LONG`→`MEDIUM LONG`, `SHORT`→`MEDIUM SHORT` (STRONG/WEAK unchanged, `NO TRADE*` unchanged). `direction` and `confidence` — your action keys — are untouched. Please confirm nothing on your side string-matches `verdict` (the contract routes actionability through direction+confidence); your disposition log will simply start echoing the new strings. Mixed vocabulary will appear across the rename date in soak-log joins — join on (instance_id, signal_id) as always.
- **Mid-soak note for the reviewer:** the soak-log joins straddle the rename date; id-based joins are unaffected, vocabulary in `would-act` lines mixes. Expected.
- **Report legend (per the rename, now minimal):** one line in the matrix header — `Tiers: STRONG_x ↔ "STRONG x" (payload HIGH) · MEDIUM_x ↔ "MEDIUM x" (payload MEDIUM) · WEAK excluded from the matrix` — the underscore↔space mapping is now self-evident; the line mainly pins the payload-confidence correspondence (STRONG↔HIGH is the one non-obvious pair).
- **Docs:** UserManual/TraderGuide verdict tables + CLAUDE.md §5 ladder + F11's leftover (§18 retired constants) in the same pass.

**E3b — TICKED: `scoring.tier_floor` stays as-is** (trader 2026-07-21). The guard note REMAINS load-bearing: `tier_floor` is the TRANSITIONAL penalty floor on raw score, NOT tier vocabulary — documented in the spec-back's F12 near-miss so no future seat "harmonizes" it into the ladder.

## 4. Riders

- **F7:** session-cell tooltip gains the word "block" (`most-recent London block, 0/26`) — kills the "London is broken" misread.
- **Docs ride this pass (STILL in scope under display-only E3a — the screen ladder changes even though the strings don't):** UserManual/TraderGuide verdict tables gain the displayed forms (`MEDIUM LONG` / `MEDIUM SHORT`) with the display↔stored mapping stated; the `DeribitIndicatorProject.md` §5 verdict-levels ladder gets the same note *(the superseded inventory's "CLAUDE.md §5 ladder" pointed here — CLAUDE.md has no §5)*; **+ F11's leftover** (`UserManual.md` §18 retired threshold constants, ~line 1861) fixed in the same pass.
- The §7a cross-check recipe (strip replication + cache↔CSV join + `.0000000Z` provenance) is referenced from the report header as the standing parity instrument.

## 5. E-table

**ALL TICKED 2026-07-21:**

| # | Decision | Ticked |
|---|---|---|
| **E1** | Success orientation at render; internal failure-truth + tweaker comparison untouched | ✅ as §1 |
| **E2a** | WEAK visibility after exclusion | ✅ line added to the **EXISTING** perf-strip tooltip (`_perfTip` — no new tooltip; trader note) |
| **E2b** | `min_sample_for_render` | ✅ **10** |
| **E3a** | Band naming | ✅ **REVISED 07-21 (same day): DISPLAY-RENDERING only** — snapshot+card render `MEDIUM LONG`/`MEDIUM SHORT`; stored/wire strings UNCHANGED; order-app prerequisite CANCELLED; rename inventory retained as decision-of-record |
| **E3b** | `tier_floor` | ✅ **remains as-is** (guard note stays) |
| **E4** | Sequencing / model | After F4 ships (no external dependency remains); **Opus, medium**; spec-back `eval-display-semantics-spec-back.md` |

## 6. Acceptance

Builds 0/0; harness unregressed (A27c-class report-heading pins will re-pin); fixtures: tweaker trigger comparison unchanged under the flipped render (a BELOW_THRESHOLD case stays BELOW_THRESHOLD); WEAK-excluded aggregate vs WEAK-inclusive tooltip; band **prefix** (`MEDIUM LONG` / `MEDIUM SHORT` — not the superseded `LONG [MEDIUM]` suffix form) renders on both surfaces (parity fixture); **stored-form pins — the revision's load-bearing invariant:** CSV `Verdict`, payload `verdict`, and eval-cache strings still carry bare `LONG`/`SHORT` (fixtures assert the NON-change); report legend + success captions. Live smoke: strip NY cell ≈ the directional-only rate from the last cross-check (~33% at the time — F4 may have moved it).
