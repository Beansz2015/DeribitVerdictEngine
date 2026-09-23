# Doc scanner (harness 4) — build spec-back: ⛔ STOPPED at the escalation trigger in `doc-scanner-check-spec.md` §5 item 3

**Written 2026-09-23 (UTC)** by the harness-4 implementer, model `claude-opus-5-5`, effort medium. **Spec:** [`doc-scanner-check-spec.md`](doc-scanner-check-spec.md). **Measurement it rests on:** [`doc-scanner-measurement-2026-09-22.md`](doc-scanner-measurement-2026-09-22.md). **Format:** [`batch-review-packet-convention.md`](batch-review-packet-convention.md). This packet also carries the outcome record, so there is no separate summary file.

| | |
|---|---|
| Start commit | `928aaeb` |
| Commits | `7e4ebfa` (enumeration module + pairing measurement) · `323c996` (fix: line number overwritten by line text) · this packet |
| Jev calls | **0**. No API key was loaded. No baseline was written. Nothing was run over the living set with a key |
| Settings / `.vb` / collector / trade store | Untouched |

---

## 0. ⛔ Outcome — read this first

`doc-scanner-check-spec.md` §0 names this trigger: *"§5 item 3 leaves more than 2 `never_shipped` hits, or drops any of the 11 labelled `E2` true positives."* **It fired.**

- `E2` is the settings-value arm (a settings key followed by a number that is not the live value). `never_shipped` means the number was never a shipped value of that key.
- `doc-scanner-check-spec.md` §4.2 rules 1–3, built as written: **`never_shipped` goes from 13 to 17** on the living set at `cbc2c91`. The target is ≤ 2.
- The other direction holds: **11 of 11** labelled `E2` true positives stay flagged.
- ⭐ **Why it cannot pass as written:** only 3 of the 13 original hits are pairing errors. **8 are NAME errors.** The single-word settings leaves `threshold` (`indicators.TFI.threshold`) and `penalty` (`indicators.absorption.penalty`) match ordinary prose words. No reading of rules 1–3 removes them, because in each case the number follows the word with only `(`, `:`, `=` or a space between them. The details are in §2 below.
- **What is built:** the Python enumeration step, all seven kept arms, the replay mode, and a committed script that measures five readings of the pairing in both directions.
- **What is NOT built:** `tools/checks/doc-scanner.ps1` (the baseline gates, the Jev calls, the 5-sample loop, the report, the exit codes). So `doc-scanner-check-spec.md` §5 items 5, 6 and 7 are not run, and there is no run record under `docs/harness-runs/`.

| `doc-scanner-check-spec.md` §5 item | State |
|---|---|
| 1 — code-only recall parity (replay) | ✅ **Enumeration side PASS** — see `H-2` and `H-7`. The PowerShell `-Replay` switch does not exist yet |
| 2 — candidate parity at `cbc2c91` (3 · 4 · 0 · 0) | ✅ **Enumeration side PASS** — `H-3` |
| 3 — the pairing fix, both directions | ⛔ **FAIL: 17, not ≤ 2.** 11 of 11 kept. **Trigger fired** — `H-1` |
| 4 — the five seeded positives surface | ✅ **Enumeration side: all surface** — `H-9`. Not run through the tool |
| 5 — refusals (no baseline · incomplete · revision mismatch) | ⛔ NOT BUILT |
| 6 — `verdict` is the only answer read; `Invoke-Jev` shared | ⛔ NOT BUILT |
| 7 — live acceptance run on historical docs only | ⛔ NOT RUN. No run record |

---

## 1. Ranked verification handles

- `H-n` = a handle you can run from the tree.
- `E-n` = build-time evidence you cannot run. There are none: every claim here has a runnable handle.
- I ran every handle exactly as printed here, from the repo root in Git Bash, and pasted its output unedited.
- The last run was at `HEAD` = `b5000c9`. That is another seat's docs commit. The code is unchanged since `323c996`: `git diff --stat 323c996 HEAD -- tools/checks/lib/doc_scanner_candidates.py tools/checks/measure/doc-scanner/` prints nothing.
- Documents are read at revision `cbc2c91` with `git show`, never from the working tree. So the output does not depend on `HEAD`.
- Run `export PYTHONIOENCODING=utf-8` first.
- ⚠ Pass file paths to `python -c` as arguments, never inside the `-c` string. Windows Python does not see Git Bash's `/tmp`, and Git Bash converts only arguments. This bit me once while writing `H-9`.

⭐ **If you run only one, run `H-1`.** It is the trigger evidence, and it measures both directions.

### `H-1` — the trigger, both directions, five readings (living set, `cbc2c91`)

```
python tools/checks/measure/doc-scanner/pairing_variants.py cbc2c91 | grep -E '^(REV|E2 true|---|  NEVER|  E2_TP)'
```

Drop the `grep` to see every `never_shipped` line and the label split.

```
REV=cbc2c91 LIVING_DOCS=12 living_missing=[]
E2 true_positive lines in the pre-registered labels: 11
--- legacy
  NEVER_SHIPPED_LINES=13  WAS_SHIPPED_ROWS=22  WAS_SHIPPED_LINES=20
  E2_TP_STILL_FLAGGED=11/11  lost=[]
--- literal
  NEVER_SHIPPED_LINES=17  WAS_SHIPPED_ROWS=22  WAS_SHIPPED_LINES=20
  E2_TP_STILL_FLAGGED=11/11  lost=[]
--- no_span
  NEVER_SHIPPED_LINES=10  WAS_SHIPPED_ROWS=22  WAS_SHIPPED_LINES=20
  E2_TP_STILL_FLAGGED=11/11  lost=[]
--- literal+nq
  NEVER_SHIPPED_LINES=6  WAS_SHIPPED_ROWS=22  WAS_SHIPPED_LINES=20
  E2_TP_STILL_FLAGGED=11/11  lost=[]
--- no_span+nq
  NEVER_SHIPPED_LINES=2  WAS_SHIPPED_ROWS=22  WAS_SHIPPED_LINES=20
  E2_TP_STILL_FLAGGED=11/11  lost=[]
```

