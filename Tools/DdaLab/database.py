"""Read-only public-client PostgREST analytics. Never writes DB or returns identities/keys."""
import base64,collections,datetime,json,math,threading,time
from pathlib import Path
from urllib.parse import urlencode,urlsplit
from urllib.request import Request,build_opener,HTTPRedirectHandler
from urllib.error import HTTPError,URLError
COHORT='demo_exhibit_20261121'
GROUPS=('novice','normal','expert')
TABLES={
 'proficiency_assessments':'owner_key,profile_id,event_id,is_first,group_name,completed,skipped,valid,score,confidence,active_seconds,elapsed_seconds,parry_attempts,parry_successes,dodge_attempts,dodge_successes,damage_hits,raw_json',
 'proficiency_battles':'owner_key,profile_id,event_id,assessment_event_id,boss,seconds,raw_json',
 'proficiency_pattern_samples':'owner_key,profile_id,battle_id,pattern,level,opportunities,parries,dodges,hits',
 'pattern_difficulty_by_proficiency':'boss,pattern,group_name,opportunities,players,success_rate,parry_rate,hit_rate,suggested_level',
 'battles':'id,boss,clear,seconds,dda,hits,parries'}
ORDERS={'proficiency_assessments':'owner_key,profile_id,event_id','proficiency_battles':'owner_key,profile_id,event_id','proficiency_pattern_samples':'owner_key,profile_id,battle_id,pattern','pattern_difficulty_by_proficiency':'boss,pattern,group_name','battles':'id'}
def utc():return datetime.datetime.now(datetime.timezone.utc).isoformat()
def public_config(path):
 c=json.loads(Path(path).read_text(encoding='utf-8-sig'));u=urlsplit(c.get('url',''));key=c.get('anonKey','')
 if u.scheme!='https' or not u.hostname or not u.hostname.endswith('.supabase.co') or u.port not in (None,443) or u.username or u.password or u.query or u.fragment or u.path not in ('','/'):
  raise ValueError('공개 Supabase HTTPS 프로젝트 URL이 필요합니다.')
 if key.startswith('sb_publishable_'):pass
 else:
  try:role=json.loads(base64.urlsafe_b64decode(key.split('.')[1]+'===')).get('role')
  except Exception:role=None
  if role!='anon':raise ValueError('anon/publishable 공개 클라이언트 키만 허용합니다. 관리자 키 사용 금지.')
 return {'url':c['url'].rstrip('/'),'anonKey':key,'proficiencySync':c.get('proficiencySync') is True}
class NoRedirect(HTTPRedirectHandler):
 def redirect_request(self,*args,**kwargs):return None

def fetch_table(config,table):
 rows=[];opener=build_opener(NoRedirect())
 for offset in range(0,20000,1000):
  url=config['url']+'/rest/v1/'+table+'?'+urlencode({'select':TABLES[table],'order':ORDERS[table],'offset':offset,'limit':1000})
  key=config['anonKey'];headers={'apikey':key,'Accept':'application/json'}
  if not key.startswith('sb_publishable_'):headers['Authorization']='Bearer '+key
  try:
   with opener.open(Request(url,headers=headers,method='GET'),timeout=8) as response:
    raw=response.read(8*1024*1024+1)
    if len(raw)>8*1024*1024:raise ValueError('응답 크기 한도 초과')
    batch=json.loads(raw)
  except HTTPError as e:raise ValueError(f'HTTP {e.code}: SELECT 권한/RLS/테이블·열 정의 확인 필요') from None
  except URLError as e:raise ValueError('GET 전송 실패: '+type(e.reason).__name__+': '+str(e.reason).replace(key,'[redacted]')[:160]) from None
  except (TimeoutError,OSError):raise ValueError('서버 GET 시간 초과/연결 오류') from None
  if not isinstance(batch,list) or any(not isinstance(x,dict) for x in batch):raise ValueError('SELECT 응답 형식 불일치')
  rows.extend(batch)
  if not batch:return rows
  # Keep paging even when server cap is less than requested, with actual count.
  if len(batch)!=1000:
   # A smaller final page is terminal on the default PostgREST cap; probe once via count below is safer.
   return _finish_pages(config,table,rows,opener,headers)
 raise ValueError('20,000행 조회 한도 도달: 부분 결과를 전체로 표시하지 않습니다.')
