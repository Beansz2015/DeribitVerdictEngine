# Venue-check build — summary (option A)

**Written:** 2026-09-14 (UTC) by the implementer seat. **Record for:** the trader. **Working packet for the orchestrator:** [`venue-check-build-spec-back.md`](venue-check-build-spec-back.md). **Brief:** [`venue-check-plan-review-2026-09-14.md`](venue-check-plan-review-2026-09-14.md).

⚠ **Top-placed finding:** the build added a fifth venue verdict, `VENUE_SHORT`. The sample-review table in `venue-check-plan-review-2026-09-14.md` §5 does not list it. A ruling is queued in the spec-back (R-1 there).

✅ **Rulings, 2026-09-14 (UTC):** all four spec-back rulings (R-1 to R-4 in `venue-check-build-spec-back.md` §2) took option (a). R-2 and R-3 were trader-ruled; R-1 and R-4 were orchestrator-agreed. R-1 was applied by the orchestrator in `8c65176`. R-2 (`missing_inside_seq_span`, fixture `A78f`) and R-3 (three more ledger columns) were built in a follow-up commit (see the git log). The ledger still holds zero real rows.

## Outcome per step

| Step (review brief §1) | Commit | Outcome |
|---|---|---|
| 1 — `venue_status.log` wiring | `dc02219` | ✅ Built. Fixture `A78a` failed first against a copied bug, then passed |
| 2 — `V-1` pagination · `V-4` flags, machine line, exit codes · `--venue-dump` · `tool_commit` | `6509a0f` | ✅ Built. Fixtures `A78b`–`A78e`, each failed under a named mutation, then passed |
| 3 — post-fetch hook | `9b6fd0b` | ✅ Built: `tools/ops/venue-check.ps1`, called by `collector.ps1 fetch`, `-SkipVenueCheck` opts out |
| 4 — plan updated for the ruling | `b441fab` | ✅ Done. Queue got commit hashes only |
| `V-2`, `V-3` | — | ⏸ Held, not built |

All commits local, tagged `[no-engine-change]`, **not pushed** (10 ahead of `origin/master` at `b441fab`).

## Gate and harness

- Harness at `b441fab`: `ALL PASS`, 381 checks, `A78a`–`A78e` included.
- `tools/checks/verify-gate.ps1`: `GATE PASSED` (run after step 1 and after step 2).
- No file in `Core/`, `DeribitVerdictEngine.vbproj`, `tools/BacktestRunner/HistoricalStore.vb` or `settings.json` changed between `fb780d8` and `b441fab`.

## Real hook run (scratch ledger, so the real ledger stays empty)

`tools/ops/venue-check.ps1 -FetchFolder aws_fetch/20260913-153704 -ToUtc 2026-09-13T15:00:00Z`:

```
"2026-09-14T16:25:58Z","20260913-153704","2026-09-13T02:00:00Z","2026-09-13T15:00:00Z","VENUE_SHORT","0","0","0","0","0","1","none","none","true","9b6fd0b0f753","6","venue history does not cover the window: 28713 store row(s) outside the venue span; 0 missing inside it"
```

- Expected: the window was ~38 h old, past Deribit's retention, so the venue returned `trades: []`.
- A missing-folder run wrote a `NOT_RUN` row, exit 2.
- `collector.ps1 fetch` itself was **not** run: it contacts the box.

## Venue facts verified live, 2026-09-14

- `has_more` is present on `get_last_trades_by_instrument_and_time`.
- `start_timestamp` is **inclusive** for this endpoint.
- A window ~30 h old returns `"trades":[],"has_more":false`.
