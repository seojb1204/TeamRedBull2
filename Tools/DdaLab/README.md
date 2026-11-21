# 역전의 탑 — DDA Lab & 게임 진단 모니터

Windows 발표용 자체 포함 패키징/원클릭 실행/DB 분석은 [EXHIBIT.md](EXHIBIT.md)를 참고하세요. 기존 아래 개발자 실행 방식은 유지됩니다.


독립 Python/브라우저 프로그램이다. **시뮬레이션은 현재 checkout의 실제 C#**, 모니터는 게임이 기록한 로컬 진단만 읽는다. LinUCB를 Python/JavaScript로 재구현하지 않는다. Unity·Supabase 로그인이나 인터넷 연결은 필요하지 않다.

기준 게임: `integration/dda-selective-20261119@3342884`. 11/18 Lab 도구만 복원했으며 과거 게임 파일은 덮지 않았다. 이 브랜치는 독립 도구/진단 후보이고 main 배포본이 아니다. [검증 보고서](../../Docs/Validation/DDA_LAB_LIVE_MONITOR_20261119.md).

## 시작

- 시뮬레이터+모니터: **Python3.10+ / .NET8 SDK / 브라우저**. 추가 pip/npm 패키지는 없다.
- 로그 모니터·재생만: Python3.10+ / 브라우저, **.NET 없이 `--monitor-only`**.
- 실제 게임: 이 브랜치의 opt-in 출력 코드가 포함된 Unity/Windows 빌드가 필요하다. 이 환경에서 실제 Unity/Windows 실행은 검증하지 못했다.

저장소 root:

```bash
python Tools/DdaLab/lab.py --dotnet dotnet --data-dir ./TestResults/DdaLab
# .NET이 없는 PC의 기록 관찰/재생 전용
python Tools/DdaLab/lab.py --monitor-only --data-dir ./TestResults/DdaLab
```

브라우저에서 `http://127.0.0.1:8765`. 다른 프로세스가 쓰면 `--port 8766`. SDK 경로에 공백이 있으면 따옴표로 묶는다. 설치 후 오프라인으로 실행한다. `--data-dir`는 Lab 결과 폴더이며 **게임 save 폴더와 별도**다. 클라우드 localhost를 다른 PC에서 그대로 여는 주소로 안내하지 않는다.

Windows **CMD 우선** (PowerShell 실행 정책 변경 불필요):

```cmd
Tools\DdaLab\Start-DdaLab.cmd
Tools\DdaLab\Start-DdaLab.cmd --monitor-only
Tools\DdaLab\Start-DdaLab.cmd --dotnet "C:\Program Files\dotnet\dotnet.exe" --port 8766
```

탐색기에서 `Tools/DdaLab/Start-DdaLab.cmd`를 더블클릭해도 된다. 시작 후 브라우저에서 위 localhost 주소를 연다. Python3.10+를 확인하고 필수 파일/SDK/참조 팩을 검사한다. CMD·프로그램 콘솔과 파일 입출력에 UTF-8을 명시했다. Git CLI가 없어도 실험은 실행되며 소스 SHA256과 Git 미수집 사유를 기록한다. 원본 저장소의 `global.json`이나 임시 폴더를 수동 수정하지 않는다.

- 결과: `%LOCALAPPDATA%\ReversalTower-DdaLab\results`
- 시작/서버 로그: `%LOCALAPPDATA%\ReversalTower-DdaLab\launcher.log` (재시작 시 append)
- 실험별 상세 로그: 결과 폴더의 `<run id>\execution.log` 및 `job.json`
- .NET8 SDK가 여러 개면 가장 높은 설치 버전을 선택하고 빌드별 `global.json`에 **정확한 버전/rollForward=disable**을 자동 기록한다. .NET10만 있으면 설치 안내 후 중단한다. SDK가 요구하는 로컬 참조 팩이 없으면8SDK 복구 설치를 안내하며 외부 NuGet으로 우회하지 않는다.
- `NU1100`: 새 실행 로그의 SDK effective/참조 팩/BUILD 명령 확인. `WinError 2`: 실패한 실제 명령·cwd 확인. Git 메타데이터 실패는 실험 실패로 처리하지 않는다.
- `PSSecurityException`: 실행 정책을 바꾸지 말고 CMD 실행기 사용.
- Windows Python3.13/.NET10 공존/CMD 실물 실행은 이 Linux 환경에서 미검증이다. 자세한 원인·재검증 항목은 [긴급 수정 보고서](../../Docs/Validation/DDA_LAB_WINDOWS_STARTUP_20261119.md).

