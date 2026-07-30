# Review — in-app trade-store capture build (v64)

**Reviewer:** the Opus orchestrator seat, 2026-07-31. **Reviewed:** the implementer's working-tree build of [`in-app-trade-store-capture-proposal.md`](in-app-trade-store-capture-proposal.md) (APPROVED, D1–D5 ticked).
**State at review:** the build landed as **`1229a30`** (local, unpushed) partway through this review — the first pass ran against the uncommitted tree, the second against the commit.
**Verdict: the build is sound and matches the approved spec.** Four findings, none blocking; the load-bearing one is a claim-strength issue about A48f that the implementer's own spec-back reads the other way.

> **Update after the commit landed.** Two findings resolved themselves and one was added.
> - **F4 resolved** — `docs/in-app-trade-store-capture-spec-back.md` shipped inside `1229a30`. It is a good packet; §5 is honest about the live-run gap and independently reaches my read on A48d ("tests the *decision* that guards them, not the fetch").
> - **F5 resolved** — re-running the gate post-commit makes both git-diff guards run substantively, and they pass: *display-parity → no snapshot/card drift*, *version-bump → **engine-path change accompanied by a settings.json version bump***. The caveat below was real for the pre-commit run and is closed for the committed state.
> - **F6 added** — surfaced by the implementer's own spec-back §4 and worth elevating, because it collides with a standing operational constraint.
>
> **F1 stands, and it is a disagreement, not an oversight.** Their spec-back §4 says *"A48f pins the stronger reading."* For the two-switch asymmetry it describes, that is correct. For the reversibility claim it is attached to, it is not — see §3 F1.

---

## 1. Checklist result

| # | Item | Result |
|---|---|---|
| 1 | `HistoricalStore` **split**, not moved; new host-agnostic network-free `Core/TradeStoreWriter.vb` | **PASS** — see §2 for the one nuance |
| 2 | Gap repair **fires once on start**, then on interval (§7.1, binding under AWS-only D1) | **PASS** |
| 3 | Monotonic last-written guard makes reconnect re-seed idempotent | **PASS** |
| 4 | Store path **exe-relative**, not cwd-relative (A48h) | **PASS** — and correct at the *call site*, not only the helper |
| 5 | Never-throws: a disk error logs and drops, feed and run unaffected | **PASS** |
| 6 | Buffered writes, **both** D2 triggers | **PASS** |
| 7 | HC27 fences the `trade_store.` **prefix** in `SettingsDiffApplier` + PromptBuilder rule 27 | **PASS** |
| 8 | Settings v63 → v64 + `change_log` entry + §15 row | **PASS** |
| 9 | Display-parity: no rendered surface ⇒ no obligation, **stated explicitly** | **PASS** (see §4) |
| 10 | `enabled:false` byte-identical to pre-build, harness-proven (A48f) | **PARTIAL — F1** |
| 11 | Fixtures A48a–h present and **meaningful**, incl. A48d exercising real overlap | **PASS** — A48d verified against the live call path |
| 12 | `verify-gate.ps1 -Mode prepush` ⇒ GATE PASSED, all builds Release | **PASS — with a caveat, F5** |
| 13 | A spec-back doc exists; nothing pushed | **spec-back ABSENT — F4**; nothing pushed ✅ |

Harness: **A48a–h all PASS**, **A1–A47b unregressed**. On the gate's two git-based guards, read F5 before treating them as evidence.

---

## 2. The split — done right, with one nuance the checklist overstates

`Core/TradeStoreWriter.vb` imports only `System.Collections.Generic`, `System.Globalization`, `System.IO`. **No networking of any kind.** It owns file naming, monthly rollover, the row format, the row *parse*, `LastTradeTimestamp`, the buffered append and the monotonic guard. `HistoricalStore` keeps `Backfill*Async` and now delegates **both** its trade append **and** `LoadTradeRange`'s per-file read to the writer — so writer and reader genuinely cannot drift. That is the `ComputeSideLevels` "one seam, no copies" move applied correctly.

**The nuance:** the brief's checklist says *"verify no HttpClient reached either."* Two corrections to that framing, neither of which is a defect in the build:

- **The app does gain an `HttpClient`.** The root `.vbproj` now links `tools\BacktestRunner\HistoricalStore.vb` (+ the hoisted `BacktestFundingSample.vb`). That is **spec-sanctioned**: §2 says in terms that gap repair "is the one place the app takes a dependency on the runner's networking, and is why D5 exists" — and D5 was ruled **Yes**. What the split protects is the **feed path**, and `ApplyTrades` touches only `TradeStoreWriter`. That holds.
- **The fixture project already linked `HistoricalStore` before this build** — it is pre-existing context in the diff, not an added line. So "no HttpClient in the fixture project" was already untrue pre-build and this build did not change it. What the build *did* do is add `TradeStoreWriter` to OrderCheck and deliberately keep `TradeStoreGapRepair` out.