def _finish_pages(config,table,rows,opener,headers):
 # Explicit offsets prevent duplicate first pages on servers with a reduced max-rows.
 for _ in range(200):
  url=config['url']+'/rest/v1/'+table+'?'+urlencode({'select':TABLES[table],'order':ORDERS[table],'offset':len(rows),'limit':1000})
  try:
   with opener.open(Request(url,headers=headers,method='GET'),timeout=8) as r:
    raw=r.read(8*1024*1024+1)
   if len(raw)>8*1024*1024:raise ValueError('응답 크기 한도 초과')
   batch=json.loads(raw)
  except HTTPError as e:raise ValueError(f'HTTP {e.code}: SELECT 권한/RLS/테이블·열 정의 확인 필요') from None
  except (URLError,TimeoutError,OSError):raise ValueError('추가 페이지 GET 실패: 부분 결과 제외') from None
  if not isinstance(batch,list) or any(not isinstance(x,dict) for x in batch):raise ValueError('페이지 응답 불일치')
  if not batch:return rows
  rows.extend(batch)
  if len(rows)>=20000:raise ValueError('20,000행 조회 한도 도달: 부분 결과 제외')
 raise ValueError('페이지 한도 도달: 부분 결과 제외')
def num(x):return float(x) if isinstance(x,(int,float)) and not isinstance(x,bool) and math.isfinite(x) else 0.
def category(row):
 raw=row.get('raw_json')
 if not isinstance(raw,dict):return 'other_demo'
 demo=raw.get('_demo')
 if isinstance(demo,dict) and demo.get('cohort')==COHORT:return 'demo'
 if isinstance(raw,dict) and '_demo' in raw:return 'other_demo'
 return 'real'
def valid(a):return a.get('is_first') is True and a.get('completed') is True and a.get('valid') is True and not a.get('skipped') and num(a.get('confidence'))>=.5 and num(a.get('active_seconds'))>0 and num(a.get('parry_attempts'))>=1 and num(a.get('dodge_attempts'))>=1
def aggregate(tables):
 assessments=tables.get('proficiency_assessments',[]);battles=tables.get('proficiency_battles',[]);samples=tables.get('proficiency_pattern_samples',[])
 aidx={(a.get('owner_key'),a.get('profile_id'),a.get('event_id')):a for a in assessments};bidx={(b.get('owner_key'),b.get('profile_id'),b.get('event_id')):b for b in battles}
 result={'cohort':COHORT,'source':'실제 DB SELECT 결과 · 발표용 가상 데이터와 실제 기록 구분','groups':[],'patterns':[],'bosses':[],'excludedSamples':0,'joinConflicts':0,'mixedView':tables.get('pattern_difficulty_by_proficiency',[]),'mixedViewLabel':'전체 집계 뷰: 실제·발표용 가상 데이터 혼합 가능 · 출처별 분리 불가','legacyBattles':len(tables.get('battles',[])),'counts':{}}
 for kind in ('demo','real','other_demo'):
  aa=[a for a in assessments if category(a)==kind];bb=[b for b in battles if category(b)==kind]
  result['counts'][kind]={'players':len({(a.get('owner_key'),a.get('profile_id')) for a in aa}),'assessments':len(aa),'battles':len(bb),'samples':0}
  for group in GROUPS:
   group_a=[a for a in aa if a.get('group_name')==group and valid(a)]
   avg=lambda field:sum(num(a.get(field)) for a in group_a)/len(group_a) if group_a else None
   rate=lambda success,total:sum(num(a.get(success)) for a in group_a)/sum(num(a.get(total)) for a in group_a) if sum(num(a.get(total)) for a in group_a)>0 else None
   result['groups'].append({'source':kind,'group':group,'players':len({(a.get('owner_key'),a.get('profile_id')) for a in aa if a.get('group_name')==group}),'validFirst':len(group_a),'score':avg('score'),'activeSeconds':avg('active_seconds'),'elapsedSeconds':avg('elapsed_seconds'),'parryRate':rate('parry_successes','parry_attempts'),'dodgeRate':rate('dodge_successes','dodge_attempts'),'hits':avg('damage_hits')})
  for boss in sorted({str(b.get('boss')) for b in bb}):
   bs=[b for b in bb if b.get('boss')==boss];result['bosses'].append({'source':kind,'boss':boss,'battles':len(bs),'seconds':sum(num(b.get('seconds')) for b in bs)/len(bs)})
 buckets={}
 for s in samples:
  b=bidx.get((s.get('owner_key'),s.get('profile_id'),s.get('battle_id')));a=aidx.get((b.get('owner_key'),b.get('profile_id'),b.get('assessment_event_id'))) if b else None
  if not a or not b:result['excludedSamples']+=1;continue
  if category(a)!=category(b):result['joinConflicts']+=1;continue
  kind=category(a);result['counts'][kind]['samples']+=1
  if not valid(a) or s.get('level')!=0 or num(s.get('opportunities'))<=0:continue
  key=(kind,a.get('group_name'),b.get('boss'),s.get('pattern'))
  bucket=buckets.setdefault(key,{'owners':set(),'opportunities':0,'parries':0,'dodges':0,'hits':0});bucket['owners'].add(b.get('owner_key'))
  for field in ('opportunities','parries','dodges','hits'):bucket[field]+=num(s.get(field))
 for (kind,group,boss,pattern),b in sorted(buckets.items()):
  op=b['opportunities'];success=(b['parries']+b['dodges'])/op;enough=len(b['owners'])>=3 and op>=20
  result['patterns'].append({'source':kind,'group':group,'boss':boss,'pattern':pattern,'players':len(b['owners']),'opportunities':op,'successRate':success,'parryRate':b['parries']/op,'hitRate':b['hits']/op,'eligible':enough,'suggestedLevel':(-1 if success<.45 else 1 if success>.85 else 0) if enough else None,'basis':'읽기 전용 로컬 집계 / repository SQL 규칙: neutral Lv0, valid first, confidence>=.5, >=3 owners, >=20 opportunities'})
 return result
