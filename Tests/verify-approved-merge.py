"""Integration contracts from user choices; not a substitute for Unity playback."""
from pathlib import Path
from PIL import Image
import subprocess,re
r=Path(__file__).resolve().parents[1]
def git(ref,p):return subprocess.check_output(['git','show',ref+':'+p],cwd=r)
p='Assets/Resources/ArakusV1/Walk.png'
assert (r/p).read_bytes()==git('2c7e44ade165ff3ebc1bde0ee1aa221ffbf96205',p)
assert (r/(p+'.meta')).read_bytes()==git('origin/main',p+'.meta')
im=Image.open(r/p);assert im.size==(768,192) and im.mode=='RGBA'
meta=(r/(p+'.meta')).read_text();assert len(re.findall(r'      name:',meta))==4
assert 'spritePixelsToUnits: 56' in meta
src=(r/'Assets/Scripts/ArakusVisuals.cs').read_text()
# 2026-11-16 user choice: every Arakus motion (walk included) prefers V2, V1 only as fallback
assert 'Resources.LoadAll<Sprite>("ArakusV2/"+id)' in src and '"ArakusV1/":"ArakusV2/"' not in src
for f in (r/'Assets/Resources/ArakusV2').iterdir():
 assert f.read_bytes()==git('fc133cca61656fad4193664df6d8e4eb5d553732',str(f.relative_to(r)))
w=(r/'Assets/Scripts/WorldArtist.cs').read_text()
arena=w.split('bool DrawBossArena()',1)[1].split('bool DrawThroneRoom()',1)[0]
assert 'centerX=g.bossIndex==0?g.zoomFocus.x*g.zoomPunch*.12f:camera.transform.position.x' in arena
assert arena.count('if(g.bossIndex==0)RoomEdges(')==2
assert 'sourceWidth-1' in w
bridge=r/'Assets/Scripts/PixelAgentBridge.cs'
if bridge.exists():
 source=bridge.read_text(encoding='utf-8').split('public static PixelAgentBridge AttachIfRequested',1)[1]
 assert 'if(Array.IndexOf(args,"-towerPixelAgent")<0)return null;' in source
 assert '!SaveStore.IsIsolated' in source and '!AutoRunArgs.DisableDda' in source
print('APPROVED_MERGE_CONTRACT_PASS: all motions prefer V2, V1 walk file kept, unchanged metadata/V2 pack, current arena camera path, pixel experiment requires explicit isolated opt-in')
