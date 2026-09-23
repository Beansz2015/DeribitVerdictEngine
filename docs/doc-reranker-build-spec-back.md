# Doc re-ranker (harness 5) — build spec-back

Reports against [`doc-reranker-measurement-plan.md`](doc-reranker-measurement-plan.md). Question
set: [`harness-runs/doc-reranker-20260923T1930Z-queries.json`](harness-runs/doc-reranker-20260923T1930Z-queries.json).
Acceptance report: [`harness-runs/doc-reranker-acceptance-run-2026-09-23.md`](harness-runs/doc-reranker-acceptance-run-2026-09-23.md).
Follows [`batch-review-packet-convention.md`](batch-review-packet-convention.md).

Start commit: `42ff859`. Commits this build (all `[no-engine-change]`, tooling only):
`89d7511` (M2 measurement) → `488894f` (build) → `af5d6af` (three PS-5.1/BOM fixes found by
the first live acceptance run). Current HEAD `af5d6af`; confirmed via `git log` that no
commit landed from the concurrent seat during either acceptance run, so both runs read the
doc corpus at exactly `af5d6af` even though neither `-Query` nor `-AcceptanceRun` pins
`-Rev` by default.

---

## 1. Ranked verification handles

All `H-n` run from committed code. None are `E-n` (build-time-only evidence) — everything
below was executed straight from the tree after each commit that claims it.

1. **`H-1`, if you only run one.** Reproduce M2 exactly:
   ```
   python tools/checks/measure/doc-reranker/measure_shortlist.py 42ff859
   ```
   Actual output:
   ```
   REV=42ff859  DOC_SCOPE=453 docs  SECTIONS=6692  FOUND_QUERIES=26 (acceptance=7 reserved=19)

   bm25  k=10  TOTAL 17/26 = 65.4%   acceptance 5/7   reserved 12/19
   bm25  k=30  TOTAL 22/26 = 84.6%   acceptance 7/7   reserved 15/19
   bm25 k=30 MISSES: ['Q08', 'Q21', 'Q22', 'Q27']
   bm25 k=10 MISSES: ['Q04', 'Q08', 'Q14', 'Q16', 'Q17', 'Q21', 'Q22', 'Q25', 'Q27']

   grep  k=10  TOTAL 12/26 = 46.2%   acceptance 5/7   reserved 7/19
   grep  k=30  TOTAL 21/26 = 80.8%   acceptance 7/7   reserved 14/19
   grep k=30 MISSES: ['Q02', 'Q08', 'Q18', 'Q22', 'Q27']
   grep k=10 MISSES: ['Q01', 'Q02', 'Q07', 'Q08', 'Q09', 'Q11', 'Q16', 'Q17', 'Q18', 'Q19', 'Q20', 'Q21', 'Q22', 'Q27']

   BEST_METHOD=bm25  k=30 hit rate 22/26 = 84.6%  GO (>= 2/3)
   ```
   Load-bearing value: **BEST_METHOD=bm25, 22/26 at k=30** — the number the go/no-go and the
   method choice both hang on, not the headline "84.6%" alone (a reader should check the
   fraction, not just the rounded percentage).

2. **`H-2`.** Arithmetic identity on the tracked acceptance report — open
   [`harness-runs/doc-reranker-acceptance-run-2026-09-23.md`](harness-runs/doc-reranker-acceptance-run-2026-09-23.md)
   and check the "Totals" line against its own "Per query" table: count `True` in the
   `+Jev top10` column (7: `Q03 Q06 Q10 Q14 Q17 Q20 Q24`, all but `Q28`) against the printed
   "7/8 = 87.5%". A silent scoring bug (below) makes this check worth actually doing, not
   skipping.

3. **`H-3`.** `git log --oneline -3` from repo root shows `af5d6af → 488894f → 89d7511` in
   that order, confirming the build followed M2 → build → fix, never the reverse.

