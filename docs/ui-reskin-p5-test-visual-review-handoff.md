# UI Reskin P5-test — Visual Review Handoff

**Purpose:** Hand off the visual-review findings from the trader's screenshot pass over the harness's 55 artifacts. Intended audience: spec author drafting the fix spec (likely titled `docs/ui-reskin-p5-test-gap-fixes-proposal.md` per kickoff §5.3). Implementer of the fix spec will be a fresh Opus 4.7 High conversation, not the current one.

**Source conversation:** Opus 4.7 High that built the P5-test render-parity harness (commits `db1675c` → `f54607f`).

**Date:** 2026-05-27.

**Status of harness work:** Commits 1 + 2 + B6 fix shipped; 55/55 PARITY confirmed on every run including post-fix. Commit 3 (cleanup) **deferred** until the fix spec is implemented and re-verification passes — see §5.

---

## 1. Background

### 1.1 What the P5-test harness is

Temporary render-parity test harness that drives both:
- **Legacy:** `MainForm.RenderOutput(r, v, norms, vwapWarmup, lastTradePrice)` → writes RTF to `txtOutput`
- **New (snapshot):** `MainForm.BuildPlaintextSnapshot(v, r, norms, cfg, vwapWarmup, lastTradePrice)` → returns plain text

…over a curated set of 55 synthesized `(IndicatorResults, VerdictResult, DynamicNorms)` triples, captures text artifacts + screenshots per case in `verify/p5-test/`, and produces a discrepancy report (`test-results.md`). Triggered via the hidden `Ctrl+Shift+T` hotkey.

Spec source: `docs/ui-reskin-p5-test-harness-kickoff.md` (with §0.6 addendum).

### 1.2 What the harness was supposed to find

Per kickoff §5.2, the harness should surface three categories of discrepancy:
1. **Missing in cards** — legacy text shows X, snapshot agrees, card screenshot doesn't visibly show X → card binding needs to surface X
2. **Missing in snapshot** — legacy text shows X, snapshot omits → `BuildPlaintextSnapshot` shape bug
3. **Test case bug** — synthesized data not coherent

The text-level harness diff (legacy `txtOutput.Text` vs `BuildPlaintextSnapshot`) caught category 2 and 3. **Category 1 surfaces via the trader's visual review of the 55 PNG screenshots**, which is what this doc handles.

---

## 2. Report-back on completed harness work

### 2.1 Commits shipped (local-only, not pushed)

| Hash | Subject | Notes |
|---|---|---|
| `db1675c` | `feat(test): P5-test — render parity harness scaffolding` | Commit 1. `UI/MainForm_TestHarness.vb` partial + `Ctrl+Shift+T` ElseIf branch in existing `OnFormKeyDown` per kickoff §0.6 (added at `MainForm_Layout.vb:1286-1289`). 5 sentinel cases. Avoided needing `_testHarnessMode` guards entirely — discovered all three side-effect calls (`AnalysisLogger.LogRun`, `LivePerformanceTracker.UpdateAsync`, `AnalysisOutputDump.Append`) live in `RunAnalysisAsync` post-P5a, not inside `RenderOutput`. Kickoff §1.2 table was stale on this. |
| `10eafab` | `fix(test): P5-test — populate SignalBreakdown in NeutralVerdict` | Intermediate trivial-fix (auto-populated 21 empty rows). **Superseded by `303ba51`** but preserved in history for traceability. |
| `303ba51` | `fix(test): P5-test — populate breakdown items per case (supersedes 10eafab)` | Per spec-author guidance: per-case explicit `SignalBreakdown` population via new `WithBreakdownItem(label, longHit, shortHit, note)` + `WithBreakdown(items)` fluent setters. Sentinels now build the full 21-row roster with realistic hit patterns. |
| `8507fd4` | `feat(test): P5-test — full test case library (40-60 cases) + spec-back` | Commit 2. New `UI/TestHarnessCases.vb` (990 LoC, 55 cases) + extended `TestCaseBuilder` with 27 additional fluent setters + `SignalBreakdownPresets` helper class (7 roster builders). Runner switched from `BuildSentinelCases()` to `TestHarnessCases.BuildAll(SettingsLoader.Current)`. Includes full spec-back at `docs/ui-reskin-p5-test-spec-back.md` with branch-arm gap audit. |
| `f54607f` | `fix(test): P5-test — flip NeutralVerdict HoldStatus default to suppress empty HOLD/EXIT lines` | Per spec-author B6 directive (test-side gap closure). Flipped `NeutralVerdict.HoldStatus` from `""` to `"N/A -- no open position"` so the RenderOutputHeader gate suppresses by default. Post-fix re-run confirmed 55/55 PARITY preserved. |

