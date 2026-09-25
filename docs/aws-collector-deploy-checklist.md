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
2. **Concat for pooled reads — REPLACED 2026-09-24 (UTC) by `RIDER-2` (a) of [`csv-rotation-riders.md`](csv-rotation-riders.md), ruled in [`absorption-d2-stage1-rotation-build-spec.md`](absorption-d2-stage1-rotation-build-spec.md) §4.5.**
   - **Pool EVERY rotated book on a box plus that box's live file.** A rotated book is any `analysis_log.csv*.bak`: the literal `analysis_log.csv.v0.7.bak` (on production it holds the 111-column book, 2026-07-22 → 2026-09-01 15:48:01 UTC) and every book named `analysis_log.csv.<N>col-<h8>.<yyyyMMdd_HHmmss>.bak` since the 2026-09 rotation. `tools/ops/collector.ps1 fetch` now discovers and size-verifies all of them.
   - **Concat the NEWEST schema FIRST**, then each older book minus its header line. Readers resolve columns by header name and short rows read empty, so the newest header must be the one the reader sees (the order settled 2026-08-20; row order is never consulted — see the ledger's archive row).
   - **Never require header equality across a rotation.** Headers differ by construction after one. Require it only between two boxes' books of the same period: if a rotation happened on one box only, DO NOT pool across boxes until both are on the same schema.
   - **Check the spans do not overlap** (each book's last timestamp is before the next book's first). `tools/ops/kelly-trigger-read.ps1` does this for every adjacent pair; `-Mode LocalDir -Dir <fetch folder>` runs it on a fetched folder.
   - *Replaced text, kept for history:* ~~Concat for pooled reads: local CSV + AWS CSV minus its header line (both v0.8/111-col schema — verify header equality first; if a rotation happened on one box only, DO NOT pool until both are on the same schema).~~
3. Provenance: `InstanceId` distinguishes boxes; the standing exclusions apply as always (weekday-only, burst instances).
3b. **Cross-box dedup — RE-RULED 2026-07-31 (trader): AWS-PREFERRED, per MINUTE-KEY.** Both boxes fire on the same bar closes, so overlapping rows are near-duplicate observations and **pooled STATISTICAL reads must not double-count them**. Rule: **key each row by its timestamp floored to the minute; where both books have that key, take the AWS row; local fills the keys AWS missed.** Applied at pooled-snapshot construction (the concat step above), so **no tool changes** — CeilingAudit / report / what-if consume the deduped snapshot as an ordinary CSV. Coverage-map reads (row counts per box) and single-box reads are unaffected.

> ⚠ **This supersedes the 2026-07-29 rule on BOTH axes, and the second change is easy to miss.**
> **Preference flipped** local→AWS: AWS is the canonical 24/7 collector and the D1 end-state topology, while the local box is an opportunistic addendum that runs only while the trader is at the desk (measured: local logged 567/392/268 rows on 07-28/29/30 against AWS's 921/921/914).
> **Granularity changed** session-hour→**minute-key**. The old formulation — *"for any (UTC session-hour) where the local book has rows, use ONLY local rows"* — discards every AWS row in an hour where local produced even one, which throws away coverage the pooling exists to gain. Minute-key dedup removes exactly the duplicate observation and keeps the rest.
> **Both boxes fire 1–10 s after the close, so a bar never straddles two minute-keys** — verified on both books before this was adopted.
> **Measured impact:** on the 2,159 minute-keys present in both books the two boxes **disagree on verdict 4.49 %** of the time (97 bars), so the preference is not cosmetic — but it moves the F1 STRONG count by 2 and the W6-1 LONDON count by 2, i.e. no gate conclusion turns on it.
> **Reproducibility note:** every pooled figure published from 2026-07-31 onward — F1's 203 evaluable STRONG, the W6-1 grids, the W6-4 ceiling audit, the ASIA burst derivation — was built on **this** method. A snapshot built the old way will not reproduce them.

> ### ⚠ AMENDED 2026-08-11 — what the minute key does WITHIN one book, measured
>
> **The rule above is written as a cross-box rule. It is not only that.** A minute key also collapses two runs from the **same** box that land in one minute, and nothing here said so. Raised as finding **S-5** in [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md); the figures below are an **independent re-measurement**, not the audit's.
>
> | Book | Rows | Minutes carrying >1 distinct run | Rows the minute key DROPS |
> |---|---:|---:|---:|
> | AWS copy-back 2026-08-10 | 15,499 | 25 | **25 (0.16 %)** |
> | Local `bin\Debug\net8.0-windows` | 10,779 | 181 | **548 (5.08 %)** |
>
> ⚠ **The AWS losses are strongly session-clustered: 21 of 25 sit at hour 13:00 UTC** (3 at 20:00, 1 at 09:00). 13:00 is the NY `session_volume` boundary where `execution_resolution` switches **3 → 1**, so the cadence changes and two runs can land in one minute. **A session-correlated loss is not noise.**
>
> ### ✅ THE RULING: keep the minute key. Do NOT change it for the Kelly / W6-4 freeze.
>
> ⚠ **This reverses the orchestrator's first recommendation, which was "fix the key before the next pooled read". Measurement did not support it.**
>
> **Of the 573 dropped rows, exactly ONE is a weekday STRONG** — one in the local book, zero on AWS — against **281** weekday STRONG across both books. **The instrument that actually consumes the pooled read is the Kelly dated trigger (≥406 pooled weekday STRONG), and the minute key costs it one row in 281 — 0.36 %.**
>
> Against that, changing the key **breaks the reproducibility note directly above**: every pooled figure published since 2026-07-31 was built on the minute key. **Reproducibility beats one row.**
>
> ### ⚠ When this DOES matter — the hazard is real for reads that do not exist yet
>
> The bias is concentrated, not diffuse. **Any future pooled read that segments by session, by execution resolution, or specifically around the 13:00 handover is materially biased by this key** — it removes ~84 % of its dropped AWS rows from one hour. **Before any such read, switch to second-resolution keying, or `InstanceId` + `SignalId` + timestamp** — both are exact and cost nothing extra — **and say in the write-up that the figures will not reconcile with pre-2026-08-11 pooled reads.**
>
> **Not a code change.** No minute-key dedup exists in any `.vb` file; this is a construction procedure applied by hand at pooled-snapshot time.
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

**1. Copy off AWS — read-only, delete nothing.** `analysis_log.csv`, `ws_health.log`, ⚠ **`capture_marker.log`**, the whole `backtest_data\`, and `liq_events.log` if present (A4 gate evidence — pool both boxes' sidecars). The box keeps collecting throughout.

> ⚠⚠ **`capture_marker.log` ADDED TO THIS LIST 2026-08-14 — its absence blocked a scheduled check.** The 2026-08-14 copy-back brought back the CSV, the health log and the store, and **no marker log**, because this list did not name one. **Without AWS's markers `CoverageReport.ResolveScope` returns `unknown` for every hour**, so every hour classifies `UnknownScope`, no split hour can occur, and the coverage report cannot answer anything about capture scope. That blocked the SH-1 / D-3 real-world confirmation on AWS hour 2026-08-07 16:00.
>
> ⚠ **Do NOT substitute a local box's `capture_marker.log`.** The verb reads the repo root and the store there is AWS tape; staging local markers against AWS tape produces a plausible report describing neither box. **The report has no way to detect the mismatch.**
>
> **Where it lives on the box:** beside the exe, exe-relative, same directory as `analysis_log.csv`. It is append-only and small (~2 KB), so there is no reason it was ever left off.

**2. Land it in a dated staging folder under `AWS-copybacks\`,** which is gitignored (added 2026-08-07 — it was untracked, i.e. one `git add -A` from committing ~22 MB of collector data into the repo).

**3. Verify the CSV before trusting it.** Header equality against the local book (§4.2). Then — because AWS's `analysis_log.csv` is a *whole book*, not an increment — **prove the new copy is a strict superset of the previous one** before it supersedes it: `comm -23 <(old timestamps) <(new timestamps)` must be empty. Only then overwrite `bin\Debug\net8.0-windows\analysis_log_aws.csv`.

**4. Compare any second tape against AWS *before* merging** (§4a). Whole-row, not by timestamp.

**5. Merge — additive, whole-line dedup, counts on both sides.**

> ## ⚠⚠ CORRECTED 2026-08-14 — **DO NOT WHOLE-LINE DEDUP A MIXED-ERA STORE. It destroys real trades.**
>
> **Measured on the 2026-08-14 copy-back: whole-line dedup would have removed 23,806 genuinely distinct rows from `trades_2026-08.csv` alone.** The legacy era is five-field, and **two distinct trades can share all five fields** — that is the entire reason `trade_id` was added, and it is already recorded in §4a's root-cause box. Whole-line equality was the right relation only while nothing better existed.
>
> ✅ **THE RULE NOW: CONCATENATE. Do not dedup on disk at all.**
>
> **Duplicates on disk are harmless BY DESIGN** — `Core/TradeStoreWriter.vb` states it in terms: *"A duplicate on disk is harmless — the read path dedups it."* Both readers (`LoadTradeRange`, `CoverageReport.AccumulateHourStats`) route through `TradeStoreWriter.DedupTrades`, the identity-first §3.4 contract. **Deduping at merge time applies a WEAKER relation than the read path and the loss is permanent.**
>
> ⚠ **Check containment BEFORE deciding anything** — it is usually cheaper than a merge and often shows no merge is needed:
>
> - **2026-08-14, August:** 0 distinct lines only in the repo, 162,518 only in the copy-back ⇒ **the copy-back is a strict superset; a plain file copy is correct and safe.**
> - **2026-08-14, July:** 0 distinct lines only in the copy-back, **115,029 only in the repo** ⇒ ⚠⚠ **the repo file is authoritative and a copy would have destroyed 118,775 rows.** July on AWS holds only what gap repair reached back into it; the repo's July is a full REST backfill.
>
> ⚠ **And beware a containment check that cannot fail.** The first pass at this compared each source against the union that contained it, so "rows lost: 0" was vacuous. **Compare each source against the OTHER source, not against the result.**

*Superseded guidance follows, kept per the quote-and-label convention — its `sort -u` warning still holds for any single-file operation.*

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
| **AWS** | **`ffced26c-aaab-4cbc-b7e8-a0f1882dd3b3`** | **v65 — UNCHANGED** | **2026-08-10 09:59:56.592** | ⚠ **RECORDED LATE — added 2026-08-11, after the D3 watch read found it in the copy-back rather than here.** `DOWN` 09:59:56.592Z → `OK` 10:00:03.328Z, a 6.7-second connect. **Not a deploy — this is the restart that ENDED the intentional weekend instance stop** (trader-confirmed). No code and no settings moved; the binary was still the pre-identity one, so its 129 CSV rows and all tape it repaired are **five-field**. ⚠ **It is the far edge of a ~49.5 h collection hole**: no AWS analysis row exists between 2026-08-08 08:33:26 and 2026-08-10 10:00:01. An EC2 stop kills the process without running shutdown code, which is why no `DOWN` line marks the start of that hole — **absence of a `DOWN` line is not evidence the box stayed up.** Gap repair back-filled tape 20 h from this restart (`gap_repair_lookback_hours` = 20; 10:00 − 20 h = 14:00 exactly), so the **tape** hole is 29.4 h while the **CSV** hole is 49.5 h and has no repair path at all |
| ⭐ **AWS** | **`d8678d2b-94c4-4308-adc1-a88a51c2feea`** | **v65 — UNCHANGED** | **2026-08-10 14:08:39.770** | ⭐ **THE TRADE-IDENTITY DEPLOY — the first id anywhere that writes IDENTIFIED tape.** `DOWN` 14:08:39.770Z → `OK` 14:08:44.514Z, a **4.7-second connect**, the fastest of the four deploys (v64 18 s, v65 20 s, C1 5.8 s). Trader confirmed 7-field rows appearing and all four post-deploy checks. ⚠ **NOT a scoring boundary** — `settings.json` did not move. **Its significance is the store, not the scoring: every tape row before this id is five-field and identity-less, and every row after carries `trade_id` + `trade_seq`.** `trades_2026-08.csv` is therefore a **mixed-shape file** from here on — and that is precisely the case ratified as Q1 in [`trade-store-trade-identity-review-2026-08-08.md`](trade-store-trade-identity-review-2026-08-08.md), which was ruled while the affected population was still zero. **From this id onward that ruling is load-bearing.** The file keeps its five-field header until the September rollover; `LegacyHeaderLine` exists so the reader accepts both |
| ⭐ **AWS** | **`3be7f4c9-8a85-46c8-b3be-f5b820c82278`** | ⚠ **v66** | ✅ **2026-08-10 18:35:18.809** | ⭐ **THE D2 DEPLOY — `indicators.OBV.trend_gate` 18.0 → 23.0.** ⚠ **This IS a scoring boundary and a dataset boundary**, unlike the three restarts before it: ASIA/LONDON/NY rows under this id carry a **less-often-directional OBV** (expected ≈52/49/51 % against 62.2/58.1/60.3 %). ✅ **TIMESTAMP FILLED 2026-08-12 from the copy-back — the OWED marker is discharged.** `DOWN` 18:35:18.809Z → `OK` **18:36:03.112Z**: a **44.3-second connect, by far the slowest of the five deploys** (v64 18 s, v65 20 s, C1 5.8 s, identity 4.7 s). Worth noting, not alarming — the `OK` is a completed-run signal, so a run had fired by then. ✅ **The version edge is clean and the 40-second box gap does NOT straddle:** AWS went down 18:35:18 and local 18:35:58, so in that window AWS was **off** and local was still on **v65** (`e50f3db5`, its pre-deploy id) — **no v66 row exists before either box restarted**. ⚠ **Also carries a 26-minute DEGRADED window: 2026-08-11 09:04:21.695Z → 09:30:39.338Z**, coinciding with the Deribit `system_maintenance` outage. Rows are thin there; treat that span as reduced coverage, not as a defect |
| ⭐ **AWS** | **`a5d701ad-eea1-4ba0-97a5-2ea05274c8c5`** | **v66 — UNCHANGED** | **2026-08-11 17:18:36.490** | ⭐⭐ **THE WRITE-GUARD FIX — the first id anywhere that captures same-millisecond siblings.** `DOWN` 17:18:36.490Z → `OK` 17:18:42.445Z, a **5.96-second connect**. ⚠ **NOT a scoring boundary** — `settings.json` does not move, and the engine never read the trade store. ⚠ **But it IS a TAPE boundary, and the sharpest one in the store's history:** tape before this id is **~50 % complete and biased** (the guard kept one leg per millisecond, dropping the later legs of sweeps); tape after it is complete. **Any tape-derived measure spanning this id must split on it.** ✅ **VERIFIED POST-DEPLOY from the 2026-08-12 copy-back** — see the box below |
| ⭐ **AWS** | **`e551f15e-b245-4392-8e71-89e749636f1c`** | **v66 — UNCHANGED** | **2026-08-13 13:10:32.637** | ⭐ **THE DOWNTIME-REPAIR DEPLOY — the first id anywhere running hole-derived repair windows.** Carries Part A (`c6c6942`) plus **DR-1** (no width floor) and **DR-2** (time-contiguous truncation cut) from `91942d6`. `DOWN` 13:10:32.637Z → `OK` 13:10:38.160Z, a **5.5-second connect**. ⚠ **NOT a version edge and NOT a scoring boundary** — AWS has been on **v66 since `3be7f4c9…` on 2026-08-10**, so `settings.json` did not move and this is a **restart/code edge only.** ⚠ **The queue carried "v66 deploy still owed" in four places and it was stale; corrected 2026-08-13.** **Its significance is the repair path, not the scoring:** from this id onward a tape hole left by an outage the app RIDES THROUGH is recoverable at the next 6-hourly pass. ✅ **Trader-confirmed live at 13:11:07Z: title bar `settings v66`, NO `+local`** — the overlay did not travel and AWS capture is on. **The falsifiable prediction is now armed: after the next real outage, the hole must fill within one `gap_repair_interval_hours` (6 h). Read `trade_seq` completeness FROM THE STORE, not the repair log** |
| AWS | `e3781e57-f08c-480a-b79f-c87fb6e8285c` | ⚠ **NOT VERIFIED** | 2026-08-17 16:23:05.658 | ⛔ **BACK-FILLED 2026-09-13 (UTC) from the box's own `ws_health.log` and `capture_marker.log` (fetch `aws_fetch/20260913-153704`) — this ledger had no AWS row after `e551f15e…`.** Restart; the cause is NOT recorded in either log. Store dir `C:\DeribitVerdictEngine\backtest_data`. `DEGRADED` 2026-08-18 09:04:04 → `OK` 10:26:55 |
| AWS | `03a60e32-37f0-4236-af15-f4991ccdc96c` | ⚠ **NOT VERIFIED** | 2026-08-22 16:02:49.842 | ⛔ **BACK-FILLED 2026-09-13.** ⭐ **The store dir CHANGES here to `C:\DeribitEngine\backtest_data`** — consistent with the cutover to production `i-0d6c133058876273e`, ⚠ **not verified against the cutover record.** The `DOWN` at 2026-08-27 00:51:12 under the SAME id is a feed drop inside one process, not a restart |
| **AWS** | **`3fe57c53-5c32-4cdd-87fa-4f6f64901c1a`** | **v68** (carried from the 2026-09-11 15:47 UTC status read) | **2026-09-01 15:49:09.256** | ⭐ **The absorption-instrumentation deploy** — commit `49d0098` via `tools/ops/collector.ps1 deploy`, 15:49:02 UTC per [`absorption-instrumentation-batch-summary.md`](absorption-instrumentation-batch-summary.md) §4.2. ⛔ **BACK-FILLED 2026-09-13.** 56-second connect (`OK` 15:50:05.710). ⛔⛔ **THIS ROW'S END CORRECTED 2026-09-21. It read *"the same process still runs"* and *"One process for the whole 2026-09-01 → 09-11 book — the next deploy is the next version edge"*. Both are now false.** The process did NOT run to the next deploy: it **stopped writing `analysis_log.csv` at 2026-09-18 02:00:07** and was killed at **2026-09-21 10:53:43**. It was ALIVE but catatonic for that 3 d 8 h — Windows trimmed its working set to 0.5 MB under commit exhaustion caused by the UI-thread timer leak (11,644 threads at kill). ⚠ **So this id's book ENDS at 2026-09-18 02:00:07 with no `DOWN` line and no marker of any kind.** Absence of a shutdown record is not evidence the box stayed up — the same lesson `ffced26c…` records for an EC2 stop |
| ⛔ **AWS** | **FIVE ZERO-ROW INSTANCES, 2026-09-21, collapsed into one row.** `0ee29530-16db-4382-a351-745fcd66c826` (10:53:43, old build) · `2bb0e41d-2d07-4e88-8ee3-0c8f6c06496a` (14:30:50, old build, started so the deploy tool could resolve the install dir) · `add6c551-b40d-473e-9daa-5151b304d12e` (14:32:27, ⭐ **the NEW build WITHOUT the proxy fix**) · `7b933b14-1b44-471f-b946-09783e066925` (14:45:19, old build, the automatic rollback) · `86b63d2e-a48f-4051-aee7-3dcd2f1c7900` (15:36:58, old build, started for the second deploy) | **v68 — UNCHANGED throughout. No deploy here is a version edge** | **2026-09-21 10:53 → 15:38** | ⛔⛔ **EVERY ONE WROTE ZERO `analysis_log.csv` ROWS.** All five are the WPAD outage (see the `ee159d03…` row below): proxy auto-detect hung every connect before a socket opened, so every run skipped and a skipped run writes no row. ⭐⭐ **`add6c551…` is the load-bearing one: it ran the new build WITH the thread-leak fixes and WITHOUT the proxy fix, and produced zero rows exactly like the old build. That is what proved the zero-row symptom was NOT caused by those fixes** — same acceptance gate, same box, same network, 28 polls at `rowsAfterRestart=0`, then the rollback produced 25 more. ⚠ **Collapsed deliberately: five rows for five instances that contribute NOTHING to any pooled read would bury the two rows that matter.** They are recorded so they are FINDABLE, per the reconciliation hole named below. Original single-instance row text for `0ee29530…`: ⛔⛔ **A ZERO-ROW INSTANCE. It wrote NO `analysis_log.csv` rows at all** — its single trace anywhere is one `ws_health.log` line, `DOWN` at 10:55:54.875Z. **The outage-recovery relaunch**, not a deploy: same 2026-09-01 binary, restarted per the `Start-RemoteApp` create-run-delete recipe (`tools/ops/collector.ps1:883-900`) after the leaked process was killed. PID 50704, SessionId 2, up 2 s after `schtasks /run`. **The WS never connected** (`Get-NetTCPConnection` for the PID returned nothing; CPU flat at 3.7 s over ten minutes) and **the thread leak resumed within seven minutes** — 16 → 223 threads from 11:00:16 at ~1/second, the same 1 Hz signature as 09-18. Killed on our instruction the same day; ⚠ **the kill itself is NOT confirmed to this seat.** ⛔⛔ **RECORD-KEEPING CONSEQUENCE, and it is why this row was nearly lost: the standing reconciliation in this file's §5a note — diff the distinct `InstanceId` values in the copied-back `analysis_log.csv` against this table — CANNOT EVER FIND THIS ID, because it logged no rows. A zero-row instance is invisible to the only automatic check we have. It exists here because it was written by hand, once** |
| ⭐⭐ **AWS** | **`ee159d03-1233-456a-9cd4-46bb411cc0d8`** | **v68 — UNCHANGED. ⚠ NOT a settings edge, but it IS a CODE edge and a big one** | **2026-09-21 15:38:41** | ⭐⭐ **THE RECOVERY DEPLOY — and the first instance anywhere that can reach the venue since 2026-09-01.** Commit `584c616` via `tools/ops/collector.ps1 deploy`. ⛔ **ROOT CAUSE it fixes: WPAD proxy auto-detect was ON machine-wide, and every HTTP and WebSocket client in the tree was at the default.** Proxy resolution happens BEFORE the request is issued, so `HttpClient.Timeout` never bounded it and a hung connect opened NO socket at all — which is why the box showed zero TCP connections with flat CPU. Found by the co-tenant seat in-process (`UseProxy=False` HTTP 200 in 1,003 ms against a default client timing out past 25,000 ms); confirmed here by grep — `UseProxy`/`DefaultWebProxy`/`WebProxy`/`Options.Proxy` returned **zero** matches across every `.vb`. ✅ **`ws_health.log` `DOWN` 15:38:42.982 → `OK` 15:39:49.295 — the first `OK` since 2026-09-01.** ⭐ **The acceptance gate is the proof, because it is unchanged:** two earlier attempts the same afternoon sat at `rowsAfterRestart=0`; this one ran `0→1→2→3` with an 81 s span and ACCEPTED. ⭐ **Also the first instance carrying the thread-leak fixes** (re-entrancy gates on all three timer marshals, `GetRowCount` streaming). **Leak confirmed dead by the co-tenant at 18:31: 13 threads at 175 minutes, private memory FALLING 106.1 → 90.9 → 87.9 MB, paging zero across 25 samples** — against 11,630 threads in four hours on 09-18 and 223 in three minutes on the 09-21 relaunch. ⚠ **This deploy also carries ~20 commits' worth of engine change since 2026-09-01**, including the three gap-repair fixes (so the 2026-10-01 cross-month deadline is DISCHARGED), the venue-status instrument, atomic writes, `WD-SEMANTICS`, the weekday filters, and the `S-4` eval-cache v6→v7 migration. ⚠ **New files on the box from here: `venue_status.log`, `repair_status.log`, `ws_feed.log`** |
| ⭐⭐ **AWS** | **`25951567-9721-4644-86e4-b486840dfbf4`** | **v68 — UNCHANGED. ⛔ A CODE EDGE AND A SCHEMA EDGE** | **2026-09-24 18:46:06** (first row; deploy started 18:44:09) | ⭐ **THE ONE DEPLOY** — commit `55788bb` via `tools/ops/collector.ps1 deploy`, ACCEPTED (3 rows, 58 s span). Plan: [`absorption-d2-s2-batch-summary.md`](absorption-d2-s2-batch-summary.md) §5. *One code edge at 2026-09-24 18:46:06 UTC: the absorption `D-2` meaning change (`AbsorptionAggrUsd`, `AbsorptionRatio`), the `analysis_log.csv` rotation 116 → 124 columns, and the POC-tier gate fix (`TargetCapReason`, `Placed*`, about 1.68 % of verdicts); Session C moves rendered values only. Boundary count for the engine-fix build under `EF-1` (a): **TWO** — Session B (the liquidation flag, `D-4`/`D-5`) did not make this deploy and takes its own later boundary.* **Post-deploy checks, all passed:** rotated book `analysis_log.csv.116col-83564b1b.20260924_184606.bak` (16,874,238 B); `analysis_log.csv.v0.7.bak` unchanged at 31,234,053 B; header 124 columns; new columns populated (`TriggerMode` `ON_CLOSE`, `SettingsVersion` 68, `VPFRSignal` set); `absorption_episodes.log` 2 lines 50.7 s apart with matching `iid`/`sid`. **Pre-deploy fetch `aws_fetch\20260924-183736`:** `T-7` passed (`DISCOVERED_BAK=analysis_log.csv.v0.7.bak`); Kelly calibrate passed; venue check `LOSS missing=6` — both seq runs (`301043197..199`, `301069755..757`) were younger than the 15:39 repair pass, and this instance's startup pass repaired both at 18:46:08. ⚠ **The liquidation probe on this box (`C:\probe-runs\`) was not touched and kept running.** ✅ **Second `fetch` DONE 2026-09-25 08:53 UTC** (`aws_fetch\20260925-085341`): two `DISCOVERED_BAK=` lines, all transfers size-verified including `absorption_episodes.log` (492 lines, all this `iid`); venue check `CLEAN` (missing 0, `seq_contiguous=true`); `kelly-trigger-read.ps1 -Mode LocalDir` pools the three books to 553 with every adjacent pair clean |
| **AWS** | **`c1855035-3e12-42e6-b736-924b3d0c9afc`** (read back 2026-09-25 13:09 UTC with `tools/ops/collector-readback.ps1`) | **v69 — ⛔ A SETTINGS EDGE** (`kelly.min_book_rows` added, `kelly.est_prob_*` removed) | **2026-09-25 12:30:50** (first row; process started 12:29:28) | ⚠ **Slow connect this time:** `ws_health.log` `DOWN` 12:29:31 → `DEGRADED` 12:30:57 → `OK` 12:33:09, about 3.6 min, against 1.4 s on the 2026-09-24 deploy. The first rows were written while `DEGRADED`. Rows since carry `WsHealth` `OK` and `SettingsVersion` 69. The old instance `25951567…` holds 565 rows. **The Kelly one-class + placed-payoff deploy** — commit `410deb6` via `tools/ops/collector.ps1 deploy`, ACCEPTED (PID 49300, 2 rows, 135 s span, settings v69 read back by the gate). Carries `06e34c1` + `31f57d0`: Kelly p and b from the live eval cache per session and tercile; the `[NO EDGE]` block. **Display-only: no scoring change, no `analysis_log.csv` change** — the settings edge moves no scored value. Old instance `25951567…` ended here. The first read-back attempt was blocked by the auto-mode permission classifier; it ran at 13:09 UTC once the trader allow-listed `tools/ops/collector-readback.ps1` |
| ⭐ **local** | **`7c9d9a59-b8b1-4395-bda9-65ba9b4d6fd4`** | ⚠ **v66** | **2026-08-10 18:35:58.849** | ⭐ **The D2 deploy, local side.** `DOWN` 18:35:58.849Z → `OK` 18:36:04.323Z, a 5.5-second connect. Verified after the rebuild: `bin\Debug\net8.0-windows\settings.json` reads **v66**; the `settings.local.json` overlay **survived** and `capture_marker.log` for this id reads **`False`** — local capture correctly still OFF under D1. ⚠ **Note the preceding local id `e50f3db5-a17a-41cd-819b-3c02da1cc047` (2026-08-10 17:28:23.014Z) is NOT in this table** — a short run about an hour before the deploy. Recorded here rather than given its own row because nothing distinguishes it; **it is v65 and its capture was OFF** |
| **local** | **`ad7cadf4-93a8-4700-a1f6-fb5abba223e8`** | **v65 — UNCHANGED** | **2026-08-07 16:05:56.841** | ⚠ **D1-a takes effect here — local capture is ON.** Verified from `bin\Debug\net8.0-windows\capture_marker.log`, whose line for this id reads `True`. Title bar no longer shows `+local`; the Debug overlay is deleted, the Release one is kept |

### 5b. `DEGRADED` events on AWS — the full set, recorded 2026-08-13 from the box's `ws_health.log`

⚠ **NEW SECTION. Two of these three were in no document at all**, and the log is transitions-only, so they are only visible when someone reads the whole file.

| # | `DEGRADED` (UTC) | → `OK` | Duration | InstanceId | Recorded before? |
|---|---|---|---|---:|---|
| 1 | 2026-08-06 16:11:04.217 | 16:12:04.177 | **60.0 s** | `09c747f8…` (v65) | ⚠ **NO** |
| 2 | 2026-08-11 09:04:21.695 | 09:30:39.338 | **26 m 17.6 s** | `3be7f4c9…` (v66) | ✅ yes — the Deribit `system_maintenance` outage |
| 3 | 2026-08-11 20:47:27.818 | 20:48:39.648 | **71.8 s** | `a5d701ad…` (v66) | ⚠ **NO** |

**Reading them.** #1 and #3 are one-to-two auto-run cycles at the shipped 1-minute cadence — a single run fell back to REST and the next recovered. **Self-healing, and exactly what `DEGRADED` is for.** #2 is the venue outage already on file.

⚠ **#3 is worth one check and is now unrecoverable if it matters.** It sits under `a5d701ad…`, the write-guard instance whose tape we describe as *complete* — and the app was **not restarted** between 17:18:36 and 2026-08-13 13:10, so under pre-Part-A code any tape hole there was **permanently skipped** (§5a-bis of [`trade-store-same-millisecond-drop-2026-08-11.md`](trade-store-same-millisecond-drop-2026-08-11.md)). **Check `trade_seq` continuity across 2026-08-11 20:47–20:49 at the next copy-back.** Past retention, so this is evidence for the fix, not a recovery.

> ## ⚠⚠ WHY `ws_health.log` UNDER-REPORTS — the mechanism, read from the code 2026-08-13
>
> **This is not a lag and not a classification bug. The log measures a different thing than `CoverageReport`'s S1 join assumes.** `SignalEmitter.DeriveWsHealth` (`Core/SignalEmitter.vb:94`) is:
>
> ```vb
> If Not transportIsWs Then Return "REST"
> If degradedThisRun Then Return "DEGRADED"
> If Not feedExists OrElse Not feedConnected Then Return "DOWN"
> Return "OK"
> ```
>
> **Its four inputs are: transport mode, the per-run stale-stream REST-fallback flag, and socket connectivity. Not one of them reflects TRADE FLOW or a STORE WRITE.**
>
> ✅ **So `OK` means "the socket is up and this run did not fall back". It does not mean capture is healthy, and it cannot.** That explains the 2026-08-11 discrepancy exactly: the socket recovered at **09:30:39** (→ `OK`) while the tape stayed empty until **10:00:12** — ~30 minutes of `OK` over zero capture. The ~5 minutes *before* the `DEGRADED` marker are the same defect at the other end.
>
> ⚠ **Consequence for the open queue item:** the fix is not to make the classifier faster. **An hour the box could not capture can never be visible in a log whose inputs do not include capture.** Either the S1 join stops treating `OK` as capture evidence, or `DeriveWsHealth` gains a capture-liveness input — `TradeStoreWriter.LastFlushUtc` already exists and is exactly that signal. **That is a design choice and it belongs in that item's spec.**

**The two boxes were 3 m 15 s apart this time** (AWS 16:02:41, local 16:05:56), against 1.6 s at the v65 deploy. **That gap does not matter here and it is worth saying why:** the v65 deploy was a *version* edge, where a wide gap leaves a mixed-version fleet and an unresolvable straddle. This deploy moved code only, with both boxes on v65 either side, so a row logged in that 3-minute window is scored identically on both. **Judge deploy-gap tightness by whether `settings.json` moved, not by habit.**

> ⚠ **Consequence for the D3 ASIA watch, and it is easy to get wrong.** The armed-ASIA (v65) population on AWS spans **four** instance ids — `09c747f8…` → `ec487909…` → `ffced26c…` → `d8678d2b…` — and **six** across both boxes, since local adds `3916540f…` and `ad7cadf4…`. **All six are v65 and all six are armed.** A pooled ASIA read filtering on `09c747f8` alone silently drops everything after 2026-08-07. The version edge is still only the v64→v65 one on 2026-08-01; every id after that is a restart.
>
> ⚠⚠ **This table is hand-maintained and it has now missed an entry once** — `ffced26c…` was found by a data read on 2026-08-11, not recorded at the restart. **The check that catches it costs one command:** diff the distinct `InstanceId` values in the copied-back `analysis_log.csv` against this table at every copy-back (step 7 of §4b already says to, and that step was skipped). **If AWS ever moves to a scheduled start/stop, a hand-maintained ledger stops being viable at roughly one new id per day** — at that point the `SettingsVersion` per-row CSV column in [`trader-tick-queue.md`](trader-tick-queue.md) §3 stops being a rider and becomes a prerequisite.
>
> ⛔⛔ **THAT CHECK HAS A HOLE, found 2026-09-21: it cannot see an instance that wrote ZERO CSV rows.** The diff reads distinct `InstanceId` values **out of `analysis_log.csv`**, so an instance that started, failed to collect and was killed contributes nothing to diff against. `0ee29530-16db-4382-a351-745fcd66c826` is exactly that shape and is in this table **only because it was hand-written once.** ⚠ **A zero-row instance is not a curiosity — it is what a BROKEN collector looks like**, so it is precisely the entry you most want and the one the check is blindest to. ⭐ **The cheap cover is a second source: `ws_health.log` gets a line from every process that starts, including one that never collects.** Diff the distinct ids in **both** `analysis_log.csv` and `ws_health.log` against this table at every copy-back. `capture_marker.log` is a third. **No instrument yet does this — it is a manual step until one does.**
>
> **A robust way to split the D3 boundary WITHOUT this table, discovered during the 2026-08-11 watch read:** the classifier itself carries the edge. On armed ASIA rows `AggrVelSignal = BURST_*` agrees exactly with `AggrVelBurstRatio ≥ 5.5`; on unarmed ASIA rows the minimum burst ratio among fires is **2.507**, the old exploratory default. **The arming state is therefore recoverable from the data alone**, which makes it a check on this ledger rather than a dependant of it.

**The v65 edge is unusually clean and worth recording as the standard to hit.** AWS went down at **19:02:31.111Z** and local at **19:02:32.710Z** — **1.6 seconds apart** — so the two-box mixed-version window is effectively zero, and no pooled ASIA read spanning the boundary has a straddle to resolve beyond the id split itself. Contrast the alternative the §4.5 correction warns about: had the settings been dropped onto either running box, the version would have changed mid-`InstanceId` and no split would have been possible at all.

**Reading `ws_health.log` correctly** (it is easy to over- or under-read, and both happened during the v64 landing): the process-start line comes from `WsHealthLog.LogStart` and fires unconditionally, so **a lone `DOWN` means the app started and nothing else has happened**. The following `OK` comes from `LogWsHealthTransitionForRun`, which runs **for every completed run, success or skip** — so **an `OK` line is positive evidence that at least one analysis run completed.** It is *not* evidence the auto-run timer is still ticking; only `analysis_log.csv` row growth shows that.

⚠ **The v64→v65 edge is a SCORING boundary.** ASIA rows under a v64 id carry an *unarmed* TFI burst vote; ASIA rows under a v65 id carry an armed one, and the two are byte-identical in shape. **Any ASIA read spanning 2026-08-01/02 must split on this table**, or the D3 watch reads its own contamination.

### 5c. ⭐⭐ FIRST `ws_feed.log` READ — 2026-09-22 (UTC). It sees four reconnects that §5b's instrument CANNOT

**The 2026-09-22 06:00 UTC scheduled check, run 8 h late at 14:05 UTC** ([`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §0). ⭐ **First read of `ws_feed.log` since it was built and deployed 2026-09-21 15:38.**

⛔ **The check as written could not be completed by `collector.ps1 status`.** The handover says each check reads `ws_feed.log`; `status` never displays it. It is in `$FetchFiles` ([`../tools/ops/collector.ps1:132`](../tools/ops/collector.ps1)), so **only `fetch` retrieves it**. Fix the handover's wording, or surface it in `status`.

**Health, instance `ee159d03…`, 22.5 h since the WPAD recovery:**

| Criterion | Reading | Verdict |
|---|---|---|
| Threads | **12** | ✅ band is ~10–20; the 2026-09-18 leak reached 11,630 in four hours |
| `analysis_log.csv` | last row 14:10:01, gap **0.7 min**, 16,075 rows | ✅ advancing |
| `ws_health.log` | `OK` 2026-09-21 15:39:49, **no transition since** | ⚠ see below |
| Settings / overlay | **v68** / `False` | ✅ correct for AWS |

#### ⛔ The finding: four reconnects, none of them in `ws_health.log`

| # | UTC | Signature |
|---|---|---|
| 1 | 2026-09-21 22:28:33 | `remote party closed the WebSocket without completing the close handshake` → `reconnecting in 1s` → connected → `subscribed to 7 channels` |
| 2 | 2026-09-22 04:50:28 | identical |
| 3 | 2026-09-22 06:04:52 | identical |
| 4 | 2026-09-22 06:38:41 | identical |

**291 lines in the file, 267 of them heartbeats. Every non-heartbeat line is above.**

⭐ **This is §5b's mechanism box confirmed by a second instrument.** `DeriveWsHealth`'s four inputs do not include trade flow or a store write, so a sub-second reconnect that re-seeds successfully never moves the health state. **`ws_health.log` is not wrong — it is measuring something else, and `ws_feed.log` is what closes the gap.** Relevant to the open `ws_health.log` under-reporting row in [`trader-tick-queue.md`](trader-tick-queue.md) §2.

⭐⭐ **NO TAPE WAS LOST, and that is independently corroborated, not assumed.** The post-fetch venue check over 2026-09-22 01:00→14:00 UTC returned **`CLEAN — missing=0, venue=109704, pages=110, seq_contiguous=true`**. Four reconnects, each re-seeding via REST, zero missing trades. **The reconnect-and-reseed design works and can now be seen working.**

#### ⚠ `pagesOUT/s` — UNRESOLVED, deliberately not called either way

| Reading (UTC) | avg / max | avail | app PVT |
|---|---|---|---|
| 14:05 | 773.1 / 4,276.6 | 101 MB | 92 MB |
| 14:07 | 449.4 / 2,453.1 | 87 MB | 119 MB |

⛔ **This is NOT §5b-era's recorded false alarm**, which read 726.8 / 5,108 and then **0 across 15/15 samples** four minutes later ([`seat-handover-2026-08-23.md`](seat-handover-2026-08-23.md)). Mine stayed non-zero across both.

⛔ **But it cannot be called thrashing either.** [`hostel-app-colocation-assessment.md`](hostel-app-colocation-assessment.md) §10.1 records a burst *"caused by the measuring probe itself"*, and the box has 87–101 MB free of 1,024 MB. **Two readings from the same probe cannot separate box pressure from probe-induced pressure.** Resolving it needs a detached instrument. **Neither alarmed nor cleared — recorded.**

### 5d. The 2026-09-24 decision-point check — HEALTHY (run 08:45 UTC, 2 h 45 m late)

Run by a scheduled read-only session; the app was not open at 06:00. Fetch `aws_fetch\20260924-084613`. Nothing deployed, restarted or edited.

| Criterion | Reading | Verdict |
|---|---|---|
| Threads | 13 (a single reading) | ✅ |
| `analysis_log.csv` | last row 08:45:01, gap 1.0 min, 17,752 rows | ✅ advancing |
| Settings / overlay | v68 / `False` | ✅ |
| Restarts | none: `ee159d03…` since 2026-09-21 15:39 in both `analysis_log.csv` and `ws_health.log` | ✅ |
| Defender 02:00 windows, 2026-09-22 to 09-24 | 15 rows each at a steady 3-min cadence; no gap and no reconnect in any window | ✅ |
| Venue check | `CLEAN — missing=0 venue=110502 pages=111 seq_contiguous=true` | ✅ |
| `ws_feed.log` | 4 more sub-2 s reconnects (8 in about 65 h), none seen by `ws_health.log` | ⚠ as §5c |
| `pagesOUT/s` | 0.0 / 0.0 over 30 s | recorded |

⛔ **A clean read does not prove the re-entrancy gates work under load.** No scan produced a stall to test them against, and there is no thread series through the 02:00 windows. This bears on whether the UI-thread liveness heartbeat is still worth building ([`seat-handover-2026-09-21.md`](seat-handover-2026-09-21.md) §0).

⚠ **New observation, not investigated: the same `trade_seq` holes are repaired on several passes.** For example, `300377526..300377528` was repaired at 2026-09-22 15:39, 21:39 and 2026-09-23 03:39, each pass `PASS_CLEAN`. Either the repair does not persist, or detection finds the same hole again inside `lookback_h=20`. Tape is complete (venue check `CLEAN`). Queued in [`trader-tick-queue.md`](trader-tick-queue.md) §2.

---

### 5a. v64 deploy — trader-executed 2026-08-01, verified

**This is the moment raw-trade capture began, anywhere.** Under D1 (AWS-only) this box is the sole capturer, and tape older than ~24 h is unobtainable at any price — so everything before 2026-08-01 17:50 UTC is permanently absent from the store, by design and not by defect. That gap is the argument the v64 build was written on.

Verified at deploy:

- **Title bar `settings v64` with NO `+local`** — the §3 glance in its inverted AWS form. The overlay correctly did not travel.
- **`backtest_data\` appeared** — the first observation anywhere of a trade going from the WS stream to disk. The [v64 review](trade-store-capture-review-2026-07-31.md) §5 listed exactly this as unverifiable without a live run; the local box could not close it because capture is off there by ruling.
- **`ws_health.log` DOWN → OK in 18 s**, new id above.
- **The perf strip still reads `Cur.Wk 43% · 3d 53%`** — which is the useful negative: AWS's own eval cache and book **survived the overwrite**. A wipe-and-replace would have blanked it. Confirms the deploy overwrote files rather than replacing the directory, per §1.
- **Both boxes now on v64** ⇒ the §5 same-settings discipline holds and rows stay poolable across the deploy. v64 added seven `trade_store` keys and changed no other value, so nothing tunable moved.
