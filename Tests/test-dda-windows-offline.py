"""Actual C# offline/restart regression; can run on Windows or Linux. No DB.
Use a NEW --output folder; intentionally removes PATH commands for this process.
"""
import sys,os,json,tempfile,argparse,shutil
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'Tools/DdaLab'))
import lab
parser=argparse.ArgumentParser();parser.add_argument('--dotnet',required=True);parser.add_argument('--output',type=Path);args=parser.parse_args()
dotnet=shutil.which(args.dotnet) or str(Path(args.dotnet).resolve())
root=(args.output or Path(tempfile.mkdtemp(prefix='dda-offline-'))/'한글 공백 결과').resolve()
os.environ['PATH']=str(root/'no-command-directory')
first=lab.Lab(dotnet,root)
a=first.submit({'mode':'control','rounds':2},asynchronous=False)
assert a['status']=='completed',a
result=json.loads((root/a['id']/'result.json').read_text(encoding='utf-8'))
assert result['manifest']['source_commit'] is None
assert 'git' in result['manifest']['git_warning']
second=lab.Lab(dotnet,root)
assert a['id'] in second.jobs
b=second.submit({'mode':'control','rounds':2},asynchronous=False)
assert b['status']=='completed' and b['replay']['identical'],b
build=next((root/'.build').glob('*'))
assert json.loads((build/'global.json').read_text(encoding='utf-8'))['sdk']['version']==first.sdk['version']
assert '<clear />' in (build/'NuGet.Config').read_text(encoding='utf-8')
assert not list((build/'packages').glob('*'))
print('OFFLINE_RESTART_PASS: PATH without git, actual C# twice, Korean/spaced result path, SDK8 pinned, no NuGet packages, restart/history/fingerprint, Git warning retained')
print(root)
