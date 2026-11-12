# 팀원 자율 PR·Windows 배포 안내 (2026-11-12)

## 권한·승인 원칙

- `seojb1204`: 소유자(Admin), `codenoob636` 및 `kimdal0803`: 공동 작업자(Write). 세 명 모두 브랜치 push, PR 작성·리뷰·병합 및 GitHub Release 생성 가능.
- 일반 작업은 **사용자(소유자)의 개별 최종 승인 없이** 팀원끼리 수행합니다.
- `main` 직접 push/force push는 하지 않고 **작업 브랜치 → PR → 필요한 검사·리뷰 → main 병합** 순서를 따릅니다.
- 팀원 1명 이상의 리뷰를 권장합니다. GitHub가 강제하는 리뷰 수·검사·브랜치 보호 설정은 반드시 지킵니다. 보호 설정 자체를 우회하지 않습니다.
- 주요 기획 변경, 제품 정책·권한 구조 변경, 비용, 운영 DB 변경, 데이터 삭제, 외부 플랫폼 정식 배포는 별도 합의 대상입니다.

## 모든 팀원의 Windows 빌드 준비

1. Windows PC에 Unity Hub 및 **Unity 6000.4.0f1**을 설치하고 Windows Build Support 모듈을 추가합니다.
2. 각자의 Unity 계정으로 로그인·라이선스를 활성화합니다. 계정 자격증명이나 라이선스 파일을 공유·커밋하지 않습니다.
3. Git과 GitHub CLI(`gh`)를 설치합니다. 최초 1회 자신의 계정으로 `gh auth login`을 실행합니다.
4. GitHub의 최신 `main`을 가져옵니다. 작업 파일이 있으면 먼저 자신의 브랜치에 안전하게 보존해야 합니다.

```powershell
git switch main
git pull --ff-only origin main

# Unity Windows 빌드 + ZIP/SHA-256 생성, 게시하지 않음
.\Publish-WindowsRelease.ps1 -Tag v0.5.0-test.1
```

생성물은 저장소의 `Builds/ReversalTower-v0.5.0-test.1-Windows-x64.zip` 및 같은 이름의 `.sha256`입니다. ZIP에는 Unity 실행 파일·필수 데이터 폴더·필요한 런타임 DLL을 포함합니다. `Builds/`는 Git 추적 대상이 아닙니다.

## 팀원 누구나 GitHub Release 게시

- 사전 조건: `main`이 `origin/main`과 일치하고, 작업 폴더가 깨끗하며, 빌드가 성공해야 합니다.
- **공개 저장소의 Release 자산은 외부에서도 내려받을 수 있습니다.** 코드·빌드 안에 DB 자격증명·개인정보·기밀 파일이 포함되지 않았는지 팀원이 확인합니다. 저장소에는 DB 연동 코드가 있으므로 운영 DB를 테스트에 연결하지 마세요.
- 빌드를 직접 실행하고 최소한 타이틀 → 로비 → 보스전 → 결과 → 로비 흐름을 실제 Windows에서 검사합니다. 자동 로직 테스트만으로 통과를 선언하지 않습니다.
- 이전에 생성한 같은 태그의 ZIP을 재사용하려면 GitHub Releases 웹 UI에서 검증한 ZIP을 올릴 수 있습니다. 스크립트는 기존 ZIP을 자동 덮어쓰지 않습니다.

```powershell
# 새 버전 번호(태그)를 사용하여 Windows 빌드부터 게시까지 실행
.\Publish-WindowsRelease.ps1 -Tag v0.5.0-test.2 -Publish
```

`-Publish`를 넣으면 **GitHub의 공개 Pre-release**가 실제 게시됩니다. 게시 후 팀원 누구든 [Releases](https://github.com/seojb1204/TeamRedBull2/releases)에서 내려받습니다. 정식 Release로 전환하거나 Steam 등 외부 플랫폼으로 배포하는 일은 이 스크립트 범위 밖입니다.

## 검증과 장애 대응

- 정상 기준: 빌드 스크립트 종료 코드 0, 새로 생성된 `ReversalTower.exe`와 `ReversalTower_Data`, ZIP, SHA-256 파일, Git 커밋 SHA 기록.
- Unity 컴파일·빌드 실패 시 Release를 게시하지 않습니다. 잘못된 ZIP을 게시했을 때는 원인을 기록하고 **새 태그로** 수정판을 게시합니다. 기존 Release는 임의 삭제하지 않습니다.
- PR 병합이 막히면 `Settings → Branches`의 `main` 보호 규칙에서 승인자 지정·필수 승인 수·필수 검사 상태를 소유자가 **한 번** 확인해야 합니다. 본 가이드는 보호 설정을 자동 변경하지 않습니다.
- GitHub Actions 자동 Unity 빌드는 아직 설정하지 않았습니다. Unity Personal의 CI 라이선스 제약 때문에, 이번 단계에서는 각 팀원이 자신의 라이선스가 활성화된 Windows PC에서 빌드합니다.
- 업로드나 빌드를 실행하지 않은 경우 성공했다고 보고하지 않습니다. 시험판도 실제 Windows에서 검증한 범위만 기록합니다.
