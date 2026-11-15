# Soundscape lifecycle harness

`dotnet run --project Tests/BossAudioLifecycle/Test.csproj`

Compiles the actual `Assets/Scripts/Soundscape.cs` against intentionally minimal Unity stubs under .NET 8, then invokes its lifecycle via reflection. The stubs model source clip/play/volume and destruction state, not Unity's mixer, audio thread scheduling, DSP or file decoding. This is **not Unity runtime or perceptual audio QA**.

Assertions cover lobby, battle entry, phase-two volume without restart, fade before changing boss, destruction of old boss clip, lobby synthesis return, missing boss fallback, index clamping, non-cached PCM reads, and synthesized carrier fallback when both recordings are unavailable.

2026-11-15: the original source compiled but failed `both missing preserve synth` (clip became null and synthesis was disabled). After the carrier fallback fix, all 11 assertions passed. WAV decode/content checks remain in `Tests/verify_boss_music.py`.

2026-11-15 lobby follow-up: 14 assertions pass, including actual lobby clip selection, lobby playback continuity during boss selection and missing-lobby synthesized fallback. No Unity runtime claim.

ESC regression: 17 assertions pass after separating combat identity from pause state. AudioSource.Pause/UnPause preserve the modeled sample cursor, with no Stop/Play during ESC or settings/help pause; leaving battle while paused still changes to lobby. Unity runtime playback is separately required.
