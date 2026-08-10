# AWS Supplementary Collector — Deploy Checklist

**Date:** 2026-07-23 (trader-directed). **Role:** SUPPLEMENTARY session-coverage collector — primarily ASIA/LONDON, where the local book is thin (344 ASIA×3 rows vs 4,417 NY×1). **The local bin\Debug book stays CANONICAL**; the AWS book pools into reads by concatenation with `InstanceId` provenance. Accelerates the coverage-bound gates (res-3 §5.2, W6-1 LONDON depth, F1 STRONG accumulation, W6-4 book depth); does NOT accelerate calendar-bound gates (A5's 30 distinct days, #6's dates, the funding calm-week).

## 1. Deploy (xcopy — there is no installer)

1. On the local machine, from the PUSHED tree (never an unpushed build): `dotnet build -c Release`.
2. ⚠ **Copy EXACTLY these six items. Nothing else.** Rewritten 2026-08-07 from "take the whole output folder" to a positive allowlist — the folder now also contains per-box state and runtime output, and one of those files stops AWS collecting. **A deploy is irreversible in one direction, so name what goes rather than what stays** (the same reason the J-B rule scopes by a positive record).

| # | Item | Why it must travel |
|---|---|---|
| 1 | `DeribitVerdictEngine.exe` | the app |
| 2 | `DeribitVerdictEngine.dll` | the code — this is what actually carries a version change |
| 3 | `DeribitVerdictEngine.deps.json` | .NET will not start without it |
| 4 | `DeribitVerdictEngine.runtimeconfig.json` | .NET will not start without it |
| 5 | `settings.json` | the tracked config. **Confirm line 2 is the version you intend** |
| 6 | `fonts\` (holds `OFL.txt`) | SIL OFL 1.1 licence must ship beside the exe. The three `.ttf` are `EmbeddedResource` and travel *inside* the exe — CLAUDE.md bundled-fonts rule |

**Optional:** `DeribitVerdictEngine.pdb` — debug symbols. Harmless, and it gives real line numbers if AWS ever throws. Take it or leave it.

**Everything else in that folder is per-box state or runtime output and must NOT travel** — see §1.4 and §1.5. As of 2026-08-07 that means `settings.local.json`, `ohlc_1m_cache.csv` and `analysis_output_dump.md`, plus anything the box generated on a previous run.
3. Confirm the copied `settings.json` is the tracked v59+ file — spot-check line 1 (`"version"`), `auto_run.trigger_mode: "on_close"` (v57 seeds it correctly), and **`trade_store.enabled: true`** (v64 seeds it correctly — see §1a).
4. ⚠ **NEVER copy `settings.local.json`. This is the one that stops AWS collecting.** Added 2026-08-07. The overlay carries `trade_store.enabled: false`; on AWS that silently switches off **the only capturing box**, which is the unrecoverable direction §1a is built around. It sits in the deploy source *because* §1a keeps it there to stop the local Release build capturing — so the file that protects the local box is the file that would kill AWS. **Backstop if it slips through: the §3 AWS glance — a `+local` on the AWS title bar means exactly this, and means capture is off.** Check it immediately after every deploy, not the next day.
5. **Do NOT copy `analysis_log.csv`, `analysis_eval_cache.csv`, `ws_health.log`, `capture_marker.log`, `ohlc_1m_cache.csv`, `analysis_output_dump.md`, any `.bak`/sidecars — or `backtest_data\`.** *(`ohlc_1m_cache.csv` and `analysis_output_dump.md` added 2026-08-07 — a rolling 7-day candle cache and the per-run text dump. Neither is dangerous: candles refetch from the API and the dump is regenerated. They are listed so the allowlist in §1.2 and this list agree.)* The AWS book starts EMPTY on purpose (a seeded copy forks the history and forces dedup at every pooled read; a fresh book concatenates cleanly). ⚠ **`backtest_data\` was added to this list 2026-08-07 and it is the dangerous one** — the others merely fork history, while the store *overwrites AWS's tape*. See the hazard box in §1a.
5. **Copy files INTO the existing folder — never replace the directory.** The v64 deploy confirmed this works: AWS's eval cache and book survived because the copy overwrote file-by-file (§5a). Replacing the directory would delete AWS's store and book outright.
5. `signal_bridge.enabled` may stay as-tracked — emission to `C:\Dev\DeribitBridge\` on a box with no consumer is harmless (payloads simply overwrite). ARM stays OFF by construction (never persisted).

## 1b. Pre-flight before every xcopy to AWS — 30 seconds, prevents an irreversible loss

Run this in the repo root, in **PowerShell** — this is a Windows box and the earlier `ls`/`grep` form did not run here. A clean source prints **only** the two `OK` lines and the version.

```powershell
$d = 'bin\Release\net8.0-windows'
Get-ChildItem $d -Force -EA SilentlyContinue | Where-Object { $_.Name -match 'backtest_data|analysis_log|analysis_eval_cache|ws_health|capture_marker|\.bak$' } | ForEach-Object { "DIRTY  -> $($_.Name)" }
if (-not (Get-ChildItem $d -Force -EA SilentlyContinue | Where-Object { $_.Name -match 'backtest_data|analysis_log|analysis_eval_cache|ws_health|capture_marker|\.bak$' })) { 'OK     deploy source carries no data files' }
if (Test-Path "$d\settings.local.json") { 'OK     Release overlay present - Release will not capture'; 'EXCLUDE  settings.local.json MUST NOT travel to AWS - it would stop AWS capturing' } else { 'ALARM  settings.local.json MISSING - Release will capture and repopulate backtest_data' }
"VERSION $((Get-Content "$d\settings.json" -TotalCount 2)[1].Trim())"
```

- **`DIRTY -> backtest_data`** — the local Release build captured tape. **Do not copy.** Merge that tape into the repo-root store first (§4b), confirm it is a copy, then delete it.
- **`DIRTY -> ` a CSV or sidecar** — delete it from the deploy source only. They regenerate on the local box and must never seed AWS.
- **`ALARM`** — restore the overlay before building, not after. The build is what repopulates the folder.
- **`VERSION`** must match the version you intend to deploy, and must equal the version the other box is on (§4.5 same-settings discipline). Its absence is why the store reappears.

## 1a. The one setting the two boxes must NOT share — `trade_store.enabled` (v64, trader-ruled 2026-07-31; amended 2026-08-07)

> ⚠ **THIS SECTION WAS AMENDED 2026-08-07 — the two boxes now share the value, and that reverses the whole point of the section for the interim.** See D1-a in `docs/in-app-trade-store-capture-proposal.md` §7. **Read the table below, not the reasoning underneath it**, which is preserved because it explains the original design and still governs the end state.

| Box | `trade_store.enabled` | Why |
|---|---|---|
| **AWS** | **`true`** | Unchanged. This box is the canonical capturer, and tape past ~24 h is unobtainable at any price. |
| **Local `bin\Debug`** | ⚠⚠ **`true` since 2026-08-07, but the reason is WITHDRAWN — awaiting a trader re-read** | **Ruled by D1-a, temporary.** ⚠ **The measurement that justified it is retracted** — see §4a. It read the two boxes' disagreement as AWS missing 16,459 trades; they in fact disagree on *amounts at shared timestamps*, and the store has no `trade_id` to tell the cases apart. **Recommendation: set this back to `false` until `trade_id` ships** — two tape books cannot be merged, so a second capturer contributes nothing usable, and tape written meanwhile is permanently unmergeable. The end state was always AWS-only regardless. |
| **Local `bin\Release`** | **`false` — KEEP the overlay** | ⚠ **Corrected 2026-08-07, same day, before it shipped.** Release exists only to build the AWS deploy. Capture there collects nothing anyone runs, and it **repopulates `backtest_data\` inside the deploy source on every run** — see the hazard box below. Keeping the overlay is also the safety net against the 2026-08-03 accident. |

> ⚠ **THE DEPLOY HAZARD — read this before any xcopy to AWS.** `bin\Release\net8.0-windows\` is the deploy source. If it contains `backtest_data\`, an overwriting copy replaces **AWS's** `trades_YYYY-MM.csv` with the local one. Measured on 2026-08-07: AWS's August file held **228,163 rows** and the local Release file held **78,798** — the copy would have destroyed about **150,000 trades**, and tape past ~24 h cannot be refetched by anyone at any price. §1.4 already bans copying the CSVs and sidecars; it predates local capture and never named the store. **Never copy `backtest_data\`. Verify the deploy source is clean before every copy** — the pre-flight is in §1b.

**Consequence of removing the `bin\Debug` overlay, easy to miss:** `trade_store.enabled` was the **only** key it overrode, so removing it leaves nothing to override and **the `+local` title-bar marker disappears from `bin\Debug`.** That marker was §3's daily alarm *in its absence*. The Debug glance is now inverted — see §3. `bin\Release` keeps its overlay and therefore keeps showing `+local`. AWS must still never show it.

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

- ⚠ **REWRITTEN 2026-08-07 — the expected marker is now different in all three folders. Read the row you are actually looking at.**

| Where | Expect `+local`? | What it means |
|---|---|---|
| **AWS** | **NO** | Unchanged. A `+local` here means an overlay AWS should not have. |
| **Local `bin\Debug`** | **NO — changed** | D1-a removed its overlay, so it captures. The live check is that **`backtest_data\` is advancing**, same as AWS. A `+local` here means someone re-added an override and capture is off. |
| **Local `bin\Release`** | **YES** | It keeps its overlay and must not capture. **A missing `+local` here is the alarm** — it means Release will capture and repopulate `backtest_data\` in the deploy source (§1a hazard box). |

  *(Superseded, kept because it explains the marker: until 2026-08-07 every local folder was expected to show `+local`, and its ABSENCE was the alarm. For `bin\Debug` that failure direction is now the intended state, so the alarm is retired there rather than fixed. For `bin\Release` it still binds.)* On AWS the same glance is inverted: a `+local` there means an overlay it should not have. The marker only appears when the overlay actually overrode a key the base carries, so it cannot be earned by a typo'd or rejected key ([F1](settings-local-overlay-spec-back.md), 2026-08-02).
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

> ⚠⚠ **WITHDRAWN 2026-08-08. THE CLAIM THIS SECTION MADE WAS WRONG. Read the correction before anything else here.**

**What was claimed on 2026-08-07:** that a whole-row comparison of the local `bin\Release` tape against the first AWS copy-back showed **16,459 trades present only locally**, putting AWS at **78.8 %** complete while healthy — and that the retention rule had therefore just saved 16,459 unrecoverable trades.

**Why it is withdrawn.** The trader challenged it on the right ground: a box in Deribit's own datacentre, running 24/7, should hold *more* trades than an intermittent laptop, not fewer. Re-checking produced this:

| Test | Result |
|---|---|
| Timestamps absent from AWS entirely | **ZERO**, across all 16,190 pre-cut rows and 6,808 distinct timestamps |
| Where the disagreement actually sits | Same timestamp, same price, same side — **different amounts** |
| Volume over the same window | AWS 78.6 M vs union 152.2 M — it nearly **doubles** |
| Aggregation anywhere in our code | **None.** `DeribitWsFeed` and `HistoricalStore` both write `amount` verbatim |
| Where the disagreement concentrates | **98.4 %** of it falls in the period the local tape was **REST-backfilled**, only 1.6 % where both boxes streamed live |

Worked example at ts `1785793449897`: AWS holds one row of `790.00`; the local box holds `120`, `310`, `500`, `900` — same millisecond, price and side. **Those are not missing trades. They are the same market activity represented differently by the two feeds.**

> ⚠ **THE ROOT CAUSE, and it is bigger than the measurement error. The store has NO TRADE IDENTITY.** Deribit's trade records carry a `trade_id`. `HistoricalStore.vb:307-317` reads price, amount, direction, timestamp and liquidation and **never reads it**. The store row is those five fields, and `TradeStoreWriter.FormatRow` — that same five-field row — is what the store dedups on *and* what S0's venue diff matches on (`CoverageReport.vb:310, 486, 492`). **Three consequences:** a whole-row comparison can never distinguish "the other box has a trade I lack" from "the other box represents the same trade differently"; the store **silently drops genuinely distinct trades at write time** when five fields collide (22,376 of AWS's 228,163 August rows are exact duplicates, and nothing can say how many were real); and **S0 inherits the same blindness**, so a daily venue-diff job would report the same ambiguity as "missing trades".

**What still stands, on reasoning that does not depend on identity:** **S3's longest-gap metric cannot detect scattered loss.** At ~1 trade per 2.3 s against a 300 s threshold you would have to lose ~130 *consecutive* trades to trip it. That is arithmetic, and it holds whatever the extra rows turn out to be.

**Store state after the correction (2026-08-08).** The merged store was **un-merged**. Each file now has a **single provenance**: `trades_2026-07.csv` is a pure `BacktestRunner fetch` (REST), `trades_2026-08.csv` is pure AWS capture. The mixed versions are kept, not deleted, in `AWS-copybacks\quarantine-mixed-2026-08-08\` per the retention rule. **Do not merge two tape books again until `trade_id` is in the schema** — see [`trader-tick-queue.md`](trader-tick-queue.md) §0a and §2.

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
| local | `3916540f-6bc9-4648-ad6c-26bd65cfa462` | **v65** | 2026-08-01 19:02:32 | Title `settings v65 +local`. Capture OFF |
| **AWS** | **`ec487909-940f-492b-8d1d-ee15f2ddcca0`** | **v65 — UNCHANGED** | **2026-08-07 16:02:41.487** | **The C1 code deploy.** `DOWN` 16:02:41.487Z → `OK` 16:02:47.264Z, a **5.8-second connect** — the fastest of the three deploys (v64 took 18 s, v65 took 20 s), and that `OK` is positive evidence a full analysis run completed. ⚠ **NOT a scoring boundary** — `settings.json` did not move, so this id is a *restart* edge and not a version edge. Its real significance: **`capture_marker.log` begins on AWS at this id.** Every AWS hour before it is permanently `unknown-scope` in a coverage report and cannot be backfilled |
| ⭐ **AWS** | **`d8678d2b-94c4-4308-adc1-a88a51c2feea`** | **v65 — UNCHANGED** | **2026-08-10 14:08:39.770** | ⭐ **THE TRADE-IDENTITY DEPLOY — the first id anywhere that writes IDENTIFIED tape.** `DOWN` 14:08:39.770Z → `OK` 14:08:44.514Z, a **4.7-second connect**, the fastest of the four deploys (v64 18 s, v65 20 s, C1 5.8 s). Trader confirmed 7-field rows appearing and all four post-deploy checks. ⚠ **NOT a scoring boundary** — `settings.json` did not move. **Its significance is the store, not the scoring: every tape row before this id is five-field and identity-less, and every row after carries `trade_id` + `trade_seq`.** `trades_2026-08.csv` is therefore a **mixed-shape file** from here on — and that is precisely the case ratified as Q1 in [`trade-store-trade-identity-review-2026-08-08.md`](trade-store-trade-identity-review-2026-08-08.md), which was ruled while the affected population was still zero. **From this id onward that ruling is load-bearing.** The file keeps its five-field header until the September rollover; `LegacyHeaderLine` exists so the reader accepts both |
| **local** | **`ad7cadf4-93a8-4700-a1f6-fb5abba223e8`** | **v65 — UNCHANGED** | **2026-08-07 16:05:56.841** | ⚠ **D1-a takes effect here — local capture is ON.** Verified from `bin\Debug\net8.0-windows\capture_marker.log`, whose line for this id reads `True`. Title bar no longer shows `+local`; the Debug overlay is deleted, the Release one is kept |

**The two boxes were 3 m 15 s apart this time** (AWS 16:02:41, local 16:05:56), against 1.6 s at the v65 deploy. **That gap does not matter here and it is worth saying why:** the v65 deploy was a *version* edge, where a wide gap leaves a mixed-version fleet and an unresolvable straddle. This deploy moved code only, with both boxes on v65 either side, so a row logged in that 3-minute window is scored identically on both. **Judge deploy-gap tightness by whether `settings.json` moved, not by habit.**

> ⚠ **Consequence for the D3 ASIA watch, and it is easy to get wrong.** The armed-ASIA (v65) population on AWS now spans **two** instance ids — `09c747f8…` and `ec487909…`. A pooled ASIA read that filters on `09c747f8` alone silently drops everything after the 2026-08-07/08 deploy. **Both are v65 and both are armed.** The same applies locally: `3916540f…` and `ad7cadf4…` are both v65. The version edge is still only the v64→v65 one on 2026-08-01; every id after that is a restart.

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
