"""Read-only sprite, pivot, scope and cue wiring checks; not a Unity visual test."""
from pathlib import Path
from PIL import Image
import argparse,subprocess,re,json
p=argparse.ArgumentParser();p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True)
r=Path(__file__).resolve().parents[1];checks=[];frames=[]
def check(n,ok):checks.append({'check':n,'pass':bool(ok)})
def unchanged(paths):return subprocess.run(['git','diff','--quiet','HEAD','--',*paths],cwd=r).returncode==0
def source(name):return (r/'Assets/Scripts'/name).read_text()
for name in re.findall(r'new Definition\("([^"]+)","캐릭터"',source('ArakusAnimationSelection.cs')):
 im=Image.open(r/'Assets/Resources/ArakusV1'/f'{name}.png').convert('RGBA')
 for i in range(im.width//192):
  box=im.crop((192*i,0,192*(i+1),192)).getchannel('A').getbbox();frames.append({'animation':name,'frame':i,'alpha_bbox':box})
  check(f'{name}_{i}_foot_baseline_y160',box[3]==160)
idle=[f for f in frames if f['animation']=='Idle']
width=max(max(96-f['alpha_bbox'][0],f['alpha_bbox'][2]-96) for f in idle);height=max(160-f['alpha_bbox'][1] for f in idle)
check('Idle_envelope_33px_by89px',width==33 and height==89)
check('body_frame_count_77',len(frames)==77)
check('resources_PPU_pivots_GUIDs_scene_prefab_background_data_unchanged',unchanged(['Assets/Resources','Assets/Animation','Assets/Prefabs','Assets/Scenes','Assets/Data','Assets/StreamingAssets','ProjectSettings']))
check('DDA_DB_save_player_rules_source_unchanged',unchanged(['Assets/Scripts/GameData.cs','Assets/Scripts/GameServices.cs','Assets/Scripts/SaveStore.cs','Assets/Scripts/CloudSync.cs','Assets/Scripts/LobbyCards.cs']))
world=source('WorldArtist.cs');base=subprocess.check_output(['git','show','HEAD:Assets/Scripts/WorldArtist.cs'],cwd=r,text=True)
extract=lambda s:s[s.index('        bool DrawThroneRoom()'):s.index('        void Pillar(float x)')]
check('throne_layout_byte_identical',extract(world)==extract(base))
check('root_scale_scoped_to_Arakus','bossActor&&game.bossIndex==0?ArakusGeometry.VisualScale:1.4f' in source('ActorView.cs'))
visual=source('ArakusVisuals.cs')
check('props_use_parent_world_scale_compensation','ArakusGeometry.LegacyEffectScale/transform.lossyScale.x' in visual and 'ArakusGeometry.LegacyEffectScale/Mathf.Abs(transform.lossyScale.y)' in visual)
check('both_pillar_fades_still_0_6',all('clock-pillar'+side+'At<.6f' in visual for side in ['Left','Right']))
check('melee_telegraph_uses_shared_geometry','width=reach+.3f' in world and 'ArakusGeometry.AttackHeight(g.spec,p)' in world)
check('rush_telegraph_includes_5m_travel_and_contact','reach=5+ArakusGeometry.Reach(g.spec,p)' in world)
report={'passed':sum(c['pass'] for c in checks),'failed':sum(not c['pass'] for c in checks),'checks':checks,'frames':frames,'idle_envelope':{'half_width_px':width,'height_px':height,'PPU':56,'new_scale':2.2,'half_width_world':width*2.2/56,'height_world':height*2.2/56},'not_verified':['Unity renderer Transform hierarchy','runtime Animator and sprite import','live input/VFX readability','Windows build']}
(a.output/'sprite-scope-checks.json').write_text(json.dumps(report,indent=2));print(report['passed'],'PASS',report['failed'],'FAIL');raise SystemExit(bool(report['failed']))
