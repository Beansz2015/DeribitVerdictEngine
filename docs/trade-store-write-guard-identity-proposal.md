# Trade-store write guard — key it on IDENTITY, not on a millisecond

**Status:** ✅ **BUILT 2026-08-11.** §1's gate PASSED (all three answers green, §1a) · the §4 D-table is **RULED — all seven as recommended, trader-directed 2026-08-11** · code, fixtures A55a–g and the §5 mutation are done. **See §9 for the build record.** ⚠ **NOT YET DEPLOYED — AWS keeps dropping ~half the tape until it gets the new binary (§6).**
**Fixes:** [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md) — the streaming capture path discards ~50 % of trades at write time.
**Depends on:** trade identity (`trade_id` + `trade_seq`), shipped 2026-08-08, deployed to AWS 2026-08-10.

---

## 0. Model + effort — read this before anything else

> ### **Model: Opus. Effort: high.**

**Why this tier, and it is not the size of the diff.** The change is perhaps forty lines. The difficulty is that **the thing being fixed is a guard whose failure mode looks exactly like correct behaviour** — it ran for ten days in a path covered by eight fixtures (A48a–h), one of them named *"monotonic guard"*, and none of them noticed. It sits in a **never-throws** write path where a wrong fix is silent in both directions: too strict and it keeps dropping real trades, too loose and it re-admits every reconnect duplicate the guard exists to stop. Neither shows up as an error.

**Where an implementer will specifically slip. Four traps, all of them plausible-looking:**

1. ⚠ **Keeping `_lastTs` "as a cheap pre-filter."** This is the most likely mistake because it reads as an optimisation. It **reinstates the defect exactly** — same-millisecond siblings still fail the pre-filter and never reach the new check. `_lastTs` must be **removed entirely**, not demoted.
2. ⚠ **Keying on an absent or empty identity.** An identity-less row keyed on `""` collapses every identity-less row into one — the original defect reproduced at greater scale inside its own fix. The read path already resolved this (`DedupTrades`, §3.4 of the identity spec); **the write path must use the same relation, not a second one.**
3. ⚠ **Writing fixture trades ≥1 ms apart.** This is precisely how A48b and A48d missed the bug: their trades are built `A48Ms(i * 1000L)`, one second apart, so no same-millisecond case is ever presented. **A fixture that does not put two distinct trades on the same millisecond does not test this.**
4. ⚠ **Reaching for `trade_seq` as a high-water mark.** It looks like the obvious fix — monotonic, O(1), same shape as the current code. See **D-2**: the identity spec explicitly records that *whether Deribit ever resets `trade_seq` was NOT verified*, and a high-water mark on an unverified monotonic is the same class of guard that caused this.

**The fixtures cannot be relied on to catch traps 1–3 by themselves, because the implementer writes the fixtures too** — a misunderstanding propagates into its own test. That is why §5 requires **mutation**, not passing tests.

> ### ⚠ Escalation trigger
> **If the new fixture cannot be made to FAIL against the current, unfixed code, STOP and escalate.** A green fixture on unfixed code is not testing this defect, and everything downstream of it is worthless.

**Session split: none needed.** One session, provided §1's gate has already passed. If the gate has *not* run, do not open a build session at all.

**Fixture family: A55.** ⚠ **A54 is reserved** for the queued JSON↔POCO drift guard (`trader-tick-queue.md` §2). Do not take A54.

---

## 1. ⚠ THE VERIFICATION GATE — run first, alone, before writing any code

**One question decides whether this spec is the right fix at all:**

> **Does Deribit's `trades.BTC-PERPETUAL.100ms` channel actually DELIVER several trades sharing a millisecond timestamp?**

Stored data cannot answer it. The guard demonstrably *would* discard such trades if they arrived, and every missing row sits at an already-seen timestamp (7,525 disputed timestamps, **zero** new moments) — which is the guard's exact signature. But "the feed delivered them" is **inferred from that pattern, not observed.**

**The instrument is built and committed: `tools/WsTradeProbe/`.** Run it on the **AWS box** — that is the capturing box, and measuring a different machine's feed is a proxy for the real question. Run **during an active session**; a quiet hour produces few multi-leg sweeps and could read `max = 1` for the wrong reason.

```bash
dotnet WsTradeProbe.dll 600
```

**The fork, and it is not symmetric:**

