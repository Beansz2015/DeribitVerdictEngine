# JOB 1 — outstanding items swept, and J-A ratified (2026-08-01)

**From:** the incoming orchestrator seat. **Prompted by the trader**, who asked whether JOB 1's pending items had been resolved — they had not, and **I had not flagged them**. The seat-close handover made the A48f ratification its §4 **task 3**; my gap audit reproduced the task list but then tracked only G-numbered items, so task 3 fell out. Recorded because that is the same drop-a-task-off-a-list shape the gap audit was written to catch.

**Sources:** [`trade-store-capture-review-2026-07-31.md`](trade-store-capture-review-2026-07-31.md) §3 (F1–F6) · [`trade-store-arc-spec-back-2026-07-31.md`](trade-store-arc-spec-back-2026-07-31.md) §2 (J-A).

---

## 1. J-A — RATIFIED. Both readings were right, about different claims.

The handover asked for a **fresh read of the fixture**, inheriting neither side's framing. Read at `verify/ordercheck/Program.vb:7153-7205`.

**What A48f proves today (post-F1-fix):**

1. **The shipped gate answers correctly, three arms** — `TradeStoreWriter.ShouldCapture` returns false for `Enabled=False`, false for `Nothing`, true for `Enabled=True`. This is the **production** function: `DeribitWsFeed.vb:150` calls `If Not TradeStoreWriter.ShouldCapture(ts) Then Return Nothing`. Verified three call sites — seam, production, fixture.
2. **`ShouldGapRepair` honours both switches independently**, all four arms.
3. **The shipped POCO defaults are pinned** — `Enabled`, `store_dir`, flush 30 s / 500, repair on, 6.0 h / 20.0 h. Genuinely useful: an absent settings block resolving wrong is a live failure mode.
4. **No file appears when the gate is closed.**

**What it still does not prove:** that a closed gate makes the **`ApplyTrades` fold** inert. `ApplyTrades` is private on `DeribitWsFeed`, which owns a `ClientWebSocket` the harness deliberately does not link (the A22/A37 boundary). The fixture proves the gate answers false and no file appears; **it never drives the feed.**

**The ratified reading:** *the disagreement was two people describing two different claims in one sentence.*

- **The implementer's** *"A48f pins the stronger reading"* is **correct about the two-switch asymmetry** — `ShouldGapRepair`'s four arms genuinely are pinned, and always were the stronger half.
- **The reviewer's F1** is **correct about the reversibility claim** it was attached to — pre-fix, A48f re-stated the gate predicate inline and would have kept passing if the production gate lost its `Not ts.Enabled` arm. That was the A43f shape and the fix was the right response.
- **Post-fix the split is exact and the fixture's own comment now states it**, verbatim: *"What remains REASONED rather than proven: that a closed gate makes the ApplyTrades fold inert."* That comment is the ratification in code, and it is honest.

**One consequence still outstanding.** The `settings.json` `change_log` v64 entry still reads:

> *"REVERSIBILITY: `enabled:false` ⇒ the fold early-outs and nothing is written, byte-identical to pre-build **(harness-proven, A48f)**."*

Post-fix that is **still stronger than what A48f establishes** — the **gate decision** is harness-proven; the **fold's inertness** is reasoned. Accurate wording: *"gate harness-proven (A48f); fold inertness reasoned — `ApplyTrades` is unlinkable in the harness."*

**Not editing `settings.json` for prose alone** — a change_log-only diff would trip the version-bump guard with no key change to justify a bump. **It rides the next settings touch**, the same discipline as the `TriggerMode` column riding a rotation. Recorded here and in §15 so it is not lost.

---

## 2. The v64 review's findings — actual status, checked in code

