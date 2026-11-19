"""Reconstruct exact 3342884 gameplay by removing ONLY the explicit diagnostic hooks.
Fail on any additional policy, input, save, scene or balance modification.
"""
from pathlib import Path
import subprocess
r=Path(__file__).resolve().parents[1];base='3342884db5f30526d399baae6bf806ee70572b24'
def old(name):return subprocess.check_output(['git','show',base+':Assets/Scripts/'+name+'.cs'],cwd=r,text=True)
def now(name):return (r/'Assets/Scripts'/(name+'.cs')).read_text()
def normalized(s):return '\n'.join(x.rstrip() for x in s.splitlines() if x.strip())
n=0
def check(x,label):
 global n
 if not x:raise AssertionError(label)
 n+=1;print('PASS',label)
for name in ['GameData','DdaPanel','DdaHistoryGraph','GameUI','NativeGameUI','ActorView','WorldArtist','ArakusAnimationSelection','ArakusVisuals','Soundscape','CloudSync','AutoRun']:
 check(now(name)==old(name),'exact baseline '+name)
for name in ['SaveStore','GameServices']:check(now(name).startswith(old(name)),'all original '+name+' code unchanged; diagnostics append only')
s=now('BossPatternSystem').replace('        public string DiagnosticSession=>diagnosticSession;\n','').replace('            g.PublishDdaMonitor("attack-applied");\n','');check(s==old('BossPatternSystem'),'exact boss mechanics after removing read-only hook')
s=now('TowerGame.Tutorial').replace('ddaMonitorTutorialRun=Guid.NewGuid().ToString("N");','');check(s==old('TowerGame.Tutorial'),'exact tutorial mechanics after removing diagnostic run ID')
s=now('TowerGame');begin=s.index('        DdaMonitorSink ddaMonitor;');end=s.rfind('\n    }');diagnostic=s[begin:end];s=s[:begin]+s[end:]
s=s.replace('DdaSnapshot(bool includeStorageStatus=true)','DdaSnapshot()').replace('uploadStatus=includeStorageStatus?SaveStore.ProficiencyStatus:"미수집",','uploadStatus=SaveStore.ProficiencyStatus,').replace('            StartDdaMonitorFromArguments();\n','').replace('            TickDdaMonitor();\n','').replace('            PublishDdaMonitor("battle-ended");\n','').replace('SaveStore.Save(save);StopDdaMonitor();}','SaveStore.Save(save);}')
check(normalized(s)==normalized(old('TowerGame')),'all TowerGame logic identical after exact diagnostic hook removal')
check('SaveStore.' not in diagnostic and '.Choose(' not in diagnostic and '.Update(' not in diagnostic and '.Complete(' not in diagnostic,'publisher cannot save or decide/learn')
check('DdaSnapshot(false)' in diagnostic,'publisher skips storage I/O read model')
changed=subprocess.check_output(['git','diff','--name-only',base,'--','Assets','ProjectSettings'],cwd=r,text=True).splitlines();check(set(changed)<=set('Assets/Scripts/'+x+'.cs' for x in ['GameServices','SaveStore','BossPatternSystem','TowerGame','TowerGame.Tutorial']),'only five diagnostic product files; assets/settings untouched')
print('LIVE_MONITOR_PRESERVATION_PASS',n)
