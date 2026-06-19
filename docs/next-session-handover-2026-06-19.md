# Next-Session Handover — 2026-06-19 (coordinator / spec-author seat)

You're taking the **coordinator / spec-author + sanity-check** seat for DeribitVerdictEngine. You write specs, independently review implementer spec-backs (re-run builds + harness + audit the diff), sequence the work, and do small `analysis/`/settings passes directly. **Local-first: commit locally, NEVER push — the trader tests + pushes.**

## 0. Start protocol (do it)
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` (full) + `docs/architecture.md`; load the `crypto-trading-context` skill; read the frontier memories — **`project-engine-audit-calibration-trap`** + **`project-v36-session-timeframe`** + **`project-websocket-migration`** (most current). Then `git log --oneline -12` + `git status`.

## 1. Frontier (2026-06-19)
- **Versions:** settings **v39** · CSV **v0.7** · eval cache **v4**.
- **origin/master = `9cde370`** (WebSocket P1). **~10 local commits are unpushed** (`4bf3333` pkg-removal → `7fbb6b5` P2 review) — the trader pushes them after the P2 live-test (the ≥50-run shadow-parity gate + 24h soak). Don't push.
- **Shipped + reviewed this arc (all local):** the **offline Analysis Report resolution-segmentation** (`ce7ba4b`, pushed earlier); **WebSocket P1** (additive-only foundation, pushed `9cde370`, trader's network-kill drill passed); **WebSocket P2** (routing + shadow parity + WS-health status + `network.*` HARD CONSTRAINT 12; coordinator-APPROVED, settings v39, `transport` stays `rest` so the dataset is unaffected). See `project-websocket-migration`.

## 2. IMMEDIATE NEXT TASK — (B) the manual `resolution_profiles["3"]` re-baseline (data now ready)
The **Asia/London accuracy fix** — settle the v36 ×2.1 ROC proxy with measured 3-min values. **Data gate now MET:** the 3-min book is **all weekday** (Wed 06-17 + Thu 06-18 + Fri 06-19 — the v34 weekend confound is gone), and the trader's hours (Asia hr6-7, London hr8-10) are the optimum slice, not a limitation. **Both ROC keys are CSV-measurable** (preview already done, single-day, caveated — re-run on the full multi-weekday set):
- `roc_magnitude_threshold` — from `|ROC|` (CSV col 15). Single-Thu preview: NY-1m p50 0.142 / ASIA-3m 0.224 / LONDON-3m 0.148 → at the current 0.21 it fires 55% ASIA / 37% LONDON vs 62% NY at 0.1 → **0.21 looks too high, esp. LONDON**; ASIA≠LONDON → maybe per-session.
- `roc_slope_delta_threshold` — from the `ROCSlope` **label** distribution (col 16, RISING/FALLING/FLAT). Single-Thu: 3-min **81% FLAT** (ASIA+LONDON) vs NY 41% → **0.105 over-suppresses**. (The numeric delta isn't logged — the v33/v34 label-distribution method is the route; raw-delta logging is N/A/unnecessary.)
- **Method:** v33/v34-style firing-rate-matching, per session, **approval-gated** (scoring votes → trader signs off the proposed values before apply). Confound caveat: can't isolate resolution-scaling from session-vol (no session runs both res) → match firing-rate, don't rescale.
- **Pairs with** the §12 WATCHING **3-min hold-window recalibration** (same data trigger — 3-min sessions fail by window-expiry; failure collapses as hold lengthens).

## 3. Backlog (detail: `DeribitIndicatorProject.md` §12/§13/§16 + the memories)
- **WebSocket P3 (cutover)** — `transport` default → `ws` + 15m-TTL collapse. **Gated** on the data-gated re-baselines closing on single-transport data (so AFTER (B) + more data). **Before flipping, resolve the §5.2 trades-staleness call** (`websocket-migration-p2-spec-back.md`): `IsDegraded()` is "all streams stale", so a lone-stale (quiet-market) trades stream skips a `transport=ws` run — coordinator recommends gating trades on connection-health, not last-trade-age.
- **WebSocket P4+** — the post-migration feature catalogue (realtime exit guard, on-close analysis, time-averaged OFI [⚠ re-baseline], …), each its own spec.
- **Auto-tweaker first live NY×1 fire** — data-gated on a real >40%-failure NY×1 window; stays HELD (dry-run, coordinator reviews diff). The "insufficient tier" the trader saw is expected (conservative book).
- Engine spec-first lower-payoff: D3/D4/P6/P12 etc.

## 4. Working rules / seat
Spec-first (novel/scoring → committed `/docs` proposal first); **approval-gated** (scoring changes need explicit trader sign-off — `analysis/` offline + display + tooling are safe to proceed); **local-first, NEVER push**; host-agnostic `analysis/`+`tools/`+the WS feed (no WinForms); display-parity hard rule (card ↔ `BuildPlaintextSnapshot`); delete test screenshots; the skill's bundled ATR bands are STALE → use [[reference-atr-bands-v37]]. Co-author trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Implementer spec-back → coordinator (you): re-run solution + AutoTweaker + `verify/ordercheck` harness, audit the diff, record a `> Coordinator review` callout + local commit.

## 5. Housekeeping note
The 3 docs dirty at session-open (`p3-maintenance-pass-proposal.md`, `ui-reskin-handover-2026-05-22.md`, `websocket-migration-p1-spec-back.md` — the last carries the trader's P1 drill annotation) are pre-existing/unrelated; every commit this arc excluded them. The stale `.claude/worktrees/*` were cleaned 2026-06-19.
