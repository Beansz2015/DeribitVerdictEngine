# AWS Supplementary Collector — Deploy Checklist

**Date:** 2026-07-23 (trader-directed). **Role:** SUPPLEMENTARY session-coverage collector — primarily ASIA/LONDON, where the local book is thin (344 ASIA×3 rows vs 4,417 NY×1). **The local bin\Debug book stays CANONICAL**; the AWS book pools into reads by concatenation with `InstanceId` provenance. Accelerates the coverage-bound gates (res-3 §5.2, W6-1 LONDON depth, F1 STRONG accumulation, W6-4 book depth); does NOT accelerate calendar-bound gates (A5's 30 distinct days, #6's dates, the funding calm-week).

## 1. Deploy (xcopy — there is no installer)

1. On the local machine, from the PUSHED tree (never an unpushed build): `dotnet build -c Release` and take the whole `bin\Release\net8.0-windows\` output.
2. **Include `fonts\`** (Geist Mono OFL licence file travels as Content — CLAUDE.md bundled-fonts rule).
3. Confirm the copied `settings.json` is the tracked v59+ file — spot-check line 1 (`"version"`), `auto_run.trigger_mode: "on_close"` (v57 seeds it correctly), and **`trade_store.enabled: true`** (v64 seeds it correctly — see §1a).
4. **Do NOT copy `analysis_log.csv`, `analysis_eval_cache.csv`, or any `.bak`/sidecars** — the AWS book starts EMPTY on purpose (a seeded copy forks the history and forces dedup at every pooled read; a fresh book concatenates cleanly).
5. `signal_bridge.enabled` may stay as-tracked — emission to `C:\Dev\DeribitBridge\` on a box with no consumer is harmless (payloads simply overwrite). ARM stays OFF by construction (never persisted).

## 1a. The one setting the two boxes must NOT share — `trade_store.enabled` (v64, trader-ruled 2026-07-31)

| Box | `trade_store.enabled` | Why |
|---|---|---|
| **AWS** | **`true`** | D1 ruled capture **AWS-only**. This box is the sole capturer of raw tape, and tape past ~24 h is unobtainable at any price. |
| **Local — EVERY output folder, `bin\Debug` *and* `bin\Release`** | **`false`** | The local box is intermittent (trader, 2026-07-31), so its store would be a partial book nobody reads, growing ~1.4 GB/year in a directory nobody watches. Capture there also recreates the dual-box topology D1 explicitly rejected. **The "and `bin\Release`" is not pedantry — see point 3 below.** |

**The tracked `settings.json` carries `true`, and that is deliberate — do not "fix" it.** §1.1 deploys AWS from the Release build output, so the tracked value *is* what lands on AWS. The two failure directions are not symmetric:

- Tracked `false`, AWS not corrected after a deploy ⇒ **the only capturing box silently stops. Tape is lost permanently.**
- Tracked `true`, local not corrected after a rebuild ⇒ local captures again. Costs disk. Nothing is lost.

So the tracked seed carries the value that is *safe when it propagates*, and the **local box is the exception that gets edited by hand** — the mirror of the v57 stomp-proofing decision, pointing the other way because here it is AWS, not local, that must never be stomped.

**Applying it locally — the overlay, since 2026-08-02.** The hand-edit chore below is **retired**. `bin\Debug\net8.0-windows\settings.local.json` holds

```json
{"trade_store": {"enabled": false}}
```

and `SettingsLoader` deep-merges it over `settings.json` at load. The file is gitignored and is not a project item, so **no build copies over it** — `PreserveNewest` can refresh the tracked settings and the merge still resolves capture to `false`. Two things to know:

1. **Place it BEFORE the first build that carries a newer tracked `settings.json`**, not after. Order is the whole point; get it backwards once and the build that was meant to be protected is the one that captures.
2. **`dotnet clean` deletes it along with the rest of `bin\`, and that failure direction is the bad one** — losing the overlay silently switches capture back **on**. §3's `+local` glance is what makes its absence visible.
3. ⚠ **ONE OVERLAY PER OUTPUT FOLDER. `bin\Debug` and `bin\Release` are separate boxes as far as this file is concerned** — added 2026-08-05 after it bit. The overlay lives in `bin\`, is gitignored, and is **not** a project item, which is exactly what stops a build from clobbering it — and equally what stops a build from ever *creating* it in a second folder. From 2026-08-02 to 2026-08-05 only `bin\Debug` had one.

> **What that cost, recorded because the number makes the point:** the local `bin\Release` build ran three times (2026-08-04 17:44, 2026-08-05 12:59 and 13:13 UTC) and **captured 78,798 trades / 3.2 MB from 2026-08-03 21:44 UTC onward**, into `bin\Release\net8.0-windows\backtest_data\`. Three `capture_marker.log` lines now record `enabled=True` for this box under instance ids `b4369c8b`, `f91e8f68`, `74af27ff` — **the marker was correct; it faithfully recorded a state that should not have existed.** Nothing was corrupted and nothing pooled (that store is in a folder no analysis reads), and the tape was kept rather than deleted.
>
> **Nobody noticed for two days because nobody runs Release locally** — it exists to be xcopied to AWS. It surfaced only when the trader ran it to visually verify the C1 TAPE STORE strip, and the strip *appeared* — which is itself the tell: on a correctly-overlaid local box that element is hidden by design (`trade-store-coverage-report-spec-back.md` §5.2.3). **A visible TAPE STORE row on a local build means capture is on and the overlay is missing.**
>
> **§1a's own asymmetry reasoning held up exactly as written** — "tracked `true`, local not corrected ⇒ local captures again. Costs disk. Nothing is lost." That is precisely what happened, in the direction the analysis called safe. **The defect was never the tracked value or the failure-direction call; it was scoping the remedy to one output folder.** A fresh clone, a `dotnet clean`, or a first-ever Release build reopens it the same way.

*(Superseded, kept for the record: the old chore was "build Debug first, then set `"enabled": false` in `bin\Debug\net8.0-windows\settings.json`, and re-apply after every settings-version bump." Its failure mode — silently restored on the next bump, with no symptom until someone noticed a growing directory — is what the overlay exists to remove. Spec: [`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md).)*