### 2.2 Verification results

- **Latest harness run:** 55/55 PARITY, zero text-level discrepancies.
- **CRLF-normalized byte-identical** confirmed on representative cases via `diff <(tr -d '\r' < legacy) <(tr -d '\r' < snapshot)`.
- **Branch-arm coverage:** ~106 of ~115 enumerated arms substantively exercised (~92%). The remaining ~8% split per spec-back §4:
  - 3 test-side gaps (B2 empty `VerdictContext`, B6 HOLD/EXIT default leak, B19 VWAP s2 anchor). B6 closed via `f54607f`. B2 + B19 accepted by spec author.
  - 4 Type B engine-side unreachables (B11 KELLY suppression dead code, B13 KELLY no-`[CAPPED]` arm, B14 KELLY Contracts≥1 + Lean≥1 arms). Deferred to a separate `post-p5b-engine-hygiene-proposal.md` per spec-author sign-off.

### 2.3 Confidence statement (spec-author-confirmed)

The harness confirms "the new design will not be missing anything that the legacy output gives" for the **~92% of arms the harness exercises at the text level**. The remaining ~8% are either production-unreachable (Type B) or trivially closable test-side cosmetics. **No documented arm exists where the new design might silently drop text-level information that legacy renders.**

The visual-review pass that this doc hands off addresses the **card-vs-legacy** layer that the text-level diff cannot catch. The trader's findings are the expected output of that second-pass review.

### 2.4 Artifacts available

- `verify/p5-test/test-results.md` — the PARITY report
- `verify/p5-test/{NN}_{name}-legacy.txt` — 55 RTF-stripped legacy outputs
- `verify/p5-test/{NN}_{name}-snapshot.txt` — 55 snapshot outputs
- `verify/p5-test/{NN}_{name}.png` — 55 full-form screenshots
- `docs/ui-reskin-p5-test-harness-kickoff.md` — original spec
- `docs/ui-reskin-p5-test-spec-back.md` — full spec-back (209 lines)
- `UI/TestHarnessCases.vb` (990 lines) — the case library
- `UI/MainForm_TestHarness.vb` — scaffolding + extended `TestCaseBuilder`
- `tools/send-ctrl-shift-t.ps1` — UIA helper for AI-driven harness dispatch

---

## 3. Trader's visual review findings

The trader performed a screenshot-by-screenshot review of all 55 cases and produced two source files that this doc consolidates. Both are committed at `docs/` for reference:

- `docs/VisualReviewQuestions.txt` (29 lines, 21+ items observed against `01_strong_long_full_confluence-snapshot.txt` + `.png`)
- `docs/ContentRequestsAfterVisualReview.txt` (47 lines, format guidelines + content change requests)

### 3.1 Trader's general formatting guidelines

The trader's guidelines (from `ContentRequestsAfterVisualReview.txt` lines 4–13) should be treated as **binding constraints across the whole fix spec**:

