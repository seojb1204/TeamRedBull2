# 발표·전시용 원클릭 패키지 (테스트 빌드)

이 문서는 **빌드 담당자용**이다. 패키지가 완성된 Windows 사용자 PC에서는 ZIP 전체를 압축 해제하고 `Start-ReversalTower.cmd`만 더블클릭한다. Python/.NET SDK/Unity Editor/Git/PowerShell 설치나 실행 인수·폴더 선택은 필요 없다. 기존 게임 단독 실행은 `game/ReversalTower.exe`로 유지한다.

현재 클라우드에서는 실제 Windows Python과 C# 자체 포함 런타임을 만들었다. **Unity Windows 게임/완성형 게임 ZIP 및 실제 Windows 실행은 미검증/미생성**이다. `DdaLabRuntime-Windows-x64-NO-GAME.zip`은 게임 없는 런타임 캐시이며 최종 게임 배포 ZIP이 아니다.

## 빌드 PC에서 한 번에 생성

필요: Python3.10+, .NET8 SDK, Unity6000.4.0f1 Windows Build Support 및 유효한 Unity 라이선스. 이는 최종 사용자 PC 요구사항이 아니다. 일반 Lab 개발 실행은 기존 Start-DdaLab.cmd/lab.py를 계속 사용한다.

저장소 루트의 CMD 예시(모든 출력 경로는 **새 경로**, 아래 한 줄):

```cmd
python -X utf8 Tools\DdaLab\package_windows.py --unity "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" --game "D:\TowerBuild\test-20261121\game" --dotnet "C:\Program Files\dotnet\dotnet.exe" --engine-output "D:\TowerBuild\test-20261121\engine" --download-python --python-source nuget --python-version 3.13.7 --python-embed "D:\TowerBuild\python.3.13.7.nupkg" --output "D:\TowerBuild\ReversalTower-Exhibit-Test-20261121.zip"
```

- `--download-python`: 공식 PSF NuGet의 Windows Python 패키지를 받는다. 공식 python.org 임베디드 ZIP도 `--python-source python.org` 또는 기존 ZIP 경로로 지원한다. 클라우드에서는 python.org 직접 다운로드가 프록시403으로 실패했고 공식 PSF NuGet은 성공했다.
- 이미 Python ZIP/nupkg를 보유하면 **--download-python을 빼고** 같은 `--python-embed` 경로를 사용한다. 추가적인 무결성 기준이 있으면 다운로드 시 `--python-sha256`을 지정한다.
- .NET runtime pack 다운로드는 **빌드 담당자의 publish 단계만** NuGet을 사용한다. 일반 개발 Lab의 SDK8 고정/offline `<clear/>` 정책은 그대로다. 최종 패키지는 Python과 .NET runtime을 포함하여 대상 PC에서 복원·설치하지 않는다.
- 원본 프로젝트를 임시 폴더에 복사하고 기존 `BuildProject.Build`를 재사용한다. 원본 씬/Prefab/설정이나 기존 `../Windows` 빌드·ZIP을 덮어쓰지 않는다. 새 게임 경로/엔진 경로/ZIP이 이미 있으면 실패한다.
- 게임 빌드는 전체 Assets/Packages/ProjectSettings hash를 `game-build.json`에 기록한다. 패키징 시 최신 소스와 다르면 거부한다. 엔진은 실제 C# 소스 hash 및 EXE hash를 검증한다.
- config의 공개 URL/키는 그대로이고 `proficiencySync=true`가 소스·빌드에 모두 있어야 한다. anon/publishable 키만 허용한다. 관리자/서버 secret을 클라이언트에 넣지 않는다.
- Unity 라이선스/빌드 실패는 새 게임 폴더 옆 `<이름>-unity.log`, .NET publish 실패는 엔진 폴더 `publish.log`에 남는다. 빌드 중 소스 변경은 실패 처리한다.
- 생성 ZIP의 모든 파일 SHA256과 ZIP checksum sidecar를 생성한다. 기존 Get-WindowsReleaseFiles.ps1의 Unity Mono/x64 의존 파일 목록을 재사용한다. GitHub 게시/Commit/Push/PR은 수행하지 않는다.

검증만 다시 실행:

```cmd
python -X utf8 Tools\DdaLab\package_windows.py --verify "D:\TowerBuild\ReversalTower-Exhibit-Test-20261121.zip"
```

이는 파일/체크섬 검사이며 Windows 프로세스 실행·Unity Game View·실제 DB 통신의 성공을 뜻하지 않는다.

## 다음 main 업데이트 후

1. 최신 승인 소스를 준비하고 이번 미커밋 구현을 팀 절차에 따라 보존/반영한다. 이 도구는 Git 브랜치 전환/동기화/Commit/Push를 하지 않는다.
2. 새 날짜/버전의 `--game`, `--engine-output`, `--output`을 지정해 다시 빌드한다.
3. Python archive는 재사용한다. C# 소스 hash가 동일하면 기존 엔진을 `--engine`으로 재사용할 수 있다. 다르면 새 publish가 필수다.
4. 새 게임은 전체 소스 provenance가 일치해야 하므로 배경/아트/BGM 변경만 있어도 Unity 재빌드가 필요하다.
5. ZIP 검증 후 **다른 한글·공백 경로**, Python/.NET SDK/Unity/Git 없는 Windows 환경에서 원클릭·LIVE·DB를 다시 확인한다.
6. 이전 ZIP은 보존한다. 이번 출력은 메인/로비 최종 교체 이전의 테스트 빌드로 표시한다.

