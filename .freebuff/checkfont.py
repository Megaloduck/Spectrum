import struct, sys

def names(path):
    d = open(path, 'rb').read()
    num = struct.unpack('>H', d[4:6])[0]
    off = None
    for i in range(num):
        rec = 12 + i * 16
        tag = d[rec:rec+4].decode('ascii', 'replace')
        o = struct.unpack('>I', d[rec+8:rec+12])[0]
        if tag == 'name':
            off = o
    if off is None:
        print(path, 'no name table')
        return
    fmt, cnt, stroff = struct.unpack('>HHH', d[off:off+6])
    print(f'== {path} (records={cnt})')
    for i in range(cnt):
        r = off + 6 + i * 12
        plat, enc, lang, nid, length, o = struct.unpack('>HHHHHH', d[r:r+12])
        if nid not in (1, 2, 4, 6, 16, 17):
            continue
        s = d[off + stroff + o: off + stroff + o + length]
        try:
            txt = s.decode('utf-16-be') if plat == 3 else s.decode('mac_roman')
        except Exception:
            txt = repr(s)
        print(f'  id={nid} plat={plat} lang={lang:#06x} -> {txt!r}')

for p in sys.argv[1:]:
    names(p)
