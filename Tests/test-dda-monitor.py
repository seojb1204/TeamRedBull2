"""Reader contracts with explicit unit fixtures. No Unity/runtime/network/user-data claim."""
import copy,datetime as dt,importlib.util,json,os,sys,tempfile,time,unittest
from pathlib import Path
from unittest.mock import patch
R=Path(__file__).resolve().parents[1];sys.path.insert(0,str(R/'Tools/DdaLab'));import monitor as m

def packet(seq=1,sid='a'*32,run='b'*32,**extra):
 data=dict(schema=m.SCHEMA,origin='headless-fixture',runtime='unit fixture',sessionId=sid,runId=run,battleId='',diagnosticSession=run,boss='arakus',bossName='아라쿠스',phase='battle',kind='snapshot',capturedUtc=m.utc(),sequence=seq,running=True,dda=True,hasAttack=False,patterns=[dict(id='cleave',name='휘두르기',level=0,opportunities=2,parries=1,dodges=0,hits=1,ignored=0,reason='fixture',source='개인')]);data.update(extra);return data
class ReaderTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory(prefix='dda-reader-only-');self.root=Path(self.tmp.name);self.path=self.root/'DdaLive'/('a'*32);self.path.mkdir(parents=True);self.reader=m.Monitor(threaded=False)
 def tearDown(self):self.reader.stop.set();self.tmp.cleanup()
 def write(self,data):self.path.joinpath('latest.json').write_text(json.dumps(data),encoding='utf-8')
 def test_dotnet_roundtrip_utc_timestamp(self):
  self.assertIsNotNone(m.timestamp('2026-11-19T04:00:00.1234567Z'))
 def test_fixture_never_live(self):
  self.write(packet());s=self.reader.connect(str(self.root));self.assertEqual(s['status'],'FIXTURE');self.assertNotEqual(s['status'],'LIVE GAME')
 def test_synthetic_heartbeat_status_unit_only(self):
  self.write(packet(origin='unity-editor'));s=self.reader.connect(str(self.root));self.assertEqual(s['status'],'LIVE GAME')
  with patch.object(m.time,'time',return_value=time.time()+10):self.assertEqual(self.reader.state()['status'],'DISCONNECTED')
 def test_fresh_file_mtime_cannot_make_old_capture_live(self):
  old=(dt.datetime.now(dt.timezone.utc)-dt.timedelta(seconds=60)).isoformat();self.write(packet(origin='unity-player',capturedUtc=old));os.utime(self.path/'latest.json',None);self.assertEqual(self.reader.connect(str(self.root))['status'],'DISCONNECTED')
 def test_unlinked_legacy_learning_is_not_joined_by_boss_or_mtime(self):
  a=self.root/'DdaApplied';a.mkdir();(a/('d'*32+'.csv')).write_text('boss,pattern,decision,assignedLevel,windup,damage,cooldown,reason\narakus,cleave,1,0,1,8,4,test\n');l=self.root/'DdaLearning';l.mkdir();(l/('e'*32+'.json')).write_text(json.dumps(dict(boss='arakus',enabled=True,clear=True,abandoned=False,patterns=[])));s=self.reader.connect(str(self.root),'legacy:'+'d'*32);self.assertNotIn('learning',s['legacy']);self.assertEqual(len(s['sessions']),2)
 def test_stop_and_replay_never_live(self):
  self.write(packet(origin='unity-player',running=False));self.reader.connect(str(self.root));self.assertEqual(self.reader.state()['status'],'DISCONNECTED');e=self.reader.export();self.reader.load_replay(e);self.assertEqual(self.reader.state()['status'],'REPLAY');self.assertIsNone(self.reader.state()['receivedUtc'])
 def test_partial_and_malformed_json_preserve_good(self):
  self.write(packet());self.reader.connect(str(self.root));self.path.joinpath('latest.json').write_text('{"schema":');self.reader.scan();self.assertEqual(self.reader.state()['frame']['sequence'],1);self.write(packet(2));self.reader.scan();self.assertEqual(self.reader.state()['frame']['sequence'],2)
 def test_history_partial_invalid_and_sequence_order(self):
  self.write(packet(4));h=self.path/'events.jsonl';h.write_text(json.dumps(packet(1))+'\n'+json.dumps({'schema':'wrong'})+'\n'+json.dumps(packet(2))[:-2]);self.reader.connect(str(self.root));self.assertEqual([x['sequence'] for x in self.reader.state()['frames']],[1,4]);h.write_text(json.dumps(packet(1))+'\n'+json.dumps(packet(2))+'\n'+json.dumps(packet(3))+'\n');self.reader.scan();self.assertEqual(self.reader.state()['frame']['sequence'],4)
 def test_other_session_not_mixed(self):
  self.write(packet());self.path.joinpath('events.jsonl').write_text(json.dumps(packet(5,sid='c'*32))+'\n');self.reader.connect(str(self.root));self.assertEqual(len(self.reader.state()['frames']),1)
 def test_two_sessions_require_selection(self):
  self.write(packet());p=self.root/'DdaLive'/('c'*32);p.mkdir();(p/'latest.json').write_text(json.dumps(packet(sid='c'*32)));s=self.reader.connect(str(self.root));self.assertIsNone(s['frame']);self.assertEqual(len(s['sessions']),2);s=self.reader.connect(str(self.root),'live:'+'a'*32);self.assertEqual(s['frame']['sessionId'],'a'*32)
 def test_reconnect_clears_previous_session(self):
  self.write(packet());self.reader.connect(str(self.root));other=self.root/'other';other.mkdir();sub=other/'DdaLive'/('c'*32);sub.mkdir(parents=True);(sub/'latest.json').write_text(json.dumps(packet(sid='c'*32)));self.reader.connect(str(other));self.assertTrue(all(x['sessionId']=='c'*32 for x in self.reader.state()['frames']))
 def test_whitelist_ignores_private_files_and_fields(self):
  for folder in ['CloudQueue','ProficiencyKeys','ProfileArchive']:self.root.joinpath(folder).mkdir();self.root.joinpath(folder,'sensitive.json').write_text('DO_NOT_READ')
  self.root.joinpath('progress.json').write_text('DO_NOT_READ');self.root.joinpath('supabase.json').write_text('DO_NOT_READ')
  p=packet();p['anonKey']='DO_NOT_EXPORT';p['profileId']='DO_NOT_EXPORT';p['patterns'][0]['ownerSecret']='DO_NOT_EXPORT';self.write(p)
  opened=[];original=Path.open
  def spy(path,*args,**kwargs):opened.append(str(path));return original(path,*args,**kwargs)
  with patch.object(Path,'open',spy):self.reader.connect(str(self.root))
  self.assertTrue(all('latest.json' in p or 'events.jsonl' in p for p in opened));self.assertNotIn('DO_NOT_',json.dumps(self.reader.state()))
 def test_symlink_escape_not_opened(self):
  secret=self.root/'private.txt';secret.write_text('PRIVATE');(self.path/'latest.json').symlink_to(secret);s=self.reader.connect(str(self.root));self.assertIsNone(s['frame']);self.assertTrue(s['warnings'])
 def test_packet_and_history_size_bounds(self):
  self.path.joinpath('latest.json').write_bytes(b'x'*(m.MAX_PACKET+1));s=self.reader.connect(str(self.root));self.assertIsNone(s['frame']);self.write(packet());h=self.path/'events.jsonl';h.touch();os.truncate(h,m.MAX_FILE+1);self.reader.scan();self.assertEqual(self.reader.state()['frame']['sequence'],1);self.assertTrue(self.reader.state()['warnings'])
 def test_frame_memory_and_incremental_state_bounds(self):
  for i in range(1,502):self.reader._accept(packet(i),'a'*32)
  s=self.reader.state(since=499);self.assertEqual([x['sequence'] for x in s['frames']],[500,501]);self.assertLessEqual(len(self.reader._frames),m.MAX_FRAMES)
 def test_legacy_group_and_no_timestamp_fabrication(self):
  a=self.root/'DdaApplied';a.mkdir();(a/('d'*32+'.csv')).write_text('boss,pattern,decision,assignedLevel,windup,damage,cooldown,reason\narakus,cleave,1,-1,1.2,8,4,test\n')
  d=self.root/'DdaDecisions';d.mkdir();(d/('d'*32+'.csv')).write_text('boss,pattern,decision,weight,probability,selected,reason\narakus,cleave,1,1,1,True,test\n')
  s=self.reader.connect(str(self.root),'legacy:'+'d'*32);self.assertEqual(s['status'],'REPLAY');self.assertIsNone(s['frame']);self.assertEqual(s['legacy']['applied'][0]['assignedLevel'],-1);self.assertIsNone(s['receivedUtc']);self.assertIn('fileModifiedUtc',s['legacy'])
 def test_legacy_partial_quoted_csv_and_truncate(self):
  a=self.root/'DdaApplied';a.mkdir();f=a/('d'*32+'.csv');f.write_text('boss,pattern,decision,assignedLevel,windup,damage,cooldown,reason\narakus,cleave,1,0,1,8,4,"first\n');self.reader.connect(str(self.root),'legacy:'+'d'*32);self.assertFalse(self.reader.state()['legacy']['applied'])
  with f.open('a') as w:w.write('second"\n')
  self.reader.scan();self.assertEqual(len(self.reader.state()['legacy']['applied']),1)
  f.write_text('boss,pattern,decision,assignedLevel,windup,damage,cooldown,reason\n');self.reader.scan();self.assertEqual(len(self.reader.state()['legacy']['applied']),1) # retain previous valid rows, mark records not live
 def test_one_session_cannot_change_origin(self):
  self.reader._accept(packet(),'a'*32);self.assertRaises(ValueError,self.reader._accept,packet(2,origin='unity-player'),'a'*32)
 def test_replay_rejects_mixed_and_simulation(self):
  self.assertRaises(ValueError,self.reader.load_replay,{'schemaVersion':1,'config':{'mode':'combat'}})
  e=dict(schema=m.REPLAY,sessionId='a'*32,frames=[packet(),packet(2,sid='c'*32)],results=[]);self.assertRaises(ValueError,self.reader.load_replay,e)
 def test_no_stale_current_attack_after_result(self):
  a=dict(pattern='cleave',patternName='휘두르기',level=1,windup=1,damage=10,cooldown=4)
  f=m.frame(packet(hasAttack=True,attack=a,phase='result'));self.assertFalse(f['hasAttack']);self.assertIsNone(f['attack'])
 def test_invalid_numbers_are_not_graphed(self):
  p=packet();p['patterns'][0]['level']=99;f=m.frame(p);self.assertEqual(f['patterns'],[]);self.assertIsNone(m.number(float('nan')))
 def test_unknown_root_and_wrong_choice_are_rejected(self):
  self.assertRaises(ValueError,self.reader.connect,'relative/path');self.assertRaises(ValueError,self.reader.connect,str(self.root),{})
if __name__=='__main__':unittest.main()
