# Seat handover — 2026-09-21 (UTC)

**Read after** `CLAUDE.md`'s session-start protocol. **This is the STATE read.**

**Prior handover:** [`seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md), superseded for state. Its §5 lessons still bind, and its §1 task list is still the work queue — **none of it was touched today.**

**State at close (2026-09-21 18:56 UTC):**
- ⭐⭐ **The collector was DOWN for 3 d 13 h and is BACK.** Root cause was **WPAD proxy auto-detect**, not anything in the engine's logic.
- Settings **v68**, untouched all day. **No settings key changed.**
- ⛔ **`master` is 1 commit ahead of `origin`, unpushed** (`d7637ba`, the deploy ledger). **Push it.**
- Harness **425 PASS, `ALL PASS`**.
- ⭐ **The 2026-10-01 cross-month gap-repair deadline is DISCHARGED** — today's deploy carried the three gap-repair fixes. **The schedule pressure that shaped the last handover is gone.**
- ⛔ Run `git status -sb` and `date -u` anyway. Never inherit a push state. The workstation is GMT+8.

---

## 0. ⛔ FIRST ACTION — a running task to restart

**Nothing is running.** The deploy finished and its watcher was stopped at close.

⭐ **But two scheduled checks were AGREED and NEVER SET UP.** They are the only unfinished operational item:

| When (UTC) | Purpose |
|---|---|
| **2026-09-22 06:00** | Smoke. One Defender 02:00 scan survived |
| **2026-09-24 06:00** | Decision point. Three scans survived |

**Each reads:** thread count (healthy ~10-20; `collector.ps1 status` now shouts above 100), `analysis_log.csv` advancing, `pagesOUT/s`, and `ws_feed.log`.

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/ops/collector.ps1 status -InstanceId i-0d6c133058876273e
```

⚠ **Set these up on the trader's greenlight.** At close, UTC was 2026-09-21 18:56, so the 09-22 check was ~11 hours out and the 02:00 scan had not yet happened.

⛔⛔ **WHAT A GREEN CHECK DOES AND DOES NOT PROVE — do not overclaim it.** With the proxy fixed the feed no longer hangs on connect, so the 02:00 scan may produce **no stall at all** to test the re-entrancy gates against. **A clean 09-24 shows the box is healthy. It does NOT prove the gates work under load.** That distinction is exactly what decides whether the UI-thread liveness heartbeat (§4) is still worth building.

---

## 1. What actually happened — the two-fault day

⭐ **There were TWO independent faults, and conflating them cost most of the day.**

| Fault | What it was | Status |
|---|---|---|
| **A. WPAD proxy auto-detect** | Machine-wide on the box (`DefaultConnectionSettings` bit `0x08`). Every HTTP and WebSocket client in the tree was at the default, so every connect hung in proxy RESOLUTION — **before a socket is opened**. ⛔ `HttpClient.Timeout` does NOT bound this: the request timeout only starts once the request is issued | ✅ **FIXED** `584c616`, deployed 15:38 |
| **B. The thread leak** | Three `Threading.Timer` callbacks marshalled with a **blocking `Control.Invoke`** and no re-entrancy guard. Any UI-thread stall parked a thread-pool thread per tick while the pool injected ~1/second | ✅ **FIXED** `4da9487` + `0b2bf88`, deployed 15:38 |

**B is what turned A into a box-down rather than a quiet stall.** Same mechanism on 2026-09-18 (trigger: Defender's 02:00 scan) and 2026-09-21 (trigger: the proxy hang). 10 threads → 11,630 in four hours on 09-18; the box died.

### The evidence that settled it, and it is clean

The deploy acceptance gate is **unchanged** across all three attempts on 2026-09-21:

| Attempt | Build | Result |
|---|---|---|
| 14:31 | new, **no** proxy fix | `rowsAfterRestart=0` for 28 polls |
| 14:45 | old (automatic rollback) | `rowsAfterRestart=0` for 25 polls |
| **15:38** | new, **with** proxy fix | **`0→1→2→3`, span 81 s, ACCEPTED** |

⭐⭐ **Same gate, same box, same network. The only variable was `UseProxy`.** And instance `add6c551…` — the new build *with* the leak fixes and *without* the proxy fix — produced zero rows exactly like the old build, which is what proved the zero-row symptom was never caused by the leak fixes.

### Confirmed healthy at +3 h (co-tenant seat, 18:31 UTC)

- **13 threads at 175 minutes**, private memory **falling** 106.1 → 90.9 → 87.9 MB.
- **Paging ZERO across 25 samples**, at their hour-18 baseline.
- `ws_health.log` unchanged since `OK` at 15:39:49 — **the first `OK` since 2026-09-01**.

---

## 2. Commits today (all pushed except the last)

| Commit | What |
|---|---|
| `a6c205d` | ⛔ **`BacktestRunner fetch` could not fetch trades at all** — VB is case-insensitive, so the parameter `storeDir` and the class const `StoreDir` are the SAME identifier; the fallback returned `Nothing`. Live path unaffected (it passes `storeDir` explicitly), which is why no fixture caught it |
| `4da9487` | Re-entrancy gates + `BeginInvoke` on all three timer marshals |
| `0b2bf88` | `GetRowCount` streams instead of `File.ReadAllLines` on a 13.5 MB book **on the UI thread** |
| `fc5ba97` | **A84** pins `CountDataRows` line semantics. Mutation-proven |
| `92191a3` | **`ws_feed.log`** — the feed's own narrative, durably. Fixtures **A85** |
| `3c6137d` | Ledger: the zero-row instance, `3fe57c53`'s corrected end, **and the hole in the reconciliation check** |
| `1018303` | App-health probe reports **thread count**, handles, process start |
| `584c616` | ⭐⭐ **The WPAD fix.** `UseProxy=False` on both `HttpClient`s, `Options.Proxy = Nothing` on both `ClientWebSocket`s |
| ⛔ `d7637ba` | **UNPUSHED.** Ledger: the recovery deploy + five collapsed zero-row instances |

---

## 3. ⭐ Tape recovered, and what is gone forever

**197,463 trades rescued**, 2026-09-20 13:38:35 → 2026-09-21 13:41:36 UTC, into the dev-machine `backtest_data/`. Readers dedupe identity-first, so it merges cleanly.

⛔ **2026-09-18 02:05 → 2026-09-20 13:38, about 59.5 hours of tape, is PERMANENTLY GONE.** Deribit's public trades endpoint serves ~24 h and refuses older windows.

⛔ **Three days of `analysis_log.csv` verdict rows are gone with no recovery path at all** — scoring rows only exist if the engine ran. That is a hole in the Kelly population and every tier read, and it lands immediately before the absorption S2 rotation.

⚠ **`trade_store.gap_repair_lookback_hours` is 20; the venue serves ~24.** A one-off wider manual repair after any future outage recovers up to 4 more hours. **Not done today.**

---

## 4. Owed, in priority order

| # | Item | Notes |
|---|---|---|
| **1** | **Push `d7637ba`** | One command |
| **2** | **The two scheduled checks** (§0) | On the trader's greenlight |
| **3** | ⭐ **The engine-fix build** — [`engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) | **Written today and UNTOUCHED.** Sessions A/C/B2 one seat, B1 (probe) its own. All of `EF-1`–`EF-4` RULED. **This is the real queue** |
| **4** | **Absorption S1/S2** — [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) | ⭐ **No longer deadline-pressured, and that changes MORE than the date. Read §4a — do not just move it down the list** |
| **5** | The UI-thread liveness heartbeat | **Gated on the 09-24 check.** Read §0's caveat before deciding |
| **6** | A fixture for the timer gates | ⚠ **Could not be built cheaply** — it needs a real `Control` and a running message pump, which the harness does not have. Said rather than implied |
| **7** | `collector.ps1` cannot deploy to a box whose app is DOWN | It resolves the install dir from the running process. Needs a `-RemoteDir` override. ⛔ **I deliberately did NOT patch this mid-incident** — its safety properties are what saved us |
| **8** | `Q-1` option (d), the postponed reads | From the prior handover. Untouched |

### 4a. ⭐ What discharging the 2026-10-01 deadline does to ABSORPTION

⛔ **Absorption never had a deadline of its own. It had one because it was the CARRIER for the gap-repair deploy.** That deploy happened today, on its own, so the carrier role is spent.

**Its deploy had FOUR preconditions** ([`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md), the `deploy` row of its session-split table). **Three fell this afternoon:**

| Precondition | Status |
|---|---|
| Rider 2b — both ops scripts must see a SECOND rotated book (`T-7`) | ⚠ **STILL OPEN — and now the ONLY gate** |
| Same-millisecond gap-repair fix (`GR-5` (b)) | ✅ shipped 2026-09-21 |
| Cross-month leading-gap fix (`F-1`) | ✅ shipped 2026-09-21 |
| Repair scan-failure fix (`DUP-1` / `DUP-2`) | ✅ shipped 2026-09-21 |

⛔ **The 2026-09-28 fallback trigger in [`seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md) §1 is DEAD.** It existed only to force a separate gap-repair deploy if absorption slipped. It cannot fire and must not be inherited.

**What follows from that:**

1. **The natural task order is restored.** The deadline was the only force that could push absorption ahead of the engine-fix build. [`seat-handover-2026-09-17.md`](seat-handover-2026-09-17.md) §1 already ordered engine fixes first; **that ordering now stands unopposed.**
2. ⭐ **`EF-1` is UNTOUCHED and still binds.** It ruled the POC-gate fix rides the absorption S2 deploy. Neither is built, so nothing changed — and its mechanism survives: **`RIDER-9` puts `VPFRSignal` and `VPFRPoc` into the header AT the S2 rotation, which is what makes the POC gate auditable per row.** S2 should still land with or before the POC-gate deploy.
3. ⛔⛔ **EIGHT riders still wait on S2, and that is the pressure that remains.** `RIDER-1`–`RIDER-7` plus `RIDER-9`, all `TRAVELLING` in [`csv-rotation-riders.md`](csv-rotation-riders.md). **`RIDER-7` was already LOST once**, on the 2026-09-01 rotation, because nothing checked the list. ⚠ **"No deadline" must not become "no urgency" — that is precisely how these riders accumulated.** Standing memory: defer a schema fix and it accretes.
4. **`T-7` is now the whole gate**, where it used to be one of four. Price it properly rather than treating it as a footnote: it blocks a rotation eight riders are queued behind.
5. ⛔ **A NEW complication from 2026-09-21 that NOBODY has assessed.** The book now carries a **three-day hole** — rows to 2026-09-18 02:00:07, nothing, then rows from 2026-09-21 15:38. Two consequences:
   - **The rotation folds that hole into the `.bak`**, which makes `RIDER-1` (derived `.bak` name) and `RIDER-2` (pooled reads must include `.bak`) more load-bearing, not less — the pre-rotation book is now discontinuous.
   - ⚠ **The absorption episode-age read may have lost population.** Its 10-weekday data gate passed 2026-09-16, **before** the hole, so the gate is probably still met — but the row count behind it is smaller than whoever passed it assumed. **CHECK THIS BEFORE RUNNING THAT READ, not after.** Not verified by this seat.

---

## 5. ⭐ Lessons from this seat

| # | Lesson | Instance |
|---|---|---|
| 1 | ⛔⛔ **A hang before the request is issued is not bounded by the request timeout.** `HttpClient.Timeout` and `ClientWebSocket` both resolve the system proxy FIRST. A process hung there **opens no socket at all** — so "zero TCP connections" is a *signature*, not just an absence | The whole outage |
| 2 | ⛔ **VB is case-insensitive: a parameter `storeDir` shadows a const `StoreDir` in the same body.** The fallback `If(IsNullOrWhiteSpace(storeDir), StoreDir, storeDir)` silently returns `Nothing`. `Core/TradeStoreWriter.vb` escaped it only by naming its const `DefaultStoreDir` | `a6c205d` |
| 3 | ⭐⭐ **A pile-up needs only "slower than the timer", not a permanent block.** `GetRowCount`'s `ReadAllLines` on 13.5 MB pushed the UI thread past 1 s; the 1 Hz timers did the rest. I hunted a deadlock for hours; there wasn't one | `0b2bf88` |
| 4 | ⛔ **I diagnosed a network outage as an engine defect, then as an AWS infrastructure failure, and was wrong both times.** I sent the co-tenant to check NAT gateways while **holding evidence against it** — their PMTU probe showed ICMP replying at 1500, and a dead NAT kills ICMP too. **Reconcile the evidence you already have before proposing a new cause** | §1 |
| 5 | ⚠ **Do not hand PowerShell to a bash Run block.** Bash expands `$var` to empty first. Three scripting errors in a row cost three round trips on a degrading box. **Write a script file** | The `grab-feedlog` arc |
| 6 | ⭐ **An instrument that does not exist cannot be consulted.** `ws_feed.log` was built at ~10:00 and would have named the proxy hang on the first attempt — but it was not on the box until 15:38. **The gap between building an instrument and deploying it is time spent guessing** | §2 |
| 7 | ⛔ **Do not patch the safety tool under time pressure.** `collector.ps1`'s refusals and automatic rollback are what made three deploy attempts safe. Rewriting it to save one round trip was the wrong trade | Owed item 7 |
| 8 | ⭐ **The co-tenant seat is a real instrument, and it self-corrects.** It found WPAD, retracted its own "we were healthy" claim, and flagged two of its own instrument errors unprompted. **Read its corrections as data, not noise** | Throughout |

---

## 6. ⚠ What I did NOT verify

- **That `ClientWebSocketOptions.Proxy = Nothing` means "no proxy" under .NET 8** rather than "use the default". It is flagged in the code comment. **The feed connecting is the only evidence, and it is circumstantial** — the `HttpClient` fix alone could account for it.
- **Whether WPAD also explains Defender's stale signatures** (2026-09-17). `MsMpEng` uses WinHTTP and takes the same path. Relayed to the co-tenant as a suggestion; not tested.
- **Why WPAD started biting on 2026-09-18** and not earlier. Nobody has established what changed.
- **Every reading from the co-tenant seat** — thread counts, memory, paging, their A/B test. All carried.
- **The re-entrancy gates under an actual stall.** No fixture, and the live box has not stalled since.
- **Collector health beyond 18:56 UTC.** That is what the scheduled checks are for.
