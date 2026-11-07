"""Read-only asset checks, plus a new JSON report. Does not launch Unity or load DB settings."""
from pathlib import Path
from PIL import Image
import json, math, hashlib, subprocess, importlib.util, sys
sys.dont_write_bytecode=True

DOC=Path(__file__).resolve().parent
ROOT=DOC.parents[2]
spec=importlib.util.spec_from_file_location('room_art',DOC/'create_assets.py')
art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
checks=[];metrics={}
def check(name,value,detail=''):
    checks.append({'name':name,'pass':bool(value),'detail':detail})
def luminance(c):
    v=[x/255 for x in c[:3]];v=[x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4 for x in v]
    return .2126*v[0]+.7152*v[1]+.0722*v[2]
def contrast(a,b):
    a,b=luminance(a),luminance(b);return (max(a,b)+.05)/(min(a,b)+.05)
manifest=json.loads((DOC/'asset_manifest.json').read_text())
for variant in art.SEEDS:
    folder=art.ART/variant;entries={}
    expected={'room_far.png':(768,512),'room_mid.png':(768,512),'floor_tile.png':(128,48),
              'pillar_intact.png':(192,192),'pillar_broken.png':(192,192),'pillar_sheet.png':(384,192)}
    for name,size in expected.items():
        p=folder/name;im=Image.open(p)
        check(variant+'/'+name+'/format',im.size==size and im.mode=='RGBA',str(im.size))
        check(variant+'/'+name+'/alpha_binary',set(art.pixels(im.getchannel('A')))<={0,255})
        check(variant+'/'+name+'/meta',Path(str(p)+'.meta').exists())
        entries[name]={'size':list(im.size),'colors':len(im.getcolors(im.width*im.height) or []),'bbox':im.getchannel('A').getbbox()}
    far=Image.open(folder/'room_far.png');mid=Image.open(folder/'room_mid.png');tile=Image.open(folder/'floor_tile.png')
    check(variant+'/far_opaque',far.getchannel('A').getextrema()==(255,255))
    check(variant+'/mid_transparent',mid.getchannel('A').getextrema()==(0,255))
    check(variant+'/floor_opaque',tile.getchannel('A').getextrema()==(255,255))
    seam=sum(abs(a-b) for y in range(48) for a,b in zip(tile.getpixel((0,y)),tile.getpixel((127,y))))
    check(variant+'/floor_repeat_edge_delta_zero',seam==0,str(seam))
    intact=Image.open(folder/'pillar_intact.png');broken=Image.open(folder/'pillar_broken.png');sheet=Image.open(folder/'pillar_sheet.png')
    check(variant+'/sheet_intact_matches',sheet.crop((0,0,192,192)).tobytes()==intact.tobytes())
    check(variant+'/sheet_broken_matches',sheet.crop((192,0,384,192)).tobytes()==broken.tobytes())
    for name,im in [('intact',intact),('broken',broken)]:
        box=im.getchannel('A').getbbox()
        check(variant+'/'+name+'/foot_baseline',box[3]==160,str(box))
        check(variant+'/'+name+'/no_clipping',0<box[0]<box[2]<192 and box[1]>0)
    check(variant+'/pillar_height',intact.getchannel('A').getbbox()[3]-intact.getchannel('A').getbbox()[1]==120)
    composite=art.layer_composite(variant)
    # Specific lower central cue band; this is not a guarantee of contrast at every animated pixel.
    samples=list(art.pixels(composite.crop((220,art.GROUND-2,548,art.GROUND+2))))
    red=min(contrast((255,76,81),c) for c in samples);blue=min(contrast((71,158,255),c) for c in samples)
    entries['cue_band_metrics']={'red_min_luminance_ratio':red,'blue_min_luminance_ratio':blue,
        'sample_source_rect':[220,art.GROUND-2,548,art.GROUND+2],
        'note':'Static background band, no characters/pillars/shaders; not a game accessibility certification.'}
    check(variant+'/cue_band_red_at_least_3',red>=3,str(red))
    check(variant+'/cue_band_blue_at_least_3',blue>=3,str(blue))
    # Overscan includes the two actual camera viewports. 21:9/portrait not promised.
    for width,height in [(640,360),(480,360)]:
        aspect=width/height;ortho=max(8.2,14.3/aspect)
        vw=2*ortho*aspect;vh=2*ortho
        box=(art.W/2-vw/2*art.PPU,art.GROUND-(5+vh/2)*art.PPU,art.W/2+vw/2*art.PPU,art.GROUND-(5-vh/2)*art.PPU)
        check(variant+f'/camera_{width}x{height}_covered',box[0]>=0 and box[1]>=0 and box[2]<=art.W and box[3]<=art.H,str(box))
    metrics[variant]=entries
for item in manifest['assets']:
    check(item['path']+'/manifest_sha',hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest()==item['sha256'])
protected=['Assets/Scripts','Assets/Scenes','Assets/Prefabs','Assets/Data','Assets/Resources','Assets/Animation',
           'Assets/Art','Assets/StreamingAssets/Art/Castle','ProjectSettings']
result=subprocess.run(['git','diff','--quiet',manifest['base_commit'],'--',*protected],cwd=ROOT)
check('existing_code_scenes_prefabs_character_castle_pillar_unchanged',result.returncode==0)
allguids=[]
for p in art.ART.rglob('*.meta'):
    guid=next(x[6:] for x in p.read_text().splitlines() if x.startswith('guid: '));allguids.append(guid)
check('new_meta_GUIDs_unique',len(allguids)==len(set(allguids)))
report={'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'checks':checks,
        'metrics':metrics,'manual_review':['Both full-size concept mockups, actual 4:3 camera, repeated floor, pillar comparison reviewed'],
        'not_run':['Unity import/rendering','Windows build','real combat/VFX/HUD overlap','ultrawide/portrait camera integration']}
(DOC/'quality_report.json').write_text(json.dumps(report,indent=2))
print('ARAKUS_ROOM_ART_CHECKS',report['passed'],'PASS',report['failed'],'FAIL')
for v in metrics:print(v,metrics[v]['cue_band_metrics'])
if report['failed']:raise SystemExit(1)
