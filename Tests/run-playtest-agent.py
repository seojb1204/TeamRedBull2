"""Offline source allowlist runner: no CloudSync, real DB configuration or Unity Editor."""
import argparse
import hashlib
import json
import datetime
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet', default=shutil.which('dotnet'))
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
if a.output.exists() or a.output.with_suffix('.manifest.json').exists():
    p.error('Refusing to overwrite existing results or manifest')
if not a.dotnet:
    p.error('Install a .NET 8 SDK or supply --dotnet')
repo = Path(__file__).resolve().parents[1]
names = ['TutorialSession', 'TowerGame.Tutorial', 'GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
         'LobbySmoke', 'BossPatternSystem', 'GameServices', 'ArakusPatternTiming', 'ArakusAnimationSelection']
entry = 'PlaytestAgentTests'
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
    result = subprocess.run([a.dotnet, 'run', '--project', str(work / 'Tests.csproj'), '--verbosity', 'quiet', '--', str(a.output.resolve())], cwd=work, env=env).returncode
    manifest = {'schema': 'towerLogicQaManifest/1', 'track': 'logic-qa/UnityShim',
                'osKeyboard': False, 'renderedPerception': False, 'exitCode': result,
                'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                'sourcesSha256': {str(s.relative_to(repo)): hashlib.sha256(s.read_bytes()).hexdigest() for s in sources}}
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.with_suffix('.manifest.json').write_text(json.dumps(manifest, indent=2))
    raise SystemExit(result)
