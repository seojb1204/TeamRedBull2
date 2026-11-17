# DDA·Cold Start·시뮬레이터 점검 — 2026-11-17 KST

## 기준과 변경 범위

원격 `origin/main`을 실제 fetch한 기준은 `83d9085fb44a6218e721b68b16d620c4669f5d95`이다.
원래 `/workspace/TeamRedBull2`의 `work` 브랜치는 깨끗했고 기존 worktree는 하나였다.
`/workspace/dda-simulator-coldstart`, `test/dda-simulator-coldstart-20261117`에서 작업한다.
게임 코드·씬·Prefab·아트·오디오·정책·DB·세이브 스키마는 수정하지 않았다.
튜토리얼을 구현하지 않았으며 환경 설정 초안 수정·환경 게시도 하지 않았다.

Git ancestry로 `aed9811` 통합 후보와 과거 PR #13~#19의 문서상 HEAD 전부가 현재 main의 조상임을 확인했다.
통합 후보는 `1f4ff2f`를 거쳐 PR #24(`5b3b3b8`)로 포함됐다.
각 과거 PR의 GitHub closed/merged 상태·종료 사유까지 확인한 것은 아니다.

| 변경 | 현재 main 포함 근거 | 실제 변경 |
|---|---|---|
| #33 | merge `bd06d5effdd160e04ddefb0fe4804e096cad1fd0`, 두 부모 사이 diff 확인 | 전투 내 유효 관측을 이용한 빈도 가중치, 반복 제한, 거리·위협 회피 관측, 자동검증 선택 근거 |
| #35 | merge `a2363339986384c10187854e52b1ab583ec734f4`, 두 부모 사이 diff 확인 | F8 직전 선택 근거·가중치·확률, 수동 플레이 로컬 CSV, 관련 회귀검사 |

Git read는 성공했다. `gh api user`, pulls/33, pulls/35는 `Forbidden`이었다.
인증 계정·필수 리뷰·브랜치 보호·병합 권한은 미확인이다. Git의 branch push와 API 접근은 별개이며 main에 쓰기로 권한을 시험하지 않는다.

## 현재 실제 연결

`StartBattle → Catalog 또는 BossDefinition.CreateRuntime → DdaBattleSession.Prepare → stats.level`
이후 `BeginPattern`이 해당 레벨을 선딜·피해·쿨다운·일부 횟수에 적용한다.
`PlayerHit` 등의 실제 관측 → `Complete` → `BanditModel.Update` → 패턴 기억 → 저장 → 다음 Prepare로 이어진다.
일반 끝내기 경로와 세션 자체 모두 중복 완료를 방어하며, 무관측·손상 관측·포기·DDA OFF는 학습하지 않는다.
과거 인수인계의 “직접 Complete 중복 방어 없음/무관측도 battles 증가”는 현재 코드와 다르다.
기존 검증기의 해당 두 항목을 단순 OBSERVED 설명에서 실제 회귀 단언으로 바꿨다.

모델은 여전히 **보스별 공유 6차원/5-arm**이고 패턴 기억만 패턴별이다.
context는 상수, 패턴 실패율, 보스 평균 실패율, 지난 레벨/2, 성장 전투력/2, 연패/5다.
정책 순서는 첫 전투 → 3연패 보호 → 성공률 <45% / >90% 보정 → 중간 구간 LinUCB다.
LinUCB는 UCB 계수 .18, prior, 이동 비용 .012, 인접 한 단계 제한을 사용한다.
reward는 성공률 60~80%에서 1이며 구간 밖 거리에 따라 감소한다. 승리 자체가 reward는 아니다.
실제 C# `Choose/Update/Prepare/Complete`를 실행하며 Python 정책 재구현으로 대신하지 않았다.

#33 빈도는 arm 레벨을 직접 바꾸지 않는다. 현재 전투의 실패 관측으로 기본 가중치에 최대 1.35배를 준다.
4회 미만·잘못된 관측·DDA OFF·체력 30% 이하는 중립이다.
최근 6회 중 2회인 패턴을 대안이 있을 때 제외하며, 거리·페이즈·쿨다운·직전 패턴 등의 원래 gate도 유지한다.
레벨은 쿨다운 등에 영향을 주므로 선택 가능 집합에는 간접 영향이 있다.
선택 변화는 관측 분포를 바꾸고, 그 관측이 다음 학습에 쓰인다. “빈도와 학습 데이터가 독립”이라는 뜻은 아니다.
위협 없는 miss는 성공 대신 ignored가 된다. 투사체 등 일부 위치 회피는 보수적으로 제외돼 패턴별 관측 편향이 남는다.

