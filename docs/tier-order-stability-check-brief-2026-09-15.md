# Brief — tier-order stability check (Part A): WEAK vs MEDIUM vs STRONG, by target type

**Written:** 2026-09-15 (UTC) by the orchestrator seat. **For:** a single-task analysis seat. **Asked by:** the trader, following [`swing-vs-fallback-target-read-2026-09-15.md`](swing-vs-fallback-target-read-2026-09-15.md). Vocabulary: `docs/DeribitIndicatorProject.md` §5a (success rate · gross/net breakeven rate · gross/net edge · net EV per trade). **Use those words exactly.**

**This is Part A of two.** Part A checks whether the tier ordering seen in the swing read is stable inside the data we already have. **Part B**, a forward test on new data with the comparisons locked in advance, is written only after Part A reports. **Do not start Part B.**

---

## 0. Model, effort, escalation — read first

| Item | Detail |
|---|---|
| **Model / effort** | **Opus, medium.** A re-cut, not a new walk: `tools/ops/SwingFallbackRead` already produces per-signal outcomes from 1-minute candles for all 8,810 signals. The judgment is in keeping the test honest, which §2 fixes in advance |
| **Where it will slip** | (1) Splitting the sample **randomly** instead of **chronologically**: neighbouring signals are correlated, so a random split makes both halves agree by construction. (2) Changing a comparison, a threshold or the split after seeing a half. (3) Reading a cell below the §2.4 readability floor. (4) A CI that treats signals as independent. It must cluster by trading day (§2.3). (5) Pooling sessions or using the ALL tier |
| **Escalate / stop** | Stop and report if the per-signal outcomes from the swing read cannot be reproduced (net EV per trade must match `swing-vs-fallback-target-read-2026-09-15.md` §4.1 to within 0.1 bps before any split). Stop and ask if any result would justify a scoring, tier or `settings.json` change: D-table only, never applied |
| **Scope** | Trader-directed scoped seat. Skip the full `CLAUDE.md` session-start reads. Read §5a of `DeribitIndicatorProject.md`, the swing read in full, and this brief. Run `date -u` first |

## 1. The question

The swing read's per-tier numbers (derived by the orchestrator from its §1 and §4.1 tables, main window, net EV per trade in bps):

| Session / target | STRONG | MEDIUM | WEAK |
|---|---|---|---|
| NY fallback | −1.8 (n 259) | −4.4 (n 939) | −3.8 (n 1,654) |
| NY swing | −9.2 (n 59) | −4.4 (n 488) | −2.7 (n 1,211) |
| ASIA fallback | +2.6 (n 51) | −3.5 (n 251) | −2.3 (n 432) |
| ASIA swing | +6.5 (n 12) | −3.8 (n 141) | −1.5 (n 693) |

MEDIUM is the lowest or joint-lowest tier in every row, and WEAK beats MEDIUM in all four. **None of these gaps is significant.** **Is the ordering stable across time and regime, or is it one noisy sample?**

## 2. Pre-registered design — fixed now, not after looking

### 2.1 Population and outcomes (same as the swing read)

- **Trading-week signals only:** Monday ASIA session open to Friday NY session close, hours from `session_volume.sessions` in `settings.json` (trader-ruled 2026-09-14).
- Directional verdicts with placed levels. **Sessions separate:** NY, LONDON, ASIA.
- **Target type:** `TargetCapReason` = `none` (ATR fallback) and `swing`. Report HVN rows only as context, never as a test.
- **Tiers:** STRONG, MEDIUM, WEAK, each on its own. Never the ALL tier, never STRONG + MEDIUM combined.
- **Primary outcome: main window** (NY 15 min, LONDON and ASIA 45 min). Secondary: carried to conclusion with the 24 h / Friday-NY-close cap.
- **Fees:** maker/maker from `scoring.trade_costs`. ⛔ **No slippage case** (trader-ruled 2026-09-15: day-to-day slippage variance makes an average misleading).

