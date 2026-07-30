# Overlay D7 (REST-mode box) — answer, and the gap it exposes

**Seat:** Opus orchestrator, 2026-07-31. **Answering:** D7 in [`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md) §7, raised by the implementer after the review's §2.2 redraw removed the REST-box capability the first draft offered.
**Answer: (a) — tracked change with a version bump.** But (a) is adequate for a *narrow* reason worth recording, and D7 exposes a straddle that already exists in the live book and that no D7 answer touches.
**Class:** decision note. No code, no settings, no spec change here. The rider in §4 is a one-column CSV addition that rides an already-queued rotation and **must never force one**.

---

## 0. First — the implementer's §2.3 corrected this reviewer

The [overlay review](settings-local-overlay-review-2026-07-31.md) §2.4 put `ws_fallback_to_rest` and `ws_stale_after_sec` in the **ADMIT** column. They belong in **REJECT**, for exactly the reason §2.3 gives: they modulate connection health, connection health selects the data source through `ResolveSource`, and that is the same defect as `transport` reached one indirection later. Recorded here rather than quietly fixed, because it is the same failure shape the review was itself warning about — a fence justified by one property being assumed to imply another.

The `shadow_parity` reclassification is also the better call: it cannot move scoring (`ResolveSource` returns `_restSource` on the first branch whenever `transport ≠ "ws"`, before parity is consulted), but it constructs a `MarketState` and starts a WS feed — which since v64 also starts trade-store capture. Rejecting it on **side-effects** rather than on scoring is the precise reason, and precision matters because it becomes the precedent for the next key.

---

## 1. The answer: (a), and the framing understates it

**(a) is right, and "a real capability removed" is a slightly generous reading of what is being given up.** A REST-mode box is **break-glass, not routine** — you reach for it when a box's WS path is broken. A break-glass change *should* be deliberate and version-visible rather than silent. That is an argument **for** (a), not a cost of it.

- **(b) — admit `transport` anyway** knowingly accepts a straddle. Rejected.
- **(c) — admit it and exclude that box's rows** is the honest version of (b), and the implementer is right that it needs a pooling-side rule that does not exist. Inventing one inside this spec would be scope creep. But **the reason it does not exist is worth naming**, and §2 does that.

---

## 2. What D7 exposes — the straddle is already here

**The pooled book cannot currently distinguish a REST-scored row from a WS-scored row, by any mechanism.** Two verified facts:

1. **No column records it.** `analysis_log.csv` has 111 columns; none carries transport, WS health, effective source, or a degraded flag. The nearest thing is `InstanceId` (col 110), which identifies a *process*, not the source that scored a given run.
2. **A `transport: ws` box already emits REST-scored rows.** This is the implementer's own §2.3 finding: `ResolveSource` returns `_restSource` for a run whenever `IsDegraded()` is true and `ws_fallback_to_rest` is on. On those rows the time-averaged OFI silently reverts to snapshot OFI and the v52 aggressor-velocity TFI modifier never fires — *"a different scoring computation, by construction."*

Put together: **the straddle D7 is trying to prevent already exists in the live book**, produced by feed degradation rather than by configuration, unmarked, and at a rate nobody can currently measure. No answer to D7 — (a), (b) or (c) — changes that by one row.

That reframes (a). It is not "we are keeping the book clean by refusing the overlay." It is "the overlay is not where this leaks from, so refusing it costs little and gains little; the leak is elsewhere and is worth closing on its own."

---

## 3. Why (a) is nonetheless correct

Three reasons, in order of weight:

1. **Break-glass should be loud.** An emergency fallback that expresses itself silently in a gitignored file is the wrong shape for an emergency.
2. **The overlay's own §2 rule holds.** `transport` moves scoring; the whitelist admits only keys that cannot. Admitting one exception to a rule this new would make the rule advisory.
3. **The cost of (a) is small and bounded.** Flipping a box to REST becomes a tracked edit plus a version bump. The version bump is the feature, not the friction — it is what makes the divergence visible in the CSV's settings-version column, which is the only straddle-detection the book has today.

One hazard to record, since it is the mirror of the problem the overlay exists to solve: a tracked flip to REST **propagates to AWS on the next xcopy deploy**. The version bump makes it visible; the deploy checklist §1a is where it must be caught. (a) is correct *and* it puts a real obligation on the checklist.

---

## 4. The rider that makes (c) available later — one column, no forced rotation

**Log the effective data source per row.**

The value already exists and is already computed on every run:

```vb
' Core/SignalEmitter.vb:87
Public Shared Function DeriveWsHealth(transportIsWs As Boolean,
                                      degradedThisRun As Boolean,
                                      feedExists As Boolean,
                                      feedConnected As Boolean) As String
    If Not transportIsWs Then Return "REST"
    If degradedThisRun Then Return "DEGRADED"
    ...
```

It ships in the bridge payload (`health.ws`, enum-pinned in the frozen v1 contract) and **never reaches the CSV**. So this is a column addition, not new computation and not a new derivation.

**Sequencing — this is the load-bearing constraint.** It **rides the next natural header rotation and must never force one**, exactly the discipline `TriggerMode` is already parked under. Since `TriggerMode` is already queued for the next rotation, the marginal cost here is one additional field in a rotation that is going to happen anyway.

**What it buys, in order of value:**

1. **(c) becomes implementable without re-opening D7.** A REST box's rows — and a degraded WS box's rows — become filterable, so "exclude from the pooled book" stops being hypothetical. D7 turns from *capability closed* into *not yet, and here is precisely what would change it*.
2. **The degradation rate becomes measurable for the first time.** That matters independently of D7: #5 aggressor velocity and #6 absorption simply do not fire on those rows, so any study pooling them is diluting a signal by an unknown amount. `ws_health.log` records *transitions*, not per-run source selection, so it cannot answer this.
3. **It strengthens the coverage report's S1.** That signal currently *infers* app-up state from a transition-only log, with a permanently ambiguous trailing interval ([coverage review](trade-store-coverage-report-review-2026-07-31.md) C2). A per-row health stamp is a **positive record of state at run time** — strictly better than an inference, and it makes the two specs reinforce each other rather than merely coexist.

**Not proposed here:** the pooling rule itself, any change to how rows are selected for a study, or forcing a rotation. Those are separate and later.

---

## 5. Suggested D7 wording

> **(a) — tracked `settings.json` change with a version bump.** Adequate **because a REST box is break-glass, not routine**, and a break-glass change should be visible rather than silent; the version bump is the feature. **Not a permanent close:** (c) becomes available once rows carry their effective data source, which `SignalEmitter.DeriveWsHealth` already computes per run and which rides the next natural CSV rotation alongside `TriggerMode` — never forcing one. Recorded because the pooled book **already** cannot distinguish REST-scored rows: degraded-feed fallback (`ResolveSource` → `_restSource`) produces them today, unmarked and at an unmeasured rate, so this straddle predates the overlay and no D7 answer touches it. **Rider on (a):** a tracked flip to REST propagates to AWS on the next xcopy, so `aws-collector-deploy-checklist.md` §1a is where it must be caught.

---

## 6. What I did not verify

- **The degradation rate itself.** I established that `ResolveSource` can return `_restSource` on a WS box and that nothing records it; I did **not** measure how often it happens — that is precisely what is unmeasurable until the column exists. `ws_health.log` shows one `DEGRADED` transition in 32 lines over 2026-07-23→07-30 locally, but transitions are not per-run source selections and the two can diverge.
- **Whether the bridge payload's `health.ws` was ever `REST` or `DEGRADED` in practice.** The payload is a per-run file, not a series; nothing retains its history.
- **The CSV rotation's current contents.** I know `TriggerMode` is queued for the next natural rotation; I did not enumerate what else is, so "one more field in a rotation that is going to happen anyway" is an argument about marginal cost, not a claim about the rotation's size.
