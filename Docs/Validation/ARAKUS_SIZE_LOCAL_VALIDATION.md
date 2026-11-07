# 아라쿠스 크기·코드 피격 영역·근접 범위 — 로컬 검증

기준: feat/arakus-room-integration / bdda9ebd8e9d607e6c4ffb54d0c74d68e84f314e.
자동 검증은 로컬 코드와 테스트/문서만 수정한 상태에서 수행했다. 이후 사용자 승인에 따라 기존 통합 브랜치에 Commit·Push한다. 새 PR 생성/병합은 하지 않는다.

## 변경 파일

- ActorView.cs: bossActor && bossIndex==0일 때만 root scale2.2. 다른 Actor는 기존1.4, 위치/피벗/반전/Animator 선택은 유지.
- ArakusVisuals.cs: 풀의 props/FX SpriteRenderer를 실제 부모 lossyScale로 역보정해 기존 월드 스케일1.4 유지.
  기둥/외곽선/방패 투사체/충격·분노 등 풀 이펙트에 적용. 본체에 붙은 파란 윤곽은 본체를 따라가는 기존 구조 유지.
- BossPatternSystem.cs: ArakusGeometry에 피격 크기와5종 공격 범위 정의. 기존 데이터는 수정하지 않는다.
  근접 추적/선택 거리/실제 타격이 동일 유효 범위를 읽는다. rush 접촉만 확대하며 이동·판정 간격은 유지.
- TowerGame.cs: 아라쿠스 BossWidth 및 발 기준 세로 피격 구간, friendly 투사체의 수신 AABB 적용.
  다른 보스의 기존 거리 조건과 플레이어의 스킬 범위/피해/시간/조작/무적/패링 조건은 그대로다.
- WorldArtist.cs: 아라쿠스 근접 예고 범위/높이를 실제 판정 계산과 연결. 왕좌 DrawThroneRoom은 바이트 동일.
- Tests/ArakusSizeTests.cs, run-arakus-size.py, verify-arakus-size-art.py: 신규 회귀/실행/리소스 검사.
- 본 문서.

Resources/Animation/Scene/Prefab/ProjectSettings/배경PNG/기둥PNG/기존 meta는 변경하지 않았다.
GameData/GameServices/CloudSync/SaveStore/LobbyCards도 변경하지 않았다.

## 외형 및 피격 크기 산정

기존 임포트 PPU56, 발 Pivot(96,160 PNG상단좌표), 기존 root1.4를 유지 기준으로 사용했다.
Idle4프레임 불투명 bbox: (70,72,128,160), (71,71,128,160), (71,71,128,160), (72,72,129,160).
따라서 발 기준 최대 높이는89px, 좌우 대칭 수신 영역의 반폭은max(96-left,right-96)=33px.
한쪽 최대 범위를 좌우 모두에 적용해 반전 시 동일한 AABB를 사용한다.

| 항목 | 이전 | 변경 후 |
|---|---:|---:|
| 아라쿠스 root |1.4|2.2 (1.5714배)|
| Idle 첫 프레임 표시 높이 |88/56×1.4=2.20m|88/56×2.2=3.4571m|
| 전체 Idle 높이 envelope |기존 코드2.2m|89/56×2.2=3.49643m|
| 피격 반폭 BossWidth |0.9m|33/56×2.2=1.29643m|
| props/풀 이펙트 world scale |1.4|1.4|
| 정상 기둥 높이 |120/56×1.4=3m|3m|
| 플레이어 첫 Idle 표시 높이 |93/75×1.4=1.736m|동일|

새 Idle 첫 프레임은 플레이어 약1.99배 높이다. 실제2.2배 확대가 아니라 root를1.4에서2.2로 변경한 것이다.
캐릭터 애니메이션77프레임 모두 마지막 불투명 행159(경계160)로 발 기준이 일치함을 검사했다.
수신 AABB는 Idle 기준의 고정 envelope이며 무기 끝 전체를 피격 대상으로 만들거나 숙임 상태별로 줄이는 방식은 아니다.

