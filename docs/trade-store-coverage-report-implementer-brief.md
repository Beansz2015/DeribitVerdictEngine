# Implementer brief — C1 trade-store coverage report

**Status: BUILD-AUTHORIZED 2026-08-03.** All seven decisions ticked. **Both parts** are in scope.
**Spec:** [`trade-store-coverage-report-proposal.md`](trade-store-coverage-report-proposal.md) — read it in full; this brief carries only what the spec does not, plus two rulings that change two of its rows.
**Verified in the tree 2026-08-03** (per the §1a convention): `BacktestProgram.vb:50` dispatches `fetch` / `replay` / `validate` / `report`. **No `coverage` verb exists.** Genuinely unbuilt.

---

## 0. Model + effort recommendation

**Sonnet 5 at high effort. Not Opus.** The judgment work is done — the proposal, the two rulings in §2 and this brief pin every decision, so nothing here needs novel design. Every mechanical piece has a close in-repo template: **four existing verbs** for dispatch (`BacktestProgram.vb:114-214`), **`WsHealthLog` / `AlertsSidecar`** for the marker's never-throws contract, the **EXIT GUARD strip / `MIN NET MOVE %` row** for a parity-exempt live element, and **A48/A50/A51** for the fixture idiom. It is a large build, not a hard one.

**Split across two sessions** — it is a lot for one context:

| Session | Scope | Effort |
|---|---|---|
| **1** | D7 marker line → Part A verb + S0–S4 + six-class logic → **A49a–l** | **high** |
| **2** | Part B `TAPE STORE` element → **A49m** | **medium** |

The marker goes first because A49k/l cannot be written without it. Session 2 is genuinely easier: a read plus a label over state `TradeStoreWriter` already tracks, parity-exempt, two precedents.

⚠ **Two inversion traps are where a fast read will fail, and both yield a report that looks fine and is backwards** — see §6.2 and §6.1. The fixtures are meant to catch them, **but the implementer writes the fixtures too**, so a misunderstanding propagates into its own test. Verify these two by hand before accepting the build.

**Escalate to Opus if** the implementer proposes collapsing any of the six classes in §2.3, or "simplifying" the classification. That is the tell that it has not internalised why six exist, and it is the failure mode that costs tape.

---

## 1. The D-table, settled

| # | Ticked |
|---|---|
| **D1** | **(a)** new `coverage` verb — not a section inside `report` |
| **D2** | **(a)** consume the uptime records, degrade gracefully when absent |
| **D3** | **(a)** `--gap-ms` as a CLI flag, not a settings key |
| **D4** | **300,000 ms** — and **no longer provisional**, see §2.2 |
| **D5** | **(a)** build **Part B in the same build** |
| **D6** | **(a)** `--strict` opt-in, not default |
| **D7** | **(a)** one marker line per process recording the flag + `store_dir` |

**D7 = (a) is free** because D5 = (a): Part B already touches the app, and the marker line's only cost was that it stopped Part A being tools-only.

---

## 2. Two rulings bind this build. They are not optional reading.

### 2.1 [`j-b-scoping-ruling-2026-08-02.md`](j-b-scoping-ruling-2026-08-02.md) — scope by positive record

The defect default applies **only to hours positively recorded as capturing**. Scope comes from D7's marker line — **never from reading `trade_store.enabled` at run time** (that gives the value *now*, not during the historical window; and post-overlay a base-file read sees `true` and flags every local up-hour as a defect). **D7 = (b) was ruled out for this reason.**

### 2.2 [`weekday-scope-ruling-2026-08-03.md`](weekday-scope-ruling-2026-08-03.md) — weekday hours only

The report evaluates **weekday hours only**. This **retires J-C's "extended to include a weekend"** requirement and settles D4: 300,000 ms was derived at 1.85× the observed 2m42s max on a **Wed→Thu** window — already a weekday basis. **Confirmed, not provisional.** There is **no outstanding data dependency**; the REST-backfill-a-weekend task is cancelled.

⚠ **Part B is the exception and stays UNCONDITIONAL** — it must report on Saturday and Sunday. An app dead on Friday night is still dead on Monday morning. Liveness detection is not performance evaluation.

### 2.3 The classification set — six classes, and none may be collapsed

