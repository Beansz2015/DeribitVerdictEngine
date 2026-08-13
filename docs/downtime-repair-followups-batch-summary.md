# Batch summary — DR-1 + DR-2 (downtime-repair follow-ups)

**Source brief:** [`downtime-repair-followups-implementer-briefs.md`](downtime-repair-followups-implementer-briefs.md) §1 (DR-1), §2 (DR-2). **Build under repair:** `c6c6942` (hole-derived repair windows, Part A). **This session carries DR-1 + DR-2 together, per the brief's own §0.1 split — DR-3 is a separate conversation and is NOT done here.**

**Model / effort used:** Sonnet, medium for both, matching the brief's own recommendation. Neither brief's escalation trigger fired — noted at the top per convention, since it changes how the rest of this summary reads: nothing here required a tier change.

**Status: code + fixtures + docs done, harness ALL PASS, `verify-gate.ps1 -Mode prepush` GATE PASSED. NOT committed** — this session does not commit per CLAUDE.md's "only commit when the user asks" rule; the user asked for implementation + spec-back, not a commit.

---

## What happened, per item

### DR-1 — remove the `MinHoleMs` width floor

- **Decision (§1.2):** ticked as recommended in the invoking instruction — remove entirely, no count floor. No decision remained open.
- `Core/TradeStoreWriter.vb`: deleted `Public Const MinHoleMs` and the `If clamped.WidthMs < MinHoleMs Then Continue For` filter in `ResolveRepairWindowsMs`. Kept the inverted-range drop (`e < s` after clamp — two rows one millisecond apart, an unfetchable venue window) and added a counter + log line for it, where before it was silent.
- `MaxHolesPerPass`'s doc comment lost its `<see cref="MinHoleMs"/>` reference (would have been a dangling cref).
- Fixtures: `A56b` part 4 repurposed (a sub-2000ms fetchable gap is now returned, not dropped); a new part 5 added inside the same fixture (the 1-ms-apart case, the one drop DR-1 keeps). `A56f`'s `widthOk` line (which read `MinHoleMs` to prove the cap, not the floor, produced the count) removed — `countOk` already covers that property directly against `MaxHolesPerPass`.

### DR-2 — `ScanForRepair`'s truncation cut is now time-contiguous

- `ScanForRepair` gained a `maxScanRows` parameter and moved `Private` → `Friend` (so the harness can drive a small cap without 500,000 rows); its backing `SeqPoint` struct moved `Private` → `Friend` to match — a `Friend` function cannot expose a `Private` type through its signature (this was a real compile error caught by the first build, not a hand-derivation — worth naming because it's exactly the kind of thing that's obvious in retrospect and easy to miss in the plan).
- Truncation logic: when the cap bites, sort the retained rows once by `(TsMs, Seq)` (not on every overflow — that would be `O(n log n)` per row), drop the oldest block **by time**, and remember the cut as `floorMs`. Any row read afterward with `TsMs < floorMs` is discarded as it arrives rather than being re-admitted and removed again later — exactly Route (a) from the brief's §2.3.
- New fixture `A56g`: 16 rows, fully sequence-contiguous (seq 0–15) when sorted by time but written out of order (an S-block then an R-block, the `A56c` shape), driven through the `Friend` overload with a cap of 10.

---

## Mutation proof — both fixes reverted in place and re-run, not just hand-argued

Per each brief's verification handle #1, and going one step further than a hand trace:

| Mutation | Result |
|---|---|
| DR-1 — reinstated a literal `2000L` width filter in the current `ResolveRepairWindowsMs` | `A56b` **FAILS**: `tiny=False(n=1)` — the fetchable sub-floor gap the fix is supposed to return came back as the tail alone, exactly the pre-fix shape |
| DR-2 — reverted `ScanForRepair`'s cut to file-position `RemoveRange`, no sort, no floor | `A56g` **FAILS**: `firstSeq=1(want 6) noPhantom=False` — matching the hand-traced prediction that the file-order cut strands the S-block's even-seq rows while every R-block row (always appended at the list's end) survives every cut |

Both mutations were then reverted back to the fix and the full harness re-run clean. No fixture needed rewriting to make either mutation fail — a good sign the fixtures test the property, not an accident of the current code shape.

---

## Gate tail

```
harness ALL PASS  (A1-A55g unregressed, A56a/c/d/e unregressed, A56b repurposed, A56f trimmed, A56g new)
display-parity:  OK  no snapshot/card drift detected
version-bump:    OK  engine path changed but [no-engine-change] token present   <- see spec-back §1, handle 2
result:          GATE PASSED
```

`OrderCheck` (Release) build: 0 warnings / 0 errors.

---

## Self-corrections made during construction (not shipped wrong, but worth naming)

The brief did not give exact expected window counts for the repurposed `A56b` part 4 — while drafting it, the first pass reused the OLD fixture's expectation (`wTiny.Count = 1`) out of habit. With the floor removed, the correct expectation is `Count = 2` (the hole window itself, then the tail) — caught before running, not after a false pass. Recorded because it is exactly the trap the spec-back convention exists to surface: a fixture that looks like it "still passes" is not the same as a fixture that tests the right thing.

---

## Not done this session

**DR-3** (`TotalRowsRepaired` over-reporting) is untouched — the brief scopes it to a separate conversation (different seam: `HistoricalStore`/`TradeStoreGapRepair`, not `TradeStoreWriter`) and the user's instruction here was DR-1 + DR-2 only.
