# Seam audit — 2026-08-11

**Brief:** [`seam-audit-brief-2026-08-11.md`](seam-audit-brief-2026-08-11.md). **Type:** read-only. **Nothing was fixed.**
**Tree state at audit:** `master`, **ahead 6** of `origin/master`, tracked `settings.json` line 2 = **v66**.
**Model + effort used:** Opus, high.

---

## 0. The short version

**Seven findings. Two matter more than the rest.**

| # | Finding | Live or latent | Age |
|---|---|---|---|
| **S-1** | ⚠⚠ **`tools/WsTradeProbe` is a FOURTH trade-parse site and does not use the shared reader.** It is the delivery gate for the `TradeStoreWriter.vb:149` fix | **Live** | 1 day |
| **S-2** | ⚠ **Method `Optional` defaults are a THIRD copy of every threshold. 11 of 42 disagree with the shipped JSON** | Latent (app) / **live (harness)** | up to 3 months |
| **S-3** | ⚠ The seeded session buckets in `EngineSettings.vb` are pinned to **settings v30**; JSON moved at v34, v36 and v58 | Latent | ~32 settings versions |
| **S-4** | The eval-cache backfill guard keys a run on a **1-second timestamp**, and does not update inside its own loop | Latent | since settings v26 |
| **S-5** | The pooled-book **minute-key dedup** would drop 5.08 % of the local book, and its losses cluster at a session boundary | Ops procedure, not code | — |
| **S-6** | `network.transport` POCO default never followed its own "P3 flips this" comment | Latent, benign | since WS-P1 |
| **S-7** | `CalcCVD`/`CalcMicroCVD` fixture data makes two shipped code paths **structurally unreachable** in test | Latent | — |

**The fourth instance the brief asked for is S-1.** It is the same shape as instance 1 and it sits inside the instrument that is supposed to certify instance 1's fix.

**Nothing found is actively destroying data.** The brief's §4 exception was not triggered.

---

## 0a. ⚠ The implementer this audit asks for — one seat, two rows

**Two of the seven findings need a build slot. The other five are cleanup, decisions, or doc edits.** Recommendation surfaced here so the orchestrator can size the spend without reading to §6.

> ### **Model: Sonnet. Effort: medium. ONE session, both rows, Row 1 first.**

**Which rows:** **Row 1** (route `tools/WsTradeProbe` through the shared trade reader) and **Row 2** (widen the queued `A54a` guard from two copies to three). Full text in this document §6.

**Why Sonnet rather than Opus.** The judgement work is finished and written down. Row 1 is a project reference plus two call-site swaps against a reader that already exists and is already reviewed. Row 2 has an in-repo template — `A52a` does exactly this shape for one key — and this document §1.4 supplies the complete three-way table, so the implementer does not have to derive which keys must agree. **Neither row requires a design decision.** That is the reason for the tier, not a general impression that the work is small.

**Why one session and why this order.** The two rows share one mental model — "count the copies of a value" — so splitting them pays the orientation cost twice. **Row 1 goes first because it is time-sensitive:** the probe gates the `TradeStoreWriter.vb:149` fix, and every reading taken from an unfixed probe has to be retaken.

**Where Sonnet will specifically slip — three traps, all concrete:**

1. ⚠ **Row 1, the tempting wrong fix.** Editing the probe's sentinel from `0` to `-1` makes the two readers agree *today* and leaves two readers in the tree. **That reproduces the defect class instead of removing it.** The fix is to call the shared reader.
2. ⚠ **Row 2, the matcher.** Matching on key name alone gives mostly false positives — 57 POCO classes share `period`, `enabled`, `window_size`. **Match class → JSON block → key.** A naive matcher gave 10 false hits out of 12 during this audit.
3. ⚠ **Row 2, the scope rule.** The guard must cover keys with a **concrete POCO default** and deliberately exclude `Double?` = Nothing nullable overrides. Getting this backwards produces a guard that fires constantly on the override keys and stays silent on the ones that actually drift.

⚠ **The fixtures cannot be relied on to catch these, because the implementer writes the fixtures too.** `A56c` and `A56d` in particular pass trivially if written carelessly — both currently-omitted parameters have plausible-looking defaults. **Prove the teeth by mutation**, the way the trade-identity build did: inject each trap, confirm the matching fixture fails.

> ### ⚠ Escalation trigger — move to Opus/high
> **Two observable conditions, either one:**
> - Linking `DeribitClient.vb` into `WsTradeProbe.vbproj` drags an `HttpClient` dependency into the probe. That is the same network/format split that put `TradeStoreWriter` in `Core/`, and it needs the same structural treatment — not a workaround.
> - The Row 2 guard cannot read the method `Optional` default without reflection or source parsing. **A guard covering only two of the three copies is the thing being replaced**, so re-scoping it is a design decision, not a build step.

