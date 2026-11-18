## 문제와 동작

잘 대응했다고 느껴도 -2가 유지되는 원인을 실제 패턴 관측·EMA·연패·LinUCB 점수로 추적할 수 없었고, DB가 비면 유효 튜토리얼도 첫 배정에 반영되지 않았습니다. 실제 공격 적용값/학습 trace와 튜토리얼 F8을 추가하고, 개인 근거 이후의 우선순위를 보존하면서 첫 만남만 그룹 DB→전체 DB→튜토리얼→기본값으로 연결합니다.

튜토리얼 초기 -1/0/+1 및 confidence/sample 기준은 보수적 v1 가설입니다. 개인 Reward/Choose thresholds/공격 보정·밸런스는 유지합니다. 신규 raw telemetry는 capability-bound RPC와 별도 outbox이며 기본 OFF입니다. 기존 DB/CloudQueue/진행 포맷을 변경하지 않습니다. 신규 SQL은 운영 미적용입니다.

## 검증

- 기존 회귀 9/9, Headless738, ultimate37, size953, learning269, frequency185, robustness31, Phanes3188.
- 실제 tutorial30/save7/cross-DDA24/legacy queue24, 신규 adaptation155/proficiency HTTP28/Cold receive23.
- 격리 PostgreSQL17 network none에서 RLS/권한·불변성·FK·집계39. Lab46 및 Chromium 실행·재현·export·비교 통과.
- 17설정×30전투×ON/OFF×repeat=2040, 재실행17/17 동일, 저장/모델 복원 모두 동일. 기존 stable6설정 수정 전후 stats 차이0.

95–100% 대응은 +2, 저대응은 -2로 반응합니다. 85%의 잘 학습된 easy arm/연패 보호는 -2 유지 가능하며 정책 원인입니다. 실제 agent 성공률은 인간 증거가 아니고 패턴별 60–80% 수렴은 입증되지 않았습니다.

## 리뷰·의존성과 미검증

기준 integration/dda-lab-tutorial-qa-20261118@969f3e9입니다. main83d9085가 아직 PR50/Lab 통합을 포함하지 않으므로 이 main 대상 Draft diff에는 이전 통합도 포함됩니다. 이번 고유 제품 commit24e6a2c/Lab166fe3f를 구분해 리뷰해야 합니다. 원본 브랜치/main 변경·병합은 하지 않습니다.

Unity6000.4.0f1 컴파일/Game View F8·F9/실제 입력/오디오/Windows/Supabase PostgREST 미검증. DB 활성화 전 staging/보안·키 백업/운영 익명 데이터 정책 검토가 필요합니다. 기존 공동 파일(TowerGame, SaveStore, CloudSync, GameServices, TutorialSession, F8/UI) 충돌 위험이 있습니다. 씬/Prefab/아트는 변경하지 않았습니다.

상세 증거와 집 PC/동결 체크리스트: Docs/Validation/DDA_ADAPTATION_COLDSTART_AUDIT_20261118.md