| Gate result | Meaning | Action |
|---|---|---|
| **G1 max trades-per-timestamp > 1** | The feed delivers siblings; the guard is discarding real trades | ✅ **This spec is correct. Proceed.** |
| **G1 max = 1** | The feed never sends siblings | ⛔ **STOP. This spec is wrong.** The guard is innocent, the trades never arrived, and the capture strategy itself must be re-opened. Do not build |
| **G2 delivered `trade_seq` contiguous** | The feed is complete | ✅ The guard is the **sole** cause. This spec is sufficient |
| **G2 delivered `trade_seq` has gaps** | ⚠ **The feed is itself incomplete** | ⚠ **Proceed, but this spec is necessary and NOT sufficient.** Report the gap rate; a second workstream is owed. **This is the finding that would change the plan most, so do not skim G2 because G1 looked good** |
| **G3 simulated drop ≈ 50 %** | Matches both stored-data instruments | ✅ Three independent routes agree — the acceptance bar |
| **G3 far from ~50 %** | Something in the model is wrong | ⚠ **Stop and reconcile before building.** Do not proceed on two of three |

### ✅ 1a. THE GATE HAS RUN — 2026-08-11, on the AWS box. **ALL THREE ANSWERS GREEN.**

**Run:** `dotnet WsTradeProbe.dll 600` on AWS, **14:37:50 → 14:47:37 UTC (NY session)**. Raw capture: `ws_trade_probe_20260811-144742.csv`, 313 rows. **Every figure below was re-derived independently from that file, not taken from the console summary.**

| Gate | Result | Verdict |
|---|---|---|
| **G1** | 313 trades over **159 distinct timestamps**; **max 24 trades on one millisecond**; 70 timestamps carried more than one | ✅ **The feed DOES deliver siblings.** The spec is the correct fix — **proceed** |
| **G2** | seq **296083662 … 296083974** · span **313** · delivered **313** · distinct **313** · **MISSING 0, gap runs 0, duplicates 0** | ✅ **The feed is COMPLETE.** ⚠ **Stronger than "proceed": the guard is the SOLE cause of store loss, so this spec is SUFFICIENT and no second workstream is owed** |
| **G3** | 313 delivered, **159 accepted, 154 DROPPED — 49.2 %** | ✅ Matches both stored-data instruments. **Three independent routes agree** |

**The acceptance bar is met.** Three routes, three methods, no shared assumptions:

| Route | Measured loss |
|---|---|
| 20 h window, streamed vs REST repair (stored data) | **50.1 %** |
| `trade_seq` gaps on 431 identified store rows | **47.2 %** |
| G3 live guard simulation off the wire | **49.2 %** |

⚠ **The mechanism is now observed, not inferred.** **69 of the 70 multi-trade timestamps arrive inside a SINGLE notification batch** — which is exactly where `_lastTs` advances on `Buffer` rather than on flush, so the 2nd…Nth siblings are rejected before the buffer ever sees them. And **`accepted` (159) equals `distinct timestamps` (159) precisely**: one trade per millisecond survives, which is the guard's signature with no interpretation required.

**The worked example, and it is the whole argument in one row.** The 24-trade millisecond is a **single aggressive sell sweeping the book from 63904.00 to 63875.00**, `trade_seq` 296083705–296083728 unbroken, **239,990 USD of notional**. The guard keeps the **first leg — 50,000 @ 63904.00 — and discards the other 23.** ⚠ **189,990 USD, 79 % of that sweep, never reaches disk.** This is §5's bias made concrete: the loss falls hardest on exactly the aggressive multi-leg prints that CVD, TFI and aggressor velocity exist to measure.

**Three caveats, stated because the numbers are cleaner than the sample:**

1. **313 trades over ~10 minutes ≈ 31/min.** Small in absolute terms — but it agrees with the 34/min density independently measured from the REST repair block, so it is not anomalous.
2. ⚠ **Deribit had just returned from maintenance, so volume may be depressed — and that biases the result DOWN, not up.** Fewer sweeps means fewer siblings means a lower drop rate. **49.2 % is a conservative floor.**
3. **One session (NY).** The 20 h stored-data route spanned NY, ASIA and LONDON and gave ~50 %, so the figure holds across sessions.

⚠ **A testable prediction for after the fix, worth writing down now:** since G2 shows the feed loses nothing, fixing the guard should take store completeness to **~100 %**, not to some partial improvement. If a post-fix `trade_seq` check still shows gaps, something else is wrong and this spec did not find it.

