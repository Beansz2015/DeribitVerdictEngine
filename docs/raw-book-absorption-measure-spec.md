# Raw-book absorption measure on the collector — spec (no build)

**Status:** SPEC ONLY, 2026-10-06 (UTC) (2026-10-07 GMT+8). Build after 2026-11-25 (trader away 2026-10-14 → 2026-11-25). Start commit `472fc68`; resumed at `d0ed69d` after a usage-limit stop (the two commits between touch only `docs/outstanding.json`).
**Source ruling:** the RULED box at the top of [`raw-book-absorption-test-results-2026-10-06.md`](raw-book-absorption-test-results-2026-10-06.md) (do not reopen). Method: [`raw-book-absorption-test-spec.md`](raw-book-absorption-test-spec.md) and its probe `tools/RawBookProbe/`.
**Class:** RESERVED as a whole — it moves a rendered value (the TAPE-strip `ABS` tag), changes the `analysis_log.csv` header, adds a `settings.json` key and puts a credential on the live collector. Every decision in this spec's decision table (section 9 below) goes to the trader.

---

## 0. Build brief — model, effort, traps, escalation, sessions

**Model: Opus 5.5 · Effort: HIGH** for sessions B1 and B2. **Sonnet 5 · medium** for session B3 (gate run and read, a recipe once the rules exist).

- **Why that tier.** The judgement is done here, and the fold is reused unchanged. Three parts are still hard: a second authenticated WebSocket beside the tape-capture connection on a 1 GB box; an order-book rebuild whose correctness must be CHECKED live (`change_id` agreement); and a header rotation with a shadow tracker, which touches the CSV, the riders ledger and the parity rule in one commit.
- **Where it will slip.**
  1. **Touching the main feed.** The 100 ms book on the main connection feeds `MarketState.UpdateBook` → `GetBook` (run path `WsMarketDataSource.vb:73-74`, `ExitGuardEvaluator.vb:70`, `LiveMicrostructureEvaluator.vb:110`), `FoldOfiAverage`, and four staleness checks (`DeribitWsFeed.IsDegraded`, `UI/MainForm_ExitGuard.vb:144`, `UI/MainForm_LiveStrip.vb:192`, `WsMarketDataSource.vb:73`). **None of these moves.** Only the two absorption fold calls leave the main feed.
  2. **Re-implementing the tracker.** `Core/LevelAbsorptionTracker.vb` is not edited. The new code hands it `OrderBookSnapshot` top-10 objects and trades, exactly as the probe's arm B did. A diff to that file is a stop sign.
  3. **A fatal auth path.** The probe treats 3 auth failures as fatal and cancels the process (`RawBookProbeProgram.vb`, `HandleResponseAsync`). In the collector an auth failure must disable only the absorption connection. It must never touch the main feed or the process.
  4. **The two carried probe defects** (section 4.4 below): stale raw state across a reconnect, and counter-only mismatch logging.
  5. **The fixtures cannot catch a cross-connection ordering bug** (the implementer writes the fixtures). The G2 gate (section 5.3 below) is the only cover for it.
- **Escalation trigger.** STOP and report if any gate fails; if the build needs an edit to `Core/LevelAbsorptionTracker.vb`; or if the absorption connection's failure can be shown to reach the main feed's receive loop.
- **Session split (by dependency).**

| Session | Content | Model · effort |
|---|---|---|
| G1 (before any code) | Pre-build gate: twin-clone run with the existing probe (section 5.2 below) | Sonnet 5 · medium (recipe) |
| B1 | `Core/RawBookRebuilder.vb` (host-agnostic), `DeribitAbsorptionFeed.vb` (second connection), agreement check and mismatch records, credential loader, fixtures | Opus 5.5 · high |
| B2 | Settings key and version bump, CSV rotation with the shadow 100 ms tracker, `absorption_episodes.log` v2 line, display-parity check, riders ledger | Opus 5.5 · high |
| G2 + B3 | Post-build, pre-deploy gate on twin clones (section 5.3 below); read; deploy only on PASS and a trader go | Sonnet 5 · medium |

---

## 1. What the measured data says (read-only, measured 2026-10-06 UTC)

### 1.1 Message rates — the 24 h probe run

Source: `C:\DeribitData\rawbook-run1\out\console.log`, 1,439 one-minute status lines, 2026-10-05 13:20:38 → 2026-10-06 13:20:38 UTC. Method: per-minute differences of the cumulative counters (no counter went backwards across the one reconnect). Script: scratchpad `rates.py` (not committed).

| Channel | 24 h total | Mean /s | Median minute /s | p99 minute /s | Max minute /s |
|---|---:|---:|---:|---:|---:|
| `book.BTC-PERPETUAL.raw` | 3,424,188 | **39.6** | 35.4 | **79.9** | **89.3** (15:33:50 UTC) |
| `book.BTC-PERPETUAL.none.10.100ms` | 259,461 | 3.0 | 3.0 | 3.1 | 3.1 |
| `trades.BTC-PERPETUAL.raw` | 113,209 | 1.3 | 0.9 | 7.2 | 12.3 |
| `trades.BTC-PERPETUAL.100ms` (batches) | 43,781 | 0.5 | 0.4 | 1.9 | 2.6 |

