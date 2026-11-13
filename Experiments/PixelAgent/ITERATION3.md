# Iteration 3: bounded actual-pixel experiment

Status: **not a successful combat-learning bot. Keep Draft PR separate; no production merge.**

## What changed
V2 is preserved. V3 cancels only the environment's -0.001 per-decision penalty in training, records raw and transformed returns separately, and adds consecutive pixel-warning age to the discrete state. The hand-written rule baseline turns toward the detected boss before attacking and waits three observed warning frames before attempting parry when its own input-history cooldown permits. The learned policy has no action mask or forced attack.

Observations remain RGB tracker output plus own submitted-action history, not world state. Post-episode damage/parry metrics are evaluation-only. The renderer pauses while awaiting a decision: this is not human real-time reaction.

## Fixed test design and complete result
Fresh model, 3 training episodes seeds31/32/33; held-out seeds201/202 run once per policy, same isolated binary, DDA off, same maximum time and actions. No failed seed excluded. All9 episodes lost.

| Policy | seed | boss damage | parries | simulation seconds |
|---|---:|---:|---:|---:|
| random |201|107.5|1|39.57|
| random |202|0|0|27.38|
| rule V3 |201|440|0|21.95|
| rule V3 |202|450|1|22.72|
| learned V3 |201|10|1|55.11|
| learned V3 |202|20|0|26.60|

Training damage:177.5/120/100; all losses. Learned mean damage15 is below random53.75 and rule445. Longer survival in one episode did not mean useful combat. The changes did not meet the goal. V2 used different seeds, so this is NOT a causal reward-only ablation against V2.

## Diagnosis
All1421 real training transitions satisfied transformed=raw+0.001 (max floating error1.1e-16). Replay11368 reuses these samples, not extra gameplay. 95.3% of rewards were zero; 186 of262 observed state/action pairs had fewer than5 visits. Defense had little successful evidence:128 parry submissions,84 without warning,66 during the own-input cooldown bin, only1 actual successful parry across training. These counts overlap.

94 of254 attack submissions had own last-movement direction opposite to detected boss direction or unknown. Input history is NOT a measured actual facing direction. Delayed attack outcomes also make credit assignment harder. Pixel tracking, facing, timing and sparse coverage remain limitations; removing the time penalty alone is not enough.

## Verification and budget
Python31 tests pass; unchanged previously compiled Unity experimental binary. Evaluation checkpoint bytes unchanged. 332.176 client wall seconds across9 episodes. Systemwide GPU76samples: peak6%, peak50C, max2082MiB,57.19W; unrelated apps included. Learning is CPU tabular arithmetic. No installs, paidAPI, production settings, DB or original saves changed. Owned game processes ended. Preflight refuses concurrent Unity/game, busy/hot GPU; runner stops its own processes on temperature>=75C or monitoring failure. Training was launched before the later monitor-error hardening; its valid temperature samples were still recorded.

## Reproduce (Windows, isolated experimental Mono build only)
Use the existing run_campaign.py interface with --policy random-v3, rule-v3 or tabular-v3. Training requires --train and a new checkpoint path; V2 checkpoints are rejected. Example:
```
python run_campaign.py --exe <isolated-exe> --output <new-train-dir> --policy tabular-v3 --train --episodes 3 --seconds 120 --seed 31 --epsilon .4
python run_campaign.py --exe <isolated-exe> --output <new-eval-dir> --policy tabular-v3 --checkpoint <train-dir>/checkpoint.json --episodes 2 --seconds 120 --seed 201
```
Repeat evaluation with random-v3 and rule-v3 at seeds201/202 for fair comparison. Actual observed training trajectories are saved next to episode reports, capped at5000transitions. Do not use production DB-configured builds.

## Recommended next decision
Do not spend longer on the same sparse table without better evidence. Prefer a small fixed visual defense/approach curriculum and recorded successful demonstrations before full-combat RL. Verify actual facing from sprite pixels, collect successful parry transitions, and test warning timing separately. Such a curriculum/behavior-cloning stage is proposed, not implemented or demonstrated here. A human-like general screen learner is not complete.
