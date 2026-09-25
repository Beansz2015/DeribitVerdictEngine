# Settings audit: `EngineSettings.vb`, `settings.json`, `SettingsLoader.vb` (2026-09-24)

**Scope:** `Core/Settings/EngineSettings.vb`, the tracked `settings.json` (v68), and `Core/Settings/SettingsLoader.vb` for reference. Audited at commit `6e74181`.

**Coverage. Files in the prompt that were NOT fully covered:**

- **`settings.json`: partial, history strings only.** Every configuration block and key was read in full. The `change_log` array and the `modified_by` string are about 120 KB of version-history prose with no runtime effect beyond populating `EngineSettings.ChangeLog`. Of those, only the v68 to v62 entries were read.
- `Core/Settings/EngineSettings.vb`: read in full.
- `Core/Settings/SettingsLoader.vb`: read in full.

**Proof code:** [`proofs/engine-settings/`](proofs/engine-settings/). The [`README.md`](proofs/engine-settings/README.md) there has the exact run command and the output.

**Edits made to the report below:** it is the report as delivered in the session, with two changes. The closing paragraph that pointed at the session scratchpad now points at the committed proofs. A chat-only offer to publish a web page was dropped.

---

Nothing in the repo was modified (`git status` is clean). The proofs are in an isolated scratchpad project that links the real `.vb` sources. I installed the .NET 8 SDK through apt so I could run them against the actual `Calculate()`, `ComputeSideLevels()`, `CalcKellySizing()`, `CalcMTFGate()` and `SettingsLoader`. Each tag `P1`–`P19` below points to a proof I ran. Where a claim comes from reading code, I say so.

## --- LOGIC TRACE ---

**Scenario (P17).** NY, 14:00 UTC, running on 1-minute candles. BTC falls from 62,700 to 62,000 in two 1-minute bars, after 240 quiet bars with a true range around $34. I used 62,000 because the fixtures use it; it isn't meant as the live price. Shipped v68 config, shorts dominant.

| Stage | Settings keys that decide it | Value in the flush | Payload field | If the JSON block is **missing** (the POCO default is used) |
|---|---|---|---|---|
| Candle resolution | `session_volume.sessions[NY].execution_resolution`, matched by UTC hour | 1-minute | `exec_resolution_min` | Same values: the POCO list mirrors the shipped one. A block that is **present but partial** is the dangerous case (F2) |
| ATR | `indicators.ATR.period` 7 (Wilder smoothing, execution candles) | 138.2 (22.3 bps) | `atr` | Same. `static_ref`, `scale_min` and `scale_max` only feed the display and the CSV `ATRMultiplier` column, never the levels |
| Target | `structural_levels.target_arbitration_mode` 0, `use_best_pivot_candidate` false, then the 5m swing, then nearest HVN, then POC (gated by `VPFRSignal`), then the ATR fallback. `target_max_atr_mult` 3.5 is the bound. The fallback multiplier comes from `structural_levels.sessions["NY"]`, which has no override, so `scoring.atr_target_multiplier` 1.75 applies. `target_buffer_pct` 0 | No confirmed 5m swing low below price: the new low needs `pivot_wing_5m`=3 bars to its right before it counts. So the target falls back to ATR: −241.8 (39.0 bps) | `levels.short.target` | Same. But `"structural_levels": null` silently switches to the legacy v50 geometry (P1) |
| Stop | 5m swing high (well above price) > `stop_max_atr_mult` 1.6 × ATR, so it clamps. `atr_stop_multiplier` 1.6 applies when no swing stop exists. `stop_min_floor_ticks` 4 × `TickSize` 0.5 = $2. `stop_arbitration_mode` 0, `stop_buffer_pct` 0 | `STOP_CLAMPED` at +221.1 (35.7 bps). The long side's stop is `FALLBACK_ATR` at −221.1 | `levels.*.stop` | Same |
| Min-move gate | `trade_costs` {maker 1.5, taker 3.5 (**no consumer**), `round_trip_style` maker_maker, `min_net_move_pct` 0.0005}, giving an `EffectiveMinMovePct` of 0.0008 | Floor $49.6. The $241.8 target passes easily. The gate never looks at the stop | `verdict` / `verdict_context` | Same |
| MTF veto | `mtf_gate.enabled`, `candle_lookback` 60 (taken from a **hard-coded** 70-bar 15m fetch, `MainForm_Analysis.vb:93`), `adx_period` 9, `adx_min` 20, `min_of` 2 | Depends on the market. On the WebSocket path the 15m series includes the forming bar. A flush bar turns −DI dominant (one bear vote). A lagging ADX and an EMA stack left over from the prior trend can leave it at 1–1, which is FLAT and **passes both sides**. On the REST path the 15m cache is kept indefinitely when the fetch fails (`:95-98`); nothing in settings limits how stale it can get | `mtf_blocked` | Same |
| Kelly | `CalcKellySizing(v, r.ATR × atr_stop_multiplier, price)` uses ATR × 1.6, **not the placed stop**. b = 1.75/1.6 = 1.094. p = `est_prob_floor` + `est_prob_scale` × {1, .5, 0}. Then `use_half_kelly`, the `max_risk_fraction` cap, `account_size_usd`, `contract_face_usd`, `max_leverage` | HIGH gives f\* .330, halved to .165, capped to .05: **risk_usd 50**. Contracts by risk = 1,402; the leverage cap cuts it to **500**, `lev_capped` true. What a stop-out actually costs: $17.83 + $2.50 fees = **$20.33** | `kelly.{contracts, risk_usd, lev_capped}` | Same |

