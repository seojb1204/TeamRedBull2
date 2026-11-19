# DDA Lab Windows 실행 안정화 — 2026-11-19

## 범위와 기준

별도 수정 브랜치 `fix/dda-lab-windows-startup-20261119`, 기준 `feat/dda-lab-live-monitor-20261119@40570a9f7a2b760a5a10bf713a268af5743de174`.
원격 재확인: main `151b5256`, 기존 통합 후보 `3342884`, 기존 Lab `40570a9`를 그대로 유지했다. 사용자 지정 수정 범위에 따라 Lab 브랜치에서 파생했으며 최신 main을 병합하지 않았다.

이번 변경은 도구 실행·오류 진단·테스트·문서만이다. Assets/ProjectSettings/Packages의 변경 **0개**, 기존 Unity DDA·성공률 기준·모니터 프로토콜·저장·CloudSync·게임 출력·디자인 변경 없음. DB/개인 저장/CloudQueue/인증키 접근 없음.

## 오류별 원인과 수정

| 발견된 오류 | 코드와 실행 경로의 원인 | 수정 |
|---|---|---|
| PSSecurityException | PowerShell 정책이 `.ps1` 실행을 Python 시작 전에 차단한다. Python 코드로 해결할 수 없음 | PowerShell을 전혀 호출하지 않는 UTF-8 CMD 실행기 추가. 실행 정책/레지스트리 변경 없음 |
| index.html CP949 UnicodeDecodeError | `Handler.do_GET`의 인코딩 없는 `Path.read_text()`가 Windows locale를 사용. job/result/config/project/log에도 동일한 잠재 결함 | 모든 도구 텍스트 읽기 UTF-8(-sig), 쓰기 UTF-8 명시. CSV UTF-8 BOM 유지. 콘솔/launcher 자식도 UTF-8 지정 |
| net8.0 reference8.0.32 NU1100 | 이전 실행기는 임시 build cwd에 global.json이 없어 설치된 상위 SDK10도 선택 가능. `<clear/>`는 외부 다운로드를 막으므로 SDK가 요구한 로컬 ref pack이 없으면 실패 | 설치 SDK 목록에서8.0 정식 버전만 선택. 해당 SDK의 BundledVersions.props에서 net8.0 참조 팩 확인. 빌드별 global.json에 정확한 버전/rollForward disable 기록하고 실제 `dotnet --version` 일치 검사. UseAppHost=false로 사용하지 않는 native apphost 패키지 의존 제외 |
| WinError2 | 이전 호출 지점은 `dotnet build`, `dotnet Lab.dll`, **실험 종료 뒤 필수 `git rev-parse HEAD`**. GitHub Desktop 설치만으로 Git CLI가 PATH에 있다고 보장되지 않음. cwd 삭제도 OSError 원인 가능 | 실패한 command/cwd/원본 오류·traceback 기록. Git 메타데이터는 optional로 변경: SHA=null 및 warning 기록, 실제 소스 SHA256 보존, 성공한 C# 결과는 정상 완료 |

학교 PC의 원래 전체 traceback/빌드 로그는 제공되지 않았으므로 **그 WinError2가 Git 때문이었다거나 8.0.32 오류가 반드시 SDK10 때문이었다고 확정하지 않는다**. 위는 코드상 확인한 실패 경로이며 수정 후 로그는 실행 파일/SDK/참조 팩/BUILD·RUN 명령/cwd를 식별한다. 외부 NuGet source clear 정책은 유지한다. 로컬 팩 부족은 SDK 복구 설치를 안내하고 다운로드로 우회하지 않는다. repo/machine global.json은 변경하지 않는다.

## 변경 파일

- `Tools/DdaLab/lab.py`: 명시적 UTF-8, SDK-aware 빌드 cache, 자동 global.json, 실제 SDK 확인, 전체 명령·오류 로그, optional Git 메타데이터.
- `Tools/DdaLab/runtime.py`: Python/필수 파일/SDK8·참조 팩 검사, 명령 오류 wrapper, Git optional 처리.
- `Tools/DdaLab/Start-DdaLab.cmd`: Python py/python 탐색, UTF-8, 경로 quoting, PowerShell 없는 진입점, 실패 시 창 유지.
- `Tools/DdaLab/launch.py`: UTF-8 자식 실행, 화면+append launcher.log, 필수 파일 누락 안내.
- `Tools/DdaLab/Start-DdaLab.ps1`: 기존 경로에서도 Python UTF-8 flag 사용. 정책 자체 변경 없음.
- `Tools/DdaLab/.gitattributes`: CMD checkout CRLF 명시.
- `Tests/test-dda-windows-startup.py`, `Tests/test-dda-windows-offline.py`: 실패 경로/실제 offline·재시작 회귀.
- `Tools/DdaLab/README.md`, 본 보고서와 실행 증거.

## 실행 방법 — 학교 Windows CMD

수정 브랜치를 GitHub Desktop에서 checkout한 후 저장소 root의 CMD:

```cmd
Tools\DdaLab\Start-DdaLab.cmd
```

탐색기에서 같은 CMD 파일을 더블클릭해도 된다. `http://127.0.0.1:8765`를 브라우저에서 연다. Python3.10+ 및 .NET8 **SDK**가 필요하다. .NET10 공존 시8을 선택하고10만 있으면 명확한 설치 안내 후 중단한다.

