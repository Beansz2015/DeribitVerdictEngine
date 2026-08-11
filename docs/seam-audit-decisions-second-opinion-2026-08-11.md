# Second opinion — the two seam-audit design decisions (2026-08-11)

**To:** the orchestrator seat, for a final decision.
**From:** an independent reviewer seat. **Model: Opus. Effort: high.**
**Basis:** measured in the tree, not reasoned from the summary. Scripts and commands are named so every number can be re-run.

> **Headline: I agree with Decision 1's direction and DISAGREE with Decision 2.**
> The requester asked for exactly that signal, so it is stated first.
> **One premise on each decision is factually wrong**, and both corrections cut against arguments the requester made.

---

## 0. The decisions, restated so this page stands alone

Every indicator threshold in this codebase exists in **four** copies:

1. A VB method `Optional` parameter default — `Optional trendGate As Double = 10.0`
2. A POCO default in `Core/Settings/EngineSettings.vb` — `Public Property TrendGate As Double = 23.0`
3. The shipped `settings.json` — `"trend_gate": 23.0`
4. Fixture literals in `verify/ordercheck/Program.vb` — `CalcOBV(…, trendGate:=10.0)`

- **Decision 1** — guard three copies (option **a**), or delete the method defaults and make those parameters required (option **b**)?
- **Decision 2** — the session-bucket list seeded in `Core/Settings/EngineSettings.vb` is stale ("aligned to live v30"; the tree is v66). Empty it, or not?

---

## 1. Q1 — is the parameter-to-key mapping problem real?

**Yes, and it is worse than the requester's write-up says.** There is no clean derivation.

I harvested the ground-truth mapping mechanically from production `param:=cfg.Path` bindings — 38 pairs. The proposed convention (strip `Calc`, camelCase to snake_case) breaks on roughly **a third** of them, not on one case.

| Parameter | Actual settings property | Derivable by a string rule? |
|---|---|---|
| `CalcMTFGate.adxPeriod` | `MTFGate.DmiPeriod` | ❌ **No — different word** |
| `CalcMTFGate.minOf` | `MTFGate.RequiredConfirms` | ❌ **No** |
| `CalcMTFGate.candleLookback` | `MTFGate.CandleCount` | ❌ **No** |
| `CalcMicroCVD.dynamicPct` | `MicroCVD.AccelThresholdDynamicPct` | ❌ No |
| `CalcMicroCVD.floorPct` | `MicroCVD.AccelThresholdFloorPct` | ❌ No |
| `CalcMicroCVD.microWindowSize` | `MicroCVD.WindowSize` | prefix stripped |
| `CalcTFI.tfiWindowSize` | `TFI.WindowSize` | prefix stripped |
| `CalcSwingPivots.pivotWing` | `Swing.PivotWing5m` | suffix added |
| `CalcSwingPivots.lookbackBars` | `Swing.LookbackBars5m` | suffix added |
| `CalcRSIDivergence.overboughtThreshold` | `RSI.DivergenceOverboughtThreshold` | prefix added |
| `CalcRSIDivergence.oversoldThreshold` | `RSI.DivergenceOversoldThreshold` | prefix added |
| `CalcRSIDivergence.*` | `Indicators.RSI` (not `RSIDivergence`) | class differs |

The three `CalcMTFGate` rows are not derivable by **any** rule — and `CalcMTFGate` also sits at JSON **top level**, not under `indicators`.

**Verdict: the requester's objection — "a hand-maintained table is a fifth copy, which is guarding the class with an instance of the class" — is correct and understated.** Option (a) as specified is not viable.

**The derivation the requester missed, and why it does not rescue option (a).** The mapping *does* already exist in compiler-checked form: it is written at the production call sites, which is exactly how I extracted it in about 40 lines. But a test that parses production source to rebuild a table is more fragile than the table it replaces. **Not a rescue.** The real answer is §4 below, which needs no mapping at all.

---

## 2. Q2 — how many fixture call sites actually omit a settings-mirroring argument?

**Nine. Not forty.** So the churn mind-changer is **not** triggered.

Method note: my first measurement said four production omissions. **That was wrong** — the scanner matched function names inside comments. Corrected, and re-run handling positional arguments as well as named ones:

| Category | Count |
|---|---|
| **Production** sites omitting a settings-mirroring, value-consumed default | **0** |
| **Fixture** sites omitting one | **9** |

The nine: `CalcCVD` ×1 · `CalcMicroCVD` ×2 · `CalcMTFGate` ×2 · `CalcOBV` ×2 · `CalcTFI` ×2 — all in `verify/ordercheck/Program.vb`.

