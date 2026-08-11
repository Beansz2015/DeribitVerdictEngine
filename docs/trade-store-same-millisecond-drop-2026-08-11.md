# ⚠⚠ The streaming capture path drops ~50 % of trades — `TradeStoreWriter.vb:149`

**Found:** 2026-08-11, while checking fixture **A48d** against the live gap-repair path at the trader's request.
**Status:** **DEFECT CONFIRMED by two independent instruments. Not fixed. No spec yet.**
**Severity:** ⚠⚠ **Live and ongoing.** Every hour the collector runs, roughly half the tape is lost at write time. Tape older than ~24 h cannot be recovered afterwards.

---

## 0. The finding in one line

> **`Core/TradeStoreWriter.vb:149` guards with `<=`, so every trade that shares a millisecond with the previously buffered trade is silently discarded — and Deribit reports a market order sweeping several price levels as several records at the same millisecond.**

```vb
If t.Timestamp <= _lastTs Then Return False
```

The guard exists for a good reason — `SeedAsync` re-seeds the trade ring from REST on every reconnect, so duplicates *will* arrive and must be rejected. **The bug is not the guard, it is that a millisecond timestamp was used as an identity.** It is not one.

---

## 1. Two independent instruments, agreeing

### Instrument A — the streamed block against the REST repair block

A gap-repair pass on 2026-08-05 13:02 re-fetched the previous 20 h and appended it. That gives an unusually clean natural experiment: **the same box, the same window, captured twice by two different paths.**

Window **2026-08-04 17:02 → 2026-08-05 13:02**, deduped per side on the shipped five-field contract:

| Measure | Streamed | REST repair |
|---|---:|---:|
| Distinct rows | **20,477** | **41,013** |
| Distinct timestamps | 20,443 | 20,443 |
| Total volume | 61,692,370 | 157,901,010 |

| Per-timestamp volume comparison | Count | Share |
|---|---:|---:|
| Identical | 12,920 | 63.2 % |
| **Repair has MORE** | **7,523** | **36.8 %** |
| **Repair has LESS** | **0** | **0.0 %** |

⚠ **The containment is strict and one-directional.** Repair ⊇ stream at **every one of 20,443 timestamps, with zero exceptions in the other direction.** Hourly, streaming holds 39–61 % of the repair count in all 21 hours — **not a block of zeros**, so this is not an outage. It is a steady-state shortfall while healthy.

**A worked example** — one millisecond, `1785862954632`:

```
streamed (1 row):   64005.50  10.00  buy
repair   (7 rows):  64005.50     10.00  buy      <- the one leg streaming kept
                    64005.50    960.00  buy
                    64007.00    150.00  buy
                    64007.00   3030.00  buy
                    64007.50  10000.00  buy
                    64010.00  14800.00  buy
                    64010.00   2370.00  buy
```

One market order sweeping five price levels. **The stream kept the first leg verbatim — 10.00 of 31,320 — and dropped the rest.** It is not an aggregate; it is one record of seven.

### Instrument B — `trade_seq`, which needs no second book at all

The trade-identity deploy (`d8678d2b…`, 2026-08-10 14:08) began writing `trade_seq`, Deribit's **per-instrument monotonic sequence**. The store now holds 431 identified rows:

| | |
|---|---:|
| `trade_seq` range | 296,042,924 → 296,043,739 |
| Sequence numbers in that span | **816** |
| Rows actually held | **431** |
| **MISSING** | **385 (47.2 %)** across 138 gap runs |
| Duplicate `trade_seq` values | 0 |

**A monotonic sequence with 385 holes is proof of loss on its own** — no venue call, no second book, no merge, no identity ambiguity. This is exactly the property the trade-identity spec called *"the field that outranks `trade_id`"*, and it settled the question on its first use.

**The two instruments agree without being told to: ~50 % capture (20,477/41,013) and ~53 % capture (431/816).**

---

## 2. Why this is NOT the withdrawn 78.8 % claim

The shapes look alike — two books, disagreement at shared timestamps, volume roughly doubling on union — so the difference has to be stated explicitly.

| | The withdrawn 78.8 % claim | This finding |
|---|---|---|
| What was compared | **Two boxes** (AWS vs local laptop) | **One box, two capture paths, same window** |
| Direction | Bidirectional disagreement | ⚠ **Strictly one-directional** — repair ⊇ stream at 20,443/20,443 timestamps, 0 reverse |
| Mechanism | **None identified** — that was the problem | ⚠ **Read in the code.** `TradeStoreWriter.vb:149`, a `<=` on a millisecond |
| Independent confirmation | None | ⚠ **`trade_seq` gaps**, needing no second book |
| Could identity settle it? | No — rows had none | Yes, and it did |

