"""Run production combat and animation selection with headless doubles in an isolated temp directory.
No CloudSync, DB config, StreamingAssets, or real save folders are loaded.
"""
from pathlib import Path
import argparse,subprocess,tempfile,os,hashlib,json,shutil
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet',default=shutil.which('dotnet'));p.add_argument('--output',type=Path,required=True);a=p.parse_args()
if not a.dotnet:p.error('Pass a .NET8 SDK executable with --dotnet')
r=Path(__file__).resolve().parents[1];out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
names=['GameData','TowerGame','BossCombat','SaveStore','LobbyCards','AutoRun','LobbySmoke','BossPatternSystem','GameServices','ArakusAnimationSelection']
if (r/'Assets/Scripts/ArakusPatternTiming.cs').is_file():names.append('ArakusPatternTiming')
files=[r/'Assets/Scripts'/f'{n}.cs' for n in names]+[r/'Tests'/f'{n}.cs' for n in ['UnityShim','HeadlessTests','ArakusAnimationTests','ArakusSizeTests']]
manifest={'base_commit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip(),'source_sha256':{str(f.relative_to(r)):hashlib.sha256(f.read_bytes()).hexdigest() for f in files},'tests':{},'limits':['HEADLESS UnityShim, not Unity API compilation/rendering','No actual editor transforms or graphics/Windows build','No real DB or existing saves']}
with tempfile.TemporaryDirectory(prefix='arakus-size-') as tmp:
 t=Path(tmp)
 for f in files:shutil.copyfile(f,t/f.name)
 (t/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
 for name in ['HeadlessTests','ArakusAnimationTests','ArakusSizeTests']:
  project=t/f'{name}.csproj';project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><DefineConstants>HEADLESS</DefineConstants><StartupObject>'+name+'</StartupObject></PropertyGroup></Project>')
  result=subprocess.run([a.dotnet,'run','--project',str(project)],cwd=t,env=dict(os.environ,DOTNET_CLI_HOME=str(t/'cli'),DOTNET_CLI_TELEMETRY_OPTOUT='1'),stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
  (out/f'{name}.log').write_text(result.stdout);manifest['tests'][name]=result.returncode;print(name,result.returncode,result.stdout.splitlines()[-1] if result.stdout else '')
(out/'headless-manifest.json').write_text(json.dumps(manifest,indent=2))
raise SystemExit(any(manifest['tests'].values()))
