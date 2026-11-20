# DDA Lab 복원·읽기 전용 게임 모니터 검증 — 2026-11-19

## 1. 브랜치·기준과 전달

작업 브랜치 `feat/dda-lab-live-monitor-20261119`. 사용자 지정 기준은 `integration/dda-selective-20261119@3342884db5f30526d399baae6bf806ee70572b24`이며 main에서 다시 구성하지 않았다. 원격 재확인 결과 main은 `151b5256d8203ebd28f7b8466738f8c12d5ea973`, 통합 후보는 `3342884` 그대로다. 기존 모든 worktree/브랜치를 보존했다. main 또는 기존 후보에 push/merge하지 않는다.

목적별 커밋: `56b68ff` Lab 복원, `04f452a` 기본 OFF 게임 진단 발행기. 후속 도구/UI/테스트 및 본 보고서는 같은 독립 브랜치에서 분리 커밋한다. 최종 전달 SHA/PR 결과는 아래 전달 기록에 기재한다.

## 2. 기존 Lab 복원

11/18 참고 커밋 `7b43148`에서 Tools/DdaLab의 실행기·C# Experiment·HTML·README 및 Lab 테스트만 복원했다. 이전 게임 소스 전체나 이전 정책은 이식하지 않았다. 이후 현재 게임 API에 맞춰 검증하고 읽기 전용 모니터를 추가했다. 기존 main에는 Lab이 없으며 이 후보는 독립 실행 도구다.

