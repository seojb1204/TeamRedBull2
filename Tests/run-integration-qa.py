"""Selected DDA/current-main crossing tests. Actual C# code, isolated roots, no real HTTP or Lab runtime.
Every result is logged; exit status and source hashes are retained. No runtime source rewriting.
"""
import argparse,hashlib,json,os,subprocess,tempfile
from pathlib import Path
from xml.sax.saxutils import escape
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet',required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--suite',choices=['all','tutorial-state','tutorial-save','tutorial-dda','queue','adaptation','proficiency-http','selective'],default='all');a=p.parse_args()
r=Path(__file__).resolve().parents[1];out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
base=['TutorialSession','TowerGame.Tutorial','GameData','TowerGame','BossCombat','SaveStore','LobbyCards','AutoRun','LobbySmoke','BossPatternSystem','GameServices','ArakusPatternTiming','ArakusAnimationSelection']
suites={
 'tutorial-state':([r/'Assets/Scripts/TutorialSession.cs',r/'Tests/TutorialSession/Program.cs'],'Program',''),
 'tutorial-save':([r/'Assets/Scripts'/(n+'.cs') for n in base]+[r/'Tests/UnityShim.cs',r/'Tests/TutorialPersistenceTests.cs'],'TutorialPersistenceTests','HEADLESS'),
 'tutorial-dda':([r/'Assets/Scripts'/(n+'.cs') for n in base]+[r/'Tests/UnityShim.cs',r/'Tests/TutorialDdaIntegrationTests.cs'],'TutorialDdaIntegrationTests','HEADLESS'),
 'queue':([r/'Assets/Scripts'/(n+'.cs') for n in ['GameData','GameServices','SaveStore','TutorialSession','CloudSync']]+[r/'Tests/CloudSyncStatus/Stubs.cs',r/'Tests/CloudQueueIntegrationTests.cs'],'CloudQueueIntegrationTests','REAL_DDA;REAL_SAVE')}
suites['adaptation']=([r/'Assets/Scripts'/(n+'.cs') for n in base]+[r/'Tests/UnityShim.cs',r/'Tests/AdaptationColdStartTests.cs'],'AdaptationColdStartTests','HEADLESS')
suites['proficiency-http']=([r/'Assets/Scripts'/(n+'.cs') for n in ['GameData','GameServices','SaveStore','TutorialSession','CloudSync']]+[r/'Tests/CloudSyncStatus/Stubs.cs',r/'Tests/ProficiencyHttpTests.cs'],'ProficiencyHttpTests','REAL_DDA;REAL_SAVE')
suites['selective']=([r/'Assets/Scripts'/(n+'.cs') for n in base]+[r/'Tests/UnityShim.cs',r/'Tests/SelectiveIntegrationTests.cs'],'SelectiveIntegrationTests','HEADLESS')
results={}
for name,(files,start,symbols) in suites.items():
 if a.suite not in ('all',name):continue
 if not all(f.is_file() for f in files):raise FileNotFoundError('Missing source in '+name)
 hashes={str(f.relative_to(r)):hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
 with tempfile.TemporaryDirectory(prefix='tower-cross-qa-') as tmp:
  w=Path(tmp);project=w/'QA.csproj';project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>'+symbols+'</DefineConstants><StartupObject>'+start+'</StartupObject></PropertyGroup><ItemGroup>'+''.join('<Compile Include="'+escape(str(f),{'"':'&quot;'})+'" />' for f in files)+'</ItemGroup></Project>');(w/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
  env=dict(os.environ,DOTNET_CLI_HOME=str(w/'cli'),NUGET_PACKAGES=str(w/'packages'),DOTNET_CLI_TELEMETRY_OPTOUT='1')
  command=[a.dotnet,'run','--project',str(project),'--verbosity','quiet'];res=subprocess.run(command,cwd=w,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
  (out/(name+'.log')).write_text(res.stdout);stable=hashes=={str(f.relative_to(r)):hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
  results[name]={'exit':res.returncode,'source_sha256':hashes,'sources_unchanged':stable,'command':command,'tail':res.stdout.splitlines()[-4:]};print(name,res.returncode,results[name]['tail'],flush=True)
(out/'manifest.json').write_text(json.dumps({'commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip(),'suites':results,'limits':['HEADLESS or mocked Unity/HTTP, not Unity API/runtime or real network','No production DB, credentials, personal saves/queues','Tutorial groups are not calibrated human skill probabilities']},indent=2))
raise SystemExit(any(v['exit'] or not v['sources_unchanged'] for v in results.values()))