**How a block can be present or absent.**
- **Absent:** the POCO default applies, and it mirrors the shipped file apart from the two deliberate exceptions: `signal_bridge.enabled`=false and `auto_run.start_engaged`=false.
- **`null`:** the property becomes `Nothing`. Some consumers null-guard it and quietly change behaviour (P1: legacy geometry); others throw (P1: `trade_costs` gives a `NullReferenceException` in `Calculate`).
- **Present but partial:** a dictionary or list replaces the POCO seed wholesale (P2).

Nothing is validated in any of the three cases.

A whole-file parse failure at startup leaves the engine idle with the bridge off. That fails closed for orders, but a collector silently stops logging, and the settings.local.json overlay is dropped (F12).

## Findings, most severe first

**F1 · HIGH: a settings value that throws halts an unattended collector until someone clicks OK**
- **Location:** `DynamicNorms.vb:60-61, 96, 105, 171` (`Math.Clamp` with settings bounds). `MainForm_Analysis.vb:28-48` (catches with `MessageBox.Show`). `MainForm_AutoRun.vb:149` (`If Not btnAnalyze.Enabled Then Return`).
- **Downstream impact:** no runs, no CSV rows and no SKIPPED payloads. The bridge goes silent and the consumer stands down on its max-age gate. Only the tape capture keeps running.
- **Failure scenario:**
  - Proven throws:
    - `ATR.scale_min 5 > scale_max 4`, `VWAPDynamic.dev_clamp_min 3.5 > max 3.0` and `Volume.dynamic_mid_clamp_min 5 > max 4` each throw `ArgumentException` on every run (P4). The first pair are **display-only keys**.
    - `"trade_costs": null` throws a `NullReferenceException` (P1).
  - Read from code: `VPFR.num_buckets 0` gives an index out of range (`Indicators_Structure.vb:133-135`), and `ATR.period 0` breaks `Take(0).Average()`.
  - Hot-reload reaches every one of these without a restart. After the first throw, a modal dialog stays up on a box nobody is watching, and every timer tick after it returns early.
- **Critique:** v68 FIX 4 already recognised this exact hazard (a MessageBox nobody is present to dismiss) and fixed it only for the interval-validation dialog. Validation effort also runs opposite to the stakes. `exit_guard`, `live_strip`, `alerts` and `trade_store` get `Math.Max` floors at their consumers. The scoring, geometry and risk keys get nothing.

