# Proofs — AutoTweaker hostile audit, 2026-09-24

Proof code for [`docs/audits/2026-09-24-autotweaker.md`](../../2026-09-24-autotweaker.md). Audited tree: commit `6e74181`.

| File | What it is |
|---|---|
| `Program.cs.txt` | C# console probe (P1–P9). It references the built `AutoTweaker.dll` and calls the real `SettingsDiffApplier`, `FailureRateMatrix`, `ConditionsExtractor`, `ForwardWindowJoiner` and `EngineSettings`. It reaches private members by reflection (`ValidateSnapshotContent`, `SuccessfulRoundsForCurrentStreak`). |
| `Probe.csproj.txt` | Project file for the probe. |
| `trigger_noise_and_geometry.py.txt` | Closed-form arithmetic behind F7 (barrier-race hit rate vs expected value) and F12 (trigger false-positive rate). |

**Why every file ends in `.txt`.** The instruction was to rename `.vb` proof files to `.vb.txt` so nothing in the repo compiles or globs them. These proofs are C# and Python rather than VB. The same rename is applied to them for the same reason: a live `.csproj`/`.cs` pair under `docs/` would be buildable by any tool that walks the tree. The run command below copies them back to their real names in a temp directory.

The files are byte-identical to what ran during the audit. Nothing was edited after the run, which leaves two hardcoded paths:

- `Program.cs.txt` line 9: `const string Repo = "/home/user/DeribitVerdictEngine";` — the probe reads `settings.json` from there. Edit it to your checkout path on another machine.
- `Probe.csproj.txt`: `<HintPath>/tmp/claude-0/atbuild/AutoTweaker.dll</HintPath>` — the run command builds AutoTweaker to exactly that path. On Windows, change both the HintPath and the `-o` path in step 1.

## Exact run command

From the repo root, with a .NET 8 SDK and python3 (bash):

```bash
# 1. Build the audited AutoTweaker to the path the probe references
dotnet build tools/AutoTweaker/AutoTweaker.vbproj -c Debug -o /tmp/claude-0/atbuild

# 2. Restore the proof files to their real names in a temp dir, build, run
W=$(mktemp -d)
cp docs/audits/proofs/autotweaker/Program.cs.txt  "$W/Program.cs"
cp docs/audits/proofs/autotweaker/Probe.csproj.txt "$W/Probe.csproj"
dotnet build "$W" -o "$W/out"
dotnet "$W/out/Probe.dll" 2>&1

# 3. Closed-form numbers (F7, F12)
python3 docs/audits/proofs/autotweaker/trigger_noise_and_geometry.py.txt
```

Step 1 leaves a gitignored `tools/AutoTweaker/obj/` behind; delete it if you want a clean tree.

Environment of the recorded run: .NET SDK 8.0.131 on Linux 6.18.44 (the Claude Code cloud container). **P9 is platform-specific.** It exercises `FileSystemWatcher` on Linux (inotify). The Windows result for the same probe is **not verified**, and the live engine runs on Windows. Run P9 there before relying on the Windows half of finding F1. The wrong-file half of F1 comes from reading the code and doesn't depend on P9.

The probe makes no network calls, and nothing calls the Anthropic API. The model-selection finding (F13) is not covered by any probe.

## Output of the recorded run

