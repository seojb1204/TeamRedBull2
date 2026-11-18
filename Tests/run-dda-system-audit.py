"""Offline production-C# audit. No Unity, real DB settings, personal saves or queue.

Require a new output directory so old results cannot be mistaken for this run.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from xml.sax.saxutils import escape

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet', default=shutil.which('dotnet'))
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
if not a.dotnet:
    p.error('A .NET 8 SDK is required')
r = Path(__file__).resolve().parents[1]
out = a.output.resolve()
out.mkdir(parents=True, exist_ok=False)
results = {}
base = ['TutorialSession', 'TowerGame.Tutorial', 'GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
        'LobbySmoke', 'BossPatternSystem', 'GameServices', 'ArakusPatternTiming',
        'ArakusAnimationSelection']
files = [r / 'Assets/Scripts' / (n + '.cs') for n in base]
files += [r / 'Assets/Scripts/CloudSync.cs']
files += list((r / 'Tests').glob('*.cs')) + list((r / 'Tests').glob('*.py'))
files += list((r / 'Tests/CloudSyncStatus').glob('*.cs'))
def hashes():
    return {str(f.relative_to(r)): hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
initial_hashes = hashes()


def run(name, command, cwd=r, env=None):
    result = subprocess.run(command, cwd=cwd, env=env, text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (out / (name + '.log')).write_text(result.stdout)
    results[name] = {'exit_code': result.returncode, 'command': command,
                     'tail': result.stdout.splitlines()[-3:]}
    print(name, 'PASS' if result.returncode == 0 else 'FAIL', flush=True)
    return result.returncode


def compile_run(name, files, args=(), define=''):
    with tempfile.TemporaryDirectory(prefix='dda-system-audit-') as tmp:
        w = Path(tmp)
        project = w / 'Audit.csproj'
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
                          '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
                          '<EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                          f'<DefineConstants>{define}</DefineConstants></PropertyGroup><ItemGroup>'
                          + ''.join('<Compile Include="' + escape(str(f), {'"': '&quot;'}) + '" />' for f in files)
                          + '</ItemGroup></Project>')
        (w / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_HOME=str(w / 'cli'), NUGET_PACKAGES=str(w / 'packages'),
                   DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
        return run(name, [a.dotnet, 'run', '--project', str(project), '--verbosity', 'quiet', '--', *map(str, args)], w, env)


for repeat in (1, 2):
    run(f'learning-{repeat}', [sys.executable, 'Tests/run-dda-learning-verification.py',
        '--dotnet', a.dotnet, '--label', 'audit', '--output', str(out / f'learning-{repeat}')])
    compile_run(f'selection-{repeat}', [r / 'Assets/Scripts' / (n + '.cs') for n in base]
                + [r / 'Tests/UnityShim.cs', r / 'Tests/DdaSelectionAuditTests.cs'],
                [out / f'selection-{repeat}'], 'HEADLESS')
    run(f'combat-{repeat}', [sys.executable, 'Tests/run-playtest-agent.py', '--dotnet', a.dotnet,
        '--output', str(out / f'combat-{repeat}.csv')])
for suite in ('frequency', 'robustness'):
    run(suite, [sys.executable, f'Tests/run-dda-{suite}.py', '--dotnet', a.dotnet])
compile_run('cloud-receive', [r / 'Assets/Scripts/CloudSync.cs',
            r / 'Tests/CloudSyncStatus/Stubs.cs', r / 'Tests/CloudSyncStatus/Program.cs'])

comparisons = {}
for kind, filenames in [('learning', ['battles.csv'] + [f'{b}_{s}_final.json'
        for b in ('arakus', 'phanes', 'adicus', 'aethelos', 'kharos')
        for s in ('low_00', 'low_30', 'high_95', 'high_100', 'mid_60', 'mid_70', 'mid_80',
                  'improving_20_to_100', 'declining_100_to_20', 'loss_streak_high', 'fixed_off', 'mixed_by_pattern')]),
        ('selection', ['selection.csv', 'catalog.json'])]:
    for filename in filenames:
        one, two = (out / f'{kind}-{n}' / filename for n in (1, 2))
        comparisons[f'{kind}/{filename}'] = one.is_file() and two.is_file() and one.read_bytes() == two.read_bytes()
one, two = (out / f'combat-{n}.csv' for n in (1, 2))
comparisons['combat.csv'] = one.is_file() and two.is_file() and one.read_bytes() == two.read_bytes()

# Data parity check only: parse numeric fields of Unity's authored YAML and compare
# against Catalog exported by actual C#. No Python implementation of game policy.
parity = []
catalog_path = out / 'selection-1/catalog.json'
if catalog_path.exists():
    for boss in json.loads(catalog_path.read_text()):
        current = None
        for line in (r / 'Assets/Data/Bosses' / (boss['id'] + '.asset')).read_text().splitlines():
            if line.startswith('    - id: '):
                current = next(x for x in boss['patterns'] if x['id'] == line.split(': ', 1)[1])
            elif current is not None and line.startswith('      ') and ': ' in line:
                key, value = line.strip().split(': ', 1)
                if key in current and isinstance(current[key], (int, float, bool)):
                    try:
                        observed = float(value)
                    except ValueError:
                        continue
                    if abs(observed - float(current[key])) > 1e-5:
                        parity.append({'boss': boss['id'], 'pattern': current['id'], 'field': key,
                                       'asset': observed, 'catalog': current[key]})

frequency = {}
selection = out / 'selection-1/selection.csv'
if selection.exists():
    for row in csv.DictReader(selection.open()):
        key = '/'.join(row[k] for k in ('seed', 'dda', 'phase', 'distance', 'cooldown_mode', 'pattern'))
        record = frequency.setdefault(key, {'eligible': 0, 'selected': 0, 'expected_count': 0.0})
        record['eligible'] += row['eligible'] == 'True'
        record['selected'] += row['selected'] == 'True'
        record['expected_count'] += float(row['probability'])

final_hashes = hashes()
manifest = {'source_commit': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=r, text=True).strip(),
    'working_tree': subprocess.check_output(['git', 'status', '--short'], cwd=r, text=True).splitlines(),
    'source_sha256': initial_hashes, 'sources_unchanged_during_run': initial_hashes == final_hashes,
    'asset_sha256': {str(f.relative_to(r)): hashlib.sha256(f.read_bytes()).hexdigest()
                     for f in (r / 'Assets/Data/Bosses').glob('*.asset')},
    'seeds': [17, 42], 'target': 'net8.0', 'results': results,
    'deterministic_policy_outputs': comparisons, 'catalog_asset_numeric_differences': parity,
    'frequency_by_conditions': frequency,
    'limits': ['Synthetic learning outcomes and controlled eligibility probes are not human play.',
               'Combat trials execute real TowerGame.Update with UnityShim and Catalog, not Unity/SO.',
               'CloudSync receive uses actual coroutine with synchronous HTTP/JSON doubles, not real network/timing.',
               'Learning and combat/selection are separate executables: no end-to-end HTTP-to-Prepare claim.',
               'Catalog parity covers numeric pattern fields only; differences are observations, not policy failures.',
               'Logs, generated session IDs, timestamps and temporary paths excluded from deterministic comparison.',
               'No Unity rendering, audio, Windows build, production DB, credentials, personal saves or queue.']}
(out / 'audit.json').write_text(json.dumps(manifest, indent=2))
ok = bool(results) and all(v['exit_code'] == 0 for v in results.values()) and all(comparisons.values()) and initial_hashes == final_hashes
print('AUDIT', 'PASS' if ok else 'FAIL', 'deterministic', sum(comparisons.values()), '/', len(comparisons),
      'asset numeric differences', len(parity), flush=True)
raise SystemExit(0 if ok else 1)
