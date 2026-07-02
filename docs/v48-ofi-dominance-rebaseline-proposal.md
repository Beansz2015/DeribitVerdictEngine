# v48 — OFI Dominance Re-baseline on the Geometric Distribution (proposal)

**Status:** ✅ **APPROVED 2026-07-03 — trader signed off §7 D1–D4 all as recommended**, with one addition folded in at sign-off: the **D3 post-ship divergence watch** (§4a) answering "how do we monitor if per-session fits diverge *after* shipping a global pair". The derivation runs the §4 recipe once §2 clears and produces a settings-only diff for approval.
**Owner:** derivation = Fable seat if the data gate clears by ~Jul 5–6; otherwise any coordinator/Opus seat can execute §4 mechanically — the judgment calls are pre-made in §7.
**Parent:** `time-averaged-ofi-proposal.md` (v46) + `time-averaged-ofi-spec-back.md` §5 (the re-baseline recipe this spec formalises) + `ofi-geometric-construction-spec.md` (why geometric). Roadmap W1 item 1.
**Class:** ⚠ settings-only calibration pass (one version bump, own dataset boundary — roadmap rule 1; P4 #5 builds only after this lands).

## 1. Why

`indicators.OFI.buy_dominant_ratio` (2.0) / `sell_dominant_ratio` (0.5) are **snapshot-era** thresholds. Since v46 the WS-path `OFIRatio` is a geometric (log-ratio) time-averaged value: the distribution is tighter (averaging removes sweep spikes) and centred near the book's true resting lean (ln-median ≈ +0.15 on the 06-30 NY DIAG session; geometric median ≈ 1.16). On the DIAG session the stale pair fired BUY 26.9% / SELL 17.2% — usable but unvalidated, and single-session. The re-baseline re-derives the pair on multi-session geometric data by firing-rate-matching to the snapshot era (the v40/v41 method), and reviews the `OFI.Momentum*` modifier whose input deltas shrank with averaging.

## 2. Data gate

- ≥ **2 full weekday session-days** of geometric rows **including ≥1 full NY session** (NY×1 is the tweaker's population and had zero geometric rows before 2026-07-02).
- Per population (NY×1 / LONDON×3 / ASIA×3): **n ≥ 150** geometric rows.
- Collection state at spec time: 07-01 partial (Asia-tail + London) + 07-02 running through NY. One more weekday (Jul 3) gives margin. **Check the gate at derivation time** — count rows per population from `analysis_log.csv` (geometric rows = post-rebuild 07-01; if the exact rebuild moment is ambiguous, use 07-02 00:00 UTC onward and accept the small loss).

## 3. Reference baseline (what we match to)

**Reference period = the v42→v45 snapshot-on-WS book (2026-06-24 → 2026-06-30 pre-v46-rebuild rows).** Rationale: same transport, same feed freshness, same sessions — the only change at v46 is the averaging construction, so matching its fire rates isolates the construction change exactly. Compute per population:

- `BUY_rate_ref` = share of rows with `OFISignal = BUY DOMINANT`
- `SELL_rate_ref` = share with `SELL DOMINANT`
- `DOM_rate_ref` = BUY_rate_ref + SELL_rate_ref (the combined "signal is active" rate)

Also record the BUY:SELL ratio of the reference (expected buy-leaning — snapshot arithmetic ratios on a bid-heavy book; this is context, not a target).

## 4. Derivation recipe

On the geometric book, per population and pooled:

1. **Fit a reciprocal pair** `(b, 1/b)`: find `b` such that `share(ratio > b) + share(ratio < 1/b) = DOM_rate_ref`. Reciprocal is the primary structure (D2 in §7): the classifier's semantics are multiplicatively symmetric and the geometric construction is exactly the one that makes a reciprocal pair meaningful; the v47-era audit confirmed the clamp is already log-symmetric.
2. **Report the per-side split** at the fitted pair vs the reference split. A BUY-heavier split on a bid-leaning book is *genuine market lean* (ln-median ≈ +0.15) and is accepted — do NOT force per-side equality by breaking reciprocity. Only if the split is grossly degenerate (one side < ~3% while the reference had both sides alive) escalate to D3.
3. **Pooled vs per-session:** compute the fitted `b` per population. If the per-population values sit within ~±10% of the pooled fit → **ship ONE global pair** (the keys are global, tweaker-tunable; per-session OFI thresholds would need new plumbing and more knobs — overfit risk for no demonstrated need). If they diverge materially → stop and bring the numbers to the trader (D3).
4. **`OFI.Momentum*` review (retire-or-retune):** reconstruct run-cadence momentum deltas from consecutive `OFIRatio` CSV rows (the `_ofiHistory` ring is run-cadence anyway; window=3). Measure RISING/FALLING/FLAT shares at the current `momentum_threshold` 0.15 on (a) the reference book, (b) the geometric book. Expected: averaged deltas are much smaller → the modifier is near-dead at 0.15. Then:
   - **Retune** (default): pick a threshold restoring the reference active rate (RISING+FALLING share) on the geometric distribution. Settings-only.
   - **Retire** (only if the evidence says so): if the reference-era modifier's fires show no conditional-outcome signal (check barrier outcomes on rows where the momentum bonus/suppression actually moved the score), recommend removal instead — that is a **scoring code change**, folds into this same v48 boundary, and needs explicit trader sign-off on the numbers (D4). The audit already flagged OFIMomentum as the top obsolescence candidate (momentum-of-a-10s-EMA at run cadence; #5 aggressor velocity covers the acceleration role at tape resolution).
5. **Output:** a settings diff — `buy_dominant_ratio` / `sell_dominant_ratio` (+ `momentum_threshold` retune, or the retire change) — version bump to the next number, change_log entry, §15 row, dated dataset-boundary marker (v42/v46 precedent, no CSV rotation). Fire-rate table (reference vs new, per population) goes in the spec-back.

**Script:** the derivation is a single PowerShell pass over `analysis_log.csv` (Import-Csv; percentile fits). The seat running it writes it as a throwaway in the session scratchpad — no repo tooling needed (v40/v41 precedent).

## 4a. D3 divergence — how it is monitored after shipping (added at sign-off, 2026-07-03)

The §4.3 check is one-shot (derivation-time). Divergence can also *emerge* later — session book character drifts, or the initial per-population samples were thin. The standing watch:

1. **Instrument:** `OFISignal` is CSV-logged per row, so the check is a one-pass query — per population (NY×1 / LONDON×3 / ASIA×3), the BUY/SELL/combined dominance fire rates at the shipped pair. The **spec-back ships the exact recipe** (a ~10-line script block) so any seat can re-run it identically; the W1 **signal-health audit re-runs report the same numbers automatically** (it is a per-population fire-rate instrument by design).
2. **§12 WATCHING row:** the v48 spec-back adds one — *"v48 OFI per-session fire-rate watch"*: after **≥2 further weekday session-days** on the shipped pair, recompute per-population rates. **Trigger:** any population's combined dominance rate outside **[0.6×, 1.5×] of its fitted target** across **two consecutive weekday sessions** (one hot session is variance; two is structure — the v40→v41 ASIA lesson).
3. **Response ladder (evidence-gated — each step only if the previous doesn't explain/absorb it):**
   - **(a) Check verdict-level impact first.** OFI is one vote of ~20 under a regime ceiling; a modest per-session misfit dilutes. If the affected population's failure-rate matrix and OFI-conditional outcomes don't move, accept and keep watching (no knob added).
   - **(b) Pooled retune.** Re-run §4 on the enlarged pooled book (settings-only, cheap, no new keys) — right answer when *all* sessions drifted together (regime change, not session structure).
   - **(c) Per-session overrides — last resort.** New nullable `session_volume.sessions[].ofi_buy_dominant_ratio`/`ofi_sell_dominant_ratio` bucket keys mirroring the `roc_magnitude_threshold` pattern (v40), own small spec, **hand-tuned / off-tweaker by construction** (array-nested = unreachable by the `Split(".")` resolver, HC11 class). This adds knobs — the Accuracy Ceiling's #1 risk — so it needs the (a)/(b) evidence trail first.

## 5. What this pass does NOT touch

`avg_window_sec` (10 — stays; the ratios are derived FOR this window, and the window↔ratio coupling caveat from v46 §10 stands: if the window is ever hand-changed, re-check the ratios), `book_depth`, `averaging_enabled`, the accumulator code, CSV schema (v0.7), eval cache (v4). No renderer/card/snapshot line changes (values change, labels don't) → no parity obligation beyond the standing rule.

## 6. Acceptance

- Builds 0/0 (settings-only ⇒ trivially; if D4=retire, full gate applies to the code change).
- Harness A1–A21b unregressed (fixtures pass explicit ratios → byte-identical regardless of settings values). If D4=retire: the affected fixture(s) updated in the same commit with the change documented.
- Spec-back carries: the gate check (row counts), the fire-rate table, the reciprocity/per-side numbers, the pooled-vs-per-session comparison, and the Momentum* decision evidence.
- Live sanity after flip: OFISignal distribution over the first ~100 runs roughly matches the fitted rates.

## 7. Sign-off decisions (trader)

| # | Decision | Recommendation |
|---|---|---|
| D1 | Reference period = v42→v45 snapshot-on-WS book | **Yes** — isolates the construction change; pre-v42 REST rows are a different feed |
| D2 | Reciprocal pair `(b, 1/b)` as the primary structure, per-side split reported not forced | **Yes** — matches the classifier's multiplicative semantics; the geometric construction exists precisely to make this valid |
| D3 | Global pair unless per-session fits diverge >~10% (then escalate with numbers) | **Global** — fewer knobs, keys already on the tweaker surface, no plumbing |
| D4 | `OFI.Momentum*`: retune by default; retire only on conditional-outcome evidence (scoring change, same boundary) | **Retune-by-default**, decide retire on the derivation-time numbers |
