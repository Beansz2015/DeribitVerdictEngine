# Item 6 — `WsTradeProbe` through the shared trade reader: the `S-1` re-check

**Status:** ⛔ **MEASUREMENT ONLY — no code changed. ONE DECISION IS OWED before any build.**
**Seat:** Opus, 2026-09-07 (UTC). **Trigger:** [`seat-handover-2026-09-06.md`](seat-handover-2026-09-06.md) §0 pick 3 — *"⛔ Read the code first; nobody has checked whether `S-1` still holds."* **This is that read.**
**Against:** [`seam-audit-2026-08-11.md`](seam-audit-2026-08-11.md) §3.1 (`S-1`) · [`trader-tick-queue.md`](trader-tick-queue.md) §2 row *"Route `tools/WsTradeProbe` through the shared trade reader"*.

⚠ **All dates UTC.** At the time of writing, UTC is **2026-09-07 (Monday)** while the workstation clock reads 2026-09-08.

---

## 0. The answer, in three lines

1. ✅ **`S-1` STILL HOLDS — verified line by line at `eb05a4b`, not inherited.** Four weeks on, the probe is unchanged.
2. ⛔⛔ **But its recommended FIX is not available, and the queue row's own escalation trigger FIRES.** Measured, not predicted: linking `DeribitClient.vb` does not compile, and making it compile drags the settings loader — which **writes `settings.json` if absent** and installs two `FileSystemWatcher`s.
3. ⭐ **The structural fix the trigger points at is small and clean** — `TradeRecord` is fully self-contained. **But it is a different job from the one the queue row scoped, so it needs a decision before a slot.**

---

## 1. `S-1` still holds — every claim re-verified at `eb05a4b`

| `S-1`'s claim (2026-08-11) | State at `eb05a4b` (2026-09-07) | |
|---|---|---|
| Probe declares its own `TradeId` / `TradeSeq` at `:53-54` | **`:53-54`, unchanged** | ✅ holds |
| Reads them with private `ReadString`/`ReadLong` at `:203-204` | **`:203-204`, unchanged** | ✅ holds |
| Helpers at `:333` (`ReadLong`) / `:355` (`ReadString`) | `ReadLong` **`:333`** · `ReadString` **`:353`** | ✅ holds (2-line drift) |
| Validity tested as `TradeSeq > 0` | **`:250`, unchanged** | ✅ holds |
| `WsTradeProbe.vbproj` links **none** of the shared sources | **Confirmed — the project has NO `ItemGroup` at all** | ✅ holds |

**The shared seam also has not moved:** `TradeRecord.AbsentSeq` = `-1L` at `DeribitClient.vb:342`, `ReadTradeId` at `:384`, `ReadTradeSeq` at `:402`. **So all five divergences `S-1` tabulated are still live**, unchanged in either direction.

⭐ **Nothing has degraded and nothing has healed. `S-1` is exactly four weeks older.**

---

## 2. ⛔⛔ The escalation trigger FIRES — and the cost is worse than the trigger anticipated

The queue row scopes the fix as ***"project ref + two call swaps"*** and names the stop condition:

> ⚠ **Escalation trigger:** if linking `DeribitClient.vb` drags an `HttpClient` into the probe, **stop** — that is the same network/format split that put `TradeStoreWriter` in `Core/`, and it needs the same structural treatment, not a workaround.

### 2.1 Measured, by building it

⭐ **This was RUN, not reasoned.** The probe's `.vbproj` was edited, built, and reverted (`git checkout`, tree confirmed clean).

| Step | `Compile Include` added | Result |
|---|---|---|
| 1 | `..\..\DeribitClient.vb` | ⛔ **Build FAILED** — `BC30451: 'SettingsLoader' is not declared` at `DeribitClient.vb(15,46)` **and** `(30,19)` |
| 2 | `+ Core\Settings\SettingsLoader.vb` `+ Core\Settings\EngineSettings.vb` | ✅ **Build succeeded** — cascade terminates at depth 2 |

⭐ **The cascade is BOUNDED — three files, not unbounded.** That is the one piece of good news and it is worth stating plainly, because "drags in the whole app" would have been the easy assumption.

### 2.2 ⛔ But look at what those three files are

`DeribitClient.vb:10` — `Private Shared ReadOnly _http As New HttpClient()`
`DeribitClient.vb:13-15` — `Shared Sub New()` reads `SettingsLoader.Current.Network.RequestTimeoutSeconds`

