# UI Reskin P5-test — Gap Fix Proposal

**Phase:** Card-binding gap fixes surfaced by P5-test's visual review pass. Slots between **P5-test commit 2** (full case library) and **P5-test commit 3** (harness cleanup). After this spec lands and re-verification passes, the implementation conversation ships the cleanup commit and P5b becomes actionable.

**Spec source:** Trader's visual review of all 55 harness PNGs against the corresponding snapshots (`docs/VisualReviewQuestions.txt` + `docs/ContentRequestsAfterVisualReview.txt`); coder's triage handoff (`docs/ui-reskin-p5-test-visual-review-handoff.md`); spec-author sign-off on triage + defaults + research-backed C1a + new C1c (this conversation, 2026-05-27).

**Author:** Claude (Opus 4.7, spec-author conversation)
**Date:** 2026-05-27
**Recommended model for implementation:** **Opus 4.7 High.** ~20–25 distinct card-binding edits across `MainForm_Render_Cards.vb` (lines 700–2900), plus one new conditional-format helper for C1c. Synthesis of trader's 8 formatting guidelines across heterogeneous card sections. Lower models risk inconsistent application of `=` / `|` / bracket conventions.

---

## 0. What this phase is (and isn't)

**Is:** a focused card-UI cleanup spec that closes the binding gaps + format inconsistencies surfaced by the trader's visual review of the 55-case harness artifacts. Touches `UI/MainForm_Render_Cards.vb` for all card-binding work plus one display-only conditional in the ATR row (C1c). Re-runs the harness post-fix to confirm text parity (55/55) is preserved, then ships the existing P5-test cleanup commit.

**Isn't:**
- ❌ A scoring / engine code change. `Core/` is read-only throughout this spec.
- ❌ A `MainForm.Designer.vb` edit.
- ❌ A `UI/Controls/*.vb` modification. The §4 paint carve-out (handover §4.1) is **NOT invoked**. C3a (equal box widths) + C3b (header alignment) are layout-sizing changes in the card composition, not paint changes to `SectionGroup` or any control.
- ❌ A `BuildPlaintextSnapshot` rewrite. Most format guidelines apply only to the card UI; the snapshot stays at legacy-parity to preserve the 55/55 text parity gate. Post-P5b polish round can re-tune snapshot independently once it becomes the canonical text output.
- ❌ A legacy RTF pipeline change. `MainForm_Render_Header.vb` / `MainForm_Render_Sections.vb` are untouched — they delete in P5b, no point modifying them now.
- ❌ A settings.json change.
- ❌ A Spec C pre-empt. **Q3g + the SC under-reporting half of C6 explicitly defer to Spec C** where the architectural fix lives.
- ❌ An engine stop-capping change (C1a). Research-backed decision: stops stay uncapped; dual-row display already provides the worse-of information.

---

## 1. Trader's binding formatting guidelines

These eight rules are **binding constraints across every item in this spec**. Source: `docs/ContentRequestsAfterVisualReview.txt` lines 4–13. The implementer applies them consistently — when a specific item in §4–§8 is silent on format, default to these rules.