## 런타임 동작

- 매 실행에서 `%LOCALAPPDATA%/ReversalTowerExhibit/<패키지경로hash>/runs/<새GUID>`를 생성한다. 사용자 세이브/이전 LIVE 폴더를 지우지 않는다.
- 게임에 `-towerDdaMonitor <이번 live절대경로>`를 자동 전달한다. 다른 자동검증 인자를 추가하지 않는다. 정상 게임 저장·DB 정책을 바꾸지 않는다.
- 서버는127.0.0.1의 OS가 배정한 포트로 시작한다. 임의의 기존 서버 포트에 연결하지 않으며 이번 ready 파일만 사용한다.
- 이번 비어 있는 전용 LIVE root에만 연결한다. 새 게임 publisher가 만든 세션만 자동 선택하며 이전/모의 기록을 LIVE로 승격하지 않는다.
- 같은 패키지 경로의 중복 실행은 파일 lock으로 차단한다. 게임 종료 후 기록/DB 화면을 위해 Lab을 유지한다. **완전히 다시 시작하려면 실행기 창을 닫은 뒤 더블클릭**한다. 다른 설치 폴더는 별도 인스턴스다.
- Python/도구가 없거나 Lab이 실패해도 게임 단독 실행 경로가 있다. 게임과 Lab이 시작된 뒤 Ctrl+C/창 닫기로 모두 종료한다. 강제 종료 직후 파일 미수신은 기존3초 freshness로 DISCONNECTED 처리한다.
- `launcher.log`는 `%LOCALAPPDATA%/ReversalTowerExhibit`, 세션별 `lab.log/game-launch.log/experiments`는 위 run 폴더다.

## DB 분석과 보안 경계

Supabase DB 분석 탭은 **GET SELECT만** 수행한다. 원시 기록/키/owner/profile ID를 브라우저에 전달하지 않고 메모리에서 집계한다. 기본 공개 키로 조회할 수 없는 항목은 실패/미수집으로 표시한다. 관리자 키·RLS 완화·SQL migration·DB 데이터 생성/추가/수정/삭제를 수행하지 않는다.

- 발표용 데이터: `raw_json._demo.cohort == demo_exhibit_20261121`. owner_key 문자열 접두사는 사용하지 않는다.
- 패턴 샘플: `(owner_key,profile_id,battle_id)`→보스전의 event_id, 다시 assessment_event_id→평가의 event_id로 연결한다. 평가와 전투의 데모 구분이 다르면 제외/충돌 수를 기록한다.
- 다른 `_demo` 또는 raw_json 미확인은 실제 기록으로 합산하지 않는다. 실제 기록은 조회된 `_demo` 없는 기록이며 실제 사람 인증을 의미하지 않는다.
- 기존 `pattern_difficulty_by_proficiency`는 **실제·발표용 혼합 가능**으로 표시한다. 그 뷰 자체만으로 출처별 건수/평균을 추측하지 않는다.
- 출처별 별도 로컬 집계는 저장소 migration의 유효 최초 평가/confidence>=.5/실습시간>0/패링·회피 관측/Lv0/3개 owner/20기회 조건이다. **운영 view SQL을 공개 API로 확인하지 못하면 동일하다고 확정하지 않는다.**
- 총 프로필·평가/보스전/샘플, 그룹별 유효 최초평가/평균점수/수행·전체시간/패링·회피·피격, 보스전 및 패턴별 표본/권장 Lv를 표시한다. 원시 SELECT가 차단되면 이 수치를 생성하지 않는다. 페이지를 끝까지 조회하며 한도 도달은 부분 데이터를 전체로 표시하는 대신 실패한다.
- 사용자 전달 300프로필/300전투/3300샘플/뷰33행은 관측 기준값이며 자동으로 주입하지 않는다. SELECT 결과는 RLS로 필터링된 가시 범위일 수 있다.
- 설정 존재/GET SELECT/게임 POST/RPC 수락은 별도 상태다. 게임 진단은 현재 설정·평가/전투/패턴 전송 결과·마지막 동기화 큐 관측·수락UTC·마지막 오류를 전달한다. 기존409 처리는 새 INSERT 성공으로 표시하지 않는다. 보호 RPC의 하위 패턴 행은 개별 INSERT를 직접 확인한 것으로 표시하지 않는다.
- 이 클라우드의 실제 GET은 **프록시 CONNECT403**으로 막혀 운영 스키마/권한/300명 데이터를 확인하지 못했다. 저장소 SQL은 원시 숙련도 테이블 공개SELECT를 차단한다. Windows 네트워크에서 연결되어도 SELECT 권한 문제는 별도로 확인해야 하며 관리자 키로 우회하면 안 된다.

## Windows에서 마지막으로 확인할 것

- Python/.NET SDK/Git/Unity Editor 없는 새 Windows 환경에서 ZIP 전체 압축 해제→CMD 더블클릭.
- game/Lab/browser 자동 시작, F8/F9+LIVE, 새 세션·종료 후 DISCONNECTED, 재시작, 중복 실행, 포트 충돌, 한글·공백 경로.
- 게임 단독 실행/Lab 오류 시 게임 유지, 로그·저장 복원, 기존 배경/보스/궁극기/오디오 회귀.
- 실제 공개키 SELECT/RLS·schema 확인과 출처별300명 통계, 서버혼합뷰 표기.
- 실제 게임 송신은 별도 승인된 테스트 계획으로 검증한다. Codex의 클라우드 테스트는 모의 HTTP였으며 운영 INSERT를 실행하지 않았다.
