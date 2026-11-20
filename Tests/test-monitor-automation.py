"""Compile exact production AutoRunArgs/F8-input slices, real CloudSync/SaveStore.
Slices only avoid unrelated Unity rendering dependencies; no body rewriting.
HTTP/Unity APIs are stubs. New process per scenario tests pre-Awake static flags.
"""
import argparse,hashlib,json,os,subprocess,sys,tempfile
from pathlib import Path
from xml.sax.saxutils import escape
root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root/'Tools/DdaLab'))
from runtime import select_sdk,pin_sdk
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet',required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--baseline-ref',help='Negative regression control: compile AutoRunArgs from this Git ref');a=p.parse_args()
out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
sdk=select_sdk(a.dotnet)
files=[root/'Assets/Scripts'/(n+'.cs') for n in ['GameData','GameServices','SaveStore','TutorialSession','CloudSync']]+[root/'Tests/CloudSyncStatus/Stubs.cs',root/'Tests/MonitorAutomationTests.cs']
auto=root/'Assets/Scripts/AutoRun.cs';panel=root/'Assets/Scripts/DdaPanel.cs'
manifest={'sdk':sdk,'source_sha256':{str(f.relative_to(root)):hashlib.sha256(f.read_bytes()).hexdigest() for f in files+[auto,panel]},'cases':{},'limits':'Actual production class/method slices; mocked Unity events/coroutines/HTTP, not Unity rendering or real network'}
with tempfile.TemporaryDirectory(prefix='monitor-automation-') as tmp:
 w=Path(tmp);pin_sdk(w,sdk)
 source=subprocess.check_output(['git','show',a.baseline_ref+':Assets/Scripts/AutoRun.cs'],cwd=root).decode('utf-8') if a.baseline_ref else auto.read_text(encoding='utf-8');manifest['classification_input_sha256']=hashlib.sha256(source.encode('utf-8')).hexdigest();manifest['baseline_ref']=a.baseline_ref;source=source[:source.index('    // Opt-in QA evidence.')]+ '}\n'
 (w/'AutoRunArgs.cs').write_text(source,encoding='utf-8')
 source=panel.read_text(encoding='utf-8');source=source[source.index('        void DdaPanelInput()'):source.index('        // 결과 화면에서 한 번만 계산한다')]
 (w/'PanelInput.cs').write_text('using UnityEngine;namespace ReversalTower{public partial class TowerGame{\n'+source+'}}',encoding='utf-8')
 manifest['compiled_slice_sha256']={n:hashlib.sha256((w/n).read_bytes()).hexdigest() for n in ['AutoRunArgs.cs','PanelInput.cs']}
 (w/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>',encoding='utf-8')
 (w/'QA.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><UseAppHost>false</UseAppHost><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>REAL_DDA;REAL_SAVE;REAL_AUTORUN</DefineConstants></PropertyGroup><ItemGroup>'+''.join('<Compile Include="'+escape(str(f),{'"':'&quot;'})+'" />' for f in files+[w/'AutoRunArgs.cs',w/'PanelInput.cs'])+'</ItemGroup></Project>',encoding='utf-8')
 env=dict(os.environ,DOTNET_CLI_HOME=str(w/'cli'),NUGET_PACKAGES=str(w/'packages'),DOTNET_PROCESSOR_COUNT='1',DOTNET_CLI_TELEMETRY_OPTOUT='1')
 build=subprocess.run([sdk['executable'],'build',str(w/'QA.csproj'),'--output',str(w/'out'),'--verbosity','quiet'],cwd=w,env=env,capture_output=True,text=True,encoding='utf-8',errors='replace');(out/'build.log').write_text(build.stdout+build.stderr,encoding='utf-8')
 if build.returncode:print(build.stdout+build.stderr);raise SystemExit(build.returncode)
 cases={'automation':['-towerDataRoot',str(w/'isolated')],'automation-default-root':['-towerNoDda'],'ordinary':[],'monitor':['-towerDdaMonitor',str(w/'monitor')],'mixed':['-towerDdaMonitor',str(w/'monitor'),'-towerDataRoot',str(w/'isolated')],'mixed-reversed':['-towerDataRoot',str(w/'isolated'),'-towerDdaMonitor',str(w/'monitor')]}
 for name,flags in cases.items():
  command=[sdk['executable'],str(w/'out/QA.dll'),*flags]
  result=subprocess.run(command,cwd=w,env=env,capture_output=True,text=True,encoding='utf-8',errors='replace');(out/(name+'.log')).write_text(result.stdout+result.stderr,encoding='utf-8');manifest['cases'][name]={'command':command,'exit':result.returncode,'tail':result.stdout.splitlines()[-1:]};print(name,result.returncode,result.stdout.splitlines()[-1:] if result.returncode==0 else result.stdout+result.stderr,flush=True)
ordinary=(out/'ordinary.log').read_text(encoding='utf-8');monitor=(out/'monitor.log').read_text(encoding='utf-8')
http=lambda text:next(line for line in text.splitlines() if line.startswith('HTTP_SEQUENCE '))
manifest['ordinary_monitor_http_identical']=http(ordinary)==http(monitor) if all(c['exit']==0 for c in manifest['cases'].values()) else False
if not a.baseline_ref:assert manifest['ordinary_monitor_http_identical']
manifest['sources_unchanged']=all(hashlib.sha256((root/f).read_bytes()).hexdigest()==h for f,h in manifest['source_sha256'].items())
(out/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
raise SystemExit(any(c['exit'] for c in manifest['cases'].values()) or not manifest['sources_unchanged'])
