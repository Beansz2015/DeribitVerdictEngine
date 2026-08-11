# Implementer brief — the value-copy guard (A54a as ruled) + the probe parse site

**Status:** ✅ **BUILD-AUTHORIZED.** Both design decisions are RULED — [`trader-tick-queue.md`](trader-tick-queue.md) §0a, 2026-08-11. **Do not re-open them; §1 below is the ruling, not a proposal.**
**Evidence, for reference only — you do not need to read these to build:** [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md) (findings S-1, S-2, S-3, S-7) · [`seam-audit-decisions-second-opinion-2026-08-11.md`](seam-audit-decisions-second-opinion-2026-08-11.md) (the review that decided it).

---

## 0. Model + effort

> ### **Model: Sonnet. Effort: medium. ONE session, in the order below.**

**Why this tier.** Every design decision is finished and written down, twice, by two seats that disagreed and resolved it on measurements. The mapping problem that would have made this hard **was eliminated by the ruling** — option (d) never touches method parameters, so there is nothing to derive. What remains is a reflection walk against an in-repo template (`A52a`), nine fixture edits, and two call-site swaps. **No step requires a judgement you are being asked to make.**

**Where you will specifically slip — four traps, all concrete:**

1. ⚠ **Matching on key NAME alone instead of class → JSON block → key.** 57 POCO classes share `period`, `enabled`, `window_size`. A naive matcher produced **10 false positives out of 12** during the audit. This is the single most likely failure.
2. ⚠ **Getting the scope rule backwards.** A key with a **concrete POCO default** can drift and must be guarded. A key declared **`Double?` = Nothing** (the nullable "inherit" override) **cannot drift**, because it carries no competing value — guarding those produces a test that fires constantly on keys that are fine and stays silent on the ones that are not.
3. ⚠ **Applying the deletion bluntly.** `AppendFundingSample.maxAgeMs`, `CalcVWAP/CalcVWAPBands.nowUtc` and the `CalcSpread` thresholds at `LiveMicrostructureEvaluator.vb:135` are **genuinely internal conveniences with no settings counterpart. Leave them alone.** Making the `CalcSpread` ones required is actively misleading — the call site discards those values by design.
4. ⚠ **"Just making the probe match."** Editing `WsTradeProbe`'s sentinel from `0` to `−1` makes the two readers agree *today* and leaves two readers in the tree. **That reproduces the defect class instead of removing it.**

⚠ **The fixtures cannot be relied on to catch traps 1–3, because you write the fixtures too.** `A56c` and `A56d` in particular pass trivially if written carelessly — both currently-omitted parameters have plausible-looking defaults.

> ### ⚠ Escalation trigger — stop and move to Opus, high effort
> **Either condition:**
> - **The reflection walk needs a hand-written exception** to resolve a POCO property to its JSON path. **That means option (d) has quietly become option (a), which was ruled out** — the decision needs re-taking, not working around.
> - **Linking `DeribitClient.vb` into `WsTradeProbe.vbproj` drags an `HttpClient` dependency into the probe.** That is the same network/format split that put `TradeStoreWriter` in `Core/`, and it needs the same structural treatment.

**Prove the teeth by mutation, not by passing.** For each fixture, inject the defect it guards and confirm it fails. The trade-identity build set this precedent and it is the only thing distinguishing a real test from a green one.

---

## 1. What was ruled, and the one thing that is NOT fixed

**The problem.** Every indicator threshold exists in **four** copies, and nothing asserts they agree:

| # | Copy | Example (`OBV.trend_gate` after v66) |
|---|---|---|
| 1 | Method `Optional` default | `Core/Indicators_Structure.vb:44` — **10.0** |
| 2 | POCO default | `Core/Settings/EngineSettings.vb` — 23.0 |
| 3 | Shipped `settings.json` | 23.0 |
| 4 | Fixture literal | `verify/ordercheck/Program.vb:661-662` — **10.0** |

**11 of 42** method defaults disagree with shipped JSON. An 8.0 divergence on `trend_gate` survived **two months**.

**The ruling:**

- **Copies 2 ↔ 3 — guard them** with a reflection walk (§2). This is the fix.
- **Copy 1 — delete it**, scoped (§3). This is dead-code removal, **not** the fix.
- **Copy 4 — nothing here fixes it.** ⚠ See below. **Do not attempt to.**

⚠⚠ **Read this before you start, because it explains why the obvious approach was rejected.** The drift that motivated this whole exercise **did not hide behind an omission**. Fixture `A6` passes `trendGate:=10.0` **explicitly**. Deleting method defaults leaves it untouched. **Copy 4 is independent of copy 1, and copy 4 is the one that actually failed.**

