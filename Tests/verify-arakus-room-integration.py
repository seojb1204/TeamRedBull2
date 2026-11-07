"""Offline asset/contracts checks. Does not launch Unity, load DB config, or touch saves."""
from pathlib import Path
from PIL import Image,ImageChops,ImageFilter
import subprocess,json,hashlib,sys,importlib.util,argparse
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);args=p.parse_args();args.output.mkdir(parents=True,exist_ok=True)
ART=ROOT/'Assets/StreamingAssets/Art/ArakusRoom';RES=ROOT/'Assets/Resources/ArakusV1'
checks=[]
def check(name,value):
 checks.append({'name':name,'pass':bool(value)})
 if not value: print('FAIL',name)
def original(path):return subprocess.check_output(['git','show','HEAD:'+path],cwd=ROOT)
sheet=Image.open(RES/'Pillar.png').convert('RGBA');rim=Image.open(RES/'Pillar_Outline.png').convert('RGBA')
check('sheet_384x192',sheet.size==(384,192));check('rim_dimensions',rim.size==sheet.size)
check('original_pillar_meta_and_GUID_unchanged',(RES/'Pillar.png.meta').read_bytes()==original('Assets/Resources/ArakusV1/Pillar.png.meta'))
for i in range(2):
 a=sheet.crop((192*i,0,192*(i+1),192)).getchannel('A');r=rim.crop((192*i,0,192*(i+1),192)).getchannel('A')
 check(f'frame_{i}_foot_baseline',a.getbbox()[3]==160)
 check(f'frame_{i}_height',a.getbbox()[3]-a.getbbox()[1]==120)
 check(f'frame_{i}_one_pixel_outer_rim',r.tobytes()==ImageChops.subtract(a.filter(ImageFilter.MaxFilter(3)),a).tobytes())
for f in ['WorldArtist.cs','ArakusVisuals.cs']:check(f+'_meta_unchanged',(ROOT/'Assets/Scripts'/f'{f}.meta').read_bytes()==original('Assets/Scripts/'+f+'.meta'))
protected=['Assets/Scripts/BossPatternSystem.cs','Assets/Scripts/TowerGame.cs','Assets/Scripts/BossCombat.cs','Assets/Scripts/GameData.cs','Assets/Scripts/GameServices.cs','Assets/Scripts/SaveStore.cs','Assets/Scripts/CloudSync.cs','Assets/Scripts/ActorView.cs','Assets/Scripts/ScenePresentation.cs','Assets/Scenes','Assets/Prefabs','Assets/Data','ProjectSettings','Assets/StreamingAssets/Art/Castle']
check('combat_DDA_DB_input_camera_scene_prefab_settings_unchanged',subprocess.run(['git','diff','--quiet','HEAD','--',*protected],cwd=ROOT).returncode==0)
world=(ROOT/'Assets/Scripts/WorldArtist.cs').read_text();visual=(ROOT/'Assets/Scripts/ArakusVisuals.cs').read_text()
check('battle_result_only_gate','bool fight=g.mode==ScreenMode.Battle||g.mode==ScreenMode.Result;' in world and 'throneRoom=fight&&g.bossIndex==0&&DrawThroneRoom()' in world)
check('legacy_world_has_fallback','if(!throneRoom)' in world and 'if(room==null)return false;' in world)
check('fade_kept_both_sides',all('clock-pillar'+s+'At<.6f' in visual and '1-(clock-pillar'+s+'At)/.6f' in visual for s in ['Left','Right']))
check('glow_charge_kind_only','g.current.kind==AttackKind.Dash' in visual)
check('glow_behind_body','alpha*(charging?.55f:.25f)),-31)' in visual)
bg=Image.open(ART/'Arakus_Throne_Background_Game.png').convert('RGBA');raw=Image.open(ART/'Arakus_Throne_Background_16x9.png').convert('RGBA')
check('background_dimensions',raw.size==bg.size==(1248,702))
# Only the two authored post rectangles may differ.
difference=ImageChops.difference(raw.convert('RGB'),bg.convert('RGB'))
for box in [(430,453,511,608),(791,453,870,608)]:difference.paste((0,0,0),box)
check('background_edits_confined_to_baked_posts',difference.getbbox() is None)
# Offline camera projection, explicitly not Unity rendering or a shader simulation.
spec=importlib.util.spec_from_file_location('roomart',ROOT/'Docs/Art/ArakusRoomConcept/create_assets.py');helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
for width,height in [(640,360),(480,360),(840,360)]:
 aspect=width/height;ortho=max(8.2,14.3/aspect);ppu=height/(2*ortho);vw=2*ortho*aspect
 check(f'{width}x{height}_whole_room_width_visible',vw>=26.5)
 check(f'{width}x{height}_whole_room_top_visible',608*26.5/1248<=5+ortho)
 im=Image.new('RGBA',(width,height),(9,13,19,255));world=lambda x,y:(width/2+x*ppu,height/2+(5-y)*ppu)
 def paste(image,x,y,ww,hh):
  tile=image.resize((round(ww*ppu),round(hh*ppu)),Image.Resampling.NEAREST);cx,cy=world(x,y);im.alpha_composite(tile,(round(cx-tile.width/2),round(cy-tile.height/2)))
 unit=26.5/1248;bottom=(608-702)*unit;strip=raw.crop((0,640,1248,702))
 paste(bg,0,(608-351)*unit,26.5,702*unit)
 for i in range(12):paste(strip,0,bottom-(i+.5)*62*unit,26.5,62*unit)
 for x in [-4,4]:
  size=round(192*ppu/40);fr=sheet.crop((0,0,192,192)).resize((size,size),Image.Resampling.NEAREST);px,py=world(x,0);im.alpha_composite(fr,(round(px-size/2),round(py-160*size/192)))
 helper.actor(im,world,ppu,'player');helper.actor(im,world,ppu,'boss')
 im.resize((width*2,height*2),Image.Resampling.NEAREST).save(args.output/f'offline_{width}x{height}.png')
report={'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'checks':checks,'not_verified':['Unity compilation/rendering','real combat visuals and HUD','Windows build'],'note':'Source/asset checks and offline previews only; no DB connection.'}
(args.output/'resource-checks.json').write_text(json.dumps(report,indent=2));print(report['passed'],'PASS',report['failed'],'FAIL');sys.exit(bool(report['failed']))
