import json,re,sys,hashlib
from pathlib import Path
from PIL import Image
from visual_tracker import Tracker,choose_action
root=Path(sys.argv[1]).resolve()
tracker=Tracker(); rows=[]; previous=-1
for line in (root/'sequence-capture.log').read_text(encoding='utf-8-sig').splitlines():
 m=re.search(r'FRAME seq=(\d+) stamp100ns=(\d+) age_ms=([0-9.]+)',line)
 if not m: continue
 seq,stamp=int(m[1]),int(m[2]);age=float(m[3])
 fresh=stamp>previous and 0<=age<=500;previous=stamp
 im=Image.open(root/f'sequence-{seq}.ppm').convert('RGB')
 state,diag=tracker.observe((im.width,im.height,im.tobytes()))
 proposal=choose_action(state,diag) if fresh else 0
 rows.append({'seq':seq,'stamp100ns':stamp,'age_ms':age,'fresh':fresh,'rgb_sha256':hashlib.sha256(im.tobytes()).hexdigest(),'proposed_action':proposal,'dispatched_action':None,'gate':'shadow only; uncalibrated production-art perception','diag':diag})
(root/'sequence-shadow.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
print(json.dumps({'frames':len(rows),'fresh':sum(r['fresh'] for r in rows),'unique_images':len({r['rgb_sha256'] for r in rows}),'proposals':[r['proposed_action'] for r in rows],'dispatches':0,'diagnostics':rows[:2]},indent=2))
