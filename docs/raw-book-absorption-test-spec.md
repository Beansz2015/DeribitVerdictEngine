# Raw order-book absorption test — does the 100 ms book feed mis-measure `pullFrac`?

**Status:** session 1 done 2026-10-05 (UTC): spec, probe, smoke run. The full run is sized above 6 hours, so it goes to the temporary AWS instance (trader direction 2026-10-05, relayed by the orchestrator). **Session 2 = the read.** Start commit `a7a9cf0`.

⛔ **Session 2, 2026-10-06 (UTC): NO VERDICT.** The run ended with 29 "prior" book mismatches. The diagnosis (`raw-book-absorption-test-spec.md` §11) cannot show any of the 29 to be a fallback-comparison artefact, because the probe recorded counters only, not the mismatching books. The trader's 2026-10-06 ruling therefore stops the read. The registered read was NOT run. Results: [`raw-book-absorption-test-results-2026-10-06.md`](raw-book-absorption-test-results-2026-10-06.md).

✅ **Session 3, 2026-10-06 (UTC): READ RUN under the quarantine ruling (option (d), `raw-book-absorption-test-spec.md` §0 box). Verdict: H-NET CONFIRMED, H-POST REFUTED.** 1,418 A-B pairs after the quarantine dropped 25; 723 qualifying. Output and checks: `raw-book-absorption-test-spec.md` §12.

