"""Protect current-main gameplay and the exact reviewed selective source scope.
No DB configuration, user saves, CloudQueue, Unity launch or product file writes.
"""
from pathlib import Path
import subprocess,json,hashlib,re
r=Path(__file__).resolve().parents[1]
c=json.loads((r/'Tests/Contracts/SELECTIVE_SCOPE_20261119.json').read_text());baseline=c['baseline']
def git(*args):return subprocess.check_output(['git',*args],cwd=r,text=True).strip()
def old(path):return subprocess.check_output(['git','show',baseline+':'+path],cwd=r,text=True)
n=0
def check(ok,name):
 global n
 if not ok:raise AssertionError(name)
 n+=1;print('PASS',name)
check(git('rev-parse',baseline+'^{commit}')==baseline,'immutable full main baseline')
changed=set(git('diff','--name-only',baseline,'--','Assets','ProjectSettings').splitlines());changed|=set(git('ls-files','--others','--exclude-standard','--','Assets','ProjectSettings').splitlines())
check(changed<=set(c['files']),'only reviewed product scripts changed; no assets/scenes/prefabs/settings')
for name,meta in c['files'].items():check(hashlib.sha256((r/name).read_bytes()).hexdigest()==meta['sha256'],'exact reviewed bytes '+name)
check(not (r/'Tools/DdaLab').exists(),'Lab runtime excluded')
for name in ['LobbyCards.cs','ArakusAnimationSelection.cs','ArakusVisuals.cs','ActorView.cs','WorldArtist.cs','PhanesPose.cs','AutoRun.cs','PixelAgentBridge.cs','Soundscape.cs']:
 path='Assets/Scripts/'+name;check((r/path).read_text()==old(path),'unchanged latest main '+name)
def body(text,signature):
 # Match braces outside comments/strings, including verbatim string contents.
 start=text.index('{',text.index(signature));depth=0;i=start;state=None
 while i<len(text):
  ch=text[i];pair=text[i:i+2]
  if state=='line':
   if ch=='\n':state=None
  elif state=='block':
   if pair=='*/':state=None;i+=1
  elif state in ('"',"'"):
   if ch=='\\':i+=1
   elif ch==state:state=None
  elif pair=='//':state='line';i+=1
  elif pair=='/*':state='block';i+=1
  elif ch in ('"',"'"):state=ch
  elif ch=='{':depth+=1
  elif ch=='}':
   depth-=1
   if depth==0:return text[start:i+1]
  i+=1
 raise ValueError(signature)
methods={'TowerGame.cs':['void BeginAction(','void TickAction(','void HandleEscape(','bool QueueScreenChange(','void TickTransition('], 'TowerGame.Tutorial.cs':['void TickTutorialOpponent(','void TutorialActionStarted(','void RestoreTutorialProfile('], 'TutorialSession.cs':['void ResolveThreat(','void CompleteThreatFeedback('], 'BossPatternSystem.cs':['void ExecutePattern(','void FirePhanesStars('], 'GameData.cs':['double Reward(','void Update(','double Value('], 'SaveStore.cs':['string ResolveUploadOwner(','bool CommitCompletedBattle(']}
for name,signatures in methods.items():
 path='Assets/Scripts/'+name;a=old(path);b=(r/path).read_text()
 for sig in signatures:check(body(a,sig)==body(b,sig),'main method unchanged '+name+' '+sig)
# The settings method moved out of GameUI without substituting a test no-op.
check(body(old('Assets/Scripts/GameUI.cs'),'void CloseSettings(')==body((r/'Assets/Scripts/TowerGame.cs').read_text(),'void CloseSettings('),'exact real main settings-save logic retained')
check(body(old('Assets/Scripts/TutorialSession.cs'),'void Score(')==body((r/'Assets/Scripts/TutorialSession.cs').read_text(),'void Score(').replace('value.','Assessment.'),'existing tutorial scoring formula unchanged')
cloud=(r/'Assets/Scripts/CloudSync.cs').read_text();check('public bool proficiencySync;' in cloud and 'proficiencySync=true' not in cloud,'new telemetry defaults OFF')
shim=(r/'Tests/CloudSyncStatus/Stubs.cs').read_text();production=(r/'Assets/Scripts/BossPatternSystem.cs').read_text();constant=r'const string PhanesUltimate\s*=\s*"([^"]+)"';check(re.search(constant,shim).group(1)==re.search(constant,production).group(1),'HTTP catalog-only constant matches real Phanes constant')
print('SELECTIVE_PRESERVATION_PASS',n)
