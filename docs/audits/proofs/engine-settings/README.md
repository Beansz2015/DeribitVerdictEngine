# Proofs: settings audit, 2026-09-24

These are the runnable proofs `P1` to `P19` cited in [`../../2026-09-24-engine-settings.md`](../../2026-09-24-engine-settings.md). Each one drives the **real shipped sources** (`SettingsLoader`, `ScoringEngine.Calculate`, `SignalEmitter.ComputeSideLevels`, `ScoringEngine.CalcKellySizing`, `IndicatorEngine.CalcMTFGate`, `DynamicNorms`, `ExecutionResolution`, `DeribitClient`). None of them re-implements engine logic.

| File | What it is |
|---|---|
| `Program.vb.txt` | The proof program. Rename it to `Program.vb` to build |
| `Audit.vbproj.txt` | The project file: a path-parameterised copy of `verify/ordercheck/OrderCheck.vbproj`. It links the same 69 shipped source files, and uses this `Program` in place of the harness's. Rename it to `Audit.vbproj` to build |

⛔ **Never build these in place under `docs/`.** The root `DeribitVerdictEngine.vbproj` compiles every `*.vb` outside `tools/` and `verify/` (its `Compile Remove` entries at lines 23 and 31). A `Program.vb`, or the generated `obj/*.vb` files, sitting under `docs/` would be pulled into the app build. That is why both files carry a `.txt` suffix and why the command below copies them to a temp directory first.

## Pinned to

- **Commit `6e74181`** (tracked `settings.json` v68). If the sources change after this commit, the numbers below can change.
- **.NET SDK 8.0.131** (Ubuntu apt package `dotnet-sdk-8.0`) on Linux. The linked sources target `net8.0` with no WinForms, so the project builds on Linux.
- **Run on 2026-09-24.**

## Exact run command

Run from the repo root, in bash. Requires the .NET 8 SDK.

```bash
W=$(mktemp -d)
cp docs/audits/proofs/engine-settings/Audit.vbproj.txt "$W/Audit.vbproj"
cp docs/audits/proofs/engine-settings/Program.vb.txt   "$W/Program.vb"
dotnet run -c Release --project "$W/Audit.vbproj" --property:RepoRoot="$PWD" -- "$PWD" 2>&1 | grep -v '^\[SettingsLoader\] Hot-reloaded'
```

- **`--property:RepoRoot`** tells the project where the linked sources live. The project refuses to build without it.
- **The trailing `"$PWD"`** tells the program where to read the tracked `settings.json`.
- **The `grep -v`** drops only the loader's `Hot-reloaded settings.json` console lines. `FileSystemWatcher` raises a variable number of `Changed` events per write, so their count differs from run to run. Nothing else is filtered: the `[SettingsLoader] Parse error:` lines stay in the output on purpose.
- **Exit code 0.** I ran it exactly as written, with `TMPDIR` pointed at a session scratch directory so `mktemp` stayed out of `/tmp`.

## What it touches

- **Reads:** `<repo>/settings.json`. Every proof starts from the tracked file and mutates an in-memory copy.
- **Writes:** temp `settings.json` and `settings.local.json` files under `<build output>/work/`, which is inside `$W`. **It never writes to the repo.**
- **Network:** none.
  - `P19` triggers `DeribitClient`'s static constructor with a zero timeout, so it throws before any request is made.
  - `P17`'s candles are synthetic.
- **Timing-dependent:**
  - `P8` and `P9` wait 1.5 s for `FileSystemWatcher` to pick up a file write. On a very slow disk they could print the pre-edit values.
  - `P10`'s "startup" is simulated by resetting `SettingsLoader._current` to `New EngineSettings()` through reflection before `Initialise`.

## Differences from the session scratchpad copy

1. The repo root is now a command-line argument, not a hard-coded path.
2. The header comment was updated.
3. The project file gained the `RepoRoot` guard and an explicit `AssemblyName`.

The output below matches the session run.

The fixture builders `BuildA8Cfg`, `BuildA8Indicators`, `BuildA8Norms` and `BuildGateIndicators` are copied verbatim from `verify/ordercheck/Program.vb`.

## Proof to finding map

