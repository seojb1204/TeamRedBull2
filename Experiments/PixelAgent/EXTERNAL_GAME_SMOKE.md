# Actual external-input game smoke (2026-11-17)

Base: PR49 bd53f31c4b7c7b797a23b5298d89a8016dbfe7c4. Unity 6000.4.0f1,
legacy Input handler 0. This is not main, a release, or a trained screen policy.

## Isolation
Use a separate project copy with live StreamingAssets/supabase.json and its meta
excluded without inspecting their contents. Keep build output inside that copy.
ExternalInput requires a new absolute run directory and finite limit (0,30] seconds.
Report/capture outputs must remain in that directory. Demo, built-in agent, pixel
bridge and lobby fixture flags conflict and are rejected. Raw automation flags
keep CloudSync disabled before configuration/preferences load. The game is muted.
Copy UnityProbe/BuildExternalProbe.cs into the isolated Assets/Editor directory to
run 17 argument checks and BuildProject.Build. Do not commit build-path or config
exclusions from the local verification copy.

## Evidence
- Windows build success: 328152454 bytes; 17 argument cases pass.
- Own spawned HWND only; no SetForegroundWindow/SendInput/global key injection.
- WGC direct Unity-window capture: 640x480, nonflat; foreground unchanged.
- Script: Enter, Enter, Escape, J. Title -> lobby -> battle, tutorial dismissed.
- External J request 1, committed J attack 1, both at battle t=1.033337.
- At 20-second process deadline: battle elapsed 11.60s; HP64; four hits,
  damage received36; dealt0; parries0. No win or autonomous policy claim.
- Hit events: rush4, rush4, cleave18, shield10. Game callbacks provide ground-truth
  evaluation only, not policy observations. Screenshot independently shows combat.
- The normal player's files are not used. Previous source39 script hashes unchanged.

## Differences and limits
Fresh progression, muted audio, target30FPS, 640x480, background nonactivating window,
short wall-clock limit differ from ordinary play. DDA is not explicitly disabled
in the latest run, but fresh history differs from a user's history. Combat timing,
damage, attacks and menu Input calls are unchanged; no invulnerability, healing,
HP correction, unlock fixture, or privileged combat policy. This is transport
smoke, not proof of visual policy quality, human-equivalent timing, or release parity.
The raw evidence initially retained the legacy agent.observation label; the final
source corrects that label only. The callback evidence remains instrumented.

The harness requires opaque foreground coverage and >=10s user inactivity. It
aborts on foreground/input changes. A foreground window moved by another program
could briefly uncover the test; it is not a universal unattended-desktop guarantee.
It only stops the child it creates, and has a bounded child-process watchdog.
Next: multiple timestamped frames, capture staleness/drop tests, screen-only action
selection, and repeated same-condition comparison. Do not use game state as vision.