**Not in this seat:** Row 3 (seeded session buckets) carries a real decision about whether the code-defaults path should exist at all — **that is a trader tick, not a build**. Rows 4–6 are independent and can go to any free slot.

---

## 1. Sweep 1 — POCO ↔ JSON default pairs

### 1.1 Method

I did not match on key names. I parsed `Core/Settings/EngineSettings.vb` into a class graph, walked it from the `EngineSettings` root, and resolved each property to a **JSON path** before comparing. The brief's trap 2 is real: 57 classes share key names like `period` and `enabled`.

Two corrections to the brief's own numbers, both verified:

- The brief's §6 says **175** properties carry an explicit default. Measured: **255** scalar properties with an explicit default.
- `SettingsLoader.vb:317` and `:360` set `PropertyNameCaseInsensitive = True`. **Case drift is therefore not a hazard.** I checked anyway and found none.

### 1.2 Found — genuine drift

| Path | POCO | Shipped JSON | Verdict |
|---|---|---|---|
| `indicators.CVD.slope_pct_of_value` | 0.01 | 0.10 | Known (brief §6) |
| `indicators.MicroCVD.accel_threshold_dynamic_pct` | 0.03 | 0.30 | Known (brief §6) |
| `session_volume.sessions` ASIA `high_multiplier` | 0.8 | 1.00 | ⚠ **NEW** |
| `session_volume.sessions` ASIA `mid_multiplier` | 0.85 | 1.00 | ⚠ **NEW** |
| `session_volume.sessions` ASIA `execution_resolution` | 1 | 3 | ⚠ **NEW** |
| `session_volume.sessions` LONDON `execution_resolution` | 1 | 3 | ⚠ **NEW** |

**S-3 in full.** `SessionVolumeSettings.Sessions` is not an empty list — it is seeded by a `From { … }` collection initialiser, and the code comment above it reads *"Default buckets aligned to live **v30**"*. The tree is on v66. The comment is honest about being a snapshot of a moment; nobody re-synced it. The NY bucket still agrees on every key, so the drift is ASIA and LONDON only.

This is the same shape as the `OBV.trend_gate` drift the queue records: a value seeded to match "live at the time", then left behind.

### 1.3 Found — divergence that is defensible, stated anyway

- **`network.transport`** — POCO `"rest"`, JSON `"ws"`. The POCO comment reads *"Transport: 'rest' | 'ws' — cutover flag; **P3 flips the default**."* P3 shipped; the flip never happened. **No fixture exposure** — `A16d` and `A16e` pass the transport string as literals, not through a cfg. Harmless today. Recorded because the code states an intent that was not carried out, which is how a reader is misled later.
- **`signal_bridge.enabled`** — POCO `false`, JSON `true`. A safe-default-off for an emitter is legitimate. No action.
- `version` / `last_modified` / `modified_by` — metadata, not behaviour. Legitimate.

### 1.4 ⚠ Sweep 1's second axis — a THIRD value the brief did not name

The brief scoped this sweep to two things that must agree. **There are three.** Every indicator threshold also exists as a VB `Optional` parameter default on the `Calc*` method itself.

**11 of 42 `Optional` defaults in `Core/Indicators_*.vb` disagree with the shipped JSON:**

| Function | Parameter | Method default | Shipped JSON |
|---|---|---|---|
| `CalcOBV` | `trendGate` | **10.0** | **23.0** |
| `CalcCVD` | `slopeMinUsd` | 50000 | 12000 |
| `CalcCVD` | `slopePctOfValue` | 0.05 | 0.10 |
| `CalcCVD` | `divergencePriceGate` | 0.002 | 0.0005 |
| `CalcMicroCVD` | `dynamicPct` | 0.0 | 0.30 |
| `CalcOFI` | `buyDominantRatio` | 1.2 | 1.6 |
| `CalcOFI` | `sellDominantRatio` | 0.833 | 0.625 |
| `CalcOFI` | `bookDepth` | 3 | 5 |
| `CalcLiquidations` | `dominanceRatio` | 1.0 | 2 |
| `CalcRSIDivergence` | `pivotWing` | 3 | 2 |
| `CalcRSIDivergence` | `lookbackBars` | 30 | 20 |

**Failure bias and reach, both verified:**

