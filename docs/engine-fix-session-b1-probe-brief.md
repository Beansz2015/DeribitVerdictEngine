# Implementer brief — Session B1: the liquidation-flag measurement probe

**You are Session B1 ONLY.** Build the probe, start it, leave it running. **Do NOT build the fix** — that is Session B2, and it goes to a different seat after your measurement returns.

**Spec:** [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md). **Read it in full.** Your work is its §4.1. The rest of the spec is context you need — a seat that reads only its own section gets the neighbouring traps wrong.

---

## 0. Model and effort

**Model / effort: Opus, medium.**

**Why that tier.** The code is mechanical and `tools/WsTradeProbe/WsTradeProbeProgram.vb` already does the WebSocket half — subscribe, receive loop, parse, CSV out. **The tier is for the asymmetry, not the difficulty: there is no way to test this probe except by running it, and a silent mistake costs a full day of wall-clock on a build that has about nine days of runway.**

**Where you will specifically slip:**

1. ⛔ **Re-serialising a parsed struct instead of dumping the raw JSON text.** The existing probe parses into `ProbeTrade` and writes a CSV. **That is exactly what destroys the answer** — the whole question is what field NAMES arrive, and a parsed struct has already thrown them away. You need `JsonElement.GetRawText()` or the raw message text, written verbatim.
2. ⛔ **Building arms 1 and 3 and calling it done.** Arm 2 is the arm that answers the question. Arms 1 and 3 alone can only report "no liquidation seen yet", which is indistinguishable from "no liquidation happened".
3. ⚠ **Stopping the run too early.** Liquidations pass roughly twice a day. **Do not stop until arm 2 has paired at least one flagged REST trade, and prefer two** — one trade cannot separate a channel-wide omission from a one-off.

**Escalation trigger — stop and report, do not proceed:** the measurement shows the stream DOES carry a readable liquidation flag on some trades. That contradicts finding `L-1`'s 51,107-of-51,107 measurement, and the whole fix design changes. Surface it; do not judge it mechanical and push through.

---

## 1. What to build

Three arms in one run, per [`docs/engine-fix-build-spec-2026-09-21.md`](engine-fix-build-spec-2026-09-21.md) §4.1:

| Arm | What it does | What it settles |
|---|---|---|
| **1** | Dump the **raw JSON text** of every streamed trade object to a rolling file | What field names `trades.BTC-PERPETUAL.100ms` actually delivers |
| **2** | Poll REST `public/get_last_trades_by_instrument` over the same window. For every REST trade whose `liquidation` is not `none`, find the streamed message with the same `trade_id` and print **both raw objects side by side** | Whether that exact trade reached the stream with the field under another name, with no field at all, or not at all |
| **3** | Subscribe `trades.BTC-PERPETUAL.raw` in parallel and record whether ITS objects carry the field | Whether the `100ms` aggregation is what drops it |

⭐ **Arm 2 is why you do not wait blindly.** The REST copies ARE flagged — finding `L-1` measured 91 liquidation trades held twice under the same `trade_id`, the streamed copy flagged `none` and the REST copy flagged `T` or `M`.

---

## 2. Home and boundaries

**Home: `tools/WsTradeProbe/`.** Extend the existing project or add a file to it. It has its own `WsTradeProbe.vbproj`, and `tools/**` is excluded from the root project glob, so nothing you add there reaches the app build.

⛔ **Do NOT touch any of these. They belong to the other seat:**

- `Core/` · `UI/` · `analysis/` · `DeribitWsFeed.vb` · `DeribitClient.vb`
- `verify/ordercheck/Program.vb` — ⛔ **especially the fixture dispatcher at lines 737-750.** You allocate no fixture ids and add no fixtures.
- `settings.json` — no key changes anywhere in this build. It stays v68.
- `docs/UserManual.md`

**Safety, and this is not negotiable:**

- **Dev machine only. NEVER the collector box.**
- Public channels only. No authentication, no orders, no credentials.
- Writes only into its own working directory. It must not touch collector state, `analysis_log.csv`, the trade store, or any `settings.json`.
- The existing probe's own header states this contract — keep it true.

---

## 3. Order of work

1. **Build the probe.**
2. ⭐ **COMMIT the instrument BEFORE the long run.** Do not hold the code hostage to a 24-hour wait. A crash, a usage-limit stop or a closed terminal must not cost you the build.
3. **Start the run.** Leave it running.
4. **When arm 2 pairs a flagged trade**, write the read document and commit it.

---

## 4. Acceptance

1. The probe builds: `dotnet build tools/WsTradeProbe/WsTradeProbe.vbproj -c Release`.
2. The instrument is committed before the run starts.
3. The run produces **paired raw-JSON output** — at least one flagged REST trade set beside its streamed message, or beside an explicit record that no streamed message carried that `trade_id`.
4. A read document under `docs/` states the run window, the number of pairings, and the answer: is the field absent, present under another name, or is the trade itself missing from the stream.
5. `git diff --stat` touches `tools/WsTradeProbe/` and `docs/` only.

---

## 5. Reporting

Report back with **two documents**, per [`docs/batch-review-packet-convention.md`](batch-review-packet-convention.md):

- a `*-batch-summary.md` outcome record — what happened;
- a `*-spec-back.md` review packet — ranked verification handles with **pasted output**, decisions taken one line each, decisions queued with your read, feedback on this brief's own assumptions, and what you did not verify.

⛔ **Run every handle and paste its actual output. A handle that has not been run is a guess.** Pin each one to the commit your build started from, never to `HEAD`.

⛔ **Rank handles by whether the READER can run them.** Label an executable check `H-n` and build-time evidence the reader cannot re-run `E-n`. **Never rank an `E-n` first.** ⚠ Most of your evidence is a live run nobody can reproduce — say so plainly rather than dressing it as a handle.

⭐ **Log every auto-proceeded decision in ONE line** — the decision, the options, what you picked, why.

---

## 6. Standing rules that bind you

- ⛔ **Run `date -u` first.** The workstation is GMT+8; every project date is UTC.
- ⛔ **Run `git status -sb`. Never inherit a push state.**
- **Local-first commits.** Commit as you go; push only after it compiles and the trader has confirmed.
- **Host-agnostic:** no `System.Windows.Forms`, no `Control.Invoke`, no `MainForm` coupling in `tools/`.
- **Do not line-anchor greps over VB.** `^\s*Return` misses `If … Then Return`, the commonest form in this codebase.
- The output-format rules in `C:\Users\user\.claude\CLAUDE.md` apply to every reply: point form and tables, never a bare section number or bare ID, short active sentences that keep every domain term, and verified separated from carried.
