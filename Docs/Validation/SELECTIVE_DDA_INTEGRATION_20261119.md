# 11/18 DDA 기능 선별 통합 후보 — 2026-11-19

기준 main **151b5256d8203ebd28f7b8466738f8c12d5ea973**, 브랜치 `integration/dda-selective-20261119`, 작업 폴더 `/workspace/dda-selective`. 시작 및 전달 전 원격 main을 확인한다. AGENTS/CONTRIBUTING을 따르고 기존 5개 worktree와 사용자 파일을 보존했다. main/이전 DDA 브랜치에 쓰지 않았다.

**전체 DDA branch merge나 24e6a2c cherry-pick을 하지 않았다.** 24e6a2c의 함수·자료구조 차이와 최신 main을 비교해 부분 적용했다. Scene/Prefab/아트/보스 데이터/설정/기존 CloudQueue·개인 저장·인증값·운영 Supabase에 접근하거나 변경하지 않았다.

## 1. 통합한 기능

| 요청 기능 | 적용 내용·주요 파일 |
|---|---|
| 실제 튜토리얼 평가 | TutorialAssessment의 기존 novice/normal/expert, 패링·회피·조작·active 시간 점수 유지. TutorialSession.Preview로 잠정 score/confidence를 원본 확정 없이 계산 |
| 시간 분리 | active/elapsed/explanation/paused. main의 패링 시범·성공 피드백도 설명 시간에 포함하며 active에서 제외. 실제 unscaled delta 사용 |
| 빈 DB 첫 배정 | 유효 최초 평가+confidence≥.5일 때 잠정 novice -1 / normal0 / expert+1. 미평가/스킵/잘못된 평가0, 저신뢰도0. 첫 만남 보호[-1,+1] 유지 |
| 초기 근거 우선순위 | OFF0 우선. 개인 근거 → 충분한 해당 그룹DB → 전체DB → 튜토리얼 → 기본0. 그룹20기회/3익명소유자, 전체20기회/1프로필. 늦은 수신은 활성 배정을 덮지 않음 |
| 숙련도 전송/보호 | 별도 capability-bound outbox/receipt/key, 실제 RPC 전송과 그룹 수신, 불변·멱등·실패 보존. 기존 진행/UUID/CloudQueue 정책 유지 |
| 튜토리얼 F8 | 실제 Preview/raw/점수·가중치/confidence/시간·로컬/전송 상태. 튜토리얼에서 패널 호출 추가, F9 관측 펼침 |
| 현재 공격 | BeginPattern에서 실제 Lv/선딜/피해/쿨다운 snapshot 및 DdaApplied CSV. Result의 stale 공격을 현재로 표시하지 않음 |
| 종료 학습/다음 예측 | 실제 A/b 변화·hash·context·arm 점수·학습 제외/보호 이유·다음 배정 trace, DdaLearning JSON. 결과 미리보기는 수신 revision에 맞춰 갱신 |

튜토리얼 등급·confidence·-1/0/+1 및 표본 충분 기준은 기존 11/18의 **보수적 v1 가설**이다. 인간 숙련도 정확도/60~80% 대응률 수렴을 검증한 확정 정책으로 주장하지 않는다. 튜토리얼 관측을 일반 전투의 LinUCB Update나 DB 보스전으로 만들지 않는다.

개인 LinUCB의 Reward/alpha/45%·90%·연패 기준과 한 단계 이동 제한은 변경하지 않았다. CandidateScore는 기존 Choose의 동일 수식을 진단에서도 재사용하도록 추출한 것이다. 신규 telemetry만 기본 OFF이며 기존 CloudSync 동작은 유지한다. SQL은 파일만 포함하고 적용하지 않았다.

## 2. 제외한 기능

