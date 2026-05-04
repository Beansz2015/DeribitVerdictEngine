# Spec: D1 — HH/HL/LH/LL Trend Structure Classification
**Proposed:** 2026-05-05
**Status:** PROPOSED 2026-05-05
**Target files:** `Core/Indicators_Structure.vb`, `Core/IndicatorResults.vb`, `Core/ScoringEngine_Calculate_Scoring.vb` (Pass 2c), `Core/Settings/EngineSettings.vb`, `settings.json`, `UI/MainForm_Render_Sections.vb`
**Builds on:** shipped 5m swing pivots
**Trader-profile alignment:** high — Section 6 explicitly cites HH/HL structure as "the bedrock of price-action trading"

---

## 1. Background

The 5m swing pivot detector currently surfaces only the most recent confirmed swing high and swing low. Trader-profile classifies the **sequence** of swings:

- **HH + HL** = uptrend structure (higher highs, higher lows)
- **LH + LL** = downtrend structure (lower highs, lower lows)
- **HH + LL** = expansion / divergence (range-widening)
- **LH + HL** = contraction (range-narrowing)
- Anything insufficient → `UNDEFINED`

Pass 2c regime alignment currently gates on EMA, ROC, CVD slope (TRENDING) or VWAP, RSI, Donchian (RANGE_BOUND). It does not consider swing structure. A TRENDING_UP regime that is also showing HH+HL is structurally stronger than one showing HH+LL. Adding structure as a regime confirmation signal aligns scoring with how the trader actually reads the chart.

---

## 2. Specification

### 2a. Classifier

New function in `Indicators_Structure.vb`:

```
Public Function ClassifyTrendStructure(
    candles5m As List(Of Candle),
    pivotWing As Integer,
    pivotCount As Integer
) As TrendStructure
```

`TrendStructure` enum: `UPTREND`, `DOWNTREND`, `EXPANSION`, `CONTRACTION`, `UNDEFINED`.

Logic:
1. Walk back from end of `candles5m`, identify the last `pivotCount` confirmed pivots (mix of highs and lows), each requiring `pivotWing` bars of confirmation on each side. Use existing pivot detection from `CalcSwingPivots`.
2. Need at least 2 highs and 2 lows in the result. If fewer (window too short or chop), return `UNDEFINED`.
3. Compare the most recent two highs: `HH` if newer > older; `LH` otherwise.
4. Compare the most recent two lows: `HL` if newer > older; `LL` otherwise.
5. Map to enum:
   - `HH + HL` → `UPTREND`
   - `LH + LL` → `DOWNTREND`
   - `HH + LL` → `EXPANSION`
   - `LH + HL` → `CONTRACTION`

Pure function. No state.

### 2b. IndicatorResults additions

```
Public TrendStructure As TrendStructure       ' from ClassifyTrendStructure
Public LastTwoHighs5m As (Older As Double, Newer As Double)
Public LastTwoLows5m As (Older As Double, Newer As Double)
```

The two tuples are display-only — used by the render section to show the actual swing values that produced the classification.

### 2c. Pass 2c integration

After existing Pass 2c TRENDING / RANGE_BOUND alignment scoring, **before** the snapshot is taken for funding modifiers:

```
If cfg.RegimeWeights.Enabled AndAlso cfg.Indicators.TrendStructure.Enabled Then
    Dim bonus = cfg.Indicators.TrendStructure.StructureBonus
    Select Case r.TrendStructure
        Case UPTREND
            If state.LongScore > state.ShortScore Then
                state.LongScore = Math.Min(state.LongScore + bonus, regimeMax)
                breakdown.Add("Trend Structure (HH+HL)", "+" & bonus, "long",
                              "uptrend structure confirms long bias")
            End If
        Case DOWNTREND
            If state.ShortScore > state.LongScore Then
                state.ShortScore = Math.Min(state.ShortScore + bonus, regimeMax)
                breakdown.Add("Trend Structure (LH+LL)", "+" & bonus, "short",
                              "downtrend structure confirms short bias")
            End If
        Case EXPANSION
            ' No score change; flagged in render section as caution
        Case CONTRACTION
            ' No score change; flagged in render section as compressing range
        Case UNDEFINED
            ' No score change
    End Select
End If
```

Key constraints:
- Bonus only applies when structure agrees with the **dominant** side (not both, not opposite). Avoids the "scoring asymmetric noise" problem.
- Bonus capped at `regimeMax` so it can't push score past saturation.
- Explicitly not double-counting: this is structure-on-pivots, distinct from EMA / ROC / CVD which are price-momentum signals.
- Suppressed in TRANSITIONAL regime (consistent with rest of Pass 2c).

### 2d. Settings.json additions

New block under `indicators`:

```json
"trend_structure": {
    "enabled": true,
    "pivot_wing": 3,
    "pivot_count": 6,
    "structure_bonus": 1
}
```

`pivot_wing` reuses the existing `Indicators.Swing.PivotWing5m` value semantically; defined as a separate setting so the trend-structure classification can use a tighter or looser window than the cap-arbitration pivots if ever needed. Default matches the swing setting.

`pivot_count` = 6 means we look for 6 confirmed pivots in the lookback. With typical alternation, that's 3 highs + 3 lows. Enough to classify; not so many that the structure decision lags real regime change.

`structure_bonus` = 1. Single point. Tunable by auto-tweaker once it ships.

### 2e. Render section

Add a row to MARKET STRUCTURE section:

```
Trend Structure : UPTREND  (HH 102450.0 > 102100.0 | HL 101800.0 > 101500.0)
```

Colours: UPTREND green, DOWNTREND red, EXPANSION amber, CONTRACTION dim cyan, UNDEFINED dim grey.

### 2f. CSV column

Add column 87 to v0.4 schema:
- `TrendStructure5m` — String (UPTREND / DOWNTREND / EXPANSION / CONTRACTION / UNDEFINED)

Update CalibrationReport with a new `TREND STRUCTURE DISTRIBUTION` section showing counts per category.

---

## 3. Out of Scope

- 15m trend structure classification — Q20 said 5m only. 15m has too few confirmed pivots in typical lookback.
- Multi-tier (last 4 highs and 4 lows) classification — overkill for v1; harder to interpret.
- Wiring into VerdictContext — defer to v2 once data shows whether `EXPANSION` correlates with weak verdicts.
- Wiring into HoldStatus exit logic — separate concern, not in this spec.

---

## 4. Acceptance

- Build clean. Settings v23 → v24 (or roll into v23 if not yet shipped).
- TrendStructure classified on every run, displayed in MARKET STRUCTURE section.
- Pass 2c bonus applied when (UPTREND + dominant LONG) or (DOWNTREND + dominant SHORT).
- CSV column 87 logged.
- 30+ runs across regimes show plausible classifications (manual eyeball check against TradingView chart).
- After ~200 rows, CalibrationReport shows distribution that matches expected regime mix (UPTREND ≈ TRENDING_UP rows, etc.).
