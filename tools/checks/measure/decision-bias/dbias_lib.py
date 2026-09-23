"""Harness 6 (decision-bias tripwire), Phase A: shared helpers for the population builder.

Code only. No Jev call and no network. Every text field in a record is a VERBATIM span read
with `git show <rev>:<path>` - never `git checkout`. A span that is not found verbatim at its
stated line raises, so a manifest typo fails loudly instead of emitting paraphrase.
"""
import datetime, os, re, subprocess
from functools import lru_cache

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', '..'))


def git(*args):
    r = subprocess.run(['git', '-C', REPO, *args], capture_output=True)
    if r.returncode != 0:
        raise RuntimeError('git %s failed: %s' % (' '.join(args), r.stderr.decode('utf-8', 'replace')))
    return r.stdout.decode('utf-8')


@lru_cache(None)
def full_sha(rev):
    return git('rev-parse', '--verify', rev + '^{commit}').strip()


@lru_cache(None)
def file_lines(rev, path):
    """1-based access: file_lines(rev, path)[n] is line n. Index 0 is a placeholder."""
    txt = git('show', '%s:%s' % (full_sha(rev), path))
    return [''] + txt.split('\n')


@lru_cache(None)
def commit_utc(rev):
    """Committer timestamp of `rev` in UTC. The workstation is GMT+8; the repo's dates are UTC."""
    iso = git('show', '-s', '--format=%cI', full_sha(rev)).strip()
    dt = datetime.datetime.fromisoformat(iso)
    return dt.astimezone(datetime.timezone.utc)


# Markdown does NOT protect a pipe inside backticks, so the renderer splits there too. Only an
# escaped \| is kept inside a cell. This split mirrors what a reader of the rendered table sees.
_CELL_SPLIT = re.compile(r'(?<!\\)\|')


def split_cells(row):
    s = row.strip()
    if s.startswith('|'):
        s = s[1:]
    if s.endswith('|') and not s.endswith('\\|'):
        s = s[:-1]
    return [c.strip() for c in _CELL_SPLIT.split(s)]


class SpanError(Exception):
    pass


def _loc(path, line, rev):
    return '%s:%d@%s' % (path, line, full_sha(rev)[:7])


def resolve(span, rev, path):
    """Resolve one span spec to (verbatim_text, location_string).

    Span forms (line numbers are at `rev`):
      ('cell', line, idx)                       whole table cell idx (1-based)
      ('cellsub', line, idx, start, end)        substring of that cell
      ('sub', line, start, end)                 substring of the whole line
      ('line', line)                            whole line, stripped
      ('lines', a, b)                           lines a..b inclusive, joined with newlines
      ('at', rev2, path2, <span>)               the inner span, read at another revision/path
      ('commitmsg', sha, start, end)            a substring of a commit MESSAGE (newlines -> spaces)
    start: the substring begins at the first occurrence of `start` (None = line/cell start).
    end:   the substring ends after the first occurrence of `end` at or after `start`
           (None = to the end of the line/cell).
    """
    kind = span[0]
    if kind == 'at':
        return resolve(span[3], span[1], span[2])
    if kind == 'commitmsg':
        # ('commitmsg', sha, start, end): a ruling recorded only in a commit message.
        msg = git('show', '-s', '--format=%B', full_sha(span[1]))
        loc = 'commit-message:%s' % full_sha(span[1])[:7]
        return _substr(' '.join(msg.split()), span[2], span[3], loc), loc
    L = file_lines(rev, path)
    if kind in ('cell', 'cellsub', 'sub', 'line'):
        n = span[1]
        if n < 1 or n >= len(L):
            raise SpanError('line %d out of range in %s@%s' % (n, path, rev))
        base = L[n]
        if kind in ('cell', 'cellsub'):
            cells = split_cells(base)
            idx = span[2]
            if idx < 1 or idx > len(cells):
                raise SpanError('cell %d out of range (%d cells) at %s' % (idx, len(cells), _loc(path, n, rev)))
            base = cells[idx - 1]
            if kind == 'cell':
                return base, _loc(path, n, rev)
            start, end = span[3], span[4]
        elif kind == 'line':
            return base.strip(), _loc(path, n, rev)
        else:
            start, end = span[2], span[3]
        return _substr(base, start, end, _loc(path, n, rev)), _loc(path, n, rev)
    if kind == 'lines':
        a, b = span[1], span[2]
        if a < 1 or b >= len(L) or b < a:
            raise SpanError('lines %d-%d out of range in %s@%s' % (a, b, path, rev))
        return '\n'.join(L[a:b + 1]).strip(), '%s:%d-%d@%s' % (path, a, b, full_sha(rev)[:7])
    raise SpanError('unknown span kind %r' % (kind,))


def _substr(base, start, end, loc):
    i = 0
    if start is not None:
        i = base.find(start)
        if i < 0:
            raise SpanError('start %r not found verbatim at %s' % (start, loc))
    if end is None:
        j = len(base)
    else:
        j = base.find(end, i)
        if j < 0:
            raise SpanError('end %r not found verbatim after start at %s' % (end, loc))
        j += len(end)
    out = base[i:j].strip()
    if not out:
        raise SpanError('empty span at %s' % loc)
    return out


# Option markers: (a) (b) ... (i) (ii) ... optionally bold and/or starred. Anchored to the
# option CELL or SPAN the manifest names, so a "(b)" inside a rationale is never read as an option.
_OPT = re.compile(r'(?:⭐\s*)?(?:\*\*\s*)?\((?P<lab>[a-z]|[ivx]{1,4}|\d)\)(?:\s*\*\*)?')
_TRIM_TAIL = re.compile(r'[\s·;,/]+(?:or)?[\s·;,/]*$')


def parse_options(text, loc):
    """Split an options span on its (a)/(b)/... markers. Each option text is a verbatim
    contiguous substring of `text` (separators trimmed from its ends only)."""
    ms = list(_OPT.finditer(text))
    out = []
    for k, m in enumerate(ms):
        a = m.end()
        b = ms[k + 1].start() if k + 1 < len(ms) else len(text)
        seg = text[a:b]
        seg = _TRIM_TAIL.sub('', seg).strip()
        seg = re.sub(r'^[\s:—\-–]+', '', seg).strip()
        if not seg:
            raise SpanError('empty option text for (%s) at %s' % (m.group('lab'), loc))
        if seg not in text:
            raise SpanError('option text not verbatim at %s' % loc)
        out.append({'label': '(%s)' % m.group('lab'), 'text': seg, 'src': loc})
    return out
