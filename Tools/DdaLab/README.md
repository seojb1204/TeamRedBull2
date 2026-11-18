# 역전의 탑 — 오프라인 DDA 실험실

실제 C# 게임 코드를 실행하고 결과를 비교하는 로컬 대시보드입니다.
Unity·Supabase·개인 저장·기존 CloudQueue를 실행하거나 읽지 않습니다.
브라우저 차트는 C# 결과를 표시하며 LinUCB를 JavaScript/Python으로 재구현하지 않습니다.

## 시작

Python 3.10 이상과 **.NET 8 SDK**가 필요합니다. 설치된 뒤에는 실험에 인터넷이 필요하지 않습니다.
추가 Python/npm 패키지나 Unity는 필요하지 않습니다. NuGet 소스는 비워서 컴파일합니다.
저장소 루트에서 실행하세요.

```bash
python Tools/DdaLab/lab.py --data-dir ./TestResults/DdaLab
```

Windows에서 `python` 대신 `py`를 사용할 수 있습니다. 브라우저에서 `http://127.0.0.1:8765`를 여세요.
이 주소는 **실행한 컴퓨터에서만** 열립니다. 클라우드의 localhost 주소를 집 PC에서 열 수 있다는 뜻이 아닙니다.
포트 충돌은 `--port 8766`, dotnet이 PATH에 없으면 `--dotnet <dotnet 실행파일>`로 지정합니다.
현재 Cloud 작업 환경의 SDK는 `/workspace/.cloud-setup/dotnet/dotnet`입니다.

기존 체크아웃이 작업별 격리 환경이면 이를 사용하고, 사용자가 요청하지 않은 추가 worktree는 만들지 마세요.
이 기능 개발은 사용자의 요청에 따라 별도 worktree/브랜치에서 진행했습니다.

## 조작

1. 실험 종류·프로필·시나리오·보스·시드·반복 횟수를 고릅니다. 기본은 DDA OFF/ON 각각 실행입니다.
2. **C# 실험 실행**을 누릅니다. 실행 중에는 완료 결과를 새 실험으로 표시하지 않습니다. 오류는 화면과 실행 폴더의 `execution.log`에 남습니다.
3. 패턴 필터와 이번/다음 배정, A/b 변화량을 선택합니다. 차트 점 위에 마우스를 올리면 값을 볼 수 있습니다.
4. **동일 조건 재실행**은 C#을 다시 실행합니다. 같은 config·소스 해시의 이전 실행이 있을 때만 `동일/불일치`를 표시합니다.
5. JSON/전투 CSV/선택 CSV를 저장합니다. 이전 JSON 불러오기는 **새 실험 실행이 아닙니다**. 비교 JSON을 추가하면 난이도·성공률·모델 그래프와 표에 비교군이 표시됩니다.
6. 전투/통제처럼 종류가 다른 JSON을 비교하면 화면에 주의 문구가 나타납니다. 직접적인 효과 비교로 해석하지 마세요.

`--data-dir`의 결과는 서버 재시작 후 목록에 복원됩니다. 지정하지 않으면 운영체제 임시 폴더를 만듭니다.
JSON 불러오기는 100MB 이하입니다. 큰 실험은 회차를 나눠 비교하거나 CSV를 사용하세요.
서버는 한 번에 실험 하나만 수행합니다. Ctrl+C로 종료합니다. 실행 중 서버 종료 시 job 결과가 미완료일 수 있습니다.

## 세 가지 실험은 다릅니다

| 모드 | 실제 실행 | 실험 입력과 한계 |
|---|---|---|
| 실제 전투 루프 | 실제 TowerGame.Update/전투/판정/학습, 60Hz, 전투당 최대 120초 | privileged rule-agent가 입력을 만듭니다. Unity 엔진·인간 플레이가 아닙니다. 시간 초과는 포기 처리로 학습 제외 |
| 성공률 주입 | 실제 Prepare/Complete/Choose/Update/저장 복원 | 일반 공격 패턴마다 20회 합성 관측. 초보 .30, 일반 .70, 숙련 .95의 입력 확률이며 실제 성공률은 표본에 따라 다름. 목표 성공률 수렴 증거가 아님 |
| 조건 통제 선택 | 실제 SelectPattern/BeginPattern | trial당 100번 직접 호출, 첫 패턴에 피격 20회 주입. 페이즈·거리·쿨다운 유지/매회 해제를 통제. 학습하지 않으며 자연 전투 빈도 아님 |

