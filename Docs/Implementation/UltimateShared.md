# Shared ultimate mechanics

Branch base: 83d9085. Gameplay remains in TowerGame, no DB/save schema changes.

- Valid Q/W/E BeginAction grants 15 once. DamageBoss's explicit chargeGauge argument prevents duplicate skill charging and delayed ultimate charging. Friendly ultimate projectile checks its persistent pattern tag rather than current player action.
- Casting is the existing complete 0.85s R action, ending earlier on interruption, not just the 0.25s activation windup. Initial R invulnerability .5s remains. Consequently the normal effective reduction window is the last 0.35s; restricting it to pre-hit windup would make it entirely hidden behind existing invulnerability. After it, only active R casting applies one 50% reduction layer after existing cap/card mitigation. The 8s sword buff grants no damage protection. Interruption ends casting/protection, not an already activated 8s sword buff.
- Default R activates sword buff at hit frame (.25s), no instant damage. Successful non-R attacks add attack × .6 × R card multiplier, separately from riposte. Timer uses simulation dt and pauses with gameplay.
- Arakus R spawns grounded SweepEcho; low at .5s = attack×5, high at 1s = attack×7, expiry1.4s. Inherit multiplier applies. Both directions, range6+boss width, low height1/high2.5, high stagger. No outgoing attack creates pattern stats. Barrier handling reuses helper.
- Tutorial integration: SwordCharged and ChargedHits allow observing actual empowered hits. ChargedHits resets at StartBattle. Tutorial must still assert its target was hit and prevent normal rewards.
- F10 fills gauge only in editor/development builds. Tutorial skip must use a distinct key (F11 agreed implementation proposal); exhibition configuration belongs to tutorial owner.
- HUD pulse is cosmetic unscaled time; slot gauge unaffected by sword duration. Effects rendering supplied separately by ArakusEchoView worker.

Validation: run `python Tests/run-ultimate.py --dotnet <SDK executable> --output <isolated evidence dir>`. It copies only source/test shims, no DB or real save directories. Headless results do not prove Unity visuals or Windows build. ArakusSizeTests no longer expects obsolete instant R hit; dedicated ultimate tests cover new timings/range instead.