- `legacy` = the measured instrument, unchanged. Its 13 matches `doc-scanner-measurement-2026-09-22.md` §2.
- **`literal` = what is built:** `doc-scanner-check-spec.md` §4.2 rules 1–3, as written.
- `no_span` = rule 1 without its second arm (*"or sit inside the same backtick span"*).
- `nq` = a name rule that is **NOT in the spec**: a single-word leaf name must have its parent segment on the same line, as a whole word.
- Identity check: in every reading, the 20 `was_shipped` lines split exactly as the pre-registered labels did (8 false, 11 true, 1 unsure). The full output shows this on the `was_shipped lines by pre-registered label` line.

### `H-2` — the tool's replay is the instrument, byte for byte

```
python tools/checks/lib/doc_scanner_candidates.py replay --kinds all --e2 legacy > /tmp/a.txt
python tools/checks/measure/doc-scanner/replay_recall.py > /tmp/b.txt
diff /tmp/a.txt /tmp/b.txt && echo "IDENTICAL ($(wc -l < /tmp/a.txt) lines)"
```
```
IDENTICAL (52 lines)
```

This proves that the copied `removed_lines()` and the output format are faithful. It is also the baseline that `H-7` builds on.

### `H-3` — `doc-scanner-check-spec.md` §5 item 2 (the tool as built, strict pairing)

```
python tools/checks/lib/doc_scanner_candidates.py scan --rev cbc2c91 --out /tmp/h3.json
```
```
SCAN_OK rev=cbc2c91 docs=12 CANDIDATES_VERSION=3 CANDIDATES_VALUE=22 CANDIDATES_POINTER=4 CANDIDATES_FIXTURE_MEANING=65 VALUE_NEVER_SHIPPED_CODE_ONLY=17 CFG_MEMBER_MISSING=0 LINE_PAST_EOF=0 DATED_STATE_OVER_HORIZON=12 NEXT_FREE_FAMILY_STALE=1 NEXT_FREE_FAMILY_CLAIMS=1 FIXTURE_MENTIONS_RESOLVED=65
```

`VERSION` · `POINTER` · `CFG_MEMBER` · `LINE_PAST_EOF` = **3 · 4 · 0 · 0**, which equals the measurement.

### `H-4` — the tool's plumbing reproduces the measurement's living-set table (legacy pairing)

```
python tools/checks/lib/doc_scanner_candidates.py scan --rev cbc2c91 --e2 legacy --out /tmp/h4.json
```
```
SCAN_OK rev=cbc2c91 docs=12 CANDIDATES_VERSION=3 CANDIDATES_VALUE=22 CANDIDATES_POINTER=4 CANDIDATES_FIXTURE_MEANING=65 VALUE_NEVER_SHIPPED_CODE_ONLY=13 CFG_MEMBER_MISSING=0 LINE_PAST_EOF=0 DATED_STATE_OVER_HORIZON=12 NEXT_FREE_FAMILY_STALE=1 NEXT_FREE_FAMILY_CLAIMS=1 FIXTURE_MENTIONS_RESOLVED=65
```

This equals every living-set count in `doc-scanner-measurement-2026-09-22.md` §2: `E1_strong` 3 · `was_shipped` 22 · `never_shipped` 13 · `E6` 4 · `E3_cfg`/`E3_eof` 0/0 · `E5` headers 12 · resolved fixture mentions 65. So `H-3` differs from the measurement in the pairing only.

### `H-5` — the measured instrument is unchanged

```
git diff --stat 6f95e42 -- tools/checks/measure/doc-scanner/enumerators.py tools/checks/measure/doc-scanner/replay_recall.py tools/checks/measure/doc-scanner/scan_head.py
```

The output is empty. `6f95e42` is the commit that made the measurement. The instrument can still replay its own recorded numbers, so a parity check against it is not circular.

### `H-6` — the other direction, over all 417 non-archive docs (`cbc2c91`)

```
python tools/checks/measure/doc-scanner/pairing_variants.py cbc2c91 --all | grep -E '^(REV|---|  NEVER|  E2_TP)|vs legacy' | sed -E 's/(lost [0-9]+) \[.*\]  (gained [0-9]+).*/\1  \2/'
```

The `sed` strips the lists of lost and gained lines. Drop it to see every line named.

```
REV=cbc2c91 ALL_NON_ARCHIVE_DOCS=417 living_missing=[]
--- legacy
  NEVER_SHIPPED_LINES=125  WAS_SHIPPED_ROWS=129  WAS_SHIPPED_LINES=109
  E2_TP_STILL_FLAGGED=11/11  lost=[]
--- literal
  NEVER_SHIPPED_LINES=123  WAS_SHIPPED_ROWS=142  WAS_SHIPPED_LINES=119
  E2_TP_STILL_FLAGGED=11/11  lost=[]
  WAS_SHIPPED_LINES vs legacy: lost 1  gained 11
--- no_span
  NEVER_SHIPPED_LINES=87  WAS_SHIPPED_ROWS=128  WAS_SHIPPED_LINES=108
  E2_TP_STILL_FLAGGED=11/11  lost=[]
  WAS_SHIPPED_LINES vs legacy: lost 1  gained 0
--- literal+nq
  NEVER_SHIPPED_LINES=60  WAS_SHIPPED_ROWS=114  WAS_SHIPPED_LINES=94
  E2_TP_STILL_FLAGGED=11/11  lost=[]
  WAS_SHIPPED_LINES vs legacy: lost 22  gained 7
--- no_span+nq
  NEVER_SHIPPED_LINES=43  WAS_SHIPPED_ROWS=105  WAS_SHIPPED_LINES=87
  E2_TP_STILL_FLAGGED=11/11  lost=[]
  WAS_SHIPPED_LINES vs legacy: lost 22  gained 0
```

- ⭐ **All 22 `was_shipped` lines that the name rule drops outside the living set are the top-level `version` key**, for example *"`version` 34 → 35"* and *"settings.json version 63"*. That key has no parent segment, so the rule always drops it. I checked this by printing each of the 22 lines. Those are settings-version quotes, which the `VERSION` arm (`E1_strong`) is meant to own.
- `no_span` drops 1 line, `docs/absorption-d2-stage1-rotation-build-spec.md:166` (*"`Version` is `1`"*). The legacy parser paired it through the word `is`.

### `H-7` — kept arms: the strict pairing changes no removed-line result in the replay

