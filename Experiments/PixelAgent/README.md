# 픽셀 자동플레이 실험 (별도 실험, 본편 봇 아님)

## 무엇을 구현했나

- **random**: 무작위 7개 조작 기준선.
- **rule**: 화면의 빨강/청록 영역과 화면 변화를 보고 이동·공격·패링하는 단순 시각 규칙. 현재 아트에 맞춘 검출기가 아니므로 오인식할 수 있다.
- **learned**: RGB 화면을 10×6 영역 평균과 직전 화면 변화로 바꾼 뒤, 선형 Q-learning으로 행동 가치를 학습한다. 처음에는 아무 지식도 없다. 훈련 후 체크포인트로 평가할 수 있다.

PR26의 보스 내부 타이머/좌표를 읽는 봇과 다르다. 정책 입력은 PPM 화면 픽셀뿐이다. **보상과 종료 신호는 게임이 알려주며**, 보상은 학습/평가에만 사용한다. 화면에 보이는 HP/문구는 픽셀에 포함되지만 수치 HP, 보스 타이머, 다음 패턴을 관찰 벡터에 넣지 않는다.

**학습형 코드가 있다는 것과 실제 게임을 잘 배웠다는 것은 다르다.** 현재 Python 자체 검사는 완료했고, 실제 Unity 연결/Arakus 학습 성공은 별도 검증 대상이다. CNN, 사람 플레이 모방, 대형 모델, GPU 학습은 구현하지 않았다. 이 작은 모델은 복잡한 보스 전략을 배우기 어려울 수 있다.

## 준비

Python 3.10 이상. 외부 패키지/모델 다운로드/유료 API 없음. 코드 자체는 GPU를 사용하지 않으며 GPU 사용률을 높이거나 사용자 게임 프로세스를 조작하지 않는다.

Unity 개발 빌드는 별도 안전 절차로 만들고, 실제 DB 설정 없는 **독립 빌드와 매번 새로운 격리 데이터 폴더**만 사용한다. 일반 플레이 실행 파일에 자동화를 붙이지 않는다. 같은 교환 폴더에 두 클라이언트를 실행하지 않는다.

## 실행

1. 실험 전용 Unity 빌드를 `-towerPixelAgent <교환폴더>`로 실행한다. 반드시 `-towerDataRoot <격리폴더>`, `-towerNoDda`, `-towerAutoSeconds <벽시계 제한초>`도 지정한다. 교환 폴더는 격리 폴더 하위에 둔다. `-towerAgent`, `-towerDemo`와 함께 사용하지 않는다.
2. 별도 터미널에서 클라이언트를 실행한다. 예:

```powershell
python pixel_agent.py --bridge C:\TowerPixelTest\run-001\bridge --policy random --seed 17 --report random-17.json
python pixel_agent.py --bridge C:\TowerPixelTest\run-002\bridge --policy rule --seed 17 --report rule-17.json
python pixel_agent.py --bridge C:\TowerPixelTest\train-001\bridge --policy learned --train --epsilon 0.3 --checkpoint arakus-q.json --report train-17.json
python pixel_agent.py --bridge C:\TowerPixelTest\eval-001\bridge --policy learned --checkpoint arakus-q.json --seed 101 --report eval-101.json
```

각 명령은 **각기 새로 실행한 게임 한 에피소드**에 연결한다. 체크포인트 파일이 존재하면 다음 훈련에서 이어 학습한다. 평가에서는 `--train`을 빼서 가중치를 변경하지 않는다. 체크포인트 없는 learned 평가를 거부한다. 게임 실행·재시작은 Python이 하지 않으며 사용자 프로세스/키보드를 제어하지 않는다.

기본 제한: 클라이언트 180초/5,000 관찰, 응답 대기 20초. `--max-seconds`, `--max-steps`, `--timeout`으로 조정할 수 있지만 max-seconds 상한 1,800초다. Unity의 벽시계 제한은 클라이언트 한도보다 작게 설정한다. 클라이언트 종료 후에도 Unity 안전 타임아웃이 끝나면 종료되어야 한다. 중간 연결 오류는 실패로 종료하며 성공으로 기록하지 않는다.

## 프로토콜

파일은 UTF-8 JSON과 P6/255 RGB PPM이다. 게임은 프레임 저장 완료 후 `transition.json`을 원자적으로 교체한다.