**Source of the test:** the surviving half of the "REFUTED ON DIRECTION 2026-08-20" row — [`trader-tick-queue-archive.md` §C, `trim-2026-09-14-47`](trader-tick-queue-archive.md#trim-2026-09-14-47) and the live row in `docs/trader-tick-queue.md` §2. **Key scope:** the read-only key, `HH-2` (the read-only-key ruling, widened 2026-09-29) in [`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) §4.

**Class:** read-only research. No `settings.json` change, no scoring change, no collector contact, no `Core/` or `UI/` edit.

---

## 0. Session-2 brief — the read

> ✅ **RULED 2026-10-06 (trader), superseding the all-or-nothing bar in the box below: option (d) QUARANTINE, read today.**
> - **Correction first:** the bar "a verdict only if all mismatches are shown to be artefacts" was the orchestrator's wording, carried from this spec's escalation trigger; the trader had ruled only "let the run finish and diagnose before any verdict".
> - **Rule, fixed before any `pullFrac` value is read:** drop every A–B episode PAIR in which EITHER arm's episode (`open_ms`–`close_ms`) overlaps any of the 28 quarantine windows in [`audits/proofs/raw-book-quarantine-2026-10-06/mismatch_windows.csv`](audits/proofs/raw-book-quarantine-2026-10-06/mismatch_windows.csv) (the one-minute status windows holding all 29 prior mismatches). Run the registered read (`RBA-3` pairing, `RBA-4` thresholds) once on the remaining pairs. Report the dropped count per arm and per session.
> - **Why this is safe:** 0 of 156,319 exact checks mismatched, so no rebuild error persisted; the mismatches can only touch episodes inside those windows (measured exposure, timestamps only: arm A 89 of 3,565 episodes, arm B 164 of 6,874, ≈ 2.5 %).
> - **Still owed for any future run:** the reconnect-reset fix and per-mismatch logging (this spec's `raw-book-absorption-test-spec.md` §11.4 and §11.6 (a)).


> ⚠ **ESCALATION TRIGGER FIRED DURING THE RUN — RULED 2026-10-06 (trader): let the run finish, and diagnose before any verdict.**
> - At 19.3 h the status read `agree 123633/0 prior 85237/20`: **0 exact mismatches, 20 "prior" mismatches**. 15 of the 20 fell in the first 4 h (2026-10-05 13:20 → 17:20 UTC, the LONDON/NY overlap); 5 in the next 15 h. Not aligned with the one reconnect (15:29 UTC).
> - **Session 2 starts with the diagnosis, not the read.** For each prior mismatch, decide: a timing artefact of the "prior" fallback comparison (no raw message carries the snapshot's `change_id`), or a rebuild error. Show the evidence per mismatch.
> - **A verdict is allowed only if all 20 are shown to be fallback-comparison artefacts.** Any rebuild error, or any mismatch left unexplained, means no verdict: report and stop.
> - Orchestrator's hypothesis, NOT verified: timing artefacts, because exact comparisons never mismatched.
> - ⛔ **Outcome, 2026-10-06 (session 2):** the final count was 29, not 20. None of the 29 could be shown to be an artefact, so the read stopped with no verdict. See `raw-book-absorption-test-spec.md` §11.

**Model: Opus 5.5 · Effort: HIGH.**

- **Why that tier.** The read rule below is mechanical, but two judgments are not: (1) whether the aligned-pair population is representative (the raw arm can fragment episodes, and the aligned set can then select the stable ones); (2) which of the three explanations a result supports (netting, trade-timing misalignment, mask effects). Arm C exists to separate the second; reading it needs care.
- **Where you will slip.**
  - ⛔ **Reading the direction from the brief, not from the arithmetic.** The brief that commissioned this test stated *"raw `postLB` larger and raw `pullFrac` smaller"*. **The arithmetic in `raw-book-absorption-test-spec.md` §2 says the second half is wrong for every episode with `pullFrac` ≤ 1.** This repo has inverted this exact direction once already (the archive row above). Read §2 before the data.
  - **Trusting the probe's end summary.** It is a convenience. Re-derive every number from `episodes_*.csv` with your own script. The summary and the status line pair episodes with the overlap rule of `raw-book-absorption-test-spec.md` §5; your script must implement that rule independently and say so.
  - **Pooling `PROBE_*` rows.** Rows whose `reason` starts with `PROBE_` were cut by a probe reset (connect, raw gap, end). Exclude them. Match only within the same `gen`.
  - **Comparing `pullFrac` across the floor without saying so.** `pullFrac = pullLB / max(postLB, 5000)`. Report floored (`postLB` < 5000) and unfloored pairs separately as well as pooled.
- **Escalation trigger.** STOP and report, without a verdict, if any of these holds: book agreement shows any mismatch (`mismatch` or `prior-mismatch` > 0 in the summary or the status lines); aligned pairs are fewer than half of arm A's informative episodes; the run's `gen` count exceeds 20 (too many resets to trust episode spans).

---

## 1. Question and why it matters

| Item | Value |
|---|---|
| Question | Does the engine's 100 ms grouped book feed change the D8 pull accounting (`pullLB`, `postLB`, `pullFrac`) against the raw book feed, on the same episodes? |
| Why | The pull veto (`max_pull_frac` 0.75) is the dominant killer of absorption candidates (2026-08-20 measurement, archive row `trim-2026-09-14b-14`). If `pullFrac` is a feed artefact, tuning the veto is the wrong move. `docs/absorption-mechanism-revision-proposal.md` decision `D-3` kept the veto and asked for instrumentation instead |
| Gate it feeds | The mechanism-revision build stays gated on this answer (archive row `trim-2026-09-14-47`) |
| Ideal timing | Before the absorption Stage 1 read (~2026-10-08) |

**Live context, measured 2026-10-05 from the box's `aws_fetch/20261005-112601/analysis_log.csv` (1,775 active rows with the D8 columns, 2026-09-24 → 2026-10-05):**

| Statistic | Value |
|---|---|
| `pullFrac` > 0.75 (vetoed) | 46.8 % (831) |
| `postLB` < 5,000 (floored) | 46.5 % (825) |
| `pullFrac` = 0 | 35.6 % (632) |
| `pullFrac` exactly 1.000 | 1.7 % (31) |
| `pullLB` = `postLB` > 0 (net-zero episode) | 2.3 % (41) — the 31 above plus 10 floored rows |
| Medians `pullLB` / `postLB` / `pullFrac` | 10,000 / 7,200 / 0.631 |

⭐ **The 1.000 point mass is net-zero episodes with `postLB` ≥ 5,000, not the floor.** Every exactly-1.000 row has `pullLB` = `postLB`; the 10 extra net-zero rows sit under the floor and read `pullLB`/5000 < 1. This matters for §2.

---

## 2. Hypothesis — and its DIRECTION, checked by arithmetic

**The fold (`Core/LevelAbsorptionTracker.vb`, `FoldBookSide`).** For each interval between two consecutive book snapshots it computes `net = ΔSize + Fills` (= Posts − Pulls), then `pullLB += max(0, −net)` and `postLB += max(0, net)`. Only the NET of each interval is credited.

**What a finer feed does.** Split one 100 ms interval into raw sub-intervals with nets `n₁ … nₖ`. If the visibility mask and the fill attribution are the same, ΔSize telescopes, so `Σ nᵢ = net₁₀₀`. Then:

- `Σ max(0, nᵢ) ≥ max(0, Σ nᵢ)` and `Σ max(0, −nᵢ) ≥ max(0, −Σ nᵢ)` (subadditivity).
- `postLB − pullLB` is the episode's total net. It is **the same** on both feeds.
- So the raw feed adds **the same δ ≥ 0 to BOTH accumulators**: `pullLB_raw = pullLB_100 + δ`, `postLB_raw = postLB_100 + δ`.

**Consequence for `pullFrac` (floor F = 5,000; P = `pullLB_100`, Q = `postLB_100`):**

| Case on the 100 ms feed | 100 ms `pullFrac` | Raw `pullFrac` | Direction |
|---|---|---|---|
| Unfloored (Q ≥ F), P < Q | P/Q < 1 | (P+δ)/(Q+δ) | **UP**, toward 1 |
| Unfloored, P = Q (the 1.000 point mass) | 1.000 | (P+δ)/(Q+δ) = 1.000 | **unchanged** |
| Unfloored, P > Q | > 1 | (P+δ)/(Q+δ) | down toward 1, **stays > 1** |
| Floored (Q < F), stays floored | P/F | (P+δ)/F | **UP** |
| Floored, leaves the floor (Q+δ ≥ F) | P/F | (P+δ)/(Q+δ) | **UP when P ≤ F** (sign of P(F−Q) + δ(F−P) ≥ 0); either way when P > F |

⛔ **So the brief's stated direction — "raw `postLB` LARGER and raw `pullFrac` SMALLER" — is half right.** `postLB` rises, but so does `pullLB`, by the same amount. **For every episode with 100 ms `pullFrac` ≤ 1, the raw `pullFrac` is HIGHER, not lower.** Episodes above 1 fall toward 1 but stay above the 0.75 veto.

⭐ **Under pure netting the raw feed can only flip the veto from PASS to VETO, never from VETO to PASS.** The 1.000 point mass is partition-invariant: a finer feed cannot remove it.

**Two hypotheses the run separates:**

| ID | Hypothesis | Predicts on aligned pairs |
|---|---|---|
| **H-NET** (this spec) | Within-interval netting hides gross posts AND pulls equally | Δ`pullLB` ≈ Δ`postLB` ≥ 0 · raw `pullFrac` ≥ 100 ms `pullFrac` where 100 ms ≤ 1 · veto flips are PASS→VETO · 1.000 rows stay 1.000 |
| **H-POST** (the archive row's) | Within-bucket cancels hide POSTS, so the floor inflates `pullFrac` | Δ`postLB` > Δ`pullLB` · raw `pullFrac` < 100 ms · veto flips are VETO→PASS · the 1.000 point mass shrinks |

**What could break H-NET's identity** (so the data, not the arithmetic, decides):

- **Trade-timing misalignment.** At raw resolution a fill can land one sub-interval before or after its book change. That writes a spurious −X/+X pair: it adds to both accumulators, so it pushes the same way as H-NET. **Arm C** (raw book + 100 ms trades) against **arm B** (raw book + raw trades) sizes this.
- **Mask differences.** The fold masks each interval to the band portion visible in both snapshots. Finer snapshots change which levels are masked when the top-10 window shifts. This can be asymmetric. It is the residual the run measures.
- **Episode fragmentation.** The raw arm sees transient top-10 shifts the 100 ms feed never shows, so it can close episodes more often (`LadderSpanLost`). Shorter episodes carry smaller sums. §5 handles this with aligned pairs plus two secondaries.

---

## 3. Design — three arms, one process

| Arm | Book | Trades | Role |
|---|---|---|---|
| **A** | `book.BTC-PERPETUAL.none.10.100ms` | `trades.BTC-PERPETUAL.100ms` | The engine's pair (`DeribitWsFeed.vb` line 27 and the trades line beside it) |
| **B** | Top 10 of the book rebuilt from `book.BTC-PERPETUAL.raw` | `trades.BTC-PERPETUAL.raw` | Raw granularity on both feeds |
| **C** | The same rebuilt top 10 | `trades.BTC-PERPETUAL.100ms` | Diagnostic: book granularity alone |

- **The fold is the shipped code.** Each arm is a separate instance of `Core/LevelAbsorptionTracker.vb`, linked by `tools/RawBookProbe/RawBookProbe.vbproj` (no copy, no edit). Book folds are stamped with receive time and trade folds with the venue stamp, as `DeribitWsFeed.vb` does.
- **Same levels, same instant.** Every minute (2 s after the bar close) the probe fetches REST candles and runs the engine's `CalcSwingPivots` (5 m), `CalcVPFRLite` and `CalcATR` (execution resolution) with the tracked `settings.json` (v69). Then it resolves proximity, band and break tolerance as ATR × the absorption fractions. This is the carry at the `SetAbsorptionLevels` call in `UI/MainForm_Analysis.vb`. That carry is three multiplications and is restated in the probe, because `UI/` cannot be linked. All three arms get the same `SetLevels` call under one lock.
- **One connection, one fold thread, one lock.** Messages fold in arrival order. A reconnect or a broken raw change chain resets ALL arms together (as the engine resets its tracker on reconnect), bumps `gen`, and re-applies the last levels.
- ⚠ **The channel the brief named does not exist.** A subscribe to `book.BTC-PERPETUAL.none.10.raw` returns `result: []` with no error, and nothing arrives (measured 2026-10-05, read-only key). The raw book is incremental only, so the probe rebuilds the full ladder from `book.BTC-PERPETUAL.raw` (snapshot, then changes chained by `prev_change_id`) and hands the tracker its top 10. The probe treats a subscribe result that does not echo the channel as a refusal and stops (exit 4).
- **The rebuild is checked, not trusted.** Each 100 ms snapshot carries a `change_id`. The probe compares its top 10 with the rebuilt top 10 at that `change_id` ("exact"), or at the latest raw change before it when no raw message carries that id ("prior"). Every status line prints `agree exact/mismatch prior match/mismatch`.

**Outputs** (all in the run folder): `episodes_*.csv` (one row per closed episode per arm, with the last active read), `samples_*.csv` (all three arms read at the same instant every second, the shape the engine logs), `levels_*.csv`, `events_*.log`, `status_latest.txt`, `summary_*.txt`, `console.log`.

**Caps:** one WS message 16 MB · rebuilt-book check ring 8,192 entries · pending 100 ms checks 512 · in-memory episode records 300,000 (summary only; the CSV is the record) · files rotate at 64 MB · total output 2 GB · GC heap stop at 160 MB (`--heap-stop-mb`), with `DOTNET_GCHeapHardLimit` 256 MB and systemd `MemoryMax` 400 MB on the cloud instance.

---

## 4. Run length — from the measured episode rate

**Box rate** (`aws_fetch/20261005-112601/absorption_episodes.log`, 2026-09-24 → 2026-10-05, non-Reset closes):

| Span | Covered hours | Episodes | Per hour | Mean life |
|---|---:|---:|---:|---:|
| last 1 day | 24.0 | 3,003 | 124.9 | 5.0 s |
| last 3 days | 72.0 | 7,201 | 99.9 | 6.4 s |
| last 7 days | 166.5 | 23,763 | 142.7 | 4.3 s |

By UTC hour (7 days): 64/h at 09:00 is the floor; 13:00–16:00 runs 209–319/h. `LadderSpanLost` is 43 % of closes at a 2.8 s mean life.

**Smoke-run rate (dev machine, 10 min, 2026-10-05):** see §7. The informative and aligned fractions come from it.

**Minimum count, pre-stated:** **200 aligned informative pairs (A vs B) with 100 ms `pullFrac` ≤ 1.** A two-sided sign test at n = 200 detects a 60/40 split at p ≈ 0.005.

**Run length: 24 h (86,400 s), on the temporary AWS instance.** The minimum count alone needs less (sizing in `raw-book-absorption-test-spec.md` §7). 24 h is chosen because it covers ASIA, LONDON and NY once each, and book dynamics differ by session. Started ~15:00 UTC 2026-10-05, it ends ~15:00 UTC 2026-10-06, before the absorption Stage 1 read (~2026-10-08). The output uploads to S3 every 30 min, so an early stop still leaves a readable folder. If the minimum count is not reached, the read is INCONCLUSIVE-BY-COUNT and a second run is the remedy, not a looser rule.

---

## 5. Pre-registered read rule

**Population.**

- Arm A **informative** episode: `reason` not `PROBE_*`, `episode_sec` ≥ 1.0, `book_folds` ≥ 2 (at least two conservation intervals). ⚠ The 100 ms grouped feed sends only when the top 10 changes — about 3 snapshots per second in the smoke run, not 10 — so a 1 s arm A episode often has only 2–3 folds.
- **Aligned pair (primary):** an arm A informative episode and the ONLY arm B episode with the same `side`, `gen` and `level` that overlaps its span, provided that episode opens within 500 ms and closes within 500 ms of it. The overlap rule stops a raw FRAGMENT from pairing with a whole 100 ms episode (seen in the smoke run: arm B split one arm A episode at a transient `LadderSpanLost`).
- **Secondary S-CONCAT:** for each arm A informative episode, sum `pull_lb` and `post_lb` over every arm B episode on the same side, gen and level that overlaps it; recompute `pullFrac` with the floor. This keeps fragmented episodes.
- **Secondary S-INSTANT:** `samples_*.csv` rows where arms A and B are both active on the same side and level, and their `episode_sec` differ by ≤ 0.5 s. One row per arm A episode (the last such row), to avoid autocorrelation.

**Statistics (on aligned pairs, pooled and split floored / unfloored by arm A `post_lb` < 5,000):**

| ID | Statistic |
|---|---|
| `S1` | Among pairs with arm A `pullFrac` ≤ 1: counts of B higher / lower / equal; two-sided sign test on higher vs lower |
| `S2` | Veto flips at `max_pull_frac` (read from the run's settings): n(PASS→VETO) vs n(VETO→PASS); exact McNemar test |
| `S3` | Balance: median of (Δ`post_lb` − Δ`pull_lb`) / (Δ`post_lb` + Δ`pull_lb`) over pairs with a non-zero denominator. H-NET predicts ≈ 0; H-POST predicts > 0 |
| `S4` | Arm A rows at exactly 1.000: the share whose arm B partner is also 1.000 |
| `S5` | Arm C vs arm B: the same `S1` and `S3` on A-vs-C pairs. If C ≈ B, trade granularity does not drive the result |

**Verdicts (applied to `S1`, `S2`, `S3`; significance p < 0.01; "material" = a flip rate ≥ 5 % of aligned pairs or a median |Δ`pullFrac`| ≥ 0.05):**

| Verdict | Condition |
|---|---|
| **H-POST CONFIRMED** — the archive's artefact | VETO→PASS > PASS→VETO (p < 0.01) AND `S1` lower > higher (p < 0.01) AND `S3` median > 0.10 |
| **H-NET CONFIRMED, H-POST REFUTED** | PASS→VETO > VETO→PASS (p < 0.01) OR `S1` higher > lower (p < 0.01); AND `S3` median within ±0.10 |
| **NO MATERIAL DIFFERENCE** | Neither flip direction is material AND the median \|Δ`pullFrac`\| < 0.05 |
| **INCONCLUSIVE** | Anything else, or fewer than 200 qualifying pairs (INCONCLUSIVE-BY-COUNT) |

⚠ **Pre-registration record.** The thresholds above were written before any episode data was viewed. Two POPULATION rules were amended after viewing the smoke-run episodes, for mechanical reasons only: `book_folds` ≥ 5 became ≥ 2 (the 100 ms feed sends ~3 snapshots/s, so ≥ 5 dropped most 1–2 s episodes), and the overlap condition was added to the aligned-pair rule (a raw fragment paired with a whole episode). **The smoke-run data is excluded from the read.** Only the cloud run counts.

The primary verdict comes from aligned pairs. If S-CONCAT or S-INSTANT gives a different verdict, the read says so and names the fragmentation share; it does not choose the friendlier one.

**What each verdict means for the veto:**

- **H-POST confirmed:** the 100 ms feed inflates `pullFrac`; the veto rejects real absorption; the remedy is the feed or the accounting, not `max_pull_frac`.
- **H-NET confirmed:** the 100 ms feed UNDERSTATES gross pulls and posts; the veto is if anything lenient at 100 ms; the 1.000 point mass is a net-zero-episode property, not a feed artefact. The open question becomes the metric's definition (net vs gross flow per interval) — a scoring-class question, reserved for the trader.
- **No material difference:** feed granularity is not the issue; `D-3` (keep the veto, instrument) stands on firmer ground.

---

## 6. Decisions — `RBA-1` to `RBA-6`

New ID prefix `RBA` (raw book absorption), checked free with `git grep -E "\bRBA-"` (0 hits) on 2026-10-05.

| ID | Question | Options | Read | Status |
|---|---|---|---|---|
| `RBA-1` | The brief's channel `book.BTC-PERPETUAL.none.10.raw` does not exist. What raw book? | (a) rebuild the full book from `book.BTC-PERPETUAL.raw`, hand the tracker its top 10, check against the 100 ms snapshot by `change_id` · (b) drop the test | **(a).** It is the only raw book the venue serves, and the `change_id` check makes the rebuild verifiable. Three-step test: no richer option exists | Auto-proceeded (research, one revert) |
| `RBA-2` | Which trades feed the raw arm? | (a) raw trades for arm B, plus diagnostic arm C on 100 ms trades · (b) 100 ms trades only · (c) raw trades only | **(a).** It records the most and separates book granularity from trade timing. Three-step test: (a) is the richest option | Auto-proceeded |
| `RBA-3` | Episode pairing rule for the primary read | (a) aligned pairs only · (b) aligned primary plus S-CONCAT and S-INSTANT secondaries, all reported · (c) S-CONCAT primary | **(b).** Aligned pairs are the only pairs with equal spans; the secondaries show what fragmentation does. Three-step test: (b) is the richest | Auto-proceeded |
| `RBA-4` | Thresholds of the read rule (`raw-book-absorption-test-spec.md` §5) | as written · other values | As written: p < 0.01, 5 % flips, 0.05 median, ±0.10 balance, minimum 200 pairs. Pre-registered before any run data was read | Auto-proceeded; open to the trader before session 2 |
| `RBA-5` | Where the full run executes | (a) the temporary AWS instance `i-0b17cf2c2eb67496e`, 24 h, key from a mode-600 env file · (b) the dev machine, ≤ 6 h, ending before 19:00 UTC · (c) several dev-machine runs on separate days | **(a)**, by trader direction relayed 2026-10-05. ⚠ **`HH-2`'s recorded text still says "dev machine only".** The temporary instance is not the collector box, but the ruling text must be updated before the key goes there. Three-step test: (b) records fewer pairs and only one session; (c) takes several days | ⛔ **Trader / orchestrator: record the `HH-2` scope change before deploy** |
| `RBA-6` | Run length | (a) 24 h, every session once · (b) stop once the 200-pair minimum is met · (c) 10 h, NY plus early ASIA | **(a).** It records the most and covers all three sessions; it still ends before the Stage 1 read. Cost: the temporary instance lives ~13 h past the backfill's end (t3.small). Three-step test: (a) is the richest | Auto-proceeded; ⚠ the orchestrator must keep the instance up until ~15:00 UTC 2026-10-06 or stop the run early (`ssm-rawbook-stop.json`) |

**Harness 6 (the decision-bias tripwire), run 2026-10-05 13:17 UTC on `RBA-2`–`RBA-6` at rev `f330c70`.** Seat labels written first (all five `no_richer_option`). Files: `docs/harness-runs/decision-bias-20261005T1317Z-rawbook-{population,baseline,jev}.json`.

| ID | Jev modal verdict | Agreement over 5 samples | Seat label |
|---|---|---|---|
| `RBA-2` | `no_richer_option` | 1.0 | same |
| `RBA-3` | `no_richer_option` | 1.0 | same |
| `RBA-4` | `richer_option_wrong` | 1.0 | `no_richer_option` |
| `RBA-5` | `richer_option_wrong` | 1.0 | `no_richer_option` |
| `RBA-6` | `no_richer_option` | 0.8 (unstable) | same |

**No `gives_up_for_economy` flag**, so nothing is sent to the trader on bias grounds. The two disagreements (`RBA-4`, `RBA-5`) are between the two "no trade" labels and do not change the action. `RBA-5` still goes to the trader on its own: the `HH-2` scope.

---

## 7. Session 1 record — build and smoke run

**Commits:** tool `2d8d77d` (local, not pushed). **Gate:** `powershell -NoProfile -File tools/checks/verify-gate.ps1` (local-fast) after `2d8d77d`: harness ALL PASS, no display-parity drift, no engine-path change, `GATE PASSED`. ⚠ The gate does not build `tools/RawBookProbe/`; `H-1` below does.

**Build.** `dotnet build tools/RawBookProbe/RawBookProbe.vbproj -c Release`: 0 warnings, 0 errors. Linux publish (`-r linux-x64 --self-contained true`): succeeded, 72 MB. ⚠ The Linux binary was **not run** on Linux in session 1 (Docker Desktop was not running on the dev machine).

**Smoke run** — dev machine, foreground, 600 s, 13:02:58 → 13:12:58 UTC 2026-10-05, folder `C:\probe-runs\rawbook-smoke-20261005-130257\`, exit 0. First and last status lines:

```
[13:03:58] up 1m | msgs b100=180 braw=4317 t100=29 traw=59 | agree 143/0 prior 36/0 unres 1 drop 0 | gaps 0 reconn 0 gen 1 | levels ok 2 fail 0 age 55s | episodes(n/open) A=6/00 B=7/00 C=7/00 | mem ws 62MB priv 27MB heap 7MB | out 5KB
[13:12:58] up 10m | msgs b100=1811 braw=42059 t100=385 traw=855 | agree 1399/0 prior 411/0 unres 1 drop 0 | gaps 0 reconn 0 gen 1 | levels ok 11 fail 0 age 55s | episodes(n/open) A=37/00 B=67/00 C=67/00 | mem ws 66MB priv 29MB heap 7MB | out 35KB
```

| Check | Result |
|---|---|
| All four channels live | auth OK; four `subscribe ACCEPTED`; 1,811 / 42,059 / 385 / 855 messages (100 ms book / raw book / 100 ms trades / raw trades) |
| Rebuilt raw book | 1,399 exact + 411 prior agreements, **0 mismatches**, 1 unresolved (the first snapshot, before the raw snapshot landed) |
| Memory | private 27 → 29 MB, GC heap 7 → 8 MB over 10 status lines: **flat** |
| Resets | 0 raw gaps, 0 reconnects, 1 generation |
| Level carry | 11 of 11 refreshes OK |
| Episodes | A 37 · B 67 · C 67. Arm B has ~1.8× arm A's count: the raw feed fragments episodes at transient top-10 shifts |
| Credential strings in the run folders | 0 files contain `access_token`, `refresh_token` or `client_secret` |

The smoke run used the build before the pairing rule changed (`raw-book-absorption-test-spec.md` §5 record). A 200 s check on the final build (`C:\probe-runs\rawbook-verify-20261005-131309\`, exit 0) printed the live `pairs A-B` count and the same 0-mismatch agreement.

**Sizing from the smoke run (pairing rule of §5, scratch script):** arm A informative 15 · aligned A-B pairs 11 · aligned with arm A `pullFrac` ≤ 1: **6 in 10 min**. That was 13:03–13:13 UTC, the busiest hour on the box (253 episodes/h at 13:00 against a 7-day mean of 143/h). Scaled to the mean: ~20 qualifying pairs per hour, so the 200 minimum needs ~10 h, and 24 h gives an expected ~490. ⚠ One 10-minute sample; the uncertainty is wide, which is a second reason for 24 h.

⛔ **The smoke pairs are NOT a result** (`raw-book-absorption-test-spec.md` §5 record excludes them). They show only that both directions of change occur and the instrument produces pairs.

---

## 8. Cloud run — deploy steps for the orchestrator

Tooling: `tools/ops/rawbook/rawbook-cloud.sh` (verbs `start` · `loop` · `upload` · `stop` · `status`) and four SSM command files beside it. Template: `tools/ops/history-backfill/`.

1. **Package** (dev machine, PowerShell):
   ```powershell
   $out = 'C:\DeribitData\rawbook-tool'
   Remove-Item -Recurse -Force $out -ErrorAction SilentlyContinue
   dotnet publish tools\RawBookProbe\RawBookProbe.vbproj -c Release -r linux-x64 --self-contained true -o "$out\pkg\probe"
   Copy-Item tools\ops\rawbook\rawbook-cloud.sh "$out\pkg\"
   tar -czf "$out\rawbook-tool.tar.gz" -C "$out\pkg" .
   aws s3 cp "$out\rawbook-tool.tar.gz" s3://deribit-engine-bucket/rawbook-test/tool/rawbook-tool.tar.gz --region eu-west-2
   ```
2. **Install:** `ssm-rawbook-install.json`. Expect the probe's "STOP: --env-file not found" line (it proves the binary starts), `"version": 69`, and free memory.
3. **Place the key** (operator, not SSM parameters): `/opt/rawbook/ro.env`, mode 600, two lines `DERIBIT_RO_CLIENT_ID=…` and `DERIBIT_RO_CLIENT_SECRET=…`. ⛔ Never on a command line, in an SSM parameter, in the repo or in an upload. `upload` packs only the run folder.
4. **Start:** `ssm-rawbook-start.json`. Expect `active (running)` and the console's four `subscribe ACCEPTED` lines.
5. **Liveness:** `ssm-rawbook-status.json` — the last status line is at most ~60 s old; `agree … /0 prior …/0` (zero mismatches); heap flat. Status also lands in S3 every 30 min: `s3://deribit-engine-bucket/rawbook-test/run1/status_latest.txt`.
6. **Stop early:** `ssm-rawbook-stop.json` (writes the STOP file; the loop uploads once more).
7. **Collect:** `s3://deribit-engine-bucket/rawbook-test/run1/rawbook-out.tgz` (refreshed every 30 min and at the end) and `DONE`.

**Memory budget on the t3.small (2 GiB, shared with the history backfill):** GC heap hard limit 256 MB; the probe stops itself at 160 MB of GC heap; systemd `MemoryMax=400M`. The smoke run held a 7 MB GC heap and about 30 MB private memory (§7).

---

## 9. Handles

| ID | Kind | Handle | What it confirms |
|---|---|---|---|
| `H-1` | runnable | `dotnet build tools/RawBookProbe/RawBookProbe.vbproj -c Release` | 0 warnings, 0 errors |
| `H-2` | runnable | `git grep -n "LevelAbsorptionTracker" -- tools/RawBookProbe/` | The tracker is linked from `Core/`, not copied: the hits are the vbproj `Compile Include` and type references; no `Class LevelAbsorptionTracker` under `tools/` |
| `H-3` | runnable | `git grep -nE "_clientSecret\|DERIBIT_RO_CLIENT_SECRET" -- tools/RawBookProbe tools/ops/rawbook` and read each hit | No write path emits the secret: every hit is a read, the auth request builder, or a length print |
| `H-4` | runnable, after the run | Re-derive `S1`–`S5` from `episodes_*.csv` with an independent script | The verdict |
| `E-1` | evidence | The smoke run folder on the dev machine (§7) | The probe ran with all four channels live and a flat heap |

---

## 10. What session 1 did not verify

- The Linux binary was not executed on Linux.
- The SSM files were validated as JSON only; none was sent.
- Whether `HH-2`'s "dev machine only" condition is lifted for the temporary instance: relayed, not recorded in `HH-2`'s text.
- The informative and aligned fractions for a 24 h run are extrapolated from 10 minutes of one session (NY open).
- H-NET's identity assumes equal masks and equal fill attribution; that is what the run tests, not a verified premise.

---

## 11. Session 2 addendum, 2026-10-06 (UTC) — diagnosis of the 29 "prior" mismatches

**Written before any per-episode `pullFrac` comparison was read.** ⚠ One disclosure: the seat printed `summary_20261005-132038.txt` to read the agreement line. That file also carries the probe's own convenience A-vs-B pair counts. The seat did not run its own pairing script and did not open `episodes_*.csv`. The diagnosis below uses only the agreement counters and the probe source.

**Inputs.** `C:\DeribitData\rawbook-run1\out\` (archive MD5 = S3 ETag `7f9e2a5dd70da73c7929f0580e086ebd`, per the brief; not re-checked here). Probe source `tools/RawBookProbe/RawBookProbeProgram.vb` at `2d8d77d`.

### 11.1 What the probe recorded about a mismatch — the limiting fact

| Item | Recorded? | Where |
|---|---|---|
| Count of exact and prior matches and mismatches | Yes, cumulative | Every status line (1 per minute) in `console.log`, and `summary_*.txt` |
| Time of each mismatch | **Only to the status-line minute** | Derived from the counter step between two status lines |
| The 100 ms snapshot's `change_id` | **No** | — |
| The prior raw `change_id` used in the comparison | **No** | — |
| The 100 ms top 10 and the rebuilt top 10 | **No** — only a hash, held in memory | `BookHash` in `RawBookProbeProgram.vb`; the ring is not written out |
| The raw messages around the mismatch | **No** | — |

⛔ **Finding: `Resolve_Locked` in `RawBookProbeProgram.vb` only increments `_agreePriorMismatch`. It writes no event, no ids and no book.** The `events_*.log` file has no mismatch line. A re-run is not available (the instance is terminated).

**So the per-mismatch test the ruling asks for cannot be run on this data.** To call one mismatch a timing artefact, the diagnosis needs at least: the 100 ms `change_id` X, the prior raw `change_id` P, the next raw `change_id` N > X, the 100 ms top 10, and the rebuilt top 10 at P and at N. A timing artefact predicts that the 100 ms book is a state between P and N: the raw arm reaches it at N, or inside the raw message that spans X. A rebuild error predicts that it matches neither, and that the rebuilt book stays wrong at the next exact check. None of these were recorded.

### 11.2 Per-mismatch record — everything the data supports

Method: a scratch script parsed all 1,439 status lines in `console.log`, took the step in each counter between consecutive lines, and listed every window where the prior-mismatch counter rose. The steps sum to 29, equal to the final counter. Columns per one-minute window: raw book messages, exact checks (all matched), prior checks that matched.

| # | Window end (UTC) | `gen` | Raw msgs in window (percentile of all windows) | Exact checks, all matched | Prior matches | Class |
|---:|---|---:|---:|---:|---:|---|
| 1 | 2026-10-05 13:52:40 | 1 | 4,396 (94th) | 120 | 61 | UNEXPLAINED |
| 2 | 13:54:40 | 1 | 4,427 (94th) | 108 | 72 | UNEXPLAINED |
| 3 | 14:08:41 | 1 | 5,029 (100th) | 118 | 62 | UNEXPLAINED |
| 4 | 14:09:41 | 1 | 4,755 (98th) | 127 | 53 | UNEXPLAINED |
| 5 | 14:19:41 | 1 | 4,516 (96th) | 125 | 55 | UNEXPLAINED |
| 6 | 14:30:42 | 1 | 4,134 (91st) | 114 | 66 | UNEXPLAINED |
| 7 | 14:31:42 | 1 | 4,787 (99th) | 112 | 67 | UNEXPLAINED |
| 8 | 15:38:51 | 2 | 4,722 (98th) | 128 | 52 | UNEXPLAINED |
| 9 | 15:42:51 | 2 | 4,773 (99th) | 136 | 44 | UNEXPLAINED |
| 10 | 15:43:51 | 2 | 4,701 (98th) | 137 | 43 | UNEXPLAINED |
| 11 | 15:45:51 | 2 | 4,820 (99th) | 143 | 37 | UNEXPLAINED |
| 12 | 16:16:54 | 2 | 3,824 (87th) | 115 | 66 | UNEXPLAINED |
| 13 | 16:17:54 | 2 | 4,507 (95th) | 118 | 62 | UNEXPLAINED |
| 14 | 16:23:54 | 2 | 4,473 (95th) | 120 | 60 | UNEXPLAINED |
| 15 | 16:43:54 | 2 | 2,442 (67th) | 112 | 68 | UNEXPLAINED |
| 16 | 17:30:57 | 2 | 4,229 (91st) | 141 | 39 | UNEXPLAINED |
| 17 | 2026-10-06 02:29:39 | 2 | 2,335 (62nd) | 115 | 66 | UNEXPLAINED |
| 18 | 03:05:41 | 2 | 2,743 (75th) | 119 | 59 | UNEXPLAINED |
| 19 | 05:43:53 | 2 | 1,992 (42nd) | 98 | 82 | UNEXPLAINED |
| 20 | 06:09:54 | 2 | 2,847 (77th) | 105 | 76 | UNEXPLAINED |
| 21 | 08:49:05 | 2 | 2,357 (63rd) | 112 | 69 | UNEXPLAINED |
| 22 | 09:16:08 | 2 | 2,350 (63rd) | 133 | 47 | UNEXPLAINED |
| 23 | 09:21:09 | 2 | 2,738 (75th) | 130 | 51 | UNEXPLAINED |
| 24–25 | 09:29:09 (two in one window) | 2 | 4,487 (95th) | 138 | 40 | UNEXPLAINED (both) |
| 26 | 09:52:10 | 2 | 3,512 (84th) | 132 | 48 | UNEXPLAINED |
| 27 | 11:32:18 | 2 | 2,002 (42nd) | 112 | 68 | UNEXPLAINED |
| 28 | 11:34:18 | 2 | 1,768 (29th) | 103 | 75 | UNEXPLAINED |
| 29 | 13:10:25 | 2 | 2,536 (70th) | 121 | 60 | UNEXPLAINED |

**Tally: artefact shown 0 · rebuild error shown 0 · unexplained 29.** The count at 19.3 h was 20 (rows 1–20); rows 21–29 came after.

### 11.3 Class-level evidence — consistent with the artefact hypothesis, but not proof per mismatch

| Evidence | Value | Points to |
|---|---|---|
| Exact checks over the run | 156,319 matched, **0 mismatched** | No rebuild error visible at an exact `change_id` |
| Exact checks inside the 28 mismatch windows | 3,392, all matched | The rebuilt top 10 agreed exactly within the same minute as every mismatch |
| Prior-mismatch rate | 29 of 103,141 prior checks (0.028 %) | Rare |
| Raw message rate, mismatch windows vs all others | median 4,182 vs 2,117 per minute; 15 of 28 windows at or above the 90th percentile | Mismatches cluster in busy minutes. **Both hypotheses predict this.** More changes per interval means more chance that a change between P and X touches the top 10. It also means more exposure to any rare apply bug |
| Reconnect at 15:29:38 | Counter stayed at 7 across it | The reconnect did not produce a mismatch |
| Raw chain breaks | 0 (`gaps 0` on every line) | No missed raw message |

⚠ **Why the class-level evidence is not enough.**

- A rebuild error that lives for less than the gap to the next exact check leaves no trace in the exact counter. That gap averages about 0.55 s: the 100 ms feed sent ~3 snapshots per second (about 181 per status minute), and 60 % of checks were exact.
- A rebuild error triggered only by multi-change raw messages would co-occur with "prior" checks, because those messages are the ones that skip a `change_id`.
- The exact-check record cannot rule out either case.

⚠ **The fallback comparison's own premise is unverified.** The `Resolve_Locked` doc comment says the raw state at the latest `change_id` before X "is the same book". That holds only if no top-10 change lies between P and X. If the raw channel delivers several changes under one `change_id`, the true book at X lies between the states at P and at N, and some prior mismatches are expected. The run does not show which venue behaviour holds.

### 11.4 A latent probe defect found during the diagnosis (did not fire)

- On reconnect, `ProbeReset_Locked` clears `_pending`. It does **not** reset `_rawValid`, `_rawLastChange` or the change ring (`_ring`, `_ringIds`).
- So a gen-2 100 ms snapshot that arrives before the gen-2 raw snapshot is held as pending. If its `change_id` is below the new raw snapshot's, the prior lookup finds the last **gen-1** raw state, about 2 s stale.
- Effect in this run: **none observed.** The prior-mismatch counter read 7 at 15:28:50 and 7 at 15:29:50. The reconnect was at 15:29:38–40.
- A re-run must fix it. Otherwise a reconnect can add a mismatch that looks like the others but has a known cause.

### 11.5 Decision under the 2026-10-06 ruling

- The ruling (`raw-book-absorption-test-spec.md` §0 box): a verdict only if **all** mismatches are shown to be fallback-comparison artefacts.
- Shown: 0 of 29. **→ NO VERDICT. The read stops here.**
- The registered read (`RBA-3` pairing rule, `RBA-4` thresholds) was **not run**. `episodes_*.csv` was not opened. The registration stays unchanged and unused, so a later run can use it without look-ahead.

### 11.6 What would let a read proceed — options for the trader (not decided here)

This is a new design decision, so the seat stops and reports it.

| Option | What it takes | What it gives |
|---|---|---|
| (a) Instrumented re-run | Add per-mismatch logging to the probe: X, P, N, both top 10s, the rebuilt top 10 at N, the raw messages in (P, N]. Fix the reconnect reset in `raw-book-absorption-test-spec.md` §11.4. Run again | A per-mismatch diagnosis, then the read on fresh data. ⚠ Needs AWS for 24 h, or a dev-machine run of at most 6 h (fewer pairs, one session). The trader is away 2026-10-14 → 2026-11-25 |
| (b) The trader accepts the class-level evidence in `raw-book-absorption-test-spec.md` §11.3 | A trader ruling that relaxes the per-mismatch bar | The read can run on this run's data. ⚠ It reverses the test the 2026-10-06 ruling set |
| (c) Leave it until after 2026-11-25 | Nothing now | The mechanism-revision build stays gated |

Seat's read: (a). It records more and needs no relaxed bar. (b) gives up a guarantee for speed, so under the auto-proceed rules in `CLAUDE.md` it is reserved for the trader in any case.

### 11.7 What this session did not verify

- The archive MD5 against the S3 ETag: carried from the brief, not re-run.
- Whether the Deribit raw book channel delivers several changes under one `change_id`: not measurable from this data.
- The probe's convenience pair counts in `summary_*.txt`: seen, not re-derived, and not used.
- Harness 6 (the decision-bias tripwire) was not run on the options in `raw-book-absorption-test-spec.md` §11.6, because the seat does not decide them.

---

## 12. Read result (quarantined), 2026-10-06 (UTC)

**Under the trader's ruling of 2026-10-06, option (d): the quarantine box at the top of `raw-book-absorption-test-spec.md` §0.** Start commit `a0d5ae7`. Run once.

### 12.1 Instrument and order of work

| Step | Record |
|---|---|
| Script | `tools/ops/rawbook/rawbook_read.py`, committed at `65889df` **before** it read any `pullFrac` value. Its header fixes every application detail the spec leaves open (quarantine overlap, PROBE_ handling, tolerances, verdict tie rule) |
| Fix before the read | `3707774`: the exact binomial p overflowed a float at n above ~1,000. The first run crashed inside the last secondary, before it printed anything. No rule changed |
| Independence | The script pairs episodes from `episodes_*.csv` itself. It does not call or copy the probe's `AlignedPairs_Locked` |
| Selftest | `python tools/ops/rawbook/rawbook_read.py --selftest` → `SELFTEST PASS (0 failed)` |
| Mutation | `--selftest --mutate per-episode` (quarantine applied to episodes BEFORE pairing) → `kept pairs are exactly case 4 (L4) want L4 got L3,L4 FAIL` · `SELFTEST FAIL (1 failed)`. The per-episode filter removes a quarantined B fragment and turns a two-overlap A episode into a false pair |
| Data | `C:\DeribitData\rawbook-run1\out\`. Archive `rawbook-out.tgz` MD5 `7f9e2a5dd70da73c7929f0580e086ebd` (re-run this session; equals the brief's S3 ETag). `episodes_*.csv` and `samples_*.csv` in `out\` are byte-identical to the archive copies (MD5 compared) |
| Command | `python tools/ops/rawbook/rawbook_read.py --run-dir C:/DeribitData/rawbook-run1/out --windows docs/audits/proofs/raw-book-quarantine-2026-10-06/mismatch_windows.csv` |

### 12.2 Output, verbatim

⚠ One cosmetic defect: in the S5 block (A vs C), the label "B higher" means "C higher". The numbers are the A-vs-C numbers.

```
==== raw-book absorption read, quarantined (ruling 2026-10-06 option (d)) ====
episodes file episodes_20261005-132038.csv | windows 28 | max_pull_frac 0.75 | floor 5000
check P3: max |recomputed pullFrac - CSV pull_frac| = 5.00e-07 (CSV prints 6 decimals)
gens [1, 2] (escalation if > 20)

-- episode exposure to the quarantine windows (all rows / non-PROBE rows) --
arm A: 89 of 3565 rows overlap a window | non-PROBE 89 of 3565
arm B: 164 of 6874 rows overlap a window | non-PROBE 164 of 6874
arm C: 164 of 6874 rows overlap a window | non-PROBE 164 of 6874
arm A informative episodes: 1979 (not overlapping a window: 1936)

-- A-B aligned pairs: before quarantine 1443, dropped 25, kept 1418 --
   dropped by cause: A only 0 | B only 0 | both 25
   session   before  dropped    A-ovl    B-ovl     kept
   ASIA         332        3        3        3      329
   LONDON       243       13       13       13      230
   NY           868        9        9        9      859

-- A-C aligned pairs: before quarantine 1226, dropped 21, kept 1205 --
   dropped by cause: A only 0 | C only 0 | both 21
   session   before  dropped    A-ovl    C-ovl     kept
   ASIA         288        3        3        3      285
   LONDON       217       11       11       11      206
   NY           721        7        7        7      714

check E1: aligned A-B / arm A informative = 1443/1979 = 0.729 before quarantine, 1418/1936 = 0.732 after (stop if < 0.5)
fragmentation (arm A informative, not in a window): overlapping B episodes 0: 3 | 1: 1419 | 2+: 514 (2+ share 26.5 %)

== PRIMARY: A vs B aligned pairs, quarantined ==

  pooled     n=1418 | S1 (A<=1, n=723): B higher 585, lower 63, equal 75, sign p=5.78e-107
             S2 flips: PASS->VETO 277 (19.5 %), VETO->PASS 76 (5.4 %), McNemar p=5.54e-28
             S3 balance median -0.0461 (n=1349 nonzero denominators, 8 negative) | median |dPF| all 0.5177, A<=1 0.3339 | median dPullLB 56625 dPostLB 51640
             S4 A exactly 1.000: 12, B partner also 1.000: 3 (25.0 %)
  floored    n=341 | S1 (A<=1, n=153): B higher 85, lower 7, equal 61, sign p=3.85e-18
             S2 flips: PASS->VETO 58 (17.0 %), VETO->PASS 21 (6.2 %), McNemar p=3.76e-05
             S3 balance median -0.0004 (n=286 nonzero denominators, 1 negative) | median |dPF| all 1.5640, A<=1 0.2083 | median dPullLB 19450 dPostLB 16070
             S4 A exactly 1.000: 0, B partner also 1.000: 0
  unfloored  n=1077 | S1 (A<=1, n=570): B higher 500, lower 56, equal 14, sign p=4.05e-90
             S2 flips: PASS->VETO 219 (20.3 %), VETO->PASS 55 (5.1 %), McNemar p=2.44e-24
             S3 balance median -0.0535 (n=1063 nonzero denominators, 7 negative) | median |dPF| all 0.4389, A<=1 0.3461 | median dPullLB 74190 dPostLB 65100
             S4 A exactly 1.000: 12, B partner also 1.000: 3 (25.0 %)
rows holding: ['H-NET CONFIRMED, H-POST REFUTED']
PRIMARY VERDICT: H-NET CONFIRMED, H-POST REFUTED

== S5: A vs C aligned pairs, quarantined (S1 and S3 are the registered statistics) ==

  pooled     n=1205 | S1 (A<=1, n=618): B higher 493, lower 52, equal 73, sign p=3.82e-91
             S2 flips: PASS->VETO 242 (20.1 %), VETO->PASS 65 (5.4 %), McNemar p=3.92e-25
             S3 balance median -0.0520 (n=1138 nonzero denominators, 9 negative) | median |dPF| all 0.5387, A<=1 0.3454 | median dPullLB 57690 dPostLB 51600
             S4 A exactly 1.000: 11, B partner also 1.000: 3 (27.3 %)
  floored    n=303 | S1 (A<=1, n=145): B higher 79, lower 7, equal 59, sign p=1.52e-16
             S2 flips: PASS->VETO 55 (18.2 %), VETO->PASS 19 (6.3 %), McNemar p=3.38e-05
             S3 balance median 0.0000 (n=250 nonzero denominators, 1 negative) | median |dPF| all 1.2000, A<=1 0.2083 | median dPullLB 17410 dPostLB 15660
             S4 A exactly 1.000: 0, B partner also 1.000: 0
  unfloored  n=902 | S1 (A<=1, n=473): B higher 414, lower 45, equal 14, sign p=8.19e-76
             S2 flips: PASS->VETO 187 (20.7 %), VETO->PASS 46 (5.1 %), McNemar p=2.35e-21
             S3 balance median -0.0593 (n=888 nonzero denominators, 8 negative) | median |dPF| all 0.4585, A<=1 0.3781 | median dPullLB 77390 dPostLB 67125
             S4 A exactly 1.000: 11, B partner also 1.000: 3 (27.3 %)

== SECONDARY S-CONCAT (A vs summed overlapping B), quarantined: kept 1933, dropped 43 ==

  pooled     n=1933 | S1 (A<=1, n=1032): B higher 830, lower 99, equal 103, sign p=1.62e-144
             S2 flips: PASS->VETO 400 (20.7 %), VETO->PASS 115 (5.9 %), McNemar p=6.58e-38
             S3 balance median -0.0415 (n=1841 nonzero denominators, 29 negative) | median |dPF| all 0.4919, A<=1 0.3344 | median dPullLB 63000 dPostLB 57820
             S4 A exactly 1.000: 18, B partner also 1.000: 3 (16.7 %)
  floored    n=425 | S1 (A<=1, n=198): B higher 105, lower 11, equal 82, sign p=2.11e-20
             S2 flips: PASS->VETO 69 (16.2 %), VETO->PASS 27 (6.4 %), McNemar p=2.15e-05
             S3 balance median 0.0000 (n=353 nonzero denominators, 3 negative) | median |dPF| all 1.4142, A<=1 0.1226 | median dPullLB 18010 dPostLB 15900
             S4 A exactly 1.000: 0, B partner also 1.000: 0
  unfloored  n=1508 | S1 (A<=1, n=834): B higher 725, lower 88, equal 21, sign p=2.07e-125
             S2 flips: PASS->VETO 331 (21.9 %), VETO->PASS 88 (5.8 %), McNemar p=3.25e-34
             S3 balance median -0.0482 (n=1488 nonzero denominators, 26 negative) | median |dPF| all 0.4380, A<=1 0.3523 | median dPullLB 81775 dPostLB 73020
             S4 A exactly 1.000: 18, B partner also 1.000: 3 (16.7 %)
rows holding: ['H-NET CONFIRMED, H-POST REFUTED'] | S-CONCAT verdict by the same rule: H-NET CONFIRMED, H-POST REFUTED

== SECONDARY S-INSTANT (last co-active sample per arm A episode), quarantined: kept 2479, dropped 56 (rows unmapped 0, PROBE_ skipped 0) ==

  pooled     n=2479 | S1 (A<=1, n=1622): B higher 1143, lower 86, equal 393, sign p=2.31e-236
             S2 flips: PASS->VETO 595 (24.0 %), VETO->PASS 114 (4.6 %), McNemar p=2.27e-79
             S3 balance median -0.0086 (n=2155 nonzero denominators, 45 negative) | median |dPF| all 0.4666, A<=1 0.2915 | median dPullLB 34100 dPostLB 31190
             S4 A exactly 1.000: 13, B partner also 1.000: 4 (30.8 %)
  floored    n=1116 | S1 (A<=1, n=800): B higher 433, lower 16, equal 351, sign p=1.42e-106
             S2 flips: PASS->VETO 294 (26.3 %), VETO->PASS 48 (4.3 %), McNemar p=2.89e-44
             S3 balance median 0.0000 (n=824 nonzero denominators, 9 negative) | median |dPF| all 0.6553, A<=1 0.2000 | median dPullLB 10535 dPostLB 6930
             S4 A exactly 1.000: 0, B partner also 1.000: 0
  unfloored  n=1363 | S1 (A<=1, n=822): B higher 710, lower 70, equal 42, sign p=3.34e-134
             S2 flips: PASS->VETO 301 (22.1 %), VETO->PASS 66 (4.8 %), McNemar p=5.72e-37
             S3 balance median -0.0190 (n=1331 nonzero denominators, 36 negative) | median |dPF| all 0.4083, A<=1 0.3236 | median dPullLB 58900 dPostLB 53650
             S4 A exactly 1.000: 13, B partner also 1.000: 4 (30.8 %)
rows holding: ['H-NET CONFIRMED, H-POST REFUTED'] | S-INSTANT verdict by the same rule: H-NET CONFIRMED, H-POST REFUTED

SUMMARY: primary H-NET CONFIRMED, H-POST REFUTED | S-CONCAT H-NET CONFIRMED, H-POST REFUTED | S-INSTANT H-NET CONFIRMED, H-POST REFUTED
```

### 12.3 Verdict against the registered rule (`RBA-4`, `raw-book-absorption-test-spec.md` §5)

**PRIMARY VERDICT: H-NET CONFIRMED, H-POST REFUTED.** Both secondaries give the same verdict, so the fragmentation share (26.5 % of clean informative arm A episodes overlap 2+ arm B episodes) does not change the answer.

| Registered condition | Required | Measured (A vs B, pooled, quarantined) | Holds? |
|---|---|---|---|
| Minimum count | ≥ 200 aligned pairs with arm A `pullFrac` ≤ 1 | 723 | ✅ |
| Escalation: aligned share | aligned ≥ half of arm A informative | 0.729 before, 0.732 after the quarantine | ✅ no stop |
| Escalation: resets | `gen` count ≤ 20 | 2 | ✅ no stop |
| H-NET part 1 | PASS→VETO > VETO→PASS (p < 0.01) OR `S1` higher > lower (p < 0.01) | Flips 277 vs 76, p = 5.5e-28 · `S1` 585 vs 63, p = 5.8e-107 | ✅ both arms of the OR |
| H-NET part 2 | `S3` median within ±0.10 | −0.046 | ✅ |
| H-POST | VETO→PASS > PASS→VETO AND `S1` lower > higher AND `S3` > 0.10 | All three fail | ❌ |
| NO MATERIAL DIFFERENCE | both flip rates < 5 % AND median \|Δ`pullFrac`\| < 0.05 | 19.5 % and 5.4 %; median 0.518 | ❌ |

### 12.4 What the read does NOT fit — reported, not used to change the verdict

| Item | H-NET (pure netting) predicts | Measured | Reading |
|---|---|---|---|
| `S4`: 1.000 rows | Stay at 1.000 (partition-invariant) | 3 of 12 stay (25 %) | Pure netting does not hold on these rows. n = 12 is small |
| VETO→PASS flips | None | 76 (5.4 %, above the 5 % "material" bar) | A residual that netting cannot produce |
| `S5`: arm C vs arm B | — | C ≈ B on every statistic (`S1` 493/52 vs 585/63; `S3` −0.052 vs −0.046; VETO→PASS 5.4 % both) | Trade granularity does not drive the result or the residual |

- The residual points to the mask differences named in `raw-book-absorption-test-spec.md` §2, not to trade timing. ⚠ **Not verified:** no statistic in the registration isolates the mask.
- The direction is not in doubt. The raw feed raises `pullLB` and `postLB` by similar amounts (median Δ 56,625 vs 51,640 USD), and raw `pullFrac` reads higher in 585 of 648 non-equal pairs.

### 12.5 What this session did not do

- No sensitivity line with the quarantined pairs included. The spec registered none, so none was computed.
- Harness 6 (the decision-bias tripwire) was not run: this session applied a registered rule and made no new recommendation on a decision with options.
- The probe's convenience pair count (1,443 in `status_latest.txt`) was seen in session 2. The independent script reproduces it exactly (1,443 before the quarantine). That is the only use made of it.