| # | Severity | Status |
|---|---|---|
| **F1** | moderate | ✅ **FIXED and verified.** `ShouldCapture` hoisted to `TradeStoreWriter.vb:154`, called by production at `DeribitWsFeed.vb:150` and by the fixture at `Program.vb:7181-7183` under an explicit `[F1 fix]` comment. Three hits, exactly as the spec-back claimed. |
| **F2** | low | ⚠ **OPEN — not fixed.** `ResetBufferState` still calls `Flush()` *outside* the lock, then `SyncLock _pending { _pending.Clear() … }`. A trade buffered between the two is silently discarded. Bounded (a handful at reconnect, and gap repair recovers them), but the `Clear()` is what makes it lossy rather than merely racy. `Monitor` is re-entrant, so wrapping the whole body in one `SyncLock _pending` closes it with no deadlock risk. |
| **F3** | cosmetic | ⚠ **OPEN.** `tools/BacktestRunner/HistoricalStore.vb:52` still sets `User-Agent: DeribitBacktestRunner/1.0`, so the live collector's repair calls reach Deribit under the backtester's identity. Only matters if venue-side attribution is ever used to reason about collector behaviour. |
| **F4** | — | ✅ Resolved (spec-back shipped inside `1229a30`). |
| **F5** | — | ✅ Resolved post-commit; the generalisable lesson (a pre-commit `GATE PASSED` has two vacuous guards) is already carried in the gap audit and JOB 1 packet. |
| **F6** | moderate (operational) | ⚠ **OPEN, and it is LATENT rather than live — see §3.** |

---

## 3. F6 is latent, and it fires on exactly the build that is the test gate

F6 says the shipped `trade_store.enabled: true` means the **local** box captures too — the dual-capture topology D1 ruled out. Checked what is actually running:

- **Tracked `settings.json`** → `trade_store.enabled = true`, mtime **2026-07-31 02:32**.
- **`bin\Debug\net8.0-windows\settings.json`** → **version 63, `trade_store` block ABSENT**, mtime **2026-07-30 00:24**.
- **Running exe/dll** → **2026-07-30 17:45 UTC**, i.e. pre-v64.
- **`bin\Debug\…\backtest_data\`** → **absent**. No local capture is happening.

So **the v64 build is not live locally** — the collector is running a pre-v64 binary against v63 settings, consistent with "awaiting the trader's test + push."

**The consequence that matters:** `settings.json` is `CopyToOutputDirectory=PreserveNewest`, and the tracked file is already **newer** than the bin copy. **The moment the trader builds Debug to test v64, the v64 settings land in `bin\` and local capture starts** — ~2.4 MB/day, ~900 MB/year, into a directory nobody watches. F6 therefore needs deciding **before** the test build, not after it.

Three ways, trader's call: set `trade_store.enabled: false` in the **local** `bin\Debug\settings.json` after the test build (the current manual chore, with the silent-restore failure mode v57 and the overlay spec both exist to fix); accept dual capture explicitly and record that D1 was softened in practice; or **build the `settings.local.json` overlay first**, which is precisely the feature F6 generated — the overlay's own header names the F6 ruling as its origin.

That last option is the tidy one and it re-orders the queue: **the overlay stops being a convenience and becomes the clean way to run the v64 test.**

*(Aside, noted not asserted: the running exe's 2026-07-30 17:45 UTC stamp is the same rebuild the JOB 1 packet flagged — "something did rebuild Debug at 2026-07-30 17:45 UTC … and the collector was down for ~16 h afterwards. I did not establish a causal link and am not asserting one." Same timestamp, same non-assertion.)*

---

## 4. What remains open on the JOB 1 arc

- **F2** — a ~4-line lock fix. No boundary, no settings, no parity surface. Any gap.
- **F3** — one-line User-Agent, cosmetic. Any gap, or never.
- **F6** — a **trader decision**, best resolved by sequencing the overlay ahead of the v64 test.
- **The change_log wording** — rides the next settings touch (§1).
- **Everything else on the arc is closed:** the two store-integrity incidents fixed, the candle-merge test debt closed by A51a–e (verified present), F1/F4/F5 resolved, J-A ratified here.
- **Not JOB 1's, but adjacent and still open:** the coverage-report and overlay D-tables await the trader, and the v64 test-and-push gate is the trader's.
