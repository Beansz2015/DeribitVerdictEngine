# Seam audit — brief for a fresh seat (2026-08-11)

**From:** the orchestrator seat that found the two instances motivating this. **To:** a new conversation, starting cold.
**Type:** read-only investigation producing one report. ⚠ **Not a build. Fix nothing.**

---

## 0. Model + effort

> ### **Model: Opus. Effort: high.**

**Why this tier.** The entire purpose is **finding things that look correct**. Every instance on record passed its build review, passed its fixtures, and produced perfectly plausible output for weeks or months. There is no error message to grep for and no test that fails. That is a judgement task at every step, and a cheaper tier will report the easy structural matches and miss the class.

**Where this will specifically slip. Five traps, and the first is the one that decides whether the session was worth running:**

1. ⚠⚠ **Anchoring on the three known instances.** They are given in §6 so you do not waste time re-finding them — **but the value of this audit is the fourth instance, not a tidier write-up of the first three.** If the report comes back confirming the known set and nothing else, treat that as a signal the sweep was too narrow, not as a clean bill of health.
2. ⚠ **A naive POCO↔JSON matcher.** I ran one. **Ten of its twelve hits were false positives** — cross-class name collisions, `ATR.period` matching `RsiSettings.period`, because 57 POCO classes share key names like `period`, `window_size`, `enabled`. **Match class → JSON block, then key. Never key alone.**
3. ⚠ **Line-anchored VB greps.** `CLAUDE.md` carries this warning already and it applies directly to sweep 2: a scan anchored on `^\s*Return` misses **`If … Then Return`**, VB's inline form and *the commonest one in this codebase* — which is exactly the shape of the defect that started this. **Prefer unanchored patterns and filter by eye. A line-anchored VB grep returning few hits is more likely mis-anchored than genuinely sparse.**
4. ⚠ **Treating every difference as a defect.** Not every POCO/JSON divergence is drift; some defaults are legitimately fallbacks. Each needs a judgement and a stated reason, not a table row.
5. ⚠ **Reporting a doc's claim as verified.** Several docs assert "one seam, no copies". Sweep 3 exists because those claims are *unverified*. Check the tree.

> ### ⚠ Escalation trigger
> **If a sweep returns zero findings in a category where §6 already names a known instance, the sweep method is wrong — not the code clean.** Stop, fix the method, re-run. A false all-clear from this audit is worse than not running it, because it will be cited later as evidence the class was handled.

**Session split:** one session if it stays read-only. If a sweep opens into something large, report what you have and name the remainder rather than pushing on.

---

## 1. What this audit IS, and what it is NOT

**IS:** a sweep for one **defect class**, across the whole tree.

**IS NOT:**

- ⚠ **Not a signal-health audit.** That instrument exists ([`signal-health-audit-2026-07-03.md`](signal-health-audit-2026-07-03.md)) and measures fire rates, pairwise agreement and conditional outcomes. **None of the three known instances would be caught by it** — they are not statistical anomalies in output, they produce entirely plausible numbers. Do not re-run it and do not fold it in.
- **Not an indicator-by-indicator review.** The scoping is the defect class, not the indicator list. An audit organised by indicator will spend the session on arithmetic that the 2026-07-02 audit already found correct.
- **Not a re-derivation** of any threshold, band or gate.
- ⚠ **Not a fix.** See §4 — this one is load-bearing.

---

## 2. The defect class

Three instances are on record. They share one shape:

> **Two things that must agree, with nothing asserting that they do — and the failure is silent and looks exactly like correct behaviour.**

| # | Instance | The two things | How long it lived |
|---|---|---|---|
| 1 | `TradeStoreWriter.vb:149` | A dedup guard's **key** vs the thing it is meant to identify. A millisecond timestamp used as a trade identity | ~10 days, ~50 % of tape |
| 2 | `EngineSettings.vb` POCO defaults | The **POCO default** vs the **shipped JSON value** — the harness builds cfgs from `New EngineSettings()`, so they must agree | **2 months**, three keys |
| 3 | Three candle/funding store holes (2026-07-31) | A guard checking **existence or a fixed tolerance** rather than **completeness** | — |

The project already wrote instance 3's lesson down: *"a guard that checks EXISTENCE or a FIXED TOLERANCE rather than COMPLETENESS turns one bad fetch into permanent silent loss."* **Instance 1 is that lesson recurring in a different seam.** Assume there are more.

---

## 3. The five sweeps

### Sweep 1 — POCO ↔ JSON default pairs

**175** properties in `Core/Settings/EngineSettings.vb` carry an explicit default. **Exactly one** is guarded (`A52a`, the ASIA arming key). The other 174 are unasserted.

- Compare every default-carrying POCO property against the shipped `settings.json` value, **matched by class → JSON block**.
- ⚠ For each divergence, decide whether it is a **drift** (the two should agree) or **legitimate** (a genuine fallback). State the reason either way.
- ⚠ **The bigger question, and it is the one to answer:** for each drift, does any fixture exercise that value through `New EngineSettings()`? If so, **the harness has been pinning behaviour the app does not have.** That is the finding, not the drift itself.

### Sweep 2 — dedup and guard keys

For every dedup, monotonic guard, "already seen" test and resume cursor in the tree:

- **What is it keyed on, and is that key actually unique for the thing it identifies?**
- Is it an **existence/tolerance** test or a **completeness** test?
- ⚠ **What is the failure bias?** Does it err toward dropping or toward admitting — and which is cheaper if it is wrong? Instance 1's guard erred toward dropping in a path where drops are unrecoverable and duplicates are free.

Known starting points, not an exhaustive list: `TradeStoreWriter`, `MarketState` (candle staleness at `:109` — note that keying a *candle* on its timestamp is legitimate, since a candle **is** identified by its open time; the contrast with trades is instructive), `OhlcCache`, `HistoricalStore`, `LivePerformanceTracker`'s eval cache, `CoverageReport`.