선택적 Windows PowerShell(학교 PC 설치 권한/실행 정책은 별도 확인):

```powershell
.\Tools\DdaLab\Start-DdaLab.ps1
.\Tools\DdaLab\Start-DdaLab.ps1 -MonitorOnly
.\Tools\DdaLab\Start-DdaLab.ps1 -Dotnet "C:\Program Files\dotnet\dotnet.exe" -Port 8766
```

스크립트 실행이 제한되면 정책을 해제하지 말고 `py -3 Tools\DdaLab\lab.py --monitor-only` 또는 Python 직접 명령을 사용한다. Python은 python.org, .NET은 Microsoft의 **8 SDK**가 필요하다(런타임만으로 C# 컴파일은 안 됨). Windows 시작 스크립트 자체는 이 Linux 환경에서 미실행이다.

## 시뮬레이션 탭

1. 실제 전투 루프 / 성공률 주입 / 조건 통제 선택을 고른다. 초보(novice)·일반(balanced)·숙련(expert), 상승/하락/반복 실패, seed/1~100전투/DDA ON·OFF를 설정한다.
2. **C# 실험 실행**. 컴파일/실행 실패를 완료 결과로 표시하지 않는다. 같은 코드 hash 빌드만 재사용한다.
3. 이번 배정/다음 배정·대응률·A/b·빈도 그래프와 실제 이유/후보 점수를 본다. `candidateScoresUsed=false`이면 그 점수가 실제 선택 근거가 아니다.
4. 동일 조건 재실행은 C#을 다시 실행한다. JSON·전투/선택/학습 CSV 저장, 이전 JSON 불러오기·비교가 가능하다. 불러오기는 새로운 실험이 아니다.

| 모드 | 실제 실행과 한계 |
|---|---|
| 전투 루프 | 실제 TowerGame.Update/판정/학습 + UnityShim, 60Hz/최대120초. privileged rule-agent이며 인간/Unity 엔진이 아님. timeout은 포기로 학습 제외 |
| 성공률 주입 | 실제 Prepare/Complete/Choose/Update/저장 복원. 일반 패턴당20회 합성 관측: .30/.70/.95 확률. 사람이 적응/목표60~80% 수렴한 증거가 아님 |
| 조건 선택 | 실제 SelectPattern/BeginPattern 100회 직접 호출. 거리/페이즈/쿨다운 해제를 통제하며 자연 빈도가 아님 |

통제 전용 `perfect`100%, `near_perfect`97.5% 확률, `loss_recovery`100% 대응+앞1/3패배, `sparse`패턴당1관측, `unobserved_clear`0관측을 지원한다. 다른 모드에서 요청하면 거부한다. 상승/하락은 회차3구간 입력을 바꾼다. 자연 전투 반복실패는 novice 입력을 쓸 뿐 죽음을 강제하지 않는다.

통제 ON/OFF는 같은 관측을 주입한다. 실제 전투 ON/OFF는 전투 길이/선택/난수 소비가 달라질 수 있다. 매 전투 모델/다음 배정을 저장→로드하여 동일 확인한다. C#은 Catalog+Shim을 사용하므로 실제 scenePresentation/BossDefinition 재생과 다르다. 자동화에서는 기존 정책대로 F8이 숨겨진다.

실행 폴더에 config/result/raw JSON, battles/selections/learning CSV, execution.log/job.json. 결과는 실제 A/b 배열·context·score·소스 SHA256·결정적 fingerprint를 포함한다. 소스가 실행 중 바뀌면 결과를 거부한다. 과거의17종2040전투 보고서/실행기를 이번 검증의 성공으로 가져오지 않았다.

```bash
python Tools/DdaLab/lab.py --dotnet dotnet --config Tools/DdaLab/example-control.json --data-dir ./TestResults/DdaLab
python Tests/test-dda-lab.py --dotnet dotnet
```

.NET의 사용 CPU 수 힌트는1로 설정한다. OS CPU 제한이나 실제 게임 FPS 영향 없음의 보장은 아니다. 시연 중 대규모 컴파일/반복 실험은 피하고 미리 실행한 결과를 준비한다.

## 실제 게임 모니터 탭

### 게임 출력 — 기본 OFF

새 진단 코드는 `-towerDdaMonitor <절대 폴더>`가 있어야 시작한다. 게임은 모니터/브라우저가 없어도 동작한다.

```powershell
# DB 설정 없는 테스트 복사본 + 개인 save와 분리된 Windows 테스트 계정/VM에서 실행
.\ReversalTower.exe -towerDdaMonitor "C:\DdaDemo"
```

Unity Editor도 위 사용자 인자를 붙여 시작한 **테스트 프로젝트**에서 Play한다. 실제 Editor/Windows 연결 검증은 남아 있다. 이 인자는 **기존 CloudSync를 끄거나 기존 진행 저장을 격리하지 않는다**. DB 설정 없는 빌드/새 테스트 프로필을 사용한다. 기존 개인 저장/CloudQueue를 삭제·초기화하지 않는다. `-towerDataRoot` 같은 기존 자동화 flag는 F8을 숨기므로 F8 수동 검증 대신 쓰지 않는다. 새 진단 flag 자체는 F8/게임 입력/자동화 모드를 바꾸지 않는다.

`C:\DdaDemo\DdaLive\<프로세스 세션 GUID>\latest.json/events.jsonl` 생성. 기본5Hz 상태, 공격 시작/전투 종료/진단 종료 사건을 함께 기록한다. 최신 파일은 atomic replace, 실제 파일 I/O는 background thread. 큐32개+최신상태1개, packet256KiB UTF-8, 세션 history16MiB. 이력이 차도 최신 상태는 계속 갱신한다. 이전 파일 삭제/자동 pruning은 없다. 강제 종료 시 마지막 종료 record가 없을 수 있으나3초 freshness로 연결 끊김을 표시한다.

### 연결과 데이터 출처

- 모니터 탭에서 **진단 폴더 절대 경로를 입력**하거나 `폴더 선택`을 누른다. 폴더 선택은 선택적 표준 tkinter 창이며 GUI 없는 환경에서는 경로 입력을 사용한다. 추측한 경로를 자동 연결하지 않는다.
- 새 출력의 상위 폴더(C:\DdaDemo) 또는 정확한 세션 폴더를 선택한다. 세션이 여러 개면 직접 선택한다. 폴더 갱신 시각은 선택을 돕는 metadata이며 사건 시각이 아니다. 새 게임 프로세스를 임의로 옛 세션과 합치지 않는다.
- **LIVE GAME**: Unity origin의 새 heartbeat(캡처UTC/최근 수신3초 이내). Editor/Player 구분. 파일 프로토콜은 로컬 파일 출처 표기이며 프로세스를 암호학적으로 인증하지 않는다.
- **FIXTURE**: HEADLESS 시험 발행. 실제 Unity/Windows 성공으로 표시하지 않는다.
- **REPLAY**: 저장 JSON 또는 기존 CSV/종료 기록. 방금 수정된 옛 파일도 LIVE로 승격하지 않는다.
- **DISCONNECTED**: 미수신/오래된 캡처/종료 heartbeat. 마지막 기록은 보관하되 이전 공격을 현재로 표시하지 않는다.

게임 로그4종도 읽는다: DdaApplied/Decisions는 동일 diagnosticSession 파일명만 합치고, DdaLearning/Battles는 관계 ID가 없으므로 별도 선택 기록으로 다룬다. 같은 보스/mtime으로 추측해 다른 전투를 연결하지 않는다. 다른 사용자의 save/CloudQueue/ProficiencyKeys/설정 파일은 열지 않는다. Symlink/junction으로 진단 경계를 벗어나는 파일을 거부한다.

### 표시 범위

- 실시간: 현재 보스/phase/ON·OFF, 실제 공격 Lv·선딜/피해/쿨다운·배수, 패턴별 배정/이유/대응 합계, 튜토리얼 Preview/score/confidence/시간, 마지막 선택의 가중치/확률/이유.
- 종료 후: 실제 learned/excluded, A/b 변화량·hash/context/후보 점수, 다음 전투 배정. 종료 전의 다음 값을 추정해서 만들지 않는다.
- **패링/회피/피격 합계는 실시간 snapshot**이다. 개별 입력/방어 사건의 정확한 시각은 새로 만들지 않았다. 이력의 `관측 합계 갱신`은 캡처 기록이며 정확한 입력 이벤트가 아니다.
- 실제 공격이 없거나 오래되면 현재 공격은 비운다. 이전에 적용한 수치와 다음 판 예측을 같은 칸에 놓지 않는다.
- 6개 그래프: 배정·대응률·관측 구성·종료 다음 배정·실제 선택 횟수·학습 변화. 보스/패턴 필터. 같은 이름의 패턴도 보스/전투 run을 구분한다. 이력 표본이 빠지면 전체 빈도라고 주장하지 않는다.

`큰 글씨/전체 화면`, 펼침 학습 상세, 진단 JSON/CSV 저장·불러오기를 제공한다. 재생 slider는 선택 시점 이후의 종료/학습을 앞당겨 표시하지 않는다. 시뮬레이터 JSON과 진단 재생 JSON은 서로 다른 schema다. 저장된 HEADLESS 기록은 계속 HEADLESS/REPLAY이며 실제 인간 플레이가 아니다.

## 읽기·성능·안전 경계

서버는127.0.0.1만 bind. Host 검사·POST/모니터 API token·요청 크기 한도 적용. 게임 폴더의 임의 파일 download API는 없다. Viewer는 입력한 root의 알려진 진단 파일만 읽고 기록/정책을 변경하지 않는다.

Reader는0.5초마다, 목록은2초마다 갱신. 폴더당200개 entry·파일당 poll512KiB·패킷256KiB·이력 파일17MiB 이하·보관300frame/60종료, frame4MiB/result3MiB 한도를 둔다. 여러 세션/누적 로그가 많으면 구체적인 세션 폴더를 선택한다. UI는1초마다 새 sequence만 요청한다. 잘린 행·손상 JSON·잠긴 파일은 마지막 정상 데이터를 보존하고 경고한다. 파일 mtime은 캡처/사건UTC로 사용하지 않는다.

publisher CPU 측정은 **HEADLESS forced capture**이고 reader 측정도 이 환경만의 값이다. 실제 Unity 프레임/GC/Windows 파일 잠금·권한·디스크 성능과 시뮬레이션 동시 CPU 경합은 집/학교 PC에서 확인해야 한다. fresh heartbeat가 없는데 LIVE로 표시하지 않는 것을 우선한다.

## 검증과 문제 보고

```bash
python Tests/test-dda-monitor.py
python Tests/test-dda-live-preservation.py
python Tests/test-selective-preservation.py --scope-contract Tests/Contracts/LIVE_MONITOR_SCOPE_20261119.json
python Tests/run-integration-qa.py --dotnet dotnet --output <새 폴더>
```

선택적 브라우저 검증은 Playwright+Chromium이 있는 개발 환경에서만 필요하다. 일반 사용자 실행에는 필요 없다. Windows/Unity actual compile, Game View/F8, 청음, 실제 빌드 연동은 별도 체크리스트다. 과거부터 재현되는 아라쿠스 animation/size/charge 검사3종 FAIL은 보고서에 유지하며 이 도구가 고쳤다고 주장하지 않는다.
