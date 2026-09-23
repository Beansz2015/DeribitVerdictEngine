"""tools/checks/lib/doc_reranker_shortlist.py -- harness 5 (doc re-ranker) CLI.

Thin CLI wrapper around tools/checks/lib/doc_sections.py's BM25 index, for
tools/checks/doc-reranker.ps1 to call. Owns CODE facts only (the shortlist and
each candidate's commit date / archive label); it never calls Jev and never
sees a query's "expected" answer -- that belongs to the M2 measurement script
(tools/checks/measure/doc-reranker/measure_shortlist.py) only.

M2 (docs/doc-reranker-measurement-plan.md section 2) measured BM25 as the
better of the two shortlist methods at both k=10 and k=30 (bm25 22/26 vs grep
21/26 at k=30; bm25 17/26 vs grep 12/26 at k=10) -- see
docs/doc-reranker-build-spec-back.md section "M2". BM25 is therefore the only
method this CLI, and the live tool, use; the grep method stays in
doc_sections.py as the measured comparison point, not as a second live path.

Usage:
  python doc_reranker_shortlist.py shortlist --rev REV --query TEXT --k 30 --out FILE
  python doc_reranker_shortlist.py score --ranked-json FILE --expected-json FILE --out FILE
      `score` computes hit@1/5/10 for ONE query, given an already-ORDERED list
      of section dicts (a shortlist, or that shortlist after PS1's Jev
      re-rank -- this subcommand does not care which). It calls the SAME
      hit-matching rule as M2 (tools/checks/lib/doc_reranker_matching.py), so
      "BM25 alone" and "BM25 plus Jev" in the acceptance report answer the
      same question, just over two different orderings of the one shortlist.
"""
import argparse
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import doc_sections as DS  # noqa: E402
from doc_reranker_matching import query_hits_at_k  # noqa: E402


def main(argv):
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    s = sub.add_parser('shortlist')
    s.add_argument('--rev', default='HEAD')
    s.add_argument('--query', required=True)
    s.add_argument('--k', type=int, default=30)
    s.add_argument('--out', required=True)

    sc = sub.add_parser('score')
    sc.add_argument('--ranked-json', required=True)
    sc.add_argument('--expected-json', required=True)
    sc.add_argument('--out', required=True)

    a = ap.parse_args(argv)
    if a.cmd == 'score':
        # utf-8-sig, not utf-8: PowerShell 5.1's `Set-Content -Encoding UTF8` (the writer of
        # both these temp files, from tools/checks/doc-reranker.ps1) always emits a UTF-8 BOM
        # -- unlike pwsh 7's UTF8NoBOM, PS 5.1 has no BOM-less UTF-8 option. Plain 'utf-8'
        # chokes on the BOM (JSONDecodeError). Reproduced live: the first -AcceptanceRun
        # (1191 real Jev calls, 1.1M input tokens, ~26 min) silently scored 0/8 on every
        # column because every `score` subprocess call crashed on this before it could read
        # the file -- the crash went to stderr, which the PS1 caller piped to Out-Null.
        with open(a.ranked_json, encoding='utf-8-sig') as f:
            ranked = json.load(f)
        with open(a.expected_json, encoding='utf-8-sig') as f:
            expected = json.load(f)
        result = {k: query_hits_at_k(expected, ranked, k) for k in (1, 5, 10)}
        with open(a.out, 'w', encoding='utf-8', newline='\n') as f:
            json.dump(result, f)
        print(f"SCORE_OK hit@1={result[1]} hit@5={result[5]} hit@10={result[10]}")
        return

    rev = DS.rev_full(a.rev)
    rev7 = rev[:7]
    sections = DS.flat_sections(rev)
    bm25 = DS.BM25Index(sections)
    ranked = bm25.rank(a.query, a.k)

    out = []
    for s_, score in ranked:
        out.append({
            'id': s_['id'], 'path': s_['path'], 'heading_chain': s_['heading_chain'],
            'heading_text': s_['heading_text'], 'text': s_['text'],
            'is_archive': s_['is_archive'], 'truncated': s_['truncated'],
            'start_line': s_['start_line'], 'end_line': s_['end_line'],
            'bm25_score': score,
            'commit_date_epoch': DS.commit_date_epoch(rev, s_['path']),
        })
    doc = {'rev': rev, 'rev7': rev7, 'query': a.query, 'k': a.k,
           'doc_scope_count': len(DS.doc_scope(rev)), 'section_count': len(sections),
           'shortlist': out}
    with open(a.out, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(doc, f, ensure_ascii=False, indent=1)
    print(f'SHORTLIST_OK rev={rev7} k={a.k} candidates={len(out)}')


if __name__ == '__main__':
    main(sys.argv[1:])