- The raw book sends **13.2×** the 100 ms book's messages.
- By UTC hour, the raw book mean runs from 21.0/s (21:00) to 75.3/s (15:00). 13:00–15:00 is the peak.
- ⚠ **Per-SECOND peaks are NOT measured.** The probe counted per minute only. A one-second burst above 89/s is likely and unknown.
- ⚠ **Message SIZE is NOT measured.** The full-book snapshot held 1,444 bids and 675 asks at the first subscribe (1,361 / 697 after the reconnect), per `events_20261005-132038.log`. The probe's own comment puts a snapshot at ~100 KB; nobody measured it.

### 1.2 Probe memory (Linux t3.small, separate process, three trackers)

| Metric | Min | Median | Max |
|---|---:|---:|---:|
| GC heap | 6 MB | 10 MB | 19 MB |
| Private | 182 MB | 225 MB | 228 MB |
| Working set | 82 MB | 92 MB | 110 MB |

- **This does not size the in-process cost.** The probe is a self-contained process with its own runtime, three trackers, and CSV writers. The in-process marginal cost inside the collector is NOT measured. Section 5 measures it.

### 1.3 Collector box headroom — `tools/ops/collector-readback.ps1`, 2026-10-06 15:34 UTC

| Reading | Value |
|---|---|
| Instance | `i-0d6c133058876273e`, t2.micro, 1,024 MB, Windows Server 2019 ([`hostel-app-colocation-assessment.md`](hostel-app-colocation-assessment.md) section 1) |
| `MEM_FREE_MB` | **78** |
| `COMMIT_FREE_MB` / limit | 776 / 2,421 |
| App private / threads | 107 MB / 14, up since 2026-10-01 09:35 UTC |
| `Pages Output/sec` (3 × 2 s) | **mean 1,453.9** |
| `Pages Input/sec` | mean 6,015.8 — not usable; it counts mapped-file reads ([`trader-tick-queue-archive.md`](trader-tick-queue-archive.md), 2026-08-21 footprint row) |
| Disk free | 7.68 GB |
| Collection | rows advancing (sid 4829 at 15:34:00), watchdog OK, 0 run errors |
| Co-tenant | scheduled task `RedInnCourt-DynamicPricing-Hourly` (the hostel app) ran at 15:25:25 |

- ⚠ **The eviction reading is NOT clean.** The read-back's own PowerShell imports the 10.5 MB `analysis_log.csv` twice and holds it in `$csv` before it samples the counters. The deploy checklist (`aws-collector-deploy-checklist.md` section 5c) records the same observer effect and leaves such readings "neither alarmed nor cleared". This reading is in the same class.
- **CPU is NOT in the read-back.** Last measured 2026-08-22: `CPUCreditBalance` pinned at the 144 maximum, peak 52.5 % of one vCPU (`hostel-app-colocation-assessment.md` section 3). That is six weeks old and predates the hostel app and several builds.
- **Memory is the binding constraint, as in August.** Headroom was ~150–170 MB then (`hostel-app-colocation-assessment.md` section 10.1); today's single reading is 78 MB with the observer inside it.

