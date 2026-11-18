# 역전의탑 규칙 봇 전투 검사

실행 기록: 240회, UnityShim (실제 Unity 화면 아님).

|보스|실력|DDA|전투|승리|사망|시간초과|중앙 전투초|피격 중앙값|
|---|---|---|---:|---:|---:|---:|---:|---:|
|0|balanced|False|8|6|2|0|49.31|6.00|
|0|balanced|True|8|7|1|0|53.83|5.00|
|0|expert|False|8|8|0|0|51.03|2.50|
|0|expert|True|8|7|1|0|52.12|4.00|
|0|novice|False|8|3|5|0|62.52|18.00|
|0|novice|True|8|2|6|0|65.77|18.50|
|1|balanced|False|8|0|8|0|34.56|13.00|
|1|balanced|True|8|0|8|0|52.72|17.50|
|1|expert|False|8|0|8|0|43.70|13.00|
|1|expert|True|8|1|7|0|48.11|12.50|
|1|novice|False|8|0|8|0|29.74|12.00|
|1|novice|True|8|0|8|0|46.11|17.50|
|2|balanced|False|8|1|7|0|43.23|15.00|
|2|balanced|True|8|4|4|0|53.56|16.00|
|2|expert|False|8|4|4|0|53.83|14.00|
|2|expert|True|8|4|4|0|52.14|14.00|
|2|novice|False|8|0|8|0|28.41|13.00|
|2|novice|True|8|0|8|0|34.08|14.50|
|3|balanced|False|8|0|8|0|28.41|10.50|
|3|balanced|True|8|0|8|0|37.67|12.50|
|3|expert|False|8|0|8|0|34.94|10.50|
|3|expert|True|8|0|8|0|49.52|13.00|
|3|novice|False|8|0|8|0|24.31|10.00|
|3|novice|True|8|0|8|0|44.16|15.50|
|4|balanced|False|8|0|8|0|25.45|11.50|
|4|balanced|True|8|0|8|0|28.39|15.00|
|4|expert|False|8|0|8|0|29.22|13.00|
|4|expert|True|8|0|8|0|35.48|15.00|
|4|novice|False|8|0|8|0|23.19|12.00|
|4|novice|True|8|0|8|0|26.43|15.00|

## 해석 제한
- Privileged state rule bots, not human or visual-learning players.
- Fun is not measured. Timeouts, hits and duration only flag cases for human review.
- Rounds share DDA history. Intervals are descriptive, not independent-sample statistical proof.
- Matched initial seeds do not guarantee identical later RNG events under different actions.
- No rendering, collision engine, device latency or Unity build verification.