Worth fixing in the checklist rather than in the code.

`HistoricalStore`'s `HttpClient` is a `Shared ReadOnly` field behind an explicit `Shared Sub New()`, so it is constructed on first static access — i.e. on the first repair pass, not at app start. Benign.

---

## 3. Findings

### F1 — moderate. A48f proves less than the claim that cites it.

The `change_log` and the §15 row both say: *"`enabled:false` ⇒ the fold early-outs and nothing is written, byte-identical to pre-build (harness-proven, A48f)."*

A48f does **not** exercise `ApplyTrades`. It re-states the feed's gate predicate inline —

```vb
Dim off As New TradeStoreSettings() With {.Enabled = False, .StoreDir = dir}
Dim gateOpen As Boolean = off IsNot Nothing AndAlso off.Enabled
```

— and asserts the restated copy is false, then that no file appeared. `TradeStoreGapRepairGate` does the same for `RepairOnceAsync`'s first guard, and the fixture's own comment says so. If `ResolveTradeStore`'s gate were later changed — say the `Not ts.Enabled` arm dropped — **A48f would still pass**.

This is the A43f shape the brief warned about: internal consistency of a re-statement rather than the production path. It is milder than A43f (a mirrored boolean is far easier to keep honest than a unit), and the constraint is real — `ApplyTrades` is private on `DeribitWsFeed`, which owns a `ClientWebSocket` the harness deliberately does not link (the A22/A37 boundary).

**What A48f does earn:** it pins the shipped POCO defaults (genuinely useful — an absent block resolving wrong is a live failure mode) and that the two repair switches are independent.

**Proportionate fix, ~10 lines:** hoist the gate onto the seam the build already established — `TradeStoreWriter.ShouldCapture(ts)` — and have both `ResolveTradeStore` and A48f call it. Then the fixture tests the production decision, and the claim becomes true as written. Failing that, soften the wording: the fold's inertness is *reasoned*, not harness-proven.

### F2 — low. `ResetBufferState` drops trades in a narrow race.

```vb
Public Sub ResetBufferState()
    Flush()                       ' takes and RELEASES _pending
    SyncLock _pending
        _pending.Clear()          ' <- anything buffered in between dies here
        _lastTs = -1
        _seeded = False
    End SyncLock
End Sub
```

A trade buffered by `ApplyTrades` between the `Flush()` and the `SyncLock` is silently discarded. Impact is bounded — a handful of trades at reconnect, which gap repair recovers, and the design already tolerates loss there — but the second `Clear()` is what makes it *lossy* rather than merely racy; without it those trades would simply flush on the next tick. `Monitor` is re-entrant, so wrapping the whole body in one `SyncLock _pending` closes it with no deadlock risk.

### F3 — low / cosmetic. Gap repair calls Deribit as `DeribitBacktestRunner/1.0`.

`HistoricalStore`'s shared constructor sets that User-Agent. Post-build the **live collector** makes venue calls under the backtester's identity. Harmless functionally; only matters if venue-side attribution is ever used to reason about the collector's behaviour.

### F5 — RESOLVED post-commit, but the mechanism is worth keeping.

`verify-gate.ps1` in `prepush` mode derives its changed-file set from **committed** history:

```powershell
$changed = @(& git diff --name-only "$base" HEAD)
```

The entire trade-store build is in the **working tree**, so `$changed` does not contain `Core/TradeStoreWriter.vb`, `Core/Settings/EngineSettings.vb`, `UI/MainForm_Layout.vb` or `settings.json`. Consequently:

- **`display-parity` → "no snapshot/card drift detected"** did not inspect this build at all.
- **`version-bump` → "no engine-path change"** likewise — it never saw `Core/` change, so it never looked for the version bump.

**Both conclusions happen to be correct** and I verified them directly rather than through the gate: the only UI file touched is `UI/MainForm_Layout.vb` (gap-repair start/stop — no snapshot line, no card binding), and `settings.json` genuinely carries v63 → v64 with a `change_log` entry and a §15 row. So this is not a hidden defect.

What it means: **"GATE PASSED" pre-commit is a weaker claim than "GATE PASSED" post-commit.** The load-bearing halves — the Release builds of all six projects and the A1–A48h harness run — execute the working tree and are fully genuine. The two git-diff guards are not. The `change_log` acceptance line ("verify-gate `prepush` GATE PASSED") will be true in the ordinary sense once the work is committed; **re-run the gate after committing** so the parity and version guards actually see the diff they exist to police.

**Closed.** Re-running the gate against `1229a30` makes both guards run substantively and both pass:

```
=== display-parity ===   OK  no snapshot/card drift detected
=== version-bump ===     OK  engine-path change accompanied by a settings.json version bump
=== result ===           GATE PASSED
```

Generalizable beyond this build, and the reason to keep the finding: **any lane that runs the gate before committing gets the same two blind spots**, and the gate reports `GATE PASSED` either way. The acceptance line in a `change_log` should mean the post-commit run.

