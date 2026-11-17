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
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlsplit, parse_qs
from xml.sax.saxutils import escape

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
NAMES = ['GameData', 'TowerGame', 'BossCombat', 'SaveStore', 'LobbyCards', 'AutoRun',
         'LobbySmoke', 'BossPatternSystem', 'GameServices', 'ArakusPatternTiming', 'ArakusAnimationSelection']
DEFAULT = dict(mode='combat', profile='balanced', scenario='stable', dda='both', seed=17,
               rounds=8, boss=0, phase=1, distance=4, releaseCooldowns=False)


def validate(data):
    if not isinstance(data, dict) or set(data) - DEFAULT.keys():
        raise ValueError('Unknown configuration fields')
    c = DEFAULT | data
    for k, choices in dict(mode=['combat', 'control', 'selection'], profile=['novice', 'balanced', 'expert'],
                           scenario=['stable', 'improving', 'declining', 'repeated_failures'], dda=['both', 'on', 'off']).items():
        if c[k] not in choices:
            raise ValueError('Invalid ' + k)
    for k, low, high in [('seed', 0, 10**9), ('rounds', 1, 100), ('boss', 0, 4), ('phase', 1, 2)]:
        if type(c[k]) is not int or not low <= c[k] <= high:
            raise ValueError(f'{k}: {low}..{high} integer required')
    if type(c['distance']) not in (int, float) or not 0 <= c['distance'] <= 20:
        raise ValueError('distance: 0..20 required')
    if type(c['releaseCooldowns']) is not bool:
        raise ValueError('releaseCooldowns must be boolean')
    return c


