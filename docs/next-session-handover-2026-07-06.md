# Next-Session Handover — 2026-07-06 (post-implementation-wave)

**For:** the next Fable conversation. **Supersedes** `next-session-handover-2026-07-03.md` (its entire queue is DONE).
**Author:** Fable seat, 2026-07-06. The Jul-3 window plan executed ~4 days early: audit, retune, bridge freeze, A1+B4 builds, #6 spec, placed-geometry spec+derivation all landed Jul 3–6.

## 0. Start protocol

Per CLAUDE.md: `docs/DeribitIndicatorProject.md` + `docs/architecture.md` in full, load `crypto-trading-context`, read `docs/roadmap.md` in full, skim the memory index. Frontier memory: `project-roadmap-signal-bridge` (dense, current through 07-06). Then this doc.
**Timezone trap (standing):** machine/trader GMT+8; CSV timestamps UTC; compare against NOW-in-UTC before claiming any data gap.
**Seat-coordination lessons (new this cycle):** implementer conversations update `MEMORY.md`/the roadmap memory THEMSELVES — grep for an existing entry before writing yours (two duplications happened and were merged); never let two seats hold uncommitted changes on shared files (`EngineSettings.vb`, `settings.json`, `PromptBuilder`, `MainForm_Analysis`) — serialize at commit boundaries.

## 1. State (verify push state with `git status -sb` before assuming)

- **Engine v50 LIVE** since 07-03 ~18:11 local: #5 aggressor-velocity build + retune bundle (R1 OFIMomentum retired, R2 funding 2e-7) + CSV v0.8 (111 cols — 16 new incl. TFI, 5 reserved absorption, 4 Placed*, InstanceId/SignalId). Old book = `analysis_log.csv.v0.7.bak` (NEVER delete — the v48 watch reads it). Collector runs the Debug exe from bin\Debug; Release-only builds while it runs.
- **Signal bridge v1**: schema FROZEN + cross-verified; engine emitter SHIPPED (v49, `23fd8b9`, coordinator-approved, smoke-tested) behind `signal_bridge.enabled:false`; consumer lane (A2) go-ahead delivered (`signal-bridge-a2-goahead-orderapp.md`); consumer disposition vocab confirmed 07-06 (`would-act`/`rejected:` extensions — soak-review checklist in proposal §5.7).
- **Placed-geometry**: spec APPROVED (D1–D8, D3=clamp) + §6 derivation DONE + **DG1–DG5 ticked 07-06** (`placed-geometry-derivation-2026-07-06.md` §4 = the values; §6b = the sizing-by-stop-distance routing). **B4b brief handed to the trader 07-06** — implementation may be in flight or done.
- **#6 absorption**: spec APPROVED (D1–D8 incl. the pull-fraction spoof guard); build waits for #5 calibration; its 5 columns sit reserved.
- Local docs commits since the last confirmed push: check `git log origin/master..HEAD`.

## 2. Work queue (priority order; adjust by trader)

1. **Coordinator review of B4b** when the trader says it's done (spec-back expected; verify: gate re-run, the four-surface parity — snapshot/card/payload/Placed* — via the shared `ComputeSideLevels`, min-move gate reads placed values, `enabled:false` byte-identical, StaticRef POCO fix rode along). On approval: **autotrade live-at-minimum-size unlocks** (their side already knows log-only doesn't wait).
2. **Retune §5 post-ship watch** — checkable NOW on v0.8 rows (first 2 weekday sessions post-07-03): FundingMomentum FLAT% 60–70%, Step-3b engagement 15–25%, OFI row shows `MOM:state` with no suffix. Quick CSV pass.
3. **#5 correlation gate** (data-gated, likely clears ~Jul 8–9): recipe = aggressor-velocity spec §5.1 on post-v50 rows — Spearman(lean, TFI) + fire-overlap vs TFI/MicroCVD/VolumeRatio; working rule >0.7 AND >80% ⇒ display-only (#5 closes honestly); else §5.2 per-session firing-rate re-baseline + the scoring sub-version (TFI-modifier wire-in). Needs 2–3 weekday days of burst columns (only Fri 07-03 partial exists as of 07-06 morning).
4. **CLI-port run-state extraction spec** (W4; roadmap): host-agnostic run-context for `_fundingHistory`/`_ofiHistory`/`_oiHistory`/MTF cache/`_prevRegime` + headless runner skeleton; zero behaviour change. Fable spec, Opus implement.
5. **Month handover doc** (the Jul-7 deliverable): Opus execution order for what remains (consumer lane finish → log-only soak → soak review → live-min-size [post-B4b] → #5 gate/scoring → #6 build → sizing spec → un-clamp → bridge v2), gates, review checklists. Much is already encoded in roadmap §5 + memory.
6. **Log-only soak review** when the trader starts it: checklist = bridge proposal §5.7 (would-act ≡ Placed* diff; rejected: vs refused: separation) + payload↔CSV joins.
- Standing watches: v48 §4a OFI fire-rates (recipe in its spec-back §3; spans the .bak), LONDON struct-target watch (post-B4b; trigger in derivation F3), F3 exit-guard observational, liq-feed validation gate on A4 (#7 must carry the first-liq-seen diagnostic).

## 3. Standing rules (unchanged)

Local-first, NEVER push (trader tests + pushes). Spec-first for scoring/novel changes. One ⚠ scoring change per open collection window — currently OPEN: #5 (since 07-03) and B4b lands as its own pre-agreed boundary regardless (D7; the #5 gate is geometry-invariant). Gate everything: `tools/checks/verify-gate.ps1 -Mode prepush`. Display parity = FOUR surfaces once B4b ships (snapshot ↔ cards ↔ payload ↔ Placed* columns), all through `SignalEmitter.ComputeSideLevels`. Delete test screenshots. Budget: trader on the $20 plan — batch asks, keep Fable turns dense.

## 4. Key docs index

Bridge: `signal-bridge-v1-proposal.md` (frozen mirror + §5.7 soak checklist) · their canonical `DeribitOrderPlacementApp/docs/integration-contract-verdictengine.md` · `signal-bridge-v1-spec-back.md` (emitter) · `signal-bridge-a2-goahead-orderapp.md`.
Geometry: `placed-geometry-structural-first-proposal.md` + `placed-geometry-derivation-2026-07-06.md` (values + §6b sizing routing).
#5/#6: `aggressor-velocity-proposal.md` (+ spec-back, v50) · `book-absorption-proposal.md`.
Audit/retune: `signal-health-audit-2026-07-03.md` · `signal-health-retune-proposal.md` (shipped in v50).
Sequencing: `docs/roadmap.md` (§5 month order; item 3b = B4b).
