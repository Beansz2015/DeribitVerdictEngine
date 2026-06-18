# Next-Session Handover — 2026-06-18 (coordinator / spec-author seat)

You're taking over the **coordinator / spec-author + sanity-check** seat for DeribitVerdictEngine. You write specs, independently review implementer spec-backs (re-run builds + harness + audit the diff), and sequence the work — you don't usually implement (that routes to fresh Opus conversations with an approved spec). **Local-first: commit locally, NEVER push — the trader tests + pushes.**

## 0. Start protocol (do it)
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` (full) + `docs/architecture.md`; load the `crypto-trading-context` skill; read the memories — the live frontier anchors are **`project-engine-audit-calibration-trap`** + **`project-v36-session-timeframe`** (most current), plus `project-p5-review-done` (reskin + Spec C). Then `git log --oneline -10` + `git status`.

## 1. Frontier (2026-06-18) — everything PUSHED, `origin/master = e3ba5bd`
- **Versions:** settings **v37** · CSV **v0.7** (`+ExecResolution +CVDWeightedSlope`) · eval cache **v4**.
- **Shipped + pushed this session (all on origin):** v36 Phase-1 (session-timeframe resolution) reviewed + live-EXEC-3m-verified; **auto-tweaker (session × resolution) population filter = Phase-2a** (`e4742b2`) — proposal + hand-off + spec-back + coordinator review, isolated-replay-validated; **Spec C** (SC-column/TOTAL parity + permanent `CheckLedger` ledger guard, `2ddc6aa`) — bit-identical **statically proven**, trader ran 10 cycles ledger-quiet; **P11 ATR-band recalibration** (settings **v37**, `4980aad`: `static_ref` 115→38, profile §5 bands 1-min 20/55 + 3-min ~42/115); **Tweak-Settings dialog status fix** (`2aeda76` — the negative `-1634/75` was a stale-state display bug, core self-heals).
- **Phase-2b** (auto-tweaker per-population auto-tuning) — **DRAFT only** (`05d0e74`, `docs/auto-tweaker-phase2b-per-population-autotuning-proposal.md`), the optional automation layer; NOT the Asia/London accuracy fix.

## 2. IMMEDIATE NEXT ACTION — audit the offline Analysis Report (trader-flagged, not yet done)
The trader ran the **Analysis Report** (`analysis/` — `AnalysisRunner` / `FailureRateMatrix` / the §5–§7 diagnostics; report `20260618_090018`, 893 rows) and flagged: *"the ATRs reported in the failure-rate matrix and pending data might be wrong"* + *"check for anything else stale since this function was implemented — we've done many updates since."* I had **not** started the audit when we cut to this handover. **This is task #1.**

**Strong hypothesis (start here):** the offline `FailureRateMatrix`/`AnalysisRunner` is **resolution-blind** — the same class of bug Phase-2a just fixed for the *auto-tweaker*. Post-v36 the book mixes 1-min NY (ATR ~13–50) and 3-min Asia/London (ATR ~27–100) rows; the matrix appears to **pool both resolutions into the same tier cell** (e.g. MEDIUM_LONG n=78), mixing two ATR-scale barrier populations → confounded cell failure rates. Per-row barriers (entry ± mult×ATR) are likely *individually* correct (ATR is per-row from the CSV), but the **aggregate** is meaningless across resolutions. **The fix is probably the offline analogue of the Phase-2a `(session × resolution)` filter** — reuse `Core/ExecutionResolution.MatchSessionBucket` + the v0.7 `ExecResolution` column, and segment the matrix by `(session × resolution)` (or at minimum by resolution). It is host-agnostic `analysis/` code.

**Secondary staleness checks (the report shows these — verify each against current engine):**
- **`Below-min-tradeable-move rows excluded: 0`** — suspicious. Does the offline matrix apply the v35 min-move floor + `EXCLUDED_BELOW_MIN_MOVE` the same way `LivePerformanceTracker` does? Either no rows are gate-killed in this regime, or the offline path never got the v35 de-confound. Confirm parity offline↔live.
- **§5 Funding diagnostic** recommends threshold "0.0004 bp" but v34 set `momentum_threshold = 5e-8` — the recommendation logic looks pre-v34. Stale?
- **v0.7 columns:** confirm `AnalysisRunner`/`ForwardWindowJoiner`-equivalent read the new `ExecResolution` + `CVDWeightedSlope` columns (and that the 893-row parse is correct post schema-bump).
- **§7 OI×CVD = 0/0 INCONCLUSIVE** — expected (OISignal ~95% NEUTRAL, the standing v34 WATCHING item), not a bug.
- Tier sweep ranges (STRONG 0.5/0.8×, MEDIUM 0.3/0.5×) and the n≥30 gate — sanity-check they're still right post-v36.

**Routing:** this is `analysis/`-layer (offline, host-agnostic, NO scoring votes) → it's a **safe, non-approval-gated fix** you can spec + hand to an implementer (or, if small, do directly per the trader's "small fixes OK for orchestrators" rule). The full report text is in the 2026-06-18 conversation; regenerate via the app's Analysis Report button if needed.

## 3. Backlog — what's workable now vs data-gated (full detail: `DeribitIndicatorProject.md` §12/§13/§16)
**Workable while data accumulates (not data-gated):**
- **WebSocket migration** — `websocket-migration-proposal.md`, **APPROVED 2026-06-12**, never built. v1 is semantics-neutral (not a recalibration event), unlocks Section A (spread momentum, absorption, liq×OFI flip, VPFR shape). Caveat: foundation rebuild + needs the **v33→v37 reconciliation** of its `network` keys + shadow-parity validation (§7). The high-impact strategic next project.
- Engine spec-first, lower payoff: D3 (5m RSI div), D4 (Donchian×BBW), P6 (STRUCTURAL_RR_LOW tag), P12 (transitional size haircut), D5/D6.
- P11 is **DONE** (this session). The reskin arc + Spec C are **DONE** (pending nothing — pushed).

**Data-gated (the bulk — this is why data is accumulating):**
- **(B) MANUAL `resolution_profiles["3"]` re-baseline** — the **Asia/London accuracy fix** (settle the 2.1× ROC proxy + extend the profile to TTM/divergence gates; v33/v34 settings method, NOT the tweaker). Gated on **≥50 weekday-3-min rows/session**. *This is the priority once data lands — do not conflate with Phase-2b (which only automates it).*
- **Auto-tweaker first live NY×1 fire** — gated on a real **>40%-failure NY×1 window**. Mechanics fully validated (isolated replay); the Phase-2a filter makes it safe on mixed-resolution data. Keep `dry_run`/`auto_commit=false`; coordinator reviews the diff before any apply.
- The §12 threshold sweeps (TFI/OFI/TTM/VPFR/Liq/funding/session-vol/OI×CVD/AtrTargetMult/ContextTag/Kelly/spread) — 50+ rows each.
- **Phase-2b** (auto-tweaker per-population) — after (B), optional, needs the schema-home decision.

## 4. Working rules / seat
Spec-first (novel/scoring → committed `/docs` proposal first); **approval-gated** (scoring changes need explicit trader sign-off — `analysis/` offline + display + tooling are NOT scoring votes and are safe to proceed); **local-first, NEVER push**; host-agnostic `analysis/` + `tools/` (no WinForms); **display-parity hard rule** (card ↔ `BuildPlaintextSnapshot`); delete test screenshots after live UI checks ([[delete-test-screenshots]]); the crypto-trading-context skill's bundled ATR bands are STALE — use [[reference-atr-bands-v37]]. Co-author trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## 5. Routing reminders
Implementer spec-back → coordinator (you): re-run solution + AutoTweaker + `verify/ordercheck` harness, audit the diff, record an `> Coordinator review` callout in the spec-back + local commit. A genuine *design* question → trader (approval-gated). The auto-tweaker stays HELD until its live first fire (trader-supervised, data-gated).
