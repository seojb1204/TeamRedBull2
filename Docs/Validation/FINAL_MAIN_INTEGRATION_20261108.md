# 최종 main 통합 후보 검증 — 2026-11-08(KST)

**main에는 아직 반영하지 않았다. 사용자 최종 승인 전 main Push/병합은 금지한다.**

## 기준 및 권한

- 원본 main: `f13bb6bd25f469e38acce05eceb712712dc30c3f`
- 백업: `backup/main-before-final-20261108` (동일 커밋, 원격 보존)
- 통합 후보: `integration/final-main-20261108`
- 원래 `feat/arakus-room-art`의 미커밋 파일과 `feat/arakus-room-integration`은 별도 worktree에 그대로 보존.
- gh auth status는 GH_TOKEN 인증 실패를 보고했다. user/repo permissions/main protection/branch rules API는 모두Forbidden.
  API 차단과 실제 토큰 상태를 분리 진단할 수 없어 현재 인증 계정명/main Push·Merge 권한/필수 승인 조건은 미확인이다.
- 백업 브랜치 원격 생성은 성공했다. 일반 브랜치 쓰기 가능의 근거이며 main 권한의 증거는 아니다.
  실제 main Push로 권한을 시험하지 않았다. main 병합 전 API 접근과 보호 규칙을 별도로 확인해야 한다.

## 원본 head와 의존 관계

| 대상 | 확인한 커밋 |
|---|---|
|main|`f13bb6bd25f469e38acce05eceb712712dc30c3f`|
|arakus-integration|`ccf9ba9313ef5050d3cdc75a46d162faf3f5bfaf`|
|pr13|`2bcab4154ae534ba21befc08b5ce4c0d40741329`|
|pr14|`fd9428f358dedc56a9c30ae02804c90585ebabc1`|
|pr15|`4420022bfc0e472d034ba936fa19aa9aeb575dbb`|
|pr16|`a94fbc0936f78e91c35737c4dfaef441afed027b`|
|pr17|`a161f489ccc1135765b54dcd0e4febe40af2fe67`|
|pr18|`c049ab887bfc673981a2c5723af2f8c95c4587de`|
|pr19|`e4626a8c4271a9fdea14cad45bdccf8e49bbdffa`|

`#13 ⊂ #14 ⊂ #16`, `#19 ⊂ feat/arakus-room-integration`을 git ancestry로 확인했다.
#16→#15→#17→아라쿠스 통합→#18 순서로5개 no-ff merge를 수행했다.
#13/#14/#19를 별도로 중복 병합하지 않았다. 최종 후보에 모든 대상 head가 조상으로 포함됨을 검사했다.
원본 브랜치 이력을 rewrite/rebase/force push하지 않았다.

## 충돌 및 통합 수정

텍스트 충돌은0건이었다. 자동 병합 후 실제 검증에서 다음 조합 문제를 발견·보정했다.

| 발견 사항 | 최소 보정 | 범위 |
|---|---|---|
|PR17 추가 ArakusPatternTiming을 일부 테스트가 컴파일 목록에서 누락|DDA/History/Size Python runner와 Verify-DdaHistory.ps1에 의존 소스 추가|테스트 실행 설정, 게임 정책 변경 없음|
|Charge 아트 검사에서 ActorView가 원래main과 같아야 한다고 가정|--presentation-baseline을 추가해 승인된 확대 코드ccf9ba9와 바이트 비교|Charge PNG·팔레트·발·메타 검사는 원래 main 기준 그대로|
|결과 화면에서 초기화/ON-OFF 후 F8 다음판 미리보기 캐시가 남을 수 있음|DdaPanel이 enabled 상태와 BossLearning 객체 변경을 감지해 미리보기만 갱신|UI 캐시, 학습/난이도 정책 변경 없음|

초기 컴파일 실패/검사 실패 로그는 artifacts에 보존했다. 기준을 낮추거나 실패 검사를 삭제하지 않았다.
F8 캐시 보정은 코드와 문법 검증만 했으며 실제 Unity GUI 실행은 미검증이다.

## 이번 최종 실행 결과

기존 PR18의 과거219/29·248/0 결과를 재사용하지 않고 통합 소스를 새로 컴파일·실행했다.

| 검사 | 결과 | 범위 |
|---|---|---|
|기존 HeadlessTests|132 PASS|DDA/성장/전투/초기화 등 실제 소스 + UnityShim|
|DDA-LinUCB 검증|248 PASS / 0 FAIL|실제Choose/Update/Prepare/Complete,5보스 반복 실험|
|DDA 이력|47 PASS|기록/고정DDA/포기/저장/모델 비간섭 등|
|F8 패널 창 계산|205 PASS|리사이즈/화면 경계/마우스 소비 계산, 실제 GUI 아님|
|ArakusChargePolish|325 PASS|1.30초 이동/5m/판정 간격/Rage/DDA/기둥|
|ArakusAnimation|422 PASS|포즈·프레임 선택, 실제 Animator 실행 아님|
|ArakusSize|957 PASS|크기/피격/근접/좌우/대시·패링/다른 보스|
|보스룸 리소스·수치 배치|98 PASS|Point/기준점/가로100%/보호 소스/PNG|
|크기·스프라이트 계약|87 PASS|77프레임 발 기준/보호 에셋/예고 참조|
|Charge PNG|5프레임 전체 통과|원본픽셀 nearest 확대/발/메타/Groggy/승인된ActorView|
|Assets C# 문법|39파일 오류0|Roslyn 구문 검사, Unity API 컴파일 아님|
|GUID|262개 중복0|외부7참조는 원래main에도 존재, manifest에UGUI2.0.0 명시|