**The requester's scoping caveat is empirically confirmed.** The other production omissions are all genuinely internal conveniences with no settings counterpart, and must be left alone:

| Site | Parameter | Why it is legitimate |
|---|---|---|
| `UI/MainForm_Analysis.vb:322`, `tools/BacktestRunner/ReplayLoop.vb:447` | `AppendFundingSample.maxAgeMs` | Defaults to a named constant, not a settings key |
| `UI/MainForm_Analysis.vb:270`, `:272` | `CalcVWAP/CalcVWAPBands.nowUtc` | A determinism test-seam; production correctly wants "now" |
| `LiveMicrostructureEvaluator.vb:135` | `CalcSpread` thresholds | **Documented deliberate** — only the bps value is read, and bps is threshold-independent |

**A mind-changer that is now dead: VB positional rules do not bite.** In every affected signature the settings-mirroring optionals **already precede** the trailing genuinely-optional parameters (the `ByRef` outputs, `nowUtc`). Making them required needs **no signature reordering and no call-site churn**.

### ⚠ 2.1 The objection the requester should hear — option (b) fixes the class that did not break

**The drift that motivated this entire exercise did not hide behind an omission.**

Fixture **A6** — the OBV normalisation fixture, `verify/ordercheck/Program.vb:661-662` — passes `trendGate:=10.0` **explicitly**. Making the parameter required leaves A6 **completely unchanged**, still pinning 10.0 against a shipped 23.0.

The write-up says "three copies become two". **Copy 4 is independent of copy 1, and copy 4 is the one that actually failed.**

Corroborating instance, found while checking this: `BuildResolutionCfg()` at `verify/ordercheck/Program.vb:995` hard-codes `RocSlopeDeltaThreshold = 0.105`. Shipped is **0.06** — settings v40 re-baselined it. A second stale fixture literal, also untouched by option (b).

---

## 3. Q3 — do any fixtures reach the seeded buckets?

**No. Zero.** Six fixtures touch a session-bucket consumer (`A14a`, `A14b`, `A14d`, `A14e`, `A14i`, `A15d`). **All six route through `BuildResolutionCfg()` at `verify/ordercheck/Program.vb:985-997`, which fully replaces `SessionVolume.Sessions`.** Emptying the seed breaks nothing in the harness.

### ⚠ 3.1 But the premise behind Decision 2 is wrong

The write-up says *"the code-defaults path is theoretical except in the harness."* **It is not.**

- `Core/Settings/SettingsLoader.vb:44` initialises `_current = New EngineSettings()`.
- The parse-error handler **deliberately keeps it**. Its own comment: *"On parse error, keep the last good settings rather than crashing. At startup 'last good' is the POCO defaults, so the engine is then running on uncalibrated values — record it so MainForm can surface it."*

**So a real non-harness path reaches the seed: a `settings.json` parse failure at startup.**

**And it is already announced.** `UI/MainForm_Layout.vb:1897-1898` renders `"settings.json parse failed — running on code defaults · "` on the LOG line, recomputed every render, self-clearing on a successful load.

⚠ **This kills the central argument for Decision 2.** The write-up's load-bearing line is *"wrong values are worse than absent values, because absent is detectable and wrong is not."* **Here both are announced by the same banner.** Emptying the seed buys **nothing** on the silence axis. It swaps one degraded mode for another, and both shout.

**One argument for emptying that the requester did NOT make, and it is their best one:** an empty list gives a *coherent* degraded mode — no session scaling anywhere, everything at base resolution. The current stale seed gives an *incoherent* one — settings v30 multipliers bolted onto a v66 engine. Coherence is a real benefit. **But a correct seed beats both.**

**Residual silent path, stated for completeness:** the banner covers a *parse failure*. It does **not** cover a `settings.json` that parses cleanly but is **missing the `session_volume` block** — no exception, no banner, seed applies silently. That is narrow, because the file is tracked and shipped, but it is the one genuinely silent route.

---

## 4. Q4 — option (d), and it dissolves the Q1 blocker entirely

> ### **Option (d): compare `New EngineSettings()` against the deserialised shipped `settings.json` by reflection, at runtime.**

| Property | Why it matters |
|---|---|
| **No parameter-to-key mapping** | It never touches method parameters. §1's blocker simply does not arise |
| **No fifth copy** | Reflection reads the `JsonPropertyName` attributes **the serialiser itself uses**. It consumes the real mapping rather than transcribing it |
| **Covers seeded collections free** | Decision 2 becomes a special case of Decision 1, not a separate job |
| **Small** | ~40 lines. `A52a` — the ASIA arming-key guard — is the working template |

