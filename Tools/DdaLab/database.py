"""Read-only public-client PostgREST analytics. Never writes DB or returns identities/keys."""
import base64,collections,datetime,json,math,threading,time
from pathlib import Path
from urllib.parse import urlencode,urlsplit
from urllib.request import Request,build_opener,HTTPRedirectHandler
from urllib.error import HTTPError,URLError
COHORT='demo_exhibit_20261121'
GROUPS=('novice','normal','expert')
RPC='exhibit_dda_summary_v1'
TABLES={'pattern_difficulty_by_proficiency':'boss,pattern,group_name,opportunities,players,success_rate,parry_rate,hit_rate,suggested_level'}
ORDERS={'pattern_difficulty_by_proficiency':'boss,pattern,group_name'}
PATTERNS={
 'arakus':set('cleave shield kick charge throw slam slash rush sweep step rage'.split()),
 'phanes':set('orb fan beam rain barrier pulse phanes_star'.split()),
 'adicus':set('scythe cross chain fall prisoner judgement spin'.split()),
 'aethelos':set('blade lance rift thunder creation heaven'.split()),
 'kharos':set('twelve draw echo eclipse rain phantom end'.split())}
SOURCES=('demo','real','other_demo')
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
 if table not in TABLES:raise ValueError('원시 테이블 조회 금지: 공개 집계 뷰만 허용합니다.')
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
 # Offline fixture/reference analyzer only. Runtime never fetches raw tables.
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
def _quantity(value,minimum=0,maximum=10**12,nullable=False):
 if value is None and nullable:return None
 if type(value) not in (int,float) or not math.isfinite(value) or not minimum<=value<=maximum:raise ValueError('집계 숫자 범위/형식 오류')
 return value

def _count(value,minimum=0):
 if type(value) is not int:raise ValueError('집계 건수 형식 오류')
 return _quantity(value,minimum)

def clean_summary(raw):
 if not isinstance(raw,dict) or type(raw.get('schemaVersion')) is not int or raw.get('schemaVersion')!=1 or raw.get('cohort')!=COHORT:raise ValueError('집계 RPC schema/version 불일치: 마이그레이션 확인 필요')
 policy=raw.get('policy',{})
 if not isinstance(policy,dict) or not isinstance(raw.get('counts'),dict):raise ValueError('집계 정책/합계 형식 오류')
 if policy.get('minOwners')!=3 or policy.get('minPatternOpportunities')!=20:raise ValueError('서버 집계 소표본 보호 정책 불일치')
 out={'schemaVersion':1,'cohort':COHORT,'source':'server aggregate RPC','policy':{'minOwners':3,'minPatternOpportunities':20},'groups':[],'patterns':[],'bosses':[],'counts':{}}
 groups=raw.get('groups')
 if not isinstance(groups,list) or len(groups)!=9:raise ValueError('집계 그룹 범위 오류')
 seen=set()
 def identity(row):
  if not isinstance(row,dict) or row.get('source') not in SOURCES or row.get('group') not in GROUPS:raise ValueError('집계 출처/그룹 오류')
  return {'source':row['source'],'group':row['group']}
 for g in groups:
  row=identity(g);key=(row['source'],row['group'])
  if key in seen:raise ValueError('중복 집계 그룹')
  seen.add(key)
  if type(g.get('released')) is not bool:raise ValueError('집계 공개 상태 오류')
  row['released']=g['released']
  fields={'players':(0,10**12),'validFirst':(0,10**12),'score':(0,1),'activeSeconds':(0,86400),'elapsedSeconds':(0,172800),'parryRate':(0,1),'dodgeRate':(0,1),'hits':(0,10**9)}
  for field,(low,high) in fields.items():
   value=g.get(field)
   if not row['released']:
    if value is not None:raise ValueError('소표본 그룹에 공개 수치가 포함됨')
    row[field]=None
   elif field in ('players','validFirst'):row[field]=_count(value,3)
   else:row[field]=_quantity(value,low,high,nullable=field in ('parryRate','dodgeRate'))
  out['groups'].append(row)
 out['groups'].sort(key=lambda g:(SOURCES.index(g['source']),GROUPS.index(g['group'])))
 for source in SOURCES:
  counts=raw.get('counts',{}).get(source);gg=[g for g in out['groups'] if g['source']==source]
  if not all(g['released'] for g in gg):
   if counts is not None:raise ValueError('소표본 출처의 전체 합계 공개 금지')
   out['counts'][source]=None
  else:
   if not isinstance(counts,dict):raise ValueError('공개 집계 합계 누락')
   clean={key:_count(counts.get(key)) for key in ('players','assessments','battles','samples')}
   if clean['players']!=sum(g['players'] for g in gg) or clean['assessments']!=sum(g['validFirst'] for g in gg):raise ValueError('집계 합계/그룹 불일치')
   out['counts'][source]=clean
 patterns=raw.get('patterns');bosses=raw.get('bosses')
 if not isinstance(patterns,list) or len(patterns)>400 or not isinstance(bosses,list) or len(bosses)>45:raise ValueError('집계 행 범위 오류')
 seen=set()
 for p in patterns:
  row=identity(p);boss=p.get('boss');pattern=p.get('pattern');key=(row['source'],row['group'],boss,pattern)
  if boss not in PATTERNS or pattern not in PATTERNS[boss] or key in seen:raise ValueError('알 수 없는/중복 패턴 집계')
  seen.add(key);row.update(boss=boss,pattern=pattern,players=_count(p.get('players'),3),opportunities=_count(p.get('opportunities'),20))
  for field in ('successRate','parryRate','hitRate'):row[field]=_quantity(p.get(field),0,1)
  if p.get('eligible') is not True or type(p.get('suggestedLevel')) is not int or p['suggestedLevel'] not in (-1,0,1):raise ValueError('공개 패턴 조건 오류')
  row.update(eligible=True,suggestedLevel=p['suggestedLevel']);out['patterns'].append(row)
 seen=set()
 for b in bosses:
  row=identity(b);boss=b.get('boss');key=(row['source'],row['group'],boss)
  if boss not in PATTERNS or key in seen:raise ValueError('알 수 없는/중복 보스 집계')
  seen.add(key);row.update(boss=boss,battles=_count(b.get('battles'),3),seconds=_quantity(b.get('seconds'),0,7200));out['bosses'].append(row)
 return out

