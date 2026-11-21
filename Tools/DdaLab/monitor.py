"""Bounded, read-only local diagnostic observer. Never opens saves/config/queues/keys.
File mtime is metadata, not event time. Only the new publisher's capturedUtc/sequence
can order live samples; old logs remain REPLAY even when their files were just updated.
"""
from __future__ import annotations
import collections,csv,datetime as dt,io,json,math,os,re,threading,time
from pathlib import Path

SCHEMA='tower.dda.live/1'; REPLAY='tower.dda.replay/1'
MAX_PACKET=262144; MAX_FILE=17*1024*1024; MAX_EXPORT=8*1024*1024
MAX_FRAMES=300; MAX_FILES=200; READ_BUDGET=512*1024; STALE=3.0
HEX=re.compile(r'^[a-f0-9]{32}$'); UUID=re.compile(r'^[a-f0-9-]{32,36}$')
def text(v,n=240):return v[:n] if isinstance(v,str) else ''
def number(v,default=None):
 return float(v) if isinstance(v,(int,float)) and not isinstance(v,bool) and math.isfinite(v) and abs(v)<=1e15 else default
def integer(v,default=0):
 x=number(v);return int(x) if x is not None and x==int(x) and abs(x)<=2147483647 else default
def timestamp(v):
 try:
  # .NET round-trip UTC emits 7 fractional digits; Python 3.10 accepts microseconds.
  iso=re.sub(r'(\.\d{6})\d+(?=[+-]\d{2}:\d{2}$)',r'\1',v.replace('Z','+00:00'))
  x=dt.datetime.fromisoformat(iso);return x.timestamp() if x.tzinfo else None
 except (ValueError,TypeError,AttributeError,OverflowError,OSError):return None
def utc():return dt.datetime.now(dt.timezone.utc).isoformat()
def trace(raw):
 if not isinstance(raw,dict):return None
 out={k:raw.get(k) is True for k in ['enabled','clear','abandoned']};out['boss']=text(raw.get('boss'),80);out['patterns']=[]
 for item in raw.get('patterns',[])[:64] if isinstance(raw.get('patterns'),list) else []:
  if not isinstance(item,dict):continue
  row={k:text(item.get(k)) for k in ['pattern','source','reason','nextReason','skipReason','aBefore','aAfter','bBefore','bAfter','priorGroup']}
  row.update({k:integer(item.get(k)) for k in ['assignedLevel','nextLevel','opportunities','parries','dodges','hits','ignored','lossBefore','lossAfter','observations','battles','priorOpportunities','priorPlayers']})
  row.update({k:number(item.get(k)) for k in ['aDelta','bDelta','reward','rememberedSuccess','observedSuccess']})
  row.update({k:item.get(k) is True for k in ['learned','candidateScoresUsed','priorMock']})
  for k in ['context','candidates','candidateScores']:row[k]=[number(x) for x in item.get(k,[])[:8]] if isinstance(item.get(k),list) else []
  if row['pattern'] and -2<=row['assignedLevel']<=2 and -2<=row['nextLevel']<=2:out['patterns'].append(row)
 return out

