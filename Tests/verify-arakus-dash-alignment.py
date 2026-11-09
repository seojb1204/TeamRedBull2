"""Read-only check that Arakus dash frames keep the body on the simulated position; requires Pillow and NumPy.

Each sprite frame's pivot x must place the hip (body anchor) at the same offset from the pivot as the
Idle frames do. Before this fix the Charge run loop (frames 2<->3) drew the body 36 to 44px apart, so the
boss visibly jumped back and forth about 1m every 0.1s while the simulation moved smoothly.
Only pivot x may change; every other byte of the .meta files must match the baseline (main).
"""
import argparse
from pathlib import Path
import re
import subprocess

import numpy as np
from PIL import Image

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--baseline', default='f13bb6bd25f469e38acce05eceb712712dc30c3f')
a = p.parse_args()
repo = Path(__file__).resolve().parents[1]
art = 'Assets/Resources/ArakusV1/'
CELL = 192
TOLERANCE_PX = 1.0
# Frames shown while the boss is moving or about to move. Other ShieldThrowing frames are standing poses.
ALIGNED = {'Charge': [0, 1, 2, 3, 4], 'ShieldThrowing_DashAttack': [2, 3]}
# Two frames that alternate while the boss is moving (ArakusAnimationSelection.Sample).
RUN_LOOP = {'Charge': (2, 3), 'ShieldThrowing_DashAttack': (2, 3)}
EXPECTED_PIVOT_PX = {'Charge': {0: 105, 1: 124, 2: 83, 3: 127, 4: 98}, 'ShieldThrowing_DashAttack': {3: 122}}
PIVOT = re.compile(r'      name: (\S+)\n(?:(?!      name: ).*\n)*?      pivot: \{x: ([0-9.]+), y: ([0-9.]+)\}')
PIVOT_X = re.compile(r'(      pivot: \{x: )[0-9.]+(, y: )')


def original(name):
    return subprocess.check_output(['git', 'show', a.baseline + ':' + name], cwd=repo).decode('utf-8')


def hips(png):
    """Mean x of non-plume body pixels in the 55-75% height band of each frame (pelvis area)."""
    im = np.array(Image.open(png).convert('RGBA')).astype(int)
    result = []
    for i in range(im.shape[1] // CELL):
        f = im[:, i*CELL:(i+1)*CELL]
        opaque = f[..., 3] > 8
        plume = (f[..., 0] > 140) & (f[..., 1] < 80) & (f[..., 2] < 80)
        body = opaque & ~plume
        ys = np.nonzero(opaque)[0]
        top, bottom = ys.min(), ys.max()
        band = body.copy()
        band[:int(top + (bottom-top)*.55)] = False
        band[int(top + (bottom-top)*.75):] = False
        result.append(float(np.nonzero(band)[1].mean()))
    return result


def pivots(meta_text):
    return {name: (float(x), float(y)) for name, x, y in PIVOT.findall(meta_text)}


idle_offset = float(np.mean(hips(repo / (art + 'Idle.png')))) - CELL / 2
idle_pivots = pivots((repo / (art + 'Idle.png.meta')).read_text('utf-8'))
assert all(abs(x - .5) < 1e-6 for x, _ in idle_pivots.values()), 'Idle reference pivot changed'
print(f'Idle hip offset from pivot: {idle_offset:+.2f}px')

for name, frames in ALIGNED.items():
    meta_path = art + name + '.png.meta'
    meta = (repo / meta_path).read_text('utf-8')
    base = original(meta_path)
    assert PIVOT_X.sub(r'\1*\2', meta) == PIVOT_X.sub(r'\1*\2', base), meta_path + ' changed beyond pivot x'
    now, before = pivots(meta), pivots(base)
    h = hips(repo / (art + name + '.png'))
    for k in range(len(h)):
        sprite = f'{name}_{k:02d}'
        x, y = now[sprite]
        px = x * CELL
        assert abs(px - round(px)) < 1e-3, sprite + ' pivot is not on a whole pixel'
        assert y == before[sprite][1], sprite + ' pivot y (foot line) changed'
        expected = EXPECTED_PIVOT_PX[name].get(k)
        if expected is None:
            assert x == before[sprite][0], sprite + ' pivot changed but frame is not in the aligned set'
            continue
        assert round(px) == expected, f'{sprite} pivot {px:.1f}px, expected {expected}px'
        offset = h[k] - px
        old = h[k] - before[sprite][0] * CELL
        print(f'PASS {sprite}: hip offset {old:+.1f}px -> {offset:+.1f}px')
        if k in frames:
            assert abs(offset - idle_offset) <= TOLERANCE_PX, f'{sprite} body {offset - idle_offset:+.1f}px off the Idle anchor'
    if name in RUN_LOOP:
        loop = RUN_LOOP[name]
        jitter_before = abs((h[loop[0]] - before[f'{name}_{loop[0]:02d}'][0]*CELL) - (h[loop[1]] - before[f'{name}_{loop[1]:02d}'][0]*CELL))
        jitter_after = abs((h[loop[0]] - now[f'{name}_{loop[0]:02d}'][0]*CELL) - (h[loop[1]] - now[f'{name}_{loop[1]:02d}'][0]*CELL))
        print(f'{name} frames {loop[0]}<->{loop[1]} body jump: {jitter_before:.1f}px -> {jitter_after:.1f}px')
        assert jitter_after <= 2 * TOLERANCE_PX

print('ARAKUS_DASH_ALIGNMENT_CHECKS_PASSED: Charge 0-4 and ShieldThrowing_DashAttack 2-3 hip-aligned; only pivot x changed')