**F2 · HIGH: one session list decides resolution, ROC thresholds, target geometry and aggressor-velocity arming, and a partial, renamed or wrapped entry fails silently**
- **Location:** `ExecutionResolution.vb:39` (`MatchSessionBucket`, first match, inclusive hours, no wrap) and every name-keyed override dictionary.
- **Downstream impact:** live scoring and levels change with `LastLoadError` still empty.
- **Failure scenario (P2, P3, P18):**
  - `sessions` edited down to NY only: hours 3 and 10 switch from 3-minute to 1-minute candles, ROC magnitude drops from 0.17/0.11 to 0.1, the fallback target multiplier goes from 1.25/2.0 to 1.75, and aggressor velocity is **disarmed**.
  - NY renamed to `NEW_YORK`: disarmed, norm window 60 becomes 120, burst threshold 4.5 becomes 2.5.
  - ASIA written as a wrap (22→7): hours 0–7 match nothing, hour 23 goes to NY.
  - `structural_levels.sessions: {}`: ASIA's target grows 40% (1.25 to 1.75 ATR).
- **Critique:** arming by the *presence* of a key, joined across three blocks by a free-text name, is a foreign key with no integrity check. The design decided that absence means inert, so a typo is a live scoring change that the reserved-class rules exist to prevent, and nothing reports it.

**F3 · HIGH: `mtf_gate` is asymmetric and fails open. This goes beyond the already-known "min_of 0 loads" finding**
- **Location:** `Indicators_Structure.vb:455-520`. `MainForm_Analysis.vb:93`.
- **Downstream impact:** the hard veto either blocks one side only or disappears, while `enabled` still reads true.
- **Failure scenario (P5), with Bull 1 against Bear 2:**
  - `min_of` 0 or 1 gives **BULL** because the bull branch is tested first. Shorts are blocked and longs pass, even though the bear vote is the majority. 0 is not "gate off"; it bans shorts.
  - `min_of` ≥ 4 is always FLAT, so the gate is off.
  - Fewer than `adx_period`+2 candles: both sides pass.
  - `candle_lookback` 40: the EMA leg votes BULL in an uptrend but only MIXED in a downtrend, because EMA50 is 0 and `ema21 < 0` can never be true. So it can only ever vote bull.
  - `candle_lookback` above 70 is silently capped by the fetch.
- **Critique:** only {2, 3} are valid. The tie-break is not symmetric by construction, and staleness has no bound at all.

**F4 · HIGH: sign errors create directional bias or reintroduce non-directional padding, and the ledger guard cannot see it**
- **Location:** `ScoringEngine_Calculate_Scoring.vb:714-726` (funding) and `:256-257` (squeeze). Every integer penalty and bonus has the same shape.
- **Downstream impact:** a permanent skew in the score behind a clean `LedgerMismatch=False`.
- **Failure scenario:**
  - `funding_high_negative` written as +8e-5 applies −2 short and +1 long on **every** run with funding ≤ 1e-5, zero funding included. SHORT 4/11 becomes WEAK SHORT 5/9 (P13).
  - `bbw_squeeze_penalty −2` adds +2 to both sides: WEAK SHORT becomes STRONG SHORT (P14).
- **Critique:** P14 is the non-directional padding pattern removed in v0.18, brought back by a single character. The ledger checks attribution, not sign, so it passes.

**F5 · HIGH: the min-move floor turns off quietly, and one execution style has to price two exit paths**
- **Location:** `ScoringEngine_Calculate_Verdict.vb:313-316` (`If floorDist > 0`). `EngineSettings.vb:957-982`.
- **Downstream impact:** directional payloads with targets inside the fee. `signal-bridge-v1-proposal.md:80` promises the consumer a floor of 0.0008 × price, and that promise lapses without notice.
- **Failure scenario (P7):**
  - A maker rebate, `maker_fee_bps −2.5`, is exactly the "venue fact" edit the doc comment tells you to make. It gives a floor of 0.00000, and a SHORT with a **3.7 bps** target and a 3.4 bps stop goes through.
  - `min_net_move_pct −0.0003` gives floor 0.
  - `0.05`, meaning 0.05%, gives a 5% floor, and every directional verdict is gated. The UI rejects both of these; a file edit does not.
