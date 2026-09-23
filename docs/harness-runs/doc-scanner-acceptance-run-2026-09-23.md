# Doc scanner (harness 4) — ACCEPTANCE run, 2026-09-23 (UTC)

**What this is:** the one live Jev run for `doc-scanner-check-spec.md` §5 item 7, the build's acceptance window. ⛔ **It is NOT the measured first run.** That run is the seat's, on the living set at `cbc2c91`, with a seat-written baseline (`doc-scanner-check-spec.md` §6; `harness-shadow-mode-protocol.md` §4a and §4c).

| | |
|---|---|
| Tool | `tools/checks/doc-scanner.ps1` at `b51f797`, enumeration by `tools/checks/lib/doc_scanner_candidates.py` (E2 pairing `ruled` = the orchestrator's ruling `DS-A` (g); dating = UTC) |
| Revision of the docs | `-Rev 55de8fb` |
| Window | `-Docs` = `docs/seat-handover-2026-08-2*.md` (5 files), `docs/seat-handover-2026-08-10.md`, `docs/seat-handover-2026-08-12.md`, `docs/next-session-handover-2026-05-14.md`, `docs/next-session-handover-2026-05-18.md`, `docs/fable-continuation-2026-07-23.md` |
| Overlap with the living set | **0 docs** (`in_living_set=0` in the coverage block). All ten are dated historical handovers |
| Baseline | [`doc-scanner-20260923T135211Z-acceptance-baseline.json`](doc-scanner-20260923T135211Z-acceptance-baseline.json). **Implementer-written**, committed at `83fe0a6` BEFORE this run. It measures implementer-versus-detector agreement, not seat-versus-detector |
| Run | 2026-09-23 13:52:36Z → 13:53:09Z (33 s start to finish) |
| Jev | **14 items × 5 samples = 70 calls. `USAGE_INPUT_TOKENS=60593`. Cost $0.0025** (60,593 × $0.042/Mtok, input only; `docs/seat-handover-2026-09-22.md` §1). The Jev loop's `WALL_TIME_SEC=23.19` |
| Exit | **1**: 4 judged findings, 4 unstable rows, 6 code-only findings |

## Coverage block (as printed)

```
REV=55de8fb
LIVING_DOCS=12  (list: CLAUDE.md docs/DeribitIndicatorProject.md docs/architecture.md docs/trader-profile.md docs/trader-tick-queue.md docs/roadmap.md docs/backlog-dependency-map.md docs/csv-rotation-riders.md docs/harness-shadow-mode-protocol.md docs/UserManual.md docs/aws-collector-deploy-checklist.md docs/seat-handover-2026-09-23.md)
DOCS_SCANNED=10  source=explicit  in_living_set=0
E2_PAIRING=ruled  DATED_STATE_DATES=utc  LIVE_SETTINGS_VERSION=v68
CANDIDATES_VERSION=2  CANDIDATES_VALUE=3  CANDIDATES_POINTER=1  CANDIDATES_FIXTURE_MEANING=8
VALUE_NEVER_SHIPPED_CODE_ONLY=0  VALUE_UNQUALIFIED_NEVER_SHIPPED=1  VALUE_OPERATOR_NEVER_SHIPPED=0
CFG_MEMBER_MISSING=1  LINE_PAST_EOF=0  DATED_STATE_OVER_HORIZON=4  NEXT_FREE_FAMILY_STALE=1  NEXT_FREE_FAMILY_CLAIMS=4
ITEMS_JUDGED=14  JEV_CALLS=70  USAGE_INPUT_TOKENS=60593  WALL_TIME_SEC=23.19
```

## Per-item verdicts (acceptance window only)

```
VERSION|docs/next-session-handover-2026-05-14.md|317@55de8fb [STABLE] verdict=asserts_current agreement_rate=1 mean_top_prob=0.736 baseline=asserts_current [AGREE]
VERSION|docs/next-session-handover-2026-05-18.md|365@55de8fb [UNSTABLE] verdict=asserts_current agreement_rate=0.8 mean_top_prob=0.554 baseline=asserts_current [AGREE]
VALUE|docs/seat-handover-2026-08-10.md|15@55de8fb [STABLE] verdict=history_or_quote agreement_rate=1 mean_top_prob=0.86 baseline=unsure [OPERATOR_UNSURE]
VALUE|docs/seat-handover-2026-08-12.md|45@55de8fb [UNSTABLE] verdict=asserts_current agreement_rate=0.8 mean_top_prob=0.59 baseline=history_or_quote [DISAGREE]
VALUE|docs/seat-handover-2026-08-12.md|61@55de8fb [STABLE] verdict=history_or_quote agreement_rate=1 mean_top_prob=0.858 baseline=history_or_quote [AGREE]
POINTER|docs/fable-continuation-2026-07-23.md|3@55de8fb [STABLE] verdict=asserts_current agreement_rate=1 mean_top_prob=0.532 baseline=asserts_current [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-10.md|40@55de8fb [STABLE] verdict=no_description agreement_rate=1 mean_top_prob=0.684 baseline=no_description [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-12.md|48@55de8fb [STABLE] verdict=no_description agreement_rate=1 mean_top_prob=0.858 baseline=no_description [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|33@55de8fb [STABLE] verdict=describes_this_fixture agreement_rate=1 mean_top_prob=0.86 baseline=describes_this_fixture [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|37@55de8fb [STABLE] verdict=no_description agreement_rate=1 mean_top_prob=0.474 baseline=no_description [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|37@55de8fb#2 [STABLE] verdict=describes_this_fixture agreement_rate=1 mean_top_prob=0.642 baseline=no_description [DISAGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|39@55de8fb [UNSTABLE] verdict=describes_this_fixture agreement_rate=0.8 mean_top_prob=0.488 baseline=describes_this_fixture [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|49@55de8fb [STABLE] verdict=no_description agreement_rate=1 mean_top_prob=0.752 baseline=no_description [AGREE]
FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|227@55de8fb [UNSTABLE] verdict=no_description agreement_rate=0.8 mean_top_prob=0.544 baseline=describes_this_fixture [DISAGREE]
```

The `draws=[...]` field per row is dropped here for width. Re-run the command below to see it.

| | Count |
|---|---|
| Agree with the implementer baseline | 10 (8 stable, 2 unstable) |
| Disagree | 3 (1 stable, 2 unstable) |
| Implementer `unsure` (excluded from the score) | 1 |
| Scored agreement | **10 of 13** |

## The three disagreements — adjudicated by reading the line, reasons given

1. **`VALUE|docs/seat-handover-2026-08-12.md|45@55de8fb`** — the Jev verdict was `asserts_current`, UNSTABLE at 4/5. The line is *"`| Settings version | v66 (D2, OBV trend_gate 18→23) |`"*. **I keep my read, `history_or_quote`.** 18 is the from-value inside a change label that records v66 as shipped. The row is unstable, so it is untrusted under `harness-shadow-mode-protocol.md` §4d anyway.
2. **`FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|37@55de8fb#2`** (`A49g`) — the Jev verdict was `describes_this_fixture`, **STABLE at 5/5**. My read was `no_description`. **On a re-read I concede that my baseline was probably wrong.** The sentence right after the mention (*"a fix with only the first false-flags the last hour of every run without `ws_health` evidence"*) describes exactly the case `A49g_AbsentWsHealthSkipsS1S2ToS4StillRun` covers. This one row is stable and against my baseline. Per `harness-shadow-mode-protocol.md` §4g, stability alone certifies nothing in either direction; here it is the baseline that looks wrong.
3. **`FIXTURE_MEANING|docs/seat-handover-2026-08-25.md|227@55de8fb`** (`A59a`) — the Jev verdict was `no_description`, UNSTABLE at 4/5. The line names *"Fixtures A59a–e"* in the row for the AutoTweaker weekday-only filter. Whether a feature row naming its fixture range counts as a description is a genuine border case. It is unstable, so untrusted.

⚠ **Two observations, not findings: one window of 14 items is `n=1` on an occasion (`harness-shadow-mode-protocol.md` §1).**
- **All 4 unstable rows sit at `mean_top_prob` 0.488–0.59. So do two stable rows (0.532, 0.474).** Probability does not separate stable from unstable here either, consistent with `harness-shadow-mode-protocol.md` §4d.
- **On the three `trend_gate` change-label lines, Q-TENSE split three ways.** `:61`, the prose *"Shipped: v66 … 18.0 → 23.0"*: history, stably. `:45`, the table row: 4 of 5 `asserts_current`, unstable. The pre-ship `18→~23` line: history, stably. This is the hardest case the spec names (`doc-scanner-check-spec.md` §0, trap 5). The detector handled the prose form cleanly and wobbled on the table-cell form. Across 14 items that is an anecdote, not a rate.

## Re-run

```
set -a; . ./typesafe.local.env; set +a
powershell -NoProfile -File tools/checks/doc-scanner.ps1 -Rev 55de8fb -Docs 'docs/seat-handover-2026-08-2*.md,docs/seat-handover-2026-08-10.md,docs/seat-handover-2026-08-12.md,docs/next-session-handover-2026-05-14.md,docs/next-session-handover-2026-05-18.md,docs/fable-continuation-2026-07-23.md' -BaselinePath docs/harness-runs/doc-scanner-20260923T135211Z-acceptance-baseline.json
```

The detector is not deterministic (`harness-shadow-mode-protocol.md` §4b). Expect the unstable rows to move.
