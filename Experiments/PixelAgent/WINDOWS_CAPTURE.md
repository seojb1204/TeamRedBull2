# Target-window capture prototype

Status: **experimental, not wired to gameplay**. Native positive pixel capture is
NOT verified. Do not enable autonomous keyboard input based on this module.

## Implemented
- Target HWND, PID and exact process creation time; pre/post identity/size checks.
- PrintWindow client-only request; never desktop BitBlt, focus activation or input.
- Dedicated spawned worker with 0.05–3s timeout; only this owned worker is terminated.
- Bounded 1920x1080 RGB payload; exact identity types; dimensions/length/time checks;
  flat black/white/other single-color frames rejected.

## Verified on 2026-11-17
Six unit tests pass: valid/corrupt payloads, worker success/failure, bounded timeout,
invalid call arguments. Pixels in these tests are synthetic. This does not test
Unity rendering, accuracy or play.

Actual native test against our own **unshown STATIC window** returned PrintWindow
failure, and the capture was rejected. Changing to the documented client-only flag
and pumping the fixture's window messages did not resolve it. Foreground/user
windows were not activated, captured or modified. The failure reproducer is
windows_capture_smoke.py and should fail until a native positive path works.
This is a known blocker, not a passed capture test.

## Remaining limitations
- PrintWindow support depends on target rendering. Hidden test-window failure does
  not establish whether Unity will work. A separate approved isolated game test
  is still required; no production/native input path exists.
- A successful API return and non-flat pixels do not prove fresh or unoccluded
  semantic content. Repeated-frame and UI confidence checks are still required.
- Timestamps detect transport delay, not frozen render buffers.
- No host/GPU/user-idleness authorization is supplied by this module.
- Next alternative to evaluate: Windows Graphics Capture for an explicitly
  selected owned window, with bounded transfer and the same identity checks.
  Do not silently fall back to capturing the entire user desktop.
