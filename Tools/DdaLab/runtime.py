"""Offline launcher checks. No Git requirement and no machine-wide SDK changes."""
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET


def run(command, **kwargs):
    """Name the exact failing child process (including WinError 2)."""
    try:
        return subprocess.run(command, **kwargs)
    except OSError as e:
        raise RuntimeError(f'외부 명령 실행 실패: {command!r}; cwd={kwargs.get("cwd", os.getcwd())}; {e}. 실행 파일 경로와 설치 상태를 확인하세요.') from e


def select_sdk(dotnet):
    executable = shutil.which(str(dotnet)) or str(Path(dotnet).resolve())
    if not Path(executable).is_file():
        raise RuntimeError(f'.NET 실행 파일 없음: {executable}. .NET 8 SDK 설치 후 --dotnet "전체 경로"를 지정하세요.')
    result = run([executable, '--list-sdks'], capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=30)
    if result.returncode:
        raise RuntimeError(f'.NET SDK 목록 실패: {[executable, "--list-sdks"]!r}\n{result.stdout}\n{result.stderr}')
    choices = []
    for line in result.stdout.splitlines():
        match = re.fullmatch(r'(8\.0\.(\d+))\s+\[(.+)\]', line.strip())
        if match:
            choices.append((int(match[2]), match[1], Path(match[3]) / match[1]))
    if not choices:
        raise RuntimeError('.NET 8 SDK가 없습니다. .NET 10 또는 런타임만으로는 실행하지 않습니다. .NET 8 SDK를 설치하세요.\n' + result.stdout)
    _, version, sdk_path = max(choices)
    props = sdk_path / 'Microsoft.NETCoreSdk.BundledVersions.props'
    if not props.is_file():
        raise RuntimeError(f'SDK 설치 파일 누락: {props}. .NET 8 SDK를 복구 설치하세요.')
    framework = next((n for n in ET.parse(props).getroot().iter('KnownFrameworkReference')
                      if n.get('Include') == 'Microsoft.NETCore.App' and n.get('TargetFramework') == 'net8.0'), None)
    if framework is None:
        raise RuntimeError(f'net8.0 참조 정보 없음: {props}. SDK를 복구 설치하세요.')
    pack_version = framework.get('TargetingPackVersion')
    pack = sdk_path.parent.parent / 'packs' / 'Microsoft.NETCore.App.Ref' / pack_version
    if not (pack / 'ref/net8.0/System.Runtime.dll').is_file():
        raise RuntimeError(f'오프라인 참조 팩 누락: {pack}. .NET 8 SDK {version}를 복구 설치하세요. NuGet 다운로드로 우회하지 않습니다.')
    return dict(executable=executable, version=version, sdk_path=str(sdk_path), reference_pack=str(pack))


def pin_sdk(folder, sdk):
    (folder / 'global.json').write_text(json.dumps({'sdk': {'version': sdk['version'], 'rollForward': 'disable', 'allowPrerelease': False}}), encoding='utf-8')


def git_revision(root):
    command = ['git', 'rev-parse', 'HEAD']
    try:
        result = run(command, cwd=root, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=10)
        if result.returncode == 0:
            return result.stdout.strip(), None
        return None, f'Git 메타데이터 미수집: {command!r}; exit={result.returncode}; {result.stderr.strip()}'
    except (RuntimeError, subprocess.TimeoutExpired) as e:
        return None, f'Git 메타데이터 미수집: {e}. 소스 SHA256으로 결과를 식별하며 실험은 계속합니다.'


def check_files(root, here, names):
    files = [here / n for n in ['lab.py', 'runtime.py', 'monitor.py', 'index.html', 'monitor.js', 'monitor.css', 'Experiment.cs']]
    files += [root / 'Assets/Scripts' / (n + '.cs') for n in names] + [root / 'Tests/UnityShim.cs']
    missing = [str(f) for f in files if not f.is_file()]
    if missing:
        raise RuntimeError('필수 파일 누락. 저장소 전체를 내려받으세요:\n' + '\n'.join(missing))
    if sys.version_info < (3, 10):
        raise RuntimeError('Python 3.10 이상이 필요합니다.')
