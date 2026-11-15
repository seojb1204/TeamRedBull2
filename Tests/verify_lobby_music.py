"""Asset and wiring checks; not a substitute for Unity playback/listening QA."""
from pathlib import Path
import hashlib,json,wave,array,math
root=Path(__file__).resolve().parents[1]
audio=root/'Assets/StreamingAssets/Audio'
rows=json.loads((audio/'LOBBY_MUSIC_MANIFEST.json').read_text())
assert [r['boss'] for r in rows]==['lobby']
for r in rows:
 p=audio/(r['boss']+'.wav')
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
assert 'lobbyMusic=Imported("lobby",false)' in s
assert '(lobbyMusic??carrier)' in s
assert 'if(music.clip==bossMusic)music.Stop();' in s
print('PASS: lobby recording, synthesized fallback, no restart on boss selection')

tower=(root/'Assets/Scripts/TowerGame.cs').read_text()
assert 'sound.SetPaused(mode==ScreenMode.Battle&&(paused||showHelp||showSettings));' in tower
assert 'sound.tension=mode==ScreenMode.Battle?' in tower
assert 'music.Pause();sourcePaused=true;' in s and 'music.UnPause();sourcePaused=false;' in s
print('PASS: ESC pause routed separately from battle identity')