**Also settled by this run:** the seam audit's **S-1** (the probe's private readers diverge from `TradeRecord.ReadTradeId`/`ReadTradeSeq` on `trade_seq ≤ 0`) **had zero exposure here — the capture contains no `trade_seq ≤ 0`.** S-1 remains a real instance of the defect class and is queued, but **it did not affect this result.**

*Historical note: the gate was blocked earlier on 2026-08-11 by a venue-wide outage (`system_maintenance`, code 11051, HTTP 503 on every endpoint, REST and WS alike). That was the outage, not a finding.*

---

## 2. The defect, in brief

`Core/TradeStoreWriter.vb:149`:

```vb
If t.Timestamp <= _lastTs Then Return False
```

**A millisecond timestamp used as an identity.** It is not one — Deribit reports a market order sweeping several price levels as several records at the same millisecond. The writer keeps the first leg and silently discards the rest.

Measured two ways, agreeing: a 20 h window captured twice by the same box shows **20,477 streamed rows against 41,013** from the REST repair, with repair ⊇ stream at **20,443 of 20,443 timestamps and zero reverse**; and `trade_seq` on the 431 identified rows shows **385 of 816 sequence numbers missing (47.2 %)**. Full evidence, including two rival explanations that were tested and rejected, is in the finding doc.

**The guard is not wrong to exist.** `SeedAsync` re-seeds on every reconnect and the WS may replay on re-subscribe, so duplicates genuinely arrive. **Only its key is wrong.**

---

## 3. The design

### 3.1 The principle that should drive every decision below

⚠ **The costs are asymmetric, so the guard must be asymmetric.**

- **A duplicate on disk is harmless.** The read path dedups it — A48d already proves this, and deliberately writes duplicates to disk (`raw = 10` for five trades) asserting only that a read collapses them to five.
- **A dropped trade is unrecoverable** past Deribit's ~24 h retention.

**Therefore: when uncertain, ADMIT.** The current guard has exactly the opposite bias, and that is the whole bug. Every ambiguous case in this build resolves toward writing the row.

### 3.2 Mechanism

Replace the timestamp high-water mark with a **bounded recent-window membership test, using the relation the read path already defines**.

```vb
Public Function Buffer(t As TradeRecord) As Boolean
    SyncLock _pending
        EnsureSeeded(t)                       ' now populates the window from the file tail
        If AlreadyCommitted(t) Then Return False
        _pending.Add(t)
        Remember(t)                           ' advance on BUFFER, not on flush — see 3.4
        Return True
    End SyncLock
End Function
```

`AlreadyCommitted` follows `TradeStoreWriter.DedupTrades`'s contract exactly — **one relation, two call sites, no copies**:

- If `t` carries an identity **and** that identity is in the window ⇒ duplicate.
- If `t` carries **no** identity ⇒ fall back to whole-row equality on the five legacy fields against the window.
- ⚠ **Never key on an absent or empty identity.**

`_lastTs` is **deleted**. Not demoted, not kept as a pre-filter — see §0 trap 1.

### 3.3 Why not `trade_seq` as a high-water mark

It is the tempting answer: monotonic, O(1), no memory, and it drops into the existing shape unchanged. **Reject it**, for a reason this project has already written down:

> *"a guard that checks EXISTENCE or a FIXED TOLERANCE rather than COMPLETENESS turns one bad fetch into permanent silent loss"*

A high-water mark is a **tolerance**. Set membership tests **the actual thing**. And the identity spec records plainly that **whether Deribit ever resets `trade_seq` was never verified** — a reset, a wrap, or one out-of-order batch would silently drop everything behind the mark, which is the exact failure class being fixed. Carried to D-2 as a real alternative rather than dismissed here.

### 3.4 Two behaviours that must NOT change

1. **The window advances on `Buffer`, not on `Flush`.** The current comment explains why and it still holds: *"a batch arriving before the flush timer fires would otherwise re-admit its own duplicates."*
2. **Never throws.** A disk or state error logs and drops the batch; capture must never kill the feed or a run.

### 3.5 What this fix makes moot — do not spec it separately

§5a of the finding doc records that **gap repair cannot heal in-flight loss**, because `ResolveResumeCursorMs` starts the fetch at the file's last written row and everything dropped sits behind that cursor.

⚠ **That is a consequence of this defect, not an independent bug.** Once streaming is complete, repair only ever needs to cover downtime — a hole at the tail — which its cursor handles correctly. **No second fix is owed.**