1. Keep content of the same item on the same row if possible.
2. Label numbers as much as possible — trader shouldn't have to guess which number is which reading.
3. Within a row, don't use spaces to separate label/number pairs — use `=` (e.g., `H=2.00x`) and `|` between label/number sets (e.g., `ATR=80.00 | ref=80.00`).
   - For the row's own header label, `:` is fine (e.g., `ADX: 32.5` is correct).
4. Bracket all timeframe mentions: `(1m)`, `(5m)`, `(15m)`.
5. Retain full names from legacy output if there is space.
6. Retain as much of the label's info from legacy output if there is space.
7. Capitalize the starting characters in the "Note" column of SIGNAL BREAKDOWN (e.g., `none` → `None`).
8. Font / abbreviation changes affect all corresponding content — if you change `NEW LONGS` → `NW LNGS`, the same abbreviation must apply to `NEW SHORTS` → `NW SHRTS`.

### 3.2 VisualReviewQuestions.txt — raw item list

(Reproduced verbatim for spec-author convenience. Reference snapshot: `01_strong_long_full_confluence-snapshot.txt`; reference screenshot: `01_strong_long_full_confluence.png`. All change requests apply to the card UI, not to the snapshots.)

1. TIME for both legacy and snapshot always shows `2026-01-01 12:00:00 UTC+8`. The legacy output in the screenshot also shows the same, so something appears to have broken before this.
2. Missing `(risk 96.0 / rwd 160.0)` and `(risk 300.0 / rwd 300.0)` under ATR Entry Levels — can't find in screenshot.
3. SIGNAL BREAKDOWN section:
   - **a.** Volume — add BTC volume to the NOTE column, format like `"1.60x SMA | $8.0M | 100.0000 BTC"`.
   - **b.** `Donchian(20): Upper=50100.0 Lower=49600.0` — prices shown in snapshot but not findable in screenshot.
     - i. Snapshot shows `breakout up` in SIGNAL BREAKDOWN but screenshot shows `upper qtr`. Expected?
   - **c.** OBV shows `BULL` (not `RISE`) and `no div` in NOTE column, but snapshot shows `RISING` only. Expected?
   - **d.** Trend Str shows `HH/HL` in NOTE column but snapshot shows `(HH 50050.0>49920.0 | HL 49900.0>49810.0)`. Shown elsewhere in screenshot?
   - **e.** `CVD: Net:12500 | Slope:RISING | Div:NONE` in snapshot. Screenshot doesn't show `Div:NONE` — does the card show DIV when it's something other than NONE?
   - **f.** RSI(9) shows `64.0` in NOTE column but snapshot shows `64 > 50`. Modify to `64.0 > 50.0`?
   - **g.** **Possible scoring engine bug:** Screenshot's DMI/ADX gives a +1, but snapshot shows +1 for each of two separate events (`DMI +/-DI` = +DI 28 > -DI 14 AND `ADX>22`). Shouldn't screenshot show +2?
   - **h.** VWAP Bands — visible in screenshot but not in snapshot. Does it affect scoring but wasn't shown in SIGNAL BREAKDOWN snapshot?
   - **i.** BBW/TTM — screenshot shows `none` in NOTE column but snapshot shows `BULL_BUILDING`. Expected?
   - **j.** EMA Ribbon — screenshot shows `9>21>50`; snapshot shows the meaning `BULL alignment`. Modify to `9>21>50 = BULL alignment`?
   - **k.** VPFR shows `BULL` in screenshot but `NEUTRAL` in snapshot. Possible bug?
   - **l.** Snapshot shows MTF Gate (15m) in SIGNAL BREAKDOWN appears to be scored, but no MTF row in screenshot's SIGNAL BREAKDOWN. Expected?
   - **m.** Swing Pivots — `Best Vol Pivot 5m: HIGH 50050.0 (vol×1.8 vs avg pivot)` in snapshot but screenshot shows `5m+15m`. Always represented 5m+15m but unlabelled? Or does it mean the best vol pivot of BOTH 5m/15m? Or adjusts based on which has the best vol pivot? OR Swing Pivot doesn't represent Best vol pivot and only the cyan `best vol: HIGH @ 50050.0 (1.8x)` does?
