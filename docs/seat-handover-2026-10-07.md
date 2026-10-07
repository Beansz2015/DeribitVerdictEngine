# Seat handover — 2026-10-07 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol (now 8 steps: step 7 = the Jev harness triggers, step 8 = keep `docs/outstanding.json` current). **This is the STATE read.** Prior: [`seat-handover-2026-10-01.md`](seat-handover-2026-10-01.md), superseded for state. **The pane (`docs/outstanding.json`) mirrors this doc's open items** — check it, then check git.

⛔ **Run `date -u` first.** This seat ran 2026-10-01 16:30 → 2026-10-07 ~16:00 UTC. The workstation shows GMT+8.

⛔⛔ **The trader is away 2026-10-14 → 2026-11-25; last working day 2026-10-13.** No builds, deploys, scoring or settings changes during the holiday. **Remind the trader of the pre-holiday items below at seat start.**

⛔ **The dev machine is shut down daily by 03:00–04:00 GMT+8 (~19:00–20:00 UTC).** Any run over 6 h goes to a temporary AWS instance (the backfill runbook pattern).

**State at close:**
- `master` is ahead of `origin` by this handover's commits only (docs). Settings **v69**, tracked and on the box.
- **Collector:** instance `41a3e894…` since 2026-10-01 09:35 UTC (the watchdog's restart during a 57-min venue halt — no fault). Healthy at the 2026-10-05 fetch. Collector EC2 `i-0d6c133058876273e`.
- **No temporary AWS instance is running.** Both (`i-09345ccb43e175215`, `i-0b17cf2c2eb67496e`) are terminated.
- **Nothing runs on the dev machine.**
- **Dev history stores:** `C:\DeribitData\history\` (2025-01-01 → 2026-10-03; comparisons #1–#3 PASS, ledger `compare_ledger.csv`) and `C:\DeribitData\history-2023-2024\` (2023–2024, separate, deep clean). Raw-book probe output: `C:\DeribitData\rawbook-run1\out\`.

---

## 0. FIRST ACTIONS

| # | Action | Model + effort | Notes |
|---|---|---|---|
| 1 | `date -u` · `git status -sb` · read-back: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-readback.ps1 -InstanceId i-0d6c133058876273e` | seat, low | Check `WATCHDOG_TAIL` (`decision=OK`), `RUNERR_TAIL`, memory. Then read the pane (`docs/outstanding.json`) |
| 2 | **~2026-10-08: absorption Stage 1 read** | per its doc | ⚠ **New context it must carry:** the raw-book test found the 100 ms book fold **understates** `pullFrac` (H-NET confirmed; [`raw-book-absorption-test-results-2026-10-06.md`](raw-book-absorption-test-results-2026-10-06.md)). Any Stage 1 reading of the pull veto must say its input is biased low |
| 3 | **~2026-10-08: fetch** (`tools/ops/collector.ps1 fetch -InstanceId i-0d6c133058876273e`; its hook runs the history comparison) **+ re-count the sealed liquidation × OFI study**: `python tools/ops/a4_liq_ofi_counts.py --fetch aws_fetch/<new> --data-cut <day after the dev store's last settled day>` | seat, low | `verdict=FAIL` stops the plan. Log the re-count in the RULED box of [`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) §9. Re-run the box-store duplicate check only if asked |
| 4 | **~2026-10-09: ASIA and NY burst-watch reads** (`tools/ops/burst-watch-read.ps1`) | per their docs | Carried from the 10-01 handover |
| 5 | **2026-10-13: final read-back + fetch** | seat, low | Last look before 6 unattended weeks |
| 6 | **Adversarial review of [`indicator-calibration-review-2026-10-02.md`](indicator-calibration-review-2026-10-02.md)** — the trader ruled: a FRESH seat | Opus 5.5 or Fable 5.1, high | Still open. Its §8 questions, §9 carried claims |
| 7 | **Decisions `RBM-1`–`RBM-12`** (the raw-book measure spec's decision table, [`raw-book-absorption-measure-spec.md`](raw-book-absorption-measure-spec.md) §9) | Trader | Harness 6 flagged `RBM-6` (mismatch-record depth) and `RBM-7` (key storage) as economy picks. ⚠ `RBM-9`: the read-only-key scope record (`HH-2`, [`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) §4) still says "the collector box is still excluded" — the trader must update it before any key goes on the collector or the gate clones. Build is after 2026-11-25 |

### ⛔ Armed Jev harnesses — carry this table forward (`CLAUDE.md` Session Start step 7)

| Harness | Fire it when | This seat's use |
|---|---|---|
| 5 · doc re-ranker | Any "where was this decided/defined?" — before grepping | Trial sessions logged: **1** (2026-10-02, [`harness-runs/doc-reranker-trial-2026-10-02.md`](harness-runs/doc-reranker-trial-2026-10-02.md)); used again 2026-10-05 (raw-book brief: miss). **≥ 4 more sessions owed** |
| 6 · decision-bias tripwire | Every new recommendation with options; labels FIRST | Fired ~10 times this seat; it caught real economy picks (`RVF-3`, `DSR-4`, `TFS-6/8`, `RBM-6/7`) |
| 4 · doc scanner | Every handover | Run on this handover — result in §4 below |
| 2 · commit walker | Seat start with unseen commits | Not fired this seat |
| 3 · fixture parser | New fixtures passing a settings threshold literal | Fired on `A91`–`A94`: nothing to judge ([`harness-runs/fixture-parser-a91-a94-run-2026-10-02.md`](harness-runs/fixture-parser-a91-a94-run-2026-10-02.md)) |
| 1 · rider travel | An `analysis_log.csv` header rotation | None this seat |

