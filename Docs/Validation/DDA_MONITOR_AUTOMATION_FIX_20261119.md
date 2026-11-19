# LIVE 모니터와 자동검증 판별 충돌 수정 — 2026-11-19

## 기준·수정 범위

- 신규 브랜치 `fix/dda-monitor-automation-classification-20261119`.
- 사용자 지정 Windows 안정화 브랜치 `fix/dda-lab-windows-startup-20261119@de192a9d83b53e6bd0702f3e26d4c127f87184ef`에서 분기.
- 원격 조회/fetch로 main151b5256 및 Windows 기준de192a9 확인. 기존 main/통합 후보/Windows/Lab 브랜치·worktree와 미커밋 파일을 변경하지 않았다.
- 제품 변경은 **Assets/Scripts/AutoRun.cs의 공통 판별식 1곳**뿐이다. CloudSync/F8/F9/저장/게임 정책/모니터 출력 코드/씬/Prefab/아트/설정은 변경하지 않았다. 테스트와 문서를 별도 추가했다.

## 원인과 해결

이전 `HasAutomationArguments`는 인자의 `-tower` 접두어만 보고 true를 반환했다. `-towerDdaMonitor`는 Parse의 Active 활성화 옵션이 아니므로 **Active=false, IsAutomationProcess=true**가 됐다. `CloudSync.Awake`, F8 `DdaPanelInput`, 그래프 입력 보호는 IsAutomationProcess를 사용하므로 모니터 단독 일반 플레이가 자동검증으로 오인됐다. 이 결함은 앞선 도구 보존검사만으로 발견하지 못했고 이번 실제 실행 인자 테스트로 보완했다.

정확한 문자열 `-towerDdaMonitor`만 접두어 검사에서 제외했다. 다른 -tower 옵션, 알 수 없는 미래 옵션, 잘못된 `-towerDdaMonitor=...` 표기는 기존대로 보수적으로 자동검증 처리한다. 옵션 조합에서 다른 자동검증 인자가 있으면 순서와 관계없이 자동검증이다. Parse/Active, 자동 저장 격리, Boot 순서 이전 raw 판별, 네트워크 차단 정책은 그대로다. 유효한 모니터 경로는 기존 발행기 규칙대로 절대 경로여야 한다.

`DdaPanel.cs`의 IsAutomationProcess→Active 임시 변경은 이 브랜치에 이식하거나 덮어쓰지 않았다. 최종 통합 시에는 근본 수정과 함께 **원래 IsAutomationProcess 공통 guard를 유지**하는 구성을 권장한다. Active만 사용하면 Parse 이전의 자동검증 F8 차단이 약해질 수 있으며 추가한 회귀검사가 이를 확인한다. 사용자 임시 수정 커밋을 검토 없이 함께 cherry-pick하지 않는다.

## 실제 검증

모든 엔진 저장/StreamingAssets 경로는 테스트가 새로 만든 임시 경로다. 사용한 config는 `https://invalid.local`/`mock-only`의 합성 설정이며 모든 UnityWebRequest는 메모리 응답 stub이다. 실제 설정/개인 저장/CloudQueue/인증키를 열거나 운영 DB로 요청하지 않았다. 테스트용 새 UUID/소유권 키는 기존 검증처럼 격리 경로에서만 생성하고 값은 출력하지 않았다.

| 실행 | 결과 |
|---|---|
| 원래 Windows 기준de192a9 negative control | 일반/자동검증 5사례 PASS, **모니터 단독 1사례 예상 FAIL**: Parse 이전 IsAutomationProcess 오인 재현 |
| 수정 후 6개 별도 프로세스 | **모두 PASS, 총265개 assertions**. ordinary46 / monitor47 / automation43 / default-isolation43 / mixed43 / reversed43 |
| 실제 기존 DB 동작 모의 HTTP | 일반/모니터 단독의 요청 순서 **완전히 동일**: POST players→battles→pattern_stats, GET pattern_difficulty. 실패 수신 상태도 양쪽 확인 |
| 기존 통합8 suites | tutorial30 / save7 / tutorial-DDA24 / queue24 / adaptation155 / proficiency HTTP28 / selective38 / monitor34 전부 PASS |
| C# Lab | 46 PASS: 실제 현재 C# 실행·저장복원·ON/OFF·재현성 |
| Windows 실행 경로 | 9 PASS (모의 SDK 공존/UTF-8/Git 누락 등) |
| reader | 22 PASS |
| 제품 보존 | live20 PASS / 명시적 신규 계약46 PASS |
| diff check | PASS |

