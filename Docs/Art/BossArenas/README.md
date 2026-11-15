# Boss arena art refresh

Five original generated environment images (OpenAI image generation, 2026-11-15), not scraped third-party game art. No third-party attribution is asserted for these generated images. Original output pixels are kept unchanged. Generation brief: side-view dark-fantasy arena, no characters/UI/text, clear horizontal floor, low-contrast battle area; each boss's existing lore determines the architecture and palette. See manifest for measured floor rows and checksums.

- Arakus: iron oath, tarnished throne arches.
- Phanes: false splendor, celestial observatory and broken aureole.
- Adicus: blind judgement, crypt/scale/chain architecture.
- Aethelos: fallen creator, storm-sky forge and shattered sun.
- Kharos: first fallen, eclipse/obsidian/sword grave.

WorldArtist selects by existing boss index only in Battle/Result. World floor remains y=0. Cover fit fills narrow windows; side cropping is intentional. A dark 0.82 multiplier preserves combat contrast, with original boss/telegraph/pillar drawing unchanged. Missing art falls back to original Arakus throne room then existing generic arena. Existing originals are not removed.

Validation: `python Tests/verify-boss-arenas.py`. This is offline asset/projection validation, NOT a Unity render or Windows playtest. Runtime screenshots, import/compile and audible balance must be checked in Unity/Windows before merge/release. AI pixel evaluations must remain on their frozen checkout: new backgrounds change the observation distribution.
