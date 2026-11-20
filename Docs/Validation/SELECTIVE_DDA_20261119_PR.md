최신 main `151b5256`을 보존하면서 11/18 DDA 기능만 함수 단위로 선별 통합한 후보입니다. 전체 브랜치 merge/cherry-pick은 하지 않았습니다. 제품·테스트 commit: `6bd9292`.

튜토리얼 실시간 평가·시간 분리, 보수적 최초 난이도, 개인/그룹/전체/튜토리얼 fallback, 보호된 opt-in telemetry, F8 actual attack 및 종료 학습/다음 예측을 추가합니다. 기존 개인 LinUCB 보상·임계값·보정 수치는 유지합니다. 신규 전송은 기본 OFF, SQL 운영 미적용입니다.

main의 파네스 궁극기/R 계승·아라쿠스 애니메이션·반복 강화·transition·기존 F8/F9·저장 소유권을 보존했습니다. 시범/성공 피드백을 active 시간에서 제외하고, 실제 설정 저장 helper를 공통 partial로 이동해 HEADLESS 경계를 복구했습니다. 튜토리얼 종료 Enter 재사용도 차단했습니다.

DDA Lab은 제외했습니다. 166fe3f는 main에 없는 기존 Lab 기반을 필요로 하며 제품 기능의 실행 의존성이 아닙니다. 아트/씬/Prefab/보스 데이터/프로젝트 설정/기존 실험 도구도 변경하지 않았습니다.

검증: 연결 E2E38, cross7 suites 통과(30/7/24/24/155/28/38), Cold23, Headless739, Ultimate37, learning248, frequency185, robustness31, Phanes3188, history47, panel205, profiles31, source-preservation45.

**남은 FAIL:** ArakusAnimationTests의 distance gait, ArakusSizeTests의 cleave hit boundary, ArakusChargePolishTests의 windup pose. 세 가지가 원본 main 소스의 격리 baseline에서도 동일하게 재현됐습니다. 삭제/완화하지 않았고 전투/아트 코드를 변경하지 않았습니다. 모든 검사가 green이라는 주장이 아닙니다.

Unity6000.4.0f1 actual compile/Game View/F8·F9/Windows/청음/실제 Supabase는 미검증입니다. 실제 빌드/수동 검증과 기존 FAIL 판정 전 병합·배포 승인본으로 취급하지 마세요. 이번 PR은 Draft 검토 후보이며 main 병합을 수행하지 않습니다.

상세 변경·의존성·초기 실패·baseline 대조·수동 체크리스트: `Docs/Validation/SELECTIVE_DDA_INTEGRATION_20261119.md`. 실행 증거: `Docs/Validation/SelectiveDda20261119/`.
