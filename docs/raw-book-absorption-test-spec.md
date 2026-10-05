# Raw order-book absorption test — does the 100 ms book feed mis-measure `pullFrac`?

**Status:** session 1 done 2026-10-05 (UTC): spec, probe, smoke run. The full run is sized above 6 hours, so it goes to the temporary AWS instance (trader direction 2026-10-05, relayed by the orchestrator). **Session 2 = the read.** Start commit `a7a9cf0`.

**Source of the test:** the surviving half of the "REFUTED ON DIRECTION 2026-08-20" row — [`trader-tick-queue-archive.md` §C, `trim-2026-09-14-47`](trader-tick-queue-archive.md#trim-2026-09-14-47) and the live row in `docs/trader-tick-queue.md` §2. **Key scope:** the read-only key, `HH-2` (the read-only-key ruling, widened 2026-09-29) in [`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) §4.

**Class:** read-only research. No `settings.json` change, no scoring change, no collector contact, no `Core/` or `UI/` edit.

---

## 0. Session-2 brief — the read

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