def fetch_summary(config):
 key=config['anonKey'];headers={'apikey':key,'Accept':'application/json'}
 if not key.startswith('sb_publishable_'):headers['Authorization']='Bearer '+key
 request=Request(config['url']+'/rest/v1/rpc/'+RPC,headers=headers,method='GET')
 try:
  with build_opener(NoRedirect()).open(request,timeout=8) as r:raw=r.read(1024*1024+1)
 except HTTPError as e:
  reason='집계 RPC 미배포/스키마 캐시 미갱신' if e.code==404 else '공개키/집계 함수 EXECUTE 권한/배포 상태 확인 필요'
  raise ValueError(f'HTTP {e.code}: {reason}; 원시 SELECT나 관리자 키로 우회하지 않습니다.') from None
 except URLError as e:raise ValueError('집계 GET 연결 실패: '+str(e.reason).replace(key,'[redacted]')[:160]) from None
 except (TimeoutError,OSError):raise ValueError('집계 GET 시간 초과/연결 오류') from None
 if len(raw)>1024*1024:raise ValueError('집계 응답 크기 초과')
 try:return clean_summary(json.loads(raw))
 except (TypeError,KeyError,AttributeError,json.JSONDecodeError):raise ValueError('집계 응답 형식 불일치') from None

def clean_view(rows):
 out=[]
 for p in rows:
  boss=p.get('boss');pattern=p.get('pattern');group=p.get('group_name')
  if boss not in PATTERNS or pattern not in PATTERNS[boss] or group not in GROUPS:continue
  row={'boss':boss,'pattern':pattern,'group_name':group,'players':_count(p.get('players'),3),'opportunities':_count(p.get('opportunities'),20)}
  for field in ('success_rate','parry_rate','hit_rate'):row[field]=_quantity(p.get(field),0,1)
  if type(p.get('suggested_level')) is not int or p['suggested_level'] not in (-1,0,1):raise ValueError('기존 뷰 난이도 응답 오류')
  row['suggested_level']=p['suggested_level'];out.append(row)
 return out

class Database:
 def __init__(self,config_path):
  self.path=config_path;self.lock=threading.Lock();self.value={'status':'미조회','configured':False,'tables':{},'analysis':None,'writeStatus':'집계 조회는 게임 INSERT 성공을 증명하지 않습니다.'};self.busy=False;self.last=0
 def state(self):
  with self.lock:return json.loads(json.dumps(self.value))
 def refresh(self):
  with self.lock:
   if self.busy or time.monotonic()-self.last<5:return
   self.busy=True;self.last=time.monotonic();self.value['status']='조회 중'
  threading.Thread(target=self._read,daemon=True).start()
 def _read(self):
  statuses={};summary=None;view=None
  try:
   c=public_config(self.path)
   from concurrent.futures import ThreadPoolExecutor
   def one(name):
    try:
     rows=fetch_summary(c) if name==RPC else clean_view(fetch_table(c,name))
     return name,rows,None
    except Exception as e:return name,None,str(e) if isinstance(e,ValueError) else '집계 응답 검증 실패'
   with ThreadPoolExecutor(max_workers=2) as pool:
    for name,rows,error in pool.map(one,[RPC,'pattern_difficulty_by_proficiency']):
     statuses[name]={'status':'실패' if error else '집계 조회 성공','rows':None if error else len(rows.get('patterns',[])) if name==RPC else len(rows),'error':error}
     if name==RPC:summary=rows
     else:view=rows
   analysis=summary or {'counts':None,'groups':[],'patterns':[],'bosses':[]}
   analysis.update(mixedView=view or [],mixedViewLabel='기존 서버 뷰: 실제·발표용 혼합 가능 · 출처별 통계로 사용하지 않음')
   with self.lock:self.value={'status':'집계 조회 완료' if summary is not None and view is not None else '일부 집계 조회 실패' if summary is not None or view is not None else '집계 조회 실패','configured':True,'proficiencySync':c['proficiencySync'],'tables':statuses,'analysis':analysis,'aggregateAvailable':summary is not None,'lastAttemptUtc':utc(),'lastSelectUtc':utc() if summary is not None or view is not None else None,'liveSchema':'고정 v1 서버 집계 API · 원시 SELECT 없음 · 3소유자 미만 비공개 · 패턴 미표시는 기록 없음 또는 표본 부족','writeStatus':'집계 조회와 게임 송신은 별개입니다. 아래 LIVE 진단을 확인하세요.'}
  except Exception as e:
   with self.lock:self.value={'status':'설정 오류/집계 조회 불가','configured':False,'error':str(e) if isinstance(e,ValueError) else '설정 파일 없음/읽기 실패','tables':{},'analysis':None}
  finally:
   with self.lock:self.busy=False