- **DDA Lab 전체**와 166fe3f의 웹 UI·시나리오·실험기·장기 결과 exporter.
- 이전 DDA 통합의 WorldArtist/ScenePresentation 수정, 과거 아트·보스 balance·일괄 source 교체.
- 2,040전투·그래프·옛 QA 보고서의 일괄 반입. 이번에는 실제 새 후보의 테스트 결과만 기록한다.
- 외부 실시간 모니터/IPC, 보상·임계값 재설계, DB 운영 활성화·새 retry/영속 캐시 정책.
- 새 음원·에셋·씬·Prefab·배포 빌드.

### Lab 의존성 판정

166fe3f는 기존 `Tools/DdaLab/Experiment.cs/lab.py/index.html/README.md` 위의 개선 commit이다. main에는 그 기반이 없어 개선분만으로 독립 실행할 수 없다. 제품 DDA에는 Python/JS/web Lab 의존성이 없다. 따라서 이번 후보에 **Tools/DdaLab은 존재하지 않으며** 별도 통합 대상으로 남긴다. Lab 테스트 전용 가상 평가 DTO/adapter도 제외했고 수신/튜토리얼 검사는 실제 TutorialAssessment로 바꿨다.

## 3. 필수 의존성으로 함께 이식한 부분

24e6a2c가 이전 Lab 진단에 의존하던 다음 **제품 코드**만 가져왔다.

- CommunityStats 수신 상태/HTTP 대 메모리·모의 구분/atomic replace/revision/cancel finally.
- DdaPreviewStamp와 패널 캐시 invalidation: 그룹 통계 도착 후 다음 전투 예측 갱신에 필요.
- 실제 DdaDecisionTrace/DdaTraceReport/DdaLiveSnapshot 및 비치명적 로그 writer.
- SaveStore.Proficiency*와 CloudSync의 opt-in RPC/그룹 view, 신규 migration/checklist.
- 기존 main에는 없던 실제 SaveStore+CloudSync 모의 HTTP/소유권/튜토리얼 연결 테스트 실행기.

기존 main의 선택 가중치·선택 CSV·F8/F9·프로필 기능을 중복 구현하지 않았다. 보호 outbox는 기존 CloudQueue와 별도다. 신규 HTTP 테스트의 좁은 shim에는 Catalog가 참조하는 PhanesUltimate 상수만 있고, 실제 제품 상수와 동일한지 보존 검사가 확인한다. 전투/학습 알고리즘 대체 구현이 아니다.

## 4. 충돌·최신 main 보존

| 충돌/경계 | 해결 |
|---|---|
| BossPatternSystem.Reset/BeginPattern이 #51 파네스 상태로 변경됨 | 옛 patch 적용 검사 실패 후 수동 함수 단위 삽입. phanesUltimateUsed/arakusChasing·별 궁극기 windup/damage 분기는 유지하고 snapshot8줄만 추가 |
| TutorialSession에 AwaitingThreatFeedback/CompleteThreatFeedback가 추가됨 | main의 threat 판정/advance를 그대로 두고 Tick에 instructionFeedback 인자 추가. 시범/피드백 시간을 active에 넣지 않음 |
| TutorialBeforeUpdate의 시범·성공 연출 gating | 해당 플래그를 새 clock 분류에 전달. 새 HUD/도움말·설정 동작 유지 |
| GameUI의 fade/Settings/Help/DrawGameUi | 기존 함수·그리기 순서를 유지하며 tutorial guide 뒤 F8 호출만 삽입. 전체 파일 교체 안 함 |
| HEADLESS Application.isPlaying 누락 | UnityShim에 bool 추가. 기본false로 기존 비실행 단위검사 의미 유지, 별도 true 조건에서 실제 queue/fade 전환도 검사 |
| CloseSettings가 GameUI source allowlist 밖에 있음 | **main의 실제 SettingsProfile/CloseSettings를 내용 그대로 TowerGame으로 이동**. no-op stub으로 저장 효과를 지우지 않음 |
| 튜토리얼 종료 재시도 Enter가 새 보스 입장에도 사용됨 | Update에 wasTutorial guard 추가, main transition 조건과 함께 유지. 실제 기존 연결 테스트로 재현/회귀 검증 |
| F8 완료 평가 cache가 reset/restore 프로필과 혼동 가능 | 완료 평가를 현재 save의 first/replay에서 조회. 다른 프로필로 교체하면 이전 평가를 표시하지 않는 검사 추가 |

