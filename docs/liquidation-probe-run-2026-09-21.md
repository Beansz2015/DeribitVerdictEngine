# Liquidation-flag probe — run record, 2026-09-21 (UTC)

**Status: RUN 1 DIED. RUN 2 (dev machine) STOPPED. RUN 3 (box) STOPPED — its REST arm froze. ⭐ RUN 4 RUNNING ON THE AWS COLLECTOR BOX since 2026-09-24 16:53 UTC (PID 11248). The measurement has NOT returned yet.**

## 000. Run 3's defect and run 4 (added 2026-09-24 UTC)

| Item | Value |
|---|---|
| Run 3 window | 2026-09-24 13:58:49 → about 16:52 UTC, stopped through `STOP` |
| ⛔ Defect | **Arm 2 froze at 181 REST polls, about 31 min in, with no log line.** `HttpClient.Timeout` throws `TaskCanceledException`, an `OperationCanceledException`. The loop's cancel arm caught it and returned. The slow, paging box made a poll exceed 20 s; the fast dev machine never did |
| Fix | `eff6def`: only a real stop request ends the loop. Timeouts and ingest errors are logged and counted. The 5-minute status line adds `rest errors`, `index` and `priv MB` |
| Run 3 summary | 181 polls, 181,000 REST trades, **0 flagged**, 0 pairings. Inconclusive |
| Run 3 memory | probe private 38 MB at +10 min, 111 MB at +170 min; box commit free 1,611 then 1,569 MB of 3,313; the collector wrote a row every minute throughout |
| Run 3 raw dump | file `_01` rotated at 64 MB. A `Get-ChildItem` listing showed 0 bytes while the file was open: a stale directory entry, not an empty file |
| ✅ Run 4 fix confirmed live | 17:38 UTC (+45 min): 257 REST polls, **`rest errors 1`**, and polling carried on past the error. That is the exact failure that ended run 3's arm. Probe private 38 MB (index 13,782), box commit free 1,625 MB, collector writing every minute |
| Run 4 | started 16:53 UTC, PID 11248, BelowNormal, built from `eff6def`. `run.cmd` now sets `DOTNET_GCHeapHardLimit=0xC800000` (200 MB), so a runaway heap kills the probe, not the box. Run 3's logs are kept as `run3-probe-console.log` and `run3-probe-stderr.log` |

## 00. ⭐ Run 3 — on the collector box (trader-ruled 2026-09-24 UTC)

⛔ **This overrides the build spec's "dev machine only, never the collector box" line** (`engine-fix-build-spec-2026-09-21.md` §4.1). **Trader ruling, 2026-09-24:** the dev machine shuts down daily, so the probe runs on the box, *"guarded — allow probe to run unless memory is predicted to almost completely run out by the time it is predicted to complete."*

| Item | Value |
|---|---|
| Box | `i-0d6c133058876273e`: 1,024 MB RAM, 1 vCPU; before launch 156 MB free, 1,648 MB commit free, 7.63 GB disk free |
| Directory | `C:\probe-runs\liq-2026-09-24\` on the box, outside `C:\DeribitEngine` |
| Binary | built from `951a36c`, copied through `s3://deribit-engine-bucket/probe/liq-2026-09-24/bin/` |
| Launch | a SYSTEM one-shot scheduled task `LiqProbe` (created, run, deleted), so there is no window and no tie to SSM. PID 42392 in `probe.pid`, priority **BelowNormal** |
| Guards built first (`951a36c`) | `IndexCap` 400,000 → 40,000. **The old cap would have held about 370 MB, more than the box's free RAM.** Measured on run 2: about 0.9 KB per entry. Raw dumps: keep the newest 8 files (about 512 MB) |
| Memory checks | at +10 min and +1 h after launch; results below |

**How to check it (from the dev machine):** send an SSM command that tails `C:\probe-runs\liq-2026-09-24\probe-console.log` and reads the probe's private MB. A status line prints every 5 minutes.
**How to stop it:** SSM `New-Item C:\probe-runs\liq-2026-09-24\STOP`. The probe prints its summary and exits within 30 s.
**How to fetch the result:** SSM `aws s3 cp` of `liq_probe_pairings_*.jsonl` and `probe-console.log` to `s3://deribit-engine-bucket/probe/liq-2026-09-24/out/`, then pull it down.

**Run 2 (dev machine), stopped 2026-09-24 about 14:00 UTC through `STOP`:** 185 REST polls, 185,000 REST trades scanned, **0 flagged**, 0 pairings. `liquidation` never present on `100ms` (25,949 trades). Inconclusive, as expected with no liquidation in the window.

