"""Read-only Charge PNG checks against the approved original main commit; requires Pillow."""
import argparse
import io
from pathlib import Path
import subprocess
from PIL import Image

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--baseline', default='f13bb6bd25f469e38acce05eceb712712dc30c3f')
a = p.parse_args()
repo = Path(__file__).resolve().parents[1]
path = 'Assets/Resources/ArakusV1/Charge.png'

def original(name):
    return subprocess.check_output(['git', 'show', a.baseline + ':' + name], cwd=repo)

before = Image.open(io.BytesIO(original(path))).convert('RGBA')
after = Image.open(repo / path).convert('RGBA')
assert before.size == after.size == (960, 192)
for frame in range(5):
    old = before.crop((frame*192, 0, (frame+1)*192, 192))
    new = after.crop((frame*192, 0, (frame+1)*192, 192))
    b, c = old.getchannel('A').getbbox(), new.getchannel('A').getbbox()
    assert b[3] == c[3] == 160, 'Foot baseline moved'
    assert 0 < c[0] < c[2] < 192 and c[1] > 0, 'Clipped equipment/body'
    assert c[2]-c[0] == round((b[2]-b[0])*1.25)
    assert c[3]-c[1] == round((b[3]-b[1])*1.25)
    assert set(new.getdata()) <= set(old.getdata()) | {(0, 0, 0, 0)}, 'Interpolated/new palette colors'
    # Exact reproducibility: proportional source crop enlargement, no independent gear stretch.
    expected = old.crop(b).resize((c[2]-c[0], c[3]-c[1]), Image.Resampling.NEAREST)
    assert new.crop(c).tobytes() == expected.tobytes()
    print('PASS frame', frame, 'opaque WH', (b[2]-b[0], b[3]-b[1]), '->', (c[2]-c[0], c[3]-c[1]))
for unchanged in [path+'.meta', 'Assets/Resources/ArakusV1/Groggy.png',
                  'Assets/Resources/ArakusV1/Groggy.png.meta', 'Assets/Scripts/ActorView.cs']:
    assert (repo / unchanged).read_bytes() == original(unchanged), unchanged + ' changed'
print('ARAKUS_CHARGE_ART_CHECKS_PASSED: 5 frames, exact nearest-neighbor source pixels, baseline, metadata and excluded files')
