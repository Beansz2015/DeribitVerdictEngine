# `BuildResolutionCfg` — declare the literals MECHANISM, and make them look it

**Status:** ✅ **BUILT 2026-09-03.** Harness `ALL PASS`, gate `PASSED`, all seven consumers re-verified unchanged. Packet: [`queue-17-18-batch-spec-back.md`](queue-17-18-batch-spec-back.md).
**Item:** queue item **17** ([`trader-tick-queue.md`](trader-tick-queue.md) §2, raised 2026-08-11 by the reviewing seat while checking something else — **not by the audit**).
**Ruling:** ⭐ **MECHANISM, option (c) — trader, 2026-09-03.** Keep literals, add the declaring comment, **and change the values to obviously-synthetic ones.**
**Author:** the orchestrator seat of 2026-09-03.

⚠ **The row's own words: *"the decision above is the work, not the edit."* That was right. The decision is made; what follows is small.**

---

## 0. Implementer brief

> ### Model: **Sonnet.** Effort: **LOW.**
>
> **Why that tier.** **One helper function, two literals, one fixture name string, one comment.** No logic changes, no signature changes, no new fixture. ⭐ **And the analysis is done: §1 records why this is mechanism, with the proof.**
>
> **Where it will slip — two traps:**
>
> | | Trap |
> |---|---|
> | **T1** | ⛔ **Do NOT change the resolution-**1** literals in `A14b` (`0.1` / `0.05`).** Those are **POCO defaults**, a different class — see §3's scope-out. Changing them is out of scope and would break the fallback assertion |
> | **T2** | ⚠ **`A14b`'s CHECK NAME repeats the numbers.** Change the name string with the values or the harness prints one thing and asserts another — **which is exactly the misreading this item exists to end** |
>
> **Escalation trigger:** if any of the seven consumers fails after the value change, **stop.** A mechanism literal is by definition one no assertion depends on; a failure means §1's analysis is wrong and the ruling must be re-taken.
>
> **One session. Minutes, not hours.**

---

## 1. Why this is MECHANISM — the proof, so it is not re-litigated

**`verify/ordercheck/Program.vb:1086`, inside `BuildResolutionCfg`:**

```vb
{"3", New ResolutionProfile With {.RocMagnitudeThreshold = 0.21, .RocSlopeDeltaThreshold = 0.105}}}
```

**Shipped** (`settings.json`): magnitude `0.21` (`:394`) · slope **`0.06`** (`:395`). **So the pair is half-current and half-stale, on one line, with nothing saying which is deliberate.**

⭐⭐ **`A14b` settles it, and more cleanly than reading all seven consumers:**

```vb
Math.Abs(ExecutionResolution.ResolveRocSlopeDelta(cfg, 3) - 0.105) < 0.0000001
```

**`BuildResolutionCfg` WRITES `0.105` into `cfg`. `A14b` asserts it READS BACK as `0.105`.** It is a **round-trip test of the resolver**. ⛔ **The shipped value never enters the computation, so a `settings.json` change could not fail it.** **A fixture that asserts its own input cannot be asserting shipped behaviour.**

**All seven consumers, read 2026-09-03:**

| Consumer | Touches ROC thresholds? | Class |
|---|---|---|
| `A14a`, `A14e`, `A14i`, `A15a` | ❌ Resolution *selection* only | n/a |
| **`A14b`** | ✅ Round-trips what the helper wrote | **MECHANISM** |
| **`A14d`** | ✅ `Resolve…(cfg,1) = cfg.Indicators.ROC.…` — **both sides derived, no literal** | ✅ **Already the correct shipped-behaviour form** |
| `A14j` | ✅ Writes its own `0.17`/`0.11` and reads back | **MECHANISM** |

✅ **Not one of the seven asserts a shipped ROC value.**

---

## 2. Why the value changes anyway — the cost the rule does not cover

**Under the rule, a mechanism literal needs only a comment. So why touch the number?**