**So the trigger's literal condition — *"drags an `HttpClient` into the probe"* — is met.** But the settings loader is the bigger problem, and the trigger did not anticipate it:

- `Core/Settings/SettingsLoader.vb:46-47` — **two `FileSystemWatcher` fields.**
- `Core/Settings/SettingsLoader.vb:245-247` — **`Initialise` WRITES a default `settings.json` if the file does not exist.**

### 2.3 ⛔⛔ Why that specifically is disqualifying

`tools/WsTradeProbe/WsTradeProbe.vbproj:12-14` states a design guarantee, in the file, in capitals:

> **DELIBERATELY STANDALONE. It links NOTHING from the app: no Core files, no settings loader, no TradeStoreWriter. It cannot read or write any file the collector uses, so it can be run on the same box as a live collector without touching its state.**

⛔ **The `S-1` audit did not weigh that comment, and the queue row does not mention it.** The probe is built to run **on the production collector box**. The proposed fix links the settings loader by name — the exact component the guarantee excludes by name.

⚠ **At runtime, today, it would still be safe — and that is the trap, not the reassurance.** `TradeRecord`'s shared readers are `Shared` members of `TradeRecord`, a *different class* that happens to share a file with `DeribitClient`. Touching them does **not** run `DeribitClient`'s static constructor, so no `HttpClient` is built and no settings are read.

⛔⛔ **The safety would therefore be an ACCIDENT of VB static-initialisation semantics, not a declared property.** After the fix, the `.vbproj`'s guarantee is false as written, and one future line — a seat adding a REST cross-check to the probe — silently arms a `settings.json` write and two file watchers **on the box running production capture.**

⭐ **That is a strictly worse trade than the defect it fixes.** `S-1`'s divergence is real but provably unreachable today (see §3). The fix would introduce a latent write-to-production-state risk in its place.

---

## 3. How much is actually at stake in `S-1` — the honest bound

⚠ **Restated from the queue row and re-checked, because it governs whether this is worth a slot at all.**

- **The only reachable divergence** is a genuine `trade_seq = 0`: a valid sequence to production (`n >= 0`), absent to the probe (`> 0`), which would drop it from the G2 contiguity check at `WsTradeProbeProgram.vb:250`.
- **Deribit's `trade_seq` runs around 296 million.** A zero is not a value the venue produces.
- **G1 and G3 use `timestamp` only and are completely unaffected.**

⭐ **So the probe is not currently returning a wrong answer, and `S-1` never claimed it was.** The claim is that *"the gate and the thing it gates can disagree, and nothing asserts they agree"* — a class defect, held safe by an assumption in a comment rather than by shared code.

⛔ **Which means this item's value is hygiene, not correctness — and that has to be weighed against §2's cost, because the obvious fix makes a real guarantee false to remove a divergence that cannot fire.**

---

## 4. ⭐ The structural fix the trigger points at — measured, and it is clean

The trigger says the answer is *"the same network/format split that put `TradeStoreWriter` in `Core/`"*. **That precedent is exact:** `TradeStoreWriter` was split because it owned a live `HttpClient` — the format/rollover/guard half went to `Core/TradeStoreWriter.vb` and links everywhere including the fixture project; the network half stayed in `tools/BacktestRunner/`.

**`TradeRecord` is the same shape, and it is already separable.** Verified by reading `DeribitClient.vb:317-420`:

| Check | Result |
|---|---|
| References to `DeribitClient`, `HttpClient`, `SettingsLoader`, `_http` inside `TradeRecord` | ⭐ **NONE — fully self-contained** |
| Namespaces it actually uses | `System.Globalization` · `System.Text.Json` only — ⛔ **not `System.Net.Http`** |
| Members | `Price` · `Amount` · `Direction` · `Liquidation` · `Timestamp` · `TradeId` · `AbsentSeq` · `TradeSeq` · `ReadTradeId` · `ReadTradeSeq` |

### 4.1 Blast radius — measured, not estimated

**Move `TradeRecord` from `DeribitClient.vb` into a new `Core/TradeRecord.vb`:**

