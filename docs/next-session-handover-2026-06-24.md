# Next-Session Handover — 2026-06-24 (coordinator / spec-author seat)

You're the **coordinator / spec-author + sanity-check** seat for DeribitVerdictEngine. Write specs, review implementer spec-backs (re-run builds + harness + audit the diff), sequence the work, do small `analysis/`/settings/tooling passes directly. **Local-first: commit locally, NEVER push — the trader tests + pushes.**

## 0. Start protocol
Per `CLAUDE.md`: read `docs/DeribitIndicatorProject.md` (full) + `docs/architecture.md`; load `crypto-trading-context`; read frontier memories — **`project-websocket-migration`**, **`project-engine-audit-calibration-trap`**, **`project-v36-session-timeframe`**. Then `git log --oneline -20` + `git status`.

## 1. Frontier (2026-06-24) — WebSocket P3 is BUILT, REVIEWED, and TRIAL-VALIDATED; one decision left: the trader's cutover flip
- **Versions:** settings **v41** · CSV v0.7 · eval cache v4.
- **~12 local commits unpushed** (the WS arc through P3 + the gate-trial tolerance fix). Trader pushes. Don't push.
- **The engine's directional reach-target reality (from the D7 investigation):** NY directional verdicts = **43% reach target / 53% window-expiry / 4% adverse** — they rarely *lose*, they just often don't travel a full 2×ATR in-window. Not a bug; a barrier/window-proxy + calibration topic (parked, post-cutover).

## 2. WebSocket P3 — state + the ONE open decision
P3 cutover (`docs/websocket-migration-p3-cutover-spec.md`, status READY/promoted; spec-back `…p3-spec-back.md`, coordinator-APPROVED). Build = §3 trades-gate (`808f510`) + §4 15m-TTL collapse (`MtfRefreshPolicy.vb`) + §1#5 closed-bar volume tolerance + A16 harness. **Both gates MET** (G1 live + G2 re-baselines).

**Gate trial (2026-06-24) — DONE, WS data validated:**
- **Volume fix CONFIRMED** — 0 closed-bar mismatches across 214 runs (soak had 72).
- **Live `transport=ws` smoke-test PASSED** — WS OK, all 4 series fresh, 214 runs no skips, no ws-specific desync.
- Funding + book parity tolerances widened (`8dbd47e`-style follow-up commit): funding → `Math.Max(5e-8, |rate|·5%)`; book 5→8 ticks. (The 1e-10 funding epsilon was tripping ~1e-8 rounding.)
- **Book/mark gate excursions DIAGNOSED + CLEARED as a comparer artifact (NOT a WS problem):** the comparer compares **run-start REST** vs a WS snapshot read **~sub-second-to-1s later**, so moving-value (book/mark) gaps = price move over that skew window. Evidence: all gap *directions* track price direction (ws read later → higher when price rose / lower when it fell); candles (settled) never trip; direction rules out WS staleness (ws is *newer*, i.e. fresh ≤100ms). **The skew exists ONLY in the comparer — in live `transport=ws` the engine reads WS fresh, so the excursions don't exist in real operation.** WS data is sound; the ≥50-consecutive gate just fights the comparer's own read-skew on moving fields.

**THE OPEN DECISION (trader's):** cutover is **data-justified** (candles byte-identical; excursions are instrument-only). Either (a) flip `transport=ws` + stay + dated §15 marker + push, or (b) get a literal clean ≥50 first by running in a very calm stretch. Do NOT "fix" the comparer skew — it's a validation instrument; widening its moving-field tolerances further is whack-a-mole.

**Cutover mechanics (P3 §5):** natural path = run with `shadow_parity=true` (feed live) → hot-flip `transport=ws` (takes effect next run, no restart) → it STAYS ws. Rollback = hot edit back to `rest`. POCO default stays `rest` (fail-safe). No CSV transport column.

## 3. ⚠ Live app / bin state — SOFT-FLIPPED TO WS (trader cutting over here)
The bin `bin/Debug/net8.0-windows/settings.json` is now **`transport=ws`** + `shadow_parity=true` — the running app is **live on WS** (soft/live cutover, the trader-requested "flip it", 2026-06-24). **The trader is completing the FORMAL cutover in this conversation.** Formal cutover (P3 §5, the trader's deliberate action): edit the **tracked-root** `settings.json` `transport: "ws"` + add the dated §15 / `change_log` **dataset-boundary marker** + push the WS stack (~12 commits). **The bin flip alone is ephemeral — a Debug rebuild resets the bin to the tracked root (`rest`), so the tracked-root edit is what makes the cutover survive a rebuild.** Set `shadow_parity=false` for the clean final state once early-WS monitoring is done. Rollback = `transport` back to `rest` (hot, next run). Parity log `ws_parity_log.txt` is the instrument.

## 4. Backlog (after the cutover)
- **P4 post-WS features** — each its own re-baseline-flagged spec; recommended first = realtime exit guard / on-close analysis (zero scoring impact), then time-averaged OFI (first re-baseline upgrade). Also the early-resolution-on-confirmed-hit stub (WS proposal §11 item 11) + the comparer-skew reduction (optional instrument polish).
- **Auto-tweaker first live NY×1 fire** — data-gated on a real >40%-failure NY×1 window; HELD.
- **D7 CONFIRMED-tag** — INVESTIGATED + RESOLVED (not a tag defect; `post-websocket-post-calibration-backlog.md` D7). The real signal (directional reach-target / barrier-vs-window calibration) is parked, transport-invariant, post-cutover-OK.
- **Hold-window recal** — CLOSED (plateau-validated). **3-min Asia/London now have genuinely strong tiers** (ASIA MEDIUM_LONG 13% fail @30m = best cell in the book).

## 5. Working rules
Spec-first (novel/scoring → committed `/docs` proposal); approval-gated (scoring changes need trader sign-off; `analysis/`+display+tooling safe to proceed); **local-first, NEVER push**; host-agnostic `analysis/`/`tools/`/WS feed; display-parity hard rule (card ↔ `BuildPlaintextSnapshot`); delete test screenshots; skill's ATR bands STALE → use [[reference-atr-bands-v37]]. Co-author trailer `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Live app reads the **bin** `settings.json` (gitignored). The 3 pre-existing dirty docs (`p3-maintenance`, `ui-reskin-handover`, `websocket-migration-p1-spec-back`) are unrelated — every commit excludes them.