```json
{"episode":"UUID hex","step":0,"frame":"observation_UUID_0.ppm","reward":0.0,"terminated":false,"truncated":false}
```

정책은 해당 프레임만 읽는다. `episode`와 `step`은 입출력 동기화에만 사용한다. 그 외 텔레메트리는 무시한다. 게임은 대응하는 `action.json`이 도착할 때까지 시뮬레이션을 멈춘다.

```json
{"episode":"UUID hex","step":0,"action":3}
```

행동: `0 idle / 1 left / 2 right / 3 attack / 4 parry / 5 jump / 6 dash`. 매 행동은 게임 기준 4×1/60초 동안 적용된다. 스킬/포션/복합 이동+공격은 현재 행동 집합에서 제외했으므로 기존 내부정보 봇보다 불리할 수 있다. 종료 transition에는 행동을 보내지 않는다. 이전 step 중복은 무시하고, step 누락/다른 episode/위험한 프레임 경로는 거부한다. 프레임 삭제는 게임 담당이며 클라이언트는 삭제하지 않는다.

## 검증 및 해석

```powershell
python -m unittest -v
python synthetic_smoke.py --report synthetic-smoke-results.json
```

`synthetic_smoke.py`는 3색 화면을 보고 정답 행동을 고르는 **인공 색상 과제**다. 씨드 17/29/43 각각 1,200회 훈련 후 학습 없이 420회 평가한다. 환경 API나 픽셀 encoder가 학습 코드에 연결되는지 검사할 뿐 **Arakus 승률도, 게임 실력도 아니다**. 결과 파일은 그 사실을 명시한다.

실제 게임 성능 주장 전 필요한 후속 실험:

1. random/rule/미학습 learned/훈련 learned에 동일한 보스와 난수 씨드, 행동 간격, 전투 시간, DDA OFF를 적용.
2. 훈련 씨드와 다른 평가 씨드 최소 10개, 평가에서 업데이트 OFF.
3. 승률·받은 피해·준 피해·완료 전투시간·프레임/연결 오류·벽시계 잘림을 각각 기록.
4. random보다 좋아져도 사람같은 플레이로 주장하지 않는다. 영상에서 위치 착각/패링 남발/무한 회피를 확인한다.
5. DDA는 학습 환경을 계속 바꾸므로 초기 성능 검증에는 끈다. 향후 난이도별 평가를 별도로 수행한다.

## 안전 실행 도우미 (Windows 전용)

`run_campaign.py`는 검증 대상 독립 Windows 빌드를 지정하면 게임과 클라이언트를 함께 시작한다. 기본 1회, 최대 3회 및 회당 180초 제한이다. 설치/다운로드는 하지 않는다. 시작한 자식 프로세스만 종료하며 기존 게임/에디터 프로세스는 건드리지 않는다.

```powershell
python run_campaign.py --exe C:\TowerPixelBuild\ReversalTower.exe --output C:\TowerPixelResults\random-001 --policy random --episodes 1
python run_campaign.py --exe C:\TowerPixelBuild\ReversalTower.exe --output C:\TowerPixelResults\train-001 --policy learned --train --episodes 3
python run_campaign.py --exe C:\TowerPixelBuild\ReversalTower.exe --output C:\TowerPixelResults\eval-001 --policy learned --checkpoint C:\TowerPixelResults\train-001\checkpoint.json
```

출력 폴더는 새 폴더여야 한다. 격리 데이터 폴더는 시스템 임시 디렉터리에 새로 생성하고 증거 보존을 위해 자동 삭제하지 않는다. 실행 전 Mono 어셈블리의 실험/네트워크 차단 마커를 검사하고 `StreamingAssets/supabase.json`이 있으면 내용을 읽지 않고 실행을 거부한다. Art/Audio는 그대로 보존한다. 이 검사는 서명/보안 감사가 아니며 신뢰하는 이 프로젝트 빌드만 지정해야 한다.

`bridge_error.json`이 있으면 클라이언트는 즉시 실패로 종료한다. 같은 step의 timeout을 새 보상으로 학습하지 않는다. 도우미는 게임 종료 코드·클라이언트 종료 코드·최종 transition을 확인하고 실패를 `campaign.json`에 남긴다. **도우미 실행 경로는 코드/가짜 빌드 검사까지만 검증했으며 실제 Windows Unity 구동 검증은 별도다.**