### 2.2 Splits

| Split | Rule |
|---|---|
| **Chronological halves** | Order the trading days of the whole sample. **H1 is the first half of trading days and H2 is the second.** One split date for every cell; state it |
| **Regime** | **R1** = signals before 2026-08-20 00:00 UTC, **R2** = on or after it (the ATR step found by `d3-asia-burst-watch-read-2026-09-14.md`) |

### 2.3 Statistic

- **Net EV per trade** per cell, and the **difference** for each comparison, with a 95 % CI from a **bootstrap that resamples whole trading days** (2,000 resamples minimum). State the method in the read.
- Success rate and gross edge per cell, as secondary columns.

### 2.4 Readability floor

A cell is **readable** in a sub-sample only if it holds **n ≥ 100**. A comparison is readable only if both of its cells are. Mark every unreadable comparison `NOT READABLE` and do not interpret it. ⚠ STRONG swing will be unreadable almost everywhere; say so and move on.

### 2.5 The comparisons (per session × target type)

| ID | Comparison | Difference reported as |
|---|---|---|
| **C1** | WEAK vs MEDIUM | EV(WEAK) − EV(MEDIUM) |
| **C2** | STRONG vs MEDIUM | EV(STRONG) − EV(MEDIUM) |
| **C3** | WEAK vs STRONG | EV(WEAK) − EV(STRONG) |

### 2.6 Stability rule — decided now

For each comparison, look at every readable sub-sample among the four: H1, H2, R1, R2.

| Label | Rule |
|---|---|
| **STABLE** | At least three readable sub-samples, **including both H1 and H2**, and the difference has the **same sign in every readable sub-sample** |
| **UNSTABLE** | The sign flips in any readable sub-sample |
| **INCONCLUSIVE** | Fewer than three readable sub-samples, or H1 or H2 unreadable |

Also report, alongside the label (not as part of it), whether the full-sample difference's CI excludes zero.

⛔ **Do not change §2 after seeing any result.** If a rule proves mechanically impossible (for example a sub-sample with no signals), record the problem and your replacement rule in the read **before** re-running, and flag it in the report.

## 3. Inputs

| Input | Use |
|---|---|
| `docs/swing-vs-fallback-target-read-2026-09-15.md` and its `-output.md` | The population, method and numbers to reproduce |
| `tools/ops/SwingFallbackRead/` (committed at `989b89c`) | Extend with a stability mode, or add a sibling. Keep it re-runnable |
| `AWS-copybacks/pooled-book-2026-09-09/analysis_log_pooled.csv` and `aws_fetch/20260913-153704/` | The same data the swing read used (gitignored; in the main checkout) |
| 1-minute candles | Reuse whatever the swing read cached or fetched. Deribit public GETs are allowed, read-only |

## 4. Deliverable

1. `docs/tier-order-stability-read-2026-09-XX.md`, in this order:
   - **Verdict first:** a table of C1–C3 × session × target type, each labelled STABLE, UNSTABLE, INCONCLUSIVE or NOT READABLE.
   - The reproduction check against the swing read.
   - The split date and the CI method.
   - Per-sub-sample tables: n, net EV per trade with CI, and the difference with CI.
   - **The candidates for Part B:** the STABLE comparisons, with the forward sample each would need to reach a significant difference, estimated from this data.
   - What was not verified.
2. The instrument change, committed, with its real output pasted (or in a sibling `-output.md`).
3. Commit locally with `[no-engine-change]`. Do not push. Stage only your own files; other sessions commit to this repo.
4. Output format: point form and tables; no bare section numbers without the document name; no bare IDs without source and meaning; short active sentences keeping domain terms; a section separating verified from not verified.

## 5. Report back

At most 10 lines: whether the reproduction matched; the STABLE comparisons; the UNSTABLE ones; the Part B candidates with their estimated forward sample; commit hash; anything needing a trader ruling.
