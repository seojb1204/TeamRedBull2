# 실제 시각 시연으로 학습 초기화

V3 무작위 탐색의 공격/방향 학습 실패를 줄이기 위해 `imitation_training.py`를 추가했다. RGB 검출 상태와 자신의 입력 이력에서 실제 시각 규칙 봇이 제출했던 행동 빈도를 학습한다. 내부 HP·위치·패턴·보상은 입력이나 정답으로 사용하지 않는다. 이는 신경망/사람 모방/자율 전략 발견이 아닌 **시각 규칙 시연의 표 기반 행동 모방**이다.

```powershell
python imitation_training.py <rule-301.trajectory.json> <rule-302.trajectory.json> --output cloned-v3.json --evaluation-seeds 401 402
python run_campaign.py --exe <isolated-exe> --output <new-evaluation-folder> --policy tabular-v3 --checkpoint cloned-v3.json --seed 401 --episodes 2 --seconds 60
```

- 완료된 `rule-v3` 기록만 허용한다. 불완전/중복/잘못된 상태·행동/훈련·평가 시드 중복은 거부한다.
- 최대100파일/10만전이, 덮어쓰기 금지. 체크포인트에 출처 SHA256/시드/표본 수를 남긴다.
- 평가 중 학습하지 않는다. 처음 보는 상태는 원래 V3의 seeded random tie를 사용하며 숨은 규칙 fallback은 없다.
- `--train`으로 기존 V3 RL 추가학습도 가능하나, 빈도는 기대수익으로 보정된 Q값이 아니므로 별도 비교 실험이 필요하다. 지금은 모방만 한 평가를 먼저 한다.
- 동일 평가 시드의 random/rule/원래 learned와 비교해야 한다. 시연 수집 시드 성적을 일반화 성능으로 제시하지 않는다.
- 검증: Python43검사, 실제 PixelAgentBridge C# shim24검사와 잘못된 설정 거부 확인. 실제 Unity 결과는 수집 후 별도 기록한다.
