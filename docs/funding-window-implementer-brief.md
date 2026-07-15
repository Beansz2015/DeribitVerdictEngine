# Funding Time-Anchored Window — Implementer Brief (fresh conversation handoff)

**Date:** 2026-07-15 · **Status:** build-authorized (proposal APPROVED 2026-07-07, D1–D5 ticked, **values final: W = 5 min, T = 2e-7**) · **Model/effort: Opus, medium — one conversation** (~150 LoC + fixtures). Coordinator review after.
**⚠ Scoring change** — **bundled at the open v52 boundary with trader sign-off 2026-07-15** (rule-1 bundling precedent; v52 wire-in went live the same evening with negligible rows between — say exactly this in the change_log dataset note). Run the CLAUDE.md session-start protocol first.

## Spec
`funding-momentum-time-anchored-window-proposal.md` — the mechanism, values, and §7 acceptance are all final there. Context: `signal-health-retune-proposal.md` §5 watch finding (the count-based 3-change window breaks at on-close cadence; res-3 FundingMomentum currently uninformative).

## Scope
1. **Timestamped funding ring** replacing the count-based `_fundingHistory` window semantics: append every run (no dedup), **30-min eviction**; momentum delta = current − the **newest sample ≥ `momentum_window_minutes` (5) old** (the anchor); no anchor yet (cold start / post-gap) ⇒ **FLAT**. Threshold `momentum_threshold` = **2e-7** (unchanged key, retuned meaning). Host-agnostic (`CalcFundingMomentum` + call-site plumbing; the ring state moves wherever the run-context owns it today).
2. **Settings v53:** retire `indicators.funding.momentum_window` (count) → new `momentum_window_minutes` (5). Remove key + POCO field — removal makes it applier-unresolvable (C-6 rejects; **do NOT add to `RejectedPathFragments`** — the v47-F1 snapshot-poisoning lesson). New key stays tweaker-tunable like its siblings. POCO defaults ride the commit; change_log (dataset note incl. the bundle statement) + §15 row + §12 funding-row update.
3. **Post-ship watch (name it):** the retune §5 re-check, **per-resolution** — FLAT 60–70% + Step-3b engagement 15–25%, **both res-1 AND res-3 in-band = success** (handover §4 watch table row updates from "AFTER the funding build ships" to live).
4. **Rider (trader request 2026-07-15): title-bar version.** The form caption still reads "Deribit Verdict Engine v0.47 [P4]" — a stale hardcoded string. Replace with a dynamic caption set at form load from `SettingsLoader.Current` (e.g. `Deribit Verdict Engine — settings v{N}`), so it never drifts again; refresh on hot-reload if trivial, else load-time only is fine. Title bar is not a rendered analysis surface — no parity obligation; state that in the commit.

## Acceptance (proposal §7 — highlights)
- **The cadence-invariance fixture is the point:** the same funding path sampled at 30s vs 180s cadence yields the SAME momentum states at the same instants.
- Anchor = newest-≥W (not oldest-in-window); eviction at 30 min; cold-start + post-gap FLAT; crowding amplify/soften arms unchanged (Step 3b mechanics untouched — only the window semantics change).
- Builds 0/0; **A1–A28d unregressed** + the new fixture set; verify-gate prepush green. HC ledger: next free = **HC23** (only if a fence is actually needed — none expected).

## Constraints
Local-first, NEVER push; spec-back (`funding-window-spec-back.md`) with every deviation; **the collector is running the Debug exe — Release-only builds**; trader does the Debug build + restart at the test gate (that restart = the v53 activation). No other implementer lane is open — but check `git status` before starting anyway (the 07-15 duplicate-commit lesson).