**The 2026-08-08 lesson still applied here and changed the answer twice on the way.** Two hypotheses were formed and rejected before the conclusion: **(1) multiplicity** — that `comm` was counting repeated rows rather than distinct trades; rejected, 20,538 *distinct* five-field values appear only in the repair block. **(2) WS aggregation** — that the channel batches several trades into one record; rejected, because aggregation preserves volume and the repair's mean trade is *larger* (3,850 vs 3,013), not smaller.

---

## 3. What A48d actually says — my earlier flag was too strong

**A48d is not contradicted, and it should not be described as failing.** Its "no-op" is scoped to two claims, both of which hold:

1. `ResolveResumeCursorMs` returns `-1` on a fully covered window, so no fetch is issued.
2. A read collapses on-disk duplicates.

⚠ **A48d's own case (4) deliberately writes duplicates to disk** — `raw = 10` for five trades — and asserts only that `DedupTrades` returns 5. **On-disk uniqueness was never claimed.** So the 20,477 duplicate rows in the live store are consistent with A48d, not evidence against it.

**But A48d has a blind spot, and it is exactly the one that matters:**

> Its fixture trades are built as `A48Trade(A48Ms(i * 1000L), …)` — **one second apart. Every timestamp is distinct.**

So neither A48d nor A48b — the fixture named *"monotonic guard"* — ever presents **two trades at the same millisecond**, which is the only input where the `<=` guard destroys real data. The family pins the guard's **idempotence** and never its **loss**.

⚠ **The data needed to catch this was already in the repo.** Fixture **A53e**, added by the trade-identity build, uses two *real* Deribit trades at the same millisecond (`439922656` / `439922657`, same price, amount and direction). It points them at the **dedup contract** and never at the **write guard**. One fixture, aimed one seam away.

---

## 4. This is a different, much larger defect than F2

`trader-tick-queue.md` §2 carries **F2 — `ResetBufferState` drops trades in a narrow race** (`DeribitWsFeed.vb:298`, ~4 lines, one `SyncLock`).

⚠ **This is not that, and the "narrow race" framing would lead someone to under-prioritise it.** F2 is a race that loses trades occasionally. This is deterministic, steady-state, and costs **roughly half the tape at all times**. Both are real; they are not the same bug and they do not share a fix.

---

## 5. What is affected — and what is NOT

**Affected: the trade store only.**

- Every streamed tape row written since capture began (v64, 2026-08-01 17:50 UTC).
- ⚠ **Unrecoverable beyond ~24 h.** Deribit's public-trades retention is ~24 h, so tape already lost cannot be re-fetched. Gap repair recovers only what is still inside the window — which is what accidentally exposed this.

**NOT affected — and this matters for how urgently anything else needs re-checking:**

- ⚠ **No scoring, no verdict, no CSV column, and nothing in `analysis_log.csv`.** The live engine reads trades from `MarketState`'s in-memory ring, not from the store. `TradeStoreWriter` is a **write-only sink** with no rendered surface — the v64 spec's "ZERO scoring impact" holds.
- **Nothing has ever been calibrated off the tape store**, so no derivation, threshold or watch needs redoing. Every figure in the D3 read, the OBV re-anchor and the ceiling audit comes from `analysis_log.csv`, which is unaffected.

**The cost is future optionality, not past results:** the store exists so trade-derived signals (CVD, MicroCVD, TFI, aggressor velocity, liquidations) can be **re-derived under different settings** from raw ticks. A tape holding half the trades — and a *biased* half, since it systematically drops the later legs of sweeps — cannot support that.

⚠ **The bias is the worst part.** This is not a random 50 % sample. It preferentially discards multi-leg sweeps, which are exactly the aggressive, high-urgency prints that CVD, TFI and aggressor velocity are built to measure. **Any future re-derivation off this tape would understate aggression precisely where it matters most.**

---

## 5a. ⚠ Gap repair CANNOT heal this, and the reason is structural — added 2026-08-11

**The v64 design is "two deliberately redundant halves, neither sufficient alone." Against this failure mode they are not redundant at all.**

`TradeStoreGapRepair.RepairOnceAsync` builds the window `[now − gap_repair_lookback_hours, now]`. But the fetch does not start at the window's start — `ResolveResumeCursorMs` starts it at **the file's last written row + 1**:

```vb
Dim resumeMs As Long = LastTradeTimestamp(path)   ' the file's LAST LINE
cursorMs = resumeMs + 1
If clampToSegStart AndAlso cursorMs < segStartMs Then cursorMs = segStartMs
If cursorMs > segEndInclMs Then Return -1          ' "already covered" ⇒ no fetch
```

With streaming active, the last written row is always ~now. **So the cursor is always at the tail, the fetched window is near-empty, and everything streaming dropped sits BEHIND the cursor and is skipped permanently.**

**This predicts something that should have looked odd earlier and did not:** repair runs every `gap_repair_interval_hours` (6), yet across ten days it appended **exactly one** block. It fired that once because something — a flush stall or a brief disconnect — left a genuine hole at the tail, which is the only shape that lets the cursor reach backwards. **That single accident is the only reason this defect was ever visible.**