**Copy 4 cannot be machine-guarded**, and this is settled, not open: `A20a`/`A20b` pass OFI thresholds at 2.0/0.5 — neither the default nor the shipped value — and that is **legitimate** (they are refactor-equivalence tests; any consistent value serves). `A6` pins 10.0 and that is **stale**. **The two are indistinguishable to a machine.** It is governed by a convention enforced in review:

> **A fixture asserting SHIPPED BEHAVIOUR derives its value from cfg. A fixture asserting MECHANISM passes a literal and says so in a comment.**

**You are not being asked to apply that convention across the suite.** Out of scope, §7.

---

## 2. Build part 1 — the guard (this is the fix)

**Compare `New EngineSettings()` against the deserialised shipped `settings.json`, by reflection, and assert equality.**

**Why this shape, so you do not re-derive it:**

- It **never touches method parameters**, so the parameter→key mapping problem does not arise. That mapping is genuinely underivable — `mtf_gate.adx_period` → `DmiPeriod`, `min_of` → `RequiredConfirms`, `candle_lookback` → `CandleCount`, and `mtf_gate` sits at JSON **top level**, not under `indicators`.
- It introduces **no new copy**: reflection reads the `JsonPropertyName` attributes **the serialiser itself uses**, so it consumes the real mapping rather than transcribing it. A hand-written table would be a fifth copy that can drift — which is why option (a) was rejected.

**Template:** `A52a` in `verify/ordercheck/Program.vb` already does exactly this shape for one key. Generalise it.

**Scope rule — apply it, do not hand-list keys:**

- **Guard:** every scalar property with a **concrete** POCO default that has a shipped JSON counterpart.
- **Exclude:** `Double?` = Nothing nullable overrides. ⚠ **Assert the exclusion explicitly** so it reads as deliberate rather than forgotten.
- **Exclude:** `version`, `last_modified`, `modified_by` — metadata, not behaviour.

**Two known-legitimate divergences the guard must tolerate or explicitly whitelist, with the reason recorded in the code:**

| Key | POCO | JSON | Why legitimate |
|---|---|---|---|
| `network.transport` | `"rest"` | `"ws"` | A cutover flag whose comment says *"P3 flips the default"*. P3 shipped; the flip never happened. Benign |
| `signal_bridge.enabled` | `false` | `true` | Safe-default-off for an emitter |

**Expected first run — if you get a different set, your walk is wrong:**

| Path | POCO | Shipped |
|---|---|---|
| `indicators.CVD.slope_pct_of_value` | 0.01 | 0.10 |
| `indicators.MicroCVD.accel_threshold_dynamic_pct` | 0.03 | 0.30 |
| `session_volume.sessions` ASIA `high_multiplier` | 0.8 | 1.00 |
| `session_volume.sessions` ASIA `mid_multiplier` | 0.85 | 1.00 |
| `session_volume.sessions` ASIA `execution_resolution` | 1 | 3 |
| `session_volume.sessions` LONDON `execution_resolution` | 1 | 3 |

**Then fix all six by moving the POCO to match shipped JSON.**

⚠ **The four session-bucket rows are a seeded `From { … }` collection at `Core/Settings/EngineSettings.vb:668-671`, and its comment says *"aligned to live v30"*. The tree is v66.** ⚠⚠ **RULED: re-sync the seed. DO NOT empty it.** The code-defaults path is real — `SettingsLoader.vb:44` initialises `New EngineSettings()` and the parse-error handler deliberately keeps it (`:460-461`), with the state announced at `UI/MainForm_Layout.vb:1898`. **Update the comment to say the guard now keeps it current, and delete the version reference** — a comment naming a version is the thing that went stale.

---

## 3. Build part 2 — delete copy 1, scoped

Remove the `Optional` default from every method parameter that **mirrors a settings key**, making it required.

**Measured, so you know the size before you start:**

- **Production call sites that omit one: 0.** Nothing in the app changes.
- **Fixture call sites that omit one: 9** — `CalcCVD` ×1 · `CalcMicroCVD` ×2 · `CalcMTFGate` ×2 · `CalcOBV` ×2 · `CalcTFI` ×2, all in `verify/ordercheck/Program.vb`.
- ⚠ **VB positional rules do not bite.** In every affected signature the settings-mirroring optionals already precede the trailing genuinely-optional parameters (`ByRef` outputs, `nowUtc`). **No signature reordering, no call-site churn.**

**For each of the nine, you must choose — and this is the only judgement in the build:** does the fixture assert **shipped behaviour** (derive the value from a cfg) or **mechanism** (pass a literal and comment it)? **Say which, in a comment, at each site.**