| Proof | What it shows | Finding |
|---|---|---|
| P1 | `null` block: `structural_levels` switches to legacy geometry silently; `trade_costs` makes `Calculate` throw | F12, F1 |
| P2 | Partial `sessions` / `structural_levels.sessions` / `resolution_profiles` replace wholesale | F2 |
| P3 | Renaming the NY session disarms aggressor-velocity scoring | F2 |
| P4 | Inverted clamp bounds throw on every run, including the display-only ATR keys | F1 |
| P5 | `min_of` asymmetry, fail-open on thin data, bull-only EMA leg below 50 bars | F3 |
| P6 | Kelly `risk_usd` is not the risk of `contracts`; `max_leverage` 0 removes the cap | F7 |
| P7 | A maker rebate or non-positive `min_net_move_pct` turns off Step 5c; 0.05 means a 5 % floor | F5, F15 |
| P8 | A run holding v68 while `DynamicNorms` reads v69 | F9 |
| P9 | Hot-reload parse failure: last-good keeps running while the status text says "code defaults" | F12 |
| P10 | A base parse failure at startup drops the `settings.local.json` overlay | F12 |
| P11 | `4.0` for an integer key rejects the whole file | F12 |
| P12 | A duplicate block: no overlay takes the last copy, an overlay box fails to parse, A62 sees the first | F11 |
| P13 | A sign slip on `funding_high_negative` gives a permanent long bias | F4 |
| P14 | A negative `bbw_squeeze_penalty` becomes non-directional padding; the ledger guard stays clean | F4 |
| P15 | `stop_max_atr_mult` differing from `atr_stop_multiplier` makes the stop non-monotonic | F8 |
| P16 | A 4-tick structural stop against fees | F6 |
| P17 | The high-volatility flush trace at shipped config | LOGIC TRACE, F7 |
| P18 | Wrap-around and gapped session buckets | F2 |
| P19 | `request_timeout_seconds` 0 breaks `DeribitClient` for the whole process, and it stays broken after the file is fixed | F13 |

## Output

