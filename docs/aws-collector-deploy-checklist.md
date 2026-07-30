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
| **Local `bin\Debug`** | **`false`** | The local box is intermittent (trader, 2026-07-31), so its store would be a partial book nobody reads, growing ~1.4 GB/year in a directory nobody watches. Capture there also recreates the dual-box topology D1 explicitly rejected. |

**The tracked `settings.json` carries `true`, and that is deliberate — do not "fix" it.** §1.1 deploys AWS from the Release build output, so the tracked value *is* what lands on AWS. The two failure directions are not symmetric:

- Tracked `false`, AWS not corrected after a deploy ⇒ **the only capturing box silently stops. Tape is lost permanently.**
- Tracked `true`, local not corrected after a rebuild ⇒ local captures again. Costs disk. Nothing is lost.

So the tracked seed carries the value that is *safe when it propagates*, and the **local box is the exception that gets edited by hand** — the mirror of the v57 stomp-proofing decision, pointing the other way because here it is AWS, not local, that must never be stomped.

**Applying it locally, and the chore it creates.** `settings.json` is `CopyToOutputDirectory=PreserveNewest`, so **every Debug build with a newer tracked file overwrites `bin\Debug\net8.0-windows\settings.json`** and restores `true`. Therefore:

1. Build Debug **first**, then set `"enabled": false` inside the `trade_store` block of `bin\Debug\net8.0-windows\settings.json`, then start the collector. Editing before the build is wasted — the build stomps it.
2. **Re-apply after every settings-version bump**, since that is exactly when the tracked file becomes newer. If it is missed, the symptom is a growing `bin\Debug\net8.0-windows\backtest_data\` — harmless, and safe to delete.

**Durable fix, not built:** a gitignored `settings.local.json` overlay applied after `settings.json` would make per-box divergence survive builds and retire this chore. It is a real code change and a design decision, so it wants a spec first — flagged here rather than done.

## 2. Run 24/7 (WinForms — needs an interactive session)

- Auto-logon enabled + a Startup-folder shortcut to the exe (survives the reboots Windows Update WILL force; defer updates where the AMI allows).
- On the app: set auto-run REPEAT + ON-CLOSE, start it, then **disconnect RDP — do not log off** (logoff kills the GUI session).
- No crash watchdog exists: a crash stops collection until someone RDPs in. The WS feed reconnects itself; app death does not.

## 3. Daily one-glance health check (RDP in, ~30 seconds)

- TAPE strip alive + `[B]` strip populated → collecting.
- `ws_health.log` tail → any DOWN/DEGRADED transitions overnight (transitions-only, so a short file is a healthy file).
- **`liq_events.log` existence** → the AWS box runs the A4 cascade instrument 24/7 too — it may catch the first cascade before the local box does. A CASCADE line here counts as the A4 gate evidence (pool both boxes' sidecars).
- **[v64] `backtest_data\` newest-file mtime is advancing** → capture is alive. **This is the item on this list that now carries real data risk.** Under D1 there is no second capturing box, so this glance is the only thing standing between an unnoticed app death and permanently lost tape — trades older than ~24 h cannot be refetched, by anyone, at any price. A stale mtime with the app otherwise healthy points at `trade_store.enabled` having been reset to `false` by a redeploy (§1a) or at the store path being unwritable. Everything else on this list is recoverable; this one is not.

## 4. Copy-back / pooled-read recipe (at analysis time)

1. Copy the AWS `analysis_log.csv` back as `analysis_log_aws.csv` (never overwrite the local file).
2. Concat for pooled reads: local CSV + AWS CSV minus its header line (both v0.8/111-col schema — verify header equality first; if a rotation happened on one box only, DO NOT pool until both are on the same schema).
3. Provenance: `InstanceId` distinguishes boxes; the standing exclusions apply as always (weekday-only, burst instances).
3b. **Cross-box dedup (RULED 2026-07-29, trader tick "as recommended"):** both boxes fire on the same bar closes, so overlapping-hours rows are near-duplicate observations — **pooled STATISTICAL reads must not double-count them**. Rule: **local-preferred** — for any (UTC session-hour) where the local book has rows, use ONLY local rows; AWS rows fill the hours local missed. Applied at pooled-snapshot construction (the read-time concat step above), so NO tool changes — the CeilingAudit/report/what-if consume the deduped snapshot as an ordinary CSV. Coverage-map reads (row counts per box) and single-box reads are unaffected.
4. Eval caches do NOT pool (each box's tracker walks its own book); offline re-walks via the report/what-if tools regenerate outcomes from the pooled CSV + fresh OHLC — the more trustworthy surface anyway (§7a).
5. **Same-settings discipline:** rows are only poolable while both boxes run the same settings version. After any settings bump locally, redeploy to AWS at the next opportunity; the CSV's settings-version column + `InstanceId` make any straddle visible and filterable.

## 5. Decommission / handover note

The box owes nothing at end-of-life beyond a final copy-back of `analysis_log.csv`, `liq_events.log`, and `ws_health.log`. **DEPLOYED 2026-07-23 (trader-executed; CSVs sighted and populating).** Engine `InstanceId` (recorded at first copy-back, 2026-07-27 08:18 UTC): `4325cb7e-c21e-444d-b6c4-b355178776cf` (deploy-evening run, 181 rows, 07-22 16:24–19:24 UTC) · `fb908147-0312-4c55-b9d1-a23be310256e` (the standing collector, since 07-22 19:25 UTC). Note: every app restart mints a new id — the authoritative provenance set is the distinct `InstanceId` values in `analysis_log_aws.csv` at each copy-back; re-record any new ids that appear.