| Class | Trigger |
|---|---|
| `captured` | store rows present in the hour |
| `defect` | recorded capturing, hour short or ambiguous — **J-B in full**, incl. trailing window and cross-GUID gaps |
| `expected-missing` | recorded capturing, recorded not-up (S1's uptime join) |
| `not-capturing` | D7's marker says capture was off for that process life |
| `unknown-scope` | **no capture record exists for the window** (pre-marker history, or a copy-back that dropped the marker) |
| `out-of-scope-weekend` | Saturday/Sunday hour |

**Collapsing `not-capturing` into `expected-missing` produces the false-alarm storm the J-B clause exists to prevent. Collapsing `unknown-scope` into `not-capturing` silently absolves the windows least able to defend themselves.** Report all six distinctly.

---

## 3. Build shape

**Part A — the `coverage` verb** (`tools/BacktestRunner`, tools-only, no settings keys):
- Signals S0–S4 per spec §2. **S1's primary uptime record is `analysis_log.csv`** (~60 s heartbeat with `InstanceId`), **not** `ws_health.log` (32 lines/week, transition-only). `ws_health.log` supplements it with DEGRADED/REST and the all-runs-skipped case.
- **`WsHealthLog.LogStart` writes `DOWN` at process start, before the socket connects — so a `DOWN` line proves the app was ALIVE to write it.** Absent lines, not `DOWN` lines, indicate a dead process. The spec's §2 flags this because the token is genuinely misleading; get it backwards and the report inverts.
- **A trailing interval on a copied file is bounded by the copy time, not by now** — otherwise every AWS copy-back reads as a fresh death. Applies to `analysis_log.csv`, `ws_health.log` and the store alike.
- Usage line joins `PrintUsage` (`BacktestProgram.vb:269`).

**Part B — the live `TAPE STORE` element** (app side):
- `TAPE STORE: 12s · 47.3k rows` — seconds since the last **successful flush** (a flush proves the whole chain to disk; a trade only proves the stream) and rows committed this process. Amber past `3 × flush_seconds`, red past `10 ×`.
- `TradeStoreWriter` already tracks the state — this is a read plus a label.

**D7's marker line** — one per process, recording the resolved `trade_store.enabled` **merged** value plus `store_dir`. Follow the `WsHealthLog` / `AlertsSidecar` contract: `<exe>\`-relative, append-only, never throws.

---

## 4. Fences, parity, boundary — all settled, restated so nothing is re-litigated

- **Tweaker: NO new HARD CONSTRAINT.** Part A adds no settings keys. Anything Part B adds lands under `trade_store.`, already fenced by **HC27**. **HC28 stays free.**
- **Display-string parity: NO OBLIGATION.** Part A's surfaces are console + a markdown file, neither a parity surface (offline-report precedent). Part B is a **live status element ⇒ parity-exempt**, on the v62 `MIN NET MOVE %` / EXIT GUARD strip precedent — no snapshot line, no card binding.
- **Dataset boundary: NONE.** Nothing on the scoring path; `analysis_log.csv` untouched in schema and content. **It does not consume the one-⚠-per-window slot and cannot confound the running D3 ASIA watch.**
- **Version bump: only if keys are added.** The rule is keys-changed, not app-touched. If Part B derives amber/red from the existing `flush_seconds` and adds nothing, **no bump is required** — but a §15 entry is required either way. State which you did and why.
- **Reversibility:** Part A is additive and read-only — not running the verb *is* the rollback.

---

## 5. Fixtures — **A49** (reserved for this; verified free, 0 uses in the harness)

A49a–j are specified in proposal §6 and stand as written. **Three arms the rulings add:**

- **A49k** — `not-capturing`: a process life whose marker records capture OFF yields **zero defects** across a silence that would otherwise flag; a capturing life with the *same* silence yields defects. The inversion trap, in A49i's shape.
- **A49l** — `unknown-scope`: a window with **no marker at all** classifies `unknown-scope`, distinctly from both `not-capturing` and `expected-missing`.
- **A49m** — weekday scope: a Saturday/Sunday hour classifies `out-of-scope-weekend` and never `defect`, **while Part B's liveness read still reports on that same hour**.

**Acceptance:** solution + AutoTweaker + WhatIfRunner + CeilingAudit + BacktestRunner + OrderCheck **0/0 Release**; A1–A52a unregressed + A49a–m; verify-gate `prepush` **GATE PASSED** (run it **post-commit** — it reads committed diffs, so parity and version guards pass vacuously before).

---

## 6. Traps, from this project's own history

1. **Do not read `trade_store.enabled` to determine scope.** Ruled out. Use the marker.
2. **Do not conflate `DOWN` with dead.** `DOWN` proves the app was alive.
3. **Copy-back drops S1 silently.** The store is `<exe>\backtest_data\`, but **both** uptime records sit in `<exe>\` — the store's *parent*. Copying `backtest_data\` alone loses them. The report must say so when they are absent (A49g), not quietly skip S1.
4. **`--strict` is opt-in.** Interactive runs during a study must not fail on a known historical hole.
5. **The gap threshold is deliberately loose.** §0 measured a 2m42s silence as normal on a weekday, and per-hour volume spanning 1,555 → 15,602 trades. Do not "tighten it to something sensible" — that finding is why it looks blunt.
