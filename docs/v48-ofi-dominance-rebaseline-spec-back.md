# Spec-Back — v48 OFI Dominance Re-baseline (2026-07-03)

**Spec:** `v48-ofi-dominance-rebaseline-proposal.md` (APPROVED 2026-07-03, D1–D4 as recommended + §4a).
**Result:** `indicators.OFI.buy_dominant_ratio` 2.0 → **1.60**, `sell_dominant_ratio` 0.5 → **0.625**; `momentum_threshold` **unchanged** (0.15). Settings-only; settings **v47 → v48** in both copies (the bin flip is the live dataset boundary — the running collector hot-reloaded it at ~16:35 UTC 07-02). POCO defaults untouched (ride the next code commit; v33/v34/v37/v41 precedent). Derivation run by the spec-author seat (Fable), one pass over `analysis_log.csv` per the §4 recipe.

## 1. Gate check (§2)

Geometric rows = Timestamp ≥ **2026-07-01 07:02 UTC** (the Debug exe's geometric-build timestamp 07-01 07:01:45Z; all runs since are that binary). Total **991** over 2 weekday days: 07-01 n=630 (UTC hours 7–21 — includes a **near-full NY session, hours 13–21**), 07-02 n=361 (hours 5–16, NY continuing at derivation time). Per population: **NY×1 = 722 ✓, LONDON×3 = 200 ✓, ASIA×3 = 69** (< the 150 floor — see §3; the reference period turns out to contain **zero** Asia rows, so a per-session Asia fit was impossible on either side regardless; Asia lands under the §4a watch by construction). Gate judged **met for the D3-recommended global fit**.

## 2. Reference vs geometric (the §3/§4 tables)

Reference = snapshot-on-WS book, 2026-06-24 00:00 → 2026-06-30 13:00 UTC (ends before the arithmetic-averaging build deployed for the NY DIAG run):

| Population | n | BUY @2.0 | SELL @0.5 | DOM |
|---|---|---|---|---|
| NY×1 | 675 | 36.4% | 28.4% | 64.9% |
| LONDON×3 | 392 | 32.4% | 27.8% | 60.2% |
| ASIA×3 | 0 | — | — | — |
| **POOLED** | **1067** | **35.0%** | **28.2%** | **63.2%** |

(Hour-band note: the spec's partial-NY concern was moot — the reference NY rows cover the same 13–21 UTC band as the geometric sample, so banded = full.)

Geometric (991 rows):

| Pair | BUY | SELL | DOM |
|---|---|---|---|
| stale 2.0 / 0.5 | 31.0% | 18.1% | **49.0%** (under-firing, as v46 predicted) |
| fitted 1.593 / 0.628 | 38.5% | 24.6% | **63.2%** ✓ target |
| **shipped 1.60 / 0.625** (exact reciprocal rounding) | ≈38% | ≈24% | ≈63% |

Per-side split at the fit is more buy-lean than reference (1.56:1 vs 1.24:1) — **accepted per D2**: the geometric ln-median is +0.15 (a genuine resting bid-lean), both sides remain well alive, and forcing symmetry would break the reciprocal structure for no informational gain.

## 3. Per-session fits (D3 — numbers brought to the trader, ruling: global)

| Population | own-target fit b | divergence vs pooled 1.593 |
|---|---|---|
| NY×1 (target 64.9%) | 1.505 | −5.5% ✓ |
| LONDON×3 (target 60.2%) | 1.779 | **+11.7%** — marginally over the ±10% line, but a percentile fit at n=200 carries ≈±10% sampling error on its own, so this is not structural evidence |
| ASIA×3 | unfittable (geo n=69; reference n=0) | — |

**Shipped global; LONDON + ASIA are named in the §12 "v48 OFI per-session fire-rate watch"** (added this commit): after ≥2 further weekday session-days, recompute per-population rates; trigger = combined dominance outside [0.6×, 1.5×] of 63.2% across 2 consecutive weekday sessions; ladder = verdict-impact check → pooled retune → per-session bucket overrides (last resort).

### §4a watch recipe (run verbatim)

```powershell
$log = Import-Csv '<bin>\analysis_log.csv' | Where-Object { $_.Timestamp -ge '<watch-start-utc>' }
$log | Group-Object { $h=[int]$_.Timestamp.Substring(11,2); $s=if($h -le 7){'ASIA'}elseif($h -le 12){'LONDON'}else{'NY'}; "$s|$($_.ExecResolution)" } |
  ForEach-Object { $n=$_.Count
    $b=@($_.Group|? {$_.OFISignal -eq 'BUY DOMINANT'}).Count; $s=@($_.Group|? {$_.OFISignal -eq 'SELL DOMINANT'}).Count
    '{0}  n={1}  BUY {2:P1}  SELL {3:P1}  DOM {4:P1}  (target 63.2%; trigger <37.9% or >94.8% on 2 consecutive weekdays)' -f $_.Name,$n,($b/$n),($s/$n),(($b+$s)/$n) }
```

## 4. Momentum review (D4) — threshold unchanged; a finding handed onward

Run-cadence |Δ₃| of consecutive `OFIRatio` rows (window 3 = the `_ofiHistory` ring; session gaps >15 min skipped): reference active rate (RISING+FALLING) at 0.15 = **90.6%** (n=1052); geometric = **89.2%** (n=985); the threshold that would exactly match the reference on geometric = 0.136 ≈ current. **So 0.15 stays** — the construction change did not move this input.

**Finding (not acted on tonight):** a "momentum" modifier active on ~9 runs in 10 — in both eras — is nearly always-on, which questions its information content regardless of averaging. Per D4, retirement needs conditional-outcome evidence → handed to the **roadmap W1 signal-health audit** as a named question (measure barrier outcomes conditional on the momentum bonus/suppression actually moving the score).

## 5. Acceptance

- Settings-only; both copies at v48 with identical values; change_log entry (newest-first) + §15 row + §6 pointer updated; §12 watch row added.
- Gate re-run post-change: 3 Release builds 0/0; **A1–A21b unregressed** (fixtures pass explicit ratios — byte-identical by construction).
- Live sanity (trader, over the first ~100 post-flip runs): OFISignal split should read roughly BUY ~38% / SELL ~24% / BALANCED ~37% in NY; a grossly different split within the first day = re-check before trusting the pair.
- **Unblocked downstream:** the v48 boundary closes roadmap rule-1's open window — **P4 #5 aggressor velocity may now be built** (spec already approved).