## 2. Run 24/7 (WinForms — needs an interactive session)

- Auto-logon enabled + a Startup-folder shortcut to the exe (survives the reboots Windows Update WILL force; defer updates where the AMI allows).
- On the app: set auto-run REPEAT + ON-CLOSE, start it, then **disconnect RDP — do not log off** (logoff kills the GUI session).
- No crash watchdog exists: a crash stops collection until someone RDPs in. The WS feed reconnects itself; app death does not.

## 3. Daily one-glance health check (RDP in, ~30 seconds)

- **Title bar reads `settings v{N} +local`** (local box only — AWS must NOT show it) → the per-box overlay is in force, so `trade_store.enabled` is `false` here and capture is where D1 put it. **Its absence is the alarm**, and it is a *quiet* alarm: `bin\settings.local.json` does not survive `dotnet clean`, and losing it silently switches local capture back on — the state §1a rules against. On AWS the same glance is inverted: a `+local` there means an overlay it should not have. The marker only appears when the overlay actually overrode a key the base carries, so it cannot be earned by a typo'd or rejected key ([F1](settings-local-overlay-spec-back.md), 2026-08-02).
- TAPE strip alive + `[B]` strip populated → collecting.
- `ws_health.log` tail → any DOWN/DEGRADED transitions overnight (transitions-only, so a short file is a healthy file).
- **`liq_events.log` existence** → the AWS box runs the A4 cascade instrument 24/7 too — it may catch the first cascade before the local box does. A CASCADE line here counts as the A4 gate evidence (pool both boxes' sidecars).
- **[v64] `backtest_data\` newest-file mtime is advancing** → capture is alive. **This is the item on this list that now carries real data risk.** Under D1 there is no second capturing box, so this glance is the only thing standing between an unnoticed app death and permanently lost tape — trades older than ~24 h cannot be refetched, by anyone, at any price. A stale mtime with the app otherwise healthy points at `trade_store.enabled` having been reset to `false` by a redeploy (§1a) or at the store path being unwritable. Everything else on this list is recoverable; this one is not.

## 4. Copy-back / pooled-read recipe (at analysis time)

1. Copy the AWS `analysis_log.csv` back as `analysis_log_aws.csv` (never overwrite the local file).
2. Concat for pooled reads: local CSV + AWS CSV minus its header line (both v0.8/111-col schema — verify header equality first; if a rotation happened on one box only, DO NOT pool until both are on the same schema).
3. Provenance: `InstanceId` distinguishes boxes; the standing exclusions apply as always (weekday-only, burst instances).
3b. **Cross-box dedup — RE-RULED 2026-07-31 (trader): AWS-PREFERRED, per MINUTE-KEY.** Both boxes fire on the same bar closes, so overlapping rows are near-duplicate observations and **pooled STATISTICAL reads must not double-count them**. Rule: **key each row by its timestamp floored to the minute; where both books have that key, take the AWS row; local fills the keys AWS missed.** Applied at pooled-snapshot construction (the concat step above), so **no tool changes** — CeilingAudit / report / what-if consume the deduped snapshot as an ordinary CSV. Coverage-map reads (row counts per box) and single-box reads are unaffected.

> ⚠ **This supersedes the 2026-07-29 rule on BOTH axes, and the second change is easy to miss.**
> **Preference flipped** local→AWS: AWS is the canonical 24/7 collector and the D1 end-state topology, while the local box is an opportunistic addendum that runs only while the trader is at the desk (measured: local logged 567/392/268 rows on 07-28/29/30 against AWS's 921/921/914).
> **Granularity changed** session-hour→**minute-key**. The old formulation — *"for any (UTC session-hour) where the local book has rows, use ONLY local rows"* — discards every AWS row in an hour where local produced even one, which throws away coverage the pooling exists to gain. Minute-key dedup removes exactly the duplicate observation and keeps the rest.
> **Both boxes fire 1–10 s after the close, so a bar never straddles two minute-keys** — verified on both books before this was adopted.
> **Measured impact:** on the 2,159 minute-keys present in both books the two boxes **disagree on verdict 4.49 %** of the time (97 bars), so the preference is not cosmetic — but it moves the F1 STRONG count by 2 and the W6-1 LONDON count by 2, i.e. no gate conclusion turns on it.
> **Reproducibility note:** every pooled figure published from 2026-07-31 onward — F1's 203 evaluable STRONG, the W6-1 grids, the W6-4 ceiling audit, the ASIA burst derivation — was built on **this** method. A snapshot built the old way will not reproduce them.
4. Eval caches do NOT pool (each box's tracker walks its own book); offline re-walks via the report/what-if tools regenerate outcomes from the pooled CSV + fresh OHLC — the more trustworthy surface anyway (§7a).
5. **Same-settings discipline:** rows are only poolable while both boxes run the same settings version. After any settings bump locally, redeploy to AWS at the next opportunity.

> ⚠ **CORRECTED 2026-08-02 — this item claimed a column that does not exist.** The old text read *"the CSV's settings-version column + `InstanceId` make any straddle visible and filterable."* **There is no settings-version column.** Verified in the tree: the header constant in `AnalysisLogger.vb` runs `Timestamp` → `SignalId`, and the only attribution fields are **`InstanceId,SignalId`** (the §5 v0.8 rotation added those two and nothing else). So a version straddle is **NOT filterable from the data.**
>
> **It is worse than a two-box problem.** `settings.json` is hot-reloaded by `FileSystemWatcher`, so a version change can land **mid-`InstanceId`** — same instance, different scoring, one undifferentiated row stream, no marker anywhere. `InstanceId` is minted per *process start*, not per settings version, so it does not track this on its own.
>
> **Discipline that actually works, until a version column ships:** **make every settings-version change coincide with a process restart** — stop the collector, swap settings, start it. That mints a fresh `InstanceId` at the version edge and makes it a usable proxy for the straddle. Then **deploy both boxes close together** to keep the mixed window short.
>
> **This makes §5's `InstanceId` ledger load-bearing, not a nicety.** The version↔instance mapping exists *only* there (`0efcda74…` = v63, `5a3afd99…` = v64). If it is not recorded at each deploy, the straddle becomes unreconstructable — the data cannot answer it.
>
> **⚠ This matters most at a scoring boundary.** For a display-only or capture-only bump a straddle is harmless. For **v65/D3** it is not: armed and unarmed ASIA rows are byte-identical in shape, so a mixed fleet silently contaminates the D3 watch's own numerator. Get both boxes onto v65 before reading that watch.
>
> Filed as a rider on the next CSV header rotation — see [`trader-tick-queue.md`](trader-tick-queue.md) §3, alongside `TriggerMode` and the J-E effective-source stamp, which share the same "rows cannot be attributed" shape.

## 4a. Tape retention — **RULED 2026-08-05 (trader)**

> **Keep all tape unless it is a copy of tape already held.** Deleting is the only irreversible option, and tape past Deribit's ~24 h window cannot be re-fetched at any price. Duplicate rows are cheap: the store's read path whole-row-dedups, and the pooled recipe (§4.3b) is AWS-preferred per minute-key, so an overlapping book costs disk and nothing else.

**The practical order is therefore: merge first, judge duplication after** — never discard a store because it *looks* redundant. A book can only be shown redundant by comparing it against the one that supersedes it, and that comparison needs both books in hand.

**RESOLVED 2026-08-07 — and the rule paid for itself.** `bin\Release\net8.0-windows\backtest_data\trades_2026-08.csv` (78,798 rows / 76,354 unique, 2026-08-03 21:44 → 2026-08-05 13:13 UTC), captured by the un-overlaid local Release build (§1a point 3), was compared whole-row against the first AWS store copy-back. It is **NOT a duplicate**:

| Overlap window 08-03 21:44 → 08-05 13:13 UTC | rows |
|---|---|
| In both books | 59,895 |
| **Release-only — would have been lost** | **16,459** |
| AWS-only | 1,145 |
| Union | 77,499 |

AWS held **78.8 %** of known trades in that window; the local Release box held **98.5 %**. **AWS was healthy throughout** — no `DOWN`/`DEGRADED` between 2026-08-01 19:02 and 2026-08-06 16:11, 921 analysis rows/day on 08-03/04/05 — so this is not an outage, and the loss is scattered rather than contiguous (36 distinct UTC hours affected, spanning the whole window). Both tapes are now merged into the repo-root store per §4b. **The earlier reading that this span was "probably covered" was wrong, and only the retention rule stopped it being acted on.**

> ⚠ **What this says about the instrument, and it is the load-bearing part.** AWS-only max inter-trade gap over that window is **153.1 s** against the 300 s threshold — **no breach** — and the *merged* store, carrying all 16,459 extra trades, reads **exactly the same 153.1 s**. At one trade per ~2.3 s, scattered per-trade loss can never approach a 300 s threshold, so **S3's gap metric is structurally incapable of detecting it.** Queued for a ruling — see [`trader-tick-queue.md`](trader-tick-queue.md) §0a.

## 4b. Store copy-back — the step-by-step. **Written 2026-08-07, after executing it for the first time**

§4 covers the **CSV**. This covers the **store** (raw tape + candles + funding), which had no written procedure — §4a said to do it and not how. Everything below was run end-to-end on 2026-08-07; the traps are the ones that actually fired, not anticipated ones.

### Know these two before you start, or the run reads the wrong data and says so quietly

1. ⚠ **`BacktestRunner` sets its own working directory.** `BacktestProgram.vb:359` walks up from the exe looking for `DeribitVerdictEngine.sln` and calls `Directory.SetCurrentDirectory` on it. So the `coverage` verb **always** reads the **repo-root** `backtest_data\`, `analysis_log.csv`, `ws_health.log` and `capture_marker.log` — **launching it from elsewhere changes nothing.** There is no `--store-dir` flag. A staging folder therefore cannot be the target: **merge into the repo root first, then run coverage.**
2. ⚠ **The app writes exe-relative; the verb reads CWD-relative.** `TradeStoreWriter.ResolveStoreDir` anchors to `AppDomain.BaseDirectory`, so the app's store is `bin\…\backtest_data\` while the verb's is the repo root. They are different directories on purpose — the repo root is the analysis workspace — but nothing enforces it, and a run against the wrong one produces a plausible report rather than an error. **Tell them apart by the S1 line: `analysis_log.csv has no rows in range` when you know it has rows means you are reading the repo root and staged nothing.**

*(A third trap if you drive this from PowerShell: `Push-Location` does **not** change `[Environment]::CurrentDirectory`, which is what a child process inherits. It will silently run against the repo root anyway.)*

### The procedure

**0.** §3 glance on AWS: title bar `settings v{N}` with **no** `+local`, `backtest_data\` mtime advancing, note the current InstanceId from `ws_health.log`.

**1. Copy off AWS — read-only, delete nothing.** `analysis_log.csv`, `ws_health.log`, the whole `backtest_data\`, and `liq_events.log` if present (A4 gate evidence — pool both boxes' sidecars). The box keeps collecting throughout.

**2. Land it in a dated staging folder under `AWS-copybacks\`,** which is gitignored (added 2026-08-07 — it was untracked, i.e. one `git add -A` from committing ~22 MB of collector data into the repo).

**3. Verify the CSV before trusting it.** Header equality against the local book (§4.2). Then — because AWS's `analysis_log.csv` is a *whole book*, not an increment — **prove the new copy is a strict superset of the previous one** before it supersedes it: `comm -23 <(old timestamps) <(new timestamps)` must be empty. Only then overwrite `bin\Debug\net8.0-windows\analysis_log_aws.csv`.

**4. Compare any second tape against AWS *before* merging** (§4a). Whole-row, not by timestamp.

**5. Merge — additive, whole-line dedup, counts on both sides.**

> ⚠ **`sort -u` with a KEY (`sort -t, -k1,1n -u`) dedups on the KEY, not the line.** On 2026-08-07 that silently collapsed every trade sharing a millisecond and would have destroyed ~44,000 rows — **10,199 timestamps in one month's AWS file carry more than one distinct trade.** Use whole-line `sort -u`. Timestamps are 13-digit epoch ms, so lexicographic order *is* chronological and no second sort is needed.

**Count before, merge, count after, and require every number to be ≥ its before value.** This is the store-integrity lesson applied: all three of the 2026-07-31 store holes were found by counting rows against a deterministic expectation, and none by a test. Assert **zero rows lost from each source** (`comm -23 source merged` empty for every input) *before* installing. Build into a temp file and install only on a clean check — never edit the store in place.

*Also: the store is CRLF. A filter that strips `\r` (plain `awk` will) makes every line differ by one byte and turns a comparison into nonsense — a `0 rows in common` result between two books of the same instrument is that bug, not a finding.*

**6. Coverage.** Stage AWS's `analysis_log.csv` / `ws_health.log` at the **repo root** (there is no local file of those names there, so nothing is overwritten), run the verb, then **delete them** — repo-root `analysis_log.csv` is *not* gitignored. Expect `unknown-scope` for every hour until AWS runs a build carrying `Core/CaptureMarkerLog.vb`; S2/S3/S4 are scope-independent and still meaningful.

**7. Ledger.** Record any new InstanceId in §5a. If AWS has not restarted since the last copy-back, nothing is owed — confirm from `ws_health.log` rather than assuming.

---

## 5. Decommission / handover note

The box owes nothing at end-of-life beyond a final copy-back of `analysis_log.csv`, `liq_events.log`, and `ws_health.log`. **DEPLOYED 2026-07-23 (trader-executed; CSVs sighted and populating).** Engine `InstanceId` (recorded at first copy-back, 2026-07-27 08:18 UTC): `4325cb7e-c21e-444d-b6c4-b355178776cf` (deploy-evening run, 181 rows, 07-22 16:24–19:24 UTC) · `fb908147-0312-4c55-b9d1-a23be310256e` (the standing collector, since 07-22 19:25 UTC). Note: every app restart mints a new id — the authoritative provenance set is the distinct `InstanceId` values in `analysis_log_aws.csv` at each copy-back; re-record any new ids that appear.

**Subsequent ids, from `ws_health.log`:** `0efcda74-6b75-4d5f-af04-f3875b5afd8e` (the v63 redeploy, 2026-07-30 16:41 UTC — not a silent death, despite one 2026-08-01 read calling it that) · **`5a3afd99-6db4-461c-886e-dddcca3d8c62` (the v64 deploy, 2026-08-01 17:49:55 UTC DOWN → 17:50:13 OK, an 18-second connect).**

### 5a. Version ↔ InstanceId ledger — **BOTH boxes.** Load-bearing, not a nicety

**Added 2026-08-02 with the §4.5 correction.** There is **no settings-version column in the CSV**, so this table is the *only* place a version straddle can be reconstructed from. It covers **both** boxes because both books pool. **Record a row at every deploy and every restart-for-a-bump.** The [restart discipline](#4-copy-back--pooled-read-recipe-at-analysis-time) is what keeps it sound: stop → swap settings → start, so the version edge and the new id are the same instant and no row straddles a version inside an instance.

| Box | InstanceId | Settings | From (UTC) | Note |
|---|---|---|---|---|
| AWS | `0efcda74-6b75-4d5f-af04-f3875b5afd8e` | v63 | 2026-07-30 16:41 | |
| AWS | `5a3afd99-6db4-461c-886e-dddcca3d8c62` | **v64** | 2026-08-01 17:49:55 | 18-second connect. **First raw-trade capture anywhere** |
| **AWS** | **`09c747f8-1efb-4ffe-8716-ec8cedfa54c6`** | **v65** | **2026-08-01 19:02:31** | **The D3 deploy.** ⚠ **ASIA aggressor velocity ARMED from this id onward.** 20-second connect (19:02:50.932Z OK), and that `OK` is a *completed-run* signal, not just a connect — so a run had fired by then |
| local | `2f8c9fe1-8325-4fbb-9ee5-41fc267e1efd` | v64 | 2026-08-01 18:00:26 | capture OFF via overlay |
| local | `a4333d00-2b3e-43fd-9226-32184761f4f6` | **v65** | 2026-08-01 18:58:37 | short run, ~4 min. v65 confirmed by mtime — the Debug exe was built 18:58:32Z, five seconds before |
| local | `3916540f-6bc9-4648-ad6c-26bd65cfa462` | **v65** | 2026-08-01 19:02:32 | current. Title `settings v65 +local` |

**The v65 edge is unusually clean and worth recording as the standard to hit.** AWS went down at **19:02:31.111Z** and local at **19:02:32.710Z** — **1.6 seconds apart** — so the two-box mixed-version window is effectively zero, and no pooled ASIA read spanning the boundary has a straddle to resolve beyond the id split itself. Contrast the alternative the §4.5 correction warns about: had the settings been dropped onto either running box, the version would have changed mid-`InstanceId` and no split would have been possible at all.

**Reading `ws_health.log` correctly** (it is easy to over- or under-read, and both happened during the v64 landing): the process-start line comes from `WsHealthLog.LogStart` and fires unconditionally, so **a lone `DOWN` means the app started and nothing else has happened**. The following `OK` comes from `LogWsHealthTransitionForRun`, which runs **for every completed run, success or skip** — so **an `OK` line is positive evidence that at least one analysis run completed.** It is *not* evidence the auto-run timer is still ticking; only `analysis_log.csv` row growth shows that.

⚠ **The v64→v65 edge is a SCORING boundary.** ASIA rows under a v64 id carry an *unarmed* TFI burst vote; ASIA rows under a v65 id carry an armed one, and the two are byte-identical in shape. **Any ASIA read spanning 2026-08-01/02 must split on this table**, or the D3 watch reads its own contamination.

### 5a. v64 deploy — trader-executed 2026-08-01, verified

**This is the moment raw-trade capture began, anywhere.** Under D1 (AWS-only) this box is the sole capturer, and tape older than ~24 h is unobtainable at any price — so everything before 2026-08-01 17:50 UTC is permanently absent from the store, by design and not by defect. That gap is the argument the v64 build was written on.

Verified at deploy:

- **Title bar `settings v64` with NO `+local`** — the §3 glance in its inverted AWS form. The overlay correctly did not travel.
- **`backtest_data\` appeared** — the first observation anywhere of a trade going from the WS stream to disk. The [v64 review](trade-store-capture-review-2026-07-31.md) §5 listed exactly this as unverifiable without a live run; the local box could not close it because capture is off there by ruling.
- **`ws_health.log` DOWN → OK in 18 s**, new id above.
- **The perf strip still reads `Cur.Wk 43% · 3d 53%`** — which is the useful negative: AWS's own eval cache and book **survived the overwrite**. A wipe-and-replace would have blanked it. Confirms the deploy overwrote files rather than replacing the directory, per §1.
- **Both boxes now on v64** ⇒ the §5 same-settings discipline holds and rows stay poolable across the deploy. v64 added seven `trade_store` keys and changed no other value, so nothing tunable moved.