다음 main 파일은 **바이트 동일**하다: LobbyCards, ArakusAnimationSelection/Visuals, ActorView, WorldArtist, PhanesPose, AutoRun, PixelAgentBridge, Soundscape. 전체 Assets/Resources·Data·Scenes·Prefabs·ProjectSettings 변경도 없다. 실제 TickAction/BeginAction/HandleEscape/QueueScreenChange/TickTransition, tutorial opponent/feedback, Phanes star 생성·실행, Reward/Update/Value, 원래 업로드 소유권·completed-battle commit과 scoring 수식을 source 비교로 보호한다.

[보존 검사](../../Tests/test-selective-preservation.py)와 [고정 scope](../../Tests/Contracts/SELECTIVE_SCOPE_20261119.json)는 exact main SHA와 정확한 후보 파일 hash를 사용한다. commit 후에도 예상치 않은 변경이 무조건 PASS가 되지 않는다. 정적 보존은 Unity 실제 실행의 대체물이 아니다.

## 5. 실제 재실행 결과

코드는 실제 제품 C#, .NET8+UnityShim/모의 HTTP, 별도 신규 임시 저장에서 실행했다. 실제 Supabase config나 사용자 save/queue는 읽지 않았다.

| 검사 | 최종 결과 |
|---|---:|
| 선택 통합 연결 E2E | **38 PASS** |
| 튜토리얼 상태 / 저장 / 실제 game-DDA 연결 | 30 / 7 / 24 PASS |
| 기존 큐 소유권 / 신규 보호 HTTP | 24 / 28 PASS |
| 실제 적응·fallback·clock·평가 | 155 PASS |
| 실제 Cold receive→CommunityStats→Prepare | 23 PASS |
| Headless / 공유 궁극기 | 739 / 37 PASS |
| main의 DDA 학습 / 빈도 / 손상 방어 | 248 / 185 / 31 PASS |
| Phanes | 3,188 PASS |
| 이력 / 패널 window model / 프로필 아카이브 | 47 / 205 / 31 PASS |
| 정확한 main 기능·scope 보존 | 45 PASS |
| 아라쿠스 애니메이션 / 크기 / 돌진 polish | **FAIL 3종 — 원본 main에서도 동일 재현** |

신규 E2E는 novice/normal/expert 각각 실제 TutorialSession 평가 → FinishTutorial의 로컬 저장 → 실제 StartBattle -1/0/+1 → PlayerHit를 통한 패링20회 → 강제 클리어 정산 → 학습1회 → 저장/로드 → 개인 관측에 따른 다음 배정을 검증한다. 시범 피드백 제외 clock, 실제 CloseSettings의 원래 프로필 저장, transition queue, 새 Phanes 특별 windup snapshot, 계승R의 실제 friendly 별5개도 확인한다. **입력/충돌 계약을 통제한 검사이며 인간 입력·자연 전투 완주의 증거가 아니다.**

248은 이번 main의 학습 검사 개수다. 과거 후보269/2040 등을 이번 실행 결과로 표시하지 않는다. SQL은 11/18 원본과 동일한 migration을 반입했지만 **이번에 격리 DB조차 실행하지 않았다**. 신규 DB 관련 재검증은 실제 C#과 모의 HTTP28개다.

### 남긴 FAIL과 원인 분류

1. `ArakusAnimationTests`: `walk follows distance instead of wall clock`.
2. `ArakusSizeTests`: `cleave actual hit boundary 1 3.9899998 0`.
3. `ArakusChargePolishTests`: `windup pose`.

