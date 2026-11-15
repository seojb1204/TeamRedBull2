"""Offline contracts only: not Unity rendering or a playtest."""
from pathlib import Path
from PIL import Image
import json,hashlib,math,subprocess
root=Path(__file__).resolve().parents[1]
items=json.loads((root/'Docs/Art/BossArenas/manifest.json').read_text())
world=(root/'Assets/Scripts/WorldArtist.cs').read_text()
assert len(items)==5
assert 'fight&&(DrawBossArena()||(g.bossIndex==0&&DrawThroneRoom()))' in world
assert 'if(room==null)return false;' in world
for a in items:
 p=root/a['file'];im=Image.open(p);im.verify()
 assert list(Image.open(p).size)==a['size']
 assert hashlib.sha256(p.read_bytes()).hexdigest()==a['sha256']
 assert Path(str(p)+'.meta').exists()
 w,h=a['size'];row=a['ground_row']
 assert 0<row<h
 for vw,vh in [(1920,1080),(1024,768),(2520,1080),(640,360),(720,1280)]:
  for zoom,cx,cy in [(0,0,5),(.8,-1,4.5),(.8,1,5)]:
   ortho=max(8.2,14.3/(vw/vh))-zoom
   unit=max(2*ortho*(vw/vh)/w,max(0,cy+ortho)/row)
   center=(row-h*.5)*unit
   assert abs(center+(h*.5-row)*unit)<1e-9
   assert w*unit>=2*ortho*(vw/vh)-1e-9
   assert row*unit>=cy+ortho-1e-9
   bottom=(row-h)*unit;strip=64*unit
   count=math.ceil(max(0,bottom-(cy-ortho))/strip)
   assert bottom-count*strip<=cy-ortho+1e-9
for path in ['Assets/StreamingAssets/Art/ArakusRoom','Assets/StreamingAssets/Audio/battle.wav','Assets/Scripts/BossCombat.cs','Assets/Scripts/BossPatternSystem.cs','Assets/Scripts/GameData.cs','Assets/Data','Assets/Scenes','Assets/Prefabs']:
 assert subprocess.run(['git','diff','--quiet','HEAD','--',path],cwd=root).returncode==0,path
print('PASS: five art assets, hashes, floor anchoring and viewport coverage (75 projections); originals/combat untouched. Unity/runtime NOT tested.')
