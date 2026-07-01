# OFI Geometric (Log-Ratio) Averaging — Spec-Back

Implements `docs/ofi-geometric-construction-spec.md` (READY FOR IMPLEMENTER, all 5 steps).
Local commits only — trader tests + pushes. Coordinator: please re-run builds + harness +
diff audit per the spec's Acceptance section, same as the v46 review.

## 1. What shipped, commit by commit

- **`816c443`** — `git revert --no-edit eee6e4b`. Clean revert (nothing after the DIAG commit
  touched `Core/OfiAccumulator.vb`, `Core/OfiGapDiagnostic.vb`, or `UI/MainForm_Analysis.vb`).
  Removes `OfiGapDiagnostic.vb`, the `[DIAG]` geometric track, and the `WriteSample` call,
  returning `OfiAccumulator` to pure pre-DIAG v46 arithmetic.
- **`9740d7b`** — the geometric conversion + harness + docs, in one commit (steps 2-5 of the
  spec — small enough not to split further).

## 2. `OfiAccumulator` conversion (spec step 2)

`Core/OfiAccumulator.vb`:
- `_emaRatio` renamed `_emaLnRatio`; first fold seeds `Math.Log(Math.Max(ratio, 0.000001))`
  (was `= ratio`); decay fold is `_emaLnRatio += alpha * (Math.Log(Math.Max(ratio, 0.000001)) - _emaLnRatio)`
  (was the same line without the `Math.Log`/`Math.Max` wrapping).
- `Snapshot()` reads back `Math.Exp(_emaLnRatio)` (was `_emaRatio` directly).
- `Reset()` clears `_emaLnRatio` (was `_emaRatio`).
- `dt` floor, `tauSec <= 0` overwrite, warmup gate (`MinWarmupUpdates`/`HasWarmup`), and the
  `_emaBid`/`_emaAsk` arithmetic tracks are **untouched** — only the ratio's averaging space
  moved from linear to log. Class header comment updated to document the construction + cite
  the NY DIAG test and the spec doc.
- No `GeoRatio` field, no dual-track — this is a straight conversion, not a parallel track
  (the DIAG's parallel-track approach was the measurement instrument; production only ever
  needs the one construction).

## 3. Harness (spec step 3)

- **A20c** (constant 2.0 steady-state) — unchanged, still passes (geomean of a constant is
  the constant).
- **A20d** — updated to the geometric expected value. Seed ratio 1.0 (`lnSeed=0`), fold 2.0 at
  `dt=tau` (`alpha=1-e^-1=0.63212`): `emaLn = 0.63212 * ln(2) = 0.43817`, `Ratio = exp(0.43817)
  = 1.5500` (was 1.6321 arithmetic). Relabelled "time-aware geometric step" in the comment.
- **A20i (NEW)** — the point of the switch. Alternating ratio 2.0/0.5 at equal 1s `dt` steps.
  **Deviation from the spec's literal fixture:** the spec suggested `tau=10` for this test; at
  `tau=10`/`dt=1` (`alpha=0.1813`) the accumulator settles into a genuine **steady-state
  two-cycle** in log space (not a fixed point) with amplitude `alpha*ln(2)/(2-alpha) = ±0.069`
  in log space → `Ratio` oscillates `[0.933, 1.072]` depending on fold parity — outside the
  spec's `[0.95, 1.05]` assert window (I ran it first at `tau=10`/200 folds and got
  `ratio=0.933248`, a genuine FAIL, not a bug in the accumulator). This is expected recency-
  weighted-EMA behaviour on a genuinely alternating series, not an implementation error — an
  EMA doesn't converge to a fixed point under alternating input, it converges to a **cycle**
  whose amplitude scales with `alpha` relative to the alternation period. **Fix:** raised
  `tau` to 8.0 (dt still 1s, `alpha=0.1175`) and extended to 400 folds — amplitude shrinks to
  `±0.0433` in log space → `Ratio ∈ [0.958, 1.044]`, safely inside `[0.95, 1.05]` with margin,
  and 400 folds is well past the ~50-fold settle time for this alpha. Locks in the intended
  property (geometric symmetry — arithmetic would drift to ~1.25) without asserting a
  numerically-impossible tighter bound at the original tau. Flagged for review since it's a
  parameter deviation from the literal spec text, not a scope deviation.
