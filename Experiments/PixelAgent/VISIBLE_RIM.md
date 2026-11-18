# Visible blue rim candidate, shadow only
Detector frozen before separate-run evaluation. It is not a learned model.
Calibration: one actual Windows run (2026-11-17 03:00 UTC), 72 captures;
24 every-third-frame images visually inspected by assistant. Four show blue
boss outline (21,24,66,69), twenty do not. This label is visible outline,
NOT exact imminent attack, parryable hit, or collision truth. Blue attack FX
can outlive windup. No internal state is input to the detector.
Old warning predicate returned true on all24; candidate distinguishes these24.
This is calibration resubstitution, NOT independent accuracy/generalization.
Seven synthetic unit tests pass; synthetic examples are not real-game evidence.
Whole-arena blue projectile/player/FX false positives remain possible. Viewport
other than640x480 rejects. action_authorized always false. Existing Tracker and
behavior policy are unchanged. Next evaluate frozen threshold on a separate run;
do not split adjacent same-run frames and call it independent generalization.
Seed is not controlled for normal external input (towerSeed applies to built-in
agent only), so separate run is not a proven held-out-seed study.
