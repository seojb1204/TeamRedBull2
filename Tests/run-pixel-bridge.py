"""Compile real PixelAgentBridge.cs with a minimal API shim, not Unity rendering."""
import argparse,pathlib,tempfile,subprocess,os
p=argparse.ArgumentParser();p.add_argument('--dotnet',required=True);a=p.parse_args()
root=pathlib.Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='pixel-bridge-test-') as t:
 d=pathlib.Path(t);os.environ['DOTNET_CLI_HOME']=str(d);os.environ['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1';os.environ['DOTNET_CLI_TELEMETRY_OPTOUT']='1';(d/'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
 (d/'Test.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup>'+''.join(f'<Compile Include="{path}" />' for path in [root/'Assets/Scripts/PixelAgentBridge.cs',root/'Tests/PixelBridge/Program.cs'])+'</ItemGroup></Project>')
 subprocess.run([a.dotnet,'build',str(d/'Test.csproj'),'-o',str(d/'out'),'--verbosity','quiet'],check=True,cwd=d)
 exe=[a.dotnet,str(d/'out/Test.dll')]
 subprocess.run(exe+['--case','invalid','-towerPixelAgent',str(d/'invalid')],check=True)
 subprocess.run(exe+['-towerPixelAgent',str(d/'run/exchange'),'-towerDataRoot',str(d/'run'),'-towerAutoSeconds','180'],check=True)

 for case in ['oracle','dda','database']:
  subprocess.run(exe+['--case',case,'-towerPixelAgent',str(d/case/'exchange'),'-towerDataRoot',str(d/case),'-towerAutoSeconds','180'],check=True)
 subprocess.run(exe+['--case','outside-root','-towerPixelAgent',str(d/'outside'),'-towerDataRoot',str(d/'inside'),'-towerAutoSeconds','180'],check=True)
