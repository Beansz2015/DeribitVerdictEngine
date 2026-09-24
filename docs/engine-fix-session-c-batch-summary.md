# Batch summary — engine-fix Session C (`D-8` report lean column, `D-9` Step 3b effect on both surfaces)

**Written:** 2026-09-24 (UTC). **Seat:** Seat 2 of the engine-fix build, Opus, high, same conversation as Session A.
**Spec:** [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §5 (Session C) and §6 (rulings `EF-2`, `EF-4`). **Brief:** [`docs/engine-fix-sessions-a-c-b2-brief.md`](engine-fix-sessions-a-c-b2-brief.md).
**Review packet:** [`docs/engine-fix-session-c-spec-back.md`](engine-fix-session-c-spec-back.md). **Previous session:** [`docs/engine-fix-session-a-batch-summary.md`](engine-fix-session-a-batch-summary.md).

## 0. Read this first

- ✅ **Built and committed locally, NOT pushed, NOT deployed.** Commit `ea32818` on `master`.
- ✅ **Rendered values only.** No score, tier or placed level moves. Not a dataset boundary. `D-8` is the offline report and never deploys; `D-9` rides the `EF-1` (a) combined deploy.
- ✅ **The Session C escalation trigger did NOT fire.** The snapshot's `Momentum:` line was re-formatted; no line was added or removed.
- ⚠ **The spec named three card sites for `D-9`. There are four.** The fourth, `BuildGroupFunding`, is the card twin of the snapshot line and printed the same config values. It moved in the same commit. See `docs/engine-fix-session-c-spec-back.md` §2.
- ⚠ **Session B2 was NOT started**, as instructed. It waits for the liquidation probe.

## 1. What changed — commit `ea32818`

| Ruling | File | Change |
|---|---|---|
| `D-9` | `Core/ScoringEngine_Types.vb` | New `VerdictResult.FundingStep3bLongPoints` / `FundingStep3bShortPoints` (default 0) |
| `D-9` | `Core/ScoringEngine_Calculate_Scoring.vb` | Step 3b records its ACTUAL delta per side (after the 0 floor and the `regimeMax` cap). Two reads, two assignments |
| `D-9` | `Core/FundingStep3bDisplay.vb` (new) | Host-agnostic: `EffectText`, `Tone`, `MomentumLine`, `CardValue` |
| `D-9` | `UI/MainForm_PlaintextSnapshot.vb` | `AppendFunding`: `Momentum:` line re-formatted |
| `D-9` | `UI/MainForm_Render_Cards.vb` | `BuildGroupFunding` row relabelled `Config:` → `Step 3b:`; three colour sites follow the effect |
| `D-8` | `analysis/AnalysisRunner.vb` | `ClassifyContextRow`; `ComputeContextOutcomes` fills three fields; ties counted, never walked |
| `D-8` | `analysis/AnalysisReport.vb` | `PopulationReport.LeanContextOutcomes`, `LeanTieCounts` |
| `D-8` | `analysis/MarkdownReportWriter.vb` | §6 (a) is a three-column table |
| both | `verify/ordercheck/Program.vb`, `verify/ordercheck/OrderCheck.vbproj` | Fixtures `A86`, `A87`; harness links the new Core file and `AnalysisRunner.vb` |
| both | `docs/UserManual.md`, `docs/DeribitIndicatorProject.md` | Funding row 2, card colours, report §4 note; one §15 row (784 B summary cell) |

**Not touched:** `settings.json` (stays v68), `AnalysisLogger.Header`, `docs/csv-rotation-riders.md`, `tools/WsTradeProbe/`, `docs/trader-tick-queue.md`.

## 2. The rendered changes

| Surface | Before | After |
|---|---|---|
| Snapshot `FUNDING` row 2 | `  Momentum: RISING  \|  Enabled: YES  \|  Soften: +1  \|  Amplify: -1` | `  Momentum: RISING  \|  Enabled: YES  \|  Effect: -1[L]` |
| Card FUNDING group, third row | `Config: Enabled=Y \| Soften=+1 \| Amplify=-1` (grey) | `Step 3b: Enabled=Y \| Effect=-1[L]` (coloured by the effect) |
| Card Funding Mom meter, signal row, footer aggregate | Colour by direction: amber `RISING`, green `FALLING` | Colour by effect: amber = penalty, green = soften, grey = none. Words unchanged |
| Offline report §6 (a) | One bullet per context; lean rows counted as trades; ties walked as shorts | Table: `DIRECTIONAL (traded)` · `LEAN NO TRADE (not traded)` · `TIE (not walked)` |

## 3. Fixture outcome

| Fixture | Checks | What it pins |
|---|---|---|
| `A86a` | 7 | `FundingStep3b*Points` on all six firing arms and the disabled arm, against cfg AND against the breakdown delta (enabled minus disabled) |
| `A86b` | 1 | Actual, not nominal: the penalty arm on a long score already at 0 → effect 0, tone neutral |
| `A86c` | 7 | Snapshot line text, card row text and card tone, per arm |
| `A87a` | 1 | `ClassifyContextRow` on 11 verdict shapes |
| `A87b` | 1 | The shipped cross-tab: trades n=2, lean n=4, ties 2 not walked, a lean-only context |
| `A87c` | 1 | The rendered three-column table |
| **Total** | **18** | Harness **435 → 453 PASS, `ALL PASS`**. Known-defects mode: 453 PASS + 2 `A81b` FAIL |

- `A34f` (the older §6 split fixture) still passes unchanged.
- Solution build: 0 warnings, 0 errors. `tools/checks/verify-gate.ps1 -Mode prepush`: `GATE PASSED`.

## 4. Mutation results (working tree, reversed with the inverse edit)

| Mutation | FAIL lines |
|---|---|
| Effect read from the note text (`-1` if the note has `-` and `[L]`) | `A86a` + `A86c` longs-FALLING, `A86b` — 3. ⭐ The note `STEP3b: +1[L] de-crowding` contains a `-` (in "de-crowding"), so the text parse read a soften as a penalty |
| Ties added to the lean walk | `A87b`, `A87c` — 2 |
| Long and short effects swapped | all six `A86a` and six `A86c` arm checks — 12 |

## 5. Commits

| Commit | Content |
|---|---|
| `ea32818` | Code, fixtures, manual, §15 row |
| (next) | This summary and the spec-back |
