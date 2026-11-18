# Skill FX V1 runtime integration

- Fixed source: patch-gg/pixelfx @ 5294518ce7c72e48b6100a744596e0acf57f2826. License and notices are included beside the runtime textures.
- Assets/Resources/SkillFxV1 contains the three approved PNG sheets, byte-identical to the reviewed previews.
- ScenePresentation attaches SkillEffectVisuals. SkillFxTimeline observes gameplay state; neither class writes combat fields or consumes gameplay randomness.
- Arakus slam: new landing FX replaces only FX_LeapingSlam when its texture is valid. Missing/wrong-size texture retains the old asset. Existing hitboxes, dust Burst, camera and sounds remain.
- W starts at .35 - 6/30 = .15 combat seconds. Source frame6 (first closed ring) aligns with actual W damage at .35.
- E starts at .7 - 11/30 = .333333 combat seconds. Source frame11 (first impact) aligns with actual E damage at .7.
- Source metadata continuous peaks (.17/.335) are deliberately NOT used: discrete PNG sampling would make contact a frame late.
- Effects follow the caster until contact, then anchor in world space. Facing is captured at emission. No character transforms, colliders or Animator assets are modified.
- Pause/hit-stop use unchanged game.seconds so effects freeze together with combat. Pre-impact interruption removes uncommitted windup FX; post-impact trails finish. Lobby/player death/restart clear active FX. A boss-killing blow retains existing FX briefly in Result using realClock so the final contact is not erased; Result never emits new FX.
- Tail is shortened and faded for readability: landing1.05s with final.3 fade, W.65s / E1.2s with final.2 fade. Reviewed source frames are not regenerated or recolored.
- Default sprite PPU: landing44, W19, E24. Sprites are alpha-blended with the owning actor material, not additive. Renderer order is actor+12.
- PNG import: Point, no mipmaps/compression, NPOT resize off, max4096. Runtime slicing avoids editing a scene or creating 180 importer sprite entries.
- ParryFeedback, parry cues, parry materials, player parry animation and combat timing are not edited.

## Validation status

- Source SHA and sheet dimensions/alpha/frame counts verified before integration.
- Pure SkillFxTimeline tests cover windup thresholds, discrete impact frames, pause/no duplicates, cancellation/recast, restart, landing edge, other bosses and fade/frame clamps.
- Actual Unity compilation/rendering and mechanism regression MUST pass before marking the PR ready or producing a release. See final test report; this document alone does not claim Unity verification.
