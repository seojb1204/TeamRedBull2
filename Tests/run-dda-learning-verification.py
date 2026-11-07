"""Run unchanged production sources from main or a read-only PR worktree, without DB/config access."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--source', type=Path, default=Path(__file__).resolve().parents[1])
parser.add_argument('--label', required=True)
parser.add_argument('--output', type=Path, required=True)
parser.add_argument('--dotnet', default=shutil.which('dotnet'))
parser.add_argument('--suite', choices=['verification', 'existing'], default='verification')
args = parser.parse_args()
if not args.dotnet:
    parser.error('A .NET 8 SDK is required; pass --dotnet')
repo = Path(__file__).resolve().parents[1]
source = args.source.resolve()
output = args.output.resolve()
output.mkdir(parents=True, exist_ok=True)
names = ['GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
         'LobbySmoke', 'BossPatternSystem', 'GameServices']
files = [source / 'Assets' / 'Scripts' / (n + '.cs') for n in names]
files += [source / 'Tests' / 'UnityShim.cs']
files += [repo / 'Tests' / 'DdaLearningVerificationTests.cs' if args.suite == 'verification' else source / 'Tests' / 'HeadlessTests.cs']
manifest = {
    'label': args.label, 'source_commit': subprocess.check_output(['git', '-C', str(source), 'rev-parse', 'HEAD'], text=True).strip(),
    'suite': args.suite, 'target': 'net8.0', 'define': 'HEADLESS',
    'sources': {str(p.relative_to(source) if p.is_relative_to(source) else p.relative_to(repo)): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
    'excluded': ['CloudSync.cs', 'StreamingAssets', 'supabase.json', 'Unity Editor', 'real player saves/CloudQueue'],
}
with tempfile.TemporaryDirectory(prefix='dda-learning-execution-') as temp:
    work = Path(temp)
    project = work / 'Checks.csproj'
    project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
        '<TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
        '<DefineConstants>HEADLESS</DefineConstants></PropertyGroup><ItemGroup>'
        + ''.join('<Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />' for p in files)
        + '</ItemGroup></Project>')
    (work / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
    env = dict(os.environ, DOTNET_CLI_HOME=str(work / 'cli'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
               DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', NUGET_PACKAGES=str(work / 'packages'))
    command = [args.dotnet, 'run', '--project', str(project), '--verbosity', 'quiet']
    if args.suite == 'verification':
        command += ['--', args.label, str(output)]
    result = subprocess.run(command, cwd=work, env=env, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
    (output / (args.suite + '-run.log')).write_text(result.stdout)
    manifest['exit_code'] = result.returncode
    (output / (args.suite + '-manifest.json')).write_text(json.dumps(manifest, indent=2))
    print(result.stdout)
raise SystemExit(result.returncode)