---

## 1. What happened this seat

| Item | Result | Record |
|---|---|---|
| 21-month history backfill (2025-01 → 2026-09) | DONE, 71.77 M trades, comparisons #1–#3 PASS; `D-6` ruled (run dates, 14 days, closing comparison after the holiday) | [`history-store-backfill-runbook.md`](history-store-backfill-runbook.md) top box |
| Box-store duplicates and holes | Fully attributed; no new defect; `A79g` tail overlap live by design | [`box-store-duplicates-and-holes-read-2026-10-02.md`](box-store-duplicates-and-holes-read-2026-10-02.md) |
| 10-01 collector restart | Watchdog restart during a 57-min venue halt (no trades 09:02–10:00 UTC) | [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5 |
| `large_liq_size` re-derivation | p90 ≈ 60k USD vs shipped 200; `LLS-1` = (b) per-session, Rider 3 of the liquidation park build | [`large-liq-size-rederivation-2026-10-02.md`](large-liq-size-rederivation-2026-10-02.md) |
| Burst-outcome tools | Second review → fixes `RV-1`–`RV-3`, `RVF-1`–`RVF-3` built; nothing owed before run 1 | [`burst-outcome-read-rv-fixes-spec-back.md`](burst-outcome-read-rv-fixes-spec-back.md) |
| A4 liquidation × OFI study (logged era) | Unreadable (29 joined events); SEALED, re-counted at each refresh (`A4L-9` (b), `A4L-10` (a)+(e)) | [`a4-liq-ofi-logged-era-study-spec.md`](a4-liq-ofi-logged-era-study-spec.md) |
| Liquidation × trade-flow flip study | 2023–2024 backfill added; re-registered (FLIP 171); session 2: **NO DIFFERENCE SHOWN** on all three Holm tests; the fade loses after fees. **Licenses no engine change** | [`liq-tradeflow-flip-study-results-2026-10-06.md`](liq-tradeflow-flip-study-results-2026-10-06.md) |
| Raw order-book absorption test | 24 h probe on AWS; quarantined read (option (d)): **H-NET CONFIRMED** — the 100 ms fold understates `pullFrac`; ~19.5 % of passing episodes would veto on raw. Ruled: switch to the raw-equivalent measure + raw book subscription on the collector; `max_pull_frac` unchanged now. Spec written (`RBM-*` open) | [`raw-book-absorption-test-results-2026-10-06.md`](raw-book-absorption-test-results-2026-10-06.md); [`raw-book-absorption-measure-spec.md`](raw-book-absorption-measure-spec.md) |
| Doc state register | Reviewed; `DSR-1`–`DSR-8` ruled (`DSR-4` = (b) full backfill); P1 after 2026-11-25 | [`doc-state-register-proposal-review-2026-10-05.md`](doc-state-register-proposal-review-2026-10-05.md) |
| Outstanding-items pane | Mod built (separate seat); `docs/outstanding.json` committed; rule in `CLAUDE.md` step 8 and the GLOBAL `CLAUDE.md` | [`outstanding-json-seat-instructions.md`](outstanding-json-seat-instructions.md) |
| Jev harnesses | Trigger table added to `CLAUDE.md` step 7 | — |

## 2. After 2026-11-25 (in order; pane `next_up`)

1. First fetch → closing history comparison → the stage-4 pass rule (`D-6`).
2. Burst outcome run 1 (fresh cache, `--run 1`, ten STOP gates) by 12-04.
3. The 10-01 handover's post-holiday order (`seat-handover-2026-10-01.md` §3): measurement fixes, daylight-saving session hours (F5), audit scoring fixes, the liquidation park build **with Rider 3 (per-session `large_liq_size`)**, ADX/RSI re-anchor, vote-value study.
4. Raw-book absorption measure: the two-clone CPU gate, then the build (after `RBM-*` rulings).
5. Doc state register P1 (full backfill).

## 3. Lessons — saved to memory before this handover

- [[feedback-check-tree-before-dispatch]] · [[feedback-dont-commit-files-an-agent-is-editing]] · [[feedback-record-rulings-in-trader-words]] · [[user-dev-machine-daily-shutdown]] · the push-row count trap (in [[reference-outstanding-pane-mod]]) · "usage %" = the 5-hour window (in [[user-budget-constraints]]).

## 4. Harness 4 (doc scanner) on this handover

Run 2026-10-07 at `4ed4445` (`tools/checks/doc-scanner.ps1 -Docs docs/seat-handover-2026-10-07.md`; seat baseline written first):
- Code-only checks: 0 version, value or pointer candidates; 0 dated-state, missing-member or stale-family flags.
- 1 Jev item: the `A79g` fixture mention in §1 → `describes_this_fixture`, STABLE 5/5 (0.82), **agrees with the seat's label**. 5 Jev calls, 5,063 input tokens.