## 0. ⛔ Run 1 died unnoticed — read before §1 (added 2026-09-24 UTC)

| Item | Value | How checked |
|---|---|---|
| Run 1 last status line | 2026-09-21 11:13:47 UTC, `up 110m`, `flagged 0`, `paired 0` | `probe-console.log` tail |
| Run 1 last raw write | 2026-09-21 11:16:18 UTC, **cut mid-line** | tail of `liq_probe_raw_20260921-092345_01.jsonl` |
| Run 1 summary | **None.** The process was killed, not stopped through `STOP` | no summary in the console log |
| Run 1 process 7368 | not running on 2026-09-24 | `tasklist` |
| Pairings | 0 (the file is empty) | `wc -l` |

- **Cause: not verified.** The machine never sleeps (power plan `High performance`, standby 0). The likeliest cause is that the launching seat's process tree was killed. A usage-limit stop does this to background agents (`seat-handover-2026-09-17.md` §5 lesson 6).
- ⛔ **Three handovers passed without noticing.** `seat-handover-2026-09-24.md` says "nothing is running", which was true but did not name the dead probe.
- **Also found:** `584c616` (the WPAD fix) missed this probe. Its WebSocket and HTTP clients stayed at the defaults. Fixed in `a3c9079` before run 2.

### Run 2

