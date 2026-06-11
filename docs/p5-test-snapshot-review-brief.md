# P5-Test Snapshot Review — Brief (visual + parity audit of the 55-case set)

**Date:** 2026-06-10
**Reviewer model:** ~~Fable 5 at Max effort~~ **AMENDED 2026-06-11 — BUDGET MODE (supersedes the model/effort/coverage lines below):** Opus at **High** effort, on a **~12-case representative subset**, ~3 crops per case maximum. Rationale: the full-55 Max-Fable run costs ~10% of a 5-hour usage window *per case* — unaffordable on the current plan, and largely redundant: visual defects (clipping, colour inversion, layout) are **code-path-level, not case-level** — a clipped cell clips in every case that populates it, so render-path coverage ≈ defect coverage. **Subset selection (reviewer picks exact cases from `test-results.md` descriptions):** one case per render path — STRONG LONG full confluence, STRONG SHORT full confluence (colour-inversion check priority), one WEAK of each direction with a context tag, NO TRADE balanced, NO TRADE MTF-block, TRANSITIONAL with penalty, plus cases exercising: CAPPED target with reason label, structural rows populated both sides, Kelly CAPPED, hold/exit with a declared position, and one FLOW_UNCONFIRMED/STRUCTURALLY_WEAK. The §3 worksheet, §2 exclusion list, and §4 output format apply to the subset; the coverage matrix lists all 55 rows with non-subset rows marked `NOT REVIEWED (budget mode — path covered by case NN)`. Two cases were already reviewed at Max by a stopped Fable run — fold its notes in if available, don't redo them.
**Mode:** FIND-ONLY. No code changes, no fixes, no commits to source. The output is a findings report; fixes are consolidated into a separate implementation kickoff afterwards (find-vs-fix separation, same discipline as the engine audit).

---

## 1. What you are reviewing