4. VOLUME PROFILE section:
   - **a.** Snapshot shows `VPFR-lite: POC:49950.0 | NEAR_HVN_SUPPORT | HVN@POC:NO` but `HVN@POC:NO` not shown anywhere in screenshot. Necessary?
   - **b.** `HVN walls: Above:50200.0 Below:49800.0 | LVN: ^— v—`:
     - i. What does `LVN: ^— v—` mean and is it represented in the screenshot?

### 3.3 ContentRequestsAfterVisualReview.txt — raw item list

(Reproduced verbatim.)

**ATR ENTRY LEVELS section:**
1. **a.** `R:R 1:1.0` is positioned between STOP and ENTRY. What happens when there's a CAPPED between STOP and ENTRY? No sample exists — does the engine even cap stops?
1. **b.** A CAPPED appears below ENTRY (e.g. `CAPPED @ 72952.2 (NEAREST_HVN_ABOVE)`). Will the price shown always be the same as the CAPPED between ENTRY and TARGET? If yes, remove and append the note to the CAPPED between ENTRY and TARGET. If not, leave it.

**Structural (Long/Short) sections:**
2. **a.** Align the stop/entry/target as per ATR Entry Levels format. Current: `STRUCT STOP | STRUCT TARGET | ENTRY`. Wanted: `STRUCT STOP | ENTRY | STRUCT TARGET`.

**Indicator Details sections:**
3. **a.** Redraw box borders around each sub-section to be the same width as each other.
3. **b.** Align the headers of each sub-section on both left/right sides. OK if there's a gap from one sub-section's end to the next. Example: `MTF GATE 15m . PASS` should be horizontally aligned with `VWAP` and their corresponding sub-sections. Right sub-sections should follow the left sub-section headers as priority.
3. **c.** `NORMS [LIVE]`:
   - i. Should be `DYNAMIC NORMS [LIVE]`.
   - ii. `Vol H/M: 2.00 x / 1.3x` → `Vol threshold: H=2.00x M=1.30x`.
   - iii. ATR scale should be: `ATR scale: 1.00x (ATR=80.00 | ref=80.00)`.
3. **d.** `REGIME 5m . TRENDING_UP` → `REGIME (5m) . TRENDING_UP`.
3. **e.** `BBW / TTM` → `BBW / TTM SQUEEZE`.
3. **f.** `EMA RIBBON . BULL` → `EMA RIBBON (1m) . BULL`.
3. **g.** `MTF GATE 15M . PASS` → `MTF GATE (15m) . PASS`.
3. **h.** FUNDING:
   - i. `Config: en: Y soft:+1 amp:-1` → `Config: Enabled:Y|Soften:+1|Amplify:-1`.

**SIGNAL BREAKDOWN section:**
4. **a.** `Donchian` → `Donchian(20)`.
4. **b.** `best vol: HIGH @ 50050.0 (1.8x)` → `Best vol. pivot (5m) : HIGH @ 50050.0 (vol x 1.8 vs avg. pivot)`.
4. **c.** OI Change:
   - i. `NEW LO` → `NW LNGS` (and vice versa `NEW SH` → `NW SHRTS`).
4. **d.** `EMA200 5m` → `EMA200 (5m)`.

**VOLUME PROFILE section:**
5. **a.** `near hvn support` → `Near HVN support` (should be coloured).
5. **b.** `inside va` → `Inside VA`.

**DMI/ADX NOTE column:**
6. Shows `ADX 32.5`. Should show `ADX=32.5 | +DI=28.0 | -DI=14.0`.

---

## 4. Triage

The triage categorizes every item by what it actually requires. Code references are grounded in the actual card-binding code spelunked during triage.

