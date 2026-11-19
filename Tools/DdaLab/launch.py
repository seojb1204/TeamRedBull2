"""CMD entry point: UTF-8 console + durable startup/server log, no PowerShell."""
import os
from pathlib import Path
import subprocess
import sys
import traceback


def main():
    for stream in (sys.stdout, sys.stderr):
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8', errors='backslashreplace')
    if sys.version_info < (3, 10):
        print('Python 3.10 이상을 설치하세요. 현재: ' + sys.version)
        return 1
    home = Path(os.environ.get('LOCALAPPDATA') or Path.home()) / 'ReversalTower-DdaLab'
    home.mkdir(parents=True, exist_ok=True)
    command = [sys.executable, '-X', 'utf8', '-u', str(Path(__file__).with_name('lab.py')),
               '--data-dir', str(home / 'results'), *sys.argv[1:]]
    logfile = home / 'launcher.log'
    print('실행 로그: ' + str(logfile), flush=True)
    with logfile.open('a', encoding='utf-8') as log:
        log.write('\nSTART ' + repr(command) + '\nPython: ' + sys.version + '\n'); log.flush()
        for name in ('lab.py', 'runtime.py', 'monitor.py', 'index.html', 'monitor.js', 'monitor.css', 'Experiment.cs'):
            required = Path(__file__).with_name(name)
            if not required.is_file():
                message = '필수 파일 누락: ' + str(required) + '. 저장소 전체를 내려받으세요.'
                print(message); log.write(message + '\n')
                return 1
        try:
            child = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                     encoding='utf-8', errors='replace')
            try:
                for line in child.stdout:
                    print(line, end='', flush=True)
                    log.write(line); log.flush()
                return child.wait()
            except KeyboardInterrupt:
                child.terminate()
                child.wait(timeout=10)
                return 0
        except Exception:
            traceback.print_exc(file=log)
            print('시작 실패: ' + repr(command) + '. ' + str(logfile) + ' 확인')
            return 1


if __name__ == '__main__':
    raise SystemExit(main())
