# Iteration 4: actual pixel imitation pilot

Actual Unity 6000.4.0f1 independent build; hidden Camera.Render / RenderTexture. No HUD/IMGUI.
Two rule demonstrations (seeds301/302) yielded976 transitions and33 visual states. Frequency imitation initializes a tabular action preference, not a general human player.

|Policy|Seed|Boss damage|Outcome|Simulation seconds|Missing actors|
|---|---:|---:|---|---:|---:|
|random-v3|401|40.0|loss|27.22|3.80%|
|random-v3|402|60.0|loss|29.43|5.92%|
|rule-v3|401|690.0|loss|34.72|0.33%|
|rule-v3|402|530.0|loss|26.80|2.15%|
|tabular-v3|401|470.0|loss|21.67|0.26%|
|tabular-v3|402|390.0|loss|29.70|1.21%|

Evaluation seeds401/402 were excluded from demonstrations. DDA OFF, same binary, offscreen capture and60-second wall budget for every policy. Checkpoint hash unchanged. All raw reports and failed-attempt logs retained.

STARTUPINFO-only hiding exposed a game window and was aborted. A hidden parent without message pumping timed out. Own hidden parent plus message pumping completed the six comparison trials without visibility-guard failures.

Small-sample pilot only: cannot establish fun, robust balance, human-like play, or generalization. The earlier black framebuffer samples were never used for learning.