**One residual is NOT fixed here and is named so it is not lost:** `LastTradeTimestamp` reads the file's **last line**, not its maximum timestamp. The store already contains one out-of-order block, so that invariant is already violated once. It is harmless today. **Out of scope; record it, do not fix it in this build.**

---

## 4. D-table — ✅ RULED 2026-08-11, trader-directed

> **All seven rows ruled AS RECOMMENDED. No option was overruled and no row was deferred.** The
> recommendation column below is therefore the decision text; it is left as written rather than
> reworded, so the reasoning that was ruled on is still legible.

| # | Decision | Options | Ruling — as recommended |
|---|---|---|---|
| **D-1** | The guard's key | (a) identity set, read-path relation · (b) `trade_seq` high-water · (c) composite | **(a).** §3.1's asymmetry and §3.3's tolerance-vs-existence argument both point here, and (a) reuses a relation that already exists and is already fixture-covered |
| **D-2** | Ratify rejecting `trade_seq` as a high-water mark | ratify / overrule | **Ratify.** It is the obvious fix and it rests on an unverified monotonic. If overruled, the build must first verify that Deribit never resets `trade_seq` and that batches never arrive out of sequence order — both are gate work, not build work |
| **D-3** | Window size | (a) 20,000 entries · (b) smaller · (c) settings key | **(a) 20,000, as a CONSTANT.** ≈4 h of tape at the measured true rate, ~1 MB. Not a settings key: it has no failure-rate linkage, nobody will ever tune it, and a key means a version bump and a boundary question for nothing. If it must be configurable it goes under `trade_store.`, already fenced by HC27 |
| **D-4** | Restart behaviour | (a) seed the window from the file tail · (b) start empty | **(a).** Cheap (`ReadTradeFile` exists) and it stops a restart re-writing the tail. ⚠ **Either way the failure mode must be duplicates, never drops** — if the window cannot be seeded, admit and let the read path dedup |
| **D-5** | Existing half-tape | (a) leave it · (b) attempt recovery | **(a) leave it.** Past ~24 h retention nothing recovers it. ⚠ **Do not attempt a wide historical backfill to "repair" it** — it would write REST rows over a window the store already half-holds, and with no identity on the old rows the result is uninterpretable, which is the 2026-08-08 lesson exactly |
| **D-6** | Settings / version | bump or not | **No settings key, no version bump — stays v66.** Code-only. ⚠ The verify-gate's version-bump guard may still WARN on an engine-path change; that warning is expected and non-blocking, same as the C1 sessions |
| **D-7** | Measure §5b before shipping? | yes / no | **Take the measurement — but it does NOT gate the ship.** ⚠ **CORRECTED 2026-08-11: an earlier draft of this row claimed shipping first would destroy the experiment. That was wrong.** The repaired band is written to disk and bounded by an outage window recoverable from `ws_health.log`, and the streaming bands either side are **pre-fix and therefore still ~50 %** — so the contrast survives on disk permanently and the fix does not rewrite history. **The only real deadline is upstream of the measurement:** gap repair has to *run* after the venue returns, within its 20 h lookback and Deribit's ~24 h retention. If the collector is down or stopped across the return, the hole is never filled and there is no band to measure. **So: keep the collector up when Deribit comes back. The analysis itself can wait for any later copy-back** |

---

## 5. Fixtures — family A55, and the teeth must be PROVEN

⚠ **Mutation is mandatory, not optional.** For each fixture below, revert the guard to the shipped `<=` and confirm the fixture **fails**. A fixture that passes on unfixed code is not testing this defect. State the mutation result in the spec-back.

