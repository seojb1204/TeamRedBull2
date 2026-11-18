# Temporal template shadow experiment

No input is authorized. Pure RGB experiments, not game-playing success.
Dependencies: actor_template.py (NumPy); upstream original WGC timestamp and capture age checks remain mandatory. Increasing caller timestamps alone cannot prove freshness.

Three visual crops from labels-20261117-030017 frames 0/6/12, box [172,305,30,47], evaluated on separate native-timing-20261117-032057 run. 72 frames: 58 visual candidates, 54 with at least three temporally consistent observations. These are coverage counts, not accuracy. Two approximate manually inspected visible-player boxes (frames33/35) have IoU0.958 each; not independent annotators, not held-out novel-scene generalization. 14 rejected frames retained.

Continuity parameters (240px/s +6px tolerance, <=0.5s gaps, 3 consecutive observations) are uncalibrated development choices. No rescue of visual rejection, no inference that stable object is player. Multiple templates choosing different objects remains a risk.

Independent review found exceptions preserving prior state and returned dictionaries mutating tracking state. Fixed fail-closed reset on input/matcher exceptions and deep copies. Seven temporal unit tests plus nine existing actor/screen tests passed locally. Synthetic tests do not establish actual-game accuracy.

## Shield experiment: failure preserved
Manually inspected native-timing frame55 shows brown shield around [211,319,23,24]. Template matching against same run (development only, source frame excluded from claims):
- frame53 NCC0.626 rejected despite effect/shield region.
- frame54 NCC0.648 rejected despite shield/effect region.
- frame55 source NCC0.667 accepted (self-match, not independent evidence).
- frame56 NCC0.670 accepted at [192,310,23,24], but full-frame visual inspection shows this is a star-shaped effect, not the shield. Actual shield is near boss x260. This is a confirmed visual false positive.
- frames33/35/57 rejected.
Therefore this template must NOT become projectile tracking or timing input. Brown silhouette + shape/appearance discrimination and separately annotated flight/held-shield/effect negatives are still needed. No threshold relaxed to pass.

Existing local full-suite issue was reproduced: test_ui_calibration_eval.LoaderTests.test_manifest_plain_data_and_paths fails at Windows symlink creation, WinError1314 before escape assertion. No security setting altered and no test skipped or weakened. Prior remote96b501 CI both success; that does not certify Windows native capture.