## Cold Start 경계

실제 경로는 `CloudSync.Awake → SyncAll → FetchDifficulty → JSON 행 변환 → CommunityStats.Replace → Decide/Prepare`다.
첫 시작의 비동기 수신을 기다리는 전투 시작 barrier는 없다. Prepare 후 도착한 데이터는 진행 중 배정을 바꾸지 않는다.
공동 통계 20기회 이상, 개인 관측 없음일 때 공동 성공률을 context에 쓴다.
보스 전투 수가 0이면 추천 레벨을 -1~+1로 제한한다. 개인 완료 전투·관측 이후에는 개인 기억이 우선한다.
패턴 누락·표본 부족은 개인 기본값, DDA OFF는 레벨 0이다.

공동 통계 캐시는 정적 메모리뿐이며 디스크 영속 캐시·TTL·숙련도별 집계가 없다.
실패 시 이전 메모리 값과 마지막 성공 시간을 보존하고, 정상 빈 배열은 비운다.
요청 timeout은 6초다. 업로드 파일은 다음 실행/새 enqueue 등의 SyncAll에서 재전송한다.
주기적 retry/backoff나 수신 대기 타이머는 없다. DdaHistoryRuntime의 5초 retry는 이력 저장 관련이며 DB 재시도가 아니다.

기존 CloudSyncStatus는 실제 CloudSync 코루틴 + 모의 HTTP·JSON·CommunityStats를 실행한다.
이번에 수신 중단 시점의 미반영, 재개 후 8개 필드 전달, 실패 후 동일 entry 보존, 명시적 재호출 복구를 추가했다.
별도 learning 실행기는 실제 CommunityStats와 Prepare를 검사한다.
**두 경계를 한 실행 파일로 연결한 통합 검사나 Unity 비동기 타이밍 검사가 아니다.**
최소 후속 방안은 기존 두 스텁의 공통 인터페이스를 정리해 실제 CommunityStats/Prepare까지 한 하네스에 포함하는 것이다.
제품 추상화가 필요해지면 먼저 원인·최소 변경안을 승인받는다.

## F8·기록·저장

F8은 stats의 실제 배정 레벨, 개인 기억, Decide로 계산한 다음 전투를 보여준다.
#35는 직전 선택 후보의 관측·기본 가중치·배수·확률·이유·선택 표시와 CSV 상태를 더했다.
기본 gate로 제외된 패턴은 패널에 표시하지 않는다. 확률은 실제 관측 빈도와 같은 값이 아니다.
선택 이력은 마지막 128행, 디스크는 세션 파일별 16MB 한도다. 동기식 파일 append이므로 Unity 프레임 비용은 별도 측정 대상이다.
CSV 쓰기 오류가 전투를 중단하지 않는 회귀검사도 통과했다.

결과 미리보기는 result·DDA 설정·BossLearning 참조 변경 시 갱신하므로 기존 초기화/ON-OFF 캐시 보정은 남아 있다.
공동 통계 수신 자체는 무효화 조건에 없다. 결과 화면에서 늦은 수신 이후 미관측 패턴 예측이 낡을 가능성은 정적 조사 사항이며 GUI 재현은 미검증이다.
초기화는 learning만 비운다. 성장·클리어·전투 기록·이력 JSON·CSV를 삭제하지 않는다.
이력 recorder는 BattleLogged를 관찰하며 학습 메서드/전투 RNG를 호출하지 않는다.
이력의 GUID·시각은 결정적 비교에서 제외한다. 기존 이력 검사에서 모델 비간섭·저장 복원을 검사한다.

## 이번 검증기

- `Tests/DdaLearningVerificationTests.cs`: 기존 검사를 유지하면서 실력 하락 입력, 무관측/중복 완료 단언, 늦은 공동 통계·누락 패턴·DDA OFF·개인 레벨 우선순위 추가.
- `Tests/DdaSelectionAuditTests.cs`: 실제 SelectPattern/BeginPattern, seeds 17/42, DDA ON/OFF, phase 1/2, 거리 1/4/8, 쿨다운 유지/각 probe 해제 조건. cleave 실패 관측은 인위적으로 주입한다.
- `Tests/CloudSyncStatus`: 실제 수신 코루틴의 전달 필드와 중단/재개 검사 보강.
- `Tests/run-dda-system-audit.py`: 허용 목록 컴파일, 격리 저장, 빈 새 출력 디렉터리 강제, 두 번의 학습/선택/전투 실행, 소스 SHA-256·기준 커밋·시드·명령·exit code, 실행 중 소스 변경 감지.

