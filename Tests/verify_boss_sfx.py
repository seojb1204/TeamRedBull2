"""Boss combat SFX: files, wiring and coverage checks. Not a substitute for listening in Unity.

1. Every file named in Soundscape.BuildSfxMap exists as 44.1 kHz mono 16-bit PCM with a .meta.
2. Every pattern of every boss gets a wind-up (tell_) sound and a strike sound
   (resolved the same way Soundscape.Resolve does).
3. Every literal PlaySound/sound.Play key used for the new events resolves to a file.
4. The generator is deterministic: rendering again gives byte-identical files.
"""
from pathlib import Path
import hashlib, re, subprocess, sys, tempfile, wave

root = Path(__file__).resolve().parents[1]
audio = root / 'Assets/StreamingAssets/Audio'
scape = (root / 'Assets/Scripts/Soundscape.cs').read_text(encoding='utf-8')
patterns_src = (root / 'Assets/Scripts/BossPatternSystem.cs').read_text(encoding='utf-8')
tower = (root / 'Assets/Scripts/TowerGame.cs').read_text(encoding='utf-8')
data = (root / 'Assets/Scripts/GameData.cs').read_text(encoding='utf-8')

# --- the map, parsed from source ---
sfx_map = {}
for key, files in re.findall(r'Map\("([^"]+)",[^,]+,[^,]+,((?:"[^"]+",?)+)\)', scape):
    sfx_map[key] = re.findall(r'"([^"]+)"', files)
for key in re.findall(r'sfxMap\["([^"]+)"\]=Silent', scape):
    sfx_map[key] = []
assert len(sfx_map) > 60, len(sfx_map)

def resolve(key):
    k = key
    while True:
        if k in sfx_map:
            return sfx_map[k]
        cut = k.rfind('_')
        if cut <= 0:
            return None
        k = k[:cut]
        if k.startswith('boss_'):
            parts = k.split('_')
            if len(parts) == 3 and 'boss_' + parts[2] in sfx_map:
                return sfx_map['boss_' + parts[2]]

# 1. files
used = sorted({f for files in sfx_map.values() for f in files})
for name in used:
    p = audio / f'sfx_{name}.wav'
    assert p.exists(), p
    assert Path(str(p) + '.meta').exists(), p
    with wave.open(str(p)) as w:
        assert (w.getnchannels(), w.getsampwidth(), w.getframerate()) == (1, 2, 44100), p
        frames = w.readframes(w.getnframes())
        assert 0.2 < w.getnframes() / 44100 < 8, p
    peak = max(abs(int.from_bytes(frames[i:i + 2], 'little', signed=True)) for i in range(0, len(frames), 2))
    assert 20000 < peak <= 28200, (p, peak)          # about -1.5 dBFS, never clipped
on_disk = {p.stem[4:] for p in audio.glob('sfx_*.wav')}
assert on_disk == set(used), ('unused or missing files', on_disk ^ set(used))
guids = [re.search(r'guid: (\w+)', (audio / f'sfx_{n}.wav.meta').read_text()).group(1) for n in used]
assert len(set(guids)) == len(guids)
print(f'PASS files: {len(used)} sfx, 44.1 kHz mono 16-bit, peak below clipping, unique GUIDs')

# 2. coverage: every pattern of every boss
catalog = data[data.index('public static readonly BossSpec[] Bosses'):]
catalog = catalog[:catalog.index('};')]
bosses = [(chunk.split('"', 1)[0], chunk) for chunk in catalog.split('new BossSpec("')[1:]]
assert [b for b, _ in bosses] == ['arakus', 'phanes', 'adicus', 'aethelos', 'kharos'], [b for b, _ in bosses]
arakus_special = {'Step', 'ShieldThrow', 'ShieldRush', 'Sweep', 'Rage'}   # struck inside TickArakus, not ExecutePattern
for index, (boss, block) in enumerate(bosses):
    for pid, kind in re.findall(r'P\("(\w+)","[^"]+",AttackKind\.(\w+)', block):
        tell = resolve(f'tell_{index}_{pid}')
        assert tell, f'{boss}.{pid} has no wind-up sound'
        if kind not in arakus_special:
            strike = resolve(f'boss_{index}_{kind}_{pid}')
            assert strike, f'{boss}.{pid} ({kind}) has no strike sound'
print('PASS coverage: every pattern of all five bosses has a wind-up and a strike sound')

# 3. literal keys in game code
literal = set(re.findall(r'PlaySound\("([^"]+)"\)', patterns_src)) | set(re.findall(r'sound\.Play\("([^"]+)"\)', tower))
literal |= {'boss_death'}  # EndBattle uses a conditional
new_events = {'boss_0_Rage', 'boss_0_ShieldThrow', 'boss_0_Slash_throw', 'boss_0_landing', 'boss_0_ShieldRush',
              'boss_0_Slash_sweep_low', 'boss_0_Slash_sweep_high', 'phase_change', 'pillar_crash', 'barrier_blast',
              'barrier_break', 'player_hurt', 'boss_death'}
assert new_events <= literal, new_events - literal
for key in new_events:
    assert resolve(key), key
assert 'g.PlaySound("tell_"+g.bossIndex+"_"+p.id);' in patterns_src
assert 'g.PlaySound("erupt_"+g.bossIndex+"_"+h.pattern);' in patterns_src
assert 'sound.danger=' in tower and 'public float tension,danger;' in (root / 'Tests/UnityShim.cs').read_text()
assert resolve('hit') is None and resolve('cue') is None and resolve('dash') is None   # old keys keep their sounds
print('PASS wiring: wind-up, strike, hazard, phase, barrier, death, hurt and heartbeat events resolve')

# 4. determinism
with tempfile.TemporaryDirectory() as tmp:
    subprocess.run([sys.executable, str(root / 'Tools/sfx/generate_boss_sfx.py'), '--out', tmp], check=True, capture_output=True)
    for name in used:
        a = hashlib.sha256((audio / f'sfx_{name}.wav').read_bytes()).hexdigest()
        b = hashlib.sha256((Path(tmp) / f'sfx_{name}.wav').read_bytes()).hexdigest()
        assert a == b, f'{name} is not reproducible'
print('PASS determinism: generator reproduces every committed file byte for byte')
