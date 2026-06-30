# TraderGuide + UserManual Refresh — Spec (bring current through v46)

**Status:** PROPOSED — ready for a fresh, lower-cost seat. Pure docs update: **no code / settings / CSV change, no §15 entry, no version bump.**
**Recommended seat:** a NEW conversation on **Sonnet 4.6, medium effort.** The work is guided + largely mechanical, but it's trader-facing prose plus one framing reconciliation, so not Haiku; Opus is overkill. ~1 focused session. Local-first — commit as you go, do NOT push.
**Goal:** update `docs/TraderGuide.md` (how to read verdicts / when to act) and `docs/UserManual.md` (field-by-field reference) so both match the live app through settings **v46** (time-averaged OFI).

## 0. Start protocol (for the fresh seat)
Read `docs/DeribitIndicatorProject.md` + `docs/architecture.md`; load the `crypto-trading-context` skill; skim `settings.json` `change_log` v32->v46 and `DeribitIndicatorProject.md` §15 (the per-version summaries are the change source). Do NOT read every `.vb` — open only the surfaces named below. To see live output, use the screenshot tools (memory `reference-ui-automation-tools`) or read `BuildPlaintextSnapshot` in `UI/MainForm_PlaintextSnapshot.vb`.

## 1. Why the docs are stale (verified 2026-06-30)
- UserManual "Source of truth" still lists `UI/MainForm_Render_Header.vb` / `MainForm_Render_Sections.vb` — **both retired in the P5b reskin.** The live render is the **card** layer (`UI/MainForm_Render_Cards.vb`) plus the **plaintext snapshot** (`UI/MainForm_PlaintextSnapshot.vb`, `BuildPlaintextSnapshot`), which stay in parity.
- UserManual says "settings.json v24" — current is **v46**.
- Neither doc covers any of v32->v46 (below). TraderGuide's "output top-to-bottom" list predates every P4 display element.

## 2. The v32->v46 delta to add / fix
Each item: where it surfaces + a one-line user-facing description to write.
- **v36 execution resolution** — NY runs **1-min**, Asia/London **3-min** (`ExecutionResolution`). The "Core Signals (1m)" framing and the perf strip are resolution-dependent; say so.
- **v37 ATR bands** — 1-min Low<20 / Normal 20-55 / High>55; 3-min ~Low<42 / Normal 42-115 / High>115 (was 80/150). Update any ATR-band table; note it's resolution-dependent.
- **v40/v41 ROC re-baseline** — per-session Asia/London ROC magnitude + a re-derived 3-min slope. One-line note in the ROC/Core section (no user action change).
- **WS migration (v38-v42, transport=ws)** — the app is live on WebSocket. Document the **WS-health status line**: `WS OK · 1/3/5/15 fresh · trades N` / `WS DEGRADED -- REST fallback` / `WS DOWN -- reconnecting`.
- **P4 #1 exit guard (v43)** — the full-width **EXIT GUARD** strip + optional alarm; fires on fast microstructure deterioration while a position is declared. **Display/alert only — never changes the verdict.** Document its states (Clear / EXIT).
- **P4 #2 on-close mode (v44)** — the **INTERVAL | ON-CLOSE** toggle + `Next close: M:SS` countdown; on-close fires the run at each exec-resolution bar close instead of on the interval timer.
- **P4 #3 live microstructure TAPE strip (v45)** — the **TAPE** strip + checkbox: last price bracketed by nearest structural levels (SH/SL/HVN), TFI, spread (bps), top-book imbalance, tape speed (tr/s + $/s). **Awareness only, deliberately NOT a verdict** — explain the distinction so it isn't read as a signal.
- **P4 #4 time-averaged OFI (v46)** — on the WS path the OFI **Ratio** is now a time-weighted average over the run window (cleaner/steadier; transient sweeps damped). Same field, averaged value. **Cosmetic note worth including:** the OFI card shows `ratio · bid · ask` where the averaged ratio won't equal bid/ask (average-of-ratios vs averaged volumes) — by design.
- **Fix the stale references:** "Source of truth" file list Header/Sections -> `MainForm_Render_Cards.vb` + `MainForm_PlaintextSnapshot.vb`; "v24" -> v46.

## 3. Scope / non-goals
- Anchor the UserManual field-by-field reference on **`BuildPlaintextSnapshot`** (the canonical text surface that mirrors the cards). Light-touch the "RTF output pane" framing to "card layout + plaintext snapshot" — don't rewrite the whole doc.
- Do **NOT** document dev-only tooling (the verification gate, the screenshot tools, the auto-tweaker internals) — these are end-user/trader docs.
- Keep the existing Kelly advisory note and the conservative-bias framing; they're still accurate.

## 4. Acceptance
- Both docs reference **v46** and the current render surfaces; every P4 display element (WS-health, exit guard, on-close, TAPE) + averaged OFI is covered; ATR bands updated to v37; no mention of retired Header/Sections as live surfaces.
- Spot-check 2-3 sections against the live app (screenshot tools) or `BuildPlaintextSnapshot`; **delete any screenshots afterwards** (memory `feedback-delete-test-screenshots`).
- Docs-only commit(s); local; trader reviews + pushes.
