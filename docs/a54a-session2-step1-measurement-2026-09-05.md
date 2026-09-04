# A54a session 2, step 1 — the re-measurement

**Spec:** [`a54a-json-poco-drift-guard-spec.md`](a54a-json-poco-drift-guard-spec.md) §7.1,
trader-directed 2026-09-05. **Model: Sonnet · Effort: MEDIUM.**

**Status:** ✅ **DONE. NO CODE EDITED** — this is a measurement, not a build. Every claim
below is from reading the tracked tree (`git status` clean throughout), not from memory
or from the inherited figure.

---

## 0. The two headline counts

| | Inherited claim | **Measured 2026-09-05** |
|---|---:|---:|
| Production omissions | 0 | **0** |
| Fixture omissions | 9 | **26** (per call site × parameter — see §4 for the convention note) |

**⛔ The fixture count is NOT 9. It is 26, under the convention §7.1 trap 4 mandates
("per call site × parameter, not per method").** This is a finding, not an error —
reported plainly, not reconciled away. §4 below shows a different, undocumented
convention that *does* reproduce 9 exactly, offered as a likely explanation for where the
inherited figure came from, not as a correction to the 26.

**The production count holds: 0.** No production call site — across `UI/`, `Core/`,
`tools/BacktestRunner`, `ExitGuardEvaluator.vb`, `LiveMicrostructureEvaluator.vb` — omits
an in-scope optional parameter. **The escalation trigger (any production omission ≥ 1)
does NOT fire.** Session 2's dead-code removal, when it happens, is confirmed to be a
behaviour-preserving cleanup on the production side.

---

## 1. The population — re-derived, not trusted

**Starting inventory, reproduced exactly:** `grep -c "Optional "` on the four
`Core/Indicators_*.vb` files gives 4 (Momentum) · 12 (Volatility) · 27 (OrderFlow) · 17
(Structure) = **60**, matching §7.1's own starting figure exactly.

**That 60 is keyword occurrences, not parameters — confirmed, then corrected.** Reading
every line: Momentum, Volatility and Structure's occurrences are all real parameter
declarations (0 comment mentions). **OrderFlow's 27 include 8 comment-only mentions**
(design-note lines like *"CalcTFI: Optional tfiWindowSize As Integer = 30 (was 50,
renamed)"*) — only 19 are actual `Optional` parameter declarations in code.

**Real Optional parameter declarations: 4 + 12 + 19 + 17 = 52.**

**Exclusions, applied and counted:**

| Exclusion class | Parameters | Count |
|---|---|---:|
| `ByRef` output parameters (outputs wearing `Optional` clothing) | `weightedSlopeOut` (`CalcCVD`, `Indicators_OrderFlow.vb:295`); `bestPivotByVolume` / `bestPivotVolumeRatio` / `bestPivotIsHigh` (`CalcSwingPivots`, `Indicators_Structure.vb:264-266`) | 4 |
| `nowUtc` — named ruling exclusion | `GetSessionCandles:27`, `CalcVWAP:47`, `CalcVWAPBands:67` | 3 |
| `maxAgeMs` — named ruling exclusion | `AppendFundingSample:527` | 1 |
| `CalcSpread` discard — named ruling exclusion | `wideThresholdBps` / `tightThresholdBps` (`:561-562`) — see §3 for why this is excluded rather than counted as a production omission | 2 |
| **Total excluded** | | **10** |

**In-scope population: 52 − 10 = 42 parameters, across 14 methods:**

`CalcRSIDivergence`(4) · `CalcVWAP`(2) · `CalcVWAPBands`(2) · `CalcBBW`(2) ·
`CalcTTMSqueeze`(3) · `CalcOFI`(3) · `CalcLiquidations`(1) · `CalcCVD`(5) · `CalcTFI`(2) ·
`CalcMicroCVD`(4) · `CalcOBV`(2) · `CalcVPFRLite`(6) · `CalcSwingPivots`(2) ·
`CalcMTFGate`(4).

**Settings-mirroring confirmed for all 14 methods** by reading the matching `EngineSettings.vb`
class for each (`RsiSettings`, `VwapSettings`, `BbwSettings`, `TtmSettings`, `OfiSettings`,
`LiquidationSettings`, `CvdSettings`, `TfiSettings`, `MicroCvdSettings`, `ObvSettings`,
`VpfrSettings`, `SwingSettings`, `MTFGateSettings`) — every in-scope parameter has a named
`JsonPropertyName`-decorated counterpart.

