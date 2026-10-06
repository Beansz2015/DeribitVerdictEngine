# Raw order-book absorption test — results, 2026-10-06 (UTC)

**Spec:** [`raw-book-absorption-test-spec.md`](raw-book-absorption-test-spec.md). Full diagnosis: `raw-book-absorption-test-spec.md` §11. Run: 24 h, 2026-10-05 13:20:38 → 2026-10-06 13:20:38 UTC, temporary AWS instance, data in `C:\DeribitData\rawbook-run1\out\`.

## Verdict

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
