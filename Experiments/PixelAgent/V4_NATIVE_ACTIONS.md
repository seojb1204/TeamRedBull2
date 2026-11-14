# V4: 정상 게임 버튼을 사용하는 화면 학습 실험 (아직 실제 Unity 검증 전)

사용자 목표는 화면을 보는 AI가 실제로 승리하는 것이다. 현재 V3 최고765 피해도 패배였다.18전 읽기 분석에서 총165피격/355패링입력 중 성공3회였다. 공격잠금 동안 패링·방향전환이 거절되는데 이전정책은 제출입력을 성공한 것으로 기억했고, 인간이 쓸 수 있는 스킬/물약 버튼도 전혀 없었다.

## 변경과 공정성
- `-towerPixelFullActions`를 명시한 독립 실험만12행동 사용: 기존7 + Q/W/E/R/F. F는 정상게임의 기본3물약을 소비하는 입력이다. 체력·데미지·무적·DDA·게임난이도를 보정하지 않는다.
- 정책은 RGB와 자기 제출입력 이력만 받는다. 실제HP·쿨다운·다음패턴은 읽지 않는다. 초기교사(rule-v4)의 F는 자기입력 경과에 따라 최대3번 시도하며 실제회복 성공을 아는 것은 아니다.
- 자기 공격입력 후 예상잠금을 기억하고 그동안 공격/패링/방향전환을 무작정 반복하지 않는다. 경고 시 공격을 멈춘다. 이것은 실제잠금/방향 측정이 아니므로 hitstop·입력거절에 의한 오차가 남는다.
- V4는 상태10개/행동12개 별도스키마, V3모델/원시결과는 보존하며 섞어쓰지 않는다.
- V4 시뮬레이션 시간상한120초(기존V3 60초), 런처벽시계 상한180초. 정상게임에 없는 승리보정이 아니라 시험 종료예산이며 모든V4 기준선은 동일하게 사용한다.
- HUD없는 렌더링은 유지한다. `playerDamage` 기존지표는 시작HP-종료HP의 순손실로, 물약을 쓴 V4에서 누적피해량이라고 해석하지 않는다. 승리/사망, 정상 물약·스킬 입력수와 보스피해를 함께 기록한다.

## 준비된 명령
```powershell
python run_campaign.py --exe <new-isolated-v4-build.exe> --output <new-rule-dir> --policy rule-v4 --seed 701 --episodes 2 --seconds 180 --hidden --offscreen
python imitation_training_v4.py <rule701.trajectory.json> <rule702.trajectory.json> --output <fresh-v4-model.json> --evaluation-seeds 801 802 901 902
python run_campaign.py --exe <same-v4-build.exe> --output <new-eval-dir> --policy tabular-v4 --checkpoint <fresh-v4-model.json> --seed 801 --episodes 2 --seconds 180 --hidden --offscreen
```
같은새빌드/행동군/시간예산의random-v4,rule-v4와 비교한다. 701/702는 시연·개발,801/802는 첫검증,901/902는 후보선택 후 최종검증용으로 아직 쓰지 않는다. 첫승리를 찾더라도 학습시드 승리와 미사용평가시드 승리를 구분한다.

현재는 Python56검사와 C# 브리지 legacy/offscreen25개·native12 26개 검사를 통과한 코드 준비 단계다. 사용자게임 중이므로 PC빌드/실행하지 않았다. 실제승리는 아직 없다. 새단계는 사용자PC 게임/Unity/GPU 안전검사를 통과할 때만 제한횟수로 진행한다.
