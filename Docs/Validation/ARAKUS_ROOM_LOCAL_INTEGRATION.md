# 아라쿠스 왕좌 배경·기둥 — Unity 통합 (PR #19 선행 필요)

## 브랜치 의존 관계 — PR #19 선행 필요

최신 main 기준의 일반 작업 규칙에 대한 승인된 예외다. 아트 전용 PR #19의
`feat/arakus-room-art` 커밋 `e4626a8c4271a9fdea14cad45bdccf8e49bbdffa`에서
`feat/arakus-room-integration`을 분기했다. PR Base는 우선 `feat/arakus-room-art`로 설정한다.
PR #19의 원본/적용용 배경, 별도 기둥 시트, 외곽선 PNG와 .meta를 의존성으로 사용하며 중복 추가하지 않는다.
기존 아트 브랜치 작업 트리의 미커밋 통합 파일은 삭제/초기화하지 않고 보존했다.
별도 worktree에서 승인된 다섯 파일만 복사·검증했다. main과 PR #13~#18은 가져오거나 수정하지 않았다.

이 변경은 Draft 검토 대상이며 PM이 병합한다. AI는 병합하지 않는다.

### PM 병합 순서

1. 아트 전용 PR #19를 확인하고 main에 먼저 병합한다.
2. 통합 브랜치에서 최신 main을 반영하고 본 문서의 검사와 Unity 검증을 다시 수행한다.
   PR #19를 squash 병합했다면 Base만 변경할 때 아트 변경이 중복 표시될 수 있다.
   통합 브랜치에 최신 main을 일반 merge하는 등의 방법으로 공통 이력을 정리한 뒤 diff를 확인한다.
   충돌은 PM/담당자가 의도를 확인하며, 강제 Push나 무조건 덮어쓰기는 하지 않는다.
3. PR Base를 main으로 바꾸고 변경 파일이 통합 코드2개·기둥 교체1개·테스트1개·문서1개인지 확인한다.
   그 사이 승인된 추가 검증 수정이 생기면 최종 목록을 다시 확인한다.
4. 아래 Unity6000.4.0f1 및 Windows 검증을 마친 후 PM이 Draft 해제/병합 여부를 결정한다.

## 이번 재검증

별도 격리 디렉터리에서 현재 소스로 기존 Headless 테스트를 다시 컴파일·실행해119개 통과.
리소스/보존 검사25개 통과, 변경 C#2개 Roslyn 문법 오류0개. git diff --check 통과.
새로운 Unity 컴파일·실행/Windows 빌드를 수행한 것은 아니다.


## 원본 및 적용본

ZIP `Arakus_Original_PNGs_For_Codex.zip`의 PNG를 직접 읽었다.
- 배경: 1248×702 RGB. 전달 원본은 StreamingAssets/Art/ArakusRoom/Arakus_Throne_Background_16x9.png에 그대로 보존.
- 기둥: 384×192 RGBA. 전달 시트를 Resources/ArakusV1/Pillar.png에 바이트 그대로 적용.
- 기존 Pillar.png.meta 전체 바이트 보존: GUID, Sprite ID, PPU56, Pivot(0.5,32/192), 두 프레임 이름/영역 동일.
- 새 정상/균열 불투명 높이 모두 120px, 마지막 불투명 행 159. 기존 루트1.4에서 표시 높이3m.

원본 배경의 전면 작은 기둥을 그대로 두면 실제 기둥이 사라진 뒤에도 남는다.
별도 Arakus_Throne_Background_Game.png에서 해당 두 직사각형만 주변 계단의 같은 행 픽셀로 이어 붙였다.
수정 범위: (430,453)-(511,608), (791,453)-(870,608), 오른쪽/아래 배타 좌표.
제작 시 사용한 원본의 같은 행 소스 x는 각각512,688이다. 크기와 나머지 영역은 그대로 보존.
블러·생성형 보간 없음. 붙인 계단의 질감 연결은 실제 Unity 화면에서도 미술 검토가 필요하다.

## 변경 코드

WorldArtist.Render: bossIndex==0 및 Battle/Result일 때만 DrawThroneRoom.
기존 절차 배경/Castle/바닥/안개는 해당 분기에서 제외. 새 파일이 없으면 기존 배경으로 복귀.
전투 예고/플레이어/보스/투사체/피격/패링 렌더링은 기존 공유 경로 유지.

