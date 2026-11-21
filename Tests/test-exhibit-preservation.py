"""Exact pre-edit main reconstruction after removing only diagnostic projections."""
from pathlib import Path
import subprocess,json,hashlib
ROOT=Path(__file__).resolve().parents[1];BASE='f0a0da2e8a6d68875fc5b9aa698bf98bb541fb34'
def old(p):return subprocess.check_output(['git','show',BASE+':'+p],cwd=ROOT).decode('utf-8')
def now(p):return (ROOT/p).read_text(encoding='utf-8')
def check(value,label):
 if not value:raise AssertionError(label)
 print('PASS',label)
p='Assets/Scripts/TowerGame.cs';check(now(p).replace('runtime=runtime,cloud=DdaCloudDiagnostics.Snapshot(),','runtime=runtime,')==old(p),'entire TowerGame unchanged after exact diagnostic field removal')
p='Assets/Scripts/GameServices.cs';s=now(p);marker='\nnamespace ReversalTower\n{\n    // Diagnostic projection only:';i=s.index(marker);diagnostics=s[i:];s=s[:i].rstrip()+'\n';s=s.replace('public DdaTraceReport learning;public DdaCloudStatus cloud;','public DdaTraceReport learning;');check(s==old(p),'all DDA/LinUCB/initial-difficulty/reward logic identical')
check(all(x not in diagnostics for x in ['SaveStore','UnityWebRequest','System.IO','.Choose(','.Update(']),'new telemetry cannot save, network, or decide DDA')
p='Assets/Scripts/CloudSync.cs';s=now(p)
s=s.replace('DdaCloudDiagnostics.Configuration("자동검증: 네트워크 꺼짐");','').replace('DdaCloudDiagnostics.Configuration("설정 없음/유효하지 않음");','').replace('            DdaCloudDiagnostics.Configuration("클라이언트 설정 확인 · 전송 성공은 별도",config.proficiencySync);\n','')
s=s.replace('DdaCloudDiagnostics.Pending(files.Length);','').replace('DdaCloudDiagnostics.Pending(left,config.proficiencySync&&Directory.Exists(SaveStore.ProficiencyQueueRoot)?Directory.GetFiles(SaveStore.ProficiencyQueueRoot,"*.json").Length:-1);','')
s=s.replace('                DdaCloudDiagnostics.Sent(table,ok,req.responseCode);\n','').replace('                    DdaCloudDiagnostics.Sent(e.kind,req.result==UnityWebRequest.Result.Success&&req.downloadHandler.text.Trim()=="true",req.responseCode);\n','')
check(s==old(p),'all CloudSync upload/ownership/retry/acknowledgement code identical after hooks removed')
p='Assets/StreamingAssets/supabase.json';a=json.loads(old(p));b=json.loads(now(p));check(b.get('proficiencySync') is True,'requested proficiencySync true');check({k:v for k,v in a.items() if k!='proficiencySync'}=={k:v for k,v in b.items() if k!='proficiencySync'},'all original URL/key/config values preserved (not printed)')
allowed={'Assets/Scripts/TowerGame.cs','Assets/Scripts/GameServices.cs','Assets/Scripts/CloudSync.cs','Assets/StreamingAssets/supabase.json','Assets/Resources/ArakusNew/Death.png.meta'}
changed=set(subprocess.check_output(['git','diff','--name-only',BASE,'--','Assets','Packages','ProjectSettings'],cwd=ROOT,text=True).splitlines());check(changed<=allowed,'no unrelated game/art/settings edits')
check((ROOT/'Assets/Resources/ArakusNew/Death.png.meta').read_text(encoding='utf-8')==old('Assets/Resources/ArakusNew/Death.png.meta').replace('\r\n','\n'),'unrelated Death meta content unchanged across CRLF/LF checkouts')
print('EXHIBIT_PRESERVATION_PASS 8')