`C:\Dev\DeribitVerdictEngine\bin\Debug\net8.0-windows\verify\p5-test\` contains 55 synthesised test cases, each with three artifacts:

- `NN_case_slug.png` — full-form screenshot of the card UI rendering that case (fit-scale, tall capture including the plaintext block at the bottom of the form).
- `NN_case_slug-legacy.txt` — the legacy RTF renderer's text output for the same `VerdictResult`. **This is ground truth for content.**
- `NN_case_slug-snapshot.txt` — the `BuildPlaintextSnapshot` output (the machine-compared parity layer).

`test-results.md` in the same directory: generated 2026-05-28, 55/55 **text** parity (legacy ↔ snapshot). That harness compared text to text. **Nobody has systematically verified the rendered card pixels** — that is your job, on both axes:

**(a) Visual defects** — anything out of place in the rendering: clipped text (cell, row, or border clipping), misalignment (columns, label/value pairs), **direction-colour inversions** (a SHORT value in the long/green colour, a LONG value in red — these are CRITICAL), borders/rounded corners clipping, section boxes out of shape, overlapping controls, truncation, spacing anomalies, gauge/histogram rendering inconsistent with the underlying values.

**(b) Legacy ↔ PNG content parity** — every value, sign, unit, status word, tag, and breakdown row in `-legacy.txt` must appear correctly in the rendered cards: numbers exact, statuses exact (RISING/FALLING, PASS/BLOCK, context tags), capped targets with their reason labels, R:R values, Kelly numbers, SIGNAL BREAKDOWN rows **including the TOTAL row arithmetic**, regime/session strings. Cards showing *extra* detail beyond the legacy text (histograms, meters) is by design — flag **contradictions**, not additions. When PNG disagrees with legacy.txt, check `-snapshot.txt` to localise the break: snapshot-matches-legacy ⇒ card *binder* bug; snapshot-differs ⇒ deeper.

Review **this existing set** — do not regenerate it. (Freshness note for your report: the set predates the 2026-06-10 Tier C commits, which touched no card rendering on the happy path.)

## 2. Read these before looking at a single pixel

In order — they prevent the two failure modes of this review: re-reporting known items, and flagging intentional design as defects.

1. `docs/ui-reskin-handover-2026-05-27.md` **§4 locked decisions** — what is intentional. Highlights you must not flag: MTF Gate row absent from the card SIGNAL BREAKDOWN (it IS in the legacy text — that asymmetry is locked, by design, on every case); HOLD/EXIT block hidden when no position is declared; three MTF reason formats; STRONG verdicts coexisting with warning context tags; v30 sub-noise CAPPED suppression (`|raw − adjusted| < max(0.5, ATR × 0.02)` hides the amber label by design).
2. `docs/ui-reskin-p5-test-gap-fixes-proposal.md` — the ~30 items already found and fixed. For these, you are doing a **regression check**: confirm fixed, report only if broken again.
3. `docs/ui-reskin-p5-test-visual-review-handoff.md`, `docs/ContentRequestsAfterVisualReview.txt`, `docs/VisualReviewQuestions.txt` — the trader's raw review trail; context for what "done" looks like.
4. `docs/ui-reskin-atr-clip-fix-kickoff.md` — **KNOWN-0**, the one open confirmed defect: the ATR ENTRY LEVELS card clips the third content line in its zone rows (the Q2 `(risk N / rwd N)` line under R:R, and the C1b `(label)` parenthetical in the CAPPED cell). Confirm its presence as a baseline, label it KNOWN-0, do not re-report as new — but **actively hunt the same disease elsewhere**: any other card/cell whose content lines exceed its pixel budget (STRUCTURAL rows, KELLY block, INDICATOR DETAILS blocks, breakdown table rows).
5. `UI/Theme/Theme.vb` — grep the palette tokens so colour judgements are anchored to hex values, not vibes: the verdict 7-tier ramp, `ACC_STRONG_LONG`/long-side greens, `ACC_SHORT` red, `ACC_WARN` amber (reserved for CAPPED/warnings), `FG_PRIMARY`…`FG_QUATERNARY` greys. A long-side value rendered in the short-side colour (or vice versa) is a CRITICAL finding even if the number is correct.

Do not read other engine source. If a parity question needs a binding checked, `UI/MainForm_Render_Cards.vb` is the card binder — targeted greps only.

## 3. Method (mandatory)

- **Crop-and-zoom is non-negotiable.** The fit-scale full PNG hides clipping — that is exactly how the ATR clip survived earlier passes. Before reviewing, write a small PowerShell crop helper (System.Drawing: load PNG, crop rect, save) and generate per-region crops into a scratch dir (e.g. `verify\p5-review-crops\` — gitignored area). Review dense regions (ATR card, STRUCTURAL pair, SIGNAL BREAKDOWN, KELLY, INDICATOR DETAILS) from crops, not from the full image.
- **Fixed per-case worksheet, completed before moving to the next case.** For each case record a verdict per region — `OK` / `F-nn` (finding ref) / `KNOWN-0` / `N/A` — across: top bar; SCORE/VERDICT/LAST PRICE cards; ATR ENTRY LEVELS; STRUCTURAL LONG; STRUCTURAL SHORT; SIGNAL BREAKDOWN (+ TOTAL arithmetic vs legacy); OI×CVD; VOLUME PROFILE; KELLY; INDICATOR DETAILS blocks; bottom plaintext block; **legacy-parity tick-off** (every legacy.txt value located in the PNG or a discrepancy recorded).
- **All 55 cases. No sampling.** Batch in groups of ~5. If context runs low, stop cleanly at a batch boundary, write the report with the coverage matrix as-is, and state the resume point explicitly — a partial report with honest coverage beats a complete report with silent skips. A follow-up conversation resumes from the matrix.
- Variety note: the 55 cases were designed to exercise different verdicts/regimes/contexts (case slugs describe them). Direction-colour checks matter most on the SHORT-side cases (06–08 and mirrors) — historically inversions hide there because reviewers anchor on the long cases.

## 4. Output

Write `docs/p5-test-snapshot-review-report.md`:

1. **Findings table** — `F-nn | severity | case(s) | card/region | what's wrong | evidence crop path`. Severities: **CRITICAL** = wrong value/direction/colour-inversion (could mislead a trade decision); **HIGH** = clipped/illegible load-bearing value; **MED** = misalignment, box/border defects; **LOW** = cosmetic. A defect appearing on many cases is ONE finding listing all affected cases.
2. **Coverage matrix** — all 55 rows × the worksheet regions, every cell filled. This is the proof of systematic coverage.
3. **Regression confirmations** — gap-fix items verified still fixed; KNOWN-0 confirmation.
4. **Caveats** — anything you could not judge from the artifacts (e.g. needs a live run), freshness notes.

No source edits, no commits. The report is triaged by the spec-author seat; confirmed findings + KNOWN-0 then become one consolidated fix kickoff (Opus) so the build → fix → regenerate → re-review cycle runs once, not twice.
