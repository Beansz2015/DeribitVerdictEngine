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

**Applying it locally — the overlay, since 2026-08-02.** The hand-edit chore below is **retired**. `bin\Debug\net8.0-windows\settings.local.json` holds

```json
{"trade_store": {"enabled": false}}
```

and `SettingsLoader` deep-merges it over `settings.json` at load. The file is gitignored and is not a project item, so **no build copies over it** — `PreserveNewest` can refresh the tracked settings and the merge still resolves capture to `false`. Two things to know:

1. **Place it BEFORE the first build that carries a newer tracked `settings.json`**, not after. Order is the whole point; get it backwards once and the build that was meant to be protected is the one that captures.
2. **`dotnet clean` deletes it along with the rest of `bin\`, and that failure direction is the bad one** — losing the overlay silently switches capture back **on**. §3's `+local` glance is what makes its absence visible.

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

## 5. Decommission / handover note

The box owes nothing at end-of-life beyond a final copy-back of `analysis_log.csv`, `liq_events.log`, and `ws_health.log`. **DEPLOYED 2026-07-23 (trader-executed; CSVs sighted and populating).** Engine `InstanceId` (recorded at first copy-back, 2026-07-27 08:18 UTC): `4325cb7e-c21e-444d-b6c4-b355178776cf` (deploy-evening run, 181 rows, 07-22 16:24–19:24 UTC) · `fb908147-0312-4c55-b9d1-a23be310256e` (the standing collector, since 07-22 19:25 UTC). Note: every app restart mints a new id — the authoritative provenance set is the distinct `InstanceId` values in `analysis_log_aws.csv` at each copy-back; re-record any new ids that appear.

**Subsequent ids, from `ws_health.log`:** `0efcda74-6b75-4d5f-af04-f3875b5afd8e` (the v63 redeploy, 2026-07-30 16:41 UTC — not a silent death, despite one 2026-08-01 read calling it that) · **`5a3afd99-6db4-461c-886e-dddcca3d8c62` (the v64 deploy, 2026-08-01 17:49:55 UTC DOWN → 17:50:13 OK, an 18-second connect).**

### 5a. Version ↔ InstanceId ledger — **BOTH boxes.** Load-bearing, not a nicety

**Added 2026-08-02 with the §4.5 correction.** There is **no settings-version column in the CSV**, so this table is the *only* place a version straddle can be reconstructed from. It covers **both** boxes because both books pool. **Record a row at every deploy and every restart-for-a-bump.** The [restart discipline](#4-copy-back--pooled-read-recipe-at-analysis-time) is what keeps it sound: stop → swap settings → start, so the version edge and the new id are the same instant and no row straddles a version inside an instance.

| Box | InstanceId | Settings | From (UTC) | Note |
|---|---|---|---|---|
| AWS | `0efcda74-6b75-4d5f-af04-f3875b5afd8e` | v63 | 2026-07-30 16:41 | |
| AWS | `5a3afd99-6db4-461c-886e-dddcca3d8c62` | **v64** | 2026-08-01 17:49:55 | 18-second connect. **First raw-trade capture anywhere** |
| AWS | *(pending — read from `ws_health_aws.log`)* | **v65** | 2026-08-02 | **The D3 deploy.** ⚠ ASIA aggressor velocity ARMED from this id onward |
| local | `2f8c9fe1-8325-4fbb-9ee5-41fc267e1efd` | v64 | 2026-08-01 18:00:26 | capture OFF via overlay |
| local | `a4333d00-2b3e-43fd-9226-32184761f4f6` | **v65** | 2026-08-01 18:58:37 | short run, ~4 min |
| local | `3916540f-6bc9-4648-ad6c-26bd65cfa462` | **v65** | 2026-08-01 19:02:32 | current. Title `settings v65 +local` |

⚠ **The v64→v65 edge is a SCORING boundary.** ASIA rows under a v64 id carry an *unarmed* TFI burst vote; ASIA rows under a v65 id carry an armed one, and the two are byte-identical in shape. **Any ASIA read spanning 2026-08-01/02 must split on this table**, or the D3 watch reads its own contamination.

### 5a. v64 deploy — trader-executed 2026-08-01, verified

**This is the moment raw-trade capture began, anywhere.** Under D1 (AWS-only) this box is the sole capturer, and tape older than ~24 h is unobtainable at any price — so everything before 2026-08-01 17:50 UTC is permanently absent from the store, by design and not by defect. That gap is the argument the v64 build was written on.

Verified at deploy:

- **Title bar `settings v64` with NO `+local`** — the §3 glance in its inverted AWS form. The overlay correctly did not travel.
- **`backtest_data\` appeared** — the first observation anywhere of a trade going from the WS stream to disk. The [v64 review](trade-store-capture-review-2026-07-31.md) §5 listed exactly this as unverifiable without a live run; the local box could not close it because capture is off there by ruling.
- **`ws_health.log` DOWN → OK in 18 s**, new id above.
- **The perf strip still reads `Cur.Wk 43% · 3d 53%`** — which is the useful negative: AWS's own eval cache and book **survived the overwrite**. A wipe-and-replace would have blanked it. Confirms the deploy overwrote files rather than replacing the directory, per §1.
- **Both boxes now on v64** ⇒ the §5 same-settings discipline holds and rows stay poolable across the deploy. v64 added seven `trade_store` keys and changed no other value, so nothing tunable moved.
