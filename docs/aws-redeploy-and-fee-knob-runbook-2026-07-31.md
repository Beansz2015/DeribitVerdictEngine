# Runbook — AWS v63 redeploy + the Aug-1 fee knob

**Written:** 2026-07-31 (Opus), for the trader to execute at the next RDP.
**Why it is time-boxed:** the redeploy must land **BEFORE** the knob turn. Reason in §0.

---

## 0. Why the order matters (the thing that makes this urgent)

Right now **both boxes gate at the same floor**, which is the only reason the pooled corpus is valid:

| Box | Build | Floor mechanism | Effective floor |
|---|---|---|---|
| **Local** (`bin\Debug`, canonical book) | **v63** ✅ | composed `trade_costs` | 0.0003 + 0.0005 = **0.0008** |
| **AWS** (supplementary collector) | **v61** ⚠ | flat `min_tradeable_move_pct` | **0.0008** |

Different mechanisms, same number — so the straddle pools cleanly. That is what made lane B's 13,339-row pooled snapshot a legitimate corpus.

**The moment you raise the knob on local, that stops being true.** Local's floor moves, AWS's stays at 0.0008, the two boxes start emitting different `BELOW_MIN_MOVE` rates and different verdict populations, and `aws-collector-deploy-checklist.md` §4.5 same-settings discipline is broken. Worse, **you cannot fix it by editing AWS's `settings.json`** — v61 has no `trade_costs` block at all, so the composed floor doesn't exist on that box. It needs the v63 **binary**.

**Redeploy first. Turn the knob second. Both boxes, same sitting.**

---

## 1. The knob

**`scoring.trade_costs.min_net_move_pct`** — `settings.json` line 442, currently **`0.0005`**.

- **UI:** row **`MIN NET MOVE % (after fees)`** in the **SETTINGS & TOOLS** card. Type the value, press **Enter** (or click away) — it saves without bumping the settings version and hot-reloads on the next analysis run. The row shows the derived composed floor beside it.
- **File:** editing `settings.json` directly works too and hot-reloads the same way.
- **Not the knob:** `maker_fee_bps` / `taker_fee_bps` / `round_trip_style` are **venue facts** and already carry the post-Aug-1 values (1.5 / 3.5, set at v62 on 07-27). Leave them alone. They are settings-file-only by design and fenced off the auto-tweaker (HC26).

### 1.1 The arithmetic, so you can pick the number

```
effective floor = round_trip_fee_pct(style) + min_net_move_pct
                = 0.0003  (maker_maker: 2 × 1.5 bps)  +  min_net_move_pct
```

Today: `0.0003 + 0.0005 = 0.0008`.

The v35 floor of 0.0008 was sized under **zero maker fees** — all 8 bps of it was net move you kept. Now 3 bps of it is fee, so at the current setting you're keeping **5 bps net**, not 8.

Two natural anchors — **this is your call, not mine:**

| Intent | `min_net_move_pct` | composed floor | net move kept |
|---|---:|---:|---:|
| Keep the *floor* where it was (change nothing) | 0.0005 | 0.0008 | 5 bps |
| Keep the *net move* where it was | **0.0008** | **0.0011** | 8 bps |

Anything between is legitimate. Raising it makes the engine more selective (more `BELOW_MIN_MOVE`, fewer directional verdicts); leaving it makes it accept trades that keep less after costs.

### 1.2 The knob turn IS a dataset boundary

Unlike the v62 build (which was byte-identical at defaults), **turning this knob changes verdicts**. Expect `BELOW_MIN_MOVE` to rise and the directional population to shrink. The v35 hot-reload + eval floor-change re-walk machinery absorbs it attributably — the perf strip will re-walk its cache — but rows before and after are **not** comparable on gate-sensitive metrics.

**Record the UTC instant you flip it, on both boxes.** That timestamp is the boundary marker for every later read.

---

## 2. AWS redeploy — step by step

Per `aws-collector-deploy-checklist.md` §1 (xcopy; there is no installer).

### 2.1 On the local machine, before you RDP

