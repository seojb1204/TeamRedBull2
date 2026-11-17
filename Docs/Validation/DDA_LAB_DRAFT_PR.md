기존 자동검증은 고정된 실험만 실행할 수 있어 프로필·시드·시나리오를 바꿔 결과를 비교하기 어려웠습니다. 실제 C# 게임 소스를 실행하는 오프라인 실험실과 Cold Start/F8 진단을 추가합니다.

- Python + .NET 8만으로 실행하는 로컬 대시보드: 5보스, 초보/일반/숙련, 상승/하락/반복실패, DDA ON/OFF, 시드·반복 횟수 설정.
- 실제 전투 루프 / 성공률 주입 / 조건 통제 선택을 구분합니다. A/b 전후, 다음 배정, 통계, 조건별 선택·공격 시작, CSV/JSON·그래프·이전 결과 비교·재현성을 제공합니다.
- 실제 CloudSync→CommunityStats→Prepare 모의 통합, 수신 상태/배정 스냅샷, 잘못된 행의 캐시 보존, 취소/IO 오류 시 in-flight 상태 복구를 보완합니다.
- F8은 늦은 통계 수신 후 다음판 예측을 갱신하며, 진행 중 배정은 유지합니다. 튜토리얼 계약은 테스트 전용입니다.
- 오디오 하네스의 잘못된 1000Hz 스텁을 44100Hz로 복구합니다. 실제 음원/게임 오디오 코드는 변경하지 않습니다.

게임의 난이도 결정식·전투 밸런스·SaveData·DB 스키마·튜토리얼·씬·Prefab·아트는 변경하지 않았습니다. 실제 Supabase와 개인 저장/업로드 큐는 사용하지 않았습니다.

검증:

- 새 실험실 통합 46 PASS, 실제 Chromium에서 실행/재실행/JSON 저장/비교/불러온 설정 재실행/오류 JSON 보존/좁은 화면/입력 거절 확인.
- 실제 Cold Start 경계 23 PASS, 오디오 수명주기 27 PASS, 기존 BGM/SFX 검증 통과.
- 기존 감사: 269 학습 검사×2, 240전투×2, 2,968선택×2, 64산출물 재현 동일. 진단 변경 전과도 64/64 정책 산출물이 같습니다.
- 기존 통합은 **7 PASS / 2 FAIL**입니다. room/size의 실패는 아트 전용 범위에서 DDA/DB 파일의 바이트 동일성을 강제하는 보호 검사이며 이번 허용된 진단 변경을 탐지했습니다. 검사 기준을 낮추거나 삭제하지 않았습니다.

Unity API/F8 실화면/청음/Windows/실제 DB는 미검증입니다. 기능 동결 전 Unity 검증, 첫 판 fallback 요구, 튜토리얼 계약, 위 두 보호 검사와 실제 diff 리뷰가 필요합니다. main에 병합하지 않고 Draft로 검토합니다.

사용법: `python Tools/DdaLab/lab.py --data-dir ./TestResults/DdaLab` (Python 3.10+ / .NET 8 SDK).

보고서: `Docs/Validation/DDA_LAB_DELIVERY_20261117.md`, 결과: `Docs/Validation/DDA_LAB_RESULTS_20261117.json`.

![실제 전투 모드 대시보드](https://raw.githubusercontent.com/seojb1204/TeamRedBull2/feat/dda-lab-diagnostics-20261117/Docs/Validation/dda-lab/dashboard-combat.png)
