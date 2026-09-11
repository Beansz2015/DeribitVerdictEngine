# Venue-status instrument — the recorded source `C-3b` needs

**Status:** ⛔ **ONE DECISION OPEN (`D-1`). Everything else is ruled.** Build after `C-3a` ships.

**Author seat:** Opus, 2026-09-11 (UTC). **Baseline commit: `e84c0bf`.**

⚠ **`tools/BacktestRunner/CoverageReport.vb` IS BEING EDITED RIGHT NOW by the coverage-cluster implementer, so this document deliberately cites NO line numbers in that file.** Re-anchor the consumer half after [`coverage-report-cluster-spec.md`](coverage-report-cluster-spec.md) lands.

**Origin:** [`coverage-report-cluster-spec.md`](coverage-report-cluster-spec.md) §1.1 proved `C-3b` is **not buildable** — the queue row assumed a venue-maintenance hour was detectable from `ws_health.log` and it is not. **This is the missing prerequisite.**

---

## 0. Model and effort

> ### Model: **Sonnet** · Effort: **LOW** for the writer · **MEDIUM** if `D-1` is ruled (b)
> ### One session.

**Why LOW.** ⭐ **It has an exact in-repo template that already solves every hard part.** `Core/WsHealthLog.vb` is an append-only sidecar with the same contract: *"path = AppDomain BaseDirectory + filename, one line per event: `utc | state | instance_id` … No settings keys, no bump. Display/observation only — ZERO scoring impact."* **Copy that shape; do not invent one.**

⚠ **It becomes MEDIUM only under `D-1` (b)**, which changes how every market-data request is issued.

### 0.1 ⛔ Where the implementer will slip

| # | Trap | Why |
|---|---|---|
| **V-1** | ⛔⛔ **LOGGING OUR OWN FAILURE INSTEAD OF THE VENUE'S DECLARATION** | **This is the whole point of the instrument and the easiest thing to get wrong.** A timeout, a DNS failure, a socket drop or a generic network error is **indistinguishable from our box being broken.** ⛔ **If those get logged, the coverage report will scope out OUR OWN defects** — the [`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md) baseline failure in a new costume. **Log ONLY a response the venue itself returned** |
| **V-2** | ⛔ **The body is NOT reachable at the current catch site** | Every fetcher calls `_http.GetStringAsync(url)` (`DeribitClient.vb:98,152,197,216,233,267`). That throws `HttpRequestException`, which **carries `StatusCode` but DISCARDS the response body** — so Deribit's `11051` / `system_maintenance` code **cannot be read where the exception is caught today** (`DeribitClient.vb:38-51` (the `HttpRequestException` arm; the 4xx test is at `:40-42`)). **This is what `D-1` is about. Do not assume the body is available** |
| **V-3** | ⚠ **A new sidecar that nobody fetches is invisible** | `collector.ps1`'s `$FetchFiles` (line 117) is a **literal list, never a glob** — ruled `OPS-1`, 2026-09-06, and the reason is recorded there. **A new log not added to it never reaches the analysis box, and the consumer silently never fires** |
| **V-4** | ⛔ **Never throw from the log path** | `WsHealthLog`'s contract is *"never throws"*. **A market-data fetch must not fail because a diagnostic log could not be written** |
| **V-5** | ⛔ **The clock** | GMT+8 workstation, UTC project dates. **Run `date -u`** |

### 0.2 Escalation trigger

- **Any design that logs a line when the venue did not respond at all.** That is `V-1`.
- **Any need for a `settings.json` key.** The template needs none; if you think you need one, stop.
- ⛔ **Under `D-1` (b): any change in behaviour on the SUCCESS path.** A refactor of the request path must be provably zero-change for 200 responses.

---

## 1. What it must record, and why that exact thing

**The question `ws_health.log` cannot answer:** *"was the VENUE refusing, or were WE broken?"* **Both render as an empty store, and the coverage report currently calls both `defect`.**