---

## 2. Call-site enumeration — every site, all 56, read in full (not line-anchored)

**Every file in the repo that references any of these 14 method names was checked** — a
tree-wide sweep (`grep -rn` across `*.vb`, no directory assumptions) returns exactly 9
files: the 4 definition files (`Core/Indicators_*.vb`, confirmed to hold zero internal
cross-calls between these methods) and 5 call-site files: `UI/MainForm_Analysis.vb`,
`tools/BacktestRunner/ReplayLoop.vb`, `ExitGuardEvaluator.vb`,
`LiveMicrostructureEvaluator.vb` (all four **production**, trap 3), and
`verify/ordercheck/Program.vb` (the **only** fixture side). `analysis/` and the rest of
`tools/` (`AutoTweaker`, `CeilingAudit`, `WhatIfRunner`) call none of these 14 methods
directly — checked, not assumed.

**Every call site was read with enough surrounding context to see every argument**, per
trap 1 — several span 4+ lines with trailing named arguments below the fold (`CalcCVD` at
`UI/MainForm_Analysis.vb:432-438`, `CalcMTFGate` at `UI/MainForm_Analysis.vb:505-512`,
etc. — a single-line grep on any of these would see the call and zero of its arguments).

| Method | In-scope params | Production sites | Prod. omissions | Fixture sites | Fixture omissions |
|---|---:|---:|---:|---:|---:|
| `CalcRSIDivergence` | 4 | 2 | 0 | 0 | 0 |
| `CalcVWAP` | 2 | 2 | 0 | 5 | 0 |
| `CalcVWAPBands` | 2 | 2 | 0 | 4 | 0 |
| `CalcBBW` | 2 | 2 | 0 | 0 | 0 |
| `CalcTTMSqueeze` | 3 | 2 | 0 | 0 | 0 |
| `CalcOFI` | 3 | 3 | 0 | 3 | 0 |
| `CalcLiquidations` | 1 | 2 | 0 | 0 | 0 |
| `CalcCVD` | 5 | 3 | 0 | 1 | **5** |
| `CalcTFI` | 2 | 4 | 0 | 2 | **3** |
| `CalcMicroCVD` | 4 | 3 | 0 | 2 | **8** |
| `CalcOBV` | 2 | 2 | 0 | 2 | **2** |
| `CalcVPFRLite` | 6 | 2 | 0 | 0 | 0 |
| `CalcSwingPivots` | 2 | 4 | 0 | 0 | 0 |
| `CalcMTFGate` | 4 | 2 | 0 | 2 | **8** |
| **Total** | **42** | **35** | **0** | **21** | **26** |

### 2.1 Every omitting call site, named — the 26 in full

| # | Method | Call site | In-scope params supplied | Omitted |
|---|---|---|---|---|
| 1 | `CalcCVD` | `verify/ordercheck/Program.vb:667` (`A1`) | 0 of 5 | `slopeMinUsd`, `slopePctOfValue`, `divergencePriceGate`, `lateSegmentWeight`, `earlySegmentWeight` — **5** |
| 2 | `CalcTFI` | `Program.vb:743` (`A4`) | 0 of 2 | `tfiWindowSize`, `threshold` — **2** |
| 3 | `CalcTFI` | `Program.vb:7042` (`A43b_SliceTradesAscendingAndLastN`) | 1 of 2 (`tfiWindowSize:=30`) | `threshold` — **1** |
| 4 | `CalcMicroCVD` | `Program.vb:697` (`A2`) | 0 of 4 | all four — **4** |
| 5 | `CalcMicroCVD` | `Program.vb:720` (`A3`) | 0 of 4 | all four — **4** |
| 6 | `CalcOBV` | `Program.vb:791` (`A6`) | 1 of 2 (`trendGate:=10.0`) | `divergenceGate` — **1** |
| 7 | `CalcOBV` | `Program.vb:792` (`A6`) | 1 of 2 (`trendGate:=10.0`) | `divergenceGate` — **1** |
| 8 | `CalcMTFGate` | `Program.vb:929-930` (`A9`) | 0 of 4 | all four — **4** |
| 9 | `CalcMTFGate` | `Program.vb:1302` (`A14h`) | 0 of 4 | all four — **4** |

