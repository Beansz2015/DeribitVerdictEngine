# C1 Session 2 — orchestrator review, 2026-08-05

**Verdict: ✅ ACCEPTED. C1 is complete pending the trader's visual gate.** One finding, **medium, not blocking** — but it should be closed **before the daily RDP glance is actually retired**, because it is the one case where the strip does not yet do the job the glance does.
**Reviewed:** `5843e0b` + the §5 spec-back. **Method:** re-verified in the tree, not read from the write-up.

---

## 1. Independently verified

| Claim | Check | Result |
|---|---|---|
| Layout arithmetic (the one thing they could not test live) | Counted in `MainForm_Layout.vb` | ✅ `RowCount = 5` · five `RowStyles.Add` (1132/1136/1139/1142/1143) · five `Controls.Add` at **indices 0–4** (1244/1263/1275/1297/1410). No gap, no duplicate. **The paper verification was sound** |
| `SyncSettingsCardHeight` sums BOTH conditional strips | Read the body | ✅ `SETTINGS_CARD_H_BASE + If(exitShown, …) + If(tapeShown, …)` |
| Flush accounting — only a flush that committed ≥1 row counts | Read `Flush()` | ✅ `If written > 0 Then` guards both `_lastFlushUtc` and `_totalRowsWritten`, under the same `SyncLock _pending` the readers use |
| Tier thresholds | `ClassifyTapeStoreTier` | ✅ RED ≥10×, AMBER ≥3×, `Math.Max(1, flushSeconds)` guards a zero/negative divisor |
| No settings keys ⇒ no bump | `settings.json` line 2 | ✅ still **v65** |
| A49m pins what it claims | Harness output | ✅ *"a Saturday hour classifies out-of-scope-weekend and never defect, while Part B's liveness tier still reports RED on the identical silence"* |
| F1's legend line landed | `CoverageReport.vb` | ✅ present at two sites (console summary + the markdown that wraps it) |
| Acceptance | Independent Release rebuild + fresh harness run | ✅ OrderCheck 0/0, solution 0/0, **0 FAIL, ALL PASS** |

**Judgment calls §5.2.1–§5.2.5: all agreed.** §5.2.1's reasoning is particularly right — both named precedents (MIN NET MOVE %, EXIT GUARD) are dedicated rows, and folding capture health into the microstructure strip would bury the one signal meant to retire a glance. §5.2.5's decision not to launch live (the v57-stomp precedent) was correct: a run would have appended real collector rows under a fresh `InstanceId`.

**F1 and F2 handled correctly.** F1 got the legend line I recommended as the minimum, and the bounded fix was deliberately *not* rushed — the right call, and the reasoning given matches my own steer. F2 recorded, not changed, as specified.

---

## 2. Finding — `UNKNOWN` is unbounded in time, so a dead capture path renders as a benign cold start (medium)

`ClassifyTapeStoreTier(secondsSinceFlush As Double?, flushSeconds)` returns `"UNKNOWN"` whenever `secondsSinceFlush` has no value, and the tick handler renders `UNKNOWN` in the neutral tertiary colour under `Case Else ' NORMAL / UNKNOWN (cold start, not a fault)`.

`secondsSinceFlush` is `Nothing` in **two** situations, and the classifier cannot tell them apart because **it is never given a start time**:

1. the writer has not been constructed yet (no trades have streamed) — genuinely a cold start;
2. **the writer exists and every flush has returned 0.**

**Case 2 is the sharp one, and it composes with a deliberate design decision elsewhere.** A48e pins that an unwritable store — blocked directory, locked file, full disk, permissions — **never throws**: `AppendRows` logs to console and returns 0. So `written > 0` is never true, `_lastFlushUtc` stays `Nothing`, the tier stays `UNKNOWN`, and the strip reads **`TAPE STORE: no flush yet · 0 rows` in neutral, indefinitely.**

That is precisely the failure this element exists to surface, rendered as benign. The never-throws discipline and "a cold start is not a fault" are each correct on their own; **together they produce a silent failure on the only box that captures.**

**Why it matters more than its severity suggests:** tape is unrecoverable past Deribit's ~24 h window, and Part B's stated purpose is to retire the daily RDP glance — a glance that *would* catch this, because the v64 rider put the store's newest-file mtime in it. Until this is closed, **the glance cannot safely be retired**, which is most of Part B's value.

**Suggested fix, small:** give the classifier a second clock — seconds since capture *should* have started (feed connect, or the strip's own first tick) — and escalate `UNKNOWN → AMBER → RED` on the same 3×/10× thresholds when `Enabled` is true and no flush has ever landed. Cold start stays neutral for the first few multiples; a permanent one goes red. Pure, still fixture-reachable, no settings key.

### 2a. Minor, same area — `transport="rest"` shows nothing at all

`OnTapeStoreTick` hides the strip when `_wsFeed Is Nothing`, which is the REST-transport case. But under REST, capture still happens — via `TradeStoreGapRepair` → `HistoricalStore` → `TradeStoreWriter`, a different writer instance than the feed's. So on a REST box the strip is blank while capture is healthy. Not a false alarm (it is silent, not wrong), and REST is break-glass per D7, so **noted, not asked for.**

---

## 3. Also noted

**§15 is regrowing**, as their §5.4 flags: four settings-untouched rows now sit atop the five capped versioned ones. They were right not to splice it under time pressure. **I will take that trim** — I did the last one and know the mechanics; it is not a Session 2 defect and should not be charged to this build.

---

## 4. State

**C1 is functionally complete.** Both parts shipped, all thirteen A49 fixtures pass, settings unchanged at v65 throughout, no dataset boundary, no new hard constraint, HC28 still free.

**Open before the glance is retired:** the §2 finding.
**Open for the trader:** the visual gate — the SETTINGS & TOOLS card growing by 28 px when capture is on, the strip's shape and its neutral/amber/red transitions, and that it stays fully hidden with no dead space when capture is off. The element has never been rendered on screen by anyone.
