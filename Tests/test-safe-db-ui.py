"""UI integration using actual isolated PostgreSQL aggregate output + mocked GET transport.
C# experiments/monitor files are real HEADLESS. Never claims actual Supabase/Unity LIVE.
"""
import argparse,json,sys,time,threading,io
from pathlib import Path
from urllib.parse import urlsplit,parse_qs
from urllib.error import HTTPError
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[1];sys.path.insert(0,str(ROOT/'Tools/DdaLab'))
import database,lab
p=argparse.ArgumentParser();p.add_argument('--fixture-dir',type=Path,required=True);p.add_argument('--engine',type=Path,required=True);p.add_argument('--monitor-root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--chromium',default='/usr/bin/chromium');a=p.parse_args();a.output.mkdir(parents=True,exist_ok=False)
summary=json.loads((a.fixture_dir/'ISOLATED_TEST_FIXTURE-summary.json').read_text());mixed=json.loads((a.fixture_dir/'ISOLATED_TEST_FIXTURE-mixed.json').read_text());database.clean_summary(summary)
mode={'missing':False};requests=[]
class Response:
 def __init__(self,obj):self.obj=obj
 def __enter__(self):return self
 def __exit__(self,*args):pass
 def read(self,n):return json.dumps(self.obj).encode()
class Opener:
 def open(self,request,timeout):
  path=urlsplit(request.full_url).path;requests.append({'method':request.get_method(),'path':path})
  if path=='/rest/v1/rpc/'+database.RPC:
   if mode['missing']:raise HTTPError(request.full_url,404,'not deployed',{},None)
   return Response(summary)
  if path=='/rest/v1/pattern_difficulty_by_proficiency':
   offset=int(parse_qs(urlsplit(request.full_url).query)['offset'][0]);return Response(mixed[offset:offset+1000])
  raise AssertionError('Forbidden raw/unexpected request '+path)
database.build_opener=lambda *args:Opener()
config=a.output/'config-fixture.json';config.write_text(json.dumps({'url':'https://fixture.supabase.co','anonKey':'sb_publishable_UNIT_FIXTURE_NOT_A_KEY','proficiencySync':True}),encoding='utf-8')
ready=a.output/'ready.json';engine=lab.Lab(None,a.output/'experiments',prebuilt=a.engine)
threading.Thread(target=lab.serve,args=(engine,0,a.monitor_root,ready,config),daemon=True).start()
for _ in range(100):
 if ready.exists():break
 time.sleep(.1)
url=json.loads(ready.read_text())['url']
with sync_playwright() as pw:
 browser=pw.chromium.launch(executable_path=a.chromium,headless=True,args=['--no-sandbox']);page=browser.new_page(viewport={'width':1440,'height':1000});errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
 page.goto(url);page.click('#tab-database');page.wait_for_function("document.querySelector('#db-status').textContent.includes('집계 조회 완료')")
 page.evaluate("document.body.insertAdjacentHTML('afterbegin','<div id=fixture-banner style=\"padding:12px;background:#7b4300;color:white\">격리 PostgreSQL 검증 데이터 · 실제 Supabase 조회 아님</div>')")
 assert '13' in page.locator('#db-counts').inner_text();assert '300' not in page.locator('#db-counts').inner_text();assert page.locator('#db-groups tr').count()==3;assert page.locator('#db-patterns tr').count()==3
 assert page.locator('#db-mixed tr').count()==len(mixed)
 page.screenshot(path=str(a.output/'safe-aggregate-ISOLATED-FIXTURE.png'),full_page=True)
 page.select_option('#db-source','real');assert '비공개' in page.locator('#db-counts').inner_text();assert '비공개(소표본)' in page.locator('#db-groups').inner_text()
 token=page.evaluate('token');state=page.request.get(url+'/api/database/state',headers={'X-Lab-Token':token}).json();serialized=json.dumps(state)
 assert all(x not in serialized for x in ['owner_key','profile_id','event_id','raw_json','service_role'])
 monitor=page.request.get(url+'/api/monitor/state',headers={'X-Lab-Token':token}).json();assert monitor['frame']['origin']=='headless-fixture' and monitor['status']!='LIVE GAME'
 mode['missing']=True;time.sleep(5.1);page.click('#db-refresh');page.wait_for_function("document.querySelector('#db-counts').textContent.includes('집계 API 조회 불가')")
 assert page.locator('#db-groups tr').count()==0 and page.locator('#db-mixed tr').count()==len(mixed)
 page.screenshot(path=str(a.output/'missing-rpc-mixed-fallback-ISOLATED-FIXTURE.png'),full_page=True)
 page.click('#tab-simulation');page.select_option('#mode','control');page.fill('#rounds','1');page.click('#run');page.wait_for_function("document.querySelector('#status').textContent.includes('실험 완료')",timeout=120000);assert page.locator('#count').inner_text()=='2'
 page.click('#tab-monitor');assert 'LIVE GAME' not in page.locator('#source-badge').inner_text();assert not errors,errors;browser.close()
assert all(r['method']=='GET' and r['path'] in ['/rest/v1/rpc/'+database.RPC,'/rest/v1/pattern_difficulty_by_proficiency'] for r in requests)
(a.output/'requests.json').write_text(json.dumps(requests,indent=2),encoding='utf-8')
print('SAFE_DB_UI_PASS actual isolated SQL output, source groups/privacy, no raw GET, missing-RPC fallback, actual C# experiment, HEADLESS monitor preserved; NOT production Supabase/Unity')