- **Critique:**
  - `taker_fee_bps` has no consumer at all.
  - `round_trip_style` is one value, but a take-profit exit is maker and a stop exit is taker.
  - The parked Kelly fix (`kelly-placed-payoff-proposal.md` §3) repeats the mistake: it charges maker/maker on the loss side.

**F6 · MEDIUM-HIGH: the stop floor is measured in ticks, and fees are 93% of a tight structural stop-out**
- **Location:** `SignalEmitter.vb:455, 477`.
- **Downstream impact:** the payload can carry a stop a few dollars from entry.
- **Failure scenario (P16):** with the swing low 5 ticks under price, the result is `SWING_STOP` at $2.50 (0.4 bps) against a $70 target, a gross R:R of 28. Per BTC of notional, a stop-out costs $2.50 + $31 in fees. That's shipped behaviour, not a misconfiguration.
- **Critique:** fees and noise scale with price and ATR; ticks don't. The floor should come from `trade_costs` plus a fraction of ATR. I didn't measure how often this happens: there is no CSV in the container, and the DG derivation says structural stops are usually wide.

**F7 · MEDIUM: the Kelly block in the payload misstates its own risk and is effectively a constant**
- **Location:** `ScoringEngine_Kelly.vb:97` (`KellyRiskUsd` is set before the leverage cap and never recomputed) and `:113-117`.
- **Downstream impact:** `kelly.risk_usd` is not the risk of `kelly.contracts`.
- **Failure scenario (P6):**
  - HIGH and MEDIUM both give 500 contracts and $50 of "risk". The real risk is $18.07.
  - Contracts only drop below 500 when the stop is wider than 1% of price, so at shipped config the block carries one bit: edge or no edge.
  - `max_leverage 0` **removes** the cap: 1,383 contracts, 13.8× leverage.
  - `contract_face_usd 0` gives 0 contracts with `risk_usd` still 50.
- **Critique:**
  - Fixture A10 pins the $50 as correct.
  - The global b is already parked as K-1. Including fees on the loss side flips the sign of the edge: quiet NY p=.55 has a net f\* of **−0.28** while the display shows the 5% cap (P17).
  - `contract_face_usd` is a venue constant; `TickSize` is already a `Const`.

**F8 · MEDIUM: one number lives in two keys (`stop_max_atr_mult` and `atr_stop_multiplier`), and both are tunable**
- **Location:** `SignalEmitter.vb:454-488`. `SettingsDiffApplier.vb:185` (both proposable).
- **Downstream impact:** the stop stops being monotonic, and Kelly sizes on a different stop from the one that's placed.
- **Failure scenario (P15):** set `stop_max` to 1.2. A swing stop $500 away gets clamped to $48, but *no* swing stop gives $64. Kelly sizes on $64.
- **Critique:** DG2 says "deliberately equal". Nothing enforces it, and the mode-1 comment assumes it.

**F9 · MEDIUM: a hot-reload during a run mixes two settings versions**
- **Location:** the run takes its `cfg` snapshot at `MainForm_Analysis.vb:53`, but `DynamicNorms.vb:28,123,142` reads `SettingsLoader.Current`, as do `ResolveSource` at `:754` and `BindCardKelly` / `BindCardIndicatorDetails` at `:674-675`.
- **Downstream impact:** the norms come from v(N+1) while scoring uses v(N), and `settings_version` stamps N. The Kelly card and the payload can disagree.
- **Failure scenario (P8):** the run's cfg shows `static_fallback` 1.5 at v68, while DynamicNorms uses 2.7 from v69. The window is the length of the fetch await.
- **Critique:** a run needs one immutable snapshot, passed down explicitly.

**F10 · MEDIUM: operational saves change verdicts with no version bump, and a failed save leaves the change live**
- **Location:** `MainForm_Layout.vb:1583-1592`, `MainForm_AutoRun.vb:102,348`, `SettingsLoader.vb:280-289`, and the CSV header at `AnalysisLogger.vb:92-127`, which has no version column.
- **Downstream impact:** rows can't be split on a version edge when the `min_net_move_pct` floor, the trigger mode or the interval changes.
- **Failure scenario:**
  - The UI mutates the live `Current` object and then calls `Save`.
  - If the write throws, the label reads "save failed" but the new floor is already scoring.
  - `Save` serialises the POCO, so any key the running binary doesn't know (a new settings.json deployed ahead of the new binary) is stripped on the next toggle.
