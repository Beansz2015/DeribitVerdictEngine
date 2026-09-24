# Spec-back — Q-1 option (d), target and stop distance by tier

**Written:** 2026-09-24 (UTC) by the Q-1 (d) analyst seat. **Question:** [`docs/medium-tier-diagnosis-spec-back.md`](medium-tier-diagnosis-spec-back.md) decision `Q-1`, option (d) (trader ruling 2026-09-18). **Outcome record:** [`docs/q1d-tier-geometry-read-2026-09-24.md`](q1d-tier-geometry-read-2026-09-24.md) (the read). **Full output:** [`docs/q1d-tier-geometry-2026-09-24-output.md`](q1d-tier-geometry-2026-09-24-output.md). **Format:** [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md). **Model / effort used:** Opus, high.

**One-line result:** the higher success rate at the top tier is paid for by a lower payoff ratio (higher tiers lack a swing target, so they place the nearer ATR fallback target) and by fewer timeouts; no tier gap in success rate, gross edge or net EV per trade is CONFIRMED in any session.

**Legend. IDs used in this packet:**

| ID | Source and kind | Meaning |
|---|---|---|
| `H-n` | This packet, handle | A check the reader can run, with output pasted from this seat's run |
| `E-n` | This packet, build-time evidence | A check the reader cannot re-run as committed |
| `QD-n` | This packet, decision queued | A decision for the trader or the orchestrator (section 3). Named `QD-` so it does not collide with `Q-1` … `Q-3` in `docs/medium-tier-diagnosis-spec-back.md` |
| `Q-1` | Decision in `docs/medium-tier-diagnosis-spec-back.md` §3 | The tier-ladder decision; option (a) = `F-1`, (c) = `F-4a`, (e) = `F-4b` (REJECTED) |
| `F-1`, `F-4a`, `F-4b` | Fix classes in `docs/medium-tier-diagnosis-read-2026-09-17.md` §3 | `F-1` = do not re-cut thresholds or the tier floor; `F-4a` = one class for Kelly sizing only; `F-4b` = re-map the payload `confidence` field (REJECTED) |
| P0, H1, H2, H3 | The read, legend | P0 = the prior read's 8,810 rows with its halves H1 / H2; H3 = 1,710 forward rows 2026-09-14 to 2026-09-24 |

---

## 1. Ranked verification handles

**If you only run one, run `H-1`.** It reads the load-bearing NY lines from the committed output. `H-2` proves the output regenerates. Every command runs from the repo root in Git Bash.

**Prerequisites for the re-run handles (`H-2`, `H-6`):** the read's re-run section (`docs/q1d-tier-geometry-read-2026-09-24.md` §9). The P0 inputs come from the prior read's re-run (`docs/medium-tier-diagnosis-read-2026-09-17.md` §6).

### H-1 — the headline numbers (NY STRONG − WEAK)

```
F=docs/q1d-tier-geometry-2026-09-24-output.md; awk '/^### 2.1/,/^## 3\./' $F | grep -E "^\| NY \| STRONG - WEAK \| share with no swing target"; awk '/^## 5\./,/^## 6\./' $F | grep -E "^\| NY \| STRONG - WEAK \| (payoff ratio sum T / sum S|gross breakeven rate, pp|success rate, pp|timeout share, pp|net EV per trade, bps) \|"; awk '/^## 10\./,/^## 11\./' $F | grep -E "^\| NY \| STRONG - WEAK \| gross edge, carried 24 h"
```