4. **`H-4` (re-runnable, not byte-exact).**
   ```
   set -a; . ./typesafe.local.env; set +a
   powershell -NoProfile -File tools/checks/doc-reranker.ps1 -Query "Where are the ATR low / normal / high bands defined?" -K 3
   ```
   Actual output when run (`REV=efa3290`, before the BOM fix but that code path is
   unaffected by it):
   ```
   NO_ANSWER_NOUL(any_answer)=noul=0.97 (yes, an answer exists)
   JEV_CALLS=4  USAGE_INPUT_TOKENS=3913  WALL_TIME_SEC=1.73
   TOP 3:
     1. noul=0.97 docs/trader-profile.md  ::  Trader Profile > 5. Risk Management Rules > ATR thresholds  [commit 2026-09-15]
     2. noul=0.97 docs/archive/doc-trim-2026-09-14b/originals/trader-profile.md [ARCHIVE]  ::  Trader Profile > 5. Risk Management Rules  [commit 2026-09-14]
     3. noul=0.8 docs/time-averaged-ofi-spec-back.md  ::  ... stale ATR bands in the loaded trader-profile  [commit 2026-07-02]
   ```
   Not byte-exact on re-run: `-Query` floats on `HEAD` by default, and a real Jev call is
   not deterministic (harness-shadow-mode-protocol.md §4b). What should reproduce: the
   correct section (`docs/trader-profile.md` § ATR thresholds) ranked first, its archived
   duplicate ranked second on the date tie-break, both with a high noul.

---

## 2. Decisions made under the auto-proceed ruling

Per `CLAUDE.md`'s auto-proceed ruling — each is a reversible, tooling-only, no-live-surface
call (the RESERVED classes there — `settings.json`, scoring, rendered values, the live
collector, schema/CSV — do not apply to this harness at all).

- **BM25 as the sole live shortlist method.** M2 measured it ahead of grep at both k=10 and
  k=30 (17/26 and 22/26 vs 12/26 and 21/26). Grep stays in `doc_sections.py` only as the
  measured comparison point, never as a second live path.
- **Section granularity: leaf sections at every heading level, not nested inclusion.** A
  section is the text between one heading (any level) and the next, fence-aware. This
  matches the ID grammar the plan specifies (`path#heading-chain@rev7`) and keeps the Jev
  state small; a nested-inclusion scheme would re-send a parent's whole subtree at every
  child heading.
- **Archive labelling by path substring (`'archive' in path.lower()`).** Cheap, and correct
  for every archive path in this repo (`docs/archive/...`, `*-archive.md`); ranked, never
  excluded, per the plan.
- **Jev state cap at 20,000 characters per section**, windowed around the start with a
  marked cut. Not specced numerically; chosen to stay well under the 32k-token limit even
  for the query plus a maximal section, after the doc-scanner measurement's ~2.29 B/token
  rate.
- **Date tie-break bucketed at 0.01 noul, not a global secondary sort key.** The brief's
  "where the top candidates are rulings, order newest first" reads as a tie-break among
  near-equal candidates, not a full override of the noul ranking; a global date-first sort
  would let an old, barely-relevant ruling outrank a highly relevant recent non-ruling
  section, which nothing in the plan asks for. No read that would have made this a
  cheaper-but-less-informative call was available — see §3.
- **No-answer Noul's top-5 pool is the RE-RANKED order, not the raw BM25 order.** The
  re-ranker's own final say is what the reader will act on, so it is what "does any of the
  top 5 answer it" should ask about.
- **Aggregate acceptance stats include `Q28` (the nowhere query) rather than excluding it.**
  The brief says "report top-1/5/10 for BM25 alone and BM25+Jev" over the 8 acceptance
  queries without carving `Q28` out, and it can never contribute a hit by construction
  (`expected: []`), so including it is a conservative (harder) denominator, not a
  self-serving one.
- **`Invoke-SectionNoul`'s "agreement" statistic (self-consistency across the 5 samples) is
  computed but not surfaced in the printed report or the acceptance `.md`.** The brief asks
  for sampling 5x for the acceptance measurement (done) and does not separately ask for a
  per-candidate agreement-rate report the way `doc-scanner.ps1` prints one per item; adding
  it would have meant either a much longer acceptance table (240 candidate-rows instead of
  8 query-rows) or a second, unspecified aggregate. Flagged in §4 as unverified, not
  silently dropped.

