"""In-memory test fixtures only, not inserted demo/production records."""
import sys,json,unittest,tempfile,zipfile,hashlib
from pathlib import Path
from unittest.mock import patch,Mock
from urllib.parse import parse_qs,urlsplit
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Tools/DdaLab'))
import database as db
from presentation import paths,commands,InstanceLock
from package_windows import stage_python,validate_game
class Exhibit(unittest.TestCase):
 def test_cohort_join_and_owner_threshold(self):
  # Small labelled fixture: matching keys must all join; owner hash is NOT the cohort marker.
  a={'owner_key':'1'*64,'profile_id':'p','event_id':'a','group_name':'expert','is_first':True,'completed':True,'valid':True,'skipped':False,'score':.9,'confidence':.8,'active_seconds':60,'parry_attempts':2,'parry_successes':2,'dodge_attempts':2,'dodge_successes':2,'raw_json':{'_demo':{'cohort':db.COHORT}}}
  b={'owner_key':a['owner_key'],'profile_id':'p','event_id':'b','assessment_event_id':'a','boss':'arakus','seconds':30,'raw_json':a['raw_json']}
  sample={'owner_key':a['owner_key'],'profile_id':'p','battle_id':'b','pattern':'cleave','level':0,'opportunities':20,'parries':10,'dodges':8,'hits':2}
  x=db.aggregate({'proficiency_assessments':[a],'proficiency_battles':[b],'proficiency_pattern_samples':[sample]})
  self.assertEqual(x['counts']['demo']['samples'],1);self.assertFalse(x['patterns'][0]['eligible']);self.assertIsNone(x['patterns'][0]['suggestedLevel'])
  self.assertEqual(x['counts']['real']['players'],0);self.assertEqual(x['groups'][2]['players'],1)
  b['raw_json']={};x=db.aggregate({'proficiency_assessments':[a],'proficiency_battles':[b],'proficiency_pattern_samples':[sample]});self.assertEqual(x['joinConflicts'],1)
  sample['profile_id']='wrong';x=db.aggregate({'proficiency_assessments':[a],'proficiency_battles':[b],'proficiency_pattern_samples':[sample]});self.assertEqual(x['excludedSamples'],1)
 def test_missing_marker_and_unknown_demo(self):
  self.assertEqual(db.category({'raw_json':{}}),'real');self.assertEqual(db.category({'raw_json':{'_demo':{'cohort':'other'}}}),'other_demo');self.assertEqual(db.category({}),'other_demo')
 def test_group_threshold_and_neutral_only(self):
  aa=[];bb=[];ss=[]
  for i in range(3):
   owner=str(i)*64;a={'owner_key':owner,'profile_id':'p','event_id':'a','group_name':'novice','is_first':True,'completed':True,'valid':True,'confidence':.7,'active_seconds':60,'parry_attempts':1,'dodge_attempts':1,'raw_json':{}}
   aa.append(a);bb.append({'owner_key':owner,'profile_id':'p','event_id':'b','assessment_event_id':'a','boss':'arakus','raw_json':{}});ss.append({'owner_key':owner,'profile_id':'p','battle_id':'b','pattern':'cleave','level':0,'opportunities':10,'parries':1,'dodges':1,'hits':8})
  result=db.aggregate({'proficiency_assessments':aa,'proficiency_battles':bb,'proficiency_pattern_samples':ss})
  self.assertTrue(result['patterns'][0]['eligible']);self.assertEqual(result['patterns'][0]['suggestedLevel'],-1)
  ss[-1]['level']=1;self.assertFalse(db.aggregate({'proficiency_assessments':aa,'proficiency_battles':bb,'proficiency_pattern_samples':ss})['patterns'][0]['eligible'])
 def test_pagination_get_only_reduced_cap(self):
  class Response:
   def __init__(self,rows):self.rows=rows
   def __enter__(self):return self
   def __exit__(self,*args):pass
   def read(self,n):return json.dumps(self.rows).encode()
  calls=[]
  class Opener:
   def open(self,request,timeout):
    calls.append(request);offset=int(parse_qs(urlsplit(request.full_url).query)['offset'][0]);return Response([{'boss':'x'}] if offset<2 else [])
  with patch.object(db,'build_opener',return_value=Opener()):rows=db.fetch_table({'url':'https://example.supabase.co','anonKey':'sb_publishable_fixture'},'pattern_difficulty_by_proficiency')
  self.assertEqual(len(rows),2);self.assertEqual(len(calls),3);self.assertTrue(all(r.get_method()=='GET' for r in calls))
 def test_admin_key_and_ssrf_rejected(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'config.json'
   for url,key in [('http://127.0.0.1','sb_publishable_test'),('https://example.supabase.co','sb_secret_test')]:
    p.write_text(json.dumps({'url':url,'anonKey':key}),encoding='utf-8')
    with self.assertRaises(ValueError):db.public_config(p)
 def test_no_raw_identities_returned(self):
  s=db.aggregate({'proficiency_assessments':[],'proficiency_battles':[],'proficiency_pattern_samples':[]});self.assertNotIn('owner_key',json.dumps(s));self.assertNotIn('profile_id',json.dumps(s));self.assertIn('혼합',s['mixedViewLabel'])
 def test_incomplete_select_never_fabricates_counts(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'config.json';p.write_text(json.dumps({'url':'https://example.supabase.co','anonKey':'sb_publishable_fixture'}),encoding='utf-8')
   with patch.object(db,'fetch_summary',side_effect=ValueError('HTTP 404 aggregate RPC missing')),patch.object(db,'fetch_table',return_value=[]) as view:
    x=db.Database(p);x._read();self.assertIsNone(x.state()['analysis']['counts']);self.assertEqual(x.state()['tables'][db.RPC]['status'],'실패');view.assert_called_once_with(db.public_config(p),'pattern_difficulty_by_proficiency')
 def test_new_run_paths_and_flags(self):
  with tempfile.TemporaryDirectory(prefix='한글 공백 ') as tmp:
   r=Path(tmp);a=paths(r,r);b=paths(r,r);self.assertNotEqual(a['live'],b['live']);lab,game=commands(a);self.assertIn('--port',lab);self.assertEqual(lab[lab.index('--port')+1],'0');self.assertEqual(game[1],'-towerDdaMonitor');self.assertTrue(a['live'].is_dir());self.assertIn('--prebuilt',lab)
 def test_duplicate_lock_release(self):
  with tempfile.TemporaryDirectory() as tmp:
   a=InstanceLock(Path(tmp)/'lock');b=InstanceLock(Path(tmp)/'lock');self.assertTrue(a.acquire());self.assertFalse(b.acquire());a.close();self.assertTrue(b.acquire());b.close()
 def test_zip_escape_rejected(self):
  with tempfile.TemporaryDirectory() as tmp:
   r=Path(tmp)
   for i,name in enumerate(['../escape','..\\escape','C:/escape']):
    z=r/(str(i)+'.zip')
    with zipfile.ZipFile(z,'w') as f:f.writestr(name,'bad')
    with self.assertRaises(ValueError):stage_python(z,r/str(i))
 def test_cloud_frame_whitelist(self):
  import monitor
  data={'schema':monitor.SCHEMA,'origin':'headless-fixture','sessionId':'a'*32,'runId':'b'*32,'sequence':1,'capturedUtc':monitor.utc(),'cloud':{'configuration':'클라이언트 설정 확인','pendingLegacy':3,'pendingProtected':-1,'anonKey':'DO_NOT_EXPORT','profileId':'DO_NOT_EXPORT'}}
  clean=monitor.frame(data);self.assertEqual(clean['cloud']['pendingLegacy'],3);self.assertNotIn('DO_NOT_EXPORT',json.dumps(clean));self.assertEqual(clean['cloud']['pendingProtected'],-1)
 def test_viewer_failure_keeps_game_start(self):
  import presentation,os
  with tempfile.TemporaryDirectory() as tmp:
   r=Path(tmp);(r/'game').mkdir();(r/'game/ReversalTower.exe').write_bytes(b'test fixture never executed')
   process=Mock();process.poll.return_value=0
   with patch.object(sys,'argv',['presentation','--root',str(r)]),patch.dict(os.environ,{'LOCALAPPDATA':str(r/'local')}),patch.object(presentation.subprocess,'Popen',side_effect=[process,FileNotFoundError('viewer fixture')]) as spawn:
    self.assertEqual(presentation.main(),0)
    self.assertEqual(spawn.call_count,2);self.assertEqual(spawn.call_args_list[0].args[0][1],'-towerDdaMonitor')
 def test_portable_template_no_installer_or_powershell(self):
  s=(Path(__file__).resolve().parents[1]/'Tools/DdaLab/Start-ReversalTower.cmd').read_text(encoding='utf-8')
  self.assertIn('runtime\\python\\python.exe',s);self.assertIn('goto gameonly',s);self.assertNotIn('powershell',s.lower());self.assertNotIn('dotnet ',s.lower())
 def test_python_staging_path_only_fixture(self):
  with tempfile.TemporaryDirectory() as tmp:
   r=Path(tmp);z=r/'fixture.zip'
   with zipfile.ZipFile(z,'w') as f:f.writestr('python.exe',b'MZ-fixture-only');f.writestr('python313._pth','python313.zip\n.\n');f.writestr('python313.dll',b'MZ fixture');f.writestr('python313.zip',b'fixture');f.writestr('_ssl.pyd',b'MZ fixture')
   stage_python(z,r/'out');self.assertIn('../../lab',(r/'out/python313._pth').read_text(encoding='utf-8'))
 def test_invalid_unity_build_not_packaged(self):
  with tempfile.TemporaryDirectory() as tmp:
   with self.assertRaises(ValueError):validate_game(Path(tmp))
if __name__=='__main__':unittest.main()
