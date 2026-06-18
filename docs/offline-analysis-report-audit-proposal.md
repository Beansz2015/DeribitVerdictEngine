# Offline Analysis Report — Resolution-Segmentation Fix + Staleness Audit (Proposal)

**Status:** READY FOR IMPLEMENTER — coordinator audit + spec; **§5 D1–D3 approved by trader 2026-06-18** (incl. the tier-major per-session layout, §2.4). Routes to a fresh Opus implementer; spec-back → coordinator review → local commit.
**Layer:** `analysis/` (offline, host-agnostic, **zero scoring votes / thresholds / vetoes**) → safe, non-approval-gated for the *code change itself*; the §5 decisions are presentation choices the trader should pick.
**Trigger:** trader ran the Analysis Report (`analysis_report_20260618_090018.md`, 893 rows) and flagged *"the ATRs reported in the failure-rate matrix and pending data might be wrong"* + *"check for anything else stale since this function was implemented — we've done many updates since"* (v31 → v37, incl. the v36 resolution split).
**Sibling:** this is the **offline analogue of Phase-2a** (`auto-tweaker-session-resolution-filter-proposal.md`). The auto-tweaker got a `(session × resolution)` population filter on 2026-06-17; the offline report never did.

---

## 0. Scope in one paragraph

The offline `AnalysisRunner` hands `FailureRateMatrix.Compute` **one mixed pile of rows** spanning two execution regimes (1-min NY + 3-min Asia/London), so every failure-rate cell pools two populations with different ATR scales, different session volatility, and — for Asia/London — *provisional-by-design* 3-min ROC thresholds. The headline cell the trader saw (`MEDIUM_LONG 31% @ 15m, n=78`) is **44 NY-1min rows blended with 34 Asia/London-3min rows**. The fix is the offline twin of the Phase-2a tweaker filter: **partition rows by `(session × resolution)` in `AnalysisRunner` and run the matrix once per population.** `FailureRateMatrix.Compute` itself needs **no change** — it is already population-agnostic (the auto-tweaker already hands it a pre-filtered NY×1 set). This proposal also records four secondary staleness checks the trader asked for, with verdicts (one real minor fix, three "not-a-bug, document it").

---

## 1. The problem — the matrix is resolution-blind

### 1.1 What the report does today
`AnalysisRunner.Run` (`analysis/AnalysisRunner.vb:32`) loads all CSV rows, populates forward bars, and calls `FailureRateMatrix.Compute(rows, …)` **once over the entire row set** (`AnalysisRunner.vb:82`). `Compute` classifies each row into a tier (`STRONG_LONG`/`MEDIUM_LONG`/`STRONG_SHORT`/`MEDIUM_SHORT`) and accumulates it into `counts(tier)(window)(threshold)` — **with no session or resolution dimension**. The VerdictContext cross-tab (`AnalysisRunner.vb:98–143`) pools the same way.

`CsvRow.ExecResolution` **is parsed** (v0.7 column 94, `ForwardWindowJoiner.vb:105`, default 1 for legacy) — Phase-2a added it — but `AnalysisRunner` / `FailureRateMatrix` **never read it**.

### 1.2 Empirical confirmation (live `bin/Debug/net8.0-windows/analysis_log.csv`, 992 rows, v0.7)
All 992 rows parse cleanly to 95 fields; the v0.7 schema bump is transparent (header-name indexing). The book is one contiguous post-v36 run (2026-06-17 14:45 → 06-18 09:52 UTC), so every row is post-v35-gate.

