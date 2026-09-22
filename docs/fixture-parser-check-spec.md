# Fixture parser — build spec

**Created 2026-09-21 (UTC).** Harness 3 of the Jev programme ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §6).

**Two jobs, one parse of `verify/ordercheck/Program.vb`:**

- **`FP-1` — fixture-literal provenance.** `CLAUDE.md` makes this a hard rule and says plainly that **no tool can enforce it**, so it falls to review. It has failed twice in the tree, found by two different routes, neither by a test.
- **`FP-2` — fixture name against assertion.** Does a fixture's body assert what its name claims?

⛔ **NOT a gate.** Advisory, same three reasons as [`rider-travel-check-spec.md`](rider-travel-check-spec.md) `D-3`.

---

## 0. ⭐ Model and effort

**Model: Sonnet. Effort: medium.**

**Why.** Two complete in-repo templates exist — [`../tools/checks/commit-walker.ps1`](../tools/checks/commit-walker.ps1) for the full self-consistency shape, and [`../tools/checks/rider-travel.ps1`](../tools/checks/rider-travel.ps1) for the baseline refusal and coverage block. The population is measured (§2). The judgment design is settled. What remains is a parser and two question sets.

**Where Sonnet will slip — four traps.**

1. ⛔⛔ **Walking fewer than ALL 87 `settings.json` revisions.** `CLAUDE.md`'s rule is **off-EVER-shipped**, not off-currently-shipped. **Measured: the most recent 40 revisions return only `{18.0, 23.0}` for `indicators.OBV.trend_gate`; all 87 return `{0.001, 10.0, 18.0, 23.0}`.** A 40-revision walk misses two-thirds of the history and would clear a stale literal as novel.
2. ⛔ **Composing Nouls with AND.** Standing across this programme, measured at 0 of 8 against a single Choice's 4 of 8. Verdict reads ONE Choice. Copy `commit-walker.ps1`'s pattern.
3. ⛔ **Trusting a documented value history.** `docs/trader-tick-queue-archive.md` records `trend_gate` as starting at `8.0`; **`8.0` never existed in the tree.** Already recorded as finding `F6` in [`i17-sweep-batch-summary.md`](i17-sweep-batch-summary.md). **Ground truth comes from the revision walk, never from a doc.**
4. ⛔ **Regex-parsing VB with line anchors.** `CLAUDE.md` has a standing warning: an anchored scan misses VB's inline `If … Then Return`, the commonest form here. **Prefer unanchored patterns and filter by eye.**

**Escalation trigger — stop and report.**

- The revision walk yields a value set for fewer than half the matched keys. The extractor is then broken, not the history sparse.
- Fewer than 60 literal call sites are found. §2 measured 120; half that means the parser is missing a form.

**Session split: none.**

---

## 1. Why this needs a judgment model

`CLAUDE.md` states the problem in its own words:

> **no tool can tell the two apart, and one of them rots**

A fixture passing a settings-derived threshold as a literal is either **SHIPPED BEHAVIOUR** (then it must derive from cfg, never hardcode) or **MECHANISM** (then a literal is correct and the comment must say why). `CLAUDE.md`'s worked pair: two fixtures pass OFI thresholds at values that are neither the method default nor the shipped pair, and that is **legitimate** — they are refactor-equivalence tests where any consistent value serves. Another pins a value against a different shipped one and that is **stale**. **To a machine they are the same shape.**

⭐ **The split that makes this tractable: the VALUE question is code, the COMMENT question is judgment.**

- **Code** computes whether a literal matches any ever-shipped value of its key. Deterministic, and it is where trap 1 bites.
- **Jev** judges whether the comment at that call site *declares the right class for what the code found*. Semantic, and it is what no tool can do.

---

## 2. The population — measured 2026-09-21 (UTC)

| Quantity | Value |
|---|---|
| `verify/ordercheck/Program.vb` | 16,739 lines |
| Fixture subs | **353** |
| Harness checks reported at last run | 425 — **more checks than subs; do not conflate them** |
| Named-argument literal passes | **118** |
| Distinct parameter names among them | **41** |
| `settings.json` revisions to walk | **87** |

