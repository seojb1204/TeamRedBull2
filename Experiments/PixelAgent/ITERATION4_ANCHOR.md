# Demonstration-assisted RL validation

Source: 2094e4073e8e06b80af98cf9df78745682ec5ef0
Same isolated Unity build, no DDA, hidden camera-texture capture, max60s each.

|Phase|Seed|Damage|Outcome|Missing fraction|Anchor updates|
|---|---:|---:|---|---:|---:|
|train|601|440.0|loss|0.08958|73|
|train|602|170.0|loss|0.04485|2577|
|development evaluation|401|410.0|loss|0.00298|0|
|development evaluation|402|765.0|loss|0.01780|0|

Development mean damage: 587.5. Prior imitation430, unassistedRL75, visual-rule610, random50.
Evaluation had no training or anchor argument. Model hashes unchanged across evaluation; original imitation and failed RL preserved.
401/402 reused for method development, NOT independent final generalization tests. Only two evaluation episodes. All losses. Teacher-assisted RL, not pure autonomous discovery. No human fun verdict.
No additional experiments; owned game processes exited.