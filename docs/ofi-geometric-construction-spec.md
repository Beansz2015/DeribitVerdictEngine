# OFI Geometric (Log-Ratio) Averaging — Construction Switch (v46 amendment)

**Status:** READY FOR IMPLEMENTER — the seat that built v46 (`ab97f40`) + the DIAG (`eee6e4b`). Decided **empirically** by the NY DIAG test 2026-06-30; supersedes the arithmetic construction in `time-averaged-ofi-proposal.md` §4.1. **Local-first: commit as you go, do NOT push** (trader tests + pushes). Coordinator reviews the spec-back.

## Why (the data)
The throwaway DIAG (`eee6e4b`) logged arithmetic vs geometric averaged `OFIRatio` over a **616-row / 5.1-hour NY session** (13:42–18:49 UTC) that closed **net-flat** (price 58421 → 58502, +0.14%; ranged ~1.8% both ways). On that flat tape, on the *same* book:

| metric | arithmetic (current v46) | geometric (log-ratio) |
|---|---|---|
| median ratio | **2.69** (above the 2.0 BUY-DOM line) | **1.10** (near balance) |
| BUY : SELL dominant | **386 : 32 (12:1)** | 153 : 112 (1.4:1) |
| sell-leaning (ratio<1) | 15% | 47% |
| gap arith/geo | median 2.3×, max 17.7× | — |

The two constructions disagree on the OFI signal in **306/616 (~50%)** of runs. The arithmetic mean of a multiplicatively-symmetric ratio manufactures a severe buy-bias (Jensen / AM≥GM) that firing-rate-match **cannot** remove (it's distribution *shape*, not *level*). Geometric is symmetric → ship it. The DIAG's `geo_ratio` column is exactly this construction's output.

## Scope
Convert the v46 averaged-OFI **ratio** from an arithmetic EMA to a **geometric (log-space) EMA**: fold `ln(ratio)`, return `exp(emaLn)` at read. **v46 is unpushed, so this folds INTO v46** — the dataset boundary lands on the final construction; **no version bump** (v47 stays reserved for the data-gated threshold re-baseline). The weighted bid/ask volumes stay **arithmetic** (display context). `averaging_enabled=false` stays the snapshot `CalcOFI` rollback (unchanged, byte-identical to v45).

## Steps

**1. Revert the throwaway DIAG.** `git revert --no-edit eee6e4b` — clean (nothing after it touched those files). Removes `Core/OfiGapDiagnostic.vb`, the `[DIAG]` geometric track, and the `WriteSample` call in `MainForm_Analysis`, returning `OfiAccumulator` to pure v46 arithmetic.

**2. Convert `OfiAccumulator` to geometric (new commit).** In `Core/OfiAccumulator.vb`, replace the arithmetic ratio EMA with a log-space one:
- First fold seeds `_emaLnRatio = Math.Log(Math.Max(ratio, 0.000001))`.
- Decay fold: `_emaLnRatio += alpha * (Math.Log(Math.Max(ratio, 0.000001)) - _emaLnRatio)`.
- Read: `Snapshot.Ratio = Math.Exp(_emaLnRatio)` (the geometric mean).
- Remove the now-unused arithmetic `_emaRatio` (it only fed the ratio). **Keep `_emaBid`/`_emaAsk` arithmetic** (display).
- `dt` floor + `tauSec <= 0` overwrite, warmup gate, and `Reset()` are unchanged in mechanism (Reset now clears `_emaLnRatio`).
- Update the class header: the ratio is a **geometric (log-ratio) EMA** — multiplicatively unbiased for the symmetric bid/ask ratio (cite the NY test 2026-06-30). Same `alpha = 1 - exp(-dt/tau)`, applied in log space.

**3. Harness (`verify/ordercheck/Program.vb`).**
- **A20c** (constant 2.0 → avg 2.0): still holds (geomean of a constant = the constant). Keep.
- **A20d** (time-aware step): value changes to geometric. Seed ratio 1.0 (lnseed 0), fold 2.0 at `dt=tau` (alpha = 1−e⁻¹ = 0.63212): `emaLn = 0.63212·ln2 = 0.43816`, `Ratio = exp(0.43816) = `**`1.5500`**. Update the expected 1.6321 → ~1.5500 (±0.001) and relabel it geometric.
- **A20i (NEW — the point of the switch):** fold alternating 2.0 / 0.5 at equal `dt` to steady state → geometric mean converges to **~1.0** (arithmetic would give ~1.25). Assert `Ratio ∈ [0.95, 1.05]`. Locks in the multiplicative symmetry.
- **A20e/A20f** (warmup/reset) unchanged.

**4. Docs / dataset-boundary marker.**
- `settings.json` change_log v46 + `DeribitIndicatorProject.md §15`: "time-AWARE EMA" → "time-AWARE **geometric (log-ratio)** EMA (`Ratio = exp(EMA(ln ratio))`) — multiplicatively unbiased; chosen over arithmetic per the NY DIAG test (arithmetic gave a 12:1 buy skew on a flat market vs geometric 1.4:1)." **No version bump** (still v46).
- `time-averaged-ofi-proposal.md §4.1` + `time-averaged-ofi-spec-back.md §3.2`: add a note that the averaging space was decided empirically = **geometric/log-ratio** (supersedes the proposal's arithmetic fold of the raw ratio — same alpha, applied to `ln(ratio)`).
- Note in the change_log that the v47 dominance-threshold re-baseline (firing-rate-match) + the `OFI.Momentum*` review now run on the **geometric** distribution.

**5. User docs (light check).** `UserManual.md §12` / `TraderGuide.md` OFI sections say "time-weighted average" (construction-agnostic) — confirm they don't assert *arithmetic* specifically; the cosmetic `ratio ≠ bid/ask` note still holds (geometric ratio vs arithmetic bid/ask). Likely no edit.

## Acceptance
- Build **0/0** (solution Release + AutoTweaker + OrderCheck) — or `tools/checks/verify-gate.ps1 -Mode prepush`.
- Harness **A1–A20 pass** with the updated A20d + the new A20i symmetry fixture.
- **`averaging_enabled=false` still byte-identical to v45** (geometric only affects the warmed WS-averaged path; the rollback is snapshot `CalcOFI`, untouched).
- `OfiAccumulator` references no `System.Windows.Forms`; `OfiGapDiagnostic.vb` is gone.
- No new/removed/renamed rendered line (OFIRatio value shifts, same field) → no card-binding obligation.
- Spec-back to the coordinator (re-run builds + harness + diff audit, same as the v46 review).

## Out of scope
- The v47 dominance-threshold re-baseline (data-gated, multi-session — collect on this geometric build) and the `OFI.Momentum*` review. Tonight's NY `geo_ratio` distribution is a preview only (NY-only, one session).