### 4.1 Categorization table

| # | Source | Item | Cat | Evidence / notes |
|---|---|---|---|---|
| Q1 | Visual | TIME stuck at `2026-01-01 12:00:00 UTC+8` | **INVALID** | `NeutralVerdict` deliberately pins `v.Timestamp = New DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)` (at `UI/MainForm_TestHarness.vb` in `NeutralVerdict` factory) so both renderers produce byte-identical timestamps for clean diffing. Production sets `verdict.Timestamp = DateTime.Now` at `UI/MainForm_Analysis.vb:440`. Not a bug — by harness design. The legacy screenshot shows it because both renderers read the same synthesized `v.Timestamp`. **No action.** |
| Q2 | Visual | Missing `(risk N / rwd N)` under ATR Entry Levels in card | **VALID — binding gap** | `BindAtrRow` at `UI/MainForm_Render_Cards.vb:916` emits `R:R` value but doesn't surface the underlying risk / reward USD amounts that legacy `RenderOutputHeader` emits at line ~172. Add risk/rwd to the ATR card. |
| Q3a | Visual | Volume NOTE — add BTC + USD breakdown | **VALID — format** | `BuildRowVolume` at `MainForm_Render_Cards.vb:2522` currently shows `"{ratio:F2}× {USD}"`. Add BTC volume per trader request. |
| Q3b | Visual | Donchian raw upper/lower prices not in card | **VALID — binding gap** | Card shows derived quartile, not raw prices. Belongs either as additional content in `BuildRowDonchian` or in INDICATOR DETAILS card. |
| Q3b.i | Visual | `upper qtr` (card) vs `breakout up` (snapshot SIGNAL BREAKDOWN) | **BY DESIGN — clarify** | `BuildRowDonchian` at `MainForm_Render_Cards.vb:2769` computes quartile zone from current price position vs upper/lower. The snapshot's note comes from the scoring engine's breakdown emission. Different summaries of related info. |
| Q3c | Visual | OBV shows `BULL` / `no div` (card) vs `RISING` (snapshot) | **BY DESIGN — could align per §G5** | `BuildRowObv` at `MainForm_Render_Cards.vb:2783` maps `RISING → BULL` pill + brief note. Per trader's guideline §G5 ("retain full names from legacy if there is space"), could revert to `RISING`. |
| Q3d | Visual | Trend Str shows `HH/HL` (card) vs `(HH 50050.0>49920.0 | HL 49900.0>49810.0)` (snapshot) | **VALID — format per §G6** | Trader's guideline §G6 ("retain label info from legacy if there is space"). |
| Q3e | Visual | `CVD Div:NONE` not in card | **VALID — investigate** | Card likely suppresses divergence when `NONE`. Confirm that when divergence is `BULLISH` / `BEARISH`, the card surfaces it. |
| Q3f | Visual | RSI `64.0` (card) vs `64 > 50.0` (snapshot) | **VALID — format per §G2** | Trader's guideline §G2 (label numbers). |
| Q3g | Visual | **DMI/ADX shows +1 in card but snapshot shows +1 for two separate events** | **DISPLAY BUG (card-side, not engine)** | `BuildRowDmiAdx` at `MainForm_Render_Cards.vb:2502-2505` literally does `Dim sc As Integer = ScForItem(items, "DMI +/-DI") + ScForItemPrefix(items, "ADX>")  / If sc > 1 Then sc = 1 / If sc < -1 Then sc = -1`. The card sums DMI + ADX into the combined row, then **clamps the sum to [-1, +1]**. Engine correctly emits two separate items at +1 each (contributing +2 to `LongScore`). The card display under-reports the contribution. **Fix:** uncap the clamp so combined rows show summed SC, OR split DMI and ADX into two separate card rows to mirror the snapshot. **Not an engine bug** — engine is correct. |
| Q3h | Visual | VWAP Bands in card but not snapshot | **BY DESIGN** | `BuildRowVwapBands` at `MainForm_Render_Cards.vb:2547` is a display-only row (`SC=0`). VWAP score counted on the VWAP Dev row above. Comment in code (line 2548) documents this. |
| Q3i | Visual | BBW shows `none` (card) vs `BULL_BUILDING` (snapshot) | **BY DESIGN — could surface both per §G6** | Card displays `SqueezeStatus` field; snapshot's `BULL_BUILDING` is the `TTMSignal` field. Different fields, both valid information. Could merge or display both. |
| Q3j | Visual | EMA Ribbon — add `= BULL alignment` to card | **VALID — format per §G6** | Per explicit trader request + guideline. |
| Q3k | Visual | VPFR `BULL` (card) vs `NEUTRAL` (snapshot) | **BY DESIGN — clarify** | `BuildRowVpfr` at `MainForm_Render_Cards.vb:2800-2806` maps `r.VPFRSignal = "NEAR_HVN_SUPPORT"` → directional pill `BULL`. The snapshot's SIGNAL BREAKDOWN row reflects scoring outcome (the VPFR signal didn't fire as a +1/-1 hit in this case). Different concepts: indicator state vs scoring hit. Both correct. |
| Q3l | Visual | MTF Gate row in snapshot SIGNAL BREAKDOWN but absent from card SIGNAL BREAKDOWN | **BY DESIGN** | Card's `BindCardSignalBreakdown` at `MainForm_Render_Cards.vb:2088-2199` intentionally omits an MTF Gate row from the breakdown table. MTF gate has its own dedicated section in the INDICATOR DETAILS card. Not a gap — relocated. Trader should verify MTF info is present there. |
| Q3m | Visual | Best Vol Pivot 5m (snapshot) vs `5m+15m` (card) | **BY DESIGN — clarify** | `BuildRowSwingPivots` at `MainForm_Render_Cards.vb:2811-2837` has two layers: (a) the main row's state pill = `5m+15m` indicates **availability** of pivots on each timeframe; (b) the sub-note row underneath = `best vol: {direction} @ {price} ({ratio}×)` shows the actual best volume pivot. Trader may have missed the sub-note row. Both pieces of info ARE present. |
| Q4a | Visual | `HVN@POC:NO` not in card | **VALID — surface or omit** | Trivial decision: trader preference. |
| Q4b | Visual | `LVN: ^— v—` meaning | **CLARIFICATION** | `^` = LVN-above, `v` = LVN-below, `—` = none found within range. Card surfaces walls differently — needs alignment per general guidelines. |
| C1a | Content | ATR stop CAPPED handling | **ENGINE QUESTION** | Engine currently has `AdjustedLongTarget` / `AdjustedShortTarget` but no equivalent for stops. To cap stops at structural support/resistance would require engine work. Spec author must decide: add stop capping, or document that stops are uncapped. |
| C1b | Content | Duplicate CAPPED labels | **VALID — investigate** | Need to inspect a real CAPPED screenshot to confirm where the second CAPPED label renders. If duplicate of the in-line one, can be removed. |
| C2a | Content | STRUCT row alignment to match ATR row format | **VALID — layout** | Re-layout STRUCT row columns. |
| C3a | Content | Equalize INDICATOR DETAILS sub-section box widths | **VALID — layout** | Per trader request. |
| C3b | Content | Align INDICATOR DETAILS sub-section headers L/R | **VALID — layout** | Per trader request. |
| C3c.i | Content | `NORMS [LIVE]` → `DYNAMIC NORMS [LIVE]` | **VALID — label** | Per §G5. |
| C3c.ii | Content | `Vol H/M: 2.00 x / 1.3x` → `Vol threshold: H=2.00x M=1.30x` | **VALID — format per §G3** | |
| C3c.iii | Content | ATR scale row reformat | **VALID — format per §G3** | |
| C3d–g | Content | Add timeframe brackets `(5m)`, `(1m)`, `(15m)` | **VALID — format per §G4** | Multiple section headers. |
| C3e | Content | `BBW / TTM` → `BBW / TTM SQUEEZE` | **VALID — label per §G5** | |
| C3h.i | Content | FUNDING Config row reformat | **VALID — format per §G3** | |
| C4a | Content | `Donchian` → `Donchian(20)` in SIGNAL BREAKDOWN | **VALID — label per §G4** | |
| C4b | Content | Best vol. pivot row full expansion | **VALID — format per §G5** | |
| C4c.i | Content | `NEW LO` → `NW LNGS` abbreviation rule | **VALID — format per §G8** | Must apply symmetrically to all permutations. |
| C4d | Content | `EMA200 5m` → `EMA200 (5m)` | **VALID — format per §G4** | |
| C5a | Content | `near hvn support` → `Near HVN support` + colour | **VALID — format per §G7** | |
| C5b | Content | `inside va` → `Inside VA` | **VALID — format per §G7** | |
| C6 | Content | DMI/ADX NOTE column expansion | **VALID — format** | Related to Q3g (combined row). The two should be designed together. |

