"""Asset and wiring checks; not a substitute for Unity playback/listening QA."""
from pathlib import Path
import hashlib,json,wave,array,math
root=Path(__file__).resolve().parents[1]
audio=root/'Assets/StreamingAssets/Audio'
rows=json.loads((audio/'BOSS_MUSIC_MANIFEST.json').read_text())
assert [r['boss'] for r in rows]==['arakus','phanes','adicus','aethelos','kharos']
for r in rows:
 p=audio/('boss_'+r['boss']+'.wav')
 assert hashlib.sha256(p.read_bytes()).hexdigest()==r['sha256']
 assert Path(str(p)+'.meta').exists()
 with wave.open(str(p)) as w:
  assert (w.getnchannels(),w.getsampwidth(),w.getframerate(),w.getnframes())==(2,2,32000,1984000)
  a=array.array('h',w.readframes(w.getnframes()))
 assert max(abs(x) for x in a)<=26215
 assert sum(x*x for x in a)>0
 assert r['seam_step']<.025
 print(r['boss'], 'PASS', '62s stereo', 'peak',round(r['peak'],4),'rms',round(r['rms'],4))
s=(root/'Assets/Scripts/Soundscape.cs').read_text()
assert all('"boss_'+r['boss']+'"' in s for r in rows)
assert 'bossMusic??battleMusic' in s and 'Time.unscaledDeltaTime' in s
assert 'Imported(BossTracks[requestedBoss],false)' in s
assert 'sound.SetBoss(bossIndex);' in (root/'Assets/Scripts/TowerGame.cs').read_text()
assert (audio/'battle.wav').exists()
print('PASS: five unique loops, original fallback, boss wiring, fade and bounded decode')
