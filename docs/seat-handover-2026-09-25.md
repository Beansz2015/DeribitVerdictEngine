# Seat handover — 2026-09-25 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-24b.md`](seat-handover-2026-09-24b.md), superseded for state. Its §2 (the probe on the box) and §3 (lessons) still bind. [`seat-handover-2026-09-24.md`](seat-handover-2026-09-24.md) §2 (the Jev rulings and armed harnesses) still binds.

⛔ **Run `date -u` first.** This seat ran 2026-09-24 13:19 → 2026-09-25 ~13:30 UTC. The workstation shows GMT+8.

⭐ **UPDATE 2026-09-25 13:32 UTC: the trader pushed and the RR-1 fix is DEPLOYED** — instance `a19acc4d-465f-446a-ad6f-479e0c6cb9a9` from 13:31:02 UTC, all read-back checks healthy (ledger row in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5). **§0 action 2 is DONE**; what remains of it is the live proof: at the next copy-back, no `trade_seq` range should be repaired twice under this instance. The "AHEAD of origin" and "c1855035" lines below are history.

**State at close (before the update above):**
- `master` is AHEAD of `origin` by this handover plus 9 commits (docs, tools, and the RR-1 engine fix `72f262e`). **The trader pushes.**
- Settings **v69** (tracked and on the box).
- Fixtures **486 PASS, `ALL PASS`**; the only SKIP is `A81b` (engine-fix B2's fixture). Orchestrator re-run.
- Collector: instance `c1855035-3e12-42e6-b736-924b3d0c9afc` since 2026-09-25 12:30:50 UTC, the Kelly v69 build. Healthy at 13:09 UTC.
- ⭐ **The liquidation probe runs on the collector box** (run 4, PID 11248). **0 liquidations flagged in 20 h.**
- No agent is running.

---

## 0. FIRST ACTIONS

| # | Action | Model + effort | Why |
|---|---|---|---|
| **1** | Probe liveness + collector health: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector-readback.ps1 -InstanceId i-0d6c133058876273e` | seat, low | Allow-listed for auto mode (trader-added 2026-09-25). Read-only. It prints the probe's status line too |
| **2** | **Deploy the RR-1 fix** (`72f262e`) — ⚠ **the trader asked for it 2026-09-25; it waited only on the push.** After the trader pushes: `dotnet build DeribitVerdictEngine.sln -c Release`, then `'y' \| powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector.ps1 deploy -InstanceId i-0d6c133058876273e`, then the read-back, then a deploy-ledger row in [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §5 | seat, medium | No settings change, no scoring, no rendered value. It changes what the repair pass writes (no phantom-hole re-fetches). Not a dataset boundary |
| **3** | Engine-fix **B2** (the liquidation flag) when the probe pairs a liquidation. Brief: [`engine-fix-sessions-a-c-b2-brief.md`](engine-fix-sessions-a-c-b2-brief.md) step 4 | Opus 5.5, high | ⚠ If the probe stays at 0 flagged for about 48 h more, question the instrument: does REST `get_last_trades_by_instrument` (the probe's endpoint) carry the flag the repair path's endpoint did? Unverified |
| **4** | ASIA burst watch read, plus the re-derivation read owed since 2026-09-14 ([`trader-tick-queue.md`](trader-tick-queue.md) §4) | Opus 5.5, medium | Watch read due from 2026-09-26 UTC |
| **5** | Absorption Stage 1 read ([`absorption-d2-s2-batch-summary.md`](absorption-d2-s2-batch-summary.md) §5 step 6) | Opus 5.5, high | About two weekday-weeks after 2026-09-24, so around 2026-10-08. Watch before then: flagged rate rising while the ratio shifts left means STOP |
| **6** | Venue-check sample review ([`venue-check-plan-review-2026-09-14.md`](venue-check-plan-review-2026-09-14.md) §5) | seat, medium | From 2026-10-05 UTC. ⚠ Its table has an ambiguity: the 2026-09-24 sample matched two rows (`missing_inside_seq_span > 0` AND `seq_contiguous = false`); that LOSS was repair lag, filled by the next pass |
| **7** | Retest the NY VPFR vote (`Q-2` in [`medium-tier-diagnosis-spec-back.md`](medium-tier-diagnosis-spec-back.md)), then the pre-registered vote-value study (add the `QD-3` NY STRONG fallback-target comparison) | Opus 5.5, high | After B2 ships and post-fix data accrues |

⚠ **Usage:** the account ran near its limit (93–96 %). The trader allows at most two agents; run at most one heavy agent at a time.

---

## 1. What happened this seat

| Item | Result | Record |
|---|---|---|
| Engine-fix A (POC gate) and C (lean column, Step 3b display) | Built and deployed 2026-09-24 18:46 UTC | [`engine-fix-session-a-spec-back.md`](engine-fix-session-a-spec-back.md), [`engine-fix-session-c-spec-back.md`](engine-fix-session-c-spec-back.md) |
| Absorption S1 + S2 (header 116 → 124, eight riders) | Built and deployed in the same deploy; all post-deploy checks passed; second fetch showed two rotated books | [`absorption-d2-s2-batch-summary.md`](absorption-d2-s2-batch-summary.md), deploy ledger |
| Harness 1 (rider-travel) first run | 8 of 8 agree (weak evidence) | [`harness-runs/rider-travel-first-run-2026-09-24.md`](harness-runs/rider-travel-first-run-2026-09-24.md) |
| `Q-1` (d) tier geometry | Top-tier success rise is a payoff shift; no tier outcome gap CONFIRMED | [`q1d-tier-geometry-read-2026-09-24.md`](q1d-tier-geometry-read-2026-09-24.md) |
| Kelly one-class + placed payoff (v69) | Ruled (`K-1` = (g), per-tercile measured p and b), built, deployed 2026-09-25 12:30 UTC. f* < 0 in all 9 session × bucket cells, so the block shows `[NO EDGE]` | [`kelly-one-class-placed-payoff-spec.md`](kelly-one-class-placed-payoff-spec.md) §4 rulings; [`kelly-one-class-placed-payoff-batch-summary.md`](kelly-one-class-placed-payoff-batch-summary.md) |
| Gap-repair `RR-1` (phantom holes) | Measured, specced, built (`72f262e`); **deploy pending the push** | [`gap-repair-repeat-fills-read-2026-09-25.md`](gap-repair-repeat-fills-read-2026-09-25.md), [`gap-repair-rr1-seq-order-spec.md`](gap-repair-rr1-seq-order-spec.md), [`gap-repair-rr1-spec-back.md`](gap-repair-rr1-spec-back.md) |
| Light tasks | v64 row archived; `ORDERCHECK_KELLY_BOOK` diagnostic; `D-10`/`TOOL-1` fixed; `CLAUDE.md` Kelly line corrected; `tools/ops/collector-readback.ps1` added | commits `0ba8d9b`, `03a0cbb`, `0988119`, `410deb6`, `7965ac2` |

**Trader rulings this seat** (all recorded in their docs): engine-fix `Q-1`/`Q-C1`/`Q-C2`; absorption S1 `Q-1`–`Q-3`, S2 `Q-1`–`Q-5`; `QD-1` (a)+(c), `QD-2` measured p; `KO-1`–`KO-5`, `K-1` (g), `K-2`–`K-4`; the probe runs on the collector box, guarded ("unless memory is predicted to almost run out"); RR-1 build and deploy.

**Queued, not yet ruled:** RR-1 spec-back `D-1`/`D-2` (the implementer corrected the spec's expected values for its OWN new fixtures `A91b`/`A91c`; orchestrator did not re-derive them) and `D-3` (escalation-trigger scope; orchestrator's read: correctly applied). Minor: the RR-1 code comment states the legacy-before-identified invariant too broadly; the true condition is narrower (no legacy row inside a seq-inverted pair's 1 ms).

---

## 2. The liquidation probe — on the collector box

Record: [`liquidation-probe-run-2026-09-21.md`](liquidation-probe-run-2026-09-21.md) (sections 00 and 000 for runs 3 and 4).

| Item | Value |
|---|---|
| Directory | `C:\probe-runs\liq-2026-09-24\` on `i-0d6c133058876273e` |
| Run 4 | from 2026-09-24 16:53 UTC, PID 11248, BelowNormal, 200 MB heap cap, built from `eff6def` |
| At 2026-09-25 13:08 UTC | up 1,216 min, 7,120 REST polls, 8 REST errors survived, index at its 40,000 cap, ~95 MB private; box commit free ~1,540 MB |
| Flagged / paired | **0 / 0** |
| Stop | SSM `New-Item C:\probe-runs\liq-2026-09-24\STOP` (the summary prints within 30 s) |

---

## 3. Lessons — each one cost something this seat

✅ **Saved as memory files before handover (2026-09-25), indexed in `MEMORY.md`:** liveness checks for long runs, `HttpClient.Timeout` as a cancellation, byte-sized caps for the collector, verify a doc's behaviour claim in code before relaying it, the allow-listed read-back script, the harness running on POCO defaults, and "save lessons before handover" itself (the trader's catch).

| Lesson | Where it bit |
|---|---|
| **A long-running process needs a liveness check at every seat start** | Probe run 1 died after 110 min; three handovers missed it |
| **`HttpClient.Timeout` is an `OperationCanceledException`** — a cancel handler swallows it | Probe run 3's REST arm ended silently on the slow box |
| **Size an in-memory cap in bytes against the target host** | `IndexCap` 400,000 would have been ~370 MB on a 1 GB box |
| **The fixture harness runs on POCO defaults (`Version = 1`), not the tracked `settings.json`** | A measurement mode must load the tracked file explicitly (`ORDERCHECK_KELLY_BOOK` refuses without it) |
| **A doc can describe a gate the code never had** | `CLAUDE.md` said Kelly was "suppressed when KellyF ≤ 0"; the code gated on `KellyPWin` since `9becdbc` (2026-04-20). Found only by reading the render gate |
| **Auto mode blocks self-modification of permissions and some production reads** | The trader added the allow rule for `collector-readback.ps1`; the seat cannot |

---

## 4. ⚠ What I did NOT verify

**Not checked at all:**

| Item | Why it matters |
|---|---|
| Why the probe has seen no liquidation in 20+ h (a quiet market, or an instrument gap such as the REST endpoint not carrying the flag) | It gates engine-fix B2 |
| **The RR-1 fix working live** — no `trade_seq` range repaired twice under instance `a19acc4d…` | Needs several 6-hourly passes; check at the next copy-back |
| The live Kelly card and snapshot on the collector | Only the implementer's LOCAL screen check exists; the box's rendered `[NO EDGE]` block was never seen |
| The gap between the live eval cache's p (NY 0.367) and the offline read's (0.404) | Kelly spec finding `F-3`; cause unknown. f* is negative on either, but the rendered number depends on it |
| Whether the order app shows or uses the payload's advisory `kelly` block | Another repo |
| The box's paging with NO probe running, at the same hour | Paging readings with the probe (300–1,275 `pagesOUT/s`) have no probe-free baseline to compare |
| The probe's raw-dump retention path (delete files beyond 8) | Never exercised: a rotation takes ~5 h and only 2 files existed |
| Whether the thread-leak re-entrancy gates hold under load | Carried from the 2026-09-24 handover, still open |

**Carried from agents without re-running** (I re-ran only the named ones):

| Claim | What I did check |
|---|---|
| RR-1: each new fixture fails on the old code and the wrong fixes; `A56c`/`A56d` unchanged; the corrected expected values of `A91b`/`A91c` (`D-1`/`D-2`) | Harness 486 PASS; read the comparator |
| RR-1 read: 5 holes, 2 recurring, 19 duplicate rows, 5 extra fetches | One inverted pair checked in the store (seq `300377528` at `…804` ms) |
| Kelly build: the 12 mutation runs, the local screen check, snapshot/card byte parity | Harness 483; the AC-4 table re-run via `ORDERCHECK_KELLY_BOOK` (identical); the render gate read in code |
| Absorption S1/S2: 16 mutation runs, gate output | Harness 468; the post-deploy read-back shows every new column populated |
| `Q-1` (d): every handle except `H-1`, incl. the 7-minute regeneration | `H-1` re-run, identical; the Kelly f* arithmetic |
| Engine-fix A and C beyond `H-3` and the gate edit | Harness; the two `NEAR_HVN_*` literals read in both copies |

**Now answered (was on this list):** the 3.6-min WebSocket connect after the v69 deploy did NOT recur — the RR-1 deploy connected in 1.4 s.