플레이어 공격의 세로 유효 구간은 기존 플레이어 측 높이2.2m를 유지해
[playerFeet, playerFeet+2.2]와[bossFeet,bossFeet+3.49643]의 겹침을 검사한다.
단순 abs거리3.5로 바꾸지 않아, 보스가 높이 떠 있는데 지상 공격이 닿는 부작용을 줄였다.
기본공격/Q/W/E/궁극기의 원래 거리/피해/실행 타이밍은 바꾸지 않았다.
반사/플레이어 friendly 투사체는 확대된 보스 몸체와 자신의 반경을 사용한다. 투사체 자체 크기/속도/공격 사거리는 변경하지 않았다.

## 근접 공격 후보 수치

| 패턴 | 이전 거리 | 새 거리 | 배율 | 이전 높이 → 새 높이 |
|---|---:|---:|---:|---:|
|cleave|2.9|4.0|1.3793|2.2→3.3|
|shield|1.9|2.5|1.3158|2.2→2.7|
|kick|1.4|2.1|1.5|2.2→2.2|
|slash|2.4|3.2|1.3333|2.2→3.4|
|rush 접촉|1.4|2.0|1.4286|2.2→2.7|

위 거리는 보스 중심에서 플레이어 기준점까지의 기존 코드 의미를 유지한다.
원본 p.range/maxRange를 변경하지 않고 상대 보정 계수로 계산하므로 ScriptableObject와 Catalog를 이중 수정하지 않는다.
추적 기준은 유효 거리×0.75, 선택 maxRange는 원래 maxRange+거리 증가분을 사용한다.
추적형 Slash의 기존 선택 여유1.5m와 minRange/Phase 조건은 유지한다.

판정 높이는 active-frame alpha 높이를 참고했다. Sword_Swinging 타격 프레임 높이84px≈3.3m,
NormalAttack_L 타격 프레임68px≈2.67m, NormalAttack_R 타격 프레임87px≈3.42m.
kick은 낮은 공격 특성을 보존해 세로 범위를 유지했다. rush는 방패 접촉과 대시 회피를 고려해2.7m로 제한했다.
가로는 전신 alpha bbox를 그대로 공격 사거리로 쓰지 않았다. 무기/몸체의 전진 범위와 기존 타격 여유를 함께 고려한 튜닝 후보값이다.
예를 들어 NormalAttack_R 타격 프레임의 오른쪽 끝x161은 새 root에서 약2.55m,
kick의 x141은 약1.77m, Sword_Swinging의 x182는 약3.38m다. 플레이어 폭/기존 판정 여유를 고려해 거리값을 설정했다.
최종 체감 밸런스와 점프 회피 여유는 Unity 실전 확인이 필요하다. 키가 커진 만큼 일부 공격의 상단 영역이 커진다.

근접 예고는 뒤쪽 허용0.3m부터 앞쪽 새 거리까지, 높이도 같은 계산을 사용한다.
rush 예고는 기존5m 이동+접촉2m, 뒤 허용0.3m를 포함한다.
PlayerHit의 기존 패링/대시 무적 조건을 바꾸지 않아 확장 영역에서도 동일하게 작동한다.
sweep/charge/throw/slam/step/rage의 원래 범위·선택 거리는 유지한다.
charge 기둥 위치±4,3.2초 그로기와 기둥0.6초 페이드·붉은 외곽선은 유지한다.
DDA -2~+2의 피해/선딜/쿨타임, Rage1.5배, LinUCB 정책/저장/DB는 변경하지 않았다.

## 실제 실행한 검사

| 검사 | 결과 | 범위/한계 |
|---|---|---|
|기존 HeadlessTests|119 PASS|실제 전투 C# + UnityShim|
|기존 ArakusAnimationTests|422 PASS|포즈/프레임 선택 및 시간 경계, 실제 Animator 렌더 아님|
|신규 ArakusSizeTests|957 PASS|실제 Player TickAction/적 ExecutePattern/러시 TickBoss/투사체 TickShots/기둥 충돌 호출|
|스프라이트·보호 범위 검사|87 PASS|77프레임 발 기준, 보호 파일, 기둥/배경/메타 보존, 예고 연결 정적 확인|
|C# 문법 검사|6파일 오류0|변경5개 C#와 신규 테스트, Unity API 컴파일 아님|

