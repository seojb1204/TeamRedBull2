"""Read-only baseline comparisons; writes documentation images/reports only. No Unity/DB access.
Run after create_assets.py and validate_assets.py. Requires the original PR #19 commit in local Git history.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import subprocess, hashlib, json, io
DOC=Path(__file__).resolve().parent
ROOT=DOC.parents[2]
BASE='b7210c075badc9533c7418f5410bf0d74983d820'
def git(*args):return subprocess.check_output(['git',*args],cwd=ROOT)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
tracked=git('ls-tree','-r','--name-only',BASE).decode().splitlines()
meta=[p for p in tracked if p.startswith('Assets/StreamingAssets/Art/ArakusRoom') and p.endswith('.meta')]
iron=[p for p in tracked if '/IronWarworks/' in p or p.startswith('Docs/Art/ArakusRoomConcept/IronWarworks_')]
checks=[]
for group,paths in [('meta_bytes_and_GUIDs',meta),('IronWarworks_bytes',iron)]:
    checks.append({'name':group,'files':len(paths),'pass':all((ROOT/p).read_bytes()==git('show',BASE+':'+p) for p in paths)})
changed=git('diff','--name-only',BASE).decode().splitlines()
checks.append({'name':'only_approved_art_and_documentation_modified','pass':all(p.startswith('Docs/Art/ArakusRoomConcept/') or (p.startswith('Assets/StreamingAssets/Art/ArakusRoom/StoneFortress/') and p.endswith('.png')) for p in changed)})
# Comparison uses the historical art, not a recolored or reconstructed "before".
old=Image.open(io.BytesIO(git('show',BASE+':Docs/Art/ArakusRoomConcept/StoneFortress_future_mockup.png'))).convert('RGB')
new=Image.open(DOC/'StoneFortress_future_mockup.png').convert('RGB')
canvas=Image.new('RGB',(1280,390),(16,18,23));d=ImageDraw.Draw(canvas)
for x,im,label in [(0,old,'BEFORE / fortress and gate'),(640,new,'AFTER / Crimson Throne of the War God')]:
    d.text((x+12,10),label,fill=(225,217,215));canvas.paste(im.resize((640,360),Image.Resampling.NEAREST),(x,30))
canvas.save(DOC/'StoneFortress_redesign_comparison.png')
# Check deterministic regeneration of the generator's 34 original outputs.
paths=sorted((ROOT/'Assets/StreamingAssets/Art/ArakusRoom').rglob('*.png'))+sorted(p for p in DOC.glob('*.png') if p.name!='StoneFortress_redesign_comparison.png')
before={str(p.relative_to(ROOT)):sha(p) for p in paths}
subprocess.run(['python3',str(DOC/'create_assets.py')],cwd=ROOT,check=True)
after={str(p.relative_to(ROOT)):sha(p) for p in paths}
checks.append({'name':'deterministic_regeneration','files':len(paths),'pass':before==after})
(DOC/'reproduction_report.json').write_text(json.dumps({'identical_pngs':sum(before[k]==after[k] for k in before),'method':'Regenerated the 34 generator PNG outputs and checked byte equality; historical comparison generated separately by verify_revision.py','sha256':after},indent=2)+'\n')
report={'baseline':BASE,'checks':checks,'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'scope_note':'Git diff checks tracked paths; inspect git status before staging new documents as well.'}
(DOC/'revision_preservation_report.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
assert all(c['pass'] for c in checks)