class Lab:
    def __init__(self, dotnet, data):
        self.dotnet = dotnet
        self.data = Path(data).resolve()
        self.data.mkdir(parents=True, exist_ok=True)
        self.lock = threading.Lock()
        self.busy = False
        self.jobs = {}
        # Reopen only this tool's own completed job records; never scan player saves.
        for f in self.data.glob('*/job.json'):
            if re.fullmatch(r'[0-9a-f]{24}', f.parent.name):
                try:
                    job = json.loads(f.read_text())
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
            config_path.write_text(json.dumps(config))
            # Cache only builds of the exact selected source hashes. One worker owns build/run.
            key = hashlib.sha256(json.dumps(hashes, sort_keys=True).encode()).hexdigest()
            w = self.data / '.build' / key
            w.mkdir(parents=True, exist_ok=True)
            project = w / 'Lab.csproj'
            project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
                '<TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
                '<DefineConstants>HEADLESS</DefineConstants></PropertyGroup><ItemGroup>'
                + ''.join('<Compile Include="' + escape(str(p), {'"': '&quot;'}) + '" />' for p in self.files)
                + '</ItemGroup></Project>')
            (w / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
            env = dict(os.environ, DOTNET_CLI_HOME=str(w / 'cli'), NUGET_PACKAGES=str(w / 'packages'),
                       DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
            dll = w / 'out/Lab.dll'
            with (folder / 'execution.log').open('w') as log:
                if not dll.is_file() or not (w / 'build.complete').is_file():
                    build = subprocess.run([self.dotnet, 'build', str(project), '--output', str(w / 'out'), '--verbosity', 'quiet'],
                                           cwd=w, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=180)
                    if build.returncode:
                        raise RuntimeError('C# build failed. ' + (folder / 'execution.log').read_text()[-1800:])
                    (w / 'build.complete').write_text(key)
                command = [self.dotnet, str(dll), str(config_path), str(folder / 'raw.json')]
                result = subprocess.run(command, cwd=w, env=env, stdout=log, stderr=subprocess.STDOUT, timeout=600)
            if result.returncode:
                raise RuntimeError(f'C# failed ({result.returncode}). See execution.log. ' + (folder / 'execution.log').read_text()[-1800:])
            if hashes != self.hashes():
                raise RuntimeError('Source changed during experiment; results rejected')
            raw = json.loads((folder / 'raw.json').read_text())
            if not raw['battles'] or any(not b['restored'] for b in raw['battles']):
                raise RuntimeError('Missing trials or failed save restore')
            # Fingerprint includes every result field, not run ID/time/manifest/host paths.
            fingerprint = hashlib.sha256(json.dumps(raw, sort_keys=True, separators=(',', ':'), ensure_ascii=False).encode()).hexdigest()
            replay = None
            for previous in self.jobs.values():
                if previous['id'] != run_id and previous.get('status') == 'completed':
                    old = json.loads((self.data / previous['id'] / 'result.json').read_text())
                    if old['config'] == config and old['manifest']['source_sha256'] == hashes:
                        replay = dict(run=previous['id'], identical=old['manifest']['policy_sha256'] == fingerprint)
                        break
            raw['manifest'] = dict(source_commit=subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(),
                source_sha256=hashes, policy_sha256=fingerprint, sources_unchanged=True, replay=replay,
                command=command, exit_code=result.returncode, run_id=run_id, created_utc=time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()))
            (folder / 'result.json').write_text(json.dumps(raw, ensure_ascii=False, indent=2))
            self.csv(folder, raw)
            job.update(status='completed', replay=replay)
        except Exception as e:
            job.update(status='failed', error=str(e))
        finally:
            try:
                (folder / 'job.json').write_text(json.dumps(job, ensure_ascii=False))
            except OSError as e:
                job.update(status='failed', error='Could not persist result: ' + str(e))
            finally:
                with self.lock:
                    self.busy = False

    @staticmethod
    def csv(folder, data):
        rows = []
        for b in data['battles']:
            nxt = {s['pattern']: s for s in b['next']}
            assigned = {s['pattern']: s for s in b['assigned']}
            for s in b['stats']:
                rows.append(dict(dda=b['dda'], round=b['round'], seed=b['seed'], profile=b['profile'], outcome=b['outcome'],
                    **s, next_level=nxt[s['pattern']]['level'], assignment_reason=assigned[s['pattern']]['reason'],
                    next_reason=nxt[s['pattern']]['reason']))
        for name, values in [('battles.csv', rows), ('selections.csv', data['selections'])]:
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
                return self.send(200, (HERE / 'index.html').read_text().replace('__CSRF__', token), 'text/html; charset=utf-8')
            if url.path == '/api/jobs':
                return self.send(200, list(lab.jobs.values()))
            if url.path == '/api/result':
                q = parse_qs(url.query)
                run = q.get('id', [''])[0]
                filename = q.get('file', ['result.json'])[0]
                if run not in lab.jobs or filename not in ('result.json', 'battles.csv', 'selections.csv', 'execution.log'):
                    return self.send(404, {'error': 'Unknown result'})
                f = lab.data / run / filename
                if not f.is_file():
                    return self.send(404, {'error': 'Result is not ready'})
                return self.send(200, f.read_bytes(), 'application/json' if filename.endswith('.json') else 'text/plain; charset=utf-8')
            return self.send(404, {'error': 'Not found'})

        def do_POST(self):
            if not self.allowed() or self.headers.get('X-Lab-Token') != token:
                return self.send(403, {'error': 'Local UI token required'})
            if self.path != '/api/run':
                return self.send(404, {'error': 'Not found'})
            try:
                length = int(self.headers.get('Content-Length', '0'))
                if not 0 < length <= 4096:
                    raise ValueError('Invalid body size')
                data = json.loads(self.rfile.read(length))
                return self.send(202, lab.submit(data))
            except ValueError as e:
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
    p.add_argument('--data-dir', type=Path)
    p.add_argument('--port', type=int, default=8765)
    p.add_argument('--config', type=Path, help='Run one validated JSON config without starting UI')
    a = p.parse_args()
    if not a.dotnet:
        p.error('Install .NET 8 SDK or pass --dotnet PATH')
    dotnet = shutil.which(a.dotnet) or str(Path(a.dotnet).resolve())
    if not Path(dotnet).is_file():
        p.error('dotnet executable not found')
    lab = Lab(dotnet, a.data_dir or tempfile.mkdtemp(prefix='tower-dda-results-'))
    if a.config:
        job = lab.submit(json.loads(a.config.read_text()), asynchronous=False)
        print(json.dumps(job, ensure_ascii=False))
        print(lab.data / job['id'])
        return 0 if job['status'] == 'completed' else 1
    serve(lab, a.port)


if __name__ == '__main__':
    raise SystemExit(main())