| # | Rule | Example |
|---|---|---|
| G1 | Keep content of the same item on the same row when possible | Single-row VWAP Bands; single-row Volume w/ BTC + USD + ratio |
| G2 | Label numbers — trader shouldn't have to guess which reading is which | `RSI(9): 64.0 > 50.0` not `64 > 50` |
| G3 | Inside a row, use `=` for label/value pairs and `\|` between pairs; never spaces alone | `H=2.00x \| M=1.30x` not `H:2.00 x / 1.3x` |
| G3a | The row's own header label may use `:` (e.g., `ADX: 32.5`) — `:` is fine for the leading label, `=` is for inline pairs | `ADX: 32.5 \| +DI=28.0 \| -DI=14.0` |
| G4 | Bracket all timeframe mentions | `REGIME (5m)`, `EMA200 (5m)`, `MTF GATE (15m)`, `EMA RIBBON (1m)` |
| G5 | Retain full names from legacy output when space allows | `RISING` not `BULL`; `Best vol. pivot (5m)` not `best vol` |
| G6 | Retain as much of the legacy label's info as space allows | `HH 50050.0>49920.0 \| HL 49900.0>49810.0` not `HH/HL` |
| G7 | Capitalise the starting characters in the SIGNAL BREAKDOWN NOTE column and equivalent labels | `none` → `None`, `near hvn support` → `Near HVN support` |
| G8 | Font / abbreviation changes apply symmetrically — both directions, both polarities | If `NEW LONGS` → `NW LNGS`, then `NEW SHORTS` → `NW SHRTS` (don't change one direction only) |

---

## 2. Triage prefix

Every item from the two source files is categorised. **IN-SCOPE** items go through §4–§8; **DEFERRED** items document the deferral target; **NO ACTION** items explain why no work is needed.

### 2.1 IN-SCOPE items (this spec)

ATR card (§4): Q2, C1a (doc-only), C1b (investigation), **C1c (NEW — stop-deeper flag)**
Structural rows (§5): C2a
INDICATOR DETAILS card (§6): C3a, C3b, C3c.i, C3c.ii, C3c.iii, C3d, C3e, C3f, C3g, C3h.i
SIGNAL BREAKDOWN card (§7): Q3a, Q3b, Q3c, Q3d, Q3e, Q3f, Q3i, Q3j, Q3k (code comment only), Q3m, C4a, C4b, C4c.i, C4d, C6 (format half only)
VOLUME PROFILE card (§8): Q4a, Q4b, C5a, C5b

### 2.2 DEFERRED items (other specs)

| Item | Defer target | Reason |
|---|---|---|
| **Q3g** — DMI/ADX card SC clamp under-reports engine score | **Spec C** (`docs/sc-column-total-parity-proposal.md`) | `BuildRowDmiAdx` at `MainForm_Render_Cards.vb:2502-2505` artificially clamps the summed score to `[-1, +1]`. Spec C's per-item `LongPoints` / `ShortPoints` field migration is the architectural fix — adding `LongPoints - ShortPoints` returns the true delta, no clamp needed. Fixing here ships a band-aid Spec C reverts. **Note:** C6's NOTE-column content expansion (`ADX=32.5 \| +DI=28.0 \| -DI=14.0`) remains in this spec — only the SC display under-reporting defers. |
| **C6 (SC half)** — paired with Q3g | **Spec C** | Same architectural fix. C6's format expansion stays here; SC accuracy defers. |
| **C1a** — stop capping at structural levels | **No action** (documented in §4.1) | Research-backed decision: industry hybrid pattern shows BOTH ATR and structural; engine already does that. Capping ATR row would break position-sizing decoupling. See spec-author response to trader 2026-05-27. |
| **Future — adaptive stop invalidation** | **New proposal** (`docs/adaptive-stop-invalidation-proposal.md`, post-Spec-C) | Trader-requested deeper research on industry buffer-sizing convention + crypto-perp volatility-regime ATR multiplier scaling. Engine work, separate from card UI. |
| **B11 / B13 / B14** — Type B Kelly arm cleanups | **Post-P5b engine hygiene proposal** (handover §3.6a) | Engine code-hygiene work. Discuss-before-implementation flag set. |
| **Snapshot format consistency** | **Post-P5b polish round** | Most format guidelines (G3 `=`/`\|`, G4 brackets, G5 full names) apply to card UI in this round. Snapshot stays legacy-parity to preserve 55/55 gate. Post-P5b, when snapshot becomes canonical text output, separate polish round can apply the same guidelines to snapshot text. |

### 2.3 NO ACTION items (explain to trader)

| Item | Why no action |
|---|---|
| **Q1** TIME stuck at `2026-01-01 12:00:00 UTC+8` | `NeutralVerdict` deliberately pins `v.Timestamp` so both renderers produce byte-identical timestamps for clean diffing. Production sets `verdict.Timestamp = DateTime.Now` at `MainForm_Analysis.vb:440`. Harness-only artifact; not a bug. |
| **Q3b.i** `upper qtr` (card) vs `breakout up` (snapshot) | Different summaries of related Donchian data — card computes quartile zone from current price position; snapshot reflects scoring breakdown emission. Both correct, surfacing different facets. |
| **Q3h** VWAP Bands in card but not snapshot SIGNAL BREAKDOWN | `BuildRowVwapBands` is a display-only row (`SC=0`); scoring is on the VWAP Dev row above. Documented in code at line 2548. Same field of view, snapshot's SIGNAL BREAKDOWN tracks scoring rows only. |
| **Q3l** MTF Gate row in snapshot SIGNAL BREAKDOWN but absent from card breakdown | MTF is a hard veto, not a +1/−1 score contributor (`CLAUDE.md` invariant). The breakdown row exists in legacy for displayability but emits no points (`—` SC in every case). Trader confirmed: keep card breakdown without MTF row; MTF lives in INDICATOR DETAILS section. Add a code comment to `BindCardSignalBreakdown` documenting the omission. |
| **C1a** stop capping (engine work) | See §4.1. |

---

## 3. Commit plan

Four commits within the gap-fix lifecycle. The fifth (P5-test cleanup) ships AFTER this spec's verification gates pass.

| # | Subject | Scope | Effort | LoC est. |
|---|---|---|---|---|
| 1 | `fix(ui-reskin): gap-fix — ATR + STRUCTURAL card fixes` | §4 + §5: Q2 risk/rwd surfacing, C1c stop-deeper conditional, C1a documentation comment, C1b investigation note (if confirmed duplicate, dedup), C2a structural row column re-order | Medium | ~150 |
| 2 | `fix(ui-reskin): gap-fix — INDICATOR DETAILS card formatting + layout` | §6: C3a-h (equal box widths, header alignment, section header label fixes per G3/G4/G5, FUNDING Config row reformat) | Medium | ~250 |
| 3 | `fix(ui-reskin): gap-fix — SIGNAL BREAKDOWN card content + format` | §7: Q3a–f, Q3i–k, Q3m, C4a–d, C6 NOTE expansion. Q3k adds code comment only. Q3l adds code comment only. | Medium | ~300 |
| 4 | `fix(ui-reskin): gap-fix — VOLUME PROFILE card content + format` | §8: Q4a omission, Q4b LVN alignment, C5a/b capitalisation + colour | Low | ~80 |

Per-commit verification gate: `dotnet build` clean + re-run the harness via `Ctrl+Shift+T` + confirm `test-results.md` still shows 55/55 PARITY at the text level. If any text-level discrepancy surfaces, the commit has accidentally touched the snapshot or legacy text path — fix before moving to the next commit.

After commits 1–4 land + trader's visual sign-off on the regenerated 55 PNGs, the implementation conversation ships the existing **P5-test commit 3 cleanup** as its final commit:

5. `chore(test): P5-test — remove harness after parity confirmed` — delete `UI/MainForm_TestHarness.vb`, `UI/TestHarnessCases.vb`, the `Ctrl+Shift+T` ElseIf in `MainForm_Layout.vb:1286-1289`, and `tools/send-ctrl-shift-t.ps1`.

---

## 4. ATR ENTRY LEVELS card

Card-binding code lives in `BindAtrRow` / `BindCardAtrLevels` (or equivalent) in `MainForm_Render_Cards.vb`. Implementer should grep for the function names to confirm.

### 4.1 Item Q2 — surface risk / rwd USD amounts

**Source:** Visual review Q2.
**Current state:** Card shows entry/stop/target/R:R numerically but omits the absolute USD risk and reward amounts. Legacy emits `(risk 96.0 / rwd 160.0)` inline.
**Fix:** Append `(risk {risk:F1} / rwd {rwd:F1})` to the right of the `R:R` cell on each ATR row. Both long and short directions; both ATR row and structural row.

```
Long:             Stop  49904.0  | Entry  50000.0 | Target  50160.0   R:R 1:1.7  (risk 96.0 / rwd 160.0)
Long structural:  Stop  49700.0  | Entry  50000.0 | Target  50300.0   R:R 1:1.0  (risk 300.0 / rwd 300.0)
```

Compute as `risk = |entry - stop|`, `rwd = |target - entry|`. Format `F1`. No engine work — fields already available.

### 4.2 Item C1a — stops intentionally uncapped (DOC ONLY)

**Source:** Content request C1a.
**Decision:** No engine work. Stops stay uncapped. Research-backed (industry "worse-of" pattern: show both ATR and structural; trader picks deeper). See spec-author response 2026-05-27.

**Fix:** Add a code comment to `BindAtrRow` (or wherever the ATR card's STOP cell is bound) documenting the asymmetry:

```vb
' DESIGN NOTE: ATR stops are deliberately uncapped (no CAPPED indicator
' on the STOP cell). Targets are capped at HVN/POC via AdjustedLongTarget /
' AdjustedShortTarget; stops are not. Rationale: capping the ATR stop would
' (a) break position-sizing decoupling (Base × AvgATR/CurrATR reads atrStop
' as the volatility reference), (b) duplicate information already shown in
' the structural row beneath, (c) mislead the ATR row's R:R reading.
' Trader uses the dual-row display + C1c "worse-of" prefix to pick which
' stop to execute against. Future adaptive-stop work tracked in
' docs/adaptive-stop-invalidation-proposal.md (post-Spec-C).
```

### 4.3 Item C1b — investigate duplicate CAPPED label

**Source:** Content request C1b.
**Action:** Implementer investigates first. No CAPPED-on-stop case exists in the 55 harness cases, but a CAPPED-on-target case does (`16_atr_capped_swing_high_target_long` or similar — implementer greps `TestHarnessCases.vb` for CAPPED to locate). Inspect the rendered screenshot for that case.

If the second CAPPED label always duplicates the in-line one (same price, same reason), remove the second one — keep the in-line CAPPED only. If the second CAPPED carries different information (different cap target, different reason), leave both and document why.

Report finding in the spec-back. If duplicate, ship the dedup in this commit; if distinct, leave a code comment explaining the two labels' independence.

### 4.4 Item C1c (NEW) — stop-deeper visual flag

**Source:** Spec-author + trader collaboration 2026-05-27, derived from C1a research.
**Intent:** When the structural stop is further from entry than the ATR stop (the typical case), surface the structural stop figure inline on the ATR row's STOP cell so the trader can decide which stop to execute against without scanning down to the structural row.

**Rule:**

- LONG direction: if `structuralStop < atrStop` (i.e., structural is lower / further from entry), prepend the structural stop figure to the ATR row's STOP cell with a visual cue.
- SHORT direction: if `structuralStop > atrStop` (i.e., structural is higher / further from entry), prepend the structural stop figure similarly.
- If `structuralStop == atrStop` exactly, no prefix (no decision value).
- If `structuralStop` is unset / NaN / placeholder (insufficient pivot data), no prefix; fall back to current ATR-only display.

**Format suggestion (implementer picks the exact glyph — keep it scannable):**

```
Current (always two rows):
Long:             Stop  49904.0  | Entry  50000.0 | Target  50160.0  R:R 1:1.7
Long structural:  Stop  49700.0  | Entry  50000.0 | Target  50300.0  R:R 1:1.0

After (structural deeper — the typical case):
Long:             Struct→49700.0  Stop 49904.0  | Entry  50000.0 | Target  50160.0  R:R 1:1.7
Long structural:  Stop  49700.0                 | Entry  50000.0 | Target  50300.0  R:R 1:1.0

After (ATR deeper — rare, tight swing structure):
Long:             Stop 49904.0  | Entry  50000.0 | Target  50160.0  R:R 1:1.7
Long structural:  Stop 49890.0  | Entry  50000.0 | Target  50300.0  R:R 1:1.0
```

**Card-only in this spec.** The snapshot stays at legacy-parity to preserve the 55/55 text-parity gate. Post-P5b backlog item (logged in handover §6): re-add a C1c-equivalent text variant to `BuildPlaintextSnapshot` when it becomes the canonical text output.

**Implementation hint:** introduce a small helper `Function FormatAtrStopCell(atrStop As Double, structuralStop As Double, direction As String) As String` that encapsulates the conditional. Symmetric across LONG and SHORT.

**No engine work.** Both `atrStop` and `structuralStop` are already computed and bound on the verdict / indicator results — implementer reads them inline. Position-sizing math (`Base × (AvgATR / CurrATR)`) stays referencing `atrStop`; sizing is unchanged.

**Test case adjustment:** existing 55 cases include enough variation that both branches of C1c are exercised. Implementer confirms during re-run: at least one case shows the prefix; at least one case shows the ATR-deeper fallback. If neither path fires across 55 cases, the case factory needs a tweak.

---

## 5. STRUCTURAL row layout

### 5.1 Item C2a — column re-order to match ATR row layout

**Source:** Content request C2a.
**Current state:** Structural row columns are `STRUCT STOP | STRUCT TARGET | ENTRY`.
**Wanted:** `STRUCT STOP | ENTRY | STRUCT TARGET` — matching the ATR row's `STOP | ENTRY | TARGET` order.
**Fix:** Re-order the column composition in whichever `BindAtrRow` variant handles the structural row. Adjust column widths if needed to keep alignment with the ATR row above.

After the change, the ATR row and structural row read identically column-for-column. Trader's scan rhythm preserves.

---

## 6. INDICATOR DETAILS card

Card composition + sub-section layout in `BindIndicatorDetailsCard` (or equivalent) in `MainForm_Render_Cards.vb`. Sub-section headers + label content per item below.

### 6.1 Layout fixes (C3a + C3b)

**C3a — equal box widths.** Trader wants the bordered sub-sections inside INDICATOR DETAILS to share a uniform width. Currently they vary based on inner content. Fix: set a common width on each `SectionGroup` instance in this card's composition. This is a layout-sizing change at the consumer level, NOT a property change to `SectionGroup` — the §4 paint carve-out is not invoked.

**C3b — header alignment L/R.** Sub-section headers on the left column should horizontally align with sub-section headers on the right column. Example: `MTF GATE (15m) · PASS` (left column) should sit at the same y-position as `VWAP` (right column). Right-column headers follow the left-column rhythm as priority — if a left-column sub-section is taller, the right-column matching row leaves a gap before the next sub-section.

**Implementation hint:** if the card composition uses a per-cell stack, switch to a paired-row approach where each "row" of the INDICATOR DETAILS card contains the left + right sub-sections side-by-side and their heights are unified via the taller of the two.

### 6.2 Sub-section header label fixes (C3c.i, C3d, C3e, C3f, C3g)

Section header text changes. Apply per G4 (timeframe brackets) and G5 (full names from legacy).

| Section | Current | Wanted | Rule |
|---|---|---|---|
| Dynamic norms | `NORMS [LIVE]` | `DYNAMIC NORMS [LIVE]` | G5 (full name) |
| Regime | `REGIME 5m · TRENDING_UP` | `REGIME (5m) · TRENDING_UP` | G4 (timeframe bracket) |
| BBW / TTM | `BBW / TTM` | `BBW / TTM SQUEEZE` | G5 (full name) |
| EMA Ribbon | `EMA RIBBON · BULL` | `EMA RIBBON (1m) · BULL` | G4 |
| MTF Gate | `MTF GATE 15M · PASS` | `MTF GATE (15m) · PASS` | G4 (note case: `15m` not `15M`) |

### 6.3 Sub-section content fixes

**C3c.ii — Vol threshold row reformat.**
- Current: `Vol H/M: 2.00 x / 1.3x`
- Wanted: `Vol threshold: H=2.00x | M=1.30x`
- Rules: G3 (`=` for pairs, `|` between), G3a (`:` after the leading label `Vol threshold` is fine), G5 (full label name).

**C3c.iii — ATR scale row reformat.**
- Current: implementer to confirm card's current format
- Wanted: `ATR scale: 1.00x (ATR=80.00 | ref=80.00)`
- Rules: G3.

**C3h.i — FUNDING Config row reformat.**
- Current: `Config: en: Y soft:+1 amp:-1`
- Wanted: `Config: Enabled=Y | Soften=+1 | Amplify=-1`
- Rules: G3 (`=`, `|`), G5 (full names — `Enabled` not `en`, `Soften` not `soft`, `Amplify` not `amp`).

---

## 7. SIGNAL BREAKDOWN card

`BindCardSignalBreakdown` at `MainForm_Render_Cards.vb:2088-2199` + per-row helpers `BuildRowXxx` from line 2502 onward.

### 7.1 NOTE column content additions (Q3a, Q3c, Q3d, Q3e, Q3f, Q3i, Q3j, C6)

The trader's pattern: NOTE column should carry as much of the legacy snapshot's information as fits. Each row below specifies the wanted NOTE content. Card's left-column label + SC value stay as today (subject to deferred Spec C work on SC accuracy).

| Row | Current NOTE | Wanted NOTE | Source |
|---|---|---|---|
| **Volume (Q3a)** | `1.60x SMA` (or similar) | `1.60x SMA \| $8.0M \| 100.0000 BTC` | Q3a — add BTC volume per trader request |
| **OBV (Q3c)** | `no div` | `RISING \| div: NONE` (or `FALLING \| div: BULLISH` etc.) | Q3c — surface both slope (full name per G5) and divergence |
| **Trend Str (Q3d)** | `HH/HL` | `HH 50050.0>49920.0 \| HL 49900.0>49810.0` | Q3d — retain legacy detail per G6 |
| **CVD (Q3e)** | `RISING` (suppresses Div when NONE) | Confirm via investigation: when divergence is `BULLISH` / `BEARISH`, does the card surface it? If not, add. Format: `RISING \| div: NONE` symmetric with OBV. | Q3e — investigation + fix |
| **RSI(9) (Q3f)** | `64.0` | `64.0 > 50.0` | Q3f — G2 (label the comparison) |
| **BBW/TTM (Q3i)** | `none` (squeeze status) | `none \| TTM=BULL_BUILDING` (or similar) — surface both squeeze + TTM signal | Q3i — both fields valid per G6 |
| **EMA Ribbon (Q3j)** | `9>21>50` | `9>21>50 = BULL alignment` | Q3j — explicit trader request |
| **DMI/ADX (C6)** | `ADX 32.5` | `ADX=32.5 \| +DI=28.0 \| -DI=14.0` | C6 — NOTE format expansion. SC under-reporting defers to Spec C (§2.2). |

For the investigation half of Q3e: implementer checks `BuildRowCvd` to confirm the suppression rule and adds the divergence surfacing if missing. Symmetric with OBV's divergence pattern.

### 7.2 Donchian gap (Q3b)

Card shows derived quartile zone (`upper qtr`) but not raw `Upper / Lower` prices. Add the raw prices to either:
- Donchian row's NOTE column: `upper qtr \| U:50100.0 L:49600.0` (per G6), OR
- A small label in the INDICATOR DETAILS Donchian sub-section (if one exists) and reference from the SIGNAL BREAKDOWN row.

Implementer picks based on space. Prefer NOTE-column inline if the row fits.

### 7.3 VPFR card pill — code comment only (Q3k)

**No display change.** Card pill maps `r.VPFRSignal = "NEAR_HVN_SUPPORT"` to a `BULL` directional pill, while snapshot's SIGNAL BREAKDOWN row shows `NEUTRAL` because the VPFR signal didn't fire as a +1 scoring hit in that case. Both are correct — indicator state vs scoring contribution are distinct concepts.

**Fix:** Add a code comment to `BuildRowVpfr` at `MainForm_Render_Cards.vb:2800-2806`:

```vb
' DESIGN NOTE: This pill encodes VPFR indicator state (NEAR_HVN_SUPPORT
' → BULL pill etc.), NOT scoring contribution. The SC column displays
' scoring hit (±1/0); these can disagree (pill = BULL, SC = 0/NEUTRAL)
' when the VPFR indicator state is informational but didn't trigger
' the engine's +1/-1 emission. Both are correct. Spec C's per-item
' LongPoints/ShortPoints migration makes SC more granular but does
' not change the pill-vs-SC distinction.
```

### 7.4 MTF Gate absence from card breakdown — code comment only (Q3l)

**No display change.** MTF Gate is a hard veto, not a scoring contributor — confirmed by trader 2026-05-27. Card's `BindCardSignalBreakdown` deliberately omits the MTF row from the breakdown table; MTF lives in INDICATOR DETAILS as its own sub-section.

**Fix:** Add a code comment near the breakdown label roster in `BindCardSignalBreakdown`:

```vb
' DESIGN NOTE: MTF Gate (15m) is intentionally absent from the card's
' SIGNAL BREAKDOWN table. MTF is a hard veto (CLAUDE.md invariant) that
' forces NO TRADE on BLOCK; it does not emit +1/-1 scoring hits. The
' snapshot retains an MTF breakdown row for legacy parity, but the row's
' SC is always 0 (—) by engine design. MTF state is surfaced in this
' card's INDICATOR DETAILS sub-section instead.
```

### 7.5 Best Vol Pivot sub-note visibility (Q3m)

Trader missed the sub-note `best vol: HIGH @ 50050.0 (1.8x)` under the Swing Pivots row because its visual weight reads as decoration rather than data.

**Fix:**
- Sub-note ForeColor: `Theme.FG_TERTIARY` → `Theme.FG_SECONDARY`
- Sub-note font weight: regular → semibold (use `New Font(Theme.FontMono(...), FontStyle.Bold)` or equivalent)

Cheap fix, no layout reshuffle. Verify by re-running the harness and trader confirming on the regenerated case `01_strong_long_full_confluence.png`.

### 7.6 Label / format fixes (C4a, C4b, C4c.i, C4d)

| Row | Current | Wanted | Rule |
|---|---|---|---|
| Donchian (C4a) | `Donchian` | `Donchian(20)` | G4 |
| Best vol. pivot (C4b) | `best vol: HIGH @ 50050.0 (1.8x)` | `Best vol. pivot (5m): HIGH @ 50050.0 (vol × 1.8 vs avg. pivot)` | G4 + G5 (full name, full description) |
| OI Change (C4c.i) | `NEW LO` / `NEW SH` | `NW LNGS` / `NW SHRTS` (symmetric per G8) | trader explicit |
| EMA200 (C4d) | `EMA200 5m` | `EMA200 (5m)` | G4 |

For C4c.i: G8 requires symmetric application. The abbreviation must apply to BOTH long and short emissions consistently — implementer audits `BuildRowOi` (or wherever OI emission is bound) and confirms both directions use the new short form.

---

## 8. VOLUME PROFILE card

### 8.1 HVN@POC omission (Q4a)

Card omits `HVN@POC:NO`. Snapshot surfaces it. Per spec-author default (§4.3 of triage handoff), `HVN@POC:NO` is redundant when `NEAR_HVN_SUPPORT` / `NEAR_HVN_RESIST` pill is shown (POC vs HVN-near are distinct concepts; explicit "NO" adds noise without information).

**Fix:** No change. Document in code comment that `HVN@POC` is intentionally omitted from card display.

### 8.2 LVN walls alignment (Q4b)

Snapshot shows `LVN: ^— v—` (where `^` = LVN-above, `v` = LVN-below, `—` = none found within range). Card surfaces walls differently. Implementer audits the card's HVN walls / LVN display, aligns the format with the snapshot's semantic intent. Suggested card format per G3 + G5:

```
HVN walls: Above=50200.0 | Below=49800.0
LVN walls: Above=— | Below=—
```

If the card already separates `HVN` and `LVN` walls, the format change is per-line. If the card combines them into one line, split into two lines per G1 (same item, same row — `HVN walls` and `LVN walls` are different items).

### 8.3 Capitalisation fixes (C5a, C5b)

Per G7: capitalise the starting characters of NOTE column values and equivalent labels.

| Item | Current | Wanted | Notes |
|---|---|---|---|
| VPFR signal label (C5a) | `near hvn support` | `Near HVN support` | Also: trader wants this coloured. Use `Theme.ACC_OK` (or matching success colour) for `Near HVN support` (long-favouring), `Theme.ACC_WARN` for `Near HVN resist` (short-favouring), `Theme.FG_PRIMARY` for neutral states. Symmetric per G8. |
| Value Area label (C5b) | `inside va` | `Inside VA` | Same capitalisation rule; no colour change unless trader requests later. |

---

## 9. Verification plan

Two gates between work and cleanup. Both must pass before P5-test commit 3 ships.

### 9.1 Text parity gate (per-commit)

After each of commits 1–4: `dotnet build` clean, run app, trigger harness via `Ctrl+Shift+T`, open `verify/p5-test/test-results.md`, confirm 55/55 PARITY.

If any text-level discrepancy appears, the commit accidentally touched a text-emitting path (`MainForm_Render_Header.vb`, `MainForm_Render_Sections.vb`, or `MainForm_PlaintextSnapshot.vb`). Identify the discrepant case, revert the offending change, re-implement card-only.

Expected outcome: 55/55 PARITY preserved across all four commits. The scope is deliberately card-only.

### 9.2 Trader visual sign-off gate (after commit 4)

After commit 4 builds clean and the harness produces 55/55 PARITY, the implementation conversation pings the trader for visual review. Trader scrolls the regenerated 55 PNGs in `verify/p5-test/*.png` and confirms each gap-fix item lands as expected. Specifically worth checking:

- ATR row's `(risk N / rwd N)` cell appears on every case (Q2)
- C1c stop-deeper prefix fires when structural is further (typical cases) and doesn't fire when ATR is deeper (rare cases) — at least one case of each branch
- INDICATOR DETAILS sub-section boxes are uniform-width (C3a) and headers align L/R (C3b)
- All section header label tweaks land (C3c.i, C3d, C3e, C3f, C3g)
- All NOTE-column content additions render (Q3a, Q3c, Q3d, Q3e, Q3f, Q3i, Q3j, C6)
- Best Vol Pivot sub-note reads clearly (Q3m)
- Capitalisation + colour land (C5a, C5b)

If trader flags any item as not-as-intended, the implementer iterates — small follow-up commit, re-run harness, re-confirm parity, re-ping trader.

### 9.3 Cleanup commit (after both gates pass)

Implementation conversation ships P5-test commit 3 cleanup. Delete:
- `UI/MainForm_TestHarness.vb`
- `UI/TestHarnessCases.vb`
- The `Ctrl+Shift+T` ElseIf in `MainForm_Layout.vb` at lines 1286–1289 (per kickoff §0.6 addendum; confirm exact line range at delete time)
- `tools/send-ctrl-shift-t.ps1`

After cleanup: `grep -rn "_testHarnessMode\|RunRenderParityHarness\|TestHarnessCases\|TestCaseBuilder" UI/` returns zero matches. Build clean.

P5b becomes actionable. Kickoff already exists at `docs/ui-reskin-p5b-kickoff.md`.

---

## 10. Out of scope

- ❌ Touch `Core/`. Scoring engine, indicators, settings — all read-only.
- ❌ Touch `MainForm.Designer.vb`.
- ❌ Touch `UI/Controls/*.vb`. §4 paint carve-out NOT invoked.
- ❌ Modify `BuildPlaintextSnapshot` content semantics. Snapshot stays legacy-parity for the 55/55 gate.
- ❌ Modify legacy RTF renderers (`MainForm_Render_Header.vb`, `MainForm_Render_Sections.vb`). They delete in P5b.
- ❌ Spec C work. `SignalBreakdownItem` field migration + 23 emission sites — defers entirely.
- ❌ Engine stop-capping (C1a) or adaptive stop invalidation (parked).
- ❌ Type B Kelly arm cleanups. Discuss-before-implementation flag set on the post-P5b engine hygiene proposal.
- ❌ Post-P5b snapshot polish. Snapshot stays current shape until P5b deletes the legacy parity reference.
- ❌ Push to remote.

---

## 11. Workflow conventions

- **Local commits only.** Four commits + a fifth cleanup commit. Do NOT push.
- No `--no-verify`, no force ops, no `MainForm.Designer.vb` edits.
- Engine code untouched. `Core/` is read-only.
- Settings.json untouched.
- §4 paint carve-out NOT invoked.
- Each card-binding commit goes through the harness re-run gate (text parity 55/55).
- Trader visual sign-off is the load-bearing gate before cleanup.

---

## 12. If you get stuck

1. **A commit breaks text parity.** Identify the discrepant case in `test-results.md`, open the diff, find where the snapshot text changed. Most likely the binding code accidentally reached into a shared formatter that the snapshot also calls. Localise the change to a card-only helper.

2. **C1c branches don't fire in any case.** Audit the 55 cases for variation in `structuralStop` vs `atrStop`. If all cases have similar relationships, edit a few cases' synthesised data to introduce the missing branch (small `TestHarnessCases.vb` tweak, no spec-level change needed — fold into commit 1).

3. **C3a equal box widths don't render.** Likely the card composition uses content-driven sizing. Switch to explicit width constants per row OR use a TableLayoutPanel column-spanning approach. Test with the case that has the longest sub-section content to confirm no clipping.

4. **C3b header alignment fails when left + right column sub-sections have different heights.** Use the taller height as the row height for both columns; the shorter column gets bottom padding. Don't try to vertically center either column — the header positions are what align.

5. **Q3m sub-note bump doesn't read as more prominent.** Combine the ForeColor + font weight change; if still ambiguous, also bump the font size by 1pt. The change is intentional emphasis, not subtle decoration.

6. **C5a colour mapping ambiguous for a non-standard VPFR signal value.** Default to `Theme.FG_PRIMARY` (neutral colour) for any value not in the canonical set. Don't guess.

---

## 13. Reporting back

Spec-back doc: `docs/ui-reskin-p5-test-gap-fixes-spec-back.md`. Same structure as past spec-backs.

Specifically worth reporting:

1. **Per-commit text parity result.** 55/55 PARITY confirmation for each of commits 1–4.
2. **C1c branches exercised.** Confirm at least one case shows the prefix; at least one shows ATR-deeper fallback. List case names.
3. **C1b investigation finding.** Was the second CAPPED label a duplicate? If so, dedup shipped; if not, code comment shipped explaining the two labels' independence.
4. **Q3e investigation finding.** Does the card's CVD row already surface divergence when non-NONE? If not, the fix shipped.
5. **C3a + C3b layout approach.** Brief description of which technique closed the equal-width + header-alignment requirement (TableLayoutPanel, explicit widths, paired-row composition).
6. **Trader sign-off result.** Confirmation that the regenerated PNGs match trader expectation, or list of iteration items.
7. **Cleanup commit status.** Whether commit 5 (harness cleanup) has shipped.

The next spec-author conversation uses this spec-back to confirm P5b is actionable and to draft the next phase's work (P5b kickoff already exists at `docs/ui-reskin-p5b-kickoff.md`).

---

**End of proposal.** Drop into a fresh Opus 4.7 High conversation when ready. The 8 binding formatting guidelines + the explicit triage prefix should keep the work tightly scoped; the harness re-run gate catches accidental snapshot drift; the trader visual sign-off is the closing condition before the cleanup commit ships.
