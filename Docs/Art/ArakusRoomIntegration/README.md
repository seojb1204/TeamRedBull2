# 아라쿠스 전달 아트 — PR #19 아트 전용 추가분

이번 커밋은 신규 PNG와 .meta 및 이 아트 검증 문서만 포함한다.
WorldArtist.cs / ArakusVisuals.cs / 기존 Resources/ArakusV1/Pillar.png 교체본은 **로컬 미커밋 상태로 보존**한다.
이 아트 커밋만으로 게임 화면이 새 배경·기둥으로 바뀌지는 않는다. 통합 코드는 별도 검토 대상이다.

## 파일

| 경로 | 규격 | 용도 |
|---|---|---|
| StreamingAssets/Art/ArakusRoom/Arakus_Throne_Background_16x9.png | 1248×702 RGB | 전달받은 왕좌 배경 원본, 바이트 동일 |
| StreamingAssets/Art/ArakusRoom/Arakus_Throne_Background_Game.png | 1248×702 RGB | 배경 내부의 작은 기둥 두 개를 제거한 적용 후보 |
| StreamingAssets/Art/ArakusRoom/Arakus_Pillar_sheet_384x192.png | 384×192 RGBA | 전달 정상·균열 시트 원본, 바이트 동일 |
| Resources/ArakusV1/Pillar_Outline.png | 384×192 RGBA | 각 프레임의 바깥 1px 외곽선 마스크, 흰색 마스크에 런타임 색을 곱하는 용도 |

각 PNG에 신규 GUID의 .meta를 추가한다. 기존 StoneFortress/IronWarworks 및 기존 기둥 메타/GUID는 변경하지 않는다.
배경과 전달 기둥 원본은 StreamingAssets의 DefaultImporter이다. 현재 Sprite로 임포트된 기존 기둥을 교체하는 커밋이 아니다.
외곽선만 Sprite Multiple 192×192 두 프레임, PPU56, 발 Pivot=(0.5,32/192), Point/no mipmap 계약을 갖는다.
기존 애니메이션 전체 설치 도구에 새 정의를 추가하지 않는다.

## 배경 보정

원본을 유지하고 별도 적용본에서 두 사각 영역만 같은 행의 계단 픽셀로 연결했다.
- 좌측: (430,453)~(511,608), 같은 행 소스 x512부터 복사
- 우측: (791,453)~(870,608), 같은 행 소스 x688부터 복사
- 좌표는 PNG 상단 기준, 오른쪽/아래 경계 배타. 블러/보간/원본 덮어쓰기 없음.

배경에 그려진 작은 기둥이 실제 기둥 파괴 후에도 남는 혼동을 줄이는 보정이다.
계단 텍스처의 연결 경계는 실제 게임 화면에서 최종 미술 검토가 필요하다.

## 기둥 및 외곽선

왼쪽은 정상, 오른쪽은 균열 상태다. 두 프레임 높이120px, 마지막 불투명 행159.
기존 PPU56×루트1.4 계약이라면 높이3m이다. 두 번째 프레임은 완전 붕괴가 아니므로 붕괴 애니메이션/영구 잔해는 별도 아트가 필요하다.
외곽선은 프레임별 알파를 3×3 최대 필터로 확장한 뒤 원래 알파를 빼서 만들었다.
발광 강도/돌진 상태/0.6초 페이드 연결은 본 아트 커밋의 범위가 아니다. 새 셰이더/패키지는 없다.

## 검사 및 한계

art-validation.json에 PNG 원본 일치, 규격, 발 기준, 외곽선 마스크, 편집 영역 제한,
기존 에셋/GUID 보존 및 신규 임포트 계약을 검사한 결과와 SHA-256을 기록했다: 18 PASS / 0 FAIL.
Unity 임포트·실행, 실제 전투 가독성, Windows 빌드는 검증하지 않았다.
별도 로컬 통합에서 수행한 전투 Headless 테스트는 이 아트 전용 커밋의 Unity 실행 검증을 의미하지 않는다.
