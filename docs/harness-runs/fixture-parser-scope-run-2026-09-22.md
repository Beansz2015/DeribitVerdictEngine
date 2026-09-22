# FP-Q1 scope detector — first measured run, 2026-09-22 (UTC)

**Harness:** [`../../tools/checks/fixture-parser.ps1`](../../tools/checks/fixture-parser.ps1) · **detector:** `FP-Q1`, the threshold-or-input scope filter ([`fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §7.2).

**Closes owed item 1** of [`seat-handover-2026-09-22.md`](../seat-handover-2026-09-22.md) §4 — *"a second, UNMEASURED detector lives inside harness 3"* — and adjudicates owed item 2 (`upgradeBonus`).

**Baseline:** [`fixture-parser-scope-20260922T152622Z-baseline.json`](fixture-parser-scope-20260922T152622Z-baseline.json), committed at `9f0e1c3` **before** the detector ran. The ordering is provable from git, not asserted.

---

## 1. ⭐⭐ The result

| | Seat | Detector |
|---|---|---|
| `threshold` | **7** | **1** |
| `input` | 16 | 22 |
| `unsure` | 0 | — |

**Agreement: 17 of 23 (73.9 %).** ⛔ **All SIX disagreements run one way: the seat says `threshold`, the detector says `input`.** Not one runs the other way.

⭐ **The detector UNDER-scopes.** It found 1 of the 7 settings-derived thresholds in its own population.

### The six misses, each verified in the tree

| Parameter | The settings key the detector missed | Where the seat proved it |
|---|---|---|
| `fundingBoost` | `scoring.funding_high_boost` | `BuildA8Cfg` assigns `cfg.Scoring.FundingHighBoost = fundingBoost` — `verify/ordercheck/Program.vb:1128` |
| `upgradeBonus` | `indicators.aggressor_velocity.upgrade_bonus` | `BuildBurstCfg` assigns `cfg.Indicators.AggressorVelocity.UpgradeBonus` — `verify/ordercheck/Program.vb:3712` |
| `tauFastSec` | `indicators.aggressor_velocity.fast_window_sec` | `DeribitWsFeed.vb:509` passes `av.FastWindowSec` |
| `tauNormSec` | `indicators.aggressor_velocity.…norm_window_sec` | `DeribitWsFeed.vb:509` passes `tauNorm` (session-resolved) |
| `grossFloorUsdPerSec` | `indicators.aggressor_velocity.gross_floor_usd_per_sec` | `UI/MainForm_Analysis.vb:461` passes `avCfg.GrossFloorUsdPerSec` |
| `minCoverageSec` | the session-resolved `norm_window_sec` | `UI/MainForm_Analysis.vb:461` passes `avNormWin` |

⚠ **The `tauFastSec`/`tauNormSec` claim was first taken from `settings.json`'s own v50 `change_log` prose, then re-verified against `DeribitWsFeed.vb:504-510` before being written here.** [`fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §0 trap 3 says ground truth comes from the tree, never a doc, and that trap applies to the seat as much as to the tool.

### ⛔ What the under-scoping actually hides

**One fixture, `A23a` at `verify/ordercheck/Program.vb:3029-3032`, passes FOUR literals that every one equal a shipped value** — `tauFastSec:=5.0` (shipped 5) · `tauNormSec:=120.0` (shipped 120) · `grossFloorUsdPerSec:=50.0` (shipped 50) · `minCoverageSec:=120.0`. **All four were scoped OUT, so `FP-1` never judged one of them, and none carries a class declaration.** That is the same shape as the `A43b` breach the 2026-09-22 clean run found — except four at once, in a fixture the harness reported as fully covered.

---

## 2. ⛔⛔ It is STABLE, not a coin flip

| Evidence | Reading |
|---|---|
| Three identical harness runs | **Identical output all three times.** `IN_SCOPE_PARAMS=19`, `SCOPE_USAGE_INPUT_TOKENS=17404`, same single parameter in scope |
| Probe controls, 5 samples each | `grossFloorUsdPerSec` **0/5** `threshold` · `staleAfterSec` **5/5** `threshold` |

⭐ **This matters because [`harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4d makes the agreement rate the trust gate, and `FP-Q1` HAS NO AGREEMENT RATE** — `tools/checks/fixture-parser.ps1` calls it once per parameter and never 5×-samples it (`FP-D14`, line ~960). **Before this run there was no way to tell a stable miss from a flip.** The six misses are stable, so they are an evidence gap, not noise.

### ⛔⛔ CORRECTED 2026-09-23 (UTC) — this heading over-claims, and the quoted text above is kept rather than edited

**The heading says "It is STABLE". That is true of THE SIX MISSES and FALSE of `FP-Q1` in general.**

⛔ **`ttlSeconds` flips.** It was excluded on all three runs above and on both review runs, and **in scope** on one of the 8a implementer's runs — `IN_SCOPE_PARAMS` 25 and `IN_SCOPE_SITES` 63 there against **24 and 57** measured twice on review. **The delta is exactly `ttlSeconds`: one parameter, six sites.** Logged as `8h` in [`../fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §8.4.

⛔⛔ **And `ttlSeconds` was an AGREEMENT row in §1 — both judges called it `input`.** So **an agreeing row can be unstable.** [`harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4d found the opposite on `FP-1` (*"agreements 23, all STABLE 5 of 5"*) and that finding does **not** carry across detectors. See the new §4f there.

⚠ **What survives, and what does not.** The six misses held across five runs and two 5-sample probe cells (0/5 and 5/5), so **calling them an evidence gap rather than noise still stands.** What does not stand is reading this section as *"`FP-Q1` is stable"*. It is not, it is unsampled, and nothing in the harness would detect a flip.

---

## 3. ⛔⛔ The cause I proposed was WRONG — measured and refuted

**The hypothesis.** The one parameter the detector got right, `staleAfterSec`, was the ONLY one of the 23 whose mapping class read `NO_SIGNATURE_FOUND` — *"the code could not look"*. All 22 others carried a class that asserts a negative (`NOT_CFG_SOURCED`, `NO_PRODUCTION_CALL_SITE`, `PARAM_NOT_IN_SIGNATURE`). A perfect 1-for-1 correspondence across 23 items. **It read like the state's own framing steering the verdict toward the code's failure.**

**The probe.** Swap ONLY `production_mapping_classes`, hold every other field fixed, 5 samples per cell. Two cells are controls that must reproduce the harness.

| Cell | State | `threshold` |
|---|---|---|
| **A** control | `grossFloorUsdPerSec` + `NOT_CFG_SOURCED` | **0/5** ✅ reproduces the harness |
| **B** test | `grossFloorUsdPerSec` + `NO_SIGNATURE_FOUND` | **0/5** ⛔ did not flip |
| **C** control | `staleAfterSec` + `NO_SIGNATURE_FOUND` | **5/5** ✅ reproduces the harness |
| **D** test | `staleAfterSec` + `NOT_CFG_SOURCED` | **4/5** ⛔ did not flip |

⛔ **Both controls reproduced, so the state reconstruction is faithful and the probe is valid. Both tests held their original answer. The mapping-class label is NOT the driver.**

⭐ **I do not know what drives the split, and I am not substituting a second unmeasured cause for the one I just refuted.** What is ruled out is ruled out; the rest is open.

⚠ **Note what the correspondence was worth: it was perfect across all 23 items and still wrong.** The deciding class held exactly ONE item, so the pattern was `n=1` on the arm that mattered and looked like `n=23`.

---

## 4. ⭐⭐ What to fix — and it is not the judge

⛔ **Do not tune the question. Close the enumeration gap.** Every one of the six misses has a **mechanically derivable** cfg path that the code failed to find. Once `FP-Q3` derives it, the parameter is in scope **by construction** and `FP-Q1` is never consulted for it. That fix stands whatever drives Jev.

**Two gaps, both code, both already precedented by shapes `FP-Q3` handles:**

| Gap | Covers | The shape |
|---|---|---|
| **A — the fixture-local cfg builder** | `fundingBoost` · `upgradeBonus` | The callee is a fixture helper whose body does `cfg.<path> = <param>`. `FP-Q3` reads PRODUCTION call sites and never opens the fixture-local callee. A one-hop scan of the callee body resolves both outright |
| **B — the one-hop forwarding wrapper** | `tauFastSec` · `tauNormSec` · `grossFloorUsdPerSec` · `minCoverageSec` | The fixture calls the inner method (`Fold`, `Snapshot`); production calls a wrapper (`MarketState.FoldAggressorVelocity`, `MarketState.GetAggressorVelocity`) that forwards positionally. `FP-Q3` looks for a production call to the INNER name, finds none, and asserts `NOT_CFG_SOURCED` |

⛔ **Gap B means `NOT_CFG_SOURCED` is currently an assertion the code has not earned.** It is emitted when the search failed, and it reads as a finding. **Rename it or split it** — *"searched and it is not cfg-sourced"* and *"could not search"* are different facts and the report conflates them.

⭐⭐ **This is the programme's finding 3 for the FOURTH time** ([`harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §4): the binding constraint is candidate enumeration, not judgment. True of the git evidence in the pilot, the residual definition in harness 2, key matching in harness 3 — **and now positively evidenced, because §3 above tried to improve the judge's input framing and measured zero effect.**

---

## 5. Cost

| | Measured |
|---|---|
| Harness calls | 23 per run × 3 runs |
| Input tokens | **17,404 per run**, identical all three |
| Wall time | **12.3 s** per run |
| Cost | **≈ $0.0022** for the three runs, at $0.042/Mtok input |

⚠ **The probe's 20 calls were not token-instrumented.** Its cost is not measured and is not in that figure.

---

## 6. Side findings, all recorded in the baseline BEFORE the run

1. ⛔⛔ **`FP-Q1` has NO baseline refusal.** `FP-D11` puts its Jev calls **before** the baseline gate by design (`tools/checks/fixture-parser.ps1` ~1147-1149). [`harness-shadow-mode-protocol.md`](../harness-shadow-mode-protocol.md) §2 step 1 makes the refusal structural *"not a convention"* — **and the second detector sits outside it.** That is precisely how a detector shaped a measured population while itself unmeasured.
2. `ttlSeconds:=60` (`verify/ordercheck/Program.vb:2127-2141`) restates production's `Private Const MTF_TTL_SECONDS = 60` (`UI/MainForm_Layout.vb:72`). `CLAUDE.md` rules a constant into `Public Const` so a fixture can **read** the production number instead of copying it. **Both judges called this `input` and both are right** — it has no `settings.json` key (`mtf_gate` holds no TTL, whole-file grep for `ttl` finds none) — so the rule that bites here is the `Public Const` rule, not the provenance rule.
3. `staleAfterSec:=10` (`verify/ordercheck/Program.vb:2075`) equals the shipped `network.ws_stale_after_sec: 10`. Production omits the argument (`UI/MainForm_Layout.vb:537`) and `WsMarketDataSource.vb:37-38` falls back to the setting. **The one site both judges agree is in scope, and it carries no class declaration.**
4. [`seat-handover-2026-09-22.md`](../seat-handover-2026-09-22.md) §4 item 2 says `upgradeBonus` was excluded *"though `indicators.OiCvd.upgrade_bonus` is a real key"*. **It names the wrong block** — `BuildBurstCfg` assigns `cfg.Indicators.AggressorVelocity.UpgradeBonus`. Both keys exist, which is what makes the slip easy.

---

## 7. ⚠ What this run does NOT establish

- **That 23 generalises.** It is the whole current population, not a sample — but one population is not a benchmark.
- **What drives the detector's split.** §3 refuted one cause and named no replacement.
- **Whether `FP-Q1` is self-consistent in general.** Three whole-harness runs agreed and two probe cells were 5/5 and 0/5; cell D moved 4/5, so it is **not** deterministic. No broad sampling was done.
- **The five spec-anchored parameters as an independent measurement.** `atr`, `price`, `epochs`, `lr`, `nowUtcMs` have their answers prescribed by [`fixture-parser-check-spec.md`](../fixture-parser-check-spec.md) §7.5 acceptance items 4 and 5, which the seat read before writing the baseline. **All five agree, and all five are declared contaminated.** ⭐ **Excluding them the score is 12 of 18 (66.7 %), and every one of the six misses is in that clean subset** — so the headline understates the gap rather than flattering it.
- **Whether any parameter was shipped only in an untracked `settings.local.json` overlay.**
