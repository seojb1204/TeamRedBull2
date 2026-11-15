# Boss music credits

All five recordings below are by **Kevin MacLeod (incompetech.com)** and are licensed under **Creative Commons Attribution 4.0 International**: https://creativecommons.org/licenses/by/4.0/

The official track pages identified below were checked on 2026-11-15. The artist does not endorse this game. Keep this attribution file with distributed builds.

| Boss | Recording | Official source | Art direction |
|---|---|---|---|
| Arakus | Five Armies | https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100875 | Martial orchestral weight for the iron-oath knight. |
| Phanes | Darkest Child | https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100783 | Unsettling, delicate fantasy contrast for deceptive radiance. |
| Adicus | The Descent | https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1200094 | Dark cinematic momentum for chains and judgement. |
| Aethelos | Volatile Reaction | https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1400039 | Forceful orchestral battle at the creator's ruined summit. |
| Kharos | Lightless Dawn | https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1100655 | Ominous atmospheric intensity for the hidden abyss. |

## Adaptations

The original MP3 recordings were converted to 32 kHz stereo PCM16 WAV. Each in-game edit extracts 64 seconds at the offset recorded in `Audio/BOSS_MUSIC_MANIFEST.json`, overlaps the last and first two seconds with a linear crossfade, and adjusts gain toward RMS 0.12 while limiting the peak to at most 0.8. Result: a 62-second loop. These are edited excerpts, not the unmodified recordings or claimed original compositions. Game playback uses a subdued music level to leave room for combat cues.

Reproduction: `python Tools/build_boss_music.py /path/to/official-mp3s` (ffmpeg and numpy required). The manifest records source and output SHA-256, offsets, duration, peak, RMS, and loop seam step. The pre-existing `battle.wav` is preserved as fallback; its original attribution remains in `CREDITS.md`.

Audio signal checks are automated. Perceptual in-game listening, real-device mixing and Unity execution must be checked separately before claiming production audio QA.
