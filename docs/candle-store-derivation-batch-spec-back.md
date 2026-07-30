# Spec-back — candle-store derivation batch (2026-07-31)

**From:** the Opus orchestrator seat that ran JOB 1 + JOB 2 of the 2026-07-31 handover brief.
**For:** the reviewing seat. Working document — the outcome record is [`candle-store-derivation-batch-summary-2026-07-31.md`](candle-store-derivation-batch-summary-2026-07-31.md); the analysis is [`candle-store-derivations-2026-07-31.md`](candle-store-derivations-2026-07-31.md); JOB 1 is [`trade-store-capture-review-2026-07-31.md`](trade-store-capture-review-2026-07-31.md).

---

## 1. Ranked verification handles

Ordered by how much of the batch each covers. All are one command or one grep against files that exist now.

**If you run only one:** open [`candle-store-derivations-2026-07-31.md`](candle-store-derivations-2026-07-31.md) §1.3 and compare the two rows of the *production confirms the derivation* table. Derived TTM `FLAT%` at the live threshold is **0.55 / 0.41**; logged is **0.69 / 0.61**. Those come from completely independent paths — a 6-month store replay and 8,452 live rows — and agreeing to within 0.2pp is what makes the "the knob is ~100× too small" claim safe. Anything that broke the replay would show up here as a gross mismatch, not a near-miss.