정확한 main151b525 소스를 별도 추출하고, main의 실제 settings 두 메서드를 수정 없이 partial로 포함하며 isPlaying shim만 보완해 같은 세 검사를 실행했다. **세 오류가 모두 동일하게 재현됐다.** 원래 소스나 assertion을 고치지 않았다. 따라서 이번 DDA 이식으로 새로 생긴 회귀와 구분하지만, 실제 게임 결함인지 낡은 검사 가정인지는 해당 전투·아트 담당 확인 전 확정하지 않는다. 이 후보를 전체 테스트 green/발표 승인이라고 보고하지 않는다.

초기 새 이식 단계의 실패도 보존했다.

- HTTP fixture의 GameData→BossPatternSystem 상수 의존 compile: 좁은 fixture 상수 추가 및 실제품 상수 일치 검사로 해결.
- 튜토리얼 Enter 재사용: wasTutorial guard로 해결.
- Headless의 옛 기본 이유 문자열: 새 이유에 맞추고 모델 serialization 불변 검사도 강화.
- '로비1회만 강화' 기대: main PR51 commit5c51ba3가 의도적으로 cardChosen gate를 제거하고 offer를 비우는 근거 확인 후, 정확한 gold/stack/빈 offer 보존 및 비용 없이 새 offer3개 재열림을 검사하도록 수정. 제품 LobbyCards는 변경하지 않았다.
- 새 transition 검사 fixture가 잠긴 보스1을 요청: unlockedBoss=4를 명시해 실제 transition 경로를 검사.

자동 승인 검토는 처음의 일괄 테스트 수정 명령을 '회귀 기준 약화 위험'으로 차단했다. 우회하지 않고 위 PR51 diff와 제품 무변경 근거를 먼저 확인한 후, 보존해야 할 실제 정책을 더 구체적으로 검사하는 수정으로 재검토를 통과했다. 실패 검사를 삭제하거나 조건 없는 PASS로 바꾸지 않았다.

[실행 요약/소스 hash](SelectiveDda20261119/summary.json), [최종 연결 suite](SelectiveDda20261119/cross/manifest.json), [main 기준 실패 대조](SelectiveDda20261119/baseline/manifest.json), 해당 폴더의 candidate-failures/로그에 결과를 남겼다. 초기 시도 기록은 initial/에 있다.

## 6. 재현

저장소 root에서 .NET8 SDK를 지정한다. 결과 폴더는 새 경로를 사용한다.

```bash
python Tests/run-integration-qa.py --dotnet <dotnet> --output <새 결과 폴더>
python Tests/run-cold-start-integration.py --dotnet <dotnet>
python Tests/run-ultimate.py --dotnet <dotnet> --output <새 결과 폴더>
python Tests/run-dda-learning-verification.py --dotnet <dotnet> --label selective --output <새 결과 폴더>
python Tests/test-selective-preservation.py
```

부가 회귀는 run-dda-frequency.py / run-dda-robustness.py / run-phanes.py / run-dda-history.py --suite history|panel|profiles. 오래된 art/size/polish 검사3종은 위 알려진 FAIL을 재현하므로 임의로 정상 결과를 기대하지 않는다.

## 7. Unity·Windows 수동 검증 체크리스트

Unity6000.4.0f1·Game View·실제 키 입력·오디오·Windows 빌드는 이번 환경에서 **미실행**이다. HEADLESS는 실제 F8 렌더 코드를 컴파일/검증한 것이 아니다.