class Database:
 def __init__(self,config_path):
  self.path=config_path;self.lock=threading.Lock();self.value={'status':'미조회','configured':False,'tables':{},'analysis':None,'writeStatus':'SELECT 화면은 게임 INSERT 성공을 증명하지 않습니다.'};self.busy=False;self.last=0
 def state(self):
  with self.lock:return json.loads(json.dumps(self.value))
 def refresh(self):
  with self.lock:
   if self.busy or time.monotonic()-self.last<5:return
   self.busy=True;self.last=time.monotonic();self.value['status']='조회 중'
  threading.Thread(target=self._read,daemon=True).start()
 def _read(self):
  statuses={};tables={}
  try:
   c=public_config(self.path)
   from concurrent.futures import ThreadPoolExecutor
   def one(table):
    try:return table,fetch_table(c,table),None
    except Exception as e:return table,None,str(e) if isinstance(e,ValueError) else 'SELECT 응답/정의 검증 실패'
   with ThreadPoolExecutor(max_workers=5) as pool:
    for table,rows,error in pool.map(one,TABLES):
     statuses[table]={'status':'실패' if error else 'SELECT 성공','rows':None if error else len(rows),'error':error}
     if rows is not None:tables[table]=rows
   required=set(TABLES)-{'battles','pattern_difficulty_by_proficiency'}
   analysis=aggregate(tables) if required<=tables.keys() else {'mixedView':tables.get('pattern_difficulty_by_proficiency',[]),'mixedViewLabel':'전체 집계: 실제·발표용 혼합 가능','counts':None,'groups':[],'patterns':[],'bosses':[]}
   with self.lock:self.value={'status':('일부 조회 실패' if tables else '조회 실패') if any(x['error'] for x in statuses.values()) else 'SELECT 완료','configured':True,'proficiencySync':c['proficiencySync'],'tables':statuses,'analysis':analysis,'lastAttemptUtc':utc(),'lastSelectUtc':utc() if tables else None,'liveSchema':'운영 view SQL 정의는 공개 클라이언트로 확인 불가. 출처별 집계는 repository migration 규칙 사용.','writeStatus':'게임 전송 상태는 LIVE 진단에서 별도 확인. SELECT 성공은 INSERT 성공이 아닙니다.'}
  except Exception as e:
   with self.lock:self.value={'status':'설정 오류/조회 불가','configured':False,'error':str(e) if isinstance(e,ValueError) else '설정 파일 없음/읽기 실패','tables':{},'analysis':None}
  finally:
   with self.lock:self.busy=False
