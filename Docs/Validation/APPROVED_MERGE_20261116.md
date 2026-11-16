# Approved integration candidate (2026-11-16)

User chose PR43 comparison GIF's middle walk and requested remaining changes merged. This candidate combines PR29,30,39,40,41,43. PR28 is excluded pending separate experimental-runtime review: it modifies the normal TowerGame entry/update/input path, although activation requires explicit experiment flags.

- Retains exact PR43 V1 Walk PNG and its existing metadata. Renderer selects V1 only for Walk; other motions prefer unchanged V2 assets. V2 source pack is not overwritten.
- Resolves PR29's stale validation gate against current main. Also applies intended Arakus shake synchronization to the now-preferred DrawBossArena path, preserving other bosses' positioning and current background tint/scale. Edge padding uses actual source width.
- PR30 is documentation-only preview assets, not activated combat effects.

Executed here: V2 81-frame contract; room 207/207; size 87/87; charge five-frame exact-pixel contract; approved integration contracts; git diff --check. The initial room check failed because current main's newer arena path still cancelled shake; source was corrected, not assertions removed.

Not executed here: C# suites (.NET SDK absent and official installer download timed out); Unity compilation/rendering; Windows build; sound audition/mix; live DB. PC is occupied by authorized LoRA training, so no Unity task or focus/mouse interaction was started. This candidate is NOT yet verified for main merge/release.