```
python tools/checks/lib/doc_scanner_candidates.py replay --kinds kept --e2 legacy > /tmp/l.txt
python tools/checks/lib/doc_scanner_candidates.py replay --kinds kept --e2 strict > /tmp/s.txt
diff <(grep -E '^\s+(HIT|miss)' /tmp/l.txt) <(grep -E '^\s+(HIT|miss)' /tmp/s.txt) && echo "REMOVED-LINE RESULTS IDENTICAL ($(grep -cE '^\s+(HIT|miss)' /tmp/s.txt) removed lines)"
grep -E 'removed content|info' /tmp/s.txt
```
```
REMOVED-LINE RESULTS IDENTICAL (40 removed lines)
  removed content lines: 32, flagged: 26; candidates on NON-removed lines: 166
  [info, not parity] removed content lines with a FIXTURE_MEANING candidate: 1
  removed content lines: 6, flagged: 1; candidates on NON-removed lines: 21
  [info, not parity] removed content lines with a FIXTURE_MEANING candidate: 0
  removed content lines: 1, flagged: 1; candidates on NON-removed lines: 16
  [info, not parity] removed content lines with a FIXTURE_MEANING candidate: 0
  removed content lines: 1, flagged: 0; candidates on NON-removed lines: 3
  [info, not parity] removed content lines with a FIXTURE_MEANING candidate: 1
```

- Kept-arm recall is **26/32 · 1/6 · 1/1 · 0/1**. The instrument's all-kinds figures were 28 · 3 · 1 · 0. The difference is 4 removed lines that only dropped arms caught: `E1_weak_cue` ×3 and `E3_missing_file` ×1. By `doc-scanner-measurement-2026-09-22.md` §3, none of those 4 was caught for the right reason.
- ⭐ The new `FIXTURE_MEANING` arm enumerates the `A56b` line that the measurement recorded as *"0 — semantic"* (the fix `2f46679`). It is kept out of the parity lines on purpose.

### `H-8` — the date trap in the instrument (a defect, not changed)

```
git log -1 --format='%cs' cbc2c91
python -c "import datetime,subprocess;t=int(subprocess.check_output(['git','log','-1','--format=%ct','cbc2c91']));print(datetime.datetime.fromtimestamp(t,datetime.timezone.utc).date())"
```
```
2026-09-23
2026-09-22
```

- `enumerators.rev_date()` uses `%cs`, which is the committer's recorded timezone (GMT+8 here).
- So every `DATED_STATE` age at `cbc2c91` is **one day high**.
- No header sits at the 7-day boundary (the nearest is 9 days), so no count changes at this revision.
- The scan JSON now prints both dates, as `rev_date_instrument` and `rev_date_utc`. This is `DS-B` below.

### `H-9` — `doc-scanner-check-spec.md` §5 item 4, enumeration side (strict scan, `cbc2c91`)

Run `H-3` first. It writes `/tmp/h3.json`.

```
python -c "import json,sys;d=json.load(open(sys.argv[1],encoding='utf-8'));[print(i['id']) for i in d['items'] if i['arm']=='VALUE' and (i['path'],i['line']) in {('docs/DeribitIndicatorProject.md',75)}|{('docs/UserManual.md',n) for n in (587,906,1128,1227,1228,1300,1598,1599,1600,1601)}];[print(x['id'],'claimed=A%d highest=A%d stale=%s'%(x['claimed'],x['highest'],x['stale'])) for x in d['code_only']['NEXT_FREE_FAMILY']];[print(x['id'],'|',x['detail']) for x in d['code_only']['DATED_STATE_OVER_HORIZON'] if (x['path'],x['line']) in {('docs/roadmap.md',31),('docs/roadmap.md',100),('docs/backlog-dependency-map.md',64)}]" /tmp/h3.json
```
```
VALUE|docs/DeribitIndicatorProject.md|75@cbc2c91
VALUE|docs/DeribitIndicatorProject.md|75@cbc2c91#2
VALUE|docs/UserManual.md|587@cbc2c91
VALUE|docs/UserManual.md|906@cbc2c91
VALUE|docs/UserManual.md|1128@cbc2c91
VALUE|docs/UserManual.md|1227@cbc2c91
VALUE|docs/UserManual.md|1228@cbc2c91
VALUE|docs/UserManual.md|1300@cbc2c91
VALUE|docs/UserManual.md|1598@cbc2c91
VALUE|docs/UserManual.md|1599@cbc2c91
VALUE|docs/UserManual.md|1600@cbc2c91
VALUE|docs/UserManual.md|1601@cbc2c91
NEXT_FREE_FAMILY|docs/roadmap.md|43@cbc2c91 claimed=A73 highest=A85 stale=True
DATED_STATE|docs/roadmap.md|31@cbc2c91 | 2026-08-24 age 30d, lines 31-75
DATED_STATE|docs/roadmap.md|100@cbc2c91 | 2026-08-07 age 47d, lines 100-109
DATED_STATE|docs/backlog-dependency-map.md|64@cbc2c91 | 2026-08-07 age 47d, lines 64-70
```

