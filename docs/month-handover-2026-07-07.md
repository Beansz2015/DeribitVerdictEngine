# Month Handover — 2026-07-07 (Fable window close → Opus execution month)

**For:** every Opus orchestrator/implementer conversation this month, and any later Fable seat. **Supersedes** `next-session-handover-2026-07-06.md` (its queue is done except what reappears below).
**Authorities:** `docs/roadmap.md` = *what runs in what order* (§5 month order + §W6 ceiling programme); this doc = *how to execute it* (triggers, recipes, models, review checklists). When they disagree on order, the roadmap wins.
**Author:** Fable coordinator seat, 2026-07-07. The window may extend a few hours into Jul 8 MYT — if a Fable seat is still reachable, spend it on coordinator reviews of in-flight work, not new specs.

---

## 0. Start protocol (every conversation)

Per CLAUDE.md: `docs/DeribitIndicatorProject.md` + `docs/architecture.md` in full, load `crypto-trading-context`, `docs/roadmap.md` for anything involving order/priority, skim the memory index. Frontier memory: `project-roadmap-signal-bridge` (current through 07-07).
**Timezone trap (standing):** machine/trader = GMT+8 (MYT); CSV timestamps = UTC. Compare against NOW-in-UTC before claiming any data gap.
**Seat coordination (standing):** grep `MEMORY.md` for an existing entry before writing yours; never let two seats hold uncommitted changes on shared files (`EngineSettings.vb`, `settings.json`, `PromptBuilder.vb`, `MainForm_Analysis.vb`, `SignalEmitter.vb`) — serialize at commit boundaries; implementer conversations update the roadmap memory themselves.

## 1. State at window close (verify with `git status -sb` — never assume push state)

- **Engine v51 (B4b placed-geometry) BUILT + COORDINATOR-APPROVED, local, UNPUSHED** (`e8dd64a`+`9ab3f04` + docs commits on top). Trader tests + pushes. **On that push:** autotrade live-at-minimum-size unlocks (order-app side), and CLI-port Stage 3 unblocks.
- **v50 LIVE + collecting** since 07-03 (on-close cadence; CSV v0.8; collector runs the Debug exe from `bin\Debug` — **Release-only builds while it runs**; `analysis_log.csv.v0.7.bak` is NEVER deleted, two watches read it).
- **Signal bridge v1:** schema FROZEN; engine emitter shipped (v49) behind `signal_bridge.enabled:false`; **consumer lane (order-app) still open** — A2 go-ahead delivered.
- **Specs decision-complete and ready to build:** funding time-anchored window (D1–D5 ticked 07-07), CLI-port run-state extraction (D1–D7 ticked 07-07). **Spec candidates parked with defined gates:** cross-venue lead-lag (W6-7; D1–D6 tick at scheduling), #6 absorption activation decisions (later, evidence-gated).
- HARD CONSTRAINT ledger current through **HC21** (structural_levels). Harness at **119 fixtures** (A1–A26g). Gate: `tools/checks/verify-gate.ps1 -Mode prepush`.

## 2. Standing rules (binding, unchanged)

1. **One ⚠ scoring change per open collection window.** Open now: #5 (since 07-03) and B4b's own boundary (07-06). A new ⚠ lands only at a boundary; bundling at a boundary is allowed with trader sign-off (R1/R2-at-#5 precedent).
2. **Spec-first** for scoring/novel behaviour; trader ticks the D-table before build; implementer writes a spec-back with deviations; coordinator reviews.
3. **Local-first; NEVER push** — trader tests + pushes. Gate everything before calling it done.
4. **Display parity = FOUR surfaces** (plaintext snapshot ↔ cards ↔ `verdict_signal.json` ↔ CSV `Placed*`), all through `SignalEmitter.ComputeSideLevels`. Any commit changing a value on one updates all or states why not.
5. Rejected-approaches list (trader-profile §4) binds everything. New-indicator bar = roadmap W6 header (orthogonal *class* or refuse).
6. Delete test screenshots. Budget: batch asks; Opus on Max-5x this month — dense single-conversation items preferred.

