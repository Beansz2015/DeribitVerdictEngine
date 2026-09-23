"""Harness 6 (decision-bias tripwire), Phase A: build the population, outcomes and unruled files.

    python tools/checks/measure/decision-bias/build_population.py            # check only
    python tools/checks/measure/decision-bias/build_population.py --write <UTC-stamp>

Reads manifest.py (the state spans) and manifest_outcomes.py (the rulings). Every text field is pulled VERBATIM from git; a span that is not found raises.
Makes NO Jev call and NO network call.

Writes (with --write), all under docs/harness-runs/:
  decision-bias-<stamp>-population.json   the STATE only: question, options, recommended, rationale
  decision-bias-<stamp>-outcomes.json     the trader's rulings. The seat labels WITHOUT opening this
  decision-bias-<stamp>-unruled.json      recommendations with no trader ruling, count-reported only
  decision-bias-<stamp>-excluded.json     every other candidate, with a reason code (no text, no outcome)

The leak check (always run) scans every population text field for ruling markers and for any
date later than the recommendation's own date. A hit must be explained in manifest.LEAK_NOTES
or it counts as UNEXPLAINED, and --write refuses while any hit is unexplained.
"""
import collections, json, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import dbias_lib as L          # noqa: E402
import manifest as M           # noqa: E402
import manifest_outcomes as MO  # noqa: E402

OUT_DIR = os.path.join(L.REPO, 'docs', 'harness-runs')

# Ruling markers, as the Phase A brief names them. Case-insensitive except where a lower-case
# form would only ever be an ordinary English word ('adopted' is kept case-insensitive anyway).
LEAK_PATTERNS = [
    ('RULED', re.compile(r'RULED')),
    ('ruled', re.compile(r'\bruled\b')),
    ('ticked', re.compile(r'(?i)\bticked\b|\bTICK(?:ED)?\b')),
    ('trader', re.compile(r'(?i)trader')),
    ('overrul', re.compile(r'(?i)overrul')),
    ('DEFEAT', re.compile(r'(?i)defeat')),
    ('check-mark', re.compile('✅')),
    ('as recommended', re.compile(r'(?i)as recommended')),
    ('adopted', re.compile(r'(?i)adopted')),
]
DATE_RE = re.compile(r'\b(20\d\d)-(\d\d)-(\d\d)\b')


def _id(e):
    return '%s|%s@%s' % (e['doc'], e['label'], L.full_sha(M.REV)[:7])


def _resolve_many(spans, rev, path):
    if spans and isinstance(spans[0], str):
        spans = [spans]
    texts, locs = [], []
    for sp in spans:
        t, loc = L.resolve(sp, rev, path)
        texts.append(t)
        locs.append(loc)
    return '\n'.join(texts), locs


def build_record(e):
    rev, path = e['src'], e['src_path']
    q, qloc = L.resolve(e['q'], rev, path)
    if e['opts'][0] == 'parse':
        otext, oloc = L.resolve(e['opts'][1], rev, path)
        options = L.parse_options(otext, oloc)
    else:
        options = []
        for lab, sp in e['opts']:
            t, loc = L.resolve(sp, rev, path)
            options.append({'label': lab, 'text': t, 'src': loc})
    if len(options) < 2:
        raise L.SpanError('%s: fewer than two options' % _id(e))
    labels = [o['label'] for o in options]
    if len(set(labels)) != len(labels):
        raise L.SpanError('%s: duplicate option labels %r' % (_id(e), labels))
    if e['rec'] not in labels:
        raise L.SpanError('%s: recommended %r is not an option label %r' % (_id(e), e['rec'], labels))
    rat, rlocs = _resolve_many(e['rat'], rev, path)
    when = L.commit_utc(rev)
    return {
        'id': _id(e),
        'question': q,
        'question_src': qloc,
        'options': options,
        'recommended': e['rec'],
        'rationale': rat,
        'rationale_src': rlocs,
        'state_rev': L.full_sha(rev),
        'state_rev_committed_utc': when.strftime('%Y-%m-%dT%H:%M:%SZ'),
    }


def build_outcome(e, rid):
    o = MO.OUT.get((e['doc'], e['label']))
    if o is None:
        raise L.SpanError('%s: population entry without an outcome' % rid)
    label, ruled, spans, date, gran, note = o
    if label not in ('ADOPTED', 'OVERRULED', 'PARTIAL'):
        raise L.SpanError('%s: bad outcome label %r' % (rid, label))
    if label == 'ADOPTED' and ruled != e['rec']:
        raise L.SpanError('%s: ADOPTED but ruled %r != recommended %r' % (rid, ruled, e['rec']))
    texts, locs = [], []
    for path, sp in spans:
        t, loc = L.resolve(sp, M.REV, path)
        texts.append(t)
        locs.append(loc)
    if not DATE_RE.fullmatch(date):
        raise L.SpanError('%s: ruling date %r is not YYYY-MM-DD' % (rid, date))
    return {'id': rid, 'outcome': label, 'ruled': ruled, 'recommended': e['rec'],
            'ruling_text': texts, 'ruling_src': locs, 'ruling_date_utc': date,
            'ruling_granularity': gran, 'note': note}


def leak_scan(rec):
    rec_date = rec['state_rev_committed_utc'][:10]
    fields = [('question', rec['question']), ('rationale', rec['rationale'])]
    fields += [('option%s' % o['label'], o['text']) for o in rec['options']]
    hits = []
    for fname, text in fields:
        for name, rx in LEAK_PATTERNS:
            for m in rx.finditer(text):
                hits.append((fname, name, text[max(0, m.start() - 60):m.end() + 60].replace('\n', ' ')))
        for m in DATE_RE.finditer(text):
            if m.group(0) > rec_date:
                hits.append((fname, 'date>' + rec_date, text[max(0, m.start() - 60):m.end() + 60].replace('\n', ' ')))
    return hits