- **Critique:** the loader's own comment says trigger_mode "moves scoring… divergence is invisible". The UI saves it with `bumpVersion:=False` anyway.

**F11 · MEDIUM: with a duplicated block, the engine reads the last copy, the drift guard reads the first, and a box with an overlay fails to parse**
- **Location:** `SettingsLoader.vb:384` (`JsonNode.Parse` on the overlay path) against `:490` (the serializer). `Program.vb:11856` (A62 matches the first occurrence, case-insensitive).
- **Failure scenario (P12):** a second `mtf_gate` block with `min_of 0`. Without an overlay the engine loads min_of 0 and bans shorts. With any overlay present the parse fails, so it's POCO at startup or last-good on reload. A62 sees min_of 2 and reports clean.
- **Critique:** the same file behaves three ways depending on which box it's on.

**F12 · MEDIUM: parse failures are reported wrongly and drop the overlay**
- **Location:** `SettingsLoader.vb:475-481`. `MainForm_Layout.vb:1944-1945`.
- **Failure scenario:**
  - A trailing comma during hot-reload: the engine keeps running v68 with the bridge on, while the status bar says "running on code defaults", which would be v1 with the bridge **off** (P9).
  - A base parse failure at startup ignores `settings.local.json`, so `trade_store.enabled` comes up true on the box that opted out (P10).
  - `stop_min_floor_ticks: 4.0` rejects the whole file (P11).
  - `"structural_levels": null` loads cleanly and switches to legacy geometry (P1).
- **Critique:** the system is honest about *whether* the parse failed and wrong about *what is running*.

**F13 · MEDIUM: `request_timeout_seconds` is read once, in a static constructor**
- **Location:** `DeribitClient.vb:26-29`.
- **Downstream impact:** hot-reload does nothing, and a value of 0 kills REST for the rest of the process.
- **Failure scenario (P19):** with 0, every call throws `TypeInitializationException`, and it keeps throwing **after the file is fixed back to 15**.
- **Critique:** the key is admitted by the per-box overlay on the grounds that network keys only "change whether a run skips".

**F14 · MEDIUM: session boundaries and the VWAP anchor are fixed UTC hours, with no DST handling**
- **Location:** the `session_volume` hours, `VWAP.session2_start_*` (`MainForm_Analysis.vb:276-283`).
- **Failure scenario:**
  - On 2026-10-25 the London open moves from 07:00 to 08:00 UTC.
  - On 2026-11-01 the US cash open moves from 13:30 to 14:30 UTC. The session-2 VWAP reset then fires an hour before the open, and NY's 1-minute candles and burst thresholds start 1.5 hours before the open.
- **Critique:** every per-session calibration was fit on summer data. Five weeks from now, each bucket holds a different hour of flow, with no version edge.

**F15 · MEDIUM: `_pct` means two different units**
- `target_buffer_pct` and `stop_buffer_pct` are percentages (divided by 100).
- `min_net_move_pct`, `hvn_proximity_pct` and `verdict_*_pct` are fractions.
- P7's 0.05 case shows how much it costs to guess wrong.

**F16 · MEDIUM: the drift guard skips every nullable field**
- **Location:** A62c ("PRESENT in JSON is Skipped, never compared").
- **Downstream impact:** the per-session seeds for aggressor-velocity arming and fallback targets are never checked against JSON on the parse-failure path. Only the ROC keys are (A63a). A26e pins them to literals, so it passes if the JSON moves.

**F17 · LOW: `signal_bridge.output_path` resolves relative paths against the process working directory** (`SignalEmitter.vb:559-564`). The D3 rule forbids exactly that for `trade_store.store_dir`.

### Per-key range and sign table

Tags: **P#** proven by a run, **code** read in the source, **—** constraint only, consumer not traced.

