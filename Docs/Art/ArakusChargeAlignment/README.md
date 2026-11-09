# 아라쿠스 돌진 프레임 정렬

기준: PR #17 `fix/arakus-charge-polish` (`a161f48`) 위에 쌓은 브랜치 `fix/arakus-charge-frame-alignment`.
#17이 main에 병합되면 이 PR의 대상도 main으로 바꾼다.

## 문제

철벽 돌진(`charge`) 중에는 `Charge` 2번·3번 프레임이 0.1초마다 번갈아 나온다.
두 프레임은 그림 안에서 몸이 그려진 위치가 서로 다르다. #17의 1.25배 그림 기준으로 골반이 44px 떨어져 있다(main 그림은 36px).
모든 프레임 피벗이 0.5라서 게임에서는 몸이 0.1초마다 약 1m씩 앞뒤로 튀고, 그림과 실제 판정 위치도 그만큼 어긋난다.
방패 투척 후 대쉬(`throw`)의 `ShieldThrowing_DashAttack` 2번·3번 프레임도 같은 문제로 26px(약 0.65m) 흔들린다.

## 변경

프레임마다 피벗 x를 골반 위치에 맞춰, 골반이 Idle 프레임과 같은 자리(피벗 오른쪽 2.3px)에 오게 했다.

| 스프라이트 | 피벗 x (px / 192) | 정렬 전 골반 | 정렬 후 골반 |
|---|---|---|---|
| Charge_00 (선딜) | 96 → 105 | +11.5px | +2.5px |
| Charge_01 (출발) | 96 → 124 | +30.6px | +2.6px |
| Charge_02 (달리기) | 96 → 83 | −10.9px | +2.1px |
| Charge_03 (달리기) | 96 → 127 | +33.4px | +2.4px |
| Charge_04 (정지) | 96 → 98 | +3.9px | +1.9px |
| ShieldThrowing_DashAttack_03 (달리기) | 96 → 122 | +28.6px | +2.6px |

- 달리기 루프 몸 흔들림: Charge 44.3px → 0.3px, ShieldThrowing_DashAttack 26.3px → 0.3px.
- 바꾼 것은 `.meta`의 프레임별 `pivot.x` 6개뿐이다. PNG 픽셀, 발 높이(pivot y), GUID, 슬라이스, PPU, 코드, 판정, 시간 값은 그대로다.
- ShieldThrowing_DashAttack의 나머지 프레임은 제자리 동작이고 차이가 6px 이하라 그대로 두었다.
- 피벗은 정수 픽셀로 맞춰 도트가 반 픽셀에 걸리지 않게 했다.
- 골반 위치: 붉은 깃털을 뺀 몸 픽셀 중 키의 55~75% 높이 띠의 평균 x.

`charge-alignment.gif`: 위는 #17 그대로, 아래는 이 PR이다. 빨간 삼각형이 시뮬레이션상 실제 위치다.
게임 코드와 같은 타이밍(선딜 1.1초, 13m/s, 프레임 선택 규칙)으로 스프라이트를 다시 그린 것이고 **Unity 캡처가 아니다**.
`frame-overlay.png`: 달리기 프레임을 겹친 것이다. 왼쪽부터 Charge 정렬 전·후, ShieldThrowing_DashAttack 정렬 전·후.

## 검사

```sh
python3 Tests/verify-arakus-charge-art.py      # #17 그림 검사. meta는 "pivot x 외 동일"로 확인
python3 Tests/verify-arakus-dash-alignment.py  # 새 검사. 피벗 값, 골반 정렬, pivot x 외 바이트 동일
```

- `verify-arakus-charge-art.py`는 원래 `Charge.png.meta`가 main과 바이트 단위로 같아야 했다.
  이 PR은 피벗 x를 의도적으로 바꾸므로, 피벗 x만 제외하고 나머지가 바이트 단위로 같은지 확인하도록 고쳤다.
  피벗 x 값은 새 검사가 정확한 값으로 고정한다. #17 README의 "Charge.png.meta byte-identical" 문장은 이 PR 이후에는 "pivot x 외 동일"로 읽는다.
- 새 검사는 main의 원래 meta로 돌리면 실패한다(`Charge_00 pivot 96.0px, expected 105px`).
- #17이 Charge 그림을 다시 바꾸면 골반 정렬 검사가 실패하므로 피벗을 다시 계산해야 한다.

## 미검증

Unity 6000.4.0f1에서의 재임포트, 실제 게임 화면, 좌우 반전 상태, 기둥 충돌 장면, Windows 빌드는 실행하지 않았다.
Unity에서 확인할 것: 철벽 돌진과 투척 후 대쉬를 좌우 양쪽으로 보고, 달릴 때 몸이 앞뒤로 튀지 않는지, 선딜·출발·정지 자세로 바뀔 때 몸이 앞뒤로 튀지 않는지, 기둥에 박히는 위치가 그림과 맞는지.
