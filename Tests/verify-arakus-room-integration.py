"""Offline asset/contracts checks. Does not launch Unity, load DB config, or touch saves."""
from pathlib import Path
from PIL import Image,ImageChops,ImageFilter
import subprocess,json,hashlib,sys,importlib.util,argparse,math
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
metrics=[]
# Numerical layout oracle and offline projection; this does not execute Unity Camera or GPU rendering.
for width,height in [(1920,1080),(1280,720),(2560,1440),(1024,768),(2520,1080),(640,360)]:
 for zoom,cx,cy in [(0,0,5),(.8,-1,4.5),(.8,1,5)]:
  aspect=width/height;ortho=max(8.2,14.3/aspect)-zoom;ppu=height/(2*ortho)
  fit=min((width-2*abs(cx*ppu)-16)/1248,((cy+ortho)*ppu-8)/608,((ortho-cy)*ppu-8)/94)
  scale=math.floor(fit) if fit>=1 else 1/math.ceil(1/fit)
  unit=scale/ppu;ww=1248*unit
  left=round(width/2-cx*ppu-1248*scale/2);ground=round(height/2+cy*ppu)
  top=ground-608*scale;right=left+1248*scale;bottom=ground+94*scale
  key=f'{width}x{height}_zoom{zoom}_cx{cx}'
  check(key+'_whole_composition_contained',left>=0 and right<=width and top>=0 and bottom<=height)
  check(key+'_integer_or_reciprocal_scale',scale>=1 and scale==int(scale) or scale<1 and abs(1/scale-round(1/scale))<1e-6)
  check(key+'_walkline_snap_half_pixel',abs(ground-(height/2+cy*ppu))<=.5)
  beforeScale=26.5/1248*ppu
  beforeLeft=width/2-cx*ppu-1248*beforeScale/2
  beforeCrop=(max(0,-beforeLeft)+max(0,beforeLeft+1248*beforeScale-width))/beforeScale
  metrics.append(dict(width=width,height=height,zoom=zoom,camera_x=cx,source_pixels_per_screen_pixel_inverse=1/scale,screen_pixels_per_source_pixel=scale,before_screen_scale=beforeScale,before_horizontal_source_pixels_cropped=beforeCrop,after_horizontal_source_pixels_cropped=0))
  if zoom:continue
  world=lambda x,y:(width/2+(x-cx)*ppu,height/2+(cy-y)*ppu)
  def render(old):
   im=Image.new('RGBA',(width,height),(9,13,19,255))
   if old:
    k=beforeScale;xx=round(width/2-1248*k/2);yy=round(height/2+5*ppu-608*k);walk=height/2+5*ppu
   else:k=scale;xx=left;yy=round(top);walk=ground
   def paste(image,x,y):
    tile=image.resize((round(image.width*k),round(image.height*k)),Image.Resampling.NEAREST);im.alpha_composite(tile,(round(x),round(y)))
   paste(bg,xx,yy)
   strip=bg.crop((0,640,1248,702));end=yy+round(702*k);stripH=round(62*k)
   for y in range(end,height+stripH,stripH):paste(strip,xx,y)
   for x in [-4,4]:
    size=round(192*ppu/40);fr=sheet.crop((0,0,192,192)).resize((size,size),Image.Resampling.NEAREST);px,py=world(x,0);im.alpha_composite(fr,(round(px-size/2),round(py-160*size/192)))
   helper.actor(im,world,ppu,'player');helper.actor(im,world,ppu,'boss')
   return im
  render(False).save(args.output/f'offline_after_{width}x{height}.png')
  if (width,height)==(1920,1080):
   render(True).save(args.output/'offline_before_1920x1080.png')
   after=render(False);before=render(True)
   # Same source stair region, displayed at nearest 2x for direct sampling comparison.
   cut=Image.new('RGBA',(1280,400),(9,13,19,255))
   for offset,image,k in [(0,before,beforeScale),(640,after,scale)]:
    x0=round(width/2-1248*k/2+510*k);y0=round(height/2+5*ppu-608*k+485*k)
    crop=image.crop((x0,y0,x0+round(220*k),y0+round(95*k))).resize((round(440*k),round(190*k)),Image.Resampling.NEAREST);cut.alpha_composite(crop,(offset,0))
   cut.save(args.output/'offline_stairs_before_after.png')
(args.output/'pixel-fit-metrics.json').write_text(json.dumps(metrics,indent=2))
report={'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'checks':checks,'not_verified':['Unity compilation/rendering','real combat visuals and HUD','Windows build'],'note':'Source/asset checks and offline previews only; no DB connection.'}
(args.output/'resource-checks.json').write_text(json.dumps(report,indent=2));print(report['passed'],'PASS',report['failed'],'FAIL');sys.exit(bool(report['failed']))