- [ ] 실제 DB 설정을 제외한 테스트 복사본, 새 Windows 테스트 사용자/VM 등 **개인 저장과 분리된 프로필**에서 정상 일반 실행. 기존 사용자 save/CloudQueue를 초기화하지 않는다.
- [ ] Editor 실제 compile 오류0. main의 transition/fade, 설정 저장·ESC/도움말, 반복 강화, 아라쿠스 ArakusNew→V2→V1, 파네스 phase2 별/R계승 유지.
- [ ] 튜토리얼 설명·일시정지·패링 시범·성공 피드백/실습에서 clock 분류/score/confidence/원시 관측 실시간 확인. 스킵·중단·재시도·재평가·저장 실패·재시작 보존.
- [ ] 빈 DB 초보-1/일반0/숙련+1(저신뢰0), 개인 학습 이후 우선, OFF0/포기/no-observation/연패 보호. 모의 통계를 실제 수신처럼 표시하지 않음.
- [ ] 튜토리얼·보스전·결과·프로필 reset/restore에서 F8/F9/scroll/resize/저해상도/패널 위 마우스 입력·메뉴 겹침. **자동화 -towerDataRoot 등은 기존 정책상 F8을 숨기므로 그 실행으로 F8 정상 입력 검증을 주장하지 않는다.**
- [ ] 현재 공격 actual Lv/windup/damage/cooldown을 DdaApplied CSV와 대조. 결과의 next/reason/A·b 진단과 DdaLearning JSON을 구분. 늦은 수신은 현재 공격을 바꾸지 않음.
- [ ] Q/W/E 게이지·R 시전 감쇠·검 강화·상속, 모든 보스·회복약·반복 강화·세이브/복원 smoke. 알려진 아라쿠스3종 FAIL의 실제 동작/검사 가정 판정.
- [ ] Windows 빌드·실제 전체 시연 흐름·BGM/SFX/일시정지 복귀 청음. 검증한 commit과 빌드 로그/ZIP hash 연결.

신규 telemetry는 기본 OFF를 유지한다. 별도 staging/보안/키 백업·Windows ACL·PostgREST 검토 후 활성화를 판단한다. [migration 절차](../DB/PROFICIENCY_MIGRATION_20261118.md). 운영 DB 적용·실제 데이터 건수/사용자 충분성은 확인하지 않았다.

## 8. Git 전달·남은 위험

새 브랜치만 commit/push하고 Draft PR만 시도한다. main 직접 merge/push와 기존 branch 변경은 하지 않는다. 현재 main의 알려진 세 FAIL과 최신 Unity/Windows 미검증 때문에 **검토용 통합 후보**이며 발표/배포 승인본이 아니다.

다음 우선순위: Unity actual compile/UI/보스·저장 흐름 확인 → 아라쿠스 baseline FAIL 판정 → 검증한 단일 commit 동결. DDA Lab은 별도 작업으로 분리했고 신규 개발을 임의로 시작하지 않는다.

제품/테스트 commit: `6bd9292cb513a1312c4e37483d58fa16c10f876e`. 실행은 commit 전 동일 소스에서 했으므로 일부 manifest의 commit 필드는 기준151b525를 가리키며, 실행 소스 SHA256과 exact scope로 실제 후보 바이트를 기록했다. commit 후 보존 검사45개도 다시 통과했다.

### 전달 결과

- 제품·필수 테스트: `6bd9292cb513a1312c4e37483d58fa16c10f876e`.
- 검증 보고서/실제 로그: `aa847c3`.
- `integration/dda-selective-20261119` 원격 Push 성공.
- Draft PR 생성은 `Post https://api.github.com/graphql: Forbidden`으로 실패. PR 번호가 생성됐다고 보고하지 않는다.
- [main 대비 비교](https://github.com/seojb1204/TeamRedBull2/compare/main...integration/dda-selective-20261119).
- [준비한 PR 본문](SELECTIVE_DDA_20261119_PR.md).

최신 main 기반 자체 commit만 추가했으며24e6a2c/166fe3f/7b43148의 전체 이력을 merge/cherry-pick하지 않았다. DDA Lab은 제외되어 있다. main 병합·환경 게시·운영 DB 적용은 하지 않았다.