### 4.2 Category summary

| Category | Count |
|---|---|
| **INVALID** — no action, explain to trader | 1 (Q1 TIME) |
| **BY DESIGN — clarify** — explain rationale to trader, decide if alignment to legacy is wanted | 6 (Q3b.i, Q3c, Q3h, Q3i, Q3k, Q3l, Q3m — Q3c/Q3i could become format-fix work if trader confirms) |
| **VALID — fix needed (binding gap or format)** | ~32 (most of Q2–Q4 + most of C2–C6) |
| **DISPLAY BUG (card-side)** | 1 (Q3g — DMI/ADX clamp logic) |
| **ENGINE QUESTION** | 2 (C1a stop CAPPED, plus Q3l/Q3k/Q3m if reinterpreted) |

### 4.3 Items to clarify with the trader before drafting

These items can't be drafted cleanly without trader input — the spec author should ask or assume sensible defaults:

- **Q3c, Q3i** — accept the card's brevity (state pill + short note), or revert to legacy's longer string per §G5?
- **Q3k** — card's directional pill (`BULL` from `NEAR_HVN_SUPPORT`) IS useful information that the snapshot doesn't surface this way. Keep both, or align them?
- **Q3l** — MTF Gate row in SIGNAL BREAKDOWN snapshot but separate MTF section in card. Add MTF row to card SIGNAL BREAKDOWN, or leave separated as today?
- **Q3m** — trader saw `5m+15m` but missed the best-vol-pivot sub-note. Visual emphasis problem? Sub-note may need to be more prominent.
- **Q4a** — surface `HVN@POC:NO` in card or omit as redundant?
- **C1a** — engine work: cap stops at structural levels, or leave uncapped? This is the only item that could touch the scoring engine.
- **C1b** — investigation: confirm whether the second CAPPED label always duplicates the first, before deciding to dedupe.

