"""Portable Windows coordinator. No SDK/Git/PowerShell; no DB writes of its own."""
import argparse,hashlib,json,os,subprocess,sys,time,uuid,webbrowser
from pathlib import Path

class InstanceLock:
 def __init__(self,path):self.path=Path(path);self.file=None
 def acquire(self):
  self.file=self.path.open('a+b')
  if self.path.stat().st_size==0:self.file.write(b'0');self.file.flush()
  self.file.seek(0)
  try:
   if os.name=='nt':
    import msvcrt;msvcrt.locking(self.file.fileno(),msvcrt.LK_NBLCK,1)
   else:
    import fcntl;fcntl.flock(self.file.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
   return True
  except OSError:self.file.close();self.file=None;return False
 def close(self):
  if self.file:self.file.close();self.file=None

def paths(root,state):
 run=state/'runs'/uuid.uuid4().hex;run.mkdir(parents=True);live=run/'live';live.mkdir()
 return {'run':run,'live':live,'ready':run/'ready.json','game':root/'game/ReversalTower.exe','python':root/'runtime/python/python.exe','engine':root/'engine/DdaExperiment.exe','config':root/'game/ReversalTower_Data/StreamingAssets/supabase.json','lab':root/'lab/lab.py'}
def commands(p):
 return ([str(p['python']),'-X','utf8','-u',str(p['lab']),'--prebuilt',str(p['engine']),'--data-dir',str(p['run']/'experiments'),'--port','0','--auto-monitor-root',str(p['live']),'--ready-file',str(p['ready']),'--db-config',str(p['config'])], [str(p['game']),'-towerDdaMonitor',str(p['live'])])
def main():
 parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,default=Path(__file__).resolve().parent);a=parser.parse_args();root=a.root.resolve()
 state=Path(os.environ.get('LOCALAPPDATA',str(Path.home())))/'ReversalTowerExhibit'/hashlib.sha256(str(root).encode()).hexdigest()[:16];state.mkdir(parents=True,exist_ok=True)
 lock=InstanceLock(state/'instance.lock')
 if not lock.acquire():
  print('이 패키지 실행기가 이미 실행 중입니다. 기존 게임을 사용하세요.');return 0
 processes=[];logs=[]
 try:
  p=paths(root,state);labcmd,gamecmd=commands(p);print('진단/오류 로그:',p['run'],flush=True)
  if not p['game'].is_file():raise RuntimeError('게임 파일 없음: '+str(p['game']))
  # Game is launched independently, even if the viewer/runtime is missing or fails.
  game_log=(p['run']/'game-launch.log').open('w',encoding='utf-8');logs.append(game_log)
  game=subprocess.Popen(gamecmd,cwd=p['game'].parent,stdout=game_log,stderr=subprocess.STDOUT);processes.append(game)
  try:
   lab_log=(p['run']/'lab.log').open('w',encoding='utf-8');logs.append(lab_log)
   viewer=subprocess.Popen(labcmd,cwd=root,stdout=lab_log,stderr=subprocess.STDOUT);processes.append(viewer)
   deadline=time.monotonic()+35;url=None
   while time.monotonic()<deadline and viewer.poll() is None:
    if p['ready'].exists():
     try:
      data=json.loads(p['ready'].read_text(encoding='utf-8'));url=data['url']
      from urllib.parse import urlsplit
      u=urlsplit(url)
      if u.scheme!='http' or u.hostname!='127.0.0.1' or not u.port:raise ValueError('잘못된 로컬 서버 주소')
      break
     except (ValueError,OSError,KeyError):pass
    time.sleep(.1)
   if url:webbrowser.open(url);print('DDA Lab:',url,flush=True)
   else:print('Lab 시작 실패/지연. 게임은 계속 실행됩니다. lab.log를 확인하세요.',flush=True)
  except OSError as e:print('Lab 실행 실패. 게임은 계속 실행됩니다:',e,flush=True)
  print('게임 종료 후에도 DB/기록 화면은 유지됩니다. 모두 종료하려면 이 창에서 Ctrl+C.',flush=True)
  while any(proc.poll() is None for proc in processes):time.sleep(.5)
  return 0
 except KeyboardInterrupt:return 0
 except Exception as e:
  print('실행 오류:',e,flush=True);return 1
 finally:
  for proc in processes:
   if proc.poll() is None:
    proc.terminate()
    try:proc.wait(timeout=5)
    except subprocess.TimeoutExpired:proc.kill();proc.wait()
  for log in logs:log.close()
  lock.close()
if __name__=='__main__':
 for stream in (sys.stdout,sys.stderr):
  if hasattr(stream,'reconfigure'):stream.reconfigure(encoding='utf-8',errors='replace')
 raise SystemExit(main())
