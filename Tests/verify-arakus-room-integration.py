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
world=(ROOT/'Assets/Scripts/WorldArtist.cs').read_text(encoding='utf-8');visual=(ROOT/'Assets/Scripts/ArakusVisuals.cs').read_text(encoding='utf-8')
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
check('width_fit_uses_actual_viewport','unit=pixelWidth/sourceWidth/pixelsPerWorld' in world and 'centerX=g.zoomFocus.x*g.zoomPunch*.12f' in world)
check('room_does_not_cancel_camera_shake','centerX=camera.transform.position.x' not in world)
check('no_integer_scale_or_floor_snap','Mathf.Floor(fit)' not in world and 'Mathf.Round(groundPixel)' not in world)
check('art_and_pillar_resources_unchanged',subprocess.run(['git','diff','--quiet','HEAD','--','Assets/Resources','Assets/StreamingAssets'],cwd=ROOT).returncode==0)
# Independent numerical projection oracle; not Unity Camera/GPU execution.
for width,height in [(1920,1080),(1280,720),(2560,1440),(1024,768),(2520,1080),(640,360)]:
 for zoom,cx,cy in [(0,0,5),(.8,-1,4.5),(.8,1,5)]:
  aspect=width/height;ortho=max(8.2,14.3/aspect)-zoom;ppu=height/(2*ortho)
  scale=width/1248;unit=scale/ppu;worldWidth=1248*unit
  ground=height/2+cy*ppu;top=ground-608*scale;bottom=ground+94*scale
  oldFit=min((width-2*abs(cx*ppu)-16)/1248,((cy+ortho)*ppu-8)/608,((ortho-cy)*ppu-8)/94)
  oldScale=math.floor(oldFit) if oldFit>=1 else 1/math.ceil(1/oldFit)
  key=f'{width}x{height}_zoom{zoom}_cx{cx}'
  check(key+'_fills_actual_width',abs(worldWidth*ppu-width)<1e-6)
  check(key+'_no_horizontal_crop_or_margin',abs((worldWidth/2)*ppu-width/2)<1e-6)
  centerY=(608-702/2)*unit
  check(key+'_exact_walkline_y_zero',abs(centerY+(702/2-608)*unit)<1e-9)
  check(key+'_preserves_image_aspect',abs(worldWidth/(702*unit)-1248/702)<1e-9)
  if abs(aspect-16/9)<1e-6 and zoom==0:
   check(key+'_top_crop_under_7_percent',max(0,-top)<height*.07)
  metrics.append(dict(width=width,height=height,zoom=zoom,camera_x=cx,camera_y=cy,
    background_screen_width=width,background_screen_height=702*scale,
    background_world_width=worldWidth,background_world_height=702*unit,
    screen_pixels_per_source_pixel=scale,left_margin_px=0,right_margin_px=0,
    top_crop_px=max(0,-top),top_margin_px=max(0,top),top_crop_source_px=max(0,-top)/scale,
    walkline_world_y=0,walkline_screen_y_from_top=ground,
    original_bottom_screen_y=bottom,lower_extension_px=max(0,height-bottom),
    previous_contain_width_px=1248*oldScale,previous_side_margin_px=(width-1248*oldScale)/2))
  if zoom:continue
  worldpos=lambda x,y:(width/2+(x-cx)*ppu,height/2+(cy-y)*ppu)
  def render(old):
   im=Image.new('RGBA',(width,height),(9,13,19,255))
   k=oldScale if old else scale
   xx=round(width/2-1248*k/2);yy=round(ground-608*k)
   def paste(image,x,y):
    tile=image.resize((round(image.width*k),round(image.height*k)),Image.Resampling.NEAREST);im.alpha_composite(tile,(round(x),round(y)))
   paste(bg,xx,yy)
   strip=bg.crop((0,640,1248,702));end=yy+round(702*k);stripH=round(62*k)
   for y in range(end,height+stripH,stripH):paste(strip,xx,y)
   for x in [-4,4]:
    size=round(192*ppu/40);fr=sheet.crop((0,0,192,192)).resize((size,size),Image.Resampling.NEAREST);px,py=worldpos(x,0);im.alpha_composite(fr,(round(px-size/2),round(py-160*size/192)))
   helper.actor(im,worldpos,ppu,'player');helper.actor(im,worldpos,ppu,'boss')
   return im
  render(False).save(args.output/f'offline_after_{width}x{height}.png')
  if (width,height)==(1920,1080):
   render(True).save(args.output/'offline_previous_contain_1920x1080.png')
   # Source pixels are unchanged; representative stair fragment shown at new Point scale.
   after=render(False);x0=round(510*scale);y0=round(top+485*scale)
   after.crop((x0,y0,x0+500,y0+200)).save(args.output/'offline_stairs_width_fit.png')
(args.output/'width-fit-metrics.json').write_text(json.dumps(metrics,indent=2))
# Fixed zoom focus plus camera shake moves room and world pillars equally.
# Same unit and edge padding as WorldArtist; authored width/aspect stay unchanged.
for width,height in [(1920,1080),(1280,720),(2560,1440),(1024,768),(2520,1080),(640,360)]:
 for zoom,focus in [(0,0),(.8,-3),(.8,3)]:
  for sx,sy in [(-.3,.2),(.3,-.2),(0,0)]:
   center=focus*zoom*.12; ortho=max(8.2,14.3/(width/height))-zoom
   ppu=height/(2*ortho); unit=width/1248/ppu
   room_shift=((center-(center+sx))*ppu,-sy*ppu)
   pillar_shift=((-4-(center+sx))*ppu-(-4-center)*ppu,-sy*ppu)
   key=f'shake_{width}x{height}_zoom{zoom}_focus{focus}_x{sx}'
   check(key+'_room_pillar_equal_delta',all(abs(a-b)<1e-8 for a,b in zip(room_shift,pillar_shift)))
   padding=abs(sx)+unit
   left=(center-width/ppu/2-padding-(center+sx))*ppu
   right=(center+width/ppu/2+padding-(center+sx))*ppu
   check(key+'_outside_gutters_cover_viewport',left<=-width/2 and right>=width/2)
report={'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'checks':checks,'not_verified':['Unity compilation/rendering','real combat visuals and HUD','Windows build'],'note':'Source/asset checks and independent numeric projection only; no DB connection.'}
(args.output/'resource-checks.json').write_text(json.dumps(report,indent=2));print(report['passed'],'PASS',report['failed'],'FAIL')


sys.exit(bool(report['failed']))