**Evidence it works:** a PowerShell prototype of exactly this walk found all four drifted session-bucket values, both known drifts (`CVD.slope_pct_of_value`, `MicroCVD.accel_threshold_dynamic_pct`), zero JSON orphans and zero false positives, once scoped **class → JSON block → key**.

**Scope rule for the guard, derived and verified:** a key with a **concrete POCO default** can drift; a key declared **`Double?` = Nothing** (the nullable "inherit" override) **cannot**, because it carries no competing value. This is why settings v40, v41 and v48 produced no drift while v33, v34, v36 and v58 did. **Scope by that rule, not by a hand-listed key set.**

### Considered and rejected — option (e): make settings load failure fatal

It would make POCO defaults unreachable in production and moot the whole copy-2 versus copy-3 question. **Wrong for a 24/7 unattended AWS collector**, where "keep last good and shout" beats "refuse to start". Recorded so it is not re-proposed.

---

## 5. Recommendation

| Decision | Requester's call | This seat's call |
|---|---|---|
| **1** | (b) delete method defaults | ✅ **(d) + scoped (b)** — (d) is the guard that actually works; (b) is still worth doing, but as **removing dead code**, not as the fix |
| **2** | empty the seed | ❌ **Disagree — guard it via (d), do not empty it** |

**Why the two decisions diverge, which is the question the requester flagged as most useful.** They look identical — "remove the stale mirror, force the caller to be explicit" — but they are not:

- **Decision 1's copies cannot be guarded** without a mapping table that is itself an instance of the defect. **Deletion wins by default.**
- **Decision 2's copies can be guarded trivially** — it is plain POCO-versus-JSON, which reflection handles with no mapping at all. **Deletion is unnecessary.**

Empty the seed only if the project decides the code-defaults path should not exist. **In that case the honest move is option (e), not an empty list with a half-alive path.**

### ⚠ 5.1 Nothing proposed here fixes copy 4, and that must be said plainly

No guard can distinguish a fixture testing **mechanism** from one testing **shipped behaviour**:

- Fixtures `A20a` / `A20b` pass OFI thresholds at 2.0 / 0.5 — neither the method default nor the shipped 1.6 / 0.625. **Legitimate**: they are refactor-equivalence tests comparing two code paths on identical inputs. Any consistent value serves.
- Fixture `A6` pins `trendGate:=10.0` against a shipped 23.0. **Stale**, and it is how the two-month drift survived.

**The two are indistinguishable to a machine.** This needs a convention, enforced by review:

> **A fixture asserting shipped behaviour derives the value from cfg. A fixture asserting mechanism passes a literal and says so in a comment.**

---

## 6. Corrections this seat owes, both against its own prior audit

1. The audit stated *"every production call site passes the cfg value by name."* Production frequently passes these **positionally** (for example `CalcOBV(candlesExec, r.OBVTrend, r.OBVDivergence, cfg.Indicators.OBV.TrendGate, cfg.Indicators.OBV.DivergenceGate)`). **The verification method was weaker than the claim.** The conclusion survives re-measurement, but only after excluding `maxAgeMs`, `nowUtc` and the documented `CalcSpread` discard.
2. The first omission measurement reported **4 production omissions**. That was a **comment-matching artefact**; the true figure is **0**. Caught by reading the flagged lines instead of trusting the count.

---

## 7. If this is approved, the work it implies

> ### **Model: Sonnet. Effort: medium. ONE session.**

**Why that tier.** The judgement is finished and recorded above. Option (d) is a reflection walk against an in-repo template (`A52a`), and the scope rule is stated. Scoped option (b) is nine fixture edits and no production edits, with no signature reordering.

**Where Sonnet will specifically slip:**

1. ⚠ Matching **key name alone** instead of class → JSON block → key. 57 POCO classes share `period`, `enabled`, `window_size`; a naive matcher produced **10 false positives out of 12** during the audit.
2. ⚠ Getting the scope rule **backwards** — guarding nullable overrides produces a guard that fires constantly on keys that cannot drift and stays silent on the ones that do.
3. ⚠ Applying option (b) **bluntly** — making `maxAgeMs`, `nowUtc` or the `CalcSpread` thresholds required is pointless churn, and in the `CalcSpread` case actively misleading, because it would make the call site look dependent on values it discards.

> ### ⚠ Escalation trigger — move to Opus, high effort
> The reflection walk cannot resolve a POCO property to its JSON path without a hand-written exception. **That would mean option (d) has quietly become option (a)**, and the decision needs re-taking rather than working around.

**Not in this seat:** the copy-4 convention in §5.1 is a **standing rule for review**, not a build task.
