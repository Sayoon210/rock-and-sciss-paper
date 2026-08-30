# -*- coding: utf-8 -*-
"""Per-TYPE reference extraction for the RockAndScissPaper source.

Each declaration's brace-matched body is scanned on its own, so an enum declared beside a
class does not inherit that class's references.
"""
import io, os, re, json, glob, collections

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
os.chdir(REPO)

files = []
for pat in ('Scripts/**/*.cs', 'GameLogic/**/*.cs'):
    for f in glob.glob(pat, recursive=True):
        if 'obj' + os.sep in f or 'Deprecated' in f:
            continue
        files.append(f.replace('\\', '/'))
files.sort()

DECL = re.compile(
    r'^[ \t]*(?:public|internal|private)\s+(?:sealed\s+|static\s+|abstract\s+|partial\s+|readonly\s+)*'
    r'(class|interface|enum|struct)\s+(\w+)\s*(?::\s*([^\n{]+?))?\s*$', re.M)


def blank_out(src):
    """Strings BEFORE comments.

    Doing comments first eats the tail of any line holding a "res://..." path: the // inside
    the literal reads as a comment start, the closing quote goes with it, and every quote
    after that pairs up shifted by one. That silently swallowed braces and cut class bodies
    short - MatchWorldView came out with 2 references instead of a dozen.
    """
    src = re.sub(r'@"(?:[^"]|"")*"', '""', src)
    src = re.sub(r'"(?:\\.|[^"\\\n])*"', '""', src)
    src = re.sub(r"'(?:\\.|[^'\\])'", "' '", src)
    src = re.sub(r'/\*.*?\*/', ' ', src, flags=re.S)
    src = re.sub(r'//[^\n]*', ' ', src)
    return src


types = {}
for f in files:
    raw = io.open(f, encoding='utf-8').read()
    clean = blank_out(raw)
    for m in DECL.finditer(clean):
        kind, name, base = m.group(1), m.group(2), (m.group(3) or '').strip()
        brace = clean.find('{', m.end())
        if brace < 0:
            continue
        depth, i = 0, brace
        while i < len(clean):
            if clean[i] == '{':
                depth += 1
            elif clean[i] == '}':
                depth -= 1
                if depth == 0:
                    break
            i += 1
        types[name] = {
            'file': f,
            'folder': os.path.dirname(f).replace('\\', '/'),
            'kind': kind,
            'base': base,
            'body': clean[brace:i],
            'lines': raw.count('\n') + 1,
        }

OUT = os.path.join('Tools', 'TypeGraph', 'graph.json')

names = set(types)
edges = collections.defaultdict(set)
for name, info in types.items():
    for tok in set(re.findall(r'\b[A-Z]\w+\b', info['body'])):
        if tok in names and tok != name:
            edges[name].add(tok)

fan_in = collections.Counter()
for src, targets in edges.items():
    for t in targets:
        fan_in[t] += 1

out = {n: {'file': i['file'], 'folder': i['folder'], 'kind': i['kind'],
           'base': i['base'], 'lines': i['lines'],
           'fanIn': fan_in[n], 'refs': sorted(edges[n])}
       for n, i in sorted(types.items())}
io.open(OUT, 'w', encoding='utf-8').write(json.dumps(out, indent=1, ensure_ascii=False))

print('wrote ' + OUT)
print('types: %d   edges: %d' % (len(types), sum(len(v) for v in edges.values())))
print('\nfan-in top 12')
for t, c in fan_in.most_common(12):
    print('   %-28s %d' % (t, c))
print('\nspot-check')
for t in ('MatchWorldView', 'GameState', 'ESide', 'ECardName', 'RoundResolver'):
    if t in types:
        print('   %-16s refs=%d  %s' % (t, len(edges[t]), sorted(edges[t])[:10]))