- The 11 seeded `VALUE` lines (12 IDs, because line 75 holds two) are exactly the 11 labelled `E2` true positives.
- The ages carry the one-day offset from `H-8`.
- ⚠ **This handle tests membership against a list I typed from `doc-scanner-measurement-2026-09-22.md` §5.** It cannot show a seeded line that is missing from my list. Cross-check: the list equals the 11 `true_positive` `E2` lines in the pre-registered labels file (`H-1`'s `E2 true_positive lines ... 11`).

---

## 2. The trigger match, line by line

This is code mechanics only: why a number got paired. It is **not** a tense read of any line. The 13 original lines are already labelled `false_positive` by the seat.

**The 13 original `never_shipped` lines, by cause:**

| Cause | Lines at `cbc2c91` | Removed by `doc-scanner-check-spec.md` §4.2? |
|---|---|---|
| Slash group, paired by position (rule 3) | `DeribitIndicatorProject.md:196` · `trader-profile.md:110` | ✅ yes |
| Number glued to a word (`20th`) | `UserManual.md:731` | ✅ yes, by the number-token definition (decision 5 in §3) |
| ⛔ Single-word leaf `threshold` matched in prose | `architecture.md:560` · `UserManual.md:1415, 1502, 2232, 2532` | ❌ no |
| ⛔ Single-word leaf `penalty` matched in prose | `trader-profile.md:78` · `UserManual.md:66, 1803` | ❌ no |
| Sum expression (`EstProbFloor + EstProbScale` = 0.45 + 0.20) | `UserManual.md:325` | ❌ no. No rule pairs across `+` |
| Correct key, adjacent value, value never shipped (`target_arbitration_mode: 1`, an enum option) | `UserManual.md:250` | ❌ no. This is not a parser error |

**The 7 new lines that rule 1's second arm adds** (*"or sit inside the same backtick span"*, read as the first number token after the key inside that span, subject to rule 2):

| Cause | Lines at `cbc2c91` |
|---|---|
| `threshold` inside a hyphenated file name in a link (`asia-burst-threshold-derivation-2026-08-01.md`), paired with the year `2026` | `DeribitIndicatorProject.md:345` · `trader-tick-queue.md:186, 204` · `backlog-dependency-map.md:68` |
| A comparison (`AtrStopMultiplier <= 0`) | `UserManual.md:290` |
| A division (`EstProbScale / 2`) | `UserManual.md:326` |
| Arithmetic (`MomentumWindow + 1`) | `UserManual.md:1264` |

⚠ These 7 were not in the pre-registered labels. My cause column is a read of the mechanism only, and I did not verify it against anything but the line text.

---

## 3. Decisions

### Decisions queued — for the reviewer

**`DS-A` — which E2 pairing to adopt** (it decides whether `doc-scanner-check-spec.md` §5 item 3 can pass). All figures are from `H-1` and `H-6`.

| Option | Living set: never / TP | All 417 docs: `was_shipped` lines lost / gained |
|---|---|---|
| (a) Literal rules 1–3 — **built** | 17 / 11 of 11 | 1 / 11 |
| (b) Drop rule 1's span arm | 10 / 11 of 11 | 1 / 0 |
| (c) Literal + name rule | 6 / 11 of 11 | 22 / 7 |
| (d) Drop span arm + name rule | **2 / 11 of 11** | 22 / 0 — every lost line is the top-level `version` key |
| (e) Keep (a), relax the ≤ 2 threshold | 17 / 11 of 11 | 1 / 11 |

- **My read, as a hypothesis: (d), with the top-level `version` key either exempted or left to the `VERSION` arm.** It is the only reading that meets the spec's own acceptance on the labelled set. The span arm adds noise and removes none: in `H-6`, (b) against (a) gains 0 lines and loses the same one.
- ⛔ **Why I did not take it: it is a TRADE, so it is reserved (`CLAUDE.md`, the three-step test).**
  - The name rule gives up possible recall to gain precision.
  - A real stale value quoted under a single-word key, with no parent segment on the line, would go unseen. For example: *"`threshold` (0.2)"* in a TFI section that does not say "TFI".
  - The measured cost is 0 of 22 `was_shipped` rows in the living set. Outside it, the cost is 22 lines, all of them `version`.
  - "0 in the living set" is one population at one revision.
- `never_shipped` rows are code-only and never reach Jev. So (e) costs only report noise. Its extra rows are 20 of 20 noise by cause (§2 above).

**`DS-B` — `DATED_STATE` ages use the GMT+8 commit date.** Options: (a) keep `enumerators.rev_date()` (`%cs`) for parity (built); (b) compute ages in the tool from the UTC date.

- My read: (b). It is the more truthful option, and the instrument can stay unchanged.
- Before taking (b), re-run `H-7`. A header exactly at the 7-day boundary in a replay case could flip. I did not measure this.

### Auto-proceeded — one line each (the decision · the options · my pick · why)

1. **Where the E2 fix lives** · edit `enumerators.py` behind a flag / a new module that imports it unchanged · **new module** (`tools/checks/lib/doc_scanner_candidates.py`) · the instrument stays byte-identical (`H-5`), so the parity items are not circular. This is the richer guarantee.
2. **Home of the living-set list** (`DS-D1`) · the PowerShell script / the Python module · **Python**, one home · the PowerShell side is to print it from the JSON (`living_docs`). There is no second copy.
3. **ID suffix** (`doc-scanner-check-spec.md` §4.4) · `#1..#k` on every sibling / the first bare and `#2..#k` · **first bare** · the spec's own example (`VALUE|docs/DeribitIndicatorProject.md|75@cbc2c91`) is a line that holds two `VALUE` candidates. The second one is `…75@cbc2c91#2`.
4. **Code-only ID prefixes** · reuse the arm name / name the finding · **name the finding**: `VALUE_NEVER_SHIPPED`, `CFG_MEMBER`, `LINE_PAST_EOF`, `DATED_STATE`, `NEXT_FREE_FAMILY` · a line with both a judged `VALUE` and a never-shipped pair cannot collide.
5. **What counts as a number** · any digit run (the instrument's) / a token not glued to a following letter or `.digit` · **token** · step 3 of the test: the richer option is mechanically wrong, because `20th` is not the number 20. It removes `UserManual.md:731`. A sentence-final `4.` still reads as 4.
6. **Rule 1's second arm** · a narrower reading / the literal one (first number token inside the same span, subject to rule 2) · **literal** · the spec says it plainly. What it costs is measured in `H-1` and `H-6` and queued as `DS-A`, not changed.
7. **Slash groups** (rule 3) · two keys only (the spec's example) / N keys · **N keys, by position, only when the number count equals the key count, else no pair** · `trader-profile.md:110` is a 3-key group.
8. **Jev state caps** (not specced) · none / cap · **the line is windowed on the candidate's column at 6,000 chars, context lines at 1,500 chars, and every cut is marked in the text** · one `DeribitIndicatorProject.md` row runs past 9,000 tokens (the `CLAUDE.md` session-start note). The largest item state at `cbc2c91` is 8,299 chars.
9. **Heading chain** · naive / fence-aware · **fence-aware** · `#` lines inside code fences are not headings.
10. **Replay kinds** · include `FIXTURE_MEANING` in the parity lines / keep it separate · **separate, printed as `[info, not parity]`** · it has no instrument counterpart, and mixing it in would break parity with `replay_recall.py`.
11. **Highest fixture family** (`DS-D9`) · letter-suffixed IDs only / also bare families · **also bare families** (`Sub A84_`, `Sub A85_`, `Check("A8 …")`) · the measured truth is A85, and `A85_` has no letter.
12. **Next-free claim** · a free-text parse / the first `A\d+` after *"next free fixture family"* on the same line, stale when the claimed family is at or below the highest · **the latter** · one typed claim, as `DS-D9` specifies. The JSON also counts `NEXT_FREE_FAMILY_CLAIMS`, so a vanished claim is not read as a 0.
13. **Commit date** · print one date / print both · **both** (`rev_date_instrument`, `rev_date_utc`) · the offset in `H-8` stays visible and is never silent.

⭐ **Did I pick the cheaper or less-informative option anywhere?** Not knowingly. Decision 10 keeps a new arm out of one set of lines, but it prints that arm's number right beside them. I name it here so you can judge it.

---

## 4. Feedback on the spec

- ⭐ **What worked:** measuring before speccing. Every recorded number reproduced exactly through the new plumbing (`H-4`), and the replay is byte-identical (`H-2`). **The escalation trigger did its job:** it stopped a premise error before any judge saw a row.
- ⛔ **The assumption that broke:** `DS-D4` and `doc-scanner-measurement-2026-09-22.md` §4 say *"`never_shipped` hits are parser errors — fix the pairing"* and *"a number paired with the wrong key"*. Measured: **3 of 13 are pairing errors that `doc-scanner-check-spec.md` §4.2 fixes. 8 are NAME errors: a prose word matched to a key. 1 is a `+` pairing that §4.2 does not cover. 1 is a correct pairing of a never-shipped enum value.** A pairing fix alone cannot reach ≤ 2.
- ⚠ **Rule 1's second arm works against its own purpose.** It adds 7 noise lines on the living set and removes none (`H-1`: `literal` 17 against `no_span` 10).
- ⚠ **`doc-scanner-check-spec.md` §5 item 1 names a "kept arms" result that `replay_recall.py` cannot produce**, because it has no kinds filter. I substituted a chain: (i) all kinds, legacy pairing, byte-identical to the instrument (`H-2`); (ii) kept arms, legacy against strict, with identical removed-line results (`H-7`).
- ⚠ **The instrument dates a revision in GMT+8** (`H-8`). It is the same clock trap this repo's memory records, now inside a committed instrument.
- ⚠ **The coverage block has no field for how many next-free claims were found.** `NEXT_FREE_FAMILY_STALE=0` cannot tell "the claim is fresh" from "the claim is gone". The JSON carries `NEXT_FREE_FAMILY_CLAIMS`, and the block should print it too.
- ⚠ **Constraint pair:** `DS-D1` says 12 living docs, and `ENUMERATOR_SUSPECT` fires when `LIVING_DOCS` < 12. A `-Docs` run scans fewer docs by design. I kept `LIVING_DOCS` as the resolved living-set size on every run and planned a separate `DOCS_SCANNED` count. That part is in the JSON (`docs_scanned`, `docs_in_living_set`). The PowerShell side does not exist yet.

---

## 5. What I did NOT verify

- ⛔ **The whole PowerShell layer. It is not built:** the gates, the revision-mismatch refusal, the 5-sample loop, the agreement rate, the report, the exit codes, and `ENUMERATOR_SUSPECT`.
- **Any Jev behaviour.** No call was made. That includes whether the `FIXTURE_MEANING` states (65 at `cbc2c91`) pass the web firewall that blocked one fixture-parser item.
- **`--docs` glob expansion.** It is not exercised: no `-Docs` run was made.
- **`last_shipped_version` for all 22 `VALUE` items.** I read the figures, and three agree with what the docs themselves say (OFI 2.0/0.5 last at v47, `trend_gate` 18 last at v65, funding at v18). I did not check them independently against `git log`.
- **The name rule at any revision other than `cbc2c91`.**
- **The 7 new never-shipped lines' causes** (§2 above). This is a read of the line text only.
- **Whether `DS-B` (b) keeps `H-7`'s parity.**

---

## 6. Doc rot I noticed and did NOT fix

⛔ I fixed nothing: the living set carries five seeded positives on purpose (`doc-scanner-measurement-2026-09-22.md` §5). The seat decides on each item below. All are checked at `HEAD` = `b5000c9` by reading the line.

| Where | What |
|---|---|
| `docs/harness-shadow-mode-protocol.md` §1, line 30 (living set) | Doc scanner row: *"about 380 docs, many pairs"*. Measured: 420 top-level docs, and whole-doc and doc-pair states are ruled out (`doc-scanner-measurement-2026-09-22.md` §1) |
| `docs/harness-shadow-mode-protocol.md` §7, line 228 (living set) | *"Harnesses 2 to 5 have not been built."* Harnesses 2 and 3 are built (`docs/seat-handover-2026-09-22.md` §2) |
| `docs/doc-scanner-measurement-2026-09-22.md` §4 (a record, not living) | Calls all 13 `never_shipped` lines *"a number paired with the wrong key"*. 8 are a WORD matched to the wrong key (§2 above) |
| `tools/checks/measure/doc-scanner/enumerators.py`, `rev_date()` (code) | Uses `%cs`, the GMT+8 commit date. Every `DATED_STATE` age is one day high at `cbc2c91` (`H-8`) |

⚠ The working tree held uncommitted edits to `docs/harness-shadow-mode-protocol.md` by another seat while I wrote this. The two rows above are read from `HEAD`, not from those edits.

---

## 7. ⛔ Resumed 2026-09-23 (UTC) — the orchestrator's rulings `DS-A` and `DS-B`: SECOND STOP

**Appended below the stopped record; nothing above this line is rewritten.** The orchestrator ruled on both decisions this packet queued in §3:

- `DS-A` (which E2 pairing to adopt). **Option (f)**, which was none of the five in §3: rule 1 without its backtick-span arm, plus the name rule as a **label** (`unqualified`), never a filter, with top-level keys exempt. (f) could be taken only if four named checks passed; otherwise stop and report.
- `DS-B` (b): the tool dates `DATED_STATE` from the UTC commit date.

Then the orchestrator asked for the spec amendment, `tools/checks/doc-scanner.ps1`, and `doc-scanner-check-spec.md` §5 items 5–7.

| | Result |
|---|---|
| `DS-A` option (f) | ⛔ **NOT confirmed.** Checks 1, 2 and 4 PASS. Check 3: the span arm's gains hold **11 correct key-number pairings in 14 rows**, so the ruling says *"do NOT drop the arm"* |
| `DS-A` fallback the ruling names: the span arm kept, but never inside a file name or a link target | ⛔ **Check 1 FAIL: 5 qualified `never_shipped` lines** (need ≤ 2) |
| `DS-B` (b) | ✅ **BUILT** (`0891089`). `H-7` re-run: no removed-line result flips |
| Spec amendment | **NOT written.** It would amend `doc-scanner-check-spec.md` §4.2 to option (f), which is not confirmed. I will write it as one block once `DS-A` is settled |
| `tools/checks/doc-scanner.ps1`, items 5–7 | **NOT built, NOT run.** The ruling orders them after `DS-A` |
| Jev calls | **0.** No key loaded, no baseline written, nothing over the living set |
| Tool behaviour now | E2 pairing: still the spec-literal reading (`--e2 strict`). Dating: UTC |

**Commits:**
- `0891089`: the (f) measurement plus the `DS-B` build.
- `9462b35`: an eighth reading, for information only (§7.4).
- This section's own commit.

### 7.1 Handles — run at `9462b35`, output pasted unedited

The conventions are the same as `docs/doc-scanner-build-spec-back.md` §1. Documents are read at `cbc2c91`. Run `export PYTHONIOENCODING=utf-8` first, and pass file paths to `python -c` as arguments.

**`H-10` — checks 1 and 4 (living set) for option (f), the restricted arm, and the information-only eighth reading.** ⭐ If you run only one, run this.

```
python tools/checks/measure/doc-scanner/pairing_variants.py cbc2c91 | grep -E '^(--- (ruled|restricted|no_span\+sep)|  QUALIFIED|===|CHECK)'
```
```
--- ruled(f)
  QUALIFIED_NEVER_SHIPPED_LINES=2  UNQUALIFIED_NEVER_SHIPPED_LINES=8  UNQUALIFIED_WAS_SHIPPED_LINES=0 []
--- restricted_span+label
  QUALIFIED_NEVER_SHIPPED_LINES=5  UNQUALIFIED_NEVER_SHIPPED_LINES=8  UNQUALIFIED_WAS_SHIPPED_LINES=0 []
--- no_span+sep+label
  QUALIFIED_NEVER_SHIPPED_LINES=2  UNQUALIFIED_NEVER_SHIPPED_LINES=8  UNQUALIFIED_WAS_SHIPPED_LINES=0 []
=== DS-A checks for reading ruled(f)
CHECK1 PASS living set: QUALIFIED never_shipped lines=2 (need <= 2), E2 TP flagged=11/11 (need all)
CHECK4 PASS living set: was_shipped rows=22 lines=20 (need 22/20); same (path,line,key,value) rows as literal=True, as legacy=True
=== DS-A checks for reading restricted_span+label
CHECK1 FAIL living set: QUALIFIED never_shipped lines=5 (need <= 2), E2 TP flagged=11/11 (need all)
CHECK4 PASS living set: was_shipped rows=22 lines=20 (need 22/20); same (path,line,key,value) rows as literal=True, as legacy=True
=== DS-A checks for reading no_span+sep+label
CHECK1 PASS living set: QUALIFIED never_shipped lines=2 (need <= 2), E2 TP flagged=11/11 (need all)
CHECK4 PASS living set: was_shipped rows=22 lines=20 (need 22/20); same (path,line,key,value) rows as literal=True, as legacy=True
```

- Under (f), the 2 qualified lines are `UserManual.md:250` and `:325`, the same two §2 above called genuine and `+`-paired.
- The 8 unqualified lines are the §2 `threshold`/`penalty` name errors, now kept in their own bucket and not dropped.
- The restricted arm's 3 extra qualified lines are `UserManual.md:290` (`AtrStopMultiplier <= 0`), `:326` (`EstProbScale / 2`) and `:1264` (`MomentumWindow + 1`). Its file-name guard removes the 4 `threshold`-in-a-file-name lines as intended.
- The five earlier readings still print exactly what `H-1` published. I checked this with an md5 of the first 17 lines of `H-1`'s grep, against the published text.

**`H-11` — check 2 and the span-arm recovery, all 417 non-archive docs.**

```
python tools/checks/measure/doc-scanner/pairing_variants.py cbc2c91 --all > /tmp/all.txt
grep -E '^(===|CHECK2|SPAN_ARM)' /tmp/all.txt | sed -E 's/(gained vs no_span=[0-9]+) \[[^]]*\]/\1 [list]/; s/(SPAN_ARM_ROWS_RECOVERED [0-9]+\/[0-9]+) .*/\1/'
```
```
=== DS-A checks for reading ruled(f)
SPAN_ARM_ROWS_RECOVERED 0/14
CHECK2 PASS all docs: was_shipped lines lost vs no_span=0 [] (need 0), gained vs no_span=0 [list]; lines no_span+nq dropped=21, of them top-level `version`=21, present in ruled(f)=21
=== DS-A checks for reading restricted_span+label
SPAN_ARM_ROWS_RECOVERED 14/14
CHECK2 PASS all docs: was_shipped lines lost vs no_span=0 [] (need 0), gained vs no_span=11 [list]; lines no_span+nq dropped=21, of them top-level `version`=21, present in restricted_span+label=21
=== DS-A checks for reading no_span+sep+label
SPAN_ARM_ROWS_RECOVERED 7/14
CHECK2 PASS all docs: was_shipped lines lost vs no_span=0 [] (need 0), gained vs no_span=35 [list]; lines no_span+nq dropped=21, of them top-level `version`=21, present in no_span+sep+label=21
```

⚠ **Check 2 says 21, and the ruling says *"restore all 22 `version` lines"*.** See correction 1 in §7.3. I scored check 2 on its binding clause, *"lost against the no_span reading must be 0"*. If you meant 22 literally, check 2 fails on one line, and rule 1 causes that loss, not the name rule.

**`H-12` — `DS-B`: the date source flips no replay result, and `H-2` still holds.**

```
python tools/checks/lib/doc_scanner_candidates.py replay --kinds kept --e2 legacy --dates instrument > /tmp/li.txt
python tools/checks/lib/doc_scanner_candidates.py replay --kinds kept --e2 legacy --dates utc > /tmp/lu.txt
python tools/checks/lib/doc_scanner_candidates.py replay --kinds kept --e2 strict --dates utc > /tmp/su.txt
diff /tmp/li.txt /tmp/lu.txt && echo "DS-B ALONE: WHOLE KEPT REPLAY IDENTICAL"
diff <(grep -E '^\s+(HIT|miss)' /tmp/li.txt) <(grep -E '^\s+(HIT|miss)' /tmp/su.txt) && echo "INSTRUMENT vs TOOL AS BUILT: REMOVED-LINE RESULTS IDENTICAL ($(grep -cE '^\s+(HIT|miss)' /tmp/su.txt) removed lines)"
python tools/checks/lib/doc_scanner_candidates.py replay --kinds all --e2 legacy > /tmp/a.txt && python tools/checks/measure/doc-scanner/replay_recall.py > /tmp/b.txt && diff /tmp/a.txt /tmp/b.txt && echo "H-2 STILL IDENTICAL ($(wc -l < /tmp/a.txt) lines)"
```
```
DS-B ALONE: WHOLE KEPT REPLAY IDENTICAL
INSTRUMENT vs TOOL AS BUILT: REMOVED-LINE RESULTS IDENTICAL (40 removed lines)
H-2 STILL IDENTICAL (52 lines)
```

- `replay --dates` defaults to `instrument`, so the published `H-2` command is unchanged.
- The tool's `scan` defaults to `--dates utc`.
- Only the ruled change: the instrument's `enumerators.py` is untouched (`H-5`).

**`H-13` — `DS-B` at `cbc2c91`: the same 12 headers, each one day younger.**

```
python tools/checks/lib/doc_scanner_candidates.py scan --rev cbc2c91 --out /tmp/u.json
python tools/checks/lib/doc_scanner_candidates.py scan --rev cbc2c91 --dates instrument --out /tmp/i.json
python -c "import json,sys;u=json.load(open(sys.argv[1],encoding='utf-8'));i=json.load(open(sys.argv[2],encoding='utf-8'));print('source',u['dated_state_date_source'],'utc',u['rev_date_utc'],'instrument',u['rev_date_instrument']);[print(a['id'],'|',a['detail'].split(',')[0],'<- instrument:',b['detail'].split(',')[0]) for a,b in zip(u['code_only']['DATED_STATE_OVER_HORIZON'],i['code_only']['DATED_STATE_OVER_HORIZON'])]" /tmp/u.json /tmp/i.json
```

The two `SCAN_OK` lines are identical to `H-3`'s and are omitted here. The third command prints:

```
source utc utc 2026-09-22 instrument 2026-09-23
DATED_STATE|CLAUDE.md|273@cbc2c91 | 2026-08-01 age 52d <- instrument: 2026-08-01 age 53d
DATED_STATE|docs/trader-tick-queue.md|5@cbc2c91 | 2026-08-07 age 46d <- instrument: 2026-08-07 age 47d
DATED_STATE|docs/trader-tick-queue.md|43@cbc2c91 | 2026-09-14 age 8d <- instrument: 2026-09-14 age 9d
DATED_STATE|docs/roadmap.md|31@cbc2c91 | 2026-08-24 age 29d <- instrument: 2026-08-24 age 30d
DATED_STATE|docs/roadmap.md|43@cbc2c91 | 2026-09-10 age 12d <- instrument: 2026-09-10 age 13d
DATED_STATE|docs/roadmap.md|45@cbc2c91 | 2026-09-10 age 12d <- instrument: 2026-09-10 age 13d
DATED_STATE|docs/roadmap.md|100@cbc2c91 | 2026-08-07 age 46d <- instrument: 2026-08-07 age 47d
DATED_STATE|docs/roadmap.md|190@cbc2c91 | 2026-08-12 age 41d <- instrument: 2026-08-12 age 42d
DATED_STATE|docs/roadmap.md|205@cbc2c91 | 2026-09-10 age 12d <- instrument: 2026-09-10 age 13d
DATED_STATE|docs/backlog-dependency-map.md|64@cbc2c91 | 2026-08-07 age 46d <- instrument: 2026-08-07 age 47d
DATED_STATE|docs/backlog-dependency-map.md|127@cbc2c91 | 2026-09-10 age 12d <- instrument: 2026-09-10 age 13d
DATED_STATE|docs/aws-collector-deploy-checklist.md|21@cbc2c91 | 2026-08-07 age 46d <- instrument: 2026-08-07 age 47d
```

How it is built: `e5_dated()` calls the instrument's own `e5()` with `E.rev_date` swapped, for that one call only, for a UTC-date function, and restores it in `finally`. It reuses the measured logic, not a copy of it.

### 7.2 Check 3 — the span arm's 14 rows, classified by MECHANISM only (no tense read)

Source: `H-11`'s `/tmp/all.txt`. Print it with `sed -n '/^CHECK3/,$p' /tmp/all.txt`, which shows each row with the text from the key to the number. "Correct" means the text gives that number as a value of that key.

| # | Line (all outside the living set) | Key | Number | Text from key to number | Mechanism |
|---|---|---|---|---|---|
| 1 | `a54a-drift-guard-batch-summary.md:57` | `indicators.CVD.slope_pct_of_value` | 0.01 | `slope_pct_of_value: poco=0.01` | ✅ correct: the POCO value of the key |
| 2 | `a54a-drift-guard-batch-summary.md:58` | `indicators.MicroCVD.accel_threshold_dynamic_pct` | 0.03 | `accel_threshold_dynamic_pct: poco=0.03` | ✅ correct: same shape |
| 3 | `absorption-engagement-derivation-2026-07-23.md:18` | `indicators.absorption.absorb_ratio` | 3.0 | `absorb_ratio ≥ 3.0` | ✅ correct: the threshold the key holds |
| 4 | `absorption-engagement-derivation-2026-07-23.md:72` | `indicators.absorption.max_pull_frac` | 0.5 | `max_pull_frac ∈ {0.5` | ✅ correct, first member of a value set only |
| 5 | `aws-redeploy-and-fee-knob-runbook-2026-07-31.md:101` | `version` | 63 | `"version": 63` | ✅ correct: a quoted JSON pair |
| 6 | `aws-redeploy-and-fee-knob-runbook-2026-07-31.md:110` | `version` | 63 | `"version": 63` | ✅ correct |
| 7 | `i17-sweep-batch-summary.md:184` | `indicators.CVD.slope_min_usd` | 1000 | `slope_min_usd {1000` | ✅ correct, first member of a sweep set |
| 8 | `i17-sweep-batch-summary.md:184` | `indicators.CVD.slope_pct_of_value` | 0.01 | `slope_pct_of_value {0.01` | ✅ correct, first member |
| 9 | `i17-sweep-batch-summary.md:186` | `indicators.MicroCVD.accel_threshold` | 5000 | `accel_threshold {5000` | ✅ correct, first member |
| 10 | `i17-sweep-batch-summary.md:186` | `indicators.MicroCVD.accel_threshold_dynamic_pct` | 0.03 | `accel_threshold_dynamic_pct {0.03` | ✅ correct, first member |
| 11 | `offline-whatif-replay-proposal.md:42` | `scoring.atr_stop_multiplier` | 1.0 | `atr_stop_multiplier"], "min": 1.0` | ❌ mis-pairing: 1.0 is the `min` bound of a ratio |
| 12 | `trade-store-downtime-repair-proposal.md:234` | `version` | 1 | `version"' -TotalCount 1` | ❌ mis-pairing: 1 is a `Select-String` argument |
| 13 | `trade-store-downtime-repair-spec-back.md:38` | `version` | 1 | `version"' -TotalCount 1` | ❌ mis-pairing: same |
| 14 | `trade-store-downtime-repair-spec-back.md:38` | `version` | 66 | `"version": 66` | ✅ correct: a quoted JSON pair |

- **11 correct, 3 mis-paired. 9 of the 11 lines hold at least one correct pairing.** Under the ruling's own branch, the arm must not be dropped.
- Rows 4 and 7–10 pair only the first member of a set. The other members are never paired under any reading.
- I read each row's key-to-number text and its line. I did not open the surrounding documents.

### 7.3 ⛔ Corrections to my own record

1. **`docs/doc-scanner-build-spec-back.md` §1 `H-6` says *"All 22 `was_shipped` lines that the name rule drops outside the living set are the top-level `version` key."* That is wrong as worded.** It was measured against `legacy`.
   - 21 are dropped by the name rule.
   - The 22nd, `absorption-d2-stage1-rotation-build-spec.md:166` (*"`Version` is `1`"*), is dropped by **rule 1** in every strict reading, because the legacy parser accepted the word `is`. `H-6` listed it separately under `no_span`.
   - By mechanism it is a **correct** pairing, a POCO default. **So rule 1 itself loses one correct pairing outside the living set.**
2. **Commit `0891089`'s message says the span arm's gains are *"13 rows"* with *"10 CORRECT"*.** They are **14 rows: 11 correct, 3 mis-paired** (§7.2).
3. **Commit `9462b35`'s message says *"28 of those are outside the span-arm set"*.** It is **30 lines**: 35 gained lines, minus the 5 span-arm lines among them. I subtracted 7 rows from 35 lines.

⭐ Two of these three errors are rows counted as lines, or lines as rows, which is the same shape both times. `H-6` and `H-11` print lines; `SPAN_ARM_ROWS_RECOVERED` prints rows. **Read the unit before quoting a number from either.**

### 7.4 Decision queued — `DS-A`, third round

Every reading gives the **same 22 judged `VALUE` rows on the living set at `cbc2c91`** (check 4 passes for all). **So this choice changes nothing about the seat's first measured run.** It changes only what the tool does on other docs and other revisions.

| Reading | Check 1 (≤ 2) | Check 2 | Span-arm rows kept (of 14; 11 correct) | New lines outside that set, unexamined |
|---|---|---|---|---|
| Literal, as built | ⛔ 17 | — | 14 (all 3 mis-pairings too) | 0 |
| (f) as ruled | ✅ 2 | ✅ | 0: loses all 11 correct | 0 |
| Restricted span arm (the ruling's fallback) | ⛔ 5 | ✅ | 14 (all 3 mis-pairings too) | 0 |
| No span arm, rule-1 separators plus `"` and `{` (**information only; not ruled; not adopted**) | ✅ 2 | ✅ | 7: 7 correct, 0 mis-pairings | **30** |

- **Read, labelled a hypothesis:** the last row looks the most promising. It meets check 1, keeps no mis-pairing, and recovers 7 of the 11 correct rows. **But I cannot recommend it:** its 30 extra lines have not been classified. Classifying them would be the same mechanism read as §7.2, and it is the missing half of the both-directions test.
- **The one reading that loses no correct row is the restricted span arm.** Its cost is 3 arithmetic lines in the code-only `never_shipped` bucket (`H-10`), plus 3 mis-paired rows outside the living set that would go to Jev as `VALUE` candidates.
- ⭐ **Under `CLAUDE.md`'s three-step test**, (f) now gives up information: 11 correct pairings for check-1 precision. So it is a trade, and the ruling already reserves it. The choice is between noise in a code-only bucket and lost correct rows. **That criterion is yours.**
- **Scoping:** whichever reading is ruled, the change is one `e2_strict()` argument set and one `pairing_variants.py` reading. Every reading is already implemented and measured, and the spec amendment and the PowerShell build do not depend on which is chosen.

### 7.5 What I did NOT verify (additions)

- **The 30 extra lines of the information-only eighth reading.** Unexamined, in both senses.
- **§7.2's classification** reads the line text only, not the surrounding documents.
- **Whether `E.rev_date`'s swap is safe under concurrency.** The tool is single-threaded, and the swap is restored in `finally`. I did not test a concurrent caller.
- **`DS-B` at a revision where a dated header sits exactly on the 7-day boundary.** No replay case and no header at `cbc2c91` is on it. The effect there is one day by construction, and it is not observed.
