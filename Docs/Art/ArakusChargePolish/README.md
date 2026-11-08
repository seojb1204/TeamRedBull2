# Arakus charge polish

Base: main `f13bb6bd25f469e38acce05eceb712712dc30c3f`.
Branch: `fix/arakus-charge-polish`. Independent of DDA graph PR #16.
Scope and Python/Pillow pixel processing were approved; the user confirmed team pre-sharing.
Another team member must review before merge. No merge is authorized.

## ShieldRush

- Move duration: 1.75 -> 1.30 seconds, shared through `ArakusPatternTiming` by simulation and sprite sampling.
- Distance 5m, windup 0.5s, recovery 0.35s, damage 4 and contact interval 0.25s remain unchanged before existing modifiers.
- Rage divides move duration and interval by 1.5: movement is approximately 0.867s.
- Existing DDA modifiers remain unchanged; they modify windup, damage and cooldown, not move duration.
- Keeping the interval while shortening movement means six theoretical scheduled checks instead of seven
  (the asset's count=8 remains an upper bound). Actual damage also depends on overlap and invulnerability.
- The end-crossing frame now places the boss at the exact clamped goal; the old branch could stop just short.
- Native clip frame durations remain unchanged. Runtime `Sample()` selects moving/recovery frames from the actual shared duration.
  Generic Dash, pillar stun timing, attack range, hitboxes and other bosses are unchanged.

## Charge sprite

Scale and import settings were not causing the visual shrink: root scale is always 1.4, with PPU 56 and the same foot pivot.
The original Charge art is much smaller than the 88–89px Idle silhouette. Correct only the five Charge source frames
using proportional 1.25x nearest-neighbor enlargement of their opaque crops, anchored to the original pivot/foot baseline.
This preserves crouch, palette, poses and equipment proportions, with only pixel-grid rounding.

| Frame | Before opaque W×H | After opaque W×H |
|---|---|---|
| 0 | 51×60 | 64×75 |
| 1 | 52×64 | 65×80 |
| 2 | 54×65 | 68×81 |
| 3 | 53×64 | 66×80 |
| 4 | 48×64 | 60×80 |

The sheet remains 960×192, with five 192×192 cells and feet ending at row 159 in every frame.
`Charge.png.meta` is byte-identical to base, preserving GUID, slices, PPU and pivot. No runtime Scale compensation.
`Groggy.png`, its metadata and its existing pose loop/recovery behavior are unchanged.
The 75–81px crouching silhouettes remain shorter than standing Idle. No sword/shield crosses its cell edge.

`Charge-comparison.png` shows original above / corrected below, each beside the same Idle reference.
It is an asset comparison, **not a Unity screenshot**. `metrics.json` contains per-frame bounds.
The complete original remains in the base commit; a separate local backup is also retained outside the project.
An image-generation draft was rejected because it failed exact sheet dimensions; it is not included in the game.

## Validation

Linux .NET 8.0.427 (source allowlist + UnityShim, without CloudSync or real DB configuration):

```sh
python3 Tests/run-arakus-polish.py --dotnet /path/to/dotnet --suite logic
python3 Tests/run-arakus-polish.py --dotnet /path/to/dotnet --suite animation
python3 Tests/run-arakus-polish.py --dotnet /path/to/dotnet --suite polish
python3 Tests/verify-arakus-charge-art.py
```

- Existing main logic: 119 assertions passed.
- Existing animation: 422 assertions passed.
- Added polish: 325 assertions passed, including forced -2..+2 assignments, normal/Rage, 30/60/144fps,
  full arrival, animation boundaries, interval, dash invulnerability, both pillar collisions,
  logical Charge/Groggy/Idle state transitions and another boss's Dash.
- PNG checks passed: palette, exact nearest-neighbor pixels, foot baseline, dimensions, frame boundaries,
  unchanged metadata/Groggy/ActorView. Before/after comparison was visually inspected.
- Changed C# syntax: Roslyn errors 0. This is not Unity API compilation.

Unity Editor/Windows are unavailable here. Unity compilation, actual ActorView/Animator sampling,
input and human dodgeability, Game View captures, PowerShell entrypoints and Windows build/play were not executed.
Headless state checks do not validate rendered character size or animation smoothness.

## Unity 6000.4.0f1 acceptance checks

1. Use an independent copy with actual `Assets/StreamingAssets/supabase.json` excluded **before opening the Editor**.
   Do not change/delete the original configuration or commit its exclusion. Confirm DB is unconfigured;
   SaveStore.Root isolation alone does not disable CloudSync. Protect personal saves.
2. Compile, run existing PowerShell logic/animation checks and clear Console errors.
3. Force `rush` with existing F5/F6 development controls. Compare 5m travel in 1.30s normal / ~0.867s Rage,
   unchanged windup/recovery/contact interval, moving frames stopping at arrival, dash/spacing evasion.
4. Exercise -2..+2 with an isolated controlled fixture; do not add learning/debug changes to production.
   Hardest DDA + Rage windup is ~0.267s, so human reaction/telegraph fairness needs playtesting.
5. Force `charge` from each side into each pillar. Compare windup, sprint, collision, 3.2s groggy,
   and return to idle, including hit-stop/pause. Check feet, body/equipment size, left-facing mirror and no clipping.
6. Check other Arakus attacks, other bosses' Dash, DDA learning/save behavior and Windows build/play.
7. Attach real Game View evidence and obtain one team member's review before merge.
