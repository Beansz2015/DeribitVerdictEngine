# Seat handover — 2026-09-29 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.** Prior: [`seat-handover-2026-09-25.md`](seat-handover-2026-09-25.md), superseded for state.

⛔ **Run `date -u` first.** This seat ran 2026-09-25 13:41 → 2026-09-29 ~19:45 UTC. The workstation shows GMT+8.

⛔⛔ **The trader is away 2026-10-14 → 2026-11-25.** The plan of record is the TRADER AWAY block at the top of the [`trader-tick-queue.md`](trader-tick-queue.md) state banner. From 2026-10-01, remind the trader of its pre-holiday list at every seat start.

**State at close:**
- `master` is **21 commits ahead of `origin`**, all docs and tools; no engine code is unpushed. **The trader pushes.**
- Settings **v69** (tracked, and on the box).
- Collector: instance `05a543bd-e79d-41b0-84a3-444b93690bad` since 2026-09-28 16:19:21 UTC (the `D-2`/`D-2b` deploy). Healthy at the 2026-09-29 13:50 read-back.
- Fixtures **489 PASS, 1 SKIP (`A81b`)** at the `D-2b` build.
- **Nothing is running.** The collector-box liquidation probe is STOPPED (retired). The dev-machine raw-channel probe finished. The history-host flag watcher was stopped after 8 minutes at the trader's request (0 events).

---

## 0. FIRST ACTIONS

| # | Action | Model + effort | Notes |
|---|---|---|---|
| **1** | `date -u` · `git status -sb` · read-back `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-readback.ps1 -InstanceId i-0d6c133058876273e` | seat, low | Allow-listed. Check the repair tail for any `SEQLESS_AFTER_CUTOVER` line (none expected) |
| **2** | ⭐ **The trader has an adversarial-review package from a separate review orchestrator. Ask for it, then triage it.** | Opus 5.5, medium | See §2 below. It is **not** the four audits in `docs/audits/` |
| **3** | **Decide with the trader:** history store build before the holiday, or fix the review's bugs first (then the history store moves to after 2026-11-25) | Trader | My read: triage (#2) first, then decide. See §2 |
| **4** | **History-host flag test** — only when the trader says the dev machine can stay on for hours: `powershell -NoProfile -File tools/ops/liq-late-flag-watch.ps1 -Source history -Minutes 480` (log `C:\probe-runs\liqlag\watch.log`) | seat, low to launch; medium to read | Answers: is a liquidation trade flagged **at first sight** on `history.deribit.com`? See §3 |
| **5** | **`D-4` re-ruling** on the liquidation vote and cascade alarm, after #4 | Trader | Options in §3 |
| **6** | After #5: set the timing of the **raw order-book absorption test** brief (Opus 5.5, high) | Trader + orchestrator | Key scope widened for it (`HH-2`); row in `trader-tick-queue.md` §2 |

⚠ **Usage:** the account ran near its weekly limit this seat. Run at most one heavy agent at a time. On 2026-09-28/29 the auto-mode classifier failed ("no safety verdict") while two seats were active; one of this seat's agents was killed by it. Stop after 2–3 retries and do read-only work.

---

## 1. What happened this seat