### Sweep 3 — "one seam, no copies" claims

Several docs assert a single shared seam. **Verify each is genuinely one**, and that every consumer routes through it:

- `SignalEmitter.ComputeSideLevels` — claimed as one seam across snapshot, card, payload, CSV (**four** surfaces).
- `TradeStoreWriter` — claimed shared by the feed's streaming capture, `HistoricalStore`'s backfill, and `LoadTradeRange`'s read.
- `TradeCostSettings.EffectiveMinMovePct` — claimed shared by the live gate, eval cache, offline matrix, ceiling audit and what-if replay.
- `TradeRecord.ReadTradeId` / `ReadTradeSeq` — claimed shared by all three parse sites.

⚠ **Precedent that this is worth checking:** the identity build found that *"the writer links everywhere"* was **false** — AutoTweaker, WhatIfRunner and CeilingAudit link `DeribitClient.vb` but **not** `Core/TradeStoreWriter.vb`, and neither the app build nor the harness caught it. Only a Release build of all six projects did.

### Sweep 4 — completeness vs existence

Every check that decides "do I have the data?": does it verify **completeness**, or merely that *something* is present? Instance 3 is three occurrences of getting this wrong in one subsystem.

### Sweep 5 — fixture blind spots ⚠ probably the highest yield

**For each fixture family, name the input it never presents.**

A48's blind spot was *"two distinct trades in the same millisecond"* — its trades are built `A48Ms(i * 1000L)`, one second apart, so the case simply never arose. Eight fixtures, one of them named *"monotonic guard"*, and none could have failed.

⚠ **The sharpest version of this question:** the repo *already contained* the data that would have caught instance 1 — `A53e` uses two real same-millisecond Deribit trades — but aimed at the dedup contract rather than the write guard. **Look for other fixtures whose data would catch a bug they are not pointed at.**

---

## 4. ⚠ Report. Do not fix. This is deliberate.

**Fixing as you go destroys the measurement.**

Concretely: fix a POCO drift, the harness starts failing, and you would naturally fix the fixture too. **But the count of fixtures that were pinning stale behaviour is the actual finding** — it tells us how deep the class runs. Repair the symptom and that number is gone, and a later reader finds a clean tree and concludes the class was handled.

This is the same reason the two known POCO drifts (§6) are being deliberately left in place for you to find with the rest.

**Exception:** if you find something **actively destroying data right now** — the class instance 1 belongs to — stop the sweep and report it immediately. Do not sit on it until the end.

---

## 5. Deliverable

One report, `docs/seam-audit-2026-08-<dd>.md`, carrying:

1. **Per sweep:** what was checked, what was found, and ⚠ **what was checked and found clean** — a negative result is a finding here, because it bounds the class.
2. **Per finding:** the seam, the two things that disagree, the failure mode, whether it is **live or latent**, and how long it has been there.
3. **Proposed queue rows** for `trader-tick-queue.md` §2, each with a model + effort recommendation per `CLAUDE.md`.
4. ⚠ **What you did NOT check**, stated plainly. A bounded audit that says where its boundary is beats an unbounded one that implies it looked everywhere.
5. **Recommended fixtures** — but do not write them. **Fixture families A54 and A55 are taken** (A54 = the queued JSON↔POCO drift guard, A55 = the store write-guard fix). **Start at A56.**

If the work turns out to be multi-lane, follow [`batch-review-packet-convention.md`](batch-review-packet-convention.md) — summary plus review packet, two documents.

---

## 6. Already known — do not report these as new findings

| Item | State |
|---|---|
| `indicators.OBV.trend_gate` — POCO 10.0 vs JSON 18.0 | ✅ **FIXED** as settings v66, 2026-08-11. POCO now 23.0 |
| `indicators.CVD.slope_pct_of_value` — JSON **0.10**, POCO **0.01** | ⚠ **KNOWN, deliberately UNFIXED.** Yours to fold in |
| `indicators.MicroCVD.accel_threshold_dynamic_pct` — JSON **0.30**, POCO **0.03** | ⚠ **KNOWN, deliberately UNFIXED.** Yours to fold in |
| `TradeStoreWriter.vb:149` same-millisecond drop | **KNOWN**, spec written ([`trade-store-write-guard-identity-proposal.md`](trade-store-write-guard-identity-proposal.md)), gated on a venue probe |
| A48's fixture blind spot | **KNOWN** |

⚠ **A pattern worth carrying into sweep 1:** all three POCO drifts come from **one commit** — **v33** (2026-06-13), a JSON-only re-baseline pass that never touched the POCO. **Check whether other settings-only passes did the same.** `v34`, `v40`, `v41`, `v48` and `v58` are re-baseline-shaped by their change_log entries and are the obvious next places to look. **The unit of this defect may be the commit, not the key.**

---

## 7. Orientation

Follow `CLAUDE.md`'s session-start protocol, including **step 6** (read `trader-tick-queue.md` before saying anything about what is outstanding).

The two documents that give this audit its motivating detail:

- [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md) — instance 1 in full, including two rival explanations that were formed, tested and **rejected**. That method is the standard for this audit too: **a ready explanation for a surprising result is a warning sign, not a resolution.**
- [`trader-tick-queue.md`](trader-tick-queue.md) §2 — the current build-slot list, which your proposed rows join.

**Last full engine audit:** [`fable5-audit-2026-07-02.md`](fable5-audit-2026-07-02.md) — 2026-07-02, and it found the scoring core correct. **Six weeks and nineteen settings versions ago** (v48 → v66), plus C1 coverage, trade identity, and the settings overlay. That is the surface this audit is being asked to cover.
