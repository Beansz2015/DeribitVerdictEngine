# D2-v2 What-If Candidate Mode — Proposal (BestPivot as a testable target candidate)

**Date:** 2026-07-29 (Fable coordinator). **Status:** **APPROVED 2026-07-29 — D1–D6 ticked all-as-recommended (trader). BUILD-AUTHORIZED** (Opus, medium; net-EV rider landed `99cc0dc`, lane clear).
**Class:** v56-pattern seam extension — **one new settings key, default `false`, byte-identical at default ⇒ NOT a dataset boundary at ship.** What-if-testable immediately; **live-enable is a LATER separate ⚠** (the P1 promotion), evidence-gated on the what-if study + the P1 conditions.
**Trigger:** the [map](backlog-dependency-map.md) D2-v2 row — BestPivot promotion is "NOT what-if-testable today (`ComputeSideLevels` never reads `BestPivot*`)" — and the Aug-1 geometry session, which wants to test it against the 2,151 post-07-08 directional rows carrying a non-empty `BestPivotByVolume5m`. Context: D2 volume-weighted pivots shipped display-only at v24; P1 (§16.6) parks promotion behind evidence this extension exists to produce.

## 1. Mechanism

**New key `scoring.structural_levels.use_best_pivot_candidate`** (bool, default `false`). When true, the volume-weighted best pivot joins `ComputeSideLevels`' **target** candidate set:

- **Side by price comparison, not `IsHigh`** (D3): pivot above entry ⇒ long-target candidate; below ⇒ short-target candidate. One rule for live AND replay — the CSV logs `BestPivotByVolume5m` (price) + `BestPivotVolumeRatio5m` but NOT `IsHigh`, so price-side is the only definition both paths can share (one-seam rule). A LOW pivot sitting above entry still marks a level the market defended on volume — price-side is the placement-relevant fact.
- **Participation per the active `target_arbitration_mode`** (composes with v56): ladder mode ⇒ inserted as the FIRST tier, above swing (P1's "4th cap tier above swing" verbatim); nearest mode ⇒ just another candidate. Same looseness bound (`target_max_atr_mult`) as every tier; zero-distance/absent pivot ⇒ candidate simply absent.
- **Stop side untouched** — D2 was always a target idea; stops stay DG1.
- **Replay path:** the what-if adapter feeds the logged `BestPivotByVolume5m` column (`CsvRow` gains the field if not already parsed); rows with the column empty have no candidate — counted, not guessed (POC-tier precedent).
- `VolumeRatio` is NOT consulted at the build (no min-ratio gate knob until evidence says the ratio matters — YAGNI; noted for the P1 promotion spec).

## 2. Surfaces

Reason label when the pivot tier places: `BEST_PIVOT_5M` (joins the existing label set; renders through the same `PLACED @` string — snapshot/card/payload/CSV inherit it through the one seam, no new lines, no format change). At default `false` nothing renders differently anywhere — four-surface parity preserved by construction (v56 A36a pattern).

## 3. Fences + what-if

- Tweaker: the new key joins the **HC24 exact-match set** (hand-ruled geometry class — same rationale, same mechanism; no new HC number needed).
- What-if: whitelist + `ApplyKnob`/`ReadKnob` mirrors + launcher row (`Use best-pivot candidate`, `{0,1}` sweep — the v56 int-mode precedent covers boolean-as-int sweeps).
- Settings v62 → **v63**, change_log entry, §15 row.

## 4. Fixtures (A42 family — confirm next-free at build) + acceptance

- **A42a** default-false byte-identity across the A26 case set through the real `Calculate()`.
- **A42b** pivot-above-entry enters the long ladder FIRST (beats a closer swing in ladder mode); nearest mode picks by distance; short mirror.
- **A42c** looseness bound rejects a too-far pivot (falls through to swing); absent/zero pivot ⇒ identical to A42a.
- **A42d** fence: HC24 rejects the key; what-if whitelist accepts it; `{0,1}` sweep round-trips through `BuildCellSettings` and reproduces `ComputeSideLevels` (A36f pattern).
- Acceptance: 5 projects build 0/0 Release; A1–A41d unregressed; verify-gate prepush GATE PASSED; Release-only builds (collector rule); local commit, trader pushes.

## 5. The study it unlocks (Aug-1 geometry session)

What-if grid `{target_arbitration_mode 0/1} × {use_best_pivot_candidate 0/1}` on the full v0.8 book + the post-07-08 window separately, net-EV-in-ATR (the §6.1 rider makes this net of fees) + split-half. P1 promotion decision then reads: does the pivot tier place often enough to matter, and does it beat swing-first on net EV without DIVERGENT flags? Fail ⇒ D2 stays display-only, honestly.

## 6. D-table (await trader)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Knob shape | **Boolean `use_best_pivot_candidate`**, composing with the v56 modes (not a third mode int — orthogonal axes stay orthogonal) |
| D2 | Ladder priority | **First tier, above swing** (P1 verbatim) |
| D3 | Side determination | **Price-side vs entry**, identical live + replay (IsHigh unlogged; one seam) |
| D4 | Fence | **Join the HC24 exact-match set** (no new HC) |
| D5 | VolumeRatio gate | **Not at this build** — revisit at the P1 promotion spec if the study shows ratio-dependence |
| D6 | Scope | **Build = what-if instrument only; live-enable is a separate later ⚠** with its own D-table + the §5 evidence |