def main(argv):
    write = '--write' in argv
    stamp = argv[argv.index('--write') + 1] if write else None
    show_hits = '--show-hits' in argv
    rev_full = L.full_sha(M.REV)
    pop, outs, unruled, errors = [], [], [], []
    seen = set()
    for e in M.ENTRIES:
        rid = _id(e)
        if rid in seen:
            errors.append('duplicate id %s' % rid)
            continue
        seen.add(rid)
        try:
            if e['status'] == 'pop':
                r = build_record(e)
                pop.append(r)
                outs.append(build_outcome(e, rid))
            elif e['status'] == 'unruled':
                r = build_record(e)
                unruled.append({'id': rid, 'reason': e['reason'], 'question': r['question'],
                                'question_src': r['question_src'], 'note': e['note']})
            else:
                errors.append('%s: unknown status %r' % (rid, e['status']))
        except (L.SpanError, RuntimeError) as ex:
            errors.append('%s: %s' % (rid, ex))
    for doc, label, note in M.UNRECOVERABLE:
        rid = '%s|%s@%s' % (doc, label, rev_full[:7])
        if rid in seen:
            errors.append('duplicate id %s (unrecoverable)' % rid)
        seen.add(rid)
    excluded = []
    for doc, label, reason, note in M.EXCLUDED:
        rid = '%s|%s@%s' % (doc, label, rev_full[:7])
        if rid in seen:
            errors.append('duplicate id %s (excluded)' % rid)
        seen.add(rid)
        excluded.append({'id': rid, 'reason': reason, 'note': note})
    for doc, label, note in M.UNRECOVERABLE:
        excluded.append({'id': '%s|%s@%s' % (doc, label, rev_full[:7]), 'reason': 'rationale_unrecoverable', 'note': note})

    # ---- leak check ------------------------------------------------------------------------
    notes = getattr(M, 'LEAK_NOTES', {})
    total_hits, unexplained = 0, []
    used_notes = set()
    for r in pop:
        for fname, name, ctx in leak_scan(r):
            total_hits += 1
            key = (r['id'], fname, name)
            if key in notes:
                used_notes.add(key)
            else:
                unexplained.append((r['id'], fname, name, ctx))
    stale_notes = [k for k in notes if k not in used_notes]
    pop_keys = {(e['doc'], e['label']) for e in M.ENTRIES if e['status'] == 'pop'}
    for k in MO.OUT:
        if k not in pop_keys:
            errors.append('outcome with no population entry: %r' % (k,))

    cnt = collections.Counter(o['outcome'] for o in outs)
    print('REV=%s' % rev_full)
    print('POPULATION=%d  ADOPTED=%d  OVERRULED=%d  PARTIAL=%d' % (len(pop), cnt['ADOPTED'], cnt['OVERRULED'], cnt['PARTIAL']))
    print('UNRULED=%d  RATIONALE_UNRECOVERABLE=%d  EXCLUDED_OTHER=%d' % (len(unruled), len(M.UNRECOVERABLE), len(M.EXCLUDED)))
    cand = len(pop) + len(M.UNRECOVERABLE)
    print('UNRECOVERABLE_SHARE=%.1f%% of %d candidates meeting all three criteria' % (100.0 * len(M.UNRECOVERABLE) / max(cand, 1), cand))
    exc = collections.Counter(x['reason'] for x in excluded)
    print('EXCLUDED_BY_REASON=' + ', '.join('%s:%d' % kv for kv in sorted(exc.items())))
    print('LEAK_HITS=%d  EXPLAINED=%d  UNEXPLAINED=%d  STALE_NOTES=%d' % (total_hits, total_hits - len(unexplained), len(unexplained), len(stale_notes)))
    if show_hits:
        for u in unexplained:
            print('  UNEXPLAINED %s [%s] %s :: %s' % u)
        for k in stale_notes:
            print('  STALE_NOTE %r' % (k,))
    print('ERRORS=%d' % len(errors))
    for er in errors:
        print('  ERROR ' + er)
    if not write:
        return 1 if errors else 0
    if errors or unexplained:
        print('REFUSED: --write needs ERRORS=0 and UNEXPLAINED=0')
        return 2
    meta = {'rev': rev_full, 'rev7': rev_full[:7], 'stamp_utc': stamp,
            'builder': 'tools/checks/measure/decision-bias/build_population.py'}
    files = {
        'population': dict(meta, note='STATE ONLY. No ruling is in this file. Label from this file without opening outcomes.json.', items=pop),
        'outcomes': dict(meta, note='The trader rulings. Do NOT open before the seat baseline is written and committed.', items=outs),
        'unruled': dict(meta, note='Recommendations with no trader ruling. Excluded from scoring; count reported only.', items=unruled),
        'excluded': dict(meta, note='Every other candidate, with a reason code. No text and no outcome.', items=excluded),
    }
    for k, v in files.items():
        p = os.path.join(OUT_DIR, 'decision-bias-%s-%s.json' % (stamp, k))
        with open(p, 'w', encoding='utf-8', newline='\n') as fh:
            json.dump(v, fh, ensure_ascii=False, indent=1)
            fh.write('\n')
        print('WROTE %s (%d items)' % (os.path.relpath(p, L.REPO).replace('\\', '/'), len(v['items'])))
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
