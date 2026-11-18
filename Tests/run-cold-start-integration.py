"""Compile real HTTP receive, CommunityStats and Prepare together; no HEADLESS source exclusion."""
import argparse,os,subprocess,tempfile
from pathlib import Path
from xml.sax.saxutils import escape
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dotnet',required=True);a=p.parse_args();r=Path(__file__).resolve().parents[1]
files=[r/'Assets/Scripts'/f for f in ['GameData.cs','GameServices.cs','CloudSync.cs','SaveStore.cs','TutorialSession.cs']]+[r/'Tests'/f for f in ['CloudSyncStatus/Stubs.cs','ColdStartIntegrationTests.cs','Contracts/TutorialAssessmentCandidate.cs']]
with tempfile.TemporaryDirectory(prefix='tower-cold-integration-') as tmp:
 w=Path(tmp);proj=w/'Tests.csproj';proj.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><DefineConstants>REAL_DDA;REAL_SAVE</DefineConstants><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'+''.join('<Compile Include="'+escape(str(f),{'"':'&quot;'})+'" />' for f in files)+'</ItemGroup></Project>');(w/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
 env=dict(os.environ,DOTNET_CLI_HOME=str(w/'cli'),NUGET_PACKAGES=str(w/'packages'),DOTNET_CLI_TELEMETRY_OPTOUT='1')
 raise SystemExit(subprocess.run([a.dotnet,'run','--project',str(proj),'--verbosity','quiet'],cwd=w,env=env).returncode)
