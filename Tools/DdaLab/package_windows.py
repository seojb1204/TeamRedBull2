"""Build-only portable test package; does not publish, commit, or contact game DB.
Build machine needs Python/.NET8 SDK and a Unity Windows build. Target PC does not.
"""
import argparse,hashlib,json,os,shutil,subprocess,sys,tempfile,zipfile
from pathlib import Path
from xml.sax.saxutils import escape
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[1]
sys.path.insert(0,str(HERE))
from runtime import select_sdk,pin_sdk
from database import public_config
REQUIRED=['ReversalTower.exe','UnityPlayer.dll','dstorage.dll','dstoragecore.dll','MonoBleedingEdge/EmbedRuntime/mono-2.0-bdwgc.dll','ReversalTower_Data/globalgamemanagers','ReversalTower_Data/StreamingAssets/supabase.json']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def source_stamp():
    return {str(p.relative_to(ROOT)).replace('\\','/'):sha(p) for folder in ['Assets','Packages','ProjectSettings'] for p in sorted((ROOT/folder).rglob('*')) if p.is_file()}

def build_game(unity,game):
    unity=Path(unity).resolve()
    if not unity.is_file() or '6000.4.0f1' not in str(unity):raise ValueError('Unity 6000.4.0f1 + Windows Build Support 실행 파일 필요')
    if game.exists():raise ValueError('기존 Windows 빌드 폴더는 덮어쓰지 않습니다. 새 --game 경로를 지정하세요.')
    before=source_stamp()
    with tempfile.TemporaryDirectory(prefix='tower-unity-testbuild-') as tmp:
        w=Path(tmp);project=w/'Project';project.mkdir()
        for folder in ['Assets','Packages','ProjectSettings']:shutil.copytree(ROOT/folder,project/folder)
        game.parent.mkdir(parents=True,exist_ok=True);log=game.parent/(game.name+'-unity.log')
        command=[str(unity),'-batchmode','-nographics','-projectPath',str(project),'-buildTarget','Win64','-executeMethod','BuildProject.Build','-logFile',str(log)]
        r=subprocess.run(command,timeout=3600)
        if r.returncode:raise ValueError('Unity 빌드 실패: '+str(log))
        generated=w/'Windows'
        validate_game(generated)
        if source_stamp()!=before:raise ValueError('빌드 중 소스 변경: 패키지 생성 중단')
        shutil.copytree(generated,game)
        (game/'game-build.json').write_text(json.dumps({'unity':'6000.4.0f1','source_sha256':before,'config_sha256':sha(ROOT/'Assets/StreamingAssets/supabase.json'),'testBuild':True},indent=2),encoding='utf-8')

def validate_game(game):
 for name in REQUIRED:
  p=game/name
  if not p.is_file() or p.stat().st_size==0:raise ValueError('Unity 필수 파일 없음/비어 있음: '+name)
 c=public_config(game/'ReversalTower_Data/StreamingAssets/supabase.json')
 if not c['proficiencySync']:raise ValueError('빌드 StreamingAssets proficiencySync=true 필요')
 for name in ['ReversalTower.exe','UnityPlayer.dll']:
  if (game/name).read_bytes()[:2]!=b'MZ':raise ValueError('Windows PE 파일 아님: '+name)
 return c