⛔⛔ **CORRECTED 2026-09-21 after the build. This table first read 359 subs, 120 passes, 43 params — all measured with a raw regex that DID NOT STRIP VB COMMENTS.** The two phantom passes were `wide:=1.0` / `tight:=2.0` at `verify/ordercheck/Program.vb:12534`, which is **prose inside a MECHANISM comment block**. ⭐ **That comment is a CORRECT provenance declaration, so the seat counted the rule's own worked example as a violation candidate** — trap 4, the trap this spec itself carries, manifesting as comment-versus-code rather than line-anchoring.

⛔ **Two further counts were REMOVED, not corrected: "call sites carrying `MECHANISM` 44" and "carrying `SHIPPED` 18".** They were whole-file marker counts over a **different population** than named-argument passes — a `MECHANISM` comment also sits above object-initializer assignments such as `.RocMagnitudeThreshold = 0.50`, which is not a `:=` call. **`SITES_WITH_PROVENANCE_COMMENT` is 15 and can never reproduce 62.** Listing them beside the call-site count implied one population where there are two.

⚠ **These move. Treat as smoke anchors, never assertions.**

---

## 3. Decisions

Auto-proceeded and recorded. **None RESERVED** — this tool reads the harness and git history and writes a report. No settings key, no scoring, no rendered value, no collector or store write, no schema change.