실력 상승/하락은 회차를 3구간으로 나눠 novice/balanced/expert 입력 프로필을 바꿉니다.
`rounds=1~2`에서는 일부 구간이 생략되므로 6회 이상을 권장합니다.
통제 반복 실패는 성공 확률 .10과 `clear=false`를 주입합니다. 실제 전투 반복 실패는 novice 입력을 사용할 뿐 사망을 강제하지 않습니다.
이 확률들은 **시뮬레이터 실험 입력**이며 제품의 숙련도 분류 임계값/새 DDA 정책이 아닙니다.

OFF/ON은 같은 시드에서 시작하지만 전투 결과·길이·패턴이 달라져 이후 난수 소비/관측도 달라질 수 있습니다.
합성 통제 모드는 OFF/ON의 관측 입력을 같게 유지합니다. 각 회차 seed는 설정 seed + 회차 - 1입니다.

## 결과 해석

- 패턴별 이번/다음 배정 레벨, 기회·패링·회피·피격·ignored, 계산 reward, 배정 근거를 봅니다.
- 계산 reward가 있어도 DDA OFF·포기·선택 probe에서는 학습하지 않습니다. 실제 갱신은 A/b 전후와 변화량으로 확인하세요.
- 패턴 선택 후보 노출·실제 선택·확률합, 기본 가중치·배수·이유를 봅니다. 확률합은 조건별 기대 횟수이며 작은 표본에서 실측과 다를 수 있습니다.
- `selections`는 후보와 반복 제외 행입니다. 기본 거리/쿨다운/페이즈 gate로 빠진 패턴의 개별 제외 사유는 현재 제품 로그가 제공하지 않습니다.
- `starts`는 weighted selection 외의 강제 패턴도 포함하는 공격 시작 표본입니다. 각 Update 직후 patternSerial/current를 읽으며 실제 Unity 프레임 기록은 아닙니다.
- 자연 전투의 거리·페이즈는 **프레임 종료 표본**입니다. 정확한 gate 평가 순간의 거리라고 주장하지 않습니다.
- HEADLESS는 Catalog를 씁니다. 실제 Unity의 scenePresentation/BossDefinition과 같은 엔진 실행은 아닙니다.
- 전투 진행에 따른 일반 보상·성장도 실제 코드의 결과입니다. 학습만 독립 관찰하려면 통제 모드를 사용하세요.

각 실행 폴더: `config.json`, `result.json`, `raw.json`, `battles.csv`, `selections.csv`, `execution.log`, `job.json`.
JSON에는 실제 A/b 배열, 다음 배정, 소스 커밋·SHA-256·결과 fingerprint가 포함됩니다.
실행 시 소스가 바뀌면 결과를 거부합니다. timestamp·작업 ID·호스트 경로는 결정적 비교에서 제외합니다.
정확한 소스 해시별 빌드만 재사용합니다. 이전 소스 바이너리로 새 소스 결과를 표시하지 않습니다.

## CLI / 자동화

```bash
python Tools/DdaLab/lab.py --config Tools/DdaLab/example-control.json --data-dir ./TestResults/DdaLab
python Tests/test-dda-lab.py --dotnet dotnet
python Tests/run-cold-start-integration.py --dotnet dotnet
python Tests/run-audio-lifecycle.py --dotnet dotnet
```

`example-combat.json`, `example-selection.json`도 제공합니다. CLI는 실패 시 0이 아닌 종료 코드를 돌려줍니다.
선택 항목은 JSON 스키마 후보의 `mode/profile/scenario/dda/seed/rounds/boss/phase/distance/releaseCooldowns`만 받습니다.
서버는 loopback에만 바인딩하며, POST는 로컬 UI 토큰과 허용 Host가 필요합니다. 외부 공개용 서버가 아닙니다.

선택적 UI 회귀검사는 Playwright/Chromium이 이미 있는 환경에서 실행합니다. 실험실 사용 자체에는 필요하지 않습니다.

```bash
python Tests/test-dda-lab-ui.py --chromium /path/to/chromium --output ./TestResults/DdaLabUi
```

샌드박스에서 .NET `NuGet-Migrations` named mutex 오류가 나면 지원되는 명령 권한 조정을 사용하세요.
검사를 우회하거나 TLS·서명 검증을 끄지 마세요. Unity 전체/F8 화면/청음/Windows/실제 DB 검증은 별도입니다.
