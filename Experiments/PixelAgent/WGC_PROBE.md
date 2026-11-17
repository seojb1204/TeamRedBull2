# WGC owned-window probe

Microsoft-supported alternative investigated after PrintWindow failed:
- [CreateForWindow](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow) targets a single HWND (Windows 10 1903+).
- [Microsoft Win32 sample](https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/tree/master/cpp/ScreenCaptureforHWND)
- [Capture overview](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)

## Scope
wgc_owned_probe.cpp only creates/captures its own STATIC fixture. It does not accept
an external HWND, capture the desktop, send input, activate a window, disable the
capture border, change security settings, install packages or call a paid API.
It verifies fixture PID == current PID. The fixture is outside the virtual desktop;
any intersection or focus change aborts. It uses a WARP D3D11 readback device.
WGC still uses the Windows compositor; this is not a claim of zero system GPU work.

## Actual results: 2026-11-17
- Built using installed MSVC 14.51 and Windows SDK 10.0.26100, C++20.
- Initial C++17/namespace build errors were corrected, not suppressed.
- Hidden fixture: CreateForWindow rejected with HRESULT 0x80070057.
- Off-desktop nonactivating fixture: received a 320x240 frame, but single-color
  pixels failed the non-flat check. Exit 4, foreground unchanged.
- No game process launched, no Unity capture validated, no input sent.
- Each run was wrapped by an 8-second watchdog targeting ONLY the owned probe.
  Internal frame wait is bounded to 3 seconds. No timeout occurred in these runs.

## Build / run
From an x64 Visual Studio Developer Command Prompt in this folder:
```
cl /nologo /std:c++20 /EHsc /W4 wgc_owned_probe.cpp /Fe:wgc_owned_probe.exe /link d3d11.lib dxgi.lib windowsapp.lib user32.lib
```
Run under a separate watchdog, as WGC initialization/driver calls themselves are
not guaranteed to respect the 3-second frame wait. Keep executables/objects/logs
out of Git.

## Interpretation and next step
Successful frame delivery is NOT successful image capture. Off-desktop composition
may explain blank pixels, but this is a hypothesis, not a demonstrated cause.
Positive validation needs an explicitly scheduled, visible owned test window or
isolated game window, with user work unaffected, known visual content and inspection
of pixels. Do not silently move a fixture onto the user's screen or fall back to
whole-desktop capture. Current probe deliberately remains in Draft and is not wired
to the bot. Production capture still needs identity rechecks, content-size/race
validation, frame-age/frozen-render checks and safe teardown.
