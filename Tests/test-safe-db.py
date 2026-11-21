"""Safe aggregate transport/validation fixtures only, no production traffic."""
import sys,json,copy,unittest,tempfile,io
from pathlib import Path
from urllib.error import HTTPError
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Tools/DdaLab'))
import database as db

def summary():
 groups=[]
 for source in db.SOURCES:
  for group in db.GROUPS:
   released=source=='demo'
   g={'source':source,'group':group,'released':released}
   g.update({k:(v if released else None) for k,v in dict(players=3,validFirst=3,score=.7,activeSeconds=60,elapsedSeconds=80,parryRate=.5,dodgeRate=.5,hits=2).items()});groups.append(g)
 return {'schemaVersion':1,'cohort':db.COHORT,'policy':{'minOwners':3,'minPatternOpportunities':20},'counts':{'demo':{'players':9,'assessments':9,'battles':9,'samples':9},'real':None,'other_demo':None},'groups':groups,'patterns':[{'source':'demo','group':'novice','boss':'arakus','pattern':'cleave','players':3,'opportunities':30,'successRate':.8,'parryRate':.5,'hitRate':.2,'eligible':True,'suggestedLevel':0}],'bosses':[{'source':'demo','group':'novice','boss':'arakus','battles':3,'seconds':30}]}
class Response:
 def __init__(self,data):self.data=data
 def __enter__(self):return self
 def __exit__(self,*args):pass
 def read(self,n):return json.dumps(self.data).encode()
class SafeDatabase(unittest.TestCase):
 def test_valid_snapshot(self):
  x=db.clean_summary(summary());self.assertEqual(x['counts']['demo']['players'],9);self.assertIsNone(x['counts']['real']);self.assertEqual(x['groups'][0]['group'],'novice')
 def test_identity_projection(self):
  x=summary();x['owner_key']='NEVER';x['groups'][0]['raw_json']={'secret':'NEVER'};x['patterns'][0]['profile_id']='NEVER';self.assertNotIn('NEVER',json.dumps(db.clean_summary(x)))
 def test_suppression_cannot_be_disabled(self):
  x=summary();x['policy']['minOwners']=2
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_suppressed_numbers_rejected(self):
  x=summary();x['groups'][3]['players']=1
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_suppressed_source_totals_rejected(self):
  x=summary();x['counts']['real']={'players':1,'assessments':1,'battles':1,'samples':1}
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_group_sum_mismatch_rejected(self):
  x=summary();x['counts']['demo']['players']=300
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_duplicates_rejected(self):
  x=summary();x['groups'][1]=x['groups'][0]
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_nan_rejected(self):
  x=summary();x['groups'][0]['score']=float('nan')
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_untrusted_pattern_rejected(self):
  x=summary();x['patterns'][0]['pattern']='private-user-label'
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_small_pattern_rejected(self):
  for field,value in [('players',2),('opportunities',19)]:
   x=summary();x['patterns'][0][field]=value
   with self.assertRaises(ValueError):db.clean_summary(x)
 def test_schema_version_rejected(self):
  x=summary();x['schemaVersion']=True
  with self.assertRaises(ValueError):db.clean_summary(x)
 def test_raw_select_blocked_before_network(self):
  with patch.object(db,'build_opener') as opener:
   for table in ['proficiency_assessments','proficiency_battles','proficiency_pattern_samples','battles']:
    with self.assertRaises(ValueError):db.fetch_table({},table)
   opener.assert_not_called()
 def test_get_only_parameterless_rpc(self):
  calls=[]
  class Opener:
   def open(self,request,timeout):calls.append(request);return Response(summary())
  with patch.object(db,'build_opener',return_value=Opener()):x=db.fetch_summary({'url':'https://fixture.supabase.co','anonKey':'sb_publishable_fixture'})
  self.assertEqual(calls[0].get_method(),'GET');self.assertTrue(calls[0].full_url.endswith('/rpc/'+db.RPC));self.assertNotIn('?',calls[0].full_url);self.assertFalse(calls[0].has_header('Authorization'));self.assertEqual(x['counts']['demo']['players'],9)
 def test_http_error_body_not_exposed(self):
  class Opener:
   def open(self,request,timeout):raise HTTPError(request.full_url,401,'unauthorized',{},io.BytesIO(b'{"secret":"NEVER"}'))
  with patch.object(db,'build_opener',return_value=Opener()):
   with self.assertRaisesRegex(ValueError,'HTTP 401') as e:db.fetch_summary({'url':'https://fixture.supabase.co','anonKey':'sb_publishable_fixture'})
  self.assertNotIn('NEVER',str(e.exception))
 def test_missing_rpc_keeps_33_mixed_rows_no_raw(self):
  view=[{'boss':'arakus','pattern':pattern,'group_name':group,'players':3,'opportunities':30,'success_rate':.8,'parry_rate':.5,'hit_rate':.2,'suggested_level':0} for group in db.GROUPS for pattern in sorted(db.PATTERNS['arakus'])]
  calls=[]
  class Opener:
   def open(self,request,timeout):
    calls.append(request.full_url)
    if '/rpc/' in request.full_url:raise HTTPError(request.full_url,404,'missing',{},None)
    from urllib.parse import parse_qs,urlsplit
    offset=int(parse_qs(urlsplit(request.full_url).query)['offset'][0]);return Response(view[offset:offset+1000])
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'config.json';p.write_text(json.dumps({'url':'https://fixture.supabase.co','anonKey':'sb_publishable_fixture'}),encoding='utf-8')
   with patch.object(db,'build_opener',return_value=Opener()):x=db.Database(p);x._read();state=x.state()
  self.assertEqual(len(state['analysis']['mixedView']),33);self.assertIsNone(state['analysis']['counts']);self.assertFalse(state['aggregateAvailable']);self.assertIn('혼합',state['analysis']['mixedViewLabel']);self.assertEqual(len(calls),3);self.assertTrue(all('/rpc/'+db.RPC in u or '/pattern_difficulty_by_proficiency?' in u for u in calls))
 def test_rpc_survives_view_failure(self):
  with tempfile.TemporaryDirectory() as tmp:
   p=Path(tmp)/'config.json';p.write_text(json.dumps({'url':'https://fixture.supabase.co','anonKey':'sb_publishable_fixture'}),encoding='utf-8')
   with patch.object(db,'fetch_summary',return_value=db.clean_summary(summary())),patch.object(db,'fetch_table',side_effect=ValueError('HTTP503')):x=db.Database(p);x._read();state=x.state()
  self.assertTrue(state['aggregateAvailable']);self.assertEqual(state['analysis']['counts']['demo']['players'],9);self.assertIn('실패',state['status'])
if __name__=='__main__':unittest.main()
