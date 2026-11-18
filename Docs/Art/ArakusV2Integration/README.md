# Arakus 2026-11-13 review art integration candidate

Source: user-requested generated review pack `Arakus_All_Motions_Review_v1.zip` (2026-11-13 generated review pack), SHA256 9b42468b7e961e773a9428a02ecc53811b6be12b6b08223b4cee056f92d02a15.

16 body sheets/77 frames and shield/4 frames are now in a separate ArakusV2 folder. Original ArakusV1 PNG/meta, Animator and clips remain unchanged. Renderer prefers V2 and falls back to V1 per missing sheet. Same 192-cell,56PPU,ground pivot; shield uses its new centered pivot. No gameplay/hitbox/DDA changes.

Gait is driven by actual chase displacement (1.4 world units/cycle), not a fixed wall-time loop. Stationary chase no longer shuffles. Existing four-frame walk art still has limited alternating leg clarity; this fix does not claim to redraw anatomy. Original generated source/visual review explicitly notes equipment variation and stride limitations.

Checks: headless132, animation430, size957 passed before V2 integration. Actual Unity import, final render, all attack alignment and Windows playback still required before release. New appearance changes RGB AI observations; AI baseline builds must remain frozen.
