# Next-Session Handover — 2026-06-16 (coordinator / spec-author seat)

You're taking over the **coordinator / spec-author + sanity-check** seat for DeribitVerdictEngine. You write specs, independently review implementer spec-backs, and sequence the work — you don't usually implement (that routes to fresh Opus conversations with an approved spec).

## 0. Start protocol (do it)
Per CLAUDE.md: read `docs/DeribitIndicatorProject.md` (full) + `docs/architecture.md`; load the `crypto-trading-context` skill; read the `project-engine-audit-calibration-trap` memory (the live frontier anchor — most current) + `project-v36-session-timeframe`. Then `git log --oneline -8` + `git status`.

## 1. Frontier (2026-06-16)
- **Shipped + pushed:** v31–v35 (engine correctness, Tier D, clean-data re-baselines, min-move gate + eval de-confound) + full UI reskin. `origin/master = a5f2818`.
- **Local-unpushed (5 commits — trader tests + pushes, never push for them):** `28fd606` tweaker-MinTier WATCHING, `e4546e7` v36 Phase-2 WATCHING, `2eb79b3` v36 doc trail + coordinator review, `85fc4ff` PromptBuilder crash fix, `79b7836` v36 §10 version-churn.
- **Auto-tweaker first-fire MECHANICS validated 2026-06-15** via isolated dry-run replay — found + fixed a latent `PromptBuilder` `String.Format`-on-literal-JSON-braces crash (`85fc4ff`) that had blocked every fire. Validated: FIRE path (slice→OHLC→matrix→threshold→prompt→payload→state) AND BELOW_THRESHOLD→streak→snapshot-create. Only apply/revert (settings mutation) remains (supervised-anyway; needs auto_commit+API or `--apply-manual`). Live tweaker `state.json` re-seeded to row 1639.
- **v36 (session-timeframe resolution) in IMPLEMENTATION:** Phase 1 kicked off against the trader-approved + coordinator-reviewed build spec `docs/session-timeframe-resolution-implementer-handoff.md`.

## 2. Immediate next action
When the **v36 Phase-1 implementer spec-back** arrives, review it as an independent seat against the hand-off: verify the **2-key ROC seed (not 4)**, `MatchSessionBucket` boundary (`<=` inclusive) + Enabled-independence, fixtures **A14a–A14i** (incl. A14i: resolution survives `session_volume` disabled), the display-parity hard rule (EXEC tag → card binding **and** `BuildPlaintextSnapshot` same commit), host-agnostic rule (resolver in `Core/`), and the **§10 version-churn + root/bin settings** fixes. Routing: implementer spec-back → coordinator (you); a genuine *design* question → trader (approval-gated).

## 3. Sequence
v36 Phase 1 (in impl) → **you write the tweaker (session × resolution) filter spec** (Phase-2 precondition; spec + isolated-replay-validate it before any live fire on mixed-resolution data) → accumulate 3-min Asia/London data → live tweaker fire. The live fire is **DATA-gated** (needs real >40%-failure windows), not just code-gated, and also needs the §10 root/bin + version-churn fixes landed first.

## 4. Open items (detail in memory + DeribitIndicatorProject.md §12 WATCHING)
- **Tweaker MinTier floor vs the v35 book** (§12) — window bumped to 75 interim; needs per-session recalibration against the ~23% actionable-directional rate.
- **v36 Phase-2 threshold carry-forward** (§12) — TTM/divergence-gate scaling + the 3-min DynamicNorms baseline (LONDON session-scoped baseline question).
- **settings.json version churn** (runtime bin-copy at v38 via UI auto_run saves) + **root/bin split** (tweaker `settings_path`=root, app uses bin) — fold fixes into v36 (§10).

## 5. Working rules
Spec-first (novel/scoring → committed `/docs` proposal first); approval-gated (scoring changes need explicit trader sign-off); local-first (commit locally, **NEVER push** — trader tests + pushes); host-agnostic `analysis/` + `tools/`; display-parity hard rule. Co-author trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
