# Seat Handover — 2026-07-18 (execution brief; **REFRAMED 2026-07-19: the Fable seat CONTINUES through ~Aug 1**)

**Seat plan (trader 2026-07-19):** Fable is extended for the trader's Max plan; the trader downgrades to Pro on **AUGUST 2** — Fable then becomes credit-rationed. So: **Fable orchestrates + reviews through ~Aug 1; Opus implements throughout (economics unchanged); the REAL handoff doc gets written ~Aug 1** for an Opus-led month with rationed Fable moments (this doc's queue is its base). Deliberate scheduling consequence: the judgment items — soak review, #6 activation gates, W6-1 ruling, funding re-read, matrix-migration review — all land INSIDE the window; batch them before Aug 2 and leave the post-Aug-2 world execution-shaped (W6-4 sits right at the boundary — prep it, hand it off clean).
**For:** every conversation from here. **Supersedes the queue in `month-handover-2026-07-07.md`** (Q1–Q6 done; its §0 start protocol, §5 review checklist, and §4 watch recipes remain valid and are referenced below, not repeated).
**Authorities:** `roadmap.md` = what runs in what order (§5 + §W6 + the 07-16/07-18 update lines) · `profitability-risk-levers.md` = the trader's profitability sub-project lens (L1–L9; tracking, not sequencing) · this doc = current state + how to execute the open queue.

## 0. Start protocol (every conversation)