- **The app is safe.** Every production call site passes the cfg value by name. I checked all of them: `UI/MainForm_Analysis.vb`, `tools/BacktestRunner/ReplayLoop.vb`, `ExitGuardEvaluator.vb`, `LiveMicrostructureEvaluator.vb`. All three offline tools load real settings through `SettingsLoader.Initialise`; `tools/CeilingAudit/CeilingAuditProgram.vb:71-78` fails closed rather than falling back to POCO defaults.
- **The harness is not safe.** Fixtures either omit the argument or pin a stale literal. That is the brief's own question — *"has the harness been pinning behaviour the app does not have?"* — and the answer is **yes, on this axis.**

⚠ **The sharpest single fact in this audit's sweep 1:** v66 moved `OBV.trend_gate` in POCO and JSON "in lockstep". **Two of the four copies still say 10.0** — the method default at `Core/Indicators_Structure.vb:44`, and the fixture literal at `verify/ordercheck/Program.vb:661-662`. **The queued `A54a` guard pins POCO against JSON only, so it would not catch either.**

### 1.5 ⚠ The brief's §6 hypothesis — tested, and the answer is sharper than the guess

The brief proposed that *"the unit of this defect may be the commit, not the key"*, and named v34, v40, v41, v48 and v58 as the next places to look.

**Confirmed, with corrections.** Traced by `git log -S`:

| Commit | Version | Produced surviving drift? |
|---|---|---|
| `1e9df84` | v33 | Yes — OBV, MicroCVD |
| `61b4532` | **v34** | ⚠ **Yes — CVD `slope_pct` moved AGAIN here, and ASIA `session_volume`** |
| `33cf40f` | **v36** | ⚠ **Yes — `execution_resolution`. Not on the brief's list** |
| `bd31a1a` | **v58** | ⚠ **Yes — ASIA `session_volume` dial-to-neutral** |
| — | v40, v41, v48 | **No** |

⚠ **Correction to the brief §6:** it states all three POCO drifts come from **one** commit, v33. For `CVD.slope_pct_of_value` that is not the whole story — v33 moved it, and **v34 moved it again**. The current divergence was set at v34.

⚠ **And the reason v40/v41/v48 are clean is the useful part, because it tells you how to stop this class.** Those commits changed `roc_magnitude_threshold` and `roc_slope_delta_threshold`, which are declared **`Double?` = Nothing** — the nullable-override pattern, where absent means "inherit". **A nullable override cannot drift, because it carries no competing value.** Keys with a concrete POCO default can. That distinction should drive the `A54a` guard's scope, not a hand-listed key set.

### 1.6 Checked and found CLEAN — negative results that bound the class

- **Zero JSON orphans.** `SettingsLoader` sets no `UnmappedMemberHandling`, so an unmapped JSON key would be silently skipped. I walked the JSON against the POCO in reverse. Every shipped key maps.
- **Zero case mismatches**, and case-insensitive matching is on regardless.
- **Zero POCO blocks with no JSON counterpart.**
- **Zero unreachable POCO classes** — all 57 are reachable from the root.
- The nullable session-override pattern behaves as designed everywhere it appears.
- `indicators.spread.wide_threshold_bps` **agrees** across all three copies. My first path guess for it was wrong and produced a false "absent"; I caught it and corrected it. Recording this because it is trap 2 landing on me mid-audit.

---

## 2. Sweep 2 — dedup and guard keys

### 2.1 Found — S-4, the eval-cache backfill guard

**Seam:** `LivePerformanceTracker.vb:358` and `:431`.

```
Dim existingTs As New HashSet(Of DateTime)(_evalCache.Select(Function(e) e.Timestamp))
…
If existingTs.Contains(row.Timestamp) Then Continue For
```

**The two things that must agree:** the guard's key, and the identity of an analysis run.
**The key is a 1-second timestamp.** `AnalysisLogger.vb:180` writes `"yyyy-MM-dd HH:mm:ss"`. This is instance 1's shape in a different seam.

**Two distinct defects in one guard:**

1. **The key is not an identity.** The CSV carries `InstanceId` and `SignalId` — a run *does* have an identity, and the guard does not use it.
2. **The guard is not applied uniformly.** `existingTs` is built once, before the loop, and never updated inside it. So a cold start **admits** two same-second rows, and a warm start **drops** one. The bias flips depending on cache state.

**Live or latent: latent, and I measured it rather than assuming.**

| Book | Rows | Distinct timestamps | Duplicates |
|---|---|---|---|
| Local `bin\Debug\net8.0-windows\analysis_log.csv` | 10,779 | 10,779 | **0** |
| AWS copy-back 2026-08-10 | 15,499 | 15,499 | **0** |