| Surface | Impact |
|---|---|
| **Call sites** | ⭐ **ZERO changes.** Same namespace, same type name — VB does not care which file a class lives in |
| **Root `DeribitVerdictEngine.vbproj`** | ⭐ **ZERO changes** — it globs `**/*.vb` (removing only `tools\**` and `verify\**`) |
| **Projects that link `DeribitClient.vb`** | **5, each gains ONE `Compile Include` line:** `tools/AutoTweaker` · `tools/BacktestRunner` · `tools/CeilingAudit` · `tools/WhatIfRunner` · `verify/ordercheck` |
| **`tools/WsTradeProbe`** | gains **ONE** `Compile Include` — `Core\TradeRecord.vb`, which has **no dependencies at all**, so the standalone guarantee SURVIVES intact |

**Then** the two call swaps `S-1` originally asked for become available, and the probe's own `ReadString`/`ReadLong` can go.

---

## 5. ⛔ THE DECISION — one question, three options

**This is owed before any build.** The queue row's sizing (*"project ref + two call swaps · Sonnet, medium"*) does not survive §2, so the item has to be re-scoped or dropped.

| | Option | Cost | My read |
|---|---|---|---|
| **(a)** | ⭐ **Split `TradeRecord` into `Core/TradeRecord.vb`, then route the probe through it** — the structural treatment the trigger names | 1 file move · 6 `.vbproj` one-line edits · 0 call-site changes · a spec with a D-table | ⭐ **This is the right END STATE and the trigger pre-authorises its direction** |
| **(b)** | **Accept `S-1` and record why** — the divergence cannot fire at `trade_seq` ≈ 296 M; document the assumption at both sites and close the row | ~30 minutes, doc-only | ⭐ **Defensible, and cheaper than (a) by a wide margin** |
| **(c)** | Link the three files as originally scoped | ⛔ **Rejected — §2.3.** Trades a benign divergence for a latent settings-write on the production box, and falsifies a stated guarantee | ⛔ **Do not take this** |

### ⭐ My read: **(a), but NOT NOW — and I want to say why the sequencing matters more than the choice.**

**(a) is right on the merits.** It removes the class rather than guarding it, it is the same one-seam pattern as `ComputeSideLevels` and `TradeStoreWriter`, and — ⭐ **the part that makes it worth more than this one probe** — a dependency-free `Core/TradeRecord.vb` means *any* future tool can parse a Deribit trade without importing an HTTP client and a settings loader. **The probe is the first caller to need that, not the only one.**

⚠ **But nothing is bought today.** `S-1` cannot fire, the write-guard fix it gates has long since shipped and been verified on a 2,033× sample, and the probe has not been run since 2026-08-11. **(a) is a correctness-preserving refactor whose only current beneficiary is an instrument nobody is using.**

⛔ **So the honest recommendation is: rule (a) as the direction, and schedule it behind anything with a live consumer.** ⭐ **If a slot is wanted now, `D3-RESIDUAL` has a real if small user-visible effect and this does not.**

⚠ **What I would NOT do is (b).** Not because it is wrong — it is defensible — but because *"held safe by an assumption in a comment"* is the precise wording of the defect class this project has now hit four times, and writing the assumption down a fifth time is what (b) amounts to.

---

## 6. What I did NOT verify

- ⛔ **The probe was never RUN.** No WS connection was opened. Everything here is source reading plus two compile experiments.
- ✅ **CLOSED — the probe's own baseline WAS checked.** `dotnet build tools/WsTradeProbe/WsTradeProbe.vbproj -c Release` at `eb05a4b`, unmodified: **Build succeeded, 0 errors, 0 warnings.** *(This bullet originally read "I did not check whether the probe still builds green today" — it was a 30-second check and it should not have been left in this list.)*
- ⛔ **I did not confirm the `trade_seq` ≈ 296 M figure against the venue.** It is carried from `seam-audit-2026-08-11.md` and the queue row. **It is load-bearing for §3's "cannot fire" claim** — if it is wrong, `S-1`'s urgency changes.
- ⚠ **I did not measure whether moving `TradeRecord` breaks any `.vbproj` I did not find.** The five were found by `grep -rln 'Compile Include=".*DeribitClient.vb"' --include=*.vbproj .` — **a project referencing it by a different path form would be missed.**
- ⚠ **No runtime proof that `DeribitClient`'s static constructor stays cold** under option (c). That claim rests on VB/CLR static-initialisation semantics, read not executed — it is an argument against (c), so being wrong would only strengthen the case against it.
