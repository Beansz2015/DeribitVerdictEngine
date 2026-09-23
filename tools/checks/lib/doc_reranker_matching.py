"""tools/checks/lib/doc_reranker_matching.py -- harness 5's ONE hit-matching rule.

Shared by tools/checks/measure/doc-reranker/measure_shortlist.py (M2, BM25 vs
grep, all 26 found-kind queries, no Jev) and
tools/checks/lib/doc_reranker_shortlist.py's `score` subcommand (M4 acceptance,
BM25-alone vs BM25+Jev, the 8 acceptance queries only). One implementation, so
"was the expected section found in this ranked list" means the same thing in
both measurements -- a divergent copy would let M2's 84.6% and M4's acceptance
numbers describe two different questions without anyone noticing.

MATCHING RULE (stated once, here): a query's "expected" entry is {path,
section}. A candidate section (a dict carrying at least path, heading_chain,
text, start_line, end_line) is a HIT for that entry when:
  1. its `path` equals the expected path, AND
  2. one of:
     a. the expected `section` text contains "whole" (e.g. "(whole
        proposal)") -- ANY section of that doc present in the ranked list
        counts, because the query's answer is the whole doc, not one heading;
     b. the expected `section` text contains "line NNN" -- the candidate
        section whose [start_line, end_line] covers NNN counts;
     c. otherwise: a leading section-number token in the expected text
        ("§5", "row D7", "table row: ...") must appear as a whole word in the
        candidate's normalised heading_chain, AND at least one descriptive
        word (length > 3) from the remainder must appear in the normalised
        heading_chain OR the candidate's own body text -- a heading can be
        terse; the body often repeats the identifying phrase (a table row
        name, a decision label). Without a leading number token, a heading-only
        word-overlap fallback applies (see `hit_for_entry`).
This is a LOOSE, textual rule by design (measurement plan section 2: "match
the section loosely, by the heading text or the section number"), never a
byte-exact one.
"""
import re

NUMTOK_RX = re.compile(r'^\s*(?:§\s*([0-9]+[a-z]?)|row\s+([A-Za-z0-9\-]+)|table row:?\s*(.+?)(?:$|\s{2,}))', re.I)


def norm(t):
    return re.sub(r'[^a-z0-9 ]+', ' ', t.lower())


def desc_words(s):
    return [w for w in norm(s).split() if len(w) > 3]


def hit_for_entry(entry, sections_by_path):
    """sections_by_path: {path: [section_dict, ...]} restricted to whatever
    pool (shortlist / ranked-order slice) the caller wants tested."""
    path = entry['path']
    sec = entry['section']
    cands = sections_by_path.get(path, [])
    if not cands:
        return False, None
    low = sec.lower()
    if 'whole' in low:
        return True, cands[0]['id']
    m = re.search(r'line\s+(\d+)', low)
    if m:
        n = int(m.group(1))
        for s in cands:
            if s['start_line'] <= n <= s['end_line']:
                return True, s['id']
        return False, None
    m = NUMTOK_RX.match(sec)
    numtok = None
    rest = sec
    if m:
        numtok = next((g for g in m.groups() if g), None)
        rest = sec[m.end():]
    words = desc_words(rest) if rest.strip() else desc_words(sec)
    for s in cands:
        chain_n = norm(s['heading_chain'])
        body_n = norm(s['text'][:4000])
        tok_ok = True
        if numtok:
            tok_ok = bool(re.search(r'(?<![a-z0-9])' + re.escape(numtok.lower()) + r'(?![a-z0-9])', chain_n))
        if not tok_ok:
            continue
        word_ok = any(w in chain_n or w in body_n for w in words) if words else True
        if tok_ok and word_ok:
            return True, s['id']
    if not numtok:
        for s in cands:
            chain_n = norm(s['heading_chain'])
            if words and sum(1 for w in words if w in chain_n) >= max(1, len(words) // 3):
                return True, s['id']
    return False, None


def query_hits_at_k(expected, ranked_sections, k):
    """ranked_sections: an ORDERED list of section dicts (already the ranking
    to test). Returns True if any expected entry hits within the first k."""
    pool = ranked_sections[:k]
    by_path = {}
    for s in pool:
        by_path.setdefault(s['path'], []).append(s)
    for entry in expected:
        ok, _ = hit_for_entry(entry, by_path)
        if ok:
            return True
    return False