초보/일반/숙련, 상승/하락/반복실패, ON/OFF, seed/반복수, 패턴 배정·선택·학습 그래프, JSON/CSV, 재실행·비교를 지원한다. 자연 전투(실제 C# Update+UnityShim)와 성공률 주입, 조건 통제 선택 실험을 별도 표시한다. Python/JavaScript LinUCB 대체 구현은 없다.

## 3. 시뮬레이터 실행 검증

현 소스 기준 `Tests/test-dda-lab.py` **46 PASS**. 실제 C# 실행, ON/OFF·시나리오·저장 복원·동일 조건 결정적 결과·입력 거부를 검사했다. 기존 17종/2,040전투 등 과거 숫자를 이번 실행 결과로 사용하지 않는다.

브라우저 검증 `SIMULATOR_UI_PASS`: 실제 C# 실행/재실행, 그래프, JSON/CSV, 결과 비교, 좁은 화면, JavaScript 오류 검사. 실행 실패를 성공 화면으로 표시하지 않는다. 실제 인간 플레이/Unity 물리·scenePresentation 재생/목표 성공률 수렴 검증은 아니다.

## 4. 실제 게임 연동 방식

독립 Python localhost 서버가 사용자가 지정한 **진단 폴더만 읽는다**. 신규 프로토콜 `tower.dda.live/1`: 게임이 `<절대root>/DdaLive/<새세션GUID>/latest.json` 및 `events.jsonl`을 발행한다. Viewer→게임 명령/정책 쓰기/DB 호출은 없다. 기존 Applied/Decisions/Learning/Battles 로그도 제한된 경로에서 읽는다. 과거 로그의 mtime을 실제 사건UTC로 사용하지 않으며, 관련 ID 없는 종료 기록은 추측 연결하지 않는다.

새 출력: 보통 최대5Hz 캡처, background I/O, atomic latest 교체, 중요한 공격/종료 사건. 큐32+coalesced 최신1, 패킷256KiB, 세션 이력16MiB, 이력 포화 시 최신 상태는 계속 갱신한다. 이전 기록 자동 삭제 없음. 이력 snapshot은 최대1Hz이며 연속 모든 프레임/모든 입력을 보장하지 않는다.

출처는 SIMULATION / LIVE GAME(Editor·Player) / FIXTURE / REPLAY / DISCONNECTED로 분리한다. LIVE에는 Unity origin 및 새 캡처·수신3초 freshness가 필요하다. HEADLESS는 항상 FIXTURE, 불러온 파일은 REPLAY다. 이는 로컬 파일 프로토콜의 출처 표기이며 실행 프로세스의 암호학적 인증은 아니다. 다중 세션은 사용자가 선택하며 임의 결합하지 않는다.

## 5. 실시간 관측 가능 항목

현재 보스·전투/튜토리얼 phase·ON/OFF, 실제 적용 공격의 Lv/선딜/피해/쿨다운/배수, 패턴별 배정·근거, 대응 합계, 마지막 실제 선택의 가중치/확률/이유, 실제 TutorialAssessment preview의 score/confidence/수행시간을 발행한다. 게임/F8이 쓰는 읽기 모델을 투영한다.

패링·회피·피격은 **합계 snapshot의 변화**다. 개별 입력/방어 이벤트의 정확한 시각은 미구현이며, UI도 캡처 시점으로 표시한다. 실제 공격 없음·전투 종료·오래된 heartbeat에서는 현재 공격을 비운다. 튜토리얼 평가와 일반 전투 데이터를 섞지 않는다.

## 6. 전투 종료 후에만 관측 가능 항목

실제 완료 Trace에 있는 learned/excluded, A/b 변화·hash/context/후보 점수 및 다음 전투 배정. 종료 이전에 값을 만들어내지 않는다. 재생 slider도 선택 시점 이후 종료 학습을 앞당겨 표시하지 않는다. 기존 CSV에 없는 정보는 미수집으로 남긴다.

## 7. 게임 코드 변경과 보존 증거

변경 제품 파일은 5개이며 별도 커밋 `04f452a`에 격리했다.

| 파일 | 최소 변경 |
|---|---|
| Assets/Scripts/GameServices.cs | 직렬화 가능한 진단 전용 DTO 추가. 원본 전체는 byte-prefix 그대로 |
| Assets/Scripts/SaveStore.cs | bounded background DdaMonitorSink 추가. 기존 save/queue 코드 전체 byte-prefix 그대로 |
| Assets/Scripts/TowerGame.cs | CLI 시작·5Hz 캡처·종료 hooks, 종료시 flush 제한150ms. 기존 F8의 기본값은 그대로이며 발행기만 저장소 상태 탐색 제외 |
| Assets/Scripts/BossPatternSystem.cs | read-only diagnosticSession getter, 기존 적용 로그 직후 진단 hook |
| Assets/Scripts/TowerGame.Tutorial.cs | 튜토리얼별 진단 run GUID만 추가 |

CloudSync, GameData/LinUCB, F8/F9 패널, scene/prefab/art/audio/settings는 변경하지 않았다. Publisher에 Choose/Update/Complete/게임 save 호출이 없고 개인 UUID/키/큐를 내보내지 않는다. 모든 원래 동작은 정확한 진단 hook 제거 후 기준3342884와 비교하는 **20 PASS**로 보호한다.

선별 통합 검사 기본 계약은 그대로 유지하고 명시적 `--scope-contract Tests/Contracts/LIVE_MONITOR_SCOPE_20261119.json`에서만 독립 Lab 허용 및 검토된 진단 파일 hash를 추가했다. **45 PASS**. 무조건 허용·검사 삭제·기준 완화로 PASS 처리하지 않았다. 새main 변경 시 hash 자동 갱신 대신 변경 검토가 필요하다.

## 8. 기본 ON/OFF 및 실행 분리

**기본 OFF**. 명시적 `-towerDdaMonitor <절대폴더>`에서만 신규 출력. Viewer 없이 게임이 실행되고 게임 없이 Lab/기록 재생이 실행된다. 인자가 없으면 신규 폴더/worker가 생성되지 않는 C# 검사를 통과했다.

이 인자는 기존 CloudSync를 끄거나 기존 저장을 격리하지 않는다. 수동 시험은 DB 설정 없는 테스트 복사본과 새 Windows 테스트 계정/VM을 사용한다. 개인 저장·CloudQueue·인증키/설정을 열거나 삭제하지 않는다. 기존 자동화 인자 `-towerDataRoot`는 F8을 숨기므로 F8 수동 확인에 대신 사용하지 않는다.

## 9. 실행한 자동검증 및 실패

| 이번 실제 실행 | 결과 |
|---|---|
| 실제 C# Lab | 46 PASS, 동일 조건 재현성 검사 포함 |
| Python reader | 22 테스트 PASS: partial JSON/CSV, stale, bounds, symlink, session/origin 혼합 거부, timestamp, privacy |
| 실제 C# publisher | 34 PASS: OFF, parity, 직렬화 속성, lifecycle, 모델 불변, queue/16MiB, I/O 실패, stale pending 순서 |
| 통합 8 suites | tutorial30 / save7 / tutorial-DDA24 / queue24 / adaptation155 / mocked proficiency HTTP28 / selective38 / monitor34 전부 PASS |
| Headless / 궁극기 | 739 / 37 PASS |
| DDA learning | 248 PASS |
| Phanes | 3,188 assertions PASS |
| 보존 / 명시적 scope | 20 / 45 PASS |
| 브라우저 simulator / monitor | 각각 PASS, desktop/mobile/JSON/CSV/재생·연결상태/JS 오류 검사 |
| Arakus animation / size / charge | **3 suites FAIL**: distance walk / cleave boundary(3.9899998) / windup pose |

마지막 세 실패는 선별 통합 보고서의 main151b5256에서도 재현되던 기존 실패와 동일하다. 이번에도 현재 소스로 실제 재실행하여 실패를 보존했다. 관련 제품 코드는 기준 후보와 동일한 보존 검사로 확인했다. **이번 작업에서 main baseline 재실행은 하지 않았으며** 이전 baseline 증거와 동일 원인으로 분류한다. 테스트를 고치거나 실패를 숨기지 않았다. 신규 기능 테스트의 최종 실패 없음. 개발 중 드러난 stale queue 순서/재생 미래 학습 노출/표시 버튼 문제는 수정하고 재검증했다. Unity 환경 부재는 별도 미검증이다.

실행 로그/manifest/screenshots는 [증거 폴더](DdaLabLiveMonitor20261119). manifest의 git commit은 실행 당시 직전 커밋일 수 있다. `tested-source-verification.json`은 실행 manifest의 소스 SHA256 **91개 항목이 최종 제품 소스와 일치**함을 확인한다. 파일 출처와 전체 명령은 각 manifest/log에 보존한다. 모든 HTTP는 mock이며 실제 Supabase 접속·쓰기 없음.

## 10. Unity 실제 게임 검증 여부

**미실행**. Unity 6000.4.0f1 compile/Game View/F8/F9/실제 프레임·GC/오디오를 확인하지 못했다. 실제 C# 게임+UnityShim 발행 및 브라우저 소비는 완료했으나 이를 실제 Unity 연결 성공으로 주장하지 않는다. 스크린샷도 HEADLESS 기록의 REPLAY라고 표시한다.

## 11. Windows 빌드 연동 여부

**미실행**. Windows Python/PowerShell, 파일 잠금·atomic replace·권한·방화벽·Editor 인자 전달·실제 게임과 Lab 동시 부하를 검증해야 한다. Python 실행 검증은 Linux3.12이며3.10 호환 문법/UTC 파서는 준비했으나3.10 및 Windows runtime 실행은 별도다. 학교 PC 설치/스크립트 정책도 확인 필요하다.

## 12. 사용 방법

상세 [Windows/오프라인 실행 안내](../../Tools/DdaLab/README.md).

```powershell
# 저장소 root: Python3.10+와 .NET8 SDK 설치 후 오프라인 실행
.\Tools\DdaLab\Start-DdaLab.ps1
# Python만 있는 PC는 보기/재생 전용
py -3 Tools\DdaLab\lab.py --monitor-only
# DB 설정 없는 별도 테스트 빌드/계정에서만
.\ReversalTower.exe -towerDdaMonitor "C:\DdaDemo"
```

브라우저 `http://127.0.0.1:8765`, 모니터 탭에서 `C:\DdaDemo`를 직접 연결한다. 여러 세션이면 정확한 항목을 선택한다. 게임이 없으면 첨부 `monitor-ui-final/actual-headless-recording.json`을 불러와 **REPLAY(HEADLESS)**로 시연할 수 있다. 이 파일은 이번 실제 C# 실행에서 생성한 비개인 테스트 기록이다. 실행하지 않은 게임을 LIVE로 표시하지 않는다. `.NET`이 없으면 시뮬레이션 실행은 비활성화된다.

## 13. 향후 main 병합 이후 재검증

이 후보를 main에 자동 병합하지 않는다. 승인된 게임 통합 후 새 worktree에서 Lab 도구와 필요한 진단 hook을 함수별로 검토하고 반영한다. Lab은 checkout source hash로 다시 컴파일한다. 실제 Lab46, reader22, publisher34, 통합8, Headless/Ultimate/Learning 및 보존검사를 새 소스로 다시 실행한다. 보호 계약은 main 변경의 의도 검토 후 명시적으로 갱신한다. 과거 게임 파일을 복사하거나 hash를 일괄 PASS로 바꾸지 않는다. `tower.dda.live/1` schema/ID/종료 trace 호환성을 확인한 뒤 Windows/Unity 실연을 반복한다.

## 14. 현재 시연 가능한 범위와 수동 체크

이 Linux 환경에서 실제 실행 확인: C# 실험+비교 그래프/저장, 실제 C# HEADLESS 진단 읽기, 실제 생성 기록 재생·시점 이동·학습 분석·JSON/CSV, 큰 글씨/좁은 화면·연결 끊김. Windows에서 실게임 LIVE를 발표 기능으로 확정하려면 아래 확인을 완료해야 한다.

- [ ] 새 테스트 계정/DB 설정 없는 빌드로 기본 OFF 실행: 새진단 폴더 없음, F8/F9/세이브/게임 단독 정상.
- [ ] 명시적 flag ON: 튜토리얼 실제 조작·score/수행시간, 패턴별 Lv와 F8 실제 적용값·로그 일치.
- [ ] 튜토리얼→보스→학습→저장 복원→다음 배정 확인. 튜토리얼/재평가/skip 학습 제외 유지.
- [ ] Unity Editor/Windows player 각각 LIVE source·run 구분, 공격/idle/종료 화면, 게임 강제 종료3초 후 DISCONNECTED.
- [ ] 재시작 다중 세션 선택, 기존 기록 REPLAY, 오래된 파일 mtime 변경해도 LIVE 아님.
- [ ] 실제 플레이+시뮬레이션 동시 CPU/GC/FPS 비교. 대규모 실험은 발표 전 미리 수행.
- [ ] ON/OFF 파일 잠금/권한 오류/디스크 지연에서 게임 정상, 이력 포화/종료 후 최신 파일 검증.
- [ ] F8/F9 화면 잘림·그래프·로비 전환·궁극기·애니메이션·사운드/일시정지, Windows 해상도/스크립트 실행 정책 확인.

## 15. 남은 위험·동결 우선순위

HEADLESS 강제 결과캡처200회: p50 **968.5µs**, p95 **1,791µs**, max **102,793.9µs**. 후자는 실제 관측한 outlier이며 숨기지 않는다. reader100회(11frames)는 p50 2.996ms/p95 3.585ms/max3.766ms. 이는 이 환경의 계측이며 실제 Unity FPS 영향 없음의 증거가 아니다. background 파일I/O와 CPU캡처/직렬화는 구분해야 하며 실제 프레임 영향 측정이 발표 전 필수다. .NET CPU hint1은 OS 제한이 아니다.

11/20 전 필수: Unity compile 및 Windows 실게임 연결/기존 동작 회귀/프레임 측정, Arakus 기존3FAIL 원인 담당자 검토. LIVE 검증이 끝나기 전에는 발표를 실제 C# SIMULATION + 명확히 표시된 HEADLESS REPLAY로 준비한다. 개별 패링/회피의 정확한 event timing은 미구현, 현재는 합계 관측만 제공한다. 로그 표본의 선택 횟수는 전체 인간 플레이 빈도가 아니다. 로컬 타임/3초 freshness가 잘못된 기기 시계에서 연결을 보수적으로 끊을 수 있다.

팀원 충돌은 GameServices/SaveStore/TowerGame/BossPatternSystem/TowerGame.Tutorial 및 통합 실행기의 공통 파일에 한정된다. 원래 기획/정책/씬/아트는 보존했으나 차기 병합에서 hook 중복과 DTO 이름/컴파일 경계를 검토해야 한다. 이 후보는 신규 정책·DB 스키마·인증·세이브 변경이나 기존 실패 해결을 포함하지 않는다.

### 전달 기록

아래 최종 GitHub 전달 결과를 별도 기록한다. 기존 main/후보 유지, 환경 게시/게임 배포/병합 없음.

- 기능/증거 전달 커밋: `00e15d1` (Lab/UI `c278022`, 게임 진단 `04f452a`, 복원 `56b68ff`). 이후 이 전달 결과만 문서 커밋한다.
- 독립 브랜치 Push 성공: [feat/dda-lab-live-monitor-20261119](https://github.com/seojb1204/TeamRedBull2/tree/feat/dda-lab-live-monitor-20261119).
- Draft PR 생성 실제 시도: base `integration/dda-selective-20261119`, head 위 브랜치. `Post https://api.github.com/graphql: Forbidden`으로 생성 실패. 생성/병합된 PR은 없다.
- [기준3342884 대비 비교](https://github.com/seojb1204/TeamRedBull2/compare/3342884...feat/dda-lab-live-monitor-20261119).
- final 사용 소스/실행 증거는 위 목적별 커밋에 포함되며, `main`/기존 후보를 갱신하지 않았다.
