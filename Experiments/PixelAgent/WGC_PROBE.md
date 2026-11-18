# WGC owned-window probe

Microsoft-supported alternative:
- [CreateForWindow](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createforwindow) targets one HWND (Windows 10 1903+).
- [Microsoft Win32 sample](https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/tree/master/cpp/ScreenCaptureforHWND)
- [Capture overview](https://learn.microsoft.com/en-us/windows/apps/develop/media-authoring-processing/screen-capture)

## Scope and guardrails
wgc_owned_probe.cpp creates/captures ONLY its own red/blue painted fixture. It does
not accept an external HWND, capture the desktop, send global input, activate a
window, disable the capture border, change security settings, install packages or
call a paid API. It checks fixture PID == current PID before targeted messages.

Default mode keeps the fixture outside the virtual desktop. --covered places it
under the opaque foreground window with HWND_BOTTOM and SWP_NOACTIVATE. It refuses
this mode if the foreground is missing/minimized/layered/transparent/too small,
the candidate lies outside the virtual desktop, or input occurred in the last
10 seconds. Foreground and last-input tick must remain unchanged through capture.
No user's window is moved, hidden or terminated. The owned fixture is destroyed.

The probe sends one PostMessage WM_KEYDOWN/WM_KEYUP J pair to its OWN fixture and
checks its own handler received exactly one of each. This is not SendInput,
physical keyboard input, Unity input or gameplay. Unity may ignore this mechanism.

Uses a WARP D3D11 readback device. WGC still uses the Windows compositor; this is
not a claim of zero system GPU work. Run only after coordination with other work.

## Actual results: 2026-11-17
Built with installed MSVC 14.51 / SDK 10.0.26100, C++20. Initial C++17/namespace
compile errors and missing GDI link were corrected, not suppressed.

Historical attempts:
- Hidden STATIC: CreateForWindow HRESULT 0x80070057.
- Off-desktop STATIC and painted fixture: frame delivered, but flat; exit 4.
These are preserved failures, not counted as successful pixels.

Covered painted fixture:
- 320x240 frame; red_pixels=38400, blue_pixels=38400.
- fixture_keydowns=1, fixture_keyups=1.
- foreground_unchanged=1 (also includes unchanged last-input tick).
- exit 0 on two runs including final input-recheck revision.
- No game launched, no global input sent. This verifies native capture of known
  fixture content and scoped Win32 message receipt, NOT game play.
Each run had an external 8-second watchdog that could terminate ONLY its owned
probe. Frame wait is internally bounded to 3 seconds. No watchdog timeout occurred.

## Build / run
From an x64 Visual Studio Developer Command Prompt:
```
cl /nologo /std:c++20 /EHsc /W4 wgc_owned_probe.cpp /Fe:wgc_owned_probe.exe /link d3d11.lib dxgi.lib windowsapp.lib user32.lib gdi32.lib
wgc_owned_probe.exe --covered
```
Use a separate watchdog because WGC initialization/driver calls may block.
Keep binaries/objects/logs out of Git. Linux CI does not compile or execute this
Windows-only probe; native evidence above is a separate check.

## Still missing
Actual Unity capture, resize/device loss, current full-frame UI dataset, production
input transport, screenshot persistence, frame-age/frozen-render tests and game
session lifecycle integration. Never infer gameplay readiness from fixture tests.
Known-color checks are only for this fixture, not general screenshot validity.

Covered mode depends on a stable opaque foreground window. A user moving or
closing it can expose the fixture briefly before the next check. Do not run if
that could interfere with active work; arrange a dedicated visible test window
instead. Last-input tick is not a human-only sequence and can wrap. Runtime bot
authorization still requires full-host/GPU and user-activity checks.
