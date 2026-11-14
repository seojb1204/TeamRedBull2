# 역전의탑 규칙 봇 전투 검사

실행 기록: 240회, UnityShim (실제 Unity 화면 아님).

|보스|실력|DDA|전투|승리|사망|시간초과|중앙 전투초|피격 중앙값|
|---|---|---|---:|---:|---:|---:|---:|---:|
|0|balanced|False|8|7|1|0|49.07|5.50|
|0|balanced|True|8|7|1|0|51.96|5.50|
|0|expert|False|8|7|1|0|45.51|1.00|
|0|expert|True|8|8|0|0|48.26|2.00|
|0|novice|False|8|3|5|0|62.52|18.00|
|0|novice|True|8|1|7|0|63.36|19.00|
|1|balanced|False|8|0|8|0|34.56|13.00|
|1|balanced|True|8|2|6|0|45.42|14.00|
|1|expert|False|8|0|8|0|43.70|13.00|
|1|expert|True|8|1|7|0|51.82|13.00|
|1|novice|False|8|0|8|0|29.74|12.00|
|1|novice|True|8|0|8|0|37.85|14.50|
|2|balanced|False|8|0|8|0|48.96|16.00|
|2|balanced|True|8|1|7|0|50.19|16.00|
|2|expert|False|8|5|3|0|54.03|15.50|
|2|expert|True|8|5|3|0|53.20|14.50|
|2|novice|False|8|0|8|0|28.41|13.00|
|2|novice|True|8|0|8|0|37.09|14.50|
|3|balanced|False|8|0|8|0|34.70|11.50|
|3|balanced|True|8|0|8|0|47.54|14.00|
|3|expert|False|8|0|8|0|40.93|12.00|
|3|expert|True|8|0|8|0|50.14|14.00|
|3|novice|False|8|0|8|0|24.31|10.00|
|3|novice|True|8|0|8|0|36.54|13.50|
|4|balanced|False|8|0|8|0|28.99|14.00|
|4|balanced|True|8|0|8|0|30.99|15.00|
|4|expert|False|8|0|8|0|27.34|13.00|
|4|expert|True|8|0|8|0|29.22|15.00|
|4|novice|False|8|0|8|0|23.19|12.00|
|4|novice|True|8|0|8|0|23.91|13.50|

## 해석 제한
- Privileged state rule bots, not human or visual-learning players.
- Fun is not measured. Timeouts, hits and duration only flag cases for human review.
- Rounds share DDA history. Intervals are descriptive, not independent-sample statistical proof.
- Matched initial seeds do not guarantee identical later RNG events under different actions.
- No rendering, collision engine, device latency or Unity build verification.