**Sum: 5+2+1+4+4+1+1+4+4 = 26.** All nine of these are fixture call sites. All 35
production call sites — 0 exceptions — supply every in-scope parameter, whether by name
or positionally (trap 2: `CalcVWAP`/`CalcVWAPBands`/`CalcOBV`'s production sites pass
`session2Hour`/`session2Minute`/`trendGate`/`divergenceGate` positionally, not by name,
and were checked as supplied, not flagged as omitted for lacking a `:=`).

---

## 3. The `CalcSpread` trap, resolved — not silently applied

§7.1 warns: *"both `CalcSpread` parameters DO have settings counterparts... so read
`Indicators_OrderFlow.vb:558-562` before deciding what the 'discard' refers to rather than
assuming."*

**Read, and traced to a real call site.** `LiveMicrostructureEvaluator.vb:132-135`
(production, host-agnostic, feeds the live TAPE strip):

```vb
' Spread — the SpreadBps formula via CalcSpread (only the bps value is read; the bps is
' threshold-independent, so the TIGHT/NORMAL/WIDE status args are left at their defaults).
Dim sBps As Double = 0, sStatus As String = "NORMAL"
IndicatorEngine.CalcSpread(book, sBps, sStatus)
```

**This IS a production call site that omits both `wideThresholdBps` and
`tightThresholdBps`.** Without the ruling's exclusion, this would be 2 production
omissions and would trip the escalation trigger outright. **It does not, because §7's own
exclusion list names it explicitly** ("the documented `CalcSpread` discard alone") — this
is that discard: the comment confirms `sBps` (the only field the caller reads) does not
depend on the threshold arguments at all, only `sStatus` (discarded, never read — the
caller uses `HasTopOfBook(book)` for its own separate flag) does. **Both `CalcSpread`
parameters are therefore excluded from the in-scope population entirely** — not just at
this call site — consistent with how `maxAgeMs`/`nowUtc` are excluded as parameters, not
per-call-site. The other production call site
(`UI/MainForm_Analysis.vb:422-424`) supplies both by name regardless, so this exclusion
changes nothing there.

---

## 4. Reconciling the inherited "9" — an explanation, not a correction

§7.1 trap 4 requires the per-call-site × parameter convention, which measures **26**. A
different, unstated convention — **count of distinct call sites carrying at least one
omission, regardless of how many parameters** — reproduces **9** exactly: the nine rows
in §2.1's table (`A1` once, `A4`+`A43b` twice for `CalcTFI`, `A2`+`A3` twice for
`CalcMicroCVD`, `A6` twice for `CalcOBV`, `A9`+`A14h` twice for `CalcMTFGate`). **This is
very likely where the inherited figure came from** — it is an exact match, not a rough
one — but it is not the convention §7.1 asks this measurement to use, and per-method
counting (5 methods carry ≥1 fixture omission) does not reach 9 either. **Reported for
the record; the headline figure for this measurement is 26, per call site × parameter, as
instructed.**

---

## 5. Escalation trigger — checked, not fired

§7.1: *"Any production omission at all... report the count and stop."* **Production
omissions measured: 0.** No further action is required or taken. No call site was edited
to "fix" an omission — none needed fixing, and doing so mid-measurement would in any case
have been the exact behaviour-preserving edit §7.1 explicitly forbids performing
unilaterally.

---

## 6. What was verified, and how

- **Every one of the 60 raw keyword occurrences was individually classified** (comment vs.
  code, in-scope vs. excluded) by reading the source, not by pattern-matching alone.
- **Every one of the 14 in-scope methods' settings-mirroring was confirmed** by reading the
  matching `EngineSettings.vb` class and its `JsonPropertyName` attributes.
- **All 56 call sites were read with full surrounding context** (not line-anchored),
  confirming argument supply positionally or by name per trap 2.
- **A tree-wide sweep** (`grep -rn` with no directory scoping) confirmed no call site
  exists outside the 9 files examined.
- **The `CalcSpread` discard was traced to its actual call site and read**, not assumed
  from the ruling's summary alone.

**Not verified / out of scope for step 1:**

- Whether removing the `Optional` defaults is otherwise safe (VB positional-parameter
  ordering, compile impact) — that is step 2's own job, not this measurement's.
- The R-2/R-3 follow-up (`docs/a54a-r2-r3-followup-spec.md`) — separate, already
  build-authorized, not bundled here per the instruction.
