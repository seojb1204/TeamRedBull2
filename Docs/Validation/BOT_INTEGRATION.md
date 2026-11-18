# Bot QA integration rehearsal

This branch is a draft integration candidate, not a main merge or deployment.

- Base: current main-derived PR47 event evidence.
- Imported: pixel experiment and PR48 bridge limits/HP metrics/input safety adapter preparation.
- Conflicts: .gitignore combined; TowerGame preserves main's skill multipliers, damage reduction, sound, and upgrades.
- HealPlayer records generic evidence and pixel HP change exactly once; PlayerHit records both after clamping actual loss.
- HEADLESS excludes pixel bridge runtime; PixelBridge shim covers bridge separately. Neither proves actual Unity rendering or OS input.
- Existing pixel policies/models/reward remain unchanged. No production save or network configuration is read.
- OS input adapter remains disabled/injected transport only, not connected to Windows or a running game.

Integration gates: Python suite, C# bridge legacy/offscreen/native12, 240-battle logic QA, real Windows independent build later after safe-host check.
Current user PC was gaming at the last check: do not run live input or a second game without a fresh safe-host check.
