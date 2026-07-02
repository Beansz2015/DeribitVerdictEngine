# Next-Session Handover — 2026-07-03 (Fable window, days 2–5)

**For:** the next Fable 5 conversation. **Supersedes** `next-session-handover-2026-07-02.md` (that was the audit brief; the audit + fixes + v48 are DONE).
**Author:** Fable seat, 2026-07-03 ~01:00 local (GMT+8). The trader has Fable until **Jul 7**; Opus on Max-5x for the month after. Fable = specs/derivations/rulings; Opus = implementation.

## 0. Start protocol

Per CLAUDE.md: read `docs/DeribitIndicatorProject.md` + `docs/architecture.md` in full, load the `crypto-trading-context` skill, **read `docs/roadmap.md` in full** (it is the sequencing authority and this window's plan), skim the memory index. Then this doc. Frontier memories: `project-roadmap-signal-bridge` (the dense current-state record), `project_audit_2026_07_02`, `project_websocket_migration`.

**⚠ Timezone lesson (a real trap this session):** the machine/trader are **GMT+8**; CSV timestamps are **UTC**; date-change notes fire at local midnight ≈ 16:00 UTC. Always compare against NOW-in-UTC before claiming a data gap — the previous seat wrongly declared the collection dead over a 19-minute window because of this.

## 1. State (all LOCAL, gate-green, UNPUSHED — trader tests + pushes; ~30 commits ahead of origin `a00ac35`)

- **settings v48 SHIPPED + LIVE** (`a86b4f1`): OFI dominance 2.0/0.5 → **1.60/0.625** (geometric re-baseline, firing-rate-matched; spec + spec-back `v48-ofi-dominance-rebaseline-*.md`). Bin flipped ~16:35 UTC 07-02, hot-reload proven by boundary-band rows. `momentum_threshold` unchanged. **Roadmap rule-1 window CLOSED → P4 #5 aggressor velocity is buildable** (spec approved days ago — a ready-to-paste Opus job).
- v47 audit fixes shipped + coordinator-approved earlier (spec-back §7 callout). The 2026-07-02 full audit found the engine SOUND (report `fable5-audit-2026-07-02.md`).
- **Signal bridge:** `signal-bridge-v1-proposal.md` (trader-approved rulings R1/R2 inside; §8 D1–D5 recommended, not yet formally ticked) + the cross-project **orchestrator brief** `signal-bridge-orchestrator-brief.md` delivered to the order-app orchestrator (they were converging on a similar contract independently). **Awaiting their field-level diff.**
- Collection: Debug exe (geometric build 07-01 07:01:45Z) collecting via WS/on-close; the trader powers the PC down overnight local. Post-flip rows are the v48-era book.

## 2. Work queue for this window (roadmap §4; adjust by trader priority)

1. **Signal-health audit** (W1 — pulled forward; data exists in the post-v42 book). Per-signal fire rates, pairwise agreement, conditional barrier outcomes across OFI/TFI/CVD/MicroCVD + FundingMomentum/RSI-div/OISignal/spread. **Named questions with evidence waiting:** (a) **OFIMomentum is active ~90% in BOTH eras** (ref 90.6% / geo 89.2%; v48 spec-back §4) — an always-on modifier; measure barrier outcomes conditional on its bonus/suppression actually moving the score → retire-or-keep recommendation (retirement = scoring change, spec + sign-off). (b) **Spread revival** — v34 called the WIDE penalty REST-dead at 5bps; the 100ms WS book revived it; re-derive wide/tight thresholds from the live distribution (A1 spread-momentum rides along). (c) FundingMomentum fire rate on WS-continuous funding_8h (threshold 5e-8 may now over/under-fire). Output = one consolidated retune/retire spec for trader sign-off. **Run this in a fresh context — it is data-heavy.** Boundaries for slicing the book: v42 cutover 06-24 (WS), 06-30 13:00 UTC (arith-averaging deploy), 07-01 07:02 UTC (geometric), ~16:35 UTC 07-02 (v48 pair).
2. **Orchestrator reply → schema freeze.** When the order-app orchestrator's field-diff arrives: reconcile against the brief's schema v1, cut the final schema, mark it FROZEN in `signal-bridge-v1-proposal.md`, then both implementation lanes open (Opus, both repos). **Defend the semantic rules, be flexible on names/shape:** `direction: NONE` on ALL `NO TRADE*` leans; effective post-cap levels as the placed values; SKIPPED-means-stand-down (and stale-file-means-stand-down); de-dupe on `signal_id`; tier-based (never raw-score) action mapping. R1/R2 are trader rulings, not negotiable by the other side.
3. **P4 #6 book-absorption spec** (snapshot-feed v1 — public data, no auth, no incremental-book plumbing; must be specced against the audit's redundancy findings, so sequence after item 1).
4. **CLI-port run-state extraction spec** (W4): host-agnostic run-context for `_fundingHistory`/`_ofiHistory`/`_oiHistory`/MTF cache/`_prevRegime` + headless runner skeleton; zero behaviour change.
5. **Jul 7: month handover doc** — Opus execution order (roadmap §5), gates, review checklists, sign-off points. Mandatory before the window closes.
6. Buffer items if time: reach-target calibration spec (D7 spin-off, O2-elevated); #7/#8 display-alert mini-spec.

Opus can run in parallel NOW (paste-ready briefs were given 07-02): #5 aggressor-velocity build; bridge implementation once schema freezes; P13 UserManual tier table; WS-health log persistence.

## 3. Standing rules (unchanged)

Local-first, NEVER push (trader tests + pushes; exclude the 3 pre-existing dirty docs + untracked non-engine files). Spec-first for scoring/novel changes. One ⚠ scoring change per dataset boundary (rule 1 — next boundary opens when #5 builds). Gate before/after everything: `tools/checks/verify-gate.ps1 -Mode prepush` (Release-only — never build Debug while the collector runs). Display parity is now **three** surfaces (snapshot ↔ cards ↔ signal file, once the bridge ships). Delete test screenshots. §12 watches now include the **v48 OFI per-session fire-rate watch** (recipe in the v48 spec-back §3) and the trader's **F3 observational watch** (exit-guard strip vs HOLD\EXIT row during holds).

## 4. Open questions parked for the trader

- Bridge §8 D1–D5: recommended-not-ticked (likely moot once the orchestrator reconciliation lands — re-confirm then).
- TAPE-strip "WS only" during quiet tape (observed 07-03 00:22 local while WS OK): if persistent, it's the strip's 10s trades-age tick gate — same age-vs-connection-health class as the P3 §3 fix; display-polish candidate, not yet scheduled.