신규957검사는 좌우,5레벨,Rage,근접의 안/밖/뒤/상단 경계, 대시·가능한 패링,
플레이어 J/Q/W/E/R의 확대된 몸체 적중, friendly 투사체 적중/미스, 기둥 충돌, 다른 보스 보존을 포함한다.
props의 실제 Transform 크기는 algebra와 source wiring으로만 검사했다. 실제 Unity Transform/Renderer 실행으로 검증한 것은 아니다.

```sh
python3 Tests/run-arakus-size.py --dotnet /path/to/dotnet --output /tmp/arakus-size-results
python3 Tests/verify-arakus-size-art.py --output /tmp/arakus-size-results
```

임시 디렉터리에 허용 소스와 테스트만 복사한다. 실제 DB 설정/CloudSync는 실행하지 않으며 기존 세이브/대기열에 접근하지 않는다.
결과와 소스 해시: /workspace/artifacts/arakus-size-regression/final/.
기존 아트 브랜치 작업 트리와 승인된 통합 커밋은 변경하지 않았다. 검증 당시 이 변경은 통합 worktree의 미커밋 상태였으며, 검증 후 사용자 승인으로 기존 통합 브랜치에 기록한다.

## PR #17 충돌 검토

최신 읽기 기준: a161f489ccc1135765b54dcd0e4febe40af2fe67.
공통 main의 BossPatternSystem을 base로 현재 미커밋 코드와 PR17 코드를 git merge-file로 비교했다.
텍스트 충돌0, 실제 git merge/commit은 하지 않았다.

- PR17은 rush 이동 시간을 공유 상수1.30초로 바꾸고 종료 시 moveTo 도달을 보정한다.
- 이번 작업은 접촉 거리/높이만 바꿨고 기존 이동시간1.75초 줄과 .25초 타격 간격은 수정하지 않았다.
- 현재 작업 트리 자체는 PR17을 가져오지 않았으므로 여전히1.75초다. PR17 반영 시1.30초와 새2m 접촉을 함께 유지해야 한다.
- PR17의 Charge.png/ArakusAnimationSelection/ArakusPatternTiming은 이번 작업에서 수정하지 않았다.

별도 임시 폴더에만 합성된 BossPatternSystem과 PR17 타이밍/애니메이션 코드를 넣어 실행했다.
기존 Headless119 PASS, 신규 Size957 PASS, PR17 원래 ChargePolish325 PASS.
이는 합성 소스 수준 호환 검증이며 실제 브랜치 병합이나 Unity 실행 검증은 아니다.
추후 주변 줄이 변경되면 충돌 가능성은 다시 확인해야 한다.

## Unity 필수 확인 — 현재 미검증

Unity6000.4.0f1 Editor가 없어 Unity 컴파일/Animator/실플레이/Windows 빌드는 실행하지 못했다.
1. 본체 root±2.2, 플레이어1.4, 발y0, 좌우 반전과77프레임의 바닥 정렬 확인.
2. 기둥/외곽선/투사체/충격·분노 FX의 world 크기가 기존과 같은지 확인.
3. 기본공격/Q/W/E/궁극기 및 반사 투사체가 몸체 상단/양쪽 끝에서 맞는지 확인.
4. 좌우 근접 예고/실제 접촉, 패링·대시·점프 여유, windup 중 추적/패턴 선택 거리 확인.
5. 일반/Rage/DDA5레벨, 양쪽 기둥 충돌/균열/페이드/그로기 및 재전투 확인.
6. 몸체를 따라 커지는 파란 테두리와 기존 크기 독립 FX의 위치·가독성 확인.
7. 다른 보스/플레이어/왕좌 배치/카메라/HUD가 유지되는지와 Windows 빌드 확인.