⚠ **Repair recovers DOWNTIME — a hole at the tail. It cannot recover IN-FLIGHT LOSS — holes behind the cursor.** The v64 spec states the assumption in its own words: *"streaming is complete while the app runs and recovers NOTHING from downtime."* **The first half of that sentence is false, and the whole redundancy argument rests on it.**

**Consequence for planning: there is no stopgap.** No settings value fixes this — the no-op is structural, not a tuning question. Loss continues at roughly half the tape every hour the collector runs until a code fix ships.

---

## 5b. ⚠ A free second experiment is arriving — MEASURE IT AT THE NEXT COPY-BACK

**Deribit entered system maintenance on 2026-08-11** (`{"error":{"message":"system_maintenance","code":11051}}`, HTTP 503 on every endpoint). When it returns, gap repair will backfill the outage window **via `AppendRows`, which bypasses `Buffer` and therefore bypasses the guard.**

**So the tape will hold a COMPLETE band sandwiched between two ~50 % streaming bands.** That is a second natural experiment, free, and it confirms the finding by a different route than either instrument in §1 — this time with the boundaries known in advance rather than discovered by accident.

**What to measure**, on the first copy-back after the venue returns:

1. Locate the outage window from `ws_health.log` and the `analysis_log.csv` hole.
2. Compare rows/hour **inside** the repaired band against rows/hour in the streamed bands either side. **Prediction: the repaired band runs ~2× the streamed density.**
3. Compare `trade_seq` continuity inside the repaired band against the streamed bands. **Prediction: near-contiguous inside, ~47 % missing outside.**

⚠ **This window closes.** Once the tape scrolls past Deribit's ~24 h retention the repaired band can never be reconstructed, and nobody will think to look for it later. **If the fix ships before the measurement is taken, the experiment is lost** — the repaired band would then be indistinguishable from correctly-streamed tape.

---

## 6. The fix, and why it is not a one-liner

The guard cannot simply become `<`. That would re-admit every duplicate the REST re-seed produces on each reconnect, which is the failure the guard was written to prevent.

**The guard needs an identity, and one now exists.** `trade_id` has shipped and is on every streamed row since 2026-08-10 14:08. The correct guard is *"reject a trade whose `trade_id` I have already committed"*, with the timestamp comparison kept only as the pre-identity fallback for the reconnect window.

**Named, not designed here.** Three things a spec must settle, none of which is obvious:

1. **How much identity state to retain.** A per-`trade_id` set cannot grow forever at ~60,000 trades/day. A bounded ring keyed on `trade_seq` is the obvious candidate, since it is monotonic and dense.
2. **The pre-identity fallback.** REST re-seed rows and any older path may still lack identity; the guard must stay safe when identity is absent, without collapsing to the current defect.
3. ⚠ **Whether the feed even delivers the dropped trades.** This is the one thing the stored data cannot answer. The guard demonstrably *would* discard them if delivered, and the fact that every missing row sits at an **already-seen timestamp** (7,525 disputed timestamps, **0 new moments**) is the guard's exact signature — but "the WS channel delivered them" is **inferred from that pattern, not directly observed.** A spec should confirm it by instrumenting `ApplyTrades` before designing around it.

**Model: Opus. Effort: high.** The thing being fixed is a guard whose failure mode looks exactly like correct behaviour, in a never-throws write path, where a wrong fix silently reinstates either the duplicates or the loss. **Escalation trigger:** if the fixture cannot be made to fail on the *current* code before the fix goes in, stop — the test is not testing what it claims.

**Fixtures the fix must carry, at minimum:**

- Two trades at the **same millisecond** with different `trade_id` ⇒ **both survive**. This is the fixture that would have caught it, and A53e already contains suitable real Deribit data.
- The reconnect re-seed replaying an identical batch ⇒ still idempotent (A48b's property must not regress).
- A mixed batch — some rows identified, some not — ⇒ neither duplicates nor drops.
- ⚠ **Prove the teeth by mutation:** revert `TradeStoreWriter.vb:149` to `<=` and confirm the new fixture fails. The trade-identity build set this precedent and it is the only thing that distinguishes a real test from a passing one.

---

## 7. What I did not verify

- **That the WS channel delivers the same-millisecond trades.** See §6 item 3 — inferred from the timestamp pattern, not observed.
- **The exact loss rate outside the one 20 h window.** Both instruments give ~50 %, from a 20 h window and a ~20 minute window respectively. **I did not sweep the whole store.**
- **Whether the same defect touches the REST backfill path.** `AppendRows` is called directly there and does not pass through `Buffer`, so it should be exempt — the repair block's 41,013 distinct rows against the stream's 20,477 is consistent with that, but I did not read the backfill's own write path.
- **Whether liquidation rows are affected differently.** Not examined.
- **Anything about the local box.** Local capture is OFF under D1, so it holds no tape to check.
