# Native window observer (2026-11-17)

Read-only first stage for a future live-screen bot. It queries only the explicitly
supplied HWND: PID, process creation FILETIME, executable path, client rectangle,
foreground HWND, minimized state and session last-input tick. It never activates a
window, sends keys, captures pixels or terminates a process.

## Actual Windows check
`python windows_observer_smoke.py` creates its own unshown STATIC popup, queries it,
rejects wrong PID/creation time and six malformed HWND inputs, destroys only that
owned window, then verifies the destroyed handle is rejected. Foreground HWND is
unchanged. On Windows 11 / user's host, 2026-11-17: PASS, 9 rejection cases.

The first test attempt failed because a default overlapped window had a smaller
client area than its outer size. The fixture was corrected to an unshown popup
with a specified 320x240 client area; production checks were not loosened.

## Limits / next gate
- This is native Win32 API validation, NOT Unity play, screenshot accuracy or
  keyboard delivery validation.
- Last-input tick is a 32-bit session value, wraps, and may include injected input.
  It is not a monotonic human-only input sequence. Do not wire it directly into
  InputSafetyGate.user_input_sequence.
- Whole-host process scan and GPU safety are NOT inferred by this module.
- Read/check races remain. Recheck identity before a later capture/input operation.
- Do not use it to authorize global SendInput. A future transport must implement
  session-owned release semantics and resolve foreground/physical-input races.
- Capture needs a separate bounded worker, blank/stale/occlusion checks and actual
  Unity validation. A successful metadata query does not establish visible pixels.