def frame(raw):
 if not isinstance(raw,dict) or raw.get('schema')!=SCHEMA:raise ValueError('진단 schema 불일치')
 sid=raw.get('sessionId');seq=raw.get('sequence');capture=raw.get('capturedUtc')
 if not isinstance(sid,str) or not HEX.fullmatch(sid) or type(seq)!=int or not 0<seq<2**53 or timestamp(capture) is None:raise ValueError('세션/순서/캡처 시각 오류')
 origin=raw.get('origin')
 if origin not in ['unity-editor','unity-player','headless-fixture']:raise ValueError('알 수 없는 진단 출처')
 run=text(raw.get('runId'),80)
 if run and not HEX.fullmatch(run):raise ValueError('전투 run ID 오류')
 out={k:text(raw.get(k),80) for k in ['runtime','boss','bossName','phase','kind','battleId','diagnosticSession']}
 if out['battleId'] and not UUID.fullmatch(out['battleId']):raise ValueError('전투 UUID 오류')
 out.update(schema=SCHEMA,sessionId=sid,runId=run,sequence=seq,capturedUtc=capture,origin=origin)
 out.update({k:raw.get(k) is True for k in ['running','dda','hasAttack','clear','abandoned','historyLimitReached']})
 out.update({k:max(0,integer(raw.get(k))) for k in ['dropped','writeErrors']});out['simulationSeconds']=number(raw.get('simulationSeconds'))
 c=raw.get('cloud');out['cloud']=None
 if isinstance(c,dict):
  out['cloud']={k:text(c.get(k),180) for k in ['configuration','legacyBattle','legacyPattern','assessment','proficiencyBattle','proficiencySamples','lastSuccessfulSendUtc','lastError']}
  out['cloud'].update(proficiencyEnabled=c.get('proficiencyEnabled') is True,pendingLegacy=integer(c.get('pendingLegacy'),-1),pendingProtected=integer(c.get('pendingProtected'),-1))
 out['patterns']=[]
 for p in raw.get('patterns',[])[:64] if isinstance(raw.get('patterns'),list) else []:
  if not isinstance(p,dict):continue
  row={k:text(p.get(k),80 if k in ('id','name') else 240) for k in ['id','name','source','reason']}
  row.update({k:integer(p.get(k)) for k in ['level','opportunities','parries','dodges','hits','ignored']})
  if not row['id'] or not -2<=row['level']<=2 or min(row[k] for k in ['opportunities','parries','dodges','hits','ignored'])<0:continue
  if row['parries']+row['dodges']+row['hits']!=row['opportunities']:continue
  out['patterns'].append(row)
 attack=raw.get('attack');out['attack']=None
 if out['hasAttack'] and out['phase']=='battle' and isinstance(attack,dict):
  a={k:text(attack.get(k)) for k in ['pattern','patternName','source','reason']};a['level']=integer(attack.get('level'),99)
  for k in ['windup','damage','cooldown','cooldownRemaining','windupScale','damageScale','cooldownScale']:a[k]=number(attack.get(k))
  if a['pattern'] and -2<=a['level']<=2 and all(a[k] is not None and a[k]>=0 for k in ['windup','damage','cooldown']):out['attack']=a
 out['hasAttack']=out['attack'] is not None
 t=raw.get('tutorial');out['tutorial']=None
 if isinstance(t,dict) and t.get('group') in ['novice','normal','expert']:
  t2={k:text(t.get(k),80) for k in ['step','group']}
  t2.update({k:number(t.get(k)) for k in ['score','confidence','active','elapsed','explanation','paused']})
  t2.update({k:max(0,integer(t.get(k))) for k in ['parryAttempts','parrySuccesses','dodgeAttempts','dodgeSuccesses','hits','retries','controls','mistakes']})
  t2.update({k:t.get(k) is True for k in ['completed','skipped','valid','first','ultimateHit']});out['tutorial']=t2
 out['selection']=[]
 for e in raw.get('selection',[])[:64] if isinstance(raw.get('selection'),list) else []:
  if not isinstance(e,dict):continue
  row={k:text(e.get(k)) for k in ['id','reason']};row.update({k:number(e.get(k)) for k in ['seconds','baseWeight','multiplier','weight','probability','success']});row.update({k:max(0,integer(e.get(k))) for k in ['opportunities','parries','dodges','hits','ignored']});row['decision']=integer(e.get('decision'));row['selected']=e.get('selected') is True
  if row['id'] and row['decision']>=0 and row['probability'] is not None and 0<=row['probability']<=1:out['selection'].append(row)
 out['learning']=trace(raw.get('learning'))
 return out

class Tail:
 def __init__(self,mode):self.mode=mode;self.offset=0;self.identity=None;self.buffer=b'';self.header=None
 def read(self,path):
  st=path.stat();identity=(st.st_dev,st.st_ino)
  if st.st_size>MAX_FILE:raise ValueError('진단 파일 크기 한도 초과')
  if self.identity!=identity or st.st_size<self.offset:self.offset=0;self.buffer=b'';self.header=None;self.identity=identity
  with path.open('rb') as f:f.seek(self.offset);chunk=f.read(READ_BUDGET)
  self.offset+=len(chunk);self.buffer+=chunk
  if len(self.buffer)>MAX_PACKET*2:self.buffer=b'';raise ValueError('불완전한 진단 행 크기 초과')
  # A line without its terminating newline is never treated as a completed record.
  end=self.buffer.rfind(b'\n')+1
  if not end:return []
  complete,self.buffer=self.buffer[:end],self.buffer[end:];lines=complete.decode('utf-8-sig',errors='strict').splitlines(keepends=True);out=[]
  if self.mode=='jsonl':
   for line in lines:
    try:
     if len(line.encode())<=MAX_PACKET:out.append(json.loads(line))
    except (ValueError,UnicodeError):pass
  else:
   consumed=0;reader=csv.reader(io.StringIO(''.join(lines)),strict=True)
   try:
    for row in reader:
     consumed=reader.line_num
     if self.header is None:self.header=row
     elif len(row)==len(self.header):out.append(dict(zip(self.header,row)))
   except csv.Error:
    self.buffer=''.join(lines[consumed:]).encode()+self.buffer
  return out