```
| NY | STRONG - WEAK | share with no swing target, pp | +31.2 [+24.7, +37.6] (342/3523) | +35.5 [+25.4, +43.9] | +28.3 [+19.8, +37.0] | CONFIRMED (d > 0) |
| NY | STRONG - WEAK | payoff ratio sum T / sum S | -0.11 [-0.15, -0.06] (342/3523) | -0.11 [-0.18, -0.04] | -0.10 [-0.15, -0.04] | CONFIRMED (d < 0) |
| NY | STRONG - WEAK | gross breakeven rate, pp | +2.4 [+1.3, +3.3] (342/3523) | +2.4 [+0.9, +3.8] | +2.3 [+0.8, +3.4] | CONFIRMED (d > 0) |
| NY | STRONG - WEAK | success rate, pp | +5.8 [+0.2, +10.8] (342/3523) | +3.5 [-4.8, +10.8] | +7.4 [-0.8, +14.0] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | timeout share, pp | -5.8 [-7.5, -3.9] (342/3523) | -5.4 [-8.2, -2.1] | -6.1 [-8.2, -3.7] | CONFIRMED (d < 0) |
| NY | STRONG - WEAK | net EV per trade, bps | -0.0 [-1.8, +1.6] (342/3523) | -0.4 [-2.4, +1.5] | +0.2 [-2.6, +2.7] | NO DIFFERENCE SHOWN |
| NY | STRONG - WEAK | gross edge, carried 24 h, pp | +0.2 [-5.4, +5.4] (342/3523) | -2.1 [-10.6, +5.6] | +1.8 [-6.0, +8.9] | NO DIFFERENCE SHOWN |
```

- **Load-bearing values:** the payoff ratio and gross breakeven CONFIRMED together with the success rate NOT confirmed and the carried gross edge near 0. Any one alone does not carry the answer.
- The awk bounds matter: the same row text also appears in output sections 6 and 7 (a first draft of this handle matched those too).

### H-2 — the full run is deterministic

```
python tools/ops/q1d_tier_geometry.py --root . --out backtest_data/q1d-tier-geometry/identity-q1d.md > /dev/null 2>&1; diff <(grep -v "^- Run at (UTC)" docs/q1d-tier-geometry-2026-09-24-output.md) <(grep -v "^- Run at (UTC)" backtest_data/q1d-tier-geometry/identity-q1d.md) > /dev/null && echo "Q1D OUTPUT IDENTICAL (apart from the run timestamp line)" || echo "Q1D OUTPUT DIFFERS"
```

```
Q1D OUTPUT IDENTICAL (apart from the run timestamp line)
```

- About 7 minutes.

### H-3 — the self-checks

```
awk '/^## 0. Self-checks/,/^\| Segment/' docs/q1d-tier-geometry-2026-09-24-output.md
```

```
## 0. Self-checks

| Check | Result |
|---|---|
| Rows in --rows | 10520 |
| P0 rows (in --prior-rows) / prior export rows (must be equal) | 8810 / 8810 |
| P0 rows whose fields differ from the prior export, Half excluded (must be 0) | 0 |
| H3 rows (from 2026-09-14) | 1710 |
| Rows with no log row for the stop label (must be 0) | 0 |
| Stop label reproduces the logged placed stop (must be all) | 10520 of 10520 |
| Target-hit rows where net EV = T - fee (must be all) | 4437 of 4437 |
| Stop-hit and same-bar rows where net EV = -S - fee (must be all) | 5279 of 5279 |
| Largest absolute residual of the net EV identity over session x tier cells, bps (must be ~0) | 2.66e-15 |
| Tier = tier of the share (STRONG iff share >= 0.70, MEDIUM iff 0.53 <= share < 0.70), mismatches (must be 0) | 0 |
| POC-fix slice rows in P0 (a superset of the 143 moved population rows) | 176 |

| Segment | Session | Rows | STRONG | MEDIUM | WEAK | Trading days | H1 rows | H2 rows |
```