| Property | Requirement |
|---|---|
| **Trigger** | A response **the venue itself returned** that says it is not serving |
| **Line shape** | ⭐ **Mirror `ws_health.log` exactly** — `utc \| state \| instance_id`. The coverage report already parses that format, which is what makes the consumer nearly free |
| **File** | `venue_status.log`, beside the CSV, `AppDomain.BaseDirectory` — same as `ws_health.log` |
| **Frequency** | **Transition-only**, plus nothing at start. Mirrors `WsHealthLog`; keeps the file tiny and needs no rotation |
| **Settings** | ⛔ **NONE. No key, no version bump.** The template is explicit that this class of sidecar takes none |

### 1.1 ⭐ REST only — the WS path is already covered, and duplicating it would be worse

✅ **Verified: `DeribitWsFeed.vb`'s error handling is generic (`Catch ex As Exception` → `Log(...)`, lines 129/142/185/215-217) with no venue-specific arm.** **A maintenance window makes the socket fail, and `ws_health.log` ALREADY records that as `DOWN`/`DEGRADED`.**

⛔ **Do NOT add a second signal on the WS side.** **The new log exists to say something `ws_health` cannot — that the refusal came from the VENUE.** A socket that will not connect cannot distinguish the two, so a WS-side line would be exactly the `V-1` defect.

---

## 2. ⛔ The one open decision

| # | Decision | Options | My read |
|---|---|---|---|
| **`D-1`** | **How is the venue's declaration obtained?** | **(a) HTTP 503 status alone**, logged at the existing catch site `DeribitClient.vb:38-51`. One line, zero change to the request path · **(b)** change `GetStringAsync` → `GetAsync` + read the body, giving the real `11051` / `system_maintenance` code · **(c)** on seeing a 503, fire ONE extra probe read to recover the body | ⚠⚠ **RESERVED — and I am flagging it under the ruling's own last class rather than taking it.** ⭐ **My read is (a), and I believe it is ADEQUATE rather than merely cheap:** a 503 **is** the venue's own response, so it is a positive record of the venue's state, not an inference from ours — which is all J-B requires. **For scoping an hour, "the venue was not serving" is sufficient; whether it was planned maintenance or an unplanned outage does not change whose defect it is.** ⛔ **But (a) IS the less-information option, and the ruling reserves exactly that, so it is yours.** ⚠ **(b) is the only one that yields `11051`, and it touches EVERY market-data fetch — the highest-blast-radius code in the app. I would not pay that for a diagnostic.** ⚠ **(c) adds a network call during an outage and recovers the same answer (b) does, for less risk and more moving parts** |

⭐ **Whatever is ruled, record the STATUS CODE in the log line** — e.g. state `VENUE_503` rather than a bare `DOWN`. **That keeps a slot for a richer signal later without a format change, and it is free under every option.**

### Decisions TAKEN under the auto-proceed ruling

| # | Decision | ✅ Taken |
|---|---|---|
| **`D-2`** | Which path gets the hook | ✅ **REST only.** §1.1 of this document — the WS side is already covered by `ws_health.log` and a second signal there would be the `V-1` defect |
| **`D-3`** | Transition-only or every occurrence | ✅ **Transition-only**, mirroring `WsHealthLog`. During a maintenance window every poll fails; logging each one would write thousands of lines and rotation would then be owed |
| **`D-4`** | Does `collector.ps1` need updating | ✅ **YES — add `venue_status.log` to `$FetchFiles` as a LITERAL** (line 117), per the `OPS-1` ruling that forbids a glob there. ⛔ **Without this the file never leaves the box** |
| **`D-5`** | Consumer built in the same session | ✅ **NO — instrument only.** ⛔ **The consumer cannot be tested until real data exists, and the instrument records FORWARD only.** Writing both at once produces a consumer nothing has ever exercised |

---

## 3. Build list

