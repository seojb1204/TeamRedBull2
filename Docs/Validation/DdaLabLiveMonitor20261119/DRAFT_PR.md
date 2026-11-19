## 목적과 동작
선별 통합 후보 3342884 기준의 독립 DDA Lab/읽기 전용 모니터 후보입니다. main 배포/병합용 검증 완료본이 아닙니다. 기존 C# Lab을 복원하고 현재 게임 코드로 실행하며, 별도 모니터 탭에서 로컬 진단과 기록 재생을 구분합니다.

게임 진단은 기본 OFF이며 -towerDdaMonitor <절대폴더>에서만 bounded background 파일 출력합니다. 정책·학습·세이브·CloudSync·아트 변경은 없으며 제품 hook은 별도 커밋04f452a의5파일에 한정됩니다. 실제 DB/개인 저장/큐/키에 접근하지 않았습니다.

## 검증
현 코드 Lab46, reader22, publisher34, 통합8suites, Headless739, Ultimate37, Learning248, Phanes3188, 보존20/45 및 두 브라우저 검증 PASS. 기존 Arakus animation/size/charge3suites FAIL은 유지하고 원인/로그를 보고서에 기록했습니다. 실행 소스91hash 항목 일치 확인.

Unity/Windows 실제 플레이 연결·오디오·FPS는 미검증입니다. 화면 증거는 실제 C# HEADLESS가 만든 기록의 REPLAY이며 실제 Unity LIVE가 아닙니다. HEADLESS 강제캡처 최대103ms outlier가 있어 실게임 성능 계측이 필요합니다.

Docs/Validation/DDA_LAB_LIVE_MONITOR_20261119.md 및 Tools/DdaLab/README.md에 실행법/증거/학교PC 체크리스트가 있습니다. main 및 기존 후보는 변경하지 않았으며 자동 병합하지 않습니다.
