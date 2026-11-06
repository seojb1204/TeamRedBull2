"""Offline .NET 8+ runner. Uses only selected C# source and test doubles, never DB settings."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
from xml.sax.saxutils import escape


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', default=shutil.which('dotnet'))
    parser.add_argument('--suite', choices=['history', 'logic'], default='history')
    args = parser.parse_args()
    if not args.dotnet:
        parser.error('A .NET SDK is required; supply --dotnet /path/to/dotnet')
    repo = Path(__file__).resolve().parents[1]
    names = ['GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
             'LobbySmoke', 'BossPatternSystem', 'GameServices', 'DdaDifficultyHistory',
             'DdaDifficultyHistoryStore', 'DdaHistoryRecorder']
    entry = 'DdaDifficultyHistoryTests' if args.suite == 'history' else 'HeadlessTests'
    sources = [repo / 'Assets' / 'Scripts' / (name + '.cs') for name in names]
    sources += [repo / 'Tests' / 'UnityShim.cs', repo / 'Tests' / (entry + '.cs')]
    with tempfile.TemporaryDirectory(prefix='tower-dda-tests-') as folder:
        work = Path(folder)
        project = work / 'Tests.csproj'
        project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
            '<DefineConstants>HEADLESS</DefineConstants><Nullable>disable</Nullable>'
            '</PropertyGroup><ItemGroup>' + ''.join('<Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />' for p in sources)
            + '</ItemGroup></Project>', encoding='utf-8')
        # No restore network or credentials. SDK reference assemblies are already local.
        (work / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
        env = dict(os.environ, DOTNET_CLI_HOME=str(work / 'cli'), DOTNET_CLI_TELEMETRY_OPTOUT='1',
                   DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', NUGET_PACKAGES=str(work / 'packages'))
        return subprocess.run([args.dotnet, 'run', '--project', str(project), '--verbosity', 'quiet'], cwd=work, env=env).returncode


if __name__ == '__main__':
    raise SystemExit(main())