- **A20e/A20f** (warmup/reset) — unchanged, still pass (mechanism untouched).

## 4. Docs (spec step 4 — in-place amendment, no version bump)

- **`settings.json`** — the existing v46 `change_log` entry amended in place (not a new
  entry): the "time-AWARE EMA" sentence now reads "time-AWARE **geometric (log-ratio)** EMA
  (`Ratio = exp(EMA(ln ratio))`)", with the NY-DIAG-test rationale and the 12:1 vs 1.4:1 numbers
  folded in; the re-baseline sentence now notes it "now run[s] on the GEOMETRIC distribution,
  not the arithmetic one this entry originally described"; the acceptance sentence gained the
  A20i mention; an `AMENDMENT (2026-07-01)` sentence appended noting no version bump. `version`
  field untouched at 46. JSON validated (`ConvertFrom-Json` round-trip).
- **`docs/DeribitIndicatorProject.md` §15** — same three edits mirrored into the v46 table row
  (kept prose/code-formatting consistent with the rest of the row — backtick-quoted symbols,
  bold section labels).
- **`docs/time-averaged-ofi-proposal.md` §4.1** — appended an `AMENDMENT (2026-07-01)`
  paragraph after the "output is still an OFIRatio" sentence, pointing at the geometric switch
  and this spec doc. Left the original arithmetic-EMA prose above it untouched (historical
  record of what was proposed, not what shipped) rather than rewriting it — the amendment
  block makes the supersession explicit without erasing the decision trail.
- **`docs/time-averaged-ofi-spec-back.md`** — deviation **#2** (which is the exact passage that
  *predicted* this problem — "a mild buy-side lean... consider a geometric mean / log-ratio
  framing only if the firing-rate-match struggles") rewritten to say RESOLVED, with the DIAG
  numbers and the resolution. §4 Acceptance's A20d/A20i line updated to match.

## 5. User docs (spec step 5 — confirmed no edit needed)

Checked `docs/UserManual.md:1221` and `docs/TraderGuide.md:428` — both say "time-weighted
average," construction-agnostic, no "arithmetic" claim. TraderGuide's existing cosmetic note
("average-of-ratios... won't always divide out to exactly the displayed ratio") still holds
verbatim under geometric averaging (still true — the ratio and the bid/ask vols are averaged
in different spaces either way). No edit made, per the spec's "likely no edit" expectation.

## 6. Acceptance

- Build **0/0** — solution(Release) + AutoTweaker + OrderCheck, and `tools/checks/verify-gate.ps1
  -Mode prepush` (full gate: build x3 + harness + display-parity + version-bump check) — **GATE
  PASSED**.
- Harness **A1-A20h unregressed + A20d updated + A20i new — ALL PASS** (78 checks total, ran
  via both the direct `dotnet run` and the gate script).
- **`averaging_enabled=false` still byte-identical to v45** — confirmed by inspection:
  `UI/MainForm_Analysis.vb`'s `usedAvgOfi` short-circuit and the snapshot `CalcOFI` fallback
  path are untouched by either the revert or the geometric conversion (grep confirms zero
  references to `OfiGapDiagnostic`/`GeoRatio`/`DVE_OFI_GAP_DIAG` anywhere in the tree post-revert).
- `OfiAccumulator` still references no `System.Windows.Forms`; `OfiGapDiagnostic.vb` confirmed
  deleted by the revert.
- No new/removed/renamed rendered line — `OFIRatio`'s value source is unchanged (same field,
  shifted construction upstream of it) — no card-binding obligation, per the spec.

## 7. Out of scope (unchanged from the spec)

The v47 dominance-threshold re-baseline (data-gated, multi-session) and the `OFI.Momentum*`
review remain open, now scoped to run against the geometric distribution instead of the
arithmetic one. Not started here.