⛔ **Because `0.105` is printed by the harness on every green run**, inside `A14b`'s check name: `"A14b ROC override (mag 3→0.21 / 1→0.1; slope 3→0.105 / 1→0.05)"`. **A reader takes that for the shipped 3-minute value. It has now been flagged twice, by two seats, both by accident.**

⛔ **And `0.06` would be the WRONG fix.** A value matching shipped invites the next seat to "helpfully" derive it from `cfg` — **re-coupling a mechanism test to a value it does not care about**, and reintroducing the ambiguity from the other side.

⭐ **The project's own precedent is option (c).** `A20a`/`A20b` pass OFI thresholds of **2.0 / 0.5** against a shipped **1.6 / 0.625**. **That is not an accident — an off-spec value is self-documenting. Nobody mistakes it for an assertion about shipped.**

---

## 3. The change

### R1 Both literals become obviously-synthetic

⚠ **BOTH — including `RocMagnitudeThreshold`, which currently MATCHES shipped at `0.21`.** Leaving it is the same trap in the other direction: a matching value reads as an assertion.

**Suggested: magnitude `0.50`, slope `0.25`.** Clearly not shipped (`0.21` / `0.06`), clearly arbitrary, ordering preserved. ⭐ **Any consistent pair works — that is the point — but pick one no shipped value could ever be confused with.**

### R2 The declaring comment, at the call site

**The rule requires the declaration to sit where the literal is.** Say it is MECHANISM, say why, and say what would change the answer:

```vb
' [Fixture-literal provenance, CLAUDE.md RULED 2026-08-11] MECHANISM, not shipped
' behaviour. BuildResolutionCfg builds a SYNTHETIC EngineSettings — it invents the
' session buckets too — so A14b round-trips what this line writes and no assertion
' depends on the value. The numbers are deliberately OFF-SPEC (shipped is 0.21/0.06)
' so nobody reads them as a claim about settings.json, the same signal A20a/A20b use.
' ⛔ If a future fixture asserts that ROC fires at the SHIPPED 3-minute threshold, that
' fixture derives from cfg — it does not read these.
```

### R3 Update `A14b`'s check name to match the new values

**Trap T2.** The name is what the harness prints.

### R4 Scope-out, stated so it is not swept up

⛔ **The resolution-1 literals in `A14b` (`0.1` / `0.05`) are NOT in scope.** They are **POCO defaults** — `BuildResolutionCfg` starts from `New EngineSettings()`, and profile `"1"` is `New ResolutionProfile()`. **They assert the fallback path, and they drift only if the POCO drifts, which is the class `A54a`'s reflection guard already watches.** ⚠ **A third literal class in one fixture; naming it is the point of this scope-out.**

### R5 No settings key, no version bump, no §15 entry, no card surface

**Test-only. `verify/` alone.**

---

## 4. Acceptance

| | Check |
|---|---|
| 1 | Harness green — **all 311+ PASS, 0 FAIL, `ALL PASS`.** ⚠ Run with `2>/dev/null`, never `2>&1` |
| 2 | ⭐ **All seven consumers still pass unchanged.** This is R1's guard: a mechanism literal is one no assertion depends on. **A failure means §1 is wrong — stop and re-take the ruling** |
| 3 | `grep -n "0.105" verify/ordercheck/Program.vb` returns **nothing** |
| 4 | The comment is at the call site, not in a header block |
| 5 | `tools/checks/verify-gate.ps1` green |

**Fixtures:** ⚠ **none added, deliberately.** This changes a fixture helper; the seven existing consumers are the coverage, and acceptance item 2 is the assertion.

---

## 5. What this spec does not know

- ⛔ **Whether other stale fixture literals exist.** This is the **second confirmed instance** after `A6`'s `trendGate:=10.0`, and **both were found by accident, neither by a test.** ⚠ **This spec fixes one. It does not sweep.** A sweep is its own item.
- ⚠ **`A6` is still stale** — `trendGate:=10.0` against a shipped `23.0`. **Same class, not fixed here, and it needs the same MECHANISM-or-SHIPPED ruling before anyone touches it.**