| # | Fixture | Property |
|---|---|---|
| **A55a** | ⭐ Two trades, **same millisecond**, different `trade_id` ⇒ **both survive** | The bug itself. ⚠ **A53e already holds suitable REAL Deribit data** — `439922656`/`439922657`, same ms, price, amount and direction. Reuse it; it was aimed at the dedup contract and never at the write guard |
| **A55b** | Reconnect re-seed replays an identical batch ⇒ written **once** | A48b's property must not regress. This is the guard's reason to exist |
| **A55c** | Mixed batch — some rows identified, some not ⇒ neither duplicates nor drops | The §3.4 fallback arm |
| **A55d** | Ten identity-less rows differing **only in amount** ⇒ all ten survive | The empty-identity collapse (A53c's property) at the **write** path this time |
| **A55e** | A duplicate **older than the window** ⇒ **admitted**, not dropped | ⚠ Pins the §3.1 bias. If this fixture asserts the opposite, the build has the bias backwards |
| **A55f** | Restart with a populated file ⇒ a re-delivered trade already on disk is rejected | D-4 |
| **A55g** | Unwritable store ⇒ never throws, fold keeps running | A48e's property must not regress |

---

## 6. Acceptance

- All six projects build **0/0 Release**: solution · AutoTweaker · WhatIfRunner · CeilingAudit · BacktestRunner · OrderCheck.
  ⚠ **`Core/TradeStoreWriter.vb` is NOT linked by AutoTweaker / WhatIfRunner / CeilingAudit** — that trap was found and fixed during the identity build and *"the writer links everywhere" is false*. Only a Release build of all six catches it.
- Harness **ALL PASS**, A1–A53h unregressed plus A55a–g.
- **Mutation results stated** for every A55 fixture (§5).
- `tools/checks/verify-gate.ps1 prepush` **GATE PASSED**.
- **Display-string parity: NO OBLIGATION**, stated explicitly per the hard rule — the trade store has no rendered surface at all: no snapshot line, no card binding, no CSV column, no bridge field.
- **Deploy is part of this change**, not a follow-up. AWS keeps dropping ~half the tape until it gets the new binary, and that tape is unrecoverable past ~24 h. Follow `aws-collector-deploy-checklist.md` §1.2's six-item allowlist; record the new InstanceId in §5a.

## 7. Out of scope

- **F2** — `ResetBufferState` drops trades in a narrow race (`DeribitWsFeed.vb:298`). ⚠ **A different bug in the same file.** Do not fix it here and do not conflate them: F2 is an occasional race, this is deterministic and costs half the tape at all times.
- **`LastTradeTimestamp` reading the last line rather than the max** — §3.5.
- **Recovering the existing half-tape** — D-5.
- **Any change to `analysis_log.csv`, scoring, or a rendered surface.** ⚠ This defect touches **none** of them: the engine reads trades from `MarketState`'s in-memory ring, never the store, and nothing has ever been calibrated off the tape. **Nothing needs redoing.**

## 8. Reversibility

Code-only and additive in behaviour. Reverting the file restores the shipped guard exactly; rows already written stay readable, since the read path's dedup is unchanged and was already tolerant of duplicates. **No settings key moves, so there is no config to roll back and no dataset boundary.**

---

## 9. ✅ BUILD RECORD — 2026-08-11

> **Review packet: [`trade-store-write-guard-spec-back.md`](trade-store-write-guard-spec-back.md)** — ranked verification handles, five decisions queued, feedback on this spec, and what was not verified. **This section is the outcome record; that document is the working document for the reviewing seat.** ⚠ Its §3.1 corrects an assumption **this spec makes in §2 and §3.4** — the guard's duplicates do not come from `SeedAsync`'s REST re-seed, which never reaches the store. Comment-only fix in `161aaa7`; the fix's correctness is unaffected.

### 9.1 What changed

| File | Change |
|---|---|
| `Core/TradeStoreWriter.vb` | `_lastTs` and `LastWrittenTimestamp` **deleted**. `Buffer` now calls `AlreadyCommitted` / `Remember` against a bounded window (`RecentWindowCapacity = 20000`, a public constant per D-3). New `ReadTradeFileTail(path, maxRows)` seeds the window from the file tail per D-4. New `RecentWindowCount` test surface for A55e's boundary |
| `DeribitWsFeed.vb` | Comments only, at both call sites (lines ~293, ~502) — they described a monotonic timestamp guard that no longer exists |
| `verify/ordercheck/Program.vb` | A55a–g added and registered. A48b's comment and `Check` label corrected — it was named "monotonic guard" and had never presented two trades on one millisecond |

**`AlreadyCommitted` reuses `DedupTrades`'s relation and does not copy it:** identified rows settle on `TradeId` alone, identity-less rows on `LegacyRowKey`, and an absent identity is never a key. An identified row registers **both** keys, matching `DedupTrades` Pass 1's `claimedLegacy` arm.

⚠ **One deliberate divergence from `DedupTrades`, recorded because it is a real behaviour difference and not an oversight.** `DedupTrades` settles identified rows first, which is what makes its result order-independent. A streaming guard sees arrival order only and cannot look ahead. So if an identity-less row arrives *before* an identified row sharing its five legacy fields, both are written where `DedupTrades` would keep one. **That is a duplicate on disk, which the read path removes — the §3.1 direction.** It is commented at the function.

### 9.2 ⚠ §5 MUTATION — all seven fixtures PROVEN to have teeth

The guard was reverted to the shipped `If t.Timestamp <= _lastTs Then Return False`, including its disk seed and its `ResetBufferState` reset, and the harness was re-run. **§5's escalation trigger did not fire.**

| Fixture | On the fix | On the shipped `<=` guard | What the mutation reported |
|---|---|---|---|
| **A55a** | PASS | ⚠ **FAIL** | `accepted=1` — wanted 2. The defect itself, on real Deribit rows |
| **A55b** | PASS | ⚠ **FAIL** | `acc1=2` (wanted 3), `onDisk=3` (wanted 4) |
| **A55c** | PASS | ⚠ **FAIL** | `accepted=2` — wanted 4; one identified and one legacy row lost |
| **A55d** | PASS | ⚠ **FAIL** | `accepted=1` — wanted 10. Nine of ten sweep legs discarded |
| **A55e** | PASS | ⚠ **FAIL** | `readmitted=False` — the bias inverted, which is the bug |
| **A55f** | PASS | ⚠ **FAIL** | `siblingAccepted=False`, `onDisk=2` — wanted 4 |
| **A55g** | PASS | ⚠ **FAIL** | `accepted=1` — wanted 2 |

⚠ **A55b, A55f and A55g would each have passed on the shipped guard as first drafted**, because their stated job in §5 is only to stop an older property regressing. Each was given a same-millisecond sibling pair until it failed. **A regression guard that passes on the unfixed code proves nothing about the fix** — that is A48b's whole story, and it nearly repeated here.

**A48b, A48d and A48e passed under BOTH the fix and the mutation**, which is the §1a finding restated as a measurement: those fixtures are structurally blind to this defect.

### 9.3 §6 acceptance

| Item | Result |
|---|---|
| Six projects, Release | ✅ **0 warnings / 0 errors** on all six: solution · AutoTweaker · WhatIfRunner · CeilingAudit · BacktestRunner · OrderCheck |
| Harness | ✅ **ALL PASS** — A1–A53h unregressed, plus A55a–g |
| Mutation stated | ✅ §9.2 |
| `verify-gate.ps1 prepush` | ✅ **GATE PASSED**, 0 warnings |
| Display-string parity | ✅ **NO OBLIGATION**, and the gate agrees (`no snapshot/card drift detected`). The trade store has no rendered surface: no snapshot line, no card binding, no CSV column, no bridge field |
| Deploy to AWS | ⛔ **NOT DONE — the one open item.** §6 makes it part of this change |

### 9.4 Two corrections to this spec's own text

1. ⚠ **D-6's expected version-bump WARN did not appear, and that is not the same as the guard clearing the change.** `tools/checks/verify-gate.ps1` line 126 tests **committed** paths against `Core/`, `DynamicNorms.vb`, `analysis/`. The gate was run with the work uncommitted, so `$changed` was empty and it reported `no engine-path change`. `Core/TradeStoreWriter.vb` matches `Core/`, so **the nudge will fire on the pre-push hook instead.** Expected and non-blocking, exactly as D-6 says — but it fires one step later than D-6 implies.
2. **D-3's cost figures were understated.** 20,000 entries is **≈5–10 h** of tape at the measured 31–60 trades/min, not ≈4 h, and it costs **a few MB** rather than ~1 MB (two precomputed key strings and two refcount dictionary entries per trade, not one bare key). Both are wrong in the harmless direction — more coverage and still trivial memory — so the ruling stands unchanged. The code comment carries the corrected numbers.

### 9.5 Left undone on purpose

- **`LastTradeTimestamp` still reads the last line, not the maximum** (§3.5). Now documented at the function. It no longer has anything to do with the write guard; its remaining caller wants a resume point.
- **F2**, the `ResetBufferState` race at `DeribitWsFeed.vb:298` (§7) — untouched.
- **The existing half-tape** (D-5) — untouched.
- **A restart in the first moments of a new month** seeds from the new month's empty file and may re-admit a few rows from the previous month's tail. Duplicates, in the safe direction; commented at `EnsureSeeded` rather than fixed, to avoid a second file read on every writer construction.

