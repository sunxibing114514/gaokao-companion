#!/usr/bin/env python3
"""生成应用图标 Assets/app.ico(经典 BMP 条目,兼容 Roslyn /win32icon)。"""
import math
import os
import struct

SIZES = [16, 32, 48, 256]
BG = (103, 80, 164)   # M3 primary #6750A4
FG = (255, 255, 255)


def rounded_rect_sdf(x, y, hw, hh, r):
    dx = abs(x) - (hw - r)
    dy = abs(y) - (hh - r)
    if dx > 0 and dy > 0:
        return math.hypot(dx, dy) - r
    return max(dx, dy)


def render(size, ss):
    n = size * ss
    acc = [[[0, 0, 0, 0] for _ in range(size)] for _ in range(size)]
    half = size / 2.0
    for sy in range(n):
        y = (sy + 0.5) / ss - half
        for sx in range(n):
            x = (sx + 0.5) / ss - half
            if rounded_rect_sdf(x, y, half - 0.5, half - 0.5, size * 0.22) > 0:
                continue
            d = math.hypot(x, y)
            ring = abs(d - size * 0.30) <= size * 0.048
            hand_v = abs(x) <= size * 0.032 and (-size * 0.27 <= y <= -size * 0.03)
            hand_h = (size * 0.03 <= x <= size * 0.24) and abs(y) <= size * 0.032
            dot = d <= size * 0.045
            color = FG if (ring or hand_v or hand_h or dot) else BG
            cell = acc[sy // ss][sx // ss]
            cell[0] += color[0]
            cell[1] += color[1]
            cell[2] += color[2]
            cell[3] += 255
    rows = []
    k = ss * ss
    for row in acc:
        out = bytearray()
        for c in row:
            out += bytes([round(c[0] / k), round(c[1] / k), round(c[2] / k), round(c[3] / k)])
        rows.append(bytes(out))
    return rows


def bmp_entry(size, rows):
    header = struct.pack('<IiiHHIIiiII', 40, size, size * 2, 1, 32, 0, size * size * 4, 0, 0, 0, 0)
    pixel = bytearray()
    for row in reversed(rows):
        line = bytearray()
        for i in range(0, len(row), 4):
            r, g, b, a = row[i], row[i + 1], row[i + 2], row[i + 3]
            line += bytes([b, g, r, a])
        pixel += line
    mask_row = ((size + 31) // 32) * 4
    pixel += bytes(mask_row * size)
    return header + bytes(pixel)


def build_ico():
    images = []
    for s in SIZES:
        rows = render(s, 4 if s <= 48 else 2)
        images.append((s, bmp_entry(s, rows)))
    header = struct.pack('<HHH', 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b''
    data = b''
    for s, blob in images:
        entries += struct.pack('<BBBBHHII', s % 256, s % 256, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        data += blob
    return header + entries + data


out_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                       '..', 'src', 'GaokaoCompanion', 'Assets')
os.makedirs(out_dir, exist_ok=True)
out_path = os.path.join(out_dir, 'app.ico')
with open(out_path, 'wb') as f:
    f.write(build_ico())
print('written:', os.path.abspath(out_path))
