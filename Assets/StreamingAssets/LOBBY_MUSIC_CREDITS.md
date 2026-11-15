# Lobby score attribution

**Lost Frontier** by **Kevin MacLeod (incompetech.com)**.

Official source: https://incompetech.com/music/royalty-free/index.html?isrc=USUAN1300039

Licensed under **Creative Commons Attribution 4.0 International**: https://creativecommons.org/licenses/by/4.0/

Official artist listing verified 2026-11-15. The artist does not endorse the game. Keep this file with distributed builds.

The subdued orchestral mystery score was chosen for the ruined tower lobby, contrasting with the more rhythmic boss recordings. This direction is based on the composer's track description; perceptual in-game listening remains a separate QA step.

Adaptation: excerpt starting at 36 seconds, 64 seconds long, two-second linear tail/head overlap yielding a 62-second loop. Converted to 32 kHz stereo PCM16; gain targets RMS 0.12 with peak at most 0.8. Playback uses 0.24 source volume. Original synthesized ambience remains available if the recording is missing. See `Audio/LOBBY_MUSIC_MANIFEST.json` for hashes and measurements, and `Tools/build_lobby_music.py` for reproduction.
