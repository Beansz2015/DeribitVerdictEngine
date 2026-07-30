# Review — `settings.local.json` per-box overlay proposal

**Reviewer:** the Opus orchestrator seat, 2026-07-31. **Reviewed:** [`settings-local-overlay-proposal.md`](settings-local-overlay-proposal.md), AWAITING TRADER, D1–D6 open, nothing built.
**Verdict: the mechanism is right and the problem is real — but the whitelist as drawn admits three keys that demonstrably move the failure rate, which defeats the spec's own §2 argument.** Fix is small and local to §2. Everything else I'd build as written.

---

## 1. What it gets right

**§1.2 is the best thing in the document, and the author found it unprompted.** `Save()` writing the *merged* document back into the tracked file would promote a local-only override into `settings.json`, and from there onto AWS on the next xcopy. For `trade_store.enabled` that is exactly the direction that loses tape silently and permanently. Identifying it, naming it "the single most important thing to get right", and giving it a dedicated fixture (A50c) with the inversion spelled out as the regression trap is the correct treatment.

**The problem is real and I verified the mechanism.** `DeribitVerdictEngine.vbproj:56–58` does carry `<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>` on `settings.json`, so a newer tracked file genuinely does overwrite the local copy on build. The chore §0 describes is not hypothetical, and the asymmetry argument (tracked `false` un-corrected on AWS loses tape permanently; tracked `true` un-corrected locally costs disk) correctly forces the tracked seed to carry the AWS value and makes the local box the standing exception.

**§3's insistence on visibility is the right lesson from the right evidence.** Three silent divergences in two days — a 28-day funding hole, a lost month of candles, a self-stomping capture flag — is a good reason to make an override mechanism announce itself in the title bar *and* the console.

---

## 2. The finding — the whitelist admits scoring-relevant keys

§2's argument is that per-box divergence is safe **exactly** for blocks already fenced off the tweaker surface, because both mean *"this cannot move the failure rate."* The rule is elegant. **The list does not satisfy it.**

### 2.1 `network.transport` moves scoring

`transport` is not plumbing. It selects the market-data source for the entire run:

