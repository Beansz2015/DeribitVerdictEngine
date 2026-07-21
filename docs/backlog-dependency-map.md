# Backlog Dependency Map

**Created:** 2026-07-22 (trader-directed). **Purpose:** one referral table for everything outstanding — what blocks what, so any seat can see what must happen first. **Authorities unchanged:** `roadmap.md` = execution order · `profitability-risk-levers.md` = profitability lens · `post-websocket-post-calibration-backlog.md` = full item detail. This doc is the CROSS-REFERENCE, not a new authority. **Maintenance:** update State on every tick/ship; move dead rows to the bottom section rather than deleting.

| Item | Class | Blocked by | Unblocks | State (2026-07-22) |
|---|---|---|---|---|
| **#7 liq-cascade alarm + #8 level alerts** (one spec, carries the first-liq-seen diagnostic) | Display/ops | Nothing — trader D-table (`liq-cascade-level-alerts-proposal.md` H1–H5) | **A4** (the diagnostic is A4's gate instrument) | Spec PROPOSED 2026-07-22 |
| A4 liquidation × OFI flip ⚠ | Scoring | #7 diagnostic live + ≥1 real cascade observed (F9: zero liq-marked trades in 8,025 runs) | — | Gated |
| WS-health line persistence | Ops | Nothing | Closes "feed health is inferred" caveat | Buildable any gap (Opus, tiny) |
| 3-min weekday-ASIA `session_volume` re-verify | Calibration | Data gate likely MET (verify ≥50 weekday-Asia 3-min rows) | Possible dial-back of v34's Saturday-set 1.10/1.05; bundles the OBV re-anchor | **READ DONE 2026-07-22** (`asia-session-volume-reverify-2026-07-22.md`: weekday Asia materially calmer — trade-rate 29.4% vs 63.1%; REC: dial to neutral 1.00/1.00 — **awaits trader tick**, then a small settings pass) |
| v28 target-hit vs barrier-hit gap (§12) | Measurement | None (migrated matrix + [T]/[B] make it readable) | Closes the §12 row | Readable now |
| **D7 CONFIRMED-tag re-read (RE-OPENED)** | Display → possibly ⚠ | None — new evidence (placed-geometry §6: CONFIRMED 13.3% [8–21] vs MEDIUM baseline ~33–35%, CI-separated; the 06-24 "≈ baseline" resolution was on the old yardstick) | P5 policy values; the never-built §6 clarity fix (separate directional vs lean tags) | **READ DONE 2026-07-22 — ARTIFACT** (`d7-confirmed-reread-2026-07-22.md`; 06-24 resolution holds; P5 un-flagged; §6 clarity fix is now the actionable remainder) |
| §6 report clarity fix (directional vs lean tags) | Display | D7 verdict (rides it) | Honest context table | Never implemented (D7 spin-off 2, 2026-06-24) |
| Geometry-modes study re-read (v56 instrument) | Measurement | Book ~doubles (~mid-Aug) or the W6-1 audit — DIVERGENT flags must clear | TP-half (nearest-mode) live ⚠ candidate | First run 2026-07-21: nearest tops table, not separable |
| res-3 §5.2 (aggr-vel Asia/London thresholds) | Calibration | ~150 fires/session (late Jul) | Burst scoring auto-arms on those sessions | Data-gated |
| Funding calm-week re-read | Watch | A calm funding week | Fully closes the v52+v53 window | Waiting |
| **Bridge soak review** | Consumer | Dated ~Jul 26–30 | Live ladder: ARM → live-at-min-size → then L3 spec slot; **⚑ P5 values carry the CONFIRMED-inversion reminder (seat-handover §2.3)** | Dated |
| **#6 absorption activation gates ⚠** | Scoring | Post-F4 eval data ~Jul 29–31 (independence + ≥10pp gradient, n≥30) | Absorption Step-2 penalty wire-in | Dated |
| W6-1 LONDON ruling | Calibration | Audit re-run ~Aug 1 (+ what-if LONDON grid — L5/L6) | LONDON stop_max override; swing-buffer design input | Dated |
| W6-4 offline ceiling audit | Measurement | ~3–4 wks of v0.8 rows (early Aug); method draftable NOW | **W6-5 (B1)** + any W6-7 Tier-C spend | Prep-ahead OK |
| F1 tier-ladder re-read | Measurement | n≥150 STRONG (report §9 ladder = the instrument; currently n≈103 pooled) | **Kelly CAL (W6-3/L4)** + P5 tier values | Gated |
| Kelly CAL (EST→CAL) | Display | F1 re-read passes | L4; consumer sizing reads empirical kelly fields | Gated on F1 |
| L3 stop-distance sizing (order app) | Consumer | Live-at-min-size stabilises (post-soak) | **L9 un-clamp** + the widest-stop mode's live eligibility | Queued |
| L9 structural-stop un-clamp + swing-buffer | Scoring ⚠ | L3 shipped (+ calm-regime re-derivation, DG5) | The trader's real stop method live | End-state |
| Tweaker first fire (W5/W6-2) | Loop | A >40%-failure NY×1 window (post-migration rates only) | Window/MinTier recalibration | Data-gated, supervised dry-run first |
| D2-v2 volume-weighted pivot promotion | Geometry | Revive only with evidence — test via the v56 what-if modes | — | Parked (B4b partially superseded) |
| D3 / D4 / D5 / D6-backlog (5m RSI-div · Donchian×BBW · smart OBV · MFI) | Scoring refinements | W6-4 must show combination headroom (the W6 new-indicator bar) | — | Parked |
| A5 VPFR shape classification | Scoring | 30-day book | — | Data-gated (approaching) |
| C1 / C2 (multi-session VPFR · anchored VWAP) | Scoring | Multi-session state plumbing → effectively behind Q6 | — | Parked |
| CLI port (Q6) | Platform | Trader ruling 07-08: AFTER the W6 programme — do not pull forward | O3 | Deferred LAST |
| W6-7 cross-venue lead-lag | New signal class | Current queue done + W6-4; #5-style gates bind activation | The one remaining non-marginal class | Spec candidate authored |
| Aug-1 handover doc | Process | ~Aug 1 (Pro downgrade Aug 2) | The credit-rationed month | Scheduled — this seat's last deliverable |
| Untracked strays ruling (`.codex/`, DeepSeek script, `models-full.json`, `tools/tools/`) + UI-automation .ps1 commit | Hygiene | Trader keep-or-delete ruling | Clean tree | Pending |

**Standing rejections (do not re-propose without new evidence):** backlog §E + trader-profile §4 + roadmap §5b (sub-minute baseline cadence, #9 provisional verdict, auth feeds, Phase-2b autotune, A1 spread-momentum — refuted with evidence 2026-07-03).