- **Resolution mix:** 586 rows `res=1`, 406 rows `res=3`.
- **Session↔resolution is clean** (no legacy contamination): UTC hr 06–09 → `res=3`, hr 14–20 → `res=1`. No `res=1` rows appear in Asia/London hours, so the `ExecResolution` stamp is trustworthy and the whole book is genuinely two-population.
- **The directional cells pool both regimes** (this is the bug, matching the report's `n`):

| Tier | NY × 1-min | Asia/London × 3-min | Pooled `n` (report) |
|---|---|---|---|
| STRONG_LONG | 8 | 3 | 11 |
| STRONG_SHORT | 4 | 0 | 4 |
| MEDIUM_LONG | 44 | 34 | **78** |
| MEDIUM_SHORT | 27 | 0 | 27 |

The headline `MEDIUM_LONG n=78` is 44 NY + 34 Asia/London — exactly the confound the handover predicted.

### 1.3 The two populations are genuinely different distributions
ATR percentiles, all rows:

| | n | p25 | p50 | p75 | p90 | max |
|---|---|---|---|---|---|---|
| `res=1` (NY) | 586 | 46.9 | **63.8** | 110.1 | 136.0 | 228.9 |
| `res=3` (Asia/London) | 406 | 44.4 | **49.5** | 54.0 | 61.7 | 79.1 |

**The res=3 / res=1 median ratio is 0.78×, NOT the 2.1× resolution multiplier** — because the difference here is *session volatility*, not pure resolution scaling. NY (1-min, intrinsically hot, fat right tail to 229) vs Asia/London (3-min, intrinsically cold, tight, capped ~79) happen to land at comparable ATR scales, with NY higher. **This is why the fix must be SEGMENT, not RESCALE** — there is no clean constant to divide out; the populations must be held apart.

### 1.4 Why the v35 floor only *partially* masks it (the honest nuance)
The favourable barrier is floored: `favDist = Max(thr × ATR, floorPct × entry)`, floor ≈ `0.0008 × 65000 ≈ $52`. Measured binding on directional rows:

| population / threshold | floor-dominated | ATR-scaled (binds) |
|---|---|---|
| `res=3`, any threshold (0.3/0.5/0.8×) | ~100% | 0–33% |
| `res=1`, 0.3× | 96% | 4% |
| `res=1`, 0.5× | 66% | 34% |
| `res=1` STRONG, 0.8× | 58% | 42% |

So **right now** the floor pins almost all Asia/London barriers at a fixed $52, which coincidentally neutralises *most* of the favourable-side ATR-scale gap. But:
1. The fat-tail NY rows (34–42% of them) **do** use wider ATR-scaled barriers, so even today a pooled cell mixes fixed-$52 trades with wider-target trades — heterogeneous.
2. This masking is **regime-dependent and fragile**: as BTC volatility rises, `thr × ATR` overtakes the floor and the 3-min vs 1-min barrier-distance gap re-opens. The matrix would then be materially confounded with no warning.
3. The **adverse** barrier is the structural 5m swing stop for all 120 directional rows (report: structural 120 / fallback 0) — resolution-independent, good — but the **favourable** side scales with execution resolution while the stop does not, so the per-row R:R geometry differs systematically by resolution. Another reason not to pool.
4. **Asia/London verdicts are provisional by design** (v36 Phase-1: the 3-min ROC thresholds are the ×2.1 ATR proxy, not measured ROC scaling — "Phase-1 Asia/London verdicts are provisional"). Pooling provisional-threshold verdicts with calibrated NY verdicts into a single failure number is doubly misleading.

**Net:** the trader's instinct is correct. The single number is an average over two regimes that represents neither. Today the distortion is modest (floor masks the favourable side); the *value of the fix* is correctness, robustness as vol rises, per-population honesty, and consistency with the tweaker filter we already shipped.

---

## 2. The fix — segment by `(session × resolution)` in `AnalysisRunner`

**Key insight that shrinks the change:** `FailureRateMatrix.Compute` computes a matrix over **whatever rows it is given**. The auto-tweaker already exploits this — `AutoTweakerCore.vb:408` hands it a `windowRows` set already filtered to NY×1 by the Phase-2a population filter. The **only** caller that hands `Compute` a mixed set is `AnalysisRunner.vb:82`. So the fix lives in `AnalysisRunner` + the report container + the writer. **`FailureRateMatrix.Compute` is unchanged** (strong correctness argument: the matrix engine the tweaker depends on is not touched).

### 2.1 Population key (mirror Phase-2a)
Per row, derive the population exactly as the tweaker does:
```vb
Dim utcHour    As Integer = row.Timestamp.Hour        ' AnalysisLogger logs UTC
Dim bucket     As SessionBucketSettings = ExecutionResolution.MatchSessionBucket(cfg, utcHour)
Dim sessionName As String  = If(bucket IsNot Nothing, bucket.Name, "UNKNOWN")
Dim popKey     As String   = sessionName & "|" & row.ExecResolution.ToString()   ' "NY|1", "ASIA|3", "LONDON|3"
```
- Session is **derived** from the timestamp via the shared `ExecutionResolution.MatchSessionBucket` (engine bucket, ASIA 0–7 inclusive — the same matcher `AutoTweakerCore.vb:806` uses; do NOT reimplement the hour ranges).
- Resolution is the **logged authoritative** `row.ExecResolution` (never re-derived from the hour — matches the Phase-2a contract).
- Group rows into an **ordered** list of populations. Suggested display order: NY×1, LONDON×3, ASIA×3 (highest-data first), then any phantom/`UNKNOWN` populations last.

### 2.2 Run the matrix per population
For each population's row subset, call the **existing** `FailureRateMatrix.Compute(popRows, atrEx, structStop, atrFb, belowMin, cfg.Scoring.MinTradeableMovePct, cfg.Scoring.AtrTargetMultiplier)` and the existing VerdictContext cross-tab loop. Store results per population.

### 2.3 Report container schema change
`AnalysisReport` currently has top-level `FailureCells` / `ContextOutcomes` / the four exclusion counters. Move those into a per-population container; keep genuinely-global fields at the top.

```vb
Public Class PopulationReport
    Public Property PopulationKey       As String        ' "NY|1"
    Public Property SessionName         As String        ' "NY"
    Public Property Resolution          As Integer       ' 1 | 3
    Public Property RowCount            As Integer       ' all rows in this population
    Public Property FailureCells        As New List(Of FailureCellResult)()
    Public Property ContextOutcomes     As New Dictionary(Of String, FailureCellResult)()
    Public Property ExcludedRows         As Integer
    Public Property AtrInvalidExcluded   As Integer
    Public Property BelowMinMoveExcluded As Integer
    Public Property StructuralStopRows   As Integer
    Public Property AtrFallbackRows      As Integer
    ' Caption stats for the per-session table headers (§2.4 requirement 3): ATR
    ' distribution of this population's DIRECTIONAL rows (the rows that feed the
    ' tier matrices) + the $ move-floor. AnalysisRunner computes these from popRows.
    Public Property DirAtrN      As Integer  ' directional-row count in this population
    Public Property DirAtrP25    As Double
    Public Property DirAtrP50    As Double
    Public Property DirAtrP75    As Double
    Public Property MoveFloorUsd As Double   ' cfg.Scoring.MinTradeableMovePct × representative price
End Class
```
`AnalysisReport` keeps `TotalRows`, `VerdictCounts` (global), the three global diagnostics (`FundingDiagnostic`, `OfiAudit`, `OiCvdAudit`), `MarkdownText`/`MarkdownFilePath`/`SummaryCsvPath`, and gains `Public Property Populations As New List(Of PopulationReport)()`. The old top-level barrier fields are removed (only `MarkdownReportWriter` reads them; the auto-tweaker does **not** read `AnalysisReport`, and `AnalysisReportForm` only renders `MarkdownText`).

### 2.4 Markdown layout (`MarkdownReportWriter`) — TIER-MAJOR (trader call 2026-06-18)
Group **verdict-tier first, sessions nested** — so each tier shows NY / ASIA / LONDON side by side, which is the comparison the trader actually makes ("I have tier X — which session is it reliable in"). The writer's outer loop is tier; the inner loop is population. (The underlying segmentation is identical to a population-major layout — only the render order differs.)

```
# Analysis Report — <ts>
> failure-model note (unchanged)

## Global Summary
- Rows in CSV, verdict counts (global)
- Populations detected:  NY×1 n=586  |  LONDON×3 n=…  |  ASIA×3 n=…

## 2. Failure-Rate Matrix
### STRONG_LONG
#### NY · 1-min   · ATR p50=64 (p25–p75 47–110) · move-floor $52
| Window | 0.5× ATR | 0.8× ATR |        ← full grid; ★◆ picked WITHIN this sub-table
#### ASIA · 3-min · ATR p50=50 (p25–p75 44–54) · move-floor $52
| Window | 0.5× ATR | 0.8× ATR |        ← rendered even at n<30 (watch it fill)
#### LONDON · 3-min · (no STRONG_LONG rows yet)
### STRONG_SHORT   (same three sub-tables)
### MEDIUM_LONG    (same)
### MEDIUM_SHORT   (same)

## 3. Recommended (window, threshold)   — per (tier × session)
## 4a. Barrier-Hit Decomposition         — per (tier × session)
## 4. Verdict Context × Outcome          — per session (context-tag rows; barrier-based, so segment)
## 8. Hold Window Selection              — per (tier × session)
## 9. Pending data (n < 30)              — per (tier × session)

## Global Diagnostics
### 5. Funding Momentum Diagnostic   (global — see §4.2)
### 6. OFI Outlier Audit             (global)
### 7. OI×CVD Asymmetry Audit        (global)
```

**Per-session sub-table requirements (all confirmed by the trader 2026-06-18):**
1. **Full grid kept** — do NOT collapse the 0.3/0.5/0.8× ATR threshold columns or the 5/10/15-min window rows. The trader reads individual cells to decide (§4.3).
2. **Resolution label on every sub-table** (`NY · 1-min`, `ASIA · 3-min`, `LONDON · 3-min`) — surfaces the ATR-scale context that pooling hid.
3. **ATR caption per sub-table** — that session's directional-row ATR (`p25–p50–p75` from `PopulationReport.DirAtr*`) + the `$` move-floor (`MoveFloorUsd`). Lets `0.5× ATR` translate to dollars at a glance and makes the floor-collapse legible per session (NY's columns differ where ATR beats the floor; Asia/London's are identical = all floored). This is the direct answer to "I need to see the individual ATR used."
4. **★◆ recommended / most-profitable cell picked WITHIN each (tier × session) sub-table** — not once across the pooled mix. (`FailureRateMatrix.Compute` already marks ★◆ per its row set, so this falls out for free once it is called per population.)
5. **Render the sub-table even when n < 30** (cells show their actual small n) so Asia/London visibly accumulate; a (tier × session) with zero rows renders a one-line "(no rows yet)".

The §5/§6/§7 diagnostics stay **global** (D2). The §3/§4a/§8/§9 sections follow the same tier→session nesting; §4 (context × outcome) segments per session (it is barrier-based) using that session's representative recommended cell. The summary CSV (`BuildSummaryCsv`) gains a leading `Population` column (one row per population × tier × window × threshold).

**Honest consequence to expect:** after segmentation, fewer cells clear the `n ≥ 30` gate. NY×1 MEDIUM_LONG stays n=44 (≥30 ✓); Asia/London split to ~34 and ~0 → mostly "insufficient sample." That is the correct picture and exactly *why* 3-min data is still accumulating — it is not a regression.

---

## 3. Granularity — RESOLVED (D1 approved 2026-06-18: session × resolution)
Every session maps 1:1 to a resolution (NY=1, ASIA=3, LONDON=3), so `(session × resolution)` yields three populations: **NY×1, ASIA×3, LONDON×3**. Approved over `(resolution only)` — consistency with the Phase-2a tweaker filter; ASIA and LONDON are distinct volatility/liquidity regimes (trader-profile + the v34 weekday-confound work).

---

## 4. Secondary staleness checks (the "anything else stale" sweep)

### 4.1 `Below-min-tradeable-move rows excluded: 0` — **NOT A BUG (document it)**
The report shows 0 and the handover flagged it as suspicious. It is **correct** for this book. The offline EXCLUDE removes directional rows whose `engineTargetMult × ATR < floorPct × entry` — i.e. rows the **live v35 gate** would have NO-TRADE'd. But this CSV is entirely post-v35 (earliest row 2026-06-17, post-migration fresh book), so the live gate **already** converted every sub-floor directional signal to `NO TRADE` / `BELOW_MIN_MOVE` *before logging* — none survive as STRONG/MEDIUM rows. The offline EXCLUDE is therefore a **no-op on forward (post-v35) data**; it only bites on *pre-v35 historical* directional rows (which the v31 reset + v36 migration removed). `0` is expected, not stale. **Action:** add a one-line clarification to the §1 Summary text (e.g. "(0 expected for an all-post-v35 book — see audit §4.1)") so it is not re-flagged in future audits. No logic change.

### 4.2 §5 Funding Momentum Diagnostic — **REAL minor staleness (fix)**
`FundingMomentumDiagnostic.Compute` (`analysis/FundingMomentumDiagnostic.vb:48`) hardcodes the "current threshold" it reports against:
```vb
Dim currentThresholdBp As Double = 0.001 * 10000   ' = 10 ; pre-v22 value, NOT the live setting
```
The live `cfg.Indicators.Funding.MomentumThreshold` is **5e-8** since v34 (was 0.0001 at v22). `Compute` does not even take `cfg`, and its recommendation ladder (the `0.5 bp` cutoffs, "lower to 0.0005") is pre-v34 reasoning. **Severity: low** — the 893-row report took the first branch ("p95 < 0.5 bp → genuine REST ceiling, defer to WebSocket"), which is still correct advice and never prints the stale number; the staleness only surfaces in the `Else` branch. **Fix:** thread `cfg` into `Compute(rows, cfg)`, set `currentThresholdBp = cfg.Indicators.Funding.MomentumThreshold * 10000`, and reword the `Else`-branch recommendation to compare the implied threshold against the *live* value. `AnalysisRunner.vb:146` is the single call site.

### 4.3 Threshold-axis degeneracy (0.3× ≡ 0.5× ≡ 0.8×) — **kept + made visible (not collapsed)**
In the flagged report every threshold column within a tier is identical (e.g. MEDIUM_LONG 15m: both 0.3× and 0.5× show 54 success / 2 adverse / 22 expiry). Cause: at current low ATR the `$52` floor dominates *both* multipliers (`thr × ATR < floor` for ≥96% of rows), so all threshold cells collapse onto the same floored barrier — the matrix differentiates only on the window dimension. This is the floor **working as designed**. **Decision (trader, 2026-06-18): keep the full threshold grid** — the trader reads individual cells and the columns re-activate as vol rises. The §2.4 per-session **ATR caption + move-floor** makes the collapse *legible* (the reader can see the floor is binding, and that NY's columns differ only where ATR beats the floor) instead of mysterious — resolving the concern by exposure, not by removing columns. No collapsing, no logic change.

### 4.4 `AdverseFallbackAtrMultiplier = 1.2` hardcoded — **latent drift, low priority**
`AnalysisConstants.AdverseFallbackAtrMultiplier` (1.2) mirrors `cfg.Scoring.AtrStopMultiplier` by hand. It currently matches (v37 left the stop multiplier at 1.2), but it is not read from cfg the way `MinTradeableMovePct` / `AtrTargetMultiplier` now are. Moot for this book (all 120 directional rows used structural stops, fallback=0), so no observable effect. **Action (optional):** while in `Compute`'s neighbourhood, accept it as an optional param defaulting to the constant and pass `cfg.Scoring.AtrStopMultiplier` from `AnalysisRunner`, for parity with the other two live-cfg pass-throughs. Skip if it widens the diff.

### 4.5 §7 OI×CVD = 0/0 INCONCLUSIVE — **expected, not a bug**
OISignal is ~95% NEUTRAL (standing v34 WATCHING item), so confirmed-LONG/SHORT counts of 0/0 are correct. No action.

### 4.6 v0.7 column handling — **correct**
`ForwardWindowJoiner.Load` is header-name-indexed, so the appended `ExecResolution` (col 94) + `CVDWeightedSlope` (col 95) are transparent; all 992 rows parse to 95 fields. `ExecResolution` is read into `CsvRow` (default 1); `CVDWeightedSlope` is not read because the offline pipeline does not need it. No action.

---

## 5. Design decisions — ALL APPROVED (trader, 2026-06-18)

- **D1 — Segmentation granularity: `(session × resolution)`** ✓ → NY×1, ASIA×3, LONDON×3.
- **D2 — Diagnostics scope: global** ✓ → §5 funding / §6 OFI / §7 OI×CVD stay one global set; only the barrier/ATR-based sections segment (matrix, recommended, decomposition, context-tag, hold-window, pending).
- **D3 — Pooled view: DROP the single pooled matrix** ✓, **replaced with a tier-major per-session expansion** (trader refinement). Each tier renders three per-session sub-tables (NY / ASIA / LONDON), full 5/10/15 × ATR-threshold grid each, resolution-labelled, ATR-captioned, ★◆ picked within each sub-table, rendered even at n<30 — see §2.4. The old all-rows-mixed number is gone; the per-population→pooled sum (e.g. 44+34=78) is verified once at acceptance (§7), not shown in every report.

---

## 6. Out of scope (do not conflate)
- **Not** the manual `resolution_profiles["3"]` re-baseline (the Asia/London **accuracy** fix, workstream (B)) — that is data-gated on ≥50 weekday-3-min rows/session and is a settings pass, not this report change. This proposal makes the report *correctly show* per-population numbers; it does not change any threshold.
- **Not** any auto-tweaker change — Phase-2a already filters the tweaker; this is its offline mirror. `FailureRateMatrix.Compute` stays byte-compatible so the tweaker is untouched.
- **No** scoring votes / thresholds / vetoes / CSV-schema changes. `settings.json` is **not** bumped (offline-analysis-only, no config keys). The summary-CSV `Population` column is an additive artifact column.

---

## 7. Acceptance
- `dotnet build` solution + `AutoTweaker.vbproj` + `verify/ordercheck` harness all 0/0; existing harness A1–A15 unregressed (this change does not touch scoring fixtures).
- Regenerate the report against the live book; confirm: separate **NY×1 / LONDON×3 / ASIA×3** blocks; NY×1 MEDIUM_LONG ≈ n=44 (≥30, real number), Asia/London mostly "insufficient sample"; global diagnostics unchanged vs the 893-row run; summary CSV has a `Population` column; `AnalysisReportForm` renders the new markdown without error.
- Spot-check one population's matrix by hand against a few CSV rows (barrier walk on 1-min OHLC) to confirm per-population numbers match the old pooled math restricted to that population.
- Implementer writes a `…-spec-back.md`; **coordinator** (this seat) re-runs the builds + harness, audits the diff, records a `> Coordinator review` callout, local-commits. **Local-first — never push; the trader tests + pushes.**

## 8. Commit checklist (§5 approved — ready to build)
1. `AnalysisReport.vb` — add `PopulationReport` class (incl. `DirAtr*` / `MoveFloorUsd` caption fields) + `Populations` list; remove the migrated top-level barrier fields.
2. `AnalysisRunner.vb` — partition rows by `popKey`; per population, call `FailureRateMatrix.Compute` + the context cross-tab, and compute the directional-row ATR caption stats (`DirAtr*`, `MoveFloorUsd = cfg.Scoring.MinTradeableMovePct × representative price`); populate `Populations`; keep global verdict counts + diagnostics.
3. `MarkdownReportWriter.vb` — **tier-major** render: outer loop tier, inner loop population; per-session sub-tables with resolution label + ATR caption + ★◆ + render-at-n<30 (§2.4); Global Summary + Global Diagnostics; leading `Population` column in `BuildSummaryCsv`.
4. `FundingMomentumDiagnostic.vb` + its `AnalysisRunner` call site — thread `cfg`, read live `MomentumThreshold` (§4.2).
5. (Optional) §4.1 summary-line clarification; §4.4 `AdverseFallbackAtrMultiplier` cfg-pass-through.
6. `architecture.md` / `DeribitIndicatorProject.md` Display-Behaviour or §15 note that the offline report is per-`(session × resolution)`, tier-major, since this change. **No `settings.json` bump.**

---

## 9. Routing
`analysis/`-layer, host-agnostic, no scoring votes → safe to implement once the trader picks §5 (D1–D3). The §5 picks are presentation calls, not scoring design, so they are a light sign-off, not a full approval-gate. Implement in a fresh Opus conversation against this spec; spec-back → coordinator review → local commit. Consult the trader-profile approval gate only if any change reaches into scoring (it does not).