1. **Push first.** The checklist requires deploying from a **pushed** tree, never an unpushed build. There are 20 local commits waiting.
2. Build Release from the pushed tree:
   ```bash
   dotnet build -c Release
   ```
   > Do **not** build Debug — `bin\Debug` is the live collector's own directory and rebuilding it stomps the running collector.
3. The payload is the whole of `bin\Release\net8.0-windows\` — **7 items**:

   | Item | Notes |
   |---|---|
   | `DeribitVerdictEngine.exe` | the app |
   | `DeribitVerdictEngine.dll` | |
   | `DeribitVerdictEngine.deps.json` | required by the .NET host |
   | `DeribitVerdictEngine.runtimeconfig.json` | required by the .NET host |
   | `DeribitVerdictEngine.pdb` | optional (symbols); harmless to copy |
   | `fonts\` (contains `OFL.txt`) | **required** — CLAUDE.md bundled-fonts rule |
   | `settings.json` | **v63**, verify line 2 reads `"version": 63` |

### 2.2 On the AWS box

4. **Stop the running app** (it holds `analysis_log.csv` open).
5. **Back up the existing book before overwriting anything** — copy `analysis_log.csv`, `analysis_eval_cache.csv`, `liq_events.log` and `ws_health.log` somewhere outside the app directory. These are the AWS collector's accumulated data; the redeploy must not lose them.
6. Copy the 7 items over the existing install, **overwriting** the binaries and `settings.json`.
7. **Do NOT copy** `analysis_log.csv`, `analysis_eval_cache.csv`, or any `.bak`/sidecar from local — checklist §1.4. The AWS book stays its own; a seeded copy forks history and forces dedup at every pooled read.
8. **Restore** the files from step 5 if the copy overwrote any of them (it shouldn't — they aren't in the build output — but check).
9. Spot-check `settings.json` on the box: line 2 `"version": 63`, and `auto_run.trigger_mode` still `"on_close"`.
10. Restart the app: auto-run **REPEAT** + **ON-CLOSE**, press start, confirm the TAPE strip is alive, then **disconnect RDP — do not log off** (logoff kills the GUI session).

### 2.3 Then, and only then, the knob — on BOTH boxes

11. On **AWS**: set `MIN NET MOVE % (after fees)` to your chosen value. Note the UTC time.
12. On **local**: set the identical value. Note the UTC time.
13. Confirm on both that the derived read-out shows the same composed floor.

> Keeping the two flips close together keeps the boundary a single known instant rather than a smear.

---

## 3. Local machine — what it does and doesn't need

**No redeploy.** Verified 2026-07-31: `bin\Debug\net8.0-windows\` is already on **v63** (`settings.json` line 2 = 63, `min_net_move_pct` 0.0005, exe dated 07-30 00:48). The local box builds from source and is current.

**It does need the knob turn** (§2.3 step 12) — the pooling requirement is symmetric.

**Do not rebuild Debug while the collector is running.** If you ever need to, stop the app first.

---

## 4. After

- **Re-verify the pool before the next pooled read.** Header equality between the two `analysis_log.csv` files, then the local-preferred-per-UTC-session-hour dedup (`aws-collector-deploy-checklist.md` §4.3b). Recipe and worked numbers: `pooled-report-runner-spec-back.md` §4.1.
- **Rows straddling the knob turn are not comparable** on `BELOW_MIN_MOVE`, directional share, or anything downstream of the gate. Filter by the timestamps from §2.3.
- **The AWS `InstanceId` changes** on restart. Re-record the new one at the next copy-back — the deploy checklist §5 keeps the authoritative list.

---

## 5. Quick checklist

```
[ ] push the 20 local commits
[ ] dotnet build -c Release   (never Debug)
[ ] back up AWS analysis_log.csv + eval cache + liq_events.log + ws_health.log
[ ] stop AWS app
[ ] copy 7 items incl. fonts\ and settings.json
[ ] verify AWS settings.json: version 63, trigger_mode on_close
[ ] restart AWS: REPEAT + ON-CLOSE, strip alive, disconnect (don't log off)
[ ] set MIN NET MOVE % on AWS      — note UTC
[ ] set MIN NET MOVE % on local    — note UTC, same value
[ ] record both timestamps as the dataset boundary
```