Per CLAUDE.md: project doc + architecture.md in full, `crypto-trading-context` skill, roadmap.md for anything sequencing-shaped, memory index. **Verify push/tree state with `git status -sb` — never assume** (at handover: ~20 docs/code commits local + the manual-fold-in lane's uncommitted manual/PDF work; the trader pushes after the PDFs finish). **Timezone trap:** trader/machine GMT+8, CSV timestamps UTC. **Two-lane rule (this week's scars):** check `git status` before opening any implementer lane; never two lanes holding uncommitted changes to shared files — `verify/ordercheck/Program.vb` is the usual collision; `git add` with EXPLICIT PATHS only (a greedy `add -A docs/` swept another lane's stack on 07-16; a duplicate commit caused a push rejection on 07-15 — both documented, both avoidable).

## 1. State at handover (engine v54, settings v54, CSV v0.8/111 cols, eval cache v5, harness A1–A31g)

- **The indicator set is BUILD-COMPLETE** (the W6 header ruling realized): #6 book absorption BUILT as **v54** (`ae6678c`, coordinator-approved, LIVE, `scoring_enabled:false`, display/CSV only, rotation-free). **#6 activation is the engine's last scheduled ⚠ scoring decision** — evidence-gated twice, see §2 item 4.
- **This window shipped:** #5 correlation gate verdict (NOT redundant) → §5.2 derivation (NY 4.5) → **v52 wire-in** (TFI modifier, NY-armed, HC22 rider) · **v53 funding time-anchored window** (bundled at the v52 boundary with trader sign-off — ONE dataset boundary; count-cap removed; title bar now dynamic) · **D6 eval migration** (both eval surfaces' stop side → placed levels; cache v5; strip re-based redder — honest) · **what-if replay runner + in-app launcher** (grid sweeps, EV-in-ATR, split-half; the trader uses it) · UI space pass · **#6 build** · session-policy-gate spec (APPROVED, handed to the ORDER-APP orchestrator) · placed-target matrix migration spec (APPROVED, build-ready) · `profitability-risk-levers.md` (L1–L9).
- **Bridge log-only soak LIVE since 07-16** (`signal_bridge.enabled:true` now in the TRACKED settings — stomp-proofed after a Debug rebuild reverted the bin copy on 07-17, causing a ~7h emission gap; the soak-log join will show it, it's explained, not a defect). ARM checkbox now visible from form load (constructor-order fix).
- **HARD CONSTRAINT ledger through HC23; next free HC24.** Gate: `tools/checks/verify-gate.ps1 -Mode prepush`; the version-bump check is WARN-only on code-only eval changes (D6 precedent).

## 2. Execution queue (work top-down; data-gated items interleave)

1. **Manual-fold-in lane finishes → trader pushes.** The what-if manual (approved) folds into UserManual/TraderGuide + PDFs — in progress at handover, its lane commits its own stack. Nothing else starts until this is pushed.
2. **Placed-target matrix migration [O, medium] — the next build.** Kickoff: *"Implement docs/offline-matrix-placed-target-proposal.md"* (M1–M5 ticked; implementer orders inline; spec-back `offline-matrix-placed-target-spec-back.md`). It is the **p-instrument** for Kelly CAL (levers L4) — priority-upgraded, ahead of all smalls. Touches the tweaker's pick-space (window-only) — read §2 of the proposal.
3. **Bridge soak review [C] — ~Jul 26–30** (1–2 supervised weeks from 07-16). Checklist = bridge proposal §5.7 + month-handover Q4 + the 07-18 addendum: `would-act` diff vs CSV `Placed*` per (instance_id, signal_id); tallies of `rejected:` vs `refused:` vs the NEW **`refused: policy(...)`** (the session-policy gate, if the order app has shipped it by then); the 07-17 emission gap appears in the joins — expected. Pass ⇒ the trader's live ladder: ARM interlock → live-at-minimum-size (v51 unlocked it long ago). **⚑ REMINDER AT THE POLICY-VALUES STEP (trader-requested 2026-07-22): the P5 starting policy ("CONFIRMED only" for LONDON) currently selects the WORST-performing context tag — the 20260721 offline report §6 shows CONFIRMED INVERTED on both readable sessions (NY 13.3% success vs ALIGNED 33.7%; LONDON 7.1%, n=14). Small-n / F1-class caution applies, but do NOT set the P5 values without re-reading §6 on the then-current book (after the F1 re-read, per the standing gate).**
4. **#6 activation gates [O+C] — data-ready ~Jul 29–31** (~1.5–2 weekday-weeks post-07-17). Recipe: `book-absorption-proposal.md` §5 — independence (|Spearman| < 0.7 vs OFIRatio/burstRatio/TFI + fire-overlap < 80%, the #5-gate method — reuse the 07-13 verdict-doc pattern) AND outcome gradient (≥10pp worse success on n≥30 flagged evaluated rows, via the eval cache join). Verdict doc either way. **Pass ⇒ activation = its own ⚠ boundary**: D-table to the trader, Step-2 penalty wire-in, `Absorption` breakdown row (snapshot + card SAME commit — this one is NOT strip-only), §12 watch row. Fail ⇒ closes honestly as display-only. Engagement design target 3–8% of directional runs; per-session `min_aggr_usd` overrides expected.
5. **Funding bands calm-week re-read [O, low] — next week.** The 07-17 watch PASSED on mechanism; bands deferred: if FLAT stays <50% through a CALM funding week too ⇒ a T-re-fit evidence pass (T fit given W — the coupling is documented in the POCO + change_log).
6. **res-3 §5.2 (aggr-vel Asia/London thresholds) [O] — ~8–10 weekday session-days per session** (counting from 07-14; late July). Derivation only; sessions AUTO-ARM via threshold-presence (wire-in spec-back D1) — no scoring-engine change needed.
7. **W6-1 LONDON ruling [C, high] — at the next audit re-run** (re-runnable by recipe, `signal-health-audit-2026-07-03.md`). Evidence on file: 76.8% adverse-first on structural rows, 92% of stops at the clamp, F3 watch NOT tripped (49.3% vs 45%), winners-MAE p75 1.63×ATR. The two named candidates (LONDON `stop_max` 2.0–2.2; swing-buffer offset = un-clamp input) decide TOGETHER, evidence via a **what-if LONDON grid** (its first registered use case). Sizing interaction: b moves if stops move — re-derive with the levers L4/L5 coupling note.
8. **W6-4 offline ceiling audit — unlocks ~3–4 weeks of v0.8 rows (early August).** Before B1 (W6-5) and any cross-venue Tier-C thought.
9. **Smalls, any gap [O]:** WS-health line persistence (the 07-13 crash showed why) · tweaker first-fire (data-gated, >40%-failure NY×1 window; **gate readings re-based by D6 — read post-migration rates only**) · v48 §4a OFI watch continues (ASIA 84.9% one-day hot, since regressed to 57% — in-band) · commit hygiene sweeps. **Q6 CLI port stays DEFERRED TO LAST** (trader ruling 07-08, do not pull forward).
10. **Order-app side (not this repo's lanes, but track):** session-policy gate implements there (P1–P5 ticked; P2 UI note: context selector joins the existing tier-settings section); stop-distance sizing spec after live-at-min-size stabilises (levers L3 → unlocks L9 un-clamp).

## 3. Standing rules (binding; the 07-07 handover §2 list plus this window's additions)

1. One ⚠ scoring change per open window; **bundling at one boundary with trader sign-off is precedented** (v52+v53). Current open window: the v52+v53 bundle (watch PASSED; closes fully when the funding re-read clears). #6 activation opens the next.
2. Spec-first · D-tables before build · spec-back with every deviation · coordinator review per month-handover §5 (re-run the gate YOURSELF; the last four reviews each caught something).
3. Local-first, NEVER push — trader tests + pushes. **Release-only builds while the Debug collector runs** — the 07-17 settings-stomp incident is the cautionary tale (`PreserveNewest` copies tracked→bin on Debug builds; a trader-side bin edit is protected only until the tracked file is newer).
4. Display parity: four surfaces via `ComputeSideLevels`; live status elements (TAPE/strips/checkboxes/title) are exempt BUT say so in the commit message.
5. Trader-profile §4 rejected patterns bind everything. New-indicator bar = W6 header (the set is complete; refuse anything that isn't an orthogonal CLASS).
6. Effort matching (memory `feedback_effort_matching`): low for recipes/watch reads, high for derivations/⚠ reviews. Link docs on first mention per reply (memory `feedback_link_referenced_docs`). Delete test screenshots. Freeze the CSV before computing stats (live-append drift). Weekday-only for all data gates.
7. **`verdict_context` values are STABLE IDENTIFIERS (order-app session-policy addendum 2026-07-21; recorded in `signal-bridge-v1-proposal.md` §3):** renaming/removing any of `CONFIRMED / ALIGNED / FLOW_UNCONFIRMED / STRUCTURALLY_WEAK / MOMENTUM_FADING / BELOW_MIN_MOVE` — or touching the tier↔confidence 1:1 or the `generated_at_utc` session buckets — is a coordinated cross-repo pass (owner relays), never free drift. Adding tags is free but must be documented as policy-targetable.

## 4. Watches (live at handover)

| Watch | Band / trigger | Next read |
|---|---|---|
| Funding per-resolution (§5 re-check) | FLAT 60–70% + 3b 15–25%, both resolutions | Calm week (queue item 5) |
| Wire-in NY burst | fire 8–12%, same-side ≥85% | Passed 2 sessions (8.1/6.7%, 94–97%); spot-check with the funding re-read |
| #6 activation evidence | §5 gates | ~Jul 29–31 (queue item 4) |
| B4b §12 (reach, STOP_CLAMPED, BELOW_MIN_MOVE, LONDON F3) | F3 <45% | F3 read 07-15: 49.3%, NOT tripped; next at W6-1 |
| v48 §4a OFI dominance | 0.6×–1.5× of 63.2%, 2 consecutive days | Continues; ASIA regressed in-band |
| Bridge soak | §5.7 + `refused: policy` token | Review ~Jul 26–30 (queue item 3) |
| pullFrac distribution (W4 trigger) | fidelity-binds evidence | Accrues passively; read at #6 activation |

## 5. Decision inventory (do not re-litigate)

**Ticked this window:** S1–S5 (wire-in, NY 4.5) · funding D1–D5 (built) · D6-migration D1–D5 (built) · W1–W7 (what-if, built) · #6 build D1–D8 (built) · **M1–M5 (matrix migration — BUILD NEXT)** · **P1–P5 (session policy gate — order-app lane)**.
**Tick at their scheduling:** #6 ACTIVATION (evidence first) · cross-venue D1–D6 · any T-re-fit (funding) or LONDON stop_max (W6-1) — both evidence passes first.
**Decisions of record:** roadmap §5b + trader-profile §4 + Q6-last + the R1/R2 bridge rulings + "engine levels flow as PRICES, never per-signal ratios" (reaffirmed 07-18, levers doc §2).

## 6. Key docs index

Sequencing: `roadmap.md` · this doc · `profitability-risk-levers.md` (profitability lens).
Build-next: `offline-matrix-placed-target-proposal.md` (M1–M5 ticked, orders inline).
Recipes: `book-absorption-proposal.md` §5 (activation gates) · `aggressor-velocity-correlation-gate-verdict-2026-07-13.md` (the gate-verdict pattern to reuse) · `signal-health-audit-2026-07-03.md` (audit re-run) · bridge proposal §5.7 + month-handover Q4 (soak review).
Live-build references: `book-absorption-spec-back.md` · `aggr-vel-wirein-spec-back.md` · `funding-window-spec-back.md` · `d6-migration-spec-back.md` · `offline-whatif-replay-spec-back.md`.
Cross-project: `session-policy-gate-proposal.md` (order-app implements) · `ui-automation-harness-recipe.md` (portable, all projects).
