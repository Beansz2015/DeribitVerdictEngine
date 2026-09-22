"""Replay the doc-scanner enumerators at each known fix commit's PARENT revision and report which of
the lines the fix removed they flag (recall on labelled historical positives).
Record: docs/doc-scanner-measurement-2026-09-22.md."""
import sys, re, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from enumerators import *

def removed_lines(fix, path):
    diff = git('show', fix, '--unified=0', '--format=', '--', path)
    out, cur = [], None
    for l in diff.splitlines():
        m = re.match(r'@@ -(\d+)(?:,(\d+))? ', l)
        if m:
            cur = int(m.group(1)); continue
        if l.startswith('-') and not l.startswith('---'):
            out.append((cur, l[1:])); cur += 1
    return out

cases = [
    ('5c7c178', ['CLAUDE.md', 'docs/DeribitIndicatorProject.md', 'docs/architecture.md', 'docs/roadmap.md', 'docs/trader-tick-queue.md']),
    ('9c447e1', ['docs/architecture.md', 'docs/DeribitIndicatorProject.md']),
    ('5b8515e', ['docs/DeribitIndicatorProject.md']),
    ('2f46679', ['CLAUDE.md']),
]
for fix, paths in cases:
    pre = fix + '^'
    res = run_all(pre, paths)
    flagged = {(p, i) for (p, i, *_ ) in res}
    tot_rm = hit_rm = 0
    print(f'=== fix {fix} (replay at {pre})')
    for p in paths:
        rm = removed_lines(fix, p)
        # only count removed lines that are real content (skip blank / table-rule lines)
        rm = [(n, t) for n, t in rm if len(t.strip()) > 12 and not re.fullmatch(r'[|\-: ]+', t.strip())]
        h = [(n, t) for n, t in rm if (p, n) in flagged]
        tot_rm += len(rm); hit_rm += len(h)
        for n, t in rm:
            kinds = sorted({k for (pp, i, k, *_ ) in res if pp == p and i == n})
            print(f'  {"HIT " if kinds else "miss"} {p}:{n} {",".join(kinds):28s} {t.strip()[:110]}')
    other = [r for r in res if (r[0], r[1]) not in {(p, n) for p in paths for n, _ in removed_lines(fix, p)}]
    print(f'  removed content lines: {tot_rm}, flagged: {hit_rm}; candidates on NON-removed lines: {len(other)}')
    from collections import Counter
    print('  by kind (non-removed):', dict(Counter(r[2] for r in other)))
