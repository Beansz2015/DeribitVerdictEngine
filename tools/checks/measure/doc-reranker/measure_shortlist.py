"""tools/checks/measure/doc-reranker/measure_shortlist.py -- harness 5, step M2.

CODE ONLY. No Jev call anywhere in this file (measurement plan section 2, M2).
Measures the shortlist hit rate for the expected section, at k=10 and k=30, for
two methods (BM25 and grep-on-distinctive-terms, both in
tools/checks/lib/doc_sections.py), over the 26 "found"-kind queries in the
question set (docs/harness-runs/doc-reranker-<stamp>-queries.json). This is
allowed for ALL queries including the reserved subset -- M2 is code-only, no Jev
output is produced or read, so section 4a's acceptance/reserved split (which
gates JEV output, not code counts) does not apply here.

MATCHING RULE: shared with M4's acceptance scoring, stated ONCE in
tools/checks/lib/doc_reranker_matching.py's module docstring (see
`hit_for_entry` there) -- so M2's 84.6% and M4's acceptance hit rates describe
the same question. Not used anywhere in the live re-ranker build -- the live
tool never sees "expected" answers, only Jev's own judgement.
"""
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
LIB = os.path.normpath(os.path.join(HERE, '..', '..', 'lib'))
sys.path.insert(0, LIB)
import doc_sections as DS  # noqa: E402
from doc_reranker_matching import query_hits_at_k  # noqa: E402

REPO = DS.REPO
QUERY_FILE = os.path.join(REPO, 'docs', 'harness-runs', 'doc-reranker-20260923T1930Z-queries.json')


def query_hits(expected, shortlist_ids, sections_by_id):
    ranked = [sections_by_id[sid] for sid in shortlist_ids]
    return query_hits_at_k(expected, ranked, len(ranked))


def main():
    rev = sys.argv[1] if len(sys.argv) > 1 else 'HEAD'
    rev = DS.rev_full(rev)
    rev7 = rev[:7]
    with open(QUERY_FILE, encoding='utf-8') as f:
        qset = json.load(f)
    queries = [q for q in qset['queries'] if q['kind'] == 'found']
    assert len(queries) == 26, f'expected 26 found-kind queries, got {len(queries)}'

    sections = DS.flat_sections(rev)
    sections_by_id = {s['id']: s for s in sections}
    bm25 = DS.BM25Index(sections)

    K30 = 30
    results = {'bm25': {}, 'grep': {}}
    misses = {'bm25': {10: [], 30: []}, 'grep': {10: [], 30: []}}
    per_subset = {'bm25': {'acceptance': {10: 0, 30: 0}, 'reserved': {10: 0, 30: 0}},
                  'grep': {'acceptance': {10: 0, 30: 0}, 'reserved': {10: 0, 30: 0}}}
    subset_n = {'acceptance': 0, 'reserved': 0}

    for q in queries:
        subset_n[q['subset']] += 1
        bm25_top30 = [s['id'] for s, _ in bm25.rank(q['query'], K30)]
        grep_top30 = [s['id'] for s, _ in DS.grep_rank(sections, q['query'], K30)]
        for method, top30 in (('bm25', bm25_top30), ('grep', grep_top30)):
            for k in (10, 30):
                hit = query_hits(q['expected'], top30[:k], sections_by_id)
                if hit:
                    per_subset[method][q['subset']][k] += 1
                else:
                    misses[method][k].append(q['id'])

    print(f'REV={rev7}  DOC_SCOPE={len(DS.doc_scope(rev))} docs  SECTIONS={len(sections)}  '
          f'FOUND_QUERIES=26 (acceptance={subset_n["acceptance"]} reserved={subset_n["reserved"]})')
    print()
    for method in ('bm25', 'grep'):
        for k in (10, 30):
            acc = per_subset[method]['acceptance'][k]
            res = per_subset[method]['reserved'][k]
            tot = acc + res
            print(f'{method:5s} k={k:2d}  TOTAL {tot}/26 = {tot/26:.1%}   '
                  f'acceptance {acc}/{subset_n["acceptance"]}   reserved {res}/{subset_n["reserved"]}')
        print(f'{method} k=30 MISSES: {misses[method][30]}')
        print(f'{method} k=10 MISSES: {misses[method][10]}')
        print()

    # go/no-go per the brief's escalation trigger: fewer than 2/3 of found-type
    # queries hit at k=30, for the BEST method.
    best = max(('bm25', 'grep'), key=lambda m: per_subset[m]['acceptance'][30] + per_subset[m]['reserved'][30])
    best_hits30 = per_subset[best]['acceptance'][30] + per_subset[best]['reserved'][30]
    frac = best_hits30 / 26
    print(f'BEST_METHOD={best}  k=30 hit rate {best_hits30}/26 = {frac:.1%}  '
          f'{"GO (>= 2/3)" if frac >= (2/3) else "NO-GO (< 2/3)"}')


if __name__ == '__main__':
    main()