| Item | Value |
|---|---|
| Working directory | `C:\Dev\probe-runs\liq-2026-09-24\` |
| Started (UTC) | 2026-09-24 13:28:03 |
| Launch | `Win32_Process.Create` (WMI), so the process is not a child of any Claude session. `cmd.exe` (9556) runs `dotnet …WsTradeProbe.dll liq 0 10` (PID 25032, also in `probe.pid`) |
| Subscriptions at start | `100ms` ACCEPTED · `raw` REFUSED (`13778`, as finding `PB-1` in §2 below) · `agg2` ACCEPTED |

⚠ **Check it is alive at every seat start:** `tail -3 /c/Dev/probe-runs/liq-2026-09-24/probe-console.log`. The status line prints every 5 minutes, so a timestamp older than about 10 minutes means the probe is dead.

The §1 table below describes run 1. For run 2, replace the directory with `liq-2026-09-24`.
**Written:** 2026-09-21, `date -u` = `Mon Sep 21 09:06:19 UTC 2026` at session start.
**Session:** B1 of [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4.1, under [`docs/engine-fix-session-b1-probe-brief.md`](engine-fix-session-b1-probe-brief.md).
**Instrument commits:** `951cde0` (the probe) and `bb924c8` (the STOP-file shutdown). Both committed **before** the run started, per the brief's §3 order of work.

⛔ **This seat built the MEASUREMENT only. The `D-4`, `D-5` and `D-6` fix is Session B2 and goes to a different seat after this run returns.**

---

## 1. Where the run lives

| Item | Value |
|---|---|
| Working directory | `C:\Dev\probe-runs\liq-2026-09-21\` — **outside the repo, by design** |
| Started (UTC) | 2026-09-21 09:23:45 |
| Process id | recorded in `probe.pid` in that directory (7368 at launch) |
| Command | `dotnet …\WsTradeProbe\bin\Release\net8.0\WsTradeProbe.dll liq 0 10` |
| Arguments | `liq` selects this probe · `0` = run until stopped · `10` = REST poll seconds |
| Console log | `probe-console.log` (and `probe-stderr.log`) |
| Arm 1 raw dump | `liq_probe_raw_<stamp>_NN.jsonl`, rotating at 64 MB |
| Arm 2 pairings | `liq_probe_pairings_<stamp>.jsonl`, flushed on every pairing |

### How to check it

```bash
tail -40 /c/Dev/probe-runs/liq-2026-09-21/probe-console.log
```

```bash
cat /c/Dev/probe-runs/liq-2026-09-21/liq_probe_pairings_20260921-092345.jsonl
```

**A non-empty pairings file is the whole result.** It is empty until a liquidation passes.

### How to stop it

Drop a file named `STOP` in the working directory. The probe notices within 30 seconds, then prints the full run summary and exits.

```bash
touch /c/Dev/probe-runs/liq-2026-09-21/STOP
```

⛔ **Do not kill the process instead.** A kill skips the summary. The data files survive, but the per-channel subscription states and the field tallies are only printed by the summary.

---

## 2. What is already measured — and what it does NOT settle

⭐ **Two findings arrived while building the instrument. Both change what arm 3 can say, and neither is the answer to `D-4`.**

| Id | Finding | How it was checked |
|---|---|---|
| `PB-1` | ⛔ **`trades.BTC-PERPETUAL.raw` is REFUSED to an unauthorized client** — error `13778 raw_subscriptions_not_available_for_unauthorized`. The probe is forbidden to authenticate, so **arm 3 as specified in the build spec CANNOT RUN** | Live subscribe, three separate runs, 2026-09-21 09:13 to 09:23 UTC. The refusal is printed in `probe-console.log` on every run |
| `PB-2` | ⛔ **A single `public/subscribe` naming several channels is rejected WHOLE when any one channel is refused.** Arm 3 therefore killed arms 1 and 2 in silence — the first smoke run collected **zero trades over 90 seconds of live market** and printed no error | The first smoke run, 2026-09-21 09:11 UTC. Fixed by subscribing each channel in its own request, and by logging every control frame |

**`PB-2` is the one worth carrying.** The probe originally reported the run honestly and the report was useless: a rejected subscription and a quiet market are the same observation when nothing logs the control frames. The run summary now states each channel's subscription state, so **a REFUSED channel can never be read as an empty one**.

### Arm 3b — the substitute, named rather than silently swapped

`trades.BTC-PERPETUAL.agg2` is open to an unauthorized client and is now subscribed alongside. It is a **different aggregation of the same tape**.

- ✅ It separates *"aggregation drops the field"* from *"no trades channel carries it"* — which is the fork `D-4` turns on.
- ⛔ It does **not** answer what the unaggregated feed carries. Only an authenticated `.raw` subscription can, and that is outside this probe's safety contract.

### ⚠ What arm 1 cannot settle on its own — read this before quoting a field tally

**Over 301 streamed trade objects on `trades.BTC-PERPETUAL.100ms`, the `liquidation` property was NEVER present.** That sounds like the answer. **It is not.**

⛔ **REST omits `liquidation` on ordinary trades too.** Checked directly against `public/get_last_trades_by_instrument` on 2026-09-21 09:18 UTC: three consecutive trades carried the 13 keys `amount · contracts · direction · index_price · instrument_name · mark_price · price · starbase_match_id · starbase_timestamp · tick_direction · timestamp · trade_id · trade_seq`, and **no `liquidation` key at all** — yet finding `L-1` measured 91 REST trades that DO carry it.

⭐ **So the field is present only on liquidation trades, on both surfaces.** A tally showing "never present" across a sample containing no liquidation proves nothing. **Arm 2 is the only arm that can answer `D-4`**, exactly as the brief says.

---

## 3. The answer — TO BE FILLED WHEN ARM 2 PAIRS

⛔ **Do not fill this section from the field tallies. Fill it from the pairings file.**

| Question | Answer |
|---|---|
| Run window (UTC) | *pending* |
| Number of pairings | *pending* |
| Is `liquidation` absent from the streamed object, present under another name, or is the trade itself missing from the stream? | *pending* |

The three verdicts the pairing record can carry:

| Verdict in the pairings file | What it means for `D-4` |
|---|---|
| `PAIRED` | The streamed twin exists. Read `stream_100ms_has_liquidation_field` and the raw text beside it — this distinguishes "absent" from "renamed" |
| `NO_STREAMED_TRADE` | The trade never reached the stream at all. Neither `D-4` option (a) nor (b) as written would see it |
| `UNTRUSTED_RECONNECT` | The socket dropped while this trade was pending. **Says nothing.** Do not count it either way |

⚠ **Two pairings are wanted, not one.** One trade cannot separate a channel-wide omission from a one-off. Liquidations pass roughly twice a day.

⛔ **The escalation trigger, restated so the next reader does not have to look it up:** if the stream DOES carry a readable liquidation flag under its own name, that contradicts `L-1`'s 51,107-of-51,107 measurement and the whole `D-4` fix design changes. **Stop and report. Do not judge it mechanical.**

---

## 4. What this record did NOT verify

- **Whether the streamed copy of a liquidation trade carries the field.** That is the entire question and the run is still open.
- **Whether `.raw` would carry it.** The venue refuses the channel unauthorized, and this probe may not authenticate. **Unmeasured, and unmeasurable within the safety contract.**
- **Whether `agg2` differs from `100ms` on a liquidation trade.** Both channels are subscribed; no liquidation has been paired yet.
- **The REST field list across a liquidation trade.** The three-trade sample checked above contained no liquidation, so the flagged shape is carried from `L-1`, not re-measured here.
- **Long-run stability.** The WebSocket supervisor reconnects with backoff and the STOP path was tested against a live run, but the longest continuous run at the time of writing is minutes, not days.