**On a single box the collision does not occur today.** At 60 s and 180 s cadences it essentially cannot. It becomes reachable through a manual *Analyze Now* landing in the same second as an auto-run fire, or through a pooled book.

**Age:** the guard dates to `625ad4a`, the original live-performance-strip build at settings v26 — about 40 settings versions.

### 2.2 Found — S-5, the pooled minute-key dedup

The Kelly dated trigger in [`trader-tick-queue.md`](trader-tick-queue.md) §4 needs a **pooled** weekday STRONG count, and [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §3b specifies an **AWS-preferred minute-key** dedup for pooling. I measured what that key does.

- **Cross-book same-second collisions: 1,415** local timestamps also present in the AWS book.
- **Under a minute key**, within each book alone:

| Book | Minutes carrying >1 distinct run | Rows a minute-key dedup would DROP |
|---|---|---|
| AWS | 25 | **25 (0.16 %)** |
| Local | 181 | **548 (5.08 %)** |

⚠ **The AWS losses are not random — they cluster at `13:00`.** `2026-08-03 13:00`, `2026-08-04 13:00`, `2026-08-05 13:00` each carry two distinct runs. 13:00 UTC is the NY `session_volume` boundary where `execution_resolution` switches 3 → 1, so the cadence changes and two runs can land in one minute. **A session-correlated loss is the same "biased, and worse than its size" property that made instance 1 serious.**

**Scoping, stated plainly:** this dedup is **a documented ops procedure, not code.** I found no minute-key dedup implemented in any `.vb` file. So this is a hazard for the next pooled read, not a live code defect. It is in scope because the queue has a pooled read scheduled and this audit can measure it now.

### 2.3 Checked and found CLEAN

- **`MarketState.vb:102-109`** — candle staleness. Keys a candle on its open time: `=` updates the forming bar, `>` rolls, `<` is ignored as stale. **Legitimate** — a candle *is* identified by its open time, exactly as the brief said. The contrast with trades is the whole lesson.
- **`Core/BarCloseDetector.vb:36-53`** — `lastSeenOpen` monotonic guard on bar opens. Same reasoning, legitimate.
- **`Core/TradeStoreWriter.vb:386` `DedupTrades`** — the shipped identity-first dedup. Uses `seenIds` plus `claimedLegacy`, never keys on an absent identity. This is the reviewed and ratified build; no new finding.
- **`Core/AggressorVelocityAccumulator.vb` / `Core/OfiAccumulator.vb`** — `dt = Math.Max(0.0, (tsMs - _lastFoldMs) / 1000.0)`. An out-of-order fold yields `dt = 0` rather than a negative decay. Bias is **admit with zero decay**, which is the safe direction here.
- **`Core/WsHealthLog.vb:51`** — logs only on state change. This is a deliberate change-log, and the queue already records the consequence (an EC2 stop leaves no `DOWN` line). No new finding.
- **`TradeStoreWriter.vb:149`** — the known same-millisecond drop. Confirmed present and unchanged. Not re-reported.

---

## 3. Sweep 3 — "one seam, no copies" claims

Three of four claims **hold**. One is **now false**.

| Claim | Verdict |
|---|---|
| `SignalEmitter.ComputeSideLevels` — one seam across 4 surfaces | ✅ **HOLDS**, and understates itself — **8** production call sites |
| `TradeStoreWriter` — shared by feed, backfill, reader | ✅ **HOLDS** |
| `TradeCostSettings.EffectiveMinMovePct` — shared by 5 consumers | ✅ **HOLDS**, and understates — there is a **6th** |
| `TradeRecord.ReadTradeId` / `ReadTradeSeq` — all three parse sites | ❌ ⚠⚠ **FALSE as of 2026-08-11** |

### 3.1 ⚠⚠ S-1 — the finding this audit exists for

**`tools/WsTradeProbe/WsTradeProbeProgram.vb` is a fourth trade-parse site, and it does not route through the shared reader.**

- It declares its own `TradeId As String` / `TradeSeq As Long` fields at `:53-54`.
- It reads them with its own private `ReadString` (`:355`) and `ReadLong` (`:333`) at `:203-204` — **not** `TradeRecord.ReadTradeId` / `ReadTradeSeq`.
- `tools/WsTradeProbe/WsTradeProbe.vbproj` links **none** of the shared source files. It is fully standalone.
- Introduced **2026-08-11** in `31483ce`, one day before this audit.

**Why this is the same defect class, and why it is worse than a duplicate parser:**

**`WsTradeProbe` is the delivery gate for the `TradeStoreWriter.vb:149` fix.** [`trader-tick-queue.md`](trader-tick-queue.md) §2 records that fix as *"gated on a venue probe"*, and `31483ce`'s own message calls the probe *"the delivery gate for the same-millisecond drop"*. **A gate whose parsing differs from production's does not measure what production will do.**

**The concrete divergence, verified line by line:**

| Behaviour | Shared seam (`DeribitClient.vb:400-414`) | Probe (`:333-346`) |
|---|---|---|
| `trade_seq` absent | `AbsentSeq` = **−1** | **0** |
| `trade_seq` negative | rejected → `AbsentSeq` | returned as-is |
| Validity rule | `n >= 0` is a **real value** | presence tested as `TradeSeq > 0` |
| `trade_id` absent / whitespace | `Nothing` | `""` |
| `trade_id` whitespace handling | trimmed; whitespace-only ⇒ absent | returned unchanged |

**The reachable divergence:** a genuine `trade_seq = 0` is a **valid sequence** to production and **absent** to the probe, which would count it out of the G2 contiguity check at `:250`. Today `trade_seq` runs around 296 million, so this is narrow — it is held safe by an *assumption* recorded in a comment (*"venue values are positive"*), not by shared code.

⚠ **I am not claiming the probe currently returns a wrong answer. I am claiming the gate and the thing it gates can disagree, and nothing asserts they agree** — which is the defect class exactly.

### 3.2 The three claims that hold — evidence

- **`ComputeSideLevels`:** verified at snapshot (`UI/MainForm_PlaintextSnapshot.vb:160-161`), card (`UI/MainForm_Render_Cards.vb:1064-1065`), CSV (`AnalysisLogger.vb:177-178`), Step 5b (`Core/ScoringEngine_Calculate_Verdict.vb:208-209`), live tracker (`LivePerformanceTracker.vb:1141-1142`), backtest writer (`tools/BacktestRunner/BacktestRowWriter.vb:85-86`), what-if replay (`tools/WhatIfRunner/WhatIfReplay.vb:167-168`), plus the payload inside `SignalEmitter` itself. `tools/CeilingAudit` does not link `SignalEmitter.vb` and does not need to — it reads the logged `Placed*` columns, which are that seam's output.
- **`EffectiveMinMovePct`:** all five claimed consumers verified, plus a sixth the claim omits — `tools/AutoTweaker/AutoTweakerCore.vb:95`.
- **`TradeStoreWriter`:** explicitly linked by `BacktestRunner.vbproj` and `OrderCheck.vbproj`; the root project globs it. `AutoTweaker`, `CeilingAudit` and `WhatIfRunner` do **not** link it — consistent with the precedent recorded on 2026-08-08, no change.

---

## 4. Sweep 4 — completeness vs existence

**Found clean, and this is a real negative result worth recording.** The subsystem that produced instance 3 has since learned the lesson, and the current code shows it.

`LivePerformanceTracker.vb:198-253` decides "do I have the OHLC data?" with **three** layers, not one:

1. A trailing-edge fetch from the newest bar forward (`:230-239`).
2. **An interior gap scan** — Step 1.5, which is the completeness half.
3. **A row-count-against-unique-keys check** (`:215-228`): if the file has more rows than the dictionary has keys, it rewrites from the de-duplicated dictionary.

That third check is the 2026-07-31 lesson applied in code — *"all three were found by counting rows"*.

`TradeStoreGapRepair.vb:88-110` passes `clampToSegStart:=True`, and the clamp is **documented as deliberate**: Deribit refuses windows past its ~24 h retention, so an unclamped resume after a long outage asks for a window the venue rejects.

**Known and not re-reported:** the coverage report's S3 longest-gap metric cannot see scattered per-trade loss. [`trader-tick-queue.md`](trader-tick-queue.md) §2 already carries it, and it rests on arithmetic rather than on the withdrawn 78.8 % figure.

---

## 5. Sweep 5 — fixture blind spots

**Scope:** 53 fixture families, 269 `Check()` assertions in `verify/ordercheck/Program.vb`.

### 5.1 S-7 — two shipped code paths that no fixture can reach

⚠ **`indicators.CVD.slope_pct_of_value` has no fixture that can exercise it — and it is one of the two known POCO drifts.**

`A1_CvdSlopeRising` (`:520-543`) is the only `CalcCVD` fixture. Its data is 30 sells at 20,000, then 15 balanced buy/sell pairs, then 30 buys at 20,000. That is **perfectly balanced**, so `cvdValue = 0`. The threshold is:

```
slopeThreshold = Math.Max(slopeMinUsd, absValue * slopePctOfValue)
```

With `absValue = 0`, the percentage arm is **identically zero at every possible value of `slopePctOfValue`**. The fixture cannot distinguish 0.01 from 0.10 from 100. **The input it never presents is an imbalanced book.**

⚠ **The entire dynamic-threshold branch of `CalcMicroCVD` has no fixture coverage.**

`Indicators_OrderFlow.vb:416-424` is gated on `If dynamicPct > 0.0`. Both MicroCVD fixtures — `A2_MicroCvdBullAccel` (`:567`) and `A3_MicroCvdWindowFromEnd` (`:590`) — omit the argument, so `dynamicPct = 0.0` and both take the **static-only legacy arm**. The shipped app runs 0.30 and **always takes the other arm.** The branch was added by `dynamic-microcvd-accel-proposal.md` (spec #3) and has never been executed by a test.

### 5.2 Stale but not currently masking anything — stated honestly

The brief's trap 4 warns against treating every difference as a defect. These are differences I checked and **will not call defects**:

- **`A6_ObvNormalisation`** pins `trendGate:=10.0` against a shipped 23.0. I computed the fixture's own arithmetic: 49 up-bars at volume 10 with mean volume 10 give `obvChange = 48`, which clears **both** gates. **A6 is not pinned at 10.0 to hide a failure.** It is a stale literal that happens not to matter for this assertion — but it is also the reason no fixture would ever notice a `trend_gate` drift, which is precisely how the 8.0 drift survived two months.
- **`A2_MicroCvdBullAccel`** still passes under the app's real config. Under `dynamicPct = 0.30` the effective threshold is 41,400 against a late-minus-early of 74,000, so the assertion holds either way. **Untested is not the same as wrong**, and I am not going to inflate it.
- **`A20a` / `A20b`** pass OFI thresholds explicitly at 2.0 / 0.5 — neither the method default nor the shipped 1.6 / 0.625. **This is legitimate.** Both are refactor-equivalence tests that compare `CalcOFI` against `ComputeOfiImbalance` + `ClassifyOfiRatio` on identical inputs. Any consistent threshold serves that purpose.

### 5.3 The brief's sharpest sweep-5 question

The brief asked for *"other fixtures whose data would catch a bug they are not pointed at"* — the `A53e` shape.

**I did not find another `A53e`.** What I found is the inverse and, for this audit, more actionable: **fixtures whose data structurally cannot reach the parameter they appear to cover.** `A1` looks like CVD slope coverage and cannot see `slope_pct_of_value` at all. That is a blind spot that reads as coverage, which is the harder kind to notice.

---

## 6. Proposed rows for `trader-tick-queue.md` §2

**Ordered by what each one prevents.**

### Row 1 — ⚠⚠ Route `WsTradeProbe` through the shared trade reader, before the probe is trusted

> **The probe gates the `TradeStoreWriter.vb:149` fix. Fix the gate before reading it.**
> `tools/WsTradeProbe/WsTradeProbeProgram.vb:203-204` uses private `ReadString`/`ReadLong` instead of `TradeRecord.ReadTradeId`/`ReadTradeSeq`. Sentinels differ (−1 vs 0), the validity rule differs (`n >= 0` vs `> 0`), and `trade_id` trimming differs. Link `DeribitClient.vb` into `WsTradeProbe.vbproj` — or, if the probe must stay dependency-free, state that in a comment and pin the two readers' agreement in a fixture.
> ⚠ **If the probe has already been run, its G2 output should be re-read after this lands.**
>
> **Model: Sonnet. Effort: medium.** The judgement is done and recorded here; the change is a project reference plus two call swaps. **Where it slips:** the temptation to "just make the probe match" by editing its sentinel to −1 — that reproduces the copy instead of removing it. **Escalation trigger:** if linking `DeribitClient.vb` drags a `HttpClient` dependency into the probe, stop — that is the same split that put `TradeStoreWriter` in `Core/`, and it needs the same treatment, not a workaround.

### Row 2 — ⚠ Widen `A54a` from two copies to three

> **`A54a` as queued pins POCO against JSON. That would not have caught the copy that is still wrong.** After v66, `OBV.trend_gate` reads 23.0 in JSON and POCO, and **10.0** in the method default (`Core/Indicators_Structure.vb:44`) and the fixture literal (`verify/ordercheck/Program.vb:661-662`).
> Scope it by the rule this audit derived, not by a hand-listed key set: **a key with a concrete POCO default can drift; a `Double?` = Nothing nullable override cannot.** Guard the first group. That rule is why v40, v41 and v48 produced no drift while v33, v34, v36 and v58 did.
> Full three-way table in this document §1.4; 11 of 42 currently disagree.
>
> **Model: Sonnet. Effort: medium.** Mechanical against the `A52a` template. **Where it slips:** matching on key name alone — 57 POCO classes share `period`, `enabled`, `window_size`, and a naive matcher gave me 10 false positives out of 12. Match class → JSON block → key. **Escalation trigger:** if the guard cannot see the method `Optional` default without reflection or source parsing, stop and re-scope — a guard covering only two of the three copies is the thing being replaced.

### Row 3 — ⚠ Re-sync or retire the seeded session buckets

> `Core/Settings/EngineSettings.vb:668-671` seeds ASIA/LONDON/NY with a comment reading *"aligned to live v30"*. Live is v66. ASIA `high_multiplier` 0.8 vs 1.00, ASIA `mid_multiplier` 0.85 vs 1.00, ASIA and LONDON `execution_resolution` 1 vs 3. NY is clean.
> ⚠ **Decide the intent first, and it is a real decision, not a typo fix.** The comment says the seed exists so *"an empty default would not silently skip all session scaling on the code-defaults path"*. Either that path matters — in which case the seed must be re-synced **and guarded**, or it does not — in which case an empty list is honest and the seed should go. Re-syncing without picking one just resets the clock.
>
> **Model: Sonnet. Effort: medium.** Small change, but the decision above wants stating in the commit message.

### Row 4 — Key the eval-cache backfill on identity, and fix the loop

> `LivePerformanceTracker.vb:431` guards on a 1-second timestamp; `:358` builds the set once and never updates it inside the loop, so a cold start admits same-second duplicates while a warm start drops one.
> **Latent — measured zero duplicate timestamps in both the local book (10,779 rows) and the AWS copy-back (15,499 rows).** The CSV already carries `InstanceId` and `SignalId`; the identity exists and is unused.
> ⚠ **Not urgent, and it should not be bundled with a CSV rotation** — it needs no schema change.
>
> **Model: Sonnet. Effort: medium.** **Where it slips:** "just add the row to `existingTs` inside the loop" fixes the batch inconsistency and leaves the key wrong. Both halves, or say why only one.

### Row 5 — Bound the pooled minute-key dedup before the next pooled read

> [`aws-collector-deploy-checklist.md`](aws-collector-deploy-checklist.md) §3b specifies an AWS-preferred **minute-key** dedup. Measured against the books in hand: it would drop **548 local rows (5.08 %)** and **25 AWS rows (0.16 %)**, and ⚠ **the AWS losses cluster at 13:00 UTC**, the NY `execution_resolution` 3 → 1 handover. A session-correlated loss is not noise.
> The Kelly dated trigger and the W6-4 re-run are both scheduled to consume one pooled freeze. **Second-resolution keys, or `InstanceId` + timestamp, cost nothing extra here.**
>
> **Model: Sonnet. Effort: low.** A procedure edit plus a re-measure. **Not a code change.**

### Row 6 — Two doc corrections found in passing

> - `docs/DeribitIndicatorProject.md` §4 (Tier 2 table) names `cfg.Indicators.Spread.WidePenaltyThresholdBps`. **No such property exists** — it is `WideThresholdBps`. Anyone grepping the documented name finds nothing.
> - `tools/CeilingAudit/CeilingAuditProgram.vb:79` holds `expectedVersion = 59` against live **v66**. [`trader-tick-queue.md`](trader-tick-queue.md) §2 calls this "six versions stale"; **it is now seven.**
>
> **Model: Haiku. Effort: low.** Both trivial. Blocks nothing.

---

## 7. Recommended fixtures — A54 and A55 are taken, so these start at A56

**I did not write any of these.** Named so a builder does not have to re-derive them.

| Family | What it pins | Notes |
|---|---|---|
| **A56a** | Every parse site reads `trade_id` / `trade_seq` through `TradeRecord.ReadTradeId` / `ReadTradeSeq` and returns identical values for the same `JsonElement` | Include `WsTradeProbe` once it is linked. ⚠ Must include the **absent**, **whitespace**, **negative** and **zero** cases — those are exactly where the copies diverge today |
| **A56b** | Three-way agreement: method `Optional` default ≡ POCO default ≡ shipped JSON, for every key with a concrete POCO default | The `A54a` widening. Nullable overrides are **out of scope by construction** — assert that too, so the exclusion is deliberate rather than forgotten |
| **A56c** | `CalcCVD` slope with an **imbalanced** book, so `absValue × slopePctOfValue` is non-zero | ⚠ Closes the `A1` blind spot. Fails today if `slopePctOfValue` is wired from a `New EngineSettings()` cfg |
| **A56d** | `CalcMicroCVD` with `dynamicPct > 0`, exercising `Indicators_OrderFlow.vb:416-424` | The dynamic branch has never run in a test. Pin the `Math.Max(dyn, floor)` arm on both sides |
| **A56e** | Eval-cache backfill: two CSV rows sharing one second are both retained, from a **cold** and a **warm** cache | ⚠ Must fail on current code in both states before the fix — the states disagree today |
| **A56f** | The seeded `SessionVolumeSettings.Sessions` match shipped JSON, or the seed is empty | Whichever Row 3 decides. Assert the decision, not the values alone |

⚠ **Carry the trade-identity build's discipline into these: prove the teeth by mutation.** That build injected each trap and confirmed the matching fixture failed. `A56c` and `A56d` in particular will pass trivially if written carelessly, because both currently-omitted parameters have *plausible* defaults.

---

## 8. ⚠ What I did NOT check

**Stated plainly, because a bounded audit that names its boundary beats an unbounded one that implies it looked everywhere.**

1. **I did not run the harness or build anything.** Read-only, per the brief §4. So every claim about a fixture's behaviour is **derived from reading the code and doing the arithmetic by hand**, not observed. The three I computed — `A6` (`obvChange = 48`), `A1` (`cvdValue = 0`), `A2` (effective threshold 41,400 vs 74,000) — are arithmetic on constants in the fixture source, and I show the working so it can be checked.
2. **I did not read `docs/trader-profile.md`.** The `crypto-trading-context` skill points to it. This audit proposes no indicator, threshold or scoring change, so the preference list is not load-bearing here. Naming it because the skill says to read it.
3. **I read `docs/DeribitIndicatorProject.md` §1–§6 and §15 only, not in full.** It measures **47K tokens**, not the "~24K, measured 2026-08-02" that `CLAUDE.md` step 1 claims. ⚠ **That figure is stale again, by roughly 2×, and `CLAUDE.md`'s own note predicts this exact failure** — §15 has re-grown and wants a trim to the archive. I used `settings.json`'s `change_log` and `git log -S` as the authority for version history instead, which is stronger evidence than the summary table anyway.
4. **Sweep 2 was not exhaustive.** I swept on the vocabulary of the class — `dedup`, `_last*`, `lastSeen`, `monotonic`, `resume cursor`, `HashSet(Of` — unanchored, and worked the named starting points. **A guard that uses none of those words would not appear.** Specifically unexamined: `SettingsDiffApplier`, `TweakerState`, `OverlapValidator`, and the `analysis/` joiners.
5. **`CoverageReport` was not swept directly.** The brief listed it. I checked its two recorded issues are still recorded and moved on, because the queue already carries both and the brief said not to re-report known items. **If a fourth instance is hiding there, I did not look.**
6. **I did not verify the 11 method-default drifts against the `.claude/worktrees/compassionate-lamarr-b0e155/` copy.** It is a stale worktree, excluded from every grep. It contains a divergent `Program.vb`. **Worth someone confirming it is abandoned** — it is not the tree, but it is on disk and greppable.
7. **I did not check the auto-tweaker's writable surface** against the drifted keys. `AutoTweaker` is the only surface that writes `settings.json`. It is recorded as never having fired, so a drift there is latent — but I did not confirm the drifted keys are fenced.
8. **No dataset boundary analysis.** Nothing here proposes a scoring change, so none was needed.

---

## 9. Method note, kept because the brief asked for it

The brief set the standard: *"a ready explanation for a surprising result is a warning sign, not a resolution."*

Applied twice, and it changed the answer both times:

- **A naive POCO↔JSON key matcher gave 12 hits.** I did not report them. Class-scoping the walk left 6 genuine ones. The brief predicted 10 of 12 would be false; the mechanism was exactly as described.
- **My first path guess for `spread.wide_threshold_bps` produced a false "key absent in JSON".** The ready explanation was "another drift". The real one was that I guessed the key name from the project doc, **and the project doc is wrong** — which turned a false finding into a true one about the documentation.

And one place it cut the other way: I expected `A6_ObvNormalisation` to be pinned at 10.0 *because* 23.0 would fail it. That is a tidy story and it is false — the arithmetic clears both. **Reported as stale-not-broken.**

---

## 10. Closing judgement

**The class is not handled, and this audit should not be read as saying it is.**

The strongest evidence is not any single row above. It is that **v66 fixed a POCO drift "in lockstep" on 2026-08-11, and on the same day left two of the four copies of that value at the old number** — while a brand-new tool committed the same week reintroduced a parse copy that a documented claim says does not exist.

**The class reproduces faster than it is being repaired.** The two rows that change that are Row 1 and Row 2; the rest are cleanup.
