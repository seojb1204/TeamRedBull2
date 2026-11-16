# Arakus room / pillar camera shake alignment

## Change
The room previously followed camera.x, cancelling horizontal camera shake while world-space pillars still moved on screen. The room now follows only the zoom-focus baseline used by TowerGame. World pillars remain at x=-4/+4,y=0; ActorView retains independent boss hit recoil. Existing room art width-fit, aspect and ground alignment are unchanged. Outside-only edge-texel gutters cover the small area revealed by horizontal shake, including repeated lower floor strips. No gameplay, collision, DDA, save, network or original art changes.

## Real Unity verification
Unity 6000.4.0f1, isolated copy of main 51f1e1c without live connection settings. ArakusCameraVerify.Run uses an inactive fixture: no gameplay Awake, save loading or network. It invokes production DamageBoss, LateUpdate, ScenePresentation and rendering, writes screenshots and measured coordinates under _verify/arakus-camera, and asserts room/pillar equal screen displacement, fixed pillar world anchors, independent boss recoil, maximum shake edge coverage, zoom focus and room gates for lobby/other bosses/result. Render targets cover 4:3,16:9,21:9. This is controlled renderer verification, not a manual full playthrough or new release build.

Before: recoil-only moves boss +0.17 world, pillars zero. Full hit moves pillars +3.57px horizontally while the room stays horizontally fixed. After: room and pillars both move +3.57px (and the same vertical delta); boss retains additional recoil. At the opposite camera phase they again move together.

## Other checks and limitations
Existing offline room asset/width-fit contracts are retained, with 54 camera/zoom cases added for equal motion and edge coverage. Run Tests/verify-arakus-room-integration.py --output <directory> in a clean checkout with Python UTF-8. The intentionally sanitized Unity copy cannot pass strict whole-checkout integrity checks because live connection files were omitted and its Unity project settings are isolated; report these separately rather than weakening the checks. Windows application-control policy blocked execution of the compiled headless test executable; no workaround was used. Unity compilation and the renderer checks completed successfully. No main merge or release deployment is part of this change.
