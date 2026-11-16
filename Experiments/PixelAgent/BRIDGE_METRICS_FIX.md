# 2026-11-16 브리지 계측/시간제한 수정

native12의 `BeforeTick`은 60초에서 멈추고 CaptureLoop는 120초까지 기다리던 불일치를 공통 SimulationLimitSeconds로 통일했다. legacy7=60초, native12=120초이며 벽시계 한도는 별도 유지한다.

EpisodeMetrics schema1의 기존 playerDamage는 호환성을 위해 **최대 HP-최종 HP(순손실)** 의미를 그대로 둔다. 새 cumulativePlayerDamage/cumulativePlayerHealing은 PlayerHit·포션·회복 궁극기의 실제 HP 변경 지점에서 각각 누적한 값이다. 한 프레임 내 피해와 회복도 상쇄되지 않는다. 초과 피해·초과 회복은 실제 HP 변화량만 포함한다. 디버그 신 모드 강제 HP 복원은 계측하지 않으며 평가에 사용하지 않는다. 정책 관찰/보상/모델은 변경하지 않았다. 기존 reward의 HP 차이 기반 회복 보상 성격도 유지하므로 이전 모델 비교가 가능하다.

## 검증 범위
- Python 정책 테스트, 실제 PixelAgentBridge C# + API shim의 legacy/offscreen/native12 경계/누적치/비누출 검사.
- 실제 TowerGame 전투 소스 UnityShim 캠페인으로 headless 빌드 호환성 검사.
- 실제 Unity 렌더링, Windows 입력, 화면 인식 정확도, 이 변경으로 승률 개선 여부는 미검증.
- 테스트의 HP 기록 호출은 shim에서 수동 주입한다. 실제 Unity 콜백 호출 검증을 대체하지 않는다.

## PR47 통합 주의
TowerGame.cs PlayerHit 및 포션/궁극기 회복 지점은 PR47 계측과 같은 줄이다. 추후 통합 시 PR47 NoteEvidence/HealPlayer와 본 PR RecordPixelHpChange를 모두 보존해야 한다. 통합 전 서로 자동 병합하지 않는다. 장기적으로 단일 전투 이벤트 계층을 두 소비자가 구독하도록 통일한다.

## 정상 화면 입력 후속
현 브리지는 게임정지 + 내부 입력주입이다. 정상 키보드 입력 테스트는 별도 실행 경로로 구현한다. 자기 소유 HWND/PID, 전경창 일치, 최신 캡처, 프로세스/GPU 감시를 통과한 경우만 입력하고 실패 시 자신의 키만 해제한다. 포커스를 자동 탈취하지 않는다. 로비→전투→정산 상태 인식은 HUD 포함 framebuffer로 별도 검증하며 상점/강화/저장 삭제는 하지 않는다.


`input_safety.py`는 Windows API를 호출하지 않는 순수 결정 계층이며 현재 실행기에 연결하지 않았다. 소유 PID+생성시각/HWND·전경창·해상도·캡처/전체프로세스 증거 신선도·GPU 안전 여부·사용자 입력 시퀀스를 검증한다. 불명확/변경/시간역행/포커스 상실 시 해당 세션을 중지 상태로 고정한다. 추후 네이티브 adapter가 실제 증거 수집 및 전송 직전 재검사를 담당해야 하며, 테스트의 True 값만으로 PC 안전이 증명되는 것은 아니다. 현재 파일은 입력을 전송하지 않으며 정상 입력 지원 완료를 의미하지 않는다. 실패한 전송의 부분 키다운은 adapter에서 별도 추적·해제해야 한다.
