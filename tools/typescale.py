"""KEMAS type scale. snap() maps any size onto the scale; check() reports sizes outside it.
Scale: 12.5 caption, 14 body, 15 card title / nav, 17 section, 20 panel heading, 26 page title, 36 metric.
Sizes of 42 and above are display numerals (score, results) and are allowed. Icon glyphs are exempt."""
import re, sys
SCALE = [12.5, 14, 15, 17, 20, 26, 36]
def snap(v):
    if v <= 13: return 12.5
    if v <= 14.5: return 14
    if v <= 16: return 15
    if v <= 18.5: return 17
    if v <= 23: return 20
    if v <= 30: return 26
    if v <= 40: return 36
    return v
def fmt(v): return ('%g' % v)
ICON = re.compile(r'Segoe|Glyph\(|StaticResource Icon|Content="&#x|"WinBtn"|"IconBtn"|EngLogo')
def ok(v): return v in SCALE or v >= 42
def run(path, fix):
    bad = []
    src = open(path, encoding='utf-8').read().split('\n')
    out = []
    for n, line in enumerate(src, 1):
        if ICON.search(line): out.append(line); continue
        def sub_xaml(m):
            v = float(m.group(2))
            if ok(v): return m.group(0)
            bad.append((n, v))
            return m.group(1) + fmt(snap(v)) + m.group(3)
        line2 = re.sub(r'(FontSize="|Property="FontSize" Value=")([0-9.]+)(")', sub_xaml, line)
        def sub_cs(m):
            v = float(m.group(2))
            if ok(v): return m.group(0)
            bad.append((n, v))
            return m.group(1) + fmt(snap(v)) + m.group(3)
        line2 = re.sub(r'(FontSize = |FontSizeProperty, )([0-9]+(?:\.[0-9]+)?)(\b[^.0-9]|$)', sub_cs, line2)
        out.append(line2)
    if fix: open(path, 'w', encoding='utf-8').write('\n'.join(out))
    return bad
if __name__ == '__main__':
    fix = sys.argv[1] == 'fix'
    total = 0
    for p in sys.argv[2:]:
        b = run(p, fix)
        total += len(b)
        if not fix:
            for n, v in b: print('%s:%d font size %g is not on the type scale' % (p, n, v))
    if fix: print('normalised', total, 'sizes')
    sys.exit(1 if (total and not fix) else 0)
