# Screen threat / actor shadow experiments
No gameplay or native input change. All feature outputs keep action_authorized=false.
screen_threats consumes only RGB plus capture timestamps; it estimates old tracker
distance, rim onset/present/brightening/fading/absent, and small cyan-component
motion. Horizontal crossing ETA is not collision prediction. Actor detections and
cyan projectiles are unverified. Bad sequence gaps/nonmonotonic time reset/reject.

Actual offline replay of four separate real captures,72 frames each(288 total):
only one small cyan candidate track; actual shield projectile in native-timing run
was NOT tracked. Brown shield/overlap defeats cyan-only tracking. Preserve failure;
do not claim projectile or hit prediction success. Rim fade samples match prior
visual examples but exact attack phase is not inferred.

actor_template is a grayscale normalized-correlation experiment with optional NumPy.
Template: actual labels-20261117-030017 frame0 crop(x172,y305,w30,h47), visually
verified to contain the player. Applied unchanged to heldout-20261117-030442 and
native-timing-20261117-032057,24 frames each. Simple pixel skipping produced phase
aliasing and rejected every candidate in the latter run. Area averaging fixed this
without lowering NCC .65/margin .08 thresholds:22/24 and19/24 accepted respectively.
These are acceptance counts, NOT measured accuracy. Development reused recorded
runs, so this is regression/development evidence, not a blind generalization test.
Hit flashes and actor overlap remain failures; rejected candidates can land on
background. Do not convert rejection into a fallback movement or parry.

Tests: screen features6 synthetic tests and actor matcher3 synthetic tests pass on
Windows. Full pre-actor suite158 tests:157 pass, one existing symlink test errors
with Windows1314 privilege missing. No security setting was changed and the test
was not weakened. Actor tests explicitly skip if optional NumPy unavailable; CI
passing with skipped tests is not evidence of native image-match execution.

Next: labeled multi-pose actor/projectile templates with masks and temporal tracks,
independent run/pose evaluation, negative FX cases; separate screen-based timing
prediction from evaluation-only QPC labels. Do not feed labels to the policy.