| Item | Result | Record |
|---|---|---|
| Stale docs fixed; `D-1`/`D-3`/`D-2`/`D-2b` (RR-1 review) ruled | `D-2` + `D-2b` built (`2dd65fb`), reviewed, **deployed** 2026-09-28 16:19 UTC | [`gap-repair-rr1-d2-d2b-spec-back.md`](gap-repair-rr1-d2-d2b-spec-back.md) §5; deploy ledger |
| RR-1 live proof | ✅ 12 passes, 10 ranges, **0 repeated** | [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5 |
| ASIA burst watch (3rd read) + re-derivation | Fire rate is **volatility-conditional in all three sessions**; `AVR-1` (b), `AVR-2` (a), `AVR-3` (a) ruled; ATR-conditional watch built and reviewed | [`aggr-vel-burst-rederivation-read-2026-09-26.md`](aggr-vel-burst-rederivation-read-2026-09-26.md); [`avr1-atr-conditional-burst-watch-spec-back.md`](avr1-atr-conditional-burst-watch-spec-back.md) |
| Burst outcome read | Power count + pre-registered spec (`BO-D1` = run 1); **tools built and reviewed**; two pre-outcome fixes by the orchestrator (readability on covered strata; §5a Σ breakevens) | [`burst-outcome-read-spec.md`](burst-outcome-read-spec.md); [`burst-outcome-read-tools-decisions-for-orchestrator.md`](burst-outcome-read-tools-decisions-for-orchestrator.md) §5 |
| ⭐⭐ **History host** | `history.deribit.com` serves the full trade history to 2020 **with liquidation flags**; validated exact and complete | [`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) §5a |
| **History data store** | Spec with a staged plan (dev-machine store → parallel run → switch off box capture → retire); `HDS-1`–`HDS-4` ruled (`HDS-2`: all 21 months in one cloud backfill on a temporary instance); **drop list** for stage 4/5 | [`history-data-store-spec.md`](history-data-store-spec.md) §2, §2a, §2b, §6 |
| Liquidation flag | Main host: flag added **~60 min** after the trade (n = 1). Authenticated raw channel: **not flagged at first delivery** (0 of 3). Collector-box probe **retired** | [`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) §0000; [`raw-channel-liquidation-test-spec-back.md`](raw-channel-liquidation-test-spec-back.md) §7 |
| Fillers | `verify-gate.ps1` version-bump check now per commit (`e00075a`); venue report funding/candle "missing" explained (mis-scoped check); venue-check review, S4 fix and the probe record **dropped** | [`trader-tick-queue.md`](trader-tick-queue.md) |

---

## 2. The review package and the build-order decision

- **The package:** the trader has it now, from a separate review orchestrator. It was prepared for this project; earlier confusion came from review conversations run in the cloud rather than on this machine.
- ⚠ **Check which commit it audited.** The four audits in `docs/audits/` (2026-09-25) audited `6e74181` (2026-09-22). Several of their findings were already fixed or queued this week: the POC gate (fixed and deployed 2026-09-24), the liquidation-flag skip and the maker-side liquidation booking `L-2` (engine-fix B2, now superseded by `D-4`). **Dedupe the package against work since its audited commit before planning fixes.**
- **Triage buckets:** already fixed · known and queued · fixture-only (safe before the holiday) · engine or rendered-value change (reserved; a scoring change deployed just before 2026-10-14 is risky — land by ~2026-10-07 with read-backs, or after 2026-11-25).
- **The decision (item #3):** if triage finds a live defect that corrupts what the collector records during the six weeks away, fix it first. If it is mostly coverage gaps and display issues, build the history store before the holiday (the cloud backfill can then run while the trader is away, on a temporary instance that never touches the collector).

---

## 3. The liquidation question — where it stands

| Source | Flag at trade time? | Evidence |
|---|---|---|
| Public stream (100ms, agg2) | No | `L-1`; raw test |
| Authenticated raw channel | **No** (0 of 3) | raw test §7 |
| Main host REST | No — added ~60 min later (n = 1) | watcher, 2026-09-28 |
| **History host REST** | **UNKNOWN — the test is item #4** | It is live to ~1 s; only its first-sight flag state is untested |

**`D-4` options for the trader (after #4):**
- (a) Park the live vote and the cascade alarm, and say so in code (no real-time source).
- (b) Enrich from REST ~60–90 min late (useless for 1–3 min bars).
- (c) Leave as is (the vote silently never fires — the "docs rot, code survives" failure).
- **(d) If #4 shows first-sight flags: poll the history host every few seconds and feed the vote from it** (no key needed).

Reserved either way (scoring). Nothing here deploys before 2026-11-25 unless the trader decides otherwise.

**Loose ends on the dev machine:** env vars `DERIBIT_RO_CLIENT_ID` / `DERIBIT_RO_CLIENT_SECRET` (read-only key; delete the key at Deribit when the tests are done); scheduled task `rawliq-run1` (idle, can be removed).

---

## 4. The outstanding list

The full chronological list with gates is the TRADER AWAY block of the [`trader-tick-queue.md`](trader-tick-queue.md) state banner plus its §2 and §4. Summary:

**Before 2026-10-14:** review triage (#2) → build-order decision (#3) · history-host flag test (#4) → `D-4` (#5) → book test timing (#6) · `D-2b` read-backs · absorption Stage 1 read ~10-08 · ASIA and NY burst-watch reads ~10-09 (`tools/ops/burst-watch-read.ps1`, ATR-conditional reference; NY judged like ASIA) · final read-back + fetch ~10-13. Reminders from 10-01.

**2026-10-14 → 11-25:** read-only checks only.

**After 2026-11-25:** burst outcome **run 1** (data cut 2026-11-25, by 12-04) → `AVR-2` (c) decision · history store build → 21-month cloud backfill → `large_liq_size` re-derivation → parallel run ≥ 14 days → switch off box capture (reserved) → work the drop list ([`history-data-store-spec.md`](history-data-store-spec.md) §2a) → stage-5 retirement spec · the `D-4` build · NY VPFR retest · burst outcome **run 2** on or after 2027-01-27.

**Undated backlog:** `_evalCache` decoupling · `DeribitWsFeed` subscribe reply · eval-cache double-stores · `SwingFallbackRead` rotated-book rider (four modes) · tier-order forward test (parked) · fills-import tool.

---

## 5. Lessons — saved to memory before this handover

- [[reference-deribit-history-host]] — the history host and its paging rule; check for alternate endpoints before accepting a venue limit.
- [[feedback-latest-n-probe-blind-to-late-fields]] — a latest-N sampler cannot see a late-arriving field; judge after a delay.
- [[feedback-check-copied-stats-against-house-rules]] — copied stats code can break §5a; check every column before a pre-registered run.
- [[project-trader-away-2026-10-14]] and [[project-history-store-drop-list]] — written earlier this seat.

---

## 6. ⚠ What I did not verify

- The history host's rate limits and terms of use; completeness beyond the tested windows.
- The history host's flag state at first sight (item #4).
- Whether the 04:18 UTC 2026-09-29 repair pass wrote a `SEQLESS_AFTER_CUTOVER` line (outside the 3-line read-back tail; none expected).
- The absorption Stage 1 and burst-watch dates — carried from their docs.
- The review package's content — not seen by this seat.
