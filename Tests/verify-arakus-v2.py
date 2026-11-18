from pathlib import Path
import json,re,hashlib
from PIL import Image
r=Path(__file__).resolve().parents[1]
m=json.loads((r/'Docs/Art/ArakusV2Integration/SOURCE_MANIFEST.json').read_text())
n=0
for a in m['animations']:
 p=r/'Assets/Resources/ArakusV2'/f"{a['id']}.png"
 im=Image.open(p);assert im.size==(192*a['count'],192),p
 assert im.mode=='RGBA',p
 meta=Path(str(p)+'.meta').read_text()
 assert 'spritePixelsToUnits: 56' in meta
 assert len(re.findall(r'      name:',meta))==a['count']
 for i in range(a['count']):
  alpha=im.crop((i*192,0,(i+1)*192,192)).getchannel('A')
  assert alpha.getbbox() is not None
  n+=1
 assert (r/'Assets/Resources/ArakusV1'/p.name).is_file()
print('ARAKUS_V2_ASSET_CONTRACT_PASSED',n,'frames')
