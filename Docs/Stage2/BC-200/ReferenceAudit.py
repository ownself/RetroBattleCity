"""Independent PNG-only cell audit; run from project root with Python 3 (stdlib only).
No Unity asset or scene is written. Unknown colors/shapes fail, rather than becoming Empty.
"""
import collections
import csv
import hashlib
import json
from pathlib import Path
import struct
import zlib

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
SOURCE = ROOT / 'Assets/Sprites/NES - Battle City (JPN) - Backgrounds - Stage 01.png'
data = SOURCE.read_bytes()
assert data[:8] == b'\x89PNG\r\n\x1a\n'
packed = b''
offset = 8
while offset < len(data):
    length = struct.unpack('>I', data[offset:offset+4])[0]
    tag, payload = data[offset+4:offset+8], data[offset+8:offset+8+length]
    offset += length + 12
    if tag == b'IHDR':
        width, height, depth, color, compression, filtering, interlace = struct.unpack('>IIBBBBB', payload)
        assert (width, height, depth, color, compression, filtering, interlace) == (208, 208, 8, 6, 0, 0, 0)
    if tag == b'IDAT':
        packed += payload
raw = zlib.decompress(packed)
stride, bpp = width * 4, 4
previous = [0] * stride
pixels = []
for y in range(height):
    kind = raw[y*(stride+1)]
    line = list(raw[y*(stride+1)+1:(y+1)*(stride+1)])
    for i in range(stride):
        a, b, c = (line[i-bpp] if i >= bpp else 0), previous[i], (previous[i-bpp] if i >= bpp else 0)
        p = a + b - c
        pa, pb, pc = abs(p-a), abs(p-b), abs(p-c)
        paeth = a if pa <= pb and pa <= pc else b if pb <= pc else c
        line[i] = (line[i] + [0, a, b, (a+b)//2, paeth][kind]) % 256
    pixels.append([tuple(line[i:i+4]) for i in range(0, stride, 4)])
    previous = line

black, gray, brown, dark, light, white = [(r,g,b,255) for r,g,b in
    [(0,0,0), (102,102,102), (153,79,0), (108,7,0), (174,174,174), (254,254,254)]]
brick = collections.Counter({brown:24, dark:18, gray:22})
steel = collections.Counter({light:26, white:16, gray:22})
rows, cells = [], []
for sy in range(26):
    row = ''
    for x in range(26):
        patch = [pixels[sy*8+dy][x*8+dx] for dy in range(8) for dx in range(8)]
        histogram = collections.Counter(patch)
        if histogram == brick: cell = 'B'
        elif histogram == steel: cell = 'S'
        elif histogram == {black:64}: cell = '.'
        elif x in (12,13) and sy in (24,25):
            assert set(histogram) <= {black, gray, dark}
            cell = 'H'
        else: raise ValueError(f'Unclassified PNG cell ({x},{sy}): {histogram}')
        row += cell
        cells.append([x, sy, x, 25-sy, x*8, sy*8, cell,
            hashlib.sha256(bytes(channel for p in patch for channel in p)).hexdigest()])
    rows.append(row)
counts = dict(collections.Counter(''.join(rows)))
assert counts == {'.':444, 'B':220, 'S':8, 'H':4}, counts
# Compare the independent pixel classification against, not derive it from, the authoring matrix.
matrix = (ROOT/'Assets/BattleCity/Editor/Stage01Layout.txt').read_text().splitlines()
assert rows == matrix, 'PNG vs authoring matrix mismatch'
with (OUT/'ReferenceCells.csv').open('w', newline='', encoding='utf-8') as f:
    writer = csv.writer(f)
    writer.writerow(['sourceCellX','sourceCellYDown','runtimeCellX','runtimeCellYUp','sourcePixelX','sourcePixelYDown','kind','rgba8PatchSha256'])
    writer.writerows(cells)
report = {'source':str(SOURCE.relative_to(ROOT)), 'sha256':hashlib.sha256(data).hexdigest(),
    'dimensions':[width,height], 'counts8px':counts, 'baseSourceRect':[96,192,16,16],
    'baseRuntimeCenter':[104,8], 'matrixMatchesIndependentSamples':True,
    'rowsTopDown':rows, 'paletteRGBA': [list(p) for p in (black,gray,brown,dark,light,white)]}
(OUT/'ReferenceAudit.json').write_text(json.dumps(report, indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k != 'rowsTopDown'},indent=2))
