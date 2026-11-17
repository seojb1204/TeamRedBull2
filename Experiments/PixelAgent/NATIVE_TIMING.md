# Native clock / rim vs actual hit evaluation
Evaluation-only recorder lives outside Assets in this repository. Copy into an
isolated project only, with -towerTimingLabels and validated -towerExternalInput.
It writes after quit under the new isolated root. Never feed these labels to the
screen policy. No action decision was sent by the visual detector.

Failure preserved: first recorder used Unity Mono Stopwatch as if it shared native
QPC epoch; observed alignment residual ~14,286,262ms. All timing conclusions from
that run are invalid. Do not correct by fitting an offset to the same event.
The corrected recorder calls kernel32 QueryPerformanceCounter/Frequency directly.
The evaluator fails closed above50ms nearest-sample residual.

Actual corrected Windows run:72 RGB frames,516 LateUpdate labels; maximum nearest
sample residual15.164ms. This is approximate compositor/game sample alignment,
not exact render fence correspondence. Instrumentation changes timing overhead.
16 pixel-rim candidates, including4 after cueEta became-1. One visible cue sequence
at game5.10..6.10s ended without a hit. Another begins7.767s; actual hit8.500s,
about0.733s later; rim persists at the hit. Thus outline presence alone cannot
mean 'parry now'. Range, attack phase and projectile travel matter.

Shots are present in frames53..55. Visual review confirms shield projectile and
player hit effect. Boss rim coexists at53..54, disappears by55 while shield remains.
This does NOT isolate a bright blue projectile-only negative: shield at55 is brown.
No claim of robust projectile discrimination, reaction latency, parry success or
unseen-seed generalization. First cue ETA is scheduled attack info, not collision.
Normal external-input mode does not force a seed; no held-out-seed claim.

Policy sees RGB only. Labels and actual hit callbacks are used offline for
validation. Next: independent runs with cue misses, range changes, projectile-only
blue FX, then screen-based timing prediction evaluation before action enablement.