def publish_engine(dotnet,dest,rid='win-x64'):
 import lab
 sdk=select_sdk(dotnet)
 with tempfile.TemporaryDirectory(prefix='tower-engine-publish-') as tmp:
  w=Path(tmp);pin_sdk(w,sdk)
  files=[ROOT/'Assets/Scripts'/(n+'.cs') for n in lab.NAMES]+[ROOT/'Tests/UnityShim.cs',HERE/'Experiment.cs']
  (w/'DdaExperiment.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>HEADLESS</DefineConstants></PropertyGroup><ItemGroup>'+''.join('<Compile Include="'+escape(str(p),{'"':'&quot;'})+'" />' for p in files)+'</ItemGroup></Project>',encoding='utf-8')
  # Packaging machine only: framework runtime packs from official NuGet; developer Lab remains offline.
  (w/'NuGet.Config').write_text('<configuration><packageSources><clear/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>',encoding='utf-8')
  dest.mkdir(parents=True,exist_ok=False)
  env=dict(os.environ,DOTNET_CLI_HOME=str(w/'cli'),NUGET_PACKAGES=str(w/'packages'),DOTNET_CLI_TELEMETRY_OPTOUT='1',DOTNET_PROCESSOR_COUNT='1')
  command=[sdk['executable'],'publish',str(w/'DdaExperiment.csproj'),'-c','Release','-r',rid,'--self-contained','true','-o',str(dest),'--verbosity','minimal']
  with (dest/'publish.log').open('w',encoding='utf-8') as log:
   log.write(repr(command)+'\n');log.flush();result=subprocess.run(command,cwd=w,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=600)
  if result.returncode:raise ValueError('C# 자체 포함 publish 실패: '+str(dest/'publish.log'))
  exe=dest/('DdaExperiment.exe' if rid=='win-x64' else 'DdaExperiment')
  if not exe.is_file():raise ValueError('publish 실행 파일 없음')
  manifest={'rid':rid,'source_sha256':{str(p.relative_to(ROOT)):sha(p) for p in files},'exe_sha256':sha(exe),'sdk':sdk['version'],'engine':'actual C# / HEADLESS UnityShim, not Unity runtime','sourceCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True,encoding='utf-8',errors='replace').strip(),'sourceDirty':bool(subprocess.check_output(['git','status','--porcelain'],cwd=ROOT,text=True,encoding='utf-8',errors='replace').strip())}
  (dest/'engine.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

def stage_python(archive,dest):
 dest.mkdir(parents=True,exist_ok=False)
 with zipfile.ZipFile(archive) as z:
  nuget='tools/python.exe' in z.namelist()
  if nuget:
   import xml.etree.ElementTree as ET
   spec=next((n for n in z.namelist() if n.endswith('.nuspec')),None)
   if not spec:raise ValueError('Python NuGet metadata 누락')
   values={e.tag.split('}')[-1]:e.text for e in ET.fromstring(z.read(spec)).iter()}
   if values.get('id')!='python' or values.get('authors')!='Python Software Foundation':raise ValueError('공식 PSF Python NuGet package metadata 필요')
  total=0
  for item in z.infolist():
   name=item.filename
   # Validate all names, even ones not copied from a NuGet archive.
   if '\\' in name or ':' in name or '..' in Path(name).parts or name.startswith('/') or ((item.external_attr>>16)&0o170000)==0o120000:raise ValueError('Python ZIP 경로 탈출/심볼릭 링크 거부')
   if nuget and not name.startswith('tools/'):continue
   name=name[6:] if nuget else name
   if not name or item.is_dir():continue
   p=(dest/name).resolve()
   if not p.is_relative_to(dest.resolve()):raise ValueError('Python ZIP 경로 탈출 거부')
   total+=item.file_size
   if item.file_size>50*1024*1024 or total>150*1024*1024:raise ValueError('Python ZIP 크기 한도 초과')
   p.parent.mkdir(parents=True,exist_ok=True)
   with z.open(item) as src,p.open('xb') as dst:shutil.copyfileobj(src,dst)
 if not (dest/'python.exe').is_file() or (dest/'python.exe').read_bytes()[:2]!=b'MZ':raise ValueError('Windows portable Python 파일 없음')
 pths=list(dest.glob('python*._pth'))
 if nuget and not pths:
  import re
  dll=next((p for p in dest.glob('python*.dll') if re.fullmatch(r'python3[0-9]{2}\.dll',p.name)),None)
  if not dll:raise ValueError('Python DLL version 불명')
  pth=dest/(dll.stem+'._pth');pth.write_text('.\nLib\nDLLs\n../../lab\n../..\n',encoding='utf-8')
 else:
  if len(pths)!=1:raise ValueError('Python embed _pth 구성 확인 필요')
  with pths[0].open('a',encoding='utf-8') as f:f.write('\n../../lab\n../..\n')
 pth=next(dest.glob('python*._pth'));stem=pth.stem
 if not (dest/(stem+'.dll')).is_file() or not ((dest/(stem+'.zip')).is_file() or (dest/'Lib/os.py').is_file()):raise ValueError('Python DLL/표준 라이브러리 누락')
 if not ((dest/'_ssl.pyd').is_file() or (dest/'DLLs/_ssl.pyd').is_file()):raise ValueError('Python SSL 모듈 누락')


def validate_engine(engine):
 required=['DdaExperiment.exe','DdaExperiment.dll','DdaExperiment.runtimeconfig.json','System.Private.CoreLib.dll','coreclr.dll','hostfxr.dll','hostpolicy.dll','engine.json']
 for name in required:
  p=engine/name
  if not p.is_file() or p.stat().st_size==0:raise ValueError('자체 포함 C# runtime 누락: '+name)
 m=json.loads((engine/'engine.json').read_text(encoding='utf-8'))
 if m.get('rid')!='win-x64' or sha(engine/'DdaExperiment.exe')!=m.get('exe_sha256'):raise ValueError('Windows 엔진 manifest/hash 불일치')
 return m

def package(game,engine,python_zip,output):
 built_config=validate_game(game)
 provenance=json.loads((game/'game-build.json').read_text(encoding='utf-8'))
 if provenance.get('source_sha256')!=source_stamp() or provenance.get('unity')!='6000.4.0f1':raise ValueError('게임 빌드 provenance가 최신 소스와 다릅니다. --unity로 새 빌드를 생성하세요.')
 current_config=public_config(ROOT/'Assets/StreamingAssets/supabase.json')
 if built_config!=current_config:raise ValueError('빌드와 소스 공개 설정 불일치')
 m=validate_engine(engine)
 expected=m['source_sha256'];actual={p:sha(ROOT/p) for p in expected}
 if actual!=expected:raise ValueError('엔진과 현재 게임 소스 불일치: 최신 소스로 엔진 재publish 필요')
 if output.exists() or output.with_suffix(output.suffix+'.sha256').exists():raise ValueError('기존 ZIP/체크섬은 덮어쓰지 않습니다')
 # Preserve only known Unity runtime names, using the same allowlist as Get-WindowsReleaseFiles.ps1.
 names=['ReversalTower.exe','ReversalTower_Data','UnityPlayer.dll','MonoBleedingEdge','dstorage.dll','dstoragecore.dll','UnityCrashHandler64.exe','WinPixEventRuntime.dll','GameAssembly.dll']
 with tempfile.TemporaryDirectory(prefix='tower-exhibit-stage-') as tmp:
  stage=Path(tmp);(stage/'game').mkdir()
  for name in names:
   src=game/name
   if src.is_dir():shutil.copytree(src,stage/'game'/name)
   elif src.is_file():shutil.copy2(src,stage/'game'/name)
  shutil.copy2(game/'game-build.json',stage/'game-build.json')
  shutil.copytree(engine,stage/'engine')
  shutil.copytree(HERE,stage/'lab',ignore=shutil.ignore_patterns('__pycache__','*.pyc'))
  stage_python(python_zip,stage/'runtime/python')
  shutil.copy2(HERE/'presentation.py',stage/'presentation.py')
  shutil.copy2(HERE/'Start-ReversalTower.cmd',stage/'Start-ReversalTower.cmd')
  (stage/'README.txt').write_text('역전의 탑 발표 테스트 빌드 (메인/로비 최종 교체 전)\nStart-ReversalTower.cmd 더블클릭. 별도 Python/.NET SDK/Unity/Git 설치 불필요.\n게임 단독: game/ReversalTower.exe\n로그: %LOCALAPPDATA%/ReversalTowerExhibit\nDB SELECT는 저장 성공이 아닙니다. 발표용 가상 데이터는 별도 표시됩니다.\n실제 Windows/Unity/LIVE/DB 권한은 별도 검증 필요.\n',encoding='utf-8')
  checks={str(p.relative_to(stage)).replace('\\','/'):sha(p) for p in stage.rglob('*') if p.is_file()}
  (stage/'SHA256SUMS.json').write_text(json.dumps(checks,indent=2),encoding='utf-8')
  output.parent.mkdir(parents=True,exist_ok=True)
  with zipfile.ZipFile(output,'x',compression=zipfile.ZIP_DEFLATED) as z:
   for p in stage.rglob('*'):
    if p.is_file():z.write(p,str(p.relative_to(stage)))
 output.with_suffix(output.suffix+'.sha256').write_text(sha(output)+'  '+output.name+'\n',encoding='ascii')
 return output

def download_python(version,destination,expected=None,source="nuget"):
 import re,urllib.request
 if not re.fullmatch(r'3\.13\.[0-9]+',version):raise ValueError('공식 Python 3.13.x 버전을 지정하세요')
 if destination.exists():raise ValueError('기존 Python archive를 덮어쓰지 않습니다')
 url=f'https://api.nuget.org/v3-flatcontainer/python/{version}/python.{version}.nupkg' if source=='nuget' else f'https://www.python.org/ftp/python/{version}/python-{version}-embed-amd64.zip'
 with urllib.request.urlopen(url,timeout=60) as r:data=r.read(32*1024*1024+1)
 if len(data)>32*1024*1024:raise ValueError('Python archive 크기 한도 초과')
 digest=hashlib.sha256(data).hexdigest()
 if expected and digest.lower()!=expected.lower():raise ValueError('Python archive SHA256 불일치')
 destination.parent.mkdir(parents=True,exist_ok=True)
 with destination.open('xb') as f:f.write(data)
 print('Official Python embedded archive SHA256:',digest)

def verify_archive(path):
 with zipfile.ZipFile(path) as z:
  names=set(z.namelist());required={'Start-ReversalTower.cmd','presentation.py','runtime/python/python.exe','engine/DdaExperiment.exe','engine/engine.json','engine/coreclr.dll','engine/hostfxr.dll','engine/hostpolicy.dll','engine/System.Private.CoreLib.dll','engine/DdaExperiment.dll','engine/DdaExperiment.runtimeconfig.json','lab/lab.py','lab/database.py','lab/monitor.py','SHA256SUMS.json','game-build.json'}|{'game/'+p for p in REQUIRED}
  if not required<=names:raise ValueError('ZIP 필수 파일 누락: '+str(sorted(required-names)))
  if any(n.startswith(('/','\\')) or '..' in Path(n.replace('\\','/')).parts or ':' in n for n in names):raise ValueError('ZIP 경로 탈출 거부')
  hashes=json.loads(z.read('SHA256SUMS.json'))
  if set(hashes)!=names-{'SHA256SUMS.json'}:raise ValueError('ZIP checksum 파일 범위 불일치')
  for name,h in hashes.items():
   if hashlib.sha256(z.read(name)).hexdigest()!=h:raise ValueError('ZIP checksum 불일치: '+name)
  for name in ['runtime/python/python.exe','engine/DdaExperiment.exe','game/ReversalTower.exe']:
   if z.read(name)[:2]!=b'MZ':raise ValueError('ZIP Windows PE signature 불일치: '+name)
 return {'files':len(names),'sha256':sha(path),'runtimeExecution':'미검증 · 파일/체크섬 검사만 수행'}

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--verify',type=Path);p.add_argument('--download-python',action='store_true');p.add_argument('--python-version',default='3.13.7');p.add_argument('--python-source',choices=['nuget','python.org'],default='nuget');p.add_argument('--python-sha256');p.add_argument('--unity',type=Path);p.add_argument('--dotnet',default=shutil.which('dotnet'));p.add_argument('--engine-output',type=Path);p.add_argument('--engine',type=Path);p.add_argument('--game',type=Path);p.add_argument('--python-embed',type=Path);p.add_argument('--output',type=Path);a=p.parse_args()
 if a.verify:print(verify_archive(a.verify));return
 if a.download_python:
  if not a.python_embed:p.error('--download-python은 --python-embed 저장 경로 필요')
  download_python(a.python_version,a.python_embed.resolve(),a.python_sha256,a.python_source)
 if a.unity:
  if not a.game:p.error('--unity는 새 --game 경로 필요')
  build_game(a.unity,a.game.resolve())
 if a.engine_output:publish_engine(a.dotnet,a.engine_output.resolve());print('Actual self-contained engine:',a.engine_output)
 if a.output:
  if not all((a.game,a.engine or a.engine_output,a.python_embed)):p.error('--game --engine --python-embed 필수')
  result=package(a.game.resolve(),(a.engine or a.engine_output).resolve(),a.python_embed.resolve(),a.output.resolve());print(result);print(verify_archive(result))
if __name__=='__main__':main()
