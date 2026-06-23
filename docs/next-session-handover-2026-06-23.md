# Next-Session Handover — 2026-06-23 (coordinator / spec-author seat)

You're the **coordinator / spec-author + sanity-check** seat for DeribitVerdictEngine. Write specs, review implementer spec-backs (re-run builds + harness + audit the diff), sequence the work, do small `analysis/`/settings passes directly. **Local-first: commit locally, NEVER push — the trader tests + pushes.**

## 0. Start protocol
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` (full) + `docs/architecture.md`; load `crypto-trading-context`; read the frontier memories — **`project-engine-audit-calibration-trap`**, **`project-v36-session-timeframe`**, **`project-websocket-migration`** (all updated 2026-06-23). Then `git log --oneline -20` + `git status`.

## 1. Frontier (2026-06-23)
- **Versions:** settings **v41** · CSV v0.7 · eval cache v4.
- **origin/master = `9cde370`** · **18 local commits unpushed** (`4bf3333` → `dde5485`). The trader pushes after the live tests below. Don't push.
- **Shipped this arc (all local):** WebSocket **P2** (routing + shadow parity, v39) → **B Asia/London ROC re-baseline v40** + **Monday recalibration v41** (ASIA mag 0.17 / LONDON 0.11 / shared slope 0.06) → **P3 cutover spec** (draft, §5 confirmed) → **2 WS fixes** found+fixed during G1 testing: trades-staleness connection-health gating (`808f510`) + reconnect storm-guard flap-counting (`dbf99a4`) → **3-min hold-window recal spec** (`dde5485`).

## 2. ⚠ IN FLIGHT — soak + B-test running on the trader's machine (started ~06:42 UTC 06-23)
Combined 12h **WebSocket soak** + **B v41 live-test**: `shadow_parity=true` staged in `bin/Debug/net8.0-windows/settings.json` (transport=rest), parity log cleared. **WHEN THE TRADER REPORTS IT DONE:**
1. **Parse** `bin/Debug/net8.0-windows/ws_parity_log.txt` — confirm 12h stability (heartbeats held; clean reconnects on any drops; parity streak healthy). Format: `PARITY ok N/50` per clean run, `PARITY MISMATCH (streak reset; K fields)` + a detail line (`MISMATCH book/mark_price` = benign snapshot-jitter; `WS-NOT-READY trades` should be GONE post-`808f510`).
2. **Check B-test verdicts** — Asia/London more tradeable (esp LONDON, was over-suppressed at 0.21→now 0.11), `EXEC 3m` tag shows, **NY (hr13+) byte-identical**. This closes the B v41 live-test.
3. **RESET** `shadow_parity=false` in the bin (gate done).
4. Trader **pushes** the WS stack + B re-baseline.

## 3. WebSocket G1 (cutover gate) — 3 of 4 done
parity ✓ (51/50, after the trades fix) · reconnect drill ✓ (~20s recovery, after the storm-guard fix) · fallback drill ✓ (dead feed → silent REST fallback) · **24h→12h soak = the running test above**. After G1: **P3 cutover stays gated on G2** (the single-transport re-baselines closing on REST data). P3 spec = `websocket-migration-p3-cutover-spec.md` (trades fix already shipped early; transport flip + 15m-TTL collapse remain, manual dated flip, POCO default stays "rest").

## 4. Hold-window recal — handed to a fresh implementer conversation
Spec `three-min-hold-window-recalibration-proposal.md` (`dde5485`). Trader spun off a separate conversation to build it (resolution-scaled `HoldWindowsMinutes` = `{5,10,15}×execRes` → 3-min `{15,30,45}`; analysis-layer, no scoring votes, auto-tweaker-safe). **Its spec-back routes to you** — re-run Release builds + OrderCheck harness + audit the diff (`FailureRateMatrix`/`tools/AutoTweaker` should be NY×1 byte-unchanged) + the post-soak offline-report validation + a `> Coordinator review` callout + local commit. NOTE: that conversation was told **Release builds only, no Debug rebuild/relaunch** (live app mid-soak) and to **defer the offline-report validation to after the soak**.

## 5. Backlog (after the above)
- **Auto-tweaker first live NY×1 fire** — data-gated on a real >40%-failure NY×1 window; stays HELD (dry-run, coordinator reviews diff).
- **G2 re-baselines** then P3 cutover. P4+ post-migration features each own spec.

## 6. Working rules / housekeeping
Spec-first (novel/scoring → committed `/docs` proposal); approval-gated (scoring changes need trader sign-off; `analysis/`+display+tooling safe to proceed); **local-first, NEVER push**; host-agnostic `analysis/`+`tools/`+WS feed (no WinForms); display-parity hard rule (card ↔ `BuildPlaintextSnapshot`); delete test screenshots; skill's bundled ATR bands STALE → use [[reference-atr-bands-v37]]. Co-author trailer: `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Live app reads the **bin** `settings.json` (not the tracked root) — gitignored, safe to edit for tests, restore after. The 3 docs dirty at session-open (`p3-maintenance`, `ui-reskin-handover`, `websocket-migration-p1-spec-back`) are pre-existing/unrelated — every commit this arc excluded them.