`dotnet "$W/out/Probe.dll" 2>&1` (the `[SettingsDiffApplier] Parse error` lines are the applier's own stderr, interleaved):

```
=== P1 ParseDiff: malformed responses ===
  valid                      items=1 action='tweak' revertTarget=''
[SettingsDiffApplier] Parse error: Expected end of string, but instead reached end of data. LineNumber: 0 | BytePositionInLine: 110.
  truncated at max_tokens    items=0 action='tweak' revertTarget=''
[SettingsDiffApplier] Parse error: 'H' is an invalid start of a value. LineNumber: 0 | BytePositionInLine: 0.
  prose before JSON          items=0 action='tweak' revertTarget=''
[SettingsDiffApplier] Parse error: '`' is invalid after a single JSON value. Expected end of data. LineNumber: 1 | BytePositionInLine: 0.
  fence + trailing prose     items=0 action='tweak' revertTarget=''
[SettingsDiffApplier] Parse error: Object reference not set to an instance of an object.
  action:null                items=0 action='tweak' revertTarget=''
  revert w/o target          items=0 action='revert' revertTarget=''
=== P2 Validate: the one value check and the path fences ===
  mtf_gate.enabled -> null                                       valid=True  | reload THROWS JsonException: The JSON value could not be converted to System
  mtf_gate.enabled -> 0.0                                        valid=True  | reload THROWS JsonException: The JSON value could not be converted to System
  mtf_gate.enabled -> "off"                                      valid=True  | reload THROWS JsonException: The JSON value could not be converted to System
  bbw_squeeze_penalty 2->1.5 (Integer POCO; prompt's own example shape) valid=True  | reload THROWS JsonException: The JSON value could not be converted to System
  atr_stop_multiplier -> "2.4" (string)                          valid=True  | reload THROWS JsonException: The JSON value could not be converted to System
  atr_stop_multiplier, old_value OMITTED                         valid=True  | reload OK -> stopMult=2.4
  atr_stop_multiplier old 1.60 (file has 1.6)                    valid=False (Stale diff: path 'scoring.atr_stop_multiplier' has current value 1.6 but diff expects 1.60.)
  PARENT 'mtf_gate' -> enabled:false                             valid=True  | reload OK -> mtf.enabled=False
  PARENT 'kelly' -> max_risk 0.5                                 valid=True  | reload OK -> kelly.maxRisk=0.5
  PARENT 'scoring.trade_costs' -> fees 0                         valid=True  | reload OK -> floorPct=0
  PARENT 'signal_bridge' -> new output_path                      valid=True  | reload OK -> bridge.path=C:\Dev\DeribitBridge\x.json
  PARENT 'scoring.structural_levels' -> enabled:false            valid=True  | reload OK -> sl.enabled=False
  Core diffSummary on missing old_value -> InvalidOperationException
=== P3 Apply (pre-existing finding; only establishing WHERE it stops for the trace) ===
  Apply threw InvalidOperationException: JsonSerializerOptions instance must specify a TypeInfoResolver setting before being marked as read-only.
  file unchanged: True; .tmp left behind: False
=== P4 ValidateSnapshotContent on a snapshot that differs in trader-owned keys ===
  integrity IsValid=True reason=''
=== P5 every row a stop-out, 64 tier-eligible rows split 16/16/16/16 ===
  tier-eligible=64 (min tier for 120-row window = 60); raw failure=64/64; recommended cells=0; aggregateRatePct=0.0 -> BELOW_THRESHOLD, streak+1
=== P6 which window the trigger reads (narrowest Wilson CI) ===
  high-fail tier: 5m fail=80 % ciW=0.200*  10m fail=65 % ciW=0.235  15m fail=50 % ciW=0.245  -> aggregate 80.0%
  moderate tier: 5m fail=45 % ciW=0.191  10m fail=38 % ciW=0.187  15m fail=30 % ciW=0.177*  -> aggregate 30.0%
=== P7 SuccessfulRoundsForCurrentStreak with a skip inside the streak ===
  streak counter=3, rounds returned=2
=== P8 ConditionsExtractor spread bucket: core call vs snapshot call ===
  core (0.2/2.0): T:33|N:33|W:33   snapshot path (0/0): T:0|N:0|W:100
=== P9 FileSystemWatcher(LastWrite|Size) vs the applier's tmp+rename write (Linux inotify run) ===
  atomic tmp+rename: Changed events=0
  in-place write (control): Changed events=1
```

`python3 docs/audits/proofs/autotweaker/trigger_noise_and_geometry.py.txt`:

```
true fail 0.30 n=30: P(obs>=40%)=0.159
true fail 0.30 n=45: P(obs>=40%)=0.099
true fail 0.30 n=60: P(obs>=40%)=0.063
true fail 0.35 n=30: P(obs>=40%)=0.345
true fail 0.35 n=45: P(obs>=40%)=0.289
true fail 0.35 n=60: P(obs>=40%)=0.247
ATR 140: shipped T=1.75xATR (38.3bps) S=1.6xATR (35.0bps) p_success=0.478 fail=0.522 EV_zero_edge=-4.04bps floor_ok=True
ATR 140: tweak T=1.25xATR (27.3bps) S=2.2xATR (48.1bps) p_success=0.638 fail=0.362 EV_zero_edge=-3.72bps floor_ok=True
ATR 40: shipped T=1.75xATR (10.9bps) S=1.6xATR (10.0bps) p_success=0.478 fail=0.522 EV_zero_edge=-4.04bps floor_ok=True
ATR 40: tweak T=1.25xATR (7.8bps) S=2.2xATR (13.8bps) p_success=0.638 fail=0.362 EV_zero_edge=-3.72bps floor_ok=False
inverse long loss/gain BTC ratio at 35bps: 1.007024586051179
```

## Probe → finding map

| Probe | Supports | What it shows |
|---|---|---|
| P1 | F3 | Truncated, prose-wrapped, fence-plus-text, `action:null` and target-less revert responses all parse to 0 items, which the Core books as a successful round |
| P2 | F2, F4, F5 | Wrong-type values and parent-object paths pass `Validate`; the stale check is fail-open on a missing `old_value` and rejects on `1.60` vs `1.6` |
| P3 | trace step 8 (pre-existing finding) | Where `Apply` stops: `ToJsonString(opts)`, before the write |
| P4 | F11 | Snapshot integrity accepts changed Kelly, fees, bridge and feature switches, and a missing key |
| P5 | F6 | 64 of 64 stop-outs report as a 0.0% aggregate |
| P6 | F12, trace step 4C | The narrowest-CI pick reads the worst window above 50% failure and the best window below it |
| P7 | F10 | A skip inside a streak truncates the rounds returned |
| P8 | F10 | Snapshot-path spread mix is always `W:100` |
| P9 | F1 (Linux half only) | The tmp+rename write raises no `Changed` event on a `LastWrite|Size` watcher |