**Escalation trigger check (the brief's).** It fires only if the MEASURED load says the t2.micro cannot carry the change. The data does not say that: CPU is unmeasured and the memory reading is observer-contaminated. **It does say the margin is thin and unknown.** So the build is gated on a clean measurement (section 5 below), not on this reading.

### 1.4 The switch moves EVERY `Absorption*` column, not only `pullLB`/`postLB` — measured

The ruling names the `pullLB`/`postLB` fold. Feeding the shipped tracker a raw-rebuilt book (the probe's method) also changes episode boundaries and the band trajectory. Measured on the run's `episodes_20261005-132038.csv` (non-`PROBE_` rows; last active read per episode; scratchpad `arms.py`, not committed):

| Statistic | Arm A (100 ms, today) | Arm B (raw book + raw trades) |
|---|---:|---:|
| Episodes in 24 h | 3,565 | 6,874 (×1.93) |
| Median episode life | 1.33 s | 0.30 s |
| `LadderSpanLost` closes | 1,560 | 4,159 (×2.7) |
| Episodes passing `min_aggr_usd` and `absorb_ratio` at last read | 279 | 241 |
| Episodes that would show `ABSORB_*` (adds the `max_pull_frac` veto) | **162** | **71** |

On the 1,443 aligned A-B pairs (same rule as `raw-book-absorption-test-spec.md` section 5, unquarantined):

- `SizeMin` is lower on B in 855 pairs and never higher (median −2,300 USD). The raw feed sees transient band dips.
- `absorbRatio` is lower on B in 482 pairs, higher in 243.
- The `ABSORB` classification goes A→B: 48 lose it, 11 gain it, 16 keep it.

⭐ **Consequence: the strip's `ABS` tag would show less than half as often** (162 → 71 per day on this run). This is a rendered-value change beyond the veto shift. It is what the probe measured, and decision `RBM-1` (section 9 below) asks the trader to confirm it with this number in view.
⚠ **Not verified:** whether one day generalises; whether 4,159 `LadderSpanLost` closes are top-10 window artefacts or real. The D-6d instrument in `absorption_episodes.log` will show it after deploy.

---

## 2. Design

### 2.1 Shape — a second, authenticated connection that feeds the absorption tracker only

```
Main connection (DeribitWsFeed, public, UNCHANGED channels)
  book.none.10.100ms ─► UpdateBook / FoldOfiAverage / staleness   (unchanged)
                     └► FoldAbsorptionBook ON THE SHADOW TRACKER   (section 2.4)
  trades.100ms ──────► AppendTrade / aggressor velocity / alerts / trade store (unchanged)
                     └► FoldAbsorptionTrade ON THE SHADOW TRACKER
Absorption connection (NEW DeribitAbsorptionFeed, authenticated, read-only key)
  book.BTC-PERPETUAL.raw ─► RawBookRebuilder ─► top 10 ─► FoldAbsorptionBook (PRIMARY tracker)
  trades.BTC-PERPETUAL.raw ─────────────────────────────► FoldAbsorptionTrade (PRIMARY tracker)
  book.BTC-PERPETUAL.none.10.100ms ─► agreement check only (section 2.3)
```

- **Primary tracker = probe arm B exactly**: the rebuilt top 10 plus raw trades, folded in arrival order on ONE socket, one receive loop. Book folds are stamped with receive time; trade folds with the venue stamp (as `DeribitWsFeed.vb` does today).
- **The tracker is unchanged.** `MarketState` gains a second `LevelAbsorptionTracker` field (the shadow) and routes by feed. Both are touched only under `MarketState`'s one `SyncLock`.
- **Fold cadence: every raw message folds**, as the probe did — also when the top 10 did not change. Skipping unchanged-top-10 folds would move fills between intervals, so the deployed measure would differ from the measured one.
- **Host-agnostic.** `Core/RawBookRebuilder.vb` and `DeribitAbsorptionFeed.vb` carry no WinForms reference (the Linux-port rule in `CLAUDE.md`).

### 2.2 The rebuild (`Core/RawBookRebuilder.vb`)

- Port of the probe's `OnBookRaw_Locked`, `ApplyRawSide` and `RawTop10` (`tools/RawBookProbe/RawBookProbeProgram.vb`). Bids in a descending `SortedDictionary(Of Double, Double)`, asks ascending. `snapshot` replaces; a change applies `new`/`change`/`delete`; amount ≤ 0 deletes.
- **Chain check:** a change whose `prev_change_id` is not the last applied `change_id` is a **raw gap**. The book becomes invalid, the primary tracker resets, and the feed unsubscribes and re-subscribes the raw channel for a fresh snapshot.
- **No folding while invalid.** Between a gap or reconnect and the next snapshot, the primary tracker receives nothing and reads IDLE.
- Raw sizes are USD on the inverse contract, the same unit as the 100 ms ladder and the trades (verified in the probe run: 0 exact mismatches).

### 2.3 The agreement check, with the two carried defects fixed

| Probe defect (`raw-book-absorption-test-spec.md`) | Fix in the build |
|---|---|
| Section 11.4: reconnect does not reset `_rawValid`, `_rawLastChange` or the change ring, so an early 100 ms snapshot of a new connection can be compared with a ~2 s stale book | On every absorption-connection (re)connect AND every raw gap: clear the ladder, `valid = False`, clear the change ring and the pending list, bump a `gen` counter. A 100 ms snapshot that arrives while `valid = False` counts as `unresolved`, never as a comparison |
| Section 11.1 / 11.6 (a): `Resolve_Locked` only increments a counter; no ids, no books | Every mismatch writes one record (below) |

- **Check rule (unchanged from the probe):** each 100 ms snapshot's top 10 is compared with the rebuilt top 10 at its `change_id` ("exact"), or at the latest raw change before it when no raw message carries that id ("prior"). A pending list holds 100 ms snapshots that arrive before their raw change.
- **Ring of rebuilt top-10 snapshots, not hashes:** `RingCapacity = 512` changes (~6–13 s at the measured 39.6–89.3/s) — `Public Const`, read by the fixture (the `CLAUDE.md` constant rule). The probe's 8,192 hashes covered ~200 s, far more than any pending wait.
- **Raw-message ring:** the last 512 raw change messages, kept as their `bids`/`asks` change arrays (not the parsed full book).
- **Mismatch record** (one JSONL line in `C:\DeribitEngine\rawbook_agreement.log`), written when the next raw change after the 100 ms `change_id` arrives, so it can name it: `utc`, `iid`, `gen`, `kind` (`exact`/`prior`), X (the 100 ms `change_id`), P (the prior raw `change_id` used), N (the next raw `change_id`), the 100 ms top 10, the rebuilt top 10 at P and at N, and the raw change arrays in (P, N]. This is exactly the set `raw-book-absorption-test-spec.md` section 11.1 lists as missing.
- **Bounds:** at most 200 records per UTC day and 8 MB per file, then one `SUPPRESSED n` line per hour (the `WsFeedLog` rate-limit precedent). Counters (`exact match/mismatch`, `prior match/mismatch`, `unresolved`, `dropped`, `gaps`, `gen`) go to one line per run in `absorption_episodes.log` v2 (section 3.3 below).
- **Action on a mismatch** (`RBM-5`): EXACT mismatch → record, then treat as a raw gap (resubscribe, reset). PRIOR mismatch → record only. The 2026-08-20 Deribit best-practices note says the raw channel may coalesce two or three changes under one notification ([`trader-tick-queue-archive.md`](trader-tick-queue-archive.md), 2026-08-20 WS channel row). That would produce prior mismatches with no rebuild error. The records will show whether it does. ⚠ Not verified.

### 2.4 The shadow 100 ms tracker

- The existing tracker keeps running on the main connection's 100 ms book and trades, as today. Its D8 numbers go to new shadow CSV columns (section 3.2 below). It renders nothing.
- Cost: today's fold cost, which the box already carries.
- Purpose: a continuous paired series of the old and new measure on the same rows. It bridges the dataset boundary, and it is data for re-deriving `max_pull_frac` on more than the one day the probe covered (`raw-book-absorption-test-results-2026-10-06.md`, "Not verified: whether the result holds outside this one 24 h window").

### 2.5 Resets — each tracker follows the connection that feeds it

| Event | Primary (raw) tracker | Shadow (100 ms) tracker |
|---|---|---|
| Main connection (re)connect (`SeedAsync`) | No reset — its inputs had no gap | `Reset()` as today |
| Absorption connection (re)connect, raw gap, exact mismatch | `Reset()`, then re-apply the last carried levels and geometry (`RBM-10`) | No reset |
| Analysis run carry (`SetAbsorptionLevels`, `UI/MainForm_Analysis.vb:755`) | Gets the levels | Gets the same levels, same call |

- `MarketState` keeps the last carried levels so a raw-only reset can re-apply them, as the probe did. A main reconnect still clears levels on the shadow, as today.

### 2.6 Fallback — the raw book is not available

| Cause | Primary tracker | Strip tag | CSV `AbsorptionBookFeed` |
|---|---|---|---|
| `book_feed` = `100ms` (rollback) | not created; the 100 ms tracker is primary, as today | as today | `100MS` |
| No key, key unreadable, or auth refused | IDLE | none | `RAW_NO_AUTH` |
| Connection down or raw book invalid (gap, waiting for snapshot) | IDLE | none | `RAW_DOWN` |
| Raw book live | measuring | as classified | `RAW` |

- `RBM-3` read: **no silent fallback to the 100 ms measure.** The tracker's own rule for a degenerate ladder is "no measurement is honest measurement" (`Core/LevelAbsorptionTracker.vb`, `FoldBook`). The shadow columns still carry the 100 ms values, so no data is lost.
- **Auth failure is never fatal to the process.** After 3 consecutive auth refusals the absorption connection stops retrying for 1 hour and logs `AUTH_REFUSED` (no secret, no response body — lengths only, the probe's rule). The main feed never sees it.
- **Staleness of the absorption connection** joins nothing in `IsDegraded`. The run's `WsHealth` stays the main feed's. The feed column carries the absorption connection's state.

### 2.7 Credentials on the box

- **Key:** a NEW, dedicated read-only key for the collector (`RBM-8`), not the probe key.
- **Storage (`RBM-7` read (a)):** a DPAPI-encrypted file, `CurrentUser` scope for the auto-logon account, at `C:\DeribitEngine\secrets\deribit-ro.dpapi`, NTFS ACL = that account plus SYSTEM. The app decrypts it at absorption-connection start.
- **Placement:** the operator runs a one-time setup tool interactively over RDP: `tools/ops/rawbook-key-setup.ps1`. It reads id and secret with a hidden prompt (`Read-Host -AsSecureString`) and writes the DPAPI blob. **The secret never appears on a command line, in an SSM parameter or document, in an S3 object, in the repo, or in a log.** The tool prints lengths only.
- **Never:** in `settings.json`, in an environment variable, in `collector.ps1 deploy`'s manifest, or in any fetch. `collector.ps1 fetch` must exclude `secrets\` (B2 adds the exclusion and a fixture-like check in `tools/checks/`).
- **Logging rule:** the auth request string is built, sent and dropped. It is never passed to `Log`, `WsFeedLog` or any exception message. B1 adds a grep check, the probe's handle `H-3` in `raw-book-absorption-test-spec.md` section 9, adapted to the new files.
- **Rotation / revocation:** the trader revokes the key on the venue; the app reads `RAW_NO_AUTH` on the next connect. Re-running the setup tool replaces the file.
- ⛔ **Precondition (`RBM-9`):** `HH-2`'s recorded text ([`history-host-and-raw-channel-read-2026-09-28.md`](history-host-and-raw-channel-read-2026-09-28.md) section 4) still says *"The collector box is still excluded."* The 2026-10-06 ruling agrees the subscription. The text must be updated by the trader to cover the collector and the gate clones BEFORE any key goes on either.

### 2.8 Receive loop

- Same skeleton as `DeribitWsFeed.ReceiveLoopAsync`, with a hard per-message cap of 4 MB (the main feed has none; the probe used 16 MB for a ~100 KB snapshot). Over the cap → drop the connection, `RAW_DOWN`, reconnect with backoff.
- `ws.Options.Proxy = Nothing` (the WPAD hang lesson, `DeribitWsFeed.vb:212`).
- Heartbeat 30 s, `public/test` replies, exponential backoff 2 s → 60 s, its own storm guard.

---

## 3. Settings, CSV and rendered impact

### 3.1 Settings — v69 → v70

| Key | Value | Notes |
|---|---|---|
| `indicators.absorption.book_feed` | `"raw"` | `"raw"` or `"100ms"`. Read at feed start; a change needs an app restart (the main feed's own rule, `DeribitWsFeed.vb` header). The rollback switch |

- Caps (`RingCapacity` 512, mismatch records 200/day, 8 MB/file, message cap 4 MB, auth hold-off 1 h) are `Public Const`, not settings: they are safety bounds, not tunable thresholds.
- **Tweaker:** `indicators.absorption.book_feed` joins the exact-match reject list with `enabled` and `scoring_enabled` (`tools/AutoTweaker/SettingsDiffApplier.vb`, HARD CONSTRAINT 23 — the tweaker rule named in `Core/Settings/EngineSettings.vb:522`). A feed choice is never a tweaker proposal.
- POCO default `"100ms"`. An absent key keeps today's behaviour, so an old `settings.json` cannot turn on a credentialed connection.
- `change_log` v70 entry: names this spec, the dataset boundary and the rollback.

### 3.2 `analysis_log.csv` — header rotation (`RBM-4` read (b))

- **The existing `Absorption*` columns change meaning**: from the 100 ms measure to the raw measure. All of them move (section 1.4 above), not only `AbsorptionPullLB`/`AbsorptionPostLB`/`AbsorptionPullFrac`.
- **Appended columns** (after the current last column; no column moves):

| Column | Content |
|---|---|
| `AbsorptionBookFeed` | `RAW` · `RAW_DOWN` · `RAW_NO_AUTH` · `100MS` (section 2.6 above) |
| `Absorption100Signal` | The shadow tracker's classification (`ABSORB_ABOVE` / `ABSORB_BELOW` / `NONE`) |
| `Absorption100PullLB` · `Absorption100PostLB` · `Absorption100PullFrac` | The shadow tracker's primary-episode D8 values |
| `Absorption100EpisodeSec` | The shadow episode's age — needed to pair the two measures |

- Rotation = a header change, so `AnalysisLogger.EnsureLogFile` renames the old book to `analysis_log.csv.124col-<h8>.<stamp>.bak` (`RIDER-1` mechanism, already shipped). The physical file edge IS the dataset boundary; `SettingsVersion` 70 marks it per row; `AbsorptionBookFeed` makes each row self-describing.
- **Riders:** the build reads [`csv-rotation-riders.md`](csv-rotation-riders.md) at B2 and carries every open row. Today only `RIDER-8` is open, and it is conditional (it does not ride). The B2 commit touches the ledger in the same commit (`tools/checks/rotation-riders.ps1` enforces it). This spec defers nothing to a later rotation, so it adds no ledger row now.
- `RIDER-2` tooling (`collector.ps1 fetch`, `kelly-trigger-read.ps1`) already pools every `.bak`. B2 checks it finds the new one (a handle, not an assumption).
- **Reads that span the edge** split on `SettingsVersion` ≥ 70 or `AbsorptionBookFeed` present. The absorption Stage 1 data and every pre-edge row are the 100 ms measure.

### 3.3 `absorption_episodes.log` — line v1 → v2

- Adds `feed=<RAW|RAW_DOWN|RAW_NO_AUTH|100MS>` and the agreement counters since the last run (`agree_exact=m/x prior=m/x unres=n drop=n gaps=n gen=n`).
- The D-6d close-reason tallies stay; they now describe the primary (raw) tracker. ⚠ Expect `LadderSpanLost` ×2.7 (section 1.4 above). That is the feed, not a regression.

### 3.4 Rendered surfaces — display-string parity

- The only absorption render is the TAPE strip tag `ABS↑ <level> (<ratio>×)` (`UI/MainForm_LiveStrip.vb:270-273`, read via `LiveMicrostructureEvaluator.vb:197-211`). It now reads the primary tracker. **Format unchanged; frequency drops** (~162 → ~71 episodes per day on the probe's day).
- `BuildPlaintextSnapshot` and the cards carry no absorption line (verified by grep: no `Absorption` in `UI/MainForm_PlaintextSnapshot.vb` or `UI/MainForm_Render_Cards.vb`). The B2 commit message states that no card surface is affected, as the parity rule requires.
- No verdict changes: `scoring_enabled: false` (`settings.json`, line 344).

---

## 4. Rollback

| Step | Effect |
|---|---|
| 1. Set `indicators.absorption.book_feed` to `"100ms"` and restart the app | The absorption connection is not opened. The 100 ms tracker is primary. Rows read `AbsorptionBookFeed=100MS`, so the edge is visible per row. No second rotation |
| 2. If the code itself must go: `git revert` the B1/B2 commits and redeploy | ⚠ A revert changes the header back: that is a SECOND rotation. Prefer step 1 |
| 3. Revoke the key on the venue; delete `C:\DeribitEngine\secrets\deribit-ro.dpapi` | Removes the credential from the box |

- ⛔ **What rollback cannot undo:** rows written under the raw measure stay raw (the scoring-class lesson in `CLAUDE.md`, applied to CSV meaning). The feed column makes them filterable forever.

---

## 5. Gates

### 5.1 Instruments (both gates)

- **Twin clones** of the collector: two temporary t2.micro instances from one fresh AMI of `i-0d6c133058876273e`, same region, run at the same time → same market, same Windows, same Defender.
- ⛔ **On both clones, disable the scheduled task `RedInnCourt-DynamicPricing-Hourly` before first boot completes** — a clone of the hostel app could push real prices. Add its measured marginal footprint (~20.5 MB, `hostel-app-colocation-assessment.md` section 10.1) to the memory margin instead.
- ⛔ **No S3 write and no write to production stores from a clone.** Data stays on the clone; collect by a read-only fetch, then terminate.
- **Counters:** a `logman` counter log created before the run, 15 s interval, written to the clone's disk. No PowerShell probe during the run (the observer effect in section 1.3 above). Counters: `\Processor(_Total)\% Processor Time`, `\Process(<app>)\% Processor Time`, `\Process(<app>)\Private Bytes`, `\Memory\Available MBytes`, `\Memory\Pages Output/sec`.
- **CPU credits:** CloudWatch `CPUCreditBalance` and `CPUUtilization`, 5-minute, read from the AWS API after the run.
- **Collection health:** each clone's `analysis_log.csv` rows per hour, `ws_health.log` DEGRADED lines, and a venue check of each clone's trade store (`CLEAN` expected).

### 5.2 G1 — pre-build gate (before any code is written)

- **T clone:** the collector as deployed, plus `tools/RawBookProbe` (win-x64 build) running beside it for 24 h with the read-only key. **K clone:** the collector only.
- The probe does MORE work than the build will (three trackers, CSV writers, its own runtime). So G1 is an upper bound on CPU. Its memory is a separate process, so G1 does not size the in-process memory cost; G2 does.
- **PASS (all must hold, pre-registered):**

| ID | Rule |
|---|---|
| G1-C1 | T box CPU 24 h mean − K box CPU 24 h mean ≤ 3.0 percentage points of one vCPU |
| G1-C2 | T box CPU 24 h mean < 10 % (the t2.micro credit-earn rate) |
| G1-C3 | T `CPUCreditBalance` never below 72 (half the 144 maximum) and ends ≥ start − 10 |
| G1-C4 | T p99 of 1-minute box CPU < 80 % |
| G1-K1 | T collector rows per hour ≥ K's − 1 in every hour; T has no DEGRADED line that K lacks; T venue check `CLEAN` |
| G1-S | Hard stop: T `Available MBytes` < 20 MB at any sample, or any process crash → FAIL |

- **FAIL → STOP.** The build does not start. Report to the trader. This spec does not redesign the box (resize or the Linux port are the trader's call).
- K's counter log is also the first observer-free headroom reading of the box since August. Record it whatever the outcome.

### 5.3 G2 — post-build, pre-deploy gate

- **T clone:** the built app, `book_feed = "raw"`, the new collector key. **K clone:** the same binary, `book_feed = "100ms"`. 24 h, same instruments.
- **PASS (all must hold):** G1-C1 to G1-C4 and G1-K1 on the built app, plus:

| ID | Rule |
|---|---|
| G2-M1 | T app `Private Bytes` p99 − K app p99 ≤ 25 MB |
| G2-M2 | T `Available MBytes` 1-minute minimum ≥ 50 MB |
| G2-M3 | T share of 15 s samples with `Pages Output/sec` > 0 ≤ K's share + 5 percentage points |
| G2-A1 | Agreement: exact mismatches = 0; every prior mismatch has a complete record (all fields in section 2.3 above) |
| G2-A2 | `AbsorptionBookFeed` = `RAW` on ≥ 99 % of T's rows; every other row names its cause |
| G2-R | Rollback rehearsal on T at the end: set `"100ms"`, restart; the next row reads `100MS` and no rotation happens |

- **Then deploy** with the normal deploy gate (`aws-collector-deploy-checklist.md`), only after the trader's go. The `HH-2` record (`RBM-9`) and the key placement come first.

---

## 6. Fixtures (planned; the tree assigns numbers at build — next free family today is `A95`)

| Planned | Asserts | Class (fixture-literal rule) |
|---|---|---|
| `A95a` | Rebuild: snapshot + change sequence → top 10 equals a hand-computed ladder; `delete` and amount 0 remove a level | MECHANISM |
| `A95b` | Chain break (`prev_change_id` ≠ last) → invalid, primary tracker reset, no fold until the next snapshot | MECHANISM |
| `A95c` | Reconnect resets ladder, ring and pending list; a 100 ms snapshot before the new raw snapshot counts `unresolved`, never `prior` (the section 11.4 defect). **Must fail on the probe's reset logic** — run that mutation once | MECHANISM |
| `A95d` | A prior mismatch writes one record with X, P, N, both top-10s and the (P, N] changes; an exact mismatch also triggers the gap path | MECHANISM |
| `A95e` | Parity: the primary tracker fed by the rebuilder equals a `LevelAbsorptionTracker` fed the same top-10 sequence directly (the tracker is reused, not changed) | MECHANISM |
| `A95f` | Routing: with `book_feed="raw"` the main feed folds only the shadow tracker; with `"100ms"` only one tracker exists and it is primary | SHIPPED BEHAVIOUR — reads `book_feed` from cfg |
| `A95g` | Ring size read from `RawBookRebuilder.RingCapacity` (`Public Const`), never restated | SHIPPED BEHAVIOUR |
| `A95h` | Header: new columns appended after the current last column; `.bak` named by `RotatedBakName` | SHIPPED BEHAVIOUR |
| `A95i` | Secret hygiene: an auth failure path writes no string containing the secret into `WsFeedLog` or `rawbook_agreement.log` | MECHANISM |

- ⚠ No fixture covers cross-connection ordering between the main and absorption sockets in production timing. G2 is the only cover.

---

## 7. Verification handles for the build's review packet (to be RUN, then pasted)

| ID | Kind | Handle | Confirms |
|---|---|---|---|
| `H-1` | runnable | `git diff <start>..<end> -- Core/LevelAbsorptionTracker.vb` prints nothing | The tracker is not edited |
| `H-2` | runnable | `git grep -n "FoldAbsorptionBook\|FoldAbsorptionTrade" -- DeribitWsFeed.vb DeribitAbsorptionFeed.vb MarketState.vb`, read each hit | Main feed folds only the shadow; absorption feed folds only the primary |
| `H-3` | runnable | `git grep -nE "client_secret\|ClientSecret" -- '*.vb' '*.ps1'`, read each hit | No write path emits the secret |
| `H-4` | runnable | the `A95a`–`A95i` harness run | Fixtures pass |
| `E-1` | evidence | the G2 counter logs and CloudWatch export | The box carries it |

---

## 8. What is NOT in this build

- **No `max_pull_frac` change** (ruled). Re-derived at absorption activation on raw-measured rows against outcomes.
- **No deeper ladder.** The rebuilt book has the full depth; the tracker still gets the top 10, as measured. Handing it more depth would change `LadderSpanLost` (the D-6d suspect close) and is a separate, unmeasured proposal — an option for the gated mechanism-revision build.
- **No change to the main feed's channels**, to OFI, to the trade store, or to scoring.

---

## 9. Decision table — `RBM-1` to `RBM-12`

ID prefix `RBM` (raw-book measure), checked free with `git grep -E "\bRBM-"` (0 hits) on 2026-10-06 UTC. Every row is part of a reserved build, so every row goes to the trader. "Label" is this seat's harness-6 label, written BEFORE the harness ran (section 10 below).

| ID | Question | Options | Read | Three-step test · label |
|---|---|---|---|---|
| `RBM-1` | Scope of the switch | (a) the whole tracker fed from the rebuilt top 10 + raw trades (probe arm B, measured): every `Absorption*` column moves; `ABS` tag ~162 → ~71 per day · (b) only the D8 accumulators from the raw stream; episode lifecycle and band trajectory stay on 100 ms (closest to the ruling's literal words; needs a tracker change; never measured) | **(a).** It is the configuration the read measured, and it changes the feed, not the tracker. ⚠ The tag-rate drop is material; the trader should see it before ruling | Step 1: (b) does not record or guarantee more; it is unmeasured · `no_richer_option` |
| `RBM-2` | Connection topology | (a) a second authenticated connection for absorption only (raw book, raw trades, 100 ms book for the check) · (b) authenticate the existing public connection and add the channels to it | **(a).** It isolates the tape-capture connection from auth and raw-channel faults, and keeps arm B's same-socket order for the tracker | Step 1: (a) guarantees more (isolation) and keeps measured order · `no_richer_option` |
| `RBM-3` | Raw book unavailable | (a) primary tracker IDLE, no tag, feed column names the cause · (b) silent fall back to the 100 ms fold · (c) fall back to the 100 ms fold, tagged by the feed column | **(a).** The shadow columns already record the 100 ms values, so (c) adds no data; it would make the strip tag mean two different measures with no visible mark | Step 3: the richer-looking (c) is mechanically wrong on the strip (one tag, two measures, no marker) and duplicates the shadow columns · `richer_option_wrong` |
| `RBM-4` | CSV and dataset boundary | (a) in-place meaning change, no header change, split on `SettingsVersion` only (the v61 precedent) · (b) header rotation: `Absorption*` become raw; append `AbsorptionBookFeed` and five `Absorption100*` shadow columns · (c) rotation with `AbsorptionBookFeed` only | **(b).** Each row says which measure it holds, and the paired series covers more than one day for the `max_pull_frac` re-derivation | Step 1: (b) records the most · `no_richer_option` |
| `RBM-5` | Action on an agreement mismatch | (a) exact → record + resubscribe + reset; prior → record only · (b) counters only (the probe as shipped) · (c) no check · (d) reset on prior mismatches too | **(a).** It is the carried-defect fix. (d) resets ~29 times a day on events that are most likely venue coalescing, and the exact check (0 of 156,319 mismatched) already bounds any persistent error to the ~0.55 s between exact checks | Step 3: (d) adds no guarantee the exact check does not already give, and destroys episodes · `richer_option_wrong` |
| `RBM-6` | Mismatch-record depth | (a) full record: X, P, N, both top-10s, the (P, N] change arrays, 512-entry rings, 200/day cap · (b) ids only · (c) unbounded | **(a).** It is the minimum set `raw-book-absorption-test-spec.md` section 11.1 says a diagnosis needs, with a byte cap for a 1 GB box | Step 3: (c) is forbidden by the byte-cap lesson for this host · `richer_option_wrong` |
| `RBM-7` | Credential storage on the box | (a) DPAPI file, auto-logon user scope, placed by an interactive RDP setup tool · (b) ACL-restricted plain env file (the probe's method) · (c) SSM Parameter Store SecureString fetched at app start via the instance role · (d) Windows Credential Manager | **(a).** Encrypted at rest, bound to one account, no new startup dependency. (c) keeps no secret on disk but makes feed start depend on an AWS API call on the path where a pre-request hang is unbounded | Step 2/3 boundary: (c) guarantees "no secret on disk"; my reason is a failure-mode argument, not only cost · `ambiguous` |
| `RBM-8` | Which key | (a) a new dedicated read-only key for the collector · (b) reuse the probe's key | **(a).** Separate revocation and audit; the queue row forbids running two authenticated probes at once, and the collector would hold its session permanently | Step 1: (a) guarantees more · `no_richer_option` |
| `RBM-9` | `HH-2` scope record | (a) the trader updates `HH-2`'s text to cover the collector and the gate clones before any key goes on them · (b) proceed on the 2026-10-06 ruling's wording | **(a).** `HH-2` says "the collector box is still excluded"; the `RBA-5` precedent recorded the scope change first | Step 1: (a) leaves the record true · `no_richer_option` |
| `RBM-10` | Levels after a raw-only reset | (a) re-apply the last carried levels and geometry at once (the probe's method) · (b) wait for the next analysis run to re-carry | **(a).** A raw gap does not age the levels; (b) loses up to a run interval of measurement per reset | Step 1: (a) records more · `no_richer_option` |
| `RBM-11` | How the feed is switched | (a) one `settings.json` key `indicators.absorption.book_feed`, read at feed start, tweaker-rejected; caps as `Public Const` · (b) a hot-reloadable key · (c) a compile-time constant | **(a).** A rollback without a rebuild, and no mid-`InstanceId` hot-reload edge on a credentialed connection | Step 3: (b)'s hot-reload lands a measure change mid-instance, the unfilterable-edge class `CLAUDE.md` reserves settings for · `richer_option_wrong` |
| `RBM-12` | Gate thresholds (section 5 above) | as written · other values | As written, pre-registered before any gate data exists | Step 1: no richer option; open to the trader before G1 · `no_richer_option` |

**One-line log of what this spec decided on its own (auto-proceed, all reversible by one revert of this doc):** fold on every raw message, not only on top-10 changes (probe parity — the alternative changes fill attribution) · agreement check uses the absorption connection's own 100 ms subscription, not the main feed's book (self-contained, same as the probe) · ring 512 entries (covers the measured 6–13 s; 8,192 covered ~200 s for no use) · the shadow tracker resets with the main connection, the primary with the absorption connection (each follows its own inputs).

---

## 10. Harness 6 — the decision-bias tripwire

Run 2026-10-06 20:28 UTC at rev `d0ed69d`, 5 samples, `jev-1.13.0`, AFTER the seat labels in `raw-book-absorption-measure-spec.md` section 9 were written to the baseline file. Files: `docs/harness-runs/decision-bias-20261006T2026Z-rbm-{population,baseline,jev}.json`.

| ID | Jev modal verdict | Agreement | Seat label |
|---|---|---:|---|
| `RBM-1` | `richer_option_wrong` | 0.8 (unstable) | `no_richer_option` |
| `RBM-2` | `richer_option_wrong` | 1.0 | `no_richer_option` |
| `RBM-3` | `richer_option_wrong` | 1.0 | same |
| `RBM-4` | `no_richer_option` | 1.0 | same |
| `RBM-5` | `richer_option_wrong` | 1.0 | same |
| `RBM-6` | ⛔ **`gives_up_for_economy`** | 1.0 | `richer_option_wrong` |
| `RBM-7` | ⛔ **`gives_up_for_economy`** | 1.0 | `ambiguous` |
| `RBM-8` | `richer_option_wrong` | 1.0 | `no_richer_option` |
| `RBM-9` | `richer_option_wrong` | 1.0 | `no_richer_option` |
| `RBM-10` | `richer_option_wrong` | 1.0 | `no_richer_option` |
| `RBM-11` | `richer_option_wrong` | 1.0 | same |
| `RBM-12` | `no_richer_option` | 1.0 | same |

- ⛔ **Two flags: `RBM-6` and `RBM-7`.** By the harness rule a flag sends the decision to the trader. Both go there anyway (the whole build is reserved); the flag means the trader should read them as possible economy picks, not as settled.
  - `RBM-6`: the richer option is the uncapped record. My case against it is the byte-cap rule for a 1 GB host. A middle path exists if the trader wants it: a higher cap (for example 1,000 records/day, 32 MB) sized after G2's memory reading.
  - `RBM-7`: the richer option is SSM Parameter Store (no secret on disk). My case is a failure mode (an AWS call on the feed-start path), which I labelled `ambiguous` myself. Lead with the trader's prior: if no-secret-on-disk is worth an IAM change and a bounded, timed-out fetch, (c) is the more truthful pick.
- The other disagreements are between the two "no trade" labels (`no_richer_option` vs `richer_option_wrong`) and do not change the action. `RBM-1` was unstable (0.8).

---

## 11. What this spec did not verify

- Per-second raw message peaks and message sizes (the probe recorded neither).
- The in-process memory and CPU cost of the rebuild on Windows / t2.micro — that is what G1 and G2 measure.
- Today's box eviction: the only reading (1,453.9 pages out/s) was taken by a probe that loads the CSV first.
- CPU headroom since 2026-08-22.
- Whether the 162 → 71 `ABSORB` drop and the ×2.7 `LadderSpanLost` hold beyond one day.
- Whether Deribit coalesces several raw changes under one `change_id` (the likely source of prior mismatches).
- That `collector.ps1 fetch` would copy a `secrets\` folder today — B2 must check, not assume.
- The hostel app's current footprint (the ~20.5 MB figure is from 2026-08-22).
