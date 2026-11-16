# Boss pattern accents candidate

Five presentation identities: bronze sparks (Arakus), crystal diamond (Phanes), broken chain segments (Adicus), branched lightning (Aethelos), doubled crescents (Kharos). Every general pattern's actual attack-step increment can create a weapon/cast discharge, with stable pattern-specific rotation. Slam landing and delayed hazard activation get separate accents. Warning regions, parry star/rim and damage/DDA fields are unchanged. All accents draw before existing telegraphs, last 0.28 simulated seconds, alpha <=0.5, at most12 concurrent events. Artistically modest vector accents, not replacement hand-painted animations. RGB inputs change; AI baseline builds must stay frozen.

35 standalone C# contract tests passed (five bosses: no windup impact, discharge present, repeat render no duplicate, delayed hazard no early impact, activation present, expiry, result cleanup). This uses test-only Unity/game doubles; it does not validate full Unity compilation/rendering, performance or perceptual quality. Actual PC runtime capture across all patterns and legibility under simultaneous warnings remains required before merge/release.

Run contract: compile Assets/Scripts/WorldArtist.BossAccents.cs with Tests/BossAccentContractTests.cs only as a standalone net8 executable. Do not compile these doubles into Unity. Existing game ignores Tests outside Assets.
