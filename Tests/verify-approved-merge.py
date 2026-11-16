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
assert '(id=="Walk"?"ArakusV1/":"ArakusV2/")+id' in src
for f in (r/'Assets/Resources/ArakusV2').iterdir():
 assert f.read_bytes()==git('fc133cca61656fad4193664df6d8e4eb5d553732',str(f.relative_to(r)))
w=(r/'Assets/Scripts/WorldArtist.cs').read_text()
arena=w.split('bool DrawBossArena()',1)[1].split('bool DrawThroneRoom()',1)[0]
assert 'centerX=g.bossIndex==0?g.zoomFocus.x*g.zoomPunch*.12f:camera.transform.position.x' in arena
assert arena.count('if(g.bossIndex==0)RoomEdges(')==2
assert 'sourceWidth-1' in w
assert not (r/'Assets/Scripts/PixelAgentBridge.cs').exists()
print('APPROVED_MERGE_CONTRACT_PASS: exact chosen walk, unchanged metadata/V2 pack, current arena camera path, no pixel experiment')