```cmd
Tools\DdaLab\Start-DdaLab.cmd --dotnet "C:\Program Files\dotnet\dotnet.exe"
Tools\DdaLab\Start-DdaLab.cmd --port 8766
Tools\DdaLab\Start-DdaLab.cmd --monitor-only
```

CMD가 제한되면 Python 직접 실행도 기본 locale에 의존하지 않는다:

```cmd
python Tools\DdaLab\lab.py --dotnet "C:\Program Files\dotnet\dotnet.exe"
```

CMD 런처 결과는 `%LOCALAPPDATA%\ReversalTower-DdaLab\results`, 시작/서버 로그는 같은 상위 폴더의 `launcher.log`, 각 실험의 상세 로그는 `<run id>\execution.log`, 상태·오류는 `job.json`. `--data-dir`로 결과 경로를 지정해도 launcher.log 위치는 고정이다. 로그는 진단 명령/도구 결과만 기록하며 인증 환경변수를 덤프하지 않는다. 기존 결과를 삭제하지 않고 새로운 SDK-aware cache를 자동 생성하므로 임시 build 파일 수동 편집은 불필요하다.

## 이번 실제 실행 결과

실행 환경은 **Linux, Python3.12.14, .NET8.0.428**이다. 실제 Windows/Python3.13.17/.NET10 공존 기기에서는 실행하지 못했다.

| 검사 | 결과와 증거 |
|---|---|
| Windows 실패 경로 단위검사 | **9 PASS**. SDK8/10 동시 목록 선택·exact pin,8없는 경우, pack누락, WinError2의 command, Git없음/비저장소, UTF-8 AST 보호, CMD PowerShell/Git 비의존, 필수 파일 누락. SDK 공존은 모의 목록이며 실물 공존 시험 아님 |
| 실제 Lab C# 회귀 | **46 PASS**. ON/OFF, 자연전투·통제 실험, 저장 복원, 재현성, 입력 거부. 최종 SDK/UseAppHost 수정 후 실행 |
| 실제 Git없는 PATH·한글 공백 경로 | **PASS**. C# build/run2회, SDK8 exact pin 확인, NuGet 패키지 폴더 비어 있음, Git 미수집 경고, Lab 재생성 후 기록 복원·fingerprint 동일 |
| Python CMD용 launcher 본체 | **PASS**. launch.py 통해 C# control12회 ON/OFF 완료, UTF-8 출력·launcher.log 기록. `.cmd` 자체는 미실행 |
| 브라우저 시뮬레이터 | **PASS**. 실제 C# 실행·재실행, 결과/그래프, JSON/CSV, 비교·작은 화면·JS 오류 검사 |
| 모니터 reader | **22 PASS** |
| 브라우저 모니터 | **PASS**. 기존 실제 C# HEADLESS fixture 소비, source분리·재생·attack/learning/tutorial·export·disconnect. 실제Unity LIVE 성공 아님 |
| 제품 보존 검사 | **20 PASS**. 이번 기준40570a9 대비 모든 게임/설정 변경0 |
| py_compile / git diff --check | PASS |

이번 작업은 실행기 수정이므로 전체 보스 회귀를 무의미하게 재반복하지 않았다. 앞 작업에서 보고한 기존 Arakus3suite FAIL은 고치거나 PASS로 바꾸지 않았고 이번 실행기의 성공 증거와 구분한다.

[이번 실행 로그/대표 결과/화면 증거](DdaLabWindowsStartup20261119). 실제 offline 재시작 test는 Windows에서도 실행 가능:

```cmd
python Tests\test-dda-windows-startup.py
python Tests\test-dda-windows-offline.py --dotnet "C:\Program Files\dotnet\dotnet.exe" --output "C:\DdaLabTest\새 결과"
```

위 offline 테스트는 해당 Python 프로세스의 PATH를 비워 Git 누락을 검증하며 실제 C#의 격리된 Lab 결과만 생성한다. 시스템 PATH는 변경하지 않는다. `--output`은 새 폴더로 지정한다.

## 학교 PC 최우선 재확인

- [ ] 한국어 Windows/Python3.13.17에서 CMD 하나로 시작하고 한글 HTML·오류 로그가 깨지지 않는가?
- [ ]8.0.428/10.0.402 공존 상태에서 startup SDK 및 execution.log `SDK effective`가8.0.428인가?
- [ ] Git CLI가 PATH에 없어도 실험 완료/그래프/CSV·JSON이 나오고 Git 경고만 남는가?
- [ ] 종료→같은 CMD 재시작→같은 조건 재실행에서 기록 복원·산출물 동일인가?
- [ ] 없는 dotnet 경로·참조 팩 누락·필수 파일 누락·포트 사용 중 오류의 실제 경로/명령/로그가 확인되는가?
- [ ] Windows Defender/학교 정책·경로 공백·한글 사용자명·권한·파일 잠금의 실제 동작 확인.
- [ ] 실제 Unity/Windows LIVE 연동은 기존 안내의 DB 설정 없는 테스트 환경에서 **별도로** 검증. 이번 HEADLESS 결과를 실제 게임 연결 성공으로 취급하지 않음.

### GitHub 전달

수정 브랜치만 Push하며 main/기존 후보/기존 Lab 브랜치는 변경하지 않는다. 전달 결과는 마지막 문서 커밋에 기록한다.
