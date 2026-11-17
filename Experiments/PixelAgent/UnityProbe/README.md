# Isolated Unity capture and targeted-message probe

## Result (2026-11-17)
Actual Windows standalone **Unity 6000.4.0f1 / Direct3D11 / RTX5080** fixture:
- Direct WGC capture of spawned UnityWndClass: 320x240, red pixels 37956,
  blue pixels 38400. Remaining pixels are the visible text "Unity capture probe J=1".
- Unity Input.GetKeyDown(J)=1; Input.GetKeyUp(J)=1; heldFrames=49.
- One targeted WM_KEYDOWN/WM_KEYUP pair, held approximately 200ms.
- External foreground and session last-input tick unchanged. No global SendInput.
- The temporary Unity process and native probe exited; 11:20:56 KST GPU 0%.
- Screenshot inspected visually. Native capture/input is verified for this fixture,
  NOT for ReversalTower, its current input system, combat, menus or performance.

Earlier failed run is preserved conceptually:
capturing parent host yielded non-flat pixels but zero expected red/blue, and Unity
keydown/up/held were all zero. Parent-host capture and child-message delivery did
not establish success. Direct owned Unity top-level window was required here.

## Files / building
Create a NEW throwaway Unity project, never reuse production game/DB configuration:
- Copy InputCaptureProbe.cs to Assets/
- Copy BuildProbe.cs to Assets/Editor/
- Packages/manifest.json: {"dependencies":{"com.unity.modules.imgui":"1.0.0"}}
- ProjectSettings/ProjectVersion.txt: m_EditorVersion: 6000.4.0f1
- Run installed Unity with -batchmode -nographics -quit -projectPath <new directory>
  -executeMethod BuildProbe.Run -logFile <new build log>.
Result: Build/Probe.exe. No game assets, DB credentials or network client are present.
Fixture auto-quits after 15 seconds and writes only explicit -probe-output location.

Compile wgc_unity_probe.cpp using installed SDK:
```
cl /nologo /std:c++20 /EHsc /W4 wgc_unity_probe.cpp /Fe:wgc_unity_probe.exe /link d3d11.lib dxgi.lib windowsapp.lib user32.lib gdi32.lib
```
Run from a dedicated output directory, under a 30-second outer watchdog:
```
wgc_unity_probe.exe --unity-covered <full-path-to-new-Build/Probe.exe>
```
Only pass this freshly built trusted fixture. The CLI launches the supplied executable.

## Isolation and limitations
Starts the fixture embedded in its own host to avoid startup focus theft; then ONLY
the spawned PID's UnityWndClass is detached and positioned nonactivating at the
bottom under the current opaque foreground window. Never manipulates a user's game.
Only this spawned process can be terminated on shutdown failure. Uses no global input.
Checks recent input, foreground and desktop bounds; current host/game/GPU checks
must still be made by the operator before running. Moving/closing covering windows
can briefly expose the fixture before abort. Not suitable during active user work.

Actual game integration still requires exact build/hash provenance, game input-map
compatibility, complete-host safety collector, session-owned key cleanup, target
destruction/resize/device-loss tests and bounded game lifecycle. No auto-play policy
has been connected here. The legacy Input API fixture is not evidence that Unity's
new Input System or any production configuration accepts these messages.

Large hidden-stage Update counts are not a frame-rate or real-time benchmark.
The initial PPM exporter emitted escaped header newlines; only the header was
corrected when converting retained raw RGB to PNG. Source exporter is fixed and
rebuilt; game was not rerun for that formatting-only change.