새 테스트는 AutoRun.cs의 **완전한 AutoRunArgs 클래스**와 DdaPanel.cs의 **변경하지 않은 DdaPanelInput 메서드**를 그대로 잘라 별도 테스트 컴파일 단위로 넣는다. 렌더링 의존 코드만 컴파일 경계 밖으로 두며 판별식/함수 본문을 재작성하거나 가짜 구현으로 교체하지 않는다. 실제 CloudSync/SaveStore/CommunityStats를 컴파일하고 Unity 이벤트·코루틴·HTTP API만 대체한다. 각 프로세스에서 Parse 전/후, F8/F9 이벤트 처리, 기존 DB 업로드·조회, 신규 기능 기본OFF, 자동 저장 격리를 확인한다. 프로세스별 인자를 사용하므로 정적 raw 플래그를 테스트가 대신 세팅하지 않는다.

기존 모니터34검사는 실제 TowerGame/UnityShim 발행·F8 읽기모델 일치·학습 불변·큐/오류 처리를 별도로 검증한다. **F8/F9 이벤트 로직과 발행기는 자동검증 완료지만 실제 Unity 화면에서의 동시 동작은 미검증**이다. Unity Editor/Windows player/F8 창·F9 그래프 렌더링·실제 LIVE 파일 연동은 여기서 실행하지 않았다. Python3.12/Linux/.NET8.0.428 환경이다. 이전 Arakus3suite FAIL은 이번 수정 범위와 무관하며 이번에 재실행하거나 해결로 보고하지 않는다.

[실행 로그·소스 hash·negative control 증거](MonitorAutomation20261119). 기존 통합 실행 manifest가 표시하는 commit은 작업 전 HEAD이며 source_sha256이 실제 실행 소스의 근거다. 모든 기존 회귀 source hash가 최종 파일과 일치함을 `verified-execution-sources.json`으로 확인했다.

## 전체 DB와 숙련도별 DB 활성화 조건

| 구분 | 실제 코드 조건 | 모니터 flag의 영향 |
|---|---|---|
| CloudSync 공통 시작 | HEADLESS/DESKTOP_PREVIEW가 아닌 빌드에서 Boot. IsAutomationProcess=false, StreamingAssets/supabase.json의 url/anonKey가 비어 있지 않고 placeholder 검사 통과 | 수정 후 monitor-only는 일반 플레이와 동일. 모니터는 DB ON/OFF 스위치가 아님 |
| 기존 전체 DB 조회 | 위 설정으로 SyncAll 실행 시 pattern_difficulty 조회. 설정 없으면 DB 설정 없음으로 종료 | ON/OFF 동일 |
| 기존 전체 DB 전송 | 유효한 소유자별 등록과 대기열, 완료 전투/카드 구매 등 기존 event. 포기 전투는 기존대로 업로드 제외. players/battles/pattern_stats/card_picks, 기존 실패 시 대기열 보존 | ON/OFF 동일. 자동검증 인자 조합이면 공통 gate에서 차단 |
| 신규 숙련도 HTTP | 동일 공통 조건 **및 config.proficiencySync=true일 때만** SyncProficiency와 FetchGroups. bool 생략 시 false | 계속 기본 OFF, 모니터로 활성화하지 않음 |
| 신규 endpoint/성공 처리 | RPC submit_proficiency, view pattern_difficulty_by_proficiency. 보호 전송은 성공 응답 true에서만 acknowledge, 409/false/오류는 보존 | 기존 규칙 유지 |
| 로컬 숙련도 대기열 | SaveStore.Save/QueueAssessments 및 유효한 평가·전투의 QueueProficiencyBattle은 네트워크 flag와 별개로 동작 | **네트워크 OFF가 로컬 outbox/키 생성 OFF를 뜻하지 않음**. 이번에는 기존 저장 정책 변경 안 함 |
| 그룹 DB를 난이도에 사용 | 실제 수신/모의 주입된 CommunityStats 그룹 메모리가 있고 기존 평가·표본 조건 충족 시 기존 우선순위에 따름. 영속 DB 캐시 아님 | 신규 네트워크 OFF는 모의 메모리 주입 차단 스위치가 아님. 정책 변경 없음 |