⚠ **See trap 3 for what to leave alone.**

---

## 4. Build part 3 — the fourth trade-parse site

`tools/WsTradeProbe/WsTradeProbeProgram.vb:203-204` reads `trade_id` / `trade_seq` with its own private `ReadString`/`ReadLong` instead of `TradeRecord.ReadTradeId` / `ReadTradeSeq`. That makes it a **fourth parse site** against a documented three-site claim.

**Verified divergences:** absent `trade_seq` ⇒ shared reader returns `AbsentSeq` = **−1**, probe returns **0** · shared reader rejects negatives, probe returns them · shared reader treats `n >= 0` as a real value, probe tests presence as `> 0` · `trade_id` whitespace trimmed vs returned raw.

**Fix:** link `DeribitClient.vb` into `WsTradeProbe.vbproj` and call the shared readers. See trap 4 and the escalation trigger.

⚠ **Bounded honestly, so you do not over-weight it:** the probe has already run and **S-1 had zero exposure** — the capture contains no `trade_seq ≤ 0`, and the gate's G1/G3 answers use `timestamp` only. **This is hygiene, not a correction to a result.** It rides here because it shares this session's mental model: *count the copies of a value.*

---

## 5. Fixtures — family A56

⚠ **A54 and A55 are taken.** A54 = this guard's original queue identity; A55 = the trade-store write-guard fix, a separate build.

| # | Pins | Notes |
|---|---|---|
| **A56a** | Every trade-parse site reads `trade_id` / `trade_seq` through the shared readers and returns identical values for the same `JsonElement` | ⚠ Must include the **absent**, **whitespace**, **negative** and **zero** cases — that is exactly where the copies diverge |
| **A56b** | ⭐ **The guard itself** — POCO default ≡ shipped JSON for every concrete-default key, plus an explicit assertion that nullable overrides are excluded **by design** | The main deliverable |
| **A56c** | `CalcCVD` slope with an **imbalanced** book, so `absValue × slopePctOfValue` is non-zero | ⚠ Closes a blind spot that **reads as coverage**: `A1`'s data is perfectly balanced, so `cvdValue = 0` and the percentage arm is identically zero **at every possible threshold value**. `A1` cannot distinguish 0.01 from 100 |
| **A56d** | `CalcMicroCVD` with `dynamicPct > 0`, exercising `Core/Indicators_OrderFlow.vb:416-424` | ⚠ **That branch has never executed in a test.** Both existing fixtures omit the argument and take the static-only legacy arm; the app always takes the other one |

**Not in this session:** `A56e` (eval-cache identity, a separate queue row) and `A56f` (session buckets — **subsumed by A56b** once the guard covers seeded collections).

---

## 6. Acceptance

- All six projects build **0/0 Release**: solution · AutoTweaker · WhatIfRunner · CeilingAudit · BacktestRunner · OrderCheck. ⚠ **Only a Release build of all six catches a missing project link** — that trap was found during the trade-identity build and neither the app build nor the harness caught it.
- Harness **ALL PASS**, everything unregressed plus A56a–d.
- **Mutation results stated** for each new fixture.
- `tools/checks/verify-gate.ps1 prepush` **GATE PASSED**.
- ⚠ **Settings version: the six POCO fixes change no JSON value, so `settings.json` does NOT move — it stays v66.** The verify-gate's version-bump guard may WARN on an engine-path change; **that warning is expected and non-blocking**, same as the C1 sessions.
- **Display-string parity: NO OBLIGATION**, stated explicitly per the hard rule — nothing here touches a rendered surface.

## 7. Out of scope

- ⚠ **Copy 4 — fixture literals.** Do not sweep the suite for stale literals. Two are known (`A6`'s `trendGate:=10.0`; `BuildResolutionCfg`'s `RocSlopeDeltaThreshold = 0.105` at `Program.vb:995`, against a v40-shipped 0.06) and both are separately queued. **Governed by convention, not by this build.**
- **The eval-cache identity key** (`LivePerformanceTracker.vb:358,431`) — its own queue row.
- **The pooled minute-key dedup** — a procedure edit, not code.
- **Emptying the session-bucket seed** — ⚠ **explicitly ruled against.** Re-sync it; do not delete it.
- **Making settings-load failure fatal** — considered and rejected: wrong for a 24/7 unattended collector, where *keep last good and shout* beats *refuse to start*.

## 8. Reversibility

Every part is additive or a value correction. The guard is a new fixture — deleting it is the rollback. The six POCO moves restore by reverting the property initialisers. The deletions restore by re-adding `Optional … = <value>`. **No settings key moves, no dataset boundary, no scoring change.**
