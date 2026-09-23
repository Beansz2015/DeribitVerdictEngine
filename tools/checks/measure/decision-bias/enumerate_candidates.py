"""Harness 6 (decision-bias tripwire), Phase A: the RECALL ledger for the hand enumeration.

    python tools/checks/measure/decision-bias/enumerate_candidates.py [--list]

Code-only. Lists every CANDIDATE doc at REV by two mechanical rules, then says, for each one,
whether the manifest records a decision from it (population, unrecoverable, unruled or excluded)
or which DOC_TRIAGE disposition covers it. A candidate doc with neither is UNTRIAGED, and a
non-zero UNTRIAGED count means the enumeration has a known hole.

Candidate rules (tracked docs under docs/, *.md, excluding docs/archive/ byte-copies):
  R1  the doc holds at least one recommendation marker AND at least one ruling marker
  R2  the doc holds a markdown table whose header names a decision-shaped column
The rules are deliberately broad: they over-select, and triage records why a doc holds nothing.
"""
import collections, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dbias_lib as L          # noqa: E402
import manifest as M           # noqa: E402

REC = re.compile(r'[Mm]y read|MY READ|[Rr]ecommend|RECOMMEND|\b[Rr]ec\b|Default Answer|orchestrator read|[Mm]y pick|[Mm]y lean')
RUL = re.compile(r'RULED|TICKED|[Tt]icked|[Rr]uled|APPROVED|[Ss]ign-off|DEFEATED|[Oo]verrul|trader-directed')
HDR = re.compile(r'(?i)decision|question|option|my read|recommend|\brec\b|ruling|\bread\b|pick|choice')
SEP = re.compile(r'^\s*\|[\s:\-|]+\|\s*$')


def candidates(rev):
    files = [f for f in L.git('ls-tree', '-r', '--name-only', L.full_sha(rev), 'docs').splitlines()
             if f.endswith('.md') and not f.startswith('docs/archive/')]
    out = {}
    for f in files:
        lines = L.file_lines(rev, f)[1:]
        text = '\n'.join(lines)
        rules = []
        if REC.search(text) and RUL.search(text):
            rules.append('R1')
        for i in range(len(lines) - 1):
            if lines[i].lstrip().startswith('|') and SEP.match(lines[i + 1]) and HDR.search(lines[i]):
                rules.append('R2')
                break
        if rules:
            out[f] = rules
    return out


def main(argv):
    cands = candidates(M.REV)
    recorded = collections.Counter()
    for e in M.ENTRIES:
        recorded[e['doc']] += 1
    for coll in (M.EXCLUDED, M.UNRECOVERABLE, getattr(M, 'UNRULED', [])):
        for row in coll:
            recorded[row[0]] += 1
    triage = getattr(M, 'DOC_TRIAGE', {})
    with_entries, triaged, untriaged = [], collections.Counter(), []
    for f in sorted(cands):
        if recorded[f]:
            with_entries.append(f)
        elif f in triage:
            triaged[triage[f][0]] += 1
        else:
            untriaged.append(f)
    stale = [f for f in triage if f not in cands]
    print('REV=%s' % L.full_sha(M.REV))
    print('CANDIDATE_DOCS=%d  (R1=%d  R2=%d)' % (len(cands), sum('R1' in r for r in cands.values()), sum('R2' in r for r in cands.values())))
    print('WITH_MANIFEST_ENTRIES=%d' % len(with_entries))
    print('TRIAGED_NO_ENTRY=%d  ' % sum(triaged.values()) + ', '.join('%s:%d' % kv for kv in sorted(triaged.items())))
    print('UNTRIAGED=%d' % len(untriaged))
    print('MANIFEST_DOCS_OUTSIDE_CANDIDATES=%d' % len([d for d in recorded if d not in cands]))
    print('STALE_TRIAGE=%d' % len(stale))
    if '--list' in argv:
        for f in untriaged:
            print('  UNTRIAGED %s %s' % (f, ','.join(cands[f])))
        for d in sorted(d for d in recorded if d not in cands):
            print('  OUTSIDE %s' % d)
        for f in stale:
            print('  STALE_TRIAGE %s' % f)
    return 1 if untriaged else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
