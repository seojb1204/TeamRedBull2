"""Local offline DDA experiment UI and CLI. Requires Python 3.10+ and .NET 8 SDK.
No packages, registry restore, database access or Unity startup.
"""
import argparse
import csv
import hashlib
import json
import os
from pathlib import Path
import secrets
import re
import shutil
import subprocess
import tempfile
import sys
import threading
import time
import traceback
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from monitor import Monitor,MAX_EXPORT
from runtime import run, select_sdk, pin_sdk, git_revision, check_files
NAMES = ['TutorialSession', 'TowerGame.Tutorial', 'GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
         'LobbySmoke', 'BossPatternSystem', 'GameServices', 'ArakusPatternTiming', 'ArakusAnimationSelection']
DEFAULT = dict(mode='combat', profile='balanced', scenario='stable', dda='both', seed=17,
               rounds=8, boss=0, phase=1, distance=4, releaseCooldowns=False)


def validate(data):
    if not isinstance(data, dict) or set(data) - DEFAULT.keys():
        raise ValueError('Unknown configuration fields')
    c = DEFAULT | data
    for k, choices in dict(mode=['combat', 'control', 'selection'], profile=['novice', 'balanced', 'expert'],
                           scenario=['stable', 'improving', 'declining', 'repeated_failures', 'perfect', 'near_perfect', 'loss_recovery', 'sparse', 'unobserved_clear'], dda=['both', 'on', 'off']).items():
        if c[k] not in choices:
            raise ValueError('Invalid ' + k)
    for k, low, high in [('seed', 0, 10**9), ('rounds', 1, 100), ('boss', 0, 4), ('phase', 1, 2)]:
        if type(c[k]) is not int or not low <= c[k] <= high:
            raise ValueError(f'{k}: {low}..{high} integer required')
    if type(c['distance']) not in (int, float) or not 0 <= c['distance'] <= 20:
        raise ValueError('distance: 0..20 required')
    if type(c['releaseCooldowns']) is not bool:
        raise ValueError('releaseCooldowns must be boolean')
    if c['mode'] != 'control' and c['scenario'] in ('perfect','near_perfect','loss_recovery','sparse','unobserved_clear'):
        raise ValueError('This observation-injection scenario requires control mode')
    return c