---

## 3. Spec-back proper — feedback on the plan and the brief

**What the brief got right, specifically.** The M2-before-build ordering caught nothing
(BM25 cleared the bar easily), but the explicit go/no-go escalation trigger and the
acceptance/reserved split were exactly the two guardrails that mattered once real Jev calls
started — the split is why the first (broken) acceptance run cost only wall-clock time and
API pennies instead of a spent reserved query.

**Where a live run exposed a real gap: reserved-run instructions never got exercised.** The
brief hands `-AcceptanceRun` to the implementer and the reserved subset to "the seat," but
never states how the seat is meant to invoke the same tool safely against reserved queries
(a `-Query` free-text call is unrestricted by design — nothing stops a seat from
accidentally typing one of the 21 reserved questions verbatim into `-Query` and burning it).
Recommend: a `-Reserved <id>` mode, gated to allow exactly one judged run per reserved-query
ID ever (a small tracked ledger), so the tool itself enforces "spent for good" rather than
relying on the seat's memory.

**Two bugs the spec's own process caught, worth recording as method, not just as fixes:**
the BOM bug was invisible to the tool's own printed output (it read as a clean 0% run, not
an error) precisely because `Out-Null` discarded the Python traceback that would have
revealed it. The fix (§ commit `af5d6af`) both patches the immediate cause and adds a
$LASTEXITCODE check specifically so a *future* scoring failure cannot again present as a
quiet, wrong zero. This is the same class CLAUDE.md's handle rules describe — a check that
passed when the underlying property was false.

**Where the spec was silent and I had to choose:** M4's "sample 5 times per pair" doesn't
say whether the no-answer Noul over the top 5 is sampled 5× as well in acceptance mode. I
sampled it once, matching the live-mode default, since it's one call per query rather than
one per (query, candidate) pair and the brief's sampling instruction is stated in exactly
those terms ("per pair"). Flagged, not asserted as obviously right.

---

## 4. What I did not verify, and cannot

- **Jev's dollar cost for this run.** `USAGE_INPUT_TOKENS=1129513` over 1191 calls is
  measured directly from the API's own `usage.input_tokens`. The **price** is not: the
  cookbook's $0.042/1M-input figure is dated "as of 2026-08" for `jev-1.12`; this tool calls
  `jev-latest`, whose current price I did not look up. Carried over, not verified.
- **Whether re-running `-AcceptanceRun` reproduces the exact 3/8, 4/8, 5/8 (BM25) and
  3/8, 5/8, 7/8 (+Jev) figures.** Jev is not deterministic per call
  (harness-shadow-mode-protocol.md §4b) and this run sampled 5× per candidate specifically
  to blunt that, but the acceptance report does not print each candidate's own agreement
  rate (§2), so I cannot say from this run alone how close to the boundary any one of the 8
  queries' hits sat.
- **Whether BM25's heading-chain double-weighting is well-tuned**, versus some other weight.
  It was chosen ad hoc to make headings matter without formally tuning the weight, and one
  M2 miss (`Q08`) traced to it inflating an unrelated candidate whose headings repeat the
  query's most common term ("local") — noted live during M2 debugging, not chased further
  since M2 cleared its bar regardless.
- **The trial itself (measurement plan section 4).** This build makes the tool ready to
  log; it has not yet been exercised across "at least 5 sessions" of real engine work, which
  is the actual acceptance criterion for the harness closing per
  harness-shadow-mode-protocol.md §6a (armed and in trial, not validated).
- **Whether a live `-Query` run's `SAMPLED_ONCE` single draw is representative** the way the
  5-sample acceptance mean is. By design (build brief: "live use may sample once"), so a
  live top-5 list can occasionally differ from what a 5-sample run would have produced;
  the printed `SAMPLED_ONCE=true` line is what makes this legible to the reader rather than
  silent.
