# Next-Session Handover — 2026-06-15 (coordinator / spec-author seat)

**You are taking over the long-running coordinator seat** for the DeribitVerdictEngine engine-calibration arc. Your role across this project has been **spec-author + sanity-check + coordinator** — you write specs, review implementer spec-backs as an independent seat, and sequence the work. You do **not** usually implement; implementation is routed to fresh Opus conversations with an approved spec. (The previous seat handed off at ~60% context; this doc is the cold-start.)

## 0. Start here (CLAUDE.md session protocol — do it)
1. Read `docs/DeribitIndicatorProject.md` in full, then `docs/architecture.md`.
2. Load the `crypto-trading-context` skill (trader profile + writing style — don't read `trader-profile.md` separately).
3. Read the memory file `project-engine-audit-calibration-trap` (the cross-conversation anchor — it carries the live frontier).
4. Do **not** read individual `.vb` files until a specific task needs one.

## 1. The trader (operating context)
Ex-exchange ops + ex-developer; sharp, technical, concise — treat as a peer, no hand-holding. **Budget-constrained ($20 plan): you (Opus) are 1×, Fable 2× — minimise turns, batch asks, route mechanical/well-specced work to fresh Opus conversations, keep this coordinator seat for judgement.** Discipline (hard): **spec-first** (novel/scoring changes get a committed `/docs` proposal before code), **approval-gated** (scoring-affecting changes need explicit trader sign-off; the proposal is the approval artifact), **local-first** (commit locally as you go; **never push** — the trader tests then pushes; remote = tested milestones only). Push back on changes that reintroduce removed patterns (double-counting, non-directional padding, flat-vs-ADX-proximity penalties) — cite the reason. Co-author trailer on commits: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.

## 2. Frontier (what's done, what's live)
- **Shipped & pushed:** engine correctness pass (v31), Tier D (v32), v33 + v34 re-baselines, full UI reskin through P5b. The engine is past the major-bug + reskin arc.
- **v35 (local-unpushed, trader-tested + ACCEPTED 2026-06-15):** the min-tradeable-move **gate** + eval **de-confound** pair, commits `3705d92` + `db050a5`. One settings bump (v34→v35), one key `scoring.min_tradeable_move_pct = 0.0008` (0.08%, price-relative, ≈$50 at $62k, off the auto-tweaker surface). Gate = NO TRADE / `BELOW_MIN_MOVE` when the effective (post-cap) target can't clear the floor (fires below ATR ~24.8 at $62k). De-confound = EXCLUDE (not re-score) gate-killed low-ATR trades from the failure-matrix + eval cache (schema v2→v3) so the metric the auto-tweaker reads stops rewarding sub-tradeable chop. A1–A13 harness all pass. Spec-back: `docs/v35-min-move-gate-and-deconfound-spec-back.md` (sanity-checked, sound).
- **v36 (in spec-writing, separate conversation):** session-timeframe resolution — move Asia/London **execution** to 3-min (where 2×ATR clears the floor), NY stays 1-min. Path B (whole execution stack), ASIA=3/LONDON=3/NY=1, trader-signed-off, 28-day study done. Inputs: `docs/session-timeframe-resolution-spec-writer-brief.md` (coordination brief) + `docs/session-timeframe-resolution-proposal.md`. This emerged because v35 correctly made 1-min Asia/London non-tradeable — v36 restores coverage on the right timeframe rather than weakening the floor.

## 3. Sequence (the plan — keep it)
1. **Verify the v35 EXCLUDE count on the live book** (cheap: AutoTweakerCore's per-run "EXCLUDED below min-move" log should be non-zero) → confirms the tweaker reads the de-confounded matrix.
2. **Supervised auto-tweaker FIRST FIRE on a NY weekday (1-min) window** — dry-run (`tweaker_config.json`: `dry_run_enabled:true`, `auto_commit_enabled:false`), trader watching. **NY is unaffected by v36, so the first fire is independent of v36 and can proceed now.** The proposed diff comes to the coordinator seat (you) for **independent review before any apply**.
3. **v36 spec-writing** (its own conversation, the brief is the input) → finalize the threshold-profile design (the big open decision: config taxonomy, which keys scale ~2.1× for 3-min, bar-count keep-vs-rescale, Phase-1-seed vs Phase-2-rebaseline, A14 fixtures) → re-confirm with trader → Phase 1 build → accumulate 3-min data → Phase 2 re-baseline.
4. **Push** the local-unpushed stack when the trader is satisfied (v35 pair + v34 revision commit `3c0be4c` + v36 docs + recent doc commits — `git status` to confirm; trader pushes).

## 4. HARD RISKS / invariants to carry
- **Auto-tweaker is session-blind AND v36 makes it resolution-blind.** It must NEVER tune beyond the NY/1-min window until BOTH a session filter AND a resolution filter land (so it never pools 3-min Asia/London with 1-min NY, or different sessions). The first fire stays NY/1-min-only to sidestep this. This is the single biggest landmine — the metric it optimizes was ATR-confounded (that's why the v35 de-confound exists; rationale in memory + `clean-data-rebaseline-v34-brief.md` post-v34 section).
- **Scoring changes are approval-gated + spec-first.** v36 changes verdict behaviour in Asia/London → re-confirm the final threshold profile with the trader before code.
- **Engine display-string parity rule (CLAUDE.md):** any change to a line emitted by `BuildPlaintextSnapshot` must update the matching `MainForm_Render_Cards.vb` binding in the same commit (the legacy RTF renderer is gone post-P5b; snapshot↔card is the remaining parity pair).
- **Host-agnostic** for `analysis/` + `tools/` (Linux-port rule): no WinForms there.

## 5. Open / deferred items (trader decides)
- **UI dialog control for `min_tradeable_move_pct`** — v35 shipped hot-reloadable file-editing only; the dialog was deferred (thin follow-on, `OutputDumpSettingsForm` pattern). Trader's call whether to add it.
- **v35 forensic N/K** — not captured live (thin post-reset history); re-confirmable from console on a fresh launch. Low priority.
- **Several v34 items are ABSORBED by v36** — the weekday-ASIA `session_volume` re-verify (the 28-day study already produced the weekday/weekend split) and the ASIA 1.10/1.05 multipliers (may not transfer to 3-min). Don't run them separately; they fold into v36's 3-min calibration.
- **Backlog (§16.6 of the project doc):** P11 ATR-band recalibration (Low<80 stale for $62k; low-impact housekeeping), P12 reduced-size-in-transitional (Kelly advisory; tension with vol-normalization to resolve first).
- **Schema batching (v36 §4.3):** the next CSV bump (v0.6→v0.7, `ExecResolution`) should batch the v34-flagged "log weightedSlope" + possibly Spec C SC/TOTAL parity, so the book doesn't churn twice.

## 6. Data collection guidance (told to trader 2026-06-15)
Continue auto-run; value is asymmetric now. **NY/1-min weekday = valuable** (feeds the first fire + v34 first-N checks). **1-min Asia/London = low forward value** (v35 gates it to NO TRADE; v36 moves those sessions to 3-min, so 1-min rows won't pool with future 3-min data). Don't stop, don't wait, don't treat 1-min Asia/London as a calibration set. 3-min Asia/London collection can't begin until v36 Phase 1 ships.

## 7. Key docs
- `docs/v35-min-move-gate-and-deconfound-spec-back.md` — v35 (accepted).
- `docs/session-timeframe-resolution-spec-writer-brief.md` + `-proposal.md` — v36 (active spec-writing).
- `docs/clean-data-rebaseline-v34-brief.md` — the ATR-confound finding + first-fire caution (read the post-v34 section).
- `docs/min-tradeable-move-gate-proposal.md` + `docs/eval-metric-deconfound-proposal.md` — v35 source specs (with the 2026-06-14 exclude-vs-rescore refinement).
- `docs/DeribitIndicatorProject.md` §12 WATCHING (live re-baseline triggers) + §16.6 (P11/P12 backlog).

**First action for the new seat:** after the start protocol, confirm the live state with `git log --oneline -8` + `git status`, then ask the trader which thread they want to drive — the v35 first fire (you review the diff) or v36 coordination — rather than assuming.