## 3. Execution queue

Work top-down; data-gated items interleave as their gates clear. **[T] = trader action, [O] = Opus implement, [C] = coordinator review (Opus seat wearing the review hat, or Fable if reachable).**

### Q1. v51 push [T] — the unlock event
Trader tests B4b live, pushes. Confirms: ATR rows show placed labels (`SWING_STOP`/`STOP_CLAMPED`/`FALLBACK_ATR`, `PLACED @`), fallback R:R reads ~1:1.1, BELOW_MIN_MOVE modestly up on NY. Post-push, tick roadmap 3b.

### Q2. #5 correlation gate [O+C] — data-ready ~Jul 8–9
**Gate:** 2–3 weekday session-days of post-v50 burst columns (Fri 07-03 partial + Mon 07-06 exist; Tue/Wed complete it).
**Recipe:** `aggressor-velocity-proposal.md` §5.1 on post-v50 rows — Spearman(lean, TFI) + fire-overlap vs TFI/MicroCVD/VolumeRatio. AggrVel numerics are EMPTY (not zero) when unavailable — exclude, don't coerce.
**Verdict rule:** correlation >0.7 AND overlap >80% ⇒ redundant ⇒ **#5 closes honestly as display-only** (likely outcome; TAPE strip keeps the burst field). Else ⇒ `§5.2` per-session firing-rate re-baseline + the scoring sub-version (TFI-modifier wire-in) as its own ⚠ boundary with trader sign-off.
**Model:** Opus, medium. Deliverable: a short dated verdict doc + roadmap tick.

### Q3. Funding time-anchored window build [O+C] — after the Q2 verdict
**Spec:** `funding-momentum-time-anchored-window-proposal.md` — **APPROVED, values final** (W=5 min, T=2e-7; `momentum_window` count-key retired → `momentum_window_minutes`). Own ⚠ boundary (D4) — if Q2 spawned a scoring sub-version, that lands first, funding after.
**Acceptance highlights:** §7 — the **cadence-invariance fixture is the point** (same funding path sampled at 30s vs 180s ⇒ same states at the same instants); anchor = *newest* sample ≥W old; 30-min eviction; cold-start FLAT; A-series unregressed; settings bump + POCO + change_log + §15.
**Until it ships:** treat `FundingMomentum` on res-3 rows as uninformative (bounded ±amplify/soften contamination only).
**Model:** Opus, medium — small diff (~150 LoC + fixtures), one conversation.

### Q4. Bridge: consumer lane → log-only soak → soak review → live ladder [T/O/C]
Order-app side finishes the consumer (their repo); trader runs the **log-only soak**.
**Soak-review checklist [C]:** bridge proposal §5.7 — (a) `would-act: side @ entry,stop,target,size` lines diff CLEAN against CSV `PlacedTargetLong/Short`+`PlacedStopLong/Short` per (instance_id, signal_id) — this is the FOURTH parity check (consumer parse/mapping drift); (b) `rejected:` (API-level) tallied separately from `refused:` (gate-level); (c) joins: CSV→payload total, payload→CSV partial (SKIPPED burns ids — expected).
**Then [T]:** `signal_bridge.enabled` flip = dated action; ARM AUTOTRADE stays the dual-arm interlock (runtime-only, never persisted). **Live-at-minimum-size** = post-Q1 + soak-review pass. Consumer **sizing-by-stop-distance** = their own small spec after live-min-size stabilises (all inputs already in the payload) — its existence later unlocks the engine's structural-stop **un-clamp** settings pass (placed-geometry derivation §6b; ideally after a calmer-regime re-derivation, DG5).
**Bridge v2** (position-state + disposition feedback file; makes HoldStatus/exit-guard actionable): own spec, gated on 1–2 supervised weeks of v1.

