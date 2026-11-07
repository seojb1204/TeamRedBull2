"""Offline source allowlist runner: no CloudSync, real DB configuration or Unity Editor."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet', default=shutil.which('dotnet'))
p.add_argument('--suite', choices=['logic', 'animation', 'polish'], default='polish')
a = p.parse_args()
if not a.dotnet:
    p.error('Install a .NET 8 SDK or supply --dotnet')
repo = Path(__file__).resolve().parents[1]
names = ['GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
         'LobbySmoke', 'BossPatternSystem', 'GameServices', 'ArakusPatternTiming', 'ArakusAnimationSelection']
entry = {'logic': 'HeadlessTests', 'animation': 'ArakusAnimationTests', 'polish': 'ArakusChargePolishTests'}[a.suite]
sources = [repo / 'Assets' / 'Scripts' / (n + '.cs') for n in names]
sources += [repo / 'Tests' / 'UnityShim.cs', repo / 'Tests' / (entry + '.cs')]
with tempfile.TemporaryDirectory(prefix='arakus-polish-') as tmp:
    work = Path(tmp)
    (work / 'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
        '<EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>HEADLESS</DefineConstants>'
        '</PropertyGroup><ItemGroup>' + ''.join('<Compile Include="' + escape(str(s), {'"': '&quot;'}) + '" />' for s in sources)
        + '</ItemGroup></Project>')
    (work / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    env = dict(os.environ, DOTNET_CLI_HOME=str(work / 'cli'), DOTNET_CLI_TELEMETRY_OPTOUT='1', NUGET_PACKAGES=str(work / 'packages'))
    raise SystemExit(subprocess.run([a.dotnet, 'run', '--project', str(work / 'Tests.csproj'), '--verbosity', 'quiet'], cwd=work, env=env).returncode)