```text
=== P1 null block ===
  structural_levels:null  -> LastLoadError=''  StructuralLevels Is Nothing=True
  ASIA long, swing stop $10 below: null-block stop=61936 (legacy/Nothing) target=62070  | shipped stop=61990 (SWING_STOP) target=62100 (SWING_HIGH_5M)
  trade_costs:null        -> LastLoadError=''  TradeCosts Is Nothing=True
  Calculate THROWS NullReferenceException

=== P2 partial dictionaries / lists replace wholesale ===
  structural_levels.sessions:{} -> err='' ASIA fallback mult=1.75 (shipped 1.25), LONDON=1.75 (shipped 2)
  sessions=[NY only] hour 3: bucket=<none> execRes=1 rocMag=0.1 fallbackMult=1.75 aggrVelArmed=False  | shipped execRes=3 rocMag=0.17 fallbackMult=1.25 armed=True
  sessions=[NY only] hour 10: bucket=<none> execRes=1 rocMag=0.1 fallbackMult=1.75 aggrVelArmed=False  | shipped execRes=3 rocMag=0.11 fallbackMult=2 armed=True
  resolution_profiles:{"1":{}} -> 3-min slope delta=0.05 (shipped 0.06)

=== P3 session rename disarms scoring ===
  session_volume NY->NEW_YORK, hour 15: armed=False normWindow=120 burstThr=2.5  | shipped armed=True normWindow=60 burstThr=4.5
  LastLoadError='' (no warning)

=== P4 inverted clamp bounds throw every run ===
  indicators.ATR.scale_min 5 > scale_max 4 (display-only keys) -> loaded (err=''), DynamicNorms.Compute THROWS ArgumentException: '5' cannot be greater than 4.
  indicators.VWAPDynamic.dev_clamp_min 3.5 > dev_clamp_max 3.0 -> loaded (err=''), DynamicNorms.Compute THROWS ArgumentException: '3.5' cannot be greater than 3.
  indicators.Volume.dynamic_mid_clamp_min 5 > dynamic_mid_clamp_max 4 -> loaded (err=''), DynamicNorms.Compute THROWS ArgumentException: '5' cannot be greater than 4.

=== P5 mtf_gate.min_of semantics ===
  min_of=0: trend=BULL passLong=True  passShort=False | 15m +DI:15.7 -DI:43.6 ADX:69.1 EMA:BULL | Bull:1 Bear:2 (need 0)
  min_of=1: trend=BULL passLong=True  passShort=False | 15m +DI:15.7 -DI:43.6 ADX:69.1 EMA:BULL | Bull:1 Bear:2 (need 1)
  min_of=2: trend=BEAR passLong=False passShort=True  | 15m +DI:15.7 -DI:43.6 ADX:69.1 EMA:BULL | Bull:1 Bear:2 (need 2)
  min_of=3: trend=FLAT passLong=True  passShort=True  | 15m +DI:15.7 -DI:43.6 ADX:69.1 EMA:BULL | Bull:1 Bear:2 (need 3)
  min_of=4: trend=FLAT passLong=True  passShort=True  | 15m +DI:15.7 -DI:43.6 ADX:69.1 EMA:BULL | Bull:1 Bear:2 (need 4)
  10 candles (< adx_period+2): passLong=True passShort=True | MTF: insufficient 15m candles (10)
  steady DOWNtrend, candle_lookback=60: EMA leg=BEAR trend=BEAR | 15m +DI:0.0 -DI:50.0 ADX:100.0 EMA:BEAR | Bull:0 Bear:3 (need 2)
  steady DOWNtrend, candle_lookback=40: EMA leg=MIXED trend=BEAR | 15m +DI:0.0 -DI:50.0 ADX:100.0 EMA:MIXED | Bull:0 Bear:2 (need 2)
  steady UPtrend,   candle_lookback=60: EMA leg=BULL trend=BULL | 15m +DI:50.0 -DI:0.0 ADX:100.0 EMA:BULL | Bull:3 Bear:0 (need 2)
  steady UPtrend,   candle_lookback=40: EMA leg=BULL trend=BULL | 15m +DI:50.0 -DI:0.0 ADX:100.0 EMA:BULL | Bull:3 Bear:0 (need 2)

=== P6 Kelly payload risk_usd vs contracts; max_leverage 0 ===
  HIGH: f*=0.330 contracts=500 risk_usd(payload)=50.00 lev_capped=True  | risk of those contracts over the stop=18.07 USD (+ fees maker in/taker out on 5000 USD notional = 2.50)
  MEDIUM: f*=0.139 contracts=500 risk_usd(payload)=50.00 lev_capped=True  | risk of those contracts over the stop=18.07 USD (+ fees maker in/taker out on 5000 USD notional = 2.50)
  max_leverage=0: contracts=1383 lev_capped=False notional=13830 USD = 13.8x a 1000 USD account
  contract_face_usd=0: contracts=0 risk_usd=50.00

=== P7 maker rebate disables Step 5c ===
  maker 1.5 bps : floor=0.00080 verdict='NO TRADE' ctx=BELOW_MIN_MOVE
  maker -2.5 bps: floor=0.00000 verdict='SHORT' ctx=CONFIRMED  target dist=22.75 USD (3.7 bps), stop dist=20.80 USD (3.4 bps)
  min_net_move_pct -0.0003 (file edit; UI rejects <0): floor=0.00000
  min_net_move_pct 0.05 (someone meaning 0.05 %): floor=0.0503 = 3119 USD at 62k

=== P8 torn run across hot-reload ===
  run cfg: version=68 static_fallback=1.5  | DynamicNorms (reads SettingsLoader.Current) VWAPDevThreshold=2.7, Current.version=69

=== P9 hot-reload parse failure keeps last-good (status says code defaults) ===
[SettingsLoader] Parse error: The JSON object contains a trailing comma at the end which is not supported in this mode. Change the reader options. Path: $.mtf_gate | LineNumber: 403 | BytePositionInLine: 2.
  after bad edit: LastLoadError set=True | running version=68 signal_bridge.enabled=True start_engaged=True
  status bar would read 'running on code defaults' = version 1, signal_bridge.enabled False — which is NOT what is running

=== P10 base parse failure at startup drops the overlay ===
[SettingsLoader] Parse error: The JSON object contains a trailing comma at the end which is not supported in this mode. Change the reader options. LineNumber: 403 | BytePositionInLine: 2.
  base broken + overlay {trade_store.enabled:false}: err set=True OverlayActive=False trade_store.enabled=True (overlay asked false) transport=ws

=== P11 integer key written as 4.0 rejects the whole file ===
[SettingsLoader] Parse error: The JSON value could not be converted to System.Int32. Path: $.scoring.structural_levels.stop_min_floor_ticks | LineNumber: 431 | BytePositionInLine: 33.
  stop_min_floor_ticks: 4.0 -> LastLoadError='The JSON value could not be converted to System.Int32. Path: $.scoring.structural_levels.stop_min_floor_ticks | LineNumber: 431 | BytePositionInLine: 33.' version=1

=== P12 duplicate block: overlay vs no-overlay diverge ===
  duplicate mtf_gate (2nd has min_of 0), NO overlay:   err='' min_of=0
[SettingsLoader] Parse error: An item with the same key has already been added. Key: mtf_gate (Parameter 'propertyName')
  same file WITH an overlay present:                 err='An item with the same key has already been added. Key: mtf_gate (Parameter 'propertyName')' min_of=2 version=1
  A62-style first match sees min_of=2

=== P13 funding sign slip = permanent long bias ===
  funding  0.000000: correct L/S=4/11 'SHORT'   | high_negative sign-slipped to +8e-5: L/S=5/9 'WEAK SHORT'
  funding  0.000005: correct L/S=4/11 'SHORT'   | high_negative sign-slipped to +8e-5: L/S=5/9 'WEAK SHORT'
  funding -0.000030: correct L/S=4/10 'SHORT'   | high_negative sign-slipped to +8e-5: L/S=5/9 'WEAK SHORT'

=== P14 negative squeeze penalty = non-directional padding ===
  bbw_squeeze_penalty= 2: L/S=3/9 max=18 verdict='WEAK SHORT' ledgerMismatch=False
  bbw_squeeze_penalty=-2: L/S=5/13 max=18 verdict='STRONG SHORT' ledgerMismatch=False

=== P15 stop_max_atr_mult != atr_stop_multiplier is non-monotonic ===
  stop_max 1.2 / atr_stop 1.6: swing stop $500 away -> STOP_CLAMPED at 48.0 USD; NO swing stop -> FALLBACK_ATR at 64.0 USD; Kelly sizes on 64.0 USD

=== P16 4-tick structural stop vs fees ===
  swing low 5 ticks under price: stop=SWING_STOP 2.5 USD (0.4 bps), target=FALLBACK_ATR 70.0 USD; gross R:R=28.0
  per 1 BTC notional: loss if stopped = 2.5 + taker/maker fees 31.0 = 33.5 USD; win = 70.0 - 18.6 = 51.4 USD; net R:R=1.53; fees are 93% of the stop-out loss

=== P17 high-vol flush trace (shipped cfg) ===
  price 62000.0  ATR(7,1m) after flush=138.2 (22.3 bps)  floor=49.6 USD
  SHORT: stop STOP_CLAMPED +221.1 (35.7 bps) target FALLBACK_ATR -241.8 (39.0 bps) | LONG: stop FALLBACK_ATR -221.1 target FALLBACK_ATR +241.8
  Kelly HIGH: contracts=500 risk_usd=50.00 lev_capped=True; actual stop-out = 17.83 + fees 2.50 = 20.33 USD
  b: Kelly uses 1.094; placed gross 1.094; placed net (maker/maker win, maker/taker loss) 0.885; breakeven p: 47.8 % vs 53.0 %
    p=0.55: f* Kelly-shown=0.139  f* net=0.042
    p=0.65: f* Kelly-shown=0.330  f* net=0.255
  quiet NY (ATR 40): placed net b=0.541; p=0.55 f* net=-0.282; p=0.65 f* net=0.003 (Kelly shows 0.139 / 0.330, both capped to 5 %)

=== P18 wrap-around / gapped session buckets ===
  ASIA 22..7 (wrap), hour 23: bucket=NY execRes=1 aggrVelArmed=True
  ASIA 22..7 (wrap), hour 0: bucket=<none> execRes=1 aggrVelArmed=False
  ASIA 22..7 (wrap), hour 5: bucket=<none> execRes=1 aggrVelArmed=False
  LastLoadError=''

=== P19 request_timeout_seconds 0 poisons DeribitClient for the process ===
  attempt 1 (timeout key now 0): TypeInitializationException <- ArgumentOutOfRangeException: value ('00:00:00') must be greater than '00:00:00'. (Parameter 'value')
  attempt 2 (timeout key now 15): TypeInitializationException <- ArgumentOutOfRangeException: value ('00:00:00') must be greater than '00:00:00'. (Parameter 'value')
```