class Lab:
    def __init__(self, dotnet, data):
        self.sdk = select_sdk(dotnet) if dotnet else None
        self.dotnet = self.sdk["executable"] if self.sdk else None
        self.data = Path(data).resolve()
        self.data.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock()
        self.busy = False
        self.jobs = {}
        # Reopen only this tool's own completed job records; never scan player saves.
        for f in self.data.glob('*/job.json'):
            if re.fullmatch(r'[0-9a-f]{24}', f.parent.name):
                try:
                    job = json.loads(f.read_text(encoding='utf-8-sig'))
                    if job.get('id') == f.parent.name and job.get('status') in ('completed', 'failed'):
                        self.jobs[job['id']] = job
                except (OSError, ValueError):
                    pass
        self.files = [ROOT / 'Assets/Scripts' / (n + '.cs') for n in NAMES] + [ROOT / 'Tests/UnityShim.cs', HERE / 'Experiment.cs']

    def hashes(self):
        return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest() for p in self.files}

    def execute(self, config, run_id):
        job = self.jobs[run_id]
        folder = self.data / run_id
        try:
            folder.mkdir()
            hashes = self.hashes()
            config_path = folder / 'config.json'
            config_path.write_text(json.dumps(config), encoding='utf-8')
            # Cache only builds of the exact selected source hashes. One worker owns build/run.
            key = hashlib.sha256(json.dumps(dict(sources=hashes, sdk=self.sdk, build_contract=3), sort_keys=True).encode()).hexdigest()
            w = self.data / '.build' / key
            w.mkdir(parents=True, exist_ok=True)
            project = w / 'Lab.csproj'
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net8.0</TargetFramework><UseAppHost>false</UseAppHost><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                '<DefineConstants>HEADLESS</DefineConstants></PropertyGroup><ItemGroup>'
                + ''.join('<Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />' for p in self.files)
                + '</ItemGroup></Project>', encoding='utf-8')
            (w / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>', encoding='utf-8')
            pin_sdk(w, self.sdk)
            env = dict(os.environ, DOTNET_CLI_HOME=str(w / 'cli'), NUGET_PACKAGES=str(w / 'packages'),
                       DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', DOTNET_PROCESSOR_COUNT='1')
            dll = w / 'out/Lab.dll'
            with (folder / 'execution.log').open('w', encoding='utf-8') as log:
                log.write('SDK: ' + json.dumps(self.sdk, ensure_ascii=False) + '\n')
                log.flush()
                if not dll.is_file() or not (w / 'build.complete').is_file():
                    version = run([self.dotnet, '--version'], cwd=w, env=env, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=30)
                    log.write('SDK effective: ' + version.stdout + version.stderr)
                    if version.returncode or version.stdout.strip() != self.sdk['version']:
                        raise RuntimeError('SDK 고정 실패: ' + version.stdout + version.stderr)
                    build_command = [self.dotnet, 'build', str(project), '--output', str(w / 'out'), '--verbosity', 'quiet']
                    log.write('BUILD: ' + repr(build_command) + '\n'); log.flush()
                    build = run(build_command,
                                           cwd=w, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=180)
                    if build.returncode:
                        raise RuntimeError('C# build failed. ' + (folder / 'execution.log').read_text(encoding='utf-8-sig')[-1800:])
                    (w / 'build.complete').write_text(key, encoding='utf-8')
                command = [self.dotnet, str(dll), str(config_path), str(folder / 'raw.json')]
                log.write('RUN: ' + repr(command) + '\n'); log.flush()
                result = run(command, cwd=w, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=600)
            if result.returncode:
                raise RuntimeError(f'C# failed ({result.returncode}). See execution.log. ' + (folder / 'execution.log').read_text(encoding='utf-8-sig')[-1800:])
            if hashes != self.hashes():
                raise RuntimeError('Source changed during experiment; results rejected')
            raw = json.loads((folder / 'raw.json').read_text(encoding='utf-8-sig'))
            if not raw['battles'] or any(not b['restored'] for b in raw['battles']):
                raise RuntimeError('Missing trials or failed save restore')
            # Fingerprint includes every result field, not run ID/time/manifest/host paths.
            fingerprint = hashlib.sha256(json.dumps(raw, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest()
            replay = None
            for previous in self.jobs.values():
                if previous['id'] != run_id and previous.get('status') == 'completed':
                    try:
                        old = json.loads((self.data / previous['id'] / 'result.json').read_text(encoding='utf-8-sig'))
                    except (OSError, ValueError):
                        continue
                    if old['config'] == config and old['manifest']['source_sha256'] == hashes:
                        replay = dict(run=previous['id'], identical=old['manifest']['policy_sha256'] == fingerprint)
                        break
            revision, git_warning = git_revision(ROOT)
            if git_warning:
                with (folder / 'execution.log').open('a', encoding='utf-8') as log:
                    log.write(git_warning + '\n')
            raw['manifest'] = dict(source_commit=revision, git_warning=git_warning, sdk=self.sdk,
                source_sha256=hashes, policy_sha256=fingerprint, sources_unchanged=True, replay=replay,
                command=command, exit_code=result.returncode, run_id=run_id, created_utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()))
            (folder / 'result.json').write_text(json.dumps(raw, ensure_ascii=False, indent=2), encoding='utf-8')
            self.csv(folder, raw)
            job.update(status='completed', replay=replay)
        except Exception as e:
            job.update(status='failed', error=str(e), log=str(folder / 'execution.log'))
            try:
                with (folder / 'execution.log').open('a', encoding='utf-8') as log:
                    traceback.print_exc(file=log)
            except OSError:
                pass
        finally:
            try:
                (folder / 'job.json').write_text(json.dumps(job, ensure_ascii=False), encoding='utf-8')
            except OSError as e:
                job.update(status='failed', error='Could not persist result: ' + str(e))
            finally:
                with self.lock:
                    self.busy = False

    @staticmethod
    def csv(folder, data):
        rows = []
        traces = []
        for b in data['battles']:
            for t in (b.get('trace') or {}).get('patterns', []):
                traces.append(dict(dda=b['dda'],round=b['round'],**{k: json.dumps(v,ensure_ascii=False) if isinstance(v,(list,dict)) else v for k,v in t.items()}))
            nxt = {s['pattern']: s for s in b['next']}
            assigned = {s['pattern']: s for s in b['assigned']}
            for s in b['stats']:
                rows.append(dict(dda=b['dda'], round=b['round'], seed=b['seed'], profile=b['profile'], outcome=b['outcome'],
                    **s, next_level=nxt[s['pattern']]['level'], assignment_reason=assigned[s['pattern']]['reason'],
                    next_reason=nxt[s['pattern']]['reason']))
        for name, values in [('battles.csv', rows), ('selections.csv', data['selections']), ('learning.csv', traces)]:
            with (folder / name).open('w', newline='', encoding='utf-8-sig') as f:
                writer = csv.DictWriter(f, fieldnames=list(values[0]) if values else ['no_selection_rows'])
                writer.writeheader()
                writer.writerows(values)

    def submit(self, config, asynchronous=True):
        config = validate(config)
        with self.lock:
            if self.busy:
                raise RuntimeError('Another experiment is running')
            self.busy = True
            run_id = secrets.token_hex(12)
            self.jobs[run_id] = dict(id=run_id, status='running', config=config)
        if asynchronous:
            threading.Thread(target=self.execute, args=(config, run_id), daemon=True).start()
        else:
            self.execute(config, run_id)
        return self.jobs[run_id]


def serve(lab, port):
    monitor=Monitor()
    token = secrets.token_urlsafe(32)
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            pass

        def send(self, status, body, kind='application/json; charset=utf-8'):
            if isinstance(body, (dict, list)):
                body = json.dumps(body, ensure_ascii=False).encode()
            elif isinstance(body, str):
                body = body.encode()
            self.send_response(status)
            self.send_header('Content-Type', kind)
            self.send_header('Content-Length', str(len(body)))
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.end_headers()
            self.wfile.write(body)

        def allowed(self):
            return self.headers.get('Host') in (f'127.0.0.1:{self.server.server_port}', f'localhost:{self.server.server_port}')

        def do_GET(self):
            if not self.allowed():
                return self.send(403, {'error': 'Local host only'})
            url = urlsplit(self.path)
            if url.path == '/':
                return self.send(200, (HERE / 'index.html').read_text(encoding='utf-8-sig').replace('__CSRF__', token).replace('__MONITOR_ONLY__', 'true' if lab.dotnet is None else 'false'), 'text/html; charset=utf-8')
            if url.path in ('/monitor.js','/monitor.css'):
                name=url.path[1:]
                return self.send(200,(HERE/name).read_text(encoding='utf-8-sig'),'text/javascript; charset=utf-8' if name.endswith('.js') else 'text/css; charset=utf-8')
            if url.path.startswith('/api/monitor/'):
                if self.headers.get('X-Lab-Token')!=token:return self.send(403,{'error':'Local UI token required'})
                try:
                    if url.path=='/api/monitor/state':return self.send(200,monitor.state(max(0,int(parse_qs(url.query).get('since',['0'])[0]))))
                    if url.path=='/api/monitor/export':return self.send(200,monitor.export())
                except ValueError as e:return self.send(400,{'error':str(e)})
                return self.send(404,{'error':'Unknown monitor endpoint'})
            if url.path == '/api/jobs':
                return self.send(200, list(lab.jobs.values()))
            if url.path == '/api/result':
                q = parse_qs(url.query)
                run = q.get('id', [''])[0]
                filename = q.get('file', ['result.json'])[0]
                if run not in lab.jobs or filename not in ('result.json', 'battles.csv', 'selections.csv', 'learning.csv', 'execution.log'):
                    return self.send(404, {'error': 'Unknown result'})
                f = lab.data / run / filename
                if not f.is_file():
                    return self.send(404, {'error': 'Result is not ready'})
                return self.send(200, f.read_bytes(), 'application/json' if filename.endswith('.json') else 'text/plain; charset=utf-8')
            return self.send(404, {'error': 'Not found'})

        def do_POST(self):
            if not self.allowed() or self.headers.get('X-Lab-Token') != token:
                return self.send(403, {'error': 'Local UI token required'})
            path=urlsplit(self.path).path
            if path not in ('/api/run','/api/monitor/connect','/api/monitor/disconnect','/api/monitor/replay','/api/monitor/browse'):
                return self.send(404, {'error': 'Not found'})
            try:
                length = int(self.headers.get('Content-Length', '0'))
                maximum=MAX_EXPORT if path=='/api/monitor/replay' else 4096
                if not 0 < length <= maximum:raise ValueError('Invalid body size')
                data=json.loads(self.rfile.read(length))
                if path=='/api/monitor/connect':return self.send(200,monitor.connect(data.get('root'),data.get('choice')))
                if path=='/api/monitor/disconnect':monitor.disconnect();return self.send(200,monitor.state())
                if path=='/api/monitor/replay':return self.send(200,monitor.load_replay(data))
                if path=='/api/monitor/browse':return self.send(200,{'root':monitor.browse()})
                if lab.dotnet is None:return self.send(503,{'error':'모니터 전용 실행 중입니다. 시뮬레이션에는 .NET 8 SDK가 필요합니다.'})
                return self.send(202, lab.submit(data))
            except (ValueError,TypeError,AttributeError) as e:
                return self.send(400, {'error': str(e)})
            except RuntimeError as e:
                return self.send(409, {'error': str(e)})
    server = ThreadingHTTPServer(('127.0.0.1', port), Handler)
    print(f'DDA Lab: http://127.0.0.1:{server.server_port} (local only)', flush=True)
    print('Results:', lab.data, flush=True)
    server.serve_forever()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--dotnet', default=shutil.which('dotnet'))
    p.add_argument('--monitor-only', action='store_true', help='Observe/replay logs with Python only; disable C# experiments')
    p.add_argument('--data-dir', type=Path)
    p.add_argument('--port', type=int, default=8765)
    p.add_argument('--config', type=Path, help='Run one validated JSON config without starting UI')
    a = p.parse_args()
    check_files(ROOT, HERE, NAMES)
    if not a.dotnet and not a.monitor_only:
        p.error('.NET 8 SDK 실행 파일을 찾을 수 없습니다. 설치 후 --dotnet 전체경로를 지정하세요. 보기 전용은 --monitor-only 입니다.')
    dotnet = None if a.monitor_only else (shutil.which(a.dotnet) or str(Path(a.dotnet).resolve()))
    lab = Lab(dotnet, a.data_dir or tempfile.mkdtemp(prefix='tower-dda-results-'))
    print('Python:', sys.version.split()[0], '| 필수 파일 확인 완료', flush=True)
    print('SDK:', json.dumps(lab.sdk, ensure_ascii=False) if lab.sdk else '모니터 전용 (.NET 불필요)', flush=True)
    if a.config and a.monitor_only:p.error('--config requires the simulator/.NET SDK')
    if a.config:
        job = lab.submit(json.loads(a.config.read_text(encoding='utf-8-sig')), asynchronous=False)
        print(json.dumps(job, ensure_ascii=False))
        print(lab.data / job['id'])
        return 0 if job['status'] == 'completed' else 1
    serve(lab, a.port)


if __name__ == '__main__':
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8', errors='backslashreplace')
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        pass
    except Exception:
        traceback.print_exc()
        print('DDA Lab 시작 실패. 위 명령/경로/원인을 확인하세요. CMD 실행 로그는 %LOCALAPPDATA%\\ReversalTower-DdaLab\\launcher.log 입니다.', file=sys.stderr)
        raise SystemExit(1)