| Key(s) | Shipped | Must hold | What happens outside that |
|---|---|---|---|
| ADX/RSI/ROC/DMI/ATR period; EMA fast/mid/slow; BBW, Donchian, Volume SMA and TTM periods | 9/9/9/9/7; 9/21/50; 20/20/9/20,7 | Integer ≥1 and ≤ the bars fetched (1m 250, execution 250, 5m 210). EMA fast < mid < slow | ATR 0 throws (code); EMA above the fetch returns 0 (known) |
| `BBW.period × series_window_multiplier` | 100 | ≤ 250 | — |
| `ADX.range_threshold` < `trend_threshold` | 20 < 25 | Strict | — |
| RSI zones | 40/45/50/55/60; div 35/65 | 0 < oversold < partial < midline < partial < overbought < 100; low < high for both divergence pairs | — |
| `ROC.*_threshold`, `series_lookback` | .05/.1, 3 | ≥0; lookback ≥2 | — |
| `VWAP.session2_start_hour/minute` | 13:30 | 0–23 / 0–59, **with DST handling** | F14 |
| `BBW.std_dev`, `squeeze_percentile` | 2.0, .20 | >0; (0,1) | — |
| `Donchian.quartile_pct` | .25 | (0, 0.5) | ≥.5 makes q3 ≤ q1, so the middle band votes LONG_PARTIAL on every bar: a long bias (code `:531-545`) |
| `ATR.static_ref`; `scale_min` ≤ `scale_max` | 38; .25/4 | >0; min ≤ max | Inverted: throws every run → F1 stall (P4) |
| `Volume` clamp min/max pairs; `static_mid` < `static_high` | 2/6, 1.5/4; 2<3 | min ≤ max; mid ≤ high | Inverted: throws (P4) |
| `VWAPDynamic.dev_clamp_min` ≤ `max`; `static_fallback` inside that range | .3/3; 1.5 | Same | Inverted: throws (P4) |
| `OFI.sell_dominant_ratio` < 1 < `buy_dominant_ratio`; `book_depth`, `avg_window_sec` | .625/1.6; 5, 10 | Reciprocal; ≥1; >0 | — |
| `OBV.trend_gate`, `divergence_gate` | 23, .001 | >0 | — |
| `Liquidations.large_liq_size`, `dominance_ratio` | 200, 2 | >0; >1 | — |
| `OI.*_pct` | .05, .002 | ≥0 (unit not traced) | — |
| `CVD` / `MicroCVD` thresholds, weights, penalties; `TFI.threshold` | see file | Penalties ≥0 (sign flips them, F4); `TFI.threshold` in (0,1); MicroCVD window ≥3 | Window sizes also set `MinTradesForScoring` |
| `TTM.flat_threshold` | .5 | ≥0; absolute price units (parked D1 issue) | — |
| `VPFR.num_buckets`; `decay_base`; `lvn` < `hvn` ≤ 1; `value_area_pct` | 50; .985; .2/.6; .7 | ≥1; (0,1]; ordered; (0,1] | 0 buckets throws (code `:133-135`); decay >1 weights **older** bars more (code `:140`) |
| `funding.momentum_*` | 5 min, 2e-7, 1/1 | >0; ≥0; ≥0 | — |
| Every integer bonus/penalty (oi_cvd, regime_weights, trend_structure, aggressor, absorption, all `scoring.*_penalty/boost`) | 1–2 | **≥0** | Negative gives padding or a flipped sign (P14) |
| `swing` wings and lookbacks | 3/30, 2/20 | wing ≥1; lookback > 2×wing; 5m ≤210, 15m ≤70 | — |
| `aggressor_velocity` | 5/120 (NY 60), thresholds 2.5/4.5/5.5 | 0 < fast < norm; burst threshold >1; lean floor [0,1); session keys must equal `session_volume` names | Rename disarms (P3) |
| `absorption` fracs | .30/.10/.05 | break_tol < band ≤ proximity; >0 | Scoring is off, so blast radius is low |
| `spread.tight` < `wide` | 1.5/5 | Ordered | `wide` is unreachable at a 1-tick book (known since the v34 rebaseline) |
| `session_volume.sessions[]` | 3 buckets | Cover 0–23 exactly once, start ≤ end, unique names used by every override dictionary; `execution_resolution` ∈ {1,3,5} | Gaps, wraps and renames change behaviour silently (P2, P3, P18) |
| `resolution_profiles` keys | "1","3" | Must include every resolution in use | Missing "3": 3-minute sessions use the 1-minute ROC values (P2) |
| `mtf_gate.min_of` | 2 | **{2,3}** only | 0 or 1 is bull-biased; ≥4 turns the gate off (P5) |
| `mtf_gate.candle_lookback`, `adx_period`, `adx_min` | 60, 9, 20 | 50 ≤ lookback ≤ 70; adx_period < lookback−1; adx_min in (0,100) | <50 makes the EMA leg bull-only (P5) |
| `auto_run` interval, `trigger_mode` | 1 min, on_close | ≥10 s; one of {interval, on_close} | — |
| `scoring.verdict_*_pct` | .35/.53/.70 | 0 < weak < med < strong ≤ 1 | ≤0: a 1-point lead is a WEAK verdict (code) |
| Funding bands | ±8e-5 / ±1e-5 | high− < low− < 0 < low+ < high+ | Sign slip gives a long bias (P13) |
| `atr_target_multiplier`, `atr_stop_multiplier` | 1.75, 1.6 | >0; stop multiplier equal to `stop_max_atr_mult` | ≤0 puts a long's stop at or above entry (code); mismatch (F8, P15) |
| `structural_levels.target_max_atr_mult` | 3.5 | ≥ every fallback multiplier | Otherwise it rejects a structural target tighter than the fallback it accepts (code) |
| `structural_levels.stop_min_floor_ticks` | 4 | Should come from fees and ATR | F6 (P16) |
| `*_arbitration_mode`; `*_buffer_pct` | 0; 0 | {0,1}; **percent** > −100 | F15 |
| `trade_costs` | 1.5/3.5/mm/.0005 | Effective floor >0; `min_net_move_pct` a fraction in [0, .01] | Floor ≤0 turns the gate off; 0.05 means a 5% floor (P7) |
| `hold_*`, `context_tag_*`, `tier_floor`, `regime_gates`, `regime_max_score` | — | TP long >0> TP short; floors < thresholds, thresholds ordered; mid ADX < high ADX; everything ≥0 | — |
| `kelly` | 1000/½/.05/10/.45/.2/5 | account >0; 0 < max_risk ≤ .05; face = 10 (venue constant); floor + scale ≤ 1; **max_leverage >0** | 0 leverage removes the cap; 0 face gives risk ≠ contracts (P6) |
| `network.request_timeout_seconds`, retry keys, `ws_heartbeat_sec` | 15, 1/1000, 30 | ≥1 (restart to apply); ≥0; ≥10 | 0 poisons REST for the whole process (P19) |
| `performance_display.enabled`, `max_gap_fill_minutes` | true, 5000 | Must be true (known deadlock); ≤5000 | — |
| `exit_guard`, `live_strip`, `alerts`, `trade_store` numerics | — | — | Floored with `Math.Max` at the consumer (code): the only guarded blocks |
| `signal_bridge.output_path` | Absolute path | Absolute or empty | Relative resolves against the working directory (F17) |

### Not verified
- **Base-file watcher and rename-based saves.** The base watcher only handles `Changed` (`SettingsLoader.vb:698-702`), so an editor that saves by renaming may not trigger a hot-reload. I couldn't test this: Linux's file watching (inotify) behaves differently from Windows.
- **Frequency of sub-fee `SWING_STOP` rows.** There's no data in the container to measure it.
- **What the order app does with `risk_usd`.** That's the other repo.
- **Session-start doc reads.** I skipped the CLAUDE.md start protocol (the project doc, the architecture doc, the trader profile). The audit rests on the code and the docs cited above.

To re-run the proofs, see [`proofs/engine-settings/README.md`](proofs/engine-settings/README.md) for the exact command and its output. In the session they were run from a scratchpad copy of the same program.
