# 발표판 통합 검증 — 2026-11-20

## 범위
원격 main 151b5256d8203ebd28f7b8466738f8c12d5ea973에 대한 DDA 최종 30f1a00f36518e9748c088e83f67c1b8deebc937(16커밋) 및 PR #52 c67dc96f9dc678b65de8df535fb195cca8d9bc32 통합 후보.
DDA와 PR52 두 부모를 보존한 통합 커밋을 PR로 main에 병합한다. 원본 작업 폴더·운영 DB는 변경하지 않는다.

## 수정
- 아디쿠스·에텔로스·카로스 선택 및 전투 진입 차단, 준비 중 표시. 기존 해금·클리어·궁극기 기록 유지. 저장 구조와 씬 직렬화 변경 없음.
- 튜토리얼 완료 시 프로필/보스 복원을 화면 전환의 암전 시점으로 이동. 전환 전 허수아비와 훈련실 유지.
- DDA 패널은 재실행과 타이틀/로비 복귀 시 닫힘. 명시적 F8/F9 및 튜토리얼 요약 지원 유지. 닫힌 패널은 과거 PlayerPrefs로 입력을 가로채지 않음.
- 허수아비 패링 예고를 몸체의 파란 발광과 타이밍 반짝임으로 변경. 회피용 붉은 범위 예고는 유지. 공격 타이밍/판정 변경 없음.
- 메인과 ESC 메뉴에 게임 종료. 저장 실패 시 종료하지 않음. 튜토리얼 임시 데이터로 실제 진행을 덮어쓰지 않음.
- PR52의 복원 지점 50개/백업 10개 상한, 기록 창 가시 행 렌더링 통합. 기록 캐시를 프레임 간격 추정 대신 창 닫힘으로 무효화.
- 배경 교체/궁극기 이펙트 재설계/새 BGM은 미정이라 제외. 기존 BGM 파일 및 연결 검사만 수행.

## 충돌 해결
HeadlessTests의 연속 카드 구매 검증과 UnityShim.isPlaying은 DDA와 PR52가 같은 문제를 각각 수정했다. 더 강한 DDA 검증(금액·스택 불변 포함)을 유지하고 PR52의 아카이브·봇 테스트를 통합했다.
PR52의 HEADLESS 설정 닫기 분기는 DDA에서 CloseSettings 자체를 공통 코드로 옮겨 이미 해결했으므로 저장 동작까지 유지하는 DDA 구현을 보존했다.

## 실행한 검사
| 검사 | 결과 |
|---|---|
| Verify-Logic.ps1 | 739 assertions PASS |
| Tests/Verify-Presentation.ps1 (.NET 8) | 12 suites PASS: 발표판22, 아카이브50, 궁극기37, 적응/초기평가155, 튜토리얼저장7, 튜토리얼DDA24, 통합38, 모니터34, 빈도185, 내구성31, 파네스3188, 자동전투240판 |
| run-integration-qa.py | 8 suites PASS (상태30, 저장7, 튜토리얼DDA24, 큐24, 적응155, proficiency HTTP28, 통합38, 모니터34). HTTP는 모의 통신 |
| test-monitor-automation.py | 6개 별도 프로세스 시나리오 PASS, F8/F9와 자동화 분류·재실행·메뉴 복귀 포함 |
| run-dda-learning-verification.py | 248 PASS |
| run-dda-history.py --suite panel | 205 PASS |
| test-dda-lab.py | 46 PASS |
| test-dda-windows-startup.py | 9 PASS, 실패 경로 모의 검사 |
| test-dda-monitor.py | 21 PASS, 1 환경 차단: Windows WinError 1314로 심볼릭 링크 생성 불가. 기준을 낮추거나 테스트를 삭제하지 않음 |
| verify_boss_music.py / verify_lobby_music.py | 음원 6개 형식·길이·연결·전환 검사 PASS. 실제 청취 검증 아님 |
| 실제 Unity 6000.4 API 어셈블리 참조 컴파일 | 게임 소스 43개 컴파일 PASS. 에디터 빌드와 구분 |

## 테스트 관리
HEADLESS는 전체 보스 연구/회귀 검사를 위해 발표 잠금 기본값을 끈다. PresentationReleaseTests는 잠금을 명시적으로 켜 같은 진입 함수를 검사한다. 일반 Unity 실행은 잠금이 기본 활성화된다.
새 실행기: powershell -File Tests/Verify-Presentation.ps1 -DotNetPath <.NET8 SDK의 dotnet.exe>
다른 실행기: python Tests/run-integration-qa.py --dotnet <경로> --output <새 결과 폴더>
기존 .NET Framework로 .NET8 전용 테스트를 실행하면 Json/ToHexString/HashData 참조 오류와 긴 임시 경로 제한이 발생했다. 새 실행기는 .NET8 참조와 런타임을 통일하고 PhanesPose 의존성을 포함한다. 제품 기대 결과는 완화하지 않았다.
역사적 아트/소스 보존 계약은 특정 과거 커밋의 검증으로, 현재 발표판의 실행 성공을 대신하지 않는다.
Git CLI 네트워크 연결이 불가능해 원격 GitHub API와 해시 대조로 소스를 구성했다. 임시 로컬 Git HEAD는 캐시 베이스이므로 개별 하네스의 commit 필드가 아닌 이 문서의 원격 기준 및 source_sha256를 사용한다.

## 배포 차단 / 미검증
Unity 6000.4.0f1 새 Windows 빌드: Licensing 초기화 60초 시간 초과 후 에디터 크래시. 새 빌드 산출물 없음.
기존 플레이어에 DLL만 교체한 격리 진단도 level0 직렬화 호환 오류로 실행되지 않았다. 해당 진단본은 배포하지 않는다.
실제 Unity 화면·허수아비 발광·전환 모션·종료 버튼 클릭·BGM 청취는 미검증이다. Windows 실행 파일 배포는 새 빌드와 실제 실행 검사 후에만 진행한다.
실제 Supabase 키/설정은 읽거나 출력하지 않았고 DB 마이그레이션/초기화/전송은 실행하지 않았다. 실제 LIVE 및 운영 DB 연동은 별도 검증이 필요하다.

## 학교 PC 최종 확인
1. main Pull 후 Unity 6000.4.0f1에서 컴파일/Windows 빌드.
2. 새 프로필 튜토리얼 완주, 종료 암전에서 보스/맵 노출이 없는지 확인.
3. 몸체 패링 예고, F8/F9, 재실행/로비 복귀, 잠금 3종(기존 해금 세이브 포함), 메인/ESC 종료 확인.
4. 아라쿠스/파네스 전투·저장·강화·궁극기·BGM 확인. 합의한 DB 환경에서 proficiency/LIVE 확인.
5. 새 실행 파일 확인 후 검증된 main에 태그를 붙이고 기존 Publish-WindowsRelease.ps1로 사전배포.

롤백: 통합 PR의 merge commit을 revert하는 별도 PR. 강제 push 및 저장 데이터 삭제/DB 롤백은 하지 않는다.
