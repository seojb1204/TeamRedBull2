from pathlib import Path
from PIL import Image
import sys,csv,re,json
p=Path(sys.argv[1])
from visible_rim import detect_visible_rim
labels=list(csv.DictReader((p/'run'/'evaluation-only-timing.csv').open()));rows=[]
for line in (p/'sequence-capture.log').read_text(encoding='utf-8-sig').splitlines():
 m=re.search(r'FRAME seq=(\d+) stamp100ns=(\d+) age_ms=([0-9.]+)',line)
 if not m:continue
 seq,stamp=int(m[1]),int(m[2]);lab=min(labels,key=lambda l:abs(int(l['qpc100ns'])-stamp));residual=abs(int(lab['qpc100ns'])-stamp)/10000
 if residual>50:raise ValueError(f'Clock alignment invalid: {residual}ms; no timing conclusions permitted')
 im=Image.open(p/f'sequence-{seq}.ppm').convert('RGB');d=detect_visible_rim(640,480,im.tobytes())
 rows.append({'seq':seq,'residual_ms':residual,'pixel_candidate':d['visible_rim_candidate'],'pixels':d['pixels'],**{k:float(v) for k,v in lab.items() if k!='qpc100ns'}})
positives=[r for r in rows if r['pixel_candidate']];hits=[];prev=0
for l in labels:
 if int(l['hits'])>prev:hits.append({'gameSeconds':float(l['gameSeconds']),'hits':int(l['hits']),'hp':float(l['hp'])});prev=int(l['hits'])
result={'frames':len(rows),'labels':len(labels),'max_residual_ms':max(r['residual_ms'] for r in rows),'positive_frames':len(positives),'positive_eta_range':[min((r['cueEta'] for r in positives),default=None),max((r['cueEta'] for r in positives),default=None)],'positive_after_cue':sum(r['cueEta']<0 for r in positives),'shots_frames':sum(r['shots']>0 for r in rows),'hits':hits,'limitation':'Nearest LateUpdate sample is approximate render alignment, not exact present fence. Internal values are evaluation-only; cueEta is not actual collision time.','rows':rows}
(p/'timing-alignment-valid.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='rows'},indent=2));print('POSITIVES',json.dumps(positives))