선택 probe는 0.5초마다 SelectPattern을 직접 부르는 **조건 통제 실험**이다.
공격 완료·피격을 자연 진행한 전투 빈도로 해석하면 안 된다. 기존 PlaytestAgentTests의 실제 Update-loop 전투 240회는 별도로 실행한다.
학습 실험은 초보/일반/숙련에 해당하는 합성 성공률, 상승·하락, 반복 실패, DDA OFF, 패턴별 혼합 입력이다.
실제 전투 agent는 novice/balanced/expert이며 인간 숙련도나 성공률 수렴의 증거가 아니다.

선택 CSV는 후보 여부·선택·기본 가중치·배수·확률·이유·배정 레벨·실제 선딜/피해를 남긴다.
기본 gate 제외의 개별 사유는 제품 코드가 노출하지 않아 `not in decision evidence`로 남긴다. 임의 사유를 만들어내지 않는다.
조건별 selected/eligible/확률합(expected count)을 JSON에 집계한다. 작은 표본에 유의성·개선 효과 판정을 하지 않는다.
학습 CSV에는 context, reward, A/b 전후 해시·델타, 다음 레벨이 있다.
학습 갱신/저장 재사용/레벨 변화는 각각 검사하며 **인간 적응 성능은 미검증**이다.

HEADLESS는 Catalog를 사용한다. 실제 Unity는 scenePresentation의 BossDefinition을 사용할 수 있다.
실제 C# Catalog를 export해 5개 `.asset`의 숫자 패턴 필드를 비교했으며 차이는 0개였다.
이 검사는 Unity 역직렬화·씬 연결·문자열/보스 상위 필드 전체 동등성을 보장하지 않는다.
기존 learning 실행기의 현재 의존 목록은 컴파일되며, 새 선택 probe는 ArakusAnimationSelection도 포함한다.
전투/agent RNG는 시드로 초기화한다. UnityShim의 별도 정적 RNG는 시각 mote에도 쓰이므로 바이트 비교 대상은 정책/전투 결과이며 화면·particle state는 아니다.

## 실행 및 결과

```bash
cd /workspace/dda-simulator-coldstart
python3 Tests/run-dda-system-audit.py \
  --dotnet /workspace/.cloud-setup/dotnet/dotnet \
  --output /workspace/dda-audit-results/system-final
```

새 실행에는 다른 output 경로를 사용한다. .NET 8.0.428, net8.0.
기본 게임 하네스는 HEADLESS, CloudSync 하네스는 심볼 없이 실제 CloudSync와 별도 스텁을 컴파일한다.
이 환경은 .NET named mutex의 샌드박스 제한 때문에 지원되는 명령 escalation으로 실행했다.
설정·비밀값·개인 저장·기존 업로드 큐를 읽지 않았고 실제 Supabase에 접속하지 않았다.

| 검사 | 실제 결과 |
|---|---|
| 수정 전 기존 통합 | 9/9 suite PASS; Headless 738, 기존 DDA 248 포함 |
| 보강된 실제 C# 학습 | 각 실행 269 PASS / 0 FAIL, 두 실행 |
| 실제 선택 조건 probe | 각 실행 2,968 decision / 52,800행, 두 실행 |
| Update-loop agent | 각 실행 240전투, 두 실행; seeds 17/42를 바탕으로 round별 +0~3 |
| 빈도 기존 회귀 | 185 PASS |
| DDA robustness | 31 PASS |
| 실제 CloudSync + HTTP/CommunityStats double | 16 PASS |
| 결정적 산출물 비교 | 학습 CSV·60개 모델 JSON·선택 CSV·Catalog JSON·전투 CSV, 64/64 동일 |
| Catalog/에셋 | 숫자 패턴 필드 차이 0 |

겹치는 검사 수를 더해 독립 검증 건수로 표시하지 않는다. 로그·타임스탬프·임시 경로·세션 ID는 재현성 비교 대상에서 제외한다.
세부 manifest/명령/log/CSV는 `/workspace/dda-audit-results/system-final`이며 전체 대용량 CSV는 Git에 추가하지 않는다.

## 문제·심각도·기능 동결