- `UI/MainForm_Analysis.vb:69` — `ResolveSource()` picks `WsMarketDataSource` vs `RestMarketDataSource` by `cfg.Network.Transport`.
- `MtfRefreshPolicy.vb:12–20` — branches on transport: `"ws"` always refreshes 15m, `"rest"` keeps the TTL gate.
- WS serves a 100 ms book/trades/ticker; REST serves snapshot polling. The whole v38→v42 migration existed because that difference is material, and the microstructure signals built on it (#5 aggressor velocity, #6 absorption) are fed from `MarketState`'s WS folds, which the REST path never populates.

So a box on `transport: rest` computes different OFI, different absorption, different aggressor velocity — and therefore different verdicts — **while stamping the same settings version**. That is precisely the invisible, unfilterable straddle §2 was written to prevent. The spec names the use case ("a box may need `transport: rest`") without noticing it is the counterexample to its own rule.

The reason `network.` carries HC12 is that the tweaker has no business tuning timeouts and retries — **not** that transport cannot move the failure rate. The two justifications are not the same, which is where the one-list claim cracks.

### 2.2 `auto_run.trigger_mode` and `interval_minutes` move scoring

Both are in-block under HC14, and both are load-bearing:

- **`trigger_mode`** (`interval` | `on_close`). My own JOB 2 §3.2 finding depends entirely on it: `on_close` is *why* `VolumeRatio` reads a newly-opened bar, giving a live p50 of **0.0100** against a closed-bar 0.66 and a volume-vote fire rate of **0.69%**. A box on `interval` samples mid-bar and gets a materially different volume vote. Same settings version, different scoring.
- **`interval_minutes`** — run cadence. The **entire v53 funding time-anchored-window rewrite** exists because cadence changed `FundingMomentum`: FLAT 52.1% at 60 s versus FLAT 0% / engaged 96% on 3-minute sessions. That is the repo's own documented proof that cadence is scoring-relevant.

### 2.3 Two rows already break the stated rule on their face

`performance_display.` and `analysis_logging.` appear in the table with **"—"** under *Fenced by*. They have no HC fence, so "exactly the set of blocks already fenced" is not what the list is. Both entries are *substantively* fine — perf strip, OHLC gap-fill, metric mode and the output dump touch no scoring path — but they show the derivation is doing more rhetorical work than the list supports.

### 2.4 Recommended fix — small, and it preserves the good idea

Move to **key granularity for the two offending blocks** rather than abandoning the rule:

| Block | Admit | Reject |
|---|---|---|
| `network.` | `request_timeout_seconds`, `retry_count`, `retry_backoff_ms`, `ws_url`, `ws_heartbeat_sec`, `ws_stale_after_sec`, `ws_cooldown_sec` | **`transport`**, `shadow_parity` |
| `auto_run.` | *(nothing, or an explicitly justified subset)* | **`trigger_mode`**, **`interval_minutes`**, `interval_seconds` |

And restate the rule honestly: *a key is safe to diverge per box when it cannot move the failure rate; the HC fences are a **good first filter** for that, not a proof.* Blocks whose fence exists for a different reason (network) need per-key review. That keeps §2's insight — which is genuinely valuable and worth keeping — without overclaiming.

If the trader wants a REST-mode box, that is a legitimate need, but it should be a deliberate, version-visible act, not a silent overlay. It is also the case that a REST box's rows should arguably be *excluded* from the pooled book rather than merely flagged — which is a bigger question than this spec.

---

## 3. Cross-spec dependency — two in-flight specs interact

The [coverage-report review](trade-store-coverage-report-review-2026-07-31.md) raised **C3**: the report cannot distinguish a capture defect from `trade_store.enabled:false`, and I suggested a **D7** to read the flag so a deliberately-disabled box does not report every up-hour as a defect.

With this overlay built, that flag no longer lives in `settings.json` on the local box — it lives in `settings.local.json`. **D7 must therefore read the *merged* value, not the base file.** If it reads the base it sees `true`, and the coverage verb reports every hour on the local store as a capture defect — the exact false alarm D7 exists to prevent.

Neither spec currently mentions the other. Whichever builds second inherits the dependency; cheapest is to note it in both §6/§8 now.

---

## 4. Smaller points

- **`dotnet clean` deletes the override, silently, and the failure direction is the bad one.** §1 says "documented, not defended against." On the local box that means capture silently switches back **on** — the very state F6 ruled against. §3's own principle ("an overlay must never be silent") applies to its *absence* as much as its presence: the title-bar `+local` marker makes absence observable, so it is worth adding "title bar reads `+local`" to the daily glance in `aws-collector-deploy-checklist.md`, which the trader already performs. Cheap, and it closes the loop.
- **§5's build-time question resolves cleanly, and I can confirm the mechanism.** `tools/checks/verify-gate.ps1:126` sets `$enginePrefixes = @('Core/', 'DynamicNorms.vb', 'analysis/')`, so `Core/Settings/SettingsLoader.vb` does trip it — but the check is **WARN-only** ("nudge only", line 141), so it cannot block, and the `[no-engine-change]` token at line 138 satisfies it outright. One caveat from the v64 review's F5: `$msgs` comes from `git log $base..HEAD`, i.e. **committed** messages — so the token has to be in a commit, and a pre-commit gate run sees neither the change nor the token.
- **Fixture family A50 is correct** (A49 reserved by the coverage report, still unbuilt). Next free after this is A51. Both A49 and A50 are now reserved by unbuilt specs; no conflict, but worth tracking so a third spec does not reach for A50.
- **A50 coverage looks right**, with one addition worth making: a fixture pinning that an overlay **cannot** change a scoring value end-to-end — i.e. build `EngineSettings` with a `scoring.*` overlay present and assert the verdict is byte-identical through the real `Calculate()`, the A42a/A36a pattern. A50d proves the key is *ignored at parse*; the stronger claim in §5's header ("Scoring impact: NONE by construction") deserves a pin at the scoring surface, not just the loader.

---

## 5. D-table — where I land

| # | My position |
|---|---|
| **D1** | **(a) whitelist — but redraw it per §2.4.** The principle is right; the list needs `network.transport` and `auto_run.*` removed. (b) is correctly rejected for the pooling reason. (c) is correctly rejected as re-openable. |
| **D2** | **Agree (a).** Surviving a build is the actual requirement, and a CLI arg or env var does not survive the Startup-folder shortcut the collector runs under. |
| **D3** | **Agree (a) ignore + log.** A box that will not boot loses more data than a box on shared settings — and on AWS "will not boot" is silent tape loss. |
| **D4** | **Agree (a) + console**, and extend it: the *absence* of the marker matters too (§4 above). |
| **D5** | **Agree (a).** Conditional on D1 staying at (a); the spec already says so, which is the right guard. |
| **D6** | **Agree (a) out of scope.** |

**No decision here is mine to make** — these are the trader's. Where I have a read I have given it; D1 is the only one where I am arguing against the spec as written rather than agreeing with it.

---

## 6. What I did not verify

- **Nothing is built** — every claim about merge, `Save`, hot reload and rejection behaviour is a claim about the spec, not code.
- **The `JsonNode` deep-merge approach** is plausible and idiomatic but untested; array-replace semantics (§1, A50g) in particular are asserted, not demonstrated.
- **Whether any *other* whitelisted block hides a scoring-relevant key.** I checked `network.` and `auto_run.` because they looked suspicious, and both were. I did **not** audit `signal_bridge.`, `alerts.`, `live_strip.`, `exit_guard.`, `performance_display.` or `analysis_logging.` key-by-key. Given two of eight blocks failed on inspection, **that audit should happen before build**, not after.