반복 실행되는132개 등은 합산하지 않았다. 각 묶음의 별도 결과다.
7개 외부 Scene 참조의 Unity Package 해석은 실제 Editor에서 확인해야 한다.

## 보존된 기능 연결

- StartBattle→Prepare→패턴별 배정→BeginPattern 선딜/피해/쿨타임→관측→Complete→A/b→SaveStore→다음 전투를 실제 소스 검사로 확인.
- PR13의5판 이후 성공률 보정,3연패 보호,단계 제한,포기/종료 학습 제외,쿨타임-1=1.12 보존.
- CSV는 패턴별 기회/피격/패링/회피/레벨/피해/abandoned를 기록한다. Context/UCB/A/b를 직접 담는 CSV는 아니다.
- F8→패널, F9접기, 그래프 점/선 좌표 보정, 리사이즈·마우스 입력 차단, 런타임 이력 Recorder/저장 경로 연결 보존.
- #15 초기화는 learning만 비운다. 골드/카드/클리어/전투기록과 F8 그래프 이력 파일은 삭제하지 않는 기존 범위다.
- 왕좌 Battle/Result 전용 가로100% 배치, 발y0, 기둥±4/정상·균열/0.6초 페이드/3.2초 그로기 보존.
- root2.2, 피격 반폭1.29643m/높이3.49643m, 근접별 확대와 독립FX 월드1.4 보존.
- PR17의Charge PNG, 공유 타이밍1.30초, 애니메이션 시간 연결을 보존.
- WorldArtist/ActorView/ArakusVisuals/기둥은 승인된ccf9ba9와 바이트 동일, Charge/타이밍/애니메이션은PR17과 바이트 동일 검사 통과.

LinUCB는 A/b 학습 점수와 불확실성을 사용하지만 극단 성공률/연패는 안전 규칙이 우선하는 하이브리드다.
주입된 성공률 실험은 실제 사용자의60~80% 수렴 증거가 아니다.
Cold Start의 메모리 주입 테스트는 통과했지만 실제 서버 수신 시점/업로드는 검증하지 않았다.

## 재현

```sh
python3 Tests/run-final-integration.py --dotnet /path/to/dotnet --output /tmp/tower-final-results
```

소스 허용 목록만 임시 프로젝트에서 컴파일한다. CloudSync/실제Supabase 설정/실제사용자세이브/CloudQueue는 실행에 포함하지 않는다.
PR18의 기존 Docs/Validation/dda_results는 과거 보관 결과다. 이번 실행 로그는
`/workspace/artifacts/final-main-integration-20261108/final-run/`에 있으며 source SHA-256과 종료코드를 포함한다.

## 아직 검증하지 못한 사항 / 발표 전 필수

Unity6000.4.0f1 Editor가 없어 Unity 전체 API 컴파일·실플레이·실제GameView 캡처·Windows 빌드는 수행하지 않았다.
자동검사 통과를 Unity/Windows 최종 통과로 해석하지 않는다.

1. 이 후보를 별도 로컬 폴더에서 Unity6000.4.0f1로 열어 컴파일/참조/임포트 오류 확인.
2. 일반 테스트 복사본에는 실제Supabase 설정과 기존CloudQueue를 넣지 말고 저장 경로도 분리해 업로드/기존세이브 오염 방지.
3. Title→Lobby→성장→Arakus→승리/사망→Lobby,재전투 및 다른 보스 회귀 확인.
4. F8/F9/리사이즈/스크롤/선-점 좌표/마우스공격 차단, 결과에서초기화와DDA설정변경 후다음판예측 갱신 확인.
5. 신규저장→1~6판의DDA/A·b/실제배정/CSV,OFF/포기/창닫기,재실행 복원 확인.
6. 16:9 배경 가로100%/발y0/상단소폭크롭,스케일2.2/기둥크기불변,근접예고/패링·대시/돌진1.30초/양쪽기둥 충돌 확인.
7. Reversal Tower/Build Windows 또는 팀의FoundationSetup.Build 경로로 실제 Windows 빌드와실행 확인.
8. 실제DB 자체 검증은 담당자와 별도 테스트환경을 합의한 후 수행. 이번 검증에서는 접근하지 않음.

## main 반영 절차

후보 브랜치와 main diff를 검토하고 위 미검증 항목을 확인한다. main 보호/필수리뷰/상태검사 요구도 확인해야 한다.
사용자 최종 승인 전에는 main을 변경하지 않는다. 승인 후에도 보호 규칙을 우회하거나 main을 직접 Push하지 않는다.
GitHub API 접근이 복구되면 main대상 PR을 통해 정상 병합한다. API가 계속 차단되면 PM이 웹에서 같은 후보를 검토·병합한다.
기존 #13~#19는 원본 이력을 모두 포함하는 후보를 통해 반영되므로 별도 중복 병합하지 않는다.