| 항목 | 판정/심각도 | 최소 후속 조치 |
|---|---|---|
| 실제 Unity 컴파일/F8/전투/오디오/Windows 실행 미검증 | 제출 판단의 높은 검증 공백 | 6000.4.0f1과 라이선스·Windows 모듈/실행 환경을 갖춘 팀원이 오프라인 복사본 검증 |
| 첫 전투 전에 DB가 도착한다는 보장 없음 | 중간, 정책상 기본값 fallback은 정상 경로 | 첫 판 공동 통계 적용이 필수 요구인지 합의; 필수라면 별도 승인 후 제한 대기/fallback 설계 |
| 메모리 캐시 TTL/영속/주기 retry 없음 | 현재 기능의 한계, 곧바로 결함으로 단정하지 않음 | UI/발표에서 구현됐다고 주장하지 않기; 필요 시 별도 정책 결정 |
| 결과 화면 늦은 통계와 미리보기 무효화 | 중간 가능성, 정적 근거만 있음 | Unity 재현 후 CommunityStats revision 기반 최소 UI 갱신안 검토 |
| 패턴 관측 편향/희소 후보의 반복 fallback | 측정 해석 주의 | 조건부 빈도·기회 수와 함께 보고; 밸런스 임의 수정 금지 |
| #33 문서의 “수동 파일 쓰기 없음/F8 그대로” | #35 이전 설명으로 현재와 다름 | 본 보고서와 #35 문서를 현재 근거로 사용 |
| 오디오 lifecycle 별도 기존 실패 | 이전 환경 온보딩에서 확인, 이번 DDA 검사 대상 아님 | 스텁 1000Hz와 Render 허용 8000Hz 이상의 불일치; 별도 테스트 수정 업무 |

**11월 20일 전 필수:** Unity 담당자의 실제 컴파일/플레이/F8 확인, Cold Start 첫 판 보장 여부와 fallback 표현 합의,
DB 실제 접속을 제외한 검증 경계 합의, 튜토리얼 담당자와 아래 계약 확정, GitHub 리뷰·필수 체크 확인.
정상 기능 동결을 본 오프라인 검사만으로 선언하지 않는다.

**동결 이후 가능:** 영속 공동 통계 캐시·TTL·backoff, 숙련도별 DB 통계/스키마, 실사용자 적응 효과 실험,
시각 RNG까지 포함하는 전체 상태 재현성, 동기 CSV I/O의 성능 개선. 합의된 출시 요구라면 우선순위를 다시 결정한다.

## 튜토리얼 데이터 계약 제안 — 미구현

현재 소스에서 확인한 것은 `tutorialSeen` 도움말 플래그뿐이다. 숙련도 평가·결과 계약은 없다.
원격 튜토리얼 브랜치 담당자/미공개 작업은 확인하지 못했다. 예상 클래스명으로 연결하지 않는다.

| 전달 항목 후보 | 합의할 내용 |
|---|---|
| assessmentVersion, completedSuccessfully | 평가 규칙 버전, 중단/실패 구분 |
| proficiency 또는 rawScore | 등급/원시값 의미·산정식·임계값은 담당자 합의 |
| parries/dodges/hits/opportunities | 유효 기회·중복·ignored 의미, 데이터 유효성 |
| practiceSeconds, retryCount, assessedAt | 시간 기준, 재평가/만료·취소 정책 |

테스트 전용 DTO/fixture를 Tests 아래에 두고 실제 CommunityStats.Replace와 새 SaveData를 사용하면 운영 DB·세이브 변경 없이 계약 후보를 실험할 수 있다.
기존 커뮤니티 Entry에는 숙련도 key가 없으므로 현 구조에 있다고 가정하지 않는다.
후보 우선순위는 **유효한 개인 패턴 기록 → 합의된 숙련도 공동 통계 → 전체 공동 통계 → 기본값**이며 아직 정책 결정이 아니다.
튜토리얼 점수로 최초 prior만 정하는 안과 매 전투 강제 보정하는 안은 별개다. 후자는 현재 LinUCB/보호 규칙을 바꾸므로 별도 승인이 필요하다.
모델 A/b를 튜토리얼 점수만으로 학습 완료처럼 채우는 정책도 도입하지 않았다.

연결 후보는 GameServices.Decide/CommunityStats, CloudSync의 통계 전달, 승인될 경우 GameData 세이브 모델,
팀원 튜토리얼 결과 경계, DdaPanel 표시다. 이번 변경은 이 공통 제품 파일을 건드리지 않는다.
팀원 충돌 가능성은 주로 Tests/DdaLearningVerificationTests.cs와 CloudSyncStatus 스텁이며, 실제 tutorial/DB/save 계약 확정 후 함께 리뷰해야 한다.
