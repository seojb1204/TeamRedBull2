PR #50(`be57b0b`)의 튜토리얼·프로필·궁극기와 DDA Lab(`779352c`)을 main `83d9085` 기반의 독립 통합 브랜치에 함께 반영합니다. 원본 두 head를 merge 조상으로 보존했습니다.

CloudSync 시작 충돌은 SaveStore UUID와 Lab 수신 진단을 결합했습니다. source allowlist/실험 fixture를 새 tutorial/profile 타입에 맞췄습니다. 교차 검사에서 재현한 두 문제만 제품에서 최소 수정했습니다.

- 잘못된 owner 큐가 뒤의 성공 POST로 가려지지 않게 진단 실패를 보존하고 대기 수를 표시합니다. 원본 큐 파일·UUID·전송/재시도 정책은 유지합니다.
- 튜토리얼 완료 저장 재시도의 Enter가 같은 프레임에 보스전 시작으로 재사용되지 않게 합니다.

검증:

- 기존 전체 통합 **9/9 PASS**. 명시적인 main 기준/18개 검토 파일 해시 계약으로 scope 보호를 유지했습니다. 이전 HEAD 변경 여부 검사와 tutorial-room 문자열 가정의 한계는 보고서에 설명했습니다.
- 튜토리얼 상태30, 저장실패7, 실제 강화 검 충돌/평가/저장/DDA 교차24, 실제 profile reset/restore를 in-flight upload 중 수행한 큐 검사24 PASS.
- 기존 프로필31, 궁극기37, main Headless738, 후보738, 파네스3188 PASS. 보고된 float 정확 비교 실패는 현재 .NET8 환경에서 미재현입니다.
- DDA269×2·실제 전투240×2·선택2968×2·결정적 산출물64/64, 빈도185/robustness31/Cold Start23 PASS. Lab46과 Chromium 실험/재실행/내보내기/그래프도 확인했습니다.
- BanditModel, frequency policy, Decide, Complete, SelectPattern이 main과 바이트 동일합니다. 숙련도 이름 매핑은 테스트 전용이며 runtime group-aware difficulty를 도입하지 않습니다.

Unity 실행 환경이 없어 Unity API/Game View/F8 화면·청음·Windows 빌드·실제 DB는 미검증입니다. 실제 Supabase·인증키·개인 저장/CloudQueue는 사용하지 않았습니다. 새 스키마·중요 save 정책·난이도/밸런스·씬·Prefab·아트를 임의 변경하지 않았습니다.

`Docs/Validation/INTEGRATION_QA_20261118.md`에 모든 FAIL의 초기 원인/최종 결과, 명령, 근거, 집 PC 체크리스트와 동결 전 필수 사항을 정리했습니다. **Draft 검토용이며 main에 병합하지 않습니다.**
