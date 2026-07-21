# Fable Coordinator Continuation — 2026-07-21 (mid-context port)

**For:** the next Fable coordinator conversation (this one hit context limits mid-flight). **Read AFTER the session-start protocol and `seat-handover-2026-07-18.md`** — that doc remains the standing queue/rules authority (Fable seat runs through ~Aug 1; Pro downgrade AUG 2 = the real handoff; Opus implements throughout). This brief carries ONLY what changed since it was written.

## 1. What landed since the seat-handover (all coordinator-reviewed, gate re-run each time)

1. **Manuals + what-if manual fold-in: PUSHED** (origin current as of 07-20 before the matrix work).
2. **Placed-target matrix migration: SHIPPED + APPROVED** (`offline-matrix-placed-target-spec-back.md` — READ ITS §7a/§8 IN FULL; it is the richest artifact of the week). Threshold axis retired, cell space = (tier × window), favourable barrier = logged `PlacedTarget*`. Implementer decisions all accepted — notably the UNFLOORED placed target and the EXACT min-move test (A32d pins the floored-grid impossibility). **Geometry parity 1335/1335 exact** (tracker barriers ≡ logged Placed* — the measurement stack is now one truth). WhatIfRunner had been broken since #6 (missing Compile Include) — fixed in passing; the gate hole it exposed (F10) rides the F4 spec.
3. **The F-series findings** (spec-back §8; none caused by the migration — the shared geometry made them legible). Processed as:
   - **F4 → `eval-no-data-outcome-proposal.md` — N1–N5 ALL TICKED, BUILD-AUTHORIZED, buildable NOW** (Opus medium-low). Empty bar-lists were recorded as live failures (offline already excludes them — same condition, opposite handling; 07-03 = 22 fabricated expiries). Fix: `NO_DATA` outcome + denominator exclusion + v5→v6 load-time reclassification sweep. Riders: F10 (WhatIfRunner → verify-gate build set) + N4 (RoundStatsBuilder migrates to placed geometry — the last synthetic yardstick).
   - **F2/F3/F12 → `eval-display-semantics-proposal.md` — E1–E4 ALL TICKED (E3a revised same-day), builds AFTER F4** (Opus medium). E1: report flips to SUCCESS at render only — **the tweaker's failure-oriented trigger comparison must NOT flip** (named invariant). E2a: strip excludes WEAK at display time; WEAK line joins the EXISTING `_perfTip` tooltip. E2b: `min_sample_for_render` 4→10. **E3a FINAL: display-rendering only** — snapshot+card render `MEDIUM LONG`/`MEDIUM SHORT`, stored/wire strings UNCHANGED (the trader first chose a genuine rename; on the blast-radius inventory — permanent CSV era-boundary, both-era canonicalization tax, order-app dependency — they reverted to display-only; the rename inventory is retained in the spec as decision-of-record). Order-app heads-up CANCELLED. E3b: `tier_floor` stays (the guard note is load-bearing: it is a raw-score penalty floor, NOT tier vocabulary).
   - **F1 → RULED, not specced:** the tier ladder (STRONG>MEDIUM>WEAK) is **UNVERIFIED in either direction** — the trader's STRONG-worse-than-MEDIUM and the cross-check's MEDIUM-worse-than-WEAK are both small-n noise (STRONG n≈90 book-wide; every pairwise z < 1.2). **Gates recorded in roadmap W6-3 + levers L4: Kelly CAL must NOT be built on today's book (it would fit noise); re-read at n≥150 STRONG rows, via the OFFLINE report (not the eval cache — F4), after F4 ships. Still unordered at n≥150 ⇒ its own scoring-quality spec.** Also: the trader's session-policy P5 values ("trade MEDIUM only") should be set AFTER the F1 re-read, not before — flag it at the live-ladder step.
   - F5–F11 recorded in the spec-back (F8's `.0000000Z` backfill-signature diagnostic is reusable; F7 tooltip "block" wording rides the display pass; F11 manual leftover rides it too).

## 2. Immediate queue (in order, with dates)

1. **F4 build** — kickoff NOW: *"Implement docs/eval-no-data-outcome-proposal.md"* (Opus medium-low). Coordinator-review its spec-back (re-run the gate yourself — every review this month caught something).
2. **Display pass** — after F4: *"Implement docs/eval-display-semantics-proposal.md"* (Opus medium). No external dependency (E3a revision cancelled it).
3. **Bridge soak review ~Jul 26–30** — recipes: bridge proposal §5.7 + month-handover Q4 + the 07-18 addendum (`refused: policy` third token, if the order app shipped the session-policy gate — its P1–P5 were handed over 07-20). The 07-17 emission gap appears in the joins (documented). Mixed vocab note is MOOT (E3a revision).
4. **#6 activation gates ~Jul 29–31** — `book-absorption-proposal.md` §5: independence (the #5-gate Spearman/overlap method — reuse the 07-13 verdict-doc pattern) + ≥10pp gradient on n≥30 flagged evaluated rows (**post-F4 eval data only**). Verdict doc either way; pass ⇒ activation ⚠ D-table (Step-2 penalty + breakdown row snapshot+card SAME commit + §12 watch).
5. **Funding calm-week re-read** (whenever a calm stretch appears): FLAT 60–70 / 3b 15–25 per-resolution; hot-week readings 07-17 were mechanism-PASS, bands deferred.
6. **res-3 §5.2** (~150 fires/session; late Jul) · **W6-1 LONDON ruling ~Aug 1** (what-if grid on the candidates; ~600–800 more LONDON rows wanted) · **Aug-1 handover doc** (the credit-rationed month; this seat's last deliverable) · W6-4 prep (~8–10k rows, early Aug).

## 3. Trader-pending at this moment

- Test + push the local stack (**ahead ~8**: the matrix-migration commits + F-series specs + ticks + this brief).
- Kick off F4 (item 1) whenever ready.
- (Standing) session-policy gate implements in the order-app repo; policy VALUES wait for the F1 re-read + soak review.

## 4. Working rules refreshed this week (beyond the handover's list)

- Explicit paths in `git add`, always (a greedy `add -A docs/` swept another lane's stack on 07-16). Check `git status` before opening any lane.
- Freeze the CSV before stats; `.0000000Z` = backfilled eval rows (F8).
- The verify gate now (post-F4) builds WhatIfRunner too — a dead project sat four days because it wasn't in the build set.
- Effort matching + doc-linking-on-first-mention + delete-screenshots (memories exist for each).
- The measurement stack is placed-vs-placed EVERYWHERE now: any rate quoted from before 2026-07-21 is on a different yardstick (two re-bases: D6 stops 07-14, targets 07-21). Compare across the boundary only via the §3 before/after machinery.
