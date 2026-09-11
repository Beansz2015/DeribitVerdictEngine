# `D-6d` — the absorption counting gap · SPEC

> ## ⛔ STATUS: **NOT BUILT. NOT TICKED.** The §7 D-table is owed by the trader.
>
> **Written 2026-09-11 (UTC).** `D-6d` is a decision row in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 — **the only part of that D-table that is neither ruled nor data-gated.** It was raised 2026-09-01 by [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §2 and given **no row of its own**, which is why every summary of that table reads CLOSED.
>
> ⛔ **DO NOT HAND THIS TO AN IMPLEMENTER UNTIL §7 IS TICKED.**
>
> ⚠ **A DATE GATE BINDS BEFORE ANY BUILD.** `D-2`'s cell in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 reads ***"Do not build against this row before ~2026-09-15"*** — `D-1`'s post-ship read stands at **2 weekdays of a ruled ~10**. **Stage 1 of this spec does not touch `D-2` and is not blocked by that gate; Stage 2 is.**

---

## 0. Implementer brief — model, effort, and where it slips

**Model: Opus · Effort: HIGH · Stage 1 and Stage 2 are SEPARATE SESSIONS.**

**Why that tier.** This is a **state-machine defect on a dual-fed path under one `MarketState` lock**. The trade fold and the book fold run at different cadences against shared per-side state, and the defect is *when* that state is live, not what it computes. [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §0 already names those fold paths as where a change slips, and [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §2 independently sized `D-6d` at **Opus, high**. This spec does not lower that.

⛔ **Where it will slip — three concrete traps, not a general warning.**

| # | Trap | Why a fixture will not save you |
|---|---|---|
| **T-1** | ⛔⛔ **Letting `Press` survive an episode close while `SizeStart` restarts.** `absorbRatio = aggrUsd / max(SizeStart − SizeMin, floor)`. A numerator that spans two episodes over a denominator that spans one **inflates the ratio and produces more flags that mean less** | **This reads as SUCCESS.** Flag rate rises toward the 3–8 % design band and looks like the fix working. See §6 — it is the single thing this spec forbids outright |
| **T-2** | **Instrumenting the close and believing you measured the loss.** The dropped flow is dropped at [`Core/LevelAbsorptionTracker.vb:170`](../Core/LevelAbsorptionTracker.vb) (`If Not side.Active Then Return`) — **after** the close, while the side is idle. A close-time log cannot see a single USD of it | A close-log fixture passes cleanly and measures the wrong event |
| **T-3** | **Writing the shadow accumulator into `PressSum`.** The measurement must never reach `absorbRatio`, or Stage 1 stops being behaviour-neutral and becomes an undeclared live change | `scoring_enabled` is `false`, so nothing goes red. The live strip would move silently |

⛔ **Escalation trigger — carried verbatim from [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) §2 because it is still the right one:** ***if the flagged rate rises while the ratio distribution shifts left, STOP.*** **That is `T-1`, not a fix.** Add one more, specific to Stage 1: **if Stage 1's measured counting gap does not land within roughly 20–45 %, the §2 diagnosis is wrong** — stop and re-derive rather than proceeding to Stage 2.

**Session split, sequenced by dependency and not by size.**

| Session | Scope | Effort |
|---|---|---|
| **Stage 1** | The instrument only. **Zero behaviour change.** §4 | **Opus, high** — the shadow predicate must mirror the live one exactly or it measures nothing |
| **read** | ~2 weekday-weeks of `absorption_episodes.log`, then §5's decision | — |
| **Stage 2** | The fix §5 selects. **Gated on Stage 1's read AND on `D-2`'s ~2026-09-15 date gate** | **Opus, high** |

---

## 1. What `D-6d` is

**Measured in [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §5.2(b)**, replaying the tracker's own predicate over the `trade_seq`-verified 100.000 %-complete tape era (134,204 rows), at an alignment control validated to **+2 s** where all 22 logged-pressed rows reproduce:

> **The engine logs `aggrUsd > 0` on 22 rows where its own band-and-window definition admits qualifying in-band flow on 72. It counts 31 % of what it should.**

**It is not a window-length problem — it is the same 10-second window on both sides of that comparison.**

| Fact | Value |
|---|---|
| The gap | **50 rows** |
| Of those, had a break-tolerance print to justify closing | **21 (42 %)** — the engine was right |
| ⛔ **Had NO break-tolerance print at all** | **29.** The level held for the whole window and the tracker recorded nothing |
| Group-B unlogged flow | p50 **6,800 USD** · p75 18,910 · p90 51,280 |
| ⛔ **Rows carrying ≥ 20,000 USD** (`default.min_aggr_usd`) | **13 of the 50** — against just **19 rows clearing `min_aggr_usd` in the entire 17-weekday AWS book** |

⭐ **That last line is why `D-6d` outranks the other two causes: recovering it would roughly DOUBLE the population that reaches the money gate at all.**

⚠ **42 % is an UPPER bound on the legitimate half** — [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §5.3 records that the group-B break control tested *presence* of a break print in the window, never whether it arrived **before** the flow.

---

## 2. The mechanism — read out of the code, 2026-09-11

⛔ **`aggrUsd` does not measure what its own definition says. The press window is not `window_sec`. It is `min(window_sec, age of the current episode)`.**

**Three statements compose to produce that, all in [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb):**

| Line | Statement | Consequence |
|---|---|---|
| **`:170`** | `If Not side.Active Then Return` — the first line of `FoldTradeSide` | A print arriving while the side is idle is **DROPPED, not deferred.** Nothing records it |
| **`:117`** | `Press.Clear() : PressSum = 0.0` inside `CloseEpisode()` | **Every close wipes the whole accumulated window**, however much flow it held |
| **`:315`** | `side.Press.Clear() : side.PressSum = 0.0` on episode open, followed by `Return` | The opening snapshot **wipes again and counts nothing** |

**So a side that closes and reopens starts its 10-second window from zero, every time.**

### 2.1 ⭐ The arithmetic reconciles three independent measurements

| Source | Reading |
|---|---|
| `D-1`'s shipped instrumentation (2 weekdays, in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6 `D-2` cell) | median `AbsorptionEpisodeSec` = **1.7 s** |
| Shipped `settings.json` `indicators.absorption.window_sec` | **10 s** |
| Naive expected counting rate, 1.7 ÷ 10 | **17 %** |
| ⭐ **Measured counting rate** ([`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §5.2(b)) | **31 %** |

**Same order, and the direction of the residual is the one a right-skewed duration distribution predicts** — the mean episode outlives the median, and flow is not uniform in time. ⚠ **This is ARITHMETIC CONSISTENCY, NOT PROOF.** It is the reason Stage 1 exists.

### 2.2 ⭐⭐ And it explains a result `D-2` already recorded without explaining

[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §6's `D-2` cell says the first `D-1` reading *"points AGAINST §4.1 — median `EpisodeSec` 1.7 s, so episode-cumulative would SHORTEN the span, not extend it."*

⛔ **The mechanism above says something sharper: on the median episode, `D-2` IS A NO-OP.** `PressSum` is already the sum over `min(10 s, episode age)` because `CloseEpisode` clears the queue. **Episode-cumulative and 10-second-rolling are the same number on every episode younger than 10 seconds — which is most of them.** `D-2` can only bind on episodes that outlive the window.

⭐ **So `D-6d` is not merely larger than `D-2`. It is `D-2`'s precondition.** Widening or re-basing the window buys nothing while the queue is wiped every 1.7 s. **Sequence `D-6d` first.**

### 2.3 ⚠ Why episodes are short is NOT KNOWN, and that is the whole problem

**There are SIX ways a side stops being `Active`, and NOTHING RECORDS WHICH ONE FIRED.**

| # | Path | Anchor in [`Core/LevelAbsorptionTracker.vb`](../Core/LevelAbsorptionTracker.vb) | Legitimate? |
|---|---|---|---|
| **1** | Degenerate ladder — no asks or no bids; **both** sides closed | `:232` | Yes |
| **2** | Level re-map — `lvl <> side.LevelPrice`, an **exact `Double` compare** against a per-run re-carried level | `:276` | ⚠ Depends on jitter |
| **3** | ⭐ **Proximity gate shut — INCLUDING `lvl = 0`, i.e. the level left the VISIBLE ladder span** | `:278`, gate at `:262`, span test at `:366` | ⛔ **The suspect** |
| **4** | Break-through via the opposing touch | `:284`–`:294` | Yes |
| **5** | Break-through via a trade print | `:179`–`:193` | Yes — this is the measured 42 % |
| **6** | `Reset()` on WS reconnect | [`DeribitWsFeed.vb:284`](../DeribitWsFeed.vb) | Yes |

⭐⭐ **WHY PATH 3 IS THE SUSPECT, and it is already half-documented.** `NearestAbove` / `NearestBelow` (`:366`) admit a candidate level **only while it sits inside the visible ladder span** — and the feed subscribes **`book.BTC-PERPETUAL.none.10.100ms`** ([`DeribitWsFeed.vb:27`](../DeribitWsFeed.vb)), **ten levels, ten times a second**. A level at the edge of that span can flicker in and out at **10 Hz**, and each disappearance drives `lvl = 0` → `gateOpen = False` → `CloseEpisode()` → **press queue wiped**.

⚠ **[`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) §4.3a already carries the qualifier — *"`:42` enforces `min(proximity, visible ladder span)`. 0.30 is a ceiling, not always the operative distance."*** ⛔ **That note recorded the ceiling. Nobody has measured how often the ladder is the binding term, and it cannot be measured from stored data** — [`absorption-d6-spec-back.md`](absorption-d6-spec-back.md) handle `H-4` establishes that **no order-book depth is stored anywhere**.

⛔ **So the fix is not derivable from the book. It needs live instrumentation. That is Stage 1.**

---

## 3. Rulings this spec makes, so §7 is a short D-table and not a long one

| # | Ruling |
|---|---|
| **R-1** | ⛔ **TWO STAGES, NOT ONE.** The diagnosis in §2 is inference from code plus two measurements. Building a fix on it directly would be the *"mechanism read from a config file is a hypothesis"* error [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §6.3 already charged once, in this same feature |
| **R-2** | ⛔ **The instrument measures the DROP, not the CLOSE.** Trap `T-2`. The loss happens at `:170` while the side is idle; a close-time record is blind to it |
| **R-3** | **Stage 1 is behaviour-neutral by construction** — a separate accumulator, never `PressSum`, never `absorbRatio`, never a rendered field. `scoring_enabled` stays `false` throughout both stages |
| **R-4** | **Fixture family `A78`** — measured free at `828d868` (`A77e` is high-water). No new hard constraint: no `settings.json` key is added by Stage 1 |
| **R-5** | ⛔ **`D-2` is RE-SEQUENCED BEHIND `D-6d`, not cancelled.** §2.2 shows `D-2` is a no-op on the median episode. It becomes measurable only after episodes survive |
| **R-6** | **The engine display-string parity rule DOES NOT FIRE for Stage 1.** The live strip renders `ABS↑ <level> (<ratio>×)` from `AbsorptionSignal` / `AbsorptionLevel` / `AbsorptionRatio` only ([`UI/MainForm_LiveStrip.vb:270`](../UI/MainForm_LiveStrip.vb)). Stage 1 adds no field any surface reads. ⚠ **State this in the commit message; do not leave it to be inferred** |

---

## 4. Stage 1 — the instrument

**Goal: measure the counting gap live, and attribute it to one of the six paths in §2.3.**

### 4.1 The shadow-press accumulator — the measurement that matters

- `SideState` gains **`LastLevelPrice As Double`** — set at episode open beside `LevelPrice`, and ⛔ **deliberately NOT cleared by `CloseEpisode()`**, so an idle side still knows what it was watching.
- `SideState` gains **`ShadowPressUsd As Double`** and **`ShadowPressCount As Integer`**.
- `FoldTradeSide` ([`Core/LevelAbsorptionTracker.vb:170`](../Core/LevelAbsorptionTracker.vb)) replaces its bare early-out. When `Not side.Active` **and** `LastLevelPrice > 0`, it runs **the identical band / aggression / break-tolerance predicate against `LastLevelPrice`** and accrues to the shadow pair. **Then returns.**
- ⛔ **The shadow path must reuse the SAME predicate expression as the live path, not a copy of it.** Extract it once as a `Friend Shared Function` taking `(level, price, band, breakTol, isBuy, isAbove)` and call it from both arms. **A second copy is the fixture-literal provenance failure in executable form** — it drifts the first time the live predicate moves, and the instrument then silently measures a different thing than the engine does.

⭐ **Then `ShadowPressUsd / (PressSum + ShadowPressUsd)` is the counting gap, measured live, per run, directly comparable to the replay's 31 %.**

### 4.2 Close-reason attribution

- A `Friend Enum AbsorptionCloseReason` with the six §2.3 members: `DegenerateLadder` · `LevelRemap` · `ProximityShut` · `LadderSpanLost` · `BreakThrough` · `Reset`.
- ⭐ **`ProximityShut` and `LadderSpanLost` are SEPARATE members even though both reach `:278`.** They are distinguished by whether `lvl = 0`. **Collapsing them destroys the answer this whole stage exists to get** — path 3 splits into a legitimate half (price genuinely left) and the suspect half (the level fell off a ten-deep ladder).
- `CloseEpisode()` takes the reason as a required parameter. **Required, not `Optional`** — an `Optional` default is the third copy of a value and every call site is being edited anyway.
- Per-reason counters on the tracker, plus **press-USD-discarded-at-close per reason**, both reset at each read.

### 4.3 Where it is written — a sidecar, not a CSV column

**New `Core/AbsorptionEpisodeLog.vb`, modelled on [`Core/VenueStatusLog.vb`](../Core/VenueStatusLog.vb):** never throws, host-agnostic, append-only at `AppDomain.BaseDirectory + "absorption_episodes.log"`.

- **ONE LINE PER RUN**, written where `Snapshot()` is read — **not one line per close.** At a ~1.7 s median episode life a per-close log runs to roughly **100k lines/day ≈ 360 MB/month** on a t2.micro whose trade store is already 78 MB/month. **One line per run is ~1,400 lines/day.**
- Each line carries **`InstanceId` and `SignalId`** — ⭐ **that pair is the CSV's own attribution identity and `S-4` (commit `1aeae5a`) just made it the eval-cache dedup key, so the join back to `analysis_log.csv` is EXACT and this sidecar gives up nothing a column would have given.**
- Plus, per side: `PressSum`, `ShadowPressUsd`, `ShadowPressCount`, `EpisodeSec`, and the six close counters with their discarded USD.
- ⛔ **A sidecar forces NO `analysis_log.csv` header rotation, which matters beyond convenience** — [`trader-tick-queue.md`](trader-tick-queue.md) §3 parks **five riders** on the next rotation under a standing ***"NEVER FORCE ONE"***. See §7 `D-6d.1`, and the §10 finding about the rotation that already went past.

### 4.4 What Stage 1 must NOT do

- ⛔ Not touch `PressSum`, `SizeStart`, `SizeMin`, `PullLB`, `PostLB`, `absorbRatio` or any `AbsorptionRead` field.
- ⛔ Not change when an episode opens or closes. **Only record why.**
- ⛔ Not add a `settings.json` key. **Settings stays at v68.**

---

## 5. Stage 2 — the fix, selected by Stage 1's read

⛔ **Do not pre-commit to one of these. Stage 1 chooses.**

| Option | Fires when Stage 1 shows | Shape | Risk |
|---|---|---|---|
| **F-1 Ladder hysteresis** | `LadderSpanLost` dominates the lossy closes | Hold the episode across N snapshots / M ms of the level being invisible, rather than closing | ⚠ **`SizeNow` / `SizeMin` cannot be measured while the level is invisible.** The spec must say freeze or skip — a stale trajectory is a silent lie |
| **F-2 Re-map tolerance** | `LevelRemap` dominates | Replace the exact `Double` compare at `:276` with a tolerance | ⚠ Too loose and two genuinely different levels bleed into one episode — the thing `:276` exists to prevent |
| **F-3 Deeper book** | `LadderSpanLost` dominates **and** F-1's stale-trajectory problem proves unfixable | Subscribe `book.<i>.none.20.100ms` | ⛔ **Widest blast radius — OFI reads the same ladder.** Its own spec, not a rider on this one |
| **F-4 Nothing** | `BreakThrough` dominates | `D-6d` is smaller than believed. **Record it and redirect to the geometry cause** | ✅ **A legitimate outcome.** Say so plainly; do not manufacture a fix |

---

## 6. ⛔⛔ THE INVARIANT THAT GOVERNS EVERY STAGE-2 OPTION

> ## THE NUMERATOR AND THE DENOMINATOR MUST COVER THE SAME SPAN.

`absorbRatio = aggrUsd / max(SizeStart − SizeMin, depletion_floor_usd)`. **`SizeStart` is sampled at episode open** ([`Core/LevelAbsorptionTracker.vb:311`](../Core/LevelAbsorptionTracker.vb), the `D-6a` arm-early / measure-tight baseline).

⛔ **So the obvious-looking fix — "just let `Press` survive the close" — is FORBIDDEN.** It gives a numerator spanning two episodes over a denominator spanning one. **The ratio rises, the flag rate climbs toward the 3–8 % design band, and it is pure artefact.**

⭐ **This is the same argument that killed collapsing the proximity shells in `D-6a`, and it should be recognised as the same argument:** *"you would get more flags by weakening the measurement, which is the pattern [`trader-profile.md`](trader-profile.md) rejects."* **`D-6d`'s tempting fix fails for the identical reason.**

⭐ **The legitimate shape is therefore to fix EPISODE CONTINUITY, never the press queue.** If an episode is not spuriously closed, `Press` and `SizeStart` survive **together** and the ratio stays honest by construction rather than by care.

**Escalation trigger, restated so it cannot be missed: flagged rate UP while the ratio distribution shifts LEFT ⇒ STOP. That is the artefact.**

---

## 7. D-table — owed by the trader

| # | Decision | My read |
|---|---|---|
| **`D-6d.1`** | **Sidecar `absorption_episodes.log`, or five new `analysis_log.csv` columns?** | ⭐ **(a) SIDECAR.** ⛔ **Reserved rather than auto-proceeded, because the CSV is the richer-sounding option and "a rotation is expensive" is the exact tell `CLAUDE.md`'s three-step test names.** **My reason is mechanical, not economic: a CSV column can carry ONE scalar per run, where the loss is a distribution over six close reasons across two sides. The column is not richer — it is narrower. Carrying `InstanceId` + `SignalId` makes the sidecar join losslessly, so nothing is given up.** ⚠ **And a rotation drags in five parked riders under a *"NEVER FORCE ONE"* rule** |
| **`D-6d.2`** | **Does Stage 1 ship alone and read for ~2 weekday-weeks, or do both stages ship together?** | ⭐ **(a) ALONE.** The §2 diagnosis is inference. **[`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §6.3 already charged one wrong mechanism-from-code hypothesis in this exact feature, and its direction was inverted** |
| **`D-6d.3`** | **Does `D-2` (`window_sec` → episode-cumulative) stay scheduled for ~2026-09-15, or move behind `D-6d`?** | ⭐ **(b) MOVE BEHIND.** §2.2: on the median 1.7 s episode `D-2` is arithmetically a **no-op**. Building it first spends a session to move nothing and adds a dataset boundary for it |
| **`D-6d.4`** | **`ShadowPressUsd` — measurement only, or eventually the numerator?** | ⭐ **(a) MEASUREMENT ONLY, permanently.** ⛔ **Promoting it is trap `T-1` wearing a different hat** — shadow flow accrued while no episode was live has **no matching denominator at all.** Recorded here so a future seat does not rediscover it as an idea |

⚠ **`D-6d.3` is the one with a live consequence for your 15th-of-September framing.** Ticking (b) means the absorption build that starts then is **Stage 1 of this spec**, not `D-2`.

---

## 8. Fixtures — family `A78`

⛔ **Every one mutation-proved, with the actual output pasted. A prediction of what a mutation would do is not a mutation run** — `CLAUDE.md`'s standing rule, and [`absorption-instrumentation-spec-back.md`](absorption-instrumentation-spec-back.md) records two fixtures in this same feature that were built to a spec's own worked description and could not observe the mutation that spec named.

| Fixture | Asserts | Required mutation |
|---|---|---|
| **`A78a`** | ⭐ **The defect itself.** Press accrues, the episode closes, the side reopens, and `PressSum` restarts at zero while `ShadowPressUsd` holds the flow that arrived in between | Remove the shadow arm ⇒ the gap is invisible, both read 0 |
| **`A78b`** | ⭐⭐ **The predicate is ONE expression, not two.** Move the live band test by one tick and the shadow test moves with it | Give the shadow arm its own copy ⇒ `A78b` fails alone |
| **`A78c`** | `LadderSpanLost` and `ProximityShut` are **distinguishable** — a level pushed outside the visible ladder span increments the first, price genuinely leaving increments the second | Collapse the two enum members ⇒ fails alone |
| **`A78d`** | ⛔ **Behaviour neutrality.** Over a scripted fold sequence, every `AbsorptionRead` field is **byte-identical** with the instrumentation present and absent | Let the shadow accumulator touch `PressSum` ⇒ fails alone. **This is `T-3`'s only guard** |
| **`A78e`** | `LastLevelPrice` **survives** `CloseEpisode()` while `LevelPrice` is zeroed | Clear it in `CloseEpisode` ⇒ the shadow arm silently measures nothing forever |
| **`A78f`** | The sidecar never throws on a locked or unwritable path, and one read emits exactly one line carrying `InstanceId` + `SignalId` | Remove the `Try`/`Catch` from the append ⇒ `IOException` escapes |

⚠ **`A78a`'s SHAPE is the one to get right, and the standing memory says why:** a fixture built from a spec's *description* of a trap can be structurally incapable of producing it. **`A78a` must drive a real close between the two press batches — not simulate one by calling the accumulator directly.**

---

## 9. Acceptance — Stage 1

1. Solution + `AutoTweaker` + `WhatIfRunner` + `CeilingAudit` + `BacktestRunner` + `OrderCheck` Release `-t:Rebuild` **0 errors 0 warnings**, each run separately. ⚠ **`-t:Rebuild`, not incremental — an incremental build hid a `BC42109` in the `S-4` build.**
2. `tools/checks/verify-gate.ps1 -Mode local-fast` ⇒ **`GATE PASSED`**, harness **ALL PASS**.
3. Harness **376 → 382** (`A78a`–`A78f`).
4. `git diff --stat -- settings.json` is **EMPTY**. Settings stays **v68**.
5. Display-parity: the gate reports **`no snapshot/card drift detected`**, and the commit message states `R-6` explicitly.
6. ⭐ **A real run against the live collector emits at least TWO `absorption_episodes.log` lines spanning more than one run interval.** ⛔ **Two, not one — the v68 auto-run defect passed its own acceptance gate on a single row and the box had stopped collecting.** That precedent is in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15.
7. A version-history row in [`DeribitIndicatorProject.md`](DeribitIndicatorProject.md) §15, and the **BUILT banner written into THIS file in the SAME commit** — [`seat-handover-2026-09-09.md`](seat-handover-2026-09-09.md) §6's standing rule.

---

## 10. ⚠ What I did NOT verify

- ⛔⛔ **THE CENTRAL CLAIM IS INFERENCE, NOT MEASUREMENT.** §2 reads the code and reconciles it against two existing numbers (1.7 s median episode, 31 % counting rate). **I did not measure which of the six close paths actually fires, and it cannot be measured from stored data.** Stage 1 exists precisely because of this. **Do not let §2 be quoted as a finding.**
- ⚠ **The visible top-10 ladder span was NEVER measured against `0.30 × ATR`.** Path 3 is the suspect on a structural reading of `:366` plus the ten-level subscription at [`DeribitWsFeed.vb:27`](../DeribitWsFeed.vb) — **not on any observation.** If the ladder is routinely wider than the proximity shell, `F-1` and `F-3` are both dead and `F-2` or `F-4` wins.
- ⚠ **The 1.7 s median rests on TWO weekdays of a ruled ~10**, per `D-2`'s own cell. It is preliminary.
- ⚠ **I did not re-run the 2026-08-19 replay.** The 22 / 72 / 31 % figures are carried from [`absorption-blind-rederivation-2026-08-19.md`](absorption-blind-rederivation-2026-08-19.md) §5.2(b).
- ⚠ **Sidecar volume is estimated, not measured** — ~1,400 lines/day from the collector's ~38.3 rows/hour. The ~100k/day figure that rules out a per-close log is arithmetic from the 1.7 s median.

### 10.1 ⛔ A separate finding, surfaced here because this spec ran into it

**[`trader-tick-queue.md`](trader-tick-queue.md) §3's rider convention says the five parked riders *"attach to the next `analysis_log.csv` header rotation"*. A rotation HAPPENED — the absorption instrumentation build of 2026-09-01 added five columns and its own §15 row states `EnsureLogFile` rotates on that change. ⛔ NONE of the riders travelled.**

✅ **Verified in the tree at `828d868`, not carried:** `grep -c` on [`AnalysisLogger.vb`](../AnalysisLogger.vb) returns **0** for `TriggerMode`, **0** for `SettingsVersion` and **0** for `EffectiveSource`, and the `.bak` literal still reads `analysis_log.csv.v0.7.bak` at [`AnalysisLogger.vb:159`](../AnalysisLogger.vb).

⚠ **One of the five was skipped deliberately and says so** — the absorption row records the `.bak` name as *"left alone deliberately"*. **The other four carry no such note.**

⭐ **The lesson, and it is not about absorption: a rider that waits for an EVENT needs something to CHECK THE LIST when the event fires.** Nothing did. **This is out of scope for `D-6d` and belongs in its own row** — recorded here so it is not lost a second time.