1. **`Core/VenueStatusLog.vb`** — new. ⭐ **Copy `Core/WsHealthLog.vb`'s structure**: append-only, `SyncLock`, never throws, transition-only, host-agnostic, no settings keys.
2. **One hook in `DeribitClient.ExecuteWithRetry`** at the existing `HttpRequestException` catch, per `D-1`.
3. **`collector.ps1` `$FetchFiles`** gains `'venue_status.log'` as a literal.
4. **Fixture family `A74`** (⚠ **`A73` is taken by the coverage cluster — count it, do not inherit this**).

### Not in this build

- ⛔ **The coverage-report consumer.** `D-5`. It follows once real lines exist.
- ⛔ **Anything on the WS path.** `D-2`.

---

## 4. Fixtures — family `A74`

| # | Asserts | ⛔ The mutation that must fail it |
|---|---|---|
| **`A74a`** | A venue 503 writes **exactly one** line, shaped `utc \| state \| instance_id` | Remove the hook → no line |
| **`A74b`** | ⛔⛔ **`V-1` GUARD — the one with teeth.** A **timeout**, a generic network failure, and a 4xx each write **NOTHING** | Log on any failure → this fails, and it is the defect that would let our own outages scope themselves out |
| **`A74c`** | Transition-only: **N consecutive** venue failures write **one** line, not N | Log every occurrence → N lines |
| **`A74d`** | The log path **never throws** — an unwritable directory does not propagate into the fetch | Remove the swallow → the fetch fails because a diagnostic could not be written |

⭐ **`A74b` is the acceptance item. If only one fixture is written, write that one.**

---

## 5. Acceptance

| # | Check | Expected |
|---|---|---|
| `AC-1` | Harness | **+4, ALL PASS** |
| `AC-2` | Solution Release `-t:Rebuild` | **0 errors, 0 warnings** |
| `AC-3` | `verify-gate.ps1 -Mode local-fast` | **GATE PASSED** |
| `AC-4` | `settings.json` | ⛔ **untouched at v68** |
| `AC-5` | Display-string parity | **does not fire** — no snapshot line, no card binding. Confirm from the gate's own *"no snapshot/card drift detected"*, do not assert it |
| `AC-6` | `grep -c "venue_status.log" tools/ops/collector.ps1` | **≥1** (`V-3`) |
| `AC-7` | Under `D-1` (b) ONLY: the success path is provably unchanged | **byte-identical results on 200 responses** |

**Tag `[no-engine-change]` under `D-1` (a) or (c).** ⚠ **Under (b) it is an engine-path change to the data layer and a `DeribitIndicatorProject.md` §15 entry IS owed** — say which in the commit rather than leaving it implied.

---

## 6. ⚠ Two limits — know these before building, they do not go away

- ⛔ **IT RECORDS FORWARD ONLY. It cannot retro-scope the 2026-08-11 outage**, which stays a false defect permanently. **No instrument can fix a past hour that was never observed.**
- ⚠ **It only fires when the venue actually refuses.** **A venue degradation that returns HTTP 200 with no trades still reads as our defect** and this instrument will not catch it. ⭐ **That residual is real and is not worth chasing** — it needs a venue-side truth source we do not have.

⭐ **Honest value statement: this is a small build with a natural hook and a nearly free consumer, and it buys nothing until the next venue outage — roughly monthly.** **It is worth doing because the alternative is a coverage report that permanently mislabels someone else's outage as our defect, and that error is silent.**

---

## 7. What I did NOT verify

- ⚠ **I did not confirm Deribit returns HTTP 503 specifically for `system_maintenance`.** The code `11051` and the 503 pairing are **carried from the queue row's 2026-08-11 observation**, not reproduced. ⛔ **`D-1` (a) depends on it — if the venue returns 200 with an error body during maintenance, (a) does not work at all and the ruling must be (b) or (c).** **Check this first; it is the spec's load-bearing assumption.**
- ⚠ **I did not read `Core/AlertsTracker.vb`**, which `WsHealthLog` names as its own contract mirror. **A third copy of this sidecar shape may make it worth extracting one** — flagged, not ruled.
- ⚠ **I did not verify how `_http` handles a 503 under retry** — whether all attempts are exhausted first, which would change where the transition is detected.