배경 월드 폭26.5m, 비율 보존, 원본 y608을 바닥y0에 맞춘다. 아래 석재62px 스트립만 반복 연장.
카메라 수식·위치·충돌 바닥은 변경하지 않았다. 일반16:9/4:3/21:9 구도를 보존하며 어두운 가장자리 여백이 생긴다.
기존 패링 줌과 화면 흔들림 중 잠깐의 크롭은 실제 실행 확인 필요. 배경 자체를 화면비마다 왜곡하지 않는다.

ArakusVisuals.DrawPillar: 기존 프레임/좌표/페이드 값을 전달받아 얇은 외곽선만 추가.
Pillar_Outline.png는 각 프레임 알파의 3×3 최대 필터에서 원래 알파를 뺀 바깥1px 마스크.
별도 새 Sprite 시트 .meta로 PPU/Pivot 계약을 맞췄으며 기존 기둥 GUID에는 영향 없음.
기본 Sprite 재질과 어두운 붉은 색(.65,.12,.18)을 사용. 불투명도는 평소.25, Dash 패턴 중.55.
가산 셰이더/Bloom/깜빡임/외부 패키지 없음. 외곽선은 본체보다 한 정렬 순서 뒤에 위치.
충돌 후 균열 프레임과 외곽선 모두 기존0.6초 페이드에 동기화된다.

BossPatternSystem, TowerGame, ActorView, DDA/LinUCB/Save/DB, Scene/Prefab/ProjectSettings 미수정.
기존 x=-4,+4 통과 충돌, 3.2초 그로기, 새 전투 기둥 초기화 로직을 그대로 사용한다.
두 번째 프레임은 균열 상태이며 완전 붕괴 애니메이션은 아니다. 무너짐/영구 잔해가 필요하면 추가 아트가 필요하다.

## 실제 실행한 검증

- 별도 /tmp/arakus-room-local-tests 복사본에서 현재 전투 소스를 HEADLESS로 컴파일/실행: 119 assertions PASS.
  UnityShim 사용. CloudSync/실제 DB 설정을 복사하거나 실행하지 않음. 테스트 Save 경로도 /tmp 아래.
- Roslyn C# 문법 검사: WorldArtist.cs/ArakusVisuals.cs, 0 오류. Unity API 타입 검사/컴파일을 뜻하지 않음.
- Tests/verify-arakus-room-integration.py: 25 PASS / 0 FAIL.
  원래 GUID/PPU/Pivot 메타, 발 정렬, 외곽 마스크, 보호 코드 무변경, 배경 수정 범위, 세 화면비 기본 시야 확인.
- git diff --check PASS.

```sh
python3 Tests/verify-arakus-room-integration.py --output /workspace/artifacts/arakus-integration
```

결과/오프라인 목업: /workspace/artifacts/arakus-integration/
- headless-tests.log
- resource-checks.json
- offline_640x360.png / offline_480x360.png / offline_840x360.png

목업은 카메라 수식과 실제 스프라이트를 이용한 Pillow 합성으로, Unity 실행 캡처가 아니다.
오프라인 목업은 실제 재질/외곽선/HUD/동적 VFX를 완전히 재현하지 않는다.

## Unity에서 반드시 확인

Unity6000.4.0f1 미설치로 Unity 컴파일/실플레이/Windows 빌드 미실행.
1. Unity에서 오류 없이 임포트/컴파일되는지, Pillar_Outline이2개 Sprite로 읽히는지 확인.
2. Title/Lobby/다른 보스 기존 화면, 아라쿠스 Battle/승리·사망 Result 새 화면 확인.
3. 양쪽 x±4 기둥과 발/바닥 정렬, 돌진 중 외곽선 강화, 양방향 충돌의 균열 전환/0.6초 소멸/그로기 확인.
4. 재전투에서 기둥 복구, 일시정지/Result에서 잔상 및 시간 처리 확인.
5. 16:9/16:10/4:3/21:9, 패링 줌/화면 흔들림, 어두운 여백·계단 보정 경계 확인.
6. 빨강 위험 예고와 파랑 패링·HUD 가독성 확인.
7. Windows 빌드와 실행, StreamingAssets 파일 포함 확인.