---

## 5. Recommendations on path forward

### 5.1 Single fix spec with triage prefix

**Yes — one spec covers both files.** They're interconnected (Q3g and C6 are the same DMI/ADX issue; trader's general guidelines apply across multiple items). Suggested filename: **`docs/ui-reskin-p5-test-gap-fixes-proposal.md`** (matches kickoff §5.3's anticipated naming).

Suggested spec structure:

1. **Triage prefix** — adopt §4 of this doc, marking each item as in-scope / out-of-scope-but-documented / clarification-needed.
2. **General formatting guidelines** — restate trader's 8 rules from §3.1 as binding constraints.
3. **Fixes grouped by card:**
   - ATR ENTRY LEVELS card (Q2, C1a, C1b)
   - STRUCTURAL (Long/Short) cards (C2)
   - INDICATOR DETAILS card (C3a–h)
   - SIGNAL BREAKDOWN card (Q3a–m, C4, C6)
   - VOLUME PROFILE card (Q4, C5)
4. **One engine question** (C1a stop CAPPED) — explicitly flagged for trader decision.
5. **Verification plan** — re-run harness (parity must still be 55/55) + trader's second visual pass.

### 5.2 Fresh conversation for fix implementation

**Strongly recommend a new Opus 4.7 High conversation** for the fix implementation. Reasons:
- Current conversation's context is full of harness-construction state irrelevant to card-binding edits.
- Fix work is substantial — likely 20+ card-binding edits across `UI/MainForm_Render_Cards.vb` (file spans line 700 to ~2900).
- A clean conversation reads the spec fresh and approaches the cards systematically.
- Per kickoff §5.3: *"Fix spec ships as its own implementation conversation (probably Medium effort)."*

### 5.3 Commit 3 cleanup — defer to the fix-implementation conversation

Don't ship commit 3 from the current conversation. Reasons:
- The harness IS the verification mechanism for the fix work. Deleting it now means the fix conversation has to re-create it or do worse manual verification.
- Per kickoff §5.4: *"After the fix spec lands: re-run the harness, confirm clean, ship commit 3 cleanup."*
- The fix-implementation conversation naturally owns the cleanup step as its closing act.

### 5.4 Recommended workflow

1. **Spec-author conversation** — read this handoff, draft `docs/ui-reskin-p5-test-gap-fixes-proposal.md`.
2. **New Opus 4.7 High conversation** — implement the fix per the spec. ~20+ edits to `UI/MainForm_Render_Cards.vb`. May touch `MainForm_PlaintextSnapshot.vb` if any snapshot changes are deemed necessary (probably none, since text parity is already clean).
3. **In the fix conversation:** re-run the harness via `Ctrl+Shift+T`. Verify text-level PARITY still 55/55.
4. **Trader visual sign-off** on the new screenshots in `verify/p5-test/*.png`.
5. **In the same fix conversation:** ship commit 3 cleanup as the closing act. Delete `UI/MainForm_TestHarness.vb`, `UI/TestHarnessCases.vb`, the `Ctrl+Shift+T` ElseIf in `MainForm_Layout.vb:1286–1289`, and `tools/send-ctrl-shift-t.ps1`.
6. **P5b becomes actionable** — kickoff at `docs/ui-reskin-p5b-kickoff.md` (already exists).

### 5.5 About the current conversation

After the trader uses this doc to seed the spec-author conversation, the **current Opus 4.7 High conversation is functionally complete**. The harness work is done, parity is verified, the B6 fix is shipped, and commit 3 has been correctly identified as belonging to the fix-implementation conversation.

The current conversation can be:
- **Archived** (clean closure) — preferred.
- **Kept open as a fallback** — in case the fix conversation needs harness construction context that isn't in this doc or the spec-back.

---

## 6. Appendix — references

- Kickoff: `docs/ui-reskin-p5-test-harness-kickoff.md`
- Full spec-back: `docs/ui-reskin-p5-test-spec-back.md`
- Trader's source files: `docs/VisualReviewQuestions.txt`, `docs/ContentRequestsAfterVisualReview.txt`
- Card-binding code: `UI/MainForm_Render_Cards.vb`
- Legacy renderer: `UI/MainForm_Render_Header.vb`, `UI/MainForm_Render_Sections.vb`
- Snapshot renderer: `UI/MainForm_PlaintextSnapshot.vb`
- Harness: `UI/MainForm_TestHarness.vb`, `UI/TestHarnessCases.vb`
- Run artifacts: `bin/Debug/net8.0-windows/verify/p5-test/`
- Next-phase kickoff (post-fix, post-cleanup): `docs/ui-reskin-p5b-kickoff.md`

---

**End of handoff.** Hand to spec author when ready.
