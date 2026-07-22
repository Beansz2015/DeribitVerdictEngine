# Liquidation-Cascade Alarm (#7) + Level-Approach Alerts (#8) · Proposal

**Date:** 2026-07-22 · **Status:** APPROVED — **H1–H5 TICKED 2026-07-22, H4 AMENDED by the trader: the first-liq-seen event must be PERSISTED (it could be rare; a per-process flag dies on restart) and future checks must be able to find it.** Amended H4 shape: a tiny append-only sidecar `liq_events.log` next to the CSV — one line per event (`utc | kind(FIRST_SEEN|CASCADE) | side | usd | instance_id`), written on the FIRST liq-flagged trade ever observed AND on every cascade trigger thereafter (rare, tiny, never rotated). This IS the durable A4 gate evidence: the A4 unlock check = "does liq_events.log contain ≥1 CASCADE line". The strip-tooltip flag reads the file's existence, so it survives restarts. BUILD-AUTHORIZED (Opus small, any gap; settings bump from whatever is current — v58 shipped the same day, so this build takes v59). · **Type:** display/alert only — ZERO scoring impact, no ⚠ boundary. The §11 catalogue's two remaining gap-fillers, paired per the roadmap note.
**Why now:** #7 must carry the **first-liq-seen diagnostic** — the audit (F9, 2026-07-03) found `LiqSignal` has NEVER fired in 8,025 runs (plumbing verified REST+WS end-to-end; the signal is unproven live). Until that diagnostic exists and one real cascade is observed, **A4 (liq×OFI flip) stays locked**. #8 is the trader-facing timeliness half: the scoring penalty already exists; these alert the trader while it happens.

## 1. #7 — Cascade alarm

When liq-flagged trades stream in a cascade (≥ `cascade_min_trades` within `cascade_window_sec`, either side), raise the alert surface (H1) + append one `[LIQ] first liquidation-flagged trade seen: <ts> <side> <usd>` log/dump line THE FIRST time any liq-flagged trade is ever observed per process (the A4 gate instrument — fires even for a single trade, below cascade threshold). Reuses the exit-guard strip pattern; WS trades stream is the source; REST fallback degrades to per-run checks (never blocks).

## 2. #8 — Level-approach alert

When price comes within `level_ticks` of an active CARRIED level (the TAPE strip's candidate set: swing5m + VPFR HVN — carried, never recomputed), raise the same surface once per level-approach episode (re-arm on leave, the absorption-episode pattern). Purely awareness — no verdict, no payload field.

## 3. Settings (new `alerts` block; settings v57→v58; ALL display-tier)

`{enabled:true, cascade_min_trades:3, cascade_window_sec:10, level_ticks:12, sound_enabled:false}` — provisional anchors, trader-adjustable in the UI (operational saves, no bump). Tweaker: `alerts.` prefix fenced (next free HC number) — display keys must never enter the tuning surface.

## 4. H-table

| # | Decision | Recommendation |
|---|---|---|
| **H1** | Alert surface | **TAPE-strip tag + status-bar flash** (the exit-guard/ABS pattern); `sound_enabled` optional, default OFF. Alternative: modal/banner — rejected rec: interrupts the discretionary read |
| **H2** | Cascade definition | **≥3 liq-flagged trades in 10s** (provisional — nobody has seen a live cascade; re-anchor after the first real one) |
| **H3** | Level-approach distance | **12 ticks** (= the absorption `proximity_ticks` anchor, one mental model for "near a level") — episode re-arm on leave |
| **H4** | First-liq-seen diagnostic shape | Log/dump line + a `LiqEverSeen` per-process flag surfaced in the strip tooltip; NO CSV column (the A4 gate needs one observation, not a series) |
| **H5** | Slot / model | Any gap, **Opus small**; spec-back `liq-cascade-level-alerts-spec-back.md`; fixtures pin cascade window math + episode re-arm + fence |