실제 운영 설정의 현재 값은 읽지 않았으므로 **운영 전체 DB가 지금 활성화되어 있다거나 신규 기능 flag가 false라고 관측한 것은 아니다**. 위는 소스의 활성화 조건과 기본값이다. 실제 운영 스키마/RLS/RPC/view 배포·권한·응답·네트워크·키 유효성·실데이터 집계는 모두 미검증이다. 모니터 옵션만으로 기존 DB를 끄지 않으므로 실제 게임 시험은 기존 안내대로 DB 설정 없는 격리 환경 또는 승인된 별도 테스트 백엔드에서 수행해야 한다.

## 변경 파일 및 테스트 유지

- 제품: `Assets/Scripts/AutoRun.cs` 공통 판별식만 수정.
- `Tests/MonitorAutomationTests.cs`, `Tests/test-monitor-automation.py`: 실제 소스 조각/CloudSync의 6프로세스 회귀 및 optional baseline negative control.
- `Tests/CloudSyncStatus/Stubs.cs`: REAL_AUTORUN에서만 기존 가짜 AutoRunArgs를 제외. 기존 테스트의 기본 동작은 동일.
- `Tests/test-dda-live-preservation.py`: 정확히 승인된 모니터 예외/주석을 제거하면 **전체 AutoRun 원본이 동일**함을 검사. 나머지 코드 변경 허용 없음.
- `Tests/test-selective-preservation.py`, `Tests/Contracts/MONITOR_AUTOMATION_SCOPE_20261119.json`: 신규 명시 계약에서만 해당 예외 허용, AutoRun 전체 hash 고정+원본 재구성 비교. 기존 기본 계약/이전 계약은 그대로 두며 새 코드에 이전 strict 계약을 적용하면 의도적으로 거부함.

```cmd
python Tests\test-monitor-automation.py --dotnet "C:\Program Files\dotnet\dotnet.exe" --output "C:\DdaQa\새 결과"
python Tests\test-dda-live-preservation.py
python Tests\test-selective-preservation.py --scope-contract Tests\Contracts\MONITOR_AUTOMATION_SCOPE_20261119.json
```

## 최종 main 통합 및 수동 확인

1. 독립 제품 수정 커밋의 AutoRun 판별식과 새 테스트를 반영한다. 전체 Lab/과거 게임 브랜치를 이 수정 때문에 무조건 merge하지 않는다.
2. 모니터 출력 코드와 Windows 도구 안정화가 아직 main에 없으면 각각 기존 후보에서 의존성을 검토해 선별 반영한다. 이번 커밋 자체가 그 전체를 다시 이식하지 않는다.
3. 사용자 F8 임시 커밋과 비교하고 공통 IsAutomationProcess guard를 유지한다. DdaHistoryGraph의 동일 guard도 유지한다.
4. 합쳐진 새 main 후보에서 여섯 인자 사례·통합8·Lab·보존 계약을 재실행한다. 승인된 새main 변화에 맞춰 계약을 검토하되 hash만 무조건 갱신하지 않는다.
5. DB 설정 없는 Unity/Windows 테스트 복사본에서 `-towerDdaMonitor <절대경로>` 단독: F8/F9·LIVE 동시 표시, 입력 차단/창 조절/튜토리얼/전투/종료 학습을 확인한다.
6. 모니터+자동검증 옵션에서는 F8/F9 차단, 저장 격리, 네트워크 OFF 확인. monitor-only 정상 DB 흐름은 운영 DB 대신 모의/별도 승인된 테스트 환경에서 확인한다.
7. 실제 운영 DB 연결 검증은 이번 완료 범위가 아니며 신규 숙련도 HTTP는 별도 합의 없이 활성화하지 않는다.

### 전달

새 수정 브랜치만 Push한다. main 직접 Push/병합/기존 후보 변경/환경 게시 없음. 최종 커밋·PR 시도 결과는 아래 추가 기록한다.
