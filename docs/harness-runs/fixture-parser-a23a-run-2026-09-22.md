# Fixture parser — the `8a` RE-MEASURE on the `A23a` window, 2026-09-22 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) at `2f0381b`. **Detectors:** `FP-1` (provenance verdict) and `FP-2` (name against assertion). **Protocol:** [`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md).

**Baseline:** [`fixture-parser-20260922T185529Z-baseline.json`](fixture-parser-20260922T185529Z-baseline.json), committed at `73a721c` **before** the detector ran. The ordering is provable from git.

**Closes** [`../seat-handover-2026-09-22b.md`](../seat-handover-2026-09-22b.md) §0 action 1 and the RE-MEASURE half of item `8a` in [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §8.4.

---

## 1. The window, and a correction to the handover's count

`seat-handover-2026-09-22b.md` §0 says *"six newly-opened `FP-1` sites"*. **Measured: four.**

| | Count | Source |
|---|---|---|
| Parameters newly in scope after item `8a` | 6 | `fundingBoost` · `upgradeBonus` · `tauFastSec` · `tauNormSec` · `grossFloorUsdPerSec` · `minCoverageSec` |
| Of their sites, those that carry a provenance marker | **4** | all in `A23a_AggrVelSteadyRate` |
| `FP1_CANDIDATES` at `2f0381b` | 23 | the 2026-09-22 clean run's 19 + these 4 |
| New `FP-2` names | 1 | `A23a_AggrVelSteadyRate` |

⭐ **The six counted PARAMETERS. Only an in-scope site that also carries a marker joins the `FP-1` residual.** The `fundingBoost` and `upgradeBonus` sites carry none (see §5).

**Run window:** `-SubFilter '^A23a_'`, so the 19 sites and 6 names judged in the clean run are excluded. **No `-ScopeBaselinePath`**, so `FP-Q1` made zero calls and the judged population is exactly the list printed by the no-key run.

⭐ **How the list was obtained without spending it:** the harness was run with `TYPESAFE_API_KEY` stripped (`SCOPE_JEV_CALLS=0`, `EXIT_REASON=BASELINE_MISSING`). The detector's exact state fields were then dumped from a scratch copy of the harness whose `Invoke-Jev` was replaced by a `throw`. **The seat's baseline read the same input the detector got.**

---

## 2. Result

| Item | Seat | Detector | Agreement rate | `mean_top_prob` (min) |
|---|---|---|---|---|
| `A23a#3061#tauFastSec` | `mechanism_declared_ok` | `mechanism_declared_ok` | **1.0** | 0.818 (0.81) |
| `A23a#3061#tauNormSec` | `mechanism_declared_ok` | `mechanism_declared_ok` | **1.0** | 0.784 (0.76) |
| `A23a#3064#grossFloorUsdPerSec` | `mechanism_declared_ok` | `mechanism_declared_ok` | **1.0** | 0.814 (0.79) |
| `A23a#3064#minCoverageSec` | `mechanism_declared_ok` | `mechanism_declared_ok` | **1.0** | 0.670 (0.59) |
| `A23a` name (`FP-2`) | `name_matches` | `name_matches` | **1.0** | 0.640 (0.62) |

**Agreement: 5 of 5. Every row STABLE, 5 of 5 samples. Zero disagreements to adjudicate.**

| Cost | Measured |
|---|---|
| Jev calls | 25 (5 items × 5 samples) |
| `USAGE_INPUT_TOKENS` | **38,022** |
| Cost | **≈ $0.0016** at $0.042 / Mtok input |
| Wall time | **13.4 s** in the judging step; 27.5 s for the whole process |

---

## 3. ⛔ What 5 of 5 is worth — less than it looks

- ⛔ **The four `FP-1` rows share ONE comment block.** It opens *"MECHANISM — all FOUR literals below"*. The deciding input is one declaration, read four times. **On the arm that decides, this is `n=1`, not `n=4`** — the lesson of [`fixture-parser-scope-run-2026-09-22.md`](fixture-parser-scope-run-2026-09-22.md) §3.
- ⚠ **It is the easy case.** The class is stated in capitals with per-literal reasons. The hard `FP-1` case is a declaration in prose without the keyword, or one whose reasoning quietly tracks the key. This window holds neither.
- ⚠ **The seat was not blind to the intended class.** It read the item-`8d` ruling ([`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §8.6) before the comment. Declared in the baseline file, before the run.
- ⭐ **One useful data point for §4d of the protocol:** `minCoverageSec` had the lowest top probability (0.59–0.73) and was still stable and agreeing. Probability and stability stay separate readings.

⭐ **So the clean population is now spent, and it told us the detector reads an explicit declaration correctly. It did not test the cases that matter.** The population that WOULD test them is §5 below.

---

## 4. A comment defect recorded BEFORE the run

The shared `A23a` block says *"Three of the four DO equal currently-shipped values"*. **All four do** (`fast_window_sec` 5 · `gross_floor_usd_per_sec` 50 · `default.norm_window_sec` 120, twice). The next line lists three KEYS, which is the likely source. It does not touch the MECHANISM class, so it moves no read. **`FP-1` was not asked about prose accuracy, so its silence here is not a miss.**

---

## 5. ⛔⛔ WHAT THE HARNESS CANNOT SEE — the finding of this run

[`../harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §3 requires this section. It turned out to hold the run's real result.

### 5.1 In-scope sites with NO marker never reach `FP-1`, and nothing counts them

The residual is `HasMarker -and InScope` (`tools/checks/fixture-parser.ps1`, the `$residualSites` line). `HasMarker` is a keyword match for `MECHANISM` or `SHIPPED` in the comment block. **A site in scope with no marker is never judged, and no counter reports it.** Measured code-only, from the same no-API scratch copy:

| | Count |
|---|---|
| In-scope sites | 57 |
| **In scope, no marker** | **34** |
| **…of which the literal equals an ever-shipped value** | **11** |

The 11, each checked in the tree:

| Fixture | Literals | Ever-shipped set |
|---|---|---|
| `A20a_CalcOfiRefactorEquivalence` (line 2535) | `buyDominantRatio:=2.0` · `sellDominantRatio:=0.5` · `bookDepth:=5` | {1.6, 2, 3} · {0.333, 0.5, 0.625} · {5} |
| `A20b_CalcOfiEdgeCasesUnchanged` (lines 2556, 2563) | the same three, twice | same |
| `A23b_AggrVelBurstDetection` (line 3092) | `tauFastSec:=5.0` · `tauNormSec:=120.0` | {5} · {120} |

⛔ **The run exits 0 with `FP1_BAD_VERDICTS=0` while these 11 sit in scope, undeclared, equal to shipped values.** That is the `A43b` breach shape, eleven times, and a silent hole of the kind this repo rejects. The other 23 unmarked sites are `fundingBoost` and `upgradeBonus` literals that do NOT equal a shipped value — undeclared under the rule's *"MUST declare"*, but not confusable.

Two things this exposes outside the harness:

- ⛔ **`CLAUDE.md`'s fixture-literal provenance rule cites `A20a`/`A20b` as the LEGITIMATE example**: *"pass OFI thresholds at 2.0/0.5 — neither the method default nor the shipped 1.6/0.625"*. **`2.0`/`0.5` WERE shipped**, from `dc40f8c` (2026-04-22) until the v48 re-baseline `a86b4f1` (2026-07-03). The example is off-currently-shipped and ON-ever-shipped — the exact trap the same rule's `F3` addendum warns about. And `bookDepth:=5` equals today's shipped value. **The legitimacy argument (a refactor-equivalence test, any consistent value serves) still holds. The declaration is still missing.**
- ⛔ **The item-`8d` comment on `A23a` is wrong about `A23b`.** It says *"A23b/A23c/A23d pass the same taus positionally"*. `A23b`'s burst loop passes them **by name** (line 3092).

### 5.2 Positional literals are invisible

The parser reads named-argument literals only (spec §4.1 step 1). `A23b` alone passes `5.0, 120.0` to `Fold`, `50.0, 120.0` to `Snapshot`, and `2.5, 0.2` to `ClassifyAggressorBurst` positionally. **All six equal ever-shipped values** (`burst_ratio_threshold` 2.5 and `direction_lean_floor` 0.2 both shipped at `922da8e`, v50). **None is visible to the harness at all.** Not sized beyond `A23b`.

### 5.3 A doc-rot instance found in passing — kept for harness 4

[`../DeribitIndicatorProject.md`](../DeribitIndicatorProject.md) §4, Tier 2 table, `OFI` row, still gives `BuyDominantRatio (2.0) / SellDominantRatio (0.5)`. Shipped since v48 is **1.6 / 0.625** (tracked `settings.json` at `2f0381b`). ⭐ **Left unfixed on purpose:** it is a known positive for the harness-4 doc scanner (version rot), found by the seat and not by any detector, so it can go in that harness's baseline without contaminating it.

---

## 6. ⚠ What this run does NOT establish

- **That `FP-1` handles an implicit or prose-only declaration.** Not in the window.
- **Anything about `FP-Q1`.** Zero calls this run, by design.
- **Whether the 34 unmarked sites carry a prose declaration without the keyword.** The marker is a keyword match. For the 11 listed in §5.1 the comments were read and none declares a class; the other 23 were not read.
- **The size of the positional blind spot** beyond `A23b`.
