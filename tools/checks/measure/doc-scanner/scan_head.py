"""Run every doc-scanner enumerator over HEAD's non-archive docs; split counts by the living set.
Writes all-candidates.json and living-candidates.json to $OUT_DIR (default: current directory).
Record: docs/doc-scanner-measurement-2026-09-22.md."""
import sys, re, os, json
from collections import Counter, defaultdict
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from enumerators import *

OUT = os.environ.get('OUT_DIR', '.')
# REV pins the scan to a commit without touching the checkout (the recorded run used cbc2c91).
rev = git('rev-parse', os.environ.get('REV', 'HEAD')).strip()
allmd = sorted(f for f in tree_files(rev) if f.endswith('.md') and (f.startswith('docs/') or f == 'CLAUDE.md'))
ARCH = re.compile(r'(archive|harness-runs/|/archive/)', re.I)
scan = [f for f in allmd if not ARCH.search(f)]
print('HEAD', rev[:7])
print('md files tracked (docs + CLAUDE.md):', len(allmd), ' scanned (archives excluded):', len(scan))
res = run_all(rev, scan)
by = Counter(r[2] for r in res)
files_by = defaultdict(set)
for r in res: files_by[r[2]].add(r[0])
for k, v in sorted(by.items()):
    print(f'{k:28s} {v:6d} lines in {len(files_by[k]):4d} files')
print('distinct flagged lines (any kind):', len({(r[0], r[1]) for r in res}))
print('fixture-ID mentions that resolve to Program.vb (the A56b-shaped population):',
      sum(e4_mentions(show(rev, p), rev) for p in scan))
json.dump([list(r) for r in res], open(os.path.join(OUT, 'all-candidates.json'), 'w', encoding='utf-8'))

LIVING = {'CLAUDE.md', 'docs/DeribitIndicatorProject.md', 'docs/architecture.md', 'docs/trader-profile.md',
          'docs/trader-tick-queue.md', 'docs/roadmap.md', 'docs/backlog-dependency-map.md', 'docs/csv-rotation-riders.md',
          'docs/harness-shadow-mode-protocol.md', 'docs/UserManual.md', 'docs/aws-collector-deploy-checklist.md',
          'docs/seat-handover-2026-09-22b.md'}
print('--- split: living set (%d docs) vs the rest' % len(LIVING))
for k in sorted(by):
    liv = sum(1 for r in res if r[2] == k and r[0] in LIVING)
    print(f'{k:28s} living {liv:5d}   rest {by[k]-liv:6d}')
print('resolved fixture-ID mentions in the living set:', sum(e4_mentions(show(rev, p), rev) for p in LIVING))
json.dump([list(r) for r in res if r[0] in LIVING], open(os.path.join(OUT, 'living-candidates.json'), 'w', encoding='utf-8'))
