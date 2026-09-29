# Spec-back — raw trades channel test for the real-time liquidation flag (HH-2)

**Reviewer model + effort:** Opus 5.5, medium. The result is a count against a small sample, and the two review risks are (a) the judge's slice edges and (b) whether the run was long enough to say anything. Both need judgement, not lookup. Escalate to high if the result is "raw carries the flag" and someone proposes scoring on it.

**Author seat:** Sonnet 5.5, medium (implementer). **Brief:** the HH-2 implementer brief, ruled 2026-09-28 (`docs/history-host-and-raw-channel-read-2026-09-28.md` §4, ruling `HH-2` = (a)).
**Code:** commit `ef21aaf`, local, not pushed, tagged `[no-engine-change]`. **Status of this file: DRAFT written while the run is live (2026-09-29 ~08:40 UTC). Section 2 is PENDING and is filled when the run's summary prints (~17:55 UTC).**

---

## 0. Status at time of writing

| Item | Value |
|---|---|
| Run | `C:\probe-runs\rawliq-run1\`, scheduled task `rawliq-run1`, started 2026-09-29 08:24:15 UTC |
| Collection ends | about 16:24 UTC (28,800 s) |
| Final summary | about 17:55 UTC, after the drain judges the last 90-minute slice |
| Verified live at 08:35 UTC | process alive, 19 MB private, `auth OK`, three channels `ACCEPTED`, deliveries 1,795 / 1,795 / 1,787 (100ms / agg2 / raw), 0 reconnects |
| Result | **PENDING** — see section 2 |

⛔ **Do not read section 1 as a result.** The smoke runs and the first 10 minutes of the run held zero liquidations. They prove the instrument runs, not what raw carries.

---

## 1. Ranked verification handles

⭐ **If you run only one: `H-2`.** It re-derives the headline count from the per-trade file, with no trust in the summary code.

| ID | Kind | Handle | What it confirms | Status |
|---|---|---|---|---|
| `H-1` | runnable | `dotnet build tools/WsTradeProbe/WsTradeProbe.vbproj -c Release` then `powershell -NoProfile -File tools/checks/verify-gate.ps1` | 0 errors, 0 warnings; the gate's last line is `GATE PASSED` | Run 2026-09-29: both passed |
| `H-2` | runnable, after the run | Count lines in `C:\probe-runs\rawliq-run1\rawliq_flagged_*.jsonl` whose `kind` is `history_flagged` and whose `channels.raw.carried_flag_at_delivery` is `true`; compare with the summary's `raw carried-at-delivery` and with the history-flagged total | The headline count. Also check the identity: carried + delivered-without-flag + never-delivered = history-flagged, for the `raw` row | PENDING |
| `H-3` | runnable | `git grep -nE "DERIBIT_RO_CLIENT_SECRET\|access_token\|refresh_token" -- tools/` then read each hit; then grep the run folder for the literal strings `access_token`, `refresh_token`, `client_secret` (expect 0 files) | No write path emits a credential. Every code hit is a read, a request builder, a response parser or the redactor | Code half run 2026-09-29 (8 hits, all reads or redaction). Run-folder half: 0 files on the smoke run and 0 files on the live run at 08:40 UTC (12 min in). Re-run after the run ends |
| `H-4` | runnable | Read `tools/WsTradeProbe/RawChannelProbe.vb`, `HandleResponseAsync`: the branch that matches the auth and refresh ids returns before any generic logging | Auth frames never reach a log line | Read by the author only |
| `H-5` | runnable, after the run | In the summary, `delivered trades absent on history` and `unjudged entries expired / cap-evicted` both read 0 | The judge compared like with like: every delivered trade exists on the history host, and nothing was dropped unjudged | PENDING |
| `E-1` | evidence, cannot be re-run | The two smoke runs (`C:\probe-runs\rawliq-smoke-20260929-081255\`, `rawliq-smoke2-…`): auth OK, raw accepted on `public/subscribe`, 3 refreshes at a 40 s test cap with 0 reconnects | The refresh path works. The token lasts a year (`expires_in=31536000`), so refresh never fires in a real run | Held; the logs stay on disk, but the test cap is a run argument, not a fixture |

---

## 2. THE RESULT — PENDING

Fill when the summary prints. Required content:

- **Does raw carry `liquidation` at first delivery?** yes / no / inconclusive, with counts: history-flagged trades judged, raw carried, delivered without the flag, never delivered, censored by an outage.
- **The same three counts for 100ms and agg2**, from the summary table. The old public-channel finding (flag absent) should reproduce; if it does not, say so.
- **Delay:** for each flagged trade, the raw delivery delay in ms (from `rawliq_flagged_*.jsonl`, field `delivery_delay_ms`). Note the dev-machine clock reads 0.2–0.9 s behind the venue, so small delays are not precise.
- **Later-delivery flags:** the summary's `later-delivery-flagged` column. A non-zero value means the flag showed up on a repeat delivery of the same trade, which is a different answer from "at first delivery".
- **Fields raw has that the history host lacks:** see the finding below; confirm it against the final tally.
- **Rule:** if history-flagged trades judged is 0, the result is INCONCLUSIVE and the run is extended. It is not a "no".

**One finding already in hand (smoke run 2026-09-29, 260 judged trades; repeated in the live run's sample file at 08:40 UTC: 15 of 15 sample objects carry `starbase_match_id`):** `starbase_match_id` and `starbase_timestamp` are present on all three channels and never on the history host. ⚠ They are on the **public** 100ms and agg2 channels too, so they are not a raw-only field. The engine parses neither. This answers the brief's second question, with a caveat: the stored copy of these fields was not compared, and their meaning is unknown to this seat.

---

## 3. Decisions taken (implementer, one line each)

| Decision | Options | Picked | Why |
|---|---|---|---|
| Where the code lives | edit `LiqFlagProbe.vb` · new file `RawChannelProbe.vb` | new file, new mode `rawliq` | The AWS probe is built from `LiqFlagProbe.vb` and must stay unauthenticated. The brief said do not touch that probe. A new file keeps auth code out of it |
| REST arm | keep · disable | disabled | The brief allowed it. It cannot see a flag that arrives about 60 minutes late |
| Judging shape | per-trade cohort by index membership · contiguous venue-time slices | slices | A cohort cannot tell "never delivered" from "already judged and evicted". A slice can |
| Slice seq span | history time endpoint · seq span from delivered trades | seq span from delivered trades | The brief forbids paging by timestamp. Cost: a flagged trade just outside the first or last delivered seq of a slice is not searched |
| Judge age | 90 min fixed · configurable | configurable, default 90 | A smoke run needs 1 min. The real run uses the default |
| Raw refused after auth | continue with the other two arms · stop the run | stop the run, exit code 5, after one retry on `private/subscribe` | The other two arms answer nothing. Collecting them in silence is the failure the brief named |
| Drain after collection | stop at once · keep judging until the last slice is 90 min old | drain | Without it the last 90 minutes of the run are never judged |
| Outage handling | ignore · record windows | record; a history-flagged trade inside one is `censored`, not `never delivered` | A disconnect must not read as a raw miss |
| Refresh test hook | none · optional third argument | optional third argument, test only | Tokens last a year, so the refresh path cannot be tested otherwise. It is off in the real run |
| Keep-awake | change the power plan · `SetThreadExecutionState` in the probe | the probe call | It ends with the process and changes no system setting |
| Detach method | `Start-Process` · scheduled task | scheduled task with the interactive flag | It leaves this session's process tree and loads the user's environment variables, so the key never goes into a file |

---

## 4. Feedback on the brief

**What worked:**
- The three explicit hand-offs (`H-1` to `H-4`, secret rules, "INCONCLUSIVE means extend") left almost no design questions.
- Naming the history host's seq-widening rule in `docs/history-host-and-raw-channel-read-2026-09-28.md` §5a saved an experiment. De-duplicating on `trade_seq` worked first time.
- "Redact any message that carries them before it reaches any dump" was right, and it shaped the design: auth frames are handled before any logging.

**Where the brief was narrower or wrong:**
- **`H-3` as written asked the implementer to grep for the first 6 characters of the client id, then retracted it mid-sentence.** The retraction is correct. Recommend deleting the dead clause; a reader could still run the first version.
- **"Refresh at ~80 % of `expires_in`" assumed short-lived tokens.** The measured value is a year. The refresh code is correct, but a real run never exercises it. Say so in the brief, or ask for a test cap.
- **The commit trailer said "Claude Opus 5.5" while the seat line said Sonnet 5.** The commit carries the brief's trailer; the author was Sonnet 5.5. Decide which is wanted.
- **"6–8 h, ideally spanning the NY session" and "start now" conflict.** The run was launched at 08:24 UTC for 8 h, so it covers 13:00–16:24 UTC of the NY session, not 13:00–23:00. A 4-liquidation-per-day base rate makes an inconclusive result plausible. If it is inconclusive, the cheaper extension is a second run started at about 12:30 UTC, not a longer first run.
- **The brief asked for "history-flagged trades a channel never delivered".** That needs a definition for a disconnect. The implementation records outage windows and counts a trade inside one as censored. Say if you want a different rule.

**Constraint pair that nearly conflicted:** "launch detached, outside this session's process tree" and "read the key only from env vars". A process started through WMI does not inherit user environment variables. A scheduled task run as the interactive user does. That is the hatch; write it into any future brief.

---

## 5. What I did not verify

- **The result itself** — pending (section 2).
- **Reconnect and re-authentication.** No disconnect was forced. The code path exists (`WsSupervisorAsync` re-sends the auth request on every connect) and was read, not run. If the real run reconnects, the log will show it.
- **A flag arriving later than the judge's minimum age (90 min).** The 2026-09-28 watcher saw about 60 minutes (n = 1) and could not see beyond 90.
- **Slice edges.** A history-flagged trade lying before the first or after the last delivered seq of a slice is not searched. Expected size: a few seconds of tape per slice. Not measured.
- **The 400-byte-per-entry index estimate.** The real private working set (19 MB at 10 minutes) is printed every 5 minutes. The estimate is generous; I did not compare it with a measured per-entry size.
- **Whether the 100ms and agg2 objects can be told from raw by content.** The summary compares property names only.
- **Whether the dev machine stays awake.** The keep-awake call blocks idle sleep. It does not block a manual sleep, a logoff, a shutdown or a Windows restart. The trader must not do those until after 17:55 UTC.
- **`starbase_*` meaning and the stored-copy comparison** (section 2).
- **Clock skew.** Receive delays run negative by 0.2–0.9 s; the skew was not measured against a time source.