### F6 — moderate (operational, not code). `enabled` ships `true`, so the **local collector captures too** — which is the topology D1 ruled out.

The implementer's spec-back §4 raises this and calls it harmless. It is harmless *for the store*; it is less harmless in context. D1 ruled **AWS-only**, and the spec §5 says to *"ship with `enabled` defaulting appropriately for a single capturing box."* The shipped default is `true`, which is right on AWS — but **this repo's `bin\Debug` build is the live local collector**, running 24/7 alongside AWS. Post-build it will also capture, into `<exe>\backtest_data\` under `bin\Debug\net8.0-windows\`.

Consequences, in order of how much they matter:

1. **The dual-capture topology D1 rejected is now the de-facto state**, by default, on the box the ruling was about. Not dangerous — exe-relative resolution (D3) keeps those files out of the repo's analysis store, and §5 anticipates that each store is its own — but it is the opposite of what the ruling said the build was for.
2. **~900 MB/year of silent disk growth on the local box**, in a directory nobody is watching, from a mechanism whose out-of-scope §9 explicitly defers retention.
3. A future seat finding capture files under `bin\Debug\` has to reconstruct why they are there.

**Not a code change — a settings call for the trader:** set `trade_store.enabled: false` in the **local** box's `settings.json` (the AWS deployment keeps `true`), or accept dual capture explicitly and record that D1 was softened in practice. Worth a line in `aws-collector-deploy-checklist.md` beside the §7.1 mtime rider, since that is where the capture-ops knowledge is accumulating.

### F4 — RESOLVED. Spec-back now exists.

At first-pass review the work existed only in the working tree and `docs/` contained no `*trade-store*spec-back*`. Both are closed: `1229a30` carries `docs/in-app-trade-store-capture-spec-back.md` along with the code, plus `docs/architecture.md` updates I had not seen in the working tree. Recorded rather than deleted so the sequence is legible.

---

## 4. What I checked rather than took on trust

- **A48d is not a tautology.** It exercises `TradeStoreWriter.ResolveResumeCursorMs` across four arms — covered window ⇒ cursor `-1`; a real gap ⇒ resume at `last+1`; the retention clamp pulling a stale cursor forward to `segStart` (and the unclamped historical path still filling the hole); and a **forced double-write** followed by a read-back proving the reader dedups (raw 10 rows → 5 deduped). I then confirmed in the diff that `BackfillTradeMonthAsync` **genuinely calls** `ResolveResumeCursorMs` rather than duplicating the arithmetic — the fixture's comment claims this, and the claim is true.
- **The row format is right against ground truth, not just against the fixture's expected string.** A48a compares to a hand-written literal, which is the A43f pattern in miniature. I checked the writer's `HeaderLine` and `FormatRow` output against a **real store file**: header `Timestamp,Price,Amount,Direction,Liquidation` and a real row `1785321796089,64615.50,1990.00,sell,none` — both match `F2`/`F2` and the `"none"` default. Cheap improvement for a future pass: assert the constant against the shipped store file so the pin is against reality.
- **Exe-relative resolution is at the call site.** `ResolveTradeStore` calls `TradeStoreWriter.ResolveStoreDir(ts.StoreDir)` before constructing the writer, so A48h is not pinning a helper that production bypasses.
- **§7.1 "once on start" is real.** `New Timer(AddressOf OnTick, Nothing, 0, periodMs)` — `dueTime` 0. `MainForm_Layout` constructs and `Start()`s it, **independent of transport**, and `Stop()`s it on close.
- **Streaming capture is actually the live path.** `network.transport` is `"ws"`, so `ApplyTrades` runs and the primary mechanism is live rather than silently degrading to repair-only.
- **The D2 count trigger is wired**, not just the timer: `store.PendingCount >= Math.Max(1, ts.FlushTradeCount)` after each batch fold. The choice of a real timer over an inline elapsed-time check is well-reasoned in the comment — a quiet hour is exactly when no batch arrives to run such a check.
- **`Stop()` flushes the tail**, so a clean shutdown loses nothing.

## 5. What I did not verify

- **Anything requiring the running app.** No live capture was observed: no store file was written by the engine during this review, so end-to-end "trades actually land on disk from the WS stream" is unproven here. That is the trader's test gate.
- **AWS behaviour.** The exe-relative store path, the once-on-start repair after a real restart, and the D1 single-box topology are all reasoned and fixture-backed but not observed on the deployed box.
- **Gap-repair network behaviour.** `BackfillTradeMonthAsync`'s paging, the retention refusal past ~24 h, and the `clampToSegStart` benefit under a real outage are covered only at the cursor-arithmetic level (A48d). No live HTTP call was made.
- **Long-run buffer behaviour** — memory growth across a multi-day uptime at ~60k trades/day, and month-rollover under a live stream rather than a synthetic straddling batch.