### Q5. #6 absorption build [O+C] — after #5 calibrates/closes
**Spec:** `book-absorption-proposal.md` (D1–D8 ticked incl. the pull-fraction spoof guard). Rotation-free (its 5 CSV columns shipped reserved in v0.8). Build sub-version `scoring_enabled:false`; **activation is its own ⚠ boundary, evidence-gated TWICE** (independence per the #5 rule AND ≥10pp conditional-outcome gradient on n≥30 flagged evaluated rows).
**Model:** Opus, **high** — dual-fed (book+trades) episode tracker under the MarketState lock; the hardest remaining build. Budget a careful coordinator review.

### Q6. CLI-port run-state extraction, Stages 1–4 [O+C] — any gap after Q1
**Spec:** `cli-port-run-state-extraction-proposal.md` — **D1–D7 ticked** (scope clarification recorded in its §0: WinForms behaviour byte-identical, internals restructured; the Linux artifact is `tools/HeadlessRunner`). Four commits, gate + review per stage; Stage-1 fixtures pin current semantics EXACTLY (bugs move, filed separately, never fixed in-flight). Zero behaviour change ⇒ no boundary; but it rewrites `MainForm_Analysis.vb`, so **serialize against anything else touching that file**.
**Model:** Opus, medium, one conversation per stage.

### Q7. W6 ceiling programme — pick up as gates clear [O+C]
- **W6-1 LONDON calibration debt** — pickable at any audit re-run; the retune-audit method is re-runnable by recipe (`signal-health-audit-2026-07-03.md`). F15 rule: re-measure divergence-gate mechanisms on 3-min, never blind-scale ×2.1.
- **W6-4 offline ceiling audit** — unlocks ~3–4 weeks of v0.8 rows (early August). Regularized model on the feature book vs outcomes; REPORT deliverable; never a live scorer. **Run before W6-5 (B1) and before any cross-venue Tier-C thought.**
- **W6-2 tweaker first fire** — data-gated (>40%-failure NY×1 window); supervised dry-run first; window/MinTier recalibration rides the follow-up.
- **W6-3 Kelly CAL mode** — size its book-depth gate when picked up (per tier × session × resolution).
- **W6-7 cross-venue lead-lag** — `cross-venue-lead-lag-proposal.md` is the candidate; slot = after #6 verdict + W6-4; D1–D6 tick at scheduling.

### Q8. Small items, on demand / any gap [O]
- **D6 eval-migration analysis pass (B4b follow-up):** the eval stack deliberately kept its ATR yardstick at v51 — `LivePerformanceTracker.FAV_ATR_MULT` const 2.0, the `AnalysisConstants` 1.2/2.0 mirrors, `FailureRateMatrix`, while `AnalysisRunner` **and `AutoTweakerCore`** read the live 1.75 gate (coordinator record addition 07-06). The follow-up migrates the eval/offline barriers onto the logged `Placed*` columns (F4 established the old barriers weren't pure k×ATR anyway). Analysis pass first, code second; do it before trusting any post-v51 failure-rate reading too precisely.
- **3-min weekday-ASIA `session_volume` re-verify + OBV `|obvChange|` re-anchor** (roadmap W1 last-but-one row + §12): by hand, NEVER the tweaker; ≥50 weekday-Asia 3-min rows (likely long met — verify at execution); dial 1.10/1.05 toward neutral if weekday Asia is calmer than the Saturday-confounded v34 sample.
- UserManual/TraderGuide: 1.2/2.0 multipliers + `PLACED @` vocabulary (B4b follow-up) + P13 tier docs. WS-health line persistence (tiny; closes the "feed health is inferred" caveat). Commit the untracked `tools/*.ps1` UI-automation helpers.
- Display tier on trader ask: #7 liq cascade alarm (**must carry the first-liq-seen diagnostic** — it gates A4), #8 level-approach alerts (CME weekend-gap levels from cross-venue Tier B can ride along — internal computation, no feed), #11 early-resolution (contiguous-bars guard mandatory). A4 liq×OFI spec only after #7's diagnostic sees a real cascade.
- **Working-tree noise (ignore, don't investigate):** `.codex/`, `configure-claude-deepseek.ps1`, `models-full.json`, `tools/tools/` = non-repo experiment files. `docs/VisualReviewQuestions.txt` + `docs/ContentRequestsAfterVisualReview.txt` = **STALE May-era review notes, fully processed by the P5-test gap-fix cycle** (3g was fixed by Spec C; 3k is by design — closed 2026-07-07); safe to delete on trader nod.

## 4. Watches (consolidated; recipes in the named docs)

| Watch | Trigger / band | Recipe |
|---|---|---|
| B4b post-ship (§12) | reach-rate vs fallback; STOP_CLAMPED freq (expected high at fixed size); BELOW_MIN_MOVE +4–6pp NY | `placed-geometry-spec-back.md` §5 |
| LONDON struct-target (F3/DG4) | in-bound structural reach still <45% after ≥3 more LON session-days ⇒ London override or bound tightening | derivation §4 F3 |
| Retune §5 re-check | AFTER the funding build ships: FLAT 60–70% + 3b engagement 15–25%, **per-resolution — both res-1 AND res-3 in-band** is the success criterion | funding proposal §7 |
| v48 §4a OFI fire rates | any population outside 0.6×–1.5× of DOM 63.2% for 2 consecutive weekday sessions | `v48-ofi-dominance-rebaseline-spec-back.md` §3–4 (reads the `.bak` + new rows) |
| F3 exit-guard observational | EXIT GUARD strip vs HOLD\EXIT row corroboration during holds | `time-averaged-ofi-spec-back.md` §3 |
| WS 3-min volume undercount | a vol-spike (>3× SMA-9) signal that REST would clear and WS misses | P3 cutover spec §7 |

## 5. Coordinator review checklist (per implementation commit-stack)

1. Re-run `verify-gate.ps1 -Mode prepush` yourself — builds 0/0, harness unregressed + the new fixtures, parity + version guards.
2. Full diff audit against the spec's mechanism section; every deviation must be in the spec-back (accept or bounce, never silently).
3. Parity: does any rendered value change? Then all FOUR surfaces in the same commit, or the commit message says why not.
4. Tweaker surface: new keys classified (ON / hand-toggle OFF / hand-tuned prefix OFF); `SettingsDiffApplier` + PromptBuilder HC in sync (next free: **HC22**); no recorded-APPLIED-no-op keys (v47-F1 class).
5. Settings: version bump, change_log entry (newest-first), POCO defaults ride the code commit, §15 row.
6. ⚠ items: confirm the boundary is legal (rule 1), the dataset note is in change_log, and the post-ship watch is named.
7. Record the review verdict in the roadmap memory + tick the roadmap.

## 6. Decision inventory (so no seat re-litigates)

**Ticked, build-ready:** funding window D1–D5 (07-07) · CLI-port D1–D7 (07-07) · #6 build D1–D8 (07-03) · placed-geometry D1–D8 + DG1–DG5 (built as v51).
**Tick at their scheduling:** cross-venue D1–D6 · #6 *activation* (evidence first) · any Q2-spawned §5.2 re-baseline.
**Decisions of record — do not re-open without new evidence:** roadmap §5b table (sub-minute cadence, #9 forming-bar, auth/raw feeds, Phase-2b autotune) + trader-profile §4 + the W6 new-indicator bar.

## 7. Key docs index

Sequencing: `roadmap.md` (§5 order, §W6 programme) · this doc.
Ready specs: `funding-momentum-time-anchored-window-proposal.md` · `cli-port-run-state-extraction-proposal.md` · `book-absorption-proposal.md`.
Live-build references: `placed-geometry-spec-back.md` (+ proposal + derivation) · `aggressor-velocity-spec-back.md` (+ proposal — §5.1/§5.2 are Q2) · `signal-health-retune-proposal.md` (+ audit — the re-runnable method).
Bridge: `signal-bridge-v1-proposal.md` (§5.7 soak checklist) · order-app `integration-contract-verdictengine.md` (canonical consumer copy) · `signal-bridge-v1-spec-back.md` · `signal-bridge-a2-goahead-orderapp.md`.
Candidates: `cross-venue-lead-lag-proposal.md`.
