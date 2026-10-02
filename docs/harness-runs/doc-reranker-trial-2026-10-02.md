# Doc re-ranker (harness 5) — on-the-go trial, session 1 of ≥ 5 — 2026-10-02 (UTC)

**Trial rule:** [`seat-handover-2026-09-24.md`](../seat-handover-2026-09-24.md) §1 (harness 5) and [`doc-reranker-measurement-plan.md`](../doc-reranker-measurement-plan.md) §4. Real "where was this decided?" questions from engine sessions; verdict after ≥ 5 sessions. **This is the first real trial session.** The gitignored `doc-reranker-query-log.jsonl` held one line (a 2026-09-23 test) before it.
**Seat's expected answer written BEFORE each run** (shadow-mode rule, [`harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §2). Both questions came up for real this session; the seat had found the answers by grep earlier the same day.
**Rev:** `1984dd9`. `jev-latest` resolved to `jev-1.13.0`. One sample each (live mode).

| # | Query (verbatim) | Seat's expected answer | Top 5 holds it? | No-answer Noul | Verdict |
|---|---|---|---|---|---|
| 1 | "Where is the A4 liquidation x OFI flip detector defined?" | `docs/post-websocket-post-calibration-backlog.md`, "A4. Liquidation × OFI Flip Detector" | ❌ No. Top 5: the `history-data-store-spec.md` §2b row, `engine-fix-build-spec-2026-09-21.md` §4.2, the overlay re-audit F1, the alerts proposal, the park spec scope | 0.44 ("no, none answers it") | **MISS — but flagged.** The low no-answer Noul correctly warned the shortlist lacked the answer. Possible cause, not verified: the query says "x" where the doc says "×"; whether the doc reached the K=30 shortlist was not checked |
| 2 | "Where was it decided whether the stage-4 pass rule counts its 14 days by comparison run dates or data windows?" | `docs/history-data-store-build-spec-back.md`, decision `D-6` | ✅ **Top-1** (§4.1, Noul 0.95); same doc's §4 at #3 | 0.93 | **HIT** |

**Cost:** 62 Jev calls, 74,714 input tokens, ~18 s total.

**Read for the trial (one session, not a verdict):** a definition question about an old backlog item missed; a recent decision question hit top-1. The no-answer Noul separated them (0.44 vs 0.93). That matches the acceptance runs, where the separating signal was confidence, not rank.