- **Load-bearing values:** 0 differing P0 rows (so this read and the prior read describe the same trades) and the stop label 10,520 of 10,520 (so the stop-label mix is the engine's own).

### H-4 — the POC-fix sensitivity (NY)

```
awk '/^## 9\./,/^## 10\./' docs/q1d-tier-geometry-2026-09-24-output.md | grep -E "^\| NY \| 5 / 24 / 115 \| STRONG - WEAK"
```

```
| NY | 5 / 24 / 115 | STRONG - WEAK | mean target distance, bps | +1.2 | +1.1 [-0.5, +2.8] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | STRONG - WEAK | gross breakeven rate, pp | +2.4 | +2.4 [+1.3, +3.4] | CONFIRMED (d > 0) |
| NY | 5 / 24 / 115 | STRONG - WEAK | success rate, pp | +5.8 | +5.3 [-0.6, +10.5] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | STRONG - WEAK | gross edge, pp | +3.4 | +2.9 [-3.0, +8.2] | NO DIFFERENCE SHOWN |
| NY | 5 / 24 / 115 | STRONG - WEAK | net EV per trade, bps | -0.0 | -0.2 [-2.0, +1.6] | NO DIFFERENCE SHOWN |
```

### H-5 — no engine, settings, UI or harness file changed

```
git show --stat --format= 5c91cca -- Core UI analysis settings.json DeribitWsFeed.vb AnalysisLogger.vb DynamicNorms.vb MarketState.vb verify tools/ops/SwingFallbackRead; echo "(end)"
```

```
(end)
```

- `5c91cca` is this read's commit. It is pinned by hash, not `HEAD~1`: another seat committed `b1cfb1f` and `33d5ee1` between this seat's base `48e367a` and `5c91cca`.

### H-6 — the extended export regenerates byte-identical

```
dotnet tools/ops/SwingFallbackRead/bin/Release/net8.0/SwingFallbackRead.dll --root . --mode diagexport --fetch aws_fetch/20260924-084613 --pooled AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv --cache backtest_data/q1d-tier-geometry --out backtest_data/q1d-tier-geometry/identity-rows.csv > /dev/null; md5sum backtest_data/q1d-tier-geometry/identity-rows.csv backtest_data/q1d-tier-geometry/diagnosis-rows-20260924.csv
```

```
219dac4a5ecbb065554f4e4d61f0421d *backtest_data/q1d-tier-geometry/identity-rows.csv
219dac4a5ecbb065554f4e4d61f0421d *backtest_data/q1d-tier-geometry/diagnosis-rows-20260924.csv
```

- ⚠ **Reproducible only with this seat's cache.** The week-of-2026-09-21 candle file was fetched mid-week. A reader who fetches it later gets more bars, which changes the carried-24 h walk for the last H3 rows only. The main-window outcomes and P0 do not depend on it.

### H-7 — Kelly's assumption against the book (NY)

```
awk '/^## 11\./,/^## 12\./' docs/q1d-tier-geometry-2026-09-24-output.md | grep -E "^\| NY \|"
```

```
| NY | STRONG | 0.65 | 1.094 | +0.330 | 0.459 | 1.081 | -0.041 |
| NY | MEDIUM | 0.55 | 1.094 | +0.139 | 0.399 | 1.142 | -0.128 |
| NY | WEAK | 0.45 | 1.094 | -0.053 | 0.401 | 1.188 | -0.103 |
```

- Columns: Kelly p, Kelly b, Kelly f*, measured success rate, measured payoff ratio, f* at the measured p and b.

### E-1 — the pilot runs that shaped the extra cuts

- Four 200-resample pilot runs preceded the 10,000-resample run. After pilot 1 I added: the "why the swing tier did not place" table (output section 2.1), the within-target-type comparisons (section 6.1), the timeout share in the geometry control, and labelled carried-24 h comparisons (section 10). The pilots were scratch files and are not committed. Every added cut is reported in full; none replaced a cut that was already there.

---

## 2. Decisions taken (auto-proceed, one line each)

- **File date 2026-09-24, not the brief's 2026-09-25:** `date -u` read 2026-09-24 17:48; project dates are UTC; the brief's date is the GMT+8 date. Undone by one rename.
- **P0 is labelled; H3 is reported apart:** options (a) pool H3 into P0 and re-split halves, (b) keep P0 and its halves unchanged, add H3 as a forward segment → **(b)**. The prior read's labels stay comparable, and H3 becomes a forward check away from the v66 edge. Step 3 of the three-step test in `CLAUDE.md` does not arise: (b) records more than (a) (two readings instead of one).
- **Era edge handling:** keep the prior halves (label comparability) and add era segments E1 / E2 / E3 / H3 with no halves → the era table shows whether a finding holds inside every era. Records more than either alone.
- **Stop label derived, not logged:** the CSV has no stop-reason column. Derived by the shipped DG1 rule from `SwingStop*`, `Price`, `ATR`; checked against the logged placed stop on every row (10,520 of 10,520).
- **POC-fix slice as a superset (176 rows) from the verified VPFR label:** the exact 143 rows need the POC price, which no per-row file carries. A superset removes every moved row; the sensitivity is therefore conservative.
- **New instrument committed under `tools/ops/`, not the scratchpad:** handles must be runnable. Host-agnostic Python standard library, file reads only.
- **Separate cache `backtest_data/q1d-tier-geometry/`:** the shared cache would keep a partial-week candle file (the read §9 warns).
- **Geometry control uses two stratifications (R geometry × bps size, and target type):** they disagree on NY STRONG, and reporting both is more truthful than picking the one that reads cleaner.
- **No `docs/trader-tick-queue.md` edit:** the orchestrator owns it (row changes in section 4).

---

## 3. Decisions queued

✅ **RULED 2026-09-24 (UTC), trader, as recommended:** `QD-1` = record (a) AND adopt (c): tiers are not re-cut, and Kelly sizing treats all tiers as one class. `QD-2` = (a): p = the measured pooled success rate, so the Kelly block is silent on every signal until the book supports an edge. `QD-3` = (a), orchestrator: add the NY STRONG fallback-target comparison to the pre-registered vote-value study when it is specced. **Not built yet:** the (c) change is a rendered-value build (Kelly display, and the payload's advisory `kelly` block reads 0) and needs its own small spec; consider settling it with the parked `K-1`–`K-4` payoff proposal in one pass. Kelly has zero scoring impact (checked 2026-09-24: no `Kelly` reference in the scoring files; the payload comment reads *"Advisory context only — never sizing in v1"*).

### QD-1 — `Q-1` (a) against (c) (trader; ⚠ reserved: (c) moves a rendered value)

| Option | What changes | Measured basis (the read §1, §6) |
|---|---|---|
| (a) `F-1` only: record that thresholds and the tier floor are not re-cut | Nothing | A tier step trades success rate for payoff: NY STRONG − WEAK success +5.8 pp (NO DIFFERENCE SHOWN) against payoff ratio −0.11 and gross breakeven +2.4 pp (both CONFIRMED); net EV −0.0 bps |
| (c) `F-4a`: one class for Kelly sizing only | Kelly's win-probability lookup, so the Kelly display and the payload's advisory `kelly` block | Kelly EST assumes p = 0.65 / 0.55 / 0.45 at a fixed b = 1.094. Measured NY success 0.459 / 0.399 / 0.401 and payoff ratio 1.08 / 1.14 / 1.19. f* at the measured p and b ≤ 0 in every readable cell but LONDON MEDIUM (+0.011) |

- **Read: record (a) and adopt (c).** They are not exclusive. (c) removes a rendered claim the book contradicts; (a) alone keeps it and is the cheaper option, so I do not recommend (a) alone.
- **What would change my read:** a CONFIRMED success or net EV gap at STRONG inside one target type. The nearest thing is NY STRONG − WEAK on ATR fallback targets, success +10.2 [+1.2, +18.4] pp, NO DIFFERENCE SHOWN (neither half significant alone), net EV of that STRONG cell −1.8 bps.

### QD-2 — the single-class p value inside (c) (trader; ⚠ reserved: rendered value)

| Option | Kelly display result |
|---|---|
| (a) p = the measured pooled success rate (0.40–0.48 in every readable cell) | f* ≤ 0 at b = 1.094 (Kelly breakeven p = 0.478): the Kelly block is silent on every signal |
| (b) p = `est_prob_floor` 0.45 | Same: silent on every signal (f* −0.053) |
| (c) p = 0.55 (today's MEDIUM value) | Positive on every signal and at the 5 % cap (half-Kelly 0.069), including WEAK |

- **Read (hypothesis): (a).** It is the only value this book supports, and it is self-describing. (c) would switch Kelly on for WEAK signals, which today are silent. Today STRONG and MEDIUM both already sit at the 5 % cap, so the tier ladder's practical effect on the display is only "WEAK silent, the rest capped".
- Shares a root with the parked Kelly placed-payoff proposal (`docs/kelly-placed-payoff-proposal.md`, decisions `K-1` to `K-4`): that proposal replaces b. One pass could settle p and b together.

### QD-3 — the NY STRONG fallback-target hint (orchestrator; not reserved: measurement only)

- **Options:** (a) add "NY STRONG − WEAK on ATR fallback targets" to the pre-registered vote-value study that the `F-2` ruling requires; (b) drop it.
- **Read: (a).** It is the only cell where a tier shows a success lead at the same geometry in FULL, H1, H2 and H3 point estimates. It costs one pre-registered comparison, and a forward replication decides it.

---

## 4. Rows for `docs/trader-tick-queue.md` (for the orchestrator; I did not edit it)

- The MEDIUM-tier row (the one that names `Q-1`): add "✅ `Q-1` (d) DONE 2026-09-24 (`5c91cca`): the top-tier success-rate rise is a payoff shift plus fewer timeouts; no tier outcome gap CONFIRMED. Read: `docs/q1d-tier-geometry-read-2026-09-24.md`. Next: trader rules `Q-1` (a) and (c), and the single-class p value (spec-back `QD-2`)."
- The same row's `F-4a` note ("gated on `Q-1` (d)"): the gate is discharged; the decision is now the trader's.
- The shipped-state-sweep row names `Q-1` (d) as a predecessor; that predecessor is done.

---

## 5. Feedback on the brief

### What worked

- **"Measure the opposite direction too."** In bps the nearer-target hypothesis fails in NY; in R it holds. Without the two-sided framing the NY bps null would have read as "hypothesis rejected".
- **"Include rotated `.bak` books."** Checked: the `.bak` is wholly inside the pooled book (0 missing timestamps), so the pooled count is complete.
- **Naming the POC-fix slice and the collector hole up front.** Both became explicit sensitivity items instead of footnotes.

### What broke or was incomplete

- ⚠ **Highest value: the brief asked for stop labels (SWING_STOP / STOP_CLAMPED / FALLBACK_ATR), but `analysis_log.csv` does not log them.** They had to be derived. It worked (10,520 of 10,520), but a logged `StopReason` column would make the next read cheaper. That is a CSV-header change, so it would be a rider (`docs/csv-rotation-riders.md`); I did not add one.
- **"Target distance" in bps and in R answer different questions.** The brief's hypothesis ("nearer targets") is true in R and false in bps for NY. The next brief should name the unit.
- **The brief's filename date was the GMT+8 date** (2026-09-25 against UTC 2026-09-24).
- **"Which target tier placed (swing / HVN / POC / ATR fallback)":** POC placed on 0 rows in every tier (the pre-fix gate), so that column is empty until the fix deploys.

---

## 6. What I did not verify

- Full list: the read §8. The items that change a decision:
  - **Whether NY STRONG carries direction on fallback targets:** single-half strength only (`QD-3`).
  - **ASIA STRONG** (the only positive-EV cell): NOT READABLE, n = 65; H3 (n = 21) does not repeat it.
  - **The mechanism links:** "no swing target ← extension at entry" and "fewer timeouts ← realised volatility" are readings, not row-level measurements.
  - **The POC-fix slice in H3:** no VPFR attribution for those rows.
- **Carried over without checking:** the swing read's candle walk and funnel; the 143 and 1,288 POC-fix counts; the 2026-09-21 deploy facts from `docs/aws-collector-deploy-checklist.md`.
