# 구조 설계 · 단계별 계획

## 이번 실제 분리

| 소유자 | 실제 소유 데이터·책임 | 연결 |
|---|---|---|
| BossPatternSystem | 패턴 시계·현재 패턴·쿨타임·난수·페이즈·기둥/결계 상태, 선택·실행·장판/탄/소환 갱신 | TowerGame은 기존 API 호환용 속성/호출만 위임 |
| DdaBattleSession | 전투 시작 때 고정한 Enabled/Stats/Contexts, 준비·학습 | 입력/연출에서 학습 코드 제거 |
| RewardLedger | 정산 여부, 보상·해금·계승·카드 재개방 | Reset/단 한번 TrySettle. 반복 종료도 기존 결과 보존 |
| BossDefinition | Inspector 원본 설정 | 전투마다 JSON 깊은 복사로 별도 BossSpec 반환 |
| ActorView | SpriteRenderer·Animator와 표시 포즈 | 게임 판정 시계의 준비/실행/회수 구간을 읽음 |
| NativeGameUI | 직렬화된 Canvas·페이지·버튼 이벤트·표시 | 기존 카드/저장/전투 API 호출 |
| ScenePresentation | 씬의 프리팹 인스턴스·보스 에셋 참조 | 실제 Production 씬에서 연결 |

단순 partial 분할이 아니다. 패턴 상태와 실행은 일반 C# 객체로, 보상과 DDA 세션도 각 소유 객체로 옮겼다. 다만 TowerGame은 아직 플레이어 입력/전투와 진행을 담당한다. 완전한 책임 분리가 끝났다고 하지 않는다.

## 씬과 에셋

- Assets/Scenes/TowerProduction.unity: Game Flow, 카메라, Scene Presentation, Player/Arakus 프리팹 인스턴스, Canvas/EventSystem, 참고용 지면/벽 Collider.
- Assets/Prefabs/Player.prefab, Arakus.prefab: ActorView·SpriteRenderer·Animator.
- Assets/Data/Bosses: 보스5종 ScriptableObject. 원본 자산을 전투 중 수정하지 않음.
- Assets/Animation: 두 AnimatorController와 25개 스프라이트 클립. 아라쿠스의 미구현 패턴용 그림도 상태로 준비했지만 전투 선택을 임의 추가하지 않음.
- Assets/Art/Arakus: 팀 제공14장, 원본 바이트 유지. 156×146 분할, 방패40×40, PPU45·Point·무압축·밉맵없음.
- Assets/Art/Player: 기존 Hero 이미지를 에디터 임포트 경로에 복사. StreamingAssets 원본도 프리뷰 호환을 위해 유지.

초기 생성: 메뉴 Reversal Tower/Create Production Foundation. 기존 Production 씬이 있으면 열고 검사만 하므로 팀이 편집한 씬을 자동으로 다시 만들지 않는다. 배치 빌드 진입점 FoundationSetup.Build. 새 씬은 기존 빈 Tower.unity와 별도로 보존한다.

## M0~M4

| 단계 | 산출물 | 선행/완료 기준 | 남은 위험 |
|---|---|---|---|
| M0 | 진단·보스현황/충돌·구조·로드맵·자료요청 | 실제0.4조사·기획 충돌 공개. 사용자 구조/에셋 진행 승인 받음 | 이미지 타임라인 재검토 및 팀 결정 필요 |
| M1 기반(이번) | 책임 분리·씬/프리팹/Animator/SO·핵심uGUI·아라쿠스 그림 | 기존114검사+참조검사+Unity빌드+실제플레이어 스모크 | 설정/도움말/기록 IMGUI, 수동충돌·플레이어 코드 이행 남음 |
| M1 완성 | 아라쿠스 모든필수패턴·기둥·분노·시각피드백 | 원본 타이밍/수치 확정→패턴 단위 검사→로비에서 정상규칙 처치/정산, 팀 플레이 | 제공 포즈의 중간프레임 부족 |
| M2 | 파네스 원본기믹·계승/진행·DDA신모델/대조군 | 공통 피해상한/페이즈/모델정책 결정. prior데이터 검증 또는rule fallback | 신모델 저장 변환/실험 검증 |
| M3 | 나머지3보스 전체기믹·맵·연출 | 카로스대응행동,아디쿠스판결,에텔로스디버프 확정 | 큰 전투설계/아트 물량 |
| M4 | 전기능 회귀·빌드·성능·사람플레이·DDA실험/문서 | 재현 가능한조건/수집동의/테스트PC 지정 | 재미·공정성은 사람 검증 필요 |

## 다음 파일 단위 이행

1. PlayerState/PlayerController/PlayerCombat로 이동·상태·타격을 분리. 기존 검사 호환 어댑터를 단계적으로 축소.
2. PatternDefinition 데이터에 사거리/타임라인/페이즈/제외조건과 실행 종류를 명시. SpecialMechanic 인터페이스로 기둥·결계·저울·환영 분리.
3. SaveManager에 경로/저장 트랜잭션 및 모델 버전 이전 집중. v1/v2 원본을 파괴하지 않음.
4. DDAContext/SharedLinUcbModel/ArmTable/DecisionLog를 독립 순수 C#로 구현. 기존 모델과 별도 버전으로 보존하고 차등 비교.
5. 남은 UI 오버레이와 키 바인딩, 수동충돌의 씬/히트박스 편집을 이행.

## 검사 계획

현재0.4 회귀→서비스 단위/다중세션 격리→SO깊은복사→클립/씬참조→실제uGUI이벤트→실행화면→팀플레이 순서다. 테스트 저장은 별도 폴더다. 로비 하네스의 Gold/강제정산과 DemoInput은 정상 플레이 성과로 표시하지 않는다.

DDA 비교는 같은빌드·같은시드·같은세이브/강화·같은 입력 추적에서 OFF/rule/신모델을 비교한다. 현재0.4 난수 시드 고정/입력 재생은 아직 구현하지 않았으므로 연구 실험 완료가 아니다. CSV에 조건·policy/priorVersion·문맥·적용배율·예측/불확실성·관측/유효타·학습여부를 추가한다. 유효타3미만 미학습,원본SO불변,한방상한,연패보호·범위제한·고장fallback을 별도 검사한다.