def csv_row(raw,kind):
 # Explicit field projection: no raw arbitrary file contents or unknown fields cross the API.
 row={k:text(raw.get(k),240) for k in ['boss','pattern','reason']}
 fields={'applied':['decision','assignedLevel','windup','damage','cooldown'],
         'decisions':['seconds','decision','opportunities','hits','parries','dodges','ignored','success','baseWeight','multiplier','weight','probability'],
         'battlelog':['seconds','level','opportunities','hits','parries','dodges','ignored']}[kind]
 for k in fields:
  try:v=float(raw.get(k,''));row[k]=v if math.isfinite(v) else None
  except (ValueError,TypeError):row[k]=None
 for k in ['selected','clear','dda','abandoned']:
  if k in raw:row[k]=str(raw[k]).lower()=='true'
 return row

class Monitor:
 def __init__(self,threaded=True):
  self.lock=threading.RLock();self.root=None;self.choice=None;self.catalog=[];self._generation=0;self._tails={};self._frames={};self._sizes={};self._results={};self.current=None
  self._received=None;self._monotonic=None;self._last_scan=0.;self._catalog_at=0.;self.warnings=collections.deque(maxlen=12);self.legacy={};self.replaying=False;self.browse_lock=threading.Lock()
  self.stop=threading.Event()
  if threaded:threading.Thread(target=self._loop,daemon=True,name='read-only-diagnostics').start()
 def _loop(self):
  while not self.stop.wait(.5):self.scan()
 def _safe(self,path):
  if self.root is None:raise ValueError('선택한 폴더 없음')
  path=Path(path);resolved=path.resolve()
  if not resolved.is_relative_to(self.root):raise ValueError('진단 경계 밖 경로 거부')
  for part in [path,*path.parents]:
   if part==self.root:break
   if part.is_symlink():raise ValueError('진단 symlink 거부')
  return path
 def _files(self,directory):
  if not directory.is_dir():return []
  self._safe(directory);out=[]
  with os.scandir(directory) as scan:
   for i,e in enumerate(scan):
    if i>=MAX_FILES:self.warnings.append('파일 목록 한도 도달 · 더 구체적인 세션 폴더를 선택하세요');break
    if not e.is_symlink():out.append(Path(e.path))
  return out
 def _catalog(self):
  found=[]
  if (self.root/'latest.json').is_file():found.append(dict(id='live:'+self.root.name,kind='live',path=self.root,label='직접 선택한 진단 세션'))
  for p in self._files(self.root/'DdaLive'):
   if HEX.fullmatch(p.name) and p.is_dir() and (p/'latest.json').is_file():found.append(dict(id='live:'+p.name,kind='live',path=p,label='진단 '+p.name[:8]+' · 파일갱신 '+dt.datetime.fromtimestamp((p/'latest.json').stat().st_mtime,dt.timezone.utc).strftime('%m-%d %H:%M:%S UTC')))
  legacy={}
  for folder,kind,ext in [('DdaApplied','applied','.csv'),('DdaDecisions','decisions','.csv'),('DdaLearning','learning','.json'),('Battles','battlelog','.csv')]:
   for p in self._files(self.root/folder):
    if p.is_file() and p.suffix==ext and re.fullmatch(r'[A-Za-z0-9_-]{1,96}',p.stem):
     key=('legacy:'+p.stem) if kind in ('applied','decisions') else kind+':'+p.stem
     item=legacy.setdefault(key,dict(id=key,kind='legacy',paths={},label=('선택/적용 기록 ' if kind in ('applied','decisions') else '종료 학습 ' if kind=='learning' else '종료 전투 ')+p.stem[:36]));item['paths'][kind]=p
  found.extend(legacy.values());return sorted(found,key=lambda x:x['id'])
 def connect(self,root,choice=None):
  if not isinstance(root,str) or len(root)>4096:raise ValueError('폴더 경로 오류')
  if choice is not None and (not isinstance(choice,str) or len(choice)>160):raise ValueError('세션 선택 오류')
  p=Path(root).expanduser()
  if not p.is_absolute() or p.is_symlink() or not p.is_dir():raise ValueError('존재하는 절대 경로 폴더를 직접 선택하세요')
  with self.lock:
   self.root=p.resolve();self.choice=choice;self.catalog=[];self._generation+=1;self._tails={};self._frames={};self._sizes={};self._results={};self.current=None;self._received=None;self._monotonic=None;self.legacy={};self.replaying=False;self._catalog_at=0;self.warnings.clear()
  self.scan();return self.state()
 def disconnect(self):
  with self.lock:self.root=None;self.catalog=[];self.choice=None;self.current=None;self.legacy={};self._frames={};self._sizes={};self._results={};self._received=None;self._monotonic=None;self.replaying=False;self._tails={}
 def _accept(self,data,sid):
  f=frame(data)
  if f['sessionId']!=sid:raise ValueError('다른 세션 데이터 거부')
  if self.current and f['origin']!=self.current['origin']:raise ValueError('한 세션의 진단 출처 변경 거부')
  if f['sequence'] in self._frames:return
  self._frames[f['sequence']]=f;self._sizes[f['sequence']]=len(json.dumps(f,ensure_ascii=False).encode())
  while len(self._frames)>MAX_FRAMES or sum(self._sizes.values())>4*1024*1024:
   key=min(self._frames);del self._frames[key];self._sizes.pop(key,None)
  if self.current is None or f['sequence']>self.current['sequence']:
   self.current=f;self._received=utc();self._monotonic=time.monotonic()
  if f['learning'] and f['runId'] and f['sequence']>self._results.get(f['runId'],{}).get('sequence',0):
   self._results[f['runId']]=f
   while len(self._results)>60 or sum(len(json.dumps(x,ensure_ascii=False).encode()) for x in self._results.values())>3*1024*1024:del self._results[next(iter(self._results))]
 def scan(self):
  # One bounded reader owns all I/O. API calls only copy this bounded state.
  with self.lock:
   if self.root is None or self.replaying:return
   started=time.perf_counter()
   try:
    if time.monotonic()-self._catalog_at>=2:
     self.catalog=self._catalog();self._catalog_at=time.monotonic()
    choices=[x for x in self.catalog if x['id']==self.choice]
    if not self.choice:
     live=[x for x in self.catalog if x['kind']=='live']
     if len(live)==1:self.choice=live[0]['id'];choices=live
     elif not live and len(self.catalog)==1:self.choice=self.catalog[0]['id'];choices=self.catalog
    if not choices:return
    choice=choices[0]
    if choice['kind']=='live':
     latest=self._safe(choice['path']/'latest.json');sid=choice['path'].name
     if not HEX.fullmatch(sid):raise ValueError('진단 세션 폴더 이름 오류')
     if latest.stat().st_size>MAX_PACKET:raise ValueError('스냅샷 크기 초과')
     self._accept(json.loads(latest.read_text(encoding='utf-8-sig')),sid)
     history=self._safe(choice['path']/'events.jsonl')
     if history.is_file():
      tail=self._tails.setdefault(str(history),Tail('jsonl'))
      for raw in tail.read(history):
       try:self._accept(raw,sid)
       except (ValueError,TypeError):self.warnings.append('손상된 이벤트 한 행 제외')
    else:
     for kind,path in choice['paths'].items():
      path=self._safe(path)
      if kind=='learning':
       if path.stat().st_size>1024*1024:raise ValueError('학습 기록 크기 초과')
       self.legacy['learning']=trace(json.loads(path.read_text(encoding='utf-8-sig')))
      else:
       tail=self._tails.setdefault(str(path),Tail('csv'));rows=self.legacy.setdefault(kind,[])
       rows.extend(csv_row(raw,kind) for raw in tail.read(path));del rows[:-MAX_FRAMES]
      self.legacy['fileModifiedUtc']=dt.datetime.fromtimestamp(path.stat().st_mtime,dt.timezone.utc).isoformat()
   except (OSError,ValueError,UnicodeError,TypeError,OverflowError) as e:
    msg='진단 읽기 지연/손상 · 마지막 정상 표본 유지' if not isinstance(e,ValueError) else text(str(e),100)
    if not self.warnings or self.warnings[-1]!=msg:self.warnings.append(msg)
   finally:self._last_scan=(time.perf_counter()-started)*1000
 def state(self,since=0):
  with self.lock:
   f=self.current;age=None;status='DISCONNECTED';source='연결 없음'
   if self.replaying:status='REPLAY';source='저장된 실제/시험 진단 기록 · 실시간 아님'
   elif f:
    stamp=timestamp(f['capturedUtc']);age=time.time()-stamp if stamp is not None else None
    fresh=age is not None and -5<=age<=STALE and self._monotonic is not None and time.monotonic()-self._monotonic<=STALE
    if f['origin']=='headless-fixture':status='FIXTURE';source='HEADLESS 시험 출력 · 실제 Unity 아님'
    elif f['running'] and fresh:status='LIVE GAME';source='Unity Editor' if f['origin']=='unity-editor' else 'Unity Player'
    else:source='게임 heartbeat 중단/오래된 기록'
   elif self.legacy:status='REPLAY';source='기존 파일 기록 · 실시간 연결 확인 불가'
   elif self.root and not self.choice:source='세션을 선택하세요' if self.catalog else '진단 기록 대기'
   # Deep copy via JSON so clients cannot mutate observer state.
   return json.loads(json.dumps(dict(status=status,source=source,root=str(self.root) if self.root else None,choice=self.choice,sessions=[{k:x[k] for k in ['id','kind','label']} for x in self.catalog],frame=f,frames=[self._frames[x] for x in sorted(self._frames) if x>since],results=[x for x in self._results.values() if x['sequence']>since],legacy=self.legacy,warnings=list(self.warnings),receivedUtc=self._received,capturedAgeSeconds=age,scanMilliseconds=self._last_scan,limits=dict(frames=MAX_FRAMES,files=MAX_FILES,readBytesPerFilePerPoll=READ_BUDGET,staleSeconds=STALE)),allow_nan=False))
 def export(self):
  with self.lock:
   data=dict(schema=REPLAY,sessionId=self.current['sessionId'] if self.current else None,frames=[self._frames[x] for x in sorted(self._frames)],results=list(self._results.values()),legacy=self.legacy)
   if len(json.dumps(data,ensure_ascii=False).encode())>MAX_EXPORT:raise ValueError('내보내기 한도 초과')
   return data
 def load_replay(self,data):
  if not isinstance(data,dict) or data.get('schema')!=REPLAY:raise ValueError('진단 REPLAY 형식이 아닙니다')
  frames=data.get('frames',[]);results=data.get('results',[])
  if not isinstance(frames,list) or len(frames)>MAX_FRAMES or not isinstance(results,list) or len(results)>60:raise ValueError('재생 기록 수 한도 초과')
  clean=[frame(x) for x in frames];clean_results=[frame(x) for x in results]
  if len({x['sequence'] for x in clean})!=len(clean):raise ValueError('중복 순서의 재생 기록 거부')
  if len({x['origin'] for x in clean+clean_results})>1:raise ValueError('실게임과 시험 출처를 섞을 수 없음')
  if sum(len(json.dumps(x,ensure_ascii=False).encode()) for x in clean)>4*1024*1024 or sum(len(json.dumps(x,ensure_ascii=False).encode()) for x in clean_results)>3*1024*1024:raise ValueError('재생 메모리 한도 초과')
  sid=data.get('sessionId')
  if any(x['sessionId']!=sid for x in clean+clean_results):raise ValueError('다른 세션을 한 재생으로 합칠 수 없음')
  legacy={}
  for kind in ['applied','decisions','battlelog']:
   vals=(data.get('legacy') or {}).get(kind,[])
   if not isinstance(vals,list) or len(vals)>MAX_FRAMES:raise ValueError('기존 기록 수 한도 초과')
   # Normalized legacy export is whitelisted again. Preserve only known scalar fields.
   legacy[kind]=[{k:(text(v) if isinstance(v,str) else v) for k,v in x.items() if k in ['boss','pattern','reason','decision','assignedLevel','windup','damage','cooldown','seconds','level','opportunities','hits','parries','dodges','ignored','success','baseWeight','multiplier','weight','probability','selected','clear','dda','abandoned'] and isinstance(v,(str,int,float,bool,type(None))) and (not isinstance(v,float) or math.isfinite(v))} for x in vals if isinstance(x,dict)]
  legacy['learning']=trace((data.get('legacy') or {}).get('learning'))
  if not clean and not clean_results and not any(legacy.values()):raise ValueError('재생 가능한 기록 없음')
  with self.lock:
   self.disconnect();self.replaying=True;self._frames={x['sequence']:x for x in clean};self._sizes={x['sequence']:len(json.dumps(x,ensure_ascii=False).encode()) for x in clean};self._results={x['runId']:x for x in clean_results};self.current=max(clean+clean_results,key=lambda x:x['sequence']) if clean+clean_results else None;self.legacy=legacy;self.warnings.clear();self._received=None
  return self.state()
 def browse(self):
  if not self.browse_lock.acquire(blocking=False):raise ValueError('폴더 선택창이 이미 열려 있습니다')
  try:
   import tkinter as tk
   from tkinter import filedialog
   w=tk.Tk();w.withdraw()
   try:return filedialog.askdirectory(title='DDA 진단 폴더 선택 (읽기 전용)',parent=w) or None
   finally:w.destroy()
  except Exception:raise ValueError('이 환경에는 폴더 선택창이 없습니다. 절대 경로를 직접 입력하세요')
  finally:self.browse_lock.release()
