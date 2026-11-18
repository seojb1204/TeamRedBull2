# Sequential native capture / shadow policy
Base game: PR49 bd53f31 plus external-input isolation patch (7d5dbf2).
Actual Windows run 2026-11-17: 12 distinct 640x480 RGB frames, monotonic
WGC SystemRelativeTime, QPC-computed age 8.1662..89.4622ms. Capture rejects
nonincreasing timestamps and age outside [0,500]ms. This is observed latency,
not a guaranteed bound or performance benchmark.

Same isolated, muted, 20s child/window-only restrictions as EXTERNAL_GAME_SMOKE.md.
The game-entry keys remain scripted. sequence_shadow.py CAPTURE_DIRECTORY runs
Tracker and choose_action on saved RGB only, never telemetry. It proposed right
12 times and dispatched zero actions. This is a hand-authored rule, not learning.

Warning detector found about 9500 cyan pixels around the boss. Blue environment
pixels satisfy its permissive RGB predicate, so this is a false-positive risk.
Per-frame actual attack-warning ground truth has NOT been labeled yet. Do not
report measured false-positive rate, successful parries or an autonomous bot.
Next: label real negative/positive warning frames independently, verify actor
positions, compare detection on held-out frames, then enable bounded actions.

Build C++ like wgc_external_game_probe with MSVC C++20 and d3d11/dxgi/windowsapp/
user32/gdi32. Arguments: --unity-covered ISOLATED_EXE NEW_ABSOLUTE_RUN_DIRECTORY.
Use a new working directory per run (sequence-N.ppm and sequence-capture.log).
The log is harness stdout redirected by caller. Python shadow requires Pillow.