| # | Claim | Cheap check | Load-bearing value |
|---|---|---|---|
| 1 | The whole store replay is faithful to production | Derivations §0.2 matched-replay table | **Row agreement 98.35% / 99.22%**, and res-3 directional **77.05% vs 77.05%** — the rate alone would tolerate offsetting errors; the *row* agreement would not |
| 2 | TTM's knob is inert (§0.1) | Derivations §1.3, both tables | Not the AWARD% — the **FLAT%**. 0.55/0.41 derived vs 0.69/0.61 logged. Two independent instruments, same answer |
| 3 | The transcriptions are not the weak link | Derivations §0 pin table | **0 mismatches** on 5,000 + 5,000 (TTM) and 3,000 + 3,000 (OBV) windows, against the *shipped* functions. Not "high agreement" — zero |
| 4 | The volume lever is inert live (§0.2) | Derivations §3.2, the two tables | The **p99**, not the p50: live `VolumeRatio` p99 is **2.04 / 2.37** against a `VolHighThr` p50 of **4.47 / 3.70**. The 99th percentile of the input sits below the threshold's median — so no multiplier in a plausible range changes the fire rate materially |
| 5 | `flat_threshold` is in price units, not normalized | `Core/Indicators_Volatility.vb:146–179` | `deltas(k) = cs(i).Close − SMA20` — a USD difference, compared directly against `flatThreshold` |
| 6 | The volume asymmetry is real, not inferred | `DynamicNorms.vb:39–42` vs `UI/MainForm_Analysis.vb:236–239` | The baseline comment says *"the final candle is in progress and excluded"*; the numerator is `candlesExec.Last().Volume`. **Excluded from the threshold, and used as the value** |
| 7 | Swing widening is a bad trade | Derivations §4 table | The **pair**, not either column: 3→5 buys 7.5pp of durability and costs **14.3pp** of placeability |
| 8 | Lookback is not binding | Same table | **Median pivot age 8 bars** against a lookback of 30 — and lb45 moves placeability +0.1pp. The age is the reason; the +0.1pp is the confirmation |
| 9 | The ×2.1 proxy over-scales TTM | Derivations §1.4 | **76.48 / 52.58 = 1.45**, and it is stable across anchors (p25 1.50, p75 1.37) — a single-percentile coincidence would not be |
| 10 | Nothing was consumed or changed | `git status -sb`; `sed -n 2p settings.json` | Settings **v64** (the trade-store build's, not mine); **A49** and **HC28** still free; my only writes are four `docs/*.md` |

**Arithmetic identity worth spot-checking:** in derivations §1.3, `AWARD% + FADING% + FLAT% = 100` on every row of both ladders. A transcription error in `TtmDelta` would almost certainly break that closure at some threshold.

---

## 2. Decisions queued, with my read

### D-A · TTM `flat_threshold` re-anchor — **my read: move it, target AWARD ≈ 50%**

Options: **(a)** 1m **25.0** / 3m **40.0** (AWARD ≈ 50%); **(b)** 1m 20.0 / 3m 30.0 (FLAT ≈ 22%); **(c)** leave at 0.5.

**My read is (a), and I'll label it a hypothesis where it is one.** What is *not* a hypothesis: 0.5 does not do the job the FLAT band was added for — that is measured on two independent instruments. What *is* a hypothesis: that ~50% is the right AWARD rate. The distribution says what each threshold buys; it cannot say what selectivity a Tier-1 confirmation signal should have. I prefer (a) over (b) because `AWARD%` **is** the vote, whereas FLAT% is an intermediate — and under (a) the 3m value carries its own measured ratio (1.60) rather than an inherited proxy.

**Scoping, offered without recommending it.** `indicators.TTM.flat_threshold` is a flat global. Per-resolution values need `ResolutionProfile` (`Core/Settings/EngineSettings.vb:112–113`, `Dictionary(Of String, ResolutionProfile)`) to gain a nullable `ttm_flat_threshold` plus a resolver read — **the exact shape v40 used for `roc_slope_delta_threshold`**, so it is precedent, not new machinery. The call site already passes the value from cfg (`UI/MainForm_Analysis.vb:282–285`). **Display parity:** no line is added, removed, renamed or re-formatted — the BBW/TTM breakdown note keeps its shape and only its *state values* change — so the parity rule is satisfied by construction, on the v52 burst-suffix reasoning. A single-value change (1m only, no profile key) is the narrower option and touches one JSON literal.

**⚠ This is a live scoring change** and needs its own spec and dataset boundary.

### D-B · OBV `trend_gate` 18.0 → ≈23 — **my read: move it; low risk**

This restores v33's own stated ~50% design point, which drifted to 58–62% because the original fit used one session's p50 (18.5) and six months puts it at 22.2–24.2. One key, no new POCO field, no resolver, no display format change. The risk is that OBV divergence blocks cross-category upgrade, so a less-directional OBV unblocks some upgrades.

**D-A and D-B share a root** and should be ruled together: both are ⚠ scoring changes, both derived from the same store on the same day, and the sequencing rule is one scoring change per dataset boundary. Ruling them separately either serializes two boundaries or risks an inconsistent outcome. Bundling at one boundary with trader sign-off is the v52+v53 precedent.

**Rider, needs no ruling:** OBV needs **no per-session or per-resolution split** — p50 spread 22.2–24.2, 3m/1m ratio ≈0.95. Worth recording as a decision-of-record so it is not re-proposed.

### D-C · Does session-volume calibration park behind D3? — **my read: yes, and the dependency should be recorded**

The volume vote fires on **0.69%** of NY runs. Tuning its multiplier is tuning a dial on a threshold nothing reaches, and a closed-bar-derived multiplier describes the D3 closed-bar arm rather than the live engine. **`backlog-dependency-map.md` does not currently carry this edge.** Adding one row — *session_volume multipliers · blocked-by · D3 forming-bar ruling* — is a doc edit, costs nothing, and stops the next seat re-deriving what I just derived.

**Not a decision I'm asking for, but flagged:** v58's *direction* (dial back an unjustified weekend-set notch) stands on its own terms, but its stated **mechanism** — the notch "suppressing trades" — could account for at most a fraction of a percentage point through the volume channel. I am not proposing a reversal. I am flagging it because the same reasoning is queued next for LONDON and NY, and it should not be applied a second time without the §0.2 context.

### D-D · Close the two swing-pivot §12 rows as "no change, evidenced"? — **my read: yes**

Both knobs are fine and the evidence is unusually clean (lookback is provably not the constraint; widening the wing is a 2-for-1 bad trade). The only reason not to close them is if someone wants the outcome-joined false-positive rate, which is blocked.

### D-E · §12 doc-drift — **my read: just fix it**

`DeribitIndicatorProject.md` §12 line 330 still lists the 3-min weekday-ASIA `session_volume` re-verify as an open **Medium** row. `roadmap.md` (2026-07-22), `settings.json` v58 and `backlog-dependency-map.md` line 10 all record it **CLOSED**. Doc-only; no ruling needed, but someone should own it. **I did not edit §12** — the brief scoped me to derivations, and silently editing the backlog of a doc I was told to read seemed like the wrong call. Say the word and it's a one-line fix.

---

## 3. Spec-back proper — feedback on the brief

### What the brief got right, specifically

**The dividing line did real work.** *"Candle/volume-derived work is unblocked NOW; anything needing verdicts or outcomes is NOT"* — stated up front and applied per item — is what kept this batch from drifting into the failure matrix. It also gave me the exact language to describe why the swing "false-positive rate" is unavailable, rather than quietly substituting a proxy and calling it done.

**The closing note on posture produced the single most useful paragraph in the summary.** *"When two parties agree on how to SCORE a discrepancy, that is exactly the moment to verify the INPUTS."* I hit a 15pp discrepancy, and the instruction to chase it rather than score it is what led to the matched replay — which found the fault was **mine**. Without that framing the honest-but-wrong move was available: report 58–62% as "the population value" and note production "runs hotter."

**"Confirm the next-free fixture family against `verify/ordercheck/Program.vb` at each build rather than trusting a number quoted in a doc"** is the right instinct generalized. I applied the same rule to the §12 rows and found the drift in D-E.

### Which assumptions broke

**1. Item 4 was specified as a derivation; it is a blocked dependency.** The brief lists *"§12 session volume multipliers — the per-session volume distribution is candle-derived. Unblocked."* The distribution is candle-derived and I produced it. But the **engine does not consume that distribution** — it compares a partial-bar ratio against a closed-bar threshold, so the lever is inert at 0.69% live fire. **A candle-derived multiplier recommendation would have been a correct number answering the wrong question.** This is the highest-value item in the packet: the brief's own fence (*"do not extend the D3 evidence into a live change"*) turns out to be *upstream* of item 4, not parallel to it.

**2. The data-quality note does not reproduce.** *"At least one candle in the store has a zero close … do not take raw price extrema from the store without filtering."* Across all four resolutions there are **zero** non-positive OHLC bars. What exists is **zero-volume** bars (2.1% of 1m). The substituted guidance: price extrema are safe unfiltered; **volume**-denominated work needs the divisor guard, and both live sites already have it. Cost of the substitution: none — but a future seat acting on the original note would filter the wrong axis.

**3. The TTM row asks for the wrong quantity.** §12 (inherited by the brief) says *"review FLAT vs RISING/FALLING against 1m candle range distribution."* The knob gates the 7-bar drift of `close − SMA20`, not the candle range. Right order of magnitude, wrong quantity — and the difference matters, because the ratio you get from candle range is the ATR-ish ×2.1, while the measured ratio for the actual quantity is **1.45**. Substituting the real quantity is what produced the §12 Phase-2 answer.

**4. The item-1 status was stale in the brief *and* in the handover it inherited from.** The brief says the row is *"PARTIALLY unblocked — read carefully"*; `fable-handover-2026-07-31.md` §4.2 lists it as *"Directly unblocked."* Both were written after **v58 closed the live half on 2026-07-22**. The brief's instruction to check §12 + roadmap first is exactly what caught it — the instruction was right and the summary line was wrong. Worth noting that §12 itself is the stale source, so anyone reading it first inherits the error (D-E).

**5. The JOB 1 checklist is stricter than the spec it reviews.** *"Verify no HttpClient reached either"* — but the spec's §2 says gap repair *"is the one place the app takes a dependency on the runner's networking, and is why D5 exists,"* and D5 was ruled Yes. The app is *supposed* to gain one. Separately, the fixture project already linked `HistoricalStore` **before** this build, so that half of the check could not pass in either direction. Both are checklist bugs, not build bugs, and I flagged them rather than raising false positives.

### Where the brief was narrower than its own words

*"Each item is a DERIVATION … It reads the store, produces a RECOMMENDED threshold with the evidence behind it, and stops."* Two of the four items do not end in a threshold: item 3 ends in **"no change, here's why,"** and item 4 ends in **"blocked, here's the measurement that shows it."** Both are legitimate outputs and I treated them as such, but the sentence as written implies every item yields a number, and an implementer taking it literally would be under pressure to manufacture one for item 4. Suggest: *"…produces a recommended threshold, a reasoned no-change, or a blocking finding."*

### Constraint pair that nearly conflicted, and the hatch

Three rules together looked like a deadlock: **(i)** derivations are analysis and must not add repo surface; **(ii)** measure, don't proxy — use the real shipped math (the v40 precedent, and the A43f lesson that a re-implementation can be internally consistent and wrong); **(iii)** never build Debug, because `bin\Debug` is the live collector. (ii) pushes toward building against the repo; (i) and (iii) push away from touching it.

**The hatch:** a **scratchpad-only** project that `<Compile Include>`s the repo's `Core/Indicators_*.vb` files **by absolute path**, supplying only the `Candle` DTO so `DeribitClient.vb`'s `HttpClient` never comes along. Real shipped math, zero repo surface, no repo project file modified, and its own `bin\` far away from the collector's. Worth writing into the next derivation brief explicitly — I spent time deciding it was allowed, and the next seat shouldn't have to. The residual (two scalars the shipped functions don't return) is covered by the pin discipline in derivations §0, which is the generalizable half: **transcribe only what you must, and pin the transcription against the shipped function's own output.**

---

## 4. What I did not verify, and cannot

- **Nothing was joined to outcomes.** Every recommendation here is a *distributional* argument — what fires how often. Whether a more selective TTM or a re-anchored OBV **improves accuracy** is unmeasured, and unmeasurable until trades-covered replay exists. Both D-A and D-B are "restore the stated design intent," not "this will make money."
- **The swing false-positive rate.** Blocked by the brief's own dividing line. "Crossed within 20 bars" is direction-blind and I did not treat it as a substitute.
- **The WS-vs-REST bar difference.** The matched replay disagrees on 1.65% / 0.78% of rows. I attributed that to the known §12 *WS 3-min closed-bar volume undercount* row but **did not isolate it** — a real check would compare WS-built bars against REST bars directly, which needs a live capture window.
- **Weekend and holiday behaviour.** Everything is weekday-filtered, matching the §12 rows' framing. Weekend distributions are unexamined.
- **Whether `resolution_profiles` is the right home for a TTM key.** I confirmed the POCO shape supports it and that v40 set the precedent; I did not check whether the auto-tweaker fence (HC11) covers a new profile key by prefix or would need extending. That belongs to whoever specs D-A.
- **Any live-app behaviour, for either job.** No app was run. For JOB 1 specifically that means no observed capture, no observed once-on-start repair against a real restart, and nothing checked on AWS — enumerated in [`trade-store-capture-review-2026-07-31.md`](trade-store-capture-review-2026-07-31.md) §5.
- **The trade-store build's runtime characteristics** — buffer memory across multi-day uptime at ~60k trades/day, and month rollover under a live stream rather than a synthetic straddling batch.
