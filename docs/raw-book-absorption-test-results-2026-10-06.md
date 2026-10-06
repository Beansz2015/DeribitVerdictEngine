# Raw order-book absorption test — results, 2026-10-06 (UTC)

**Spec:** [`raw-book-absorption-test-spec.md`](raw-book-absorption-test-spec.md). Full diagnosis: `raw-book-absorption-test-spec.md` §11. Run: 24 h, 2026-10-05 13:20:38 → 2026-10-06 13:20:38 UTC, temporary AWS instance, data in `C:\DeribitData\rawbook-run1\out\`.

> ✅ **Superseded in part, same day:** the trader ruled option (d), a per-pair quarantine (`raw-book-absorption-test-spec.md` §0 box). The read then ran. **Verdict: H-NET CONFIRMED, H-POST REFUTED** — see "Session 3 — the read (quarantined)" at the end of this file. The session-2 record below stays as written.

## Verdict (session 2)

⛔ **NO VERDICT.** The registered read was not run.

## Why

- The trader's ruling of 2026-10-06 (`raw-book-absorption-test-spec.md` §0 box): a verdict is allowed only if every "prior" book mismatch is shown to be an artefact of the fallback comparison.
- The run ended at `agree 156319/0 prior 103112/29`: 0 exact mismatches, 29 prior mismatches.
- **None of the 29 can be classified.** The probe recorded only a cumulative counter. It did not write the `change_id`s, the two top-10 books or the raw messages for a mismatch (`Resolve_Locked` in `tools/RawBookProbe/RawBookProbeProgram.vb` increments a counter and nothing else).
- Each mismatch is located to its one-minute status window only (table in `raw-book-absorption-test-spec.md` §11.2).

| Class | Count | Evidence type |
|---|---:|---|
| Shown to be a fallback-comparison artefact | 0 | — |
| Shown to be a rebuild error | 0 | — |
| Unexplained | 29 | Minute-level location from status-line counter steps |

## Class-level evidence (not enough under the ruling)

- 156,319 exact checks, 0 mismatched. 3,392 of them fell in the 28 mismatch minutes, all matched.
- The mismatch minutes are busy: median 4,182 raw messages per minute against 2,117 elsewhere. Both hypotheses predict this.
- No mismatch at the one reconnect (15:29:38 UTC). 0 raw chain breaks.
- This fits the timing-artefact hypothesis. It does not exclude a short-lived rebuild error, or one tied to multi-change raw messages.

## A probe defect found (did not fire)

- On reconnect the probe does not reset its raw-book state or its change ring. A 100 ms snapshot early in a new `gen` can be compared with a ~2 s stale raw book.
- The counter did not move across the reconnect, so it did not affect this run. A re-run must fix it (`raw-book-absorption-test-spec.md` §11.4).

## What it means

| Item | Effect |
|---|---|
| The absorption pull veto (`max_pull_frac` 0.75) | **No new evidence either way.** `D-3` of `docs/absorption-mechanism-revision-proposal.md` (keep the veto, instrument) stands as it was. Do not tune the veto on this run |
| The gated mechanism-revision build (`docs/trader-tick-queue.md` §2, row "UNBLOCKED 2026-09-29") | **Stays gated.** The answer it waits for does not exist yet |
| The absorption Stage 1 read (~2026-10-08) | Goes ahead without this answer |
| The pre-registration (`RBA-3` pairing rule, `RBA-4` thresholds in `raw-book-absorption-test-spec.md` §6) | Unchanged and unused. `episodes_*.csv` was not opened, so a later run can use it without look-ahead |

## Decision for the trader (not decided by this seat)

- (a) Instrumented re-run: per-mismatch logging plus the reconnect fix, then run again. **Seat's read: (a).**
- (b) Accept the class-level evidence and run the read on this data. This relaxes the 2026-10-06 ruling's own test.
- (c) Leave it until after 2026-11-25. The trader is away 2026-10-14 → 2026-11-25.
- Details: `raw-book-absorption-test-spec.md` §11.6.

## Verified and not verified

- **Verified:** the 29 counter steps sum to the final counter (script over all 1,439 status lines in `console.log`). The probe source records no per-mismatch data (read at `2d8d77d`). The reconnect window shows no counter step.
- **Not verified:** the archive MD5 against the S3 ETag (carried from the brief). Whether the venue's raw channel batches several changes under one `change_id`. The probe's convenience pair counts in `summary_*.txt` were seen when that file was printed for its agreement line; they were not re-derived and are not a result.

---

## Session 3 — the read (quarantined), 2026-10-06 (UTC)

**Rule:** the trader's ruling of 2026-10-06, option (d) (`raw-book-absorption-test-spec.md` §0 box). Drop every A–B pair in which EITHER episode overlaps one of the 28 quarantine windows. Then run the registered read once (`RBA-3` pairing, `RBA-4` thresholds, both in `raw-book-absorption-test-spec.md` §6). Full output: `raw-book-absorption-test-spec.md` §12.

**Script:** `tools/ops/rawbook/rawbook_read.py`, committed at `65889df` before any `pullFrac` was read. Overflow fix `3707774` before the read (the crash printed nothing). Start commit `a0d5ae7`.

### Verdict

✅ **H-NET CONFIRMED, H-POST REFUTED.** The 100 ms feed does not inflate `pullFrac`. It UNDERSTATES gross pulls and posts by about the same amount, so raw `pullFrac` reads HIGHER. Both secondaries (S-CONCAT, S-INSTANT) give the same verdict.

### Pairs before and after the quarantine

| Population | Before | Dropped | Kept |
|---|---:|---:|---:|
| A-B aligned pairs (primary) | 1,443 | 25 | 1,418 |
| A-C aligned pairs (`S5`) | 1,226 | 21 | 1,205 |
| Qualifying A-B pairs (arm A `pullFrac` ≤ 1) | — | — | 723 (minimum 200) |

| Session (arm A open hour, UTC) | A-B before | A-B dropped | A-B kept | A-C dropped |
|---|---:|---:|---:|---:|
| ASIA (0–7) | 332 | 3 | 329 | 3 |
| LONDON (8–12) | 243 | 13 | 230 | 11 |
| NY (13–23) | 868 | 9 | 859 | 7 |

- **Per arm:** in every dropped pair, BOTH episodes overlapped a window (A-B: 25 both, 0 A only, 0 B only; A-C: 21 both). This is expected: aligned episodes sit within 500 ms of each other, and a window is 60 s.
- **Episode exposure** (reproduces the ruling box): arm A 89 of 3,565, arm B 164 of 6,874, arm C 164 of 6,874.

### The numbers behind it (A vs B, pooled, quarantined)

| Statistic | H-POST needs | H-NET needs | Measured |
|---|---|---|---|
| `S1`: arm B `pullFrac` vs arm A, where A ≤ 1 | lower > higher, p < 0.01 | higher > lower, p < 0.01 (or `S2`) | higher 585 · lower 63 · equal 75 · p = 5.8e-107 |
| `S2`: veto flips at `max_pull_frac` 0.75 | VETO→PASS > PASS→VETO, p < 0.01 | PASS→VETO > VETO→PASS, p < 0.01 (or `S1`) | PASS→VETO 277 (19.5 %) · VETO→PASS 76 (5.4 %) · p = 5.5e-28 |
| `S3`: balance median | > 0.10 | within ±0.10 | −0.046 |
| `S4`: arm A rows at 1.000 that stay 1.000 | (shrinks) | (all stay) | 3 of 12 |
| `S5`: arm C vs arm B | — | — | C ≈ B: `S1` 493/52, `S3` −0.052 |

- Floored and unfloored splits agree in direction (floored `S1` 85/7, unfloored 500/56; `S3` −0.0004 and −0.054).
- Median Δ`pullLB` 56,625 USD and Δ`postLB` 51,640 USD: the finer feed adds to both, as `raw-book-absorption-test-spec.md` §2 derived.

### What does not fit pure netting — reported, verdict unchanged

- **76 VETO→PASS flips (5.4 %).** Pure netting cannot produce them. Arm C shows the same rate, so trade timing is not the cause. The likely source is the visibility-mask difference. ⚠ Not verified: no registered statistic isolates the mask.
- **9 of 12 arm A rows at exactly 1.000 move on arm B.** H-NET predicts all 12 stay. n = 12 is small.

### What it means

| Item | Effect |
|---|---|
| The absorption pull veto (`max_pull_frac` 0.75); decision `D-3` in [`absorption-mechanism-revision-proposal.md`](absorption-mechanism-revision-proposal.md) (keep the veto, instrument) | **`D-3` stands, on firmer ground.** There is no feed-artefact case for LOOSENING the veto: the H-POST premise is refuted. The spec's registered meaning (`raw-book-absorption-test-spec.md` §5): the veto is, if anything, lenient at 100 ms. **Do not tune the veto on this result in either direction.** Tightening it would be a scoring change, reserved for the trader |
| The open question this verdict creates | Per `raw-book-absorption-test-spec.md` §5: the metric's DEFINITION — net flow per book interval (today) vs gross flow. **Scoring-class, reserved for the trader.** No build follows from this read alone |
| The gated mechanism-revision build (`docs/trader-tick-queue.md` §2, row "UNBLOCKED 2026-09-29"; archive row `trim-2026-09-14-47`) | **The answer it waited for now exists: the feed is not the H-POST artefact, so the remedy is not the feed and not a looser veto.** The gate on the feed question is answered. Any change the build would make to the pull accounting (net vs gross) is a scoring change and needs a trader ruling first. This seat did not edit the queue row |
| The 1.000 point mass named in `D-3`'s re-grounding | Not fully partition-invariant on aligned pairs (3 of 12 stay). Too few rows to re-ground `D-3` on; record only |
| The absorption Stage 1 read (~2026-10-08) | Can now cite this result |

### Verified and not verified

- **Verified (run this session):**
  - Archive MD5 `7f9e2a5dd70da73c7929f0580e086ebd` matches the brief. The `out\` episodes and samples files are byte-identical to the archive copies.
  - Selftest PASS. A per-episode quarantine mutation goes red (`got L3,L4`, want `L4`).
  - The independent pairing reproduces the probe's own count, 1,443 A-B pairs before the quarantine.
  - The recomputed `pullFrac` matches the CSV column to 5e-7.
  - Episode exposure reproduces the ruling box's 89 and 164.
- **Not verified:**
  - Whether the VETO→PASS residual comes from the visibility mask. It is an inference from arm C ≈ arm B.
  - Whether the result holds outside this one 24 h window (one run, one day).
  - The 29 prior mismatches stay unclassified. The quarantine removes their windows; it does not explain them.
  - No sensitivity line with quarantined pairs was computed (none was registered).