| ID | Decision | Taken |
|---|---|---|
| `FP-D1` | Where the value history comes from | **The 87-revision walk, always.** Never a doc, never a recent slice. Trap 1 and trap 3 |
| `FP-D2` | Parameter-to-key matching | **Code proposes candidates by name normalisation** (camelCase to snake_case, strip a leading indicator prefix); **Jev adjudicates only where code finds zero or multiple candidates.** Most matches are mechanical; the residual is a naming judgment |
| `FP-D3` | `FP-2` ground truth | **Mutation.** Corrupt a known-good fixture's name, confirm the detector flags it, restore. ⭐ **The only route to positives here, and this repo already requires fixtures be mutation-proven** |
| `FP-D4` | Self-consistency | **5 samples, agreement rate on every row**, per [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4b. Not optional and not a later addition |
| `FP-D5` | Scope of `FP-2` on the first build | **The 44 `MECHANISM` and 18 `SHIPPED` sites only**, not all 359 subs. Those are the sites the rule governs; widening later is cheap |

---

## 4. What to build

`tools/checks/fixture-parser.ps1`. PowerShell 5.1. ⭐ **Mirror [`../tools/checks/commit-walker.ps1`](../tools/checks/commit-walker.ps1) throughout** — parameters, coverage block, baseline refusal, self-consistency loop, exit codes. Reuse `Invoke-Jev` from `tools/checks/lib/InvokeJev.ps1`; **never write a second HTTP call.**

### 4.1 Steps

1. Parse `verify/ordercheck/Program.vb`: every named-argument literal pass, its enclosing `Sub`, and the comment block above the call site.
2. Walk all 87 `settings.json` revisions. Build, per key path, the **set of ever-shipped values**. Cache it to a gitignored scratch file — the walk is slow and the history below `HEAD` is immutable.
3. Match each parameter to candidate settings keys (`FP-D2`). Record zero-candidate and multi-candidate cases separately.
4. Classify in code: does the literal equal any ever-shipped value of its matched key?
5. **Refuse without a baseline.** Structural, before any API key read.
6. Jev judges the residual — self-consistency, 5 samples.
7. Report, exit.

### 4.2 ⛔ Coverage block, and a broken parser is loud

```
FIXTURE_SUBS=<n>
LITERAL_CALL_SITES=<n>
PARAMS_DISTINCT=<n>
SETTINGS_REVISIONS_WALKED=<n>
KEYS_WITH_VALUE_HISTORY=<n>
MATCHED_ONE_KEY=<n>
MATCHED_ZERO_KEYS=<n>
MATCHED_MULTI_KEYS=<n>
LITERAL_EQUALS_EVER_SHIPPED=<n>
SITES_WITH_PROVENANCE_COMMENT=<n>
SITES_JUDGED=<n>
```

⛔ **Exit 2 with `PARSER_SUSPECT` when `LITERAL_CALL_SITES < 60`** (§2 measured 120) **or `SETTINGS_REVISIONS_WALKED < 80`** (§2 measured 87). Both are the escalation trigger firing rather than a quiet wrong answer.

### 4.3 The questions

**`FP-1`, per residual call site.** State: the enclosing sub's name, the call line, the comment block above it, the matched key, and **the ever-shipped value set the walk found**.

| ID | Type | Asks |
|---|---|---|
| `comment_declares_class` | noul | Whether the comment states which of the two classes this literal is |
| `class_matches_evidence` | noul | Whether the class the comment states is consistent with the supplied ever-shipped value set |
| `verdict` | choice | `mechanism_declared_ok` · `shipped_declared_ok` · `undeclared` · `declared_but_contradicted` · `ambiguous` |

**`FP-2`, per named fixture.** State: the sub name and its body.

| ID | Type | Asks |
|---|---|---|
| `name_matches_assertion` | noul | Whether the body asserts the property the name claims |
| `verdict` | choice | `name_matches` · `name_overclaims` · `name_understates` · `ambiguous` |

⛔ **`verdict` is the only answer code reads. Nouls are diagnostics, never combined.**

### 4.4 Baseline

⛔⛔ **First-run baseline to the TRACKED `docs/harness-runs/fixture-parser-<UTC-stamp>-baseline.json`**, per [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §2 step 3. Vocabulary is the verdict values above plus `unsure`.

⚠ **And §4a applies: the acceptance dry run's window must be disjoint from the measured run's.** Provide `-SubFilter <pattern>` so the two can be split by fixture-ID range.

---

## 5. Acceptance

1. All 87 revisions walked; `SETTINGS_REVISIONS_WALKED` printed. **Show `indicators.OBV.trend_gate` resolving to `{0.001, 10.0, 18.0, 23.0}`** — the §2 measurement, and the trap-1 proof.
2. `LITERAL_CALL_SITES` within sight of 120.
3. Forced `LITERAL_CALL_SITES < 60` exits 2 with `PARSER_SUSPECT`. Output pasted.
4. Runs without a baseline: exits 2, **no API call**. Output pasted.
5. Verdict reads the `verdict` Choice alone. **One line, confirmable by reading.**
6. Self-consistency: 5 samples, agreement rate on **every** row.
7. `Invoke-Jev` shared, not copied. One definition in the tree.
8. **`FP-D3` mutation:** corrupt one fixture's name, show the detector flags it, restore, show it stops. ⛔ **Restore via `git checkout` on that file and prove the tree is clean afterwards.**
9. A live acceptance run on a `-SubFilter` range. ⛔ **Counters, tokens and timings only. NO per-item verdicts, NO aggregates** — [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4a.

---

## 5a. ⛔⛔ OWED — the key matcher is the binding constraint, and it blocks the measurement

**Measured on the built harness, 2026-09-21 (UTC):**

```
LITERAL_CALL_SITES=118   MATCHED_ONE_KEY=23   MATCHED_MULTI_KEYS=5
MATCHED_ZERO_KEYS=90     SITES_WITH_PROVENANCE_COMMENT=15   SITES_JUDGED=15
```

⛔ **90 of 118 literals match NO settings key, so 76% of the population can never be judged.** `FP-D2`'s camelCase-to-snake_case normalisation carries far less than the spec assumed.

⭐⭐ **This is the THIRD appearance of the same lesson in this programme** — [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4 finding 3: *"the binding constraint was evidence gathering, not judgment."* It was true of the git evidence in the pilot run, true of the residual definition in harness 2, and true of key matching here. **The next improvement on any harness is almost always better candidate enumeration, not a better question.**

⚠ **And it is what blocks a clean first measurement.** The judged population is 15 and the seat is contaminated on all 15. **A working key matcher would open ~100 unseen sites — a genuinely fresh population, which is the fastest route to the clean measurement this programme still lacks.**

### Owed, in priority order

| # | Item | State |
|---|---|---|
| **1** | ⛔ **Fix `FP-D2` key matching.** 90 unmatched. Likely needs the enclosing method's parameter list read from the shipped source, not name normalisation alone | ⭐ **Open, but now DIAGNOSED and no longer blocking.** §8 measured `FP-Q1` and named the two gaps precisely: **A** the fixture-local cfg builder, **B** the one-hop forwarding wrapper. Both are code, both mechanical |
| **2** | ⛔ **Give `FP-D2` a real question with criteria.** It is a Decision with no Question; the build had to design a third question type from one line | ✅ **Answered by revision 1's `FP-Q1`, and MEASURED 2026-09-22** — [`harness-runs/fixture-parser-scope-run-2026-09-22.md`](harness-runs/fixture-parser-scope-run-2026-09-22.md). ⛔ The question is not the problem; see §8 |
| **3** | **Rule how far a shared comment block extends.** `A6_ObvNormalisation` calls `CalcOBV` twice under one comment; the parser attaches it to the first only, silently dropping the second call's literals | **Open — needs a decision, not a guess** |
| **4** | Adjudicate the two remaining disagreements from the first run | **Open** |
| **5** | ⭐ **Consider rewording the past-tense provenance comments.** The `A3` prose misled a careful reader; the detector read it correctly | **Open — trader's call** |
| 6 | §2's counts | ✅ **Corrected above** |
| 7 | Who writes a first-run baseline | ✅ **Fixed** — [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4c |

---

## 7. ⭐⭐ REVISION 1, 2026-09-21 (UTC) — three trader rulings, and a correction to §5a

**Supersedes `FP-D2` and §5a's diagnosis. §0's traps, §2's corrected counts and §4's mechanics stand.**

### 7.1 ⛔ Correcting §5a before the rulings

**§5a said "90 of 118 literals match NO settings key, so 76% can never be judged" and called the matcher the binding constraint. That overstates it, and the larger gap was mine.**

Measured: **only 43 of the 118 passes (22 of 41 parameters) are plausibly settings-derived.** The other 75 are **fixture inputs** — `price:=62000` is a BTC price, `lr` a learning rate, `epochs:=200` an iteration count, `nowUtcMs` a clock.

⛔ **`CLAUDE.md`'s rule governs a *"settings-derived THRESHOLD"*. This spec dropped that scoping word and treated every named-argument literal as a candidate.** So most of the 90 are correctly unmatched. **The matcher's real miss is about 15 of 43 in-scope passes, not 90 of 118** — weaker than wanted, not broken.

⚠ **Recorded because it is the second time in one session the seat called a one-sided number a binding constraint.** The first was claiming the commit walker's `tools/` exclusion was wrong; measuring the other direction refuted it ([`commit-walker-check-spec.md`](commit-walker-check-spec.md) §9.1).

### 7.2 The rulings — all three as recommended, trader, 2026-09-21 (UTC)

| # | Question | Ruled |
|---|---|---|
| `FP-Q1` | What is in scope for the rule? | **(b)** Scope in code, then **Jev adjudicates** *"is this parameter a settings-derived threshold, or a fixture input?"*. Name matching alone both over-matches (`atr:=20` hits the ATR block but is an input) and under-matches |
| `FP-Q2` | How far does one comment block extend? | **(b) Until the next blank line.** ⛔ Current behaviour — immediately-following statement only — **silently drops** `A6_ObvNormalisation`'s second `CalcOBV` call and its two literals. A silent hole is the class this repo rejects |
| `FP-Q3` | What resolves an ambiguous parameter name? | **(b) Read the PRODUCTION call site**, and the called method's signature where needed. ⛔ **(c), letting Jev pick from a shortlist, was rejected on MECHANISM not cost:** the parameter-to-key mapping is a fact in the source, and `docs.typesafe.ai/model-jaggedness/jev-1.13.md` names *"asking the model something code can compute exactly"* as its first anti-pattern |

### 7.3 `FP-Q3` is buildable — three shapes, all mechanical

**Verified against the tree before speccing:**

| Production call site | Shape | How the mapping is derived |
|---|---|---|
| `UI/MainForm_Analysis.vb:447` `CalcTFI` | `tfiWindowSize:=cfg.Indicators.TFI.WindowSize` | **Named — parameter to cfg path directly** |
| `UI/MainForm_Analysis.vb:409` `CalcOFI` | `buyDominantRatio:=ofiCfg.BuyDominantRatio` | Named via a **local alias**; resolve `ofiCfg` back to its cfg path, one hop |
| `UI/MainForm_Analysis.vb:549` `CalcOBV` | `cfg.Indicators.OBV.TrendGate, …` — **positional, no `:=`** | Read the method signature from `Core/Indicators_*.vb`, map **position to parameter name**, then to the cfg path |

⭐⭐ **The production call site is the right ground truth, and it makes the harness self-correcting: if production changes which key feeds a parameter, the mapping follows automatically.** A hand-kept table would be a fourth copy that drifts — the shape `CLAUDE.md` rejects elsewhere.

⚠ **A method with NO production call site has no derivable mapping.** Report those as their own class; never guess one.

### 7.4 What to build

1. **Mapping table, derived** (`FP-Q3`): for each method a fixture calls with a literal, locate its production call site(s), resolve named / aliased / positional forms, and emit `parameter → cfg path`. **Report methods with no production call site separately.**
2. **Scope filter** (`FP-Q1`): a parameter with a derived cfg path is in scope. One with none goes to Jev — *threshold or input?* — and only the thresholds join the residual.
3. **Comment extent** (`FP-Q2`): a block covers every statement down to the next blank line. **Print how many sites gained coverage**; `A6_ObvNormalisation`'s second call must appear.
4. Everything else in §4 unchanged, including self-consistency at 5 samples and the baseline refusal.

### 7.5 Acceptance

1. The derived mapping resolves all three §7.3 shapes. **Paste the resolved `parameter → cfg path` for `CalcTFI`, `CalcOFI` and `CalcOBV`** — named, aliased, positional.
2. Methods with no production call site are reported in their own class, not guessed.
3. `FP-Q2`: `A6_ObvNormalisation`'s **second** `CalcOBV` call appears in the population. Show the before and after site count.
4. `FP-Q1`: the in-scope count lands near **43**, not 118. `price`, `epochs`, `lr` and `nowUtcMs` must NOT be in scope.
5. ⛔ **`atr:=20` must be ruled OUT of scope** despite name-matching the ATR block. It is the worked over-match case.
6. Self-consistency at 5 samples, agreement on every row. Verdict reads the `verdict` Choice alone.
7. ⛔⛔ **DO NOT WRITE A BASELINE.** [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4c: the seat writes it. **Run the tool to the point it refuses, paste that, and stop.** ⭐ **The newly-opened sites are the clean population this programme has not yet had — reporting one verdict on them spends it.**
8. Counters, tokens and timings only. **No verdicts, no aggregates.**

---

## 8. ⭐⭐ `FP-Q1` MEASURED, 2026-09-22 (UTC) — the second detector under-scopes

**Full record: [`harness-runs/fixture-parser-scope-run-2026-09-22.md`](harness-runs/fixture-parser-scope-run-2026-09-22.md).** Seat baseline [`harness-runs/fixture-parser-scope-20260922T152622Z-baseline.json`](harness-runs/fixture-parser-scope-20260922T152622Z-baseline.json), committed at `9f0e1c3` **before** the run.

**23 parameters. Agreement 17 of 23 (73.9 %), and 12 of 18 (66.7 %) once the five spec-anchored parameters are removed.**

⛔ **All SIX disagreements run ONE way — the seat says `threshold`, the detector says `input`.** The detector found **1** of the **7** settings-derived thresholds in its own population. It is **stable**: three identical harness runs, identical output.

⛔⛔ **What the under-scoping hides, concretely: fixture `A23a` (`verify/ordercheck/Program.vb:3029-3032`) passes FOUR literals that all equal shipped values** — `tauFastSec:=5.0` · `tauNormSec:=120.0` · `grossFloorUsdPerSec:=50.0` · `minCoverageSec:=120.0` — **every one scoped OUT, so `FP-1` never judged any of them and none declares a class.** Same shape as the `A43b` breach, four at once.

### 8.1 ⛔ The cause was measured and the first answer was WRONG

The one hit, `staleAfterSec`, was the ONLY parameter whose mapping class read `NO_SIGNATURE_FOUND`; all 22 others asserted a negative. A perfect 1-for-1 correspondence across 23. **A controlled probe swapping ONLY `production_mapping_classes`, 5 samples a cell, refuted it:** both controls reproduced the harness (0/5 and 5/5) and **neither test cell flipped** (0/5 and 4/5).

⚠ **The correspondence was perfect across 23 items and still wrong, because the deciding class held exactly ONE item.** It was `n=1` dressed as `n=23`.

### 8.2 ⭐ What to fix — the enumeration, not the question

Every miss has a **mechanically derivable** cfg path the code failed to find. Once `FP-Q3` derives it the parameter is in scope by construction and `FP-Q1` is never asked. **This fix holds whatever drives Jev.**

| Gap | Covers | Shape |
|---|---|---|
| **A** fixture-local cfg builder | `fundingBoost` · `upgradeBonus` | Callee is a fixture helper whose body does `cfg.<path> = <param>` (`BuildA8Cfg`, `BuildBurstCfg`). `FP-Q3` reads production call sites and never opens it |
| **B** one-hop forwarding wrapper | `tauFastSec` · `tauNormSec` · `grossFloorUsdPerSec` · `minCoverageSec` | Fixture calls the inner method (`Fold`, `Snapshot`); production calls a forwarding wrapper (`MarketState.FoldAggressorVelocity`, `MarketState.GetAggressorVelocity`) |

⛔ **`NOT_CFG_SOURCED` is an assertion the code has not earned** — it is emitted when the SEARCH failed and it reads as a finding. Split it from *"could not search"*.

⭐⭐ **Programme finding 3 for the FOURTH time, and now positively evidenced:** improving the judge's input framing measured **zero** effect; the constraint is enumeration.

### 8.3 ⛔ `FP-Q1` sits OUTSIDE the baseline refusal

`FP-D11` places its Jev calls **before** the baseline gate. [`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §2 step 1 makes that refusal structural *"not a convention"* — **and the second detector is not covered by it.** That is exactly how a detector shaped a measured population while itself unmeasured. **A harness with two detectors needs two gates.**

### 8.4 Also owed, opened by this run

| # | Item | State |
|---|---|---|
| **8a** | Build gaps A and B into `FP-Q3`; re-run and re-measure | ✅ **BUILT 2026-09-22 (UTC)** — see §8.5. All six parameters now resolve a cfg path **by construction**. ⚠ The RE-MEASURE is NOT done and is the **seat's**, never the implementer's ([`harness-shadow-mode-protocol.md`](harness-shadow-mode-protocol.md) §4c) |
| **8b** | Split `NOT_CFG_SOURCED` into *searched-and-negative* vs *could-not-search* | ✅ **DONE** — `NOT_CFG_SOURCED` (earned negative) vs `SOURCE_NOT_TRACEABLE` (the search did not complete). ⛔ Measured **0 / 22**: not one site the old label wrote off was an earned negative |
| **8c** | Extend the baseline refusal to cover `FP-Q1` | ✅ **DONE** — `-ScopeBaselinePath`, `FP-D15`. **No conflict with `FP-D11`**, because the refusal is scoped to the CALL, not the run |
| **8d** | Declare a class on `staleAfterSec:=10` and on `A23a`'s four literals | ✅ **DONE** at `3c320b8` — all five ruled **MECHANISM**, decided per literal, both inertness claims mutation-checked. See §8.6 |
| **8e** | `MTF_TTL_SECONDS` is `Private Const`; `CLAUDE.md` rules it `Public Const` so the fixture reads it instead of restating `60` | ⛔ **STOPPED — do not force it.** `MTF_TTL_SECONDS` lives in `UI/MainForm_Layout.vb`, a `MainForm` partial class, and `verify/ordercheck/OrderCheck.vbproj` links **no** `UI/*.vb` file by design. `Public Const` alone does **not** let the fixture read it; compiling `MainForm` into the fixture project would drag in `System.Windows.Forms` and break the host-agnostic boundary that vbproj asserts in six separate comments. ⭐ **The move that WOULD work, unbuilt and unruled: put the constant on the host-agnostic `MtfRefreshPolicy` (already linked) as `Public Const` and have `MainForm` read it there.** That edits a live-path file for a fixture's benefit, so it is the trader's call, not an implementer's |
| **8f** | ⛔ **NEW.** A **constructor** callee with an `Optional` parameter production never passes is unresolvable: `New WsMarketDataSource(…, staleAfterSec:=10)` reads `NO_SIGNATURE_FOUND` (the declaration is `Sub New`, not `Sub WsMarketDataSource`), and even with the signature, production **omits** the argument — the real key is read inside the constructor body (`SettingsLoader.Current.Network.WsStaleAfterSec`, `WsMarketDataSource.vb:37-38`). **A fourth shape — "the callee's own default-fallback body" — would close it** | **Open.** `staleAfterSec` is the only instance in today's population |
| **8g** | ⛔ **NEW, and it is a circularity.** `FP-Q1`'s Jev state carries `example_comment_block`. So **adding a MECHANISM declaration can push a site OUT of scope**, and `FP-1` then never judges the declaration that was just written. Sites in scope *by construction* (an `FP-Q3` key) are immune; only the Jev-scoped remainder is exposed | **Open** |
| **8h** | ⛔ **NEW.** `FP-Q1` answered `ttlSeconds` differently on two runs whose state for that parameter was **identical** (§8.5). `FP-D14` runs it unsampled, so nothing detects this | **Open** |

---

## 8.5 ⭐⭐ REVISION 2 BUILT, 2026-09-22 (UTC) — items 8a, 8b, 8c

**Tool:** [`../tools/checks/fixture-parser.ps1`](../tools/checks/fixture-parser.ps1), decisions `FP-D15`–`FP-D20` in its header. **No baseline was written and no `FP-1` verdict exists** — every run below stops at `EXIT_REASON=BASELINE_MISSING`, which is the correct outcome (§7.5 item 7).

### The six, each resolved and each checked against the tree

| Parameter | Derived cfg path | Shape |
|---|---|---|
| `fundingBoost` | `scoring.funding_high_boost` | `FIXTURE_LOCAL_CFG_BUILDER` — `Program.vb:1136` |
| `upgradeBonus` | `indicators.aggressor_velocity.upgrade_bonus` | `FIXTURE_LOCAL_CFG_BUILDER` — `Program.vb:3748` |
| `tauFastSec` | `indicators.aggressor_velocity.fast_window_sec` | `wrapper(FoldAggressorVelocity)->POSITIONAL` — `DeribitWsFeed.vb:508` |
| `tauNormSec` | `indicators.aggressor_velocity.default.norm_window_sec` | `wrapper(FoldAggressorVelocity)->RESOLVER_RETURN` — `ReplayLoop.vb:279` |
| `grossFloorUsdPerSec` | `indicators.aggressor_velocity.gross_floor_usd_per_sec` | `wrapper(GetAggressorVelocity)->POSITIONAL` — `UI/MainForm_Analysis.vb:461` |
| `minCoverageSec` | `indicators.aggressor_velocity.default.norm_window_sec` | `wrapper(GetAggressorVelocity)->RESOLVER_RETURN` — `UI/MainForm_Analysis.vb:461` |

### Counters, before → after (both with the seat's scope baseline supplied)

| Counter | Before | After |
|---|---|---|
| `IN_SCOPE_PARAMS` | 19 | **25** |
| `IN_SCOPE_SITES` | 29 | **63** |
| `MAPPING_NO_PRODUCTION_CALL_SITE_SITES` | 58 | 35 |
| `MAPPING_FIXTURE_LOCAL_CFG_BUILDER` | — | 23 |
| `MAPPING_WRAPPER_FORWARDED` | — | 6 |
| `MAPPING_NOT_CFG_SOURCED_SITES` / `MAPPING_SOURCE_NOT_TRACEABLE_SITES` | 32 pooled | **0 / 22** |
| `LITERAL_CALL_SITES` · `PARAMS_DISTINCT` · `SETTINGS_REVISIONS_WALKED` | 118 · 41 · 87 | **118 · 41 · 87, unchanged** |

⭐ **The three §7.3 proof lines are byte-identical**, by construction: every revision-2 shape is a **fallback** that runs only after the pre-revision resolution has already failed at every production call site.

⛔ **The 8b split's own headline is `0 / 22`.** Every site the old `NOT_CFG_SOURCED` label wrote off was in fact *"the search did not complete"*. The label was never once an earned negative — a stronger result than §8.2 predicted.

### ⚠ Three things this build measured but did NOT settle

1. ⛔ **`FP-Q1` is not answer-stable, and it is still unsampled.** Two revision-2 runs differed on `ttlSeconds` (excluded on one, in scope on the other) **with an identical state for that parameter** — only the `-SourceFile` copy differed, and not in anything `ttlSeconds`'s state reads. That is item **8h**.
2. ⚠ **`staleAfterSec` also moved** (in scope before §8.6's comment landed, excluded after). Its state DID change — the comment block is part of it — so the comment is a *plausible* cause. ⛔ **Not established:** with `ttlSeconds` proving the detector flips unprompted, one sample cannot separate the two. That is item **8g**, and the honest reading is the §8.1 lesson again: `n=1` is `n=1` however tidy the story.
3. ⚠ **`FP-D18`'s derived key under-states the ever-shipped set.** `ResolveAggrVelNormWindow` reaches its per-session override through a second hop, so the derived key is the **default** arm (`norm_window_sec` {120}) and the NY override (60) is not in the set. A literal of `60` at such a site would read as never-shipped. Smaller than the parameter being invisible; still a hole.

### ⛔ The newly-opened sites are a CLEAN population — do not spend it

`FP1_CANDIDATES` grew from 13 to 23. `A23a`'s four are in it. **No verdict has ever been recorded on any of them** (§7.5 item 7). The next seat writes its own read FIRST.

---

## 8.6 ⭐ Item 8d — the five literals, decided one at a time (`3c320b8`, 2026-09-22 UTC)

**All five ruled MECHANISM. None became SHIPPED BEHAVIOUR, so nothing is derived from `cfg` and no assertion value moved.** The decision was taken per literal; the reasons differ and are not interchangeable.

| Literal | Role in the test | Why MECHANISM, and why deriving from `cfg` would be WRONG |
|---|---|---|
| `staleAfterSec:=10` (`SeededWsSource`) | isolates the connection-health gate from the age gate | Needs only `staleAfterSec << tradesAgeSeconds` (300). A `cfg`-read value ≥ 300 would make the age gate un-trippable and `A16a` **silently vacuous**. A hardcoded tight value cannot rot that way |
| `tauFastSec:=5.0` · `tauNormSec:=120.0` (`A23a`) | **LOAD-BEARING** — the expected bands are analytic functions of the taus | **Mutation-checked:** 5.0 → 7.0 gives `grossFast` 107.31 and fails the 109–112 band, exactly as `A* = a/(1-e^(-1/7))/7` predicts. Reading them from `cfg` would let a settings change silently alter what an arithmetic test asserts |
| `grossFloorUsdPerSec:=50.0` · `minCoverageSec:=120.0` (`A23a`) | **INERT** — `grossNorm ≈ 96.8` sits above the floor; coverage is 399 s against a 120 s bar | **Mutation-checked:** `(37.0, 83.0)` leaves `A23a` passing. An inert value asserts nothing, so it cannot be SHIPPED BEHAVIOUR |

⚠ **Off-EVER-shipped, walked over all 87 tracked `settings.json` revisions, not today's file:** `ws_stale_after_sec` {10} · `fast_window_sec` {5} · `gross_floor_usd_per_sec` {50} · `norm_window_sec` {60, 120}. **All five DO equal a shipped value.**

⛔ **The literals were deliberately NOT swapped for off-shipped values**, though that was on the table. Three reasons: §6 below says a legitimate literal that happens to equal a shipped value **is** the case the declaration exists to license; `A23b`/`A23c`/`A23d` pass the same taus **positionally**, so changing `A23a` alone would split the family for no test gain; and neutering the values would delete the hardest members of the very population `FP-1` exists to judge — the same "do not tune the input" error [`harness-runs/fixture-parser-scope-run-2026-09-22.md`](harness-runs/fixture-parser-scope-run-2026-09-22.md) §4 warns against for the question.

---

## 6. What this spec does NOT verify

- **That the parameter-to-key matching is right.** `FP-D2` is a naming convention, not a contract. A wrong match produces a confidently wrong value set.
- **That an ever-shipped match means the literal is stale.** `CLAUDE.md`'s own example has two fixtures legitimately passing non-shipped values; the converse — a legitimate literal that happens to equal a shipped value — is exactly what the `MECHANISM` declaration exists to license. **Equality is evidence, never a verdict.**
- **Values that were shipped only in an untracked `settings.local.json` overlay.** The walk sees the tracked file alone.
- **`FP-2` beyond the 62 sites in `FP-D5`.**
